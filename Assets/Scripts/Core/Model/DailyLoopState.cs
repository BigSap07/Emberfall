using System;
using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Model
{
    /// <summary>One order on today's board: which order, how far along, and whether its reward
    /// has been paid.</summary>
    public sealed class OrderProgress
    {
        public string Id = "";
        public int Progress;
        public bool Claimed;
    }

    /// <summary>
    /// Everything the daily loop remembers (DESIGN-DAILY-LOOP.md), saved from version 22 under the
    /// key `dailyLoop`, written only once it holds something. A profile that never switched the
    /// loop on carries none of it and serialises exactly as it did before version 22.
    ///
    /// THE BOARD IS STORED, NOT RE-DERIVED. The day's orders are generated once, when the reward
    /// day rolls, and their ids are kept here, so retuning orders.json mid-day cannot reorder or
    /// remove an order a player is halfway through.
    ///
    /// EVERY STAMP ONLY MOVES FORWARD. DayUnix, WeekIndex and CalendarDayUnix are compared with
    /// the reward day of the clock the game is handed, and a clock wound back compares as "not a
    /// later day" and changes nothing, so it cannot reopen a board, a chest or the calendar. This
    /// is the same immunity GameSession.RollDailyReset gives the repeat cap, on the same clock.
    /// </summary>
    public sealed class DailyLoopState
    {
        /// <summary>Start of the reward day the board belongs to (GameSession.DayStartUnix). Zero
        /// means no board has ever been generated.</summary>
        public long DayUnix;

        /// <summary>Today's orders, in slot order.</summary>
        public readonly List<OrderProgress> Orders = new List<OrderProgress>();

        /// <summary>True once the daily chest has been opened for the board above.</summary>
        public bool ChestClaimed;

        /// <summary>The week the counters below belong to (DailyLoop.WeekOf). Zero means none.</summary>
        public long WeekIndex;

        /// <summary>Days of that week on which every order was claimed.</summary>
        public int WeekDays;

        /// <summary>True once the weekly chest has been opened for that week.</summary>
        public bool WeekChestClaimed;

        /// <summary>Index into the calendar (0 to 6) of the reward the NEXT claim pays.</summary>
        public int CalendarNext;

        /// <summary>Reward day of the last calendar claim. Zero means never claimed.</summary>
        public long CalendarDayUnix;

        /// <summary>True when nothing has ever been recorded, which is what Serialize leaves out.</summary>
        public bool IsEmpty
        {
            get
            {
                return DayUnix == 0 && Orders.Count == 0 && !ChestClaimed && WeekIndex == 0 && WeekDays == 0
                    && !WeekChestClaimed && CalendarNext == 0 && CalendarDayUnix == 0;
            }
        }

        public OrderProgress Find(string id)
        {
            for (int i = 0; i < Orders.Count; i++) if (Orders[i].Id == id) return Orders[i];
            return null;
        }

        public JsonValue Write()
        {
            JsonValue o = JsonValue.NewObject();
            o.Set("day", DayUnix);
            JsonValue orders = JsonValue.NewArray();
            for (int i = 0; i < Orders.Count; i++)
            {
                JsonValue oj = JsonValue.NewObject();
                oj.Set("id", Orders[i].Id);
                oj.Set("progress", Orders[i].Progress);
                oj.Set("claimed", Orders[i].Claimed);
                orders.Add(oj);
            }
            o.Set("orders", orders);
            o.Set("chestClaimed", ChestClaimed);
            o.Set("week", WeekIndex);
            o.Set("weekDays", WeekDays);
            o.Set("weekChestClaimed", WeekChestClaimed);
            o.Set("calendarNext", CalendarNext);
            o.Set("calendarDay", CalendarDayUnix);
            return o;
        }

        /// <summary>Reads a saved block. Absent or damaged reads as empty, and every number is
        /// clamped to what the game could have written.</summary>
        public static DailyLoopState Read(JsonValue o)
        {
            DailyLoopState s = new DailyLoopState();
            if (o == null || !o.IsObject) return s;
            s.DayUnix = Math.Max(0, o.GetLong("day", 0));
            List<JsonValue> orders = o.GetList("orders");
            for (int i = 0; i < orders.Count; i++)
            {
                string id = orders[i].GetString("id", "");
                if (id.Length == 0 || s.Find(id) != null) continue;
                OrderProgress p = new OrderProgress();
                p.Id = id;
                p.Progress = Math.Max(0, orders[i].GetInt("progress", 0));
                p.Claimed = orders[i].GetBool("claimed", false);
                s.Orders.Add(p);
            }
            s.ChestClaimed = o.GetBool("chestClaimed", false);
            s.WeekIndex = Math.Max(0, o.GetLong("week", 0));
            s.WeekDays = Math.Max(0, Math.Min(7, o.GetInt("weekDays", 0)));
            s.WeekChestClaimed = o.GetBool("weekChestClaimed", false);
            s.CalendarNext = Math.Max(0, Math.Min(6, o.GetInt("calendarNext", 0)));
            s.CalendarDayUnix = Math.Max(0, o.GetLong("calendarDay", 0));
            return s;
        }
    }
}
