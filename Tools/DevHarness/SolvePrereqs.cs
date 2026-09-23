using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Emberfall.Core.Cloud;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE SOLVE'S PREREQUISITES: the instruments the big campaign solve depends on, read before it
    /// runs rather than discovered by it. Run with --solve-prereqs [speed|refline|quick]. Not a
    /// check and not run by the gate. It asserts nothing, writes nothing and changes no content.
    /// Docs/SOLVE-PREREQS.md is the reading.
    ///
    /// - speed: what StatMath.PowerScore's linear price on Speed costs each system that builds a
    ///   fight to a power score. Each is read at the score it builds to, as a win rate, and then
    ///   again with ten percent of the built Speed traded for Attack of exactly equal score
    ///   (ISO-SCORE). The score cannot tell those arms apart, so the win-rate gap between them is
    ///   difficulty the score does not see.
    /// - refline: the reference player recommendedPower is derived from, under the role-blind line
    ///   policy it had and the role-sane one it has now (WantedLine). Twenty walks each, the
    ///   solver's first-iteration proposal each, both lines fought at equal power, and the multiple
    ///   of recommendedPower each needs to fight as the instrument line does (R4).
    ///
    /// A partial of Program for the reason TempoStudy and WallResolve are: the arenas are the
    /// instruments the harness already uses, not copies of them.
    /// </summary>
    internal static partial class Program
    {
        private static int _spScale = 1;

        private static int RunSolvePrereqs(string contentDir, string mode)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            ContentDatabase db = LoadContent(contentDir);
            if (db == null) return 1;
            _spScale = mode == "quick" ? 1 : 4;
            Stopwatch clock = Stopwatch.StartNew();
            Line("SOLVE PREREQUISITES (--solve-prereqs " + (mode ?? "full") + ")");
            Line("deltas are PAIRED: same seeds in both arms, mean of the per-seed difference, +/- one standard error");
            Line("");
            if (mode == null || mode == "quick" || mode == "speed")
            {
                SpSpeedProfile(db);
                SpSpeedCampaign(db);
                SpSpeedProving(db);
                SpSpeedGuild(db);
            }
            if (mode == null || mode == "quick" || mode == "refline") SpRefLine(db);
            Line("");
            Line("solve prerequisites done in " + (clock.ElapsedMilliseconds / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + "s");
            return 0;
        }

        // ================================================================== iso-score variants

        /// <summary>
        /// The hero with its BUILT Speed multiplied by f at this Level (as SpeedTimes does) and its
        /// BaseAttack re-solved so the line's power score, armor included, is what it was. The
        /// score then cannot tell the two apart; only the fight can.
        /// </summary>
        private static HeroDef IsoSpeedHero(HeroDef d, int level, float f, List<ArmorPiece> armor, ArmorConfig cfg)
        {
            int target = StatMath.PowerScore(StatMath.ForHero(d, level, null, armor, cfg));
            HeroDef c = SpeedTimes(d, level, f);
            for (int k = 0; k < 6; k++)
            {
                UnitStats st = StatMath.ForHero(c, level, null, armor, cfg);
                int p = StatMath.PowerScore(st);
                if (p == target || st.Attack <= 0f) break;
                float wantAtk = st.Attack + (target - p) / 2.4f;
                c.BaseAttack = c.BaseAttack * wantAtk / st.Attack;
            }
            return c;
        }

        /// <summary>The same move on an enemy template, in place, at one level. Returns what it
        /// replaced so the caller can put it back.</summary>
        private static float[] IsoSpeedEnemyInPlace(EnemyDef d, int level, float f)
        {
            float[] was = { d.BaseSpeed, d.BaseAttack };
            int target = StatMath.PowerScore(StatMath.ForEnemy(d, level));
            float grown = (Math.Max(1, level) - 1) * 0.35f;
            d.BaseSpeed = (d.BaseSpeed + grown) * f - grown;
            for (int k = 0; k < 6; k++)
            {
                UnitStats st = StatMath.ForEnemy(d, level);
                int p = StatMath.PowerScore(st);
                if (p == target || st.Attack <= 0f) break;
                float wantAtk = st.Attack + (target - p) / 2.4f;
                d.BaseAttack = d.BaseAttack * wantAtk / st.Attack;
            }
            return was;
        }

        private static double SpMeanSpeed(List<UnitStats> units)
        {
            double s = 0;
            for (int i = 0; i < units.Count; i++) s += units[i].Speed;
            return units.Count == 0 ? 0 : s / units.Count;
        }

        private static string SpWr(TempoObs[] a) { return F(Mean(a, o => o.Win) * 100, "0.0") + "%"; }

        private static string SpD(TempoObs[] control, TempoObs[] treated)
        {
            double m, se;
            Paired(control, treated, o => o.Win, out m, out se);
            return (m >= 0 ? "+" : "") + F(m * 100, "0.0") + " +/-" + F(se * 100, "0.0");
        }

        /// <summary>The same paired delta on the margin, which still moves where the win rate is
        /// pinned at 0% or 100%.</summary>
        private static string SpDm(TempoObs[] control, TempoObs[] treated)
        {
            double m, se;
            Paired(control, treated, o => o.Margin, out m, out se);
            return (m >= 0 ? "+" : "") + F(m, "0.000") + " +/-" + F(se, "0.000");
        }

        // ================================================================== 1. where Speed sits

        private static void SpSpeedProfile(ContentDatabase db)
        {
            Line("S0. WHERE SPEED SITS. Built Speed is BaseSpeed + 0.35 per Level for heroes and enemies alike (READ: StatMath.Build);");
            Line("    HP, Attack and Defense grow by the def's growth rate instead. The spread a real line can differ by at one score:");
            for (int stars = 1; stars <= 5; stars++)
            {
                List<float> v = new List<float>();
                for (int i = 0; i < db.Heroes.Count; i++) if (db.Heroes[i].Stars == stars) v.Add(StatMath.ForHero(db.Heroes[i], 9).Speed);
                if (v.Count == 0) continue;
                v.Sort();
                Line("    " + stars + "-star heroes at Level 9: Speed min " + F(v[0], "0") + ", median " + F(v[v.Count / 2], "0")
                     + ", max " + F(v[v.Count - 1], "0") + " (" + v.Count + " heroes)");
            }
            List<float> ev = new List<float>();
            for (int i = 0; i < db.Enemies.Count; i++) ev.Add(db.Enemies[i].BaseSpeed);
            ev.Sort();
            Line("    enemy templates: BaseSpeed min " + F(ev[0], "0") + ", median " + F(ev[ev.Count / 2], "0") + ", max " + F(ev[ev.Count - 1], "0"));
            Line("    per chapter, the CEIL battle's mean enemy Speed against the instrument line's, both at the stage's seeded numbers:");
            List<double> perStage = new List<double>();
            for (int ch = 1; ch <= 6; ch++)
            {
                double es = 0, hs = 0; int n = 0;
                for (int s = 0; s < db.AllStages.Count; s++)
                {
                    StageDef st = db.AllStages[s];
                    if (st.ChapterNumber != ch) continue;
                    StageDef ceil = TempoCeil(st);
                    List<UnitStats> e = new List<UnitStats>();
                    for (int i = 0; i < ceil.Enemies.Count; i++)
                    {
                        EnemyDef d = db.GetEnemy(ceil.Enemies[i].EnemyId);
                        if (d != null) e.Add(StatMath.ForEnemy(d, ceil.Enemies[i].Level));
                    }
                    GameSession line = BuildPinnedLine(db, st, true);
                    List<UnitStats> h = new List<UnitStats>();
                    List<HeroInstance> team = line.Profile.TeamHeroes();
                    for (int i = 0; i < team.Count; i++)
                        h.Add(StatMath.ForHero(team[i].Def, team[i].Level, null, line.Profile.EquippedArmorOf(team[i]), db.Armor));
                    es += SpMeanSpeed(e); hs += SpMeanSpeed(h); n++;
                    perStage.Add(SpMeanSpeed(h) / Math.Max(1e-9, SpMeanSpeed(e)));
                }
                if (n > 0) Line("      chapter " + ch + ": enemies " + F(es / n, "0.0") + ", instrument line " + F(hs / n, "0.0")
                                + " (x" + F(hs / Math.Max(1e-9, es), "0.00") + ")");
            }
            perStage.Sort();
            Line("    per stage, line Speed over CEIL enemy Speed: min x" + F(perStage[0], "0.00") + ", tenth x" + F(perStage[perStage.Count / 10], "0.00")
                 + ", median x" + F(perStage[perStage.Count / 2], "0.00") + ", ninetieth x" + F(perStage[perStage.Count * 9 / 10], "0.00")
                 + ", max x" + F(perStage[perStage.Count - 1], "0.00") + "; faster than the enemy line at " + perStage.Count(v => v >= 1.0) + " of " + perStage.Count);
            Line("");
        }

        // ================================================================== 1a. the campaign

        /// <summary>
        /// recommendedPower and the solve. The instrument line levelled and geared to each stage's
        /// recommendedPower (BuildPinnedLine), against the stage's CEIL battle, which the CEIL band
        /// says it should clear 85% of the time, 65% at a finale. Arms: as built, and ten percent
        /// of the built Speed traded each way for Attack at exactly equal score.
        /// </summary>
        private static void SpSpeedCampaign(ContentDatabase db)
        {
            int n = 10 * _spScale;
            Line("S1. THE CAMPAIGN (recommendedPower and the solve): instrument line at each stage's recommendedPower, CEIL battle,");
            Line("    " + db.AllStages.Count + " stages x " + n + " seeds per arm. ISO-SCORE: Speed x0.90 or x1.10, Attack re-solved so every hero's score is unchanged.");
            List<TempoObs[]> all0 = new List<TempoObs[]>(), allLo = new List<TempoObs[]>(), allHi = new List<TempoObs[]>();
            List<TempoObs[]> allS = new List<TempoObs[]>(), allA = new List<TempoObs[]>();
            int residual = 0;
            for (int ch = 1; ch <= 6; ch++)
            {
                List<TempoObs[]> b = new List<TempoObs[]>(), lo = new List<TempoObs[]>(), hi = new List<TempoObs[]>();
                List<TempoObs[]> sp = new List<TempoObs[]>(), at = new List<TempoObs[]>();
                for (int s = 0; s < db.AllStages.Count; s++)
                {
                    StageDef st = db.AllStages[s];
                    if (st.ChapterNumber != ch) continue;
                    StageDef ceil = TempoCeil(st);
                    GameSession session = BuildPinnedLine(db, st, true);
                    PlayerProfile p = session.Profile;
                    int before = p.EffectiveTeamPower(db);
                    Func<float, Func<HeroInstance, HeroDef>> iso = f => h => IsoSpeedHero(h.Def, h.Level, f, p.EquippedArmorOf(h), db.Armor);
                    // The score of the variant line, read through the same entry point the game uses.
                    List<HeroInstance> team = p.TeamHeroes();
                    HeroDef[] was = team.Select(h => h.Def).ToArray();
                    for (int i = 0; i < team.Count; i++) team[i].Def = iso(1.1f)(team[i]);
                    residual = Math.Max(residual, Math.Abs(p.EffectiveTeamPower(db) - before));
                    for (int i = 0; i < team.Count; i++) team[i].Def = was[i];

                    b.Add(TempoCampaign(session, ceil, st, n, null, null, null));
                    lo.Add(TempoCampaign(session, ceil, st, n, iso(0.9f), null, null));
                    hi.Add(TempoCampaign(session, ceil, st, n, iso(1.1f), null, null));

                    // The tempo study's own two arms, rebuilt on this content: +10% Speed with the
                    // score allowed to rise, and the Attack that raises the score by exactly as much
                    // (TempoPowerPrice's arithmetic). Iso-score is the difference of these two by
                    // construction, so the pair says whether the premise still holds here.
                    Dictionary<string, float> atkFor10 = new Dictionary<string, float>();
                    for (int i = 0; i < team.Count; i++)
                    {
                        UnitStats worn = StatMath.ForHero(team[i].Def, team[i].Level, null, p.EquippedArmorOf(team[i]), db.Armor);
                        UnitStats bare = StatMath.ForHero(team[i].Def, team[i].Level);
                        atkFor10[team[i].Uid] = 1f + (float)(0.10 * worn.Speed * 1.9 / 2.4 / Math.Max(1f, bare.Attack));
                    }
                    sp.Add(TempoCampaign(session, ceil, st, n, h => SpeedTimes(h.Def, h.Level, 1.1f), null, null));
                    at.Add(TempoCampaign(session, ceil, st, n, h => AttackTimes(h.Def, atkFor10[h.Uid]), null, null));
                }
                TempoObs[] bb = Concat(b), ll = Concat(lo), hh = Concat(hi), ss = Concat(sp), aa = Concat(at);
                all0.AddRange(b); allLo.AddRange(lo); allHi.AddRange(hi); allS.AddRange(sp); allA.AddRange(at);
                Line("    chapter " + ch + ": as built " + SpWr(bb).PadLeft(6) + "   ISO x0.90 " + SpD(bb, ll).PadLeft(13)
                     + "pp   ISO x1.10 " + SpD(bb, hh).PadLeft(13) + "pp (margin " + SpDm(bb, hh) + ")   |   +10% Speed " + SpD(bb, ss).PadLeft(13)
                     + "pp, same power as Attack " + SpD(bb, aa).PadLeft(13) + "pp");
            }
            TempoObs[] a0 = Concat(all0), aL = Concat(allLo), aH = Concat(allHi), aS = Concat(allS), aA = Concat(allA);
            Line("    all 72: as built " + SpWr(a0) + ", ISO x0.90 " + SpD(a0, aL) + "pp (margin " + SpDm(a0, aL) + "), ISO x1.10 " + SpD(a0, aH)
                 + "pp (margin " + SpDm(a0, aH) + "); the same score spans " + SpD(aL, aH) + "pp of win rate.");
            Line("    all 72, the tempo study's arms: +10% Speed " + SpD(a0, aS) + "pp (margin " + SpDm(a0, aS) + "), the same power as Attack "
                 + SpD(a0, aA) + "pp (margin " + SpDm(a0, aA) + "); Speed minus Attack " + SpD(aA, aS) + "pp.");
            // Speed moves a winner only in a fight near even (TEMPO-STUDY section 4), and most seeded
            // stages are not: they read 0% or 100%. So the same arms pooled over the stages whose
            // as-built rate sits between 15% and 95%, which is what every stage is after a solve.
            List<TempoObs[]> c0 = new List<TempoObs[]>(), cL = new List<TempoObs[]>(), cH = new List<TempoObs[]>(), cS = new List<TempoObs[]>(), cA = new List<TempoObs[]>();
            for (int i = 0; i < all0.Count; i++)
            {
                double wr = Mean(all0[i], o => o.Win);
                if (wr < 0.15 || wr > 0.95) continue;
                c0.Add(all0[i]); cL.Add(allLo[i]); cH.Add(allHi[i]); cS.Add(allS[i]); cA.Add(allA[i]);
            }
            if (c0.Count > 0)
            {
                TempoObs[] k0 = Concat(c0), kL = Concat(cL), kH = Concat(cH), kS = Concat(cS), kA = Concat(cA);
                Line("    CLOSE STAGES ONLY (" + c0.Count + " of " + all0.Count + " read 15-95% as built, together " + SpWr(k0) + "): ISO x0.90 " + SpD(k0, kL)
                     + "pp, ISO x1.10 " + SpD(k0, kH) + "pp, spread " + SpD(kL, kH) + "pp; +10% Speed " + SpD(k0, kS) + "pp, the same power as Attack "
                     + SpD(k0, kA) + "pp, Speed minus Attack " + SpD(kA, kS) + "pp.");
            }
            Line("    Largest score residual after the iso re-solve: " + residual + " points.");
            Line("");
        }

        // ================================================================== 1b. the Proving

        /// <summary>
        /// The Proving's opponent generation. ProvingGenerator.Generate draws five heroes from the
        /// whole roster and solves one Level so their score lands on ProvingTargetPower - the
        /// player's bare score, times 1 + 1% per standing. At standing 0 the score says an even
        /// fight. Read two ways: the natural spread (the opponent's own Speed against the
        /// player's, bucketed), and the iso-score arm on the player's line.
        /// </summary>
        private static void SpSpeedProving(ContentDatabase db)
        {
            int n = 60 * _spScale;
            Line("S2. THE PROVING (opponent generation at standing 0, where the score says an even fight):");
            Line("    the player line geared at a stage's depth and levelled to its recommendedPower; " + n + " generated opponents per row.");
            Line("    'bare' is the opponent's target as the game computes it (GameSession.ProvingTargetPower: no armor), 'eff' the line's worn score.");
            List<TempoObs> pB = new List<TempoObs>(), pL = new List<TempoObs>(), pH = new List<TempoObs>();
            string[][] lines = { PinnedLine, BalanceLineIds() };
            string[] names = { "pinned", "balance" };
            string[] at = { "ch1_12", "ch2_12", "ch4_6" };
            StageDef arena = CampaignWaveGenerator.BuildBattle(db.AllStages[0], 1, 1);
            List<float> ratios = new List<float>(); List<float> wins = new List<float>();
            for (int li = 0; li < lines.Length; li++)
            {
                for (int ai = 0; ai < at.Length; ai++)
                {
                    StageDef st = db.GetStage(at[ai]);
                    if (st == null) continue;
                    GameSession session = BuildLineAt(db, st, true, st.RecommendedPower, lines[li]);
                    int bare = session.ProvingTargetPower();
                    int eff = session.Profile.EffectiveTeamPower(db);
                    List<HeroInstance> team = session.Profile.TeamHeroes();
                    List<UnitStats> mine = new List<UnitStats>();
                    for (int i = 0; i < team.Count; i++)
                        mine.Add(StatMath.ForHero(team[i].Def, team[i].Level, null, session.Profile.EquippedArmorOf(team[i]), db.Armor));
                    double mySpeed = SpMeanSpeed(mine);

                    TempoObs[] b = new TempoObs[n], lo = new TempoObs[n], hi = new TempoObs[n];
                    int oppPower = 0;
                    List<KeyValuePair<float, float>> rows = new List<KeyValuePair<float, float>>();
                    HeroDef[] was = team.Select(h => h.Def).ToArray();
                    for (int s = 0; s < n; s++)
                    {
                        int seed = ProvingGenerator.SeedFor(0, s);
                        BattleLoadout opp = ProvingGenerator.Generate(db, db.Balance, 0, bare, seed);
                        oppPower += opp.PowerScore;
                        List<UnitStats> theirs = new List<UnitStats>();
                        for (int u = 0; u < opp.Units.Count; u++)
                        {
                            HeroDef d = db.GetHero(opp.Units[u].DefId);
                            theirs.Add(StatMath.ForOpponent(d, opp.Units[u].Level, opp.Units[u].Stars, null, opp.Units[u].Armor, db.Armor));
                        }
                        int fightSeed = 91000000 + s * 37 + ai * 7919 + li * 104729;
                        b[s] = TempoRead(CombatSimulation.Run(session.BuildProvingRequest(arena, opp, fightSeed)), null);
                        try
                        {
                            for (int i = 0; i < team.Count; i++) team[i].Def = IsoSpeedHero(was[i], team[i].Level, 0.9f, session.Profile.EquippedArmorOf(team[i]), db.Armor);
                            lo[s] = TempoRead(CombatSimulation.Run(session.BuildProvingRequest(arena, opp, fightSeed)), null);
                            for (int i = 0; i < team.Count; i++) team[i].Def = IsoSpeedHero(was[i], team[i].Level, 1.1f, session.Profile.EquippedArmorOf(team[i]), db.Armor);
                            hi[s] = TempoRead(CombatSimulation.Run(session.BuildProvingRequest(arena, opp, fightSeed)), null);
                        }
                        finally { for (int i = 0; i < team.Count; i++) team[i].Def = was[i]; }
                        float ratio = (float)(SpMeanSpeed(theirs) / Math.Max(1e-9, mySpeed));
                        rows.Add(new KeyValuePair<float, float>(ratio, b[s].Win));
                        ratios.Add(ratio); wins.Add(b[s].Win);
                    }
                    pB.AddRange(b); pL.AddRange(lo); pH.AddRange(hi);
                    rows.Sort((x, y) => x.Key.CompareTo(y.Key));
                    int third = rows.Count / 3;
                    double slow = rows.Take(third).Average(r => r.Value), fast = rows.Skip(rows.Count - third).Average(r => r.Value);
                    Line("    " + names[li].PadRight(8) + " at " + st.DisplayCode.PadRight(5) + " bare " + bare + ", eff " + eff
                         + ", opponents' mean score " + (oppPower / n) + ": WR " + SpWr(b)
                         + "  | opponent slower third (Speed x" + F(rows[third / 2].Key, "0.00") + ") " + F(slow * 100, "0") + "%, faster third (x"
                         + F(rows[rows.Count - 1 - third / 2].Key, "0.00") + ") " + F(fast * 100, "0") + "%"
                         + "  | player Speed x0.90 iso " + SpD(b, lo) + "pp, x1.10 iso " + SpD(b, hi) + "pp");
                }
            }
            TempoObs[] PB = pB.ToArray(), PL = pL.ToArray(), PH = pH.ToArray();
            Line("    pooled over the six rows, " + PB.Length + " fights: WR " + SpWr(PB) + "; player Speed x0.90 iso " + SpD(PB, PL) + "pp (margin " + SpDm(PB, PL)
                 + "), x1.10 iso " + SpD(PB, PH) + "pp (margin " + SpDm(PB, PH) + "); the same score spans " + SpD(PL, PH) + "pp.");
            Line("    pooled, opponent Speed over player Speed against the win: rank correlation "
                 + F(GuildDungeonChecks.RankCorrelation(ratios.ToArray(), wins.ToArray()), "0.00") + " over " + ratios.Count + " fights.");
            Line("");
        }

        private static string[] BalanceLineIds()
        {
            return new string[] { "kael_the_unyielding", "kiva_the_forsaken_oath", "isara_the_second_chance",
                                  "adric_the_blank_map", "orelle_the_fever_warden" };
        }

        // ================================================================== 1c. the guild dungeon

        /// <summary>
        /// The guild dungeon's floor builder. GuildDungeonGenerator.BuildFloor solves one Level so
        /// the enemy line's score lands on TargetPowerForFloor; every member fights it at
        /// guildDungeonHeroLevel with no armor. Read with the harness's own sample population
        /// (GuildDungeonChecks.SampleRosters), no floor condition, and the iso-score move made on
        /// the ENEMY side, the side the score was built for.
        /// </summary>
        private static void SpSpeedGuild(ContentDatabase db)
        {
            int cycles = 3 * _spScale;
            List<GuildDungeonChecks.Sample> pop = GuildDungeonChecks.SampleRosters(db, 424242);
            int floors = Math.Max(1, db.Balance.GuildDungeonFloors);
            Line("S3. THE GUILD DUNGEON (floor builder): " + cycles + " cycles x " + floors + " floors, " + pop.Count
                 + " sample rosters each at Level " + db.Balance.GuildDungeonHeroLevel + ", no condition. Iso-score move on the enemies.");
            double heroSpeed = 0; int heroN = 0;
            for (int i = 0; i < pop.Count; i++)
            {
                TeamSnapshot line = GuildDungeon.NormaliseTeam(db, pop[i].Team);
                for (int u = 0; u < line.Units.Count; u++)
                { heroSpeed += StatMath.ForHero(db.GetHero(line.Units[u].DefId), line.Units[u].Level).Speed; heroN++; }
            }
            heroSpeed /= Math.Max(1, heroN);
            List<TempoObs> b = new List<TempoObs>(), lo = new List<TempoObs>(), hi = new List<TempoObs>();
            List<KeyValuePair<float, float>> floorRows = new List<KeyValuePair<float, float>>();
            for (int c = 0; c < cycles; c++)
            {
                int seed = GuildDungeon.SeedFor("solve-prereqs-" + c, 1);
                for (int f = 1; f <= floors; f++)
                {
                    StageDef floor = GuildDungeonGenerator.BuildFloor(db, seed, f);
                    List<UnitStats> e = new List<UnitStats>();
                    for (int i = 0; i < floor.Enemies.Count; i++) e.Add(StatMath.ForEnemy(db.GetEnemy(floor.Enemies[i].EnemyId), floor.Enemies[i].Level));
                    TempoObs[] fb = SpGuildFloor(db, pop, floor, seed, f, 1f, 0);
                    TempoObs[] fl = SpGuildFloor(db, pop, floor, seed, f, 0.9f, 0);
                    TempoObs[] fh = SpGuildFloor(db, pop, floor, seed, f, 1.1f, 0);
                    b.AddRange(fb); lo.AddRange(fl); hi.AddRange(fh);
                    floorRows.Add(new KeyValuePair<float, float>((float)(SpMeanSpeed(e) / heroSpeed), (float)Mean(fb, o => o.Win)));
                }
            }
            TempoObs[] B = b.ToArray(), L = lo.ToArray(), H = hi.ToArray();
            Line("    mean hero Speed " + F(heroSpeed, "0.0") + ". Clear rate as built " + SpWr(B) + "; enemy Speed x0.90 at equal score "
                 + SpD(B, L) + "pp (margin " + SpDm(B, L) + ", heroes' view), x1.10 " + SpD(B, H) + "pp (margin " + SpDm(B, H)
                 + "); the same floor score spans " + SpD(H, L) + "pp.");
            floorRows.Sort((x, y) => x.Key.CompareTo(y.Key));
            int third = Math.Max(1, floorRows.Count / 3);
            Line("    natural spread over " + floorRows.Count + " floors: the slowest third of enemy lines (x" + F(floorRows[0].Key, "0.00") + "-x"
                 + F(floorRows[third - 1].Key, "0.00") + " of hero Speed) are cleared " + F(floorRows.Take(third).Average(r => r.Value) * 100, "0")
                 + "%, the fastest third (x" + F(floorRows[floorRows.Count - third].Key, "0.00") + "-x" + F(floorRows[floorRows.Count - 1].Key, "0.00") + ") "
                 + F(floorRows.Skip(floorRows.Count - third).Average(r => r.Value) * 100, "0") + "%; rank correlation of enemy Speed with the clear rate "
                 + F(GuildDungeonChecks.RankCorrelation(floorRows.Select(r => r.Key).ToArray(), floorRows.Select(r => r.Value).ToArray()), "0.00")
                 + " (floors grow 5% in score each, so this mixes floor number in).");

            // At Level 40 nearly every floor is cleared, and a clear rate pinned at 100% cannot move.
            // So the same floors again with every member some levels lower: the first drop, in steps
            // of 5, at which cycle 0 is cleared at most 60% of the time. The floor is unchanged; only
            // the fight is closer, which is where the tempo study found Speed decides winners.
            int drop = 0;
            for (int d = 5; d <= 35; d += 5)
            {
                int seed0 = GuildDungeon.SeedFor("solve-prereqs-0", 1);
                List<TempoObs> probe = new List<TempoObs>();
                for (int f = 1; f <= floors; f++) probe.AddRange(SpGuildFloor(db, pop, GuildDungeonGenerator.BuildFloor(db, seed0, f), seed0, f, 1f, d));
                drop = d;
                if (Mean(probe.ToArray(), o => o.Win) <= 0.60) break;
            }
            List<TempoObs> cb = new List<TempoObs>(), cl = new List<TempoObs>(), chh = new List<TempoObs>();
            for (int c = 0; c < cycles; c++)
            {
                int seed = GuildDungeon.SeedFor("solve-prereqs-" + c, 1);
                for (int f = 1; f <= floors; f++)
                {
                    StageDef floor = GuildDungeonGenerator.BuildFloor(db, seed, f);
                    cb.AddRange(SpGuildFloor(db, pop, floor, seed, f, 1f, drop));
                    cl.AddRange(SpGuildFloor(db, pop, floor, seed, f, 0.9f, drop));
                    chh.AddRange(SpGuildFloor(db, pop, floor, seed, f, 1.1f, drop));
                }
            }
            TempoObs[] CB = cb.ToArray(), CL = cl.ToArray(), CH = chh.ToArray();
            Line("    CLOSE: the same floors with members at Level " + (db.Balance.GuildDungeonHeroLevel - drop) + ": clear rate " + SpWr(CB)
                 + "; enemy Speed x0.90 at equal score " + SpD(CB, CL) + "pp (margin " + SpDm(CB, CL) + "), x1.10 " + SpD(CB, CH)
                 + "pp (margin " + SpDm(CB, CH) + "); the same floor score spans " + SpD(CH, CL) + "pp.");
            Line("");
        }

        private static TempoObs[] SpGuildFloor(ContentDatabase db, List<GuildDungeonChecks.Sample> pop, StageDef floor, int seed, int f, float speed, int levelDrop)
        {
            Dictionary<EnemyDef, float[]> was = new Dictionary<EnemyDef, float[]>();
            try
            {
                if (speed != 1f)
                    for (int i = 0; i < floor.Enemies.Count; i++)
                    {
                        EnemyDef d = db.GetEnemy(floor.Enemies[i].EnemyId);
                        if (!was.ContainsKey(d)) was[d] = IsoSpeedEnemyInPlace(d, floor.Enemies[i].Level, speed);
                    }
                TempoObs[] obs = new TempoObs[pop.Count];
                for (int r = 0; r < pop.Count; r++)
                {
                    TeamSnapshot line = GuildDungeon.NormaliseTeam(db, pop[r].Team);
                    BattleRequest req = new BattleRequest();
                    req.Content = db;
                    req.Stage = floor;
                    req.Seed = GuildDungeon.FightSeedFor(seed, f) + r * 17;
                    for (int u = 0; u < line.Units.Count; u++)
                    {
                        HeroInstance hero = new HeroInstance();
                        hero.Uid = "sp_" + u;
                        hero.DefId = line.Units[u].DefId;
                        hero.Def = db.GetHero(hero.DefId);
                        hero.Level = Math.Max(1, line.Units[u].Level - levelDrop);
                        hero.SelectedTalent = line.Units[u].SelectedTalent;
                        req.Team.Add(hero);
                    }
                    obs[r] = TempoRead(CombatSimulation.Run(req), null);
                }
                return obs;
            }
            finally
            {
                foreach (KeyValuePair<EnemyDef, float[]> kv in was) { kv.Key.BaseSpeed = kv.Value[0]; kv.Key.BaseAttack = kv.Value[1]; }
            }
        }

        // ================================================================== 2. the reference player

        private sealed class SpPolicyReading
        {
            public string Name;
            public List<EconomyRun> Walks = new List<EconomyRun>();
            public Dictionary<string, int> Stuck = new Dictionary<string, int>();
            public int EntriesNoHealer, EntriesNoFront, Entries;
        }

        private static SpPolicyReading SpWalkPolicy(ContentDatabase db, bool roleBlind, int walks)
        {
            SpPolicyReading r = new SpPolicyReading();
            r.Name = roleBlind ? "role-blind (was)" : "role-sane (now)";
            bool had = _walkRoleBlind;
            _walkRoleBlind = roleBlind;
            try
            {
                for (int k = 0; k < walks; k++)
                {
                    EconomyRun run = SimulateEconomyUnder(db, WalkSeed(k), false, false);
                    r.Walks.Add(run);
                    if (run.StuckAt >= 0)
                    {
                        string code = db.AllStages[run.StuckAt].DisplayCode;
                        int n; r.Stuck.TryGetValue(code, out n); r.Stuck[code] = n + 1;
                    }
                    for (int i = 0; i < run.Stages.Count; i++)
                    {
                        string[] ids = run.Stages[i].EntryIds;
                        bool healer = false, front = false;
                        for (int j = 0; j < ids.Length; j++)
                        {
                            HeroDef d = db.GetHero(ids[j]);
                            if (d == null) continue;
                            if (d.Role == HeroRole.Mender) healer = true;
                            if (d.Role == HeroRole.Bulwark || d.Role == HeroRole.Vanguard) front = true;
                        }
                        r.Entries++;
                        if (!healer) r.EntriesNoHealer++;
                        if (!front) r.EntriesNoFront++;
                    }
                }
            }
            finally { _walkRoleBlind = had; }
            return r;
        }

        private static void SpRefLine(ContentDatabase db)
        {
            int walks = FusionNeverGatesWalks;
            Line("R1. THE REFERENCE PLAYER, " + walks + " walks under each line policy (never converts, never ascends: the player recommendedPower is derived from)");
            Stopwatch w = Stopwatch.StartNew();
            SpPolicyReading blind = SpWalkPolicy(db, true, walks);
            Line("    (" + walks + " role-blind walks took " + (w.ElapsedMilliseconds / 1000) + "s)");
            SpPolicyReading sane = SpWalkPolicy(db, false, walks);
            SpPolicyReading[] both = { blind, sane };
            for (int p = 0; p < both.Length; p++)
            {
                SpPolicyReading r = both[p];
                int stuck = r.Walks.Count(x => x.StuckAt >= 0);
                Line("    " + r.Name.PadRight(17) + " stuck " + stuck + "/" + walks + " (" + string.Join(", ", r.Stuck.Select(kv => kv.Key + " x" + kv.Value).ToArray())
                     + "); stages reached, median " + Median(r.Walks.Select(x => x.Stages.Count)) + "; stage entries with no Mender "
                     + r.EntriesNoHealer + " of " + r.Entries + ", with no front hero " + r.EntriesNoFront + " of " + r.Entries);
            }
            Line("    walk 1's line at a few entries:");
            string[] show = { "ch1_6", "ch1_12", "ch2_4", "ch2_6", "ch2_12" };
            for (int i = 0; i < show.Length; i++)
            {
                for (int p = 0; p < both.Length; p++)
                {
                    EconomyStageOutcome o = both[p].Walks[0].Stages.Find(x => x.Stage.Id == show[i]);
                    if (o != null) Line("      " + o.Stage.DisplayCode.PadRight(5) + " " + both[p].Name.PadRight(17) + " entry " + o.EntryPower.ToString().PadLeft(6)
                                        + (o.Cleared ? "" : " STUCK") + "   " + o.EntryLine);
                }
            }
            Line("");

            // --- the solver's first iteration, from the committed table, under each policy
            Line("R2. WHAT THE SOLVE WOULD INHERIT: the solver's first-iteration recommendedPower (ProposeRecommendedPower on walk 1's entry power,");
            Line("    with one SolverStuckStep per walk that stuck on the stage), from the table as committed. A stage walk 1 did not reach has no reading.");
            Dictionary<string, int>[] proposed = new Dictionary<string, int>[2];
            for (int p = 0; p < both.Length; p++)
            {
                proposed[p] = new Dictionary<string, int>();
                EconomyRun run = both[p].Walks[0];
                Dictionary<string, int> steps = new Dictionary<string, int>();
                foreach (EconomyRun x in both[p].Walks)
                    if (x.StuckAt >= 0) { string id = db.AllStages[x.StuckAt].Id; int n; steps.TryGetValue(id, out n); steps[id] = n + 1; }
                for (int i = 0; i < run.Stages.Count; i++)
                {
                    int k; steps.TryGetValue(run.Stages[i].Stage.Id, out k);
                    proposed[p][run.Stages[i].Stage.Id] = ProposeRecommendedPower(run.Stages[i], i, BaselineOf(run.Stages[i].Stage), k).NewRecommended;
                }
            }
            Line("    stage  committed  role-blind  role-sane   sane/blind");
            for (int ch = 1; ch <= 6; ch++)
            {
                List<double> r = new List<double>();
                for (int s = 0; s < db.AllStages.Count; s++)
                {
                    StageDef st = db.AllStages[s];
                    if (st.ChapterNumber != ch) continue;
                    int a, c;
                    bool ha = proposed[0].TryGetValue(st.Id, out a), hc = proposed[1].TryGetValue(st.Id, out c);
                    Line("    " + st.DisplayCode.PadRight(6) + st.RecommendedPower.ToString().PadLeft(9) + (ha ? a.ToString() : "-").PadLeft(12)
                         + (hc ? c.ToString() : "-").PadLeft(11) + (ha && hc ? "x" + F(c / (double)a, "0.000") : "").PadLeft(13));
                    if (ha && hc) r.Add(c / (double)a);
                }
                Line("      chapter " + ch + ": " + (r.Count == 0 ? "no stage reached by both"
                     : r.Count + " stages read by both, sane/blind mean x" + F(r.Average(), "0.000") + ", min x" + F(r.Min(), "0.000") + ", max x" + F(r.Max(), "0.000")));
            }
            Line("");

            // --- the same proposal from every walk, not only walk 1. Walk 1 role-blind sticks
            // before chapter 3, so R2 cannot read chapters 3-6 for it. Here each stage is proposed
            // from the median entry power of every walk that reached it. The role-blind walks that
            // reach chapter 3 are the ones that did not stick, so they are its luckiest rolls and
            // this UNDERSTATES the gap there.
            Line("R2b. THE SAME PROPOSAL FROM EVERY WALK: each stage proposed from the median entry power over the walks that reached it,");
            Line("    stuck steps as in R2. Per chapter, the role-sane proposal over the role-blind one.");
            Dictionary<string, int>[] med = new Dictionary<string, int>[2];
            Dictionary<string, int>[] reached = new Dictionary<string, int>[2];
            for (int p = 0; p < both.Length; p++)
            {
                med[p] = new Dictionary<string, int>();
                reached[p] = new Dictionary<string, int>();
                Dictionary<string, int> steps = new Dictionary<string, int>();
                foreach (EconomyRun x in both[p].Walks)
                    if (x.StuckAt >= 0) { string id = db.AllStages[x.StuckAt].Id; int n; steps.TryGetValue(id, out n); steps[id] = n + 1; }
                for (int s = 0; s < db.AllStages.Count; s++)
                {
                    StageDef st = db.AllStages[s];
                    List<KeyValuePair<EconomyStageOutcome, int>> at = new List<KeyValuePair<EconomyStageOutcome, int>>();
                    foreach (EconomyRun x in both[p].Walks)
                    {
                        int idx = x.Stages.FindIndex(o => o.Stage.Id == st.Id);
                        if (idx >= 0) at.Add(new KeyValuePair<EconomyStageOutcome, int>(x.Stages[idx], idx));
                    }
                    if (at.Count == 0) continue;
                    at.Sort((u, v) => u.Key.EntryPower.CompareTo(v.Key.EntryPower));
                    KeyValuePair<EconomyStageOutcome, int> m = at[at.Count / 2];
                    int k; steps.TryGetValue(st.Id, out k);
                    med[p][st.Id] = ProposeRecommendedPower(m.Key, m.Value, BaselineOf(st), k).NewRecommended;
                    reached[p][st.Id] = at.Count;
                }
            }
            for (int ch = 1; ch <= 6; ch++)
            {
                List<double> r = new List<double>();
                List<double> vsCommitted = new List<double>();
                int minB = int.MaxValue, minS = int.MaxValue;
                for (int s = 0; s < db.AllStages.Count; s++)
                {
                    StageDef st = db.AllStages[s];
                    if (st.ChapterNumber != ch) continue;
                    int a, c;
                    bool ha = med[0].TryGetValue(st.Id, out a), hc = med[1].TryGetValue(st.Id, out c);
                    if (hc) vsCommitted.Add(c / (double)st.RecommendedPower);
                    if (ha && hc) r.Add(c / (double)a);
                    int nb, ns;
                    minB = Math.Min(minB, reached[0].TryGetValue(st.Id, out nb) ? nb : 0);
                    minS = Math.Min(minS, reached[1].TryGetValue(st.Id, out ns) ? ns : 0);
                }
                Line("    chapter " + ch + ": " + (r.Count == 0 ? "no stage reached by both"
                     : "sane/blind mean x" + F(r.Average(), "0.000") + ", min x" + F(r.Min(), "0.000") + ", max x" + F(r.Max(), "0.000")
                       + " over " + r.Count + " stages")
                     + "; role-sane over committed mean x" + (vsCommitted.Count == 0 ? "-" : F(vsCommitted.Average(), "0.000"))
                     + "; fewest walks reaching a stage: blind " + minB + ", sane " + minS);
            }
            Line("");

            // --- the two lines fought at equal power
            int n2 = 10 * _spScale;
            Line("R3. AT EQUAL POWER: each policy's entry line (walk 1's, else the first walk that reached the stage), rebuilt and geared at the stage's recommendedPower (BuildLineAt, as the");
            Line("    instrument line is), against the CEIL battle, beside the instrument line itself. " + n2 + " seeds per stage.");
            for (int ch = 1; ch <= 6; ch++)
            {
                List<TempoObs[]> inst = new List<TempoObs[]>(), bl = new List<TempoObs[]>(), sn = new List<TempoObs[]>(), sf = new List<TempoObs[]>();
                int unseated = 0;
                for (int s = 0; s < db.AllStages.Count; s++)
                {
                    StageDef st = db.AllStages[s];
                    if (st.ChapterNumber != ch) continue;
                    // Walk 1's line where walk 1 reached the stage, else the first walk that did:
                    // role-blind walk 1 sticks in chapter 2.
                    EconomyStageOutcome ob = SpFirstEntry(blind, st.Id), os = SpFirstEntry(sane, st.Id);
                    if (ob == null || os == null || ob.EntryIds.Length < 5 || os.EntryIds.Length < 5) continue;
                    StageDef ceil = TempoCeil(st);
                    inst.Add(TempoCampaign(BuildPinnedLine(db, st, true), ceil, st, n2, null, null, null));
                    bl.Add(TempoCampaign(BuildLineAt(db, st, true, st.RecommendedPower, ob.EntryIds), ceil, st, n2, null, null, null));
                    sn.Add(TempoCampaign(BuildLineAt(db, st, true, st.RecommendedPower, os.EntryIds), ceil, st, n2, null, null, null));
                    string[] seated = SpFrontSeated(db, os.EntryIds);
                    if (seated[0] != os.EntryIds[0]) unseated++;
                    sf.Add(TempoCampaign(BuildLineAt(db, st, true, st.RecommendedPower, seated), ceil, st, n2, null, null, null));
                }
                if (inst.Count == 0) { Line("    chapter " + ch + ": no stage reached by both walks"); continue; }
                TempoObs[] I = Concat(inst), B = Concat(bl), S = Concat(sn);
                Line("    chapter " + ch + " (" + inst.Count + " stages): instrument " + SpWr(I) + ", role-blind " + SpWr(B) + " (" + SpD(I, B)
                     + "pp), role-sane " + SpWr(S) + " (" + SpD(I, S) + "pp); sane minus blind " + SpD(B, S) + "pp");
                TempoObs[] SF = Concat(sf);
                Line("      the role-sane line with its front hero moved to slot 1 (the front; FrontEnemy, the default target, takes the"
                     + " lowest slot): " + SpWr(SF) + ", " + SpD(S, SF) + "pp over the walk's own seating; " + unseated + " of " + inst.Count
                     + " entry lines did not already have a front hero in slot 1");
            }
            Line("");

            // --- the same gap as power. The CEIL levels are solved against the instrument line at
            // recommendedPower; recommendedPower is derived from the reference walk. A reference line
            // that fights worse at one power needs more power to clear what the instrument line
            // clears, and the solver meets that as stuck walks (SolverStuckStep). This reads how much
            // more, per chapter: the multiple of recommendedPower at which each policy's line wins the
            // CEIL as often as the instrument line does at x1.00, interpolated between the rungs.
            float[] rungs = { 1.00f, 1.05f, 1.10f, 1.15f, 1.20f, 1.30f, 1.45f };
            int n4 = 5 * _spScale;
            Line("R4. THE GAP AS POWER: the multiple of recommendedPower at which each policy's entry line wins the CEIL as often as the");
            Line("    instrument line does at x1.00. Rungs " + string.Join(", ", rungs.Select(r => "x" + F(r, "0.00")).ToArray()) + "; " + n4 + " seeds per stage per rung.");
            for (int ch = 1; ch <= 6; ch++)
            {
                List<TempoObs[]> inst = new List<TempoObs[]>();
                List<TempoObs[]>[][] at = new List<TempoObs[]>[2][];
                for (int p = 0; p < 2; p++) { at[p] = new List<TempoObs[]>[rungs.Length]; for (int k = 0; k < rungs.Length; k++) at[p][k] = new List<TempoObs[]>(); }
                for (int s = 0; s < db.AllStages.Count; s++)
                {
                    StageDef st = db.AllStages[s];
                    if (st.ChapterNumber != ch) continue;
                    EconomyStageOutcome[] o = { SpFirstEntry(blind, st.Id), SpFirstEntry(sane, st.Id) };
                    if (o[0] == null || o[1] == null) continue;
                    StageDef ceil = TempoCeil(st);
                    inst.Add(TempoCampaign(BuildPinnedLine(db, st, true), ceil, st, n4, null, null, null));
                    for (int p = 0; p < 2; p++)
                        for (int k = 0; k < rungs.Length; k++)
                            at[p][k].Add(TempoCampaign(BuildLineAt(db, st, true, (int)Math.Round(st.RecommendedPower * rungs[k]), o[p].EntryIds), ceil, st, n4, null, null, null));
                }
                if (inst.Count == 0) { Line("    chapter " + ch + ": no stage reached by both"); continue; }
                double target = Mean(Concat(inst), x => x.Win);
                string[] txt = new string[2];
                for (int p = 0; p < 2; p++)
                {
                    double[] wr = new double[rungs.Length];
                    for (int k = 0; k < rungs.Length; k++) wr[k] = Mean(Concat(at[p][k]), x => x.Win);
                    string hit = "above x" + F(rungs[rungs.Length - 1], "0.00");
                    if (wr[0] >= target) hit = "x1.00 or less";
                    else
                        for (int k = 1; k < rungs.Length; k++)
                            if (wr[k] >= target)
                            {
                                double t = (target - wr[k - 1]) / Math.Max(1e-9, wr[k] - wr[k - 1]);
                                hit = "x" + F(rungs[k - 1] + t * (rungs[k] - rungs[k - 1]), "0.000");
                                break;
                            }
                    txt[p] = hit + " (" + string.Join(" ", wr.Select(v => F(v * 100, "0")).ToArray()) + ")";
                }
                Line("    chapter " + ch + " (" + inst.Count + " stages, instrument " + F(target * 100, "0.0") + "%): role-blind " + txt[0] + ";  role-sane " + txt[1]);
            }
            Line("");
        }

        /// <summary>The line in slot order with its first front hero (Bulwark or Vanguard) moved to
        /// slot 1, everyone else keeping their order.</summary>
        private static string[] SpFrontSeated(ContentDatabase db, string[] ids)
        {
            List<string> l = new List<string>(ids);
            int f = l.FindIndex(id => { HeroDef d = db.GetHero(id); return d != null && (d.Role == HeroRole.Bulwark || d.Role == HeroRole.Vanguard); });
            if (f > 0) { string front = l[f]; l.RemoveAt(f); l.Insert(0, front); }
            return l.ToArray();
        }

        private static EconomyStageOutcome SpFirstEntry(SpPolicyReading r, string stageId)
        {
            foreach (EconomyRun x in r.Walks)
            {
                EconomyStageOutcome o = x.Stages.Find(s => s.Stage.Id == stageId);
                if (o != null && o.EntryIds.Length >= 5) return o;
            }
            return null;
        }

        private static int Median(IEnumerable<int> v)
        {
            List<int> l = v.ToList();
            l.Sort();
            return l.Count == 0 ? 0 : l[l.Count / 2];
        }
    }
}
