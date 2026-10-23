using System;

namespace Emberfall.Core.Progression
{
    /// <summary>
    /// HOW THICK THE KIT'S SMALL FRAME DRAWS, and how far content must sit from it (R26 round 3). The small panel
    /// (panel_small) is nine-sliced with a border of 61 by 43 pixels at 234.615 pixels per unit, so it draws 26 units
    /// wide and 18 units tall on a tile 108 units high: its rim ate the labels at the edge of reward tiles and quest
    /// rows. Small panels now draw it at SmallPanelMultiplier times the pixels per unit (thinner), and content that
    /// hugs an edge keeps ContentPad from it. Pure, so the harness pins the numbers; the UI reads them.
    /// </summary>
    public static class KitFrameRule
    {
        public const int SmallBorderX = 61;
        public const int SmallBorderY = 43;
        public const float SmallPpu = 234.615f;

        /// <summary>Image.pixelsPerUnitMultiplier for a small kit panel: 2 draws the frame at half thickness.</summary>
        public const float SmallPanelMultiplier = 2f;

        /// <summary>The thickness, in canvas units, a border of this many pixels draws at.</summary>
        public static float Drawn(int borderPixels, float ppu, float multiplier)
        {
            return borderPixels * 100f / (ppu * multiplier);
        }

        public static float SmallDrawnX { get { return Drawn(SmallBorderX, SmallPpu, SmallPanelMultiplier); } }
        public static float SmallDrawnY { get { return Drawn(SmallBorderY, SmallPpu, SmallPanelMultiplier); } }

        /// <summary>The gap content keeps from an edge: what it asked for, or the frame's drawn thickness plus two
        /// units of air, whichever is more. 0 frame (kit off) leaves what it asked for.</summary>
        public static float ContentPad(float wanted, float drawnFrame)
        {
            if (drawnFrame <= 0f) return wanted;
            return Math.Max(wanted, drawnFrame + 2f);
        }
    }
}
