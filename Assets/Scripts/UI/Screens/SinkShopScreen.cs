using System;
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
    /// <summary>
    /// The face of a sink shop (DESIGN-SINKS.md): a paged row of cards on the Relic Shop's furniture,
    /// one purse plate, one card per item. The Armory and the Archive are thin subclasses that say
    /// only which shop they are and how they are worded.
    ///
    /// THIS SCREEN DECIDES NOTHING. It draws SinkShop.View and sells through SinkShop.Buy, and
    /// everything it prints about a price, a cap, a bank or a refusal is a field of what those
    /// returned. A card that cannot be bought says why from SinkItemView.Blocked; the screen never
    /// works out for itself whether a purchase would go through.
    ///
    /// A hero item opens the PICKER inside this same screen: a grid of the heroes it lists, a BACK
    /// button and one confirm button that names the hero and the price, so the player always sees
    /// what they are about to pay for. Nothing is bought by tapping a portrait.
    /// </summary>
    public abstract class SinkShopScreen : UIScreen
    {
        protected abstract SinkShopId Shop { get; }
        protected abstract string Title { get; }
        protected abstract string PurseLabel { get; }
        protected abstract Sprite PurseIcon { get; }
        protected abstract Color PurseTint { get; }
        protected abstract string EarnedFrom { get; }

        /// <summary>This screen carries its own CurrencyStrip and purse plate, so the shared top bar
        /// would state the same numbers a few pixels above them. Same reasoning as the Relic Shop.</summary>
        public override bool HidesTopBar { get { return true; } }

        private const int PageSize = 5;
        private const float CardWidth = 262f;
        private const float CardHeight = 500f;
        private const float CardGap = 14f;
        private const float ArrowWidth = 64f;
        private const float PickCardWidth = 150f;
        private const float PickCardHeight = 180f;
        private const int PickPerRow = 7;

        private Text _headerSubtitle;
        private readonly CurrencyStrip _currencies = new CurrencyStrip();

        private RectTransform _row;
        private RectTransform _pick;
        private UIButton _prevPage;
        private UIButton _nextPage;
        private Text _pageLabel;
        private Text _footer;
        private Text _purseValue;
        private UIButton _confirm;
        private UIButton _back;

        private int _page;
        private SinkItemView _picking;
        private string _chosen;

        private static float ShelfWidth { get { return CardWidth * PageSize + CardGap * (PageSize - 1); } }

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            ScreenChrome.ForgedHeader(Root, Title, Router, out _headerSubtitle);
            _currencies.Build(Root);
            BuildPursePlate();

            float top = CurrencyStrip.BottomOf(CurrencyStrip.DefaultTop);

            _prevPage = PageArrow("Prev", "<", -(ShelfWidth * 0.5f + ArrowWidth * 0.75f), top);
            _nextPage = PageArrow("Next", ">", ShelfWidth * 0.5f + ArrowWidth * 0.75f, top);
            _prevPage.OnClick(delegate { TurnPage(-1); });
            _nextPage.OnClick(delegate { TurnPage(1); });

            _row = UIBuilder.Node("Shelf", Root);
            UIBuilder.Box(_row, 0f, -top, ShelfWidth, CardHeight, new Vector2(0.5f, 1f));
            UIBuilder.HStack(_row, CardGap, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            // The picker takes the shelf's place while a hero item is open.
            _pick = UIBuilder.Node("Picker", Root);
            UIBuilder.Box(_pick, 0f, -top, ShelfWidth, CardHeight, new Vector2(0.5f, 1f));

            float below = top + CardHeight + UITheme.Space24;

            _pageLabel = EmberforgeUI.Label("Page", Root, "", UITheme.FontSmall, Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Box(_pageLabel.rectTransform, 0f, -below, 600f, 32f, new Vector2(0.5f, 1f));

            _footer = EmberforgeUI.Label("Footer", Root, "", UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Box(_footer.rectTransform, 0f, -(below + 34f), 1500f, 28f, new Vector2(0.5f, 1f));
        }

        /// <summary>The shop's own purse, seated beside the strip at its height, as the Relic Shop
        /// seats its Dust.</summary>
        private void BuildPursePlate()
        {
            RectTransform plate = UIBuilder.Node("PursePlate", Root);
            UIBuilder.Box(plate, -UITheme.ScreenPadding, -CurrencyStrip.DefaultTop, 380f,
                          CurrencyStrip.Height, new Vector2(1f, 1f));

            Image bg = EmberforgeUI.SunkenOn(plate);
            bg.raycastTarget = false;

            Image icon = UIBuilder.Img("Icon", plate, PurseIcon, PurseTint);
            UIBuilder.Box(icon.rectTransform, UITheme.Space16, 0f, 32f, 32f, new Vector2(0f, 0.5f));
            icon.raycastTarget = false;

            _purseValue = EmberforgeUI.Label("Purse", plate, "", UITheme.FontSmall,
                                             Emberforge.TextGold, TextAnchor.MiddleLeft);
            UIBuilder.FillParent(_purseValue.rectTransform,
                                 UITheme.Space16 + 32f + UITheme.Space12, 0f, UITheme.Space16, 0f);
            UIBuilder.Outlined(_purseValue, Emberforge.Outline, Emberforge.StrokeThin);
            _purseValue.raycastTarget = false;
        }

        private UIButton PageArrow(string name, string glyph, float x, float top)
        {
            ForgeDisc disc = EmberforgeUI.IconButton(name, Root, "", TextureLab.Sigil(0, 64),
                                                     Emberforge.TextGold, ArrowWidth, null);
            UIBuilder.Box(disc.Root, x, -(top + CardHeight * 0.5f - HubStyle.TouchMin * 0.5f),
                          HubStyle.TouchMin, HubStyle.TouchMin, new Vector2(0.5f, 1f));
            disc.Glyph.enabled = false;

            Text mark = EmberforgeUI.Title("Glyph", disc.Face, glyph, UITheme.FontHeading,
                                           Emberforge.TextGold, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(mark.rectTransform, 0f);
            UIBuilder.Outlined(mark, Emberforge.Outline, Emberforge.StrokeThin);
            mark.raycastTarget = false;
            return disc.Button;
        }

        // ------------------------------------------------------------------ refresh

        public override void OnShow()
        {
            _picking = null;
            _chosen = null;
            App.RefreshHud();
            Rebuild();
        }

        /// <summary>BACK from the hero picker steps out of it before the router goes anywhere.</summary>
        public override bool TryStepBack()
        {
            if (_picking == null) return false;
            _picking = null;
            _chosen = null;
            Rebuild();
            return true;
        }

        private void TurnPage(int delta)
        {
            int pages = _pages;
            int next = Mathf.Clamp(_page + delta, 0, pages - 1);
            if (next == _page) return;
            _page = next;
            App.Audio.Tap();
            Rebuild();
        }

        private int _pages = 1;

        private void Rebuild()
        {
            long now = TimeUtil.NowUnix();
            _currencies.Refresh(App.Session, now);

            SinkShopView view = SinkShop.View(App.Session, Shop, now);
            _purseValue.text = PurseLabel + "   " + HudTopBar.Format(view.Purse);

            bool picking = _picking != null;
            _row.gameObject.SetActive(!picking);
            _pick.gameObject.SetActive(picking);

            _pages = Mathf.Max(1, (view.Items.Count + PageSize - 1) / PageSize);
            _page = Mathf.Clamp(_page, 0, _pages - 1);
            _prevPage.Root.gameObject.SetActive(!picking && _pages > 1);
            _nextPage.Root.gameObject.SetActive(!picking && _pages > 1);
            _prevPage.Interactable = _page > 0;
            _nextPage.Interactable = _page < _pages - 1;

            _headerSubtitle.text = view.Active ? view.Items.Count + " ON THE SHELF" : "CLOSED";
            _pageLabel.text = picking ? "" : (_pages > 1 ? "PAGE " + (_page + 1) + " OF " + _pages : "");
            _footer.text = view.Active
                ? "ONE DAY OF YOUR " + PurseLabel + " IS " + HudTopBar.Format(view.Day) + "   -   " + EarnedFrom
                  + "   -   LIMITS RESET AT " + App.Session.Balance.DailyResetHourUtc.ToString("00") + ":00 UTC"
                : "";

            for (int i = _row.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_row.GetChild(i).gameObject);
            for (int i = _pick.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_pick.GetChild(i).gameObject);

            if (!view.Active || view.Items.Count == 0)
            {
                Text empty = EmberforgeUI.Label("Empty", _row, "This shop is closed.",
                                                UITheme.FontSmall, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
                UIBuilder.Sized(empty.rectTransform, ShelfWidth, CardHeight);
                return;
            }

            if (picking)
            {
                BuildPicker(view, now);
                return;
            }

            int first = _page * PageSize;
            for (int i = first; i < first + PageSize && i < view.Items.Count; i++)
                BuildCard(view.Items[i], now);
        }

        // ------------------------------------------------------------------ the shelf

        private void BuildCard(SinkItemView v, long now)
        {
            SinkItemDef item = v.Item;
            bool soldOut = v.Blocked == SinkRefusal.CapReached;
            bool buyable = v.Blocked == SinkRefusal.None;
            bool poor = v.Blocked == SinkRefusal.NotEnough;

            RectTransform card = UIBuilder.Node("Item_" + item.Id, _row);
            UIBuilder.Sized(card, CardWidth, CardHeight);

            if (soldOut) EmberforgeUI.PlainPanelOn(card);
            else EmberforgeUI.PanelOn(card, false);

            float top = UITheme.Space16;

            // ---- the mark
            const float tile = 150f;
            RectTransform iconTile = UIBuilder.Node("Tile", card);
            UIBuilder.Box(iconTile, 0f, -top, tile, tile, new Vector2(0.5f, 1f));
            Image tileBg = EmberforgeUI.SunkenOn(iconTile);
            tileBg.raycastTarget = false;

            Color tint = MarkTint(item);
            Image mark = UIBuilder.Img("Mark", iconTile, MarkSprite(item), UITheme.WithAlpha(tint, soldOut ? 0.5f : 0.95f));
            UIBuilder.Box(mark.rectTransform, 0f, 0f, tile * 0.62f, tile * 0.62f, new Vector2(0.5f, 0.5f));

            top += tile + UITheme.Space8;

            // ---- name and the chip under it
            Text nameLabel = EmberforgeUI.Body("Name", card, NameOf(item), UITheme.FontSmall,
                                               Emberforge.TextBody, TextAnchor.UpperCenter);
            UIBuilder.Box(nameLabel.rectTransform, 0f, -top, CardWidth - UITheme.Space24, 34f, new Vector2(0.5f, 1f));
            top += 34f + UITheme.Space4;

            RectTransform chip = EmberforgeUI.Chip("Kind", card, ChipOf(item), tint, UITheme.FontTiny - 2);
            UIBuilder.Box(chip, 0f, -top, CardWidth - UITheme.Space24, 28f, new Vector2(0.5f, 1f));
            top += 28f + UITheme.Space8;

            // ---- what it is
            Text body = EmberforgeUI.Body("Body", card, BodyOf(v), UITheme.FontTiny - 4,
                                          Emberforge.TextQuiet, TextAnchor.UpperCenter);
            body.rectTransform.anchorMin = new Vector2(0f, 0f);
            body.rectTransform.anchorMax = new Vector2(1f, 1f);
            body.rectTransform.offsetMin = new Vector2(UITheme.Space12, 138f);
            body.rectTransform.offsetMax = new Vector2(-UITheme.Space12, -top);

            // ---- how many are left, or when they come back
            Text cap = EmberforgeUI.Label("Cap", card, soldOut ? "BACK " + ResetText(v, now) : CapText(v),
                                          UITheme.FontTiny - 4, soldOut ? UITheme.Danger : Emberforge.TextQuiet,
                                          TextAnchor.MiddleCenter);
            UIBuilder.Box(cap.rectTransform, 0f, 104f, CardWidth - UITheme.Space24, 26f, new Vector2(0.5f, 0f));

            // ---- cost chip
            RectTransform cost = UIBuilder.Node("Cost", card);
            UIBuilder.Box(cost, 0f, 62f, CardWidth - UITheme.Space24, 32f, new Vector2(0.5f, 0f));
            Image costPlate = EmberforgeUI.SunkenOn(cost);
            costPlate.raycastTarget = false;
            Image costIcon = UIBuilder.Img("Icon", cost, PurseIcon, PurseTint);
            UIBuilder.Box(costIcon.rectTransform, UITheme.Space8, 0f, 24f, 24f, new Vector2(0f, 0.5f));
            Text costText = EmberforgeUI.Label("Value", cost, HudTopBar.Format(v.Price), UITheme.FontTiny,
                                               poor ? UITheme.Danger : Emberforge.TextGold, TextAnchor.MiddleLeft);
            UIBuilder.FillParent(costText.rectTransform, UITheme.Space8 + 24f + UITheme.Space8, 0f, UITheme.Space8, 0f);
            UIBuilder.Outlined(costText, Emberforge.Outline, Emberforge.StrokeThin);

            // ---- the action, or the reason there is none
            if (buyable || poor)
            {
                bool hero = item.Kind == SinkKind.HeroCopy;
                UIButton buy = EmberforgeUI.Button("Buy", card, hero ? "CHOOSE" : "BUY", UITheme.FontSmall, ForgeButtonKind.Primary);
                UIBuilder.Box(buy.Root, 0f, UITheme.Space12, CardWidth - UITheme.Space24, 46f, new Vector2(0.5f, 0f));
                buy.Interactable = buyable;
                SinkItemView captured = v;
                if (hero) buy.OnClick(delegate { OpenPicker(captured); });
                else buy.OnClick(delegate { OnBuy(captured, null); });
            }
            else
            {
                RectTransform state = EmberforgeUI.Chip("State", card, StateText(v.Blocked), Emberforge.TextQuiet, UITheme.FontTiny);
                UIBuilder.Box(state, 0f, UITheme.Space16, CardWidth - UITheme.Space24, 40f, new Vector2(0.5f, 0f));
            }
        }

        // ------------------------------------------------------------------ what a card says

        private static string NameOf(SinkItemDef item)
        {
            switch (item.Kind)
            {
                case SinkKind.Armor: return item.Armor.Name.ToUpperInvariant();
                case SinkKind.Brand: return "+1 BRAND";
                case SinkKind.Echo: return "+1 ECHO";
            }
            return "HERO COPY";
        }

        private static string ChipOf(SinkItemDef item)
        {
            switch (item.Kind)
            {
                case SinkKind.Armor:
                    return ArmorInfo.SlotLabel(item.Armor.Slot).ToUpperInvariant() + "   "
                         + ArmorInfo.TemperLabel(item.Armor.Temper);
                case SinkKind.Brand: return "PROVING ATTEMPT";
                case SinkKind.Echo: return "RECITAL ATTEMPT";
            }
            return "FOUR STAR";
        }

        private static Color MarkTint(SinkItemDef item)
        {
            switch (item.Kind)
            {
                case SinkKind.Armor: return Emberforge.Accent(item.Armor.Aspect);
                case SinkKind.Brand: return UITheme.Ember;
                case SinkKind.Echo: return UITheme.Echo;
            }
            return UITheme.Gold;
        }

        private static Sprite MarkSprite(SinkItemDef item)
        {
            switch (item.Kind)
            {
                case SinkKind.Armor: return TextureLab.Sigil((int)item.Armor.Slot * 2 + 1, 128);
                case SinkKind.Brand: return TextureLab.Sigil(3, 128);
                case SinkKind.Echo: return TextureLab.HourglassIcon(128);
            }
            return TextureLab.Crown(128);
        }

        private string BodyOf(SinkItemView v)
        {
            SinkItemDef item = v.Item;
            switch (item.Kind)
            {
                case SinkKind.Armor:
                {
                    string text = item.Armor.Aspect.ToString().ToUpperInvariant() + " ASPECT\n" + item.Armor.Primary.Describe();
                    for (int i = 0; i < item.Armor.Subs.Count; i++) text += "\n" + item.Armor.Subs[i].Describe();
                    return text;
                }
                case SinkKind.Brand:
                    return "One more Proving attempt, put straight into your bank.\nBANK  " + v.Banked + " / " + v.BankMax;
                case SinkKind.Echo:
                    return "One more Deep Recital attempt, put straight into your bank.\nBANK  " + v.Banked + " / " + v.BankMax;
            }
            return "One more copy of a four star hero you choose from " + item.Heroes.Count + ".";
        }

        private static string CapText(SinkItemView v)
        {
            return v.CapLeft + " OF " + v.CapLimit + " LEFT " + (v.Item.CapPer == SinkCapPer.Day ? "TODAY" : "THIS MONTH");
        }

        private static string StateText(SinkRefusal why)
        {
            switch (why)
            {
                case SinkRefusal.BankFull: return "BANK FULL";
                case SinkRefusal.Locked: return "LOCKED";
                case SinkRefusal.CapReached: return "SOLD OUT";
            }
            return "NOT FOR SALE";
        }

        /// <summary>When a spent allowance comes back, in words: hours and minutes for a day's, the
        /// date for a month's.</summary>
        private static string ResetText(SinkItemView v, long now)
        {
            if (v.Item.CapPer == SinkCapPer.Month)
                return "ON " + DateTimeOffset.FromUnixTimeSeconds(v.ResetUnix).UtcDateTime
                                .ToString("d MMM", CultureInfo.InvariantCulture).ToUpperInvariant();
            long seconds = Math.Max(0L, v.ResetUnix - now);
            long minutes = seconds / 60;
            return "IN " + (minutes / 60) + "H " + (minutes % 60).ToString("00") + "M";
        }

        // ------------------------------------------------------------------ the picker

        private void OpenPicker(SinkItemView v)
        {
            App.Audio.Tap();
            _picking = v;
            _chosen = null;
            Rebuild();
        }

        /// <summary>CAPTURE ONLY: opens the hero picker of an item with a hero already chosen, so the
        /// picker's layout can be photographed. The game never calls this; a tap does the same
        /// through OpenPicker and Choose.</summary>
        public void PickForCapture(string itemId, string heroId)
        {
            SinkShopView view = SinkShop.View(App.Session, Shop, TimeUtil.NowUnix());
            for (int i = 0; i < view.Items.Count; i++)
            {
                if (view.Items[i].Item.Id != itemId) continue;
                _picking = view.Items[i];
                _chosen = heroId;
                Rebuild();
                return;
            }
        }

        private void BuildPicker(SinkShopView view, long now)
        {
            // The item may have changed under the picker (a purchase made it sold out), so it is
            // looked up again by id from what the view just returned.
            SinkItemView v = null;
            for (int i = 0; i < view.Items.Count; i++) if (view.Items[i].Item.Id == _picking.Item.Id) v = view.Items[i];
            if (v == null) { _picking = null; return; }
            _picking = v;

            Text prompt = EmberforgeUI.Label("Prompt", _pick, "CHOOSE A HERO", UITheme.FontHeading,
                                             Emberforge.TextGold, TextAnchor.UpperCenter);
            UIBuilder.Box(prompt.rectTransform, 0f, 0f, ShelfWidth, 48f, new Vector2(0.5f, 1f));

            RectTransform grid = UIBuilder.Node("Grid", _pick);
            UIBuilder.Box(grid, 0f, -56f, ShelfWidth, PickCardHeight * 2f + CardGap, new Vector2(0.5f, 1f));
            UIBuilder.VStack(grid, CardGap, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);

            RectTransform rowA = null;
            List<string> heroes = v.Item.Heroes;
            for (int i = 0; i < heroes.Count; i++)
            {
                if (i % PickPerRow == 0)
                {
                    rowA = UIBuilder.Node("Row" + (i / PickPerRow), grid);
                    UIBuilder.Sized(rowA, ShelfWidth, PickCardHeight);
                    UIBuilder.HStack(rowA, CardGap, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
                }
                BuildPickCard(rowA, heroes[i]);
            }

            string chosenName = null;
            if (_chosen != null)
            {
                HeroDef def = App.Session.Content.GetHero(_chosen);
                if (def != null) chosenName = def.Name.ToUpperInvariant();
            }

            _back = EmberforgeUI.Button("Back", _pick, "BACK", UITheme.FontSmall, ForgeButtonKind.Secondary);
            UIBuilder.Box(_back.Root, -(ShelfWidth * 0.5f) + 130f, 4f, 240f, 60f, new Vector2(0.5f, 0f));
            _back.OnClick(delegate { TryStepBack(); });

            _confirm = EmberforgeUI.Button("Confirm", _pick,
                chosenName == null ? "CHOOSE A HERO" : "TAKE " + chosenName + " FOR " + HudTopBar.Format(v.Price),
                UITheme.FontSmall, ForgeButtonKind.Primary);
            UIBuilder.Box(_confirm.Root, 80f, 4f, ShelfWidth - 240f - 160f, 60f, new Vector2(0.5f, 0f));
            _confirm.Interactable = chosenName != null && v.Blocked == SinkRefusal.None;
            string pick = _chosen;
            SinkItemView bought = v;
            _confirm.OnClick(delegate { OnBuy(bought, pick); });
        }

        private void BuildPickCard(RectTransform parent, string heroId)
        {
            HeroDef def = App.Session.Content.GetHero(heroId);
            if (def == null) return;
            bool chosen = _chosen == heroId;
            HeroInstance owned = App.Session.Profile.FindHeroByDefId(heroId);

            RectTransform card = UIBuilder.Node("Pick_" + heroId, parent);
            UIBuilder.Sized(card, PickCardWidth, PickCardHeight);
            if (chosen) EmberforgeUI.PanelOn(card, false);
            else EmberforgeUI.PlainPanelOn(card);

            RectTransform portrait = EmberforgeUI.PortraitFrame("Portrait", card, def, def.Stars, 96f);
            UIBuilder.Box(portrait, 0f, -UITheme.Space12, 96f, 96f, new Vector2(0.5f, 1f));

            Text label = EmberforgeUI.Label("Name", card, def.Name.ToUpperInvariant(), UITheme.FontTiny - 6,
                                            Emberforge.TextBody, TextAnchor.UpperCenter);
            UIBuilder.Box(label.rectTransform, 0f, -(UITheme.Space12 + 100f), PickCardWidth - 12f, 34f, new Vector2(0.5f, 1f));

            Text own = EmberforgeUI.Label("Own", card, owned == null ? "NEW" : "OWNED  x" + owned.Copies, UITheme.FontTiny - 4,
                                          owned == null ? Emberforge.TextGold : Emberforge.TextQuiet, TextAnchor.MiddleCenter);
            UIBuilder.Box(own.rectTransform, 0f, 4f, PickCardWidth - 12f, 24f, new Vector2(0.5f, 0f));

            Button tap = card.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            Image hit = card.gameObject.GetComponent<Image>();
            if (hit != null) tap.targetGraphic = hit;
            string id = heroId;
            tap.onClick.AddListener(delegate { Choose(id); });
        }

        private void Choose(string heroId)
        {
            App.Audio.Tap();
            _chosen = heroId;
            Rebuild();
        }

        // ------------------------------------------------------------------ actions

        private void OnBuy(SinkItemView v, string choice)
        {
            SinkResult result = SinkShop.Buy(App.Session, v.Item.Id, choice, TimeUtil.NowUnix());
            if (!result.Success)
            {
                App.Notify(RefusalLine(result.Reason));
                Rebuild();
                return;
            }

            App.Audio.Reward();
            App.Notify(BoughtLine(v.Item, result));
            _picking = null;
            _chosen = null;
            App.RefreshHud();
            App.RequestSave();
            Rebuild();
        }

        private static string RefusalLine(SinkRefusal why)
        {
            switch (why)
            {
                case SinkRefusal.NotEnough: return "Not enough to pay for that.";
                case SinkRefusal.CapReached: return "That is sold out for now.";
                case SinkRefusal.BankFull: return "That bank is already full.";
                case SinkRefusal.Locked: return "That is not open to you yet.";
                case SinkRefusal.BadChoice: return "Choose a hero first.";
            }
            return "That cannot be bought.";
        }

        private string BoughtLine(SinkItemDef item, SinkResult result)
        {
            switch (item.Kind)
            {
                case SinkKind.Armor: return item.Armor.Name + " added to your bag.";
                case SinkKind.Brand: return "A Brand was added to your bank.";
                case SinkKind.Echo: return "An Echo was added to your bank.";
            }
            HeroDef def = result.Hero != null ? App.Session.Content.GetHero(result.Hero.DefId) : null;
            return def != null ? "A copy of " + def.Name + " joined your roster." : "A hero copy was added.";
        }
    }
}
