using System;
using Emberfall.Core.Json;

namespace Emberfall.Core.Model
{
    /// <summary>
    /// Everything Wayfarer's Road remembers (Docs/DESIGN-WAYFARERS-ROAD.md), saved from version 24 under
    /// the key `wayfarersRoad`, written only once it holds something. A profile that never earned a
    /// point carries none of it and serialises as it did before.
    ///
    /// ONE SEASON AT A TIME. SeasonIndex names the season the rest belongs to (1 is the first from the
    /// epoch); a later season replaces everything, so unclaimed tiers lapse. THE STAMP ONLY MOVES
    /// FORWARD, like the daily loop's: a clock wound back compares as "not a later season" and changes
    /// nothing. There is no field for a paid track, on purpose: nothing can unlock it.
    /// </summary>
    public sealed class WayfarersRoadState
    {
        /// <summary>The season the counters below belong to. Zero means none.</summary>
        public long SeasonIndex;

        /// <summary>Points earned this season, capped at what finishes the track.</summary>
        public int Points;

        /// <summary>Bit (tier - 1) is set once that tier's reward has been claimed.</summary>
        public int ClaimedMask;

        /// <summary>Tiers bought this season with Valor or Coda.</summary>
        public int Bought;

        public bool IsEmpty
        {
            get { return SeasonIndex == 0 && Points == 0 && ClaimedMask == 0 && Bought == 0; }
        }

        public bool HasClaimed(int tier)
        {
            return tier >= 1 && tier <= 30 && (ClaimedMask & (1 << (tier - 1))) != 0;
        }

        public JsonValue Write()
        {
            JsonValue o = JsonValue.NewObject();
            o.Set("season", SeasonIndex);
            o.Set("points", Points);
            o.Set("claimed", ClaimedMask);
            o.Set("bought", Bought);
            return o;
        }

        /// <summary>Reads a saved block. Absent or damaged reads as empty, and every number is clamped to
        /// what the game could have written.</summary>
        public static WayfarersRoadState Read(JsonValue o)
        {
            WayfarersRoadState s = new WayfarersRoadState();
            if (o == null || !o.IsObject) return s;
            s.SeasonIndex = Math.Max(0, o.GetLong("season", 0));
            s.Points = Math.Max(0, Math.Min(100000, o.GetInt("points", 0)));
            long mask = o.GetLong("claimed", 0);
            s.ClaimedMask = (int)(Math.Max(0L, mask) & ((1L << 30) - 1));
            s.Bought = Math.Max(0, Math.Min(30, o.GetInt("bought", 0)));
            return s;
        }
    }
}
