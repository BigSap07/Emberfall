using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Emberfall.Core.Cloud;
using Emberfall.Core.Json;
using Emberfall.Core.Session;

namespace Emberfall.Game.Cloud
{
    /// <summary>
    /// Guilds, over the same plain REST every other service here uses.
    ///
    /// EVERY RULE THIS TOUCHES IS ENFORCED IN THE DATABASE, NOT HERE, and this class is written to
    /// be redundant with 0004 and 0005 on purpose. Only a leader changes a role; only a leader or
    /// an officer accepts; a player cannot be in two guilds; the thirty-member cap holds under two
    /// simultaneous accepts; one message every two seconds. None of that is kept by this file -
    /// it decides what to SHOW and what to say, and `Tools/supabase-guild-rules-test.py` is what
    /// proves the other half holds with this class out of the way entirely.
    ///
    /// WHAT IS CACHED, AND WHY IT IS CACHED HERE. `Current` and `MyRole` are read by four screens
    /// and by every permission decision on each of them, and they change only when this account
    /// joins, leaves, is promoted or is removed. Re-reading them per draw would be a request per
    /// frame; re-reading them per screen would be four requests to answer one question. So the
    /// membership is fetched by Refresh and held, and every call below that CAN change it ends by
    /// refreshing it rather than by patching the cached copy - because a client that edits its own
    /// cached role is a client that can disagree with the database about what it is allowed to do.
    ///
    /// Coroutines with callbacks, like the services beside it. There is no async/await in this
    /// project and this is not the place to introduce one.
    /// </summary>
    public sealed class GuildService
    {
        private readonly SupabaseClient _client;
        private readonly AccountService _account;
        private readonly CloudLocalState _state;

        public GuildService(SupabaseClient client, AccountService account, CloudLocalState state)
        {
            _client = client;
            _account = account;
            _state = state;
        }

        /// <summary>The guild this account is in, or null. Null is the ordinary state, not an
        /// error: most players are in no guild and the screen has a whole face for it.</summary>
        public GuildSummary Current { get; private set; }

        /// <summary>This account's role in `Current`, or None when there is no guild.</summary>
        public GuildRole MyRole { get; private set; }

        /// <summary>True once Refresh has answered at least once, so a screen can tell "not in a
        /// guild" from "we have not asked yet" - which look identical in the fields above and
        /// want completely different empty states.</summary>
        public bool Known { get; private set; }

        public void Forget()
        {
            Current = null;
            MyRole = GuildRole.None;
            Known = false;
            _lastTouchUnix = 0;
        }

        // ================================================================== membership

        /// <summary>
        /// Who this account is, guild-wise: one request for the membership row, and a second for
        /// the guild itself only when there is one.
        ///
        /// Two requests rather than an embed. `guild_members` could carry `guilds(...)` inline,
        /// but the guild row is also read on its own by the settings card after an edit, and a
        /// second code path that produced the same object differently is the thing that drifts.
        /// The second request only happens for a player who is in a guild.
        /// </summary>
        public IEnumerator Refresh(Action<string> done)
        {
            if (!_account.SignedIn) { Report(done, "not signed in"); yield break; }

            string error = "";
            string guildId = "";
            GuildRole role = GuildRole.None;

            yield return _client.Send("GET",
                "rest/v1/guild_members?select=guild_id,role&user_id=eq." + _state.UserId, null,
                delegate (SupabaseResponse response)
                {
                    if (!response.Ok) { error = response.Error; return; }
                    JsonValue root;
                    string parseError;
                    if (!JsonValue.TryParse(response.Body, out root, out parseError) || root == null
                        || !root.IsArray || root.Count == 0) return;
                    guildId = root[0].GetString("guild_id", "");
                    role = GuildRules.ParseRole(root[0].GetString("role", ""));
                });

            if (!string.IsNullOrEmpty(error)) { Report(done, error); yield break; }

            if (string.IsNullOrEmpty(guildId))
            {
                Current = null;
                MyRole = GuildRole.None;
                Known = true;
                Report(done, "");
                yield break;
            }

            yield return _client.Send("GET",
                "rest/v1/guilds?select=id,name,description,guild_code,is_open&id=eq." + guildId, null,
                delegate (SupabaseResponse response)
                {
                    if (!response.Ok) { error = response.Error; return; }
                    List<GuildSummary> rows = GuildCodec.ReadRows(response.Body, GuildCodec.ReadGuild);
                    if (rows.Count > 0) Current = rows[0];
                });

            if (!string.IsNullOrEmpty(error)) { Report(done, error); yield break; }
            MyRole = role;
            Known = true;
            Report(done, "");
        }

