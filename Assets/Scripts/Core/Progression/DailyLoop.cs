using System;
using System.Collections.Generic;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.Core.Progression
{
    /// <summary>What one claim paid. Failure carries a sentence and pays nothing.</summary>
    public sealed class LoopPayout
    {
        public bool Success;
        public string Message = "";
        public double Hours;
        public int Gold;
        public int Shards;
    }

    /// <summary>One order as the screen draws it. Every field is read off DailyLoop.Board; the
    /// screen decides nothing.</summary>
    public sealed class OrderView
    {
        public string Id = "";
        public string Line = "";
        public int Progress;
        public int Target = 1;
        public bool Done;
        public bool Claimed;
        public bool CanClaim;
        public double Hours;
        public int Gold;
        public int Shards;
    }

    /// <summary>One calendar day as the screen draws it.</summary>
    public sealed class CalendarView
    {
        /// <summary>1 to 7.</summary>
        public int Day;
        public double Hours;
        public string Label = "";
        public int Gold;
        public int Shards;
        /// <summary>Already paid in this lap of the calendar.</summary>
        public bool Claimed;
        /// <summary>The reward the next claim pays.</summary>
        public bool Next;
    }

    /// <summary>The whole loop at one moment, for the screen to read.</summary>
    public sealed class LoopBoard
    {
        public bool Active;

        /// <summary>Seconds until the reward day turns over and the board is replaced.</summary>
        public long ResetInSeconds;

        public readonly List<OrderView> Orders = new List<OrderView>();
        public bool AllClaimed;

        public double ChestHours;
        public int ChestGold;
        public int ChestShards;
        public bool ChestClaimed;
        public bool ChestCanClaim;

        public int WeekDays;
        public int WeekRequired;
        public double WeeklyHours;
        public int WeeklyGold;
        public int WeeklyShards;
        public bool WeeklyClaimed;
        public bool WeeklyCanClaim;

        public readonly List<CalendarView> Calendar = new List<CalendarView>();
        public bool CalendarCanClaim;

        /// <summary>True when something on the board can be claimed right now.</summary>
        public bool AnythingToClaim
        {
            get
            {
                if (ChestCanClaim || WeeklyCanClaim || CalendarCanClaim) return true;
                for (int i = 0; i < Orders.Count; i++) if (Orders[i].CanClaim) return true;
                return false;
            }
        }
    }

    /// <summary>
    /// THE DAILY LOOP'S RULES (DESIGN-DAILY-LOOP.md): the day's orders, their progress, the daily
    /// and weekly chests and the login calendar. Pure rules over a GameSession's profile and
    /// content; the screen only calls these and reads LoopBoard.
    ///
    /// ONE CLOCK. The day is GameSession.DayStartUnix, the reward day the repeat cap already
    /// uses, and every stamp the loop keeps (DailyLoopState) only moves forward, so a device clock
    /// wound back reopens nothing.
    ///
    /// ONE FORMULA. Every reward is hours of idle income at the player's current frontier, paid by
    /// GameSession.IdleYield, the function the idle chest itself is paid by.
    ///
    /// OFF MEANS INERT. With DailyLoopSwitch off, or with no usable orders.json, every call here
    /// returns at once and writes nothing to the profile.
    /// </summary>
    public static class DailyLoop
    {
        public static bool Active(GameSession session)
        {
            return DailyLoopSwitch.Enabled && session != null && session.Content.Orders.IsUsable;
        }

        // ------------------------------------------------------------------ days and weeks

        /// <summary>Whole reward days since the epoch's first reward day. Exact because a day start
        /// is always the reset hour past a midnight.</summary>
        public static long DayNumber(GameSession session, long dayStart)
        {
            long offset = (long)session.Balance.DailyResetHourUtc * 3600L;
            return FloorDiv(dayStart - offset, 86400L);
        }

        /// <summary>
        /// The week a reward day belongs to, as a positive index that only grows. A week runs
        /// Monday to Sunday by reward day: day 0 was a Thursday, so the shift of 3 puts the
        /// boundary on a Monday.
        /// </summary>
        public static long WeekOf(GameSession session, long dayStart)
        {
            return FloorDiv(DayNumber(session, dayStart) + 3L, 7L) + 1L;
        }

        private static long FloorDiv(long a, long b)
        {
            long q = a / b;
            return (a % b != 0 && ((a < 0) != (b < 0))) ? q - 1 : q;
        }

        // ------------------------------------------------------------------ the board

        /// <summary>
        /// The orders a reward day carries: one entry of each slot, chosen by the day alone, so the
        /// same day is the same board everywhere. An entry whose feature is still locked is not
        /// eligible; a slot with no eligible entry falls back to its fallback entries, and a slot
        /// with none of those is left out. Reads the profile for nothing but unlocks.
        /// </summary>
        public static List<OrderDef> BuildBoard(GameSession session, long dayStart)
        {
            List<OrderDef> board = new List<OrderDef>();
            OrdersConfig cfg = session.Content.Orders;
            List<int> slots = new List<int>();
            for (int i = 0; i < cfg.Orders.Count; i++)
                if (!slots.Contains(cfg.Orders[i].Slot)) slots.Add(cfg.Orders[i].Slot);
            slots.Sort();

            long day = DayNumber(session, dayStart);
            for (int s = 0; s < slots.Count; s++)
            {
                List<OrderDef> eligible = new List<OrderDef>();
                List<OrderDef> fallback = new List<OrderDef>();
                for (int i = 0; i < cfg.Orders.Count; i++)
                {
                    OrderDef d = cfg.Orders[i];
                    if (d.Slot != slots[s] || !Unlocked(session, d)) continue;
                    if (d.Fallback) fallback.Add(d); else eligible.Add(d);
                }
                List<OrderDef> pool = eligible.Count > 0 ? eligible : fallback;
                if (pool.Count == 0) continue;
                int seed = unchecked((int)(day * 2654435761L + slots[s] * 40503L + 12345L));
                board.Add(pool[new DeterministicRng(seed).Range(0, pool.Count)]);
            }
            return board;
        }

        /// <summary>True when at least one feature the entry requires is open, or it requires none.
        /// A feature is asked through its first door in FeatureEntryPoints, the way every screen
        /// asks, so the one unlock table stays the only thing that decides.</summary>
        public static bool Unlocked(GameSession session, OrderDef def)
        {
            if (def.Requires.Count == 0) return true;
            for (int i = 0; i < def.Requires.Count; i++)
            {
                List<FeatureEntryPoint> doors = FeatureEntryPoints.Of(def.Requires[i]);
                if (doors.Count == 0 || FeatureEntryPoints.Allows(session.Content, session.Profile, doors[0].Id)) return true;
            }
            return false;
        }

        /// <summary>
        /// Moves the loop to the reward day nowUnix falls in, and returns true when that changed
        /// anything. A later day gets a fresh board and an unopened chest, and a later week gets
        /// fresh weekly counters. The same day or an earlier one (a clock wound back) changes
        /// nothing at all.
        /// </summary>
        public static bool Roll(GameSession session, long nowUnix)
        {
            if (!Active(session)) return false;
            DailyLoopState st = session.Profile.DailyLoop;
            long start = session.DayStartUnix(nowUnix);
            if (start <= st.DayUnix) return false;

            st.DayUnix = start;
            st.ChestClaimed = false;
            st.Orders.Clear();
            List<OrderDef> board = BuildBoard(session, start);
            for (int i = 0; i < board.Count; i++)
            {
                OrderProgress p = new OrderProgress();
                p.Id = board[i].Id;
                st.Orders.Add(p);
            }

            long week = WeekOf(session, start);
            if (week > st.WeekIndex)
            {
                st.WeekIndex = week;
                st.WeekDays = 0;
                st.WeekChestClaimed = false;
            }
            return true;
        }

        // ------------------------------------------------------------------ progress

        /// <summary>
        /// Counts amount of kind toward today's orders of that kind, capped at each target. Called
        /// by GameSession at the moment the thing happens (a wave falls, a stage is won, a level
        /// is bought, a summon lands, an arena fight is fought), never by a screen.
        /// </summary>
        public static void Record(GameSession session, OrderKind kind, int amount, long nowUnix)
        {
            if (!Active(session) || amount <= 0) return;
            Roll(session, nowUnix);
            DailyLoopState st = session.Profile.DailyLoop;
            for (int i = 0; i < st.Orders.Count; i++)
            {
                OrderProgress p = st.Orders[i];
                OrderDef d = session.Content.Orders.Order(p.Id);
                if (d == null || d.Kind != kind || p.Claimed) continue;
                p.Progress = (int)Math.Min((long)d.Target, (long)p.Progress + amount);
            }
        }

        // ------------------------------------------------------------------ claims

        /// <summary>The idle income hours buy at the player's frontier right now, without paying it.</summary>
        public static LoopPayout Preview(GameSession session, double hours)
        {
            LoopPayout payout = new LoopPayout();
            payout.Hours = hours;
            IdleReport yield = session.IdleYield((long)Math.Round(hours * 3600.0));
            payout.Gold = yield.Gold;
            payout.Shards = yield.Shards;
            return payout;
        }

        private static LoopPayout Pay(GameSession session, double hours)
        {
            LoopPayout payout = Preview(session, hours);
            session.Profile.Gold += payout.Gold;
            session.Profile.Shards += payout.Shards;
            payout.Success = true;
            return payout;
        }

        private static LoopPayout Refuse(string message)
        {
            LoopPayout payout = new LoopPayout();
            payout.Message = message;
            return payout;
        }

        /// <summary>Pays one order, once. Refuses an order that is not on today's board, is not
        /// finished, or has been paid. Claiming the last order of the board counts the day toward
        /// the week.</summary>
        public static LoopPayout ClaimOrder(GameSession session, string orderId, long nowUnix)
        {
            if (!Active(session)) return Refuse("Orders are not open.");
            Roll(session, nowUnix);
            DailyLoopState st = session.Profile.DailyLoop;
            OrderProgress p = st.Find(orderId);
            OrderDef d = session.Content.Orders.Order(orderId);
            if (p == null || d == null) return Refuse("That order is not on today's board.");
            if (p.Claimed) return Refuse("Already claimed.");
            if (p.Progress < d.Target) return Refuse("Not finished yet.");

            p.Claimed = true;
            LoopPayout payout = Pay(session, d.Hours);
            if (AllClaimed(session)) st.WeekDays = Math.Min(7, st.WeekDays + 1);
            WayfarersRoad.OnClaim(session, PassPointSource.Order, nowUnix);   // the pass counts the claim
            LiveEvents.Record(session, EventQuestKind.ClaimOrder, 1, nowUnix);   // and so do the live events' quests
            return payout;
        }

        /// <summary>Every order on the board whose definition still exists is claimed, and there is
        /// at least one. An order whose definition has left orders.json cannot be claimed, so it
        /// must not hold the chest shut.</summary>
        public static bool AllClaimed(GameSession session)
        {
            DailyLoopState st = session.Profile.DailyLoop;
            int counted = 0;
            for (int i = 0; i < st.Orders.Count; i++)
            {
                if (session.Content.Orders.Order(st.Orders[i].Id) == null) continue;
                counted++;
                if (!st.Orders[i].Claimed) return false;
            }
            return counted > 0;
        }

        public static LoopPayout ClaimDailyChest(GameSession session, long nowUnix)
        {
            if (!Active(session)) return Refuse("Orders are not open.");
            Roll(session, nowUnix);
            DailyLoopState st = session.Profile.DailyLoop;
            if (st.ChestClaimed) return Refuse("Already opened.");
            if (!AllClaimed(session)) return Refuse("Claim every order first.");
            st.ChestClaimed = true;
            LoopPayout payout = Pay(session, session.Content.Orders.DailyChestHours);
            WayfarersRoad.OnClaim(session, PassPointSource.DailyChest, nowUnix);
            return payout;
        }

        public static LoopPayout ClaimWeeklyChest(GameSession session, long nowUnix)
        {
            if (!Active(session)) return Refuse("Orders are not open.");
            Roll(session, nowUnix);
            DailyLoopState st = session.Profile.DailyLoop;
            if (st.WeekChestClaimed) return Refuse("Already opened.");
            if (st.WeekDays < session.Content.Orders.WeeklyDaysRequired) return Refuse("Not enough days yet.");
            st.WeekChestClaimed = true;
            LoopPayout payout = Pay(session, session.Content.Orders.WeeklyChestHours);
            WayfarersRoad.OnClaim(session, PassPointSource.WeeklyChest, nowUnix);
            return payout;
        }

        /// <summary>True when a calendar reward can be taken: the reward day of nowUnix is later
        /// than the day of the last claim.</summary>
        public static bool CanClaimCalendar(GameSession session, long nowUnix)
        {
            if (!Active(session)) return false;
            return session.DayStartUnix(nowUnix) > session.Profile.DailyLoop.CalendarDayUnix;
        }

        /// <summary>Pays the next day of the calendar, at most once per reward day. The calendar
        /// repeats: after day 7 the next claim is day 1. A missed day delays the next reward and
        /// skips none.</summary>
        public static LoopPayout ClaimCalendar(GameSession session, long nowUnix)
        {
            if (!Active(session)) return Refuse("Orders are not open.");
            DailyLoopState st = session.Profile.DailyLoop;
            long start = session.DayStartUnix(nowUnix);
            if (start <= st.CalendarDayUnix) return Refuse("Come back tomorrow.");
            CalendarDayDef day = session.Content.Orders.Calendar[st.CalendarNext];
            st.CalendarDayUnix = start;
            st.CalendarNext = (st.CalendarNext + 1) % OrdersConfig.CalendarLength;
            return Pay(session, day.Hours);
        }

        // ------------------------------------------------------------------ what the screen reads

        /// <summary>The loop as of nowUnix, rolled to that day first. Every number the screen
        /// prints is on it; previews are priced at the frontier as it stands.</summary>
        public static LoopBoard Board(GameSession session, long nowUnix)
        {
            LoopBoard b = new LoopBoard();
            if (!Active(session)) return b;
            Roll(session, nowUnix);
            b.Active = true;
            DailyLoopState st = session.Profile.DailyLoop;
            OrdersConfig cfg = session.Content.Orders;
            b.ResetInSeconds = Math.Max(0L, session.DayStartUnix(nowUnix) + 86400L - nowUnix);

            for (int i = 0; i < st.Orders.Count; i++)
            {
                OrderDef d = cfg.Order(st.Orders[i].Id);
                if (d == null) continue;
                OrderView v = new OrderView();
                v.Id = d.Id;
                v.Line = d.Line;
                v.Target = d.Target;
                v.Progress = Math.Min(d.Target, st.Orders[i].Progress);
                v.Done = v.Progress >= d.Target;
                v.Claimed = st.Orders[i].Claimed;
                v.CanClaim = v.Done && !v.Claimed;
                v.Hours = d.Hours;
                LoopPayout pay = Preview(session, d.Hours);
                v.Gold = pay.Gold;
                v.Shards = pay.Shards;
                b.Orders.Add(v);
            }
            b.AllClaimed = AllClaimed(session);

            b.ChestHours = cfg.DailyChestHours;
            LoopPayout chest = Preview(session, cfg.DailyChestHours);
            b.ChestGold = chest.Gold;
            b.ChestShards = chest.Shards;
            b.ChestClaimed = st.ChestClaimed;
            b.ChestCanClaim = b.AllClaimed && !st.ChestClaimed;

            b.WeekDays = st.WeekDays;
            b.WeekRequired = cfg.WeeklyDaysRequired;
            b.WeeklyHours = cfg.WeeklyChestHours;
            LoopPayout weekly = Preview(session, cfg.WeeklyChestHours);
            b.WeeklyGold = weekly.Gold;
            b.WeeklyShards = weekly.Shards;
            b.WeeklyClaimed = st.WeekChestClaimed;
            b.WeeklyCanClaim = !st.WeekChestClaimed && st.WeekDays >= cfg.WeeklyDaysRequired;

            for (int i = 0; i < cfg.Calendar.Count; i++)
            {
                CalendarView c = new CalendarView();
                c.Day = i + 1;
                c.Hours = cfg.Calendar[i].Hours;
                c.Label = cfg.Calendar[i].Label;
                LoopPayout pay = Preview(session, c.Hours);
                c.Gold = pay.Gold;
                c.Shards = pay.Shards;
                c.Claimed = i < st.CalendarNext;
                c.Next = i == st.CalendarNext;
                b.Calendar.Add(c);
            }
            b.CalendarCanClaim = CanClaimCalendar(session, nowUnix);
            return b;
        }
    }
}
