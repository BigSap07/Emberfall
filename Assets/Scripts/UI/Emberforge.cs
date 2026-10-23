using UnityEngine;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;

namespace Emberfall.UI
{
    /// <summary>
    /// EMBERFORGE: every colour, weight, radius and step the game's chrome is made of, in one
    /// file, and the only place any of them is written down.
    ///
    /// The rule this file exists to enforce is the one HubStyle already enforces for the Hub, now
    /// stated for the whole game: A SCREEN NEVER WRITES A STYLE LITERAL. If a component needs a
    /// colour, a weight, a radius or a step, it reads it here. A number at a call site is a number
    /// that cannot be retuned from one place.
    ///
    /// HOW THIS RELATES TO THE TWO STYLE FILES ALREADY HERE.
    ///
    ///   UITheme stays. It owns the reference resolution, the type scale, the spacing scale and
    ///   the game-meaning lookups - aspect, rarity, role, relic grade, armour temper - and those
    ///   are semantics rather than surface. Emberforge names SURFACES, and the two sets do not
    ///   overlap: there is no aspect colour here and no bevel there.
    ///
    ///   HubStyle stays, and stays the Hub MAP's dialect. Its own scope note calls it the Hub's
    ///   chrome language rather than the game's, and that description is still exactly right for
    ///   what it is left holding: the translucent plates the building markers sit on, which must
    ///   stay quiet enough to keep the painting readable underneath. A gold-framed marker over a
    ///   painted gatehouse is a sticker on a painting. Everything in the Hub's FRAME - the plate,
    ///   the currencies, the rail, the nav row, the campaign card, the chest - is Emberforge now.
    ///
    /// THE DIRECTION, so the numbers below read as a set rather than as a palette.
    ///
    ///   A dark outline on everything, at one weight, because the heroes are drawn with a crisp
    ///   dark outline (see DESIGN-ART-BIBLE on what all characters share) and chrome without one
    ///   sits in front of them as a different material rather than as the same world's furniture.
    ///
    ///   Warm brown panels, gold frames, glossy chunky buttons. The reference is the depth a
    ///   mobile idle RPG gets from baked bevels; the palette is Emberfall's own, out of the Ember
    ///   accent and the forge the game is named for.
    ///
    ///   Two button voices and no more. Ember orange is the thing to press; teal is the way back
    ///   out. A third colour of button is a third thing for a player to learn.
    /// </summary>
    public static class Emberforge
    {
        // ------------------------------------------------------------------ the outline
        //
        // ONE colour and ONE weight, on every element without exception. This is the single value
        // that makes a screen read as one kit: a panel, a button, a disc and a portrait frame that
        // share an outline belong to each other even when they share nothing else.

        public static readonly Color Outline = UITheme.Hex("0B0C18");
        public const int OutlineWeight = 3;

        // ------------------------------------------------------------------ panel surface
        //
        // Three stops rather than two. A two-stop panel reads as a flat card lit from one side; a
        // third stop below the midpoint is what makes the bottom of a panel feel like it is
        // sitting in shadow rather than being painted darker.

        public static readonly Color PanelTop = UITheme.Hex("5A3B2C");
        public static readonly Color PanelMid = UITheme.Hex("3C261C");
        public static readonly Color PanelLow = UITheme.Hex("281812");

        /// <summary>The lit lip just inside the frame at the top of a panel: how strongly, and
        /// over how many pixels it fades out.</summary>
        public const float PanelHighlight = 0.22f;
        public const int PanelHighlightDepth = 7;

        /// <summary>The soft inner shadow all the way round the inside of the frame. It is what
        /// separates the frame from the fill without drawing a second line between them.</summary>
        public const float PanelInnerShadow = 0.5f;
        public const int PanelInnerShadowDepth = 11;

        /// <summary>A sunken surface: a trough, an empty slot, a list row behind content. The same
        /// idea inverted, so a recess is lit from the bottom instead of the top.</summary>
        public static readonly Color SunkenTop = UITheme.Hex("180E08");
        public static readonly Color SunkenMid = UITheme.Hex("21140D");
        public static readonly Color SunkenLow = UITheme.Hex("2E1D15");

        // ------------------------------------------------------------------ the gold frame
        //
        // Four stops, read as a struck metal section rather than as a gradient: bright at the
        // outer rim, mid across the face, low into the inner groove, deep where the light does
        // not reach. Which of the four a pixel gets is a function of BOTH its depth through the
        // band and the direction its own surface faces, so the same four numbers give a lit top
        // edge and a dark underside without either being authored separately.

