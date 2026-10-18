using System;
using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Model
{
    /// <summary>One cap counter: how many units of a cap key have been bought in the current period.</summary>
    public sealed class SinkCount
    {
        public string Key = "";
        public int Count;
    }

    /// <summary>
    /// Everything the Armory and the Archive remember (DESIGN-SINKS.md), saved from version 23
    /// under the key `sinks`, written only once it holds something. A profile that never bought
    /// anything carries none of it and serialises exactly as it did before version 23, version
    /// number included (SaveSerializer.VersionOf).
    ///
    /// Two sets of counters, one per period. The daily counters belong to the reward day named by
    /// DayUnix and the monthly counters to the month named by MonthKey; when a purchase finds a
    /// later day or month the set is emptied and restamped (SinkShop.Buy). THE STAMPS ONLY MOVE
    /// FORWARD, like DailyLoopState's: a clock wound back compares as "not a later period" and
    /// changes nothing, so it cannot reopen a cap.
    /// </summary>
    public sealed class SinksState
    {
        /// <summary>Largest count a counter may hold on read, far above any cap the game sets.</summary>
        public const int CountMax = 1000;

        /// <summary>Start of the reward day the daily counters belong to (GameSession.DayStartUnix).
        /// Zero means nothing has ever been bought.</summary>
        public long DayUnix;

        /// <summary>Units bought today, by cap key.</summary>
        public readonly List<SinkCount> Daily = new List<SinkCount>();

        /// <summary>The month the monthly counters belong to: year x 12 + month - 1 of the reward
        /// day, UTC. Zero means nothing has ever been bought.</summary>
        public long MonthKey;

        /// <summary>Units bought this month, by cap key.</summary>
        public readonly List<SinkCount> Monthly = new List<SinkCount>();

        /// <summary>True when nothing has ever been recorded, which is what Serialize leaves out.</summary>
        public bool IsEmpty
        {
            get { return DayUnix == 0 && Daily.Count == 0 && MonthKey == 0 && Monthly.Count == 0; }
        }

        public static int CountOf(List<SinkCount> list, string key)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Key == key) return list[i].Count;
            return 0;
        }

        /// <summary>Adds one to a counter, creating it at one.</summary>
        public static void Bump(List<SinkCount> list, string key)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Key != key) continue;
                list[i].Count = Math.Min(CountMax, list[i].Count + 1);
                return;
            }
            SinkCount c = new SinkCount();
            c.Key = key;
            c.Count = 1;
            list.Add(c);
        }

        public JsonValue Write()
        {
            JsonValue o = JsonValue.NewObject();
            o.Set("day", DayUnix);
            o.Set("daily", WriteCounts(Daily));
            o.Set("month", MonthKey);
            o.Set("monthly", WriteCounts(Monthly));
            return o;
        }

        private static JsonValue WriteCounts(List<SinkCount> list)
        {
            JsonValue a = JsonValue.NewArray();
            for (int i = 0; i < list.Count; i++)
            {
                JsonValue c = JsonValue.NewObject();
                c.Set("key", list[i].Key);
                c.Set("n", list[i].Count);
                a.Add(c);
            }
            return a;
        }

        /// <summary>Reads a saved block. Absent or damaged reads as empty, and every number is
        /// clamped to what the game could have written.</summary>
        public static SinksState Read(JsonValue o)
        {
            SinksState s = new SinksState();
            if (o == null || !o.IsObject) return s;
            s.DayUnix = Math.Max(0, o.GetLong("day", 0));
            ReadCounts(o.GetList("daily"), s.Daily);
            s.MonthKey = Math.Max(0, o.GetLong("month", 0));
            ReadCounts(o.GetList("monthly"), s.Monthly);
            return s;
        }

        private static void ReadCounts(List<JsonValue> source, List<SinkCount> into)
        {
            for (int i = 0; i < source.Count; i++)
            {
                string key = source[i].GetString("key", "");
                if (key.Length == 0 || CountOf(into, key) > 0) continue;
                int n = Math.Max(0, Math.Min(CountMax, source[i].GetInt("n", 0)));
                if (n == 0) continue;
                SinkCount c = new SinkCount();
                c.Key = key;
                c.Count = n;
                into.Add(c);
            }
        }
    }
}
