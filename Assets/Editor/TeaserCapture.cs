#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;
using Emberfall.Dev;
using Emberfall.Game;
using Emberfall.UI;
using Emberfall.UI.Screens;
using Emberfall.UI.Summon;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// SHOOTS THE TEASER'S RAW FOOTAGE, REPRODUCIBLY. Every take is a Unity Recorder movie driven
    /// from here rather than from the Recorder window, so running this again produces the same
    /// frames. Docs/TEASER-CUT.md is the edit built on top of these takes.
    ///
    ///   Unity.exe -projectPath . -executeMethod Emberfall.EditorTools.TeaserCapture.Run
    ///
    /// NOT -batchMode: the Recorder reads the Game view, and batch mode has none. Launched this
    /// way it quits the editor when done.
    ///
    /// The takes are shot long - two to three times what the cut uses - so the edit has handles.
    /// The summon is shot at 1/8 speed and the battle at 1/4, both natively: the summon by
    /// stepping its (phase, time) clock 1/480 s per frame, the battle by Time.timeScale, which
    /// every battle routine already scales by. A speed ramp in the edit is then frame SELECTION,
    /// never frame interpolation.
    ///
    /// What is on screen during a take is TeaserDirector's job; this owns the Recorder, the take
    /// list and the scratch identity. Runs under a scratch company/product so the quit-time save
    /// cannot touch a real save - the SummonSequenceShots pattern. Output goes to Media/captures,
    /// which is gitignored.
    /// </summary>
    [InitializeOnLoad]
    public static class TeaserCapture
    {
        private const string ActiveKey = "TeaserCapture.Active";
        private const string RestoreKey = "TeaserCapture.Restore";
        private const string ExitKey = "TeaserCapture.ExitWhenDone";
        private const string OnlyKey = "TeaserCapture.Only";
        private const string OrigCompanyKey = "TeaserCapture.OrigCompany";
        private const string OrigProductKey = "TeaserCapture.OrigProduct";
        private const string OrigSuppressKey = "TeaserCapture.OrigSuppress";

        private const string ScratchCompany = "Emberfall Scratch";
        private const string ScratchProduct = "Emberfall Teaser Capture";

        public const int Width = 2560;
        public const int Height = 1440;
        public const float Fps = 60f;
        /// <summary>Mbps. Roughly 1.1 bits per pixel per frame at 1440p60: well past visually
        /// lossless for flat cel art, and the edit re-encodes from it once.</summary>
        public const float BitrateMbps = 250f;

        /// <summary>The fight. Pinned so the combat take is the same fight every run.</summary>
        public const int BattleSeed = 51807;
        public const string BattleEnvironment = "ember";

        /// <summary>The revealed hero, and the line she leads. Every one of these has painted
        /// battle sheets, so no unit in the fight falls back to a placeholder.</summary>
        public const string RevealHero = "seraphine_the_closed_volume";
        private static readonly string[] PlayerLine =
            { "seraphine_the_closed_volume", "kael_the_unyielding", "hale_the_relentless_hunter",
              "marrow_the_irrevocable_vow", "kiva_the_forsaken_oath" };
        private static readonly string[] EnemyLine =
            { "vaelis_the_unfixed_blade", "caldus_the_appointed_guard", "sylven_the_unfading_blade",
              "velis_the_hollow_vow", "altheon_the_first_witness" };

        /// <summary>The preview batch: five 5-stars, content order, so pull n is the nth drawn
        /// 5-star. The reveal index is looked up by id, never assumed.</summary>
        private static readonly int[] Batch = { 5, 5, 5, 5, 5 };

        private sealed class TakeSpec
        {
            public string Name;
            public bool Battle;
            public float Rate;        // summon: sequence seconds per frame
            public float Start;       // summon: sequence time at frame 0
            public bool ShowName = true;
            public float Slow = 1f;   // battle: Time.timeScale
            public float CamSize;     // battle: 0 = the game's own camera
            public Vector3 CamTarget;
            public int Frames;
        }

        private static List<TakeSpec> BuildTakes()
        {
            SummonTiming t = SummonTiming.Full;
            float revealAt = t.Dim + t.BuildupFor(5) + t.FlashIn;
            List<TakeSpec> takes = new List<TakeSpec>();

            // The whole summon from the press to 1.2 s into the reveal, at 1/8 speed. The opening
            // and the summon shot are both cut from this one take.
            float slowSpan = revealAt + 1.2f + 0.05f;
            takes.Add(new TakeSpec { Name = "summon_slow8", Rate = 1f / 480f, Start = -0.05f,
                                     Frames = Mathf.CeilToInt(slowSpan * 480f) });
            // The approach alone, from the press to just past the tell, at 1/16: the opening and
            // the first half of the summon run longer in the cut than 1/8 can fill without
            // repeating frames.
            float tellSpan = t.Dim + t.Tell + 0.30f + 0.05f;
            takes.Add(new TakeSpec { Name = "summon_slow16", Rate = 1f / 960f, Start = -0.05f,
                                     Frames = Mathf.CeilToInt(tellSpan * 960f) });
            // The reveal at real time, held 16 s: the long hold is cut from here.
            takes.Add(new TakeSpec { Name = "reveal_name", Rate = 1f / 60f, Start = revealAt - 0.10f,
                                     Frames = Mathf.CeilToInt(16.1f * 60f) });
            takes.Add(new TakeSpec { Name = "reveal_clean", Rate = 1f / 60f, Start = revealAt - 0.10f,
                                     ShowName = false, Frames = Mathf.CeilToInt(16.1f * 60f) });
            // The reveal again at 1/2, for a slower settle if the cut wants one.
            takes.Add(new TakeSpec { Name = "reveal_slow2", Rate = 1f / 120f, Start = revealAt - 0.10f,
                                     Frames = Mathf.CeilToInt(8.1f * 120f) });
            // The summon at real time, as reference for what the player sees.
            takes.Add(new TakeSpec { Name = "summon_rt", Rate = 1f / 60f, Start = -0.05f,
                                     Frames = Mathf.CeilToInt((revealAt + 3f) * 60f) });
            // Thirty seconds of the fight at 1/4 speed: two minutes of footage.
            takes.Add(new TakeSpec { Name = "battle_slow4", Battle = true, Slow = 0.25f,
                                     Frames = Mathf.CeilToInt(30f / 0.25f * 60f) });
            // The same fight - same seed - framed tight on the front ranks: Seraphine (ally slot
            // 0, world x -1.4, z -3.25) and Vaelis opposite her (enemy slot 0, x 1.4, z -3.25).
            // Shot native rather than cropped from the wide take, so the close-up is not an
            // upscale. 2.1 against the game's 3.65 is 1.74x tighter.
            takes.Add(new TakeSpec { Name = "battle_close_slow4", Battle = true, Slow = 0.25f,
                                     CamSize = 2.1f, CamTarget = new Vector3(0f, 0.9f, -3.25f),
                                     Frames = Mathf.CeilToInt(26f / 0.25f * 60f) });
            return takes;
        }

        // ================================================================== entry

        static TeaserCapture()
        {
            EditorApplication.update += Pump;
            EditorApplication.update += PumpRestore;
        }

        public static void Run()
        {
            SessionState.SetBool(ExitKey, true);
            SessionState.SetString(OnlyKey, ParseOnly());
            Begin();
        }

        [MenuItem("Emberfall/Capture/Teaser takes", false, 90)]
        public static void RunFromMenu()
        {
            SessionState.SetBool(ExitKey, false);
            SessionState.SetString(OnlyKey, "");
            Begin();
        }

        /// <summary>-teaserOnly name,name on the command line shoots just those takes.</summary>
        private static string ParseOnly()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-teaserOnly") return args[i + 1];
            return "";
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
                Debug.LogError("TeaserCapture: persistentDataPath is '" + dataPath
                               + "', not the scratch identity. Refusing to enter Play Mode.");
                SessionState.SetBool(RestoreKey, true);
                return;
            }
            string save = Path.Combine(dataPath, "emberfall_save.json");
            if (File.Exists(save)) File.Delete(save);
            PlayerPrefs.DeleteAll();

            Directory.CreateDirectory(OutDir());
            if (!GameViewSize.Apply(Width, Height))
                Debug.LogWarning("TeaserCapture: could not pin the Game view size.");

            Debug.Log("TeaserCapture: capturing into " + OutDir());
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.isPlaying = true;
        }

        public static string OutDir()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../Media/captures"));
        }

        // ================================================================== the run

        private static List<TakeSpec> _takes;
        private static int _takeIndex;
        private static int _stage;
        private static int _wait;
        private static TeaserDirector _director;
        private static RecorderController _recorder;
        private static string _file;

        private static void Pump()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (!EditorApplication.isPlaying) return;
            GameApp app = GameApp.Instance;

            if (_stage == 0)
            {
                if (app == null || !GameApp.BootCompleted) return;
                Application.runInBackground = true;
                _takes = BuildTakes();
                string only = SessionState.GetString(OnlyKey, "");
                if (only.Length > 0)
                {
                    List<string> names = new List<string>(only.Split(','));
                    _takes.RemoveAll(k => !names.Contains(k.Name));
                }
                _takeIndex = 0;
                SeedLine(app);
                _director = app.gameObject.AddComponent<TeaserDirector>();
                _wait = 30;
                _stage = 1;
                return;
            }

            if (_wait > 0) { _wait--; return; }

            // ---- set up the next take
            if (_stage == 1)
            {
                if (_takeIndex >= _takes.Count) { _stage = 9; return; }
                TakeSpec take = _takes[_takeIndex];
                _director.End();
                if (take.Battle) SetUpBattle(app, take);
                else SetUpSummon(app, take);
                // Settle frames: the canvas scaler, the first pose and the world all need a few
                // frames before the first one is worth keeping. The battle starts playing the
                // moment its screen shows, so it gets none.
                _wait = take.Battle ? 0 : 20;
                _stage = 2;
                return;
            }

            // ---- roll
            if (_stage == 2)
            {
                TakeSpec take = _takes[_takeIndex];
                _file = Path.Combine(OutDir(), take.Name);
                if (File.Exists(_file + ".mp4")) File.Delete(_file + ".mp4");
                _recorder = StartRecorder(_file);
                _director.Frame = 0;
                _director.Running = true;
                Debug.Log("TeaserCapture: rolling " + take.Name + " for " + take.Frames + " frames");
                _stage = 3;
                return;
            }

            // ---- cut
            if (_stage == 3)
            {
                TakeSpec take = _takes[_takeIndex];
                if (_director.Frame < take.Frames + 2) return;
                _recorder.StopRecording();
                _director.End();
                if (take.Battle)
                {
                    BattleScreen.CaptureSeed = 0;
                    app.Router.Show(ScreenId.MainMenu, false);
                }
                Debug.Log("TeaserCapture: cut " + take.Name);
                _takeIndex++;
                _stage = 1;
                _wait = 30;
                return;
            }

            if (_stage == 9)
            {
                SessionState.SetBool(ActiveKey, false);
                SessionState.SetBool(RestoreKey, true);
                Debug.Log("TeaserCapture: done");
                EditorApplication.isPlaying = false;
            }
        }

        private static void SetUpSummon(GameApp app, TakeSpec take)
        {
            app.Router.Show(ScreenId.Summon);
            SummonScreen summon = app.Router.Summon;
            summon.DebugPreview(Batch);
            _director.Rate = take.Rate;
            _director.StartTime = take.Start;
            _director.ShowName = take.ShowName;
            _director.RevealIndex = RevealIndexOf(summon.DebugSequence, RevealHero);
            _director.BeginSummon();
        }

        private static void SetUpBattle(GameApp app, TakeSpec take)
        {
            SummonSequence seq = app.Router.Summon.DebugSequence;
            if (seq != null) seq.Abort();

            BattleLoadout opponent = new BattleLoadout();
            opponent.Label = "Teaser";
            for (int i = 0; i < EnemyLine.Length; i++)
                opponent.Units.Add(new LoadoutUnit { DefId = EnemyLine[i], Level = 60, Stars = 5 });

            BattleScreen battle = app.Router.Battle;
            battle.SetDuelOpponent(opponent, "Teaser");
            // The duel fights in the Proving's generated backdrop; the teaser wants a painted one.
            FieldInfo stageField = typeof(BattleScreen).GetField("_stage", BindingFlags.Instance | BindingFlags.NonPublic);
            StageDef stage = stageField != null ? stageField.GetValue(battle) as StageDef : null;
            if (stage != null) stage.Environment = BattleEnvironment;

            BattleScreen.CaptureSeed = BattleSeed;
            _director.CameraSize = take.CamSize;
            _director.CameraTarget = take.CamTarget;
            _director.BeginBattle(take.Slow);
            app.Router.Show(ScreenId.Battle);
        }

        /// <summary>Owns the player line the duel fights with, at the level the enemy line is.</summary>
        private static void SeedLine(GameApp app)
        {
            ContentDatabase content = app.Session.Content;
            PlayerProfile profile = app.Session.Profile;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            for (int i = 0; i < PlayerLine.Length; i++)
            {
                HeroDef def = content.GetHero(PlayerLine[i]);
                HeroInstance hero = app.Session.GrantHeroDirect(def, now);
                if (hero == null) { Debug.LogError("TeaserCapture: no hero " + PlayerLine[i]); continue; }
                hero.Level = 60;
                profile.AssignToTeam(i, hero.Uid);
            }
        }

        private static int RevealIndexOf(SummonSequence seq, string heroId)
        {
            FieldInfo f = typeof(SummonSequence).GetField("_outcomes", BindingFlags.Instance | BindingFlags.NonPublic);
            List<SummonOutcome> outcomes = f != null ? f.GetValue(seq) as List<SummonOutcome> : null;
            if (outcomes != null)
                for (int i = 0; i < outcomes.Count; i++)
                    if (outcomes[i].Hero != null && outcomes[i].Hero.Id == heroId) return i;
            Debug.LogError("TeaserCapture: " + heroId + " is not in the preview batch; revealing pull 0.");
            return 0;
        }

        // ================================================================== recorder

        private static RecorderController StartRecorder(string file)
        {
            RecorderControllerSettings controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            RecorderController controller = new RecorderController(controllerSettings);

            MovieRecorderSettings movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = Path.GetFileName(file);
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingProfile = CoreEncoderSettings.H264EncodingProfile.High,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.Custom,
                TargetBitRate = BitrateMbps,
                // A short GOP keeps every frame near a keyframe, so ffmpeg can seek and cut
                // anywhere in the take cheaply.
                GopSize = 30,
                KeyframeDistance = 30,
                NumConsecutiveBFrames = 0,
            };
            movie.CaptureAlpha = false;
            movie.CaptureAudio = false;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = Width, OutputHeight = Height };
            movie.OutputFile = file;

            controllerSettings.AddRecorderSettings(movie);
            controllerSettings.SetRecordModeToManual();
            controllerSettings.FrameRate = Fps;
            controllerSettings.FrameRatePlayback = FrameRatePlayback.Constant;
            controllerSettings.CapFrameRate = true;
            controllerSettings.ExitPlayMode = false;

            RecorderOptions.VerboseMode = false;
            controller.PrepareRecording();
            if (!controller.StartRecording()) Debug.LogError("TeaserCapture: recorder did not start for " + file);
            return controller;
        }

        // ================================================================== restore

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
            Debug.Log("TeaserCapture: identity restored to '" + company + "/" + product + "'");
            if (SessionState.GetBool(ExitKey, false)) EditorApplication.Exit(0);
        }
    }
}
#endif