        public static readonly Color GoldLit = UITheme.Hex("FFF4C2");
        public static readonly Color GoldMid = UITheme.Hex("F2C766");
        public static readonly Color GoldLow = UITheme.Hex("D39A36");
        public static readonly Color GoldDeep = UITheme.Hex("9C6420");
        public const int FrameWeight = 5;

        /// <summary>The corner diamond: gold, with an Ember inset. The one piece of ornament in
        /// the kit, and it is four small sprites at a panel's corners rather than anything drawn
        /// along its edges - see DESIGN-ART-BIBLE on the ban on gacha-detail soup.</summary>
        public static readonly Color OrnamentInset = UITheme.Hex("E84532");
        public const float OrnamentSize = 26f;

        /// <summary>
        /// How far in from a framed panel's corner a control has to stay to clear the corner
        /// diamond, on BOTH axes.
        ///
        /// It exists because four screens got this wrong the same way: a full-width button hung
        /// off the bottom of a panel at the panel's own padding lands exactly on the two lower
        /// ornaments and prints itself over them. The diamond is centred OrnamentSize * 0.34 in
        /// from the corner and is OrnamentSize across, so it reaches OrnamentSize * 0.84 - this
        /// is that, rounded up with a little air, so a caller that uses it is clear rather than
        /// touching.
        /// </summary>
        public static float OrnamentClearance
        {
            get { return (UiKit.On ? KitOrnamentSize + KitOrnamentInset : OrnamentSize) + Space4; }
        }

        /// <summary>R26: with the kit on the corner is a filigree (corner_ornament) drawn this large, KitOrnamentInset
        /// in from the panel's corner, and OrnamentClearance grows with it so a control placed by the clearance still
        /// stays clear. Kit off, the small diamond and its old clearance are unchanged.</summary>
        public const float KitOrnamentSize = 48f;

        /// <summary>The gap content at an edge of a small panel keeps from the frame (R26 round 3): what the call site
        /// asked for, or the kit's drawn small frame plus air. Kit off, exactly what it asked for.</summary>
        public static float SmallPadX(float wanted)
        {
            return UiKit.On ? KitFrameRule.ContentPad(wanted, KitFrameRule.SmallDrawnX) : wanted;
        }

        public static float SmallPadY(float wanted)
        {
            return UiKit.On ? KitFrameRule.ContentPad(wanted, KitFrameRule.SmallDrawnY) : wanted;
        }
        public const float KitOrnamentInset = 2f;

        // ------------------------------------------------------------------ buttons
        //
        // A button is a FACE sitting on a LIP sitting inside an EDGE. The lip is the part that
        // makes it a physical object: it is the thickness of the thing, and pressing it is the
        // lip shrinking and the face dropping into the space that frees. That is the whole of
        // the press animation, and it is why the pressed state is drawn rather than tinted.

        public static readonly Color PrimaryTop = UITheme.Hex("FFE08A");
        public static readonly Color PrimaryMid = UITheme.Hex("F5A331");
        public static readonly Color PrimaryLow = UITheme.Hex("D06A14");
        public static readonly Color PrimaryLip = UITheme.Hex("7A3A08");
        public static readonly Color PrimaryEdge = UITheme.Hex("4A2204");

        public static readonly Color SecondaryTop = UITheme.Hex("A9E7EC");
        public static readonly Color SecondaryMid = UITheme.Hex("2FB3C4");
        public static readonly Color SecondaryLow = UITheme.Hex("18798A");
        public static readonly Color SecondaryLip = UITheme.Hex("0B4550");
        public static readonly Color SecondaryEdge = UITheme.Hex("062A31");

        /// <summary>
        /// A DISABLED BUTTON HAS TO RECEDE, and the first ramp here did the opposite.
        ///
        /// It was a cool light grey - 9EA0A8 down to 4E505A - which is BRIGHTER than the warm
        /// brown panel it sits on. On the forge, whose three anvil actions are all unavailable
        /// until the player has Slag, that made the one row a player cannot use the loudest thing
        /// on the page. The capture is what caught it; nothing about the token read wrong on its
        /// own, only against the surface it lands on.
        ///
        /// Warm, and darker than PanelTop at every stop, so an unavailable control reads as the
        /// same object with the light off rather than as a different, paler one.
        /// </summary>
        public static readonly Color DisabledTop = UITheme.Hex("4A433E");
        public static readonly Color DisabledMid = UITheme.Hex("332D2A");
        public static readonly Color DisabledLow = UITheme.Hex("241F1D");
        public static readonly Color DisabledLip = UITheme.Hex("171312");
        public static readonly Color DisabledEdge = UITheme.Hex("0C0A09");

