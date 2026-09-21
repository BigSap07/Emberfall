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
    /// FLAT ON PURPOSE. This does not use the game's theme: a tool wants labels you can read at a
    /// glance and hit targets you can find with a thumb, and dressing it in the game's frames would
    /// make it harder to tell the tool from the thing being tested. The one thing it does borrow is
    /// UITheme.Font, so text renders the same way it does everywhere else.
    ///
    /// ARENA FIRST. The battlefield is not a panel - it is the camera, and everything here is laid
    /// over it. The top bar is one row, the log is four lines, and both drawers start closed, so
    /// what is on screen at rest is the fight.
    /// </summary>
    public sealed class DevArenaUi
    {
        public const float RefW = 1600f, RefH = 900f;

        public Canvas Canvas;
        public RectTransform Root;

        // top bar
        public Button ModeButton;
        public Text ModeLabel;
        public Button PlayButton;
        public Text PlayLabel;
        public readonly List<Button> SpeedButtons = new List<Button>();
        public readonly float[] Speeds = { 0.25f, 0.5f, 1f, 2f, 4f };
        public Button NextActionButton;
        public Button EndRoundButton;
        public Button ResetButton;
        public InputField SeedField;
        public Button RerollButton;
        public Button HelpButton;
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
        public Button LogExpandButton;
        public Text LogExpandLabel;

        // drawers
        public RectTransform SetupDrawer;
        public RectTransform RightDrawer;
        public Button SetupTab, UnitTab, AnimTab, SetupClose, RightClose;
        public RectTransform UnitPage, AnimPage;
        public RectTransform RosterContent;
        public InputField RosterSearch;
        public RectTransform BoardRows;
        public Button SaveBoardButton, LoadBoardButton, ClearBoardButton, ApplyBoardButton;

        // unit page
        public Text UnitTitle, UnitStats, UnitStatuses;
        public Button KillButton, ReviveButton, Hp10Button, Hp50Button, HpFullButton, EnergyFullButton, EnergyZeroButton;

        // animation page
        public RectTransform ClipRow;
        public Button AnimPauseButton, AnimStepButton, AnimLoopButton, SoloButton, DetailsButton;
        public Text AnimSpeedLabel, ClipDetails;
        public Slider AnimSpeed;
        public RectTransform DetailsPanel;

        // help
        public RectTransform HelpPanel;
        public Button HelpClose;

        private static readonly Color Ink = new Color(0.92f, 0.94f, 0.98f, 1f);
        private static readonly Color Dim = new Color(0.64f, 0.68f, 0.76f, 1f);
        private static readonly Color Chrome = new Color(0.07f, 0.08f, 0.11f, 0.92f);
        private static readonly Color Panelly = new Color(0.10f, 0.11f, 0.15f, 0.97f);
        private static readonly Color Live = new Color(0.16f, 0.46f, 0.72f, 1f);

        // ------------------------------------------------------------------ construction

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
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            Root = canvasGo.GetComponent<RectTransform>();

            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem",
                    typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(parent, false);
            }

            UnitLayer = Blank("UnitLayer", Root);
            Stretch(UnitLayer);
            UnitLayer.GetComponent<RectTransform>();

            BuildTopBar();
            BuildLog();
            BuildActionBar();
            BuildToast();
            BuildSetupDrawer();
            BuildRightDrawer();
            BuildHelp();
        }

        private void BuildTopBar()
        {
            Image bar = Panel("TopBar", Root, Chrome);
            RectTransform r = bar.rectTransform;
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(0f, -60f); r.offsetMax = new Vector2(0f, 0f);

            float x = 12f;
            ModeButton = Btn("Mode", r, "MANUAL", 150f, ref x, Live);
            ModeLabel = ModeButton.GetComponentInChildren<Text>();
            x += 10f;
            PlayButton = Btn("Play", r, "PLAY", 96f, ref x, Panelly);
            PlayLabel = PlayButton.GetComponentInChildren<Text>();
            for (int i = 0; i < Speeds.Length; i++)
                SpeedButtons.Add(Btn("Speed" + i, r, Speeds[i] + "x", 62f, ref x, Panelly));
            x += 10f;
            NextActionButton = Btn("NextAction", r, "NEXT ACTION", 150f, ref x, Panelly);
            x += 10f;
            EndRoundButton = Btn("EndRound", r, "END ROUND", 136f, ref x, Panelly);
            ResetButton = Btn("Reset", r, "RESET", 96f, ref x, Panelly);

            x += 14f;
            Label("SeedCaption", r, "SEED", 16, Dim, TextAnchor.MiddleLeft, x, 54f);
            x += 54f;
            SeedField = Field("Seed", r, "12345", 110f, ref x);
            RerollButton = Btn("Reroll", r, "RE-ROLL", 96f, ref x, Panelly);

            HelpButton = Btn("Help", r, "?", 46f, Root, new Vector2(1f, 1f), new Vector2(-58f, -74f), Panelly);
            RoundLabel = Label("Round", r, "ROUND 1", 18, Ink, TextAnchor.MiddleRight, -180f, 170f);
            RectTransform rl = RoundLabel.rectTransform;
            rl.anchorMin = new Vector2(1f, 0f); rl.anchorMax = new Vector2(1f, 1f);
            rl.offsetMin = new Vector2(-190f, 0f); rl.offsetMax = new Vector2(-12f, 0f);
        }

        private void BuildLog()
        {
            Image p = Panel("Log", Root, Chrome);
            LogPanel = p.rectTransform;
            LogPanel.anchorMin = new Vector2(0f, 0f); LogPanel.anchorMax = new Vector2(1f, 0f);
            LogPanel.pivot = new Vector2(0.5f, 0f);
            LogPanel.offsetMin = new Vector2(0f, 0f); LogPanel.offsetMax = new Vector2(0f, 104f);

            LogContent = Blank("LogContent", LogPanel);
            LogContent.anchorMin = new Vector2(0f, 0f); LogContent.anchorMax = new Vector2(1f, 1f);
            LogContent.offsetMin = new Vector2(14f, 6f); LogContent.offsetMax = new Vector2(-130f, -6f);
            VerticalLayoutGroup v = LogContent.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.LowerLeft;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            v.spacing = 1f;

            LogExpandButton = Btn("LogExpand", LogPanel, "EXPAND", 108f, Root,
                                  new Vector2(1f, 0f), new Vector2(-70f, 78f), Panelly);
            LogExpandLabel = LogExpandButton.GetComponentInChildren<Text>();
        }

        private void BuildActionBar()
        {
            Image p = Panel("ActionBar", Root, Panelly);
            ActionBar = p.rectTransform;
            ActionBar.sizeDelta = new Vector2(268f, 86f);
            ActionBar.anchorMin = ActionBar.anchorMax = new Vector2(0f, 0f);
            ActionBar.pivot = new Vector2(0.5f, 0f);

            ActionTitle = Label("Who", ActionBar, "", 16, Dim, TextAnchor.UpperCenter, 0f, 260f);
            RectTransform t = ActionTitle.rectTransform;
            t.anchorMin = new Vector2(0f, 1f); t.anchorMax = new Vector2(1f, 1f);
            t.pivot = new Vector2(0.5f, 1f);
            t.offsetMin = new Vector2(6f, -26f); t.offsetMax = new Vector2(-6f, -4f);

            float x = 8f;
            BasicButton = Btn("Basic", ActionBar, "BASIC", 124f, ref x, Live);
            ActiveButton = Btn("Active", ActionBar, "ACTIVE", 124f, ref x, Live);
            foreach (Button b in new[] { BasicButton, ActiveButton })
            {
                RectTransform br = b.GetComponent<RectTransform>();
                br.anchorMin = new Vector2(0f, 0f); br.anchorMax = new Vector2(0f, 0f);
                br.pivot = new Vector2(0f, 0f);
                br.sizeDelta = new Vector2(124f, 44f);
            }
            SetPos(BasicButton, 8f, 8f);
            SetPos(ActiveButton, 138f, 8f);
            ActionBar.gameObject.SetActive(false);
        }

        private void BuildToast()
        {
            Image p = Panel("Toast", Root, new Color(0.45f, 0.13f, 0.09f, 0.95f));
            RectTransform r = p.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.sizeDelta = new Vector2(940f, 46f);
            r.anchoredPosition = new Vector2(0f, -70f);
            Toast = Label("ToastText", r, "", 18, Ink, TextAnchor.MiddleCenter, 0f, 0f);
            Stretch(Toast.rectTransform);
            ToastGroup = p.gameObject.AddComponent<CanvasGroup>();
            ToastGroup.alpha = 0f;
            ToastGroup.blocksRaycasts = false;
        }

        // ------------------------------------------------------------------ drawers

        private void BuildSetupDrawer()
        {
            Image p = Panel("SetupDrawer", Root, Panelly);
            SetupDrawer = p.rectTransform;
            SetupDrawer.anchorMin = new Vector2(0f, 0f); SetupDrawer.anchorMax = new Vector2(0f, 1f);
            SetupDrawer.pivot = new Vector2(0f, 0.5f);
            SetupDrawer.sizeDelta = new Vector2(430f, 0f);
            SetupDrawer.offsetMin = new Vector2(-430f, 104f);
            SetupDrawer.offsetMax = new Vector2(0f, -60f);

            Label("SetupTitle", SetupDrawer, "SETUP", 20, Ink, TextAnchor.UpperLeft, 14f, 200f)
                .rectTransform.anchoredPosition = new Vector2(14f, -12f);
            SetupClose = Btn("SetupClose", SetupDrawer, "CLOSE", 92f, Root,
                             new Vector2(1f, 1f), new Vector2(-56f, -22f), Chrome);

            BoardRows = Blank("BoardRows", SetupDrawer);
            BoardRows.anchorMin = new Vector2(0f, 1f); BoardRows.anchorMax = new Vector2(1f, 1f);
            BoardRows.pivot = new Vector2(0.5f, 1f);
            BoardRows.offsetMin = new Vector2(10f, -404f); BoardRows.offsetMax = new Vector2(-10f, -44f);
            VerticalLayoutGroup bv = BoardRows.gameObject.AddComponent<VerticalLayoutGroup>();
            bv.spacing = 2f; bv.childForceExpandHeight = false; bv.childForceExpandWidth = true;

            float bx = 10f;
            SaveBoardButton = Btn("SaveBoard", SetupDrawer, "SAVE", 98f, ref bx, Chrome);
            LoadBoardButton = Btn("LoadBoard", SetupDrawer, "LOAD", 98f, ref bx, Chrome);
            ClearBoardButton = Btn("ClearBoard", SetupDrawer, "CLEAR", 98f, ref bx, Chrome);
            ApplyBoardButton = Btn("ApplyBoard", SetupDrawer, "RESTART", 110f, ref bx, Live);
            foreach (Button b in new[] { SaveBoardButton, LoadBoardButton, ClearBoardButton, ApplyBoardButton })
                Pin(b, new Vector2(0f, 1f), new Vector2(0f, 1f));
            SetPos(SaveBoardButton, 10f, -412f);
            SetPos(LoadBoardButton, 116f, -412f);
            SetPos(ClearBoardButton, 222f, -412f);
            SetPos(ApplyBoardButton, 328f, -412f);

            RosterSearch = Field("RosterSearch", SetupDrawer, "search", 300f, ref bx);
            Pin(RosterSearch, new Vector2(0f, 1f), new Vector2(0f, 1f));
            SetPos(RosterSearch, 10f, -466f);
            RosterSearch.GetComponent<RectTransform>().sizeDelta = new Vector2(410f, 40f);

            RosterContent = Scroll("Roster", SetupDrawer, new Vector4(10f, 10f, -10f, -512f));
            SetupDrawer.gameObject.SetActive(false);
        }

        private void BuildRightDrawer()
        {
            Image p = Panel("RightDrawer", Root, Panelly);
            RightDrawer = p.rectTransform;
            RightDrawer.anchorMin = new Vector2(1f, 0f); RightDrawer.anchorMax = new Vector2(1f, 1f);
            RightDrawer.pivot = new Vector2(1f, 0.5f);
            RightDrawer.sizeDelta = new Vector2(450f, 0f);
            RightDrawer.offsetMin = new Vector2(-450f, 104f);
            RightDrawer.offsetMax = new Vector2(0f, -60f);

            float tx = 10f;
            UnitTab = Btn("UnitTab", RightDrawer, "UNIT", 130f, ref tx, Live);
            AnimTab = Btn("AnimTab", RightDrawer, "ANIMATION", 160f, ref tx, Chrome);
            RightClose = Btn("RightClose", RightDrawer, "CLOSE", 92f, ref tx, Chrome);
            foreach (Button b in new[] { UnitTab, AnimTab, RightClose }) Pin(b, new Vector2(0f, 1f), new Vector2(0f, 1f));
            SetPos(UnitTab, 10f, -10f);
            SetPos(AnimTab, 148f, -10f);
            SetPos(RightClose, 316f, -10f);

            UnitPage = Blank("UnitPage", RightDrawer);
            UnitPage.anchorMin = new Vector2(0f, 0f); UnitPage.anchorMax = new Vector2(1f, 1f);
            UnitPage.offsetMin = new Vector2(10f, 10f); UnitPage.offsetMax = new Vector2(-10f, -58f);

            UnitTitle = Label("UnitTitle", UnitPage, "no unit selected", 20, Ink, TextAnchor.UpperLeft, 0f, 0f);
            Top(UnitTitle.rectTransform, 0f, 30f);
            UnitStats = Label("UnitStats", UnitPage, "", 16, Dim, TextAnchor.UpperLeft, 0f, 0f);
            Top(UnitStats.rectTransform, 34f, 190f);
            UnitStatuses = Label("UnitStatuses", UnitPage, "", 16, Ink, TextAnchor.UpperLeft, 0f, 0f);
            Top(UnitStatuses.rectTransform, 228f, 220f);

            Hp10Button = EditBtn("Hp10", UnitPage, "HP 10%", 0f, -466f);
            Hp50Button = EditBtn("Hp50", UnitPage, "HP 50%", 140f, -466f);
            HpFullButton = EditBtn("HpFull", UnitPage, "HP FULL", 280f, -466f);
            EnergyFullButton = EditBtn("EnFull", UnitPage, "ENERGY FULL", 0f, -516f);
            EnergyZeroButton = EditBtn("EnZero", UnitPage, "ENERGY 0", 140f, -516f);
            KillButton = EditBtn("Kill", UnitPage, "KILL", 280f, -516f);
            ReviveButton = EditBtn("Revive", UnitPage, "REVIVE", 0f, -566f);

            AnimPage = Blank("AnimPage", RightDrawer);
            AnimPage.anchorMin = new Vector2(0f, 0f); AnimPage.anchorMax = new Vector2(1f, 1f);
            AnimPage.offsetMin = new Vector2(10f, 10f); AnimPage.offsetMax = new Vector2(-10f, -58f);

            ClipRow = Blank("ClipRow", AnimPage);
            ClipRow.anchorMin = new Vector2(0f, 1f); ClipRow.anchorMax = new Vector2(1f, 1f);
            ClipRow.pivot = new Vector2(0.5f, 1f);
            ClipRow.offsetMin = new Vector2(0f, -184f); ClipRow.offsetMax = new Vector2(0f, 0f);
            GridLayoutGroup g = ClipRow.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(134f, 40f);
            g.spacing = new Vector2(6f, 6f);

            AnimPauseButton = EditBtn("AnimPause", AnimPage, "PAUSE", 0f, -196f);
            AnimStepButton = EditBtn("AnimStep", AnimPage, "STEP", 140f, -196f);
            AnimLoopButton = EditBtn("AnimLoop", AnimPage, "LOOP", 280f, -196f);
            SoloButton = EditBtn("Solo", AnimPage, "SOLO", 0f, -246f);
            DetailsButton = EditBtn("Details", AnimPage, "DETAILS", 140f, -246f);

            AnimSpeedLabel = Label("AnimSpeedLabel", AnimPage, "clip speed 1.00x", 16, Dim,
                                   TextAnchor.UpperLeft, 0f, 0f);
            Top(AnimSpeedLabel.rectTransform, 300f, 24f);
            AnimSpeed = MakeSlider("AnimSpeed", AnimPage, 0.05f, 2f, 1f);
            Top(AnimSpeed.GetComponent<RectTransform>(), 330f, 30f);

            DetailsPanel = Blank("DetailsPanel", AnimPage);
            DetailsPanel.anchorMin = new Vector2(0f, 0f); DetailsPanel.anchorMax = new Vector2(1f, 1f);
            DetailsPanel.offsetMin = new Vector2(0f, 0f); DetailsPanel.offsetMax = new Vector2(0f, -376f);
            ClipDetails = Label("ClipDetails", DetailsPanel, "", 15, Dim, TextAnchor.UpperLeft, 0f, 0f);
            Stretch(ClipDetails.rectTransform);
            DetailsPanel.gameObject.SetActive(false);

            AnimPage.gameObject.SetActive(false);
            RightDrawer.gameObject.SetActive(false);
        }

        private void BuildHelp()
        {
            Image p = Panel("Help", Root, new Color(0.05f, 0.05f, 0.08f, 0.97f));
            HelpPanel = p.rectTransform;
            HelpPanel.anchorMin = new Vector2(0.5f, 0.5f); HelpPanel.anchorMax = new Vector2(0.5f, 0.5f);
            HelpPanel.sizeDelta = new Vector2(1060f, 620f);
            HelpPanel.anchoredPosition = Vector2.zero;

            Text t = Label("HelpText", HelpPanel, HelpText, 17, Ink, TextAnchor.UpperLeft, 0f, 0f);
            RectTransform tr = t.rectTransform;
            tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 1f);
            tr.offsetMin = new Vector2(26f, 70f); tr.offsetMax = new Vector2(-26f, -22f);

            HelpClose = Btn("HelpClose", HelpPanel, "CLOSE", 140f, Root,
                            new Vector2(0.5f, 0f), new Vector2(0f, 20f), Live);
            HelpPanel.gameObject.SetActive(false);
        }

        public const string HelpText =
            "DEV ARENA\n\n"
            + "This is the real battle view driving the real engine. CombatSimulation.Run is Begin()\n"
            + "plus StepNatural() until the fight ends, so AUTO is the game, not an imitation of it.\n\n"
            + "MANUAL — tap any unit on either side, then BASIC or ACTIVE, then tap a target.\n"
            + "The action plays out in real time. If the engine redirects or refuses your pick, a\n"
            + "message says why.\n\n"
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
            + "ANCHORS — the ORANGE mark is where the engine puts the unit's feet. The RED line is\n"
            + "the bottom edge of the sprite frame itself. When they disagree, the clip's foot\n"
            + "fraction is wrong and the unit will float or sink in a real battle.";

        // ------------------------------------------------------------------ small widgets

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
                                 TextAnchor anchor, float x, float width)
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
            RectTransform r = t.rectTransform;
            r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 0.5f);
            r.sizeDelta = new Vector2(width, 0f);
            r.anchoredPosition = new Vector2(x, 0f);
            return t;
        }

        public static Button Btn(string name, Transform parent, string text, float width,
                                 ref float x, Color colour)
        {
            Button b = Btn(name, parent, text, width, parent, new Vector2(0f, 0.5f), new Vector2(x, 0f), colour);
            x += width + 4f;
            return b;
        }

        public static Button Btn(string name, Transform parent, string text, float width,
                                 Transform unusedRoot, Vector2 anchor, Vector2 pos, Color colour)
        {
            Image img = Panel(name, parent, colour);
            RectTransform r = img.rectTransform;
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = new Vector2(0f, 0.5f);
            r.sizeDelta = new Vector2(width, 44f);
            r.anchoredPosition = pos;
            Button b = img.gameObject.AddComponent<Button>();
            ColorBlock cb = b.colors;
            cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            b.colors = cb;
            Text t = Label(name + "Text", r, text, 17, Ink, TextAnchor.MiddleCenter, 0f, width);
            Stretch(t.rectTransform);
            return b;
        }

        private static Button EditBtn(string name, Transform parent, string text, float x, float y)
        {
            Image img = Panel(name, parent, Chrome);
            RectTransform r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.sizeDelta = new Vector2(134f, 42f);
            r.anchoredPosition = new Vector2(x, y);
            Button b = img.gameObject.AddComponent<Button>();
            Text t = Label(name + "Text", r, text, 16, Ink, TextAnchor.MiddleCenter, 0f, 134f);
            Stretch(t.rectTransform);
            return b;
        }

        public static InputField Field(string name, Transform parent, string value, float width, ref float x)
        {
            Image img = Panel(name, parent, new Color(0.03f, 0.03f, 0.05f, 1f));
            RectTransform r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot = new Vector2(0f, 0.5f);
            r.sizeDelta = new Vector2(width, 40f);
            r.anchoredPosition = new Vector2(x, 0f);
            x += width + 4f;

            Text t = Label(name + "Text", r, value, 17, Ink, TextAnchor.MiddleLeft, 8f, width - 16f);
            t.raycastTarget = true;
            RectTransform tr = t.rectTransform;
            tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 1f);
            tr.offsetMin = new Vector2(8f, 2f); tr.offsetMax = new Vector2(-8f, -2f);

            InputField f = img.gameObject.AddComponent<InputField>();
            f.textComponent = t;
            f.text = value;
            return f;
        }

        private static Slider MakeSlider(string name, Transform parent, float min, float max, float value)
        {
            RectTransform root = Blank(name, parent);
            root.anchorMin = new Vector2(0f, 1f); root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.sizeDelta = new Vector2(0f, 30f);

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

        /// <summary>A vertical scroller with a content column that grows with its children.</summary>
        public static RectTransform Scroll(string name, Transform parent, Vector4 padding)
        {
            RectTransform view = Blank(name, parent);
            view.anchorMin = new Vector2(0f, 0f); view.anchorMax = new Vector2(1f, 1f);
            view.offsetMin = new Vector2(padding.x, padding.y);
            view.offsetMax = new Vector2(padding.z, padding.w);
            view.gameObject.AddComponent<RectMask2D>();

            RectTransform content = Blank(name + "Content", view);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            VerticalLayoutGroup v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 2f;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            ContentSizeFitter f = content.gameObject.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.viewport = view;
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 26f;
            return content;
        }

        public static void Stretch(RectTransform r)
        {
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 1f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        private static void Top(RectTransform r, float y, float height)
        {
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(0f, -(y + height)); r.offsetMax = new Vector2(0f, -y);
        }

        private static void Pin(Component c, Vector2 anchorMin, Vector2 anchorMax)
        {
            RectTransform r = c.GetComponent<RectTransform>();
            r.anchorMin = anchorMin; r.anchorMax = anchorMax;
            r.pivot = new Vector2(0f, 1f);
        }

        private static void SetPos(Component c, float x, float y)
        {
            c.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
        }

        public static void SetLabel(Button b, string text)
        {
            Text t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        public static void SetColour(Button b, Color c)
        {
            Image i = b.GetComponent<Image>();
            if (i != null) i.color = c;
        }

        public static Color Accent { get { return Live; } }
        public static Color Quiet { get { return Chrome; } }
        public static Color Text0 { get { return Ink; } }
        public static Color Text1 { get { return Dim; } }
    }
}
#endif
