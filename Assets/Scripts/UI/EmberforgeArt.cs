using System.Collections.Generic;
using UnityEngine;

namespace Emberfall.UI
{
    /// <summary>Which outline a shape's silhouette follows. Three is the whole set the kit
    /// needs: every plate, pill, disc and frame is a rounded rectangle (a disc is one whose
    /// radius is half its height), every ornament and jewel is a diamond, and a ribbon's two
    /// ends are the one shape neither of those can express.</summary>
    public enum ForgeShape
    {
        RoundedRect,
        Diamond,
        RibbonTail
    }

    /// <summary>
    /// ONE ELEMENT OF THE KIT, described rather than drawn.
    ///
    /// Everything Emberforge renders is the same section: an OUTLINE, optionally a FRAME band
    /// inside it, optionally a thin INNER LINE inside that, and a FACE filling the rest with a
    /// three-stop vertical gradient, an inner top highlight, a soft inner shadow and - on a
    /// button - a lip along the bottom. A panel, a button, a pill, a tab, a rail disc, a rarity
    /// frame and a corner ornament are all that section with different numbers, which is why
    /// there is one renderer and a table of these rather than twenty drawing routines.
    /// </summary>
    public struct ForgeSpec
    {
        public string Slot;
        public int Width;
        public int Height;
        public ForgeShape Shape;
        public int Radius;

        /// <summary>The dark outline. Every element in the kit has one.</summary>
        public Color Edge;
        public int EdgeWeight;

        /// <summary>The band inside the outline: gold on a panel, a rarity colour on a portrait
        /// frame, absent on most buttons. Four stops read through the surface normal, so the same
        /// numbers light the top edge and shade the underside.</summary>
        public int FrameWeight;
        public Color FrameLit;
        public Color FrameMid;
        public Color FrameLow;
        public Color FrameDeep;

        /// <summary>Gold along the TOP of the silhouette only, for a ribbon. A ribbon framed on
        /// all four sides is a second panel.</summary>
        public bool FrameTopOnly;

        /// <summary>A hairline immediately inside the frame - the gold line in a currency pill,
        /// the dark ring that closes a rarity frame off from the portrait inside it.</summary>
        public Color InnerLine;
        public int InnerLineWeight;

        /// <summary>The three-stop face. An alpha of zero on all three leaves the element hollow,
        /// which is what a portrait frame and a jewel's edge overlay are.</summary>
        public Color FaceTop;
        public Color FaceMid;
        public Color FaceLow;

        /// <summary>The lip along the bottom of a button's face: the thickness of the object.</summary>
        public int Lip;
        public Color LipColor;

        /// <summary>The cavity wall at the TOP of a pressed button's face, in the lip's colour.
        /// A press is the face dropping into the space a shrinking lip frees, so the pressed
        /// sprite has to show what the face has dropped away FROM. Without it the lip just gets
        /// thinner and the button reads as changing size rather than as being pushed.</summary>
        public int Cavity;

        public float Highlight;
        public int HighlightDepth;
        public float InnerShadow;
        public int InnerShadowDepth;

        /// <summary>Stamps a plus through the middle. One control in the kit needs one, and a
        /// baked glyph stays crisp where a Text glyph at 20pt inside a 42-unit disc does not.</summary>
        public int PlusBar;
        public Color PlusColor;

        /// <summary>Nine-slice borders, in pixels, left/top/right/bottom. All four zero means the
        /// sprite is drawn whole and scaled - which is right for a disc and wrong for a plate.</summary>
        public int BorderLeft;
        public int BorderTop;
        public int BorderRight;
        public int BorderBottom;
    }

