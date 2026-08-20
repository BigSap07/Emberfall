using System;
using System.IO;
using UnityEngine;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Save;
using Emberfall.Core.Session;

namespace Emberfall.Game
{
    /// <summary>
    /// Local save. Writes JSON to persistentDataPath and mirrors it into PlayerPrefs, so the
    /// prototype keeps progress on Android, in the editor, and anywhere file IO is restricted.
    /// </summary>
    public static class SaveService
    {
        public const string FileName = "emberfall_save.json";
        public const string PrefsKey = "emberfall.save.v1";

        public static string FilePath
        {
            get { return Path.Combine(Application.persistentDataPath, FileName); }
        }

        public static void Save(PlayerProfile profile)
        {
            if (profile == null) return;
            profile.LastSaveUnix = TimeUtil.NowUnix();
            string json = SaveSerializer.Serialize(profile);

            try
            {
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Emberfall: file save failed (" + ex.Message + "), falling back to PlayerPrefs.");
            }

            try
            {
                PlayerPrefs.SetString(PrefsKey, json);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Emberfall: PlayerPrefs save failed: " + ex.Message);
            }
        }

        /// <summary>Returns null when there is no usable save, so the caller can start fresh.</summary>
        public static PlayerProfile Load(ContentDatabase content)
        {
            string json = ReadRaw();
            if (string.IsNullOrEmpty(json)) return null;

            string error;
            PlayerProfile profile = SaveSerializer.Deserialize(json, content, out error);
            if (profile == null)
            {
                Debug.LogWarning("Emberfall: save was unreadable (" + error + "), starting a new game.");
                return null;
            }
            if (profile.Heroes.Count == 0)
            {
                Debug.LogWarning("Emberfall: save had no valid heroes, starting a new game.");
                return null;
            }
            return profile;
        }

        private static string ReadRaw()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string text = File.ReadAllText(FilePath);
                    if (!string.IsNullOrEmpty(text)) return text;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Emberfall: could not read save file: " + ex.Message);
            }

            try
            {
                if (PlayerPrefs.HasKey(PrefsKey)) return PlayerPrefs.GetString(PrefsKey, null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Emberfall: could not read PlayerPrefs save: " + ex.Message);
            }
            return null;
        }

        public static void DeleteSave()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Emberfall: could not delete save file: " + ex.Message);
            }
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }
    }
}
