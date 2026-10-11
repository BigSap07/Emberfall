using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;

namespace Emberfall.UI
{
    /// <summary>Which voice a button speaks in. Two and a disabled state, and deliberately no
    /// more: Ember orange is the thing to press and teal is the way back out, so a screen with
    /// three colours of button is a screen that has stopped saying which one matters.</summary>
    public enum ForgeButtonKind
    {
        Primary,
        Secondary
    }

    /// <summary>A built tab. Returned rather than discarded so a bar can repaint its selection
    /// without looking children up by name.</summary>
    public sealed class ForgeTab
    {
        public RectTransform Root;
        public UIButton Button;
        public Image Plate;
        public Text Caption;
    }

    /// <summary>A built selectable card: a pressable plate that gains the kit's gold frame while
    /// it is the chosen one. Body is where a caller hangs everything it draws, so a repaint can
    /// clear the contents without touching the plate or its button.</summary>
    public sealed class ForgeCard
    {
        public RectTransform Root;
        public RectTransform Body;
        public Image Plate;
        public UIButton Button;
    }

    /// <summary>A built currency pill: the value is what a caller writes to, the plus is what it
    /// wires up, and the jewel is what tells the three of them apart.</summary>
    public sealed class ForgePill
    {
        public RectTransform Root;
        public Text Value;
        public Image Jewel;
        public UIButton Plus;
    }

    /// <summary>A built round button: the art, the hit rect it is centred in, and the glyph on
    /// it. Handed back whole so a caller can hang a notification dot off the FACE rather than off
    /// the hit rect, which is the difference between a dot on the disc's shoulder and a dot
    /// floating in the empty corner of a target that is deliberately larger than its art.</summary>
    public sealed class ForgeDisc
    {
        public RectTransform Root;
        public RectTransform Face;
        public Image Disc;
        public Image Glyph;
        public UIButton Button;
        public CanvasGroup Group;
    }

    /// <summary>A built player plate. Every field here is filled from the session by whoever owns
    /// the plate; this kit owns what it looks like and nothing about what it says.</summary>
    public sealed class ForgePlate
    {
        public RectTransform Root;
        public RectTransform AvatarHost;
        public Text Rank;
        public Text Name;
        public Text Xp;
        public Image XpFill;
        public Button Press;
    }

    /// <summary>
    /// THE COMPONENTS. One function per element of the kit, every one of them built from
    /// Emberforge's tokens and EmberforgeSkin's slots, and no style literal anywhere in the file.
    ///
    /// These are this project's PREFABS. The game builds every pixel of its UI in code from a
    /// deliberately near-empty scene, so there is no prefab asset to hang a sprite slot off;
    /// HudWidgets already established the shape this file follows - one factory per element,
    /// called from one place, with its art exposed by name - and this is that idea applied to the
    /// whole game rather than to the Hub's frame.
    ///
    /// TOUCH TARGETS ARE A PROPERTY OF THE CONTROL, NOT OF THE DRAWING. Every pressable thing
    /// built here is at least HubStyle.TouchMin on both axes even when its art is smaller, which
    /// is the rule the Hub's frame was measured against and the reason it is read from there
    /// rather than restated here.
    /// </summary>
    public static class EmberforgeUI
    {
        // ------------------------------------------------------------------ surfaces

        /// <summary>
        /// A PANEL: the warm three-stop fill, the gold frame and the dark outline, with a small
        /// gold diamond on each corner.
        ///
        /// The ornaments are four small sprites rather than anything drawn into the plate, because
        /// the plate is nine-sliced and a corner drawn into it would stretch with the panel. Four
        /// fixed-size children stay the size they were authored at whatever the panel does.
        /// </summary>
        public static Image Panel(string name, Transform parent, bool ornaments)
        {
            RectTransform root = UIBuilder.Node(name, parent);
            UIBuilder.FillParent(root, 0f);
            return PanelOn(root, ornaments);
        }

        /// <summary>The same panel, built ONTO an existing rect rather than into a new child. The
        /// call site owns the geometry; this owns the surface.</summary>
        public static Image PanelOn(RectTransform rect, bool ornaments)
        {
            Image plate = rect.gameObject.AddComponent<Image>();
            plate.sprite = EmberforgeSkin.Get(EmberforgeArt.Slot.Panel);
            plate.type = Image.Type.Sliced;
            plate.color = Color.white;
            if (ornaments) Ornaments(rect);
            return plate;
        }

        /// <summary>A panel with no gold frame: a card in a grid, a row in a list. The frame is
        /// what says "this is a container"; a hero tile in a grid of forty of them does not need
        /// forty gold frames competing with the portraits inside them.</summary>
        public static Image PlainPanelOn(RectTransform rect)
        {
            Image plate = rect.gameObject.AddComponent<Image>();
            plate.sprite = EmberforgeSkin.Get(EmberforgeArt.Slot.PanelPlain);
            plate.type = Image.Type.Sliced;
            plate.color = Color.white;
            return plate;
        }

        /// <summary>A recess: a trough, an empty slot, a well for content to sit in.</summary>
        public static Image SunkenOn(RectTransform rect)
        {
            Image plate = rect.gameObject.AddComponent<Image>();
            plate.sprite = EmberforgeSkin.Get(EmberforgeArt.Slot.Sunken);
            plate.type = Image.Type.Sliced;
            plate.color = Color.white;
            return plate;
        }

        /// <summary>The same recess as a new child filling its parent, for a call site that owns
        /// a node it cannot put an Image on because something else is already drawing there.</summary>
        public static Image Sunken(string name, Transform parent)
        {
            RectTransform rect = UIBuilder.Node(name, parent);
            UIBuilder.FillParent(rect, 0f);
            return SunkenOn(rect);
        }

        /// <summary>
        /// THE PAGE'S OWN GROUND: one flat Image in the kit's darkest panel tone, behind
        /// everything else on the screen.
        ///
        /// The shared backdrop is the near-black violet every unconverted screen sat on, and warm
        /// brown panels on it read as two different pieces of software. Changing that global
        /// colour would have restyled every page in one edit, including the ones that had not had
        /// a visual pass; one Image per converted screen is the smaller, reversible move, and it
        /// is what HeroesScreen laid down first.
        ///
        /// Returned so an embeddable screen can hide it: a page drawn inside a panel sheet is
        /// already sitting on the sheet's surface, and a second ground over it would cover the
        /// sheet's own face.
        /// </summary>
        public static Image Ground(RectTransform root)
        {
            Image ground = UIBuilder.Img("Ground", root, TextureLab.White, Emberforge.PanelLow);
            UIBuilder.FillParent(ground.rectTransform, 0f);
            ground.raycastTarget = false;
            ground.rectTransform.SetAsFirstSibling();
            return ground;
        }

        /// <summary>
        /// A SHEET: the large framed surface a hosted screen or a full-page card is drawn inside.
        ///
        /// It is a Panel with its ornaments, not a slot of its own. A sheet and a panel are the
        /// same object at two sizes, and the one thing that would have differed - a slightly
        /// larger corner radius - is invisible at the scale a sheet is actually drawn at, which
        /// is not worth a twenty-eighth entry in the catalogue and a second thing to retune.
        /// </summary>
        public static RectTransform Sheet(string name, Transform parent)
        {
            RectTransform sheet = UIBuilder.Node(name, parent);
            PanelOn(sheet, true);
            return sheet;
        }

