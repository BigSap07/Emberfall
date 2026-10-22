using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;

namespace Emberfall.Core.Cloud
{
    /// <summary>
    /// The guild dungeon's SIZE and ATTEMPT rules: how long a guild's ladder is, and how many
    /// attempts a member has left. claude/DESIGN-GUILDS-PHASE-TWO.md, "Size" and "Bounded".
    ///
    /// THE SERVER HOLDS BOTH (Server/supabase/migrations/0016_guild_dungeon_size.sql). This copy
    /// decides what to draw and what to say before a round trip, exactly as GuildRules does for
    /// membership, and GuildDungeonChecks fails the build when the two disagree.
    ///
    /// Three rules, each written against a failure the design names:
    ///
    /// 1. THE LADDER SCALES WITH THE GUILD, THE FLOORS DO NOT. A guild of thirty attempts more
    ///    floors than a guild of four; floor 3 is the same fight for both (GuildDungeonGenerator). The
    ///    smallest ladder is the base five, so no guild has fewer floors than every guild had before
    ///    ladders scaled.
    ///
    /// 2. THE LADDER IS FIXED FOR THE CYCLE. The server snapshots the member count the first time
    ///    anyone in the guild asks for the cycle, and that snapshot sets the ladder until the cycle
    ///    rolls. A floor can never disappear mid-week under somebody's recorded attempt.
    ///
    /// 3. A MEMBER'S ATTEMPTS ARE BOUNDED THE SAME AT EVERY SIZE. Three a floor, as before, and a
    ///    cycle cap of three times the BASE ladder (15) per account. A member of a big guild has more
    ///    floors to spend them on, never more attempts, so guild content cannot grow to fill a big
    ///    guild member's week ("overtaking the loop"). The cap is derived, not typed: it is
    ///    guildDungeonAttemptsPerFloor x guildDungeonFloors, both already in balance.json.
    /// </summary>
    public static class GuildDungeonScale
    {
        /// <summary>Floors in a guild's ladder for a guild of this many members.</summary>
        public static int LadderFor(ContentDatabase content, int members)
        {
            int baseLadder = BaseLadder(content);
            if (content == null) return baseLadder;
            int floors = content.GuildDungeonScale.FloorsFor(members, baseLadder);
            return Math.Max(baseLadder, floors);
        }

        /// <summary>The smallest ladder, and the one every guild had before ladders scaled:
        /// balance.json's guildDungeonFloors.</summary>
        public static int BaseLadder(ContentDatabase content)
        {
            return content != null ? Math.Max(1, content.Balance.GuildDungeonFloors) : GuildDungeonGenerator.BlockFloors;
        }

        /// <summary>The longest ladder any guild can have: a guild at the member cap.</summary>
        public static int MaxLadder(ContentDatabase content)
        {
            return LadderFor(content, GuildRules.MemberCap);
        }

        /// <summary>Attempts one account may make in one cycle, across every floor: the per-floor
        /// cap times the base ladder.</summary>
        public static int CycleAttemptCap(BalanceConfig cfg)
        {
            return Math.Max(1, cfg.GuildDungeonAttemptsPerFloor) * Math.Max(1, cfg.GuildDungeonFloors);
        }

        /// <summary>This member's attempts in this cycle, every floor. Only the rows the caller can
        /// read: a member who changed guild mid-cycle has spent attempts in the old guild that this
        /// count cannot see, which is why guild_dungeon_status reports the server's count
        /// (GuildDungeonStatus.AttemptsUsed) and AttemptsLeft prefers it.</summary>
        public static int CycleAttemptsUsed(List<GuildDungeonAttempt> attempts, string userId, int cycle)
        {
            int used = 0;
            for (int i = 0; attempts != null && i < attempts.Count; i++)
            {
                GuildDungeonAttempt a = attempts[i];
                if (a != null && a.UserId == userId && a.Cycle == cycle) used++;
            }
            return used;
        }

        /// <summary>
        /// Attempts this member has left on this floor this cycle: the lesser of the floor's
        /// remainder and the cycle's. serverCycleUsed is the server's count when known (pass -1
        /// when not), and wins when it is higher than what the rows show.
        /// </summary>
        public static int AttemptsLeft(BalanceConfig cfg, List<GuildDungeonAttempt> attempts, string userId, int cycle,
                                       int floor, int serverCycleUsed)
        {
            int floorLeft = Math.Max(1, cfg.GuildDungeonAttemptsPerFloor) - GuildDungeon.AttemptsUsed(attempts, userId, cycle, floor);
            int cycleUsed = Math.Max(serverCycleUsed, CycleAttemptsUsed(attempts, userId, cycle));
            int cycleLeft = CycleAttemptCap(cfg) - cycleUsed;
            return Math.Max(0, Math.Min(floorLeft, cycleLeft));
        }

        /// <summary>
        /// Whether this member may attempt this floor now, and if not, the sentence that says why.
        /// The order is the server's (0016's guild_dungeon_submit): the floor exists, the ladder
        /// reaches it, the guild has opened it, then the two caps.
        /// </summary>
        public static bool CanAttempt(ContentDatabase content, List<GuildDungeonAttempt> attempts, string userId, int cycle,
                                      int floor, int ladder, int serverCycleUsed, out string reason)
        {
            reason = "";
            BalanceConfig cfg = content.Balance;
            if (floor < 1 || floor > MaxLadder(content))
            {
                reason = "a floor is 1 to " + MaxLadder(content);
                return false;
            }
            if (floor > ladder)
            {
                reason = "your guild's dungeon is " + ladder + " floors this cycle";
                return false;
            }
            if (!GuildDungeon.FloorOpen(attempts, cycle, floor))
            {
                reason = "floor " + floor + " opens when somebody in your guild clears floor " + (floor - 1);
                return false;
            }
            if (GuildDungeon.AttemptsUsed(attempts, userId, cycle, floor) >= cfg.GuildDungeonAttemptsPerFloor)
            {
                reason = "no attempts left on floor " + floor + " this cycle";
                return false;
            }
            if (Math.Max(serverCycleUsed, CycleAttemptsUsed(attempts, userId, cycle)) >= CycleAttemptCap(cfg))
            {
                reason = "no attempts left this cycle (" + CycleAttemptCap(cfg) + " of " + CycleAttemptCap(cfg) + ")";
                return false;
            }
            return true;
        }
    }
}
