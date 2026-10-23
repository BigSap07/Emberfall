using System;

namespace Emberfall.Core.Progression
{
    /// <summary>
    /// WHEN A PANEL CARRIES THE KIT'S FILIGREE CORNER, and how large (R26 round 2). Pure so the harness can pin it.
    /// A corner sized for a big panel cut the text of small tiles and thin rows, so the corner now scales with the
    /// panel and a panel too small for it carries none (its own border is its ornament).
    /// </summary>
    public static class KitOrnamentRule
    {
        public const float MaxSize = 48f;
        public const float Share = 0.30f;

        /// <summary>The shortest side a panel needs to carry a filigree corner at all. Under it, none: tiles, quest
        /// rows, hero cards and chips keep their plain border.</summary>
        public const float MinSide = 180f;

        /// <summary>The corner's size for a panel of this width and height, or 0 for none.</summary>
        public static float SizeFor(float width, float height)
        {
            float side = Math.Min(width, height);
            if (side < MinSide) return 0f;
            return Math.Min(MaxSize, Share * side);
        }
    }
}
