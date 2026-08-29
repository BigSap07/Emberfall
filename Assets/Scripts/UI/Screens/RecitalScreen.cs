using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// The Deep Recital's home screen: current floor, the permanent highest-floor record, the
    /// Echo bank with its regen countdown, a preview of the current six-floor Aspect cycle, and
    /// the one action that matters - fight the current floor. Follows the same card conventions
    /// MainMenuScreen's idle cache card and RelicShopScreen's banner already established rather
    /// than inventing a new visual language.
    /// </summary>
    public sealed class RecitalScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Recital; } }

        private Text _headerSubtitle;
        private Text _floorHeading;
        private RectTransform _aspectChip;
        private Text _aspectChipText;
        private RectTransform _statList;
        private Text _highestValue;
        private Text _echoesValue;
        private Text _nextEchoValue;
        private UIButton _fightButton;
        private RectTransform _cycleRow;

        private float _tick;

        private const float CardWidth = 900f;

        protected override void Build()
        {
            ScreenChrome.Header(Root, "THE DEEP RECITAL", Router, out _headerSubtitle);
            BuildStatusCard();
            BuildCycleRow();
        }

        private void BuildStatusCard()
        {
            const float cardHeight = 420f;
            float top = ScreenChrome.HeaderTop + 108f + 16f;
            RectTransform card = UIBuilder.Node("Status", Root);
            UIBuilder.Box(card, 0f, -top, CardWidth, cardHeight, new Vector2(0.5f, 1f));

            Image background = card.gameObject.AddComponent<Image>();
            background.sprite = TextureLab.RoundedRect(24);
            background.type = Image.Type.Sliced;
            background.color = UITheme.WithAlpha(UITheme.PanelSunken, 0.95f);
            UIBuilder.Frame("Edge", card, UITheme.WithAlpha(UITheme.Mythic, 0.4f), 24, 2);

            Image glow = UIBuilder.Img("Glow", card, TextureLab.Glow(128, 2f), UITheme.WithAlpha(UITheme.Mythic, 0.08f));
            UIBuilder.FillParent(glow.rectTransform, 0f);

            float inner = UITheme.Space24;

            _floorHeading = UIBuilder.Label("Floor", card, "", UITheme.FontDisplay, UITheme.TextPrimary, TextAnchor.UpperLeft);
            UIBuilder.Box(_floorHeading.rectTransform, 30f, -inner, 500f, 74f, new Vector2(0f, 1f));

            _aspectChip = UIBuilder.Chip("AspectChip", card, "", UITheme.TextMuted, UITheme.FontSmall);
            UIBuilder.Box(_aspectChip, -30f, -inner, 260f, 46f, new Vector2(1f, 1f));
            _aspectChipText = _aspectChip.GetComponentInChildren<Text>();

            inner += 74f + UITheme.Space16;

            Image divider = UIBuilder.Divider("Divider", card, UITheme.BorderSoft);
            UIBuilder.Box(divider.rectTransform, 0f, -inner, CardWidth - 60f, 2f, new Vector2(0.5f, 1f));
            inner += UITheme.Space16;

            const float listHeight = 150f;
            _statList = UIBuilder.Node("Stats", card);
            UIBuilder.Box(_statList, 0f, -inner, CardWidth - 60f, listHeight, new Vector2(0.5f, 1f));
            UIBuilder.VStack(_statList, 6f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);

            _highestValue = Widgets.StatRow("Highest", _statList, "HIGHEST FLOOR REACHED", "", UITheme.Gold, 40f);
            _echoesValue = Widgets.StatRow("Echoes", _statList, "ECHOES AVAILABLE", "", UITheme.Shard, 40f);
            _nextEchoValue = Widgets.StatRow("NextEcho", _statList, "NEXT ECHO IN", "", UITheme.TextMuted, 40f);

            inner += listHeight + UITheme.Space24;

            _fightButton = UIBuilder.GoldButton("Fight", card, "FIGHT", UITheme.FontHeading);
            UIBuilder.Box(_fightButton.Root, 0f, -inner, CardWidth - 60f, 100f, new Vector2(0.5f, 1f));
            _fightButton.OnClick(OnFight);
        }

        private void BuildCycleRow()
        {
            float top = ScreenChrome.HeaderTop + 108f + 16f + 420f + UITheme.Space24;
            Text heading = UIBuilder.Label("CycleHeading", Root, "THIS CYCLE - EMBER, TIDE, VERDANT, GALE, UMBRA, LUMEN",
                                           UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperCenter);
            UIBuilder.Box(heading.rectTransform, 0f, -top, CardWidth, 28f, new Vector2(0.5f, 1f));
            top += 28f + UITheme.Space8;

            _cycleRow = UIBuilder.Node("CycleRow", Root);
            UIBuilder.Box(_cycleRow, 0f, -top, CardWidth, 64f, new Vector2(0.5f, 1f));
            UIBuilder.HStack(_cycleRow, 12f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
        }

        // ------------------------------------------------------------------ refresh

        public override void OnShow()
        {
            App.RefreshHud();
            _tick = 0f;
            Refresh();
        }

        private void Update()
        {
            if (Root == null || !Root.gameObject.activeInHierarchy) return;
            _tick += Time.unscaledDeltaTime;
            if (_tick < 1f) return;
            _tick = 0f;
            Refresh();
        }

        private void Refresh()
        {
            long now = TimeUtil.NowUnix();
            int floor = App.Session.Profile.RecitalCurrentFloor;
            BalanceConfig cfg = App.Session.Balance;
            Aspect aspect = RecitalGenerator.AspectForFloor(floor);
            int level = RecitalGenerator.LevelForFloor(cfg, floor);

            _headerSubtitle.text = "FLOOR " + floor + "  -  " + aspect.ToString().ToUpperInvariant() + " ECHOES  -  LEVEL " + level;
            _floorHeading.text = "FLOOR " + floor;

            if (_aspectChipText != null) _aspectChipText.text = aspect.ToString().ToUpperInvariant();
            Color aspectColor = UITheme.AspectColor(aspect);
            Image chipBg = _aspectChip.GetComponent<Image>();
            if (chipBg != null) chipBg.color = UITheme.WithAlpha(aspectColor, 0.22f);
            if (_aspectChipText != null) _aspectChipText.color = aspectColor;

            _highestValue.text = App.Session.Profile.RecitalHighestFloor > 0
                ? App.Session.Profile.RecitalHighestFloor.ToString() : "NONE YET";

            RecitalEchoesStatus echoes = App.Session.PeekRecitalEchoes(now);
            _echoesValue.text = echoes.Banked + " / " + echoes.Max;
            _nextEchoValue.text = echoes.SecondsToNext > 0 ? FormatDuration(echoes.SecondsToNext) : "FULL";

            _fightButton.Interactable = echoes.Banked > 0;
            _fightButton.SetText(echoes.Banked > 0 ? "FIGHT  -  1 ECHO" : "OUT OF ECHOES");

            RefreshCycleRow(floor);
        }

        private void RefreshCycleRow(int floor)
        {
            for (int i = _cycleRow.childCount - 1; i >= 0; i--) Destroy(_cycleRow.GetChild(i).gameObject);

            int cycleStart = floor - ((floor - 1) % RecitalGenerator.AspectCycleLength);
            for (int i = 0; i < RecitalGenerator.AspectCycleLength; i++)
            {
                int cycleFloor = cycleStart + i;
                Aspect cycleAspect = RecitalGenerator.AspectForFloor(cycleFloor);
                bool isCurrent = cycleFloor == floor;
                bool isPast = cycleFloor < floor;
                Color tint = UITheme.AspectColor(cycleAspect);

                RectTransform cell = UIBuilder.Node("Cycle" + cycleFloor, _cycleRow);
                UIBuilder.Flexible(cell, 1f, 0f);
                UIBuilder.Sized(cell, 0f, 64f);

                Image bg = cell.gameObject.AddComponent<Image>();
                bg.sprite = TextureLab.RoundedRect(14);
                bg.type = Image.Type.Sliced;
                bg.color = isCurrent ? UITheme.WithAlpha(tint, 0.32f) : UITheme.WithAlpha(tint, isPast ? 0.10f : 0.16f);
                UIBuilder.Frame("Edge", cell, UITheme.WithAlpha(tint, isCurrent ? 0.9f : 0.4f), 14, isCurrent ? 3 : 2);

                Text label = UIBuilder.Label("Label", cell, cycleAspect.ToString().Substring(0, 3).ToUpperInvariant(),
                                             UITheme.FontTiny, isPast ? UITheme.TextDim : tint, TextAnchor.MiddleCenter);
                UIBuilder.FillParent(label.rectTransform, 4f);
            }
        }

        private static string FormatDuration(long seconds)
        {
            if (seconds < 60) return seconds + "s";
            long minutes = seconds / 60;
            if (minutes < 60) return minutes + "m " + (seconds % 60) + "s";
            long hours = minutes / 60;
            return hours + "h " + (minutes % 60) + "m";
        }

        // ------------------------------------------------------------------ actions

        /// <summary>
        /// Checks affordability only - does NOT spend. BattleScreen.OnShow is the single place an
        /// Echo is actually deducted, for both this button's first entry and a Rematch from the
        /// result screen, so there is exactly one spend per attempt rather than a second deduction
        /// here on top of BattleScreen's own gate.
        /// </summary>
        private void OnFight()
        {
            if (App.Session.PeekRecitalEchoes(TimeUtil.NowUnix()).Banked <= 0)
            {
                App.Notify("Out of Echoes. Wait for one to regenerate.");
                Refresh();
                return;
            }
            App.Audio.Tap();
            int floor = App.Session.Profile.RecitalCurrentFloor;
            Router.Battle.SetRecitalFloor(floor);
            Router.Show(ScreenId.Battle);
        }
    }
}
