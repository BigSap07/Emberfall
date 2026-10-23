using System;

namespace Emberfall.Battle
{
    /// <summary>
    /// THE BATTLE FORMATION, AS DATA: where each of a side's five units stands on the screen, as a
    /// fraction of the safe area, Idle Heroes style, in the bottom half of the frame.
    ///
    /// ONE TABLE (Cells below) is the whole layout, so it is the one place to tune. Everything else
    /// here is arithmetic over it: where the feet land inside a safe area, how big a unit reads, the
    /// world position and orthographic size that make the battle camera put them there, and which
    /// unit is drawn in front. BattleStage reads it to stand the actors; BattleScreen reads it (through
    /// BattleHudLayout) to hang each unit's bars above its head; Tools/DevHarness (R29FormationChecks)
    /// compiles this very file and proves the table without Unity. Free of UnityEngine for that reason.
    ///
    /// THE TARGET (1920x1080 reference). Positions are where a unit's FEET stand, as fractions of the
    /// safe area measured from its left edge and from its TOP edge. The right team is the mirror image
    /// (x becomes 1 minus x). Two ranks, as the game has them (BattleFraming.FrontRankSlots): a front
    /// pair and a back three.
    ///
    ///     back row   B1 0.18, 0.64   B2 0.13, 0.77   B3 0.09, 0.90
    ///     front row  F1 0.32, 0.69   F2 0.29, 0.84
    ///
    /// A unit is about UnitHeightFraction of the safe height tall, heroes and enemies alike (the rig is
    /// scaled to it, UnitActor.Setup); a boss is larger (BossScaleAt). The lower unit is drawn in
    /// front. Health and energy bars sit just above the head and the highest of them stays below
    /// BarsCeiling of the height from the top, so the background art above stays clear. The centre
    /// between the teams stays open, which is where attacks travel.
    ///
    /// GAME SLOT TO CELL. The simulation numbers a side's slots 0 to 4 (front 0 and 1, back 2 to 4).
    /// Before this table, slot 0 stood nearest the camera (the lowest on screen) and slot 4 furthest,
    /// so SlotCell keeps that order rather than reshuffling who stands where: slot 0 is F2, slot 1 is
    /// F1, slot 2 is B3, slot 3 is B2 (the middle) and slot 4 is B1. DEFAULT TAKEN, the mapping is
    /// one array, flip it there.
    ///
    /// THE BOSS. The target would have a boss take the middle back cell, B2. The campaign lists a boss
    /// FIRST in its line (campaign.json, every stage 12 and the minibosses of chapters 1 and 2), which
    /// is slot 0, and the simulation's FrontEnemy rule targets the lowest slot: a boss drawn in B2
    /// would be hit as the front unit from behind two others, which the screen must not say. So the
    /// boss stands in the cell of the slot it has (F2), larger, and the middle back cell (BossSlot) is
    /// where it would stand if content ever put it in slot 3. DEFAULT TAKEN: no content change.
    /// </summary>
    public static class FormationLayout
    {
        /// <summary>The kill switch. True stands the battle on this table; false returns every
        /// battle to the R12 and R15 slots in BattleFraming, which stay in the tree for exactly that.</summary>
        public static readonly bool Enabled = true;

        public struct Cell
        {
            public readonly string Name;
            /// <summary>Fraction of the safe area's width from its left edge, for the left team.</summary>
            public readonly float X;
            /// <summary>Fraction of the safe area's height from its top edge.</summary>
            public readonly float Y;

            public Cell(string name, float x, float y) { Name = name; X = x; Y = y; }
        }

        public const int Count = 5;

        // THE TABLE. Index 0 to 4 is B1, B2, B3, F1, F2, in the order the target lists them.
        public const int B1 = 0, B2 = 1, B3 = 2, F1 = 3, F2 = 4;

        public static readonly Cell[] Cells =
        {
            new Cell("B1", 0.18f, 0.64f),
            new Cell("B2", 0.13f, 0.77f),
            new Cell("B3", 0.09f, 0.90f),
            new Cell("F1", 0.32f, 0.69f),
            new Cell("F2", 0.29f, 0.84f)
        };

        /// <summary>Game slot (0 to 4) to the index in Cells. See the class comment.</summary>
        public static readonly int[] SlotCell = { F2, F1, B3, B2, B1 };

