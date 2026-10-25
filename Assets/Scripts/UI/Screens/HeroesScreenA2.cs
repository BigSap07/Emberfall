using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// THE A2 HEROES SCREEN (R57): the collection drawn to the A2 boards. A round back disc and the word HEROES
    /// on the left, the shared top bar slimmed to Gold and Shards on the right, one cream panel under them with
    /// a filter row, a tan well of tiles and a detail card carrying the one OPEN HERO. Built only while
    /// HeroesA2Switch is on; the pre R57 screen is the other half of this partial class and is left untouched.
    ///
    /// NOTHING HERE DECIDES A RULE. Who the player owns, a hero's level and its power come from PlayerProfile and
    /// StatMath exactly as the old screen read them, and the order, the count and the words come from the pure
    /// HeroesPick helpers. The tile and the crop are the Formation ones (FormationScreen.BuildCrop, RoleGlyph).
    ///
    /// THE SAME NODE NAMES. The onboarding coach finds "Header/Back" and the first owned hero as "Card0"; the
    /// A2 build keeps both, and adds "OpenHero", which the coach prefers once it exists.
    /// </summary>
    public sealed partial class HeroesScreen
    {
        // ------------------------------------------------------------------ constants

        private const float A2HeaderHeight = 108f;
        private const float A2HeaderTop = 12f;              // puts the header's middle on the pills' middle (TopBarHeight / 2)
        private const float A2BottomMargin = 32f;
        private const float A2PanelPad = 20f;
        private const float A2FilterRowHeight = 84f;
        private const float A2SortWidth = 270f;
        private const float A2SortHeight = 64f;
        private const float A2CardWidth = 560f;
        private const float A2ColumnGap = 16f;
        private const float A2MinTile = 190f;
        private const float A2TileGap = 10f;
        private const float A2TileAspect = 148f / 118f;     // tile height over width, as in Formation
        private const float A2OpenHeight = 96f;             // twice the 48 touch floor; the card must also fit a wide screen, where the canvas scales up and the card is short
        private const string A2LockedPrefix = "L:";

        // ------------------------------------------------------------------ state

        /// <summary>Which half of this partial class built the screen (decided once, at Build).</summary>
        private bool _a2;

        /// <summary>The aspect filter: -1 for ALL, otherwise the Aspect ordinal.</summary>
        private int _a2Filter = -1;

        /// <summary>One of the HeroesPick sort modes.</summary>
        private int _a2Sort = HeroesPick.SortPower;

        /// <summary>The selected tile: an owned hero's uid, or "L:" and a locked hero's definition id. Empty for none.</summary>
        private string _a2Selected = "";

        private RectTransform _a2FilterRow;
        private Text _a2Count;
        private UIButton _a2SortButton;
        private ScrollRect _a2Scroll;
        private RectTransform _a2Grid;
        private RectTransform _a2Detail;
        private readonly Image[] _a2FilterFace = new Image[7];
        private readonly List<A2HeroTile> _a2Tiles = new List<A2HeroTile>();

        private sealed class A2HeroTile
        {
            public string Key;
            public HeroInstance Hero;
            public HeroDef Def;
            public RectTransform Body;
            public GameObject Ring;
        }

        private sealed class A2Entry
        {
            public HeroInstance Hero;
            public HeroDef Def;
            public HeroSortKey Key;
        }

        /// <summary>Flags the capture tooling and the harness read: this screen drew the A2 half.</summary>
        public bool IsA2 { get { return _a2; } }

        /// <summary>The shared top bar stays, slimmed to Gold and Shards (HudTopBar.SetSlim), only while this half is on.</summary>
        public override bool SlimTopBar { get { return _a2; } }

        // ------------------------------------------------------------------ build

        private void BuildA2()
        {
            _a2 = true;
            _a2Tiles.Clear();

            A2Kit.Backdrop(Root);
            BuildA2Header();

            RectTransform main = UIBuilder.Node("Main", Root);
            main.anchorMin = Vector2.zero;
            main.anchorMax = Vector2.one;
            main.offsetMin = new Vector2(UITheme.ScreenPadding, A2BottomMargin);
            main.offsetMax = new Vector2(-UITheme.ScreenPadding, -UITheme.TopBarHeight);
            Image face = A2Kit.ChunkPanel("Panel", main, 26);
            UIBuilder.FillParent(face.rectTransform.parent as RectTransform, 0f);

            RectTransform inner = UIBuilder.Node("Inner", main);
            UIBuilder.FillParent(inner, A2PanelPad);
            BuildA2Top(inner);
            BuildA2Body(inner);
        }

        private void BuildA2Header()
        {
            RectTransform header = UIBuilder.Node("Header", Root);
            UIBuilder.TopStrip(header, A2HeaderHeight, A2HeaderTop, UITheme.ScreenPadding);

            // Back: a cream round disc. The disc is smaller than the hit, so the hit stays a full touch target.
            RectTransform back = UIBuilder.Node("Back", header);
            UIBuilder.Box(back, 0f, 0f, HubStyle.TouchMin, HubStyle.TouchMin, new Vector2(0f, 0.5f));
            Image disc = UIBuilder.Img("Disc", back, TextureLab.Circle(128), A2Kit.Cream);
            UIBuilder.FillParent(disc.rectTransform, 8f);
            disc.raycastTarget = true;
            Image edge = UIBuilder.Img("Edge", back, TextureLab.Ring(128, 5), A2Kit.Ink);
            UIBuilder.FillParent(edge.rectTransform, 8f);
            edge.raycastTarget = false;
            Text chevron = A2Kit.Label("Glyph", back, "<", 52, A2Kit.Ink, TextAnchor.MiddleCenter, A2Kit.Display);
            UIBuilder.FillParent(chevron.rectTransform, 0f);
            Button button = back.gameObject.AddComponent<Button>();
            button.targetGraphic = disc;
            button.transition = Selectable.Transition.None;
            UIButton backHandle = new UIButton();
            backHandle.Root = back;
            backHandle.Button = button;
            backHandle.Background = disc;
            backHandle.Label = chevron;
            backHandle.OnClick(delegate { App.Audio.Tap(); Router.Back(); });

            Text title = A2Kit.Chunky("Title", header, "HEROES", 56, A2Kit.TextOnDark, TextAnchor.MiddleLeft);
            UIBuilder.Outlined(title, A2Kit.InkDeep, 3f);
            UIBuilder.Box(title.rectTransform, HubStyle.TouchMin + 18f, 0f, 560f, A2HeaderHeight, new Vector2(0f, 0.5f));
        }

        /// <summary>The one top row: the aspect filter on the left, the count and the sort on the right. No second row.</summary>
        private void BuildA2Top(RectTransform inner)
        {
            _a2FilterRow = UIBuilder.Node("Filters", inner);
            _a2FilterRow.anchorMin = new Vector2(0f, 1f);
            _a2FilterRow.anchorMax = new Vector2(1f, 1f);
            _a2FilterRow.pivot = new Vector2(0.5f, 1f);
            _a2FilterRow.offsetMin = new Vector2(0f, -A2FilterRowHeight);
            _a2FilterRow.offsetMax = new Vector2(0f, 0f);

            for (int i = 0; i < 7; i++) BuildA2FilterToggle(_a2FilterRow, i, i - 1);

            _a2SortButton = A2Kit.Button("Sort", _a2FilterRow, "SORT: POWER", 24, A2ButtonKind.Quiet);
            UIBuilder.Box(_a2SortButton.Root, 0f, 0f, A2SortWidth, A2SortHeight, new Vector2(1f, 0.5f));
            _a2SortButton.OnClick(OnSortA2);

            _a2Count = A2Kit.Label("Count", _a2FilterRow, "", 30, A2Kit.Ink, TextAnchor.MiddleRight, A2Kit.BodyBold);
            UIBuilder.Box(_a2Count.rectTransform, -(A2SortWidth + 20f), 0f, 220f, 44f, new Vector2(1f, 0.5f));
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

        /// <summary>The tan well of tiles on the left, the detail card on the right, both under the top row.</summary>
        private void BuildA2Body(RectTransform inner)
        {
            RectTransform body = UIBuilder.Node("Body", inner);
            body.anchorMin = Vector2.zero;
            body.anchorMax = Vector2.one;
            body.offsetMin = Vector2.zero;
            body.offsetMax = new Vector2(0f, -(A2FilterRowHeight + 12f));

            RectTransform wellHost = UIBuilder.Node("WellHost", body);
            wellHost.anchorMin = Vector2.zero;
            wellHost.anchorMax = Vector2.one;
            wellHost.offsetMin = Vector2.zero;
            wellHost.offsetMax = new Vector2(-(A2CardWidth + A2ColumnGap), 0f);
            Image well = A2Kit.Well("Well", wellHost, 22);
            UIBuilder.FillParent(well.rectTransform, 0f);

            _a2Grid = UIBuilder.ScrollGrid("Grid", wellHost, out _a2Scroll, new Vector2(A2MinTile, A2MinTile * A2TileAspect),
                                           new Vector2(A2TileGap, A2TileGap), HeroesPick.GridColumns,
                                           new RectOffset(10, 10, 12, 12));
            RectTransform scrollRect = _a2Scroll.GetComponent<RectTransform>();
            UIBuilder.FillParent(scrollRect, 10f);
            A2GridFit fit = _a2Grid.gameObject.AddComponent<A2GridFit>();
            fit.Bind(_a2Grid.GetComponent<GridLayoutGroup>(), _a2Scroll.viewport, A2MinTile, A2TileGap, A2TileAspect, 20f);

            _a2Detail = UIBuilder.Node("Detail", body);
            _a2Detail.anchorMin = new Vector2(1f, 0f);
            _a2Detail.anchorMax = new Vector2(1f, 1f);
            _a2Detail.pivot = new Vector2(1f, 0.5f);
            _a2Detail.offsetMin = new Vector2(-A2CardWidth, 0f);
            _a2Detail.offsetMax = new Vector2(0f, 0f);
        }

        // ------------------------------------------------------------------ refresh

        private void OnShowA2()
        {
            RebuildA2Grid();
        }

        /// <summary>
        /// The roster: owned heroes first in the chosen sort, then the heroes not yet summoned, dimmed. The order
        /// never depends on the squad, so a tile does not move when a hero joins or leaves it. The filter hides the rest.
        /// </summary>
        private void RebuildA2Grid()
        {
            for (int i = _a2Grid.childCount - 1; i >= 0; i--) Destroy(_a2Grid.GetChild(i).gameObject);
            _a2Tiles.Clear();

            PlayerProfile profile = App.Session.Profile;
            List<A2Entry> owned = new List<A2Entry>();
            for (int i = 0; i < profile.Heroes.Count; i++)
            {
                HeroInstance h = profile.Heroes[i];
                if (h.Def == null || !SquadPick.PassesFilter(_a2Filter, (int)h.Def.Aspect)) continue;
                A2Entry e = new A2Entry();
                e.Hero = h;
                e.Def = h.Def;
                e.Key = new HeroSortKey { Id = h.Uid, Power = h.Power, Level = h.Level, Stars = h.Def.Stars, Aspect = (int)h.Def.Aspect };
                owned.Add(e);
            }
            int mode = _a2Sort;
            owned.Sort(delegate (A2Entry a, A2Entry b) { return HeroesPick.CompareOwned(mode, a.Key, b.Key); });

            List<A2Entry> locked = new List<A2Entry>();
            List<HeroDef> all = App.Session.Content.Heroes;
            for (int i = 0; i < all.Count; i++)
            {
                if (profile.Owns(all[i].Id)) continue;
                if (!SquadPick.PassesFilter(_a2Filter, (int)all[i].Aspect)) continue;
                A2Entry e = new A2Entry();
                e.Def = all[i];
                e.Key = new HeroSortKey { Id = all[i].Id, Stars = all[i].Stars, Aspect = (int)all[i].Aspect };
                locked.Add(e);
            }
            locked.Sort(delegate (A2Entry a, A2Entry b) { return HeroesPick.CompareLocked(mode, a.Key, b.Key); });

            int index = 0;
            for (int i = 0; i < owned.Count; i++) BuildA2Tile("Card" + index++, owned[i].Hero, owned[i].Def);
            for (int i = 0; i < locked.Count; i++) BuildA2Tile("Card" + index++, null, locked[i].Def);

            _a2Count.text = HeroesPick.CountText(owned.Count, owned.Count + locked.Count);
            _a2SortButton.Label.text = "SORT: " + HeroesPick.SortLabel(_a2Sort);
            _a2Scroll.verticalNormalizedPosition = 1f;

            if (FindA2Tile(_a2Selected) == null)
                _a2Selected = _a2Tiles.Count > 0 ? _a2Tiles[0].Key : "";

            RefreshA2Filters();
            RefreshA2Tiles();
            RefreshA2Detail();
        }

        private A2HeroTile FindA2Tile(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            for (int i = 0; i < _a2Tiles.Count; i++)
                if (_a2Tiles[i].Key == key) return _a2Tiles[i];
            return null;
        }

        private void RefreshA2Filters()
        {
            for (int i = 0; i < _a2FilterFace.Length; i++)
                _a2FilterFace[i].color = (i - 1) == _a2Filter ? A2Kit.Teal : A2Kit.Card;
        }

        private void BuildA2Tile(string cellName, HeroInstance hero, HeroDef def)
        {
            bool locked = hero == null;
            A2HeroTile view = new A2HeroTile();
            view.Hero = hero;
            view.Def = def;
            view.Key = locked ? A2LockedPrefix + def.Id : hero.Uid;

            RectTransform cell = UIBuilder.Node(cellName, _a2Grid);
            Image hit = UIBuilder.Panel("Hit", cell, new Color(0f, 0f, 0f, 0f), 0);
            UIBuilder.FillParent(hit.rectTransform, 0f);
            hit.raycastTarget = true;
            Button button = cell.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            UIButton handle = new UIButton();
            handle.Root = cell;
            handle.Button = button;
            string key = view.Key;
            handle.OnClick(delegate { OnTileTappedA2(key); });

            RectTransform body = UIBuilder.Node("Body", cell);
            UIBuilder.FillParent(body, 0f);
            view.Body = body;

            // The selected ring: a cream band against the dark edge, then a teal ring outside it.
            RectTransform ringNode = UIBuilder.Node("Ring", body);
            UIBuilder.FillParent(ringNode, 0f);
            Image ringOuter = UIBuilder.Frame("Teal", ringNode, A2Kit.Teal, 18, 6);
            UIBuilder.FillParent(ringOuter.rectTransform, -9f);
            Image ringInner = UIBuilder.Frame("Cream", ringNode, A2Kit.Cream, 15, 5);
            UIBuilder.FillParent(ringInner.rectTransform, -3f);
            view.Ring = ringNode.gameObject;

            Color fill = locked ? A2Kit.CreamWellInset : A2Kit.AspectFill(def.Aspect);
            Image tile = UIBuilder.Panel("Tile", body, fill, 12);
            tile.raycastTarget = false;
            UIBuilder.FillParent(tile.rectTransform, 0f);
            UIBuilder.Frame("Edge", tile.rectTransform, A2Kit.Ink, 12, 4);

            RectTransform window = UIBuilder.Node("Window", body);
            window.anchorMin = new Vector2(0f, 0f);
            window.anchorMax = new Vector2(1f, 1f);
            window.offsetMin = new Vector2(7f, 58f);
            window.offsetMax = new Vector2(-7f, -7f);
            Image windowFill = UIBuilder.Panel("Fill", window, A2Kit.WithAlpha(A2Kit.InkDeep, 0.25f), 8);
            windowFill.raycastTarget = false;
            UIBuilder.FillParent(windowFill.rectTransform, 0f);
            Mask clip = windowFill.gameObject.AddComponent<Mask>();
            clip.showMaskGraphic = true;
            FormationScreen.BuildCrop(windowFill.rectTransform, def, FormationScreen.A2TileCrop, FormationScreen.A2TileFocus, Color.white);
            if (!locked) SkinArt.Dress(windowFill.rectTransform, App.Session, hero);
            Transform badgeNode = windowFill.rectTransform.Find("Badge");
            Image badge = badgeNode != null ? badgeNode.GetComponent<Image>() : null;
            if (locked && badge != null) badge.color = new Color(0.45f, 0.4f, 0.38f, 0.55f);

            // The role badge, top right: a glyph, not a name.
            RectTransform role = UIBuilder.Node("Role", body);
            UIBuilder.Box(role, -10f, -10f, 36f, 36f, new Vector2(1f, 1f));
            Image roleFill = UIBuilder.Panel("Fill", role, A2Kit.Ink, 9);
            roleFill.raycastTarget = false;
            UIBuilder.FillParent(roleFill.rectTransform, 0f);
            Image glyph = UIBuilder.Img("Glyph", role, FormationScreen.RoleGlyph(def.Role, 48), A2Kit.TextOnDark);
            UIBuilder.FillParent(glyph.rectTransform, 6f);

            // The one state mark: a check when in the squad, a padlock when not yet summoned, nothing otherwise.
            int markKind = HeroesPick.Mark(locked, !locked && App.Session.Profile.IsDeployed(hero.Uid));
            if (markKind != HeroesPick.MarkNone)
            {
                RectTransform mark = UIBuilder.Node("Mark", body);
                UIBuilder.Box(mark, -10f, 66f, 40f, 40f, new Vector2(1f, 0f));
                Image markDisc = UIBuilder.Img("Disc", mark, TextureLab.Circle(64), A2Kit.Ink);
                UIBuilder.FillParent(markDisc.rectTransform, 0f);
                Image markGlyph = UIBuilder.Img("Glyph", mark,
                                                markKind == HeroesPick.MarkInSquad ? TextureLab.Check(48) : TextureLab.Padlock(48),
                                                A2Kit.TextOnDark);
                UIBuilder.FillParent(markGlyph.rectTransform, 8f);
            }

            // The band: stars over the level (a hero not yet summoned has no level to show).
            RectTransform band = UIBuilder.Node("Band", body);
            band.anchorMin = new Vector2(0f, 0f);
            band.anchorMax = new Vector2(1f, 0f);
            band.pivot = new Vector2(0.5f, 0f);
            band.offsetMin = new Vector2(7f, 7f);
            band.offsetMax = new Vector2(-7f, 7f);
            band.sizeDelta = new Vector2(band.sizeDelta.x, 50f);
            Image bandFill = UIBuilder.Panel("Fill", band, A2Kit.Ink, 8);
            bandFill.raycastTarget = false;
            UIBuilder.FillParent(bandFill.rectTransform, 0f);
            RectTransform stars = A2Kit.Stars("Stars", band, def.Stars, 16f, 2f);
            UIBuilder.Box(stars, 0f, -4f, 16f * 5 + 8f, 16f, new Vector2(0.5f, 1f));
            if (!locked)
            {
                Text level = A2Kit.Label("Level", band, "LV " + hero.Level, 22, A2Kit.TextOnDark, TextAnchor.LowerCenter, A2Kit.BodyBold);
                UIBuilder.Box(level.rectTransform, 0f, 2f, 104f, 26f, new Vector2(0.5f, 0f));
            }

            _a2Tiles.Add(view);
        }

        /// <summary>Repaints each tile's selected ring (and lifts it a little).</summary>
        private void RefreshA2Tiles()
        {
            for (int i = 0; i < _a2Tiles.Count; i++)
            {
                A2HeroTile t = _a2Tiles[i];
                bool selected = t.Key == _a2Selected;
                t.Ring.SetActive(selected);
                t.Body.anchoredPosition = new Vector2(0f, selected ? 6f : 0f);
            }
        }

        // ------------------------------------------------------------------ detail card

        private void RefreshA2Detail()
        {
            for (int i = _a2Detail.childCount - 1; i >= 0; i--) Destroy(_a2Detail.GetChild(i).gameObject);

            A2HeroTile pick = FindA2Tile(_a2Selected);
            _a2Detail.gameObject.SetActive(pick != null);
            if (pick == null) return;

            HeroDef def = pick.Def;
            HeroInstance hero = pick.Hero;
            bool locked = hero == null;

            Image panel = UIBuilder.Panel("Card", _a2Detail, A2Kit.Card, 22);
            panel.raycastTarget = false;
            UIBuilder.FillParent(panel.rectTransform, 0f);
            UIBuilder.Frame("Edge", panel.rectTransform, A2Kit.Ink, 22, 4);

            // Portrait, framed in the aspect colour.
            RectTransform faceBox = UIBuilder.Node("Face", panel.rectTransform);
            UIBuilder.Box(faceBox, 24f, -24f, 216f, 216f, new Vector2(0f, 1f));
            Image faceOuter = A2Kit.CutPanel("Outer", faceBox, A2Kit.InkDeep, 20, 36);
            UIBuilder.FillParent(faceOuter.rectTransform, 0f);
            Image faceRim = A2Kit.CutPanel("Rim", faceBox, A2Kit.AspectFill(def.Aspect), 18, 32);
            UIBuilder.FillParent(faceRim.rectTransform, 4f);
            Image faceWindow = A2Kit.CutPanel("Window", faceBox, A2Kit.Ink, 14, 26);
            UIBuilder.FillParent(faceWindow.rectTransform, 9f);
            Mask mask = faceWindow.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            FormationScreen.BuildCrop(faceWindow.rectTransform, def, FormationScreen.A2SlotCrop, FormationScreen.A2SlotFocus, Color.white);
            if (!locked) SkinArt.Dress(faceWindow.rectTransform, App.Session, hero);
            else
            {
                Transform art = faceWindow.rectTransform.Find("Badge");
                if (art != null) art.GetComponent<Image>().color = new Color(0.45f, 0.4f, 0.38f, 0.55f);
            }

            // Name, role, aspect, stars and level.
            const float textX = 264f;
            const float textW = A2CardWidth - textX - 24f;
            Text name = A2Kit.Label("Name", panel.rectTransform, def.Name, 44, A2Kit.Ink, TextAnchor.MiddleLeft, A2Kit.Display);
            UIBuilder.BestFit(name, 28, 44);
            UIBuilder.Box(name.rectTransform, textX, -24f, textW, 56f, new Vector2(0f, 1f));

            Image roleGlyph = UIBuilder.Img("RoleGlyph", panel.rectTransform, FormationScreen.RoleGlyph(def.Role, 48), A2Kit.InkSoft);
            UIBuilder.Box(roleGlyph.rectTransform, textX, -96f, 30f, 30f, new Vector2(0f, 1f));
            Text role = A2Kit.Label("Role", panel.rectTransform, def.Role.ToString(), 26, A2Kit.Ink, TextAnchor.MiddleLeft, A2Kit.BodyBold);
            UIBuilder.Box(role.rectTransform, textX + 40f, -92f, textW - 40f, 36f, new Vector2(0f, 1f));

            RectTransform aspectEmblem = A2Kit.Emblem("AspectEmblem", panel.rectTransform, def.Aspect, 30f);
            UIBuilder.Box(aspectEmblem, textX, -138f, 30f, 30f, new Vector2(0f, 1f));
            Text aspect = A2Kit.Label("Aspect", panel.rectTransform, AspectChart.DisplayName(def.Aspect), 26, A2Kit.Ink,
                                      TextAnchor.MiddleLeft, A2Kit.BodyBold);
            UIBuilder.Box(aspect.rectTransform, textX + 40f, -134f, textW - 40f, 36f, new Vector2(0f, 1f));

            RectTransform plate = UIBuilder.Node("Plate", panel.rectTransform);
            UIBuilder.Box(plate, textX, -190f, textW, 50f, new Vector2(0f, 1f));
            Image plateFill = UIBuilder.Panel("Fill", plate, A2Kit.Ink, 12);
            plateFill.raycastTarget = false;
            UIBuilder.FillParent(plateFill.rectTransform, 0f);
            RectTransform plateStars = A2Kit.Stars("Stars", plate, def.Stars, 24f, 2f);
            UIBuilder.Box(plateStars, 12f, 0f, 24f * 5 + 8f, 24f, new Vector2(0f, 0.5f));
            if (!locked)
            {
                Text level = A2Kit.Label("Level", plate, "LV " + hero.Level, 26, A2Kit.TextOnDark, TextAnchor.MiddleRight, A2Kit.BodyBold);
                UIBuilder.Box(level.rectTransform, -12f, 0f, 90f, 40f, new Vector2(1f, 0.5f));
            }

            if (locked) BuildA2LockedBody(panel.rectTransform);
            else BuildA2OwnedBody(panel.rectTransform, hero);

            // The one Primary. A hero not yet summoned shows it disabled.
            UIButton open = A2Kit.Button("OpenHero", panel.rectTransform, "OPEN HERO", 38, A2ButtonKind.Primary);
            UIBuilder.Box(open.Root, 0f, 20f, A2CardWidth - 48f, A2OpenHeight, new Vector2(0.5f, 0f));
            if (locked)
            {
                open.Interactable = false;
            }
            else
            {
                HeroInstance captured = hero;
                open.OnClick(delegate { OpenDetailA2(captured); });
            }
        }

        private void BuildA2OwnedBody(RectTransform panel, HeroInstance hero)
        {
            UnitStats stats = hero.Stats;

            Text powerLabel = A2Kit.Label("PowerLabel", panel, "Power", 26, A2Kit.InkSoft, TextAnchor.LowerLeft, A2Kit.BodyBold);
            UIBuilder.Box(powerLabel.rectTransform, 28f, -250f, 240f, 30f, new Vector2(0f, 1f));
            Text power = A2Kit.Label("Power", panel, hero.Power.ToString("N0", CultureInfo.InvariantCulture), 64, A2Kit.Ink,
                                     TextAnchor.UpperLeft, A2Kit.Display);
            UIBuilder.Box(power.rectTransform, 28f, -280f, A2CardWidth - 56f, 72f, new Vector2(0f, 1f));

            // At most four stat rows: what a fight reads first.
            BuildA2StatRow(panel, 0, "Health", stats.MaxHp);
            BuildA2StatRow(panel, 1, "Attack", stats.Attack);
            BuildA2StatRow(panel, 2, "Defense", stats.Defense);
            BuildA2StatRow(panel, 3, "Speed", stats.Speed);
        }

        private static void BuildA2StatRow(RectTransform panel, int row, string label, float value)
        {
            float y = -360f - row * 42f;
            Image rule = UIBuilder.Panel("Rule" + row, panel, A2Kit.WithAlpha(A2Kit.Ink, 0.18f), 0);
            rule.raycastTarget = false;
            UIBuilder.Box(rule.rectTransform, 0f, y + 2f, A2CardWidth - 56f, 2f, new Vector2(0.5f, 1f));
            Text name = A2Kit.Label("StatName" + row, panel, label, 26, A2Kit.InkSoft, TextAnchor.MiddleLeft, A2Kit.BodyBold);
            UIBuilder.Box(name.rectTransform, 28f, y - 2f, 240f, 38f, new Vector2(0f, 1f));
            Text figure = A2Kit.Label("StatValue" + row, panel, Mathf.RoundToInt(value).ToString("N0", CultureInfo.InvariantCulture), 30,
                                      A2Kit.Ink, TextAnchor.MiddleRight, A2Kit.Display);
            UIBuilder.Box(figure.rectTransform, -28f, y - 2f, 220f, 38f, new Vector2(1f, 1f));
        }

        private static void BuildA2LockedBody(RectTransform panel)
        {
            Text line = A2Kit.Label("LockedLine", panel, HeroesPick.LockedLine, 28, A2Kit.Ink, TextAnchor.UpperLeft, A2Kit.BodyBold);
            line.horizontalOverflow = HorizontalWrapMode.Wrap;
            line.verticalOverflow = VerticalWrapMode.Truncate;
            UIBuilder.Box(line.rectTransform, 28f, -276f, A2CardWidth - 56f, 120f, new Vector2(0f, 1f));
        }

        // ------------------------------------------------------------------ interactions

        /// <summary>The first tap selects. A second tap on the same tile does nothing: only OPEN HERO opens a hero.</summary>
        private void OnTileTappedA2(string key)
        {
            App.Audio.Tap();
            if (key == _a2Selected) return;
            _a2Selected = key;
            RefreshA2Tiles();
            RefreshA2Detail();
        }

        private void OpenDetailA2(HeroInstance hero)
        {
            App.Audio.Tap();
            Router.HeroDetail.SetHero(hero);
            Router.Show(ScreenId.HeroDetail);
        }

        private void OnFilterA2(int aspect)
        {
            App.Audio.Tap();
            _a2Filter = aspect == _a2Filter ? -1 : aspect;
            RebuildA2Grid();
        }

        private void OnSortA2()
        {
            App.Audio.Tap();
            _a2Sort = HeroesPick.NextSort(_a2Sort);
            RebuildA2Grid();
        }

        // ------------------------------------------------------------------ capture hook

        /// <summary>
        /// For the capture tooling only: sets the aspect filter (-1 for ALL) and the selection, then redraws.
        /// <paramref name="select"/> is "" for the default (the first tile), "locked" for the first hero not yet
        /// summoned among the tiles shown, or an owned hero's uid. Not called by anything at run time.
        /// </summary>
        public void ApplyCaptureState(int filterAspect, string select)
        {
            if (!_a2) return;
            _a2Filter = filterAspect;
            _a2Sort = HeroesPick.SortPower;
            _a2Selected = "";
            RebuildA2Grid();
            if (select == "locked")
            {
                for (int i = 0; i < _a2Tiles.Count; i++)
                    if (_a2Tiles[i].Hero == null) { _a2Selected = _a2Tiles[i].Key; break; }
            }
            else if (!string.IsNullOrEmpty(select) && FindA2Tile(select) != null)
            {
                _a2Selected = select;
            }
            RefreshA2Tiles();
            RefreshA2Detail();
            if (select == "locked") ScrollToSelectedA2();
        }

        /// <summary>For the capture tooling only: brings the selected tile into the viewport, one row below the top.</summary>
        private void ScrollToSelectedA2()
        {
            Canvas.ForceUpdateCanvases();
            GridLayoutGroup grid = _a2Grid.GetComponent<GridLayoutGroup>();
            int at = -1;
            for (int i = 0; i < _a2Tiles.Count; i++)
                if (_a2Tiles[i].Key == _a2Selected) { at = i; break; }
            if (at < 0 || grid == null) return;
            int row = at / Mathf.Max(1, grid.constraintCount);
            float pitch = grid.cellSize.y + grid.spacing.y;
            float range = _a2Grid.rect.height - _a2Scroll.viewport.rect.height;
            if (range <= 1f) return;
            float top = Mathf.Max(0f, (row - 1) * pitch);
            _a2Scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(top / range);
        }

        /// <summary>For the capture tooling only: scrolls the roster of either half (1 is the top, 0 the bottom).</summary>
        public void ScrollForCapture(float normalized)
        {
            ScrollRect scroll = (_a2 ? _a2Grid : _grid).GetComponentInParent<ScrollRect>();
            if (scroll != null) scroll.verticalNormalizedPosition = normalized;
        }

        /// <summary>For the capture tooling only: the uid of the strongest owned hero shown, or empty.</summary>
        public string FirstOwnedUid()
        {
            for (int i = 0; i < _a2Tiles.Count; i++)
                if (_a2Tiles[i].Hero != null) return _a2Tiles[i].Hero.Uid;
            return "";
        }
    }

    /// <summary>
    /// Fits a tile grid to the width it has: as many columns as hold tiles at least MinTile wide, and the tile
    /// width that fills the row exactly, at a fixed height to width ratio. Runs in LateUpdate so it follows the
    /// viewport after layout, whatever the screen shape (six columns at 1920 wide, more on a wider screen).
    /// </summary>
    public sealed class A2GridFit : MonoBehaviour
    {
        private GridLayoutGroup _grid;
        private RectTransform _view;
        private float _minTile;
        private float _gap;
        private float _aspect;
        private float _sidePad;
        private float _lastWidth = -1f;

        public void Bind(GridLayoutGroup grid, RectTransform view, float minTile, float gap, float aspect, float sidePad)
        {
            _grid = grid;
            _view = view;
            _minTile = minTile;
            _gap = gap;
            _aspect = aspect;
            _sidePad = sidePad;
            Apply();
        }

        private void LateUpdate()
        {
            Apply();
        }

        private void Apply()
        {
            if (_grid == null || _view == null) return;
            float width = _view.rect.width - _sidePad;
            if (width < 1f || Mathf.Abs(width - _lastWidth) < 0.5f) return;
            _lastWidth = width;
            int columns = HeroesPick.ColumnsFor(width, _minTile, _gap);
            float cell = Mathf.Floor((width - (columns - 1) * _gap) / columns);
            _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _grid.constraintCount = columns;
            _grid.cellSize = new Vector2(cell, Mathf.Round(cell * _aspect));
        }
    }
}
