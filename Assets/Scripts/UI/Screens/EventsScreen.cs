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
    ///
    /// ---- visual pass ----
    /// THE MILESTONE LADDER IS HORIZONTAL AND IT IS A TRACK. It used to be a stack of identical
    /// full-width rows, each with its own CLAIM button, which said nothing about ORDER or DISTANCE
    /// - the two things a milestone ladder exists to communicate. It is now a rail with a node per
    /// threshold: the rail is lit up to where the player has reached, nodes past that are dark, and
    /// the reward under each node is a tile rather than a sentence. Where they are on the track is
    /// now visible without reading a single number.
    ///
    /// THE CURRENCY TIERS ARE A CARD ROW, AND VISIBLY NOT THE TRACK. They were the same row widget
    /// as the milestones, one section heading apart, which made a spend look like a claim. They are
    /// now cards - a different shape, a different frame, their own currency icon on every price -
    /// because the two do genuinely different things: a milestone is earned once and taken, a tier
    /// is bought repeatedly out of a balance.
    ///
    /// Claiming and redeeming are the same calls they always were. OnClaimMilestone and
    /// OnRedeemCurrency are untouched, and both still gate on GameSession rather than on anything
    /// this screen believes.
    /// </summary>
    public sealed class EventsScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Events; } }

        private const float CardPadding = 24f;
        private const float HeaderBlock = 108f;
        private const float TrackBlock = 244f;
        private const float TierBlock = 250f;
        private const float SectionLabelHeight = 30f;
        private const float NodeSize = 26f;
        private const float TileSize = 96f;

        private Text _headerSubtitle;
        private readonly CurrencyStrip _currencies = new CurrencyStrip();
        private RectTransform _listContent;

        protected override void Build()
        {
            ScreenChrome.Header(Root, "EVENTS", Router, out _headerSubtitle);
            _currencies.Build(Root);

            ScrollRect scroll;
            _listContent = UIBuilder.ScrollList("EventList", Root, out scroll, 20f, new RectOffset(32, 32, 12, 40));
            UIBuilder.FillParent(scroll.GetComponent<RectTransform>(), 0f,
                                 CurrencyStrip.BottomOf(CurrencyStrip.DefaultTop), 0f, 0f);
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
            _currencies.Refresh(App.Session, now);

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
            float h = CardPadding + HeaderBlock + SectionLabelHeight + TrackBlock;
            if (evt.HasCurrency) h += UITheme.Space24 + SectionLabelHeight + TierBlock;
            return h + CardPadding;
        }

        private void BuildEventCard(EventDef evt, long now)
        {
            EventProgressEntry progress = App.Session.Profile.FindEventProgress(evt.Id);
            long points = progress != null ? progress.Points : 0L;
            long currency = progress != null ? progress.Currency : 0L;
            bool active = evt.IsActive(now);

            RectTransform card = UIBuilder.Node("Event_" + evt.Id, _listContent);
            UIBuilder.Sized(card, 0f, CardHeight(evt));

            Image bg = card.gameObject.AddComponent<Image>();
            bg.sprite = TextureLab.NotchedRect(20);
            bg.type = Image.Type.Sliced;
            bg.color = active ? UITheme.WithAlpha(UITheme.PanelRaised, 0.92f) : UITheme.Panel;
            UIBuilder.NotchedFrame("Edge", card,
                                   UITheme.WithAlpha(active ? UITheme.ChromeBrass : UITheme.BorderSoft,
                                                     active ? 0.85f : 0.5f), 20, 2);

            float top = CardPadding;

            // ---- name, status, and the points line
            Text nameLabel = UIBuilder.Label("Name", card, evt.Name, UITheme.FontBody,
                                             UITheme.TextPrimary, TextAnchor.UpperLeft);
            UIBuilder.Box(nameLabel.rectTransform, 28f, -top, 700f, 38f, new Vector2(0f, 1f));

            string statusText; Color statusColor;
            StatusOf(evt, now, out statusText, out statusColor);
            RectTransform statusChip = UIBuilder.Chip("Status", card, statusText, statusColor, UITheme.FontTiny);
            UIBuilder.Box(statusChip, -28f, -top, 240f, 32f, new Vector2(1f, 1f));

            top += 44f;

            long nextThreshold = NextUnclaimedThreshold(evt, progress);
            long denominator = nextThreshold > 0 ? nextThreshold : evt.HighestThreshold;
            float ratio = denominator > 0 ? Mathf.Clamp01(points / (float)denominator) : 1f;

            Text pointsLabel = UIBuilder.Label("Points", card,
                points + (nextThreshold > 0 ? " / " + nextThreshold : "") + "  " + evt.PointsLabel.ToUpperInvariant(),
                UITheme.FontSmall, UITheme.TextPrimary, TextAnchor.UpperLeft);
            UIBuilder.Box(pointsLabel.rectTransform, 28f, -top, 700f, 30f, new Vector2(0f, 1f));
            top += 34f;

            Image pointsBar = UIBuilder.Bar("PointsBar", card, UITheme.PanelSunken, UITheme.Gold);
            StretchTop(pointsBar.rectTransform.parent as RectTransform, top, 14f, 28f, 28f);
            UIBuilder.SetBar(pointsBar, ratio);
            top += 14f + UITheme.Space16;

            // ---- the milestone track
            Text trackHeading = UIBuilder.Label("TrackHeading", card, "MILESTONES",
                                                UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperLeft);
            UIBuilder.Box(trackHeading.rectTransform, 28f, -top, 400f, SectionLabelHeight, new Vector2(0f, 1f));
            top += SectionLabelHeight;

            BuildTrack(card, evt, progress, points, top);
            top += TrackBlock;

            // ---- the currency tiers, when the event has any
            if (!evt.HasCurrency) return;

            top += UITheme.Space24;
            Text tierHeading = UIBuilder.Label("TierHeading", card,
                evt.CurrencyName.ToUpperInvariant() + "  -  " + currency + " HELD",
                UITheme.FontTiny, UITheme.EventToken, TextAnchor.UpperLeft);
            UIBuilder.Box(tierHeading.rectTransform, 28f, -top, 600f, SectionLabelHeight, new Vector2(0f, 1f));
            top += SectionLabelHeight;

            BuildTierRow(card, evt, currency, top);
        }

        /// <summary>
        /// The horizontal point ladder: a rail, a node per threshold, a reward tile under each.
        ///
        /// Positioned by ANCHOR FRACTION rather than by pixel offset, because a card in a scroll
        /// list has whatever width the list gives it and this screen never learns that number. Node
        /// i of n sits at (i + 0.5) / n across, which spaces them evenly and leaves half a cell of
        /// margin at both ends without the card's own width appearing anywhere.
        /// </summary>
        private void BuildTrack(RectTransform card, EventDef evt, EventProgressEntry progress,
                                long points, float top)
        {
            int count = evt.Milestones.Count;

            RectTransform track = UIBuilder.Node("Track", card);
            StretchTop(track, top, TrackBlock, 28f, 28f);

            if (count == 0)
            {
                Text none = UIBuilder.Label("None", track, "This event authors no milestones.",
                                            UITheme.FontTiny, UITheme.TextDim, TextAnchor.MiddleCenter);
                UIBuilder.FillParent(none.rectTransform, 0f);
                return;
            }

            float first = 0.5f / count;
            float last = (count - 0.5f) / count;

            // How far along the rail the player has actually got. The rail is lit to the last
            // threshold they have MET, so the lit length is the same fact the nodes state - not a
            // second, subtly different progress number.
            float reachedFraction = 0f;
            for (int i = 0; i < count; i++)
                if (points >= evt.Milestones[i].Threshold) reachedFraction = (i + 0.5f) / count;

            Rail(track, "RailDim", first, last, UITheme.WithAlpha(UITheme.BorderSoft, 0.9f), 4f);
            if (reachedFraction > first)
                Rail(track, "RailLit", first, reachedFraction, UITheme.Gold, 6f);

            for (int i = 0; i < count; i++)
                BuildNode(track, evt, evt.Milestones[i], progress, points, (i + 0.5f) / count, i == count - 1);
        }

        private static void Rail(RectTransform parent, string name, float from, float to, Color color, float height)
        {
            Image rail = UIBuilder.Img(name, parent, TextureLab.White, color);
            RectTransform rt = rail.rectTransform;
            rt.anchorMin = new Vector2(from, 1f);
            rt.anchorMax = new Vector2(to, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(0f, -(NodeSize * 0.5f + height * 0.5f));
            rt.offsetMax = new Vector2(0f, -(NodeSize * 0.5f - height * 0.5f));
        }

        /// <summary>
        /// One milestone: the threshold, a node on the rail, and a reward tile that IS the claim
        /// button when there is something to claim.
        ///
        /// Three states, and each is a different shape rather than a different shade: claimed shows
        /// a check and stops being pressable, reached shows the reward on a lit tile that can be
        /// pressed, and locked shows a padlock. The old list said all three with grey text.
        /// </summary>
        private void BuildNode(RectTransform track, EventDef evt, EventMilestone milestone,
                               EventProgressEntry progress, long points, float centre, bool isLast)
        {
            bool claimed = progress != null && progress.ClaimedMilestoneThresholds.Contains(milestone.Threshold);
            bool reached = points >= milestone.Threshold;
            bool claimable = reached && !claimed;

            Color tint = claimed ? UITheme.Success : (reached ? UITheme.Gold : UITheme.TextDim);

            RectTransform column = UIBuilder.Node("Node_" + milestone.Threshold, track);
            column.anchorMin = new Vector2(centre, 0f);
            column.anchorMax = new Vector2(centre, 1f);
            column.pivot = new Vector2(0.5f, 0.5f);
            column.offsetMin = new Vector2(-72f, 0f);
            column.offsetMax = new Vector2(72f, 0f);

            // ---- the node on the rail
            float nodeSize = isLast ? NodeSize * 1.35f : NodeSize;
            Image node = UIBuilder.Img("Pip", column, TextureLab.Diamond(48),
                                       reached ? tint : UITheme.WithAlpha(UITheme.PanelSunken, 1f));
            UIBuilder.Box(node.rectTransform, 0f, -(NodeSize * 0.5f), nodeSize, nodeSize, new Vector2(0.5f, 1f));

            Image nodeEdge = UIBuilder.Img("PipEdge", column, TextureLab.Diamond(48),
                                           UITheme.WithAlpha(reached ? tint : UITheme.BorderSoft, 0.5f));
            UIBuilder.Box(nodeEdge.rectTransform, 0f, -(NodeSize * 0.5f), nodeSize + 8f, nodeSize + 8f,
                          new Vector2(0.5f, 1f));
            nodeEdge.rectTransform.SetAsFirstSibling();

            float top = NodeSize + UITheme.Space8;

            // ---- threshold
            Text threshold = UIBuilder.Label("Threshold", column, milestone.Threshold.ToString(),
                                             UITheme.FontTiny - 2, reached ? UITheme.TextPrimary : UITheme.TextDim,
                                             TextAnchor.UpperCenter);
            UIBuilder.Box(threshold.rectTransform, 0f, -top, 140f, 24f, new Vector2(0.5f, 1f));
            top += 26f;

            // ---- the reward tile
            RectTransform tile = UIBuilder.Node("Tile", column);
            UIBuilder.Box(tile, 0f, -top, TileSize, TileSize, new Vector2(0.5f, 1f));

            Image tileBg = tile.gameObject.AddComponent<Image>();
            tileBg.sprite = TextureLab.NotchedRect(12);
            tileBg.type = Image.Type.Sliced;
            tileBg.color = claimable
                ? UITheme.WithAlpha(UITheme.Gold, 0.16f)
                : UITheme.WithAlpha(UITheme.PanelSunken, 0.92f);
            UIBuilder.NotchedFrame("TileEdge", tile, UITheme.WithAlpha(tint, reached ? 0.85f : 0.4f), 12, 2);

            Sprite glyph = claimed ? TextureLab.Check(64)
                         : (!reached ? TextureLab.Padlock(64)
                         : (isLast ? TextureLab.Chest(64) : TextureLab.GemIcon(64)));
            Image mark = UIBuilder.Img("Mark", tile, glyph, tint);
            UIBuilder.Box(mark.rectTransform, 0f, 0f, 44f, 44f, new Vector2(0.5f, 0.5f));

            // The tile is the button. A separate CLAIM control under a 96px tile would double the
            // column's height for a second target hitting the same action.
            if (claimable)
            {
                Button press = tile.gameObject.AddComponent<Button>();
                press.transition = Selectable.Transition.None;
                EventDef capturedEvt = evt;
                long capturedThreshold = milestone.Threshold;
                press.onClick.AddListener(delegate { OnClaimMilestone(capturedEvt, capturedThreshold); });
            }

            top += TileSize + UITheme.Space4;

            // ---- what it actually is
            Text reward = UIBuilder.WrappedLabel("Reward", column, milestone.Reward.Describe(),
                                                 UITheme.FontTiny - 6,
                                                 reached ? UITheme.TextMuted : UITheme.TextDim,
                                                 TextAnchor.UpperCenter);
            UIBuilder.Box(reward.rectTransform, 0f, -top, 140f, 34f, new Vector2(0.5f, 1f));
            top += 36f;

            Text state = UIBuilder.Label("State", column,
                                         claimed ? "CLAIMED" : (claimable ? "CLAIM" : ""),
                                         UITheme.FontTiny - 4, tint, TextAnchor.UpperCenter);
            UIBuilder.Box(state.rectTransform, 0f, -top, 140f, 22f, new Vector2(0.5f, 1f));
        }

        /// <summary>
        /// The redeemable tiers, as a card row that cannot be mistaken for the track above it.
        ///
        /// Evenly spaced by anchor fraction for the same reason the track is: the card's width
        /// belongs to the scroll list, not to this screen.
        /// </summary>
        private void BuildTierRow(RectTransform card, EventDef evt, long currency, float top)
        {
            int count = evt.CurrencyRewards.Count;

            RectTransform row = UIBuilder.Node("Tiers", card);
            StretchTop(row, top, TierBlock, 28f, 28f);

            if (count == 0)
            {
                Text none = UIBuilder.Label("None", row, "Nothing to redeem yet.",
                                            UITheme.FontTiny, UITheme.TextDim, TextAnchor.MiddleCenter);
                UIBuilder.FillParent(none.rectTransform, 0f);
                return;
            }

            for (int i = 0; i < count; i++)
                BuildTierCard(row, evt, evt.CurrencyRewards[i], currency, (i + 0.5f) / count);
        }

        private void BuildTierCard(RectTransform row, EventDef evt, EventCurrencyTier tier,
                                   long currency, float centre)
        {
            bool affordable = currency >= tier.Cost;

            RectTransform cardRoot = UIBuilder.Node("Tier_" + tier.Cost, row);
            cardRoot.anchorMin = new Vector2(centre, 0f);
            cardRoot.anchorMax = new Vector2(centre, 1f);
            cardRoot.pivot = new Vector2(0.5f, 0.5f);
            cardRoot.offsetMin = new Vector2(-150f, 0f);
            cardRoot.offsetMax = new Vector2(150f, 0f);

            Image bg = cardRoot.gameObject.AddComponent<Image>();
            bg.sprite = TextureLab.NotchedRect(14);
            bg.type = Image.Type.Sliced;
            bg.color = UITheme.WithAlpha(UITheme.PanelSunken, 0.92f);
            UIBuilder.NotchedFrame("Edge", cardRoot,
                                   UITheme.WithAlpha(UITheme.EventToken, affordable ? 0.85f : 0.4f), 14, 2);

            float top = UITheme.Space12;

            RectTransform tile = UIBuilder.Node("Tile", cardRoot);
            UIBuilder.Box(tile, 0f, -top, 78f, 78f, new Vector2(0.5f, 1f));

            Image tileBg = tile.gameObject.AddComponent<Image>();
            tileBg.sprite = TextureLab.NotchedRect(10);
            tileBg.type = Image.Type.Sliced;
            tileBg.color = UITheme.WithAlpha(UITheme.Panel, 0.95f);
            tileBg.raycastTarget = false;
            UIBuilder.NotchedFrame("TileEdge", tile, UITheme.WithAlpha(UITheme.EventToken, 0.6f), 10, 2);

            Image mark = UIBuilder.Img("Mark", tile, TextureLab.GemIcon(64), UITheme.EventToken);
            UIBuilder.Box(mark.rectTransform, 0f, 0f, 40f, 40f, new Vector2(0.5f, 0.5f));

            top += 78f + UITheme.Space4;

            Text reward = UIBuilder.WrappedLabel("Reward", cardRoot, tier.Reward.Describe(),
                                                 UITheme.FontTiny - 4, UITheme.TextPrimary, TextAnchor.UpperCenter);
            UIBuilder.Box(reward.rectTransform, 0f, -top, 276f, 44f, new Vector2(0.5f, 1f));

            // ---- cost, in the strip's language
            RectTransform cost = UIBuilder.Node("Cost", cardRoot);
            UIBuilder.Box(cost, 0f, 56f, 260f, 28f, new Vector2(0.5f, 0f));

            Image costPlate = cost.gameObject.AddComponent<Image>();
            costPlate.sprite = UISkin.Panel(8);
            costPlate.type = Image.Type.Sliced;
            costPlate.color = UITheme.WithAlpha(HubStyle.Plate, HubStyle.PlateAlphaSolid);
            costPlate.raycastTarget = false;

            Image costIcon = UIBuilder.Img("Icon", cost, TextureLab.GemIcon(64), UITheme.EventToken);
            UIBuilder.Box(costIcon.rectTransform, UITheme.Space8, 0f, 20f, 20f, new Vector2(0f, 0.5f));

            Text costText = UIBuilder.Label("Value", cost, tier.Cost.ToString(), UITheme.FontTiny - 2,
                                            affordable ? UITheme.TextPrimary : UITheme.Danger,
                                            TextAnchor.MiddleLeft);
            UIBuilder.FillParent(costText.rectTransform, UITheme.Space8 + 20f + UITheme.Space8, 0f, UITheme.Space8, 0f);

            UIButton redeem = UIBuilder.Notch(
                UIBuilder.SecondaryButton("Redeem", cardRoot, "REDEEM", UITheme.FontTiny), 10);
            UIBuilder.Box(redeem.Root, 0f, UITheme.Space12, 260f, 38f, new Vector2(0.5f, 0f));
            redeem.Interactable = affordable;

            EventDef capturedEvt = evt;
            long capturedCost = tier.Cost;
            redeem.OnClick(delegate { OnRedeemCurrency(capturedEvt, capturedCost); });
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