        /// <summary>The cell the boss stands in, as a game slot: the middle of the back rank.</summary>
        public const int BossSlot = 3;

        /// <summary>How tall a unit reads, as a fraction of the safe area's height.</summary>
        public const float UnitHeightFraction = 0.20f;

        /// <summary>A boss's height as a multiple of an ordinary unit's.</summary>
        public const float BossScale = 1.30f;

        /// <summary>The highest bar's top stays at or below this fraction of the height from the top.</summary>
        public const float BarsCeiling = 0.38f;

        /// <summary>A standard unit's top, in world units. It sets how many pixels a world unit is, and
        /// so the camera's orthographic size, and it is close to the 1.2x look's own heroes, so the effects
        /// that are sized in world units (rings, impacts, bolts) keep the size they were authored at.</summary>
        public const float WorldUnitHeight = 2.30f;

        /// <summary>A melee unit's step toward its target (UnitActor.ApproachDistance, 0.85 world units)
        /// is scaled by this. 0.68 world units is about 70 pixels at 1080p: well inside the open centre,
        /// which is over 600 pixels between the front pairs.</summary>
        public const float ApproachScale = 0.8f;

        // ---------------------------------------------------------------- slots

        /// <summary>The cell a game slot stands in. A slot outside 0 to 4 is clamped into the five a
        /// side has, which is what BattleFraming.Slot always did.</summary>
        public static Cell CellOf(bool isPlayer, int slot)
        {
            int i = slot < 0 ? 0 : slot >= Count ? Count - 1 : slot;
            Cell c = Cells[SlotCell[i]];
            return isPlayer ? c : new Cell(c.Name, 1f - c.X, c.Y);
        }

        /// <summary>
        /// Where each unit of a side stands, given the slot each one asks for. A slot is kept as asked
        /// (clamped into 0 to 4) unless an earlier unit already holds it; a unit that finds its slot
        /// taken, or that asks for one past the end, takes the nearest free slot (by number, the lower
        /// first on a tie). A side with fewer than five units keeps the slots they occupy and does not
        /// recentre: the empty cells simply stay empty.
        /// </summary>
        public static int[] ResolveSlots(int[] requested)
        {
            int n = requested == null ? 0 : requested.Length;
            int[] resolved = new int[n];
            bool[] taken = new bool[Count];
            for (int i = 0; i < n; i++) resolved[i] = -1;

            // First pass: every unit whose clamped slot nobody has claimed keeps it.
            for (int i = 0; i < n; i++)
            {
                int want = requested[i] < 0 ? 0 : requested[i] >= Count ? Count - 1 : requested[i];
                if (taken[want]) continue;
                taken[want] = true;
                resolved[i] = want;
            }

            // Second pass: the rest take the nearest free slot, or the nearest by number when all
            // five are held (a sixth unit has nowhere to stand and shares the nearest one).
            for (int i = 0; i < n; i++)
            {
                if (resolved[i] >= 0) continue;
                int want = requested[i] < 0 ? 0 : requested[i] >= Count ? Count - 1 : requested[i];
                int best = want;
                bool found = false;
                for (int d = 1; d < Count && !found; d++)
                {
                    int lo = want - d, hi = want + d;
                    if (lo >= 0 && !taken[lo]) { best = lo; found = true; }
                    else if (hi < Count && !taken[hi]) { best = hi; found = true; }
                }
                if (found) taken[best] = true;
                resolved[i] = best;
            }
            return resolved;
        }

        // ---------------------------------------------------------------- screen

        /// <summary>Where a unit's feet land inside a safe area, in the area's own units (canvas units
        /// or pixels), origin bottom-left, y up.</summary>
        public static BattleFraming.CanvasPoint Feet(bool isPlayer, int slot, BattleFraming.CanvasRect safe)
        {
            Cell c = CellOf(isPlayer, slot);
            return new BattleFraming.CanvasPoint(safe.X + c.X * safe.Width, safe.YMax - c.Y * safe.Height);
        }

        /// <summary>What a boss's bars (and the turn lift and the gap under them) need above its head,
        /// as a fraction of the height: the head may not stand higher than the ceiling plus this.</summary>
        public const float BarsReserve = 0.055f;