        public const int ButtonLip = 6;
        public const int ButtonLipPressed = 2;

        /// <summary>How far the face drops when pressed. It is ButtonLip minus ButtonLipPressed by
        /// construction rather than by coincidence: the face falls exactly as far as the lip
        /// shrinks, so nothing about the button's outer silhouette moves.</summary>
        public const float ButtonPressDrop = ButtonLip - ButtonLipPressed;

        /// <summary>The glossy highlight over the top of a button's face, as a fraction of its
        /// height, and how strong it is at its brightest.</summary>
        public const float GlossFraction = 0.38f;
        public const float GlossStrength = 0.34f;

        // ------------------------------------------------------------------ the title ribbon

        public static readonly Color RibbonTop = UITheme.Hex("FF8A4C");
        public static readonly Color RibbonMid = UITheme.Hex("E0502A");
        public static readonly Color RibbonLow = UITheme.Hex("8E2A12");

        /// <summary>A ribbon carries a gold edge along its TOP only. A ribbon framed on all four
        /// sides is a second panel; a gold line catching the light along the top is a ribbon.</summary>
        public const int RibbonGoldEdge = 3;
        public const float RibbonHeight = 56f;

        /// <summary>How far a ribbon's tailed ends reach past the panel it labels, per side.</summary>
        public const float RibbonOverhang = 26f;

        // ------------------------------------------------------------------ the map marker
        //
        // The LIGHTEST thing in the kit, and deliberately so. A building marker sits on a painting
        // whose buildings are the primary way into this game, so it has to say the building's name
        // without covering the building. It gets the outline, because everything does, and a thin
        // gold edge so it belongs to the same set as the frame - and then it stops. No heavy
        // frame, no corner ornaments, no gloss.
        //
        // It is also the one surface in the kit that is TRANSLUCENT. The outline and the gold edge
        // are opaque so the shape stays crisp against whatever it lands on; the fill lets a little
        // of the painting through, which is the difference between a banner on a building and a
        // sticker over one.

        public const float MarkerFillAlpha = 0.86f;
        public const int MarkerFrameWeight = 2;
        public const int MarkerRadius = 12;

                // ------------------------------------------------------------------ the currency pill

        public static readonly Color PillFill = UITheme.Hex("120D0A");

        /// <summary>The thin gold line INSIDE a pill, which is what stops a dark pill on a dark
        /// panel reading as a hole.</summary>
        public static readonly Color PillInnerLine = UITheme.Hex("C9973F");
        public const int PillInnerLineWeight = 2;

        /// <summary>The add button on the end of a currency pill. Green because it is the one
        /// control in the kit that means more of this, and nothing else in the kit is green.</summary>
        public static readonly Color PlusTop = UITheme.Hex("A8E86A");
        public static readonly Color PlusMid = UITheme.Hex("5FB93E");
        public static readonly Color PlusLow = UITheme.Hex("2F7A24");
        public const float PlusSize = 42f;

        // ------------------------------------------------------------------ the chip
        //
        // A CHIP IS THE KIT'S ONE TINTABLE SURFACE, and it is two sprites for the reason the
        // currency jewel is two: Image.color multiplies the WHOLE sprite, so a one-piece chip in
        // an aspect colour would have its dark outline tinted along with its fill and every
        // aspect would end up with a differently coloured outline. A dark body carrying the
        // shared outline, with a hairline over it that is the only thing tinted, gives one
        // authored shape that can say aspect, role, rarity grade or temper without the outline
        // ever moving off Emberforge.Outline.

        public static readonly Color ChipFill = UITheme.Hex("17100C");

        /// <summary>How strongly a chip's tinted hairline reads, and how strongly the same tint
        /// washes the fill behind it. The wash is deliberately faint: a chip is a label with a
        /// colour attached, not a coloured button, and a grid of six of them at full strength is
        /// six things shouting at a portrait.</summary>
        public const float ChipLineAlpha = 0.9f;
        public const float ChipWashAlpha = 0.20f;
        public const int ChipRadius = 12;
        public const int ChipLineWeight = 2;

