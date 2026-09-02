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

        /// <summary>Building labels. Uniform height for every marker on the map.</summary>
        public const float LabelHeight = 48f;
        public const float LabelPadX = 44f;
        public const float LabelMinWidth = 150f;
        public const float LabelCharWidth = 12f;

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

        // ------------------------------------------------------------------ shared components

        /// <summary>
        /// A dark translucent surface with a warm edge - the one shape all Hub chrome is made of.
        /// Pills, building labels, SOON badges and panels are all this with different numbers, so
        /// they cannot drift apart into four separate looks.
        /// </summary>
        public static Image Surface(RectTransform rt, int radius, float fillAlpha, float edgeAlpha, int edgeWeight)
        {
            Image plate = rt.gameObject.AddComponent<Image>();
            plate.sprite = TextureLab.RoundedRect(radius);
            plate.type = Image.Type.Sliced;
            plate.color = UITheme.WithAlpha(Plate, fillAlpha);
            UIBuilder.Frame("Edge", rt, UITheme.WithAlpha(BorderWarm, edgeAlpha), radius, edgeWeight);
            return plate;
        }

        /// <summary>
        /// A top-bar pill: filled circular icon at the left, value text to its right, on the shared
        /// surface. tint colours the icon only - the value stays ValueGold on every pill, so three
        /// currencies read as three of the same thing rather than three different things.
        /// </summary>
        public static Text Pill(string name, Transform parent, float x, float width, Color tint, string glyph)
        {
            RectTransform pill = UIBuilder.Node(name, parent);
            UIBuilder.Box(pill, x, -(UITheme.TopBarHeight - PillHeight) * 0.5f, width, PillHeight, new Vector2(1f, 1f));
            Surface(pill, RadiusPill, PlateAlpha, BorderAlpha, BorderWeight);

            RectTransform icon = UIBuilder.Node("Icon", pill);
            UIBuilder.Box(icon, PillPadX, 0f, PillIcon, PillIcon, new Vector2(0f, 0.5f));
            Image fill = icon.gameObject.AddComponent<Image>();
            fill.sprite = TextureLab.Circle(96);
            fill.color = tint;
            fill.raycastTarget = false;
            Text mark = UIBuilder.Label("Glyph", icon, glyph, FontCaption, UITheme.TextOnAccent, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(mark.rectTransform, 0f);

            Text value = UIBuilder.Label("Value", pill, "0", FontValue, ValueGold, TextAnchor.MiddleRight);
            float valueLeft = PillPadX + PillIcon + PillGapIconToValue;
            UIBuilder.Box(value.rectTransform, -PillPadX, 0f, width - valueLeft - PillPadX, PillIcon, new Vector2(1f, 0.5f));
            return value;
        }

        /// <summary>
        /// A surface built as a CHILD that fills its parent, rather than on the parent itself.
        ///
        /// Not a stylistic preference - it is the only way the tail below can work. Unity UI draws
        /// a parent's own Graphic BEFORE all of its children, so a tail added as a child of a
        /// plate-bearing node draws on top of that plate instead of behind it, and the half that
        /// was supposed to be buried shows as a diamond-shaped patch of doubled alpha. Moving the
        /// plate into a child that is created AFTER the tail puts the two in the right order.
        /// </summary>
        public static Image SurfaceChild(string name, RectTransform parent, int radius,
                                         float fillAlpha, float edgeAlpha, int edgeWeight)
        {
            RectTransform rt = UIBuilder.Node(name, parent);
            UIBuilder.FillParent(rt, 0f);
            return Surface(rt, radius, fillAlpha, edgeAlpha, edgeWeight);
        }

        /// <summary>
        /// The downward pointer that ties a floating label back to the structure it names. A White
        /// square turned 45 degrees, centred exactly on the plate's bottom edge so the plate buries
        /// its top half and what is left is a triangle aiming at the building. That is a triangle
        /// without adding a sprite to TextureLab for one decoration.
        ///
        /// Must be created BEFORE the plate that buries it - see SurfaceChild.
        /// </summary>
        public static Image Tail(RectTransform parent, float fillAlpha)
        {
            Image tail = UIBuilder.Img("Tail", parent, TextureLab.White, UITheme.WithAlpha(Plate, fillAlpha));
            UIBuilder.Box(tail.rectTransform, 0f, 0f, TailSize, TailSize, new Vector2(0.5f, 0f));
            tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tail.raycastTarget = false;
            return tail;
        }

        /// <summary>
        /// The "not built yet" badge. Smaller face, muted tone and a lower alpha than any label it
        /// hangs off, so it is subordinate by construction rather than by being remembered to be.
        /// </summary>
        public static RectTransform SoonBadge(RectTransform parent, float x, float y, Vector2 anchor)
        {
            RectTransform tag = UIBuilder.Node("Soon", parent);
            UIBuilder.Box(tag, x, y, SoonWidth, SoonHeight, anchor);
            Surface(tag, RadiusSoon, PlateAlphaSolid * SoonAlpha, BorderAlphaMuted, BorderWeight);

            Text label = UIBuilder.Label("Text", tag, "SOON", FontSoon,
                                         UITheme.WithAlpha(SoonText, SoonAlpha), TextAnchor.MiddleCenter);
            UIBuilder.FillParent(label.rectTransform, 0f);
            return tag;
        }

        /// <summary>Width of a building label: the shared padding plus the word.</summary>
        public static float LabelWidthFor(string label)
        {
            return Mathf.Max(LabelMinWidth, LabelPadX + label.Length * LabelCharWidth);
        }
    }
}
