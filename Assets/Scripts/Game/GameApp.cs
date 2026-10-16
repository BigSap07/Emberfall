using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;
using Emberfall.Game.Cloud;
using Emberfall.UI;

namespace Emberfall.Game
{
    /// <summary>
    /// Composition root. Owns content, the player's session, the canvas and the router,
    /// and is the one object every screen talks to.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameApp : MonoBehaviour
    {
        public static GameApp Instance { get; private set; }

        public ContentDatabase Content { get; private set; }
        public GameSession Session { get; private set; }
        public ScreenRouter Router { get; private set; }
        public Canvas RootCanvas { get; private set; }
        public RectTransform ScreenLayer { get; private set; }

        /// <summary>The shared top bar's home: full bleed, inset to Screen.safeArea.</summary>
        public RectTransform HudFrame { get; private set; }

        /// <summary>
        /// Where a panel is drawn: above every screen and above the top bar, below the popups.
        ///
        /// Above the top bar deliberately. A panel dims what it covers, and a HUD frame left
        /// bright over a dimmed map would read as chrome floating on top of the sheet rather than
        /// as the Hub waiting underneath it.
        /// </summary>
        public RectTransform PanelLayer { get; private set; }

        public RectTransform OverlayLayer { get; private set; }
        public Sfx Audio { get; private set; }

        /// <summary>
        /// Everything currently open over the current screen, newest last. Escape and the Android
        /// back key close the topmost entry; see UiLayerStack for the rule in full.
        /// </summary>
        public UiLayerStack Layers { get; private set; }

        /// <summary>The panel layer's host: the player panel and the guild panel are both this.</summary>
        public PanelHost Panels { get; private set; }

        /// <summary>Every currency the account holds, in one list. Opened from CurrencyStrip -
        /// see BagPanel for why it has to exist at all.</summary>
        public BagPanel Bag { get; private set; }

        /// <summary>
        /// Accounts, cloud save, friends and guilds - or null, on a build with no Supabase config.
        ///
        /// NULL IS A SUPPORTED STATE AND EVERY CALLER CHECKS FOR IT. A fresh clone has no
        /// Assets/Resources/Config/supabase.json (it is gitignored, and must be), so the ordinary
        /// state of this field on a machine nobody has pointed at a project is null - and the game
        /// has to boot and play exactly as it did before any of this existed. Constructing the
        /// coordinator anyway and letting it fail on every request would work too, but it would
        /// mean the offline case was a path through the online code rather than the absence of it,
        /// and the absence is what the offline promise actually is.
        /// </summary>
        public CloudCoordinator Cloud { get; private set; }

        /// <summary>The two-save chooser. Built whether or not there is a cloud, because it costs
        /// one hidden object and the alternative is a null check at the one moment in this system
        /// where a missing screen would silently overwrite somebody's progress.</summary>
        public ConflictChooser Conflict { get; private set; }

        private HudTopBar _topBar;
        private RectTransform _background;
        private Camera _idleCamera;
        private Toast _toast;
        private ModalPopup _modal;
        private Coroutine _toastRoutine;
        private float _saveTimer = -1f;

        /// <summary>
        /// How far Awake got, and whether it finished. Static so a guard elsewhere can read it
        /// even when Instance is missing or half-built.
        ///
        /// This exists because "Session is null" was reported twice in this project with no
        /// accompanying exception, and the second report reasoned - correctly, from what was
        /// visible - that Awake must be dying somewhere in the content or save load. There was no
        /// way to tell from outside whether Awake had run at all, died partway, or completed. Now
        /// there is: any guard that trips can say which stage the boot reached, and BootFailed
        /// distinguishes "never started" from "threw" from "finished fine".
        /// </summary>
        /// <summary>
        /// EditorPrefs key for the opt-in Kael grant in Awake. Public so the Debug menu item that
        /// toggles it cannot drift from the key the check reads - the two used to be one literal in
        /// two files in this project's earlier editor tooling, and they drifted.
        /// </summary>
        public const string GrantKaelOnBootKey = "Emberfall.GrantKaelOnBoot";

        /// <summary>EditorPrefs key that suppresses the first-run sequence for a capture run. Set
        /// and cleared by the capture tool around the run, never by anything that ships.</summary>
        public const string SuppressFirstRunKey = "Emberfall.SuppressFirstRun";

        public static string BootStage = "not started";
        public static bool BootCompleted;
        public static bool BootFailed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Every step of the boot is named as it is entered, and the whole run is wrapped, so a
            // failure reports WHERE rather than leaving the next reader to infer it from which
            // fields ended up null. The catch rethrows: Unity's own handler should still record
            // the stack, and swallowing it here would turn a loud failure into exactly the silent
            // one this instrumentation exists to make impossible.
            BootStage = "start";
            BootCompleted = false;
            BootFailed = false;
            try
            {
            BootStage = "ConfigureRuntime";
            ConfigureRuntime();
            WarnIfWrongRenderPipeline();

            BootStage = "ContentService.Load";
            Content = ContentService.Load();

#if UNITY_EDITOR
            CombatSimulation.OnBattleComplete += CombatLogger.Write;
#endif

            BootStage = "SaveService.Load";
            PlayerProfile profile = SaveService.Load(Content);
            bool freshStart = profile == null;
            if (freshStart)
            {
                // A new player is a Keeper: the ordinary new profile with Kael held back for the
                // Cinder's recall, at the first step of the onboarding flow (DESIGN-ONBOARDING.md).
                BootStage = "Onboarding.CreateKeeperProfile";
                profile = Onboarding.CreateKeeperProfile(Content, TimeUtil.NowUnix());
#if UNITY_EDITOR
                // The capture tools run on a deliberately fresh scratch profile and photograph the
                // game, not the first session, unless a target asks for the first session. The
                // ordinary new profile, complete, is every build before onboarding existed.
                if (UnityEditor.EditorPrefs.GetBool(SuppressFirstRunKey, false))
                {
                    profile = GameSession.CreateNewProfile(Content, TimeUtil.NowUnix());
                    Onboarding.MarkComplete(profile);
                }
#endif
            }

            BootStage = "new GameSession";
            Session = new GameSession(Content, profile);

#if UNITY_EDITOR
            // OFF BY DEFAULT, and that is the fix. This grant existed for the animation pipeline
            // task, on the reasoning that "Kael has no summon banner wiring yet, so this is the
            // smallest path onto a team for a real test battle". That reasoning is now stale on
            // both halves.
            //
            // Kael IS summonable: RollHero draws from Content.HeroesOfStars weighted by star tier,
            // he is five stars, and summonWeight5 is a non-zero weight in balance.json (it was the
            // only one when this was written, and a live ten-pull returned him as a duplicate). And the cost of it running unconditionally
            // was worse than the convenience: it fired on EVERY editor launch, so a fresh save in
            // the editor showed a four-hero roster where a real new player gets three. Every
            // first-run check any of us has ever done in the editor was against a line-up no
            // player will ever see - which now matters more, because FirstRun's opening card tells
            // that player about "your starting allies".
            //
            // Kept rather than deleted, behind a flag a human has to turn on
            // (Emberfall/Debug/Grant Kael On Editor Boot), because the affordance is still useful
            // and an opt-in switch says so out loud where a silently-removed one says nothing.
            if (UnityEditor.EditorPrefs.GetBool(GrantKaelOnBootKey, false))
            {
                HeroDef kael = Content.GetHero("kael_the_unyielding");
                if (kael != null && !Session.Profile.Owns(kael.Id))
                    Session.GrantHeroDirect(kael, TimeUtil.NowUnix());
            }
#endif

            BootStage = "BuildCanvas";
            Layers = new UiLayerStack();
            BuildCanvas();
            EnsureEventSystem();

            BootStage = "Sfx";
            Audio = gameObject.AddComponent<Sfx>();

            BootStage = "HudTopBar.Build";
            _topBar = new HudTopBar();
            // Inside the safe frame, not on the raw canvas. The bar is the top half of the HUD
            // frame on every screen in the game, and a plate anchored to the canvas corner puts
            // the player's name under a notch on exactly the phones this project targets.
            _topBar.Build(HudFrame);

            BootStage = "ScreenRouter.Build";
            GameObject routerHost = new GameObject("ScreenRouter");
            routerHost.transform.SetParent(transform, false);
            Router = routerHost.AddComponent<ScreenRouter>();
            Router.Build(this, ScreenLayer, OverlayLayer);

            BootStage = "panels";
            Panels = new PanelHost(this, PanelLayer);

            BootStage = "overlays";
            _toast = new Toast(OverlayLayer);
            _modal = new ModalPopup(OverlayLayer);
            NamePrompt = new NamePrompt(OverlayLayer);
            Conflict = new ConflictChooser(OverlayLayer);
            // One bag for the whole app, not one per screen. Four screens open it and they open
            // the same object, which is why it is built here beside the other overlays rather
            // than by whichever page happens to be showing.
            Bag = new BagPanel(OverlayLayer);

            _topBar.Root.SetAsLastSibling();
            PanelLayer.SetAsLastSibling();
            OverlayLayer.SetAsLastSibling();

            BootStage = "first screen";
            RefreshHud();
            Router.Show(ScreenId.MainMenu, false);

            if (freshStart) SaveNow();

            // AFTER the first screen is up, and deliberately last. Nothing about an account is on
            // the path to a playable game: this starts a coroutine and returns, the menu is
            // already drawn, and a device with no network never notices that it did not finish.
            BootStage = "cloud";
            StartCloud(freshStart);

            if (OnboardingSwitch.Enabled)
            {
                // The first session and the free-play tips (DESIGN-ONBOARDING.md). Installed for
                // every player: the coach reads PlayerProfile.TutorialStep each frame and shows
                // nothing once it is complete except a tip the first time each screen opens. A
                // player who quit mid-flow resumes at the step after the last one they completed,
                // because that step is all the coach reads; a progressed save from before version
                // 21 loads complete.
                BootStage = "onboarding";
                OnboardingCoach.Install(this);
            }
            else
            {
                // The OnboardingSwitch is off: the only onboarding this game had before the
                // Keeper's first session, and only ever for a genuinely new install. Keyed to
                // freshStart rather than a saved flag so the save shape is untouched and an
                // existing player cannot be shown it - see FirstRun for that trade in full.
                bool showFirstRun = freshStart;
#if UNITY_EDITOR
                // The capture tools run on a deliberately fresh scratch profile, which is exactly
                // the account FirstRun exists for - so every automated screenshot of the Hub was a
                // screenshot of a welcome card with the Hub behind it. The opt-out is editor-only
                // and off unless a tool sets it, so a real first run is untouched.
                if (UnityEditor.EditorPrefs.GetBool(SuppressFirstRunKey, false)) showFirstRun = false;
#endif
                if (showFirstRun) FirstRun.Begin(this);
            }

            BootStage = "complete";
            BootCompleted = true;
            }
            catch (System.Exception ex)
            {
                BootFailed = true;
                Debug.LogError("GameApp.Awake FAILED at stage '" + BootStage + "'. The app is now "
                    + "half-built: Session" + (Session == null ? " IS NULL" : " exists")
                    + ", Router" + (Router == null ? " IS NULL" : " exists")
                    + ". Everything downstream of this stage did not run. " + ex);
                throw;
            }
        }

