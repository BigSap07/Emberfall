#if EMBERFALL_PLAYLOG
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Emberfall.Core.Session;
using Emberfall.Game.Cloud;
using Emberfall.UI;

namespace Emberfall.Game
{
    /// <summary>
    /// THE PLAY LOG, WIRED (R27, Docs/FRIENDS-BUILD.md). Compiled only with EMBERFALL_PLAYLOG. Everything that decides
    /// a line, a cap or a rotation is in Core (PlayLogRules, PlayLogSession, PlayLogWriter) and is proved by the
    /// harness; this class owns the clock, the device facts, the hooks that are not in Core (the router, the error
    /// callback, pause and quit), the share action and the smoke test.
    ///
    /// IT ONLY WATCHES. It changes no profile, no screen and no fight, and it never reads or sends anything: the file
    /// is local and the player sends it by hand. It is independent of the play stats consent.
    /// </summary>
    public sealed class PlayLogService
    {
        public const string SmokeFlag = "-emberfallSmoke";

        public static PlayLogService Instance { get; private set; }

        private readonly GameApp _app;
        private readonly PlayLogWriter _writer;
        private readonly PlayLogSession _session;
        private readonly float _start;
        private bool _ended;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            PlayLogHooks.Active = null;
            SmokeStorage.Active = false;
        }

        /// <summary>
        /// Decides, before any scene loads and so before the save is read, whether this run is a smoke run. If it is,
        /// SmokeStorage redirects the save file, the save mirror, the cloud session and the play log to throw-away
        /// names, so the run is a brand new anonymous player and the real data is never opened.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void DecideSmokeStorage()
        {
            SmokeStorage.Active = SmokeStorage.ShouldActivate(PlayLogSwitch.Enabled, Environment.GetCommandLineArgs());
        }

        /// <summary>Builds the service once, writes the session header and starts listening. Never throws.</summary>
        public static PlayLogService Install(GameApp app)
        {
            if (!PlayLogSwitch.Enabled || app == null) return null;
            if (Instance != null) return Instance;
            try { Instance = new PlayLogService(app); }
            catch (Exception) { Instance = null; PlayLogHooks.Active = null; }
            if (Instance != null && PlayLogRules.HasFlag(Environment.GetCommandLineArgs(), SmokeFlag))
                app.StartCoroutine(Instance.SmokeRoutine());
            return Instance;
        }

        private PlayLogService(GameApp app)
        {
            _app = app;
            _start = Time.realtimeSinceStartup;
            _writer = new PlayLogWriter(true, Application.persistentDataPath, SmokeStorage.PlayLogFile, SmokeStorage.PlayLogPrevFile);
            _writer.Open();
            string id = Guid.NewGuid().ToString("N").Substring(0, 8);
            _session = new PlayLogSession(_writer, delegate () { return Time.realtimeSinceStartup - _start; }, id);
            PlayLogHooks.Active = _session;

            _session.Header(DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"), Application.version, BuildStamp.Commit,
                            Application.platform.ToString(), SystemInfo.deviceModel, DefineSet());
            if (app.Router != null) app.Router.Shown += OnShown;
            if (app.Panels != null) app.Panels.Shown += OnShown;
            Application.logMessageReceived += OnLog;
        }

        private void OnShown(ScreenId id)
        {
            try { _session.Screen(id.ToString()); } catch (Exception) { }
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            // Errors only. A log line the play log itself prints is never an error, so there is no loop.
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            try { _session.Error(type + ": " + condition); } catch (Exception) { }
        }

        /// <summary>Purchases the debug store finishes (bought) or the store fails (refused). The refused attempts of
        /// PurchaseFlow.Buy itself arrive through PlayLogHooks.Purchase.</summary>
        public void WatchPurchases(Emberfall.Core.Progression.PurchaseFlow flow)
        {
            if (flow == null) return;
            flow.Finished += delegate (Emberfall.Core.Progression.PurchaseResult r)
            {
                _session.Purchase(r.OfferId, r.Success, r.Success ? "" : r.Reason.ToString());
            };
            flow.Failed += delegate (string offerId, string message)
            {
                _session.Purchase(offerId, false, message);
            };
        }

        /// <summary>The summary line, at pause and at quit. Cumulative, so the last one in a session is the total.</summary>
        public static void OnPause(bool paused)
        {
            if (Instance == null) return;
            if (paused) Instance.WriteSummary();
            else Instance._session.Resume();
        }

        public static void OnQuit()
        {
            if (Instance != null) Instance.WriteSummary();
        }

        private void WriteSummary()
        {
            try { _session.Summary(); } catch (Exception) { }
        }

        private static string DefineSet()
        {
            System.Collections.Generic.List<string> d = new System.Collections.Generic.List<string>();
#if EMBERFALL_PLAYLOG
            d.Add("EMBERFALL_PLAYLOG");
#endif
#if EMBERFALL_TESTSTORE
            d.Add("EMBERFALL_TESTSTORE");
#endif
#if EMBERFALL_STORE_LIVE
            d.Add("EMBERFALL_STORE_LIVE");
#endif
#if EMBERFALL_STORE_OFF
            d.Add("EMBERFALL_STORE_OFF");
#endif
#if EMBERFALL_UNITY_IAP
            d.Add("EMBERFALL_UNITY_IAP");
#endif
#if EMBERFALL_TELEMETRY_OFF
            d.Add("EMBERFALL_TELEMETRY_OFF");
#endif
#if DEVELOPMENT_BUILD
            d.Add("DEVELOPMENT_BUILD");
#endif
            return string.Join(";", d.ToArray());
        }

        // ------------------------------------------------------------------ sharing

