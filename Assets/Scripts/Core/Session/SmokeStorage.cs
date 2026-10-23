using System.IO;

namespace Emberfall.Core.Session
{
    /// <summary>
    /// WHERE THE SMOKE RUN KEEPS ITS STATE (R27, Docs/FRIENDS-BUILD.md). The smoke test (-emberfallSmoke, only in a
    /// build with EMBERFALL_PLAYLOG) signs in to the cloud and opens screens, so it must never touch the player's real
    /// save, real cloud session or real play log. While Active, every name the save and the cloud session are stored
    /// under is replaced by a smoke one: a scratch-smoke folder inside persistentDataPath for the save file, separate
    /// PlayerPrefs keys for the save mirror and the cloud session, and emberfall_playlog.smoke.txt for the log. The
    /// smoke run therefore starts as a brand new anonymous player with its own session, whatever is on the device.
    ///
    /// Pure and engine-free so the harness proves the mapping. Active is false unless the Game sets it at startup, and
    /// the Game does so only under EMBERFALL_PLAYLOG with the flag on the command line (ShouldActivate).
    /// </summary>
    public static class SmokeStorage
    {
        public const string FolderName = "scratch-smoke";
        public const string SaveKey = "emberfall.smoke.save.v1";
        public const string CloudKey = "emberfall.smoke.cloud.v1";
        public const string PlayLogName = "emberfall_playlog.smoke.txt";
        public const string PlayLogPrevName = "emberfall_playlog.smoke.prev.txt";
        public const string Flag = "-emberfallSmoke";

        public static bool Active;

        /// <summary>Whether this run is a smoke run: the define is compiled in AND the flag is on the command line.</summary>
        public static bool ShouldActivate(bool playLogDefine, string[] args)
        {
            return playLogDefine && PlayLogRules.HasFlag(args, Flag);
        }

        /// <summary>The folder the save file lives in: the real one, or the scratch-smoke folder inside it.</summary>
        public static string SaveDirectory(string persistentDir)
        {
            return Active ? Path.Combine(persistentDir, FolderName) : persistentDir;
        }

        public static string SavePrefsKey(string realKey)
        {
            return Active ? SaveKey : realKey;
        }

        public static string CloudPrefsKey(string realKey)
        {
            return Active ? CloudKey : realKey;
        }

        public static string PlayLogFile
        {
            get { return Active ? PlayLogName : PlayLogRules.FileName; }
        }

        public static string PlayLogPrevFile
        {
            get { return Active ? PlayLogPrevName : PlayLogRules.PrevFileName; }
        }
    }
}