        // ------------------------------------------------------------------ the rule
        //
        // The hairline that separates two sections of one panel. It is gold rather than a grey
        // border because everything structural in this kit is gold, and it carries the outline
        // above and below it so it reads as a struck line inlaid into the panel rather than as a
        // scratch across it.

        public const int RuleHeight = 6;

        // ------------------------------------------------------------------ text
        //
        // Three voices. White for a label sitting ON a colour, which always carries the dark
        // stroke so it survives whatever is behind it; gold for a value or a secondary label;
        // warm off-white for body copy on a panel.

        public static readonly Color TextOnColor = UITheme.Hex("FFFFFF");
        public static readonly Color TextGold = UITheme.Hex("F6CE6C");
        public static readonly Color TextBody = UITheme.Hex("F2E7D8");
        public static readonly Color TextQuiet = UITheme.Hex("A99380");

        /// <summary>The label of a disabled button (R26): dimmer than TextOnColor so it reads as unavailable, still
        /// light enough to read on the kit's stone plate (the harness measures the contrast).</summary>
        public static readonly Color TextDisabled = UITheme.Hex("E0D6C8");
        public static readonly Color TextOnGold = UITheme.Hex("2A1606");

        /// <summary>The dark stroke under a label on colour. One distance for the small sizes and
        /// one for display text, because a 2-unit stroke disappears at 62pt and a 4-unit stroke
        /// closes up the counters at 20pt.</summary>
        public const float StrokeThin = 2f;
        public const float StrokeThick = 3.5f;

        // ------------------------------------------------------------------ geometry

        public const int RadiusButton = 14;
        public const int RadiusPanel = 18;
        public const int RadiusPill = 26;

        /// <summary>The spacing scale. Nothing in a converted screen sits at a gap that is not one
        /// of these six.</summary>
        public const float Space4 = 4f;
        public const float Space8 = 8f;
        public const float Space12 = 12f;
        public const float Space16 = 16f;
        public const float Space24 = 24f;
        public const float Space32 = 32f;

        // ------------------------------------------------------------------ type
        //
        // Cinzel for titles, Barlow Condensed for everything else. Both are SIL Open Font License
        // 1.1 from Google Fonts and both licences ship beside the files in Assets/Resources/Fonts.
        //
        // THEY ARE LEGACY DYNAMIC Font ASSETS, NOT TMP FONT ASSETS, and that is a stated deviation
        // rather than an omission. Every one of the twenty-one screens in this project draws text
        // with UnityEngine.UI.Text, there is no TextMeshPro anywhere in the tree and no TMP
        // package in Packages/manifest.json. A TMP font asset can only be drawn by a TMP_Text
        // component, so importing two of them would have meant either migrating every screen in
        // the game off Text - a change with nothing to do with a design system, touching far more
        // than the screens this pass owns - or shipping two SDF atlases that nothing draws. A
        // dynamic Font renders the same outlines through the same rasteriser and is what the text
        // in this game is actually made of. If the project ever moves to TMP, these same two .ttf
        // files are the input to that font asset and nothing here has to be re-sourced.

        private static Font _title;
        private static Font _ui;

        /// <summary>Cinzel. Titles, ribbons and display numbers only - it is a caps-only Roman
        /// display face and it is hard work at body size, which is the reason for the split.</summary>
        public static Font TitleFont
        {
            get
            {
                if (_title != null) return _title;
                _title = Resources.Load<Font>("Fonts/Cinzel");
                if (_title == null) _title = UiFont;
                return _title;
            }
        }

        /// <summary>Barlow Condensed. Every label, caption, stat and currency figure. Condensed
        /// because this game's numbers live in pills and cells sized for a thumb, and a condensed
        /// face is the difference between a full XP fraction fitting and being abbreviated.</summary>
        public static Font UiFont
        {
            get
            {
                if (_ui != null) return _ui;
                _ui = Resources.Load<Font>("Fonts/BarlowCondensed-SemiBold");
                if (_ui == null) _ui = UITheme.Font;
                return _ui;
            }
        }

        /// <summary>Drops the cached font handles. Only needed when a font is imported or replaced
        /// while the editor is running.</summary>
        public static void ForgetFonts()
        {
            _title = null;
            _ui = null;
        }

        // ------------------------------------------------------------------ rarity

