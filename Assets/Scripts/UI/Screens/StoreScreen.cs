using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;
using Emberfall.Game;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// THE STORE (Docs/DESIGN-STORE.md): three tabs. PASS shows Wayfarer's Road's free and paid tracks one
    /// above the other, tier by tier, with the paid track unlocked through the store. PACKS lists every offer
    /// with its contents in full and its price before anything can be bought. SHOPS links the four
    /// earned-currency shops in one place (they keep their own screens).
    ///
    /// THIS SCREEN DECIDES NOTHING. It draws StoreOffers.View and WayfarersRoad.Peek, claims through
    /// StoreOffers.ClaimLedger and ClaimGrowth and WayfarersRoad.Claim and ClaimPaid, and buys only through
    /// the purchase flow (PurchaseService): it never grants anything. In a release build the flow has no
    /// backend and every offer reads COMING SOON with no buy button.
    ///
    /// Every icon here is a placeholder drawn by TextureLab (listed for Pinoy in Docs/DESIGN-STORE.md).
    /// </summary>
    public sealed class StoreScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Store; } }

        public override bool HidesTopBar { get { return false; } }

        public enum Tab { Pass, Packs, Shops }

        private const float ContentWidth = 1760f;
        private const float BodyHeight = 796f;
        private const float TabWidth = 300f;
        private const int PageSize = 10;
        private const float TileWidth = 150f;
        private const float TileGap = 10f;
        private const float LabelColumn = 96f;
        private const float ListWidth = 560f;
        private const float RowHeight = 104f;
        private const int PickPerRow = 7;
        private const int PickPerPage = 14;
        private const float PickCardWidth = 150f;
        private const float PickCardHeight = 196f;

        private Text _headerSubtitle;
        private RectTransform _body;
        private Text _banner;
        private readonly ForgeTab[] _tabs = new ForgeTab[3];

        private Tab _tab = Tab.Packs;
        private int _page;
        private string _selected = "starter_pact";

        // the hero picker (a starter pact, a contract, a paid selector tier)
        private bool _picking;
        private string _pickTitle = "";
        /// <summary>What the offer gives besides the hero, one line under the picker title. Empty when there is none.</summary>
        private string _pickNote = "";
        private readonly List<HeroDef> _pickHeroes = new List<HeroDef>();
        private int _pickPage;
        private string _chosen;
        private Action<string> _pickConfirm;
        private string _pickConfirmLabel = "";

        private PurchaseService _service;

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            ScreenChrome.ForgedHeader(Root, "THE STORE", Router, out _headerSubtitle);

            float top = ScreenChrome.HeaderTop + 108f + UITheme.Space8;
            RectTransform tabRow = UIBuilder.Node("Tabs", Root);
            UIBuilder.Box(tabRow, 0f, -top, TabWidth * 3f + HubStyle.TabGap * 2f, HubStyle.TabHeight, new Vector2(0.5f, 1f));
            UIBuilder.HStack(tabRow, HubStyle.TabGap, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
            string[] names = { "PASS", "PACKS", "SHOPS" };
            for (int i = 0; i < 3; i++)
            {
                Tab which = (Tab)i;
                _tabs[i] = EmberforgeUI.Tab(names[i], tabRow, TabWidth, HubStyle.TabHeight, delegate { SetTab(which); });
            }

            _banner = EmberforgeUI.Label("Banner", Root, "", UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.MiddleRight);
            UIBuilder.Box(_banner.rectTransform, -UITheme.ScreenPadding, -top, 560f, HubStyle.TabHeight, new Vector2(1f, 1f));

            _body = UIBuilder.Node("Body", Root);
            UIBuilder.Box(_body, 0f, -(top + HubStyle.TabHeight + 12f), ContentWidth, BodyHeight, new Vector2(0.5f, 1f));
        }

        // ------------------------------------------------------------------ lifecycle

        public override void OnShow()
        {
            _picking = false;
            _chosen = null;
            Bind();
            App.RefreshHud();
            Rebuild();
        }

        public override void OnHide()
        {
            Unbind();
        }

        public override bool TryStepBack()
        {
            if (!_picking) return false;
            _picking = false;
            _chosen = null;
            Rebuild();
            return true;
        }

        /// <summary>Opens the Store on a tab. Used by the Hub's door and by the capture targets.</summary>
        public void OpenTab(Tab tab)
        {
            _tab = tab;
            _page = 0;
            _picking = false;
            Rebuild();
        }

        /// <summary>CAPTURE ONLY: selects an offer on the Packs tab, as a tap on its row does.</summary>
        public void SelectForCapture(string offerId)
        {
            _selected = offerId;
            _tab = Tab.Packs;
            _picking = false;
            Rebuild();
        }

        /// <summary>CAPTURE ONLY: opens the hero picker of an offer, with a hero already chosen when given.</summary>
        public void PickForCapture(string offerId, string heroId)
        {
            _tab = Tab.Packs;
            _selected = offerId;
            StoreView view = StoreOffers.View(App.Session, TimeUtil.NowUnix());
            StoreOfferView offer = Find(view, offerId);
            if (offer != null) OpenOfferPicker(offer);
            _chosen = heroId;
            Rebuild();
        }

        /// <summary>CAPTURE ONLY: sets the page of the Pass tab's tiers.</summary>
        public void PageForCapture(int page)
        {
            _page = Mathf.Clamp(page, 0, WayfarersRoadConfig.TierCount / PageSize - 1);
            Rebuild();
        }

        private void Bind()
        {
            StoreOffers.Bind(App.Session);
            if (_service == null)
            {
                List<StoreOffer> offers = new List<StoreOffer>();
                List<StoreOfferDef> defs = App.Session.Content.Store.Offers;
                for (int i = 0; i < defs.Count; i++) offers.Add(defs[i].Built);
                _service = PurchaseService.Ensure(StoreRules.Shared, offers);
            }
            if (_service != null && _service.Flow != null)
            {
                _service.Flow.Finished -= OnFinished;
                _service.Flow.Failed -= OnFailed;
                _service.Flow.Finished += OnFinished;
                _service.Flow.Failed += OnFailed;
            }
        }

        private void Unbind()
        {
            if (_service != null && _service.Flow != null)
            {
                _service.Flow.Finished -= OnFinished;
                _service.Flow.Failed -= OnFailed;
            }
        }

        private void SetTab(Tab tab)
        {
            if (_tab == tab && !_picking) return;
            App.Audio.Tap();
            _tab = tab;
            _page = 0;
            _picking = false;
            Rebuild();
        }

        private static void Clear(RectTransform body)
        {
            for (int i = body.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(body.GetChild(i).gameObject);
        }

        private static StoreOfferView Find(StoreView view, string id)
        {
            for (int i = 0; i < view.Offers.Count; i++) if (view.Offers[i].Id == id) return view.Offers[i];
            return null;
        }

        // ------------------------------------------------------------------ the frame

#if UNITY_EDITOR
        /// <summary>CAPTURE ONLY: draws the release build's look (every offer COMING SOON, no buy button) in the editor.</summary>
        public static bool CaptureComingSoon;
#endif

        private StoreMode Mode
        {
            get
            {
#if UNITY_EDITOR
                if (CaptureComingSoon) return StoreMode.ComingSoon;
#endif
                return _service != null ? _service.Mode : StoreMode.ComingSoon;
            }
        }

        private bool CanBuy
        {
            get
            {
#if UNITY_EDITOR
                if (CaptureComingSoon) return false;
#endif
                return _service != null && _service.Flow != null && _service.Flow.CanBuy;
            }
        }

        private void Rebuild()
        {
            long now = TimeUtil.NowUnix();
            Clear(_body);
            for (int i = 0; i < _tabs.Length; i++) EmberforgeUI.SetTabSelected(_tabs[i], (int)_tab == i);

            StoreView view = StoreOffers.View(App.Session, now);
            switch (Mode)
            {
                case StoreMode.Test: _banner.text = "TEST STORE  -  NO REAL MONEY MOVES"; _banner.color = Emberforge.TextGold; break;
                case StoreMode.Live: _banner.text = ""; break;
                default: _banner.text = "COMING SOON  -  PURCHASES OPEN WITH THE STORE"; _banner.color = Emberforge.TextQuiet; break;
            }

            if (!view.Active)
            {
                _headerSubtitle.text = "CLOSED";
                Text closed = EmberforgeUI.Label("Closed", _body, "The Store is not open.", UITheme.FontBody, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
                UIBuilder.FillParent(closed.rectTransform, 0f);
                return;
            }

            if (_picking) { _headerSubtitle.text = "CHOOSE A HERO"; BuildPicker(); return; }
            switch (_tab)
            {
                case Tab.Pass: _headerSubtitle.text = "WAYFARER'S ROAD  -  FREE AND PAID"; BuildPass(view, now); break;
                case Tab.Packs: _headerSubtitle.text = "EVERY PACK IS FIXED AND SHOWN IN FULL"; BuildPacks(view, now); break;
                default: _headerSubtitle.text = "THE SHOPS YOU SPEND EARNED CURRENCY IN"; BuildShops(); break;
            }
        }

        // ------------------------------------------------------------------ the pass tab

        private void BuildPass(StoreView view, long now)
        {
            if (!view.PassOpen)
            {
                Text none = EmberforgeUI.Label("None", _body, "No season is running.", UITheme.FontBody, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
                UIBuilder.FillParent(none.rectTransform, 0f);
                return;
            }
            PassView road = WayfarersRoad.Peek(App.Session, now);   // read only: opening the tab writes nothing; a claim or a purchase rolls the season itself
            StoreOfferView premium = Find(view, StoreOffers.PassOfferId);

            RectTransform summary = UIBuilder.Node("Summary", _body);
            UIBuilder.Box(summary, 0f, 0f, ContentWidth, 116f, new Vector2(0.5f, 1f));
            EmberforgeUI.PanelOn(summary, true);

            Text title = EmberforgeUI.Title("Title", summary, "SEASON " + view.PassSeason + "   -   " + view.PassDaysLeft + (view.PassDaysLeft == 1 ? " DAY" : " DAYS") + " LEFT",
                                            UITheme.FontBody, Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(title.rectTransform, 28f, -14f, 900f, 40f, new Vector2(0f, 1f));
            UIBuilder.Outlined(title, Emberforge.Outline, Emberforge.StrokeThin);
            string how = view.PassNeedsLoop
                ? "Points come from the daily orders, which this build does not run. The tracks are shown; nothing can be earned."
                : "TIER " + view.PassReached + " OF " + WayfarersRoadConfig.TierCount + " REACHED   -   points come from the daily orders and chests";
            Text sub = EmberforgeUI.Label("Sub", summary, how, UITheme.FontTiny, view.PassNeedsLoop ? UITheme.Danger : Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(sub.rectTransform, 28f, -58f, 1000f, 30f, new Vector2(0f, 1f));
            Text track = EmberforgeUI.Label("Track", summary, "The free track is yours. The paid track is bought once a season; everything on it is shown below.",
                                            UITheme.FontTiny - 2, Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(track.rectTransform, 28f, -84f, 1100f, 26f, new Vector2(0f, 1f));

            UIButton openRoad = EmberforgeUI.Button("OpenRoad", summary, "THE ROAD", UITheme.FontTiny, ForgeButtonKind.Secondary);
            UIBuilder.Box(openRoad.Root, -28f, -10f, 300f, 44f, new Vector2(1f, 1f));
            openRoad.Interactable = Router.WayfarersRoad != null;
            openRoad.OnClick(delegate { App.Audio.Tap(); Router.Show(ScreenId.WayfarersRoad); });

            if (view.PassOwned)
            {
                RectTransform chip = EmberforgeUI.Chip("Owned", summary, "PREMIUM ACTIVE", UITheme.Success, UITheme.FontTiny);
                UIBuilder.Box(chip, -28f, -62f, 300f, 44f, new Vector2(1f, 1f));
            }
            else if (premium != null)
            {
                BuyButton(summary, premium, "UNLOCK PREMIUM", -28f, -62f, 300f, 44f, UITheme.FontTiny);
            }

            // the two tracks, ten tiers a page
            float rowTop = 128f;
            float free = rowTop + 34f;
            const float tileHeight = 214f;
            float paid = free + tileHeight + 10f;
            float gridX = -(ContentWidth * 0.5f) + 12f;
            Text freeLabel = EmberforgeUI.Title("FreeLabel", _body, "FREE", UITheme.FontSmall, Emberforge.TextGold, TextAnchor.MiddleCenter);
            UIBuilder.Box(freeLabel.rectTransform, gridX, -free, LabelColumn, tileHeight, new Vector2(0.5f, 1f));
            freeLabel.rectTransform.anchoredPosition = new Vector2(-(ContentWidth * 0.5f) + 12f + LabelColumn * 0.5f, -free - tileHeight * 0.5f);
            freeLabel.rectTransform.sizeDelta = new Vector2(LabelColumn, 44f);
            Text paidLabel = EmberforgeUI.Title("PaidLabel", _body, "PAID", UITheme.FontSmall, view.PassOwned ? Emberforge.TextGold : Emberforge.TextQuiet, TextAnchor.MiddleCenter);
            UIBuilder.Box(paidLabel.rectTransform, 0f, 0f, LabelColumn, 44f, new Vector2(0.5f, 1f));
            paidLabel.rectTransform.anchoredPosition = new Vector2(-(ContentWidth * 0.5f) + 12f + LabelColumn * 0.5f, -paid - tileHeight * 0.5f);

            float firstX = -(ContentWidth * 0.5f) + 12f + LabelColumn + 8f;
            for (int c = 0; c < PageSize; c++)
            {
                int tier = _page * PageSize + c + 1;
                float x = firstX + c * (TileWidth + TileGap) + TileWidth * 0.5f;
                PassTierView freeTier = tier - 1 < road.Tiers.Count ? road.Tiers[tier - 1] : null;
                StorePaidTierView paidTier = null;
                for (int p = 0; p < view.Paid.Count; p++) if (view.Paid[p].Tier == tier) paidTier = view.Paid[p];

                Text num = EmberforgeUI.Title("Num" + tier, _body, tier.ToString(), UITheme.FontSmall,
                                              road.Reached >= tier ? Emberforge.TextGold : Emberforge.TextQuiet, TextAnchor.MiddleCenter);
                UIBuilder.Box(num.rectTransform, 0f, 0f, TileWidth, 30f, new Vector2(0.5f, 1f));
                num.rectTransform.anchoredPosition = new Vector2(x, -(rowTop + 14f));
                UIBuilder.Outlined(num, Emberforge.Outline, Emberforge.StrokeThin);

                if (freeTier != null) BuildFreeTile(freeTier, x, free, tileHeight);
                BuildPaidTile(paidTier, tier, view.PassOwned, x, paid, tileHeight);
            }

            // the pages
            float navY = paid + tileHeight + 14f;
            int pages = WayfarersRoadConfig.TierCount / PageSize;
            UIButton prev = EmberforgeUI.Button("Prev", _body, "<", UITheme.FontSmall, ForgeButtonKind.Secondary);
            UIBuilder.Box(prev.Root, -160f, -navY, 96f, 52f, new Vector2(0.5f, 1f));
            prev.Interactable = _page > 0;
            prev.OnClick(delegate { _page--; App.Audio.Tap(); Rebuild(); });
            UIButton next = EmberforgeUI.Button("Next", _body, ">", UITheme.FontSmall, ForgeButtonKind.Secondary);
            UIBuilder.Box(next.Root, 160f, -navY, 96f, 52f, new Vector2(0.5f, 1f));
            next.Interactable = _page < pages - 1;
            next.OnClick(delegate { _page++; App.Audio.Tap(); Rebuild(); });
            Text label = EmberforgeUI.Label("Page", _body, "TIERS " + (_page * PageSize + 1) + " TO " + ((_page + 1) * PageSize) + "   -   PAGE " + (_page + 1) + " OF " + pages,
                                            UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
            UIBuilder.Box(label.rectTransform, 0f, -navY, 320f, 52f, new Vector2(0.5f, 1f));
        }

        private RectTransform Tile(string name, float x, float y, float height, bool claimed, bool highlight)
        {
            RectTransform tile = UIBuilder.Node(name, _body);
            UIBuilder.Box(tile, x, -y, TileWidth, height, new Vector2(0.5f, 1f));
            if (claimed) EmberforgeUI.PlainPanelOn(tile);
            else EmberforgeUI.PanelOn(tile, highlight);
            return tile;
        }

        private void BuildFreeTile(PassTierView t, float x, float y, float height)
        {
            RectTransform tile = Tile("Free" + t.Tier, x, y, height, t.Claimed, t.Kind == PassRewardKind.Selector);
            TileFace(tile, t.Kind, t.Line, t.Detail, t.Claimed ? 0.5f : 0.95f, height);
            int tier = t.Tier;
            if (t.Claimed) StateChip(tile, "CLAIMED", UITheme.Success);
            else if (t.CanClaim)
            {
                bool selector = t.Kind == PassRewardKind.Selector;
                TileButton(tile, selector ? "CHOOSE" : "CLAIM", delegate
                {
                    if (selector) { App.Audio.Tap(); Router.Show(ScreenId.WayfarersRoad); return; }
                    ClaimFree(tier);
                });
            }
            else StateChip(tile, "NOT YET", Emberforge.TextQuiet);
        }

        private void BuildPaidTile(StorePaidTierView t, int tier, bool owned, float x, float y, float height)
        {
            if (t == null)
            {
                RectTransform empty = Tile("Paid" + tier, x, y, height, true, false);
                Text dash = EmberforgeUI.Label("Dash", empty, "-", UITheme.FontHeading, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
                UIBuilder.FillParent(dash.rectTransform, 0f);
                return;
            }
            RectTransform tile = Tile("Paid" + tier, x, y, height, t.Claimed || !owned, t.Kind == PassRewardKind.Selector);
            TileFace(tile, t.Kind, t.Short, "SEALED", owned ? (t.Claimed ? 0.5f : 0.95f) : 0.4f, height);
            if (!owned)
            {
                Image pad = UIBuilder.Img("Lock", tile, TextureLab.Padlock(64), UITheme.WithAlpha(Emberforge.TextQuiet, 0.9f));
                UIBuilder.Box(pad.rectTransform, 0f, 30f, 38f, 38f, new Vector2(0.5f, 0f));
                return;
            }
            if (t.Claimed) { StateChip(tile, "CLAIMED", UITheme.Success); return; }
            if (!t.CanClaim) { StateChip(tile, "NOT YET", Emberforge.TextQuiet); return; }
            int number = t.Tier;
            PassRewardKind kind = t.Kind;
            int stars = t.Stars;
            TileButton(tile, kind == PassRewardKind.Selector ? "CHOOSE" : "CLAIM", delegate
            {
                if (kind != PassRewardKind.Selector) { ClaimPaid(number, null); return; }
                OpenPaidPicker(number, stars);
            });
        }

        private void TileFace(RectTransform tile, PassRewardKind kind, string line, string detail, float alpha, float height)
        {
            Color tint = kind == PassRewardKind.Selector ? UITheme.Mythic
                       : kind == PassRewardKind.Dust ? UITheme.Dust
                       : kind == PassRewardKind.Slag ? UITheme.Slag
                       : kind == PassRewardKind.Cosmetic ? Emberforge.TextGold : UITheme.Gold;
            const float icon = 56f;
            Image mark = UIBuilder.Img("Mark", tile, IconFor(kind), UITheme.WithAlpha(tint, alpha));
            UIBuilder.Box(mark.rectTransform, 0f, -12f, icon, icon, new Vector2(0.5f, 1f));
            Text text = EmberforgeUI.Body("Line", tile, line, UITheme.FontTiny - 4, Emberforge.TextBody, TextAnchor.UpperCenter);
            UIBuilder.Box(text.rectTransform, 0f, -74f, TileWidth - 16f, 70f, new Vector2(0.5f, 1f));
            if (detail != null && detail.Length > 0)
            {
                Text d = EmberforgeUI.Label("Detail", tile, detail, UITheme.FontTiny - 6, Emberforge.TextQuiet, TextAnchor.UpperCenter);
                UIBuilder.Box(d.rectTransform, 0f, -146f, TileWidth - 8f, 22f, new Vector2(0.5f, 1f));
            }
        }

        private static void StateChip(RectTransform tile, string text, Color tint)
        {
            RectTransform chip = EmberforgeUI.Chip("State", tile, text, tint, UITheme.FontTiny - 4);
            UIBuilder.Box(chip, 0f, UITheme.Space8, TileWidth - 20f, 34f, new Vector2(0.5f, 0f));
        }

        private void TileButton(RectTransform tile, string text, Action action)
        {
            UIButton b = EmberforgeUI.Button("Claim", tile, text, UITheme.FontTiny, ForgeButtonKind.Primary);
            UIBuilder.Box(b.Root, 0f, UITheme.Space8, TileWidth - 18f, 42f, new Vector2(0.5f, 0f));
            b.OnClick(delegate { action(); });
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

        private void ClaimFree(int tier)
        {
            PassResult r = WayfarersRoad.Claim(App.Session, tier, null, TimeUtil.NowUnix());
            AfterClaim(r.Success, r.Success ? ClaimLine(r) : "That cannot be claimed.");
        }

        private void ClaimPaid(int tier, string heroId)
        {
            PassResult r = WayfarersRoad.ClaimPaid(App.Session, tier, heroId, TimeUtil.NowUnix());
            AfterClaim(r.Success, r.Success ? ClaimLine(r) : "That cannot be claimed.");
        }

        private static string ClaimLine(PassResult r)
        {
            if (r.Hero != null) return "A Sealed copy of " + r.Hero.DisplayName + " joined your roster.";
            if (r.Dust > 0) return "+" + r.Dust + " Relic Dust.";
            if (r.Slag > 0) return "+" + r.Slag + " Slag.";
            if (r.Gold > 0) return "+" + HudTopBar.Format(r.Gold) + " Gold.";
            return "Claimed.";
        }

        private void AfterClaim(bool success, string line)
        {
            if (success) { App.Audio.Reward(); App.RefreshHud(); App.RequestSave(); }
            else App.Audio.Locked();
            App.Notify(line);
            _picking = false;
            _chosen = null;
            Rebuild();
        }

        private void OpenPaidPicker(int tier, int stars)
        {
            App.Audio.Tap();
            _pickHeroes.Clear();
            _pickHeroes.AddRange(WayfarersRoad.SelectorHeroes(App.Session, stars));
            _pickTitle = "CHOOSE A " + stars + "-STAR  -  TIER " + tier + " OF THE PAID TRACK";
            _pickNote = "";
            _pickConfirmLabel = "TAKE ";
            _pickPage = 0;
            _chosen = null;
            _picking = true;
            _pickConfirm = delegate (string heroId) { ClaimPaid(tier, heroId); };
            Rebuild();
        }

        // ------------------------------------------------------------------ the packs tab

        private void BuildPacks(StoreView view, long now)
        {
            if (view.Offers.Count == 0) return;
            if (Find(view, _selected) == null) _selected = view.Offers[0].Id;

            for (int i = 0; i < view.Offers.Count; i++) BuildRow(view.Offers[i], i);
            BuildDetail(Find(view, _selected), now);

            if (_service != null && _service.Flow != null && _service.Flow.CanBuy)
            {
                UIButton restore = EmberforgeUI.Button("Restore", _body, "RESTORE PURCHASES", UITheme.FontTiny, ForgeButtonKind.Secondary);
                UIBuilder.Box(restore.Root, 0f, 0f, ListWidth, 48f, new Vector2(0f, 0f));
                restore.OnClick(delegate { App.Audio.Tap(); _service.Flow.Restore(delegate (string offerId) { return ""; }); App.Notify("Asking the store for earlier purchases."); Rebuild(); });
            }
            BuildWaitingPicks();
        }

        /// <summary>A paid purchase (or a restore) the store delivered with no pick waits in the flow, granted nothing
        /// and still open in the store. One button per waiting entry asks the player for the hero and finishes it.
        /// The screen decides nothing: FinishPurchase and FinishRestore grant, the flow confirms.</summary>
        private void BuildWaitingPicks()
        {
            if (_service == null || _service.Flow == null) return;
            List<string[]> waiting = new List<string[]>();
            List<bool> restore = new List<bool>();
            for (int i = 0; i < _service.Flow.PurchaseWaiting.Count; i++) { waiting.Add(_service.Flow.PurchaseWaiting[i]); restore.Add(false); }
            for (int i = 0; i < _service.Flow.RestoreWaiting.Count; i++) { waiting.Add(_service.Flow.RestoreWaiting[i]); restore.Add(true); }
            for (int i = 0; i < waiting.Count && i < 2; i++)
            {
                string[] entry = waiting[i];
                bool isRestore = restore[i];
                StoreOffer offer = StoreRules.Shared.FindByProduct(entry[0]);
                string name = offer == null ? "PURCHASE" : App.Session.Content.Store.Offer(offer.Id) != null ? App.Session.Content.Store.Offer(offer.Id).Name.ToUpperInvariant() : offer.Id.ToUpperInvariant();
                UIButton pick = EmberforgeUI.Button("Waiting" + i, _body, (isRestore ? "RESTORED: " : "PAID: ") + name + "  -  CHOOSE YOUR HERO", UITheme.FontTiny, ForgeButtonKind.Primary);
                UIBuilder.Box(pick.Root, ListWidth + 24f, i * 56f, 620f, 48f, new Vector2(0f, 0f));
                pick.OnClick(delegate { OpenWaitingPicker(entry, isRestore); });
            }
        }

        private void OpenWaitingPicker(string[] entry, bool isRestore)
        {
            StoreOffer offer = StoreRules.Shared.FindByProduct(entry[0]);
            StoreOfferDef def = offer == null ? null : App.Session.Content.Store.Offer(offer.Id);
            if (def == null || def.HeroChoices.Count == 0) { App.Notify("Nothing to choose."); return; }
            App.Audio.Tap();
            _pickHeroes.Clear();
            for (int i = 0; i < def.HeroChoices.Count; i++)
            {
                HeroDef h = App.Session.Content.GetHero(def.HeroChoices[i]);
                if (h != null) _pickHeroes.Add(h);
            }
            _pickTitle = def.Name.ToUpperInvariant() + "  -  YOU HAVE PAID, CHOOSE YOUR HERO";
            _pickNote = "";
            _pickConfirmLabel = "TAKE ";
            _pickPage = 0;
            _chosen = null;
            _picking = true;
            string productId = entry[0];
            _pickConfirm = delegate (string heroId)
            {
                PurchaseResult r = isRestore ? _service.Flow.FinishRestore(productId, heroId) : _service.Flow.FinishPurchase(productId, heroId);
                _picking = false;
                _chosen = null;
                if (!r.Success && r.Reason != PurchaseRefusal.AlreadyGranted) { App.Audio.Locked(); App.Notify("That could not be finished. Nothing was lost."); }
                Rebuild();
            };
            Rebuild();
        }

#if UNITY_EDITOR
        /// <summary>CAPTURE ONLY: opens the pick prompt of the first waiting paid purchase.</summary>
        public void WaitingPickForCapture(string heroId)
        {
            _tab = Tab.Packs;
            _picking = false;
            Rebuild();
            if (_service == null || _service.Flow == null) return;
            if (_service.Flow.PurchaseWaiting.Count > 0) OpenWaitingPicker(_service.Flow.PurchaseWaiting[0], false);
            else if (_service.Flow.RestoreWaiting.Count > 0) OpenWaitingPicker(_service.Flow.RestoreWaiting[0], true);
            _chosen = heroId;
            Rebuild();
        }
#endif

        private void BuildRow(StoreOfferView o, int index)
        {
            bool selected = o.Id == _selected;
            string id = o.Id;
            ForgeCard card = EmberforgeUI.SelectableCard("Offer_" + id, _body, delegate { App.Audio.Tap(); _selected = id; Rebuild(); });
            EmberforgeUI.SetCardSelected(card, selected);
            UIBuilder.Box(card.Root, 0f, -(index * (RowHeight + 8f)), ListWidth, RowHeight, new Vector2(0f, 1f));
            RectTransform body = card.Body;

            Image icon = UIBuilder.Img("Icon", body, OfferIcon(o.Kind), UITheme.WithAlpha(OfferTint(o.Kind), 0.95f));
            UIBuilder.Box(icon.rectTransform, 14f, 0f, 56f, 56f, new Vector2(0f, 0.5f));
            Text name = EmberforgeUI.Title("Name", body, o.Name.ToUpperInvariant(), UITheme.FontSmall, selected ? Emberforge.TextGold : Emberforge.TextBody, TextAnchor.UpperLeft);
            UIBuilder.Box(name.rectTransform, 84f, -12f, ListWidth - 270f, 34f, new Vector2(0f, 1f));
            Text price = EmberforgeUI.Label("Price", body, o.PriceLabel + "   -   " + o.LimitLabel, UITheme.FontTiny - 2, Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(price.rectTransform, 84f, -52f, ListWidth - 270f, 28f, new Vector2(0f, 1f));

            string stateText;
            Color tint;
            if (_service != null && _service.Flow != null && _service.Flow.IsPending(o.Id)) { stateText = "WAITING"; tint = Emberforge.TextGold; }
            else if (o.Owned && o.Kind == StoreOfferKind.Ledger) { stateText = o.DaysLeft + (o.DaysLeft == 1 ? " DAY LEFT" : " DAYS LEFT"); tint = UITheme.Success; }
            else if (o.Owned) { stateText = "BOUGHT"; tint = UITheme.Success; }
            else if (!o.Available) { stateText = StateWord(o.Reason); tint = Emberforge.TextQuiet; }
            else { stateText = o.PriceLabel; tint = Emberforge.TextGold; }
            RectTransform chip = EmberforgeUI.Chip("State", body, stateText, tint, UITheme.FontTiny - 4);
            UIBuilder.Box(chip, -12f, 0f, 170f, 36f, new Vector2(1f, 0.5f));
        }

        private static string StateWord(StoreRefusal r)
        {
            switch (r)
            {
                case StoreRefusal.SoldOut: return "SOLD OUT";
                case StoreRefusal.PassInert: return "NEEDS ORDERS";
                case StoreRefusal.PassClosed: return "NO SEASON";
                case StoreRefusal.NotOwned: return "NO 5-STAR";
            }
            return "UNAVAILABLE";
        }

        private static Sprite OfferIcon(StoreOfferKind kind)
        {
            switch (kind)
            {
                case StoreOfferKind.Starter: return TextureLab.Crown(128);
                case StoreOfferKind.Pack: return TextureLab.Chest(128);
                case StoreOfferKind.Ledger: return TextureLab.HourglassIcon(128);
                case StoreOfferKind.Growth: return TextureLab.GemIcon(128);
                case StoreOfferKind.Pass: return TextureLab.Sigil(3, 128);
                case StoreOfferKind.Contract: return TextureLab.Swords(128);
            }
            return TextureLab.Sigil(0, 128);
        }

        private static Color OfferTint(StoreOfferKind kind)
        {
            switch (kind)
            {
                case StoreOfferKind.Starter: return UITheme.Mythic;
                case StoreOfferKind.Ledger: return UITheme.Gold;
                case StoreOfferKind.Growth: return UITheme.Shard;
                case StoreOfferKind.Contract: return UITheme.Ember;
            }
            return UITheme.Gold;
        }

        private void BuildDetail(StoreOfferView o, long now)
        {
            RectTransform pane = UIBuilder.Node("Detail", _body);
            UIBuilder.Box(pane, 0f, 0f, ContentWidth - ListWidth - 20f, BodyHeight, new Vector2(1f, 1f));
            EmberforgeUI.PanelOn(pane, true);
            if (o == null) return;

            Image icon = UIBuilder.Img("Icon", pane, OfferIcon(o.Kind), UITheme.WithAlpha(OfferTint(o.Kind), 0.95f));
            UIBuilder.Box(icon.rectTransform, 28f, -24f, 72f, 72f, new Vector2(0f, 1f));
            Text name = EmberforgeUI.Title("Name", pane, o.Name.ToUpperInvariant(), UITheme.FontHeading, Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(name.rectTransform, 116f, -22f, 800f, 44f, new Vector2(0f, 1f));
            UIBuilder.Outlined(name, Emberforge.Outline, Emberforge.StrokeThin);
            Text price = EmberforgeUI.Label("Price", pane, o.PriceLabel + " (placeholder price)   -   " + o.LimitLabel, UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(price.rectTransform, 116f, -68f, 900f, 28f, new Vector2(0f, 1f));
            Text blurb = EmberforgeUI.Body("Blurb", pane, o.Blurb, UITheme.FontSmall - 2, Emberforge.TextBody, TextAnchor.UpperLeft);
            UIBuilder.Box(blurb.rectTransform, 28f, -112f, 1100f, 56f, new Vector2(0f, 1f));

            Text contents = EmberforgeUI.Label("ContentsLabel", pane, "WHAT YOU GET, IN FULL", UITheme.FontTiny, Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(contents.rectTransform, 28f, -176f, 600f, 28f, new Vector2(0f, 1f));

            float y = 210f;
            if (o.Kind == StoreOfferKind.Growth && o.Milestones.Count > 0)
            {
                for (int i = 0; i < o.Milestones.Count; i++)
                {
                    StoreMilestoneView m = o.Milestones[i];
                    Text line = EmberforgeUI.Body("M" + i, pane, m.Line, UITheme.FontTiny, m.Reached ? Emberforge.TextBody : Emberforge.TextQuiet, TextAnchor.MiddleLeft);
                    UIBuilder.Box(line.rectTransform, 28f, -y, 800f, 50f, new Vector2(0f, 1f));
                    int index = m.Index;
                    if (m.Claimed)
                    {
                        RectTransform chip = EmberforgeUI.Chip("C" + i, pane, "CLAIMED", UITheme.Success, UITheme.FontTiny - 4);
                        UIBuilder.Box(chip, 860f, -y - 6f, 190f, 38f, new Vector2(0f, 1f));
                    }
                    else if (m.CanClaim)
                    {
                        UIButton claim = EmberforgeUI.Button("Claim" + i, pane, "CLAIM", UITheme.FontTiny, ForgeButtonKind.Primary);
                        UIBuilder.Box(claim.Root, 860f, -y - 4f, 190f, 42f, new Vector2(0f, 1f));
                        claim.OnClick(delegate { ClaimGrowth(index); });
                    }
                    else
                    {
                        RectTransform chip = EmberforgeUI.Chip("C" + i, pane, m.Reached ? "BUY FIRST" : "NOT YET", Emberforge.TextQuiet, UITheme.FontTiny - 4);
                        UIBuilder.Box(chip, 860f, -y - 6f, 190f, 38f, new Vector2(0f, 1f));
                    }
                    y += 54f;
                }
                Text fixedNote = EmberforgeUI.Label("FixedNote", pane, "Each reward is priced at its own stage, whenever you claim it.", UITheme.FontTiny - 2, Emberforge.TextQuiet, TextAnchor.UpperLeft);
                UIBuilder.Box(fixedNote.rectTransform, 28f, -(y + 6f), 900f, 28f, new Vector2(0f, 1f));
            }
            else
            {
                for (int i = 0; i < o.Lines.Count; i++)
                {
                    Text line = EmberforgeUI.Body("L" + i, pane, o.Lines[i], UITheme.FontSmall - 2, Emberforge.TextBody, TextAnchor.UpperLeft);
                    UIBuilder.Box(line.rectTransform, 28f, -y, ContentWidth - ListWidth - 80f, 34f, new Vector2(0f, 1f));
                    y += 34f;
                }
            }

            if (o.Kind == StoreOfferKind.Ledger && o.Owned)
            {
                Text left = EmberforgeUI.Label("Left", pane, "RUNNING   -   " + o.DaysLeft + (o.DaysLeft == 1 ? " DAY" : " DAYS") + " LEFT", UITheme.FontSmall, UITheme.Success, TextAnchor.UpperLeft);
                UIBuilder.Box(left.rectTransform, 28f, -(y + 14f), 700f, 32f, new Vector2(0f, 1f));
                UIButton claim = EmberforgeUI.Button("ClaimLedger", pane, o.CanClaim ? "CLAIM TODAY" : "CLAIMED TODAY", UITheme.FontSmall, ForgeButtonKind.Primary);
                UIBuilder.Box(claim.Root, 28f, -(y + 56f), 380f, 58f, new Vector2(0f, 1f));
                claim.Interactable = o.CanClaim;
                claim.OnClick(delegate { ClaimLedger(); });
            }

            BuildBuyBar(pane, o, now);
        }

        private void BuildBuyBar(RectTransform pane, StoreOfferView o, long now)
        {
            bool pending = _service != null && _service.Flow != null && _service.Flow.IsPending(o.Id);
            string note = "";
            if (o.Owned && (o.Kind == StoreOfferKind.Growth || o.Kind == StoreOfferKind.Pass))
            {
                RectTransform chip = EmberforgeUI.Chip("Bought", pane, o.Kind == StoreOfferKind.Pass ? "PREMIUM ACTIVE THIS SEASON" : "BOUGHT", UITheme.Success, UITheme.FontSmall);
                UIBuilder.Box(chip, 28f, 24f, 420f, 60f, new Vector2(0f, 0f));
                return;
            }
            if (!o.Available && !(o.Kind == StoreOfferKind.Ledger && o.Owned))
            {
                note = StoreOffers.ReasonText(o.Reason);
                if (o.Reason == StoreRefusal.SoldOut && o.ResetInSeconds > 0) note += " Back in " + Span(o.ResetInSeconds) + ".";
            }
            else if (o.Kind == StoreOfferKind.Ledger && o.Owned) note = "A card is running. Buy another when it ends.";
            else if (!CanBuy) note = "Coming soon. Purchases open with the store; nothing is charged in this build.";

            if (note.Length > 0)
            {
                Text n = EmberforgeUI.Body("Note", pane, note, UITheme.FontSmall - 2, Emberforge.TextQuiet, TextAnchor.LowerLeft);
                UIBuilder.Box(n.rectTransform, 28f, 100f, 900f, 40f, new Vector2(0f, 0f));
            }
            if (!CanBuy)
            {
                RectTransform soon = EmberforgeUI.Chip("Soon", pane, "COMING SOON  -  " + o.PriceLabel, Emberforge.TextQuiet, UITheme.FontSmall);
                UIBuilder.Box(soon, 28f, 24f, 560f, 60f, new Vector2(0f, 0f));
                return;
            }
            string label = pending ? "WAITING FOR THE STORE" : o.NeedsChoice ? "CHOOSE A HERO  -  " + o.PriceLabel : "BUY  -  " + o.PriceLabel;
            UIButton buy = EmberforgeUI.Button("Buy", pane, label, UITheme.FontSmall, ForgeButtonKind.Primary);
            UIBuilder.Box(buy.Root, 28f, 24f, 560f, 64f, new Vector2(0f, 0f));
            buy.Interactable = o.Available && !pending;
            StoreOfferView captured = o;
            buy.OnClick(delegate
            {
                if (captured.NeedsChoice) OpenOfferPicker(captured);
                else Buy(captured.Id, "");
            });
        }

        private static string Span(long seconds)
        {
            if (seconds >= 86400L) return (seconds / 86400L) + (seconds / 86400L == 1 ? " day" : " days");
            if (seconds >= 3600L) return (seconds / 3600L) + (seconds / 3600L == 1 ? " hour" : " hours");
            return Math.Max(1L, seconds / 60L) + " minutes";
        }

        private void ClaimLedger()
        {
            StoreFulfilment r = StoreOffers.ClaimLedger(App.Session, TimeUtil.NowUnix());
            AfterClaim(r.Success, r.Success ? "+" + HudTopBar.Format(r.Grant.Gold) + " Gold (Sealed)." : StoreOffers.ReasonText(r.Reason));
        }

        private void ClaimGrowth(int index)
        {
            StoreFulfilment r = StoreOffers.ClaimGrowth(App.Session, index, TimeUtil.NowUnix());
            AfterClaim(r.Success, r.Success ? "+" + HudTopBar.Format(r.Grant.Gold) + " Gold (Sealed), +" + HudTopBar.Format(r.Grant.Dust) + " Relic Dust." : StoreOffers.ReasonText(r.Reason));
        }

        private void OpenOfferPicker(StoreOfferView o)
        {
            App.Audio.Tap();
            _pickHeroes.Clear();
            for (int i = 0; i < o.Choices.Count; i++)
            {
                HeroDef def = App.Session.Content.GetHero(o.Choices[i].HeroId);
                if (def != null) _pickHeroes.Add(def);
            }
            _pickTitle = o.Name.ToUpperInvariant() + "  -  CHOOSE YOUR HERO";
            // what the pact also gives, in its own words from the card: every line that is not the hero list
            List<string> also = new List<string>();
            for (int i = 0; i < o.Lines.Count; i++)
                if (!o.Lines[i].StartsWith("One copy of a ") && !o.Lines[i].StartsWith("  ")) also.Add(o.Lines[i]);
            _pickNote = also.Count > 0 ? "ALSO IN THE PACT: " + string.Join("  -  ", also.ToArray()) + "   -   " + o.PriceLabel : o.PriceLabel;
            _pickConfirmLabel = "BUY, TAKE ";
            _pickPage = 0;
            _chosen = null;
            _picking = true;
            string offerId = o.Id;
            _pickConfirm = delegate (string heroId) { Buy(offerId, heroId); };
            Rebuild();
        }

        private void Buy(string offerId, string heroId)
        {
            if (_service == null || _service.Flow == null) { App.Notify("Coming soon."); return; }
            PurchaseResult r = _service.Flow.Buy(offerId, heroId);
            _picking = false;
            _chosen = null;
            if (!r.Success)
            {
                App.Audio.Locked();
                App.Notify(r.Message.Length > 0 ? r.Message : "That cannot be bought right now.");
            }
            else
            {
                App.Audio.Tap();
                App.Notify("Waiting for the store.");
            }
            Rebuild();
        }

        private void OnFinished(PurchaseResult r)
        {
            if (r.Success)
            {
                App.Audio.Reward();
                string lines = r.Grant != null && r.Grant.Lines.Count > 0 ? string.Join(", ", r.Grant.Lines.ToArray()) : "Done.";
                App.Notify("Bought. " + lines);
                App.RefreshHud();
            }
            else if (r.Message.Length > 0) App.Notify(r.Message);
            if (Root != null && Root.gameObject.activeInHierarchy) Rebuild();
        }

        private void OnFailed(string offerId, string message)
        {
            App.Notify(message);
            if (Root != null && Root.gameObject.activeInHierarchy) Rebuild();
        }

        // ------------------------------------------------------------------ the shops tab

        private struct ShopDoor
        {
            public string Name;
            public string Pays;
            public string Sells;
            public ScreenId Screen;
            public Sprite Icon;
            public Color Tint;
            public bool Built;
            public string Purse;
        }

        private void BuildShops()
        {
            PlayerProfile p = App.Session.Profile;
            List<ShopDoor> doors = new List<ShopDoor>();
            doors.Add(new ShopDoor { Name = "PROVING ARMORY", Pays = "Valor", Sells = "Honed armor presets, Brand and Echo refills and one four-star copy a month. Every item is fixed.",
                                     Screen = ScreenId.Armory, Icon = TextureLab.Plate(128), Tint = UITheme.Ember, Built = Router.Armory != null, Purse = HudTopBar.Format(p.Valor) + " Valor" });
            doors.Add(new ShopDoor { Name = "RECITAL ARCHIVE", Pays = "Coda", Sells = "One four-star hero copy a month, chosen from a fixed list.",
                                     Screen = ScreenId.Archive, Icon = TextureLab.SpiralIcon(128), Tint = UITheme.Mythic, Built = Router.Archive != null, Purse = HudTopBar.Format(p.Coda) + " Coda" });
            doors.Add(new ShopDoor { Name = "QUARTERMASTER", Pays = "Gold", Sells = "A daily shelf of rolled armor, one piece for each slot, restocked every reward day.",
                                     Screen = ScreenId.Quartermaster, Icon = TextureLab.Chest(128), Tint = UITheme.Gold, Built = Router.Quartermaster != null, Purse = HudTopBar.Format(p.Gold) + " Gold" });
            doors.Add(new ShopDoor { Name = "RELIC SHOP", Pays = "Relic Dust", Sells = "Relics at their fixed price, and the upgrade of each relic you own.",
                                     Screen = ScreenId.RelicShop, Icon = TextureLab.Diamond(128), Tint = UITheme.Dust, Built = Router.RelicShop != null, Purse = HudTopBar.Format(p.RelicDust) + " Dust" });

            const float cardWidth = 420f, gap = 26f, cardHeight = 600f;
            float startX = -(cardWidth * doors.Count + gap * (doors.Count - 1)) * 0.5f + cardWidth * 0.5f;
            for (int i = 0; i < doors.Count; i++)
            {
                ShopDoor d = doors[i];
                RectTransform card = UIBuilder.Node("Shop" + i, _body);
                UIBuilder.Box(card, startX + i * (cardWidth + gap), -20f, cardWidth, cardHeight, new Vector2(0.5f, 1f));
                EmberforgeUI.PanelOn(card, true);

                Image mark = UIBuilder.Img("Icon", card, d.Icon, UITheme.WithAlpha(d.Tint, 0.95f));
                UIBuilder.Box(mark.rectTransform, 0f, -36f, 120f, 120f, new Vector2(0.5f, 1f));
                Text name = EmberforgeUI.Title("Name", card, d.Name, UITheme.FontBody, Emberforge.TextGold, TextAnchor.UpperCenter);
                UIBuilder.Box(name.rectTransform, 0f, -176f, cardWidth - 24f, 40f, new Vector2(0.5f, 1f));
                UIBuilder.Outlined(name, Emberforge.Outline, Emberforge.StrokeThin);
                Text pays = EmberforgeUI.Label("Pays", card, "SPENDS " + d.Pays.ToUpperInvariant(), UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.UpperCenter);
                UIBuilder.Box(pays.rectTransform, 0f, -222f, cardWidth - 24f, 28f, new Vector2(0.5f, 1f));
                Text sells = EmberforgeUI.Body("Sells", card, d.Sells, UITheme.FontSmall - 2, Emberforge.TextBody, TextAnchor.UpperCenter);
                UIBuilder.Box(sells.rectTransform, 0f, -262f, cardWidth - 48f, 150f, new Vector2(0.5f, 1f));
                Text purse = EmberforgeUI.Label("Purse", card, "YOU HAVE " + d.Purse.ToUpperInvariant(), UITheme.FontTiny, UITheme.Gold, TextAnchor.UpperCenter);
                UIBuilder.Box(purse.rectTransform, 0f, -430f, cardWidth - 24f, 28f, new Vector2(0.5f, 1f));

                string lockLine = d.Built ? FeatureGates.LockLine(App, FeatureGates.ScreenEntry(d.Screen)) : "NOT IN THIS BUILD";
                if (lockLine.Length > 0)
                {
                    RectTransform chip = EmberforgeUI.Chip("Lock", card, lockLine.ToUpperInvariant(), Emberforge.TextQuiet, UITheme.FontTiny - 2);
                    UIBuilder.Box(chip, 0f, 30f, cardWidth - 48f, 52f, new Vector2(0.5f, 0f));
                    continue;
                }
                UIButton open = EmberforgeUI.Button("Open", card, "OPEN", UITheme.FontSmall, ForgeButtonKind.Primary);
                UIBuilder.Box(open.Root, 0f, 30f, cardWidth - 48f, 64f, new Vector2(0.5f, 0f));
                ScreenId screen = d.Screen;
                open.OnClick(delegate { App.Audio.Tap(); Router.Show(screen); });
            }
        }

        // ------------------------------------------------------------------ the hero picker

        private void BuildPicker()
        {
            int pages = Mathf.Max(1, (_pickHeroes.Count + PickPerPage - 1) / PickPerPage);
            _pickPage = Mathf.Clamp(_pickPage, 0, pages - 1);

            Text prompt = EmberforgeUI.Label("Prompt", _body, _pickTitle + (pages > 1 ? "   -   PAGE " + (_pickPage + 1) + " OF " + pages : ""),
                                             UITheme.FontHeading - 4, Emberforge.TextGold, TextAnchor.UpperCenter);
            UIBuilder.Box(prompt.rectTransform, 0f, 0f, ContentWidth, 48f, new Vector2(0.5f, 1f));

            float gridWidth = PickPerRow * PickCardWidth + (PickPerRow - 1) * 12f;
            RectTransform grid = UIBuilder.Node("Grid", _body);
            if (_pickNote.Length > 0)
            {
                Text note = EmberforgeUI.Label("Note", _body, _pickNote, UITheme.FontTiny, Emberforge.TextBody, TextAnchor.UpperCenter);
                UIBuilder.Box(note.rectTransform, 0f, -50f, ContentWidth, 30f, new Vector2(0.5f, 1f));
            }
            UIBuilder.Box(grid, 0f, -92f, gridWidth, PickCardHeight * 2f + 12f, new Vector2(0.5f, 1f));
            UIBuilder.VStack(grid, 12f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
            RectTransform rowA = null;
            int first = _pickPage * PickPerPage;
            for (int i = first; i < first + PickPerPage && i < _pickHeroes.Count; i++)
            {
                if ((i - first) % PickPerRow == 0)
                {
                    rowA = UIBuilder.Node("Row" + ((i - first) / PickPerRow), grid);
                    UIBuilder.Sized(rowA, gridWidth, PickCardHeight);
                    UIBuilder.HStack(rowA, 12f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
                }
                BuildPickCard(rowA, _pickHeroes[i]);
            }

            float y = 92f + PickCardHeight * 2f + 12f + 20f;
            UIButton back = EmberforgeUI.Button("Back", _body, "BACK", UITheme.FontSmall, ForgeButtonKind.Secondary);
            UIBuilder.Box(back.Root, -(gridWidth * 0.5f) + 100f, -y, 200f, 56f, new Vector2(0.5f, 1f));
            back.OnClick(delegate { TryStepBack(); });

            UIButton prev = EmberforgeUI.Button("PrevPick", _body, "<", UITheme.FontSmall, ForgeButtonKind.Secondary);
            UIBuilder.Box(prev.Root, -(gridWidth * 0.5f) + 270f, -y, 80f, 56f, new Vector2(0.5f, 1f));
            prev.Interactable = _pickPage > 0;
            prev.OnClick(delegate { _pickPage--; App.Audio.Tap(); Rebuild(); });
            UIButton next = EmberforgeUI.Button("NextPick", _body, ">", UITheme.FontSmall, ForgeButtonKind.Secondary);
            UIBuilder.Box(next.Root, -(gridWidth * 0.5f) + 360f, -y, 80f, 56f, new Vector2(0.5f, 1f));
            next.Interactable = _pickPage < pages - 1;
            next.OnClick(delegate { _pickPage++; App.Audio.Tap(); Rebuild(); });

            string chosenName = null;
            if (_chosen != null)
            {
                HeroDef def = App.Session.Content.GetHero(_chosen);
                if (def != null) chosenName = def.Name.ToUpperInvariant();
            }
            UIButton confirm = EmberforgeUI.Button("Confirm", _body, chosenName == null ? "CHOOSE A HERO" : _pickConfirmLabel + chosenName,
                                                   UITheme.FontSmall, ForgeButtonKind.Primary);
            UIBuilder.Box(confirm.Root, 190f, -y, gridWidth - 560f, 56f, new Vector2(0.5f, 1f));
            confirm.Interactable = chosenName != null;
            string pick = _chosen;
            Action<string> action = _pickConfirm;
            confirm.OnClick(delegate { if (action != null) action(pick); });
        }

        private void BuildPickCard(RectTransform parent, HeroDef def)
        {
            string id = def.Id;
            bool chosen = _chosen == id;
            HeroInstance owned = App.Session.Profile.FindHeroByDefId(id);
            ForgeCard forged = EmberforgeUI.SelectableCard("Pick_" + id, parent, delegate { App.Audio.Tap(); _chosen = id; Rebuild(); });
            EmberforgeUI.SetCardSelected(forged, chosen);
            UIBuilder.Sized(forged.Root, PickCardWidth, PickCardHeight);
            RectTransform card = forged.Body;

            RectTransform portrait = EmberforgeUI.PortraitFrame("Portrait", card, def, def.Stars, 96f);
            UIBuilder.Box(portrait, 0f, -UITheme.Space12, 96f, 96f, new Vector2(0.5f, 1f));
            Text label = EmberforgeUI.Label("Name", card, def.Name.ToUpperInvariant(), UITheme.FontTiny - 6, Emberforge.TextBody, TextAnchor.UpperCenter);
            UIBuilder.Box(label.rectTransform, 0f, -(UITheme.Space12 + 100f), PickCardWidth - 12f, 24f, new Vector2(0.5f, 1f));
            Text role = EmberforgeUI.Label("Role", card, def.Role.ToString().ToUpperInvariant() + "  " + def.Aspect.ToString().ToUpperInvariant(), UITheme.FontTiny - 8, Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Box(role.rectTransform, 0f, -(UITheme.Space12 + 126f), PickCardWidth - 8f, 22f, new Vector2(0.5f, 1f));
            Text own = EmberforgeUI.Label("Own", card, owned == null ? "NEW" : "OWNED  x" + owned.Copies, UITheme.FontTiny - 4,
                                          owned == null ? Emberforge.TextGold : Emberforge.TextQuiet, TextAnchor.MiddleCenter);
            UIBuilder.Box(own.rectTransform, 0f, 4f, PickCardWidth - 12f, 24f, new Vector2(0.5f, 0f));
        }

        // ------------------------------------------------------------------ buy buttons shared by the tabs

        private void BuyButton(RectTransform parent, StoreOfferView o, string label, float x, float y, float w, float h, int font)
        {
            bool pending = _service != null && _service.Flow != null && _service.Flow.IsPending(o.Id);
            string text = !CanBuy ? "COMING SOON  -  " + o.PriceLabel : pending ? "WAITING FOR THE STORE" : label + "  -  " + o.PriceLabel;
            UIButton b = EmberforgeUI.Button("Buy" + o.Id, parent, text, font, ForgeButtonKind.Primary);
            UIBuilder.Box(b.Root, x, y, w, h, new Vector2(1f, 1f));
            b.Interactable = CanBuy && o.Available && !pending;
            string id = o.Id;
            b.OnClick(delegate { Buy(id, ""); });
        }
    }
}
