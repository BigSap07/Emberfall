using System;
using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Model
{
    /// <summary>One purchase counter: how many of a key were bought in the period it was last stamped with.</summary>
    public sealed class StoreCount
    {
        /// <summary>An offer id, or "hero_contract|" + a hero id for the per-hero contract limit.</summary>
        public string Key = "";
        public int Count;
        /// <summary>The week, month or season the Count belongs to (0 for a limit that never resets).</summary>
        public long Period;
    }

    /// <summary>
    /// Everything the Store remembers (Docs/DESIGN-STORE.md), saved under the key `store`, written only once it
    /// holds something. A profile that never bought anything carries none of it and serialises exactly as
    /// the build before the Store wrote it. The save version it needs is the save owner's to decide
    /// (SaveSerializer): this class only says whether it is empty.
    ///
    /// THE CLOCK ONLY MOVES FORWARD. ClockUnix is the latest reward day any store write saw. Every read and
    /// write uses the LATER of the real clock's reward day and ClockUnix, so a device clock wound back
    /// reopens no limit, revives no expired card and pays no second claim.
    ///
    /// What is here is what a rule needs to be fixed in time: counters per offer, the ledger card's days, the
    /// Growth Fund's claimed milestones and the paid track's claimed tiers. What a purchase GRANTED is in the
    /// profile (marked Sealed), and receipts are the purchase path's own record, not this one.
    /// </summary>
    public sealed class StoreState
    {
        public const int CountMax = 1000;

        /// <summary>Start of the latest reward day a store write saw (GameSession.DayStartUnix). Zero: none yet.</summary>
        public long ClockUnix;

        public readonly List<StoreCount> Counts = new List<StoreCount>();

        /// <summary>Ledger: the reward day (start unix) the card was bought on. Zero: no card.</summary>
        public long LedgerStartUnix;

        /// <summary>Ledger: the latest reward day (start unix) whose hours were claimed. Zero: none.</summary>
        public long LedgerClaimedUnix;

        /// <summary>Ledger: the card's length in days when it was bought, so a retune of store.json never
        /// shortens or stretches a card already paid for.</summary>
        public int LedgerDays;

        /// <summary>Ledger: the percent added to idle Gold when it was bought, for the same reason.</summary>
        public int LedgerBonusPct;

        /// <summary>Growth Fund: bit (n) is set once milestone n (in the offer's order) is claimed.</summary>
        public int GrowthMask;

        /// <summary>The paid track: the season its claimed tiers belong to.</summary>
        public long PaidSeason;

        /// <summary>The paid track: bit (tier - 1) is set once that paid tier is claimed in PaidSeason.</summary>
        public int PaidMask;

        public bool IsEmpty
        {
            get
            {
                return ClockUnix == 0 && Counts.Count == 0 && LedgerStartUnix == 0 && LedgerClaimedUnix == 0
                    && LedgerDays == 0 && LedgerBonusPct == 0 && GrowthMask == 0 && PaidSeason == 0 && PaidMask == 0;
            }
        }

        public StoreCount Find(string key)
        {
            for (int i = 0; i < Counts.Count; i++) if (Counts[i].Key == key) return Counts[i];
            return null;
        }

        /// <summary>The count of a key in the period asked for: a counter stamped for an EARLIER period reads as zero.</summary>
        public int CountOf(string key, long period)
        {
            StoreCount c = Find(key);
            if (c == null || c.Period < period) return 0;
            return c.Count;
        }

        /// <summary>Adds one to a key in a period, restamping (and zeroing) a counter from an earlier period first.</summary>
        public void Bump(string key, long period)
        {
            StoreCount c = Find(key);
            if (c == null)
            {
                c = new StoreCount();
                c.Key = key;
                Counts.Add(c);
            }
            if (c.Period < period) { c.Period = period; c.Count = 0; }
            c.Count = Math.Min(CountMax, c.Count + 1);
        }

        public bool HasGrowth(int index)
        {
            return index >= 0 && index < 30 && (GrowthMask & (1 << index)) != 0;
        }

        public bool HasPaid(int tier)
        {
            return tier >= 1 && tier <= 30 && (PaidMask & (1 << (tier - 1))) != 0;
        }

        public JsonValue Write()
        {
            JsonValue o = JsonValue.NewObject();
            o.Set("clock", ClockUnix);
            JsonValue counts = JsonValue.NewArray();
            for (int i = 0; i < Counts.Count; i++)
            {
                JsonValue c = JsonValue.NewObject();
                c.Set("key", Counts[i].Key);
                c.Set("n", Counts[i].Count);
                c.Set("period", Counts[i].Period);
                counts.Add(c);
            }
            o.Set("counts", counts);
            o.Set("ledgerStart", LedgerStartUnix);
            o.Set("ledgerClaimed", LedgerClaimedUnix);
            o.Set("ledgerDays", LedgerDays);
            o.Set("ledgerBonus", LedgerBonusPct);
            o.Set("growth", GrowthMask);
            o.Set("paidSeason", PaidSeason);
            o.Set("paid", PaidMask);
            return o;
        }

        /// <summary>Reads a saved block. Absent or damaged reads as empty, and every number is clamped to what
        /// the game could have written.</summary>
        public static StoreState Read(JsonValue o)
        {
            StoreState s = new StoreState();
            if (o == null || !o.IsObject) return s;
            s.ClockUnix = Math.Max(0, o.GetLong("clock", 0));
            List<JsonValue> counts = o.GetList("counts");
            for (int i = 0; i < counts.Count; i++)
            {
                string key = counts[i].GetString("key", "");
                if (key.Length == 0 || s.Find(key) != null) continue;
                int n = Math.Max(0, Math.Min(CountMax, counts[i].GetInt("n", 0)));
                if (n == 0) continue;
                StoreCount c = new StoreCount();
                c.Key = key;
                c.Count = n;
                c.Period = Math.Max(0, counts[i].GetLong("period", 0));
                s.Counts.Add(c);
            }
            s.LedgerStartUnix = Math.Max(0, o.GetLong("ledgerStart", 0));
            s.LedgerClaimedUnix = Math.Max(0, o.GetLong("ledgerClaimed", 0));
            s.LedgerDays = s.LedgerStartUnix == 0 ? 0 : Math.Max(1, Math.Min(366, o.GetInt("ledgerDays", 30)));
            s.LedgerBonusPct = s.LedgerStartUnix == 0 ? 0 : Math.Max(0, Math.Min(100, o.GetInt("ledgerBonus", 0)));
            s.GrowthMask = (int)(Math.Max(0L, o.GetLong("growth", 0)) & ((1L << 30) - 1));
            s.PaidSeason = Math.Max(0, o.GetLong("paidSeason", 0));
            s.PaidMask = (int)(Math.Max(0L, o.GetLong("paid", 0)) & ((1L << 30) - 1));
            return s;
        }
    }
}
