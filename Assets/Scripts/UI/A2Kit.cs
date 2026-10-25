using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Session;

namespace Emberfall.UI
{
    /// <summary>The three voices of an A2 button. Primary is the ember orange and there is at most one per screen.</summary>
    public enum A2ButtonKind { Primary, Secondary, Quiet }

    /// <summary>
    /// THE A2 KIT (R55): the pieces the A2 screen boards share, built only as far as the Formation screen
    /// needs them. Tokens, the three fonts, the cream chunk panel, the cut corner frame, the A2 button and
    /// the Cinder Notch emblem. Nothing here is called by an existing screen, so building it changes no
    /// other screen; a later screen pass reuses it and may widen it.
    ///
    /// Everything is procedural (TextureLab) and code built, in keeping with the asset free UI. The only
    /// files it loads are the three fonts, and it says plainly when one is missing (see FontReport).
    /// </summary>
    public static class A2Kit
    {
        // ------------------------------------------------------------------ tokens

        public static readonly Color Cream = Hex(0xF6E7C8);
        public static readonly Color CreamWell = Hex(0xDCBE91);
        public static readonly Color CreamWellInset = Hex(0xB8976A);
        public static readonly Color Card = Hex(0xFFF4DC);
        public static readonly Color Ink = Hex(0x4B2A16);
        public static readonly Color InkDeep = Hex(0x2E170B);
        public static readonly Color InkSoft = Hex(0x7A5A3E);
        public static readonly Color Ember = Hex(0xE8802A);
        public static readonly Color EmberBevel = Hex(0xB5581A);
        public static readonly Color Teal = Hex(0x3FA7B8);
        public static readonly Color TealBevel = Hex(0x2E8A99);
        public static readonly Color StarOn = Hex(0xFFD66B);
        public static readonly Color StarOff = Hex(0x8A6A4A);
        // R57: the hero tier star. StarOn stays the warm accent the pills and cards use; the star itself is #F4A62A
        // with a dim rim for the empty ones (the language doc's "Stars #F4A62A").
        public static readonly Color StarGold = Hex(0xF4A62A);
        public static readonly Color StarDim = Hex(0x9A8266);
        public static readonly Color Good = Hex(0x5DD36A);
        public static readonly Color GoodStroke = Hex(0x1F5A2A);
        public static readonly Color TextOnDark = Hex(0xFFF4DC);

        public static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
        }

        public static Color WithAlpha(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }

        /// <summary>The aspect fill colours of the language doc. A2 only: UITheme.AspectColor is not touched.</summary>
        public static Color AspectFill(Aspect aspect)
        {
            switch (aspect)
            {
                case Aspect.Ember: return Hex(0xD6452B);
                case Aspect.Tide: return Hex(0x3C8FD6);
                case Aspect.Verdant: return Hex(0x4FA35A);
                case Aspect.Gale: return Hex(0x8FD3C9);
                case Aspect.Umbra: return Hex(0x8A5CC2);
                default: return Hex(0xE5C14A);
            }
        }

        // ------------------------------------------------------------------ fonts

        public const string DisplayFontPath = "Fonts/LilitaOne-Regular";
        public const string BodyFontPath = "Fonts/Nunito-Regular";
        public const string BodyBoldFontPath = "Fonts/Nunito-Bold";

        private static Font _display;
        private static Font _body;
        private static Font _bodyBold;
        private static bool _displayFell;
        private static bool _bodyFell;
        private static bool _bodyBoldFell;

        /// <summary>Lilita One. Titles, buttons, numbers: the chunky voice.</summary>
        public static Font Display
        {
            get
            {
                if (_display == null) _display = Load(DisplayFontPath, out _displayFell);
                return _display;
            }
        }

        /// <summary>Nunito Regular. Captions and hints.</summary>
        public static Font Body
        {
            get
            {
                if (_body == null) _body = Load(BodyFontPath, out _bodyFell);
                return _body;
            }
        }

        /// <summary>Nunito Bold. Names, chips, stat lines.</summary>
        public static Font BodyBold
        {
            get
            {
                if (_bodyBold == null) _bodyBold = Load(BodyBoldFontPath, out _bodyBoldFell);
                return _bodyBold;
            }
        }

        /// <summary>
        /// The font at a Resources path, or the shipped Barlow face if it is missing. A fall back is not
        /// silent: it sets the flag FontReport reads and writes one warning, so a capture or a play session
        /// can never quietly show Barlow where Lilita One was asked for.
        /// </summary>
        private static Font Load(string path, out bool fellBack)
        {
            Font font = Resources.Load<Font>(path);
            fellBack = false;
            if (font == null)
            {
                fellBack = true;
                font = Emberforge.BarlowFont;
                Debug.LogWarning("A2Kit: font '" + path + "' did not load, using the Barlow fallback.");
            }
            return font;
        }