    /// <summary>
    /// THE PIXELS. Every Emberforge surface is drawn here, from the tokens in Emberforge and
    /// nothing else, and this is the only file in the project that decides what one looks like.
    ///
    /// IT RUNS IN TWO PLACES AND MUST STAY ONE PIECE OF CODE.
    ///
    ///   At IMPORT time, EmberforgeBaker (an editor tool) calls Render on every slot in the
    ///   catalogue and writes the result to Assets/Resources/Sprites/Emberforge/&lt;slot&gt;.png with
    ///   its nine-slice border set on the importer. That is what ships, and it is what the game
    ///   actually draws - see EmberforgeSkin for why baked art rather than a UI shader.
    ///
    ///   At RUNTIME, EmberforgeSkin calls Generate on a slot whose PNG is missing, so a clone
    ///   that has never run the baker still renders the correct kit instead of magenta boxes or
    ///   nothing at all. A fully baked project never reaches this path.
    ///
    /// Splitting those two would be the usual way this rots: the tool and the fallback drift, and
    /// the game looks different depending on whether somebody remembered to press a menu item.
    ///
    /// THE SECTION, top to bottom, because every slot in the catalogue is this and nothing else:
    ///
    ///   outline          EdgeWeight pixels of Emberforge.Outline, on everything
    ///   frame            FrameWeight pixels of a four-stop metal ramp, lit by surface normal
    ///   inner line       InnerLineWeight pixels of a hairline
    ///   face             a three-stop vertical gradient, then an inner shadow, then an inner
    ///                    top highlight, then the lip if there is one
    ///
    /// ANTI-ALIASING IS 3x3 SUPERSAMPLING rather than a distance-based alpha ramp. The ramp works
    /// for a single silhouette and falls apart at a band boundary, where two different colours
    /// both want the same pixel: a gold frame against a dark outline aliases into a bright fringe.
    /// Supersampling resolves both the silhouette and every internal boundary with one mechanism,
    /// and it is free here because it runs once at import rather than per frame.
    /// </summary>
    public static class EmberforgeArt
    {
        /// <summary>Samples per axis. Nine samples per pixel, resolved in premultiplied alpha so
        /// a half-covered edge pixel does not pull the transparent colour into the visible one.</summary>
        private const int Supersample = 3;

        // ------------------------------------------------------------------ the slot catalogue
        //
        // Named constants rather than string literals at the call sites, so the set of surfaces an
        // artist can override is enumerable from one place and a typo is a compile error rather
        // than a silently ignored PNG. This mirrors HudSkin's slot block exactly, and for the same
        // reason.

        public static class Slot
        {
            public const string Panel = "forge_panel";
            public const string PanelPlain = "forge_panel_plain";
            public const string Sunken = "forge_sunken";

            public const string ButtonPrimary = "forge_btn_primary";
            public const string ButtonPrimaryDown = "forge_btn_primary_down";
            public const string ButtonSecondary = "forge_btn_secondary";
            public const string ButtonSecondaryDown = "forge_btn_secondary_down";
            public const string ButtonDisabled = "forge_btn_disabled";
            public const string Gloss = "forge_gloss";

            public const string Ribbon = "forge_ribbon";
            public const string RibbonTail = "forge_ribbon_tail";

            /// <summary>The Hub map's building nametag, and the one translucent surface in the
            /// kit. See the marker section in Emberforge for why it is the lightest thing here.</summary>
            public const string Marker = "forge_marker";
            public const string MarkerTail = "forge_marker_tail";

            public const string Pill = "forge_pill";
            public const string TabOn = "forge_tab_on";
            public const string TabOff = "forge_tab_off";

            public const string BarTrough = "forge_bar_trough";
            public const string BarFill = "forge_bar_fill";

            public const string Disc = "forge_disc";
            public const string DiscDown = "forge_disc_down";
            public const string Plus = "forge_plus";
            public const string Ornament = "forge_ornament";

            public const string JewelBody = "forge_jewel_body";
            public const string JewelEdge = "forge_jewel_edge";

            /// <summary>A rarity portrait frame. One per star tier, so the caller asks for a tier
            /// rather than assembling a filename.</summary>
            public static string Frame(int stars)
            {
                int tier = Mathf.Clamp(stars, 1, 5);
                return "forge_frame_" + tier;
            }
        }

        /// <summary>
        /// Every slot the kit contains, in the order the baker writes them and the gallery shows
        /// them. This list IS the catalogue: a slot that is not here is not baked, is not
        /// overridable and does not exist.
        /// </summary>
        public static string[] AllSlots()
        {
            return new string[]
            {
                Slot.Panel, Slot.PanelPlain, Slot.Sunken,
                Slot.ButtonPrimary, Slot.ButtonPrimaryDown,
                Slot.ButtonSecondary, Slot.ButtonSecondaryDown,
                Slot.ButtonDisabled, Slot.Gloss,
                Slot.Ribbon, Slot.RibbonTail,
                Slot.Marker, Slot.MarkerTail,
                Slot.Pill, Slot.TabOn, Slot.TabOff,
                Slot.BarTrough, Slot.BarFill,
                Slot.Disc, Slot.DiscDown, Slot.Plus, Slot.Ornament,
                Slot.JewelBody, Slot.JewelEdge,
                Slot.Frame(1), Slot.Frame(2), Slot.Frame(3), Slot.Frame(4), Slot.Frame(5)
            };
        }

        // ------------------------------------------------------------------ the recipes

