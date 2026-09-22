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

        // ------------------------------------------------------------------ the rail

        /// <summary>
        /// A rail button: an Emberforge disc inside a full-size hit rect, with an optional
        /// notification dot on its top-right. Friends, Mail, Events and the overflow menu are all
        /// this, so the rail reads as one control strip rather than four decisions.
        ///
        /// The disc, its rim and its press are EmberforgeUI.IconButton's now rather than three
        /// Images assembled here. What is left in this method is the two things that are the
        /// HUB's rather than the kit's: the notification dot, which only a HUD element has, and
        /// the HudElement handle the Hub reaches back through to relabel and re-badge.
        /// </summary>
        public static HudElement RailButton(string name, RectTransform parent, string slot,
                                            Sprite fallbackGlyph, Color tint, Action action)
        {
            ForgeDisc disc = EmberforgeUI.IconButton("Rail" + name, parent, slot, fallbackGlyph,
                                                     tint, HubStyle.RailDisc, action);
            HudElement element = Wrap(disc);
            AddBadge(element, disc.Face);
            return element;
        }

        /// <summary>Carries a built disc across into the handle the Hub already speaks in. One
        /// conversion in one place, rather than every call site knowing both shapes.</summary>
        private static HudElement Wrap(ForgeDisc disc)
        {
            HudElement element = new HudElement();
            element.Root = disc.Root;
            element.Group = disc.Group;
            element.Disc = disc.Disc;
            element.Glyph = disc.Glyph;
            element.Button = disc.Button;
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

            // The same dark outline every other element in the kit carries. Without it a red dot
            // on a gold rim is two saturated colours meeting with nothing between them, which is
            // the one place on the Hub that reads as a different piece of software.
            Image ring = UIBuilder.Img("Outline", element.Badge,
                                       TextureLab.Ring(64, Emberforge.OutlineWeight * 2),
                                       Emberforge.Outline);
            UIBuilder.FillParent(ring.rectTransform, 0f);
            ring.raycastTarget = false;

            element.BadgeText = EmberforgeUI.Label("Count", element.Badge, "", HubStyle.FontSoon,
                                                   Emberforge.TextOnColor, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(element.BadgeText.rectTransform, 0f);
            element.Badge.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ the bottom nav bar

        /// <summary>
        /// A nav cell: an Emberforge disc with a glyph in it and a caption underneath, in a cell
        /// wide enough for the longest word in the row. The whole cell is the target, not the
        /// disc, which is what makes the row comfortable to hit with a thumb rather than accurate
        /// to hit with a stylus.
        ///
        /// The disc is built at NavIcon and the cell at NavCellWidth x NavCellHeight, so the hit
        /// rect IconButton gives the disc is deliberately discarded here - the cell replaces it,
        /// and the cell is larger on both axes than the minimum the disc would have carried.
        /// </summary>
        public static HudElement NavButton(string name, RectTransform parent, string caption,
                                           string slot, Sprite fallbackGlyph, Color tint, Action action)
        {
            ForgeDisc disc = EmberforgeUI.IconButton("Nav" + name, parent, slot, fallbackGlyph,
                                                     tint, HubStyle.NavIcon, action);
            HudElement element = Wrap(disc);

            UIBuilder.Sized(element.Root, HubStyle.NavCellWidth, HubStyle.NavCellHeight);
            UIBuilder.Size(element.Root, HubStyle.NavCellWidth, HubStyle.NavCellHeight);
            // The disc goes to the TOP of the cell rather than its middle, because the caption
            // takes the bottom of it. IconButton centres its face, which is right for a rail and
            // wrong for a cell that is taller than its art.
            UIBuilder.Box(disc.Face, 0f, 0f, HubStyle.NavIcon, HubStyle.NavIcon, new Vector2(0.5f, 1f));

            element.Caption = EmberforgeUI.Label("Label", element.Root, caption, HubStyle.FontCaption,
                                                 Emberforge.TextBody, TextAnchor.UpperCenter);
            UIBuilder.Box(element.Caption.rectTransform, 0f, -(HubStyle.NavIcon + HubStyle.NavIconGap),
                          HubStyle.NavCellWidth, HubStyle.NavCaptionHeight, new Vector2(0.5f, 1f));
            // Outlined rather than shadowed: this row sits on the painting's foreground, which is
            // the brightest part of the map, and a drop shadow on one side does not hold a word
            // off a light background the way a stroke all the way round does.
            UIBuilder.Outlined(element.Caption, Emberforge.Outline, Emberforge.StrokeThin);
            element.Button.Label = element.Caption;

            AddBadge(element, disc.Face);
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
            ForgeDisc disc = EmberforgeUI.IconButton(name, parent, HudSkin.IconChest,
                                                     TextureLab.Chest(64), Emberforge.TextGold,
                                                     HubStyle.ChestSize, action);
            HudElement element = Wrap(disc);

            // The bubble hangs ABOVE the chest and overlaps its rim slightly, which is what makes
            // it read as belonging to the chest rather than as a second control stacked on it. It
            // is a currency pill's plate, because that is what it is: a figure in a dark capsule
            // with a gold line inside it.
            RectTransform bubble = UIBuilder.Node("Bubble", element.Root);
            UIBuilder.Box(bubble, 0f, HubStyle.BubbleHeight - HubStyle.BubbleLift,
                          HubStyle.BubbleWidth, HubStyle.BubbleHeight, new Vector2(0.5f, 1f));
            Image plate = bubble.gameObject.AddComponent<Image>();
            plate.sprite = EmberforgeSkin.Get(EmberforgeArt.Slot.Pill);
            plate.type = Image.Type.Sliced;
            plate.color = Color.white;
            plate.raycastTarget = false;

            element.Caption = EmberforgeUI.Label("Stored", bubble, "0", HubStyle.FontCaption,
                                                 Emberforge.TextGold, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(element.Caption.rectTransform, Emberforge.Space8);
            UIBuilder.Outlined(element.Caption, Emberforge.Outline, Emberforge.StrokeThin);
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

        /// <summary>
        /// A PANEL SHEET: the large surface a hosted screen is drawn inside.
        ///
        /// An Emberforge panel with its corner ornaments now. It was deliberately left in the old
        /// language while the five screens it frames were unconverted - DESIGN-EMBERFORGE records
        /// that decision and its reason, which was that a gold frame around old chrome is worse
        /// than either language on its own. Profile, Account, Friends, Settings and Guild have all
        /// had their pass, so the condition that note set for converting this has been met by the
        /// note's own terms.
        /// </summary>
        public static RectTransform Sheet(string name, RectTransform parent)
        {
            RectTransform sheet = UIBuilder.Node(name, parent);
            EmberforgeUI.PanelOn(sheet, true);
            return sheet;
        }

        /// <summary>
        /// A popup plate: the small surface a claim or a menu is drawn inside.
        ///
        /// An Emberforge panel with its corner ornaments, because a popup is the one surface on
        /// this screen that a player is LOOKING AT rather than looking past - the map layer's own
        /// plates stay quiet for the opposite reason, and HubStyle still owns those.
        /// </summary>
        public static RectTransform Popup(string name, RectTransform parent, float width, float height,
                                          float x, float y, Vector2 anchor)
        {
            RectTransform popup = UIBuilder.Node(name, parent);
            UIBuilder.Box(popup, x, y, width, height, anchor);
            EmberforgeUI.PanelOn(popup, true);
            return popup;
        }

        /// <summary>
        /// The close cross every layer carries in its top-right corner, at full target size
        /// whatever the glyph inside it is.
        ///
        /// The kit's disc now, and the geometry is unchanged: same corner, same TouchMin hit rect,
        /// same RailDisc face inside it. EmberforgeUI.CloseDisc owns the drawing so the cross on a
        /// panel sheet and the cross on a popup cannot drift into two designs; this stays as the
        /// name every caller already uses, and it hands back the same UIButton it always did.
        /// </summary>
        public static UIButton CloseButton(RectTransform parent, Action action)
        {
            return EmberforgeUI.CloseDisc(parent, action).Button;
        }

        /// <summary>
        /// One tab on a panel's tab bar, in the kit.
        ///
        /// The selected state is a different PLATE rather than a colour swap on one, for the same
        /// reason a pressed button is a different sprite: a colour multiplier on a baked bevel
        /// darkens the highlight and the shadow by the same factor, which is what light does not
        /// do. EmberforgeUI owns both plates; this keeps HudElement as the handle, because
        /// PanelHost holds a list of them and repaints the row through SetTabSelected.
        /// </summary>
        public static HudElement Tab(string caption, RectTransform parent, Action action)
        {
            ForgeTab built = EmberforgeUI.Tab(caption, parent, HubStyle.TabMinWidth,
                                              HubStyle.TabHeight, action);
            HudElement element = new HudElement();
            element.Root = built.Root;
            element.Disc = built.Plate;
            element.Caption = built.Caption;
            element.Button = built.Button;
            EmberforgeUI.SetTabSelected(built, false);
            return element;
        }

        /// <summary>Paints a tab as the open one.</summary>
        public static void SetTabSelected(HudElement tab, bool selected)
        {
            if (tab == null) return;
            ForgeTab built = new ForgeTab();
            built.Root = tab.Root;
            built.Plate = tab.Disc;
            built.Caption = tab.Caption;
            built.Button = tab.Button;
            EmberforgeUI.SetTabSelected(built, selected);
        }
    }
}
