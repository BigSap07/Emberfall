using System;
using System.Collections.Generic;

namespace Emberfall.Core.Data
{
    /// <summary>
    /// What one line of a purchase gives. A CLOSED SET, appended and never inserted, and every kind is
    /// FIXED: there is no kind that rolls, draws, summons or picks at random, so a grant described in
    /// these terms cannot reach a random mechanic by construction (THE RULE, Docs/DESIGN-SEALED.md).
    /// Shards are not a kind on purpose: a Sealed Shard buys nothing (the only Shard sink is the
    /// summon, which refuses Sealed), so a pack paying them would show a number it cannot spend.
    /// </summary>
    public enum GrantKind
    {
        /// <summary>Amount Gold, fixed. Sealed.</summary>
        Gold,
        /// <summary>Amount Slag, fixed. Sealed.</summary>
        Slag,
        /// <summary>Amount Relic Dust, fixed. Not marked: Dust only buys the relic shop's fixed items.</summary>
        Dust,
        /// <summary>Amount sweeps of the buyer's OWN frontier: each is one repeat-clear payout of the frontier
        /// stage (WayfarersRoad.DustPerSweep), paid as Relic Dust at the moment of the grant. Not marked: Dust only
        /// buys the relic shop's fixed items.</summary>
        DustSweeps,
        /// <summary>Amount hours of the buyer's OWN frontier idle Gold at the moment of the grant
        /// (GameSession.IdleYield). Gold only, never the Shards the same hour would pay. Sealed.</summary>
        IdleGoldHours,
        /// <summary>One copy of the fixed hero HeroId. A Sealed copy.</summary>
        HeroCopy,
        /// <summary>One copy of the hero the buyer picks from HeroIds, named in full before purchase. A Sealed copy.</summary>
        HeroChoice,
        /// <summary>One copy of a hero the buyer already owns with exactly Stars stars (the Hero Contract). A Sealed copy.</summary>
        OwnedHeroCopy,
        /// <summary>Unlocks the paid track of the current Wayfarer's Road season. Grants nothing else.</summary>
        PassPremium,
        /// <summary>Grants nothing now. Starts an offer whose payouts come later (Ember Ledger, Growth
        /// Fund); each payout is granted through PurchaseGrants.Apply, so it is Sealed too.</summary>
        Activate,
        /// <summary>Unlocks the paid Event Track of the event that runs now in the slot Amount names (0 weekly, 1
        /// monthly). Grants nothing else; each paid tier is granted later through Apply, Sealed.</summary>
        EventTrack,
        /// <summary>The fixed pack of the event that runs now in the slot Amount names (0 weekly, 1 monthly): its
        /// hours and sweeps are applied by Apply as the lines they are, once per event.</summary>
        EventPack,
        /// <summary>R24. One relic the buyer NAMES from a list shown in full before purchase (RelicPacks.Picks): a relic
        /// they do not own is added at tier 1, an owned one below the top tier is raised one tier. Both are what the
        /// Relic Shop sells for Relic Dust; nothing is drawn. Amount is the most Dust a pick may be worth at the shop
        /// (0 for no cap). No random mechanic can consume a relic.</summary>
        RelicChoice,
        /// <summary>R24. The fixed hero skin SkinId, owned by the buyer. A look and a small stat bonus on that hero only
        /// (SkinsConfig). No random mechanic can consume a skin.</summary>
        Skin
    }

    /// <summary>One line of a grant. Plain data, filled from store.json.</summary>
    public sealed class GrantItem
    {
        public GrantKind Kind;
        public double Amount;
        public string HeroId = "";
        public List<string> HeroIds = new List<string>();
        public int Stars;
        /// <summary>Skin only: the fixed skin the grant gives.</summary>
        public string SkinId = "";

        public GrantItem() { }

        public GrantItem(GrantKind kind, double amount)
        {
            Kind = kind;
            Amount = amount;
        }

