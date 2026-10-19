using System;
using System.Collections.Generic;
using Emberfall.Core.Json;
using Emberfall.Core.Save;

namespace Emberfall.Core.Model
{
    /// <summary>One shelf of the Quartermaster: a rolled piece that has no uid until it is bought,
    /// and whether it has been.</summary>
    public sealed class QuartermasterSlot
    {
        public ArmorPiece Piece;
        public bool Sold;
    }

    /// <summary>
    /// Everything the Quartermaster remembers (Docs/DESIGN-QUARTERMASTER.md), saved from version 24
    /// under the key `quartermaster`, written only once it holds a stock. A profile that never opened
    /// the shop carries none of it and serialises exactly as it did before (SaveSerializer.VersionOf).
    ///
    /// THE STOCK IS STORED AS RESOLVED PIECES, not as a roll seed (DESIGN-SHOP section 7): armor.json
    /// is tuning data expected to move, and a seed would mean every retune silently rerolled the stock
    /// a player was looking at. Prices are NOT stored: they are hours of idle Gold, worked out at
    /// display and at purchase from the live frontier (Quartermaster.PriceOf).
    ///
    /// DayUnix is the reward day the stock belongs to. The stamp only moves forward, like
    /// SinksState's: a clock wound back compares as "not a later day" and rolls nothing, so it cannot
    /// buy a fresh shelf.
    /// </summary>
    public sealed class QuartermasterState
    {
        /// <summary>More shelves than this on read are damage, far above any stock the game rolls.</summary>
        public const int MaxSlots = 12;

        /// <summary>Start of the reward day the stock belongs to. Zero means no stock was ever rolled.</summary>
        public long DayUnix;

        public readonly List<QuartermasterSlot> Slots = new List<QuartermasterSlot>();

        public bool IsEmpty
        {
            get { return DayUnix == 0 && Slots.Count == 0; }
        }

        public JsonValue Write()
        {
            JsonValue o = JsonValue.NewObject();
            o.Set("day", DayUnix);
            JsonValue slots = JsonValue.NewArray();
            for (int i = 0; i < Slots.Count; i++)
            {
                if (Slots[i].Piece == null) continue;
                JsonValue s = JsonValue.NewObject();
                s.Set("piece", SaveSerializer.WriteArmorPieceJson(Slots[i].Piece));
                if (Slots[i].Sold) s.Set("sold", true);
                slots.Add(s);
            }
            o.Set("slots", slots);
            return o;
        }

        /// <summary>Reads a saved block. Absent or damaged reads as empty (the shop rolls a fresh
        /// stock the next time it is opened), and a shelf whose piece is not an object is dropped.</summary>
        public static QuartermasterState Read(JsonValue o)
        {
            QuartermasterState s = new QuartermasterState();
            if (o == null || !o.IsObject) return s;
            s.DayUnix = Math.Max(0, o.GetLong("day", 0));
            List<JsonValue> slots = o.GetList("slots");
            for (int i = 0; i < slots.Count && s.Slots.Count < MaxSlots; i++)
            {
                JsonValue pj = slots[i].IsObject ? slots[i]["piece"] : null;
                if (pj == null || !pj.IsObject) continue;
                QuartermasterSlot slot = new QuartermasterSlot();
                slot.Piece = SaveSerializer.ReadArmorPieceJson(pj);
                slot.Piece.Uid = "";
                slot.Piece.Sealed = false;
                slot.Sold = slots[i].GetBool("sold", false);
                s.Slots.Add(slot);
            }
            if (s.Slots.Count == 0) s.DayUnix = 0;
            return s;
        }
    }
}
