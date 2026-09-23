using System;
using System.Collections;
using UnityEngine;
using Emberfall.Core.Cloud;
using Emberfall.Core.Json;
using Emberfall.Core.Session;

namespace Emberfall.Game.Cloud
{
    /// <summary>What a signed-in player's own profile row says.</summary>
    public sealed class AccountProfile
    {
        public string UserId = "";
        public string DisplayName = "";
        public string FriendCode = "";
    }

    /// <summary>
    /// Signing in, staying signed in, and attaching an email to an account that started without
    /// one.
    ///
    /// The shape of the whole system is in one sentence: THE FIRST LAUNCH WITH A NETWORK CREATES
    /// AN ANONYMOUS USER SILENTLY, AND THE PLAYER NEVER SEES A LOGIN WALL. A login wall in front
    /// of a player who has nothing yet is a wall in front of nothing; the account exists so the
    /// save has somewhere to go, and the player finds out about it when they want to move to
    /// another device or add a friend.
    ///
    /// Linking an email later keeps the SAME auth.users id. That is what makes the whole design
    /// hold together: profiles, saves, team_snapshots and friendships all key off that id, so
    /// converting an anonymous account into a permanent one migrates nothing, and the save the
    /// player has been accumulating for a week is simply already theirs.
    ///
    /// Everything here is a coroutine with a callback. There is no async/await in this project
    /// and this is not the place to introduce one.
    /// </summary>
    public sealed class AccountService
    {
        private readonly SupabaseClient _client;
        private readonly CloudLocalState _state;

        /// <summary>Unix seconds at which the current access token stops being accepted. Zero when
        /// there is no token. Refreshed a minute early - see EnsureFreshToken.</summary>
        private long _expiresAtUnix;

        /// <summary>True while a refresh is in flight. A refresh token is single-use: GoTrue
        /// rotates it on every exchange, so two requests that both took a 401 and both presented
        /// the same token would be a reuse, which the server may answer by revoking the session
        /// (CLOUD-GAPS S3). A second caller waits for the first refresh and shares its answer.</summary>
        private bool _refreshing;
        private bool _lastRefreshOk;
        private string _lastRefreshError = "";

        public AccountService(SupabaseClient client, CloudLocalState state)
        {
            _client = client;
            _state = state;
            _client.RenewSession = RenewAfterUnauthorised;
        }

        public CloudLocalState State { get { return _state; } }

        public bool SignedIn { get { return !string.IsNullOrEmpty(_state.UserId) && !string.IsNullOrEmpty(_client.AccessToken); } }

        /// <summary>True once an email is attached. The ONE thing this gates is whether signing
        /// out is offered: an anonymous account whose refresh token is discarded cannot be
        /// reached again by anyone, including its owner.</summary>
        public bool CanSignOutSafely { get { return SignedIn && !_state.IsAnonymous; } }

        // ------------------------------------------------------------------ session restore

        /// <summary>
        /// The whole of what the game does about accounts at boot.
        ///
        /// Three outcomes, in order of how common they are: a stored refresh token is exchanged
        /// for a fresh session; no token exists so an anonymous user is created; or there is no
        /// network, or no config, and nothing happens at all. The third is not an error - the
        /// game is offline-first and a player with no account plays exactly as before - so it
        /// reports through the callback and logs nothing alarming.
        /// </summary>
        public IEnumerator Resume(Action<bool, string> done)
        {
            if (!_client.Config.Configured)
            {
                Finish(done, false, _client.Config.Problem);
                yield break;
            }

            if (!string.IsNullOrEmpty(_state.RefreshToken))
            {
                bool refreshed = false;
                bool tokenDead = false;
                string error = "";
                yield return (Refresh(delegate (bool ok, string message, bool dead)
                {
                    refreshed = ok;
                    error = message;
                    tokenDead = dead;
                }));

                if (refreshed) { Finish(done, true, ""); yield break; }

                // A refresh fails for two different reasons and they need different answers. If
                // the AUTH SERVER said no - the token was revoked, or the anonymous user was
                // cleaned up - the token is dead and the right move is a new anonymous account.
                // Anything else - no signal, a captive portal, or the gateway refusing a rotated
                // anon key before the token was even looked at - leaves the token as good as it
                // was, and throwing it away would strand a real account, so the sign-in is simply
                // deferred. Which one it was is TokenRefusal's rule, read off the status and
                // GoTrue's error code, never off the sentence (Docs/CLOUD-GAPS.md P8).
                if (!tokenDead) { Finish(done, false, error); yield break; }

                Debug.LogWarning("Emberfall: the stored session was rejected (" + error
                    + "), starting a new anonymous account.");
                _state.ClearAccount();
            }

            yield return (SignInAnonymously(done));
        }

