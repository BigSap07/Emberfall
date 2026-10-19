using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.Core.Progression
{
    /// <summary>One relic as the Relic Shop shows it. Read only: a screen never works any of this out.</summary>
    public sealed class RelicCardView
    {
        /// <summary>The relic as the player has it: scaled to the owned tier when owned, the content's
        /// own otherwise. What the card's text is read from.</summary>
        public RelicDef Relic;
        public bool Owned;
        public int Tier;
        public int MaxTier;
        /// <summary>What buying it costs. Meaningful while it is not owned.</summary>
        public int Price;
        /// <summary>What raising it one tier costs, or 0 when it is not owned, at the top tier, or the
        /// relic endgame is off.</summary>
        public int UpgradePrice;
        /// <summary>The stat line the next tier would give, for the upgrade button to state.</summary>
        public string NextStats = "";
        public bool CanBuy;
        public bool CanUpgrade;
        /// <summary>True when the purse is short of what the card's one action costs.</summary>
        public bool Poor;
    }

    /// <summary>
    /// The Relic Shop's shelf (Docs/DESIGN-RELIC-ENDGAME.md): every relic content names, with its price
    /// and, once owned, its tier and the price of the next. Engine free. THE LIST IS relics.json and
    /// nothing else: a relic added there is on the shelf, on sale at its grade's price and fights with
    /// its own numbers, with no entry anywhere else and no code (CheckRelicShopIsDataDriven).
    /// </summary>
    public static class RelicShop
    {
        public static List<RelicCardView> View(GameSession session)
        {
            List<RelicCardView> cards = new List<RelicCardView>();
            ContentDatabase content = session.Content;
            PlayerProfile profile = session.Profile;
            for (int i = 0; i < content.Relics.Count; i++)
            {
                RelicDef relic = content.Relics[i];
                RelicCardView v = new RelicCardView();
                v.Owned = profile.OwnsRelic(relic.Id);
                v.Tier = v.Owned ? profile.RelicTier(relic.Id) : 0;
                v.MaxTier = session.RelicMaxTier;
                v.Relic = v.Owned ? profile.RelicAtOwnedTier(relic.Id, content) : relic;
                v.Price = session.RelicPrice(relic);
                if (v.Owned && v.Tier < v.MaxTier)
                {
                    v.UpgradePrice = session.RelicUpgradePrice(relic, v.Tier + 1);
                    if (v.UpgradePrice > 0)
                    {
                        RelicTierDef next = content.RelicShop.TierDef(v.Tier + 1);
                        if (next != null) v.NextStats = relic.AtScale(next.StatScale).DescribeStats();
                    }
                }
                v.CanBuy = !v.Owned && profile.RelicDust >= v.Price;
                v.CanUpgrade = v.UpgradePrice > 0 && profile.RelicDust >= v.UpgradePrice;
                v.Poor = v.Owned ? (v.UpgradePrice > 0 && !v.CanUpgrade) : !v.CanBuy;
                cards.Add(v);
            }
            return cards;
        }

        public static bool Buy(GameSession session, string relicId, long nowUnix)
        {
            return session.BuyRelic(relicId, nowUnix);
        }

        public static bool Upgrade(GameSession session, string relicId, long nowUnix)
        {
            return session.UpgradeRelic(relicId, nowUnix);
        }
    }
}