        /// <summary>
        /// "A2 fonts: Display=LilitaOne-Regular, Body=Nunito-Regular, BodyBold=Nunito-Bold" when all three
        /// loaded, otherwise names which one fell back. Reads the real loaded font objects, so it proves what
        /// the Text components use, not what was requested.
        /// </summary>
        public static string FontReport()
        {
            Font d = Display, b = Body, bb = BodyBold;
            return "A2 fonts: Display=" + FontName(d, _displayFell) + ", Body=" + FontName(b, _bodyFell)
                   + ", BodyBold=" + FontName(bb, _bodyBoldFell);
        }

        private static string FontName(Font f, bool fell)
        {
            string name = f == null ? "none" : f.name;
            return fell ? "FALLBACK(" + name + ")" : name;
        }

        /// <summary>True only when all three A2 fonts loaded from Resources/Fonts, none from the fallback.</summary>
        public static bool FontsLoaded
        {
            get
            {
                Font touch = Display;
                touch = Body;
                touch = BodyBold;
                return touch != null && !_displayFell && !_bodyFell && !_bodyBoldFell;
            }
        }

        /// <summary>Drops cached handles (an editor that imports a font while running).</summary>
        public static void ForgetFonts()
        {
            _display = null;
            _body = null;
            _bodyBold = null;
        }

        // ------------------------------------------------------------------ text

        public static Text Label(string name, Transform parent, string text, int size, Color color,
                                 TextAnchor anchor, Font font)
        {
            Text label = UIBuilder.Label(name, parent, text, size, color, anchor);
            label.font = font;
            return label;
        }

        /// <summary>A Lilita One label with a dark drop edge, for text that sits on a busy or mid tone ground.</summary>
        public static Text Chunky(string name, Transform parent, string text, int size, Color color, TextAnchor anchor)
        {
            Text label = Label(name, parent, text, size, color, anchor, Display);
            UIBuilder.Shadowed(label, WithAlpha(InkDeep, 0.8f), 3f);
            return label;
        }

        // ------------------------------------------------------------------ surfaces

        /// <summary>
        /// The cream chunk panel: a 4 unit bottom bevel, then the cream face with a 3 unit dark border.
        /// Returns the face (children go on it).
        /// </summary>
        public static Image ChunkPanel(string name, Transform parent, int radius)
        {
            RectTransform root = UIBuilder.Node(name, parent);
            Image bevel = UIBuilder.Panel("Bevel", root, InkDeep, radius);
            bevel.raycastTarget = false;
            UIBuilder.FillParent(bevel.rectTransform, 0f);

            Image face = UIBuilder.Panel("Face", root, Cream, radius);
            face.raycastTarget = false;
            UIBuilder.FillParent(face.rectTransform, 0f, 0f, 0f, 4f);
            UIBuilder.Frame("Border", face.rectTransform, Ink, radius, 3);
            return face;
        }

        /// <summary>An inset well: the darker cream the squad slots and the roster sit in.</summary>
        public static Image Well(string name, Transform parent, int radius)
        {
            Image well = UIBuilder.Panel(name, parent, CreamWell, radius);
            well.raycastTarget = false;
            UIBuilder.Frame("Inset", well.rectTransform, CreamWellInset, radius, 3);
            return well;
        }

