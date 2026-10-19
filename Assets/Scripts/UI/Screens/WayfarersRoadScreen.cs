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
    /// WAYFARER'S ROAD, THE FREE TRACK (Docs/DESIGN-WAYFARERS-ROAD.md): 30 tiers in three pages of ten,
    /// every reward shown up front, a season bar, two ways to buy a tier and, only with PassPaidSwitch
    /// on, a locked paid row. Reached from a door on the Orders page, because its points are that
    /// page's claims; built only when WayfarersRoadSwitch is on.
    ///
    /// THIS SCREEN DECIDES NOTHING. It draws WayfarersRoad.View and claims and buys through
    /// WayfarersRoad.Claim and BuyTier; every figure and state is a field of what those returned. A
    /// selector tier opens a hero picker inside this page with a confirm step that names the hero.
    /// The paid row has no button at all.
    /// </summary>
    public sealed class WayfarersRoadScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.WayfarersRoad; } }

        public override bool HidesTopBar { get { return false; } }

        private const float ContentWidth = 1760f;
        private const int PageSize = 10;
        private const float TileWidth = 156f;
        private const float TileHeight = 320f;
        private const float TileGap = 10f;
        private const float SummaryHeight = 172f;
        private const float RowTop = SummaryHeight + 12f;
        private const float PaidTop = RowTop + TileHeight + 12f;
        private const float PaidHeight = 104f;
        private const int PickPerRow = 7;
        private const int PickPerPage = 14;
        private const float PickCardWidth = 150f;
        private const float PickCardHeight = 180f;

        private Text _headerSubtitle;
        private RectTransform _content;
        private RectTransform _summary;
        private RectTransform _row;
        private RectTransform _paid;
        private RectTransform _pick;
        private UIButton _prev;
        private UIButton _next;
        private Text _pageLabel;

        private int _page;
        private int _pickTier;
        private int _pickPage;
        private string _chosen;

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            ScreenChrome.ForgedHeader(Root, "WAYFARER'S ROAD", Router, out _headerSubtitle);

            float top = ScreenChrome.HeaderTop + 108f + UITheme.Space8;
            _content = UIBuilder.Node("Content", Root);
            UIBuilder.Box(_content, 0f, -top, ContentWidth, PaidTop + PaidHeight + 40f, new Vector2(0.5f, 1f));

            _summary = UIBuilder.Node("Summary", _content);
            UIBuilder.Box(_summary, 0f, 0f, ContentWidth, SummaryHeight, new Vector2(0.5f, 1f));
            EmberforgeUI.PanelOn(_summary, true);

            _row = UIBuilder.Node("Tiers", _content);
            UIBuilder.Box(_row, 0f, -RowTop, PageSize * TileWidth + (PageSize - 1) * TileGap, TileHeight, new Vector2(0.5f, 1f));
            UIBuilder.HStack(_row, TileGap, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            _paid = UIBuilder.Node("Paid", _content);
            UIBuilder.Box(_paid, 0f, -PaidTop, PageSize * TileWidth + (PageSize - 1) * TileGap, PaidHeight, new Vector2(0.5f, 1f));

            _pick = UIBuilder.Node("Picker", _content);
            UIBuilder.Box(_pick, 0f, -RowTop, ContentWidth, TileHeight + 12f + PaidHeight, new Vector2(0.5f, 1f));

            float rowWidth = PageSize * TileWidth + (PageSize - 1) * TileGap;
            _prev = Arrow("Prev", "<", -(rowWidth * 0.5f + 52f), top);
            _next = Arrow("Next", ">", rowWidth * 0.5f + 52f, top);
            _prev.OnClick(delegate { TurnPage(-1); });
            _next.OnClick(delegate { TurnPage(1); });

            _pageLabel = EmberforgeUI.Label("Page", _content, "", UITheme.FontSmall, Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Box(_pageLabel.rectTransform, 0f, -(PaidTop + PaidHeight + 6f), 600f, 30f, new Vector2(0.5f, 1f));
        }

        private UIButton Arrow(string name, string glyph, float x, float top)
        {
            ForgeDisc disc = EmberforgeUI.IconButton(name, Root, "", TextureLab.Sigil(0, 64), Emberforge.TextGold, 64f, null);
            UIBuilder.Box(disc.Root, x, -(top + RowTop + TileHeight * 0.5f - HubStyle.TouchMin * 0.5f),
                          HubStyle.TouchMin, HubStyle.TouchMin, new Vector2(0.5f, 1f));
            disc.Glyph.enabled = false;
            Text mark = EmberforgeUI.Title("Glyph", disc.Face, glyph, UITheme.FontHeading, Emberforge.TextGold, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(mark.rectTransform, 0f);
            UIBuilder.Outlined(mark, Emberforge.Outline, Emberforge.StrokeThin);
            mark.raycastTarget = false;
            return disc.Button;
        }

        // ------------------------------------------------------------------ refresh

        public override void OnShow()
        {
            _pickTier = 0;
            _chosen = null;
            long now = TimeUtil.NowUnix();
            PassView view = WayfarersRoad.View(App.Session, now);
            // Open on the page that holds the next tier to claim, or the one past the last reached.
            if (view.Active) _page = Mathf.Clamp(Mathf.Max(0, (view.Reached - 1)) / PageSize, 0, 2);
            App.RefreshHud();
            Rebuild();
        }

        public override bool TryStepBack()
        {
            if (_pickTier == 0) return false;
            _pickTier = 0;
            _chosen = null;
            Rebuild();
            return true;
        }

        private void TurnPage(int delta)
        {
            int next = Mathf.Clamp(_page + delta, 0, WayfarersRoadConfig.TierCount / PageSize - 1);
            if (next == _page) return;
            _page = next;
            App.Audio.Tap();
            Rebuild();
        }

        private static void Clear(RectTransform body)
        {
            for (int i = body.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(body.GetChild(i).gameObject);
        }

        private void Rebuild()
        {
            long now = TimeUtil.NowUnix();
            PassView view = WayfarersRoad.View(App.Session, now);

            Clear(_summary);
            Clear(_row);
            Clear(_paid);
            Clear(_pick);

            bool picking = _pickTier != 0 && view.Active;
            _row.gameObject.SetActive(!picking);
            _paid.gameObject.SetActive(!picking);
            _pick.gameObject.SetActive(picking);
            int pages = WayfarersRoadConfig.TierCount / PageSize;
            _prev.Root.gameObject.SetActive(!picking);
            _next.Root.gameObject.SetActive(!picking);
            _prev.Interactable = _page > 0;
            _next.Interactable = _page < pages - 1;
            _pageLabel.text = picking ? "" : "TIERS " + (_page * PageSize + 1) + " TO " + ((_page + 1) * PageSize) + "   -   PAGE " + (_page + 1) + " OF " + pages;

            if (!view.Active)
            {
                _headerSubtitle.text = "NOT OPEN";
                Text closed = EmberforgeUI.Label("Closed", _summary, "The Wayfarer's Road is not running.", UITheme.FontBody,
                                                 Emberforge.TextQuiet, TextAnchor.MiddleCenter);
                UIBuilder.FillParent(closed.rectTransform, 0f);
                return;
            }

            _headerSubtitle.text = "SEASON " + view.Season + "   -   " + view.DaysLeft + (view.DaysLeft == 1 ? " DAY" : " DAYS") + " LEFT";
            BuildSummary(view);
            if (picking) { BuildPicker(view); return; }
            for (int i = _page * PageSize; i < (_page + 1) * PageSize && i < view.Tiers.Count; i++) BuildTile(view.Tiers[i]);
            BuildPaid(view);
        }

        // ------------------------------------------------------------------ the summary

        private void BuildSummary(PassView view)
        {
            Text title = EmberforgeUI.Title("Title", _summary, "TIER " + view.Reached + " OF " + WayfarersRoadConfig.TierCount, UITheme.FontBody,
                                            Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(title.rectTransform, 28f, -16f, 520f, 40f, new Vector2(0f, 1f));
            UIBuilder.Outlined(title, Emberforge.Outline, Emberforge.StrokeThin);

            int into = view.Points % Mathf.Max(1, view.TierPoints);
            Text pts = EmberforgeUI.Label("Points", _summary,
                view.Reached >= WayfarersRoadConfig.TierCount ? "THE TRACK IS COMPLETE"
                : view.Points + " POINTS   -   " + into + " / " + view.TierPoints + " TO THE NEXT TIER",
                UITheme.FontSmall, Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(pts.rectTransform, 28f, -58f, 760f, 30f, new Vector2(0f, 1f));

            Image bar = EmberforgeUI.ProgressBar("Bar", _summary);
            UIBuilder.Box(bar.rectTransform.parent as RectTransform, 28f, -92f, 760f, 22f, new Vector2(0f, 1f));
            EmberforgeUI.SetProgress(bar, view.TotalPoints > 0 ? Mathf.Clamp01(view.Points / (float)view.TotalPoints) : 0f);

            Text how = EmberforgeUI.Body("How", _summary,
                "Points come from claiming the daily orders and the two chests. Unclaimed tiers lapse when the season ends.",
                UITheme.FontTiny - 2, Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(how.rectTransform, 820f, -16f, 440f, 110f, new Vector2(0f, 1f));

            // Buying a tier: two earned purses, at most BuyMax a season.
            Text bought = EmberforgeUI.Label("Bought", _summary, "TIERS BOUGHT  " + view.Bought + " OF " + view.BuyMax,
                                             UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
            UIBuilder.Box(bought.rectTransform, -28f, -14f, 340f, 26f, new Vector2(1f, 1f));
            BuyButton("BuyValor", "BUY A TIER  -  " + HudTopBar.Format(view.ValorPrice) + " VALOR", -28f, -46f, view.CanBuyWithValor, PassCurrency.Valor);
            BuyButton("BuyCoda", "BUY A TIER  -  " + HudTopBar.Format(view.CodaPrice) + " CODA", -28f, -98f, view.CanBuyWithCoda, PassCurrency.Coda);
        }

        private void BuyButton(string name, string label, float x, float y, bool can, PassCurrency currency)
        {
            UIButton b = EmberforgeUI.Button(name, _summary, label, UITheme.FontTiny, ForgeButtonKind.Secondary);
            UIBuilder.Box(b.Root, x, y, 340f, 44f, new Vector2(1f, 1f));
            b.Interactable = can;
            PassCurrency captured = currency;
            b.OnClick(delegate { OnBuy(captured); });
        }

        // ------------------------------------------------------------------ a tier

        private void BuildTile(PassTierView t)
        {
            RectTransform tile = UIBuilder.Node("Tier_" + t.Tier, _row);
            UIBuilder.Sized(tile, TileWidth, TileHeight);
            if (t.Claimed) EmberforgeUI.PlainPanelOn(tile);
            else EmberforgeUI.PanelOn(tile, t.Kind == PassRewardKind.Selector);

            Color tint = t.Kind == PassRewardKind.Selector ? UITheme.Mythic
                       : t.Kind == PassRewardKind.Dust ? UITheme.Dust
                       : t.Kind == PassRewardKind.Slag ? UITheme.Slag : UITheme.Gold;

            Text num = EmberforgeUI.Title("Num", tile, t.Tier.ToString(), UITheme.FontBody,
                                          t.Reached ? Emberforge.TextGold : Emberforge.TextQuiet, TextAnchor.MiddleCenter);
            UIBuilder.Box(num.rectTransform, 0f, -8f, TileWidth - 12f, 36f, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(num, Emberforge.Outline, Emberforge.StrokeThin);

            const float icon = 76f;
            RectTransform iconTile = UIBuilder.Node("Icon", tile);
            UIBuilder.Box(iconTile, 0f, -52f, icon + 16f, icon + 16f, new Vector2(0.5f, 1f));
            Image bg = EmberforgeUI.SunkenOn(iconTile);
            bg.raycastTarget = false;
            Image mark = UIBuilder.Img("Mark", iconTile, IconFor(t.Kind), UITheme.WithAlpha(tint, t.Claimed ? 0.5f : 0.95f));
            UIBuilder.Box(mark.rectTransform, 0f, 0f, icon, icon, new Vector2(0.5f, 0.5f));

            Text line = EmberforgeUI.Body("Line", tile, t.Line, UITheme.FontTiny - 4, Emberforge.TextBody, TextAnchor.UpperCenter);
            UIBuilder.Box(line.rectTransform, 0f, -150f, TileWidth - 36f, 64f, new Vector2(0.5f, 1f));   // padded so a long line wraps inside the card
            Text detail = EmberforgeUI.Label("Detail", tile, t.Detail, UITheme.FontTiny - 6, Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Box(detail.rectTransform, 0f, -216f, TileWidth - 10f, 40f, new Vector2(0.5f, 1f));

            if (t.Claimed)
            {
                RectTransform chip = EmberforgeUI.Chip("State", tile, "CLAIMED", UITheme.Success, UITheme.FontTiny - 2);
                UIBuilder.Box(chip, 0f, UITheme.Space12, TileWidth - 16f, 38f, new Vector2(0.5f, 0f));
            }
            else if (t.CanClaim)
            {
                UIButton claim = EmberforgeUI.Button("Claim", tile, t.Kind == PassRewardKind.Selector ? "CHOOSE" : "CLAIM", UITheme.FontSmall, ForgeButtonKind.Primary);
                UIBuilder.Box(claim.Root, 0f, UITheme.Space12, TileWidth - 16f, 46f, new Vector2(0.5f, 0f));
                int tier = t.Tier;
                bool selector = t.Kind == PassRewardKind.Selector;
                claim.OnClick(delegate { if (selector) OpenPicker(tier); else OnClaim(tier, null); });
            }
            else
            {
                RectTransform chip = EmberforgeUI.Chip("State", tile, "NOT YET", Emberforge.TextQuiet, UITheme.FontTiny - 2);
                UIBuilder.Box(chip, 0f, UITheme.Space12, TileWidth - 16f, 38f, new Vector2(0.5f, 0f));
            }
        }

        private static Sprite IconFor(PassRewardKind kind)
        {
            switch (kind)
            {
                case PassRewardKind.Hourglass: return TextureLab.HourglassIcon(128);
                case PassRewardKind.Dust: return TextureLab.SpiralIcon(128);
                case PassRewardKind.Slag: return TextureLab.IngotIcon(128);
                case PassRewardKind.Selector: return TextureLab.Crown(128);
            }
            return TextureLab.Sigil(3, 128);
        }

        // ------------------------------------------------------------------ the paid track

        private void BuildPaid(PassView view)
        {
            if (view.Paid.Count == 0) return;
            Text title = EmberforgeUI.Label("Title", _paid, "PAID TRACK   -   LOCKED", UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(title.rectTransform, 4f, 0f, 600f, 26f, new Vector2(0f, 1f));
            for (int i = 0; i < view.Paid.Count; i++)
            {
                PassPaidView p = view.Paid[i];
                if (p.Tier <= _page * PageSize || p.Tier > (_page + 1) * PageSize) continue;
                float x = ((p.Tier - 1) % PageSize) * (TileWidth + TileGap);
                RectTransform cell = UIBuilder.Node("Paid_" + p.Tier, _paid);
                UIBuilder.Box(cell, x, -28f, TileWidth, PaidHeight - 30f, new Vector2(0f, 1f));
                EmberforgeUI.PlainPanelOn(cell);
                // The tier number and the reward are two lines, so "20" and "4 HOURS" never read as one number.
                Text tierLabel = EmberforgeUI.Label("Tier", cell, "TIER " + p.Tier, UITheme.FontTiny - 4, Emberforge.TextGold, TextAnchor.UpperCenter);
                UIBuilder.Box(tierLabel.rectTransform, 0f, -6f, TileWidth - 12f, 22f, new Vector2(0.5f, 1f));
                Text label = EmberforgeUI.Body("Line", cell, p.Line, UITheme.FontTiny - 6, Emberforge.TextQuiet, TextAnchor.UpperCenter);
                UIBuilder.Box(label.rectTransform, 0f, -30f, TileWidth - 24f, PaidHeight - 66f, new Vector2(0.5f, 1f));
            }
        }

        // ------------------------------------------------------------------ the picker

        private void OpenPicker(int tier)
        {
            App.Audio.Tap();
            _pickTier = tier;
            _pickPage = 0;
            _chosen = null;
            Rebuild();
        }

        /// <summary>CAPTURE ONLY: opens a selector's picker with a hero already chosen, to photograph its
        /// layout. The game never calls this; a tap does the same through OpenPicker and Choose.</summary>
        public void PickForCapture(int tier, string heroId)
        {
            _pickTier = tier;
            _pickPage = 0;
            _chosen = heroId;
            Rebuild();
        }

        private void BuildPicker(PassView view)
        {
            PassTierView tier = null;
            for (int i = 0; i < view.Tiers.Count; i++) if (view.Tiers[i].Tier == _pickTier) tier = view.Tiers[i];
            if (tier == null || tier.Kind != PassRewardKind.Selector || !tier.CanClaim) { _pickTier = 0; return; }

            List<HeroDef> heroes = WayfarersRoad.SelectorHeroes(App.Session, tier.Stars);
            int pages = Mathf.Max(1, (heroes.Count + PickPerPage - 1) / PickPerPage);
            _pickPage = Mathf.Clamp(_pickPage, 0, pages - 1);

            Text prompt = EmberforgeUI.Label("Prompt", _pick, "CHOOSE A " + tier.Stars + "-STAR   -   PAGE " + (_pickPage + 1) + " OF " + pages,
                                             UITheme.FontHeading, Emberforge.TextGold, TextAnchor.UpperCenter);
            UIBuilder.Box(prompt.rectTransform, 0f, 0f, ContentWidth, 48f, new Vector2(0.5f, 1f));

            float gridWidth = PickPerRow * PickCardWidth + (PickPerRow - 1) * 12f;
            RectTransform grid = UIBuilder.Node("Grid", _pick);
            UIBuilder.Box(grid, 0f, -56f, gridWidth, PickCardHeight * 2f + 12f, new Vector2(0.5f, 1f));
            UIBuilder.VStack(grid, 12f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
            RectTransform rowA = null;
            int first = _pickPage * PickPerPage;
            for (int i = first; i < first + PickPerPage && i < heroes.Count; i++)
            {
                if ((i - first) % PickPerRow == 0)
                {
                    rowA = UIBuilder.Node("Row" + ((i - first) / PickPerRow), grid);
                    UIBuilder.Sized(rowA, gridWidth, PickCardHeight);
                    UIBuilder.HStack(rowA, 12f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
                }
                BuildPickCard(rowA, heroes[i]);
            }

            float y = 56f + PickCardHeight * 2f + 12f + 16f;
            UIButton back = EmberforgeUI.Button("Back", _pick, "BACK", UITheme.FontSmall, ForgeButtonKind.Secondary);
            UIBuilder.Box(back.Root, -(gridWidth * 0.5f) + 100f, -y, 200f, 56f, new Vector2(0.5f, 1f));
            back.OnClick(delegate { TryStepBack(); });

            UIButton prev = EmberforgeUI.Button("PrevPick", _pick, "<", UITheme.FontSmall, ForgeButtonKind.Secondary);
            UIBuilder.Box(prev.Root, -(gridWidth * 0.5f) + 270f, -y, 80f, 56f, new Vector2(0.5f, 1f));
            prev.Interactable = _pickPage > 0;
            prev.OnClick(delegate { _pickPage--; App.Audio.Tap(); Rebuild(); });
            UIButton next = EmberforgeUI.Button("NextPick", _pick, ">", UITheme.FontSmall, ForgeButtonKind.Secondary);
            UIBuilder.Box(next.Root, -(gridWidth * 0.5f) + 360f, -y, 80f, 56f, new Vector2(0.5f, 1f));
            next.Interactable = _pickPage < pages - 1;
            next.OnClick(delegate { _pickPage++; App.Audio.Tap(); Rebuild(); });

            string chosenName = null;
            if (_chosen != null)
            {
                HeroDef def = App.Session.Content.GetHero(_chosen);
                if (def != null) chosenName = def.Name.ToUpperInvariant();
            }
            UIButton confirm = EmberforgeUI.Button("Confirm", _pick, chosenName == null ? "CHOOSE A HERO" : "TAKE " + chosenName,
                                                   UITheme.FontSmall, ForgeButtonKind.Primary);
            UIBuilder.Box(confirm.Root, 190f, -y, gridWidth - 560f, 56f, new Vector2(0.5f, 1f));
            confirm.Interactable = chosenName != null;
            int tierNumber = _pickTier;
            string pick = _chosen;
            confirm.OnClick(delegate { OnClaim(tierNumber, pick); });
        }

        private void BuildPickCard(RectTransform parent, HeroDef def)
        {
            string id = def.Id;
            bool chosen = _chosen == id;
            HeroInstance owned = App.Session.Profile.FindHeroByDefId(id);
            ForgeCard forged = EmberforgeUI.SelectableCard("Pick_" + id, parent, delegate { Choose(id); });
            EmberforgeUI.SetCardSelected(forged, chosen);
            UIBuilder.Sized(forged.Root, PickCardWidth, PickCardHeight);
            RectTransform card = forged.Body;

            RectTransform portrait = EmberforgeUI.PortraitFrame("Portrait", card, def, def.Stars, 96f);
            UIBuilder.Box(portrait, 0f, -UITheme.Space12, 96f, 96f, new Vector2(0.5f, 1f));
            Text label = EmberforgeUI.Label("Name", card, def.Name.ToUpperInvariant(), UITheme.FontTiny - 6,
                                            Emberforge.TextBody, TextAnchor.UpperCenter);
            UIBuilder.Box(label.rectTransform, 0f, -(UITheme.Space12 + 100f), PickCardWidth - 12f, 34f, new Vector2(0.5f, 1f));
            Text own = EmberforgeUI.Label("Own", card, owned == null ? "NEW" : "OWNED  x" + owned.Copies, UITheme.FontTiny - 4,
                                          owned == null ? Emberforge.TextGold : Emberforge.TextQuiet, TextAnchor.MiddleCenter);
            UIBuilder.Box(own.rectTransform, 0f, 4f, PickCardWidth - 12f, 24f, new Vector2(0.5f, 0f));
        }

        private void Choose(string heroId)
        {
            App.Audio.Tap();
            _chosen = heroId;
            Rebuild();
        }

        // ------------------------------------------------------------------ actions

        private void OnClaim(int tier, string heroId)
        {
            PassResult result = WayfarersRoad.Claim(App.Session, tier, heroId, TimeUtil.NowUnix());
            if (!result.Success)
            {
                App.Notify(RefusalLine(result.Reason));
                Rebuild();
                return;
            }
            App.Audio.Reward();
            App.Notify(PaidLine(result));
            _pickTier = 0;
            _chosen = null;
            App.RefreshHud();
            App.RequestSave();
            Rebuild();
        }

        private void OnBuy(PassCurrency currency)
        {
            PassResult result = WayfarersRoad.BuyTier(App.Session, currency, TimeUtil.NowUnix());
            if (!result.Success)
            {
                App.Notify(RefusalLine(result.Reason));
                Rebuild();
                return;
            }
            App.Audio.Reward();
            App.Notify("A tier was added to your road.");
            App.RefreshHud();
            App.RequestSave();
            Rebuild();
        }

        private static string PaidLine(PassResult r)
        {
            if (r.Hero != null) return "A copy of " + r.Hero.DisplayName + " joined your roster.";
            if (r.Dust > 0) return "+" + r.Dust + " Relic Dust.";
            if (r.Slag > 0) return "+" + r.Slag + " Slag.";
            return "+" + r.Gold + " Gold, +" + r.Shards + " Shards.";
        }

        private static string RefusalLine(PassRefusal why)
        {
            switch (why)
            {
                case PassRefusal.NotReached: return "That tier is not reached yet.";
                case PassRefusal.Claimed: return "That tier is already claimed.";
                case PassRefusal.BadChoice: return "Choose a hero first.";
                case PassRefusal.CapReached: return "No more tiers can be bought this season.";
                case PassRefusal.Full: return "The track is already complete.";
                case PassRefusal.NotEnough: return "Not enough to pay for that.";
                case PassRefusal.Closed: return "The Wayfarer's Road is not running.";
            }
            return "That cannot be done.";
        }
    }
}
