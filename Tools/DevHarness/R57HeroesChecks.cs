using System;
using System.IO;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// R57 HEROES: the star shape (step 0) and, further down, the A2 Heroes roster, proved without Unity.
    /// The UI is not compiled into the harness, so geometry is called directly (StarShape is plain C# in Core)
    /// and the wiring is read out of the source, the way R55FormationChecks and R56SkinChecks do it.
    /// </summary>
    internal static partial class Program
    {
        private static void CheckR57Heroes(string contentDir)
        {
            Line("");
            Line("R57 HEROES - the five point star shape (StarShape, TextureLab.Star) and the A2 Heroes roster");

            string root = Path.GetFullPath(Path.Combine(Path.Combine(Path.Combine(contentDir, ".."), ".."), ".."));

            // Step 0: the old sprite was a pentagon (inner corner radius over tip radius was 0.81).
            float tip = StarShape.EdgeRadius(0f);
            float inner = StarShape.EdgeRadius(36f);
            float ratio = inner / tip;
            Check(StarShape.CornerCount == 10, "STAR: ten corners, five tips and five inner corners");
            Check(ratio > 0.30f && ratio < 0.55f, "STAR: the inner corner sits at " + ratio.ToString("0.000") + " of the tip radius, a star (a pentagon is 0.809)");
            bool symmetric = true;
            for (int k = 1; k < 5; k++)
            {
                if (Math.Abs(StarShape.EdgeRadius(72f * k) - tip) > 0.01f) symmetric = false;
                if (Math.Abs(StarShape.EdgeRadius(36f + 72f * k) - inner) > 0.01f) symmetric = false;
            }
            Check(symmetric, "STAR: the five tips match and the five inner corners match (five fold symmetry)");
            Check(StarShape.Distance(0f, StarShape.CentreY) < 0f, "STAR: the centre is inside");
            Check(StarShape.Distance(0.95f, 0.95f) > 0f && StarShape.Distance(-0.95f, -0.95f) > 0f, "STAR: the square's corners are outside");
            Check(StarShape.Distance(0f, StarShape.CentreY + tip * 0.97f) < 0f, "STAR: the top tip is filled just short of its end");
            // Tips up, bounding box centred: the top tip and the two lower tips sit the same distance from the square's edge.
            float top = StarShape.CornerY(0);
            float low = StarShape.CornerY(6);
            Check(Math.Abs(top + low) < 0.01f && top > 0.85f && top < 1.0f, "STAR: tips up, centred on its bounding box and inside the -1..1 square");

            string lab = Read(root, "Assets/Scripts/UI/TextureLab.cs");
            Check(lab.Contains("StarShape.Distance") && !lab.Contains("StarSdf"), "WIRING: TextureLab.Star draws StarShape, the pentagon distance field is gone");
            Check(lab.Contains("StarSuper = 4") && lab.Contains("StarEmpty"), "WIRING: the star is supersampled and has an empty outline twin");
            string widgets = Read(root, "Assets/Scripts/UI/Widgets.cs");
            string a2kit = Read(root, "Assets/Scripts/UI/A2Kit.cs");
            Check(a2kit.Contains("StarGold = Hex(0xF4A62A)") && a2kit.Contains("public static RectTransform Stars("), "WIRING: the shared A2Kit.Stars row in star gold #F4A62A");
            Check(widgets.Contains("A2Kit.StarGold") && widgets.Contains("TextureLab.StarEmpty"), "WIRING: Widgets.StarRow uses the same gold and outline under A2");
            string formation = Read(root, "Assets/Scripts/UI/Screens/FormationScreenA2.cs");
            Check(formation.Contains("A2Kit.Stars("), "WIRING: the Formation screen draws its stars through A2Kit.Stars");

            CheckR57HeroesScreen(root);

            string[] files =
            {
                "Assets/Scripts/Core/Session/StarShape.cs", "Assets/Scripts/Core/Session/HeroesA2Switch.cs",
                "Assets/Scripts/UI/Screens/HeroesScreenA2.cs", "Assets/Scripts/UI/Screens/HeroesScreen.cs",
                "Tools/DevHarness/R57HeroesChecks.cs"
            };
            bool dashes = false;
            for (int i = 0; i < files.Length; i++)
            {
                string t = Read(root, files[i]);
                if (t.IndexOf((char)0x2014) >= 0 || t.IndexOf((char)0x2013) >= 0) dashes = true;
            }
            Check(!dashes, "no em or en dashes in the R57 files");
        }

        /// <summary>The A2 Heroes screen: the switch, the order, the words, the node names, the one Primary and the size floors.</summary>
        private static void CheckR57HeroesScreen(string root)
        {
            // The switch: on by default, off through the one define, and writable for the capture targets.
            string sw = Read(root, "Assets/Scripts/Core/Session/HeroesA2Switch.cs");
            Check(HeroesA2Switch.ShippedDefault, "HEROES: the A2 screen is the shipped default");
            Check(sw.Contains("#if EMBERFALL_HEROES_A2_OFF") && sw.Contains("ShippedDefault = false"), "HEROES: EMBERFALL_HEROES_A2_OFF gives the old screen back");
            bool was = HeroesA2Switch.Enabled;
            HeroesA2Switch.Enabled = false;
            Check(!HeroesA2Switch.Enabled, "HEROES: the switch can be forced off for the legacy twin");
            HeroesA2Switch.Enabled = was;

            // Sort: four modes, cycling, and the words on the control.
            int mode = HeroesPick.SortPower;
            string cycle = "";
            for (int i = 0; i < HeroesPick.SortCount; i++) { cycle += HeroesPick.SortLabel(mode) + " "; mode = HeroesPick.NextSort(mode); }
            Check(cycle.Trim() == "POWER LEVEL STARS ASPECT" && mode == HeroesPick.SortPower, "HEROES: sort cycles Power, Level, Stars, Aspect and back (" + cycle.Trim() + ")");

            // Owned order: strongest first by default, level and stars on their own sort, ties by power then id.
            HeroSortKey a = new HeroSortKey { Id = "a", Power = 900, Level = 5, Stars = 3, Aspect = 2 };
            HeroSortKey b = new HeroSortKey { Id = "b", Power = 700, Level = 9, Stars = 5, Aspect = 0 };
            HeroSortKey c = new HeroSortKey { Id = "c", Power = 700, Level = 9, Stars = 5, Aspect = 0 };
            Check(HeroesPick.CompareOwned(HeroesPick.SortPower, a, b) < 0, "HEROES: Power puts the stronger hero first");
            Check(HeroesPick.CompareOwned(HeroesPick.SortLevel, a, b) > 0, "HEROES: Level puts the higher level first");
            Check(HeroesPick.CompareOwned(HeroesPick.SortStars, a, b) > 0, "HEROES: Stars puts the higher tier first");
            Check(HeroesPick.CompareOwned(HeroesPick.SortAspect, a, b) > 0, "HEROES: Aspect groups by the aspect order");
            Check(HeroesPick.CompareOwned(HeroesPick.SortPower, b, c) < 0 && HeroesPick.CompareOwned(HeroesPick.SortPower, c, b) > 0, "HEROES: equal heroes order by id, so the order is stable");
            Check(HeroesPick.CompareLocked(HeroesPick.SortPower, b, a) < 0, "HEROES: locked heroes order by stars, highest first");

            // Words and marks.
            Check(HeroesPick.CountText(4, 94) == "4 of 94", "HEROES: the count reads '4 of 94'");
            Check(HeroesPick.Mark(true, false) == HeroesPick.MarkLocked && HeroesPick.Mark(true, true) == HeroesPick.MarkLocked, "HEROES: a hero not yet summoned wears the padlock");
            Check(HeroesPick.Mark(false, true) == HeroesPick.MarkInSquad && HeroesPick.Mark(false, false) == HeroesPick.MarkNone, "HEROES: a hero in the squad wears the check, any other owned hero nothing");
            Check(HeroesPick.LockedLine.Contains("Summon") && !HeroesPick.LockedLine.Contains("team"), "HEROES: the locked line says to Summon, and says squad words only");

            // Columns: six at 1920x1080 (the panel leaves about 1.2k for the well), more on a wider screen, never under four.
            Check(HeroesPick.ColumnsFor(1190f, 190f, 10f) == 6, "HEROES: six columns fit the 1920 wide well");
            Check(HeroesPick.ColumnsFor(1700f, 190f, 10f) > HeroesPick.ColumnsFor(1190f, 190f, 10f), "HEROES: a wider screen fits more columns");
            Check(HeroesPick.ColumnsFor(300f, 190f, 10f) == 4, "HEROES: never fewer than four columns");

            // The screen source: the A2 half exists, keeps the node names the coach and the router rely on, and has one Primary.
            string src = Read(root, "Assets/Scripts/UI/Screens/HeroesScreenA2.cs");
            string old = Read(root, "Assets/Scripts/UI/Screens/HeroesScreen.cs");
            Check(old.Contains("if (HeroesA2Switch.Enabled) { BuildA2(); return; }"), "WIRING: Build takes the A2 half only while the switch is on, the old build stays below it");
            Check(old.Contains("ScreenChrome.ForgedHeader(Root, \"YOUR ALLIES\"") && old.Contains("Widgets.HeroCard(\"Card\""), "WIRING: the old screen is still there for EMBERFALL_HEROES_A2_OFF");
            Check(src.Contains("\"Header\"") && src.Contains("\"Back\"") && src.Contains("Router.Back()"), "WIRING: Header/Back goes where it went (Router.Back)");
            Check(src.Contains("\"Card\" + index++"), "WIRING: the tiles keep the Card0, Card1 names");
            Check(src.Contains("\"OpenHero\""), "WIRING: the one action is named OpenHero");
            string coach = Read(root, "Assets/Scripts/UI/OnboardingCoach.cs");
            Check(coach.Contains("FindDeep(root, \"OpenHero\") ?? FindDeep(root, \"Card0\")"), "WIRING: the coach points at OpenHero, and at Card0 when there is no card yet");
            Check(src.Contains("Router.HeroDetail.SetHero(hero)") && src.Contains("Router.Show(ScreenId.HeroDetail)"), "WIRING: OPEN HERO opens Hero Detail");
            Check(CountR57(src, "A2ButtonKind.Primary") == 1, "WIRING: exactly one Primary in the A2 Heroes screen (" + CountR57(src, "A2ButtonKind.Primary") + ")");
            Check(CountR57(src, "A2ButtonKind.Secondary") == 0, "WIRING: no Secondary button on the A2 Heroes screen");
            // R58 step 0: a hero not yet summoned is PREVIEW, still the one Primary, and opens the locked preview.
            Check(HeroesPick.OpenLabel(true) == "PREVIEW" && HeroesPick.OpenLabel(false) == "OPEN HERO", "HEROES R58: the Primary reads PREVIEW for a locked hero, OPEN HERO for an owned one");
            Check(!src.Contains("open.Interactable = false"), "WIRING R58: the Primary is no longer disabled for a locked hero");
            Check(src.Contains("Router.HeroDetail.SetLockedHero(def)"), "WIRING R58: PREVIEW opens the locked hero preview in Hero Detail");
            Check(ConstAtLeast(src, "A2TileStar", 22f), "SIZE R58: the tile stars are about 1.4 times the old 16 (" + ConstOf(src, "A2TileStar") + ")");
            Check(ConstAtLeast(src, "A2BandHeight", 64f), "SIZE R58: the tile band is tall enough to keep the stars clear of the level label (" + ConstOf(src, "A2BandHeight") + ")");
            Check(src.Contains("A2LockedFace") && !src.Contains("new Color(0.45f, 0.4f, 0.38f, 0.55f)"), "WIRING R58: the locked face tint is lifted from the R57 value");
            Check(src.Contains("if (key == _a2Selected) return;"), "WIRING: a second tap on the selected tile does nothing (no double tap open)");
            Check(src.Contains("_a2Filter = aspect == _a2Filter ? -1 : aspect;"), "WIRING: an emblem filters, tapping it again clears, ALL clears");
            Check(src.Contains("HeroesPick.NextSort(_a2Sort)"), "WIRING: the sort control cycles");
            Check(!src.ToLowerInvariant().Contains("team"), "WIRING: the screen says squad, never team, in its words");
            Check(src.Contains("SlimTopBar { get { return _a2; } }"), "WIRING: the rank card and Dust pill are hidden only while the A2 half is on");
            string router = Read(root, "Assets/Scripts/UI/ScreenRouter.cs");
            Check(router.Contains("virtual bool SlimTopBar") && router.Contains("SetTopBarSlim(next.SlimTopBar)"), "WIRING: the router resets the slim top bar on every screen change");

            // Touch targets and text, from the constants in the source (48 is the floor, the Formation text floor is 22).
            Check(ConstAtLeast(src, "A2OpenHeight", 48f), "SIZE: OPEN HERO is at least 48 tall (" + ConstOf(src, "A2OpenHeight") + ")");
            Check(ConstAtLeast(src, "A2SortHeight", 48f), "SIZE: the Sort control is at least 48 tall (" + ConstOf(src, "A2SortHeight") + ")");
            Check(ConstAtLeast(src, "A2FilterRowHeight", 48f), "SIZE: each aspect emblem hit is 80 wide and the row is at least 48 tall (" + ConstOf(src, "A2FilterRowHeight") + ")");
            Check(src.Contains("UIBuilder.Box(node, index * 84f, 0f, 80f, 80f"), "SIZE: an aspect emblem's hit box is 80 by 80");
            Check(src.Contains("HubStyle.TouchMin, HubStyle.TouchMin"), "SIZE: the back disc's hit box is HubStyle.TouchMin");
            Check(ConstAtLeast(src, "A2MinTile", 100f), "SIZE: a tile is at least 100 wide, far over 48 (" + ConstOf(src, "A2MinTile") + ")");
            int smallest = SmallestLabelSize(src);
            Check(smallest >= 22, "SIZE: the smallest label size in the A2 Heroes screen is " + smallest + " (floor 22)");
        }

        private static int CountR57(string text, string needle)
        {
            int n = 0, at = 0;
            while ((at = text.IndexOf(needle, at, StringComparison.Ordinal)) >= 0) { n++; at += needle.Length; }
            return n;
        }

        private static string ConstOf(string src, string name)
        {
            int at = src.IndexOf(" " + name + " = ", StringComparison.Ordinal);
            if (at < 0) return "missing";
            int start = at + name.Length + 4;
            int end = start;
            while (end < src.Length && (char.IsDigit(src[end]) || src[end] == '.')) end++;
            return src.Substring(start, end - start);
        }

        private static bool ConstAtLeast(string src, string name, float floor)
        {
            float v;
            return float.TryParse(ConstOf(src, name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) && v >= floor;
        }

        /// <summary>The smallest integer font size passed to A2Kit.Label or A2Kit.Button in the source (the argument after the text).</summary>
        private static int SmallestLabelSize(string src)
        {
            int smallest = int.MaxValue;
            string[] lines = src.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.IndexOf("A2Kit.Label(", StringComparison.Ordinal) < 0 && line.IndexOf("A2Kit.Button(", StringComparison.Ordinal) < 0
                    && line.IndexOf("A2Kit.Chunky(", StringComparison.Ordinal) < 0) continue;
                int comma = -1, depth = 0, seen = 0;
                for (int k = 0; k < line.Length; k++)
                {
                    char ch = line[k];
                    if (ch == '(') depth++;
                    else if (ch == ')') depth--;
                    else if (ch == ',' && depth == 1) { seen++; if (seen == 3) { comma = k; break; } }
                }
                if (comma < 0) continue;
                int p = comma + 1;
                while (p < line.Length && line[p] == ' ') p++;
                int q = p;
                while (q < line.Length && char.IsDigit(line[q])) q++;
                int size;
                if (q > p && int.TryParse(line.Substring(p, q - p), out size) && size < smallest) smallest = size;
            }
            return smallest == int.MaxValue ? 0 : smallest;
        }
    }
}
