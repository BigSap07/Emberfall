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
        /// Height of the actual artwork inside hub.png, in source pixels.
        ///
        /// The current file is 1650x780 but only the top 1650x720 is painted - rows 720-779 are a
        /// band of pure white, an export artifact rather than content. It is not off-screen: once
        /// the map is envelope-fitted the band lands inside the visible area and renders as a pale
        /// strip along the bottom edge, behind the dock and the squad. The sprite is therefore cut
        /// from the art region only.
        ///
        /// Trimming also fixes the fit. The whole file is 2.115:1, the artwork alone is 2.292:1,
        /// and the screen is about 2.254:1. Fitting the untrimmed image reserved screen height for
        /// the white band and cropped real painting off the top and bottom to pay for it. The
        /// trimmed sprite is fractionally wider than the screen instead, so the full height of the
        /// painting is visible and roughly 0.8% is lost off each side.
        ///
        /// Expressed as a FRACTION of the texture, never as a pixel count. The importer does not
        /// hand back the file: hub.png is 1650x780 on disk and arrived as a 2048x1024 Texture2D,
        /// because a non-power-of-two texture is rescaled per axis by default. A literal 720-pixel
        /// trim against that texture kept the top 70% of the image and threw away the bottom third
        /// of the village, which is what put every node above its own structure on the first
        /// attempt. A ratio survives any rescale, any maxTextureSize, and any future re-export.
        ///
        /// The importer also DISTORTS: 1650x780 (2.115:1) arrives as 2048x1024 (2.0:1), scaled by a
        /// different factor per axis. Setting npotScale: 0 on the asset does not stop it, because
        /// 1650 is not a multiple of four and the compressed format cannot represent it - that was
        /// tried and reverted rather than left in as a setting that changes nothing. So the true
        /// shape of the artwork is declared here instead of being read back off the texture, and
        /// the fitter is driven from these numbers. Rendering the squashed texture at its authored
        /// aspect undoes the importer's stretch.
        ///
        /// These three are the only place in this screen that knows the source file's dimensions.
        /// When new art ships, update them together and everything else follows.
        /// </summary>
        private const float SourceFileWidth = 1650f;
        private const float SourceFileHeight = 780f;
        private const float SourceArtHeight = 720f;

        // ---------- map node anchors ----------
        // Fractions of the ARTWORK, x then y, measured from its top-left - not fractions of the
        // screen, and not of the source file (the white band above is excluded before these are
        // applied). Every node is parented to the map image and anchored by these, so a node stays
        // on its structure at any aspect ratio. The map is envelope-fitted, so the screen crops it
        // rather than stretching it.
        //
        // MEASURED AGAINST THE CURRENT hub.png, which is a village, not the stone plateau the
        // previous set was measured against. Not one of the old six values survived: the art was
        // replaced wholesale, and a coordinate measured against a different painting is not an
        // approximation of the right answer, it is an unrelated number. The old CampaignY of 0.585
        // landed on a market stall; the old SummonX of 0.645 landed on the campaign portal.
        //
        // How these were measured, so the next person can repeat it rather than guess: a coordinate
        // grid was rendered over the art region (Tools/Screenshots/hub_grid_newart.png), then every
        // candidate was re-cropped at 2x with a crosshair on the proposed point and checked one by
        // one. Both passes are cheap and the first one alone is not enough - the pass that produced
        // the previous set skipped the crosshair check and put HEROES on top of the forge.
        private const float HeroesX = 0.157f, HeroesY = 0.262f;     // the barracks, blue banner
        private const float ForgeX = 0.341f, ForgeY = 0.442f;       // the smithy's lit arch
        private const float SummonX = 0.551f, SummonY = 0.314f;     // the rune obelisk
        private const float MarketX = 0.441f, MarketY = 0.569f;     // the market stall canopy
        private const float CampaignX = 0.663f, CampaignY = 0.452f; // the fire portal
        private const float RecitalX = 0.763f, RecitalY = 0.448f;   // the purple cave mouth

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
            BuildCampaignWaypoint();
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
                    // Cut from the artwork only, never the whole file - see ArtHeight. Unity texture
                    // space has its origin at the bottom-left while the white band sits at the
                    // bottom of the image as viewed, so the region to keep starts at
                    // (height - artHeight), not at zero.
                    float artHeight = painting.height * (SourceArtHeight / SourceFileHeight);
                    _mapSprite = Sprite.Create(painting,
                                               new Rect(0f, painting.height - artHeight, painting.width, artHeight),
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
                fitter.aspectRatio = SourceFileWidth / SourceArtHeight;
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

            RectTransform tag = UIBuilder.Chip("Soon", node.Root, "SOON", UITheme.ChromeBrass, UITheme.FontTiny - 4);
            UIBuilder.Box(tag, 0f, -24f, 92f, 26f, new Vector2(0.5f, 0f));
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
        /// The campaign waypoint: where the player is in the story, and the one button this screen
        /// exists to get them to press. Sits on the monolith like any other node, but is built
        /// apart from them because it is the only one carrying live state and the only one allowed
        /// to be Gold.
        /// </summary>
        private void BuildCampaignWaypoint()
        {
            RectTransform post = UIBuilder.Node("Campaign", _map);
            AnchorOnMap(post, CampaignX, CampaignY, 520f, 214f);

            Image plate = post.gameObject.AddComponent<Image>();
            plate.sprite = TextureLab.RoundedRect(18);
            plate.type = Image.Type.Sliced;
            plate.color = UITheme.WithAlpha(HubBackdrop, 0.86f);
            UIBuilder.Frame("Edge", post, UITheme.WithAlpha(UITheme.ChromeBrass, 0.75f), 18, 2);

            // Both drop to FontTiny-2 and are given hard widths that add up to less than the plate.
            // At FontTiny on a 470-wide plate, "CHAPTER 1 - THE ASHEN ROAD" ran straight through
            // "0 / 12 CLEARED" and the two read as one broken string.
            _chapterLabel = UIBuilder.Label("Chapter", post, "", UITheme.FontTiny - 2, UITheme.TextPrimary, TextAnchor.UpperLeft);
            UIBuilder.Box(_chapterLabel.rectTransform, 18f, -14f, 320f, 24f, new Vector2(0f, 1f));

            _chapterCount = UIBuilder.Label("Count", post, "", UITheme.FontTiny - 2, UITheme.Gold, TextAnchor.UpperRight);
            UIBuilder.Box(_chapterCount.rectTransform, -18f, -14f, 158f, 24f, new Vector2(1f, 1f));

            _chapterFill = UIBuilder.Bar("Bar", post, HubPanelSunken, UITheme.Gold);
            RectTransform barRoot = _chapterFill.rectTransform.parent as RectTransform;
            UIBuilder.Box(barRoot, 0f, -46f, 484f, 8f, new Vector2(0.5f, 1f));

            // GoldButton: the primary action, the same colour the de-aspect pass gave it and the
            // same colour the Claim button uses. Nothing else on this map is allowed to be Gold.
            _continueButton = UIBuilder.GoldButton("Continue", post, "CONTINUE CAMPAIGN", UITheme.FontSmall);
            UIBuilder.Box(_continueButton.Root, 0f, 58f, 484f, 72f, new Vector2(0.5f, 0f));
            _continueButton.OnClick(OpenCampaign);

            _continueSub = UIBuilder.Label("Sub", post, "", UITheme.FontTiny - 2, UITheme.ChromeBrass, TextAnchor.LowerCenter);
            UIBuilder.Box(_continueSub.rectTransform, 0f, 20f, 484f, 28f, new Vector2(0.5f, 0f));
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
            UIBuilder.Box(dock, UITheme.ScreenPadding, UITheme.ScreenPadding, 900f, 150f, new Vector2(0f, 0f));
            UIBuilder.HStack(dock, 18f, new RectOffset(0, 0, 0, 0), TextAnchor.LowerLeft);

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
            UIBuilder.Sized(cell, 132f, 148f);

            UIButton emblem = UIBuilder.IconButton("Button", cell, "", 96f,
                                                   UITheme.WithAlpha(HubPanelRaised, locked ? 0.7f : 0.95f),
                                                   UITheme.TextPrimary);
            UIBuilder.Box(emblem.Root, 0f, 0f, 96f, 96f, new Vector2(0.5f, 1f));
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
            UIBuilder.Box(caption.rectTransform, 0f, -100f, 132f, 24f, new Vector2(0.5f, 1f));
            UIBuilder.Shadowed(caption, UITheme.InkArt, 1.4f);

            if (locked)
            {
                RectTransform tag = UIBuilder.Chip("Soon", cell, "SOON", UITheme.ChromeBrass, UITheme.FontTiny - 6);
                UIBuilder.Box(tag, 0f, -126f, 84f, 22f, new Vector2(0.5f, 1f));
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
            UIBuilder.Box(_teamRow, -(UITheme.ScreenPadding + 12f), UITheme.ScreenPadding, 880f, 152f, new Vector2(1f, 0f));
            UIBuilder.HStack(_teamRow, 16f, new RectOffset(0, 0, 0, 0), TextAnchor.LowerRight);
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
            const float cellWidth = 160f;
            const float portraitSize = 104f;
            List<HeroInstance> team = App.Session.Profile.TeamHeroes();
            for (int slot = 0; slot < PlayerProfile.TeamSize; slot++)
            {
                RectTransform cell = UIBuilder.Node("Slot" + slot, _teamRow);
                UIBuilder.Sized(cell, cellWidth, 148f);

                if (slot >= team.Count)
                {
                    // an open slot is room to grow, not a hole: a ring where the portrait will
                    // stand (same convention as the campaign banner strip) and the slot's number,
                    // so the cell promises a fourth and fifth ally rather than apologising
                    Image ring = UIBuilder.Img("Ring", cell, TextureLab.Ring(96, 5),
                                               UITheme.WithAlpha(UITheme.ChromeBrass, 0.55f));
                    UIBuilder.Box(ring.rectTransform, 0f, -4f, portraitSize, portraitSize, new Vector2(0.5f, 1f));
                    Text hint = UIBuilder.Label("Hint", cell, "SLOT " + (slot + 1), UITheme.FontTiny - 4, UITheme.TextDim, TextAnchor.UpperCenter);
                    UIBuilder.Box(hint.rectTransform, 0f, -114f, cellWidth, 24f, new Vector2(0.5f, 1f));
                    UIBuilder.Shadowed(hint, UITheme.InkArt, 1.4f);
                    continue;
                }

                HeroInstance hero = team[slot];
                RectTransform portrait = Widgets.Portrait("Portrait", cell, hero.Def, hero.Def.Stars, portraitSize, true);
                UIBuilder.Box(portrait, 0f, -4f, portraitSize, portraitSize, new Vector2(0.5f, 1f));

                Text name = UIBuilder.Label("Name", cell, hero.Def.Name, UITheme.FontTiny - 4, UITheme.TextPrimary, TextAnchor.UpperCenter);
                UIBuilder.Box(name.rectTransform, 0f, -114f, cellWidth, 24f, new Vector2(0.5f, 1f));
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
