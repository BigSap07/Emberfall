#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Emberfall.Battle;
using Emberfall.Game;
using Emberfall.UI;
using Emberfall.UI.Screens;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;
using Emberfall.Core.Progression;

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
        private const string LastShotKey = "PlayModeScreenshot.LastShot";
        private const string BattleArmKey = "PlayModeScreenshot.BattleArm";
        private const string WidthKey = "PlayModeScreenshot.Width";
        private const string HeightKey = "PlayModeScreenshot.Height";
        private const string SuffixKey = "PlayModeScreenshot.Suffix";
        private const string VerifiedKey = "PlayModeScreenshot.Verified";
        /// <summary>The campaign stage a Battle target fights instead of Session.NextStage, or
        /// empty for NextStage. StageKey is the schedule's slot index, not this.</summary>
        private const string BattleStageKey = "PlayModeScreenshot.BattleStage";

        /// <summary>
        /// Every run of this tool is on a scratch identity with the cloud held off, unconditionally:
        /// Begin swaps to it (unless a tool above, ScratchCaptures, already did) and enters Play
        /// mode through EditorCaptureGuard, which arms the forced-offline switch. The real save is
        /// not written and the real account is not signed in to, whatever files the worktree holds.
        /// </summary>
        private const string ScratchProduct = "Emberfall Screenshots";

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
        private const string BuildMarker = "FIXV21-wave-results-merged";

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
        // Thirteen screens, one code path. Most of these are a plain "show this screen and
        // photograph it" capture, which is all they need - unlike Events and Recital above, which
        // have to seed state first, and unlike Battle and Hero detail, which have to be handed a
        // stage and a hero respectively before either will build anything.
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

        [MenuItem("Emberfall/Capture/Battle result", false, 73)]
        public static void CaptureBattleResult() { Begin("Screen:Result"); }

        // The wave result screen, one target per outcome it can show. Each seeds the scratch
        // profile through GameSession, then starts the wave through CampaignFlow.Launch, the same
        // door a tap uses. They refuse to run off a scratch identity: see SeedWaveResult.
        [MenuItem("Emberfall/Capture/Wave result (wave cleared)", false, 82)]
        public static void CaptureWaveResult() { Begin("Screen:WaveResult"); }

        // The same won wave with the player's AUTO setting on, so the countdown digit is in shot.
        [MenuItem("Emberfall/Capture/Wave result (wave cleared, AUTO on)", false, 82)]
        public static void CaptureWaveResultAuto() { Begin("Screen:WaveResultAuto"); }

        [MenuItem("Emberfall/Capture/Wave result (stage cleared)", false, 83)]
        public static void CaptureWaveResultCleared() { Begin("Screen:WaveResultCleared"); }

        [MenuItem("Emberfall/Capture/Wave result (lost)", false, 84)]
        public static void CaptureWaveResultLost() { Begin("Screen:WaveResultLost"); }

        // The same won wave as the first of the three, then WATCH pressed: one frame of the replay
        // on the battle screen, frozen while a hit effect is alive. See PumpWaveReplay.
        [MenuItem("Emberfall/Capture/Wave replay (mid-heal)", false, 86)]
        public static void CaptureWaveReplayHeal() { Begin("Screen:WaveReplayHeal"); }

        [MenuItem("Emberfall/Capture/Wave replay (mid-fight)", false, 85)]
        public static void CaptureWaveReplay() { Begin("Screen:WaveReplay"); }

        // Formation and Hero detail are the two portrait-dense screens that had no target here,
        // which is how a portrait delivery could have been called verified without either being
        // photographed. Both need a little more than Show(): the formation screen is the roster
        // laid out against the deployed line and reads correctly from a fresh profile, while the
        // detail screen is ABOUT one hero and renders nothing until it is handed one, so the
        // driver picks the first owned hero rather than leaving it on whatever the last caller
        // set. Neither fakes state - both show what a real profile at this point actually holds.
        [MenuItem("Emberfall/Capture/Formation", false, 71)]
        public static void CaptureFormationScreen() { Begin("Screen:Formation"); }

        [MenuItem("Emberfall/Capture/Hero detail", false, 72)]
        public static void CaptureHeroDetailScreen() { Begin("Screen:HeroDetail"); }

        [MenuItem("Emberfall/Capture/Talents", false, 74)]
        public static void CaptureTalentsScreen() { Begin("Screen:Talents"); }

        // Two entries for one screen, because the fusion page's two tabs are two layouts that
        // share only their header and their action bar.
        [MenuItem("Emberfall/Capture/Fusion (ascend)", false, 75)]
        public static void CaptureFusionScreen() { Begin("Screen:Fusion"); }

        [MenuItem("Emberfall/Capture/Fusion (convert)", false, 76)]
        public static void CaptureFusionConvertScreen() { Begin("Screen:FusionConvert"); }

        // The three account screens, through the same generic path. What this captures is each
        // one's OFFLINE state - the router builds them whether or not a cloud exists, and with no
        // network they render "no account service" or "offline" rather than a blank. That state is
        // worth photographing in its own right, because it is what every build with no
        // supabase.json shows and it is the one this project's offline promise is about.
        //
        // The SIGNED-IN states are not reachable from here and deliberately are not faked: they
        // need a live account, a second live account, and a friendship between them, which is
        // AccountsVerification's job and which no synthetic seeding could stand in for without
        // photographing something no server ever returned.
        [MenuItem("Emberfall/Capture/Account", false, 68)]
        public static void CaptureAccountScreen() { Begin("Screen:Account"); }

        [MenuItem("Emberfall/Capture/Friends", false, 69)]
        public static void CaptureFriendsScreen() { Begin("Screen:Friends"); }

        [MenuItem("Emberfall/Capture/Friend's team", false, 70)]
        public static void CaptureFriendTeamScreen() { Begin("Screen:FriendTeam"); }

        [MenuItem("Emberfall/Capture/ALL of the above, in sequence", false, 80)]
        public static void CaptureAllScreens() { Begin("Screen:ALL"); }

        /// <summary>
        /// What a capture from this tool is framed at by default. Pinned rather than inherited,
        /// because the logical canvas is a function of ASPECT (see GameViewSize) - an unpinned
        /// capture only proves the layout works at whatever size the Game view was last dragged
        /// to, which is the caveat Tools/capture-hub.sh already states for the Hub and which
        /// applied silently to every screen this tool photographed. 1920x1080 is ProjectSettings'
        /// own Android resolution and the reference UITheme lays out against.
        /// </summary>
        private const int CaptureWidth = 1920;
        private const int CaptureHeight = 1080;

        /// <summary>
        /// The OTHER aspect every screen has to be photographed at, and the reason this tool
        /// takes a size at all now.
        ///
        /// 16:9 is the editor's shape and 20:9 is the phone's, and the canvas scaler blends the
        /// two - so a 20:9 device gets a logical canvas about 2147x966, which is 114 units
        /// SHORTER than the 1920x1080 an editor capture proves. DESIGN-EMBERFORGE records three
        /// sections of the component gallery that overflowed their panels at 20:9 and nowhere
        /// else. A screen photographed at one aspect is a screen that has been half checked.
        /// </summary>
        private const int WideWidth = 2400;
        private const int WideHeight = 1080;

        [MenuItem("Emberfall/Capture/ALL of the above, at 20:9", false, 81)]
        public static void CaptureAllScreensWide() { Begin("Screen:ALL@" + WideWidth + "x" + WideHeight); }

        /// <summary>
        /// Starts a run. The target may carry its own frame size and its own file suffix:
        ///
        ///     Screen:Summon                  1920x1080, writes screen_Summon.png
        ///     Screen:Summon@2400x1080        2400x1080, writes screen_Summon@2400x1080.png
        ///     Screen:Battle=2-6              fights stage 2-6, writes screen_Battle=2-6.png
        ///
        /// The stage exists because Battle otherwise fights Session.NextStage, which is wherever
        /// the scratch profile happens to stand, so a capture of the art on a chosen enemy line
        /// (chapter 2's elites, a boss) had no way to ask for it. It rides in the file name for
        /// the size's reason: a shot that does not say what it fought can be filed as another.
        ///
        /// The size rides on the target rather than sitting in a second trigger file because the
        /// trigger is one file with one line in it, and an aspect that had to be set separately
        /// is an aspect that can be forgotten between the two writes - which photographs one
        /// shape and files it as the other.
        /// </summary>
        private static void Begin(string target)
        {
            int width = CaptureWidth;
            int height = CaptureHeight;
            string suffix = "";

            int at = target.LastIndexOf('@');
            if (at > 0)
            {
                string size = target.Substring(at + 1);
                target = target.Substring(0, at);
                string[] parts = size.Split('x');
                int w, h;
                if (parts.Length == 2 && int.TryParse(parts[0], out w) && int.TryParse(parts[1], out h)
                    && w > 0 && h > 0)
                {
                    width = w;
                    height = h;
                    suffix = "@" + w + "x" + h;
                }
                else
                {
                    Debug.LogWarning("PlayModeScreenshot: '" + size + "' is not a WxH size, so this"
                                     + " run uses the default " + CaptureWidth + "x" + CaptureHeight
                                     + ". A capture at an unintended aspect proves nothing about"
                                     + " the one that was asked for.");
                }
            }

            string battleStage = "";
            int eq = target.IndexOf('=');
            if (eq > 0)
            {
                battleStage = target.Substring(eq + 1);
                target = target.Substring(0, eq);
                suffix = "=" + battleStage + suffix;
            }

            if (!EditorScratchIdentity.Enter(ScratchProduct))
            {
                Debug.LogError("PlayModeScreenshot: could not switch to the scratch identity. "
                               + "Refusing to enter Play mode on the real one.");
                return;
            }

            if (!GameViewSize.Apply(width, height))
            {
                Debug.LogWarning("PlayModeScreenshot: could not pin the Game view to "
                                 + width + "x" + height + "; the capture will use"
                                 + " whatever size it is currently at and its layout is unproven.");
            }
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(VerifiedKey, false);
            SessionState.SetString(TargetKey, target);
            SessionState.SetInt(WidthKey, width);
            SessionState.SetInt(HeightKey, height);
            SessionState.SetString(SuffixKey, suffix);
            SessionState.SetString(BattleStageKey, battleStage);
            SessionState.SetInt(FrameKey, 0);
            SessionState.SetInt(StageKey, 0);
            // Both of these survive a domain reload, so a second run in the same editor session
            // would inherit the first run's arming frame and shoot immediately if they were left.
            SessionState.SetInt(LastShotKey, 0);
            SessionState.SetInt(BattleArmKey, 0);
            EditorCaptureGuard.EnterPlayMode();
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

            // Once per run, as soon as the game is up: refuse to photograph a session that could
            // reach the real account or write the real save.
            if (!SessionState.GetBool(VerifiedKey, false) && (GameApp.Instance != null || frame >= 9))
            {
                SessionState.SetBool(VerifiedKey, true);
                if (!EditorCaptureGuard.Verify("PlayModeScreenshot", true))
                {
                    Debug.LogError("PlayModeScreenshot: the run is not offline on a scratch identity. Stopping without a capture.");
                    Stop();
                    return;
                }
            }

            if (target == "Recital") PumpRecital(frame);
            else if (target == "Proving") PumpProving(frame);
            else if (target.StartsWith("Screen:")) PumpScreen(frame, target.Substring(7));
            else PumpEvents(frame);
        }

        private static void Stop()
        {
            Time.timeScale = 1f;
            SessionState.SetInt(ReplayStageKey, 0);
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.isPlaying = false;
            EditorScratchIdentity.LeaveWhenIdle();
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
                session.ClaimEventMilestone(rite.Id, rite.Milestones[0].Id);

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

            // Every page the router owns, so "ALL" means all of them rather than the thirteen
            // that happened to have a target when this list was last written. The eight added
            // here - Campaign, Forge, Settings, Profile, Guild and its two sub-pages, Proving -
            // are pages a player reaches by tapping, and a page with no capture target is a page
            // whose layout nothing in this repository has ever looked at.
            string[] all = { "MainMenu", "Campaign", "Heroes", "HeroDetail", "Talents", "Formation", "Summon",
                             "Forge", "Inventory", "RelicShop", "Events", "Recital", "Proving",
                             "Profile", "Account", "Friends", "FriendTeam", "Settings",
                             "Fusion", "FusionConvert",
                             "Guild", "GuildRequests", "GuildChat", "Battle", "Result",
                             "Claim", "Sheet", "Menu", "Gallery", "Dialog", "Toast", "ToastLong" };
            string[] list = which == "ALL" ? all : new string[] { which };

            // Per screen: show at (index * Step), capture at (index * Step + its settle).
            const int Step = 130;
            const int Settle = 90;

            // BATTLE IS THE ONE SCREEN A FRAME COUNT CANNOT TIME, and the comment above this
            // method has claimed for a while that it gets a longer settle than the rest. It did
            // not - every screen shared one constant - and the capture that caught it is why this
            // is a condition now rather than a number. At 90 frames the battle screen has painted,
            // the battle-start passives are mid-banner and the round label still reads PREPARING,
            // so THE TURN STRIP HAS NOT BEEN BUILT YET: the strip is rebuilt on RoundStart, and a
            // capture taken before the first round photographs a battlefield with no strip on it
            // and files that as the battle HUD.
            //
            // Raising the number did not fix it and could not. How long the opening runs is a
            // property of WHICH FIVE HEROES ARE DEPLOYED, because every BattleStart passive they
            // carry banners before the first round; 330 frames, five and a half seconds, was still
            // inside the preamble for the starting five. So Battle waits on
            // BattleScreen.RoundUnderway instead, which is the screen answering for itself.
            //
            // A floor and a ceiling are both still needed. The floor is Settle, because a freshly
            // built 3D scene does not paint in the frame Show is called on and an early shot
            // catches solid black. The ceiling stops a hang waiting forever: if no round ever
            // starts it shoots anyway, and that screenshot is itself the evidence.
            const int BattleWaitCeiling = 2400;
            const int BattlePaintDelay = 20;

            int slot = SessionState.GetInt(StageKey, 0);
            if (slot >= list.Length)
            {
                // Gated on the frame the LAST capture was requested on rather than on a frame
                // computed from the schedule - the schedule no longer has a fixed end, and
                // computing it is what stopped the previous run ten frames before its final write.
                if (frame > SessionState.GetInt(LastShotKey, 0) + 60)
                {
                    Debug.Log("PlayModeScreenshot: screen captures done.");
                    Stop();
                }
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
                    case "Campaign": id = ScreenId.Campaign; break;
                    case "Heroes": id = ScreenId.Heroes; break;
                    case "HeroDetail": id = ScreenId.HeroDetail; break;
                    case "Talents": id = ScreenId.Talents; break;
                    case "Fusion": id = ScreenId.Fusion; break;
                    case "FusionConvert": id = ScreenId.Fusion; break;
                    case "Formation": id = ScreenId.Formation; break;
                    case "Summon": id = ScreenId.Summon; break;
                    case "Forge": id = ScreenId.Forge; break;
                    case "Inventory": id = ScreenId.Inventory; break;
                    case "RelicShop": id = ScreenId.RelicShop; break;
                    case "Events": id = ScreenId.Events; break;
                    case "Recital": id = ScreenId.Recital; break;
                    case "Proving": id = ScreenId.Proving; break;
                    case "Profile": id = ScreenId.Profile; break;
                    case "Account": id = ScreenId.Account; break;
                    case "Friends": id = ScreenId.Friends; break;
                    case "FriendTeam": id = ScreenId.FriendTeam; break;
                    case "Settings": id = ScreenId.Settings; break;
                    case "Guild": id = ScreenId.Guild; break;
                    case "GuildRequests": id = ScreenId.GuildRequests; break;
                    case "GuildChat": id = ScreenId.GuildChat; break;
                    case "Battle": id = ScreenId.Battle; break;
                    case "Result": id = ScreenId.Battle; break;
                    case "WaveResult":
                    case "WaveResultAuto":
                    case "WaveResultCleared":
                    case "WaveResultLost":
                    case "WaveReplay":
                    case "WaveReplayResult":
                    case "WaveReplayHeal": id = ScreenId.WaveResult; break;
                    // The ability breakdown is a DIALOG over the hero page rather than a screen,
                    // so it routes to the page and opens the dialog a frame later, exactly as the
                    // Hub's layers do below.
                    case "AbilityNumbers": id = ScreenId.HeroDetail; break;
                }

                // THE HUB'S THREE LAYERS ABOVE THE MAP are targets of their own, because none of
                // them is a screen the router owns: a popup, a panel sheet and a menu are opened
                // BY the Hub, over it, and a capture schedule that only ever calls Router.Show
                // can photograph none of the three. MainMenuScreen.OpenLayerForCapture is the
                // door they already have, and it opens each one the way a tap does.
                // THE TOAST is not a screen either: it lands on top of whatever is showing. The Hub
                // is put up here and the message is sent just before the shot, below, because a
                // toast only holds for a second and a half.
                if (name == "Toast" || name == "ToastLong")
                {
                    app.Router.Show(ScreenId.MainMenu);
                    return;
                }
                if (name == "Claim" || name == "Sheet" || name == "Menu"
                    || name == "Gallery" || name == "Dialog")
                {
                    app.Router.Show(ScreenId.MainMenu);
                    OpenHubLayer(app, name);
                    Debug.Log("PlayModeScreenshot: opened the Hub layer for " + name);
                    return;
                }
                // HeroDetail is the one generic target that is about a SUBJECT rather than a
                // screen, so it is handed one the way HeroesScreen hands it one - the first hero
                // the profile actually owns, through the same SetHero the game calls. Skipped
                // rather than faked if the profile owns nobody: a detail page for a hero the
                // player does not have is a screenshot of something no player can reach.
                if (name == "HeroDetail" || name == "AbilityNumbers")
                {
                    List<HeroInstance> owned = app.Session.Profile.Heroes;
                    if (owned == null || owned.Count == 0)
                    {
                        Debug.LogWarning("PlayModeScreenshot: profile owns no heroes, skipping " + name + ".");
                        SessionState.SetInt(StageKey, slot + 1);
                        return;
                    }
                    // The ability dialog gets the BUSIEST hero the player owns rather than the
                    // first, because the only thing that capture can disprove is that the block
                    // runs out of the panel, and it only can when an ability carries several
                    // numbers. The hero page itself is photographed on owned[0] as before.
                    app.Router.HeroDetail.SetHero(name == "AbilityNumbers" ? BusiestHero(owned) : owned[0]);
                    // A frame apart from the Show for the same reason the Hub's layers are:
                    // ScreenRouter.Show closes every open layer before it changes screens, so a
                    // dialog opened in this call would be opened and closed again.
                    if (name == "AbilityNumbers") SessionState.SetString(AbilityKey, "1");
                }

                // THE TALENT PAGE NEEDS A HERO WITH SOMETHING STILL LOCKED, which is the one
                // state the page was rebuilt around and the one a fresh profile cannot show. A
                // starter is a five-star at ascension tier 0, so it reaches talent 1 and the other
                // two cards read LOCKED - which photographs the gate but not the page, because
                // the interesting frame is the one where some are open and one is not. So the
                // subject is ascended ONCE, through the same GameSession.Ascend the forge calls
                // and with the copy it charges granted first: two cards live, one shut, which is
                // every state the page has in one frame.
                if (name == "Talents")
                {
                    HeroInstance subject = TalentSubject(app);
                    if (subject == null)
                    {
                        Debug.LogWarning("PlayModeScreenshot: no owned hero authors talents, skipping Talents.");
                        SessionState.SetInt(StageKey, slot + 1);
                        return;
                    }
                    app.Router.Talents.SetHero(subject);
                }

                // THE FUSION PAGE IS TWO LAYOUTS AND NEEDS TWO SUBJECTS, because its two tabs
                // share nothing but the header and the action bar. The ascend tab wants a
                // five-star with the copies its next tier costs, so the cost line reads as met and
                // the before-and-after rows carry real figures rather than a blocked card. The
                // convert tab wants a low-star of a SEED aspect, because the seed rule is the one
                // thing on that tab a capture can disprove and only Umbra and Lumen show it.
                //
                // Both are seeded through GrantHeroDirect, the same door the summon's duplicate
                // path uses, for the reason PopulateItems rolls armour through ArmorRoller: a
                // screenshot of state no code path produces is evidence of nothing.
                if (name == "Fusion" || name == "FusionConvert")
                {
                    HeroInstance subject = name == "Fusion" ? FusionAscendSubject(app)
                                                            : FusionConvertSubject(app);
                    if (subject == null)
                    {
                        Debug.LogWarning("PlayModeScreenshot: no subject for " + name + ", skipping it.");
                        SessionState.SetInt(StageKey, slot + 1);
                        return;
                    }
                    app.Router.Fusion.ShowHero(subject);
                }

                // THE ITEM SCREENS NEED ITEMS. A fresh profile owns no armour and no relics, so
                // the inventory and the forge photograph as two empty boxes - which proves the
                // empty state and nothing at all about the grid, the detail panel or the item
                // cards, and those are the whole of both pages.
                //
                // Granted through the same ArmorRoller.Roll and GameSession.GrantRelic the drop
                // and the shop call, exactly as PopulateEvents drives real Summon and BuyRelic
                // calls rather than poking the profile: a screenshot of state no code path
                // produces is evidence of nothing.
                if (name == "Inventory" || name == "Forge") PopulateItems(app);

                if (name == "Battle" || name == "Result")
                {
                    string asked = SessionState.GetString(BattleStageKey, "");
                    StageDef next = asked.Length > 0 ? app.Session.Content.GetStage(asked) : app.Session.NextStage;
                    if (next == null)
                    {
                        Debug.LogWarning("PlayModeScreenshot: no campaign stage to fight"
                                         + (asked.Length > 0 ? " (no stage '" + asked + "')" : "")
                                         + ", skipping Battle.");
                        SessionState.SetInt(StageKey, slot + 1);
                        return;
                    }
                    app.Router.Battle.SetStage(next);
                }
                if (IsWaveResultTarget(name))
                {
                    // Not Router.Show: the screen resolves a wave as it opens, so it has to be
                    // started the way the Campaign screen starts it, with a stage and a seeded
                    // profile behind it.
                    // A replay is watched from a won mid-stage wave, so it seeds as one.
                    StageDef waveStage = SeedWaveResult(app, name == "WaveReplay" || name == "WaveReplayResult" || name == "WaveReplayHeal" ? "WaveResult" : name);
                    if (waveStage == null)
                    {
                        SessionState.SetInt(StageKey, slot + 1);
                        return;
                    }
                    CampaignFlow.LaunchResult went = CampaignFlow.Launch(app, waveStage);
                    if (went != CampaignFlow.LaunchResult.Resolved)
                        Debug.LogWarning("PlayModeScreenshot: CampaignFlow.Launch returned " + went
                                         + " for " + name + ", not Resolved.");
                }
                else app.Router.Show(id);
                Debug.Log("PlayModeScreenshot: showing " + name);
            }

            // THE WAVE RESULT waits on the screen, not on a frame count: how long a wave takes to
            // resolve depends on how far the line gets, and the screen paints only after the
            // resolve. Painted is its own answer, and the same paint delay as the battle result
            // lets the layout settle before the shot. The outcome is logged and checked against
            // the target's name, because a capture of the wrong outcome is a wrong capture.
            if (IsWaveResultTarget(name))
            {
                if (frame <= showAt) return;
                if (name == "WaveReplay" || name == "WaveReplayResult" || name == "WaveReplayHeal")
                {
                    PumpWaveReplay(app, name, frame, showAt, slot, BattleWaitCeiling, BattlePaintDelay);
                    return;
                }

                int armed = SessionState.GetInt(BattleArmKey, 0);
                if (armed == 0)
                {
                    WaveResultScreen waveScreen = app.Router.WaveResult;
                    if (waveScreen != null && waveScreen.Painted)
                    {
                        SessionState.SetInt(BattleArmKey, frame);
                        ReportWaveOutcome(name, waveScreen.Result);
                    }
                    else if (frame > showAt + BattleWaitCeiling)
                    {
                        SessionState.SetInt(BattleArmKey, frame);
                        Debug.LogWarning("PlayModeScreenshot: the wave result never painted within "
                                         + BattleWaitCeiling + " frames. Shooting anyway.");
                    }
                    return;
                }

                if (frame == armed + BattlePaintDelay - 5) Canvas.ForceUpdateCanvases();
                if (frame == armed + BattlePaintDelay) Shoot(name, frame, slot);
                return;
            }

            // Battle takes over the schedule from here: it arms when the round starts, or when
            // the ceiling runs out, and shoots BattlePaintDelay frames later so the strip has a
            // frame to lay out and paint before it is photographed.
            if (name == "Battle")
            {
                // Everything below this block is the fixed-frame path and Battle must not reach
                // it. `frame > shotAt` on this line alone was not enough: the generic shot fires
                // on `frame == shotAt`, which is one frame BEFORE that became true, so the first
                // run of this rewrite still shot on the old schedule and still caught PREPARING.
                if (frame <= shotAt) return;

                int armed = SessionState.GetInt(BattleArmKey, 0);
                if (armed == 0)
                {
                    BattleScreen battle = app.Router.Battle;
                    if (battle != null && battle.RoundUnderway)
                    {
                        SessionState.SetInt(BattleArmKey, frame);
                        Debug.Log("PlayModeScreenshot: the first round is underway at frame " + frame
                                  + "; shooting the battle HUD in " + BattlePaintDelay + " frames.");
                    }
                    else if (frame > showAt + BattleWaitCeiling)
                    {
                        SessionState.SetInt(BattleArmKey, frame);
                        Debug.LogWarning("PlayModeScreenshot: no round started within "
                                         + BattleWaitCeiling + " frames. Shooting anyway - the"
                                         + " capture then shows whatever the screen is stuck on.");
                    }
                    return;
                }

                if (frame == armed + BattlePaintDelay - 5) Canvas.ForceUpdateCanvases();
                if (frame == armed + BattlePaintDelay) Shoot(name, frame, slot);
                return;
            }

            // THE RESULT PANEL is the same fight photographed at its other end, and it is a
            // target of its own because it is the screen a player sees after EVERY battle in the
            // game and no capture in this repository has ever looked at it.
            //
            // It presses SKIP rather than sitting through the playback: how long a fight lasts is
            // a property of which five heroes are deployed and how many waves the stage has, and
            // 144 battles at playback speed is not a schedule. Skip is a real control on the real
            // screen, so what this photographs is what a player who taps it sees.
            if (name == "Result")
            {
                if (frame <= shotAt) return;

                BattleScreen screen = app.Router.Battle;
                if (screen == null)
                {
                    Debug.LogWarning("PlayModeScreenshot: no battle screen, skipping Result.");
                    SessionState.SetInt(StageKey, slot + 1);
                    return;
                }

                int armed = SessionState.GetInt(BattleArmKey, 0);
                if (armed == 0)
                {
                    if (screen.Finished) { SessionState.SetInt(BattleArmKey, frame); return; }
                    if (screen.RoundUnderway) screen.SkipToEndForCapture();
                    else if (frame > showAt + BattleWaitCeiling)
                    {
                        SessionState.SetInt(BattleArmKey, frame);
                        Debug.LogWarning("PlayModeScreenshot: no round started within "
                                         + BattleWaitCeiling + " frames, so the result never"
                                         + " arrived. Shooting anyway - the capture shows"
                                         + " whatever the screen is stuck on.");
                    }
                    return;
                }

                if (frame == armed + BattlePaintDelay - 5) Canvas.ForceUpdateCanvases();
                if (frame == armed + BattlePaintDelay) Shoot(name, frame, slot);
                return;
            }

            // The Hub's layers open one frame after the Show that put the Hub up, because
            // ScreenRouter.Show closes every open layer before it changes screens - opening one
            // in the same call opens it and closes it again.
            if (SessionState.GetString(AbilityKey, "").Length > 0 && frame == showAt + 6)
            {
                SessionState.SetString(AbilityKey, "");
                HeroDetailScreen page = app.Router.HeroDetail;
                if (page == null) Debug.LogWarning("PlayModeScreenshot: no hero page to open an ability on.");
                else page.OpenAbilityForCapture();
            }

            string pendingLayer = SessionState.GetString(HubLayerKey, "");
            if (pendingLayer.Length > 0 && frame == showAt + 6)
            {
                SessionState.SetString(HubLayerKey, "");
                MainMenuScreen hub = app.Router.MainMenu;
                if (hub == null) Debug.LogWarning("PlayModeScreenshot: no Hub screen to open a layer on.");
                else hub.OpenLayerForCapture(HubLayerName(pendingLayer));
            }

            if ((name == "Toast" || name == "ToastLong") && frame == shotAt - 30) app.Notify(ToastSample(name));

            if (frame == shotAt - 20) Canvas.ForceUpdateCanvases();
            if (frame == shotAt) Shoot(name, frame, slot);
        }

        /// <summary>
        /// What the toast targets say. "Toast" is the sentence the live guild dungeon actually
        /// raised when floor 2 was asked for too early - the one that ran past both ends of the
        /// strip. "ToastLong" is longer than any one line can hold at either aspect, so the strip
        /// has to wrap and grow rather than only widen.
        /// </summary>
        private static string ToastSample(string name)
        {
            if (name == "Toast")
                return "The attempt was not recorded, so it was not spent. floor 2 opens when somebody in your guild clears floor 1";
            return "The attempt was not recorded, so it was not spent. The server said: no attempts left on floor 1 this "
                 + "cycle (3 of 3). Your three attempts come back when the cycle turns over, and the boards keep "
                 + "your best clear until then.";
        }

        /// <summary>
        /// Gives the profile a handful of real armour pieces and every relic, so the item screens
        /// have something to draw.
        ///
        /// One piece per slot per owned hero at one temper would be a wall of identical cards, so
        /// the tempers cycle: what a capture of these pages has to show is four different rarity
        /// borders and four different pip counts side by side, which is the whole of the item
        /// card's rarity read.
        /// </summary>
        private static void PopulateItems(GameApp app)
        {
            GameSession session = app.Session;
            ArmorConfig config = session.Content != null ? session.Content.Armor : null;
            if (config == null || !config.IsUsable)
            {
                Debug.LogWarning("PlayModeScreenshot: the armor config is not usable, so the item"
                                 + " screens will photograph empty.");
                return;
            }

            PlayerProfile profile = session.Profile;
            if (profile.OwnedArmor.Count == 0)
            {
                int seed = 770000;
                for (int h = 0; h < profile.Heroes.Count && h < 4; h++)
                {
                    HeroInstance hero = profile.Heroes[h];
                    if (hero.Def == null) continue;
                    for (int sIdx = 0; sIdx < ArmorInfo.AllSlots.Length; sIdx++)
                    {
                        ArmorTemper temper = (ArmorTemper)((h + sIdx) % 4);
                        ArmorPiece piece = ArmorRoller.Roll(config, ArmorInfo.AllSlots[sIdx], temper,
                                                            hero.Def.Aspect, seed++);
                        if (piece == null) continue;
                        piece.Uid = profile.AllocateArmorUid();
                        profile.OwnedArmor.Add(piece);
                        if (h == 0) session.EquipArmor(hero, piece.Uid);
                    }
                }
            }

            // HALF the relics, not all of them. Granting every relic makes the shop photograph as
            // ten OWNED cards, which shows one of that screen's two states and hides the other -
            // and the one it hides is the card with a price and a BUY on it, which is what the
            // shop is for.
            for (int i = 0; i < session.Content.Relics.Count; i += 2)
                session.GrantRelic(session.Content.Relics[i].Id);

            app.RefreshHud();
            Debug.Log("PlayModeScreenshot: the profile now holds " + profile.OwnedArmor.Count
                      + " armor pieces and " + profile.OwnedRelics.Count + " relics.");
        }

        /// <summary>
        /// Opens one of the Hub's layers, one frame after the Hub itself is shown.
        ///
        /// A frame apart on purpose: ScreenRouter.Show closes every open layer before it changes
        /// screens, so opening a popup in the same call that shows the Hub opens it and closes it
        /// again. The schedule below runs this on the frame AFTER the Show.
        /// </summary>
        private static void OpenHubLayer(GameApp app, string name)
        {
            SessionState.SetString(HubLayerKey, name);
        }

        private const string HubLayerKey = "PlayModeScreenshot.HubLayer";
        private const string AbilityKey = "PlayModeScreenshot.AbilityBreakdown";

        /// <summary>
        /// A hero to photograph the talent page on, ascended exactly one tier so the page shows
        /// both of the states the ascension gate has: two cards the player can run and one that
        /// reads LOCKED with the tier that opens it (DESIGN-TALENTS T6).
        ///
        /// The ascension is real. GameSession.Ascend is the only way a tier moves in the game, it
        /// charges copies, and the copy is granted through GrantHeroDirect first - the same reason
        /// PopulateItems rolls armour through ArmorRoller instead of poking the profile. A
        /// screenshot of a tier no code path produces would be evidence of nothing.
        ///
        /// Returns null when the profile owns no hero that authors talents, and the caller skips
        /// the shot rather than faking one.
        /// </summary>
        private static HeroInstance TalentSubject(GameApp app)
        {
            List<HeroInstance> owned = app.Session.Profile.Heroes;
            if (owned == null) return null;
            HeroInstance subject = null;
            for (int i = 0; i < owned.Count && subject == null; i++)
                if (owned[i].Def != null && owned[i].Def.Talents.Count >= HeroDef.MaxTalents) subject = owned[i];
            if (subject == null) return null;

            // One tier, no more: at tier 1 the third card is still shut, which is the frame worth
            // taking. A fully ascended subject would photograph a page with nothing locked on it.
            if (subject.Ascension == 0)
            {
                for (int guard = 0; guard < 8 && app.Session.AscensionBlocker(subject) != null; guard++)
                    app.Session.GrantHeroDirect(subject.Def, 0);
                AscensionResult result = app.Session.Ascend(subject);
                if (!result.Success)
                    Debug.LogWarning("PlayModeScreenshot: could not ascend " + subject.Def.Name
                                     + " for the talent page - " + result.Message
                                     + ". Shooting it at tier 0, where all three cards read LOCKED but the first.");
            }
            app.Session.SelectTalent(subject, 1);
            return subject;
        }

        /// <summary>
        /// A five-star holding the copies its next ascension costs, so the ascend tab photographs
        /// a card that can actually be pressed.
        ///
        /// The copies are granted, the ascension is NOT taken: the frame worth having is the one
        /// before the step, because the before-and-after rows and the level-reset warning are the
        /// whole of what that tab exists to say and they are gone the moment it is pressed.
        ///
        /// Returns null when the profile owns no five-star at all, and the caller skips the shot.
        /// </summary>
        private static HeroInstance FusionAscendSubject(GameApp app)
        {
            List<HeroInstance> owned = app.Session.Profile.Heroes;
            if (owned == null) return null;

            // A TIER-0 FIVE-STAR STANDING AT ITS CAP, which is the frame the card's three rows are
            // checked against: tier level 9 » 1, Level 9 » 10, cap 9 » 18. The capture profile
            // carries heroes from before the star-gated caps at Level 60 (DESIGN-BUILD-ORDER,
            // Known bugs), and one of those photographs four correct figures for a hero that
            // should not exist. So an owned hero already at its cap is taken first, and failing
            // that an unowned five-star is granted and levelled to its cap through the same
            // LevelUpHero the hero page calls. Each level's price is seeded first, the way
            // DebugCheatPanel seeds gold: a fresh profile cannot pay for nine levels, and the
            // frame stopped at Level 3 the first time this ran.
            HeroInstance subject = null;
            for (int i = 0; i < owned.Count && subject == null; i++)
            {
                HeroInstance h = owned[i];
                if (h.Def != null && h.Def.Stars >= 5 && h.Ascension == 0
                    && h.Level == app.Session.HeroLevelCapOf(h)) subject = h;
            }
            List<HeroDef> heroes = app.Session.Content.Heroes;
            for (int i = 0; i < heroes.Count && subject == null; i++)
            {
                HeroDef def = heroes[i];
                if (def.Stars < 5 || def.Talents.Count < HeroDef.MaxTalents || app.Session.Profile.Owns(def.Id)) continue;
                HeroInstance granted = app.Session.GrantHeroDirect(def, 0);
                if (granted == null) continue;
                for (int guard = 0; guard < 100 && granted.Level < app.Session.HeroLevelCapOf(granted); guard++)
                {
                    app.Session.Profile.Gold += app.Session.LevelUpCost(granted);
                    app.Session.LevelUpHero(granted, 1);
                }
                if (granted.Level != app.Session.HeroLevelCapOf(granted))
                    Debug.LogWarning("PlayModeScreenshot: " + def.Name + " stopped at Level " + granted.Level
                                     + " short of its cap.");
                subject = granted;
            }
            for (int i = 0; i < owned.Count && subject == null; i++)
                if (owned[i].Def != null && owned[i].Def.Stars >= 5) subject = owned[i];
            if (subject == null) return null;

            for (int guard = 0; guard < 8 && app.Session.AscensionBlocker(subject) != null; guard++)
                app.Session.GrantHeroDirect(subject.Def, 0);
            if (app.Session.AscensionBlocker(subject) != null)
                Debug.LogWarning("PlayModeScreenshot: " + subject.Def.Name + " still cannot ascend - "
                                 + app.Session.AscensionBlocker(subject)
                                 + ". Shooting the ascend tab blocked, which is also a state it has.");
            return subject;
        }

        /// <summary>
        /// A low-star hero of a SEED aspect, with generic fodder beside it, so the convert tab
        /// photographs the rule that only Umbra and Lumen carry.
        ///
        /// FusionScreen.ShowHero sends anything below five stars to the convert tab on its own
        /// aspect, so handing it one of these is the same door a player comes through from the
        /// hero page - not a second way in written for the camera.
        ///
        /// The fodder is granted rather than assumed: a fresh profile owns a handful of heroes and
        /// an empty grid proves the empty state and nothing about the cells, the points meter or
        /// the not-this-aspect tag, which are the whole of that tab.
        /// </summary>
        private static HeroInstance FusionConvertSubject(GameApp app)
        {
            List<HeroDef> heroes = app.Session.Content.Heroes;
            HeroInstance seed = null;
            for (int i = 0; i < heroes.Count; i++)
            {
                HeroDef def = heroes[i];
                if (def.Stars >= 5 || def.Stars < 1) continue;
                bool seedAspect = app.Session.IsSeedAspect(def.Aspect);
                // Two copies of the seed and one of everything else: the seed needs a spare body
                // to be offerable at all, and one of each of the rest fills the grid without
                // making the shot a picture of a collection nobody has.
                app.Session.GrantHeroDirect(def, 0);
                if (seedAspect)
                {
                    app.Session.GrantHeroDirect(def, 0);
                    if (seed == null) seed = app.Session.Profile.FindHeroByDefId(def.Id);
                }
            }
            return seed;
        }

        /// <summary>
        /// The owned hero whose busiest single ability carries the most printable numbers.
        ///
        /// Counted from the effect data rather than guessed, and counted the way the card counts:
        /// AbilityMath.Carries is the same predicate the hero page reads. Falls back to the first
        /// hero when nobody owns anything with a number on it.
        /// </summary>
        private static HeroInstance BusiestHero(List<HeroInstance> owned)
        {
            HeroInstance best = owned[0];
            int bestCount = -1;
            for (int i = 0; i < owned.Count; i++)
            {
                HeroDef def = owned[i].Def;
                if (def == null) continue;
                int most = 0;
                most = Math.Max(most, CountValues(def.BasicAttack));
                most = Math.Max(most, CountValues(def.Active));
                for (int p = 0; p < def.Passives.Count; p++)
                    most = Math.Max(most, CountValues(AbilityMath.AsAbility(def.Passives[p])));
                if (most > bestCount) { bestCount = most; best = owned[i]; }
            }
            return best;
        }

        private static int CountValues(AbilityDef ability)
        {
            if (ability == null) return 0;
            int n = 0;
            for (int i = 0; i < ability.Effects.Count; i++)
                if (AbilityMath.Carries(ability.Effects[i])) n++;
            return n;
        }

        /// <summary>Requests one screen's PNG and advances the schedule to the next screen.
        ///
        /// The frame size is in the FILE NAME, not only in the log. Two captures of one screen at
        /// two aspects are two pieces of evidence about two different layouts, and writing both
        /// to one path means the second silently replaces the first and the run reports two
        /// successes for one surviving file.</summary>
        private static void Shoot(string name, int frame, int slot)
        {
            string file = "screen_" + name + SessionState.GetString(SuffixKey, "") + ".png";
            ScreenCapture.CaptureScreenshot(OutputPath(file), 1);
            Debug.Log("PlayModeScreenshot: requested " + file);
            SessionState.SetInt(LastShotKey, frame);
            SessionState.SetInt(StageKey, slot + 1);
            // Cleared here rather than at the next screen's start: Battle and Result both arm on
            // this key, so an ALL run that left it set would shoot the second of them on the
            // first one's arming frame and photograph a screen that had not been shown yet.
            SessionState.SetInt(BattleArmKey, 0);
        }

        /// <summary>The target name a capture asks for, as the door the Hub already has.</summary>
        private static string HubLayerName(string target)
        {
            switch (target)
            {
                case "Claim": return "claim";
                case "Sheet": return "profile";
                case "Menu": return "menu";
                case "Gallery": return "gallery";
            }
            return "dialog";
        }

        // ================================================================== wave result

        private static bool IsWaveResultTarget(string name)
        {
            return name == "WaveResult" || name == "WaveResultAuto" || name == "WaveResultCleared"
                   || name == "WaveResultLost" || name == "WaveReplay" || name == "WaveReplayResult"
                   || name == "WaveReplayHeal";
        }

        private const string ReplayStageKey = "PlayModeScreenshot.ReplayStage";

        /// <summary>
        /// WaveReplay: waits for the won wave to paint, presses WATCH through the screen's own
        /// handler, waits for the replay to be underway with a hit effect alive, freezes time, and
        /// shoots. Time is frozen rather than the shot timed because an effect lives a fraction of
        /// a second and a frame count cannot land on one; a frozen particle system still renders.
        /// Stage 0 waits on the result, 1 on the effect, 2 is frozen and counting down to the shot.
        /// </summary>
        private static void PumpWaveReplay(GameApp app, string name, int frame, int showAt, int slot, int waitCeiling, int paintDelay)
        {
            // WaveReplayResult is the same walk to the same replay, ended at the other end: it skips
            // the fight to its result panel, where the replay's BACK button lives, instead of
            // freezing a hit mid-fight.
            bool toResult = name == "WaveReplayResult";
            // WaveReplayHeal freezes on a Heal being played instead of a blow, to photograph the heal flipbook.
            bool onHeal = name == "WaveReplayHeal";
            int stage = SessionState.GetInt(ReplayStageKey, 0);
            int armed = SessionState.GetInt(BattleArmKey, 0);

            if (stage == 0)
            {
                WaveResultScreen waveScreen = app.Router.WaveResult;
                if (waveScreen != null && waveScreen.Painted)
                {
                    ReportWaveOutcome("WaveResult", waveScreen.Result);
                    if (waveScreen.Result == null || waveScreen.Result.Replay == null)
                    {
                        Debug.LogWarning("PlayModeScreenshot: the wave kept no replay to watch. Skipping WaveReplay.");
                        SessionState.SetInt(StageKey, slot + 1);
                        return;
                    }
                    waveScreen.WatchForCapture();
                    Debug.Log("PlayModeScreenshot: WATCH pressed on wave " + waveScreen.Result.Replay.Wave
                              + ", battle " + waveScreen.Result.Replay.Battle + ", seed " + waveScreen.Result.Replay.Seed + ".");
                    SessionState.SetInt(ReplayStageKey, 1);
                    SessionState.SetInt(BattleArmKey, frame);
                }
                else if (frame > showAt + waitCeiling)
                {
                    Debug.LogWarning("PlayModeScreenshot: the wave result never painted. Skipping WaveReplay.");
                    SessionState.SetInt(StageKey, slot + 1);
                }
                return;
            }

            if (stage == 1)
            {
                BattleScreen battle = app.Router.Battle;
                if (toResult)
                {
                    if (battle != null && battle.RoundUnderway)
                    {
                        battle.SkipToEndForCapture();
                        SessionState.SetInt(ReplayStageKey, 2);
                        SessionState.SetInt(BattleArmKey, frame);
                    }
                    else if (frame > armed + waitCeiling)
                    {
                        Debug.LogWarning("PlayModeScreenshot: the replay never got underway. Skipping WaveReplayResult.");
                        SessionState.SetInt(ReplayStageKey, 0);
                        SessionState.SetInt(StageKey, slot + 1);
                    }
                    return;
                }
                VfxDirector director = UnityEngine.Object.FindFirstObjectByType<VfxDirector>();
                int live = 0;
                if (director != null)
                {
                    ParticleSystem[] systems = director.GetComponentsInChildren<ParticleSystem>();
                    for (int i = 0; i < systems.Length; i++) live += systems[i].particleCount;
                }
                if (battle != null && battle.RoundUnderway && (onHeal ? battle.HealJustPlayed : battle.HitJustPlayed) && live >= (onHeal ? 1 : 8))
                {
                    Time.timeScale = 0f;
                    SessionState.SetInt(ReplayStageKey, 2);
                    SessionState.SetInt(BattleArmKey, frame);
                    Debug.Log("PlayModeScreenshot: froze the replay with " + live + " live particles at frame " + frame + ".");
                }
                else if (frame > armed + waitCeiling)
                {
                    Debug.LogWarning("PlayModeScreenshot: no hit effect appeared in the replay within " + waitCeiling
                                     + " frames. Shooting whatever is on screen - do not file it as a hit.");
                    SessionState.SetInt(ReplayStageKey, 2);
                    SessionState.SetInt(BattleArmKey, frame);
                }
                return;
            }

            if (frame == armed + paintDelay - 5) Canvas.ForceUpdateCanvases();
            if (frame == armed + paintDelay)
            {
                Shoot(name, frame, slot);
                Time.timeScale = 1f;
                SessionState.SetInt(ReplayStageKey, 0);
            }
        }

        /// <summary>
        /// Puts the SCRATCH profile in a state where one tap on a stage resolves to the outcome
        /// the target is named for, and returns the stage to launch (null to skip the target).
        ///
        ///   WaveResult         a mid-stage wave of the first 12-wave stage, won whole ("WAVE 4
        ///                      CLEARED"), with the player's AUTO setting off
        ///   WaveResultAuto     the same wave with AUTO on, the countdown held on its first digit
        ///   WaveResultCleared  the last wave of the same stage, which clears it on a FIRST clear
        ///                      that drops armor (the reward tiles with an armor tile)
        ///   WaveResultLost     a wave of the 12-wave stage that is lost part-way, ideally after a
        ///                      few won battles
        ///
        /// WHAT IT DOES. The campaign is rebuilt from nothing on every call - cleared ids emptied,
        /// the cursor reset, then every stage before the target granted its first clear through
        /// GrantBattleResult in campaign order - so the result does not depend on which target ran
        /// before it. The line is then set to a level and the wave tried for real through
        /// GameSession.ResolveWave, which changes the profile; each candidate therefore starts from
        /// that same rebuild. A sweep over level and seed finds the first state that gives the
        /// outcome, and the state is rebuilt once more for the screen's own resolve to run on, with
        /// the winning seed handed to it through WaveResultScreen.CaptureSeed, so the photograph is
        /// the probe's wave. ReportWaveOutcome still checks, rather than assuming it.
        ///
        /// It sets hero levels and credits gold directly, which is why it refuses to run unless the
        /// identity is the scratch one ScratchCaptures installs: the same rule PopulateRecitalLadder
        /// documents for real balances.
        /// </summary>
        private static StageDef SeedWaveResult(GameApp app, string name)
        {
            if (Application.persistentDataPath.IndexOf("Emberfall Scratch", StringComparison.OrdinalIgnoreCase) < 0)
            {
                Debug.LogError("PlayModeScreenshot: " + name + " rewrites campaign progress and hero levels, so it"
                               + " only runs on the scratch identity (run it through ScratchCaptures). Skipping.");
                return null;
            }

            GameSession session = app.Session;
            List<StageDef> stages = session.Content.AllStages;
            int index = -1;
            for (int i = 0; i < stages.Count; i++)
            {
                if (!stages[i].HasWaveCurve) continue;
                if (stages[i].WaveCount >= 12) { index = i; break; }
            }
            if (index < 0)
            {
                Debug.LogWarning("PlayModeScreenshot: no stage suits " + name + ", skipping it.");
                return null;
            }
            StageDef stage = stages[index];

            // A CHAPTER 2 WAVE NEEDS A CHAPTER 2 LINE. The scratch profile starts with the three
            // starters, and at their cap they lost battle 1 of ch2_1's fourth wave at every level,
            // so "WAVE 4 CLEARED" could never be photographed with them. The won wave is fielded
            // with the harness's pinned line instead, one five-star per role, which is the kind of
            // line a player at that stage commands. The loss and the stage clear keep the starters:
            // they already produce their outcomes, and a weak line is what a loss looks like.
            // The stage clear is the same stage's twelfth wave, which the starters cannot reach
            // either, so it is fielded with the same line.
            bool auto = name == "WaveResultAuto";
            if (auto) name = "WaveResult";
            WaveResultScreen.AutoAdvance = auto;
            WaveResultScreen.CaptureHoldCountdown = auto;
            if (name == "WaveResult" || name == "WaveResultCleared") FieldLine(session, WaveLine);

            // THE PROBE AND THE PHOTOGRAPH MUST REBUILD THE SAME PROFILE, and two things in it
            // moved between rebuilds: the clock every earlier stage's grant rolled its armor on,
            // and the armor every probe's own drop added. A stage clear's drop is seeded from the
            // clock, the stage and how much armor is owned, so both are pinned here: one clock for
            // the whole search, and the collection cut back to what it held before the search.
            _waveNow = TimeUtil.NowUnix();
            _waveArmorBase = session.Profile.OwnedArmor.Count;

            List<HeroInstance> team = session.Profile.TeamHeroes();
            if (team.Count == 0)
            {
                Debug.LogWarning("PlayModeScreenshot: no deployed hero, skipping " + name + ".");
                return null;
            }
            int cap = 1;
            for (int i = 0; i < team.Count; i++) cap = Math.Max(cap, session.HeroLevelCapOf(team[i]));

            int wave = name == "WaveResultCleared" ? stage.WaveCount : 4;
            int[] waves = name == "WaveResultLost"
                ? new int[] { stage.WaveCount, stage.WaveCount / 2, 1 }
                : new int[] { wave };
            int step = name == "WaveResultLost" ? 1 : 2;

            // THE SEED IS PART OF THE SEARCH, and the screen is handed the one that won it. Probing
            // on a clock seed and then resolving on another let the photograph land a different wave
            // from the one the probe found: the first run shot "lost at battle 1" under
            // WaveResultLost at 2400x1080 after its probe had found a later loss. With fixed seeds
            // the probe and the photograph are the same wave.
            int[] seeds = { 1009, 2003, 3001, 4007 };
            int bestLevel = cap;
            int bestWave = waves[0];
            int bestSeed = seeds[0];
            int bestScore = -1;
            for (int w = 0; w < waves.Length && bestScore < 1000; w++)
            {
                for (int level = 1; level <= cap && bestScore < 1000; level += step)
                {
                    for (int s = 0; s < seeds.Length; s++)
                    {
                        RebuildCampaign(app, stages, index, waves[w], level);
                        WaveResolution probe = session.ResolveWave(stage, seeds[s], _waveNow);
                        int score = ScoreWaveProbe(name, probe);
                        if (score > bestScore) { bestScore = score; bestLevel = level; bestWave = waves[w]; bestSeed = seeds[s]; }
                        if (score >= 1000) break;
                    }
                }
            }

            // A first clear that drops armor: the clear is found above, and the drop is a roll on
            // the clock, so the clock is searched with that clear's level and seed.
            long bestNow = _waveNow;
            if (name == "WaveResultCleared" && bestScore >= 1000)
            {
                bool dropped = false;
                for (int t = 0; t < 400 && !dropped; t++)
                {
                    RebuildCampaign(app, stages, index, bestWave, bestLevel);
                    WaveResolution probe = session.ResolveWave(stage, bestSeed, _waveNow + t);
                    if (probe.StageCleared && probe.Rewards != null && probe.Rewards.ArmorDrop != null)
                    {
                        dropped = true;
                        bestNow = _waveNow + t;
                    }
                }
                if (!dropped) Debug.LogWarning("PlayModeScreenshot: no clock in 400 seconds dropped armor on the clear.");
            }

            RebuildCampaign(app, stages, index, bestWave, bestLevel);
            WaveResultScreen.CaptureSeed = bestSeed;
            WaveResultScreen.CaptureNow = bestNow;
            Debug.Log("PlayModeScreenshot: " + name + " seeded on stage " + stage.DisplayCode + ", wave " + bestWave
                      + ", line level " + bestLevel + " of " + cap + ", seed " + bestSeed + ", clock " + bestNow
                      + ", AUTO " + (auto ? "on" : "off") + " (probe score " + bestScore + ").");
            return stage;
        }

        /// <summary>The one clock and the armor count a wave capture's rebuilds share; see SeedWaveResult.</summary>
        private static long _waveNow;
        private static int _waveArmorBase;

        /// <summary>The harness's pinned line (PinnedLine in Tools/DevHarness/Program.cs): one
        /// five-star per role. Restated rather than shared because the harness is not part of the
        /// project; a renamed hero is logged and skipped rather than failing the capture.</summary>
        private static readonly string[] WaveLine =
        {
            "kael_the_unyielding", "aeron_the_unbroken_stride", "nereia_the_field_medic",
            "seraphine_the_closed_volume", "isolde_the_slow_fading",
        };

        /// <summary>Grants the named heroes through GameSession.GrantHeroDirect and deploys them
        /// as the line, in order. Scratch identity only, like everything SeedWaveResult does.</summary>
        private static void FieldLine(GameSession session, string[] ids)
        {
            PlayerProfile profile = session.Profile;
            long now = TimeUtil.NowUnix();
            List<string> uids = new List<string>();
            for (int i = 0; i < ids.Length; i++)
            {
                HeroDef def = session.Content.GetHero(ids[i]);
                if (def == null) { Debug.LogWarning("PlayModeScreenshot: no hero '" + ids[i] + "' to field, skipping it."); continue; }
                HeroInstance hero = session.GrantHeroDirect(def, now);
                if (hero != null) uids.Add(hero.Uid);
            }
            if (uids.Count == 0) return;
            for (int i = 0; i < profile.Team.Length; i++) profile.ClearTeamSlot(i);
            for (int i = 0; i < uids.Count && i < profile.Team.Length; i++) profile.AssignToTeam(i, uids[i]);
        }

        /// <summary>
        /// How close a probe resolve came to the target's outcome. 1000 or more is the outcome
        /// wanted; below that is a ranking, so a sweep with no perfect level still settles on the
        /// nearest thing and says so.
        /// </summary>
        private static int ScoreWaveProbe(string name, WaveResolution r)
        {
            if (r.Refused != WaveRefusal.None) return -1;
            bool lost = r.LostBattle > 0;
            if (name == "WaveResultLost")
            {
                if (!lost) return 0;
                if (r.LostBattle >= 4) return 1000;
                return r.LostBattle >= 3 ? 500 : 100 + r.LostBattle;
            }
            if (name == "WaveResultCleared") return r.StageCleared ? 1000 : (r.WaveCleared ? 50 : 0);
            // A won mid-stage wave: the whole wave, and one that paid, so the reward chips are in shot.
            if (r.StageCleared || lost) return 0;
            if (!r.WaveCleared) return 0;
            return r.WaveGold > 0 ? 1000 : 500;
        }

        /// <summary>
        /// The campaign as a player who had cleared every stage before the target and stands at
        /// the start of the given wave of it, with the deployed line at the given level (never past
        /// a hero's cap). Rebuilt from nothing each time, through GrantBattleResult for the
        /// earlier stages, so it can be called repeatedly and after any other target.
        /// </summary>
        private static void RebuildCampaign(GameApp app, List<StageDef> stages, int targetIndex, int wave, int level)
        {
            GameSession session = app.Session;
            PlayerProfile profile = session.Profile;
            long now = _waveNow != 0 ? _waveNow : TimeUtil.NowUnix();

            if (profile.OwnedArmor.Count > _waveArmorBase)
                profile.OwnedArmor.RemoveRange(_waveArmorBase, profile.OwnedArmor.Count - _waveArmorBase);
            profile.ClearedStageIds.Clear();
            profile.StagesCleared = 0;
            profile.CampaignCurrentStageId = "";
            profile.CampaignCurrentWave = 1;
            profile.CampaignCurrentBattle = 1;
            for (int i = 0; i < targetIndex; i++) session.GrantBattleResult(stages[i], true, now);

            List<HeroInstance> team = profile.TeamHeroes();
            for (int i = 0; i < team.Count; i++)
                team[i].Level = Math.Max(1, Math.Min(level, session.HeroLevelCapOf(team[i])));

            StageDef target = stages[targetIndex];
            profile.CampaignCurrentStageId = target.Id;
            profile.CampaignCurrentWave = Math.Max(1, Math.Min(wave, target.WaveCount));
            profile.CampaignCurrentBattle = 1;
            app.RefreshHud();
        }

        /// <summary>Logs what the wave actually came to and warns when it is not what the target is
        /// named for. The probe and the screen share a seed, so a mismatch means the state moved
        /// between the two.</summary>
        private static void ReportWaveOutcome(string name, WaveResolution r)
        {
            if (r == null) { Debug.LogWarning("PlayModeScreenshot: " + name + " painted with no resolution."); return; }
            string actual = r.Refused != WaveRefusal.None ? "refused (" + r.Refused + ")"
                          : r.LostBattle > 0 ? "lost at battle " + r.LostBattle + " of " + r.BattleCount
                                               + " (" + r.Battles.Count + " fought from battle " + r.FirstBattle + ")"
                          : r.StageCleared ? "stage cleared" + (r.Rewards != null && r.Rewards.FirstClear ? " (first clear)" : "")
                                             + (r.Rewards != null && r.Rewards.ArmorDrop != null ? " with " + r.Rewards.ArmorDrop.Name : " with no armor")
                          : "wave " + r.Wave + " cleared, +" + r.WaveGold + " gold, +" + r.WaveAccountXp + " XP";
            bool match = name == "WaveResultLost" ? r.LostBattle > 0
                       : name == "WaveResultCleared" ? r.StageCleared && r.Rewards != null && r.Rewards.FirstClear && r.Rewards.ArmorDrop != null
                       : r.WaveCleared && !r.StageCleared;
            Debug.Log("PlayModeScreenshot: " + name + " outcome: " + actual + ", AUTO " + (WaveResultScreen.AutoAdvance ? "on" : "off") + ".");
            if (!match)
                Debug.LogWarning("PlayModeScreenshot: " + name + " did NOT produce its named outcome (" + actual
                                 + "). Do not file this capture under that name.");
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
