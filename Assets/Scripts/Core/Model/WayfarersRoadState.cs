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

        /// <summary>The season whose paid track the player bought (R19), or 0. The paid track is open exactly
        /// when this equals SeasonIndex, so a new season locks it again with no step to run. Saved from
        /// version 25 under `paid`, written only once set.</summary>
        public long PaidSeason;

        /// <summary>Bit (tier - 1) is set once that tier's PAID reward has been claimed. Saved under `paidClaimed`.</summary>
        public int PaidClaimedMask;

        public WayfarersRoadState Clone()
        {
            return (WayfarersRoadState)MemberwiseClone();
        }

        public bool IsEmpty
        {
            get { return SeasonIndex == 0 && Points == 0 && ClaimedMask == 0 && Bought == 0 && PaidSeason == 0 && PaidClaimedMask == 0; }
        }

        /// <summary>True when the paid track data is what makes this block a version 25 block.</summary>
        public bool HoldsPaidData
        {
            get { return PaidSeason != 0 || PaidClaimedMask != 0; }
        }

        public bool HasClaimedPaid(int tier)
        {
            return tier >= 1 && tier <= 30 && (PaidClaimedMask & (1 << (tier - 1))) != 0;
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
            if (PaidSeason != 0) o.Set("paid", PaidSeason);
            if (PaidClaimedMask != 0) o.Set("paidClaimed", PaidClaimedMask);
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
            s.PaidSeason = Math.Max(0, o.GetLong("paid", 0));
            long paidMask = o.GetLong("paidClaimed", 0);
            s.PaidClaimedMask = (int)(Math.Max(0L, paidMask) & ((1L << 30) - 1));
            return s;
        }
    }
}
