using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// Lists every event in content, active or not, each showing its own points ladder (and, if it
    /// has one, its own currency ladder) against the player's PlayerProfile.EventProgress. Nothing
    /// here is special-cased per event - The Recollection Rite and The Ledger's Week render off the
    /// exact same rows this screen would draw for a third event added to events.json tomorrow.
    ///
    /// Every event in content is listed, not only active ones: an upcoming event previews what is
    /// coming, and an ended one stays visible for as long as it has an unclaimed milestone or a
    /// spendable currency balance, since GameSession does not gate claiming or redeeming on the
    /// window - only earning is gated to it.
    /// </summary>
    public sealed class EventsScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Events; } }

        private Text _headerSubtitle;
        private RectTransform _listContent;

        private const float RowHeight = 56f;
        private const float HeaderHeight = 128f;
        private const float SectionLabelHeight = 32f;
        private const float CardPadding = 24f;
        private const float CardGap = 16f;

        protected override void Build()
        {
            ScreenChrome.Header(Root, "EVENTS", Router, out _headerSubtitle);

            ScrollRect scroll;
            _listContent = UIBuilder.ScrollList("EventList", Root, out scroll, 20f, new RectOffset(32, 32, 12, 40));
            UIBuilder.FillParent(scroll.GetComponent<RectTransform>(), 0f, ScreenChrome.HeaderTop + 108f, 0f, 0f);
        }

        // ------------------------------------------------------------------ refresh

        public override void OnShow()
        {
            App.RefreshHud();
            Rebuild();
        }

        private void Rebuild()
        {
            long now = TimeUtil.NowUnix();
            List<EventDef> events = App.Session.Content.Events;

            int activeCount = 0;
            for (int i = 0; i < events.Count; i++) if (events[i].IsActive(now)) activeCount++;
            _headerSubtitle.text = activeCount + " ACTIVE  -  " + events.Count + " TOTAL";

            for (int i = _listContent.childCount - 1; i >= 0; i--)
                Object.Destroy(_listContent.GetChild(i).gameObject);

            for (int i = 0; i < events.Count; i++) BuildEventCard(events[i], now);

            if (events.Count == 0)
            {
                Text empty = UIBuilder.Label("Empty", _listContent, "No events in the content database.",
                                             UITheme.FontSmall, UITheme.TextDim, TextAnchor.MiddleCenter);
                UIBuilder.Sized(empty.rectTransform, 0f, 80f);
            }
        }

        private float CardHeight(EventDef evt)
        {
            float h = CardPadding + HeaderHeight + CardGap + SectionLabelHeight + 8f
                    + evt.Milestones.Count * RowHeight;
            if (evt.HasCurrency)
                h += CardGap + SectionLabelHeight + 8f + evt.CurrencyRewards.Count * RowHeight;
            return h + CardPadding;
        }

        private void BuildEventCard(EventDef evt, long now)
        {
            EventProgressEntry progress = App.Session.Profile.FindEventProgress(evt.Id);
            long points = progress != null ? progress.Points : 0L;
            long currency = progress != null ? progress.Currency : 0L;

            float cardHeight = CardHeight(evt);
            RectTransform card = UIBuilder.Node("Event_" + evt.Id, _listContent);
            UIBuilder.Sized(card, 0f, cardHeight);

            Image bg = card.gameObject.AddComponent<Image>();
            bg.sprite = UISkin.Panel(20);
            bg.type = Image.Type.Sliced;
            bg.color = evt.IsActive(now) ? UITheme.WithAlpha(UITheme.PanelRaised, 0.92f) : UITheme.Panel;
            UIBuilder.Frame("Edge", card, UITheme.WithAlpha(evt.IsActive(now) ? UITheme.Success : UITheme.BorderSoft, 0.6f), 20, 2);

            float top = CardPadding;

            // Name, left, and a status chip, right.
            Text nameLabel = UIBuilder.Label("Name", card, evt.Name, UITheme.FontBody, UITheme.TextPrimary, TextAnchor.UpperLeft);
            UIBuilder.Box(nameLabel.rectTransform, 24f, -top, 560f, 36f, new Vector2(0f, 1f));

            string statusText; Color statusColor;
            StatusOf(evt, now, out statusText, out statusColor);
            RectTransform statusChip = UIBuilder.Chip("Status", card, statusText, statusColor, UITheme.FontTiny);
            UIBuilder.Box(statusChip, -24f, -top, 220f, 32f, new Vector2(1f, 1f));

            top += 40f;

            // Points row: "168 / 200 MEMORY FRAGMENTS" against the next unclaimed threshold, plus a
            // bar toward it. Once every milestone is claimed the bar simply reads full.
            long nextThreshold = NextUnclaimedThreshold(evt, progress);
            long denominator = nextThreshold > 0 ? nextThreshold : evt.HighestThreshold;
            float ratio = denominator > 0 ? Mathf.Clamp01(points / (float)denominator) : 1f;

            Text pointsLabel = UIBuilder.Label("Points", card,
                points + (nextThreshold > 0 ? " / " + nextThreshold : "") + "  " + evt.PointsLabel.ToUpperInvariant(),
                UITheme.FontSmall, UITheme.TextPrimary, TextAnchor.UpperLeft);
            UIBuilder.Box(pointsLabel.rectTransform, 24f, -top, 700f, 30f, new Vector2(0f, 1f));
            top += 34f;

            Image pointsBar = UIBuilder.Bar("PointsBar", card, UITheme.PanelSunken, UITheme.Success);
            StretchTop(pointsBar.rectTransform.parent as RectTransform, top, 18f, 24f, 24f);
            UIBuilder.SetBar(pointsBar, ratio);
            top += 18f + CardGap;

            // Milestone ladder.
            Text milestonesLabel = UIBuilder.Label("MilestonesHeading", card, "MILESTONES",
                                                    UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperLeft);
            UIBuilder.Box(milestonesLabel.rectTransform, 24f, -top, 400f, SectionLabelHeight, new Vector2(0f, 1f));
            top += SectionLabelHeight + 8f;

            for (int i = 0; i < evt.Milestones.Count; i++)
                top = BuildMilestoneRow(card, evt, evt.Milestones[i], progress, top);

            // Currency ladder, only when the event has one.
            if (evt.HasCurrency)
            {
                top += CardGap;
                Text currencyLabel = UIBuilder.Label("CurrencyHeading", card,
                    evt.CurrencyName.ToUpperInvariant() + "  -  " + currency,
                    UITheme.FontTiny, UITheme.Shard, TextAnchor.UpperLeft);
                UIBuilder.Box(currencyLabel.rectTransform, 24f, -top, 500f, SectionLabelHeight, new Vector2(0f, 1f));
                top += SectionLabelHeight + 8f;

                for (int i = 0; i < evt.CurrencyRewards.Count; i++)
                    top = BuildCurrencyRow(card, evt, evt.CurrencyRewards[i], currency, top);
            }
        }

        private float BuildMilestoneRow(RectTransform card, EventDef evt, EventMilestone milestone,
                                        EventProgressEntry progress, float top)
        {
            long points = progress != null ? progress.Points : 0L;
            bool claimed = progress != null && progress.ClaimedMilestoneThresholds.Contains(milestone.Threshold);
            bool reached = points >= milestone.Threshold;

            RectTransform row = UIBuilder.Node("Milestone_" + milestone.Threshold, card);
            StretchTop(row, top, RowHeight - 8f, 24f, 24f);

            Image rowBg = row.gameObject.AddComponent<Image>();
            rowBg.sprite = UISkin.Panel(12);
            rowBg.type = Image.Type.Sliced;
            rowBg.color = UITheme.WithAlpha(UITheme.PanelSunken, 0.7f);

            Text text = UIBuilder.Label("Text", row,
                milestone.Threshold + " " + evt.PointsLabel + "  -  " + milestone.Reward.Describe(),
                UITheme.FontTiny, claimed ? UITheme.TextDim : UITheme.TextPrimary, TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.offsetMin = new Vector2(16f, 0f);
            text.rectTransform.offsetMax = new Vector2(-160f, 0f);

            if (claimed)
            {
                RectTransform chip = UIBuilder.Chip("Claimed", row, "CLAIMED", UITheme.TextMuted, UITheme.FontTiny);
                UIBuilder.Box(chip, -12f, 0f, 128f, 32f, new Vector2(1f, 0.5f));
            }
            else
            {
                UIButton claimButton = UIBuilder.GoldButton("Claim", row, "CLAIM", UITheme.FontTiny);
                UIBuilder.Box(claimButton.Root, -12f, 0f, 128f, 40f, new Vector2(1f, 0.5f));
                claimButton.Interactable = reached;
                EventDef capturedEvt = evt;
                long capturedThreshold = milestone.Threshold;
                claimButton.OnClick(delegate { OnClaimMilestone(capturedEvt, capturedThreshold); });
            }

            return top + RowHeight;
        }

        private float BuildCurrencyRow(RectTransform card, EventDef evt, EventCurrencyTier tier,
                                       long currency, float top)
        {
            bool affordable = currency >= tier.Cost;

            RectTransform row = UIBuilder.Node("Currency_" + tier.Cost, card);
            StretchTop(row, top, RowHeight - 8f, 24f, 24f);

            Image rowBg = row.gameObject.AddComponent<Image>();
            rowBg.sprite = UISkin.Panel(12);
            rowBg.type = Image.Type.Sliced;
            rowBg.color = UITheme.WithAlpha(UITheme.PanelSunken, 0.7f);

            Text text = UIBuilder.Label("Text", row,
                tier.Cost + " " + evt.CurrencyName + "  -  " + tier.Reward.Describe(),
                UITheme.FontTiny, UITheme.TextPrimary, TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.offsetMin = new Vector2(16f, 0f);
            text.rectTransform.offsetMax = new Vector2(-160f, 0f);

            UIButton redeemButton = UIBuilder.SecondaryButton("Redeem", row, "REDEEM", UITheme.FontTiny);
            UIBuilder.Box(redeemButton.Root, -12f, 0f, 128f, 40f, new Vector2(1f, 0.5f));
            redeemButton.Interactable = affordable;
            EventDef capturedEvt = evt;
            long capturedCost = tier.Cost;
            redeemButton.OnClick(delegate { OnRedeemCurrency(capturedEvt, capturedCost); });

            return top + RowHeight;
        }

        /// <summary>
        /// Positions rt full-width inside its parent (a card, whose own width is whatever the
        /// scroll list's layout gives it - never a literal this screen can read), top-anchored at
        /// `top` pixels below the parent's top edge, `height` pixels tall. UIBuilder.Box alone
        /// cannot express this: its w/h are a fixed size in pixels, not a fill-relative-to-parent
        /// stretch, which is exactly what every full-width row in an event card needs.
        /// </summary>
        private static void StretchTop(RectTransform rt, float top, float height, float leftPad, float rightPad)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMax = new Vector2(-rightPad, -top);
            rt.offsetMin = new Vector2(leftPad, -(top + height));
        }

        /// <summary>Lowest not-yet-claimed threshold, or 0 once every milestone is claimed (or the
        /// event authors none) - the sentinel BuildEventCard reads as "show the bar full".</summary>
        private static long NextUnclaimedThreshold(EventDef evt, EventProgressEntry progress)
        {
            for (int i = 0; i < evt.Milestones.Count; i++)
            {
                long threshold = evt.Milestones[i].Threshold;
                bool claimed = progress != null && progress.ClaimedMilestoneThresholds.Contains(threshold);
                if (!claimed) return threshold;
            }
            return 0;
        }

        private static void StatusOf(EventDef evt, long now, out string text, out Color color)
        {
            if (evt.IsActive(now)) { text = "ACTIVE"; color = UITheme.Success; return; }
            if (evt.HasNotStarted(now)) { text = "STARTS IN " + FormatDays(evt.StartUnix - now); color = UITheme.TextMuted; return; }
            text = "ENDED"; color = UITheme.TextDim;
        }

        private static string FormatDays(long seconds)
        {
            long days = seconds / 86400L;
            if (days > 0) return days + "D";
            long hours = seconds / 3600L;
            return System.Math.Max(1L, hours) + "H";
        }

        // ------------------------------------------------------------------ actions

        private void OnClaimMilestone(EventDef evt, long threshold)
        {
            bool success = App.Session.ClaimEventMilestone(evt.Id, threshold);
            if (!success)
            {
                App.Notify("Not ready to claim yet.");
                return;
            }
            App.Audio.Reward();
            EventMilestone milestone = evt.FindMilestone(threshold);
            App.Notify("Claimed " + (milestone != null ? milestone.Reward.Describe() : "the reward") + ".");
            App.RefreshHud();
            App.RequestSave();
            Rebuild();
        }

        private void OnRedeemCurrency(EventDef evt, long cost)
        {
            bool success = App.Session.RedeemEventCurrency(evt.Id, cost);
            if (!success)
            {
                App.Notify("Not enough " + evt.CurrencyName + ".");
                return;
            }
            App.Audio.Reward();
            EventCurrencyTier tier = evt.FindCurrencyTier(cost);
            App.Notify("Redeemed " + (tier != null ? tier.Reward.Describe() : "the reward") + ".");
            App.RefreshHud();
            App.RequestSave();
            Rebuild();
        }
    }
}
