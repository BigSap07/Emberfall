using System;
using System.Globalization;

namespace Emberfall.Core.Session
{
    /// <summary>
    /// THE HERO DETAIL A2 SWITCH (R58): the hero page drawn to the A2 boards (a portrait card on the left, a
    /// well with a STATS / SKILLS / GEAR tab rail on the right, one Primary in the bottom bar). ON by default;
    /// define EMBERFALL_HERODETAIL_A2_OFF to build the page as it was before R58 (the same opt-out shape as
    /// HeroesA2Switch). The field is writable so the harness and the capture targets can force it; whatever
    /// sets it puts it back.
    ///
    /// OFF IS THE OLD PAGE, EXACTLY. HeroDetailScreen takes its old Build path when this is off, and the shared
    /// top bar keeps all three pills and the rank card. The switch is a presentation preference of the build:
    /// it reads nothing from a save, writes nothing to one, and never reaches a fight.
    /// </summary>
    public static class HeroDetailA2Switch
    {
#if EMBERFALL_HERODETAIL_A2_OFF
        public static readonly bool ShippedDefault = false;
#else
        public static readonly bool ShippedDefault = true;
#endif

        public static bool Enabled = ShippedDefault;
    }

    /// <summary>
    /// What the A2 hero page SAYS and which button leads, as plain functions so the harness can prove it without
    /// an engine. Nothing here decides a game rule: a level price is StatMath's, whether a hero can ascend is
    /// GameSession's, and every number is read from them. This words them and picks the order of the buttons.
    /// </summary>
    public static class HeroDetailPick
    {
        public const int TabStats = 0;
        public const int TabSkills = 1;
        public const int TabGear = 2;
        public const int TabCount = 3;

        /// <summary>The most stat rows the STATS tab draws. Everything else goes on the one More line.</summary>
        public const int MaxStatRows = 4;

        /// <summary>Which control the bottom bar gives the one Primary.</summary>
        public const int PrimaryLevelUp = 0;
        public const int PrimaryAscend = 1;

        public static string TabLabel(int tab)
        {
            switch (tab)
            {
                case TabSkills: return "SKILLS";
                case TabGear: return "GEAR";
                default: return "STATS";
            }
        }

        /// <summary>The tabs a hero page offers: a hero not yet summoned has no gear to show.</summary>
        public static bool TabAvailable(int tab, bool locked)
        {
            if (tab < 0 || tab >= TabCount) return false;
            return !(locked && tab == TabGear);
        }

        /// <summary>The tab the page opens on. An owned hero opens on STATS, a hero being previewed on SKILLS.</summary>
        public static int StartTab(bool locked)
        {
            return locked ? TabSkills : TabStats;
        }

        /// <summary>
        /// ASCEND takes the Primary the moment it is available, and LEVEL UP steps down to Secondary. In every
        /// other state LEVEL UP keeps it (orange, and Disabled when it cannot be bought). Never both.
        /// </summary>
        public static int PrimaryOf(bool ascendReady)
        {
            return ascendReady ? PrimaryAscend : PrimaryLevelUp;
        }

        /// <summary>LEVEL UP can be pressed: below the cap and with the gold in hand.</summary>
        public static bool LevelUpEnabled(bool atCap, bool affordable)
        {
            return !atCap && affordable;
        }

        /// <summary>The label of LEVEL UP: the price rides on it while a level can be bought.</summary>
        public static string LevelUpLabel(bool atCap, int cost)
        {
            return atCap ? "LEVEL UP" : "LEVEL UP   " + cost.ToString(CultureInfo.InvariantCulture) + "G";
        }

        /// <summary>The hint line when a level cannot be bought for want of gold.</summary>
        public static string NeedGold(int cost)
        {
            return "Need " + cost.ToString(CultureInfo.InvariantCulture) + " gold";
        }

        /// <summary>The hint line when a level can be bought.</summary>
        public static string NextLevelLine(int cost, long gold)
        {
            return "Next level costs " + cost.ToString(CultureInfo.InvariantCulture) + " gold  -  you hold "
                 + gold.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The small gear number beside a stat: "+120", or nothing when the gear gives none.</summary>
        public static string GearBonus(float withGear, float withoutGear)
        {
            int delta = (int)Math.Round(withGear - withoutGear, MidpointRounding.AwayFromZero);
            return delta > 0 ? "+" + delta.ToString(CultureInfo.InvariantCulture) : "";
        }

        /// <summary>A share as a whole percent, "12%".</summary>
        public static string Percent(float share)
        {
            return ((int)Math.Round(share * 100f, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>The one More line under the four stat rows.</summary>
        public static string MoreLine(float effectHit, float effectResist)
        {
            return "More:  Effect Hit " + Percent(effectHit) + "   Effect Resist " + Percent(effectResist);
        }

        /// <summary>Where the hero stands in the squad, for the small line under the power.</summary>
        public static string StandingLine(int slot)
        {
            if (slot < 0) return "On the bench";
            return "Standing in slot " + (slot + 1).ToString(CultureInfo.InvariantCulture)
                 + (slot == 0 ? "  -  the front of the line" : "");
        }

        /// <summary>The label of the second door in the bar: a five star ascends, anything below converts.</summary>
        public static string FusionLabel(int stars)
        {
            return stars >= 5 ? "ASCEND" : "CONVERT";
        }
    }
}
