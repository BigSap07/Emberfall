using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;
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
        public RectTransform OverlayLayer { get; private set; }
        public Sfx Audio { get; private set; }

        private HudTopBar _topBar;
        private RectTransform _background;
        private Camera _idleCamera;
        private Toast _toast;
        private ModalPopup _modal;
        private Coroutine _toastRoutine;
        private float _saveTimer = -1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            ConfigureRuntime();
            WarnIfWrongRenderPipeline();

            Content = ContentService.Load();

            PlayerProfile profile = SaveService.Load(Content);
            bool freshStart = profile == null;
            if (freshStart) profile = GameSession.CreateNewProfile(Content, TimeUtil.NowUnix());
            Session = new GameSession(Content, profile);

            BuildCanvas();
            EnsureEventSystem();

            Audio = gameObject.AddComponent<Sfx>();

            _topBar = new HudTopBar();
            _topBar.Build(ScreenLayer.parent as RectTransform);

            GameObject routerHost = new GameObject("ScreenRouter");
            routerHost.transform.SetParent(transform, false);
            Router = routerHost.AddComponent<ScreenRouter>();
            Router.Build(this, ScreenLayer, OverlayLayer);

            _toast = new Toast(OverlayLayer);
            _modal = new ModalPopup(OverlayLayer);

            _topBar.Root.SetAsLastSibling();
            OverlayLayer.SetAsLastSibling();

            RefreshHud();
            Router.Show(ScreenId.MainMenu, false);

            if (freshStart) SaveNow();
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

            OverlayLayer = UIBuilder.Node("Overlay", canvasRect);
            UIBuilder.FillParent(OverlayLayer, 0f);
        }

        /// <summary>
        /// Adds an EventSystem that works whether the project uses the old input manager,
        /// the Input System package, or both - decided at runtime, no compile-time dependency.
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;

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

        /// <summary>Queues a save a moment from now so bursts of changes collapse into one write.</summary>
        public void RequestSave()
        {
            _saveTimer = 0.6f;
        }

        public void SaveNow()
        {
            _saveTimer = -1f;
            SaveService.Save(Session.Profile);
        }

        private void Update()
        {
            if (_saveTimer < 0f) return;
            _saveTimer -= Time.unscaledDeltaTime;
            if (_saveTimer <= 0f) SaveNow();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveNow();
        }

        private void OnApplicationQuit()
        {
            SaveNow();
        }

        /// <summary>Wipes progress and rebuilds a starter profile. Exposed on the main menu.</summary>
        public void ResetProgress()
        {
            SaveService.DeleteSave();
            Session = new GameSession(Content, GameSession.CreateNewProfile(Content, TimeUtil.NowUnix()));
            SaveNow();
            RefreshHud();
            Router.ClearHistory();
            Router.Show(ScreenId.MainMenu, false);
            Notify("Progress reset.");
        }
    }
}
