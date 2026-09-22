#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Emberfall.Game;
using Emberfall.UI;
using Emberfall.UI.Screens;
using Emberfall.UI.Summon;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// PHOTOGRAPHS EVERY BEAT OF THE SUMMON, for a 3-star, a 4-star and a 5-star, plus a ten-pull,
    /// at 1920x1080 and 2400x1080.
    ///
    /// A summon is 2.5 seconds of motion and a capture that tried to catch a beat by timing would
    /// photograph whatever frame the editor happened to render. So this does not time anything:
    /// SummonSequence poses every frame from (pull, phase, time) alone, and this freezes it at the
    /// exact instant of each beat and photographs that. Two runs produce the same pictures.
    ///
    /// The heroes come from SummonScreen.DebugPreview, which rolls nothing and grants nothing,
    /// because the live weights make every real pull a 5-star and the other two tiers could not
    /// otherwise be shown. The EDITOR PREVIEW line in the corner of every shot is that path saying
    /// so, and it is left in rather than hidden for the capture.
    ///
    /// Runs under a scratch company/product so the quit-time save cannot touch a real save - the
    /// ArmorVerification pattern, as ClipWiringShots uses it - with the first-run prologue
    /// suppressed. Output goes to Docs/ui-shots/summon/, which is gitignored.
    ///
    ///   Unity.exe -projectPath . -executeMethod Emberfall.EditorTools.SummonSequenceShots.Run
    ///
    /// NOT -batchMode: ScreenCapture photographs the Game view, and batch mode has none. Launched
    /// this way it quits the editor when done. From the menu it leaves the editor open.
    /// </summary>
    [InitializeOnLoad]
    public static class SummonSequenceShots
    {
        private const string ActiveKey = "SummonSequenceShots.Active";
        private const string RestoreKey = "SummonSequenceShots.Restore";
        private const string ExitKey = "SummonSequenceShots.ExitWhenDone";
        private const string OrigCompanyKey = "SummonSequenceShots.OrigCompany";
        private const string OrigProductKey = "SummonSequenceShots.OrigProduct";
        private const string OrigSuppressKey = "SummonSequenceShots.OrigSuppress";

        private const string ScratchCompany = "Emberfall Scratch";
        private const string ScratchProduct = "Emberfall Summon Shots";

        private static readonly int[][] Sizes = { new[] { 1920, 1080 }, new[] { 2400, 1080 } };

        private struct Shot
        {
            public string Name;
            public int[] Tiers;
            public int Index;
            public SummonSequence.Phase Phase;
            public float Time;
            public bool FirstSizeOnly;
        }

        private static List<Shot> _shots;
        private static int _sizeIndex;
        private static int _shotIndex;
        private static int _wait;
        private static int _stage;
        private static string _pending;
        private static int[] _playing;

        static SummonSequenceShots()
        {
            EditorApplication.update += Pump;
            EditorApplication.update += PumpRestore;
        }

        public static void Run()
        {
            SessionState.SetBool(ExitKey, true);
            Begin();
        }

        [MenuItem("Emberfall/Capture/Summon sequence beats", false, 81)]
        public static void RunFromMenu()
        {
            SessionState.SetBool(ExitKey, false);
            Begin();
        }

        private static void Begin()
        {
            SessionState.SetString(OrigCompanyKey, PlayerSettings.companyName);
            SessionState.SetString(OrigProductKey, PlayerSettings.productName);
            SessionState.SetBool(OrigSuppressKey, EditorPrefs.GetBool(GameApp.SuppressFirstRunKey, false));

            PlayerSettings.companyName = ScratchCompany;
            PlayerSettings.productName = ScratchProduct;
            EditorPrefs.SetBool(GameApp.SuppressFirstRunKey, true);

            string dataPath = Application.persistentDataPath;
            if (dataPath.IndexOf(ScratchProduct, StringComparison.OrdinalIgnoreCase) < 0)
            {
                Debug.LogError("SummonSequenceShots: persistentDataPath is '" + dataPath
                               + "', not the scratch identity. Refusing to enter Play Mode.");
                SessionState.SetBool(RestoreKey, true);
                return;
            }
            string save = Path.Combine(dataPath, "emberfall_save.json");
            if (File.Exists(save)) File.Delete(save);
            PlayerPrefs.DeleteAll();

            string outDir = OutDir();
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            if (!GameViewSize.Apply(Sizes[0][0], Sizes[0][1]))
                Debug.LogWarning("SummonSequenceShots: could not pin the Game view size.");

            Debug.Log("SummonSequenceShots: capturing into " + outDir);
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.isPlaying = true;
        }

        private static string OutDir()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/ui-shots/summon"));
        }

        // ------------------------------------------------------------------ the shot list

        /// <summary>Every beat of one pull, at the Full timings. Times are read off SummonTiming
        /// rather than written here, so retuning a beat moves its photograph with it.</summary>
        private static void AddBeats(List<Shot> shots, string tier, int stars)
        {
            SummonTiming t = SummonTiming.Full;
            int[] tiers = { stars };
            Add(shots, tier + "_01_press", tiers, 0, SummonSequence.Phase.Press, t.Dim * 0.55f);
            Add(shots, tier + "_02_buildup", tiers, 0, SummonSequence.Phase.Buildup, t.Tell * 0.6f);
            Add(shots, tier + "_03_tell", tiers, 0, SummonSequence.Phase.Buildup, t.Tell + 0.06f);
            Add(shots, tier + "_04_tell-ring", tiers, 0, SummonSequence.Phase.Buildup, t.Tell + 0.24f);
            Add(shots, tier + "_05_charge", tiers, 0, SummonSequence.Phase.Buildup, t.BuildupFor(stars) - 0.05f);
            Add(shots, tier + "_06_flash", tiers, 0, SummonSequence.Phase.Flash, t.FlashIn * 0.7f);
            Add(shots, tier + "_07_reveal-in", tiers, 0, SummonSequence.Phase.Reveal, 0.1f);
            Add(shots, tier + "_08_stamp", tiers, 0, SummonSequence.Phase.Reveal, t.StarsAt + t.StarGap * 2f + 0.03f);
            Add(shots, tier + "_09_settled", tiers, 0, SummonSequence.Phase.Reveal, t.Settled + 0.5f);
        }

        private static List<Shot> BuildShots()
        {
            List<Shot> shots = new List<Shot>();
            AddBeats(shots, "3star", 3);
            AddBeats(shots, "4star", 4);
            AddBeats(shots, "5star", 5);

            SummonTiming t = SummonTiming.Full;
            int[] batch = DebugCheatPanel.PreviewBatch;
            int gold = Array.IndexOf(batch, 5);
            Add(shots, "x10_01_first-reveal", batch, 0, SummonSequence.Phase.Reveal, t.Settled + 0.5f);
            Add(shots, "x10_02_mini-tell", batch, 1, SummonSequence.Phase.Buildup, 0.12f);
            Add(shots, "x10_03_mini-tell-gold", batch, gold, SummonSequence.Phase.Buildup, 0.14f);
            Add(shots, "x10_04_gold-reveal", batch, gold, SummonSequence.Phase.Reveal, t.Settled + 0.5f);
            Add(shots, "x10_05_summary", batch, 0, SummonSequence.Phase.Summary, 1f);

            // The timing strip: a whole 5-star pull every 0.1s, 16:9 only. Not a beat picture;
            // it is the pacing laid out flat, for judging whether the wait ramps and where it drags.
            int[] five = { 5 };
            float total = t.Dim + t.BuildupFor(5) + t.FlashIn + t.Settled + 0.3f;
            for (int i = 0; i * 0.1f <= total; i++)
            {
                float at = i * 0.1f;
                SummonSequence.Phase phase;
                float local;
                if (at < t.Dim) { phase = SummonSequence.Phase.Press; local = at; }
                else if (at < t.Dim + t.BuildupFor(5)) { phase = SummonSequence.Phase.Buildup; local = at - t.Dim; }
                else if (at < t.Dim + t.BuildupFor(5) + t.FlashIn) { phase = SummonSequence.Phase.Flash; local = at - t.Dim - t.BuildupFor(5); }
                else { phase = SummonSequence.Phase.Reveal; local = at - t.Dim - t.BuildupFor(5) - t.FlashIn; }
                Add(shots, "strip/5star_t" + (i * 100).ToString("0000"), five, 0, phase, local);
                Shot last = shots[shots.Count - 1];
                last.FirstSizeOnly = true;
                shots[shots.Count - 1] = last;
            }
            return shots;
        }

        private static void Add(List<Shot> shots, string name, int[] tiers, int index,
                                SummonSequence.Phase phase, float time)
        {
            Shot s = new Shot();
            s.Name = name;
            s.Tiers = tiers;
            s.Index = index;
            s.Phase = phase;
            s.Time = time;
            shots.Add(s);
        }

        // ------------------------------------------------------------------ the run

        private static void Pump()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (!EditorApplication.isPlaying) return;

            GameApp app = GameApp.Instance;
            ForceGameViewRepaint();

            if (_stage == 0)
            {
                if (app == null || !GameApp.BootCompleted) return;
                Application.runInBackground = true;
                _shots = BuildShots();
                _sizeIndex = 0;
                _shotIndex = 0;
                _playing = null;
                _pending = null;
                app.Router.Show(ScreenId.Summon);
                _wait = 30;
                _stage = 1;
                return;
            }

            if (_wait > 0) { _wait--; return; }

            if (_stage == 1)
            {
                // A requested PNG is written asynchronously; wait for it before moving the pose.
                if (_pending != null)
                {
                    if (!File.Exists(_pending)) return;
                    _pending = null;
                }

                if (_shotIndex >= _shots.Count)
                {
                    _shotIndex = 0;
                    _sizeIndex++;
                    SummonSequence seq = app.Router.Summon.DebugSequence;
                    if (seq != null) seq.Abort();
                    _playing = null;
                    if (_sizeIndex >= Sizes.Length) { _stage = 2; return; }
                    GameViewSize.Apply(Sizes[_sizeIndex][0], Sizes[_sizeIndex][1]);
                    // A new aspect rebuilds nothing, but the canvas scaler needs frames to settle.
                    _wait = 30;
                    return;
                }

                Shot shot = _shots[_shotIndex];
                if (shot.FirstSizeOnly && _sizeIndex > 0) { _shotIndex++; return; }
                SummonScreen summon = app.Router.Summon;
                if (_playing != shot.Tiers)
                {
                    summon.DebugPreview(shot.Tiers);
                    _playing = shot.Tiers;
                }
                summon.DebugSequence.FreezeAt(shot.Index, shot.Phase, shot.Time);
                _stage = 3;
                _wait = 4;
                return;
            }

            if (_stage == 3)
            {
                Shot shot = _shots[_shotIndex];
                string file = "summon_" + shot.Name + "@" + Sizes[_sizeIndex][0] + "x" + Sizes[_sizeIndex][1] + ".png";
                _pending = Path.Combine(OutDir(), file);
                Directory.CreateDirectory(Path.GetDirectoryName(_pending));
                ScreenCapture.CaptureScreenshot(_pending, 1);
                Debug.Log("SummonSequenceShots: requested " + file);
                _shotIndex++;
                _stage = 1;
                _wait = 2;
                return;
            }

            if (_stage == 2)
            {
                SessionState.SetBool(ActiveKey, false);
                SessionState.SetBool(RestoreKey, true);
                Debug.Log("SummonSequenceShots: done");
                EditorApplication.isPlaying = false;
            }
        }

        /// <summary>See PlayModeScreenshot.ForceGameViewRepaint: ScreenCapture hands back what the
        /// Game view last rendered, and a Game view nothing asks to repaint has nothing new.</summary>
        private static void ForceGameViewRepaint()
        {
            Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null) return;
            EditorWindow window = EditorWindow.GetWindow(gameViewType, false, null, false);
            if (window == null) return;
            window.Repaint();
        }

        private static void PumpRestore()
        {
            if (!SessionState.GetBool(RestoreKey, false)) return;
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(RestoreKey, false);

            string company = SessionState.GetString(OrigCompanyKey, "");
            string product = SessionState.GetString(OrigProductKey, "");
            if (!string.IsNullOrEmpty(product))
            {
                PlayerSettings.companyName = company;
                PlayerSettings.productName = product;
                AssetDatabase.SaveAssets();
            }
            EditorPrefs.SetBool(GameApp.SuppressFirstRunKey, SessionState.GetBool(OrigSuppressKey, false));
            Debug.Log("SummonSequenceShots: identity restored to '" + company + "/" + product + "'");
            if (SessionState.GetBool(ExitKey, false)) EditorApplication.Exit(0);
        }
    }
}
#endif