        /// <summary>
        /// A rarity frame is a two-stop gradient, lit stop first. Five tiers, and the ramp between
        /// them is the whole rarity read on a portrait - the star row underneath says the number,
        /// this says it before the player has counted anything.
        /// </summary>
        public static Color RarityLit(int stars)
        {
            switch (stars)
            {
                case 5: return UITheme.Hex("FFE9A8");
                case 4: return UITheme.Hex("C9A6F2");
                case 3: return UITheme.Hex("8DBCF0");
                case 2: return UITheme.Hex("A6D9A0");
            }
            return UITheme.Hex("C8CBD4");
        }

        public static Color RarityDeep(int stars)
        {
            switch (stars)
            {
                case 5: return UITheme.Hex("C08A1E");
                case 4: return UITheme.Hex("A274E6");
                case 3: return UITheme.Hex("4A8FE0");
                case 2: return UITheme.Hex("6FB36A");
            }
            return UITheme.Hex("6E727E");
        }

        /// <summary>
        /// The aspect accent a portrait glows with. Deliberately UITheme.AspectAccent rather than
        /// AspectColor: AspectAccent is the six values DESIGN-ART-BIBLE specifies, AspectColor is
        /// the older set that disagrees with the bible on five of six, and UITheme's own note on
        /// the pair says a screen that has had its visual pass calls AspectAccent. Everything
        /// built out of this file has had one.
        /// </summary>
        public static Color Accent(Aspect aspect)
        {
            return UITheme.AspectAccent(aspect);
        }

        /// <summary>How strongly the aspect glow reads behind a portrait, and how far outside the
        /// frame it reaches as a fraction of the frame's own size.</summary>
        public const float AccentGlowAlpha = 0.62f;
        public const float AccentGlowSpread = 0.15f;

        /// <summary>
        /// How far a rarity PLATE reaches outside the badge it sits behind, as a fraction of the
        /// badge's own size.
        ///
        /// A hero whose portrait Pinoy has drawn arrives as a finished badge with its own gold
        /// frame, so putting a rarity frame around one is two frames on one object. Rarity moves
        /// BEHIND the badge instead: a disc in the tier's colour, reaching out just past the
        /// badge's rim, with the kit's dark outline round it. The badge itself is not resized by
        /// this, which is the point of reaching outward rather than insetting - every screen that
        /// already draws one keeps the portrait at exactly the size it had.
        ///
        /// The four cardinal spikes on the badge art cross the plate, which is why this reads as
        /// a rosette rather than as a coaster under a coin.
        /// </summary>
        public const float BadgePlateSpread = 0.05f;

        /// <summary>The plate's own colours: the tier's deep stop, with its lit stop catching the
        /// top of the rim so the ring is a struck edge rather than a flat circle.</summary>
        public const float BadgePlateLitAlpha = 0.55f;

        // ------------------------------------------------------------------ the summon tell
        //
        // The light a summon builds up commits to a colour before the hero is shown, and that
        // colour IS the rarity frame's colour. It is read off RarityDeep and RarityLit rather than
        // written again here, so the tell and the frame the hero then arrives in can never drift
        // apart: a blue light that reveals a hero in a blue frame is one statement made twice, and
        // a blue light beside a differently blue frame is two statements that nearly agree.
        //
        // See DESIGN-SUMMON-PRESENTATION for the beats these colour.

        /// <summary>
        /// The colour the light holds BEFORE it commits: a cool near-white, so it belongs to no
        /// tier and the tell has to move away from it, never stop at it.
        ///
        /// The first pass used a warm cream, and the timing strip showed what that costs: cream
        /// sits next to gold, so the gold tell was the SMALLEST colour change of the three and
        /// the neutral light half-suggested gold before anything had been decided on screen.
        /// </summary>
        public static readonly Color SummonNeutral = UITheme.Hex("EEF0F6");

        /// <summary>The committed colour of the light for a rolled tier: the frame's deep stop.</summary>
        public static Color SummonTell(int stars)
        {
            return RarityDeep(stars);
        }

        /// <summary>The white-hot centre of the committed light: the frame's lit stop.</summary>
        public static Color SummonTellHot(int stars)
        {
            return RarityLit(stars);
        }

        /// <summary>How dark the room goes behind a summon. Near black rather than black, so the
        /// screen underneath is still faintly there and the summon reads as happening IN the Rift
        /// rather than on a different screen. Measured in the captures: the Rift's brightest
        /// furniture sits at about 17 of 255 under it.</summary>
        public const float SummonDim = 0.93f;
    }
}
