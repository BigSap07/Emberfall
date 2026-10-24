using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;
using Emberfall.Game.Cloud;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// Home base, in four layers.
    ///
    ///   0  THE MAP. The painting, and a marker on every structure that leads somewhere. The
    ///      buildings are the primary way into this game and nothing in the layers above may
    ///      cover one or its label.
    ///   1  THE HUD FRAME. Edges only: the player plate and the currencies along the top (both
    ///      live in the shared HudTopBar), a rail of small icon buttons down the LEFT edge, a nav
    ///      bar across the bottom, and the campaign card and idle chest in the bottom right
    ///      corner. All of it inside Screen.safeArea, and all of it in Emberforge - see
    ///      Emberforge.cs for why the map's own plates deliberately are not.
    ///   2  PANELS. Sheets over a dimmed map, hosting screens the router already owns. Opened by
    ///      the player plate and by the nav bar's guild entry. See PanelHost.
    ///   3  POPUPS. The idle claim popup and the rail's overflow menu. See IdleClaimPopup and
    ///      HubOverflowMenu.
    ///
    /// WHAT THIS REPLACED, and why each piece moved rather than being redesigned:
    ///
    ///   ACCOUNT, FRIENDS and GUILD were three slabs under the top bar, covering the top left of
    ///   the painting and burying the HEROES marker. Account and Friends are tabs of the player
    ///   panel now, reached from the plate that already carries the player's name; Guild is a nav
    ///   entry, and the second GUILD - the badge dock carried one as well, a thumb below the
    ///   button, which is a Hub disagreeing with itself - is gone.
    ///
    ///   THE BADGE DOCK and THE SQUAD STRIP were two five-cell rows of circles along the bottom,
    ///   one per corner, reading as one meaningless rail of ten. The dock's entries are the nav
    ///   bar now. The squad strip is gone outright: the lineup is what the formation screen is
    ///   for, and a read-only copy of it on the home screen was five portraits of chrome over a
    ///   painting.
    ///
    ///   THE IDLE PANEL was a 640x200 card in the top right, permanently covering a quarter of the
    ///   map to show five lines about a number that changes once a second. It is a chest beside
    ///   the campaign card with the stored gold in a bubble over it; the detail moved into the
    ///   popup it opens.
    ///
    ///   THE OPTIONS AND DEV BUTTONS were loose circles stacked down the right edge, where DEV
    ///   overlapped both the PROVING marker and the "..." above it. Both are rows in the rail's
    ///   overflow menu, and Settings is a tab of the player panel.
    ///
    /// NO GAME LOGIC LIVES HERE. This screen reads the session and routes; the idle cache is
    /// GameSession's, the campaign's next stage is GameSession's, and the claim is the popup's.
    /// </summary>
    public sealed class MainMenuScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.MainMenu; } }

        private float _tick;

        /// <summary>
        /// One-shot latch for the session-missing report. This screen ticks once a SECOND, so an
        /// unlatched log here is 3600 identical lines an hour, which buries the one line that
        /// matters and is how the original fault presented.
        /// </summary>
        private bool _sessionWarned;

        // ---------- layer 1 ----------
        private RectTransform _hud;
        private HudElement _chest;
        private HudElement _friendsButton;
        private HudElement _ordersButton;
        private HudElement _storeButton;
        private HudElement _eventsButton;
        private float _overflowButtonTop;

        // ---------- the campaign card ----------
        private UIButton _continueButton;
        private Text _continueSub;
        private Text _chapterLabel;
        private Image _chapterFill;
        private Text _chapterCount;

        // ---------- layer 3 ----------
        private IdleClaimPopup _claimPopup;

        /// <summary>The idle claim popup, for the capture tool and the checks (A1 of the rewarded ads lives in it).</summary>
        public IdleClaimPopup ClaimPopup { get { return _claimPopup; } }
        private HubOverflowMenu _overflowMenu;

        // ---------- the Hub's own surfaces ----------
        // The four Hub-scoped surface tones that used to be declared here - HubBackdrop, HubPanel,
        // HubPanelRaised and HubPanelSunken - live in HubStyle with the rest of the Hub's chrome
        // vocabulary, along with every metric the layers above are built from.

        // ---------- map node anchors ----------
        // Fractions of the PAINTING, x then y from its top-left - not of the screen. Every node is
        // parented to the map image and anchored by these, so a node stays on its structure at any
        // aspect ratio. The map is envelope-fitted and centred, so the screen crops it rather than
        // stretching it, and the crop is symmetric on both axes.
        //
        // Measured by rendering a coordinate grid over the file, proposing a point per structure,
        // then re-cropping each at 2x with a crosshair on it and checking one by one. The grid
        // alone is not enough: an earlier pass's first proposal put SUMMON in the sky beside the
        // obelisk and DEEP RECITAL on the brazier next to the cave, and only the crosshair pass
        // caught it.
        private const float HeroesX = 0.175f, HeroesY = 0.300f;     // the barracks, above its banner
        private const float ForgeX = 0.332f, ForgeY = 0.430f;       // the smithy roof, over the arch
        private const float MarketX = 0.485f, MarketY = 0.520f;     // the market stall canopy
        private const float SummonX = 0.578f, SummonY = 0.330f;     // the rune obelisk
        private const float CampaignX = 0.680f, CampaignY = 0.440f; // the fire portal's arch
        private const float RecitalX = 0.800f, RecitalY = 0.450f;   // the purple cave mouth

        /// <summary>
        /// The Proving, on the walled gatehouse at the far right of the painting. Measured against
        /// the art rather than picked: the gate spans roughly x 0.86 to 0.95, and 0.915 sits on
        /// its door.
        ///
        /// It is the one marker a RIGHT-hand rail could reach, which is why HubMarkerClamp takes
        /// an inset: the rail is HUD, the gatehouse is a building, and the rule is that the frame
        /// gives way to the map rather than the other way round. The clamp slides the PLATE clear
        /// of the rail's column and leaves the tail on the door, so the marker still points at the
        /// building it names. With the rail on the left that column is the other side now and this
        /// marker no longer needs the clamp, but the clamp is what makes moving the rail a
        /// one-line change rather than a re-measurement, so it stays.
        /// </summary>
        private const float ProvingX = 0.915f, ProvingY = 0.500f;   // the walled gatehouse door

        private RectTransform _map;

        /// <summary>
        /// The campaign card, and the numbers the chest beside it is placed from. Shared consts
        /// rather than a local in each method, because BuildIdleChest positions itself off the
        /// card's width and the two had already drifted into two copies of 600f.
        ///
        /// Taller than the 168 it was: a framed Emberforge panel spends its outer eight units on
        /// an outline and a gold frame, the ribbon takes the top edge, and the button underneath
        /// is a full TouchMin rather than the 62 it used to be.
        /// </summary>
        private const float CardWidth = 620f;
        private const float CardHeight = 256f;
        private const float CardInner = 22f;

        /// <summary>
        /// The card's first row: the chapter name on the left, the cleared count on the right, both
        /// inside the plate (R51 - the ribbon that used to straddle the top edge is gone). Rows under
        /// it are placed from the title row's bottom edge.
        /// </summary>
        private const float CardTitleTop = 16f;
        private const float CardTitleHeight = 40f;
        private const float CardCountWidth = 220f;
        private const float CardBarTop = CardTitleTop + CardTitleHeight + Emberforge.Space8;
        private const float CardBarHeight = 22f;
        private const float CardSubTop = CardBarTop + CardBarHeight + 6f;

        /// <summary>
        /// Deliberately NOT static, and deliberately not cached between builds. It was a static
        /// cache, and that quietly defeated an art swap: a Sprite built from an earlier import
        /// survived in the static field, so replacing hub.png and reimporting it changed nothing on
        /// screen and every measurement taken against the new file disagreed with what was actually
        /// being drawn. Build() runs once per screen, so there is nothing to save by caching it.
        /// </summary>
        private Sprite _mapSprite;

        protected override void Build()
        {
            BuildMap();

            // EVERY HUD ELEMENT IS BUILT INSIDE THIS RECT and nothing else is. It is the safe area,
            // so an element anchored to "the top right corner" is anchored to the top right corner
            // of the usable screen rather than to the corner a notch is sitting in. The map is
            // deliberately outside it: layer 0 is art and runs full bleed.
            _hud = UIBuilder.Node("Hud", Root);
            UIBuilder.FillParent(_hud, 0f);
            SafeAreaRect.Attach(_hud);

            BuildMapNodes();
            BuildCampaignCard();
            BuildIdleChest();
            BuildRail();
            BuildNavBar();
            BuildStampLabel();

            _claimPopup = new IdleClaimPopup(App, App.OverlayLayer);
            _claimPopup.Claimed += RefreshIdle;
            _overflowMenu = new HubOverflowMenu(App, App.OverlayLayer, OpenSettings);
        }

        /// <summary>
        /// The painted map, plus the two layers that make it belong to this game: a warm multiply
        /// wash on the painting itself and a dark scrim over it. Drawn first, so every marker and
        /// every HUD element built after it sits on top.
        ///
        /// Envelope-fitted rather than stretched. An AspectRatioFitter in EnvelopeParent mode keeps
        /// the painting's own proportions and lets the screen crop it, which is the only way node
        /// anchors expressed as fractions of the painting can stay on their structures - a stretched
        /// map would slide every structure sideways as the aspect changed.
        /// </summary>
        private void BuildMap()
        {
            Image floor = UIBuilder.Img("Backdrop", Root, TextureLab.White, HubStyle.Plate);
            UIBuilder.FillParent(floor.rectTransform, 0f);

            _map = UIBuilder.Node("Map", Root);
            _map.anchorMin = new Vector2(0.5f, 0.5f);
            _map.anchorMax = new Vector2(0.5f, 0.5f);
            _map.pivot = new Vector2(0.5f, 0.5f);
            _map.anchoredPosition = Vector2.zero;

            {
                Texture2D painting = Resources.Load<Texture2D>("Backgrounds/hub");
                if (painting != null)
                {
                    _mapSprite = Sprite.Create(painting, new Rect(0f, 0f, painting.width, painting.height),
                                               new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
                }
            }

            if (_mapSprite != null)
            {
                // Color.white: no tint at all. This carried a B29E7E multiply wash, which was
                // correct for the pale blue-grey plateau the Hub used to sit on and is wrong for a
                // painting that already arrives warm and saturated.
                Image art = UIBuilder.Img("Art", _map, _mapSprite, Color.white);
                UIBuilder.FillParent(art.rectTransform, 0f);

                AspectRatioFitter fitter = _map.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                // From the sprite's own rect, which is the file's own shape again.
                //
                // This was two hand-declared constants, because the importer used to rescale a
                // non-power-of-two texture per axis and hand back 1536x828 as 2048x1024 - feeding
                // that rect to the fitter rendered the distortion instead of correcting it. The
                // texture-size pass set nPOTScale to 0 on every painting in
                // Assets/Resources/Backgrounds, so the imported texture is the authored size and
                // the rect is the right number to read. Two constants that have to be kept in
                // step with a PNG by hand are two constants that will eventually disagree with it.
                fitter.aspectRatio = _mapSprite.rect.width / _mapSprite.rect.height;
            }
            else
            {
                // No painting on disk. The markers still need a rect to anchor into, so the map
                // becomes an invisible full-screen surface and the Hub degrades to labels on the
                // warm floor rather than throwing or silently building nothing.
                UIBuilder.FillParent(_map, 0f);
            }

            // 0.10. This was 0.5, then 0.32, both tuned against a pale plateau that genuinely
            // needed pulling down. What is left is the little that the chrome needs to seat
            // against, nothing more.
            Image scrim = UIBuilder.Img("Scrim", Root, TextureLab.White,
                                        UITheme.WithAlpha(HubStyle.Plate, HubStyle.ScrimAlpha));
            UIBuilder.FillParent(scrim.rectTransform, 0f);
        }

        /// <summary>
        /// Every structure on the map that leads somewhere. Each is a real button over its painted
        /// structure: ChromeBrass borders on a dark translucent plate, warm off-white text. No node
        /// carries Gold - Gold means currency, progress or the primary action, and a place you can
        /// walk to is none of those.
        ///
        /// THE MARKET STALL IS THE MARKETPLACE (R26). It was the Relic Shop's door for a while; it now opens
        /// the overview of all four shops (Relic Shop, Quartermaster, Proving Armory, Recital Archive), each
        /// a card with its own lock. The node opens at 2-4, the first shop to open, under Feature.Marketplace.
        /// </summary>
        private void BuildMapNodes()
        {
            AddMapNode("HEROES", HeroesX, HeroesY, OpenHeroes, "hub.map.HEROES");
            AddMapNode("SUMMON", SummonX, SummonY, OpenSummon, "hub.map.SUMMON");
            AddMapNode("FORGE", ForgeX, ForgeY, OpenForge, "hub.map.FORGE");
            AddMapNode("MARKETPLACE", MarketX, MarketY, OpenMarketplace, "hub.map.MARKETPLACE");
            AddMapNode("DEEP RECITAL", RecitalX, RecitalY, OpenRecital, "hub.map.RECITAL");
            AddMapNode("PROVING", ProvingX, ProvingY, OpenProving, "hub.map.PROVING");
            // The portal is a place you can walk to, like the others. Its live state and its
            // primary action live in the corner card instead, so the map keeps only the tap target.
            AddMapNode("CAMPAIGN", CampaignX, CampaignY, OpenCampaign, "hub.map.CAMPAIGN");
        }

        /// <summary>
        /// THE building marker. Every structure on the map is this and only this, so they form one
        /// visual system instead of separately-styled tags: one height, one radius, one surface,
        /// one border, one lift above the target, one tail pointing back down at it. Every number
        /// comes from HubStyle - there is no style literal in this method.
        ///
        /// Built here rather than through UIBuilder.MakeButton, because MakeButton builds a BUTTON -
        /// a 3-unit border, a 22-unit corner radius and a white sheen across the top half - and
        /// three pieces of raised-button chrome are exactly what made these tags sit on top of the
        /// painting instead of on the buildings.
        ///
        /// The plate sits HubStyle.LabelLift above its anchor with a tail reaching back down, so
        /// the anchor still points at the building, the building stays visible, and the label reads
        /// as a caption for it rather than a sticker over it.
        /// </summary>
        private UIButton BuildNodePlate(string label, float mx, float my)
        {
            UIButton handle = new UIButton();
            handle.Root = UIBuilder.Node("Node" + label, _map);
            AnchorOnMap(handle.Root, mx, my, HubStyle.LabelWidthFor(label), HubStyle.LabelHeight);
            // Lift off the anchor. Same rule for every marker, which is what makes them a system.
            handle.Root.anchoredPosition = new Vector2(0f, HubStyle.LabelLift);
            handle.Group = handle.Root.gameObject.AddComponent<CanvasGroup>();

            // Tail first, plate second. Unity draws a parent's own graphic before its children, so
            // the plate has to be a CHILD created after the tail for it to bury the tail's top half
            // and leave a triangle pointing down at the building.
            Image tail = EmberforgeUI.MarkerTail(handle.Root, HubStyle.TailSize);
            handle.Background = EmberforgeUI.MarkerPlateChild("Plate", handle.Root);

            handle.Button = handle.Root.gameObject.AddComponent<Button>();
            handle.Button.targetGraphic = handle.Background;
            handle.Button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = UITheme.ButtonHighlight;
            colors.pressedColor = UITheme.ButtonPressed;
            colors.disabledColor = UITheme.ButtonDisabled;
            colors.fadeDuration = UITheme.ButtonFadeDuration;
            colors.colorMultiplier = 1f;
            handle.Button.colors = colors;
            handle.Root.gameObject.AddComponent<ButtonPress>();

            // Cinzel, because a place on the map is a NAME and the kit's title face is what names
            // things. Warm white rather than pure white, so it belongs to the same text set as the
            // panels, and a dark stroke all the way round rather than a drop shadow: this word
            // lands on a sunlit plaza as often as on a shadowed wall, and a shadow on one side
            // only holds a label off a background that happens to be darker than it.
            handle.Label = EmberforgeUI.Title("Text", handle.Root, label, HubStyle.FontLabel,
                                              Emberforge.TextBody, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(handle.Label.rectTransform, Emberforge.Space8);
            UIBuilder.Outlined(handle.Label, Emberforge.Outline, Emberforge.StrokeThin);

            // The map is WIDER than the window at most aspects this game targets, because its
            // AspectRatioFitter envelopes rather than fits, so a marker anchored near the right of
            // the map lands past the right of the screen. This slides such a marker back inside and
            // puts its tail back over its building. The inset is the rail's column, so the frame's
            // one full-height element cannot land on a label either. See HubMarkerClamp.
            handle.Root.gameObject.AddComponent<HubMarkerClamp>()
                  .Bind(handle.Root, _hud, tail.rectTransform,
                        HubStyle.RailOnRightEdge ? 0f : RailColumnWidth,
                        HubStyle.RailOnRightEdge ? RailColumnWidth : 0f);
            return handle;
        }

        /// <summary>How much of its edge the rail occupies, measured the same way the rail is
        /// placed: its own hit width plus the margin outside it and a gap inside it.</summary>
        private static float RailColumnWidth
        {
            get { return HubStyle.HudEdge + HubStyle.TouchMin + HubStyle.RailGap; }
        }

        /// <summary>
        /// One building. Every one carries its FeatureEntryPoints id: the tap asks the unlock
        /// table first, and a locked building keeps its place and its name, greyed, with a
        /// padlock on its left edge and its gold "UNLOCKS AT" tag above it (FeatureLockView).
        /// </summary>
        private UIButton AddMapNode(string label, float mx, float my, System.Action action, string entryId)
        {
            UIButton node = BuildNodePlate(label, mx, my);
            node.OnClick(Gated(entryId, action));
            FeatureLockView.Attach(node.Root, entryId, node.Label,
                                   new Vector2(-HubStyle.LabelWidthFor(label) * 0.5f, 0f),
                                   FeatureLockView.TagSide.Above);
            return node;
        }

        /// <summary>A door's action behind the unlock table: a locked door says its line instead.</summary>
        private System.Action Gated(string entryId, System.Action action)
        {
            return delegate
            {
                if (!FeatureGates.TryEnter(App, entryId)) return;
                action();
            };
        }

        /// <summary>
        /// Places a rect by fraction of the map rather than by pixel offset from a screen edge.
        /// Anything positioned by a fixed offset from one edge drifts relative to anything
        /// positioned from the opposite edge as soon as the logical canvas is not exactly 1080
        /// tall, which on this project's real target aspects it never is.
        /// </summary>
        private static RectTransform AnchorOnMap(RectTransform rt, float mx, float my, float w, float h)
        {
            rt.anchorMin = new Vector2(mx, 1f - my);
            rt.anchorMax = new Vector2(mx, 1f - my);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        // ------------------------------------------------------------------ layer 1: the frame

        /// <summary>
        /// Campaign status and the one button this screen exists to get pressed, as a compact card
        /// pinned to the bottom-right edge above the nav bar.
        ///
        /// Bottom-right specifically, checked against the node anchors rather than picked by eye:
        /// the right edge below the top bar collides with DEEP RECITAL at every aspect, and the
        /// top-left collides with HEROES. Above the nav bar is the one edge slot that clears every
        /// marker at 16:9, 16:10, 20:9 and 19.5:9 alike - the band it sits in is the painting's
        /// empty foreground plaza.
        /// </summary>
        private void BuildCampaignCard()
        {
            RectTransform card = UIBuilder.Node("Campaign", _hud);
            UIBuilder.Box(card, -HubStyle.HudEdge, HubStyle.NavBarHeight + Emberforge.Space16,
                          CardWidth, CardHeight, new Vector2(1f, 0f));
            // R51: the card is the Hub's one focal point, so it is the FLAT plate (the kit's lightest
            // surface: a translucent warm fill and a thin gold edge) and not the framed panel with
            // corner diamonds and a red ribbon. Three loud things in one corner (gold frame, red
            // ribbon, orange button) left the button as one of three; now it is the only saturated one.
            EmberforgeUI.MarkerPlateOn(card);

            // The chapter name is a gold title on the card's first row, level with the count, in
            // place of the ribbon. Best fit down to the size the ribbon used, so a long chapter name
            // shrinks to one line exactly as it did before and nothing gets smaller than it was.
            _chapterLabel = EmberforgeUI.Title("Chapter", card, "", UITheme.FontSmall,
                                               Emberforge.TextGold, TextAnchor.MiddleLeft);
            UIBuilder.Box(_chapterLabel.rectTransform, CardInner, -CardTitleTop,
                          CardWidth - CardInner * 2f - CardCountWidth, CardTitleHeight, new Vector2(0f, 1f));
            UIBuilder.Outlined(_chapterLabel, Emberforge.Outline, Emberforge.StrokeThin);
            _chapterLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _chapterLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _chapterLabel.resizeTextForBestFit = true;
            _chapterLabel.resizeTextMinSize = UITheme.FontTiny - 4;
            _chapterLabel.resizeTextMaxSize = UITheme.FontSmall;

            _chapterCount = EmberforgeUI.Label("Count", card, "", HubStyle.FontSecondary,
                                               Emberforge.TextGold, TextAnchor.MiddleRight);
            UIBuilder.Box(_chapterCount.rectTransform, -CardInner, -CardTitleTop, CardCountWidth, CardTitleHeight,
                          new Vector2(1f, 1f));
            UIBuilder.Outlined(_chapterCount, Emberforge.Outline, Emberforge.StrokeThin);

            _chapterFill = EmberforgeUI.ProgressBar("Bar", card);
            RectTransform barRoot = _chapterFill.rectTransform.parent as RectTransform;
            UIBuilder.Box(barRoot, 0f, -CardBarTop, CardWidth - CardInner * 2f, CardBarHeight,
                          new Vector2(0.5f, 1f));

            _continueSub = EmberforgeUI.Label("Sub", card, "", HubStyle.FontCaption,
                                              Emberforge.TextBody, TextAnchor.UpperCenter);
            UIBuilder.Box(_continueSub.rectTransform, 0f, -CardSubTop,
                          CardWidth - CardInner * 2f, 26f, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(_continueSub, Emberforge.Outline, Emberforge.StrokeThin);

            // The one primary button on the Hub. The only other Ember-orange button anywhere in
            // this screen's four layers is CLAIM inside the idle popup, which is the primary
            // action of that layer - so at any moment there is exactly one orange thing to press.
            _continueButton = EmberforgeUI.Button("Continue", card, "CONTINUE CAMPAIGN",
                                                  HubStyle.FontButton, ForgeButtonKind.Primary);
            // The flat plate has no corner ornaments to clear, so the card's own padding is the inset.
            UIBuilder.Box(_continueButton.Root, 0f, CardInner,
                          CardWidth - CardInner * 2f,
                          HubStyle.TouchMin, new Vector2(0.5f, 0f));
            _continueButton.OnClick(Gated("hub.card.Campaign", OpenCampaign));
        }

        /// <summary>
        /// The idle cache, as a chest beside the campaign card with the stored gold in a bubble
        /// over it. Everything else the old panel said - the elapsed time, the cap, the two rates -
        /// is one tap away in the claim popup, which is where five lines of detail about a number
        /// belong.
        /// </summary>
        private void BuildIdleChest()
        {
            _chest = HudWidgets.ChestButton("IdleChest", _hud, Gated("hub.chest", OpenClaim));
            // R51: the chest joins the bottom row, level with the nav discs and just left of them,
            // instead of floating in the plaza beside the card. Its bubble still hangs above it.
            float navWidth = HubStyle.NavCellWidth * 5f + HubStyle.NavCellGap * 4f;
            float discCentre = HubStyle.NavBottomMargin + HubStyle.NavCellHeight - HubStyle.NavIcon * 0.5f;
            UIBuilder.Box(_chest.Root, -(navWidth * 0.5f + HubStyle.NavCellGap + HubStyle.ChestSize * 0.5f),
                          discCentre - HubStyle.ChestSize * 0.5f,
                          HubStyle.ChestSize, HubStyle.ChestSize, new Vector2(0.5f, 0f));
        }

        /// <summary>
        /// The rail: the doors that are neither places on the map nor the five things the nav bar
        /// carries. Which edge it hangs off is HubStyle.RailOnRightEdge's to say, and it says the
        /// left, because the right edge is where the painting puts the Proving's gatehouse.
        ///
        /// MAIL IS A PLACEHOLDER AND IS DRAWN AS ONE. There is no mail system in this build - no
        /// service, no screen, no data - so the entry is present and disabled rather than absent:
        /// the rail's shape is part of the layout being verified here, and a rail that grows a
        /// fourth button later would need every measurement in this pass redone. It says what it
        /// is when tapped rather than doing nothing.
        /// </summary>
        private void BuildRail()
        {
            float step = HubStyle.TouchMin + HubStyle.RailGap;
            float top = HubStyle.RailTop;

            _friendsButton = HudWidgets.RailButton("Friends", _hud, HudSkin.IconFriends,
                                                   TextureLab.Heart(64), HubStyle.LabelText,
                                                   Gated("hub.rail.Friends", OpenFriends));
            Place(_friendsButton.Root, top);
            Recede(_friendsButton);
            // On the disc rather than its larger hit rect, so the padlock and the one-time dot sit
            // on the art the way the pending-work badge does.
            FeatureLockView.Attach(_friendsButton.Disc.rectTransform, "hub.rail.Friends", null,
                                   new Vector2(HubStyle.RailDisc * 0.3f, -HubStyle.RailDisc * 0.3f),
                                   FeatureLockView.TagSide.Right);

            HudElement mail = HudWidgets.RailButton("Mail", _hud, HudSkin.IconMail,
                                                    TextureLab.Envelope(64), HubStyle.LabelText,
                                                    NotSoonEnough("Mail"));
            Place(mail.Root, top + step);
            mail.Group.alpha = HubStyle.DimAlpha;

            _eventsButton = HudWidgets.RailButton("Events", _hud, HudSkin.IconEvents,
                                                  TextureLab.Sigil(8, 64), UITheme.Success,
                                                  OpenEvents);
            Place(_eventsButton.Root, top + step * 2f);
            Recede(_eventsButton);
            // A caption under the disc, in the Store door's own style, so the door reads as Events and not as one more icon.
            Text eventsCaption = EmberforgeUI.Label("EventsCaption", _eventsButton.Root, "EVENTS", UITheme.FontTiny - 6,
                                                    Emberforge.TextGold, TextAnchor.LowerCenter);
            UIBuilder.Box(eventsCaption.rectTransform, 0f, -2f, HubStyle.TouchMin, 22f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(eventsCaption, Emberforge.Outline, Emberforge.StrokeThin);
            eventsCaption.raycastTarget = false;
            RefreshEventsBadge();

            // THE DAILY ORDERS DOOR, present only with DailyLoopSwitch on. It takes the slot above
            // More and pushes More one slot down; with the switch off there is no fifth button,
            // nothing moves, and the rail is the four it always was. A dot says something is ready.
            int slot = 3;

            // THE STORE'S DOOR, present only with StoreSwitch on, in the slot above Orders. A gold caption
            // sits under the disc so it reads as a shop and not as one more icon. A dot says a payout of
            // something bought is ready (a ledger day, a growth milestone, a paid pass tier).
            if (StoreSwitch.Enabled)
            {
                _storeButton = HudWidgets.RailButton("Store", _hud, HudSkin.IconStore, TextureLab.CoinIcon(64),
                                                     UITheme.Gold, OpenStore);
                Place(_storeButton.Root, top + step * slot);
                Recede(_storeButton);
                Text caption = EmberforgeUI.Label("StoreCaption", _storeButton.Root, "STORE", UITheme.FontTiny - 6,
                                                  Emberforge.TextGold, TextAnchor.LowerCenter);
                UIBuilder.Box(caption.rectTransform, 0f, -2f, HubStyle.TouchMin, 22f, new Vector2(0.5f, 0f));
                UIBuilder.Outlined(caption, Emberforge.Outline, Emberforge.StrokeThin);
                caption.raycastTarget = false;
                slot++;
                RefreshStoreBadge();
            }

            if (DailyLoopSwitch.Enabled)
            {
                _ordersButton = HudWidgets.RailButton("Orders", _hud, HudSkin.IconOrders, TextureLab.Check(64),
                                                      Emberforge.TextGold, OpenOrders);
                Place(_ordersButton.Root, top + step * slot);
                Recede(_ordersButton);
                slot++;
                RefreshOrdersBadge();
            }

            HudElement more = HudWidgets.RailButton("More", _hud, HudSkin.IconMore,
                                                    TextureLab.Ellipsis(64), HubStyle.LabelText,
                                                    OpenOverflow);
            _overflowButtonTop = top + step * slot;
            Place(more.Root, _overflowButtonTop);
            Recede(more);
        }

        /// <summary>R51: a rail door is the Hub's third tier, so its plate and glyph sit at
        /// HubStyle.RailAlpha. The dot and the caption are separate graphics and stay full strength.</summary>
        private static void Recede(HudElement element)
        {
            if (element.Disc != null)
            {
                Color c = element.Disc.color;
                c.a *= HubStyle.RailAlpha;
                element.Disc.color = c;
            }
            if (element.Glyph != null)
            {
                Color c = element.Glyph.color;
                c.a *= HubStyle.RailAlpha;
                element.Glyph.color = c;
            }
        }

        /// <summary>One rail slot. The edge it hangs off is HubStyle.RailOnRightEdge's to decide,
        /// and every other number here is shared with the clamp that reserves its column.</summary>
        private void Place(RectTransform rect, float top)
        {
            bool right = HubStyle.RailOnRightEdge;
            UIBuilder.Box(rect, right ? -HubStyle.HudEdge : HubStyle.HudEdge, -top,
                          HubStyle.TouchMin, HubStyle.TouchMin,
                          new Vector2(right ? 1f : 0f, 1f));
        }

        /// <summary>
        /// The bottom navigation bar: the five doors a player uses every session, in one row,
        /// centred on the bottom edge.
        ///
        /// Centred rather than cornered, and without a full-width backing plate. The corners are
        /// taken - the campaign card and the chest are in one, and a bar spanning the whole width
        /// would put an opaque band across the painting's foreground to hold five discs that
        /// already carry their own plates.
        ///
        /// HEROES opens the FORMATION screen, not the collection: changing who fights is what a
        /// player comes here to do, and browsing every hero in the game is one tap further in from
        /// there. That is unchanged from the marker this row's entry mirrors.
        /// </summary>
        private void BuildNavBar()
        {
            RectTransform nav = UIBuilder.Node("Nav", _hud);
            UIBuilder.Box(nav, 0f, HubStyle.NavBottomMargin,
                          HubStyle.NavCellWidth * 5f + HubStyle.NavCellGap * 4f, HubStyle.NavCellHeight,
                          new Vector2(0.5f, 0f));
            UIBuilder.HStack(nav, HubStyle.NavCellGap, new RectOffset(0, 0, 0, 0), TextAnchor.LowerCenter);

            NavCell("Heroes", nav, "HEROES", HudSkin.IconHeroes,
                    TextureLab.Helm(64), UITheme.ChromeBrass, OpenHeroes, "hub.nav.Heroes");
            NavCell("Bag", nav, "BAG", HudSkin.IconBag,
                    TextureLab.Plate(64), UITheme.ChromeBrass, OpenInventory, "hub.nav.Bag");
            NavCell("Summon", nav, "SUMMON", HudSkin.IconSummon,
                    TextureLab.Spark(64), UITheme.Shard, OpenSummon, "hub.nav.Summon");
            NavCell("Guild", nav, "GUILD", HudSkin.IconGuild,
                    TextureLab.Sigil(2, 64), UITheme.ChromeBrass, OpenGuild, "hub.nav.Guild");
            NavCell("Campaign", nav, "CAMPAIGN", HudSkin.IconCampaign,
                    TextureLab.Swords(64), UITheme.Ember, OpenCampaign, "hub.nav.Campaign");
        }

        /// <summary>One nav cell behind the unlock table, with its lock dress: the caption greys,
        /// the padlock sits on the disc and the tag rides above the cell.</summary>
        private void NavCell(string name, RectTransform nav, string caption, string slot, Sprite glyph,
                             Color tint, System.Action action, string entryId)
        {
            HudElement cell = HudWidgets.NavButton(name, nav, caption, slot, glyph, tint, Gated(entryId, action));
            FeatureLockView.Attach(cell.Root, entryId, cell.Caption,
                                   new Vector2(HubStyle.NavIcon * 0.3f, HubStyle.NavCellHeight * 0.5f - HubStyle.NavIcon * 0.75f),
                                   FeatureLockView.TagSide.Above);
        }

        /// <summary>
        /// Which build this is, in the bottom-left corner: version and commit, from
        /// Game.BuildStamp. Every report from a tester has to name a build, and this is where they
        /// read it off.
        ///
        /// The bottom-left is the one corner of the frame nothing else claims. The rail hangs from
        /// the top of the left edge and ends well above it on the shortest canvas, the nav bar is
        /// centred, and the campaign card and chest sit bottom right. It stands on the nav bar's
        /// own bottom margin so it shares a baseline with the row beside it, and it is the
        /// quietest text on the screen - outlined only so it stays readable over the painting.
        /// Not a button and not a raycast target: it must never eat a tap meant for the map.
        /// </summary>
        private void BuildStampLabel()
        {
            Text stamp = EmberforgeUI.Label("BuildStamp", _hud, Game.BuildStamp.Label, UITheme.FontTiny - 2,
                                            Emberforge.TextQuiet, TextAnchor.LowerLeft);
            UIBuilder.Box(stamp.rectTransform, HubStyle.HudEdge, HubStyle.NavBottomMargin, 340f, 26f,
                          new Vector2(0f, 0f));
            UIBuilder.Outlined(stamp, Emberforge.Outline, Emberforge.StrokeThin);
            stamp.raycastTarget = false;
        }

        /// <summary>
        /// The shared "there is no screen behind this yet" response. One sentence, one place, so
        /// every placeholder says the same thing and none of them silently swallows the tap.
        /// </summary>
        private System.Action NotSoonEnough(string label)
        {
            string title = label.Substring(0, 1) + label.Substring(1).ToLowerInvariant();
            return delegate
            {
                App.Audio.Tap();
                App.Notify(title + " is not open yet.");
            };
        }

        // ------------------------------------------------------------------ behaviour

        public override void OnShow()
        {
            RefreshIdle();
            RefreshContinue();
            RefreshChapter();
            App.RefreshHud();

            // The Hub's HUD is a frame around a painting, so the top bar's full-width plate comes
            // off while this screen is up and goes back on for every page that needs a header
            // seated on something. Paired with the restore in OnHide, never left to the next
            // screen to remember.
            App.SetTopBarBackingVisible(false);

            RefreshFriendBadge();
            RefreshOrdersBadge();
            RefreshStoreBadge();
            RefreshEventsBadge();
            FeatureLockView.RefreshAll(App, Root);
            _tick = 0f;
        }

        public override void OnHide()
        {
            App.SetTopBarBackingVisible(true);
        }

        private void RefreshChapter()
        {
            List<ChapterDef> chapters = App.Session.Content.Chapters;
            if (chapters.Count == 0)
            {
                _chapterLabel.text = "NO CAMPAIGN CONTENT";
                _chapterCount.text = "";
                EmberforgeUI.SetProgress(_chapterFill, 0f);
                return;
            }

            ChapterDef chapter = chapters[0];
            int cleared = 0;
            for (int i = 0; i < chapter.Stages.Count; i++)
                if (App.Session.Profile.HasClearedStage(chapter.Stages[i].Id)) cleared++;

            _chapterLabel.text = "CHAPTER " + chapter.Number + "  -  " + chapter.Name.ToUpperInvariant();
            _chapterCount.text = cleared + " / " + chapter.Stages.Count + " CLEARED";
            EmberforgeUI.SetProgress(_chapterFill,
                chapter.Stages.Count > 0 ? cleared / (float)chapter.Stages.Count : 0f);
        }

        private void Update()
        {
            if (Root == null || !Root.gameObject.activeInHierarchy) return;
            _tick += Time.unscaledDeltaTime;
            if (_tick < 1f) return;
            _tick = 0f;
            RefreshIdle();
            RefreshOrdersBadge();
            RefreshStoreBadge();
            RefreshEventsBadge();
            // The popup is not a MonoBehaviour and does not tick itself: while it is open, this is
            // what keeps its stored time honest under the player's eyes.
            if (_claimPopup != null && _claimPopup.IsOpen) _claimPopup.Refresh();
        }

        /// <summary>
        /// True when this screen can safely read the world. Reports the first failure LOUDLY and
        /// exactly once, then stays quiet.
        ///
        /// This is a guard, but deliberately not a silent one, because the state it checks for is
        /// state that genuinely SHOULD exist: GameApp assigns Session in Awake before it builds the
        /// canvas, the router, or any screen. Swallowing that quietly would trade a visible crash
        /// for an invisible one - a Hub that silently stops updating its idle numbers - which is
        /// strictly worse to diagnose. What it buys is the difference between one actionable error
        /// and a once-per-second exception storm.
        /// </summary>
        private bool SessionReady(string caller)
        {
            if (App != null && App.Session != null) return true;

            if (!_sessionWarned)
            {
                _sessionWarned = true;
                Debug.LogError("MainMenuScreen." + caller + ": "
                    + (App == null ? "App" : "App.Session")
                    + " is null while the Hub is live and ticking, so idle rewards have stopped "
                    + "updating. This screen only exists because ScreenRouter.Build ran, which "
                    + "happens AFTER Session is assigned, so a boot that got this far cannot "
                    + "have left Session null. Boot state: GameApp.BootStage='"
                    + Game.GameApp.BootStage + "' completed=" + Game.GameApp.BootCompleted
                    + " failed=" + Game.GameApp.BootFailed
                    + ". Reported once; the per-second retry is suppressed from here.");
            }
            return false;
        }

        /// <summary>
        /// The chest's bubble, which is the whole of what the map still says about idle income.
        /// Gold only: the cache pays gold and shards, and two numbers in a bubble over a chest is
        /// a panel again. The shards are in the popup with everything else.
        /// </summary>
        private void RefreshIdle()
        {
            if (!SessionReady("RefreshIdle")) return;
            if (_chest == null) return;

            IdleReport report = App.Session.PeekIdle(TimeUtil.NowUnix());
            _chest.Caption.text = HudTopBar.Format(report.Gold);
            // The CHEST stays at full strength whether or not there is anything in it - it is a
            // door, and a door that fades out when the room behind it is empty is a door players
            // stop finding. It is the NUMBER that goes quiet, which is the honest signal: gold
            // when there is something to collect, the muted tone when there is not.
            _chest.Caption.color = report.HasAnything ? HubStyle.ValueGold : HubStyle.TextTertiary;
        }

        /// <summary>The dot on the Orders rail button: lit while anything on the board can be
        /// claimed, read from the same LoopBoard the Orders page draws, so it cannot disagree.
        /// There is no button, and so nothing to do, with the switch off.</summary>
        private void RefreshOrdersBadge()
        {
            if (_ordersButton == null || !SessionReady("RefreshOrdersBadge")) return;
            _ordersButton.SetBadge(DailyLoop.Board(App.Session, TimeUtil.NowUnix()).AnythingToClaim ? 1 : 0);
        }

        private void RefreshContinue()
        {
            StageDef next = App.Session.NextStage;
            if (next == null)
            {
                _continueButton.SetText("CAMPAIGN");
                _continueSub.text = "No campaign content loaded.";
                return;
            }

            bool complete = App.Session.CampaignComplete;
            _continueButton.SetText(complete ? "REPLAY CAMPAIGN" : "CONTINUE CAMPAIGN");
            _continueSub.text = (complete ? "Chapter cleared  -  " : "Next: ") + next.DisplayCode + "  " + next.Name;
        }

        /// <summary>
        /// The dot on the Friends rail button: how many requests are waiting on THIS player.
        ///
        /// Read from the same SocialService call the friends list itself uses, and counted by the
        /// same flag that screen sorts on, so the badge cannot disagree with the list it opens. It
        /// asks only when there is a signed-in cloud to ask; offline, or on a build with no
        /// account service at all, the badge simply never appears.
        /// </summary>
        private void RefreshFriendBadge()
        {
            if (_friendsButton == null) return;
            _friendsButton.SetBadge(0);

            CloudCoordinator cloud = App.Cloud;
            if (cloud == null || !cloud.SignedIn) return;
            StartCoroutine(FriendBadgeRoutine());
        }

        private IEnumerator FriendBadgeRoutine()
        {
            List<FriendEntry> fetched = null;
            yield return App.Cloud.Social.ListFriends(delegate (List<FriendEntry> list, string message)
            {
                fetched = list;
            });

            if (fetched == null) yield break;
            int waiting = 0;
            for (int i = 0; i < fetched.Count; i++)
                if (fetched[i].AwaitingMyAnswer) waiting++;
            if (_friendsButton != null) _friendsButton.SetBadge(waiting);
        }

        // ------------------------------------------------------------------ actions

        private void OpenClaim()
        {
            App.Audio.Tap();
            _claimPopup.Show();
        }

        private void OpenOverflow()
        {
            App.Audio.Tap();
            _overflowMenu.Show(_overflowButtonTop);
        }

        private void OpenSettings()
        {
            App.Panels.ShowProfile("SETTINGS");
        }

        /// <summary>
        /// The Hub's heroes door opens the BANNER, not the collection. Changing who fights is what
        /// a player comes here to do; browsing every hero in the game is reachable one tap further
        /// in, from a button on that screen.
        /// </summary>
        private void OpenHeroes()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Formation);
        }

        private void OpenCampaign()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Campaign);
        }

        private void OpenSummon()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Summon);
        }

        /// <summary>The market stall opens the Marketplace (R26), the overview of the four shops. The Relic Shop is
        /// one card inside it and keeps its own lock.</summary>
        private void OpenMarketplace()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Marketplace);
        }

        private void OpenInventory()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Inventory);
        }

        /// <summary>The Events door opens the live events page (the weekly and monthly events) when it exists, and
        /// the limited-time events page as it always did when LiveEventsSwitch is off.</summary>
        private void OpenEvents()
        {
            App.Audio.Tap();
            Router.Show(Router.LiveEvents != null ? ScreenId.LiveEvents : ScreenId.Events);
        }

        /// <summary>The dot on the Events rail button: lit while a live event quest or tier can be claimed, read from
        /// the same LiveEvents.View the page draws and without writing the save. Dark with the switch off.</summary>
        private void RefreshEventsBadge()
        {
            if (_eventsButton == null || !SessionReady("RefreshEventsBadge")) return;
            _eventsButton.SetBadge(LiveEvents.AnythingToClaim(App.Session, TimeUtil.NowUnix()) ? 1 : 0);
        }

        private void OpenStore()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Store);
        }

        /// <summary>The dot on the Store rail button: lit while something bought can be claimed, read from the
        /// same StoreView the Store draws, so it cannot disagree.</summary>
        private void RefreshStoreBadge()
        {
            if (_storeButton == null || !SessionReady("RefreshStoreBadge")) return;
            _storeButton.SetBadge(StoreOffers.View(App.Session, TimeUtil.NowUnix()).AnythingToClaim ? 1 : 0);
        }

        private void OpenOrders()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Orders);
        }

        private void OpenProving()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Proving);
        }

        private void OpenRecital()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Recital);
        }

        private void OpenForge()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Forge);
        }

        /// <summary>Friends open as a tab of the player panel rather than as a page, so the Hub
        /// stays behind them - the rail is a glance, not a journey.</summary>
        private void OpenFriends()
        {
            App.Audio.Tap();
            App.Panels.ShowProfile("FRIENDS");
        }

        private void OpenGuild()
        {
            App.Audio.Tap();
            App.Panels.ShowGuild();
        }

        // ------------------------------------------------------------------ capture hooks

        /// <summary>
        /// Opens one of this screen's layers by name, for the capture tool. Editor only: a screen
        /// that could be driven by a string from a shipped build is a screen with a second,
        /// untested set of entry points.
        ///
        /// It exists because a layout check has to see the layers, not just the frame - the claim
        /// popup, the player panel and the overflow menu each cover part of the screen, and
        /// "nothing overlaps" is only proven for a state that was actually captured.
        /// </summary>
