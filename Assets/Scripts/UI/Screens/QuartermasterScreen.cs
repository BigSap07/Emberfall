using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// The Quartermaster (Docs/DESIGN-QUARTERMASTER.md): a shelf of rolled armor, one piece per slot,
    /// sold for Gold, restocked at the reward day. On the sink shops' furniture: a purse plate, a row
    /// of cards, a cost chip that goes red when unaffordable, a Buy button and a footer.
    ///
    /// THIS SCREEN DECIDES NOTHING. It opens the shelf through Quartermaster.Open, draws
    /// Quartermaster.View and sells through Quartermaster.Buy, and everything it prints about a price,
    /// a sold shelf or a refusal is a field of what those returned. A card that cannot be bought says
    /// why from QuartermasterSlotView.Blocked; the screen never works out for itself whether a purchase
    /// would go through.
    /// </summary>
    public sealed class QuartermasterScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Quartermaster; } }

        /// <summary>This screen carries its own CurrencyStrip and Gold plate, so the shared top bar
        /// would state the same numbers a few pixels above them. Same reasoning as the Relic Shop.</summary>
        public override bool HidesTopBar { get { return true; } }

        private const float ShelfWidth = 1366f;
        private const float CardHeight = 500f;
        private const float CardGap = 14f;
        private const int Cards = 6;
        private const float CardWidth = (ShelfWidth - CardGap * (Cards - 1)) / Cards;

        private Text _headerSubtitle;
        private readonly CurrencyStrip _currencies = new CurrencyStrip();
        private RectTransform _row;
        private Text _footer;
        private Text _purseValue;

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            ScreenChrome.ForgedHeader(Root, "THE QUARTERMASTER", Router, out _headerSubtitle);
            _currencies.Build(Root);
            BuildPursePlate();

            float top = CurrencyStrip.BottomOf(CurrencyStrip.DefaultTop);

            _row = UIBuilder.Node("Shelf", Root);
            UIBuilder.Box(_row, 0f, -top, ShelfWidth, CardHeight, new Vector2(0.5f, 1f));
            UIBuilder.HStack(_row, CardGap, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            float below = top + CardHeight + UITheme.Space24;
            _footer = EmberforgeUI.Label("Footer", Root, "", UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Box(_footer.rectTransform, 0f, -below, 1500f, 56f, new Vector2(0.5f, 1f));
        }

        /// <summary>The Gold plate, seated beside the strip at its height, as the Relic Shop seats its Dust.</summary>
        private void BuildPursePlate()
        {
            RectTransform plate = UIBuilder.Node("PursePlate", Root);
            UIBuilder.Box(plate, -UITheme.ScreenPadding, -CurrencyStrip.DefaultTop, 380f,
                          CurrencyStrip.Height, new Vector2(1f, 1f));

            Image bg = EmberforgeUI.SunkenOn(plate);
            bg.raycastTarget = false;

            Image icon = UIBuilder.Img("Icon", plate, TextureLab.CoinIcon(64), UITheme.Gold);
            UIBuilder.Box(icon.rectTransform, UITheme.Space16, 0f, 32f, 32f, new Vector2(0f, 0.5f));
            icon.raycastTarget = false;

            _purseValue = EmberforgeUI.Label("Purse", plate, "", UITheme.FontSmall,
                                             Emberforge.TextGold, TextAnchor.MiddleLeft);
            UIBuilder.FillParent(_purseValue.rectTransform,
                                 UITheme.Space16 + 32f + UITheme.Space12, 0f, UITheme.Space16, 0f);
            UIBuilder.Outlined(_purseValue, Emberforge.Outline, Emberforge.StrokeThin);
            _purseValue.raycastTarget = false;
        }

        // ------------------------------------------------------------------ refresh

        public override void OnShow()
        {
            // Opening the shelf is what rolls a new day's stock; a purchase never does.
            if (Quartermaster.Open(App.Session, TimeUtil.NowUnix())) App.RequestSave();
            App.RefreshHud();
            Rebuild();
        }

        private void Rebuild()
        {
            long now = TimeUtil.NowUnix();
            _currencies.Refresh(App.Session, now);

            QuartermasterView view = Quartermaster.View(App.Session, now);
            _purseValue.text = "GOLD   " + HudTopBar.Format(view.Gold);

            for (int i = _row.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_row.GetChild(i).gameObject);

            if (!view.Active || !view.Open || view.Slots.Count == 0)
            {
                _headerSubtitle.text = "CLOSED";
                _footer.text = "";
                Text empty = EmberforgeUI.Label("Empty", _row,
                    view.Active && !view.Open ? FeatureGate.LockLine(App.Session.Content, Feature.Quartermaster) : "The Quartermaster is closed.",
                    UITheme.FontSmall, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
                UIBuilder.Sized(empty.rectTransform, ShelfWidth, CardHeight);
                return;
            }

            int left = 0;
            for (int i = 0; i < view.Slots.Count; i++) if (!view.Slots[i].Sold) left++;
            _headerSubtitle.text = left + " OF " + view.Slots.Count + " LEFT TODAY";

            string footer = "ONE HOUR OF YOUR IDLE GOLD IS " + HudTopBar.Format(view.HourGold)
                          + "   -   NEW STOCK AT " + App.Session.Balance.DailyResetHourUtc.ToString("00") + ":00 UTC ("
                          + ResetText(view, now) + ")";
            if (view.Gold > view.FreeGold)
                footer += "\nSEALED GOLD CANNOT BUY ROLLED ARMOR";
            _footer.text = footer;

            for (int i = 0; i < view.Slots.Count; i++) BuildCard(view.Slots[i]);
        }

        // ------------------------------------------------------------------ the shelf

        private void BuildCard(QuartermasterSlotView v)
        {
            ArmorPiece piece = v.Piece;
            bool sold = v.Sold;
            bool buyable = v.Blocked == QuartermasterRefusal.None;
            bool poor = v.Blocked == QuartermasterRefusal.NotEnough || v.Blocked == QuartermasterRefusal.SealedGold;

            RectTransform card = UIBuilder.Node("Piece_" + v.Index, _row);
            UIBuilder.Sized(card, CardWidth, CardHeight);

            if (sold) EmberforgeUI.PlainPanelOn(card);
            else EmberforgeUI.PanelOn(card, false);

            float top = UITheme.Space12;
            Color tint = Emberforge.Accent(piece.Aspect);

            // ---- the mark
            const float tile = 120f;
            RectTransform iconTile = UIBuilder.Node("Tile", card);
            UIBuilder.Box(iconTile, 0f, -top, tile, tile, new Vector2(0.5f, 1f));
            Image tileBg = EmberforgeUI.SunkenOn(iconTile);
            tileBg.raycastTarget = false;
            Image mark = UIBuilder.Img("Mark", iconTile, TextureLab.Sigil((int)piece.Slot * 2 + 1, 128),
                                       UITheme.WithAlpha(tint, sold ? 0.5f : 0.95f));
            UIBuilder.Box(mark.rectTransform, 0f, 0f, tile * 0.62f, tile * 0.62f, new Vector2(0.5f, 0.5f));
            top += tile + UITheme.Space8;

            // ---- name and the chip under it
            Text nameLabel = EmberforgeUI.Body("Name", card, piece.Name.ToUpperInvariant(), UITheme.FontTiny - 2,
                                               Emberforge.TextBody, TextAnchor.UpperCenter);
            UIBuilder.Box(nameLabel.rectTransform, 0f, -top, CardWidth - UITheme.Space16, 52f, new Vector2(0.5f, 1f));
            top += 52f;

            RectTransform chip = EmberforgeUI.Chip("Kind", card,
                ArmorInfo.SlotLabel(piece.Slot).ToUpperInvariant() + "   " + ArmorInfo.TemperLabel(piece.Temper),
                tint, UITheme.FontTiny - 4);
            UIBuilder.Box(chip, 0f, -top, CardWidth - UITheme.Space16, 28f, new Vector2(0.5f, 1f));
            top += 28f + UITheme.Space8;

            // ---- the stats
            string stats = piece.Aspect.ToString().ToUpperInvariant() + " ASPECT\n" + piece.Primary.Describe();
            for (int i = 0; i < piece.SubStats.Count; i++) stats += "\n" + piece.SubStats[i].Describe();
            Text body = EmberforgeUI.Body("Body", card, stats, UITheme.FontTiny - 6,
                                          Emberforge.TextQuiet, TextAnchor.UpperCenter);
            body.rectTransform.anchorMin = new Vector2(0f, 0f);
            body.rectTransform.anchorMax = new Vector2(1f, 1f);
            body.rectTransform.offsetMin = new Vector2(UITheme.Space8, 130f);
            body.rectTransform.offsetMax = new Vector2(-UITheme.Space8, -top);

            // ---- the price in hours, which is how it is set
            Text hours = EmberforgeUI.Label("Hours", card, Fmt(v.Hours) + " HOURS OF IDLE GOLD",
                                            UITheme.FontTiny - 6, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
            UIBuilder.Box(hours.rectTransform, 0f, 100f, CardWidth - UITheme.Space16, 26f, new Vector2(0.5f, 0f));

            // ---- cost chip
            RectTransform cost = UIBuilder.Node("Cost", card);
            UIBuilder.Box(cost, 0f, 62f, CardWidth - UITheme.Space16, 32f, new Vector2(0.5f, 0f));
            Image costPlate = EmberforgeUI.SunkenOn(cost);
            costPlate.raycastTarget = false;
            Image costIcon = UIBuilder.Img("Icon", cost, TextureLab.CoinIcon(64), UITheme.Gold);
            UIBuilder.Box(costIcon.rectTransform, UITheme.Space8, 0f, 24f, 24f, new Vector2(0f, 0.5f));
            Text costText = EmberforgeUI.Label("Value", cost, HudTopBar.Format(v.Price), UITheme.FontTiny - 2,
                                               poor ? UITheme.Danger : Emberforge.TextGold, TextAnchor.MiddleLeft);
            UIBuilder.FillParent(costText.rectTransform, UITheme.Space8 + 24f + UITheme.Space8, 0f, UITheme.Space8, 0f);
            UIBuilder.Outlined(costText, Emberforge.Outline, Emberforge.StrokeThin);

            // ---- the action, or the reason there is none
            if (!sold)
            {
                UIButton buy = EmberforgeUI.Button("Buy", card, "BUY", UITheme.FontSmall, ForgeButtonKind.Primary);
                UIBuilder.Box(buy.Root, 0f, UITheme.Space12, CardWidth - UITheme.Space16, 46f, new Vector2(0.5f, 0f));
                buy.Interactable = buyable;
                int index = v.Index;
                buy.OnClick(delegate { OnBuy(index); });
            }
            else
            {
                RectTransform state = EmberforgeUI.Chip("State", card, "SOLD", Emberforge.TextQuiet, UITheme.FontTiny);
                UIBuilder.Box(state, 0f, UITheme.Space16, CardWidth - UITheme.Space16, 40f, new Vector2(0.5f, 0f));
            }
        }

        private static string Fmt(double hours)
        {
            return hours.ToString("0.#", CultureInfo.InvariantCulture);
        }

        /// <summary>When the shelf is restocked, in words: hours and minutes from now.</summary>
        private static string ResetText(QuartermasterView view, long now)
        {
            long seconds = Math.Max(0L, view.ResetUnix - now);
            long minutes = seconds / 60;
            return "IN " + (minutes / 60) + "H " + (minutes % 60).ToString("00") + "M";
        }

        // ------------------------------------------------------------------ actions

        private void OnBuy(int index)
        {
            QuartermasterResult result = Quartermaster.Buy(App.Session, index, TimeUtil.NowUnix());
            if (!result.Success)
            {
                App.Notify(RefusalLine(result.Reason));
                Rebuild();
                return;
            }

            App.Audio.Reward();
            App.Notify(result.Piece.Name + " added to your bag.");
            App.RefreshHud();
            App.RequestSave();
            Rebuild();
        }

        private static string RefusalLine(QuartermasterRefusal why)
        {
            switch (why)
            {
                case QuartermasterRefusal.NotEnough: return "Not enough Gold for that.";
                case QuartermasterRefusal.SealedGold: return "Sealed Gold cannot buy rolled armor.";
                case QuartermasterRefusal.Sold: return "That piece is sold.";
                case QuartermasterRefusal.Locked: return "The Quartermaster is not open to you yet.";
                case QuartermasterRefusal.Stale: return "The shelf is being restocked. Open the shop again.";
            }
            return "That cannot be bought.";
        }
    }
}