        // ================================================================== the roster

        /// <summary>
        /// Every member, with their name, role, presence and the power of the line they published.
        ///
        /// TWO REQUESTS, AND THE SECOND ONE IS THE POINT. The roster and its names come back
        /// together because PostgREST resolves `guild_members -> profiles` inline. The power score
        /// lives in `team_snapshots`, which is a different table with a different policy, and it
        /// is fetched by a second filtered read rather than a nested embed - partly because a
        /// three-table chain is the kind of embed that starts answering ambiguously the first time
        /// another foreign key is added, and partly because doing it this way EXERCISES the policy
        /// 0006 added: if a guildmate could not read a guildmate's snapshot, this request would
        /// come back with fewer rows than the roster and the screen would say so.
        ///
        /// A member with no published line keeps PowerScore -1, which is not zero. Zero is a
        /// number a real snapshot can hold; -1 is "there is no snapshot", and the row says
        /// "NO LINE" rather than claiming they field nothing.
        /// </summary>
        public IEnumerator ListRoster(Action<List<GuildMemberEntry>, string> done)
        {
            if (!_account.SignedIn) { if (done != null) done(null, "not signed in"); yield break; }
            if (Current == null) { if (done != null) done(new List<GuildMemberEntry>(), ""); yield break; }

            List<GuildMemberEntry> members = null;
            string error = "";

            const string select = "select=user_id,role,joined_at,last_seen,"
                                + "profiles!guild_members_user_id_fkey(display_name,friend_code)";
            yield return _client.Send("GET",
                "rest/v1/guild_members?" + select + "&guild_id=eq." + Current.Id
                + "&order=role.desc,joined_at.asc", null,
                delegate (SupabaseResponse response)
                {
                    if (!response.Ok) { error = response.Error; return; }
                    string self = _state.UserId;
                    members = GuildCodec.ReadRows(response.Body,
                        delegate (JsonValue row) { return GuildCodec.ReadMember(row, self); });
                });

            if (members == null) { if (done != null) done(null, error); yield break; }
            Current.MemberCount = members.Count;

            if (members.Count > 0)
            {
                StringBuilder ids = new StringBuilder();
                for (int i = 0; i < members.Count; i++)
                {
                    if (ids.Length > 0) ids.Append(',');
                    ids.Append(members[i].UserId);
                }

                yield return _client.Send("GET",
                    "rest/v1/team_snapshots?select=user_id,power_score&user_id=in.(" + ids + ")", null,
                    delegate (SupabaseResponse response)
                    {
                        // A failure here is not a failure of the roster. The names, roles and
                        // presence are already in hand and are what the screen is mostly for; the
                        // power column simply stays blank.
                        if (!response.Ok) return;
                        JsonValue root;
                        string parseError;
                        if (!JsonValue.TryParse(response.Body, out root, out parseError)
                            || root == null || !root.IsArray) return;
                        List<JsonValue> rows = root.AsList();
                        for (int i = 0; i < rows.Count; i++)
                        {
                            string id = rows[i].GetString("user_id", "");
                            int power = rows[i].GetInt("power_score", 0);
                            for (int m = 0; m < members.Count; m++)
                                if (members[m].UserId == id) members[m].PowerScore = power;
                        }
                    });
            }

            if (done != null) done(members, "");
        }

        // ================================================================== requests

        /// <summary>
        /// Who is waiting to be let in. Readable only by a leader or an officer - the policy
        /// filters rather than refuses, so a member calling this gets an empty list rather than an
        /// error, and the screen is what decides not to offer it at all.
        /// </summary>
        public IEnumerator ListRequests(Action<List<GuildRequestEntry>, string> done)
        {
            if (!_account.SignedIn) { if (done != null) done(null, "not signed in"); yield break; }
            if (Current == null) { if (done != null) done(new List<GuildRequestEntry>(), ""); yield break; }

            const string select = "select=id,user_id,created_at,"
                                + "profiles!guild_requests_user_id_fkey(display_name,friend_code)";
            yield return _client.Send("GET",
                "rest/v1/guild_requests?" + select + "&guild_id=eq." + Current.Id
                + "&order=created_at.asc", null,
                delegate (SupabaseResponse response)
                {
                    if (!response.Ok) { if (done != null) done(null, response.Error); return; }
                    if (done != null)
                        done(GuildCodec.ReadRows(response.Body, GuildCodec.ReadRequest), "");
                });
        }

