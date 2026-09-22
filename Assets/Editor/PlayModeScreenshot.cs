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
        private const string BuildMarker = "FIXV13-the-trigger-carries-its-own-aspect";

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

            if (!GameViewSize.Apply(width, height))
            {
                Debug.LogWarning("PlayModeScreenshot: could not pin the Game view to "
                                 + width + "x" + height + "; the capture will use"
                                 + " whatever size it is currently at and its layout is unproven.");
            }
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(TargetKey, target);
            SessionState.SetInt(WidthKey, width);
            SessionState.SetInt(HeightKey, height);
            SessionState.SetString(SuffixKey, suffix);
            SessionState.SetInt(FrameKey, 0);
            SessionState.SetInt(StageKey, 0);
            // Both of these survive a domain reload, so a second run in the same editor session
            // would inherit the first run's arming frame and shoot immediately if they were left.
            SessionState.SetInt(LastShotKey, 0);
            SessionState.SetInt(BattleArmKey, 0);
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

            // Every page the router owns, so "ALL" means all of them rather than the thirteen
            // that happened to have a target when this list was last written. The eight added
            // here - Campaign, Forge, Settings, Profile, Guild and its two sub-pages, Proving -
            // are pages a player reaches by tapping, and a page with no capture target is a page
            // whose layout nothing in this repository has ever looked at.
            string[] all = { "MainMenu", "Campaign", "Heroes", "HeroDetail", "Formation", "Summon",
                             "Forge", "Inventory", "RelicShop", "Events", "Recital", "Proving",
                             "Profile", "Account", "Friends", "FriendTeam", "Settings",
                             "Guild", "GuildRequests", "GuildChat", "Battle", "Result",
                             "Claim", "Sheet", "Menu", "Gallery", "Dialog" };
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
                }

                // THE HUB'S THREE LAYERS ABOVE THE MAP are targets of their own, because none of
                // them is a screen the router owns: a popup, a panel sheet and a menu are opened
                // BY the Hub, over it, and a capture schedule that only ever calls Router.Show
                // can photograph none of the three. MainMenuScreen.OpenLayerForCapture is the
                // door they already have, and it opens each one the way a tap does.
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
                if (name == "HeroDetail")
                {
                    List<HeroInstance> owned = app.Session.Profile.Heroes;
                    if (owned == null || owned.Count == 0)
                    {
                        Debug.LogWarning("PlayModeScreenshot: profile owns no heroes, skipping HeroDetail.");
                        SessionState.SetInt(StageKey, slot + 1);
                        return;
                    }
                    app.Router.HeroDetail.SetHero(owned[0]);
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
            string pendingLayer = SessionState.GetString(HubLayerKey, "");
            if (pendingLayer.Length > 0 && frame == showAt + 6)
            {
                SessionState.SetString(HubLayerKey, "");
                MainMenuScreen hub = app.Router.MainMenu;
                if (hub == null) Debug.LogWarning("PlayModeScreenshot: no Hub screen to open a layer on.");
                else hub.OpenLayerForCapture(HubLayerName(pendingLayer));
            }

            if (frame == shotAt - 20) Canvas.ForceUpdateCanvases();
            if (frame == shotAt) Shoot(name, frame, slot);
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