        /// <summary>A rounded square with the top left corner cut, filled.</summary>
        public static Image CutPanel(string name, Transform parent, Color color, int radius, int cut)
        {
            RectTransform rt = UIBuilder.Node(name, parent);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = TextureLab.CutCornerRect(radius, cut);
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>The cut corner outline, filling its parent.</summary>
        public static Image CutFrame(string name, Transform parent, Color color, int radius, int cut, int thickness)
        {
            RectTransform rt = UIBuilder.Node(name, parent);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = TextureLab.CutCornerOutline(radius, cut, thickness);
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            UIBuilder.FillParent(rt, 0f);
            return image;
        }

        /// <summary>The Cinder Notch emblem for an aspect: the aspect colour fill with a dark one unit stroke.</summary>
        public static RectTransform Emblem(string name, Transform parent, Aspect aspect, float size)
        {
            RectTransform root = UIBuilder.Node(name, parent);
            UIBuilder.Size(root, size, size);
            int px = Mathf.Max(24, Mathf.RoundToInt(size * 2f));
            Image fill = UIBuilder.Img("Fill", root, TextureLab.CinderNotch((int)aspect, px, false), AspectFill(aspect));
            UIBuilder.FillParent(fill.rectTransform, 0f);
            Image stroke = UIBuilder.Img("Stroke", root, TextureLab.CinderNotch((int)aspect, px, true), InkDeep);
            UIBuilder.FillParent(stroke.rectTransform, 0f);
            return root;
        }

        // ------------------------------------------------------------------ buttons

        /// <summary>
        /// An A2 button: a solid face over a darker bevel, the label in Lilita One, a press that sinks the
        /// face onto its bevel. Returns the UIButton handle every screen already uses (OnClick, SetText,
        /// Interactable). Kind decides the colours and is read back by the button hierarchy check.
        /// </summary>
        public static UIButton Button(string name, Transform parent, string text, int fontSize, A2ButtonKind kind)
        {
            Color face, bevel;
            switch (kind)
            {
                case A2ButtonKind.Primary: face = Ember; bevel = EmberBevel; break;
                case A2ButtonKind.Secondary: face = Teal; bevel = TealBevel; break;
                default: face = Ink; bevel = InkDeep; break;
            }

            UIButton handle = new UIButton();
            handle.Root = UIBuilder.Node(name, parent);
            handle.Group = handle.Root.gameObject.AddComponent<CanvasGroup>();

            handle.Frame = UIBuilder.Panel("Bevel", handle.Root, bevel, 18);
            handle.Frame.raycastTarget = false;
            UIBuilder.FillParent(handle.Frame.rectTransform, 0f);

            handle.Background = UIBuilder.Panel("Face", handle.Root, face, 18);
            UIBuilder.FillParent(handle.Background.rectTransform, 0f, 0f, 0f, 6f);
            UIBuilder.Frame("Edge", handle.Background.rectTransform, kind == A2ButtonKind.Quiet ? InkDeep : WithAlpha(InkDeep, 0.85f), 18, 3);

            handle.Button = handle.Root.gameObject.AddComponent<Button>();
            handle.Button.targetGraphic = handle.Background;
            handle.Button.transition = Selectable.Transition.None;

            handle.Label = Label("Text", handle.Background.rectTransform, text, fontSize, TextOnDark, TextAnchor.MiddleCenter, Display);
            UIBuilder.FillParent(handle.Label.rectTransform, 10f, 0f, 10f, 0f);
            UIBuilder.Shadowed(handle.Label, WithAlpha(InkDeep, 0.7f), 2f);

            A2ButtonSkin skin = handle.Root.gameObject.AddComponent<A2ButtonSkin>();
            skin.Bind(handle.Background, kind);
            handle.InteractableChanged = delegate (bool on) { skin.SetInteractable(on); };
            return handle;
        }

        /// <summary>
        /// R58: re-dresses a button built with <see cref="Button"/> as another kind, for a control whose role changes
        /// with the state of the screen (LEVEL UP is the Primary until ASCEND is ready, then it steps down). The
        /// skin is rebound so the hierarchy check reads the new kind.
        /// </summary>
        public static void SetKind(UIButton button, A2ButtonKind kind)
        {
            if (button == null || button.Root == null) return;
            A2ButtonSkin skin = button.Root.GetComponent<A2ButtonSkin>();
            if (skin == null || skin.Kind == kind) return;
            Color bevel;
            switch (kind)
            {
                case A2ButtonKind.Primary: bevel = EmberBevel; break;
                case A2ButtonKind.Secondary: bevel = TealBevel; break;
                default: bevel = InkDeep; break;
            }
            if (button.Frame != null) button.Frame.color = bevel;
            Transform edge = button.Background != null ? button.Background.rectTransform.Find("Edge") : null;
            if (edge != null)
            {
                Image edgeImage = edge.GetComponent<Image>();
                if (edgeImage != null) edgeImage.color = kind == A2ButtonKind.Quiet ? InkDeep : WithAlpha(InkDeep, 0.85f);
            }
            skin.Bind(button.Background, kind);
        }

        /// <summary>
        /// R58: how far a disabled face greys toward neutral (0 keeps the kind's colour, 1 is grey). The default is
        /// 0.65; a Primary that is disabled only for want of gold stays orange with a lower mix.
        /// </summary>
        public static void SetDisabledMix(UIButton button, float mix)
        {
            if (button == null || button.Root == null) return;
            A2ButtonSkin skin = button.Root.GetComponent<A2ButtonSkin>();
            if (skin != null) skin.SetDisabledMix(mix);
        }

        // ------------------------------------------------------------------ backdrop

        /// <summary>
        /// A row of five hero tier stars (R57): gold #F4A62A with a thin dark outline when earned, a dim outline
        /// when not. One widget for every A2 screen; the shape is TextureLab.Star and StarShape.
        /// </summary>
        public static RectTransform Stars(string name, Transform parent, int stars, float size, float gap)
        {
            RectTransform root = UIBuilder.Node(name, parent);
            UIBuilder.HStack(root, gap, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
            int tex = TextureLab.StarTexSize(size);
            Sprite on = TextureLab.Star(tex);
            Sprite off = TextureLab.StarEmpty(tex);
            for (int i = 0; i < 5; i++)
            {
                bool earned = i < stars;
                Image image = UIBuilder.Img("Star" + i, root, earned ? on : off, earned ? StarGold : StarDim);
                UIBuilder.Sized(image.rectTransform, size, size);
                image.raycastTarget = false;
            }
            return root;
        }

        /// <summary>
        /// The screen ground: the hub painting dimmed behind a flat warm overlay and a vignette, so the
        /// cream panels carry the screen. The blur of the board is dropped (it needs a render texture pass
        /// the offline shim cannot compile); the dim overlay keeps the same hierarchy of light.
        /// Falls back to a flat brown when the painting is missing.
        /// </summary>
        public static RectTransform Backdrop(Transform parent)
        {
            RectTransform root = UIBuilder.Node("Backdrop", parent);
            UIBuilder.FillParent(root, 0f);

            Image floor = UIBuilder.Panel("Floor", root, Hex(0x2A150A), 0);
            floor.raycastTarget = false;
            UIBuilder.FillParent(floor.rectTransform, 0f);

            Texture2D painting = Resources.Load<Texture2D>("Backgrounds/hub");
            if (painting != null)
            {
                Sprite sprite = Sprite.Create(painting, new Rect(0f, 0f, painting.width, painting.height),
                                              new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
                RectTransform art = UIBuilder.Node("Art", root);
                UIBuilder.FillParent(art, 0f);
                Image image = UIBuilder.Img("Painting", art, sprite, Color.white);
                UIBuilder.FillParent(image.rectTransform, 0f);
                AspectRatioFitter fitter = art.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            }

            Image dim = UIBuilder.Panel("Dim", root, new Color(46f / 255f, 23f / 255f, 11f / 255f, 0.56f), 0);
            dim.raycastTarget = false;
            UIBuilder.FillParent(dim.rectTransform, 0f);

            Image vignette = UIBuilder.Img("Vignette", root, TextureLab.Spotlight(128), new Color(0.05f, 0.02f, 0.01f, 0.55f));
            UIBuilder.FillParent(vignette.rectTransform, -200f);
            return root;
        }
    }

    /// <summary>
    /// The press and disabled behaviour of an A2 button, and the marker the button hierarchy check reads
    /// (Kind). Pressing sinks the face onto its bevel; a disabled button greys its face.
    /// </summary>
    public sealed class A2ButtonSkin : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private Image _face;
        private A2ButtonKind _kind;
        private bool _interactable = true;
        private bool _down;
        private float _disabledMix = DefaultDisabledMix;

        /// <summary>The share of grey a disabled face is mixed with.</summary>
        public const float DefaultDisabledMix = 0.65f;

        public void SetDisabledMix(float mix)
        {
            _disabledMix = Mathf.Clamp01(mix);
            Apply();
        }

        public void Bind(Image face, A2ButtonKind kind)
        {
            _face = face;
            _kind = kind;
            Apply();
        }

        public A2ButtonKind Kind { get { return _kind; } }

        public bool IsInteractable { get { return _interactable; } }

        public void SetInteractable(bool value)
        {
            _interactable = value;
            if (!value) _down = false;
            Apply();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_interactable) return;
            _down = true;
            Apply();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _down = false;
            Apply();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _down = false;
            Apply();
        }

        private void Apply()
        {
            if (_face == null) return;
            RectTransform rt = _face.rectTransform;
            // Rest: 6 units of bevel show below the face. Down: the face sinks to 1.
            rt.offsetMin = new Vector2(0f, _down ? 1f : 6f);
            rt.offsetMax = new Vector2(0f, _down ? -5f : 0f);
            Color baseColor;
            switch (_kind)
            {
                case A2ButtonKind.Primary: baseColor = A2Kit.Ember; break;
                case A2ButtonKind.Secondary: baseColor = A2Kit.Teal; break;
                default: baseColor = A2Kit.Ink; break;
            }
            _face.color = _interactable ? baseColor : Color.Lerp(baseColor, new Color(0.55f, 0.52f, 0.5f, 1f), _disabledMix);
        }
    }
}
