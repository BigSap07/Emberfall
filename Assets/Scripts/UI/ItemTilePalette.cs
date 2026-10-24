namespace Emberfall.UI
{
    /// <summary>
    /// THE RARITY COLOUR LANGUAGE, IN ONE PLACE (R47). Every item tile in the game takes its plate, rim,
    /// glow and frame weight from here, and UITheme.ArmorTemperColor and UITheme.RelicGradeColor read the
    /// rim colour back out of it, so a tier is the same colour on a tile, on a text row and on a chip.
    ///
    /// ARMOR HAS FOUR TIERS, not five: ArmorTemper is Rough, Honed, Gilded, Radiant (armor.json carries them
    /// in its tempers array). Relics have two grades, Legendary and Mythic. The two ladders stay two ladders.
    ///
    /// READS AT A GLANCE: tier 1 is a quiet grey-brown plate, and every step up is a clearly more saturated and
    /// brighter plate with a brighter rim. The top tier also gets a faint glow ring and a corner gem.
    ///
    /// READS WITHOUT COLOUR: four channels all rise with the tier, so a colour-blind player can still rank a
    /// row of tiles. Plate luminance rises, rim luminance rises, the rim gets thicker, and one more pip is lit.
    /// ItemTileChecks in the harness proves each of the four is strictly increasing and that the silhouette
    /// keeps enough contrast against every plate.
    ///
    /// Deliberately free of UnityEngine, so the harness compiles it. Colours are 0xRRGGBB integers here and
    /// become Colors in ItemTile.
    /// </summary>
    public static class ItemTilePalette
    {
        /// <summary>ArmorTemper has exactly four members. The harness asserts this against the enum.</summary>
        public const int ArmorTiers = 4;

        /// <summary>RelicGrade has exactly two members, Legendary then Mythic.</summary>
        public const int RelicTiers = 2;

        /// <summary>The light neutral metal every slot silhouette is drawn in, so it reads on every plate.</summary>
        public const int Silhouette = 0xE9E3D8;

        /// <summary>The plate of an empty or locked slot: dark, and the same for every tier.</summary>
        public const int DimPlate = 0x1E1712;
        public const int DimRim = 0x4A3F36;

        /// <summary>The dark line outside the rim, shared with Emberforge.Outline.</summary>
        public const int Outline = 0x0B0C18;

        private static readonly int[] ArmorPlates = { 0x3C342E, 0x1B4E5C, 0x6B4F14, 0xA24A0E };
        private static readonly int[] ArmorRims = { 0x7A6D62, 0x3FA7B8, 0xD9A441, 0xFFC070 };
        private static readonly string[] ArmorNames = { "Rough", "Honed", "Gilded", "Radiant" };

        private static readonly int[] RelicPlates = { 0x5A4411, 0x5C1A52 };
        private static readonly int[] RelicRims = { 0xF2C14E, 0xE84FD1 };
        private static readonly string[] RelicNames = { "Legendary", "Mythic" };

        // ---------------------------------------------------------------- armor

        public static int ArmorPlate(int tier) { return ArmorPlates[ClampArmor(tier)]; }
        public static int ArmorRim(int tier) { return ArmorRims[ClampArmor(tier)]; }
        public static string ArmorName(int tier) { return ArmorNames[ClampArmor(tier)]; }

        /// <summary>The rim thickness in canvas units: 2, 3, 4, 5. A heavier frame is the second colour-free read.</summary>
        public static int ArmorFrame(int tier) { return 2 + ClampArmor(tier); }

        /// <summary>Lit pips on the bottom row: one more per tier, out of ArmorTiers.</summary>
        public static int ArmorMarks(int tier) { return ClampArmor(tier) + 1; }

        /// <summary>Only the top tier carries the faint glow ring and the corner gem.</summary>
        public static bool ArmorHasGem(int tier) { return ClampArmor(tier) == ArmorTiers - 1; }

        // ---------------------------------------------------------------- relics

        public static int RelicPlate(int grade) { return RelicPlates[ClampRelic(grade)]; }
        public static int RelicRim(int grade) { return RelicRims[ClampRelic(grade)]; }
        public static string RelicName(int grade) { return RelicNames[ClampRelic(grade)]; }
        public static int RelicFrame(int grade) { return 3 + ClampRelic(grade) * 2; }
        public static bool RelicHasGem(int grade) { return ClampRelic(grade) == RelicTiers - 1; }

        // ---------------------------------------------------------------- measures

        /// <summary>WCAG relative luminance of a 0xRRGGBB colour, 0 (black) to 1 (white).</summary>
        public static double Luminance(int rgb)
        {
            return 0.2126 * Linear((rgb >> 16) & 255) + 0.7152 * Linear((rgb >> 8) & 255) + 0.0722 * Linear(rgb & 255);
        }

        /// <summary>WCAG contrast ratio of two colours, 1 (none) to 21 (black on white).</summary>
        public static double Contrast(int a, int b)
        {
            double la = Luminance(a);
            double lb = Luminance(b);
            double hi = la > lb ? la : lb;
            double lo = la > lb ? lb : la;
            return (hi + 0.05) / (lo + 0.05);
        }

        /// <summary>The distance between two colours in plain RGB, 0 to 441. Two tiers closer than the harness's
        /// floor would read as one colour.</summary>
        public static double Distance(int a, int b)
        {
            double dr = ((a >> 16) & 255) - ((b >> 16) & 255);
            double dg = ((a >> 8) & 255) - ((b >> 8) & 255);
            double db = (a & 255) - (b & 255);
            return System.Math.Sqrt(dr * dr + dg * dg + db * db);
        }

        private static double Linear(int channel)
        {
            double c = channel / 255.0;
            return c <= 0.03928 ? c / 12.92 : System.Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        private static int ClampArmor(int tier) { return tier < 0 ? 0 : tier >= ArmorTiers ? ArmorTiers - 1 : tier; }
        private static int ClampRelic(int grade) { return grade < 0 ? 0 : grade >= RelicTiers ? RelicTiers - 1 : grade; }
    }
}