        /// <summary>True when the buyer must name a hero for this line.</summary>
        public bool NeedsChoice
        {
            get { return Kind == GrantKind.HeroChoice || Kind == GrantKind.OwnedHeroCopy || Kind == GrantKind.RelicChoice; }
        }
    }

    /// <summary>The fixed contents of one purchase or one payout.</summary>
    public sealed class PurchaseGrant
    {
        public readonly List<GrantItem> Items = new List<GrantItem>();

        public PurchaseGrant Add(GrantItem item)
        {
            if (item != null) Items.Add(item);
            return this;
        }

        public bool NeedsChoice
        {
            get
            {
                for (int i = 0; i < Items.Count; i++) if (Items[i].NeedsChoice) return true;
                return false;
            }
        }

        /// <summary>Null when every line is well formed and deterministic, else the first fault, in a
        /// sentence. A grant with an unknown hero, a non-positive amount or two choices (the buyer names
        /// one hero) is not a grant.</summary>
        public string Problem(ContentDatabase db)
        {
            if (Items.Count == 0) return "the grant has no items";
            int choices = 0;
            for (int i = 0; i < Items.Count; i++)
            {
                GrantItem it = Items[i];
                switch (it.Kind)
                {
                    case GrantKind.Gold:
                    case GrantKind.Slag:
                    case GrantKind.Dust:
                    case GrantKind.IdleGoldHours:
                    case GrantKind.DustSweeps:
                        if (!(it.Amount > 0.0) || double.IsInfinity(it.Amount)) return it.Kind + " needs a positive amount";
                        if (it.Kind != GrantKind.IdleGoldHours && it.Kind != GrantKind.DustSweeps && it.Amount != Math.Floor(it.Amount)) return it.Kind + " is a whole number";
                        break;
                    case GrantKind.HeroCopy:
                        if (db != null && db.GetHero(it.HeroId) == null) return "HeroCopy names no hero (" + it.HeroId + ")";
                        break;
                    case GrantKind.HeroChoice:
                        choices++;
                        if (it.HeroIds.Count == 0) return "HeroChoice names no heroes";
                        if (db != null)
                            for (int h = 0; h < it.HeroIds.Count; h++)
                                if (db.GetHero(it.HeroIds[h]) == null) return "HeroChoice names no hero (" + it.HeroIds[h] + ")";
                        break;
                    case GrantKind.OwnedHeroCopy:
                        choices++;
                        if (it.Stars < 1) return "OwnedHeroCopy needs a star count";
                        break;
                    case GrantKind.RelicChoice:
                        choices++;
                        break;
                    case GrantKind.Skin:
                        if (string.IsNullOrEmpty(it.SkinId)) return "Skin names no skin";
                        if (db != null && db.Skins.Skin(it.SkinId) == null) return "Skin names no skin (" + it.SkinId + ")";
                        break;
                    case GrantKind.PassPremium:
                    case GrantKind.Activate:
                        break;
                    case GrantKind.EventTrack:
                    case GrantKind.EventPack:
                        if (it.Amount != 0.0 && it.Amount != 1.0) return it.Kind + " names a slot that is not 0 or 1";
                        break;
                    default:
                        return "unknown grant kind";
                }
            }
            if (choices > 1) return "a grant takes at most one choice from the buyer";
            return null;
        }
    }

    /// <summary>
    /// One thing the Store sells, as the purchase path needs it. Plain data: store.json fills it.
    /// ProductId is a PLACEHOLDER everywhere in this repository; the real store ids are set when a store
    /// account exists and are never committed.
    /// </summary>
    public sealed class StoreOffer
    {
        public string Id = "";
        /// <summary>The store's id for this product. A placeholder in data.</summary>
        public string ProductId = "";
        /// <summary>A placeholder tier in euros, for example "4.99". The owner sets the real prices.</summary>
        public string PriceTier = "";
        /// <summary>True for a product a store can hand back to a new profile of the same account (a
        /// non-consumable): the Starter Pact, the Growth Fund. A pack that resets is not restorable.</summary>
        public bool Restorable;
        public PurchaseGrant Grant = new PurchaseGrant();
    }
}
