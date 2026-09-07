using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
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

        private int _page;

        private static float ShelfWidth { get { return CardWidth * PageSize + CardGap * (PageSize - 1); } }

        protected override void Build()
        {
            ScreenChrome.Header(Root, "RELIC SHOP", Router, out _headerSubtitle);
            _currencies.Build(Root);

            float top = CurrencyStrip.BottomOf(CurrencyStrip.DefaultTop);

            _prevPage = PageArrow("Prev", "<", -(ShelfWidth * 0.5f + ArrowWidth * 0.75f), top);
            _nextPage = PageArrow("Next", ">", ShelfWidth * 0.5f + ArrowWidth * 0.75f, top);
            _prevPage.OnClick(delegate { TurnPage(-1); });
            _nextPage.OnClick(delegate { TurnPage(1); });

            _row = UIBuilder.Node("Shelf", Root);
            UIBuilder.Box(_row, 0f, -top, ShelfWidth, CardHeight, new Vector2(0.5f, 1f));
            UIBuilder.HStack(_row, CardGap, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            float below = top + CardHeight + UITheme.Space24;

            _pageLabel = UIBuilder.Label("Page", Root, "", UITheme.FontSmall, UITheme.TextMuted, TextAnchor.UpperCenter);
            UIBuilder.Box(_pageLabel.rectTransform, 0f, -below, 600f, 32f, new Vector2(0.5f, 1f));

            _priceHint = UIBuilder.Label("Prices", Root, "", UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperCenter);
            UIBuilder.Box(_priceHint.rectTransform, 0f, -(below + 34f), 900f, 28f, new Vector2(0.5f, 1f));
        }

        private UIButton PageArrow(string name, string glyph, float x, float top)
        {
            UIButton button = UIBuilder.IconButton(name, Root, glyph, ArrowWidth,
                                                   UITheme.WithAlpha(UITheme.PanelRaised, 0.9f), UITheme.TextPrimary);
            UIBuilder.Box(button.Root, x, -(top + CardHeight * 0.5f - ArrowWidth * 0.5f),
                          ArrowWidth, ArrowWidth, new Vector2(0.5f, 1f));
            return button;
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

        private void Rebuild()
        {
            _currencies.Refresh(App.Session, TimeUtil.NowUnix());

            int dust = (int)App.Session.Profile.RelicDust;
            List<RelicDef> relics = App.Session.Content.Relics;
            int pages = PageCount();
            _page = Mathf.Clamp(_page, 0, pages - 1);

            _headerSubtitle.text = relics.Count + " RELICS AVAILABLE";
            _pageLabel.text = "PAGE " + (_page + 1) + " OF " + pages;
            _priceHint.text = "LEGENDARY " + App.Session.Balance.RelicShopPrice
                            + " DUST   -   MYTHIC " + App.Session.Balance.RelicMythicShopPrice + " DUST";

            _prevPage.Interactable = _page > 0;
            _nextPage.Interactable = _page < pages - 1;

            for (int i = _row.childCount - 1; i >= 0; i--)
                Object.Destroy(_row.GetChild(i).gameObject);

            if (relics.Count == 0)
            {
                Text empty = UIBuilder.Label("Empty", _row, "No relics in the content database.",
                                             UITheme.FontSmall, UITheme.TextDim, TextAnchor.MiddleCenter);
                UIBuilder.Sized(empty.rectTransform, ShelfWidth, CardHeight);
                return;
            }

            int first = _page * PageSize;
            for (int i = first; i < first + PageSize && i < relics.Count; i++)
                BuildRelicCard(relics[i], i, dust);
        }

        /// <summary>
        /// One relic, as a card. Grade drives the frame, the glow and the badge and nothing else -
        /// the layout of a legendary and a mythic is identical, so the two are compared on their
        /// text rather than on their shape.
        /// </summary>
        private void BuildRelicCard(RelicDef relic, int index, int dust)
        {
            bool mythic = relic.Grade == RelicGrade.Mythic;
            Color grade = UITheme.RelicGradeColor(relic.Grade);

            int price = App.Session.RelicPrice(relic);
            bool owned = App.Session.Profile.OwnsRelic(relic.Id);
            bool affordable = !owned && dust >= price;

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

            Image bg = card.gameObject.AddComponent<Image>();
            bg.sprite = TextureLab.NotchedRect(CardCut);
            bg.type = Image.Type.Sliced;
            bg.color = owned ? UITheme.WithAlpha(UITheme.PanelRaised, 0.72f) : UITheme.Panel;

            // Frame weight and brightness are the grade read: a mythic frame is a full-strength
            // three-pixel edge, a legendary a softer two.
            UIBuilder.NotchedFrame("Edge", card,
                                   UITheme.WithAlpha(grade, mythic ? 0.95f : 0.62f),
                                   CardCut, mythic ? 3 : 2);

            float top = UITheme.Space16;

            // ---- icon tile
            const float tile = 178f;
            RectTransform iconTile = UIBuilder.Node("Tile", card);
            UIBuilder.Box(iconTile, 0f, -top, tile, tile, new Vector2(0.5f, 1f));

            Image tileBg = iconTile.gameObject.AddComponent<Image>();
            tileBg.sprite = TextureLab.NotchedRect(TileCut);
            tileBg.type = Image.Type.Sliced;
            tileBg.color = UITheme.WithAlpha(UITheme.PanelSunken, 0.92f);
            tileBg.raycastTarget = false;
            UIBuilder.NotchedFrame("TileEdge", iconTile, UITheme.WithAlpha(grade, mythic ? 0.85f : 0.5f), TileCut, 2);

            // The relic's own mark. Sigil is the project's existing emblem set, indexed off the
            // relic id's hash so a given relic always draws the same mark - stable between sessions
            // and between here and anywhere else that wants to draw it later.
            Image mark = UIBuilder.Img("Mark", iconTile, TextureLab.Sigil(SigilFor(index), 128),
                                       UITheme.WithAlpha(grade, owned ? 0.55f : 0.9f));
            UIBuilder.Box(mark.rectTransform, 0f, 0f, tile * 0.58f, tile * 0.58f, new Vector2(0.5f, 0.5f));

            top += tile + UITheme.Space12;

            // ---- name
            Text nameLabel = UIBuilder.WrappedLabel("Name", card, relic.Name, UITheme.FontSmall,
                                                    UITheme.TextPrimary, TextAnchor.UpperCenter);
            UIBuilder.Box(nameLabel.rectTransform, 0f, -top, CardWidth - UITheme.Space24, 58f, new Vector2(0.5f, 1f));
            top += 58f + UITheme.Space4;

            // ---- grade badge
            RectTransform badge = UIBuilder.Chip("Grade", card, UITheme.RelicGradeLabel(relic.Grade),
                                                 grade, UITheme.FontTiny - 2);
            UIBuilder.Box(badge, 0f, -top, 148f, 28f, new Vector2(0.5f, 1f));
            top += 28f + UITheme.Space8;

            // ---- the relic's own text, from CardText() so the shop and the inventory cannot drift
            // into describing the same relic two ways.
            Text rule = UIBuilder.WrappedLabel("Rule", card, relic.CardText(), UITheme.FontTiny - 4,
                                               UITheme.TextMuted, TextAnchor.UpperCenter);
            rule.rectTransform.anchorMin = new Vector2(0f, 0f);
            rule.rectTransform.anchorMax = new Vector2(1f, 1f);
            rule.rectTransform.offsetMin = new Vector2(UITheme.Space12, 104f);
            rule.rectTransform.offsetMax = new Vector2(-UITheme.Space12, -top);

            // ---- cost chip, in the currency strip's own language
            RectTransform cost = UIBuilder.Node("Cost", card);
            UIBuilder.Box(cost, 0f, 62f, CardWidth - UITheme.Space24, 32f, new Vector2(0.5f, 0f));

            Image costPlate = cost.gameObject.AddComponent<Image>();
            costPlate.sprite = UISkin.Panel(8);
            costPlate.type = Image.Type.Sliced;
            costPlate.color = UITheme.WithAlpha(HubStyle.Plate, HubStyle.PlateAlphaSolid);
            costPlate.raycastTarget = false;

            Image costIcon = UIBuilder.Img("Icon", cost, TextureLab.SpiralIcon(64), UITheme.Dust);
            UIBuilder.Box(costIcon.rectTransform, UITheme.Space8, 0f, 24f, 24f, new Vector2(0f, 0.5f));

            Text costText = UIBuilder.Label("Value", cost, price.ToString(), UITheme.FontTiny,
                                            (affordable || owned) ? UITheme.TextPrimary : UITheme.Danger,
                                            TextAnchor.MiddleLeft);
            UIBuilder.FillParent(costText.rectTransform, UITheme.Space8 + 24f + UITheme.Space8, 0f, UITheme.Space8, 0f);

            // ---- action
            if (owned)
            {
                RectTransform ownedBadge = UIBuilder.Chip("Owned", card, "OWNED", UITheme.TextMuted, UITheme.FontTiny);
                UIBuilder.Box(ownedBadge, 0f, UITheme.Space16, CardWidth - UITheme.Space24, 40f, new Vector2(0.5f, 0f));
            }
            else
            {
                UIButton buy = UIBuilder.Notch(UIBuilder.GoldButton("Buy", card, "BUY", UITheme.FontSmall), 12);
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
            bool success = App.Session.BuyRelic(relic.Id, TimeUtil.NowUnix());
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
    }
}