        // ------------------------------------------------------------------ the cloud

        /// <summary>
        /// Builds the account stack and starts the sign-in, or does nothing at all.
        ///
        /// WRAPPED, AND THE WRAP IS THE POINT. This runs inside Awake's own try, so an exception
        /// here would be reported as a boot failure and rethrown - and a game that refused to
        /// start because a JSON config file was malformed would be exactly the failure this whole
        /// feature promises cannot happen. Caught here instead: the cloud is left null, one
        /// warning says why, and the menu behind this line is already drawn and already playable.
        /// </summary>
        private void StartCloud(bool bootWasFreshStart)
        {
            // Editor automation is held off inside SupabaseConfig, not here: while
            // SupabaseConfig.ForcedOffline is set, Current is an unconfigured config, so the
            // Configured check below returns before any SupabaseClient is built. A scratch
            // identity has no stored session, so a configured build would otherwise sign a
            // brand-new anonymous account up on the LIVE project for a photograph.
            try
            {
                SupabaseConfig config = SupabaseConfig.Current;
                if (!config.Configured)
                {
                    // Not a warning. A build with no config is the normal state of a fresh clone
                    // and of every machine that has not been pointed at a project, and logging it
                    // as a problem would train everyone to ignore this line.
                    Debug.Log("Emberfall: no cloud configured - " + config.Problem
                              + ". The game runs entirely locally.");
                    return;
                }

                CloudLocalState state = CloudLocalState.Load();

                // A NEW GAME DOES NOT DESCEND FROM ANY CLOUD SAVE. The base revision says "local
                // was built on cloud revision N", which stops being true the moment boot replaces
                // an unusable save with a starter profile - and with it left in place, the first
                // sync saw cloud == base and uploaded the starter profile over the one copy that
                // could have restored the player. Forgetting it makes that sync ask instead, with
                // both saves on the card. See Docs/CLOUD-GAPS.md.
                if (bootWasFreshStart && state.BaseRevision != 0)
                {
                    Debug.LogWarning("Emberfall: this launch started a new game on a device that had synced "
                                     + "revision " + state.BaseRevision + ", so the next sync will ask which save to keep.");
                    state.BaseRevision = 0;
                    state.Save();
                }
                SupabaseClient client = new SupabaseClient(config, this);
                AccountService account = new AccountService(client, state);
                CloudSaveService saves = new CloudSaveService(client, account, state);
                SocialService social = new SocialService(client, account, state);
                GuildService guild = new GuildService(client, account, state);
                Cloud = new CloudCoordinator(this, client, account, saves, social, guild, state);
                StartCoroutine(Cloud.Begin());
            }
            catch (System.Exception ex)
            {
                Cloud = null;
                Debug.LogWarning("Emberfall: the cloud stack could not be built, so this session is "
                                 + "local only. The game is unaffected. " + ex.Message);
            }
        }

