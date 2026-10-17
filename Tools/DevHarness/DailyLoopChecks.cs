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
    /// THE DAILY LOOP (DESIGN-DAILY-LOOP.md), one named check per rule: the orders a day carries,
    /// what counts toward them, claiming once, the two chests, the calendar, the day rolling over,
    /// a clock wound back, the save, orders.json staying out of ContentVersion, and the switch.
    ///
    /// Every call is made through GameSession's own rules with an explicit clock, never by setting
    /// a counter, except where a check names the board it wants (LoopSetBoard) the way a stored
    /// board really is: the loop stores its day's orders, so a test may store its own.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>A fixed clock for the loop's checks (2025-10-09). Days are counted from the
        /// reward day it falls in, so every check reads the same calendar.</summary>
        private const long LoopBase = 1760000000L;

        private static void CheckDailyLoop(ContentDatabase db, string contentDir)
        {
            Line("");
            Line("DAILY LOOP (DESIGN-DAILY-LOOP.md: five orders a day, two chests, a seven day calendar, all paid in hours of idle income)");

            bool was = DailyLoopSwitch.Enabled;
            bool onboardingWas = OnboardingSwitch.Enabled;
            try
            {
                DailyLoopSwitch.Enabled = true;
                CheckDailyLoopContent(db);
                CheckDailyLoopBoard(db);
                CheckDailyLoopProgress(db);
                CheckDailyLoopClaims(db);
                CheckDailyLoopChests(db);
                CheckDailyLoopCalendar(db);
                CheckDailyLoopClock(db);
                CheckDailyLoopSave(db);
                CheckDailyLoopNotHashed(db, contentDir);
                CheckDailyLoopOff(db);
                CheckDailyLoopWiring(contentDir);
            }
            finally
            {
                DailyLoopSwitch.Enabled = was;
                OnboardingSwitch.Enabled = onboardingWas;
            }
            Check(DailyLoopSwitch.Enabled == was && OnboardingSwitch.Enabled == onboardingWas,
                  "the switches are put back as the run found them (daily loop " + (was ? "on" : "off") + ")");
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A session with the first <paramref name="cleared"/> stages cleared.</summary>
        private static GameSession LoopSession(ContentDatabase db, int cleared)
        {
            PlayerProfile p = GameSession.CreateNewProfile(db, LoopBase);
            for (int i = 0; i < cleared && i < db.AllStages.Count; i++) p.ClearedStageIds.Add(db.AllStages[i].Id);
            p.StagesCleared = CampaignProgress.CountCleared(p, db);
            return new GameSession(db, p);
        }

        /// <summary>How many stages must be cleared for this feature's stage to be among them.</summary>
        private static int LoopClearedToOpen(ContentDatabase db, Feature feature)
        {
            StageDef stage = FeatureGate.UnlockStage(db, feature);
            for (int i = 0; i < db.AllStages.Count; i++)
                if (stage != null && db.AllStages[i].Id == stage.Id) return i + 1;
            return 0;
        }

        private static long LoopDayStart(GameSession s, int day)
        {
            return s.DayStartUnix(LoopBase) + day * 86400L;
        }

        /// <summary>A moment inside reward day <paramref name="day"/>, hour hours after its start.</summary>
        private static long LoopAt(GameSession s, int day, int hour)
        {
            return LoopDayStart(s, day) + hour * 3600L;
        }

        /// <summary>Stores a board of exactly these orders for the reward day of now.</summary>
        private static void LoopSetBoard(GameSession s, long now, params string[] ids)
        {
            DailyLoop.Roll(s, now);
            DailyLoopState st = s.Profile.DailyLoop;
            st.Orders.Clear();
            for (int i = 0; i < ids.Length; i++)
            {
                OrderProgress p = new OrderProgress();
                p.Id = ids[i];
                st.Orders.Add(p);
            }
        }

        private static int LoopProgress(GameSession s, string id)
        {
            OrderProgress p = s.Profile.DailyLoop.Find(id);
            return p == null ? -1 : p.Progress;
        }

        /// <summary>Finishes and claims every order on the board of now's day, through ClaimOrder.</summary>
        private static void LoopClaimAll(GameSession s, long now)
        {
            DailyLoop.Roll(s, now);
            List<OrderProgress> orders = new List<OrderProgress>(s.Profile.DailyLoop.Orders);
            for (int i = 0; i < orders.Count; i++)
            {
                OrderDef d = s.Content.Orders.Order(orders[i].Id);
                if (d != null) orders[i].Progress = d.Target;
                DailyLoop.ClaimOrder(s, orders[i].Id, now);
            }
        }

        private static string LoopBoardIds(List<OrderDef> board)
        {
            string[] ids = new string[board.Count];
            for (int i = 0; i < board.Count; i++) ids[i] = board[i].Id;
            return string.Join(",", ids);
        }

        // ------------------------------------------------------------------ content

        private static void CheckDailyLoopContent(ContentDatabase db)
        {
            OrdersConfig c = db.Orders;
            Check(c.IsUsable && c.Warnings.Count == 0,
                  "orders.json loads usable with no warnings (" + c.Orders.Count + " orders, " + c.Calendar.Count + " calendar days, "
                  + c.Warnings.Count + " warnings)");

            List<int> slots = new List<int>();
            for (int i = 0; i < c.Orders.Count; i++) if (!slots.Contains(c.Orders[i].Slot)) slots.Add(c.Orders[i].Slot);
            Check(slots.Count == 5, "the pool fills five slots, one board entry each (" + slots.Count + " slots)");

            List<string> kinds = new List<string>();
            for (int i = 0; i < c.Orders.Count; i++)
                if (c.Orders[i].Kind != OrderKind.Waves || !c.Orders[i].Fallback) { if (!kinds.Contains(c.Orders[i].Kind.ToString())) kinds.Add(c.Orders[i].Kind.ToString()); }
            Check(kinds.Count == 5, "the five decided orders are all in the pool: waves, stage, level, summon and arena (" + string.Join(", ", kinds.ToArray()) + ")");

            bool arenaGuarded = true, fallbackOpen = false;
            for (int i = 0; i < c.Orders.Count; i++)
            {
                if (c.Orders[i].Kind == OrderKind.Arena
                    && !(c.Orders[i].Requires.Contains(Feature.Proving) && c.Orders[i].Requires.Contains(Feature.Recital))) arenaGuarded = false;
                if (c.Orders[i].Slot == 4 && c.Orders[i].Fallback && c.Orders[i].Requires.Count == 0) fallbackOpen = true;
            }
            Check(arenaGuarded && fallbackOpen,
                  "every arena order requires the Proving or the Recital, and the arena slot has a fallback that needs neither");

            bool fixedRewards = c.DailyChestHours > 0 && c.WeeklyChestHours > 0 && c.WeeklyDaysRequired == 5;
            for (int i = 0; i < c.Calendar.Count; i++) if (c.Calendar[i].Hours <= 0) fixedRewards = false;
            for (int i = 0; i < c.Orders.Count; i++) if (c.Orders[i].Hours <= 0) fixedRewards = false;
            Check(fixedRewards,
                  "every reward is a fixed, positive number of hours, the weekly chest wants 5 days (daily chest " + c.DailyChestHours
                  + " h, weekly chest " + c.WeeklyChestHours + " h)");
        }

        // ------------------------------------------------------------------ the board

        private static void CheckDailyLoopBoard(ContentDatabase db)
        {
            GameSession open = LoopSession(db, db.AllStages.Count);
            bool same = true, five = true, oneEach = true;
            List<string> distinct = new List<string>();
            for (int d = 0; d < 40; d++)
            {
                long start = LoopDayStart(open, d);
                List<OrderDef> a = DailyLoop.BuildBoard(open, start);
                List<OrderDef> b = DailyLoop.BuildBoard(open, start);
                if (LoopBoardIds(a) != LoopBoardIds(b)) same = false;
                if (a.Count != 5) five = false;
                for (int i = 1; i < a.Count; i++) if (a[i].Slot <= a[i - 1].Slot) oneEach = false;
                string ids = LoopBoardIds(a);
                if (!distinct.Contains(ids)) distinct.Add(ids);
            }
            Check(same, "ORDER GENERATION IS DETERMINISTIC: the same reward day builds the same board every time, 40 days");
            Check(five && oneEach, "a day carries five orders, one from each slot in slot order");
            Check(distinct.Count >= 8, "and the days differ from one another (" + distinct.Count + " distinct boards in 40 days)");

            GameSession same2 = LoopSession(db, 0);
            string a0 = LoopBoardIds(DailyLoop.BuildBoard(open, LoopDayStart(open, 3)));
            string b0 = LoopBoardIds(DailyLoop.BuildBoard(same2, LoopDayStart(same2, 3)));
            Check(a0.Length > 0 && a0.Split(',').Length == 5 && b0.Split(',').Length == 5,
                  "a player with nothing unlocked still gets five orders (the arena slot is filled by its fallback)");

            // ---- UNLOCK AWARE: never an order for a locked feature, at any point of the campaign
            OnboardingSwitch.Enabled = true;
            int openProving = LoopClearedToOpen(db, Feature.Proving), openRecital = LoopClearedToOpen(db, Feature.Recital);
            int[] cleared = { 0, 1, openProving - 1, openProving, openRecital - 1, openRecital, db.AllStages.Count };
            List<string> offered = new List<string>();
            int arenaSeenLocked = 0, arenaSeenOpen = 0, fallbackSeenOpen = 0, checkedBoards = 0;
            for (int k = 0; k < cleared.Length; k++)
            {
                GameSession s = LoopSession(db, cleared[k]);
                bool provingOpen = FeatureGate.IsUnlocked(db, s.Profile, Feature.Proving);
                bool recitalOpen = FeatureGate.IsUnlocked(db, s.Profile, Feature.Recital);
                for (int d = 0; d < 60; d++)
                {
                    List<OrderDef> board = DailyLoop.BuildBoard(s, LoopDayStart(s, d));
                    checkedBoards++;
                    for (int i = 0; i < board.Count; i++)
                    {
                        bool arena = board[i].Kind == OrderKind.Arena;
                        if (arena && !provingOpen && !recitalOpen) arenaSeenLocked++;
                        if (arena && (provingOpen || recitalOpen)) arenaSeenOpen++;
                        if (board[i].Fallback && (provingOpen || recitalOpen)) fallbackSeenOpen++;
                        if (!DailyLoop.Unlocked(s, board[i])) offered.Add(board[i].Id + " after " + cleared[k] + " stages");
                    }
                }
            }
            Check(arenaSeenLocked == 0 && offered.Count == 0,
                  "NEVER AN ORDER FOR A LOCKED FEATURE: " + checkedBoards + " boards across " + cleared.Length
                  + " points of the campaign offer no arena order while both the Proving and the Recital are shut"
                  + (offered.Count > 0 ? " - offered: " + string.Join("; ", offered.ToArray()) : ""));
            Check(arenaSeenOpen > 0 && fallbackSeenOpen == 0,
                  "and once one of them opens the arena order is offered (" + arenaSeenOpen + " times) and its fallback is not (" + fallbackSeenOpen + ")");

            OnboardingSwitch.Enabled = false;
            GameSession allOpen = LoopSession(db, 0);
            bool arenaWithSwitchOff = false;
            for (int d = 0; d < 60; d++)
                foreach (OrderDef o in DailyLoop.BuildBoard(allOpen, LoopDayStart(allOpen, d)))
                    if (o.Kind == OrderKind.Arena) arenaWithSwitchOff = true;
            OnboardingSwitch.Enabled = true;
            Check(arenaWithSwitchOff, "with the onboarding switch off every feature reads open, so the arena order is offered from the start");
        }

        // ------------------------------------------------------------------ progress

        private static void CheckDailyLoopProgress(ContentDatabase db)
        {
            StageDef first = db.AllStages[0];
            GameSession s = LoopSession(db, 0);
            long t0 = LoopAt(s, 1, 6);
            LoopSetBoard(s, t0, "waves_8", "stage_2", "level_3", "summon_1", "arena_2");
            Check(s.Profile.DailyLoop.Orders.Count == 5 && LoopProgress(s, "waves_8") == 0, "a stored board starts every order at zero");

            // ---- a wave cleared: the fight is the game's own ResolveWave, counted once per wave
            int waves = 0, resolves = 0, lostWaves = 0;
            bool stageCleared = false;
            while (!stageCleared && resolves < 60)
            {
                WaveResolution res = s.ResolveWave(first, 7100 + resolves * 101, t0 + resolves);
                resolves++;
                if (res.WaveCleared) waves++; else lostWaves++;
                if (res.StageCleared) stageCleared = true;
            }
            OrderDef wavesDef = db.Orders.Order("waves_8");
            Check(waves > 0 && LoopProgress(s, "waves_8") == Math.Min(wavesDef.Target, waves),
                  "WAVE CLEARS COUNT: " + waves + " waves fell over " + resolves + " resolves (" + lostWaves
                  + " stopped on a loss) and the order reads " + LoopProgress(s, "waves_8") + " of " + wavesDef.Target
                  + ", so a lost battle counted for nothing");

            // ---- a stage won
            Check(stageCleared && LoopProgress(s, "stage_2") == 1,
                  "A STAGE WIN COUNTS: clearing " + first.DisplayCode + " moves 'win 2 stages' to " + LoopProgress(s, "stage_2") + " of 2");

            // ---- a sweep is a stage win and stands for the stage's whole ladder
            int stageBefore = LoopProgress(s, "stage_2");
            BattleRewards swept = s.SweepClearedStage(first, 8800, t0 + 500);
            Check(swept.Victory && LoopProgress(s, "stage_2") == Math.Min(2, stageBefore + 1)
                  && LoopProgress(s, "waves_8") == Math.Min(wavesDef.Target, waves + Math.Max(1, first.WaveCount)),
                  "A SWEEP IS A STAGE WIN AND ITS WAVES: it moves 'win 2 stages' to " + LoopProgress(s, "stage_2")
                  + " and 'clear 8 waves' to " + LoopProgress(s, "waves_8") + " (the stage has " + first.WaveCount + " waves)");

            // ---- a level bought; a refused one counts nothing
            HeroInstance hero = s.Profile.Heroes[0];
            s.Profile.Gold = 0;
            LevelUpResult broke = s.LevelUpHero(hero, 1, t0 + 600);
            Check(!broke.Success && LoopProgress(s, "level_3") == 0, "a level-up the gold cannot pay for counts nothing");
            s.Profile.Gold = 1000000000L;
            LevelUpResult bought = s.LevelUpHero(hero, 2, t0 + 601);
            Check(bought.Success && LoopProgress(s, "level_3") == Math.Min(3, bought.LevelsGained),
                  "A LEVEL-UP COUNTS: " + bought.LevelsGained + " levels bought move 'level heroes up 3 times' to " + LoopProgress(s, "level_3"));

            // ---- a summon; a refused one counts nothing
            s.Profile.Shards = 0;
            SummonOutcome refused = s.Summon(9301, t0 + 700);
            Check(!refused.Success && LoopProgress(s, "summon_1") == 0, "a summon the shards cannot pay for counts nothing");
            s.Profile.Shards = db.Balance.SummonCost;
            SummonOutcome summoned = s.Summon(9302, t0 + 701);
            Check(summoned.Success && LoopProgress(s, "summon_1") == 1, "A SUMMON COUNTS: 'summon once' reads " + LoopProgress(s, "summon_1") + " of 1");

            // ---- the Proving and the Recital, a loss counting like a win
            s.GrantProvingResult(false, t0 + 800);
            Check(LoopProgress(s, "arena_2") == 1, "A PROVING FIGHT COUNTS, lost or won: 'fight twice' reads " + LoopProgress(s, "arena_2") + " of 2");
            s.GrantRecitalResult(1, false, t0 + 801);
            Check(LoopProgress(s, "arena_2") == 2, "A RECITAL FIGHT COUNTS, lost or won: 'fight twice' reads " + LoopProgress(s, "arena_2") + " of 2");

            // ---- capped at the target, and nothing counts toward an order already claimed
            s.GrantProvingResult(true, t0 + 802);
            DailyLoop.ClaimOrder(s, "arena_2", t0 + 803);
            s.GrantProvingResult(true, t0 + 804);
            OrderProgress claimed = s.Profile.DailyLoop.Find("arena_2");
            Check(claimed.Progress == 2 && claimed.Claimed, "progress stops at the target, and a claimed order counts nothing more");

            // ---- a LOST fight counts nothing: a wave lost, a stage not won, a sweep lost
            GameSession weak = LoopSession(db, 11);
            long tw = LoopAt(weak, 1, 6);
            LoopSetBoard(weak, tw, "waves_8", "stage_2", "level_3", "summon_1", "arena_2");
            StageDef hard = db.AllStages[11];
            WaveResolution lostRes = weak.ResolveWave(hard, 4242, tw + 1);
            Check(lostRes.LostBattle > 0 && !lostRes.WaveCleared && !lostRes.StageCleared
                  && LoopProgress(weak, "waves_8") == 0 && LoopProgress(weak, "stage_2") == 0,
                  "A LOST WAVE COUNTS NOTHING: a level 1 line fought " + hard.DisplayCode + " and lost battle " + lostRes.LostBattle
                  + ", and the wave and stage orders read " + LoopProgress(weak, "waves_8") + " and " + LoopProgress(weak, "stage_2"));

            BattleRewards lostSweep = weak.SweepClearedStage(hard, 4343, tw + 2);
            Check(!lostSweep.Victory && LoopProgress(weak, "waves_8") == 0 && LoopProgress(weak, "stage_2") == 0,
                  "A LOST SWEEP COUNTS NOTHING: the sweep of " + hard.DisplayCode + " was lost (victory " + lostSweep.Victory + ") and moved no order");

            // ---- a sweep past the repeat cap pays nothing, so it counts nothing
            GameSession capped = LoopSession(db, 1);
            long tc = LoopAt(capped, 1, 6);
            LoopSetBoard(capped, tc, "waves_8", "stage_2", "level_3", "summon_1", "arena_2");
            capped.RollDailyReset(tc);
            capped.Profile.StageClearsToday[first.Id] = capped.RepeatFullClearsPerDay;
            long goldBefore = capped.Profile.Gold;
            BattleRewards cappedSweep = capped.SweepClearedStage(first, 8800, tc + 1);
            Check(cappedSweep.Victory && cappedSweep.RepeatCapped && capped.Profile.Gold == goldBefore
                  && LoopProgress(capped, "waves_8") == 0 && LoopProgress(capped, "stage_2") == 0,
                  "A SWEEP PAST THE REPEAT CAP COUNTS NOTHING: it paid " + (capped.Profile.Gold - goldBefore)
                  + " gold and the wave and stage orders read " + LoopProgress(capped, "waves_8") + " and " + LoopProgress(capped, "stage_2"));

            // ---- watching a replay is free and counts nothing
            GameSession watcher = LoopSession(db, 0);
            long tr = LoopAt(watcher, 1, 6);
            LoopSetBoard(watcher, tr, "waves_8", "stage_2", "level_3", "summon_1", "arena_2");
            WaveResolution played = watcher.ResolveWave(first, 7100, tr + 1);
            string beforeReplay = SaveSerializer.Serialize(watcher.Profile);
            if (played.Replay != null) watcher.ReplayBattle(played.Replay);
            Check(played.Replay != null && SaveSerializer.Serialize(watcher.Profile) == beforeReplay
                  && LoopProgress(watcher, "waves_8") > 0,
                  "A WATCHED REPLAY COUNTS NOTHING: replaying a fought wave leaves the saved profile, the loop key included, byte for byte as it was ("
                  + beforeReplay.Length + " characters)");

            // ---- kinds do not bleed into each other
            GameSession lone = LoopSession(db, 0);
            LoopSetBoard(lone, t0, "stage_1", "summon_1");
            lone.Profile.Shards = db.Balance.SummonCost;
            lone.Summon(9303, t0 + 5);
            Check(LoopProgress(lone, "stage_1") == 0 && LoopProgress(lone, "summon_1") == 1, "a summon moves only the summon order");
        }

        // ------------------------------------------------------------------ claiming

        private static void CheckDailyLoopClaims(ContentDatabase db)
        {
            GameSession s = LoopSession(db, 6);
            long t0 = LoopAt(s, 1, 6);
            LoopSetBoard(s, t0, "waves_3", "stage_1", "level_1", "summon_1", "waves_6f");
            OrderDef def = db.Orders.Order("waves_3");

            LoopPayout early = DailyLoop.ClaimOrder(s, "waves_3", t0);
            Check(!early.Success && s.Profile.Gold == GameSession.CreateNewProfile(db, LoopBase).Gold && !s.Profile.DailyLoop.Find("waves_3").Claimed,
                  "an unfinished order cannot be claimed and pays nothing");

            s.Profile.DailyLoop.Find("waves_3").Progress = def.Target;
            long gold = s.Profile.Gold, shards = s.Profile.Shards;
            LoopPayout expected = DailyLoop.Preview(s, def.Hours);
            LoopPayout paid = DailyLoop.ClaimOrder(s, "waves_3", t0 + 1);
            Check(paid.Success && s.Profile.Gold == gold + expected.Gold && s.Profile.Shards == shards + expected.Shards
                  && expected.Gold > 0 && expected.Shards > 0,
                  "A FINISHED ORDER PAYS ITS HOURS (" + def.Hours + " h: " + paid.Gold + " gold, " + paid.Shards + " shards)");

            long goldAfter = s.Profile.Gold, shardsAfter = s.Profile.Shards;
            LoopPayout again = DailyLoop.ClaimOrder(s, "waves_3", t0 + 2);
            Check(!again.Success && s.Profile.Gold == goldAfter && s.Profile.Shards == shardsAfter,
                  "CLAIM ONCE: a second claim of the same order is refused and pays nothing");

            Check(!DailyLoop.ClaimOrder(s, "no_such_order", t0 + 3).Success && !DailyLoop.ClaimOrder(s, "arena_1", t0 + 3).Success,
                  "an order that is not on today's board cannot be claimed");

            // ---- the reward is the idle formula and nothing else
            GameSession idle = LoopSession(db, 12);
            long now = LoopAt(idle, 2, 5);
            double[] hours = { 0.25, 0.5, 1, 2, 4 };
            bool sameAsIdle = true;
            string worst = "";
            for (int i = 0; i < hours.Length; i++)
            {
                LoopPayout p = DailyLoop.Preview(idle, hours[i]);
                PlayerProfile probe = GameSession.CreateNewProfile(db, now);
                for (int c = 0; c < 12; c++) probe.ClearedStageIds.Add(db.AllStages[c].Id);
                probe.LastIdleClaimUnix = now - (long)Math.Round(hours[i] * 3600.0);
                IdleReport report = new GameSession(db, probe).PeekIdle(now);
                if (report.Gold != p.Gold || report.Shards != p.Shards) { sameAsIdle = false; worst = hours[i] + " h"; }
            }
            Check(sameAsIdle, "NO SECOND FORMULA: " + hours.Length + " rewards equal exactly what the idle chest pays for the same hours at the same frontier"
                  + (worst.Length > 0 ? " - differs at " + worst : ""));

            GameSession early1 = LoopSession(db, 1), late = LoopSession(db, 30);
            LoopPayout a = DailyLoop.Preview(early1, 1), b = DailyLoop.Preview(late, 1);
            GameSession none = LoopSession(db, 0);
            LoopPayout floorPay = DailyLoop.Preview(none, 1);
            Check(b.Gold > a.Gold && b.Shards > a.Shards && floorPay.Gold > 0,
                  "and it follows the frontier: one hour is " + a.Gold + " gold at the first stage, " + b.Gold
                  + " at stage 30, and the floor rate (" + floorPay.Gold + ") before anything is cleared");
        }

        // ------------------------------------------------------------------ the chests

        private static void CheckDailyLoopChests(ContentDatabase db)
        {
            GameSession s = LoopSession(db, 12);
            long t0 = LoopAt(s, 1, 6);
            LoopSetBoard(s, t0, "waves_3", "stage_1", "level_1", "summon_1", "waves_6f");
            LoopBoard board = DailyLoop.Board(s, t0);
            Check(!board.ChestCanClaim && !DailyLoop.ClaimDailyChest(s, t0).Success, "THE DAILY CHEST IS SHUT while any order is unclaimed");

            string[] ids = { "waves_3", "stage_1", "level_1", "summon_1", "waves_6f" };
            for (int i = 0; i < ids.Length - 1; i++)
            {
                s.Profile.DailyLoop.Find(ids[i]).Progress = db.Orders.Order(ids[i]).Target;
                DailyLoop.ClaimOrder(s, ids[i], t0 + i);
            }
            Check(!DailyLoop.Board(s, t0).ChestCanClaim && !DailyLoop.ClaimDailyChest(s, t0).Success,
                  "and four orders of five do not open it");

            s.Profile.DailyLoop.Find(ids[4]).Progress = db.Orders.Order(ids[4]).Target;
            DailyLoop.ClaimOrder(s, ids[4], t0 + 9);
            Check(DailyLoop.Board(s, t0).ChestCanClaim, "THE DAILY CHEST OPENS when all five orders are claimed");

            long gold = s.Profile.Gold, shards = s.Profile.Shards;
            LoopPayout expected = DailyLoop.Preview(s, db.Orders.DailyChestHours);
            LoopPayout chest = DailyLoop.ClaimDailyChest(s, t0 + 10);
            Check(chest.Success && s.Profile.Gold == gold + expected.Gold && s.Profile.Shards == shards + expected.Shards && expected.Gold > 0,
                  "and pays its hours (" + db.Orders.DailyChestHours + " h: " + chest.Gold + " gold, " + chest.Shards + " shards)");
            long g2 = s.Profile.Gold;
            Check(!DailyLoop.ClaimDailyChest(s, t0 + 11).Success && s.Profile.Gold == g2 && !DailyLoop.Board(s, t0 + 12).ChestCanClaim,
                  "CLAIM ONCE: the daily chest opens once a day");

            LoopBoard next = DailyLoop.Board(s, LoopAt(s, 2, 6));
            Check(!next.ChestClaimed && !next.ChestCanClaim && next.Orders.Count == 5, "and the next day brings a new board with the chest shut again");

            // ---- THE WEEKLY CHEST: orders completed on 5 days of a week
            GameSession w = LoopSession(db, 12);
            int w0 = 0;
            while (DailyLoop.WeekOf(w, LoopDayStart(w, w0)) == DailyLoop.WeekOf(w, LoopDayStart(w, w0 - 1)) || w0 < 1) w0++;
            int[] days = { 0, 1, 2, 3 };
            for (int i = 0; i < days.Length; i++) LoopClaimAll(w, LoopAt(w, w0 + days[i], 7));
            LoopBoard four = DailyLoop.Board(w, LoopAt(w, w0 + 3, 9));
            Check(four.WeekDays == 4 && !four.WeeklyCanClaim && !DailyLoop.ClaimWeeklyChest(w, LoopAt(w, w0 + 3, 9)).Success,
                  "THE WEEKLY CHEST IS SHUT on 4 days of orders (" + four.WeekDays + " of " + four.WeekRequired + ")");

            LoopClaimAll(w, LoopAt(w, w0 + 4, 7));
            LoopClaimAll(w, LoopAt(w, w0 + 4, 8));
            LoopBoard five = DailyLoop.Board(w, LoopAt(w, w0 + 4, 9));
            Check(five.WeekDays == 5 && five.WeeklyCanClaim, "and OPENS on the fifth, a day claimed twice counting once (" + five.WeekDays + " of " + five.WeekRequired + ")");

            long wg = w.Profile.Gold;
            LoopPayout weeklyExpected = DailyLoop.Preview(w, db.Orders.WeeklyChestHours);
            LoopPayout weekly = DailyLoop.ClaimWeeklyChest(w, LoopAt(w, w0 + 4, 10));
            Check(weekly.Success && w.Profile.Gold == wg + weeklyExpected.Gold && weeklyExpected.Gold > 0,
                  "and pays its hours (" + db.Orders.WeeklyChestHours + " h: " + weekly.Gold + " gold)");
            long wg2 = w.Profile.Gold;
            Check(!DailyLoop.ClaimWeeklyChest(w, LoopAt(w, w0 + 5, 7)).Success && w.Profile.Gold == wg2,
                  "CLAIM ONCE: the weekly chest opens once a week");

            LoopBoard nextWeek = DailyLoop.Board(w, LoopAt(w, w0 + 7, 7));
            Check(nextWeek.WeekDays == 0 && !nextWeek.WeeklyClaimed && !nextWeek.WeeklyCanClaim,
                  "and the next week starts at zero days with its chest shut");

            // any 5 days of a week count, not 5 in a row
            GameSession g = LoopSession(db, 12);
            int[] scattered = { 0, 2, 3, 5, 6 };
            for (int i = 0; i < scattered.Length; i++) LoopClaimAll(g, LoopAt(g, w0 + scattered[i], 7));
            Check(DailyLoop.Board(g, LoopAt(g, w0 + 6, 9)).WeeklyCanClaim, "any 5 days of the week count, not 5 in a row");

            // a week's days do not carry over to the next
            GameSession h = LoopSession(db, 12);
            for (int i = 0; i < 4; i++) LoopClaimAll(h, LoopAt(h, w0 + i, 7));
            for (int i = 7; i < 8; i++) LoopClaimAll(h, LoopAt(h, w0 + i, 7));
            Check(DailyLoop.Board(h, LoopAt(h, w0 + 7, 9)).WeekDays == 1 && !DailyLoop.Board(h, LoopAt(h, w0 + 7, 9)).WeeklyCanClaim,
                  "four days one week and one the next make a week of one, not five");

            DateTime weekStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(LoopDayStart(w, w0));
            Check(weekStart.DayOfWeek == DayOfWeek.Monday,
                  "the week turns over on a Monday reward day (" + weekStart.ToString("yyyy-MM-dd HH:mm") + " UTC, " + weekStart.DayOfWeek + ")");
        }

        // ------------------------------------------------------------------ the calendar

        private static void CheckDailyLoopCalendar(ContentDatabase db)
        {
            GameSession s = LoopSession(db, 12);
            long t1 = LoopAt(s, 1, 8);
            LoopBoard day1 = DailyLoop.Board(s, t1);
            Check(day1.CalendarCanClaim && day1.Calendar.Count == 7 && day1.Calendar[0].Next && !day1.Calendar[0].Claimed,
                  "the calendar opens on day 1 with seven fixed rewards shown in advance");

            long gold = s.Profile.Gold;
            LoopPayout expected = DailyLoop.Preview(s, db.Orders.Calendar[0].Hours);
            LoopPayout one = DailyLoop.ClaimCalendar(s, t1);
            Check(one.Success && s.Profile.Gold == gold + expected.Gold && expected.Gold > 0,
                  "DAY 1 PAYS ITS HOURS (" + db.Orders.Calendar[0].Hours + " h: " + one.Gold + " gold)");

            long g2 = s.Profile.Gold;
            Check(!DailyLoop.ClaimCalendar(s, t1 + 3600).Success && !DailyLoop.CanClaimCalendar(s, t1 + 3600) && s.Profile.Gold == g2,
                  "THE CALENDAR ADVANCES ONCE PER RESET DAY: a second claim the same day is refused");

            bool inOrder = true;
            for (int d = 2; d <= 7; d++)
            {
                long before = s.Profile.Gold;
                LoopPayout expectedDay = DailyLoop.Preview(s, db.Orders.Calendar[d - 1].Hours);
                LoopPayout p = DailyLoop.ClaimCalendar(s, LoopAt(s, d, 8));
                if (!p.Success || s.Profile.Gold != before + expectedDay.Gold) inOrder = false;
            }
            Check(inOrder, "and each following day pays the next reward in order, day 2 to day 7");

            LoopBoard lap = DailyLoop.Board(s, LoopAt(s, 8, 8));
            Check(lap.CalendarCanClaim && lap.Calendar[0].Next && !lap.Calendar[6].Claimed,
                  "THE CALENDAR REPEATS: after day 7 the next claim is day 1 again");

            GameSession missed = LoopSession(db, 12);
            DailyLoop.ClaimCalendar(missed, LoopAt(missed, 1, 8));
            long mg = missed.Profile.Gold;
            LoopPayout expectedSecond = DailyLoop.Preview(missed, db.Orders.Calendar[1].Hours);
            LoopPayout back = DailyLoop.ClaimCalendar(missed, LoopAt(missed, 6, 8));
            Check(back.Success && missed.Profile.Gold == mg + expectedSecond.Gold && missed.Profile.DailyLoop.CalendarNext == 2,
                  "and a missed stretch of days delays the next reward and skips none: after a gap the claim is day 2");

            // the reward day is the boundary, not midnight
            GameSession edge = LoopSession(db, 12);
            DailyLoop.ClaimCalendar(edge, LoopAt(edge, 1, 1));
            long justBefore = LoopDayStart(edge, 2) - 1, justAfter = LoopDayStart(edge, 2);
            Check(!DailyLoop.CanClaimCalendar(edge, justBefore) && DailyLoop.CanClaimCalendar(edge, justAfter),
                  "the next day opens exactly on the daily reset (reset hour " + db.Balance.DailyResetHourUtc + " UTC)");
        }

        // ------------------------------------------------------------------ the clock

        private static void CheckDailyLoopClock(ContentDatabase db)
        {
            // ---- rollover on the existing reset
            GameSession s = LoopSession(db, 12);
            long d1 = LoopAt(s, 1, 6);
            DailyLoop.Roll(s, d1);
            LoopSetBoard(s, d1, "waves_3", "stage_1", "level_1", "summon_1", "waves_6f");
            s.Profile.DailyLoop.Find("waves_3").Progress = 3;
            DailyLoop.ClaimOrder(s, "waves_3", d1);
            s.Profile.DailyLoop.Find("stage_1").Progress = 1;

            long edge = LoopDayStart(s, 2);
            Check(!DailyLoop.Roll(s, edge - 1) && s.Profile.DailyLoop.Find("waves_3") != null && s.Profile.DailyLoop.Find("waves_3").Claimed,
                  "one second before the reset the board is still yesterday's, claims and all");
            Check(DailyLoop.Roll(s, edge), "ROLLOVER: the board rolls on the same instant GameSession.RollDailyReset does");

            PlayerProfile probe = GameSession.CreateNewProfile(db, d1);
            GameSession ps = new GameSession(db, probe);
            ps.RollDailyReset(d1);
            Check(!ps.RollDailyReset(edge - 1) && ps.RollDailyReset(edge),
                  "the repeat cap's reset (RollDailyReset) turns over at that same instant, so there is one clock");

            DailyLoopState st = s.Profile.DailyLoop;
            bool fresh = st.Orders.Count == 5 && !st.ChestClaimed;
            for (int i = 0; i < st.Orders.Count; i++) if (st.Orders[i].Progress != 0 || st.Orders[i].Claimed) fresh = false;
            string newIds = "";
            for (int i = 0; i < st.Orders.Count; i++) newIds += (i > 0 ? "," : "") + st.Orders[i].Id;
            Check(fresh && st.DayUnix == edge && newIds == LoopBoardIds(DailyLoop.BuildBoard(s, edge)),
                  "and the new day starts clean: five orders, no progress, no claims, the daily chest shut (unclaimed work does not carry over)");

            // ---- REWIND IMMUNITY: a device clock wound back reopens nothing
            GameSession r = LoopSession(db, 12);
            long today = LoopAt(r, 10, 6);
            LoopSetBoard(r, today, "waves_3", "stage_1", "level_1", "summon_1", "waves_6f");
            LoopClaimAll(r, today);
            DailyLoop.ClaimDailyChest(r, today);
            DailyLoop.ClaimCalendar(r, today);
            string frozen = SaveSerializer.Serialize(r.Profile);
            long gold = r.Profile.Gold, shards = r.Profile.Shards;

            int[] backDays = { 0, 1, 2, 9 };
            bool nothing = true;
            for (int i = 0; i < backDays.Length; i++)
            {
                long wound = LoopAt(r, 10 - backDays[i], 5) - (backDays[i] == 0 ? 3600 : 0);
                if (DailyLoop.Roll(r, wound)) nothing = false;
                if (DailyLoop.ClaimOrder(r, "waves_3", wound).Success) nothing = false;
                if (DailyLoop.ClaimDailyChest(r, wound).Success) nothing = false;
                if (DailyLoop.ClaimWeeklyChest(r, wound).Success) nothing = false;
                if (DailyLoop.ClaimCalendar(r, wound).Success || DailyLoop.CanClaimCalendar(r, wound)) nothing = false;
                LoopBoard wb = DailyLoop.Board(r, wound);
                if (wb.CalendarCanClaim || wb.ChestCanClaim || !wb.Orders[0].Claimed) nothing = false;
            }
            Check(nothing && r.Profile.Gold == gold && r.Profile.Shards == shards && SaveSerializer.Serialize(r.Profile) == frozen,
                  "REWIND IMMUNITY: with the clock wound back by an hour, a day, two days and nine days, no order, chest or calendar day reopens, "
                  + "nothing is paid, and the saved loop is byte for byte what it was");

            // a clock wound back and then forward over days already seen cannot claim them again
            DailyLoop.Roll(r, LoopAt(r, 11, 6));
            long afterForward = r.Profile.Gold;
            Check(!DailyLoop.ClaimCalendar(r, LoopAt(r, 10, 20)).Success && r.Profile.Gold == afterForward,
                  "and a claim stamped on a day already used stays used however the clock moves");

            // the weekly counters never go backward either
            GameSession wk = LoopSession(db, 12);
            for (int d = 20; d < 25; d++) LoopClaimAll(wk, LoopAt(wk, d, 7));
            long weekHeld = wk.Profile.DailyLoop.WeekIndex;
            int daysHeld = wk.Profile.DailyLoop.WeekDays;
            DailyLoop.Roll(wk, LoopAt(wk, 3, 7));
            Check(wk.Profile.DailyLoop.WeekIndex == weekHeld && wk.Profile.DailyLoop.WeekDays == daysHeld,
                  "and the week's stamp and count stay where they were when the clock goes back a long way");
        }

        // ------------------------------------------------------------------ the save

        private static void CheckDailyLoopSave(ContentDatabase db)
        {
            // ---- a rich state, round trip
            GameSession s = LoopSession(db, 12);
            long t0 = LoopAt(s, 4, 6);
            LoopSetBoard(s, t0, "waves_5", "stage_2", "level_3", "summon_1", "waves_6f");
            s.Profile.DailyLoop.Find("waves_5").Progress = 5;
            DailyLoop.ClaimOrder(s, "waves_5", t0);
            s.Profile.DailyLoop.Find("stage_2").Progress = 1;
            DailyLoop.ClaimCalendar(s, t0);
            DailyLoop.ClaimCalendar(s, LoopAt(s, 5, 6));
            s.Profile.DailyLoop.WeekDays = 3;
            string json = SaveSerializer.Serialize(s.Profile);
            string error;
            PlayerProfile back = SaveSerializer.Deserialize(json, db, out error);
            DailyLoopState a = s.Profile.DailyLoop, b = back == null ? null : back.DailyLoop;
            bool same = b != null && a.DayUnix == b.DayUnix && a.ChestClaimed == b.ChestClaimed && a.WeekIndex == b.WeekIndex
                        && a.WeekDays == b.WeekDays && a.WeekChestClaimed == b.WeekChestClaimed && a.CalendarNext == b.CalendarNext
                        && a.CalendarDayUnix == b.CalendarDayUnix && a.Orders.Count == b.Orders.Count;
            for (int i = 0; same && i < a.Orders.Count; i++)
                same = a.Orders[i].Id == b.Orders[i].Id && a.Orders[i].Progress == b.Orders[i].Progress && a.Orders[i].Claimed == b.Orders[i].Claimed;
            Check(error == null && same && SaveSerializer.Serialize(back) == json,
                  "SAVE 22 ROUND TRIP: a board with progress and a claim, a calendar at day 3 and a week at 3 days saves and loads back identical, "
                  + "and saves again byte for byte (" + json.Length + " characters)");
            Check(JsonValue.Parse(json).GetInt("version", 0) == SaveSerializer.CurrentVersion && SaveSerializer.CurrentVersion == 22 && json.Contains("\"dailyLoop\""),
                  "and it is stamped version 22, with the loop under its own key");

            bool was = DailyLoopSwitch.Enabled;
            try
            {
                DailyLoopSwitch.Enabled = false;
                PlayerProfile off = SaveSerializer.Deserialize(json, db, out error);
                Check(off != null && SaveSerializer.Serialize(off) == json,
                      "with the switch off the loop's state is still read and written back untouched: turning it off costs a player nothing");
            }
            finally { DailyLoopSwitch.Enabled = was; }

            // ---- the pre-bump load: a version 21 save
            // Played with the switch off, so its loop is empty the way a save written by version 21 is.
            DailyLoopSwitch.Enabled = false;
            GameSession played = LoopSession(db, 5);
            HeroInstance hero = played.Profile.Heroes[0];
            played.Profile.Gold = 777777;
            played.LevelUpHero(hero, 3, LoopBase);
            played.Profile.Shards = 4321;
            played.Profile.TutorialStep = 3;
            played.Profile.SeenTips.Add("Summon");
            string v22 = SaveSerializer.Serialize(played.Profile);
            DailyLoopSwitch.Enabled = was;
            Check(!v22.Contains("\"dailyLoop\""), "a profile whose loop is empty writes no dailyLoop key at all");

            string v21 = v22.Replace("\"version\": " + SaveSerializer.CurrentVersion, "\"version\": 21");
            PlayerProfile old = SaveSerializer.Deserialize(v21, db, out error);
            bool kept = old != null && old.Gold == played.Profile.Gold && old.Shards == played.Profile.Shards
                        && old.Heroes.Count == played.Profile.Heroes.Count && old.Heroes[0].Level == hero.Level
                        && old.ClearedStageIds.Count == played.Profile.ClearedStageIds.Count && old.TutorialStep == 3
                        && old.SeenTips.Count == played.Profile.SeenTips.Count;
            Check(error == null && kept && old.DailyLoop.IsEmpty,
                  "A VERSION 21 SAVE LOADS WITH EVERYTHING KEPT and an empty loop: gold " + (old == null ? -1 : old.Gold) + ", " + (old == null ? -1 : old.Heroes.Count)
                  + " heroes, level " + (old == null ? -1 : old.Heroes[0].Level) + ", " + (old == null ? -1 : old.ClearedStageIds.Count) + " cleared stages, the tutorial step and tips");
            Check(old != null && SaveSerializer.Serialize(old) == v22,
                  "and saves again as exactly the same document at version 22: the migration invents and drops nothing");

            // ---- damaged blocks read as empty rather than failing the load
            string[] damaged = { "\"dailyLoop\": \"x\"", "\"dailyLoop\": 7", "\"dailyLoop\": []", "\"dailyLoop\": { \"orders\": 3, \"day\": \"x\" }" };
            int fine = 0;
            for (int i = 0; i < damaged.Length; i++)
            {
                string bad = v22.Substring(0, v22.LastIndexOf('}')).TrimEnd() + ",\n  " + damaged[i] + "\n}";
                PlayerProfile loaded = SaveSerializer.Deserialize(bad, db, out error);
                if (loaded != null && error == null && loaded.DailyLoop != null && loaded.DailyLoop.IsEmpty && loaded.Gold == played.Profile.Gold) fine++;
            }
            Check(fine == damaged.Length, "a damaged dailyLoop block (a string, a number, an array, wrong field types) loads as an empty loop, " + fine + " of " + damaged.Length);

            string negative = v22.Substring(0, v22.LastIndexOf('}')).TrimEnd()
                + ",\n  \"dailyLoop\": { \"day\": -5, \"weekDays\": 99, \"calendarNext\": 40, \"orders\": [ { \"id\": \"x\", \"progress\": -3 } ] }\n}";
            PlayerProfile clamped = SaveSerializer.Deserialize(negative, db, out error);
            Check(clamped != null && clamped.DailyLoop.DayUnix == 0 && clamped.DailyLoop.WeekDays == 7 && clamped.DailyLoop.CalendarNext == 6
                  && clamped.DailyLoop.Orders.Count == 1 && clamped.DailyLoop.Orders[0].Progress == 0,
                  "and out-of-range numbers are clamped to what the game could have written");
        }

        // ------------------------------------------------------------------ orders.json is not hashed

        private static void CheckDailyLoopNotHashed(ContentDatabase db, string contentDir)
        {
            string[] hashed = { "heroes", "enemies", "campaign", "balance", "relics", "events", "armor", "floor-conditions" };
            string[] texts = new string[hashed.Length];
            for (int i = 0; i < hashed.Length; i++)
            {
                string path = Path.Combine(contentDir, hashed[i] + ".json");
                texts[i] = File.Exists(path) ? File.ReadAllText(path) : null;
            }
            Check(ContentDatabase.VersionOf(texts) == db.ContentVersion,
                  "ORDERS.JSON IS NOT HASHED: ContentVersion is exactly the hash of the eight fight files (" + db.ContentVersion
                  + "), and orders.json is not among them");

            string ordersText = File.ReadAllText(Path.Combine(contentDir, "orders.json"));
            string edited = ordersText.Replace("\"hours\": 0.75", "\"hours\": 9").Replace("\"hours\": 4,", "\"hours\": 40,");
            ContentDatabase other = LoadContent(contentDir);
            string before = other.ContentVersion;
            other.LoadOrders(edited);
            Check(other.Orders.DailyChestHours == 9 && other.Orders.WeeklyChestHours == 40 && other.ContentVersion == before && before == db.ContentVersion,
                  "so retuning every chest in it moves the loop's numbers (" + other.Orders.DailyChestHours + " h, " + other.Orders.WeeklyChestHours
                  + " h) and not ContentVersion, which keeps every recorded guild dungeon replay");

            other.LoadOrders(null);
            GameSession withOrders = new GameSession(db, GameSession.CreateNewProfile(db, LoopBase));
            GameSession withoutOrders = new GameSession(other, GameSession.CreateNewProfile(other, LoopBase));
            StageDef stage = db.AllStages[0];
            string fightWith = FingerprintOf(CombatSimulation.Run(
                withOrders.BuildBattleRequest(CampaignWaveGenerator.BuildBattle(stage, 1, 1), 4242)));
            string fightWithout = FingerprintOf(CombatSimulation.Run(
                withoutOrders.BuildBattleRequest(CampaignWaveGenerator.BuildBattle(other.AllStages[0], 1, 1), 4242)));
            Check(!other.Orders.IsUsable && other.ContentVersion == before && fightWith == fightWithout,
                  "and a fight is the same event for event with the whole file removed: nothing a fight reads is in it");
        }

        // ------------------------------------------------------------------ the screen and the hub

        /// <summary>
        /// WIRING TRIPWIRES, not behavioural proof: the screen and the hub cannot run in the harness,
        /// so this reads their source. It fails if the door stops being behind the switch, if the
        /// screen starts to decide anything, or if the capture targets lose their forced switch. What
        /// the screen then looks like is for the Play queue.
        /// </summary>
        private static void CheckDailyLoopWiring(string contentDir)
        {
            string root = Path.GetFullPath(Path.Combine(contentDir, "..", "..", ".."));
            string ui = Path.Combine(root, "Assets", "Scripts", "UI");
            string screenPath = Path.Combine(ui, "Screens", "OrdersScreen.cs");
            string hubPath = Path.Combine(ui, "Screens", "MainMenuScreen.cs");
            string routerPath = Path.Combine(ui, "ScreenRouter.cs");
            string shotsPath = Path.Combine(root, "Assets", "Editor", "PlayModeScreenshot.cs");
            if (!File.Exists(screenPath) || !File.Exists(hubPath) || !File.Exists(routerPath) || !File.Exists(shotsPath))
            {
                Check(false, "the daily loop's screen, hub, router and capture files are where the harness expects them");
                return;
            }
            string screen = File.ReadAllText(screenPath), hub = File.ReadAllText(hubPath);
            string router = File.ReadAllText(routerPath), shots = File.ReadAllText(shotsPath);

            int doorAt = hub.IndexOf("if (DailyLoopSwitch.Enabled)", StringComparison.Ordinal);
            int orderAt = hub.IndexOf("RailButton(\"Orders\"", StringComparison.Ordinal);
            int moreAt = hub.IndexOf("RailButton(\"More\"", StringComparison.Ordinal);
            Check(doorAt >= 0 && orderAt > doorAt && orderAt < moreAt && hub.Contains("int slot = 3;")
                  && hub.Contains("_overflowButtonTop = top + step * slot;"),
                  "WIRING (reads source): the hub's Orders door is built only inside 'if (DailyLoopSwitch.Enabled)', and More keeps slot 3 when it is off, so the rail is the four it always was");
            Check(router.Contains("if (Emberfall.Core.Session.DailyLoopSwitch.Enabled) Orders = Create<Screens.OrdersScreen>();"),
                  "WIRING (reads source): the router builds the Orders page only with the switch on, so a build without it has no such screen");

            bool thinScreen = !screen.Contains(".Profile") && !screen.Contains("Content.Orders") && !screen.Contains("Balance")
                              && screen.Contains("DailyLoop.ClaimOrder(") && screen.Contains("DailyLoop.ClaimDailyChest(")
                              && screen.Contains("DailyLoop.ClaimWeeklyChest(") && screen.Contains("DailyLoop.ClaimCalendar(")
                              && screen.Contains("DailyLoop.Board(");
            Check(thinScreen,
                  "WIRING (reads source): the Orders page reads only DailyLoop.Board and calls only DailyLoop's four claims - it touches no profile, no content and no balance, so it decides nothing");

            List<string> others = new List<string>();
            foreach (string file in Directory.GetFiles(ui, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (name == "OrdersScreen.cs") continue;
                string text = File.ReadAllText(file);
                if (text.Contains("OrderView") || text.Contains("CalendarView")) others.Add(name);
            }
            Check(others.Count == 0,
                  "WIRING (reads source): all of the loop's layout is in the one screen file; no other UI file draws an order or a calendar day"
                  + (others.Count > 0 ? " - found in " + string.Join(", ", others.ToArray()) : ""));

            string[] targets = { "Orders", "OrdersHalf", "OrdersClaimed", "OrdersCalendar1", "OrdersCalendar4", "HubLoopOn", "HubLoopOff" };
            List<string> missing = new List<string>();
            for (int i = 0; i < targets.Length; i++)
                if (!shots.Contains("Begin(\"Screen:" + targets[i] + "\")")) missing.Add(targets[i]);
            Check(missing.Count == 0 && shots.Contains("LoopForceKey") && shots.Contains("DailyLoopSwitch.Enabled = true;")
                  && shots.Contains("DailyLoopSwitch.Enabled = false;") && shots.Contains("EditorCaptureGuard.EnterPlayMode();"),
                  "WIRING (reads source): the seven daily-loop capture targets exist, each forces the switch for its run, and Begin still enters Play mode through the offline guard"
                  + (missing.Count > 0 ? " - missing: " + string.Join(", ", missing.ToArray()) : ""));
        }

        // ------------------------------------------------------------------ the switch

        /// <summary>One scripted session through the game's own calls: waves, a sweep, levels, a
        /// summon, a Proving and a Recital fight, an idle claim. Deterministic by its seeds and
        /// clock, so two runs differ only in what the daily loop does.</summary>
        private static GameSession LoopScript(ContentDatabase db)
        {
            GameSession s = LoopSession(db, 0);
            StageDef first = db.AllStages[0];
            long t0 = LoopAt(s, 1, 6);
            int resolves = 0;
            while (!s.Profile.HasClearedStage(first.Id) && resolves < 60)
            {
                s.ResolveWave(first, 7100 + resolves * 101, t0 + resolves);
                resolves++;
            }
            s.SweepClearedStage(first, 8800, t0 + 500);
            s.Profile.Gold += 100000;
            s.LevelUpHero(s.Profile.Heroes[0], 2, t0 + 600);
            s.Profile.Shards += db.Balance.SummonCost * 2;
            s.Summon(9302, t0 + 700);
            s.Summon(9303, t0 + 701);
            s.GrantProvingResult(true, t0 + 800);
            s.GrantRecitalResult(1, true, t0 + 801);
            s.ClaimIdle(t0 + 3600);
            return s;
        }

        private static void CheckDailyLoopOff(ContentDatabase db)
        {
            bool was = DailyLoopSwitch.Enabled;
            try
            {
                DailyLoopSwitch.Enabled = false;
                GameSession off = LoopScript(db);
                string offJson = SaveSerializer.Serialize(off.Profile);
                long t = LoopAt(off, 2, 6);
                long gold = off.Profile.Gold, shards = off.Profile.Shards;

                LoopBoard board = DailyLoop.Board(off, t);
                bool refused = !DailyLoop.ClaimOrder(off, "waves_3", t).Success && !DailyLoop.ClaimDailyChest(off, t).Success
                               && !DailyLoop.ClaimWeeklyChest(off, t).Success && !DailyLoop.ClaimCalendar(off, t).Success
                               && !DailyLoop.CanClaimCalendar(off, t) && !DailyLoop.Roll(off, t);
                Check(!DailyLoop.Active(off) && !board.Active && board.Orders.Count == 0 && !board.AnythingToClaim && refused
                      && off.Profile.Gold == gold && off.Profile.Shards == shards,
                      "ROLLBACK: with the switch off the loop is inert: no board, no claim of any kind, nothing paid");
                Check(off.Profile.DailyLoop.IsEmpty && !offJson.Contains("dailyLoop") && SaveSerializer.Serialize(off.Profile) == offJson,
                      "and a whole session of waves, a sweep, levels, summons and arena fights records nothing and writes no loop key");

                // The same session with the switch on differs from it in the loop's key and in nothing else.
                DailyLoopSwitch.Enabled = true;
                GameSession on = LoopScript(db);
                bool counted = !on.Profile.DailyLoop.IsEmpty;
                on.Profile.DailyLoop = new DailyLoopState();
                string onWithoutLoop = SaveSerializer.Serialize(on.Profile);
                Check(counted && onWithoutLoop == offJson,
                      "and the same session with the switch on is byte for byte the same save once the loop's own key is set aside ("
                      + offJson.Length + " characters compared): the loop's hooks change no gold, shard, level, roster, cursor or clock");

                // The off save is origin/main's document apart from the version stamp and the one key that is absent.
                JsonValue root = JsonValue.Parse(offJson);
                List<string> keys = new List<string>();
                foreach (string key in root.Keys) keys.Add(key);
                Check(!keys.Contains("dailyLoop") && keys.Count == 40 && keys.Contains("tutorialStep") && keys.Contains("seenTips") && keys.Contains("dailyResetUnix"),
                      "the off save carries the 40 keys version 21 wrote and no more (" + keys.Count + " keys), so only the version number differs from origin/main's save");
            }
            finally { DailyLoopSwitch.Enabled = was; }
        }
    }
}
