using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.UI
{
    /// <summary>Persistent header: account rank, experience and the two currencies.</summary>
    public sealed class HudTopBar
    {
        public RectTransform Root;

        private Text _rankText;
        private Text _nameText;
        private Image _xpFill;
        private Text _goldText;
        private Text _shardText;
        private Text _dustText;

        public void Build(RectTransform parent)
        {
            Root = UIBuilder.Node("TopBar", parent);
            UIBuilder.TopStrip(Root, UITheme.TopBarHeight, 0f, 0f);

            Image backing = Root.gameObject.AddComponent<Image>();
            backing.sprite = TextureLab.White;
            backing.color = UITheme.WithAlpha(UITheme.BackdropDeep, 0.92f);

            Image underline = UIBuilder.Img("Underline", Root, TextureLab.White, UITheme.WithAlpha(UITheme.Border, 0.85f));
            UIBuilder.Box(underline.rectTransform, 0f, 0f, 0f, 2f, new Vector2(0.5f, 0f));
            underline.rectTransform.anchorMin = new Vector2(0f, 0f);
            underline.rectTransform.anchorMax = new Vector2(1f, 0f);
            underline.rectTransform.sizeDelta = new Vector2(0f, 2f);

            BuildRankBlock();
            BuildCurrencies();
        }

        private void BuildRankBlock()
        {
            RectTransform block = UIBuilder.Node("Rank", Root);
            UIBuilder.Box(block, UITheme.ScreenPadding, -20f, 420f, 92f, new Vector2(0f, 1f));

            RectTransform badge = UIBuilder.Node("Badge", block);
            UIBuilder.Box(badge, 0f, 0f, 84f, 84f, new Vector2(0f, 0.5f));
            badge.anchoredPosition = new Vector2(0f, 0f);
            Image badgeFill = badge.gameObject.AddComponent<Image>();
            badgeFill.sprite = TextureLab.Circle(128);
            badgeFill.color = UITheme.PanelRaised;
            Image badgeRing = UIBuilder.Img("Ring", badge, TextureLab.Ring(128, 8), UITheme.Gold);
            UIBuilder.FillParent(badgeRing.rectTransform, 0f);

            _rankText = UIBuilder.Label("Level", badge, "1", 38, UITheme.Gold, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(_rankText.rectTransform, 0f);

            _nameText = UIBuilder.Label("Name", block, "WAYFARER", UITheme.FontSmall, UITheme.TextMuted, TextAnchor.LowerLeft);
            UIBuilder.Box(_nameText.rectTransform, 100f, -12f, 300f, 30f, new Vector2(0f, 1f));

            _xpFill = UIBuilder.Bar("XpBar", block, UITheme.PanelSunken, UITheme.Shard);
            RectTransform xpRoot = _xpFill.rectTransform.parent as RectTransform;
            UIBuilder.Box(xpRoot, 100f, 16f, 260f, 16f, new Vector2(0f, 0f));
        }

        private void BuildCurrencies()
        {
            _dustText  = BuildCurrencyChip("Dust",   -UITheme.ScreenPadding - 440f, UITheme.Ember, "D");
            _goldText  = BuildCurrencyChip("Gold",   -UITheme.ScreenPadding - 220f, UITheme.Gold,  "G");
            _shardText = BuildCurrencyChip("Shards", -UITheme.ScreenPadding,        UITheme.Shard, "S");
        }

        private Text BuildCurrencyChip(string name, float x, Color tint, string glyph)
        {
            RectTransform chip = UIBuilder.Node(name, Root);
            UIBuilder.Box(chip, x, -24f, 210f, 62f, new Vector2(1f, 1f));

            Image background = chip.gameObject.AddComponent<Image>();
            background.sprite = TextureLab.RoundedRect(18);
            background.type = Image.Type.Sliced;
            background.color = UITheme.WithAlpha(UITheme.PanelSunken, 0.95f);
            UIBuilder.Frame("Edge", chip, UITheme.WithAlpha(tint, 0.45f), 18, 2);

            RectTransform icon = UIBuilder.Node("Icon", chip);
            UIBuilder.Box(icon, 8f, 0f, 44f, 44f, new Vector2(0f, 0.5f));
            Image iconFill = icon.gameObject.AddComponent<Image>();
            iconFill.sprite = TextureLab.Circle(96);
            iconFill.color = UITheme.WithAlpha(tint, 0.9f);
            iconFill.raycastTarget = false;
            Text iconGlyph = UIBuilder.Label("Glyph", icon, glyph, 26, UITheme.BackdropDeep, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(iconGlyph.rectTransform, 0f);

            Text value = UIBuilder.Label("Value", chip, "0", UITheme.FontSmall, UITheme.TextPrimary, TextAnchor.MiddleRight);
            UIBuilder.Box(value.rectTransform, -14f, 0f, 140f, 44f, new Vector2(1f, 0.5f));
            return value;
        }

        public void Refresh(GameSession session)
        {
            if (session == null) return;
            _rankText.text = session.Profile.PlayerLevel.ToString();
            _nameText.text = session.Profile.PlayerName.ToUpperInvariant() + "  -  RANK " + session.Profile.PlayerLevel;

            int needed = StatMath.PlayerXpForNextLevel(session.Balance, session.Profile.PlayerLevel);
            UIBuilder.SetBar(_xpFill, needed > 0 ? session.Profile.PlayerXp / (float)needed : 0f);

            _dustText.text  = Format(session.Profile.RelicDust);
            _goldText.text  = Format(session.Profile.Gold);
            _shardText.text = Format(session.Profile.Shards);
        }

        public static string Format(long value)
        {
            if (value < 10000) return value.ToString();
            if (value < 1000000) return (value / 1000f).ToString("0.#") + "K";
            return (value / 1000000f).ToString("0.##") + "M";
        }
    }
}
