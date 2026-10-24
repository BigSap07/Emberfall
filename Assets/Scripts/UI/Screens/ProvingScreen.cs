using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;
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
    ///
    /// THE BOARDS DOOR (R28) DOES NOT CHANGE THAT. BOARDS opens the leaderboards screen, where the server ranks real
    /// players' own saves; this page still draws no rank, no list of names and no "top %" of its own.
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

        /// <summary>A2 of the rewarded ads (Docs/ui-specs/R28-SCREENS.md): the block beside the opponent card at the height
        /// of its Brands rows, in the free column right of the card. 430 wide fits that column at 1920, so it draws
        /// stacked (RewardedAdBlock.NarrowBelow). Nothing is built while AdsSwitch is off.</summary>
        private const float AdWidth = 430f;
        private const float AdBesideBrands = 250f;
        private RewardedAdBlock _ad;

        /// <summary>The ad block, for the capture tool and the checks.</summary>
        public RewardedAdBlock AdBlock { get { return _ad; } }

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            ScreenChrome.ForgedHeader(Root, "THE PROVING", Router, out _headerSubtitle);
            BuildOpponentCard();
            BuildHonestyNote();
            BuildBoardsDoor();
            BuildAd();
        }

        /// <summary>
        /// THE LEADERBOARDS' DOOR (R28): BOARDS, a secondary button in the top right, under the header and clear of the
        /// opponent card. It opens the leaderboards with the Proving's board selected. The board is the server's own
        /// ranking of real players' saves; the standing shown on this page stays what it always was.
        /// </summary>
        private void BuildBoardsDoor()
        {
            UIButton boards = EmberforgeUI.Button("Boards", Root, "BOARDS", UITheme.FontSmall, ForgeButtonKind.Secondary);
            UIBuilder.Box(boards.Root, -UITheme.ScreenPadding, -(ScreenChrome.HeaderTop + 108f + 16f), 260f, HubStyle.TouchMin,
                          new Vector2(1f, 1f));
            boards.OnClick(delegate
            {
                App.Audio.Tap();
                Router.Leaderboards.OpenBoard(Emberfall.Core.Cloud.LeaderboardBoard.Proving);
            });
        }

        private void BuildAd()
        {
            float top = ScreenChrome.HeaderTop + 108f + 16f;
            RectTransform host = UIBuilder.Node("AdBlock", Root);
            UIBuilder.Box(host, CardWidth * 0.5f + UITheme.Space24 + AdWidth * 0.5f, -(top + AdBesideBrands),
                          AdWidth, RewardedAdBlock.HeightFor(AdWidth), new Vector2(0.5f, 1f));
            _ad = new RewardedAdBlock(App, AdPlacementKind.Brand, host, AdWidth, null);
            host.gameObject.SetActive(false);
        }

        private void BuildOpponentCard()
        {
            // Measured from the blocks inside it rather than guessed: 414 of content, then the
            // FIGHT button's own 92 and the panel's padding under it. It was a flat 520, and two
            // captures paid for that: once the button moved to the card's bottom edge - to clear
            // the corner ornaments - a fixed 520 drew it straight over the third stat row, and at
            // 564 the card's own lower edge ran into the Valor purse under it on a 20:9 canvas.
            //
            //   966 short canvas - 268 header - 168 of footer = 530 for the card.
            const float cardHeight = 414f + 92f + UITheme.Space24;
            float top = ScreenChrome.HeaderTop + 108f + 16f;
            RectTransform card = UIBuilder.Node("Opponent", Root);
            UIBuilder.Box(card, 0f, -top, CardWidth, cardHeight, new Vector2(0.5f, 1f));

            EmberforgeUI.PanelOn(card, true);

            float inner = UITheme.Space24;

            _opponentName = EmberforgeUI.Title("Name", card, "", UITheme.FontTitle,
                                               Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(_opponentName.rectTransform, 30f, -inner, 560f, 56f, new Vector2(0f, 1f));
            EmberforgeUI.ClearCornerOrnament(_opponentName.rectTransform);

            _ghostChip = EmberforgeUI.Chip("GhostChip", card, "", UITheme.CinderGlass, UITheme.FontTiny);
            UIBuilder.Box(_ghostChip, -30f, -inner, 260f, 42f, new Vector2(1f, 1f));
            _ghostChipText = _ghostChip.GetComponentInChildren<Text>();

            inner += 56f + UITheme.Space8;

            _opponentPower = EmberforgeUI.Label("Power", card, "", UITheme.FontSmall,
                                                Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(_opponentPower.rectTransform, 30f, -inner, 560f, 32f, new Vector2(0f, 1f));

            inner += 32f + UITheme.Space12;

            Image divider = EmberforgeUI.Rule("Divider", card);
            UIBuilder.Box(divider.rectTransform, 0f, -inner, CardWidth - 60f, Emberforge.RuleHeight,
                          new Vector2(0.5f, 1f));
            inner += UITheme.Space16;

            // The opposing line, as portraits. Widgets.Portrait is the same badge the Hub and the
            // campaign strip use, so an opponent's heroes read as heroes rather than as a list.
            _lineRow = UIBuilder.Node("Line", card);
            UIBuilder.Box(_lineRow, 0f, -inner, CardWidth - 60f, 120f, new Vector2(0.5f, 1f));
            UIBuilder.HStack(_lineRow, 16f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            inner += 120f + UITheme.Space12;

            RectTransform statList = UIBuilder.Node("Stats", card);
            UIBuilder.Box(statList, 0f, -inner, CardWidth - 60f, 114f, new Vector2(0.5f, 1f));
            UIBuilder.VStack(statList, 4f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);

            _standingValue = Widgets.StatRow("Standing", statList, "YOUR STANDING", "", Emberforge.TextGold, 34f);
            _brandsValue = Widgets.StatRow("Brands", statList, "BRANDS AVAILABLE", "", UITheme.Ember, 34f);
            _nextBrandValue = Widgets.StatRow("NextBrand", statList, "NEXT BRAND IN", "", Emberforge.TextQuiet, 34f);

            inner += 114f + UITheme.Space16;

            // Anchored to the card's BOTTOM with the panel's own padding under it, rather than
            // carried down by the running `inner` offset. Hung off the top it landed exactly on
            // the card's lower edge and printed itself over the two bottom corner ornaments.
            _fightButton = EmberforgeUI.Button("Fight", card, "FIGHT", UITheme.FontHeading, ForgeButtonKind.Primary);
            UIBuilder.Box(_fightButton.Root, 0f, UITheme.Space24, CardWidth - 60f, 92f,
                          new Vector2(0.5f, 0f));
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
            // BOTH ANCHORED TO THE BOTTOM of the screen rather than measured down from a
            // hard-coded copy of the card's height. Two numbers that have to agree by hand are
            // two numbers that stop agreeing: this method carried a literal 520 that the card no
            // longer is. Hung off the bottom, the purse and the note sit where they belong at any
            // canvas height and cannot drift from the card above them.
            float noteHeight = 60f;
            float bottom = UITheme.Space24 + noteHeight + UITheme.Space12;

            RectTransform purse = UIBuilder.Node("Purse", Root);
            UIBuilder.Box(purse, 0f, bottom, CardWidth, 56f, new Vector2(0.5f, 0f));
            EmberforgeUI.SunkenOn(purse);
            _valorValue = EmberforgeUI.Label("Valor", purse, "", UITheme.FontSmall,
                                             Emberforge.TextGold, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(_valorValue.rectTransform, 8f);
            UIBuilder.Outlined(_valorValue, Emberforge.Outline, Emberforge.StrokeThin);

            // THE ARMORY'S DOOR (DESIGN-SINKS.md), beside the purse it spends: right of it, at the
            // same height, clear of the opponent card above at 20:9 as well. Only with the switch on.
            if (SinksSwitch.Enabled)
            {
                UIButton armory = EmberforgeUI.Button("Armory", Root, "ARMORY", UITheme.FontSmall, ForgeButtonKind.Secondary);
                UIBuilder.Box(armory.Root, CardWidth * 0.5f + UITheme.Space16 + 130f, bottom, 260f, 56f, new Vector2(0.5f, 0f));
                armory.OnClick(delegate
                {
                    if (!FeatureGates.TryEnter(App, "proving.Armory")) return;
                    App.Audio.Tap();
                    Router.Show(ScreenId.Armory);
                });
            }

            _honestyNote = EmberforgeUI.Body("Note", Root,
                "Opponents are reconstructed lines, not other players. Standing is your own record"
                + " and sets how hard the next line is - it is not a rank against anyone.",
                UITheme.FontTiny - 2, Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Box(_honestyNote.rectTransform, 0f, UITheme.Space24, CardWidth, noteHeight,
                          new Vector2(0.5f, 0f));
        }

        // ------------------------------------------------------------------ refresh

        public override void OnShow()
        {
            App.RefreshHud();
            _tick = 0f;
            _ad.Bind();
            Refresh();
        }

        public override void OnHide()
        {
            if (_ad != null) _ad.Unbind();
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
            if (_ad != null) _ad.Refresh();
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

                RectTransform portrait = EmberforgeUI.PortraitFrame("Portrait", cell, def,
                                                                    entry.Stars, 96f);
                UIBuilder.Box(portrait, 0f, 0f, 96f, 96f, new Vector2(0.5f, 1f));

                Text level = EmberforgeUI.Label("Level", cell, "LV " + entry.Level, UITheme.FontTiny - 4,
                                             Emberforge.TextQuiet, TextAnchor.UpperCenter);
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
