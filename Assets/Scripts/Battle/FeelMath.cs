using System;
using Emberfall.Core.Data;

namespace Emberfall.Battle
{
    /// <summary>
    /// The decisions of the R15 feel pass, as pure functions of a FeelProfile. Free of UnityEngine
    /// on purpose: Tools/DevHarness compiles this file, so the switch, the rollback and the numbers
    /// are proven by build-check without the editor (FeelChecks.cs).
    ///
    /// Every function given FeelProfile.V1 returns the answer the R12 battle gave, so the battle code
    /// can ask these when Feel.V2 and the harness can pin both sides.
    /// </summary>
    public static class FeelMath
    {
        // ---------------------------------------------------------------- hit flash

        /// <summary>The lerp toward white a hit sets: a tint, not a fill.</summary>
        public static float FlashPeak(FeelProfile p, bool crit)
        {
            return crit ? p.FlashPeakCrit : p.FlashPeakNormal;
        }

        /// <summary>Flash lerp lost per second so that a hit's flash, whatever its peak, reaches zero
        /// in exactly FlashSeconds. Hit stop does not pause it (UnitActor decays it before the
        /// freeze test), so the flash is never held past this.</summary>
        public static float FlashDecayRate(FeelProfile p, bool crit)
        {
            float seconds = Math.Max(0.001f, p.FlashSeconds);
            return FlashPeak(p, crit) / seconds;
        }

        /// <summary>The flash lerp t seconds after a hit, run through the same arithmetic the actor
        /// runs frame by frame: a straight fall from the peak to zero.</summary>
        public static float FlashAt(FeelProfile p, bool crit, float t)
        {
            float v = FlashPeak(p, crit) - FlashDecayRate(p, crit) * Math.Max(0f, t);
            return v < 0f ? 0f : v;
        }
    }
}
