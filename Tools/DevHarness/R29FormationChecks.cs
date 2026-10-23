using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Emberfall.Battle;
using Emberfall.UI.Battle;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// R29 BATTLE FORMATION (FormationLayout), PROVED WITHOUT UNITY.
    ///
    /// The Idle Heroes style 5v5 formation is one table of feet fractions (FormationLayout.Cells) and
    /// arithmetic over it. This compiles that file and BattleHudLayout beside the harness and proves, at
    /// 16:9, 18:9, 20:9 and 4:3, each with and without a notch and a gesture bar: the table, the mirror, the
    /// slot mapping, draw order by feet height, every unit and every bar inside the safe area, the highest
    /// bar below 0.38 of the height from the top, the open centre, unit size about 0.20 of the height, the HUD
    /// rectangles (top bar, ability callout, turn order pill) clear of every unit and every bar, and the
    /// camera really putting the feet where the table says. All in canvas units, where one unit is the
    /// canvas scaler's, so the result is the one the screen draws.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>One screen shape: pixels, a bottom inset (gesture bar), a side inset on each side (notch).</summary>
        private struct R29Screen
        {
            public int Width, Height, Bottom, Side;
            public R29Screen(int w, int h, int bottom, int side) { Width = w; Height = h; Bottom = bottom; Side = side; }
            public string Tag { get { return Width + "x" + Height + (Bottom > 0 || Side > 0 ? " inset b" + Bottom + " s" + Side : ""); } }
        }

        private static readonly R29Screen[] R29Screens =
        {
            new R29Screen(1920, 1080, 0, 0),
            new R29Screen(1920, 1080, 24, 0),
            new R29Screen(2160, 1080, 0, 0),
            new R29Screen(2400, 1080, 0, 0),
            new R29Screen(2400, 1080, 24, 66),
            new R29Screen(1440, 1080, 0, 0),
            new R29Screen(1440, 1080, 24, 0)
        };

        // A body is judged by a box this wide and this tall relative to the unit's height (the real art is
        // narrower than 0.8 of its height, so this is the generous side), and the blob shadow by this much
        // below the feet.
        private const float R29BodyHalfWidth = 0.40f;
        private const float R29ShadowBelow = 0.06f;

        private static void CheckR29Formation(string contentDir)
        {
            Line("");
            Line("R29 BATTLE FORMATION - the Idle Heroes style 5v5 table (FormationLayout) and what hangs off it");

            string root = Path.GetFullPath(Path.Combine(Path.Combine(Path.Combine(contentDir, ".."), ".."), ".."));
            R29Table();
            R29Mirror();
            R29SlotMapping();
            R29ResolveSlots();
            for (int s = 0; s < R29Screens.Length; s++) R29AtScreen(R29Screens[s]);
            R29Approach(root);
            R29Wiring(root);
        }

        // ---------------------------------------------------------------- the data

        private static void R29Table()
        {
            string[] names = { "B1", "B2", "B3", "F1", "F2" };
            float[] xs = { 0.18f, 0.13f, 0.09f, 0.32f, 0.29f };
            float[] ys = { 0.64f, 0.77f, 0.90f, 0.69f, 0.84f };
            bool ok = FormationLayout.Cells.Length == 5;
            string seen = "";
            for (int i = 0; ok && i < 5; i++)
            {
                FormationLayout.Cell c = FormationLayout.Cells[i];
                ok = c.Name == names[i] && Math.Abs(c.X - xs[i]) < 1e-6f && Math.Abs(c.Y - ys[i]) < 1e-6f;
                seen += c.Name + " " + c.X.ToString("0.00", CultureInfo.InvariantCulture) + "," + c.Y.ToString("0.00", CultureInfo.InvariantCulture) + "  ";
            }
            Check(ok, "the table is the target's: B1 0.18,0.64  B2 0.13,0.77  B3 0.09,0.90  F1 0.32,0.69  F2 0.29,0.84 (got " + seen.Trim() + ")");
            Check(FormationLayout.Enabled, "FormationLayout.Enabled is true in the shipped tree");
            Check(Math.Abs(FormationLayout.UnitHeightFraction - 0.20f) < 1e-6f && FormationLayout.BossScale > 1f
                  && Math.Abs(FormationLayout.BarsCeiling - 0.38f) < 1e-6f,
                  "a unit is 0.20 of the height, a boss is larger, the bars' ceiling is 0.38");
        }

        private static void R29Mirror()
        {
            bool ok = true;
            for (int slot = 0; slot < 5; slot++)
            {
                FormationLayout.Cell a = FormationLayout.CellOf(true, slot), e = FormationLayout.CellOf(false, slot);
                if (Math.Abs(a.X + e.X - 1f) > 1e-6f || Math.Abs(a.Y - e.Y) > 1e-6f || a.Name != e.Name) ok = false;
                if (a.X >= 0.5f || e.X <= 0.5f) ok = false;
            }
            Check(ok, "the right team is the mirror image: x becomes 1 minus x, y and the cell are the same, and each team stays on its own half");

            BattleFraming.CanvasRect safe = new BattleFraming.CanvasRect(100f, 40f, 1600f, 900f);
            BattleFraming.CanvasPoint pa = FormationLayout.Feet(true, 2, safe), pe = FormationLayout.Feet(false, 2, safe);
            Check(Math.Abs((pa.X - safe.X) + (pe.X - safe.X) - safe.Width) < 0.01f && Math.Abs(pa.Y - pe.Y) < 0.01f,
                  "inside a safe area that does not start at the origin the mirror is about the area's own centre line");
        }

        private static void R29SlotMapping()
        {
            // Game slots 0 and 1 are the front rank, 2 to 4 the back (BattleFraming.FrontRankSlots).
            bool front = FormationLayout.CellOf(true, 0).Name[0] == 'F' && FormationLayout.CellOf(true, 1).Name[0] == 'F';
            bool back = true;
            for (int slot = BattleFraming.FrontRankSlots; slot < 5; slot++) back &= FormationLayout.CellOf(true, slot).Name[0] == 'B';
            HashSet<string> distinct = new HashSet<string>();
            for (int slot = 0; slot < 5; slot++) distinct.Add(FormationLayout.CellOf(true, slot).Name);
            Check(front && back && distinct.Count == 5, "slots 0 and 1 stand in the front row (F), 2 to 4 in the back row (B), five distinct cells");
            Check(FormationLayout.CellOf(true, FormationLayout.BossSlot).Name == "B2",
                  "the boss's slot (3, the middle of the back rank) is B2");

            // The order a team stood in before the table: slot 0 lowest on screen, slot 4 highest.
            bool lowFirst = FormationLayout.CellOf(true, 0).Y > FormationLayout.CellOf(true, 1).Y
                            && FormationLayout.CellOf(true, 2).Y > FormationLayout.CellOf(true, 3).Y
                            && FormationLayout.CellOf(true, 3).Y > FormationLayout.CellOf(true, 4).Y;
            Check(lowFirst,"slot 0 stays the lowest on screen of the front pair and slot 2 the lowest of the back three, as before");

            // A slot outside 0 to 4 clamps, as BattleFraming.Slot always did.
            Check(FormationLayout.CellOf(true, -3).Name == FormationLayout.CellOf(true, 0).Name
                  && FormationLayout.CellOf(false, 9).Name == FormationLayout.CellOf(false, 4).Name,
                  "a slot outside 0 to 4 clamps into the five a side has");
        }

        private static string R29List(int[] a)
        {
            string s = "";
            for (int i = 0; i < a.Length; i++) s += (i > 0 ? "," : "") + a[i];
            return "[" + s + "]";
        }

        private static void R29ResolveSlots()
        {
            int[] full = FormationLayout.ResolveSlots(new[] { 0, 1, 2, 3, 4 });
            Check(R29List(full) == "[0,1,2,3,4]", "five units in five slots keep their slots (got " + R29List(full) + ")");

            int[] few = FormationLayout.ResolveSlots(new[] { 0, 3 });
            Check(R29List(few) == "[0,3]", "fewer than five keep the slots they occupy and are not recentred (got " + R29List(few) + ")");
            int[] gap = FormationLayout.ResolveSlots(new[] { 1, 4, 2 });
            Check(R29List(gap) == "[1,4,2]", "a team with gaps in its slots keeps its gaps (got " + R29List(gap) + ")");

            int[] dup = FormationLayout.ResolveSlots(new[] { 2, 2 });
            Check(R29List(dup) == "[2,1]", "a second unit asking for a held slot takes the nearest free one, the lower first on a tie (got " + R29List(dup) + ")");
            int[] pile = FormationLayout.ResolveSlots(new[] { 4, 4, 4, 4, 4 });
            Check(R29List(pile) == "[4,3,2,1,0]", "five units all asking for slot 4 fill the side from the nearest outward (got " + R29List(pile) + ")");
            int[] past = FormationLayout.ResolveSlots(new[] { 7, 0, 9 });
            Check(R29List(past) == "[4,0,3]", "a slot past the end clamps to 4 and the next such unit takes the nearest free (got " + R29List(past) + ")");

            int[] six = FormationLayout.ResolveSlots(new[] { 0, 1, 2, 3, 4, 2 });
            bool allIn = true;
            for (int i = 0; i < six.Length; i++) allIn &= six[i] >= 0 && six[i] < 5;
            Check(six.Length == 6 && allIn && R29List(six).StartsWith("[0,1,2,3,4,"),
                  "a sixth unit (there is none today) shares the nearest slot instead of failing (got " + R29List(six) + ")");
            Check(FormationLayout.ResolveSlots(new int[0]).Length == 0 && FormationLayout.ResolveSlots(null).Length == 0,
                  "an empty side resolves to nothing");
        }

        // ---------------------------------------------------------------- one screen shape

        private static BattleFraming.CanvasRect R29Body(BattleFraming.CanvasPoint feet, float height)
        {
            float hw = R29BodyHalfWidth * height;
            float below = R29ShadowBelow * height;
            return new BattleFraming.CanvasRect(feet.X - hw, feet.Y - below, hw * 2f, height + below);
        }

        private static bool R29Inside(BattleFraming.CanvasRect r, BattleFraming.CanvasRect area)
        {
            return r.X >= area.X - 0.01f && r.XMax <= area.XMax + 0.01f && r.Y >= area.Y - 0.01f && r.YMax <= area.YMax + 0.01f;
        }

        private static float R29Overlap(BattleFraming.CanvasRect a, BattleFraming.CanvasRect b)
        {
            float w = Math.Min(a.XMax, b.XMax) - Math.Max(a.X, b.X);
            float h = Math.Min(a.YMax, b.YMax) - Math.Max(a.Y, b.Y);
            return w > 0f && h > 0f ? w * h : 0f;
        }

        private static void R29AtScreen(R29Screen sc)
        {
            string tag = sc.Tag;
            float scale = BattleHudLayout.CanvasScale(sc.Width, sc.Height);
            float canvasW = sc.Width / scale, canvasH = sc.Height / scale;
            float sideC = sc.Side / scale, bottomC = sc.Bottom / scale;
            BattleFraming.CanvasRect safe = new BattleFraming.CanvasRect(sideC, bottomC, canvasW - 2f * sideC, canvasH - bottomC);
            float safeTop = safe.YMax;
            BattleFraming.CanvasRect viewport = BattleHudLayout.ToCanvas(
                BattleHudLayout.Viewport(sc.Width, sc.Height, sc.Bottom, sc.Height, scale), canvasW, canvasH);

            float ortho = FormationLayout.OrthographicSize(viewport.Height, safe.Height);
            float perUnit = viewport.Height / (2f * ortho);

            // Ten feet, ten bodies (slot 3 of the enemies as a boss, the widest case), ten plates.
            BattleFraming.CanvasPoint[] feet = new BattleFraming.CanvasPoint[10];
            BattleFraming.CanvasRect[] body = new BattleFraming.CanvasRect[10];
            BattleFraming.CanvasPoint[] head = new BattleFraming.CanvasPoint[10];
            string[] id = new string[10];
            float[] height = new float[10];
            bool roundTrip = true, depthOk = true, sizeOk = true;
            float worstRound = 0f, worstSize = 0f;
            float minDepth = float.MaxValue, maxDepth = float.MinValue;
            double pitch = BattleFraming.CameraPitch * Math.PI / 180.0;
            for (int i = 0; i < 10; i++)
            {
                bool ally = i < 5;
                int slot = i % 5;
                bool boss = !ally && slot == FormationLayout.BossSlot;
                id[i] = (ally ? "A" : "E") + FormationLayout.CellOf(ally, slot).Name;
                feet[i] = FormationLayout.Feet(ally, slot, safe);
                height[i] = FormationLayout.UnitScreenHeight(safe.Height, boss);
                body[i] = R29Body(feet[i], height[i]);

                float wx, wz;
                FormationLayout.ToWorld(feet[i], viewport, ortho, out wx, out wz);
                BattleFraming.CanvasPoint back = BattleFraming.Project(wx, 0f, wz, viewport, true, ortho);
                worstRound = Math.Max(worstRound, Math.Max(Math.Abs(back.X - feet[i].X), Math.Abs(back.Y - feet[i].Y)));

                float worldH = FormationLayout.WorldHeight(boss);
                head[i] = BattleFraming.Project(wx, worldH, wz, viewport, true, ortho);
                float shown = head[i].Y - feet[i].Y;
                float want = height[i];
                worstSize = Math.Max(worstSize, Math.Abs(shown - want));
                if (Math.Abs(shown - want) > 0.5f) sizeOk = false;

                // Depth along the camera's view axis, for the feet and the head: past the 0.3 near clip and
                // inside the backdrop, which BattleStage.FitBackdrop stands 22 units out.
                float ry = 0f - BattleFraming.CameraYV2, rz = wz - BattleFraming.CameraZ;
                float dFeet = (float)(-ry * Math.Sin(pitch) + rz * Math.Cos(pitch));
                float dHead = (float)(-(worldH + ry) * Math.Sin(pitch) + rz * Math.Cos(pitch));
                minDepth = Math.Min(minDepth, Math.Min(dFeet, dHead));
                maxDepth = Math.Max(maxDepth, Math.Max(dFeet, dHead));
            }
            roundTrip = worstRound < 0.01f;
            depthOk = minDepth > 0.3f && maxDepth < 22f;
            Check(roundTrip, tag + ": the camera puts every unit's feet on its table point (round trip through BattleFraming.Project, worst "
                             + F2(worstRound) + " canvas units)");
            Check(sizeOk, tag + ": a unit's head stands " + F1(height[0]) + " canvas units over its feet = "
                          + F2(height[0] / safe.Height) + " of the safe height (a boss "
                          + F2(height[FormationLayout.BossSlot + 5] / safe.Height) + "), as drawn, worst " + F2(worstSize));
            Check(Math.Abs(height[0] / safe.Height - 0.20f) <= 0.01f && height[FormationLayout.BossSlot + 5] > height[0],
                  tag + ": scale is about 0.20 of the safe height, a boss larger");
            Check(depthOk, tag + ": every unit is in front of the near clip and in front of the backdrop (depth " + F1(minDepth) + " to " + F1(maxDepth) + ")");

            // The plates over the heads.
            BattleFraming.CanvasRect[] plate = new BattleFraming.CanvasRect[10];
            BattleFraming.CanvasRect[] plateLift = new BattleFraming.CanvasRect[10];
            BattleFraming.CanvasRect[] bars = new BattleFraming.CanvasRect[10];
            BattleFraming.CanvasRect[] barsLift = new BattleFraming.CanvasRect[10];
            for (int i = 0; i < 10; i++)
            {
                plate[i] = BattleHudLayout.PlateBoxAboveHead(head[i], false, i < 5);
                plateLift[i] = BattleHudLayout.PlateBoxAboveHead(head[i], true, i < 5);
                bars[i] = BattleHudLayout.BarsBoxAboveHead(head[i], false);
                barsLift[i] = BattleHudLayout.BarsBoxAboveHead(head[i], true);
            }

            // Inside the safe area: bodies and the whole plate, acting or not.
            bool inside = true;
            string outside = "";
            for (int i = 0; i < 10; i++)
            {
                if (!R29Inside(body[i], safe)) { inside = false; outside += " body " + id[i]; }
                if (!R29Inside(plateLift[i], safe)) { inside = false; outside += " plate " + id[i]; }
            }
            Check(inside, tag + ": every unit and every plate (bars, name, icon row, turn lift) is inside the safe area" + outside);

            // The highest bar, and the highest anything, below 0.38 of the height from the top.
            float highestBar = 0f, highestPlate = 0f;
            string highestBarAt = "";
            for (int i = 0; i < 10; i++)
            {
                if (barsLift[i].YMax > highestBar) { highestBar = barsLift[i].YMax; highestBarAt = id[i]; }
                highestPlate = Math.Max(highestPlate, plateLift[i].YMax);
            }
            float barFrac = (canvasH - highestBar) / canvasH;
            float plateFrac = (canvasH - highestPlate) / canvasH;
            Check(barFrac >= FormationLayout.BarsCeiling, tag + ": the highest bar (" + highestBarAt + ", acting) is at " + F3(barFrac)
                                                          + " of the height from the top, below " + F2(FormationLayout.BarsCeiling));
            Check(plateFrac >= FormationLayout.BarsCeiling, tag + ": the whole plate over it (icon row, and the name if one is shown) stays below " + F2(FormationLayout.BarsCeiling)
                                                           + " (at " + F3(plateFrac) + ")");

            // The open centre: the left team's furthest reach against the right team's.
            float leftBody = float.MinValue, rightBody = float.MaxValue, leftPlate = float.MinValue, rightPlate = float.MaxValue;
            for (int i = 0; i < 10; i++)
            {
                if (i < 5) { leftBody = Math.Max(leftBody, body[i].XMax); leftPlate = Math.Max(leftPlate, plateLift[i].XMax); }
                else { rightBody = Math.Min(rightBody, body[i].X); rightPlate = Math.Min(rightPlate, plateLift[i].X); }
            }
            float gapBody = (rightBody - leftBody) / safe.Width, gapPlate = (rightPlate - leftPlate) / safe.Width;
            Check(gapBody >= 0.20f && gapPlate >= 0.12f,
                  tag + ": the centre stays open, " + F3(gapBody) + " of the width between the bodies (min 0.20) and " + F3(gapPlate)
                  + " between the plates (min 0.12)");

            // Draw order: the lower unit is in front.
            int[] order = FormationLayout.DrawOrder(feet);
            bool orderOk = order.Length == 10;
            for (int k = 1; orderOk && k < 10; k++) orderOk = feet[order[k - 1]].Y >= feet[order[k]].Y;
            string[] expect = { "B1", "F1", "B2", "F2", "B3" };
            string got = "";
            for (int k = 0; k < 10; k++) if (order[k] < 5) got += FormationLayout.CellOf(true, order[k]).Name + " ";
            string want5 = string.Join(" ", expect) + " ";
            Check(orderOk && got == want5, tag + ": draw order by feet height, back to front (one team: " + got.Trim() + ")");

            // Bodies overlap only slightly.
            float worstOverlap = 0f, worstBossOverlap = 0f;
            string worstOverlapAt = "", worstBossAt = "";
            for (int i = 0; i < 10; i++)
                for (int j = i + 1; j < 10; j++)
                {
                    float smaller = Math.Min(body[i].Width * body[i].Height, body[j].Width * body[j].Height);
                    float f = R29Overlap(body[i], body[j]) / smaller;
                    bool withBoss = i == FormationLayout.BossSlot + 5 || j == FormationLayout.BossSlot + 5;
                    if (withBoss) { if (f > worstBossOverlap) { worstBossOverlap = f; worstBossAt = id[i] + "/" + id[j]; } }
                    else if (f > worstOverlap) { worstOverlap = f; worstOverlapAt = id[i] + "/" + id[j]; }
                }
            Check(worstOverlap <= 0.30f && worstBossOverlap <= 0.55f,
                  tag + ": units overlap only slightly, worst " + F3(worstOverlap) + " of the smaller body (" + worstOverlapAt
                  + ", max 0.30), a boss in B2 (larger, drawn in front of the unit behind it) " + F3(worstBossOverlap)
                  + " (" + worstBossAt + ", max 0.55)");

            // Plates against plates, the turn lift on one at a time.
            float worstPlate = float.MaxValue;
            string worstPlateAt = "";
            for (int i = 0; i < 10; i++)
                for (int j = 0; j < 10; j++)
                {
                    if (i == j) continue;
                    float c = BattleHudLayout.Clearance(plateLift[i], plate[j]);
                    if (c < worstPlate) { worstPlate = c; worstPlateAt = id[i] + " acting / " + id[j]; }
                }
            Check(worstPlate >= BattleHudMetrics.PlateMinClearance,
                  tag + ": no two plates meet, worst clearance " + F1(worstPlate) + " (" + worstPlateAt + ", min " + F1(BattleHudMetrics.PlateMinClearance) + ")");

            // The HUD never covers a unit or its bars.
            BattleFraming.CanvasRect bar = BattleHudLayout.TopBar(canvasW, canvasH, safeTop);
            BattleFraming.CanvasRect callout = BattleHudLayout.Callout(canvasW, safeTop, BattleHudMetrics.CalloutMaxWidth);
            BattleFraming.CanvasRect next = BattleHudLayout.Next(canvasW, safe.Y, true, BattleHudMetrics.NextMaxUpcoming);
            float worstHud = float.MaxValue;
            string worstHudAt = "";
            for (int i = 0; i < 10; i++)
            {
                BattleFraming.CanvasRect[] mine = { body[i], plateLift[i], barsLift[i] };
                string[] what = { "body", "plate", "bars" };
                BattleFraming.CanvasRect[] hud = { bar, callout, next };
                string[] hudName = { "top bar", "callout", "turn pill" };
                for (int a = 0; a < mine.Length; a++)
                    for (int b = 0; b < hud.Length; b++)
                    {
                        float c = BattleHudLayout.Clearance(mine[a], hud[b]);
                        if (c < worstHud) { worstHud = c; worstHudAt = id[i] + " " + what[a] + " / " + hudName[b]; }
                    }
            }
            Check(worstHud >= BattleHudMetrics.PlateMinClearance,
                  tag + ": the top bar, the callout and the turn order pill never touch a unit, its plate or its bars, worst clearance "
                  + F1(worstHud) + " (" + worstHudAt + ", min " + F1(BattleHudMetrics.PlateMinClearance) + ")");

            // The camera frame holds the team: the viewport is the field below the bar, the bars sit in it.
            bool inField = true;
            for (int i = 0; i < 10; i++) inField &= barsLift[i].YMax <= viewport.YMax && plateLift[i].X >= viewport.X && plateLift[i].XMax <= viewport.XMax;
            Check(inField && viewport.YMax <= BattleHudLayout.BarBottom(safeTop) + 0.01f,
                  tag + ": every plate is inside the camera's viewport, which stops at the top bar");
        }

        // ---------------------------------------------------------------- attack movement

        private static void R29Approach(string root)
        {
            string path = Path.Combine(root, Path.Combine("Assets", Path.Combine("Scripts", Path.Combine("Battle", "UnitActor.cs"))));
            if (!File.Exists(path)) { Check(false, "UnitActor.cs is found under " + root); return; }
            Match m = Regex.Match(File.ReadAllText(path), @"ApproachDistance\s*=\s*([0-9.]+)f");
            float approach = m.Success ? float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : -1f;
            Check(approach > 0f, "UnitActor.ApproachDistance is read from source (" + G(approach) + ")");

            bool ok = approach > 0f;
            float worst = 0f;
            string worstAt = "";
            for (int s = 0; ok && s < R29Screens.Length; s++)
            {
                R29Screen sc = R29Screens[s];
                float scale = BattleHudLayout.CanvasScale(sc.Width, sc.Height);
                float canvasW = sc.Width / scale, canvasH = sc.Height / scale;
                BattleFraming.CanvasRect safe = new BattleFraming.CanvasRect(sc.Side / scale, sc.Bottom / scale, canvasW - 2f * sc.Side / scale, canvasH - sc.Bottom / scale);
                float step = approach * FormationLayout.ApproachScale * FormationLayout.PerUnit(safe.Height);
                float centre = safe.X + safe.Width * 0.5f;
                for (int i = 0; i < 10; i++)
                {
                    BattleFraming.CanvasPoint f = FormationLayout.Feet(i < 5, i % 5, safe);
                    // How much of the way to the centre line the step is, and whether it stays on screen.
                    float toCentre = Math.Abs(f.X - centre);
                    float used = step / toCentre;
                    if (used > worst) { worst = used; worstAt = sc.Tag + " " + (i < 5 ? "A" : "E") + FormationLayout.CellOf(i < 5, i % 5).Name; }
                    if (used >= 0.5f || f.X - step < safe.X || f.X + step > safe.XMax) ok = false;
                }
            }
            Check(ok, "a melee step (ApproachDistance x ApproachScale) is at most " + F3(worst) + " of the way from a unit to the centre line ("
                      + worstAt + ", max 0.5): it stays in the open centre and on screen at every shape");
        }

        // ---------------------------------------------------------------- wiring

        private static string R29Read(string root, string rel)
        {
            string path = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        private static void R29Wiring(string root)
        {
            string stage = R29Read(root, "Assets/Scripts/Battle/BattleStage.cs");
            string screen = R29Read(root, "Assets/Scripts/UI/Screens/BattleScreen.cs");
            string actor = R29Read(root, "Assets/Scripts/Battle/UnitActor.cs");
            if (stage == null || screen == null || actor == null) { Check(false, "BattleStage, BattleScreen and UnitActor are found"); return; }

            Check(stage.Contains("FormationLayout.Feet(") && stage.Contains("FormationLayout.ToWorld(")
                  && stage.Contains("FormationLayout.ResolveSlots(") && stage.Contains("FormationLayout.OrthographicSize("),
                  "BattleStage stands units where FormationLayout puts them and frames them with its orthographic size");
            Check(screen.Contains("BattleHudLayout.PlateOriginAboveHead(") && screen.Contains("_world.FormationArea = "),
                  "BattleScreen hangs each plate over its unit's head and hands the stage the safe area");
            Check(actor.Contains("FormationLayout.WorldHeight(") && actor.Contains("FormationLayout.ApproachScale"),
                  "UnitActor draws every unit at the formation's height and steps by the formation's approach");

            // The table lives in FormationLayout and nowhere else.
            Regex literal = new Regex(@"\b0\.(64|77|69|84)f\b");
            bool single = !literal.IsMatch(stage) && !literal.IsMatch(actor);
            Check(single, "the formation's numbers are written in FormationLayout.cs only (none in BattleStage or UnitActor)");

            // Every tool that compiles the harness compiles FormationLayout with it.
            string gate = R29Read(root, "Tools/build-check.ps1") ?? "";
            string solver = R29Read(root, "Tools/solve-recommended-power.ps1") ?? "";
            Check(gate.Contains("FormationLayout.cs") && solver.Contains("FormationLayout.cs")
                  && gate.Contains("^R2[4679]") && solver.Contains("^R2[4679]"),
                  "build-check.ps1 and solve-recommended-power.ps1 both compile FormationLayout.cs and glob R24, R26, R27 and R29 checks");
        }

        private static string F3(float v) { return v.ToString("0.000", CultureInfo.InvariantCulture); }
    }
}
