using System;

namespace Emberfall.Core.Session
{
    /// <summary>
    /// THE FIVE POINT STAR (R57), as plain geometry so the harness can prove its shape without an engine.
    /// Hero tier is stars, and the old sprite drew a rounded pentagon: its distance field was the
    /// regular-star formula with m = 2, and m = 2 is a pentagon (the inner corners sit at 0.81 of the tip
    /// radius, where a star has them near 0.4). This is an explicit ten corner polygon instead, tips up,
    /// centred on its bounding box in the -1..1 square that TextureLab.Star draws.
    ///
    /// Distance is signed: negative inside, positive outside, in the same units as the square. Nothing
    /// here is a game rule and nothing reads a save.
    /// </summary>
    public static class StarShape
    {
        public const int Points = 5;

        /// <summary>Tip radius. 1.03 fills the square once the shape is centred (about 0.94 up and down, 0.98 across).</summary>
        public const float OuterRadius = 1.03f;

        /// <summary>Inner corner radius over tip radius. A pentagram is 0.38; 0.45 stays a star but keeps the arms thick enough for 16 px.</summary>
        public const float InnerRatio = 0.45f;

        /// <summary>Width of the dark outline, measured inward from the edge. 0.12 is one pixel at 16 px.</summary>
        public const float RimWidth = 0.12f;

        /// <summary>Where the circle through the tips is centred once the shape is centred on its bounding box (below zero).</summary>
        public static readonly float CentreY;

        private static readonly float[] VX = new float[Points * 2];
        private static readonly float[] VY = new float[Points * 2];

        static StarShape()
        {
            // Tips at 90 degrees plus k * 72, inner corners halfway between. Then shift down so the
            // bounding box, not the centre of the circle, sits on the middle of the square.
            double step = Math.PI / Points;
            for (int k = 0; k < Points * 2; k++)
            {
                double angle = Math.PI * 0.5 + k * step;
                double radius = (k % 2 == 0) ? OuterRadius : OuterRadius * InnerRatio;
                VX[k] = (float)(Math.Cos(angle) * radius);
                VY[k] = (float)(Math.Sin(angle) * radius);
            }
            float top = float.MinValue, bottom = float.MaxValue;
            for (int k = 0; k < VY.Length; k++)
            {
                if (VY[k] > top) top = VY[k];
                if (VY[k] < bottom) bottom = VY[k];
            }
            float shift = (top + bottom) * 0.5f;
            for (int k = 0; k < VY.Length; k++) VY[k] -= shift;
            CentreY = -shift;
        }

        public static int CornerCount { get { return VX.Length; } }
        public static float CornerX(int k) { return VX[k]; }
        public static float CornerY(int k) { return VY[k]; }

        /// <summary>Signed distance from (x, y) to the star outline: below zero inside.</summary>
        public static float Distance(float x, float y)
        {
            int n = VX.Length;
            float dx = x - VX[0], dy = y - VY[0];
            float best = dx * dx + dy * dy;
            float sign = 1f;
            for (int i = 0, j = n - 1; i < n; j = i, i++)
            {
                float ex = VX[j] - VX[i], ey = VY[j] - VY[i];
                float wx = x - VX[i], wy = y - VY[i];
                float t = (wx * ex + wy * ey) / (ex * ex + ey * ey);
                if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
                float bx = wx - ex * t, by = wy - ey * t;
                float d = bx * bx + by * by;
                if (d < best) best = d;

                bool a = y >= VY[i];
                bool b = y < VY[j];
                bool c = ex * wy > ey * wx;
                if ((a && b && c) || (!a && !b && !c)) sign = -sign;
            }
            return sign * (float)Math.Sqrt(best);
        }

        /// <summary>
        /// Radius at which the outline crosses the ray from the tip circle's centre at <paramref name="degrees"/>
        /// (0 is straight up). The harness uses it to compare a tip against an inner corner.
        /// </summary>
        public static float EdgeRadius(float degrees)
        {
            double rad = (degrees + 90.0) * Math.PI / 180.0;
            float cx = (float)Math.Cos(rad), cy = (float)Math.Sin(rad);
            float lo = 0f, hi = 1.5f;
            for (int i = 0; i < 40; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Distance(cx * mid, CentreY + cy * mid) < 0f) lo = mid; else hi = mid;
            }
            return lo;
        }
    }
}
