using System;
using System.Collections;
using System.Collections.Generic;
using Emberfall.Core.Cloud;
using Emberfall.Core.Json;
using Emberfall.Core.Session;

namespace Emberfall.Game.Cloud
{
    /// <summary>
    /// Everything the guild dungeon's screens and the guild chat ask of a server, as one surface.
    ///
    /// AN INTERFACE, AND THE SECOND IMPLEMENTATION IS THE REASON. The live one below talks to
    /// Supabase. The other lives in the editor capture tool (GuildDungeonShots) and holds a guild of
    /// several members in memory, so the whole loop - attempt, board, share, watch, try it yourself -
    /// can be driven through the real screens on a scratch copy of a real save without a second
    /// live account. Every record either one hands back has been through GuildDungeon.ReadRow, so
    /// the screens read the same shape either way.
    ///
    /// Coroutines with callbacks, like every service beside it. A callback's string is empty on
    /// success and one sentence on failure.
    /// </summary>
    public interface IGuildDungeonBackend
    {
        /// <summary>True when there is a signed-in member of a guild to ask on behalf of.</summary>
        bool Ready { get; }

        /// <summary>Why not, in one sentence, when Ready is false.</summary>
        string NotReadyReason { get; }

        string SelfUserId { get; }
        string GuildName { get; }

        IEnumerator Status(Action<GuildDungeonStatus, string> done);

        /// <summary>Every attempt anybody in the guild made this cycle, names included.</summary>
        IEnumerator ListAttempts(int cycle, Action<List<GuildDungeonAttempt>, string> done);

        /// <summary>Stores one attempt. The record handed back is the stored row: id, attempt
        /// number and member filled in by the server.</summary>
        IEnumerator Record(GuildDungeonAttempt attempt, Action<GuildDungeonAttempt, string> done);

        /// <summary>Posts one of this member's attempts into guild chat as a card.</summary>
        IEnumerator Share(GuildDungeonAttempt attempt, Action<bool, string> done);

        IEnumerator FetchChat(Action<List<GuildMessageEntry>, string> done);
        IEnumerator PostMessage(string typed, Action<bool, string> done);
    }

    /// <summary>Which backend the screens use.</summary>
    public static class GuildDungeonBackends
    {
        /// <summary>
        /// Set only by editor tooling, to put the in-memory guild in front of the screens. Nothing
        /// in a player build writes it, so a player build always talks to the live backend or, with
        /// no cloud configured, to nothing.
        /// </summary>
        public static IGuildDungeonBackend Override;

        private static SupabaseGuildDungeonBackend _live;

        public static IGuildDungeonBackend For(GameApp app)
        {
            if (Override != null) return Override;
            if (app == null || app.Cloud == null) return null;
            if (_live == null || !_live.Wraps(app.Cloud)) _live = new SupabaseGuildDungeonBackend(app.Cloud);
            return _live;
        }
    }

    /// <summary>
    /// The live backend: migration 0007's status call and table, 0008's share call, and 0011's
    /// attempt call, which pays the Guild Path.
    ///
    /// The chat read asks for each message's attempt INLINE, so a shared attempt arrives as a
    /// whole record and a card can be drawn and played with no second request. If the server has
    /// not had 0008 applied, that read fails on the unknown column, and this falls back to the
    /// plain read GuildService makes: the chat keeps working, as text, rather than going blank
    /// because a migration is behind the client.
    /// </summary>
    public sealed class SupabaseGuildDungeonBackend : IGuildDungeonBackend
    {
        private readonly CloudCoordinator _cloud;

        public SupabaseGuildDungeonBackend(CloudCoordinator cloud) { _cloud = cloud; }

        public bool Wraps(CloudCoordinator cloud) { return ReferenceEquals(cloud, _cloud); }

        public bool Ready { get { return _cloud.SignedIn && _cloud.Guild.Known && _cloud.Guild.Current != null; } }

        public string NotReadyReason
        {
            get
            {
                if (!_cloud.SignedIn) return "You are offline, so the guild cannot be reached.";
                if (!_cloud.Guild.Known) return "Your guild has not loaded yet.";
                return "You are not in a guild.";
            }
        }

        public string SelfUserId { get { return _cloud.State.UserId; } }

        public string GuildName { get { return _cloud.Guild.Current != null ? _cloud.Guild.Current.Name : ""; } }

        public IEnumerator Status(Action<GuildDungeonStatus, string> done)
        {
            if (!Ready) { done(null, NotReadyReason); yield break; }
            yield return _cloud.Client.Send("POST", "rest/v1/rpc/guild_dungeon_status", "{}",
                delegate (SupabaseResponse response)
                {
                    if (!response.Ok) { done(null, response.Error); return; }
                    JsonValue root;
                    string error;
                    if (!JsonValue.TryParse(response.Body, out root, out error)) { done(null, error); return; }
                    GuildDungeonStatus status = GuildDungeonStatus.Read(root);
                    if (status.Cycle < 0) { done(null, "the dungeon did not answer"); return; }
                    done(status, "");
                });
        }

