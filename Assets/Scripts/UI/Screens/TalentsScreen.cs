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
    /// THE TALENT PICK, for one hero, as a decision rather than a dropdown.
    ///
    /// ALL THREE AT ONCE, IN FULL. The comparison is the whole point of the page, so the three
    /// talents stand side by side at the same size and nothing about one is a tap further away than
    /// the same thing about another. Each card carries three things, top to bottom:
    ///
    ///   WHAT IT SAYS. The authored sentence, which by house rule quotes no number.
    ///   WHAT IT CHANGES, IN NUMBERS. Durations, sizes, thresholds, Energy and damage, printed from
    ///     the effect data through TalentMath and TalentText - before and after, where a talent
    ///     moves something the kit already had. This is the only place in the game those numbers
    ///     appear at all.
    ///   WHAT IT DID TO THIS HERO. Measured, not claimed: TalentTrial fights the player's own line
    ///     against a frozen mirror of itself, forty seeds per talent and forty with nothing picked,
    ///     and the card prints the paired change in this hero's own output and the wins beside it.
    ///     A talent that makes its hero worse says so, in red, and a talent the audit found to be a
    ///     trap says that too even when this line's reading is quiet.
    ///
    /// THE PICK COSTS NOTHING, AND THE PAGE SAYS SO. GameSession.SelectTalent is free, instant and
    /// reversible, and a choice that looks weighty but costs nothing to change is the choice a
    /// player should be encouraged to try both ways. The footer says it in so many words.
    ///
    /// IT TRAVELS, AND THE PAGE SAYS THAT TOO. A deployed hero's pick is part of the line other
    /// players meet - the snapshot a friend duels is published on the next save, and the next
    /// Proving ghost is drawn with it.
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
        //   966 short canvas - 252 header - 92 hero strip - 8 - 100 footer = 514 of card.
        //
        // Inside a card the TOP block (name, sentence) and the BOTTOM block (reading, button) are
        // fixed, and the numbers list between them takes what is left - 123 units at 20:9, which
        // is a heading and four figures - and scrolls past it. The longest list in the roster,
        // Talren's third, scrolls at 20:9 and fits at 16:9.
        private const float ShortestCanvasHeight = 966f;
        private const float HeaderBottom = 252f;
        private const float StripHeight = 92f;
        private const float StripGap = 8f;
        private const float FooterHeight = 100f;
        private const float CardsTop = HeaderBottom + StripHeight + StripGap;
        private const float CardGutter = 24f;

        private const float CardPad = 30f;
        private const float NameTop = 22f;
        private const float NameHeight = 40f;
        private const float ProseTop = NameTop + NameHeight + 6f;
        private const float ProseHeight = 90f;
        private const float NumbersTop = ProseTop + ProseHeight + 10f;
        private const float NumberRow = 24f;
        private const float NumberHeading = 20f;

        private const float ButtonHeight = 64f;
        private const float ReadingHeight = 124f;
        private const float BottomBlock = CardPad * 0.5f + ButtonHeight + 10f + ReadingHeight;

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

        private Text _footerFree;
        private Text _footerTravels;
        private Text _footerMeasure;
        private UIButton _runNone;

        /// <summary>What one talent's card needs to repaint without being rebuilt.</summary>
        private sealed class CardView
        {
            public int Selection;
            public ForgeCard Card;
            public Text Numeral;
            public Text Name;
            public RectTransform StateChip;
            public Text Prose;
            public RectTransform NumbersList;
            public Text Verdict;
            public Text Detail;
            public Text Wins;
            public Text Audit;
            public Text More;
            public ScrollRect NumbersScroll;
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
            BuildFooter();

            // Says out loud when a card has outgrown the shortest canvas, for the reason
            // DesignGallery.CheckFits exists: the failure is silent at 16:9.
            float card = ShortestCanvasHeight - CardsTop - FooterHeight;
            float needed = NumbersTop + NumberHeading + NumberRow * 2f + BottomBlock;
            if (needed > card)
                Debug.LogError("TalentsScreen: a card needs " + needed + " units for two lines of numbers"
                               + " and the shortest canvas leaves " + card + ".");
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
            _cardsHost.offsetMin = new Vector2(UITheme.ScreenPadding, FooterHeight);
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
            RectTransform numeral = EmberforgeUI.Chip("Numeral", root, Roman(index + 1), Emberforge.TextGold,
                                                      UITheme.FontSmall);
            UIBuilder.Box(numeral, CardPad, -NameTop, 58f, NameHeight, new Vector2(0f, 1f));
            view.Numeral = numeral.GetComponentInChildren<Text>();

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

            view.Prose = EmberforgeUI.Body("Prose", root, "", UITheme.FontTiny, Emberforge.TextBody,
                                           TextAnchor.UpperLeft);
            Stretch(view.Prose.rectTransform, ProseTop, ProseHeight);
            view.Prose.resizeTextForBestFit = true;
            view.Prose.resizeTextMinSize = UITheme.FontTiny - 6;
            view.Prose.resizeTextMaxSize = UITheme.FontTiny;
            UIBuilder.Outlined(view.Prose, Emberforge.Outline, Emberforge.StrokeThin);

            Image ruleTop = EmberforgeUI.Rule("RuleTop", root);
            Stretch(ruleTop.rectTransform, NumbersTop - 8f, Emberforge.RuleHeight);

            // The numbers list: between the sentence and the reading, scrolling past its room.
            ScrollRect scroll;
            view.NumbersList = UIBuilder.ScrollList("Numbers", root, out scroll, 2f, new RectOffset(0, 0, 2, 2));
            view.NumbersScroll = scroll;
            RectTransform scrollRect = scroll.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(CardPad, BottomBlock + 10f);
            scrollRect.offsetMax = new Vector2(-CardPad, -NumbersTop);

            // Says there is more below when the list outruns its room, which only the longest lists
            // do and only at 20:9. A list cut at its fourth line reads as a list of four.
            view.More = EmberforgeUI.Label("More", root, "MORE BELOW", UITheme.FontTiny - 8, Emberforge.TextGold,
                                           TextAnchor.LowerRight);
            Bottom(view.More.rectTransform, BottomBlock + 8f, 18f);
            UIBuilder.Outlined(view.More, Emberforge.Outline, Emberforge.StrokeThin);
            view.More.gameObject.SetActive(false);

            Image ruleBottom = EmberforgeUI.Rule("RuleBottom", root);
            RectTransform rb = ruleBottom.rectTransform;
            rb.anchorMin = new Vector2(0f, 0f);
            rb.anchorMax = new Vector2(1f, 0f);
            rb.pivot = new Vector2(0.5f, 0f);
            rb.offsetMin = new Vector2(CardPad, BottomBlock);
            rb.offsetMax = new Vector2(-CardPad, BottomBlock + Emberforge.RuleHeight);

            // The reading, bottom-anchored so it sits in the same place on all three cards however
            // long each one's sentence and list run: the three verdicts read across as one row.
            float readingBase = CardPad * 0.5f + ButtonHeight + 10f;
            view.Verdict = EmberforgeUI.Title("Verdict", root, "", UITheme.FontBody, Emberforge.TextQuiet,
                                              TextAnchor.LowerLeft);
            Bottom(view.Verdict.rectTransform, readingBase + 84f, 36f);
            UIBuilder.Outlined(view.Verdict, Emberforge.Outline, Emberforge.StrokeThin);

            view.Detail = EmberforgeUI.Label("Detail", root, "", UITheme.FontTiny - 2, Emberforge.TextBody,
                                             TextAnchor.LowerLeft);
            Bottom(view.Detail.rectTransform, readingBase + 58f, 24f);
            UIBuilder.Outlined(view.Detail, Emberforge.Outline, Emberforge.StrokeThin);

            view.Wins = EmberforgeUI.Label("Wins", root, "", UITheme.FontTiny - 2, Emberforge.TextQuiet,
                                           TextAnchor.LowerLeft);
            Bottom(view.Wins.rectTransform, readingBase + 34f, 24f);
            UIBuilder.Outlined(view.Wins, Emberforge.Outline, Emberforge.StrokeThin);

            view.Audit = EmberforgeUI.Body("Audit", root, "", UITheme.FontTiny - 5, UITheme.Danger,
                                           TextAnchor.LowerLeft);
            Bottom(view.Audit.rectTransform, readingBase, 34f);
            view.Audit.resizeTextForBestFit = true;
            view.Audit.resizeTextMinSize = UITheme.FontTiny - 8;
            view.Audit.resizeTextMaxSize = UITheme.FontTiny - 3;
            UIBuilder.Outlined(view.Audit, Emberforge.Outline, Emberforge.StrokeThin);

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

        /// <summary>The two facts about a pick that are true of every talent on the page, said once
        /// under all three rather than on each card, and the line saying how the reading was made.</summary>
        private void BuildFooter()
        {
            RectTransform footer = UIBuilder.Node("Footer", Root);
            UIBuilder.BottomStrip(footer, FooterHeight, 0f, 0f);
            Image recess = EmberforgeUI.SunkenOn(footer);
            recess.raycastTarget = false;

            float right = 320f;
            _footerFree = EmberforgeUI.Label("Free", footer, "", UITheme.FontTiny, Emberforge.TextGold,
                                             TextAnchor.MiddleLeft);
            FooterLine(_footerFree.rectTransform, 8f, right);

            _footerTravels = EmberforgeUI.Label("Travels", footer, "", UITheme.FontTiny - 2, Emberforge.TextBody,
                                                TextAnchor.MiddleLeft);
            FooterLine(_footerTravels.rectTransform, 38f, right);
            BestFit(_footerTravels, UITheme.FontTiny - 8, UITheme.FontTiny - 2);

            _footerMeasure = EmberforgeUI.Label("Measure", footer, "", UITheme.FontTiny - 4, Emberforge.TextQuiet,
                                                TextAnchor.MiddleLeft);
            FooterLine(_footerMeasure.rectTransform, 66f, right);
            BestFit(_footerMeasure, UITheme.FontTiny - 8, UITheme.FontTiny - 4);

            _runNone = EmberforgeUI.Button("RunNone", footer, "RUN NO TALENT", UITheme.FontSmall,
                                           ForgeButtonKind.Secondary);
            UIBuilder.Box(_runNone.Root, -UITheme.ScreenPadding, 0f, 280f, 68f, new Vector2(1f, 0.5f));
            _runNone.OnClick(delegate { Pick(0); });
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
            _runNone.Root.gameObject.SetActive(has);

            if (hero == null || hero.Def == null)
            {
                _headerSubtitle.text = "";
                ShowNoTalents(null);
            }
            else
            {
                _headerSubtitle.text = hero.Def.Name.ToUpperInvariant() + "   -   "
                                     + (hero.Def.Title.Length > 0 ? hero.Def.Title.ToUpperInvariant() + "   -   " : "")
                                     + hero.Def.Stars + "-STAR";
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

        private void FillCards(HeroInstance hero)
        {
            HeroDef def = hero.Def;
            RelicDef relic = string.IsNullOrEmpty(hero.EquippedRelicId) ? null : App.Session.Content.GetRelic(hero.EquippedRelicId);
            // The numbers are this hero's own, at its own level and with its own relic and armour,
            // exactly the stat line the hero page prints its ability numbers from.
            UnitStats stats = StatMath.ForHero(def, hero.Level, relic, App.Session.Profile.EquippedArmorOf(hero),
                                               App.Session.Content.Armor);

            for (int i = 0; i < _cards.Count; i++)
            {
                CardView view = _cards[i];
                PassiveDef talent = def.TalentAt(view.Selection);
                view.Card.Root.gameObject.SetActive(talent != null);
                if (talent == null) continue;

                view.Name.text = talent.Name.ToUpperInvariant();
                view.Prose.text = TalentText.Prose(talent);
                view.Prose.color = view.Prose.text == TalentText.NoProse ? Emberforge.TextQuiet : Emberforge.TextBody;

                for (int c = view.NumbersList.childCount - 1; c >= 0; c--)
                    Destroy(view.NumbersList.GetChild(c).gameObject);
                List<TalentNumber> numbers = TalentText.Numbers(def, talent, stats, App.Session.Balance);
                if (numbers.Count == 0)
                    AddNumberRow(view.NumbersList, "No numbers of its own. The sentence is all of it.", Emberforge.TextQuiet);
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

                AuditedTalent audit = TalentAudit.For(def.Id, view.Selection);
                view.Audit.text = audit != null ? audit.Note : "";
                view.Audit.color = audit != null && audit.Trap ? UITheme.Danger : UITheme.Warning;
            }
        }

        /// <summary>The ability a run of numbers belongs to, in the kit's quiet voice.</summary>
        private void AddNumberHeading(RectTransform list, string where)
        {
            RectTransform row = UIBuilder.Node("Where", list);
            UIBuilder.Sized(row, 0f, NumberHeading);
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
            f.resizeTextForBestFit = true;
            f.resizeTextMinSize = UITheme.FontTiny - 8;
            f.resizeTextMaxSize = UITheme.FontTiny - 1;
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

        /// <summary>Everything that moves when the pick moves: which card is framed, which button
        /// is live, and the two footer lines that depend on this hero.</summary>
        private void RefreshState()
        {
            if (_hero == null || _hero.Def == null)
            {
                _footerFree.text = "";
                _footerTravels.text = "";
                return;
            }
            int running = _hero.SelectedTalent;
            for (int i = 0; i < _cards.Count; i++)
            {
                CardView view = _cards[i];
                bool on = view.Selection == running;
                EmberforgeUI.SetCardSelected(view.Card, on);
                view.StateChip.gameObject.SetActive(on);
                view.Run.SetText(on ? "RUNNING" : "RUN THIS TALENT");
                view.Run.Interactable = !on;
            }
            _runNone.Interactable = running != 0;

            string name = _hero.Def.Name;
            _footerFree.text = _hero.Def.Talents.Count > 0
                ? "Free to change, any time, as often as you like. Nothing is spent and nothing is locked in."
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
                for (int i = 0; i < _cards.Count; i++) ClearReading(_cards[i]);
                return;
            }

            TalentReading bare = _trial.Readings[0];
            for (int i = 0; i < _cards.Count; i++)
            {
                CardView view = _cards[i];
                if (view.Selection >= _trial.Readings.Length) { ClearReading(view); continue; }
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

        private static void ClearReading(CardView view)
        {
            view.Verdict.text = "";
            view.Detail.text = "";
            view.Wins.text = "";
        }

        /// <summary>
        /// One talent's reading, in three lines: the verdict, the change in this hero's output,
        /// and the wins. The verdict is the only line in colour, and it only goes green or red
        /// when the change is clear of the noise - the harness's own two-standard-error line.
        /// </summary>
        private void ShowReading(CardView view, TalentReading r, TalentReading bare)
        {
            if (r.Fights < 2)
            {
                view.Verdict.text = "MEASURING";
                view.Verdict.color = Emberforge.TextQuiet;
                view.Detail.text = "";
                view.Wins.text = "";
                return;
            }

            long delta = (long)System.Math.Round(r.Delta);
            float percent = bare.MeanOutput > 0 ? (float)(r.Delta / bare.MeanOutput) * 100f : 0f;
            string signed = (delta >= 0 ? "+" : "") + AbilityText.Amount((int)delta);
            string pct = (percent >= 0f ? "+" : "") + percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
            string pronoun = _hero.Def.Name;

            switch (r.Verdict)
            {
                case TalentVerdict.Gains:
                    view.Verdict.text = "GAINS " + pct;
                    view.Verdict.color = UITheme.Success;
                    break;
                case TalentVerdict.Loses:
                    view.Verdict.text = "COSTS " + pct.TrimStart('-');
                    view.Verdict.color = UITheme.Danger;
                    break;
                case TalentVerdict.Unreached:
                    view.Verdict.text = "NEVER SET OFF HERE";
                    view.Verdict.color = Emberforge.TextQuiet;
                    break;
                default:
                    view.Verdict.text = "NO CLEAR CHANGE";
                    view.Verdict.color = Emberforge.TextBody;
                    break;
            }
            if (!_trial.Finished) view.Verdict.text += "  ...";

            view.Detail.text = r.Verdict == TalentVerdict.Unreached
                ? "Nothing it needs happened in " + r.Fights + " fights of your line."
                : pronoun + "'s own output " + AbilityText.Amount((int)bare.MeanOutput) + " » "
                  + AbilityText.Amount((int)r.MeanOutput) + " a fight (" + signed + ")";
            view.Wins.text = "Beat the mirror " + r.Wins + " of " + r.Fights + ", against "
                           + bare.Wins + " with no talent";
        }

        // ------------------------------------------------------------------ small

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

        private static void FooterLine(RectTransform rt, float top, float right)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(UITheme.ScreenPadding, -(top + 28f));
            rt.offsetMax = new Vector2(-(UITheme.ScreenPadding + right), -top);
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
