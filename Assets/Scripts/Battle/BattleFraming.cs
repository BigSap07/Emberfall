using System;

namespace Emberfall.Battle
{
    /// <summary>
    /// WHERE THE TEN SLOTS STAND AND HOW THE BATTLE CAMERA SEES THEM, as numbers and nothing else.
    ///
    /// BattleStage reads its slots and its camera from here, so this is the one place either is
    /// written down. The battle HUD's layout model (BattleHudLayout) reads the same numbers to say
    /// where a unit's feet land on the canvas, which is what lets Tools/DevHarness prove that no two
    /// unit plates overlap at any formation slot without opening Unity.
    ///
    /// Deliberately free of UnityEngine, so the harness compiles this file beside the core the way
    /// it compiles BattleClips.cs. Project ignores camera shake: shake is a transient of at most
    /// 0.18 world units that moves every plate together, so it can never make two of them meet.
    /// </summary>
    public static class BattleFraming
    {
        /// <summary>A point on the ground plane: x across the field, z into it.</summary>
        public struct SlotPoint
        {
            public readonly float X;
            public readonly float Z;

            public SlotPoint(float x, float z) { X = x; Z = z; }
        }

        // Left-versus-right clusters, replacing the opposing-lines arrangement. Allies occupy
        // negative x, enemies occupy positive x, and the empty middle band that used to run
        // horizontally across the screen (the gap between a near row and a far row) now runs
        // vertically down the centre instead (the gap between the two x clusters). Positions
        // only; facing lives in UnitRig.Build, which turns each side toward the other by
        // UnitRig.ThreeQuarterTurn off camera-square, mirrored by IsPlayer.
        //
        // Each cluster's internal axes swap roles from the old design: x used to be the free
        // spread axis and z separated the two rows. Now x separates the two sides and, within a
        // side, separates that side's own ranks from the centreline outward, while z is the
        // free axis units within a rank spread across.
        //
        // Two ranks: front 2 (slots 0, 1), back 3 (slots 2, 3, 4).
        //
        // FrontEnemy (CombatSimulation.cs) targets whichever alive unit has the lowest Slot
        // index, nothing more - it is not aware of rank or position. With a single front slot
        // that was exactly "the physically front unit." With two, it reliably still targets a
        // front-rank unit as long as either survives (both are genuinely front), but of the two
        // it always prefers slot 0 over slot 1 while both are alive, an arbitrary tie-break
        // rather than anything about which is more exposed - they stand at the same depth and
        // read as equally exposed on screen. Once both front slots are dead it falls through to
        // whichever back-rank slot has the lowest index, the same tie-break one layer down.
        //
        // This is a real behaviour change, not a cosmetic one: a content author ordering an
        // enemy list in campaign.json, or a player choosing deploy order, is now implicitly
        // choosing which of two equally-front units draws single-target front fire, likely
        // without realising slot order carries that weight. Options, none taken here since all
        // of them touch CombatSimulation.cs (Core), which this pass does not touch:
        //   1. Leave it. Low stakes unless a specific hero or enemy identity depends on reliably
        //      drawing front-target fire (a designated tank), in which case that identity now
        //      only holds if it is placed in slot 0 specifically, not just "the front rank."
        //   2. Change the tie-break among units sharing the minimum rank (not raw slot index) to
        //      something that does not privilege one slot - lowest current HP, or random per
        //      resolution - so both front-rank units genuinely share exposure.
        //   3. Introduce an explicit rank concept FrontEnemy reads instead of inferring "front"
        //      from slot order at all, so the targeting rule and the visual layout stay in sync
        //      by construction rather than by the layout happening to put low slots first.
        // Reported so the choice is made deliberately, not decided here.
        // Tightened again: front x 1.60 -> 1.40, back x 4.25 -> 3.95, front z +/-3.45 -> +/-3.25,
        // back z +/-5.25 -> +/-4.95. Cumulatively from where this started, front x 2.40 -> 1.40
        // and back z +/-6.42 -> +/-4.95.
        //
        // CAMERA PITCH WAS TESTED AS A LEVER FOR CHARACTER SIZE AND IT IS NOT ONE. The reasoning
        // that it should be is sound as far as it goes: a z gap costs H / tan(pitch), which falls
        // 24% between 28 and 35 degrees, on exactly the axis that binds. But the saving does not
        // survive projection. The z spread needed is proportional to H / tan(p), and what it
        // contributes to the VERTICAL FRAME is that spread times sin(p):
        //     (H / tan(p)) * sin(p) = H * cos(p)
        // and a character's own apparent height is also H * cos(p), since a vertical billboard
        // foreshortens by the cosine. Every term in the vertical budget carries the same cos(p),
        // so orthographicSize falls in exact proportion to how much shorter the character gets,
        // and apparent size is invariant. Pitch buys nothing by construction, not by coincidence.
        //
        // Measured, holding minimum separation fixed at 0.373 and re-solving the layout at each
        // pitch, character height in pixels at 1080p:
        //     24 deg 215.5   28 deg 213.0   32 deg 210.1   35 deg 207.9   40 deg 203.6   45 deg 198.7
        // Steeper is monotonically WORSE, and 35 degrees would have cost 2.4%. The residual slope
        // is the blob shadow: it lies flat, so its projected depth grows with sin(p) while
        // everything else shrinks with cos(p). Pitch stays at 28, which is also the shallow end of
        // the range that still reads as a battle line rather than a map. For the record the point
        // where it stops reading as a line is somewhere near 45, where a depth step and a height
        // step project equally (tan = 1) and rank separation stops being distinguishable from
        // height, but there is no reason to go looking for it: the direction is unprofitable from
        // the first degree.
        //
        // What did buy size, since pitch could not:
        //   the minimum separation target, 0.373 -> 0.232 world units (33 px at 1080p, still
        //     clearly a gap), which let the whole z spread come in;
        //   the blob shadow, 1.35 x 1.05 -> 1.10 x 0.85 (see UnitRig.ShadowWidth), which was
        //     wider than any body it sat under and was 5.5% of the vertical budget on its own.
        // Together those are orthographicSize 3.85 -> 3.65, characters 5% larger. That is the
        // honest ceiling for this content: the vertical budget at the shipped layout is 65% back
        // rank z spread, 22% the tallest hero, 5.5% shadow, 4.2% shake reserve, 2.8% cast reserve,
        // and the z spread is already at its floor for the separation above. Anything much larger
        // needs either overlap allowed, per-stage framing, or the two golem-scale enemies
        // (Stone Warden 1.18, Sundered Colossus 1.55) toned down in enemies.json.
        //
        // x closed as far as it goes; that air was free, since vertical binds either way.
        //
        // Every number re-derived rather than scaled down by eye, and one correction in the
        // method itself made most of the room the first time:
        //
        // The old rule was "clearing on z costs H / sin(28)". That is not the requirement. Two
        // units at the same x are separated on screen by dz*sin(28), and the near one's head
        // reaches H*cos(28) above its own feet, so what non-overlap actually costs is
        //   dz >= H * cos(28) / sin(28) = H / tan(28) = H * 1.881
        // not H / sin(28) = H * 2.130. The old formula over-charged every z gap by 13%.
        //
        // Binding pairs, over every authored composition in campaign.json and every hero in every
        // slot at team sizes 1 to 5:
        //   front rank  Sundered Colossus slot 0 next to a Grave Moth slot 1 (ch1_12). The
        //               Colossus's hammer tips out at 3.162, so 2*Zf*sin(28) must clear
        //               3.162*cos(28): Zf >= 3.208 at the current separation. Chosen 3.25.
        //   back rank   Stone Warden slot 3 next to a Hollow Husk slot 4 (ch1_10). Hammer top
        //               2.407, so Zb >= 4.925. Chosen 4.95, and this pair is what sets the final
        //               minimum separation.
        //   sides       Nyx Serrow slot 0 against the Colossus slot 0 across the middle. Their
        //               turned half-widths are 0.63 and 1.32, so Xf >= 1.114. Chosen 1.40.
        //   x-step      Colossus slot 0 against a Grave Moth slot 2, needing Xb - Xf >= 2.320.
        //               Chosen 2.55.
        //
        // Minimum screen separation over everything checked: 0.232 world units, 33 px at 1080p,
        // at the Stone Warden / Hollow Husk pair above. Nothing overlaps at any team size.
        //
        // Half-widths come from the assembled rig, not from a nominal figure: the local AABB of
        // every primitive a Build* method emits including its weapon, turned by
        // UnitRig.ThreeQuarterTurn, which widens a procedural rig (real depth rotates into the
        // width axis) and narrows a flat sprite quad (no depth to rotate). Shadows are excluded
        // from the overlap test on purpose - they are soft blobs on the ground and reading as
        // pooled is correct - but they ARE included in the frame bound below, which is where the
        // last pass lost them.
        public static readonly SlotPoint[] AllySlots =
        {
            new SlotPoint(-1.4f, -3.25f), new SlotPoint(-1.4f, 3.25f),
            new SlotPoint(-3.95f, -4.95f), new SlotPoint(-3.95f, 0f), new SlotPoint(-3.95f, 4.95f)
        };