        /// <summary>
        /// What each slot is, as numbers. Nothing here is a colour literal - every value comes
        /// from Emberforge - so retuning the kit is editing that file and re-running the baker.
        ///
        /// THE VERTICAL BORDERS ARE WIDE ON PURPOSE. A nine-slice stretches its middle row, so a
        /// three-stop gradient baked across the whole height would flatten to a single stretched
        /// scanline the moment the element is taller than the sprite. Giving the top and bottom
        /// borders most of the sprite's height puts both ramps inside the fixed regions and leaves
        /// only the mid stop to stretch - so a 400-unit panel gets a lit top band, a long flat
        /// midtone and a shadowed bottom band, which is what the section is meant to read as.
        /// </summary>
        public static ForgeSpec SpecFor(string slot)
        {
            ForgeSpec s = Blank();
            s.Slot = slot;

            if (slot == Slot.Panel)
            {
                s.Width = 160; s.Height = 160; s.Radius = Emberforge.RadiusPanel;
                Face(ref s, Emberforge.PanelTop, Emberforge.PanelMid, Emberforge.PanelLow);
                Gold(ref s, Emberforge.FrameWeight);
                s.Highlight = Emberforge.PanelHighlight;
                s.HighlightDepth = Emberforge.PanelHighlightDepth;
                s.InnerShadow = Emberforge.PanelInnerShadow;
                s.InnerShadowDepth = Emberforge.PanelInnerShadowDepth;
                Border(ref s, 34, 64, 34, 64);
                return s;
            }

            if (slot == Slot.PanelPlain)
            {
                s.Width = 128; s.Height = 128; s.Radius = Emberforge.RadiusPanel;
                Face(ref s, Emberforge.PanelTop, Emberforge.PanelMid, Emberforge.PanelLow);
                s.Highlight = Emberforge.PanelHighlight;
                s.HighlightDepth = Emberforge.PanelHighlightDepth;
                s.InnerShadow = Emberforge.PanelInnerShadow;
                s.InnerShadowDepth = Emberforge.PanelInnerShadowDepth;
                Border(ref s, 26, 50, 26, 50);
                return s;
            }

            if (slot == Slot.Sunken)
            {
                s.Width = 96; s.Height = 96; s.Radius = 12;
                Face(ref s, Emberforge.SunkenTop, Emberforge.SunkenMid, Emberforge.SunkenLow);
                s.InnerShadow = 0.72f;
                s.InnerShadowDepth = 7;
                Border(ref s, 20, 34, 20, 34);
                return s;
            }

            if (slot == Slot.ButtonPrimary || slot == Slot.ButtonPrimaryDown
                || slot == Slot.ButtonSecondary || slot == Slot.ButtonSecondaryDown
                || slot == Slot.ButtonDisabled)
            {
                bool down = slot == Slot.ButtonPrimaryDown || slot == Slot.ButtonSecondaryDown;
                s.Width = 128; s.Height = 80; s.Radius = Emberforge.RadiusButton;
                s.Lip = down ? Emberforge.ButtonLipPressed : Emberforge.ButtonLip;
                s.Cavity = down ? (int)Emberforge.ButtonPressDrop : 0;

                if (slot == Slot.ButtonSecondary || slot == Slot.ButtonSecondaryDown)
                {
                    Face(ref s, Emberforge.SecondaryTop, Emberforge.SecondaryMid, Emberforge.SecondaryLow);
                    s.LipColor = Emberforge.SecondaryLip;
                    s.Edge = Emberforge.SecondaryEdge;
                }
                else if (slot == Slot.ButtonDisabled)
                {
                    Face(ref s, Emberforge.DisabledTop, Emberforge.DisabledMid, Emberforge.DisabledLow);
                    s.LipColor = Emberforge.DisabledLip;
                    s.Edge = Emberforge.DisabledEdge;
                }
                else
                {
                    Face(ref s, Emberforge.PrimaryTop, Emberforge.PrimaryMid, Emberforge.PrimaryLow);
                    s.LipColor = Emberforge.PrimaryLip;
                    s.Edge = Emberforge.PrimaryEdge;
                }

                // The outer edge on a button is its own darkest tone rather than the shared
                // outline, with the outline drawn OUTSIDE it - a button reads as a coloured object
                // with a rim, not as a coloured object in a black box. So the outline is the first
                // pixel of the edge band and the button's own edge colour is the rest of it, which
                // is what EdgeWeight being wider than OutlineWeight expresses.
                s.EdgeWeight = Emberforge.OutlineWeight + 2;

                // Pressed: the face has fallen into the lip, so the top highlight all but goes and
                // the inner shadow deepens. That is what a button looks like when it is down, and
                // it is why the pressed state is a second sprite rather than a colour multiplier.
                s.Highlight = down ? 0.06f : 0.30f;
                s.HighlightDepth = 8;
                s.InnerShadow = down ? 0.55f : 0.26f;
                s.InnerShadowDepth = 9;
                Border(ref s, 24, 28, 24, 30);
                return s;
            }

            if (slot == Slot.Gloss)
            {
                // The only surface in the kit with no silhouette of its own: it is a white wash
                // that rides the top of whatever it is laid over, so a button of any height gets
                // its highlight over the top GlossFraction of ITSELF rather than over whatever
                // fraction a baked-in gloss happened to be authored at.
                s.Width = 96; s.Height = 48; s.Radius = Emberforge.RadiusButton;
                s.EdgeWeight = 0;
                s.FaceTop = new Color(1f, 1f, 1f, Emberforge.GlossStrength);
                s.FaceMid = new Color(1f, 1f, 1f, Emberforge.GlossStrength * 0.45f);
                s.FaceLow = new Color(1f, 1f, 1f, 0f);
                Border(ref s, 20, 20, 20, 4);
                return s;
            }

            if (slot == Slot.Ribbon)
            {
                s.Width = 128; s.Height = 64; s.Radius = 6;
                Face(ref s, Emberforge.RibbonTop, Emberforge.RibbonMid, Emberforge.RibbonLow);
                Gold(ref s, Emberforge.RibbonGoldEdge);
                s.FrameTopOnly = true;
                s.Highlight = 0.18f;
                s.HighlightDepth = 6;
                s.InnerShadow = 0.34f;
                s.InnerShadowDepth = 9;
                Border(ref s, 16, 26, 16, 26);
                return s;
            }

            if (slot == Slot.RibbonTail)
            {
                // Drawn as the RIGHT tail and mirrored on x for the left, so the two ends of a
                // ribbon cannot drift apart into two shapes.
                s.Width = 40; s.Height = 64; s.Shape = ForgeShape.RibbonTail; s.Radius = 4;
                Face(ref s, Emberforge.RibbonMid, Emberforge.RibbonLow, Emberforge.RibbonLow);
                s.InnerShadow = 0.5f;
                s.InnerShadowDepth = 8;
                return s;
            }

            if (slot == Slot.Marker)
            {
                s.Width = 96; s.Height = 56; s.Radius = Emberforge.MarkerRadius;
                Face(ref s, UITheme.WithAlpha(Emberforge.PanelTop, Emberforge.MarkerFillAlpha),
                     UITheme.WithAlpha(Emberforge.PanelMid, Emberforge.MarkerFillAlpha),
                     UITheme.WithAlpha(Emberforge.PanelLow, Emberforge.MarkerFillAlpha));
                Gold(ref s, Emberforge.MarkerFrameWeight);
                s.Highlight = 0.16f;
                s.HighlightDepth = 5;
                s.InnerShadow = 0.3f;
                s.InnerShadowDepth = 7;
                Border(ref s, 22, 22, 22, 22);
                return s;
            }

            if (slot == Slot.MarkerTail)
            {
                // The pointer that ties a marker back to the structure it names, as a DIAMOND with
                // the same outline and the same translucent fill. It is drawn before the plate and
                // half buried by it, so what is left is a triangle aiming at the building - the
                // same trick HubStyle.Tail used, now carrying the kit's outline instead of being a
                // bare rotated square.
                s.Width = 28; s.Height = 28; s.Shape = ForgeShape.Diamond;
                s.EdgeWeight = Emberforge.OutlineWeight;
                Face(ref s, UITheme.WithAlpha(Emberforge.PanelMid, Emberforge.MarkerFillAlpha),
                     UITheme.WithAlpha(Emberforge.PanelLow, Emberforge.MarkerFillAlpha),
                     UITheme.WithAlpha(Emberforge.PanelLow, Emberforge.MarkerFillAlpha));
                return s;
            }

            if (slot == Slot.Pill)
            {
                s.Width = 128; s.Height = 64; s.Radius = 32;
                Face(ref s, Emberforge.PillFill, Emberforge.PillFill, Emberforge.PillFill);
                s.InnerLine = Emberforge.PillInnerLine;
                s.InnerLineWeight = Emberforge.PillInnerLineWeight;
                s.InnerShadow = 0.4f;
                s.InnerShadowDepth = 8;
                Border(ref s, 34, 30, 34, 30);
                return s;
            }

            if (slot == Slot.TabOn || slot == Slot.TabOff)
            {
                bool on = slot == Slot.TabOn;
                s.Width = 128; s.Height = 72; s.Radius = Emberforge.RadiusButton;
                if (on) Face(ref s, Emberforge.PanelTop, Emberforge.PanelMid, Emberforge.PanelMid);
                else Face(ref s, Emberforge.PanelLow, UITheme.Shade(Emberforge.PanelLow, -0.2f),
                          UITheme.Shade(Emberforge.PanelLow, -0.35f));
                if (on) Gold(ref s, 3);
                s.Highlight = on ? 0.24f : 0.05f;
                s.HighlightDepth = 6;
                s.InnerShadow = on ? 0.2f : 0.5f;
                s.InnerShadowDepth = 9;
                Border(ref s, 22, 26, 22, 26);
                return s;
            }

            if (slot == Slot.BarTrough)
            {
                s.Width = 64; s.Height = 36; s.Radius = 18;
                Face(ref s, Emberforge.SunkenTop, Emberforge.SunkenMid, Emberforge.SunkenLow);
                s.InnerShadow = 0.8f;
                s.InnerShadowDepth = 6;
                Border(ref s, 20, 15, 20, 15);
                return s;
            }

            if (slot == Slot.BarFill)
            {
                // No outline: the fill sits inside the trough, which already has one, and a second
                // dark line between a trough and its own fill reads as a gap rather than as depth.
                s.Width = 64; s.Height = 36; s.Radius = 18;
                s.EdgeWeight = 0;
                Face(ref s, Emberforge.GoldLit, Emberforge.GoldMid, Emberforge.GoldLow);
                s.Highlight = 0.4f;
                s.HighlightDepth = 6;
                Border(ref s, 20, 15, 20, 15);
                return s;
            }

            if (slot == Slot.Disc || slot == Slot.DiscDown)
            {
                bool down = slot == Slot.DiscDown;
                s.Width = 128; s.Height = 128; s.Radius = 64;
                if (down) Face(ref s, Emberforge.PanelLow, Emberforge.PanelLow, Emberforge.PanelMid);
                else Face(ref s, Emberforge.PanelTop, Emberforge.PanelMid, Emberforge.PanelLow);
                Gold(ref s, Emberforge.FrameWeight);
                s.Highlight = down ? 0.05f : 0.26f;
                s.HighlightDepth = 9;
                s.InnerShadow = down ? 0.6f : 0.34f;
                s.InnerShadowDepth = 12;
                return s;
            }

            if (slot == Slot.Plus)
            {
                s.Width = 64; s.Height = 64; s.Radius = 32;
                Face(ref s, Emberforge.PlusTop, Emberforge.PlusMid, Emberforge.PlusLow);
                s.Highlight = 0.3f;
                s.HighlightDepth = 7;
                s.InnerShadow = 0.3f;
                s.InnerShadowDepth = 8;
                s.PlusBar = 7;
                s.PlusColor = Emberforge.TextOnColor;
                return s;
            }

            if (slot == Slot.Ornament)
            {
                s.Width = 36; s.Height = 36; s.Shape = ForgeShape.Diamond; s.Radius = 0;
                Face(ref s, Emberforge.OrnamentInset,
                     UITheme.Shade(Emberforge.OrnamentInset, -0.18f),
                     UITheme.Shade(Emberforge.OrnamentInset, -0.4f));
                Gold(ref s, 4);
                s.EdgeWeight = 2;
                s.InnerShadow = 0.35f;
                s.InnerShadowDepth = 4;
                return s;
            }

            if (slot == Slot.JewelBody)
            {
                // WHITE, so the one sprite serves every currency: the pill tints this and lays the
                // untinted edge over it. Baking one jewel per currency would mean a new bake every
                // time the economy grows a token.
                s.Width = 56; s.Height = 56; s.Shape = ForgeShape.Diamond;
                s.EdgeWeight = 0;
                Face(ref s, Color.white, new Color(0.82f, 0.82f, 0.82f, 1f), new Color(0.55f, 0.55f, 0.55f, 1f));
                s.Highlight = 0.5f;
                s.HighlightDepth = 10;
                s.InnerShadow = 0.3f;
                s.InnerShadowDepth = 8;
                return s;
            }

            if (slot == Slot.JewelEdge)
            {
                s.Width = 56; s.Height = 56; s.Shape = ForgeShape.Diamond;
                s.EdgeWeight = 2;
                Gold(ref s, 3);
                s.FaceTop = Color.clear; s.FaceMid = Color.clear; s.FaceLow = Color.clear;
                return s;
            }

            for (int stars = 1; stars <= 5; stars++)
            {
                if (slot != Slot.Frame(stars)) continue;
                s.Width = 160; s.Height = 160; s.Radius = 80;
                s.FrameWeight = 7;
                Color lit = Emberforge.RarityLit(stars);
                Color deep = Emberforge.RarityDeep(stars);
                s.FrameLit = lit;
                s.FrameMid = UITheme.Mix(lit, deep, 0.4f);
                s.FrameLow = deep;
                s.FrameDeep = UITheme.Shade(deep, -0.35f);
                // Hollow: the portrait is what goes inside it.
                s.FaceTop = Color.clear; s.FaceMid = Color.clear; s.FaceLow = Color.clear;
                // The dark ring that closes the frame off from the art it holds, so a bright
                // portrait does not bleed into a bright frame.
                s.InnerLine = Emberforge.Outline;
                s.InnerLineWeight = 2;
                return s;
            }

            // An unknown slot is a programming error rather than a content one, so it is loud and
            // it still returns something drawable - a bare outlined plate - rather than null.
            Debug.LogError("EmberforgeArt: no recipe for slot '" + slot + "'. Returning a bare plate. "
                         + "Every slot must appear in SpecFor and in AllSlots.");
            s.Width = 64; s.Height = 64; s.Radius = 8;
            Face(ref s, Emberforge.PanelMid, Emberforge.PanelMid, Emberforge.PanelLow);
            Border(ref s, 16, 16, 16, 16);
            return s;
        }