        /// <summary>
        /// A brand-new user with no credentials. GoTrue's /signup with neither an email nor a
        /// phone is the anonymous sign-in; the project setting "Allow anonymous sign-ins" has to
        /// be on, and the error when it is not says so plainly enough to pass straight through.
        /// </summary>
        public IEnumerator SignInAnonymously(Action<bool, string> done)
        {
            yield return (_client.Send("POST", "auth/v1/signup", "{}",
                delegate (SupabaseResponse response)
                {
                    if (!response.Ok) { Finish(done, false, response.Error); return; }
                    if (!AdoptSession(response.Body, true, "")) { Finish(done, false, "sign-in returned no session"); return; }
                    Finish(done, true, "");
                }));
        }

        /// <summary>
        /// Attaches an email and password to the account that is already signed in.
        ///
        /// A PUT to /auth/v1/user with the caller's own access token, which is an UPDATE of the
        /// existing user rather than a new sign-up - the user id does not move, so nothing the
        /// player owns has to be migrated or re-pointed.
        ///
        /// If the project requires email confirmation, the password takes effect immediately and
        /// the email stays pending until the link is clicked; the account is not fully
        /// recoverable until then, and the callback says so rather than claiming success.
        /// </summary>
        public IEnumerator LinkEmail(string email, string password, Action<bool, string> done)
        {
            if (!SignedIn) { Finish(done, false, "not signed in"); yield break; }
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                Finish(done, false, "an email and a password are both needed");
                yield break;
            }

            JsonValue body = JsonValue.NewObject();
            body.Set("email", email.Trim());
            body.Set("password", password);

            yield return (_client.Send("PUT", "auth/v1/user", body.ToJson(false),
                delegate (SupabaseResponse response)
                {
                    if (!response.Ok) { Finish(done, false, response.Error); return; }

                    JsonValue user;
                    string error;
                    string confirmed = "";
                    if (JsonValue.TryParse(response.Body, out user, out error) && user != null && user.IsObject)
                    {
                        confirmed = user.GetString("email", "");
                        // GoTrue reports a pending address in new_email until it is confirmed.
                        string pending = user.GetString("new_email", "");
                        if (!string.IsNullOrEmpty(pending))
                        {
                            _state.Email = pending;
                            _state.Save();
                            Finish(done, true, "check " + pending + " and click the link to finish linking - "
                                             + "the account is not recoverable by email until you do");
                            return;
                        }
                    }

                    _state.Email = string.IsNullOrEmpty(confirmed) ? email.Trim() : confirmed;
                    _state.IsAnonymous = false;
                    _state.Save();
                    Finish(done, true, "");
                }));
        }