        public static readonly SlotPoint[] EnemySlots =
        {
            new SlotPoint(1.4f, -3.25f), new SlotPoint(1.4f, 3.25f),
            new SlotPoint(3.95f, -4.95f), new SlotPoint(3.95f, 0f), new SlotPoint(3.95f, 4.95f)
        };

        /// <summary>Slots 0 and 1 are the front rank; 2 to 4 are the back.</summary>
        public const int FrontRankSlots = 2;

        /// <summary>The slot a unit stands in, clamped into the five a side has, which is what
        /// BattleStage.Populate always did with an out-of-range Slot.</summary>
        public static SlotPoint Slot(bool isPlayer, int slot)
        {
            SlotPoint[] side = isPlayer ? AllySlots : EnemySlots;
            int i = slot < 0 ? 0 : slot >= side.Length ? side.Length - 1 : slot;
            return side[i];
        }

        // Orthographic (unchanged) at 28 degrees of pitch (unchanged, held fixed across this
        // pass by instruction): apparent size still depends only on orthographicSize, never
        // distance, and f(y, z) = y*cos(28) + z*sin(28) = y*0.8829 + z*0.4695 is still the
        // formula mapping a world point to screen height (see the slot comment above for
        // what it implies about occlusion in this new arrangement).
        //
        // orthographicSize re-derived against the tightened slots above. Bounds taken over
        // every hero in every slot and every enemy in the slots campaign.json actually places
        // it in, and over the animation range rather than the rest pose alone. Two extras are
        // reserved that earlier passes left out, because both were caught clipping:
        //   the blob shadow's rotated footprint, 0.79 * scale of z-reach past the slot, and
        //   0.85 * scale of x-reach, which is what made the bottom of frame slice shadows;
        //   the camera shake, +/-0.18 world (with the R12 juice flag off BattleScreen asks 0.14 on
        //   a crit and 0.18 on a burst; with it on the director asks 0.09 on a crit and 0.15 on a
        //   skill, and JuiceChecks pins both under 0.18), which moves the frame 0.18*cos(28) = 0.159 vertically. The backdrop
        //   already budgeted for shake; the frame bound never did.
        //
        //   highest  f  4.388   Orrin Kaal, slot 4, mid-cast, at shake extreme
        //   lowest   f -2.806   Orrin Kaal, slot 2, shadow near edge, at shake extreme
        //   span 7.194, half-span 3.597, centre 0.791
        //
        // Horizontal: widest reach is a Stone Warden in the back rank's outer slot, 3.95 plus
        // its own shadow half-width 0.81, plus shake, = 5.15.
        //   orthographicSize by width = 5.15 / 1.778 = 2.90
        //
        // Vertical binds, 3.60 against 2.90. Chosen 3.65, which leaves about 0.05 world (8 px
        // at 1080p) beyond the already-reserved shake and cast at each edge, and 1.34 world
        // (198 px) of unused air at each SIDE. Horizontal is not close and cannot become
        // close at this aspect: it would only bind below an aspect ratio of 5.15 / 3.65 =
        // 1.41, so anything at or above 3:2 is clear and 16:9 has room to spare.
        //
        // 3.85 -> 3.65 renders everything 5% larger again. See the slot comment above for why
        // that is the ceiling and what would have to change to beat it.
        //
        // The battle screen's top bar takes the top of the screen out of the camera's viewport
        // (BattleHudLayout.Viewport) and orthographicSize stays 3.65 against the shorter viewport,
        // so every number above still holds in world units and the whole field renders about 10%
        // smaller on screen. That was accepted when the HUD was reworked: it keeps each clearance
        // above true as stated, and no unit or plate can be drawn under the bar.
        //
        // Camera: x 0, symmetric. z -9.5 unchanged. y re-solved for the new centre:
        // camY = (0.791 + 9.5*0.4695) / 0.8829 = 5.95. Floors re-checked: camY (5.95) is
        // still above orthographicSize*cos(28) (3.22), and the nearest occupied point is well
        // past the 0.3 near clip.
        public const float CameraPitch = 28f;
        public const float CameraX = 0f;
        public const float CameraY = 5.95f;
        public const float CameraZ = -9.5f;
        public const float OrthographicSize = 3.65f;

