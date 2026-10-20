using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Emberfall.Core.Data;
using Emberfall.Core.Json;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Save;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// WAYFARER'S ROAD, THE FREE TRACK (Docs/DESIGN-WAYFARERS-ROAD.md): a 28 day season of 30 tiers whose
    /// only points are the daily loop's claims. One named check per rule: the content, the sizing (24
    /// active days finish the track, 23 do not), the points source, every reward, the selectors (earned,
    /// never Sealed), buying tiers, every refusal leaving the save untouched, the season rollover across
    /// clock changes, the save, the file staying out of ContentVersion, the paid track that cannot be
    /// unlocked, the switch and the wiring.
    ///
    /// Every claim goes through the real DailyLoop and WayfarersRoad calls with an explicit clock; a check
    /// sets a purse only to name the situation it wants.
    /// </summary>
    internal static partial class Program
    {
        private static void CheckWayfarersRoad(ContentDatabase db, string contentDir)
        {
            Line("");
            Line("WAYFARER'S ROAD (Docs/DESIGN-WAYFARERS-ROAD.md: the free track, 28 days, 30 tiers, points only from the daily loop)");
            bool was = WayfarersRoadSwitch.Enabled, loopWas = DailyLoopSwitch.Enabled, paidWas = PassPaidSwitch.Enabled, liveWas = LiveEventsSwitch.Enabled;
            try
            {
                WayfarersRoadSwitch.Enabled = true;
                DailyLoopSwitch.Enabled = true;
                PassPaidSwitch.Enabled = false;
                LiveEventsSwitch.Enabled = false;   // the live events count the same claims and would add their own block to every save compared here
                CheckPassContent(db);
                CheckPassSizing(db);
                CheckPassRewards(db);
                CheckPassSelectors(db);
                CheckPassBuying(db);
                CheckPassRollover(db);
                CheckPassSave(db);
                CheckPassNotHashed(db, contentDir);
                CheckPassPaidTrack(db);
                CheckPassOff(db);
                CheckPassWiring(contentDir);
            }
            finally
            {
                WayfarersRoadSwitch.Enabled = was;
                DailyLoopSwitch.Enabled = loopWas;
                PassPaidSwitch.Enabled = paidWas;
                LiveEventsSwitch.Enabled = liveWas;
            }
            Check(WayfarersRoadSwitch.Enabled == was && DailyLoopSwitch.Enabled == loopWas && PassPaidSwitch.Enabled == paidWas,
                  "the switches are put back as the run found them (road " + (was ? "on" : "off") + ", paid " + (paidWas ? "on" : "off") + ")");
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>The first reward day of season n (n of 1 is the first from the epoch), at the reset hour.</summary>
        private static long PassSeasonStart(ContentDatabase db, long season)
        {
            return (db.WayfarersRoad.EpochDay + (season - 1) * db.WayfarersRoad.SeasonDays) * 86400L + (long)db.Balance.DailyResetHourUtc * 3600L;
        }

        private static GameSession PassSession(ContentDatabase db, long gold, long valor, long coda)
        {
            PlayerProfile p = GameSession.CreateNewProfile(db, SinkBase);
            for (int i = 0; i < db.AllStages.Count; i++) p.ClearedStageIds.Add(db.AllStages[i].Id);
            p.StagesCleared = CampaignProgress.CountCleared(p, db);
            p.Gold = gold;
            p.Valor = valor;
            p.Coda = coda;
            p.ProvingHighestStanding = 5;
            p.RecitalHighestFloor = 10;
            p.RecitalCurrentFloor = 11;
            return new GameSession(db, p);
        }

        /// <summary>One active day at `now`: finish every order on the board, claim each, open the daily chest.
        /// Returns the points the pass gained.</summary>
        private static int PassPlayDay(GameSession s, long now)
        {
            int before = s.Profile.WayfarersRoad.Points;
            DailyLoop.Roll(s, now);
            DailyLoopState st = s.Profile.DailyLoop;
            for (int i = 0; i < st.Orders.Count; i++)
            {
                OrderDef def = s.Content.Orders.Order(st.Orders[i].Id);
                if (def == null) continue;
                DailyLoop.Record(s, def.Kind, def.Target, now);
                DailyLoop.ClaimOrder(s, def.Id, now);
            }
            DailyLoop.ClaimDailyChest(s, now);
            return s.Profile.WayfarersRoad.Points - before;
        }

        /// <summary>Plays one season from its first reward day: every day active but the listed rest days (0 based),
        /// opening the weekly chest as soon as the loop allows. perDay is the points the first active day paid.</summary>
        private static GameSession PassPlaySeason(ContentDatabase db, long start, int[] rest, out int perDay)
        {
            GameSession s = PassSession(db, 0, 0, 0);
            perDay = 0;
            bool first = true;
            for (int d = 0; d < db.WayfarersRoad.SeasonDays; d++)
            {
                if (Array.IndexOf(rest, d) >= 0) continue;
                long now = start + d * 86400L + 600;
                int gained = PassPlayDay(s, now);
                if (first) { perDay = gained; first = false; }
                DailyLoop.ClaimWeeklyChest(s, now + 60);
            }
            return s;
        }

        private static string PassSnapshot(GameSession s) { return SaveSerializer.Serialize(s.Profile); }

        // ------------------------------------------------------------------ 1. content

        private static void CheckPassContent(ContentDatabase db)
        {
            WayfarersRoadConfig c = db.WayfarersRoad;
            Check(c.IsUsable && c.Warnings.Count == 0 && c.Tiers.Count == 30 && c.SeasonDays == 28,
                  "the shipped wayfarers-road.json loads clean: " + c.Tiers.Count + " tiers, a " + c.SeasonDays + " day season, " + c.Paid.Count + " paid rewards, no warnings");

            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(c.EpochDay);
            Check(epoch.DayOfWeek == DayOfWeek.Monday && c.SeasonDays % 7 == 0,
                  "the epoch (" + epoch.ToString("yyyy-MM-dd") + ") is a Monday and a season is whole weeks, so a season holds exactly " + (c.SeasonDays / 7) + " weekly chests' weeks");

            PassRewardDef t15 = c.Tier(15), t30 = c.Tier(30);
            Check(t15.Kind == PassRewardKind.Selector && t15.Stars == 4 && t30.Kind == PassRewardKind.Selector && t30.Stars == 5,
                  "a 4-star selector sits at tier 15 and a 5-star selector at tier 30");
            bool hour = false, dust = false, slag = false, selectorsOnly = true;
            for (int i = 0; i < c.Tiers.Count; i++)
            {
                if (c.Tiers[i].Kind == PassRewardKind.Hourglass) hour = true;
                if (c.Tiers[i].Kind == PassRewardKind.Dust) dust = true;
                if (c.Tiers[i].Kind == PassRewardKind.Slag) slag = true;
                if (c.Tiers[i].Kind == PassRewardKind.Selector && c.Tiers[i].Tier != 15 && c.Tiers[i].Tier != 30) selectorsOnly = false;
            }
            Check(hour && dust && slag && selectorsOnly && c.Paid.Count > 0 && c.PaidAt(30) != null,
                  "the free track carries hourglasses, Dust and Slag and exactly the two selectors, and the paid track exists in data");

            WayfarersRoadConfig bad = WayfarersRoadConfig.FromJson("{ \"epoch\": \"2026-01-05\", \"tiers\": [ { \"tier\": 1, \"kind\": \"slag\", \"amount\": 5 } ] }");
            WayfarersRoadConfig badKind = WayfarersRoadConfig.FromJson("{ \"epoch\": \"nonsense\", \"tiers\": [ { \"tier\": 1, \"kind\": \"mystery\" } ] }");
            Check(!bad.IsUsable && bad.Warnings.Count > 0 && !badKind.IsUsable && !WayfarersRoadConfig.FromJson(null).IsUsable,
                  "a file with the wrong number of tiers, an unknown kind or a bad epoch is not usable, each with a warning, and a missing file runs nothing");
        }

        // ------------------------------------------------------------------ 2. sizing

        private static void CheckPassSizing(ContentDatabase db)
        {
            WayfarersRoadConfig c = db.WayfarersRoad;
            long start = PassSeasonStart(db, 5);

            // THE TRACK IS SIZED ON A PLAYER WHO ALSO CLAIMS THE WEEKLY CHEST. A season is four whole
            // Monday to Sunday weeks. 24 active days rest on the Sundays (days 6, 13, 20, 27), so every week
            // has six complete days and its weekly chest opens; 23 rest one more day (day 5), so every week
            // still has five complete days and its chest still opens.
            int perDay;
            GameSession s24 = PassPlaySeason(db, start, new[] { 6, 13, 20, 27 }, out perDay);
            Check(perDay == 5 * c.OrderPoints + c.DailyChestPoints && s24.Profile.DailyLoop.Orders.Count == 5,
                  "an active day (five orders and the daily chest) is " + perDay + " points: 5 x " + c.OrderPoints + " + " + c.DailyChestPoints);
            int fullWeeks = 4 * (6 * perDay) + 4 * c.WeeklyChestPoints;
            Check(WayfarersRoad.Reached(s24) == 30 && s24.Profile.WayfarersRoad.Points >= WayfarersRoad.TotalPoints(s24),
                  "24 ACTIVE DAYS FINISH THE TRACK: " + s24.Profile.WayfarersRoad.Points + " points (24 x " + perDay + " + 4 weekly chests x " + c.WeeklyChestPoints
                  + ") reach tier " + WayfarersRoad.Reached(s24) + " of 30 with no bought tier, against " + WayfarersRoad.TotalPoints(s24) + " needed");
            Check(fullWeeks == 1408 && fullWeeks >= WayfarersRoad.TotalPoints(s24) && s24.Profile.DailyLoop.WeekChestClaimed, "and the arithmetic is the sizing's: 24 x 52 + 4 x " + c.WeeklyChestPoints + " = " + fullWeeks + " against " + WayfarersRoad.TotalPoints(s24) + " (the weekly chest of the last week was opened)");

            int perDay23;
            GameSession s23 = PassPlaySeason(db, start, new[] { 5, 6, 13, 20, 27 }, out perDay23);
            Check(WayfarersRoad.Reached(s23) < 30 && WayfarersRoad.Reached(s23) >= 29,
                  "and 23 do not, weekly chests counted: " + s23.Profile.WayfarersRoad.Points + " points reach tier " + WayfarersRoad.Reached(s23));

            // The weekly chest adds its points to a week of five complete days.
            GameSession weekly = PassSession(db, 0, 0, 0);
            for (int d = 0; d < 7; d++) PassPlayDay(weekly, start + d * 86400L + 600);
            int pointsBeforeChest = weekly.Profile.WayfarersRoad.Points;
            LoopPayout chest = DailyLoop.ClaimWeeklyChest(weekly, start + 6 * 86400L + 700);
            Check(chest.Success && weekly.Profile.WayfarersRoad.Points - pointsBeforeChest == c.WeeklyChestPoints,
                  "the weekly chest adds " + c.WeeklyChestPoints + " points on a week of five complete days");

            // Half a day counts for what it did.
            GameSession half = PassSession(db, 0, 0, 0);
            DailyLoop.Roll(half, start + 600);
            OrderDef first = half.Content.Orders.Order(half.Profile.DailyLoop.Orders[0].Id);
            DailyLoop.Record(half, first.Kind, first.Target, start + 600);
            DailyLoop.ClaimOrder(half, first.Id, start + 600);
            Check(half.Profile.WayfarersRoad.Points == c.OrderPoints && WayfarersRoad.Reached(half) == 0,
                  "one claimed order is " + c.OrderPoints + " points and not yet a tier: an unfinished day still counts for what it did");
        }

        // ------------------------------------------------------------------ 3. rewards

        private static void CheckPassRewards(ContentDatabase db)
        {
            long now = PassSeasonStart(db, 5) + 600;
            GameSession s = PassSession(db, 0, 0, 0);
            WayfarersRoad.OnClaim(s, PassPointSource.Order, now);   // opens the season
            s.Profile.WayfarersRoad.Points = 30 * db.WayfarersRoad.TierPoints;   // name the situation: every tier reached

            // Hourglass: tier 1 pays what the idle formula pays for its hours at the frontier.
            PassRewardDef d1 = db.WayfarersRoad.Tier(1);
            IdleReport expected = s.IdleYield((long)Math.Round(d1.Amount * 3600.0));
            long gold = s.Profile.Gold, shards = s.Profile.Shards;
            PassResult r1 = WayfarersRoad.Claim(s, 1, null, now);
            Check(r1.Success && s.Profile.Gold - gold == expected.Gold && s.Profile.Shards - shards == expected.Shards && expected.Gold > 0,
                  "AN HOURGLASS pays hours of idle income at the frontier, by the one idle formula: tier 1 pays " + r1.Gold + " Gold and " + r1.Shards + " Shards");

            // Slag: tier 2.
            long slag = s.Profile.Slag;
            PassResult r2 = WayfarersRoad.Claim(s, 2, null, now);
            Check(r2.Success && s.Profile.Slag - slag == (long)db.WayfarersRoad.Tier(2).Amount && s.Profile.FreeSlag == s.Profile.Slag && s.Profile.Sealed.IsEmpty,
                  "SLAG pays its fixed amount (" + r2.Slag + "), free Slag a reroll can spend");

            // Dust: tier 3, in sweeps of the frontier stage.
            long dust = s.Profile.RelicDust;
            int sweep = WayfarersRoad.DustPerSweep(s);
            PassResult r3 = WayfarersRoad.Claim(s, 3, null, now);
            Check(r3.Success && s.Profile.RelicDust - dust == (long)Math.Round(db.WayfarersRoad.Tier(3).Amount * sweep) && sweep == s.RelicDustForRepeatClear(s.FurthestClearedStage),
                  "DUST pays that many repeat-clear payouts of the frontier stage (" + db.WayfarersRoad.Tier(3).Amount + " x " + sweep + " = " + r3.Dust + "), so it scales with the player");

            // Every reward is on the view up front, whatever is reached.
            GameSession fresh = PassSession(db, 0, 0, 0);
            PassView view = WayfarersRoad.View(fresh, now);
            bool shown = view.Active && view.Tiers.Count == 30;
            for (int i = 0; shown && i < view.Tiers.Count; i++) if (view.Tiers[i].Line.Length == 0) shown = false;
            Check(shown && view.Reached == 0 && !view.Tiers[0].CanClaim,
                  "EVERY REWARD IS SHOWN UP FRONT: a player with no points sees all 30 tiers and what each pays, none claimable");

            // The refusals leave the save untouched.
            string before = PassSnapshot(s);
            PassResult again = WayfarersRoad.Claim(s, 1, null, now);
            PassResult none = WayfarersRoad.Claim(s, 31, null, now), zero = WayfarersRoad.Claim(s, 0, null, now);
            Check(!again.Success && again.Reason == PassRefusal.Claimed && none.Reason == PassRefusal.NoSuchTier && zero.Reason == PassRefusal.NoSuchTier && PassSnapshot(s) == before,
                  "CLAIMED TWICE, and a tier that does not exist, are refused, the save byte for byte as it was");
            GameSession low = PassSession(db, 0, 0, 0);
            DailyLoop.Roll(low, now);
            WayfarersRoad.OnClaim(low, PassPointSource.Order, now);
            string lowBefore = PassSnapshot(low);
            PassResult early = WayfarersRoad.Claim(low, 5, null, now);
            Check(!early.Success && early.Reason == PassRefusal.NotReached && PassSnapshot(low) == lowBefore,
                  "NOT REACHED: a tier above the points is refused, the save byte for byte as it was");
        }

        // ------------------------------------------------------------------ 4. selectors

        private static void CheckPassSelectors(ContentDatabase db)
        {
            long now = PassSeasonStart(db, 5) + 600;
            GameSession s = PassSession(db, 0, 0, 0);
            WayfarersRoad.OnClaim(s, PassPointSource.Order, now);
            s.Profile.WayfarersRoad.Points = 30 * db.WayfarersRoad.TierPoints;

            List<HeroDef> fours = WayfarersRoad.SelectorHeroes(s, 4), fives = WayfarersRoad.SelectorHeroes(s, 5);
            bool starting = false, fusable = true;
            for (int i = 0; i < fours.Count; i++) if (fours[i].StartingHero || fours[i].Stars != 4) starting = true;
            for (int i = 0; i < fives.Count; i++) if (!fives[i].Fusable || fives[i].Stars != 5) fusable = false;
            Check(fours.Count > 0 && fives.Count > 0 && !starting && fusable,
                  "the selectors offer " + fours.Count + " four-stars (no starting hero) and " + fives.Count + " fusable five-stars, read from the roster and never a list that goes stale");

            string before = PassSnapshot(s);
            PassResult none = WayfarersRoad.Claim(s, 15, null, now), wrong = WayfarersRoad.Claim(s, 15, fives[0].Id, now), unknown = WayfarersRoad.Claim(s, 15, "no_such_hero", now);
            Check(!none.Success && !wrong.Success && !unknown.Success && none.Reason == PassRefusal.BadChoice && wrong.Reason == PassRefusal.BadChoice
                  && PassSnapshot(s) == before,
                  "A SELECTOR NEEDS A HERO OF ITS OWN STARS: none, a five-star at the four-star tier and an unknown id are refused as BadChoice, the save untouched");

            HeroDef pick4 = null;
            for (int i = 0; i < fours.Count; i++) if (!s.Profile.Owns(fours[i].Id)) { pick4 = fours[i]; break; }
            int copies = s.Profile.Heroes.Count;
            PassResult ok4 = WayfarersRoad.Claim(s, 15, pick4.Id, now);
            HeroInstance got4 = s.Profile.FindHeroByDefId(pick4.Id);
            Check(ok4.Success && got4 != null && got4.Copies == 1 && s.Profile.Heroes.Count == copies + 1 && s.Profile.Sealed.IsEmpty && got4.SealedCopies == 0,
                  "tier 15 grants the chosen four-star as a plain copy: new to the roster, not Sealed");

            HeroDef pick5 = fives[0];
            HeroInstance owned5 = s.GrantHeroDirect(pick5, now);
            int copies5 = owned5.Copies;
            PassResult ok5 = WayfarersRoad.Claim(s, 30, pick5.Id, now);
            Check(ok5.Success && owned5.Copies == copies5 + 1 && owned5.SealedCopies == 0 && s.Profile.Sealed.IsEmpty && !SealedState.ProfileHoldsAny(s.Profile),
                  "TIER 30 GRANTS THE CHOSEN FIVE-STAR AND IT IS NOT SEALED: an owned five-star gains an earned copy that a conversion or an ascension may spend");
            Check(ok5.Hero == owned5 && ConversionSpareOk(s, owned5),
                  "and the copy counts as free to a conversion (the Sealed rule would have held it back)");
        }

        private static bool ConversionSpareOk(GameSession s, HeroInstance h)
        {
            return s.ConversionSpare(h) == h.Copies - (s.Profile.IsDeployed(h.Uid) ? 1 : 0);
        }

        // ------------------------------------------------------------------ 5. buying tiers

        private static void CheckPassBuying(ContentDatabase db)
        {
            long now = PassSeasonStart(db, 5) + 600;
            WayfarersRoadConfig c = db.WayfarersRoad;
            GameSession s = PassSession(db, 0, 0, 0);
            long valorPrice = WayfarersRoad.BuyPrice(s, PassCurrency.Valor), codaPrice = WayfarersRoad.BuyPrice(s, PassCurrency.Coda);
            long valorDay = SinkPricing.DayOf(s, SinkShopId.Armory), codaDay = SinkPricing.DayOf(s, SinkShopId.Archive);
            Check(valorPrice >= c.BuyTierDays * valorDay && valorPrice < c.BuyTierDays * valorDay + c.PriceRound && valorPrice % c.PriceRound == 0
                  && codaPrice >= c.BuyTierDays * codaDay && codaPrice < c.BuyTierDays * codaDay + c.PriceRound,
                  "A TIER COSTS DAYS OF THE PURSE'S INCOME: " + c.BuyTierDays + " days is " + valorPrice + " Valor (a day is " + valorDay + ") or " + codaPrice + " Coda (a day is " + codaDay + ")");

            PassResult poor = WayfarersRoad.BuyTier(s, PassCurrency.Valor, now);
            long afterPoor = s.Profile.Valor;
            Check(!poor.Success && poor.Reason == PassRefusal.NotEnough && afterPoor == 0 && s.Profile.WayfarersRoad.Bought == 0,
                  "NOT ENOUGH: an empty purse buys nothing");

            s.Profile.Valor = valorPrice * 2;
            s.Profile.Coda = codaPrice * 2;
            s.Profile.Shards = 9; s.Profile.Gold = 8; s.Profile.Slag = 7; s.Profile.RelicDust = 6;
            PassResult v = WayfarersRoad.BuyTier(s, PassCurrency.Valor, now);
            Check(v.Success && s.Profile.Valor == valorPrice && s.Profile.Coda == codaPrice * 2 && s.Profile.WayfarersRoad.Bought == 1 && WayfarersRoad.Reached(s) == 1
                  && s.Profile.Shards == 9 && s.Profile.Gold == 8 && s.Profile.Slag == 7 && s.Profile.RelicDust == 6,
                  "buying with Valor takes exactly its price from Valor, raises the tier reached by one and moves no other purse");
            PassResult cd = WayfarersRoad.BuyTier(s, PassCurrency.Coda, now);
            Check(cd.Success && s.Profile.Coda == codaPrice && WayfarersRoad.Reached(s) == 2, "and with Coda the same");

            s.Profile.Valor = valorPrice * 10;
            WayfarersRoad.BuyTier(s, PassCurrency.Valor, now);
            string beforeCap = PassSnapshot(s);
            PassResult capped = WayfarersRoad.BuyTier(s, PassCurrency.Valor, now);
            PassResult cappedCoda = WayfarersRoad.BuyTier(s, PassCurrency.Coda, now);
            Check(s.Profile.WayfarersRoad.Bought == c.BuyTierMax && !capped.Success && capped.Reason == PassRefusal.CapReached && !cappedCoda.Success
                  && PassSnapshot(s) == beforeCap,
                  "AT MOST " + c.BuyTierMax + " A SEASON, across both purses: the fourth is refused as CapReached, the save untouched");

            // A bought tier is claimed like any other, and next season the allowance is back.
            PassResult claim = WayfarersRoad.Claim(s, 1, null, now);
            Check(claim.Success, "a bought tier's reward is claimed like any other");
            WayfarersRoad.Roll(s, PassSeasonStart(db, 6) + 600);
            Check(s.Profile.WayfarersRoad.Bought == 0 && WayfarersRoad.Reached(s) == 0, "and the next season starts with the allowance back and nothing reached");

            // Never past the top.
            GameSession top = PassSession(db, 0, valorPrice * 5, 0);
            WayfarersRoad.OnClaim(top, PassPointSource.Order, now);
            top.Profile.WayfarersRoad.Points = 30 * c.TierPoints;
            string beforeTop = PassSnapshot(top);
            PassResult full = WayfarersRoad.BuyTier(top, PassCurrency.Valor, now);
            Check(!full.Success && full.Reason == PassRefusal.Full && PassSnapshot(top) == beforeTop, "a finished track cannot be bought further: refused as Full, the save untouched");
        }

        // ------------------------------------------------------------------ 6. the season

        private static void CheckPassRollover(ContentDatabase db)
        {
            int hour = db.Balance.DailyResetHourUtc;
            long s5 = PassSeasonStart(db, 5), s6 = PassSeasonStart(db, 6), s7 = PassSeasonStart(db, 7);
            GameSession s = PassSession(db, 0, 0, 0);
            Check(WayfarersRoad.SeasonOf(s, s6 - 60) == 5 && WayfarersRoad.SeasonOf(s, s6 + 60) == 6 && WayfarersRoad.SeasonOf(s, s6) == 6
                  && s6 - s5 == 28 * 86400L,
                  "A SEASON TURNS AT THE REWARD DAY (" + hour.ToString("00") + ":00 UTC) and lasts exactly 28 days: a minute before is season 5, a minute after is season 6");
            Check(WayfarersRoad.SeasonOf(s, PassSeasonStart(db, 1) - 60) == 0,
                  "before the epoch there is no season");

            // Play, then cross the boundary.
            for (int d = 0; d < 6; d++) PassPlayDay(s, s5 + d * 86400L + 600);
            WayfarersRoad.Claim(s, 1, null, s5 + 6 * 86400L);
            int points5 = s.Profile.WayfarersRoad.Points;
            Check(s.Profile.WayfarersRoad.SeasonIndex == 5 && points5 > 0 && s.Profile.WayfarersRoad.HasClaimed(1),
                  "season 5 holds the points earned and the tier claimed (" + points5 + " points)");

            PassView lastDay = WayfarersRoad.View(s, s6 - 3600);
            Check(lastDay.DaysLeft == 1 && lastDay.ResetInSeconds == 3600,
                  "on the last day the view says 1 day left and the seconds to the turn");
            PassPlayDay(s, s6 + 600);
            Check(s.Profile.WayfarersRoad.SeasonIndex == 6 && s.Profile.WayfarersRoad.Points == 5 * db.WayfarersRoad.OrderPoints + db.WayfarersRoad.DailyChestPoints
                  && s.Profile.WayfarersRoad.ClaimedMask == 0 && s.Profile.WayfarersRoad.Bought == 0,
                  "ACROSS THE BOUNDARY the pass starts season 6 empty: the first day's " + s.Profile.WayfarersRoad.Points + " points, nothing claimed, unclaimed tiers lapsed");

            // A clock wound back: nothing is reopened.
            string wound = PassSnapshot(s);
            bool rolled = WayfarersRoad.Roll(s, s5 + 3 * 86400L);
            PassResult windClaim = WayfarersRoad.Claim(s, 20, null, s5 + 3 * 86400L);
            Check(!rolled && PassSnapshot(s) == wound && windClaim.Reason == PassRefusal.NotReached && s.Profile.WayfarersRoad.SeasonIndex == 6,
                  "A CLOCK WOUND BACK to season 5 rolls nothing and reopens nothing: season 6 stands, and season 5's lapsed tiers do not come back");
            // Wound-back play counts toward the standing season (the loop's own stamps refuse a repeat claim).
            int again = PassPlayDay(s, s5 + 4 * 86400L + 600);
            Check(again == 0, "and the loop's own stamps stop a wound-back day from paying twice (" + again + " points)");

            // A clock jumped forward many seasons lands in the season it names and pays nothing for the ones it skipped.
            GameSession jump = PassSession(db, 0, 0, 0);
            PassPlayDay(jump, s5 + 600);
            long far = PassSeasonStart(db, 40) + 5 * 3600;
            WayfarersRoad.Roll(jump, far);
            Check(jump.Profile.WayfarersRoad.SeasonIndex == 40 && jump.Profile.WayfarersRoad.Points == 0 && jump.Profile.WayfarersRoad.ClaimedMask == 0,
                  "a clock jumped forward 35 seasons lands in season 40, empty: the skipped seasons pay nothing");
            // And back to the same season: a stable season.
            bool again40 = WayfarersRoad.Roll(jump, far + 86400L * 3);
            Check(!again40, "and later in the same season nothing rolls again");

            // The turn is on the reward-day clock, not the midnight: a minute before the reset hour is still the old season.
            Check(WayfarersRoad.SeasonOf(s, s7 - 3600 * (hour + 1) + 3599) >= 6, "the season follows the reward day, the clock the daily loop uses");
        }

        // ------------------------------------------------------------------ 7. the save

        private static void CheckPassSave(ContentDatabase db)
        {
            long now = PassSeasonStart(db, 5) + 600;
            string error;
            GameSession fresh = PassSession(db, 0, 0, 0);
            string freshJson = PassSnapshot(fresh);
            Check(JsonValue.Parse(freshJson).GetInt("version", 0) == SaveSerializer.BaseVersion && !freshJson.Contains("wayfarersRoad"),
                  "A PROFILE THAT NEVER EARNED A POINT carries no wayfarersRoad key and says version " + SaveSerializer.BaseVersion + ": the document the build before wrote");

            GameSession s = PassSession(db, 0, 0, 0);
            for (int d = 0; d < 9; d++) PassPlayDay(s, now + d * 86400L);
            WayfarersRoad.Claim(s, 1, null, now + 9 * 86400L);
            WayfarersRoad.Claim(s, 2, null, now + 9 * 86400L);
            string json = PassSnapshot(s);
            PlayerProfile back = SaveSerializer.Deserialize(json, db, out error);
            Check(back != null && error == null && SaveSerializer.Serialize(back) == json
                  && back.WayfarersRoad.Points == s.Profile.WayfarersRoad.Points && back.WayfarersRoad.HasClaimed(1) && back.WayfarersRoad.HasClaimed(2) && !back.WayfarersRoad.HasClaimed(3),
                  "SAVE ROUND TRIP: a season with points and two tiers claimed saves, loads back identical and saves again byte for byte");
            Check(JsonValue.Parse(json).GetInt("version", 0) == 24 && SaveSerializer.VersionOfJson(json) == 24 && json.Contains("\"wayfarersRoad\""),
                  "and the document claims version 24, the version that holds the pass");

            string[] damaged = { "\"wayfarersRoad\": \"x\"", "\"wayfarersRoad\": 7", "\"wayfarersRoad\": []", "\"wayfarersRoad\": { \"season\": \"x\", \"points\": [], \"claimed\": { } }" };
            int fine = 0;
            for (int i = 0; i < damaged.Length; i++)
            {
                string doc = freshJson.TrimEnd();
                int last = doc.LastIndexOf('}');
                string withBlock = doc.Substring(0, last).TrimEnd() + ",\n  " + damaged[i] + "\n}";
                PlayerProfile p = SaveSerializer.Deserialize(withBlock, db, out error);
                if (p != null && error == null && p.WayfarersRoad.IsEmpty) fine++;
            }
            Check(fine == damaged.Length, "damaged blocks read as empty and the save still loads (" + fine + " of " + damaged.Length + ")");

            JsonValue block = JsonValue.NewObject();
            block.Set("season", -4L);
            block.Set("points", 99999999);
            block.Set("claimed", 4294967295L);
            block.Set("bought", 99);
            WayfarersRoadState clamped = WayfarersRoadState.Read(block);
            Check(clamped.SeasonIndex == 0 && clamped.Points == 100000 && clamped.ClaimedMask == (1 << 30) - 1 && clamped.Bought == 30,
                  "and the numbers are clamped to what the game could have written");
        }

        // ------------------------------------------------------------------ 8. not hashed

        private static void CheckPassNotHashed(ContentDatabase db, string contentDir)
        {
            string[] hashed = { "heroes", "enemies", "campaign", "balance", "relics", "events", "armor", "floor-conditions" };
            string[] texts = new string[hashed.Length];
            for (int i = 0; i < hashed.Length; i++)
            {
                string p = Path.Combine(contentDir, hashed[i] + ".json");
                texts[i] = File.Exists(p) ? File.ReadAllText(p) : null;
            }
            Check(ContentDatabase.VersionOf(texts) == db.ContentVersion,
                  "WAYFARERS-ROAD.JSON IS NOT HASHED: ContentVersion is exactly the hash of the eight fight files (" + db.ContentVersion + "), and wayfarers-road.json is not among them");

            ContentDatabase other = LoadContent(contentDir);
            string version = other.ContentVersion;
            other.LoadWayfarersRoad(File.ReadAllText(Path.Combine(contentDir, "wayfarers-road.json")).Replace("\"tierPoints\": 46", "\"tierPoints\": 50"));
            Check(other.WayfarersRoad.TierPoints == 50 && other.ContentVersion == version && version == db.ContentVersion,
                  "so retuning the pass moves the pass (a tier is 50 points) and not ContentVersion, which keeps every recorded guild dungeon replay");
            other.LoadWayfarersRoad(null);
            Check(!other.WayfarersRoad.IsUsable && !WayfarersRoad.Active(PassSession(other, 0, 0, 0)), "and removing the file closes the pass");
        }

        // ------------------------------------------------------------------ 9. the paid track

        private static void CheckPassPaidTrack(ContentDatabase db)
        {
            long now = PassSeasonStart(db, 5) + 600;
            GameSession s = PassSession(db, 0, 0, 0);
            WayfarersRoad.OnClaim(s, PassPointSource.Order, now);
            s.Profile.WayfarersRoad.Points = 30 * db.WayfarersRoad.TierPoints;

            Check(!PassPaidSwitch.Enabled && WayfarersRoad.View(s, now).Paid.Count == 0,
                  "THE PAID TRACK IS OFF: EMBERFALL_PASS_PAID is not defined, and with it off the view carries no paid row");
            PassPaidSwitch.Enabled = true;
            PassView on = WayfarersRoad.View(s, now);
            Check(on.Paid.Count == db.WayfarersRoad.Paid.Count && on.Paid.Count > 0, "with the define on the paid row is on the view (" + on.Paid.Count + " rewards), as a locked row");

            string before = PassSnapshot(s);
            bool allRefused = true;
            for (int t = 0; t <= 31; t++)
            {
                PassResult r = WayfarersRoad.ClaimPaid(s, t);
                if (r.Success || r.Reason != PassRefusal.PaidLocked) allRefused = false;
            }
            PassPaidSwitch.Enabled = false;
            for (int t = 0; t <= 31; t++)
            {
                PassResult r = WayfarersRoad.ClaimPaid(s, t);
                if (r.Success || r.Reason != PassRefusal.PaidLocked) allRefused = false;
            }
            Check(allRefused && PassSnapshot(s) == before,
                  "NO PURCHASE PATH: every paid claim, with the define on or off, is refused as PaidLocked and the save is byte for byte as it was");

            // Only the Store (R19) can write an unlock: the state's two paid fields, and ClaimPaid reads them only with StoreSwitch on.
            FieldInfo[] fields = typeof(WayfarersRoadState).GetFields(BindingFlags.Public | BindingFlags.Instance);
            List<string> names = new List<string>();
            for (int i = 0; i < fields.Length; i++) names.Add(fields[i].Name);
            names.Sort();
            Check(string.Join(",", names.ToArray()) == "Bought,ClaimedMask,PaidClaimedMask,PaidSeason,Points,SeasonIndex",
                  "and the only fields that record an unlock are PaidSeason and PaidClaimedMask (" + string.Join(", ", names.ToArray()) + "), written by a purchase alone (CheckStorePass)");
        }

        // ------------------------------------------------------------------ 10. rollback

        private static void CheckPassOff(ContentDatabase db)
        {
            long now = PassSeasonStart(db, 5) + 600;
            bool was = WayfarersRoadSwitch.Enabled;
            try
            {
                WayfarersRoadSwitch.Enabled = false;
                GameSession off = PassSession(db, 0, 5000, 5000);
                for (int d = 0; d < 3; d++) PassPlayDay(off, now + d * 86400L);
                string offJson = PassSnapshot(off);
                PassView view = WayfarersRoad.View(off, now);
                PassResult claim = WayfarersRoad.Claim(off, 1, null, now), buy = WayfarersRoad.BuyTier(off, PassCurrency.Valor, now);
                Check(!WayfarersRoad.Active(off) && !view.Active && claim.Reason == PassRefusal.Off && buy.Reason == PassRefusal.Off
                      && off.Profile.WayfarersRoad.IsEmpty && PassSnapshot(off) == offJson && off.Profile.Valor == 5000,
                      "ROLLBACK: with the switch off no point is counted, the view is empty, every call is refused as Off, nothing is charged and nothing is recorded");

                JsonValue root = JsonValue.Parse(offJson);
                List<string> keys = new List<string>();
                foreach (string key in root.Keys) keys.Add(key);
                Check(!keys.Contains("wayfarersRoad") && root.GetInt("version", 0) == SaveSerializer.BaseVersion,
                      "and the save is the document the build before wrote: version " + root.GetInt("version", 0) + ", no wayfarersRoad key");

                // The same play with the road on is the off save plus the one key and version 24, byte for byte.
                WayfarersRoadSwitch.Enabled = true;
                GameSession on = PassSession(db, 0, 5000, 5000);
                for (int d = 0; d < 3; d++) PassPlayDay(on, now + d * 86400L);
                string onJson = PassSnapshot(on);
                JsonValue onRoot = JsonValue.Parse(onJson);
                JsonValue rebuilt = JsonValue.NewObject();
                foreach (string key in onRoot.Keys)
                {
                    if (key == "wayfarersRoad") continue;
                    if (key == "version") { rebuilt.Set(key, root.GetInt("version", 0)); continue; }
                    rebuilt.Set(key, onRoot[key]);
                }
                Check(onRoot.GetInt("version", 0) == 24 && rebuilt.ToJson(true) == offJson,
                      "THE OFF SAVE IS MAIN'S DOCUMENT: the same three days with the road on are the off save plus the wayfarersRoad key and version 24, byte for byte");

                // A season on file is kept untouched with the switch off.
                WayfarersRoadSwitch.Enabled = false;
                string error;
                PlayerProfile kept = SaveSerializer.Deserialize(onJson, db, out error);
                Check(kept != null && SaveSerializer.Serialize(kept) == onJson,
                      "with the switch off a saved season is still read and written back untouched: turning it off costs a player nothing");
            }
            finally { WayfarersRoadSwitch.Enabled = was; }
        }

        // ------------------------------------------------------------------ 11. wiring

        private static void CheckPassWiring(string contentDir)
        {
            string root = Path.GetFullPath(Path.Combine(contentDir, "..", "..", ".."));
            string ui = Path.Combine(root, "Assets", "Scripts", "UI");
            string routerPath = Path.Combine(ui, "ScreenRouter.cs");
            string ordersPath = Path.Combine(ui, "Screens", "OrdersScreen.cs");
            string screenPath = Path.Combine(ui, "Screens", "WayfarersRoadScreen.cs");
            string loopPath = Path.Combine(root, "Assets", "Scripts", "Core", "Progression", "DailyLoop.cs");
            string[] files = { routerPath, ordersPath, screenPath, loopPath };
            for (int i = 0; i < files.Length; i++)
                if (!File.Exists(files[i])) { Check(false, "the pass screen, router, Orders page and loop are where the harness expects them (" + files[i] + ")"); return; }
            string router = File.ReadAllText(routerPath), orders = File.ReadAllText(ordersPath), screen = File.ReadAllText(screenPath), loop = File.ReadAllText(loopPath);

            Check(router.Contains("if (Emberfall.Core.Session.WayfarersRoadSwitch.Enabled) WayfarersRoad = Create<Screens.WayfarersRoadScreen>();") && CountOf(router, "Create<Screens.WayfarersRoadScreen>()") == 1,
                  "WIRING (reads source): the router builds the pass once, and only inside the switch");
            Check(orders.Contains("if (WayfarersRoadSwitch.Enabled) BuildRoadDoor();") && CountOf(orders, "ScreenId.WayfarersRoad") == 1,
                  "and the Orders page draws its one door to it only inside the switch");

            bool decides = screen.Contains("Profile.Valor") || screen.Contains("Profile.Coda") || screen.Contains("Profile.Gold") || screen.Contains("Profile.WayfarersRoad")
                        || screen.Contains("ClaimedMask") || screen.Contains(".Points +=") || screen.Contains("Random");
            Check(!decides && screen.Contains("WayfarersRoad.View(") && screen.Contains("WayfarersRoad.Claim(") && screen.Contains("WayfarersRoad.BuyTier(") && !screen.Contains("ClaimPaid"),
                  "WIRING (reads source): the screen reads WayfarersRoad.View, claims and buys through WayfarersRoad only, writes no purse and has no paid button");

            // The only road to a point is a loop claim.
            int inLoop = CountOf(loop, "WayfarersRoad.OnClaim(");
            List<string> elsewhere = new List<string>();
            string[] all = Directory.GetFiles(Path.Combine(root, "Assets"), "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < all.Length; i++)
            {
                string name = Path.GetFileName(all[i]);
                if (name == "DailyLoop.cs" || name == "WayfarersRoad.cs") continue;
                if (all[i].Contains(Path.DirectorySeparatorChar + "Editor" + Path.DirectorySeparatorChar)) continue;   // the capture tool seeds a scratch profile through the rules
                if (File.ReadAllText(all[i]).Contains("WayfarersRoad.OnClaim(")) elsewhere.Add(name);
            }
            Check(inLoop == 3 && elsewhere.Count == 0,
                  "POINTS ONLY FROM THE LOOP: WayfarersRoad.OnClaim is called three times, from the order, daily chest and weekly chest claims in DailyLoop, and nowhere else in the game (the editor's capture seed aside)"
                  + (elsewhere.Count > 0 ? " (" + string.Join(", ", elsewhere.ToArray()) + ")" : ""));
        }
    }
}
