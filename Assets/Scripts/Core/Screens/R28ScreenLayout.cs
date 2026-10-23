using System;
using System.Collections.Generic;
using System.Text;
using Emberfall.Core.Progression;

namespace Emberfall.Core.Screens
{
    /// <summary>
    /// WHERE THINGS GO ON THE LEADERBOARDS SCREEN, as plain numbers (Docs/ui-specs/R28-SCREENS.md, "Leaderboards").
    ///
    /// Engine-free on purpose: the screen asks Plan where each band sits for the view it is about to draw, and the
    /// harness asks the same function whether a band can run off a short canvas or a tap target can shrink below the
    /// size a thumb needs. Nothing here decides what is shown; every flag comes from the view.
    ///
    /// All distances are in the canvas's own units (the 1920 wide reference), measured from the top of the screen
    /// unless the name says "Inset", which is measured from the bottom.
    /// </summary>
    public sealed class LeaderboardsPlan
    {
        public float TabsTop;
        public float CaptionTop;
        public float BannerTop;
        public bool HasBanner;
        public float LockTop;
        public bool HasLock;
        public float ColumnHeaderTop;
        public float ListTop;
        /// <summary>What the list leaves free under it, from the canvas bottom: the pinned row, the rank line and the margin.</summary>
        public float ListBottomInset;
        public float ListHeight;
        /// <summary>Whole rows that fit in the list at once.</summary>
        public int RowsVisible;
        /// <summary>From the canvas bottom: where the rank and submit line sits, and where the pinned row ends.</summary>
        public float RankLineInset;
        public float PinnedInset;
    }

    public static class LeaderboardsLayout
    {
        /// <summary>The smallest a button or tab may be on either axis. HubStyle.TouchMin, restated here because Core
        /// cannot see the UI; the harness fails the build if the two differ.</summary>
        public const float TapMin = 108f;

        public const float TabHeight = TapMin;
        public const float TabWidth = 280f;
        public const float TabGap = 10f;
        public const float RefreshWidth = 300f;
        public const float RetryWidth = 360f;

        public const float RowHeight = 72f;
        public const float RowGap = 8f;
        public const float CaptionHeight = 36f;
        public const float BannerHeight = 44f;
        public const float LockHeight = 30f;
        public const float ColumnHeaderHeight = 30f;
        public const float RankLineHeight = 44f;
        public const float BottomMargin = 20f;
        public const float DividerHeight = 2f;
        public const float Gap = 8f;

        public const float RankColumn = 170f;
        public const float ValueColumn = 300f;

        public static LeaderboardsPlan Plan(float top, float canvasHeight, bool banner, bool lockLine, bool pinned)
        {
            LeaderboardsPlan p = new LeaderboardsPlan();
            float y = top;
            p.TabsTop = y;
            y += TabHeight + Gap;
            p.CaptionTop = y;
            y += CaptionHeight;
            p.HasBanner = banner;
            if (banner) { p.BannerTop = y; y += BannerHeight + Gap; }
            p.HasLock = lockLine;
            if (lockLine) { p.LockTop = y; y += LockHeight + 4f; }
            p.ColumnHeaderTop = y;
            y += ColumnHeaderHeight + 4f;
            p.ListTop = y;

            p.RankLineInset = BottomMargin;
            float inset = BottomMargin + RankLineHeight;
            if (pinned)
            {
                p.PinnedInset = inset + Gap;
                inset += Gap + RowHeight + Gap + DividerHeight;
            }
            p.ListBottomInset = inset + Gap;
            p.ListHeight = Math.Max(0f, canvasHeight - p.ListTop - p.ListBottomInset);
            p.RowsVisible = (int)Math.Floor((p.ListHeight + RowGap) / (RowHeight + RowGap));
            return p;
        }