        public IEnumerator ListAttempts(int cycle, Action<List<GuildDungeonAttempt>, string> done)
        {
            if (!Ready) { done(null, NotReadyReason); yield break; }
            string path = "rest/v1/guild_dungeon_attempts?select=" + GuildDungeon.RowColumns
                        + ",profiles!guild_dungeon_attempts_user_id_fkey(display_name)"
                        + "&guild_id=eq." + _cloud.Guild.Current.Id + "&cycle=eq." + cycle + "&order=id.asc";
            yield return _cloud.Client.Send("GET", path, null, delegate (SupabaseResponse response)
            {
                if (!response.Ok) { done(null, response.Error); return; }
                done(GuildCodec.ReadRows(response.Body, delegate (JsonValue row) { return GuildDungeon.ReadRow(row); }), "");
            });
        }

        /// <summary>
        /// Stores an attempt through 0011's guild_dungeon_attempt, which also records the boards it
        /// entered and pays the Guild Path's base. If the project has not had 0011 applied,
        /// PostgREST answers 404 for the unknown function and this files the same attempt through
        /// 0008's guild_dungeon_submit instead: the attempt counts and pays nothing, rather than
        /// the dungeon refusing every fight because a migration is behind the client.
        /// </summary>
        public IEnumerator Record(GuildDungeonAttempt attempt, Action<GuildDungeonAttempt, string> done)
        {
            if (!Ready) { done(null, NotReadyReason); yield break; }
            string name = _cloud.Profile != null ? _cloud.Profile.DisplayName : "";
            bool unknownFunction = false;
            Action<SupabaseResponse> read = delegate (SupabaseResponse response)
                {
                    if (!response.Ok) { done(null, response.Error); return; }
                    JsonValue root;
                    string error;
                    if (!JsonValue.TryParse(response.Body, out root, out error)) { done(null, error); return; }
                    if (root.IsArray) root = root[0];
                    GuildDungeonAttempt stored = GuildDungeon.ReadRow(root);
                    if (stored.Id == 0) { done(null, "the attempt was not stored"); return; }
                    stored.DisplayName = name;
                    done(stored, "");
                };
            yield return _cloud.Client.Send("POST", "rest/v1/rpc/guild_dungeon_attempt",
                GuildDungeon.WriteRpcArgs(attempt).ToJson(false),
                delegate (SupabaseResponse response)
                {
                    if (response.Status == 404) { unknownFunction = true; return; }
                    read(response);
                });
            if (!unknownFunction) yield break;
            // 0008's function takes no p_boards, and PostgREST refuses an argument it does not name.
            int claimed = attempt.Boards;
            attempt.Boards = -1;
            string before0011 = GuildDungeon.WriteRpcArgs(attempt).ToJson(false);
            attempt.Boards = claimed;
            yield return _cloud.Client.Send("POST", "rest/v1/rpc/guild_dungeon_submit", before0011, read);
        }

        public IEnumerator Share(GuildDungeonAttempt attempt, Action<bool, string> done)
        {
            if (!Ready) { done(false, NotReadyReason); yield break; }
            yield return _cloud.Client.Send("POST", "rest/v1/rpc/guild_share_attempt",
                GuildDungeon.WriteShareArgs(attempt).ToJson(false),
                delegate (SupabaseResponse response)
                {
                    if (response.Ok) { done(true, ""); return; }
                    done(false, response.IsRateLimited
                        ? "one message every " + GuildRules.ChatCooldownSeconds + " seconds"
                        : response.Error);
                });
        }

        /// <summary>The chat read with each message's attempt embedded under "attempt".</summary>
        public const string ChatSelect = "select=id,user_id,body,created_at,attempt_id,"
                                       + "profiles!guild_messages_user_id_fkey(display_name),"
                                       + "attempt:guild_dungeon_attempts!guild_messages_attempt_id_fkey(" + GuildDungeon.RowColumns + ")";

        public IEnumerator FetchChat(Action<List<GuildMessageEntry>, string> done)
        {
            if (!_cloud.SignedIn) { done(null, "not signed in"); yield break; }
            if (_cloud.Guild.Current == null) { done(new List<GuildMessageEntry>(), ""); yield break; }

            string path = "rest/v1/guild_messages?" + ChatSelect + "&guild_id=eq." + _cloud.Guild.Current.Id
                        + "&order=id.desc&limit=" + GuildRules.ChatWindow;
            bool fallBack = false;
            string self = SelfUserId;
            yield return _cloud.Client.Send("GET", path, null, delegate (SupabaseResponse response)
            {
                // 400 is PostgREST refusing the select: the columns 0008 adds are not there yet.
                if (response.Status == 400) { fallBack = true; return; }
                if (!response.Ok) { done(null, response.Error); return; }
                done(GuildCodec.ReadRows(response.Body,
                    delegate (JsonValue row) { return GuildCodec.ReadMessage(row, self); }), "");
            });
            if (fallBack) yield return _cloud.Guild.FetchChat(done);
        }

        public IEnumerator PostMessage(string typed, Action<bool, string> done)
        {
            yield return _cloud.Guild.PostMessage(typed, done);
        }
    }
}
