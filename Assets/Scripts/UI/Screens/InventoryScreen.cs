using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;
using Emberfall.Core.Progression;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// Everything the player owns, in one grid.
    ///
    /// Relics and armor share the grid rather than living in separate stacked sections: they are
    /// both "a thing you own and put on a hero", the decision to equip one is made against the
    /// other, and a player looking for their best Weapon should not have to know which of two
    /// systems it belongs to first. The category rail narrows the grid; it does not change what
    /// kind of screen this is.
    ///
    /// Tapping a card selects it. The detail panel states what it is and what it does, and carries
    /// the one action - EQUIP, which opens the same hero picker this screen has always used. No
    /// aspect restriction of any kind, and none by grade either: a hero wears one relic, legendary
    /// or mythic, not one of each.
    ///
    /// Reroll, Salvage and Combine live on their own screen, the Forge (ScreenId.Forge) - they need
    /// no hero in context, so they belong to a pool-wide hub rather than to a per-item row here.
    /// Equip stays: it is a per-hero decision, made in the presence of the hero it affects.
    ///
    /// ---- visual pass ----
    /// A GRID OF SHARED ITEM CARDS, not three stacked lists of text rows. The screen used to be a
    /// resources card, then a list of relic rows, then a list of armor rows, each row a wide strip
    /// of prose - which meant nothing was scannable, the two kinds of item looked like two kinds of
    /// screen, and the player's whole collection was a vertical scroll. It is now the rail-grid-
    /// detail shape the mockup draws, and every cell is ItemCard, the same component the Hero
    /// Detail slots and the Forge anvil draw.
    ///
    /// THE PICKER IS UNTOUCHED. Everything from OpenPicker down to OnPickerUnassign is the code it
    /// was, including the roster-sized modal, the per-hero power pricing and the displaced-item
    /// line. That machinery is the good part of this screen and none of it is visual.
    /// </summary>
    public sealed class InventoryScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Inventory; } }

        private const float RailX = -872f;
        private const float RailWidth = 116f;
        private const float GridX = -226f;
        private const float GridWidth = 1044f;
        private const float CardSize = 128f;
        private const float DetailX = 626f;
        private const float DetailWidth = 552f;

        private Text _headerSubtitle;

        private RectTransform _rail;
        private RectTransform _grid;
        private RectTransform _detail;
        private Text _gridHeading;
        private readonly List<UIButton> _railButtons = new List<UIButton>();

        /// <summary>Presentation state. 0 all, 1 relics, 2..7 an armor slot. Nothing reaches the profile.</summary>
        private int _filter;

        /// <summary>"R:relicId" or "A:armorUid". Prefixed because the two are keyed differently.</summary>
        private string _selected = "";

        // Hero picker overlay -- built once in Build(), shown/hidden on demand.
        private RectTransform _pickerRoot;
        private Text _pickerTitle;
        private RectTransform _pickerHeroList;

        /// <summary>
        /// The modal card itself. Held because its HEIGHT is now a function of how many heroes are
        /// on the roster - see SizePickerPanel.
        /// </summary>
        private RectTransform _pickerPanel;

        /// <summary>
        /// What the candidate item itself is and does, under the title. The player was being asked
        /// to choose a wearer without the thing being worn ever stating its own numbers.
        /// </summary>
        private Text _pickerItemLine;

        /// <summary>
        /// The piece being placed, resolved once when the picker opens. Held as the object rather
        /// than re-looked-up per row because every row prices the same candidate against a
        /// different hero, and eleven lookups of a value that cannot change mid-modal is work for
        /// nothing.
        /// </summary>
        private ArmorPiece _pickerArmorPiece;
        private RelicDef _pickerRelicDef;

        // The picker holds exactly one of these at a time; the other is always null/empty. Two
        // fields rather than one object reference because a relic is identified by its content id
        // and a piece of armor by its save uid, and collapsing them into one string would make the
        // picker guess which kind it was holding.
        private OwnedRelic _pickerRelic;
        private string _pickerArmorUid = "";

        protected override void Build()
        {
            ScreenChrome.Header(Root, "INVENTORY", Router, out _headerSubtitle);

            float top = ScreenChrome.HeaderTop + 108f + UITheme.Space16;
            BuildRail(top);
            BuildGrid(top);
            BuildDetail(top);
            BuildHeroPicker();
        }

        /// <summary>The category rail, down the left, as the mockup draws it.</summary>
        private void BuildRail(float top)
        {
            _rail = UIBuilder.Node("Rail", Root);
            UIBuilder.Box(_rail, RailX, -top, RailWidth, 8f * 82f + 8f, new Vector2(0.5f, 1f));

            Image bg = _rail.gameObject.AddComponent<Image>();
            bg.sprite = TextureLab.NotchedRect(14);
            bg.type = Image.Type.Sliced;
            bg.color = UITheme.WithAlpha(UITheme.PanelSunken, 0.9f);
            bg.raycastTarget = false;
            UIBuilder.NotchedFrame("Edge", _rail, UITheme.WithAlpha(UITheme.ChromeBrass, 0.6f), 14, 2);

            AddRailButton(0, TextureLab.Chest(64), "ALL");
            AddRailButton(1, TextureLab.Sigil(13, 64), "RELIC");
            for (int i = 0; i < ArmorInfo.AllSlots.Length; i++)
                AddRailButton(i + 2, ItemCard.SlotIcon(ArmorInfo.AllSlots[i]),
                              ItemCard.ShortSlotName(ArmorInfo.AllSlots[i]));
        }

        private void AddRailButton(int index, Sprite icon, string tip)
        {
            UIButton button = UIBuilder.Notch(
                UIBuilder.MakeButton("Rail" + index, _rail, "", UITheme.FontTiny,
                                     UITheme.WithAlpha(UITheme.Panel, 0.9f), UITheme.BorderSoft,
                                     UITheme.TextPrimary), 10);
            UIBuilder.Box(button.Root, 0f, -(8f + index * 82f), 92f, 74f, new Vector2(0.5f, 1f));

            Image glyph = UIBuilder.Img("Glyph", button.Root, icon, UITheme.TextMuted);
            UIBuilder.Box(glyph.rectTransform, 0f, 6f, 34f, 34f, new Vector2(0.5f, 0.5f));

            Text label = UIBuilder.Label("Tip", button.Root, tip, UITheme.FontTiny - 8,
                                         UITheme.TextDim, TextAnchor.LowerCenter);
            UIBuilder.Box(label.rectTransform, 0f, 4f, 88f, 18f, new Vector2(0.5f, 0f));

            int captured = index;
            button.OnClick(delegate { SetFilter(captured); });
            _railButtons.Add(button);
        }

        private void BuildGrid(float top)
        {
            _gridHeading = UIBuilder.Label("GridHeading", Root, "", UITheme.FontTiny,
                                           UITheme.TextDim, TextAnchor.UpperLeft);
            // Centred on the grid with left-aligned TEXT, not anchored to the parent's left edge.
            // Box's anchor is also its pivot, so a (0,1) anchor measures x from the canvas edge -
            // which put this heading at x=-1708, off screen and through the rail.
            UIBuilder.Box(_gridHeading.rectTransform, GridX, -top, GridWidth, 28f,
                          new Vector2(0.5f, 1f));

            ScrollRect scroll;
            _grid = UIBuilder.ScrollGrid("Grid", Root, out scroll,
                                         new Vector2(CardSize, CardSize),
                                         new Vector2(UITheme.Space16, UITheme.Space16), 7,
                                         new RectOffset(10, 10, 10, 10));
            UIBuilder.Box(scroll.GetComponent<RectTransform>(), GridX, -(top + 32f), GridWidth, 604f,
                          new Vector2(0.5f, 1f));
        }

        private void BuildDetail(float top)
        {
            _detail = UIBuilder.Node("Detail", Root);
            UIBuilder.Box(_detail, DetailX, -top, DetailWidth, 636f, new Vector2(0.5f, 1f));

            Image bg = _detail.gameObject.AddComponent<Image>();
            bg.sprite = TextureLab.NotchedRect(18);
            bg.type = Image.Type.Sliced;
            bg.color = UITheme.WithAlpha(UITheme.Panel, 0.94f);
            bg.raycastTarget = false;
            UIBuilder.NotchedFrame("Edge", _detail, UITheme.WithAlpha(UITheme.ChromeBrass, 0.8f), 18, 2);
        }

        // ------------------------------------------------------------------ picker overlay

        private void BuildHeroPicker()
        {
            _pickerRoot = UIBuilder.Node("HeroPicker", Root);
            UIBuilder.FillParent(_pickerRoot, 0f);

            Image dim = _pickerRoot.gameObject.AddComponent<Image>();
            dim.sprite = TextureLab.White;
            dim.color = UITheme.ScrimModal;

            // Height is a placeholder; SizePickerPanel sets the real one each time the picker
            // opens, because it depends on the roster.
            RectTransform panel = UIBuilder.Node("Panel", _pickerRoot);
            _pickerPanel = panel;
            UIBuilder.Box(panel, 0f, 0f, 900f, 560f, new Vector2(0.5f, 0.5f));

            Image panelBg = panel.gameObject.AddComponent<Image>();
            panelBg.sprite = UISkin.Panel(24);
            panelBg.type = Image.Type.Sliced;
            panelBg.color = UITheme.Panel;
            UIBuilder.Frame("Edge", panel, UITheme.Border, 24, 3);

            _pickerTitle = UIBuilder.Label("Title", panel, "", UITheme.FontBody,
                                           UITheme.TextPrimary, TextAnchor.UpperCenter);
            UIBuilder.Box(_pickerTitle.rectTransform, 0f, -22f, 820f, 34f, new Vector2(0.5f, 1f));

            _pickerItemLine = UIBuilder.Label("ItemLine", panel, "", UITheme.FontTiny,
                                              UITheme.TextMuted, TextAnchor.UpperCenter);
            UIBuilder.Box(_pickerItemLine.rectTransform, 0f, -60f, 820f, 30f, new Vector2(0.5f, 1f));

            UIButton cancel = UIBuilder.SecondaryButton("Cancel", panel, "CANCEL", UITheme.FontSmall);
            UIBuilder.Box(cancel.Root, 0f, 20f, 240f, 76f, new Vector2(0.5f, 0f));
            cancel.OnClick(ClosePicker);

            // Scrollable hero list inside the panel.
            ScrollRect heroScroll;
            _pickerHeroList = UIBuilder.ScrollList("Heroes", panel, out heroScroll,
                                                   10f, new RectOffset(16, 16, 8, 8));
            UIBuilder.FillParent(heroScroll.GetComponent<RectTransform>(),
                                 16f, TitleBand, 16f, CancelBand);

            _pickerRoot.gameObject.SetActive(false);
        }

        private void OpenPicker(OwnedRelic relic)
        {
            _pickerRelic = relic;
            _pickerArmorUid = "";
            _pickerArmorPiece = null;
            _pickerRelicDef = App.Session.Content.GetRelic(relic.RelicId);
            RelicDef def = App.Session.Content.GetRelic(relic.RelicId);
            OpenPickerFor(def != null ? def.Name : relic.RelicId,
                          App.Session.Profile.HeroHoldingRelic(relic.RelicId));
        }

        /// <summary>
        /// The armor half of the picker. Same overlay, same rows, same unassign affordance - the
        /// only difference is which of the two id fields is set, which OnPickerAssign and
        /// OnPickerUnassign branch on.
        /// </summary>
        private void OpenArmorPicker(ArmorPiece piece)
        {
            _pickerRelic = null;
            _pickerArmorUid = piece.Uid;
            _pickerArmorPiece = piece;
            _pickerRelicDef = null;
            OpenPickerFor(piece.Name + "  (" + ArmorInfo.SlotLabel(piece.Slot) + ")",
                          App.Session.Profile.HeroWearingArmor(piece.Uid));
        }

        private void OpenPickerFor(string itemName, HeroInstance currentHolder)
        {
            _pickerTitle.text = "EQUIP  " + itemName.ToUpperInvariant() + "  TO...";
            _pickerItemLine.text = DescribeCandidate();

            // Rebuild hero list.
            for (int i = _pickerHeroList.childCount - 1; i >= 0; i--)
                Object.Destroy(_pickerHeroList.GetChild(i).gameObject);

            bool isAssigned = currentHolder != null;

            UIButton unassignBtn = UIBuilder.SecondaryButton("Unassign", _pickerHeroList,
                                                             "UNEQUIP (return to pool)", UITheme.FontSmall);
            UIBuilder.Sized(unassignBtn.Root, 0f, UnassignRowHeight);
            unassignBtn.Interactable = isAssigned;
            unassignBtn.OnClick(delegate { OnPickerUnassign(); });

            UIBuilder.Divider("Div", _pickerHeroList, UITheme.WithAlpha(UITheme.BorderSoft, 0.5f));

            // One row per hero, each PRICED against that hero rather than just naming them.
            List<HeroInstance> heroes = App.Session.Profile.Heroes;
            for (int i = 0; i < heroes.Count; i++) AddCandidateRow(heroes[i], heroes[i] == currentHolder);

            if (heroes.Count == 0)
            {
                Text noHeroes = UIBuilder.Label("NoHeroes", _pickerHeroList,
                                                "No heroes in your collection yet.",
                                                UITheme.FontSmall, UITheme.TextDim, TextAnchor.MiddleCenter);
                UIBuilder.Sized(noHeroes.rectTransform, 0f, 60f);
            }

            SizePickerPanel(itemName, heroes.Count);

            _pickerRoot.gameObject.SetActive(true);
            _pickerRoot.SetAsLastSibling();
        }

        /// <summary>
        /// Grows the modal to fit its rows instead of clipping them.
        ///
        /// The bug this fixes: the panel was a fixed 560 tall, which left a 378-tall scroll
        /// viewport, and five 72px rows plus the unequip row and divider need 510. The list is a
        /// real ScrollRect with a RectMask2D, so the overflow was not drawn OVER anything - it was
        /// clipped, and clipped content is not raycastable either. Hit-testing every row found the
        /// last two returning the panel and the CANCEL button instead of themselves. They were
        /// reachable only by dragging a list that gave no sign it could scroll, and the row at the
        /// mask boundary was cut in half, which reads as a rendering fault rather than an
        /// invitation. Two of five heroes were effectively missing - including, for an aspect-
        /// attuned piece, the one the item was actually for.
        ///
        /// Sizing to content rather than nudging the constant is what makes this hold for a roster
        /// that grows: any count that fits under MaxPanel is fully visible with no scrolling at
        /// all. Beyond that it must still scroll - there is no height that fits an unbounded list -
        /// so the title says so outright rather than leaving the player to discover it by accident.
        /// </summary>
        private void SizePickerPanel(string itemName, int heroCount)
        {
            // Mirrors BuildHeroPicker's own numbers: rows are Sized to 72, ScrollList spacing is
            // 10, its padding is RectOffset(16, 16, 8, 8), and UIBuilder.Divider is 2 tall.
            const float Spacing = 10f;
            const float PadV = 16f;
            const float DividerHeight = 2f;

            const float MinPanel = 320f;
            const float MaxPanel = 940f;   // leaves a margin inside the 1080 reference height

            float contentHeight = PadV
                                + UnassignRowHeight
                                + heroCount * CandidateRowHeight
                                + DividerHeight
                                + Spacing * (heroCount + 1);

            float wanted = TitleBand + contentHeight + CancelBand;
            float panelHeight = Mathf.Clamp(wanted, MinPanel, MaxPanel);

            UIBuilder.Box(_pickerPanel, 0f, 0f, 900f, panelHeight, new Vector2(0.5f, 0.5f));

            bool scrolls = contentHeight > panelHeight - TitleBand - CancelBand;
            _pickerTitle.text = "EQUIP  " + itemName.ToUpperInvariant() + "  TO..."
                                + (scrolls ? "      (scroll for more)" : "");
        }

        private const float TitleBand = 108f;
        private const float CancelBand = 110f;
        private const float CandidateRowHeight = 96f;
        private const float UnassignRowHeight = 64f;

        /// <summary>
        /// One hero, priced. This is the whole point of the change: the row states what this hero
        /// is WEARING in the slot the candidate wants, and what their power becomes if the swap
        /// happens, so the decision is made against numbers instead of against a name.
        ///
        /// What it replaces: a list of bare uppercase hero names. Nothing on that list said what
        /// the item did, what it would displace, or which hero it helped most - and EquipArmor
        /// silently returns whatever was in the slot to the pool, so every tap was an unannounced
        /// swap. "Messy" was an accurate description of choosing blind.
        /// </summary>
        private void AddCandidateRow(HeroInstance hero, bool isHolder)
        {
            UIButton row = UIBuilder.MakeButton("Hero_" + hero.Uid, _pickerHeroList, "", UITheme.FontSmall,
                                                isHolder ? UITheme.Ember : UITheme.PanelRaised,
                                                isHolder ? UITheme.EmberDeep : UITheme.Border,
                                                UITheme.TextPrimary);
            UIBuilder.Sized(row.Root, 0f, CandidateRowHeight);
            row.Label.gameObject.SetActive(false);   // this row lays its own text out

            RectTransform body = UIBuilder.Node("Body", row.Root);
            UIBuilder.FillParent(body, 0f);
            body.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;

            UnitStats before = StatsFor(hero, false);
            UnitStats after = StatsFor(hero, true);
            int delta = after.Power - before.Power;

            string heroLabel = hero.Def != null ? hero.Def.Name : hero.DefId;
            Text name = UIBuilder.Label("Name", body, heroLabel.ToUpperInvariant()
                                        + "   LV " + hero.Level + (isHolder ? "   (equipped)" : ""),
                                        UITheme.FontSmall, UITheme.TextPrimary, TextAnchor.MiddleLeft);
            UIBuilder.Box(name.rectTransform, 24f, 18f, 520f, 30f, new Vector2(0f, 0.5f));

            Text swap = UIBuilder.Label("Swap", body, DescribeDisplaced(hero), UITheme.FontTiny,
                                        UITheme.TextMuted, TextAnchor.MiddleLeft);
            UIBuilder.Box(swap.rectTransform, 24f, -18f, 520f, 26f, new Vector2(0f, 0.5f));

            // Before -> after on one line, because the comparison IS the row. Power leads because
            // it is the one number every other screen already ranks heroes by.
            Text power = UIBuilder.Label("Power", body, before.Power + "  ->  " + after.Power,
                                         UITheme.FontSmall, UITheme.TextPrimary, TextAnchor.MiddleRight);
            UIBuilder.Box(power.rectTransform, -24f, 18f, 380f, 30f, new Vector2(1f, 0.5f));

            string sign = delta > 0 ? "+" : "";
            Color tint = delta > 0 ? UITheme.Success : delta < 0 ? UITheme.Danger : UITheme.TextDim;
            string detail = sign + delta + " POWER" + StatDiff(before, after);
            Text change = UIBuilder.Label("Delta", body, detail, UITheme.FontTiny, tint, TextAnchor.MiddleRight);
            UIBuilder.Box(change.rectTransform, -24f, -18f, 380f, 26f, new Vector2(1f, 0.5f));

            HeroInstance captured = hero;
            row.OnClick(delegate { OnPickerAssign(captured); });
        }

        /// <summary>
        /// This hero's stats as they are, or as they would be with the candidate on. Resolved
        /// through the same StatMath.ForHero overload the SIMULATION uses, relic and armor folded
        /// in, so the number the player compares against is the number that fights.
        /// </summary>
        private UnitStats StatsFor(HeroInstance hero, bool withCandidate)
        {
            ContentDatabase content = App.Session.Content;
            ArmorConfig config = content != null ? content.Armor : null;

            RelicDef relic = !string.IsNullOrEmpty(hero.EquippedRelicId)
                ? content.GetRelic(hero.EquippedRelicId) : null;
            List<ArmorPiece> worn = App.Session.Profile.EquippedArmorOf(hero);

            if (withCandidate)
            {
                if (_pickerArmorPiece != null)
                {
                    // The candidate DISPLACES whatever shares its slot rather than adding to the
                    // set - modelling it as an addition would price a two-boots hero and overstate
                    // every swap.
                    List<ArmorPiece> next = new List<ArmorPiece>();
                    for (int i = 0; i < worn.Count; i++)
                        if (worn[i].Slot != _pickerArmorPiece.Slot) next.Add(worn[i]);
                    next.Add(_pickerArmorPiece);
                    worn = next;
                }
                else if (_pickerRelicDef != null)
                {
                    relic = _pickerRelicDef;
                }
            }

            return StatMath.ForHero(hero.Def, hero.Level, relic, worn, config);
        }

        /// <summary>
        /// The candidate stating its own numbers, under the title. Previously the modal named the
        /// item and then asked for a decision about it without ever showing what it did - the
        /// stats were on the card the player had already navigated away from.
        /// </summary>
        private string DescribeCandidate()
        {
            if (_pickerArmorPiece != null)
                return ArmorInfo.TemperLabel(_pickerArmorPiece.Temper).ToUpperInvariant()
                     + "   -   " + AspectChart.DisplayName(_pickerArmorPiece.Aspect).ToUpperInvariant()
                     + "   -   " + _pickerArmorPiece.DescribeStats();
            if (_pickerRelicDef != null)
            {
                string stats = _pickerRelicDef.DescribeStats();
                return string.IsNullOrEmpty(_pickerRelicDef.Rule) ? stats : stats + "   -   " + _pickerRelicDef.Rule;
            }
            return "";
        }

        /// <summary>Names what this tap would take off, which is the half of the trade the old
        /// list never mentioned at all.</summary>
        private string DescribeDisplaced(HeroInstance hero)
        {
            if (_pickerArmorPiece != null)
            {
                List<ArmorPiece> worn = App.Session.Profile.EquippedArmorOf(hero);
                for (int i = 0; i < worn.Count; i++)
                    if (worn[i].Slot == _pickerArmorPiece.Slot)
                        return worn[i].Uid == _pickerArmorPiece.Uid
                            ? "already in this slot"
                            : "replaces " + worn[i].Name;
                return ArmorInfo.SlotLabel(_pickerArmorPiece.Slot) + " slot is empty";
            }

            if (!string.IsNullOrEmpty(hero.EquippedRelicId))
            {
                RelicDef held = App.Session.Content.GetRelic(hero.EquippedRelicId);
                return held != null ? "replaces " + held.Name : "replaces their relic";
            }
            return "no relic held";
        }

        /// <summary>
        /// The stats that actually moved, up to three, so the row says WHY the power changed.
        /// Only non-zero diffs appear - listing every stat with "+0" beside it would bury the two
        /// that matter under twelve that do not.
        /// </summary>
        private static string StatDiff(UnitStats a, UnitStats b)
        {
            string outp = "";
            int shown = 0;
            shown += Append(ref outp, "HP", b.MaxHp - a.MaxHp, shown);
            shown += Append(ref outp, "ATK", b.Attack - a.Attack, shown);
            shown += Append(ref outp, "DEF", b.Defense - a.Defense, shown);
            shown += Append(ref outp, "SPD", b.Speed - a.Speed, shown);
            return outp;
        }

        private static int Append(ref string text, string label, float diff, int shown)
        {
            if (shown >= 3 || Mathf.Abs(diff) < 0.5f) return 0;
            text += "   " + label + (diff > 0 ? " +" : " ") + Mathf.RoundToInt(diff);
            return 1;
        }

        private void ClosePicker()
        {
            _pickerRoot.gameObject.SetActive(false);
            _pickerRelic = null;
            _pickerArmorUid = "";
        }

        private void OnPickerAssign(HeroInstance hero)
        {
            string heroName = hero.Def != null ? hero.Def.Name : hero.DefId;

            if (!string.IsNullOrEmpty(_pickerArmorUid))
            {
                ArmorPiece piece = App.Session.Profile.FindArmor(_pickerArmorUid);
                if (piece == null) { ClosePicker(); RebuildAll(); return; }
                App.Session.EquipArmor(hero, _pickerArmorUid);
                App.Audio.Tap();
                App.Notify(piece.Name + " equipped on " + heroName + ".");
                App.RequestSave();
                ClosePicker();
                RebuildAll();
                return;
            }

            if (_pickerRelic == null) return;
            App.Session.AssignRelic(hero, _pickerRelic.RelicId);
            App.Audio.Tap();

            RelicDef def = App.Session.Content.GetRelic(_pickerRelic.RelicId);
            string relicName = def != null ? def.Name : _pickerRelic.RelicId;
            App.Notify(relicName + " equipped on " + heroName + ".");
            App.RequestSave();
            ClosePicker();
            RebuildRelics();
        }

        private void OnPickerUnassign()
        {
            if (!string.IsNullOrEmpty(_pickerArmorUid))
            {
                ArmorPiece piece = App.Session.Profile.FindArmor(_pickerArmorUid);
                HeroInstance wearer = App.Session.Profile.HeroWearingArmor(_pickerArmorUid);
                if (piece != null && wearer != null)
                {
                    App.Session.UnequipArmor(wearer, piece.Slot);
                    App.Audio.Tap();
                    App.Notify(piece.Name + " unequipped.");
                    App.RequestSave();
                }
                ClosePicker();
                RebuildAll();
                return;
            }

            if (_pickerRelic == null) return;
            HeroInstance holder = App.Session.Profile.HeroHoldingRelic(_pickerRelic.RelicId);
            if (holder != null)
            {
                App.Session.UnassignRelic(holder);
                App.Audio.Tap();
                RelicDef def = App.Session.Content.GetRelic(_pickerRelic.RelicId);
                string relicName = def != null ? def.Name : _pickerRelic.RelicId;
                App.Notify(relicName + " unassigned.");
                App.RequestSave();
            }
            ClosePicker();
            RebuildRelics();
        }

        // ------------------------------------------------------------------ refresh

        public override void OnShow()
        {
            ClosePicker();
            App.RefreshHud();
            RebuildAll();
        }

        private void SetFilter(int filter)
        {
            if (_filter == filter) return;
            _filter = filter;
            _selected = "";
            App.Audio.Tap();
            RebuildAll();
        }

        private void Select(string key)
        {
            if (_selected == key) return;
            _selected = key;
            App.Audio.Tap();
            RebuildAll();
        }

        /// <summary>Kept: the picker's assign/unassign paths call this by name.</summary>
        private void RebuildRelics()
        {
            RebuildAll();
        }

        private void RebuildAll()
        {
            PlayerProfile profile = App.Session.Profile;

            for (int i = 0; i < _railButtons.Count; i++)
            {
                bool active = i == _filter;
                _railButtons[i].Background.color = active
                    ? UITheme.WithAlpha(UITheme.Gold, 0.20f)
                    : UITheme.WithAlpha(UITheme.Panel, 0.9f);
                _railButtons[i].Frame.color = active ? UITheme.Gold : UITheme.BorderSoft;
                Transform glyph = _railButtons[i].Root.Find("Glyph");
                if (glyph != null)
                    glyph.GetComponent<Image>().color = active ? UITheme.Gold : UITheme.TextMuted;
            }

            _headerSubtitle.text = profile.OwnedRelics.Count + " RELICS   -   "
                                 + profile.OwnedArmor.Count + " ARMOR PIECES";

            RebuildGrid();
            RebuildDetail();
        }

        // ------------------------------------------------------------------ the grid

        private bool ShowRelics { get { return _filter == 0 || _filter == 1; } }

        private bool ShowsSlot(ArmorSlot slot)
        {
            if (_filter == 0) return true;
            if (_filter == 1) return false;
            return ArmorInfo.AllSlots[_filter - 2] == slot;
        }

        private void RebuildGrid()
        {
            for (int i = _grid.childCount - 1; i >= 0; i--) Object.Destroy(_grid.GetChild(i).gameObject);

            PlayerProfile profile = App.Session.Profile;
            int count = 0;

            if (ShowRelics)
            {
                List<OwnedRelic> relics = profile.OwnedRelics;
                for (int i = 0; i < relics.Count; i++)
                {
                    RelicDef def = App.Session.Content.GetRelic(relics[i].RelicId);
                    if (def == null) continue;
                    AddRelicCell(relics[i], def);
                    count++;
                }
            }

            List<ArmorPiece> armor = profile.OwnedArmor;
            for (int i = 0; i < armor.Count; i++)
            {
                if (!ShowsSlot(armor[i].Slot)) continue;
                AddArmorCell(armor[i]);
                count++;
            }

            _gridHeading.text = FilterName() + "   (" + count + ")";

            if (count == 0)
            {
                RectTransform cell = UIBuilder.Node("Empty", _grid);
                Text empty = UIBuilder.Label("Text", cell, "-", UITheme.FontSmall,
                                             UITheme.TextDim, TextAnchor.MiddleCenter);
                UIBuilder.FillParent(empty.rectTransform, 0f);

                Text hint = UIBuilder.Label("Hint", Root, EmptyHint(), UITheme.FontTiny,
                                            UITheme.TextDim, TextAnchor.UpperCenter);
                UIBuilder.Box(hint.rectTransform, GridX, -(ScreenChrome.HeaderTop + 108f + UITheme.Space16 + 220f),
                              700f, 30f, new Vector2(0.5f, 1f));
                hint.name = "EmptyHint";
            }
            else
            {
                Transform stale = Root.Find("EmptyHint");
                if (stale != null) Object.Destroy(stale.gameObject);
            }

            // Nothing selected, or the selection fell out of the filter: land on the first cell so
            // the detail panel always has a subject.
            if (_grid.childCount > 0 && !SelectionVisible()) SelectFirstVisible();
        }

        private string FilterName()
        {
            if (_filter == 0) return "EVERYTHING";
            if (_filter == 1) return "RELICS";
            return ArmorInfo.SlotLabel(ArmorInfo.AllSlots[_filter - 2]).ToUpperInvariant();
        }

        private string EmptyHint()
        {
            if (_filter == 1) return "Relics are bought at the Relic Shop.";
            return "Armor drops from stage clears.";
        }

        private bool SelectionVisible()
        {
            if (string.IsNullOrEmpty(_selected)) return false;
            for (int i = 0; i < _grid.childCount; i++)
                if (_grid.GetChild(i).name == "Cell_" + _selected) return true;
            return false;
        }

        private void SelectFirstVisible()
        {
            for (int i = 0; i < _grid.childCount; i++)
            {
                string n = _grid.GetChild(i).name;
                if (!n.StartsWith("Cell_")) continue;
                _selected = n.Substring(5);
                RebuildDetail();
                MarkSelected();
                return;
            }
        }

        /// <summary>Lifts the selected cell. Re-run rather than rebuilt, so selecting is cheap.</summary>
        private void MarkSelected()
        {
            for (int i = 0; i < _grid.childCount; i++)
            {
                Transform cell = _grid.GetChild(i);
                Transform halo = cell.Find("Selected");
                if (halo == null) continue;
                halo.gameObject.SetActive(cell.name == "Cell_" + _selected);
            }
        }

        private void AddRelicCell(OwnedRelic owned, RelicDef def)
        {
            string key = "R:" + owned.RelicId;
            RectTransform cell = MakeCell(key);

            RectTransform card = ItemCard.ForRelic("Card", cell, CardSize, def,
                                                   owned.Tier, App.Session.RelicMaxTier);
            UIBuilder.FillParent(card, 0f);

            if (App.Session.Profile.HeroHoldingRelic(owned.RelicId) != null) MarkWorn(cell);
            FinishCell(cell, key);
        }

        private void AddArmorCell(ArmorPiece piece)
        {
            string key = "A:" + piece.Uid;
            RectTransform cell = MakeCell(key);

            RectTransform card = ItemCard.ForArmor("Card", cell, CardSize, piece);
            UIBuilder.FillParent(card, 0f);

            if (App.Session.Profile.HeroWearingArmor(piece.Uid) != null) MarkWorn(cell);
            FinishCell(cell, key);
        }

        private RectTransform MakeCell(string key)
        {
            RectTransform cell = UIBuilder.Node("Cell_" + key, _grid);

            Image halo = UIBuilder.Img("Selected", cell, TextureLab.Glow(96, 1.6f),
                                       UITheme.WithAlpha(UITheme.Gold, 0.32f));
            UIBuilder.FillParent(halo.rectTransform, -14f);
            halo.gameObject.SetActive(key == _selected);
            return cell;
        }

        /// <summary>A worn item is marked, so the grid says what is already in use.</summary>
        private static void MarkWorn(RectTransform cell)
        {
            Image worn = UIBuilder.Img("Worn", cell, TextureLab.Check(32), UITheme.Success);
            UIBuilder.Box(worn.rectTransform, -6f, -6f, 22f, 22f, new Vector2(1f, 1f));
        }

        private void FinishCell(RectTransform cell, string key)
        {
            Image target = cell.gameObject.AddComponent<Image>();
            target.color = new Color(1f, 1f, 1f, 0.003f);   // a Button needs a graphic to be hit
            target.rectTransform.SetAsFirstSibling();

            Button press = cell.gameObject.AddComponent<Button>();
            press.transition = Selectable.Transition.None;
            press.targetGraphic = target;

            string captured = key;
            press.onClick.AddListener(delegate { Select(captured); });
        }

        // ------------------------------------------------------------------ the detail panel

        private void RebuildDetail()
        {
            for (int i = _detail.childCount - 1; i >= 0; i--)
            {
                Transform child = _detail.GetChild(i);
                if (child.name == "Edge") continue;
                Object.Destroy(child.gameObject);
            }

            if (string.IsNullOrEmpty(_selected))
            {
                Text none = UIBuilder.Label("None", _detail, "SELECT AN ITEM", UITheme.FontSmall,
                                            UITheme.TextDim, TextAnchor.MiddleCenter);
                UIBuilder.FillParent(none.rectTransform, 0f);
                return;
            }

            if (_selected.StartsWith("A:")) DetailForArmor(_selected.Substring(2));
            else DetailForRelic(_selected.Substring(2));
        }

        /// <summary>The shared head of both detail panels: the card, the tier row, the name.</summary>
        private float DetailHead(RectTransform card, string name, string rung, Color tint,
                                 int pips, int maxPips)
        {
            float top = UITheme.Space24;

            RectTransform host = UIBuilder.Node("CardHost", _detail);
            UIBuilder.Box(host, UITheme.Space24, -top, 150f, 150f, new Vector2(0f, 1f));
            card.SetParent(host, false);
            UIBuilder.FillParent(card, 0f);

            float textLeft = UITheme.Space24 + 150f + UITheme.Space24;

            Text nameLabel = UIBuilder.WrappedLabel("Name", _detail, name, UITheme.FontSmall,
                                                    UITheme.TextPrimary, TextAnchor.UpperLeft);
            UIBuilder.Box(nameLabel.rectTransform, textLeft, -top, DetailWidth - textLeft - UITheme.Space24,
                          62f, new Vector2(0f, 1f));

            Text rungLabel = UIBuilder.Label("Rung", _detail, rung, UITheme.FontTiny, tint, TextAnchor.UpperLeft);
            UIBuilder.Box(rungLabel.rectTransform, textLeft, -(top + 66f),
                          DetailWidth - textLeft - UITheme.Space24, 26f, new Vector2(0f, 1f));

            // The tier row, bracketed by rule lines exactly as the mockup draws it.
            Rule(textLeft, top + 98f, DetailWidth - textLeft - UITheme.Space24);
            RectTransform pipRow = UIBuilder.Node("Pips", _detail);
            UIBuilder.Box(pipRow, textLeft, -(top + 104f), 220f, 30f, new Vector2(0f, 1f));
            for (int i = 0; i < maxPips; i++)
            {
                Image pip = UIBuilder.Img("Pip" + i, pipRow, TextureLab.Diamond(32),
                                          i < pips ? tint : UITheme.WithAlpha(UITheme.BorderSoft, 0.8f));
                UIBuilder.Box(pip.rectTransform, 13f + i * 34f, 0f, 24f, 24f, new Vector2(0f, 0.5f));
            }
            Rule(textLeft, top + 140f, DetailWidth - textLeft - UITheme.Space24);

            return top + 168f;
        }

        private void Rule(float x, float y, float width)
        {
            Image rule = UIBuilder.Img("Rule" + y, _detail, TextureLab.White,
                                       UITheme.WithAlpha(UITheme.ChromeBrass, 0.55f));
            UIBuilder.Box(rule.rectTransform, x, -y, width, 1f, new Vector2(0f, 1f));
        }

        private void DetailForArmor(string uid)
        {
            ArmorPiece piece = App.Session.Profile.FindArmor(uid);
            if (piece == null) { _selected = ""; RebuildDetail(); return; }

            Color tint = UITheme.ArmorTemperColor(piece.Temper);
            RectTransform card = ItemCard.ForArmor("Card", _detail, 150f, piece);
            float top = DetailHead(card, piece.Name,
                                   ArmorInfo.TemperLabel(piece.Temper).ToUpperInvariant()
                                   + "   -   " + ArmorInfo.SlotLabel(piece.Slot).ToUpperInvariant(),
                                   tint, (int)piece.Temper + 1, 4);

            List<ArmorStatRoll> rolls = piece.AllRolls();
            for (int i = 0; i < rolls.Count && i < 5; i++)
            {
                ArmorStatRoll roll = rolls[i];
                StatLine(ArmorInfo.StatLabel(roll.Stat), RollText(roll), StatIcon(roll.Stat),
                         tint, DisplayRatio(roll), top);
                top += 34f;
            }

            top += UITheme.Space16;
            AspectRow(top, piece.Aspect, true);
            top += 92f;

            Wearer(top, App.Session.Profile.HeroWearingArmor(uid));
            ArmorPiece captured = piece;
            Action(delegate { OpenArmorPicker(captured); });
        }

        private void DetailForRelic(string relicId)
        {
            OwnedRelic owned = null;
            List<OwnedRelic> all = App.Session.Profile.OwnedRelics;
            for (int i = 0; i < all.Count; i++) if (all[i].RelicId == relicId) { owned = all[i]; break; }

            RelicDef def = App.Session.Content.GetRelic(relicId);
            if (owned == null || def == null) { _selected = ""; RebuildDetail(); return; }

            Color tint = UITheme.RelicGradeColor(def.Grade);
            RectTransform card = ItemCard.ForRelic("Card", _detail, 150f, def, owned.Tier,
                                                   App.Session.RelicMaxTier);
            float top = DetailHead(card, def.Name, UITheme.RelicGradeLabel(def.Grade),
                                   tint, owned.Tier, Mathf.Max(1, App.Session.RelicMaxTier));

            for (int i = 0; i < def.Stats.Count && i < 5; i++)
            {
                RelicStatBonus bonus = def.Stats[i];
                int pct = Mathf.RoundToInt(bonus.Value * 100f);
                StatLine(RelicDef.StatLabel(bonus.Stat), (pct >= 0 ? "+" : "") + pct + "%",
                         RelicStatIcon(bonus.Stat), tint, bonus.Value / 0.30f, top);
                top += 34f;
            }

            if (!string.IsNullOrEmpty(def.Rule))
            {
                top += UITheme.Space8;
                Text rule = UIBuilder.WrappedLabel("Rule", _detail, def.Rule, UITheme.FontTiny - 4,
                                                   UITheme.TextMuted, TextAnchor.UpperLeft);
                UIBuilder.Box(rule.rectTransform, UITheme.Space24, -top,
                              DetailWidth - UITheme.Space48, 74f, new Vector2(0f, 1f));
                top += 78f;
            }

            top += UITheme.Space16;
            // A relic is never flavoured to an aspect, so the row is drawn inert rather than
            // omitted - the absence is the fact, and leaving the row out would just look like the
            // panel forgot it. See UITheme.RelicGradeColor's own note.
            AspectRow(top, Aspect.Ember, false);
            top += 92f;

            Wearer(top, App.Session.Profile.HeroHoldingRelic(relicId));
            OwnedRelic captured = owned;
            Action(delegate { OpenPicker(captured); });
        }

        private void StatLine(string label, string value, Sprite icon, Color tint, float ratio, float top)
        {
            RectTransform row = ItemCard.StatBar("Stat_" + label, _detail, icon, tint, ratio, 6, 26f);
            UIBuilder.Box(row, UITheme.Space24, -top, DetailWidth - UITheme.Space48 - 178f, 26f,
                          new Vector2(0f, 1f));

            Text text = UIBuilder.Label("Val_" + label, _detail, label + "  " + value,
                                        UITheme.FontTiny - 6, UITheme.TextMuted, TextAnchor.MiddleRight);
            UIBuilder.Box(text.rectTransform, -UITheme.Space24, -top, 186f, 26f, new Vector2(1f, 1f));
        }

        /// <summary>
        /// The six aspects, with this item's own lit. Armour carries a rolled Aspect that decides
        /// its attunement with a wearer; a relic carries none at all, and <paramref name="live"/>
        /// false draws every pip dark to say exactly that.
        /// </summary>
        private void AspectRow(float top, Aspect own, bool live)
        {
            RectTransform row = UIBuilder.Node("Aspects", _detail);
            UIBuilder.Box(row, 0f, -top, DetailWidth - UITheme.Space48, 78f, new Vector2(0.5f, 1f));

            Image bg = row.gameObject.AddComponent<Image>();
            bg.sprite = TextureLab.NotchedRect(10);
            bg.type = Image.Type.Sliced;
            bg.color = UITheme.WithAlpha(UITheme.PanelSunken, 0.85f);
            bg.raycastTarget = false;

            float width = DetailWidth - UITheme.Space48;
            float step = width / 6f;
            for (int i = 0; i < 6; i++)
            {
                Aspect aspect = (Aspect)i;
                bool lit = live && aspect == own;
                Color accent = UITheme.AspectAccent(aspect);

                float x = -width * 0.5f + step * (i + 0.5f);
                Image icon = UIBuilder.Img("A" + i, row, TextureLab.Sigil(AspectSigil(aspect), 128),
                                           lit ? accent : UITheme.WithAlpha(accent, 0.22f));
                UIBuilder.Box(icon.rectTransform, x, 12f, 30f, 30f, new Vector2(0.5f, 0.5f));

                Image dot = UIBuilder.Img("D" + i, row, TextureLab.Circle(32),
                                          lit ? accent : UITheme.WithAlpha(UITheme.BorderSoft, 0.8f));
                UIBuilder.Box(dot.rectTransform, x, -18f, 12f, 12f, new Vector2(0.5f, 0.5f));
            }

            if (!live)
            {
                Text note = UIBuilder.Label("Note", row, "RELICS CARRY NO ASPECT",
                                            UITheme.FontTiny - 8, UITheme.TextDim, TextAnchor.LowerCenter);
                UIBuilder.Box(note.rectTransform, 0f, 2f, width, 18f, new Vector2(0.5f, 0f));
            }
        }

        private void Wearer(float top, HeroInstance holder)
        {
            string text = holder != null
                ? "WORN BY " + (holder.Def != null ? holder.Def.Name.ToUpperInvariant() : holder.DefId)
                : "NOT EQUIPPED";
            Text label = UIBuilder.Label("Wearer", _detail, text, UITheme.FontTiny,
                                         holder != null ? UITheme.Success : UITheme.TextDim,
                                         TextAnchor.UpperCenter);
            UIBuilder.Box(label.rectTransform, 0f, -top, DetailWidth - UITheme.Space48, 28f,
                          new Vector2(0.5f, 1f));
        }

        private void Action(UnityEngine.Events.UnityAction onEquip)
        {
            UIButton equip = UIBuilder.Notch(
                UIBuilder.GoldButton("Equip", _detail, "EQUIP", UITheme.FontSmall), 14);
            UIBuilder.Box(equip.Root, 0f, UITheme.Space24, DetailWidth - UITheme.Space48, 64f,
                          new Vector2(0.5f, 0f));
            equip.Button.onClick.AddListener(onEquip);
        }

        // ------------------------------------------------------------------ glyph mapping

        private static Sprite StatIcon(ArmorStat stat)
        {
            switch (stat)
            {
                case ArmorStat.MaxHp: return TextureLab.Heart(64);
                case ArmorStat.Defense: return TextureLab.Shield(64);
                case ArmorStat.Speed: return TextureLab.Boot(64);
                case ArmorStat.EffectHit: return TextureLab.Spark(64);
                case ArmorStat.EffectResist: return TextureLab.SpiralIcon(64);
            }
            return TextureLab.Sword(64);
        }

        private static Sprite RelicStatIcon(RelicStat stat)
        {
            switch (stat)
            {
                case RelicStat.MaxHp: return TextureLab.Heart(64);
                case RelicStat.Defense: return TextureLab.Shield(64);
                case RelicStat.Speed: return TextureLab.Boot(64);
                case RelicStat.EffectHit: return TextureLab.Spark(64);
                case RelicStat.EffectResist: return TextureLab.SpiralIcon(64);
            }
            return TextureLab.Sword(64);
        }

        /// <summary>
        /// Which of TextureLab's emblems stands for an aspect. Mapped by MEANING, not by casting the
        /// enum - the same table RecitalScreen uses, for the same reason: the enum's own order puts
        /// a flame mark on Tide.
        /// </summary>
        private static int AspectSigil(Aspect aspect)
        {
            switch (aspect)
            {
                case Aspect.Ember: return 7;
                case Aspect.Tide: return 12;
                case Aspect.Verdant: return 2;
                case Aspect.Gale: return 8;
                case Aspect.Umbra: return 6;
                case Aspect.Lumen: return 13;
            }
            return 13;
        }

        private static string RollText(ArmorStatRoll roll)
        {
            return ArmorInfo.IsPercentStat(roll.Stat)
                ? "+" + Mathf.RoundToInt(roll.Value * 100f) + "%"
                : "+" + Mathf.RoundToInt(roll.Value);
        }

        /// <summary>
        /// How full to draw a roll's bar. DISPLAY ONLY - the printed number beside it is the
        /// authority. A roll has no natural maximum to divide by, so these reference scales are
        /// chosen for legibility rather than read from balance, and are not a percentage of
        /// anything. Same table the Forge's comparison panel uses.
        /// </summary>
        private static float DisplayRatio(ArmorStatRoll roll)
        {
            if (ArmorInfo.IsPercentStat(roll.Stat)) return roll.Value / 0.45f;
            if (roll.Stat == ArmorStat.MaxHp) return roll.Value / 1200f;
            return roll.Value / 260f;
        }
    }
}
