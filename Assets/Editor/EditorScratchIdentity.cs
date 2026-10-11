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
    /// Puts an editor capture on a SCRATCH identity and puts the real one back afterwards, so the
    /// real local save is never written by a photograph.
    ///
    /// persistentDataPath and PlayerPrefs both derive from PlayerSettings.companyName and
    /// productName, and GameApp saves on the way out, so any automation that enters Play mode on
    /// the project's own identity writes the real save. The per-tool copies of this swap
    /// (ConflictShots, GuildDungeonShots, ...) each do it for themselves; PlayModeScreenshot did
    /// not, and this is what it uses. Enter is idempotent: when the identity is already a scratch
    /// one (a tool above it swapped first, as ScratchCaptures does) it changes nothing and does not
    /// own the restore.
    ///
    /// The restore is delayed until the editor has sat in edit mode for SettleTicks ticks. When
    /// Play mode ends, isPlayingOrWillChangePlaymode turns false BEFORE Unity has finished leaving
    /// it, and the last thing it does is load the backup it took on the way in, which was taken
    /// with the scratch identity on. A restore made in that window is undone and Unity then writes
    /// the scratch company into ProjectSettings.asset. ScratchCaptures learned this on its first
    /// three runs; the delay and the read-back of the file are the same fix.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorScratchIdentity
    {
        public const string ScratchCompany = "Emberfall Scratch";

        private const string OwnedKey = "EditorScratchIdentity.Owned";
        private const string PendingKey = "EditorScratchIdentity.RestorePending";
        private const string IdleKey = "EditorScratchIdentity.Idle";
        private const string OrigCompanyKey = "EditorScratchIdentity.OrigCompany";
        private const string OrigProductKey = "EditorScratchIdentity.OrigProduct";
        private const string OrigSuppressKey = "EditorScratchIdentity.OrigSuppress";
        private const string LiveSaveKey = "EditorScratchIdentity.LiveSave";
        private const string LiveHashKey = "EditorScratchIdentity.LiveHash";

        private const int SettleTicks = 90;

        static EditorScratchIdentity()
        {
            EditorApplication.update += Tick;
        }

        /// <summary>True when the project is currently on a scratch identity, however it got
        /// there. Every scratch company and product in the editor tools carries "Scratch".</summary>
        public static bool IsScratch
        {
            get
            {
                return Application.persistentDataPath.IndexOf("Scratch", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        /// <summary>
        /// Switches to the scratch identity named by product (unless already on one), clears any
        /// leftover scratch save, and suppresses the first-run card so a capture of a fresh profile
        /// is a capture of the screen and not of a welcome card. Returns false, having restored
        /// whatever it changed, if the path does not resolve under the scratch identity.
        /// </summary>
        public static bool Enter(string product)
        {
            if (SessionState.GetBool(OwnedKey, false))
            {
                // Our own swap from an earlier run in this editor is still in place, waiting out
                // its restore. Keep it: a new run is starting on the same scratch identity.
                SessionState.SetBool(PendingKey, false);
                SessionState.SetInt(IdleKey, 0);
                return IsScratch;
            }
            if (IsScratch) return true;

            string liveSave = Path.Combine(Application.persistentDataPath, SaveService.FileName);
            SessionState.SetString(LiveSaveKey, liveSave);
            SessionState.SetString(LiveHashKey, HashOf(liveSave));
            SessionState.SetString(OrigCompanyKey, PlayerSettings.companyName);
            SessionState.SetString(OrigProductKey, PlayerSettings.productName);
            SessionState.SetBool(OrigSuppressKey, EditorPrefs.GetBool(GameApp.SuppressFirstRunKey, false));
            SessionState.SetBool(OwnedKey, true);
            SessionState.SetBool(PendingKey, false);

            PlayerSettings.companyName = ScratchCompany;
            PlayerSettings.productName = product;
            EditorPrefs.SetBool(GameApp.SuppressFirstRunKey, true);

            string scratch = Application.persistentDataPath;
            if (!IsScratch)
            {
                Debug.LogError("EditorScratchIdentity: persistentDataPath is '" + scratch
                               + "', not the scratch identity. Not running.");
                RestoreNow();
                return false;
            }

            Directory.CreateDirectory(scratch);
            string save = Path.Combine(scratch, SaveService.FileName);
            if (File.Exists(save)) File.Delete(save);
            PlayerPrefs.DeleteAll();
            Debug.Log("EditorScratchIdentity: scratch identity at '" + scratch + "'");
            return true;
        }

        /// <summary>Marks the run finished: the real identity goes back once the editor has
        /// settled in edit mode. A no-op if Enter did not do the swap.</summary>
        public static void LeaveWhenIdle()
        {
            if (!SessionState.GetBool(OwnedKey, false)) return;
            SessionState.SetBool(PendingKey, true);
            SessionState.SetInt(IdleKey, 0);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetInt(IdleKey, 0);
                return;
            }
            int idle = SessionState.GetInt(IdleKey, 0) + 1;
            SessionState.SetInt(IdleKey, idle);
            if (idle >= SettleTicks) RestoreNow();
        }

        private static void RestoreNow()
        {
            SessionState.SetBool(PendingKey, false);
            SessionState.SetBool(OwnedKey, false);

            string company = SessionState.GetString(OrigCompanyKey, "");
            string product = SessionState.GetString(OrigProductKey, "");
            if (!string.IsNullOrEmpty(product))
            {
                PlayerSettings.companyName = company;
                PlayerSettings.productName = product;
                // A PlayerSettings setter does not mark the asset dirty, so without this the
                // restore lives only in memory and ProjectSettings.asset keeps the scratch company.
                UnityEngine.Object[] settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
                if (settings.Length > 0) EditorUtility.SetDirty(settings[0]);
                AssetDatabase.SaveAssets();
            }
            EditorPrefs.SetBool(GameApp.SuppressFirstRunKey, SessionState.GetBool(OrigSuppressKey, false));

            string live = SessionState.GetString(LiveSaveKey, "");
            string before = SessionState.GetString(LiveHashKey, "");
            if (HashOf(live) == before) Debug.Log("EditorScratchIdentity: [ok] the real save is byte for byte what it was before the run");
            else Debug.LogError("EditorScratchIdentity: [FAIL] the real save CHANGED during the run");

            // Read back from the FILE, not from PlayerSettings: the in-memory value can be right
            // while the file is not.
            string file = File.ReadAllText(SettingsPath());
            if (file.Contains("companyName: " + company + "\n") || file.Contains("companyName: " + company + "\r\n"))
                Debug.Log("EditorScratchIdentity: [ok] ProjectSettings.asset holds the real identity again");
            else
                Debug.LogError("EditorScratchIdentity: [FAIL] ProjectSettings.asset still holds the scratch identity - restore it with git");
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