#if UNITY_EDITOR
        public void OpenLayerForCapture(string which)
        {
            switch (which)
            {
                case "profile": App.Panels.ShowProfile("PROFILE"); break;
                case "account": App.Panels.ShowProfile("ACCOUNT"); break;
                case "settings": App.Panels.ShowProfile("SETTINGS"); break;
                case "guild": App.Panels.ShowGuild(); break;
                case "claim": _claimPopup.Show(); break;
                case "menu": _overflowMenu.Show(_overflowButtonTop); break;
                case "gallery": _overflowMenu.OpenDesignGallery(); break;

                // The confirm dialog, on the one question in the game that actually asks it. It
                // is opened through App.Modal rather than built here, so what a capture sees is
                // the dialog every screen shares - and it is opened on the RESET copy because
                // that is the only two-answer dialog with real words in it.
                // The words are SettingsScreen.ResetBody's, on the cloud copy, which says the most.
                // The answers stay unwired: PlayModeScreenshot runs on the real identity, and a
                // capture door must not be one tap from wiping a real save.
                case "dialog":
                    App.Modal.ShowConfirm("RESET PROGRESS", SettingsScreen.ResetBody(true),
                                          "RESET", "KEEP PLAYING", null, null);
                    break;

                case "heroes": Router.Show(ScreenId.Heroes); break;

                // Opens two layers, then presses back twice and once more for luck. What the
                // capture then shows is the bare Hub: the first back closed the menu, the second
                // closed the panel under it, and the third did nothing at all, which is the whole
                // of the back rule in one photograph.
                case "back":
                    App.Panels.ShowProfile("PROFILE");
                    _overflowMenu.Show(_overflowButtonTop);
                    App.HandleBack();
                    App.HandleBack();
                    App.HandleBack();
                    break;
            }
        }
#endif
    }
}
