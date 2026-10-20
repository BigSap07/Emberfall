using System;
using System.Collections.Generic;
using System.Globalization;
using Emberfall.Core.Json;

namespace Emberfall.Core.Data
{
    /// <summary>The closed set of things the store sells. Appended and never inserted: the file names a kind
    /// by its lower-case string, and an unknown kind is dropped with a warning, so a roll cannot be written
    /// into the file by accident.</summary>
    public enum StoreOfferKind
    {
        /// <summary>A fixed bundle: hours of idle Gold, Slag, Dust sweeps. Weekly and monthly packs.</summary>
        Pack,
        /// <summary>A fixed bundle plus one hero the buyer picks from a named list.</summary>
        Starter,
        /// <summary>A card that pays hours every reward day it is claimed and adds a share to every idle claim.</summary>
        Ledger,
        /// <summary>Fixed rewards paid when named stages are cleared.</summary>
        Growth,
        /// <summary>The paid track of Wayfarer's Road, for the season it is bought in.</summary>
        Pass,
        /// <summary>One more copy of a hero the buyer already owns, for ascension.</summary>
        Contract,
        /// <summary>The paid Event Track or the pack of the weekly or monthly live event that runs now
        /// (live-events.json). Its contents come from that event; its limit is once per event and is counted in the
        /// profile's live event state, not in an offer record.</summary>
        Event,
        /// <summary>R24. One relic the buyer names from a list shown in full: one they do not own, or one more tier on one
        /// they own (the Relic of Choice).</summary>
        RelicChoice,
        /// <summary>R24. One fixed hero skin. Never written in store.json: the Store derives one offer per skin in
        /// skins.json (StoreConfig.Validate), priced by the skin's rarity (SkinTiers).</summary>
        Skin
    }

    /// <summary>When a purchase limit comes back.</summary>
    public enum StoreReset
    {
        /// <summary>Never: the limit is for the life of the profile.</summary>
        Never,
        /// <summary>Monday to Sunday, by reward day.</summary>
        Week,
        /// <summary>The calendar month of the reward day, UTC.</summary>
        Month,
        /// <summary>The Wayfarer's Road season.</summary>
        Season,
        /// <summary>When the card the offer sells runs out.</summary>
        Expiry
    }

    /// <summary>One Growth Fund milestone: a stage the player clears, and the fixed reward it pays.</summary>
    public sealed class StoreMilestone
    {
        public string StageId = "";
        public double Hours;
        public double DustSweeps;
    }

    /// <summary>One offer. Everything it gives is a fixed number or a fixed list here: there is no field that
    /// could hold a chance, a pool or a roll.</summary>
    public sealed class StoreOfferDef
    {
        public string Id = "";
        public string Name = "";
        public string Blurb = "";
        /// <summary>A PLACEHOLDER, never a real store product id.</summary>
        public string ProductId = "";
        public string Tier = "";
        public double PriceEur;
        public StoreOfferKind Kind;
        public int Limit = 1;
        public StoreReset Reset = StoreReset.Never;
        /// <summary>True for a product a store can hand back to a new profile of the same account.</summary>
        public bool Restorable;
        /// <summary>R24: the switch that lists this offer. "relicPacks" (RelicPacksSwitch) and, for a derived skin
        /// offer, "skins" (SkinsSwitch). Empty for an offer with no switch of its own.</summary>
        public string Group = "";
        /// <summary>R24: a feature that must be open for the offer to be bought (store.json "requires", for example
        /// "Relics" for the Relic Shop). The offer is still LISTED while it is locked, with the feature's lock line.</summary>
        public bool HasRequires;
        public Feature Requires;
        /// <summary>R24: a stage whose clear opens the offer (store.json "requiresStage", for example "ch4_1" for the Hero
        /// Contract). Empty for none. Listed while locked, like Requires.</summary>
        public string RequiresStage = "";
        /// <summary>Relic Of Choice only: the most Relic Dust a pick may be worth at the Relic Shop ("pickCapDust"). A pick
        /// dearer than this is not on the list, so one pack never names a whole ladder step. 0 means no cap.</summary>
        public int PickCapDust;
        /// <summary>Pack: fixed Relic Dust, paid as Relic Dust (never marked: Dust only buys the Relic Shop's fixed items).</summary>
        public int Dust;
        /// <summary>Skin offer only: the skin it sells.</summary>
        public string SkinId = "";

