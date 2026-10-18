using UnityEngine;

namespace Emberfall.Battle
{
    /// <summary>
    /// The player's camera shake setting. A device preference in PlayerPrefs, not a field on the
    /// save, for the reason SummonSettings keeps Fast summon there: it is how this phone plays the
    /// battle, it changes nothing about the account, and the save format does not move for it.
    ///
    /// On by default. BattleStage.Shake and ShakeAtLeast are the single choke point that reads it,
    /// so every caller (the host, the director, the dev arena) obeys it, whichever juice flag state
    /// is running. Inert at its default: with it on the stage shakes exactly as before.
    /// </summary>
    public static class ScreenShakePrefs
    {
        private const string Key = "Emberfall.ScreenShake";

        public static bool Enabled
        {
            get { return PlayerPrefs.GetInt(Key, 1) == 1; }
            set
            {
                PlayerPrefs.SetInt(Key, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
