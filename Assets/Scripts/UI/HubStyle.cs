using UnityEngine;
using UnityEngine.UI;

namespace Emberfall.UI
{
    /// <summary>
    /// Every colour, radius, border weight, opacity, font size and metric the Hub's chrome is made
    /// of, plus the handful of shared components built from them. One place, so the whole Hub
    /// retunes by editing this file and nothing else.
    ///
    /// The rule this file exists to enforce: no call site writes a style literal. If a screen needs
    /// a radius, an alpha or a font size for Hub chrome, it reads it from here. A number that
    /// appears at a call site is a number that cannot be retuned from one place, which is the exact
    /// failure the four disagreeing bottom-row metrics documented in MainMenuScreen were.
    ///
    /// Scope note. This is the HUB's chrome language, not the game's. UITheme stays the shared
    /// vocabulary for every other screen, and this file is deliberately built ON it - the tones
    /// below are UITheme values or alphas of them, never new hexes - so the Hub reads as a warmed
    /// dialect of the game rather than a second unrelated design. The one place that leaks past the
    /// Hub is the top bar, which is a single global instance (see HudTopBar).
    /// </summary>
    public static class HubStyle
    {
        // ------------------------------------------------------------------ surfaces

        /// <summary>
        /// The dark base every Hub surface is a translucent wash of. This is the Hub's whole
        /// surface ramp now: the four separate tones this screen used to declare (backdrop, panel,
        /// raised, sunken) collapse to one base plus the three alphas below, because with
        /// translucent chrome over a painted map the depth comes from how much art shows through,
        /// not from four hand-picked hexes.
        /// </summary>
        public static readonly Color Plate = UITheme.Hex("14121A");

        /// <summary>
        /// The scrim over the painted map. Not chrome, but the same base, and it belongs with the
        /// numbers that tune this screen: it is the one value holding the whole Hub's contrast.
        /// </summary>
        public const float ScrimAlpha = 0.10f;

        /// <summary>
        /// The persistent top bar's own backing and underline. Here rather than in HudTopBar
        /// because the bar is styled in this language now, and its opacity is exactly the kind of
        /// number this file exists to keep in one place.
        /// </summary>
        public const float BarBackingAlpha = 0.92f;

        /// <summary>
        /// Three depths of the same plate. Chrome sitting over the painted map needs more cover
        /// than chrome sitting in a panel, which is the whole difference between these.
        /// </summary>
        public const float PlateAlphaSoft = 0.62f;   // a label over art: reads without hiding it
        public const float PlateAlpha = 0.82f;       // a pill or panel: chrome, clearly on top
        public const float PlateAlphaSolid = 0.92f;  // a badge that must carry small text

        /// <summary>
        /// The warm edge on every Hub surface. ChromeBrass is the project's structural-chrome
        /// accent (see UITheme.ChromeBrass) and is what "subtle warm border" resolves to here.
        /// </summary>
        public static readonly Color BorderWarm = UITheme.ChromeBrass;

        public const float BorderAlpha = 0.42f;      // the default hairline
        public const float BorderAlphaStrong = 0.62f; // a panel edge, which frames more area
        public const float BorderAlphaMuted = 0.24f; // an unavailable entry

        /// <summary>Border weights. Hairline for labels and pills, heavy for panel edges.</summary>
        public const int BorderWeight = 1;
        public const int BorderWeightHeavy = 2;

        // ------------------------------------------------------------------ radii

        public const int RadiusPill = 24;    // fully rounded at PillHeight
        public const int RadiusLabel = 10;   // building labels
        public const int RadiusPanel = 18;   // the idle and campaign panels
        public const int RadiusSoon = 8;     // the SOON badge

        // ------------------------------------------------------------------ text

        /// <summary>Currency and reward values. The Hub's one "this is a number that matters" tone.</summary>
        public static readonly Color ValueGold = UITheme.Gold;

        /// <summary>Label text on a plate - building names, nav captions. Light and warm.</summary>
        public static readonly Color LabelText = UITheme.TextPrimary;

        /// <summary>Secondary lines: rates, timers, sub-captions.</summary>
        public static readonly Color TextSecondary = UITheme.TextMuted;

        /// <summary>The quietest text on the screen: an empty slot's number, a disabled caption.</summary>
        public static readonly Color TextTertiary = UITheme.TextDim;

        /// <summary>The SOON badge's own tone: warm, but deliberately not the label's.</summary>
        public static readonly Color SoonText = UITheme.ChromeBrass;

