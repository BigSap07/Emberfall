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
using Emberfall.Core.Model;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// PHOTOGRAPHS THE TALENT PAGE, the hero page's door to it, and the Rift's empty stage, at
    /// 1920x1080 and 2400x1080, into Docs/ui-shots (gitignored).
    ///
    /// ON A COPY OF THE REAL SAVE. The talent page's readings are fights of the player's own line,
    /// so a fresh profile - one 1-star and nobody deployed - would photograph a page with nothing
    /// on it to measure. The live save is copied into a scratch identity (the ArmorVerification
    /// pattern, as SummonSequenceShots uses it) and every change this run makes lands on the copy:
    /// the quit-time save writes to the scratch folder, never to the real one.
    ///
    /// The trial is left to run on its own frames rather than forced, so the console line
    /// TalentsScreen logs on finishing is the page's real cost in the editor, and the shot is
    /// taken once it has finished.
    ///
    ///   Unity.exe -projectPath . -executeMethod Emberfall.EditorTools.TalentShots.Run
    ///
    /// NOT -batchMode: ScreenCapture photographs the Game view, and batch mode has none.
    /// </summary>
    [InitializeOnLoad]
    public static class TalentShots
    {
        private const string ActiveKey = "TalentShots.Active";
        private const string RestoreKey = "TalentShots.Restore";
        private const string ExitKey = "TalentShots.ExitWhenDone";
        private const string OrigCompanyKey = "TalentShots.OrigCompany";
        private const string OrigProductKey = "TalentShots.OrigProduct";
        private const string OrigSuppressKey = "TalentShots.OrigSuppress";

        private const string ScratchCompany = "Emberfall Scratch";
        private const string ScratchProduct = "Emberfall Talent Shots";

        private static readonly int[][] Sizes = { new[] { 1920, 1080 }, new[] { 2400, 1080 } };

        /// <summary>The heroes photographed, by def id. Kael is the hero the trial's confidence
        /// was diagnosed on: his first two talents are gains small enough to need more than forty
        /// fights. Velis should print no audit line, Talren has the longest numbers list in the
        /// roster, and Nysera carries the one talent TalentAudit.Costly marks, a trade. Which
        /// talents are audited is TalentAudit.Costly, read at runtime by the screen.</summary>
        private static readonly string[] TalentHeroes =
        {
            "kael_the_unyielding", "velis_the_hollow_vow", "talren_the_wayfinder",
            "mireth_the_erased_name", "nysera_the_missing_arrow"
        };

        private static List<string> _shots;
        private static int _sizeIndex;
        private static int _shotIndex;
        private static int _wait;
        private static int _stage;
        private static int _waited;
        private static string _pending;

        static TalentShots()
        {
            EditorApplication.update += Pump;
            EditorApplication.update += PumpRestore;
        }

        public static void Run()
        {
            SessionState.SetBool(ExitKey, true);
            Begin();
        }

        [MenuItem("Emberfall/Capture/Talent page", false, 82)]
        public static void RunFromMenu()
        {
            SessionState.SetBool(ExitKey, false);
            Begin();
        }

        private static void Begin()
        {
            // The live save's path, read BEFORE the identity moves, so the copy has a source.
            string liveSave = Path.Combine(Application.persistentDataPath, SaveService.FileName);

            SessionState.SetString(OrigCompanyKey, PlayerSettings.companyName);
            SessionState.SetString(OrigProductKey, PlayerSettings.productName);
            SessionState.SetBool(OrigSuppressKey, EditorPrefs.GetBool(GameApp.SuppressFirstRunKey, false));

            PlayerSettings.companyName = ScratchCompany;
            PlayerSettings.productName = ScratchProduct;
            EditorPrefs.SetBool(GameApp.SuppressFirstRunKey, true);

            string dataPath = Application.persistentDataPath;
            if (dataPath.IndexOf(ScratchProduct, StringComparison.OrdinalIgnoreCase) < 0)
            {
                Debug.LogError("TalentShots: persistentDataPath is '" + dataPath
                               + "', not the scratch identity. Refusing to enter Play Mode.");
                SessionState.SetBool(RestoreKey, true);
                return;
            }
            Directory.CreateDirectory(dataPath);
            string save = Path.Combine(dataPath, SaveService.FileName);
            if (File.Exists(save)) File.Delete(save);
            PlayerPrefs.DeleteAll();
            if (File.Exists(liveSave)) File.Copy(liveSave, save);
            else Debug.LogWarning("TalentShots: no live save at '" + liveSave + "', so this runs on a fresh profile.");

            Directory.CreateDirectory(OutDir());
            if (!GameViewSize.Apply(Sizes[0][0], Sizes[0][1]))
                Debug.LogWarning("TalentShots: could not pin the Game view size.");

            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            SessionState.SetBool(ActiveKey, true);
            EditorCaptureGuard.EnterPlayMode();
        }

        private static string OutDir()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/ui-shots"));
        }

        private static List<string> BuildShots()
        {
            List<string> shots = new List<string>();
            shots.Add("summon");
            shots.Add("summon-guarantee");
            shots.Add("herodetail-talents");
            for (int i = 0; i < TalentHeroes.Length; i++) shots.Add("talents-" + TalentHeroes[i]);
            shots.Add("talents-no-talents");
            return shots;
        }

        private static HeroInstance Owned(GameApp app, string defId)
        {
            return app.Session.Profile.FindHeroByDefId(defId);
        }

        /// <summary>Puts the screen for one shot up. Returns false to skip a shot this save
        /// cannot show (a hero it does not own), which is logged rather than faked.</summary>
        private static bool Stage(GameApp app, string shot)
        {
            PlayerProfile profile = app.Session.Profile;
            if (shot == "summon" || shot == "summon-guarantee")
            {
                // The guarantee state, on the scratch copy only: one pull into a fresh account.
                if (shot == "summon-guarantee") profile.TotalSummons = 1;
                app.Router.Show(ScreenId.Summon);
                return true;
            }
            if (profile.TotalSummons < 3) profile.TotalSummons = 3;

            if (shot == "herodetail-talents")
            {
                HeroInstance kael = Owned(app, "kael_the_unyielding");
                if (kael == null) return false;
                app.Router.HeroDetail.SetHero(kael);
                app.Router.Show(ScreenId.HeroDetail);
                return true;
            }
            if (shot == "talents-no-talents")
            {
                HeroInstance four = null;
                for (int i = 0; i < profile.Heroes.Count && four == null; i++)
                    if (profile.Heroes[i].Def != null && profile.Heroes[i].Def.Stars == 4) four = profile.Heroes[i];
                if (four == null) return false;
                app.Router.Talents.SetHero(four);
                app.Router.Show(ScreenId.Talents);
                return true;
            }

            HeroInstance hero = Owned(app, shot.Substring("talents-".Length));
            if (hero == null) return false;
            app.Router.Talents.SetHero(hero);
            app.Router.Show(ScreenId.Talents);
            return true;
        }

        private static string FileName(string shot)
        {
            string name = shot.StartsWith("talents-") && shot != "talents-no-talents"
                ? "talents-" + shot.Substring("talents-".Length).Split('_')[0]
                : shot;
            return name + (_sizeIndex == 0 ? "" : "-2400") + ".png";
        }

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
                _pending = null;

                // One pick made, on the copy, so the page is photographed with a card RUNNING and
                // the strip showing a numeral rather than a row of NONE.
                HeroInstance kael = Owned(app, "kael_the_unyielding");
                if (kael != null) app.Session.SelectTalent(kael, 2);
                _stage = 1;
                _wait = 20;
                return;
            }

            if (_wait > 0) { _wait--; return; }

            if (_stage == 1)
            {
                if (_pending != null)
                {
                    if (!File.Exists(_pending)) return;
                    _pending = null;
                }
                if (_shotIndex >= _shots.Count)
                {
                    _shotIndex = 0;
                    _sizeIndex++;
                    if (_sizeIndex >= Sizes.Length) { _stage = 9; return; }
                    GameViewSize.Apply(Sizes[_sizeIndex][0], Sizes[_sizeIndex][1]);
                    _wait = 30;
                    return;
                }
                string shot = _shots[_shotIndex];
                if (!Stage(app, shot))
                {
                    Debug.LogWarning("TalentShots: this save cannot show '" + shot + "', skipped.");
                    _shotIndex++;
                    return;
                }
                _stage = 2;
                _waited = 0;
                _wait = 12;
                return;
            }

            if (_stage == 2)
            {
                // Wait for the page's own measurement to finish on its own frames, with a ceiling.
                TalentsScreen talents = app.Router.Talents;
                bool onTalents = app.Router.Current == talents;
                if (onTalents && !talents.TrialFinished && _waited < 1200) { _waited++; return; }
                if (onTalents && !talents.TrialFinished)
                {
                    Debug.LogWarning("TalentShots: the trial had not finished in 1200 frames; finishing it.");
                    talents.FinishTrialForCapture();
                }
                Canvas.ForceUpdateCanvases();
                _stage = 3;
                _wait = 10;
                return;
            }

            if (_stage == 3)
            {
                string shot = _shots[_shotIndex];
                _pending = Path.Combine(OutDir(), FileName(shot));
                if (File.Exists(_pending)) File.Delete(_pending);
                ScreenCapture.CaptureScreenshot(_pending, 1);
                Debug.Log("TalentShots: requested " + FileName(shot));
                if (shot == "summon-guarantee") app.Session.Profile.TotalSummons = 3;
                _shotIndex++;
                _stage = 1;
                _wait = 2;
                return;
            }

            if (_stage == 9)
            {
                SessionState.SetBool(ActiveKey, false);
                SessionState.SetBool(RestoreKey, true);
                Debug.Log("TalentShots: done");
                EditorApplication.isPlaying = false;
            }
        }

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
            Debug.Log("TalentShots: identity restored to '" + company + "/" + product + "'");
            if (SessionState.GetBool(ExitKey, false)) EditorApplication.Exit(0);
        }
    }
}
#endif
