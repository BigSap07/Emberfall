using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Emberfall.Core.Cloud;
using Emberfall.Core.Json;

namespace Emberfall.Game.Cloud
{
    /// <summary>One other player, as a friend list shows them.</summary>
    public sealed class FriendEntry
    {
        /// <summary>The friendships row, which is what accepting or removing names.</summary>
        public string FriendshipId = "";

        public string UserId = "";
        public string DisplayName = "";
        public string FriendCode = "";

        /// <summary>"pending" or "accepted", straight from the row.</summary>
        public string Status = "";

        /// <summary>True when the other player asked and this account has not answered. The only
        /// case in which Accept is offered - the database refuses it for anybody else, so this
        /// flag decides what to draw rather than what is allowed.</summary>
        public bool AwaitingMyAnswer;
    }

    /// <summary>
    /// Friend codes, requests, and fetching the line a friend has published.
    ///
    /// Every rule this touches is enforced in the database, not here: only the addressee can
    /// accept, a snapshot is unreadable until a friendship is accepted, and neither party can
    /// rewrite who a friendship is between. This class is the client half of those rules and it
    /// is written to be redundant with them on purpose - it decides what to SHOW, and the
    /// database decides what is allowed. Tools/supabase-rules-test.py is what proves the second
    /// half holds with this class out of the way entirely.
    /// </summary>
    public sealed class SocialService
    {
        private readonly SupabaseClient _client;
        private readonly AccountService _account;
        private readonly CloudLocalState _state;

        public SocialService(SupabaseClient client, AccountService account, CloudLocalState state)
        {
            _client = client;
            _account = account;
            _state = state;
        }

        // ------------------------------------------------------------------ finding somebody

        /// <summary>
        /// Resolves a typed friend code to an account.
        ///
        /// The code is normalised first - upper-cased, spaces and dashes dropped - and a code
        /// that is not in the alphabet is refused HERE rather than sent. That is not an
        /// optimisation: it is what lets the player be told "that is not a valid code" instead of
        /// "no player found", which are different problems with different fixes.
        /// </summary>
        public IEnumerator LookupByFriendCode(string typed, Action<AccountProfile, string> done)
        {
            string code = FriendCode.Normalise(typed);
            if (!FriendCode.IsValid(code))
            {
                if (done != null) done(null, "a friend code is six characters from " + FriendCode.Alphabet);
                yield break;
            }
            if (!_account.SignedIn) { if (done != null) done(null, "not signed in"); yield break; }

            string path = "rest/v1/profiles?select=user_id,display_name,friend_code&friend_code=eq." + code;
            yield return _client.Send("GET", path, null, delegate (SupabaseResponse response)
            {
                if (!response.Ok) { if (done != null) done(null, response.Error); return; }

                JsonValue root;
                string error;
                if (!JsonValue.TryParse(response.Body, out root, out error) || root == null
                    || !root.IsArray || root.Count == 0)
                {
                    if (done != null) done(null, "no player has the code " + FriendCode.Format(code));
                    return;
                }

                JsonValue row = root[0];
                AccountProfile profile = new AccountProfile();
                profile.UserId = row.GetString("user_id", "");
                profile.DisplayName = row.GetString("display_name", "");
                profile.FriendCode = row.GetString("friend_code", "");

                if (profile.UserId == _state.UserId)
                {
                    if (done != null) done(null, "that is your own friend code");
                    return;
                }
                if (done != null) done(profile, "");
            });
        }

        // ------------------------------------------------------------------ the friend list

        /// <summary>
        /// Every friendship this account is part of, pending or accepted, with the other player's
        /// name and code attached.
        ///
        /// One request, not two. PostgREST resolves the two foreign keys into auth.users through
        /// the profiles table when the embed names the constraint, so the response already
        /// carries both parties' profiles and there is no second round trip and no N+1.
        ///
        /// The row does not say which side "the other player" is - it says requester and
        /// addressee - so that is worked out here against this account's own id, which is also
        /// what decides whether a pending request is one to answer or one to wait on.
        /// </summary>
        public IEnumerator ListFriends(Action<List<FriendEntry>, string> done)
        {
            if (!_account.SignedIn) { if (done != null) done(null, "not signed in"); yield break; }

            const string select = "select=id,requester,addressee,status,created_at,"
                                + "requester_profile:profiles!friendships_requester_fkey(user_id,display_name,friend_code),"
                                + "addressee_profile:profiles!friendships_addressee_fkey(user_id,display_name,friend_code)";
            yield return _client.Send("GET", "rest/v1/friendships?" + select + "&order=created_at.desc", null,
                delegate (SupabaseResponse response)
                {
                    if (!response.Ok) { if (done != null) done(null, response.Error); return; }

                    List<FriendEntry> entries = new List<FriendEntry>();
                    JsonValue root;
                    string error;
                    if (!JsonValue.TryParse(response.Body, out root, out error) || root == null || !root.IsArray)
                    {
                        if (done != null) done(entries, "");
                        return;
                    }

                    List<JsonValue> rows = root.AsList();
                    for (int i = 0; i < rows.Count; i++)
                    {
                        JsonValue row = rows[i];
                        if (row == null || !row.IsObject) continue;

                        string requester = row.GetString("requester", "");
                        string addressee = row.GetString("addressee", "");
                        bool iAmAddressee = addressee == _state.UserId;

                        JsonValue other = iAmAddressee ? row["requester_profile"] : row["addressee_profile"];
                        FriendEntry entry = new FriendEntry();
                        entry.FriendshipId = row.GetString("id", "");
                        entry.Status = row.GetString("status", "");
                        entry.UserId = iAmAddressee ? requester : addressee;
                        if (other != null && other.IsObject)
                        {
                            entry.DisplayName = other.GetString("display_name", "");
                            entry.FriendCode = other.GetString("friend_code", "");
                        }
                        entry.AwaitingMyAnswer = iAmAddressee && entry.Status == "pending";
                        entries.Add(entry);
                    }
                    if (done != null) done(entries, "");
                });
        }

