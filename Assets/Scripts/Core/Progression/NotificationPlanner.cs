using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Session;

namespace Emberfall.Core.Progression
{
    /// <summary>What a local notification is about. Each kind has one stable id, so a replan replaces
    /// the notification it planned last time instead of stacking a second one.</summary>
    public enum NotificationKind
    {
        IdleChestFull,
        OrdersReset,
        EventEndingWeekly,
        EventEndingMonthly
    }

    /// <summary>One notification the device should schedule: when, and the words. The words never name a
    /// price, an offer or anything sold (Docs/DESIGN-NOTIFICATIONS.md, "Nothing sold").</summary>
    public sealed class PlannedNotification
    {
        public NotificationKind Kind;
        public int Id;
        public long FireUnix;
        public string Title = "";
        public string Body = "";
    }

    /// <summary>The player's own switches, kept on the device (PlayerPrefs), not in the save. All on by
    /// default; Enabled false is "no notifications at all".</summary>
    public sealed class NotificationPrefs
    {
        public bool Enabled = true;
        public bool IdleChest = true;
        public bool Orders = true;
        public bool Events = true;
    }

    /// <summary>The timings and caps. Every number is a first pass; Docs/DESIGN-NOTIFICATIONS.md says why
    /// each is what it is.</summary>
    public sealed class NotificationRules
    {
        /// <summary>Quiet hours in the player's local time, minutes after local midnight. Nothing fires from
        /// QuietStartMinute up to QuietEndMinute. Equal values mean no quiet hours.</summary>
        public int QuietStartMinute = 22 * 60;
        public int QuietEndMinute = 9 * 60;
        /// <summary>A deadline that falls in quiet hours moves to this long before they start.</summary>
        public long EveningMarginSeconds = 15L * 60L;
        /// <summary>At most this many notifications on one local calendar day.</summary>
        public int MaxPerLocalDay = 2;
        /// <summary>At least this long between any two.</summary>
        public long MinGapSeconds = 4L * 3600L;
        /// <summary>Nothing is planned further ahead than this. A player who stops opening the game gets what
        /// was planned at their last visit and then silence.</summary>
        public long HorizonSeconds = 48L * 3600L;
        /// <summary>Nothing fires sooner than this after the plan is made.</summary>
        public long MinLeadSeconds = 30L * 60L;
        /// <summary>How long before an event ends its reminder fires.</summary>
        public long WeeklyEventLeadSeconds = 8L * 3600L;
        public long MonthlyEventLeadSeconds = 24L * 3600L;
    }

    /// <summary>
    /// WHEN TO FIRE, GIVEN A SAVE (Docs/DESIGN-NOTIFICATIONS.md). A pure function of the profile, the
    /// content, the clock and the device's UTC offset: it reads, it writes nothing, and the same inputs
    /// give the same plan. The Unity side calls Plan when the app goes to the background, cancels every
    /// notification it scheduled before, and schedules exactly what this returns.
    ///
    /// THREE TOPICS. The idle chest reaching its cap (Balance.IdleCapHours after the last claim). The daily
    /// orders turning over (the reward day, GameSession.DayStartUnix), only while the daily loop is on.
    /// A live event ending, only for a player who has taken part in it and still has something free to
    /// gain from it.
    ///
    /// THE CAPS. Nothing in quiet hours: a reminder that something is ready moves to the end of them, a
    /// deadline moves to the evening before. At least MinGapSeconds between any two, at most MaxPerLocalDay
    /// on one local day, nothing past HorizonSeconds, nothing sooner than MinLeadSeconds. Where two
    /// collide the higher priority keeps its time: an event ending, then the idle chest, then the orders.
    /// A deadline is never moved past its deadline; it is dropped instead.
    /// </summary>
    public static class NotificationPlanner
    {
        public const int IdleChestId = 101;
        public const int OrdersResetId = 102;
        public const int WeeklyEventId = 103;
        public const int MonthlyEventId = 104;

        private sealed class Candidate
        {
            public PlannedNotification Note;
            /// <summary>True for a reminder that something is ready (it may move later). False for a deadline
            /// (it may only move earlier, and never past Deadline).</summary>
            public bool Ready;
            public long Deadline;
        }

