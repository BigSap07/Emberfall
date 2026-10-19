using System;
using System.Collections.Generic;
using System.IO;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Json;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Save;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE RELIC ENDGAME (Docs/DESIGN-RELIC-ENDGAME.md): relics as endgame purchases and the upgrade
    /// path that is the long tail of the Dust sink.
    ///
    /// One named check per rule: the relic list is data (a relic added to relics.json is on the shelf,
    /// buyable and fights, with no code), the Dust a committed player earns and the weeks it takes to
    /// buy every relic, the upgrade prices (each tier about double) and what a tier is worth in a
    /// fight, every refusal leaving the save untouched, the save, relic-shop.json staying out of
    /// ContentVersion, combat not moving for a tier 1 relic, the rollback, and the wiring.
    ///
    /// RelicDustStudy is the measurement the prices were sized from, printed so it can be re-read.
    /// </summary>
    internal static partial class Program
    {
        // ================================================================== the study

        private const int CommittedSweeps = 12;
        private const int CasualSweeps = 4;

        /// <summary>Grants a relic and puts it on every hero standing in the line. One owned relic is worn by
        /// one hero at a time, so the last wearer keeps it; the fights compare like for like.</summary>
        private static void RelicEquipTeam(GameSession s, string relicId)
        {
            s.GrantRelic(relicId);
            for (int t = 0; t < s.Profile.Team.Length; t++)
            {
                HeroInstance h = s.Profile.FindHeroByUid(s.Profile.Team[t]);
                if (h != null) s.AssignRelic(h, relicId);
            }
        }

        private static StageDef RelicStageByCode(ContentDatabase db, string code)
        {
            for (int i = 0; i < db.AllStages.Count; i++) if (db.AllStages[i].DisplayCode == code) return db.AllStages[i];
            return null;
        }

        /// <summary>Relic Dust a day from sweeping the topN highest cleared stages as many times as the
        /// repeat cap pays: the repeat-clear loop (GameSession.RelicDustForRepeatClear), which is the
        /// early-game source and the one that scales with the frontier.</summary>
        private static long RelicDustPerDay(ContentDatabase db, GameSession s, int frontierIndex, int topN)
        {
            long total = 0;
            int taken = 0;
            for (int i = frontierIndex; i >= 0 && taken < topN; i--, taken++)
                total += (long)s.RelicDustForRepeatClear(db.AllStages[i]) * s.RepeatFullClearsPerDay;
            return total;
        }

        private static long RelicCatalogue(ContentDatabase db, GameSession s)
        {
            long total = 0;
            for (int i = 0; i < db.Relics.Count; i++) total += s.RelicPrice(db.Relics[i]);
            return total;
        }

        private static long RelicCatalogueAtTop(ContentDatabase db, GameSession s)
        {
            long total = 0;
            for (int i = 0; i < db.Relics.Count; i++)
            {
                total += s.RelicPrice(db.Relics[i]);
                for (int t = 2; t <= s.RelicMaxTier; t++) total += s.RelicUpgradePrice(db.Relics[i], t);
            }
            return total;
        }

        private static string RelicDays(long cost, long perDay)
        {
            return perDay > 0 ? Fmt(cost / (float)perDay) : "n/a";
        }

        /// <summary>The Dust a player earns a day at a frontier and the days it takes to buy every relic
        /// content names (and then to raise them all to the top tier). --check RelicDustStudy.</summary>
        private static void RelicDustStudy(ContentDatabase db)
        {
            Line("RELIC DUST (the Dust a day the repeat-clear loop pays, and the days to buy every relic at the shipped prices)");
            Line("  A committed player sweeps their " + CommittedSweeps + " highest cleared stages " + 3 + "x a day (" + (CommittedSweeps * 3)
                 + " taps), a casual one their " + CasualSweeps + "; the ceiling sweeps every cleared stage. Prices from relic-shop.json.");
            string[] frontiers = { "3-1", "3-12", "4-12", "5-12", "6-12" };
            Line("  frontier  topStage  committed/day  casual/day  ceiling/day  buyAll(days)  casual  ceiling  maxAll(days)  casual");
            for (int f = 0; f < frontiers.Length; f++)
            {
                StageDef stage = RelicStageByCode(db, frontiers[f]);
                if (stage == null) continue;
                GameSession s = QmSession(db, stage.GlobalIndex + 1, 0);
                long committed = RelicDustPerDay(db, s, stage.GlobalIndex, CommittedSweeps);
                long casual = RelicDustPerDay(db, s, stage.GlobalIndex, CasualSweeps);
                long ceiling = RelicDustPerDay(db, s, stage.GlobalIndex, int.MaxValue);
                long all = RelicCatalogue(db, s), max = RelicCatalogueAtTop(db, s);
                Line("  " + frontiers[f].PadRight(8)
                     + (s.RelicDustForRepeatClear(stage) + " x3").PadLeft(10)
                     + committed.ToString().PadLeft(15)
                     + casual.ToString().PadLeft(12)
                     + ceiling.ToString().PadLeft(13)
                     + RelicDays(all, committed).PadLeft(14)
                     + RelicDays(all, casual).PadLeft(8)
                     + RelicDays(all, ceiling).PadLeft(9)
                     + RelicDays(max, committed).PadLeft(14)
                     + RelicDays(max, casual).PadLeft(8));
            }
            GameSession any = QmSession(db, db.AllStages.Count, 0);
            Line("  relics " + db.Relics.Count + ", buying every one costs " + RelicCatalogue(db, any) + " Dust, and raising every one to tier "
                 + any.RelicMaxTier + " costs " + RelicCatalogueAtTop(db, any) + " in all.");
            Line("");
        }

        // ================================================================== the checks

        private static void CheckRelicEndgame(ContentDatabase db, string contentDir)
        {
            Line("");
            Line("RELIC ENDGAME (Docs/DESIGN-RELIC-ENDGAME.md: relics priced as endgame purchases, and tiers as the long tail of the Dust sink)");
            bool was = RelicEndgameSwitch.Enabled;
            try
            {
                RelicEndgameSwitch.Enabled = true;
                CheckRelicContentFile(db);
                CheckRelicShopIsDataDriven(db, contentDir);
                CheckRelicPricing(db);
                CheckRelicUpgrades(db, contentDir);
                CheckRelicUpgradeRefusals(db);
                CheckRelicTierInAFight(db, contentDir);
                CheckRelicEndgameSave(db);
                CheckRelicShopNotHashed(db, contentDir);
                CheckRelicEndgameOff(db);
                CheckRelicEndgameWiring(contentDir);
            }
            finally { RelicEndgameSwitch.Enabled = was; }
            Check(RelicEndgameSwitch.Enabled == was, "the switch is put back as the run found it (relic endgame " + (was ? "on" : "off") + ")");
        }

        // ------------------------------------------------------------------ 1. the file

        private static void CheckRelicContentFile(ContentDatabase db)
        {
            RelicShopConfig c = db.RelicShop;
            Check(c.IsUsable && c.Warnings.Count == 0 && c.Tiers.Count == 2 && c.MaxTier == 3,
                  "the shipped relic-shop.json loads clean: Legendary " + c.LegendaryPrice + ", Mythic " + c.MythicPrice + ", " + c.Tiers.Count + " upgrade tiers (max tier " + c.MaxTier + ")");
            Check(c.Tiers[0].PriceMultiplier >= 1.8 && c.Tiers[0].PriceMultiplier <= 2.2 && c.Tiers[1].PriceMultiplier >= c.Tiers[0].PriceMultiplier * 1.8
                  && c.Tiers[1].PriceMultiplier <= c.Tiers[0].PriceMultiplier * 2.2,
                  "each tier costs about double the one before (x" + c.Tiers[0].PriceMultiplier + " then x" + c.Tiers[1].PriceMultiplier + " of the relic's own price)");
            Check(c.Tiers[0].StatScale > 1.0 && c.Tiers[1].StatScale > c.Tiers[0].StatScale,
                  "and each tier is worth more in a fight (stat block x" + c.Tiers[0].StatScale + " then x" + c.Tiers[1].StatScale + "), tier 1 being the relic as shipped");

            RelicShopConfig bad = RelicShopConfig.FromJson("{ \"prices\": { \"Legendary\": 100, \"Mythic\": 900 }, \"overrides\": { \"a\": 0, \"b\": 50 },"
                                                           + " \"upgrades\": { \"tiers\": [ { \"tier\": 2, \"priceMultiplier\": 2, \"statScale\": 1.2 }, { \"tier\": 3, \"priceMultiplier\": 1, \"statScale\": 1.4 } ] } }");
            Check(bad.IsUsable && bad.Tiers.Count == 1 && bad.Overrides.Count == 1 && bad.Warnings.Count == 2,
                  "a zero override and a tier that costs no more than the one before are dropped, each with a warning (" + bad.Warnings.Count + ")");
            Check(!RelicShopConfig.FromJson(null).IsUsable && !RelicShopConfig.FromJson("{ \"prices\": { \"Legendary\": 5 } }").IsUsable,
                  "a missing file, or one with only one grade priced, is not usable: the shop falls back to balance.json");
        }

        // ------------------------------------------------------------------ 2. data driven

        private static void CheckRelicShopIsDataDriven(ContentDatabase db, string contentDir)
        {
            string path = Path.Combine(contentDir, "relics.json");
            string text = File.ReadAllText(path);
            const string testRelic = "{ \"id\": \"test_relic_prism\", \"name\": \"Test Prism\", \"grade\": \"Legendary\", \"stats\": ["
                                   + " { \"stat\": \"Attack\", \"mode\": \"Multiply\", \"value\": 0.20 }, { \"stat\": \"CritChance\", \"mode\": \"Add\", \"value\": 0.05 } ] },";
            int at = text.IndexOf("\"relics\": [", StringComparison.Ordinal);
            if (at < 0) { Check(false, "relics.json has a relics array the data-driven check can add to"); return; }
            string withTest = text.Insert(at + "\"relics\": [".Length, "\n" + testRelic);

            ContentDatabase db2 = LoadContent(contentDir, null, null, withTest);
            Check(db2.Relics.Count == db.Relics.Count + 1 && db2.GetRelic("test_relic_prism") != null,
                  "A RELIC ADDED IN DATA: a test relic put in relics.json is loaded (" + db.Relics.Count + " relics, then " + db2.Relics.Count + ")");

            GameSession s = QmSession(db2, 40, 0);
            List<RelicCardView> shelf = RelicShop.View(s);
            RelicCardView card = null;
            for (int i = 0; i < shelf.Count; i++) if (shelf[i].Relic.Id == "test_relic_prism") card = shelf[i];
            Check(card != null && shelf.Count == db2.Relics.Count && !card.Owned && card.Price == db2.RelicShop.LegendaryPrice,
                  "it appears in the shop, at its grade's price (" + (card != null ? card.Price : 0) + "), with no entry in relic-shop.json and no code");

            s.Profile.RelicDust = card != null ? card.Price - 1 : 0;
            string before = SaveSerializer.Serialize(s.Profile);
            bool tooEarly = RelicShop.Buy(s, "test_relic_prism", SinkBase);
            Check(!tooEarly && SaveSerializer.Serialize(s.Profile) == before, "and cannot be bought one Dust short, the save untouched");
            s.Profile.RelicDust += 1;
            bool bought = RelicShop.Buy(s, "test_relic_prism", SinkBase);
            Check(bought && s.Profile.OwnsRelic("test_relic_prism") && s.Profile.RelicDust == 0 && s.Profile.RelicTier("test_relic_prism") == 1,
                  "and can be bought: it is owned at tier 1 and exactly its price came out of the Dust purse");

            // It works: the stat line and a fight.
            HeroInstance hero = s.Profile.Heroes[0];
            UnitStats bare = StatMath.ForHero(hero.Def, hero.Level);
            s.AssignRelic(hero, "test_relic_prism");
            UnitStats dressed = StatMath.ForHero(hero.Def, hero.Level, s.Profile.EquippedRelicOf(hero, db2));
            Check(dressed.Attack > bare.Attack && Math.Abs(dressed.Attack - (float)Math.Round(bare.Attack * 1.20f)) < 1.01f
                  && Math.Abs(dressed.CritChance - (bare.CritChance + 0.05f)) < 0.0001f,
                  "and it works: equipped, the hero's Attack is x1.20 (" + Fmt(bare.Attack) + " to " + Fmt(dressed.Attack) + ") and Crit Chance +5 points, exactly the data's numbers");
            GameSession bareSession = new GameSession(db2, GameSession.CreateNewProfile(db2, SinkBase));
            GameSession dressedSession = new GameSession(db2, GameSession.CreateNewProfile(db2, SinkBase));
            RelicEquipTeam(dressedSession, "test_relic_prism");
            StageDef stage = db2.AllStages[3];
            string a = FingerprintOf(CombatSimulation.Run(bareSession.BuildBattleRequest(CampaignWaveGenerator.BuildBattle(stage, 1, 1), 99)));
            string b = FingerprintOf(CombatSimulation.Run(dressedSession.BuildBattleRequest(CampaignWaveGenerator.BuildBattle(stage, 1, 1), 99)));
            Check(a != b, "and a fight with it equipped is a different fight from the same fight without it");

            // The rule that makes it data: no shipped relic id is named in game code.
            string root = Path.GetFullPath(Path.Combine(contentDir, "..", "..", ".."));
            List<string> hits = new List<string>();
            string[] files = Directory.GetFiles(Path.Combine(root, "Assets", "Scripts"), "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string src = File.ReadAllText(files[i]);
                for (int r = 0; r < db.Relics.Count; r++)
                    if (src.Contains("\"" + db.Relics[r].Id + "\"")) hits.Add(Path.GetFileName(files[i]) + ":" + db.Relics[r].Id);
            }
            Check(hits.Count == 0,
                  "NO RELIC IS NAMED IN GAME CODE: none of the " + db.Relics.Count + " shipped relic ids appears as a string anywhere under Assets/Scripts"
                  + (hits.Count > 0 ? " (" + string.Join(", ", hits.ToArray()) + ")" : ""));

            // A price override is data too.
            ContentDatabase db3 = LoadContent(contentDir, null, null, withTest);
            db3.LoadRelicShop(File.ReadAllText(Path.Combine(contentDir, "relic-shop.json")).Replace("\"overrides\": { }", "\"overrides\": { \"test_relic_prism\": 777 }"));
            GameSession s3 = QmSession(db3, 40, 0);
            Check(s3.RelicPrice(db3.GetRelic("test_relic_prism")) == 777 && s3.RelicPrice(db3.Relics[1]) == db3.RelicShop.LegendaryPrice,
                  "and one relic can be priced on its own in relic-shop.json without moving the others");
        }

        // ------------------------------------------------------------------ 3. pricing

        private static void CheckRelicPricing(ContentDatabase db)
        {
            RelicDustStudy(db);

            string[] frontiers = { "3-1", "4-12", "6-12" };
            bool weeks = true, tail = true, income = true;
            string detail = "";
            for (int f = 0; f < frontiers.Length; f++)
            {
                StageDef stage = RelicStageByCode(db, frontiers[f]);
                GameSession s = QmSession(db, stage.GlobalIndex + 1, 0);
                long committed = RelicDustPerDay(db, s, stage.GlobalIndex, CommittedSweeps);
                long all = RelicCatalogue(db, s);
                double days = committed > 0 ? all / (double)committed : double.MaxValue;
                double min = frontiers[f] == "3-1" ? 35.0 : frontiers[f] == "4-12" ? 18.0 : 12.0;
                if (committed <= 0) income = false;
                if (days < min) weeks = false;
                if (RelicCatalogueAtTop(db, s) < all * 4) tail = false;
                detail += " " + frontiers[f] + " " + Fmt((float)days) + "d";
            }
            Check(income, "Dust never runs dry: a committed player earns Dust a day at every frontier from 3-1 on");
            Check(weeks, "BUYING EVERY CURRENT RELIC TAKES A COMMITTED PLAYER WEEKS, NOT DAYS (" + detail.Trim() + "; at least 5 weeks from 3-1, 2.5 from 4-12, 12 days at the end)");
            Check(tail, "and the upgrades are the long tail: raising every relic to the top tier costs at least four times buying them");

            // The model is the real payout path: sweep the top stages through the game's own grant.
            StageDef at = RelicStageByCode(db, "4-12");
            GameSession real = QmSession(db, at.GlobalIndex + 1, 0);
            long now = SinkUnix(2025, 10, 9, 8);
            real.RollDailyReset(now);
            long before = real.Profile.RelicDust;
            for (int i = at.GlobalIndex; i > at.GlobalIndex - CommittedSweeps; i--)
                for (int k = 0; k < real.RepeatFullClearsPerDay; k++) real.GrantBattleResult(db.AllStages[i], true, now);
            Check(real.Profile.RelicDust - before == RelicDustPerDay(db, real, at.GlobalIndex, CommittedSweeps),
                  "THE MODEL IS THE PAYOUT: sweeping the top " + CommittedSweeps + " stages through GrantBattleResult pays exactly the Dust the study counts ("
                  + (real.Profile.RelicDust - before) + ")");
            long dustNow = real.Profile.RelicDust;
            real.GrantBattleResult(db.AllStages[at.GlobalIndex], true, now);
            Check(real.Profile.RelicDust == dustNow, "and a clear past the daily repeat cap pays no Dust: the income is capped by the day, not by the player's patience");

            // A mythic costs a long goal, not a quick buy.
            RelicDef legendary = null, mythic = null;
            for (int i = 0; i < db.Relics.Count; i++)
            {
                if (db.Relics[i].Grade == RelicGrade.Legendary && legendary == null) legendary = db.Relics[i];
                if (db.Relics[i].Grade == RelicGrade.Mythic && mythic == null) mythic = db.Relics[i];
            }
            GameSession g = QmSession(db, 25, 0);
            Check(legendary != null && mythic != null && g.RelicPrice(mythic) >= g.RelicPrice(legendary) * 3,
                  "a mythic relic costs at least 3x a legendary (" + g.RelicPrice(legendary) + ", " + g.RelicPrice(mythic) + "), so it reads as a goal");
        }

        // ------------------------------------------------------------------ 4. upgrades

        private static void CheckRelicUpgrades(ContentDatabase db, string contentDir)
        {
            GameSession s = QmSession(db, 40, 0);
            RelicDef relic = db.Relics[0];
            s.GrantRelic(relic.Id);

            int p1 = s.RelicPrice(relic), p2 = s.RelicUpgradePrice(relic, 2), p3 = s.RelicUpgradePrice(relic, 3);
            Check(s.RelicMaxTier == 3 && p2 > p1 && p3 > p2 && s.RelicUpgradePrice(relic, 1) == 0 && s.RelicUpgradePrice(relic, 4) == 0,
                  "UPGRADE PRICES CLIMB: buying costs " + p1 + ", tier 2 costs " + p2 + " and tier 3 costs " + p3 + " Dust; there is no price for tier 1 or for a tier past the top");
            Check(p2 == p1 * 2 && p3 == p1 * 4, "each tier is about double the one before (x2 then x4 of the relic's own price)");

            s.Profile.RelicDust = p2 + p3 + 5;
            long dustStart = s.Profile.RelicDust;
            bool up2 = s.UpgradeRelic(relic.Id, SinkBase);
            Check(up2 && s.Profile.RelicTier(relic.Id) == 2 && s.Profile.RelicDust == dustStart - p2,
                  "an upgrade takes exactly its price and raises the tier by one (tier " + s.Profile.RelicTier(relic.Id) + ", " + s.Profile.RelicDust + " Dust left)");
            bool up3 = s.UpgradeRelic(relic.Id, SinkBase);
            Check(up3 && s.Profile.RelicTier(relic.Id) == 3 && s.Profile.RelicDust == 5, "and the next tier takes its own: tier 3, 5 Dust left");
            s.Profile.RelicDust = 10 * p3;
            bool up4 = s.UpgradeRelic(relic.Id, SinkBase);
            Check(!up4 && s.Profile.RelicTier(relic.Id) == 3 && s.Profile.RelicDust == 10 * p3,
                  "a relic at the top tier cannot be raised, and nothing is charged");
            string sessionSource = File.ReadAllText(Path.Combine(SealedRepoRoot(contentDir) ?? "", "Assets", "Scripts", "Core", "Session", "GameSession.cs"));
            int upgradeAt = sessionSource.IndexOf("public bool UpgradeRelic(", StringComparison.Ordinal);
            int upgradeEnd = upgradeAt < 0 ? -1 : sessionSource.IndexOf("\n        }", upgradeAt, StringComparison.Ordinal);
            string upgradeBody = upgradeAt >= 0 && upgradeEnd > upgradeAt ? sessionSource.Substring(upgradeAt, upgradeEnd - upgradeAt) : "";
            Check(upgradeBody.Contains("SpendRelicDust(") && !upgradeBody.Contains("RelicDust -="),
                  "and the Dust goes through the one choke point every Dust expenditure uses (SpendRelicDust), so RelicDustSpent events see it");

            // What a tier is worth: the stat block scaled, exactly, hook untouched.
            HeroInstance hero = s.Profile.Heroes[0];
            s.AssignRelic(hero, relic.Id);
            RelicDef at3 = s.Profile.EquippedRelicOf(hero, db);
            bool scaled = at3 != relic && at3.Stats.Count == relic.Stats.Count && at3.Id == relic.Id && at3.Grade == relic.Grade;
            for (int i = 0; scaled && i < relic.Stats.Count; i++)
                scaled = Math.Abs(at3.Stats[i].Value - relic.Stats[i].Value * (float)db.RelicShop.StatScaleOf(3)) < 0.00001f && at3.Stats[i].Stat == relic.Stats[i].Stat;
            Check(scaled && !ReferenceEquals(db.GetRelic(relic.Id).Stats[0], at3.Stats[0]),
                  "TIER 3 SCALES THE STAT BLOCK by x" + db.RelicShop.StatScaleOf(3) + " (" + relic.DescribeStats() + " becomes " + at3.DescribeStats() + ") and never mutates the content's own relic");
            Check(db.GetRelic(relic.Id).DescribeStats() == relic.DescribeStats() && db.GetRelic(relic.Id).Stats[0].Value == relic.Stats[0].Value,
                  "relics.json's own numbers are untouched: tier 1 is the relic as shipped");

            RelicDef mythic = null;
            for (int i = 0; i < db.Relics.Count; i++) if (db.Relics[i].Grade == RelicGrade.Mythic) { mythic = db.Relics[i]; break; }
            if (mythic != null)
            {
                s.GrantRelic(mythic.Id);
                s.Profile.FindOwnedRelic(mythic.Id).Tier = 3;
                RelicDef m3 = s.Profile.RelicAtOwnedTier(mythic.Id, db);
                Check(m3.Hook == mythic.Hook && m3.Rule == mythic.Rule && m3.Grade == RelicGrade.Mythic,
                      "a mythic relic keeps its hook and its rule at every tier: only the stat block scales");
            }

            // Tier 1 is the very object content holds, so nothing that does not upgrade is touched.
            GameSession fresh = QmSession(db, 40, 0);
            RelicEquipTeam(fresh, relic.Id);
            Check(ReferenceEquals(fresh.Profile.EquippedRelicOf(fresh.Profile.HeroHoldingRelic(relic.Id), db), db.GetRelic(relic.Id)),
                  "a tier 1 relic resolves to the content's own object, so every fight that uses no upgrade goes through exactly what it always did");

            // Power follows the tier: the score the campaign is graded on reads the same resolver.
            GameSession power = QmSession(db, 40, 0);
            RelicEquipTeam(power, relic.Id);
            int p1power = power.Profile.EffectiveTeamPower(db);
            power.Profile.FindOwnedRelic(relic.Id).Tier = 3;
            int p3power = power.Profile.EffectiveTeamPower(db);
            Check(p3power > p1power, "and a tier 3 relic raises the team's power score (" + p1power + " to " + p3power + "), by the same resolver the fight uses");
        }

        private static void CheckRelicUpgradeRefusals(ContentDatabase db)
        {
            RelicDef relic = db.Relics[0];
            GameSession s = QmSession(db, 40, 0);
            s.Profile.RelicDust = 10000000;
            string before = SaveSerializer.Serialize(s.Profile);
            bool notOwned = s.UpgradeRelic(relic.Id, SinkBase), unknown = s.UpgradeRelic("no_such_relic", SinkBase), empty = s.UpgradeRelic("", SinkBase);
            Check(!notOwned && !unknown && !empty && SaveSerializer.Serialize(s.Profile) == before,
                  "UPGRADE REFUSALS: a relic not owned, an unknown relic and an empty id are refused, the save byte for byte as it was");

            s.GrantRelic(relic.Id);
            int price = s.RelicUpgradePrice(relic, 2);
            s.Profile.RelicDust = price - 1;
            before = SaveSerializer.Serialize(s.Profile);
            bool poor = s.UpgradeRelic(relic.Id, SinkBase);
            Check(!poor && SaveSerializer.Serialize(s.Profile) == before, "one Dust short is refused, the save byte for byte as it was");
            s.Profile.RelicDust = price;
            Check(s.UpgradeRelic(relic.Id, SinkBase) && s.Profile.RelicDust == 0, "and exactly enough upgrades and leaves the purse at zero");

            List<RelicCardView> shelf = RelicShop.View(s);
            RelicCardView card = null;
            for (int i = 0; i < shelf.Count; i++) if (shelf[i].Relic.Id == relic.Id) card = shelf[i];
            Check(card != null && card.Owned && card.Tier == 2 && card.UpgradePrice == s.RelicUpgradePrice(relic, 3) && !card.CanUpgrade && card.Poor && card.NextStats.Length > 0,
                  "the shelf says the relic is owned at tier 2, what tier 3 costs, what it would give, and that the purse is short");
        }

        // ------------------------------------------------------------------ 5. tier in a fight

        private static void CheckRelicTierInAFight(ContentDatabase db, string contentDir)
        {
            // A fixed fight with a tier-1 relic is the same fight the build before fought (combat does not move).
            ContentDatabase noShop = LoadContent(contentDir);
            noShop.LoadRelicShop(null);
            bool same = true;
            int[] stages = { 0, Math.Min(5, db.AllStages.Count - 1), Math.Min(20, db.AllStages.Count - 1) };
            for (int i = 0; i < stages.Length; i++)
            {
                GameSession with = QmSession(db, 40, 0), without = QmSession(noShop, 40, 0);
                RelicEquipTeam(with, db.Relics[0].Id);
                RelicEquipTeam(without, db.Relics[0].Id);
                string fa = FingerprintOf(CombatSimulation.Run(with.BuildBattleRequest(CampaignWaveGenerator.BuildBattle(db.AllStages[stages[i]], 1, 1), 4242)));
                string fb = FingerprintOf(CombatSimulation.Run(without.BuildBattleRequest(CampaignWaveGenerator.BuildBattle(noShop.AllStages[stages[i]], 1, 1), 4242)));
                if (fa != fb) same = false;
            }
            Check(same, "COMBAT DOES NOT MOVE: three fixed fights with a tier 1 relic equipped are the same event for event with relic-shop.json loaded and with the file removed");

            // A tier 3 relic is a stronger fight than tier 1 (the upgrade is worth something).
            GameSession t1 = QmSession(db, 40, 0), t3 = QmSession(db, 40, 0);
            int differs = 0;
            RelicEquipTeam(t1, db.Relics[0].Id);
            RelicEquipTeam(t3, db.Relics[0].Id);
            t3.Profile.FindOwnedRelic(db.Relics[0].Id).Tier = 3;
            for (int i = 0; i < stages.Length; i++)
            {
                string f1 = FingerprintOf(CombatSimulation.Run(t1.BuildBattleRequest(CampaignWaveGenerator.BuildBattle(db.AllStages[stages[i]], 1, 1), 4242)));
                string f3 = FingerprintOf(CombatSimulation.Run(t3.BuildBattleRequest(CampaignWaveGenerator.BuildBattle(db.AllStages[stages[i]], 1, 1), 4242)));
                if (f1 != f3) differs++;
            }
            Check(differs >= 1, "while a tier 3 relic fights a different fight from a tier 1 one (" + differs + " of 3 fixed fights differ): an upgrade is worth something");
        }

        // ------------------------------------------------------------------ 6. the save

        private static void CheckRelicEndgameSave(ContentDatabase db)
        {
            string error;
            GameSession plain = QmSession(db, 40, 0);
            plain.GrantRelic(db.Relics[0].Id);
            string plainJson = SaveSerializer.Serialize(plain.Profile);
            Check(JsonValue.Parse(plainJson).GetInt("version", 0) == SaveSerializer.BaseVersion,
                  "A TIER 1 RELIC adds nothing to the document: it still says version " + SaveSerializer.BaseVersion + ", as the build before wrote");

            GameSession up = QmSession(db, 40, 0);
            up.GrantRelic(db.Relics[0].Id);
            up.Profile.FindOwnedRelic(db.Relics[0].Id).Tier = 2;
            string upJson = SaveSerializer.Serialize(up.Profile);
            PlayerProfile back = SaveSerializer.Deserialize(upJson, db, out error);
            Check(back != null && SaveSerializer.Serialize(back) == upJson && back.RelicTier(db.Relics[0].Id) == 2
                  && SaveSerializer.VersionOfJson(upJson) == 24,
                  "AN UPGRADED RELIC claims version 24, saves, loads back at tier 2 and saves again byte for byte");
        }

        // ------------------------------------------------------------------ 7. not hashed

        private static void CheckRelicShopNotHashed(ContentDatabase db, string contentDir)
        {
            string[] hashed = { "heroes", "enemies", "campaign", "balance", "relics", "events", "armor", "floor-conditions" };
            string[] texts = new string[hashed.Length];
            for (int i = 0; i < hashed.Length; i++)
            {
                string p = Path.Combine(contentDir, hashed[i] + ".json");
                texts[i] = File.Exists(p) ? File.ReadAllText(p) : null;
            }
            Check(ContentDatabase.VersionOf(texts) == db.ContentVersion,
                  "RELIC-SHOP.JSON IS NOT HASHED: ContentVersion is exactly the hash of the eight fight files (" + db.ContentVersion + "), and relic-shop.json is not among them");

            ContentDatabase other = LoadContent(contentDir);
            string version = other.ContentVersion;
            other.LoadRelicShop(File.ReadAllText(Path.Combine(contentDir, "relic-shop.json")).Replace("\"Legendary\": 5000", "\"Legendary\": 6000"));
            GameSession a = QmSession(db, 40, 0), b = QmSession(other, 40, 0);
            Check(b.RelicPrice(other.Relics[0]) == 6000 && a.RelicPrice(db.Relics[0]) == 5000 && other.ContentVersion == version && version == db.ContentVersion,
                  "so retuning a price moves the price (5000 to 6000) and not ContentVersion, which keeps every recorded guild dungeon replay");
        }

        // ------------------------------------------------------------------ 8. rollback

        private static void CheckRelicEndgameOff(ContentDatabase db)
        {
            bool was = RelicEndgameSwitch.Enabled;
            try
            {
                RelicEndgameSwitch.Enabled = false;
                RelicDef legendary = null, mythic = null;
                for (int i = 0; i < db.Relics.Count; i++)
                {
                    if (db.Relics[i].Grade == RelicGrade.Legendary && legendary == null) legendary = db.Relics[i];
                    if (db.Relics[i].Grade == RelicGrade.Mythic && mythic == null) mythic = db.Relics[i];
                }
                GameSession s = QmSession(db, 40, 0);
                Check(!s.RelicEndgameActive && s.RelicPrice(legendary) == db.Balance.RelicShopPrice && s.RelicPrice(mythic) == db.Balance.RelicMythicShopPrice
                      && s.RelicMaxTier == db.Balance.RelicMaxTier && s.RelicMaxTier == 1,
                      "ROLLBACK: with the switch off the shop charges balance.json's prices again (" + s.RelicPrice(legendary) + " and " + s.RelicPrice(mythic) + ") and the top tier is " + s.RelicMaxTier);

                s.GrantRelic(legendary.Id);
                s.Profile.RelicDust = 100000000;
                string before = SaveSerializer.Serialize(s.Profile);
                bool upgraded = s.UpgradeRelic(legendary.Id, SinkBase);
                Check(!upgraded && s.RelicUpgradePrice(legendary, 2) == 0 && SaveSerializer.Serialize(s.Profile) == before,
                      "an upgrade is refused, with Dust to spare, and the save is byte for byte as it was");

                // An owned tier is ignored in a fight and in the power score: the build before.
                GameSession tiered = QmSession(db, 40, 0), baseline = QmSession(db, 40, 0);
                RelicEquipTeam(tiered, legendary.Id);
                RelicEquipTeam(baseline, legendary.Id);
                tiered.Profile.FindOwnedRelic(legendary.Id).Tier = 3;
                string ft = FingerprintOf(CombatSimulation.Run(tiered.BuildBattleRequest(CampaignWaveGenerator.BuildBattle(db.AllStages[5], 1, 1), 4242)));
                string fb = FingerprintOf(CombatSimulation.Run(baseline.BuildBattleRequest(CampaignWaveGenerator.BuildBattle(db.AllStages[5], 1, 1), 4242)));
                Check(ft == fb && tiered.Profile.EffectiveTeamPower(db) == baseline.Profile.EffectiveTeamPower(db)
                      && ReferenceEquals(tiered.Profile.EquippedRelicOf(tiered.Profile.HeroHoldingRelic(legendary.Id), db), db.GetRelic(legendary.Id)),
                      "and an owned tier above 1 is ignored in a fight and in the power score: the fight is the tier 1 fight event for event");

                // The save an off session writes is main's document; a tier on file is kept untouched.
                GameSession script = QmSession(db, 40, 0);
                script.GrantRelic(legendary.Id);
                string offJson = SaveSerializer.Serialize(script.Profile);
                JsonValue root = JsonValue.Parse(offJson);
                List<string> keys = new List<string>();
                foreach (string key in root.Keys) keys.Add(key);
                Check(root.GetInt("version", 0) == 22 && keys.Count == 40,
                      "the save is version 22 with the 40 keys main writes (" + keys.Count + " keys)");
                string error;
                tiered.Profile.RelicDust = 0;
                string tieredJson = SaveSerializer.Serialize(tiered.Profile);
                PlayerProfile kept = SaveSerializer.Deserialize(tieredJson, db, out error);
                Check(kept != null && SaveSerializer.Serialize(kept) == tieredJson && kept.RelicTier(legendary.Id) == 3,
                      "with the switch off a tier on file is still read and written back untouched: turning it off costs a player nothing");

                // The R16 curve walk reads no relic, so it cannot move with the prices or the tiers; the walk itself runs in the full harness.
                Check(db.Balance.RelicShopPrice == 80 && db.Balance.RelicMythicShopPrice == 2400 && db.Balance.RelicMaxTier == 1,
                      "balance.json still carries the old prices (80, 2400) and relicMaxTier 1: no hashed file was touched to retune");
            }
            finally { RelicEndgameSwitch.Enabled = was; }
        }

        // ------------------------------------------------------------------ 9. wiring

        private static void CheckRelicEndgameWiring(string contentDir)
        {
            string root = Path.GetFullPath(Path.Combine(contentDir, "..", "..", ".."));
            string screenPath = Path.Combine(root, "Assets", "Scripts", "UI", "Screens", "RelicShopScreen.cs");
            if (!File.Exists(screenPath)) { Check(false, "the Relic Shop screen is where the harness expects it"); return; }
            string screen = File.ReadAllText(screenPath);
            bool decides = screen.Contains("RelicDust -=") || screen.Contains("RelicDust +=") || screen.Contains("Tier++") || screen.Contains(".Tier =")
                        || screen.Contains("BuyRelic(") || screen.Contains("UpgradeRelic(") && !screen.Contains("RelicShop.Upgrade(");
            Check(!decides && screen.Contains("RelicShop.View(") && screen.Contains("RelicShop.Buy(") && screen.Contains("RelicShop.Upgrade("),
                  "WIRING (reads source): the Relic Shop screen draws RelicShop.View and sells and upgrades only through RelicShop.Buy and Upgrade, and writes no purse and no tier");
        }
    }
}
