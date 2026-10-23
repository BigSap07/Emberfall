using Emberfall.Core.Data;
using Emberfall.Core.Model;

namespace Emberfall.Core.Progression
{
    /// <summary>One shop behind the Marketplace: the order it is shown in, its words, the feature that
    /// opens it, the door id its card passes to the gate, and the router screen it opens.</summary>
    public sealed class MarketplaceShop
    {
        public readonly string Key;
        public readonly string Name;
        public readonly string Pays;
        public readonly string Sells;
        public readonly Feature Feature;
        public readonly string EntryId;
        public readonly string ScreenName;

        public MarketplaceShop(string key, string name, string pays, string sells, Feature feature, string entryId, string screenName)
        {
            Key = key;
            Name = name;
            Pays = pays;
            Sells = sells;
            Feature = feature;
            EntryId = entryId;
            ScreenName = screenName;
        }
    }

    /// <summary>
    /// THE MARKETPLACE'S FOUR SHOPS (R26), in one engine-free list so the Store's SHOPS tab, the Marketplace
    /// screen and the harness all read the same order, words and doors. Decides nothing about opening: each
    /// card asks FeatureEntryPoints for its own door id, so the Relic Shop card stays locked until 3-1 while
    /// the Marketplace itself opens at 2-4 (Feature.Marketplace).
    /// </summary>
    public static class MarketplaceShops
    {
        public static readonly MarketplaceShop[] All =
        {
            new MarketplaceShop("RelicShop", "RELIC SHOP", "Relic Dust", "Relics at their fixed price, and the upgrade of each relic you own.",
                                Feature.Relics, "marketplace.RelicShop", "RelicShop"),
            new MarketplaceShop("Quartermaster", "QUARTERMASTER", "Gold", "A daily shelf of rolled armor, one piece for each slot, restocked every reward day.",
                                Feature.Quartermaster, "marketplace.Quartermaster", "Quartermaster"),
            new MarketplaceShop("Armory", "PROVING ARMORY", "Valor", "Honed armor presets, Brand and Echo refills and one four-star copy a month. Every item is fixed.",
                                Feature.Proving, "marketplace.Armory", "Armory"),
            new MarketplaceShop("Archive", "RECITAL ARCHIVE", "Coda", "One four-star hero copy a month, chosen from a fixed list.",
                                Feature.Recital, "marketplace.Archive", "Archive")
        };

        /// <summary>"2 of 4 open": how many of the shops can be walked into right now.</summary>
        public static string CountLine(int open)
        {
            return open + " of " + All.Length + " open";
        }

        /// <summary>Open shops by the unlock table alone (a shop a build does not contain is not counted
        /// here; the UI subtracts it).</summary>
        public static int OpenByTable(ContentDatabase content, PlayerProfile profile)
        {
            int open = 0;
            for (int i = 0; i < All.Length; i++)
                if (FeatureEntryPoints.Allows(content, profile, All[i].EntryId)) open++;
            return open;
        }
    }
}
