using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.UI.Screens
{
    /// <summary>Home base: identity, the current squad, idle income and the three ways forward.</summary>
    public sealed class MainMenuScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.MainMenu; } }

        private RectTransform _teamRow;
        private Text _idleAmounts;
        private Text _idleTimer;
        private Text _idleRate;
        private UIButton _claimButton;
        private UIButton _continueButton;
        private Text _continueSub;
        private float _tick;

        private Text _chapterLabel;
        private Image _chapterFill;
        private Text _chapterCount;

        // ---------- the Hub's own surfaces ----------
        // A Hub-scoped override of the shared surface ramp, and nothing more. UITheme.Backdrop,
        // Panel, PanelRaised and Border are NOT touched: every other screen keeps the violet-toned
        // chrome and the Ember-orange accents it has today, and this screen stops matching them.
        //
        // That seam is intended, not an oversight. The Hub is the only screen with a fiction that
        // demands its own material (DESIGN-WORLD, "The Cinder and the Keeper" - the office "takes
        // no side among the six"), and it is the only screen the reskin covers. Warming the shared
        // constants instead would drag Campaign, Inventory, RelicShop and every PrimaryButton in
        // the game along with it, which is a far larger visual change than this one and would land
        // with none of those screens actually reviewed.
        //
        // HubPanelSunken is derived rather than chosen: it sits at the same ratio to HubPanel
        // (about 0.72) that UITheme.PanelSunken sits at to UITheme.Panel, so the Hub's internal
        // depth relationship reads like every other screen's.
        //
        // Two of the six tones this set was introduced with are gone: the border pair, HubBorder
        // 3A3120 and its derived HubBorderSoft 2A2317. They existed to outline the two full-width
        // cards this screen used to be built from, and the map layout has no cards - every plate on
        // it is a node outlined in ChromeBrass, which is what ChromeBrass is for. Keeping two
        // surface-border constants with no call site would have been dead palette. If a bordered
        // card ever returns to this screen, 3A3120 is the value it wants.
        //
        // None of these is ChromeBrass. ChromeBrass is an accent that sits ON these surfaces
        // (outlines, rings, dividers); these four are the surfaces themselves.
        private static readonly Color HubBackdrop = UITheme.Hex("14121A");
        private static readonly Color HubPanel = UITheme.Hex("1C1710");
        private static readonly Color HubPanelRaised = UITheme.Hex("2B2318");
        private static readonly Color HubPanelSunken = UITheme.Hex("14100B");

        /// <summary>
        /// Multiplied over the painted map. The Hub is a warm dark screen and a full-brightness
        /// painting behind dark chrome flattens every plate on top of it; the wash pulls the art
        /// into the same family as HubPanel without repainting anything.
        /// </summary>
        private static readonly Color MapWash = UITheme.Hex("B29E7E");

        /// <summary>
        /// The authored dimensions of hub.png, declared rather than read back off the texture.
        ///
        /// The importer does not hand back the file: a non-power-of-two texture is rescaled per
        /// axis, so 1536x828 arrives as a 2048x1024 Texture2D and its 1.855:1 becomes 2.0:1.
        /// Feeding the sprite's own rect to the fitter would render that distortion instead of
        /// correcting it. Driving the fitter from these two numbers renders the squashed texture
        /// back at the shape it was painted at.
        ///
        /// npotScale: 0 does not avoid this - it was tried on the previous art and reverted,
        /// because the width is not a multiple of four and the compressed format cannot hold it.
        ///
        /// These two are the only place in this screen that knows the source file's size. The
        /// previous art also needed a trim, because it was a flattened mockup screenshot with a
        /// 60px band of white below the painting; this one is a real background plate with no band,
        /// so the sprite is simply the whole texture and the trim is gone.
        /// </summary>
        private const float SourceFileWidth = 1536f;
        private const float SourceFileHeight = 828f;

        // ---------- map node anchors ----------
        // Fractions of the PAINTING, x then y from its top-left - not of the screen. Every node is
        // parented to the map image and anchored by these, so a node stays on its structure at any
        // aspect ratio. The map is envelope-fitted and centred, so the screen crops it rather than
        // stretching it, and the crop is symmetric on both axes.
        //
        // RE-MEASURED against the clean plate. The previous set was measured against a flattened
        // mockup screenshot whose art region was 2.292:1; this file is 1.855:1 and uncropped, so
        // the visible window is a different shape and not one of the six old values transferred.
        // This is the second wholesale re-measure on this branch, for the same underlying reason
        // both times: an anchor is only as current as the image it was measured against.
        //
        // Measured by rendering a coordinate grid over the file, proposing a point per structure,
        // then re-cropping each at 2x with a crosshair on it and checking one by one. The grid
        // alone is not enough: this pass's first proposal put SUMMON in the sky beside the obelisk
        // and DEEP RECITAL on the brazier next to the cave, and only the crosshair pass caught it.
        private const float HeroesX = 0.175f, HeroesY = 0.300f;     // the barracks, above its banner
        private const float ForgeX = 0.332f, ForgeY = 0.430f;       // the smithy roof, over the arch
        private const float MarketX = 0.485f, MarketY = 0.520f;     // the market stall canopy
        private const float SummonX = 0.578f, SummonY = 0.330f;     // the rune obelisk
        private const float CampaignX = 0.680f, CampaignY = 0.440f; // the fire portal's arch
        private const float RecitalX = 0.800f, RecitalY = 0.450f;   // the purple cave mouth

        // ---------- the bottom row ----------
        // One size and one spacing rule for every circular cell along the bottom, dock and squad
        // alike. They used to disagree in four separate ways at once - 96 vs 104 icons, 18 vs 16
        // spacing, 132 vs 160 cells, 150 vs 152 row heights - which is what made the left of the
        // row look smaller and tighter than the right. None of those differences was a decision;
        // they were four literals written at different times.
        //
        // BadgeCellHeight reserves room for the tallest cell there is - icon, caption AND a SOON
        // tag - so every cell is the same height whether or not it carries one. Sizing the cell to
        // its contents instead let the locked badges' tag hang below the row and eat into the
        // bottom margin, which is the clipping.
        private const float BadgeSize = 104f;    // icon diameter
        private const float BadgeGap = 20f;      // gap between cells
        private const float BadgeCell = 132f;    // cell width
        private const float BadgeCellHeight = 172f;

        /// <summary>
        /// Distance from the bottom of the screen to the bottom of the badge row. Deliberately
        /// larger than ScreenPadding: this edge is where phones put gesture bars and rounded
        /// corners, and the row underneath everything else is the one that pays for a margin that
        /// is merely "the usual padding".
        /// </summary>
        private const float BottomSafeMargin = 48f;

        /// <summary>
        /// Total height the bottom row occupies, margin included. Anything stacked above it
        /// measures from this one number rather than carrying its own copy, so the rows and the
        /// card above them cannot drift apart.
        /// </summary>
        private const float BottomRowHeight = BottomSafeMargin + BadgeCellHeight;

        /// <summary>Width of a five-cell row at the shared metrics.</summary>
        private const float BadgeRowWidth = BadgeCell * 5f + BadgeGap * 4f;

        private RectTransform _map;

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
            BuildMapNodes();
            BuildCampaignCard();
            BuildIdlePanel();
            BuildBadgeDock();
            BuildSquadStrip();
            BuildCinderMark();
            BuildOptions();
        }

        /// <summary>
        /// The painted map, plus the two layers that make it belong to this game: a warm multiply
        /// wash on the painting itself and a dark scrim over it. Drawn first, so every node and
        /// panel built after it sits on top.
        ///
        /// Envelope-fitted rather than stretched. An AspectRatioFitter in EnvelopeParent mode keeps
        /// the painting's own proportions and lets the screen crop it, which is the only way node
        /// anchors expressed as fractions of the painting can stay on their structures - a stretched
        /// map would slide every structure sideways as the aspect changed.
        /// </summary>
        private void BuildMap()
        {
            Image floor = UIBuilder.Img("Backdrop", Root, TextureLab.White, HubBackdrop);
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
                Image art = UIBuilder.Img("Art", _map, _mapSprite, MapWash);
                UIBuilder.FillParent(art.rectTransform, 0f);

                AspectRatioFitter fitter = _map.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                // From the AUTHORED dimensions, not from _mapSprite.rect. The sprite's rect is in
                // imported-texture space, which the importer has already stretched by a different
                // factor per axis; feeding that back in would render the distortion instead of
                // correcting it.
                fitter.aspectRatio = SourceFileWidth / SourceFileHeight;
            }
            else
            {
                // No painting on disk. The nodes still need a rect to anchor into, so the map
                // becomes an invisible full-screen surface and the Hub degrades to labels on the
                // warm floor rather than throwing or silently building nothing.
                UIBuilder.FillParent(_map, 0f);
            }

            // 0.32, not the 0.5 this started at. A multiply wash preserves the painting's contrast
            // because it scales every value by the same factor; a flat scrim does not - it lifts the
            // blacks toward itself and flattens the whole image. At 0.5 the plateau, the sky and the
            // structures had collapsed into one brown field with the silhouettes barely separable,
            // which is a worse outcome than the cool palette this was correcting.
            Image scrim = UIBuilder.Img("Scrim", Root, TextureLab.White, UITheme.WithAlpha(HubBackdrop, 0.32f));
            UIBuilder.FillParent(scrim.rectTransform, 0f);

            // One soft, colourless ambient lift, the same cue the Cinder theme pass introduced -
            // kept because it is the Hub's own light, not the map's. Static, per DESIGN-ART-BIBLE
            // section 5: nothing about the Cinder animates at rest.
            Image glow = UIBuilder.Img("AmbientGlow", Root, TextureLab.Glow(512, 1.6f),
                                       UITheme.WithAlpha(UITheme.CinderGlass, 0.10f));
            UIBuilder.Box(glow.rectTransform, 0f, -180f, 1500f, 1100f, new Vector2(0.5f, 1f));
        }

        /// <summary>
        /// Every structure on the map that leads somewhere. Each is a real button over its painted
        /// structure, coloured from this branch's palette rather than from the mockup's raw hexes:
        /// ChromeBrass borders on a dark translucent plate, warm off-white text. No node carries
        /// Gold - Gold means currency, progress or the primary action, and a place you can walk to
        /// is none of those. The one Gold thing on this map is the Continue action, which is the
        /// primary action and already owned that colour.
        /// </summary>
        private void BuildMapNodes()
        {
            AddMapNode("HEROES", HeroesX, HeroesY, OpenHeroes);
            AddMapNode("SUMMON", SummonX, SummonY, OpenSummon);
            AddMapNode("FORGE", ForgeX, ForgeY, OpenForge);
            AddMapNode("DEEP RECITAL", RecitalX, RecitalY, OpenRecital);
            // The portal is a place you can walk to, like the others. Its live state and its
            // primary action live in the corner card instead, so the map keeps only the tap target.
            AddMapNode("CAMPAIGN", CampaignX, CampaignY, OpenCampaign);

            // Painted into the map for a future that does not exist yet, exactly as the mockup
            // treats it. Same treatment as the dock's two unbuilt entries: it looks like a place,
            // it says SOON, and tapping it explains itself rather than doing nothing.
            AddLockedNode("MARKETPLACE", MarketX, MarketY);
        }

        private UIButton AddMapNode(string label, float mx, float my, System.Action action)
        {
            UIButton node = UIBuilder.MakeButton("Node" + label, _map, label, UITheme.FontSmall,
                                                 UITheme.WithAlpha(HubBackdrop, 0.82f),
                                                 UITheme.WithAlpha(UITheme.ChromeBrass, 0.75f),
                                                 UITheme.TextPrimary);
            AnchorOnMap(node.Root, mx, my, NodeWidthFor(label), 62f);
            node.OnClick(action);
            UIBuilder.Shadowed(node.Label, UITheme.InkArt, 2f);
            return node;
        }

        /// <summary>
        /// A structure with no screen behind it. Dimmer plate, dimmer text, a SOON tag under it, and
        /// a tap that says so out loud. The tag is ChromeBrass, not Gold: "not built yet" is a
        /// status, not a reward, and Gold is spoken for.
        /// </summary>
        private void AddLockedNode(string label, float mx, float my)
        {
            UIButton node = UIBuilder.MakeButton("Node" + label, _map, label, UITheme.FontSmall,
                                                 UITheme.WithAlpha(HubBackdrop, 0.7f),
                                                 UITheme.WithAlpha(UITheme.ChromeBrass, 0.4f),
                                                 UITheme.TextMuted);
            AnchorOnMap(node.Root, mx, my, NodeWidthFor(label), 62f);
            node.OnClick(NotSoonEnough(label));

            AddSoonTag(node.Root, 0f, -24f, new Vector2(0.5f, 0f));
        }

        /// <summary>
        /// The "not built yet" tag.
        ///
        /// Deliberately NOT UIBuilder.Chip. Chip paints its background, its border and its text
        /// from one tint - a 22% wash, a 75% edge and 100% text, all the same hue. On a dark panel
        /// that reads fine. Over a sunlit painting it does not: ChromeBrass text on a ChromeBrass
        /// wash has almost no contrast against itself, and the result renders as an empty pale
        /// rounded rectangle with no legible word in it. That was the stray artifact under
        /// MARKETPLACE - not a broken sprite or a failed load, just a widget camouflaging its own
        /// label.
        ///
        /// A dark opaque plate carries the text instead, so the tag reads at ChromeBrass's measured
        /// 4.7:1 against HubBackdrop rather than against a wash of itself. Chip is left alone;
        /// other screens use it over panels where its single-tint assumption holds.
        /// </summary>
        private static RectTransform AddSoonTag(RectTransform parent, float x, float y, Vector2 anchor)
        {
            RectTransform tag = UIBuilder.Node("Soon", parent);
            UIBuilder.Box(tag, x, y, 96f, 28f, anchor);

            Image plate = tag.gameObject.AddComponent<Image>();
            plate.sprite = TextureLab.RoundedRect(10);
            plate.type = Image.Type.Sliced;
            plate.color = UITheme.WithAlpha(HubBackdrop, 0.92f);
            plate.raycastTarget = false;
            UIBuilder.Frame("Edge", tag, UITheme.WithAlpha(UITheme.ChromeBrass, 0.8f), 10, 2);

            Text label = UIBuilder.Label("Text", tag, "SOON", UITheme.FontTiny - 2, UITheme.ChromeBrass, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(label.rectTransform, 0f);
            return tag;
        }

        /// <summary>
        /// The shared "there is no screen behind this yet" response, used by the Marketplace node
        /// and by the dock's Guild and Splinters badges. One sentence, one place, so all three say
        /// the same thing and none of them silently swallows the tap.
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

        private static float NodeWidthFor(string label)
        {
            return Mathf.Max(190f, 34f + label.Length * 19f);
        }

        /// <summary>
        /// Places a rect by fraction of the map rather than by pixel offset from a screen edge.
        /// This is the fix for the class of bug the old BuildCinderMark documented: anything
        /// positioned by a fixed offset from one edge drifts relative to anything positioned from
        /// the opposite edge as soon as the logical canvas is not exactly 1080 tall, which on this
        /// project's real target aspect it never is.
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

        /// <summary>
        /// Campaign status and the one button this screen exists to get pressed, as a compact card
        /// pinned to the bottom-right edge above the squad strip.
        ///
        /// It used to be a 520x214 plate anchored to the middle of the map, which put the largest
        /// opaque rectangle on the screen directly over the courtyard, the portal and the Deep
        /// Recital node - it covered the art the reskin exists to show. Screen-anchored, at roughly
        /// the idle panel's footprint, it reads as chrome about the account rather than a place on
        /// the map, which is what it always was.
        ///
        /// Bottom-right specifically, checked against the node anchors rather than picked by eye.
        /// The right edge below the idle panel collides with DEEP RECITAL at every aspect, and the
        /// top-left collides with HEROES; above the squad strip is the one edge slot that clears
        /// every node at 16:9, 19.5:9 and the wider editor window alike, by 100 units at worst.
        /// </summary>
        private void BuildCampaignCard()
        {
            const float cardWidth = 600f;
            const float cardHeight = 168f;
            const float inner = 18f;

            RectTransform card = UIBuilder.Node("Campaign", Root);
            UIBuilder.Box(card, -UITheme.ScreenPadding, BottomRowHeight + UITheme.Space16,
                          cardWidth, cardHeight, new Vector2(1f, 0f));

            Image plate = card.gameObject.AddComponent<Image>();
            plate.sprite = TextureLab.RoundedRect(18);
            plate.type = Image.Type.Sliced;
            plate.color = UITheme.WithAlpha(HubPanel, 0.9f);
            UIBuilder.Frame("Edge", card, UITheme.WithAlpha(UITheme.ChromeBrass, 0.6f), 18, 2);

            _chapterLabel = UIBuilder.Label("Chapter", card, "", UITheme.FontTiny - 2, UITheme.TextPrimary, TextAnchor.UpperLeft);
            UIBuilder.Box(_chapterLabel.rectTransform, inner, -12f, 340f, 24f, new Vector2(0f, 1f));

            _chapterCount = UIBuilder.Label("Count", card, "", UITheme.FontTiny - 2, UITheme.Gold, TextAnchor.UpperRight);
            UIBuilder.Box(_chapterCount.rectTransform, -inner, -12f, 200f, 24f, new Vector2(1f, 1f));

            _chapterFill = UIBuilder.Bar("Bar", card, HubPanelSunken, UITheme.Gold);
            RectTransform barRoot = _chapterFill.rectTransform.parent as RectTransform;
            UIBuilder.Box(barRoot, 0f, -44f, cardWidth - inner * 2f, 8f, new Vector2(0.5f, 1f));

            _continueSub = UIBuilder.Label("Sub", card, "", UITheme.FontTiny - 4, UITheme.ChromeBrass, TextAnchor.UpperCenter);
            UIBuilder.Box(_continueSub.rectTransform, 0f, -58f, cardWidth - inner * 2f, 22f, new Vector2(0.5f, 1f));

            // GoldButton: the primary action, the same colour the de-aspect pass gave it and the
            // same colour Claim uses. Nothing else on this screen is allowed to be Gold.
            _continueButton = UIBuilder.GoldButton("Continue", card, "CONTINUE CAMPAIGN", UITheme.FontSmall);
            UIBuilder.Box(_continueButton.Root, 0f, 14f, cardWidth - inner * 2f, 62f, new Vector2(0.5f, 0f));
            _continueButton.OnClick(OpenCampaign);
        }

        /// <summary>
        /// Idle income, top right, screen-anchored rather than map-anchored - it is chrome about the
        /// account, not a place on the island. Replaces the old full-height EMBER CACHE card, which
        /// took a whole column to say what four lines say here.
        /// </summary>
        private void BuildIdlePanel()
        {
            RectTransform card = UIBuilder.Node("IdleCache", Root);
            UIBuilder.Box(card, -UITheme.ScreenPadding, -(UITheme.TopBarHeight + UITheme.Space16), 640f, 200f, new Vector2(1f, 1f));

            Image background = card.gameObject.AddComponent<Image>();
            background.sprite = TextureLab.RoundedRect(18);
            background.type = Image.Type.Sliced;
            background.color = UITheme.WithAlpha(HubPanel, 0.9f);
            UIBuilder.Frame("Edge", card, UITheme.WithAlpha(UITheme.ChromeBrass, 0.6f), 18, 2);

            Text heading = UIBuilder.Label("Heading", card, "IDLE REWARDS", UITheme.FontTiny - 2, UITheme.ChromeBrass, TextAnchor.UpperLeft);
            UIBuilder.Box(heading.rectTransform, 22f, -16f, 400f, 24f, new Vector2(0f, 1f));

            _idleAmounts = UIBuilder.Label("Amounts", card, "", UITheme.FontSmall, UITheme.Gold, TextAnchor.UpperLeft);
            UIBuilder.Box(_idleAmounts.rectTransform, 22f, -44f, 420f, 34f, new Vector2(0f, 1f));

            _idleTimer = UIBuilder.Label("Timer", card, "", UITheme.FontTiny - 2, UITheme.TextMuted, TextAnchor.UpperLeft);
            UIBuilder.Box(_idleTimer.rectTransform, 22f, -86f, 400f, 24f, new Vector2(0f, 1f));

            _idleRate = UIBuilder.Label("Rate", card, "", UITheme.FontTiny - 4, UITheme.TextDim, TextAnchor.UpperLeft);
            UIBuilder.Box(_idleRate.rectTransform, 22f, -114f, 400f, 22f, new Vector2(0f, 1f));

            _claimButton = UIBuilder.GoldButton("Claim", card, "CLAIM", UITheme.FontTiny);
            UIBuilder.Box(_claimButton.Root, -20f, 22f, 168f, 64f, new Vector2(1f, 0f));
            _claimButton.OnClick(ClaimIdle);
        }

        /// <summary>
        /// The player's own menus, bottom left, in the mockup's badge-dock language: a circular
        /// emblem per entry with its label beneath, sized to sit flush with the squad portraits
        /// across the bottom of the screen.
        ///
        /// Guild and Splinters have no screen in this build and are marked SOON. Events DOES have a
        /// real screen (ScreenId.Events) and is not in the mockup's dock - it is added here anyway,
        /// because the layout this replaces was that screen's only entry point and dropping it
        /// would have quietly made a working feature unreachable. Flagged rather than done silently.
        /// </summary>
        private void BuildBadgeDock()
        {
            RectTransform dock = UIBuilder.Node("Dock", Root);
            UIBuilder.Box(dock, UITheme.ScreenPadding, BottomSafeMargin, BadgeRowWidth, BadgeCellHeight, new Vector2(0f, 0f));
            UIBuilder.HStack(dock, BadgeGap, new RectOffset(0, 0, 0, 0), TextAnchor.LowerLeft);

            AddBadge(dock, "GUILD", 2, UITheme.ChromeBrass, null);
            AddBadge(dock, "BAG", 7, UITheme.ChromeBrass, OpenInventory);
            AddBadge(dock, "SPLINTERS", 12, UITheme.CinderGlass, null);
            AddBadge(dock, "RELICS", 6, UITheme.RelicGradeColor(RelicGrade.Legendary), OpenRelicShop);
            AddBadge(dock, "EVENTS", 8, UITheme.Success, OpenEvents);
        }

        /// <summary>A dock entry: emblem disc, ring, sigil, label. A null action means "not built
        /// yet" and gets the SOON tag and the shared explanation tap.</summary>
        private void AddBadge(RectTransform dock, string label, int sigil, Color ring, System.Action action)
        {
            bool locked = action == null;

            RectTransform cell = UIBuilder.Node("Badge" + label, dock);
            UIBuilder.Sized(cell, BadgeCell, BadgeCellHeight);

            UIButton emblem = UIBuilder.IconButton("Button", cell, "", BadgeSize,
                                                   UITheme.WithAlpha(HubPanelRaised, locked ? 0.7f : 0.95f),
                                                   UITheme.TextPrimary);
            UIBuilder.Box(emblem.Root, 0f, 0f, BadgeSize, BadgeSize, new Vector2(0.5f, 1f));
            emblem.OnClick(locked ? NotSoonEnough(label) : action);

            Image rim = UIBuilder.Img("Ring", emblem.Root, TextureLab.Ring(128, 5),
                                      UITheme.WithAlpha(ring, locked ? 0.45f : 0.9f));
            UIBuilder.FillParent(rim.rectTransform, 0f);

            Image glyph = UIBuilder.Img("Glyph", emblem.Root, TextureLab.Sigil(sigil, 64),
                                        UITheme.WithAlpha(ring, locked ? 0.5f : 1f));
            UIBuilder.FillParent(glyph.rectTransform, 26f);

            // TextPrimary, not ChromeBrass. ChromeBrass earns its place on this dock as the ring and
            // outline colour; a dock caption is the button's own name, not secondary chrome, and at
            // FontTiny-4 over a painted plateau the brass sat too close to the stone behind it to
            // read. The shadow does the separating, not a brighter accent.
            Text caption = UIBuilder.Label("Label", cell, label, UITheme.FontTiny - 4,
                                           locked ? UITheme.TextDim : UITheme.TextPrimary, TextAnchor.UpperCenter);
            UIBuilder.Box(caption.rectTransform, 0f, -(BadgeSize + 6f), BadgeCell, 24f, new Vector2(0.5f, 1f));
            UIBuilder.Shadowed(caption, UITheme.InkArt, 1.4f);

            if (locked) AddSoonTag(cell, 0f, -(BadgeSize + 36f), new Vector2(0.5f, 1f));
        }

        /// <summary>
        /// The squad, along the bottom right. The mockup centres this strip because its dock is a
        /// four-badge cluster tucked in the corner; this dock carries five and is wider, and a
        /// centred strip put the two side by side with no gap - ten circles in one unbroken row
        /// reading as a single meaningless rail. Anchoring the squad to the opposite edge restores
        /// the separation the mockup gets from its own proportions: menus left, party right.
        /// RefreshTeam fills it.
        /// </summary>
        private void BuildSquadStrip()
        {
            _teamRow = UIBuilder.Node("Squad", Root);
            UIBuilder.Box(_teamRow, -UITheme.ScreenPadding, BottomSafeMargin, BadgeRowWidth, BadgeCellHeight, new Vector2(1f, 0f));
            UIBuilder.HStack(_teamRow, BadgeGap, new RectOffset(0, 0, 0, 0), TextAnchor.LowerRight);
        }

        /// <summary>
        /// The Hub's one corner watermark: a small, static, colourless outline of the Cinder.
        /// DESIGN-ART-BIBLE section 5 - it never grows a face, never animates, never carries an
        /// aspect colour.
        ///
        /// Anchored to the TOP-LEFT, the one region of this layout nothing else claims: the idle
        /// panel is top-right, the dock is bottom-left, the squad strip is bottom-centre. The
        /// previous placement was a fixed offset from the top edge trying to clear a nav row
        /// anchored to the bottom edge, which held only at exactly 1080 logical units of height and
        /// collided with three buttons at this project's real target aspect. Anchoring a mark to an
        /// edge that nothing else measures from is what actually fixes that, not a better number.
        /// </summary>
        private void BuildCinderMark()
        {
            const float size = 112f;
            Image mark = UIBuilder.Img("CinderMark", Root, TextureLab.CinderFragment(256),
                                       UITheme.WithAlpha(UITheme.CinderGlass, 0.32f));
            UIBuilder.Box(mark.rectTransform, UITheme.ScreenPadding + 20f,
                          -(UITheme.TopBarHeight + UITheme.Space24), size, size, new Vector2(0f, 1f));
        }

        private void BuildOptions()
        {
            UIButton options = UIBuilder.IconButton("Options", Root, "...", 62f,
                                                    UITheme.WithAlpha(HubPanelRaised, 0.9f), UITheme.TextMuted);
            UIBuilder.Box(options.Root, -UITheme.ScreenPadding, -(UITheme.TopBarHeight + UITheme.Space16 + 210f),
                          62f, 62f, new Vector2(1f, 1f));
            options.OnClick(ShowOptions);
        }

        // ------------------------------------------------------------------ behaviour

        public override void OnShow()
        {
            RefreshTeam();
            RefreshIdle();
            RefreshContinue();
            RefreshChapter();
            App.RefreshHud();
            _tick = 0f;
        }

        private void RefreshChapter()
        {
            List<ChapterDef> chapters = App.Session.Content.Chapters;
            if (chapters.Count == 0)
            {
                _chapterLabel.text = "NO CAMPAIGN CONTENT";
                _chapterCount.text = "";
                UIBuilder.SetBar(_chapterFill, 0f);
                return;
            }

            ChapterDef chapter = chapters[0];
            int cleared = 0;
            for (int i = 0; i < chapter.Stages.Count; i++)
                if (App.Session.Profile.HasClearedStage(chapter.Stages[i].Id)) cleared++;

            _chapterLabel.text = "CHAPTER " + chapter.Number + "  -  " + chapter.Name.ToUpperInvariant();
            _chapterCount.text = cleared + " / " + chapter.Stages.Count + " CLEARED";
            UIBuilder.SetBar(_chapterFill, chapter.Stages.Count > 0 ? cleared / (float)chapter.Stages.Count : 0f);
        }

        private void Update()
        {
            if (Root == null || !Root.gameObject.activeInHierarchy) return;
            _tick += Time.unscaledDeltaTime;
            if (_tick < 1f) return;
            _tick = 0f;
            RefreshIdle();
        }

        private void RefreshTeam()
        {
            for (int i = _teamRow.childCount - 1; i >= 0; i--) Destroy(_teamRow.GetChild(i).gameObject);

            // Five cells across the 880f strip: 5 * 160 + 4 * 16 spacing = 864. The strip sits over
            // the painted map now rather than inside a panel, so each cell is a portrait and a name
            // and nothing else - the level/power line the old card carried moved out. It was the
            // only Gold on this row, and Gold on five portraits at once, over a map that also
            // carries a Gold Continue button and a Gold Claim button, spent the colour on the least
            // important thing on the screen. Heroes shows the same numbers with room to read them.
            List<HeroInstance> team = App.Session.Profile.TeamHeroes();
            for (int slot = 0; slot < PlayerProfile.TeamSize; slot++)
            {
                RectTransform cell = UIBuilder.Node("Slot" + slot, _teamRow);
                UIBuilder.Sized(cell, BadgeCell, BadgeCellHeight);

                if (slot >= team.Count)
                {
                    // an open slot is room to grow, not a hole: a ring where the portrait will
                    // stand (same convention as the campaign banner strip) and the slot's number,
                    // so the cell promises a fourth and fifth ally rather than apologising
                    Image ring = UIBuilder.Img("Ring", cell, TextureLab.Ring(96, 5),
                                               UITheme.WithAlpha(UITheme.ChromeBrass, 0.55f));
                    UIBuilder.Box(ring.rectTransform, 0f, 0f, BadgeSize, BadgeSize, new Vector2(0.5f, 1f));
                    Text hint = UIBuilder.Label("Hint", cell, "SLOT " + (slot + 1), UITheme.FontTiny - 4, UITheme.TextDim, TextAnchor.UpperCenter);
                    UIBuilder.Box(hint.rectTransform, 0f, -(BadgeSize + 6f), BadgeCell, 24f, new Vector2(0.5f, 1f));
                    UIBuilder.Shadowed(hint, UITheme.InkArt, 1.4f);
                    continue;
                }

                HeroInstance hero = team[slot];
                RectTransform portrait = Widgets.Portrait("Portrait", cell, hero.Def, hero.Def.Stars, BadgeSize, true);
                UIBuilder.Box(portrait, 0f, 0f, BadgeSize, BadgeSize, new Vector2(0.5f, 1f));

                // First word only. A cell is BadgeCell wide because that is what the dock's captions
                // need, and "Kalyx Emberwright" at full length is wider than that - it overflowed
                // into its neighbours and was the other half of why this row read as two different
                // rows. Heroes shows full names where there is room for them.
                string shortName = hero.Def.Name;
                int space = shortName.IndexOf(' ');
                if (space > 0) shortName = shortName.Substring(0, space);

                Text name = UIBuilder.Label("Name", cell, shortName, UITheme.FontTiny - 4, UITheme.TextPrimary, TextAnchor.UpperCenter);
                UIBuilder.Box(name.rectTransform, 0f, -(BadgeSize + 6f), BadgeCell, 24f, new Vector2(0.5f, 1f));
                UIBuilder.Shadowed(name, UITheme.InkArt, 1.4f);
            }
        }

        private void RefreshIdle()
        {
            GameSession session = App.Session;
            IdleReport report = session.PeekIdle(TimeUtil.NowUnix());

            _idleRate.text = "+" + Mathf.RoundToInt(session.IdleGoldPerHour) + " gold  /  +"
                             + Mathf.RoundToInt(session.IdleShardsPerHour) + " shards per hour";
            _idleAmounts.text = report.Gold + " GOLD    " + report.Shards + " SHARDS";
            _idleTimer.text = FormatDuration(report.Seconds) + " stored"
                              + (report.Capped ? "  (cache full)" : "  -  cap " + Mathf.RoundToInt(session.Balance.IdleCapHours) + "h");

            _claimButton.Interactable = report.HasAnything;
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

        private static string FormatDuration(long seconds)
        {
            if (seconds < 60) return seconds + "s";
            long minutes = seconds / 60;
            if (minutes < 60) return minutes + "m";
            long hours = minutes / 60;
            return hours + "h " + (minutes % 60) + "m";
        }

        // ------------------------------------------------------------------ actions

        private void ClaimIdle()
        {
            IdleReport report = App.Session.ClaimIdle(TimeUtil.NowUnix());
            if (!report.HasAnything)
            {
                App.Notify("The cache is still filling.");
                return;
            }
            App.Audio.Reward();
            App.Notify("Collected " + report.Gold + " gold and " + report.Shards + " shards.");
            App.RefreshHud();
            App.RequestSave();
            RefreshIdle();
        }

        private void OpenHeroes()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Heroes);
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

        private void OpenRelicShop()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.RelicShop);
        }

        private void OpenInventory()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Inventory);
        }

        private void OpenEvents()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Events);
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

        private void ShowOptions()
        {
            App.Audio.Tap();
            App.Modal.ShowConfirm(
                "RESET PROGRESS",
                "This wipes your heroes, currency and campaign progress and starts a fresh account. " +
                "Useful for testing the first-run experience.",
                "RESET", "KEEP PLAYING",
                App.ResetProgress,
                null);
        }
    }
}
