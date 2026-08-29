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
    /// <summary>Everything about one hero, and the only place gold turns into power.</summary>
    public sealed class HeroDetailScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.HeroDetail; } }

        private HeroInstance _hero;
        private HeroDef _lockedDef;

        private Text _headerSubtitle;
        private RectTransform _identityCard;
        private RectTransform _portraitHost;
        private Text _nameLabel;
        private Text _titleLabel;
        private Text _powerLabel;
        private Text _levelLabel;
        private RectTransform _chipRow;
        private Text _slotHint;
        private RectTransform _slotRow;
        private readonly UIButton[] _slotButtons = new UIButton[PlayerProfile.TeamSize];

        private RectTransform _statsCard;
        private Text _hpValue, _atkValue, _defValue, _spdValue, _critValue;
        private Text _hpDelta, _atkDelta, _defDelta, _spdDelta;

        private RectTransform _abilityHost;

        // The armor block: six fixed slot rows plus one attunement line. Read-only by design -
        // equipping happens in one place (InventoryScreen), and a second equip path would mean two
        // screens that could disagree about what is worn.
        private RectTransform _armorCard;
        private Text[] _armorSlotLabels;
        private Text[] _armorSlotValues;
        private Text _attunementLabel;
        private UIButton _levelUpButton;
        private UIButton _levelUpTenButton;
        private UIButton _obtainButton;
        private Text _capNote;

        /// <summary>The definition backing the screen right now, whichever mode it's in.</summary>
        private HeroDef CurrentDef
        {
            get { return _hero != null ? _hero.Def : _lockedDef; }
        }

        public void SetHero(HeroInstance hero)
        {
            _hero = hero;
            _lockedDef = null;
        }

        /// <summary>A hero the player hasn't summoned yet - shows identity and abilities, not stats or deploy/level actions.</summary>
        public void SetLockedHero(HeroDef def)
        {
            _lockedDef = def;
            _hero = null;
        }

        protected override void Build()
        {
            ScreenChrome.Header(Root, "HERO", Router, out _headerSubtitle);

            // Full canvas width, side gutters carried by the content padding - the same shape
            // RelicShopScreen and InventoryScreen use for their ScrollLists.
            ScrollRect scroll;
            RectTransform content = UIBuilder.ScrollList("Detail", Root, out scroll, 18f, new RectOffset(32, 32, 8, 40));
            UIBuilder.FillParent(scroll.GetComponent<RectTransform>(),
                                 0f, UITheme.TopBarHeight + 130f, 0f, 236f);

            BuildIdentityCard(content);
            BuildStatsCard(content);
            BuildArmorCard(content);
            _abilityHost = content;

            BuildActionBar();
        }

        private void BuildIdentityCard(RectTransform parent)
        {
            _identityCard = UIBuilder.Node("Identity", parent);
            UIBuilder.Sized(_identityCard, 0f, 348f);

            Image background = _identityCard.gameObject.AddComponent<Image>();
            background.sprite = TextureLab.RoundedRect(22);
            background.type = Image.Type.Sliced;
            background.color = UITheme.Panel;
            UIBuilder.Frame("Edge", _identityCard, UITheme.BorderSoft, 22, 2);

            _portraitHost = UIBuilder.Node("PortraitHost", _identityCard);
            UIBuilder.Box(_portraitHost, 28f, -24f, 176f, 176f, new Vector2(0f, 1f));

            _nameLabel = UIBuilder.Label("Name", _identityCard, "", UITheme.FontTitle, UITheme.TextPrimary, TextAnchor.UpperLeft);
            UIBuilder.Box(_nameLabel.rectTransform, 224f, -26f, 620f, 50f, new Vector2(0f, 1f));

            _titleLabel = UIBuilder.Label("Title", _identityCard, "", UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperLeft);
            UIBuilder.Box(_titleLabel.rectTransform, 224f, -74f, 620f, 28f, new Vector2(0f, 1f));

            _chipRow = UIBuilder.Node("Chips", _identityCard);
            UIBuilder.Box(_chipRow, 224f, -110f, 620f, 38f, new Vector2(0f, 1f));
            UIBuilder.HStack(_chipRow, 10f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

            _levelLabel = UIBuilder.Label("Level", _identityCard, "", UITheme.FontHeading, UITheme.TextPrimary, TextAnchor.UpperLeft);
            UIBuilder.Box(_levelLabel.rectTransform, 224f, -158f, 300f, 44f, new Vector2(0f, 1f));

            _powerLabel = UIBuilder.Label("Power", _identityCard, "", UITheme.FontHeading, UITheme.Gold, TextAnchor.UpperRight);
            UIBuilder.Box(_powerLabel.rectTransform, -28f, -158f, 320f, 44f, new Vector2(1f, 1f));

            _slotHint = UIBuilder.Label("SlotHint", _identityCard, "BANNER SLOT", UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperLeft);
            UIBuilder.Box(_slotHint.rectTransform, 28f, -218f, 300f, 26f, new Vector2(0f, 1f));

            _slotRow = UIBuilder.Node("Slots", _identityCard);
            UIBuilder.Box(_slotRow, 28f, 24f, 0f, 74f, new Vector2(0f, 0f));
            _slotRow.anchorMin = new Vector2(0f, 0f);
            _slotRow.anchorMax = new Vector2(1f, 0f);
            _slotRow.offsetMin = new Vector2(28f, 24f);
            _slotRow.offsetMax = new Vector2(-28f, 98f);
            UIBuilder.HStack(_slotRow, 12f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            for (int i = 0; i < PlayerProfile.TeamSize; i++)
            {
                int slot = i;
                UIButton button = UIBuilder.SecondaryButton("Slot" + i, _slotRow, "SLOT " + (i + 1), UITheme.FontSmall);
                UIBuilder.Flexible(button.Root, 1f, 0f);
                button.OnClick(delegate { Deploy(slot); });
                _slotButtons[i] = button;
            }
        }

        /// <summary>
        /// The six armor slots and the wearer's attunement count. Built once with fixed rows -
        /// there are always exactly ArmorInfo.AllSlots.Length of them, empty or not, because a slot
        /// a hero has not filled is information rather than absence, and a list that only showed
        /// what was worn would hide the six-slot shape from a player who owns two pieces.
        /// </summary>
        private void BuildArmorCard(RectTransform parent)
        {
            int slots = ArmorInfo.AllSlots.Length;
            _armorCard = UIBuilder.Node("Armor", parent);
            UIBuilder.Sized(_armorCard, 0f, 62f + slots * 44f + 44f);

            Image background = _armorCard.gameObject.AddComponent<Image>();
            background.sprite = TextureLab.RoundedRect(22);
            background.type = Image.Type.Sliced;
            background.color = UITheme.Panel;
            UIBuilder.Frame("Edge", _armorCard, UITheme.BorderSoft, 22, 2);

            Text heading = UIBuilder.Label("Heading", _armorCard, "ARMOR",
                                           UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperLeft);
            UIBuilder.Box(heading.rectTransform, 26f, -20f, 300f, 26f, new Vector2(0f, 1f));

            _armorSlotLabels = new Text[slots];
            _armorSlotValues = new Text[slots];
            for (int i = 0; i < slots; i++)
            {
                float y = -58f - i * 44f;

                _armorSlotLabels[i] = UIBuilder.Label("SlotName" + i, _armorCard,
                                                      ArmorInfo.SlotLabel(ArmorInfo.AllSlots[i]),
                                                      UITheme.FontSmall, UITheme.TextMuted, TextAnchor.MiddleLeft);
                UIBuilder.Box(_armorSlotLabels[i].rectTransform, 26f, y, 300f, 40f, new Vector2(0f, 1f));

                _armorSlotValues[i] = UIBuilder.Label("SlotValue" + i, _armorCard, "",
                                                      UITheme.FontSmall, UITheme.TextDim, TextAnchor.MiddleRight);
                _armorSlotValues[i].rectTransform.anchorMin = new Vector2(0f, 1f);
                _armorSlotValues[i].rectTransform.anchorMax = new Vector2(1f, 1f);
                _armorSlotValues[i].rectTransform.pivot = new Vector2(0.5f, 1f);
                _armorSlotValues[i].rectTransform.offsetMin = new Vector2(330f, y - 20f);
                _armorSlotValues[i].rectTransform.offsetMax = new Vector2(-26f, y + 20f);
            }

            _attunementLabel = UIBuilder.WrappedLabel("Attunement", _armorCard, "",
                                                      UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperLeft);
            _attunementLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            _attunementLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
            _attunementLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
            _attunementLabel.rectTransform.offsetMin = new Vector2(26f, 12f);
            _attunementLabel.rectTransform.offsetMax = new Vector2(-26f, 46f);
        }

        /// <summary>
        /// Fills the armor block for the current hero. `resolved` is the stat line armor is already
        /// folded into - passed in rather than recomputed so this cannot show a total the card above
        /// it disagrees with.
        /// </summary>
        private void RefreshArmorBlock(GameSession session, UnitStats resolved)
        {
            PlayerProfile profile = session.Profile;

            for (int i = 0; i < ArmorInfo.AllSlots.Length; i++)
            {
                ArmorPiece piece = profile.ArmorInSlot(_hero, ArmorInfo.AllSlots[i]);
                if (piece == null)
                {
                    _armorSlotValues[i].text = "empty";
                    _armorSlotValues[i].color = UITheme.TextDim;
                    continue;
                }
                _armorSlotValues[i].text = piece.Name + "   <color=#6C6382>" + ArmorInfo.TemperLabel(piece.Temper) + "</color>";
                // Tinted by the piece's aspect, so the rows a player is counting toward an
                // attunement are the ones that visually group.
                _armorSlotValues[i].color = piece.Aspect == _hero.Def.Aspect
                                          ? UITheme.AspectColor(piece.Aspect)
                                          : UITheme.TextMuted;
            }

            int matching = profile.AttunementCount(_hero);
            string aspectName = AspectChart.DisplayName(_hero.Def.Aspect);
            List<ArmorAttunementDef> met = session.Content.Armor.AttunementsFor(_hero.Def.Aspect, matching);

            if (met.Count == 0)
            {
                _attunementLabel.text = "Attunement  " + matching + "/6 " + aspectName
                                      + "  -  2 matching pieces unlocks the first bonus.";
                _attunementLabel.color = UITheme.TextDim;
            }
            else
            {
                string names = "";
                for (int i = 0; i < met.Count; i++)
                    names += (names.Length > 0 ? ", " : "") + met[i].Name + " (" + met[i].Pieces + ")";
                _attunementLabel.text = "Attunement  " + matching + "/6 " + aspectName + "  -  " + names;
                _attunementLabel.color = UITheme.AspectColor(_hero.Def.Aspect);
            }
        }

        private void BuildStatsCard(RectTransform parent)
        {
            _statsCard = UIBuilder.Node("Stats", parent);
            UIBuilder.Sized(_statsCard, 0f, 330f);

            Image background = _statsCard.gameObject.AddComponent<Image>();
            background.sprite = TextureLab.RoundedRect(22);
            background.type = Image.Type.Sliced;
            background.color = UITheme.PanelSunken;
            UIBuilder.Frame("Edge", _statsCard, UITheme.BorderSoft, 22, 2);

            RectTransform list = UIBuilder.Node("List", _statsCard);
            UIBuilder.FillParent(list, 16f, 16f, 16f, 16f);
            UIBuilder.VStack(list, 4f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);

            _hpValue = Widgets.StatRow("Hp", list, "HEALTH", "-", UITheme.TextPrimary, 58f);
            _hpDelta = AttachDelta(_hpValue);
            _atkValue = Widgets.StatRow("Atk", list, "ATTACK", "-", UITheme.TextPrimary, 58f);
            _atkDelta = AttachDelta(_atkValue);
            _defValue = Widgets.StatRow("Def", list, "GUARD", "-", UITheme.TextPrimary, 58f);
            _defDelta = AttachDelta(_defValue);
            _spdValue = Widgets.StatRow("Spd", list, "SPEED", "-", UITheme.TextPrimary, 58f);
            _spdDelta = AttachDelta(_spdValue);
            _critValue = Widgets.StatRow("Crit", list, "CRITICAL", "-", UITheme.TextPrimary, 58f);
        }

        /// <summary>Green "+n" preview hung to the left of a stat value.</summary>
        private static Text AttachDelta(Text value)
        {
            Text delta = UIBuilder.Label("Delta", value.rectTransform.parent, "", UITheme.FontTiny,
                                         UITheme.Success, TextAnchor.MiddleRight);
            UIBuilder.FillParent(delta.rectTransform, 0f, 0f, 120f, 0f);
            return delta;
        }

        private void BuildActionBar()
        {
            RectTransform bar = UIBuilder.Node("Actions", Root);
            UIBuilder.BottomStrip(bar, 200f, 0f, 0f);

            Image backing = bar.gameObject.AddComponent<Image>();
            backing.sprite = TextureLab.White;
            backing.color = UITheme.WithAlpha(UITheme.BackdropDeep, 0.94f);

            _capNote = UIBuilder.Label("CapNote", bar, "", UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperCenter);
            UIBuilder.Box(_capNote.rectTransform, 0f, -12f, 900f, 26f, new Vector2(0.5f, 1f));

            _levelUpButton = UIBuilder.GoldButton("LevelUp", bar, "LEVEL UP", UITheme.FontHeading);
            UIBuilder.Box(_levelUpButton.Root, UITheme.ScreenPadding, 28f, 640f, 116f, new Vector2(0f, 0f));
            _levelUpButton.OnClick(delegate { LevelUp(1); });

            _levelUpTenButton = UIBuilder.SecondaryButton("LevelUpTen", bar, "x10", UITheme.FontHeading);
            UIBuilder.Box(_levelUpTenButton.Root, -UITheme.ScreenPadding, 28f, 340f, 116f, new Vector2(1f, 0f));
            _levelUpTenButton.OnClick(delegate { LevelUp(10); });

            _obtainButton = UIBuilder.GoldButton("Obtain", bar, "OPEN THE RIFT", UITheme.FontHeading);
            UIBuilder.Box(_obtainButton.Root, 0f, 28f, 980f, 116f, new Vector2(0.5f, 0f));
            _obtainButton.OnClick(GoToSummon);
        }

        // ------------------------------------------------------------------ refresh

        public override void OnShow()
        {
            if (_hero == null && _lockedDef == null && App.Session.Profile.Heroes.Count > 0) _hero = App.Session.Profile.Heroes[0];
            App.RefreshHud();
            RebuildStatic();
            Refresh();
        }

        /// <summary>Rebuilds the parts that only change when the selected hero changes.</summary>
        private void RebuildStatic()
        {
            HeroDef def = CurrentDef;
            if (def == null) return;
            bool locked = _hero == null;

            for (int i = _portraitHost.childCount - 1; i >= 0; i--) Destroy(_portraitHost.GetChild(i).gameObject);
            RectTransform portrait = Widgets.Portrait("Portrait", _portraitHost, def, def.Stars, 176f, !locked);
            UIBuilder.FillParent(portrait, 0f);
            if (locked)
            {
                // dimmed silhouette - same identity chips below, but the portrait itself stays a shape, not a reveal
                Image scrim = UIBuilder.Img("Scrim", portrait, TextureLab.Circle(160), UITheme.ScrimModal);
                UIBuilder.FillParent(scrim.rectTransform, 0f);
            }

            for (int i = _chipRow.childCount - 1; i >= 0; i--) Destroy(_chipRow.GetChild(i).gameObject);
            Widgets.StarRow("Stars", _chipRow, def.Stars, 22f);
            AddChip(UITheme.RoleName(def.Role), UITheme.RoleColor(def.Role), 190f);
            AddChip(AspectChart.DisplayName(def.Aspect), UITheme.AspectColor(def.Aspect), 170f);

            // ability cards live after the three fixed cards (identity, stats, armor), so clear
            // anything past index 2. This index tracks BuildArmorCard's position in Build() - it
            // was 2 when there were two fixed cards, and it must move with them.
            for (int i = _abilityHost.childCount - 1; i >= 3; i--) Destroy(_abilityHost.GetChild(i).gameObject);

            if (def.BasicAttack != null)
                Widgets.AbilityCard("Basic", _abilityHost, "BASIC ATTACK", def.BasicAttack.Name,
                                    def.BasicAttack.Description, UITheme.TextMuted, def.Visual.Sigil);
            if (def.Active != null)
                Widgets.AbilityCard("Active", _abilityHost, "ACTIVE  -  " + def.Active.EnergyCost + " ENERGY", def.Active.Name,
                                    def.Active.Description, UITheme.Energy, def.Visual.Sigil);
            if (def.Passive != null)
                Widgets.AbilityCard("Passive", _abilityHost, "PASSIVE", def.Passive.Name,
                                    def.Passive.Description, UITheme.StarColor(def.Stars), def.Visual.Sigil);

            _nameLabel.text = def.Name;
            _titleLabel.text = def.Title.ToUpperInvariant();
            _headerSubtitle.text = def.Lore;

            _statsCard.gameObject.SetActive(!locked);
            _armorCard.gameObject.SetActive(!locked);
            _slotHint.gameObject.SetActive(!locked);
            _slotRow.gameObject.SetActive(!locked);
            _levelUpButton.Root.gameObject.SetActive(!locked);
            _levelUpTenButton.Root.gameObject.SetActive(!locked);
            _obtainButton.Root.gameObject.SetActive(locked);
        }

        private void AddChip(string text, Color tint, float width)
        {
            RectTransform chip = UIBuilder.Chip("Chip", _chipRow, text, tint, UITheme.FontTiny);
            UIBuilder.Sized(chip, width, 38f);
        }

        private void Refresh()
        {
            if (_hero == null)
            {
                RefreshLocked();
                return;
            }
            GameSession session = App.Session;
            HeroDef def = _hero.Def;

            // The equipped relic is folded into the preview, not just into the battle. A common
            // relic's whole contribution is these numbers, so a card that showed the unrelicced
            // line would be showing a stat block the simulation does not use. Both calls take it,
            // so the per-level deltas below stay honest too: they compare like with like rather
            // than reading as a relic's bonus arriving on the next level up.
            RelicDef relic = string.IsNullOrEmpty(_hero.EquippedRelicId)
                           ? null : session.Content.GetRelic(_hero.EquippedRelicId);

            // Armor is folded into the preview for exactly the reason the relic above it is: a
            // card showing the unarmored line would be showing a stat block the simulation does not
            // use. Both calls take it, so the per-level deltas stay honest - they compare like with
            // like rather than reading as a piece's bonus arriving on the next level up.
            List<ArmorPiece> armor = session.Profile.EquippedArmorOf(_hero);
            ArmorConfig armorConfig = session.Content.Armor;

            UnitStats now = StatMath.ForHero(def, _hero.Level, relic, armor, armorConfig);
            int cap = session.HeroLevelCap;
            bool atCap = _hero.Level >= cap;
            UnitStats next = atCap ? now : StatMath.ForHero(def, _hero.Level + 1, relic, armor, armorConfig);

            RefreshArmorBlock(session, now);

            _levelLabel.text = "LEVEL " + _hero.Level + "<color=#6C6382> / " + cap + "</color>";
            _powerLabel.text = "POWER " + now.Power;

            _hpValue.text = Whole(now.MaxHp);
            _atkValue.text = Whole(now.Attack);
            _defValue.text = Whole(now.Defense);
            _spdValue.text = Whole(now.Speed);
            _critValue.text = Pct(now.CritChance) + "  x" + now.CritDamage.ToString("0.00", CultureInfo.InvariantCulture);

            SetDelta(_hpDelta, next.MaxHp - now.MaxHp, atCap);
            SetDelta(_atkDelta, next.Attack - now.Attack, atCap);
            SetDelta(_defDelta, next.Defense - now.Defense, atCap);
            SetDelta(_spdDelta, next.Speed - now.Speed, atCap);

            int cost = StatMath.LevelUpCost(session.Balance, _hero.Level);
            bool affordable = !atCap && session.Profile.Gold >= cost;

            _levelUpButton.SetText(atCap ? "LEVEL CAP" : "LEVEL UP   " + cost + "G");
            _levelUpButton.Interactable = affordable;
            _levelUpTenButton.Interactable = affordable;

            _capNote.text = atCap
                ? "Raise your Wayfarer rank in the campaign to lift the level cap."
                : "Next level costs " + cost + " gold  -  you hold " + session.Profile.Gold;

            int deployedSlot = session.Profile.TeamSlotOf(_hero.Uid);
            for (int i = 0; i < _slotButtons.Length; i++)
            {
                bool active = deployedSlot == i;
                _slotButtons[i].Background.color = active ? UITheme.Success : UITheme.PanelRaised;
                _slotButtons[i].Frame.color = active ? UITheme.Success : UITheme.Border;
                _slotButtons[i].Label.color = active ? UITheme.TextOnAccent : UITheme.TextPrimary;
                HeroInstance occupant = session.Profile.FindHeroByUid(session.Profile.Team[i]);
                _slotButtons[i].SetText(active ? "DEPLOYED " + (i + 1)
                                              : (occupant != null ? occupant.Def.Name.ToUpperInvariant() : "SLOT " + (i + 1)));
            }
        }

        /// <summary>No HeroInstance exists for a locked hero, so there's no level/power/deploy state to show.</summary>
        private void RefreshLocked()
        {
            if (_lockedDef == null) return;
            _levelLabel.text = "";
            _powerLabel.text = "LOCKED";
            _capNote.text = "Not yet summoned. Open the Rift and try for " + _lockedDef.Name + ".";
        }

        private static void SetDelta(Text label, float delta, bool atCap)
        {
            if (label == null) return;
            if (atCap || delta <= 0.5f) { label.text = ""; return; }
            label.text = "+" + Mathf.RoundToInt(delta);
        }

        private static string Whole(float value)
        {
            return Mathf.RoundToInt(value).ToString();
        }

        private static string Pct(float value)
        {
            return Mathf.RoundToInt(value * 100f) + "%";
        }

        // ------------------------------------------------------------------ actions

        private void LevelUp(int times)
        {
            if (_hero == null) return;
            LevelUpResult result = App.Session.LevelUpHero(_hero, times);
            if (!result.Success)
            {
                App.Notify(result.Message);
                return;
            }
            App.Audio.Reward();
            App.Notify(_hero.Def.Name + " reached level " + result.NewLevel + ".");
            App.RefreshHud();
            App.RequestSave();
            Refresh();
        }

        private void Deploy(int slot)
        {
            if (_hero == null) return;
            App.Audio.Tap();
            App.Session.Profile.AssignToTeam(slot, _hero.Uid);
            App.RequestSave();
            App.Notify(_hero.Def.Name + " deployed to slot " + (slot + 1) + ".");
            Refresh();
        }

        private void GoToSummon()
        {
            App.Audio.Tap();
            Router.Show(ScreenId.Summon);
        }
    }
}
