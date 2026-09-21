#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Emberfall.UI;

namespace Emberfall.Dev
{
    /// <summary>
    /// The arena's chrome, built in code like every other screen in this project.
    ///
    /// ONE CANVAS SETUP, NOT TWO. The scaler is the game's own - ScaleWithScreenSize at
    /// UITheme.ReferenceResolution with match 0.5, read from UITheme rather than restated - so a
    /// control that is thumb-sized in the game is thumb-sized here. The first version of this file
    /// invented a second reference resolution (1600x900) and every offset in it was then written
    /// in a coordinate space nothing else in the project uses.
    ///
    /// EVERYTHING IS ANCHORED TO AN EDGE. Nothing is positioned by measuring from the screen
    /// origin: the bar stretches across the top, the log across the bottom, the drawers to their
    /// own side at full height, and the corner buttons to their corners. Rows of controls are
    /// HorizontalLayoutGroups, which is what fixes the hole the old bar had - a button hidden in
    /// Manual mode used to leave its baked-in gap behind, because its neighbours' positions had
    /// been computed once at build time.
    ///
    /// SAFE AREA. Everything hangs off one child rect that is resized to Screen.safeArea, so a
    /// notch or a gesture bar eats into the chrome rather than covering it. The battlefield is
    /// behind that rect and deliberately not inset: the fight should use the whole screen.
    ///
    /// FLAT ON PURPOSE. This does not use the game's theme beyond its font and its scaler. A tool
    /// wants labels you can read at a glance, and dressing it in the game's frames would make it
    /// harder to tell the tool from the thing being tested.
    /// </summary>
    public sealed class DevArenaUi
    {
        // The same numbers the game's own canvas uses. Not restated here - read from UITheme, so
        // there is exactly one answer to "what is a pixel in this project".
        public static Vector2 Reference { get { return UITheme.ReferenceResolution; } }
        public const float Match = 0.5f;

        public const float BarHeight = 72f;
        public const float LogHeight = 132f;
        public const float LogHeightExpanded = 372f;
        public const float SetupWidth = 560f;
        public const float RightWidth = 580f;

        public Canvas Canvas;
        public RectTransform Root;      // the whole canvas
        public RectTransform SafeArea;  // everything except the battlefield hangs off this

        // top bar
        public Button ModeButton;
        public Button PlayButton;
        public readonly List<Button> SpeedButtons = new List<Button>();
        public readonly float[] Speeds = { 0.25f, 0.5f, 1f, 2f, 4f };
        public Button NextActionButton;
        public Button EndRoundButton;
        public Button ResetButton;
        public InputField SeedField;
        public Button RerollButton;
        public Button HelpButton;
        public Button BackButton;
        public Text RoundLabel;

        // overlays
        public RectTransform UnitLayer;
        public RectTransform ActionBar;
        public Button BasicButton;
        public Button ActiveButton;
        public Text ActionTitle;
        public Text Toast;
        public CanvasGroup ToastGroup;

        // log
        public RectTransform LogPanel;
        public RectTransform LogContent;
        public ScrollRect LogScroll;
        public Button LogExpandButton;

        // drawers
        public RectTransform SetupDrawer;
        public RectTransform RightDrawer;
        public Button SetupEdge, UnitEdge;
        public Button UnitTab, AnimTab, SetupClose, RightClose;
        public RectTransform UnitPage, AnimPage;
        public RectTransform RosterContent;
        public InputField RosterSearch;
        public RectTransform BoardRows;
        public Button SaveBoardButton, LoadBoardButton, ClearBoardButton, ApplyBoardButton;

        // unit page
        public Text UnitTitle, UnitStats, UnitStatuses;
        public Button KillButton, ReviveButton, Hp10Button, Hp50Button, HpFullButton;
        public Button EnergyFullButton, EnergyZeroButton;

        // animation page
        public RectTransform ClipRow;
        public Button AnimPauseButton, AnimStepButton, AnimLoopButton, SoloButton, DetailsButton;
        public Text AnimSpeedLabel, ClipDetails;
        public Slider AnimSpeed;
        public RectTransform DetailsPanel;

