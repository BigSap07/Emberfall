using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// THE FUSION PAGE: the one door onto both halves of DESIGN-FUSION's ladder.
    ///
    /// Until this screen existed GameSession.Ascend and GameSession.Convert had no caller outside
    /// the harness and the capture tool, while two screens already told the player to use them -
    /// the hero page's cap note on a maxed five-star, and the talent page's ASCEND TO TIER N on a
    /// locked card. Both now lead here.
    ///
    /// TWO TABS, ONE LADDER. Fusion's output is ascension's input (DESIGN-FUSION section 1), so
    /// they are two faces of one page rather than two pages: the CONVERT tab manufactures
    /// five-stars and the ASCEND tab eats the duplicates that manufacturing produces. A player who
    /// has just been told "you need four more copies of this hero" is one tap from the thing that
    /// makes copies, and the tab bar is what says the two are the same economy.
    ///
    /// WHAT THIS PAGE HAS TO EXPRESS, over and above what it has to do:
    ///
    ///   THE ROLL IS A ROLL (F3), and it is said BEFORE the spend, not after. Conversion's output
    ///     is random within the aspect and never chosen, because that randomness is what
    ///     manufactures the duplicates ascension eats. So the whole pool is drawn - every five-star
    ///     the conversion could produce, face by face, at the top of the tab, above the fodder and
    ///     above the button - with the count said in words next to it. A player who reads one line
    ///     of this page reads that one. It is not a confirmation dialog, because a warning shown
    ///     at the moment of committing is a warning shown to somebody who has already decided.
    ///
    ///   THE SEED RULE IS A RULE, NOT AN ERROR (F9). Umbra and Lumen take one hero of their own
    ///     aspect plus generic fodder; the other four take fodder of their aspect throughout. Both
    ///     shapes are printed in the same RECIPE block in the same two lines whichever aspect is
    ///     chosen, so the difference reads as two recipes rather than as one recipe and a
    ///     complaint. The block also prints the roster count the rule is made of - four low-star
    ///     heroes against fourteen - because a rule with its reason next to it is a rule and a
    ///     rule without one is an obstacle.
    ///
    ///   THE TIER LEVEL RESETS AND THE LEVEL DOES NOT (F6). This is the one number on the page
    ///     that reads as a bug if it is met without warning, and the warning is three rows of
    ///     before and after printed above the button: the tier level falls to 1, the Level rises,
    ///     the cap over the tier level rises further. All three come from
    ///     GameSession.PreviewAscension, which shares its arithmetic with Ascend itself, so the
    ///     page cannot promise a landing the ascension does not make.
    ///
    ///     ALL THREE ROWS ARE READ AGAINST THE LIST BESIDE THEM, which is what the numbers pass
    ///     fixed. The hero list prints "TIER 0 - LEVEL 9 / 9" from TierLevelOf and HeroLevelCap,
    ///     both counted in tier levels, so the card's cap row is counted the same way: a tier-0
    ///     hero at the top of its tier reads 9 / 9 in the list and 9 -> 1 with a cap of 9 -> 18
    ///     on the card, one hero in one unit. The row that used to sit there answered in Levels
    ///     and printed 9 -> 45, which was the true absolute cap and still a second unit on a page
    ///     that had not warned the player there was one.
    ///
    ///     THE ROWS CARRY THE MEANING AND THE PROSE DOES NOT REPEAT THEM. The sentence under them
    ///     used to restate the reset, the rise and both cap figures, all four of which are rows a
    ///     player can read. What a table cannot say is what the reset means, so that is the whole
    ///     of what is left: the tier level starts again, the power does not.
    ///
    ///   A FIVE-STAR IS NEVER FODDER (F5) and heroes below five stars never ascend (F1, F2). Both
    ///     are structural here rather than enforced: the ascend tab's list holds five-stars only,
    ///     the convert tab's fodder grid holds everything below five stars only, and neither can
    ///     offer the illegal move in the first place.
    ///
    /// NO GAME LOGIC HERE. GameSession.ConversionBlocker and GameSession.AscensionBlocker are the
    /// rules, GameSession.PreviewAscension and GameSession.ConversionPool are the numbers, and
    /// this page draws what they return. Every refusal printed on it is a sentence one of those
    /// two blockers wrote; this file never decides that a move is illegal, it only asks.
    /// </summary>
    public sealed class FusionScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Fusion; } }

        // ------------------------------------------------------------------ metrics
        //
        // Measured against the SHORTEST canvas this game targets, the way the talent page is:
        //
        //   966 short canvas - 252 header - 72 tabs - 128 action bar = 514 of body.
        //
        // Everything above the fodder grid on the convert tab is fixed, so the grid is what
        // absorbs the difference between that and the 628 a 16:9 canvas leaves. Build asserts a
        // floor under it rather than trusting the arithmetic above.
        private const float ShortestCanvasHeight = 966f;
        private const float HeaderBottom = 252f;
        private const float TabRowHeight = 62f;
        private const float TabRowGap = 10f;
        private const float BodyTop = HeaderBottom + TabRowHeight + TabRowGap;
        private const float ActionBarHeight = 128f;

        private const float ColumnGap = 16f;
        private const float AscendListWidth = 480f;
        private const float AspectListWidth = 400f;

        private const float HeroRowHeight = 104f;
        private const float AspectRowHeight = 78f;
        private const float RollPortrait = 76f;
        private const float FodderCellWidth = 152f;
        private const float FodderCellHeight = 150f;

        private const float BlockGap = 8f;
        private const float HeadingHeight = 22f;

        // The convert tab's fixed blocks, each the sum of what it draws rather than a round
        // number: the roll is a heading, a row of faces and its sentence; the recipe is a heading
        // and its two lines. Build() adds them up against the shortest canvas and says so if the
        // grid underneath has been squeezed below a row.
        private const float RollBlockHeight = HeadingHeight + UITheme.Space4 + RollPortrait + 16f
                                            + UITheme.Space4 + 40f;
        private const float RecipeBlockHeight = HeadingHeight + UITheme.Space4 + 30f + 32f;

        /// <summary>Everything above the fodder grid on the convert tab: the roll, the recipe,
        /// the two rules between them and the grid's own heading. What BuildConvertPage walks
        /// down, written once so the assertion in Build measures the layout rather than a copy
        /// of it.</summary>
        private const float ConvertFixedHeight = RollBlockHeight + BlockGap
                                               + Emberforge.RuleHeight + BlockGap
                                               + RecipeBlockHeight + BlockGap
                                               + Emberforge.RuleHeight + BlockGap
                                               + HeadingHeight + UITheme.Space4;

        /// <summary>The least fodder grid worth drawing: one row of cells and a hint of the next,
        /// so a grid that scrolls looks like one.</summary>
        private const float FodderGridFloor = FodderCellHeight + 40f;

        private const float CardPad = 24f;

        // The ascend card, top to bottom. Named rather than inlined for the reason the convert
        // tab's blocks are: Build() adds them up against the shortest canvas, and a stack of
        // literals is a stack nothing can check.
        private const float PortraitSize = 88f;
        private const float CostBlockHeight = 32f + 30f;
        private const float MoveRowHeight = 32f;
        private const float ResetNoteHeight = 30f;
        private const float OpensNoteHeight = 54f;

        /// <summary>
        /// The card's whole stack, and since this pass it is also the card's HEIGHT rather than
        /// only an assertion about it. The plate used to fill the column top to bottom, so every
        /// canvas taller than the stack drew an empty panel under the talent line - furniture
        /// with nothing in it, which reads as a block that failed to load rather than as a card
        /// that finished. The plate is now this tall and the column below it is simply ground.
        ///
        /// The trailing CardPad is the frame's own breathing room, the same pad the stack opens
        /// with: "ends where its content ends" is the last sentence plus the border it sits in,
        /// not the last sentence pressed against the edge.
        /// </summary>
        private const float AscendCardHeight = CardPad + PortraitSize + BlockGap
                                             + Emberforge.RuleHeight + BlockGap
                                             + CostBlockHeight + BlockGap
                                             + Emberforge.RuleHeight + BlockGap
                                             + HeadingHeight + UITheme.Space4
                                             + MoveRowHeight * 3f + BlockGap
                                             + ResetNoteHeight + BlockGap
                                             + Emberforge.RuleHeight + UITheme.Space4
                                             + OpensNoteHeight + CardPad;

        // ------------------------------------------------------------------ state

        /// <summary>Which half of the ladder is showing. Ascension is the default because every
        /// door into this page arrives from a hero that is asking to ascend.</summary>
        private bool _convertTab;

        private HeroInstance _subject;
        private Aspect _target = Aspect.Ember;

        /// <summary>The bodies the player has offered, one entry per BODY: a hero listed twice
        /// gives two of its copies, exactly as GameSession.ConversionBlocker reads it. Kept as the
        /// same flat list the session takes rather than as a count table, so the thing the screen
        /// shows and the thing the session is handed cannot drift apart.</summary>
        private readonly List<HeroInstance> _bodies = new List<HeroInstance>();

        private Text _headerSubtitle;
        private ForgeTab _ascendTab;
        private ForgeTab _convertTabButton;

        // ascend side
        private RectTransform _ascendPage;
        private RectTransform _heroList;
        private readonly List<ForgeCard> _heroCards = new List<ForgeCard>();
        private readonly List<HeroInstance> _heroRows = new List<HeroInstance>();
        private RectTransform _ascendCard;
        private RectTransform _ascendCardBody;
        private RectTransform _ascendPortrait;
        private Text _ascendName;
        private RectTransform _ascendTierChip;
        private Text _ascendStep;
        private RectTransform _ascendCostChip;
        private Text _ascendCostNote;
        private readonly List<Text[]> _moveRows = new List<Text[]>();
        private Text _resetNote;
        private Text _opensNote;
        private Text _ascendEmpty;

        // convert side
        private RectTransform _convertPage;
        private RectTransform _aspectList;
        private readonly List<ForgeCard> _aspectCards = new List<ForgeCard>();
        private RectTransform _rollRow;
        private Text _rollNote;
        private Text _recipeSeed;
        private Text _recipeFodder;
        private RectTransform _fodderGrid;
        private Text _fodderEmpty;

        // the action bar, shared
        private Text _statusNote;
        private RectTransform _pointsRoot;
        private Image _pointsFill;
        private Text _pointsLabel;
        private UIButton _clearButton;
        private UIButton _actButton;

        // ------------------------------------------------------------------ doors in

        /// <summary>
        /// Opens the page on one hero's ascension - the door the hero page's cap note and the
        /// talent page's locked card both come through.
        ///
        /// A hero below five stars opens the CONVERT tab on its own aspect instead, because that
        /// is the only half of the ladder it can be on (F1): a four-star cannot ascend, and the
        /// page that told it so should land on the thing it can do rather than on a dead card.
        /// </summary>
        public void ShowHero(HeroInstance hero)
        {
            _subject = hero;
            if (hero != null && hero.Def != null && hero.Def.Stars < 5)
            {
                _convertTab = true;
                _target = hero.Def.Aspect;
                _subject = null;
            }
            else
            {
                _convertTab = false;
            }
            _bodies.Clear();
        }

        // ------------------------------------------------------------------ build

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            ScreenChrome.ForgedHeader(Root, "FUSION", Router, out _headerSubtitle);
            BuildTabs();
            BuildAscendPage();
            BuildConvertPage();
            BuildActionBar();

            // Says out loud when either tab has outgrown the shortest canvas, for the reason
            // TalentsScreen's own assertion exists: the failure is invisible at 16:9, where the
            // body is 114 units taller, and only shows on the phone shape the game is played in.
            //
            // Two things are checked because the two tabs fail differently. The ascend card is a
            // fixed stack and simply runs off the bottom; the convert tab absorbs the difference
            // in its fodder grid, which fails by shrinking to nothing rather than by overflowing.
            float body = ShortestCanvasHeight - BodyTop - ActionBarHeight;
            if (AscendCardHeight > body)
                Debug.LogError("FusionScreen: the ascend card needs " + AscendCardHeight
                               + " units and the shortest canvas leaves " + body + ".");

            float grid = body - ConvertFixedHeight;
            if (grid < FodderGridFloor)
                Debug.LogError("FusionScreen: the fodder grid has " + grid + " units on the shortest"
                               + " canvas and one row of cells needs " + FodderGridFloor + ".");
        }

        private void BuildTabs()
        {
            RectTransform row = UIBuilder.Node("Tabs", Root);
            UIBuilder.TopStrip(row, TabRowHeight, HeaderBottom, UITheme.ScreenPadding);

            _ascendTab = EmberforgeUI.Tab("ASCEND", row, 360f, TabRowHeight, delegate { SetTab(false); });
            UIBuilder.Box(_ascendTab.Root, 0f, 0f, 360f, TabRowHeight, new Vector2(0f, 0.5f));

            _convertTabButton = EmberforgeUI.Tab("CONVERT", row, 360f, TabRowHeight,
                                                 delegate { SetTab(true); });
            UIBuilder.Box(_convertTabButton.Root, 360f + UITheme.Space8, 0f, 360f, TabRowHeight,
                          new Vector2(0f, 0.5f));
        }

        /// <summary>The body rect both tabs draw into: between the tab row and the action bar.</summary>
        private RectTransform BodyHost(string name)
        {
            RectTransform host = UIBuilder.Node(name, Root);
            host.anchorMin = new Vector2(0f, 0f);
            host.anchorMax = new Vector2(1f, 1f);
            host.offsetMin = new Vector2(UITheme.ScreenPadding, ActionBarHeight);
            host.offsetMax = new Vector2(-UITheme.ScreenPadding, -BodyTop);
            return host;
        }

        // ------------------------------------------------------------------ the ascend tab

        private void BuildAscendPage()
        {
            _ascendPage = BodyHost("Ascend");

            // THE PLAYER'S FIVE-STARS, and only those. F2: nothing below five stars ascends, so a
            // list that offered a four-star would be offering a move the rule refuses - the list
            // is the rule, rather than a gate in front of it.
            RectTransform well = UIBuilder.Node("Well", _ascendPage);
            well.anchorMin = new Vector2(0f, 0f);
            well.anchorMax = new Vector2(0f, 1f);
            well.pivot = new Vector2(0f, 0.5f);
            well.sizeDelta = new Vector2(AscendListWidth, 0f);
            well.anchoredPosition = Vector2.zero;
            EmberforgeUI.SunkenOn(well).raycastTarget = true;

            ScrollRect scroll;
            _heroList = UIBuilder.ScrollList("Heroes", well, out scroll, UITheme.Space8,
                                             new RectOffset(10, 10, 10, 10));
            UIBuilder.FillParent((RectTransform)scroll.transform, 4f);

            // TOP-ANCHORED AND EXACTLY AS TALL AS ITS STACK. See AscendCardHeight: a plate that
            // filled the column left an empty panel under the talent line on every canvas taller
            // than the content, and an empty panel is not a neutral gap - it is a piece of
            // furniture announcing that something belongs in it.
            _ascendCard = UIBuilder.Node("Card", _ascendPage);
            _ascendCard.anchorMin = new Vector2(0f, 1f);
            _ascendCard.anchorMax = new Vector2(1f, 1f);
            _ascendCard.pivot = new Vector2(0.5f, 1f);
            _ascendCard.offsetMin = new Vector2(AscendListWidth + ColumnGap, -AscendCardHeight);
            _ascendCard.offsetMax = Vector2.zero;
            EmberforgeUI.PanelOn(_ascendCard, true).raycastTarget = false;

            // Everything the card draws hangs in here rather than off the plate, so the empty
            // state can hide the CONTENT without also hiding the panel's own frame and ornaments.
            _ascendCardBody = UIBuilder.Node("Body", _ascendCard);
            UIBuilder.FillParent(_ascendCardBody, 0f);

            BuildAscendCard();

            _ascendEmpty = EmberforgeUI.Body("Empty", _ascendCard, "", UITheme.FontBody,
                                             Emberforge.TextQuiet, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(_ascendEmpty.rectTransform, 80f);
            UIBuilder.Outlined(_ascendEmpty, Emberforge.Outline, Emberforge.StrokeThin);
        }

        private void BuildAscendCard()
        {
            float top = CardPad;

            _ascendPortrait = UIBuilder.Node("PortraitHost", _ascendCardBody);
            UIBuilder.Box(_ascendPortrait, CardPad, -top, PortraitSize, PortraitSize, new Vector2(0f, 1f));

            _ascendName = EmberforgeUI.Title("Name", _ascendCardBody, "", UITheme.FontTitle,
                                             Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(_ascendName.rectTransform, CardPad + PortraitSize + UITheme.Space16, -top, 640f, 46f,
                          new Vector2(0f, 1f));
            UIBuilder.BestFit(_ascendName, UITheme.FontBody, UITheme.FontTitle);
            UIBuilder.Outlined(_ascendName, Emberforge.Outline, Emberforge.StrokeThin);

            _ascendTierChip = EmberforgeUI.Chip("Tier", _ascendCardBody, "TIER 0", Emberforge.TextGold,
                                                UITheme.FontTiny - 2);
            UIBuilder.Box(_ascendTierChip, CardPad + PortraitSize + UITheme.Space16, -(top + 50f), 150f, 32f,
                          new Vector2(0f, 1f));

            _ascendStep = EmberforgeUI.Label("Step", _ascendCardBody, "", UITheme.FontSmall,
                                             Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(_ascendStep.rectTransform, CardPad + PortraitSize + UITheme.Space16 + 162f,
                          -(top + 52f), 520f, 30f, new Vector2(0f, 1f));
            UIBuilder.Outlined(_ascendStep, Emberforge.Outline, Emberforge.StrokeThin);

            top += PortraitSize + BlockGap;
            Divider(_ascendCardBody, top);
            top += Emberforge.RuleHeight + BlockGap;

            // WHAT IT COSTS, in the talent page's grammar: a chip that says a cost exists and a
            // sentence under it that says what the cost is and whether it is met. Copies of the
            // hero itself and nothing else (F5), which is the rule the whole conversion tab
            // exists to feed.
            _ascendCostChip = EmberforgeUI.Chip("CostChip", _ascendCardBody, "COSTS COPIES",
                                                UITheme.Warning, UITheme.FontTiny - 4);
            UIBuilder.Box(_ascendCostChip, CardPad, -top, 210f, 28f, new Vector2(0f, 1f));

            _ascendCostNote = EmberforgeUI.Body("CostNote", _ascendCardBody, "", UITheme.FontTiny,
                                                UITheme.Warning, TextAnchor.UpperLeft);
            StretchIn(_ascendCardBody, _ascendCostNote.rectTransform, top + 32f, 30f);
            UIBuilder.BestFit(_ascendCostNote, UITheme.FontTiny - 5, UITheme.FontTiny);
            UIBuilder.Outlined(_ascendCostNote, Emberforge.Outline, Emberforge.StrokeThin);

            top += CostBlockHeight + BlockGap;
            Divider(_ascendCardBody, top);
            top += Emberforge.RuleHeight + BlockGap;

            Text heading = EmberforgeUI.Label("MovesHeading", _ascendCardBody, "WHAT ASCENDING MOVES",
                                              UITheme.FontTiny - 2, Emberforge.TextGold,
                                              TextAnchor.UpperLeft);
            UIBuilder.Box(heading.rectTransform, CardPad, -top, 520f, HeadingHeight,
                          new Vector2(0f, 1f));
            UIBuilder.Outlined(heading, Emberforge.Outline, Emberforge.StrokeThin);
            top += HeadingHeight + UITheme.Space4;

            // THREE ROWS, BEFORE AND AFTER, and the tier level is deliberately the first of them.
            // It is the only one of the three that goes DOWN, it is the number a player is about
            // to watch fall, and burying it under two rising figures would be the page hiding the
            // thing it is here to warn about.
            _moveRows.Add(MoveRow("TierLevel", "TIER LEVEL", top));
            top += MoveRowHeight;
            _moveRows.Add(MoveRow("Level", "LEVEL", top));
            top += MoveRowHeight;
            _moveRows.Add(MoveRow("Cap", "LEVEL CAP", top));
            top += MoveRowHeight + BlockGap;

            _resetNote = EmberforgeUI.Body("ResetNote", _ascendCardBody, "", UITheme.FontTiny - 1,
                                           Emberforge.TextBody, TextAnchor.UpperLeft);
            StretchIn(_ascendCardBody, _resetNote.rectTransform, top, ResetNoteHeight);
            UIBuilder.BestFit(_resetNote, UITheme.FontTiny - 6, UITheme.FontTiny - 1);
            UIBuilder.Outlined(_resetNote, Emberforge.Outline, Emberforge.StrokeThin);
            top += ResetNoteHeight + BlockGap;

            Divider(_ascendCardBody, top);
            top += Emberforge.RuleHeight + UITheme.Space4;

            _opensNote = EmberforgeUI.Body("Opens", _ascendCardBody, "", UITheme.FontTiny - 1,
                                           Emberforge.TextQuiet, TextAnchor.UpperLeft);
            StretchIn(_ascendCardBody, _opensNote.rectTransform, top, OpensNoteHeight);
            UIBuilder.BestFit(_opensNote, UITheme.FontTiny - 6, UITheme.FontTiny - 1);
            UIBuilder.Outlined(_opensNote, Emberforge.Outline, Emberforge.StrokeThin);
        }

        /// <summary>One before-and-after row: what moves, where it stands, and where it lands.
        /// Returns the two figures so a repaint can write them without a lookup.</summary>
        private Text[] MoveRow(string name, string label, float top)
        {
            Text caption = EmberforgeUI.Label(name + "Label", _ascendCardBody, label, UITheme.FontTiny,
                                              Emberforge.TextQuiet, TextAnchor.MiddleLeft);
            UIBuilder.Box(caption.rectTransform, CardPad, -top, 220f, 30f, new Vector2(0f, 1f));
            UIBuilder.Outlined(caption, Emberforge.Outline, Emberforge.StrokeThin);

            Text before = EmberforgeUI.Label(name + "Before", _ascendCardBody, "", HubStyle.FontValue,
                                             Emberforge.TextBody, TextAnchor.MiddleRight);
            UIBuilder.Box(before.rectTransform, CardPad + 220f, -top, 110f, 30f, new Vector2(0f, 1f));
            UIBuilder.Outlined(before, Emberforge.Outline, Emberforge.StrokeThin);

            Text arrow = EmberforgeUI.Label(name + "Arrow", _ascendCardBody, "-", UITheme.FontTiny,
                                            Emberforge.TextQuiet, TextAnchor.MiddleCenter);
            UIBuilder.Box(arrow.rectTransform, CardPad + 336f, -top, 40f, 30f, new Vector2(0f, 1f));

            Text after = EmberforgeUI.Label(name + "After", _ascendCardBody, "", HubStyle.FontValue,
                                            Emberforge.TextGold, TextAnchor.MiddleLeft);
            UIBuilder.Box(after.rectTransform, CardPad + 382f, -top, 140f, 30f, new Vector2(0f, 1f));
            UIBuilder.Outlined(after, Emberforge.Outline, Emberforge.StrokeThin);

            return new Text[] { before, after };
        }

        // ------------------------------------------------------------------ the convert tab

        private void BuildConvertPage()
        {
            _convertPage = BodyHost("Convert");

            RectTransform well = UIBuilder.Node("AspectWell", _convertPage);
            well.anchorMin = new Vector2(0f, 0f);
            well.anchorMax = new Vector2(0f, 1f);
            well.pivot = new Vector2(0f, 0.5f);
            well.sizeDelta = new Vector2(AspectListWidth, 0f);
            well.anchoredPosition = Vector2.zero;
            EmberforgeUI.SunkenOn(well).raycastTarget = true;

            ScrollRect aspectScroll;
            _aspectList = UIBuilder.ScrollList("Aspects", well, out aspectScroll, UITheme.Space8,
                                               new RectOffset(10, 10, 10, 10));
            UIBuilder.FillParent((RectTransform)aspectScroll.transform, 4f);
            BuildAspectCards();

            RectTransform right = UIBuilder.Node("Right", _convertPage);
            right.anchorMin = new Vector2(0f, 0f);
            right.anchorMax = new Vector2(1f, 1f);
            right.offsetMin = new Vector2(AspectListWidth + ColumnGap, 0f);
            right.offsetMax = Vector2.zero;

            float top = 0f;
            BuildRollBlock(right, top);
            top += RollBlockHeight + BlockGap;
            Divider(right, top);
            top += Emberforge.RuleHeight + BlockGap;
            BuildRecipeBlock(right, top);
            top += RecipeBlockHeight + BlockGap;
            Divider(right, top);
            top += Emberforge.RuleHeight + BlockGap;
            BuildFodderGrid(right, top);
        }

        private void BuildAspectCards()
        {
            // Every aspect the game has, in the enum's own order, and each one selectable whether
            // or not the player can currently afford it. F4: conversion groups by ASPECT and not
            // by role, and six aspects listed together is the page saying that once rather than
            // in a sentence nobody reads.
            Aspect[] all = { Aspect.Ember, Aspect.Tide, Aspect.Verdant, Aspect.Gale,
                             Aspect.Umbra, Aspect.Lumen };
            for (int i = 0; i < all.Length; i++)
            {
                Aspect aspect = all[i];
                ForgeCard card = EmberforgeUI.SelectableCard("Aspect" + aspect, _aspectList,
                                                             delegate { PickAspect(aspect); });
                UIBuilder.Sized(card.Root, AspectListWidth - 40f, AspectRowHeight);

                float width = AspectListWidth - 40f;
                Image glyph = UIBuilder.Img("Glyph", card.Body,
                                            TextureLab.Sigil(AspectSigil(aspect), 64),
                                            Emberforge.Accent(aspect));
                UIBuilder.Box(glyph.rectTransform, UITheme.Space12, 0f, 44f, 44f,
                              new Vector2(0f, 0.5f));
                glyph.raycastTarget = false;

                Text name = EmberforgeUI.Title("Name", card.Body, aspect.ToString().ToUpperInvariant(),
                                               UITheme.FontSmall, Emberforge.TextGold,
                                               TextAnchor.UpperLeft);
                UIBuilder.Box(name.rectTransform, 72f, -14f, width - 84f, 28f, new Vector2(0f, 1f));
                UIBuilder.Outlined(name, Emberforge.Outline, Emberforge.StrokeThin);

                Text sub = EmberforgeUI.Label("Sub", card.Body, "", UITheme.FontTiny - 4,
                                              Emberforge.TextQuiet, TextAnchor.UpperLeft);
                UIBuilder.Box(sub.rectTransform, 72f, -44f, width - 84f, 24f, new Vector2(0f, 1f));
                UIBuilder.BestFit(sub, UITheme.FontTiny - 8, UITheme.FontTiny - 4);
                UIBuilder.Outlined(sub, Emberforge.Outline, Emberforge.StrokeThin);

                _aspectCards.Add(card);
            }
        }

        /// <summary>
        /// THE ROLL: every five-star the chosen conversion could hand back, drawn as faces.
        ///
        /// This block is the reason the page is laid out the way it is. F3 makes the output random
        /// within the aspect and never chosen, and the only honest way to show a random outcome
        /// before it happens is to show the whole set it is drawn from. Five faces and "one of
        /// these five, rolled" says it in a form nobody has to read a sentence to understand - and
        /// it sits at the TOP of the tab, above the fodder and above the button, because a player
        /// who scrolls past everything else still meets it first.
        /// </summary>
        private void BuildRollBlock(RectTransform parent, float top)
        {
            Text heading = EmberforgeUI.Label("RollHeading", parent, "WHAT A CONVERSION HANDS BACK",
                                              UITheme.FontTiny - 2, Emberforge.TextGold,
                                              TextAnchor.UpperLeft);
            UIBuilder.Box(heading.rectTransform, 0f, -top, 700f, HeadingHeight, new Vector2(0f, 1f));
            UIBuilder.Outlined(heading, Emberforge.Outline, Emberforge.StrokeThin);

            float facesTop = top + HeadingHeight + UITheme.Space4;
            _rollRow = UIBuilder.Node("Roll", parent);
            _rollRow.anchorMin = new Vector2(0f, 1f);
            _rollRow.anchorMax = new Vector2(1f, 1f);
            _rollRow.pivot = new Vector2(0.5f, 1f);
            _rollRow.offsetMin = new Vector2(0f, -(facesTop + RollPortrait + 16f));
            _rollRow.offsetMax = new Vector2(0f, -facesTop);

            _rollNote = EmberforgeUI.Body("RollNote", parent, "", UITheme.FontTiny - 1,
                                          Emberforge.TextBody, TextAnchor.UpperLeft);
            StretchIn(parent, _rollNote.rectTransform,
                      facesTop + RollPortrait + 16f + UITheme.Space4, 40f);
            UIBuilder.BestFit(_rollNote, UITheme.FontTiny - 6, UITheme.FontTiny - 1);
            UIBuilder.Outlined(_rollNote, Emberforge.Outline, Emberforge.StrokeThin);
        }

        /// <summary>
        /// THE RECIPE: two lines, always both, saying what the seed must be and what the fodder
        /// must be.
        ///
        /// Two lines whichever aspect is chosen, and that shape is the point (F9). An Umbra
        /// conversion needs one Umbra hero and then takes anything; an Ember conversion takes
        /// Ember throughout. Printing both halves for every aspect makes that a difference between
        /// two recipes. Printing only the one the player broke would make it a complaint, which is
        /// what a rule discovered by failing always becomes.
        /// </summary>
        private void BuildRecipeBlock(RectTransform parent, float top)
        {
            Text heading = EmberforgeUI.Label("RecipeHeading", parent, "THE RECIPE",
                                              UITheme.FontTiny - 2, Emberforge.TextGold,
                                              TextAnchor.UpperLeft);
            UIBuilder.Box(heading.rectTransform, 0f, -top, 700f, HeadingHeight, new Vector2(0f, 1f));
            UIBuilder.Outlined(heading, Emberforge.Outline, Emberforge.StrokeThin);

            _recipeSeed = EmberforgeUI.Body("Seed", parent, "", UITheme.FontTiny - 1,
                                            Emberforge.TextBody, TextAnchor.UpperLeft);
            StretchIn(parent, _recipeSeed.rectTransform, top + HeadingHeight + UITheme.Space4, 32f);
            UIBuilder.BestFit(_recipeSeed, UITheme.FontTiny - 6, UITheme.FontTiny - 1);
            UIBuilder.Outlined(_recipeSeed, Emberforge.Outline, Emberforge.StrokeThin);

            _recipeFodder = EmberforgeUI.Body("Fodder", parent, "", UITheme.FontTiny - 1,
                                              Emberforge.TextQuiet, TextAnchor.UpperLeft);
            StretchIn(parent, _recipeFodder.rectTransform,
                      top + HeadingHeight + UITheme.Space4 + 32f, 34f);
            UIBuilder.BestFit(_recipeFodder, UITheme.FontTiny - 6, UITheme.FontTiny - 1);
            UIBuilder.Outlined(_recipeFodder, Emberforge.Outline, Emberforge.StrokeThin);
        }

        private void BuildFodderGrid(RectTransform parent, float top)
        {
            Text heading = EmberforgeUI.Label("FodderHeading", parent, "WHAT YOU SPEND",
                                              UITheme.FontTiny - 2, Emberforge.TextGold,
                                              TextAnchor.UpperLeft);
            UIBuilder.Box(heading.rectTransform, 0f, -top, 700f, HeadingHeight, new Vector2(0f, 1f));
            UIBuilder.Outlined(heading, Emberforge.Outline, Emberforge.StrokeThin);

            RectTransform well = UIBuilder.Node("FodderWell", parent);
            well.anchorMin = new Vector2(0f, 0f);
            well.anchorMax = new Vector2(1f, 1f);
            well.offsetMin = Vector2.zero;
            well.offsetMax = new Vector2(0f, -(top + HeadingHeight + UITheme.Space4));
            EmberforgeUI.SunkenOn(well).raycastTarget = true;

            ScrollRect scroll;
            _fodderGrid = UIBuilder.ScrollGrid("Bodies", well, out scroll,
                                               new Vector2(FodderCellWidth, FodderCellHeight),
                                               new Vector2(UITheme.Space8, UITheme.Space8),
                                               FodderColumns, new RectOffset(10, 10, 10, 10));
            UIBuilder.FillParent((RectTransform)scroll.transform, 4f);

            _fodderEmpty = EmberforgeUI.Body("FodderEmpty", well, "", UITheme.FontSmall,
                                             Emberforge.TextQuiet, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(_fodderEmpty.rectTransform, 40f);
            UIBuilder.Outlined(_fodderEmpty, Emberforge.Outline, Emberforge.StrokeThin);
        }

        /// <summary>How many fodder cells fit across the well, at the reference canvas. A fixed
        /// count rather than a measured one, because GridLayoutGroup wants the number at build
        /// time and the well's width is a constant of this layout.</summary>
        private const int FodderColumns = 8;

        // ------------------------------------------------------------------ the action bar

        private void BuildActionBar()
        {
            RectTransform bar = UIBuilder.Node("Actions", Root);
            UIBuilder.BottomStrip(bar, ActionBarHeight, 0f, 0f);
            EmberforgeUI.SunkenOn(bar).raycastTarget = false;

            // WHY THE BUTTON IS DEAD, in the blocker's own words. Every sentence that lands here
            // was written by GameSession.ConversionBlocker or GameSession.AscensionBlocker, which
            // is what keeps the refusal on screen and the refusal in the rule the same sentence.
            _statusNote = EmberforgeUI.Label("Status", bar, "", UITheme.FontTiny,
                                             Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Box(_statusNote.rectTransform, 0f, -8f, 1600f, 26f, new Vector2(0.5f, 1f));
            UIBuilder.BestFit(_statusNote, UITheme.FontTiny - 5, UITheme.FontTiny);
            UIBuilder.Outlined(_statusNote, Emberforge.Outline, Emberforge.StrokeThin);

            // The points meter, on the left, and only on the convert tab. A conversion is bought
            // with a TOTAL rather than with a count of heroes - the fodder table prices a body by
            // its star rating - so the number that decides whether the button lights is the total,
            // and a bar is the one shape that says "how far along" without being read.
            // UIBuilder.Bar hands back the FILL, not the track - the fill is the only half a
            // caller ever writes to - so the rect that gets positioned and hidden is its parent.
            _pointsFill = UIBuilder.Bar("Points", bar, UITheme.BarTrackDeep, UITheme.Gold);
            _pointsRoot = (RectTransform)_pointsFill.transform.parent;
            UIBuilder.Box(_pointsRoot, UITheme.ScreenPadding, 46f, 520f, 22f, new Vector2(0f, 0f));

            _pointsLabel = EmberforgeUI.Label("PointsLabel", bar, "", UITheme.FontTiny,
                                              Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(_pointsLabel.rectTransform, UITheme.ScreenPadding, 20f, 520f, 24f,
                          new Vector2(0f, 0f));
            UIBuilder.Outlined(_pointsLabel, Emberforge.Outline, Emberforge.StrokeThin);

            _clearButton = EmberforgeUI.Button("Clear", bar, "CLEAR", UITheme.FontSmall,
                                               ForgeButtonKind.Secondary);
            UIBuilder.Box(_clearButton.Root, -(UITheme.ScreenPadding + 580f), 16f, 260f, 88f,
                          new Vector2(1f, 0f));
            _clearButton.OnClick(ClearBodies);

            _actButton = EmberforgeUI.Button("Act", bar, "ASCEND", UITheme.FontHeading,
                                             ForgeButtonKind.Primary);
            UIBuilder.Box(_actButton.Root, -UITheme.ScreenPadding, 16f, 560f, 88f,
                          new Vector2(1f, 0f));
            _actButton.OnClick(Act);
        }

        // ------------------------------------------------------------------ show and repaint

        public override void OnShow()
        {
            // A hero handed in through ShowHero may have been spent, ascended or sold since. The
            // subject is re-resolved against the live collection on every show rather than trusted,
            // which is the same reason the fodder list is rebuilt rather than kept.
            PlayerProfile profile = App.Session.Profile;
            if (_subject != null && !profile.Heroes.Contains(_subject)) _subject = null;
            PruneBodies();
            Rebuild();
        }

        private void SetTab(bool convert)
        {
            if (_convertTab == convert) return;
            App.Audio.Tap();
            _convertTab = convert;
            Rebuild();
        }

        private void PickAspect(Aspect aspect)
        {
            if (_target == aspect) return;
            App.Audio.Tap();
            _target = aspect;
            // The offered bodies are KEPT across an aspect change rather than cleared. A player
            // comparing two aspects with the same pile of fodder in hand is the ordinary case, and
            // the recipe lines say at once which of the two the pile satisfies - which is more
            // useful than silently emptying their hands each time they look.
            Rebuild();
        }

        private void PickHero(HeroInstance hero)
        {
            if (_subject == hero) return;
            App.Audio.Tap();
            _subject = hero;
            Rebuild();
        }

        private void ClearBodies()
        {
            if (_bodies.Count == 0) return;
            App.Audio.Tap();
            _bodies.Clear();
            Rebuild();
        }

        /// <summary>
        /// One more body of this hero, or none at all once it has offered everything it can.
        ///
        /// ONE GESTURE, because a cell this size has room for one. Tapping adds a body and tapping
        /// a hero that has already offered all its spare copies takes them all back, so a cell
        /// cycles 0, 1, 2, 0 rather than needing a second control to undo the first. The spare is
        /// what the session counts as spare - copies less the one a deployed hero keeps standing
        /// in the line - so the cell can never offer a body GameSession.ConversionBlocker would
        /// refuse.
        /// </summary>
        private void OfferBody(HeroInstance hero)
        {
            int spare = SpareOf(hero);
            int offered = OfferedOf(hero);
            App.Audio.Tap();
            if (offered >= spare)
            {
                for (int i = _bodies.Count - 1; i >= 0; i--) if (_bodies[i] == hero) _bodies.RemoveAt(i);
            }
            else _bodies.Add(hero);
            Rebuild();
        }

        private int SpareOf(HeroInstance hero)
        {
            return hero.Copies - (App.Session.Profile.IsDeployed(hero.Uid) ? 1 : 0);
        }

        private int OfferedOf(HeroInstance hero)
        {
            int n = 0;
            for (int i = 0; i < _bodies.Count; i++) if (_bodies[i] == hero) n++;
            return n;
        }

        /// <summary>Drops offered bodies the collection no longer backs - a hero spent elsewhere,
        /// or one that has since been deployed and so has one fewer to spare.</summary>
        private void PruneBodies()
        {
            PlayerProfile profile = App.Session.Profile;
            Dictionary<HeroInstance, int> kept = new Dictionary<HeroInstance, int>();
            for (int i = _bodies.Count - 1; i >= 0; i--)
            {
                HeroInstance hero = _bodies[i];
                int taken;
                kept.TryGetValue(hero, out taken);
                if (hero == null || hero.Def == null || !profile.Heroes.Contains(hero)
                    || taken + 1 > SpareOf(hero))
                {
                    _bodies.RemoveAt(i);
                    continue;
                }
                kept[hero] = taken + 1;
            }
        }

        private void Rebuild()
        {
            _ascendPage.gameObject.SetActive(!_convertTab);
            _convertPage.gameObject.SetActive(_convertTab);
            EmberforgeUI.SetTabSelected(_ascendTab, !_convertTab);
            EmberforgeUI.SetTabSelected(_convertTabButton, _convertTab);

            _clearButton.Root.gameObject.SetActive(_convertTab);
            _pointsRoot.gameObject.SetActive(_convertTab);
            _pointsLabel.gameObject.SetActive(_convertTab);

            if (_convertTab) RefreshConvert();
            else RefreshAscend();
        }

        // ------------------------------------------------------------------ the ascend repaint

        private void RefreshAscend()
        {
            GameSession session = App.Session;
            RebuildHeroList();

            _headerSubtitle.text = "Five-stars go past five. Copies of the hero itself are the price.";

            bool has = _subject != null && _subject.Def != null;
            _ascendEmpty.gameObject.SetActive(!has);
            SetCardVisible(has);
            if (!has)
            {
                _ascendEmpty.text = _heroRows.Count == 0
                    ? "You command no five-star yet. Convert lower heroes into one, or open the Rift."
                    : "Choose a five-star to see what its next tier costs and what it moves.";
                _statusNote.text = "";
                _actButton.SetText("ASCEND");
                _actButton.Interactable = false;
                return;
            }

            HeroDef def = _subject.Def;
            AscensionPreview preview = session.PreviewAscension(_subject);

            RebuildPortrait(_ascendPortrait, def, def.Stars, PortraitSize);
            _ascendName.text = def.Name;
            EmberforgeUI.SetChipText(_ascendTierChip, "TIER " + preview.FromTier);
            _ascendStep.text = preview.HasNextTier
                ? "to tier " + preview.ToTier
                : "the highest tier this game authors";

            // The three figures, from the preview rather than from arithmetic done here. A hero
            // with no next tier repeats the present in all three rows, which is the honest reading:
            // nothing moves.
            //
            // THE CAP ROW IS IN TIER LEVELS, and that is the fix this pass exists for. It used to
            // bind AscensionPreview.CapAfter, the cap as a LEVEL, so a tier-0 hero the list beside
            // it called "LEVEL 9 / 9" read on the card as a cap of 9 rising to 45 - the same cap,
            // answered in a unit the rest of the page never uses. Rows 0 and 2 are now a pair: a
            // tier level and the cap over it, 9 / 9 becoming 1 / 18, which is what the list says.
            // Row 1 is the one absolute figure on the card, and the one that does not reset.
            SetMoveRow(0, preview.TierLevelBefore, preview.TierLevelAfter);
            SetMoveRow(1, preview.LevelBefore, preview.LevelAfter);
            SetMoveRow(2, preview.TierCapBefore, preview.TierCapAfter);

            if (preview.HasNextTier)
            {
                _ascendCostChip.gameObject.SetActive(true);
                _ascendCostNote.text = preview.CopiesNeeded + " more cop"
                                     + (preview.CopiesNeeded == 1 ? "y" : "ies") + " of " + def.Name
                                     + ". You hold " + preview.CopiesSpare + " to spare.";
                _ascendCostNote.color = preview.CopiesSpare >= preview.CopiesNeeded
                                      ? UITheme.Success : UITheme.Warning;
                EmberforgeUI.SetChipTint(_ascendCostChip, preview.CopiesSpare >= preview.CopiesNeeded
                                                        ? UITheme.Success : UITheme.Warning);

                // THE WARNING, in one sentence, because the three rows above it now carry the
                // whole of what it used to spell out. The paragraph this replaces said the tier
                // level resets, said the Level does not, said the cap rises and gave the two
                // figures for it - and every one of those is a row the player can read directly.
                // Prose that restates the table is prose the player learns to skip, and the one
                // thing the table cannot say is what the reset MEANS. That is all this says now.
                _resetNote.text = "The tier level starts again, the power does not.";

                int slot = SlotOpenedByTier(preview.ToTier);
                PassiveDef opened = slot > 0 ? def.TalentAt(slot) : null;
                _opensNote.text = opened != null
                    ? "Tier " + preview.ToTier + " also unlocks talent slot " + Roman(slot) + ", "
                      + opened.Name + ", on the talent page."
                    : "Tier " + preview.ToTier + " raises the cap and the stat line. It opens no"
                      + " further talent slot on this hero.";
            }
            else
            {
                _ascendCostChip.gameObject.SetActive(false);
                _ascendCostNote.text = "";
                _resetNote.text = def.Stars < 5
                    ? "Below five stars a hero never ascends."
                    : "There is nothing above this tier to spend copies on.";
                _opensNote.text = "";
            }

            _statusNote.text = preview.Possible ? "" : preview.Blocker;
            _actButton.SetText(preview.HasNextTier ? "ASCEND TO TIER " + preview.ToTier : "ASCEND");
            _actButton.Interactable = preview.Possible;
        }

        private void SetMoveRow(int index, int before, int after)
        {
            Text[] row = _moveRows[index];
            row[0].text = before.ToString();
            row[1].text = after.ToString();
            // Gold when it moves, quiet when it does not. The tier level falling is as much a move
            // as the cap rising, so both are lit - the row's own label is what says which is which.
            row[1].color = after == before ? Emberforge.TextQuiet : Emberforge.TextGold;
        }

        private void SetCardVisible(bool visible)
        {
            _ascendCardBody.gameObject.SetActive(visible);
        }

        private void RebuildHeroList()
        {
            for (int i = _heroList.childCount - 1; i >= 0; i--) Destroy(_heroList.GetChild(i).gameObject);
            _heroCards.Clear();
            _heroRows.Clear();

            GameSession session = App.Session;
            PlayerProfile profile = session.Profile;

            // Deployed first, in line order, then the bench, the way the talent page's strip is
            // ordered and for the same reason: the heroes a player is actually fighting with are
            // the ones whose next tier they are deciding about.
            List<HeroInstance> line = profile.TeamHeroes();
            for (int i = 0; i < line.Count; i++)
                if (line[i].Def != null && line[i].Def.Stars >= 5) _heroRows.Add(line[i]);
            for (int i = 0; i < profile.Heroes.Count; i++)
            {
                HeroInstance hero = profile.Heroes[i];
                if (hero.Def == null || hero.Def.Stars < 5 || _heroRows.Contains(hero)) continue;
                _heroRows.Add(hero);
            }

            float width = AscendListWidth - 40f;
            for (int i = 0; i < _heroRows.Count; i++)
            {
                HeroInstance hero = _heroRows[i];
                ForgeCard card = EmberforgeUI.SelectableCard("Hero" + i, _heroList,
                                                             delegate { PickHero(hero); });
                UIBuilder.Sized(card.Root, width, HeroRowHeight);

                RectTransform face = EmberforgeUI.PortraitFrame("Face", card.Body, hero.Def,
                                                                hero.Def.Stars, 72f);
                UIBuilder.Box(face, UITheme.Space12, 0f, 72f, 72f, new Vector2(0f, 0.5f));

                Text name = EmberforgeUI.Title("Name", card.Body, hero.Def.Name, UITheme.FontSmall,
                                               Emberforge.TextGold, TextAnchor.UpperLeft);
                UIBuilder.Box(name.rectTransform, 100f, -14f, width - 112f, 28f, new Vector2(0f, 1f));
                UIBuilder.BestFit(name, UITheme.FontTiny - 2, UITheme.FontSmall);
                UIBuilder.Outlined(name, Emberforge.Outline, Emberforge.StrokeThin);

                // THE TIER BESIDE THE LEVEL, everywhere a level is shown. DESIGN-FUSION section 4:
                // once ascension exists a level number stops being comparable between heroes -
                // level 40 at tier 2 beats level 100 at tier 0 - so a row that printed a bare
                // level would be inviting exactly the comparison the system has broken.
                AscensionPreview preview = session.PreviewAscension(hero);
                string ready = preview.Possible ? "  -  READY" : "";
                Text line2 = EmberforgeUI.Label("Line", card.Body,
                    "TIER " + hero.Ascension + "  -  LEVEL " + session.TierLevelOf(hero)
                    + " / " + StatMath.HeroLevelCap(session.Balance, hero.Def.Stars, hero.Ascension)
                    + "  -  " + (hero.Copies - 1) + " SPARE" + ready,
                    UITheme.FontTiny - 4, preview.Possible ? UITheme.Success : Emberforge.TextQuiet,
                    TextAnchor.UpperLeft);
                UIBuilder.Box(line2.rectTransform, 100f, -46f, width - 112f, 24f, new Vector2(0f, 1f));
                UIBuilder.BestFit(line2, UITheme.FontTiny - 8, UITheme.FontTiny - 4);
                UIBuilder.Outlined(line2, Emberforge.Outline, Emberforge.StrokeThin);

                _heroCards.Add(card);
                EmberforgeUI.SetCardSelected(card, hero == _subject);
            }
        }

        // ------------------------------------------------------------------ the convert repaint

        private void RefreshConvert()
        {
            GameSession session = App.Session;

            _headerSubtitle.text = "Lower heroes become a five-star of their aspect. Which one is rolled.";

            RefreshAspectCards();
            RebuildRollRow();
            RefreshRecipe();
            RebuildFodderGrid();

            int points = 0;
            for (int i = 0; i < _bodies.Count; i++) points += session.FodderPoints(_bodies[i].Def);
            int required = session.Balance.ConversionPointsRequired;
            UIBuilder.SetBar(_pointsFill, required > 0 ? points / (float)required : 0f);
            _pointsLabel.text = points + " / " + required + " from " + _bodies.Count
                              + (_bodies.Count == 1 ? " body" : " bodies");
            _pointsLabel.color = points >= required ? UITheme.Success : Emberforge.TextGold;

            string blocker = session.ConversionBlocker(_target, _bodies);
            _statusNote.text = blocker != null ? blocker : "";
            _clearButton.Interactable = _bodies.Count > 0;
            _actButton.SetText("CONVERT TO " + Article(_target).ToUpperInvariant() + " "
                               + _target.ToString().ToUpperInvariant() + " 5-STAR");
            _actButton.Interactable = blocker == null;
        }

        private void RefreshAspectCards()
        {
            GameSession session = App.Session;
            Aspect[] all = { Aspect.Ember, Aspect.Tide, Aspect.Verdant, Aspect.Gale,
                             Aspect.Umbra, Aspect.Lumen };
            for (int i = 0; i < _aspectCards.Count && i < all.Length; i++)
            {
                Aspect aspect = all[i];
                EmberforgeUI.SetCardSelected(_aspectCards[i], aspect == _target);
                Text sub = _aspectCards[i].Body.Find("Sub").GetComponent<Text>();
                int pool = session.ConversionPool(aspect).Count;
                sub.text = pool + (pool == 1 ? " five-star" : " five-stars")
                         + (session.IsSeedAspect(aspect) ? "  -  needs a seed" : "");
                sub.color = session.IsSeedAspect(aspect) ? UITheme.Warning : Emberforge.TextQuiet;
            }
        }

        private void RebuildRollRow()
        {
            for (int i = _rollRow.childCount - 1; i >= 0; i--) Destroy(_rollRow.GetChild(i).gameObject);

            GameSession session = App.Session;
            List<HeroDef> pool = session.ConversionPool(_target);

            for (int i = 0; i < pool.Count; i++)
            {
                HeroDef def = pool[i];
                RectTransform cell = UIBuilder.Node("Roll" + i, _rollRow);
                UIBuilder.Box(cell, i * (RollPortrait + UITheme.Space12), 0f, RollPortrait,
                              RollPortrait + 16f, new Vector2(0f, 0.5f));

                RectTransform face = EmberforgeUI.PortraitFrame("Face", cell, def, def.Stars, RollPortrait);
                UIBuilder.Box(face, 0f, 0f, RollPortrait, RollPortrait, new Vector2(0.5f, 1f));

                Text name = EmberforgeUI.Label("Name", cell, def.Name, UITheme.FontTiny - 8,
                                               Emberforge.TextQuiet, TextAnchor.LowerCenter);
                UIBuilder.Box(name.rectTransform, 0f, 0f, RollPortrait + 12f, 16f, new Vector2(0.5f, 0f));
                UIBuilder.BestFit(name, UITheme.FontTiny - 12, UITheme.FontTiny - 8);
                UIBuilder.Outlined(name, Emberforge.Outline, Emberforge.StrokeThin);
            }

            // THE SENTENCE UNDER THE FACES, and it says "you do not choose" in those words. The
            // faces alone could be read as a menu; the whole point of F3 is that they are not one.
            _rollNote.text = pool.Count == 0
                ? "No five-star of this aspect can be converted into."
                : "One of these " + Word(pool.Count) + ", rolled. You do not choose which - the roll"
                  + " is what manufactures the duplicate five-stars ascension eats, so a conversion"
                  + " that let you pick would have nothing to feed.";
            _rollNote.color = pool.Count == 0 ? UITheme.Warning : Emberforge.TextBody;
        }

        private void RefreshRecipe()
        {
            GameSession session = App.Session;
            string aspect = _target.ToString();
            int roster = session.ConversionFodderRoster(_target);
            bool seeded = session.IsSeedAspect(_target);

            _recipeSeed.text = seeded
                ? "SEED: one " + aspect + " hero below five stars. Mandatory, and it is what makes"
                  + " this aspect slow."
                : "SEED: none. Every body in " + Article(_target) + " " + aspect
                  + " conversion is itself " + aspect + ".";

            _recipeFodder.text = seeded
                ? "FODDER: any aspect, to " + session.Balance.ConversionPointsRequired
                  + " points. The roster authors only " + roster + " " + aspect + " heroes below five"
                  + " stars, so a recipe that asked for " + aspect + " throughout would be a wall"
                  + " rather than a rarity - the seed gates the rate instead."
                : "FODDER: " + aspect + " throughout, to " + session.Balance.ConversionPointsRequired
                  + " points. The roster authors " + roster + " " + aspect + " heroes below five"
                  + " stars to draw them from. A five-star is never fodder.";
        }

        private void RebuildFodderGrid()
        {
            for (int i = _fodderGrid.childCount - 1; i >= 0; i--)
                Destroy(_fodderGrid.GetChild(i).gameObject);

            GameSession session = App.Session;
            PlayerProfile profile = session.Profile;

            // EVERY BODY BELOW FIVE STARS, whatever its aspect, on every aspect's recipe. A grid
            // filtered to the target aspect would hide the generic fodder a seeded conversion is
            // allowed to spend (F9), and a grid that hid it on the four unseeded aspects would be
            // two different grids for one rule. So the rule is said in the recipe above and shown
            // on the cell: a body the chosen recipe cannot take is drawn dim and says why.
            int shown = 0;
            bool seeded = session.IsSeedAspect(_target);
            for (int i = 0; i < profile.Heroes.Count; i++)
            {
                HeroInstance hero = profile.Heroes[i];
                if (hero.Def == null || hero.Def.Stars >= 5 || hero.Def.Stars < 1) continue;
                if (SpareOf(hero) <= 0) continue;
                shown++;
                BuildFodderCell(hero, seeded);
            }

            _fodderEmpty.gameObject.SetActive(shown == 0);
            _fodderEmpty.text = "Nothing to spend. Every hero you command is either a five-star -"
                              + " which is never fodder - or is standing in your line with no copy"
                              + " to spare.";
        }

        private void BuildFodderCell(HeroInstance hero, bool seeded)
        {
            GameSession session = App.Session;
            HeroDef def = hero.Def;
            bool matches = def.Aspect == _target;
            bool usable = seeded || matches;
            int offered = OfferedOf(hero);
            int spare = SpareOf(hero);

            ForgeCard card = EmberforgeUI.SelectableCard(hero.Uid, _fodderGrid,
                                                         delegate { OfferBody(hero); });
            EmberforgeUI.SetCardSelected(card, offered > 0);

            RectTransform face = EmberforgeUI.PortraitFrame("Face", card.Body, def, def.Stars, 68f);
            UIBuilder.Box(face, 0f, -6f, 68f, 68f, new Vector2(0.5f, 1f));

            Text name = EmberforgeUI.Label("Name", card.Body, def.Name, UITheme.FontTiny - 6,
                                           usable ? Emberforge.TextBody : Emberforge.TextQuiet,
                                           TextAnchor.UpperCenter);
            UIBuilder.Box(name.rectTransform, 0f, -78f, FodderCellWidth - 12f, 20f, new Vector2(0.5f, 1f));
            UIBuilder.BestFit(name, UITheme.FontTiny - 10, UITheme.FontTiny - 6);
            UIBuilder.Outlined(name, Emberforge.Outline, Emberforge.StrokeThin);

            Text worth = EmberforgeUI.Label("Worth", card.Body,
                def.Stars + "*  -  " + session.FodderPoints(def) + " pts  -  " + spare + " spare",
                UITheme.FontTiny - 9, Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Box(worth.rectTransform, 0f, -100f, FodderCellWidth - 8f, 18f, new Vector2(0.5f, 1f));
            UIBuilder.BestFit(worth, UITheme.FontTiny - 12, UITheme.FontTiny - 9);
            UIBuilder.Outlined(worth, Emberforge.Outline, Emberforge.StrokeThin);

            // The cell's last line is the one that carries the rule. A body this recipe cannot
            // take says so in place rather than being removed, because a hero that vanished from
            // the grid when the aspect changed would teach the player nothing about why.
            string tag = offered > 0
                ? "GIVING " + offered
                : usable
                  ? (matches ? def.Aspect.ToString().ToUpperInvariant() : "GENERIC FODDER")
                  : "NOT " + _target.ToString().ToUpperInvariant();
            Color tint = offered > 0 ? UITheme.Success
                       : usable ? (matches ? Emberforge.Accent(def.Aspect) : Emberforge.TextQuiet)
                       : UITheme.Warning;
            RectTransform chip = EmberforgeUI.Chip("Tag", card.Body, tag, tint, UITheme.FontTiny - 10);
            UIBuilder.Box(chip, 0f, 8f, FodderCellWidth - 20f, 24f, new Vector2(0.5f, 0f));

            // Dimmed rather than disabled. The cell stays pressable on an unseeded aspect so a tap
            // produces the blocker's own sentence in the action bar instead of nothing at all -
            // a control that does nothing when pressed is the one state a player cannot learn from.
            CanvasGroup group = card.Root.gameObject.GetComponent<CanvasGroup>();
            if (group == null) group = card.Root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = usable || offered > 0 ? 1f : 0.55f;
        }

        // ------------------------------------------------------------------ the two actions

        private void Act()
        {
            if (_convertTab) DoConvert();
            else DoAscend();
        }

        private void DoAscend()
        {
            if (_subject == null) return;
            HeroDef def = _subject.Def;
            AscensionResult result = App.Session.Ascend(_subject);
            if (!result.Success)
            {
                App.Notify(result.Message);
                Rebuild();
                return;
            }
            App.Audio.Reward();
            App.Notify(def.Name + " ascended to tier " + result.ToTier
                       + ", and stands at tier level 1 on a longer ladder.");
            App.RefreshHud();
            App.RequestSave();
            Rebuild();
        }

        private void DoConvert()
        {
            // The seed is built the way the summon screen builds its own: off state that moves
            // between calls, so two conversions in a row cannot roll the same face. It is not a
            // fairness device and it is not the roll - GameSession.Convert owns the roll, through
            // DeterministicRng, and this only decides where it starts.
            int seed = App.Session.Profile.TotalSummons * 7919
                     + App.Session.Profile.Heroes.Count * 104729
                     + _bodies.Count * 31
                     + Mathf.RoundToInt(Time.unscaledTime * 1000f);

            ConversionResult result = App.Session.Convert(_target, _bodies, seed, TimeUtil.NowUnix());
            if (!result.Success)
            {
                App.Notify(result.Message);
                Rebuild();
                return;
            }

            _bodies.Clear();
            App.Audio.Reward();
            App.Notify(result.Hero.Name + (result.IsNew
                ? " answers the conversion - a five-star you did not command."
                : " again. A copy, which is what an ascension spends."));
            App.RefreshHud();
            App.RequestSave();

            // Straight to the thing the copy was for, when the copy completed a step. A conversion
            // that hands back a duplicate is ascension's input by design (DESIGN-FUSION section 1),
            // so the page follows it there rather than leaving the player to find the tab.
            if (!result.IsNew && result.Instance != null
                && App.Session.PreviewAscension(result.Instance).Possible)
            {
                _subject = result.Instance;
                _convertTab = false;
            }
            Rebuild();
        }

        // ------------------------------------------------------------------ small shared things

        private void Divider(RectTransform parent, float top)
        {
            Image rule = EmberforgeUI.Rule("Rule" + top, parent);
            rule.rectTransform.anchorMin = new Vector2(0f, 1f);
            rule.rectTransform.anchorMax = new Vector2(1f, 1f);
            rule.rectTransform.pivot = new Vector2(0.5f, 1f);
            rule.rectTransform.offsetMin = new Vector2(CardPad, -(top + Emberforge.RuleHeight));
            rule.rectTransform.offsetMax = new Vector2(-CardPad, -top);
        }

        /// <summary>A label that spans its parent's width at a fixed distance from its top. The
        /// shape every prose block on this page wants and the one Box cannot express.</summary>
        private static void StretchIn(RectTransform parent, RectTransform rect, float top, float height)
        {
            if (rect.parent != parent) rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(CardPad, -(top + height));
            rect.offsetMax = new Vector2(-CardPad, -top);
        }

        private void RebuildPortrait(RectTransform host, HeroDef def, int stars, float size)
        {
            for (int i = host.childCount - 1; i >= 0; i--) Destroy(host.GetChild(i).gameObject);
            RectTransform face = EmberforgeUI.PortraitFrame("Face", host, def, stars, size);
            UIBuilder.FillParent(face, 0f);
        }

        /// <summary>
        /// Which of TextureLab's emblems stands for an aspect. The same table the hero page and
        /// the inventory use, mapped by MEANING rather than by casting the enum.
        /// </summary>
        private static int AspectSigil(Aspect aspect)
        {
            switch (aspect)
            {
                case Aspect.Ember: return 7;
                case Aspect.Tide: return 12;
                case Aspect.Verdant: return 2;
                case Aspect.Gale: return 8;
                case Aspect.Umbra: return 6;
                case Aspect.Lumen: return 13;
            }
            return 13;
        }

        /// <summary>Which talent slot a tier opens, asked of HeroInstance's own mapping rather
        /// than worked out here: TierUnlockingTalent says it is the only other place the tier and
        /// the slot are tied together, and a third copy is the one that eventually disagrees.
        /// Zero when no authored slot is opened by that tier.</summary>
        private static int SlotOpenedByTier(int tier)
        {
            for (int slot = 1; slot <= HeroDef.MaxTalents; slot++)
                if (HeroInstance.TierUnlockingTalent(slot) == tier) return slot;
            return 0;
        }

        private static string Article(Aspect aspect)
        {
            string name = aspect.ToString();
            return name.Length > 0 && "AEIOU".IndexOf(name[0]) >= 0 ? "an" : "a";
        }

        private static string Roman(int n)
        {
            switch (n)
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
            }
            return n.ToString();
        }

        private static string Word(int n)
        {
            switch (n)
            {
                case 1: return "one";
                case 2: return "two";
                case 3: return "three";
                case 4: return "four";
                case 5: return "five";
                case 6: return "six";
            }
            return n.ToString();
        }
    }
}