        private static ForgeSpec Blank()
        {
            ForgeSpec s = new ForgeSpec();
            s.Shape = ForgeShape.RoundedRect;
            s.Edge = Emberforge.Outline;
            s.EdgeWeight = Emberforge.OutlineWeight;
            s.HighlightDepth = 6;
            s.InnerShadowDepth = 8;
            return s;
        }

        private static void Face(ref ForgeSpec s, Color top, Color mid, Color low)
        {
            s.FaceTop = top;
            s.FaceMid = mid;
            s.FaceLow = low;
        }

        private static void Gold(ref ForgeSpec s, int weight)
        {
            s.FrameWeight = weight;
            s.FrameLit = Emberforge.GoldLit;
            s.FrameMid = Emberforge.GoldMid;
            s.FrameLow = Emberforge.GoldLow;
            s.FrameDeep = Emberforge.GoldDeep;
        }

        private static void Border(ref ForgeSpec s, int left, int top, int right, int bottom)
        {
            s.BorderLeft = left;
            s.BorderTop = top;
            s.BorderRight = right;
            s.BorderBottom = bottom;
        }

        // ------------------------------------------------------------------ rendering

        /// <summary>
        /// Renders a slot to a texture. The caller decides what to do with it: the baker writes a
        /// PNG, the runtime fallback wraps it in a Sprite.
        /// </summary>
        public static Texture2D Render(ForgeSpec spec)
        {
            int w = Mathf.Max(4, spec.Width);
            int h = Mathf.Max(4, spec.Height);
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color32[] pixels = new Color32[w * h];
            float step = 1f / Supersample;
            float half = step * 0.5f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Premultiplied accumulation. Averaging straight colours across a boundary
                    // pulls the transparent side's RGB into the visible side and haloes every
                    // silhouette; premultiplying and dividing back out at the end does not.
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (int sy = 0; sy < Supersample; sy++)
                    {
                        for (int sx = 0; sx < Supersample; sx++)
                        {
                            float px = x + half + sx * step;
                            float py = y + half + sy * step;
                            Color c = Sample(spec, px, py, w, h);
                            r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;
                        }
                    }

                    int n = Supersample * Supersample;
                    a /= n;
                    if (a <= 0.0001f)
                    {
                        pixels[y * w + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }
                    r /= n * a; g /= n * a; b /= n * a;
                    pixels[y * w + x] = new Color32(
                        (byte)Mathf.Clamp(r * 255f, 0f, 255f),
                        (byte)Mathf.Clamp(g * 255f, 0f, 255f),
                        (byte)Mathf.Clamp(b * 255f, 0f, 255f),
                        (byte)Mathf.Clamp(a * 255f, 0f, 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// One sample of the section. Hard-edged by design: every boundary here is a comparison
        /// rather than a blend, because the supersampler above is what softens them and doing it
        /// twice gives a kit that looks slightly out of focus.
        /// </summary>
        private static Color Sample(ForgeSpec spec, float px, float py, int w, int h)
        {
            float d = Distance(spec, px, py, w, h);
            if (d >= 0f) return Color.clear;

            float depth = -d;                       // how far inside the silhouette this sample is
            Vector2 normal = Normal(spec, px, py, w, h);

            // 1 where the surface faces straight up, 0 where it faces straight down. The single
            // value that turns four flat colours into a struck metal section.
            float lit = Mathf.Clamp01(normal.y * 0.5f + 0.5f);

            if (depth < spec.EdgeWeight)
            {
                // The shared dark outline is always the OUTERMOST pixels, whatever the element's
                // own edge colour is. On a panel the two are the same colour and this is invisible;
                // on a button it is the difference between a rimmed object and a boxed one.
                if (depth < Emberforge.OutlineWeight) return Emberforge.Outline;
                return spec.Edge;
            }

            float frameHere = spec.FrameWeight;
            if (spec.FrameTopOnly && normal.y < 0.35f) frameHere = 0f;

            if (frameHere > 0f && depth < spec.EdgeWeight + frameHere)
            {
                float u = (depth - spec.EdgeWeight) / frameHere;   // 0 at the rim, 1 at the groove
                return Metal(spec, u, lit);
            }

            float inner = depth - spec.EdgeWeight - frameHere;

            if (spec.InnerLineWeight > 0 && inner < spec.InnerLineWeight)
            {
                return spec.InnerLine;
            }

            inner -= spec.InnerLineWeight;
            return FaceColor(spec, px, py, w, h, inner);
        }

        /// <summary>The four-stop metal ramp: bright rim, mid face, low groove, deep underside,
        /// then lit or shaded by which way this piece of the section is facing.</summary>
        private static Color Metal(ForgeSpec spec, float u, float lit)
        {
            Color c;
            if (u < 0.3f) c = Color.Lerp(spec.FrameLit, spec.FrameMid, u / 0.3f);
            else if (u < 0.65f) c = Color.Lerp(spec.FrameMid, spec.FrameLow, (u - 0.3f) / 0.35f);
            else c = Color.Lerp(spec.FrameLow, spec.FrameDeep, (u - 0.65f) / 0.35f);

            float shade = 0.72f + 0.5f * lit;
            return new Color(c.r * shade, c.g * shade, c.b * shade, c.a);
        }

        private static Color FaceColor(ForgeSpec spec, float px, float py, int w, int h, float inner)
        {
            // v runs 0 at the TOP of the sprite to 1 at the bottom. Texture rows are indexed from
            // the bottom, which is the one place in this file that matters.
            float v = 1f - (py / h);
            Color face = v < 0.5f
                ? Color.Lerp(spec.FaceTop, spec.FaceMid, v / 0.5f)
                : Color.Lerp(spec.FaceMid, spec.FaceLow, (v - 0.5f) / 0.5f);

            if (face.a <= 0f) return face;

            // The soft inner shadow, all the way round. Exponential rather than linear because a
            // linear falloff over the same depth reads as a second band rather than as shading.
            if (spec.InnerShadow > 0f && spec.InnerShadowDepth > 0)
            {
                float k = spec.InnerShadow * Mathf.Exp(-inner / spec.InnerShadowDepth);
                face = new Color(Mathf.Lerp(face.r, Emberforge.Outline.r, k),
                                 Mathf.Lerp(face.g, Emberforge.Outline.g, k),
                                 Mathf.Lerp(face.b, Emberforge.Outline.b, k),
                                 face.a);
            }

            // The lit lip just inside the top of the section, measured from the top of the SPRITE
            // rather than from the silhouette, so it stays a horizontal band across the top of a
            // panel instead of following the corners round.
            if (spec.Highlight > 0f && spec.HighlightDepth > 0)
            {
                float fromTop = (h - py) - spec.EdgeWeight - spec.FrameWeight - spec.InnerLineWeight;
                if (fromTop >= 0f && fromTop < spec.HighlightDepth)
                {
                    float k = 1f - fromTop / spec.HighlightDepth;
                    k = spec.Highlight * k * k;
                    face = new Color(Mathf.Lerp(face.r, 1f, k), Mathf.Lerp(face.g, 1f, k),
                                     Mathf.Lerp(face.b, 1f, k), face.a);
                }
            }

            // The cavity the face has dropped into, on a pressed button. Drawn before the lip so
            // a sprite short enough for the two to meet resolves to the lip rather than to a gap.
            if (spec.Cavity > 0)
            {
                float belowTop = (h - py) - spec.EdgeWeight - spec.FrameWeight - spec.InnerLineWeight;
                if (belowTop < spec.Cavity) face = spec.LipColor;
            }

            // The lip: the bottom of a button's face, and the thing that makes it an object with a
            // thickness rather than a coloured rectangle. Drawn last so nothing above shades it.
            if (spec.Lip > 0)
            {
                float fromBottom = py - spec.EdgeWeight;
                if (fromBottom < spec.Lip)
                {
                    face = spec.LipColor;
                }
                else if (fromBottom < spec.Lip + 1f)
                {
                    // One lighter scanline where the face meets the lip. Without it the two tones
                    // meet as a colour change; with it the face reads as sitting ON the lip.
                    face = Color.Lerp(face, Color.white, 0.22f);
                }
            }

            // The stamped plus. Inside the face so it picks up nothing else, and square-cut so it
            // stays crisp at every size the disc is drawn at.
            if (spec.PlusBar > 0)
            {
                float cx = w * 0.5f;
                float cy = h * 0.5f;
                float arm = Mathf.Min(w, h) * 0.26f;
                float halfBar = spec.PlusBar * 0.5f;
                bool across = Mathf.Abs(px - cx) <= arm && Mathf.Abs(py - cy) <= halfBar;
                bool down = Mathf.Abs(py - cy) <= arm && Mathf.Abs(px - cx) <= halfBar;
                if (across || down) face = spec.PlusColor;
            }

            return face;
        }

        // ------------------------------------------------------------------ silhouettes

        /// <summary>Signed distance to the silhouette. Negative inside, zero on the edge.</summary>
        private static float Distance(ForgeSpec spec, float px, float py, int w, int h)
        {
            float cx = w * 0.5f;
            float cy = h * 0.5f;

            if (spec.Shape == ForgeShape.Diamond)
            {
                float hw = cx;
                float hh = cy;
                float ax = Mathf.Abs(px - cx);
                float ay = Mathf.Abs(py - cy);
                return (ax * hh + ay * hw - hw * hh) / Mathf.Sqrt(hw * hw + hh * hh);
            }

            if (spec.Shape == ForgeShape.RibbonTail)
            {
                // A rectangle with a triangular bite taken out of its right-hand edge: the swallow
                // tail that finishes a ribbon. Intersecting the rectangle with the OUTSIDE of a
                // diamond centred past the right edge is what cuts the notch, and doing it as a
                // distance rather than as a mask keeps the notch anti-aliased like everything else.
                float rect = RoundedDistance(px, py, w, h, spec.Radius);
                float ax = Mathf.Abs(py - cy);
                float notch = (w - px) - (h * 0.5f - ax) * 0.75f;
                return Mathf.Max(rect, -notch);
            }

            return RoundedDistance(px, py, w, h, spec.Radius);
        }

        private static float RoundedDistance(float px, float py, float w, float h, float radius)
        {
            float cx = w * 0.5f;
            float cy = h * 0.5f;
            float r = Mathf.Min(radius, Mathf.Min(cx, cy));
            float dx = Mathf.Abs(px - cx) - (cx - r);
            float dy = Mathf.Abs(py - cy) - (cy - r);
            float ax = Mathf.Max(dx, 0f);
            float ay = Mathf.Max(dy, 0f);
            return Mathf.Sqrt(ax * ax + ay * ay) + Mathf.Min(Mathf.Max(dx, dy), 0f) - r;
        }

        /// <summary>
        /// The outward normal of the silhouette at this point, which is what lights the frame.
        /// Analytic rather than a numerical gradient: four extra distance evaluations per sample,
        /// times nine samples, times every pixel of twenty-seven sprites is real time even at
        /// import, and the two shapes that need it have normals that are two lines each.
        /// </summary>
        private static Vector2 Normal(ForgeSpec spec, float px, float py, int w, int h)
        {
            float cx = w * 0.5f;
            float cy = h * 0.5f;
            float sx = px < cx ? -1f : 1f;
            float sy = py < cy ? -1f : 1f;

            if (spec.Shape == ForgeShape.Diamond)
            {
                Vector2 n = new Vector2(sx * cy, sy * cx);
                return n.normalized;
            }

            float r = Mathf.Min(spec.Radius, Mathf.Min(cx, cy));
            float dx = Mathf.Abs(px - cx) - (cx - r);
            float dy = Mathf.Abs(py - cy) - (cy - r);

            if (dx > 0f && dy > 0f)
            {
                Vector2 n = new Vector2(sx * dx, sy * dy);
                return n.sqrMagnitude > 0f ? n.normalized : new Vector2(0f, sy);
            }
            return dx > dy ? new Vector2(sx, 0f) : new Vector2(0f, sy);
        }

        // ------------------------------------------------------------------ runtime fallback

        private static readonly Dictionary<string, Sprite> Generated = new Dictionary<string, Sprite>();

        /// <summary>
        /// Builds a slot's sprite in memory, borders and all.
        ///
        /// This is the path a project that has never run the baker takes, and the only reason it
        /// exists is that a clone with no generated PNGs must still LOOK like Emberfall rather
        /// than like a bug report. It costs CPU and garbage at load, which is exactly what baking
        /// the kit is for, so EmberforgeSkin says out loud when it lands here.
        /// </summary>
        public static Sprite Generate(string slot)
        {
            Sprite cached;
            if (Generated.TryGetValue(slot, out cached)) return cached;

            ForgeSpec spec = SpecFor(slot);
            Texture2D tex = Render(spec);
            Vector4 border = new Vector4(spec.BorderLeft, spec.BorderBottom, spec.BorderRight, spec.BorderTop);
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, spec.Width, spec.Height),
                                          new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            Generated[slot] = sprite;
            return sprite;
        }

        /// <summary>Drops every generated sprite. Only needed when the tokens change while the
        /// editor is running.</summary>
        public static void Forget()
        {
            Generated.Clear();
        }
    }
}
