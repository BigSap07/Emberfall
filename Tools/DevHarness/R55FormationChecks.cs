using System;
using System.IO;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// R55 FORMATION (A2), PROVED WITHOUT UNITY.
    ///
    /// The UI is not compiled into the harness, so what the screen does is proven the way R26UiChecks proves it: the pure
    /// rules (SquadPick, FormationA2Switch, CinderNotch) are called, and the wiring the coach and the lock view depend on is
    /// read out of the source. The rollback is the switch: off is the old screen, and the old Build path must still exist.
    /// </summary>
    internal static partial class Program
    {
        private static void CheckR55Formation(string contentDir)
        {
            Line("");
            Line("R55 FORMATION - the A2 squad screen (SquadPick, FormationA2Switch) and the names the coach and the lock view find");

            string root = Path.GetFullPath(Path.Combine(Path.Combine(Path.Combine(contentDir, ".."), ".."), ".."));
            R55Switch();
            R55Order();
            R55Rules();
            R55Notch();
            R55Wiring(root);
        }

        private static void R55Switch()
        {
            bool was = FormationA2Switch.Enabled;
            Check(FormationA2Switch.ShippedDefault, "the A2 Formation is ON in the shipped tree (EMBERFALL_FORMATION_A2_OFF is the opt out)");
            FormationA2Switch.Enabled = false;
            Check(!FormationA2Switch.Enabled, "the switch can be forced off, which is the rollback to the pre R55 screen");
            FormationA2Switch.Enabled = was;
            Check(FormationA2Switch.Enabled == was, "the switch is put back as it was found");
        }

        private static void R55Order()
        {
            bool five = SquadPick.Slots == 5 && SquadPick.VisualOrder.Length == 5;
            bool seen0 = false, seen1 = false, seen2 = false, seen3 = false, seen4 = false;
            for (int i = 0; i < SquadPick.VisualOrder.Length; i++)
            {
                switch (SquadPick.VisualOrder[i])
                {
                    case 0: seen0 = true; break;
                    case 1: seen1 = true; break;
                    case 2: seen2 = true; break;
                    case 3: seen3 = true; break;
                    case 4: seen4 = true; break;
                }
            }
            Check(five && seen0 && seen1 && seen2 && seen3 && seen4, "the well draws each of the five game slots exactly once");
            Check(SquadPick.VisualOrder[0] == 4 && SquadPick.VisualOrder[4] == 0,
                  "the row reads back to front: game slot 4 (B1) first, game slot 0 (F2) last, the enemy cue at its right end");
            Check(SquadPick.DisplayNumber(0) == 1 && SquadPick.DisplayNumber(4) == 5, "the number shown is the game slot plus one");
            Check(SquadPick.IsFront(0) && SquadPick.IsFront(1) && !SquadPick.IsFront(2) && !SquadPick.IsFront(4),
                  "slots 1 and 2 (game 0 and 1) are the front rank, 3 to 5 the back");
        }

        private static void R55Rules()
        {
            string[] team = { "a", "b", null, "", null };
            Check(SquadPick.FirstEmpty(team) == 2, "FirstEmpty finds the first empty slot (null or empty string)");
            Check(SquadPick.FirstEmpty(new string[] { "a", "b", "c", "d", "e" }) == -1, "FirstEmpty is -1 on a full squad");
            Check(SquadPick.SlotOf(team, "b") == 1 && SquadPick.SlotOf(team, "z") == -1 && SquadPick.SlotOf(team, "") == -1,
                  "SlotOf finds a uid and says -1 for a stranger or an empty uid");

            Check(SquadPick.PowerDelta(500, 0) == 500 && SquadPick.PowerDelta(500, 320) == 180 && SquadPick.PowerDelta(300, 320) == -20,
                  "PowerDelta is the hero's power minus whoever stands there");
            Check(SquadPick.DeltaText(180) == "+180" && SquadPick.DeltaText(-20) == "-20" && SquadPick.DeltaText(0) == "0",
                  "DeltaText prints +n, -n and 0 with a plain minus sign");
            Check(SquadPick.DeltaText(-20).IndexOf((char)0x2013) < 0 && SquadPick.DeltaText(-20).IndexOf((char)0x2014) < 0, "DeltaText never uses a dash glyph");

            Check(!SquadPick.IsLit(team, "", 2), "nothing selected lights no slot");
            Check(SquadPick.IsLit(team, "x", 2) && !SquadPick.IsLit(team, "x", 3) && !SquadPick.IsLit(team, "x", 0),
                  "an unplaced hero lights only the first empty slot while the squad has room");
            string[] full = { "a", "b", "c", "d", "e" };
            bool allLit = true;
            for (int i = 0; i < 5; i++) allLit = allLit && SquadPick.IsLit(full, "x", i);
            Check(allLit, "an unplaced hero lights every slot once the squad is full (any of them swaps)");
            Check(!SquadPick.IsLit(full, "c", 2) && SquadPick.IsLit(full, "c", 0) && SquadPick.IsLit(full, "c", 4),
                  "a placed hero lights every slot but its own (a move or a swap)");

            Check(SquadPick.DeltaNote(3, false) == "if placed in slot 4" && SquadPick.DeltaNote(1, true) == "if swapped into slot 2",
                  "the delta note names the displayed slot number and says placed or swapped");

            Check(SquadPick.Hint(team, "").Length > 0 && SquadPick.Hint(team, "x").Length > 0
                  && SquadPick.Hint(full, "x").IndexOf("full", StringComparison.OrdinalIgnoreCase) >= 0
                  && SquadPick.Hint(full, "c").Length > 0,
                  "the hint line has a sentence for nothing selected, room left, a full squad and a move");

            Check(SquadPick.PassesFilter(-1, 3) && SquadPick.PassesFilter(2, 2) && !SquadPick.PassesFilter(2, 3),
                  "the aspect filter passes everyone on ALL and only the matching aspect otherwise");
        }

        private static void R55Notch()
        {
            Check(CinderNotch.SegmentCount == 6, "the Cinder Notch has six segments");
            bool distinct = true;
            for (int a = 0; a < 6; a++)
                for (int b = a + 1; b < 6; b++)
                    if (CinderNotch.GapOf(a) == CinderNotch.GapOf(b)) distinct = false;
            Check(distinct, "the six aspects have six different gap positions, so no aspect is told apart by colour alone");
        }

        private static void R55Wiring(string root)
        {
            string a2Path = Path.Combine(root, "Assets/Scripts/UI/Screens/FormationScreenA2.cs");
            string oldPath = Path.Combine(root, "Assets/Scripts/UI/Screens/FormationScreen.cs");
            bool there = File.Exists(a2Path) && File.Exists(oldPath);
            Check(there, "WIRING (reads source): both halves of FormationScreen exist");
            if (!there) return;
            string a2 = File.ReadAllText(a2Path);
            string old = File.ReadAllText(oldPath);

            Check(a2.IndexOf("UIBuilder.Node(\"Header\", Root)", StringComparison.Ordinal) >= 0
                  && a2.IndexOf("UIBuilder.Node(\"Back\", header)", StringComparison.Ordinal) >= 0,
                  "WIRING (reads source): the A2 build keeps Header/Back, which the onboarding coach finds on the Team step");
            Check(a2.IndexOf("A2Kit.Button(\"ViewAll\"", StringComparison.Ordinal) >= 0,
                  "WIRING (reads source): the A2 build keeps ViewAll, which the coach finds on the Level Up step");
            Check(a2.IndexOf("A2Kit.Button(\"GuildTech\"", StringComparison.Ordinal) >= 0
                  && a2.IndexOf("FeatureLockView.Attach(path.Root, \"formation.GuildTech\"", StringComparison.Ordinal) >= 0
                  && a2.IndexOf("FeatureGates.TryEnter(App, \"formation.GuildTech\")", StringComparison.Ordinal) >= 0,
                  "WIRING (reads source): GuildTech keeps its gate and its lock dress");
            Check(a2.IndexOf("FeatureLockView.RefreshAll(App, Root)", StringComparison.Ordinal) >= 0,
                  "WIRING (reads source): the A2 OnShow refreshes the lock views");

            Check(a2.IndexOf("HidesTopBar { get { return FormationA2Switch.Enabled; } }", StringComparison.Ordinal) >= 0,
                  "WIRING (reads source): the A2 screen hides the top bar exactly when the switch is on");
            Check(old.IndexOf("if (FormationA2Switch.Enabled) { BuildA2(); return; }", StringComparison.Ordinal) >= 0
                  && old.IndexOf("EmberforgeUI.Ground(Root);", StringComparison.Ordinal) >= 0,
                  "WIRING (reads source): Build takes the A2 path only while the switch is on, and the old Build is still there");

            Check(a2.IndexOf("AssignToTeam(", StringComparison.Ordinal) >= 0 && a2.IndexOf("ClearTeamSlot(", StringComparison.Ordinal) >= 0
                  && a2.IndexOf("NormaliseTeam(", StringComparison.Ordinal) >= 0,
                  "WIRING (reads source): placement, removal and AUTO FILL all go through PlayerProfile");
            Check(a2.IndexOf("Router.Back()", StringComparison.Ordinal) >= 0, "WIRING (reads source): CONFIRM leaves through Router.Back");
            Check(a2.IndexOf("A2ButtonKind.Primary", StringComparison.Ordinal) >= 0
                  && a2.IndexOf("A2ButtonKind.Primary", StringComparison.Ordinal) == a2.LastIndexOf("A2ButtonKind.Primary", StringComparison.Ordinal),
                  "WIRING (reads source): exactly one Primary button is built on the A2 screen");
            Check(a2.IndexOf((char)0x2013) < 0 && a2.IndexOf((char)0x2014) < 0, "WIRING (reads source): no en or em dash in the A2 screen source");
        }
    }
}
