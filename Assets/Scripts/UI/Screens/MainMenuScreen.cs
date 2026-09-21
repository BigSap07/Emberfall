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

        /// <summary>
        /// One-shot latch for the session-missing report. This screen ticks once a SECOND, so an
        /// unlatched log here is 3600 identical lines an hour, which buries the one line that
        /// matters and is how the original fault presented.
        /// </summary>
        private bool _sessionWarned;

        private Text _chapterLabel;
        private Image _chapterFill;
        private Text _chapterCount;

        // ---------- the Hub's own surfaces ----------
        // The four Hub-scoped surface tones that used to be declared here - HubBackdrop, HubPanel,
        // HubPanelRaised and HubPanelSunken - now live in HubStyle with the rest of the Hub's
        // chrome vocabulary. They were already a Hub-only override of the shared ramp, so they were
        // exactly the thing "one shared style definition" is for; keeping four of them here and the
        // radii, alphas and font sizes somewhere else was the split this pass removes.
        //
        // The reasoning that put them here still holds and still applies, so it moves with them:
        // UITheme.Backdrop, Panel, PanelRaised and Border are NOT touched, because every other
        // screen keeps the violet-toned chrome and Ember accents it has today. Warming the shared
        // constants instead would drag Campaign, Inventory, RelicShop and every PrimaryButton in
        // the game along with it, which is a far larger change than this one and would land with
        // none of those screens reviewed.
        //
        // None of them is ChromeBrass. ChromeBrass is an accent that sits ON these surfaces
        // (outlines, rings, dividers), which is what HubStyle.BorderWarm resolves to.

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

        /// <summary>
        /// The Proving, on the walled gatehouse at the far right of the painting - the one built
        /// structure the map had that nothing led to. Measured against the art rather than picked:
        /// the gate spans roughly x 0.86 to 0.95, and 0.915 sits on its door.
        ///
        /// Placed here rather than as a sixth badge in the dock on purpose. A sixth badge widens
        /// BadgeRowWidth from 740 to 892 per side, and with both rows anchored to opposite edges
        /// that leaves 72 logical units of gap at 16:9 - survivable but tight, and it would have
        /// put a layout risk into the row this branch is not otherwise touching. A map node costs
        /// the dock nothing.
        ///
        /// The gap to DEEP RECITAL at 0.800 was checked, not assumed: the two labels are 150 and
        /// 188 units wide, needing 169 units of clearance between centres, and 0.115 of map width
        /// is about 221 at 16:9. It also stays inside the horizontal crop - at 16:9 the map shows
        /// x 0.021 to 0.979, and this label's right edge lands at 0.954.
        /// </summary>
        private const float ProvingX = 0.915f, ProvingY = 0.500f;   // the walled gatehouse door
        //
        // y was 0.330 and that put the label INVISIBLE, entirely behind the idle rewards panel, at
        // both 16:9 and 19.5:9. The clearance check that missed it only compared this node against
        // the other MAP nodes and against the horizontal crop - both map-space questions - and the
        // idle panel is screen-anchored, built after the nodes, and therefore drawn over them.
        //
        // Two different coordinate systems share one screen here, so a map node has to clear both:
        //   map-space   the other five nodes, and the crop at the narrowest supported aspect
        //   screen-space the idle panel (top right, 640x200 under the top bar) and the campaign
        //                card (bottom right, 600x168 above the badge row)
        // At 0.500 the label lands 470..518 from the top at 16:9 and 419..467 at 19.5:9, against an
        // idle panel occupying 148..348 and a campaign card starting at 676 and 574 respectively -
        // clear of both at both aspects, with DEEP RECITAL still 61 units away horizontally.

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
            BuildSocialButtons();
            BuildBadgeDock();
            BuildSquadStrip();
            BuildOptions();
            BuildDevEntry();
        }

        /// <summary>
        /// ACCOUNT, FRIENDS and GUILD, top left, under the top bar.
        ///
        /// NOT IN THE BADGE DOCK, and the dock's own note says why: a sixth badge widens
        /// BadgeRowWidth from 740 to 892 per side, which at 16:9 leaves 72 logical units between
        /// the dock and the squad strip - "survivable but tight", and that was for ONE badge.
        /// These are two. The dock is full.
        ///
        /// NOT IN THE MAP NODES either, which is the other obvious home and the wrong one for a
        /// different reason: every node on that painting is a PLACE the player walks to - the
        /// barracks, the smithy, the obelisk, the gatehouse - and an account is not a place. It is
        /// chrome about who you are, exactly as the idle panel is chrome about what you have
        /// earned. So it sits where that panel sits, in the same register, on the opposite corner.
        ///
        /// Top LEFT specifically, and the clearance was checked rather than assumed. The nearest
        /// map node is the barracks at map-y 0.300, whose label lands around y 300 to 348 at 16:9 -
        /// and unlike the map nodes this row is SCREEN-anchored, so that gap does not move with
        /// the aspect the way a map-space clearance would. The Proving node's own comment records
        /// what happens when a screen-anchored panel and a map-space label are checked only
        /// against their own coordinate system: the label ended up invisible behind the idle
        /// panel at every aspect.
        ///
        /// THE THIRD BUTTON IS WHY THESE ARE 270 WIDE AND NOT 310, and the number came out of the
        /// narrow case rather than the reference one. The idle panel is the mirror of this row:
        /// right-anchored, 640 wide, 32 from the edge, so its left edge sits at
        /// (logical width - 672). At 16:9 that is 1248 and nothing here can reach it. At 4:3 -
        /// the narrowest landscape a tablet produces - the canvas scaler's 0.5 match puts the
        /// logical width at about 1663, so the panel's left edge is at about 991. Three 310-wide
        /// buttons with 16 between them run from 32 to 994, which is THREE UNITS PAST IT. Three
        /// 270-wide ones stop at 874 and leave about 117. The pair before this fitted at 310 and
        /// the maths that allowed it did not have to consider a third; this is the same check
        /// redone for the row that now exists rather than the row that used to.
        /// </summary>
        private void BuildSocialButtons()
        {
            const float buttonWidth = 270f;
            const float buttonHeight = 84f;

            RectTransform row = UIBuilder.Node("Social", Root);
            UIBuilder.Box(row, UITheme.ScreenPadding, -(UITheme.TopBarHeight + UITheme.Space16),
                          buttonWidth * 3f + UITheme.Space16 * 2f, buttonHeight, new Vector2(0f, 1f));
            UIBuilder.HStack(row, UITheme.Space16, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);

            // Secondary, not Gold. The Hub already carries exactly two Gold buttons - Continue and
            // Claim - and the note on each says nothing else on this screen is allowed to be Gold.
            // An account is not a call to action; it is a door.
            UIButton account = UIBuilder.SecondaryButton("Account", row, "ACCOUNT", HubStyle.FontButton);
            UIBuilder.Sized(account.Root, buttonWidth, buttonHeight);
            account.OnClick(OpenAccount);

            UIButton friends = UIBuilder.SecondaryButton("Friends", row, "FRIENDS", HubStyle.FontButton);
            UIBuilder.Sized(friends.Root, buttonWidth, buttonHeight);
            friends.OnClick(OpenFriends);

            // Beside FRIENDS rather than anywhere else, because it is the same kind of door: a
            // guild is people, and the player looking for one is looking where the other people
            // are. It is not a map node for the reason above - a guild is not a place on the
            // painting - and it is not a badge, because the dock is full.
            UIButton guild = UIBuilder.SecondaryButton("Guild", row, "GUILD", HubStyle.FontButton);
            UIBuilder.Sized(guild.Root, buttonWidth, buttonHeight);
            guild.OnClick(OpenGuild);
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
                // painting that already arrives warm and saturated. It cost 37% of the image's
                // luminance and crushed blue to 49% of source, which is why the sky, the banner,
                // the obelisk runes and the purple cave all read flat and dull.
                Image art = UIBuilder.Img("Art", _map, _mapSprite, Color.white);
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

            // 0.10. This was 0.5, then 0.32, both tuned against a pale plateau that genuinely
            // needed pulling down. A flat scrim does not preserve contrast the way a multiply does:
            // it lifts the blacks toward itself and flattens everything. Stacked on the wash above
            // it, a mid-tone was reaching the screen at about 43% of the source file's brightness.
            // What is left is the little that the chrome needs to seat against, nothing more.
            Image scrim = UIBuilder.Img("Scrim", Root, TextureLab.White, UITheme.WithAlpha(HubStyle.Plate, HubStyle.ScrimAlpha));
            UIBuilder.FillParent(scrim.rectTransform, 0f);

            // The ambient glow that used to sit here is gone. It was a colourless CinderGlass
            // lift for a screen whose floor was a flat dark fill and needed one; this painting has
            // a sunset in it and supplies its own. Over vivid art a cool grey veil at 10% alpha is
            // just one more thing desaturating the top half of the image.
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
            AddMapNode("PROVING", ProvingX, ProvingY, OpenProving);
            // The portal is a place you can walk to, like the others. Its live state and its
            // primary action live in the corner card instead, so the map keeps only the tap target.
            AddMapNode("CAMPAIGN", CampaignX, CampaignY, OpenCampaign);

            // Painted into the map for a future that does not exist yet, exactly as the mockup
            // treats it. Same treatment as the dock's two unbuilt entries: it looks like a place,
            // it says SOON, and tapping it explains itself rather than doing nothing.
            AddLockedNode("MARKETPLACE", MarketX, MarketY);
        }

        /// <summary>
        /// THE building marker. Every structure on the map is this and only this, so the six of
        /// them form one visual system instead of six separately-styled tags: one height, one
        /// radius, one surface, one border, one lift above the target, one tail pointing back down
        /// at it. Every number comes from HubStyle - there is no style literal in this method.
        ///
        /// Built here rather than through UIBuilder.MakeButton, because MakeButton builds a BUTTON -
        /// a 3-unit border, a 22-unit corner radius and a white sheen across the top half - and
        /// three pieces of raised-button chrome are exactly what made these tags sit on top of the
        /// painting instead of on the buildings.
        ///
        /// The lift is the part that changed: the plate used to be centred ON its anchor and so
        /// covered the structure it named. It now sits HubStyle.LabelLift above it with a tail
        /// reaching back down, so the anchor still points at the building, the building stays
        /// visible, and the label reads as a caption for it rather than a sticker over it. The
        /// anchor fractions themselves are untouched.
        ///
        /// fill and edge alphas stay arguments because the locked variant sits back further, and
        /// those two numbers are the whole of how loud a marker reads.
        /// </summary>
        private UIButton BuildNodePlate(string label, float mx, float my,
                                        float fillAlpha, float edgeAlpha, Color textColor)
        {
            UIButton handle = new UIButton();
            handle.Root = UIBuilder.Node("Node" + label, _map);
            AnchorOnMap(handle.Root, mx, my, HubStyle.LabelWidthFor(label), HubStyle.LabelHeight);
            // Lift off the anchor. Same rule for every marker, which is what makes them a system.
            handle.Root.anchoredPosition = new Vector2(0f, HubStyle.LabelLift);
            handle.Group = handle.Root.gameObject.AddComponent<CanvasGroup>();

            // Tail first, plate second. Unity draws a parent's own graphic before its children, so
            // the plate has to be a CHILD created after the tail for it to bury the tail's top half
            // and leave a triangle pointing down at the building. On the root it would draw first
            // and the tail would sit on top of it as a doubled-alpha diamond.
            HubStyle.Tail(handle.Root, fillAlpha);
            handle.Background = HubStyle.SurfaceChild("Plate", handle.Root, HubStyle.RadiusLabel,
                                                      fillAlpha, edgeAlpha, HubStyle.BorderWeight);

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

            handle.Label = UIBuilder.Label("Text", handle.Root, label, HubStyle.FontLabel, textColor,
                                           TextAnchor.MiddleCenter);
            UIBuilder.FillParent(handle.Label.rectTransform, 6f);
            // The shadow, not a heavier plate, is what holds the word off the painting behind it.
            UIBuilder.Shadowed(handle.Label, UITheme.InkArt, 1.6f);
            return handle;
        }

        private UIButton AddMapNode(string label, float mx, float my, System.Action action)
        {
            UIButton node = BuildNodePlate(label, mx, my, HubStyle.PlateAlphaSoft,
                                           HubStyle.BorderAlpha, HubStyle.LabelText);
            node.OnClick(action);
            return node;
        }

        /// <summary>
        /// A structure with no screen behind it. Same plate, sitting back further still, plus a
        /// SOON tag and a tap that says so out loud.
        /// </summary>
        private void AddLockedNode(string label, float mx, float my)
        {
            UIButton node = BuildNodePlate(label, mx, my, HubStyle.PlateAlphaSoft * 0.8f,
                                           HubStyle.BorderAlphaMuted, UITheme.TextMuted);
            node.OnClick(NotSoonEnough(label));
            // Below the tail, not on top of it, so the marker still points at its building.
            HubStyle.SoonBadge(node.Root, 0f, -(HubStyle.TailSize + HubStyle.SoonDrop),
                               new Vector2(0.5f, 0f));
        }

        // The old AddSoonTag lived here. It is now HubStyle.SoonBadge, because the dock and the
        // map both need it and "one shared component" cannot mean one per screen. Its hard-won
        // reasoning survives in that method: the badge paints a dark opaque plate under its text
        // rather than using UIBuilder.Chip, whose single-tint fill/border/text collapses to almost
        // no contrast when it is laid over a sunlit painting instead of over a dark panel.

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

        // NodeWidthFor moved to HubStyle.LabelWidthFor with the rest of the marker's metrics, so
        // a label's width retunes from the same file as its height, radius and lift.

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

            // Same panel surface as the idle card, from the same tokens: the two chrome panels on
            // this screen are one object with two sets of contents, not two designs.
            HubStyle.Surface(card, HubStyle.RadiusPanel, HubStyle.PlateAlpha,
                             HubStyle.BorderAlphaStrong, HubStyle.BorderWeightHeavy);

            _chapterLabel = UIBuilder.Label("Chapter", card, "", HubStyle.FontSecondary, HubStyle.LabelText, TextAnchor.UpperLeft);
            UIBuilder.Box(_chapterLabel.rectTransform, inner, -12f, 340f, 24f, new Vector2(0f, 1f));

            _chapterCount = UIBuilder.Label("Count", card, "", HubStyle.FontSecondary, HubStyle.ValueGold, TextAnchor.UpperRight);
            UIBuilder.Box(_chapterCount.rectTransform, -inner, -12f, 200f, 24f, new Vector2(1f, 1f));

            _chapterFill = UIBuilder.Bar("Bar", card, UITheme.WithAlpha(HubStyle.Plate, HubStyle.PlateAlphaSolid), HubStyle.ValueGold);
            RectTransform barRoot = _chapterFill.rectTransform.parent as RectTransform;
            UIBuilder.Box(barRoot, 0f, -44f, cardWidth - inner * 2f, 8f, new Vector2(0.5f, 1f));

            _continueSub = UIBuilder.Label("Sub", card, "", HubStyle.FontCaption, HubStyle.TextSecondary, TextAnchor.UpperCenter);
            UIBuilder.Box(_continueSub.rectTransform, 0f, -58f, cardWidth - inner * 2f, 22f, new Vector2(0.5f, 1f));

            // GoldButton: the primary action, the same colour the de-aspect pass gave it and the
            // same colour Claim uses. Nothing else on this screen is allowed to be Gold.
            _continueButton = UIBuilder.GoldButton("Continue", card, "CONTINUE CAMPAIGN", HubStyle.FontButton);
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
            HubStyle.Surface(card, HubStyle.RadiusPanel, HubStyle.PlateAlpha,
                             HubStyle.BorderAlphaStrong, HubStyle.BorderWeightHeavy);

            Text heading = UIBuilder.Label("Heading", card, "IDLE REWARDS", HubStyle.FontSecondary,
                                           HubStyle.SoonText, TextAnchor.UpperLeft);
            UIBuilder.Box(heading.rectTransform, HubStyle.PanelPadX, -HubStyle.PanelPadY, 400f, 24f, new Vector2(0f, 1f));

            // Gold and shards earned: the panel's headline number, in the Hub's one value tone.
            _idleAmounts = UIBuilder.Label("Amounts", card, "", HubStyle.FontValue,
                                           HubStyle.ValueGold, TextAnchor.UpperLeft);
            UIBuilder.Box(_idleAmounts.rectTransform, HubStyle.PanelPadX, -44f, 420f, 34f, new Vector2(0f, 1f));

            // Elapsed time and cache state - secondary, but not the rate line's weight.
            _idleTimer = UIBuilder.Label("Timer", card, "", HubStyle.FontSecondary,
                                         HubStyle.TextSecondary, TextAnchor.UpperLeft);
            UIBuilder.Box(_idleTimer.rectTransform, HubStyle.PanelPadX, -86f, 400f, 24f, new Vector2(0f, 1f));

            // The per-hour rate: the quietest line on the panel, muted below the timer.
            _idleRate = UIBuilder.Label("Rate", card, "", HubStyle.FontSoon,
                                        UITheme.WithAlpha(HubStyle.TextSecondary, HubStyle.SoonAlpha),
                                        TextAnchor.UpperLeft);
            UIBuilder.Box(_idleRate.rectTransform, HubStyle.PanelPadX, -114f, 400f, 22f, new Vector2(0f, 1f));

            // Solid warm gold on dark text: the single strongest call to action on the screen.
            // GoldButton is already exactly that, and is what Continue uses - nothing else on this
            // screen is allowed to be Gold, so the two of them are the only things wearing it.
            _claimButton = UIBuilder.GoldButton("Claim", card, "CLAIM", HubStyle.FontLabel);
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

        /// <summary>
        /// A dock entry: a circular button with a dark fill and a thin warm border, the entry's
        /// sigil centred in it, and its caption below in a small light warm tone. Every colour,
        /// weight, inset and font here comes from HubStyle.
        ///
        /// The ring is the shared warm border on every entry rather than a per-entry colour, which
        /// is what makes the row read as one control strip. The entry's own tint moves to the sigil,
        /// so Relics still reads legendary and Splinters still reads Cinder - the distinction is
        /// kept, it just stops being the thing that frames the button.
        ///
        /// A null action means "not built yet": the whole cell is dimmed through one CanvasGroup and
        /// takes the same SOON badge the map's locked marker does. Dimming the cell rather than each
        /// child is what keeps the badge subordinate to the caption at both brightnesses - they
        /// scale together, so the relative step HubStyle.SoonAlpha sets is preserved.
        /// </summary>
        private void AddBadge(RectTransform dock, string label, int sigil, Color tint, System.Action action)
        {
            bool locked = action == null;

            RectTransform cell = UIBuilder.Node("Badge" + label, dock);
            UIBuilder.Sized(cell, BadgeCell, BadgeCellHeight);

            if (locked)
            {
                CanvasGroup dim = cell.gameObject.AddComponent<CanvasGroup>();
                dim.alpha = HubStyle.DimAlpha;
            }

            UIButton emblem = UIBuilder.IconButton("Button", cell, "", BadgeSize,
                                                   UITheme.WithAlpha(HubStyle.Plate, HubStyle.PlateAlpha),
                                                   HubStyle.LabelText);
            UIBuilder.Box(emblem.Root, 0f, 0f, BadgeSize, BadgeSize, new Vector2(0.5f, 1f));
            emblem.OnClick(locked ? NotSoonEnough(label) : action);

            Image rim = UIBuilder.Img("Ring", emblem.Root, TextureLab.Ring(128, HubStyle.NavRingThickness),
                                      UITheme.WithAlpha(HubStyle.BorderWarm, HubStyle.BorderAlphaStrong));
            UIBuilder.FillParent(rim.rectTransform, 0f);

            Image glyph = UIBuilder.Img("Glyph", emblem.Root, TextureLab.Sigil(sigil, 64), tint);
            UIBuilder.FillParent(glyph.rectTransform, HubStyle.NavGlyphInset);

            Text caption = UIBuilder.Label("Label", cell, label, HubStyle.FontCaption,
                                           HubStyle.LabelText, TextAnchor.UpperCenter);
            UIBuilder.Box(caption.rectTransform, 0f, -(BadgeSize + HubStyle.NavIconGap), BadgeCell,
                          HubStyle.NavCaptionHeight, new Vector2(0.5f, 1f));
            UIBuilder.Shadowed(caption, UITheme.InkArt, 1.4f);

            if (locked)
            {
                HubStyle.SoonBadge(cell, 0f,
                                   -(BadgeSize + HubStyle.NavIconGap + HubStyle.NavCaptionHeight + HubStyle.SoonDrop),
                                   new Vector2(0.5f, 1f));
            }
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

        // The Cinder watermark that used to be built here is gone. It was BuildCinderMark, a
        // procedural CinderFragment tinted CinderGlass at 0.32 alpha and anchored to the top-left,
        // introduced by 81bfb95 when this screen's whole background was a flat dark fill.
        //
        // Its reasoning was sound then and does not survive the reskin. DESIGN-ART-BIBLE section 5
        // wants the Cinder as a quiet mark on the Keeper's own neutral floor, and that floor no
        // longer exists - the Hub is a painted village now, edge to edge, with no empty surface for
        // a watermark to be quiet on. Anchored where it was it sat across the barracks roofline and
        // read as an unexplained symbol over the art rather than as a motif behind it.
        //
        // TextureLab.CinderFragment and UITheme.CinderGlass both stay. The tone is still carrying
        // the Splinters badge, and the shape is still the documented Cinder per the art bible, so
        // restoring the motif is a placement decision rather than a rebuild if it is wanted back.

        /// <summary>
        /// The options entry, in the same circular language as the dock: dark fill, thin warm
        /// border, glyph centred. It used to be the one control on the Hub wearing its own tone.
        /// </summary>
        private void BuildOptions()
        {
            UIButton options = UIBuilder.IconButton("Options", Root, "...", 62f,
                                                    UITheme.WithAlpha(HubStyle.Plate, HubStyle.PlateAlpha),
                                                    HubStyle.LabelText);
            UIBuilder.Box(options.Root, -UITheme.ScreenPadding, -(UITheme.TopBarHeight + UITheme.Space16 + 210f),
                          62f, 62f, new Vector2(1f, 1f));
            Image rim = UIBuilder.Img("Ring", options.Root, TextureLab.Ring(128, HubStyle.NavRingThickness),
                                      UITheme.WithAlpha(HubStyle.BorderWarm, HubStyle.BorderAlphaStrong));
            UIBuilder.FillParent(rim.rectTransform, 0f);
            options.OnClick(ShowOptions);
        }

        /// <summary>
        /// The Dev Arena door, and the ONLY place shipped UI knows the arena exists.
        ///
        /// Compiled away entirely outside the editor and a Development Build - not hidden at
        /// runtime, not behind a flag, absent - which is the same compiler gate every file under
        /// Assets/Scripts/Dev carries. A release player has no button because it has no method.
        /// </summary>
        private void BuildDevEntry()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UIButton dev = UIBuilder.IconButton("Dev", Root, "DEV", 62f,
                                                UITheme.WithAlpha(HubStyle.Plate, HubStyle.PlateAlpha),
                                                HubStyle.LabelText);
            UIBuilder.Box(dev.Root, -UITheme.ScreenPadding,
                          -(UITheme.TopBarHeight + UITheme.Space16 + 284f),
                          62f, 62f, new Vector2(1f, 1f));
            dev.OnClick(OpenDevArena);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OpenDevArena()
        {
            // Loaded by name so this does not need a reference to the scene asset. The scene is in
            // the build only when the build was a Development Build; if it is not there, say so
            // rather than failing silently.
            if (!Application.CanStreamedLevelBeLoaded("DevArena"))
            {
                Debug.LogWarning("Emberfall: the Dev Arena scene is not in this build. It is added "
                                 + "only to Development Builds.");
                return;
            }
            UnityEngine.SceneManagement.SceneManager.LoadScene("DevArena");
        }
#endif

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
                                               UITheme.WithAlpha(HubStyle.BorderWarm, HubStyle.BorderAlphaStrong));
                    UIBuilder.Box(ring.rectTransform, 0f, 0f, BadgeSize, BadgeSize, new Vector2(0.5f, 1f));
                    Text hint = UIBuilder.Label("Hint", cell, "SLOT " + (slot + 1), HubStyle.FontCaption,
                                                HubStyle.TextTertiary, TextAnchor.UpperCenter);
                    UIBuilder.Box(hint.rectTransform, 0f, -(BadgeSize + 6f), BadgeCell, 24f, new Vector2(0.5f, 1f));
                    UIBuilder.Shadowed(hint, UITheme.InkArt, 1.4f);
                    continue;
                }

                HeroInstance hero = team[slot];
                // showPlate false: on the Hub the badge sits on the painted map, so a hero with a
                // portrait shows the portrait and its ring against that art with nothing behind it.
                // Only affects heroes that HAVE a portrait - a sigil hero keeps its disc, because a
                // flat emblem with no ground under it would not read against the map at all.
                RectTransform portrait = Widgets.Portrait("Portrait", cell, hero.Def, hero.Def.Stars, BadgeSize, true, false);
                UIBuilder.Box(portrait, 0f, 0f, BadgeSize, BadgeSize, new Vector2(0.5f, 1f));

                // First word only. A cell is BadgeCell wide because that is what the dock's captions
                // need, and a two-word hero name at full length is wider than that - it overflowed
                // into its neighbours and was the other half of why this row read as two different
                // rows. The name that forced this was "Kalyx Emberwright", who has been deleted
                // with the placeholder roster; every current hero name is one word, so the trim is
                // a no-op today and is kept for the roster that is arriving. Heroes shows full
                // names where there is room for them.
                string shortName = hero.Def.Name;
                int space = shortName.IndexOf(' ');
                if (space > 0) shortName = shortName.Substring(0, space);

                Text name = UIBuilder.Label("Name", cell, shortName, HubStyle.FontCaption,
                                                    HubStyle.LabelText, TextAnchor.UpperCenter);
                UIBuilder.Box(name.rectTransform, 0f, -(BadgeSize + 6f), BadgeCell, 24f, new Vector2(0.5f, 1f));
                UIBuilder.Shadowed(name, UITheme.InkArt, 1.4f);
            }
        }

        /// <summary>
        /// True when this screen can safely read the world. Reports the first failure LOUDLY and
        /// exactly once, then stays quiet.
        ///
        /// This is a guard, but deliberately not a silent one, because the state it checks for is
        /// state that genuinely SHOULD exist: GameApp assigns Session in Awake before it builds the
        /// canvas, the router, or any screen, so by the time a MainMenuScreen exists at all its
        /// App.Session has been non-null for several statements. There is no legitimate ordering in
        /// which this returns false. Swallowing that quietly would trade a visible crash for an
        /// invisible one - a Hub that silently stops updating its idle numbers - which is strictly
        /// worse to diagnose.
        ///
        /// What it does buy is the difference between one actionable error and a once-per-second
        /// exception storm, and it names WHICH of the two references is missing, because they mean
        /// different things: a null App means this screen was never bound (Initialise did not run),
        /// while a null App.Session means it was bound to a GameApp whose Awake did not complete.
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

        private void RefreshIdle()
        {
            if (!SessionReady("RefreshIdle")) return;

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

        /// <summary>
        /// The Hub's team node now opens the BANNER, not the collection. Changing who fights is
        /// what a player comes here to do; browsing every hero in the game is reachable one tap
        /// further in, from a button on that screen.
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

        private void OpenAccount()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Account);
        }

        private void OpenFriends()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Friends);
        }

        private void OpenGuild()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Guild);
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