        /// <summary>The offer as the purchase path needs it: id, placeholder product id, price tier and the
        /// fixed grant. Built from the fields below by StoreConfig; never written by hand.</summary>
        public StoreOffer Built = new StoreOffer();

        /// <summary>Hours of idle Gold at the buyer's frontier, paid as Sealed Gold.</summary>
        public double Hours;
        /// <summary>Slag, paid as Sealed Slag. Sealed Slag has no deterministic sink yet (a reroll and a combine
        /// both refuse it), so the shipped file sells none.</summary>
        public int Slag;
        /// <summary>Repeat-clear payouts of the frontier stage, paid as Relic Dust (never marked).</summary>
        public double DustSweeps;
        /// <summary>The named heroes a Starter offer lets the buyer pick one of.</summary>
        public readonly List<string> HeroChoices = new List<string>();
        /// <summary>Copies of the chosen hero (Starter) or of the contract's hero (Contract), paid Sealed.</summary>
        public int HeroCopies = 1;

        /// <summary>Ledger: reward days the card runs.</summary>
        public int Days = 30;
        /// <summary>Ledger: hours of idle Gold each claimed day pays.</summary>
        public double HoursPerDay;
        /// <summary>Ledger: percent added to the Gold of every idle claim while the card runs.</summary>
        public int IdleBonusPct;

        public readonly List<StoreMilestone> Milestones = new List<StoreMilestone>();

        /// <summary>Contract: the star count of a hero it may be bought for.</summary>
        public int Stars = 5;

        /// <summary>Event: which live event slot it sells.</summary>
        public LiveEventSlot EventSlot = LiveEventSlot.Weekly;
        /// <summary>Event: the paid track, or the pack.</summary>
        public LiveEventPart EventPart = LiveEventPart.Track;
        /// <summary>Contract: how many may be bought in a period across ALL heroes (the account), on top of Limit per
        /// hero. 0 means no account cap. R24: 4 a month.</summary>
        public int AccountLimit;
    }

    /// <summary>
    /// store.json: what the Store sells (Docs/DESIGN-STORE.md).
    ///
    /// NOT HASHED INTO ContentDatabase.ContentVersion, for guild-path.json's reason: nothing here is read by a
    /// fight. Loaded through ContentDatabase.LoadStore after Load. Empty (IsUsable false) on a database that
    /// never loaded the file, which the Store reads as "nothing to sell".
    ///
    /// THE RULE: every offer is fixed. A price tier is a placeholder euro amount the owner sets later, a
    /// product id is a placeholder, and no field here holds a chance. What a purchase grants is marked Sealed by
    /// StoreOffers.Fulfil through SealedGrants.
    /// </summary>
    public sealed class StoreConfig
    {
        public readonly Dictionary<string, double> Tiers = new Dictionary<string, double>();
        /// <summary>R24: the price tier of a skin by its rarity ("skinTiers" in store.json).</summary>
        public readonly Dictionary<string, string> SkinTiers = new Dictionary<string, string>();
        public readonly List<StoreOfferDef> Offers = new List<StoreOfferDef>();
        public readonly List<string> Warnings = new List<string>();

        public bool IsUsable
        {
            get { return Offers.Count > 0; }
        }

        public StoreOfferDef Offer(string id)
        {
            for (int i = 0; i < Offers.Count; i++) if (Offers[i].Id == id) return Offers[i];
            return null;
        }