        public static List<PlannedNotification> Plan(GameSession session, long nowUnix, int utcOffsetMinutes, NotificationPrefs prefs)
        {
            return Plan(session, nowUnix, utcOffsetMinutes, prefs, new NotificationRules());
        }

        public static List<PlannedNotification> Plan(GameSession session, long nowUnix, int utcOffsetMinutes,
                                                     NotificationPrefs prefs, NotificationRules rules)
        {
            List<PlannedNotification> plan = new List<PlannedNotification>();
            if (session == null || session.Profile == null || prefs == null || rules == null || !prefs.Enabled) return plan;
            // Not before the first stage is cleared: the first session teaches the game, and asking for the
            // notification permission belongs after it.
            if (session.Profile.ClearedStageIds.Count == 0) return plan;

            long offset = (long)utcOffsetMinutes * 60L;
            List<Candidate> candidates = new List<Candidate>();
            if (prefs.Events) AddEventEnding(session, nowUnix, rules, LiveEventSlot.Monthly, candidates);
            if (prefs.Events) AddEventEnding(session, nowUnix, rules, LiveEventSlot.Weekly, candidates);
            if (prefs.IdleChest) AddIdleChest(session, nowUnix, candidates);
            if (prefs.Orders) AddOrdersReset(session, nowUnix, candidates);

            long earliest = nowUnix + rules.MinLeadSeconds;
            long latest = nowUnix + rules.HorizonSeconds;
            for (int c = 0; c < candidates.Count; c++)
            {
                Candidate cand = candidates[c];
                long t = cand.Note.FireUnix;
                if (cand.Ready && t < earliest) t = earliest;
                bool placed = false;
                for (int step = 0; step < 8; step++)
                {
                    t = OutOfQuiet(t, offset, rules, cand.Ready);
                    if (t < earliest || t > latest) break;
                    if (!cand.Ready && t >= cand.Deadline) break;
                    PlannedNotification clash = Clash(plan, t, rules.MinGapSeconds);
                    if (clash == null) { placed = true; break; }
                    t = cand.Ready ? clash.FireUnix + rules.MinGapSeconds : clash.FireUnix - rules.MinGapSeconds;
                }
                if (!placed) continue;
                if (OnLocalDay(plan, t, offset) >= rules.MaxPerLocalDay) continue;
                cand.Note.FireUnix = t;
                plan.Add(cand.Note);
            }
            plan.Sort((a, b) => a.FireUnix != b.FireUnix ? a.FireUnix.CompareTo(b.FireUnix) : a.Id.CompareTo(b.Id));
            return plan;
        }

        // ------------------------------------------------------------------ the three topics

        private static void AddIdleChest(GameSession session, long nowUnix, List<Candidate> into)
        {
            // Asked through the idle chest's own door, the way every screen asks (DailyLoop.Unlocked's rule).
            List<FeatureEntryPoint> doors = FeatureEntryPoints.Of(Feature.Idle);
            if (doors.Count > 0 && !FeatureEntryPoints.Allows(session.Content, session.Profile, doors[0].Id)) return;
            long cap = (long)Math.Round(session.Balance.IdleCapHours * 3600.0);
            if (cap <= 0) return;
            long full = session.Profile.LastIdleClaimUnix + cap;
            // Already full while the player is here: they can see it, so there is nothing to remind them of.
            if (full <= nowUnix) return;
            into.Add(Ready(NotificationKind.IdleChestFull, IdleChestId, full,
                           "Your chest is full",
                           "Your heroes have filled the idle chest. Collect it so they keep earning."));
        }

        private static void AddOrdersReset(GameSession session, long nowUnix, List<Candidate> into)
        {
            if (!DailyLoop.Active(session)) return;
            // Only the next turnover. A player who does not come back is not reminded every morning.
            long next = session.DayStartUnix(nowUnix) + 86400L;
            into.Add(Ready(NotificationKind.OrdersReset, OrdersResetId, next,
                           "New orders are posted",
                           "A fresh set of daily orders is waiting on the board."));
        }