        /// <summary>A rectangle in canvas units, origin bottom-left, y up.</summary>
        public struct CanvasRect
        {
            public readonly float X;
            public readonly float Y;
            public readonly float Width;
            public readonly float Height;

            public CanvasRect(float x, float y, float width, float height)
            {
                X = x; Y = y; Width = width; Height = height;
            }

            public float XMax { get { return X + Width; } }
            public float YMax { get { return Y + Height; } }
        }

        /// <summary>A point in canvas units, origin bottom-left, y up.</summary>
        public struct CanvasPoint
        {
            public readonly float X;
            public readonly float Y;

            public CanvasPoint(float x, float y) { X = x; Y = y; }
        }

        /// <summary>
        /// Where a world point lands, for the camera at rest, given the camera's viewport in the
        /// same canvas units as the answer. Under orthographic projection one world unit is
        /// viewport.Height / (2 * OrthographicSize) canvas units everywhere in the frame, so this
        /// is exact rather than an average across near and far units. The editor compares it
        /// against Camera.WorldToScreenPoint on the first steady frame of every battle
        /// (BattleScreen), which is what ties this model to the real camera.
        /// </summary>
        public static CanvasPoint Project(float x, float y, float z, CanvasRect viewport)
        {
            double pitch = CameraPitch * Math.PI / 180.0;
            float cos = (float)Math.Cos(pitch);
            float sin = (float)Math.Sin(pitch);

            // The camera's right is world +x and its up is (0, cos, sin) after pitching down about x.
            float right = x - CameraX;
            float up = (y - CameraY) * cos + (z - CameraZ) * sin;

            float perUnit = viewport.Height / (2f * OrthographicSize);
            return new CanvasPoint(viewport.X + viewport.Width * 0.5f + right * perUnit,
                                   viewport.Y + viewport.Height * 0.5f + up * perUnit);
        }
    }
}
