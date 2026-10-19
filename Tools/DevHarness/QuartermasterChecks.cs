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
    /// THE QUARTERMASTER (Docs/DESIGN-QUARTERMASTER.md): a daily stock of rolled armor, one piece per
    /// slot, sold for Gold at a price in hours of the player's own idle Gold.
    ///
    /// Two halves. The SIZING is the measurement DESIGN-SHOP.md asks for before anything is built: the
    /// ending Gold of the reference walk per chapter, read off the same simulated player the curve band
    /// is graded on, against what a day's stock costs. The CHECKS are one per rule: the content, the
    /// price, the stock, the sale, every refusal leaving the save untouched, the clock, the save, the
    /// file staying out of ContentVersion, combat not moving, the switch, and the wiring.
    ///
    /// Every purchase goes through Quartermaster.Buy with an explicit clock; a check sets a purse only
    /// to name the situation it wants.
    /// </summary>
    internal static partial class Program
    {
        // ================================================================== the sizing

        /// <summary>One chapter of the reference walk, as the Quartermaster's sizing reads it.</summary>
        private sealed class QmChapterReading
        {
            public int Chapter;
            public int Stages;
            public StageDef Last;
            public long GoldHeldAtEnd;
            public long GoldEarned;
            public long GoldSpentOnLevels;
            public long Seconds;
            public long IdleGoldPerHour;
        }

        /// <summary>The walk read per chapter: Gold earned, Gold spent on levels, Gold left in hand,
        /// the days the chapter took and the frontier's idle Gold per hour. Reads the run
        /// RunCampaignBalancePass already walked; costs no battle.</summary>
        private static List<QmChapterReading> ReadQuartermasterChapters(ContentDatabase db, EconomyRun run)
        {
            List<QmChapterReading> chapters = new List<QmChapterReading>();
            int count = run.Stages.Count;
            for (int i = 0; i < count; i++)
            {
                EconomyStageOutcome o = run.Stages[i];
                int ch = o.Stage.ChapterNumber;
                QmChapterReading c = null;
                for (int k = 0; k < chapters.Count; k++) if (chapters[k].Chapter == ch) c = chapters[k];
                if (c == null)
                {
                    c = new QmChapterReading();
                    c.Chapter = ch;
                    chapters.Add(c);
                }
                bool lastOfChapter = i + 1 >= count || run.Stages[i + 1].Stage.ChapterNumber != ch;
                c.Stages++;
                c.Seconds += o.ElapsedSeconds;
                c.Last = o.Stage;
                if (lastOfChapter)
                    c.GoldHeldAtEnd = i + 1 < count ? run.Stages[i + 1].EntryGold : run.Profile.Gold;
            }
            for (int k = 0; k < chapters.Count; k++)
            {
                QmChapterReading c = chapters[k];
                int firstIdx = -1, nextIdx = -1;
                for (int i = 0; i < count; i++)
                {
                    int ch = run.Stages[i].Stage.ChapterNumber;
                    if (ch == c.Chapter && firstIdx < 0) firstIdx = i;
                    if (ch > c.Chapter && nextIdx < 0) nextIdx = i;
                }
                EconomyLedger from = run.Stages[firstIdx].EntryLedger;
                EconomyLedger to = nextIdx >= 0 ? run.Stages[nextIdx].EntryLedger : run.Ledger;
                c.GoldEarned = to.GoldTotal - from.GoldTotal;
                c.GoldSpentOnLevels = to.GoldSpentOnLevels - from.GoldSpentOnLevels;
                c.IdleGoldPerHour = c.Last != null ? (long)c.Last.IdleGoldPerHour : 0;
            }
            return chapters;
        }

        /// <summary>The expected hours of idle Gold in one day's stock when the player's frontier is
        /// this stage: the temper weights a drop uses there (ArmorRoller.RollTemper) against the hours
        /// quartermaster.json charges, summed over the stock's slots.</summary>
        private static double ExpectedStockHours(ContentDatabase db, int frontierIndex)
        {
            double total = 0.0, hours = 0.0;
            for (int i = 0; i < db.Armor.Tempers.Count; i++)
            {
                ArmorTemperDef t = db.Armor.Tempers[i];
                if ((int)t.Temper > (int)ArmorInfo.MaxDropTemper) continue;
                if (t.DropWeight <= 0 && t.DropWeightPerStageIndex == 0f) continue;
                double w = t.DropWeight + t.DropWeightPerStageIndex * Math.Max(0, frontierIndex);
                w = w < 1.0 ? 1.0 : Math.Round(w, MidpointRounding.AwayFromZero);
                total += w;
                hours += w * db.Quartermaster.HoursOf(t.Temper);
            }
            return total > 0.0 ? db.Quartermaster.Slots.Count * hours / total : 0.0;
        }

        /// <summary>The sizing alone, for a pass iterating on prices: --check QuartermasterSizing.</summary>
        private static void QuartermasterSizing(ContentDatabase db)
        {
            ReportQuartermasterGold(db, SimulateEconomy(db, true));
        }

        /// <summary>The Gold the reference walk holds when each chapter ends, and what a chapter's
        /// surplus is against a day's stock. DESIGN-SHOP.md section 1's first step: log ending Gold
        /// before the shop is built, so its prices are sized from a number.</summary>
        private static void ReportQuartermasterGold(ContentDatabase db, EconomyRun run)
        {
            Line("QUARTERMASTER SIZING (DESIGN-SHOP.md section 1: ending Gold per chapter on the reference walk, against a day's stock)");
            Line("  chapter  stages   days   goldEarned  spentOnLevels   surplus  heldAtEnd  idleGold/h  surplusHours  stockHours  stocksPerChapter");
            List<QmChapterReading> chapters = ReadQuartermasterChapters(db, run);
            bool early = true, late = true;
            for (int i = 0; i < chapters.Count; i++)
            {
                QmChapterReading c = chapters[i];
                long surplus = c.GoldEarned - c.GoldSpentOnLevels;
                float days = c.Seconds / 86400f;
                double surplusHours = c.IdleGoldPerHour > 0 ? surplus / (double)c.IdleGoldPerHour : 0.0;
                double stockHours = ExpectedStockHours(db, c.Last != null ? c.Last.GlobalIndex : 0);
                double stocks = stockHours > 0.0 ? surplusHours / stockHours : 0.0;
                Line("  " + c.Chapter.ToString().PadLeft(7)
                     + c.Stages.ToString().PadLeft(8)
                     + Fmt(days).PadLeft(8)
                     + c.GoldEarned.ToString().PadLeft(13)
                     + c.GoldSpentOnLevels.ToString().PadLeft(15)
                     + surplus.ToString().PadLeft(10)
                     + c.GoldHeldAtEnd.ToString().PadLeft(11)
                     + c.IdleGoldPerHour.ToString().PadLeft(12)
                     + (Fmt((float)surplusHours) + "h").PadLeft(14)
                     + (Fmt((float)stockHours) + "h").PadLeft(12)
                     + Fmt((float)stocks).PadLeft(18));
                // Where the level cap has closed the usual sink (chapters 4 to 6), a chapter's surplus
                // buys roughly one day's stock. Before it, the Gold is level fuel and a stock is far out of reach.
                if (c.Chapter >= 4 && (stocks < 0.5 || stocks > 2.5)) late = false;
                if (c.Chapter <= 3 && stocks > 0.35) early = false;
            }
            Check(late, "a chapter's surplus Gold buys roughly one day's stock (0.5 to 2.5 stocks) in chapters 4 to 6, where the level cap has closed the usual sink");
            Check(early, "and in chapters 1 to 3, where Gold is level fuel, a chapter's whole surplus buys well under half a day's stock (the shop cannot compete with levelling)");
            Line("");
        }

        // ================================================================== the checks

        private static void CheckQuartermaster(ContentDatabase db, string contentDir)
        {
            Line("");
            Line("QUARTERMASTER (Docs/DESIGN-QUARTERMASTER.md: a daily stock of rolled armor for Gold, priced in hours of idle Gold)");
            bool was = QuartermasterSwitch.Enabled;
            try
            {
                QuartermasterSwitch.Enabled = true;
                CheckQuartermasterContent(db);
                CheckQuartermasterPrice(db);
                CheckQuartermasterStock(db);
                CheckQuartermasterSale(db);
                CheckQuartermasterRefusals(db);
                CheckQuartermasterClock(db);
                CheckQuartermasterSave(db);
                CheckQuartermasterNotHashed(db, contentDir);
                CheckQuartermasterNoCombatChange(db, contentDir);
                CheckQuartermasterOff(db);
                CheckQuartermasterWiring(contentDir);
            }
            finally { QuartermasterSwitch.Enabled = was; }
            Check(QuartermasterSwitch.Enabled == was, "the switch is put back as the run found it (quartermaster " + (was ? "on" : "off") + ")");
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A session with the first `cleared` stages of the campaign cleared, so the armor bag
        /// is open and the frontier is where the check names it.</summary>
        private static GameSession QmSession(ContentDatabase db, int cleared, long gold)
        {
            PlayerProfile p = GameSession.CreateNewProfile(db, SinkBase);
            for (int i = 0; i < cleared && i < db.AllStages.Count; i++) p.ClearedStageIds.Add(db.AllStages[i].Id);
            p.StagesCleared = CampaignProgress.CountCleared(p, db);
            p.Gold = gold;
            return new GameSession(db, p);
        }

        private static string QmSnapshot(GameSession s) { return SaveSerializer.Serialize(s.Profile); }

        private static string QmStockText(GameSession s)
        {
            return s.Profile.Quartermaster.Slots.Count == 0 ? "" : JsonValue.NewObject().Set("q", s.Profile.Quartermaster.Write()).ToJson(true);
        }

        // ------------------------------------------------------------------ 1. content

        private static void CheckQuartermasterContent(ContentDatabase db)
        {
            QuartermasterConfig c = db.Quartermaster;
            Check(c.IsUsable && c.Warnings.Count == 0 && c.Slots.Count == 6,
                  "the shipped quartermaster.json loads clean: " + c.Slots.Count + " slots, " + c.Hours.Count + " priced tempers, no warnings");
            bool oneEach = true;
            ArmorSlot[] all = ArmorInfo.AllSlots;
            for (int i = 0; i < all.Length; i++) if (!c.Slots.Contains(all[i])) oneEach = false;
            Check(oneEach && c.HoursOf(ArmorTemper.Rough) > 0.0 && c.HoursOf(ArmorTemper.Honed) > c.HoursOf(ArmorTemper.Rough),
                  "it stocks one piece of every armor slot, and a better temper costs more hours (" + c.HoursOf(ArmorTemper.Rough) + " then " + c.HoursOf(ArmorTemper.Honed) + ")");
            Check(c.HoursOf(ArmorTemper.Gilded) == 0.0 && c.HoursOf(ArmorTemper.Radiant) == 0.0,
                  "and prices no temper a drop cannot reach: Gilded and Radiant stay Combine-only");

            QuartermasterConfig bad = QuartermasterConfig.FromJson("{ \"slots\": [\"Weapon\", \"Nope\", \"Weapon\"], \"hours\": { \"Rough\": 10, \"Honed\": -4, \"Gilded\": 50 } }");
            Check(bad.Slots.Count == 1 && bad.Hours.Count == 1 && bad.Warnings.Count == 3,
                  "an unknown slot, a repeated slot's second entry kept once, a zero or negative price and a temper above the drop ceiling are dropped, each with a warning (" + bad.Warnings.Count + ")");
            Check(!QuartermasterConfig.FromJson(null).IsUsable && !QuartermasterConfig.FromJson("{ not json").IsUsable,
                  "a missing or unreadable file sells nothing");
        }

        // ------------------------------------------------------------------ 2. price

        private static void CheckQuartermasterPrice(ContentDatabase db)
        {
            GameSession early = QmSession(db, 12, 0);
            GameSession late = QmSession(db, db.AllStages.Count, 0);
            ArmorPiece rough = ArmorRoller.Roll(db.Armor, ArmorSlot.Weapon, ArmorTemper.Rough, Aspect.Ember, 7);
            ArmorPiece honed = ArmorRoller.Roll(db.Armor, ArmorSlot.Weapon, ArmorTemper.Honed, Aspect.Ember, 7);

            long hourEarly = Quartermaster.HourGold(early), hourLate = Quartermaster.HourGold(late);
            long round = db.Quartermaster.PriceRound;
            long expected = (long)(Math.Ceiling(Math.Ceiling(db.Quartermaster.HoursOf(ArmorTemper.Rough) * hourEarly) / round) * round);
            Check(Quartermaster.PriceOf(early, rough) == expected && expected % round == 0,
                  "A PRICE IS HOURS OF IDLE GOLD: a Rough piece at 1-12 costs " + db.Quartermaster.HoursOf(ArmorTemper.Rough) + " h x " + hourEarly
                  + " = " + Quartermaster.PriceOf(early, rough) + ", rounded up to " + round);
            Check(hourLate > hourEarly * 10 && Quartermaster.PriceOf(late, rough) > Quartermaster.PriceOf(early, rough) * 10,
                  "and it follows the frontier: at 6-12 the same piece costs " + Quartermaster.PriceOf(late, rough) + " (the hour is worth " + hourLate + ")");
            Check(Quartermaster.PriceOf(early, honed) >= Quartermaster.PriceOf(early, rough) * 2,
                  "a Honed piece costs at least twice a Rough one at the same frontier (" + Quartermaster.PriceOf(early, honed) + " against " + Quartermaster.PriceOf(early, rough) + ")");

            // The price is read from the live frontier: clearing a stage changes it, nothing is stored.
            GameSession moving = QmSession(db, 12, 0);
            long before = Quartermaster.PriceOf(moving, rough);
            moving.Profile.ClearedStageIds.Add(db.AllStages[12].Id);
            moving.Profile.StagesCleared = CampaignProgress.CountCleared(moving.Profile, db);
            Check(Quartermaster.PriceOf(moving, rough) > before,
                  "the price is worked out when asked and never stored: clearing the next stage moves it (" + before + " to " + Quartermaster.PriceOf(moving, rough) + ")");
        }

        // ------------------------------------------------------------------ 3. stock

        private static void CheckQuartermasterStock(ContentDatabase db)
        {
            long now = SinkUnix(2025, 10, 9, 8);
            GameSession a = QmSession(db, 36, 0);
            GameSession b = QmSession(db, 36, 0);
            bool rolledA = Quartermaster.Open(a, now), rolledB = Quartermaster.Open(b, now + 600);
            Check(rolledA && rolledB && QmStockText(a) == QmStockText(b) && a.Profile.Quartermaster.Slots.Count == 6,
                  "THE STOCK IS A DAY'S, NOT A PLAYER'S MOMENT: two sessions opening at different times of one reward day get the same six pieces");

            bool onePerSlot = true, ceiling = true, noUid = true;
            for (int i = 0; i < 6; i++)
                if (a.Profile.Quartermaster.Slots[i].Piece.Slot != db.Quartermaster.Slots[i]) onePerSlot = false;
            for (int day = 0; day < 120; day++)
            {
                GameSession s = QmSession(db, db.AllStages.Count, 0);
                Quartermaster.Open(s, now + day * 86400L);
                for (int i = 0; i < s.Profile.Quartermaster.Slots.Count; i++)
                {
                    ArmorPiece p = s.Profile.Quartermaster.Slots[i].Piece;
                    if ((int)p.Temper > (int)ArmorInfo.MaxDropTemper) ceiling = false;
                    if (p.Uid != "" || p.Sealed) noUid = false;
                }
            }
            Check(onePerSlot, "the shelf holds one piece per configured slot, in the file's order");
            Check(ceiling && noUid, "over 120 days no piece is above the drop ceiling (Honed), and no stocked piece has a uid or a Sealed mark before it is bought");

            GameSession tomorrow = QmSession(db, 36, 0);
            Quartermaster.Open(tomorrow, now + 86400L);
            Check(QmStockText(tomorrow) != QmStockText(a), "and the next reward day's stock is a different one");

            // A deeper frontier tilts the temper toward Honed, as it does for a drop.
            int honedEarly = 0, honedLate = 0;
            for (int day = 0; day < 200; day++)
            {
                GameSession e = QmSession(db, 36, 0), l = QmSession(db, db.AllStages.Count, 0);
                Quartermaster.Open(e, now + day * 86400L);
                Quartermaster.Open(l, now + day * 86400L);
                for (int i = 0; i < e.Profile.Quartermaster.Slots.Count; i++)
                    if (e.Profile.Quartermaster.Slots[i].Piece.Temper == ArmorTemper.Honed) honedEarly++;
                for (int i = 0; i < l.Profile.Quartermaster.Slots.Count; i++)
                    if (l.Profile.Quartermaster.Slots[i].Piece.Temper == ArmorTemper.Honed) honedLate++;
            }
            Check(honedLate > honedEarly,
                  "a deeper frontier stocks more Honed pieces, as it drops more (" + honedEarly + " Honed in 200 days at 3-12 against " + honedLate + " at 6-12)");
        }

        // ------------------------------------------------------------------ 4. sale

        private static void CheckQuartermasterSale(ContentDatabase db)
        {
            long now = SinkUnix(2025, 10, 9, 8);
            GameSession s = QmSession(db, 36, 5000000);
            s.Profile.Shards = 77; s.Profile.Slag = 33; s.Profile.RelicDust = 21; s.Profile.Valor = 9; s.Profile.Coda = 8;
            Quartermaster.Open(s, now);
            QuartermasterView view = Quartermaster.View(s, now);
            QuartermasterSlotView first = view.Slots[0];
            ArmorPiece shown = first.Piece;
            int bagBefore = s.Profile.OwnedArmor.Count;
            long goldBefore = s.Profile.Gold;

            QuartermasterResult r = Quartermaster.Buy(s, 0, now);
            Check(r.Success && r.Price == first.Price && s.Profile.Gold == goldBefore - first.Price,
                  "A SALE takes the price from the Gold purse and nothing else: " + first.Price + " Gold for " + shown.Name);
            Check(s.Profile.Shards == 77 && s.Profile.Slag == 33 && s.Profile.RelicDust == 21 && s.Profile.Valor == 9 && s.Profile.Coda == 8
                  && s.Profile.Sealed.IsEmpty,
                  "and no other purse moves: Shards, Slag, Dust, Valor, Coda and the Sealed parts are as they were");
            ArmorPiece got = s.Profile.OwnedArmor.Count == bagBefore + 1 ? s.Profile.OwnedArmor[s.Profile.OwnedArmor.Count - 1] : null;
            Check(got != null && got != shown && got.Uid != "" && !got.Sealed && got.Slot == shown.Slot && got.Temper == shown.Temper
                  && got.Aspect == shown.Aspect && got.DescribeStats() == shown.DescribeStats() && got.Name == shown.Name,
                  "the bag gains an independent copy of exactly the piece on the shelf, with a fresh uid and no Sealed mark");
            QuartermasterView after = Quartermaster.View(s, now);
            Check(after.Slots[0].Sold && after.Slots[0].Blocked == QuartermasterRefusal.Sold && !after.Slots[1].Sold,
                  "the shelf shows that slot SOLD for the rest of the day and the others still for sale");

            // Two sessions, same buys, byte-identical saves.
            GameSession x = QmSession(db, 36, 5000000), y = QmSession(db, 36, 5000000);
            for (int k = 0; k < 2; k++)
            {
                GameSession g = k == 0 ? x : y;
                Quartermaster.Open(g, now);
                Quartermaster.Buy(g, 2, now);
                Quartermaster.Buy(g, 4, now);
            }
            Check(QmSnapshot(x) == QmSnapshot(y), "the same buys on the same day give byte-identical saves");

            Check(s.Profile.FindArmor(got.Uid) == got, "and the bought piece is findable by its uid like any other");

            // Gold is the only thing that buys it, and salvage is a loss: a purchase cannot be turned into profit.
            int salvage = s.SalvageValue(got);
            Check(salvage > 0 && salvage < first.Price,
                  "a bought piece salvages for far less than it cost (" + salvage + " Slag against " + first.Price + " Gold), so the shop is a sink and not a faucet");
        }

        // ------------------------------------------------------------------ 5. refusals

        private static void CheckQuartermasterRefusals(ContentDatabase db)
        {
            long now = SinkUnix(2025, 10, 9, 8);

            // Locked: nothing cleared, and one stage short of 3-12.
            GameSession locked = QmSession(db, 0, 5000000);
            bool rolled = Quartermaster.Open(locked, now);
            string before = QmSnapshot(locked);
            QuartermasterResult lockedBuy = Quartermaster.Buy(locked, 0, now);
            Check(!rolled && !lockedBuy.Success && lockedBuy.Reason == QuartermasterRefusal.Locked && QmSnapshot(locked) == before && !Quartermaster.View(locked, now).Open,
                  "LOCKED: before 3-12 is cleared nothing is stocked, a purchase is refused as Locked and the save is byte for byte as it was");

            // THE LEAD'S GATE: the shop opens when 3-12 is cleared (from 4-1), in its own unlock row, not with the armor bag at 1-12.
            GameSession short35 = QmSession(db, 35, 5000000), open36 = QmSession(db, 36, 5000000);
            StageDef gate = FeatureGate.UnlockStage(db, Feature.Quartermaster);
            Check(gate != null && gate.DisplayCode == "3-12" && !Quartermaster.IsOpen(short35) && Quartermaster.IsOpen(open36)
                  && FeatureGate.IsUnlocked(db, short35.Profile, Feature.Armor) && !Quartermaster.Open(short35, now) && Quartermaster.Open(open36, now),
                  "THE SHOP OPENS AT 4-1: with 35 stages cleared (3-11) the armor bag is open and the Quartermaster is not; clearing 3-12 opens it (unlocks at " + (gate != null ? gate.DisplayCode : "none") + ")");

            // Stale: the shop was not opened today.
            GameSession stale = QmSession(db, 36, 5000000);
            before = QmSnapshot(stale);
            QuartermasterResult staleBuy = Quartermaster.Buy(stale, 0, now);
            Check(!staleBuy.Success && staleBuy.Reason == QuartermasterRefusal.Stale && QmSnapshot(stale) == before && Quartermaster.View(stale, now).Stale,
                  "STALE: a shelf nobody opened today cannot be bought from (Buy never rolls a stock the player was not shown), the save untouched");

            GameSession s = QmSession(db, 36, 5000000);
            Quartermaster.Open(s, now);
            before = QmSnapshot(s);
            QuartermasterResult no1 = Quartermaster.Buy(s, -1, now), no2 = Quartermaster.Buy(s, 99, now);
            Check(!no1.Success && !no2.Success && no1.Reason == QuartermasterRefusal.NoSuchPiece && no2.Reason == QuartermasterRefusal.NoSuchPiece && QmSnapshot(s) == before,
                  "NO SUCH SHELF: an index outside the stock is refused, the save untouched");

            // Not enough: one Gold short.
            long price = Quartermaster.View(s, now).Slots[0].Price;
            s.Profile.Gold = price - 1;
            before = QmSnapshot(s);
            QuartermasterResult poor = Quartermaster.Buy(s, 0, now);
            Check(!poor.Success && poor.Reason == QuartermasterRefusal.NotEnough && poor.Price == price && QmSnapshot(s) == before
                  && Quartermaster.View(s, now).Slots[0].Blocked == QuartermasterRefusal.NotEnough,
                  "NOT ENOUGH: one Gold short is refused, naming the price, the save untouched; and the shelf says so");
            s.Profile.Gold = price;
            QuartermasterResult exact = Quartermaster.Buy(s, 0, now);
            Check(exact.Success && s.Profile.Gold == 0, "and exactly enough sells and leaves the purse at zero");

            // Sold.
            s.Profile.Gold = 5000000;
            before = QmSnapshot(s);
            QuartermasterResult again = Quartermaster.Buy(s, 0, now);
            Check(!again.Success && again.Reason == QuartermasterRefusal.Sold && QmSnapshot(s) == before,
                  "SOLD: the same shelf twice in a day is refused, the save untouched");

            // Sealed Gold never buys a rolled piece.
            GameSession sealedGold = QmSession(db, 36, 0);
            Quartermaster.Open(sealedGold, now);
            long p2 = Quartermaster.View(sealedGold, now).Slots[1].Price;
            SealedGrants.GrantGold(sealedGold, p2 * 10);
            before = QmSnapshot(sealedGold);
            QuartermasterResult sg = Quartermaster.Buy(sealedGold, 1, now);
            Check(!sg.Success && sg.Reason == QuartermasterRefusal.SealedGold && sealedGold.Profile.Gold == p2 * 10 && QmSnapshot(sealedGold) == before,
                  "THE QUARTERMASTER REFUSES SEALED GOLD: " + (p2 * 10) + " Sealed Gold buys nothing, refused as SealedGold, the save byte for byte as it was");
            sealedGold.Profile.Gold += p2;
            long sealedPart = sealedGold.Profile.Sealed.Gold;
            QuartermasterResult mix = Quartermaster.Buy(sealedGold, 1, now);
            Check(mix.Success && sealedGold.Profile.Sealed.Gold == sealedPart && sealedGold.Profile.Gold == sealedPart && sealedGold.Profile.FreeGold == 0,
                  "while Sealed plus enough FREE Gold sells, pays from the free part only and leaves the Sealed " + sealedPart + " untouched");
        }

        // ------------------------------------------------------------------ 6. the clock

        private static void CheckQuartermasterClock(ContentDatabase db)
        {
            int hour = db.Balance.DailyResetHourUtc;
            long beforeReset = SinkUnix(2025, 10, 9, hour) - 60;
            long afterReset = SinkUnix(2025, 10, 9, hour) + 60;
            long nextBefore = SinkUnix(2025, 10, 10, hour) - 60;
            long nextAfter = SinkUnix(2025, 10, 10, hour) + 60;

            GameSession s = QmSession(db, 36, 50000000);
            Quartermaster.Open(s, afterReset);
            Quartermaster.Buy(s, 0, afterReset);
            string stockA = QmStockText(s);
            long stamp = s.Profile.Quartermaster.DayUnix;

            bool again = Quartermaster.Open(s, nextBefore);
            Check(!again && QmStockText(s) == stockA && s.Profile.Quartermaster.Slots[0].Sold,
                  "OPENING AGAIN THE SAME REWARD DAY changes nothing: the stock stands and the sold shelf stays sold");

            bool next = Quartermaster.Open(s, nextAfter);
            bool noneSold = true;
            for (int i = 0; i < s.Profile.Quartermaster.Slots.Count; i++) if (s.Profile.Quartermaster.Slots[i].Sold) noneSold = false;
            Check(next && QmStockText(s) != stockA && noneSold && s.Profile.Quartermaster.DayUnix == stamp + 86400L,
                  "AT THE REWARD DAY (" + hour.ToString("00") + ":00 UTC) the shelf restocks with a new, unsold stock and the stamp moves forward a day");

            // A clock wound back finds the later day's stock standing and reopens nothing.
            string stockB = QmStockText(s);
            Quartermaster.Buy(s, 3, nextAfter);
            string withSale = QmSnapshot(s);
            bool wound = Quartermaster.Open(s, beforeReset);
            QuartermasterResult replay = Quartermaster.Buy(s, 3, beforeReset);
            Check(!wound && QmSnapshot(s) == withSale && !replay.Success,
                  "A CLOCK WOUND BACK rolls nothing and reopens nothing: the later day's stock and its sold shelf stand, and a second sale is refused");
            QuartermasterView view = Quartermaster.View(s, beforeReset);
            Check(!view.Stale && view.Slots.Count == 6 && view.Slots[3].Sold,
                  "and the view under a wound-back clock reads the standing stock, with the sold shelf still sold");
        }

        // ------------------------------------------------------------------ 7. the save

        private static void CheckQuartermasterSave(ContentDatabase db)
        {
            long now = SinkUnix(2025, 10, 9, 8);
            string error;

            // Never opened: no key, version unchanged.
            GameSession fresh = QmSession(db, 36, 1000);
            string freshJson = QmSnapshot(fresh);
            Check(JsonValue.Parse(freshJson).GetInt("version", 0) == SaveSerializer.BaseVersion && !freshJson.Contains("quartermaster"),
                  "A PROFILE THAT NEVER OPENED THE SHOP carries no quartermaster key and says version " + SaveSerializer.BaseVersion + ": the document the build before wrote");

            GameSession s = QmSession(db, 36, 50000000);
            Quartermaster.Open(s, now);
            Quartermaster.Buy(s, 1, now);
            string json = QmSnapshot(s);
            PlayerProfile back = SaveSerializer.Deserialize(json, db, out error);
            Check(back != null && error == null && SaveSerializer.Serialize(back) == json
                  && back.Quartermaster.Slots.Count == 6 && back.Quartermaster.Slots[1].Sold && !back.Quartermaster.Slots[0].Sold,
                  "SAVE ROUND TRIP: a stock with one shelf sold saves, loads back identical and saves again byte for byte (" + json.Length + " characters)");
            Check(JsonValue.Parse(json).GetInt("version", 0) == SaveSerializer.CurrentVersion && SaveSerializer.VersionOfJson(json) == 24 && json.Contains("\"quartermaster\""),
                  "and the document claims version 24, the version that holds the shelf");
            Check(back != null && back.Quartermaster.Slots[0].Piece.Uid == "" && back.OwnedArmor.Count == s.Profile.OwnedArmor.Count,
                  "the stocked pieces carry no uid and the bought one is in the bag");

            string[] damaged = { "\"quartermaster\": \"x\"", "\"quartermaster\": 7", "\"quartermaster\": []", "\"quartermaster\": { \"day\": \"x\", \"slots\": 3 }",
                                 "\"quartermaster\": { \"day\": 5, \"slots\": [1, \"x\", {}] }" };
            int fine = 0;
            for (int i = 0; i < damaged.Length; i++)
            {
                string doc = freshJson.TrimEnd();
                int last = doc.LastIndexOf('}');
                string withBlock = doc.Substring(0, last).TrimEnd() + ",\n  " + damaged[i] + "\n}";
                PlayerProfile p = SaveSerializer.Deserialize(withBlock, db, out error);
                if (p != null && error == null && p.Quartermaster.IsEmpty) fine++;
            }
            Check(fine == damaged.Length, "damaged blocks (a string, a number, an array, wrong types, shelves that are not pieces) read as no stock and the save still loads (" + fine + " of " + damaged.Length + ")");

            // A damaged shelf is rolled afresh on the next open.
            PlayerProfile reopened = SaveSerializer.Deserialize(freshJson, db, out error);
            GameSession g = new GameSession(db, reopened);
            Check(Quartermaster.Open(g, now) && g.Profile.Quartermaster.Slots.Count == 6, "and an empty shelf is stocked the next time the shop is opened");

            // An old document loads with no stock.
            PlayerProfile old = SaveSerializer.Deserialize(freshJson, db, out error);
            Check(old != null && old.Quartermaster.IsEmpty && SaveSerializer.Serialize(old) == freshJson,
                  "A VERSION 22 SAVE LOADS with no stock and saves again as exactly the same document at 22");
        }

        // ------------------------------------------------------------------ 8. not hashed

        private static void CheckQuartermasterNotHashed(ContentDatabase db, string contentDir)
        {
            string[] hashed = { "heroes", "enemies", "campaign", "balance", "relics", "events", "armor", "floor-conditions" };
            string[] texts = new string[hashed.Length];
            for (int i = 0; i < hashed.Length; i++)
            {
                string path = Path.Combine(contentDir, hashed[i] + ".json");
                texts[i] = File.Exists(path) ? File.ReadAllText(path) : null;
            }
            Check(ContentDatabase.VersionOf(texts) == db.ContentVersion,
                  "QUARTERMASTER.JSON IS NOT HASHED: ContentVersion is exactly the hash of the eight fight files (" + db.ContentVersion + "), and quartermaster.json is not among them");

            string text = File.ReadAllText(Path.Combine(contentDir, "quartermaster.json"));
            ContentDatabase other = LoadContent(contentDir);
            string version = other.ContentVersion;
            other.LoadQuartermaster(text.Replace("\"Rough\": 30", "\"Rough\": 45"));
            GameSession a = QmSession(db, 36, 0), b = QmSession(other, 36, 0);
            ArmorPiece rough = ArmorRoller.Roll(db.Armor, ArmorSlot.Weapon, ArmorTemper.Rough, Aspect.Ember, 7);
            long was = Quartermaster.PriceOf(a, rough), now = Quartermaster.PriceOf(b, rough);
            Check(now > was && other.ContentVersion == version && version == db.ContentVersion,
                  "so retuning a price moves the price (" + was + " to " + now + ") and not ContentVersion, which keeps every recorded guild dungeon replay");

            other.LoadQuartermaster(null);
            Check(!other.Quartermaster.IsUsable && other.ContentVersion == version && !Quartermaster.Active(QmSession(other, 36, 0)),
                  "and removing the file closes the shop and changes nothing the version reads");
        }

        // ------------------------------------------------------------------ 9. combat

        private static void CheckQuartermasterNoCombatChange(ContentDatabase db, string contentDir)
        {
            ContentDatabase other = LoadContent(contentDir);
            other.LoadQuartermaster(null);
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
            Check(same, "COMBAT DOES NOT MOVE: three fixed battles are the same event for event with quartermaster.json loaded and with the whole file removed");

            // A purchase adds to the bag; nothing is worn until the player wears it.
            GameSession bought = QmSession(db, 36, 50000000);
            string before = FingerprintOf(CombatSimulation.Run(bought.BuildBattleRequest(CampaignWaveGenerator.BuildBattle(db.AllStages[0], 1, 1), 4242)));
            long now = SinkUnix(2025, 10, 9, 8);
            Quartermaster.Open(bought, now);
            Quartermaster.Buy(bought, 0, now);
            string after = FingerprintOf(CombatSimulation.Run(bought.BuildBattleRequest(CampaignWaveGenerator.BuildBattle(db.AllStages[0], 1, 1), 4242)));
            Check(before == after, "and buying a piece leaves the team that was fighting fighting the same fight: a purchase adds to the bag and nothing is worn until the player wears it");
        }

        // ------------------------------------------------------------------ 10. rollback

        private static void CheckQuartermasterOff(ContentDatabase db)
        {
            long now = SinkUnix(2025, 10, 9, 8);
            bool was = QuartermasterSwitch.Enabled;
            try
            {
                QuartermasterSwitch.Enabled = false;
                GameSession off = QmSession(db, 36, 5000000);
                string offJson = QmSnapshot(off);

                bool rolled = Quartermaster.Open(off, now);
                QuartermasterResult r = Quartermaster.Buy(off, 0, now);
                QuartermasterView view = Quartermaster.View(off, now);
                Check(!rolled && !Quartermaster.Active(off) && !r.Success && r.Reason == QuartermasterRefusal.Off && !view.Active && view.Slots.Count == 0
                      && off.Profile.Quartermaster.IsEmpty && QmSnapshot(off) == offJson,
                      "ROLLBACK: with the switch off nothing is stocked, every purchase is refused as Off, the shelf is empty, nothing is charged and nothing is recorded");

                JsonValue root = JsonValue.Parse(offJson);
                List<string> keys = new List<string>();
                foreach (string key in root.Keys) keys.Add(key);
                Check(root.GetInt("version", 0) == 22 && !keys.Contains("quartermaster") && keys.Count == 40,
                      "and the save is version 22 with the 40 keys main writes (" + keys.Count + " keys, no quartermaster key)");

                // The same session with the shop on and opened differs from the off save in the version and the one key.
                QuartermasterSwitch.Enabled = true;
                GameSession on = QmSession(db, 36, 5000000);
                Quartermaster.Open(on, now);
                string onJson = QmSnapshot(on);
                JsonValue onRoot = JsonValue.Parse(onJson);
                JsonValue rebuilt = JsonValue.NewObject();
                foreach (string key in onRoot.Keys)
                {
                    if (key == "quartermaster") continue;
                    if (key == "version") { rebuilt.Set(key, 22); continue; }
                    rebuilt.Set(key, onRoot[key]);
                }
                Check(onRoot.GetInt("version", 0) == 24 && rebuilt.ToJson(true) == offJson,
                      "THE OFF SAVE IS MAIN'S DOCUMENT: the same session with a stock on file is the off save plus the quartermaster key and version 24, byte for byte");

                // A shelf on file is kept untouched with the switch off: turning it off costs a player nothing.
                QuartermasterSwitch.Enabled = false;
                string error;
                PlayerProfile kept = SaveSerializer.Deserialize(onJson, db, out error);
                Check(kept != null && SaveSerializer.Serialize(kept) == onJson,
                      "with the switch off a saved shelf is still read and written back untouched: turning it off costs a player nothing");
            }
            finally { QuartermasterSwitch.Enabled = was; }
        }

        // ------------------------------------------------------------------ 11. wiring

        /// <summary>
        /// WIRING TRIPWIRES, not behavioural proof: the screen, the router and the Forge cannot run in
        /// the harness, so this reads their source. It fails if the screen stops being built behind the
        /// switch, if the door appears outside it, or if the screen starts to decide, to price or to sell
        /// by any road but Quartermaster. What the screen then looks like is for the Play queue.
        /// </summary>
        private static void CheckQuartermasterWiring(string contentDir)
        {
            string root = Path.GetFullPath(Path.Combine(contentDir, "..", "..", ".."));
            string ui = Path.Combine(root, "Assets", "Scripts", "UI");
            string routerPath = Path.Combine(ui, "ScreenRouter.cs");
            string forgePath = Path.Combine(ui, "Screens", "ForgeScreen.cs");
            string screenPath = Path.Combine(ui, "Screens", "QuartermasterScreen.cs");
            string[] files = { routerPath, forgePath, screenPath };
            for (int i = 0; i < files.Length; i++)
                if (!File.Exists(files[i]))
                {
                    Check(false, "the Quartermaster screen, router and Forge are where the harness expects them (" + files[i] + ")");
                    return;
                }
            string router = File.ReadAllText(routerPath), forge = File.ReadAllText(forgePath), screen = File.ReadAllText(screenPath);

            int guardAt = router.IndexOf("if (Emberfall.Core.Session.QuartermasterSwitch.Enabled) Quartermaster = Create<Screens.QuartermasterScreen>();", StringComparison.Ordinal);
            int count = CountOf(router, "Create<Screens.QuartermasterScreen>()");
            Check(guardAt >= 0 && count == 1,
                  "WIRING (reads source): the router builds the Quartermaster once, and only inside the switch");

            int doorAt = forge.IndexOf("if (QuartermasterSwitch.Enabled) BuildQuartermasterDoor(top);", StringComparison.Ordinal);
            Check(doorAt >= 0 && CountOf(forge, "ScreenId.Quartermaster") == 1 && forge.Contains("FeatureGates.TryEnter(App, \"forge.Quartermaster\")"),
                  "and the Forge's door is drawn only inside the switch, and asks the gate by its own id");

            bool decides = screen.Contains("Profile.Gold") || screen.Contains(".OwnedArmor") || screen.Contains("ArmorRoller")
                        || screen.Contains("Random") || screen.Contains("PriceOf") || screen.Contains("HoursOf");
            Check(!decides && screen.Contains("Quartermaster.View(") && screen.Contains("Quartermaster.Buy(") && screen.Contains("Quartermaster.Open("),
                  "WIRING (reads source): the screen calls only Quartermaster.Open, View and Buy, prices nothing itself, rolls nothing and writes no purse");
        }

        private static int CountOf(string text, string needle)
        {
            int n = 0, at = 0;
            while ((at = text.IndexOf(needle, at, StringComparison.Ordinal)) >= 0) { n++; at += needle.Length; }
            return n;
        }
    }
}
