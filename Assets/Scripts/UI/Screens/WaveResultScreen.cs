using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Session;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// One screen per resolved wave: a stage is up to 12 of these, not 144 battle results.
    ///
    /// It answers two things, outcome largest: WHAT DID I JUST WIN, and AM I STILL WINNING. The
    /// outcome title is the biggest thing on the page; the left panel is what was won (the wave
    /// track, or the stage-clear spoils); the right panel is the wave's twelve battles, each cell
    /// filled to the share of the line's HP that fight left, with the margin beside it. On a loss
    /// one line below names the battle and the Debrief cause. There is no prose about how any of
    /// it works: labels and numbers only.
    ///
    /// PRESENTATION ONLY. GameSession.ResolveWave does the fighting, the recording and the
    /// paying; this reads the WaveResolution it returns and never computes an outcome, a reward
    /// or a margin of its own.
    ///
    /// The wave is resolved AFTER a bare frame has been shown, not inside the tap: a wave runs up
    /// to twelve simulations back to back, and doing it in the click handler would freeze the
    /// screen the player tapped on with no sign anything had happened.
    ///
    /// LAYOUT is centre-anchored and sized to fit a 1920 x 966 canvas, which is what the canvas
    /// scaler (match 0.5) gives a 20:9 phone, so it does not clip there or at 16:9.
    /// </summary>
    public sealed class WaveResultScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.WaveResult; } }
        public override bool HidesTopBar { get { return true; } }

        private static readonly Vector2 TopCentre = new Vector2(0.5f, 1f);
        private static readonly Vector2 BottomCentre = new Vector2(0.5f, 0f);
        private static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);

        private const float PanelTop = 212f;
        private const float PanelHeight = 520f;
        private const float PanelWidth = 860f;
        private const float PanelX = 440f;
        private const float Inner = 28f;
        private const float HeadingX = 36f;
        private const float HeadingY = -20f;

        private const float CellWidth = 58f;
        private const float CellGap = 8f;
        private const float CellHeight = 200f;

        private RectTransform _content;
        private Text _stageLine;
        private Text _title;

        private RectTransform _leftPanel;
        private Text _leftHeading;
        private Text _leftFigure;
        private RectTransform _pipHost;
        private RectTransform _rewardHost;
        private RectTransform _rewardList;

        private RectTransform _rightPanel;
        private RectTransform _cellHost;
        private Text _marginFigure;
        private Text _marginLabel;

        private RectTransform _lossBox;
        private Text _lossLine;

        private UIButton _nextWave;
        private UIButton _retry;
        private UIButton _continue;
        private UIButton _adjust;
        private UIButton _campaign;

        private StageDef _pending;
        private StageDef _stage;
        private WaveResolution _result;

        /// <summary>True once the result is on screen. The capture tool waits on this rather than
        /// on a fixed delay, because how long a wave takes to resolve depends on the line.</summary>
        public bool Painted { get; private set; }

        /// <summary>The last wave this screen resolved, or null before the first. Read only.</summary>
        public WaveResolution Result { get { return _result; } }

#if UNITY_EDITOR
        /// <summary>
        /// EDITOR ONLY: a seed the capture tool hands the next resolve, spent on use. Without it
        /// the photographed wave is a fresh draw on the clock and need not be the one the capture
        /// tool's probe found, which is how a capture filed under one outcome showed another. A
        /// player build does not contain it.
        /// </summary>
        public static int CaptureSeed;