        /// <summary>
        /// Opacity of a SOON badge relative to the label it hangs off. Subordinate by construction:
        /// a badge painted at this alpha can never read as loud as the label above it, whatever the
        /// label is doing, which is what keeps "coming soon" from competing with "go here".
        /// </summary>
        public const float SoonAlpha = 0.72f;

        /// <summary>Whole-element opacity for an entry that is not available yet.</summary>
        public const float DimAlpha = 0.55f;

        // ------------------------------------------------------------------ font sizes

        public const int FontValue = UITheme.FontSmall;      // currency and reward values
        public const int FontLabel = UITheme.FontTiny;       // building labels
        public const int FontCaption = UITheme.FontTiny - 4; // nav captions
        public const int FontSecondary = UITheme.FontTiny - 2;
        public const int FontSoon = UITheme.FontTiny - 6;    // strictly smaller than FontLabel
        public const int FontRankValue = 30;
        public const int FontButton = UITheme.FontSmall;     // the two gold call-to-action buttons

        // ------------------------------------------------------------------ metrics

        /// <summary>Top-bar pills. One height and one horizontal padding across all of them.</summary>
        public const float PillHeight = 62f;
        public const float PillPadX = 16f;
        /// <summary>Vertical breathing room inside a pill that stacks two things (the rank pill).</summary>
        public const float PillPadY = 9f;
        public const float PillIcon = 40f;
        public const float PillGapIconToValue = 10f;
        public const float PillCurrencyWidth = 200f;
        public const float PillRankWidth = 430f;

        /// <summary>
        /// Building labels. Uniform height for every marker on the map.
        ///
        /// LabelCharWidth is 15 rather than the 12 it was, because the lettering is CINZEL now and
        /// Cinzel is a wide Roman display face where the previous font was condensed. The width of
        /// a marker is computed from its own word, so a per-character figure that is too small
        /// does not clip the text - the label overflows its plate and the longest names on the
        /// map, DEEP RECITAL and CAMPAIGN, hang out of both ends of their banner.
        /// </summary>
        public const float LabelHeight = 48f;
        public const float LabelPadX = 44f;
        public const float LabelMinWidth = 170f;
        public const float LabelCharWidth = 15f;

        /// <summary>
        /// How far above its building a label floats, and the tail that reaches back down to it.
        /// ONE offset for every marker, which is what makes them a system rather than six
        /// separately-placed tags: the map anchors keep pointing at the structures, and the labels
        /// sit a fixed distance above whatever they point at.
        /// </summary>
        public const float LabelLift = 46f;
        public const float TailSize = 14f;

        /// <summary>SOON badge, and how far under its parent label it hangs.</summary>
        public const float SoonWidth = 84f;
        public const float SoonHeight = 24f;
        public const float SoonDrop = 6f;

        /// <summary>Bottom navigation.</summary>
        public const float NavIconGap = 6f;
        public const float NavCaptionHeight = 24f;
        public const int NavRingThickness = 4;
        public const float NavGlyphInset = 26f;

        /// <summary>Panel interiors.</summary>
        public const float PanelPadX = 22f;
        public const float PanelPadY = 16f;

        // ------------------------------------------------------------------ the HUD frame
        //
        // The Hub is four layers now - map, HUD frame, panels, popups - and everything below is
        // the frame's half of that. It hugs the edges by construction: the only numbers here are
        // a distance from an edge, a size, or a gap, and no element is placed anywhere else.

        /// <summary>
        /// The smallest a tap target is allowed to be, in logical canvas units, and the number is
        /// derived rather than picked.
        ///
        /// The canvas scales with ScreenMatchMode.MatchWidthOrHeight at 0.5 against a 1920x1080
        /// reference, so the logical canvas is the geometric blend of the two ratios. On the
        /// narrowest phone this project targets - 2400x1080, 20:9 - that puts the logical canvas
        /// at about 2147x966. The device itself is 1080 physical pixels tall at a density of
        /// about 2.75, which is roughly 393dp, so one logical unit is about 0.41dp and the 44dp
        /// floor lands at about 108 logical units. Every interactive rect in the frame is at
        /// least this on both axes; where the ART is smaller than that (the rail's discs are 76)
        /// the button's hit rect is still TouchMin and the disc is centred inside it.
        /// </summary>
        public const float TouchMin = 108f;

        /// <summary>Distance from the safe-area edge to the nearest HUD element.</summary>
        public const float HudEdge = UITheme.ScreenPadding;