        public static StoreConfig FromJson(string text)
        {
            StoreConfig c = new StoreConfig();
            if (string.IsNullOrEmpty(text)) return c;
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { c.Warnings.Add("store.json: " + err); return c; }

            JsonValue tiers = root["tiers"];
            if (tiers != null && tiers.IsObject)
            {
                foreach (string key in tiers.Keys)
                {
                    double eur = tiers[key].AsDouble(0.0);
                    if (eur <= 0.0) { c.Warnings.Add("store.json: price tier '" + key + "' needs a positive euro amount and is dropped"); continue; }
                    c.Tiers[key] = eur;
                }
            }

            JsonValue skinTiers = root["skinTiers"];
            if (skinTiers != null && skinTiers.IsObject)
            {
                foreach (string key in skinTiers.Keys)
                {
                    string tier = skinTiers[key].AsString("");
                    if (!c.Tiers.ContainsKey(tier)) { c.Warnings.Add("store.json: skin rarity '" + key + "' names price tier '" + tier + "' that the file does not define and is dropped"); continue; }
                    c.SkinTiers[key] = tier;
                }
            }

            List<JsonValue> offers = root.GetList("offers");
            for (int i = 0; i < offers.Count; i++)
            {
                StoreOfferDef d = ReadOffer(offers[i], c, i + 1);
                if (d == null) continue;
                if (c.Offer(d.Id) != null) { c.Warnings.Add("store.json: offer '" + d.Id + "' is listed twice and the second is dropped"); continue; }
                c.Offers.Add(d);
            }
            return c;
        }

