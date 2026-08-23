using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Emberfall.UI
{
    /// <summary>Handle returned by the button builders so callers can restyle or relabel later.</summary>
    public sealed class UIButton
    {
        public RectTransform Root;
        public Button Button;
        public Image Background;
        public Image Frame;
        public Text Label;
        public Text SubLabel;
        public CanvasGroup Group;

        public bool Interactable
        {
            get { return Button != null && Button.interactable; }
            set
            {
                if (Button != null) Button.interactable = value;
                if (Group != null) Group.alpha = value ? 1f : 0.45f;
            }
        }

        public void SetText(string text)
        {
            if (Label != null) Label.text = text;
        }

        public void OnClick(Action action)
        {
            if (Button == null || action == null) return;
            Button.onClick.AddListener(delegate { action(); });
        }
    }

    /// <summary>
    /// Imperative UI construction helpers. Screens read as a description of their layout
    /// rather than a pile of RectTransform arithmetic.
    /// </summary>
    public static class UIBuilder
    {
        public static readonly Vector2 Stretch = new Vector2(0.5f, 0.5f);

        // ------------------------------------------------------------------ primitives

        public static RectTransform Node(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(100f, 100f);
            return rt;
        }

        public static Image Panel(string name, Transform parent, Color color, int radius)
        {
            RectTransform rt = Node(name, parent);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = radius > 0 ? UISkin.Panel(radius) : TextureLab.White;
            image.type = radius > 0 ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            return image;
        }

        public static Image Frame(string name, Transform parent, Color color, int radius, int thickness)
        {
            RectTransform rt = Node(name, parent);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = UISkin.PanelFrame(radius, thickness);
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            FillParent(rt, 0f);
            return image;
        }

        public static Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            RectTransform rt = Node(name, parent);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor anchor)
        {
            RectTransform rt = Node(name, parent);
            Text label = rt.gameObject.AddComponent<Text>();
            label.font = UITheme.Font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.supportRichText = true;
            return label;
        }

        public static Text WrappedLabel(string name, Transform parent, string text, int size, Color color, TextAnchor anchor)
        {
            Text label = Label(name, parent, text, size, color, anchor);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.lineSpacing = 1.05f;
            return label;
        }

        public static Text Shadowed(Text label, Color color, float distance)
        {
            Shadow shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = new Vector2(distance, -distance);
            return label;
        }

        public static Text Outlined(Text label, Color color, float distance)
        {
            Outline outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, distance);
            return label;
        }

        // ------------------------------------------------------------------ layout

        public static RectTransform FillParent(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static RectTransform FillParent(RectTransform rt, float left, float top, float right, float bottom)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Pins to the top edge of the parent, full width, with the given height.</summary>
        public static RectTransform TopStrip(RectTransform rt, float height, float top, float sideInset)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(sideInset, 0f);
            rt.offsetMax = new Vector2(-sideInset, 0f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
            rt.anchoredPosition = new Vector2(0f, -top);
            return rt;
        }

        public static RectTransform BottomStrip(RectTransform rt, float height, float bottom, float sideInset)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(sideInset, 0f);
            rt.offsetMax = new Vector2(-sideInset, 0f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
            rt.anchoredPosition = new Vector2(0f, bottom);
            return rt;
        }

        public static RectTransform Box(RectTransform rt, float x, float y, float w, float h, Vector2 anchor)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
            return rt;
        }

        public static RectTransform Size(RectTransform rt, float w, float h)
        {
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static VerticalLayoutGroup VStack(RectTransform rt, float spacing, RectOffset padding, TextAnchor align)
        {
            VerticalLayoutGroup layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding != null ? padding : new RectOffset(0, 0, 0, 0);
            layout.childAlignment = align;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            return layout;
        }

        public static HorizontalLayoutGroup HStack(RectTransform rt, float spacing, RectOffset padding, TextAnchor align)
        {
            HorizontalLayoutGroup layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding != null ? padding : new RectOffset(0, 0, 0, 0);
            layout.childAlignment = align;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            return layout;
        }

        public static LayoutElement Sized(RectTransform rt, float preferredWidth, float preferredHeight)
        {
            LayoutElement element = rt.gameObject.AddComponent<LayoutElement>();
            if (preferredWidth > 0f) element.preferredWidth = preferredWidth;
            if (preferredHeight > 0f) element.preferredHeight = preferredHeight;
            return element;
        }

        public static LayoutElement Flexible(RectTransform rt, float width, float height)
        {
            LayoutElement element = rt.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = width;
            element.flexibleHeight = height;
            return element;
        }

        /// <summary>Vertical scrolling list. Returns the content transform to parent items to.</summary>
        public static RectTransform ScrollList(string name, Transform parent, out ScrollRect scrollRect, float spacing, RectOffset padding)
        {
            RectTransform content = ScrollShell(name, parent, out scrollRect);
            VStack(content, spacing, padding, TextAnchor.UpperCenter);
            AddHeightFitter(content);
            return content;
        }

        /// <summary>Vertical scrolling grid. Returns the content transform to parent cells to.</summary>
        public static RectTransform ScrollGrid(string name, Transform parent, out ScrollRect scrollRect,
                                               Vector2 cellSize, Vector2 spacing, int columns, RectOffset padding)
        {
            RectTransform content = ScrollShell(name, parent, out scrollRect);

            GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cellSize;
            grid.spacing = spacing;
            grid.padding = padding != null ? padding : new RectOffset(0, 0, 0, 0);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.childAlignment = TextAnchor.UpperCenter;

            AddHeightFitter(content);
            return content;
        }

        private static void AddHeightFitter(RectTransform content)
        {
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        /// <summary>Scroll rect, viewport and mask. The caller decides how content is laid out.</summary>
        private static RectTransform ScrollShell(string name, Transform parent, out ScrollRect scrollRect)
        {
            RectTransform root = Node(name, parent);
            scrollRect = root.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.08f;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.12f;
            scrollRect.scrollSensitivity = 42f;

            RectTransform viewport = Node("Viewport", root);
            FillParent(viewport, 0f);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.0035f); // needs a graphic to receive drags
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = Node("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);

            scrollRect.viewport = viewport;
            scrollRect.content = content;
            return content;
        }

        // ------------------------------------------------------------------ buttons

        public static UIButton PrimaryButton(string name, Transform parent, string text, int fontSize)
        {
            return MakeButton(name, parent, text, fontSize, UITheme.Ember, UITheme.EmberDeep, UITheme.TextPrimary);
        }

        public static UIButton GoldButton(string name, Transform parent, string text, int fontSize)
        {
            return MakeButton(name, parent, text, fontSize, UITheme.Gold, UITheme.GoldDeep, UITheme.TextOnAccent);
        }

        public static UIButton SecondaryButton(string name, Transform parent, string text, int fontSize)
        {
            return MakeButton(name, parent, text, fontSize, UITheme.PanelRaised, UITheme.Border, UITheme.TextPrimary);
        }

        public static UIButton MakeButton(string name, Transform parent, string text, int fontSize,
                                          Color fill, Color edge, Color textColor)
        {
            UIButton handle = new UIButton();
            handle.Root = Node(name, parent);
            handle.Group = handle.Root.gameObject.AddComponent<CanvasGroup>();

            handle.Background = handle.Root.gameObject.AddComponent<Image>();
            handle.Background.sprite = UISkin.Button((int)UITheme.CornerRadius);
            handle.Background.type = Image.Type.Sliced;
            handle.Background.color = fill;

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

            handle.Frame = Frame("Edge", handle.Root, edge, (int)UITheme.CornerRadius, 3);

            RectTransform sheen = Node("Sheen", handle.Root);
            sheen.anchorMin = new Vector2(0f, 0.55f);
            sheen.anchorMax = new Vector2(1f, 1f);
            sheen.offsetMin = new Vector2(4f, 0f);
            sheen.offsetMax = new Vector2(-4f, -4f);
            Image sheenImage = sheen.gameObject.AddComponent<Image>();
            sheenImage.sprite = TextureLab.RoundedRect((int)UITheme.CornerRadius);
            sheenImage.type = Image.Type.Sliced;
            sheenImage.color = UITheme.ButtonSheen;
            sheenImage.raycastTarget = false;

            handle.Label = Label("Text", handle.Root, text, fontSize, textColor, TextAnchor.MiddleCenter);
            FillParent(handle.Label.rectTransform, 12f);
            Shadowed(handle.Label, UITheme.InkPanel, 2f);

            handle.Root.gameObject.AddComponent<ButtonPress>();
            return handle;
        }

        /// <summary>Compact circular icon button used for back arrows and toggles.</summary>
        public static UIButton IconButton(string name, Transform parent, string glyph, float diameter, Color fill, Color textColor)
        {
            UIButton handle = new UIButton();
            handle.Root = Node(name, parent);
            Size(handle.Root, diameter, diameter);
            handle.Group = handle.Root.gameObject.AddComponent<CanvasGroup>();

            handle.Background = handle.Root.gameObject.AddComponent<Image>();
            handle.Background.sprite = UISkin.Disc(96);
            handle.Background.color = fill;

            handle.Button = handle.Root.gameObject.AddComponent<Button>();
            handle.Button.targetGraphic = handle.Background;

            handle.Label = Label("Glyph", handle.Root, glyph, Mathf.RoundToInt(diameter * 0.46f), textColor, TextAnchor.MiddleCenter);
            FillParent(handle.Label.rectTransform, 0f);
            handle.Root.gameObject.AddComponent<ButtonPress>();
            return handle;
        }

        // ------------------------------------------------------------------ bars

        /// <summary>
        /// Track plus fill. Returns the fill image; drive it with SetBar().
        /// Plain rectangles on purpose - a nine-sliced sprite cannot be used with a Filled
        /// image type, and bars are often thinner than a rounded sprite's border.
        /// </summary>
        public static Image Bar(string name, Transform parent, Color track, Color fill)
        {
            RectTransform root = Node(name, parent);
            Image trackImage = root.gameObject.AddComponent<Image>();
            trackImage.sprite = TextureLab.White;
            trackImage.color = track;
            trackImage.raycastTarget = false;

            RectTransform fillRect = Node("Fill", root);
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            fillRect.pivot = new Vector2(0f, 0.5f);

            Image fillImage = fillRect.gameObject.AddComponent<Image>();
            fillImage.sprite = TextureLab.White;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 1f;
            fillImage.color = fill;
            fillImage.raycastTarget = false;
            return fillImage;
        }

        public static void SetBar(Image fill, float ratio)
        {
            if (fill == null) return;
            fill.fillAmount = Mathf.Clamp01(ratio);
        }

        // ------------------------------------------------------------------ misc

        public static Image Divider(string name, Transform parent, Color color)
        {
            Image image = Img(name, parent, TextureLab.White, color);
            Sized(image.rectTransform, 0f, 2f);
            return image;
        }

        public static RectTransform Spacer(string name, Transform parent, float height)
        {
            RectTransform rt = Node(name, parent);
            Sized(rt, 0f, height);
            return rt;
        }

        /// <summary>Small rounded tag - aspect, role, rarity.</summary>
        public static RectTransform Chip(string name, Transform parent, string text, Color tint, int fontSize)
        {
            RectTransform root = Node(name, parent);
            Image background = root.gameObject.AddComponent<Image>();
            background.sprite = TextureLab.RoundedRect(12);
            background.type = Image.Type.Sliced;
            background.color = UITheme.WithAlpha(tint, 0.22f);
            background.raycastTarget = false;

            Frame("Edge", root, UITheme.WithAlpha(tint, 0.75f), 12, 2);

            Text label = Label("Text", root, text, fontSize, tint, TextAnchor.MiddleCenter);
            FillParent(label.rectTransform, 10f, 2f, 10f, 2f);
            return root;
        }
    }

    /// <summary>Squash-and-release feedback so taps feel physical instead of instant.</summary>
    public sealed class ButtonPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private Vector3 _baseScale = Vector3.one;
        private bool _captured;

        private void Awake()
        {
            _baseScale = transform.localScale;
            _captured = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_captured) { _baseScale = transform.localScale; _captured = true; }
            transform.localScale = _baseScale * 0.955f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            transform.localScale = _baseScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.localScale = _baseScale;
        }
    }
}
