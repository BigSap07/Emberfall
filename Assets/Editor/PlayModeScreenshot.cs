#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Emberfall.Game;
using Emberfall.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Session;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// Drives a real Play mode session to a specific screen and captures a screenshot - for a
    /// verification pass that wants more than a compile check and a DevHarness run, run
    /// unattended from the command line rather than requiring someone to click through by hand.
    ///
    /// Usage: Unity.exe -projectPath &lt;path&gt; -executeMethod Emberfall.EditorTools.PlayModeScreenshot.CaptureEvents
    /// Writes to Tools/Screenshots/events_screen.png and logs its own progress to the console
    /// (visible in -logFile output), then exits Play mode on its own.
    ///
    /// State lives in SessionState rather than an ordinary static field, and deliberately so:
    /// entering Play Mode triggers a domain reload by default (Edit > Project Settings > Editor >
    /// Enter Play Mode Options), which wipes every plain static field AND unsubscribes any
    /// EditorApplication.update delegate registered before the reload - exactly the subscription
    /// this automation depends on to keep running once Play actually begins. First version of this
    /// tool learned that the hard way: it hung forever in Play Mode with nothing driving it further,
    /// because CaptureEvents subscribed Pump and then the very next thing it did - entering Play
    /// Mode - erased that subscription. [InitializeOnLoad] re-subscribes on every domain load,
    /// including the one Play Mode itself triggers; SessionState is what lets the frame counter and
    /// "have I populated yet" flag survive that reload instead of silently resetting to zero.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeScreenshot
    {
        private const string OutputRelativePath = "Tools/Screenshots/events_screen.png";
        private const string ActiveKey = "PlayModeScreenshot.Active";
        private const string FrameKey = "PlayModeScreenshot.Frame";
        private const string PopulatedKey = "PlayModeScreenshot.Populated";

        static PlayModeScreenshot()
        {
            EditorApplication.update += Pump;
        }

        [MenuItem("Emberfall/Debug/Capture Events Screenshot")]
        public static void CaptureEvents()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(FrameKey, 0);
            SessionState.SetBool(PopulatedKey, false);
            EditorApplication.isPlaying = true;
        }

        private static void Pump()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (!EditorApplication.isPlaying) return; // still transitioning in, or already stopped

            int frame = SessionState.GetInt(FrameKey, 0) + 1;
            SessionState.SetInt(FrameKey, frame);
            bool populated = SessionState.GetBool(PopulatedKey, false);

            if (!populated && frame >= 10)
            {
                if (GameApp.Instance == null)
                {
                    Debug.LogError("PlayModeScreenshot: GameApp.Instance never appeared by frame " + frame + ".");
                    Stop();
                    return;
                }
                Populate();
                SessionState.SetBool(PopulatedKey, true);
                populated = true;
            }

            if (populated && frame == 40)
            {
                Canvas.ForceUpdateCanvases();
            }

            if (populated && frame == 45)
            {
                string outputPath = Path.Combine(Application.dataPath, "../" + OutputRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                if (File.Exists(outputPath)) File.Delete(outputPath);
                ScreenCapture.CaptureScreenshot(outputPath, 1);
                Debug.Log("PlayModeScreenshot: requested capture to " + outputPath);
            }

            if (populated && frame == 60)
            {
                Debug.Log("PlayModeScreenshot: done.");
                Stop();
            }
        }

        private static void Stop()
        {
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.isPlaying = false;
        }

        /// <summary>
        /// Drives real GameSession activity - the same Summon/BuyRelic call sites a player's taps
        /// reach - rather than poking EventProgressEntry directly, so the screenshot is evidence
        /// the whole pipeline works, not just that the screen can render arbitrary numbers.
        /// </summary>
        private static void Populate()
        {
            GameApp app = GameApp.Instance;
            GameSession session = app.Session;
            ContentDatabase content = session.Content;
            long now = TimeUtil.NowUnix();

            session.Profile.Shards = 100000;
            session.Profile.RelicDust = 100000;

            EventDef rite = content.GetEvent("recollection_rite");
            if (rite != null)
                for (int i = 0; i < 6; i++) session.Summon(90000 + i, now);

            EventDef ledger = content.GetEvent("ledgers_week");
            if (ledger != null)
            {
                session.BuyRelic("warband_standard", now);
                session.BuyRelic("bastion_core", now);
            }

            if (rite != null && rite.Milestones.Count > 0)
                session.ClaimEventMilestone(rite.Id, rite.Milestones[0].Threshold);

            if (ledger != null && ledger.CurrencyRewards.Count > 0)
                session.RedeemEventCurrency(ledger.Id, ledger.CurrencyRewards[0].Cost);

            app.RefreshHud();
            app.Router.Show(ScreenId.Events);

            Debug.Log("PlayModeScreenshot: populated event progress and opened the Events screen.");
        }
    }
}
#endif