        private static StoreOfferDef ReadOffer(JsonValue j, StoreConfig c, int number)
        {
            StoreOfferDef d = new StoreOfferDef();
            d.Id = j.GetString("id", "");
            string where = "store.json: offer " + number + (d.Id.Length > 0 ? " '" + d.Id + "'" : "");
            if (d.Id.Length == 0) { c.Warnings.Add(where + " has no id and is dropped"); return null; }
            d.Name = j.GetString("name", d.Id);
            d.Blurb = j.GetString("blurb", "");
            d.ProductId = j.GetString("productId", "");
            d.Tier = j.GetString("tier", "");

            string kind = j.GetString("kind", "");
            switch (kind)
            {
                case "pack": d.Kind = StoreOfferKind.Pack; break;
                case "starter": d.Kind = StoreOfferKind.Starter; break;
                case "ledger": d.Kind = StoreOfferKind.Ledger; break;
                case "growth": d.Kind = StoreOfferKind.Growth; break;
                case "pass": d.Kind = StoreOfferKind.Pass; break;
                case "contract": d.Kind = StoreOfferKind.Contract; break;
                case "event": d.Kind = StoreOfferKind.Event; break;
                case "relicChoice": d.Kind = StoreOfferKind.RelicChoice; break;
                default: c.Warnings.Add(where + " has unknown kind '" + kind + "' and is dropped"); return null;
            }

            string reset = j.GetString("reset", "never");
            switch (reset)
            {
                case "never": d.Reset = StoreReset.Never; break;
                case "week": d.Reset = StoreReset.Week; break;
                case "month": d.Reset = StoreReset.Month; break;
                case "season": d.Reset = StoreReset.Season; break;
                case "expiry": d.Reset = StoreReset.Expiry; break;
                default: c.Warnings.Add(where + " has unknown reset '" + reset + "' and is dropped"); return null;
            }
            d.Limit = Math.Max(1, j.GetInt("limit", 1));
            d.Restorable = j.GetBool("restorable", false);
            d.Group = j.GetString("group", "");
            d.RequiresStage = j.GetString("requiresStage", "");
            d.PickCapDust = Math.Max(0, j.GetInt("pickCapDust", 0));
            string requires = j.GetString("requires", "");
            if (requires.Length > 0)
            {
                Feature f;
                if (!Enum.TryParse(requires, true, out f)) { c.Warnings.Add(where + " requires unknown feature '" + requires + "' and is dropped"); return null; }
                d.HasRequires = true;
                d.Requires = f;
            }

            double eur;
            if (!c.Tiers.TryGetValue(d.Tier, out eur)) { c.Warnings.Add(where + " names price tier '" + d.Tier + "' that the file does not define and is dropped"); return null; }
            d.PriceEur = eur;
            if (d.ProductId.Length == 0) { c.Warnings.Add(where + " has no placeholder product id and is dropped"); return null; }

            d.Hours = Math.Max(0.0, j["hours"].AsDouble(0.0));
            d.Slag = Math.Max(0, j.GetInt("slag", 0));
            d.Dust = Math.Max(0, j.GetInt("dust", 0));
            d.DustSweeps = Math.Max(0.0, j["dustSweeps"].AsDouble(0.0));
            d.HeroCopies = Math.Max(1, j.GetInt("heroCopies", 1));
            d.Days = Math.Max(1, j.GetInt("days", 30));
            d.HoursPerDay = Math.Max(0.0, j["hoursPerDay"].AsDouble(0.0));
            d.IdleBonusPct = Math.Max(0, Math.Min(100, j.GetInt("idleBonusPct", 0)));
            d.Stars = Math.Max(1, Math.Min(5, j.GetInt("stars", 5)));
            d.AccountLimit = Math.Max(0, Math.Min(1000, j.GetInt("accountLimit", 0)));

            if (d.Kind == StoreOfferKind.Event)
            {
                string slotName = j.GetString("eventSlot", "");
                string partName = j.GetString("eventPart", "");
                if (!LiveEventsConfig.TrySlot(slotName, out d.EventSlot)) { c.Warnings.Add(where + " is an event offer with unknown slot '" + slotName + "' and is dropped"); return null; }
                if (partName == "track") d.EventPart = LiveEventPart.Track;
                else if (partName == "pack") d.EventPart = LiveEventPart.Pack;
                else { c.Warnings.Add(where + " is an event offer with unknown part '" + partName + "' and is dropped"); return null; }
            }

            List<JsonValue> choices = j.GetList("heroChoices");
            for (int i = 0; i < choices.Count; i++)
            {
                string id = choices[i].AsString("");
                if (id.Length > 0 && !d.HeroChoices.Contains(id)) d.HeroChoices.Add(id);
            }

            List<JsonValue> milestones = j.GetList("milestones");
            for (int i = 0; i < milestones.Count; i++)
            {
                StoreMilestone m = new StoreMilestone();
                m.StageId = milestones[i].GetString("stage", "");
                m.Hours = Math.Max(0.0, milestones[i]["hours"].AsDouble(0.0));
                m.DustSweeps = Math.Max(0.0, milestones[i]["dustSweeps"].AsDouble(0.0));
                if (m.StageId.Length == 0 || (m.Hours <= 0.0 && m.DustSweeps <= 0.0))
                {
                    c.Warnings.Add(where + " milestone " + (i + 1) + " names no stage or pays nothing and is dropped");
                    continue;
                }
                d.Milestones.Add(m);
            }

            // Each kind needs what it sells, and a kind that sells a pick needs the list to pick from.
            bool pays = d.Hours > 0.0 || d.Slag > 0 || d.DustSweeps > 0.0 || d.Dust > 0;
            switch (d.Kind)
            {
                case StoreOfferKind.Pack:
                    if (!pays) { c.Warnings.Add(where + " is a pack that pays nothing and is dropped"); return null; }
                    break;
                case StoreOfferKind.Starter:
                    if (d.HeroChoices.Count == 0) { c.Warnings.Add(where + " is a starter without heroes to pick from and is dropped"); return null; }
                    break;
                case StoreOfferKind.Ledger:
                    if (d.HoursPerDay <= 0.0 && d.IdleBonusPct <= 0) { c.Warnings.Add(where + " is a ledger that pays nothing and is dropped"); return null; }
                    d.Reset = StoreReset.Expiry;
                    d.Limit = 1;
                    break;
                case StoreOfferKind.Growth:
                    if (d.Milestones.Count == 0) { c.Warnings.Add(where + " is a growth fund without milestones and is dropped"); return null; }
                    if (d.Milestones.Count > 30) { c.Warnings.Add(where + " has more than 30 milestones and is dropped"); return null; }
                    break;
                case StoreOfferKind.Pass:
                    d.Reset = StoreReset.Season;
                    d.Limit = 1;
                    break;
                case StoreOfferKind.Contract:
                    if (d.Reset != StoreReset.Month) { c.Warnings.Add(where + " is a contract that does not reset monthly and is dropped"); return null; }
                    break;
                case StoreOfferKind.Event:
                    d.Reset = StoreReset.Never;   // once per event, counted by the live event state
                    d.Limit = 1;
                    break;
                case StoreOfferKind.RelicChoice:
                    if (d.Reset != StoreReset.Week && d.Reset != StoreReset.Month) { c.Warnings.Add(where + " is a relic choice that does not reset weekly or monthly and is dropped"); return null; }
                    break;
            }
            Build(d);
            return d;
        }

