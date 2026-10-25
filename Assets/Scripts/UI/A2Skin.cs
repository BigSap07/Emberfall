using System.Collections.Generic;
using UnityEngine;
using Emberfall.Core.Session;

namespace Emberfall.UI
{
    /// <summary>
    /// THE A2 SKIN FOR THE SHARED WIDGETS (R56).
    ///
    /// EmberforgeUI draws every panel, well, button, tab, pill, disc, ribbon and bar from a NAMED SLOT
    /// that EmberforgeSkin resolves. While SkinA2Switch is on, EmberforgeSkin asks this class first. For
    /// a slot it covers, the answer is the SAME shape the kit already draws (EmberforgeArt.SpecFor: the
    /// same width, height, radius and nine-slice border, so no layout moves by a pixel) with the A2
    /// tokens poured in: flat faces, a 3 unit ink outline, a bottom bevel on the chunky ones, no
    /// gradient, no highlight, no inner shadow, no gold. A2Kit owns the colours; this class holds none.
    ///
    /// Slots it does not cover (markers, jewels, chips, plus, rarity frames, the ornament) keep their
    /// art: they are hero art, map labels and semantic marks, and R56 does not repaint them.
    ///
    /// OFF IS THE OLD LOOK. With the switch off, Forge answers null for every slot and the other
    /// helpers answer what the old code did.
    /// </summary>
    public static class A2Skin
    {
        /// <summary>The prefix every sprite from this class carries in its name, which is how a label
        /// finds out it sits on a cream surface (see SurfaceOf).</summary>
        public const string Prefix = "a2_";

        public const string QuietButton = "btn_quiet";
        public const string QuietButtonDown = "btn_quiet_down";

        /// <summary>The slots Forge answers, in one place so the harness can pin the list.</summary>
        public static readonly string[] Covered =
        {
            EmberforgeArt.Slot.Panel, EmberforgeArt.Slot.PanelPlain, EmberforgeArt.Slot.Sunken,
            EmberforgeArt.Slot.ButtonPrimary, EmberforgeArt.Slot.ButtonPrimaryDown,
            EmberforgeArt.Slot.ButtonSecondary, EmberforgeArt.Slot.ButtonSecondaryDown,
            EmberforgeArt.Slot.ButtonDisabled, EmberforgeArt.Slot.Gloss,
            EmberforgeArt.Slot.Ribbon, EmberforgeArt.Slot.RibbonTail,
            EmberforgeArt.Slot.Pill, EmberforgeArt.Slot.TabOn, EmberforgeArt.Slot.TabOff,
            EmberforgeArt.Slot.BarTrough, EmberforgeArt.Slot.BarFill,
            EmberforgeArt.Slot.Disc, EmberforgeArt.Slot.DiscDown, EmberforgeArt.Slot.Rule,
            QuietButton, QuietButtonDown
        };

        // A disabled button: a grey brown plate, not a faded orange one.
        private static readonly Color DisabledFace = A2Kit.Hex(0x6B6052);
        private static readonly Color DisabledBevel = A2Kit.Hex(0x4A4137);

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static bool On { get { return SkinA2Switch.Enabled; } }

        public static bool Covers(string slot)
        {
            if (!On || slot == null) return false;
            for (int i = 0; i < Covered.Length; i++)
                if (Covered[i] == slot) return true;
            return false;
        }

        /// <summary>The A2 sprite for a slot, or null (switch off, or a slot R56 leaves alone).</summary>
        public static Sprite Forge(string slot)
        {
            if (!Covers(slot)) return null;
            Sprite cached;
            if (Cache.TryGetValue(slot, out cached) && cached != null) return cached;

            ForgeSpec spec = SpecOf(slot);
            Texture2D tex = EmberforgeArt.Render(spec);
            Vector4 border = new Vector4(spec.BorderLeft, spec.BorderBottom, spec.BorderRight, spec.BorderTop);
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, spec.Width, spec.Height),
                                          new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = Prefix + slot;
            Cache[slot] = sprite;
            return sprite;
        }

        public static void Forget()
        {
            Cache.Clear();
        }

        // ------------------------------------------------------------------ the recipes

