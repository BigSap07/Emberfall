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
    /// WHAT A TALENT DOES IS THE MIDDLE OF THE CARD AND THE LARGEST THING ON IT. Three blocks,
    /// top to bottom:
    ///
    ///   WHAT IT COSTS, when it costs anything. TalentAudit.Costly names seven talents that buy
    ///     their utility with their own hero's output, and a cost the reader finds last is a cost
    ///     the reader does not find. So it is a chip and a sentence directly under the name,
    ///     above the prose, saying both halves of the trade.
    ///   WHAT IT SAYS. The authored sentence, which by house rule quotes no number.
    ///   WHAT IT DOES, IN NUMBERS. Durations, sizes, thresholds, Energy and damage, printed from
    ///     the effect data through TalentMath, AbilityMath and TalentText - before and after,
    ///     where a talent moves something the kit already had. Not one figure here is written in
    ///     this file; they come from the same AbilityMath the harness proves equal to what
    ///     CombatSimulation deals, in CheckAbilityNumbersMatchTheSimulation.
    ///
    /// AND THE THREE WAYS A FIGURE CAN BE TRUE ARE TOLD APART rather than flattened. Six effects
    /// in the roster take their amount from a runtime quantity and twenty-one are priced entirely
    /// off the target's health: neither has an honest flat number outside a fight, so those print
    /// their clause. Twenty-eight carry a real caster-side figure with something the card cannot
    /// see added on top: those print the figure and say "or more". TalentFigureBasis is the
    /// distinction and TalentText.FloorNote and ClauseNote are the two sentences that explain it,
    /// said once under a list that contains one rather than on every line that is one.
    ///
    /// NONE IS ONE OF THE OPTIONS, not the absence of one. It sits under the three as a bar in
    /// the same plate, selected the same way, with the same kind of button - because an
    /// unselected talent is genuinely absent from the fight (HeroDef.TalentAt returns null and
    /// the simulation appends nothing), and a page that made "none" a way of failing to choose
    /// would be lying about what the fourth state is.
    ///
    /// THE MEASUREMENT IS ONE LINE. TalentTrial fights the player's own line against a mirror of
    /// itself and reports what each talent did to this hero's output. That is real and it stays,
    /// but it is a reading of ONE line and it cannot see what a trade buys - TalentAudit exists
    /// for exactly that reason - so it is a quiet line under the numbers rather than the block it
    /// used to be. What a talent DOES outranks what it measured here once.
    ///
    /// NO GAME LOGIC HERE. The rule is GameSession.SelectTalent, the numbers are TalentMath and
    /// AbilityMath, the measurement is TalentTrial, and this page draws what they return.
    /// </summary>
    public sealed class TalentsScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Talents; } }

        // THE PAGE STRETCHES between the header and the footer, measured against the SHORTEST
        // canvas this game targets, the way the hero page does.
        //
        //   966 short canvas - 252 header - 92 hero strip - 8 - 152 bottom = 462 of card.
        //
        // Inside a card the top block (name, cost, sentence) and the bottom block (measurement,
        // button) are fixed, and the numbers list between them takes what is left and scrolls past
        // it: six figures on an ordinary card at 20:9 and four on one carrying a cost banner, and
        // more than that at every wider aspect. Measured against the roster rather than guessed:
        // of the 90 authored talents the longest list is Talren III at six figures, ten carry a
        // clause footnote and three a floor footnote, no card carries both, and none of the seven
        // the audit calls costly carries either. Build below asserts the worst case anyway,
        // because that is a fact about today's content and not about the layout.
        private const float ShortestCanvasHeight = 966f;
        private const float HeaderBottom = 252f;
        private const float StripHeight = 92f;
        private const float StripGap = 8f;
        private const float CardsTop = HeaderBottom + StripHeight + StripGap;
        private const float CardGutter = 24f;

        // The bottom block, measured from the screen's bottom edge: three lines of page rule, then
        // the none bar above them.
        private const float FooterLine = 20f;
        private const float NoneBarBottom = 68f;
        private const float NoneBarHeight = 74f;
        private const float BottomBlock = NoneBarBottom + NoneBarHeight + 10f;

        private const float CardPad = 28f;
        private const float BlockGap = 8f;
        private const float NameTop = 18f;
        private const float NameHeight = 38f;
        private const float CostHeight = 64f;
        private const float ProseHeight = 68f;
        private const float RuleGap = 6f;
        private const float HeadingHeight = 20f;
        private const float NumberRow = 24f;
        private const float NoteRow = 22f;

        private const float ButtonHeight = 60f;
        private const float MeasuredHeight = 22f;

        /// <summary>Everything under the numbers list that is always there: the measurement line
        /// and the button. The footnotes are added to this per card, because a card that needs
        /// none should not reserve room for two.</summary>
        private const float CardBottomBase = CardPad * 0.5f + ButtonHeight + 10f + MeasuredHeight + 6f;

        /// <summary>Where the numbers list starts, for a talent the audit says nothing about.</summary>
        private const float NumbersTopPlain = NameTop + NameHeight + BlockGap
                                            + ProseHeight + BlockGap + RuleGap + HeadingHeight;

        /// <summary>The same, for one carrying a cost banner.</summary>
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

        private RectTransform _noneBar;
        private ForgeCard _noneCard;
        private RectTransform _noneChip;
        private Text _noneBody;
        private UIButton _noneRun;

        private Text _footerRule;
        private Text _footerTravels;
        private Text _footerMeasure;

        /// <summary>What one talent's card needs to repaint without being rebuilt.</summary>
        private sealed class CardView
        {
            public int Selection;
            public ForgeCard Card;
            public RectTransform Numeral;
            public Text Name;
            public RectTransform StateChip;
            public RectTransform CostChip;
            public Text CostNote;
            public Text Prose;
            public RectTransform Heading;
            public Image Rule;
            public Image RuleBottom;
            public RectTransform NumbersList;
            public ScrollRect NumbersScroll;
            public Text More;
            public Text FloorNote;
            public Text ClauseNote;
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
            ScreenChrome.ForgedHeader(Root, "TALENTS", Router, out _headerSubtitle);
            BuildStrip();
            BuildCards();
            BuildEmpty();
            BuildNoneBar();
            BuildFooter();

            // Says out loud when a card has outgrown the shortest canvas, for the reason
            // DesignGallery.CheckFits exists: the failure is silent at 16:9. The case measured is
            // the worst one - a costly talent whose figures need both footnotes - and the floor is
            // two lines of numbers, below which the list is not a list.
            float card = ShortestCanvasHeight - CardsTop - BottomBlock;
            float needed = NumbersTopCostly + NumberRow * 2f + CardBottomBase + NoteRow * 2f
                         + Emberforge.RuleHeight + 10f;
            if (needed > card)
                Debug.LogError("TalentsScreen: the worst card needs " + needed + " units for two lines of"
                               + " numbers and the shortest canvas leaves " + card + ".");
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
            _cardsHost = UIBuilder.Node("Cards", Root);
            _cardsHost.anchorMin = new Vector2(0f, 0f);
            _cardsHost.anchorMax = new Vector2(1f, 1f);
            _cardsHost.offsetMin = new Vector2(UITheme.ScreenPadding, BottomBlock);
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

            view.Name = EmberforgeUI.Title("Name", root, "", UITheme.FontBody, Emberforge.TextGold,
                                           TextAnchor.MiddleLeft);
            view.Name.rectTransform.anchorMin = new Vector2(0f, 1f);
            view.Name.rectTransform.anchorMax = new Vector2(1f, 1f);
            view.Name.rectTransform.pivot = new Vector2(0f, 1f);
            view.Name.rectTransform.offsetMin = new Vector2(CardPad + 70f, -(NameTop + NameHeight));
            view.Name.rectTransform.offsetMax = new Vector2(-(CardPad + 150f), -NameTop);
            view.Name.resizeTextForBestFit = true;
            view.Name.resizeTextMinSize = UITheme.FontTiny - 2;
            view.Name.resizeTextMaxSize = UITheme.FontBody;
            UIBuilder.Outlined(view.Name, Emberforge.Outline, Emberforge.StrokeThin);

            view.StateChip = EmberforgeUI.Chip("Running", root, "RUNNING", UITheme.Success, UITheme.FontTiny - 4);
            UIBuilder.Box(view.StateChip, -CardPad, -(NameTop + 6f), 136f, NameHeight - 12f, new Vector2(1f, 1f));

            // WHAT IT COSTS, first, and only on the seven talents that cost anything. The chip
            // says a cost exists; the note says what the cost buys. TalentAudit's own sentence is
            // printed exactly as it is authored, so a talent that leaves the audited list loses
            // its banner with no change here.
            view.CostChip = EmberforgeUI.Chip("Cost", root, "COSTS OUTPUT", UITheme.Warning, UITheme.FontTiny - 5);
            UIBuilder.Box(view.CostChip, CardPad, -(NameTop + NameHeight + BlockGap), 186f, 24f,
                          new Vector2(0f, 1f));

            view.CostNote = EmberforgeUI.Body("CostNote", root, "", UITheme.FontTiny - 3, UITheme.Warning,
                                              TextAnchor.UpperLeft);
            Stretch(view.CostNote.rectTransform, NameTop + NameHeight + BlockGap + 26f, CostHeight - 26f);
            BestFit(view.CostNote, UITheme.FontTiny - 8, UITheme.FontTiny - 3);
            UIBuilder.Outlined(view.CostNote, Emberforge.Outline, Emberforge.StrokeThin);

            view.Prose = EmberforgeUI.Body("Prose", root, "", UITheme.FontTiny, Emberforge.TextBody,
                                           TextAnchor.UpperLeft);
            Stretch(view.Prose.rectTransform, NumbersTopPlain - ProseHeight - BlockGap - RuleGap - HeadingHeight,
                    ProseHeight);
            BestFit(view.Prose, UITheme.FontTiny - 6, UITheme.FontTiny);
            UIBuilder.Outlined(view.Prose, Emberforge.Outline, Emberforge.StrokeThin);

            view.Rule = EmberforgeUI.Rule("Rule", root);

            // The band the figures live in is CLOSED at the bottom as well as opened at the top.
            // A talent that changes one thing prints one line, and an open-ended band with one
            // line in it and a third of a card under it reads as a card that failed to finish
            // rather than as a talent that does one thing.
            view.RuleBottom = EmberforgeUI.Rule("RuleBottom", root);

            view.Heading = UIBuilder.Node("Heading", root);
            Text heading = EmberforgeUI.Label("Text", view.Heading, "WHAT IT DOES", UITheme.FontTiny - 4,
                                              Emberforge.TextGold, TextAnchor.LowerLeft);
            UIBuilder.FillParent(heading.rectTransform, 0f);
            UIBuilder.Outlined(heading, Emberforge.Outline, Emberforge.StrokeThin);

            // The numbers list: the middle of the card, and the largest block on it.
            ScrollRect scroll;
            view.NumbersList = UIBuilder.ScrollList("Numbers", root, out scroll, 2f, new RectOffset(0, 0, 2, 2));
            view.NumbersScroll = scroll;

            // Says there is more below when the list outruns its room. A list cut at its fourth
            // line reads as a list of four.
            view.More = EmberforgeUI.Label("More", root, "MORE BELOW", UITheme.FontTiny - 8, Emberforge.TextGold,
                                           TextAnchor.LowerRight);
            UIBuilder.Outlined(view.More, Emberforge.Outline, Emberforge.StrokeThin);
            view.More.gameObject.SetActive(false);

            // The two footnotes, under the list rather than on the lines they belong to: a list
            // where four of six figures carry the same rider is a list nobody finishes.
            view.ClauseNote = EmberforgeUI.Body("ClauseNote", root, TalentText.ClauseNote,
                                                UITheme.FontTiny - 7, Emberforge.TextQuiet, TextAnchor.LowerLeft);
            BestFit(view.ClauseNote, UITheme.FontTiny - 10, UITheme.FontTiny - 7);
            UIBuilder.Outlined(view.ClauseNote, Emberforge.Outline, Emberforge.StrokeThin);

            view.FloorNote = EmberforgeUI.Body("FloorNote", root, TalentText.FloorNote,
                                               UITheme.FontTiny - 7, Emberforge.TextQuiet, TextAnchor.LowerLeft);
            BestFit(view.FloorNote, UITheme.FontTiny - 10, UITheme.FontTiny - 7);
            UIBuilder.Outlined(view.FloorNote, Emberforge.Outline, Emberforge.StrokeThin);

            // The measurement, one line, bottom-anchored so the three read across as one row.
            view.Measured = EmberforgeUI.Label("Measured", root, "", UITheme.FontTiny - 4, Emberforge.TextQuiet,
                                               TextAnchor.LowerLeft);
            Bottom(view.Measured.rectTransform, CardPad * 0.5f + ButtonHeight + 10f, MeasuredHeight);
            BestFit(view.Measured, UITheme.FontTiny - 9, UITheme.FontTiny - 4);
            UIBuilder.Outlined(view.Measured, Emberforge.Outline, Emberforge.StrokeThin);

            view.Run = EmberforgeUI.Button("Run", root, "RUN THIS TALENT", UITheme.FontSmall, ForgeButtonKind.Primary);
            RectTransform run = view.Run.Root;
            run.anchorMin = new Vector2(0f, 0f);
            run.anchorMax = new Vector2(1f, 0f);
            run.pivot = new Vector2(0.5f, 0f);
            run.offsetMin = new Vector2(Emberforge.OrnamentClearance + 8f, CardPad * 0.5f);
            run.offsetMax = new Vector2(-(Emberforge.OrnamentClearance + 8f), CardPad * 0.5f + ButtonHeight);
            int selection = view.Selection;
            view.Run.OnClick(delegate { Pick(selection); });
            return view;
        }

        /// <summary>
        /// THE FOURTH OPTION, in the same plate as the other three and selected the same way.
        ///
        /// A bar rather than a card because it has no numbers to print - that IS what it does -
        /// and a fourth column would have shrunk the three that do. It sits directly under them,
        /// in reading order, so the choice is read as four things and not as three things plus an
        /// escape hatch in a corner.
        /// </summary>
        private void BuildNoneBar()
        {
            _noneBar = UIBuilder.Node("NoTalentBar", Root);
            _noneBar.anchorMin = new Vector2(0f, 0f);
            _noneBar.anchorMax = new Vector2(1f, 0f);
            _noneBar.pivot = new Vector2(0.5f, 0f);
            _noneBar.offsetMin = new Vector2(UITheme.ScreenPadding, NoneBarBottom);
            _noneBar.offsetMax = new Vector2(-UITheme.ScreenPadding, NoneBarBottom + NoneBarHeight);

            _noneCard = new ForgeCard();
            _noneCard.Root = _noneBar;
            _noneCard.Plate = EmberforgeUI.PlainPanelOn(_noneBar);
            _noneCard.Plate.raycastTarget = false;

            _noneChip = EmberforgeUI.Chip("Numeral", _noneBar, "NONE", Emberforge.TextQuiet, UITheme.FontTiny - 2);
            UIBuilder.Box(_noneChip, CardPad, 0f, 110f, 34f, new Vector2(0f, 0.5f));

            Text title = EmberforgeUI.Title("Title", _noneBar, "NO TALENT", UITheme.FontBody,
                                            Emberforge.TextGold, TextAnchor.MiddleLeft);
            UIBuilder.Box(title.rectTransform, CardPad + 126f, 0f, 260f, 34f, new Vector2(0f, 0.5f));
            UIBuilder.Outlined(title, Emberforge.Outline, Emberforge.StrokeThin);

            _noneBody = EmberforgeUI.Body("Body", _noneBar, "", UITheme.FontTiny - 2, Emberforge.TextBody,
                                          TextAnchor.MiddleLeft);
            _noneBody.rectTransform.anchorMin = new Vector2(0f, 0f);
            _noneBody.rectTransform.anchorMax = new Vector2(1f, 1f);
            _noneBody.rectTransform.offsetMin = new Vector2(CardPad + 400f, 8f);
            _noneBody.rectTransform.offsetMax = new Vector2(-(CardPad + 300f), -8f);
            BestFit(_noneBody, UITheme.FontTiny - 7, UITheme.FontTiny - 2);
            UIBuilder.Outlined(_noneBody, Emberforge.Outline, Emberforge.StrokeThin);

            _noneRun = EmberforgeUI.Button("Run", _noneBar, "RUN NO TALENT", UITheme.FontSmall,
                                           ForgeButtonKind.Secondary);
            UIBuilder.Box(_noneRun.Root, -(CardPad * 0.5f), 0f, 260f, ButtonHeight, new Vector2(1f, 0.5f));
            _noneRun.OnClick(delegate { Pick(0); });
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

        /// <summary>The three facts that are true of every pick on the page, said once under all
        /// four options rather than on each of them: the rule the page is shaped by, where this
        /// hero's pick is seen, and how the reading was taken.</summary>
        private void BuildFooter()
        {
            RectTransform footer = UIBuilder.Node("Footer", Root);
            UIBuilder.BottomStrip(footer, NoneBarBottom - 6f, 0f, 0f);

            _footerRule = EmberforgeUI.Label("Rule", footer, "", UITheme.FontTiny - 2, Emberforge.TextGold,
                                             TextAnchor.LowerLeft);
            FooterLineAt(_footerRule.rectTransform, FooterLine * 2f + 2f);
            BestFit(_footerRule, UITheme.FontTiny - 7, UITheme.FontTiny - 2);

            _footerTravels = EmberforgeUI.Label("Travels", footer, "", UITheme.FontTiny - 4, Emberforge.TextBody,
                                                TextAnchor.LowerLeft);
            FooterLineAt(_footerTravels.rectTransform, FooterLine + 2f);
            BestFit(_footerTravels, UITheme.FontTiny - 8, UITheme.FontTiny - 4);

            _footerMeasure = EmberforgeUI.Label("Measure", footer, "", UITheme.FontTiny - 6, Emberforge.TextQuiet,
                                                TextAnchor.LowerLeft);
            FooterLineAt(_footerMeasure.rectTransform, 2f);
            BestFit(_footerMeasure, UITheme.FontTiny - 9, UITheme.FontTiny - 6);
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
            _noneBar.gameObject.SetActive(has);

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
            string body = "Talents belong to the thirty 5-star heroes, three to each. "
                        + (stars > 0 ? name + " is a " + stars + "-star, and that kit is complete as it stands." : "");
            body += fives > 0
                ? "\n\nPick one of your " + fives + (fives == 1 ? " 5-star" : " 5-stars") + " above to compare what theirs do."
                : "\n\nYou do not hold a 5-star yet. Every pull from the Rift is one.";
            _emptyBody.text = body;
            _emptyButton.Root.gameObject.SetActive(fives == 0);
        }

        /// <summary>
        /// The three cards, filled from the hero's own kit.
        ///
        /// Every block's height is decided here rather than at build time, because two of them are
        /// conditional: a cost banner exists on seven talents in the roster and the footnotes exist
        /// only where a figure needs one. Reserving room for all of them on every card would take
        /// the numbers list - the one block the page is for - down to three lines everywhere to
        /// spare four cards a scroll.
        /// </summary>
        private void FillCards(HeroInstance hero)
        {
            HeroDef def = hero.Def;
            RelicDef relic = string.IsNullOrEmpty(hero.EquippedRelicId) ? null : App.Session.Content.GetRelic(hero.EquippedRelicId);
            // The numbers are this hero's own, at its own level and with its own relic and armour,
            // exactly the stat line the hero page prints its ability numbers from.
            UnitStats stats = StatMath.ForHero(def, hero.Level, relic, App.Session.Profile.EquippedArmorOf(hero),
                                               App.Session.Content.Armor);

            // ONE BAND TOP FOR ALL THREE, decided by whether ANY of this hero's talents carries a
            // cost banner. The three cards are read across, and a rule that sat lower on one of
            // them would be the page quietly saying the cards are not the same kind of thing. A
            // card without a banner spends the difference on its sentence, which costs nothing.
            bool anyCostly = false;
            for (int i = 0; i < _cards.Count; i++)
                if (TalentAudit.For(def.Id, _cards[i].Selection) != null) anyCostly = true;
            float bandTop = anyCostly ? NumbersTopCostly : NumbersTopPlain;

            for (int i = 0; i < _cards.Count; i++)
            {
                CardView view = _cards[i];
                PassiveDef talent = def.TalentAt(view.Selection);
                view.Card.Root.gameObject.SetActive(talent != null);
                if (talent == null) continue;

                view.Name.text = talent.Name.ToUpperInvariant();

                // Read from the list, never named here: a talent that leaves the audit (Velis III
                // did) loses its banner with no change to this screen. The harness keeps the list
                // equal to its KnownCostlyTalents in CheckTalentAuditMatchesHarness.
                AuditedTalent audit = TalentAudit.For(def.Id, view.Selection);
                bool costly = audit != null;
                Color costTint = costly && audit.Trap ? UITheme.Danger : UITheme.Warning;
                view.CostChip.gameObject.SetActive(costly);
                view.CostNote.gameObject.SetActive(costly);
                if (costly)
                {
                    EmberforgeUI.SetChipTint(view.CostChip, costTint);
                    view.CostNote.text = audit.Note;
                    view.CostNote.color = costTint;
                }

                float proseTop = NameTop + NameHeight + BlockGap + (costly ? CostHeight + BlockGap : 0f);
                Stretch(view.Prose.rectTransform, proseTop,
                        bandTop - HeadingHeight - RuleGap - BlockGap - proseTop);
                view.Prose.text = TalentText.Prose(talent);
                view.Prose.color = view.Prose.text == TalentText.NoProse ? Emberforge.TextQuiet : Emberforge.TextBody;

                Stretch(view.Rule.rectTransform, bandTop - HeadingHeight - RuleGap, Emberforge.RuleHeight);
                Stretch(view.Heading, bandTop - HeadingHeight, HeadingHeight);

                for (int c = view.NumbersList.childCount - 1; c >= 0; c--)
                    Destroy(view.NumbersList.GetChild(c).gameObject);
                List<TalentNumber> numbers = TalentText.Numbers(def, talent, stats, App.Session.Balance);
                if (numbers.Count == 0)
                    AddNumberRow(view.NumbersList, TalentText.NoNumbers, Emberforge.TextQuiet);
                string heading = null;
                for (int n = 0; n < numbers.Count; n++)
                {
                    // One heading per run of lines about the same ability, so a talent that moves
                    // three things on Take Down the Wall names it once rather than three times.
                    if (numbers[n].Where != heading)
                    {
                        heading = numbers[n].Where;
                        AddNumberHeading(view.NumbersList, heading);
                    }
                    AddNumberRow(view.NumbersList, numbers[n].Figure, numbers[n].Tint);
                }

                // The two riders, said once each under the list and only where the list earns them.
                bool floor = TalentText.Any(numbers, TalentFigureBasis.Floor);
                bool clause = TalentText.Any(numbers, TalentFigureBasis.Clause);
                view.FloorNote.gameObject.SetActive(floor);
                view.ClauseNote.gameObject.SetActive(clause);

                float notes = (floor ? NoteRow : 0f) + (clause ? NoteRow : 0f);
                if (clause) Bottom(view.ClauseNote.rectTransform, CardBottomBase, NoteRow);
                if (floor) Bottom(view.FloorNote.rectTransform, CardBottomBase + (clause ? NoteRow : 0f), NoteRow);

                float bandBottom = CardBottomBase + notes + 6f;
                Bottom(view.RuleBottom.rectTransform, bandBottom, Emberforge.RuleHeight);

                RectTransform scrollRect = view.NumbersScroll.GetComponent<RectTransform>();
                scrollRect.anchorMin = new Vector2(0f, 0f);
                scrollRect.anchorMax = new Vector2(1f, 1f);
                scrollRect.offsetMin = new Vector2(CardPad, bandBottom + Emberforge.RuleHeight + 4f);
                scrollRect.offsetMax = new Vector2(-CardPad, -bandTop);
                Bottom(view.More.rectTransform, bandBottom + Emberforge.RuleHeight + 4f, 18f);
            }
        }

        /// <summary>The ability a run of numbers belongs to, in the kit's quiet voice.</summary>
        private void AddNumberHeading(RectTransform list, string where)
        {
            RectTransform row = UIBuilder.Node("Where", list);
            UIBuilder.Sized(row, 0f, HeadingHeight);
            Text w = EmberforgeUI.Label("Text", row, where.ToUpperInvariant(), UITheme.FontTiny - 6,
                                        Emberforge.TextQuiet, TextAnchor.LowerLeft);
            UIBuilder.FillParent(w.rectTransform, 0f);
            UIBuilder.Outlined(w, Emberforge.Outline, Emberforge.StrokeThin);
        }

        /// <summary>One figure, in the colour of what it is. Shrinks rather than wraps: the
        /// longest, "1,105 to 994 damage (210% to 189% of Attack)", is most of a card on its own.</summary>
        private void AddNumberRow(RectTransform list, string figure, Color tint)
        {
            RectTransform row = UIBuilder.Node("Number", list);
            UIBuilder.Sized(row, 0f, NumberRow);
            Text f = EmberforgeUI.Label("Figure", row, figure, UITheme.FontTiny - 1, tint, TextAnchor.MiddleLeft);
            UIBuilder.FillParent(f.rectTransform, 12f, 0f, 0f, 0f);
            BestFit(f, UITheme.FontTiny - 8, UITheme.FontTiny - 1);
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
                pick.text = hero.SelectedTalent > 0 ? Roman(hero.SelectedTalent) : "NONE";
                pick.color = hero.SelectedTalent > 0 ? Emberforge.TextGold : Emberforge.TextQuiet;
            }
        }

        // ------------------------------------------------------------------ state

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
        /// </summary>
        private void RefreshState()
        {
            if (_hero == null || _hero.Def == null)
            {
                _footerRule.text = "";
                _footerTravels.text = "";
                return;
            }
            int running = _hero.SelectedTalent;
            for (int i = 0; i < _cards.Count; i++)
            {
                CardView view = _cards[i];
                bool on = view.Selection == running;
                EmberforgeUI.SetCardSelected(view.Card, on);
                EmberforgeUI.SetChipTint(view.Numeral, on ? Emberforge.TextGold : Emberforge.TextQuiet);
                view.StateChip.gameObject.SetActive(on);
                view.Run.SetText(on ? "RUNNING" : "RUN THIS TALENT");
                view.Run.Interactable = !on;
            }

            bool none = running == 0;
            EmberforgeUI.SetCardSelected(_noneCard, none);
            EmberforgeUI.SetChipTint(_noneChip, none ? Emberforge.TextGold : Emberforge.TextQuiet);
            _noneRun.SetText(none ? "RUNNING" : "RUN NO TALENT");
            _noneRun.Interactable = !none;

            string name = _hero.Def.Name;
            // "the three" is read off the hero rather than written here: HeroDef.MaxTalents is the
            // ceiling and not a promise, and a hero authoring two would be told it has three.
            int count = _hero.Def.Talents.Count;
            _noneBody.text = name + " fights with none of " + (count == 1 ? "it" : "the " + Word(count))
                           + ". An unselected talent is not a quieter talent: it is not in the battle at all.";

            _footerRule.text = count > 0
                ? "One at a time, or none. Free to change, any time, as often as you like - nothing is"
                  + " spent, nothing is locked in, and no talent unlocks another."
                : "";
            bool deployed = App.Session.Profile.TeamSlotOf(_hero.Uid) >= 0;
            _footerTravels.text = _hero.Def.Talents.Count == 0 ? ""
                : deployed
                ? name + " is in your line, so this pick is part of what other players meet: friends who duel you face it from your next save, and your next Proving ghost carries it."
                : name + " is on the bench. A pick only reaches the line friends duel, and your Proving ghost, once " + name + " stands in it.";
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

        private void RefreshReadings()
        {
            if (_trial == null)
            {
                _footerMeasure.text = _hero != null && _hero.Def != null && _hero.Def.Talents.Count > 0
                    ? "No reading: there is no line to measure this hero in."
                    : "";
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

            string where = _trial.StandIn != null
                ? _hero.Def.Name + " standing in for " + _trial.StandIn.Def.Name + " at the back of your line"
                : "your line as it stands";
            if (!_trial.Finished)
            {
                _footerMeasure.text = "Measuring: " + where + ", against a mirror of itself with nothing picked. "
                                    + _trial.FightsRun + " of " + _trial.FightsTotal + " fights.";
                return;
            }

            _footerMeasure.text = "Measured: " + where + ", " + _trial.Seeds + " fights per talent against a mirror"
                                + " of itself with nothing picked, " + Seconds(_trial.SimulationMs) + " on this device."
                                + (_trial.Failures > 0 ? " " + _trial.Failures + " fights failed." : "");
            if (!_trialLogged)
            {
                _trialLogged = true;
                Debug.Log("TalentsScreen: measured " + _hero.Def.Name + " - " + _trial.FightsTotal + " fights, "
                          + _trial.SimulationMs.ToString("0", CultureInfo.InvariantCulture) + " ms of simulation over "
                          + _trialFrames + " frames" + (_trial.StandIn != null ? " (bench, stood in for " + _trial.StandIn.Def.Name + ")" : "")
                          + ".");
                if (_trial.Failures > 0)
                    Debug.LogError("TalentsScreen: " + _trial.Failures + " trial fights threw. Last: " + _trial.LastError);
            }
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
                    view.Measured.color = Emberforge.TextQuiet;
                    view.Measured.text = "In your line: no clear change in " + _hero.Def.Name + "'s own output"
                                       + tail;
                    break;
            }
        }

        // ------------------------------------------------------------------ small

        /// <summary>A small count in words, for a sentence. Only ever reaches three.</summary>
        private static string Word(int n)
        {
            return n == 2 ? "two" : n == 3 ? "three" : n.ToString(CultureInfo.InvariantCulture);
        }

        private static string Roman(int n)
        {
            return n == 1 ? "I" : n == 2 ? "II" : n == 3 ? "III" : n.ToString(CultureInfo.InvariantCulture);
        }

        private static string Seconds(double ms)
        {
            return ms < 1000.0
                ? ms.ToString("0", CultureInfo.InvariantCulture) + " ms"
                : (ms / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " s";
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

        /// <summary>One footer line, full page width, stood at a height above the page's bottom.</summary>
        private static void FooterLineAt(RectTransform rt, float bottom)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.offsetMin = new Vector2(UITheme.ScreenPadding, bottom);
            rt.offsetMax = new Vector2(-UITheme.ScreenPadding, bottom + FooterLine);
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
