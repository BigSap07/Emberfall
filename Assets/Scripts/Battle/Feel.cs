using UnityEngine;
using Emberfall.Core.Data;

namespace Emberfall.Battle
{
    /// <summary>
    /// The one switch for the R15 feel pass (hit flash, VFX restraint, one look per event class,
    /// sound restraint). Every new behaviour in the battle view sits behind Feel.V2; with it false
    /// the R12 code runs, line for line.
    ///
    /// Where it comes from: vfx.json "feelPreset" ("v2" shipped, "v1" restores R12, a missing key
    /// reads "v1"). For one launch in the editor or a development build EMBERFALL_FEEL=v1 or v2
    /// forces a preset, the same shape as Juice.EnvVar. A release build reads the file alone.
    ///
    /// V2 also needs Juice.On: the feel pass builds on the R12 pass, so the R12 rollback (juice
    /// off, the pre-R12 battle) takes the feel pass with it.
    ///
    /// The numbers live in FeelProfile (Core, engine free) and the decisions that read them in
    /// FeelMath (engine free), so the harness proves both without Unity.
    /// </summary>
    public static class Feel
    {
        public const string EnvVar = "EMBERFALL_FEEL";

        private static FeelProfile _profile;
        private static FeelProfile _forced;

        public static FeelProfile Profile
        {
            get
            {
                if (_forced != null) return _forced;
                if (_profile == null) _profile = Resolve();
                return _profile;
            }
        }

        /// <summary>True when the v2 feel is in force. What the battle code asks.</summary>
        public static bool V2 { get { return Juice.On && Profile.V2; } }

        /// <summary>Capture seam: true forces the v2 numbers, false forces V1, null hands control
        /// back to the file. The BattleMoment capture target uses it for the .v1 and .v2 shots and
        /// resets it afterwards, because statics survive a no-domain-reload session.</summary>
        public static void ForceForCapture(bool? v2)
        {
            if (v2 == null) _forced = null;
            else _forced = v2.Value ? VfxDirector.Config.FeelV2 : FeelProfile.V1;
        }

        /// <summary>Test seam: drop the cached profile so the next read resolves again.</summary>
        public static void Reset()
        {
            _profile = null;
            _forced = null;
        }

        private static FeelProfile Resolve()
        {
            VfxConfig cfg = VfxDirector.Config;
            if (Application.isEditor || Debug.isDebugBuild)
            {
                string env = System.Environment.GetEnvironmentVariable(EnvVar);
                if (env == FeelProfile.PresetV1)
                {
                    Debug.Log("Emberfall feel: " + EnvVar + "=v1, the R12 hit feedback plays for this run.");
                    return FeelProfile.V1;
                }
                if (env == FeelProfile.PresetV2)
                {
                    Debug.Log("Emberfall feel: " + EnvVar + "=v2, the R15 feel plays for this run.");
                    return cfg.FeelV2;
                }
            }
            return cfg.Feel;
        }
    }
}
