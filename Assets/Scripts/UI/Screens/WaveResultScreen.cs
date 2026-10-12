using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Battle;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;
using Emberfall.UI.Battle;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// What one resolved wave came to, drawn the way Docs/ui-specs/WAVE-RESULTS.md locks it.
    ///
    /// THREE SHAPES, one screen. A won mid-stage wave is an OVERLAY: the stage's battlefield with
    /// the line standing in idle behind a band along the bottom that carries the wave track, what
    /// the wave paid and the way on. A lost wave is the same band in a red tone. A stage clear is
    /// the one full screen: the battlefield darkened under VICTORY and a row of reward tiles.
    ///
    /// PRESENTATION ONLY. GameSession.ResolveWave does the fighting, the recording and the
    /// paying; this reads the WaveResolution it returns and never computes an outcome or a
    /// reward. The squad on the field is the record's own snapshot of the line that fought.
    ///
    /// The wave is resolved AFTER a bare frame has been shown, not inside the tap: a wave runs up
    /// to twelve simulations back to back, and doing it in the click handler would freeze the
    /// screen the player tapped on with no sign anything had happened.
    ///
    /// AUTO is a device preference in PlayerPrefs, like the battle screen's own auto and the
    /// summon's Fast setting: it changes nothing about the account, so the save does not carry it.
    /// </summary>
    public sealed class WaveResultScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.WaveResult; } }
        public override bool HidesTopBar { get { return true; } }

        private static readonly Vector2 TopCentre = new Vector2(0.5f, 1f);
        private static readonly Vector2 BottomCentre = new Vector2(0.5f, 0f);
        private static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 LeftMiddle = new Vector2(0f, 0.5f);

        private const string AutoPrefKey = "Emberfall.WaveResult.Auto";
        private const float AutoSeconds = 2f;

        // The band: about 330 of 1080, everything centred inside it, top to bottom.
        private const float BandHeight = 330f;
        private const float BandRule = 2f;
        private const float StageLineY = -14f;
        private const float TitleY = -42f;
        private const float TitleHeight = 76f;
        private const float RowY = -122f;
        private const float RowHeight = 44f;
        private const float LossLineY = -170f;
        private const float ButtonBottom = 24f;
        private const float ButtonHeight = 92f;
        private const float ButtonGap = 24f;

        private const float PipWidth = 28f;
        private const float PipCurrentWidth = 56f;
        private const float PipHeight = 14f;
        private const float PipGap = 6f;
        private const float ChipHeight = 40f;
        private const float ChipIcon = 26f;

        private const float TileSize = 180f;
        private const float TileGap = 20f;

        private Image _ground;
        private Image _dim;
        private BattleStage _world;
        private StageDef _fieldStage;
        private bool _fieldBuilt;

        private RectTransform _band;
        private Image _bandFill;
        private Text _stageLine;
        private Text _title;
        private RectTransform _rowHost;
        private Text _lossLine;

        private UIButton _nextWave;
        private RectTransform _countdownPlate;
        private Text _countdown;
        private UIButton _retry;
        private UIButton _adjust;
        private UIButton _watch;
        private RectTransform _autoToggle;
        private Image _autoCheck;

        private RectTransform _cleared;
        private RectTransform _column;
        private UIButton _continue;
        private UIButton _watchCleared;

        private StageDef _pending;
        private StageDef _stage;
        private WaveResolution _result;
        private Coroutine _autoRoutine;

        /// <summary>Set when WATCH sends the player to the battle screen, so coming back paints the
        /// wave they left instead of resolving another or showing a blank page.</summary>
        private bool _returning;

        /// <summary>True once the result is on screen. The capture tool waits on this rather than
        /// on a fixed delay, because how long a wave takes to resolve depends on the line.</summary>
        public bool Painted { get; private set; }

        /// <summary>The last wave this screen resolved, or null before the first. Read only.</summary>
        public WaveResolution Result { get { return _result; } }

        /// <summary>The player's AUTO setting: NEXT WAVE fires by itself AutoSeconds after a won
        /// mid-stage wave is shown. A device preference, never the save.</summary>
        public static bool AutoAdvance
        {
            get { return PlayerPrefs.GetInt(AutoPrefKey, 0) == 1; }
            set
            {
                PlayerPrefs.SetInt(AutoPrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// EDITOR ONLY: a seed the capture tool hands the next resolve, spent on use. Without it
        /// the photographed wave is a fresh draw on the clock and need not be the one the capture
        /// tool's probe found, which is how a capture filed under one outcome showed another. A
        /// player build does not contain it.
        /// </summary>
        public static int CaptureSeed;

        /// <summary>EDITOR ONLY: the clock the capture tool hands the next resolve, spent on use,
        /// so a stage clear's armor roll is the one its probe found.</summary>
        public static long CaptureNow;

        /// <summary>EDITOR ONLY: holds the AUTO countdown on its first digit, so a capture of it
        /// is not raced by NEXT WAVE firing in a slow editor.</summary>
        public static bool CaptureHoldCountdown;

        /// <summary>Presses WATCH from outside, so a capture can photograph the replay. Drives the
        /// same private handler the button drives, as SkipToEndForCapture does on the battle screen.</summary>
        public void WatchForCapture() { OnWatch(); }
#endif

        protected override void Build()
        {
            // Shown only when there is no battlefield to stand on: a refusal, or nothing to resolve.
            _ground = EmberforgeUI.Ground(Root);

            _dim = UIBuilder.Img("Dim", Root, TextureLab.White, new Color(0f, 0f, 0f, 0.74f));
            UIBuilder.FillParent(_dim.rectTransform, 0f);
            _dim.raycastTarget = false;

            BuildBand();
            BuildCleared();

            // On Root, built last so it draws over everything, and there from the first frame.
            ScreenChrome.HomeDisc(Root, Router);
        }

        private void BuildBand()
        {
            _band = UIBuilder.Node("Band", Root);
            _band.anchorMin = new Vector2(0f, 0f);
            _band.anchorMax = new Vector2(1f, 0f);
            _band.pivot = BottomCentre;
            _band.sizeDelta = new Vector2(0f, BandHeight);
            _band.anchoredPosition = Vector2.zero;

            // The band's own face catches any tap that is not on one of its controls, which is
            // what cancels the AUTO countdown for this wave.
            _bandFill = _band.gameObject.AddComponent<Image>();
            _bandFill.sprite = TextureLab.White;
            Button tap = _band.gameObject.AddComponent<Button>();
            tap.targetGraphic = _bandFill;
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(CancelCountdown);

            Image rule = UIBuilder.Img("Rule", _band, TextureLab.White, Emberforge.GoldLow);
            rule.rectTransform.anchorMin = new Vector2(0f, 1f);
            rule.rectTransform.anchorMax = new Vector2(1f, 1f);
            rule.rectTransform.pivot = TopCentre;
            rule.rectTransform.sizeDelta = new Vector2(0f, BandRule);
            rule.rectTransform.anchoredPosition = Vector2.zero;
            rule.raycastTarget = false;

            _stageLine = EmberforgeUI.Label("StageLine", _band, "", UITheme.FontSmall, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
            UIBuilder.Box(_stageLine.rectTransform, 0f, StageLineY, 1600f, 30f, TopCentre);
            UIBuilder.Outlined(_stageLine, Emberforge.Outline, Emberforge.StrokeThin);

            _title = EmberforgeUI.Title("Title", _band, "", 64, Emberforge.TextGold, TextAnchor.MiddleCenter);
            UIBuilder.Box(_title.rectTransform, 0f, TitleY, 1600f, TitleHeight, TopCentre);
            UIBuilder.BestFit(_title, 44, 64);
            UIBuilder.Outlined(_title, Emberforge.Outline, Emberforge.StrokeThick);

            _rowHost = UIBuilder.Node("Row", _band);
            UIBuilder.Box(_rowHost, 0f, RowY, 1600f, RowHeight, TopCentre);

            _lossLine = EmberforgeUI.Label("LossLine", _band, "", UITheme.FontBody, Emberforge.TextBody, TextAnchor.MiddleCenter);
            UIBuilder.Box(_lossLine.rectTransform, 0f, LossLineY, 1600f, 32f, TopCentre);
            UIBuilder.Outlined(_lossLine, Emberforge.Outline, Emberforge.StrokeThin);

            // Built once and shown or hidden per result, so a listener is wired once
            // (UIButton.OnClick appends) and the names stay stable for the capture tool.
            _nextWave = EmberforgeUI.Button("NextWave", _band, "NEXT WAVE", UITheme.FontHeading, ForgeButtonKind.Primary);
            _nextWave.OnClick(OnResolveAgain);
            _countdownPlate = UIBuilder.Panel("Countdown", _nextWave.Root, UITheme.WithAlpha(Emberforge.Outline, 0.55f), 10).rectTransform;
            UIBuilder.Box(_countdownPlate, -18f, Emberforge.ButtonLip * 0.5f, 44f, 44f, new Vector2(1f, 0.5f));
            _countdownPlate.GetComponent<Image>().raycastTarget = false;
            _countdown = EmberforgeUI.Title("Digit", _countdownPlate, "", UITheme.FontBody, Emberforge.TextOnColor, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(_countdown.rectTransform, 0f);
            _countdown.raycastTarget = false;

            _retry = EmberforgeUI.Button("Retry", _band, "RETRY", UITheme.FontHeading, ForgeButtonKind.Primary);
            _retry.OnClick(OnResolveAgain);
            _adjust = EmberforgeUI.Button("AdjustLine", _band, "ADJUST LINE", UITheme.FontBody, ForgeButtonKind.Secondary);
            _adjust.OnClick(OnAdjust);
            _watch = EmberforgeUI.Button("Watch", _band, "WATCH", UITheme.FontBody, ForgeButtonKind.Secondary);
            _watch.OnClick(OnWatch);

            BuildAutoToggle();
        }

        /// <summary>The AUTO checkbox: a sunken box with the kit's gold edge and a check in it,
        /// and the word beside it. The whole of it is the hit target.</summary>
        private void BuildAutoToggle()
        {
            _autoToggle = UIBuilder.Node("Auto", _band);
            Image hit = _autoToggle.gameObject.AddComponent<Image>();
            hit.sprite = TextureLab.White;
            hit.color = Color.clear;
            Button press = _autoToggle.gameObject.AddComponent<Button>();
            press.targetGraphic = hit;
            press.transition = Selectable.Transition.None;
            press.onClick.AddListener(OnAutoToggle);

            Image box = EmberforgeUI.Sunken("Box", _autoToggle);
            UIBuilder.Box(box.rectTransform, 8f, 0f, 46f, 46f, LeftMiddle);
            box.raycastTarget = false;
            Image edge = UIBuilder.Frame("Edge", box.rectTransform, Emberforge.GoldLow, 8, 3);
            edge.raycastTarget = false;
            _autoCheck = UIBuilder.Img("Check", box.rectTransform, TextureLab.Check(64), Emberforge.GoldLit);
            UIBuilder.FillParent(_autoCheck.rectTransform, 7f);
            _autoCheck.raycastTarget = false;

            Text label = EmberforgeUI.Label("Label", _autoToggle, "AUTO", UITheme.FontBody, Emberforge.TextGold, TextAnchor.MiddleLeft);
            UIBuilder.Box(label.rectTransform, 66f, 0f, 110f, 46f, LeftMiddle);
            UIBuilder.Outlined(label, Emberforge.Outline, Emberforge.StrokeThin);
            label.raycastTarget = false;
        }

        private void BuildCleared()
        {
            _cleared = UIBuilder.Node("Cleared", Root);
            UIBuilder.FillParent(_cleared, 0f);

            _column = UIBuilder.Node("Column", _cleared);
            UIBuilder.Box(_column, 0f, 0f, 1400f, 600f, Middle);

            _continue = EmberforgeUI.Button("Continue", _column, "CONTINUE", UITheme.FontHeading, ForgeButtonKind.Primary);
            _continue.OnClick(OnContinue);
            _watchCleared = EmberforgeUI.Button("Watch", _column, "WATCH", UITheme.FontBody, ForgeButtonKind.Secondary);
            _watchCleared.OnClick(OnWatch);
        }

        /// <summary>One screen back is the campaign map, which this screen was never remembered
        /// from. Resolves and pays nothing.</summary>
        public override bool TryStepBack()
        {
            Router.Show(ScreenId.Campaign, false);
            return true;
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
            _autoRoutine = null;
            Painted = false;
            HideAll();
            if (_pending == null && _returning && _result != null && _result.Refused == WaveRefusal.None && _stage != null)
            {
                // Back from watching a replay: the wave is already resolved and already paid, so
                // draw it again as it stood. Nothing is resolved, granted or saved on this path,
                // and the countdown does not start again.
                _returning = false;
                ShowField(_stage);
                Paint(false);
                return;
            }
            _returning = false;
            if (_pending == null)
            {
                // Reached with nothing to resolve: the way out and nothing else, not a blank page.
                _result = null;
                PaintRefused();
                return;
            }
            StageDef stage = _pending;
            _pending = null;
            ShowField(stage);
            StartCoroutine(ResolveRoutine(stage));
        }

        public override void OnHide()
        {
            StopAllCoroutines();
            _autoRoutine = null;
            if (_world != null)
            {
                _world.Teardown();
                Destroy(_world.gameObject);
                _world = null;
            }
            App.SetBackgroundVisible(true);
        }

        private IEnumerator ResolveRoutine(StageDef stage)
        {
            // The bare frame: the battlefield and nothing else, drawn before the blocking resolve.
            yield return null;

            _stage = stage;
            int seed = CampaignFlow.NewSeed(App, stage);
            long now = TimeUtil.NowUnix();
#if UNITY_EDITOR
            if (CaptureSeed != 0) { seed = CaptureSeed; CaptureSeed = 0; }
            if (CaptureNow != 0) { now = CaptureNow; CaptureNow = 0; }
#endif
            _result = App.Session.ResolveWave(stage, seed, now);
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
            Paint(true);
        }

        // ================================================================== the field

        /// <summary>The stage's battlefield behind the screen, through the battle's own rig. Built
        /// before the resolve so the bare frame is the place, not a blank.</summary>
        private void ShowField(StageDef stage)
        {
            _fieldBuilt = false;
            if (_world == null)
            {
                GameObject host = new GameObject("WaveResultWorld");
                _world = host.AddComponent<BattleStage>();
                _world.Initialise();
            }
            _fieldStage = stage;
            _world.Camera.rect = new Rect(0f, 0f, 1f, 1f);
            FrameField(true);
            App.SetBackgroundVisible(false);
            _ground.gameObject.SetActive(false);
        }

        /// <summary>
        /// Gives the field the screen above the band, or the whole screen. Above the band the line
        /// stands clear of it rather than behind it: the camera's viewport stops at the band's top
        /// edge and the battle's own framing fits the squad into what is left. The backdrop is
        /// built again whenever the viewport changes, because it is sized off the camera's aspect.
        /// </summary>
        private void FrameField(bool aboveBand)
        {
            if (_world == null || _fieldStage == null) return;
            float height = Root.rect.height;
            float band = aboveBand && height > 0f ? Mathf.Clamp01(BandHeight / height) : 0f;
            Rect want = new Rect(0f, band, 1f, 1f - band);
            if (_world.Camera.rect == want && _fieldBuilt) return;
            _world.Camera.rect = want;
            _world.BuildWorld(_fieldStage.Environment);
            _fieldBuilt = true;
        }

        /// <summary>Stands the line that fought on its side of the field, idle. No enemies: the
        /// record's squad is the player's side only.</summary>
        private void StandSquad(WaveResolution r)
        {
            if (_world == null) return;
            BattleResult field = new BattleResult();
            field.Units.AddRange(r.Squad);
            _world.Populate(field);
        }

        // ================================================================== paint

        private void HideAll()
        {
            _dim.gameObject.SetActive(false);
            _band.gameObject.SetActive(false);
            _cleared.gameObject.SetActive(false);
        }

        /// <summary>A refusal changes nothing and has nothing to show: HOME, and no more.</summary>
        private void PaintRefused()
        {
            HideAll();
            if (_world != null) _world.ClearActors();
            if (_world == null) _ground.gameObject.SetActive(true);
            Painted = true;
        }

        /// <summary>Draws the result. startAuto is false when coming back from a replay, which
        /// must not start a countdown the player has already sat through.</summary>
        private void Paint(bool startAuto)
        {
            WaveResolution r = _result;
            if (r.StageCleared)
            {
                FrameField(false);
                if (_world != null) _world.ClearActors();
                PaintCleared(r);
            }
            else
            {
                FrameField(true);
                StandSquad(r);
                PaintBand(r, startAuto);
            }
            Painted = true;
        }

        private string StageName()
        {
            return "STAGE " + _stage.DisplayCode + " · " + _stage.Name.ToUpperInvariant();
        }

        private void PaintBand(WaveResolution r, bool startAuto)
        {
            bool lost = r.LostBattle > 0;
            _band.gameObject.SetActive(true);
            _bandFill.color = lost ? Color.Lerp(Emberforge.SunkenTop, UITheme.Danger, 0.16f)
                                   : UITheme.WithAlpha(Emberforge.SunkenTop, 0.94f);
            if (lost) _bandFill.color = UITheme.WithAlpha(_bandFill.color, 0.94f);

            _stageLine.text = StageName();
            _title.text = "WAVE " + r.Wave + (lost ? " LOST" : " CLEARED");
            _title.color = lost ? UITheme.Danger : Emberforge.TextGold;

            PaintRow(r, lost);

            _lossLine.gameObject.SetActive(lost);
            // A loss in wave 1 keeps no wave, so it says nothing about what is kept.
            if (lost)
                _lossLine.text = "Fell in battle " + r.LostBattle + " of " + r.BattleCount + "."
                                 + (r.Wave > 1 ? " Progress up to wave " + (r.Wave - 1) + " is kept." : "");

            HideBandButtons();
            bool watch = r.Replay != null;
            if (lost)
            {
                if (watch) Place(_retry.Root, 420f, _adjust.Root, 320f, _watch.Root, 280f);
                else Place(_retry.Root, 420f, _adjust.Root, 320f);
                return;
            }

            bool auto = AutoAdvance;
            _autoCheck.enabled = auto;
            if (watch) Place(_nextWave.Root, 420f, _watch.Root, 280f, _autoToggle, 190f);
            else Place(_nextWave.Root, 420f, _autoToggle, 190f);

            _countdownPlate.gameObject.SetActive(false);
            if (auto && startAuto) _autoRoutine = StartCoroutine(AutoRoutine());
        }

        /// <summary>
        /// The one row: the wave track, the count, and a chip for each currency this wave paid.
        /// Laid out by hand from measured widths so the whole row stays centred whatever it holds.
        /// </summary>
        private void PaintRow(WaveResolution r, bool lost)
        {
            Clear(_rowHost);
            int n = Mathf.Max(1, r.WaveCount);
            int cleared = lost ? r.Wave - 1 : r.Wave;

            Text count = EmberforgeUI.Label("Count", _rowHost, cleared + " / " + n, UITheme.FontBody,
                                            lost ? Emberforge.TextBody : Emberforge.TextGold, TextAnchor.MiddleLeft);
            UIBuilder.Outlined(count, Emberforge.Outline, Emberforge.StrokeThin);
            float countWidth = count.preferredWidth + 4f;

            List<RectTransform> chips = new List<RectTransform>();
            List<float> chipWidths = new List<float>();
            if (!lost && r.WaveGold > 0)
                AddChip(chips, chipWidths, "Gold", TextureLab.CoinIcon(64), UITheme.Gold, "+" + r.WaveGold);
            if (!lost && r.WaveAccountXp > 0)
                AddChip(chips, chipWidths, "Xp", TextureLab.Star(64), Emberforge.GoldLit, "+" + r.WaveAccountXp + " XP");

            float track = (n - 1) * PipWidth + PipCurrentWidth + (n - 1) * PipGap;
            float total = track + Emberforge.Space16 + countWidth;
            for (int i = 0; i < chips.Count; i++) total += (i == 0 ? Emberforge.Space24 : Emberforge.Space12) + chipWidths[i];

            float x = -total * 0.5f;
            for (int i = 1; i <= n; i++)
            {
                bool current = i == r.Wave;
                bool done = i < r.Wave;
                float w = current ? PipCurrentWidth : PipWidth;
                Color fill = done ? Emberforge.GoldLow
                           : current ? (lost ? UITheme.Danger : Emberforge.GoldLit)
                           : Emberforge.SunkenTop;
                Image pip = UIBuilder.Panel("Pip" + i, _rowHost, fill, 6);
                UIBuilder.Box(pip.rectTransform, x + w * 0.5f, 0f, w, current ? PipHeight + 6f : PipHeight, Middle);
                if (!done && !current)
                {
                    Image edge = UIBuilder.Frame("Edge", pip.rectTransform, UITheme.WithAlpha(Emberforge.GoldDeep, 0.8f), 6, 2);
                    edge.raycastTarget = false;
                }
                pip.raycastTarget = false;
                x += w + PipGap;
            }
            x += Emberforge.Space16 - PipGap;

            UIBuilder.Box(count.rectTransform, x + countWidth * 0.5f, 0f, countWidth, RowHeight, Middle);
            x += countWidth;

            for (int i = 0; i < chips.Count; i++)
            {
                x += i == 0 ? Emberforge.Space24 : Emberforge.Space12;
                UIBuilder.Box(chips[i], x + chipWidths[i] * 0.5f, 0f, chipWidths[i], ChipHeight, Middle);
                x += chipWidths[i];
            }
        }

        /// <summary>A reward chip: the kit's chip with the currency's icon at its left end.</summary>
        private void AddChip(List<RectTransform> chips, List<float> widths, string name, Sprite icon, Color tint, string text)
        {
            RectTransform chip = EmberforgeUI.Chip(name, _rowHost, text, tint, UITheme.FontSmall);
            Image mark = UIBuilder.Img("Icon", chip, icon, tint);
            UIBuilder.Box(mark.rectTransform, Emberforge.Space8, 0f, ChipIcon, ChipIcon, LeftMiddle);
            mark.raycastTarget = false;

            Text label = chip.Find("Text").GetComponent<Text>();
            float left = Emberforge.Space8 + ChipIcon + Emberforge.Space8;
            UIBuilder.FillParent(label.rectTransform, left, 2f, Emberforge.Space12, 2f);
            label.alignment = TextAnchor.MiddleLeft;

            chips.Add(chip);
            widths.Add(left + label.preferredWidth + Emberforge.Space12 + 4f);
        }

        private void PaintCleared(WaveResolution r)
        {
            _dim.gameObject.SetActive(true);
            _cleared.gameObject.SetActive(true);
            for (int i = _column.childCount - 1; i >= 0; i--)
            {
                Transform child = _column.GetChild(i);
                if (child == _continue.Root || child == _watchCleared.Root) continue;
                Destroy(child.gameObject);
            }

            BattleRewards rewards = r.Rewards;
            const float width = 1400f;
            float y = 0f;

            y = PlaceRule("RuleTop", y, 760f);
            y -= Emberforge.Space12;
            Text victory = EmberforgeUI.Title("Victory", _column, "VICTORY", 104, Emberforge.TextGold, TextAnchor.MiddleCenter);
            UIBuilder.Box(victory.rectTransform, 0f, y, width, 112f, TopCentre);
            UIBuilder.Outlined(victory, Emberforge.Outline, 4f);
            y -= 112f;

            Text line = EmberforgeUI.Label("StageLine", _column, StageName() + " CLEARED", UITheme.FontBody,
                                           Emberforge.TextBody, TextAnchor.MiddleCenter);
            UIBuilder.Box(line.rectTransform, 0f, y, width, 40f, TopCentre);
            UIBuilder.Outlined(line, Emberforge.Outline, Emberforge.StrokeThin);
            y -= 40f + Emberforge.Space12;
            y = PlaceRule("RuleBottom", y, 760f);

            if (rewards != null && rewards.FirstClear)
            {
                y -= Emberforge.Space16;
                RectTransform tag = EmberforgeUI.Chip("FirstClear", _column, "FIRST CLEAR", Emberforge.GoldMid, UITheme.FontSmall);
                float tagWidth = tag.Find("Text").GetComponent<Text>().preferredWidth + Emberforge.Space24 * 2f;
                UIBuilder.Box(tag, 0f, y, tagWidth, 40f, TopCentre);
                y -= 40f;
            }

            y -= Emberforge.Space24;
            y = PlaceTiles(rewards, y);

            StageDef unlocked = rewards != null && rewards.UnlockedNextStage ? App.Session.FirstUnclearedStage : null;
            if (unlocked != null)
            {
                y -= Emberforge.Space16;
                RectTransform row = UIBuilder.Node("Unlocked", _column);
                Text text = EmberforgeUI.Label("Text", row, "STAGE " + unlocked.DisplayCode + " UNLOCKED", UITheme.FontBody,
                                               Emberforge.TextGold, TextAnchor.MiddleLeft);
                UIBuilder.Outlined(text, Emberforge.Outline, Emberforge.StrokeThin);
                float textWidth = text.preferredWidth + 4f;
                const float icon = 30f;
                float rowWidth = icon + Emberforge.Space12 + textWidth;
                UIBuilder.Box(row, 0f, y, rowWidth, 40f, TopCentre);
                Image padlock = UIBuilder.Img("Icon", row, TextureLab.Padlock(64), Emberforge.GoldMid);
                UIBuilder.Box(padlock.rectTransform, 0f, 0f, icon, icon, LeftMiddle);
                UIBuilder.Box(text.rectTransform, icon + Emberforge.Space12, 0f, textWidth, 40f, LeftMiddle);
                y -= 40f;
            }

            y -= Emberforge.Space32;
            bool watch = r.Replay != null;
            _watchCleared.Root.gameObject.SetActive(watch);
            float buttons = watch ? 420f + ButtonGap + 280f : 420f;
            UIBuilder.Box(_continue.Root, -buttons * 0.5f + 210f, y, 420f, 96f, TopCentre);
            if (watch) UIBuilder.Box(_watchCleared.Root, buttons * 0.5f - 140f, y, 280f, 96f, TopCentre);
            y -= 96f;

            // The column is as tall as what it holds, centred on the screen.
            _column.sizeDelta = new Vector2(width, -y);
        }

        private float PlaceRule(string name, float y, float width)
        {
            Image rule = EmberforgeUI.Rule(name, _column);
            UIBuilder.Box(rule.rectTransform, 0f, y, width, Emberforge.RuleHeight, TopCentre);
            return y - Emberforge.RuleHeight;
        }

        /// <summary>One square tile per reward, in the order the spec names them, each only when
        /// the clear actually paid it.</summary>
        private float PlaceTiles(BattleRewards rewards, float y)
        {
            if (rewards == null) return y;
            List<RectTransform> tiles = new List<RectTransform>();
            if (rewards.Gold > 0)
                tiles.Add(CurrencyTile("Gold", TextureLab.CoinIcon(64), UITheme.Gold, "+" + rewards.Gold, "GOLD"));
            if (rewards.Shards > 0)
                tiles.Add(CurrencyTile("Shards", TextureLab.CrystalIcon(64), UITheme.Shard, "+" + rewards.Shards, "EMBER SHARDS"));
            if (rewards.AccountXp > 0)
                tiles.Add(CurrencyTile("Xp", TextureLab.Star(64), Emberforge.GoldLit, "+" + rewards.AccountXp, "WAYFARER XP"));
            if (rewards.ArmorDrop != null) tiles.Add(ArmorTile(rewards.ArmorDrop));
            if (tiles.Count == 0) return y;

            float total = tiles.Count * TileSize + (tiles.Count - 1) * TileGap;
            for (int i = 0; i < tiles.Count; i++)
                UIBuilder.Box(tiles[i], -total * 0.5f + TileSize * 0.5f + i * (TileSize + TileGap), y, TileSize, TileSize, TopCentre);
            return y - TileSize;
        }

        private RectTransform Tile(string name)
        {
            RectTransform tile = UIBuilder.Node(name, _column);
            EmberforgeUI.PlainPanelOn(tile);
            return tile;
        }

        private RectTransform CurrencyTile(string name, Sprite icon, Color tint, string amount, string label)
        {
            RectTransform tile = Tile(name);
            Image mark = UIBuilder.Img("Icon", tile, icon, tint);
            UIBuilder.Box(mark.rectTransform, 0f, -22f, 64f, 64f, TopCentre);
            TileText(tile, amount, label, tint);
            return tile;
        }

        private RectTransform ArmorTile(ArmorPiece piece)
        {
            RectTransform tile = Tile("Armor");
            RectTransform card = ItemCard.ForArmor("Icon", tile, 76f, piece);
            UIBuilder.Box(card, 0f, -16f, 76f, 76f, TopCentre);
            Color temper = UITheme.ArmorTemperColor(piece.Temper);
            TileText(tile, piece.Name, ArmorInfo.TemperLabel(piece.Temper).ToUpperInvariant(), temper);
            return tile;
        }

        /// <summary>The amount, then the small caps label, under a tile's icon.</summary>
        private static void TileText(RectTransform tile, string amount, string label, Color tint)
        {
            Text value = EmberforgeUI.Title("Amount", tile, amount, UITheme.FontHeading, Emberforge.TextGold, TextAnchor.MiddleCenter);
            UIBuilder.Box(value.rectTransform, 0f, -96f, TileSize - Emberforge.Space16, 40f, TopCentre);
            UIBuilder.BestFit(value, 16, UITheme.FontHeading);
            UIBuilder.Outlined(value, Emberforge.Outline, Emberforge.StrokeThin);

            Text caption = EmberforgeUI.Label("Label", tile, label, UITheme.FontTiny, tint, TextAnchor.MiddleCenter);
            UIBuilder.Box(caption.rectTransform, 0f, -140f, TileSize - Emberforge.Space16, 28f, TopCentre);
            UIBuilder.BestFit(caption, 12, UITheme.FontTiny);
            UIBuilder.Outlined(caption, Emberforge.Outline, Emberforge.StrokeThin);
        }

        // ================================================================== buttons

        private void HideBandButtons()
        {
            _nextWave.Root.gameObject.SetActive(false);
            _retry.Root.gameObject.SetActive(false);
            _adjust.Root.gameObject.SetActive(false);
            _watch.Root.gameObject.SetActive(false);
            _autoToggle.gameObject.SetActive(false);
        }

        /// <summary>Lays the given controls out as one centred row along the band's bottom, as
        /// (control, width) pairs in order.</summary>
        private static void Place(params object[] row)
        {
            float total = ButtonGap * (row.Length / 2 - 1);
            for (int i = 1; i < row.Length; i += 2) total += (float)row[i];

            float x = -total * 0.5f;
            for (int i = 0; i < row.Length; i += 2)
            {
                RectTransform rt = (RectTransform)row[i];
                float w = (float)row[i + 1];
                rt.gameObject.SetActive(true);
                UIBuilder.Box(rt, x + w * 0.5f, ButtonBottom, w, ButtonHeight, BottomCentre);
                x += w + ButtonGap;
            }
        }

        private static void Clear(Transform host)
        {
            for (int i = host.childCount - 1; i >= 0; i--) Destroy(host.GetChild(i).gameObject);
        }

        // ================================================================== auto

        /// <summary>The countdown: a digit inside NEXT WAVE for each second left, then NEXT WAVE
        /// itself, through the same handler a tap uses.</summary>
        private IEnumerator AutoRoutine()
        {
            _countdownPlate.gameObject.SetActive(true);
            float left = AutoSeconds;
            while (left > 0f)
            {
                _countdown.text = Mathf.CeilToInt(left).ToString();
#if UNITY_EDITOR
                if (CaptureHoldCountdown) { yield return null; continue; }
#endif
                yield return null;
                left -= Time.unscaledDeltaTime;
            }
            _countdownPlate.gameObject.SetActive(false);
            _autoRoutine = null;
            OnResolveAgain();
        }

        /// <summary>Any tap on the band stops the countdown for this wave. AUTO stays as it is set.</summary>
        private void CancelCountdown()
        {
            if (_autoRoutine != null) StopCoroutine(_autoRoutine);
            _autoRoutine = null;
            _countdownPlate.gameObject.SetActive(false);
        }

        private void OnAutoToggle()
        {
            App.Audio.Tap();
            CancelCountdown();
            AutoAdvance = !AutoAdvance;
            _autoCheck.enabled = AutoAdvance;
        }

        // ================================================================== actions

        /// <summary>NEXT WAVE and RETRY are the same call: the resolve resumes at the cursor, which
        /// is the next wave after a clear and the lost battle after a loss.</summary>
        private void OnResolveAgain()
        {
            App.Audio.Tap();
            CancelCountdown();
            if (_stage != null) Begin(_stage);
        }

        private void OnContinue()
        {
            App.Audio.Tap();
            StageDef next = App.Session.NextStage;
            if (next == null) { Router.Show(ScreenId.Campaign, false); return; }
            CampaignFlow.Launch(App, next);
        }

        /// <summary>
        /// Plays the wave's hardest battle (the lost one, after a loss) on the battle screen.
        ///
        /// The fight is re-run by GameSession.ReplayBattle from the seed and the line the resolve
        /// kept, which is the same pure function the resolve ran, and handed to the battle screen
        /// as a finished result. The screen computes nothing, pays nothing and asks for no save;
        /// its one button comes back here and draws this wave again.
        /// </summary>
        private void OnWatch()
        {
            App.Audio.Tap();
            CancelCountdown();
            WaveReplay replay = _result != null ? _result.Replay : null;
            if (replay == null) return;

            BattleResult fight = App.Session.ReplayBattle(replay);

            GuildBattleRun run = new GuildBattleRun();
            run.Stage = replay.Stage;
            run.Result = fight;
            run.IsReplay = true;
            run.Home = true;
            // The battle HUD's two lines, as a live campaign fight draws them: the stage, then the
            // wave and battle ahead of the round, with REPLAY on the first line so it is never
            // mistaken for a fight being played for the first time.
            run.Label = replay.Stage.DisplayCode + BattleHudStyle.Separator + replay.Stage.Name.ToUpperInvariant()
                        + BattleHudStyle.Separator + "REPLAY";
            run.Progress = "WAVE " + replay.Wave + " / " + replay.Stage.WaveCount + BattleHudStyle.Separator
                           + "BATTLE " + replay.Battle + " / " + replay.Stage.BattleCount;
            run.Subtitle = "REPLAY   " + fight.Rounds + " ROUNDS   BANNER AT "
                           + Mathf.RoundToInt(fight.PlayerHpRatio * 100f) + "%";
            run.Rows.Add(new KeyValuePair<string, string>("REWARDS", "NONE"));
            run.PrimaryLabel = "BACK";
            run.OnPrimary = delegate
            {
                _returning = true;
                Router.Show(ScreenId.WaveResult, false);
            };

            _returning = true;
            App.Router.Battle.SetGuildRun(run);
            Router.Show(ScreenId.Battle, false);
        }

        private void OnAdjust()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Formation, false);
        }
    }
}