        /// <summary>
        /// The requests THIS account has out, for the no-guild screen.
        ///
        /// Read with the guild embedded, because a player waiting on an answer wants to see which
        /// guild they asked - and the guild row is readable to them for exactly as long as the
        /// request exists, which is what `has_guild_request` in the read policy is for.
        /// </summary>
        public IEnumerator ListMyRequests(Action<List<GuildSummary>, string> done)
        {
            if (!_account.SignedIn) { if (done != null) done(null, "not signed in"); yield break; }

            const string select = "select=id,created_at,guilds(id,name,description,guild_code,is_open)";
            yield return _client.Send("GET",
                "rest/v1/guild_requests?" + select + "&user_id=eq." + _state.UserId, null,
                delegate (SupabaseResponse response)
                {
                    if (!response.Ok) { if (done != null) done(null, response.Error); return; }

                    List<GuildSummary> guilds = new List<GuildSummary>();
                    JsonValue root;
                    string parseError;
                    if (JsonValue.TryParse(response.Body, out root, out parseError)
                        && root != null && root.IsArray)
                    {
                        List<JsonValue> rows = root.AsList();
                        for (int i = 0; i < rows.Count; i++)
                        {
                            GuildSummary guild = GuildCodec.ReadGuild(rows[i]["guilds"]);
                            // The request id travels on the summary so a Withdraw button has
                            // something to name. It is the only place these two are carried
                            // together, and the alternative was a fourth row type for one field.
                            if (guild != null)
                            {
                                guild.PendingRequestId = rows[i].GetString("id", "");
                                guilds.Add(guild);
                            }
                        }
                    }
                    if (done != null) done(guilds, "");
                });
        }

        public IEnumerator AcceptRequest(string requestId, Action<bool, string> done)
        {
            if (!_account.SignedIn) { Finish(done, false, "not signed in"); yield break; }

            JsonValue body = JsonValue.NewObject();
            body.Set("p_request_id", requestId);
            yield return _client.Send("POST", "rest/v1/rpc/guild_request_accept", body.ToJson(false),
                delegate (SupabaseResponse response)
                {
                    Finish(done, response.Ok, response.Error);
                });
        }

        /// <summary>
        /// Declining and withdrawing are the same delete, from the two sides that are allowed to
        /// make it. One row going away covers both, which is why there is no Decline call beside
        /// this - two names for one database effect would imply two outcomes.
        /// </summary>
        public IEnumerator RemoveRequest(string requestId, Action<bool, string> done)
        {
            if (!_account.SignedIn) { Finish(done, false, "not signed in"); yield break; }
            yield return _client.Send("DELETE", "rest/v1/guild_requests?id=eq." + requestId, null,
                delegate (SupabaseResponse response) { Finish(done, response.Ok, response.Error); });
        }

        // ================================================================== joining and leaving

        /// <summary>
        /// Resolves a typed code to a guild.
        ///
        /// The code is normalised and refused HERE when it is not a code at all, so the player is
        /// told "that is not a guild code" rather than "no guild found" - two different problems
        /// with two different fixes, which is the same reason the friends screen looks a code up
        /// before sending anything.
        /// </summary>
        public IEnumerator LookupByCode(string typed, Action<GuildSummary, string> done)
        {
            string code = GuildRules.NormaliseCode(typed);
            if (!GuildRules.IsValidCode(code))
            {
                if (done != null) done(null, "a guild code is six characters from " + FriendCode.Alphabet);
                yield break;
            }
            if (!_account.SignedIn) { if (done != null) done(null, "not signed in"); yield break; }

            JsonValue body = JsonValue.NewObject();
            body.Set("p_code", code);
            yield return _client.Send("POST", "rest/v1/rpc/guild_by_code", body.ToJson(false),
                delegate (SupabaseResponse response)
                {
                    if (!response.Ok) { if (done != null) done(null, response.Error); return; }
                    List<GuildSummary> rows = GuildCodec.ReadRows(response.Body, GuildCodec.ReadGuild);
                    if (rows.Count == 0)
                    {
                        if (done != null) done(null, "no guild has the code " + GuildRules.FormatCode(code));
                        return;
                    }
                    if (done != null) done(rows[0], "");
                });
        }