        /// <summary>Fills Built from the offer's fields: the purchase's fixed grant. A pack gives idle Gold hours,
        /// Slag and Dust sweeps; a starter adds one hero of a named list; a ledger, a fund and the pass give
        /// nothing at purchase (Activate, or the pass's premium) and pay later through the same grant path; a
        /// contract is a copy of an owned hero of its star count. Nothing here can be a roll: the kinds are the
        /// closed set of GrantKind.</summary>
        public static void Build(StoreOfferDef d)
        {
            StoreOffer o = new StoreOffer();
            o.Id = d.Id;
            o.ProductId = d.ProductId;
            o.PriceTier = d.PriceEur.ToString("0.00", CultureInfo.InvariantCulture);
            o.Restorable = d.Restorable;
            PurchaseGrant g = o.Grant;
            switch (d.Kind)
            {
                case StoreOfferKind.Pack:
                case StoreOfferKind.Starter:
                    if (d.Hours > 0.0) g.Add(new GrantItem(GrantKind.IdleGoldHours, d.Hours));
                    if (d.Slag > 0) g.Add(new GrantItem(GrantKind.Slag, d.Slag));
                    if (d.Dust > 0) g.Add(new GrantItem(GrantKind.Dust, d.Dust));
                    if (d.DustSweeps > 0.0) g.Add(new GrantItem(GrantKind.DustSweeps, d.DustSweeps));
                    if (d.Kind == StoreOfferKind.Starter)
                    {
                        GrantItem pick = new GrantItem(GrantKind.HeroChoice, 0);
                        pick.HeroIds.AddRange(d.HeroChoices);
                        g.Add(pick);
                    }
                    break;
                case StoreOfferKind.Ledger:
                case StoreOfferKind.Growth:
                    g.Add(new GrantItem(GrantKind.Activate, 0));
                    break;
                case StoreOfferKind.Pass:
                    g.Add(new GrantItem(GrantKind.PassPremium, 0));
                    break;
                case StoreOfferKind.Contract:
                    GrantItem owned = new GrantItem(GrantKind.OwnedHeroCopy, 0);
                    owned.Stars = d.Stars;
                    g.Add(owned);   // one copy: a grant takes at most one pick from the buyer
                    break;
                case StoreOfferKind.Event:
                    g.Add(new GrantItem(d.EventPart == LiveEventPart.Track ? GrantKind.EventTrack : GrantKind.EventPack, d.EventSlot == LiveEventSlot.Monthly ? 1 : 0));
                    break;
                case StoreOfferKind.RelicChoice:
                    g.Add(new GrantItem(GrantKind.RelicChoice, d.PickCapDust));   // the pick is a relic id, named by the buyer; Amount is its Dust cap
                    break;
                case StoreOfferKind.Skin:
                    GrantItem skin = new GrantItem(GrantKind.Skin, 0);
                    skin.SkinId = d.SkinId;
                    g.Add(skin);
                    break;
            }
            d.Built = o;
        }