        /// <summary>
        /// Signs in with an email that was linked earlier, from anywhere.
        ///
        /// This is the path that makes a second device reach the same account and the same save:
        /// the user id that comes back is the id the anonymous account was created with, so the
        /// saves row, the team snapshot and every friendship are the same rows.
        ///
        /// It REPLACES whatever account this device was using. If that account was anonymous, the
        /// caller has to have warned the player first, because an anonymous account abandoned here
        /// is gone - which is why ClearAccount is called rather than merged into.
        /// </summary>
        public IEnumerator SignInWithEmail(string email, string password, Action<bool, string> done)
        {
            JsonValue body = JsonValue.NewObject();
            body.Set("email", email == null ? "" : email.Trim());
            body.Set("password", password == null ? "" : password);

            yield return (_client.Send("POST", "auth/v1/token?grant_type=password", body.ToJson(false),
                delegate (SupabaseResponse response)
                {
                    if (!response.Ok) { Finish(done, false, response.Error); return; }

                    string previousUser = _state.UserId;
                    if (!AdoptSession(response.Body, false, email)) { Finish(done, false, "sign-in returned no session"); return; }

                    // A different account means this device's sync history belongs to somebody
                    // else's row. Dropping the base revision forces the next sync to treat the
                    // cloud save as unrelated to whatever is local - which is a conflict, which
                    // is a question for the player, which is correct.
                    if (previousUser != _state.UserId)
                    {
                        _state.BaseRevision = 0;
                        _state.LastSyncUnix = 0;
                        _state.Save();
                    }
                    Finish(done, true, "");
                }));
        }

        /// <summary>
        /// Exchanges the stored refresh token for a fresh access token. Called at boot and
        /// whenever a request is about to be made with a token that is nearly expired.
        /// </summary>
        public IEnumerator RefreshSession(Action<bool, string> done)
        {
            yield return (Refresh(delegate (bool ok, string message, bool dead) { Finish(done, ok, message); }));
        }

        /// <summary>
        /// The refresh itself. Its third answer is whether the auth server declared the token
        /// dead (TokenRefusal.TokenIsDead), which only Resume acts on. It is handed back rather
        /// than kept in a field so a refresh running at the same moment cannot overwrite it.
        /// </summary>
        private IEnumerator Refresh(Action<bool, string, bool> done)
        {
            if (string.IsNullOrEmpty(_state.RefreshToken)) { done(false, "no stored session", false); yield break; }

            JsonValue body = JsonValue.NewObject();
            body.Set("refresh_token", _state.RefreshToken);

            yield return (_client.Send("POST", "auth/v1/token?grant_type=refresh_token", body.ToJson(false),
                delegate (SupabaseResponse response)
                {
                    if (!response.Ok)
                    {
                        done(false, response.Error, TokenRefusal.TokenIsDead(response.Status, response.ErrorCode));
                        return;
                    }
                    if (!AdoptSession(response.Body, _state.IsAnonymous, _state.Email))
                    {
                        // A 2xx that is not a session - a captive portal, a proxy - is a device that
                        // did not reach the auth server, not a server refusing the token, so it is
                        // never a dead token (Docs/CLOUD-GAPS.md P5).
                        done(false, "the answer to the sign-in did not carry a session", false);
                        return;
                    }
                    done(true, "", false);
                }));
        }

        /// <summary>
        /// Refreshes if the token is within a minute of expiring, then calls back.
        ///
        /// A minute rather than zero because the clock that decides is the DEVICE's, and a phone
        /// whose clock is slightly fast would otherwise send a token the server has already
        /// stopped accepting and take a 401 for no reason.
        ///
        /// A clock that runs more than a minute SLOW is the case this cannot catch: it sends a
        /// token the server has already expired. SupabaseClient.Send catches that one instead,
        /// with one refresh through RenewAfterUnauthorised and one resend (CLOUD-GAPS P13).
        /// </summary>
        public IEnumerator EnsureFreshToken(Action<bool, string> done)
        {
            if (!SignedIn) { Finish(done, false, "not signed in"); yield break; }
            if (_expiresAtUnix == 0 || TimeUtil.NowUnix() < _expiresAtUnix - 60)
            {
                Finish(done, true, "");
                yield break;
            }
            yield return (RefreshOnce(done));
        }