        private static ForgeSpec SpecOf(string slot)
        {
            // The same geometry as the kit slot this one stands in for.
            string basis = slot == QuietButton ? EmberforgeArt.Slot.ButtonSecondary
                         : slot == QuietButtonDown ? EmberforgeArt.Slot.ButtonSecondaryDown
                         : slot;
            ForgeSpec s = EmberforgeArt.SpecFor(basis);

            // Everything A2 does not have: gold, gradients, the lit top lip, the inner shadow, hairlines.
            s.FrameWeight = 0;
            s.InnerLineWeight = 0;
            s.Highlight = 0f;
            s.InnerShadow = 0f;
            s.EdgeOnly = true;
            s.EdgeWeight = 3;
            s.Lip = 0;
            s.Cavity = 0;

            if (slot == EmberforgeArt.Slot.Panel)
            {
                Flat(ref s, A2Kit.Cream, A2Kit.Ink, 20);
                Bevel(ref s, A2Kit.InkDeep, 4, 0);
            }
            else if (slot == EmberforgeArt.Slot.PanelPlain)
            {
                Flat(ref s, A2Kit.Card, A2Kit.Ink, 16);
                Bevel(ref s, A2Kit.InkDeep, 3, 0);
            }
            else if (slot == EmberforgeArt.Slot.Sunken)
            {
                Flat(ref s, A2Kit.CreamWell, A2Kit.CreamWellInset, 12);
            }
            else if (slot == EmberforgeArt.Slot.ButtonPrimary || slot == EmberforgeArt.Slot.ButtonPrimaryDown)
            {
                Chunky(ref s, A2Kit.Ember, A2Kit.EmberBevel, slot == EmberforgeArt.Slot.ButtonPrimaryDown);
            }
            else if (slot == EmberforgeArt.Slot.ButtonSecondary || slot == EmberforgeArt.Slot.ButtonSecondaryDown)
            {
                Chunky(ref s, A2Kit.Teal, A2Kit.TealBevel, slot == EmberforgeArt.Slot.ButtonSecondaryDown);
            }
            else if (slot == QuietButton || slot == QuietButtonDown)
            {
                Chunky(ref s, A2Kit.Ink, A2Kit.InkDeep, slot == QuietButtonDown);
            }
            else if (slot == EmberforgeArt.Slot.ButtonDisabled)
            {
                Chunky(ref s, DisabledFace, DisabledBevel, false);
            }
            else if (slot == EmberforgeArt.Slot.Gloss)
            {
                // The A2 look has no gloss. The sprite stays so a caller that still places it draws nothing.
                s.EdgeWeight = 0;
                Flat(ref s, Color.clear, Color.clear, s.Radius);
                s.EdgeWeight = 0;
            }
            else if (slot == EmberforgeArt.Slot.Ribbon)
            {
                Flat(ref s, A2Kit.Ink, A2Kit.InkDeep, 10);
            }
            else if (slot == EmberforgeArt.Slot.RibbonTail)
            {
                // No tails: the same box, nothing in it.
                s.EdgeWeight = 0;
                Flat(ref s, Color.clear, Color.clear, s.Radius);
                s.EdgeWeight = 0;
            }
            else if (slot == EmberforgeArt.Slot.Pill)
            {
                Flat(ref s, A2Kit.Ink, A2Kit.InkDeep, s.Radius);
            }
            else if (slot == EmberforgeArt.Slot.TabOn)
            {
                Flat(ref s, A2Kit.Teal, A2Kit.InkDeep, s.Radius);
                Bevel(ref s, A2Kit.TealBevel, 3, 0);
            }
            else if (slot == EmberforgeArt.Slot.TabOff)
            {
                Flat(ref s, A2Kit.CreamWell, A2Kit.CreamWellInset, s.Radius);
            }
            else if (slot == EmberforgeArt.Slot.BarTrough)
            {
                Flat(ref s, A2Kit.Ink, A2Kit.InkDeep, s.Radius);
                s.EdgeWeight = 2;
            }
            else if (slot == EmberforgeArt.Slot.BarFill)
            {
                Flat(ref s, A2Kit.StarOn, A2Kit.StarOn, s.Radius);
                s.EdgeWeight = 0;
            }
            else if (slot == EmberforgeArt.Slot.Disc)
            {
                Flat(ref s, A2Kit.Ink, A2Kit.InkDeep, s.Radius);
                Bevel(ref s, A2Kit.InkDeep, 5, 0);
            }
            else if (slot == EmberforgeArt.Slot.DiscDown)
            {
                Flat(ref s, A2Kit.InkDeep, A2Kit.InkDeep, s.Radius);
            }
            else if (slot == EmberforgeArt.Slot.Rule)
            {
                Flat(ref s, A2Kit.CreamWellInset, A2Kit.CreamWellInset, s.Radius);
                s.EdgeWeight = 0;
            }
            return s;
        }

        private static void Flat(ref ForgeSpec s, Color face, Color edge, int radius)
        {
            s.FaceTop = face;
            s.FaceMid = face;
            s.FaceLow = face;
            s.Edge = edge;
            s.Radius = radius;
        }

