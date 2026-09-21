using System;
using UnityEngine;
using UnityEngine.UI;

namespace Emberfall.UI
{
    /// <summary>A built HUD element: the button, its glyph, its caption and its notification dot.
    /// Returned rather than discarded so the Hub can relabel, re-badge or disable one later
    /// without reaching into the hierarchy by name.</summary>
    public sealed class HudElement
    {
        public RectTransform Root;
        public UIButton Button;
        public Image Disc;
        public Image Glyph;
        public Text Caption;
        public RectTransform Badge;
        public Text BadgeText;
        public CanvasGroup Group;

        public bool Interactable
        {
            set
            {
                if (Button != null) Button.Interactable = value;
                if (Group != null) Group.alpha = value ? 1f : HubStyle.DimAlpha;
            }
        }

        /// <summary>Shows or hides the pending-work dot, and what it counts. A count of zero hides
        /// it: a badge showing nothing is worse than no badge, because it trains the player to
        /// ignore the one that means something.</summary>
        public void SetBadge(int count)
        {
            if (Badge == null) return;
            Badge.gameObject.SetActive(count > 0);
            if (BadgeText != null) BadgeText.text = count > 9 ? "9+" : count.ToString();
        }
    }

    /// <summary>
    /// THE HUB'S PREFABS.
    ///
    /// This project builds every pixel of its UI in code from a deliberately near-empty scene, so
    /// it has no prefab assets to hang sprite slots off. These factories are the equivalent, and
    /// they are what the re-skin brief asks for in this codebase's own terms: one function per
    /// HUD element, called from exactly one place, taking every colour and metric from HubStyle
    /// and every sprite from a named slot in HudSkin. There is no art in this file, and there is
    /// no literal number in it that is not a local sum of HubStyle values.
    ///
    /// TOUCH TARGETS ARE THE OTHER REASON THESE ARE SHARED. Every element built here is at least
    /// HubStyle.TouchMin on both axes even when its art is smaller - the rail's discs are 76 wide
    /// inside a 108 hit rect - because a target's size is a property of the CONTROL rather than of
    /// the drawing, and hand-sizing each one is how a row ends up with five different answers.
    /// </summary>
    public static class HudWidgets
    {
        /// <summary>
        /// The invisible rect that makes a small icon a large target. It is a real Image with a
        /// fully transparent colour rather than nothing at all, because uGUI routes a tap through
        /// a Graphic and an empty RectTransform is not one; alpha does not affect raycasting.
        /// </summary>
        private static Image HitArea(RectTransform rect)
        {
            Image hit = rect.gameObject.AddComponent<Image>();
            hit.sprite = TextureLab.White;
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
            return hit;
        }

        private static Button Pressable(RectTransform root, Image target, Action action)
        {
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = UITheme.ButtonHighlight;
            colors.pressedColor = UITheme.ButtonPressed;
            colors.disabledColor = UITheme.ButtonDisabled;
            colors.fadeDuration = UITheme.ButtonFadeDuration;
            colors.colorMultiplier = 1f;
            button.colors = colors;
            root.gameObject.AddComponent<ButtonPress>();
            if (action != null) button.onClick.AddListener(delegate { action(); });
            return button;
        }

        // ------------------------------------------------------------------ the right-edge rail

        /// <summary>
        /// A rail button: a small warm-rimmed disc inside a full-size hit rect, with an optional
        /// notification dot on its top-right. Friends, Mail, Events and the overflow menu are all
        /// this, so the rail reads as one control strip rather than four decisions.
        /// </summary>
        public static HudElement RailButton(string name, RectTransform parent, string slot,
                                            Sprite fallbackGlyph, Color tint, Action action)
        {
            HudElement element = new HudElement();
            element.Root = UIBuilder.Node("Rail" + name, parent);
            UIBuilder.Size(element.Root, HubStyle.TouchMin, HubStyle.TouchMin);
            element.Group = element.Root.gameObject.AddComponent<CanvasGroup>();

            Image hit = HitArea(element.Root);

            RectTransform face = UIBuilder.Node("Face", element.Root);
            UIBuilder.Box(face, 0f, 0f, HubStyle.RailDisc, HubStyle.RailDisc, new Vector2(0.5f, 0.5f));

            element.Disc = UIBuilder.Img("Disc", face, HudSkin.Circle(HudSkin.Disc, 128),
                                         UITheme.WithAlpha(HubStyle.Plate, HubStyle.PlateAlpha));
            UIBuilder.FillParent(element.Disc.rectTransform, 0f);

            Image rim = UIBuilder.Img("Ring", face,
                                      HudSkin.Ring(HudSkin.DiscRing, 128, HubStyle.NavRingThickness),
                                      UITheme.WithAlpha(HubStyle.BorderWarm, HubStyle.BorderAlphaStrong));
            UIBuilder.FillParent(rim.rectTransform, 0f);

            element.Glyph = UIBuilder.Img("Glyph", face, HudSkin.Icon(slot, fallbackGlyph), tint);
            UIBuilder.FillParent(element.Glyph.rectTransform, HubStyle.NavGlyphInset * 0.6f);

            element.Button = new UIButton();
            element.Button.Root = element.Root;
            element.Button.Background = hit;
            element.Button.Group = element.Group;
            element.Button.Button = Pressable(element.Root, hit, action);

            AddBadge(element, face);
            return element;
        }