        // help
        public RectTransform HelpPanel;
        public Button HelpClose;

        private static readonly Color Ink = new Color(0.93f, 0.95f, 0.98f, 1f);
        private static readonly Color Dim = new Color(0.66f, 0.70f, 0.78f, 1f);
        private static readonly Color Chrome = new Color(0.07f, 0.08f, 0.11f, 0.94f);
        private static readonly Color Panelly = new Color(0.10f, 0.11f, 0.15f, 0.98f);
        private static readonly Color Live = new Color(0.16f, 0.46f, 0.72f, 1f);
        private static readonly Color Warm = new Color(0.58f, 0.34f, 0.10f, 1f);

        // ================================================================== construction

        public void Build(Transform parent)
        {
            GameObject canvasGo = new GameObject("DevArenaCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent, false);
            Canvas = canvasGo.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = Match;
            Root = canvasGo.GetComponent<RectTransform>();

            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem",
                    typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(parent, false);
            }

            // The battlefield overlay sits OUTSIDE the safe area: bars belong over their units,
            // wherever those units are, and a notch is a better thing to lose a health bar behind
            // than a reason to squeeze the whole fight inward.
            UnitLayer = Blank("UnitLayer", Root);
            Stretch(UnitLayer);

            SafeArea = Blank("SafeArea", Root);
            Stretch(SafeArea);

            BuildTopBar();
            BuildLog();
            BuildEdges();
            BuildActionBar();
            BuildToast();
            BuildSetupDrawer();
            BuildRightDrawer();
            BuildHelp();
        }