        /// <summary>
        /// Rebuilds the running game from whatever is now on disk - the one thing the cloud
        /// download path needs from here, and the only caller.
        ///
        /// Goes through SaveService.Load rather than deserialising directly, so the session a
        /// download produces is bit-for-bit the session the NEXT LAUNCH would produce from the
        /// same document. Anything else would be a state the player could not get back to.
        ///
        /// Returns false without touching anything if the document will not load, which leaves
        /// the player on the save they already had - the correct outcome, because the alternative
        /// is a half-swapped session holding heroes from one profile and progress from another.
        /// </summary>
        public bool AdoptSaveFromDisk()
        {
            PlayerProfile adopted = SaveService.Load(Content);
            if (adopted == null) return false;

            Session = new GameSession(Content, adopted);
            RefreshHud();

            // Home, unconditionally. Every screen that is currently built holds references INTO
            // the profile that was just replaced - a hero list, a selected armor piece, a pending
            // opponent - and OnShow is the only thing that re-reads them. Sending the router home
            // runs it, and clearing the history stops Back walking into a screen that never did.
            if (Router != null)
            {
                Router.ClearHistory();
                Router.Show(ScreenId.MainMenu, false);
            }
            return true;
        }

        private static void ConfigureRuntime()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            QualitySettings.vSyncCount = 0;
            Time.timeScale = 1f;
        }

        private static void WarnIfWrongRenderPipeline()
        {
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
            {
                Debug.LogWarning("Emberfall: a Scriptable Render Pipeline is active. The prototype's " +
                                 "shaders include a URP sub-shader, but the project was designed for the " +
                                 "Built-In Render Pipeline. If units render magenta, switch back to Built-In.");
            }
        }

        // ------------------------------------------------------------------ canvas

        private void BuildCanvas()
        {
            GameObject canvasObject = new GameObject("UICanvas");
            canvasObject.transform.SetParent(transform, false);

            RootCanvas = canvasObject.AddComponent<Canvas>();
            RootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RootCanvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UITheme.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();

            _background = UIBuilder.Node("Background", canvasRect);
            UIBuilder.FillParent(_background, 0f);
            Image backgroundImage = _background.gameObject.AddComponent<Image>();
            backgroundImage.sprite = TextureLab.White;
            backgroundImage.color = UITheme.Backdrop;
            backgroundImage.raycastTarget = false;

            Image vignette = UIBuilder.Img("Vignette", _background, TextureLab.Glow(256, 1.4f), new Color(0.35f, 0.18f, 0.42f, 0.30f));
            UIBuilder.FillParent(vignette.rectTransform, -260f);

            // A camera always has to exist or Unity warns every frame; this one only clears.
            // The battle stage adds its own camera at a higher depth while a fight is running.
            GameObject idleCameraObject = new GameObject("IdleCamera");
            idleCameraObject.transform.SetParent(transform, false);
            _idleCamera = idleCameraObject.AddComponent<Camera>();
            _idleCamera.clearFlags = CameraClearFlags.SolidColor;
            _idleCamera.backgroundColor = UITheme.BackdropDeep;
            _idleCamera.cullingMask = 0;
            _idleCamera.depth = -50f;
            idleCameraObject.AddComponent<AudioListener>(); // without one, PlayOneShot is silent

            ScreenLayer = UIBuilder.Node("Screens", canvasRect);
            UIBuilder.FillParent(ScreenLayer, 0f);

            // The safe frame. Everything that hugs an edge lives in one of these - this one for
            // the shared top bar, one per screen for its own frame - so the inset is expressed
            // once per layer rather than once per element. See SafeAreaRect.
            HudFrame = UIBuilder.Node("HudFrame", canvasRect);
            UIBuilder.FillParent(HudFrame, 0f);
            SafeAreaRect.Attach(HudFrame);

            PanelLayer = UIBuilder.Node("Panels", canvasRect);
            UIBuilder.FillParent(PanelLayer, 0f);

            OverlayLayer = UIBuilder.Node("Overlay", canvasRect);
            UIBuilder.FillParent(OverlayLayer, 0f);
        }

        /// <summary>
        /// Adds an EventSystem that works whether the project uses the old input manager,
        /// the Input System package, or both - decided at runtime, no compile-time dependency.
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;

            GameObject host = new GameObject("EventSystem");
            host.AddComponent<EventSystem>();

            bool legacyInputAvailable;
            try
            {
                Vector3 probe = Input.mousePosition;
                legacyInputAvailable = probe.x <= float.MaxValue;
            }
            catch (Exception)
            {
                legacyInputAvailable = false;
            }

            if (legacyInputAvailable)
            {
                host.AddComponent<StandaloneInputModule>();
                return;
            }

            Type inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModule != null)
            {
                host.AddComponent(inputSystemModule);
                return;
            }

            Debug.LogError("Emberfall: no usable input module found. Set Active Input Handling to " +
                           "'Input Manager (Old)' or 'Both' in Project Settings > Player.");
            host.AddComponent<StandaloneInputModule>();
        }

        // ------------------------------------------------------------------ shared services

        public void RefreshHud()
        {
            if (_topBar != null) _topBar.Refresh(Session);
        }

        public void SetTopBarVisible(bool visible)
        {
            if (_topBar != null && _topBar.Root != null) _topBar.Root.gameObject.SetActive(visible);
        }

        /// <summary>
        /// Shows or hides the bar's full-width backing plate, leaving the plate and the pills on it
        /// exactly where they are.
        ///
        /// The Hub turns it off. Its HUD is a frame around a painting - two clusters of pills at
        /// the top corners and nothing between them - and a 132-unit opaque band across the top of
        /// the map is not a frame, it is a lid. Every other screen keeps it, because every other
        /// screen is a page that needs its header seated on something.
        /// </summary>
        public void SetTopBarBackingVisible(bool visible)
        {
            if (_topBar != null) _topBar.SetBackingVisible(visible);
        }

        /// <summary>The battle screen hides the flat backdrop so the 3D arena shows through.</summary>
        public void SetBackgroundVisible(bool visible)
        {
            if (_background != null) _background.gameObject.SetActive(visible);
        }

        public void Notify(string message)
        {
            if (_toast == null) return;
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(_toast.Play(message));
        }

        public ModalPopup Modal
        {
            get { return _modal; }
        }

        /// <summary>The one field that asks a player for a display name, when a screen that shows
        /// them to other people is first entered. Built with the other overlays; it never opens
        /// by itself.</summary>
        public NamePrompt NamePrompt { get; private set; }

        /// <summary>Queues a save a moment from now so bursts of changes collapse into one write.</summary>
        public void RequestSave()
        {
            _saveTimer = 0.6f;
        }

        /// <summary>
        /// Writes the profile to disk now and disarms the queued save.
        ///
        /// The disarm happens FIRST and stays first, because Update() calls this the instant the
        /// timer runs out and re-arming on failure would turn one broken save into a once-per-frame
        /// write storm against a disk that is already refusing.
        ///
        /// But ordering it first is exactly what made the original failure so quiet, and that part
        /// is fixed here. Previously the throw escaped into Update(), and because the timer had
        /// already been set to -1 the guard at the top of Update() returned early on every
        /// subsequent frame - so a single failed save silently ended AUTOSAVE FOR THE WHOLE
        /// SESSION. Nothing said so. Every later RequestSave() looked like it worked, no further
        /// exception was ever logged because the path was never entered again, and progress simply
        /// stopped reaching disk until the process ended.
        ///
        /// That is a data-loss bug in its own right and it does not need a null Session to fire: any
        /// IO exception - a locked file, a full disk, a permissions change - did the same thing.
        /// So the throw is caught and named rather than left to escape, and the message says
        /// plainly that progress is not on disk. RequestSave() re-arms as usual, so a transient
        /// failure now recovers on the next change instead of being permanent.
        /// </summary>
        public void SaveNow()
        {
            _saveTimer = -1f;

            if (Session == null)
            {
                Debug.LogError("GameApp.SaveNow: Session is null, so there is nothing to save and "
                    + "PROGRESS IS NOT BEING WRITTEN. " + DescribeBoot());
                return;
            }

            try
            {
                SaveService.Save(Session.Profile);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("GameApp.SaveNow: the save FAILED and PROGRESS IS NOT ON DISK. "
                    + "Autosave is idle until the next RequestSave re-arms it. " + ex);
                return;
            }

            // THE ONE HOOK THE PUBLISHED TEAM LINE NEEDS, and it is here rather than at a dozen
            // call sites for a reason worth stating: every action that can change a friend's view
            // of this player's line - levelling a hero, equipping or rerolling armor, assigning a
            // relic, picking a talent, changing the team - already ends in a save, because that is
            // how this project has recorded "something the player owns moved" since before there
            // was a cloud. So this catches all of them, cannot be forgotten by a new screen, and
            // cannot fire for a change that was not saved.
            //
            // After the save rather than before, and skipped entirely when the save threw: the
            // cloud must never advertise a line that is not on this device's disk.
            //
            // It does not upload every time. PublishSnapshotIfChanged rebuilds the snapshot and
            // compares it, so claiming idle income - which saves, and changes nothing about the
            // line - touches no network at all.
            if (Cloud != null)
            {
                try
                {
                    Cloud.PublishSnapshotIfChanged();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("Emberfall: could not publish the team line: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// One sentence a guard can append that turns "Session is null" into something actionable.
        ///
        /// The three answers mean different things. Boot COMPLETE with a null Session is the
        /// interesting one - Awake assigns Session at the top and builds the router twenty-odd
        /// lines later, so a completed boot cannot leave it null and something cleared it
        /// afterwards. A named failed stage points straight at the throw. "not started" means this
        /// GameApp never ran Awake at all, which is the duplicate-instance path.
        /// </summary>
        private static string DescribeBoot()
        {
            if (BootFailed)
                return "Awake FAILED at stage '" + BootStage + "' - the error naming it was logged "
                     + "when it happened, above this line.";
            if (BootCompleted)
                return "Awake COMPLETED, so Session was assigned and something cleared it "
                     + "afterwards - this is not a boot failure, look for whatever ran since.";
            return "Awake never completed and never reported a failure; it reached stage '"
                 + BootStage + "'. 'not started' here means this instance never ran Awake, which "
                 + "is the duplicate-instance path in Awake's first four lines.";
        }

        private void Update()
        {
            HandleBackKey();

#if UNITY_EDITOR
            DebugSlowMotionToggle();
#endif

            if (_saveTimer < 0f) return;
            _saveTimer -= Time.unscaledDeltaTime;
            if (_saveTimer <= 0f) SaveNow();
        }

        /// <summary>
        /// ONE BACK GESTURE FOR THE WHOLE GAME, in the order a player expects it.
        ///
        /// Android's hardware back button arrives through legacy Input as KeyCode.Escape, which is
        /// also the desktop key, so this one handler is both and neither needs a platform branch.
        /// The order is: the topmost open layer, then the screen history, then nothing.
        ///
        /// NOTHING is a real answer and the last branch is deliberate. On the bare Hub there is
        /// no layer to close and no screen to go back to, and an idle game's home screen is not
        /// something you back out of - a back press that quit the app from here would be a
        /// data-losing accident waiting for a thumb.
        ///
        /// Battle is the one screen the key does not leave. A fight is a simulation being played
        /// back, its result already decided, and walking out of one with a stray key press is not
        /// a navigation - the screen's own controls are what end it.
        /// </summary>
        private void HandleBackKey()
        {
            if (!UnityEngine.Input.GetKeyDown(KeyCode.Escape)) return;
            HandleBack();
        }

        /// <summary>
        /// The back gesture itself, separated from the key that raises it.
        ///
        /// Public and returning a verdict so it can be exercised without a key press: the capture
        /// tool opens a layer, calls this, and photographs the result, which is the only way to
        /// show in a screenshot that back closed the topmost layer and left the Hub alone. It is
        /// also what any future on-screen back button would call, rather than reimplementing the
        /// order of these three branches.
        ///
        /// True means the press was consumed. False means nothing happened, which on the bare Hub
        /// is the correct answer rather than a failure.
        /// </summary>
        public bool HandleBack()
        {
            // The first session's forced steps own input: back is swallowed rather than walking
            // the player out of a step.
            if (OnboardingCoach.BlocksBack) return true;

            if (Layers != null && Layers.CloseTop())
            {
                if (Audio != null) Audio.Tap();
                return true;
            }

            if (Router == null || Router.Current == null) return false;
            if (Router.Current.Id == ScreenId.MainMenu) return false;

            // A screen that knows its own step back goes first: the wave result and a campaign
            // replay were shown without being remembered, so the history cannot name them.
            if (Router.Current.TryStepBack())
            {
                if (Audio != null) Audio.Tap();
                return true;
            }

            if (Router.Current.Id == ScreenId.Battle) return false;

            if (Audio != null) Audio.Tap();
            Router.Back();
            return true;
        }

#if UNITY_EDITOR
        private bool _debugSlowMotion;

        /// <summary>
        /// Animation-pipeline task: a battle at normal pace only holds each BattleEvent on screen
        /// for a fraction of a second (BattleScreen.Handle's Wait calls), nowhere near long enough
        /// to watch a multi-second sprite clip play out. Time.timeScale is the correct single lever
        /// for that rather than a bespoke slowdown flag: every Wait() coroutine, UnitActor.Update's
        /// per-frame ticks and SpriteAnimator's frame advance all already derive from Time.deltaTime,
        /// which Unity itself scales by this value - so nothing downstream needs its own awareness
        /// of "slow mode" for this to reach it.
        ///
        /// Deliberately separate from BattleScreen's player-facing Speed button (1x/2x/4x, never
        /// below 1x - see _speed/CycleSpeed there): that field, its UI, and the shipped default
        /// pace are untouched by this. This whole block is compiled out by the #if in every build
        /// but the editor, so it can never reach a device and never ships to a player.
        /// </summary>
        private void DebugSlowMotionToggle()
        {
            if (!UnityEngine.Input.GetKeyDown(KeyCode.LeftBracket)) return;
            _debugSlowMotion = !_debugSlowMotion;
            Time.timeScale = _debugSlowMotion ? 0.2f : 1f;
        }
#endif

        /// <summary>
        /// Save, then push.
        ///
        /// The save is the part that must not fail; the sync is the part that is allowed to. On
        /// Android there is no guaranteed window at pause and none at kill, so the coroutine
        /// started here may simply be cut off - which is safe by construction rather than by luck:
        /// the disk already holds the save, an upload that never lands leaves the cloud exactly as
        /// it was, and the base revision only moves once the server has answered. A sync that dies
        /// halfway costs one sync and nothing else.
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            SaveNow();
            if (Cloud != null) Cloud.SyncOnLeaving();
        }

        private void OnApplicationQuit()
        {
            SaveNow();
            if (Cloud != null) Cloud.SyncOnLeaving();
        }

        /// <summary>Wipes progress and rebuilds a starter profile. Exposed on the main menu.</summary>
        public void ResetProgress()
        {
            SaveService.DeleteSave();
            Session = new GameSession(Content, Onboarding.CreateKeeperProfile(Content, TimeUtil.NowUnix()));
            SaveNow();
            RefreshHud();
            Router.ClearHistory();
            Router.Show(ScreenId.MainMenu, false);
            Notify("Progress reset.");
        }
    }
}