        /// <summary>
        /// A SELECTABLE CARD: a plate you can press, which gains the kit's gold frame while it is
        /// the one being acted on.
        ///
        /// This is the third pressable thing in the kit and it exists because the other two say
        /// the wrong sentence about a container. A BUTTON has two voices, ember for the thing to
        /// press and teal for the way back out, and a banner slot or a roster row is neither - a
        /// page of them built as buttons comes out as a wall of solid teal, which is the
        /// second-loudest colour in the kit spent on the calmest thing on the screen. A TAB is
        /// closer but its unselected plate is the darkest surface here, right for a tab strip over
        /// a sheet and far too heavy for a card the size of a hero.
        ///
        /// So: the same warm plate a hero card in the collection already uses, and the SELECTED
        /// state is that plate gaining a gold frame. Structure is what gold means everywhere else
        /// in this kit, and "this is the one" is a structural fact.
        /// </summary>
        public static ForgeCard SelectableCard(string name, Transform parent, Action action)
        {
            ForgeCard card = new ForgeCard();
            card.Root = UIBuilder.Node(name, parent);
            card.Plate = PlainPanelOn(card.Root);

            card.Button = new UIButton();
            card.Button.Root = card.Root;
            card.Button.Background = card.Plate;
            card.Button.Button = card.Root.gameObject.AddComponent<Button>();
            card.Button.Button.targetGraphic = card.Plate;
            card.Button.Button.transition = Selectable.Transition.None;
            card.Root.gameObject.AddComponent<ButtonPress>();
            if (action != null) card.Button.Button.onClick.AddListener(delegate { action(); });

            // Everything a caller draws goes in here, so a repaint can clear the contents without
            // destroying the plate, the button or the press feedback attached to the root.
            card.Body = UIBuilder.Node("Body", card.Root);
            UIBuilder.FillParent(card.Body, 0f);
            card.Body.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            return card;
        }

        public static void SetCardSelected(ForgeCard card, bool selected)
        {
            if (card == null || card.Plate == null) return;
            card.Plate.sprite = EmberforgeSkin.Get(selected ? EmberforgeArt.Slot.Panel
                                                            : EmberforgeArt.Slot.PanelPlain);
        }

        /// <summary>
        /// A GOLD RULE between two sections of one panel.
        ///
        /// Sized by the caller on the cross axis and 0 x RuleHeight inside a layout group, which
        /// is the one thing a divider is ever asked for two different ways.
        /// </summary>
        public static Image Rule(string name, Transform parent)
        {
            Image rule = UIBuilder.Img(name, parent, EmberforgeSkin.Get(EmberforgeArt.Slot.Rule),
                                       Color.white);
            rule.type = Image.Type.Sliced;
            rule.raycastTarget = false;
            UIBuilder.Sized(rule.rectTransform, 0f, Emberforge.RuleHeight);
            return rule;
        }

