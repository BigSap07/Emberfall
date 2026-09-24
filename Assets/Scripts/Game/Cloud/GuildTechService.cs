using System;
using System.Collections;
using Emberfall.Core.Cloud;
using Emberfall.Core.Json;

namespace Emberfall.Game.Cloud
{
    /// <summary>
    /// What the guild tech screen asks of a server: where the tree stands, a rung, and a talent
    /// pick. Migration 0012's guild_tech_state, guild_tech_buy_rung and guild_tech_pick_talent.
    ///
    /// AN INTERFACE FOR IGuildPathBackend'S REASON: the editor capture run puts an in-memory one in
    /// front of the screen, so the tree can be photographed at a guild level nobody has played to.
    ///
    /// READING IT IS NOT A GUILD CALL, WRITING A PICK IS. State answers for a signed-in account
    /// whether or not it is in a guild - the rungs are the account's and survive leaving - but
    /// picking a talent is the guild's decision and 0012 refuses it to anyone but a leader or an
    /// officer.
    /// </summary>
    public interface IGuildTechBackend
    {
        bool Ready { get; }
        string NotReadyReason { get; }

        /// <summary>Settles the guild's level from what its members have earned, then answers.</summary>
        IEnumerator State(Action<GuildTechState, string> done);

        /// <summary>Buys the next rung on one family track with the account's own Waymarks.</summary>
        IEnumerator BuyRung(string familyId, Action<GuildTechState, string> done);

        /// <summary>Sets the guild's talent on one family, from the next cycle (G4).</summary>
        IEnumerator PickTalent(string familyId, string talentId, Action<GuildTechState, string> done);
    }

    public static class GuildTechBackends
    {
        /// <summary>Set only by editor tooling. See GuildDungeonBackends.Override.</summary>
        public static IGuildTechBackend Override;

        private static SupabaseGuildTechBackend _live;

        public static IGuildTechBackend For(GameApp app)
        {
            if (Override != null) return Override;
            if (app == null || app.Cloud == null) return null;
            if (_live == null || !_live.Wraps(app.Cloud)) _live = new SupabaseGuildTechBackend(app.Cloud);
            return _live;
        }
    }

    public sealed class SupabaseGuildTechBackend : IGuildTechBackend
    {
        private readonly CloudCoordinator _cloud;

        public SupabaseGuildTechBackend(CloudCoordinator cloud) { _cloud = cloud; }

        public bool Wraps(CloudCoordinator cloud) { return ReferenceEquals(cloud, _cloud); }

        public bool Ready { get { return _cloud.SignedIn; } }

        public string NotReadyReason
        {
            get { return "You are offline. This is the tree as it stood when you last connected."; }
        }

        public IEnumerator State(Action<GuildTechState, string> done)
        {
            if (!Ready) { done(null, NotReadyReason); yield break; }
            yield return _cloud.Client.Send("POST", "rest/v1/rpc/guild_tech_state", "{}",
                delegate (SupabaseResponse response) { Answer(response, done); });
        }

        public IEnumerator BuyRung(string familyId, Action<GuildTechState, string> done)
        {
            if (!Ready) { done(null, NotReadyReason); yield break; }
            JsonValue args = JsonValue.NewObject();
            args.Set("p_family", familyId ?? "");
            yield return _cloud.Client.Send("POST", "rest/v1/rpc/guild_tech_buy_rung", args.ToJson(false),
                delegate (SupabaseResponse response) { Answer(response, done); });
        }

        public IEnumerator PickTalent(string familyId, string talentId, Action<GuildTechState, string> done)
        {
            if (!Ready) { done(null, NotReadyReason); yield break; }
            JsonValue args = JsonValue.NewObject();
            args.Set("p_family", familyId ?? "");
            args.Set("p_talent", talentId ?? "");
            yield return _cloud.Client.Send("POST", "rest/v1/rpc/guild_tech_pick_talent", args.ToJson(false),
                delegate (SupabaseResponse response) { Answer(response, done); });
        }

        /// <summary>A 404 here is PostgREST not knowing the function: 0012 has not been applied to
        /// the project yet. Said as that rather than as a raw error, because it is the one failure
        /// a player can do nothing about and a tester needs to recognise on sight.</summary>
        private static void Answer(SupabaseResponse response, Action<GuildTechState, string> done)
        {
            if (response.Status == 404) { done(null, "The guild tech tree is not open on the server yet."); return; }
            if (!response.Ok) { done(null, response.Error); return; }
            JsonValue root;
            string error;
            if (!JsonValue.TryParse(response.Body, out root, out error)) { done(null, error); return; }
            done(GuildTechState.Read(root), "");
        }
    }
}
