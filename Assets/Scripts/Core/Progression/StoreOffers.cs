using System;
using System.Collections.Generic;
using System.Globalization;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.Core.Progression
{
    /// <summary>Why a store call was refused. None is success.</summary>
    public enum StoreRefusal
    {
        None,
        /// <summary>The switch is off or the file sells nothing.</summary>
        Off,
        /// <summary>No such offer (or one behind a switch that is off).</summary>
        Unknown,
        /// <summary>The limit for this period is used up (or the card is still running).</summary>
        SoldOut,
        /// <summary>The offer needs a hero picked and none was given.</summary>
        NeedsChoice,
        /// <summary>The hero given is not one this offer may give.</summary>
        BadChoice,
        /// <summary>A contract for a hero the buyer does not own.</summary>
        NotOwned,
        /// <summary>The pass has no season running (the switch is off or the clock is before the epoch).</summary>
        PassClosed,
        /// <summary>The pass cannot earn a point while the daily orders are off, so it is not sold.</summary>
        PassInert,
        /// <summary>A claim on something that was not bought.</summary>
        NotBought,
        /// <summary>A claim on a milestone or tier not reached yet.</summary>
        NotReached,
        /// <summary>A claim already made.</summary>
        Claimed,
        /// <summary>A claim with nothing to claim (an expired card, a day already claimed).</summary>
        Nothing,
        /// <summary>No live event runs in the slot the offer sells.</summary>
        NoEvent,
        /// <summary>The account has used the month's contracts across all heroes (R24).</summary>
        AccountCap,
        /// <summary>The offer needs a feature that is not open yet (the relic packs wait for the Relic Shop) (R24).</summary>
        Locked,
        /// <summary>A Relic of Choice with nothing to pick: every relic owned at its top tier (R24).</summary>
        NoPick,
        /// <summary>A skin the buyer already owns (R24).</summary>
        AlreadyOwned,
        /// <summary>The track and the pack of a live event stop selling an hour before the event ends (R24).</summary>
        SalesClosed
    }

    /// <summary>A hero an offer may give.</summary>
    public sealed class StoreChoice
    {
        public string HeroId = "";
        public string Name = "";
        public string Role = "";
    }

    /// <summary>One Growth Fund milestone as the screen draws it.</summary>
    public sealed class StoreMilestoneView
    {
        public int Index;
        public string StageId = "";
        public string StageName = "";
        public string Line = "";
        public bool Reached;
        public bool Claimed;
        public bool CanClaim;
    }

    /// <summary>One paid-track tier as the Pass tab draws it.</summary>
    public sealed class StorePaidTierView
    {
        public int Tier;
        public PassRewardKind Kind;
        public int Stars;
        public string Line = "";
        /// <summary>The same reward in few words, for a narrow tile: "2 h idle Gold", "A 5-star of your pick".</summary>
        public string Short = "";
        public bool Reached;
        public bool Claimed;
        public bool CanClaim;
    }

    /// <summary>One offer as the Packs tab draws it. Read only.</summary>
    public sealed class StoreOfferView
    {
        public string Id = "";
        public string Name = "";
        public string Blurb = "";
        public StoreOfferKind Kind;
        public string ProductId = "";
        public string Tier = "";
        public double PriceEur;
        public string PriceLabel = "";
        /// <summary>The full fixed contents, in words. Shown before the purchase, with the price.</summary>
        public readonly List<string> Lines = new List<string>();
        /// <summary>The heroes the buyer picks one of (a starter) or may spend a contract on.</summary>
        public readonly List<StoreChoice> Choices = new List<StoreChoice>();
        public bool NeedsChoice;
        /// <summary>"Once", "1 a week", "1 a month", "One a season", "2 a month per hero", "One card at a time".</summary>
        public string LimitLabel = "";
        public bool Available;
        public StoreRefusal Reason = StoreRefusal.None;
        /// <summary>Seconds until the limit comes back, when it is used up and resets by the calendar. Else 0.</summary>
        public long ResetInSeconds;
        /// <summary>Ledger only: reward days left on a running card (today included), else 0.</summary>
        public int DaysLeft;
        /// <summary>R24: the feature's lock line ("UNLOCKS AT 3-1") while the offer is locked, else empty.</summary>
        public string LockLine = "";
        /// <summary>R24, skin offers: the skin sold, the hero it is for, and whether the buyer owns that hero.</summary>
        public string SkinId = "";
        public string SkinHeroId = "";
        public string SkinHeroName = "";
        /// <summary>Ledger only: today's hours can be claimed.</summary>
        public bool CanClaim;
        public readonly List<StoreMilestoneView> Milestones = new List<StoreMilestoneView>();
        /// <summary>Growth fund and ledger and pass: the offer has been bought and is in force.</summary>
        public bool Owned;
    }

    /// <summary>The whole store at one moment, for the screen to read.</summary>
    public sealed class StoreView
    {
        public bool Active;
        public readonly List<StoreOfferView> Offers = new List<StoreOfferView>();

        /// <summary>The Pass tab: the season's tier the player has reached, the paid track, and whether it is bought.</summary>
        public bool PassOpen;
        public bool PassOwned;
        public bool PassNeedsLoop;
        public long PassSeason;
        public int PassDaysLeft;
        public int PassReached;
        public readonly List<StorePaidTierView> Paid = new List<StorePaidTierView>();
        public bool AnythingToClaim;
    }

    /// <summary>The outcome of a claim (or of the harness's direct fulfilment).</summary>
    public sealed class StoreFulfilment
    {
        public bool Success;
        public StoreRefusal Reason = StoreRefusal.None;
        public GrantResult Grant;
    }

    /// <summary>What an offer is worth to a player at one frontier, in days of that frontier's income.</summary>
    public sealed class StoreValue
    {
        public double GoldHours;
        public double GoldDays;
        public double DustSweeps;
        public double DustDays;
        public int Slag;
        public int HeroCopies;
        public double HeroDays;
        public double TotalDays;
        /// <summary>R24: fixed Relic Dust the offer pays (a Satchel, a Chest), already counted in DustDays.</summary>
        public long FixedDust;
    }

    /// <summary>The store's offer rules, as the purchase path asks for them (IStoreRules). One shared
    /// instance; it holds no state, everything it knows is in the session's profile and content.</summary>
    public sealed class StoreRules : IStoreRules
    {
        /// <summary>
        /// ASKED ONCE, BY PurchaseLedger.Begin, AND IT HAS A SIDE EFFECT: when the purchase is allowed it calls
        /// StoreOffers.OnBegin, which remembers (in memory only) the event an event offer was begun for, so a payment
        /// that lands after the period turned over is delivered against it. Nothing may poll this to ask "could it be
        /// bought"; the reads for that are StoreOffers.Check and StoreOffers.CheckAny, which write nothing.
        /// </summary>
        public bool CanBuy(GameSession session, string offerId, string choiceHeroId, long nowUnix, out string reason)
        {
            // With a pick the rules check that pick (the Hero Contract's limit is per hero); without one, any.
            StoreRefusal r = string.IsNullOrEmpty(choiceHeroId)
                ? StoreOffers.CheckAny(session, offerId, nowUnix)
                : StoreOffers.Check(session, offerId, choiceHeroId, nowUnix);
            reason = r == StoreRefusal.None ? "" : StoreOffers.ReasonText(r);
            if (r == StoreRefusal.None) StoreOffers.OnBegin(session, offerId, nowUnix);
            return r == StoreRefusal.None;
        }

        public void RecordPurchase(GameSession session, string offerId, string choiceHeroId, long nowUnix)
        {
            StoreOffers.Record(session, offerId, choiceHeroId ?? "", nowUnix);
        }

        public StoreOffer Find(string offerId)
        {
            return StoreOffers.BuiltOffer(offerId);
        }

        public StoreOffer FindByProduct(string productId)
        {
            return StoreOffers.BuiltOfferByProduct(productId);
        }

        /// <summary>The rules the game runs with.</summary>
        public static readonly StoreRules Shared = new StoreRules();
    }

    /// <summary>
    /// THE STORE'S OFFER RULES (Docs/DESIGN-STORE.md): the offers, their fixed contents, their limits and
    /// resets, and the free payouts of the things that were bought (a ledger day, a growth milestone). Pure
    /// rules over a GameSession's profile and content; the screen only calls View, the claims and (through the
    /// purchase path) the ledger.
    ///
    /// THE RULE. This file grants nothing itself. Every good is granted by PurchaseGrants.Apply, which marks
    /// it Sealed through SealedGrants: the purchase (PurchaseLedger.Complete), a ledger day, a growth
    /// milestone, the ledger's share of an idle claim, a paid pass tier. Dust is the one good not marked,
    /// because its only sink sells fixed relics. There is no random call in this file; the offers come from
    /// store.json as fixed numbers and named heroes, and the same state gives the same answer.
    ///
    /// ONE CLOCK, FORWARD ONLY. The day is the reward day (GameSession.DayStartUnix). Every read and write
    /// uses the LATER of that and the store's clock stamp (the "clock" offer record), so a device clock wound
    /// back reopens no limit, revives no expired card and pays no day twice. Periods: a week is Monday to
    /// Sunday by reward day (DailyLoop.WeekOf), a month is the calendar month of the reward day
    /// (SinkShop.MonthKeyOf), a season is Wayfarer's Road's.
    ///
    /// OFF MEANS INERT. With StoreSwitch off, or without a usable store.json, every call returns at once and
    /// writes nothing, and OnIdleClaim adds nothing.
    /// </summary>
    public static class StoreOffers
    {
        public const string PassOfferId = "pass_premium";
        public const string ClockKey = "clock";
        public const string ContractPrefix = "hero_contract|";

        public static bool Active(GameSession session)
        {
            return StoreSwitch.Enabled && session != null && session.Content.Store.IsUsable;
        }

        /// <summary>Whether an offer is on sale in this build: the contract by its switch, the relic packs by theirs, a
        /// skin by the skins switch and by the skin itself being present (a test skin is hidden in a release build).
        /// Every switch off leaves the Store listing what it listed before R24.</summary>
        private static bool Listed(StoreOfferDef d, SkinsConfig skins)
        {
            if (d.Kind == StoreOfferKind.Contract && !HeroContractSwitch.Enabled) return false;
            if (d.Group == "relicPacks" && !RelicPacksSwitch.Enabled) return false;
            if (d.Kind == StoreOfferKind.Skin) return SkinsSwitch.Enabled && skins != null && skins.Skin(d.SkinId) != null;
            return true;
        }

        private static StoreOfferDef Find(GameSession session, string offerId)
        {
            StoreOfferDef d = session.Content.Store.Offer(offerId);
            return d != null && Listed(d, session.Content.Skins) ? d : null;
        }

        // The purchase path finds an offer without a session, so the rules keep the content they were last
        // bound to. It is only ever the session's own content.
        private static StoreConfig _config;
        private static SkinsConfig _skins;

        /// <summary>Points the session-less lookups (IStoreRules.Find) at a session's content. The game calls it
        /// when it builds the Store; the harness calls it before it drives a PurchaseLedger.</summary>
        public static void Bind(GameSession session)
        {
            _config = session != null ? session.Content.Store : null;
            _skins = session != null ? session.Content.Skins : null;
        }

        public static StoreOffer BuiltOffer(string offerId)
        {
            if (_config == null || offerId == null) return null;
            StoreOfferDef d = _config.Offer(offerId);
            return d != null && Listed(d, _skins) ? d.Built : null;
        }

        public static StoreOffer BuiltOfferByProduct(string productId)
        {
            if (_config == null || productId == null) return null;
            for (int i = 0; i < _config.Offers.Count; i++)
                if (_config.Offers[i].Built.ProductId == productId && Listed(_config.Offers[i], _skins)) return _config.Offers[i].Built;
            return null;
        }

#if UNITY_EDITOR
        /// <summary>CAPTURE ONLY (editor builds, never a player): gives the session a skin through the one grant path
        /// (PurchaseGrants.Apply), as a purchase or an event reward does, so a capture target can show a worn skin
        /// without depending on a store backend. The caller guards the scratch identity.</summary>
        public static bool GrantSkinForCapture(GameSession session, string skinId, long nowUnix)
        {
            if (session == null || string.IsNullOrEmpty(skinId)) return false;
            GrantItem item = new GrantItem(GrantKind.Skin, 0);
            item.SkinId = skinId;
            return PurchaseGrants.Apply(session, new PurchaseGrant().Add(item), null, nowUnix).Success;
        }
#endif

        // ------------------------------------------------------------------ the clock

        /// <summary>The reward day to use: the real one, or the latest the store ever saw if that is later.</summary>
        public static long EffectiveDay(GameSession session, long nowUnix)
        {
            OfferRecord clock = session.Profile.Store.Find(ClockKey);
            return Math.Max(session.DayStartUnix(nowUnix), clock != null ? clock.LastDay : 0L);
        }

        private static void Stamp(GameSession session, long effectiveDay)
        {
            OfferRecord clock = session.Profile.Store.Find(ClockKey);
            if (clock != null && effectiveDay <= clock.LastDay) return;
            session.Profile.Store.Record(ClockKey).LastDay = effectiveDay;
        }

        private static long PeriodOf(GameSession session, StoreOfferDef d, long effectiveDay)
        {
            switch (d.Reset)
            {
                case StoreReset.Week: return DailyLoop.WeekOf(session, effectiveDay);
                case StoreReset.Month: return SinkShop.MonthKeyOf(effectiveDay);
                case StoreReset.Season: return WayfarersRoad.Active(session) ? WayfarersRoad.SeasonOf(session, effectiveDay) : 0L;
            }
            return 0L;
        }

        /// <summary>When the period an offer's limit is counted in ends (unix seconds), or 0 when it does not end.</summary>
        private static long PeriodEnd(GameSession session, StoreOfferDef d, long effectiveDay)
        {
            switch (d.Reset)
            {
                case StoreReset.Week:
                    return effectiveDay + (7L - ((DailyLoop.DayNumber(session, effectiveDay) + 3L) % 7L + 7L) % 7L) * 86400L;
                case StoreReset.Month:
                    return SinkShop.NextMonthUnix(session, effectiveDay);
                case StoreReset.Season:
                    return WayfarersRoad.Active(session) ? WayfarersRoad.SeasonEndUnix(session, WayfarersRoad.SeasonOf(session, effectiveDay)) : 0L;
            }
            return 0L;
        }

        private static string CountKey(StoreOfferDef d, string choice)
        {
            return d.Kind == StoreOfferKind.Contract ? ContractPrefix + (choice ?? "") : d.Id;
        }

        private static int CountOf(GameSession session, string key, long period)
        {
            OfferRecord r = session.Profile.Store.Find(key);
            return r == null || r.Period < period ? 0 : r.Count;
        }

        private static int BoughtIn(GameSession session, StoreOfferDef d, string choice, long effectiveDay)
        {
            return CountOf(session, CountKey(d, choice), PeriodOf(session, d, effectiveDay));
        }

        // ------------------------------------------------------------------ the ledger card

        private static OfferRecord LedgerRecord(GameSession session)
        {
            StoreOfferDef d = LedgerOffer(session);
            return d == null ? null : session.Profile.Store.Find(d.Id);
        }

        /// <summary>True when a card is running on this reward day.</summary>
        public static bool LedgerActive(GameSession session, long nowUnix)
        {
            StoreOfferDef d = LedgerOffer(session);
            OfferRecord r = LedgerRecord(session);
            if (d == null || r == null || r.StartDay <= 0) return false;
            long day = EffectiveDay(session, nowUnix);
            return day >= r.StartDay && day < r.StartDay + d.Days * 86400L;
        }

        /// <summary>Reward days left on a running card, today included. Zero when none runs.</summary>
        public static int LedgerDaysLeft(GameSession session, long nowUnix)
        {
            if (!LedgerActive(session, nowUnix)) return 0;
            StoreOfferDef d = LedgerOffer(session);
            return (int)((LedgerRecord(session).StartDay + d.Days * 86400L - EffectiveDay(session, nowUnix)) / 86400L);
        }

        private static StoreOfferDef LedgerOffer(GameSession session)
        {
            List<StoreOfferDef> all = session.Content.Store.Offers;
            for (int i = 0; i < all.Count; i++) if (all[i].Kind == StoreOfferKind.Ledger) return all[i];
            return null;
        }

        private static bool LedgerCanClaim(GameSession session, long nowUnix)
        {
            return LedgerActive(session, nowUnix) && EffectiveDay(session, nowUnix) > LedgerRecord(session).LastDay;
        }

        // ------------------------------------------------------------------ checks

        /// <summary>None when the offer may be bought now. Reads, writes nothing, takes no random input.</summary>
        public static StoreRefusal Check(GameSession session, string offerId, string choice, long nowUnix)
        {
            if (!Active(session)) return StoreRefusal.Off;
            StoreOfferDef d = Find(session, offerId);
            if (d == null) return StoreRefusal.Unknown;
            long day = EffectiveDay(session, nowUnix);
            if (IsLocked(session, d)) return StoreRefusal.Locked;

            switch (d.Kind)
            {
                case StoreOfferKind.RelicChoice:
                    if (string.IsNullOrEmpty(choice)) return StoreRefusal.NeedsChoice;
                    if (RelicPacks.Find(session, choice, d.PickCapDust) == null) return StoreRefusal.BadChoice;
                    break;
                case StoreOfferKind.Skin:
                    if (session.Profile.OwnsSkin(d.SkinId)) return StoreRefusal.AlreadyOwned;
                    SkinDef skin = session.Content.Skins.Skin(d.SkinId);
                    if (skin == null) return StoreRefusal.Unknown;
                    if (session.Profile.FindHeroByDefId(skin.HeroId) == null) return StoreRefusal.NotOwned;
                    break;
                case StoreOfferKind.Ledger:
                    return LedgerActive(session, nowUnix) ? StoreRefusal.SoldOut : StoreRefusal.None;
                case StoreOfferKind.Pass:
                    if (!WayfarersRoad.Active(session) || WayfarersRoad.SeasonOf(session, day) == 0L) return StoreRefusal.PassClosed;
                    if (!DailyLoop.Active(session)) return StoreRefusal.PassInert;
                    break;
                case StoreOfferKind.Starter:
                    if (string.IsNullOrEmpty(choice)) return StoreRefusal.NeedsChoice;
                    if (!d.HeroChoices.Contains(choice) || session.Content.GetHero(choice) == null) return StoreRefusal.BadChoice;
                    break;
                case StoreOfferKind.Contract:
                {
                    if (string.IsNullOrEmpty(choice)) return StoreRefusal.NeedsChoice;
                    HeroDef hero = session.Content.GetHero(choice);
                    if (hero == null || hero.Stars != d.Stars) return StoreRefusal.BadChoice;
                    if (session.Profile.FindHeroByDefId(choice) == null) return StoreRefusal.NotOwned;
                    break;
                }
                case StoreOfferKind.Event:
                    return LiveEvents.PurchaseRefusal(session, d.EventSlot, d.EventPart, nowUnix);
            }
            if (BoughtIn(session, d, choice, day) >= d.Limit) return StoreRefusal.SoldOut;
            if (d.Kind == StoreOfferKind.Contract && d.AccountLimit > 0
                && ContractsInPeriod(session, PeriodOf(session, d, day)) >= d.AccountLimit) return StoreRefusal.AccountCap;
            return StoreRefusal.None;
        }

        /// <summary>Contracts bought across every hero in the period (the month the store's clock is in). Reads the
        /// per-hero counters Record keeps, so the account total is derived and never stored: it cannot drift from
        /// them, and a counter from an earlier month counts for nothing.</summary>
        public static int ContractsInPeriod(GameSession session, long period)
        {
            int total = 0;
            List<OfferRecord> all = session.Profile.Store.Offers;
            for (int i = 0; i < all.Count; i++)
                if (all[i].OfferId.StartsWith(ContractPrefix, StringComparison.Ordinal) && all[i].Period >= period) total += all[i].Count;
            return total;
        }

        /// <summary>True when the offer waits for a feature that is not open (the relic packs wait for the Relic Shop).</summary>
        private static bool IsLocked(GameSession session, StoreOfferDef d)
        {
            if (FeatureWaitsFor(session, d) != null) return true;
            return d.RequiresStage.Length > 0 && !session.Profile.HasClearedStage(d.RequiresStage);
        }

        /// <summary>The stage the offer's required feature waits for, read from onboarding.json's table through
        /// FeatureGate.UnlockStage (the one place that decides an unlock), or null when the feature is open or the offer
        /// requires none. The Store does not decide an unlock itself: it asks the table.</summary>
        private static StageDef FeatureWaitsFor(GameSession session, StoreOfferDef d)
        {
            if (!d.HasRequires) return null;
            StageDef stage = FeatureGate.UnlockStage(session.Content, d.Requires);
            return stage != null && !session.Profile.HasClearedStage(stage.Id) ? stage : null;
        }

        /// <summary>"UNLOCKS AT 3-1": what a locked offer says, from the feature table or the stage it waits for.</summary>
        private static string LockLineOf(GameSession session, StoreOfferDef d)
        {
            if (FeatureWaitsFor(session, d) != null)
                return FeatureGate.LockLine(session.Content, d.Requires);
            StageDef stage = d.RequiresStage.Length > 0 ? session.Content.GetStage(d.RequiresStage) : null;
            return stage != null ? "UNLOCKS AT " + StageLabel(stage) : "";
        }

        /// <summary>Like Check for an offer whose pick is not made yet: open when at least one pick would pass.
        /// What the purchase path asks before it starts a purchase.</summary>
        public static StoreRefusal CheckAny(GameSession session, string offerId, long nowUnix)
        {
            if (!Active(session)) return StoreRefusal.Off;
            StoreOfferDef d = Find(session, offerId);
            if (d == null) return StoreRefusal.Unknown;
            if (d.Kind != StoreOfferKind.Starter && d.Kind != StoreOfferKind.Contract && d.Kind != StoreOfferKind.RelicChoice)
                return Check(session, offerId, null, nowUnix);
            if (IsLocked(session, d)) return StoreRefusal.Locked;

            List<string> picks = PicksOf(session, d);
            if (picks.Count == 0)
                return d.Kind == StoreOfferKind.Contract ? StoreRefusal.NotOwned : d.Kind == StoreOfferKind.RelicChoice ? StoreRefusal.NoPick : StoreRefusal.BadChoice;
            StoreRefusal first = StoreRefusal.None;
            for (int i = 0; i < picks.Count; i++)
            {
                StoreRefusal r = Check(session, offerId, picks[i], nowUnix);
                if (r == StoreRefusal.None) return r;
                if (i == 0) first = r;
            }
            return first;
        }

        private static List<string> PicksOf(GameSession session, StoreOfferDef d)
        {
            List<string> picks = new List<string>();
            if (d.Kind == StoreOfferKind.Starter) picks.AddRange(d.HeroChoices);
            else if (d.Kind == StoreOfferKind.RelicChoice)
            {
                List<RelicPick> relics = RelicPacks.Picks(session, d.PickCapDust);
                for (int i = 0; i < relics.Count; i++) picks.Add(relics[i].RelicId);
            }
            else if (d.Kind == StoreOfferKind.Contract)
            {
                List<HeroInstance> owned = session.Profile.Heroes;
                for (int i = 0; i < owned.Count; i++)
                {
                    HeroDef hero = session.Content.GetHero(owned[i].DefId);
                    if (hero != null && hero.Stars == d.Stars) picks.Add(hero.Id);
                }
            }
            return picks;
        }

        /// <summary>A sentence for a refusal, for the screen to print. The screen decides nothing.</summary>
        public static string ReasonText(StoreRefusal r)
        {
            switch (r)
            {
                case StoreRefusal.Off: return "The Store is closed.";
                case StoreRefusal.Unknown: return "Not for sale.";
                case StoreRefusal.SoldOut: return "Sold out.";
                case StoreRefusal.NeedsChoice: return "Pick a hero first.";
                case StoreRefusal.BadChoice: return "That hero is not on the list.";
                case StoreRefusal.NotOwned: return "You do not own that hero.";
                case StoreRefusal.PassClosed: return "No season is running.";
                case StoreRefusal.PassInert: return "The pass needs the daily orders.";
                case StoreRefusal.NotBought: return "Not bought.";
                case StoreRefusal.NotReached: return "Not reached yet.";
                case StoreRefusal.Claimed: return "Already claimed.";
                case StoreRefusal.Nothing: return "Nothing to claim.";
                case StoreRefusal.NoEvent: return "No event is running.";
                case StoreRefusal.AccountCap: return "This month's contracts are used up.";
                case StoreRefusal.Locked: return "Not open yet.";
                case StoreRefusal.NoPick: return "Every relic you own is at its top tier.";
                case StoreRefusal.AlreadyOwned: return "You already own this skin.";
                case StoreRefusal.SalesClosed: return "Sales for this event are closed: they close 1 hour before the end.";
            }
            return "";
        }

        // ------------------------------------------------------------------ recording a purchase

        /// <summary>Called when a purchase is BEGUN and allowed (StoreRules.CanBuy). Writes nothing to the save: an event
        /// offer remembers, in memory only, which event the buyer saw (LiveEvents.Bind), so a payment that lands after the
        /// period turned over is delivered against that event.</summary>
        public static void OnBegin(GameSession session, string offerId, long nowUnix)
        {
            if (!Active(session)) return;
            StoreOfferDef d = Find(session, offerId);
            if (d != null && d.Kind == StoreOfferKind.Event) LiveEvents.Bind(session, d.EventSlot, d.EventPart, nowUnix);
        }

        /// <summary>Called by the purchase path once a grant is applied (IStoreRules.RecordPurchase): counts the
        /// purchase against its limit, in the period the store's clock is in, and starts a card. Writes only
        /// through PlayerProfile.Store.</summary>
        public static void Record(GameSession session, string offerId, string choice, long nowUnix)
        {
            if (!Active(session)) return;
            StoreOfferDef d = Find(session, offerId);
            if (d == null) return;
            if (d.Kind == StoreOfferKind.Event) return;   // once per event: the grant marked the live event state
            long day = EffectiveDay(session, nowUnix);
            Stamp(session, day);
            StoreState st = session.Profile.Store;
            if (d.Kind == StoreOfferKind.Ledger)
            {
                OfferRecord r = st.Record(d.Id);
                r.StartDay = day;
                r.LastDay = 0;
                return;
            }
            OfferRecord rec = st.Record(CountKey(d, choice));
            long period = PeriodOf(session, d, day);
            if (rec.Period < period) { rec.Period = period; rec.Count = 0; }
            rec.Count = Math.Min(100000, rec.Count + 1);
        }

        /// <summary>The harness's and the capture targets' direct fulfilment: the checks, the one grant path
        /// (PurchaseGrants.Apply) and the record, with no store and no receipt in between. The game never calls
        /// it; the purchase path is PurchaseLedger.Complete. Internal so no screen can reach it.</summary>
        internal static StoreFulfilment Fulfil(GameSession session, string offerId, string choice, long nowUnix)
        {
            StoreFulfilment r = new StoreFulfilment();
            r.Reason = Check(session, offerId, choice, nowUnix);
            if (r.Reason != StoreRefusal.None) return r;
            StoreOfferDef d = Find(session, offerId);
            GrantResult g = PurchaseGrants.Apply(session, d.Built.Grant, choice, nowUnix);
            r.Grant = g;
            if (!g.Success) { r.Reason = g.Reason == GrantRefusal.BadChoice ? StoreRefusal.BadChoice : StoreRefusal.PassClosed; return r; }
            Record(session, offerId, choice, nowUnix);
            r.Success = true;
            return r;
        }

        // ------------------------------------------------------------------ the words

        private static string Num(double v)
        {
            return v.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private static string Big(long v)
        {
            return v.ToString("N0", CultureInfo.InvariantCulture);
        }

        private static string HoursText(double hours)
        {
            return Num(hours) + (hours == 1.0 ? " hour" : " hours");
        }

        private static long GoldOfHours(GameSession session, double hours)
        {
            return hours <= 0.0 ? 0L : session.IdleYield((long)Math.Round(hours * 3600.0)).Gold;
        }

        /// <summary>"2-12": the chapter number and the stage's place in it, the way the campaign map prints it.</summary>
        public static string StageLabel(StageDef stage)
        {
            int chapter;
            if (!stage.ChapterId.StartsWith("ch") || !int.TryParse(stage.ChapterId.Substring(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out chapter)) chapter = 1;
            return chapter + "-" + stage.Index;
        }

        /// <summary>The Gold a Growth Fund milestone pays: its hours of idle Gold priced at the MILESTONE'S OWN stage, never
        /// at the frontier the claim is made from, so a player who hoards claims gets each milestone at its own rate.</summary>
        public static long MilestoneGold(GameSession session, StoreMilestone m)
        {
            StageDef stage = session.Content.GetStage(m.StageId);
            if (stage == null || m.Hours <= 0.0) return 0L;
            return (long)Math.Floor(StatMath.IdleGoldPerSecond(session.Balance, stage) * (float)Math.Round(m.Hours * 3600.0));
        }

        /// <summary>The Dust a Growth Fund milestone pays: its sweeps at the milestone stage's own repeat-clear payout.</summary>
        public static long MilestoneDust(GameSession session, StoreMilestone m)
        {
            StageDef stage = session.Content.GetStage(m.StageId);
            if (stage == null || m.DustSweeps <= 0.0) return 0L;
            return (long)Math.Round(m.DustSweeps * session.RelicDustForRepeatClear(stage));
        }

        private static string MilestoneLine(GameSession session, StoreMilestone m)
        {
            StageDef stage = session.Content.GetStage(m.StageId);
            string pays = "";
            if (m.Hours > 0.0) pays = HoursText(m.Hours) + " of idle Gold at that stage = " + Big(MilestoneGold(session, m)) + " Gold (Sealed)";
            if (m.DustSweeps > 0.0)
                pays += (pays.Length > 0 ? ", " : "") + Num(m.DustSweeps) + " sweeps of Dust = " + Big(MilestoneDust(session, m)) + " Dust";
            return "Clear " + (stage != null ? StageLabel(stage) : m.StageId) + ": " + pays;
        }

        /// <summary>The fixed contents of an offer in words, for the card, before a purchase. Includes what a
        /// card, a fund or a pass will give later.</summary>
        public static List<string> ContentLines(GameSession session, StoreOfferDef d)
        {
            List<string> lines = new List<string>();
            switch (d.Kind)
            {
                case StoreOfferKind.Pack:
                case StoreOfferKind.Starter:
                    for (int i = 0; i < d.Built.Grant.Items.Count; i++)
                    {
                        GrantItem it = d.Built.Grant.Items[i];
                        if (it.Kind != GrantKind.HeroChoice)
                        {
                            bool dust = it.Kind == GrantKind.Dust || it.Kind == GrantKind.DustSweeps;
                            lines.Add(PurchaseGrants.Describe(it, session) + (dust ? "" : " (Sealed)"));
                            continue;
                        }
                        lines.Add("One copy of a 5-star, your pick of " + d.HeroChoices.Count + " (Sealed):");
                        for (int h = 0; h < d.HeroChoices.Count; h++)
                        {
                            HeroDef hero = session.Content.GetHero(d.HeroChoices[h]);
                            if (hero != null) lines.Add("  " + hero.Name + ", " + hero.Role);
                        }
                    }
                    break;
                case StoreOfferKind.Ledger:
                    if (d.HoursPerDay > 0.0)
                        lines.Add(HoursText(d.HoursPerDay) + " of idle Gold every day you claim, for " + d.Days + " days = "
                                  + Big(GoldOfHours(session, d.HoursPerDay)) + " Gold a day (Sealed)");
                    if (d.IdleBonusPct > 0)
                        lines.Add("+" + d.IdleBonusPct + "% Gold on every idle claim while it runs (Sealed)");
                    lines.Add("A missed day is not made up.");
                    break;
                case StoreOfferKind.Growth:
                    for (int i = 0; i < d.Milestones.Count; i++) lines.Add(MilestoneLine(session, d.Milestones[i]));
                    break;
                case StoreOfferKind.Pass:
                    lines.AddRange(PassLines(session));
                    break;
                case StoreOfferKind.Contract:
                    lines.Add("One copy of a " + d.Stars + "-star you already own (Sealed)");
                    lines.Add("For the ascension of that hero only.");
                    break;
                case StoreOfferKind.RelicChoice:
                    List<RelicPick> relicPicks = RelicPacks.Picks(session, d.PickCapDust);
                    int fresh = 0, raise = 0, least, most;
                    for (int i = 0; i < relicPicks.Count; i++) { if (relicPicks[i].Upgrade) raise++; else fresh++; }
                    RelicPacks.WorthRange(relicPicks, out least, out most);
                    lines.Add("One relic of YOUR pick, named before you buy. Nothing is drawn.");
                    lines.Add("  A relic you do not own (" + fresh + " to pick from), or");
                    lines.Add("  one more tier on a relic you own (" + raise + " to pick from).");
                    if (most > 0)
                        lines.Add("At the Relic Shop that is " + Big(least) + " to " + Big(most) + " Relic Dust, by your pick.");
                    break;
                case StoreOfferKind.Skin:
                    SkinDef sold = session.Content.Skins.Skin(d.SkinId);
                    if (sold != null)
                    {
                        HeroDef skinHero = session.Content.GetHero(sold.HeroId);
                        lines.Add("The skin " + sold.Name + " for " + (skinHero != null ? skinHero.Name : sold.HeroId) + " only.");
                        lines.Add("A new look in battle and on the hero's screen, and " + sold.BonusLine() + " on that hero.");
                        lines.Add("One skin is worn per hero: it replaces the one worn, it never adds to it.");
                    }
                    break;
            }
            return lines;
        }

        private static List<string> PassLines(GameSession session)
        {
            List<string> lines = new List<string>();
            WayfarersRoadConfig road = session.Content.WayfarersRoad;
            double hours = 0.0;
            int cosmetics = 0, copies = 0;
            for (int i = 0; i < road.Paid.Count; i++)
            {
                PassRewardDef p = road.Paid[i];
                if (p.Kind == PassRewardKind.Hourglass) hours += p.Amount;
                else if (p.Kind == PassRewardKind.Cosmetic) cosmetics++;
                else if (p.Kind == PassRewardKind.Selector) copies++;
            }
            if (hours > 0.0) lines.Add(HoursText(hours) + " of idle Gold in all = " + Big(GoldOfHours(session, hours)) + " Gold (Sealed)");
            if (copies > 0) lines.Add(copies + " 5-star copy of your pick at tier 30 (Sealed)");
            if (cosmetics > 0) lines.Add(cosmetics + " cosmetics (placeholders)");
            lines.Add("For this season only. Every tier is listed on the Pass tab.");
            return lines;
        }

        private static string LimitLabel(StoreOfferDef d)
        {
            switch (d.Kind)
            {
                case StoreOfferKind.Ledger: return "One card at a time";
                case StoreOfferKind.Contract:
                    return d.AccountLimit > 0 ? d.AccountLimit + " a month (" + d.Limit + " per hero)" : d.Limit + " a month per hero";
                case StoreOfferKind.Skin: return "Once";
                case StoreOfferKind.Pass: return "One a season";
            }
            switch (d.Reset)
            {
                case StoreReset.Week: return d.Limit + " a week";
                case StoreReset.Month: return d.Limit + " a month";
                case StoreReset.Season: return d.Limit + " a season";
            }
            return d.Limit == 1 ? "Once" : d.Limit + " in all";
        }

        // ------------------------------------------------------------------ claims: the ledger

        /// <summary>Claims today's hours of a running card: once per reward day, never a missed day.</summary>
        public static StoreFulfilment ClaimLedger(GameSession session, long nowUnix)
        {
            StoreFulfilment r = new StoreFulfilment();
            if (!Active(session)) { r.Reason = StoreRefusal.Off; return r; }
            StoreOfferDef d = LedgerOffer(session);
            OfferRecord rec = LedgerRecord(session);
            if (d == null || rec == null || rec.StartDay <= 0) { r.Reason = StoreRefusal.NotBought; return r; }
            // A card that exists stamps the clock even when the day has nothing to claim, so a clock wound back
            // after the real one passed the card's end cannot revive it.
            long day = EffectiveDay(session, nowUnix);
            Stamp(session, day);
            if (!LedgerActive(session, nowUnix)) { r.Reason = StoreRefusal.Nothing; return r; }
            if (!LedgerCanClaim(session, nowUnix)) { r.Reason = StoreRefusal.Claimed; return r; }
            if (d.HoursPerDay <= 0.0) { r.Reason = StoreRefusal.Nothing; return r; }

            PurchaseGrant grant = new PurchaseGrant();
            grant.Add(new GrantItem(GrantKind.IdleGoldHours, d.HoursPerDay));
            GrantResult g = PurchaseGrants.Apply(session, grant, null, nowUnix);
            if (!g.Success) { r.Reason = StoreRefusal.Nothing; return r; }
            rec.LastDay = day;
            r.Grant = g;
            r.Success = true;
            return r;
        }

        /// <summary>The Gold a running card adds to an idle claim of this much Gold: the card's percent,
        /// rounded down, paid as Sealed Gold. Zero without a card.</summary>
        public static long IdleBonusFor(GameSession session, long claimedGold, long nowUnix)
        {
            if (!Active(session) || claimedGold <= 0 || !LedgerActive(session, nowUnix)) return 0L;
            return claimedGold * LedgerOffer(session).IdleBonusPct / 100L;
        }

        /// <summary>Called by GameSession.ClaimIdle after it pays the idle chest. Adds the card's share as
        /// Sealed Gold and returns it. Nothing without a card or with the switch off.</summary>
        public static long OnIdleClaim(GameSession session, IdleReport report, long nowUnix)
        {
            if (report == null || !Active(session)) return 0L;
            OfferRecord rec = LedgerRecord(session);
            if (rec == null || rec.StartDay <= 0) return 0L;
            Stamp(session, EffectiveDay(session, nowUnix));
            long bonus = IdleBonusFor(session, report.Gold, nowUnix);
            if (bonus > 0)
            {
                PurchaseGrant grant = new PurchaseGrant();
                grant.Add(new GrantItem(GrantKind.Gold, bonus));
                PurchaseGrants.Apply(session, grant, null, nowUnix);
            }
            return bonus;
        }

        // ------------------------------------------------------------------ claims: the growth fund

        private static StoreOfferDef GrowthOffer(GameSession session)
        {
            List<StoreOfferDef> all = session.Content.Store.Offers;
            for (int i = 0; i < all.Count; i++) if (all[i].Kind == StoreOfferKind.Growth) return all[i];
            return null;
        }

        private static bool GrowthOwned(GameSession session, StoreOfferDef d)
        {
            return CountOf(session, d.Id, 0L) > 0;
        }

        private static bool GrowthClaimed(GameSession session, StoreOfferDef d, int index)
        {
            OfferRecord r = session.Profile.Store.Find(d.Id);
            return r != null && index >= 0 && index < 30 && (r.Mask & (1 << index)) != 0;
        }

        /// <summary>Claims one milestone of a bought Growth Fund once its stage is cleared.</summary>
        public static StoreFulfilment ClaimGrowth(GameSession session, int index, long nowUnix)
        {
            StoreFulfilment r = new StoreFulfilment();
            if (!Active(session)) { r.Reason = StoreRefusal.Off; return r; }
            StoreOfferDef d = GrowthOffer(session);
            if (d == null || index < 0 || index >= d.Milestones.Count) { r.Reason = StoreRefusal.Unknown; return r; }
            if (!GrowthOwned(session, d)) { r.Reason = StoreRefusal.NotBought; return r; }
            if (GrowthClaimed(session, d, index)) { r.Reason = StoreRefusal.Claimed; return r; }
            StoreMilestone m = d.Milestones[index];
            if (!session.Profile.HasClearedStage(m.StageId)) { r.Reason = StoreRefusal.NotReached; return r; }

            PurchaseGrant grant = new PurchaseGrant();
            // FIXED rewards: priced at the milestone's own stage, then granted as plain Gold and Dust amounts.
            long gold = MilestoneGold(session, m), dust = MilestoneDust(session, m);
            if (gold > 0) grant.Add(new GrantItem(GrantKind.Gold, gold));
            if (dust > 0) grant.Add(new GrantItem(GrantKind.Dust, dust));
            GrantResult g = PurchaseGrants.Apply(session, grant, null, nowUnix);
            if (!g.Success) { r.Reason = StoreRefusal.Nothing; return r; }
            Stamp(session, EffectiveDay(session, nowUnix));
            session.Profile.Store.Record(d.Id).Mask |= 1 << index;
            r.Grant = g;
            r.Success = true;
            return r;
        }

        // ------------------------------------------------------------------ the paid track

        /// <summary>True when the paid track of the season the clock is in has been bought (the premium grant
        /// sets WayfarersRoadState.PaidSeason).</summary>
        public static bool PassOwned(GameSession session, long nowUnix)
        {
            if (!Active(session) || !WayfarersRoad.Active(session)) return false;
            long season = WayfarersRoad.SeasonOf(session, EffectiveDay(session, nowUnix));
            return season > 0L && session.Profile.WayfarersRoad.PaidSeason == season;
        }

        // ------------------------------------------------------------------ the view

        public static StoreView View(GameSession session, long nowUnix)
        {
            StoreView v = new StoreView();
            if (!Active(session)) return v;
            v.Active = true;
            long day = EffectiveDay(session, nowUnix);
            List<StoreOfferDef> all = session.Content.Store.Offers;

            for (int i = 0; i < all.Count; i++)
            {
                StoreOfferDef d = all[i];
                if (!Listed(d, session.Content.Skins)) continue;
                if (d.Kind == StoreOfferKind.Event) continue;   // sold from the Events screen, with the event's own words
                StoreOfferView o = new StoreOfferView();
                o.Id = d.Id;
                o.Name = d.Name;
                o.Blurb = d.Blurb;
                o.Kind = d.Kind;
                o.ProductId = d.ProductId;
                o.Tier = d.Tier;
                o.PriceEur = d.PriceEur;
                o.PriceLabel = StoreConfig.Eur(d.PriceEur);
                o.LimitLabel = LimitLabel(d);
                o.Lines.AddRange(ContentLines(session, d));
                FillChoices(session, d, o);
                o.NeedsChoice = d.Kind == StoreOfferKind.Starter || d.Kind == StoreOfferKind.Contract || d.Kind == StoreOfferKind.RelicChoice;
                o.Reason = CheckAny(session, d.Id, nowUnix);
                o.Available = o.Reason == StoreRefusal.None;
                if (o.Reason == StoreRefusal.Locked) o.LockLine = LockLineOf(session, d);
                if (d.Kind == StoreOfferKind.Skin)
                {
                    SkinDef sk = session.Content.Skins.Skin(d.SkinId);
                    o.SkinId = d.SkinId;
                    o.SkinHeroId = sk != null ? sk.HeroId : "";
                    HeroDef skHero = sk != null ? session.Content.GetHero(sk.HeroId) : null;
                    o.SkinHeroName = skHero != null ? skHero.Name : "";
                    o.Owned = session.Profile.OwnsSkin(d.SkinId);
                }

                if ((o.Reason == StoreRefusal.SoldOut || o.Reason == StoreRefusal.AccountCap) && d.Kind != StoreOfferKind.Ledger)
                {
                    long end = PeriodEnd(session, d, day);
                    o.ResetInSeconds = end > 0 ? Math.Max(0L, end - nowUnix) : 0L;
                }
                if (d.Kind == StoreOfferKind.Ledger)
                {
                    o.Owned = LedgerActive(session, nowUnix);
                    o.DaysLeft = LedgerDaysLeft(session, nowUnix);
                    o.CanClaim = LedgerCanClaim(session, nowUnix);
                    if (o.CanClaim) v.AnythingToClaim = true;
                }
                else if (d.Kind == StoreOfferKind.Growth)
                {
                    o.Owned = GrowthOwned(session, d);
                    for (int m = 0; m < d.Milestones.Count; m++)
                    {
                        StoreMilestoneView mv = new StoreMilestoneView();
                        mv.Index = m;
                        mv.StageId = d.Milestones[m].StageId;
                        StageDef stage = session.Content.GetStage(mv.StageId);
                        mv.StageName = stage != null ? StageLabel(stage) : mv.StageId;
                        mv.Line = MilestoneLine(session, d.Milestones[m]);
                        mv.Reached = session.Profile.HasClearedStage(mv.StageId);
                        mv.Claimed = GrowthClaimed(session, d, m);
                        mv.CanClaim = o.Owned && mv.Reached && !mv.Claimed;
                        if (mv.CanClaim) v.AnythingToClaim = true;
                        o.Milestones.Add(mv);
                    }
                }
                else if (d.Kind == StoreOfferKind.Pass)
                {
                    o.Owned = PassOwned(session, nowUnix);
                }
                v.Offers.Add(o);
            }

            FillPass(session, v, nowUnix);
            return v;
        }

        private static void FillChoices(GameSession session, StoreOfferDef d, StoreOfferView o)
        {
            List<string> picks = PicksOf(session, d);
            for (int i = 0; i < picks.Count; i++)
            {
                if (d.Kind == StoreOfferKind.RelicChoice)
                {
                    RelicPick rp = RelicPacks.Find(session, picks[i], d.PickCapDust);
                    if (rp == null) continue;
                    StoreChoice rc = new StoreChoice();
                    rc.HeroId = rp.RelicId;
                    rc.Name = rp.Name;
                    rc.Role = rp.Upgrade ? "Tier " + rp.FromTier + " to " + rp.ToTier + ", worth " + Big(rp.DustWorth) + " Relic Dust"
                                         : "New " + rp.Grade + ", worth " + Big(rp.DustWorth) + " Relic Dust";
                    o.Choices.Add(rc);
                    continue;
                }
                HeroDef hero = session.Content.GetHero(picks[i]);
                if (hero == null) continue;
                StoreChoice c = new StoreChoice();
                c.HeroId = hero.Id;
                c.Name = hero.Name;
                c.Role = hero.Role.ToString();
                o.Choices.Add(c);
            }
        }

        private static void FillPass(GameSession session, StoreView v, long nowUnix)
        {
            PassView road = WayfarersRoad.Peek(session, nowUnix);   // read only: drawing the Hub must never write the save
            v.PassOpen = road.Active;
            v.PassNeedsLoop = !DailyLoop.Active(session);
            if (!road.Active) return;
            v.PassSeason = road.Season;
            v.PassDaysLeft = road.DaysLeft;
            v.PassReached = road.Reached;
            v.PassOwned = PassOwned(session, nowUnix);
            WayfarersRoadState st = session.Profile.WayfarersRoad;
            WayfarersRoadConfig cfg = session.Content.WayfarersRoad;
            for (int i = 0; i < cfg.Paid.Count; i++)
            {
                PassRewardDef p = cfg.Paid[i];
                StorePaidTierView t = new StorePaidTierView();
                t.Tier = p.Tier;
                t.Kind = p.Kind;
                t.Stars = p.Stars;
                t.Line = PaidLine(session, p);
                t.Short = PaidShort(p);
                t.Reached = p.Tier <= road.Reached;
                t.Claimed = v.PassOwned && st.HasClaimedPaid(p.Tier);
                t.CanClaim = v.PassOwned && t.Reached && !t.Claimed;
                if (t.CanClaim) v.AnythingToClaim = true;
                v.Paid.Add(t);
            }
        }

        private static string PaidShort(PassRewardDef p)
        {
            switch (p.Kind)
            {
                case PassRewardKind.Hourglass: return Num(p.Amount) + " h idle Gold";
                case PassRewardKind.Slag: return Big((long)p.Amount) + " Slag";
                case PassRewardKind.Dust: return Num(p.Amount) + " sweeps Dust";
                case PassRewardKind.Selector: return "A " + p.Stars + "-star of your pick";
            }
            return p.Label.Length > 0 ? p.Label : "Cosmetic";
        }

        private static string PaidLine(GameSession session, PassRewardDef p)
        {
            switch (p.Kind)
            {
                case PassRewardKind.Hourglass: return PurchaseGrants.Describe(new GrantItem(GrantKind.IdleGoldHours, p.Amount), session) + " (Sealed)";
                case PassRewardKind.Slag: return Big((long)p.Amount) + " Slag (Sealed)";
                case PassRewardKind.Dust: return PurchaseGrants.Describe(new GrantItem(GrantKind.DustSweeps, p.Amount), session);
                case PassRewardKind.Selector: return "A " + p.Stars + "-star of your choice (Sealed)";
            }
            return (p.Label.Length > 0 ? p.Label : "Cosmetic") + " (placeholder)";
        }

        // ------------------------------------------------------------------ value, in days of income

        /// <summary>Hours of idle that make "a day of frontier income": the idle chest holds idleCapHours and
        /// is collected twice a day (IDEAS-ECONOMY-PACKS section 1).</summary>
        public static double IdleDayHours(GameSession session)
        {
            return 2.0 * Math.Max(1.0, session.Balance.IdleCapHours);
        }

        /// <summary>Days of frontier Shards income one pull at the base 5-star rate costs, for this player: the
        /// expected wait for a random 5-star. A named 5-star is worth the roster's width more than this.</summary>
        public static double DaysPerFiveStarPull(GameSession session)
        {
            BalanceConfig b = session.Balance;
            double total = b.SummonWeight1 + b.SummonWeight2 + b.SummonWeight3 + b.SummonWeight4 + b.SummonWeight5;
            if (total <= 0.0 || b.SummonWeight5 <= 0.0) return 0.0;
            double shardsPerDay = session.IdleShardsPerHour * IdleDayHours(session);
            double summonsPerDay = shardsPerDay / Math.Max(1, b.SummonCost);
            double rate = b.SummonWeight5 / total;
            return summonsPerDay * rate <= 0.0 ? 0.0 : 1.0 / (summonsPerDay * rate);
        }

        /// <summary>One day of Relic Dust for a player who spends their repeat clears: the capped repeat clears of
        /// every stage they have cleared, each at its own payout.</summary>
        public static double DustDay(GameSession session)
        {
            double total = 0.0;
            List<StageDef> stages = session.Content.AllStages;
            for (int i = 0; i < stages.Count; i++)
                if (session.Profile.HasClearedStage(stages[i].Id)) total += session.RelicDustForRepeatClear(stages[i]);
            return Math.Max(1.0, total * Math.Max(1, session.Balance.RepeatFullClearsPerDay));
        }

        /// <summary>What a purchase of the offer is worth at this player's frontier, in days of income.
        /// Gold is hours over the idle day. Dust is Dust over the day's capped repeat clears. A hero copy is the
        /// days of Shards one pull at the base 5-star rate costs. Slag is counted in units, not days, because
        /// Sealed Slag has no sink. A card counts its whole run. A growth fund counts every milestone.</summary>
        public static StoreValue ValueOf(GameSession session, StoreOfferDef d)
        {
            StoreValue v = new StoreValue();
            double dayHours = IdleDayHours(session);
            double hours = d.Hours, sweeps = d.DustSweeps;
            int copies = (d.Kind == StoreOfferKind.Starter || d.Kind == StoreOfferKind.Contract) ? d.HeroCopies : 0;
            v.Slag = d.Slag;
            if (d.Kind == StoreOfferKind.Ledger)
            {
                hours = d.HoursPerDay * d.Days;
                hours += d.IdleBonusPct / 100.0 * dayHours * d.Days;   // the idle share: percent of the Gold of two claims a day
            }
            else if (d.Kind == StoreOfferKind.Growth)
            {
                // Fixed at each milestone's own stage, so what it is worth to THIS buyer is those amounts over THEIR
                // frontier's income: most to an early buyer, who is paid chapter 2 to 6 rates against chapter 1 income.
                long gold = 0, dust = 0;
                for (int i = 0; i < d.Milestones.Count; i++) { gold += MilestoneGold(session, d.Milestones[i]); dust += MilestoneDust(session, d.Milestones[i]); }
                double perHour = Math.Max(1.0, session.IdleGoldPerHour);
                v.GoldHours = gold / perHour;
                v.GoldDays = gold / (perHour * dayHours);
                v.DustSweeps = 0.0;
                v.DustDays = dust / DustDay(session);
                v.HeroCopies = 0;
                v.HeroDays = 0.0;
                v.TotalDays = v.GoldDays + v.DustDays;
                return v;
            }
            else if (d.Kind == StoreOfferKind.Event)
            {
                // The first event of the slot stands for the slot: a pack's fixed bundle, or the sum of a track's paid tiers.
                LiveEventDef ev = session.Content.LiveEvents.EventFor(d.EventSlot, 1);
                if (ev != null && d.EventPart == LiveEventPart.Pack && ev.Pack != null) { hours = ev.Pack.Hours; sweeps = ev.Pack.DustSweeps; }
                else if (ev != null && d.EventPart == LiveEventPart.Track)
                {
                    for (int i = 0; i < ev.Tiers.Count; i++)
                    {
                        EventRewardDef p = ev.Tiers[i].Paid;
                        if (p == null) continue;
                        if (p.Kind == EventRewardKind.Hourglass) hours += p.Amount;
                        else if (p.Kind == EventRewardKind.Dust) sweeps += p.Amount;
                        else v.Slag += (int)p.Amount;
                    }
                }
            }
            else if (d.Kind == StoreOfferKind.Pass)
            {
                WayfarersRoadConfig road = session.Content.WayfarersRoad;
                for (int i = 0; i < road.Paid.Count; i++)
                {
                    PassRewardDef p = road.Paid[i];
                    if (p.Kind == PassRewardKind.Hourglass) hours += p.Amount;
                    else if (p.Kind == PassRewardKind.Dust) sweeps += p.Amount;
                    else if (p.Kind == PassRewardKind.Selector) copies++;
                }
            }
            v.GoldHours = hours;
            v.GoldDays = hours / dayHours;
            v.DustSweeps = sweeps;
            v.FixedDust = d.Dust;
            v.DustDays = (sweeps * WayfarersRoad.DustPerSweep(session) + d.Dust) / DustDay(session);
            v.HeroCopies = copies;
            v.HeroDays = copies * DaysPerFiveStarPull(session);
            v.TotalDays = v.GoldDays + v.DustDays + v.HeroDays;
            return v;
        }
    }
}
