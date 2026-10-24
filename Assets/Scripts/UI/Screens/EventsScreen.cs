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

        /// <summary>
        /// This screen carries its own CurrencyStrip, so the shared top bar's pills would state
        /// the same numbers a few pixels above it. One readout, not two. Same mechanism
        /// BattleScreen already uses, and ScreenRouter re-applies it on every Show, so the bar
        /// returns intact on any screen that does not opt out.
        /// </summary>
        public override bool HidesTopBar { get { return true; } }

        private const float CardPadding = 24f;
        // 130, not 108: 44 for the name row, 34 for the points line, then 36 for the progress
        // bar and 16 of air. The bar grew from 14 when it became the kit's, because a nine-sliced
        // trough keeps most of its height in its top and bottom borders and at 14 has no middle
        // left to stretch. Miss this and the milestone track is drawn 22 units up into the bar.
        private const float HeaderBlock = 130f;
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
            EmberforgeUI.Ground(Root);
            ScreenChrome.ForgedHeader(Root, "LIMITED-TIME EVENTS", Router, out _headerSubtitle);
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
                Text empty = EmberforgeUI.Label("Empty", _listContent, "No events in the content database.",
                                             UITheme.FontSmall, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
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

            // A RUNNING event is framed and an ended one is a plain plate. The gold frame is
            // what says "this is live", which is a structural fact about the card rather than a
            // second colour - and an ended event whose leftover currency is still spendable is
            // still a real card rather than a dimmed one.
            if (active) EmberforgeUI.PanelOn(card, false);
            else EmberforgeUI.PlainPanelOn(card);

            float top = CardPadding;

            // ---- name, status, and the points line
            Text nameLabel = EmberforgeUI.Title("Name", card, evt.Name, UITheme.FontBody,
                                                Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(nameLabel.rectTransform, 28f, -top, 700f, 38f, new Vector2(0f, 1f));

            string statusText; Color statusColor;
            StatusOf(evt, now, out statusText, out statusColor);
            RectTransform statusChip = EmberforgeUI.Chip("Status", card, statusText, statusColor, UITheme.FontTiny);
            UIBuilder.Box(statusChip, -28f, -top, 240f, 32f, new Vector2(1f, 1f));

            top += 44f;

            long nextThreshold = NextUnclaimedThreshold(evt, progress);
            long denominator = nextThreshold > 0 ? nextThreshold : evt.HighestThreshold;
            float ratio = denominator > 0 ? Mathf.Clamp01(points / (float)denominator) : 1f;

            Text pointsLabel = EmberforgeUI.Label("Points", card,
                points + (nextThreshold > 0 ? " / " + nextThreshold : "") + "  " + evt.PointsLabel.ToUpperInvariant(),
                UITheme.FontSmall, Emberforge.TextBody, TextAnchor.UpperLeft);
            UIBuilder.Box(pointsLabel.rectTransform, 28f, -top, 700f, 30f, new Vector2(0f, 1f));
            top += 34f;

            // THE KIT'S BAR, at the sprite's own height. Its trough and fill keep most of their
            // height in their top and bottom borders, so a 14-unit track has no middle left to
            // stretch and both render as a squashed pair of corners - the same fault the Rift's
            // guarantee bar hit, and the reason that one grew too.
            Image pointsBar = EmberforgeUI.ProgressBar("PointsBar", card);
            StretchTop(pointsBar.rectTransform.parent as RectTransform, top, 36f, 28f, 28f);
            EmberforgeUI.SetProgress(pointsBar, ratio);
            top += 36f + UITheme.Space16;

            // ---- the milestone track
            Text trackHeading = EmberforgeUI.Title("TrackHeading", card, "MILESTONES",
                                                   UITheme.FontTiny, Emberforge.TextGold,
                                                   TextAnchor.UpperLeft);
            UIBuilder.Box(trackHeading.rectTransform, 28f, -top, 400f, SectionLabelHeight, new Vector2(0f, 1f));
            top += SectionLabelHeight;

            BuildTrack(card, evt, progress, points, top);
            top += TrackBlock;

            // ---- the currency tiers, when the event has any
            if (!evt.HasCurrency) return;

            top += UITheme.Space24;
            Text tierHeading = EmberforgeUI.Label("TierHeading", card,
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
                Text none = EmberforgeUI.Label("None", track, "This event authors no milestones.",
                                            UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
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
            bool claimed = progress != null && progress.HasClaimed(milestone.Id);
            bool reached = points >= milestone.Threshold;
            bool claimable = reached && !claimed;

            Color tint = claimed ? UITheme.Success : (reached ? UITheme.Gold : Emberforge.TextQuiet);

            RectTransform column = UIBuilder.Node("Node_" + milestone.Id, track);
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
            Text threshold = EmberforgeUI.Label("Threshold", column, milestone.Threshold.ToString(),
                                             UITheme.FontTiny - 2, reached ? Emberforge.TextBody : Emberforge.TextQuiet,
                                             TextAnchor.UpperCenter);
            UIBuilder.Box(threshold.rectTransform, 0f, -top, 140f, 24f, new Vector2(0.5f, 1f));
            top += 26f;

            // ---- the reward tile
            RectTransform tile = UIBuilder.Node("Tile", column);
            UIBuilder.Box(tile, 0f, -top, TileSize, TileSize, new Vector2(0.5f, 1f));

            // A CLAIMABLE milestone stands proud of the track and everything else is set into
            // it, which is the same depth-carries-state rule the guild roster and the friends
            // list follow. The tint is left on the mark, where it says which KIND of reward this
            // rung pays rather than what state the rung is in.
            if (claimable) EmberforgeUI.PanelOn(tile, false);
            else EmberforgeUI.SunkenOn(tile);

            Color markTint = tint;
            Sprite glyph = claimed ? UiKit.Check(64, ref markTint)
                         : (!reached ? UiKit.Glyph("lock", TextureLab.Padlock(64), ref markTint)
                         : (isLast ? TextureLab.Chest(64) : TextureLab.GemIcon(64)));
            Image mark = UIBuilder.Img("Mark", tile, glyph, markTint);
            UIBuilder.Box(mark.rectTransform, 0f, 0f, 44f, 44f, new Vector2(0.5f, 0.5f));

            // The tile is the button. A separate CLAIM control under a 96px tile would double the
            // column's height for a second target hitting the same action.
            if (claimable)
            {
                Button press = tile.gameObject.AddComponent<Button>();
                press.transition = Selectable.Transition.None;
                EventDef capturedEvt = evt;
                EventMilestone capturedMilestone = milestone;
                press.onClick.AddListener(delegate { OnClaimMilestone(capturedEvt, capturedMilestone); });
            }

            top += TileSize + UITheme.Space4;

            // ---- what it actually is
            Text reward = EmberforgeUI.Body("Reward", column, milestone.Reward.Describe(),
                                                 UITheme.FontTiny - 6,
                                                 reached ? Emberforge.TextQuiet : Emberforge.TextQuiet,
                                                 TextAnchor.UpperCenter);
            UIBuilder.Box(reward.rectTransform, 0f, -top, 140f, 34f, new Vector2(0.5f, 1f));
            top += 36f;

            Text state = EmberforgeUI.Label("State", column,
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
                Text none = EmberforgeUI.Label("None", row, "Nothing to redeem yet.",
                                            UITheme.FontTiny, Emberforge.TextQuiet, TextAnchor.MiddleCenter);
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

            // Affordable is framed, out of reach is plain. Depth again, and for the reason the
            // milestone tiles use it: what the player can act on is what stands up off the page.
            if (affordable) EmberforgeUI.PanelOn(cardRoot, false);
            else EmberforgeUI.PlainPanelOn(cardRoot);

            float top = UITheme.Space12;

            RectTransform tile = UIBuilder.Node("Tile", cardRoot);
            UIBuilder.Box(tile, 0f, -top, 78f, 78f, new Vector2(0.5f, 1f));

            Image tileBg = EmberforgeUI.SunkenOn(tile);
            tileBg.raycastTarget = false;

            Image mark = UIBuilder.Img("Mark", tile, TextureLab.GemIcon(64), UITheme.EventToken);
            UIBuilder.Box(mark.rectTransform, 0f, 0f, 40f, 40f, new Vector2(0.5f, 0.5f));

            top += 78f + UITheme.Space4;

            Text reward = EmberforgeUI.Body("Reward", cardRoot, tier.Reward.Describe(),
                                                 UITheme.FontTiny - 4, Emberforge.TextBody, TextAnchor.UpperCenter);
            UIBuilder.Box(reward.rectTransform, 0f, -top, 276f, 44f, new Vector2(0.5f, 1f));

            // ---- cost, in the strip's language
            RectTransform cost = UIBuilder.Node("Cost", cardRoot);
            UIBuilder.Box(cost, 0f, 56f, 260f, 28f, new Vector2(0.5f, 0f));

            Image costPlate = EmberforgeUI.SunkenOn(cost);
            costPlate.raycastTarget = false;

            Image costIcon = UIBuilder.Img("Icon", cost, TextureLab.GemIcon(64), UITheme.EventToken);
            UIBuilder.Box(costIcon.rectTransform, UITheme.Space8, 0f, 20f, 20f, new Vector2(0f, 0.5f));

            Text costText = EmberforgeUI.Label("Value", cost, tier.Cost.ToString(), UITheme.FontTiny - 2,
                                            affordable ? Emberforge.TextGold : UITheme.Danger,
                                            TextAnchor.MiddleLeft);
            UIBuilder.FillParent(costText.rectTransform, UITheme.Space8 + 20f + UITheme.Space8, 0f, UITheme.Space8, 0f);

            UIButton redeem = EmberforgeUI.Button("Redeem", cardRoot, "REDEEM", UITheme.FontTiny, ForgeButtonKind.Secondary);
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
                bool claimed = progress != null && progress.HasClaimed(evt.Milestones[i].Id);
                if (!claimed) return evt.Milestones[i].Threshold;
            }
            return 0;
        }

        private static void StatusOf(EventDef evt, long now, out string text, out Color color)
        {
            if (evt.IsActive(now)) { text = "ACTIVE"; color = UITheme.Success; return; }
            if (evt.HasNotStarted(now)) { text = "STARTS IN " + FormatDays(evt.StartUnix - now); color = Emberforge.TextQuiet; return; }
            text = "ENDED"; color = Emberforge.TextQuiet;
        }

        private static string FormatDays(long seconds)
        {
            long days = seconds / 86400L;
            if (days > 0) return days + "D";
            long hours = seconds / 3600L;
            return System.Math.Max(1L, hours) + "H";
        }

        // ------------------------------------------------------------------ actions

        private void OnClaimMilestone(EventDef evt, EventMilestone milestone)
        {
            bool success = App.Session.ClaimEventMilestone(evt.Id, milestone.Id);
            if (!success)
            {
                App.Notify("Not ready to claim yet.");
                return;
            }
            App.Audio.Reward();
            App.Notify("Claimed " + milestone.Reward.Describe() + ".");
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
