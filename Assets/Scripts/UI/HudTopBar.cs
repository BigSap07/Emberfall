using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.UI
{
    /// <summary>Persistent header: account rank, experience and the two currencies.</summary>
    public sealed class HudTopBar
    {
        /// <summary>The face on the plate, and the rank disc that sits on its corner. Derived from
        /// the pill's own height so the plate retunes from HubStyle like everything else here.</summary>
        private const float AvatarSize = HubStyle.PillHeight - HubStyle.PillPadY * 2f;
        private const float RankBadge = 30f;
        private const float AvatarGlyphInset = 7f;

        /// <summary>Room for "1,240 / 3,000 XP" at the quietest size on the bar.</summary>
        private const float XpTextWidth = 190f;

        public RectTransform Root;

        private Image _backing;
        private RectTransform _avatarHost;
        private string _avatarBuiltFor = null;

        private Text _rankText;
        private Text _nameText;
        private Text _xpText;
        private Image _xpFill;
        private Image _underline;
        private Text _goldText;
        private Text _shardText;
        private Text _dustText;

        public void Build(RectTransform parent)
        {
            Root = UIBuilder.Node("TopBar", parent);
            UIBuilder.TopStrip(Root, UITheme.TopBarHeight, 0f, 0f);

            _backing = Root.gameObject.AddComponent<Image>();
            _backing.sprite = TextureLab.White;
            _backing.color = UITheme.WithAlpha(HubStyle.Plate, HubStyle.BarBackingAlpha);

            Image underline = UIBuilder.Img("Underline", Root, TextureLab.White,
                                            UITheme.WithAlpha(HubStyle.BorderWarm, HubStyle.BorderAlpha));
            UIBuilder.Box(underline.rectTransform, 0f, 0f, 0f, 2f, new Vector2(0.5f, 0f));
            underline.rectTransform.anchorMin = new Vector2(0f, 0f);
            underline.rectTransform.anchorMax = new Vector2(1f, 0f);
            underline.rectTransform.sizeDelta = new Vector2(0f, 2f);
            _underline = underline;

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
            open.onClick.AddListener(delegate { OpenPlayerPanel(); });

            // THE FACE, which is what this plate was missing and why it read as an empty bar. It
            // carried a gold disc with a level number in it beside a name and an XP fill, which is
            // a progress bar with a digit on the end rather than a person. The disc is still here,
            // it is still the rank, and it is still the only gold thing on the bar - but it is a
            // badge ON the portrait now, and the portrait is what a player recognises as
            // themselves.
            //
            // The avatar is a PLACEHOLDER by default, deliberately rather than pending: the
            // profile screen has always let a player make any hero they own their face, and a
            // fresh account has not picked one. A ringed disc with the Cinder in it says "this is
            // you, and you have not chosen a face yet" without pretending to be a portrait.
            _avatarHost = UIBuilder.Node("Avatar", block);
            UIBuilder.Box(_avatarHost, HubStyle.PillPadX, 0f, AvatarSize, AvatarSize, new Vector2(0f, 0.5f));

            RectTransform badge = UIBuilder.Node("Badge", _avatarHost);
            UIBuilder.Box(badge, 2f, -2f, RankBadge, RankBadge, new Vector2(1f, 0f));
            Image badgeFill = badge.gameObject.AddComponent<Image>();
            badgeFill.sprite = HudSkin.Circle(HudSkin.Disc, 128);
            badgeFill.color = HubStyle.ValueGold;
            badgeFill.raycastTarget = false;

            _rankText = UIBuilder.Label("Level", badge, "1", HubStyle.FontCaption,
                                        UITheme.TextOnAccent, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(_rankText.rectTransform, 0f);

            float textLeft = HubStyle.PillPadX + AvatarSize + HubStyle.PillGapIconToValue;
            float textWidth = HubStyle.PillRankWidth - textLeft - HubStyle.PillPadX;

            _nameText = UIBuilder.Label("Name", block, "WAYFARER", HubStyle.FontCaption,
                                        HubStyle.LabelText, TextAnchor.LowerLeft);
            UIBuilder.Box(_nameText.rectTransform, textLeft, -HubStyle.PillPadY, textWidth - XpTextWidth,
                          24f, new Vector2(0f, 1f));

            // The XP NUMBERS, on the name's own line, right-aligned against the pill's inner edge.
            //
            // Without them the plate was a name and a track, and a track at zero is a black bar the
            // width of the plate - which is exactly how the Hub's player plate was described when
            // this pass started, and it was a fair description. A number beside it says what the
            // bar is measuring even when the bar itself has nothing to show.
            _xpText = UIBuilder.Label("Xp", block, "", HubStyle.FontSoon,
                                      HubStyle.TextSecondary, TextAnchor.LowerRight);
            UIBuilder.Box(_xpText.rectTransform, -HubStyle.PillPadX, -HubStyle.PillPadY,
                          XpTextWidth, 24f, new Vector2(1f, 1f));

            _xpFill = UIBuilder.Bar("XpBar", block, UITheme.WithAlpha(HubStyle.Plate, HubStyle.PlateAlpha),
                                    HubStyle.ValueGold);
            RectTransform xpRoot = _xpFill.rectTransform.parent as RectTransform;
            UIBuilder.Box(xpRoot, textLeft, HubStyle.PillPadY, textWidth, 8f, new Vector2(0f, 0f));
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

        /// <summary>
        /// The plate's two doors, decided by where the player already is.
        ///
        /// On the Hub it opens the player PANEL - profile, account, friends and settings as tabs
        /// over the dimmed map - which is the whole reason the plate is pressable. Anywhere else
        /// it opens the profile PAGE, because a panel hosts the router's own screens, and hosting
        /// one the router is currently showing would leave the page behind it empty when the
        /// panel closed. One rule, checked in one place.
        /// </summary>
        private static void OpenPlayerPanel()
        {
            Game.GameApp app = Game.GameApp.Instance;
            if (app == null || app.Router == null) return;
            app.Audio.Tap();

            if (app.Panels != null && app.Router.Current != null
                && app.Router.Current.Id == ScreenId.MainMenu)
            {
                app.Panels.ShowProfile("PROFILE");
                return;
            }
            app.Router.Show(ScreenId.Profile);
        }

        /// <summary>Hides the full-width plate behind the bar and the hairline under it, leaving
        /// the pills themselves untouched. The Hub asks for this - see
        /// GameApp.SetTopBarBackingVisible for why.</summary>
        public void SetBackingVisible(bool visible)
        {
            if (_backing != null) _backing.enabled = visible;
            if (_underline != null) _underline.enabled = visible;
        }

        /// <summary>
        /// Draws the player's chosen face, or the placeholder when they have not chosen one.
        ///
        /// Rebuilt only when the CHOICE changes. Refresh runs on every screen change and after
        /// every currency movement, and a portrait torn down and rebuilt that often would be a
        /// steady drip of garbage for a picture that changes twice a month.
        /// </summary>
        private void RefreshAvatar(GameSession session)
        {
            if (_avatarHost == null) return;

            string chosenId = session.Profile.AvatarHeroId;
            if (string.IsNullOrEmpty(chosenId) || !session.Profile.Owns(chosenId)) chosenId = "";
            if (_avatarBuiltFor == chosenId) return;
            _avatarBuiltFor = chosenId;

            // The rank badge is a child of this host and has to survive the rebuild, so only the
            // portrait or the placeholder is cleared.
            for (int i = _avatarHost.childCount - 1; i >= 0; i--)
            {
                Transform child = _avatarHost.GetChild(i);
                if (child.name == "Badge") continue;
                Object.Destroy(child.gameObject);
            }

            HeroDef chosen = chosenId.Length == 0 ? null : session.Content.GetHero(chosenId);
            if (chosen != null)
            {
                RectTransform face = Widgets.Portrait("Face", _avatarHost, chosen, chosen.Stars,
                                                      AvatarSize, true, true);
                UIBuilder.FillParent(face, 0f);
                face.SetAsFirstSibling();
                return;
            }

            RectTransform placeholder = UIBuilder.Node("Placeholder", _avatarHost);
            UIBuilder.FillParent(placeholder, 0f);
            Image disc = UIBuilder.Img("Disc", placeholder, HudSkin.Circle(HudSkin.Disc, 128),
                                       UITheme.WithAlpha(HubStyle.Plate, HubStyle.PlateAlphaSolid));
            UIBuilder.FillParent(disc.rectTransform, 0f);
            Image rim = UIBuilder.Img("Ring", placeholder,
                                      HudSkin.Ring(HudSkin.DiscRing, 128, HubStyle.NavRingThickness),
                                      UITheme.WithAlpha(HubStyle.BorderWarm, HubStyle.BorderAlphaStrong));
            UIBuilder.FillParent(rim.rectTransform, 0f);
            Image mark = UIBuilder.Img("Mark", placeholder, TextureLab.CinderFragment(64),
                                       UITheme.WithAlpha(UITheme.CinderGlass, 0.95f));
            UIBuilder.FillParent(mark.rectTransform, AvatarGlyphInset);
            placeholder.SetAsFirstSibling();
        }

        public void Refresh(GameSession session)
        {
            if (session == null) return;
            RefreshAvatar(session);
            _rankText.text = session.Profile.PlayerLevel.ToString();
            _nameText.text = session.Profile.PlayerName.ToUpperInvariant() + "  -  RANK " + session.Profile.PlayerLevel;

            int needed = StatMath.PlayerXpForNextLevel(session.Balance, session.Profile.PlayerLevel);
            UIBuilder.SetBar(_xpFill, needed > 0 ? session.Profile.PlayerXp / (float)needed : 0f);
            _xpText.text = needed > 0 ? session.Profile.PlayerXp + " / " + needed + " XP" : "MAX RANK";

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
