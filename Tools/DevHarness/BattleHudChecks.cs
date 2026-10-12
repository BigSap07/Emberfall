using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Emberfall.Battle;
using Emberfall.Core.Data;
using Emberfall.UI.Battle;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE BATTLE HUD'S LAYOUT, PROVED WITHOUT UNITY.
    ///
    /// Every plate, both pills and the top bar are built through BattleHudLayout, the functions the
    /// battle screen itself places them with, and BattleFraming.Project stands in for the camera.
    /// That is the same model the editor compares against Camera.WorldToScreenPoint on the first
    /// frame of every battle, so a pass here is a statement about the screen and not about a copy.
    ///
    /// Run at the three phone shapes the game targets, 16:9, 18:9 and 20:9, each with no bottom
    /// inset and with a 24 pixel one (a gesture bar), one line per formation slot.
    /// </summary>
    internal static partial class Program
    {
        private static readonly int[][] HudScreens = { new[] { 1920, 1080 }, new[] { 2160, 1080 }, new[] { 2400, 1080 } };
        private static readonly int[] HudBottomInsets = { 0, 24 };

        private static void CheckBattleHud(string contentDir)
        {
            Line("");
            Line("BATTLE HUD - plates, pills and the top bar never meet (BattleHudLayout over BattleFraming)");

            string root = Path.GetFullPath(Path.Combine(Path.Combine(Path.Combine(contentDir, ".."), ".."), ".."));
            CheckBattleHudCanvasParity(root);
            CheckBattleStageReadsFraming(root);

            Check(BattleFraming.AllySlots.Length == 5 && BattleFraming.EnemySlots.Length == 5
                  && MirroredSlots(), "the ten slots are five a side and the enemy side mirrors the allies across x = 0");

            float worst = float.MaxValue;
            string worstAt = "";
            for (int s = 0; s < HudScreens.Length; s++)
                for (int k = 0; k < HudBottomInsets.Length; k++)
                    CheckBattleHudAt(HudScreens[s][0], HudScreens[s][1], HudBottomInsets[k], ref worst, ref worstAt);

            Line("  worst plate-to-plate clearance over every case: " + F1(worst) + " at " + worstAt
                 + " (min " + F1(BattleHudMetrics.PlateMinClearance) + ")");

            CheckStatusIconComposition();
        }

        private static void CheckBattleHudAt(int width, int height, int inset, ref float worst, ref string worstAt)
        {
            float scale = BattleHudLayout.CanvasScale(width, height);
            float canvasW = width / scale;
            float canvasH = height / scale;
            float safeBottom = inset / scale;
            float safeTop = height / scale;
            string tag = width + "x" + height + " inset" + inset;

            BattleFraming.CanvasRect viewport = BattleHudLayout.ToCanvas(
                BattleHudLayout.Viewport(width, height, inset, height, scale), canvasW, canvasH);
            float barBottom = BattleHudLayout.BarBottom(safeTop);
            BattleFraming.CanvasRect bar = BattleHudLayout.TopBar(canvasW, canvasH, safeTop);
            // The widest each pill can ever be, which is the worst case for both.
            BattleFraming.CanvasRect callout = BattleHudLayout.Callout(canvasW, safeTop, BattleHudMetrics.CalloutMaxWidth);
            BattleFraming.CanvasRect next = BattleHudLayout.Next(canvasW, safeBottom, true, BattleHudMetrics.NextMaxUpcoming);

            string[] ids = new string[10];
            BattleFraming.CanvasRect[] boxes = new BattleFraming.CanvasRect[10];
            for (int i = 0; i < 10; i++)
            {
                bool ally = i < 5;
                BattleFraming.SlotPoint slot = BattleFraming.Slot(ally, i % 5);
                BattleFraming.CanvasPoint feet = BattleFraming.Project(slot.X, 0f, slot.Z, viewport);
                ids[i] = (ally ? "A" : "E") + (i % 5);
                boxes[i] = BattleHudLayout.PlateBox(feet);
            }

            for (int i = 0; i < 10; i++)
            {
                BattleFraming.CanvasRect box = boxes[i];
                float nearest = float.MaxValue;
                string nearestId = "";
                for (int j = 0; j < 10; j++)
                {
                    if (j == i) continue;
                    float c = BattleHudLayout.Clearance(box, boxes[j]);
                    if (c < nearest) { nearest = c; nearestId = ids[j]; }
                }
                if (nearest < worst) { worst = nearest; worstAt = tag + " " + ids[i] + "/" + nearestId; }

                float aboveSafe = box.Y - safeBottom;
                float pills = Math.Min(BattleHudLayout.Clearance(box, callout), BattleHudLayout.Clearance(box, next));
                bool inside = box.X >= viewport.X && box.XMax <= viewport.XMax
                              && box.Y >= viewport.Y && box.YMax <= viewport.YMax;
                bool underBar = box.YMax > barBottom;

                float min = BattleHudMetrics.PlateMinClearance;
                Check(nearest >= min && aboveSafe >= min && pills >= min && inside && !underBar,
                      tag + " " + ids[i] + ": nearest " + nearestId + " clear " + F1(nearest) + " (min " + F1(min) + "), "
                      + F1(aboveSafe) + " above the safe bottom, " + F1(pills) + " from the nearest pill"
                      + (inside ? "" : ", OUTSIDE the field's viewport") + (underBar ? ", UNDER the top bar" : ""));
            }

            bool pillsClearOfBar = callout.YMax <= bar.Y && next.YMax <= bar.Y;
            bool pillsApart = BattleHudLayout.Clearance(callout, next) >= BattleHudMetrics.PlateMinClearance;
            bool pillsOnScreen = callout.X >= 0f && callout.XMax <= canvasW && next.X >= 0f && next.XMax <= canvasW
                                 && next.Y >= safeBottom;
            bool viewportUnderBar = Math.Abs(viewport.YMax - barBottom) < 0.01f && Math.Abs(viewport.Y - safeBottom) < 0.01f;
            Check(pillsClearOfBar && pillsApart && pillsOnScreen && viewportUnderBar,
                  tag + ": canvas " + F1(canvasW) + "x" + F1(canvasH) + ", field " + F1(viewport.Y) + " to " + F1(viewport.YMax)
                  + " stops at the bar, callout " + F1(callout.Y) + " to " + F1(callout.YMax) + " and NEXT "
                  + F1(next.Y) + " to " + F1(next.YMax) + " clear of the bar and of each other");
        }

        private static bool MirroredSlots()
        {
            for (int i = 0; i < 5; i++)
            {
                BattleFraming.SlotPoint a = BattleFraming.AllySlots[i], e = BattleFraming.EnemySlots[i];
                if (Math.Abs(a.X + e.X) > 1e-5f || Math.Abs(a.Z - e.Z) > 1e-5f) return false;
            }
            return true;
        }

        /// <summary>BattleHudMetrics restates the reference resolution and the canvas match because
        /// the files that own them are engine-side. Read both files and hold the copies to them.</summary>
        private static void CheckBattleHudCanvasParity(string root)
        {
            string themePath = Path.Combine(root, Path.Combine("Assets", Path.Combine("Scripts", Path.Combine("UI", "UITheme.cs"))));
            string appPath = Path.Combine(root, Path.Combine("Assets", Path.Combine("Scripts", Path.Combine("Game", "GameApp.cs"))));
            if (!File.Exists(themePath) || !File.Exists(appPath))
            {
                Check(false, "UITheme.cs and GameApp.cs are found under " + root);
                return;
            }

            Match reference = Regex.Match(File.ReadAllText(themePath),
                @"ReferenceResolution\s*=\s*new\s+Vector2\(\s*([0-9.]+)f\s*,\s*([0-9.]+)f\s*\)");
            Match match = Regex.Match(File.ReadAllText(appPath), @"matchWidthOrHeight\s*=\s*([0-9.]+)f");
            float refW = reference.Success ? float.Parse(reference.Groups[1].Value, CultureInfo.InvariantCulture) : -1f;
            float refH = reference.Success ? float.Parse(reference.Groups[2].Value, CultureInfo.InvariantCulture) : -1f;
            float m = match.Success ? float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : -1f;
            Check(refW == BattleHudMetrics.ReferenceWidth && refH == BattleHudMetrics.ReferenceHeight
                  && m == BattleHudMetrics.CanvasMatch,
                  "BattleHudMetrics' canvas (" + G(BattleHudMetrics.ReferenceWidth) + "x" + G(BattleHudMetrics.ReferenceHeight)
                  + ", match " + G(BattleHudMetrics.CanvasMatch) + ") is UITheme.ReferenceResolution (" + G(refW) + "x" + G(refH)
                  + ") and GameApp's matchWidthOrHeight (" + G(m) + ")");
        }

        /// <summary>BattleFraming is the one place the slots and the camera are written down; the
        /// stage that stands units on them must not carry a second copy.</summary>
        private static void CheckBattleStageReadsFraming(string root)
        {
            string stagePath = Path.Combine(root, Path.Combine("Assets", Path.Combine("Scripts", Path.Combine("Battle", "BattleStage.cs"))));
            if (!File.Exists(stagePath)) { Check(false, "BattleStage.cs is found"); return; }
            string text = File.ReadAllText(stagePath);
            bool reads = text.Contains("BattleFraming.Slot(") && text.Contains("BattleFraming.OrthographicSize")
                         && text.Contains("BattleFraming.CameraPitch") && text.Contains("BattleFraming.CameraY");
            bool restates = Regex.IsMatch(text, @"\b3\.65f\b|\b5\.95f\b|\b1\.4f\b|\b3\.95f\b|\b4\.95f\b|\b3\.25f\b");
            Check(reads && !restates, "BattleStage stands units on BattleFraming's slots and frames them with its camera,"
                                      + " with no copy of either in its own file");
        }

        private static void CheckStatusIconComposition()
        {
            List<StatusIcon> shown = new List<StatusIcon>();

            int over = StatusIcons.Compose(new List<StatusKind>
            {
                StatusKind.AttackUp, StatusKind.Burn, StatusKind.Shield, StatusKind.Mark, StatusKind.Slow
            }, shown);
            Check(shown.Count == 3 && over == 2 && shown[0].Kind == StatusKind.Burn && shown[1].Kind == StatusKind.Slow
                  && shown[2].Kind == StatusKind.Shield,
                  "five kinds show three icons and \"+2\": harmful first in landing order (Burn, Slow), then Shield;"
                  + " AttackUp and Mark are counted (got " + IconList(shown) + " +" + over + ")");

            over = StatusIcons.Compose(new List<StatusKind> { StatusKind.AttackUp, StatusKind.Mark, StatusKind.DefenseDown }, shown);
            Check(over == 0 && shown.Count == 3 && shown[0].Kind == StatusKind.DefenseDown
                  && shown[1].Kind == StatusKind.AttackUp && shown[2].Kind == StatusKind.Mark
                  && shown[0].Role == StatusIconRole.Debuff && shown[1].Role == StatusIconRole.Buff
                  && shown[2].Role == StatusIconRole.Inert,
                  "a harmful kind that landed last still sorts first, and an inert kind sorts after the buffs (got "
                  + IconList(shown) + ")");

            over = StatusIcons.Compose(new List<StatusKind>
            {
                StatusKind.Burn, StatusKind.AttackUp, StatusKind.Burn, StatusKind.None, StatusKind.Burn
            }, shown);
            Check(over == 0 && shown.Count == 2 && shown[0].Kind == StatusKind.Burn && shown[0].Depth == 3
                  && shown[1].Kind == StatusKind.AttackUp && shown[1].Depth == 1,
                  "one icon per kind carries its depth (three Burn entries are one icon at depth 3) and None is never drawn"
                  + " (got " + IconList(shown) + ")");
        }

        private static string IconList(List<StatusIcon> icons)
        {
            string s = "";
            for (int i = 0; i < icons.Count; i++)
                s += (i > 0 ? ", " : "") + icons[i].Kind + (icons[i].Depth > 1 ? " x" + icons[i].Depth : "");
            return s.Length > 0 ? s : "none";
        }

        private static string F1(float v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }
        private static string G(float v) { return v.ToString(CultureInfo.InvariantCulture); }
    }
}
