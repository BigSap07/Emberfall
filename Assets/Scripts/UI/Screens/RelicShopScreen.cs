using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// Lists every relic the player can purchase with Relic Dust. Each entry shows the relic name,
    /// its grade badge, its own text - a legendary's stat line, or a mythic's rule plus its stat
    /// line - and its price. Buying adds it to the player's collection unassigned; assigning it to
    /// a hero is done from the Inventory screen.
    ///
    /// The two grades price differently, and deliberately so: GameSession.RelicPrice reads
    /// RelicShopPrice for a legendary relic and the much steeper RelicMythicShopPrice for a mythic
    /// one, which is the single source both this screen's display and BuyRelic's deduction read -
    /// they cannot show one number and charge another.
    ///
    /// No aspect filtering of any kind: any relic can be purchased and assigned to any hero.
    ///
    /// ---- visual pass ----
    /// A PAGED CARD ROW, not a scrolling list of wide rows. The stock is ten relics and the shelf
    /// shows five, so the whole shop is two pages - which is what makes paging the right shape here
    /// rather than a scrollbar: a player can see the entire catalogue in two taps and knows when
    /// they have seen all of it, neither of which is true of a list that runs off the bottom.
    ///
    /// EXACTLY TWO GRADES CARRY EXACTLY TWO FRAMES. The mockup paints three frame colours; the
    /// content has two grades. Adding a third would have invented a tier the game does not have, so
    /// Legendary takes the gold frame and Mythic takes the brighter Mythic frame plus a glow behind
    /// the card, and nothing else varies. Rarity reads off frame BRIGHTNESS and the glow, not off a
    /// third colour a player would have to learn.
    ///
    /// Nothing about buying changed. OnBuy, the price source and the affordability gate are the
    /// same code they were.
    /// </summary>
    public sealed class RelicShopScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.RelicShop; } }

        /// <summary>
        /// This screen carries its own CurrencyStrip, so the shared top bar's pills would state
        /// the same numbers a few pixels above it. One readout, not two. Same mechanism
        /// BattleScreen already uses, and ScreenRouter re-applies it on every Show, so the bar
        /// returns intact on any screen that does not opt out.
        /// </summary>
        public override bool HidesTopBar { get { return true; } }

        private const int PageSize = 5;
        private const float CardWidth = 262f;
        private const float CardHeight = 500f;
        private const float CardGap = 14f;
        private const float ArrowWidth = 64f;
        private const int CardCut = 18;
        private const int TileCut = 14;

        private Text _headerSubtitle;
        private readonly CurrencyStrip _currencies = new CurrencyStrip();

        private RectTransform _row;
        private UIButton _prevPage;
        private UIButton _nextPage;
        private Text _pageLabel;
        private Text _priceHint;
        private Text _dustValue;

        private int _page;

        private static float ShelfWidth { get { return CardWidth * PageSize + CardGap * (PageSize - 1); } }

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            ScreenChrome.ForgedHeader(Root, "RELIC SHOP", Router, out _headerSubtitle);
            _currencies.Build(Root);
            BuildDustPlate();

            float top = CurrencyStrip.BottomOf(CurrencyStrip.DefaultTop);

            _prevPage = PageArrow("Prev", "<", -(ShelfWidth * 0.5f + ArrowWidth * 0.75f), top);
            _nextPage = PageArrow("Next", ">", ShelfWidth * 0.5f + ArrowWidth * 0.75f, top);
            _prevPage.OnClick(delegate { TurnPage(-1); });
            _nextPage.OnClick(delegate { TurnPage(1); });

            _row = UIBuilder.Node("Shelf", Root);
            UIBuilder.Box(_row, 0f, -top, ShelfWidth, CardHeight, new Vector2(0.5f, 1f));
            UIBuilder.HStack(_row, CardGap, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            float below = top + CardHeight + UITheme.Space24;

            _pageLabel = EmberforgeUI.Label("Page", Root, "", UITheme.FontSmall, Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Box(_pageLabel.rectTransform, 0f, -below, 600f, 32f, new Vector2(0.5f, 1f));

            _priceHint = EmberforgeUI.Label("Prices", Root, "", UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Box(_priceHint.rectTransform, 0f, -(below + 34f), 900f, 28f, new Vector2(0.5f, 1f));
        }

        /// <summary>
        /// THE DUST PURSE, which is this screen's own and not the strip's.
        ///
        /// Relic Dust buys exactly one thing in the game and this is the counter it buys it over,
        /// so it belongs here rather than on a bar that follows the player around - the rule
        /// CurrencyStrip states, and the one the Forge's Slag plate, the Proving's Valor purse and
        /// the Guild Path's Waymark pill were each already following. Before this, the shop was
        /// the only one of the four with nowhere to read its own currency: it took the number off
        /// the strip, and when the strip stopped carrying Dust the price tags would have been the
        /// only mention of it left.
        ///
        /// SEATED BESIDE THE STRIP, at the same Y, deliberately. A price and the purse that pays
        /// it should be one glance apart, and the strip is 700 wide on a 1920 canvas, so the room
        /// to its right is exactly the room this needs.
        /// </summary>
        private void BuildDustPlate()
        {
            RectTransform plate = UIBuilder.Node("DustPlate", Root);
            UIBuilder.Box(plate, -UITheme.ScreenPadding, -CurrencyStrip.DefaultTop, 380f,
                          CurrencyStrip.Height, new Vector2(1f, 1f));

            // The kit's recess, the same surface the strip's own cells and the Forge's Slag plate
            // use, so a purse reads as a purse wherever the game shows one.
            Image bg = EmberforgeUI.SunkenOn(plate);
            bg.raycastTarget = false;

            Image icon = UIBuilder.Img("Icon", plate, TextureLab.SpiralIcon(64), UITheme.Dust);
            UIBuilder.Box(icon.rectTransform, UITheme.Space16, 0f, 32f, 32f, new Vector2(0f, 0.5f));
            icon.raycastTarget = false;

            _dustValue = EmberforgeUI.Label("Dust", plate, "", UITheme.FontSmall,
                                            Emberforge.TextGold, TextAnchor.MiddleLeft);
            UIBuilder.FillParent(_dustValue.rectTransform,
                                 UITheme.Space16 + 32f + UITheme.Space12, 0f, UITheme.Space16, 0f);
            UIBuilder.Outlined(_dustValue, Emberforge.Outline, Emberforge.StrokeThin);
            _dustValue.raycastTarget = false;
        }

        /// <summary>
        /// One shelf arrow: the kit's rail disc with a Cinzel glyph on it. Text rather than a
        /// sprite for the reason ScreenChrome's back chevron is - there is no arrow in the
        /// generator and adding one for a handful of callers would put a shape in the shared kit
        /// that nothing else asks for.
        /// </summary>
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
            App.RefreshHud();
            Rebuild();
        }

        private void TurnPage(int delta)
        {
            int pages = PageCount();
            int next = Mathf.Clamp(_page + delta, 0, pages - 1);
            if (next == _page) return;
            _page = next;
            App.Audio.Tap();
            Rebuild();
        }

        private int PageCount()
        {
            int count = App.Session.Content.Relics.Count;
            return Mathf.Max(1, (count + PageSize - 1) / PageSize);
        }

        /// <summary>The price line under the shelf: the two grades' prices, and what a tier costs
        /// against the relic's own price when the relic endgame is on.</summary>
        private string PriceHint()
        {
            GameSession s = App.Session;
            string text = "LEGENDARY " + HudTopBar.Format(s.RelicGradePrice(RelicGrade.Legendary))
                        + " DUST   -   MYTHIC " + HudTopBar.Format(s.RelicGradePrice(RelicGrade.Mythic)) + " DUST";
            if (!s.RelicEndgameActive || s.Content.RelicShop.Tiers.Count == 0) return text;
            text += "   -   TIERS";
            for (int i = 0; i < s.Content.RelicShop.Tiers.Count; i++)
                text += (i == 0 ? " " : ", ") + s.Content.RelicShop.Tiers[i].Tier + " COSTS x"
                      + s.Content.RelicShop.Tiers[i].PriceMultiplier.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            return text;
        }

        private void Rebuild()
        {
            _currencies.Refresh(App.Session, TimeUtil.NowUnix());

            _dustValue.text = "RELIC DUST   " + HudTopBar.Format(App.Session.Profile.RelicDust);

            List<RelicCardView> relics = RelicShop.View(App.Session);
            int pages = PageCount();
            _page = Mathf.Clamp(_page, 0, pages - 1);

            _headerSubtitle.text = relics.Count + " RELICS AVAILABLE";
            _pageLabel.text = "PAGE " + (_page + 1) + " OF " + pages;
            _priceHint.text = PriceHint();

            _prevPage.Interactable = _page > 0;
            _nextPage.Interactable = _page < pages - 1;

            for (int i = _row.childCount - 1; i >= 0; i--)
                Object.Destroy(_row.GetChild(i).gameObject);

            if (relics.Count == 0)
            {
                Text empty = EmberforgeUI.Label("Empty", _row, "No relics in the content database.",
                                             UITheme.FontSmall, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
                UIBuilder.Sized(empty.rectTransform, ShelfWidth, CardHeight);
                return;
            }

            int first = _page * PageSize;
            for (int i = first; i < first + PageSize && i < relics.Count; i++)
                BuildRelicCard(relics[i], i);
        }

        /// <summary>
        /// One relic, as a card. Grade drives the frame, the glow and the badge and nothing else -
        /// the layout of a legendary and a mythic is identical, so the two are compared on their
        /// text rather than on their shape.
        /// </summary>
        private void BuildRelicCard(RelicCardView view, int index)
        {
            RelicDef relic = view.Relic;
            bool mythic = relic.Grade == RelicGrade.Mythic;
            Color grade = UITheme.RelicGradeColor(relic.Grade);

            bool owned = view.Owned;
            bool upgradable = owned && view.UpgradePrice > 0;
            int price = upgradable ? view.UpgradePrice : view.Price;
            bool affordable = !owned ? view.CanBuy : view.CanUpgrade;

            RectTransform card = UIBuilder.Node("Relic_" + relic.Id, _row);
            UIBuilder.Sized(card, CardWidth, CardHeight);

            // A mythic's glow sits OUTSIDE the card's own bounds and behind everything, so the card
            // reads as lit rather than as tinted. Subtle on purpose: a second, quieter signal
            // agreeing with the frame, not one competing with it.
            if (mythic)
            {
                Image glow = UIBuilder.Img("Glow", card, TextureLab.Glow(128, 1.7f),
                                           UITheme.WithAlpha(UITheme.Mythic, 0.22f));
                UIBuilder.FillParent(glow.rectTransform, -26f);
            }

            // A FRAMED PANEL for a relic still on the shelf and a PLAIN one for one already
            // owned. Depth carries "you have this" rather than an alpha on the fill: a translucent
            // card reads as whatever is behind it, and what is behind it here is the page.
            //
            // The grade's own colour left the card's border with it. Every frame in this kit is
            // gold, and a shelf of cards framed in two different colours was using the one
            // channel the kit spends on structure to say something else. The grade is still on
            // every card twice - the chip under the name and the tint on the mark - which is
            // where a player reads it.
            if (owned) EmberforgeUI.PlainPanelOn(card);
            else EmberforgeUI.PanelOn(card, mythic);

            float top = UITheme.Space16;

            // ---- icon tile
            const float tile = 178f;
            RectTransform iconTile = UIBuilder.Node("Tile", card);
            UIBuilder.Box(iconTile, 0f, -top, tile, tile, new Vector2(0.5f, 1f));

            Image tileBg = EmberforgeUI.SunkenOn(iconTile);
            tileBg.raycastTarget = false;

            // The relic's own mark. Sigil is the project's existing emblem set, indexed off the
            // relic id's hash so a given relic always draws the same mark - stable between sessions
            // and between here and anywhere else that wants to draw it later.
            // With the UI kit on, the relic's own icon (painted in colour: white, larger, dimmed when owned).
            Sprite kitRelic = UiKit.Relic(relic.Id);
            Image mark = UIBuilder.Img("Mark", iconTile, kitRelic != null ? kitRelic : TextureLab.Sigil(SigilFor(index), 128),
                                       kitRelic != null ? new Color(1f, 1f, 1f, owned ? 0.7f : 1f)
                                                        : UITheme.WithAlpha(grade, owned ? 0.55f : 0.9f));
            float markSize = kitRelic != null ? tile * 0.82f : tile * 0.58f;
            UIBuilder.Box(mark.rectTransform, 0f, 0f, markSize, markSize, new Vector2(0.5f, 0.5f));

            top += tile + UITheme.Space12;

            // ---- name
            Text nameLabel = EmberforgeUI.Body("Name", card, relic.Name, UITheme.FontSmall,
                                               Emberforge.TextBody, TextAnchor.UpperCenter);
            UIBuilder.Box(nameLabel.rectTransform, 0f, -top, CardWidth - UITheme.Space24, 58f, new Vector2(0.5f, 1f));
            top += 58f + UITheme.Space4;

            // ---- grade badge
            RectTransform badge = EmberforgeUI.Chip("Grade", card, UITheme.RelicGradeLabel(relic.Grade),
                                                 grade, UITheme.FontTiny - 2);
            UIBuilder.Box(badge, 0f, -top, 148f, 28f, new Vector2(0.5f, 1f));
            top += 28f + UITheme.Space8;

            // ---- the relic's own text, from CardText() so the shop and the inventory cannot drift
            // into describing the same relic two ways.
            string cardText = relic.CardText();
            if (owned) cardText += "\nTIER " + view.Tier + " OF " + view.MaxTier;
            if (upgradable && view.NextStats.Length > 0) cardText += "\nNEXT  " + view.NextStats;
            Text rule = EmberforgeUI.Body("Rule", card, cardText, UITheme.FontTiny - 4,
                                               Emberforge.TextQuiet, TextAnchor.UpperCenter);
            rule.rectTransform.anchorMin = new Vector2(0f, 0f);
            rule.rectTransform.anchorMax = new Vector2(1f, 1f);
            rule.rectTransform.offsetMin = new Vector2(UITheme.Space12, 104f);
            rule.rectTransform.offsetMax = new Vector2(-UITheme.Space12, -top);

            // ---- cost chip, in the currency strip's own language. A relic at its top tier has nothing
            // left to buy, so it carries no price row above its MAX TIER (or OWNED) chip.
            if (!owned || upgradable)
            {
                RectTransform cost = UIBuilder.Node("Cost", card);
                UIBuilder.Box(cost, 0f, 62f, CardWidth - UITheme.Space24, 32f, new Vector2(0.5f, 0f));

                Image costPlate = EmberforgeUI.SunkenOn(cost);
                costPlate.raycastTarget = false;

                Image costIcon = UIBuilder.Img("Icon", cost, TextureLab.SpiralIcon(64), UITheme.Dust);
                UIBuilder.Box(costIcon.rectTransform, UITheme.Space8, 0f, 24f, 24f, new Vector2(0f, 0.5f));

                Text costText = EmberforgeUI.Label("Value", cost, price.ToString(), UITheme.FontTiny,
                                                   (affordable || owned) ? Emberforge.TextGold : UITheme.Danger,
                                                   TextAnchor.MiddleLeft);
                UIBuilder.FillParent(costText.rectTransform, UITheme.Space8 + 24f + UITheme.Space8, 0f, UITheme.Space8, 0f);
                UIBuilder.Outlined(costText, Emberforge.Outline, Emberforge.StrokeThin);
            }

            // ---- action
            if (owned && !upgradable)
            {
                RectTransform ownedBadge = EmberforgeUI.Chip("Owned", card,
                    view.MaxTier > 1 && view.Tier >= view.MaxTier ? "MAX TIER" : "OWNED", Emberforge.TextQuiet, UITheme.FontTiny);
                UIBuilder.Box(ownedBadge, 0f, UITheme.Space16, CardWidth - UITheme.Space24, 40f, new Vector2(0.5f, 0f));
            }
            else if (owned)
            {
                UIButton up = EmberforgeUI.Button("Upgrade", card, "UPGRADE TO " + (view.Tier + 1), UITheme.FontSmall, ForgeButtonKind.Primary);
                UIBuilder.Box(up.Root, 0f, UITheme.Space12, CardWidth - UITheme.Space24, 46f, new Vector2(0.5f, 0f));
                up.Interactable = affordable;

                RelicDef capturedUp = view.Relic;
                up.OnClick(delegate { OnUpgrade(capturedUp); });
            }
            else
            {
                UIButton buy = EmberforgeUI.Button("Buy", card, "BUY", UITheme.FontSmall, ForgeButtonKind.Primary);
                UIBuilder.Box(buy.Root, 0f, UITheme.Space12, CardWidth - UITheme.Space24, 46f, new Vector2(0.5f, 0f));
                buy.Interactable = affordable;

                RelicDef captured = relic;
                buy.OnClick(delegate { OnBuy(captured); });
            }
        }

        /// <summary>
        /// PLACEHOLDER relic marks, until relics have art of their own.
        ///
        /// Ten emblems for ten relics, hand-picked out of TextureLab's fourteen after rendering the
        /// set and looking at it. Four are dropped: 2 and 7 are soft blobs, 10 is a golem's filled
        /// square, and 11 is a row of diamonds that reads as a repeating pattern rather than a mark.
        ///
        /// Assigned BY POSITION, not by hashing the relic id. Hashing was the first attempt and it
        /// is the more obviously "correct" choice - stable across content edits - but with ten ids
        /// falling into thirteen buckets it put the same emblem on three of the five relics on page
        /// one, which is the one thing a per-relic mark must not do. Position guarantees every relic
        /// on a shelf has a distinct mark. The cost is that inserting a relic mid-list shifts the
        /// marks after it, which is cosmetic drift on placeholder art and is the cheaper problem.
        ///
        /// None of this survives real relic icons, and none of it should.
        /// </summary>
        private static readonly int[] RelicSigils = { 0, 1, 3, 4, 5, 6, 8, 9, 12, 13 };

        private static int SigilFor(int index)
        {
            return RelicSigils[Mathf.Abs(index) % RelicSigils.Length];
        }

        // ------------------------------------------------------------------ actions

        private void OnBuy(RelicDef relic)
        {
            bool success = RelicShop.Buy(App.Session, relic.Id, TimeUtil.NowUnix());
            if (!success)
            {
                App.Notify("Not enough Relic Dust.");
                return;
            }
            App.Audio.Reward();
            App.Notify(relic.Name + " added to your inventory.");
            App.RefreshHud();
            App.RequestSave();
            Rebuild();
        }

        private void OnUpgrade(RelicDef relic)
        {
            bool success = RelicShop.Upgrade(App.Session, relic.Id, TimeUtil.NowUnix());
            if (!success)
            {
                App.Notify("Not enough Relic Dust.");
                return;
            }
            App.Audio.Reward();
            App.Notify(relic.Name + " raised to tier " + App.Session.Profile.RelicTier(relic.Id) + ".");
            App.RefreshHud();
            App.RequestSave();
            Rebuild();
        }
    }
}
