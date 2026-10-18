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
    /// THE SINKS (DESIGN-SINKS.md): the Armory, which sells for Valor, and the Archive, which sells
    /// for Coda. One named check per rule: the shipped content, the price formula tied to the real
    /// grants, a purchase being deterministic, only the shop's own purse moving, the caps and the
    /// clock, the attempt banks, every refusal leaving the save untouched, nothing random in the
    /// code, the save, sinks.json staying out of ContentVersion, the switch, and combat not moving.
    ///
    /// Every purchase goes through SinkShop.Buy with an explicit clock; a check sets a purse or a
    /// bank only to name the situation it wants, the way DailyLoopChecks names a board.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>A fixed clock for the sink checks (2025-10-09 08:53 UTC).</summary>
        private const long SinkBase = 1760000000L;

        private static void CheckSinks(ContentDatabase db, string contentDir)
        {
            Line("");
            Line("SINKS (DESIGN-SINKS.md: the Armory sells for Valor and the Archive for Coda, fixed items, no chance in any of them)");

            bool was = SinksSwitch.Enabled;
            bool onboardingWas = OnboardingSwitch.Enabled;
            try
            {
                SinksSwitch.Enabled = true;
                CheckSinksContent(db, contentDir);
                CheckSinksPriceFormula(db);
                CheckSinksBuyDeterministic(db);
                CheckSinksSpendsOnlyItsPurse(db);
                CheckSinksCaps(db);
                CheckSinksBanks(db);
                CheckSinksRefusals(db);
                CheckSinksNothingRandom(contentDir);
                CheckSinksWiring(contentDir);
                CheckSinksSave(db);
                CheckSinksNotHashed(db, contentDir);
                CheckSinksOff(db);
                CheckSinksNoCombatChange(db, contentDir);
            }
            finally
            {
                SinksSwitch.Enabled = was;
                OnboardingSwitch.Enabled = onboardingWas;
            }
            Check(SinksSwitch.Enabled == was && OnboardingSwitch.Enabled == onboardingWas,
                  "the switches are put back as the run found them (sinks " + (was ? "on" : "off") + ")");
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A session with every stage cleared (so the Proving, the Recital and armor are
        /// open), a standing, a floor and two full purses. Both banks start empty at SinkBase.</summary>
        private static GameSession SinkSession(ContentDatabase db, int standing, int floor, long valor, long coda)
        {
            PlayerProfile p = GameSession.CreateNewProfile(db, SinkBase);
            for (int i = 0; i < db.AllStages.Count; i++) p.ClearedStageIds.Add(db.AllStages[i].Id);
            p.StagesCleared = CampaignProgress.CountCleared(p, db);
            p.ProvingStanding = standing;
            p.ProvingHighestStanding = standing;
            p.RecitalHighestFloor = floor;
            p.RecitalCurrentFloor = floor + 1;
            p.Valor = valor;
            p.Coda = coda;
            p.RecitalEchoesBanked = 0;
            p.RecitalEchoesClockUnix = SinkBase;
            p.ProvingBrandsBanked = 0;
            p.ProvingBrandsClockUnix = SinkBase;
            return new GameSession(db, p);
        }

        private static void SinkDrain(GameSession s, long now)
        {
            s.Profile.ProvingBrandsBanked = 0;
            s.Profile.ProvingBrandsClockUnix = now;
            s.Profile.RecitalEchoesBanked = 0;
            s.Profile.RecitalEchoesClockUnix = now;
        }

        /// <summary>The hero a hero item should be bought with: its first listed hero.</summary>
        private static string SinkChoiceFor(ContentDatabase db, string itemId)
        {
            SinkItemDef item = db.Sinks.Item(itemId);
            return item != null && item.Heroes.Count > 0 ? item.Heroes[0] : null;
        }

        private static long SinkUnix(int year, int month, int day, int hour)
        {
            DateTime d = new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Utc);
            return (long)(d - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        private static long SinkPriceAt(ContentDatabase db, int standing, int floor, string itemId)
        {
            GameSession s = SinkSession(db, standing, floor, 0, 0);
            return SinkPricing.Price(s, db.Sinks.Item(itemId));
        }

        private static int SinkCount(ContentDatabase db, SinkKind kind)
        {
            int n = 0;
            for (int i = 0; i < db.Sinks.Items.Count; i++) if (db.Sinks.Items[i].Kind == kind) n++;
            return n;
        }

        // ------------------------------------------------------------------ 1. content

        private static void CheckSinksContent(ContentDatabase db, string contentDir)
        {
            int sinkWarnings = 0;
            for (int i = 0; i < db.Warnings.Count; i++) if (db.Warnings[i].StartsWith("sinks.json")) sinkWarnings++;
            Check(db.Sinks.IsUsable && sinkWarnings == 0,
                  "THE SHIPPED FILE LOADS CLEAN: sinks.json parses and validates against heroes.json and armor.json with " + sinkWarnings + " warnings");

            Check(SinkCount(db, SinkKind.Armor) == 6 && SinkCount(db, SinkKind.Brand) == 1 && SinkCount(db, SinkKind.Echo) == 1
                  && SinkCount(db, SinkKind.HeroCopy) == 2 && db.Sinks.ItemsOf(SinkShopId.Armory).Count == 9
                  && db.Sinks.ItemsOf(SinkShopId.Archive).Count == 1,
                  "and it sells six armor presets, a Brand, an Echo and two hero shelves: nine items in the Armory, one in the Archive");
            Check(Enum.GetValues(typeof(SinkKind)).Length == 4,
                  "the kinds of thing a shop can sell are exactly four (armor, brand, echo, heroCopy): nothing random is expressible");

            // Every price is a positive multiple of the price step, at several standings and floors.
            bool steps = true;
            int[] standings = { 0, 3, 10, 30 };
            int[] floors = { 0, 7, 10, 50, 100 };
            for (int a = 0; a < standings.Length; a++)
                for (int b = 0; b < floors.Length; b++)
                    for (int i = 0; i < db.Sinks.Items.Count; i++)
                    {
                        long price = SinkPriceAt(db, standings[a], floors[b], db.Sinks.Items[i].Id);
                        if (price <= 0 || price % db.Sinks.PriceRound != 0) steps = false;
                    }
            Check(steps, "every price is a positive multiple of " + db.Sinks.PriceRound + ", at any standing and floor");

            // The worked table in PLAN.md section B.
            Check(SinkPriceAt(db, 0, 0, "armory_weapon") == 900 && SinkPriceAt(db, 10, 0, "armory_weapon") == 1500
                  && SinkPriceAt(db, 30, 0, "armory_weapon") == 2700,
                  "an armor preset costs 6 days of Valor: 900 at standing 0, 1500 at 10, 2700 at 30");
            Check(SinkPriceAt(db, 0, 0, "armory_hero") == 1800 && SinkPriceAt(db, 10, 0, "armory_hero") == 3000
                  && SinkPriceAt(db, 30, 0, "armory_hero") == 5400,
                  "a Valor hero copy costs 12 days: 1800, 3000, 5400");
            Check(SinkPriceAt(db, 0, 0, "armory_brand") == 40 && SinkPriceAt(db, 10, 0, "armory_brand") == 65
                  && SinkPriceAt(db, 30, 0, "armory_brand") == 115
                  && SinkPriceAt(db, 0, 0, "armory_echo") == 150 && SinkPriceAt(db, 10, 0, "armory_echo") == 250
                  && SinkPriceAt(db, 30, 0, "armory_echo") == 450,
                  "a Brand costs a quarter day (40, 65, 115, rounded up) and an Echo a whole day (150, 250, 450)");
            Check(SinkPriceAt(db, 0, 0, "archive_hero") == 360 && SinkPriceAt(db, 0, 10, "archive_hero") == 960
                  && SinkPriceAt(db, 0, 50, "archive_hero") == 3360 && SinkPriceAt(db, 0, 100, "archive_hero") == 6360,
                  "an Archive hero copy costs 12 days of Coda: 360 at floor 0, 960 at 10, 3360 at 50, 6360 at 100");

            // The presets are things the roller could have made, and nothing stronger.
            bool presets = true;
            List<SinkItemDef> all = db.Sinks.Items;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Kind != SinkKind.Armor) continue;
                SinkArmorSpec a = all[i].Armor;
                ArmorRollDef range = db.Armor.GetPrimaryRoll(a.Slot);
                ArmorTemperDef temper = db.Armor.GetTemper(a.Temper);
                if (a.Temper != ArmorTemper.Honed || (int)a.Temper > (int)ArmorInfo.MaxDropTemper
                    || a.Subs.Count != ArmorInfo.SubStatCount(a.Temper) || a.Primary.Stat != ArmorInfo.PrimaryStat(a.Slot)
                    || range == null || temper == null
                    || a.Primary.Value < range.Min * temper.PrimaryScale - 0.0001f
                    || a.Primary.Value > range.Max * temper.PrimaryScale + 0.0001f) presets = false;
            }
            Check(presets, "every armor preset is Honed or lower, has its slot's primary and its temper's substat count, and sits inside the roller's range");

            // The hero lists.
            SinkItemDef armoryHeroes = db.Sinks.Item("armory_hero"), archiveHeroes = db.Sinks.Item("archive_hero");
            bool lists = armoryHeroes != null && archiveHeroes != null && armoryHeroes.Heroes.Count == 14 && archiveHeroes.Heroes.Count == 12;
            for (int i = 0; lists && i < all.Count; i++)
            {
                if (all[i].Kind != SinkKind.HeroCopy) continue;
                for (int h = 0; h < all[i].Heroes.Count; h++)
                {
                    HeroDef def = db.GetHero(all[i].Heroes[h]);
                    if (def == null || def.Stars != 4 || def.StartingHero) lists = false;
                }
            }
            for (int h = 0; lists && h < armoryHeroes.Heroes.Count; h++) if (archiveHeroes.Heroes.Contains(armoryHeroes.Heroes[h])) lists = false;
            Check(lists, "the hero lists name 14 and 12 heroes, every one four star and no starter, and no hero is on both");

            // A corrupted preset, a five star in a list and an unknown kind are dropped, each with a warning.
            HeroDef five = null;
            for (int i = 0; i < db.Heroes.Count && five == null; i++) if (db.Heroes[i].Stars == 5) five = db.Heroes[i];
            string text = File.ReadAllText(Path.Combine(contentDir, "sinks.json"));
            string bad = text.Replace("\"primary\": 0.084", "\"primary\": 0.9")
                             .Replace("\"branik_the_siege_mason\"", "\"" + (five == null ? "nobody" : five.Id) + "\"")
                             .Replace("\"kind\": \"echo\"", "\"kind\": \"gacha\"");
            ContentDatabase other = LoadContent(contentDir);
            int before = other.Warnings.Count;
            other.LoadSinks(bad);
            SinkItemDef listBad = other.Sinks.Item("armory_hero");
            Check(other.Sinks.Item("armory_weapon") == null && other.Sinks.Item("armory_plate") == null
                  && other.Sinks.Item("armory_echo") == null && other.Sinks.Item("armory_helm") != null
                  && listBad != null && listBad.Heroes.Count == 13 && five != null && !listBad.Heroes.Contains(five.Id)
                  && other.Warnings.Count - before >= 4,
                  "CORRUPTION IS DROPPED, NOT SOLD: a primary far outside the roller's range drops its preset, a five star drops out of a hero list, "
                  + "an unknown kind drops its item, each with a warning (" + (other.Warnings.Count - before) + " warnings)");

            other.LoadSinks(null);
            Check(!other.Sinks.IsUsable, "and a build with no sinks.json has nothing to sell");
        }

        // ------------------------------------------------------------------ 2. price formula

        private static void CheckSinksPriceFormula(ContentDatabase db)
        {
            BalanceConfig b = db.Balance;
            Check(SinkPricing.AttemptsPerDay(b.ProvingBrandsMax, b.ProvingBrandsRechargeHours) == Math.Min(b.ProvingBrandsMax, (int)(24f / b.ProvingBrandsRechargeHours))
                  && SinkPricing.AttemptsPerDay(b.RecitalEchoesMax, b.RecitalEchoesRechargeHours) == Math.Min(b.RecitalEchoesMax, (int)(24f / b.RecitalEchoesRechargeHours)),
                  "a day holds as many attempts as the bank allows or the clock regenerates, whichever is fewer ("
                  + SinkPricing.AttemptsPerDay(b.ProvingBrandsMax, b.ProvingBrandsRechargeHours) + " Brands, "
                  + SinkPricing.AttemptsPerDay(b.RecitalEchoesMax, b.RecitalEchoesRechargeHours) + " Echoes)");

            // Valor: one winning attempt at a standing, as the real grant pays it, times a day's attempts.
            int[] standings = { 0, 1, 10, 30, 75 };
            bool valorOk = true;
            string valorNote = "";
            for (int i = 0; i < standings.Length; i++)
            {
                GameSession s = SinkSession(db, standings[i], 0, 0, 0);
                ProvingRewards r = s.GrantProvingResult(true, SinkBase);
                long day = SinkPricing.ValorDay(b, standings[i]);
                long expected = (long)SinkPricing.AttemptsPerDay(b.ProvingBrandsMax, b.ProvingBrandsRechargeHours) * r.Valor;
                if (day != expected) { valorOk = false; valorNote += " S" + standings[i] + ": " + day + " vs " + expected; }
            }
            Check(valorOk, "THE VALOR DAY IS THE REAL GRANT: a day's income equals the attempts a day holds times what GrantProvingResult pays a win at that standing" + valorNote);

            // Coda: a non-Guardian floor, as the real grant pays it.
            int[] wanted = { 0, 4, 10, 50, 100 };
            bool codaOk = true;
            string codaNote = "";
            int checkedFloors = 0;
            for (int i = 0; i < wanted.Length; i++)
            {
                int highest = wanted[i];
                while (RecitalGenerator.IsGuardian(b, highest + 1)) highest++;
                GameSession s = SinkSession(db, 0, highest, 0, 0);
                RecitalRewards r = s.GrantRecitalResult(highest + 1, true, SinkBase);
                long day = SinkPricing.CodaDay(b, highest);
                long expected = (long)SinkPricing.AttemptsPerDay(b.RecitalEchoesMax, b.RecitalEchoesRechargeHours) * r.Coda;
                checkedFloors++;
                if (day != expected) { codaOk = false; codaNote += " F" + highest + ": " + day + " vs " + expected; }
            }
            Check(codaOk && checkedFloors == wanted.Length,
                  "and the Coda day is the real grant too: GrantRecitalResult on the next floor, a non-Guardian one, times a day's attempts" + codaNote);

            // The basis is the HIGHEST standing: losing on purpose lowers the standing and moves no price.
            GameSession thrown = SinkSession(db, 20, 0, 0, 0);
            long priced = SinkPricing.Price(thrown, db.Sinks.Item("armory_weapon"));
            thrown.Profile.ProvingStanding = 0;
            Check(SinkPricing.Price(thrown, db.Sinks.Item("armory_weapon")) == priced,
                  "prices follow the highest standing ever reached, so throwing fights to lower the standing does not lower a price");
        }

        // ------------------------------------------------------------------ 3. determinism

        private static void CheckSinksBuyDeterministic(ContentDatabase db)
        {
            GameSession seed = SinkSession(db, 10, 12, 20000, 20000);
            string json0 = SaveSerializer.Serialize(seed.Profile);
            string error;
            GameSession a = new GameSession(db, SaveSerializer.Deserialize(json0, db, out error));
            GameSession b = new GameSession(db, SaveSerializer.Deserialize(json0, db, out error));

            string[] items = { "armory_weapon", "armory_brand", "armory_brand", "armory_echo", "armory_hero", "archive_hero" };
            bool same = true;
            ArmorPiece first = null;
            for (int i = 0; i < items.Length; i++)
            {
                string choice = SinkChoiceFor(db, items[i]);
                SinkResult ra = SinkShop.Buy(a, items[i], choice, SinkBase + 100 + i);
                SinkResult rb = SinkShop.Buy(b, items[i], choice, SinkBase + 100 + i);
                if (!ra.Success || !rb.Success || ra.Price != rb.Price || ra.Reason != rb.Reason) same = false;
                if (ra.Armor != null) first = ra.Armor;
            }
            Check(same && SaveSerializer.Serialize(a.Profile) == SaveSerializer.Serialize(b.Profile),
                  "TWO SESSIONS FROM ONE SAVE, THE SAME SIX PURCHASES: the same results and byte-identical saves");

            SinkArmorSpec spec = db.Sinks.Item("armory_weapon").Armor;
            bool exact = first != null && first.Name == spec.Name && first.Slot == spec.Slot && first.Aspect == spec.Aspect
                         && first.Temper == spec.Temper && first.Primary.Stat == spec.Primary.Stat && first.Primary.Value == spec.Primary.Value
                         && first.Primary.Mode == spec.Primary.Mode && first.SubStats.Count == spec.Subs.Count;
            for (int i = 0; exact && i < spec.Subs.Count; i++)
                exact = first.SubStats[i].Stat == spec.Subs[i].Stat && first.SubStats[i].Value == spec.Subs[i].Value && first.SubStats[i].Mode == spec.Subs[i].Mode;
            Check(exact && a.Profile.FindArmor(first.Uid) == first && a.Profile.OwnedArmor.Count == 1,
                  "and the piece is exactly the preset in sinks.json, name, slot, aspect, temper and every stat line, in the bag under a fresh uid");

            // Buying the same thing again in a fresh session changes nothing about what it is.
            GameSession c = new GameSession(db, SaveSerializer.Deserialize(json0, db, out error));
            SinkResult again = SinkShop.Buy(c, "armory_weapon", null, SinkBase + 9999);
            Check(again.Success && again.Armor.Name == first.Name && again.Armor.Primary.Value == first.Primary.Value && again.Armor.Uid == first.Uid,
                  "at another time of day the same piece with the same uid, so nothing about a purchase depends on the clock but its caps");
        }

        // ------------------------------------------------------------------ 4. purses

        private static void CheckSinksSpendsOnlyItsPurse(ContentDatabase db)
        {
            bool ok = true;
            string note = "";
            for (int i = 0; i < db.Sinks.Items.Count; i++)
            {
                SinkItemDef item = db.Sinks.Items[i];
                GameSession s = SinkSession(db, 10, 12, 50000, 50000);
                PlayerProfile p = s.Profile;
                long gold = p.Gold, shards = p.Shards, dust = p.RelicDust, slag = p.Slag, valor = p.Valor, coda = p.Coda;
                int summons = p.TotalSummons;

                SinkResult r = SinkShop.Buy(s, item.Id, SinkChoiceFor(db, item.Id), SinkBase + 60);
                bool paid = item.Shop == SinkShopId.Armory
                    ? p.Valor == valor - r.Price && p.Coda == coda
                    : p.Coda == coda - r.Price && p.Valor == valor;
                bool rest = p.Gold == gold && p.Shards == shards && p.RelicDust == dust && p.Slag == slag && p.TotalSummons == summons;
                if (!r.Success || r.Price <= 0 || !paid || !rest) { ok = false; note += " " + item.Id; }
            }
            Check(ok, "EVERY ITEM TAKES ITS PRICE FROM ITS OWN PURSE and from nothing else: Valor for the Armory, Coda for the Archive, "
                  + "and Gold, Shards, Relic Dust, Slag and the summon count never move" + note);

            GameSession h = SinkSession(db, 10, 12, 50000, 50000);
            string choice = SinkChoiceFor(db, "archive_hero");
            int heroes = h.Profile.Heroes.Count;
            SinkResult got = SinkShop.Buy(h, "archive_hero", choice, SinkBase + 60);
            HeroInstance owned = h.Profile.FindHeroByDefId(choice);
            Check(got.Success && owned != null && owned.Copies == 1 && h.Profile.Heroes.Count == heroes + 1 && got.Hero == owned,
                  "a hero copy of a hero the player lacks adds the hero, and the next copy adds a copy");
            SinkResult second = SinkShop.Buy(h, "archive_hero", choice, SinkBase + 61);
            Check(second.Reason == SinkRefusal.CapReached, "the Archive's one copy a month is spent after the first");
            h.Profile.Sinks.Monthly.Clear();
            SinkResult third = SinkShop.Buy(h, "archive_hero", choice, SinkBase + 62);
            Check(third.Success && h.Profile.FindHeroByDefId(choice).Copies == 2, "and with the month's allowance back, the same hero gains a copy");
        }

        // ------------------------------------------------------------------ 5. caps and the clock

        private static void CheckSinksCaps(ContentDatabase db)
        {
            // ---- daily: three Brands a day, back at the next reward day
            GameSession s = SinkSession(db, 10, 12, 100000, 100000);
            long d0 = s.DayStartUnix(SinkBase);
            long next = d0 + 86400L;
            bool three = true;
            for (int i = 0; i < 3; i++) three &= SinkShop.Buy(s, "armory_brand", null, d0 + 3600 + i).Success;
            SinkResult fourth = SinkShop.Buy(s, "armory_brand", null, d0 + 3700);
            Check(three && fourth.Reason == SinkRefusal.CapReached,
                  "DAILY CAP: three Brands buy and the fourth is refused for the cap, not for the bank or the purse");
            SinkResult lastSecond = SinkShop.Buy(s, "armory_brand", null, next - 1);
            Check(lastSecond.Reason == SinkRefusal.CapReached, "and still refused the last second of the reward day");
            SinkDrain(s, next);
            SinkResult reopened = SinkShop.Buy(s, "armory_brand", null, next);
            Check(reopened.Success, "and open again at the next reward day boundary (" + db.Balance.DailyResetHourUtc + ":00 UTC)");
            SinkResult echoOne = SinkShop.Buy(s, "armory_echo", null, next + 5);
            SinkResult echoTwo = SinkShop.Buy(s, "armory_echo", null, next + 6);
            Check(echoOne.Success && echoTwo.Reason == SinkRefusal.CapReached, "the Echo is one a day");

            // ---- monthly: one hero copy a month, back on the first reward day of the next month
            int[][] months =
            {
                new[] { 2025, 10, 9, 2025, 11 },    // a 31 day month
                new[] { 2025, 11, 10, 2025, 12 },   // a 30 day month
                new[] { 2026, 2, 10, 2026, 3 },     // a 28 day month
                new[] { 2028, 2, 10, 2028, 3 },     // a leap February
                new[] { 2025, 12, 15, 2026, 1 }     // December into January
            };
            for (int m = 0; m < months.Length; m++)
            {
                GameSession g = SinkSession(db, 10, 12, 100000, 100000);
                long t0 = SinkUnix(months[m][0], months[m][1], months[m][2], 12);
                long boundary = SinkUnix(months[m][3], months[m][4], 1, db.Balance.DailyResetHourUtc);
                string choice = SinkChoiceFor(db, "armory_hero");
                SinkResult one = SinkShop.Buy(g, "armory_hero", choice, t0);
                SinkResult two = SinkShop.Buy(g, "armory_hero", choice, t0 + 3600);
                SinkResult edge = SinkShop.Buy(g, "armory_hero", choice, boundary - 1);
                long computed = SinkShop.NextMonthUnix(g, g.DayStartUnix(t0));
                SinkResult open = SinkShop.Buy(g, "armory_hero", choice, boundary);
                SinkResult shut = SinkShop.Buy(g, "armory_hero", choice, boundary + 60);
                Check(one.Success && two.Reason == SinkRefusal.CapReached && edge.Reason == SinkRefusal.CapReached
                      && open.Success && shut.Reason == SinkRefusal.CapReached && computed == boundary,
                      "MONTHLY CAP, " + months[m][0] + "-" + months[m][1] + ": one copy, refused until the first reward day of " + months[m][3] + "-" + months[m][4]
                      + ", open there, once");
            }

            // ---- one shared cap across the six armor presets
            GameSession arm = SinkSession(db, 10, 12, 100000, 100000);
            string[] presets = { "armory_weapon", "armory_helm", "armory_plate", "armory_boots", "armory_ring", "armory_charm" };
            bool firstThree = true;
            for (int i = 0; i < 3; i++) firstThree &= SinkShop.Buy(arm, presets[i], null, SinkBase + i).Success;
            SinkResult boots = SinkShop.Buy(arm, presets[3], null, SinkBase + 10);
            SinkResult charm = SinkShop.Buy(arm, presets[5], null, SinkBase + 11);
            SinkResult hero = SinkShop.Buy(arm, "armory_hero", SinkChoiceFor(db, "armory_hero"), SinkBase + 12);
            Check(firstThree && boots.Reason == SinkRefusal.CapReached && charm.Reason == SinkRefusal.CapReached && hero.Success
                  && arm.Profile.OwnedArmor.Count == 3,
                  "THE ARMOR CAP IS SHARED: three pieces a month across all six presets, whichever they are, and the hero shelf is a separate count");

            // ---- a clock wound back reopens nothing
            GameSession back = SinkSession(db, 10, 12, 100000, 100000);
            long late = SinkBase + 40 * 86400L;
            SinkDrain(back, late);
            for (int i = 0; i < 3; i++) SinkShop.Buy(back, "armory_brand", null, late + i);
            SinkShop.Buy(back, "armory_hero", SinkChoiceFor(db, "armory_hero"), late);
            SinkResult brandBack = SinkShop.Buy(back, "armory_brand", null, late - 3 * 86400L);
            SinkResult heroBack = SinkShop.Buy(back, "armory_hero", SinkChoiceFor(db, "armory_hero"), late - 40 * 86400L);
            Check(brandBack.Reason == SinkRefusal.CapReached && heroBack.Reason == SinkRefusal.CapReached,
                  "A CLOCK WOUND BACK REOPENS NOTHING: three days earlier the Brands are still spent, forty days earlier the month's hero copy still is");
            long dayStamp = back.Profile.Sinks.DayUnix, monthStamp = back.Profile.Sinks.MonthKey;
            Check(dayStamp == back.DayStartUnix(late) && monthStamp == SinkShop.MonthKeyOf(back.DayStartUnix(late)),
                  "and the stamps stayed where the later day put them: they only move forward");

            // ---- a year later the same month number is a different month, once
            GameSession year = SinkSession(db, 10, 12, 100000, 100000);
            string pick = SinkChoiceFor(db, "armory_hero");
            SinkResult y0 = SinkShop.Buy(year, "armory_hero", pick, SinkBase);
            SinkResult y1 = SinkShop.Buy(year, "armory_hero", pick, SinkBase + 1);
            long later = SinkBase + 365 * 86400L;
            SinkResult y2 = SinkShop.Buy(year, "armory_hero", pick, later);
            SinkResult y3 = SinkShop.Buy(year, "armory_hero", pick, later + 1);
            Check(y0.Success && y1.Reason == SinkRefusal.CapReached && y2.Success && y3.Reason == SinkRefusal.CapReached,
                  "a year later the allowance is back once, and spent again after it: the month is counted with its year");
        }

        // ------------------------------------------------------------------ 6. attempt banks

        private static void CheckSinksBanks(ContentDatabase db)
        {
            BalanceConfig b = db.Balance;
            long brandSeconds = (long)(b.ProvingBrandsRechargeHours * 3600f);
            long echoSeconds = (long)(b.RecitalEchoesRechargeHours * 3600f);

            // A full bank refuses and changes nothing.
            GameSession full = SinkSession(db, 10, 12, 100000, 100000);
            full.Profile.ProvingBrandsBanked = b.ProvingBrandsMax;
            full.Profile.ProvingBrandsClockUnix = SinkBase;
            full.Profile.RecitalEchoesBanked = b.RecitalEchoesMax;
            full.Profile.RecitalEchoesClockUnix = SinkBase;
            string fullJson = SaveSerializer.Serialize(full.Profile);
            SinkResult brandFull = SinkShop.Buy(full, "armory_brand", null, SinkBase + 10);
            SinkResult echoFull = SinkShop.Buy(full, "armory_echo", null, SinkBase + 10);
            Check(brandFull.Reason == SinkRefusal.BankFull && echoFull.Reason == SinkRefusal.BankFull && SaveSerializer.Serialize(full.Profile) == fullJson,
                  "A FULL BANK REFUSES: no Brand and no Echo is sold into a full bank, nothing is charged, the save is byte for byte as it was");

            // Regen that would have filled the bank counts: bank 4 with two intervals elapsed is full.
            GameSession regen = SinkSession(db, 10, 12, 100000, 100000);
            regen.Profile.ProvingBrandsBanked = b.ProvingBrandsMax - 1;
            regen.Profile.ProvingBrandsClockUnix = SinkBase - 2 * brandSeconds;
            Check(SinkShop.Buy(regen, "armory_brand", null, SinkBase).Reason == SinkRefusal.BankFull,
                  "and regen already owed counts: a bank one short with two intervals elapsed is full");

            // One short, then full: exactly one added, the clock snaps at max.
            GameSession one = SinkSession(db, 10, 12, 100000, 100000);
            one.Profile.ProvingBrandsBanked = b.ProvingBrandsMax - 1;
            one.Profile.ProvingBrandsClockUnix = SinkBase - 5000;
            one.Profile.RecitalEchoesBanked = b.RecitalEchoesMax - 1;
            one.Profile.RecitalEchoesClockUnix = SinkBase - 5000;
            SinkResult toMax = SinkShop.Buy(one, "armory_brand", null, SinkBase);
            SinkResult toMaxEcho = SinkShop.Buy(one, "armory_echo", null, SinkBase);
            Check(toMax.Success && toMaxEcho.Success && one.Profile.ProvingBrandsBanked == b.ProvingBrandsMax && one.Profile.RecitalEchoesBanked == b.RecitalEchoesMax
                  && one.Profile.ProvingBrandsClockUnix == SinkBase && one.Profile.RecitalEchoesClockUnix == SinkBase
                  && one.PeekProvingBrands(SinkBase).SecondsToNext == 0 && one.PeekRecitalEchoes(SinkBase).SecondsToNext == 0,
                  "the purchase that fills a bank adds exactly one and snaps the clock to now, like regen does at the cap");
            SinkResult over = SinkShop.Buy(one, "armory_brand", null, SinkBase + 1);
            Check(over.Reason == SinkRefusal.BankFull && one.Profile.ProvingBrandsBanked == b.ProvingBrandsMax,
                  "and the next one is refused: a bank is never above its size");

            // Below the cap the regen remainder survives.
            GameSession part = SinkSession(db, 10, 12, 100000, 100000);
            part.Profile.ProvingBrandsBanked = 2;
            part.Profile.ProvingBrandsClockUnix = SinkBase - 1000;
            part.Profile.RecitalEchoesBanked = 1;
            part.Profile.RecitalEchoesClockUnix = SinkBase - 1000;
            SinkShop.Buy(part, "armory_brand", null, SinkBase);
            SinkShop.Buy(part, "armory_echo", null, SinkBase);
            ProvingBrandsStatus bs = part.PeekProvingBrands(SinkBase);
            RecitalEchoesStatus es = part.PeekRecitalEchoes(SinkBase);
            Check(bs.Banked == 3 && es.Banked == 2 && bs.SecondsToNext == brandSeconds - 1000 && es.SecondsToNext == echoSeconds - 1000,
                  "below the cap the fraction of an interval already regenerated is kept: the next attempt is still "
                  + (brandSeconds - 1000) + " s and " + (echoSeconds - 1000) + " s away");

            // Elapsed regen is folded in before the purchase adds its one.
            GameSession fold = SinkSession(db, 10, 12, 100000, 100000);
            fold.Profile.ProvingBrandsBanked = 0;
            fold.Profile.ProvingBrandsClockUnix = SinkBase - 2 * brandSeconds - 100;
            SinkShop.Buy(fold, "armory_brand", null, SinkBase);
            Check(fold.PeekProvingBrands(SinkBase).Banked == 3 && fold.PeekProvingBrands(SinkBase).SecondsToNext == brandSeconds - 100,
                  "and regen owed is banked first: empty with two intervals and a bit elapsed, one purchase, three banked and the bit kept");
        }

        // ------------------------------------------------------------------ 7. refusals

        private static void CheckSinksRefusals(ContentDatabase db)
        {
            GameSession s = SinkSession(db, 10, 12, 100000, 100000);
            string before = SaveSerializer.Serialize(s.Profile);
            SinkResult unknown = SinkShop.Buy(s, "armory_nothing", null, SinkBase);
            SinkResult nullId = SinkShop.Buy(s, null, null, SinkBase);
            Check(unknown.Reason == SinkRefusal.Unknown && nullId.Reason == SinkRefusal.Unknown && !unknown.Success && SaveSerializer.Serialize(s.Profile) == before,
                  "an item that does not exist is refused as Unknown and the save is byte for byte as it was");

            string[] bad = { null, "", "not_a_hero", SinkChoiceFor(db, "archive_hero") };
            bool badOk = true;
            for (int i = 0; i < bad.Length; i++)
            {
                SinkResult r = SinkShop.Buy(s, "armory_hero", bad[i], SinkBase);
                if (r.Reason != SinkRefusal.BadChoice || r.Success) badOk = false;
            }
            Check(badOk && SaveSerializer.Serialize(s.Profile) == before,
                  "a hero item with no hero, an unknown hero or a hero from the OTHER shop's list is refused as BadChoice, charging nothing");

            // Not enough: one short refuses, exactly enough sells.
            GameSession poor = SinkSession(db, 10, 12, 0, 0);
            long price = SinkPricing.Price(poor, db.Sinks.Item("armory_weapon"));
            poor.Profile.Valor = price - 1;
            string poorJson = SaveSerializer.Serialize(poor.Profile);
            SinkResult short1 = SinkShop.Buy(poor, "armory_weapon", null, SinkBase);
            Check(short1.Reason == SinkRefusal.NotEnough && short1.Price == price && SaveSerializer.Serialize(poor.Profile) == poorJson,
                  "one Valor short is refused as NotEnough, naming the price, and the save is untouched");
            poor.Profile.Valor = price;
            SinkResult exact = SinkShop.Buy(poor, "armory_weapon", null, SinkBase);
            Check(exact.Success && poor.Profile.Valor == 0, "and exactly enough sells and leaves the purse at zero");

            // Valor cannot buy from the Archive and Coda cannot buy from the Armory.
            GameSession cross = SinkSession(db, 10, 12, 100000, 0);
            SinkResult coda = SinkShop.Buy(cross, "archive_hero", SinkChoiceFor(db, "archive_hero"), SinkBase);
            Check(coda.Reason == SinkRefusal.NotEnough, "the Archive is paid in Coda only: a full Valor purse buys nothing there");

            // Locked: the Proving and the Recital open at their stages.
            bool onboardingWas = OnboardingSwitch.Enabled;
            try
            {
                OnboardingSwitch.Enabled = true;
                GameSession fresh = new GameSession(db, GameSession.CreateNewProfile(db, SinkBase));
                fresh.Profile.Valor = 100000;
                fresh.Profile.Coda = 100000;
                string freshJson = SaveSerializer.Serialize(fresh.Profile);
                bool tabled = FeatureGate.UnlockStage(db, Feature.Proving) != null && FeatureGate.UnlockStage(db, Feature.Recital) != null;
                SinkResult lockedA = SinkShop.Buy(fresh, "armory_weapon", null, SinkBase);
                SinkResult lockedB = SinkShop.Buy(fresh, "archive_hero", SinkChoiceFor(db, "archive_hero"), SinkBase);
                SinkResult lockedC = SinkShop.Buy(fresh, "armory_brand", null, SinkBase);
                Check(tabled && lockedA.Reason == SinkRefusal.Locked && lockedB.Reason == SinkRefusal.Locked && lockedC.Reason == SinkRefusal.Locked
                      && SaveSerializer.Serialize(fresh.Profile) == freshJson,
                      "BEFORE THE PROVING AND THE RECITAL OPEN, THE SHOPS ARE LOCKED: every item is refused as Locked and nothing is written");
                SinkShopView lockedView = SinkShop.View(fresh, SinkShopId.Armory, SinkBase);
                bool allLocked = lockedView.Active && lockedView.Items.Count == 9;
                for (int i = 0; i < lockedView.Items.Count; i++) allLocked &= lockedView.Items[i].Blocked == SinkRefusal.Locked;
                Check(allLocked, "and the shelf says Locked for each of its items");
            }
            finally { OnboardingSwitch.Enabled = onboardingWas; }

            // The shelf a screen draws.
            GameSession shelf = SinkSession(db, 10, 10, 1000, 5);
            SinkShopView armory = SinkShop.View(shelf, SinkShopId.Armory, SinkBase);
            SinkShopView archive = SinkShop.View(shelf, SinkShopId.Archive, SinkBase);
            SinkItemView weapon = null, brand = null, hero = null;
            for (int i = 0; i < armory.Items.Count; i++)
            {
                if (armory.Items[i].Item.Id == "armory_weapon") weapon = armory.Items[i];
                if (armory.Items[i].Item.Id == "armory_brand") brand = armory.Items[i];
                if (armory.Items[i].Item.Id == "armory_hero") hero = armory.Items[i];
            }
            string shelfJson = SaveSerializer.Serialize(shelf.Profile);
            Check(armory.Active && archive.Active && armory.Purse == 1000 && archive.Purse == 5 && armory.Day == 250 && archive.Day == 80
                  && weapon != null && weapon.Price == 1500 && weapon.Blocked == SinkRefusal.NotEnough && weapon.CapLeft == 3 && weapon.CapLimit == 3
                  && brand != null && brand.Price == 65 && brand.Blocked == SinkRefusal.None && brand.Banked == 0 && brand.BankMax == db.Balance.ProvingBrandsMax
                  && hero != null && hero.Blocked == SinkRefusal.NotEnough && archive.Items.Count == 1 && archive.Items[0].Blocked == SinkRefusal.NotEnough
                  && SaveSerializer.Serialize(shelf.Profile) == shelfJson,
                  "THE SHELF: the view reports purse, one day of income, each price, what blocks it, what is left of its cap and the bank, and reading it writes nothing");
            SinkShop.Buy(shelf, "armory_brand", null, SinkBase);
            SinkItemView after = null;
            SinkShopView again = SinkShop.View(shelf, SinkShopId.Armory, SinkBase);
            for (int i = 0; i < again.Items.Count; i++) if (again.Items[i].Item.Id == "armory_brand") after = again.Items[i];
            Check(after != null && after.CapLeft == 2 && after.Banked == 1 && after.ResetUnix == shelf.DayStartUnix(SinkBase) + 86400L,
                  "and after a purchase the shelf shows two of three left, one banked, and when the day resets");
        }

        // ------------------------------------------------------------------ 8. nothing random

        private static void CheckSinksNothingRandom(string contentDir)
        {
            string root = Path.GetFullPath(Path.Combine(contentDir, "..", "..", ".."));
            string core = Path.Combine(root, "Assets", "Scripts", "Core");
            string[] files =
            {
                Path.Combine(core, "Data", "SinksConfig.cs"), Path.Combine(core, "Model", "SinksState.cs"),
                Path.Combine(core, "Progression", "SinkPricing.cs"), Path.Combine(core, "Progression", "SinkShop.cs")
            };
            string[] banned = { "Random", "DeterministicRng", "ArmorRoller", "Summon(", "Convert(", "GrantRandomUnownedRelic", "Guid", "DateTime.Now", "UtcNow" };
            string[] purses = { ".Gold", ".Shards", ".RelicDust", "Slag" };
            List<string> found = new List<string>();
            bool present = true;
            for (int i = 0; i < files.Length; i++)
            {
                if (!File.Exists(files[i])) { present = false; continue; }
                string text = File.ReadAllText(files[i]);
                for (int b = 0; b < banned.Length; b++)
                    if (text.IndexOf(banned[b], StringComparison.Ordinal) >= 0) found.Add(Path.GetFileName(files[i]) + " names " + banned[b]);
                for (int b = 0; b < purses.Length; b++)
                    if (text.IndexOf(purses[b], StringComparison.Ordinal) >= 0) found.Add(Path.GetFileName(files[i]) + " names " + purses[b]);
            }
            Check(present && found.Count == 0,
                  "NOTHING RANDOM, NOTHING BOUGHT WITH ANYTHING BUT VALOR OR CODA: the four sink source files name no random source, no roller, no summon, "
                  + "no clock read and no Gold, Shards, Dust or Slag" + (found.Count > 0 ? " - " + string.Join("; ", found.ToArray()) : ""));
        }

        // ------------------------------------------------------------------ 9. save

        private static void CheckSinksSave(ContentDatabase db)
        {
            Check(SaveSerializer.CurrentVersion == 23 && SaveSerializer.BaseVersion == 22,
                  "THE VERSIONS: this build reads up to 23 and a save with no sinks block is stamped 22");

            GameSession s = SinkSession(db, 10, 12, 100000, 100000);
            string emptyJson = SaveSerializer.Serialize(s.Profile);
            Check(JsonValue.Parse(emptyJson).GetInt("version", 0) == 22 && !emptyJson.Contains("\"sinks\"") && SaveSerializer.VersionOf(s.Profile) == 22
                  && SaveSerializer.VersionOfJson(emptyJson) == 22,
                  "a profile that bought nothing writes no sinks key and is stamped version 22: the document the build before wrote");

            SinkShop.Buy(s, "armory_weapon", null, SinkBase);
            SinkShop.Buy(s, "armory_brand", null, SinkBase + 1);
            SinkShop.Buy(s, "armory_brand", null, SinkBase + 2);
            SinkShop.Buy(s, "armory_hero", SinkChoiceFor(db, "armory_hero"), SinkBase + 3);
            string json = SaveSerializer.Serialize(s.Profile);
            string error;
            PlayerProfile back = SaveSerializer.Deserialize(json, db, out error);
            SinksState a = s.Profile.Sinks, c = back == null ? null : back.Sinks;
            bool same = c != null && a.DayUnix == c.DayUnix && a.MonthKey == c.MonthKey && a.Daily.Count == c.Daily.Count && a.Monthly.Count == c.Monthly.Count
                        && SinksState.CountOf(c.Daily, "brand") == 2 && SinksState.CountOf(c.Monthly, "armor") == 1 && SinksState.CountOf(c.Monthly, "armoryHero") == 1;
            Check(error == null && same && SaveSerializer.Serialize(back) == json,
                  "SAVE 23 ROUND TRIP: a day's Brands and a month's armor and hero copy save, load back identical and save again byte for byte ("
                  + json.Length + " characters)");
            Check(JsonValue.Parse(json).GetInt("version", 0) == 23 && SaveSerializer.VersionOfJson(json) == 23 && json.Contains("\"sinks\""),
                  "and it is stamped version 23, with the block under its own key");

            // The version 22 save, with and without the key, loads with an empty or the stored block.
            PlayerProfile old = SaveSerializer.Deserialize(emptyJson, db, out error);
            Check(old != null && error == null && old.Sinks.IsEmpty && SaveSerializer.Serialize(old) == emptyJson,
                  "A VERSION 22 SAVE LOADS with an empty block and saves again as exactly the same document at 22");

            // Damaged blocks read empty, never fail the load.
            string[] damaged = { "\"sinks\": \"x\"", "\"sinks\": 7", "\"sinks\": []", "\"sinks\": { \"daily\": 3, \"day\": \"x\", \"monthly\": { } }" };
            int fine = 0;
            for (int i = 0; i < damaged.Length; i++)
            {
                string doc = emptyJson.TrimEnd();
                int last = doc.LastIndexOf('}');
                string withBlock = doc.Substring(0, last).TrimEnd() + ",\n  " + damaged[i] + "\n}";
                PlayerProfile p = SaveSerializer.Deserialize(withBlock, db, out error);
                if (p != null && error == null && p.Sinks.IsEmpty) fine++;
            }
            Check(fine == damaged.Length, "damaged blocks (a string, a number, an array, wrong types inside) read as empty and the save still loads (" + fine + " of " + damaged.Length + ")");

            // Clamps: counts to 0..1000, stamps to 0 or more, empty and repeated keys dropped.
            JsonValue block = JsonValue.NewObject();
            block.Set("day", -50L);
            block.Set("month", -1L);
            JsonValue daily = JsonValue.NewArray();
            JsonValue d1 = JsonValue.NewObject(); d1.Set("key", "brand"); d1.Set("n", 99999); daily.Add(d1);
            JsonValue d2 = JsonValue.NewObject(); d2.Set("key", "brand"); d2.Set("n", 3); daily.Add(d2);
            JsonValue d3 = JsonValue.NewObject(); d3.Set("key", ""); d3.Set("n", 3); daily.Add(d3);
            JsonValue d4 = JsonValue.NewObject(); d4.Set("key", "echo"); d4.Set("n", -4); daily.Add(d4);
            block.Set("daily", daily);
            SinksState clamped = SinksState.Read(block);
            Check(clamped.DayUnix == 0 && clamped.MonthKey == 0 && clamped.Daily.Count == 1 && clamped.Daily[0].Count == SinksState.CountMax,
                  "and the numbers are clamped to what the game could have written: negative stamps to zero, counts to " + SinksState.CountMax
                  + ", a negative or repeated or nameless counter dropped");

            // With the switch off the block is still read and written back untouched.
            bool was = SinksSwitch.Enabled;
            try
            {
                SinksSwitch.Enabled = false;
                PlayerProfile off = SaveSerializer.Deserialize(json, db, out error);
                Check(off != null && SaveSerializer.Serialize(off) == json,
                      "with the switch off a saved block is still read and written back untouched: turning it off costs a player nothing");
            }
            finally { SinksSwitch.Enabled = was; }
        }

        // ------------------------------------------------------------------ 10. not hashed

        private static void CheckSinksNotHashed(ContentDatabase db, string contentDir)
        {
            string[] hashed = { "heroes", "enemies", "campaign", "balance", "relics", "events", "armor", "floor-conditions" };
            string[] texts = new string[hashed.Length];
            for (int i = 0; i < hashed.Length; i++)
            {
                string path = Path.Combine(contentDir, hashed[i] + ".json");
                texts[i] = File.Exists(path) ? File.ReadAllText(path) : null;
            }
            Check(ContentDatabase.VersionOf(texts) == db.ContentVersion,
                  "SINKS.JSON IS NOT HASHED: ContentVersion is exactly the hash of the eight fight files (" + db.ContentVersion + "), and sinks.json is not among them");

            string text = File.ReadAllText(Path.Combine(contentDir, "sinks.json"));
            ContentDatabase other = LoadContent(contentDir);
            string version = other.ContentVersion;
            other.LoadSinks(text.Replace("\"days\": 6,", "\"days\": 7,"));
            long was = SinkPriceAt(db, 10, 0, "armory_weapon");
            GameSession retuned = SinkSession(other, 10, 0, 0, 0);
            long now = SinkPricing.Price(retuned, other.Sinks.Item("armory_weapon"));
            Check(was == 1500 && now == 1750 && other.ContentVersion == version && version == db.ContentVersion,
                  "so retuning a price moves the price (" + was + " to " + now + ") and not ContentVersion, which keeps every recorded guild dungeon replay");

            other.LoadSinks(null);
            Check(!other.Sinks.IsUsable && other.ContentVersion == version, "and removing the file changes nothing the version reads");
        }

        // ------------------------------------------------------------------ 11. rollback

        private static GameSession SinkScript(ContentDatabase db)
        {
            GameSession s = SinkSession(db, 4, 6, 900, 90);
            s.GrantProvingResult(true, SinkBase);
            s.GrantProvingResult(false, SinkBase + 10);
            s.GrantRecitalResult(7, true, SinkBase + 20);
            s.TryStartProvingAttempt(SinkBase + 30);
            s.TryStartRecitalAttempt(SinkBase + 40);
            return s;
        }

        private static void CheckSinksOff(ContentDatabase db)
        {
            bool was = SinksSwitch.Enabled;
            try
            {
                SinksSwitch.Enabled = false;
                GameSession off = SinkScript(db);
                string offJson = SaveSerializer.Serialize(off.Profile);
                long t = SinkBase + 100;

                bool refused = !SinkShop.Active(off);
                for (int i = 0; i < db.Sinks.Items.Count; i++)
                {
                    SinkResult r = SinkShop.Buy(off, db.Sinks.Items[i].Id, SinkChoiceFor(db, db.Sinks.Items[i].Id), t);
                    if (r.Success || r.Reason != SinkRefusal.Off) refused = false;
                }
                SinkShopView view = SinkShop.View(off, SinkShopId.Armory, t);
                Check(refused && !view.Active && view.Items.Count == 0 && off.Profile.Sinks.IsEmpty && SaveSerializer.Serialize(off.Profile) == offJson,
                      "ROLLBACK: with the switch off every purchase is refused as Off, the shelf is empty, nothing is charged and nothing is recorded");

                JsonValue root = JsonValue.Parse(offJson);
                List<string> keys = new List<string>();
                foreach (string key in root.Keys) keys.Add(key);
                Check(root.GetInt("version", 0) == 22 && !keys.Contains("sinks") && keys.Count == 40,
                      "and the save is version 22 with the 40 keys main writes (" + keys.Count + " keys, no sinks key)");

                // The same session with a block on file differs from the off save in the version and the one key, and nothing else.
                SinksSwitch.Enabled = true;
                GameSession on = SinkScript(db);
                SinksState.Bump(on.Profile.Sinks.Daily, "brand");
                on.Profile.Sinks.DayUnix = on.DayStartUnix(SinkBase);
                string onJson = SaveSerializer.Serialize(on.Profile);
                JsonValue onRoot = JsonValue.Parse(onJson);
                JsonValue rebuilt = JsonValue.NewObject();
                foreach (string key in onRoot.Keys)
                {
                    if (key == "sinks") continue;
                    if (key == "version") { rebuilt.Set(key, 22); continue; }
                    rebuilt.Set(key, onRoot[key]);
                }
                Check(onRoot.GetInt("version", 0) == 23 && rebuilt.ToJson(true) == offJson,
                      "THE OFF SAVE IS MAIN'S DOCUMENT: the same session with a block on file is the off save plus the sinks key and version 23, byte for byte");

                // A new profile and an existing save load and re-save as on main, with the switch off.
                SinksSwitch.Enabled = false;
                PlayerProfile fresh = GameSession.CreateNewProfile(db, SinkBase);
                string freshJson = SaveSerializer.Serialize(fresh);
                string error;
                PlayerProfile freshBack = SaveSerializer.Deserialize(freshJson, db, out error);
                PlayerProfile existing = SaveSerializer.Deserialize(offJson, db, out error);
                Check(freshBack != null && SaveSerializer.Serialize(freshBack) == freshJson && JsonValue.Parse(freshJson).GetInt("version", 0) == 22
                      && existing != null && SaveSerializer.Serialize(existing) == offJson,
                      "and a new profile and an existing version 22 save both load and save again unchanged, still at 22");
            }
            finally { SinksSwitch.Enabled = was; }
        }

        // ------------------------------------------------------------------ 13. combat

        private static void CheckSinksNoCombatChange(ContentDatabase db, string contentDir)
        {
            ContentDatabase other = LoadContent(contentDir);
            other.LoadSinks(null);
            bool same = true;
            int[] stages = { 0, Math.Min(5, db.AllStages.Count - 1), Math.Min(20, db.AllStages.Count - 1) };
            for (int i = 0; i < stages.Length; i++)
            {
                GameSession with = new GameSession(db, GameSession.CreateNewProfile(db, SinkBase));
                GameSession without = new GameSession(other, GameSession.CreateNewProfile(other, SinkBase));
                string a = FingerprintOf(CombatSimulation.Run(with.BuildBattleRequest(CampaignWaveGenerator.BuildBattle(db.AllStages[stages[i]], 1, 1), 4242)));
                string b = FingerprintOf(CombatSimulation.Run(without.BuildBattleRequest(CampaignWaveGenerator.BuildBattle(other.AllStages[stages[i]], 1, 1), 4242)));
                if (a != b) same = false;
            }
            Check(same, "COMBAT DOES NOT MOVE: three fixed battles are the same event for event with sinks.json loaded and with the whole file removed");

            // A purchase of an armor piece changes the bag, never the fight of a team that has not equipped it.
            GameSession bought = new GameSession(db, GameSession.CreateNewProfile(db, SinkBase));
            string before = FingerprintOf(CombatSimulation.Run(bought.BuildBattleRequest(CampaignWaveGenerator.BuildBattle(db.AllStages[0], 1, 1), 4242)));
            bought.Profile.Valor = 100000;
            bought.Profile.ProvingHighestStanding = 10;
            for (int i = 0; i < db.AllStages.Count; i++) bought.Profile.ClearedStageIds.Add(db.AllStages[i].Id);
            bought.Profile.StagesCleared = CampaignProgress.CountCleared(bought.Profile, db);
            SinkShop.Buy(bought, "armory_weapon", null, SinkBase);
            string after = FingerprintOf(CombatSimulation.Run(bought.BuildBattleRequest(CampaignWaveGenerator.BuildBattle(db.AllStages[0], 1, 1), 4242)));
            Check(before == after, "and buying a preset leaves the team that was fighting fighting the same fight: a purchase adds to the bag and nothing is worn until the player wears it");
        }

        // ------------------------------------------------------------------ 12. wiring

        /// <summary>
        /// WIRING TRIPWIRES, not behavioural proof: the shop screens, the router and the capture tool
        /// cannot run in the harness, so this reads their source. It fails if the screens stop being
        /// built behind the switch, if a door appears outside it, if a shop screen starts to decide
        /// or to sell by any road but SinkShop.Buy, or if a capture target loses its forced switch.
        /// What the screens then look like is for the Play queue.
        /// </summary>
        private static void CheckSinksWiring(string contentDir)
        {
            string root = Path.GetFullPath(Path.Combine(contentDir, "..", "..", ".."));
            string ui = Path.Combine(root, "Assets", "Scripts", "UI");
            string routerPath = Path.Combine(ui, "ScreenRouter.cs");
            string provingPath = Path.Combine(ui, "Screens", "ProvingScreen.cs");
            string recitalPath = Path.Combine(ui, "Screens", "RecitalScreen.cs");
            string shopPath = Path.Combine(ui, "Screens", "SinkShopScreen.cs");
            string armoryPath = Path.Combine(ui, "Screens", "ArmoryScreen.cs");
            string archivePath = Path.Combine(ui, "Screens", "ArchiveScreen.cs");
            string shotsPath = Path.Combine(root, "Assets", "Editor", "PlayModeScreenshot.cs");
            string[] files = { routerPath, provingPath, recitalPath, shopPath, armoryPath, archivePath, shotsPath };
            for (int i = 0; i < files.Length; i++)
                if (!File.Exists(files[i]))
                {
                    Check(false, "the sink screens, router, doors and capture file are where the harness expects them (" + files[i] + ")");
                    return;
                }
            string router = File.ReadAllText(routerPath), proving = File.ReadAllText(provingPath), recital = File.ReadAllText(recitalPath);
            string shop = File.ReadAllText(shopPath), armory = File.ReadAllText(armoryPath), archive = File.ReadAllText(archivePath);
            string shots = File.ReadAllText(shotsPath);

            // The router builds both screens, once each, and only inside the switch.
            int guardAt = router.IndexOf("if (Emberfall.Core.Session.SinksSwitch.Enabled)", StringComparison.Ordinal);
            int armoryAt = router.IndexOf("Create<Screens.ArmoryScreen>()", StringComparison.Ordinal);
            int archiveAt = router.IndexOf("Create<Screens.ArchiveScreen>()", StringComparison.Ordinal);
            Check(guardAt > 0 && armoryAt > guardAt && archiveAt > guardAt
                  && router.IndexOf("Create<Screens.ArmoryScreen>()", armoryAt + 1, StringComparison.Ordinal) < 0
                  && router.IndexOf("Create<Screens.ArchiveScreen>()", archiveAt + 1, StringComparison.Ordinal) < 0,
                  "WIRING (reads source): the router builds the Armory and the Archive once each, and only inside the sinks switch");

            // Each door is drawn only inside the switch and asks the gate with its own id.
            int provingGuard = proving.IndexOf("if (SinksSwitch.Enabled)", StringComparison.Ordinal);
            int recitalGuard = recital.IndexOf("if (SinksSwitch.Enabled)", StringComparison.Ordinal);
            Check(provingGuard > 0 && proving.IndexOf("\"proving.Armory\"", StringComparison.Ordinal) > provingGuard
                  && proving.IndexOf("ScreenId.Armory", StringComparison.Ordinal) > provingGuard
                  && recitalGuard > 0 && recital.IndexOf("\"recital.Archive\"", StringComparison.Ordinal) > 0
                  && recital.IndexOf("ScreenId.Archive", StringComparison.Ordinal) > 0
                  && recital.IndexOf("BuildArchiveDoor();", recitalGuard, StringComparison.Ordinal) > recitalGuard,
                  "and each door, the Armory's on the Proving and the Archive's on the Recital, is drawn only inside the switch, and asks the gate by its own id");

            // The shop screen sells by SinkShop.Buy and reads by SinkShop.View, and by nothing else.
            System.Text.RegularExpressions.MatchCollection calls = System.Text.RegularExpressions.Regex.Matches(shop, @"SinkShop\.(\w+)");
            bool onlyTwo = calls.Count >= 2;
            bool view = false, buy = false;
            for (int i = 0; i < calls.Count; i++)
            {
                string name = calls[i].Groups[1].Value;
                if (name == "View") view = true;
                else if (name == "Buy") buy = true;
                else onlyTwo = false;
            }
            string[] banned = { "SinkPricing", "GrantHeroDirect", "OwnedArmor", "AddRecitalEcho", "AddProvingBrand", "Profile.Sinks", "Random" };
            List<string> found = new List<string>();
            for (int i = 0; i < banned.Length; i++) if (shop.IndexOf(banned[i], StringComparison.Ordinal) >= 0) found.Add(banned[i]);
            bool writesPurse = System.Text.RegularExpressions.Regex.IsMatch(shop, @"\.(Valor|Coda|Gold|Shards|RelicDust|Slag)\s*[-+]?=[^=]");
            Check(onlyTwo && view && buy && found.Count == 0 && !writesPurse,
                  "WIRING (reads source): the shop screen calls only SinkShop.View and SinkShop.Buy, prices nothing itself, grants nothing itself and writes no purse"
                  + (found.Count > 0 ? " - names " + string.Join(", ", found.ToArray()) : ""));
            Check(armory.IndexOf("SinkShop.", StringComparison.Ordinal) < 0 && archive.IndexOf("SinkShop.", StringComparison.Ordinal) < 0
                  && armory.IndexOf("SinkShopId.Armory", StringComparison.Ordinal) > 0 && archive.IndexOf("SinkShopId.Archive", StringComparison.Ordinal) > 0,
                  "and the two screens are thin: each names its shop and its words and calls nothing");

            // The capture targets exist, force the switch and put it back.
            string[] targets = { "Armory", "ArmoryBought", "ArmorySoldOut", "ArmoryPicker", "Archive", "ArchiveBought", "ArchiveSoldOut",
                                 "ArchivePicker", "ProvingSinksOn", "RecitalSinksOn", "ProvingSinksOff", "RecitalSinksOff" };
            List<string> missing = new List<string>();
            for (int i = 0; i < targets.Length; i++)
                if (shots.IndexOf("Begin(\"Screen:" + targets[i] + "\")", StringComparison.Ordinal) < 0
                    || shots.IndexOf("case \"" + targets[i] + "\":", StringComparison.Ordinal) < 0) missing.Add(targets[i]);
            Check(missing.Count == 0 && shots.Contains("SinksSwitch.Enabled = true") && shots.Contains("SinksSwitch.Enabled = SessionState.GetBool(SinksWasKey, false)")
                  && shots.Contains("SessionState.SetInt(SinksForceKey, SinksForceFor(target))"),
                  "WIRING (reads source): every sink capture target exists, forces the switch ON for its own run and puts it back after"
                  + (missing.Count > 0 ? " - missing " + string.Join(", ", missing.ToArray()) : ""));
            Check(shots.Contains("EditorScratchIdentity.IsScratch") && shots.Contains("SinkShop.Buy(session")
                  && shots.Contains("else if (force == 2) SinksSwitch.Enabled = false;"),
                  "and the seed buys through SinkShop.Buy on the scratch identity only, and the two rollback targets force the switch OFF");

            string telemetryPath = Path.Combine(root, "Assets", "Scripts", "Game", "Telemetry", "TelemetryService.cs");
            string telemetry = File.Exists(telemetryPath) ? File.ReadAllText(telemetryPath) : "";
            Check(telemetry.Contains("SessionStartPayload(Application.platform.ToString(), SaveSerializer.BaseVersion)"),
                  "WIRING (reads source): session_start reports BaseVersion (22), exactly what main sent, with the switch off or on");

            // The cloud push claims the document's own version, so a save with no sinks block says 22.
            string cloudPath = Path.Combine(root, "Assets", "Scripts", "Game", "Cloud", "CloudSaveService.cs");
            string cloud = File.Exists(cloudPath) ? File.ReadAllText(cloudPath) : "";
            int versioned = 0, from = 0;
            while ((from = cloud.IndexOf("Push(localJson, SaveSerializer.VersionOfJson(localJson)", from, StringComparison.Ordinal)) >= 0) { versioned++; from++; }
            Check(versioned == 2 && cloud.IndexOf("Push(localJson, SaveSerializer.CurrentVersion", StringComparison.Ordinal) < 0,
                  "WIRING (reads source): both cloud pushes claim the document's own version (VersionOfJson), never the highest this build reads: a flag-off push sends 22");
        }
    }
}