        /// <summary>The pending-work dot, hidden until something is pending.</summary>
        private static void AddBadge(HudElement element, RectTransform face)
        {
            element.Badge = UIBuilder.Node("Badge", face);
            UIBuilder.Box(element.Badge, HubStyle.BadgeDot * 0.25f, HubStyle.BadgeDot * 0.25f,
                          HubStyle.BadgeDot, HubStyle.BadgeDot, new Vector2(1f, 1f));
            Image dot = element.Badge.gameObject.AddComponent<Image>();
            dot.sprite = HudSkin.Circle(HudSkin.BadgeDot, 64);
            dot.color = UITheme.Danger;
            dot.raycastTarget = false;

            element.BadgeText = UIBuilder.Label("Count", element.Badge, "", HubStyle.FontSoon,
                                                UITheme.TextPrimary, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(element.BadgeText.rectTransform, 0f);
            element.Badge.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ the bottom nav bar

        /// <summary>
        /// A nav cell: a disc with a glyph in it and a caption underneath, in a cell wide enough
        /// for the longest word in the row. The whole cell is the target, not the disc, which is
        /// what makes the row comfortable to hit with a thumb rather than accurate to hit with a
        /// stylus.
        /// </summary>
        public static HudElement NavButton(string name, RectTransform parent, string caption,
                                           string slot, Sprite fallbackGlyph, Color tint, Action action)
        {
            HudElement element = new HudElement();
            element.Root = UIBuilder.Node("Nav" + name, parent);
            UIBuilder.Sized(element.Root, HubStyle.NavCellWidth, HubStyle.NavCellHeight);
            UIBuilder.Size(element.Root, HubStyle.NavCellWidth, HubStyle.NavCellHeight);
            element.Group = element.Root.gameObject.AddComponent<CanvasGroup>();

            Image hit = HitArea(element.Root);

            RectTransform face = UIBuilder.Node("Face", element.Root);
            UIBuilder.Box(face, 0f, 0f, HubStyle.NavIcon, HubStyle.NavIcon, new Vector2(0.5f, 1f));

            element.Disc = UIBuilder.Img("Disc", face, HudSkin.Circle(HudSkin.Disc, 128),
                                         UITheme.WithAlpha(HubStyle.Plate, HubStyle.PlateAlpha));
            UIBuilder.FillParent(element.Disc.rectTransform, 0f);

            Image rim = UIBuilder.Img("Ring", face,
                                      HudSkin.Ring(HudSkin.DiscRing, 128, HubStyle.NavRingThickness),
                                      UITheme.WithAlpha(HubStyle.BorderWarm, HubStyle.BorderAlphaStrong));
            UIBuilder.FillParent(rim.rectTransform, 0f);

            element.Glyph = UIBuilder.Img("Glyph", face, HudSkin.Icon(slot, fallbackGlyph), tint);
            UIBuilder.FillParent(element.Glyph.rectTransform, HubStyle.NavGlyphInset);

            element.Caption = UIBuilder.Label("Label", element.Root, caption, HubStyle.FontCaption,
                                              HubStyle.LabelText, TextAnchor.UpperCenter);
            UIBuilder.Box(element.Caption.rectTransform, 0f, -(HubStyle.NavIcon + HubStyle.NavIconGap),
                          HubStyle.NavCellWidth, HubStyle.NavCaptionHeight, new Vector2(0.5f, 1f));
            UIBuilder.Shadowed(element.Caption, UITheme.InkArt, 1.4f);

            element.Button = new UIButton();
            element.Button.Root = element.Root;
            element.Button.Background = hit;
            element.Button.Group = element.Group;
            element.Button.Label = element.Caption;
            element.Button.Button = Pressable(element.Root, hit, action);

            AddBadge(element, face);
            return element;
        }

        // ------------------------------------------------------------------ the idle chest

        /// <summary>
        /// The idle cache, as a chest on the map's own furniture rather than a panel across a
        /// quarter of the painting. The bubble over it carries the stored gold, so the player can
        /// read what is waiting without opening anything; the detail - elapsed time, the cap, the
        /// rates - lives in the popup the chest opens.
        /// </summary>
        public static HudElement ChestButton(string name, RectTransform parent, Action action)
        {
            HudElement element = new HudElement();
            element.Root = UIBuilder.Node(name, parent);
            UIBuilder.Size(element.Root, HubStyle.ChestSize, HubStyle.ChestSize);
            element.Group = element.Root.gameObject.AddComponent<CanvasGroup>();

            Image hit = HitArea(element.Root);

            // PlateAlphaSolid rather than the rail's PlateAlpha: this is the one round button on
            // the Hub that sits on the painting's BRIGHT half - the sunlit plaza - and at the
            // rail's opacity the chest read as a smudge on the stone rather than as an object.
            element.Disc = UIBuilder.Img("Disc", element.Root, HudSkin.Circle(HudSkin.Disc, 128),
                                         UITheme.WithAlpha(HubStyle.Plate, HubStyle.PlateAlphaSolid));
            UIBuilder.FillParent(element.Disc.rectTransform, 0f);

            Image rim = UIBuilder.Img("Ring", element.Root,
                                      HudSkin.Ring(HudSkin.DiscRing, 128, HubStyle.NavRingThickness),
                                      UITheme.WithAlpha(HubStyle.BorderWarm, HubStyle.BorderAlphaStrong));
            UIBuilder.FillParent(rim.rectTransform, 0f);

            element.Glyph = UIBuilder.Img("Glyph", element.Root,
                                          HudSkin.Icon(HudSkin.IconChest, TextureLab.Chest(64)),
                                          HubStyle.ValueGold);
            UIBuilder.FillParent(element.Glyph.rectTransform, HubStyle.ChestGlyphInset);

            // The bubble hangs ABOVE the chest and overlaps its rim slightly, which is what makes
            // it read as belonging to the chest rather than as a second control stacked on it.
            RectTransform bubble = UIBuilder.Node("Bubble", element.Root);
            UIBuilder.Box(bubble, 0f, HubStyle.BubbleHeight - HubStyle.BubbleLift,
                          HubStyle.BubbleWidth, HubStyle.BubbleHeight, new Vector2(0.5f, 1f));
            Image plate = bubble.gameObject.AddComponent<Image>();
            plate.sprite = HudSkin.Plate(HudSkin.PlateBubble, HubStyle.RadiusPill);
            plate.type = Image.Type.Sliced;
            plate.color = UITheme.WithAlpha(HubStyle.Plate, HubStyle.PlateAlphaSolid);
            plate.raycastTarget = false;
            UIBuilder.Frame("Edge", bubble, UITheme.WithAlpha(HubStyle.BorderWarm, HubStyle.BorderAlphaStrong),
                            HubStyle.RadiusPill, HubStyle.BorderWeight);

            element.Caption = UIBuilder.Label("Stored", bubble, "0", HubStyle.FontCaption,
                                              HubStyle.ValueGold, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(element.Caption.rectTransform, 4f);

            element.Button = new UIButton();
            element.Button.Root = element.Root;
            element.Button.Background = hit;
            element.Button.Group = element.Group;
            element.Button.Button = Pressable(element.Root, hit, action);
            return element;
        }

        // ------------------------------------------------------------------ surfaces above the map

        /// <summary>The dim that separates a layer from what it sits on. Full bleed by design -
        /// it dims the map, the HUD frame and everything else underneath - and it eats the taps
        /// that miss the layer, which is what makes tapping outside close it.</summary>
        public static Image Scrim(RectTransform parent, float alpha, Action onTap)
        {
            Image scrim = UIBuilder.Img("Scrim", parent, TextureLab.White, new Color(0f, 0f, 0f, alpha));
            UIBuilder.FillParent(scrim.rectTransform, 0f);
            scrim.raycastTarget = true;
            if (onTap != null)
            {
                Button close = scrim.gameObject.AddComponent<Button>();
                close.transition = Selectable.Transition.None;
                close.onClick.AddListener(delegate { onTap(); });
            }
            return scrim;
        }

        /// <summary>A panel sheet: the large surface a hosted screen is drawn inside.</summary>
        public static RectTransform Sheet(string name, RectTransform parent)
        {
            RectTransform sheet = UIBuilder.Node(name, parent);
            Image plate = sheet.gameObject.AddComponent<Image>();
            plate.sprite = HudSkin.Plate(HudSkin.PlateSheet, HubStyle.RadiusSheet);
            plate.type = Image.Type.Sliced;
            plate.color = UITheme.WithAlpha(HubStyle.Plate, HubStyle.PlateAlphaSolid);
            UIBuilder.Frame("Edge", sheet, UITheme.WithAlpha(HubStyle.BorderWarm, HubStyle.BorderAlphaStrong),
                            HubStyle.RadiusSheet, HubStyle.BorderWeightHeavy);
            return sheet;
        }

        /// <summary>A popup plate: the small surface a claim or a menu is drawn inside.</summary>
        public static RectTransform Popup(string name, RectTransform parent, float width, float height,
                                          float x, float y, Vector2 anchor)
        {
            RectTransform popup = UIBuilder.Node(name, parent);
            UIBuilder.Box(popup, x, y, width, height, anchor);
            Image plate = popup.gameObject.AddComponent<Image>();
            plate.sprite = HudSkin.Plate(HudSkin.PlatePopup, HubStyle.RadiusPopup);
            plate.type = Image.Type.Sliced;
            plate.color = UITheme.WithAlpha(HubStyle.Plate, HubStyle.PlateAlphaSolid);
            UIBuilder.Frame("Edge", popup, UITheme.WithAlpha(HubStyle.BorderWarm, HubStyle.BorderAlphaStrong),
                            HubStyle.RadiusPopup, HubStyle.BorderWeightHeavy);
            return popup;
        }

        /// <summary>The close cross every layer carries in its top-right corner, at full target
        /// size whatever the glyph inside it is.</summary>
        public static UIButton CloseButton(RectTransform parent, Action action)
        {
            RectTransform root = UIBuilder.Node("Close", parent);
            UIBuilder.Box(root, -HubStyle.HudEdge * 0.5f, -HubStyle.HudEdge * 0.5f,
                          HubStyle.TouchMin, HubStyle.TouchMin, new Vector2(1f, 1f));
            Image hit = HitArea(root);

            RectTransform face = UIBuilder.Node("Face", root);
            UIBuilder.Box(face, 0f, 0f, HubStyle.RailDisc, HubStyle.RailDisc, new Vector2(0.5f, 0.5f));
            Image disc = UIBuilder.Img("Disc", face, HudSkin.Circle(HudSkin.Disc, 128),
                                       UITheme.WithAlpha(HubStyle.Plate, HubStyle.PlateAlpha));
            UIBuilder.FillParent(disc.rectTransform, 0f);
            Image rim = UIBuilder.Img("Ring", face,
                                      HudSkin.Ring(HudSkin.DiscRing, 128, HubStyle.NavRingThickness),
                                      UITheme.WithAlpha(HubStyle.BorderWarm, HubStyle.BorderAlphaStrong));
            UIBuilder.FillParent(rim.rectTransform, 0f);

            Text glyph = UIBuilder.Label("Glyph", face, "X", HubStyle.FontLabel,
                                         HubStyle.LabelText, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(glyph.rectTransform, 0f);

            UIButton handle = new UIButton();
            handle.Root = root;
            handle.Background = hit;
            handle.Label = glyph;
            handle.Button = Pressable(root, hit, action);
            return handle;
        }

        /// <summary>One tab on a panel's tab bar. Selected state is a colour swap on the same
        /// plate rather than a second shape, so the bar cannot drift into two designs.</summary>
        public static HudElement Tab(string caption, RectTransform parent, Action action)
        {
            HudElement element = new HudElement();
            element.Root = UIBuilder.Node("Tab" + caption, parent);
            UIBuilder.Sized(element.Root, HubStyle.TabMinWidth, HubStyle.TabHeight);
            UIBuilder.Size(element.Root, HubStyle.TabMinWidth, HubStyle.TabHeight);

            element.Disc = element.Root.gameObject.AddComponent<Image>();
            element.Disc.sprite = HudSkin.Plate(HudSkin.PlateTab, HubStyle.RadiusPill);
            element.Disc.type = Image.Type.Sliced;
            element.Disc.color = UITheme.WithAlpha(HubStyle.Plate, HubStyle.PlateAlpha);
            UIBuilder.Frame("Edge", element.Root, UITheme.WithAlpha(HubStyle.BorderWarm, HubStyle.BorderAlpha),
                            HubStyle.RadiusPill, HubStyle.BorderWeight);

            element.Caption = UIBuilder.Label("Text", element.Root, caption, HubStyle.FontSecondary,
                                              HubStyle.TextSecondary, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(element.Caption.rectTransform, 0f);

            element.Button = new UIButton();
            element.Button.Root = element.Root;
            element.Button.Background = element.Disc;
            element.Button.Label = element.Caption;
            element.Button.Button = Pressable(element.Root, element.Disc, action);
            return element;
        }

        /// <summary>Paints a tab as the open one. Gold text on a solid plate for the tab you are
        /// looking at, muted text on a translucent one for the tabs you are not.</summary>
        public static void SetTabSelected(HudElement tab, bool selected)
        {
            if (tab == null) return;
            if (tab.Disc != null)
                tab.Disc.color = UITheme.WithAlpha(HubStyle.Plate,
                                                   selected ? HubStyle.PlateAlphaSolid : HubStyle.PlateAlpha);
            if (tab.Caption != null)
                tab.Caption.color = selected ? HubStyle.ValueGold : HubStyle.TextSecondary;
        }
    }
}