        /// <summary>Resizes the chrome rect to the device's safe area. Called every frame, because
        /// it changes on rotation and on the editor's game-view size.</summary>
        public void ApplySafeArea()
        {
            if (SafeArea == null) return;
            Rect area = Screen.safeArea;
            float w = Mathf.Max(1f, Screen.width), h = Mathf.Max(1f, Screen.height);
            Vector2 min = new Vector2(area.xMin / w, area.yMin / h);
            Vector2 max = new Vector2(area.xMax / w, area.yMax / h);
            if (SafeArea.anchorMin == min && SafeArea.anchorMax == max) return;
            SafeArea.anchorMin = min;
            SafeArea.anchorMax = max;
            SafeArea.offsetMin = Vector2.zero;
            SafeArea.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------ top bar

        private void BuildTopBar()
        {
            Image bar = Panel("TopBar", SafeArea, Chrome);
            RectTransform r = bar.rectTransform;
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(0f, -BarHeight);
            r.offsetMax = Vector2.zero;

            // Left half: the controls, in a layout group so a button hidden in Manual mode takes
            // its gap with it instead of leaving a hole where it used to sit.
            RectTransform left = Blank("Left", r);
            left.anchorMin = new Vector2(0f, 0f);
            left.anchorMax = new Vector2(1f, 1f);
            // The left row stops short of the right row's fixed block (round label, BACK, ?), so
            // the two can never draw over each other however wide the screen is.
            left.offsetMin = new Vector2(12f, 8f);
            left.offsetMax = new Vector2(-(300f + 110f + 64f + 40f), -8f);
            HorizontalLayoutGroup lg = left.gameObject.AddComponent<HorizontalLayoutGroup>();
            lg.childAlignment = TextAnchor.MiddleLeft;
            lg.spacing = 6f;
            lg.childControlWidth = true;
            lg.childControlHeight = true;
            lg.childForceExpandWidth = false;
            lg.childForceExpandHeight = true;

            ModeButton = Btn("Mode", left, "MANUAL", 170f, Warm);
            PlayButton = Btn("Play", left, "PLAY", 110f, Panelly);
            for (int i = 0; i < Speeds.Length; i++)
                SpeedButtons.Add(Btn("Speed" + i, left, Speeds[i] + "x", 74f, Panelly));
            NextActionButton = Btn("NextAction", left, "NEXT ACTION", 170f, Panelly);
            EndRoundButton = Btn("EndRound", left, "END ROUND", 156f, Panelly);
            ResetButton = Btn("Reset", left, "RESET", 108f, Panelly);
            Gap(left, 10f);
            Text seedCaption = Label("SeedCaption", left, "SEED", 18, Dim, TextAnchor.MiddleLeft);
            Fixed(seedCaption.rectTransform, 58f, 40f);
            SeedField = Field("Seed", left, "12345", 130f);
            RerollButton = Btn("Reroll", left, "RE-ROLL", 116f, Panelly);

            // Right half: read-only status and the two corner buttons, right-aligned so they stay
            // pinned to the edge at any width.
            RectTransform right = Blank("Right", r);
            right.anchorMin = new Vector2(0f, 0f);
            right.anchorMax = new Vector2(1f, 1f);
            right.offsetMin = new Vector2(12f, 8f);
            right.offsetMax = new Vector2(-12f, -8f);
            HorizontalLayoutGroup rg = right.gameObject.AddComponent<HorizontalLayoutGroup>();
            rg.childAlignment = TextAnchor.MiddleRight;
            rg.spacing = 6f;
            rg.childControlWidth = true;
            rg.childControlHeight = true;
            rg.childForceExpandWidth = false;
            rg.childForceExpandHeight = true;

            RoundLabel = Label("Round", right, "ROUND 1", 20, Ink, TextAnchor.MiddleRight);
            Fixed(RoundLabel.rectTransform, 300f, 40f);
            BackButton = Btn("Back", right, "BACK", 110f, Panelly);
            HelpButton = Btn("Help", right, "?", 64f, Panelly);
            Gap(right, 4f);
        }

        // ------------------------------------------------------------------ log

        private void BuildLog()
        {
            Image p = Panel("Log", SafeArea, Chrome);
            LogPanel = p.rectTransform;
            LogPanel.anchorMin = new Vector2(0f, 0f);
            LogPanel.anchorMax = new Vector2(1f, 0f);
            LogPanel.pivot = new Vector2(0.5f, 0f);
            LogPanel.offsetMin = Vector2.zero;
            LogPanel.offsetMax = new Vector2(0f, LogHeight);

            // A viewport with a mask, so a fight that produces four hundred lines cannot paint them
            // up the screen over the fight that produced them. The old log had no mask and did.
            RectTransform view = Blank("LogView", LogPanel);
            view.anchorMin = new Vector2(0f, 0f);
            view.anchorMax = new Vector2(1f, 1f);
            view.offsetMin = new Vector2(16f, 8f);
            view.offsetMax = new Vector2(-170f, -8f);
            view.gameObject.AddComponent<RectMask2D>();

            LogContent = Blank("LogContent", view);
            LogContent.anchorMin = new Vector2(0f, 0f);
            LogContent.anchorMax = new Vector2(1f, 0f);
            LogContent.pivot = new Vector2(0.5f, 0f);
            // sizeDelta.x is Unity's 100 until something clears it, and a horizontally stretched
            // rect reads that as "100 WIDER than my anchors" - so the column hung 50 units off each
            // side and the first seven characters of every log line sat off the left of the screen.
            LogContent.sizeDelta = new Vector2(0f, LogContent.sizeDelta.y);
            VerticalLayoutGroup v = LogContent.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.LowerLeft;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            v.spacing = 1f;
            ContentSizeFitter f = LogContent.gameObject.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            LogScroll = view.gameObject.AddComponent<ScrollRect>();
            LogScroll.content = LogContent;
            LogScroll.viewport = view;
            LogScroll.horizontal = false;
            LogScroll.movementType = ScrollRect.MovementType.Clamped;
            LogScroll.scrollSensitivity = 30f;

            LogExpandButton = Btn("LogExpand", LogPanel, "EXPAND", 150f, Panelly);
            Corner(LogExpandButton, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -10f), 150f, 44f);
        }

        // ------------------------------------------------------------------ drawer handles

