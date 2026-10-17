#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Emberfall.Game;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// Runs a queue of PlayModeScreenshot targets on a SCRATCH identity with the cloud held off,
    /// then puts everything back and exits.
    ///
    /// PlayModeScreenshot plays on whatever identity the project is configured as, which is the
    /// real one: the real save, and a sign-in to the real cloud account. Most of its targets only
    /// look, but "Result" wins a campaign fight, and a win grants gold, shards and a cleared stage
    /// into the save and syncs it. So this does for PlayModeScreenshot what GuildDungeonShots and
    /// ConflictShots do for themselves: switch PlayerSettings to a scratch company and product (so
    /// PlayerPrefs and persistentDataPath are somewhere else), run, restore, and hash the real save
    /// before and after. A changed hash is logged as a failure. The cloud is held off by
    /// PlayModeScreenshot itself, which enters Play mode through EditorCaptureGuard for every
    /// target, so this class has no cloud switch of its own.
    ///
    /// Each target goes through PlayModeScreenshot's own trigger file, one at a time, so what is
    /// photographed is exactly what that tool photographs, into Tools/Screenshots.
    ///
    ///   Unity -projectPath . -executeMethod Emberfall.EditorTools.ScratchCaptures.Run -captures "Screen:Result;Screen:Toast@2400x1080"
    ///
    /// TESTER RULE: a run captures its targets in batches in one editor session, never one launch
    /// per target. A single launch is only for retrying a target. -captures takes the whole list,
    /// ';' separated, and Tools/capture-batch.ps1 is the command that runs it (it raises the editor
    /// window, checks the real save before and after, watches for a lost play entry and prints one
    /// RESULT line per target). Docs/CAPTURE.md has the how-to.
    ///
    /// WATCHDOG: PlayModeScreenshot recovers a play entry that sends no heartbeat within 90 s, once
    /// per target, and marks the target FAILED when the second entry misses too; this class then
    /// carries on with the next target. EMBERFALL_CAPTURE_WATCHDOG=0 switches it off.
    ///
    /// SuppressFirstRunKey is an EditorPref, which every editor on this machine shares. It is held
    /// for the length of the run and restored to what it was.
    /// </summary>
    [InitializeOnLoad]
    public static class ScratchCaptures
    {
        private const string ActiveKey = "ScratchCaptures.Active";
        private const string QueueKey = "ScratchCaptures.Queue";
        private const string WaitingKey = "ScratchCaptures.Waiting";
        private const string OrigCompanyKey = "ScratchCaptures.OrigCompany";
        private const string OrigProductKey = "ScratchCaptures.OrigProduct";
        private const string OrigSuppressKey = "ScratchCaptures.OrigSuppress";
        private const string LiveSaveKey = "ScratchCaptures.LiveSave";
        private const string LiveHashKey = "ScratchCaptures.LiveHash";
        private const string ExitAtKey = "ScratchCaptures.ExitAt";
        private const string IdleKey = "ScratchCaptures.Idle";

        /// <summary>
        /// Editor ticks with Play mode fully off before the identity is put back. Not zero: when
        /// Play mode ends, isPlayingOrWillChangePlaymode turns false BEFORE Unity has finished
        /// leaving it, and the last thing it does is load the backup it took on the way in -
        /// scene and settings, taken while the scratch identity was on. A restore made in that
        /// window is undone, and Unity then writes the scratch company into ProjectSettings.asset
        /// on its way out. It did, on the first three runs of this tool.
        /// </summary>
        private const int SettleTicks = 90;

        private const string ScratchCompany = "Emberfall Scratch";
        private const string ScratchProduct = "Emberfall Scratch Captures";
        private const string TriggerPath = "Tools/Screenshots/.trigger";

        static ScratchCaptures()
        {
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            string captures = "Screen:Result;Screen:Toast;Screen:ToastLong";
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-captures") captures = args[i + 1];

            string liveSave = Path.Combine(Application.persistentDataPath, SaveService.FileName);
            SessionState.SetString(LiveSaveKey, liveSave);
            SessionState.SetString(LiveHashKey, HashOf(liveSave));
            SessionState.SetString(OrigCompanyKey, PlayerSettings.companyName);
            SessionState.SetString(OrigProductKey, PlayerSettings.productName);
            SessionState.SetBool(OrigSuppressKey, EditorPrefs.GetBool(GameApp.SuppressFirstRunKey, false));

            PlayerSettings.companyName = ScratchCompany;
            PlayerSettings.productName = ScratchProduct;
            EditorPrefs.SetBool(GameApp.SuppressFirstRunKey, true);

            string scratch = Application.persistentDataPath;
            if (scratch.IndexOf(ScratchProduct, StringComparison.OrdinalIgnoreCase) < 0)
            {
                Debug.LogError("ScratchCaptures: persistentDataPath is '" + scratch + "', not the scratch identity. Not running.");
                Restore();
                return;
            }
            Directory.CreateDirectory(scratch);
            string save = Path.Combine(scratch, SaveService.FileName);
            if (File.Exists(save)) File.Delete(save);
            PlayerPrefs.DeleteAll();

            // A relaunched batch (capture-batch.ps1 after killing a hung editor) starts with the target
            // that was lost, and that target has already used its first play entry.
            int firstAttempt;
            if (int.TryParse(Environment.GetEnvironmentVariable(CaptureWatchdog.FirstAttemptVariable), out firstAttempt) && firstAttempt > 1)
            {
                SessionState.SetInt(PlayModeScreenshot.NextAttemptKey, firstAttempt);
                Debug.Log("ScratchCaptures: the first target starts as play entry attempt " + firstAttempt + " (" + CaptureWatchdog.FirstAttemptVariable + ")");
            }
            SessionState.SetString(PlayModeScreenshot.FailedKey, "");

            SessionState.SetString(QueueKey, captures);
            SessionState.SetBool(WaitingKey, false);
            SessionState.SetBool(ActiveKey, true);
            Debug.Log("ScratchCaptures: scratch identity at '" + scratch + "', queue: " + captures);
        }

        private static void Tick()
        {
            // A couple of seconds between the restore and the exit, so nothing the restore set
            // off is still in flight when the editor shuts down.
            int exitIn = SessionState.GetInt(ExitAtKey, 0);
            if (exitIn > 0)
            {
                SessionState.SetInt(ExitAtKey, exitIn - 1);
                if (exitIn == 1)
                {
                    // Checked once more on the way out, because this is what Unity will write.
                    if (PlayerSettings.companyName != SessionState.GetString(OrigCompanyKey, PlayerSettings.companyName))
                    {
                        Debug.LogWarning("ScratchCaptures: the identity had reverted again before exit; restoring once more.");
                        Restore();
                    }
                    EditorApplication.Exit(0);
                }
                return;
            }
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) { SessionState.SetInt(IdleKey, 0); return; }
            int idle = SessionState.GetInt(IdleKey, 0) + 1;
            SessionState.SetInt(IdleKey, idle);
            if (idle < SettleTicks) return;

            string trigger = Path.Combine(Application.dataPath, "../" + TriggerPath);
            if (SessionState.GetBool(WaitingKey, false))
            {
                // Wait for PlayModeScreenshot to take the trigger and finish its run.
                if (File.Exists(trigger) || SessionState.GetBool("PlayModeScreenshot.Active", false)) return;
                SessionState.SetBool(WaitingKey, false);
                string failed = SessionState.GetString(PlayModeScreenshot.FailedKey, "");
                if (failed.Length > 0)
                {
                    SessionState.SetString(PlayModeScreenshot.FailedKey, "");
                    Debug.LogWarning("ScratchCaptures: '" + failed + "' was marked FAILED by the watchdog, carrying on with the next target");
                }
            }

            string queue = SessionState.GetString(QueueKey, "");
            if (queue.Length == 0)
            {
                SessionState.SetBool(ActiveKey, false);
                Restore();
                SessionState.SetInt(ExitAtKey, 120);
                return;
            }
            int cut = queue.IndexOf(';');
            string next = (cut < 0 ? queue : queue.Substring(0, cut)).Trim();
            SessionState.SetString(QueueKey, cut < 0 ? "" : queue.Substring(cut + 1));
            if (next.Length == 0) return;

            Directory.CreateDirectory(Path.GetDirectoryName(trigger));
            File.WriteAllText(trigger, next);
            SessionState.SetBool(WaitingKey, true);
            Debug.Log("ScratchCaptures: triggered '" + next + "'");
        }

        private static void Restore()
        {
            string company = SessionState.GetString(OrigCompanyKey, "");
            string product = SessionState.GetString(OrigProductKey, "");
            if (!string.IsNullOrEmpty(product))
            {
                PlayerSettings.companyName = company;
                PlayerSettings.productName = product;
                // Entering Play mode writes ProjectSettings.asset with the scratch identity on it,
                // and SaveAssets alone does not write it back: the asset is not marked dirty by a
                // PlayerSettings setter. Without this the restore lived only in memory, and the
                // scratch company was what the file held after Exit.
                UnityEngine.Object[] settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
                if (settings.Length > 0) EditorUtility.SetDirty(settings[0]);
                AssetDatabase.SaveAssets();
            }
            EditorPrefs.SetBool(GameApp.SuppressFirstRunKey, SessionState.GetBool(OrigSuppressKey, false));

            string live = SessionState.GetString(LiveSaveKey, "");
            string before = SessionState.GetString(LiveHashKey, "");
            if (HashOf(live) == before) Debug.Log("ScratchCaptures: [ok] the real save is byte for byte what it was before the run");
            else Debug.LogError("ScratchCaptures: [FAIL] the real save CHANGED during the run");
            Debug.Log("ScratchCaptures: identity restored to '" + company + "/" + product + "'");

            // Read back from the FILE, not from PlayerSettings: the in-memory value was right the
            // first time this ran too, and the file was not.
            string file = File.ReadAllText(SettingsPath());
            if (file.Contains("companyName: " + company + "\n") || file.Contains("companyName: " + company + "\r\n"))
                Debug.Log("ScratchCaptures: [ok] ProjectSettings.asset holds the real identity again");
            else
                Debug.LogError("ScratchCaptures: [FAIL] ProjectSettings.asset still holds the scratch identity - restore it with git");
        }

        private static string SettingsPath()
        {
            return Path.Combine(Application.dataPath, "../ProjectSettings/ProjectSettings.asset");
        }

        private static string HashOf(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "absent";
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
    }
}
#endif
