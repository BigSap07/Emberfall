using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// THE A2 FORMATION SCREEN (R55): the squad and the roster drawn to the A2 boards (cream panels, a squad
    /// well with a back and a front column, a tile roster, a detail card, a bottom bar with one ember
    /// CONFIRM). Built only while FormationA2Switch is on; the pre R55 screen is the other half of this
    /// partial class and is left untouched.
    ///
    /// NOTHING HERE DECIDES A RULE. Every change of who stands where goes through PlayerProfile
    /// (AssignToTeam, ClearTeamSlot, NormaliseTeam), exactly as the old screen did, and what the screen says
    /// about a choice (power change, lit slots, hints) comes from the pure SquadPick helpers.
    ///
    /// THE SAME NODE NAMES. The onboarding coach finds "Header/Back" (the Team step) and "ViewAll" (the
    /// Level Up step), and FeatureLockView finds "GuildTech"; the A2 build keeps all three.
    /// </summary>
    public sealed partial class FormationScreen
    {
        // ------------------------------------------------------------------ constants

        private const float A2HeaderHeight = 108f;
        private const float A2BottomBarHeight = 150f;
        private const float A2PanelGap = 12f;
        private const float A2SplitX = 0.545f;           // left well takes 54.5 percent of the cream panel
        private const float A2DetailHeight = 232f;
        private const float A2FilterRowHeight = 84f;
        private const float A2ToolRowHeight = 64f;
        private const float A2TileWidth = 118f;
        private const float A2TileHeight = 148f;

        // Portrait crops. A badge is a square with a gold ring baked in; a window shows the middle of it, scaled
        // up so the ring falls outside. Scale is the badge size over the window width, Focus is where the face
        // sits down the badge (0 top, 1 bottom). The tile crop is tighter than the board's 185 percent.
        private const float A2SlotCrop = 1.85f;
        private const float A2SlotFocus = 0.40f;
        private const float A2TileCrop = 2.15f;
        private const float A2TileFocus = 0.38f;

        // ------------------------------------------------------------------ state

        /// <summary>Which half of this partial class built the screen (decided once, at Build).</summary>
        private bool _a2;

        /// <summary>The aspect filter: -1 for ALL, otherwise the Aspect ordinal.</summary>
        private int _filter = -1;

        /// <summary>A slot tapped first, waiting for a hero (or a second slot to swap with). -1 for none.</summary>
        private int _selectedSlot = -1;

        private RectTransform _a2LineRail;
        private RectTransform _a2Slots;
        private RectTransform _a2FilterRow;
        private Text _a2Count;
        private RectTransform _a2ToolRow;
        private ScrollRect _a2Scroll;
        private RectTransform _a2Grid;
        private RectTransform _a2Detail;
        private Text _a2PowerValue;
        private Text _a2Hint;
        private UIButton _a2Confirm;
        private UIButton _a2AutoFill;
        private readonly Image[] _a2FilterFace = new Image[7];
        private readonly List<A2Tile> _a2Tiles = new List<A2Tile>();

        /// <summary>One roster tile, kept so a tap repaints its state without rebuilding the grid.</summary>
        private sealed class A2Tile
        {
            public string Uid;
            public HeroDef Locked;
            public RectTransform Body;
            public GameObject Ring;
            public Image Face;
            public GameObject Check;
        }

        /// <summary>Flags the capture tooling and the harness read: this screen drew the A2 half.</summary>
        public bool IsA2 { get { return _a2; } }

        public override bool HidesTopBar { get { return FormationA2Switch.Enabled; } }

        // ------------------------------------------------------------------ build

        private void BuildA2()
        {
            _a2 = true;
            _a2Tiles.Clear();

            A2Kit.Backdrop(Root);
            BuildA2Header();

            // The cream panel that carries the squad (left) and the roster (right).
            RectTransform main = UIBuilder.Node("Main", Root);
            main.anchorMin = Vector2.zero;
            main.anchorMax = Vector2.one;
            main.offsetMin = new Vector2(UITheme.ScreenPadding, A2BottomBarHeight);
            main.offsetMax = new Vector2(-UITheme.ScreenPadding, -(A2HeaderHeight + A2PanelGap));
            Image face = A2Kit.ChunkPanel("Panel", main, 26);
            UIBuilder.FillParent(face.rectTransform.parent as RectTransform, 0f);

            BuildA2Squad(main);
            BuildA2Roster(main);
            BuildA2Seam(main);
            BuildA2BottomBar();
        }

        private void BuildA2Header()
        {
            RectTransform header = UIBuilder.Node("Header", Root);
            UIBuilder.TopStrip(header, A2HeaderHeight, 8f, UITheme.ScreenPadding);

            // Back: a cream rounded square. The visual is smaller than the hit, so the hit stays a full touch target.
            RectTransform back = UIBuilder.Node("Back", header);
            UIBuilder.Box(back, 0f, 0f, HubStyle.TouchMin, HubStyle.TouchMin, new Vector2(0f, 0.5f));
            Image plate = UIBuilder.Panel("Plate", back, A2Kit.Cream, 20);
            UIBuilder.FillParent(plate.rectTransform, 8f);
            plate.raycastTarget = true;
            UIBuilder.Frame("Edge", plate.rectTransform, A2Kit.Ink, 20, 3);
            Text chevron = A2Kit.Label("Glyph", plate.rectTransform, "<", 52, A2Kit.Ink, TextAnchor.MiddleCenter, A2Kit.Display);
            UIBuilder.FillParent(chevron.rectTransform, 0f);
            Button button = back.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            button.transition = Selectable.Transition.None;
            UIButton backHandle = new UIButton();
            backHandle.Root = back;
            backHandle.Button = button;
            backHandle.Background = plate;
            backHandle.Label = chevron;
            backHandle.OnClick(delegate { App.Audio.Tap(); Router.Back(); });

            Text title = A2Kit.Chunky("Title", header, "FORMATION", 56, A2Kit.TextOnDark, TextAnchor.MiddleLeft);
            UIBuilder.Outlined(title, A2Kit.InkDeep, 3f);
            UIBuilder.Box(title.rectTransform, HubStyle.TouchMin + 18f, 0f, 640f, A2HeaderHeight, new Vector2(0f, 0.5f));

            // The saved lines (only a multi line profile draws tabs here).
            _a2LineRail = UIBuilder.Node("Lines", header);
            UIBuilder.Box(_a2LineRail, 0f, 0f, 6 * 168f, 64f, new Vector2(1f, 0.5f));
        }

        // ------------------------------------------------------------------ squad (left)

        private void BuildA2Squad(RectTransform main)
        {
            RectTransform left = UIBuilder.Node("Squad", main);
            left.anchorMin = new Vector2(0f, 0f);
            left.anchorMax = new Vector2(A2SplitX, 1f);
            left.offsetMin = new Vector2(16f, 16f);
            left.offsetMax = new Vector2(-8f, -16f);

            Image well = A2Kit.Well("Well", left, 22);
            UIBuilder.FillParent(well.rectTransform, 0f);

            // Columns: the back rank on the left (three), the front rank beside it (two), the enemy cue beyond.
            RectTransform backChip = A2Chip("BackChip", well.rectTransform, "Back");
            AnchorRegion(backChip, 0.07f, 0.935f, 0.43f, 0.995f);
            RectTransform frontChip = A2Chip("FrontChip", well.rectTransform, "Front");
            AnchorRegion(frontChip, 0.47f, 0.935f, 0.78f, 0.995f);

            RectTransform cue = UIBuilder.Node("EnemyCue", well.rectTransform);
            AnchorRegion(cue, 0.80f, 0.40f, 0.99f, 0.60f);
            Text arrow = A2Kit.Label("Arrow", cue, ">", 64, A2Kit.InkSoft, TextAnchor.MiddleCenter, A2Kit.Display);
            UIBuilder.Box(arrow.rectTransform, 0f, 26f, 100f, 80f, new Vector2(0.5f, 0.5f));
            Text enemy = A2Kit.Label("Word", cue, "Enemy", 26, A2Kit.InkSoft, TextAnchor.MiddleCenter, A2Kit.BodyBold);
            UIBuilder.Box(enemy.rectTransform, 0f, -34f, 160f, 36f, new Vector2(0.5f, 0.5f));

            _a2Slots = UIBuilder.Node("Slots", well.rectTransform);
            UIBuilder.FillParent(_a2Slots, 0f);
        }

        private static RectTransform A2Chip(string name, Transform parent, string text)
        {
            RectTransform chip = UIBuilder.Node(name, parent);
            Image fill = UIBuilder.Panel("Fill", chip, A2Kit.Ink, 14);
            fill.raycastTarget = false;
            UIBuilder.FillParent(fill.rectTransform, 0f);
            Text label = A2Kit.Label("Text", chip, text, 28, A2Kit.TextOnDark, TextAnchor.MiddleCenter, A2Kit.Display);
            UIBuilder.FillParent(label.rectTransform, 0f);
            return chip;
        }

        /// <summary>Anchors a rect to a fractional region of its parent with no offsets.</summary>
        private static void AnchorRegion(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// The cell a game slot occupies in the well, as fractions (x0, y0, x1, y1). The row reads back to
        /// front (SquadPick.VisualOrder): the first three go down the back column, the last two down the front.
        /// The front column is centred against the back one.
        /// </summary>
        private static void SlotRegion(int slot, out float x0, out float y0, out float x1, out float y1)
        {
            int pos = Array.IndexOf(SquadPick.VisualOrder, slot);
            const float top = 0.915f;
            const float bottom = 0.03f;
            float row = (top - bottom) / 3f;
            if (pos < 3)
            {
                x0 = 0.07f; x1 = 0.43f;
                y1 = top - pos * row;
                y0 = y1 - row;
            }
            else
            {
                x0 = 0.47f; x1 = 0.78f;
                y1 = top - row * 0.5f - (pos - 3) * row;
                y0 = y1 - row;
            }
        }

        private void RefreshA2Squad()
        {
            for (int i = _a2Slots.childCount - 1; i >= 0; i--) Destroy(_a2Slots.GetChild(i).gameObject);

            PlayerProfile profile = App.Session.Profile;
            for (int pos = 0; pos < SquadPick.Slots; pos++)
            {
                int slot = SquadPick.VisualOrder[pos];
                float x0, y0, x1, y1;
                SlotRegion(slot, out x0, out y0, out x1, out y1);
                RectTransform cell = UIBuilder.Node("Cell" + slot, _a2Slots);
                AnchorRegion(cell, x0, y0, x1, y1);
                BuildA2Slot(cell, slot, profile.FindHeroByUid(profile.Team[slot]));
            }
        }

        private void BuildA2Slot(RectTransform cell, int slot, HeroInstance hero)
        {
            PlayerProfile profile = App.Session.Profile;

            // The slot keeps one aspect inside its cell, whatever the screen shape.
            RectTransform node = UIBuilder.Node("Slot" + slot, cell);
            UIBuilder.FillParent(node, 4f);
            AspectRatioFitter fit = node.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 0.92f;

            bool lit = SquadPick.IsLit(profile.Team, _selectedUid, slot);
            bool pickedSlot = _selectedSlot == slot;
            bool pickedHero = hero != null && hero.Uid == _selectedUid;

            // The whole slot is the tap target.
            Image hit = UIBuilder.Panel("Hit", node, new Color(0f, 0f, 0f, 0f), 0);
            UIBuilder.FillParent(hit.rectTransform, 0f);
            hit.raycastTarget = true;
            Button tap = node.gameObject.AddComponent<Button>();
            tap.targetGraphic = hit;
            tap.transition = Selectable.Transition.None;
            UIButton tapHandle = new UIButton();
            tapHandle.Root = node;
            tapHandle.Button = tap;
            int captured = slot;
            tapHandle.OnClick(delegate { OnSlotTappedA2(captured); });

            RectTransform art = UIBuilder.Node("Art", node);
            AnchorRegion(art, 0f, 0.20f, 1f, 1f);
            art.offsetMin = new Vector2(0f, 0f);
            art.offsetMax = new Vector2(0f, 0f);

            if (hero == null || hero.Def == null)
            {
                Image dash = UIBuilder.Img("Dashes", art, TextureLab.DashedRoundedOutline(220, 220, 26, 4, 14, 10), A2Kit.Teal);
                UIBuilder.FillParent(dash.rectTransform, 0f);
                dash.type = Image.Type.Simple;
                Image plus = UIBuilder.Img("Plus", art, TextureLab.Plus(96), A2Kit.TealBevel);
                UIBuilder.Box(plus.rectTransform, 0f, 0f, 84f, 84f, new Vector2(0.5f, 0.5f));
            }
            else
            {
                BuildA2SlotHero(art, node, slot, hero);
            }

            // Lit slots and the picked slot or hero wear the teal ring.
            if (lit || pickedSlot || pickedHero)
            {
                Image ring = A2Kit.CutFrame("Lit", art, A2Kit.Teal, 22, 40, pickedSlot || pickedHero ? 7 : 5);
                UIBuilder.FillParent(ring.rectTransform, -6f);
            }

            // The slot number sits on the bottom left corner, as on the board.
            RectTransform number = UIBuilder.Node("Number", node);
            UIBuilder.Box(number, -6f, 6f, 52f, 52f, new Vector2(0f, 0f));
            Image disc = UIBuilder.Img("Disc", number, TextureLab.Circle(64), A2Kit.Ink);
            UIBuilder.FillParent(disc.rectTransform, 0f);
            Text numberText = A2Kit.Label("Text", number, SquadPick.DisplayNumber(slot).ToString(), 30, A2Kit.TextOnDark,
                                          TextAnchor.MiddleCenter, A2Kit.Display);
            UIBuilder.FillParent(numberText.rectTransform, 0f);

            if (hero != null) BuildA2Remove(node, slot);
        }

        private void BuildA2SlotHero(RectTransform art, RectTransform node, int slot, HeroInstance hero)
        {
            HeroDef def = hero.Def;
            Color rim = A2Kit.AspectFill(def.Aspect);

            Image outer = A2Kit.CutPanel("Outer", art, A2Kit.InkDeep, 22, 40);
            UIBuilder.FillParent(outer.rectTransform, 0f);
            Image mid = A2Kit.CutPanel("Rim", art, rim, 20, 36);
            UIBuilder.FillParent(mid.rectTransform, 4f);
            Image window = A2Kit.CutPanel("Window", art, A2Kit.Ink, 16, 30);
            UIBuilder.FillParent(window.rectTransform, 10f);
            Mask mask = window.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            BuildCrop(window.rectTransform, def, A2SlotCrop, A2SlotFocus, Color.white);
            SkinArt.Dress(window.rectTransform, App.Session, hero);

            // The aspect seat in the cut corner.
            RectTransform seat = UIBuilder.Node("Seat", art);
            UIBuilder.Box(seat, -6f, 6f, 54f, 54f, new Vector2(0f, 1f));
            Image seatDisc = UIBuilder.Img("Disc", seat, TextureLab.Circle(64), A2Kit.Cream);
            UIBuilder.FillParent(seatDisc.rectTransform, 0f);
            RectTransform emblem = A2Kit.Emblem("Emblem", seat, def.Aspect, 40f);
            UIBuilder.Box(emblem, 0f, 0f, 40f, 40f, new Vector2(0.5f, 0.5f));

            // The band: stars and level, overlapping the bottom of the frame.
            RectTransform band = UIBuilder.Node("Band", node);
            band.anchorMin = new Vector2(0.12f, 0f);
            band.anchorMax = new Vector2(0.98f, 0f);
            band.pivot = new Vector2(0.5f, 0f);
            band.offsetMin = new Vector2(0f, 0f);
            band.offsetMax = new Vector2(0f, 0f);
            band.sizeDelta = new Vector2(band.sizeDelta.x, 74f);
            band.anchoredPosition = new Vector2(0f, 0f);
            Image bandFill = UIBuilder.Panel("Fill", band, A2Kit.Ink, 16);
            bandFill.raycastTarget = false;
            UIBuilder.FillParent(bandFill.rectTransform, 0f);
            RectTransform stars = A2Stars("Stars", band, def.Stars, 22f);
            UIBuilder.Box(stars, 0f, -8f, 22f * 5 + 12f, 22f, new Vector2(0.5f, 1f));
            Text level = A2Kit.Label("Level", band, "LV " + hero.Level, 24, A2Kit.TextOnDark, TextAnchor.MiddleCenter, A2Kit.BodyBold);
            UIBuilder.Box(level.rectTransform, 0f, 6f, 200f, 28f, new Vector2(0.5f, 0f));
        }

        /// <summary>The remove control: a small dark disc with a cross, overhanging the top right corner, with a larger hit.</summary>
        private void BuildA2Remove(RectTransform node, int slot)
        {
            RectTransform hitRect = UIBuilder.Node("Remove", node);
            UIBuilder.Box(hitRect, 30f, 30f, 96f, 96f, new Vector2(1f, 1f));
            Image hit = UIBuilder.Panel("Hit", hitRect, new Color(0f, 0f, 0f, 0f), 0);
            UIBuilder.FillParent(hit.rectTransform, 0f);
            hit.raycastTarget = true;

            Image disc = UIBuilder.Img("Disc", hitRect, TextureLab.Circle(96), A2Kit.InkDeep);
            UIBuilder.Box(disc.rectTransform, 0f, 0f, 60f, 60f, new Vector2(0.5f, 0.5f));
            Image rim = UIBuilder.Img("Rim", hitRect, TextureLab.Ring(96, 4), A2Kit.Cream);
            UIBuilder.Box(rim.rectTransform, 0f, 0f, 60f, 60f, new Vector2(0.5f, 0.5f));
            Image cross = UIBuilder.Img("Cross", hitRect, TextureLab.Cross(48), A2Kit.TextOnDark);
            UIBuilder.Box(cross.rectTransform, 0f, 0f, 36f, 36f, new Vector2(0.5f, 0.5f));

            Button button = hitRect.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            UIButton handle = new UIButton();
            handle.Root = hitRect;
            handle.Button = button;
            int captured = slot;
            handle.OnClick(delegate { OnRemoveA2(captured); });
        }

        /// <summary>
        /// A hero's badge scaled up inside a clipping window so the face fills it and the baked ring falls
        /// outside. The Image is named Badge so SkinArt.Dress still dresses it. A hero with no declared badge
        /// gets its initial on the window.
        /// </summary>
        private static void BuildCrop(RectTransform window, UnitDef def, float crop, float focus, Color tint)
        {
            Sprite sprite = Widgets.DeclaredBadge(def.Id, def.Name, def.Visual);
            if (sprite == null)
            {
                string initial = string.IsNullOrEmpty(def.Name) ? "?" : def.Name.Substring(0, 1).ToUpperInvariant();
                Text letter = A2Kit.Label("Initial", window, initial, 64, A2Kit.TextOnDark, TextAnchor.MiddleCenter, A2Kit.Display);
                UIBuilder.FillParent(letter.rectTransform, 0f);
                return;
            }

            Image badge = UIBuilder.Img("Badge", window, sprite, tint);
            RectTransform rt = badge.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            // Sized from the window at layout time: the window is stretched, so the badge tracks its width.
            rt.sizeDelta = new Vector2(1f, 1f);
            BadgeCrop tracker = rt.gameObject.AddComponent<BadgeCrop>();
            tracker.Bind(window, crop, focus);
        }

        // ------------------------------------------------------------------ roster (right)

        private void BuildA2Roster(RectTransform main)
        {
            RectTransform right = UIBuilder.Node("Roster", main);
            right.anchorMin = new Vector2(A2SplitX, 0f);
            right.anchorMax = new Vector2(1f, 1f);
            right.offsetMin = new Vector2(8f, 16f);
            right.offsetMax = new Vector2(-18f, -16f);

            // Row 1: ALL and the six aspects, then the count.
            _a2FilterRow = UIBuilder.Node("Filters", right);
            _a2FilterRow.anchorMin = new Vector2(0f, 1f);
            _a2FilterRow.anchorMax = new Vector2(1f, 1f);
            _a2FilterRow.pivot = new Vector2(0.5f, 1f);
            _a2FilterRow.offsetMin = new Vector2(0f, -A2FilterRowHeight);
            _a2FilterRow.offsetMax = new Vector2(0f, 0f);

            for (int i = 0; i < 7; i++)
            {
                int aspect = i - 1;
                BuildA2FilterToggle(_a2FilterRow, i, aspect);
            }
            _a2Count = A2Kit.Label("Count", _a2FilterRow, "", 26, A2Kit.Ink, TextAnchor.MiddleRight, A2Kit.BodyBold);
            _a2Count.rectTransform.anchorMin = new Vector2(1f, 0.5f);
            _a2Count.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            _a2Count.rectTransform.pivot = new Vector2(1f, 0.5f);
            _a2Count.rectTransform.sizeDelta = new Vector2(190f, 40f);
            _a2Count.rectTransform.anchoredPosition = new Vector2(0f, 0f);

            // Row 2: the two quiet doors (GUILD TECH, VIEW ALL), right aligned.
            _a2ToolRow = UIBuilder.Node("Tools", right);
            _a2ToolRow.anchorMin = new Vector2(0f, 1f);
            _a2ToolRow.anchorMax = new Vector2(1f, 1f);
            _a2ToolRow.pivot = new Vector2(0.5f, 1f);
            _a2ToolRow.offsetMin = new Vector2(0f, -(A2FilterRowHeight + 8f + A2ToolRowHeight));
            _a2ToolRow.offsetMax = new Vector2(0f, -(A2FilterRowHeight + 8f));
            BuildA2Tools(_a2ToolRow);

            // The grid fills what is left; the detail card takes the bottom while a hero is selected.
            _a2Grid = UIBuilder.ScrollGrid("Grid", right, out _a2Scroll, new Vector2(A2TileWidth, A2TileHeight),
                                           new Vector2(8f, 10f), 6, new RectOffset(0, 0, 8, 8));
            RectTransform scrollRect = _a2Scroll.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(0f, A2DetailHeight + 12f);
            scrollRect.offsetMax = new Vector2(0f, -(A2FilterRowHeight + 8f + A2ToolRowHeight + 6f));

            _a2Detail = UIBuilder.Node("Detail", right);
            _a2Detail.anchorMin = new Vector2(0f, 0f);
            _a2Detail.anchorMax = new Vector2(1f, 0f);
            _a2Detail.pivot = new Vector2(0.5f, 0f);
            _a2Detail.offsetMin = new Vector2(0f, 0f);
            _a2Detail.offsetMax = new Vector2(0f, A2DetailHeight);
        }

        private void BuildA2FilterToggle(RectTransform row, int index, int aspect)
        {
            RectTransform node = UIBuilder.Node(index == 0 ? "FilterAll" : "Filter" + (Aspect)aspect, row);
            UIBuilder.Box(node, index * 84f, 0f, 80f, 80f, new Vector2(0f, 0.5f));

            Image disc = UIBuilder.Img("Disc", node, TextureLab.Circle(96), A2Kit.Card);
            UIBuilder.Box(disc.rectTransform, 0f, 0f, 68f, 68f, new Vector2(0.5f, 0.5f));
            disc.raycastTarget = true;
            Image edge = UIBuilder.Img("Edge", node, TextureLab.Ring(96, 4), A2Kit.Ink);
            UIBuilder.Box(edge.rectTransform, 0f, 0f, 68f, 68f, new Vector2(0.5f, 0.5f));
            _a2FilterFace[index] = disc;

            if (index == 0)
            {
                Text all = A2Kit.Label("All", node, "All", 26, A2Kit.Ink, TextAnchor.MiddleCenter, A2Kit.Display);
                UIBuilder.FillParent(all.rectTransform, 0f);
            }
            else
            {
                RectTransform emblem = A2Kit.Emblem("Emblem", node, (Aspect)aspect, 42f);
                UIBuilder.Box(emblem, 0f, 0f, 42f, 42f, new Vector2(0.5f, 0.5f));
            }

            Button button = node.gameObject.AddComponent<Button>();
            button.targetGraphic = disc;
            button.transition = Selectable.Transition.None;
            UIButton handle = new UIButton();
            handle.Root = node;
            handle.Button = button;
            int captured = aspect;
            handle.OnClick(delegate { OnFilterA2(captured); });
        }

        private void BuildA2Tools(RectTransform row)
        {
            // The collection survives as its own screen and is reached from here (see HeroesScreen).
            UIButton viewAll = A2Kit.Button("ViewAll", row, "VIEW ALL", 24, A2ButtonKind.Quiet);
            UIBuilder.Box(viewAll.Root, 0f, 0f, 180f, A2ToolRowHeight, new Vector2(1f, 0.5f));
            viewAll.OnClick(OpenCollection);

            // The guild tech tree grants the lines the header tabs switch between; its door stays on this screen.
            UIButton path = A2Kit.Button("GuildTech", row, "GUILD TECH", 24, A2ButtonKind.Quiet);
            UIBuilder.Box(path.Root, -192f, 0f, 220f, A2ToolRowHeight, new Vector2(1f, 0.5f));
            path.OnClick(delegate
            {
                if (!FeatureGates.TryEnter(App, "formation.GuildTech")) return;
                App.Audio.Tap();
                Router.Show(ScreenId.GuildTech);
            });
            FeatureLockView.Attach(path.Root, "formation.GuildTech", path.Label, new Vector2(-100f, 0f),
                                   FeatureLockView.TagSide.Below);
        }

        private void BuildA2Seam(RectTransform main)
        {
            // One Cinder Seam: a faint crack along the bottom rim of the panel, clear of every control and word.
            Image seam = UIBuilder.Img("Seam", main, TextureLab.Seam(320, 24), A2Kit.WithAlpha(A2Kit.InkSoft, 0.55f));
            seam.type = Image.Type.Simple;
            UIBuilder.Box(seam.rectTransform, -120f, -4f, 480f, 24f, new Vector2(0.5f, 0f));
        }

        // ------------------------------------------------------------------ bottom bar

        private void BuildA2BottomBar()
        {
            RectTransform bar = UIBuilder.Node("Bar", Root);
            UIBuilder.BottomStrip(bar, A2BottomBarHeight, 0f, UITheme.ScreenPadding);

            Text caption = A2Kit.Label("PowerLabel", bar, "Squad power", 26, A2Kit.TextOnDark, TextAnchor.LowerLeft, A2Kit.BodyBold);
            UIBuilder.Box(caption.rectTransform, 6f, -34f, 300f, 34f, new Vector2(0f, 1f));
            UIBuilder.Shadowed(caption, A2Kit.WithAlpha(A2Kit.InkDeep, 0.8f), 2f);
            _a2PowerValue = A2Kit.Label("PowerValue", bar, "0", 66, A2Kit.TextOnDark, TextAnchor.UpperLeft, A2Kit.Display);
            UIBuilder.Box(_a2PowerValue.rectTransform, 6f, 8f, 340f, 84f, new Vector2(0f, 0f));
            UIBuilder.Outlined(_a2PowerValue, A2Kit.InkDeep, 3f);

            _a2Hint = A2Kit.Label("Hint", bar, "", 26, A2Kit.TextOnDark, TextAnchor.MiddleCenter, A2Kit.BodyBold);
            _a2Hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            _a2Hint.verticalOverflow = VerticalWrapMode.Truncate;
            _a2Hint.rectTransform.anchorMin = new Vector2(0f, 0f);
            _a2Hint.rectTransform.anchorMax = new Vector2(1f, 1f);
            _a2Hint.rectTransform.offsetMin = new Vector2(380f, 20f);
            _a2Hint.rectTransform.offsetMax = new Vector2(-660f, -20f);
            UIBuilder.Shadowed(_a2Hint, A2Kit.WithAlpha(A2Kit.InkDeep, 0.8f), 2f);

            // AUTO FILL (Secondary) then CONFIRM (the one Primary).
            _a2AutoFill = A2Kit.Button("AutoFill", bar, "AUTO FILL", 32, A2ButtonKind.Secondary);
            UIBuilder.Box(_a2AutoFill.Root, -(360f + 16f), 0f, 280f, HubStyle.TouchMin, new Vector2(1f, 0.5f));
            _a2AutoFill.OnClick(OnAutoFillA2);

            _a2Confirm = A2Kit.Button("Confirm", bar, "CONFIRM", 38, A2ButtonKind.Primary);
            UIBuilder.Box(_a2Confirm.Root, 0f, 0f, 360f, HubStyle.TouchMin, new Vector2(1f, 0.5f));
            _a2Confirm.OnClick(OnConfirmA2);
        }

        // ------------------------------------------------------------------ refresh

        private void OnShowA2()
        {
            FeatureLockView.RefreshAll(App, Root);
            _selectedUid = "";
            _selectedSlot = -1;
            RebuildA2Grid();
            RefreshA2();
        }

        /// <summary>Redraws everything that depends on the squad or the selection. The tile grid is rebuilt only when the filter or the roster changes.</summary>
        private void RefreshA2()
        {
            RefreshA2Lines();
            RefreshA2Squad();
            RefreshA2Tiles();
            RefreshA2Filters();
            RefreshA2Detail();

            PlayerProfile profile = App.Session.Profile;
            _a2PowerValue.text = profile.TeamPower().ToString("N0", CultureInfo.InvariantCulture);
            _a2Hint.text = HintA2();
        }

        private string HintA2()
        {
            PlayerProfile profile = App.Session.Profile;
            if (_selectedSlot >= 0 && string.IsNullOrEmpty(_selectedUid))
            {
                bool filled = profile.FindHeroByUid(profile.Team[_selectedSlot]) != null;
                return filled
                    ? "Tap a hero to put them in slot " + SquadPick.DisplayNumber(_selectedSlot) + ", or tap another slot to swap places."
                    : "Tap a hero to place them in slot " + SquadPick.DisplayNumber(_selectedSlot) + ".";
            }
            return SquadPick.Hint(profile.Team, _selectedUid);
        }

        private void RefreshA2Lines()
        {
            for (int i = _a2LineRail.childCount - 1; i >= 0; i--) Destroy(_a2LineRail.GetChild(i).gameObject);
            int lines = App.Session.LinesAllowed;
            _a2LineRail.gameObject.SetActive(lines > 1);
            for (int i = 0; i < lines && i < LineNumerals.Length; i++)
            {
                int index = i;
                bool active = i == App.Session.Profile.ActiveLine;
                UIButton tab = A2Kit.Button("Line" + LineNumerals[i], _a2LineRail, "LINE " + LineNumerals[i], 22,
                                            active ? A2ButtonKind.Secondary : A2ButtonKind.Quiet);
                UIBuilder.Box(tab.Root, i * 168f, 0f, 160f, 64f, new Vector2(0f, 0.5f));
                tab.OnClick(delegate { OnLineA2(index); });
            }
        }

        private void RefreshA2Filters()
        {
            for (int i = 0; i < _a2FilterFace.Length; i++)
                _a2FilterFace[i].color = (i - 1) == _filter ? A2Kit.Teal : A2Kit.Card;
        }

        /// <summary>
        /// The roster, strongest first, then the heroes not yet summoned. The order does NOT depend on who is in
        /// the squad, so a tile never moves under a finger when it is placed. The filter hides the rest.
        /// </summary>
        private void RebuildA2Grid()
        {
            for (int i = _a2Grid.childCount - 1; i >= 0; i--) Destroy(_a2Grid.GetChild(i).gameObject);
            _a2Tiles.Clear();

            PlayerProfile profile = App.Session.Profile;
            List<HeroInstance> owned = new List<HeroInstance>(profile.Heroes);
            owned.Sort(delegate (HeroInstance a, HeroInstance b)
            {
                int byPower = b.Power.CompareTo(a.Power);
                return byPower != 0 ? byPower : string.CompareOrdinal(a.Uid, b.Uid);
            });

            int shown = 0;
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i].Def == null || !SquadPick.PassesFilter(_filter, (int)owned[i].Def.Aspect)) continue;
                BuildA2Tile(owned[i], null);
                shown++;
            }
            List<HeroDef> all = App.Session.Content.Heroes;
            for (int i = 0; i < all.Count; i++)
            {
                if (profile.Owns(all[i].Id)) continue;
                if (!SquadPick.PassesFilter(_filter, (int)all[i].Aspect)) continue;
                BuildA2Tile(null, all[i]);
            }
            _a2Count.text = shown + (shown == 1 ? " hero" : " heroes");
            _a2Scroll.verticalNormalizedPosition = 1f;
        }

        private void BuildA2Tile(HeroInstance hero, HeroDef locked)
        {
            HeroDef def = hero != null ? hero.Def : locked;
            A2Tile view = new A2Tile();
            view.Uid = hero != null ? hero.Uid : "";
            view.Locked = locked;

            RectTransform cell = UIBuilder.Node(hero != null ? "Tile_" + hero.Uid : "Locked_" + def.Id, _a2Grid);
            Image hit = UIBuilder.Panel("Hit", cell, new Color(0f, 0f, 0f, 0f), 0);
            UIBuilder.FillParent(hit.rectTransform, 0f);
            hit.raycastTarget = true;
            Button button = cell.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            UIButton handle = new UIButton();
            handle.Root = cell;
            handle.Button = button;
            HeroInstance capturedHero = hero;
            HeroDef capturedDef = locked;
            handle.OnClick(delegate { OnTileTappedA2(capturedHero, capturedDef); });

            RectTransform body = UIBuilder.Node("Body", cell);
            UIBuilder.FillParent(body, 0f);
            view.Body = body;

            // The selected ring: a dark border, then a teal ring outside it.
            Image ring = UIBuilder.Frame("Ring", body, A2Kit.Teal, 16, 6);
            UIBuilder.FillParent(ring.rectTransform, -6f);
            view.Ring = ring.gameObject;

            Color fill = hero != null ? A2Kit.AspectFill(def.Aspect) : A2Kit.CreamWellInset;
            Image tile = UIBuilder.Panel("Tile", body, fill, 12);
            tile.raycastTarget = false;
            UIBuilder.FillParent(tile.rectTransform, 0f);
            UIBuilder.Frame("Edge", tile.rectTransform, A2Kit.Ink, 12, 4);

            RectTransform window = UIBuilder.Node("Window", body);
            window.anchorMin = new Vector2(0f, 0f);
            window.anchorMax = new Vector2(1f, 1f);
            window.offsetMin = new Vector2(7f, 52f);
            window.offsetMax = new Vector2(-7f, -7f);
            Image windowFill = UIBuilder.Panel("Fill", window, A2Kit.WithAlpha(A2Kit.InkDeep, 0.25f), 8);
            windowFill.raycastTarget = false;
            UIBuilder.FillParent(windowFill.rectTransform, 0f);
            Mask clip = windowFill.gameObject.AddComponent<Mask>();
            clip.showMaskGraphic = true;
            BuildCrop(windowFill.rectTransform, def, A2TileCrop, A2TileFocus, Color.white);
            if (hero != null) SkinArt.Dress(windowFill.rectTransform, App.Session, hero);
            Image badge = windowFill.rectTransform.Find("Badge") != null
                ? windowFill.rectTransform.Find("Badge").GetComponent<Image>() : null;
            view.Face = badge;
            if (hero == null && badge != null) badge.color = new Color(0.45f, 0.4f, 0.38f, 0.55f);

            // The role badge, top right.
            RectTransform role = UIBuilder.Node("Role", body);
            UIBuilder.Box(role, -10f, -10f, 36f, 36f, new Vector2(1f, 1f));
            Image roleFill = UIBuilder.Panel("Fill", role, A2Kit.Ink, 9);
            roleFill.raycastTarget = false;
            UIBuilder.FillParent(roleFill.rectTransform, 0f);
            Image glyph = UIBuilder.Img("Glyph", role, RoleGlyph(def.Role, 48), A2Kit.TextOnDark);
            UIBuilder.FillParent(glyph.rectTransform, 6f);

            // The one state mark: a check when in the squad, a padlock when not yet summoned.
            RectTransform mark = UIBuilder.Node("Mark", body);
            UIBuilder.Box(mark, -10f, 60f, 40f, 40f, new Vector2(1f, 0f));
            Image markDisc = UIBuilder.Img("Disc", mark, TextureLab.Circle(64), A2Kit.Ink);
            UIBuilder.FillParent(markDisc.rectTransform, 0f);
            Image markGlyph = UIBuilder.Img("Glyph", mark, hero != null ? TextureLab.Check(48) : TextureLab.Padlock(48), A2Kit.TextOnDark);
            UIBuilder.FillParent(markGlyph.rectTransform, 8f);
            view.Check = mark.gameObject;
            view.Check.SetActive(hero == null);

            // The band: stars over the level.
            RectTransform band = UIBuilder.Node("Band", body);
            band.anchorMin = new Vector2(0f, 0f);
            band.anchorMax = new Vector2(1f, 0f);
            band.pivot = new Vector2(0.5f, 0f);
            band.offsetMin = new Vector2(7f, 7f);
            band.offsetMax = new Vector2(-7f, 7f);
            band.sizeDelta = new Vector2(band.sizeDelta.x, 42f);
            Image bandFill = UIBuilder.Panel("Fill", band, A2Kit.Ink, 8);
            bandFill.raycastTarget = false;
            UIBuilder.FillParent(bandFill.rectTransform, 0f);
            RectTransform stars = A2Stars("Stars", band, def.Stars, 16f);
            UIBuilder.Box(stars, 0f, -3f, 16f * 5 + 8f, 16f, new Vector2(0.5f, 1f));
            if (hero != null)
            {
                Text level = A2Kit.Label("Level", band, "LV " + hero.Level, 22, A2Kit.TextOnDark, TextAnchor.LowerCenter, A2Kit.BodyBold);
                UIBuilder.Box(level.rectTransform, 0f, 1f, 104f, 24f, new Vector2(0.5f, 0f));
            }

            _a2Tiles.Add(view);
        }

        private static Sprite RoleGlyph(HeroRole role, int size)
        {
            switch (role)
            {
                case HeroRole.Bulwark: return TextureLab.Shield(size);
                case HeroRole.Vanguard: return TextureLab.Helm(size);
                case HeroRole.Striker: return TextureLab.Sword(size);
                case HeroRole.Breaker: return TextureLab.Swords(size);
                case HeroRole.Mender: return TextureLab.Heart(size);
                case HeroRole.Warden: return TextureLab.Spark(size);
                case HeroRole.Marksman: return TextureLab.Arrow(size);
                case HeroRole.Stalker: return TextureLab.Boot(size);
                case HeroRole.Binder: return TextureLab.Ring(size, false);
                default: return TextureLab.HourglassIcon(size);
            }
        }

        /// <summary>A row of five stars in the A2 star colours (on and off).</summary>
        private static RectTransform A2Stars(string name, Transform parent, int stars, float size)
        {
            RectTransform root = UIBuilder.Node(name, parent);
            UIBuilder.HStack(root, 2f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
            Sprite star = TextureLab.Star(64);
            for (int i = 0; i < 5; i++)
            {
                Image image = UIBuilder.Img("Star" + i, root, star, i < stars ? A2Kit.StarOn : A2Kit.StarOff);
                UIBuilder.Sized(image.rectTransform, size, size);
            }
            return root;
        }

        /// <summary>Repaints each tile's selected ring and its in squad check (the check dims the face).</summary>
        private void RefreshA2Tiles()
        {
            PlayerProfile profile = App.Session.Profile;
            for (int i = 0; i < _a2Tiles.Count; i++)
            {
                A2Tile t = _a2Tiles[i];
                if (t.Locked != null) continue;
                bool inSquad = profile.IsDeployed(t.Uid);
                bool selected = t.Uid == _selectedUid;
                t.Ring.SetActive(selected);
                t.Body.anchoredPosition = new Vector2(0f, selected ? 6f : 0f);
                t.Check.SetActive(inSquad);
                if (t.Face != null)
                {
                    Color c = t.Face.color;
                    t.Face.color = new Color(c.r, c.g, c.b, inSquad ? 0.45f : 1f);
                }
            }
        }

        // ------------------------------------------------------------------ detail card

        private void RefreshA2Detail()
        {
            for (int i = _a2Detail.childCount - 1; i >= 0; i--) Destroy(_a2Detail.GetChild(i).gameObject);

            PlayerProfile profile = App.Session.Profile;
            HeroInstance hero = string.IsNullOrEmpty(_selectedUid) ? null : profile.FindHeroByUid(_selectedUid);
            bool open = hero != null && hero.Def != null;
            _a2Detail.gameObject.SetActive(open);

            RectTransform scrollRect = _a2Scroll.GetComponent<RectTransform>();
            scrollRect.offsetMin = new Vector2(0f, open ? A2DetailHeight + 12f : 0f);
            if (!open) return;

            HeroDef def = hero.Def;
            Image panel = UIBuilder.Panel("Card", _a2Detail, A2Kit.Card, 22);
            panel.raycastTarget = false;
            UIBuilder.FillParent(panel.rectTransform, 0f);
            UIBuilder.Frame("Edge", panel.rectTransform, A2Kit.Ink, 22, 4);

            // Face, framed in the aspect colour.
            RectTransform faceBox = UIBuilder.Node("Face", panel.rectTransform);
            UIBuilder.Box(faceBox, 22f, 0f, 176f, 176f, new Vector2(0f, 0.5f));
            Image faceOuter = A2Kit.CutPanel("Outer", faceBox, A2Kit.InkDeep, 20, 36);
            UIBuilder.FillParent(faceOuter.rectTransform, 0f);
            Image faceRim = A2Kit.CutPanel("Rim", faceBox, A2Kit.AspectFill(def.Aspect), 18, 32);
            UIBuilder.FillParent(faceRim.rectTransform, 4f);
            Image faceWindow = A2Kit.CutPanel("Window", faceBox, A2Kit.Ink, 14, 26);
            UIBuilder.FillParent(faceWindow.rectTransform, 9f);
            Mask mask = faceWindow.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            BuildCrop(faceWindow.rectTransform, def, A2SlotCrop, A2SlotFocus, Color.white);
            SkinArt.Dress(faceWindow.rectTransform, App.Session, hero);
            RectTransform seat = UIBuilder.Node("Seat", faceBox);
            UIBuilder.Box(seat, -4f, 4f, 48f, 48f, new Vector2(0f, 1f));
            Image seatDisc = UIBuilder.Img("Disc", seat, TextureLab.Circle(64), A2Kit.Cream);
            UIBuilder.FillParent(seatDisc.rectTransform, 0f);
            RectTransform emblem = A2Kit.Emblem("Emblem", seat, def.Aspect, 36f);
            UIBuilder.Box(emblem, 0f, 0f, 36f, 36f, new Vector2(0.5f, 0.5f));

            // Name, role, aspect, stars and level.
            Text name = A2Kit.Label("Name", panel.rectTransform, def.Name, 44, A2Kit.Ink, TextAnchor.MiddleLeft, A2Kit.Display);
            UIBuilder.BestFit(name, 28, 44);
            UIBuilder.Box(name.rectTransform, 218f, -22f, 330f, 54f, new Vector2(0f, 1f));

            Image roleGlyph = UIBuilder.Img("RoleGlyph", panel.rectTransform, RoleGlyph(def.Role, 48), A2Kit.InkSoft);
            UIBuilder.Box(roleGlyph.rectTransform, 218f, -92f, 30f, 30f, new Vector2(0f, 1f));
            Text role = A2Kit.Label("Role", panel.rectTransform, def.Role.ToString(), 26, A2Kit.Ink, TextAnchor.MiddleLeft, A2Kit.BodyBold);
            UIBuilder.Box(role.rectTransform, 258f, -88f, 250f, 36f, new Vector2(0f, 1f));

            RectTransform aspectEmblem = A2Kit.Emblem("AspectEmblem", panel.rectTransform, def.Aspect, 30f);
            UIBuilder.Box(aspectEmblem, 218f, -130f, 30f, 30f, new Vector2(0f, 1f));
            Text aspect = A2Kit.Label("Aspect", panel.rectTransform, AspectChart.DisplayName(def.Aspect), 26, A2Kit.Ink,
                                      TextAnchor.MiddleLeft, A2Kit.BodyBold);
            UIBuilder.Box(aspect.rectTransform, 258f, -126f, 250f, 36f, new Vector2(0f, 1f));

            RectTransform plate = UIBuilder.Node("Plate", panel.rectTransform);
            UIBuilder.Box(plate, 218f, -176f, 220f, 40f, new Vector2(0f, 1f));
            Image plateFill = UIBuilder.Panel("Fill", plate, A2Kit.Ink, 12);
            plateFill.raycastTarget = false;
            UIBuilder.FillParent(plateFill.rectTransform, 0f);
            RectTransform plateStars = A2Stars("Stars", plate, def.Stars, 22f);
            UIBuilder.Box(plateStars, 10f, 0f, 22f * 5 + 8f, 22f, new Vector2(0f, 0.5f));
            Text level = A2Kit.Label("Level", plate, "LV " + hero.Level, 24, A2Kit.TextOnDark, TextAnchor.MiddleRight, A2Kit.BodyBold);
            UIBuilder.Box(level.rectTransform, -10f, 0f, 90f, 36f, new Vector2(1f, 0.5f));

            // The power change if this hero goes where they would go.
            int target = DetailTargetSlot(profile, hero);
            Text label = A2Kit.Label("DeltaLabel", panel.rectTransform, "Squad power", 24, A2Kit.InkSoft, TextAnchor.MiddleRight, A2Kit.BodyBold);
            UIBuilder.Box(label.rectTransform, -24f, -34f, 240f, 32f, new Vector2(1f, 1f));
            if (target < 0)
            {
                Text here = A2Kit.Label("InSquad", panel.rectTransform, "In slot " + SquadPick.DisplayNumber(profile.TeamSlotOf(hero.Uid)),
                                        34, A2Kit.Ink, TextAnchor.MiddleRight, A2Kit.Display);
                UIBuilder.Box(here.rectTransform, -24f, -92f, 240f, 56f, new Vector2(1f, 1f));
            }
            else
            {
                HeroInstance replaced = profile.FindHeroByUid(profile.Team[target]);
                int delta = SquadPick.PowerDelta(hero.Power, replaced != null ? replaced.Power : 0);
                Color color = delta > 0 ? A2Kit.Good : (delta < 0 ? A2Kit.Hex(0xE5694A) : A2Kit.Ink);
                Color stroke = delta > 0 ? A2Kit.GoodStroke : (delta < 0 ? A2Kit.Hex(0x5A1F14) : A2Kit.Card);
                Text value = A2Kit.Label("Delta", panel.rectTransform, SquadPick.DeltaText(delta), 72, color, TextAnchor.MiddleRight, A2Kit.Display);
                UIBuilder.Outlined(value, stroke, 3f);
                UIBuilder.Box(value.rectTransform, -24f, -72f, 250f, 84f, new Vector2(1f, 1f));
                Text note = A2Kit.Label("Note", panel.rectTransform, SquadPick.DeltaNote(target, replaced != null), 22,
                                        A2Kit.InkSoft, TextAnchor.MiddleRight, A2Kit.Body);
                UIBuilder.Box(note.rectTransform, -24f, -162f, 250f, 30f, new Vector2(1f, 1f));
            }

            // Close: clears the selection.
            RectTransform close = UIBuilder.Node("Close", panel.rectTransform);
            UIBuilder.Box(close, 6f, 6f, 80f, 80f, new Vector2(1f, 1f));
            Image closeHit = UIBuilder.Panel("Hit", close, new Color(0f, 0f, 0f, 0f), 0);
            UIBuilder.FillParent(closeHit.rectTransform, 0f);
            closeHit.raycastTarget = true;
            Image cross = UIBuilder.Img("Cross", close, TextureLab.Cross(48), A2Kit.Ink);
            UIBuilder.Box(cross.rectTransform, -4f, -4f, 36f, 36f, new Vector2(0.5f, 0.5f));
            Button closeButton = close.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeHit;
            closeButton.transition = Selectable.Transition.None;
            UIButton closeHandle = new UIButton();
            closeHandle.Root = close;
            closeHandle.Button = closeButton;
            closeHandle.OnClick(delegate { App.Audio.Tap(); _selectedUid = ""; _selectedSlot = -1; RefreshA2(); });
        }

        /// <summary>
        /// Where the detail card measures a hero's power change. A hero already in the squad: nowhere (-1).
        /// A slot picked first: that slot. Otherwise the first empty slot, and on a full squad the weakest
        /// hero's slot, the swap that gains the most.
        /// </summary>
        private int DetailTargetSlot(PlayerProfile profile, HeroInstance hero)
        {
            if (profile.IsDeployed(hero.Uid)) return -1;
            if (_selectedSlot >= 0) return _selectedSlot;
            int empty = SquadPick.FirstEmpty(profile.Team);
            if (empty >= 0) return empty;
            int weakest = 0;
            int weakestPower = int.MaxValue;
            for (int i = 0; i < SquadPick.Slots; i++)
            {
                HeroInstance h = profile.FindHeroByUid(profile.Team[i]);
                int power = h != null ? h.Power : 0;
                if (power < weakestPower) { weakestPower = power; weakest = i; }
            }
            return weakest;
        }

        // ------------------------------------------------------------------ interactions

        private void OnTileTappedA2(HeroInstance hero, HeroDef locked)
        {
            App.Audio.Tap();
            if (hero == null)
            {
                App.Notify((locked != null ? locked.Name : "This hero") + " is not summoned yet.");
                return;
            }

            PlayerProfile profile = App.Session.Profile;

            // A slot was picked first: this hero goes there (a swap if the slot is taken or the hero stands elsewhere).
            if (_selectedSlot >= 0)
            {
                PlaceA2(_selectedSlot, hero.Uid);
                return;
            }

            if (_selectedUid == hero.Uid)
            {
                // The second tap: a hero not in the squad goes to the first empty slot. A full squad waits for a slot tap.
                if (!profile.IsDeployed(hero.Uid))
                {
                    int empty = SquadPick.FirstEmpty(profile.Team);
                    if (empty >= 0) { PlaceA2(empty, hero.Uid); return; }
                    _a2Hint.text = SquadPick.Hint(profile.Team, _selectedUid);
                    return;
                }
                _selectedUid = "";
                RefreshA2();
                return;
            }

            _selectedUid = hero.Uid;
            RefreshA2();
        }

        private void OnSlotTappedA2(int slot)
        {
            App.Audio.Tap();
            PlayerProfile profile = App.Session.Profile;

            // A hero is selected: place or swap it into this slot.
            if (!string.IsNullOrEmpty(_selectedUid))
            {
                if (profile.TeamSlotOf(_selectedUid) == slot) { _selectedUid = ""; RefreshA2(); return; }
                PlaceA2(slot, _selectedUid);
                return;
            }

            // A slot was picked first: a second slot swaps the two.
            if (_selectedSlot >= 0)
            {
                if (_selectedSlot == slot) { _selectedSlot = -1; RefreshA2(); return; }
                string first = profile.Team[_selectedSlot];
                string second = profile.Team[slot];
                if (!string.IsNullOrEmpty(first)) PlaceA2(slot, first);
                else if (!string.IsNullOrEmpty(second)) PlaceA2(_selectedSlot, second);
                else { _selectedSlot = slot; RefreshA2(); }
                return;
            }

            _selectedSlot = slot;
            RefreshA2();
        }

        /// <summary>One placement. AssignToTeam already swaps when the hero stands elsewhere.</summary>
        private void PlaceA2(int slot, string uid)
        {
            PlayerProfile profile = App.Session.Profile;
            HeroInstance hero = profile.FindHeroByUid(uid);
            if (hero == null) { _selectedUid = ""; _selectedSlot = -1; RefreshA2(); return; }

            profile.AssignToTeam(slot, uid);
            _selectedUid = "";
            _selectedSlot = -1;
            App.RefreshHud();
            App.RequestSave();
            RefreshA2();
            App.Notify((hero.Def != null ? hero.Def.Name : "Hero") + " now stands in slot " + SquadPick.DisplayNumber(slot) + ".");
        }

        private void OnRemoveA2(int slot)
        {
            App.Audio.Tap();
            App.Session.Profile.ClearTeamSlot(slot);
            _selectedUid = "";
            _selectedSlot = -1;
            App.RefreshHud();
            App.RequestSave();
            RefreshA2();
        }

        private void OnFilterA2(int aspect)
        {
            App.Audio.Tap();
            _filter = aspect == _filter ? -1 : aspect;
            RebuildA2Grid();
            RefreshA2();
        }

        /// <summary>AUTO FILL: the rule load and onboarding already use. Empty slots take the strongest heroes not in the squad.</summary>
        private void OnAutoFillA2()
        {
            App.Audio.Tap();
            App.Session.Profile.NormaliseTeam();
            _selectedUid = "";
            _selectedSlot = -1;
            App.RefreshHud();
            App.RequestSave();
            RefreshA2();
        }

        /// <summary>CONFIRM: every change already saved when it was made, so this only leaves.</summary>
        private void OnConfirmA2()
        {
            App.Audio.Tap();
            App.RequestSave();
            Router.Back();
        }

        private void OnLineA2(int index)
        {
            if (index == App.Session.Profile.ActiveLine) return;
            App.Audio.Tap();
            _selectedUid = "";
            _selectedSlot = -1;
            if (!App.Session.SwitchLine(index)) return;
            App.SaveNow();
            RebuildA2Grid();
            RefreshA2();
        }

        // ------------------------------------------------------------------ capture hook

        /// <summary>
        /// For the capture tooling only: selects a roster hero (by roster position among the shown tiles) and
        /// optionally sets the aspect filter, then redraws. Not called by anything at run time.
        /// </summary>
        public void ApplyCaptureState(int filterAspect, string selectUid)
        {
            if (!_a2) return;
            _filter = filterAspect;
            _selectedUid = selectUid ?? "";
            _selectedSlot = -1;
            RebuildA2Grid();
            RefreshA2();
        }
    }

    /// <summary>
    /// Sizes and places a badge inside its clipping window: the badge is Crop times the window width, hung from
    /// the top centre of the window and slid up so the face (Focus down the badge) sits at the window's middle.
    /// Runs in LateUpdate so it follows the window after layout, whatever the screen shape.
    /// </summary>
    public sealed class BadgeCrop : MonoBehaviour
    {
        private RectTransform _window;
        private float _crop;
        private float _focus;

        public void Bind(RectTransform window, float crop, float focus)
        {
            _window = window;
            _crop = crop;
            _focus = focus;
            Apply();
        }

        private void LateUpdate()
        {
            Apply();
        }

        private void Apply()
        {
            if (_window == null) return;
            RectTransform self = transform as RectTransform;
            Rect r = _window.rect;
            if (r.width < 1f || r.height < 1f) return;
            float size = r.width * _crop;
            self.sizeDelta = new Vector2(size, size);
            // The face centre is Focus down the badge; put it at the window's vertical middle.
            self.anchoredPosition = new Vector2(0f, size * _focus - r.height * 0.5f);
        }
    }
}
