using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Save;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE NOTIFICATION PLANNER (Docs/DESIGN-NOTIFICATIONS.md): when each local notification fires, given a
    /// save. NotificationPlanner is engine-free Core, so the harness runs it. What is proved: each topic fires
    /// when the design says, quiet hours and the caps hold for every plan in a sweep, a deadline never moves
    /// past its deadline, the plan writes nothing to the profile, and no notification names anything sold.
    /// </summary>
    internal static partial class Program
    {
        private static void CheckNotifications(ContentDatabase shipped, string contentDir)
        {
            Line("");
            Line("THE NOTIFICATION PLANNER (Docs/DESIGN-NOTIFICATIONS.md: idle chest full, orders reset, event ending; quiet hours and caps)");
            bool loopWas = DailyLoopSwitch.Enabled, liveWas = LiveEventsSwitch.Enabled;
            string liveDirWas = _liveContentDir;
            try
            {
                _liveContentDir = contentDir;
                ContentDatabase d = LiveDb(LiveTestJson(shipped));
                CheckNotifyGates(d);
                CheckNotifyIdle(d);
                CheckNotifyOrders(d);
                CheckNotifyEvents(d);
                CheckNotifyPriority(d);
                CheckNotifySweep(d);
            }
            finally
            {
                DailyLoopSwitch.Enabled = loopWas;
                LiveEventsSwitch.Enabled = liveWas;
                _liveContentDir = liveDirWas;
            }
            Check(DailyLoopSwitch.Enabled == loopWas && LiveEventsSwitch.Enabled == liveWas,
                  "the switches are put back as the run found them");
        }

        /// <summary>00:00 UTC of a day well inside the test event calendar.</summary>
        private static long NotifyMidnight(ContentDatabase d)
        {
            return LiveMonthStart(d, 10) - (long)d.Balance.DailyResetHourUtc * 3600L;
        }

        private static List<PlannedNotification> NotifyPlan(GameSession s, long now, int offsetMinutes)
        {
            return NotificationPlanner.Plan(s, now, offsetMinutes, new NotificationPrefs());
        }

        private static PlannedNotification NotifyOf(List<PlannedNotification> plan, NotificationKind kind)
        {
            for (int i = 0; i < plan.Count; i++) if (plan[i].Kind == kind) return plan[i];
            return null;
        }

        private static string NotifyDescribe(List<PlannedNotification> plan, long now)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < plan.Count; i++) parts.Add(plan[i].Kind + "@+" + ((plan[i].FireUnix - now) / 60L) + "m");
            return parts.Count == 0 ? "nothing" : string.Join(", ", parts.ToArray());
        }

        private static void CheckNotifyGates(ContentDatabase d)
        {
            DailyLoopSwitch.Enabled = true;
            LiveEventsSwitch.Enabled = true;
            long now = NotifyMidnight(d) + 8L * 3600L;
            GameSession fresh = LiveClearedThrough(d, null);
            fresh.Profile.LastIdleClaimUnix = now;
            Check(NotifyPlan(fresh, now, 0).Count == 0,
                  "a profile that has cleared no stage gets no notification (the first session comes first)");

            GameSession s = LiveClearedThrough(d, "ch3_1");
            s.Profile.LastIdleClaimUnix = now;
            NotificationPrefs off = new NotificationPrefs();
            off.Enabled = false;
            Check(NotificationPlanner.Plan(s, now, 0, off).Count == 0, "the master switch off plans nothing");

            NotificationPrefs noIdle = new NotificationPrefs();
            noIdle.IdleChest = false;
            NotificationPrefs noOrders = new NotificationPrefs();
            noOrders.Orders = false;
            Check(NotifyOf(NotificationPlanner.Plan(s, now, 0, noIdle), NotificationKind.IdleChestFull) == null
                  && NotifyOf(NotificationPlanner.Plan(s, now, 0, noOrders), NotificationKind.OrdersReset) == null
                  && NotifyOf(NotifyPlan(s, now, 0), NotificationKind.IdleChestFull) != null
                  && NotifyOf(NotifyPlan(s, now, 0), NotificationKind.OrdersReset) != null,
                  "each topic's own switch removes that topic and only that topic");
        }

        private static void CheckNotifyIdle(ContentDatabase d)
        {
            DailyLoopSwitch.Enabled = false;
            LiveEventsSwitch.Enabled = false;
            long midnight = NotifyMidnight(d);
            long cap = (long)Math.Round(d.Balance.IdleCapHours * 3600.0);
            GameSession s = LiveClearedThrough(d, "ch3_1");

            long now = midnight + 8L * 3600L;
            s.Profile.LastIdleClaimUnix = now - 3600L;
            List<PlannedNotification> plan = NotifyPlan(s, now, 0);
            PlannedNotification idle = NotifyOf(plan, NotificationKind.IdleChestFull);
            Check(plan.Count == 1 && idle != null && idle.FireUnix == s.Profile.LastIdleClaimUnix + cap && idle.Id == NotificationPlanner.IdleChestId,
                  "IDLE: the chest fires the moment it reaches its cap (last claim + " + d.Balance.IdleCapHours + " h), with its own id: " + NotifyDescribe(plan, now));

            now = midnight + 20L * 3600L;
            s.Profile.LastIdleClaimUnix = now;
            plan = NotifyPlan(s, now, 0);
            idle = NotifyOf(plan, NotificationKind.IdleChestFull);
            long full = now + cap;
            bool quiet = NotificationPlanner.IsQuiet(NotificationPlanner.LocalMinute(full, 0), new NotificationRules());
            Check(quiet && idle != null && NotificationPlanner.LocalMinute(idle.FireUnix, 0) == 9 * 60 && idle.FireUnix > full && idle.FireUnix - full < 86400L,
                  "IDLE IN QUIET HOURS: a chest that fills at night is announced at the end of quiet hours, 09:00 local, not at night: " + NotifyDescribe(plan, now));

            s.Profile.LastIdleClaimUnix = now - cap - 60L;
            Check(NotifyOf(NotifyPlan(s, now, 0), NotificationKind.IdleChestFull) == null,
                  "IDLE ALREADY FULL: a chest that is full while the player is in the game is not announced");
        }

        private static void CheckNotifyOrders(ContentDatabase d)
        {
            LiveEventsSwitch.Enabled = false;
            long midnight = NotifyMidnight(d);
            long now = midnight + 14L * 3600L;
            GameSession s = LiveClearedThrough(d, "ch3_1");
            s.Profile.LastIdleClaimUnix = now;

            DailyLoopSwitch.Enabled = false;
            Check(NotifyOf(NotifyPlan(s, now, 0), NotificationKind.OrdersReset) == null,
                  "ORDERS: with the daily loop off (the shipped default) there is no orders notification");

            DailyLoopSwitch.Enabled = true;
            long reset = s.DayStartUnix(now) + 86400L;
            // The orders alone, so the chest of the same save cannot take the slot first (the priority check does that).
            NotificationPrefs ordersOnly = new NotificationPrefs();
            ordersOnly.IdleChest = false;
            PlannedNotification manila = NotifyOf(NotificationPlanner.Plan(s, now, 8 * 60, ordersOnly), NotificationKind.OrdersReset);
            Check(manila != null && manila.FireUnix == reset,
                  "ORDERS ON, UTC+8: the reset at " + d.Balance.DailyResetHourUtc + ":00 UTC is noon local, so it fires at the reset itself");
            PlannedNotification brussels = NotifyOf(NotificationPlanner.Plan(s, now, 60, ordersOnly), NotificationKind.OrdersReset);
            Check(brussels != null && brussels.FireUnix > reset && NotificationPlanner.LocalMinute(brussels.FireUnix, 3600L) == 9 * 60,
                  "ORDERS ON, UTC+1: the reset falls before dawn, so it fires at 09:00 local");
            Check(CountKind(NotifyPlan(s, now, 0), NotificationKind.OrdersReset) <= 1,
                  "only the next turnover is announced, never a string of mornings");
        }

        private static int CountKind(List<PlannedNotification> plan, NotificationKind kind)
        {
            int n = 0;
            for (int i = 0; i < plan.Count; i++) if (plan[i].Kind == kind) n++;
            return n;
        }

        private static void CheckNotifyEvents(ContentDatabase d)
        {
            DailyLoopSwitch.Enabled = false;
            LiveEventsSwitch.Enabled = true;
            GameSession s = LiveClearedThrough(d, "ch3_1");
            long probe = NotifyMidnight(d) + 12L * 3600L;
            long end = LiveEvents.PeriodEndUnix(s, LiveEventSlot.Weekly, LiveEvents.PeriodOf(s, LiveEventSlot.Weekly, probe));
            long now = end - 20L * 3600L;
            s.Profile.LastIdleClaimUnix = now;
            NotificationPrefs eventsOnly = new NotificationPrefs();
            eventsOnly.IdleChest = false;

            Check(NotifyOf(NotificationPlanner.Plan(s, now, 0, eventsOnly), NotificationKind.EventEndingWeekly) == null,
                  "EVENT: a player who has not taken part in the week's event is not reminded that it ends");

            LiveEvents.Record(s, EventQuestKind.Stage, 1, now - 3600L);
            List<PlannedNotification> plan = NotificationPlanner.Plan(s, now, 0, eventsOnly);
            PlannedNotification week = NotifyOf(plan, NotificationKind.EventEndingWeekly);
            long lead = new NotificationRules().WeeklyEventLeadSeconds;
            Check(week != null && week.FireUnix == end - lead && week.Id == NotificationPlanner.WeeklyEventId,
                  "EVENT: once they have, it fires " + (lead / 3600L) + " h before the week ends: " + NotifyDescribe(plan, now));
            Check(NotifyOf(plan, NotificationKind.EventEndingMonthly) == null,
                  "and the monthly event, which ends past the 48 hour horizon, is not planned yet");

            List<PlannedNotification> late = NotificationPlanner.Plan(s, now, 3 * 60, eventsOnly);
            PlannedNotification evening = NotifyOf(late, NotificationKind.EventEndingWeekly);
            Check(evening != null && evening.FireUnix < end - lead && evening.FireUnix < end
                  && !NotificationPlanner.IsQuiet(NotificationPlanner.LocalMinute(evening.FireUnix, 3 * 3600L), new NotificationRules()),
                  "EVENT IN QUIET HOURS (UTC+3): the deadline moves EARLIER, to the evening before, never past the end: " + NotifyDescribe(late, now));

            List<PlannedNotification> tooLate = NotificationPlanner.Plan(s, end - 600L, 0, eventsOnly);
            Check(NotifyOf(tooLate, NotificationKind.EventEndingWeekly) == null,
                  "EVENT: ten minutes before the end there is no time left to remind, so nothing is planned");

            LiveEventsSwitch.Enabled = false;
            Check(NotifyOf(NotificationPlanner.Plan(s, now, 0, eventsOnly), NotificationKind.EventEndingWeekly) == null,
                  "EVENT: with the live events off there is no event notification");
            LiveEventsSwitch.Enabled = true;
        }

        private static void CheckNotifyPriority(ContentDatabase d)
        {
            DailyLoopSwitch.Enabled = true;
            LiveEventsSwitch.Enabled = false;
            GameSession s = LiveClearedThrough(d, "ch3_1");
            long reset = s.DayStartUnix(NotifyMidnight(d) + 13L * 3600L) + 86400L;
            long cap = (long)Math.Round(d.Balance.IdleCapHours * 3600.0);
            // UTC+8: the reset is noon local. The chest is claimed now and fills at that same minute.
            long now = reset - cap;
            s.Profile.LastIdleClaimUnix = now;
            List<PlannedNotification> plan = NotifyPlan(s, now, 8 * 60);
            PlannedNotification idle = NotifyOf(plan, NotificationKind.IdleChestFull);
            PlannedNotification orders = NotifyOf(plan, NotificationKind.OrdersReset);
            long gap = new NotificationRules().MinGapSeconds;
            Check(idle != null && orders != null && idle.FireUnix == reset && orders.FireUnix - idle.FireUnix >= gap,
                  "PRIORITY: when the chest and the orders land on the same minute the chest keeps it and the orders move " + (gap / 3600L) + " h later: " + NotifyDescribe(plan, now));
        }

        /// <summary>Every invariant, over many saves, clocks and time zones.</summary>
        private static void CheckNotifySweep(ContentDatabase d)
        {
            NotificationRules rules = new NotificationRules();
            long midnight = NotifyMidnight(d);
            long cap = (long)Math.Round(d.Balance.IdleCapHours * 3600.0);
            int[] offsets = { -600, -480, -300, 0, 60, 120, 330, 480, 540, 765, 840 };
            string[] forbidden = { "buy", "bought", "purchase", "paid", "offer", "pack", "price", "sale", "store", "shop", "eur", "\u20ac", "$" };
            int plans = 0, notes = 0, broken = 0, writes = 0, sold = 0, dashes = 0, nondeterministic = 0;
            string firstBroken = "";
            for (int h = 0; h < 7 * 24; h += 5)
            {
                for (int o = 0; o < offsets.Length; o++)
                {
                    for (int variant = 0; variant < 4; variant++)
                    {
                        DailyLoopSwitch.Enabled = (variant & 1) != 0;
                        LiveEventsSwitch.Enabled = true;
                        GameSession s = LiveClearedThrough(d, "ch3_1");
                        long now = midnight + h * 3600L + 17L * 60L;
                        s.Profile.LastIdleClaimUnix = now - (h * 977L) % (cap + 7200L);
                        if ((variant & 2) != 0) LiveEvents.Record(s, EventQuestKind.Stage, 1, now - 60L);
                        string before = SaveSerializer.Serialize(s.Profile);
                        List<PlannedNotification> plan = NotifyPlan(s, now, offsets[o]);
                        List<PlannedNotification> again = NotifyPlan(s, now, offsets[o]);
                        if (SaveSerializer.Serialize(s.Profile) != before) writes++;
                        if (NotifyDescribe(plan, now) != NotifyDescribe(again, now)) nondeterministic++;
                        plans++;
                        long off = offsets[o] * 60L;
                        HashSet<int> ids = new HashSet<int>();
                        Dictionary<long, int> perDay = new Dictionary<long, int>();
                        for (int i = 0; i < plan.Count; i++)
                        {
                            PlannedNotification n = plan[i];
                            notes++;
                            string why = null;
                            if (n.FireUnix < now + rules.MinLeadSeconds || n.FireUnix > now + rules.HorizonSeconds) why = "outside lead/horizon";
                            else if (NotificationPlanner.IsQuiet(NotificationPlanner.LocalMinute(n.FireUnix, off), rules)) why = "in quiet hours";
                            else if (!ids.Add(n.Id)) why = "duplicate id";
                            else if (i > 0 && n.FireUnix - plan[i - 1].FireUnix < rules.MinGapSeconds) why = "closer than the gap";
                            else if (n.Kind == NotificationKind.EventEndingWeekly || n.Kind == NotificationKind.EventEndingMonthly)
                            {
                                LiveEventSlot slot = n.Kind == NotificationKind.EventEndingWeekly ? LiveEventSlot.Weekly : LiveEventSlot.Monthly;
                                if (n.FireUnix >= LiveEvents.PeriodEndUnix(s, slot, LiveEvents.PeriodOf(s, slot, now))) why = "after its event ended";
                            }
                            long day = NotificationPlanner.LocalDay(n.FireUnix, off);
                            int c;
                            perDay.TryGetValue(day, out c);
                            perDay[day] = c + 1;
                            if (why == null && c + 1 > rules.MaxPerLocalDay) why = "over the daily cap";
                            if (why != null) { broken++; if (firstBroken.Length == 0) firstBroken = why + " (" + NotifyDescribe(plan, now) + ", offset " + offsets[o] + ")"; }
                            string words = (n.Title + " " + n.Body).ToLowerInvariant();
                            for (int f = 0; f < forbidden.Length; f++)
                                if (System.Text.RegularExpressions.Regex.IsMatch(words, "(^|[^a-z])" + System.Text.RegularExpressions.Regex.Escape(forbidden[f]))) { sold++; break; }
                            if (words.IndexOf('\u2014') >= 0 || words.IndexOf('\u2013') >= 0) dashes++;
                        }
                    }
                }
            }
            Check(broken == 0, "SWEEP: " + plans + " plans, " + notes + " notifications: none in quiet hours, none sooner than "
                  + (rules.MinLeadSeconds / 60L) + " min or past " + (rules.HorizonSeconds / 3600L) + " h, none within " + (rules.MinGapSeconds / 3600L)
                  + " h of another, none over " + rules.MaxPerLocalDay + " a local day, no deadline after its event ends"
                  + (broken > 0 ? " - BROKEN " + broken + ", first: " + firstBroken : ""));
            Check(notes > plans / 2, "and the sweep is not vacuous: " + notes + " notifications were planned over " + plans + " plans");
            Check(writes == 0 && nondeterministic == 0, "PURE: planning wrote nothing to any profile (" + writes + ") and the same inputs gave the same plan every time (" + nondeterministic + " differed)");
            Check(sold == 0, "NOTHING SOLD: no notification names a price, an offer, a pack, the store or a purchase (" + sold + " did)");
            Check(dashes == 0, "no notification text carries an em dash or an en dash (" + dashes + " did)");
        }
    }
}