        /// <summary>Drops what this content cannot honour: a hero that is not in the roster, not of the offer's
        /// star count, or a starting hero (a copy of one is not a prize); a milestone at a stage that does not
        /// exist. Adds the warnings. Called by ContentDatabase.LoadStore.</summary>
        public void Validate(ContentDatabase db)
        {
            AddSkinOffers(db);
            for (int i = Offers.Count - 1; i >= 0; i--)
            {
                StoreOfferDef d = Offers[i];
                if (d.RequiresStage.Length > 0 && db.GetStage(d.RequiresStage) == null)
                {
                    Warnings.Add("store.json: offer '" + d.Id + "' requires stage '" + d.RequiresStage + "' that does not exist and is dropped");
                    Offers.RemoveAt(i);
                    continue;
                }
                if (d.Kind == StoreOfferKind.Starter)
                {
                    for (int h = d.HeroChoices.Count - 1; h >= 0; h--)
                    {
                        HeroDef hero = db.GetHero(d.HeroChoices[h]);
                        if (hero == null || hero.Stars != 5 || hero.StartingHero)
                        {
                            Warnings.Add("store.json: offer '" + d.Id + "' lists hero '" + d.HeroChoices[h] + "' that is not a non-starting 5-star and it is dropped from the list");
                            d.HeroChoices.RemoveAt(h);
                        }
                    }
                    if (d.HeroChoices.Count == 0) { Warnings.Add("store.json: offer '" + d.Id + "' has no valid hero left and is dropped"); Offers.RemoveAt(i); continue; }
                    Build(d);
                }
                if (d.Kind == StoreOfferKind.Growth)
                {
                    for (int m = d.Milestones.Count - 1; m >= 0; m--)
                    {
                        if (db.GetStage(d.Milestones[m].StageId) != null) continue;
                        Warnings.Add("store.json: offer '" + d.Id + "' names stage '" + d.Milestones[m].StageId + "' that does not exist and it is dropped");
                        d.Milestones.RemoveAt(m);
                    }
                    if (d.Milestones.Count == 0) { Offers.RemoveAt(i); continue; }
                }
            }
        }

        /// <summary>R24: one offer per skin the Store sells, derived from skins.json (db.Skins, loaded first), so a skin
        /// is data only. Priced by rarity (SkinTiers); a skin whose rarity has no tier is not sold. A test skin gets an
        /// offer too, so a development build proves the whole purchase path, and is unlisted wherever the skin is hidden
        /// (StoreOffers.Listed). Event skins are not sold: their only way in is a grant (PurchaseGrants.Apply).</summary>
        private void AddSkinOffers(ContentDatabase db)
        {
            for (int i = 0; i < db.Skins.Skins.Count; i++)
            {
                SkinDef skin = db.Skins.Skins[i];
                if (skin.Source == SkinSource.Event) continue;
                string tier;
                double eur;
                if (!SkinTiers.TryGetValue(skin.Rarity, out tier) || !Tiers.TryGetValue(tier, out eur))
                {
                    Warnings.Add("store.json: skin '" + skin.Id + "' has rarity '" + skin.Rarity + "' with no price tier and is not sold");
                    continue;
                }
                StoreOfferDef d = new StoreOfferDef();
                d.Id = "skin_" + skin.Id;
                if (Offer(d.Id) != null) continue;
                d.Name = skin.Name;
                d.Blurb = skin.Blurb.Length > 0 ? skin.Blurb : "A skin for one hero. " + skin.BonusLine() + ", on that hero only.";
                d.ProductId = "placeholder.emberfall.store." + d.Id;
                d.Tier = tier;
                d.PriceEur = eur;
                d.Kind = StoreOfferKind.Skin;
                d.Limit = 1;
                d.Reset = StoreReset.Never;
                d.Restorable = true;
                d.Group = "skins";
                d.SkinId = skin.Id;
                Build(d);
                Offers.Add(d);
            }
        }

        public static string Eur(double eur)
        {
            return "EUR " + eur.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }
}
