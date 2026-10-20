using System;
using System.Collections.Generic;
using System.Globalization;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.Core.Progression
{
    /// <summary>One thing the Relic of Choice can give, named in full before the purchase: a relic the buyer does
    /// not own, or one more tier on a relic they own.</summary>
    public sealed class RelicPick
    {
        public string RelicId = "";
        public string Name = "";
        public RelicGrade Grade;
        /// <summary>True for a relic the buyer owns (the pick raises it one tier), false for a new one.</summary>
        public bool Upgrade;
        /// <summary>The tier the relic is at now (0 when it is not owned) and the tier the pick leaves it at.</summary>
        public int FromTier;
        public int ToTier;
        /// <summary>What the same thing costs at the Relic Shop, in Relic Dust: the relic's price, or the upgrade's.
        /// What the card shows as its worth.</summary>
        public int DustWorth;
        /// <summary>The pick in one line: "Warband Standard, new, worth 5,000 Relic Dust at the shop".</summary>
        public string Line = "";
    }

    /// <summary>
    /// THE RELIC OF CHOICE'S PICK LIST (R24, Docs/DESIGN-STORE.md): which relic the buyer may name. A relic the
    /// buyer does not own is added at tier 1; an owned relic below the top tier is raised one tier. Both exist at
    /// the Relic Shop for Relic Dust (GameSession.BuyRelic, UpgradeRelic), so this reads their prices and the
    /// relic endgame's tier ceiling from the session and invents none. The list is the shop's own list in the
    /// shop's own order (relics.json), so it is deterministic: nothing here is drawn, rolled or weighted, and the
    /// same profile gives the same list.
    /// </summary>
    public static class RelicPacks
    {
        /// <summary>The picks, in the shop's order. capDust, when above 0, leaves out a pick that is worth more than that at
        /// the shop (the offer's pickCapDust): one pack never names a whole ladder step.</summary>
        public static List<RelicPick> Picks(GameSession session, int capDust)
        {
            List<RelicPick> picks = new List<RelicPick>();
            if (session == null) return picks;
            ContentDatabase content = session.Content;
            PlayerProfile profile = session.Profile;
            int maxTier = session.RelicMaxTier;
            for (int i = 0; i < content.Relics.Count; i++)
            {
                RelicPick p = Pick(session, content.Relics[i], profile, maxTier);
                if (p != null && (capDust <= 0 || p.DustWorth <= capDust)) picks.Add(p);
            }
            return picks;
        }

        /// <summary>The pick for one relic id, or null when the buyer may not name it: unknown to content, or owned at
        /// the top tier (or with the relic endgame off, owned at all).</summary>
        public static RelicPick Find(GameSession session, string relicId, int capDust)
        {
            if (session == null || string.IsNullOrEmpty(relicId)) return null;
            RelicDef relic = session.Content.GetRelic(relicId);
            RelicPick p = relic == null ? null : Pick(session, relic, session.Profile, session.RelicMaxTier);
            return p != null && (capDust <= 0 || p.DustWorth <= capDust) ? p : null;
        }

        private static RelicPick Pick(GameSession session, RelicDef relic, PlayerProfile profile, int maxTier)
        {
            OwnedRelic owned = profile.FindOwnedRelic(relic.Id);
            RelicPick p = new RelicPick();
            p.RelicId = relic.Id;
            p.Name = relic.Name;
            p.Grade = relic.Grade;
            if (owned == null)
            {
                p.Upgrade = false;
                p.FromTier = 0;
                p.ToTier = 1;
                p.DustWorth = session.RelicPrice(relic);
                p.Line = relic.Name + " (" + relic.Grade + "), new, worth " + Group(p.DustWorth) + " Relic Dust at the shop";
                return p;
            }
            if (owned.Tier >= maxTier) return null;
            int price = session.RelicUpgradePrice(relic, owned.Tier + 1);
            if (price <= 0) return null;   // the relic endgame is off, or the file does not author that tier
            p.Upgrade = true;
            p.FromTier = owned.Tier;
            p.ToTier = owned.Tier + 1;
            p.DustWorth = price;
            p.Line = relic.Name + ", tier " + p.FromTier + " to " + p.ToTier + ", worth " + Group(price) + " Relic Dust at the shop";
            return p;
        }

        /// <summary>Applies a pick. The caller (PurchaseGrants.Apply) has checked it with Find. Returns the relic's
        /// tier afterwards.</summary>
        public static int Give(GameSession session, RelicPick pick)
        {
            if (!pick.Upgrade)
            {
                session.GrantRelic(pick.RelicId);
                return 1;
            }
            OwnedRelic owned = session.Profile.FindOwnedRelic(pick.RelicId);
            owned.Tier = pick.ToTier;
            return owned.Tier;
        }

        /// <summary>The cheapest and dearest pick, in Relic Dust at the shop, for the card's honest range. Zeros when
        /// there is nothing to pick.</summary>
        public static void WorthRange(List<RelicPick> picks, out int least, out int most)
        {
            least = 0;
            most = 0;
            for (int i = 0; i < picks.Count; i++)
            {
                if (least == 0 || picks[i].DustWorth < least) least = picks[i].DustWorth;
                if (picks[i].DustWorth > most) most = picks[i].DustWorth;
            }
        }

        private static string Group(long v)
        {
            return v.ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}