        private static void Bevel(ref ForgeSpec s, Color color, int lip, int cavity)
        {
            s.Lip = lip;
            s.LipColor = color;
            s.Cavity = cavity;
        }

        /// <summary>The chunky button: a flat face over a bevel, ink outline. Rest shows the full lip; the
        /// pressed sprite shows the shrunk lip and the cavity wall, exactly the geometry the kit press uses,
        /// so the 4 unit drop in EmberforgeButtonSkin lands the same.</summary>
        private static void Chunky(ref ForgeSpec s, Color face, Color bevel, bool down)
        {
            Flat(ref s, face, A2Kit.InkDeep, 16);
            Bevel(ref s, bevel, down ? Emberforge.ButtonLipPressed : Emberforge.ButtonLip,
                  down ? (int)Emberforge.ButtonPressDrop : 0);
        }

        // ------------------------------------------------------------------ text

        /// <summary>How much smaller text is set while the skin is on. Nunito Bold and Lilita One are
        /// wider than Barlow Condensed; the layouts were sized for the condensed face, so the size is
        /// what gives, never a box.</summary>
        public const float BodyScale = 0.88f;

        public static int Scaled(int size)
        {
            if (!On) return size;
            return Mathf.Max(8, Mathf.RoundToInt(size * BodyScale));
        }