        /// <summary>
        /// Creates one, with this account as its leader.
        ///
        /// The name and description are validated here so a player is stopped before a round trip,
        /// and the TRIMMED values are what get sent - the column refuses an untrimmed name, so
        /// sending what was typed would fail on something the player cannot see.
        /// </summary>
        public IEnumerator Create(string name, string description, Action<GuildSummary, string> done)
        {
            if (!_account.SignedIn) { if (done != null) done(null, "not signed in"); yield break; }
            if (!GuildRules.CanJoinOrCreate(MyRole))
            {
                if (done != null) done(null, "you are already in a guild");
                yield break;
            }

            string cleanName, cleanBlurb, problem;
            if (!GuildRules.ValidateName(name, out cleanName, out problem))
            {
                if (done != null) done(null, problem);
                yield break;
            }
            if (!GuildRules.ValidateDescription(description, out cleanBlurb, out problem))
            {
                if (done != null) done(null, problem);
                yield break;
            }

            GuildSummary made = null;
            string error = "";
            yield return _client.Send("POST", "rest/v1/rpc/guild_create",
                GuildCodec.WriteCreateBody(cleanName, cleanBlurb),
                delegate (SupabaseResponse response)
                {
                    if (!response.Ok) { error = response.Error; return; }
                    JsonValue root;
                    string parseError;
                    if (JsonValue.TryParse(response.Body, out root, out parseError))
                        made = GuildCodec.ReadGuild(root);
                });

            if (made == null) { if (done != null) done(null, error); yield break; }
            yield return Refresh(null);
            if (done != null) done(made, "");
        }

        /// <summary>
        /// Asks to join, by code.
        ///
        /// Answers with one word - "joined" or "requested" - because that is what the database
        /// answers and it is genuinely the guild's decision rather than the player's: an open
        /// guild admits on arrival and a closed one queues, from the same tap.
        /// </summary>
        public IEnumerator RequestJoin(string typed, Action<string, string> done)
        {
            if (!_account.SignedIn) { if (done != null) done("", "not signed in"); yield break; }
            if (!GuildRules.CanJoinOrCreate(MyRole))
            {
                if (done != null) done("", "you are already in a guild");
                yield break;
            }

            string code = GuildRules.NormaliseCode(typed);
            if (!GuildRules.IsValidCode(code))
            {
                if (done != null) done("", "a guild code is six characters from " + FriendCode.Alphabet);
                yield break;
            }

            JsonValue body = JsonValue.NewObject();
            body.Set("p_code", code);

            string outcome = "";
            string error = "";
            yield return _client.Send("POST", "rest/v1/rpc/guild_request_send", body.ToJson(false),
                delegate (SupabaseResponse response)
                {
                    if (!response.Ok) { error = response.Error; return; }
                    JsonValue root;
                    string parseError;
                    if (JsonValue.TryParse(response.Body, out root, out parseError) && root != null)
                        outcome = root.AsString("");
                });

            if (!string.IsNullOrEmpty(error)) { if (done != null) done("", error); yield break; }
            if (outcome == "joined") yield return Refresh(null);
            if (done != null) done(outcome, "");
        }

        /// <summary>
        /// Walks out. Refused by the database for a leader, and refused here first so the button
        /// can explain rather than fail - `guild_members_leave_self` carries `role &lt;&gt;
        /// 'leader'`, which is the whole of "transfer or disband first".
        /// </summary>
        public IEnumerator Leave(Action<bool, string> done)
        {
            if (!_account.SignedIn) { Finish(done, false, "not signed in"); yield break; }
            if (Current == null) { Finish(done, false, "you are not in a guild"); yield break; }
            if (!GuildRules.CanLeave(MyRole))
            {
                Finish(done, false, "a leader has to hand the guild over or disband it first");
                yield break;
            }

            bool ok = false;
            string error = "";
            yield return _client.Send("DELETE",
                "rest/v1/guild_members?user_id=eq." + _state.UserId, null,
                delegate (SupabaseResponse response) { ok = response.Ok; error = response.Error; });

            if (ok) yield return Refresh(null);
            Finish(done, ok, error);
        }

