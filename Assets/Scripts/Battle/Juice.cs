using UnityEngine;
using Emberfall.Core.Data;

namespace Emberfall.Battle
{
    /// <summary>
    /// The one switch for the R12 hit-feedback pass. Every new behaviour in the battle view sits
    /// behind Juice.On; with it false the old code runs, line for line.
    ///
    /// Where it comes from: vfx.json "juice" (default on), read once through VfxDirector.Config.
    /// "enabled": false there is the rollback. For one launch in the editor or a development build
    /// EMBERFALL_JUICE=0 forces the rollback and =1 forces the shipped numbers, the same shape as
    /// AudioService.RealAudioEnvVar. A release build reads the file alone.
    ///
    /// The numbers live in JuiceProfile (Core, engine free) and the decisions that read them in
    /// JuiceMath (engine free), so the harness proves both without Unity.
    /// </summary>
    public static class Juice
    {
        public const string EnvVar = "EMBERFALL_JUICE";

        private static JuiceProfile _profile;
        private static JuiceProfile _forced;

        public static JuiceProfile Profile
        {
            get
            {
                if (_forced != null) return _forced;
                if (_profile == null) _profile = Resolve();
                return _profile;
            }
        }

        public static bool On { get { return Profile.On; } }

        /// <summary>Capture seam: true forces the shipped numbers, false forces Legacy, null hands
        /// control back to the file. The BattleMoment capture target uses it for the .off shot and
        /// resets it afterwards, because statics survive a no-domain-reload session.</summary>
        public static void ForceForCapture(bool? on)
        {
            if (on == null) _forced = null;
            else _forced = on.Value ? JuiceProfile.Juiced : JuiceProfile.Legacy;
        }

        /// <summary>Test seam: drop the cached profile so the next read resolves again.</summary>
        public static void Reset()
        {
            _profile = null;
            _forced = null;
        }

        private static JuiceProfile Resolve()
        {
            JuiceProfile p = VfxDirector.Config.Juice;
            if (Application.isEditor || Debug.isDebugBuild)
            {
                string env = System.Environment.GetEnvironmentVariable(EnvVar);
                if (env == "0")
                {
                    Debug.Log("Emberfall juice: " + EnvVar + "=0, the pre-R12 hit feedback plays for this run.");
                    return JuiceProfile.Legacy;
                }
                if (env == "1" && !p.On)
                {
                    Debug.Log("Emberfall juice: " + EnvVar + "=1, the R12 hit feedback plays for this run.");
                    return JuiceProfile.Juiced;
                }
            }
            return p;
        }
    }
}
