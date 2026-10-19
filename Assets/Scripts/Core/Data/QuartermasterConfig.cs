using System;
using System.Collections.Generic;
using Emberfall.Core.Json;
using Emberfall.Core.Model;

namespace Emberfall.Core.Data
{
    /// <summary>
    /// quartermaster.json: what the Quartermaster stocks and what it charges
    /// (Docs/DESIGN-QUARTERMASTER.md).
    ///
    /// NOT HASHED INTO ContentDatabase.ContentVersion, for guild-path.json's reason: nothing here is
    /// read by a fight, so retuning a price must not retire every guild's replays. Loaded through
    /// ContentDatabase.LoadQuartermaster after Load. A price is HOURS of the player's idle Gold at
    /// their frontier (Quartermaster.PriceOf); there is no Gold amount in this file.
    ///
    /// Empty (IsUsable false) on a database that never loaded the file, which the shop reads as
    /// "nothing to sell" even with the switch on.
    /// </summary>
    public sealed class QuartermasterConfig
    {
        /// <summary>The slots a day's stock holds one piece of each, in shelf order.</summary>
        public readonly List<ArmorSlot> Slots = new List<ArmorSlot>();

        /// <summary>Hours of idle Gold a piece costs, by temper. A temper with no entry is never
        /// stocked (the roll is re-taken as the highest temper that has a price).</summary>
        public readonly Dictionary<ArmorTemper, double> Hours = new Dictionary<ArmorTemper, double>();

        /// <summary>Prices round up to a multiple of this.</summary>
        public int PriceRound = 10;

        public readonly List<string> Warnings = new List<string>();

        public bool IsUsable
        {
            get { return Slots.Count > 0 && Hours.Count > 0; }
        }

        public double HoursOf(ArmorTemper temper)
        {
            double h;
            return Hours.TryGetValue(temper, out h) ? h : 0.0;
        }

        public static QuartermasterConfig FromJson(string text)
        {
            QuartermasterConfig c = new QuartermasterConfig();
            if (string.IsNullOrEmpty(text)) return c;
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { c.Warnings.Add("quartermaster.json: " + err); return c; }

            c.PriceRound = Math.Max(1, root.GetInt("priceRound", c.PriceRound));

            List<JsonValue> slots = root.GetList("slots");
            for (int i = 0; i < slots.Count; i++)
            {
                ArmorSlot slot;
                if (!TryEnum(slots[i].AsString(""), out slot))
                {
                    c.Warnings.Add("quartermaster.json: unknown slot '" + slots[i].AsString("") + "' is dropped");
                    continue;
                }
                if (c.Slots.Contains(slot)) continue;
                c.Slots.Add(slot);
            }

            JsonValue hours = root["hours"];
            if (hours != null && hours.IsObject)
            {
                foreach (ArmorTemper t in Enum.GetValues(typeof(ArmorTemper)))
                {
                    if (!hours.Has(t.ToString())) continue;
                    double h = hours[t.ToString()].AsDouble(0.0);
                    if (h <= 0.0)
                    {
                        c.Warnings.Add("quartermaster.json: temper " + t + " needs a positive number of hours and is dropped");
                        continue;
                    }
                    if ((int)t > (int)ArmorInfo.MaxDropTemper)
                    {
                        c.Warnings.Add("quartermaster.json: temper " + t + " is above what a drop can roll and is dropped");
                        continue;
                    }
                    c.Hours[t] = h;
                }
            }
            return c;
        }

        private static bool TryEnum<T>(string text, out T value) where T : struct
        {
            value = default(T);
            if (string.IsNullOrEmpty(text)) return false;
            try { value = (T)Enum.Parse(typeof(T), text.Trim(), true); return true; }
            catch (Exception) { return false; }
        }
    }
}
