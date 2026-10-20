using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Json;

namespace Emberfall.Core.Model
{
    /// <summary>
    /// What one live event slot remembers about the period it is in. ONE PERIOD AT A TIME: Period names the
    /// period the rest belongs to; a later period replaces everything, so unclaimed rewards lapse. THE STAMP
    /// ONLY MOVES FORWARD, like the daily loop's and Wayfarer's Road's: a clock wound back compares as "not a
    /// later period" and changes nothing.
    /// </summary>
    public sealed class LiveEventSlotState
    {
        /// <summary>The period the counters below belong to. Zero means none.</summary>
        public long Period;

        /// <summary>The event that ran in that period (live-events.json). A period whose event has left the file
        /// reads as empty.</summary>
        public string EventId = "";

        /// <summary>Event points: the quests claimed so far.</summary>
        public int Points;

        /// <summary>Progress per quest, in the event's quest order, each capped at its target.</summary>
        public readonly List<int> Progress = new List<int>();

        /// <summary>Bit i is set once quest i has been claimed.</summary>
        public int QuestMask;

        /// <summary>Bit i is set once tier i's free reward has been claimed.</summary>
        public int FreeMask;

        /// <summary>Bit i is set once tier i's paid reward has been claimed.</summary>
        public int PaidMask;

        /// <summary>The paid Event Track of this period was bought (the grant sets it).</summary>
        public bool TrackBought;

        /// <summary>The event pack of this period was bought. Once per event.</summary>
        public bool PackBought;

        public LiveEventSlotState Clone()
        {
            LiveEventSlotState c = (LiveEventSlotState)MemberwiseClone();
            c.Progress.Clear();
            c.Progress.AddRange(Progress);
            return c;
        }

        /// <summary>The same counters in a fresh object: a copy to replace this one's with (the list is readonly).</summary>
        public void CopyFrom(LiveEventSlotState o)
        {
            Period = o.Period;
            EventId = o.EventId;
            Points = o.Points;
            Progress.Clear();
            Progress.AddRange(o.Progress);
            QuestMask = o.QuestMask;
            FreeMask = o.FreeMask;
            PaidMask = o.PaidMask;
            TrackBought = o.TrackBought;
            PackBought = o.PackBought;
        }

        public void Clear()
        {
            CopyFrom(new LiveEventSlotState());
        }

        public bool IsEmpty
        {
            get { return Period == 0 && Points == 0 && QuestMask == 0 && FreeMask == 0 && PaidMask == 0 && !TrackBought && !PackBought && EventId.Length == 0; }
        }

        public int ProgressOf(int quest)
        {
            return quest >= 0 && quest < Progress.Count ? Progress[quest] : 0;
        }

        public static bool Bit(int mask, int index)
        {
            return index >= 0 && index < 30 && (mask & (1 << index)) != 0;
        }

        public JsonValue Write()
        {
            JsonValue o = JsonValue.NewObject();
            o.Set("period", Period);
            o.Set("event", EventId);
            o.Set("points", Points);
            JsonValue progress = JsonValue.NewArray();
            for (int i = 0; i < Progress.Count; i++) progress.Add(Progress[i]);
            o.Set("progress", progress);
            o.Set("quests", QuestMask);
            o.Set("free", FreeMask);
            if (PaidMask != 0) o.Set("paid", PaidMask);
            if (TrackBought) o.Set("track", true);
            if (PackBought) o.Set("pack", true);
            return o;
        }

        /// <summary>Reads a saved block. Absent or damaged reads as empty, and every number is clamped to what the
        /// game could have written.</summary>
        public static LiveEventSlotState Read(JsonValue o)
        {
            LiveEventSlotState s = new LiveEventSlotState();
            if (o == null || !o.IsObject) return s;
            s.Period = Math.Max(0, o.GetLong("period", 0));
            s.EventId = o.GetString("event", "");
            s.Points = Math.Max(0, Math.Min(100000, o.GetInt("points", 0)));
            List<JsonValue> progress = o.GetList("progress");
            for (int i = 0; i < progress.Count && i < LiveEventsConfig.MaxQuests; i++)
                s.Progress.Add(Math.Max(0, Math.Min(100000000, progress[i].AsInt(0))));
            long mask = (1L << 30) - 1L;
            s.QuestMask = (int)(Math.Max(0L, o.GetLong("quests", 0)) & mask);
            s.FreeMask = (int)(Math.Max(0L, o.GetLong("free", 0)) & mask);
            s.PaidMask = (int)(Math.Max(0L, o.GetLong("paid", 0)) & mask);
            s.TrackBought = o.GetBool("track", false);
            s.PackBought = o.GetBool("pack", false);
            return s;
        }
    }

    /// <summary>
    /// Everything the live events remember (the events section of Docs/DESIGN-STORE.md), saved from version 26
    /// under the key `liveEvents`, written only once it holds something. A profile that never earned a point,
    /// never bought a track and never bought a pack carries none of it and serialises as it did before.
    /// </summary>
    public sealed class LiveEventsState
    {
        public LiveEventSlotState Weekly = new LiveEventSlotState();
        public LiveEventSlotState Monthly = new LiveEventSlotState();

        public LiveEventSlotState Of(LiveEventSlot slot)
        {
            return slot == LiveEventSlot.Weekly ? Weekly : Monthly;
        }

        public LiveEventsState Clone()
        {
            LiveEventsState c = new LiveEventsState();
            c.Weekly = Weekly.Clone();
            c.Monthly = Monthly.Clone();
            return c;
        }

        public bool IsEmpty
        {
            get { return Weekly.IsEmpty && Monthly.IsEmpty; }
        }

        public JsonValue Write()
        {
            JsonValue o = JsonValue.NewObject();
            if (!Weekly.IsEmpty) o.Set("weekly", Weekly.Write());
            if (!Monthly.IsEmpty) o.Set("monthly", Monthly.Write());
            return o;
        }

        public static LiveEventsState Read(JsonValue o)
        {
            LiveEventsState s = new LiveEventsState();
            if (o == null || !o.IsObject) return s;
            s.Weekly = LiveEventSlotState.Read(o["weekly"]);
            s.Monthly = LiveEventSlotState.Read(o["monthly"]);
            return s;
        }
    }
}