        /// <summary>
        /// A CHIP: a small dark tag with a tinted hairline round it and its word in the same tint.
        ///
        /// Three images rather than one, and for the reason the currency jewel is two: Image.color
        /// multiplies the whole sprite, so a single tinted chip would tint the kit's dark outline
        /// along with the fill and every aspect in a grid would carry a differently coloured
        /// outline. The body is never tinted, the wash and the line are the only things that are,
        /// and the outline stays exactly where every other element in the kit has it.
        ///
        /// It replaces UIBuilder.Chip at every converted call site. That one draws a flat
        /// translucent rectangle in the tint with a matching border, which is the placeholder
        /// treatment this whole pass exists to remove.
        /// </summary>
        public static RectTransform Chip(string name, Transform parent, string text, Color tint,
                                         int fontSize)
        {
            RectTransform root = UIBuilder.Node(name, parent);

            Image body = root.gameObject.AddComponent<Image>();
            body.sprite = EmberforgeSkin.Get(EmberforgeArt.Slot.ChipBody);
            body.type = Image.Type.Sliced;
            body.color = Color.white;
            body.raycastTarget = false;

            Image wash = UIBuilder.Img("Wash", root, EmberforgeSkin.Get(EmberforgeArt.Slot.ChipLine),
                                       UITheme.WithAlpha(tint, Emberforge.ChipWashAlpha));
            wash.type = Image.Type.Sliced;
            UIBuilder.FillParent(wash.rectTransform, 0f);
            wash.raycastTarget = false;

            Image line = UIBuilder.Img("Line", root, EmberforgeSkin.Get(EmberforgeArt.Slot.ChipLine),
                                       UITheme.WithAlpha(tint, Emberforge.ChipLineAlpha));
            line.type = Image.Type.Sliced;
            UIBuilder.FillParent(line.rectTransform, 0f);
            line.raycastTarget = false;

            Text label = Label("Text", root, text, fontSize, tint, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(label.rectTransform, Emberforge.Space8, 2f, Emberforge.Space8, 2f);
            UIBuilder.Outlined(label, Emberforge.Outline, Emberforge.StrokeThin);
            return root;
        }

        /// <summary>
        /// Recolours a chip built by <see cref="Chip"/> in place, for the one thing a chip is
        /// asked to do that its text cannot: change state.
        ///
        /// It touches the same three graphics Chip tints and leaves the body alone, so a chip
        /// retinted here is indistinguishable from one built at that tint. Rebuilding instead
        /// would mean destroying and re-creating a node inside a repaint, which is how a screen
        /// ends up with two chips in one slot for a frame.
        /// </summary>
        public static void SetChipTint(RectTransform chip, Color tint)
        {
            if (chip == null) return;
            Transform wash = chip.Find("Wash");
            if (wash != null)
            {
                Image w = wash.GetComponent<Image>();
                if (w != null) w.color = UITheme.WithAlpha(tint, Emberforge.ChipWashAlpha);
            }
            Transform line = chip.Find("Line");
            if (line != null)
            {
                Image l = line.GetComponent<Image>();
                if (l != null) l.color = UITheme.WithAlpha(tint, Emberforge.ChipLineAlpha);
            }
            Transform text = chip.Find("Text");
            if (text != null)
            {
                Text t = text.GetComponent<Text>();
                if (t != null) t.color = tint;
            }
        }

        /// <summary>
        /// Rewords a chip built by <see cref="Chip"/> in place, the other half of what a chip is
        /// asked to do when it changes state. Paired with <see cref="SetChipTint"/> because a chip
        /// that changes what it says almost always changes what colour it says it in, and a screen
        /// doing one of those by hand and the other through the kit is a screen with two chips
        /// that disagree.
        /// </summary>
        public static void SetChipText(RectTransform chip, string text)
        {
            if (chip == null) return;
            Transform label = chip.Find("Text");
            if (label == null) return;
            Text t = label.GetComponent<Text>();
            if (t != null) t.text = text;
        }

        /// <summary>The four corner diamonds. Non-interactive, so they never eat a tap aimed at
        /// whatever is inside the panel's corner.</summary>
        public static void Ornaments(RectTransform panel)
        {
            float s = Emberforge.OrnamentSize;
            float inset = s * 0.34f;
            AddOrnament(panel, new Vector2(0f, 1f), inset, -inset, s);
            AddOrnament(panel, new Vector2(1f, 1f), -inset, -inset, s);
            AddOrnament(panel, new Vector2(0f, 0f), inset, inset, s);
            AddOrnament(panel, new Vector2(1f, 0f), -inset, inset, s);
        }

        private static void AddOrnament(RectTransform panel, Vector2 anchor, float x, float y, float size)
        {
            Image gem = UIBuilder.Img("Ornament", panel,
                                      EmberforgeSkin.Get(EmberforgeArt.Slot.Ornament), Color.white);
            UIBuilder.Box(gem.rectTransform, x, y, size, size, anchor);
            gem.raycastTarget = false;
        }

        // ------------------------------------------------------------------ the title ribbon

        /// <summary>
        /// A TITLE RIBBON: an ember-orange band with a gold top edge and a tailed end on each
        /// side, carrying one line of Cinzel.
        ///
        /// It overhangs the panel it labels by Emberforge.RibbonOverhang on each side. That
        /// overhang is the whole reason a ribbon reads as laid ACROSS a panel rather than as a
        /// coloured strip inside it, and it is why the caller passes the panel's width rather than
        /// the ribbon's: the ribbon works out its own.
        /// </summary>
        public static Text TitleRibbon(string name, Transform parent, string text, float panelWidth)
        {
            RectTransform root = UIBuilder.Node(name, parent);
            float width = panelWidth + Emberforge.RibbonOverhang * 2f;
            UIBuilder.Box(root, 0f, Emberforge.RibbonHeight * 0.5f, width, Emberforge.RibbonHeight,
                          new Vector2(0.5f, 1f));

            // Tails first, body second. Unity draws a parent's own graphic before its children, so
            // the body has to be created AFTER the tails for their inner halves to be buried; the
            // same ordering rule HubStyle.SurfaceChild documents for its marker tails.
            AddTail(root, false);
            AddTail(root, true);

            RectTransform bodyRect = UIBuilder.Node("Body", root);
            UIBuilder.FillParent(bodyRect, Emberforge.RibbonOverhang, 0f, Emberforge.RibbonOverhang, 0f);
            Image body = bodyRect.gameObject.AddComponent<Image>();
            body.sprite = EmberforgeSkin.Get(EmberforgeArt.Slot.Ribbon);
            body.type = Image.Type.Sliced;
            body.raycastTarget = false;

            Text label = Title("Text", root, text, UITheme.FontHeading, Emberforge.TextOnColor,
                               TextAnchor.MiddleCenter);
            UIBuilder.FillParent(label.rectTransform, Emberforge.RibbonOverhang + Emberforge.Space12,
                                 0f, Emberforge.RibbonOverhang + Emberforge.Space12, 0f);
            UIBuilder.Outlined(label, Emberforge.Outline, Emberforge.StrokeThin);

            // BEST FIT, and it is not optional here. A ribbon's width comes from the panel it
            // labels, its text comes from content, and Cinzel is a wide Roman face - so the
            // moment a chapter is called something longer than the card it is on, an overflowing
            // label runs straight out past both tails and off the panel. The first capture of
            // the campaign card showed exactly that.
            //
            // Best fit only shrinks when the label is allowed to WRAP; with horizontal overflow
            // on, Unity measures the text as infinitely wide and never reduces the size. So the
            // wrap goes on and the vertical overflow comes off, which keeps a long title on one
            // shrunk line rather than two clipped ones.
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = UITheme.FontTiny - 4;
            label.resizeTextMaxSize = UITheme.FontHeading;
            return label;
        }

        private static void AddTail(RectTransform root, bool right)
        {
            Image tail = UIBuilder.Img(right ? "TailRight" : "TailLeft", root,
                                       EmberforgeSkin.Get(EmberforgeArt.Slot.RibbonTail), Color.white);
            // Twice the overhang wide so its inner half is buried under the body, which is what
            // makes the tail continue out of the ribbon instead of being stuck onto its end.
            UIBuilder.Box(tail.rectTransform, 0f, 0f, Emberforge.RibbonOverhang * 2f,
                          Emberforge.RibbonHeight, new Vector2(right ? 1f : 0f, 0.5f));
            // The one sprite serves both ends, mirrored. Two authored tails is two things that can
            // stop matching each other.
            if (!right) tail.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            tail.raycastTarget = false;
        }

        // ------------------------------------------------------------------ the map marker

        /// <summary>
        /// A BUILDING NAMETAG: the lightest surface in the kit, and the only translucent one.
        ///
        /// A small banner in the palette - warm fill, a thin gold edge, the kit's dark outline -
        /// and then it stops. No heavy frame, no corner ornaments, no gloss. The Hub's layer 0
        /// rule is that the buildings are the primary way into this game and nothing above may
        /// cover one, so a marker has to say a building's name without becoming an object in front
        /// of it. That is what the light treatment and the translucent fill are for; see the
        /// marker section in Emberforge.
        ///
        /// Built ONTO an existing rect rather than into a new child, because the caller owns the
        /// geometry: the marker's width comes from its own word and its lift off the anchor is the
        /// Hub's to decide.
        /// </summary>
        public static Image MarkerPlateOn(RectTransform rect)
        {
            Image plate = rect.gameObject.AddComponent<Image>();
            plate.sprite = EmberforgeSkin.Get(EmberforgeArt.Slot.Marker);
            plate.type = Image.Type.Sliced;
            plate.color = Color.white;
            return plate;
        }

        /// <summary>
        /// The same plate as a CHILD that fills its parent.
        ///
        /// Not a stylistic preference: Unity draws a parent's own Graphic before all of its
        /// children, so a tail added as a child of a plate-bearing node draws ON TOP of that plate
        /// instead of behind it, and the half that was supposed to be buried shows as a diamond of
        /// doubled alpha. Moving the plate into a child created AFTER the tail puts the two in the
        /// right order. HubStyle.SurfaceChild records the same trap for the same reason.
        /// </summary>
        public static Image MarkerPlateChild(string name, RectTransform parent)
        {
            RectTransform rt = UIBuilder.Node(name, parent);
            UIBuilder.FillParent(rt, 0f);
            return MarkerPlateOn(rt);
        }

        /// <summary>
        /// The pointer that ties a marker back to the structure it names.
        ///
        /// A diamond carrying the same outline and the same translucent fill, centred on the
        /// plate's bottom edge so the plate buries its top half and what is left is a triangle
        /// aiming at the building. It was a bare rotated white square before, which had no outline
        /// and so was the one piece of Hub chrome not wearing the kit.
        ///
        /// MUST be created BEFORE the plate that buries it - see MarkerPlateChild.
        /// </summary>
        public static Image MarkerTail(RectTransform parent, float size)
        {
            Image tail = UIBuilder.Img("Tail", parent,
                                       EmberforgeSkin.Get(EmberforgeArt.Slot.MarkerTail),
                                       Color.white);
            UIBuilder.Box(tail.rectTransform, 0f, 0f, size, size, new Vector2(0.5f, 0f));
            tail.raycastTarget = false;
            return tail;
        }

        // ------------------------------------------------------------------ buttons

        /// <summary>
        /// A BUTTON: a face on a lip inside an edge, with a gloss over the top of the face and a
        /// press that drops the face into the lip.
        ///
        /// The pressed and disabled states are SPRITES, not tints. A colour multiplier on a baked
        /// bevel darkens the highlight and the shadow by the same factor, which is exactly what
        /// light does not do, and it is the reason a tinted chunky button looks like a photograph
        /// of a button with the brightness turned down. Swapping to art drawn at the pressed depth
        /// is the only way the highlight moves.
        /// </summary>
        public static UIButton Button(string name, Transform parent, string text, int fontSize,
                                      ForgeButtonKind kind)
        {
            UIButton handle = new UIButton();
            handle.Root = UIBuilder.Node(name, parent);
            handle.Group = handle.Root.gameObject.AddComponent<CanvasGroup>();

            handle.Background = handle.Root.gameObject.AddComponent<Image>();
            handle.Background.type = Image.Type.Sliced;
            handle.Background.color = Color.white;

            handle.Button = handle.Root.gameObject.AddComponent<Button>();
            handle.Button.targetGraphic = handle.Background;
            // None, not ColorTint: the whole point is that the states are different art. Leaving
            // the tint on would multiply the swapped sprite as well and undo it.
            handle.Button.transition = Selectable.Transition.None;

            // A content rect that the press moves, rather than moving the button's own Root. The
            // Root carries the layout, and a press that nudged it would fight a LayoutGroup for
            // the four frames the press lasts.
            RectTransform content = UIBuilder.Node("Content", handle.Root);
            UIBuilder.FillParent(content, 0f);

            RectTransform gloss = UIBuilder.Node("Gloss", content);
            gloss.anchorMin = new Vector2(0f, 1f - Emberforge.GlossFraction);
            gloss.anchorMax = new Vector2(1f, 1f);
            gloss.offsetMin = new Vector2(Emberforge.OutlineWeight + 2f, 0f);
            gloss.offsetMax = new Vector2(-(Emberforge.OutlineWeight + 2f), -(Emberforge.OutlineWeight + 2f));
            Image glossImage = gloss.gameObject.AddComponent<Image>();
            glossImage.sprite = EmberforgeSkin.Get(EmberforgeArt.Slot.Gloss);
            glossImage.type = Image.Type.Sliced;
            glossImage.raycastTarget = false;

            handle.Label = Label("Text", content, text, fontSize, Emberforge.TextOnColor,
                                 TextAnchor.MiddleCenter);
            // The label sits over the FACE, so it is lifted by half the lip: centred in the
            // button's rect it would read as slightly low, because the bottom of that rect is the
            // lip rather than part of the face.
            UIBuilder.FillParent(handle.Label.rectTransform, Emberforge.Space12,
                                 Emberforge.Space4, Emberforge.Space12,
                                 Emberforge.Space4 + Emberforge.ButtonLip);
            UIBuilder.Outlined(handle.Label, Emberforge.Outline, Emberforge.StrokeThin);

            EmberforgeButtonSkin skin = handle.Root.gameObject.AddComponent<EmberforgeButtonSkin>();
            skin.Bind(handle.Background, content, glossImage, kind);
            handle.InteractableChanged = skin.SetInteractable;
            skin.SetInteractable(true);
            return handle;
        }

        /// <summary>
        /// A RAIL DISC: a round gold-rimmed button with a glyph in it, inside a full-size hit rect.
        ///
        /// The disc is the art and the hit rect is the control. They are different sizes on
        /// purpose - see the note at the top of this file - and the glyph is a slot, so a painted
        /// icon replaces it with no code change.
        /// </summary>
        public static ForgeDisc IconButton(string name, Transform parent, string glyphSlot,
                                           Sprite fallbackGlyph, Color glyphTint, float discSize,
                                           Action action)
        {
            ForgeDisc built = new ForgeDisc();
            UIButton handle = new UIButton();
            built.Button = handle;

            handle.Root = UIBuilder.Node(name, parent);
            built.Root = handle.Root;
            UIBuilder.Size(handle.Root, HubStyle.TouchMin, HubStyle.TouchMin);
            handle.Group = handle.Root.gameObject.AddComponent<CanvasGroup>();
            built.Group = handle.Group;

            // A real Image with a fully transparent colour rather than nothing at all: uGUI routes
            // a tap through a Graphic and an empty RectTransform is not one. Alpha does not affect
            // raycasting, which is what makes this work.
            handle.Background = handle.Root.gameObject.AddComponent<Image>();
            handle.Background.sprite = TextureLab.White;
            handle.Background.color = new Color(1f, 1f, 1f, 0f);
            handle.Background.raycastTarget = true;

            handle.Button = handle.Root.gameObject.AddComponent<Button>();
            handle.Button.targetGraphic = handle.Background;
            handle.Button.transition = Selectable.Transition.None;
            if (action != null) handle.Button.onClick.AddListener(delegate { action(); });

            built.Face = UIBuilder.Node("Face", handle.Root);
            UIBuilder.Box(built.Face, 0f, 0f, discSize, discSize, new Vector2(0.5f, 0.5f));

            built.Disc = UIBuilder.Img("Disc", built.Face, EmberforgeSkin.Get(EmberforgeArt.Slot.Disc),
                                       Color.white);
            UIBuilder.FillParent(built.Disc.rectTransform, 0f);

            built.Glyph = UIBuilder.Img("Glyph", built.Face, HudSkin.Icon(glyphSlot, fallbackGlyph),
                                        glyphTint);
            // 0.30 rather than a shared inset: the disc's gold frame plus its inner shadow occupy
            // the outer fifth of it, and a glyph drawn into that band reads as sitting ON the rim
            // rather than in the dish.
            UIBuilder.FillParent(built.Glyph.rectTransform, discSize * 0.30f);

            EmberforgeDiscSkin skin = handle.Root.gameObject.AddComponent<EmberforgeDiscSkin>();
            skin.Bind(built.Disc, built.Face);
            handle.InteractableChanged = skin.SetInteractable;
            return built;
        }

        /// <summary>
        /// THE CLOSE CROSS every layer carries in its top-right corner.
        ///
        /// A disc rather than a chunky button, for the reason ScreenChrome's back control is one:
        /// a rectangular ember button in the corner of every sheet would be a second primary
        /// action competing with whatever the sheet's own is, and closing is never the thing a
        /// layer wants pressed.
        ///
        /// The cross is a Text glyph over the disc rather than a sprite, exactly as the header's
        /// chevron is, and for the same reason - there is no cross in the generator and adding
        /// one for two callers would put a shape in the shared kit that nothing else asks for.
        /// </summary>
        public static ForgeDisc CloseDisc(RectTransform parent, Action action)
        {
            ForgeDisc disc = IconButton("Close", parent, "", TextureLab.Sigil(0, 64),
                                        Emberforge.TextGold, HubStyle.RailDisc, action);
            UIBuilder.Box(disc.Root, -HubStyle.HudEdge * 0.5f, -HubStyle.HudEdge * 0.5f,
                          HubStyle.TouchMin, HubStyle.TouchMin, new Vector2(1f, 1f));
            disc.Glyph.enabled = false;

            Text cross = Title("Cross", disc.Face, "X", UITheme.FontBody, Emberforge.TextGold,
                               TextAnchor.MiddleCenter);
            UIBuilder.FillParent(cross.rectTransform, 0f);
            UIBuilder.Outlined(cross, Emberforge.Outline, Emberforge.StrokeThin);
            cross.raycastTarget = false;
            return disc;
        }

        /// <summary>
        /// THE "?" DISC: where a page keeps what it explains, so the card itself only says what
        /// the player has, what it costs and what they get.
        ///
        /// Built the way CloseDisc is - a glyph-less rail disc with a Text glyph over it - and
        /// unplaced, because a header and a card corner want it in different places. The hit rect
        /// is the kit's TouchMin whatever the disc's size.
        /// </summary>
        public static ForgeDisc HelpDisc(string name, Transform parent, float discSize, Action action)
        {
            ForgeDisc disc = IconButton(name, parent, "", TextureLab.Sigil(0, 64),
                                        Emberforge.TextGold, discSize, action);
            disc.Glyph.enabled = false;

            Text mark = Title("Mark", disc.Face, "?", discSize >= 60f ? UITheme.FontHeading : UITheme.FontSmall,
                              Emberforge.TextGold, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(mark.rectTransform, 0f);
            UIBuilder.Outlined(mark, Emberforge.Outline, Emberforge.StrokeThin);
            mark.raycastTarget = false;
            return disc;
        }

        /// <summary>
        /// A SINGLE-LINE TEXT BOX: the kit's sunken surface with a field wired into it.
        ///
        /// Same parameters and same contract as UIBuilder.TextField, including the one that
        /// matters - the caller owns the RectTransform and this sizes nothing, because a field
        /// inside a VStack and a field at a fixed offset need different things.
        /// </summary>
        public static InputField TextField(string name, Transform parent, string placeholderText,
                                           int fontSize, int characterLimit, bool password)
        {
            RectTransform box = UIBuilder.Node(name, parent);
            SunkenOn(box);

            Text placeholder = Label("Placeholder", box, placeholderText, fontSize,
                                     Emberforge.TextQuiet, TextAnchor.MiddleLeft);
            UIBuilder.FillParent(placeholder.rectTransform, Emberforge.Space16);

            Text shown = Label("Text", box, "", fontSize, Emberforge.TextBody, TextAnchor.MiddleLeft);
            UIBuilder.FillParent(shown.rectTransform, Emberforge.Space16);

            InputField field = box.gameObject.AddComponent<InputField>();
            field.textComponent = shown;
            field.placeholder = placeholder;
            field.characterLimit = characterLimit;
            field.lineType = InputField.LineType.SingleLine;
            if (password) field.contentType = InputField.ContentType.Password;
            return field;
        }

        /// <summary>
        /// ONE ROW OF A STAT TABLE: the name on the left, the figure on the right, a gold rule
        /// under both.
        ///
        /// Returns the VALUE label, because that is the only part of a row a screen ever writes
        /// to after building it - the same contract Widgets.StatRow has, so a screen converts by
        /// changing which of the two it calls.
        /// </summary>
        public static Text StatRow(string name, Transform parent, string label, string value,
                                   Color valueColor, float height)
        {
            RectTransform root = UIBuilder.Node(name, parent);
            UIBuilder.Sized(root, 0f, height);

            // Both labels leave the rule's own height AND a little air clear at the bottom.
            // Filling the whole row centres the text on the row and puts its descenders straight
            // through the gold line under it; leaving exactly the rule's height still let a
            // lowercase g or y touch the top of it. Space4 is what buys the gap.
            Text labelText = Label("Label", root, label, UITheme.FontSmall, Emberforge.TextQuiet,
                                   TextAnchor.MiddleLeft);
            UIBuilder.FillParent(labelText.rectTransform, Emberforge.Space16, 0f, 0f,
                                 Emberforge.RuleHeight + Emberforge.Space4);
            UIBuilder.Outlined(labelText, Emberforge.Outline, Emberforge.StrokeThin);

            Text valueText = Label("Value", root, value, UITheme.FontSmall, valueColor,
                                   TextAnchor.MiddleRight);
            UIBuilder.FillParent(valueText.rectTransform, 0f, 0f, Emberforge.Space16,
                                 Emberforge.RuleHeight + Emberforge.Space4);
            UIBuilder.Outlined(valueText, Emberforge.Outline, Emberforge.StrokeThin);

            Image rule = UIBuilder.Img("Rule", root, EmberforgeSkin.Get(EmberforgeArt.Slot.Rule),
                                       Color.white);
            rule.type = Image.Type.Sliced;
            rule.raycastTarget = false;
            RectTransform rt = rule.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(Emberforge.Space8, 0f);
            rt.offsetMax = new Vector2(-Emberforge.Space8, 0f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, Emberforge.RuleHeight);

            return valueText;
        }

        /// <summary>
        /// A SECTION HEADING inside a panel: one short line of Cinzel in gold, anchored to the
        /// panel's top left.
        ///
        /// Not a ribbon. A ribbon is laid ACROSS a panel and overhangs it, which is right for the
        /// one thing a panel is called and wrong for the third heading down a scrolling card -
        /// three ribbons in a column is three banners fighting each other. A screen with one
        /// subject uses a ribbon; a screen that is a list of sections uses these.
        /// </summary>
        public static Text Heading(string name, Transform parent, string text, float x, float y)
        {
            Text label = Title(name, parent, text, UITheme.FontSmall, Emberforge.TextGold,
                               TextAnchor.UpperLeft);
            UIBuilder.Box(label.rectTransform, x, y, 700f, 30f, new Vector2(0f, 1f));
            UIBuilder.Outlined(label, Emberforge.Outline, Emberforge.StrokeThin);
            return label;
        }

        // ------------------------------------------------------------------ tabs

        /// <summary>One tab. Selected is a different plate and a different text tone, so a bar of
        /// them cannot drift into two designs.</summary>
        public static ForgeTab Tab(string caption, Transform parent, float width, float height,
                                   Action action)
        {
            ForgeTab tab = new ForgeTab();
            tab.Root = UIBuilder.Node("Tab" + caption, parent);
            UIBuilder.Sized(tab.Root, width, height);
            UIBuilder.Size(tab.Root, width, height);

            tab.Plate = tab.Root.gameObject.AddComponent<Image>();
            tab.Plate.sprite = EmberforgeSkin.Get(EmberforgeArt.Slot.TabOff);
            tab.Plate.type = Image.Type.Sliced;
            tab.Plate.color = Color.white;

            tab.Caption = Label("Text", tab.Root, caption, UITheme.FontSmall, Emberforge.TextQuiet,
                                TextAnchor.MiddleCenter);
            UIBuilder.FillParent(tab.Caption.rectTransform, Emberforge.Space8);
            UIBuilder.Outlined(tab.Caption, Emberforge.Outline, Emberforge.StrokeThin);

            tab.Button = new UIButton();
            tab.Button.Root = tab.Root;
            tab.Button.Background = tab.Plate;
            tab.Button.Label = tab.Caption;
            tab.Button.Button = tab.Root.gameObject.AddComponent<Button>();
            tab.Button.Button.targetGraphic = tab.Plate;
            tab.Button.Button.transition = Selectable.Transition.None;
            tab.Root.gameObject.AddComponent<ButtonPress>();
            if (action != null) tab.Button.Button.onClick.AddListener(delegate { action(); });
            return tab;
        }

        public static void SetTabSelected(ForgeTab tab, bool selected)
        {
            if (tab == null) return;
            if (tab.Plate != null)
                tab.Plate.sprite = EmberforgeSkin.Get(selected
                    ? EmberforgeArt.Slot.TabOn : EmberforgeArt.Slot.TabOff);
            if (tab.Caption != null)
                tab.Caption.color = selected ? Emberforge.TextGold : Emberforge.TextQuiet;
        }

        // ------------------------------------------------------------------ the currency pill

        /// <summary>
        /// A CURRENCY PILL: a dark pill with a gold line inside it, a faceted jewel on the left,
        /// the figure in gold, and an optional green plus hanging off the right end.
        ///
        /// The jewel is TWO images - a white body tinted per currency and an untinted gold-rimmed
        /// outline over it - because Image.color multiplies the whole sprite, so a one-piece jewel
        /// would have its dark outline tinted along with its body and every currency would get a
        /// differently coloured outline. Splitting them is what lets one authored gem serve every
        /// token the economy ever grows.
        /// </summary>
        public static ForgePill CurrencyPill(string name, Transform parent, float width, float height,
                                             Color jewelTint, Action onPlus)
        {
            ForgePill pill = new ForgePill();
            pill.Root = UIBuilder.Node(name, parent);
            UIBuilder.Size(pill.Root, width, height);

            Image plate = pill.Root.gameObject.AddComponent<Image>();
            plate.sprite = EmberforgeSkin.Get(EmberforgeArt.Slot.Pill);
            plate.type = Image.Type.Sliced;
            plate.color = Color.white;
            plate.raycastTarget = false;

            float jewelSize = height * 0.72f;
            float jewelPad = height * 0.14f;

            RectTransform jewelHost = UIBuilder.Node("Jewel", pill.Root);
            UIBuilder.Box(jewelHost, jewelPad, 0f, jewelSize, jewelSize, new Vector2(0f, 0.5f));
            pill.Jewel = UIBuilder.Img("Body", jewelHost,
                                       EmberforgeSkin.Get(EmberforgeArt.Slot.JewelBody), jewelTint);
            UIBuilder.FillParent(pill.Jewel.rectTransform, 0f);
            Image jewelEdge = UIBuilder.Img("Edge", jewelHost,
                                            EmberforgeSkin.Get(EmberforgeArt.Slot.JewelEdge), Color.white);
            UIBuilder.FillParent(jewelEdge.rectTransform, 0f);

            // The figure's right inset. It does NOT grow when a plus is present, because the
            // plus is hung mostly OUTSIDE the pill - see below - so it takes almost none of the
            // pill's own width. Reserving for it anyway is what squeezed a five-digit figure into
            // three characters in the first capture of this component.
            float valueLeft = jewelPad + jewelSize + Emberforge.Space8;
            float valueRight = height * 0.3f;

            pill.Value = Label("Value", pill.Root, "0", UITheme.FontSmall, Emberforge.TextGold,
                               TextAnchor.MiddleRight);
            UIBuilder.Box(pill.Value.rectTransform, -valueRight, 0f,
                          width - valueLeft - valueRight, jewelSize, new Vector2(1f, 0.5f));
            UIBuilder.Outlined(pill.Value, Emberforge.Outline, Emberforge.StrokeThin);

            if (onPlus != null)
            {
                pill.Plus = PlusButton("Plus", pill.Root, onPlus);

                // Hung mostly OFF the pill's right end. Fully inside it the plus is a third thing
                // competing for the pill's width with the jewel and the figure, which is exactly
                // what the first capture showed - a five-digit figure clipped to one character
                // behind a green disc.
                //
                // The arithmetic matters because Box pivots on its anchor: at anchor (1, 0.5) the
                // rect's RIGHT edge lands at the pill's right edge plus x, so the hit rect's
                // CENTRE - and therefore the visible disc - sits at x minus half of TouchMin. The
                // offset below puts that centre just outside the pill rather than a thumb's width
                // inside it.
                float centreOutside = Emberforge.Space8 + Emberforge.Space4;
                UIBuilder.Box(pill.Plus.Root, HubStyle.TouchMin * 0.5f + centreOutside, 0f,
                              HubStyle.TouchMin, HubStyle.TouchMin, new Vector2(1f, 0.5f));
            }
            return pill;
        }

        /// <summary>The green add control, inside a full-size hit rect like every other small
        /// round thing in the kit.</summary>
        public static UIButton PlusButton(string name, Transform parent, Action action)
        {
            UIButton handle = new UIButton();
            handle.Root = UIBuilder.Node(name, parent);
            UIBuilder.Size(handle.Root, HubStyle.TouchMin, HubStyle.TouchMin);

            handle.Background = handle.Root.gameObject.AddComponent<Image>();
            handle.Background.sprite = TextureLab.White;
            handle.Background.color = new Color(1f, 1f, 1f, 0f);

            handle.Button = handle.Root.gameObject.AddComponent<Button>();
            handle.Button.targetGraphic = handle.Background;
            handle.Button.transition = Selectable.Transition.None;
            if (action != null) handle.Button.onClick.AddListener(delegate { action(); });

            Image face = UIBuilder.Img("Face", handle.Root,
                                       EmberforgeSkin.Get(EmberforgeArt.Slot.Plus), Color.white);
            UIBuilder.Box(face.rectTransform, 0f, 0f, Emberforge.PlusSize, Emberforge.PlusSize,
                          new Vector2(0.5f, 0.5f));
            face.raycastTarget = false;

            handle.Root.gameObject.AddComponent<ButtonPress>();
            return handle;
        }

        // ------------------------------------------------------------------ the progress bar

        /// <summary>
        /// A PROGRESS BAR: a glossy gold fill in a dark trough.
        ///
        /// The fill is scaled by its ANCHOR rather than by Image.Type.Filled, which is the whole
        /// reason this exists beside UIBuilder.Bar. A filled image cannot be nine-sliced - Unity
        /// refuses the combination - so the shared bar draws plain rectangles and says so in its
        /// own comment. Driving anchorMax instead keeps the fill sliced, which is what lets it
        /// have rounded ends and a baked gloss at any width.
        /// </summary>
        public static Image ProgressBar(string name, Transform parent)
        {
            RectTransform root = UIBuilder.Node(name, parent);
            Image trough = root.gameObject.AddComponent<Image>();
            trough.sprite = EmberforgeSkin.Get(EmberforgeArt.Slot.BarTrough);
            trough.type = Image.Type.Sliced;
            trough.color = Color.white;
            trough.raycastTarget = false;

            RectTransform fillRect = UIBuilder.Node("Fill", root);
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = new Vector2(Emberforge.OutlineWeight, Emberforge.OutlineWeight);
            fillRect.offsetMax = new Vector2(-Emberforge.OutlineWeight, -Emberforge.OutlineWeight);

            Image fill = fillRect.gameObject.AddComponent<Image>();
            fill.sprite = EmberforgeSkin.Get(EmberforgeArt.Slot.BarFill);
            fill.type = Image.Type.Sliced;
            fill.color = Color.white;
            fill.raycastTarget = false;
            return fill;
        }

        /// <summary>
        /// Sets a ProgressBar's fill. The anchor carries the ratio, so the fill keeps its own
        /// rounded ends and its gloss instead of being cropped by a rectangular mask.
        ///
        /// Below about a tenth the bar is hidden outright rather than drawn as a sliver: a
        /// nine-sliced sprite narrower than its own two corner regions renders as a squashed pair
        /// of corners, which reads as a rendering fault rather than as "nearly empty".
        /// </summary>
        public static void SetProgress(Image fill, float ratio)
        {
            if (fill == null) return;
            float r = Mathf.Clamp01(ratio);
            RectTransform rt = fill.rectTransform;
            rt.anchorMax = new Vector2(r, rt.anchorMax.y);
            fill.enabled = r > 0.02f;
        }

        // ------------------------------------------------------------------ portraits

        /// <summary>
        /// A PORTRAIT WITH ITS RARITY AND ITS ASPECT BOTH READABLE, in one of two shapes.
        ///
        /// Tier and aspect are separate readings on separate parts of the badge on purpose: a
        /// single ring cannot carry both, which is exactly the collision Widgets.Portrait records -
        /// it dropped its rarity read to keep its aspect one, because it only had one ring to
        /// spend. This has two channels, and which two depends on what the hero arrives as.
        ///
        ///   A DRAWN BADGE already carries a gold frame, so it gets no frame from here at all.
        ///   Rarity is a coloured plate BEHIND it and aspect is the glow outside that. Drawing a
        ///   rarity ring round one of these was the double-border fault: two frames on one
        ///   portrait, with the badge's four cardinal spikes clipped against the inner edge of the
        ///   outer ring.
        ///
        ///   EVERYTHING ELSE - an undrawn hero, an enemy, a locked silhouette - gets the full
        ///   rarity frame, because there is no frame in the art for it to collide with.
        ///
        /// The portrait itself still comes from Widgets either way, so a hero with painted art and
        /// a hero without resolve exactly as they do everywhere else in the game.
        /// </summary>
        public static RectTransform PortraitFrame(string name, Transform parent, UnitDef def,
                                                  int stars, float size)
        {
            RectTransform root = UIBuilder.Node(name, parent);
            UIBuilder.Size(root, size, size);

            // A HERO WHOSE BADGE SOMEBODY DREW ALREADY HAS A GOLD FRAME, so it gets no second one.
            //
            // This was the fault: the badges Pinoy delivers are finished objects with their own
            // frame and four spikes reaching the edge of the image, and wrapping a rarity ring
            // round one puts two frames on one portrait and clips the spikes against the inner
            // edge of the outer ring. Every hero in the roster is delivered framed, so this was
            // not an edge case - it was every owned portrait in the game.
            //
            // Rarity moves BEHIND the badge instead, as a coloured plate reaching out past its
            // rim, and the star row beside it says the number. Widgets owns that treatment because
            // seven screens draw badges without ever coming through here; showRing is what asks
            // for it, exactly as it already asked for the aspect glow.
            if (Widgets.HasDeclaredBadge(def))
            {
                RectTransform badge = Widgets.Portrait("Face", root, def, stars, size, true);
                UIBuilder.FillParent(badge, 0f);
                return root;
            }

            Image glow = UIBuilder.Img("AspectGlow", root, TextureLab.Glow(128, 1.9f),
                                       UITheme.WithAlpha(Emberforge.Accent(def.Aspect),
                                                         Emberforge.AccentGlowAlpha));
            UIBuilder.FillParent(glow.rectTransform, -size * Emberforge.AccentGlowSpread);
            glow.raycastTarget = false;

            // showRing false: the rarity frame below IS the ring, and Widgets' own aspect ring
            // immediately inside a rarity frame is two concentric rings saying different things.
            RectTransform face = Widgets.Portrait("Face", root, def, stars, size, false);
            UIBuilder.FillParent(face, size * 0.09f);

            Image frame = UIBuilder.Img("Frame", root,
                                        EmberforgeSkin.Get(EmberforgeArt.Slot.Frame(stars)),
                                        Color.white);
            UIBuilder.FillParent(frame.rectTransform, 0f);
            frame.raycastTarget = false;
            return root;
        }

        // ------------------------------------------------------------------ the player plate

        /// <summary>
        /// THE PLAYER PLATE: a face with a rank badge on it, the player's name, their XP figure
        /// and the bar under it, on a framed panel.
        ///
        /// This builds the plate and returns every part of it that carries a word or a number. It
        /// reads nothing from the session and knows nothing about ranks - whoever owns the plate
        /// fills it - which is what keeps the kit free of game logic.
        /// </summary>
        public static ForgePlate PlayerPlate(string name, Transform parent, float width, float height,
                                             Action onPress)
        {
            ForgePlate plate = new ForgePlate();
            plate.Root = UIBuilder.Node(name, parent);
            UIBuilder.Size(plate.Root, width, height);
            PanelOn(plate.Root, false);

            if (onPress != null)
            {
                plate.Press = plate.Root.gameObject.AddComponent<Button>();
                plate.Press.transition = Selectable.Transition.None;
                plate.Press.onClick.AddListener(delegate { onPress(); });
            }

            float pad = Emberforge.Space12;
            float avatar = height - pad * 2f;

            plate.AvatarHost = UIBuilder.Node("Avatar", plate.Root);
            UIBuilder.Box(plate.AvatarHost, pad, 0f, avatar, avatar, new Vector2(0f, 0.5f));

            float badge = avatar * 0.42f;
            RectTransform rankBadge = UIBuilder.Node("Badge", plate.AvatarHost);
            UIBuilder.Box(rankBadge, badge * 0.16f, -badge * 0.1f, badge, badge, new Vector2(1f, 0f));
            Image badgeFace = rankBadge.gameObject.AddComponent<Image>();
            badgeFace.sprite = EmberforgeSkin.Get(EmberforgeArt.Slot.Disc);
            badgeFace.color = Color.white;
            badgeFace.raycastTarget = false;

            plate.Rank = Label("Level", rankBadge, "1", UITheme.FontTiny, Emberforge.TextGold,
                               TextAnchor.MiddleCenter);
            UIBuilder.FillParent(plate.Rank.rectTransform, 0f);
            UIBuilder.Outlined(plate.Rank, Emberforge.Outline, Emberforge.StrokeThin);

            float textLeft = pad + avatar + Emberforge.Space12;
            float textWidth = width - textLeft - pad;
            float xpWidth = textWidth * 0.46f;

            plate.Name = Label("Name", plate.Root, "", UITheme.FontTiny, Emberforge.TextBody,
                               TextAnchor.LowerLeft);
            UIBuilder.Box(plate.Name.rectTransform, textLeft, -pad, textWidth - xpWidth,
                          UITheme.FontTiny + Emberforge.Space8, new Vector2(0f, 1f));
            UIBuilder.Outlined(plate.Name, Emberforge.Outline, Emberforge.StrokeThin);

            plate.Xp = Label("Xp", plate.Root, "", UITheme.FontTiny - 4, Emberforge.TextQuiet,
                             TextAnchor.LowerRight);
            UIBuilder.Box(plate.Xp.rectTransform, -pad, -pad, xpWidth,
                          UITheme.FontTiny + Emberforge.Space8, new Vector2(1f, 1f));
            UIBuilder.Outlined(plate.Xp, Emberforge.Outline, Emberforge.StrokeThin);

            plate.XpFill = ProgressBar("XpBar", plate.Root);
            RectTransform barRoot = plate.XpFill.rectTransform.parent as RectTransform;
            UIBuilder.Box(barRoot, textLeft, pad, textWidth, Emberforge.Space16, new Vector2(0f, 0f));
            return plate;
        }

        // ------------------------------------------------------------------ text

        /// <summary>A label in the kit's UI face. Every caption, stat and figure in a converted
        /// screen goes through here rather than through UIBuilder.Label, which is what makes the
        /// font a token rather than a per-screen decision.</summary>
        public static Text Label(string name, Transform parent, string text, int size, Color color,
                                 TextAnchor anchor)
        {
            Text label = UIBuilder.Label(name, parent, text, size, color, anchor);
            label.font = Emberforge.UiFont;
            return label;
        }

        /// <summary>A label in the kit's title face. Ribbons, headings and display figures.</summary>
        public static Text Title(string name, Transform parent, string text, int size, Color color,
                                 TextAnchor anchor)
        {
            Text label = UIBuilder.Label(name, parent, text, size, color, anchor);
            label.font = Emberforge.TitleFont;
            return label;
        }

        /// <summary>A wrapping label in the kit's UI face.</summary>
        public static Text Body(string name, Transform parent, string text, int size, Color color,
                                TextAnchor anchor)
        {
            Text label = UIBuilder.WrappedLabel(name, parent, text, size, color, anchor);
            label.font = Emberforge.UiFont;
            return label;
        }
    }

    /// <summary>
    /// The press, and the disabled swap, for a chunky button.
    ///
    /// Three states, three sprites, and the content moves with the art. The face drops
    /// Emberforge.ButtonPressDrop when the button goes down, which is exactly how far the lip
    /// shrinks, so the outer silhouette never moves and only the thing that is supposed to look
    /// pushed looks pushed.
    ///
    /// Pointer exit releases it as well as pointer up, because a thumb that slides off a button
    /// mid-press never raises on it, and a button left stuck in its pressed art is the artefact
    /// that produces.
    /// </summary>
    public sealed class EmberforgeButtonSkin : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
                                               IPointerExitHandler
    {
        private Image _plate;
        private RectTransform _content;
        private Image _gloss;
        private ForgeButtonKind _kind;
        private bool _interactable = true;
        private bool _down;
        private Vector2 _restPosition;

        public void Bind(Image plate, RectTransform content, Image gloss, ForgeButtonKind kind)
        {
            _plate = plate;
            _content = content;
            _gloss = gloss;
            _kind = kind;
            if (_content != null) _restPosition = _content.anchoredPosition;
            Apply();
        }

        public void SetInteractable(bool value)
        {
            _interactable = value;
            if (!value) _down = false;
            Apply();
        }

        /// <summary>
        /// Changes which VOICE this button speaks in, after it was built.
        ///
        /// For the one shape of control the kit's two voices are genuinely a state rather than a
        /// property: a formation slot is ember while it is where a held hero would land and teal
        /// the rest of the time, and a picker's chosen row is ember while it is the chosen one.
        /// Repainting through the skin rather than by setting a sprite at the call site is what
        /// keeps the pressed and disabled art in step with it - the screen asks for a voice and
        /// the component still owns every state that voice has.
        /// </summary>
        public void SetKind(ForgeButtonKind kind)
        {
            if (_kind == kind) return;
            _kind = kind;
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
            if (_held) return;
            _down = false;
            Apply();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_held) return;
            _down = false;
            Apply();
        }

        private bool _held;

        /// <summary>
        /// Keeps the button in its pressed art after the finger has lifted, until released.
        ///
        /// For a press that STARTS something rather than finishing it: the summon's first beat is
        /// the pull button staying sunk while the room goes dark, and a button that sprang back
        /// up on release would read as the tap having been handled and over. Interactable is left
        /// alone while held, because the disabled art would replace the pressed art.
        /// </summary>
        public void HoldPressed(bool held)
        {
            _held = held;
            _down = held && _interactable;
            Apply();
        }

        private void Apply()
        {
            if (_plate == null) return;

            string slot;
            if (!_interactable) slot = EmberforgeArt.Slot.ButtonDisabled;
            else if (_kind == ForgeButtonKind.Secondary)
                slot = _down ? EmberforgeArt.Slot.ButtonSecondaryDown : EmberforgeArt.Slot.ButtonSecondary;
            else
                slot = _down ? EmberforgeArt.Slot.ButtonPrimaryDown : EmberforgeArt.Slot.ButtonPrimary;

            _plate.sprite = EmberforgeSkin.Get(slot);

            if (_content != null)
                _content.anchoredPosition = _down
                    ? new Vector2(_restPosition.x, _restPosition.y - Emberforge.ButtonPressDrop)
                    : _restPosition;

            // The gloss is a property of a lit face, and a disabled button is not lit. Hiding it
            // is what stops a greyed-out button still looking glossy and therefore still pressable.
            if (_gloss != null) _gloss.enabled = _interactable;
        }
    }

