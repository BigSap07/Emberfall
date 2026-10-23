using System;

namespace Emberfall.Core.Screens
{
    /// <summary>
    /// The guild dungeon ladder's layout maths, with no engine in it so the harness can test it
    /// (Docs/ui-specs/R28-SCREENS.md, "Guild dungeon: the size-scaled ladder"). The screen turns
    /// these numbers into rects and decides nothing else.
    ///
    /// Two rules live here. The pip strip: PipCount pips, a gap after every fifth (between the
    /// blocks), and at ten the SPACING shrinks to fit, never the pips. The tap target: a row is 96
    /// tall and a tap target is at least the hub's touch minimum, so the ATTEMPT button is taller
    /// than its row, overhangs it equally top and bottom, and the list's spacing is at least the
    /// two overhangs so two neighbouring hit rects never overlap.
    /// </summary>
    public static class LadderLayout
    {
        /// <summary>A floor row is this tall (R28-SCREENS.md).</summary>
        public const float RowHeight = 96f;

        /// <summary>A pip is this wide and this tall, in canvas units. It never changes.</summary>
        public const float PipSize = 30f;

        /// <summary>The spacing between two pips of one block when there is room (canvas units).</summary>
        public const float PipSpacingPreferred = 12f;

        /// <summary>The least spacing between two pips, below which a strip is wider than the screen allows.</summary>
        public const float PipSpacingFloor = 2f;

        /// <summary>The gap after every fifth pip, between the two blocks (UITheme.Space8).</summary>
        public const float PipBlockGap = 8f;

        /// <summary>Pips in one block: the Champion floor closes it.</summary>
        public const int PipsPerBlock = 5;

        /// <summary>How many block gaps a strip of this many pips has: one after every fifth, none after the last.</summary>
        public static int BlockGaps(int count)
        {
            if (count <= 0) return 0;
            return (count - 1) / PipsPerBlock;
        }

        /// <summary>The whole strip's width for a pip count at a spacing.</summary>
        public static float StripWidth(int count, float pip, float spacing, float blockGap)
        {
            if (count <= 0) return 0f;
            return count * pip + (count - 1) * spacing + BlockGaps(count) * blockGap;
        }

        /// <summary>
        /// The spacing between neighbouring pips for a strip that must fit in `available`: the preferred
        /// spacing when it fits, else the largest that does, never below the floor. The pip itself is
        /// never part of the answer.
        /// </summary>
        public static float PipSpacing(int count, float available, float pip, float blockGap)
        {
            if (count <= 1) return PipSpacingPreferred;
            float fixedWidth = count * pip + BlockGaps(count) * blockGap;
            float room = available - fixedWidth;
            float spacing = room / (count - 1);
            if (spacing > PipSpacingPreferred) spacing = PipSpacingPreferred;
            if (spacing < PipSpacingFloor) spacing = PipSpacingFloor;
            return spacing;
        }

        /// <summary>The left edge of pip `index` (0 based) from the strip's left edge.</summary>
        public static float PipX(int index, float pip, float spacing, float blockGap)
        {
            if (index < 0) index = 0;
            return index * (pip + spacing) + (index / PipsPerBlock) * blockGap;
        }

        /// <summary>A tap target's size: what the art wants, never below the touch minimum.</summary>
        public static float TapSize(float wanted, float touchMin)
        {
            return Math.Max(wanted, touchMin);
        }

        /// <summary>How far a tap target taller than its row overhangs the row on EACH side.</summary>
        public static float TapOverhang(float rowHeight, float tap)
        {
            return tap > rowHeight ? (tap - rowHeight) * 0.5f : 0f;
        }

        /// <summary>The least spacing between rows so two overhanging hit rects never overlap.</summary>
        public static float MinRowSpacing(float rowHeight, float tap)
        {
            return 2f * TapOverhang(rowHeight, tap);
        }
    }
}
