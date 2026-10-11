#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Game;
using Emberfall.Game.Cloud;
using Emberfall.UI;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// Photographs the name prompt, a refused name and the Friends screen with the player's code,
    /// into Docs/ui-shots, with NO NETWORK and NO REAL ACCOUNT.
    ///
    /// The cloud is held off for the run, exactly as ConflictShots does, and a stand-in coordinator
    /// is installed instead: signed in as a made-up user, over a client whose config is NOT
    /// configured, so every request returns its config's Problem without leaving the machine. That
    /// is what makes two of the captures honest about what they are:
    ///
    ///   - the CLIENT refusal ("ab") is the real DisplayNameRules sentence, produced before any
    ///     request, exactly as a player would see it;
    ///   - the SERVER-STYLE refusal is NOT a server's answer. The stand-in's Problem is set to the
    ///     sentence PostgREST gives for a violated CHECK constraint, so what is photographed is how
    ///     the prompt draws a long refusal from the other side of a request: on one line, cut, and
    ///     with the field still holding what was typed.
    ///
    /// Runs on a scratch identity and hashes the real save before and after; a changed hash fails
    /// the run.
    ///
    ///   Unity -projectPath . -executeMethod Emberfall.EditorTools.NameOnDemandShots.Run
    /// </summary>
    [InitializeOnLoad]
    public static class NameOnDemandShots
    {
        private const string ActiveKey = "NameOnDemandShots.Active";
        private const string RestoreKey = "NameOnDemandShots.Restore";
        private const string ExitKey = "NameOnDemandShots.ExitWhenDone";
        private const string OrigCompanyKey = "NameOnDemandShots.OrigCompany";
        private const string OrigProductKey = "NameOnDemandShots.OrigProduct";
        private const string OrigSuppressKey = "NameOnDemandShots.OrigSuppress";
        private const string LiveSaveKey = "NameOnDemandShots.LiveSave";
        private const string LiveHashKey = "NameOnDemandShots.LiveHash";

        private const string ScratchCompany = "Emberfall Scratch";
        private const string ScratchProduct = "Emberfall Name Shots";

        /// <summary>The sentence PostgREST raises for a CHECK violation on profiles.display_name.</summary>
        private const string CheckViolation =
            "new row for relation \"profiles\" violates check constraint \"profiles_display_name_check\"";

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
        private static SupabaseConfig _config;
        private static readonly List<string> _report = new List<string>();
        private static int _failures;

        static NameOnDemandShots()
        {
            EditorApplication.update += Pump;
            EditorApplication.update += PumpRestore;
        }

        public static void Run() { SessionState.SetBool(ExitKey, true); Begin(); }

        [MenuItem("Emberfall/Capture/Name prompt and friend code", false, 85)]
        public static void RunFromMenu() { SessionState.SetBool(ExitKey, false); Begin(); }

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
                Debug.LogError("NameOnDemandShots: persistentDataPath is '" + dataPath
                               + "', not the scratch identity. Refusing to enter Play Mode.");
                SessionState.SetBool(RestoreKey, true);
                return;
            }
            Directory.CreateDirectory(dataPath);
            string save = Path.Combine(dataPath, SaveService.FileName);
            if (File.Exists(save)) File.Delete(save);

            Directory.CreateDirectory(OutDir());
            if (!GameViewSize.Apply(Sizes[0][0], Sizes[0][1]))
                Debug.LogWarning("NameOnDemandShots: could not pin the Game view size.");

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
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/Screenshots/name-on-demand-shots.txt"));
        }

        // ================================================================== the stand-in account

        /// <summary>Signed in as nobody in particular, on a client that cannot reach anything.</summary>
        private static void InstallCloud(GameApp app, string displayName)
        {
            _config = new SupabaseConfig();
            _config.Problem = "Supabase is not configured";

            CloudLocalState state = new CloudLocalState();
            state.UserId = "00000000-0000-0000-0000-00000000cafe";
            state.RefreshToken = "scratch";

            SupabaseClient client = new SupabaseClient(_config, app);
            client.AccessToken = "scratch-token";
            AccountService account = new AccountService(client, state);
            CloudSaveService saves = new CloudSaveService(client, account, state);
            SocialService social = new SocialService(client, account, state);
            GuildService guild = new GuildService(client, account, state);
            CloudCoordinator cloud = new CloudCoordinator(app, client, account, saves, social, guild, state);

            AccountProfile profile = new AccountProfile();
            profile.UserId = state.UserId;
            profile.DisplayName = displayName;
            profile.FriendCode = "KQ7M4X";
            typeof(CloudCoordinator).GetProperty("Profile").GetSetMethod(true).Invoke(cloud, new object[] { profile });

            SetAppCloud(app, cloud);
        }

        private static void SetAppCloud(GameApp app, CloudCoordinator cloud)
        {
            typeof(GameApp).GetProperty("Cloud").GetSetMethod(true).Invoke(app, new object[] { cloud });
        }

        // ================================================================== the steps

        private static List<Step> BuildSteps(GameApp app)
        {
            List<Step> steps = new List<Step>();

            steps.Add(new Step
            {
                Name = "a signed-in account that never chose a name, opens Friends",
                Do = delegate
                {
                    app.Router.Show(ScreenId.MainMenu, false);
                    InstallCloud(app, "Wayfarer");
                    app.Router.Show(ScreenId.Friends);
                },
                // Long enough for the toast the stand-in's failed friends load raises to have left the
                // screen: it would otherwise sit over the prompt's buttons.
                Settle = 1500,
                Shot = "name-prompt"
            });
            steps.Add(new Step
            {
                Name = "a name that is too short is refused before anything is sent",
                Do = delegate
                {
                    Check(app.NamePrompt.IsOpen, "the name prompt is open over Friends");
                    Check(TypeName("ab"), "typed 'ab' into the prompt's field");
                    Check(Press("Save"), "pressed SAVE NAME");
                },
                Settle = 30,
                Shot = "name-refused-client"
            });
            steps.Add(new Step
            {
                Name = "a name the other side refuses is shown on one line, field untouched",
                Do = delegate
                {
                    _config.Problem = CheckViolation;
                    Check(TypeName("  Ash Walker  "), "typed '  Ash Walker  ' into the prompt's field");
                    Check(Press("Save"), "pressed SAVE NAME");
                },
                Settle = 60,
                Shot = "name-refused-server"
            });
            steps.Add(new Step
            {
                Name = "NOT NOW leaves Friends open, with the player's code and a copy button",
                Do = delegate
                {
                    Check(Press("Skip"), "pressed NOT NOW");
                    Check(!app.NamePrompt.IsOpen, "the prompt is closed");
                },
                // Long enough for the toast the failed friends load raises (the stand-in cannot
                // reach a server) to have left the screen.
                Settle = 600,
                Shot = "friends-code"
            });
            steps.Add(new Step
            {
                Name = "a build with no cloud: Friends has no code card and nothing asks for a name",
                Do = delegate
                {
                    app.Router.Show(ScreenId.MainMenu, false);
                    SetAppCloud(app, null);
                    app.Router.Show(ScreenId.Friends);
                    Check(app.Cloud == null, "this build has no cloud");
                },
                Settle = 120,
                Shot = "friends-no-cloud"
            });
            steps.Add(new Step
            {
                Name = "no prompt appeared in that build",
                Do = delegate { Check(!app.NamePrompt.IsOpen, "the name prompt did not open without a cloud"); },
                Settle = 5
            });
            return steps;
        }

        private static bool TypeName(string text)
        {
            InputField[] fields = UnityEngine.Object.FindObjectsByType<InputField>();
            for (int i = 0; i < fields.Length; i++)
            {
                if (!InsidePrompt(fields[i].transform)) continue;
                fields[i].text = text;
                return true;
            }
            return false;
        }

        private static bool Press(string name)
        {
            Button[] buttons = UnityEngine.Object.FindObjectsByType<Button>();
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].gameObject.name != name || !buttons[i].gameObject.activeInHierarchy) continue;
                if (!InsidePrompt(buttons[i].transform)) continue;
                buttons[i].onClick.Invoke();
                return true;
            }
            return false;
        }

        private static bool InsidePrompt(Transform t)
        {
            if (!t.gameObject.activeInHierarchy) return false;
            for (Transform p = t; p != null; p = p.parent) if (p.name == "NamePrompt") return true;
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
            Debug.Log("NameOnDemandShots: " + line);
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
                Check(app.Cloud == null, "the real cloud is held off for this run - nothing reaches the live project");
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
                string file = step.Shot + (_sizeIndex == 0 ? "" : "-2400") + ".png";
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
                Debug.Log("NameOnDemandShots: done");
                EditorApplication.isPlaying = false;
            }
        }

        private static void StartPass(GameApp app)
        {
            Note("---- pass at " + Sizes[_sizeIndex][0] + "x" + Sizes[_sizeIndex][1]);
            // A pass starts from a clean slate: the second one needs the prompt to ask again.
            if (_sizeIndex > 0) { app.Router.Show(ScreenId.MainMenu, false); SetAppCloud(app, null); }
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
            Debug.Log("NameOnDemandShots: " + verdict);
            try { File.AppendAllText(ReportPath(), verdict + Environment.NewLine); } catch (Exception) { }

            Debug.Log("NameOnDemandShots: identity restored to '" + company + "/" + product + "'");
            if (SessionState.GetBool(ExitKey, false)) EditorApplication.Exit(0);
        }
    }
}
#endif