        /// <summary>
        /// WHICH EDGE THE RAIL HUGS, and the one switch on this screen that is a judgement rather
        /// than a measurement.
        ///
        /// It is on the LEFT. It was on the right, as the Hub brief originally asked for, and what
        /// a capture then showed was the cost: the painting puts the Proving's gatehouse hard
        /// against the right edge of the map - it spans roughly map-x 0.82 to 0.97 - and the rail's
        /// discs landed on its right tower at every supported aspect. No marker and no label was
        /// covered, because HubMarkerClamp reserves the rail's column for exactly that reason, and
        /// the gate's own door at 0.915 stayed clear. But the tower behind the rail is painted
        /// architecture, and the frame was sitting on it.
        ///
        /// The left edge is a rock face with no structure until the barracks at about 0.08, so it
        /// is the one placement that satisfies "no HUD element covers a building" outright. The
        /// note that stood here left this as a switch rather than taking it, on the grounds that
        /// which of the two rules wins was not this file's call. It has been called: the map is
        /// layer 0 and the frame gives way to it.
        ///
        /// Flipping this back moves the whole rail, its menu and the marker clamp's reserved
        /// column with it. Nothing else has to change, which is what the switch is for.
        ///
        /// static readonly rather than const deliberately: a const bool folds at compile time and
        /// turns the branch it does not take into an unreachable-code warning on every build.
        /// </summary>
        public static readonly bool RailOnRightEdge = false;

        /// <summary>The right-edge rail: a visible disc inside a TouchMin hit rect.</summary>
        public const float RailDisc = 76f;
        public const float RailGap = 14f;
        /// <summary>Where the rail starts, measured from the top of the safe area: clear of the
        /// currency pills, which end at (TopBarHeight + PillHeight) / 2.</summary>
        public const float RailTop = 106f;

        /// <summary>The pending-request dot on a rail button.</summary>
        public const float BadgeDot = 36f;

        /// <summary>The bottom navigation bar: five cells, centred, one row.</summary>
        public const float NavIcon = 88f;
        public const float NavCellWidth = 168f;
        public const float NavCellHeight = 128f;
        public const float NavCellGap = 12f;
        /// <summary>Distance from the bottom of the safe area to the bottom of the nav row. This
        /// edge is where phones put gesture bars, so it is deliberately larger than HudEdge.</summary>
        public const float NavBottomMargin = 20f;
        /// <summary>Everything stacked above the nav row measures from this one number.</summary>
        public const float NavBarHeight = NavBottomMargin + NavCellHeight;

        /// <summary>The idle chest and the bubble that hangs over it.</summary>
        public const float ChestSize = TouchMin;
        public const float ChestGlyphInset = 18f;
        public const float BubbleWidth = 132f;
        public const float BubbleHeight = 42f;
        public const float BubbleLift = 8f;

        /// <summary>A panel sheet and a popup plate: the two surfaces above the map.</summary>
        public const int RadiusSheet = 22;
        public const int RadiusPopup = 26;
        public const float SheetHeaderHeight = 104f;
        public const float TabHeight = 76f;
        public const float TabGap = 10f;
        public const float TabMinWidth = 200f;

        /// <summary>How dark the map goes under an open panel or popup.</summary>
        public const float PanelScrimAlpha = 0.72f;
        public const float PopupScrimAlpha = 0.62f;

        // ------------------------------------------------------------------ what is left here
        //
        // THE DRAWING IS GONE. Surface, Pill, SurfaceChild, Tail and SoonBadge drew the Hub's own
        // dialect - a dark translucent plate with a warm hairline - and every one of them had no
        // callers left once the building nametags, the currency pills and the player plate moved
        // to Emberforge. Dead code that draws in a retired language is worse than no code: the
        // next person to need a plate finds two ways to make one and only one of them is the
        // kit's.
        //
        // What stays is the METRICS, and they are the reason this file stays at all. The Hub's
        // frame is measured in them - TouchMin, the rail's disc and gap, the nav cell, the chest,
        // the sheet's header and its tab row - and so are the screens that borrow those numbers
        // so their own controls match the Hub's. Those are measurements of THIS GAME'S layout,
        // which is a different kind of fact from what a surface looks like, and Emberforge
        // deliberately owns only the second.

        /// <summary>Width of a building label: the shared padding plus the word.</summary>
        public static float LabelWidthFor(string label)
        {
            return Mathf.Max(LabelMinWidth, LabelPadX + label.Length * LabelCharWidth);
        }
    }
}
