using System;
using System.IO;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// R58 HERO DETAIL: the A2 Hero Detail screen proved without Unity. The words and the order of the buttons are plain
    /// functions in Core (HeroDetailPick) and are run here; the screen's wiring is read out of the source, the way
    /// R57HeroesChecks does it (the UI is not compiled into the harness).
    /// </summary>
    internal static partial class Program
    {
        private static void CheckR58HeroDetail(string contentDir)
        {
            Line("");
            Line("R58 HERO DETAIL - the A2 Hero Detail screen (switch, tabs, the one Primary, the words, the node names, size floors)");

            string root = Path.GetFullPath(Path.Combine(Path.Combine(Path.Combine(contentDir, ".."), ".."), ".."));

            // The switch: on by default, off through the one define, writable for the capture twins.
            string sw = Read(root, "Assets/Scripts/Core/Session/HeroDetailA2Switch.cs");
            Check(HeroDetailA2Switch.ShippedDefault, "HERODETAIL: the A2 screen is the shipped default");
            Check(sw.Contains("#if EMBERFALL_HERODETAIL_A2_OFF") && sw.Contains("ShippedDefault = false"), "HERODETAIL: EMBERFALL_HERODETAIL_A2_OFF gives the old screen back");
            bool was = HeroDetailA2Switch.Enabled;
            HeroDetailA2Switch.Enabled = false;
            Check(!HeroDetailA2Switch.Enabled, "HERODETAIL: the switch can be forced off for the legacy twin");
            HeroDetailA2Switch.Enabled = was;

            // Tabs: three, STATS first, a hero not yet summoned has no GEAR and opens on SKILLS.
            Check(HeroDetailPick.TabCount == 3 && HeroDetailPick.TabLabel(HeroDetailPick.TabStats) == "STATS"
                  && HeroDetailPick.TabLabel(HeroDetailPick.TabSkills) == "SKILLS" && HeroDetailPick.TabLabel(HeroDetailPick.TabGear) == "GEAR",
                  "HERODETAIL: three tabs, STATS, SKILLS, GEAR");
            Check(HeroDetailPick.StartTab(false) == HeroDetailPick.TabStats && HeroDetailPick.StartTab(true) == HeroDetailPick.TabSkills,
                  "HERODETAIL: an owned hero opens on STATS, a hero being previewed on SKILLS");
            Check(HeroDetailPick.TabAvailable(HeroDetailPick.TabGear, false) && !HeroDetailPick.TabAvailable(HeroDetailPick.TabGear, true)
                  && HeroDetailPick.TabAvailable(HeroDetailPick.TabStats, true) && HeroDetailPick.TabAvailable(HeroDetailPick.TabSkills, true)
                  && !HeroDetailPick.TabAvailable(-1, false) && !HeroDetailPick.TabAvailable(3, false),
                  "HERODETAIL: a locked hero has no GEAR tab, and no tab outside the three exists");
            Check(HeroDetailPick.MaxStatRows == 4, "HERODETAIL: at most four stat rows");

            // The one Primary: LEVEL UP unless ASCEND is ready; LEVEL UP greys out when it cannot be bought.
            Check(HeroDetailPick.PrimaryOf(false) == HeroDetailPick.PrimaryLevelUp && HeroDetailPick.PrimaryOf(true) == HeroDetailPick.PrimaryAscend,
                  "HERODETAIL: LEVEL UP leads, ASCEND takes the Primary the moment it is ready");
            Check(HeroDetailPick.LevelUpEnabled(false, true) && !HeroDetailPick.LevelUpEnabled(false, false) && !HeroDetailPick.LevelUpEnabled(true, true),
                  "HERODETAIL: LEVEL UP is enabled only below the cap and with the gold");

            // Words.
            Check(HeroDetailPick.LevelUpLabel(false, 1070) == "LEVEL UP   1070G" && HeroDetailPick.LevelUpLabel(true, 1070) == "LEVEL UP",
                  "HERODETAIL: LEVEL UP carries its price, and not at the cap");
            Check(HeroDetailPick.NeedGold(1070) == "Need 1070 gold", "HERODETAIL: the hint line reads 'Need 1070 gold'");
            Check(HeroDetailPick.NextLevelLine(900, 1500).StartsWith("Next level costs 900 gold"), "HERODETAIL: the hint names the price when a level can be bought");
            Check(HeroDetailPick.GearBonus(1200f, 1000f) == "+200" && HeroDetailPick.GearBonus(1000f, 1000f) == "" && HeroDetailPick.GearBonus(999f, 1000f) == "",
                  "HERODETAIL: the gear number is the armour's share, and nothing when the gear gives none");
            Check(HeroDetailPick.MoreLine(0.12f, 0.3f) == "More:  Effect Hit 12%   Effect Resist 30%", "HERODETAIL: Effect Hit and Effect Resist share one More line");
            Check(HeroDetailPick.StandingLine(-1) == "On the bench" && HeroDetailPick.StandingLine(2) == "Standing in slot 3"
                  && HeroDetailPick.StandingLine(0).StartsWith("Standing in slot 1"), "HERODETAIL: the slot line counts from 1 and says bench for a hero off the squad");
            Check(HeroDetailPick.FusionLabel(5) == "ASCEND" && HeroDetailPick.FusionLabel(3) == "CONVERT", "HERODETAIL: a five star ascends, any other converts");

            // The screen source.
            string src = Read(root, "Assets/Scripts/UI/Screens/HeroDetailScreenA2.cs");
            string old = Read(root, "Assets/Scripts/UI/Screens/HeroDetailScreen.cs");
            Check(old.Contains("if (HeroDetailA2Switch.Enabled) { BuildA2(); return; }"), "WIRING: Build takes the A2 half only while the switch is on, the old build stays below it");
            Check(old.Contains("sealed partial class HeroDetailScreen"), "WIRING: the screen is one partial class, the old half untouched");
            Check(old.Contains("if (_a2) { RefreshA2(); return; }") && old.Contains("if (_a2) { OnShowA2();"), "WIRING: Refresh and OnShow route to the A2 half only while it is built");
            Check(src.Contains("SlimTopBar { get { return _a2; } }"), "WIRING: the rank card and Dust pill are hidden only while the A2 half is on");
            Check(src.Contains("\"Header\"") && src.Contains("\"Back\"") && src.Contains("Router.Back()"), "WIRING: Header/Back goes where it went (Router.Back)");
            string[] names = { "\"LevelUp\"", "\"LevelUpTen\"", "\"Fusion\"", "\"Obtain\"", "\"Formation\"", "\"Skin\"", "\"Talents\"", "\"Slot_\"" };
            string missing = "";
            for (int i = 0; i < names.Length; i++) if (!src.Contains(names[i])) missing += names[i] + " ";
            Check(missing.Length == 0, "WIRING: the node names the coach and the lock view find are all kept" + (missing.Length > 0 ? " (missing " + missing.Trim() + ")" : ""));
            Check(src.Contains("herodetail.Talents") && src.Contains("herodetail.Fusion") && src.Contains("herodetail.ArmorSlot"), "WIRING: the three feature gates are kept");
            Check(src.Contains("OpenArmorSlot") && src.Contains("OpenTalents") && src.Contains("OpenFusion") && src.Contains("OpenFormation")
                  && src.Contains("GoToSummon") && src.Contains("LevelUp(10)") && src.Contains("LevelUp(1)") && src.Contains("CycleSkin"),
                  "WIRING: every action calls the method it called before (level up, x10, ascend, equip, banner, skins, talents)");
            Check(src.Contains("session.LevelUpCost(_hero)") && src.Contains("session.AscensionBlocker(_hero)") && src.Contains("session.HeroLevelCapOf(_hero)"),
                  "WIRING: the price, the ascend test and the cap are read from GameSession, none is decided here");
            Check(src.Contains("ShowAbilityCard(entry"), "WIRING: an ability tile opens the shared ability sheet, so every number stays reachable");
            Check(src.Contains("AspectChart.Multiplier"), "WIRING: the aspect matchup strip reads AspectChart");
            Check(src.Contains("Mathf.Min(HeroDetailPick.MaxStatRows"), "WIRING: the stat rows are capped by HeroDetailPick.MaxStatRows");

            // One Primary at a time: LEVEL UP and ASCEND swap through one SetKind call, OPEN THE RIFT shows for a locked hero only.
            Check(CountR57(src, "A2Kit.SetKind(primary, A2ButtonKind.Primary)") == 1, "WIRING: exactly one button is set to Primary in the bar, chosen by HeroDetailPick.PrimaryOf");
            Check(src.Contains("HeroDetailPick.PrimaryOf(ascendReady)"), "WIRING: the bar asks HeroDetailPick.PrimaryOf which button leads");
            Check(src.Contains("_obtainButton.Root.gameObject.SetActive(locked)") && src.Contains("_levelUpButton.Root.gameObject.SetActive(acts)"),
                  "WIRING: OPEN THE RIFT shows for a hero not yet summoned, LEVEL UP for an owned one, never both");
            Check(CountR57(src, "A2ButtonKind.Secondary") <= 3, "WIRING: Secondary buttons stay within the budget (Talents, and LEVEL UP when ASCEND leads)");
            Check(src.Contains("A2Kit.SetDisabledMix(_levelUpButton"), "WIRING: an unaffordable LEVEL UP stays orange but disabled (a lighter disabled mix)");
            Check(src.Contains("HeroDetailPick.NeedGold(cost)"), "WIRING: the unaffordable hint reads Need N gold");

            // Touch targets (48 is the floor) and text (22 is the floor), from the constants in the source.
            string[] touch = { "A2TabHeight", "A2PrimaryHeight", "A2SecondaryHeight", "A2TenHeight", "A2LinkHeight", "A2FooterHeight", "A2SkillTileHeight" };
            for (int i = 0; i < touch.Length; i++)
                Check(ConstAtLeast(src, touch[i], 48f), "SIZE: " + touch[i] + " is at least 48 (" + ConstOf(src, touch[i]) + ")");
            Check(ConstAtLeast(src, "A2TabWidth", 48f) && ConstAtLeast(src, "A2TenWidth", 48f) && ConstAtLeast(src, "A2PrimaryWidth", 48f),
                  "SIZE: the tab, x10 and Primary widths are at least 48");
            Check(src.Contains("HubStyle.TouchMin, HubStyle.TouchMin"), "SIZE: the back disc's hit box is HubStyle.TouchMin");
            int smallest = SmallestLabelSize(src);
            Check(smallest >= 22, "SIZE: the smallest label size written in the A2 Hero Detail screen is " + smallest + " (floor 22)");

            string[] files =
            {
                "Assets/Scripts/Core/Session/HeroDetailA2Switch.cs", "Assets/Scripts/UI/Screens/HeroDetailScreenA2.cs",
                "Assets/Scripts/UI/Screens/HeroDetailScreen.cs", "Tools/DevHarness/R58HeroDetailChecks.cs"
            };
            bool dashes = false;
            for (int i = 0; i < files.Length; i++)
            {
                string t = Read(root, files[i]);
                if (t.IndexOf((char)0x2014) >= 0 || t.IndexOf((char)0x2013) >= 0) dashes = true;
            }
            Check(!dashes, "no em or en dashes in the R58 files");
        }
    }
}
