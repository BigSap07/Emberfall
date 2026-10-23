using System;
using Emberfall.Core.Data;

namespace Emberfall.Core.Cloud
{
    /// <summary>
    /// PLACEHOLDER. THE ECONOMY DECISION IS OPEN, SO NOTHING HERE PAYS.
    ///
    /// What a guild dungeon floor beyond the base ladder is worth has not been decided. Bigger
    /// guilds now see more floors (GuildDungeonScale), and the design's rule is that a member of a
    /// small guild and a member of a big one earn roughly the same, so whatever those floors pay must
    /// be breadth or recognition and must not grow with depth. The Waymark pay that already exists
    /// (guild-path.json, migration 0011: the first attempt, the first clear of ANY floor, floor 1's
    /// boards) is unchanged by ladders and is not this class's business.
    ///
    /// Until the owner decides, every reward here is Amount 0 with Placeholder set, the server mints
    /// nothing for a deep floor (0022 has no reward table on purpose), and
    /// GuildDungeonChecks.CheckRewardPlaceholder fails the build if a number appears here while the
    /// flag is still up.
    ///
    /// Two rules hold whatever is decided, and the harness holds them now:
    /// - NOTHING HERE IS RANDOM. A reward is a pure function of the floor; no seed, no roll, no
    ///   draw. (The Belgian rule: nothing a player can buy is random, and nothing in this mode is
    ///   bought at all.)
    /// - NOTHING HERE IS BOUGHT. No store, shop or pass grant kind names the dungeon, its attempts or
    ///   its floors, so attempts cannot be bought past the cap.
    /// </summary>
    public sealed class GuildDungeonFloorReward
    {
        /// <summary>True until the economy decision lands. A screen showing this must say the reward
        /// is not decided rather than print zero as if it were the answer.</summary>
        public bool Placeholder = true;
        /// <summary>Empty until decided.</summary>
        public string Currency = "";
        public int Amount;
        public string Note = "";
    }

    public static class GuildDungeonRewards
    {
        /// <summary>The flag the harness reads. Set false only in the commit that writes the real
        /// numbers, with the decision recorded in DESIGN-BUILD-ORDER.md.</summary>
        public static readonly bool IsPlaceholder = true;

        public const string PlaceholderNote =
            "PLACEHOLDER: what a guild dungeon floor pays is not decided yet. Nothing is minted.";

        /// <summary>
        /// What the first clear of a floor is worth to one member. PLACEHOLDER: always nothing.
        /// Takes the floor and the guild's ladder so the real rule can be written without a
        /// signature change; takes no seed and no member count, so it cannot be random and cannot
        /// pay a bigger guild's member more for the same floor.
        /// </summary>
        public static GuildDungeonFloorReward ForFirstClear(BalanceConfig cfg, int floor, int ladder)
        {
            GuildDungeonFloorReward r = new GuildDungeonFloorReward();
            r.Placeholder = IsPlaceholder;
            r.Currency = "";
            r.Amount = 0;
            r.Note = PlaceholderNote;
            return r;
        }
    }
}