        private static void AddEventEnding(GameSession session, long nowUnix, NotificationRules rules, LiveEventSlot slot, List<Candidate> into)
        {
            if (!LiveEvents.Active(session)) return;
            LiveEventView v = LiveEvents.View(session, slot, nowUnix);
            if (!v.Active) return;
            bool tookPart = v.Points > 0;
            for (int i = 0; i < v.Quests.Count && !tookPart; i++) tookPart = v.Quests[i].Progress > 0;
            if (!tookPart) return;
            bool freeLeft = v.AnythingToClaim;
            for (int i = 0; i < v.Tiers.Count && !freeLeft; i++) freeLeft = v.Tiers[i].HasFree && !v.Tiers[i].FreeClaimed;
            for (int i = 0; i < v.Quests.Count && !freeLeft; i++) freeLeft = !v.Quests[i].Claimed;
            if (!freeLeft) return;

            long end = LiveEvents.PeriodEndUnix(session, slot, LiveEvents.PeriodOf(session, slot, nowUnix));
            long lead = slot == LiveEventSlot.Weekly ? rules.WeeklyEventLeadSeconds : rules.MonthlyEventLeadSeconds;
            long hours = Math.Max(1L, lead / 3600L);
            string body = v.AnythingToClaim
                ? "You have rewards waiting in " + v.Name + ". It ends in about " + hours + " hours."
                : v.Name + " ends in about " + hours + " hours. There is still time to earn its rewards.";
            Candidate c = new Candidate();
            c.Note = Note(slot == LiveEventSlot.Weekly ? NotificationKind.EventEndingWeekly : NotificationKind.EventEndingMonthly,
                          slot == LiveEventSlot.Weekly ? WeeklyEventId : MonthlyEventId, end - lead, v.Name + " ends soon", body);
            c.Ready = false;
            c.Deadline = end;
            into.Add(c);
        }

        // ------------------------------------------------------------------ the caps

        private static Candidate Ready(NotificationKind kind, int id, long fire, string title, string body)
        {
            Candidate c = new Candidate();
            c.Note = Note(kind, id, fire, title, body);
            c.Ready = true;
            c.Deadline = long.MaxValue;
            return c;
        }

        private static PlannedNotification Note(NotificationKind kind, int id, long fire, string title, string body)
        {
            PlannedNotification n = new PlannedNotification();
            n.Kind = kind;
            n.Id = id;
            n.FireUnix = fire;
            n.Title = title;
            n.Body = body;
            return n;
        }

        private static PlannedNotification Clash(List<PlannedNotification> plan, long t, long gap)
        {
            for (int i = 0; i < plan.Count; i++)
                if (Math.Abs(plan[i].FireUnix - t) < gap) return plan[i];
            return null;
        }

        private static int OnLocalDay(List<PlannedNotification> plan, long t, long offset)
        {
            long day = LocalDay(t, offset);
            int n = 0;
            for (int i = 0; i < plan.Count; i++) if (LocalDay(plan[i].FireUnix, offset) == day) n++;
            return n;
        }

        /// <summary>The local calendar day a moment falls on, as whole days since the epoch.</summary>
        public static long LocalDay(long unix, long offsetSeconds)
        {
            return FloorDiv(unix + offsetSeconds, 86400L);
        }

        /// <summary>Minutes after local midnight.</summary>
        public static int LocalMinute(long unix, long offsetSeconds)
        {
            long local = unix + offsetSeconds;
            return (int)((local - FloorDiv(local, 86400L) * 86400L) / 60L);
        }

        public static bool IsQuiet(int minute, NotificationRules rules)
        {
            int s = rules.QuietStartMinute, e = rules.QuietEndMinute;
            if (s == e) return false;
            return s < e ? (minute >= s && minute < e) : (minute >= s || minute < e);
        }

        /// <summary>A moment outside quiet hours: unchanged when it already is, else the next end of quiet hours
        /// (later) or the evening margin before the quiet hours it fell in (earlier).</summary>
        private static long OutOfQuiet(long t, long offset, NotificationRules rules, bool later)
        {
            if (!IsQuiet(LocalMinute(t, offset), rules)) return t;
            long localMidnight = FloorDiv(t + offset, 86400L) * 86400L - offset;
            if (later)
            {
                long end = localMidnight + rules.QuietEndMinute * 60L;
                if (end <= t) end += 86400L;
                return end;
            }
            long start = localMidnight + rules.QuietStartMinute * 60L - rules.EveningMarginSeconds;
            if (start >= t) start -= 86400L;
            return start;
        }

        private static long FloorDiv(long a, long b)
        {
            long q = a / b;
            return (a % b != 0 && ((a < 0) != (b < 0))) ? q - 1 : q;
        }
    }
}
