#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Emberfall.Game;
using Emberfall.UI;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Session;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// Drives a real Play mode session to a specific screen and captures a screenshot - for a
    /// verification pass that wants more than a compile check and a DevHarness run, run
    /// unattended rather than requiring someone to click through by hand.
    ///
    /// Two ways to trigger it, because it has been run both ways: from the command line against a
    /// closed editor (-executeMethod Emberfall.EditorTools.PlayModeScreenshot.CaptureEvents), and
    /// via its keyboard shortcut against an already-open, already-loaded live session (Ctrl+Shift+E
    /// for Events, Ctrl+Shift+R for Recital) - the second exists specifically because entering Play
    /// Mode requires the target window to actually be focused for input to reach it, and an
    /// external process cannot focus a window it did not create; something that CAN already reach
    /// the focused window (an OS-level key send) still can.
    ///
    /// Writes to Tools/Screenshots/*.png and logs its own progress to the console, then exits Play
    /// mode on its own.
    ///
    /// State lives in SessionState rather than an ordinary static field, and deliberately so:
    /// entering Play Mode triggers a domain reload by default (Edit > Project Settings > Editor >
    /// Enter Play Mode Options), which wipes every plain static field AND unsubscribes any
    /// EditorApplication.update delegate registered before the reload - exactly the subscription
    /// this automation depends on to keep running once Play actually begins. First version of this
    /// tool learned that the hard way: it hung forever in Play Mode with nothing driving it further,
    /// because CaptureEvents subscribed Pump and then the very next thing it did - entering Play
    /// Mode - erased that subscription. [InitializeOnLoad] re-subscribes on every domain load,
    /// including the one Play Mode itself triggers; SessionState is what lets the frame counter,
    /// which target is running, and "have I populated yet" survive that reload instead of silently
    /// resetting.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeScreenshot
    {
        private const string ActiveKey = "PlayModeScreenshot.Active";
        private const string TargetKey = "PlayModeScreenshot.Target";
        private const string FrameKey = "PlayModeScreenshot.Frame";
        private const string StageKey = "PlayModeScreenshot.Stage";

        /// <summary>
        /// Poll target for an external, file-based trigger: an already-open Editor session cannot
        /// be reached by -executeMethod (that only works launching a fresh process), and this
        /// project's keyboard-shortcut binding did not fire reliably when sent via SendKeys against
        /// the live window. A file is the one signal that reaches already-running code with no UI
        /// interaction needed at all - Pump is already subscribed and already ticking, so it only
        /// has to notice the file exists. Delete-after-reading so one drop triggers exactly one run.
        /// </summary>
        private const string TriggerPath = "Tools/Screenshots/.trigger";

        /// <summary>
        /// Bump this string on every real edit to this file and log it unconditionally on load
        /// (below) - the only reliable way found to confirm a fix actually finished compiling and
        /// domain-reloading into an already-open live session BEFORE trusting a trigger file drop
        /// to use it. A trigger file sitting on disk while a background compile is still running
        /// gets consumed by the OLD assembly, which keeps ticking Pump/PollTriggerFile for the
        /// entire multi-second compile window - Unity does not pause the Update loop just because
        /// a recompile is in flight, it swaps assemblies only once the domain reload after it
        /// completes. Placing a trigger without first confirming this marker is exactly what
        /// produced two more wasted runs against stale code on this feature's own verification pass.
        /// </summary>
        private const string BuildMarker = "FIXV7-proving-plus-screen-targets";

        static PlayModeScreenshot()
        {
            Debug.Log("PlayModeScreenshot: loaded, build marker " + BuildMarker);
            EditorApplication.update += Pump;
            EditorApplication.update += PollTriggerFile;
        }

        private static void PollTriggerFile()
        {
            if (SessionState.GetBool(ActiveKey, false)) return; // a run is already in progress
            string path = Path.Combine(Application.dataPath, "../" + TriggerPath);
            if (!File.Exists(path)) return;
            string requestedTarget = File.ReadAllText(path).Trim();
            File.Delete(path);
            Debug.Log("PlayModeScreenshot: trigger file found (build marker FIXV2), starting target '" + requestedTarget + "'.");
            Begin(string.IsNullOrEmpty(requestedTarget) ? "Events" : requestedTarget);
        }

        [MenuItem("Emberfall/Debug/Capture Events Screenshot %#e")]
        public static void CaptureEvents()
        {
            Begin("Events");
        }

        [MenuItem("Emberfall/Debug/Capture Recital Screenshot %#r")]
        public static void CaptureRecital()
        {
            Begin("Recital");
        }

        [MenuItem("Emberfall/Debug/Capture Proving Screenshot %#p")]
        public static void CaptureProving()
        {
            Begin("Proving");
        }
        // ------------------------------------------------------------------ generic screen targets
        // Seven screens, one code path. Each of these is a plain "show this screen and photograph
        // it" capture, which is all six of them need - unlike Events and Recital above, which have
        // to seed state first, and unlike Battle, which has to be handed a stage before it will
        // build a world.
        //
        // Added as menu items rather than only as trigger-file targets because the trigger route
        // depends on this process winning OS focus for Unity, which it has repeatedly failed to do
        // from a non-interactive shell. A menu item needs no focus fight: a human clicks it.

        [MenuItem("Emberfall/Capture/Hub (MainMenu)", false, 60)]
        public static void CaptureHubScreen() { Begin("Screen:MainMenu"); }

        [MenuItem("Emberfall/Capture/Heroes", false, 61)]
        public static void CaptureHeroes() { Begin("Screen:Heroes"); }

        [MenuItem("Emberfall/Capture/Summon", false, 62)]
        public static void CaptureSummon() { Begin("Screen:Summon"); }

        [MenuItem("Emberfall/Capture/Inventory", false, 63)]
        public static void CaptureInventory() { Begin("Screen:Inventory"); }

        [MenuItem("Emberfall/Capture/Relic Shop", false, 64)]
        public static void CaptureRelicShop() { Begin("Screen:RelicShop"); }

        [MenuItem("Emberfall/Capture/Events screen", false, 65)]
        public static void CaptureEventsScreen() { Begin("Screen:Events"); }

        [MenuItem("Emberfall/Capture/Recital screen", false, 66)]
        public static void CaptureRecitalScreen() { Begin("Screen:Recital"); }

        [MenuItem("Emberfall/Capture/Battle (live combat)", false, 67)]
        public static void CaptureBattleScreen() { Begin("Screen:Battle"); }

        [MenuItem("Emberfall/Capture/ALL of the above, in sequence", false, 80)]
        public static void CaptureAllScreens() { Begin("Screen:ALL"); }

        private static void Begin(string target)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(TargetKey, target);
            SessionState.SetInt(FrameKey, 0);
            SessionState.SetInt(StageKey, 0);
            EditorApplication.isPlaying = true;
        }

        /// <summary>
        /// Focuses and repaints the GameView pane via EditorWindow's own API - an in-process,
        /// pure-C# operation that does not need OS-level window focus at all, unlike everything
        /// this tool tried externally (SetForegroundWindow, the Alt-key bypass, AttachThreadInput,
        /// WScript.Shell.AppActivate - all unreliable from a non-interactive shell). Found to
        /// matter after three straight runs all produced screenExists=False/battleExists=False:
        /// ScreenCapture.CaptureScreenshot captures whatever the Game view last actually rendered,
        /// and a Game view pane that never repaints (because nothing is asking it to, with the OS
        /// window unfocused) has nothing to hand back. GameView is an internal UnityEditor type,
        /// hence reflection rather than a direct reference.
        /// </summary>
        private static void ForceGameViewRepaint()
        {
            Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null) return;
            EditorWindow window = EditorWindow.GetWindow(gameViewType, false, null, false);
            if (window == null) return;
            window.Focus();
            window.Repaint();
        }

        private static void Pump()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (!EditorApplication.isPlaying) return; // still transitioning in, or already stopped

            ForceGameViewRepaint();

            int frame = SessionState.GetInt(FrameKey, 0) + 1;
            SessionState.SetInt(FrameKey, frame);
            string target = SessionState.GetString(TargetKey, "Events");

            if (target == "Recital") PumpRecital(frame);
            else if (target == "Proving") PumpProving(frame);
            else if (target.StartsWith("Screen:")) PumpScreen(frame, target.Substring(7));
            else PumpEvents(frame);
        }

        private static void Stop()
        {
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.isPlaying = false;
        }

        private static string OutputPath(string fileName)
        {
            string path = Path.Combine(Application.dataPath, "../Tools/Screenshots/" + fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path)) File.Delete(path);
            return path;
        }

        // ------------------------------------------------------------------ Events target

        private static void PumpEvents(int frame)
        {
            int stage = SessionState.GetInt(StageKey, 0);

            if (stage == 0 && frame >= 10)
            {
                if (GameApp.Instance == null) { Debug.LogError("PlayModeScreenshot: GameApp.Instance never appeared."); Stop(); return; }
                PopulateEvents();
                SessionState.SetInt(StageKey, 1);
                stage = 1;
            }

            if (stage == 1 && frame == 40) Canvas.ForceUpdateCanvases();

            if (stage == 1 && frame == 45)
            {
                ScreenCapture.CaptureScreenshot(OutputPath("events_screen.png"), 1);
                Debug.Log("PlayModeScreenshot: captured events_screen.png");
                SessionState.SetInt(StageKey, 2);
            }

            if (stage == 2 && frame == 60) { Debug.Log("PlayModeScreenshot: done."); Stop(); }
        }

        /// <summary>
        /// Drives real GameSession activity - the same Summon/BuyRelic call sites a player's taps
        /// reach - rather than poking EventProgressEntry directly, so the screenshot is evidence
        /// the whole pipeline works, not just that the screen can render arbitrary numbers.
        /// </summary>
        private static void PopulateEvents()
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

        // ------------------------------------------------------------------ Recital target

        /// <summary>
        /// Two screenshots, not one: the Recital screen itself (floor, highest floor, Echoes,
        /// the cycle preview), then a real floor attempt caught mid-playback so the Aspect-band
        /// enemies actually on screen are visible, not just implied by the cycle strip. The battle
        /// is launched through the exact GameSession calls RecitalScreen.OnFight and
        /// BattleScreen.OnShow use (TryStartRecitalAttempt, SetRecitalFloor, Show(Battle)), so this
        /// is the real pipeline, not a shortcut around it.
        /// </summary>
        /// <summary>
        /// The Proving: its own screen, then a real match against the generated opponent.
        ///
        /// Mirrors PumpRecital deliberately, including the one thing that method's comment says it
        /// got wrong first: this PEEKS Brands and never spends them. BattleScreen.OnShow is the
        /// single spend point, and calling TryStartProvingAttempt here would spend one before
        /// OnShow spent a second for the same attempt - the double-spend that made the Recital
        /// capture bounce straight back to its own screen instead of ever fighting.
        ///
        /// Brands are topped up directly for exactly the reason PopulateRecitalLadder tops up
        /// Echoes and refuses to touch Gold: Proving fields did not exist on any save before this
        /// feature, so there is no prior real value here to protect. Standing, Valor and the
        /// pending opponent are left alone.
        /// </summary>
        private static void PumpProving(int frame)
        {
            int stage = SessionState.GetInt(StageKey, 0);
            GameApp app = GameApp.Instance;

            if (stage == 0 && frame >= 10)
            {
                if (app == null) { Debug.LogError("PlayModeScreenshot: GameApp.Instance never appeared."); Stop(); return; }
                int max = app.Session.Balance.ProvingBrandsMax;
                if (app.Session.Profile.ProvingBrandsBanked < max)
                {
                    app.Session.Profile.ProvingBrandsBanked = max;
                    app.Session.Profile.ProvingBrandsClockUnix = TimeUtil.NowUnix();
                }
                app.Router.Show(ScreenId.Proving);
                Debug.Log("PlayModeScreenshot: opened the Proving screen with " + max + " Brands.");
                SessionState.SetInt(StageKey, 1);
                stage = 1;
            }

            if (stage == 1 && frame == 60) Canvas.ForceUpdateCanvases();

            string screenPath = OutputPath("proving_screen.png");
            if (stage == 1 && frame == 70)
            {
                ScreenCapture.CaptureScreenshot(screenPath, 1);
                Debug.Log("PlayModeScreenshot: requested proving_screen.png");
                SessionState.SetInt(StageKey, 2);
            }

            if (stage == 2 && frame == 90)
            {
                long now = TimeUtil.NowUnix();
                if (app.Session.PeekProvingBrands(now).Banked > 0)
                {
                    BattleLoadout opponent = app.Session.CurrentProvingOpponent(now);
                    if (opponent != null && opponent.Units.Count > 0)
                    {
                        app.Router.Battle.SetProvingOpponent(opponent);
                        app.Router.Show(ScreenId.Battle);
                        Debug.Log("PlayModeScreenshot: launched a real Proving match against '"
                                + opponent.Label + "' (power " + opponent.PowerScore
                                + ", ghost=" + opponent.IsGhost + ").");
                    }
                    else Debug.LogWarning("PlayModeScreenshot: no opponent could be assembled.");
                }
                else Debug.LogWarning("PlayModeScreenshot: out of Brands, could not launch a match.");
                SessionState.SetInt(StageKey, 3);
            }

            // Frame 200, the same late landing PumpRecital had to move to: BuildWorld and
            // RunSimulation do not finish painting a freshly built 3D scene in the frame
            // Show(Battle) is called on, and an earlier capture catches solid black.
            string battlePath = OutputPath("proving_battle.png");
            if (stage == 3 && frame == 200)
            {
                ScreenCapture.CaptureScreenshot(battlePath, 1);
                Debug.Log("PlayModeScreenshot: requested proving_battle.png");
                SessionState.SetInt(StageKey, 4);
            }

            if (stage == 4)
            {
                bool bothExist = File.Exists(screenPath) && File.Exists(battlePath);
                if (bothExist || frame >= 400)
                {
                    Debug.Log("PlayModeScreenshot: done. screenExists=" + File.Exists(screenPath)
                            + " battleExists=" + File.Exists(battlePath));
                    Stop();
                }
            }
        }

        /// <summary>
        /// Every screen this project has, captured through one path: show it, let the canvas settle,
        /// photograph it, move on. "ALL" walks the whole list in one Play Mode session, which is the
        /// only way to get a comparable before/after set - eight separate sessions means eight
        /// separate domain reloads and eight chances for something to differ between shots.
        ///
        /// Battle is the one screen that needs more than Show(): BattleScreen.OnShow builds a 3D
        /// world and runs a simulation, so it is handed the player's next stage first and given the
        /// same long settle PumpRecital documents - BuildWorld and RunSimulation do not finish
        /// painting in the frame Show is called on, and an early capture catches solid black.
        ///
        /// Writes Tools/Screenshots/screen_{name}.png, one per screen.
        /// </summary>
        private static void PumpScreen(int frame, string which)
        {
            GameApp app = GameApp.Instance;
            if (app == null)
            {
                if (frame > 300) { Debug.LogError("PlayModeScreenshot: GameApp.Instance never appeared."); Stop(); }
                return;
            }

            string[] all = { "MainMenu", "Heroes", "Summon", "Inventory", "RelicShop", "Events", "Recital", "Battle" };
            string[] list = which == "ALL" ? all : new string[] { which };

            // Per screen: show at (index * Step), capture at (index * Step + Settle). Battle gets a
            // longer settle for the reason above, so the schedule is walked rather than computed.
            const int Step = 130;
            const int Settle = 90;

            int slot = SessionState.GetInt(StageKey, 0);
            if (slot >= list.Length)
            {
                if (frame > slot * Step + Settle + 40) { Debug.Log("PlayModeScreenshot: screen captures done."); Stop(); }
                return;
            }

            string name = list[slot];
            int showAt = 10 + slot * Step;
            int shotAt = showAt + Settle;

            if (frame == showAt)
            {
                ScreenId id = ScreenId.MainMenu;
                switch (name)
                {
                    case "Heroes": id = ScreenId.Heroes; break;
                    case "Summon": id = ScreenId.Summon; break;
                    case "Inventory": id = ScreenId.Inventory; break;
                    case "RelicShop": id = ScreenId.RelicShop; break;
                    case "Events": id = ScreenId.Events; break;
                    case "Recital": id = ScreenId.Recital; break;
                    case "Battle": id = ScreenId.Battle; break;
                }
                if (name == "Battle")
                {
                    StageDef next = app.Session.NextStage;
                    if (next == null)
                    {
                        Debug.LogWarning("PlayModeScreenshot: no campaign stage to fight, skipping Battle.");
                        SessionState.SetInt(StageKey, slot + 1);
                        return;
                    }
                    app.Router.Battle.SetStage(next);
                }
                app.Router.Show(id);
                Debug.Log("PlayModeScreenshot: showing " + name);
            }

            if (frame == shotAt - 20) Canvas.ForceUpdateCanvases();

            if (frame == shotAt)
            {
                ScreenCapture.CaptureScreenshot(OutputPath("screen_" + name + ".png"), 1);
                Debug.Log("PlayModeScreenshot: requested screen_" + name + ".png");
                SessionState.SetInt(StageKey, slot + 1);
            }
        }

        private static void PumpRecital(int frame)
        {
            int stage = SessionState.GetInt(StageKey, 0);
            GameApp app = GameApp.Instance;

            if (stage == 0 && frame >= 10)
            {
                if (app == null) { Debug.LogError("PlayModeScreenshot: GameApp.Instance never appeared."); Stop(); return; }
                PopulateRecitalLadder(app);
                app.Router.Show(ScreenId.Recital);
                Debug.Log("PlayModeScreenshot: climbed the Recital ladder and opened the Recital screen.");
                SessionState.SetInt(StageKey, 1);
                stage = 1;
            }

            if (stage == 1 && frame == 60) Canvas.ForceUpdateCanvases();

            string screenPath = OutputPath("recital_screen.png");
            if (stage == 1 && frame == 70)
            {
                ScreenCapture.CaptureScreenshot(screenPath, 1);
                Debug.Log("PlayModeScreenshot: requested recital_screen.png");
                SessionState.SetInt(StageKey, 2);
            }

            if (stage == 2 && frame == 90)
            {
                int floor = app.Session.Profile.RecitalCurrentFloor;
                // Peek only, exactly like RecitalScreen.OnFight - BattleScreen.OnShow is the one
                // and only place an Echo is actually spent (it runs next, from Show(Battle) below).
                // The first version of this method called TryStartRecitalAttempt here directly,
                // which spent an Echo BEFORE BattleScreen.OnShow spent a second one for the same
                // attempt - the exact double-spend RecitalScreen's real code avoids, reproduced by
                // this test script instead. That is what actually happened on the first live run:
                // it emptied the bank one call early, so BattleScreen's own gate then correctly
                // refused and bounced back to the Recital screen instead of ever fighting.
                if (app.Session.PeekRecitalEchoes(TimeUtil.NowUnix()).Banked > 0)
                {
                    app.Router.Battle.SetRecitalFloor(floor);
                    app.Router.Show(ScreenId.Battle);
                    Debug.Log("PlayModeScreenshot: launched a real floor " + floor + " attempt through BattleScreen.");
                }
                else
                {
                    Debug.LogWarning("PlayModeScreenshot: out of Echoes, could not launch a floor attempt.");
                }
                SessionState.SetInt(StageKey, 3);
            }

            // Mid-playback, well after RunSimulation has built the world and bars - the first live
            // run of this capture landed here too early (frame 75, ~20 frames after the battle
            // launched) and caught a solid black frame, because BuildWorld/RunSimulation and the
            // first real render of a freshly constructed 3D scene do not complete in the same frame
            // Show(Battle) is called on. Pushed much later to give the scene time to actually paint.
            string battlePath = OutputPath("recital_battle.png");
            if (stage == 3 && frame == 200)
            {
                ScreenCapture.CaptureScreenshot(battlePath, 1);
                Debug.Log("PlayModeScreenshot: requested recital_battle.png");
                SessionState.SetInt(StageKey, 4);
            }

            // Stop is gated on both files actually existing on disk, not a fixed frame count -
            // ScreenCapture.CaptureScreenshot writes asynchronously, and the first live run of this
            // tool lost recital_screen.png entirely because Stop (which exits Play Mode, which
            // triggers a domain reload) fired before that write had finished landing on disk. A
            // generous frame ceiling (400) still exists as a safety net against hanging forever if
            // a write genuinely never completes.
            if (stage == 4)
            {
                bool bothExist = File.Exists(screenPath) && File.Exists(battlePath);
                if (bothExist || frame >= 400)
                {
                    Debug.Log("PlayModeScreenshot: done. screenExists=" + File.Exists(screenPath) + " battleExists=" + File.Exists(battlePath));
                    Stop();
                }
            }
        }

        /// <summary>Climbs several real floors via TryStartRecitalAttempt/GrantRecitalResult - the
        /// same two calls the real UI makes - so RecitalHighestFloor, the Echo bank and the cycle
        /// position all reflect genuine session state rather than being poked directly.
        ///
        /// Touches ONLY Recital fields (Echoes, floor) - never Gold/Shards/RelicDust or anything
        /// else pre-existing on the profile. Those are real, earned currency on whatever save this
        /// runs against (this automation is explicitly meant to run against an already-open live
        /// session, not an isolated copy), and exiting Play Mode persists whatever is in memory via
        /// OnApplicationQuit -> SaveNow. The first version of this method set Profile.Gold directly
        /// for convenience and it went straight to disk, overwriting a real balance - do not repeat
        /// that. Echoes are topped up directly here for the same reason Gold must NOT be: Recital
        /// fields did not exist on this save before this feature shipped, so there is no prior real
        /// value here to protect - only floor position and Echo count, both created by this feature.
        /// </summary>
        private static void PopulateRecitalLadder(GameApp app)
        {
            GameSession session = app.Session;
            long now = TimeUtil.NowUnix();
            session.Profile.RecitalEchoesBanked = session.Balance.RecitalEchoesMax;
            session.Profile.RecitalEchoesClockUnix = now;

            for (int i = 0; i < 4; i++)
            {
                int floor = session.Profile.RecitalCurrentFloor;
                if (!session.TryStartRecitalAttempt(now)) break;
                session.GrantRecitalResult(floor, true, now);
            }

            // Leave a full bank behind for the battle stage that follows, rather than whatever
            // remainder the climb above left - RecitalScreen.OnFight only peeks affordability and
            // BattleScreen.OnShow is the sole real spend point (see PumpRecital below), so this
            // must NOT itself call TryStartRecitalAttempt - that would be the exact double-spend
            // bug this comment exists to not repeat a second way.
            session.Profile.RecitalEchoesBanked = session.Balance.RecitalEchoesMax;

            app.RefreshHud();
        }
    }
}
#endif
