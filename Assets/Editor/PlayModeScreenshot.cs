#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Emberfall.Battle;
using Emberfall.Game;
using Emberfall.Game.Telemetry;
using Emberfall.UI;
using Emberfall.UI.Screens;
using Emberfall.UI.Summon;
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
        /// <summary>Screen:BattleMoment only: the text after the stage id, "kind[.aspect][.off]".</summary>
        private const string MomentKey = "PlayModeScreenshot.Moment";
        /// <summary>Screen:BattleMoment only: 0 waits for the round, 1 for the moment, 2 settles.</summary>
        private const string MomentStageKey = "PlayModeScreenshot.MomentStage";
        /// <summary>Screen:BattleMoment only: the GAME frame (Time.frameCount) the moment arrived on,
        /// then the one the clock was frozen on. The editor's update runs more often than the game's
        /// frames when the editor is not in front, so the settle is counted in game frames.</summary>
        private const string MomentFrameKey = "PlayModeScreenshot.MomentFrame";
        /// <summary>Screen:BattleMoment only: how many game frames this moment settles for.</summary>
        private const string MomentSettleKey = "PlayModeScreenshot.MomentSettle";
        /// <summary>Set once an onboarding target that waits on a screen (a battle's first round, a
        /// wave result's paint) has seen it ready, so the shot is timed from then.</summary>
        private const string OnboardReadyKey = "PlayModeScreenshot.OnboardReady";

        /// <summary>
        /// The daily loop's switch for this run: 0 leaves it as the build has it, 1 forces it ON for
        /// the daily orders and the hub-with-entry targets, 2 forces it OFF for the hub-without-entry
        /// target. It survives the domain reload that entering Play mode triggers, which resets the
        /// switch to the build's default, so the static constructor below re-applies it before the
        /// game boots and the router decides whether to build the Orders page. LoopWasKey is what
        /// the switch was before the run, put back by Stop.
        /// </summary>
        private const string LoopForceKey = "PlayModeScreenshot.DailyLoopForce";
        private const string LoopWasKey = "PlayModeScreenshot.DailyLoopWas";

        /// <summary>The sinks' switch for this run, the daily loop's pair over again: 1 forces
        /// SinksSwitch ON for the sink targets, 0 leaves it as the build has it. SinksWasKey is what
        /// it was before the run, put back by Stop.</summary>
        private const string SinksForceKey = "PlayModeScreenshot.SinksForce";
        private const string SinksWasKey = "PlayModeScreenshot.SinksWas";

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
        private const string BuildMarker = "FIXV24-sinks-targets";

        // THE WATCHDOG's state (CaptureWatchdog decides, this class acts). All of it lives in
        // SessionState because the domain reload that entering or leaving Play triggers wipes statics.
        // A "stamp" is EditorApplication.timeSinceStartup, which keeps counting across a reload.
        private const string RequestedKey = "PlayModeScreenshot.Requested";
        private const string WdEnabledKey = "PlayModeScreenshot.WdEnabled";
        private const string WdAttemptKey = "PlayModeScreenshot.WdAttempt";
        private const string WdEntryAtKey = "PlayModeScreenshot.WdEntryAt";
        private const string WdBeatKey = "PlayModeScreenshot.WdBeat";
        private const string WdForceKey = "PlayModeScreenshot.WdForce";
        private const string WdRecoveringKey = "PlayModeScreenshot.WdRecovering";
        private const string WdEditSinceKey = "PlayModeScreenshot.WdEditSince";

        /// <summary>The attempt number the next Begin starts at (1 when unset). ScratchCaptures sets
        /// it to 2 for the first target of a relaunched batch; Begin reads it and puts it back.</summary>
        public const string NextAttemptKey = "PlayModeScreenshot.NextAttempt";

        /// <summary>Set to the target when the watchdog marks it FAILED, so ScratchCaptures can say
        /// it is moving on. ScratchCaptures clears it.</summary>
        public const string FailedKey = "PlayModeScreenshot.FailedTarget";

        static PlayModeScreenshot()
        {
            Debug.Log("PlayModeScreenshot: loaded, build marker " + BuildMarker);
            ApplyLoopForce();
            ApplySinksForce();
            EditorApplication.update += Pump;
            EditorApplication.update += PollTriggerFile;
            EditorApplication.update += WatchdogTick;
            // Log-only quit instrumentation (see LogQuit): never blocks a quit.
            EditorApplication.wantsToQuit += delegate { LogQuit("wantsToQuit"); return true; };
            EditorApplication.quitting += delegate { LogQuit("quitting"); };
            // An interrupted capture must not leave the consent prompt switched on for a manual
            // editor Play: the key is cleared whenever Play mode ends, whoever ended it.
            EditorApplication.playModeStateChanged += delegate (PlayModeStateChange change)
            {
                if (change == PlayModeStateChange.ExitingPlayMode || change == PlayModeStateChange.EnteredEditMode)
                    SessionState.SetBool(TelemetryService.PromptInEditorKey, false);
                if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
                    Debug.Log("PlayModeScreenshot: entered play mode, " + SecondsSinceEntry().ToString("F1")
                              + " s after the entry request");
            };
        }

        /// <summary>Applies the run's daily-loop switch. Called on every domain load and again from
        /// Begin, because with domain reload off the static constructor does not run again.</summary>
        private static void ApplyLoopForce()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            int force = SessionState.GetInt(LoopForceKey, 0);
            if (force == 1) DailyLoopSwitch.Enabled = true;
            else if (force == 2) DailyLoopSwitch.Enabled = false;
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

        // ------------------------------------------------------------------ onboarding targets
        // DESIGN-ONBOARDING.md's verification: six frames of the first session, each on a NEW
        // player - the scratch save is deleted and the first run is NOT suppressed for these, so
        // the game boots exactly as an install does. Every other target keeps the suppression and
        // the gate bypass (FeatureGates.CaptureBypass), so they photograph their screens as before.

        [MenuItem("Emberfall/Capture/Onboarding/Wake", false, 90)]
        public static void CaptureOnboardWake() { Begin("Screen:OnboardWake"); }

        [MenuItem("Emberfall/Capture/Onboarding/Cinder recall", false, 91)]
        public static void CaptureOnboardRecall() { Begin("Screen:OnboardRecall"); }

        [MenuItem("Emberfall/Capture/Onboarding/Recall result", false, 92)]
        public static void CaptureOnboardRecallResult() { Begin("Screen:OnboardRecallResult"); }

        [MenuItem("Emberfall/Capture/Onboarding/Forced-arrow step", false, 93)]
        public static void CaptureOnboardArrow() { Begin("Screen:OnboardArrow"); }

        [MenuItem("Emberfall/Capture/Onboarding/Hub with locked features", false, 94)]
        public static void CaptureOnboardLocks() { Begin("Screen:OnboardLocks"); }

        [MenuItem("Emberfall/Capture/Onboarding/First-time tip", false, 95)]
        public static void CaptureOnboardTip() { Begin("Screen:OnboardTip"); }

        // The first session in order, one target per moment, for DESIGN-ONBOARDING.md's walk-through:
        // wake and recall above, then the prologue, the six guided steps and the locked Hub. Each
        // sets its player up through Onboarding's own calls and the game's own stage resolution, and
        // each refuses a profile that is not a new one.

        [MenuItem("Emberfall/Capture/Onboarding/Prologue battle", false, 96)]
        public static void CaptureOnboardPrologue() { Begin("Screen:OnboardPrologue"); }

        [MenuItem("Emberfall/Capture/Onboarding/Step 1 team (formation)", false, 97)]
        public static void CaptureOnboardTeam() { Begin("Screen:OnboardTeam"); }

        [MenuItem("Emberfall/Capture/Onboarding/Step 2 campaign (enter battle)", false, 98)]
        public static void CaptureOnboardCampaign() { Begin("Screen:OnboardCampaign"); }

        [MenuItem("Emberfall/Capture/Onboarding/Step 2 stage 1-1 (battle)", false, 99)]
        public static void CaptureOnboardStage1() { Begin("Screen:OnboardStage1"); }

        [MenuItem("Emberfall/Capture/Onboarding/Step 3 wave result", false, 100)]
        public static void CaptureOnboardWaveResult() { Begin("Screen:OnboardWaveResult"); }

        [MenuItem("Emberfall/Capture/Onboarding/Step 4 stage 1-2 (battle)", false, 101)]
        public static void CaptureOnboardStage2() { Begin("Screen:OnboardStage2"); }

        [MenuItem("Emberfall/Capture/Onboarding/Step 5 level up", false, 102)]
        public static void CaptureOnboardLevelUp() { Begin("Screen:OnboardLevelUp"); }

        [MenuItem("Emberfall/Capture/Onboarding/Step 6 idle rewards", false, 103)]
        public static void CaptureOnboardIdle() { Begin("Screen:OnboardIdle"); }

        // The play stats (Docs/DESIGN-TELEMETRY.md). The consent prompt is held off in the editor so
        // every target above shows exactly what it always showed; only the first two let it up. All
        // four run on a scratch identity with no answer on file, and refuse with [FAIL] if they do
        // not start there.

        [MenuItem("Emberfall/Capture/Telemetry/Consent prompt (over the Recall boundary)", false, 104)]
        public static void CaptureOnboardConsent() { Begin("Screen:OnboardConsent"); }

        [MenuItem("Emberfall/Capture/Telemetry/Consent answered No (then the Recall card)", false, 105)]
        public static void CaptureOnboardConsentAnswered() { Begin("Screen:OnboardConsentAnswered"); }

        [MenuItem("Emberfall/Capture/Telemetry/Settings, stats on", false, 106)]
        public static void CaptureSettingsStatsOn() { Begin("Screen:SettingsStatsOn"); }

        [MenuItem("Emberfall/Capture/Telemetry/Settings, stats off", false, 107)]
        public static void CaptureSettingsStatsOff() { Begin("Screen:SettingsStatsOff"); }

        [MenuItem("Emberfall/Capture/Hub (MainMenu)", false, 60)]
        public static void CaptureHubScreen() { Begin("Screen:MainMenu"); }

        // THE DAILY LOOP (DESIGN-DAILY-LOOP.md). Each of these forces DailyLoopSwitch ON for its run,
        // seeds the state through DailyLoop's own calls, and photographs the Orders page or the Hub.
        // HubLoopOff forces it OFF and photographs the Hub the way a build without the loop draws it.
        [MenuItem("Emberfall/Capture/Daily loop/Orders, fresh", false, 90)]
        public static void CaptureOrders() { Begin("Screen:Orders"); }

        [MenuItem("Emberfall/Capture/Daily loop/Orders, half done", false, 91)]
        public static void CaptureOrdersHalf() { Begin("Screen:OrdersHalf"); }

        [MenuItem("Emberfall/Capture/Daily loop/Orders, all claimed with the chest", false, 92)]
        public static void CaptureOrdersClaimed() { Begin("Screen:OrdersClaimed"); }

        [MenuItem("Emberfall/Capture/Daily loop/Calendar, day 1", false, 93)]
        public static void CaptureOrdersCalendar1() { Begin("Screen:OrdersCalendar1"); }

        [MenuItem("Emberfall/Capture/Daily loop/Calendar, day 4", false, 94)]
        public static void CaptureOrdersCalendar4() { Begin("Screen:OrdersCalendar4"); }

        [MenuItem("Emberfall/Capture/Daily loop/Hub with the entry", false, 95)]
        public static void CaptureHubLoopOn() { Begin("Screen:HubLoopOn"); }

        [MenuItem("Emberfall/Capture/Daily loop/Hub with the loop off", false, 96)]
        public static void CaptureHubLoopOff() { Begin("Screen:HubLoopOff"); }

        // THE SINKS (DESIGN-SINKS.md). Each forces SinksSwitch ON for its run and seeds the scratch
        // profile through SinkShop.Buy. Screen:Proving and Screen:Recital at the default switch are the
        // rollback shots: the same two pages with no door.
        [MenuItem("Emberfall/Capture/Sinks/Armory", false, 100)]
        public static void CaptureArmory() { Begin("Screen:Armory"); }

        [MenuItem("Emberfall/Capture/Sinks/Armory, after buying", false, 101)]
        public static void CaptureArmoryBought() { Begin("Screen:ArmoryBought"); }

        [MenuItem("Emberfall/Capture/Sinks/Armory, sold out", false, 102)]
        public static void CaptureArmorySoldOut() { Begin("Screen:ArmorySoldOut"); }

        [MenuItem("Emberfall/Capture/Sinks/Armory, hero picker", false, 103)]
        public static void CaptureArmoryPicker() { Begin("Screen:ArmoryPicker"); }

        [MenuItem("Emberfall/Capture/Sinks/Archive", false, 104)]
        public static void CaptureArchive() { Begin("Screen:Archive"); }

        [MenuItem("Emberfall/Capture/Sinks/Archive, after buying", false, 105)]
        public static void CaptureArchiveBought() { Begin("Screen:ArchiveBought"); }

        [MenuItem("Emberfall/Capture/Sinks/Archive, sold out", false, 106)]
        public static void CaptureArchiveSoldOut() { Begin("Screen:ArchiveSoldOut"); }

        [MenuItem("Emberfall/Capture/Sinks/Archive, hero picker", false, 107)]
        public static void CaptureArchivePicker() { Begin("Screen:ArchivePicker"); }

        [MenuItem("Emberfall/Capture/Sinks/Proving with the Armory door", false, 108)]
        public static void CaptureProvingSinksOn() { Begin("Screen:ProvingSinksOn"); }

        [MenuItem("Emberfall/Capture/Sinks/Recital with the Archive door", false, 109)]
        public static void CaptureRecitalSinksOn() { Begin("Screen:RecitalSinksOn"); }

        [MenuItem("Emberfall/Capture/Sinks/Proving with the sinks off", false, 110)]
        public static void CaptureProvingSinksOff() { Begin("Screen:ProvingSinksOff"); }

        [MenuItem("Emberfall/Capture/Sinks/Recital with the sinks off", false, 111)]
        public static void CaptureRecitalSinksOff() { Begin("Screen:RecitalSinksOff"); }

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

        // The Battle stats panel over the lost wave (opens on the lost battle) and over the stage
        // clear (opens on the whole wave).
        [MenuItem("Emberfall/Capture/Battle stats (lost wave)", false, 85)]
        public static void CaptureWaveStatsLost() { Begin("Screen:WaveStatsLost"); }

        [MenuItem("Emberfall/Capture/Battle stats (stage cleared)", false, 85)]
        public static void CaptureWaveStatsCleared() { Begin("Screen:WaveStatsCleared"); }

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
        ///     Screen:Battle=ch2_6            fights stage ch2_6, writes screen_Battle=ch2_6.png
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
            Begin(target, 0);
        }

        /// <summary>attemptOverride is 0 for "read NextAttemptKey" (1 when unset); the watchdog
        /// passes 2 for its one re-entry.</summary>
        private static void Begin(string target, int attemptOverride)
        {
            string requested = target;
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

            // Screen:BattleMoment=<stage>.<kind>[.<aspect>][.off]: a dot and not a colon, because the
            // text after the = is copied into the PNG's name and a colon cannot be in a Windows file
            // name. A stage id has no dot. The stage rides in battleStage as for Battle.
            string moment = "";
            if (target == "Screen:BattleMoment")
            {
                int dot = battleStage.IndexOf('.');
                if (dot > 0)
                {
                    moment = battleStage.Substring(dot + 1);
                    battleStage = battleStage.Substring(0, dot);
                }
            }
            SessionState.SetString(MomentKey, moment);
            SessionState.SetInt(MomentStageKey, 0);
            Juice.ForceForCapture(null);
            StageFeel.ForceForCapture(null);
            Time.captureFramerate = 0;

            if (!EditorScratchIdentity.Enter(ScratchProduct))
            {
                Debug.LogError("PlayModeScreenshot: could not switch to the scratch identity. "
                               + "Refusing to enter Play mode on the real one.");
                return;
            }

            // An onboarding target is a NEW player: no save on the scratch identity, no PlayerPrefs
            // mirror of one, and the first run left on. Every other target is the opposite, set
            // here too because a reused scratch identity keeps whatever the last run left.
            if (target.StartsWith(OnboardPrefix))
            {
                if (EditorScratchIdentity.IsScratch)
                {
                    string save = Path.Combine(Application.persistentDataPath, SaveService.FileName);
                    if (File.Exists(save)) File.Delete(save);
                    PlayerPrefs.DeleteAll();
                }
                EditorPrefs.SetBool(GameApp.SuppressFirstRunKey, false);
            }
            else EditorPrefs.SetBool(GameApp.SuppressFirstRunKey, true);

            // EVERY target starts with no play-stats answer on file, so the scratch device file is
            // removed (scratch only: the real one is never touched) and Screen:Settings and
            // Screen:ALL are the same picture every night. The consent prompt is allowed up for the
            // two targets that photograph it and for no other.
            bool consentTarget = target == "Screen:OnboardConsent" || target == "Screen:OnboardConsentAnswered";
            if (EditorScratchIdentity.IsScratch)
            {
                string telemetryFile = Path.Combine(Application.persistentDataPath, TelemetryService.FileName);
                if (File.Exists(telemetryFile)) File.Delete(telemetryFile);
            }
            SessionState.SetBool(TelemetryService.PromptInEditorKey, consentTarget);

            if (!GameViewSize.Apply(width, height))
            {
                Debug.LogWarning("PlayModeScreenshot: could not pin the Game view to "
                                 + width + "x" + height + "; the capture will use"
                                 + " whatever size it is currently at and its layout is unproven.");
            }
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(LoopForceKey, LoopForceFor(target));
            SessionState.SetBool(LoopWasKey, DailyLoopSwitch.Enabled);
            ApplyLoopForce();
            SessionState.SetInt(SinksForceKey, SinksForceFor(target));
            SessionState.SetBool(SinksWasKey, SinksSwitch.Enabled);
            ApplySinksForce();
            SessionState.SetBool(VerifiedKey, false);
            SessionState.SetString(TargetKey, target);
            SessionState.SetInt(WidthKey, width);
            SessionState.SetInt(HeightKey, height);
            SessionState.SetString(SuffixKey, suffix);
            SessionState.SetString(BattleStageKey, battleStage);
            SessionState.SetBool(OnboardReadyKey, false);
            SessionState.SetInt(FrameKey, 0);
            SessionState.SetInt(StageKey, 0);
            // Both of these survive a domain reload, so a second run in the same editor session
            // would inherit the first run's arming frame and shoot immediately if they were left.
            SessionState.SetInt(LastShotKey, 0);
            SessionState.SetInt(BattleArmKey, 0);
            StartEntry(requested, target, attemptOverride);
            EditorCaptureGuard.EnterPlayMode();
        }

        /// <summary>Records one play entry for the watchdog and logs it. The line "play entry
        /// requested" is printed in the edit-mode domain before Play starts, so a log that ends right
        /// after it (and after Unity's backup-scene load) is a lost entry, and capture-batch.ps1 reads
        /// it as such.</summary>
        private static void StartEntry(string requested, string target, int attemptOverride)
        {
            int attempt = attemptOverride > 0 ? attemptOverride : SessionState.GetInt(NextAttemptKey, 1);
            SessionState.SetInt(NextAttemptKey, 1);
            bool enabled = CaptureWatchdog.SwitchOn(Environment.GetEnvironmentVariable(CaptureWatchdog.SwitchVariable));
            bool forced = CaptureWatchdog.ForcesMiss(
                Environment.GetEnvironmentVariable(CaptureWatchdog.ForceMissVariable), requested, target, attempt);

            SessionState.SetString(RequestedKey, requested);
            SessionState.SetBool(WdEnabledKey, enabled);
            SessionState.SetInt(WdAttemptKey, attempt);
            SessionState.SetString(WdEntryAtKey, Stamp(EditorApplication.timeSinceStartup));
            SessionState.SetBool(WdBeatKey, false);
            SessionState.SetBool(WdForceKey, forced);
            SessionState.SetBool(WdRecoveringKey, false);
            SessionState.SetString(WdEditSinceKey, "");

            if (enabled)
                Debug.Log("PlayModeScreenshot: watchdog mode ON (no heartbeat after " + CaptureWatchdog.FirstEntryLimitSeconds
                          + " s recovers; " + CaptureWatchdog.SwitchVariable + "=0 switches it off)");
            else
                Debug.LogWarning("PlayModeScreenshot: watchdog mode OFF (" + CaptureWatchdog.SwitchVariable + "=0); a lost play entry will not be recovered");
            if (forced)
                Debug.LogWarning("PlayModeScreenshot: FORCE-MISS hook active for " + requested + " (" + CaptureWatchdog.ForceMissVariable
                                 + "): this first play entry will send no heartbeat and do no work");
            Debug.Log("PlayModeScreenshot: play entry requested, target " + requested + ", attempt " + attempt
                      + ", watchdog " + (enabled ? "on" : "off"));
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

        /// <summary>The daily-loop targets: the Orders page in its five states and the Hub with the
        /// loop on. Their names are matched whole so no other target can pick the switch up.</summary>
        private static bool IsLoopScreen(string name)
        {
            switch (name)
            {
                case "Orders":
                case "OrdersHalf":
                case "OrdersClaimed":
                case "OrdersCalendar1":
                case "OrdersCalendar4":
                case "HubLoopOn":
                    return true;
            }
            return false;
        }

        private static int LoopForceFor(string target)
        {
            if (!target.StartsWith("Screen:")) return 0;
            string name = target.Substring(7);
            if (IsLoopScreen(name)) return 1;
            return name == "HubLoopOff" ? 2 : 0;
        }

        // A HEARTBEAT, because a run that goes quiet after Play mode starts cannot tell "the editor
        // stopped ticking" from "ticking but not playing" from "ticking, playing, and the pump
        // returning early". One line the first time the pump sees an active run in this domain, then
        // one every 600 ticks while the run is active.
        private static bool _beatSeen;
        private static int _beatTicks;

        // The first ForceGameViewRepaint of a domain is bracketed by two lines. A log with "repaint call
        // start" and no "repaint call returned" means the call blocked. Both static flags reset with the
        // domain, so each domain prints its own pair.
        private static bool _repaintLogged;
        private static bool _repaintReturned;
        private static bool _forceMissLogged;

        private static string Stamp(double seconds)
        {
            return seconds.ToString("R", CultureInfo.InvariantCulture);
        }

        private static double ReadStamp(string key, double fallback)
        {
            double value;
            if (double.TryParse(SessionState.GetString(key, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return value;
            return fallback;
        }

        private static double SecondsSinceEntry()
        {
            double now = EditorApplication.timeSinceStartup;
            return now - ReadStamp(WdEntryAtKey, now);
        }

        /// <summary>
        /// Says that the editor is quitting while a capture run is active, with the managed stack
        /// and the run's state. A single launch of Screen:Events once exited cleanly (code 0) right
        /// after "entered play mode", with no Pump tick and none of our own exit lines; the next time
        /// this tells the two kinds of quit apart. A quit that our code asked for (EditorApplication.Exit)
        /// shows the caller in the stack; one asked for from outside (a window close, a signal)
        /// shows engine frames only. Silent when no capture run is active, so ordinary editor use
        /// is not noisy, and it never blocks the quit.
        /// </summary>
        private static void LogQuit(string which)
        {
            bool capture = SessionState.GetBool(ActiveKey, false) || SessionState.GetBool("ScratchCaptures.Active", false);
            if (!capture) return;
            Debug.LogWarning("PlayModeScreenshot: editor QUIT (" + which + ") while a capture run is active. isPlaying " + EditorApplication.isPlaying
                             + ", PlayModeScreenshot.Active " + SessionState.GetBool(ActiveKey, false)
                             + ", ScratchCaptures.Active " + SessionState.GetBool("ScratchCaptures.Active", false)
                             + ", ScratchCaptures.Waiting " + SessionState.GetBool("ScratchCaptures.Waiting", false)
                             + ", ScratchCaptures.ExitAt " + SessionState.GetInt("ScratchCaptures.ExitAt", 0)
                             + ", target " + SessionState.GetString(RequestedKey, SessionState.GetString(TargetKey, ""))
                             + ", heartbeat seen " + SessionState.GetBool(WdBeatKey, false)
                             + ". Stack: " + Environment.StackTrace);
        }

        private static string StateLine()
        {
            return "isPlaying " + EditorApplication.isPlaying
                   + ", isPlayingOrWillChangePlaymode " + EditorApplication.isPlayingOrWillChangePlaymode
                   + ", frame " + SessionState.GetInt(FrameKey, 0)
                   + ", repaint call start printed " + _repaintLogged
                   + ", repaint call returned printed " + _repaintReturned
                   + ", GameApp present " + (GameApp.Instance != null);
        }

        /// <summary>
        /// Runs on every editor tick while a run is active. Asks CaptureWatchdog what to do and does
        /// it: Recover logs the state and exits Play (Unity removes its own backup-scene folder when
        /// Play ends normally; this never touches those files), ReEnter begins the same target again,
        /// MarkFailed gives up on the target so ScratchCaptures moves to the next one.
        /// If the editor's update loop itself is dead this never runs, which is what the batch
        /// launcher's outer watchdog is for.
        /// </summary>
        private static void WatchdogTick()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (!SessionState.GetBool(WdEnabledKey, false)) return;
            bool beat = SessionState.GetBool(WdBeatKey, false);
            if (beat) return;

            double now = EditorApplication.timeSinceStartup;
            bool recovering = SessionState.GetBool(WdRecoveringKey, false);
            double sinceEntry = now - ReadStamp(WdEntryAtKey, now);
            double inEditMode = -1.0;
            if (recovering)
            {
                // Time is counted only while the editor is out of Play, so Unity's own exit is not
                // part of the 5 s.
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    SessionState.SetString(WdEditSinceKey, "");
                }
                else
                {
                    if (SessionState.GetString(WdEditSinceKey, "").Length == 0)
                        SessionState.SetString(WdEditSinceKey, Stamp(now));
                    inEditMode = now - ReadStamp(WdEditSinceKey, now);
                }
            }

            int attempt = SessionState.GetInt(WdAttemptKey, 1);
            string requested = SessionState.GetString(RequestedKey, SessionState.GetString(TargetKey, ""));
            switch (CaptureWatchdog.Decide(true, beat, attempt, recovering, sinceEntry, inEditMode))
            {
                case WatchdogAction.Recover:
                    Debug.LogWarning("PlayModeScreenshot: watchdog RECOVER " + requested + " attempt " + attempt
                                     + ": no play heartbeat " + sinceEntry.ToString("F1") + " s after the entry request. State: "
                                     + StateLine() + ". Exiting Play mode.");
                    SessionState.SetBool(WdRecoveringKey, true);
                    SessionState.SetString(WdEditSinceKey, "");
                    EditorApplication.ExitPlaymode();
                    break;

                case WatchdogAction.ReEnter:
                    Debug.Log("PlayModeScreenshot: watchdog re-entering " + requested + " as attempt " + (attempt + 1)
                              + " after " + inEditMode.ToString("F1") + " s in edit mode");
                    SessionState.SetBool(WdRecoveringKey, false);
                    Begin(requested, attempt + 1);
                    // Begin returns without starting when it cannot reach the scratch identity.
                    if (SessionState.GetInt(WdAttemptKey, 1) <= attempt) FailTarget(requested, "the re-entry could not start");
                    break;

                case WatchdogAction.MarkFailed:
                    Debug.LogWarning("PlayModeScreenshot: watchdog gives up on " + requested + " attempt " + attempt
                                     + ": no play heartbeat " + sinceEntry.ToString("F1") + " s after the entry request. State: " + StateLine());
                    FailTarget(requested, "a missed heartbeat on two play entries");
                    break;
            }
        }

        private static void FailTarget(string requested, string why)
        {
            Debug.LogError("PlayModeScreenshot: FAILED " + requested + " after " + why);
            SessionState.SetString(FailedKey, requested);
            SessionState.SetBool(WdRecoveringKey, false);
            Stop();
        }

        private static void Pump()
        {
            bool runActive = SessionState.GetBool(ActiveKey, false);
            if (runActive && (!_beatSeen || ++_beatTicks % 600 == 0))
            {
                _beatSeen = true;
                Debug.Log("PlayModeScreenshot: heartbeat, run active, playing " + EditorApplication.isPlaying
                          + ", playing or about to " + EditorApplication.isPlayingOrWillChangePlaymode
                          + ", frame " + SessionState.GetInt(FrameKey, 0) + ", GameApp " + (GameApp.Instance != null));
            }
            if (!runActive) return;
            if (!EditorApplication.isPlaying) return; // still transitioning in, or already stopped

            // A Recover is under way: the watchdog has asked Play to end. A slow entry that reaches
            // isPlaying now must record no heartbeat and do no work, or the run would stay Active
            // with no Play left to finish it.
            if (SessionState.GetBool(WdRecoveringKey, false)) return;

            // The test hook: this play entry sends no heartbeat and does no work, so the watchdog has
            // a lost entry to recover. The second entry for the target is not forced.
            if (SessionState.GetBool(WdForceKey, false))
            {
                if (!_forceMissLogged)
                {
                    _forceMissLogged = true;
                    Debug.LogWarning("PlayModeScreenshot: FORCE-MISS hook, Pump returns without a heartbeat for this play entry");
                }
                return;
            }

            // The heartbeat the watchdog waits for: the first tick of this play entry that finds the
            // editor really playing. WatchdogTick stops counting once it is set.
            if (!SessionState.GetBool(WdBeatKey, false))
            {
                SessionState.SetBool(WdBeatKey, true);
                Debug.Log("PlayModeScreenshot: play heartbeat, target " + SessionState.GetString(RequestedKey, SessionState.GetString(TargetKey, ""))
                          + ", attempt " + SessionState.GetInt(WdAttemptKey, 1) + ", " + SecondsSinceEntry().ToString("F1")
                          + " s after the entry request");
            }

            if (!_repaintLogged)
            {
                _repaintLogged = true;
                double startedAt = EditorApplication.timeSinceStartup;
                Debug.Log("PlayModeScreenshot: repaint call start, editor time " + startedAt.ToString("F3") + " s");
                ForceGameViewRepaint();
                Debug.Log("PlayModeScreenshot: repaint call returned, editor time "
                          + EditorApplication.timeSinceStartup.ToString("F3") + " s, took "
                          + ((EditorApplication.timeSinceStartup - startedAt) * 1000.0).ToString("F1") + " ms");
                _repaintReturned = true;
            }
            else
            {
                ForceGameViewRepaint();
            }

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
                    Debug.LogError("PlayModeScreenshot: [FAIL] the run is not offline on a scratch identity. Stopping without a capture.");
                    Stop();
                    return;
                }
            }

            // Every target but the onboarding ones photographs a feature's screen on a fresh
            // profile that has cleared nothing, so they walk past the unlock table.
            FeatureGates.CaptureBypass = !target.StartsWith(OnboardPrefix);

            if (target == "Recital") PumpRecital(frame);
            else if (target == "Proving") PumpProving(frame);
            else if (target.StartsWith(OnboardPrefix)) PumpOnboard(frame, target.Substring(7));
            else if (target.StartsWith("Screen:SettingsStats")) PumpSettingsStats(frame, target.Substring(7));
            else if (target.StartsWith("Screen:")) PumpScreen(frame, target.Substring(7));
            else PumpEvents(frame);
        }

        private const string OnboardPrefix = "Screen:Onboard";

        /// <summary>
        /// One frame of the first session. The game boots as a new player at the wake; this drives
        /// Onboarding's own calls - the same ones the coach's buttons make - to the frame asked for,
        /// lets the coach draw it, and shoots. It refuses a session that did not boot as a new
        /// player, since a capture of the wrong player is a capture of the wrong screen.
        /// </summary>
        private static void PumpOnboard(int frame, string name)
        {
            GameApp app = GameApp.Instance;
            if (app == null)
            {
                if (frame > 300) { Debug.LogError("PlayModeScreenshot: [FAIL] " + name + ": GameApp.Instance never appeared."); Stop(); }
                return;
            }
            int stage = SessionState.GetInt(StageKey, 0);
            if (stage == 0 && frame >= 30)
            {
                // Every refusal below ends the Play session on the spot and says [FAIL], so a queue
                // (ScratchCaptures, the nightly) moves on to its next target and the log names the
                // one that did not shoot, instead of sitting in Play mode until a watchdog fires.
                // Nothing here may write a save, so a profile that is not the scratch one is
                // refused before any of the set-up runs.
                if (!EditorScratchIdentity.IsScratch)
                {
                    Debug.LogError("PlayModeScreenshot: [FAIL] " + name + " is not running on the scratch identity. Not shooting.");
                    Stop();
                    return;
                }
                if (Array.IndexOf(OnboardTargets, name) < 0)
                {
                    Debug.LogError("PlayModeScreenshot: [FAIL] '" + name + "' is not an onboarding target. Not shooting.");
                    Stop();
                    return;
                }
                PlayerProfile profile = app.Session.Profile;
                if (profile.TutorialStep != Onboarding.StepWake || profile.ClearedStageIds.Count > 0)
                {
                    Debug.LogError("PlayModeScreenshot: [FAIL] " + name + " needs a NEW player and this one is at step "
                                   + profile.TutorialStep + " with " + profile.ClearedStageIds.Count + " stages cleared. Not shooting.");
                    Stop();
                    return;
                }
                if (name.StartsWith("OnboardConsent"))
                {
                    TelemetryService stats = TelemetryService.Instance;
                    if (stats == null || !stats.Pending || !SessionState.GetBool(TelemetryService.PromptInEditorKey, false))
                    {
                        Debug.LogError("PlayModeScreenshot: [FAIL] " + name + " needs the play stats installed, with no answer on file and"
                                       + " the editor prompt switch on. Not shooting.");
                        Stop();
                        return;
                    }
                }
                SetUpOnboarding(app, name);
                SessionState.SetInt(StageKey, 1);
                SessionState.SetInt(BattleArmKey, frame);
                return;
            }
            if (stage == 1)
            {
                int armed = SessionState.GetInt(BattleArmKey, 0);

                // A fight and a wave result cannot be timed by a frame count: the shot waits for the
                // battle's first round, or for the wave result to paint, as the plain Battle and
                // WaveResult targets do, and is taken the same BattlePaintDelay after.
                if (OnboardWaitsOnScreen(name))
                {
                    const int ceiling = 2400, paintDelay = 20;
                    if (!SessionState.GetBool(OnboardReadyKey, false))
                    {
                        bool ready = name == "OnboardWaveResult"
                            ? app.Router.WaveResult != null && app.Router.WaveResult.Painted
                            : app.Router.Current != null && app.Router.Current.Id == ScreenId.Battle && app.Router.Battle.RoundUnderway;
                        if (ready || frame > armed + ceiling)
                        {
                            SessionState.SetBool(OnboardReadyKey, true);
                            SessionState.SetInt(BattleArmKey, frame);
                            if (!ready) Debug.LogWarning("PlayModeScreenshot: " + name + " was not ready within " + ceiling + " frames. Shooting anyway.");
                        }
                        return;
                    }
                    armed = SessionState.GetInt(BattleArmKey, 0);
                    if (frame == armed + paintDelay - 5) Canvas.ForceUpdateCanvases();
                    if (frame == armed + paintDelay)
                    {
                        PlayerProfile w = app.Session.Profile;
                        OnboardingCoach wc = OnboardingCoach.Instance;
                        Debug.Log("PlayModeScreenshot: " + name + " at step " + w.TutorialStep + " on "
                                  + (app.Router.Current != null ? app.Router.Current.Id.ToString() : "none")
                                  + ", cleared " + w.ClearedStageIds.Count + ", gold " + w.Gold);
                        if (wc != null) Debug.Log("PlayModeScreenshot: coach " + wc.DescribeForCapture());
                        Shoot(name, frame, 1);
                    }
                    return;
                }

                if (name == "OnboardTip" && frame == armed + 20) app.Router.Show(ScreenId.Summon);
                if (name.StartsWith("OnboardConsent") && !PumpConsentStep(app, name, frame, armed)) return;
                if (frame == armed + 115) Canvas.ForceUpdateCanvases();
                if (frame == armed + 120)
                {
                    PlayerProfile p = app.Session.Profile;
                    OnboardingCoach coach = OnboardingCoach.Instance;
                    Debug.Log("PlayModeScreenshot: " + name + " at step " + p.TutorialStep + " on "
                              + (app.Router.Current != null ? app.Router.Current.Id.ToString() : "none")
                              + ", Kael owned " + p.Owns("kael_the_unyielding") + ", tip showing "
                              + (coach != null && coach.TipShowing) + ", summons " + p.TotalSummons);
                    if (coach != null) Debug.Log("PlayModeScreenshot: coach " + coach.DescribeForCapture());
                    Shoot(name, frame, 1);
                }
                return;
            }
            if (stage >= 2 && frame > SessionState.GetInt(LastShotKey, 0) + 60)
            {
                Debug.Log("PlayModeScreenshot: onboarding capture done.");
                Stop();
            }
        }

        /// <summary>
        /// The consent targets' own steps. At 40 frames the answered target presses NO through the
        /// button's click, the path a finger takes; at 119 both check the state the picture is meant
        /// to show, so a prompt that never came up, or an answer that did not land, ends the run with
        /// [FAIL] instead of a picture of the wrong thing. False means the run was stopped.
        /// </summary>
        private static bool PumpConsentStep(GameApp app, string name, int frame, int armed)
        {
            TelemetryService stats = TelemetryService.Instance;
            TelemetryConsentPrompt prompt = TelemetryConsentPrompt.Instance;
            PlayerProfile profile = app.Session.Profile;
            if (frame == armed + 40 && name == "OnboardConsentAnswered")
            {
                if (prompt == null || !TelemetryConsentPrompt.Showing)
                {
                    Debug.LogError("PlayModeScreenshot: [FAIL] " + name + ": the prompt was not up to answer. Not shooting.");
                    Stop();
                    return false;
                }
                prompt.ClickForCapture(false);
            }
            if (frame == armed + 119)
            {
                bool shown = TelemetryConsentPrompt.Showing;
                string coachState = OnboardingCoach.Instance != null ? OnboardingCoach.Instance.DescribeForCapture() : "";
                bool ok = stats != null && profile.TutorialStep == Onboarding.StepRecall
                          && (name == "OnboardConsent"
                              ? shown && stats.Pending
                              : !shown && !stats.Pending && !stats.IsOn && coachState.Contains("mode Recall"));
                Debug.Log("PlayModeScreenshot: " + name + " prompt showing " + shown + ", pending " + (stats != null && stats.Pending)
                          + ", stats on " + (stats != null && stats.IsOn) + ", step " + profile.TutorialStep);
                if (!ok)
                {
                    Debug.LogError("PlayModeScreenshot: [FAIL] " + name + ": the state to photograph is not there. Not shooting.");
                    Stop();
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// The Settings page with the PLAY STATS card in one state. The answer is given through
        /// TelemetryService.Answer, the very call the card's button makes, then the page is shown
        /// and the card must be on it. Refuses a device that already has an answer.
        /// </summary>
        private static void PumpSettingsStats(int frame, string name)
        {
            GameApp app = GameApp.Instance;
            if (app == null)
            {
                if (frame > 300) { Debug.LogError("PlayModeScreenshot: [FAIL] " + name + ": GameApp.Instance never appeared."); Stop(); }
                return;
            }
            bool wantOn = name == "SettingsStatsOn";
            int stage = SessionState.GetInt(StageKey, 0);
            TelemetryService stats = TelemetryService.Instance;
            if (stage == 0 && frame >= 30)
            {
                if (!EditorScratchIdentity.IsScratch)
                {
                    Debug.LogError("PlayModeScreenshot: [FAIL] " + name + " is not running on the scratch identity. Not shooting.");
                    Stop();
                    return;
                }
                if (stats == null || !stats.Pending || stats.IsOn)
                {
                    Debug.LogError("PlayModeScreenshot: [FAIL] " + name + " needs the play stats installed with no answer on file. Not shooting.");
                    Stop();
                    return;
                }
                stats.Answer(wantOn);
                app.Router.Show(ScreenId.Settings, false);
                SessionState.SetInt(StageKey, 1);
                SessionState.SetInt(BattleArmKey, frame);
                return;
            }
            if (stage == 1)
            {
                int armed = SessionState.GetInt(BattleArmKey, 0);
                if (frame == armed + 85) Canvas.ForceUpdateCanvases();
                if (frame == armed + 90)
                {
                    bool onSettings = app.Router.Current != null && app.Router.Current.Id == ScreenId.Settings;
                    bool cardThere = onSettings && OnboardingCoach.FindDeep(app.Router.Current.Root, "PlayStats") != null;
                    Debug.Log("PlayModeScreenshot: " + name + " on settings " + onSettings + ", card present " + cardThere + ", stats on " + (stats != null && stats.IsOn));
                    if (stats == null || stats.IsOn != wantOn || !cardThere)
                    {
                        Debug.LogError("PlayModeScreenshot: [FAIL] " + name + ": the card is not on the page in the state asked for. Not shooting.");
                        Stop();
                        return;
                    }
                    Shoot(name, frame, 1);
                }
                return;
            }
            if (stage >= 2 && frame > SessionState.GetInt(LastShotKey, 0) + 60)
            {
                Debug.Log("PlayModeScreenshot: settings stats capture done.");
                Stop();
            }
        }

        /// <summary>Every onboarding target, by the name that follows "Screen:" and is also the
        /// name of the file it writes (screen_{name}.png), which is what Tools/nightly.ps1 expects.</summary>
        private static readonly string[] OnboardTargets =
        {
            "OnboardWake", "OnboardRecall", "OnboardRecallResult", "OnboardArrow", "OnboardLocks", "OnboardTip",
            "OnboardPrologue", "OnboardTeam", "OnboardCampaign", "OnboardStage1", "OnboardWaveResult",
            "OnboardStage2", "OnboardLevelUp", "OnboardIdle", "OnboardConsent", "OnboardConsentAnswered"
        };

        private static bool OnboardWaitsOnScreen(string name)
        {
            return name == "OnboardPrologue" || name == "OnboardStage1" || name == "OnboardStage2" || name == "OnboardWaveResult";
        }

        /// <summary>
        /// Walks a new profile up to the guided step asked for with the calls the coach and the
        /// campaign make, never by setting the counter: the wake, the recall, the prologue, the team
        /// confirmed, then the real stage resolution (GameSession.ResolveWave, the same call the wave
        /// result screen makes) for each stage a step needs cleared and a real level bought for the
        /// level up. Returns at the step; a step past StepCampaign needs 1-1 cleared first, which is
        /// what the coach's own poll would have completed.
        /// </summary>
        private static void WalkOnboardingTo(GameApp app, int step)
        {
            GameSession session = app.Session;
            PlayerProfile profile = session.Profile;
            long now = TimeUtil.NowUnix();
            Onboarding.CompleteStep(profile, Onboarding.StepWake);
            if (step <= Onboarding.StepRecall) return;
            Onboarding.Recall(session, now);
            if (step <= Onboarding.StepPrologue) return;
            Onboarding.CompleteStep(profile, Onboarding.StepPrologue);
            if (step <= Onboarding.StepTeam) return;
            Onboarding.EnsureRecallHeroDeployed(session);
            Onboarding.CompleteStep(profile, Onboarding.StepTeam);
            if (step <= Onboarding.StepCampaign) return;

            StageDef first = Onboarding.StageFor(session.Content, Onboarding.StepWaveResult);
            StageDef second = Onboarding.StageFor(session.Content, Onboarding.StepSecondStage);
            int n = 0;
            while (first != null && !profile.HasClearedStage(first.Id) && n < 12)
            {
                session.ResolveWave(first, 7100 + n * 101, now + n);
                n++;
            }
            Onboarding.CompleteStep(profile, Onboarding.StepCampaign);
            Onboarding.CompleteStep(profile, Onboarding.StepWaveResult);
            if (step <= Onboarding.StepSecondStage) return;

            n = 0;
            while (second != null && !profile.HasClearedStage(second.Id) && n < 12)
            {
                session.ResolveWave(second, 7900 + n * 101, now + 100 + n);
                n++;
            }
            Onboarding.CompleteStep(profile, Onboarding.StepSecondStage);
            if (step <= Onboarding.StepLevelUp) return;

            HeroDef recall = Onboarding.RecallHero(session.Content);
            HeroInstance hero = recall != null ? profile.FindHeroByDefId(recall.Id) : null;
            if (hero != null) session.LevelUpHero(hero, 1);
            Onboarding.CompleteStep(profile, Onboarding.StepLevelUp);
        }

        private static void SetUpOnboarding(GameApp app, string name)
        {
            GameSession session = app.Session;
            PlayerProfile profile = session.Profile;
            long now = TimeUtil.NowUnix();
            switch (name)
            {
                case "OnboardWake":
                    break;
                case "OnboardRecall":
                    Onboarding.CompleteStep(profile, Onboarding.StepWake);
                    break;
                case "OnboardConsent":
                case "OnboardConsentAnswered":
                    // The Wake done: the profile stands on the boundary the prompt belongs to, and
                    // the prompt is drawn by the game's own rule, not placed here.
                    Onboarding.CompleteStep(profile, Onboarding.StepWake);
                    break;
                case "OnboardRecallResult":
                    Onboarding.CompleteStep(profile, Onboarding.StepWake);
                    if (OnboardingCoach.Instance != null) OnboardingCoach.Instance.ShowRecallResultForCapture();
                    break;
                case "OnboardPrologue":
                    // The coach starts the prologue battle itself the frame it sees this step.
                    WalkOnboardingTo(app, Onboarding.StepPrologue);
                    break;
                case "OnboardTeam":
                    WalkOnboardingTo(app, Onboarding.StepTeam);
                    app.Router.Show(ScreenId.Formation, false);
                    break;
                case "OnboardCampaign":
                    WalkOnboardingTo(app, Onboarding.StepCampaign);
                    app.Router.Show(ScreenId.Campaign, false);
                    break;
                case "OnboardStage1":
                    WalkOnboardingTo(app, Onboarding.StepCampaign);
                    app.Router.Battle.SetStage(Onboarding.StageFor(session.Content, Onboarding.StepWaveResult));
                    app.Router.Show(ScreenId.Battle, false);
                    break;
                case "OnboardWaveResult":
                    // The first wave of 1-1 resolved by the wave result screen itself, launched the
                    // way the campaign's ENTER BATTLE launches it; showing it is what moves the
                    // coach on from the campaign step to this one.
                    WalkOnboardingTo(app, Onboarding.StepCampaign);
                    CampaignFlow.Launch(app, Onboarding.StageFor(session.Content, Onboarding.StepWaveResult));
                    break;
                case "OnboardStage2":
                    WalkOnboardingTo(app, Onboarding.StepSecondStage);
                    app.Router.Battle.SetStage(Onboarding.StageFor(session.Content, Onboarding.StepSecondStage));
                    app.Router.Show(ScreenId.Battle, false);
                    break;
                case "OnboardLevelUp":
                {
                    WalkOnboardingTo(app, Onboarding.StepLevelUp);
                    HeroDef levelHero = Onboarding.RecallHero(session.Content);
                    HeroInstance levelInstance = levelHero != null ? profile.FindHeroByDefId(levelHero.Id) : null;
                    if (levelInstance != null) app.Router.HeroDetail.SetHero(levelInstance);
                    app.Router.Show(ScreenId.HeroDetail, false);
                    break;
                }
                case "OnboardIdle":
                    WalkOnboardingTo(app, Onboarding.StepIdle);
                    app.Router.GoHome();
                    break;
                case "OnboardArrow":
                    // Guided step 1 of 6 on the Hub: the arrow on HEROES, the line beside it.
                    Onboarding.CompleteStep(profile, Onboarding.StepWake);
                    Onboarding.Recall(session, now);
                    Onboarding.CompleteStep(profile, Onboarding.StepPrologue);
                    break;
                case "OnboardLocks":
                case "OnboardTip":
                    // Free play. The locks capture clears the first four stages so the Hub shows
                    // Friends just unlocked, with its one-time dot, beside everything still locked.
                    Onboarding.Skip(session, now);
                    if (name == "OnboardLocks")
                        for (int i = 0; i < 4 && i < session.Content.AllStages.Count; i++)
                            profile.ClearedStageIds.Add(session.Content.AllStages[i].Id);
                    profile.StagesCleared = CampaignProgress.CountCleared(profile, session.Content);
                    app.Router.GoHome();
                    break;
            }
            app.SaveNow();
            app.RefreshHud();
            Debug.Log("PlayModeScreenshot: set up " + name);
        }

        private static void Stop()
        {
            Time.timeScale = 1f;
            // Put the daily loop's switch back as the run found it, so a forced run cannot leave it
            // forced for the next one in the same editor.
            if (SessionState.GetInt(LoopForceKey, 0) != 0)
            {
                DailyLoopSwitch.Enabled = SessionState.GetBool(LoopWasKey, false);
                SessionState.SetInt(LoopForceKey, 0);
            }
            // And the sinks' switch, for the same reason.
            if (SessionState.GetInt(SinksForceKey, 0) != 0)
            {
                SinksSwitch.Enabled = SessionState.GetBool(SinksWasKey, false);
                SessionState.SetInt(SinksForceKey, 0);
            }
            SessionState.SetInt(ReplayStageKey, 0);
            SessionState.SetInt(MomentStageKey, 0);
            Juice.ForceForCapture(null);
            StageFeel.ForceForCapture(null);
            Time.captureFramerate = 0;
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(TelemetryService.PromptInEditorKey, false);
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
                    // The same page scrolled to its foot: the Credits card is below the fold.
                    case "SettingsCredits": id = ScreenId.Settings; break;
                    case "Guild": id = ScreenId.Guild; break;
                    case "GuildRequests": id = ScreenId.GuildRequests; break;
                    case "GuildChat": id = ScreenId.GuildChat; break;
                    case "Battle": id = ScreenId.Battle; break;
                    case "Orders":
                    case "OrdersHalf":
                    case "OrdersClaimed":
                    case "OrdersCalendar1":
                    case "OrdersCalendar4": id = ScreenId.Orders; break;
                    case "Armory":
                    case "ArmoryBought":
                    case "ArmorySoldOut":
                    case "ArmoryPicker": id = ScreenId.Armory; break;
                    case "Archive":
                    case "ArchiveBought":
                    case "ArchiveSoldOut":
                    case "ArchivePicker": id = ScreenId.Archive; break;
                    case "ProvingSinksOn":
                    case "ProvingSinksOff": id = ScreenId.Proving; break;
                    case "RecitalSinksOn":
                    case "RecitalSinksOff": id = ScreenId.Recital; break;
                    case "Result": id = ScreenId.Battle; break;
                    case "AudioFight": id = ScreenId.Battle; break;
                    case "BattleMoment": id = ScreenId.Battle; break;
                    case "AudioSummon": id = ScreenId.Summon; break;
                    case "AudioRoundTrip": id = ScreenId.MainMenu; break;
                    case "WaveResult":
                    case "WaveResultAuto":
                    case "WaveResultCleared":
                    case "WaveResultLost":
                    case "WaveStatsLost":
                    case "WaveStatsCleared":
                    case "WaveReplay":
                    case "WaveReplayResult":
                    case "WaveReplayHeal": id = ScreenId.WaveResult; break;
                    // The ability breakdown is a DIALOG over the hero page rather than a screen,
                    // so it routes to the page and opens the dialog a frame later, exactly as the
                    // Hub's layers do below.
                    case "AbilityNumbers": id = ScreenId.HeroDetail; break;
                }

                // THE ROSTER SHEET (R15): every unit of one side in one clip, built through the battle's own
                // door, with no UI over it. Screen:RosterSheet=<side>[.<clip>][.v1|.v2]. It shows no screen,
                // so nothing is routed; the generic shot below photographs it. See RosterSheetShots.
                if (name == "RosterSheet")
                {
                    RosterSheetShots.Build(app, SessionState.GetString(BattleStageKey, ""));
                    Debug.Log("PlayModeScreenshot: showing " + name);
                    return;
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

                // THE DAILY LOOP'S STATES are seeded through DailyLoop's own calls, on the same
                // clock the page reads, never by writing the state. HubLoopOff seeds nothing: it
                // photographs the Hub as a build without the loop draws it, and says so loudly if
                // the switch is on or the page exists, since a shot of the wrong build is not evidence.
                if (IsLoopScreen(name)) SeedLoop(app, name);
                if (IsSinksScreen(name)) SeedSinks(app, name);
                if (name == "HubLoopOff") CheckLoopIsOff(app);
                if (name == "ProvingSinksOff" || name == "RecitalSinksOff") CheckSinksAreOff(app);

                // A moment shot may ask for the Legacy profile (".off"), and that has to be in force
                // before the fight is shown, because the profile is read as the fight plays.
                if (name == "BattleMoment")
                    Juice.ForceForCapture(SessionState.GetString(MomentKey, "").EndsWith(".off", StringComparison.Ordinal) ? (bool?)false : null);

                if (name == "Battle" || name == "Result" || name == "AudioFight" || name == "BattleMoment")
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
                    StageDef waveStage = SeedWaveResult(app, name == "WaveReplay" || name == "WaveReplayResult" || name == "WaveReplayHeal" ? "WaveResult" : WaveBase(name));
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
                if (IsSinksScreen(name)) OpenSinksPicker(app, name);
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
                        ReportWaveOutcome(WaveBase(name), waveScreen.Result);
                        // The stats targets open the panel on the scope it opens on by itself.
                        if (name == "WaveStatsLost" || name == "WaveStatsCleared") waveScreen.StatsForCapture(false);
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

            // A NAMED MOMENT of a fight (R12): a hit, a crit, a skill, a heal or a death, frozen a few
            // frames after it plays. Has no menu item and no literal Begin call, like AudioFight, so
            // the nightly sweep (which reads those) does not run it. See PumpBattleMoment.
            if (name == "BattleMoment")
            {
                PumpBattleMoment(app, name, frame, showAt, slot, BattleWaitCeiling);
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

            // AUDIO PLAY PROOF, THE ROUND TRIP (R14-MUSIC): hub, map, one fight, hub. See
            // PumpAudioRoundTrip. Has no menu item and no literal Begin call on purpose, so the nightly
            // sweep does not run it: it is long and its point is the cue log.
            if (name == "AudioRoundTrip")
            {
                PumpAudioRoundTrip(app, name, frame, showAt, slot);
                return;
            }

            // AUDIO PLAY PROOF, THE SUMMON (R10-AUDIO): a pull played through its reveal. See
            // PumpAudioSummon. Has no menu item and no literal Begin call on purpose, so the nightly
            // sweep (which reads those) does not run it: it is long and its point is the cue log.
            if (name == "AudioSummon")
            {
                PumpAudioSummon(app, name, frame, showAt, slot);
                return;
            }

            // AUDIO PLAY PROOF (R10-AUDIO): one whole fight in the battle screen at the speed the
            // player has set, in real time and untouched, so the cue log (EMBERFALL_CUE_LOG) records
            // every sound a fight makes: hits, crits, casts, impacts, heals, shields, deaths and the
            // result. It waits for the result panel instead of skipping, which is the opposite of
            // Result below. Pick the stage like Battle does (Screen:AudioFight=<stage id>); a later
            // stage has more skills, crits, deaths and heals in it. The ceiling is generous because
            // a fight can run a minute or two at 1x. No menu item and no literal Begin call, like
            // AudioSummon, so the nightly sweep does not run it.
            if (name == "AudioFight")
            {
                if (frame <= showAt) { _audioFightStart = -1f; return; }
                BattleScreen fight = app.Router.Battle;
                // A ceiling in real seconds, not frames: the editor runs far above 60 fps, so a frame count
                // is a different number of seconds on every machine and cut a fight off at about 57 s.
                const float FightCeilingSeconds = 240f;
                if (_audioFightStart < 0f) _audioFightStart = Time.realtimeSinceStartup;
                int fightArmed = SessionState.GetInt(BattleArmKey, 0);
                if (fightArmed == 0)
                {
                    if (fight != null && fight.Finished) { SessionState.SetInt(BattleArmKey, frame); return; }
                    if (Time.realtimeSinceStartup - _audioFightStart > FightCeilingSeconds)
                    {
                        SessionState.SetInt(BattleArmKey, frame);
                        Debug.LogWarning("PlayModeScreenshot: the fight did not finish within " + FightCeilingSeconds + " s. Shooting anyway.");
                    }
                    return;
                }
                if (frame == fightArmed + BattlePaintDelay - 5) Canvas.ForceUpdateCanvases();
                if (frame == fightArmed + BattlePaintDelay) Shoot(name, frame, slot);
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

        // ------------------------------------------------------------------ the daily loop

        /// <summary>
        /// Puts the scratch profile at a frontier worth showing (the first sixteen stages, which
        /// opens the Proving) and the loop in the state the target names. The stages are cleared
        /// FIRST through GrantBattleResult and the loop's record of them is then discarded, so the
        /// board starts at zero whatever the clears counted.
        /// </summary>
        private static void SeedLoop(GameApp app, string name)
        {
            GameSession session = app.Session;
            long now = TimeUtil.NowUnix();
            List<StageDef> stages = session.Content.AllStages;
            for (int i = 0; i < stages.Count && i < 16; i++) session.GrantBattleResult(stages[i], true, now);
            session.Profile.DailyLoop = new DailyLoopState();
            session.Profile.Gold += 5000;

            switch (name)
            {
                case "OrdersHalf":
                case "HubLoopOn":
                    // One claimed, one finished and waiting, one part way, two untouched.
                    DailyLoop.Roll(session, now);
                    FinishOrder(session, 0, now, 1f, true);
                    FinishOrder(session, 1, now, 1f, false);
                    FinishOrder(session, 2, now, 0.5f, false);
                    break;
                case "OrdersClaimed":
                    // The last four days all finished, so the week's counter and the chest show.
                    for (int d = 3; d >= 0; d--)
                    {
                        long t = now - d * 86400L;
                        DailyLoop.Roll(session, t);
                        for (int i = 0; i < 5; i++) FinishOrder(session, i, t, 1f, true);
                    }
                    break;
                case "OrdersCalendar4":
                    // Three days already taken: the next claim is day 4.
                    for (int d = 3; d >= 1; d--) DailyLoop.ClaimCalendar(session, now - d * 86400L);
                    break;
            }
            app.RefreshHud();
            Debug.Log("PlayModeScreenshot: seeded the daily loop for " + name);
        }


        // THE SINKS (DESIGN-SINKS.md). Each target forces SinksSwitch ON for its run, seeds a scratch
        // profile through SinkShop.Buy and photographs a shop or the door that leads to one. Nothing
        // is written to a profile except through the rules, and nothing runs on a real identity.
        private static bool IsSinksScreen(string name)
        {
            switch (name)
            {
                case "Armory":
                case "ArmoryBought":
                case "ArmorySoldOut":
                case "ArmoryPicker":
                case "Archive":
                case "ArchiveBought":
                case "ArchiveSoldOut":
                case "ArchivePicker":
                case "ProvingSinksOn":
                case "RecitalSinksOn":
                    return true;
            }
            return false;
        }

        /// <summary>1 forces the switch ON, 2 forces it OFF (the rollback shots), 0 leaves it.</summary>
        private static int SinksForceFor(string target)
        {
            if (!target.StartsWith("Screen:")) return 0;
            string name = target.Substring(7);
            if (name == "ProvingSinksOff" || name == "RecitalSinksOff") return 2;
            return IsSinksScreen(name) ? 1 : 0;
        }

        /// <summary>Applies the run's sinks switch, the way ApplyLoopForce applies the daily loop's.</summary>
        private static void ApplySinksForce()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            int force = SessionState.GetInt(SinksForceKey, 0);
            if (force == 1) SinksSwitch.Enabled = true;
            else if (force == 2) SinksSwitch.Enabled = false;
        }

        private static void CheckSinksAreOff(GameApp app)
        {
            bool off = !SinksSwitch.Enabled && app.Router.Armory == null && app.Router.Archive == null && app.Session.Profile.Sinks.IsEmpty;
            Debug.Log(off
                ? "PlayModeScreenshot: [ok] the sinks are off: no Armory, no Archive, no sinks state, so the page below is the page without them."
                : "PlayModeScreenshot: [FAIL] the sinks are NOT off (switch " + SinksSwitch.Enabled + ", Armory " + (app.Router.Armory != null)
                  + "). This capture is not the page without the sinks.");
        }

        /// <summary>
        /// Puts the scratch profile where the shops have something to show (the Proving and the
        /// Recital open, standing 9, floor 12, two full purses, both banks empty) and then makes the
        /// purchases the target names, all through SinkShop.Buy. Scratch identity only: on anything
        /// else it refuses and says so.
        /// </summary>
        private static void SeedSinks(GameApp app, string name)
        {
            if (!EditorScratchIdentity.IsScratch)
            {
                Debug.LogError("PlayModeScreenshot: refusing to seed the sinks on a real identity.");
                return;
            }
            GameSession session = app.Session;
            PlayerProfile profile = session.Profile;
            long now = TimeUtil.NowUnix();
            List<StageDef> stages = session.Content.AllStages;
            for (int i = 0; i < stages.Count; i++)
                if (!profile.ClearedStageIds.Contains(stages[i].Id)) profile.ClearedStageIds.Add(stages[i].Id);
            profile.StagesCleared = CampaignProgress.CountCleared(profile, session.Content);
            profile.ProvingStanding = 9;
            profile.ProvingHighestStanding = 9;
            profile.RecitalHighestFloor = 12;
            profile.RecitalCurrentFloor = 13;
            profile.Valor = 20000;
            profile.Coda = 20000;
            profile.ProvingBrandsBanked = 0;
            profile.ProvingBrandsClockUnix = now;
            profile.RecitalEchoesBanked = 0;
            profile.RecitalEchoesClockUnix = now;
            profile.Sinks = new SinksState();

            string archiveHero = session.Content.Sinks.Item("archive_hero") != null
                ? session.Content.Sinks.Item("archive_hero").Heroes[0] : null;
            string armoryHero = session.Content.Sinks.Item("armory_hero") != null
                ? session.Content.Sinks.Item("armory_hero").Heroes[0] : null;

            switch (name)
            {
                case "ArmoryBought":
                    SinkShop.Buy(session, "armory_weapon", null, now);
                    SinkShop.Buy(session, "armory_brand", null, now);
                    SinkShop.Buy(session, "armory_brand", null, now);
                    break;
                case "ArmorySoldOut":
                    SinkShop.Buy(session, "armory_weapon", null, now);
                    SinkShop.Buy(session, "armory_helm", null, now);
                    SinkShop.Buy(session, "armory_plate", null, now);
                    for (int i = 0; i < 3; i++) SinkShop.Buy(session, "armory_brand", null, now);
                    SinkShop.Buy(session, "armory_echo", null, now);
                    SinkShop.Buy(session, "armory_hero", armoryHero, now);
                    break;
                case "ArchiveBought":
                    SinkShop.Buy(session, "archive_hero", archiveHero, now);
                    break;
                case "ArchiveSoldOut":
                    SinkShop.Buy(session, "archive_hero", archiveHero, now);
                    profile.Coda = 0;
                    break;
            }
            app.RefreshHud();
            Debug.Log("PlayModeScreenshot: seeded the sinks for " + name);
        }

        /// <summary>The picker targets open the hero picker with a hero chosen, after the Show.</summary>
        private static void OpenSinksPicker(GameApp app, string name)
        {
            if (name == "ArmoryPicker" && app.Router.Armory != null)
            {
                SinkItemDef item = app.Session.Content.Sinks.Item("armory_hero");
                if (item != null) app.Router.Armory.PickForCapture(item.Id, item.Heroes[Mathf.Min(2, item.Heroes.Count - 1)]);
            }
            if (name == "ArchivePicker" && app.Router.Archive != null)
            {
                SinkItemDef item = app.Session.Content.Sinks.Item("archive_hero");
                if (item != null) app.Router.Archive.PickForCapture(item.Id, item.Heroes[Mathf.Min(2, item.Heroes.Count - 1)]);
            }
        }

        /// <summary>Counts a fraction of an order's target through the rules (the kind's own counter,
        /// as a played wave or summon would) and optionally claims it.</summary>
        private static void FinishOrder(GameSession session, int index, long now, float fraction, bool claim)
        {
            DailyLoopState st = session.Profile.DailyLoop;
            if (index < 0 || index >= st.Orders.Count) return;
            OrderDef def = session.Content.Orders.Order(st.Orders[index].Id);
            if (def == null) return;
            int amount = Mathf.RoundToInt(def.Target * fraction);
            if (amount > 0) DailyLoop.Record(session, def.Kind, amount, now);
            if (claim) DailyLoop.ClaimOrder(session, def.Id, now);
        }

        private static void CheckLoopIsOff(GameApp app)
        {
            bool off = !DailyLoopSwitch.Enabled && app.Router.Orders == null && app.Session.Profile.DailyLoop.IsEmpty;
            Debug.Log(off
                ? "PlayModeScreenshot: [ok] the daily loop is off: no Orders page, no loop state, so the Hub below is the Hub without it."
                : "PlayModeScreenshot: [FAIL] the daily loop is NOT off (switch " + DailyLoopSwitch.Enabled + ", Orders page "
                  + (app.Router.Orders != null) + "). This capture is not the Hub without the loop.");
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
            // Kael first when the profile has him: he is a starter, and the hero the trial's
            // confidence batches were diagnosed on, so his page is the one that shows a reading
            // taking more than forty fights to settle. Any other talent hero is the fallback.
            for (int i = 0; i < owned.Count && subject == null; i++)
                if (owned[i].DefId == "kael_the_unyielding" && owned[i].Def != null
                    && owned[i].Def.Talents.Count >= HeroDef.MaxTalents) subject = owned[i];
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

        // ------------------------------------------------------------------ audio play proof

        /// <summary>The tiers the audio summon plays: a 1-star (the flat reveal), a 3-star and a
        /// 5-star (the biggest reveal and its five star pings), so every summon.reveal.N a batch can
        /// hit that matters fires once.</summary>
        private static readonly int[] AudioSummonTiers = { 1, 3, 5 };

        private static int _roundTripPhase;
        private static float _roundTripAt;          // Time.realtimeSinceStartup when the phase began
        private static int _roundTripFrame;         // the frame the closing shot is counted from
        private static float _audioFightStart = -1f;

        /// <summary>
        /// AudioRoundTrip (R14-MUSIC), started as Screen:AudioRoundTrip=&lt;stage id&gt; (the next
        /// stage when none is named): the hub, the map, one whole fight, and the hub again, each calm
        /// screen held long enough to hear it (4 s, 3 s, and 4 s on the way back, in real seconds). The cue log (EMBERFALL_CUE_LOG) must show music.hub, then
        /// music.map as a same-track carry, then the fight's battle track (a crossfade), the victory
        /// duck and stinger, and the hub again after the fight. The fight is played in real time like
        /// AudioFight, so a skill moment in it writes its DUCK line too. Nothing here decides anything:
        /// it presses the same Router.Show a tap does.
        /// </summary>
        private static void PumpAudioRoundTrip(GameApp app, string name, int frame, int showAt, int slot)
        {
            // Every hold and the fight ceiling are real seconds: the editor's frame rate is not 60.
            const float HubHold = 4f;
            const float MapHold = 3f;     // long enough to see the carry and no restart
            const float Hold = 1f;        // a beat on the result before leaving it
            const float FightCeiling = 240f;
            const int PaintDelay = 20;
            float clock = Time.realtimeSinceStartup;

            if (frame == showAt)
            {
                _roundTripPhase = 0;
                _roundTripAt = clock;
                return;
            }
            if (frame < showAt) return;

            switch (_roundTripPhase)
            {
                case 0:
                    if (clock - _roundTripAt < HubHold) return;
                    app.Router.Show(ScreenId.Campaign);
                    Debug.Log("PlayModeScreenshot: AudioRoundTrip is on the map at frame " + frame + ".");
                    _roundTripPhase = 1;
                    _roundTripAt = clock;
                    return;
                case 1:
                    if (clock - _roundTripAt < MapHold) return;
                    string asked = SessionState.GetString(BattleStageKey, "");
                    StageDef next = asked.Length > 0 ? app.Session.Content.GetStage(asked) : app.Session.NextStage;
                    if (next == null)
                    {
                        Debug.LogWarning("PlayModeScreenshot: no campaign stage to fight, skipping AudioRoundTrip.");
                        SessionState.SetInt(StageKey, slot + 1);
                        return;
                    }
                    app.Router.Battle.SetStage(next);
                    app.Router.Show(ScreenId.Battle);
                    Debug.Log("PlayModeScreenshot: AudioRoundTrip is fighting " + next.Id + " at frame " + frame + ".");
                    _roundTripPhase = 2;
                    _roundTripAt = clock;
                    return;
                case 2:
                    BattleScreen fight = app.Router.Battle;
                    bool timedOut = clock - _roundTripAt > FightCeiling;
                    if ((fight != null && fight.Finished) || timedOut)
                    {
                        if (timedOut) Debug.LogWarning("PlayModeScreenshot: the fight did not finish within " + FightCeiling + " s. Leaving it.");
                        _roundTripPhase = 3;
                        _roundTripAt = clock;
                    }
                    return;
                case 3:
                    if (clock - _roundTripAt < Hold) return;
                    app.Router.Show(ScreenId.MainMenu);
                    Debug.Log("PlayModeScreenshot: AudioRoundTrip is back on the hub at frame " + frame + ".");
                    _roundTripPhase = 4;
                    _roundTripAt = clock;
                    return;
                case 4:
                    if (clock - _roundTripAt < HubHold) return;
                    _roundTripPhase = 5;
                    _roundTripFrame = frame;
                    return;
                default:
                    if (frame == _roundTripFrame + PaintDelay - 5) Canvas.ForceUpdateCanvases();
                    if (frame == _roundTripFrame + PaintDelay) Shoot(name, frame, slot);
                    return;
            }
        }

        private static int _audioSummonStart;
        private static int _audioSummonSettledAt;
        private static int _audioSummonSettledIndex;
        private static int _audioSummonSummaryAt;
        private static int _audioSummonDoneAt;

        /// <summary>
        /// AudioSummon: plays a summon through its press, buildup, tell, flash, reveals, star pings
        /// and summary, in real time, so the cue log (EMBERFALL_CUE_LOG) records summon.start,
        /// summon.charge (LOOP+ and LOOP-), summon.reveal.1, .3 and .5, and summon.star.
        ///
        /// It uses SummonScreen.DebugPreview, so the tiers are SEEDED: nothing is rolled, no shard
        /// is spent and no hero is granted (a real pull cannot be made to land a 5-star). The sound
        /// path is the real one: the sequence fires the same SummonBeat calls either way. Each
        /// settled hero is tapped after a short wait, the way a player taps TAP TO CONTINUE; the
        /// summary is held for a moment and then the sequence is aborted, which also exercises the
        /// abort path that used to leave the charge loop running (the log must show LOOP- before it).
        /// </summary>
        private static void PumpAudioSummon(GameApp app, string name, int frame, int showAt, int slot)
        {
            const int Ceiling = 9000;
            const int SettleHold = 90;
            const int SummaryHold = 150;
            const int PaintDelay = 20;

            if (frame == showAt)
            {
                _audioSummonStart = 0;
                _audioSummonSettledAt = 0;
                _audioSummonSettledIndex = -1;
                _audioSummonSummaryAt = 0;
                _audioSummonDoneAt = 0;
                return;
            }
            if (frame < showAt) return;

            SummonScreen summon = app.Router.Summon;
            SummonSequence seq = summon != null ? summon.DebugSequence : null;
            if (seq == null)
            {
                Debug.LogWarning("PlayModeScreenshot: no summon screen or sequence, skipping AudioSummon.");
                SessionState.SetInt(StageKey, slot + 1);
                return;
            }

            if (_audioSummonStart == 0)
            {
                if (frame < showAt + 30) return;
                summon.DebugPreview(AudioSummonTiers);
                _audioSummonStart = frame;
                Debug.Log("PlayModeScreenshot: AudioSummon started a seeded preview of tiers 1, 3 and 5 at frame " + frame + ".");
                return;
            }

            if (_audioSummonDoneAt == 0)
            {
                bool timedOut = frame > _audioSummonStart + Ceiling;
                if (!seq.Running || timedOut)
                {
                    if (timedOut)
                    {
                        Debug.LogWarning("PlayModeScreenshot: the summon did not finish within " + Ceiling + " frames. Aborting it.");
                        seq.Abort();
                    }
                    _audioSummonDoneAt = frame;
                    return;
                }

                if (seq.Current == SummonSequence.Phase.Summary)
                {
                    if (_audioSummonSummaryAt == 0) _audioSummonSummaryAt = frame;
                    else if (frame - _audioSummonSummaryAt >= SummaryHold)
                    {
                        seq.Abort();
                        _audioSummonDoneAt = frame;
                    }
                    return;
                }

                if (seq.IsSettled)
                {
                    if (_audioSummonSettledIndex != seq.Index) { _audioSummonSettledIndex = seq.Index; _audioSummonSettledAt = frame; }
                    else if (frame - _audioSummonSettledAt >= SettleHold)
                    {
                        _audioSummonSettledAt = frame;
                        seq.Tap();
                    }
                }
                return;
            }

            if (frame == _audioSummonDoneAt + PaintDelay - 5) Canvas.ForceUpdateCanvases();
            if (frame == _audioSummonDoneAt + PaintDelay) Shoot(name, frame, slot);
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
            // The talent page measures on its own frames, and a capture that fired mid-trial
            // would photograph "Measuring..." or a reading still on its first batch.
            if (name == "Talents" && GameApp.Instance != null) GameApp.Instance.Router.Talents.FinishTrialForCapture();
            if (name == "SettingsCredits" && GameApp.Instance != null) GameApp.Instance.Router.Settings.ScrollToEndForCapture();
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
                   || name == "WaveReplayHeal" || name == "WaveStatsLost" || name == "WaveStatsCleared";
        }

        /// <summary>The wave target whose seeded state a stats target photographs: the panel is
        /// opened over the lost wave or over the stage clear those targets already build.</summary>
        private static string WaveBase(string name)
        {
            if (name == "WaveStatsLost") return "WaveResultLost";
            if (name == "WaveStatsCleared") return "WaveResultCleared";
            return name;
        }

        private const string ReplayStageKey = "PlayModeScreenshot.ReplayStage";

        /// <summary>"kind[.aspect][.off]" to its parts. False for anything else.</summary>
        private static bool ParseMoment(string spec, out MomentKind kind, out Aspect? aspect, out bool off)
        {
            kind = MomentKind.None;
            aspect = null;
            off = false;
            string[] parts = spec.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].ToLowerInvariant();
                if (i == 0)
                {
                    if (p == "hit") kind = MomentKind.Hit;
                    else if (p == "crit") kind = MomentKind.Crit;
                    else if (p == "skill") kind = MomentKind.Skill;
                    else if (p == "heal") kind = MomentKind.Heal;
                    else if (p == "death") kind = MomentKind.Death;
                    else return false;
                }
                else if (p == "off") off = true;
                else
                {
                    Aspect a;
                    if (!Enum.TryParse(p, true, out a)) return false;
                    aspect = a;
                }
            }
            return kind != MomentKind.None;
        }

        /// <summary>Frames to let a moment play on after it arrives, so the shot lands on its look:
        /// the flash is at its peak and the number is still popping for a hit or a crit, the dim and
        /// the ring are at their strongest for a skill, the heal ring is out, a death is mid-sink.</summary>
        private static int MomentSettleFrames(MomentKind kind)
        {
            switch (kind)
            {
                case MomentKind.Skill: return 4;
                case MomentKind.Heal: return 6;
                case MomentKind.Death: return 14;
                default: return 3;
            }
        }

        /// <summary>
        /// Screen:BattleMoment. Stage 0 waits for the first round, checks the record holds the
        /// moment (MOMENT-ABSENT and a skipped slot if not, so the batch reports the target FAILED
        /// within seconds and the caller picks another stage), and arms the screen. Stage 1 waits
        /// for the screen to play it. Stage 2 lets a few frames of game time pass (the clock is held
        /// at 60 frames a second for the shot, so frames mean game time), freezes, and shoots.
        /// </summary>
        private static void PumpBattleMoment(GameApp app, string name, int frame, int showAt, int slot, int waitCeiling)
        {
            if (frame <= showAt) return;
            string requested = SessionState.GetString(RequestedKey, SessionState.GetString(TargetKey, name));
            string spec = SessionState.GetString(MomentKey, "");
            MomentKind kind;
            Aspect? aspect;
            bool off;
            if (!ParseMoment(spec, out kind, out aspect, out off))
            {
                Debug.LogError("PlayModeScreenshot: [FAIL] " + requested + ": '" + spec + "' is not kind[.aspect][.off] with kind hit, crit, skill, heal or death. Not shooting.");
                SkipMoment(slot);
                return;
            }

            BattleScreen battle = app.Router.Battle;
            int stage = SessionState.GetInt(MomentStageKey, 0);
            int armed = SessionState.GetInt(BattleArmKey, 0);

            if (stage == 0)
            {
                if (battle != null && battle.RoundUnderway)
                {
                    if (!battle.MomentPresent(kind, aspect))
                    {
                        Debug.LogWarning("PlayModeScreenshot: MOMENT-ABSENT " + requested + ": no " + kind
                                         + (aspect != null ? " by " + aspect.Value : "") + " in this fight, pick another stage");
                        SkipMoment(slot);
                        return;
                    }
                    battle.ArmMomentCapture(kind, aspect);
                    Debug.Log("BattleMoment: line aspects " + battle.DescribeLineForCapture() + "; waiting for " + kind
                              + (aspect != null ? " by " + aspect.Value : "") + (off ? " with the pre-R12 profile" : "") + ".");
                    // Frames mean game time from here to the shot, whatever the editor's speed.
                    Time.captureFramerate = 60;
                    SessionState.SetInt(MomentFrameKey, Time.frameCount);
                    SessionState.SetInt(MomentStageKey, 1);
                    SessionState.SetInt(BattleArmKey, frame);
                }
                else if (frame > showAt + waitCeiling)
                {
                    Debug.LogWarning("PlayModeScreenshot: no round started within " + waitCeiling + " frames. Skipping " + requested + ".");
                    SkipMoment(slot);
                }
                return;
            }

            if (stage == 1)
            {
                // A fight at 1x takes a minute to reach round four, so it runs at 4x until a few events
                // before the moment and then drops to 1x, so the moment itself plays at the player's
                // speed. The progress line says where the fight is, so a fight that stops moving is
                // told apart from one that is only far from its moment.
                if (battle != null && battle.MomentEventIndex >= 0)
                    battle.SpeedForCapture = battle.PlayedEventIndex < battle.MomentEventIndex - 6 ? 4f : 1f;
                if (battle != null && (frame - armed) % 600 == 0)
                    Debug.Log("BattleMoment: progress event " + battle.PlayedEventIndex + " of " + battle.EventCount + ", the moment is event "
                              + battle.MomentEventIndex + ", " + battle.RoundLabelForCapture + ", speed " + battle.SpeedForCapture
                              + ", timeScale " + Time.timeScale + ", deltaTime " + Time.deltaTime.ToString("0.0000")
                              + ", captureFramerate " + Time.captureFramerate + ", game frame " + (Time.frameCount - SessionState.GetInt(MomentFrameKey, 0))
                              + " after arming, game time " + Time.time.ToString("0.0") + " s, editor update " + (frame - armed) + ".");
                if (battle != null && battle.MomentArrived)
                {
                    battle.SpeedForCapture = 1f;
                    int settleFrames = battle.SettleFramesForCapture(MomentSettleFrames(kind));
                    SessionState.SetInt(MomentSettleKey, settleFrames);
                    Debug.Log("PlayModeScreenshot: " + requested + " arrived at game frame " + (Time.frameCount - SessionState.GetInt(MomentFrameKey, 0))
                              + " after arming, shooting in " + settleFrames + " game frames.");
                    SessionState.SetInt(MomentFrameKey, Time.frameCount);
                    SessionState.SetInt(MomentStageKey, 2);
                }
                else if (frame > armed + waitCeiling * 10)
                {
                    Debug.LogWarning("PlayModeScreenshot: " + requested + " never played within " + waitCeiling * 10 + " editor updates (stopped at event "
                                     + (battle != null ? battle.PlayedEventIndex + " of " + battle.EventCount : "?") + "). Skipping it.");
                    SkipMoment(slot);
                }
                return;
            }

            // Stages 2 and 3 count GAME frames: settle, freeze the clock, let the canvas lay out, shoot.
            int gameFrame = Time.frameCount;
            int settleFrom = SessionState.GetInt(MomentFrameKey, gameFrame);
            if (stage == 2)
            {
                if (gameFrame - settleFrom >= SessionState.GetInt(MomentSettleKey, MomentSettleFrames(kind)))
                {
                    Time.timeScale = 0f;
                    SessionState.SetInt(MomentFrameKey, gameFrame);
                    SessionState.SetInt(MomentStageKey, 3);
                }
                return;
            }
            Canvas.ForceUpdateCanvases();
            if (gameFrame - settleFrom >= 3)
            {
                Color backdrop = app.Router.Battle != null ? app.Router.Battle.BackdropColorForCapture : Color.white;
                Debug.Log("BattleMoment: shot taken, backdrop _Color (" + backdrop.r.ToString("0.00") + ", " + backdrop.g.ToString("0.00") + ", " + backdrop.b.ToString("0.00")
                          + ") (1.00 is undimmed, 0.70 is the full skill dim), Juice.On " + Juice.On + ".");
                Shoot(name, frame, slot);
                SessionState.SetInt(MomentStageKey, 0);
                Juice.ForceForCapture(null);
                Time.captureFramerate = 0;
                Time.timeScale = 1f;
            }
        }

        /// <summary>Gives the slot up and puts back everything a moment shot changes.</summary>
        private static void SkipMoment(int slot)
        {
            SessionState.SetInt(MomentStageKey, 0);
            SessionState.SetInt(BattleArmKey, 0);
            Juice.ForceForCapture(null);
            Time.captureFramerate = 0;
            Time.timeScale = 1f;
            SessionState.SetInt(StageKey, slot + 1);
        }

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
