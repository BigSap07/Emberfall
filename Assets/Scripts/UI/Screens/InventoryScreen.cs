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
    /// and temper chips, its stat lines, and three actions - EQUIP (the same hero picker), SALVAGE
    /// and ANVIL. This is where the Anvil lives; there is no separate screen, because the inventory
    /// is already the place a player looks at things they own and decides what to do with them.
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

            RectTransform panel = UIBuilder.Node("Panel", _pickerRoot);
            UIBuilder.Box(panel, 0f, 0f, 900f, 560f, new Vector2(0.5f, 0.5f));

            Image panelBg = panel.gameObject.AddComponent<Image>();
            panelBg.sprite = TextureLab.RoundedRect(24);
            panelBg.type = Image.Type.Sliced;
            panelBg.color = UITheme.Panel;
            UIBuilder.Frame("Edge", panel, UITheme.Border, 24, 3);

            _pickerTitle = UIBuilder.Label("Title", panel, "", UITheme.FontBody,
                                           UITheme.TextPrimary, TextAnchor.UpperCenter);
            UIBuilder.Box(_pickerTitle.rectTransform, 0f, -28f, 820f, 36f, new Vector2(0.5f, 1f));

            UIButton cancel = UIBuilder.SecondaryButton("Cancel", panel, "CANCEL", UITheme.FontSmall);
            UIBuilder.Box(cancel.Root, 0f, 20f, 240f, 76f, new Vector2(0.5f, 0f));
            cancel.OnClick(ClosePicker);

            // Scrollable hero list inside the panel.
            ScrollRect heroScroll;
            _pickerHeroList = UIBuilder.ScrollList("Heroes", panel, out heroScroll,
                                                   10f, new RectOffset(16, 16, 8, 8));
            UIBuilder.FillParent(heroScroll.GetComponent<RectTransform>(),
                                 16f, 72f, 16f, 110f);

            _pickerRoot.gameObject.SetActive(false);
        }

        private void OpenPicker(OwnedRelic relic)
        {
            _pickerRelic = relic;
            _pickerArmorUid = "";
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
            OpenPickerFor(piece.Name + "  (" + ArmorInfo.SlotLabel(piece.Slot) + ")",
                          App.Session.Profile.HeroWearingArmor(piece.Uid));
        }

        private void OpenPickerFor(string itemName, HeroInstance currentHolder)
        {
            _pickerTitle.text = "EQUIP  " + itemName.ToUpperInvariant() + "  TO...";

            // Rebuild hero list.
            for (int i = _pickerHeroList.childCount - 1; i >= 0; i--)
                Object.Destroy(_pickerHeroList.GetChild(i).gameObject);

            bool isAssigned = currentHolder != null;

            UIButton unassignBtn = UIBuilder.SecondaryButton("Unassign", _pickerHeroList,
                                                             "UNEQUIP (return to pool)", UITheme.FontSmall);
            UIBuilder.Sized(unassignBtn.Root, 0f, 72f);
            unassignBtn.Interactable = isAssigned;
            unassignBtn.OnClick(delegate { OnPickerUnassign(); });

            UIBuilder.Divider("Div", _pickerHeroList, UITheme.WithAlpha(UITheme.BorderSoft, 0.5f));

            // One row per hero.
            List<HeroInstance> heroes = App.Session.Profile.Heroes;
            for (int i = 0; i < heroes.Count; i++)
            {
                HeroInstance hero = heroes[i];
                bool isHolder = hero == currentHolder;
                string heroLabel = hero.Def != null ? hero.Def.Name : hero.DefId;
                string buttonText = heroLabel.ToUpperInvariant() + (isHolder ? "  (equipped)" : "");
                UIButton heroBtn = UIBuilder.MakeButton("Hero_" + hero.Uid, _pickerHeroList,
                                                        buttonText, UITheme.FontSmall,
                                                        isHolder ? UITheme.Ember : UITheme.PanelRaised,
                                                        isHolder ? UITheme.EmberDeep : UITheme.Border,
                                                        UITheme.TextPrimary);
                UIBuilder.Sized(heroBtn.Root, 0f, 72f);
                HeroInstance captured = hero;
                heroBtn.OnClick(delegate { OnPickerAssign(captured); });
            }

            if (heroes.Count == 0)
            {
                Text noHeroes = UIBuilder.Label("NoHeroes", _pickerHeroList,
                                                "No heroes in your collection yet.",
                                                UITheme.FontSmall, UITheme.TextDim, TextAnchor.MiddleCenter);
                UIBuilder.Sized(noHeroes.rectTransform, 0f, 60f);
            }

            _pickerRoot.gameObject.SetActive(true);
            _pickerRoot.SetAsLastSibling();
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
            bg.sprite = TextureLab.RoundedRect(18);
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
        /// (temper and aspect) plus a slot label, its stat lines, and three actions rather than one.
        ///
        /// The salvage and reroll buttons carry their PRICES in their labels rather than in a
        /// tooltip nobody on a phone can reach, and both read that price off GameSession - the same
        /// method that charges it - so a shown price and a paid one cannot drift.
        /// </summary>
        private void BuildArmorRow(ArmorPiece piece)
        {
            HeroInstance wearer = App.Session.Profile.HeroWearingArmor(piece.Uid);
            bool equipped = wearer != null;
            Color temperColor = UITheme.ArmorTemperColor(piece.Temper);

            RectTransform card = UIBuilder.Node("Armor_" + piece.Uid, _listContent);
            UIBuilder.Sized(card, 0f, 176f);

            Image bg = card.gameObject.AddComponent<Image>();
            bg.sprite = TextureLab.RoundedRect(18);
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

            // Three stacked actions down the right edge. EQUIP on top because it is the common one;
            // SALVAGE is destructive and sits at the bottom, away from the thumb's resting spot.
            UIButton equipBtn = UIBuilder.PrimaryButton("Equip", card, equipped ? "MOVE" : "EQUIP", UITheme.FontTiny);
            UIBuilder.Box(equipBtn.Root, -18f, -14f, 160f, 46f, new Vector2(1f, 1f));
            ArmorPiece capturedForEquip = piece;
            equipBtn.OnClick(delegate { OpenArmorPicker(capturedForEquip); });

            int rerollCost = App.Session.RerollCost(piece);
            UIButton anvilBtn = UIBuilder.SecondaryButton("Anvil", card, "ANVIL  " + rerollCost, UITheme.FontTiny);
            UIBuilder.Box(anvilBtn.Root, -18f, 0f, 160f, 46f, new Vector2(1f, 0.5f));
            anvilBtn.Interactable = App.Session.Profile.Slag >= rerollCost && rerollCost > 0;
            string capturedUid = piece.Uid;
            anvilBtn.OnClick(delegate { OnAnvil(capturedUid); });

            int salvageValue = App.Session.SalvageValue(piece);
            UIButton salvageBtn = UIBuilder.MakeButton("Salvage", card, "SALVAGE  " + salvageValue, UITheme.FontTiny,
                                                       UITheme.PanelRaised, UITheme.Danger, UITheme.TextPrimary);
            UIBuilder.Box(salvageBtn.Root, -18f, 14f, 160f, 46f, new Vector2(1f, 0f));
            // Salvage refuses an equipped piece in GameSession; the button says so up front rather
            // than letting the tap fail silently.
            salvageBtn.Interactable = !equipped;
            salvageBtn.OnClick(delegate { OnSalvage(capturedUid); });
        }

        private void OnAnvil(string armorUid)
        {
            ArmorPiece piece = App.Session.Profile.FindArmor(armorUid);
            if (piece == null) { RebuildAll(); return; }

            int cost = App.Session.RerollCost(piece);
            if (!App.Session.RerollArmor(armorUid, TimeUtil.NowUnix()))
            {
                App.Notify("Not enough Slag — the Anvil wants " + cost + ".");
                return;
            }

            App.Audio.Tap();
            App.Notify(piece.Name + " reforged: " + piece.DescribeStats());
            App.RequestSave();
            App.RefreshHud();
            RebuildAll();
        }

        private void OnSalvage(string armorUid)
        {
            ArmorPiece piece = App.Session.Profile.FindArmor(armorUid);
            if (piece == null) { RebuildAll(); return; }

            string name = piece.Name;
            int value = App.Session.SalvageValue(piece);
            if (!App.Session.SalvageArmor(armorUid))
            {
                App.Notify("Unequip " + name + " before salvaging it.");
                return;
            }

            App.Audio.Tap();
            App.Notify(name + " salvaged for " + value + " Slag.");
            App.RequestSave();
            App.RefreshHud();
            RebuildAll();
        }

        // ------------------------------------------------------------------ helpers

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
            bg.sprite = TextureLab.RoundedRect(18);
            bg.type = Image.Type.Sliced;
            bg.color = UITheme.Panel;
            UIBuilder.Frame("Edge", card, UITheme.BorderSoft, 18, 2);

            return card;
        }
    }
}
