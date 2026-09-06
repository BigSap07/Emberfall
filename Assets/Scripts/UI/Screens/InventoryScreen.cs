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
    /// Player resources and owned relics in one place.
    ///
    /// Resources section: a data-driven list of line items (Gold, Relic Dust) so adding a third
    /// resource later is one new entry, not a new field.
    ///
    /// Owned relics section: shows every relic the player holds, assigned or not, each with its
    /// grade badge and its own text - a legendary's stat line, or a mythic's rule plus its stat
    /// line. Tapping a relic's ASSIGN button opens an in-screen hero picker. Tapping UNASSIGN
    /// returns the relic to the unassigned pool. No aspect restriction of any kind, and none by
    /// grade either: the one slot is shared between the grades, so a hero wears one relic,
    /// legendary or mythic, not one of each.
    ///
    /// Armor section (DESIGN-ARMOR section 11): every piece the player owns, with its slot, aspect
    /// and temper chips, its stat lines, and one action - EQUIP, the same hero picker relics use.
    /// Reroll, Salvage and Combine live on their own screen, the Forge (`ScreenId.Forge`) - they
    /// need no hero in context, so they moved out to a pool-wide hub rather than staying crammed
    /// onto per-hero rows here. Equip stays: it is a per-hero decision, made in the presence of the
    /// hero it affects.
    ///
    /// The picker overlay is shared between relics and armor rather than duplicated. It was already
    /// written against "pick a hero for this item", so what changed is that the item it is holding
    /// is now one of two kinds - see _pickerRelic and _pickerArmorUid.
    /// </summary>
    public sealed class InventoryScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Inventory; } }

        private Text _headerSubtitle;
        private RectTransform _listContent;

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

            ScrollRect scroll;
            _listContent = UIBuilder.ScrollList("Content", Root, out scroll,
                                                16f, new RectOffset(32, 32, 12, 48));
            UIBuilder.FillParent(scroll.GetComponent<RectTransform>(),
                                 0f, ScreenChrome.HeaderTop + 108f, 0f, 0f);

            BuildHeroPicker();
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

        private void RebuildAll()
        {
            for (int i = _listContent.childCount - 1; i >= 0; i--)
                Object.Destroy(_listContent.GetChild(i).gameObject);

            BuildResourcesSection();
            UIBuilder.Spacer("Gap", _listContent, 8f);
            BuildRelicsSection();
            UIBuilder.Spacer("Gap2", _listContent, 8f);
            BuildArmorSection();
        }

        private void RebuildRelics()
        {
            // Remove only the relic section (everything from "RelicsHeader" down).
            // Simpler: just rebuild the whole list.
            RebuildAll();
        }

        // ------------------------------------------------------------------ resources section

        private void BuildResourcesSection()
        {
            BuildSectionHeader("RESOURCES");

            PlayerProfile p = App.Session.Profile;

            // Data-driven: adding a third resource is one more entry here. Slag is that third
            // entry, and it went in as one line exactly as the card was written to allow.
            var rows = new[]
            {
                new { Label = "Gold", Value = HudTopBar.Format(p.Gold), Color = UITheme.Gold },
                new { Label = "Relic Dust", Value = HudTopBar.Format(p.RelicDust), Color = UITheme.Ember },
                new { Label = "Slag", Value = HudTopBar.Format(p.Slag), Color = UITheme.ArmorTemperColor(ArmorTemper.Gilded) },
            };

            RectTransform card = BuildCard();
            RectTransform list = UIBuilder.Node("ResourceList", card);
            UIBuilder.FillParent(list, 16f, 16f, 16f, 16f);
            UIBuilder.VStack(list, 4f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);

            for (int i = 0; i < rows.Length; i++)
            {
                Color rowColor = rows[i].Color;
                string rowLabel = rows[i].Label;
                string rowValue = rows[i].Value;

                RectTransform row = UIBuilder.Node("Row_" + i, list);
                UIBuilder.Sized(row, 0f, 54f);

                Text labelText = UIBuilder.Label("Label", row, rowLabel,
                                                 UITheme.FontSmall, UITheme.TextMuted, TextAnchor.MiddleLeft);
                UIBuilder.FillParent(labelText.rectTransform, 16f, 0f, 0f, 0f);

                Text valueText = UIBuilder.Label("Value", row, rowValue,
                                                 UITheme.FontSmall, rowColor, TextAnchor.MiddleRight);
                UIBuilder.FillParent(valueText.rectTransform, 0f, 0f, 16f, 0f);

                if (i < rows.Length - 1)
                {
                    Image rule = UIBuilder.Img("Rule", row, TextureLab.White,
                                               UITheme.WithAlpha(UITheme.BorderSoft, 0.5f));
                    rule.rectTransform.anchorMin = new Vector2(0f, 0f);
                    rule.rectTransform.anchorMax = new Vector2(1f, 0f);
                    rule.rectTransform.pivot = new Vector2(0.5f, 0f);
                    rule.rectTransform.offsetMin = new Vector2(8f, 0f);
                    rule.rectTransform.offsetMax = new Vector2(-8f, 0f);
                    rule.rectTransform.sizeDelta = new Vector2(0f, 1f);
                }
            }

            // Fix card height to match content: rows.Length rows × 54px + 32px padding.
            UIBuilder.Sized(card, 0f, rows.Length * 54f + 32f);
        }

        // ------------------------------------------------------------------ relics section

        private void BuildRelicsSection()
        {
            BuildSectionHeader("OWNED RELICS");

            List<OwnedRelic> owned = App.Session.Profile.OwnedRelics;
            if (owned.Count == 0)
            {
                RectTransform emptyCard = BuildCard();
                UIBuilder.Sized(emptyCard, 0f, 100f);
                Text empty = UIBuilder.Label("Empty", emptyCard,
                                             "No relics yet — buy them from the Relic Shop.",
                                             UITheme.FontSmall, UITheme.TextDim, TextAnchor.MiddleCenter);
                UIBuilder.FillParent(empty.rectTransform, 20f);
                return;
            }

            for (int i = 0; i < owned.Count; i++) BuildRelicRow(owned[i]);
        }

        private void BuildRelicRow(OwnedRelic owned)
        {
            RelicDef def = App.Session.Content.GetRelic(owned.RelicId);
            string name = def != null ? def.Name : owned.RelicId;
            // A legendary's stat line, or a mythic's rule plus its stat line, picked by the def
            // rather than here. A relic id the content no longer defines falls back to empty.
            string cardText = def != null ? def.CardText() : "";

            HeroInstance holder = App.Session.Profile.HeroHoldingRelic(owned.RelicId);
            bool assigned = holder != null;
            string holderName = assigned && holder.Def != null ? holder.Def.Name : "";

            RectTransform card = UIBuilder.Node("Relic_" + owned.RelicId, _listContent);
            UIBuilder.Sized(card, 0f, 150f);

            Image bg = card.gameObject.AddComponent<Image>();
            bg.sprite = UISkin.Panel(18);
            bg.type = Image.Type.Sliced;
            bg.color = assigned ? UITheme.WithAlpha(UITheme.Panel, 0.95f) : UITheme.PanelSunken;
            UIBuilder.Frame("Edge", card, UITheme.WithAlpha(UITheme.Ember, assigned ? 0.7f : 0.3f), 18, 2);

            // Ember disc icon.
            RectTransform icon = UIBuilder.Node("Icon", card);
            UIBuilder.Box(icon, 22f, 0f, 68f, 68f, new Vector2(0f, 0.5f));
            Image disc = icon.gameObject.AddComponent<Image>();
            disc.sprite = TextureLab.Circle(96);
            disc.color = UITheme.WithAlpha(UITheme.Ember, 0.22f);
            disc.raycastTarget = false;
            Image ring = UIBuilder.Img("Ring", icon, TextureLab.Ring(96, 4), UITheme.WithAlpha(UITheme.Ember, 0.8f));
            UIBuilder.FillParent(ring.rectTransform, 0f);

            // Name.
            Text nameLabel = UIBuilder.Label("Name", card, name,
                                             UITheme.FontBody, UITheme.TextPrimary, TextAnchor.UpperLeft);
            nameLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            nameLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            nameLabel.rectTransform.offsetMin = new Vector2(108f, -46f);
            nameLabel.rectTransform.offsetMax = new Vector2(-190f, -16f);

            // Grade badge, on every row of both grades - it is what tells a player whether the
            // line at the bottom of the card is a plain stat list or a rule with stats stacked on
            // it. Falls back to Legendary for a relic id the content no longer defines, which is
            // also what CardText() falls back to via RelicDef's own default.
            RelicGrade grade = def != null ? def.Grade : RelicGrade.Legendary;
            RectTransform gradeBadge = UIBuilder.Chip("Grade", card, UITheme.RelicGradeLabel(grade),
                                                      UITheme.RelicGradeColor(grade), UITheme.FontTiny);
            UIBuilder.Box(gradeBadge, 108f, -46f, 132f, 28f, new Vector2(0f, 1f));

            // Assignment status, beside the badge.
            string statusText = assigned ? ("Equipped  -  " + holderName) : "Unassigned";
            Color statusColor = assigned ? UITheme.Success : UITheme.TextMuted;
            Text statusLabel = UIBuilder.Label("Status", card, statusText,
                                               UITheme.FontTiny, statusColor, TextAnchor.UpperLeft);
            statusLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            statusLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            statusLabel.rectTransform.offsetMin = new Vector2(252f, -72f);
            statusLabel.rectTransform.offsetMax = new Vector2(-190f, -46f);

            // The relic's own text: a legendary's numbers, or a mythic's rule plus its numbers.
            Text ruleLabel = UIBuilder.WrappedLabel("Rule", card, cardText,
                                                    UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperLeft);
            ruleLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            ruleLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            ruleLabel.rectTransform.offsetMin = new Vector2(108f, 14f);
            ruleLabel.rectTransform.offsetMax = new Vector2(-190f, -74f);

            // Assign button.
            UIButton assignBtn = UIBuilder.PrimaryButton("Assign", card, "ASSIGN", UITheme.FontSmall);
            UIBuilder.Box(assignBtn.Root, -18f, 0f, 160f, 72f, new Vector2(1f, 0.5f));
            OwnedRelic captured = owned;
            assignBtn.OnClick(delegate { OpenPicker(captured); });
        }

        // ------------------------------------------------------------------ armor section

        private void BuildArmorSection()
        {
            BuildSectionHeader("ARMOR");
            BuildForgeLink();

            List<ArmorPiece> owned = App.Session.Profile.OwnedArmor;
            if (owned.Count == 0)
            {
                RectTransform emptyCard = BuildCard();
                UIBuilder.Sized(emptyCard, 0f, 100f);
                Text empty = UIBuilder.Label("Empty", emptyCard,
                                             "No armor yet — pieces drop from stage clears.",
                                             UITheme.FontSmall, UITheme.TextDim, TextAnchor.MiddleCenter);
                UIBuilder.FillParent(empty.rectTransform, 20f);
                return;
            }

            for (int i = 0; i < owned.Count; i++) BuildArmorRow(owned[i]);
        }

        /// <summary>
        /// One armor piece. Taller than a relic row because a piece carries more to say: two chips
        /// (temper and aspect) plus a slot label and its stat lines. One action - EQUIP - unlike a
        /// relic row's ASSIGN, but laid out the same way, on the same right-edge anchor.
        ///
        /// Reroll, Salvage and Combine used to live here too (three more stacked buttons); they
        /// moved to the Forge screen, which is why this row is back to its original, shorter
        /// height rather than the four-action stack it briefly grew to.
        /// </summary>
        private void BuildArmorRow(ArmorPiece piece)
        {
            HeroInstance wearer = App.Session.Profile.HeroWearingArmor(piece.Uid);
            bool equipped = wearer != null;
            Color temperColor = UITheme.ArmorTemperColor(piece.Temper);

            RectTransform card = UIBuilder.Node("Armor_" + piece.Uid, _listContent);
            UIBuilder.Sized(card, 0f, 176f);

            Image bg = card.gameObject.AddComponent<Image>();
            bg.sprite = UISkin.Panel(18);
            bg.type = Image.Type.Sliced;
            bg.color = equipped ? UITheme.WithAlpha(UITheme.Panel, 0.95f) : UITheme.PanelSunken;
            UIBuilder.Frame("Edge", card, UITheme.WithAlpha(temperColor, equipped ? 0.75f : 0.35f), 18, 2);

            // Disc icon, tinted by the piece's own aspect rather than by its temper - the aspect is
            // what a player is scanning for when they are assembling an attunement set, and the
            // temper already has the whole card edge and its own chip.
            RectTransform icon = UIBuilder.Node("Icon", card);
            UIBuilder.Box(icon, 22f, 0f, 68f, 68f, new Vector2(0f, 0.5f));
            Image disc = icon.gameObject.AddComponent<Image>();
            disc.sprite = TextureLab.Circle(96);
            disc.color = UITheme.WithAlpha(UITheme.AspectColor(piece.Aspect), 0.22f);
            disc.raycastTarget = false;
            Image ring = UIBuilder.Img("Ring", icon, TextureLab.Ring(96, 4), UITheme.WithAlpha(UITheme.AspectColor(piece.Aspect), 0.85f));
            UIBuilder.FillParent(ring.rectTransform, 0f);

            Text nameLabel = UIBuilder.Label("Name", card, piece.Name,
                                             UITheme.FontBody, UITheme.TextPrimary, TextAnchor.UpperLeft);
            nameLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            nameLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            nameLabel.rectTransform.offsetMin = new Vector2(108f, -46f);
            nameLabel.rectTransform.offsetMax = new Vector2(-190f, -16f);

            RectTransform temperChip = UIBuilder.Chip("Temper", card, ArmorInfo.TemperLabel(piece.Temper),
                                                      temperColor, UITheme.FontTiny);
            UIBuilder.Box(temperChip, 108f, -46f, 128f, 28f, new Vector2(0f, 1f));

            RectTransform aspectChip = UIBuilder.Chip("Aspect", card, AspectChart.DisplayName(piece.Aspect),
                                                      UITheme.AspectColor(piece.Aspect), UITheme.FontTiny);
            UIBuilder.Box(aspectChip, 244f, -46f, 120f, 28f, new Vector2(0f, 1f));

            string statusText = ArmorInfo.SlotLabel(piece.Slot)
                              + (equipped ? "  -  " + (wearer.Def != null ? wearer.Def.Name : wearer.DefId) : "  -  In the pool");
            Text statusLabel = UIBuilder.Label("Status", card, statusText,
                                               UITheme.FontTiny, equipped ? UITheme.Success : UITheme.TextMuted,
                                               TextAnchor.UpperLeft);
            statusLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            statusLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            statusLabel.rectTransform.offsetMin = new Vector2(376f, -74f);
            statusLabel.rectTransform.offsetMax = new Vector2(-190f, -46f);

            Text statsLabel = UIBuilder.WrappedLabel("Stats", card, piece.DescribeStats(),
                                                     UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperLeft);
            statsLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            statsLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            statsLabel.rectTransform.offsetMin = new Vector2(108f, 14f);
            statsLabel.rectTransform.offsetMax = new Vector2(-190f, -76f);

            // One action, laid out exactly like the relic row's ASSIGN button - centred on the
            // right edge rather than stacked, since there is only the one now.
            UIButton equipBtn = UIBuilder.PrimaryButton("Equip", card, equipped ? "MOVE" : "EQUIP", UITheme.FontSmall);
            UIBuilder.Box(equipBtn.Root, -18f, 0f, 160f, 72f, new Vector2(1f, 0.5f));
            ArmorPiece capturedForEquip = piece;
            equipBtn.OnClick(delegate { OpenArmorPicker(capturedForEquip); });
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// The one route from the Bag to the Forge, and deliberately ONE - a pool-wide link under
        /// the section header rather than a FORGE button on every armour row.
        ///
        /// Per-row forge buttons are exactly what task 17 moved OUT of this screen: Reroll,
        /// Salvage and Combine need no hero in context, and Combine needs three pieces at once, so
        /// they belong on a pool-wide hub and not crammed onto a row that represents one piece.
        /// Putting them back per-row to save a tap would undo that on the quiet. What was missing
        /// was never the actions - it was any acknowledgement from here that the Forge exists.
        ///
        /// Same shape as HeroDetail's EQUIP IN BAG and Formation's VIEW ALL HEROES: the screen
        /// that cannot perform the action names the screen that can, once.
        /// </summary>
        private void BuildForgeLink()
        {
            RectTransform row = UIBuilder.Node("ForgeLinkRow", _listContent);
            UIBuilder.Sized(row, 0f, 64f);

            Text note = UIBuilder.Label("Note", row,
                                        "Reroll, Salvage and Combine happen at the Forge.",
                                        UITheme.FontTiny, UITheme.TextDim, TextAnchor.MiddleLeft);
            note.rectTransform.anchorMin = new Vector2(0f, 0f);
            note.rectTransform.anchorMax = new Vector2(1f, 1f);
            note.rectTransform.offsetMin = new Vector2(4f, 0f);
            note.rectTransform.offsetMax = new Vector2(-300f, 0f);

            UIButton open = UIBuilder.SecondaryButton("OpenForge", row, "OPEN THE FORGE", UITheme.FontTiny);
            UIBuilder.Box(open.Root, 0f, 0f, 280f, 56f, new Vector2(1f, 0.5f));
            open.OnClick(OpenForge);
        }

        private void OpenForge()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Forge);
        }

        private void BuildSectionHeader(string text)
        {
            Text heading = UIBuilder.Label("Heading_" + text, _listContent,
                                           text, UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperLeft);
            UIBuilder.Sized(heading.rectTransform, 0f, 36f);
        }

        private RectTransform BuildCard()
        {
            RectTransform card = UIBuilder.Node("Card", _listContent);

            Image bg = card.gameObject.AddComponent<Image>();
            bg.sprite = UISkin.Panel(18);
            bg.type = Image.Type.Sliced;
            bg.color = UITheme.Panel;
            UIBuilder.Frame("Edge", card, UITheme.BorderSoft, 18, 2);

            return card;
        }
    }
}