        /// <summary>
        /// SupabaseClient.RenewSession: called by Send when a request carrying `rejectedToken` was
        /// answered 401. If the session has already moved on since that request left - another
        /// request renewed it - there is nothing to refresh, and the resend simply uses the new
        /// token. Otherwise one refresh, shared with anyone else asking at the same moment.
        ///
        /// A failure here clears nothing. Whether a refused refresh token means the account is
        /// gone is Resume's question, asked at boot with its own rules; a request that happened to
        /// meet an expired token mid-session is not the place to decide it.
        /// </summary>
        private IEnumerator RenewAfterUnauthorised(string rejectedToken, Action<bool, string> done)
        {
            if (!string.IsNullOrEmpty(_client.AccessToken) && _client.AccessToken != rejectedToken && !_refreshing)
            {
                Finish(done, true, "");
                yield break;
            }
            yield return (RefreshOnce(done));
        }

        /// <summary>RefreshSession, with at most one in flight. See _refreshing.</summary>
        private IEnumerator RefreshOnce(Action<bool, string> done)
        {
            if (_refreshing)
            {
                while (_refreshing) yield return null;
                Finish(done, _lastRefreshOk, _lastRefreshError);
                yield break;
            }

            _refreshing = true;
            bool ok = false;
            string error = "";
            yield return (RefreshSession(delegate (bool success, string message) { ok = success; error = message; }));
            _lastRefreshOk = ok;
            _lastRefreshError = error;
            _refreshing = false;
            Finish(done, ok, error);
        }

        /// <summary>
        /// Ends the session and forgets the account on this device.
        ///
        /// Refuses outright while the account is anonymous. This is not a UI nicety that the
        /// screen could choose to skip: an anonymous account is reachable ONLY through the
        /// refresh token stored on this device, so discarding it destroys the player's access to
        /// their own cloud save with nothing that can recover it. The screen warns; this refuses.
        /// </summary>
        public IEnumerator SignOut(Action<bool, string> done)
        {
            if (!SignedIn) { Finish(done, false, "not signed in"); yield break; }
            if (_state.IsAnonymous)
            {
                Finish(done, false, "this account has no email attached, so signing out would lose it "
                                  + "for good - link an email first");
                yield break;
            }

            yield return (_client.Send("POST", "auth/v1/logout", "{}",
                delegate (SupabaseResponse response)
                {
                    // Local state is cleared whatever the server said. A logout that failed to
                    // reach the server still has to mean "this device is signed out", or a player
                    // on a bad connection cannot get out of an account at all. The token expires
                    // on its own regardless.
                    _client.AccessToken = null;
                    _expiresAtUnix = 0;
                    _state.ClearAccount();
                    Finish(done, true, response.Ok ? "" : "signed out locally; the server was not reached ("
                                                        + response.Error + ")");
                }));
        }

        // ------------------------------------------------------------------ the profile row

        /// <summary>
        /// This account's display name and friend code. Created by the database's own trigger the
        /// instant the user was, so a signed-in user with no profile row is a broken invariant
        /// rather than a case to handle - it reports as an error instead of being papered over.
        /// </summary>
        public IEnumerator FetchOwnProfile(Action<AccountProfile, string> done)
        {
            if (!SignedIn) { if (done != null) done(null, "not signed in"); yield break; }

            string path = "rest/v1/profiles?select=user_id,display_name,friend_code&user_id=eq." + _state.UserId;
            yield return (_client.Send("GET", path, null,
                delegate (SupabaseResponse response)
                {
                    if (!response.Ok) { if (done != null) done(null, response.Error); return; }

                    JsonValue root;
                    string error;
                    if (!JsonValue.TryParse(response.Body, out root, out error) || root == null
                        || !root.IsArray || root.Count == 0)
                    {
                        if (done != null) done(null, "this account has no profile row");
                        return;
                    }

                    JsonValue row = root[0];
                    AccountProfile profile = new AccountProfile();
                    profile.UserId = row.GetString("user_id", "");
                    profile.DisplayName = row.GetString("display_name", "");
                    profile.FriendCode = row.GetString("friend_code", "");
                    if (done != null) done(profile, "");
                }));
        }