#endif

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);

            _content = UIBuilder.Node("Content", Root);
            UIBuilder.FillParent(_content, 0f);

            _stageLine = EmberforgeUI.Label("StageLine", _content, "", UITheme.FontSmall, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
            UIBuilder.Box(_stageLine.rectTransform, 0f, -24f, 1700f, 34f, TopCentre);
            UIBuilder.Outlined(_stageLine, Emberforge.Outline, Emberforge.StrokeThin);

            // THE LARGEST THING ON SCREEN, by a wide margin: 112 against the kit's biggest named
            // size of 62. Best-fit so "STAGE 12-12 CLEARED" shrinks rather than runs off a narrow
            // canvas, and never below a size that still dwarfs every other element.
            _title = EmberforgeUI.Title("Outcome", _content, "", 112, Emberforge.TextGold, TextAnchor.MiddleCenter);
            UIBuilder.Box(_title.rectTransform, 0f, -62f, 1700f, 134f, TopCentre);
            UIBuilder.BestFit(_title, 64, 112);
            UIBuilder.Outlined(_title, Emberforge.Outline, 4f);

            BuildLeft();
            BuildRight();

            _lossBox = UIBuilder.Node("LossLine", _content);
            UIBuilder.Box(_lossBox, 0f, -(PanelTop + PanelHeight + 14f), PanelWidth * 2f + 20f, 56f, TopCentre);
            EmberforgeUI.SunkenOn(_lossBox);
            _lossLine = EmberforgeUI.Body("Text", _lossBox, "", UITheme.FontSmall, Emberforge.TextBody, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(_lossLine.rectTransform, 20f, 4f, 20f, 4f);
            UIBuilder.BestFit(_lossLine, 14, UITheme.FontSmall);

            // All five are built once and shown or hidden per result, so a listener is wired once
            // (UIButton.OnClick appends) and the names stay stable for the capture tool.
            _nextWave = EmberforgeUI.Button("NextWave", _content, "NEXT WAVE", UITheme.FontHeading, ForgeButtonKind.Primary);
            _nextWave.OnClick(OnResolveAgain);
            _retry = EmberforgeUI.Button("Retry", _content, "RETRY", UITheme.FontHeading, ForgeButtonKind.Primary);
            _retry.OnClick(OnResolveAgain);
            _continue = EmberforgeUI.Button("Continue", _content, "CONTINUE", UITheme.FontHeading, ForgeButtonKind.Primary);
            _continue.OnClick(OnContinue);
            _adjust = EmberforgeUI.Button("AdjustLine", _content, "ADJUST LINE", UITheme.FontBody, ForgeButtonKind.Secondary);
            _adjust.OnClick(OnAdjust);
            _campaign = EmberforgeUI.Button("Campaign", _content, "CAMPAIGN", UITheme.FontBody, ForgeButtonKind.Secondary);
            _campaign.OnClick(OnCampaign);
        }

        private void BuildLeft()
        {
            _leftPanel = UIBuilder.Node("Won", _content);
            UIBuilder.Box(_leftPanel, -PanelX, -PanelTop, PanelWidth, PanelHeight, TopCentre);
            EmberforgeUI.PanelOn(_leftPanel, true);

            // (36, -20) is where the kit's other framed panels put their heading
            // (GuildDungeonResultScreen, GuildFloorScreen): clear of the corner ornament, which
            // the panel inset alone collided with.
            _leftHeading = EmberforgeUI.Heading("Heading", _leftPanel, "WAVES", HeadingX, HeadingY);

            _leftFigure = EmberforgeUI.Title("Figure", _leftPanel, "", 96, Emberforge.TextGold, TextAnchor.MiddleCenter);
            UIBuilder.Box(_leftFigure.rectTransform, 0f, -80f, PanelWidth - Inner * 2f, 120f, TopCentre);
            UIBuilder.Outlined(_leftFigure, Emberforge.Outline, Emberforge.StrokeThick);

            _pipHost = UIBuilder.Node("Pips", _leftPanel);
            UIBuilder.Box(_pipHost, 0f, -250f, PanelWidth - Inner * 2f, 64f, TopCentre);

            _rewardHost = UIBuilder.Node("Spoils", _leftPanel);
            UIBuilder.Box(_rewardHost, 0f, -66f, PanelWidth - Inner * 2f, PanelHeight - 90f, TopCentre);
            ScrollRect rewardScroll;
            _rewardList = UIBuilder.ScrollList("List", _rewardHost, out rewardScroll, 2f, new RectOffset(0, 0, 0, 0));
            UIBuilder.FillParent(rewardScroll.GetComponent<RectTransform>(), 0f);
        }

        private void BuildRight()
        {
            _rightPanel = UIBuilder.Node("Battles", _content);
            UIBuilder.Box(_rightPanel, PanelX, -PanelTop, PanelWidth, PanelHeight, TopCentre);
            EmberforgeUI.PanelOn(_rightPanel, true);

            EmberforgeUI.Heading("Heading", _rightPanel, "BATTLES", HeadingX, HeadingY);

            float stripWidth = 12f * CellWidth + 11f * CellGap;
            _cellHost = UIBuilder.Node("Cells", _rightPanel);
            UIBuilder.Box(_cellHost, 0f, -72f, stripWidth + 8f, CellHeight + 36f, TopCentre);

            _marginFigure = EmberforgeUI.Title("Margin", _rightPanel, "", 96, Emberforge.TextGold, TextAnchor.MiddleCenter);
            UIBuilder.Box(_marginFigure.rectTransform, 0f, -326f, PanelWidth - Inner * 2f, 112f, TopCentre);
            UIBuilder.Outlined(_marginFigure, Emberforge.Outline, Emberforge.StrokeThick);

            _marginLabel = EmberforgeUI.Label("MarginLabel", _rightPanel, "", UITheme.FontSmall, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
            UIBuilder.Box(_marginLabel.rectTransform, 0f, -440f, PanelWidth - Inner * 2f, 34f, TopCentre);
            UIBuilder.Outlined(_marginLabel, Emberforge.Outline, Emberforge.StrokeThin);
        }

        // ================================================================== entry

        /// <summary>
        /// Resolves the stage's current wave and shows the result. The one way onto this screen:
        /// CampaignFlow.Launch calls it for a stage that is fought, and NEXT WAVE and RETRY call
        /// it again. Not remembered in the history, like the other screens that end a run, so
        /// every way off names its own destination.
        /// </summary>
        public void Begin(StageDef stage)
        {
            _pending = stage;
            Router.Show(ScreenId.WaveResult, false);
        }

        public override void OnShow()
        {
            StopAllCoroutines();
            Painted = false;
            _content.gameObject.SetActive(false);
            if (_pending == null)
            {
                // Reached with nothing to resolve: the way out and nothing else, not a blank page.
                _result = null;
                PaintRefused();
                return;
            }
            StageDef stage = _pending;
            _pending = null;
            StartCoroutine(ResolveRoutine(stage));
        }

        private IEnumerator ResolveRoutine(StageDef stage)
        {
            // The bare frame: the ground and nothing else, drawn before the blocking resolve.
            yield return null;

            _stage = stage;
            int seed = CampaignFlow.NewSeed(App, stage);
#if UNITY_EDITOR
            if (CaptureSeed != 0) { seed = CaptureSeed; CaptureSeed = 0; }
#endif
            _result = App.Session.ResolveWave(stage, seed, TimeUtil.NowUnix());
            App.RefreshHud();
            App.RequestSave();

            if (_result.Refused != WaveRefusal.None)
            {
                if (_result.Refused == WaveRefusal.NoTeam) App.Notify("Deploy at least one hero first.");
                else if (_result.Refused == WaveRefusal.StageLocked) App.Notify("Clear the stage before it to unlock this one.");
                PaintRefused();
                yield break;
            }

            if (_result.WaveCleared || _result.StageCleared) App.Audio.Victory();
            else App.Audio.Defeat();
            Paint();
        }

        // ================================================================== paint

        /// <summary>A refusal changes nothing and has nothing to show: the way back, and no more.</summary>
        private void PaintRefused()
        {
            _content.gameObject.SetActive(true);
            _stageLine.text = "";
            _title.text = "";
            _leftPanel.gameObject.SetActive(false);
            _rightPanel.gameObject.SetActive(false);
            _lossBox.gameObject.SetActive(false);
            HideButtons();
            Place(_campaign);
            Painted = true;
        }

        private void Paint()
        {
            _content.gameObject.SetActive(true);
            _leftPanel.gameObject.SetActive(true);
            _rightPanel.gameObject.SetActive(true);

            WaveResolution r = _result;
            bool lost = r.LostBattle > 0;

            _stageLine.text = "STAGE " + _stage.DisplayCode + "   " + _stage.Name.ToUpperInvariant()
                              + "   -   WAVE " + r.Wave + " / " + r.WaveCount;

            if (lost)
            {
                _title.text = "BATTLE " + r.LostBattle + " LOST";
                _title.color = UITheme.Danger;
            }
            else if (r.StageCleared)
            {
                _title.text = "STAGE " + _stage.DisplayCode + " CLEARED";
                _title.color = Emberforge.GoldLit;
            }
            else
            {
                _title.text = "WAVE " + r.Wave + " CLEARED";
                _title.color = Emberforge.TextGold;
            }

            PaintLeft(r, lost);
            PaintBattles(r, lost);
            PaintLossLine(r, lost);
            PaintButtons(r, lost);
            Painted = true;
        }

        private void PaintLeft(WaveResolution r, bool lost)
        {
            Clear(_pipHost);
            Clear(_rewardList);

            if (r.StageCleared)
            {
                _leftHeading.text = "WON";
                _leftFigure.gameObject.SetActive(false);
                _pipHost.gameObject.SetActive(false);
                _rewardHost.gameObject.SetActive(true);
                if (r.Rewards != null) BattleScreen.AddClearRewardRows(_rewardList, r.Rewards);
                return;
            }

            // A won mid-stage wave pays nothing and a lost one won nothing new, so both show the
            // wave track: where the player stands in the stage.
            _leftHeading.text = "WAVES";
            _rewardHost.gameObject.SetActive(false);
            _leftFigure.gameObject.SetActive(true);
            _pipHost.gameObject.SetActive(true);

            int cleared = lost ? r.Wave - 1 : r.Wave;
            _leftFigure.text = cleared + " / " + r.WaveCount;
            _leftFigure.color = lost ? UITheme.TextMuted : Emberforge.TextGold;

            const float gap = 8f;
            float width = Mathf.Min(120f, (PanelWidth - Inner * 2f - gap * (r.WaveCount - 1)) / r.WaveCount);
            float total = r.WaveCount * width + (r.WaveCount - 1) * gap;
            float x0 = -total * 0.5f + width * 0.5f;
            for (int i = 1; i <= r.WaveCount; i++)
            {
                bool current = i == r.Wave;
                bool done = i < r.Wave || (current && !lost);

                Color fill = done ? Emberforge.GoldLow : Emberforge.SunkenMid;
                if (current && !lost) fill = Emberforge.GoldMid;
                RectTransform pip = UIBuilder.Panel("Pip" + i, _pipHost, fill, 8).rectTransform;
                UIBuilder.Box(pip, x0 + (i - 1) * (width + gap), 0f, width, 56f, Middle);
                if (current) UIBuilder.Frame("Mark", pip, lost ? UITheme.Danger : Emberforge.GoldLit, 8, 4);

                Text n = EmberforgeUI.Label("N", pip, i.ToString(), UITheme.FontSmall,
                                            done ? Emberforge.TextOnGold : Emberforge.TextQuiet, TextAnchor.MiddleCenter);
                UIBuilder.FillParent(n.rectTransform, 0f);
            }
        }

        private void PaintBattles(WaveResolution r, bool lost)
        {
            Clear(_cellHost);

            float stripWidth = r.BattleCount * CellWidth + (r.BattleCount - 1) * CellGap;
            float x0 = -stripWidth * 0.5f + CellWidth * 0.5f;
            for (int b = 1; b <= r.BattleCount; b++)
            {
                WaveBattleOutcome o = OutcomeOf(r, b);
                float cx = x0 + (b - 1) * (CellWidth + CellGap);

                RectTransform cell = UIBuilder.Panel("Cell" + b, _cellHost, Emberforge.SunkenTop, 6).rectTransform;
                UIBuilder.Box(cell, cx, 0f, CellWidth, CellHeight, TopCentre);

                Color numberColor = Emberforge.TextQuiet;
                if (b < r.FirstBattle)
                {
                    // Won by an earlier resolve: a muted solid, since the HP it kept is not on this record.
                    SetFill(cell, 1f, UITheme.TextDim);
                }
                else if (o != null && o.Victory)
                {
                    SetFill(cell, o.PlayerHpRatio, UITheme.Success);
                    numberColor = Emberforge.TextBody;
                }
                else if (o != null)
                {
                    SetFill(cell, 1f, UITheme.Danger);
                    numberColor = UITheme.Danger;
                }

                Text n = EmberforgeUI.Label("N" + b, _cellHost, b.ToString(), UITheme.FontTiny, numberColor, TextAnchor.MiddleCenter);
                UIBuilder.Box(n.rectTransform, cx, -(CellHeight + 4f), CellWidth, 28f, TopCentre);
            }

            if (lost)
            {
                WaveBattleOutcome last = OutcomeOf(r, r.LostBattle);
                _marginFigure.text = last != null ? Mathf.RoundToInt(last.EnemyHpRatio * 100f) + "%" : "";
                _marginFigure.color = UITheme.Danger;
                _marginLabel.text = "ENEMY HP LEFT";
            }
            else
            {
                _marginFigure.text = Mathf.RoundToInt(Mathf.Max(0f, r.Margin) * 100f) + "%";
                _marginFigure.color = UITheme.Success;
                _marginLabel.text = "HP KEPT";
            }
        }

        /// <summary>A cell filled from the bottom to a share of its height: a child rect anchored
        /// to the bottom, rather than a vertically filled image, so it needs nothing beyond the
        /// plain rect and image the rest of the kit already uses.</summary>
        private static void SetFill(RectTransform cell, float ratio, Color color)
        {
            float h = Mathf.Max(4f, Mathf.Clamp01(ratio) * CellHeight);
            Image fill = UIBuilder.Panel("Fill", cell, color, 6);
            UIBuilder.Box(fill.rectTransform, 0f, 0f, CellWidth, h, BottomCentre);
        }

        private static WaveBattleOutcome OutcomeOf(WaveResolution r, int battle)
        {
            for (int i = 0; i < r.Battles.Count; i++)
                if (r.Battles[i].Battle == battle) return r.Battles[i];
            return null;
        }

        private void PaintLossLine(WaveResolution r, bool lost)
        {
            _lossBox.gameObject.SetActive(lost);
            if (!lost || r.LostDebrief == null) return;

            // The same shape as BattleScreen's debrief line: the tinted cause tag, then the
            // sentence the Debrief already wrote. The tag is left out when there is none.
            string tag = BattleScreen.CauseTag(r.LostDebrief.Clause);
            string cause = string.IsNullOrEmpty(tag)
                ? ""
                : "<color=#" + ColorUtility.ToHtmlStringRGB(BattleScreen.CauseTint(r.LostDebrief.Clause)) + ">"
                  + tag + "</color>   ";
            _lossLine.text = "BATTLE " + r.LostBattle + "   " + cause + r.LostDebrief.Text;
        }

        // ================================================================== buttons

        private void PaintButtons(WaveResolution r, bool lost)
        {
            HideButtons();
            if (lost) Place(_retry, _adjust, _campaign);
            else if (r.StageCleared)
            {
                if (App.Session.FirstUnclearedStage != null) Place(_continue, _campaign);
                else Place(_campaign);
            }
            else Place(_nextWave, _campaign);
        }

        private void HideButtons()
        {
            _nextWave.Root.gameObject.SetActive(false);
            _retry.Root.gameObject.SetActive(false);
            _continue.Root.gameObject.SetActive(false);
            _adjust.Root.gameObject.SetActive(false);
            _campaign.Root.gameObject.SetActive(false);
        }

        /// <summary>Lays the given buttons out as one centred row along the bottom, the first
        /// being the wide gold one when there is more than one.</summary>
        private static void Place(params UIButton[] row)
        {
            const float gap = 24f;
            float[] widths = new float[row.Length];
            float total = gap * (row.Length - 1);
            for (int i = 0; i < row.Length; i++)
            {
                widths[i] = (i == 0 && row.Length > 1) ? 520f : 340f;
                total += widths[i];
            }

            float x = -total * 0.5f;
            for (int i = 0; i < row.Length; i++)
            {
                row[i].Root.gameObject.SetActive(true);
                UIBuilder.Box(row[i].Root, x + widths[i] * 0.5f, 26f, widths[i], 96f, BottomCentre);
                x += widths[i] + gap;
            }
        }

        private static void Clear(Transform host)
        {
            for (int i = host.childCount - 1; i >= 0; i--) Destroy(host.GetChild(i).gameObject);
        }

        // ================================================================== actions

        /// <summary>NEXT WAVE and RETRY are the same call: the resolve resumes at the cursor, which
        /// is the next wave after a clear and the lost battle after a loss.</summary>
        private void OnResolveAgain()
        {
            App.Audio.Tap();
            if (_stage != null) Begin(_stage);
        }

        private void OnContinue()
        {
            App.Audio.Tap();
            StageDef next = App.Session.NextStage;
            if (next == null) { Router.Show(ScreenId.Campaign, false); return; }
            CampaignFlow.Launch(App, next);
        }

        private void OnAdjust()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Formation, false);
        }

        private void OnCampaign()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Campaign, false);
        }
    }
}