        private void BuildEdges()
        {
            SetupEdge = Btn("SetupEdge", SafeArea, "SETUP", 150f, Panelly);
            Corner(SetupEdge, new Vector2(0f, 0f), new Vector2(0f, 0f),
                   new Vector2(12f, LogHeight + 12f), 150f, 52f);

            UnitEdge = Btn("UnitEdge", SafeArea, "UNIT", 150f, Panelly);
            Corner(UnitEdge, new Vector2(1f, 0f), new Vector2(1f, 0f),
                   new Vector2(-12f, LogHeight + 12f), 150f, 52f);
        }

        // ------------------------------------------------------------------ action bar

        private void BuildActionBar()
        {
            Image p = Panel("ActionBar", SafeArea, Panelly);
            ActionBar = p.rectTransform;
            ActionBar.anchorMin = ActionBar.anchorMax = new Vector2(0f, 0f);
            ActionBar.pivot = new Vector2(0.5f, 0f);
            ActionBar.sizeDelta = new Vector2(330f, 104f);

            ActionTitle = Label("Who", ActionBar, "", 17, Dim, TextAnchor.UpperCenter);
            RectTransform t = ActionTitle.rectTransform;
            t.anchorMin = new Vector2(0f, 1f);
            t.anchorMax = new Vector2(1f, 1f);
            t.pivot = new Vector2(0.5f, 1f);
            t.offsetMin = new Vector2(8f, -32f);
            t.offsetMax = new Vector2(-8f, -6f);

            RectTransform row = Blank("Row", ActionBar);
            row.anchorMin = new Vector2(0f, 0f);
            row.anchorMax = new Vector2(1f, 0f);
            row.pivot = new Vector2(0.5f, 0f);
            row.offsetMin = new Vector2(10f, 10f);
            row.offsetMax = new Vector2(-10f, 66f);
            HorizontalLayoutGroup g = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = 8f;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = true;
            g.childForceExpandHeight = true;

            BasicButton = Btn("Basic", row, "BASIC", 150f, Live);
            ActiveButton = Btn("Active", row, "ACTIVE", 150f, Live);
            ActionBar.gameObject.SetActive(false);
        }