        /// <summary>Renames the account. display_name is the only column a client holds an UPDATE
        /// grant on, so this is the whole of what this call can possibly do.</summary>
        public IEnumerator SetDisplayName(string name, Action<bool, string> done)
        {
            if (!SignedIn) { Finish(done, false, "not signed in"); yield break; }

            string trimmed = (name == null ? "" : name).Trim();
            if (trimmed.Length < 1 || trimmed.Length > 24)
            {
                Finish(done, false, "a display name is 1 to 24 characters");
                yield break;
            }

            JsonValue body = JsonValue.NewObject();
            body.Set("display_name", trimmed);
            string path = "rest/v1/profiles?user_id=eq." + _state.UserId;
            yield return (_client.Send("PATCH", path, body.ToJson(false),
                delegate (SupabaseResponse response)
                {
                    Finish(done, response.Ok, response.Error);
                }));
        }

        // ------------------------------------------------------------------ internals

        /// <summary>
        /// Takes a GoTrue token response and becomes that session.
        ///
        /// One place does this, for all four ways a session can arrive - anonymous sign-up, email
        /// sign-in, refresh, and the session embedded in a sign-up response - so there is one
        /// definition of what being signed in means and no path that sets half of it.
        /// </summary>
        private bool AdoptSession(string body, bool anonymous, string email)
        {
            JsonValue root;
            string error;
            if (!JsonValue.TryParse(body, out root, out error) || root == null || !root.IsObject) return false;

            string access = root.GetString("access_token", "");
            string refresh = root.GetString("refresh_token", "");
            if (string.IsNullOrEmpty(access) || string.IsNullOrEmpty(refresh)) return false;

            JsonValue user = root["user"];
            string userId = user != null && user.IsObject ? user.GetString("id", "") : "";
            string userEmail = user != null && user.IsObject ? user.GetString("email", "") : "";
            bool serverSaysAnonymous = user != null && user.IsObject && user.Has("is_anonymous")
                ? user.GetBool("is_anonymous", anonymous)
                : string.IsNullOrEmpty(userEmail) && anonymous;
            if (string.IsNullOrEmpty(userId)) return false;

            _client.AccessToken = access;
            // expires_in is seconds from now; expires_at, when present, is absolute. Prefer the
            // absolute one - it is the server's own answer and does not drift with how long the
            // response spent in flight.
            long absolute = root.GetLong("expires_at", 0);
            long lifetime = root.GetLong("expires_in", 3600);
            _expiresAtUnix = absolute > 0 ? absolute : TimeUtil.NowUnix() + lifetime;

            _state.UserId = userId;
            _state.RefreshToken = refresh;
            // The server's own answer decides whether this is still an anonymous account: an
            // account that linked an email on another device comes back with one, and believing
            // the local flag over that would keep offering to link an email that already exists.
            //
            // ONLY the server's answer. The email passed in may be an address still waiting for its
            // confirmation link - LinkEmail stores the pending one - and this used to clear
            // IsAnonymous whenever any email was known, so the first refresh after an unconfirmed
            // link enabled Sign Out on an account nobody could sign back into. GoTrue's
            // is_anonymous is read when present; otherwise a confirmed `email` on the user is the
            // evidence. See Docs/CLOUD-GAPS.md.
            _state.IsAnonymous = serverSaysAnonymous;
            _state.Email = !string.IsNullOrEmpty(userEmail) ? userEmail : (email == null ? "" : email.Trim());
            _state.Save();
            return true;
        }

        private static void Finish(Action<bool, string> done, bool ok, string message)
        {
            if (done != null) done(ok, message == null ? "" : message);
        }
    }
}