        /// <summary>The text the Share button hands over: the last session summary and the tail of the file.</summary>
        public string ShareText()
        {
            return PlayLogRules.ShareText(_writer.ReadAll(), PlayLogRules.MaxShareChars);
        }

        public string FolderPath { get { return Path.GetDirectoryName(_writer.FilePath); } }

        /// <summary>Android: the system share sheet with the text (ACTION_SEND, text/plain, no file provider needed).
        /// Windows and the editor: the folder in the file manager. Returns a short sentence for the toast.</summary>
        public string Share()
        {
            try
            {
                _session.Summary();
#if UNITY_ANDROID && !UNITY_EDITOR
                using (AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent"))
                using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                    intent.Call<AndroidJavaObject>("setType", "text/plain");
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_SUBJECT"), "Emberfall play log");
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), ShareText());
                    using (AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Share play log"))
                    using (AndroidJavaClass unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        activity.Call("startActivity", chooser);
                    }
                }
                return "Choose where to send the play log.";
#else
                string folder = FolderPath;
                if (string.IsNullOrEmpty(folder)) return "No play log folder.";
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                System.Diagnostics.Process.Start("explorer.exe", "\"" + folder.Replace('/', '\\') + "\"");
#else
                Application.OpenURL("file://" + folder);
#endif
                return "Opened the folder. Send emberfall_playlog.txt.";
#endif
            }
            catch (Exception ex)
            {
                return "Could not share the play log: " + PlayLogRules.Clean(ex.Message, 80);
            }
        }

        // ------------------------------------------------------------------ the smoke test

        private const float CloudWaitSeconds = 60f;
        private const float ScreenWaitSeconds = 30f;

        /// <summary>One smoke line: Player.log and the play log, both grep-able by "[smoke]".</summary>
        private void Say(string line)
        {
            Debug.Log(line);
            try { _session.Note(line); } catch (Exception) { }
        }

        private IEnumerator SmokeRoutine()
        {
            bool ok = true;
            Say("[smoke] start (throw-away identity: scratch-smoke folder, own session; the real save and session are not used)");
            float t0 = Time.realtimeSinceStartup;
            while (_app.Cloud == null && Time.realtimeSinceStartup - t0 < 10f) yield return null;

            CloudCoordinator cloud = _app.Cloud;
            if (cloud == null)
            {
                ok = false;
                Say("[smoke] cloud FAILED (status NotConfigured, no cloud stack in this build)");
            }
            else
            {
                t0 = Time.realtimeSinceStartup;
                while (cloud.Status != CloudStatus.Ready && cloud.Status != CloudStatus.NotConfigured
                       && cloud.Status != CloudStatus.Offline && Time.realtimeSinceStartup - t0 < CloudWaitSeconds)
                    yield return null;
                if (cloud.Status == CloudStatus.Ready)
                {
                    string uid = cloud.Account != null && cloud.Account.State != null ? cloud.Account.State.UserId : "";
                    if (uid == null) uid = "";
                    string prefix = uid.Length > 8 ? uid.Substring(0, 8) : uid;
                    if (cloud.Account.State.IsAnonymous) Say("[smoke] cloud signed in anonymously (status Ready)");
                    else Say("[smoke] cloud signed in but the account is not anonymous (status Ready)");
                    Say("[smoke] user id prefix " + prefix);
                }
                else
                {
                    ok = false;
                    Say("[smoke] cloud FAILED (status " + cloud.Status + ": " + PlayLogRules.Clean(cloud.Message, 80) + ")");
                }
            }

            FeatureGates.SmokeBypass = true;
            bool friends = false, guild = false;
            yield return SmokeScreen(ScreenId.Friends, "friends", delegate (bool r) { friends = r; });
            yield return SmokeScreen(ScreenId.Guild, "guild", delegate (bool r) { guild = r; });
            FeatureGates.SmokeBypass = false;
            if (!friends || !guild) ok = false;

            Say("[smoke] done " + (ok ? "ok" : "with failures"));
            WriteSummary();
            yield return null;
            Application.Quit(ok ? 0 : 1);
        }

        private IEnumerator SmokeScreen(ScreenId id, string word, Action<bool> done)
        {
            _app.Router.Show(id);
            yield return null;
            if (_app.Router.Current == null || _app.Router.Current.Id != id)
            {
                Say("[smoke] " + word + " screen NOT opened");
                done(false);
                yield break;
            }
            float t0 = Time.realtimeSinceStartup;
            bool timedOut = false;
            while (Loading(_app.Router.Current))
            {
                if (Time.realtimeSinceStartup - t0 > ScreenWaitSeconds) { timedOut = true; break; }
                yield return null;
            }
            string status = StatusOf(_app.Router.Current);
            if (timedOut) Say("[smoke] " + word + " screen TIMED OUT (status " + status + ")");
            else Say("[smoke] " + word + " screen loaded (status " + status + ")");
            done(!timedOut);
        }

        private static bool Loading(UIScreen screen)
        {
            Emberfall.UI.Screens.FriendsScreen f = screen as Emberfall.UI.Screens.FriendsScreen;
            if (f != null) return f.IsLoading;
            Emberfall.UI.Screens.GuildScreen g = screen as Emberfall.UI.Screens.GuildScreen;
            return g != null && g.IsLoading;
        }

        private static string StatusOf(UIScreen screen)
        {
            Emberfall.UI.Screens.FriendsScreen f = screen as Emberfall.UI.Screens.FriendsScreen;
            if (f != null) return PlayLogRules.Clean(f.StatusText, 60);
            Emberfall.UI.Screens.GuildScreen g = screen as Emberfall.UI.Screens.GuildScreen;
            return g != null ? PlayLogRules.Clean(g.StatusText, 60) : "";
        }
    }
}
#endif