        /// <summary>True for a figure: digits and the marks that travel with them. These are set in Lilita One.</summary>
        public static bool IsFigure(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > 14) return false;
            bool digit = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= '0' && c <= '9') { digit = true; continue; }
                if (c == ',' || c == '.' || c == '+' || c == '-' || c == '%' || c == '/' || c == 'x' || c == 'X' || c == ' ' || c == 'K' || c == 'M')
                    continue;
                return false;
            }
            return digit;
        }

        /// <summary>
        /// The nearest ancestor sprite from this class, by name, or null. A label calls this when it is
        /// built to learn what it sits on.
        /// </summary>
        public static string SurfaceOf(Transform node)
        {
            Transform t = node;
            while (t != null)
            {
                UnityEngine.UI.Image image = t.GetComponent<UnityEngine.UI.Image>();
                if (image != null && image.sprite != null && image.sprite.name != null
                    && image.sprite.name.StartsWith(Prefix))
                    return image.sprite.name;
                // A node a screen marked as dark backed owns its labels.
                if (t.parent != null && HasDarkBacking(t.parent)) return null;
                // A dark plate of another kit (a hexagon, a chip, an icon backing) owns its labels too.
                if (image != null && image.sprite != null && image.color.a > 0.5f && IsDark(image.color))
                    return null;
                // A control (button, disc, tab) whose plate is not set yet owns its labels: they never take a
                // cream ancestor's ink, whatever the panel behind the control is.
                if (t.GetComponent<UnityEngine.UI.Selectable>() != null) return null;
                t = t.parent;
            }
            return null;
        }

        /// <summary>
        /// True when a screen has marked this node as carrying a dark backing of its own (a painting under a scrim, a
        /// hexagon's fill) with A2DarkBacking. Labels on that node sit on the dark, not on the cream plate behind
        /// it, so they keep their light colours. A marker, not a guess: a guess misreads every ground and rim.
        /// </summary>
        public static bool HasDarkBacking(Transform node)
        {
            return node != null && node.GetComponent<A2DarkBacking>() != null;
        }

        public static bool IsCreamSurface(string spriteName)
        {
            if (spriteName == null) return false;
            return spriteName == Prefix + EmberforgeArt.Slot.Panel
                || spriteName == Prefix + EmberforgeArt.Slot.PanelPlain
                || spriteName == Prefix + EmberforgeArt.Slot.Sunken
                || spriteName == Prefix + EmberforgeArt.Slot.TabOff;
        }

        /// <summary>
        /// A light text token placed on a cream surface becomes its ink twin; every other colour is kept.
        /// TextBody (F2E7D8) and TextOnColor white go to Ink, TextGold (F6CE6C) to a deep ember brown,
        /// TextQuiet (A99380) to InkSoft.
        /// </summary>
        public static Color OnCream(Color color)
        {
            if (Near(color, Emberforge.TextBody) || Near(color, Color.white) || Near(color, A2Kit.TextOnDark)) return A2Kit.Ink;
            if (Near(color, Emberforge.TextGold)) return A2Kit.EmberBevel;
            if (Near(color, Emberforge.TextQuiet)) return A2Kit.InkSoft;
            return color;
        }

        private static bool Near(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.012f && Mathf.Abs(a.g - b.g) < 0.012f && Mathf.Abs(a.b - b.b) < 0.012f;
        }

        /// <summary>
        /// Restyles a label just built by EmberforgeUI.Label, Title or Body while the skin is on: the size
        /// (BodyScale), Lilita One for a figure, and the ink twin of a light token on a cream surface.
        /// </summary>
        public static void Style(UnityEngine.UI.Text label)
        {
            if (!On || label == null) return;
            label.fontSize = Scaled(label.fontSize);
            if (IsFigure(label.text)) label.font = A2Kit.Display;
            // Nunito runs wider than the condensed face it replaces. A label placed by hand in a fixed rect may shrink to fit
            // that rect, down to a floor; a label inside a layout group is sized by the group and is left alone.
            Transform home = label.transform.parent;
            if (home != null && home.GetComponent<UnityEngine.UI.LayoutGroup>() == null && !label.resizeTextForBestFit)
            {
                label.resizeTextForBestFit = true;
                label.resizeTextMaxSize = label.fontSize;
                label.resizeTextMinSize = Mathf.Max(8, Mathf.RoundToInt(label.fontSize * 0.7f));
            }
            if (IsCreamSurface(SurfaceOf(label.transform.parent))) Recolor(label);
        }

        /// <summary>
        /// What a label's colour becomes on a cream surface: a light token to its ink twin, and any other colour that is
        /// too pale to read on cream (a pale rarity, aspect or "good" green) deepened toward ink, same hue.
        /// </summary>
        public static Color CreamInk(Color color)
        {
            Color c = OnCream(color);
            if ((c.r * 0.3f + c.g * 0.59f + c.b * 0.11f) > 0.5f) c = Color.Lerp(c, A2Kit.Ink, 0.5f);
            return c;
        }

        /// <summary>
        /// A label on a cream surface: its colour mapped now, the stroke off (a dark stroke on cream is a smear), and an
        /// A2InkGuard so a screen that recolours it later (a value that turns good, warning or quiet) is mapped too.
        /// </summary>
        public static void Recolor(UnityEngine.UI.Text label)
        {
            A2InkGuard guard = label.GetComponent<A2InkGuard>();
            if (guard == null) guard = label.gameObject.AddComponent<A2InkGuard>();
            guard.Bind(label);
        }

        /// <summary>
        /// A plate that was given its cream sprite AFTER some of its labels were built (a screen adds the plate to a
        /// rect that already has children): the labels under it are restyled now. A child with a sprite of its own is a
        /// plate, chip or icon in its own right and is left alone (a cream one is handled by its own call), and so is a
        /// control that carries its own plate.
        /// </summary>
        public static void Retint(Transform plate)
        {
            if (!On || plate == null) return;
            bool backed = HasDarkBacking(plate);
            for (int i = 0; i < plate.childCount; i++)
            {
                Transform child = plate.GetChild(i);
                UnityEngine.UI.Image image = child.GetComponent<UnityEngine.UI.Image>();
                if (image != null && image.sprite != null) continue;
                if (child.GetComponent<UnityEngine.UI.Selectable>() != null) continue;
                UnityEngine.UI.Text text = child.GetComponent<UnityEngine.UI.Text>();
                if (text != null && !backed) Recolor(text);
                Retint(child);
            }
        }

        public static bool IsDark(Color c)
        {
            return (c.r * 0.3f + c.g * 0.59f + c.b * 0.11f) < 0.46f;
        }
    }
    /// <summary>
    /// Keeps a label on a cream plate legible. It maps the label's colour through A2Skin.CreamInk and turns its stroke off,
    /// and repeats that whenever a screen assigns a new colour (checked once a frame, one colour compare).
    /// </summary>
    public sealed class A2InkGuard : MonoBehaviour
    {
        private UnityEngine.UI.Text _text;
        private Color _last;
        private bool _bound;

        public void Bind(UnityEngine.UI.Text text)
        {
            _text = text;
            Apply();
        }

        private void Apply()
        {
            if (_text == null) return;
            Color mapped = A2Skin.CreamInk(_text.color);
            _text.color = mapped;
            _last = mapped;
            _bound = true;
            UnityEngine.UI.Outline edge = _text.GetComponent<UnityEngine.UI.Outline>();
            if (edge != null) edge.enabled = false;
        }

        private void LateUpdate()
        {
            if (!_bound || _text == null || !A2Skin.On) return;
            Color now = _text.color;
            if (now.r != _last.r || now.g != _last.g || now.b != _last.b || now.a != _last.a) Apply();
        }
    }

    /// <summary>Marks a node whose children sit on a dark backing of their own; see A2Skin.HasDarkBacking.</summary>
    public sealed class A2DarkBacking : MonoBehaviour { }
}
