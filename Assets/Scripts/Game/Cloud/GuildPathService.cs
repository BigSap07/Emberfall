using System;
using System.Collections;
using Emberfall.Core.Cloud;
using Emberfall.Core.Json;

namespace Emberfall.Game.Cloud
{
    /// <summary>
    /// What the Guild Path screen asks of a server: the balance and the nodes owned, and a
    /// purchase. Migration 0011's guild_path_state and guild_path_unlock.
    ///
    /// AN INTERFACE FOR IGuildDungeonBackend's REASON: the editor capture run puts an in-memory
    /// one in front of the screen (GuildDungeonShots), so the screen can be photographed owning
    /// nodes without a live account that has earned them.
    ///
    /// NOT A GUILD CALL. Ready asks only for a signed-in account: the Path belongs to the player,
    /// and a player who left their guild yesterday still owns it and can still spend on it.
    /// </summary>
    public interface IGuildPathBackend
    {
        bool Ready { get; }
        string NotReadyReason { get; }

        /// <summary>Settles every closed cycle's board bonus, then answers.</summary>
        IEnumerator State(Action<GuildPathState, string> done);

        IEnumerator Unlock(string nodeId, Action<GuildPathState, string> done);
    }

    public static class GuildPathBackends
    {
        /// <summary>Set only by editor tooling. See GuildDungeonBackends.Override.</summary>
        public static IGuildPathBackend Override;

        private static SupabaseGuildPathBackend _live;

        public static IGuildPathBackend For(GameApp app)
        {
            if (Override != null) return Override;
            if (app == null || app.Cloud == null) return null;
            if (_live == null || !_live.Wraps(app.Cloud)) _live = new SupabaseGuildPathBackend(app.Cloud);
            return _live;
        }
    }

    public sealed class SupabaseGuildPathBackend : IGuildPathBackend
    {
        private readonly CloudCoordinator _cloud;

        public SupabaseGuildPathBackend(CloudCoordinator cloud) { _cloud = cloud; }

        public bool Wraps(CloudCoordinator cloud) { return ReferenceEquals(cloud, _cloud); }

        public bool Ready { get { return _cloud.SignedIn; } }

        public string NotReadyReason
        {
            get { return "You are offline. This is what you owned when you last connected."; }
        }

        public IEnumerator State(Action<GuildPathState, string> done)
        {
            if (!Ready) { done(null, NotReadyReason); yield break; }
            yield return _cloud.Client.Send("POST", "rest/v1/rpc/guild_path_state", "{}",
                delegate (SupabaseResponse response) { Answer(response, done); });
        }

        public IEnumerator Unlock(string nodeId, Action<GuildPathState, string> done)
        {
            if (!Ready) { done(null, NotReadyReason); yield break; }
            JsonValue args = JsonValue.NewObject();
            args.Set("p_node", nodeId ?? "");
            yield return _cloud.Client.Send("POST", "rest/v1/rpc/guild_path_unlock", args.ToJson(false),
                delegate (SupabaseResponse response) { Answer(response, done); });
        }

        /// <summary>A 404 here is PostgREST not knowing the function: 0011 has not been applied to
        /// the project yet. Said as that, rather than as a raw error, because it is the one
        /// failure a player can do nothing about and a tester needs to recognise.</summary>
        private static void Answer(SupabaseResponse response, Action<GuildPathState, string> done)
        {
            if (response.Status == 404) { done(null, "The Guild Path is not open on the server yet."); return; }
            if (!response.Ok) { done(null, response.Error); return; }
            JsonValue root;
            string error;
            if (!JsonValue.TryParse(response.Body, out root, out error)) { done(null, error); return; }
            done(GuildPathState.Read(root), "");
        }
    }
}
