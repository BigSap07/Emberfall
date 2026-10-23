using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;
using Emberfall.Game;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// THE EVENTS PAGE (the events section of Docs/DESIGN-STORE.md): the weekly and the monthly event, one tab
    /// each. A summary strip (the event, its points, the paid Event Track and the event pack with their full
    /// contents and prices), the track (a free row and a paid row, every reward shown) and the quest list.
    /// Built only when LiveEventsSwitch is on; the Hub's Events door opens it instead of the limited-time
    /// events page, which stays one tap away.
    ///
    /// THIS SCREEN DECIDES NOTHING AND GRANTS NOTHING. It draws LiveEvents.View, claims through
    /// LiveEvents.ClaimQuest, ClaimFree and ClaimPaid, and buys only through the purchase flow
    /// (PurchaseService), exactly as the Store's Packs tab does. Every figure and every sentence is a field of
    /// what View returned. In a release build the flow has no backend, so every price is shown and no buy
    /// button is live.
    /// </summary>
    public sealed class LiveEventsScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.LiveEvents; } }

        public override bool HidesTopBar { get { return false; } }

        private const float ContentWidth = 1760f;
        private const float TabWidth = 300f;
        private const float BodyHeight = 700f;
        private const float SummaryHeight = 156f;
        private const float LabelColumn = 96f;
        private const float TileGap = 8f;
        private const float TileHeight = 108f;
        private const float TrackTop = SummaryHeight + 8f;
        private const float PaidRowTop = TrackTop + TileHeight + 6f;
        private const float QuestTop = PaidRowTop + TileHeight + 10f;
        private const float QuestRowHeight = 50f;
        private const float QuestRowGap = 4f;

        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);

        private Text _headerSubtitle;
        private Text _banner;
        private RectTransform _testChip;
        private RectTransform _body;
        private readonly ForgeTab[] _tabs = new ForgeTab[2];
        private LiveEventSlot _slot = LiveEventSlot.Weekly;
        private PurchaseService _service;

        protected override void Build()
        {
            EmberforgeUI.Ground(Root);
            ScreenChrome.ForgedHeader(Root, "EVENTS", Router, out _headerSubtitle);

            UIButton door = EmberforgeUI.Button("LimitedEvents", Root, "LIMITED-TIME EVENTS", UITheme.FontTiny, ForgeButtonKind.Secondary);
            UIBuilder.Box(door.Root, -UITheme.ScreenPadding, -(ScreenChrome.HeaderTop + 24f), 340f, 56f, new Vector2(1f, 1f));
            door.OnClick(delegate { App.Audio.Tap(); Router.Show(ScreenId.Events); });

            float top = ScreenChrome.HeaderTop + 108f + UITheme.Space8;
            RectTransform tabRow = UIBuilder.Node("Tabs", Root);
            UIBuilder.Box(tabRow, 0f, -top, TabWidth * 2f + HubStyle.TabGap, HubStyle.TabHeight, new Vector2(0.5f, 1f));
            UIBuilder.HStack(tabRow, HubStyle.TabGap, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
            string[] names = { "WEEKLY", "MONTHLY" };
            for (int i = 0; i < 2; i++)
            {
                LiveEventSlot which = i == 0 ? LiveEventSlot.Weekly : LiveEventSlot.Monthly;
                _tabs[i] = EmberforgeUI.Tab(names[i], tabRow, TabWidth, HubStyle.TabHeight, delegate { SetSlot(which); });
            }

            _banner = EmberforgeUI.Label("Banner", Root, "", UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.MiddleRight);
            UIBuilder.Box(_banner.rectTransform, -UITheme.ScreenPadding, -top, 560f, HubStyle.TabHeight, new Vector2(1f, 1f));
            _testChip = TestStoreBanner.Create(Root, top);

            _body = UIBuilder.Node("Body", Root);
            UIBuilder.Box(_body, 0f, -(top + HubStyle.TabHeight + 12f), ContentWidth, BodyHeight, new Vector2(0.5f, 1f));
        }

        // ------------------------------------------------------------------ lifecycle

        public override void OnShow()
        {
            Bind();
            App.RefreshHud();
            Rebuild();
        }

        public override void OnHide()
        {
            Unbind();
        }

        /// <summary>CAPTURE ONLY: shows a slot, as a tap on its tab does.</summary>
        public void ShowSlotForCapture(LiveEventSlot slot)
        {
            _slot = slot;
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

        private void SetSlot(LiveEventSlot slot)
        {
            if (_slot == slot) return;
            App.Audio.Tap();
            _slot = slot;
            Rebuild();
        }

        private bool CanBuy
        {
            get { return _service != null && _service.Flow != null && _service.Flow.CanBuy; }
        }

        private StoreMode Mode
        {
            get { return _service != null ? _service.Mode : StoreMode.ComingSoon; }
        }

        private static void Clear(RectTransform body)
        {
            for (int i = body.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(body.GetChild(i).gameObject);
        }

        // ------------------------------------------------------------------ the page

        private void Rebuild()
        {
            long now = TimeUtil.NowUnix();
            Clear(_body);
            EmberforgeUI.SetTabSelected(_tabs[0], _slot == LiveEventSlot.Weekly);
            EmberforgeUI.SetTabSelected(_tabs[1], _slot == LiveEventSlot.Monthly);

            switch (Mode)
            {
                case StoreMode.Test: _banner.text = TestStoreBanner.Shown(StoreMode.Test) ? "" : "TEST STORE  -  NO REAL MONEY MOVES"; _banner.color = Emberforge.TextGold; break;
                case StoreMode.Live: _banner.text = ""; break;
                default: _banner.text = "COMING SOON  -  PURCHASES OPEN WITH THE STORE"; _banner.color = Emberforge.TextQuiet; break;
            }
            _testChip.gameObject.SetActive(TestStoreBanner.Shown(Mode));

            LiveEventView v = LiveEvents.View(App.Session, _slot, now);
            if (!v.Active)
            {
                _headerSubtitle.text = "NO EVENT IS RUNNING";
                Text none = EmberforgeUI.Label("None", _body, "No " + LiveEventsConfig.SlotName(_slot) + " event is running right now.",
                                               UITheme.FontBody, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
                UIBuilder.FillParent(none.rectTransform, 0f);
                return;
            }

            _headerSubtitle.text = v.Name.ToUpperInvariant() + "  -  " + v.DaysLeft + (v.DaysLeft == 1 ? " DAY" : " DAYS") + " LEFT";
            BuildSummary(v);
            BuildTrack(v);
            BuildQuests(v);
        }

        // ------------------------------------------------------------------ the summary

        private void BuildSummary(LiveEventView v)
        {
            RectTransform summary = UIBuilder.Node("Summary", _body);
            UIBuilder.Box(summary, 0f, 0f, ContentWidth, SummaryHeight, new Vector2(0.5f, 1f));
            EmberforgeUI.PanelOn(summary, true);

            Text title = EmberforgeUI.Title("Title", summary, v.Name.ToUpperInvariant(), UITheme.FontBody, Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(title.rectTransform, 48f, -18f, 540f, 40f, TopLeft);
            UIBuilder.Outlined(title, Emberforge.Outline, Emberforge.StrokeThin);
            RectTransform chip = EmberforgeUI.Chip("Aspect", summary, v.Aspect.ToString().ToUpperInvariant(), UITheme.AspectColor(v.Aspect), UITheme.FontTiny - 4);
            UIBuilder.Box(chip, 610f, -20f, 120f, 32f, TopLeft);

            Text blurb = EmberforgeUI.Label("Blurb", summary, v.Blurb, UITheme.FontTiny - 4, Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(blurb.rectTransform, 48f, -58f, 680f, 26f, TopLeft);

            Text points = EmberforgeUI.Label("Points", summary, v.Points + " / " + v.MaxPoints + " " + v.PointsLabel.ToUpperInvariant(),
                                             UITheme.FontSmall, Emberforge.TextBody, TextAnchor.UpperLeft);
            UIBuilder.Box(points.rectTransform, 48f, -88f, 680f, 30f, TopLeft);
            Image bar = EmberforgeUI.ProgressBar("Bar", summary);
            UIBuilder.Box(bar.rectTransform.parent as RectTransform, 48f, -122f, 670f, 22f, TopLeft);
            EmberforgeUI.SetProgress(bar, v.MaxPoints > 0 ? Mathf.Clamp01(v.Points / (float)v.MaxPoints) : 0f);

            // ---- the paid Event Track: what it gives in all, its price, one button
            Text trackTitle = EmberforgeUI.Title("TrackTitle", summary, "EVENT TRACK" + (v.TrackOfferId.Length > 0 ? "  -  " + v.TrackPrice : ""),
                                                 UITheme.FontTiny, Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(trackTitle.rectTransform, 750f, -12f, 480f, 28f, TopLeft);
            Text trackBody = EmberforgeUI.Body("TrackBody", summary, string.Join("\n", v.TrackLines.ToArray()), UITheme.FontTiny - 6,
                                               Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(trackBody.rectTransform, 750f, -40f, 480f, 78f, TopLeft);
            BuyOrOwned(summary, v.TrackOfferId, v.TrackPrice, "BUY EVENT TRACK", v.TrackOwned, v.TrackCanBuy, "TRACK ACTIVE", v.TrackLock.Length > 0 ? v.TrackLock : v.SalesClosed ? "SALES CLOSED  -  THE EVENT ENDS SOON" : "", 750f, -116f, 320f);

            // ---- the event pack: its name, its price, its fixed contents in full, one button
            Text packTitle = EmberforgeUI.Title("PackTitle", summary, v.PackOfferId.Length > 0 ? v.PackName.ToUpperInvariant() + "  -  " + v.PackPrice : "EVENT PACK",
                                                UITheme.FontTiny, Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(packTitle.rectTransform, 1260f, -12f, 480f, 28f, TopLeft);
            Text packBody = EmberforgeUI.Body("PackBody", summary, v.PackOfferId.Length > 0 ? string.Join("\n", v.PackLines.ToArray()) : "This event has no pack.",
                                              UITheme.FontTiny - 6, Emberforge.TextQuiet, TextAnchor.UpperLeft);
            UIBuilder.Box(packBody.rectTransform, 1260f, -40f, 480f, 78f, TopLeft);
            if (v.PackOfferId.Length > 0)
                BuyOrOwned(summary, v.PackOfferId, v.PackPrice, "BUY EVENT PACK", v.PackOwned, v.PackCanBuy, "PACK BOUGHT", v.PackLock.Length > 0 ? v.PackLock : v.SalesClosed ? "SALES CLOSED  -  THE EVENT ENDS SOON" : "", 1260f, -116f, 320f);
        }

        private void BuyOrOwned(RectTransform parent, string offerId, string price, string label, bool owned, bool canBuy, string ownedLabel, string stop, float x, float y, float w)
        {
            if (offerId.Length == 0)
            {
                Text none = EmberforgeUI.Label("NotForSale", parent, "NOT FOR SALE", UITheme.FontTiny - 2, Emberforge.TextQuiet, TextAnchor.MiddleLeft);
                UIBuilder.Box(none.rectTransform, x, y, w, 36f, TopLeft);
                return;
            }
            if (owned)
            {
                RectTransform chip = EmberforgeUI.Chip("Owned_" + offerId, parent, ownedLabel, UITheme.Success, UITheme.FontTiny - 4);
                UIBuilder.Box(chip, x, y, w, 36f, TopLeft);
                return;
            }
            bool pending = _service != null && _service.Flow != null && _service.Flow.IsPending(offerId);
            string text = stop.Length > 0 ? stop : !CanBuy ? "COMING SOON  -  " + price : pending ? "WAITING FOR THE STORE" : label + "  -  " + price;
            UIButton b = EmberforgeUI.Button("Buy_" + offerId, parent, text, UITheme.FontTiny - 4, ForgeButtonKind.Primary);
            UIBuilder.Box(b.Root, x, y, w, 36f, TopLeft);
            b.Interactable = CanBuy && canBuy && !pending;
            string id = offerId;
            b.OnClick(delegate { Buy(id); });
        }

        // ------------------------------------------------------------------ the track

        private void BuildTrack(LiveEventView v)
        {
            int n = v.Tiers.Count;
            float tileWidth = (ContentWidth - LabelColumn - 8f - (n - 1) * TileGap) / Mathf.Max(1, n);

            Text freeLabel = EmberforgeUI.Title("FreeLabel", _body, "FREE", UITheme.FontSmall, Emberforge.TextGold, TextAnchor.MiddleCenter);
            UIBuilder.Box(freeLabel.rectTransform, 0f, -TrackTop, LabelColumn, TileHeight, TopLeft);
            Text paidLabel = EmberforgeUI.Title("PaidLabel", _body, "PAID", UITheme.FontSmall, v.TrackOwned ? Emberforge.TextGold : Emberforge.TextQuiet, TextAnchor.MiddleCenter);
            UIBuilder.Box(paidLabel.rectTransform, 0f, -PaidRowTop, LabelColumn, TileHeight, TopLeft);

            for (int i = 0; i < n; i++)
            {
                float x = LabelColumn + 8f + i * (tileWidth + TileGap);
                BuildTile(v, v.Tiers[i], false, x, -TrackTop, tileWidth);
                BuildTile(v, v.Tiers[i], true, x, -PaidRowTop, tileWidth);
            }
        }

        private void BuildTile(LiveEventView v, LiveTierView t, bool paid, float x, float y, float width)
        {
            bool has = paid ? t.HasPaid : t.HasFree;
            bool claimed = paid ? t.PaidClaimed : t.FreeClaimed;
            bool canClaim = paid ? t.PaidCanClaim : t.FreeCanClaim;
            string text = paid ? t.PaidShort : t.FreeShort;

            RectTransform tile = UIBuilder.Node((paid ? "Paid_" : "Free_") + t.Index, _body);
            UIBuilder.Box(tile, x, y, width, TileHeight, TopLeft);
            // Depth carries state, as on the limited-time events page: only a tile that can be claimed now stands up in a frame;
            // reached-and-taken, locked and empty tiles are all plain.
            if (canClaim) EmberforgeUI.PanelOn(tile, false);
            else EmberforgeUI.PlainPanelOn(tile);

            Text pts = EmberforgeUI.Label("Points", tile, t.Points + " PTS", UITheme.FontTiny - 6, t.Reached ? Emberforge.TextGold : Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Box(pts.rectTransform, 0f, -Emberforge.SmallPadY(6f), width - 8f, 22f, new Vector2(0.5f, 1f));
            if (!has) return;

            Text reward = EmberforgeUI.Body("Reward", tile, text, UITheme.FontTiny - 4, claimed ? Emberforge.TextQuiet : Emberforge.TextBody, TextAnchor.UpperCenter);
            UIBuilder.Box(reward.rectTransform, 0f, -(Emberforge.SmallPadY(6f) + 24f), width - 12f, 44f, new Vector2(0.5f, 1f));

            if (claimed)
            {
                RectTransform chip = EmberforgeUI.Chip("State", tile, "CLAIMED", UITheme.Success, UITheme.FontTiny - 6);
                UIBuilder.Box(chip, 0f, Emberforge.SmallPadY(6f), width - 14f, 26f, new Vector2(0.5f, 0f));
            }
            else if (canClaim)
            {
                UIButton claim = EmberforgeUI.Button("Claim", tile, "CLAIM", UITheme.FontTiny - 4, ForgeButtonKind.Primary);
                UIBuilder.Box(claim.Root, 0f, Emberforge.SmallPadY(6f), width - 14f, 30f, new Vector2(0.5f, 0f));
                int index = t.Index;
                claim.OnClick(delegate { if (paid) OnClaimPaid(index); else OnClaimFree(index); });
            }
            else if (paid && !v.TrackOwned)
            {
                Text locked = EmberforgeUI.Label("Locked", tile, "TRACK LOCKED", UITheme.FontTiny - 8, Emberforge.TextQuiet, TextAnchor.LowerCenter);
                UIBuilder.Box(locked.rectTransform, 0f, Emberforge.SmallPadY(6f), width - 8f, 22f, new Vector2(0.5f, 0f));
            }
        }

        // ------------------------------------------------------------------ the quests

        private void BuildQuests(LiveEventView v)
        {
            Text heading = EmberforgeUI.Title("QuestHeading", _body, "QUESTS  -  EACH CLAIM PAYS EVENT POINTS", UITheme.FontTiny, Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(heading.rectTransform, 8f, -QuestTop, 900f, 26f, TopLeft);

            float colWidth = (ContentWidth - 16f) * 0.5f;
            int perColumn = (v.Quests.Count + 1) / 2;
            for (int i = 0; i < v.Quests.Count; i++)
            {
                int column = i / perColumn, row = i % perColumn;
                float x = column * (colWidth + 16f);
                float y = -(QuestTop + 28f + row * (QuestRowHeight + QuestRowGap));
                BuildQuestRow(v.Quests[i], x, y, colWidth);
            }
        }

        private void BuildQuestRow(LiveQuestView q, float x, float y, float width)
        {
            RectTransform row = UIBuilder.Node("Quest_" + q.Id, _body);
            UIBuilder.Box(row, x, y, width, QuestRowHeight, TopLeft);
            if (q.CanClaim) EmberforgeUI.PanelOn(row, false);
            else EmberforgeUI.PlainPanelOn(row);

            Text line = EmberforgeUI.Label("Line", row, q.Line, UITheme.FontTiny - 4, q.Claimed ? Emberforge.TextQuiet : Emberforge.TextBody, TextAnchor.MiddleLeft);
            UIBuilder.Box(line.rectTransform, Emberforge.SmallPadX(16f), 0f, width - 400f, QuestRowHeight, new Vector2(0f, 0.5f));
            Text progress = EmberforgeUI.Label("Progress", row, q.Progress.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " / " + q.Target.ToString("N0", System.Globalization.CultureInfo.InvariantCulture), UITheme.FontTiny - 4,
                                               q.Done ? Emberforge.TextGold : Emberforge.TextQuiet, TextAnchor.MiddleRight);
            UIBuilder.Box(progress.rectTransform, -248f, 0f, 130f, QuestRowHeight, new Vector2(1f, 0.5f));
            Text pts = EmberforgeUI.Label("Pts", row, "+" + q.Points + " PTS", UITheme.FontTiny - 4, Emberforge.TextQuiet, TextAnchor.MiddleRight);
            UIBuilder.Box(pts.rectTransform, -140f, 0f, 100f, QuestRowHeight, new Vector2(1f, 0.5f));

            if (q.Claimed)
            {
                RectTransform chip = EmberforgeUI.Chip("State", row, "CLAIMED", UITheme.Success, UITheme.FontTiny - 6);
                UIBuilder.Box(chip, -Emberforge.SmallPadX(12f), 0f, 120f, 32f, new Vector2(1f, 0.5f));
            }
            else if (q.CanClaim)
            {
                UIButton claim = EmberforgeUI.Button("Claim", row, "CLAIM", UITheme.FontTiny - 4, ForgeButtonKind.Primary);
                UIBuilder.Box(claim.Root, -Emberforge.SmallPadX(12f), 0f, 120f, 38f, new Vector2(1f, 0.5f));
                string id = q.Id;
                claim.OnClick(delegate { OnClaimQuest(id); });
            }
        }

        // ------------------------------------------------------------------ actions

        private void Done(LiveResult r, string refused)
        {
            if (!r.Success) { App.Audio.Locked(); App.Notify(refused); Rebuild(); return; }
            App.Audio.Reward();
            App.RefreshHud();
            App.RequestSave();
            Rebuild();
        }

        private void OnClaimQuest(string id)
        {
            LiveResult r = LiveEvents.ClaimQuest(App.Session, _slot, id, TimeUtil.NowUnix());
            if (r.Success) App.Notify("+" + r.Points + " event points.");
            Done(r, "That quest is not ready.");
        }

        private void OnClaimFree(int tier)
        {
            LiveResult r = LiveEvents.ClaimFree(App.Session, _slot, tier, TimeUtil.NowUnix());
            if (r.Success) App.Notify("Claimed.");
            Done(r, "That reward is not ready.");
        }

        private void OnClaimPaid(int tier)
        {
            LiveResult r = LiveEvents.ClaimPaid(App.Session, _slot, tier, TimeUtil.NowUnix());
            if (r.Success) App.Notify("Claimed.");
            Done(r, "That reward is not ready.");
        }

        private void Buy(string offerId)
        {
            if (_service == null || _service.Flow == null) { App.Notify("Coming soon."); return; }
            PurchaseResult r = _service.Flow.Buy(offerId, "");
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
                StoreOfferDef def = App.Session.Content.Store.Offer(r.OfferId);
                App.Notify("Bought: " + (def != null ? def.Name : "your purchase") + ".");
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
    }
}
