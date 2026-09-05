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
            backing.color = UITheme.WithAlpha(HubStyle.Plate, HubStyle.BarBackingAlpha);

            Image underline = UIBuilder.Img("Underline", Root, TextureLab.White,
                                            UITheme.WithAlpha(HubStyle.BorderWarm, HubStyle.BorderAlpha));
            UIBuilder.Box(underline.rectTransform, 0f, 0f, 0f, 2f, new Vector2(0.5f, 0f));
            underline.rectTransform.anchorMin = new Vector2(0f, 0f);
            underline.rectTransform.anchorMax = new Vector2(1f, 0f);
            underline.rectTransform.sizeDelta = new Vector2(0f, 2f);

            BuildRankBlock();
            BuildCurrencies();
        }

        /// <summary>
        /// Rank, name and XP in the same pill language as the currencies rather than as bare text
        /// beside a raw bar. Same height, same radius, same surface; the level disc mirrors a
        /// currency pill's icon so the whole bar reads as one row of the same object.
        ///
        /// Nothing is dropped: the level number, the "NAME - RANK n" line and the XP fill are all
        /// still here, just seated on a plate and measured from HubStyle instead of from literals.
        /// </summary>
        private void BuildRankBlock()
        {
            RectTransform block = UIBuilder.Node("Rank", Root);
            UIBuilder.Box(block, UITheme.ScreenPadding, -(UITheme.TopBarHeight - HubStyle.PillHeight) * 0.5f,
                          HubStyle.PillRankWidth, HubStyle.PillHeight, new Vector2(0f, 1f));
            HubStyle.Surface(block, HubStyle.RadiusPill, HubStyle.PlateAlpha,
                             HubStyle.BorderAlpha, HubStyle.BorderWeight);

            // The pill already IS the player - it carries their name, their rank and their XP - so
            // it is the thing to press to see and change that, rather than a separate Hub node
            // competing with the buildings for a slot on the map. Nothing about the pill changes
            // visually: it gains a Button and keeps every pixel, because a top bar that suddenly
            // looked pressable would read as a new feature rather than as the same object.
            //
            // Wired through GameApp.Instance rather than a Router field because this bar is built
            // once, before the router exists - the same reason ScreenChrome reaches for Instance
            // for its tap sound.
            Button open = block.gameObject.AddComponent<Button>();
            open.transition = Selectable.Transition.None;
            open.onClick.AddListener(delegate
            {
                Game.GameApp app = Game.GameApp.Instance;
                if (app == null || app.Router == null) return;
                app.Audio.Tap();
                app.Router.Show(ScreenId.Profile);
            });

            RectTransform badge = UIBuilder.Node("Badge", block);
            UIBuilder.Box(badge, HubStyle.PillPadX, 0f, HubStyle.PillIcon, HubStyle.PillIcon, new Vector2(0f, 0.5f));
            Image badgeFill = badge.gameObject.AddComponent<Image>();
            badgeFill.sprite = TextureLab.Circle(128);
            badgeFill.color = HubStyle.ValueGold;
            badgeFill.raycastTarget = false;

            _rankText = UIBuilder.Label("Level", badge, "1", HubStyle.FontRankValue,
                                        UITheme.TextOnAccent, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(_rankText.rectTransform, 0f);

            float textLeft = HubStyle.PillPadX + HubStyle.PillIcon + HubStyle.PillGapIconToValue;
            float textWidth = HubStyle.PillRankWidth - textLeft - HubStyle.PillPadX;

            _nameText = UIBuilder.Label("Name", block, "WAYFARER", HubStyle.FontCaption,
                                        HubStyle.TextSecondary, TextAnchor.LowerLeft);
            UIBuilder.Box(_nameText.rectTransform, textLeft, -HubStyle.PillPadY, textWidth, 24f, new Vector2(0f, 1f));

            _xpFill = UIBuilder.Bar("XpBar", block, UITheme.WithAlpha(HubStyle.Plate, HubStyle.PlateAlphaSolid),
                                    HubStyle.ValueGold);
            RectTransform xpRoot = _xpFill.rectTransform.parent as RectTransform;
            UIBuilder.Box(xpRoot, textLeft, HubStyle.PillPadY, textWidth, 10f, new Vector2(0f, 0f));
        }

        /// <summary>
        /// The three currencies, each a HubStyle pill. Same height, same horizontal padding, same
        /// surface and the same gold value tone; only the icon tint tells them apart. Spacing is
        /// derived from the shared pill width rather than from three hand-placed offsets, which is
        /// what the -440/-220/0 literals here used to be.
        /// </summary>
        private void BuildCurrencies()
        {
            float step = HubStyle.PillCurrencyWidth + UITheme.Space12;
            _dustText  = HubStyle.Pill("Dust",   Root, -UITheme.ScreenPadding - step * 2f,
                                       HubStyle.PillCurrencyWidth, UITheme.Dust,  "D");
            _goldText  = HubStyle.Pill("Gold",   Root, -UITheme.ScreenPadding - step,
                                       HubStyle.PillCurrencyWidth, UITheme.Gold,  "G");
            _shardText = HubStyle.Pill("Shards", Root, -UITheme.ScreenPadding,
                                       HubStyle.PillCurrencyWidth, UITheme.Shard, "S");
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