    /// <summary>The same two states for a round button. A disc has no lip to shrink, so its press
    /// is the whole face dropping onto the darker plate rather than a drop plus a swap.</summary>
    public sealed class EmberforgeDiscSkin : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
                                             IPointerExitHandler
    {
        private Image _disc;
        private RectTransform _face;
        private bool _interactable = true;
        private bool _down;

        public void Bind(Image disc, RectTransform face)
        {
            _disc = disc;
            _face = face;
        }

        public void SetInteractable(bool value)
        {
            _interactable = value;
            if (!value) _down = false;
            Apply();
            if (_face != null)
            {
                CanvasGroup group = _face.GetComponent<CanvasGroup>();
                if (group == null) group = _face.gameObject.AddComponent<CanvasGroup>();
                group.alpha = value ? 1f : HubStyle.DimAlpha;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_interactable) return;
            _down = true;
            Apply();
        }

        public void OnPointerUp(PointerEventData eventData) { _down = false; Apply(); }
        public void OnPointerExit(PointerEventData eventData) { _down = false; Apply(); }

        private void Apply()
        {
            if (_disc == null) return;
            _disc.sprite = EmberforgeSkin.Get(_down
                ? EmberforgeArt.Slot.DiscDown : EmberforgeArt.Slot.Disc);
            if (_face != null)
                _face.localScale = _down ? new Vector3(0.94f, 0.94f, 1f) : Vector3.one;
        }
    }
}
