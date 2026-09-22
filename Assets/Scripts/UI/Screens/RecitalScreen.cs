using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// The Deep Recital's home screen: current floor, the permanent highest-floor record, the Echo
    /// bank with its regen countdown, a preview of the current six-floor Aspect cycle, and the one
    /// action that matters - fight the current floor.
    ///
    /// ---- visual pass ----
    /// THE FLOOR IS THE SCREEN. It used to be a 62pt label in the top-left corner of a stat card,
    /// competing with three StatRows that each looked exactly as important as it did. It is now a
    /// single large number inside a hexagonal plate at the centre of a standing banner, and the
    /// three things that qualify it - which aspect the floor rolled, how deep the player has ever
    /// been, how many attempts they hold - are arranged around it at deliberately smaller sizes.
    ///
    /// ECHOES ARE A METER, NOT A CURRENCY CARD. They are capped and they refill on a clock, and a
    /// bare "3 / 5" in a stat row says neither. Segments say both at a glance: how many are left,
    /// and how many the bank can ever hold. The refill countdown sits beside it rather than on its
    /// own row, because it is a property of the meter and not a fourth statistic.
    ///
    /// THE ASPECT USES THE BIBLE VALUES. UITheme.AspectAccent, not AspectColor - see the note on
    /// both. The cycle strip below the banner keeps AspectColor deliberately: it is a six-cell
    /// preview of the SAME data every other screen draws with AspectColor, and having one strip in
    /// the game disagree with all the others about what colour Gale is would be worse than the
    /// fork itself. The rolled aspect - the one the player is about to fight - is the accent the
    /// mockup calls for, and it is the one that moved.
    ///
    /// Nothing about fighting changed. OnFight still only CHECKS affordability; BattleScreen.OnShow
    /// remains the single place an Echo is spent.
    /// </summary>
    public sealed class RecitalScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Recital; } }

        /// <summary>
        /// This screen carries its own CurrencyStrip, so the shared top bar's Gold/Shards/Dust
        /// pills would state three of the same six numbers a few pixels above it. One readout, not
        /// two. Same mechanism BattleScreen already uses, and ScreenRouter re-applies it on every
        /// Show, so the bar returns intact on any screen that does not opt out.
        /// </summary>
        public override bool HidesTopBar { get { return true; } }

        private const float BannerWidth = 560f;

        /// <summary>
        /// THE BANNER TAKES WHAT THE CYCLE STRIP LEAVES, measured against the shortest canvas
        /// this game targets rather than against the editor's.
        ///
        /// It was a fixed 560, which put the cycle strip's cells below y 1010 - off the bottom of
        /// a 20:9 phone, whose logical canvas is about 966 tall. It fitted at 16:9 because that
        /// canvas is 1080, and the failure was invisible until the page was photographed at the
        /// other aspect. Same fault as the hero page's and the Rift's, and fixed the same way:
        /// the strip is the fixed thing and the banner takes the rest.
        ///
        ///   966 short canvas - 336 header and strip - 24 gap - 120 cycle block = 486 of banner.
        /// </summary>
        private const float ShortestCanvasHeight = 966f;
        private const float CycleBlock = 120f;
        private const float HexSize = 210f;
        private const float CycleWidth = 900f;

        private Text _headerSubtitle;
        private readonly CurrencyStrip _currencies = new CurrencyStrip();

        private Text _floorNumber;
        private Image _aspectIcon;
        private Text _aspectName;
        private Image _hexEdge;
        private Image _hexFill;
        private Text _highestValue;
        private Text _codaValue;

        private RectTransform _echoMeter;
        private Text _echoCountdown;
        private UIButton _fightButton;
        private Text _fightCost;
        private RectTransform _cycleRow;

        private float _tick;

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            ScreenChrome.ForgedHeader(Root, "THE DEEP RECITAL", Router, out _headerSubtitle);
            _currencies.Build(Root);

            float top = CurrencyStrip.BottomOf(CurrencyStrip.DefaultTop);
            float bannerHeight = ShortestCanvasHeight - top - UITheme.Space24 - CycleBlock;
            BuildBanner(top, bannerHeight);
            BuildCycleRow();
        }

        /// <summary>The standing banner: floor plate, record, echo meter, one action.</summary>
        private void BuildBanner(float top, float height)
        {
            RectTransform banner = UIBuilder.Node("Banner", Root);
            UIBuilder.Box(banner, 0f, -top, BannerWidth, height, new Vector2(0.5f, 1f));

            EmberforgeUI.PanelOn(banner, true);

            Image glow = UIBuilder.Img("Glow", banner, TextureLab.Glow(128, 2f),
                                       UITheme.WithAlpha(UITheme.Echo, 0.10f));
            UIBuilder.FillParent(glow.rectTransform, 0f);

            float inner = UITheme.Space24;

            // ---- the floor plate
            RectTransform hex = UIBuilder.Node("Hex", banner);
            UIBuilder.Box(hex, 0f, -inner, HexSize, HexSize, new Vector2(0.5f, 1f));

            _hexFill = UIBuilder.Img("Fill", hex, TextureLab.Hexagon(256), UITheme.WithAlpha(UITheme.Panel, 0.95f));
            UIBuilder.FillParent(_hexFill.rectTransform, 0f);

            _hexEdge = UIBuilder.Img("HexEdge", hex, TextureLab.HexagonOutline(256, 0.035f), UITheme.ChromeBrass);
            UIBuilder.FillParent(_hexEdge.rectTransform, 0f);

            _floorNumber = EmberforgeUI.Title("Floor", hex, "", UITheme.FontDisplay + 8,
                                              Emberforge.TextOnColor, TextAnchor.MiddleCenter);
            UIBuilder.Box(_floorNumber.rectTransform, 0f, 22f, HexSize, 96f, new Vector2(0.5f, 0.5f));
            UIBuilder.Outlined(_floorNumber, Emberforge.Outline, Emberforge.StrokeThick);

            // The rolled aspect: a small accent-coloured mark under the number, inside the plate,
            // so "floor 28" and "which aspect floor 28 is" are read as one fact rather than two.
            _aspectIcon = UIBuilder.Img("Aspect", hex, TextureLab.Sigil(0, 128), Emberforge.TextQuiet);
            UIBuilder.Box(_aspectIcon.rectTransform, 0f, -34f, 44f, 44f, new Vector2(0.5f, 0.5f));

            _aspectName = EmberforgeUI.Label("AspectName", hex, "", UITheme.FontTiny - 2,
                                          Emberforge.TextQuiet, TextAnchor.MiddleCenter);
            UIBuilder.Box(_aspectName.rectTransform, 0f, -66f, HexSize, 24f, new Vector2(0.5f, 0.5f));

            inner += HexSize + UITheme.Space8;

            // ---- the record, smaller and directly under the plate
            RectTransform record = UIBuilder.Node("Record", banner);
            UIBuilder.Box(record, 0f, -inner, 260f, 40f, new Vector2(0.5f, 1f));

            Image recordPlate = EmberforgeUI.SunkenOn(record);
            recordPlate.raycastTarget = false;

            Image crown = UIBuilder.Img("Crown", record, TextureLab.Crown(64), UITheme.Gold);
            UIBuilder.Box(crown.rectTransform, UITheme.Space12, 0f, 28f, 28f, new Vector2(0f, 0.5f));

            _highestValue = EmberforgeUI.Label("Highest", record, "", UITheme.FontSmall,
                                            Emberforge.TextBody, TextAnchor.MiddleLeft);
            UIBuilder.FillParent(_highestValue.rectTransform,
                                 UITheme.Space12 + 28f + UITheme.Space8, 0f, UITheme.Space12, 0f);

            inner += 40f + UITheme.Space8;

            // ---- Coda balance
            RectTransform codaRow = UIBuilder.Node("Coda", banner);
            UIBuilder.Box(codaRow, 0f, -inner, 260f, 32f, new Vector2(0.5f, 1f));

            Image codaIcon = UIBuilder.Img("CodaIcon", codaRow, TextureLab.GemIcon(64), UITheme.Coda);
            UIBuilder.Box(codaIcon.rectTransform, UITheme.Space12, 0f, 24f, 24f, new Vector2(0f, 0.5f));

            _codaValue = EmberforgeUI.Label("CodaValue", codaRow, "0", UITheme.FontSmall,
                                          Emberforge.TextBody, TextAnchor.MiddleLeft);
            UIBuilder.FillParent(_codaValue.rectTransform,
                                 UITheme.Space12 + 24f + UITheme.Space8, 0f, UITheme.Space12, 0f);

            inner += 32f + UITheme.Space8;

            // ---- the echo meter
            RectTransform echoRow = UIBuilder.Node("Echoes", banner);
            UIBuilder.Box(echoRow, 0f, -inner, BannerWidth - UITheme.Space48, 40f, new Vector2(0.5f, 1f));

            Image hourglass = UIBuilder.Img("Hourglass", echoRow, TextureLab.HourglassIcon(64), UITheme.Echo);
            UIBuilder.Box(hourglass.rectTransform, 0f, 0f, 32f, 32f, new Vector2(0f, 0.5f));

            _echoMeter = UIBuilder.Node("Meter", echoRow);
            _echoMeter.anchorMin = new Vector2(0f, 0.5f);
            _echoMeter.anchorMax = new Vector2(1f, 0.5f);
            _echoMeter.pivot = new Vector2(0.5f, 0.5f);
            _echoMeter.offsetMin = new Vector2(32f + UITheme.Space12, -16f);
            _echoMeter.offsetMax = new Vector2(-132f, 16f);
            UIBuilder.HStack(_echoMeter, 5f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

            _echoCountdown = EmberforgeUI.Label("Countdown", echoRow, "", UITheme.FontTiny - 2,
                                             Emberforge.TextQuiet, TextAnchor.MiddleRight);
            UIBuilder.Box(_echoCountdown.rectTransform, 0f, 0f, 124f, 30f, new Vector2(1f, 0.5f));

            inner += 40f + UITheme.Space8;

            // ---- the one action

            // Says out loud when the blocks above have outgrown the room the cycle strip leaves,
            // for the reason DesignGallery.CheckFits exists: the failure is silent at 16:9 and
            // only shows on the 20:9 capture.
            float needed = inner + 84f + UITheme.Space24;
            if (needed > height)
                Debug.LogError("RecitalScreen: the banner's contents need " + needed + " units and"
                    + " the shortest canvas this game targets leaves only " + height
                    + ". It fits at 16:9 and runs under the cycle strip at 20:9.");
            // Anchored to the banner's BOTTOM with the panel's padding under it, rather than
            // carried down by the running `inner` offset. The button used to land exactly on the
            // panel's lower edge and print itself over the two bottom corner ornaments; hanging it
            // off the bottom means it keeps that clearance whatever the blocks above it do.
            _fightButton = EmberforgeUI.Button("Fight", banner, "", UITheme.FontHeading, ForgeButtonKind.Primary);
            UIBuilder.Box(_fightButton.Root, 0f, UITheme.Space24,
                          BannerWidth - Emberforge.OrnamentClearance * 2f, 84f,
                          new Vector2(0.5f, 0f));
            _fightButton.OnClick(OnFight);

            Image blades = UIBuilder.Img("Swords", _fightButton.Root, TextureLab.Swords(64), Emberforge.TextBody);
            UIBuilder.Box(blades.rectTransform, 34f, 0f, 38f, 38f, new Vector2(0f, 0.5f));

            UIBuilder.Box(_fightButton.Label.rectTransform, 16f, 0f, 300f, 46f, new Vector2(0.5f, 0.5f));

            _fightCost = EmberforgeUI.Label("Cost", _fightButton.Root, "", UITheme.FontTiny,
                                         UITheme.WithAlpha(Emberforge.TextBody, 0.85f), TextAnchor.MiddleRight);
            UIBuilder.Box(_fightCost.rectTransform, -28f, 0f, 160f, 30f, new Vector2(1f, 0.5f));
            _fightCost.raycastTarget = false;
        }

        /// <summary>
        /// The six-aspect cycle strip, anchored to the BOTTOM of the screen rather than measured
        /// down from the banner. Every y here is a distance up from the bottom edge, which is
        /// what keeps the strip on screen at a canvas height nobody laid it out against.
        /// </summary>
        private void BuildCycleRow()
        {
            _cycleRow = UIBuilder.Node("CycleRow", Root);
            UIBuilder.Box(_cycleRow, 0f, UITheme.Space24, CycleWidth, 56f, new Vector2(0.5f, 0f));
            UIBuilder.HStack(_cycleRow, UITheme.Space12, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            Text heading = EmberforgeUI.Title("CycleHeading", Root, "THIS CYCLE",
                                              UITheme.FontTiny, Emberforge.TextGold,
                                              TextAnchor.LowerCenter);
            UIBuilder.Box(heading.rectTransform, 0f, UITheme.Space24 + 56f + UITheme.Space8,
                          CycleWidth, 26f, new Vector2(0.5f, 0f));
            UIBuilder.Outlined(heading, Emberforge.Outline, Emberforge.StrokeThin);
        }

        // ------------------------------------------------------------------ refresh

        public override void OnShow()
        {
            App.RefreshHud();
            _tick = 0f;
            Refresh();
        }

        private void Update()
        {
            if (Root == null || !Root.gameObject.activeInHierarchy) return;
            _tick += Time.unscaledDeltaTime;
            if (_tick < 1f) return;
            _tick = 0f;
            Refresh();
        }

        private void Refresh()
        {
            long now = TimeUtil.NowUnix();
            _currencies.Refresh(App.Session, now);

            int floor = App.Session.Profile.RecitalCurrentFloor;
            BalanceConfig cfg = App.Session.Balance;
            Aspect aspect = RecitalGenerator.AspectForFloor(floor);
            int level = RecitalGenerator.LevelForFloor(cfg, floor);

            _headerSubtitle.text = "FLOOR " + floor + "  -  " + aspect.ToString().ToUpperInvariant()
                                 + " ECHOES  -  LEVEL " + level;
            _floorNumber.text = floor.ToString();

            // The rolled aspect, in the bible's own value. The plate's edge takes the same accent so
            // the whole floor reads as belonging to that aspect rather than merely being labelled.
            Color accent = UITheme.AspectAccent(aspect);
            _aspectIcon.sprite = TextureLab.Sigil(AspectSigil(aspect), 128);
            _aspectIcon.color = accent;
            _aspectName.text = aspect.ToString().ToUpperInvariant();
            _aspectName.color = accent;
            _hexEdge.color = accent;
            _hexFill.color = UITheme.WithAlpha(UITheme.Mix(UITheme.Panel, accent, 0.12f), 0.95f);

            _highestValue.text = App.Session.Profile.RecitalHighestFloor > 0
                ? "BEST  " + App.Session.Profile.RecitalHighestFloor
                : "NO RECORD YET";

            _codaValue.text = "CODA  " + HudTopBar.Format(App.Session.Profile.Coda);

            RecitalEchoesStatus echoes = App.Session.PeekRecitalEchoes(now);
            RefreshEchoMeter(echoes);
            _echoCountdown.text = echoes.SecondsToNext > 0 ? "+1 IN " + FormatDuration(echoes.SecondsToNext) : "FULL";

            bool canFight = echoes.Banked > 0;
            _fightButton.Interactable = canFight;
            _fightButton.SetText(canFight ? "FIGHT" : "OUT OF ECHOES");
            _fightCost.text = canFight ? "1 ECHO" : "";

            RefreshCycleRow(floor);
        }

        /// <summary>
        /// One segment per Echo the bank can hold, filled up to what is banked.
        ///
        /// Rebuilt only when the CAP changes, not on every tick: the meter is repainted once a
        /// second by Update, and tearing down and reallocating five cells at 1Hz to change two
        /// colours would be waste. The cap is the only thing that changes the cell COUNT.
        /// </summary>
        private void RefreshEchoMeter(RecitalEchoesStatus echoes)
        {
            if (_echoMeter.childCount != echoes.Max)
            {
                for (int i = _echoMeter.childCount - 1; i >= 0; i--)
                    Destroy(_echoMeter.GetChild(i).gameObject);

                for (int i = 0; i < echoes.Max; i++)
                {
                    RectTransform cell = UIBuilder.Node("Echo" + i, _echoMeter);
                    UIBuilder.Flexible(cell, 1f, 0f);
                    // The kit's bar fill for a spent-or-banked cell, so a meter of five reads as
                    // five segments of the same gold section the progress bars are made of.
                    Image fill = cell.gameObject.AddComponent<Image>();
                    fill.sprite = EmberforgeSkin.Get(EmberforgeArt.Slot.BarFill);
                    fill.type = Image.Type.Sliced;
                    fill.raycastTarget = false;
                }
            }

            for (int i = 0; i < _echoMeter.childCount; i++)
            {
                Image fill = _echoMeter.GetChild(i).GetComponent<Image>();
                if (fill == null) continue;
                fill.color = i < echoes.Banked ? UITheme.Echo : Emberforge.SunkenMid;
            }
        }

        private void RefreshCycleRow(int floor)
        {
            for (int i = _cycleRow.childCount - 1; i >= 0; i--) Destroy(_cycleRow.GetChild(i).gameObject);

            int cycleStart = floor - ((floor - 1) % RecitalGenerator.AspectCycleLength);
            for (int i = 0; i < RecitalGenerator.AspectCycleLength; i++)
            {
                int cycleFloor = cycleStart + i;
                Aspect cycleAspect = RecitalGenerator.AspectForFloor(cycleFloor);
                bool isCurrent = cycleFloor == floor;
                bool isPast = cycleFloor < floor;

                // AspectColor, not AspectAccent, and deliberately: this strip previews the same six
                // aspects every other screen in the game draws, and one strip disagreeing with all
                // of them about Gale's hue would read as a bug. See UITheme.AspectAccent.
                Color tint = UITheme.AspectColor(cycleAspect);

                RectTransform cell = UIBuilder.Node("Cycle" + cycleFloor, _cycleRow);
                UIBuilder.Flexible(cell, 1f, 0f);
                UIBuilder.Sized(cell, 0f, 56f);

                // The floor you are ON is framed and every other is set into the strip. Depth
                // says "here" rather than three strengths of one tint, which is what the strip
                // used to do and is why a past floor and a future one read as the same thing at a
                // glance. The aspect's own colour stays on the WORD, which is what it is about.
                if (isCurrent) EmberforgeUI.PanelOn(cell, false);
                else EmberforgeUI.SunkenOn(cell);

                Text label = EmberforgeUI.Label("Label", cell,
                                             cycleAspect.ToString().Substring(0, 3).ToUpperInvariant(),
                                             UITheme.FontTiny, isPast ? Emberforge.TextQuiet : tint,
                                             TextAnchor.MiddleCenter);
                UIBuilder.FillParent(label.rectTransform, 4f);
            }
        }

        /// <summary>
        /// Which of TextureLab's emblems stands for an aspect.
        ///
        /// Mapped by MEANING, not by casting the enum to an int. The enum's own order lands Tide on
        /// sigil 1, which is authored as "flame petals" - a fire mark on the water aspect, which is
        /// worse than no mark at all. Every pairing below is one the emblem was actually drawn for:
        /// Ember takes the ember spark, Tide the tide spiral, Verdant the leaf, Umbra the hollow
        /// eye, Lumen the sundered star, and Gale the moth wings, which is the closest thing in the
        /// set to something carried on wind.
        /// </summary>
        private static int AspectSigil(Aspect aspect)
        {
            switch (aspect)
            {
                case Aspect.Ember: return 7;    // ember spark
                case Aspect.Tide: return 12;    // tide spiral
                case Aspect.Verdant: return 2;  // leaf / droplet
                case Aspect.Gale: return 8;     // moth wings
                case Aspect.Umbra: return 6;    // hollow eye
                case Aspect.Lumen: return 13;   // sundered star
            }
            return 13;
        }

        private static string FormatDuration(long seconds)
        {
            if (seconds < 60) return seconds + "s";
            long minutes = seconds / 60;
            if (minutes < 60) return minutes + "m " + (seconds % 60) + "s";
            long hours = minutes / 60;
            return hours + "h " + (minutes % 60) + "m";
        }

        // ------------------------------------------------------------------ actions

        /// <summary>
        /// Checks affordability only - does NOT spend. BattleScreen.OnShow is the single place an
        /// Echo is actually deducted, for both this button's first entry and a Rematch from the
        /// result screen, so there is exactly one spend per attempt rather than a second deduction
        /// here on top of BattleScreen's own gate.
        /// </summary>
        private void OnFight()
        {
            if (App.Session.PeekRecitalEchoes(TimeUtil.NowUnix()).Banked <= 0)
            {
                App.Notify("Out of Echoes. Wait for one to regenerate.");
                Refresh();
                return;
            }
            App.Audio.Tap();
            int floor = App.Session.Profile.RecitalCurrentFloor;
            Router.Battle.SetRecitalFloor(floor);
            Router.Show(ScreenId.Battle);
        }
    }
}
