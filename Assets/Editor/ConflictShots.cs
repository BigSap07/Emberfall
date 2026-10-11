#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Cloud;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Save;
using Emberfall.Core.Session;
using Emberfall.Game;
using Emberfall.Game.Cloud;
using Emberfall.UI;
using Emberfall.UI.Screens;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// Photographs the two save-safety dialogs of Docs/CLOUD-GAPS.md, P9 and P11: the conflict card
    /// on the two saves the audit used, and the RESET PROGRESS confirm. Both aspects, into
    /// Docs/ui-shots.
    ///
    /// The two saves are CloudChecks' pair, built the same way: a starter profile with one hero
    /// levelled and one stage cleared, and the same profile a week later with two million more
    /// gold, 5000 more shards, every hero ten levels higher and Recital floor 40. The week-old one
    /// is THIS DEVICE and the rich one THE CLOUD, which is the case that loses the most: an old
    /// phone brought back out, and a player one tap from keeping it.
    ///
    /// Runs on a scratch identity with the cloud held off, exactly as GuildDungeonShots does, and
    /// never copies or reads the real save: it hashes it before and after, and a changed hash fails
    /// the run. The card is opened through ConflictChooser.Ask with a SyncOutcome built the way
    /// CloudSaveService builds one, so what is photographed is what a sync would show.
    ///
    ///   Unity -projectPath . -executeMethod Emberfall.EditorTools.ConflictShots.Run
    ///   Unity -projectPath . -executeMethod Emberfall.EditorTools.ConflictShots.RunBefore
    ///
    /// RunBefore only changes the file names, so a before and after pair is two runs of one tool.
    /// </summary>
    [InitializeOnLoad]
    public static class ConflictShots
    {
        private const string ActiveKey = "ConflictShots.Active";
        private const string RestoreKey = "ConflictShots.Restore";
        private const string ExitKey = "ConflictShots.ExitWhenDone";
        private const string SuffixKey = "ConflictShots.Suffix";
        private const string OrigCompanyKey = "ConflictShots.OrigCompany";
        private const string OrigProductKey = "ConflictShots.OrigProduct";
        private const string OrigSuppressKey = "ConflictShots.OrigSuppress";
        private const string LiveSaveKey = "ConflictShots.LiveSave";
        private const string LiveHashKey = "ConflictShots.LiveHash";

        private const string ScratchCompany = "Emberfall Scratch";
        private const string ScratchProduct = "Emberfall Conflict Shots";

        private static readonly int[][] Sizes = { new[] { 1920, 1080 }, new[] { 2400, 1080 } };

        private sealed class Step
        {
            public string Name;
            public Action Do;
            public int Settle = 40;
            public string Shot;
        }

        private static List<Step> _steps;
        private static int _stepIndex;
        private static int _sizeIndex;
        private static int _stage;
        private static int _wait;
        private static string _pending;
        private static readonly List<string> _report = new List<string>();
        private static int _failures;

        static ConflictShots()
        {
            EditorApplication.update += Pump;
            EditorApplication.update += PumpRestore;
        }

        public static void Run() { SessionState.SetString(SuffixKey, ""); SessionState.SetBool(ExitKey, true); Begin(); }

        public static void RunBefore() { SessionState.SetString(SuffixKey, "-before"); SessionState.SetBool(ExitKey, true); Begin(); }

        [MenuItem("Emberfall/Capture/Conflict card and reset dialog", false, 84)]
        public static void RunFromMenu() { SessionState.SetString(SuffixKey, ""); SessionState.SetBool(ExitKey, false); Begin(); }

        private static void Begin()
        {
            string liveSave = Path.Combine(Application.persistentDataPath, SaveService.FileName);
            SessionState.SetString(LiveSaveKey, liveSave);
            SessionState.SetString(LiveHashKey, HashOf(liveSave));

            SessionState.SetString(OrigCompanyKey, PlayerSettings.companyName);
            SessionState.SetString(OrigProductKey, PlayerSettings.productName);
            SessionState.SetBool(OrigSuppressKey, EditorPrefs.GetBool(GameApp.SuppressFirstRunKey, false));

            PlayerSettings.companyName = ScratchCompany;
            PlayerSettings.productName = ScratchProduct;
            EditorPrefs.SetBool(GameApp.SuppressFirstRunKey, true);

            string dataPath = Application.persistentDataPath;
            if (dataPath.IndexOf(ScratchProduct, StringComparison.OrdinalIgnoreCase) < 0)
            {
                Debug.LogError("ConflictShots: persistentDataPath is '" + dataPath
                               + "', not the scratch identity. Refusing to enter Play Mode.");
                SessionState.SetBool(RestoreKey, true);
                return;
            }
            // A fresh scratch profile every run. The real save is never copied here: nothing this
            // run photographs is about the player's own progress, and the two saves on the card
            // are built below.
            Directory.CreateDirectory(dataPath);
            string save = Path.Combine(dataPath, SaveService.FileName);
            if (File.Exists(save)) File.Delete(save);

            Directory.CreateDirectory(OutDir());
            if (!GameViewSize.Apply(Sizes[0][0], Sizes[0][1]))
                Debug.LogWarning("ConflictShots: could not pin the Game view size.");

            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            SessionState.SetBool(ActiveKey, true);
            _stage = 0;
            EditorCaptureGuard.EnterPlayMode();
        }

        private static string OutDir()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/ui-shots"));
        }

        private static string ReportPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/Screenshots/conflict-shots.txt"));
        }

        // ================================================================== the two saves

        /// <summary>CloudChecks.Rich, step for step.</summary>
        private static PlayerProfile Rich(ContentDatabase content)
        {
            PlayerProfile profile = GameSession.CreateNewProfile(content, 1700000000);
            GameSession session = new GameSession(content, profile);
            session.LevelUpHero(profile.Heroes[0], 6);
            session.GrantBattleResult(content.AllStages[0], true, 1700000100);
            profile.Gold = 123456;
            return profile;
        }

        private static SyncOutcome AuditPair(ContentDatabase content)
        {
            long now = TimeUtil.NowUnix();

            PlayerProfile weekOld = Rich(content);
            PlayerProfile weekOn = Rich(content);
            weekOn.Gold += 2000000;
            weekOn.Shards += 5000;
            weekOn.RecitalHighestFloor = 40;
            for (int i = 0; i < weekOn.Heroes.Count; i++) weekOn.Heroes[i].Level += 10;
            weekOn.LastSaveUnix = now - 3 * 3600;
            weekOld.LastSaveUnix = weekOn.LastSaveUnix - 7 * 86400;
            return Outcome(content, weekOld, weekOn);
        }

        /// <summary>
        /// Two saves that each have something the other lacks, which is the case a verdict could
        /// get wrong by picking a winner. This device went on in the campaign and pulled a hero;
        /// the cloud farmed gold and climbed the Proving. Both must light, on different rows.
        /// </summary>
        private static SyncOutcome MixedPair(ContentDatabase content)
        {
            long now = TimeUtil.NowUnix();

            PlayerProfile local = Rich(content);
            GameSession session = new GameSession(content, local);
            session.GrantBattleResult(content.AllStages[1], true, 1700000200);
            session.GrantBattleResult(content.AllStages[2], true, 1700000300);
            HeroDef extra = null;
            for (int i = 0; i < content.Heroes.Count && extra == null; i++)
            {
                HeroDef candidate = content.Heroes[i];
                if (local.Heroes.TrueForAll(h => h.DefId != candidate.Id)) extra = candidate;
            }
            if (extra != null) local.Heroes.Add(new HeroInstance { Uid = "h-extra", DefId = extra.Id, Level = 1 });
            local.RelicDust += 500;
            local.LastSaveUnix = now - 40 * 60;

            PlayerProfile cloud = Rich(content);
            cloud.Gold += 300000;
            cloud.ProvingHighestStanding = 12;
            cloud.LastSaveUnix = now - 26 * 3600;
            return Outcome(content, local, cloud);
        }

        private static SyncOutcome Outcome(ContentDatabase content, PlayerProfile localProfile, PlayerProfile cloudProfile)
        {
            string local = SaveSerializer.Serialize(localProfile);
            string cloud = SaveSerializer.Serialize(cloudProfile);

            SyncOutcome outcome = new SyncOutcome();
            SyncInputs inputs = new SyncInputs();
            inputs.HasLocal = true;
            inputs.HasCloud = true;
            inputs.BaseRevision = 9;
            inputs.CloudRevision = 12;
            inputs.CloudSaveVersion = SaveSerializer.CurrentVersion;
            inputs.ReaderVersion = SaveSerializer.CurrentVersion;
            SyncPlan plan = SyncPlanner.Decide(inputs);
            outcome.Action = plan.Action;
            outcome.Reason = plan.Reason;
            outcome.Local = SaveSummaryReader.From(local);
            outcome.Cloud = SaveSummaryReader.From(cloud);
            outcome.Record = new CloudSaveRecord
            {
                Revision = 12,
                DeviceId = "0d3c9a8e-another-device",
                SaveVersion = SaveSerializer.CurrentVersion,
                Data = cloud
            };
            Check(plan.Action == SyncAction.AskPlayer, "the planner asks the player on this pair: " + plan.Reason);
            SaveComparison c = SaveComparison.Of(outcome.Local, outcome.Cloud, content);
            Note("this device: " + c.LocalVerdict + (c.LocalAlso.Length > 0 ? " / " + c.LocalAlso : ""));
            Note("the cloud:   " + c.CloudVerdict + (c.CloudAlso.Length > 0 ? " / " + c.CloudAlso : ""));
            for (int i = 0; i < c.Rows.Count; i++)
                Note("  " + c.Rows[i].Label + ": " + c.Rows[i].LocalText + " | " + c.Rows[i].CloudText
                     + (c.Rows[i].Ahead != SaveSide.Neither ? "   (" + c.Rows[i].Ahead + " " + c.Rows[i].Lead + ")" : ""));
            return outcome;
        }

        // ================================================================== the run

        private static List<Step> BuildSteps(GameApp app)
        {
            List<Step> steps = new List<Step>();
            steps.Add(new Step
            {
                Name = "Settings",
                Do = delegate
                {
                    app.Router.Show(ScreenId.MainMenu, false);
                    app.Panels.ShowProfile("SETTINGS");
                },
                Settle = 20
            });
            // The copy a player with an account reads, through the one method the button uses.
            // This run holds the cloud off, so the button itself takes the other branch, shot next.
            steps.Add(new Step
            {
                Name = "reset dialog, with an account",
                Do = delegate { SettingsScreen.ShowResetConfirm(app, true); },
                Shot = "reset-dialog"
            });
            steps.Add(new Step
            {
                Name = "Settings again",
                Do = delegate
                {
                    app.Router.Show(ScreenId.MainMenu, false);
                    app.Panels.ShowProfile("SETTINGS");
                },
                Settle = 20
            });
            steps.Add(new Step
            {
                Name = "press RESET PROGRESS, no account service",
                Do = delegate { Check(Press("Reset"), "the Settings panel has a RESET PROGRESS button to press"); },
                Shot = "reset-dialog-no-account"
            });
            AddConflict(steps, app, "the audit's pair", AuditPair, "conflict-chooser");
            AddConflict(steps, app, "a mixed pair", MixedPair, "conflict-chooser-mixed");
            return steps;
        }

        private static void AddConflict(List<Step> steps, GameApp app, string name,
                                        Func<ContentDatabase, SyncOutcome> pair, string shot)
        {
            steps.Add(new Step
            {
                Name = "conflict card: " + name,
                Do = delegate
                {
                    app.Router.Show(ScreenId.MainMenu, false);
                    app.Conflict.Ask(pair(app.Content), delegate (ConflictChoice choice)
                    {
                        Note("answered " + choice + " (the run's own press, to close the card)");
                    });
                    Check(app.Conflict.IsOpen, "the conflict card is open");
                },
                Shot = shot
            });
            steps.Add(new Step
            {
                Name = "close the card",
                Do = delegate { Press("KeepCloud"); },
                Settle = 10
            });
        }

        private static bool Press(string name)
        {
            Button[] buttons = UnityEngine.Object.FindObjectsByType<Button>();
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].gameObject.name != name || !buttons[i].gameObject.activeInHierarchy) continue;
                buttons[i].onClick.Invoke();
                return true;
            }
            return false;
        }

        private static void Check(bool ok, string label)
        {
            if (!ok) _failures++;
            Note((ok ? "[ok]   " : "[FAIL] ") + label);
        }

        private static void Note(string line)
        {
            _report.Add(line);
            Debug.Log("ConflictShots: " + line);
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
                _report.Clear();
                _failures = 0;
                Note("scratch identity: " + Application.persistentDataPath);
                Check(app.Cloud == null, "the cloud is held off for this run - nothing reaches the live project");
                _sizeIndex = 0;
                StartPass(app);
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
                if (_stepIndex >= _steps.Count)
                {
                    _sizeIndex++;
                    if (_sizeIndex >= Sizes.Length) { _stage = 9; return; }
                    GameViewSize.Apply(Sizes[_sizeIndex][0], Sizes[_sizeIndex][1]);
                    StartPass(app);
                    _wait = 30;
                    return;
                }
                Step step = _steps[_stepIndex];
                Note("step: " + step.Name + " @" + Sizes[_sizeIndex][0]);
                try { step.Do(); }
                catch (Exception ex) { Check(false, step.Name + " threw: " + ex); }
                Canvas.ForceUpdateCanvases();
                _stage = 3;
                _wait = step.Settle;
                return;
            }

            if (_stage == 3)
            {
                Step step = _steps[_stepIndex];
                _stepIndex++;
                _stage = 1;
                if (string.IsNullOrEmpty(step.Shot)) return;
                string file = step.Shot + SessionState.GetString(SuffixKey, "") + (_sizeIndex == 0 ? "" : "-2400") + ".png";
                _pending = Path.Combine(OutDir(), file);
                if (File.Exists(_pending)) File.Delete(_pending);
                ScreenCapture.CaptureScreenshot(_pending, 1);
                Note("shot: " + file);
                _wait = 2;
                return;
            }

            if (_stage == 9)
            {
                Note(_failures == 0 ? "ALL CHECKS PASSED" : _failures + " CHECK(S) FAILED");
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath()));
                File.WriteAllLines(ReportPath(), _report.ToArray());
                SessionState.SetBool(ActiveKey, false);
                SessionState.SetBool(RestoreKey, true);
                Debug.Log("ConflictShots: done");
                EditorApplication.isPlaying = false;
            }
        }

        private static void StartPass(GameApp app)
        {
            Note("---- pass at " + Sizes[_sizeIndex][0] + "x" + Sizes[_sizeIndex][1]);
            _steps = BuildSteps(app);
            _stepIndex = 0;
            _pending = null;
            _stage = 1;
            _wait = 20;
        }

        private static void ForceGameViewRepaint()
        {
            Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null) return;
            EditorWindow window = EditorWindow.GetWindow(gameViewType, false, null, false);
            if (window == null) return;
            window.Repaint();
        }

        private static string HashOf(string path)
        {
            if (!File.Exists(path)) return "absent";
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
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

            string live = SessionState.GetString(LiveSaveKey, "");
            string before = SessionState.GetString(LiveHashKey, "");
            string verdict = before == HashOf(live) ? "[ok]   the real save is byte for byte what it was before the run"
                                                    : "[FAIL] the real save CHANGED during the run";
            Debug.Log("ConflictShots: " + verdict);
            try { File.AppendAllText(ReportPath(), verdict + Environment.NewLine); } catch (Exception) { }

            Debug.Log("ConflictShots: identity restored to '" + company + "/" + product + "'");
            if (SessionState.GetBool(ExitKey, false)) EditorApplication.Exit(0);
        }
    }
}
#endif
