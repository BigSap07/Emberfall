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
        Nothing
    }

    /// <summary>What a grant is. A closed set, and none of them is a roll.</summary>
    public enum StoreGrantKind
    {
        /// <summary>Sealed Gold.</summary>
        Gold,
        /// <summary>Sealed Slag.</summary>
        Slag,
        /// <summary>Relic Dust, which is never marked (its only sink sells fixed relics).</summary>
        Dust,
        /// <summary>A Sealed hero copy.</summary>
        Hero,
        /// <summary>Nothing but a mark in the save (a cosmetic placeholder).</summary>
        Cosmetic
    }

    /// <summary>One resolved line of what a purchase or a claim gives.</summary>
    public sealed class StoreGrant
    {
        public StoreGrantKind Kind;
        public long Amount;
        public string HeroId = "";
        /// <summary>In words, with the numbers resolved for this player: "6 HOURS OF IDLE GOLD = 12,000 Gold (SEALED)".</summary>
        public string Line = "";
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
        /// <summary>"ONCE", "1 A WEEK", "1 A MONTH", "1 A SEASON", "2 A MONTH PER HERO", "ONE AT A TIME".</summary>
        public string LimitLabel = "";
        public bool Available;
        public StoreRefusal Reason = StoreRefusal.None;
        /// <summary>Seconds until the limit comes back, when it is used up and resets by the calendar. Else 0.</summary>
        public long ResetInSeconds;
        /// <summary>Ledger only: reward days left on a running card (today included), else 0.</summary>
        public int DaysLeft;
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

    /// <summary>The outcome of a fulfilment or a claim.</summary>
    public sealed class StoreFulfilment
    {
        public bool Success;
        public StoreRefusal Reason = StoreRefusal.None;
        public readonly List<StoreGrant> Grants = new List<StoreGrant>();
        public HeroInstance Hero;
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
    }

    /// <summary>
    /// THE STORE'S RULES (Docs/DESIGN-STORE.md): the offers, their fixed contents, their limits and resets,
    /// and the one place a purchase becomes goods. Pure rules over a GameSession's profile and content; the
    /// screen only calls View, Check, the claims and (through the purchase path) Fulfil.
    ///
    /// THE RULE. Every grant is made through SealedGrants, so it is marked Sealed and never enters a summon,
    /// a Conversion or a forge gamble. Dust is the one good not marked, because its only sink sells fixed
    /// relics. There is no random call in this file or in the purchase path that reaches it
    /// (CheckStoreNothingRandom reads the source). An offer's contents are fixed numbers and named heroes in
    /// store.json: Check and View take no random input and the same state gives the same answer.
    ///
    /// ONE CLOCK, FORWARD ONLY. The day is the reward day (GameSession.DayStartUnix). Every read and write
    /// uses the LATER of that and StoreState.ClockUnix, so a clock wound back reopens no limit, revives no
    /// expired card and pays no day twice. Periods: a week is Monday to Sunday by reward day
    /// (DailyLoop.WeekOf), a month is the calendar month of the reward day (SinkShop.MonthKeyOf), a season is
    /// Wayfarer's Road's.
    ///
    /// FULFIL IS ATOMIC. It checks, resolves every amount, and only then writes. A refusal writes nothing.
    /// The purchase path calls it only after a completed purchase, so a pending purchase grants nothing.
    ///
    /// OFF MEANS INERT. With StoreSwitch off, or without a usable store.json, every call returns at once and
    /// writes nothing, and OnIdleClaim adds nothing.
    /// </summary>
    public static class StoreOffers
    {
        public const string PassOfferId = "pass_premium";
        public const string ContractPrefix = "hero_contract|";

        public static bool Active(GameSession session)
        {
            return StoreSwitch.Enabled && session != null && session.Content.Store.IsUsable;
        }

        /// <summary>Only the growth fund is a one-time unlock a new profile may be given back. A pack, a card, a
        /// season's pass and a contract are used up.</summary>
        public static bool IsRestorable(string offerId)
        {
            return offerId == "growth_fund";
        }

        private static bool Listed(StoreOfferDef d)
        {
            return d.Kind != StoreOfferKind.Contract || HeroContractSwitch.Enabled;
        }

        private static StoreOfferDef Find(GameSession session, string offerId)
        {
            StoreOfferDef d = session.Content.Store.Offer(offerId);
            return d != null && Listed(d) ? d : null;
        }

        // ------------------------------------------------------------------ the clock

        /// <summary>The reward day to use: the real one, or the latest the store ever saw if that is later.</summary>
        public static long EffectiveDay(GameSession session, long nowUnix)
        {
            return Math.Max(session.DayStartUnix(nowUnix), session.Profile.Store.ClockUnix);
        }

        private static void Stamp(GameSession session, long effectiveDay)
        {
            StoreState st = session.Profile.Store;
            if (effectiveDay > st.ClockUnix) st.ClockUnix = effectiveDay;
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

        private static int BoughtIn(GameSession session, StoreOfferDef d, string choice, long effectiveDay)
        {
            return session.Profile.Store.CountOf(CountKey(d, choice), PeriodOf(session, d, effectiveDay));
        }

        // ------------------------------------------------------------------ the ledger card

        /// <summary>True when a card is running on this reward day.</summary>
        public static bool LedgerActive(GameSession session, long nowUnix)
        {
            StoreState st = session.Profile.Store;
            if (st.LedgerStartUnix <= 0) return false;
            long day = EffectiveDay(session, nowUnix);
            return day >= st.LedgerStartUnix && day < st.LedgerStartUnix + st.LedgerDays * 86400L;
        }

        /// <summary>Reward days left on a running card, today included. Zero when none runs.</summary>
        public static int LedgerDaysLeft(GameSession session, long nowUnix)
        {
            if (!LedgerActive(session, nowUnix)) return 0;
            StoreState st = session.Profile.Store;
            return (int)((st.LedgerStartUnix + st.LedgerDays * 86400L - EffectiveDay(session, nowUnix)) / 86400L);
        }

        private static StoreOfferDef LedgerOffer(GameSession session)
        {
            List<StoreOfferDef> all = session.Content.Store.Offers;
            for (int i = 0; i < all.Count; i++) if (all[i].Kind == StoreOfferKind.Ledger) return all[i];
            return null;
        }

        private static bool LedgerCanClaim(GameSession session, long nowUnix)
        {
            return LedgerActive(session, nowUnix) && EffectiveDay(session, nowUnix) > session.Profile.Store.LedgerClaimedUnix;
        }

        // ------------------------------------------------------------------ checks

        /// <summary>None when the offer may be bought now. Reads, writes nothing, takes no random input.</summary>
        public static StoreRefusal Check(GameSession session, string offerId, string choice, long nowUnix)
        {
            if (!Active(session)) return StoreRefusal.Off;
            StoreOfferDef d = Find(session, offerId);
            if (d == null) return StoreRefusal.Unknown;
            long day = EffectiveDay(session, nowUnix);

            switch (d.Kind)
            {
                case StoreOfferKind.Ledger:
                    if (LedgerActive(session, nowUnix)) return StoreRefusal.SoldOut;
                    return StoreRefusal.None;
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
            }
            if (BoughtIn(session, d, choice, day) >= d.Limit) return StoreRefusal.SoldOut;
            return StoreRefusal.None;
        }

        /// <summary>A sentence for a refusal, for the screen to print. The screen decides nothing.</summary>
        public static string ReasonText(StoreRefusal r)
        {
            switch (r)
            {
                case StoreRefusal.Off: return "THE STORE IS CLOSED";
                case StoreRefusal.Unknown: return "NOT FOR SALE";
                case StoreRefusal.SoldOut: return "SOLD OUT";
                case StoreRefusal.NeedsChoice: return "PICK A HERO";
                case StoreRefusal.BadChoice: return "THAT HERO IS NOT ON THE LIST";
                case StoreRefusal.NotOwned: return "YOU DO NOT OWN THAT HERO";
                case StoreRefusal.PassClosed: return "NO SEASON IS RUNNING";
                case StoreRefusal.PassInert: return "THE PASS NEEDS THE DAILY ORDERS";
                case StoreRefusal.NotBought: return "NOT BOUGHT";
                case StoreRefusal.NotReached: return "NOT REACHED YET";
                case StoreRefusal.Claimed: return "CLAIMED";
                case StoreRefusal.Nothing: return "NOTHING TO CLAIM";
            }
            return "";
        }

        // ------------------------------------------------------------------ what an offer gives

        private static string Num(double v)
        {
            return v.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private static string Big(long v)
        {
            return v.ToString("N0", CultureInfo.InvariantCulture);
        }

        private static long GoldOfHours(GameSession session, double hours)
        {
            if (hours <= 0.0) return 0L;
            return session.IdleYield((long)Math.Round(hours * 3600.0)).Gold;
        }

        private static string HoursText(double hours)
        {
            return Num(hours) + (hours == 1.0 ? " HOUR" : " HOURS");
        }

        private static StoreGrant GoldGrant(GameSession session, double hours, string prefix)
        {
            StoreGrant g = new StoreGrant();
            g.Kind = StoreGrantKind.Gold;
            g.Amount = GoldOfHours(session, hours);
            g.Line = prefix + HoursText(hours) + " OF IDLE GOLD = " + Big(g.Amount) + " Gold (SEALED)";
            return g;
        }

        private static StoreGrant DustGrant(GameSession session, double sweeps)
        {
            StoreGrant g = new StoreGrant();
            g.Kind = StoreGrantKind.Dust;
            g.Amount = (long)Math.Round(sweeps * WayfarersRoad.DustPerSweep(session));
            g.Line = Num(sweeps) + " SWEEPS OF DUST = " + Big(g.Amount) + " Dust";
            return g;
        }

        private static StoreGrant SlagGrant(int slag)
        {
            StoreGrant g = new StoreGrant();
            g.Kind = StoreGrantKind.Slag;
            g.Amount = slag;
            g.Line = Big(slag) + " SLAG (SEALED)";
            return g;
        }

        private static StoreGrant HeroGrant(GameSession session, string heroId, int copies)
        {
            StoreGrant g = new StoreGrant();
            g.Kind = StoreGrantKind.Hero;
            g.Amount = copies;
            g.HeroId = heroId ?? "";
            HeroDef hero = string.IsNullOrEmpty(heroId) ? null : session.Content.GetHero(heroId);
            g.Line = (copies == 1 ? "ONE COPY OF " : copies + " COPIES OF ") + (hero != null ? hero.Name.ToUpperInvariant() : "THE HERO YOU PICK") + " (SEALED)";
            return g;
        }

        /// <summary>The goods a purchase of this offer gives, resolved for this player. A card, a fund and a
        /// pass give nothing at the moment of purchase: what they give is claimed later.</summary>
        public static List<StoreGrant> GrantsOf(GameSession session, StoreOfferDef d, string choice)
        {
            List<StoreGrant> list = new List<StoreGrant>();
            if (d.Kind == StoreOfferKind.Pack || d.Kind == StoreOfferKind.Starter)
            {
                if (d.Hours > 0.0) list.Add(GoldGrant(session, d.Hours, ""));
                if (d.Slag > 0) list.Add(SlagGrant(d.Slag));
                if (d.DustSweeps > 0.0) list.Add(DustGrant(session, d.DustSweeps));
            }
            if (d.Kind == StoreOfferKind.Starter || d.Kind == StoreOfferKind.Contract)
                list.Add(HeroGrant(session, choice, d.HeroCopies));
            return list;
        }

        /// <summary>The fixed contents of an offer in words, for the card, before a purchase. Includes what a
        /// card, a fund or a pass will give later.</summary>
        public static List<string> ContentLines(GameSession session, StoreOfferDef d)
        {
            List<string> lines = new List<string>();
            switch (d.Kind)
            {
                case StoreOfferKind.Pack:
                    List<StoreGrant> g = GrantsOf(session, d, null);
                    for (int i = 0; i < g.Count; i++) lines.Add(g[i].Line);
                    break;
                case StoreOfferKind.Starter:
                    List<StoreGrant> s = GrantsOf(session, d, null);
                    for (int i = 0; i < s.Count; i++) if (s[i].Kind != StoreGrantKind.Hero) lines.Add(s[i].Line);
                    lines.Add("ONE 5-STAR COPY (SEALED), YOUR PICK OF " + d.HeroChoices.Count + ":");
                    for (int i = 0; i < d.HeroChoices.Count; i++)
                    {
                        HeroDef hero = session.Content.GetHero(d.HeroChoices[i]);
                        if (hero != null) lines.Add("  " + hero.Name.ToUpperInvariant() + ", " + hero.Role.ToString().ToUpperInvariant());
                    }
                    break;
                case StoreOfferKind.Ledger:
                    if (d.HoursPerDay > 0.0)
                        lines.Add(HoursText(d.HoursPerDay) + " OF IDLE GOLD EVERY DAY YOU CLAIM, FOR " + d.Days + " DAYS = " + Big(GoldOfHours(session, d.HoursPerDay)) + " Gold A DAY (SEALED)");
                    if (d.IdleBonusPct > 0)
                        lines.Add("+" + d.IdleBonusPct + "% GOLD ON EVERY IDLE CLAIM WHILE IT RUNS (SEALED)");
                    lines.Add("A MISSED DAY IS NOT MADE UP");
                    break;
                case StoreOfferKind.Growth:
                    for (int i = 0; i < d.Milestones.Count; i++) lines.Add(MilestoneLine(session, d.Milestones[i]));
                    break;
                case StoreOfferKind.Pass:
                    lines.AddRange(PassLines(session));
                    break;
                case StoreOfferKind.Contract:
                    lines.Add("ONE COPY OF A " + d.Stars + "-STAR YOU ALREADY OWN (SEALED)");
                    lines.Add("FOR ASCENSION OF THAT HERO ONLY");
                    break;
            }
            return lines;
        }

        private static string MilestoneLine(GameSession session, StoreMilestone m)
        {
            StageDef stage = session.Content.GetStage(m.StageId);
            string name = stage != null ? StageLabel(stage) : m.StageId;
            string pays = "";
            if (m.Hours > 0.0) pays = HoursText(m.Hours) + " OF IDLE GOLD = " + Big(GoldOfHours(session, m.Hours)) + " Gold (SEALED)";
            if (m.DustSweeps > 0.0)
                pays += (pays.Length > 0 ? ", " : "") + Num(m.DustSweeps) + " SWEEPS OF DUST = " + Big((long)Math.Round(m.DustSweeps * WayfarersRoad.DustPerSweep(session))) + " Dust";
            return "CLEAR " + name + ": " + pays;
        }

        /// <summary>"2-12": the chapter number and the stage's place in it, the way the campaign map prints it.</summary>
        public static string StageLabel(StageDef stage)
        {
            int chapter = stage.ChapterId.StartsWith("ch") ? ParseInt(stage.ChapterId.Substring(2), 1) : 1;
            return chapter + "-" + stage.Index;
        }

        private static int ParseInt(string s, int fallback)
        {
            int v;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : fallback;
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
            if (hours > 0.0) lines.Add(HoursText(hours) + " OF IDLE GOLD IN ALL, = " + Big(GoldOfHours(session, hours)) + " Gold (SEALED)");
            if (copies > 0) lines.Add(copies + " 5-STAR COPY OF YOUR PICK AT TIER 30 (SEALED)");
            if (cosmetics > 0) lines.Add(cosmetics + " COSMETICS (PLACEHOLDERS)");
            lines.Add("FOR THIS SEASON ONLY. EVERY TIER IS LISTED ON THE PASS TAB");
            return lines;
        }

        private static string LimitLabel(StoreOfferDef d)
        {
            switch (d.Kind)
            {
                case StoreOfferKind.Ledger: return "ONE CARD AT A TIME";
                case StoreOfferKind.Contract: return d.Limit + " A MONTH PER HERO";
                case StoreOfferKind.Pass: return "ONE A SEASON";
            }
            switch (d.Reset)
            {
                case StoreReset.Week: return d.Limit + " A WEEK";
                case StoreReset.Month: return d.Limit + " A MONTH";
                case StoreReset.Season: return d.Limit + " A SEASON";
            }
            return d.Limit == 1 ? "ONCE" : d.Limit + " IN ALL";
        }

        // ------------------------------------------------------------------ fulfil

        /// <summary>
        /// The one place a purchase becomes goods. The purchase path calls it after a completed, confirmed
        /// purchase and never before. Checks first, resolves every amount, then writes; a refusal writes
        /// nothing. Every grant is made through SealedGrants.
        /// </summary>
        public static StoreFulfilment Fulfil(GameSession session, string offerId, string choice, long nowUnix)
        {
            StoreFulfilment r = new StoreFulfilment();
            r.Reason = Check(session, offerId, choice, nowUnix);
            if (r.Reason != StoreRefusal.None) return r;
            StoreOfferDef d = Find(session, offerId);
            long day = EffectiveDay(session, nowUnix);
            List<StoreGrant> grants = GrantsOf(session, d, choice);

            // ---- the one write block
            StoreState st = session.Profile.Store;
            Stamp(session, day);
            if (d.Kind == StoreOfferKind.Ledger)
            {
                st.LedgerStartUnix = day;
                st.LedgerClaimedUnix = 0;
                st.LedgerDays = d.Days;
                st.LedgerBonusPct = d.IdleBonusPct;
            }
            else if (d.Kind == StoreOfferKind.Pass)
            {
                long season = WayfarersRoad.SeasonOf(session, day);
                st.Bump(CountKey(d, choice), season);
                if (st.PaidSeason != season) { st.PaidSeason = season; st.PaidMask = 0; }
            }
            else
            {
                st.Bump(CountKey(d, choice), PeriodOf(session, d, day));
            }
            r.Hero = Apply(session, grants, nowUnix);
            r.Grants.AddRange(grants);
            r.Success = true;
            return r;
        }

        /// <summary>Writes grants into the profile through SealedGrants. The only writer of a store grant.</summary>
        private static HeroInstance Apply(GameSession session, List<StoreGrant> grants, long nowUnix)
        {
            HeroInstance hero = null;
            for (int i = 0; i < grants.Count; i++)
            {
                StoreGrant g = grants[i];
                switch (g.Kind)
                {
                    case StoreGrantKind.Gold: SealedGrants.GrantGold(session, g.Amount); break;
                    case StoreGrantKind.Slag: SealedGrants.GrantSlag(session, g.Amount); break;
                    case StoreGrantKind.Dust: session.Profile.RelicDust += g.Amount; break;
                    case StoreGrantKind.Hero:
                    {
                        HeroDef def = session.Content.GetHero(g.HeroId);
                        for (int c = 0; c < g.Amount; c++) hero = SealedGrants.GrantHeroCopy(session, def, nowUnix);
                        break;
                    }
                }
            }
            return hero;
        }

        // ------------------------------------------------------------------ claims: the ledger

        /// <summary>Claims today's hours of a running card: once per reward day, never a missed day.</summary>
        public static StoreFulfilment ClaimLedger(GameSession session, long nowUnix)
        {
            StoreFulfilment r = new StoreFulfilment();
            if (!Active(session)) { r.Reason = StoreRefusal.Off; return r; }
            StoreState st = session.Profile.Store;
            if (st.LedgerStartUnix <= 0) { r.Reason = StoreRefusal.NotBought; return r; }
            // A card that exists stamps the clock even when the day has nothing to claim, so a clock wound back
            // after the real one passed the card's end cannot revive it.
            Stamp(session, EffectiveDay(session, nowUnix));
            if (!LedgerActive(session, nowUnix)) { r.Reason = StoreRefusal.Nothing; return r; }
            if (!LedgerCanClaim(session, nowUnix)) { r.Reason = StoreRefusal.Claimed; return r; }
            StoreOfferDef d = LedgerOffer(session);
            if (d == null || d.HoursPerDay <= 0.0) { r.Reason = StoreRefusal.Nothing; return r; }

            long day = EffectiveDay(session, nowUnix);
            StoreGrant gold = GoldGrant(session, d.HoursPerDay, "");
            Stamp(session, day);
            st.LedgerClaimedUnix = day;
            SealedGrants.GrantGold(session, gold.Amount);
            r.Grants.Add(gold);
            r.Success = true;
            return r;
        }

        /// <summary>The Gold a running card adds to an idle claim of this much Gold: the card's percent,
        /// rounded down, paid as Sealed Gold. Zero without a card.</summary>
        public static long IdleBonusFor(GameSession session, long claimedGold, long nowUnix)
        {
            if (!Active(session) || claimedGold <= 0 || !LedgerActive(session, nowUnix)) return 0L;
            return claimedGold * session.Profile.Store.LedgerBonusPct / 100L;
        }

        /// <summary>Called by GameSession.ClaimIdle after it pays the idle chest. Adds the card's share as
        /// Sealed Gold and returns it. Nothing without a card or with the switch off.</summary>
        public static long OnIdleClaim(GameSession session, IdleReport report, long nowUnix)
        {
            if (report == null) return 0L;
            if (Active(session) && session.Profile.Store.LedgerStartUnix > 0) Stamp(session, EffectiveDay(session, nowUnix));
            long bonus = IdleBonusFor(session, report.Gold, nowUnix);
            if (bonus > 0) SealedGrants.GrantGold(session, bonus);
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
            return session.Profile.Store.CountOf(d.Id, 0L) > 0;
        }

        /// <summary>Claims one milestone of a bought Growth Fund once its stage is cleared.</summary>
        public static StoreFulfilment ClaimGrowth(GameSession session, int index, long nowUnix)
        {
            StoreFulfilment r = new StoreFulfilment();
            if (!Active(session)) { r.Reason = StoreRefusal.Off; return r; }
            StoreOfferDef d = GrowthOffer(session);
            if (d == null || index < 0 || index >= d.Milestones.Count) { r.Reason = StoreRefusal.Unknown; return r; }
            StoreState st = session.Profile.Store;
            if (!GrowthOwned(session, d)) { r.Reason = StoreRefusal.NotBought; return r; }
            if (st.HasGrowth(index)) { r.Reason = StoreRefusal.Claimed; return r; }
            StoreMilestone m = d.Milestones[index];
            if (!session.Profile.HasClearedStage(m.StageId)) { r.Reason = StoreRefusal.NotReached; return r; }

            List<StoreGrant> grants = new List<StoreGrant>();
            if (m.Hours > 0.0) grants.Add(GoldGrant(session, m.Hours, ""));
            if (m.DustSweeps > 0.0) grants.Add(DustGrant(session, m.DustSweeps));
            Stamp(session, EffectiveDay(session, nowUnix));
            st.GrowthMask |= 1 << index;
            Apply(session, grants, nowUnix);
            r.Grants.AddRange(grants);
            r.Success = true;
            return r;
        }

        // ------------------------------------------------------------------ claims: the paid track

        /// <summary>True when the paid track of the season the clock is in has been bought.</summary>
        public static bool PassOwned(GameSession session, long nowUnix)
        {
            StoreOfferDef d = session.Content.Store.Offer(PassOfferId);
            if (d == null || !Active(session) || !WayfarersRoad.Active(session)) return false;
            long season = WayfarersRoad.SeasonOf(session, EffectiveDay(session, nowUnix));
            return season > 0L && session.Profile.Store.CountOf(d.Id, season) > 0;
        }

        /// <summary>Claims one paid tier of a bought pass once the road has reached it. A selector needs heroId,
        /// a hero of the tier's star count from WayfarersRoad.SelectorHeroes; the copy is Sealed.</summary>
        public static StoreFulfilment ClaimPassTier(GameSession session, int tier, string heroId, long nowUnix)
        {
            StoreFulfilment r = new StoreFulfilment();
            if (!Active(session)) { r.Reason = StoreRefusal.Off; return r; }
            if (!WayfarersRoad.Active(session)) { r.Reason = StoreRefusal.PassClosed; return r; }
            if (!PassOwned(session, nowUnix)) { r.Reason = StoreRefusal.NotBought; return r; }
            WayfarersRoad.Roll(session, nowUnix);
            if (session.Profile.WayfarersRoad.SeasonIndex == 0) { r.Reason = StoreRefusal.PassClosed; return r; }
            PassRewardDef p = session.Content.WayfarersRoad.PaidAt(tier);
            if (p == null) { r.Reason = StoreRefusal.Unknown; return r; }
            StoreState st = session.Profile.Store;
            long season = WayfarersRoad.SeasonOf(session, EffectiveDay(session, nowUnix));
            bool claimed = st.PaidSeason == season && st.HasPaid(tier);
            if (claimed) { r.Reason = StoreRefusal.Claimed; return r; }
            if (tier > WayfarersRoad.Reached(session)) { r.Reason = StoreRefusal.NotReached; return r; }

            List<StoreGrant> grants = new List<StoreGrant>();
            switch (p.Kind)
            {
                case PassRewardKind.Hourglass:
                    grants.Add(GoldGrant(session, p.Amount, ""));
                    break;
                case PassRewardKind.Slag:
                    grants.Add(SlagGrant((int)Math.Round(p.Amount)));
                    break;
                case PassRewardKind.Dust:
                    grants.Add(DustGrant(session, p.Amount));
                    break;
                case PassRewardKind.Selector:
                {
                    HeroDef hero = string.IsNullOrEmpty(heroId) ? null : session.Content.GetHero(heroId);
                    if (hero == null || hero.Stars != p.Stars || !WayfarersRoad.SelectorHeroes(session, p.Stars).Contains(hero))
                    {
                        r.Reason = StoreRefusal.BadChoice;
                        return r;
                    }
                    grants.Add(HeroGrant(session, heroId, 1));
                    break;
                }
                case PassRewardKind.Cosmetic:
                {
                    StoreGrant g = new StoreGrant();
                    g.Kind = StoreGrantKind.Cosmetic;
                    g.Line = (p.Label.Length > 0 ? p.Label.ToUpperInvariant() : "COSMETIC") + " (PLACEHOLDER)";
                    grants.Add(g);
                    break;
                }
            }

            Stamp(session, EffectiveDay(session, nowUnix));
            if (st.PaidSeason != season) { st.PaidSeason = season; st.PaidMask = 0; }
            st.PaidMask |= 1 << (tier - 1);
            r.Hero = Apply(session, grants, nowUnix);
            r.Grants.AddRange(grants);
            r.Success = true;
            return r;
        }

        // ------------------------------------------------------------------ the view

        public static StoreView View(GameSession session, long nowUnix)
        {
            StoreView v = new StoreView();
            if (!Active(session)) return v;
            v.Active = true;
            StoreState st = session.Profile.Store;
            long day = EffectiveDay(session, nowUnix);
            List<StoreOfferDef> all = session.Content.Store.Offers;

            for (int i = 0; i < all.Count; i++)
            {
                StoreOfferDef d = all[i];
                if (!Listed(d)) continue;
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
                o.NeedsChoice = d.Kind == StoreOfferKind.Starter || d.Kind == StoreOfferKind.Contract;

                // A pick-needing offer is open when at least one pick would pass, so the card says SOLD OUT or open
                // before a hero is picked. The real check at purchase uses the pick.
                o.Reason = Check(session, d.Id, o.Choices.Count > 0 ? o.Choices[0].HeroId : null, nowUnix);
                if (d.Kind == StoreOfferKind.Contract)
                {
                    o.Reason = o.Choices.Count == 0 ? StoreRefusal.NotOwned : o.Reason;
                    for (int c = 1; c < o.Choices.Count && o.Reason != StoreRefusal.None; c++)
                        if (Check(session, d.Id, o.Choices[c].HeroId, nowUnix) == StoreRefusal.None) o.Reason = StoreRefusal.None;
                }
                o.Available = o.Reason == StoreRefusal.None;

                if (o.Reason == StoreRefusal.SoldOut && d.Kind != StoreOfferKind.Ledger)
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
                        mv.Claimed = st.HasGrowth(m);
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
            if (d.Kind == StoreOfferKind.Starter)
            {
                for (int i = 0; i < d.HeroChoices.Count; i++) AddChoice(session, o, d.HeroChoices[i]);
            }
            else if (d.Kind == StoreOfferKind.Contract)
            {
                List<HeroInstance> owned = session.Profile.Heroes;
                for (int i = 0; i < owned.Count; i++)
                {
                    HeroDef hero = session.Content.GetHero(owned[i].DefId);
                    if (hero != null && hero.Stars == d.Stars) AddChoice(session, o, hero.Id);
                }
            }
        }

        private static void AddChoice(GameSession session, StoreOfferView o, string heroId)
        {
            HeroDef hero = session.Content.GetHero(heroId);
            if (hero == null) return;
            StoreChoice c = new StoreChoice();
            c.HeroId = hero.Id;
            c.Name = hero.Name;
            c.Role = hero.Role.ToString();
            o.Choices.Add(c);
        }

        private static void FillPass(GameSession session, StoreView v, long nowUnix)
        {
            PassView road = WayfarersRoad.View(session, nowUnix);
            v.PassOpen = road.Active;
            v.PassNeedsLoop = !DailyLoop.Active(session);
            if (!road.Active) return;
            v.PassSeason = road.Season;
            v.PassDaysLeft = road.DaysLeft;
            v.PassReached = road.Reached;
            v.PassOwned = PassOwned(session, nowUnix);
            StoreState st = session.Profile.Store;
            WayfarersRoadConfig cfg = session.Content.WayfarersRoad;
            for (int i = 0; i < cfg.Paid.Count; i++)
            {
                PassRewardDef p = cfg.Paid[i];
                StorePaidTierView t = new StorePaidTierView();
                t.Tier = p.Tier;
                t.Kind = p.Kind;
                t.Stars = p.Stars;
                t.Line = PaidLine(session, p);
                t.Reached = p.Tier <= road.Reached;
                t.Claimed = st.PaidSeason == road.Season && st.HasPaid(p.Tier);
                t.CanClaim = v.PassOwned && t.Reached && !t.Claimed;
                if (t.CanClaim) v.AnythingToClaim = true;
                v.Paid.Add(t);
            }
        }

        private static string PaidLine(GameSession session, PassRewardDef p)
        {
            switch (p.Kind)
            {
                case PassRewardKind.Hourglass: return HoursText(p.Amount) + " OF IDLE GOLD = " + Big(GoldOfHours(session, p.Amount)) + " Gold (SEALED)";
                case PassRewardKind.Slag: return Big((long)p.Amount) + " SLAG (SEALED)";
                case PassRewardKind.Dust: return Num(p.Amount) + " SWEEPS OF DUST";
                case PassRewardKind.Selector: return "A " + p.Stars + "-STAR OF YOUR CHOICE (SEALED)";
            }
            return (p.Label.Length > 0 ? p.Label.ToUpperInvariant() : "COSMETIC") + " (PLACEHOLDER)";
        }

        // ------------------------------------------------------------------ value, in days of income

        /// <summary>Hours of idle that make "a day of frontier income": the idle chest holds idleCapHours and
        /// is collected twice a day (IDEAS-ECONOMY-PACKS section 1).</summary>
        public static double IdleDayHours(GameSession session)
        {
            return 2.0 * Math.Max(1.0, session.Balance.IdleCapHours);
        }

        /// <summary>Days of frontier Shards income one random 5-star costs at the base summon rate, for this player.</summary>
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

        /// <summary>What a purchase of the offer is worth at this player's frontier, in days of income.
        /// Gold is hours over the idle day. Dust is sweeps over the day's repeat clears of the frontier stage.
        /// A hero copy is the days of Shards a random 5-star costs. Slag is counted in units, not days, because
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
                // the idle share: percent of the Gold of every idle claim over the run, two claims a day
                hours += d.IdleBonusPct / 100.0 * dayHours * d.Days;
            }
            else if (d.Kind == StoreOfferKind.Growth)
            {
                for (int i = 0; i < d.Milestones.Count; i++) { hours += d.Milestones[i].Hours; sweeps += d.Milestones[i].DustSweeps; }
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
            v.DustDays = sweeps / Math.Max(1, session.Balance.RepeatFullClearsPerDay);
            v.HeroCopies = copies;
            v.HeroDays = copies * DaysPerFiveStarPull(session);
            v.TotalDays = v.GoldDays + v.DustDays + v.HeroDays;
            return v;
        }
    }
}
