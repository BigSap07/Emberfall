using System;
using System.Collections.Generic;
using System.Globalization;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.Core.Progression
{
    /// <summary>Where a point came from.</summary>
    public enum PassPointSource
    {
        Order,
        DailyChest,
        WeeklyChest
    }

    /// <summary>The purse a bought tier is paid from. Both are earned: Valor from the Proving, Coda from the Recital.</summary>
    public enum PassCurrency
    {
        Valor,
        Coda
    }

    /// <summary>Why a pass call was refused. None is success.</summary>
    public enum PassRefusal
    {
        None,
        /// <summary>The switch is off or the file holds nothing.</summary>
        Off,
        /// <summary>No season is running (the clock is before the epoch).</summary>
        Closed,
        /// <summary>That tier is not reached yet.</summary>
        NotReached,
        /// <summary>That tier is already claimed.</summary>
        Claimed,
        /// <summary>A selector without a valid hero of the right stars.</summary>
        BadChoice,
        /// <summary>No such tier.</summary>
        NoSuchTier,
        /// <summary>The season's bought tiers are used up.</summary>
        CapReached,
        /// <summary>The track is already complete.</summary>
        Full,
        /// <summary>The purse is short of the price.</summary>
        NotEnough,
        /// <summary>The paid track. Always.</summary>
        PaidLocked
    }

    /// <summary>The outcome of a claim or a purchase.</summary>
    public sealed class PassResult
    {
        public bool Success;
        public PassRefusal Reason = PassRefusal.None;
        public long Price;
        public int Gold, Shards, Dust, Slag;
        public HeroInstance Hero;
    }

    /// <summary>One tier as the screen draws it. Read only.</summary>
    public sealed class PassTierView
    {
        public int Tier;
        public PassRewardKind Kind;
        public int Stars;
        /// <summary>What it pays, in words: "1 HOUR OF IDLE", "20 SLAG", "10 SWEEPS OF DUST", "A 4-STAR OF YOUR CHOICE".</summary>
        public string Line = "";
        /// <summary>What that is worth to this player now: "= 670 Gold, 17 Shards", "= 1000 Dust". Empty when it is not a number.</summary>
        public string Detail = "";
        public bool Reached;
        public bool Claimed;
        public bool CanClaim;
    }

    /// <summary>The paid track's row, locked.</summary>
    public sealed class PassPaidView
    {
        public int Tier;
        public string Line = "";
    }

    /// <summary>The whole pass at one moment, for the screen to read.</summary>
    public sealed class PassView
    {
        public bool Active;
        public long Season;
        /// <summary>Reward days left in the season, today included.</summary>
        public int DaysLeft;
        public long ResetInSeconds;
        public int Points;
        public int TierPoints;
        public int TotalPoints;
        public int Reached;
        public int Bought;
        public int BuyMax;
        public long ValorPrice;
        public long CodaPrice;
        public bool CanBuyWithValor;
        public bool CanBuyWithCoda;
        public readonly List<PassTierView> Tiers = new List<PassTierView>();
        /// <summary>Empty unless PassPaidSwitch is on.</summary>
        public readonly List<PassPaidView> Paid = new List<PassPaidView>();
    }

    /// <summary>
    /// WAYFARER'S ROAD, THE FREE TRACK (Docs/DESIGN-WAYFARERS-ROAD.md): a 28 day season of 30 tiers
    /// whose only points are the daily loop's claims. Pure rules over a GameSession's profile and
    /// content; the screen only calls these and reads PassView.
    ///
    /// ONE CLOCK. The season is a block of reward days (GameSession.DayStartUnix, the clock the daily
    /// loop, the repeat cap and the sinks use) counted from the file's epoch, so every player is in the
    /// same season. The stamp (WayfarersRoadState.SeasonIndex) only moves forward: a clock wound back
    /// changes nothing, and a clock jumped forward lands in the season it names and pays nothing for the
    /// ones it skipped.
    ///
    /// EVERY REWARD IS FIXED AND SHOWN UP FRONT (PassView.Tiers). A selector grants a plain earned
    /// copy, never Sealed. The paid track has no purchase path: ClaimPaid refuses every call.
    ///
    /// OFF MEANS INERT. With WayfarersRoadSwitch off, or with no usable wayfarers-road.json, every call
    /// returns at once and writes nothing to the profile.
    /// </summary>
    public static class WayfarersRoad
    {
        public static bool Active(GameSession session)
        {
            return WayfarersRoadSwitch.Enabled && session != null && session.Content.WayfarersRoad.IsUsable;
        }

        // ------------------------------------------------------------------ the season

        /// <summary>The season a clock reading falls in: 1 for the first season from the epoch, 0 before it.</summary>
        public static long SeasonOf(GameSession session, long nowUnix)
        {
            WayfarersRoadConfig cfg = session.Content.WayfarersRoad;
            long rel = DailyLoop.DayNumber(session, session.DayStartUnix(nowUnix)) - cfg.EpochDay;
            return rel < 0 ? 0L : rel / cfg.SeasonDays + 1L;
        }

        /// <summary>The first reward day (unix seconds) of the season after this one.</summary>
        public static long SeasonEndUnix(GameSession session, long season)
        {
            WayfarersRoadConfig cfg = session.Content.WayfarersRoad;
            return (cfg.EpochDay + season * cfg.SeasonDays) * 86400L + (long)session.Balance.DailyResetHourUtc * 3600L;
        }

        /// <summary>
        /// Moves the pass to the season nowUnix falls in and answers whether that changed anything. A
        /// later season starts empty (unclaimed tiers lapse); the same season or an earlier one (a clock
        /// wound back) changes nothing.
        /// </summary>
        public static bool Roll(GameSession session, long nowUnix)
        {
            if (!Active(session)) return false;
            long season = SeasonOf(session, nowUnix);
            WayfarersRoadState st = session.Profile.WayfarersRoad;
            if (season <= st.SeasonIndex) return false;
            st.SeasonIndex = season;
            st.Points = 0;
            st.ClaimedMask = 0;
            st.Bought = 0;
            st.PaidClaimedMask = 0;
            return true;
        }

        // ------------------------------------------------------------------ points

        public static int TotalPoints(GameSession session)
        {
            WayfarersRoadConfig cfg = session.Content.WayfarersRoad;
            return cfg.TierPoints * WayfarersRoadConfig.TierCount;
        }

        /// <summary>Tiers reached: the points divided by a tier's worth, plus the tiers bought, never past the top.</summary>
        public static int Reached(GameSession session)
        {
            WayfarersRoadState st = session.Profile.WayfarersRoad;
            WayfarersRoadConfig cfg = session.Content.WayfarersRoad;
            return Math.Min(WayfarersRoadConfig.TierCount, st.Points / Math.Max(1, cfg.TierPoints) + st.Bought);
        }

        /// <summary>
        /// Called by DailyLoop at the moment a claim is paid (an order, the daily chest, the weekly
        /// chest), never by a screen. Counts the points toward the season the clock is in.
        /// </summary>
        public static void OnClaim(GameSession session, PassPointSource source, long nowUnix)
        {
            if (!Active(session)) return;
            Roll(session, nowUnix);
            WayfarersRoadState st = session.Profile.WayfarersRoad;
            if (st.SeasonIndex == 0) return;
            WayfarersRoadConfig cfg = session.Content.WayfarersRoad;
            int points = source == PassPointSource.Order ? cfg.OrderPoints
                       : source == PassPointSource.DailyChest ? cfg.DailyChestPoints : cfg.WeeklyChestPoints;
            st.Points = Math.Min(TotalPoints(session), st.Points + points);
        }

        // ------------------------------------------------------------------ rewards

        /// <summary>The Relic Dust one "sweep" of a dust reward is worth: a repeat clear of the frontier stage.</summary>
        public static int DustPerSweep(GameSession session)
        {
            StageDef stage = session.FurthestClearedStage;
            return stage != null ? session.RelicDustForRepeatClear(stage) : session.Balance.RelicDustPerRepeatClear;
        }

        /// <summary>The heroes a selector of this star count may pick: every hero of those stars that is
        /// not a starting hero, and for a 5-star only a fusable one (the pool a conversion draws from).</summary>
        public static List<HeroDef> SelectorHeroes(GameSession session, int stars)
        {
            List<HeroDef> pool = new List<HeroDef>();
            List<HeroDef> all = session.Content.HeroesOfStars(stars);
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].StartingHero) continue;
                if (stars >= 5 && !all[i].Fusable) continue;
                pool.Add(all[i]);
            }
            return pool;
        }

        private static string Num(double v)
        {
            return v.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private static string LineOf(PassRewardDef d)
        {
            switch (d.Kind)
            {
                case PassRewardKind.Hourglass: return Num(d.Amount) + (d.Amount == 1.0 ? " HOUR" : " HOURS") + " OF IDLE";
                case PassRewardKind.Dust: return Num(d.Amount) + " SWEEPS OF DUST";
                case PassRewardKind.Slag: return Num(d.Amount) + " SLAG";
                case PassRewardKind.Selector: return "A " + d.Stars + "-STAR OF YOUR CHOICE";
            }
            return d.Label.ToUpperInvariant();
        }

        private static string DetailOf(GameSession session, PassRewardDef d)
        {
            if (d.Kind == PassRewardKind.Hourglass)
            {
                IdleReport y = session.IdleYield((long)Math.Round(d.Amount * 3600.0));
                return "= " + y.Gold + " Gold, " + y.Shards + " Shards";
            }
            if (d.Kind == PassRewardKind.Dust) return "= " + (long)Math.Round(d.Amount * DustPerSweep(session)) + " Dust";
            return "";
        }

        /// <summary>Claims one reached tier. A selector needs heroId, a hero of the tier's star count from SelectorHeroes.
        /// Everything is checked before anything is written.</summary>
        public static PassResult Claim(GameSession session, int tier, string heroId, long nowUnix)
        {
            PassResult r = new PassResult();
            if (!Active(session)) { r.Reason = PassRefusal.Off; return r; }
            Roll(session, nowUnix);
            WayfarersRoadState st = session.Profile.WayfarersRoad;
            if (st.SeasonIndex == 0) { r.Reason = PassRefusal.Closed; return r; }
            PassRewardDef d = session.Content.WayfarersRoad.Tier(tier);
            if (d == null) { r.Reason = PassRefusal.NoSuchTier; return r; }
            if (st.HasClaimed(tier)) { r.Reason = PassRefusal.Claimed; return r; }
            if (tier > Reached(session)) { r.Reason = PassRefusal.NotReached; return r; }

            HeroDef hero = null;
            if (d.Kind == PassRewardKind.Selector)
            {
                hero = string.IsNullOrEmpty(heroId) ? null : session.Content.GetHero(heroId);
                if (hero == null || hero.Stars != d.Stars || !SelectorHeroes(session, d.Stars).Contains(hero))
                {
                    r.Reason = PassRefusal.BadChoice;
                    return r;
                }
            }

            // ---- the one write block
            PlayerProfile p = session.Profile;
            switch (d.Kind)
            {
                case PassRewardKind.Hourglass:
                    IdleReport y = session.IdleYield((long)Math.Round(d.Amount * 3600.0));
                    p.Gold += y.Gold;
                    p.Shards += y.Shards;
                    r.Gold = y.Gold;
                    r.Shards = y.Shards;
                    break;
                case PassRewardKind.Dust:
                    r.Dust = (int)Math.Round(d.Amount * DustPerSweep(session));
                    p.RelicDust += r.Dust;
                    break;
                case PassRewardKind.Slag:
                    r.Slag = (int)Math.Round(d.Amount);
                    p.Slag += r.Slag;
                    break;
                case PassRewardKind.Selector:
                    r.Hero = session.GrantHeroDirect(hero, nowUnix);
                    break;
            }
            st.ClaimedMask |= 1 << (tier - 1);
            r.Success = true;
            return r;
        }

        /// <summary>The paid track without a clock or a choice: refuses every call, with the switch on or
        /// off, and writes nothing. A paid claim needs the four argument form below.</summary>
        public static PassResult ClaimPaid(GameSession session, int tier)
        {
            PassResult r = new PassResult();
            r.Reason = PassRefusal.PaidLocked;
            return r;
        }

        /// <summary>
        /// Claims one reached tier of the PAID track (R19). Open only when StoreSwitch is on and the player
        /// bought this season's premium (WayfarersRoadState.PaidSeason equals the season the clock is in);
        /// otherwise it refuses PaidLocked and writes nothing, not even a season roll. Its reward is paid
        /// through PurchaseGrants.Apply like every purchase, so it is SEALED (a Sealed 5-star copy at tier
        /// 30). A cosmetic marks the tier claimed and grants nothing. heroId is the pick for a selector.
        /// </summary>
        public static PassResult ClaimPaid(GameSession session, int tier, string heroId, long nowUnix)
        {
            PassResult r = new PassResult();
            r.Reason = PassRefusal.PaidLocked;
            if (!StoreSwitch.Enabled || !Active(session)) return r;
            WayfarersRoadState st = session.Profile.WayfarersRoad;
            long season = SeasonOf(session, nowUnix);
            if (season == 0 || st.PaidSeason != season) return r;
            Roll(session, nowUnix);
            PassRewardDef d = session.Content.WayfarersRoad.PaidAt(tier);
            if (d == null) { r.Reason = PassRefusal.NoSuchTier; return r; }
            if (st.HasClaimedPaid(tier)) { r.Reason = PassRefusal.Claimed; return r; }
            if (tier > Reached(session)) { r.Reason = PassRefusal.NotReached; return r; }

            PurchaseGrant grant = new PurchaseGrant();
            switch (d.Kind)
            {
                case PassRewardKind.Hourglass:
                    grant.Add(new GrantItem(GrantKind.IdleGoldHours, d.Amount));
                    break;
                case PassRewardKind.Dust:
                    grant.Add(new GrantItem(GrantKind.Dust, Math.Round(d.Amount * DustPerSweep(session))));
                    break;
                case PassRewardKind.Slag:
                    grant.Add(new GrantItem(GrantKind.Slag, Math.Round(d.Amount)));
                    break;
                case PassRewardKind.Selector:
                    GrantItem pick = new GrantItem(GrantKind.HeroChoice, 0);
                    List<HeroDef> pool = SelectorHeroes(session, d.Stars);
                    for (int i = 0; i < pool.Count; i++) pick.HeroIds.Add(pool[i].Id);
                    grant.Add(pick);
                    break;
                default:
                    grant.Add(new GrantItem(GrantKind.Activate, 0));
                    break;
            }
            GrantResult g = PurchaseGrants.Apply(session, grant, heroId, nowUnix);
            if (!g.Success) { r.Reason = g.Reason == GrantRefusal.BadChoice ? PassRefusal.BadChoice : PassRefusal.Closed; return r; }
            st.PaidClaimedMask |= 1 << (tier - 1);
            r.Gold = (int)Math.Min(int.MaxValue, g.Gold);
            r.Slag = (int)g.Slag;
            r.Dust = (int)g.Dust;
            r.Hero = g.HeroId.Length > 0 ? session.Profile.FindHeroByDefId(g.HeroId) : null;
            r.Reason = PassRefusal.None;
            r.Success = true;
            return r;
        }

        // ------------------------------------------------------------------ buying a tier

        /// <summary>What buying a tier costs in this purse: the file's days of that purse's income at the
        /// player's best standing or floor (SinkPricing), rounded up, never below one step.</summary>
        public static long BuyPrice(GameSession session, PassCurrency currency)
        {
            WayfarersRoadConfig cfg = session.Content.WayfarersRoad;
            long day = SinkPricing.DayOf(session, currency == PassCurrency.Valor ? SinkShopId.Armory : SinkShopId.Archive);
            long step = Math.Max(1, cfg.PriceRound);
            long steps = (long)Math.Ceiling(cfg.BuyTierDays * day / step - 1e-9);
            return Math.Max(1L, steps) * step;
        }

        private static PassRefusal BuyRefusal(GameSession session, PassCurrency currency, long price)
        {
            WayfarersRoadState st = session.Profile.WayfarersRoad;
            if (st.SeasonIndex == 0) return PassRefusal.Closed;
            if (st.Bought >= session.Content.WayfarersRoad.BuyTierMax) return PassRefusal.CapReached;
            if (Reached(session) >= WayfarersRoadConfig.TierCount) return PassRefusal.Full;
            long purse = currency == PassCurrency.Valor ? session.Profile.Valor : session.Profile.Coda;
            if (purse < price) return PassRefusal.NotEnough;
            return PassRefusal.None;
        }

        /// <summary>Buys one tier with Valor or Coda: at most BuyTierMax a season, never past the top.
        /// Valor and Coda are earned only. The tier reached rises by one; its reward is claimed as any other.</summary>
        public static PassResult BuyTier(GameSession session, PassCurrency currency, long nowUnix)
        {
            PassResult r = new PassResult();
            if (!Active(session)) { r.Reason = PassRefusal.Off; return r; }
            Roll(session, nowUnix);
            r.Price = BuyPrice(session, currency);
            r.Reason = BuyRefusal(session, currency, r.Price);
            if (r.Reason != PassRefusal.None) return r;

            if (currency == PassCurrency.Valor) session.Profile.Valor -= r.Price;
            else session.Profile.Coda -= r.Price;
            session.Profile.WayfarersRoad.Bought++;
            r.Success = true;
            return r;
        }

        // ------------------------------------------------------------------ what the screen reads

        /// <summary>
        /// The same view as View, READ ONLY: the season is rolled on a copy of the road's state, so a stored season
        /// older than the one the clock is in reads as no points and no claims, and nothing is written to the profile.
        /// The Hub's Store badge and StoreOffers.View read the road this way, so seeing the Hub never moves a save
        /// (a roll writes the season, and a save holding a season is a version 24 document). Opening the Pass tab uses
        /// View, which rolls, as the road's own screen does.
        /// </summary>
        public static PassView Peek(GameSession session, long nowUnix)
        {
            if (!Active(session)) return new PassView();
            WayfarersRoadState real = session.Profile.WayfarersRoad;
            session.Profile.WayfarersRoad = real.Clone();
            try { return View(session, nowUnix); }
            finally { session.Profile.WayfarersRoad = real; }
        }

        public static PassView View(GameSession session, long nowUnix)
        {
            PassView v = new PassView();
            if (!Active(session)) return v;
            Roll(session, nowUnix);
            WayfarersRoadState st = session.Profile.WayfarersRoad;
            if (st.SeasonIndex == 0) return v;
            WayfarersRoadConfig cfg = session.Content.WayfarersRoad;
            v.Active = true;
            v.Season = st.SeasonIndex;
            long end = SeasonEndUnix(session, st.SeasonIndex);
            v.ResetInSeconds = Math.Max(0L, end - nowUnix);
            v.DaysLeft = (int)Math.Max(1L, (v.ResetInSeconds + 86399L) / 86400L);
            v.Points = st.Points;
            v.TierPoints = cfg.TierPoints;
            v.TotalPoints = TotalPoints(session);
            v.Reached = Reached(session);
            v.Bought = st.Bought;
            v.BuyMax = cfg.BuyTierMax;
            v.ValorPrice = BuyPrice(session, PassCurrency.Valor);
            v.CodaPrice = BuyPrice(session, PassCurrency.Coda);
            v.CanBuyWithValor = BuyRefusal(session, PassCurrency.Valor, v.ValorPrice) == PassRefusal.None;
            v.CanBuyWithCoda = BuyRefusal(session, PassCurrency.Coda, v.CodaPrice) == PassRefusal.None;

            for (int i = 0; i < cfg.Tiers.Count; i++)
            {
                PassRewardDef d = cfg.Tiers[i];
                PassTierView t = new PassTierView();
                t.Tier = d.Tier;
                t.Kind = d.Kind;
                t.Stars = d.Stars;
                t.Line = LineOf(d);
                t.Detail = DetailOf(session, d);
                t.Reached = d.Tier <= v.Reached;
                t.Claimed = st.HasClaimed(d.Tier);
                t.CanClaim = t.Reached && !t.Claimed;
                v.Tiers.Add(t);
            }
            if (PassPaidSwitch.Enabled)
            {
                for (int i = 0; i < cfg.Paid.Count; i++)
                {
                    PassPaidView p = new PassPaidView();
                    p.Tier = cfg.Paid[i].Tier;
                    p.Line = LineOf(cfg.Paid[i]);
                    v.Paid.Add(p);
                }
            }
            return v;
        }
    }
}
