using System;

namespace Emberfall.Core.Session
{
    /// <summary>
    /// THE HEROES A2 SWITCH (R57): the Heroes screen drawn to the A2 boards (a cream panel, a tan well of
    /// tiles, a detail card with one OPEN HERO). ON by default; define EMBERFALL_HEROES_A2_OFF to build the
    /// screen as it was before R57 (the same opt-out shape as FormationA2Switch). The field is writable so
    /// the harness and the capture targets can force it; whatever sets it puts it back.
    ///
    /// OFF IS THE OLD SCREEN, EXACTLY. HeroesScreen takes its old Build path when this is off, and the shared
    /// top bar keeps all three pills and the rank card. The switch is a presentation preference of the build:
    /// it reads nothing from a save, writes nothing to one, and never reaches a fight.
    /// </summary>
    public static class HeroesA2Switch
    {
#if EMBERFALL_HEROES_A2_OFF
        public static readonly bool ShippedDefault = false;
#else
        public static readonly bool ShippedDefault = true;
#endif

        public static bool Enabled = ShippedDefault;
    }

    /// <summary>What the sort control and the roster order need to know about one hero.</summary>
    public sealed class HeroSortKey
    {
        public string Id = "";
        public int Power;
        public int Level;
        public int Stars;
        public int Aspect;
    }

    /// <summary>
    /// What the A2 Heroes screen SAYS and in what order, as plain functions so the harness can prove it
    /// without an engine. Nothing here decides a game rule: who owns whom is PlayerProfile's job, and a
    /// hero's power is StatMath's. The screen reads those and this only orders and words them.
    /// </summary>
    public static class HeroesPick
    {
        public const int SortPower = 0;
        public const int SortLevel = 1;
        public const int SortStars = 2;
        public const int SortAspect = 3;
        public const int SortCount = 4;

        /// <summary>The state mark of a tile. One mark at most: a check, a padlock, or nothing.</summary>
        public const int MarkNone = 0;
        public const int MarkInSquad = 1;
        public const int MarkLocked = 2;

        /// <summary>The columns the roster aims for at 1920 wide; wider screens fit more.</summary>
        public const int GridColumns = 6;

        /// <summary>The one line a locked hero's card says, in place of an action.</summary>
        public const string LockedLine = "Summon this hero in the Rift to get them.";

        /// <summary>R58: the label of the one Primary. A hero not yet summoned is previewed, an owned one is opened.</summary>
        public static string OpenLabel(bool locked)
        {
            return locked ? "PREVIEW" : "OPEN HERO";
        }

        /// <summary>The next sort in the cycle: Power, Level, Stars, Aspect, then Power again.</summary>
        public static int NextSort(int mode)
        {
            return (mode + 1) % SortCount;
        }

        /// <summary>The word on the sort control. Upper case, as the other small controls are.</summary>
        public static string SortLabel(int mode)
        {
            switch (mode)
            {
                case SortLevel: return "LEVEL";
                case SortStars: return "STARS";
                case SortAspect: return "ASPECT";
                default: return "POWER";
            }
        }

        /// <summary>"4 of 94": the heroes the player has, of the heroes there are (inside the current filter).</summary>
        public static string CountText(int owned, int total)
        {
            return owned + " of " + total;
        }

        /// <summary>The mark a tile wears: a padlock when not summoned, a check when in the squad, else none.</summary>
        public static int Mark(bool locked, bool inSquad)
        {
            if (locked) return MarkLocked;
            return inSquad ? MarkInSquad : MarkNone;
        }

        /// <summary>
        /// The order of the OWNED heroes. Power is strongest first; Level and Stars are highest first and fall
        /// back to power; Aspect groups by the aspect order (Ember first) and then strongest first. The squad
        /// does not decide the order, so a tile never moves under a finger when a hero joins or leaves it.
        /// </summary>
        public static int CompareOwned(int mode, HeroSortKey a, HeroSortKey b)
        {
            int c = 0;
            switch (mode)
            {
                case SortLevel: c = b.Level.CompareTo(a.Level); break;
                case SortStars: c = b.Stars.CompareTo(a.Stars); break;
                case SortAspect: c = a.Aspect.CompareTo(b.Aspect); break;
            }
            if (c != 0) return c;
            c = b.Power.CompareTo(a.Power);
            if (c != 0) return c;
            return string.CompareOrdinal(a.Id, b.Id);
        }

        /// <summary>
        /// The order of the LOCKED heroes, which trail every owned one. They have no level or power to sort
        /// by, so stars (highest first) order them whatever the mode, and Aspect groups them first.
        /// </summary>
        public static int CompareLocked(int mode, HeroSortKey a, HeroSortKey b)
        {
            int c = 0;
            if (mode == SortAspect) c = a.Aspect.CompareTo(b.Aspect);
            if (c != 0) return c;
            c = b.Stars.CompareTo(a.Stars);
            if (c != 0) return c;
            return string.CompareOrdinal(a.Id, b.Id);
        }

        /// <summary>
        /// How many columns fit. The roster wants tiles at least <paramref name="minTile"/> wide with
        /// <paramref name="gap"/> between them, and never fewer than four columns.
        /// </summary>
        public static int ColumnsFor(float width, float minTile, float gap)
        {
            int n = (int)Math.Floor((width + gap) / (minTile + gap));
            return n < 4 ? 4 : n;
        }
    }
}
