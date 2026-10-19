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
        /// <summary>
        /// The plate's own size. Wider and taller than the pill it replaces, because a framed
        /// Emberforge panel spends its outer eight pixels on an outline and a gold frame, and the
        /// old 430x62 pill had no room to give them: the face, the name, the XP figure and the bar
        /// were already touching its edges.
        ///
        /// Still comfortably inside UITheme.TopBarHeight, and the row it shares - a plate on the
        /// left and three currency pills on the right - comes to about 1220 of a 1920 canvas, so
        /// it has margin at every aspect this game targets.
        /// </summary>
        private const float PlateWidth = 470f;
        private const float PlateHeight = 92f;

        /// <summary>A currency pill. One width and one height for all three, so they read as
        /// three of the same object rather than as three different ones.</summary>
        private const float PillWidth = 224f;
        private const float PillHeight = 68f;

        private const float AvatarGlyphInset = 9f;

        public RectTransform Root;

        private Image _backing;
        private RectTransform _avatarHost;
        private string _avatarBuiltFor = null;

        private ForgePlate _plate;
        private Image _underline;
        private Text _goldText;
        private Text _shardText;
        private Text _dustText;

        public void Build(RectTransform parent)
        {
            Root = UIBuilder.Node("TopBar", parent);
            UIBuilder.TopStrip(Root, UITheme.TopBarHeight, 0f, 0f);

            // THE SEAM IS CLOSED. This plate is only ever visible on a screen that is NOT the
            // Hub, so while eighteen pages were still in the old language it was the one strip
            // where the two met - DESIGN-EMBERFORGE names it as exactly that and leaves it
            // unconverted for the reason it left the panel sheet unconverted. Every page now
            // draws Emberforge, so the bar does.
            //
            // A RECESS rather than a panel: the plate and the three pills sit ON this, and a
            // framed panel behind a framed plate is two frames deep for one row of chrome. A
            // trough with a gold rule closing it off is the kit's way of saying "a band the
            // furniture is set into".
            _backing = EmberforgeUI.SunkenOn(Root);

            Image underline = EmberforgeUI.Rule("Underline", Root);
            underline.rectTransform.anchorMin = new Vector2(0f, 0f);
            underline.rectTransform.anchorMax = new Vector2(1f, 0f);
            underline.rectTransform.pivot = new Vector2(0.5f, 0f);
            underline.rectTransform.anchoredPosition = Vector2.zero;
            underline.rectTransform.sizeDelta = new Vector2(0f, Emberforge.RuleHeight);
            _underline = underline;

            BuildRankBlock();
            BuildCurrencies();
        }

        /// <summary>
        /// THE PLAYER PLATE, built by the kit and filled here.
        ///
        /// It was a pill in the Hub's own translucent language, sharing its surface with the
        /// building markers on the map below it. That was right while the frame and the map were
        /// one visual system; it is wrong now that the frame is Emberforge and the map's plates
        /// stayed quiet on purpose - a player plate that matches a signpost is a player plate that
        /// reads as a signpost.
        ///
        /// Nothing is dropped and nothing is added: the face, the rank badge on its corner, the
        /// name line, the XP figure and the XP bar are all still here, and the plate still opens
        /// the player panel. What changed is who builds them.
        /// </summary>
        private void BuildRankBlock()
        {
            _plate = EmberforgeUI.PlayerPlate("Plate", Root, PlateWidth, PlateHeight, OpenPlayerPanel);
            UIBuilder.Box(_plate.Root, UITheme.ScreenPadding,
                          -(UITheme.TopBarHeight - PlateHeight) * 0.5f,
                          PlateWidth, PlateHeight, new Vector2(0f, 1f));
            _avatarHost = _plate.AvatarHost;
        }

        /// <summary>
        /// The three currencies, each an Emberforge pill: a dark capsule with a gold line inside
        /// it, a faceted jewel on the left and the figure in gold on the right. Same height, same
        /// width, same treatment; only the jewel's colour tells them apart.
        ///
        /// NONE OF THEM CARRIES A PLUS, and the component supports one. A plus means "here is
        /// where you get more of this", and this build has no screen that gives a player gold,
        /// shards or relic dust in exchange for anything - the summon screen and the relic shop
        /// both SPEND. A control that opens nothing is worse than no control, so the plus stays in
        /// the kit, is shown in the component gallery, and gets wired the day a store exists.
        /// </summary>
        private void BuildCurrencies()
        {
            float step = PillWidth + UITheme.Space12;
            _dustText = Pill("Dust", "DUST", -UITheme.ScreenPadding - step * 2f, UITheme.Dust, "dust");
            _goldText = Pill("Gold", "GOLD", -UITheme.ScreenPadding - step, UITheme.Gold, "gold");
            _shardText = Pill("Shards", "SHARDS", -UITheme.ScreenPadding, UITheme.Shard, "shards");
        }

        /// <summary>
        /// One pill, NAMED. Three jewels of one shape told apart only by colour were three
        /// identical diamonds to anyone who had not already learned which colour was which, so
        /// each pill says what it holds in the quiet voice beside its jewel.
        /// </summary>
        private Text Pill(string name, string caption, float x, Color jewelTint, string purse)
        {
            ForgePill pill = EmberforgeUI.CurrencyPill(name, Root, PillWidth, PillHeight,
                                                       jewelTint, null, purse);
            UIBuilder.Box(pill.Root, x, -(UITheme.TopBarHeight - PillHeight) * 0.5f,
                          PillWidth, PillHeight, new Vector2(1f, 1f));

            // Hung in the pill's own left half, after the jewel, where the right-aligned figure
            // never reaches: the widest figure Format writes is five characters.
            float captionLeft = PillHeight * 0.14f + PillHeight * 0.72f + Emberforge.Space8;
            Text label = EmberforgeUI.Label("Caption", pill.Root, caption, UITheme.FontTiny - 4,
                                            Emberforge.TextQuiet, TextAnchor.MiddleLeft);
            UIBuilder.Box(label.rectTransform, captionLeft, 0f, 90f, 28f, new Vector2(0f, 0.5f));
            UIBuilder.BestFit(label, UITheme.FontTiny - 8, UITheme.FontTiny - 4);
            UIBuilder.Outlined(label, Emberforge.Outline, Emberforge.StrokeThin);
            label.raycastTarget = false;
            return pill.Value;
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

            float avatarSize = _avatarHost.rect.height > 0f
                             ? _avatarHost.rect.height
                             : PlateHeight - Emberforge.Space12 * 2f;

            HeroDef chosen = chosenId.Length == 0 ? null : session.Content.GetHero(chosenId);
            if (chosen != null)
            {
                // The kit's framed portrait, so the face a player chose is rimmed by that hero's
                // own rarity and glows in their aspect - the same badge the collection draws. The
                // plate used to draw Widgets.Portrait directly, which gave it an aspect ring and
                // no rarity read at all.
                RectTransform face = EmberforgeUI.PortraitFrame("Face", _avatarHost, chosen,
                                                                chosen.Stars, avatarSize);
                UIBuilder.FillParent(face, 0f);
                face.SetAsFirstSibling();
                return;
            }

            // The PLACEHOLDER, deliberately rather than pending: the profile screen has always let
            // a player make any hero they own their face, and a fresh account has not picked one.
            // A framed disc with the Cinder in it says "this is you, and you have not chosen a
            // face yet" without pretending to be a portrait.
            RectTransform placeholder = UIBuilder.Node("Placeholder", _avatarHost);
            UIBuilder.FillParent(placeholder, 0f);
            Image disc = UIBuilder.Img("Disc", placeholder,
                                       EmberforgeSkin.Get(EmberforgeArt.Slot.Disc), Color.white);
            UIBuilder.FillParent(disc.rectTransform, 0f);
            Image mark = UIBuilder.Img("Mark", placeholder, TextureLab.CinderFragment(64),
                                       UITheme.WithAlpha(UITheme.CinderGlass, 0.95f));
            UIBuilder.FillParent(mark.rectTransform, AvatarGlyphInset);
            placeholder.SetAsFirstSibling();
        }

        public void Refresh(GameSession session)
        {
            if (session == null) return;
            RefreshAvatar(session);
            _plate.Rank.text = session.Profile.PlayerLevel.ToString();
            _plate.Name.text = session.Profile.PlayerName.ToUpperInvariant() + "  -  RANK " + session.Profile.PlayerLevel;

            int needed = StatMath.PlayerXpForNextLevel(session.Balance, session.Profile.PlayerLevel);
            EmberforgeUI.SetProgress(_plate.XpFill,
                                     needed > 0 ? session.Profile.PlayerXp / (float)needed : 0f);
            _plate.Xp.text = needed > 0 ? session.Profile.PlayerXp + " / " + needed + " XP" : "MAX RANK";

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
