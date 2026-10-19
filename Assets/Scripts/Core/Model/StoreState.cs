using System;
using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Model
{
    /// <summary>What the store paid for one purchase, kept so the same receipt can never grant twice and so
    /// a restore on a new profile knows what it has already delivered. No store key, no signature and no
    /// account detail is kept: the receipt id is the transaction id the store (or the debug store) gave.</summary>
    public sealed class PurchaseReceipt
    {
        public string ReceiptId = "";
        public string ProductId = "";
        public string OfferId = "";
        public long GrantedUnix;
        /// <summary>True when a restore delivered it rather than a purchase on this profile.</summary>
        public bool Restored;
    }

    /// <summary>One offer's saved counters. GENERIC ON PURPOSE so the offer rules (limits, weekly and
    /// monthly resets, the Ember Ledger's days, the Growth Fund's milestones) need no save work of their
    /// own: each rule writes whatever it needs into these fields and nothing else.</summary>
    public sealed class OfferRecord
    {
        public string OfferId = "";
        /// <summary>The week, month or season the Count belongs to; 0 for a once-only offer.</summary>
        public long Period;
        /// <summary>Purchases made in Period.</summary>
        public int Count;
        /// <summary>First reward day of a running card; 0 when none runs.</summary>
        public long StartDay;
        /// <summary>The last reward day a daily payout was claimed.</summary>
        public long LastDay;
        /// <summary>Bit n is set once the nth payout or milestone is claimed.</summary>
        public int Mask;

        public bool IsEmpty
        {
            get { return Period == 0 && Count == 0 && StartDay == 0 && LastDay == 0 && Mask == 0; }
        }
    }

    /// <summary>
    /// Everything the Store remembers (R19), saved from version 25 under the key `store`, written only once it
    /// holds something. A profile that never bought anything carries none of it and serialises as before.
    /// Pending purchases are NEVER here: a purchase the store has not completed lives in memory only, so a
    /// pending purchase that never completes leaves the save untouched.
    /// </summary>
    public sealed class StoreState
    {
        public const int ReceiptsMax = 500;
        public const int OffersMax = 64;

        public readonly List<OfferRecord> Offers = new List<OfferRecord>();
        public readonly List<PurchaseReceipt> Receipts = new List<PurchaseReceipt>();

        public bool IsEmpty
        {
            get
            {
                if (Receipts.Count > 0) return false;
                for (int i = 0; i < Offers.Count; i++) if (!Offers[i].IsEmpty) return false;
                return true;
            }
        }

        /// <summary>The record of an offer, or null when it has none.</summary>
        public OfferRecord Find(string offerId)
        {
            for (int i = 0; i < Offers.Count; i++) if (Offers[i].OfferId == offerId) return Offers[i];
            return null;
        }

        /// <summary>The record of an offer, created empty when it has none. An empty record is not written.</summary>
        public OfferRecord Record(string offerId)
        {
            OfferRecord r = Find(offerId);
            if (r != null) return r;
            r = new OfferRecord();
            r.OfferId = offerId ?? "";
            Offers.Add(r);
            return r;
        }

        public PurchaseReceipt FindReceipt(string receiptId)
        {
            for (int i = 0; i < Receipts.Count; i++) if (Receipts[i].ReceiptId == receiptId) return Receipts[i];
            return null;
        }

        public JsonValue Write()
        {
            JsonValue o = JsonValue.NewObject();
            JsonValue offers = JsonValue.NewArray();
            for (int i = 0; i < Offers.Count; i++)
            {
                OfferRecord r = Offers[i];
                if (r.IsEmpty) continue;
                JsonValue j = JsonValue.NewObject();
                j.Set("offer", r.OfferId);
                if (r.Period != 0) j.Set("period", r.Period);
                if (r.Count != 0) j.Set("count", r.Count);
                if (r.StartDay != 0) j.Set("start", r.StartDay);
                if (r.LastDay != 0) j.Set("last", r.LastDay);
                if (r.Mask != 0) j.Set("mask", r.Mask);
                offers.Add(j);
            }
            if (offers.Count > 0) o.Set("offers", offers);
            JsonValue receipts = JsonValue.NewArray();
            for (int i = 0; i < Receipts.Count; i++)
            {
                PurchaseReceipt r = Receipts[i];
                JsonValue j = JsonValue.NewObject();
                j.Set("id", r.ReceiptId);
                j.Set("product", r.ProductId);
                j.Set("offer", r.OfferId);
                j.Set("at", r.GrantedUnix);
                if (r.Restored) j.Set("restored", true);
                receipts.Add(j);
            }
            if (receipts.Count > 0) o.Set("receipts", receipts);
            return o;
        }

        /// <summary>Reads a saved block. Absent or damaged reads as empty; a damaged entry is skipped, every
        /// number is clamped to what the game could have written, and a repeated receipt id or offer id
        /// keeps its first entry.</summary>
        public static StoreState Read(JsonValue o)
        {
            StoreState s = new StoreState();
            if (o == null || !o.IsObject) return s;
            List<JsonValue> offers = o.GetList("offers");
            for (int i = 0; i < offers.Count && s.Offers.Count < OffersMax; i++)
            {
                JsonValue j = offers[i];
                if (j == null || !j.IsObject) continue;
                string id = j.GetString("offer", "");
                if (id.Length == 0 || s.Find(id) != null) continue;
                OfferRecord r = new OfferRecord();
                r.OfferId = id;
                r.Period = Math.Max(0, j.GetLong("period", 0));
                r.Count = Math.Max(0, Math.Min(100000, j.GetInt("count", 0)));
                r.StartDay = Math.Max(0, j.GetLong("start", 0));
                r.LastDay = Math.Max(0, j.GetLong("last", 0));
                r.Mask = (int)(Math.Max(0L, j.GetLong("mask", 0)) & 0x7FFFFFFFL);
                if (!r.IsEmpty) s.Offers.Add(r);
            }
            List<JsonValue> receipts = o.GetList("receipts");
            for (int i = 0; i < receipts.Count && s.Receipts.Count < ReceiptsMax; i++)
            {
                JsonValue j = receipts[i];
                if (j == null || !j.IsObject) continue;
                string id = j.GetString("id", "");
                if (id.Length == 0 || id.Length > 200 || s.FindReceipt(id) != null) continue;
                PurchaseReceipt r = new PurchaseReceipt();
                r.ReceiptId = id;
                r.ProductId = j.GetString("product", "");
                r.OfferId = j.GetString("offer", "");
                r.GrantedUnix = Math.Max(0, j.GetLong("at", 0));
                r.Restored = j.GetBool("restored", false);
                s.Receipts.Add(r);
            }
            return s;
        }
    }
}