        private void BuildToast()
        {
            Image p = Panel("Toast", SafeArea, new Color(0.46f, 0.14f, 0.09f, 0.96f));
            RectTransform r = p.rectTransform;
            r.anchorMin = new Vector2(0.15f, 1f);
            r.anchorMax = new Vector2(0.85f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(0f, -(BarHeight + 66f));
            r.offsetMax = new Vector2(0f, -(BarHeight + 12f));
            Toast = Label("ToastText", r, "", 20, Ink, TextAnchor.MiddleCenter);
            Stretch(Toast.rectTransform);
            ToastGroup = p.gameObject.AddComponent<CanvasGroup>();
            ToastGroup.alpha = 0f;
            ToastGroup.blocksRaycasts = false;
        }

        // ------------------------------------------------------------------ drawers

        private void BuildSetupDrawer()
        {
            Image p = Panel("SetupDrawer", SafeArea, Panelly);
            SetupDrawer = p.rectTransform;
            EdgeDrawer(SetupDrawer, true, SetupWidth);

            RectTransform head = HeaderRow(SetupDrawer, "SETUP");
            SetupClose = Btn("SetupClose", head, "CLOSE", 120f, Chrome);

            RectTransform body = Body(SetupDrawer);
            VerticalLayoutGroup bv = body.gameObject.AddComponent<VerticalLayoutGroup>();
            bv.childControlWidth = true;
            bv.childControlHeight = true;
            bv.spacing = 8f;
            bv.childForceExpandHeight = false;
            bv.childForceExpandWidth = true;

            BoardRows = Blank("BoardRows", body);
            VerticalLayoutGroup rows = BoardRows.gameObject.AddComponent<VerticalLayoutGroup>();
            rows.childControlWidth = true;
            rows.childControlHeight = true;
            rows.spacing = 3f;
            rows.childForceExpandHeight = false;
            rows.childForceExpandWidth = true;
            ContentSizeFitter rf = BoardRows.gameObject.AddComponent<ContentSizeFitter>();
            rf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform buttons = Row(body, 48f);
            SaveBoardButton = Btn("SaveBoard", buttons, "SAVE", 120f, Chrome);
            LoadBoardButton = Btn("LoadBoard", buttons, "LOAD", 120f, Chrome);
            ClearBoardButton = Btn("ClearBoard", buttons, "CLEAR", 120f, Chrome);
            ApplyBoardButton = Btn("ApplyBoard", buttons, "RESTART", 140f, Live);

            RectTransform search = Row(body, 48f);
            RosterSearch = Field("RosterSearch", search, "", 480f);

            RectTransform rosterHost = Blank("RosterHost", body);
            LayoutElement grow = rosterHost.gameObject.AddComponent<LayoutElement>();
            grow.flexibleHeight = 1f;
            grow.minHeight = 120f;
            RosterContent = Scroll("Roster", rosterHost);

            SetupDrawer.gameObject.SetActive(false);
        }

        private void BuildRightDrawer()
        {
            Image p = Panel("RightDrawer", SafeArea, Panelly);
            RightDrawer = p.rectTransform;
            EdgeDrawer(RightDrawer, false, RightWidth);

            RectTransform head = HeaderRow(RightDrawer, null);
            UnitTab = Btn("UnitTab", head, "UNIT", 150f, Live);
            AnimTab = Btn("AnimTab", head, "ANIMATION", 190f, Chrome);
            RightClose = Btn("RightClose", head, "CLOSE", 120f, Chrome);

            RectTransform body = Body(RightDrawer);

            UnitPage = Blank("UnitPage", body);
            Stretch(UnitPage);
            VerticalLayoutGroup uv = UnitPage.gameObject.AddComponent<VerticalLayoutGroup>();
            uv.childControlWidth = true;
            uv.childControlHeight = true;
            uv.spacing = 8f;
            uv.childForceExpandHeight = false;
            uv.childForceExpandWidth = true;

            UnitTitle = Label("UnitTitle", UnitPage, "no unit selected", 22, Ink, TextAnchor.UpperLeft);
            Fixed(UnitTitle.rectTransform, 0f, 32f);
            UnitStats = Label("UnitStats", UnitPage, "", 18, Dim, TextAnchor.UpperLeft);
            Fixed(UnitStats.rectTransform, 0f, 200f);
            UnitStatuses = Label("UnitStatuses", UnitPage, "", 18, Ink, TextAnchor.UpperLeft);
            LayoutElement statusGrow = UnitStatuses.gameObject.AddComponent<LayoutElement>();
            statusGrow.flexibleHeight = 1f;
            statusGrow.minHeight = 80f;

            RectTransform hpRow = Row(UnitPage, 48f);
            Hp10Button = Btn("Hp10", hpRow, "HP 10%", 150f, Chrome);
            Hp50Button = Btn("Hp50", hpRow, "HP 50%", 150f, Chrome);
            HpFullButton = Btn("HpFull", hpRow, "HP FULL", 150f, Chrome);
            RectTransform enRow = Row(UnitPage, 48f);
            EnergyFullButton = Btn("EnFull", enRow, "ENERGY FULL", 180f, Chrome);
            EnergyZeroButton = Btn("EnZero", enRow, "ENERGY 0", 150f, Chrome);
            RectTransform lifeRow = Row(UnitPage, 48f);
            KillButton = Btn("Kill", lifeRow, "KILL", 150f, Chrome);
            ReviveButton = Btn("Revive", lifeRow, "REVIVE", 150f, Chrome);

            AnimPage = Blank("AnimPage", body);
            Stretch(AnimPage);
            VerticalLayoutGroup av = AnimPage.gameObject.AddComponent<VerticalLayoutGroup>();
            av.childControlWidth = true;
            av.childControlHeight = true;
            av.spacing = 8f;
            av.childForceExpandHeight = false;
            av.childForceExpandWidth = true;

            ClipRow = Blank("ClipRow", AnimPage);
            GridLayoutGroup g = ClipRow.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(168f, 48f);
            g.spacing = new Vector2(8f, 8f);
            LayoutElement clipHeight = ClipRow.gameObject.AddComponent<LayoutElement>();
            clipHeight.preferredHeight = 176f;

            RectTransform animRow = Row(AnimPage, 48f);
            AnimPauseButton = Btn("AnimPause", animRow, "PAUSE", 150f, Chrome);
            AnimStepButton = Btn("AnimStep", animRow, "STEP", 130f, Chrome);
            AnimLoopButton = Btn("AnimLoop", animRow, "LOOP", 130f, Live);
            RectTransform animRow2 = Row(AnimPage, 48f);
            SoloButton = Btn("Solo", animRow2, "SOLO", 150f, Chrome);
            DetailsButton = Btn("Details", animRow2, "DETAILS", 150f, Chrome);

            AnimSpeedLabel = Label("AnimSpeedLabel", AnimPage, "clip speed 1.00x", 18, Dim, TextAnchor.UpperLeft);
            Fixed(AnimSpeedLabel.rectTransform, 0f, 26f);
            AnimSpeed = MakeSlider("AnimSpeed", AnimPage, 0.05f, 2f, 1f);

            DetailsPanel = Blank("DetailsPanel", AnimPage);
            LayoutElement detailGrow = DetailsPanel.gameObject.AddComponent<LayoutElement>();
            detailGrow.flexibleHeight = 1f;
            detailGrow.minHeight = 120f;
            ClipDetails = Label("ClipDetails", DetailsPanel, "", 16, Dim, TextAnchor.UpperLeft);
            Stretch(ClipDetails.rectTransform);
            DetailsPanel.gameObject.SetActive(false);

            AnimPage.gameObject.SetActive(false);
            RightDrawer.gameObject.SetActive(false);
        }

        /// <summary>A drawer: pinned to one side, running the full height between the bar and the
        /// log, overlaying the battlefield rather than squeezing it.</summary>
        private static void EdgeDrawer(RectTransform r, bool leftSide, float width)
        {
            r.anchorMin = new Vector2(leftSide ? 0f : 1f, 0f);
            r.anchorMax = new Vector2(leftSide ? 0f : 1f, 1f);
            r.pivot = new Vector2(leftSide ? 0f : 1f, 0.5f);
            r.sizeDelta = new Vector2(width, 0f);
            r.offsetMin = new Vector2(leftSide ? 0f : -width, LogHeight);
            r.offsetMax = new Vector2(leftSide ? width : 0f, -BarHeight);
        }

        private RectTransform HeaderRow(RectTransform drawer, string title)
        {
            RectTransform head = Blank("Header", drawer);
            head.anchorMin = new Vector2(0f, 1f);
            head.anchorMax = new Vector2(1f, 1f);
            head.pivot = new Vector2(0.5f, 1f);
            head.offsetMin = new Vector2(12f, -64f);
            head.offsetMax = new Vector2(-12f, -12f);
            HorizontalLayoutGroup g = head.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = 8f;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childAlignment = TextAnchor.MiddleLeft;
            g.childForceExpandWidth = false;
            g.childForceExpandHeight = false;
            if (!string.IsNullOrEmpty(title))
            {
                Text t = Label("Title", head, title, 22, Ink, TextAnchor.MiddleLeft);
                Fixed(t.rectTransform, 160f, 44f);
            }
            return head;
        }

        private static RectTransform Body(RectTransform drawer)
        {
            RectTransform body = Blank("Body", drawer);
            body.anchorMin = new Vector2(0f, 0f);
            body.anchorMax = new Vector2(1f, 1f);
            body.offsetMin = new Vector2(12f, 12f);
            body.offsetMax = new Vector2(-12f, -76f);
            return body;
        }

        private void BuildHelp()
        {
            Image p = Panel("Help", SafeArea, new Color(0.05f, 0.05f, 0.08f, 0.98f));
            HelpPanel = p.rectTransform;
            HelpPanel.anchorMin = new Vector2(0.08f, 0.08f);
            HelpPanel.anchorMax = new Vector2(0.92f, 0.92f);
            HelpPanel.offsetMin = Vector2.zero;
            HelpPanel.offsetMax = Vector2.zero;

            Text t = Label("HelpText", HelpPanel, HelpText, 19, Ink, TextAnchor.UpperLeft);
            RectTransform tr = t.rectTransform;
            tr.anchorMin = new Vector2(0f, 0f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.offsetMin = new Vector2(32f, 80f);
            tr.offsetMax = new Vector2(-32f, -26f);

            HelpClose = Btn("HelpClose", HelpPanel, "CLOSE", 180f, Live);
            Corner(HelpClose, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), 180f, 48f);
            HelpPanel.gameObject.SetActive(false);
        }

        public const string HelpText =
            "DEV ARENA\n\n"
            + "This is the real battle view driving the real engine. CombatSimulation.Run is Begin()\n"
            + "plus StepNatural() until the fight ends, so AUTO is the game, not an imitation of it.\n\n"
            + "MANUAL - tap any unit on either side, then BASIC or ACTIVE, then tap a target. The\n"
            + "action plays out in real time. If the engine redirects or refuses your pick, a message\n"
            + "says why.\n\n"
            + "WHAT A HAND-DRIVEN ACTION IS\n"
            + "  It is a TURN. It ticks that unit's damage-over-time, repays its turn debt, fires its\n"
            + "  turn-start passives, resolves the ability, fires the after-action passives and\n"
            + "  expires its statuses - the whole pipeline, in order.\n"
            + "  It SPENDS that unit's natural turn for the round. It does not act twice.\n"
            + "  Stun and Silence REFUSE out loud rather than quietly substituting a basic.\n"
            + "  An active empties the bar. Ignoring energy lets it cast below cost, not for free.\n"
            + "  A forced target replaces the single-enemy pick and nothing else. Taunt still wins,\n"
            + "  and a Restricted or Concealed body is refused - both are reported.\n"
            + "  END ROUND ends the round where it stands and opens the next. Units that had not\n"
            + "  acted do not act; this engine has no other end-of-round work.\n"
            + "  KILL, REVIVE and the HP and ENERGY buttons are cheats, not events in the fiction.\n"
            + "  They move the number and fire no passive.\n\n"
            + "ANCHORS - the ORANGE mark is where the engine puts the unit's feet. The RED line is\n"
            + "the bottom edge of the sprite frame itself. When they disagree, the clip's foot\n"
            + "fraction is wrong and the unit will float or sink in a real battle.";

        // ================================================================== widgets

        public static Image Panel(string name, Transform parent, Color colour)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.color = colour;
            return img;
        }