        /// <summary>
        /// Asks somebody to be a friend.
        ///
        /// A duplicate in either direction is refused by the unique index on the unordered pair,
        /// which arrives as a 409. It is turned into a sentence here rather than surfaced as a
        /// conflict, because from the player's side "you two are already connected" is an answer,
        /// not a failure.
        /// </summary>
        public IEnumerator SendRequest(string otherUserId, Action<bool, string> done)
        {
            if (!_account.SignedIn) { Finish(done, false, "not signed in"); yield break; }
            if (string.IsNullOrEmpty(otherUserId) || otherUserId == _state.UserId)
            {
                Finish(done, false, "that is not somebody you can add");
                yield break;
            }

            JsonValue body = JsonValue.NewObject();
            body.Set("requester", _state.UserId);
            body.Set("addressee", otherUserId);
            body.Set("status", "pending");

            yield return _client.Send("POST", "rest/v1/friendships", body.ToJson(false),
                delegate (SupabaseResponse response)
                {
                    if (response.Ok) { Finish(done, true, ""); return; }
                    Finish(done, false, response.IsConflict
                        ? "you already have a request with that player"
                        : response.Error);
                });
        }

        /// <summary>
        /// Accepts a request. Only the addressee can, and only from pending - both halves are the
        /// database's rule, not this method's. A client that sent this for somebody else's row
        /// gets a response that updated nothing.
        /// </summary>
        public IEnumerator Accept(string friendshipId, Action<bool, string> done)
        {
            if (!_account.SignedIn) { Finish(done, false, "not signed in"); yield break; }

            JsonValue body = JsonValue.NewObject();
            body.Set("status", "accepted");
            yield return _client.Send("PATCH", "rest/v1/friendships?id=eq." + friendshipId, body.ToJson(false),
                delegate (SupabaseResponse response) { Finish(done, response.Ok, response.Error); });
        }

        /// <summary>
        /// Removes a friendship, whatever state it is in. One row deleted covers three player
        /// actions - cancelling a request, declining one, and unfriending - which is why there is
        /// no decline and no cancel beside this.
        /// </summary>
        public IEnumerator Remove(string friendshipId, Action<bool, string> done)
        {
            if (!_account.SignedIn) { Finish(done, false, "not signed in"); yield break; }
            yield return _client.Send("DELETE", "rest/v1/friendships?id=eq." + friendshipId, null,
                delegate (SupabaseResponse response) { Finish(done, response.Ok, response.Error); });
        }

        // ------------------------------------------------------------------ team snapshots

        /// <summary>
        /// Publishes this account's line so friends can fight it.
        ///
        /// An upsert - PostgREST's merge-duplicates resolution, which is INSERT ... ON CONFLICT DO
        /// UPDATE on the primary key. One row per player, always overwritten, never appended to:
        /// a snapshot is what the line IS, and a history of lines is a different feature with
        /// different privacy consequences.
        ///
        /// updated_at is not sent. The database stamps it in a trigger, because a friend reads it
        /// to judge whether the fight on offer is current, and a client that could write it could
        /// claim a freshness it does not have.
        /// </summary>
        public IEnumerator PublishSnapshot(TeamSnapshot snapshot, Action<bool, string> done)
        {
            if (!_account.SignedIn) { Finish(done, false, "not signed in"); yield break; }
            if (snapshot == null) { Finish(done, false, "nothing to publish"); yield break; }

            JsonValue body = JsonValue.NewObject();
            body.Set("user_id", _state.UserId);
            body.Set("label", snapshot.Label);
            body.Set("power_score", snapshot.PowerScore);
            body.Set("units", TeamSnapshotCodec.WriteUnits(snapshot));

            yield return _client.Send("POST", "rest/v1/team_snapshots?on_conflict=user_id", body.ToJson(false),
                "resolution=merge-duplicates,return=minimal",
                delegate (SupabaseResponse response) { Finish(done, response.Ok, response.Error); });
        }

        /// <summary>
        /// Fetches a friend's published line.
        ///
        /// An empty result is the interesting case and it has two causes that look identical from
        /// here: the friend has not published a line, or the friendship is not accepted so the
        /// row is invisible. RLS filters rather than refuses - that is what makes it RLS and not a
        /// permission error - so both arrive as zero rows and the message says both.
        /// </summary>
        public IEnumerator FetchSnapshot(string userId, Action<TeamSnapshot, string> done)
        {
            if (!_account.SignedIn) { if (done != null) done(null, "not signed in"); yield break; }

            string path = "rest/v1/team_snapshots?select=user_id,label,power_score,units,updated_at"
                        + "&user_id=eq." + userId;
            yield return _client.Send("GET", path, null, delegate (SupabaseResponse response)
            {
                if (!response.Ok) { if (done != null) done(null, response.Error); return; }

                JsonValue root;
                string error;
                if (!JsonValue.TryParse(response.Body, out root, out error) || root == null
                    || !root.IsArray || root.Count == 0)
                {
                    if (done != null) done(null, "that player has no line published, or you are not friends yet");
                    return;
                }

                JsonValue row = root[0];
                TeamSnapshot snapshot = TeamSnapshotCodec.Read(row["units"],
                                                               row.GetString("label", ""),
                                                               row.GetInt("power_score", 0));
                if (done != null) done(snapshot, "");
            });
        }

        private static void Finish(Action<bool, string> done, bool ok, string message)
        {
            if (done != null) done(ok, message == null ? "" : message);
        }
    }
}