        /// <summary>Ends the guild. The cascades take its members, its requests and its chat; the
        /// policy is what stops anybody but the leader reaching this.</summary>
        public IEnumerator Disband(Action<bool, string> done)
        {
            if (!_account.SignedIn) { Finish(done, false, "not signed in"); yield break; }
            if (Current == null) { Finish(done, false, "you are not in a guild"); yield break; }
            if (!GuildRules.CanDisband(MyRole))
            {
                Finish(done, false, "only the leader can disband a guild");
                yield break;
            }

            bool ok = false;
            string error = "";
            yield return _client.Send("DELETE", "rest/v1/guilds?id=eq." + Current.Id, null,
                delegate (SupabaseResponse response) { ok = response.Ok; error = response.Error; });

            if (ok) yield return Refresh(null);
            Finish(done, ok, error);
        }

        // ================================================================== roles

        public IEnumerator SetRole(string userId, GuildRole role, Action<bool, string> done)
        {
            if (!_account.SignedIn) { Finish(done, false, "not signed in"); yield break; }
            if (!GuildRules.CanSetRoles(MyRole))
            {
                Finish(done, false, "only the leader can change a role");
                yield break;
            }
            if (role != GuildRole.Member && role != GuildRole.Officer)
            {
                Finish(done, false, "leadership is handed over, not assigned");
                yield break;
            }

            JsonValue body = JsonValue.NewObject();
            body.Set("p_user", userId);
            body.Set("p_role", GuildRules.RoleKey(role));
            yield return _client.Send("POST", "rest/v1/rpc/guild_set_role", body.ToJson(false),
                delegate (SupabaseResponse response) { Finish(done, response.Ok, response.Error); });
        }

        /// <summary>
        /// Hands the guild over. Refreshes afterwards because this is the one call that changes
        /// the CALLER's own role - they become an officer - and a screen still drawing leader
        /// buttons after it would be offering actions the database now refuses.
        /// </summary>
        public IEnumerator TransferLeadership(string userId, Action<bool, string> done)
        {
            if (!_account.SignedIn) { Finish(done, false, "not signed in"); yield break; }
            if (!GuildRules.CanTransferLeadership(MyRole))
            {
                Finish(done, false, "only the leader can hand the guild over");
                yield break;
            }

            JsonValue body = JsonValue.NewObject();
            body.Set("p_user", userId);

            bool ok = false;
            string error = "";
            yield return _client.Send("POST", "rest/v1/rpc/guild_transfer_leadership", body.ToJson(false),
                delegate (SupabaseResponse response) { ok = response.Ok; error = response.Error; });

            if (ok) yield return Refresh(null);
            Finish(done, ok, error);
        }

        public IEnumerator RemoveMember(string userId, GuildRole theirRole, Action<bool, string> done)
        {
            if (!_account.SignedIn) { Finish(done, false, "not signed in"); yield break; }
            if (!GuildRules.CanRemove(MyRole, theirRole, userId == _state.UserId))
            {
                Finish(done, false, "your role does not allow that");
                yield break;
            }
            yield return _client.Send("DELETE", "rest/v1/guild_members?user_id=eq." + userId, null,
                delegate (SupabaseResponse response) { Finish(done, response.Ok, response.Error); });
        }

        // ================================================================== settings

        /// <summary>
        /// The two columns a leader holds an UPDATE grant on, written together because the screen
        /// edits them together. `name` and `guild_code` are absent from the grant entirely, so
        /// there is no version of this call that could rename a guild.
        /// </summary>
        public IEnumerator SaveSettings(string description, bool isOpen, Action<bool, string> done)
        {
            if (!_account.SignedIn) { Finish(done, false, "not signed in"); yield break; }
            if (Current == null) { Finish(done, false, "you are not in a guild"); yield break; }
            if (!GuildRules.CanEditSettings(MyRole))
            {
                Finish(done, false, "only the leader can change a guild's settings");
                yield break;
            }

            string cleaned, problem;
            if (!GuildRules.ValidateDescription(description, out cleaned, out problem))
            {
                Finish(done, false, problem);
                yield break;
            }

            JsonValue body = JsonValue.NewObject();
            body.Set("description", cleaned);
            body.Set("is_open", isOpen);

            bool ok = false;
            string error = "";
            yield return _client.Send("PATCH", "rest/v1/guilds?id=eq." + Current.Id, body.ToJson(false),
                delegate (SupabaseResponse response) { ok = response.Ok; error = response.Error; });

            if (ok) yield return Refresh(null);
            Finish(done, ok, error);
        }

