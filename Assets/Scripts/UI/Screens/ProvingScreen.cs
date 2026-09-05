using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Session;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// The Proving's home screen: the opponent currently on offer, the Brand bank with its regen
    /// countdown, standing, the Valor purse, and the one action that matters - fight.
    ///
    /// Follows RecitalScreen's card conventions rather than inventing a second visual language for
    /// the second attempt-gated mode. The two screens are siblings and should read as siblings.
    ///
    /// THE ONE RULE THIS SCREEN HAS TO KEEP. DESIGN-PVP section 7: standing is a personal-progress
    /// number, not a rank. Every opponent here is generated locally, so there is no population to
    /// be ranked against, and nothing on this screen may imply otherwise. That is why there is no
    /// leaderboard, no ladder of names, no "top %", no tier badge borrowed from a game that has
    /// real players - and why a ghost opponent says out loud that it is the player's own line.
    /// Presenting a locally-generated number as a competitive position would be a straightforward
    /// lie to the player, and it is the easiest lie in this whole system to tell by accident.
    /// </summary>
    public sealed class ProvingScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Proving; } }

        private Text _headerSubtitle;
        private Text _opponentName;
        private Text _opponentPower;
        private RectTransform _ghostChip;
        private Text _ghostChipText;
        private RectTransform _lineRow;
        private Text _standingValue;
        private Text _brandsValue;
        private Text _nextBrandValue;
        private Text _valorValue;
        private UIButton _fightButton;
        private Text _honestyNote;

        private float _tick;

        private const float CardWidth = 900f;

        protected override void Build()
        {
            ScreenChrome.Header(Root, "THE PROVING", Router, out _headerSubtitle);
            BuildOpponentCard();
            BuildHonestyNote();
        }

        private void BuildOpponentCard()
        {
            const float cardHeight = 520f;
            float top = ScreenChrome.HeaderTop + 108f + 16f;
            RectTransform card = UIBuilder.Node("Opponent", Root);
            UIBuilder.Box(card, 0f, -top, CardWidth, cardHeight, new Vector2(0.5f, 1f));

            Image background = card.gameObject.AddComponent<Image>();
            background.sprite = TextureLab.RoundedRect(24);
            background.type = Image.Type.Sliced;
            background.color = UITheme.WithAlpha(UITheme.PanelSunken, 0.95f);
            UIBuilder.Frame("Edge", card, UITheme.WithAlpha(UITheme.ChromeBrass, 0.5f), 24, 2);

            float inner = UITheme.Space24;

            _opponentName = UIBuilder.Label("Name", card, "", UITheme.FontTitle, UITheme.TextPrimary, TextAnchor.UpperLeft);
            UIBuilder.Box(_opponentName.rectTransform, 30f, -inner, 560f, 56f, new Vector2(0f, 1f));

            _ghostChip = UIBuilder.Chip("GhostChip", card, "", UITheme.CinderGlass, UITheme.FontTiny);
            UIBuilder.Box(_ghostChip, -30f, -inner, 260f, 42f, new Vector2(1f, 1f));
            _ghostChipText = _ghostChip.GetComponentInChildren<Text>();

            inner += 56f + UITheme.Space8;

            _opponentPower = UIBuilder.Label("Power", card, "", UITheme.FontSmall, UITheme.Gold, TextAnchor.UpperLeft);
            UIBuilder.Box(_opponentPower.rectTransform, 30f, -inner, 560f, 32f, new Vector2(0f, 1f));

            inner += 32f + UITheme.Space16;

            Image divider = UIBuilder.Divider("Divider", card, UITheme.BorderSoft);
            UIBuilder.Box(divider.rectTransform, 0f, -inner, CardWidth - 60f, 2f, new Vector2(0.5f, 1f));
            inner += UITheme.Space16;

            // The opposing line, as portraits. Widgets.Portrait is the same badge the Hub and the
            // campaign strip use, so an opponent's heroes read as heroes rather than as a list.
            _lineRow = UIBuilder.Node("Line", card);
            UIBuilder.Box(_lineRow, 0f, -inner, CardWidth - 60f, 132f, new Vector2(0.5f, 1f));
            UIBuilder.HStack(_lineRow, 16f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            inner += 132f + UITheme.Space16;

            RectTransform statList = UIBuilder.Node("Stats", card);
            UIBuilder.Box(statList, 0f, -inner, CardWidth - 60f, 132f, new Vector2(0.5f, 1f));
            UIBuilder.VStack(statList, 4f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);

            _standingValue = Widgets.StatRow("Standing", statList, "YOUR STANDING", "", UITheme.Gold, 40f);
            _brandsValue = Widgets.StatRow("Brands", statList, "BRANDS AVAILABLE", "", UITheme.Ember, 40f);
            _nextBrandValue = Widgets.StatRow("NextBrand", statList, "NEXT BRAND IN", "", UITheme.TextMuted, 40f);

            inner += 132f + UITheme.Space16;

            _fightButton = UIBuilder.GoldButton("Fight", card, "FIGHT", UITheme.FontHeading);
            UIBuilder.Box(_fightButton.Root, 0f, -inner, CardWidth - 60f, 92f, new Vector2(0.5f, 1f));
            _fightButton.OnClick(OnFight);
        }

        /// <summary>
        /// The Valor purse and the line that keeps this screen honest.
        ///
        /// Valor is shown here rather than in the persistent top bar deliberately: the top bar
        /// carries the three currencies every screen spends, and a fourth pill for a currency only
        /// this mode pays would crowd it on every other screen for no reason. Revisit if Valor ever
        /// gets a sink somewhere else.
        /// </summary>
        private void BuildHonestyNote()
        {
            float top = ScreenChrome.HeaderTop + 108f + 16f + 520f + UITheme.Space24;

            RectTransform purse = UIBuilder.Node("Purse", Root);
            UIBuilder.Box(purse, 0f, -top, CardWidth, 56f, new Vector2(0.5f, 1f));
            Image bg = purse.gameObject.AddComponent<Image>();
            bg.sprite = TextureLab.RoundedRect(16);
            bg.type = Image.Type.Sliced;
            bg.color = UITheme.WithAlpha(UITheme.PanelSunken, 0.9f);
            UIBuilder.Frame("Edge", purse, UITheme.WithAlpha(UITheme.Gold, 0.35f), 16, 2);
            _valorValue = UIBuilder.Label("Valor", purse, "", UITheme.FontSmall, UITheme.Gold, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(_valorValue.rectTransform, 8f);

            top += 56f + UITheme.Space12;

            _honestyNote = UIBuilder.WrappedLabel("Note", Root,
                "Opponents are reconstructed lines, not other players. Standing is your own record"
                + " and sets how hard the next line is - it is not a rank against anyone.",
                UITheme.FontTiny - 2, UITheme.TextDim, TextAnchor.UpperCenter);
            UIBuilder.Box(_honestyNote.rectTransform, 0f, -top, CardWidth, 56f, new Vector2(0.5f, 1f));
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
            BattleLoadout opponent = App.Session.CurrentProvingOpponent(now);
            int standing = App.Session.Profile.ProvingStanding;

            _headerSubtitle.text = "STANDING " + standing + "   -   BEST " + App.Session.Profile.ProvingHighestStanding;
            _opponentName.text = opponent != null ? opponent.Label.ToUpperInvariant() : "NO OPPONENT";
            _opponentPower.text = opponent != null ? "LINE POWER  " + opponent.PowerScore : "";

            bool ghost = opponent != null && opponent.IsGhost;
            _ghostChip.gameObject.SetActive(ghost);
            if (ghost && _ghostChipText != null) _ghostChipText.text = "YOUR OWN LINE";

            _standingValue.text = standing.ToString();

            ProvingBrandsStatus brands = App.Session.PeekProvingBrands(now);
            _brandsValue.text = brands.Banked + " / " + brands.Max;
            _nextBrandValue.text = brands.SecondsToNext > 0 ? FormatDuration(brands.SecondsToNext) : "FULL";

            _valorValue.text = "VALOR  " + App.Session.Profile.Valor;

            _fightButton.Interactable = brands.Banked > 0 && opponent != null && opponent.Units.Count > 0;
            _fightButton.SetText(brands.Banked > 0 ? "FIGHT  -  1 BRAND" : "OUT OF BRANDS");

            RefreshLine(opponent);
        }

        private void RefreshLine(BattleLoadout opponent)
        {
            for (int i = _lineRow.childCount - 1; i >= 0; i--) Destroy(_lineRow.GetChild(i).gameObject);
            if (opponent == null) return;

            for (int i = 0; i < opponent.Units.Count; i++)
            {
                LoadoutUnit entry = opponent.Units[i];
                HeroDef def = App.Session.Content.GetHero(entry.DefId);
                if (def == null) continue;

                RectTransform cell = UIBuilder.Node("Unit" + i, _lineRow);
                UIBuilder.Sized(cell, 132f, 132f);

                RectTransform portrait = Widgets.Portrait("Portrait", cell, def, entry.Stars, 96f, true);
                UIBuilder.Box(portrait, 0f, 0f, 96f, 96f, new Vector2(0.5f, 1f));

                Text level = UIBuilder.Label("Level", cell, "LV " + entry.Level, UITheme.FontTiny - 4,
                                             UITheme.TextMuted, TextAnchor.UpperCenter);
                UIBuilder.Box(level.rectTransform, 0f, -100f, 132f, 24f, new Vector2(0.5f, 1f));
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
        /// Checks affordability only - does NOT spend. BattleScreen.OnShow is the single place a
        /// Brand is actually deducted, for both this button and a Rematch from the result screen,
        /// so there is exactly one spend per attempt.
        /// </summary>
        private void OnFight()
        {
            long now = TimeUtil.NowUnix();
            if (App.Session.PeekProvingBrands(now).Banked <= 0)
            {
                App.Notify("Out of Brands. Wait for one to regenerate.");
                Refresh();
                return;
            }

            BattleLoadout opponent = App.Session.CurrentProvingOpponent(now);
            if (opponent == null || opponent.Units.Count == 0)
            {
                App.Notify("No opponent could be assembled.");
                return;
            }

            App.Audio.Tap();
            Router.Battle.SetProvingOpponent(opponent);
            Router.Show(ScreenId.Battle);
        }
    }
}
