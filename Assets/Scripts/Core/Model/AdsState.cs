using System;
using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Model
{
    /// <summary>One placement's count for one reward day.</summary>
    public sealed class AdPlacementRecord
    {
        public string Id = "";
        /// <summary>The reward day Count belongs to (GameSession.DayStartUnix). Zero means never.</summary>
        public long Day;
        public int Count;
    }

    /// <summary>
    /// What rewarded ads remember (R28, Docs/DESIGN-REWARDED-ADS.md), saved from version 27 under the key `ads`,
    /// written only once it holds something. With AdsSwitch off nothing ever writes it, so a profile serialises
    /// exactly as it did before version 27.
    ///
    /// ONE CLOCK, FORWARD ONLY. ClockDay is the latest reward day an ad was granted on, and AdRewards counts against
    /// the later of it and the device's day, so a clock wound back reopens nothing (StoreOffers.EffectiveDay's rule).
    ///
    /// ONCE PER IMPRESSION. The last ImpressionKeep impression ids are kept, so an ad network that reports one
    /// reward twice grants once.
    /// </summary>
    public sealed class AdsState
    {
        public const int ImpressionKeep = 50;

        public long ClockDay;
        public readonly List<AdPlacementRecord> Placements = new List<AdPlacementRecord>();
        /// <summary>Oldest first.</summary>
        public readonly List<string> Impressions = new List<string>();

        public bool IsEmpty
        {
            get { return ClockDay == 0 && Placements.Count == 0 && Impressions.Count == 0; }
        }

        public AdPlacementRecord Find(string id)
        {
            for (int i = 0; i < Placements.Count; i++) if (Placements[i].Id == id) return Placements[i];
            return null;
        }

        public AdPlacementRecord Record(string id)
        {
            AdPlacementRecord r = Find(id);
            if (r != null) return r;
            r = new AdPlacementRecord();
            r.Id = id;
            Placements.Add(r);
            return r;
        }

        public bool Seen(string impressionId)
        {
            return !string.IsNullOrEmpty(impressionId) && Impressions.Contains(impressionId);
        }

        public void Remember(string impressionId)
        {
            if (string.IsNullOrEmpty(impressionId) || Impressions.Contains(impressionId)) return;
            Impressions.Add(impressionId);
            while (Impressions.Count > ImpressionKeep) Impressions.RemoveAt(0);
        }

        public JsonValue Write()
        {
            JsonValue o = JsonValue.NewObject();
            o.Set("clockDay", ClockDay);
            JsonValue list = JsonValue.NewArray();
            for (int i = 0; i < Placements.Count; i++)
            {
                JsonValue p = JsonValue.NewObject();
                p.Set("id", Placements[i].Id);
                p.Set("day", Placements[i].Day);
                p.Set("count", Placements[i].Count);
                list.Add(p);
            }
            o.Set("placements", list);
            JsonValue seen = JsonValue.NewArray();
            for (int i = 0; i < Impressions.Count; i++) seen.Add(Impressions[i]);
            o.Set("impressions", seen);
            return o;
        }

        /// <summary>Reads a saved block. Absent or damaged reads as empty; every number is clamped to what the game
        /// could have written.</summary>
        public static AdsState Read(JsonValue o)
        {
            AdsState s = new AdsState();
            if (o == null || !o.IsObject) return s;
            s.ClockDay = Math.Max(0, o.GetLong("clockDay", 0));
            List<JsonValue> list = o.GetList("placements");
            for (int i = 0; i < list.Count; i++)
            {
                string id = list[i].GetString("id", "");
                if (id.Length == 0 || s.Find(id) != null) continue;
                AdPlacementRecord r = s.Record(id);
                r.Day = Math.Max(0, list[i].GetLong("day", 0));
                r.Count = Math.Max(0, Math.Min(99, list[i].GetInt("count", 0)));
            }
            List<JsonValue> seen = o.GetList("impressions");
            for (int i = 0; i < seen.Count; i++) s.Remember(seen[i].AsString(""));
            return s;
        }
    }
}