        // ================================================================== chat

        /// <summary>
        /// The newest hundred, whole.
        ///
        /// Newest first is what the index can answer; `GuildChatLog` turns it the right way up.
        /// The whole window every time rather than a cursor - the reasoning is on GuildChatLog,
        /// and it is about a sequence handing out ids in the order they are asked for rather than
        /// the order rows commit in.
        /// </summary>
        public IEnumerator FetchChat(Action<List<GuildMessageEntry>, string> done)
        {
            if (!_account.SignedIn) { if (done != null) done(null, "not signed in"); yield break; }
            if (Current == null) { if (done != null) done(new List<GuildMessageEntry>(), ""); yield break; }

            const string select = "select=id,user_id,body,created_at,"
                                + "profiles!guild_messages_user_id_fkey(display_name)";
            string path = "rest/v1/guild_messages?" + select + "&guild_id=eq." + Current.Id
                        + "&order=id.desc&limit=" + GuildRules.ChatWindow;

            yield return _client.Send("GET", path, null, delegate (SupabaseResponse response)
            {
                if (!response.Ok) { if (done != null) done(null, response.Error); return; }
                string self = _state.UserId;
                if (done != null)
                    done(GuildCodec.ReadRows(response.Body,
                        delegate (JsonValue row) { return GuildCodec.ReadMessage(row, self); }), "");
            });
        }

        /// <summary>
        /// Sends one.
        ///
        /// A 429 is turned into a sentence rather than surfaced as a failure, because from the
        /// player's side "you are typing faster than the room allows" is an answer and the message
        /// is still in the box. The limit itself is `guild_post_message`'s and holds whatever this
        /// does.
        /// </summary>
        public IEnumerator PostMessage(string typed, Action<bool, string> done)
        {
            if (!_account.SignedIn) { Finish(done, false, "not signed in"); yield break; }
            if (Current == null) { Finish(done, false, "you are not in a guild"); yield break; }

            string cleaned, problem;
            if (!GuildRules.ValidateMessage(typed, out cleaned, out problem))
            {
                Finish(done, false, problem);
                yield break;
            }

            JsonValue body = JsonValue.NewObject();
            body.Set("p_body", cleaned);
            yield return _client.Send("POST", "rest/v1/rpc/guild_post_message", body.ToJson(false),
                delegate (SupabaseResponse response)
                {
                    if (response.Ok) { _lastTouchUnix = TimeUtil.NowUnix(); Finish(done, true, ""); return; }
                    Finish(done, false, response.IsRateLimited
                        ? "one message every " + GuildRules.ChatCooldownSeconds + " seconds"
                        : response.Error);
                });
        }

        // ================================================================== presence

        private long _lastTouchUnix;

        /// <summary>Seconds between presence stamps. A guild screen polls every few seconds and
        /// the stamp is read as "online / 12m ago / 3d ago", so anything finer than a minute is a
        /// write per poll buying a distinction nobody can see.</summary>
        private const long TouchIntervalSeconds = 60;

        /// <summary>
        /// Stamps this account as present, at most once a minute.
        ///
        /// Called by the guild screens while they are open and by the coordinator after a sync, so
        /// "last seen" means what the column comment says it means: when this player's device last
        /// reached the server. Posting a message stamps it too, in the same statement that stores
        /// the message, so somebody mid-conversation never reads as away.
        ///
        /// Silent about everything. A presence stamp that reported a failure would put a toast on
        /// a screen for something no player asked for and none can act on.
        /// </summary>
        public IEnumerator Touch()
        {
            if (!_account.SignedIn || Current == null) yield break;
            long now = TimeUtil.NowUnix();
            if (_lastTouchUnix != 0 && now - _lastTouchUnix < TouchIntervalSeconds) yield break;
            _lastTouchUnix = now;
            yield return _client.Send("POST", "rest/v1/rpc/guild_touch", "{}",
                delegate (SupabaseResponse response) { });
        }

        // ================================================================== plumbing

        private static void Finish(Action<bool, string> done, bool ok, string message)
        {
            if (done != null) done(ok, message == null ? "" : message);
        }

        private static void Report(Action<string> done, string error)
        {
            if (done != null) done(error == null ? "" : error);
        }
    }
}
