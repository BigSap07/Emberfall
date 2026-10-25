using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// THE TALENT PICK, for one hero: four mutually exclusive states, and what each one does.
    ///
    /// A LOADOUT, NOT A TREE. DESIGN-TALENTS T1: a hero authors three talents, the player runs one
    /// of them or none, and the pick is free to change at any time. Nothing on this page draws a
    /// connection between two talents, nothing is spent, nothing is earned and no talent unlocks
    /// another - so the page is a row of equal cards and a fourth option that runs none, never a
    /// diagram. The three cards are the same size at the same height with the same blocks in the
    /// same order, because the only thing a reader is being asked to do here is compare them.
    ///
    /// THE CARD ANSWERS THREE QUESTIONS AND NOTHING ELSE: what do I have, what does this cost,
    /// what do I get. The legibility pass is what cut it to that. Every block that explained a
    /// rule - the authored sentence, the audit's account of a trade, the two footnotes, the
    /// page's footer - moved behind a "?", one tap away, because a card that explains itself in
    /// prose is a card a new player reads none of. Top to bottom:
    ///
    ///   WHAT IT COSTS, when it costs anything. TalentAudit.Costly names the talents that buy
    ///     their utility with their own hero's output. The chip stays on the card, under the
    ///     name, because a cost found last is a cost not found; the sentence saying both halves
    ///     of the trade is the first thing behind the card's "?".
    ///   WHAT YOU GET, IN NUMBERS, and it is the LARGEST TEXT ON THE CARD. Durations, sizes,
    ///     thresholds, Energy and damage, printed from the effect data through TalentMath,
    ///     AbilityMath and TalentText - before and after, where a talent moves something the kit
    ///     already had. Not one figure here is written in this file; they come from the same
    ///     AbilityMath the harness proves equal to what CombatSimulation deals, in
    ///     CheckAbilityNumbersMatchTheSimulation. A talent with no number of its own prints its
    ///     sentence here instead, because then the sentence IS what you get.
    ///
    /// AND THE THREE WAYS A FIGURE CAN BE TRUE ARE STILL TOLD APART. Six effects in the roster
    /// take their amount from a runtime quantity and twenty-one are priced entirely off the
    /// target's health: neither has an honest flat number outside a fight, so those print their
    /// clause. Those that carry a term the card cannot see and a real caster-side figure print
    /// the figure and say "or more". TalentFigureBasis is the distinction; TalentText.FloorNote
    /// and ClauseNote, the two sentences that explain it, are behind the "?" of a card whose list
    /// earned one.
    ///
    /// NONE IS ONE OF THE OPTIONS, not the absence of one, and it stays selectable the same way
    /// - a row under the three that the player presses. But it is the QUIETEST control on the
    /// page: a page whose purpose is picking a talent spent its loudest button on picking none,
    /// and button weight follows usefulness. It is a plain row with no coloured button.
    ///
    /// THE MEASUREMENT IS ONE LINE per card. TalentTrial fights the player's own line against a
    /// mirror of itself and reports what each talent did to this hero's output, which is a
    /// reading of what you get and stays. How the reading was taken - fights, milliseconds on
    /// this device - was telemetry for whoever built the page, and is gone from it.
    ///
    /// CARDS END WHERE THEIR CONTENT ENDS. The three used to stretch to the bottom of the
    /// canvas whatever they held, so a talent printing one figure carried a third of a card of
    /// empty panel. They are top-anchored and as tall as the tallest of the three needs, and the
    /// none row follows directly under them.
    ///
    /// NO GAME LOGIC HERE. The rule is GameSession.SelectTalent, the numbers are TalentMath and
    /// AbilityMath, the measurement is TalentTrial, and this page draws what they return.
    /// </summary>
    public sealed class TalentsScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Talents; } }

        // THE CARDS HANG FROM THE STRIP and are as tall as their content, up to what the
        // SHORTEST canvas this game targets leaves them:
        //
        //   966 short canvas - 252 header - 92 hero strip - 8 - 12 - 64 none row - 20 = 518 of card.
        //
        // Inside a card the top block (name, cost chip) and the bottom block (measurement,
        // button) are fixed, and the numbers list between them is as long as its figures, and
        // scrolls only once the canvas runs out. Measured against the roster rather than
        // guessed: of the 90 authored talents the longest list is Talren III at six figures.
        // Build below asserts the worst case anyway, because that is a fact about today's
        // content and not about the layout.
        private const float ShortestCanvasHeight = 966f;
        private const float HeaderBottom = 252f;
        private const float StripHeight = 92f;
        private const float StripGap = 8f;
        private const float CardsTop = HeaderBottom + StripHeight + StripGap;
        private const float CardGutter = 24f;

        // The none row, directly under the cards rather than pinned to the canvas bottom.
        private const float NoneRowGap = 12f;
        private const float NoneRowHeight = 64f;
        private const float BottomMargin = 20f;

        private const float CardPad = 28f;
        private const float BlockGap = 8f;
        private const float NameTop = 18f;
        private const float NameHeight = 38f;
        private const float CostHeight = 28f;
        private const float RuleGap = 6f;
        private const float WhereRow = 22f;
        private const float OutcomeRow = 36f;
        private const float ListSpacing = 2f;
        private const float ListPad = 4f;

        private const float ButtonHeight = 60f;
        private const float MeasuredHeight = 22f;

        /// <summary>The "?" disc in a card's top-right corner, and the room the row beside it
        /// leaves for it whether or not this card has anything behind one.</summary>
        private const float HelpDiscSize = 44f;
        private const float HelpReserve = HelpDiscSize + 10f;
        private const float StateChipWidth = 116f;

        /// <summary>Everything under the numbers list: the measurement line and the button.</summary>
        private const float CardBottom = CardPad * 0.5f + ButtonHeight + 10f + MeasuredHeight + BlockGap;

        /// <summary>Where the numbers list starts, for a hero whose talents carry no cost chip.</summary>
        private const float NumbersTopPlain = NameTop + NameHeight + BlockGap + Emberforge.RuleHeight + RuleGap;

        /// <summary>The same, for a hero one of whose talents carries one.</summary>
        private const float NumbersTopCostly = NumbersTopPlain + CostHeight + BlockGap;

        private const float StripCell = 92f;
        private const float StripPortrait = 50f;

        private HeroInstance _hero;
        private TalentTrial _trial;
        private int _trialFrames;
        private bool _trialLogged;

        private Text _headerSubtitle;
        private RectTransform _stripContent;
        private readonly List<ForgeCard> _stripCells = new List<ForgeCard>();
        private readonly List<HeroInstance> _stripHeroes = new List<HeroInstance>();

        private RectTransform _cardsHost;
        private readonly List<CardView> _cards = new List<CardView>();

        private RectTransform _empty;
        private Text _emptyTitle;
        private Text _emptyBody;
        private UIButton _emptyButton;

        private ForgeCard _noneCard;
        private RectTransform _noneChip;
        private Text _noneLabel;
        private RectTransform _noneState;

        /// <summary>What the page's own "?" says: the rules every pick on it shares, rewritten on
        /// every repaint because they name the hero and its tier.</summary>
        private string _pageHelp = "";

        /// <summary>What one talent's card needs to repaint without being rebuilt.</summary>
        private sealed class CardView
        {
            public int Selection;
            public ForgeCard Card;
            public RectTransform Numeral;
            public Text Name;
            public RectTransform StateChip;
            public RectTransform CostChip;
            public ForgeDisc Help;
            public string HelpTitle = "";
            public string HelpBody = "";
            public Image Rule;
            public RectTransform NumbersList;
            public ScrollRect NumbersScroll;
            public Text More;
            public Text Measured;
            public UIButton Run;
        }

        /// <summary>Opens the page on a hero. Any owned hero is accepted: one without talents is
        /// shown as that, with the player's own 5-stars one tap away, rather than refused.</summary>
        public void SetHero(HeroInstance hero)
        {
            _hero = hero;
        }

        // ------------------------------------------------------------------ build

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            RectTransform header = ScreenChrome.ForgedHeader(Root, "TALENTS", Router, out _headerSubtitle);
            ScreenChrome.HeaderHelp(header, delegate { App.Modal.ShowNotice("TALENTS", _pageHelp, "OK", null); });
            BuildStrip();
            BuildCards();
            BuildEmpty();
            BuildNoneRow();

            // Says out loud when a card has outgrown the shortest canvas, for the reason
            // DesignGallery.CheckFits exists: the failure is silent at 16:9. The case measured is
            // the worst one - a hero carrying a cost chip - and the floor is two figures under
            // their ability's name, below which the list is not a list.
            float card = MostCardHeight(ShortestCanvasHeight);
            float needed = NumbersTopCostly + ListNeed(2, 1) + CardBottom;
            if (needed > card)
                Debug.LogError("TalentsScreen: the worst card needs " + needed + " units for two lines of"
                               + " numbers and the shortest canvas leaves " + card + ".");
        }

        /// <summary>The tallest a card may be on a canvas this tall: what is left under the strip
        /// once the none row and the bottom margin are taken.</summary>
        private static float MostCardHeight(float canvasHeight)
        {
            return canvasHeight - CardsTop - NoneRowGap - NoneRowHeight - BottomMargin;
        }

        /// <summary>How tall a numbers list is with this many figure rows and ability headings,
        /// measured the way its layout group lays them out.</summary>
        private static float ListNeed(int outcomeRows, int whereRows)
        {
            int rows = outcomeRows + whereRows;
            return outcomeRows * OutcomeRow + whereRows * WhereRow
                 + Mathf.Max(0, rows - 1) * ListSpacing + ListPad;
        }

        /// <summary>
        /// The strip of the player's own heroes with talents, each showing which one it runs.
        ///
        /// It is the page's other axis. A talent pick is made one hero at a time, but "which of my
        /// 5-stars is still running nothing" is a question about all of them, and this answers it
        /// at a glance: the numeral under each face is that hero's pick.
        /// </summary>
        private void BuildStrip()
        {
            RectTransform strip = UIBuilder.Node("Strip", Root);
            UIBuilder.TopStrip(strip, StripHeight, HeaderBottom, UITheme.ScreenPadding);
            Image recess = EmberforgeUI.SunkenOn(strip);
            recess.raycastTarget = false;

            ScrollRect scroll;
            _stripContent = UIBuilder.ScrollStrip("Heroes", strip, out scroll, 8f, new RectOffset(12, 12, 6, 6));
            UIBuilder.FillParent(scroll.GetComponent<RectTransform>(), 4f);
        }

        private void BuildCards()
        {
            // Top-anchored; FillCards sets the height once it knows what the three hold.
            _cardsHost = UIBuilder.Node("Cards", Root);
            _cardsHost.anchorMin = new Vector2(0f, 1f);
            _cardsHost.anchorMax = new Vector2(1f, 1f);
            _cardsHost.pivot = new Vector2(0.5f, 1f);
            _cardsHost.offsetMin = new Vector2(UITheme.ScreenPadding, -(CardsTop + MostCardHeight(ShortestCanvasHeight)));
            _cardsHost.offsetMax = new Vector2(-UITheme.ScreenPadding, -CardsTop);

            for (int i = 0; i < HeroDef.MaxTalents; i++) _cards.Add(BuildCard(i));
        }

        private CardView BuildCard(int index)
        {
            CardView view = new CardView();
            view.Selection = index + 1;

            view.Card = new ForgeCard();
            view.Card.Root = UIBuilder.Node("Talent" + (index + 1), _cardsHost);
            RectTransform root = view.Card.Root;
            root.anchorMin = new Vector2(index / 3f, 0f);
            root.anchorMax = new Vector2((index + 1) / 3f, 1f);
            root.offsetMin = new Vector2(index == 0 ? 0f : CardGutter * 0.5f, 0f);
            root.offsetMax = new Vector2(index == 2 ? 0f : -CardGutter * 0.5f, 0f);
            view.Card.Plate = EmberforgeUI.PlainPanelOn(root);
            view.Card.Plate.raycastTarget = false;

            // The numeral and the name, on one line. The numeral is a chip because it is the
            // talent's SLOT, which is what the strip above prints and what a friend's team page
            // will one day print, so it wants to read as a tag rather than as part of the name.
            // It also carries the selected state in colour, because a page whose whole subject is
            // "which one" cannot say which one only once.
            view.Numeral = EmberforgeUI.Chip("Numeral", root, Roman(index + 1), Emberforge.TextQuiet,
                                             UITheme.FontSmall);
            UIBuilder.Box(view.Numeral, CardPad, -NameTop, 58f, NameHeight, new Vector2(0f, 1f));

            // The name is SMALLER than the figures under it: what the talent is called is how the
            // player finds it again, and what it gives is what the card is for.
            view.Name = EmberforgeUI.Title("Name", root, "", UITheme.FontSmall, Emberforge.TextGold,
                                           TextAnchor.MiddleLeft);
            view.Name.rectTransform.anchorMin = new Vector2(0f, 1f);
            view.Name.rectTransform.anchorMax = new Vector2(1f, 1f);
            view.Name.rectTransform.pivot = new Vector2(0f, 1f);
            view.Name.rectTransform.offsetMin = new Vector2(CardPad + 70f, -(NameTop + NameHeight));
            view.Name.rectTransform.offsetMax = new Vector2(-(CardPad + HelpReserve + StateChipWidth + 10f), -NameTop);
            // Wrap and truncate, not the default overflow: a long name at the smallest size runs on to a second line inside its
            // box rather than out of it and under the RUNNING chip (16:10 cards are 585 wide).
            UIBuilder.BestFit(view.Name, UITheme.FontTiny - 4, UITheme.FontSmall);
            UIBuilder.Outlined(view.Name, Emberforge.Outline, Emberforge.StrokeThin);

            view.StateChip = EmberforgeUI.Chip("Running", root, "RUNNING", UITheme.Success, UITheme.FontTiny - 4);
            UIBuilder.Box(view.StateChip, -(CardPad + HelpReserve), -(NameTop + 6f), StateChipWidth,
                          NameHeight - 12f, new Vector2(1f, 1f));

            // The "?", centred on the name row's right end. Box pivots on the anchor, so the
            // TouchMin hit rect's corner is placed half a hit rect out from the disc's centre.
            int captured = index;
            view.Help = EmberforgeUI.HelpDisc("Help", root, HelpDiscSize, delegate { ShowCardHelp(captured); });
            float half = HubStyle.TouchMin * 0.5f;
            UIBuilder.Box(view.Help.Root, -(CardPad + HelpDiscSize * 0.5f) + half,
                          -(NameTop + NameHeight * 0.5f) + half, HubStyle.TouchMin, HubStyle.TouchMin,
                          new Vector2(1f, 1f));

            // WHAT IT COSTS, and only on the talents that cost anything: the chip alone. What the
            // cost buys is TalentAudit's own sentence, the first thing behind this card's "?", so a
            // talent that leaves the audited list loses both with no change here.
            view.CostChip = EmberforgeUI.Chip("Cost", root, "COSTS OUTPUT", UITheme.Warning, UITheme.FontTiny - 5);
            UIBuilder.Box(view.CostChip, CardPad, -(NameTop + NameHeight + BlockGap), 186f, CostHeight,
                          new Vector2(0f, 1f));

            view.Rule = EmberforgeUI.Rule("Rule", root);

            // The numbers list: what you get, and the largest text on the card.
            ScrollRect scroll;
            view.NumbersList = UIBuilder.ScrollList("Numbers", root, out scroll, ListSpacing,
                                                    new RectOffset(0, 0, (int)(ListPad * 0.5f), (int)(ListPad * 0.5f)));
            view.NumbersScroll = scroll;

            // Says there is more below when the list outruns its room. A list cut at its fourth
            // line reads as a list of four.
            view.More = EmberforgeUI.Label("More", root, "MORE BELOW", UITheme.FontTiny - 8, Emberforge.TextGold,
                                           TextAnchor.LowerRight);
            UIBuilder.Outlined(view.More, Emberforge.Outline, Emberforge.StrokeThin);
            view.More.gameObject.SetActive(false);

            // The measurement, one line, bottom-anchored so the three read across as one row.
            view.Measured = EmberforgeUI.Label("Measured", root, "", UITheme.FontTiny - 4, Emberforge.TextQuiet,
                                               TextAnchor.LowerLeft);
            Bottom(view.Measured.rectTransform, CardPad * 0.5f + ButtonHeight + 10f, MeasuredHeight);
            BestFit(view.Measured, UITheme.FontTiny - 9, UITheme.FontTiny - 4);
            UIBuilder.Outlined(view.Measured, Emberforge.Outline, Emberforge.StrokeThin);

            view.Run = EmberforgeUI.Button("Run", root, "RUN THIS TALENT", UITheme.FontSmall, ForgeButtonKind.Secondary);
            RectTransform run = view.Run.Root;
            run.anchorMin = new Vector2(0f, 0f);
            run.anchorMax = new Vector2(1f, 0f);
            run.pivot = new Vector2(0.5f, 0f);
            run.offsetMin = new Vector2(Emberforge.OrnamentClearance + 8f, CardPad * 0.5f);
            run.offsetMax = new Vector2(-(Emberforge.OrnamentClearance + 8f), CardPad * 0.5f + ButtonHeight);
            int selection = view.Selection;
            view.Run.OnClick(delegate { Press(selection); });
            return view;
        }

        /// <summary>
        /// THE FOURTH OPTION, selected the same way as the other three and pressed the same way
        /// as a card in the strip: the whole row is the control.
        ///
        /// A row rather than a card because it has no numbers to print - that IS what it does -
        /// and a fourth column would have shrunk the three that do. It sits directly under them,
        /// in reading order, so the choice is read as four things.
        ///
        /// IT CARRIES NO COLOURED BUTTON. It used to carry the only teal one on the page, which
        /// made "run nothing" the loudest thing on a screen whose purpose is picking something.
        /// Button weight follows usefulness, so this row speaks in the quiet voice and only its
        /// gold frame and RUNNING chip say when it is the pick.
        /// </summary>
        private void BuildNoneRow()
        {
            _noneCard = EmberforgeUI.SelectableCard("NoTalentRow", Root, delegate { Pick(0); });
            RectTransform row = _noneCard.Root;
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            PlaceNoneRow(MostCardHeight(ShortestCanvasHeight));

            _noneChip = EmberforgeUI.Chip("Numeral", _noneCard.Body, "NONE", Emberforge.TextQuiet, UITheme.FontTiny - 4);
            UIBuilder.Box(_noneChip, CardPad, 0f, 96f, 30f, new Vector2(0f, 0.5f));

            _noneLabel = EmberforgeUI.Label("Label", _noneCard.Body, "", UITheme.FontTiny,
                                            Emberforge.TextQuiet, TextAnchor.MiddleLeft);
            UIBuilder.Box(_noneLabel.rectTransform, CardPad + 112f, 0f, 520f, 34f, new Vector2(0f, 0.5f));
            UIBuilder.Outlined(_noneLabel, Emberforge.Outline, Emberforge.StrokeThin);

            _noneState = EmberforgeUI.Chip("Running", _noneCard.Body, "RUNNING", UITheme.Success, UITheme.FontTiny - 4);
            UIBuilder.Box(_noneState, -CardPad, 0f, StateChipWidth, NameHeight - 12f, new Vector2(1f, 0.5f));
        }

        /// <summary>Hangs the none row directly under cards of this height.</summary>
        private void PlaceNoneRow(float cardHeight)
        {
            RectTransform row = _noneCard.Root;
            float top = CardsTop + cardHeight + NoneRowGap;
            row.offsetMin = new Vector2(UITheme.ScreenPadding, -(top + NoneRowHeight));
            row.offsetMax = new Vector2(-UITheme.ScreenPadding, -top);
        }

        /// <summary>The page for a hero with no talents, in place of the three cards.</summary>
        private void BuildEmpty()
        {
            _empty = UIBuilder.Node("NoTalents", Root);
            UIBuilder.Box(_empty, 0f, -60f, 1100f, 380f, new Vector2(0.5f, 0.5f));
            EmberforgeUI.PanelOn(_empty, true);

            _emptyTitle = EmberforgeUI.Title("Title", _empty, "", UITheme.FontHeading, Emberforge.TextGold,
                                             TextAnchor.UpperCenter);
            UIBuilder.Box(_emptyTitle.rectTransform, 0f, -40f, 980f, 48f, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(_emptyTitle, Emberforge.Outline, Emberforge.StrokeThick);

            _emptyBody = EmberforgeUI.Body("Body", _empty, "", UITheme.FontSmall, Emberforge.TextBody,
                                           TextAnchor.UpperCenter);
            UIBuilder.Box(_emptyBody.rectTransform, 0f, -104f, 960f, 150f, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(_emptyBody, Emberforge.Outline, Emberforge.StrokeThin);

            _emptyButton = EmberforgeUI.Button("Rift", _empty, "OPEN THE RIFT", UITheme.FontBody,
                                               ForgeButtonKind.Primary);
            UIBuilder.Box(_emptyButton.Root, 0f, Emberforge.OrnamentClearance, 420f, 76f, new Vector2(0.5f, 0f));
            _emptyButton.OnClick(delegate { App.Audio.Tap(); Router.Show(ScreenId.Summon); });
            _empty.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ show

        public override void OnShow()
        {
            App.RefreshHud();
            List<HeroInstance> owned = App.Session.Profile.Heroes;
            if (_hero == null || App.Session.Profile.FindHeroByUid(_hero.Uid) != _hero)
            {
                _hero = null;
                for (int i = 0; i < owned.Count && _hero == null; i++)
                    if (owned[i].Def != null && owned[i].Def.Talents.Count > 0) _hero = owned[i];
                if (_hero == null && owned.Count > 0) _hero = owned[0];
            }
            RebuildStrip();
            ShowHero(_hero);
        }

        private void ShowHero(HeroInstance hero)
        {
            _hero = hero;
            _trial = TalentTrial.Create(App.Session, hero);
            _trialFrames = 0;
            _trialLogged = false;

            bool has = hero != null && hero.Def != null && hero.Def.Talents.Count > 0;
            _cardsHost.gameObject.SetActive(has);
            _empty.gameObject.SetActive(!has);
            _noneCard.Root.gameObject.SetActive(has);

            if (hero == null || hero.Def == null)
            {
                _headerSubtitle.text = "";
                ShowNoTalents(null);
            }
            else
            {
                bool deployed = App.Session.Profile.TeamSlotOf(hero.Uid) >= 0;
                _headerSubtitle.text = hero.Def.Name.ToUpperInvariant() + "   -   "
                                     + (hero.Def.Title.Length > 0 ? hero.Def.Title.ToUpperInvariant() + "   -   " : "")
                                     + hero.Def.Stars + "-STAR"
                                     + (has ? deployed ? "   -   IN YOUR LINE" : "   -   ON THE BENCH" : "");
                if (has) FillCards(hero);
                else ShowNoTalents(hero);
            }
            RefreshStripSelection();
            RefreshState();
            RefreshReadings();
        }

        /// <summary>
        /// A hero with no talents, said as a fact about the game rather than as a missing feature:
        /// sixty-four of the ninety-four heroes have none, by design, and their kits are complete.
        /// The way out is the player's own 5-stars in the strip above, or the Rift if there are none.
        /// </summary>
        private void ShowNoTalents(HeroInstance hero)
        {
            int fives = _stripHeroes.Count;
            string name = hero != null && hero.Def != null ? hero.Def.Name : "This hero";
            int stars = hero != null && hero.Def != null ? hero.Def.Stars : 0;
            _emptyTitle.text = name.ToUpperInvariant() + " HAS NO TALENTS";
            // What the player has and where to go. Why talents are a 5-star's alone is a rule, and
            // it is behind the page's "?" with the others.
            _emptyBody.text = fives > 0
                ? "Pick one of your " + fives + (fives == 1 ? " 5-star" : " 5-stars") + " above."
                : "You do not hold a 5-star yet.";
            _emptyButton.Root.gameObject.SetActive(fives == 0);
            _pageHelp = "Talents belong to the thirty 5-star heroes, three to each."
                      + (stars > 0 ? " " + name + " is a " + stars + "-star, and that kit is complete as it stands." : "");
        }

        /// <summary>
        /// The three cards, filled from the hero's own kit, and then sized to what they hold.
        ///
        /// The cost chip is decided ONCE FOR THE HERO: it sits above the numbers, so it moves the
        /// list's top, and the list's top is read across the three cards. A card without a chip
        /// leaves that row empty rather than starting its figures higher than its neighbours'.
        ///
        /// The HEIGHT is decided after all three are filled: the three are read across, so they
        /// share one height, and that height is the tallest of their contents - capped by what
        /// the canvas has, past which the list scrolls.
        /// </summary>
        private void FillCards(HeroInstance hero)
        {
            HeroDef def = hero.Def;
            RelicDef relic = App.Session.Profile.EquippedRelicOf(hero, App.Session.Content);
            // The numbers are this hero's own, at its own level and with its own relic and armour,
            // exactly the stat line the hero page prints its ability numbers from.
            UnitStats stats = StatMath.ForHero(def, hero.Level, relic, App.Session.Profile.EquippedArmorOf(hero),
                                               App.Session.Content.Armor);

            bool anyCostly = false;
            for (int i = 0; i < _cards.Count; i++)
                if (TalentAudit.For(def.Id, _cards[i].Selection) != null) anyCostly = true;
            float listTop = anyCostly ? NumbersTopCostly : NumbersTopPlain;

            float listNeed = 0f;
            for (int i = 0; i < _cards.Count; i++)
            {
                CardView view = _cards[i];
                PassiveDef talent = def.TalentAt(view.Selection);
                view.Card.Root.gameObject.SetActive(talent != null);
                if (talent == null) continue;

                view.Name.text = talent.Name.ToUpperInvariant();

                // Read from the list, never named here: a talent that leaves the audit (Velis III
                // did) loses its chip with no change to this screen. The harness keeps the list
                // equal to its KnownCostlyTalents in CheckTalentAuditMatchesHarness.
                AuditedTalent audit = TalentAudit.For(def.Id, view.Selection);
                bool costly = audit != null;
                view.CostChip.gameObject.SetActive(costly);
                if (costly) EmberforgeUI.SetChipTint(view.CostChip, audit.Trap ? UITheme.Danger : UITheme.Warning);

                Stretch(view.Rule.rectTransform, listTop - RuleGap - Emberforge.RuleHeight, Emberforge.RuleHeight);

                for (int c = view.NumbersList.childCount - 1; c >= 0; c--)
                    Destroy(view.NumbersList.GetChild(c).gameObject);
                List<TalentNumber> numbers = TalentText.Numbers(def, talent, stats, App.Session.Balance);
                string prose = TalentText.Prose(talent);
                bool hasProse = prose != TalentText.NoProse;

                int outcomeRows = 0;
                int whereRows = 0;
                if (numbers.Count == 0)
                {
                    // NO NUMBER OF ITS OWN, so the sentence is what you get, and it takes the
                    // figures' place rather than hiding behind the "?" with nothing left on the card.
                    AddProseOutcome(view.NumbersList, hasProse ? prose : TalentText.NoProse,
                                    hasProse ? Emberforge.TextBody : Emberforge.TextQuiet);
                    outcomeRows = 2;
                }
                string heading = null;
                for (int n = 0; n < numbers.Count; n++)
                {
                    // One heading per run of lines about the same ability, so a talent that moves
                    // three things on Take Down the Wall names it once rather than three times.
                    if (numbers[n].Where != heading)
                    {
                        heading = numbers[n].Where;
                        AddNumberHeading(view.NumbersList, heading);
                        whereRows++;
                    }
                    AddNumberRow(view.NumbersList, numbers[n].Figure, numbers[n].Tint);
                    outcomeRows++;
                }
                listNeed = Mathf.Max(listNeed, ListNeed(outcomeRows, whereRows));

                // BEHIND THE "?": what the cost buys, the authored sentence when the figures said
                // what it does, and the riders on the figures that need one. All of it was on the
                // card, and all of it is explanation rather than what the player gets.
                string help = "";
                if (costly) help = Join(help, audit.Note);
                if (numbers.Count > 0 && hasProse) help = Join(help, prose);
                if (TalentText.Any(numbers, TalentFigureBasis.Floor)) help = Join(help, TalentText.FloorNote);
                if (TalentText.Any(numbers, TalentFigureBasis.Clause)) help = Join(help, TalentText.ClauseNote);
                view.HelpTitle = talent.Name.ToUpperInvariant();
                view.HelpBody = help;
                view.Help.Root.gameObject.SetActive(help.Length > 0);
            }

            // ONE HEIGHT FOR ALL THREE: the tallest content, or what the canvas leaves.
            float canvas = Root.rect.height > 0f ? Root.rect.height : ShortestCanvasHeight;
            float cardHeight = Mathf.Min(listTop + listNeed + CardBottom, MostCardHeight(canvas));
            _cardsHost.offsetMin = new Vector2(UITheme.ScreenPadding, -(CardsTop + cardHeight));
            _cardsHost.offsetMax = new Vector2(-UITheme.ScreenPadding, -CardsTop);
            PlaceNoneRow(cardHeight);

            for (int i = 0; i < _cards.Count; i++)
            {
                CardView view = _cards[i];
                RectTransform scrollRect = view.NumbersScroll.GetComponent<RectTransform>();
                scrollRect.anchorMin = new Vector2(0f, 0f);
                scrollRect.anchorMax = new Vector2(1f, 1f);
                scrollRect.offsetMin = new Vector2(CardPad, CardBottom);
                scrollRect.offsetMax = new Vector2(-CardPad, -listTop);
                view.NumbersScroll.verticalNormalizedPosition = 1f;
                Bottom(view.More.rectTransform, CardBottom, 18f);
            }
        }

        private static string Join(string a, string b)
        {
            return a.Length == 0 ? b : a + "\n\n" + b;
        }

        private void ShowCardHelp(int index)
        {
            CardView view = _cards[index];
            if (view.HelpBody.Length == 0) return;
            App.Modal.ShowNotice(view.HelpTitle, view.HelpBody, "OK", null);
        }

        /// <summary>The ability a run of numbers belongs to, in the kit's quiet voice.</summary>
        private void AddNumberHeading(RectTransform list, string where)
        {
            RectTransform row = UIBuilder.Node("Where", list);
            UIBuilder.Sized(row, 0f, WhereRow);
            Text w = EmberforgeUI.Label("Text", row, where.ToUpperInvariant(), UITheme.FontTiny - 6,
                                        Emberforge.TextQuiet, TextAnchor.LowerLeft);
            UIBuilder.FillParent(w.rectTransform, 0f);
            UIBuilder.Outlined(w, Emberforge.Outline, Emberforge.StrokeThin);
        }

        /// <summary>One figure, in the colour of what it is, at the card's largest size. Shrinks
        /// rather than wraps: the longest, "1,105 to 994 damage (210% to 189% of Attack)", is most
        /// of a card on its own.</summary>
        private void AddNumberRow(RectTransform list, string figure, Color tint)
        {
            RectTransform row = UIBuilder.Node("Number", list);
            UIBuilder.Sized(row, 0f, OutcomeRow);
            Text f = EmberforgeUI.Label("Figure", row, figure, UITheme.FontBody, tint, TextAnchor.MiddleLeft);
            UIBuilder.FillParent(f.rectTransform, 12f, 0f, 0f, 0f);
            BestFit(f, UITheme.FontTiny - 4, UITheme.FontBody);
            UIBuilder.Outlined(f, Emberforge.Outline, Emberforge.StrokeThin);
        }

        /// <summary>A talent's sentence standing in for figures it does not have: two rows tall,
        /// wrapped, and no larger than a figure would be.</summary>
        private void AddProseOutcome(RectTransform list, string text, Color tint)
        {
            RectTransform row = UIBuilder.Node("Outcome", list);
            UIBuilder.Sized(row, 0f, OutcomeRow * 2f + ListSpacing);
            Text f = EmberforgeUI.Body("Text", row, text, UITheme.FontSmall, tint, TextAnchor.UpperLeft);
            UIBuilder.FillParent(f.rectTransform, 0f);
            BestFit(f, UITheme.FontTiny - 4, UITheme.FontSmall);
            UIBuilder.Outlined(f, Emberforge.Outline, Emberforge.StrokeThin);
        }

        // ------------------------------------------------------------------ the strip

        private void RebuildStrip()
        {
            for (int i = _stripContent.childCount - 1; i >= 0; i--) Destroy(_stripContent.GetChild(i).gameObject);
            _stripCells.Clear();
            _stripHeroes.Clear();

            // Deployed first, in line order, then the bench in collection order: the heroes whose
            // pick other players meet are the ones to check first.
            PlayerProfile profile = App.Session.Profile;
            List<HeroInstance> line = profile.TeamHeroes();
            for (int i = 0; i < line.Count; i++)
                if (line[i].Def != null && line[i].Def.Talents.Count > 0) _stripHeroes.Add(line[i]);
            for (int i = 0; i < profile.Heroes.Count; i++)
            {
                HeroInstance h = profile.Heroes[i];
                if (h.Def == null || h.Def.Talents.Count == 0 || _stripHeroes.Contains(h)) continue;
                _stripHeroes.Add(h);
            }

            for (int i = 0; i < _stripHeroes.Count; i++)
            {
                HeroInstance hero = _stripHeroes[i];
                ForgeCard cell = EmberforgeUI.SelectableCard("Hero" + i, _stripContent, delegate
                {
                    App.Audio.Tap();
                    ShowHero(hero);
                });
                UIBuilder.Sized(cell.Root, StripCell, 0f);

                RectTransform face = EmberforgeUI.PortraitFrame("Face", cell.Body, hero.Def, hero.Def.Stars, StripPortrait);
                UIBuilder.Box(face, 0f, -4f, StripPortrait, StripPortrait, new Vector2(0.5f, 1f));

                Text pick = EmberforgeUI.Label("Pick", cell.Body, "", UITheme.FontTiny - 7, Emberforge.TextQuiet,
                                               TextAnchor.LowerCenter);
                UIBuilder.Box(pick.rectTransform, 0f, 0f, StripCell - 6f, 17f, new Vector2(0.5f, 0f));
                UIBuilder.Outlined(pick, Emberforge.Outline, Emberforge.StrokeThin);
                _stripCells.Add(cell);
            }
        }

        private void RefreshStripSelection()
        {
            for (int i = 0; i < _stripCells.Count; i++)
            {
                HeroInstance hero = _stripHeroes[i];
                EmberforgeUI.SetCardSelected(_stripCells[i], hero == _hero);
                Text pick = _stripCells[i].Body.Find("Pick").GetComponent<Text>();
                // The pick alone. The heroes in the line lead the strip, so "in your line" is said
                // by position; saying it on the tag as well crowded the numeral off a 92-unit cell.
                //
                // WHAT IS RUNNING, not what is stored. The strip answers one question - which of my
                // 5-stars is still fighting with nothing - and a hero holding a pick its ascension
                // has not unlocked is fighting with nothing, so a gold numeral here would be the
                // strip answering a different question from the one it is for. The kept pick is not
                // dropped, it is said on the card, where there is room to say it is locked and what
                // opens it.
                int live = hero.LiveTalentSelection;
                pick.text = live > 0 ? Roman(live) : "NONE";
                pick.color = live > 0 ? Emberforge.TextGold : Emberforge.TextQuiet;
            }
        }

        // ------------------------------------------------------------------ state

        /// <summary>
        /// What the card's button does, which depends on what the card is saying.
        ///
        /// An unlocked card offers to run its talent and pressing it does that. A LOCKED card has
        /// read ASCEND TO TIER N since the gate landed, and until the fusion page existed that was
        /// a caption on a dead control - the one action that would change anything had no screen
        /// to happen on. It now opens that screen on this hero. The caption did not change; what
        /// changed is that it is true.
        /// </summary>
        private void Press(int selection)
        {
            if (_hero == null || _hero.Def == null) return;
            bool locked = _hero.Def.TalentAt(selection) != null && !_hero.TalentSlotUnlocked(selection);
            if (locked) { OpenFusion(); return; }
            Pick(selection);
        }

        /// <summary>Opens the fusion page on this hero, which is where the tier a locked card
        /// names is actually bought.</summary>
        private void OpenFusion()
        {
            if (!FeatureGates.TryEnter(App, "talents.LockedCard")) return;
            App.Audio.Tap();
            Router.Fusion.ShowHero(_hero);
            Router.Show(ScreenId.Fusion);
        }

        private void Pick(int selection)
        {
            if (_hero == null) return;
            if (!App.Session.SelectTalent(_hero, selection)) return;
            App.Audio.Tap();
            // Saved at once rather than on the way out, because the save is what publishes the
            // line a friend duels - a pick that waited for the next unrelated save would reach
            // other players at a moment nobody chose.
            App.RequestSave();
            RefreshState();
            RefreshStripSelection();
        }

        /// <summary>
        /// Everything that moves when the pick moves.
        ///
        /// Four options and exactly one of them selected, said four ways on the one that is:
        /// the plate gains its gold frame, the slot chip goes from quiet to gold, the RUNNING chip
        /// appears, and the button stops being a button. Three of those are on the card and the
        /// fourth is the strip cell above. None of the other three is dimmed - the page is a
        /// comparison, and a comparison where two of the three are faded is not one.
        ///
        /// AND A FIFTH STATE, LOCKED, which the four above could not express. DESIGN-TALENTS T6:
        /// a five-star has talent 1 at tier 0, talent 2 after one ascension and talent 3 after
        /// two. A locked card is NOT HIDDEN, because the player deciding whether to ascend is
        /// deciding on the strength of what ascending buys, and a page that hid it would be asking
        /// for that decision while withholding the only thing it turns on. So the card keeps its
        /// name, its figures and its reading, and two things say it is not available: the chip
        /// reads LOCKED, and the button names the tier that opens it instead of offering to run
        /// it. The rule itself is behind the page's "?".
        ///
        /// THE FRAME MEANS "YOUR PICK" AND THE CHIP MEANS "ITS STATE", and locking is the first
        /// thing that pulls those apart. A pick made at tier 2 and then locked - a tier that moved
        /// under it, a hand-edited save - is KEPT by GameSession.SelectTalent's own rule, so the
        /// card stays framed because it is still what the player chose, while the chip says LOCKED
        /// because it is not what is fighting. Neither half is dropped, and neither lies.
        /// </summary>
        private void RefreshState()
        {
            if (_hero == null || _hero.Def == null) return;
            int running = _hero.SelectedTalent;
            int lockedSlots = 0;
            // ONE ORANGE BUTTON (R53): RUN THIS TALENT on the first card that can be switched to, by the
            // loop-card rule (EmberforgeUI.LoopCardKind). The running card has nothing to press, and the ascension
            // door on a locked card is a trip to another page, so it is a Secondary.
            bool runPrimaryGiven = false;
            for (int i = 0; i < _cards.Count; i++)
            {
                CardView view = _cards[i];
                bool authored = _hero.Def.TalentAt(view.Selection) != null;
                bool locked = authored && !_hero.TalentSlotUnlocked(view.Selection);
                bool on = view.Selection == running;
                if (locked) lockedSlots++;

                EmberforgeUI.SetCardSelected(view.Card, on);
                EmberforgeUI.SetChipTint(view.Numeral, on ? locked ? UITheme.Warning : Emberforge.TextGold
                                                          : Emberforge.TextQuiet);
                view.StateChip.gameObject.SetActive(on || locked);
                EmberforgeUI.SetChipText(view.StateChip, locked ? "LOCKED" : "RUNNING");
                EmberforgeUI.SetChipTint(view.StateChip, locked ? UITheme.Warning : UITheme.Success);

                // The button is where the card says what to DO about it, which for a locked card
                // is not "run this" - the one action that would change anything is an ascension,
                // and it names the tier rather than saying "locked" a second time. Pressing it
                // opens the fusion page on this hero.
                view.Run.SetText(locked ? "ASCEND TO TIER " + HeroInstance.TierUnlockingTalent(view.Selection)
                                        : on ? "RUNNING" : "RUN THIS TALENT");
                view.Run.Interactable = locked || !on;
                EmberforgeUI.SetKind(view.Run, locked || on ? ForgeButtonKind.Secondary
                                                            : EmberforgeUI.LoopCardKind(true, ref runPrimaryGiven));
            }

            bool none = running == 0;
            EmberforgeUI.SetCardSelected(_noneCard, none);
            EmberforgeUI.SetChipTint(_noneChip, none ? Emberforge.TextGold : Emberforge.TextQuiet);
            _noneState.gameObject.SetActive(none);
            _noneLabel.text = none ? "NO TALENT" : "RUN NO TALENT";
            _noneLabel.color = none ? Emberforge.TextGold : Emberforge.TextQuiet;
            _noneCard.Button.Button.interactable = !none;

            // THE PAGE'S RULES, behind its "?". Every sentence below used to be printed on the
            // page - in the none bar and in two footer lines - and each one explains a rule rather
            // than saying what the player has, what it costs or what they get.
            string name = _hero.Def.Name;
            // "the three" is read off the hero rather than written here: HeroDef.MaxTalents is the
            // ceiling and not a promise, and a hero authoring two would be told it has three.
            int count = _hero.Def.Talents.Count;
            if (count == 0) return;
            string which = count == 1 ? "it" : "the " + Word(count);

            string rule = lockedSlots == 0
                ? "One at a time, or none. Free to change, any time, as often as you like - nothing is"
                  + " spent, nothing is locked in, and no talent unlocks another."
                : "One at a time, or none, and free to change as often as you like - nothing is spent. No talent"
                  + " unlocks another; ascension does. Tier 1 opens the second, tier 2 the third. " + name
                  + " is at tier " + Mathf.Max(0, _hero.Ascension) + ", so " + Word(lockedSlots)
                  + (lockedSlots == 1 ? " is" : " are") + " still shut.";

            // WHAT THE HERO IS ACTUALLY FIGHTING WITH, which is not always what the player picked.
            // A pick that resolves to nothing - locked, or naming a slot this version of the game
            // no longer authors - means the hero fights with none. Both cases end the same way,
            // because both follow the same rule: the pick is kept.
            string fights;
            PassiveDef live = _hero.LiveTalent;
            if (live != null || none)
            {
                fights = "An unselected talent is not a quieter talent: it is not in the battle at all.";
            }
            else if (_hero.Def.TalentAt(running) != null)
            {
                fights = name + " fights with none of " + which + " today: talent " + Roman(running)
                       + " is picked and still locked, so it is not in the battle at all. Ascend to tier "
                       + HeroInstance.TierUnlockingTalent(running) + " and it comes straight back.";
            }
            else
            {
                fights = name + " fights with none of " + which + ": the pick is talent " + Roman(running)
                       + ", which this version of Emberfall does not have. It is kept rather than lost -"
                       + " if that talent returns, so does the pick.";
            }

            bool deployed = App.Session.Profile.TeamSlotOf(_hero.Uid) >= 0;
            string travels = deployed
                ? name + " is in your line, so this pick is part of what other players meet: friends who duel you face it from your next save, and your next Proving ghost carries it."
                : name + " is on the bench. A pick only reaches the line friends duel, and your Proving ghost, once " + name + " stands in it.";

            _pageHelp = rule + "\n\n" + fights + "\n\n" + travels;
        }

        // ------------------------------------------------------------------ the measurement

        /// <summary>Budget per frame for the trial, in milliseconds. A 60 fps frame is 16; this
        /// leaves the rest of it to the page.</summary>
        private const double TrialBudgetMs = 6.0;

        private void Update()
        {
            if (Root == null || !Root.gameObject.activeInHierarchy) return;
            if (_trial == null || _trial.Finished) return;
            _trial.Step(TrialBudgetMs);
            _trialFrames++;
            RefreshReadings();
        }

        private void LateUpdate()
        {
            if (Root == null || !Root.gameObject.activeInHierarchy) return;
            for (int i = 0; i < _cards.Count; i++)
            {
                CardView view = _cards[i];
                RectTransform viewport = view.NumbersScroll.viewport != null
                    ? view.NumbersScroll.viewport : view.NumbersScroll.GetComponent<RectTransform>();
                bool more = view.NumbersList.rect.height > viewport.rect.height + 1f
                         && view.NumbersScroll.verticalNormalizedPosition > 0.01f;
                if (view.More.gameObject.activeSelf != more) view.More.gameObject.SetActive(more);
            }
        }

        /// <summary>
        /// The per-card readings, and nothing about how they were taken. The fight count and the
        /// milliseconds this device spent were a footer line for whoever built the page; they go
        /// to the console once, when the trial finishes, and never to the screen.
        /// </summary>
        private void RefreshReadings()
        {
            if (_trial == null)
            {
                for (int i = 0; i < _cards.Count; i++) _cards[i].Measured.text = "";
                return;
            }

            TalentReading bare = _trial.Readings[0];
            for (int i = 0; i < _cards.Count; i++)
            {
                CardView view = _cards[i];
                if (view.Selection >= _trial.Readings.Length) { view.Measured.text = ""; continue; }
                ShowReading(view, _trial.Readings[view.Selection], bare);
            }

            if (!_trial.Finished || _trialLogged) return;
            _trialLogged = true;
            Debug.Log("TalentsScreen: measured " + _hero.Def.Name + " - " + _trial.FightsRun + " fights, "
                      + _trial.SimulationMs.ToString("0", CultureInfo.InvariantCulture) + " ms of simulation over "
                      + _trialFrames + " frames" + (_trial.StandIn != null ? " (bench, stood in for " + _trial.StandIn.Def.Name + ")" : "")
                      + ".");
            if (_trial.Failures > 0)
                Debug.LogError("TalentsScreen: " + _trial.Failures + " trial fights threw. Last: " + _trial.LastError);
        }

        /// <summary>
        /// One talent's reading, in ONE line.
        ///
        /// It used to be four, and it was the loudest block on the card. It is a reading of ONE
        /// line of heroes, its verdict only leaves grey when the change clears the harness's own
        /// two-standard-error line, and it measures OUTPUT - which by construction cannot see
        /// what a trade buys, and TalentAudit's banner at the top of the card exists because of
        /// that blind spot. So it says what it found, in the page's quietest voice, under what
        /// the talent does rather than over it.
        /// </summary>
        private void ShowReading(CardView view, TalentReading r, TalentReading bare)
        {
            if (r.Fights < 2)
            {
                view.Measured.text = "Measuring in your line...";
                view.Measured.color = Emberforge.TextQuiet;
                return;
            }

            long delta = (long)System.Math.Round(r.Delta);
            float percent = bare.MeanOutput > 0 ? (float)(r.Delta / bare.MeanOutput) * 100f : 0f;
            string pct = (percent >= 0f ? "+" : "") + percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
            string signed = (delta >= 0 ? "+" : "") + AbilityText.Amount((int)delta);
            string tail = _trial.Finished ? "" : " ...";

            switch (r.Verdict)
            {
                case TalentVerdict.Gains:
                    view.Measured.color = UITheme.Success;
                    view.Measured.text = "In your line: " + pct + " of " + _hero.Def.Name + "'s own output ("
                                       + signed + " a fight)" + tail;
                    break;
                case TalentVerdict.Loses:
                    view.Measured.color = UITheme.Danger;
                    view.Measured.text = "In your line: " + pct + " of " + _hero.Def.Name + "'s own output ("
                                       + signed + " a fight)" + tail;
                    break;
                case TalentVerdict.Unreached:
                    view.Measured.color = Emberforge.TextQuiet;
                    view.Measured.text = "In your line: nothing it needs happened in " + r.Fights + " fights"
                                       + tail;
                    break;
                default:
                    // Still under the line, after as many fights as the trial will give it: say
                    // what the estimate is and how often the talent changed a fight at all.
                    view.Measured.color = Emberforge.TextQuiet;
                    view.Measured.text = "In your line: " + pct + " (unclear, changed " + r.Changed
                                       + " of " + r.Fights + " fights)" + tail;
                    break;
            }
        }

        // ------------------------------------------------------------------ small

        /// <summary>A small count in words, for a sentence. Only ever reaches three.</summary>
        private static string Word(int n)
        {
            return n == 1 ? "one" : n == 2 ? "two" : n == 3 ? "three" : n.ToString(CultureInfo.InvariantCulture);
        }

        private static string Roman(int n)
        {
            return n == 1 ? "I" : n == 2 ? "II" : n == 3 ? "III" : n.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Full card width less the padding, hung from the card's top.</summary>
        private static void Stretch(RectTransform rt, float top, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(CardPad, -(top + height));
            rt.offsetMax = new Vector2(-CardPad, -top);
        }

        /// <summary>Full card width less the padding, stood on the card's bottom.</summary>
        private static void Bottom(RectTransform rt, float bottom, float height)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(CardPad, bottom);
            rt.offsetMax = new Vector2(-CardPad, bottom + height);
        }

        private static void BestFit(Text label, int min, int max)
        {
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = min;
            label.resizeTextMaxSize = max;
        }

#if UNITY_EDITOR
        /// <summary>EDITOR ONLY. Runs the trial to the end in one call, so a capture photographs
        /// finished readings rather than whatever a frame count happened to reach.</summary>
        public void FinishTrialForCapture()
        {
            if (_trial == null) return;
            while (!_trial.Finished) { _trial.Step(1000.0); _trialFrames++; }
            RefreshReadings();
        }

        public bool TrialFinished { get { return _trial == null || _trial.Finished; } }
#endif
    }
}
