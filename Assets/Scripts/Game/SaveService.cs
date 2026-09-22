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

        /// <summary>
        /// The save document exactly as it sits on disk, or null when there is none.
        ///
        /// The cloud uploader reads THIS rather than re-serialising the live profile, and the
        /// difference matters more than it looks. Save stamps LastSaveUnix on the way past, so two
        /// serialisations of one profile are two different documents - and the whole conflict rule
        /// is built on a base revision meaning "the cloud holds exactly what this device last
        /// agreed with". A copy that differed from the file by one timestamp would make that
        /// sentence quietly false. The caller saves first, then uploads what was written.
        /// </summary>
        public static string ReadDocument()
        {
            return ReadRaw();
        }

        /// <summary>
        /// Replaces the local save with a document from somewhere else - the one call the cloud
        /// download path needs, and the only writer here that does not go through a PlayerProfile.
        ///
        /// Written to both homes, the way Save writes to both, because a half-adopted save would
        /// leave the file and the PlayerPrefs mirror disagreeing and the next launch picking
        /// whichever it found first. Returns false if NEITHER landed, which is the only case the
        /// caller can act on: it must not then swap the running session, because the session would
        /// be the cloud's and the disk would still be this device's.
        /// </summary>
        public static bool WriteDocument(string json)
        {
            if (string.IsNullOrEmpty(json)) return false;
            bool anyLanded = false;

            try
            {
                File.WriteAllText(FilePath, json);
                anyLanded = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Emberfall: could not write the downloaded save to disk (" + ex.Message + ").");
            }

            try
            {
                PlayerPrefs.SetString(PrefsKey, json);
                PlayerPrefs.Save();
                anyLanded = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Emberfall: could not mirror the downloaded save to PlayerPrefs: " + ex.Message);
            }

            return anyLanded;
        }

        /// <summary>
        /// Returns null when there is no usable save, so the caller can start fresh.
        ///
        /// TWO COPIES, AND BOTH ARE READ. Save writes the file and then the PlayerPrefs mirror, so
        /// a process killed inside File.WriteAllText leaves a partial file beside an intact mirror.
        /// This used to read the file alone, find it unreadable and report "no save", and GameApp
        /// then started a new game and wrote it over both. The mirror is now tried whenever the
        /// file does not load. See Docs/CLOUD-GAPS.md.
        ///
        /// AN UNUSABLE SAVE IS COPIED ASIDE BEFORE NULL IS RETURNED, because null is followed at
        /// boot by a fresh profile saved over the live file. A save from a newer build, or one
        /// whose every hero has left content, is refused rather than read - correctly - but the
        /// refusal used to be followed by its destruction. The copy is the exact text, beside the
        /// save, and nothing reads it: it exists so the progress still exists.
        /// </summary>
        public static PlayerProfile Load(ContentDatabase content)
        {
            string fromFile = ReadFile();
            string fromPrefs = ReadPrefs();
            if (string.IsNullOrEmpty(fromFile) && string.IsNullOrEmpty(fromPrefs)) return null;

            string fileProblem;
            PlayerProfile profile = TryLoad(fromFile, content, out fileProblem);
            if (profile != null) return profile;

            string prefsProblem = "same document as the file";
            if (fromPrefs != fromFile)
            {
                profile = TryLoad(fromPrefs, content, out prefsProblem);
                if (profile != null)
                {
                    Debug.LogWarning("Emberfall: the save file was unusable (" + fileProblem
                        + "), so the PlayerPrefs copy was loaded instead.");
                    return profile;
                }
            }

            KeepAside(fromFile);
            if (fromPrefs != fromFile) KeepAside(fromPrefs);
            Debug.LogWarning("Emberfall: no usable save (file: " + fileProblem + "; mirror: " + prefsProblem
                + "), starting a new game. The unusable copy was kept beside the save.");
            return null;
        }

        /// <summary>One candidate document through the deserialiser, with the reason it failed.</summary>
        private static PlayerProfile TryLoad(string json, ContentDatabase content, out string problem)
        {
            problem = "";
            if (string.IsNullOrEmpty(json)) { problem = "absent"; return null; }

            string error;
            PlayerProfile profile = SaveSerializer.Deserialize(json, content, out error);
            if (profile == null) { problem = "unreadable: " + error; return null; }
            if (profile.Heroes.Count == 0) { problem = "no valid heroes"; return null; }
            return profile;
        }

        /// <summary>Writes an unusable document beside the save under a name nothing loads. Best
        /// effort: a failure here is logged and must never stop the game booting.</summary>
        private static void KeepAside(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                string name = "emberfall_save.unreadable-" + TimeUtil.NowUnix() + "-"
                            + ((uint)json.GetHashCode()).ToString("x8") + ".json";
                string path = Path.Combine(Application.persistentDataPath, name);
                if (!File.Exists(path)) File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Emberfall: could not keep a copy of the unusable save: " + ex.Message);
            }
        }

        private static string ReadRaw()
        {
            string text = ReadFile();
            return !string.IsNullOrEmpty(text) ? text : ReadPrefs();
        }

        private static string ReadFile()
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
            return null;
        }

        private static string ReadPrefs()
        {
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