        /// <summary>
        /// Everything about a view that changes what is BUILT, as one string. The screen rebuilds when this changes and,
        /// when it does not, only rewrites the three lines that tick (reset, updated, banner), so the list keeps its
        /// scroll position through the once-a-second refresh instead of being rebuilt under the player's thumb.
        /// </summary>
        public static string StructureKey(LeaderboardsView v)
        {
            StringBuilder s = new StringBuilder();
            s.Append((int)v.Phase).Append('|').Append(v.Title).Append('|').Append((int)v.Board).Append('|');
            for (int i = 0; i < v.Tabs.Count; i++) s.Append(v.Tabs[i].Label).Append(v.Tabs[i].Selected ? '*' : '.').Append(',');
            s.Append('|').Append(v.Caption).Append('|').Append(v.ValueHeader).Append('|').Append(v.LockLine).Append('|').Append(v.Status);
            s.Append('|').Append(v.Banner.Length > 0 ? 'B' : '-').Append(v.ShowSpinner ? 'S' : '-');
            s.Append('|').Append(v.MyRankLine).Append('|').Append(v.SubmitLine).Append('|');
            Button(s, v.Refresh);
            Button(s, v.Retry);
            for (int i = 0; i < v.Rows.Count; i++) Row(s, v.Rows[i]);
            s.Append('|');
            if (v.PinnedSelf != null) Row(s, v.PinnedSelf);
            return s.ToString();
        }

        private static void Row(StringBuilder s, LeaderboardRowView r)
        {
            s.Append(r.RankText).Append('~').Append(r.Name).Append('~').Append(r.ValueText).Append(r.IsMe ? '!' : '.').Append(';');
        }

        internal static void Button(StringBuilder s, ButtonModel b)
        {
            s.Append(b.Visible ? 'V' : '-').Append(b.Enabled ? 'E' : '-').Append(b.Label).Append('/').Append(b.Reason).Append('|');
        }
    }

    // ------------------------------------------------------------------ the reminders card

    /// <summary>The bands of the REMINDERS card on the settings page, top to bottom, for one view.</summary>
    public sealed class RemindersPlan
    {
        public float Height;
        public float PermissionTop;
        public float PermissionHeight;
        public bool ReasonUnderButton;
        public float RuleTop1;
        public readonly float[] ToggleTops = new float[4];
        public readonly float[] ToggleHeights = new float[4];
        public readonly int[] SubLines = new int[4];
        public float RuleTop2;
        public bool ShowUpcoming;
        public float UpcomingHeaderTop;
        public readonly List<float> UpcomingTops = new List<float>();
        public readonly List<float> UpcomingHeights = new List<float>();
        public float UpcomingStatusTop;
        public float RuleTop3;
        public float RulesTop;
    }

    public static class RemindersCardLayout
    {
        public const float TapMin = 108f;
        public const float SwitchWidth = 220f;
        public const float PermissionButtonWidth = 400f;
        public const float Pad = 30f;
        public const float HeadingBand = 64f;
        public const float RuleBand = 22f;
        /// <summary>UITheme.Space24: how far the three topics sit in under Reminders.</summary>
        public const float TopicIndent = 24f;
        public const float LineHeight = 28f;
        public const float TitleLineHeight = 34f;
        public const float ReasonHeight = 28f;
        public const float RowPad = 14f;
        public const float BottomPad = 24f;
        /// <summary>A rough width of one character at the small text size (UITheme.FontTiny), for counting wrapped lines.</summary>
        public const float CharWidth = 11f;

        /// <summary>How many lines a sentence takes in a box this wide. At least one.</summary>
        public static int LinesFor(string text, float width)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int lines = (int)Math.Ceiling(text.Length * CharWidth / Math.Max(1f, width));
            return Math.Max(1, lines);
        }

        /// <summary>
        /// The line printed under a switch's label: its Reason while it cannot be flipped (the spec's rule), else its
        /// Description. A reason that repeats the sentence the card already prints once in the permission slot is not
        /// printed again four times down the card, so the Description stands in for it.
        /// </summary>
        public static string SubLine(ToggleModel t, string alreadyShown)
        {
            if (!t.Enabled && t.Reason.Length > 0 && t.Reason != alreadyShown) return t.Reason;
            return t.Description;
        }

        /// <summary>Width left for a switch row's words once the switch, the indent and the card's padding are taken.</summary>
        public static float TextWidth(float cardWidth, float indent)
        {
            return cardWidth - Pad * 2f - SwitchWidth - 16f - indent;
        }