        /// <summary>
        /// How much larger than an ordinary unit a boss in this slot is drawn: BossScale, held down where
        /// a taller body would push its bars above the ceiling. The campaign puts a boss first in its
        /// line (slot 0, the front pair's lower cell), where the full BossScale fits; B2 has room for
        /// more than BossScale; the highest cell, B1, has room for about 1.1.
        /// </summary>
        public static float BossScaleAt(int slot)
        {
            int i = slot < 0 ? 0 : slot >= Count ? Count - 1 : slot;
            float room = Cells[SlotCell[i]].Y - BarsCeiling - BarsReserve;
            float scale = room / UnitHeightFraction;
            return scale < 1f ? 1f : scale > BossScale ? BossScale : scale;
        }

        /// <summary>How tall a unit reads on screen: UnitHeightFraction of the safe height, times
        /// BossScaleAt(slot) for a boss.</summary>
        public static float UnitScreenHeight(float safeHeight, bool boss, int slot)
        {
            return UnitHeightFraction * safeHeight * (boss ? BossScaleAt(slot) : 1f);
        }

        /// <summary>A unit's top in world units: WorldUnitHeight, times BossScaleAt(slot) for a boss.
        /// UnitActor scales the rig to it.</summary>
        public static float WorldHeight(bool boss, int slot)
        {
            return WorldUnitHeight * (boss ? BossScaleAt(slot) : 1f);
        }

        // ---------------------------------------------------------------- camera

        /// <summary>Screen units per world unit that make a standard unit UnitHeightFraction tall. A
        /// vertical body foreshortens by the cosine of the camera's pitch.</summary>
        public static float PerUnit(float safeHeight)
        {
            double pitch = BattleFraming.CameraPitch * Math.PI / 180.0;
            return (float)(UnitHeightFraction * safeHeight / (WorldUnitHeight * Math.Cos(pitch)));
        }

        /// <summary>The orthographic size that gives PerUnit(safeHeight) for a camera whose viewport is
        /// viewportHeight tall (the same units as safeHeight).</summary>
        public static float OrthographicSize(float viewportHeight, float safeHeight)
        {
            float perUnit = PerUnit(safeHeight);
            return perUnit > 0f ? viewportHeight / (2f * perUnit) : BattleFraming.OrthographicSizeV2;
        }

        /// <summary>The camera's height: the 1.2x look's camera. Only the pitch and the world position of
        /// the units matter to the picture, and ToWorld solves the second for this.</summary>
        public static float CameraY { get { return BattleFraming.CameraYV2; } }

        /// <summary>
        /// The world position (x across, z into the field, standing on y 0) at which the battle camera,
        /// with this viewport and orthographic size, puts a unit's feet at the screen point. The inverse
        /// of BattleFraming.Project for the formation's camera, which the harness proves round-trips.
        /// </summary>
        public static void ToWorld(BattleFraming.CanvasPoint feet, BattleFraming.CanvasRect viewport,
                                   float orthographicSize, out float x, out float z)
        {
            double pitch = BattleFraming.CameraPitch * Math.PI / 180.0;
            float cos = (float)Math.Cos(pitch);
            float sin = (float)Math.Sin(pitch);
            float perUnit = viewport.Height / (2f * orthographicSize);
            float right = (feet.X - (viewport.X + viewport.Width * 0.5f)) / perUnit;
            float up = (feet.Y - (viewport.Y + viewport.Height * 0.5f)) / perUnit;
            x = BattleFraming.CameraX + right;
            z = BattleFraming.CameraZ + (up + CameraY * cos) / sin;
        }

        // ---------------------------------------------------------------- order

        /// <summary>Indices of the points back to front: the higher on screen is behind, so it is drawn
        /// first and the lower unit is drawn over it. Ties keep their order.</summary>
        public static int[] DrawOrder(BattleFraming.CanvasPoint[] feet)
        {
            int n = feet.Length;
            int[] order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            // Insertion sort: stable, and n is ten.
            for (int i = 1; i < n; i++)
            {
                int v = order[i];
                int j = i - 1;
                while (j >= 0 && feet[order[j]].Y < feet[v].Y) { order[j + 1] = order[j]; j--; }
                order[j + 1] = v;
            }
            return order;
        }
    }
}