        public static RectTransform Blank(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static Text Label(string name, Transform parent, string text, int size, Color colour,
                                 TextAnchor anchor)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text t = go.GetComponent<Text>();
            t.font = UITheme.Font;
            t.fontSize = size;
            t.color = colour;
            t.alignment = anchor;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            // Fill the parent by default. A Text left on Unity's 100x100 default sits centred in
            // whatever row it joins, and then reads as text with its start cut off.
            Stretch(t.rectTransform);
            return t;
        }

        /// <summary>
        /// A button that works inside a layout group and outside one. Its size comes from a
        /// LayoutElement, so a row can lay it out, and Corner() can pin it when there is no row.
        /// </summary>
        public static Button Btn(string name, Transform parent, string text, float width, Color colour)
        {
            Image img = Panel(name, parent, colour);
            RectTransform r = img.rectTransform;
            r.sizeDelta = new Vector2(width, 48f);
            LayoutElement le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.minWidth = width;
            le.preferredHeight = 48f;

            Button b = img.gameObject.AddComponent<Button>();
            ColorBlock cb = b.colors;
            cb.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
            cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            b.colors = cb;

            Text t = Label(name + "Text", r, text, 18, Ink, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
            return b;
        }

        /// <summary>Pins a widget to a corner of its parent. The only positioning in this file that
        /// names a number, and it names it as an inset from an edge rather than from the origin.</summary>
        public static void Corner(Component c, Vector2 anchor, Vector2 pivot, Vector2 inset,
                                  float width, float height)
        {
            RectTransform r = c.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = pivot;
            r.sizeDelta = new Vector2(width, height);
            r.anchoredPosition = inset;
            LayoutElement le = r.GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = true;
        }

        public static RectTransform Row(Transform parent, float height)
        {
            RectTransform row = Blank("Row", parent);
            HorizontalLayoutGroup g = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = 8f;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childAlignment = TextAnchor.MiddleLeft;
            g.childForceExpandWidth = false;
            // NOT expand-height. A horizontal group that force-expands height reports a flexible
            // height of its own to whatever contains it, so every row in a drawer claimed a share
            // of the leftover space and a row of 48-unit buttons came out 200 units tall.
            g.childForceExpandHeight = false;
            LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            le.flexibleHeight = 0f;
            return row;
        }

        private static void Gap(Transform parent, float width)
        {
            RectTransform g = Blank("Gap", parent);
            LayoutElement le = g.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.minWidth = width;
        }

        public static void Fixed(RectTransform r, float width, float height)
        {
            LayoutElement le = r.GetComponent<LayoutElement>();
            if (le == null) le = r.gameObject.AddComponent<LayoutElement>();
            if (width > 0f) { le.preferredWidth = width; le.minWidth = width; }
            le.preferredHeight = height;
            le.minHeight = height;
        }

        public static InputField Field(string name, Transform parent, string value, float width)
        {
            Image img = Panel(name, parent, new Color(0.03f, 0.03f, 0.05f, 1f));
            RectTransform r = img.rectTransform;
            r.sizeDelta = new Vector2(width, 44f);
            LayoutElement le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.minWidth = width;
            le.preferredHeight = 44f;

            Text t = Label(name + "Text", r, value, 18, Ink, TextAnchor.MiddleLeft);
            t.raycastTarget = true;
            RectTransform tr = t.rectTransform;
            tr.anchorMin = new Vector2(0f, 0f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.offsetMin = new Vector2(10f, 3f);
            tr.offsetMax = new Vector2(-10f, -3f);

            InputField f = img.gameObject.AddComponent<InputField>();
            f.textComponent = t;
            f.text = value;
            return f;
        }

        private static Slider MakeSlider(string name, Transform parent, float min, float max, float value)
        {
            RectTransform root = Blank(name, parent);
            LayoutElement le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 34f;
            le.minHeight = 34f;

            Image track = Panel("Track", root, new Color(0.04f, 0.05f, 0.07f, 1f));
            Stretch(track.rectTransform);
            RectTransform fillArea = Blank("FillArea", root);
            Stretch(fillArea);
            Image fill = Panel("Fill", fillArea, Live);
            Stretch(fill.rectTransform);

            Slider s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.targetGraphic = track;
            s.minValue = min;
            s.maxValue = max;
            s.value = value;
            return s;
        }

        /// <summary>A masked, scrolling column that fills whatever rect it is given.</summary>
        public static RectTransform Scroll(string name, Transform parent)
        {
            RectTransform view = Blank(name, parent);
            Stretch(view);
            view.gameObject.AddComponent<RectMask2D>();

            RectTransform content = Blank(name + "Content", view);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, content.sizeDelta.y);   // see BuildLog
            VerticalLayoutGroup v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 3f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            ContentSizeFitter f = content.gameObject.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.viewport = view;
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 30f;
            return content;
        }

        public static void Stretch(RectTransform r)
        {
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 1f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        public static void SetLabel(Button b, string text)
        {
            Text t = b.GetComponentInChildren<Text>(true);
            if (t != null) t.text = text;
        }

        public static void SetColour(Button b, Color c)
        {
            Image i = b.GetComponent<Image>();
            if (i != null) i.color = c;
        }

        public static Color Accent { get { return Live; } }
        public static Color Quiet { get { return Chrome; } }
        public static Color Hot { get { return Warm; } }
        public static Color Text0 { get { return Ink; } }
        public static Color Text1 { get { return Dim; } }
    }
}
#endif