        public static RemindersPlan Plan(NotificationSettingsView v, float cardWidth)
        {
            RemindersPlan p = new RemindersPlan();
            float y = HeadingBand;

            p.PermissionTop = y;
            bool button = v.PermissionButton.Visible;
            p.ReasonUnderButton = button && !v.PermissionButton.Enabled && v.PermissionButton.Reason.Length > 0;
            float lineWidth = cardWidth - Pad * 2f - (button ? PermissionButtonWidth + 16f : 0f);
            float textHeight = Math.Max(1, LinesFor(v.PermissionLine, lineWidth)) * (LineHeight + 4f);
            p.PermissionHeight = button ? Math.Max(TapMin + (p.ReasonUnderButton ? ReasonHeight : 0f), textHeight) : textHeight;
            y += p.PermissionHeight;

            p.RuleTop1 = y;
            y += RuleBand;

            ToggleModel[] t = { v.Master, v.IdleChest, v.Orders, v.Events };
            for (int i = 0; i < 4; i++)
            {
                float indent = i == 0 ? 0f : TopicIndent;
                string sub = SubLine(t[i], v.Phase == ScreenPhase.Unavailable ? v.Status : "");
                int lines = LinesFor(sub, TextWidth(cardWidth, indent));
                p.SubLines[i] = lines;
                float text = TitleLineHeight + lines * LineHeight + (t[i].Note.Length > 0 ? LineHeight : 0f);
                p.ToggleTops[i] = y;
                p.ToggleHeights[i] = Math.Max(TapMin, text) + RowPad;
                y += p.ToggleHeights[i];
            }

            p.ShowUpcoming = v.Upcoming.Count > 0 || v.UpcomingStatus.Length > 0;
            if (p.ShowUpcoming)
            {
                p.RuleTop2 = y;
                y += RuleBand;
                p.UpcomingHeaderTop = y;
                y += 36f;
                for (int i = 0; i < v.Upcoming.Count; i++)
                {
                    float h = TitleLineHeight + (v.Upcoming[i].Body.Length > 0 ? LinesFor(v.Upcoming[i].Body, cardWidth - Pad * 2f - 240f) * LineHeight : 0f) + 12f;
                    p.UpcomingTops.Add(y);
                    p.UpcomingHeights.Add(h);
                    y += h;
                }
                if (v.UpcomingStatus.Length > 0)
                {
                    p.UpcomingStatusTop = y;
                    y += LineHeight + 8f;
                }
            }

            p.RuleTop3 = y;
            y += RuleBand;
            p.RulesTop = y;
            y += v.RuleLines.Count * LineHeight;
            p.Height = y + BottomPad;
            return p;
        }

        /// <summary>Everything about a view that changes what is built, as one string, so the card is rebuilt only when it differs.</summary>
        public static string StructureKey(NotificationSettingsView v)
        {
            StringBuilder s = new StringBuilder();
            s.Append((int)v.Phase).Append('|').Append(v.Status).Append('|').Append(v.PermissionLine).Append('|');
            LeaderboardsLayout.Button(s, v.PermissionButton);
            ToggleModel[] t = { v.Master, v.IdleChest, v.Orders, v.Events };
            for (int i = 0; i < t.Length; i++)
                s.Append(t[i].Label).Append('~').Append(t[i].Description).Append('~').Append(t[i].On ? 'O' : '-').Append(t[i].Enabled ? 'E' : '-')
                 .Append('~').Append(t[i].Reason).Append('~').Append(t[i].Note).Append(';');
            s.Append('|').Append(v.UpcomingStatus).Append('|');
            for (int i = 0; i < v.Upcoming.Count; i++) s.Append(v.Upcoming[i].Title).Append('~').Append(v.Upcoming[i].Body).Append('~').Append(v.Upcoming[i].WhenText).Append(';');
            s.Append('|');
            for (int i = 0; i < v.RuleLines.Count; i++) s.Append(v.RuleLines[i]).Append(';');
            return s.ToString();
        }
    }
}
