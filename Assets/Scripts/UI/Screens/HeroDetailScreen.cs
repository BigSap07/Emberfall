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

        private Text _headerSubtitle;
        private RectTransform _identityCard;
        private RectTransform _portraitHost;
        private Text _nameLabel;
        private Text _titleLabel;
        private Text _powerLabel;
        private Text _levelLabel;
        private RectTransform _chipRow;
        private RectTransform _slotRow;
        private readonly UIButton[] _slotButtons = new UIButton[3];

        private RectTransform _statsCard;
        private Text _hpValue, _atkValue, _defValue, _spdValue, _critValue;
        private Text _hpDelta, _atkDelta, _defDelta, _spdDelta;

        private RectTransform _abilityHost;
        private UIButton _levelUpButton;
        private UIButton _levelUpTenButton;
        private Text _capNote;

        public void SetHero(HeroInstance hero)
        {
            _hero = hero;
        }

        protected override void Build()
        {
            ScreenChrome.Header(Root, "HERO", Router, out _headerSubtitle);

            ScrollRect scroll;
            RectTransform content = UIBuilder.ScrollList("Detail", Root, out scroll, 18f, new RectOffset(0, 0, 8, 40));
            UIBuilder.FillParent(scroll.GetComponent<RectTransform>(),
                                 UITheme.ScreenPadding, UITheme.TopBarHeight + 130f, UITheme.ScreenPadding, 236f);

            BuildIdentityCard(content);
            BuildStatsCard(content);
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

            Text slotHint = UIBuilder.Label("SlotHint", _identityCard, "BANNER SLOT", UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperLeft);
            UIBuilder.Box(slotHint.rectTransform, 28f, -218f, 300f, 26f, new Vector2(0f, 1f));

            _slotRow = UIBuilder.Node("Slots", _identityCard);
            UIBuilder.Box(_slotRow, 28f, 24f, 0f, 74f, new Vector2(0f, 0f));
            _slotRow.anchorMin = new Vector2(0f, 0f);
            _slotRow.anchorMax = new Vector2(1f, 0f);
            _slotRow.offsetMin = new Vector2(28f, 24f);
            _slotRow.offsetMax = new Vector2(-28f, 98f);
            UIBuilder.HStack(_slotRow, 12f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            for (int i = 0; i < 3; i++)
            {
                int slot = i;
                UIButton button = UIBuilder.SecondaryButton("Slot" + i, _slotRow, "SLOT " + (i + 1), UITheme.FontSmall);
                UIBuilder.Flexible(button.Root, 1f, 0f);
                button.OnClick(delegate { Deploy(slot); });
                _slotButtons[i] = button;
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
        }

        // ------------------------------------------------------------------ refresh

        public override void OnShow()
        {
            if (_hero == null && App.Session.Profile.Heroes.Count > 0) _hero = App.Session.Profile.Heroes[0];
            App.RefreshHud();
            RebuildStatic();
            Refresh();
        }

        /// <summary>Rebuilds the parts that only change when the selected hero changes.</summary>
        private void RebuildStatic()
        {
            if (_hero == null) return;
            HeroDef def = _hero.Def;

            for (int i = _portraitHost.childCount - 1; i >= 0; i--) Destroy(_portraitHost.GetChild(i).gameObject);
            RectTransform portrait = Widgets.Portrait("Portrait", _portraitHost, def, def.Rarity, 176f, true);
            UIBuilder.FillParent(portrait, 0f);

            for (int i = _chipRow.childCount - 1; i >= 0; i--) Destroy(_chipRow.GetChild(i).gameObject);
            AddChip(UITheme.RarityName(def.Rarity), UITheme.RarityColor(def.Rarity), 180f);
            AddChip(UITheme.RoleName(def.Role), UITheme.RoleColor(def.Role), 190f);
            AddChip(AspectChart.DisplayName(def.Aspect), UITheme.AspectColor(def.Aspect), 170f);

            // ability cards live after the two fixed cards, so clear anything past index 1
            for (int i = _abilityHost.childCount - 1; i >= 2; i--) Destroy(_abilityHost.GetChild(i).gameObject);

            if (def.BasicAttack != null)
                Widgets.AbilityCard("Basic", _abilityHost, "BASIC ATTACK", def.BasicAttack.Name,
                                    def.BasicAttack.Description, UITheme.TextMuted, def.Visual.Sigil);
            if (def.Active != null)
                Widgets.AbilityCard("Active", _abilityHost, "ACTIVE  -  " + def.Active.EnergyCost + " ENERGY", def.Active.Name,
                                    def.Active.Description, UITheme.Energy, def.Visual.Sigil);
            if (def.Passive != null)
                Widgets.AbilityCard("Passive", _abilityHost, "PASSIVE", def.Passive.Name,
                                    def.Passive.Description, UITheme.RarityColor(def.Rarity), def.Visual.Sigil);

            _nameLabel.text = def.Name;
            _titleLabel.text = def.Title.ToUpperInvariant();
            _headerSubtitle.text = def.Lore;
        }

        private void AddChip(string text, Color tint, float width)
        {
            RectTransform chip = UIBuilder.Chip("Chip", _chipRow, text, tint, UITheme.FontTiny);
            UIBuilder.Sized(chip, width, 38f);
        }

        private void Refresh()
        {
            if (_hero == null) return;
            GameSession session = App.Session;
            HeroDef def = _hero.Def;

            UnitStats now = StatMath.ForHero(def, _hero.Level);
            int cap = session.HeroLevelCap;
            bool atCap = _hero.Level >= cap;
            UnitStats next = atCap ? now : StatMath.ForHero(def, _hero.Level + 1);

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
    }
}
