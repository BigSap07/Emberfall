using System;
using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Data
{
    /// <summary>One upgrade tier: what it costs as a multiple of the relic's own price, and how much
    /// it scales the relic's stat block.</summary>
    public sealed class RelicTierDef
    {
        public int Tier;
        public double PriceMultiplier = 1.0;
        public double StatScale = 1.0;
    }

    /// <summary>
    /// relic-shop.json: what the Relic Shop charges and what an upgrade is worth
    /// (Docs/DESIGN-RELIC-ENDGAME.md).
    ///
    /// NOT HASHED INTO ContentDatabase.ContentVersion, for guild-path.json's reason: the guild dungeon
    /// fights with no relics, so no recorded replay reads it, and a retune of a price must not retire
    /// them. Loaded through ContentDatabase.LoadRelicShop after Load.
    ///
    /// The relic LIST is not here: it is relics.json, and a relic added there is on sale at its
    /// grade's price with no entry in this file. Overrides exist for a relic that should not follow
    /// its grade. Empty (IsUsable false) on a database that never loaded the file, which the shop reads
    /// as "use balance.json's prices and no upgrades".
    /// </summary>
    public sealed class RelicShopConfig
    {
        public int LegendaryPrice;
        public int MythicPrice;

        /// <summary>A price for one relic that overrides its grade's, by relic id.</summary>
        public readonly Dictionary<string, int> Overrides = new Dictionary<string, int>();

        /// <summary>Tiers 2 and up, in tier order. Tier 1 is the relic as bought.</summary>
        public readonly List<RelicTierDef> Tiers = new List<RelicTierDef>();

        public readonly List<string> Warnings = new List<string>();

        public bool IsUsable
        {
            get { return LegendaryPrice > 0 && MythicPrice > 0; }
        }

        /// <summary>The highest tier a relic can reach: 1 plus the tiers the file authors.</summary>
        public int MaxTier
        {
            get { return 1 + Tiers.Count; }
        }

        public RelicTierDef TierDef(int tier)
        {
            for (int i = 0; i < Tiers.Count; i++) if (Tiers[i].Tier == tier) return Tiers[i];
            return null;
        }

        /// <summary>The factor a relic's stat values are scaled by at this tier: 1 at tier 1 and for a
        /// tier the file does not author.</summary>
        public double StatScaleOf(int tier)
        {
            RelicTierDef t = TierDef(tier);
            return t != null ? t.StatScale : 1.0;
        }

        public static RelicShopConfig FromJson(string text)
        {
            RelicShopConfig c = new RelicShopConfig();
            if (string.IsNullOrEmpty(text)) return c;
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { c.Warnings.Add("relic-shop.json: " + err); return c; }

            JsonValue prices = root["prices"];
            if (prices != null && prices.IsObject)
            {
                c.LegendaryPrice = Math.Max(0, prices.GetInt("Legendary", 0));
                c.MythicPrice = Math.Max(0, prices.GetInt("Mythic", 0));
            }
            if (!c.IsUsable) c.Warnings.Add("relic-shop.json: prices.Legendary and prices.Mythic must both be positive; the file is ignored");

            JsonValue overrides = root["overrides"];
            if (overrides != null && overrides.IsObject)
            {
                foreach (string key in overrides.Keys)
                {
                    int price = overrides.GetInt(key, 0);
                    if (price <= 0) { c.Warnings.Add("relic-shop.json: the override for '" + key + "' is not a positive price and is dropped"); continue; }
                    c.Overrides[key] = price;
                }
            }

            JsonValue upgrades = root["upgrades"];
            List<JsonValue> tiers = upgrades != null && upgrades.IsObject ? upgrades.GetList("tiers") : new List<JsonValue>();
            int expected = 2;
            double previousMultiplier = 0.0;
            for (int i = 0; i < tiers.Count; i++)
            {
                RelicTierDef t = new RelicTierDef();
                t.Tier = tiers[i].GetInt("tier", 0);
                t.PriceMultiplier = tiers[i]["priceMultiplier"].AsDouble(0.0);
                t.StatScale = tiers[i]["statScale"].AsDouble(0.0);
                if (t.Tier != expected || t.PriceMultiplier <= previousMultiplier || t.StatScale < 1.0)
                {
                    c.Warnings.Add("relic-shop.json: upgrade tier " + t.Tier + " must be tier " + expected
                                   + ", cost more than the tier before and scale by at least 1; the tiers from here are dropped");
                    break;
                }
                c.Tiers.Add(t);
                previousMultiplier = t.PriceMultiplier;
                expected++;
            }
            return c;
        }
    }
}
