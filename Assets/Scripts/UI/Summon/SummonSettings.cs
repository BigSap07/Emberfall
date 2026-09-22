using UnityEngine;

namespace Emberfall.UI.Summon
{
    /// <summary>
    /// The player's Fast summon setting. A device preference in PlayerPrefs, not a field on the
    /// save: it is how this phone plays the animation, it changes nothing about the account, and
    /// keeping it off the save means the save format did not move for a presentation switch.
    /// </summary>
    public static class SummonSettings
    {
        private const string FastKey = "Emberfall.FastSummon";

        public static bool FastSummon
        {
            get { return PlayerPrefs.GetInt(FastKey, 0) == 1; }
            set
            {
                PlayerPrefs.SetInt(FastKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static SummonTiming Current
        {
            get { return FastSummon ? SummonTiming.Fast : SummonTiming.Full; }
        }
    }
}
