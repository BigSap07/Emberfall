using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Emberfall.Core.Cloud;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Save;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// The Guild Path's harness: the study its numbers are derived from (DevHarness.exe
    /// --guild-path) and the checks that hold guild-path.json, the server's migration and the
    /// client to it on every build-check. A file of its own for GuildDungeonChecks' reason.
    ///
    /// WHAT IS MEASURED AND WHAT IS CHOSEN. Four numbers are design inputs, named below with the
    /// sentence of the design record each one implements; everything else is read off real guild
    /// cycles - real dungeons from GuildDungeonGenerator, real rosters from GameSession.Summon at
    /// the real rates, real fights, real boards from GuildDungeon.Rank.
    /// </summary>
    internal static class GuildPathChecks
    {
        private static int _failures;

        // ---------------------------------------------------------------- the design inputs

        /// <summary>
        /// "Whatever a player's roster looks like, some board suits it" - and the base must not
        /// care either. A member on the newest roster studied must expect at least this share of
        /// the base the deepest roster expects. It is what caps the clear's pay: the more a clear
        /// is worth, the more a roster that clears less often is paid less.
        /// </summary>
        private const double FairnessFloor = 0.8;

        /// <summary>A clear is worth at most as much as turning up. Only binds when the fairness
        /// floor does not: a dungeon every roster clears would otherwise let a clear be worth
        /// anything.</summary>
        private const double ClearAtMost = 1.0;

        /// <summary>
        /// The unit. A member's mean cycle is worth 100 Waymarks, so a price reads in cycles: a
        /// node that costs 200 is two weeks of an ordinary member's dungeon. The currency buys
        /// nothing else, so the scale is free; this fixes it where it reads.
        /// </summary>
        private const int MeanCycle = 100;

        /// <summary>
        /// "The bonus categories are recognition with small material value." A fifth of a mean
        /// member's cycle comes from placements and four fifths from turning up and clearing.
        /// </summary>
        private const double BoardShare = 0.2;

        /// <summary>
        /// THE PRICE RULE: a node costs as many mean cycles as its depth on the Path. The first
        /// line is one week of turning up, and each step further is a week longer than the last.
        /// </summary>
        private static int PriceOf(GuildPathNodeDef node) { return node.Depth * MeanCycle; }

        /// <summary>A written pay number may sit this far from the derived one before the gate
        /// fails. The derivation moves with every content edit that moves a fight; a price the
        /// server charges should not have to move with each of them. Past this, it should.</summary>
        private const double PayTolerance = 0.10;

        // ---------------------------------------------------------------- the population

        /// <summary>Guild sizes studied, up to the 30-member cap. One is the guild of one.</summary>
        private static readonly int[] GuildSizes = { 1, 2, 5, 10, 20, 30 };

        /// <summary>Cycles studied per guild size, each a different guild's dungeon.</summary>
        private const int StudyCycles = 16;

        /// <summary>
        /// Summons behind each roster depth: a member who joined early, one settled in, one deep
        /// in the collection. The economy walk does not report a roster size a guild member would
        /// have, so the study reads all three and the derivation weighs them equally; the fairness
        /// floor is asserted between the shallowest and the deepest.
        /// </summary>
        private static readonly int[] PullDepths = { 10, 30, 60 };
        private static readonly string[] DepthNames = { "new (3+10 pulls)", "settled (3+30)", "deep (3+60)" };

        /// <summary>Every member uses every attempt on floor 1: the most engaged member, which is
        /// the member the board bonus is sized for.</summary>
        private const int Attempts = 3;

        private sealed class Member
        {
            public string Id;
            public int Depth;
            public List<HeroDef> Roster = new List<HeroDef>();
        }

        private sealed class Reading
        {
            // per depth: members, members who cleared floor 1 in the cycle
            public int[] Members = new int[3];
            public int[] Cleared = new int[3];
            // per guild size: members, sum of board-score sums, members entering >= 1 paying board
            public Dictionary<int, double> ScoreSum = new Dictionary<int, double>();
            public Dictionary<int, int> SizeMembers = new Dictionary<int, int>();
            public Dictionary<int, int> SizeCleared = new Dictionary<int, int>();
            public Dictionary<int, List<double>> SizeScores = new Dictionary<int, List<double>>();
            public int Fights;
        }

        private sealed class Derived
        {
            public double QNew, QDeep, QAll, KBound, K, SBar;
            public int Attempt, Clear, BoardPoint;
            public double RawAttempt, RawClear, RawBoardPoint;
        }

        // ---------------------------------------------------------------- the study

        /// <summary>
        /// DevHarness.exe --guild-path. Prints the whole working: the measured clear rates, the
        /// measured board scores by guild size, the derivation, and what the derived numbers pay
        /// each size and each roster depth. Measures and writes nothing.
        /// </summary>
        internal static int RunStudy(ContentDatabase content)
        {
            _failures = 0;
            Line("GUILD PATH - the study guild-path.json's numbers are derived from");
            Reading r = Measure(content, true);
            Derived d = Derive(r);
            Report(content, r, d);
            return 0;
        }

        private static Reading Measure(ContentDatabase content, bool progress)
        {
            Reading r = new Reading();
            int floor = Math.Max(1, content.GuildPath.BoardFloor);
            for (int s = 0; s < GuildSizes.Length; s++)
            {
                int size = GuildSizes[s];
                r.ScoreSum[size] = 0;
                r.SizeMembers[size] = 0;
                r.SizeCleared[size] = 0;
                r.SizeScores[size] = new List<double>();
                for (int c = 0; c < StudyCycles; c++)
                {
                    int seed = GuildDungeon.SeedFor("path-guild-" + size + "-" + c, 1);
                    FloorConditionDef condition = GuildDungeon.ConditionOf(content, seed, floor);
                    List<Member> members = new List<Member>();
                    for (int m = 0; m < size; m++) members.Add(MemberOf(content, "m" + m, (m + c) % 3, seed * 31 + m));

                    List<GuildDungeonAttempt> rows = new List<GuildDungeonAttempt>();
                    long id = 1;
                    for (int a = 1; a <= Attempts; a++)
                    {
                        for (int m = 0; m < members.Count; m++)
                        {
                            TeamSnapshot line = LineFor(content, members[m], condition, a, seed + m * 7919 + a);
                            if (line.Units.Count == 0) continue;
                            BattleResult unused;
                            GuildDungeonAttempt row = GuildDungeon.Attempt(content, line, seed, 1, floor, out unused);
                            row.UserId = members[m].Id;
                            row.Attempt = a;
                            row.Id = id++;
                            rows.Add(row);
                            r.Fights++;
                        }
                    }

                    for (int m = 0; m < members.Count; m++)
                    {
                        bool cleared = rows.Exists(x => x.UserId == members[m].Id && x.Victory);
                        r.Members[members[m].Depth]++;
                        if (cleared) r.Cleared[members[m].Depth]++;
                        r.SizeMembers[size]++;
                        if (cleared) r.SizeCleared[size]++;
                        List<float> scores = GuildPath.BoardScores(content, content.GuildPath, rows, members[m].Id, 1);
                        double sum = 0;
                        for (int i = 0; i < scores.Count && i < content.GuildPath.BoardsCounted; i++) sum += scores[i];
                        r.ScoreSum[size] += sum;
                        r.SizeScores[size].Add(sum);
                    }
                }
                if (progress) Line("  measured guilds of " + size + " (" + r.Fights + " fights so far)");
            }
            return r;
        }

        /// <summary>A member: a fresh profile, then the pulls of their depth through the real
        /// GameSession.Summon at the real rates and pity. What they own is the distinct set.</summary>
        private static Member MemberOf(ContentDatabase content, string id, int depth, int seed)
        {
            Member m = new Member();
            m.Id = id;
            m.Depth = depth;
            PlayerProfile profile = GameSession.CreateNewProfile(content, 0);
            GameSession session = new GameSession(content, profile);
            profile.Shards = (long)content.Balance.SummonCost * (PullDepths[depth] + 1);
            for (int p = 0; p < PullDepths[depth]; p++) session.Summon(seed * 131 + p, 0);
            for (int i = 0; i < profile.Heroes.Count; i++)
            {
                HeroDef def = content.GetHero(profile.Heroes[i].DefId);
                if (def != null) m.Roster.Add(def);
            }
            return m;
        }

        /// <summary>
        /// The line a member brings to one attempt, from their own roster and legal under the
        /// floor's condition. The first attempt is their strongest by stars, as a player's first
        /// read of a floor would be; the others are other lines they own, drawn at random, because
        /// the same line twice is the same fight and the cap makes a player try something else.
        /// </summary>
        private static TeamSnapshot LineFor(ContentDatabase content, Member m, FloorConditionDef condition, int attempt, int seed)
        {
            DeterministicRng rng = new DeterministicRng(seed);
            List<HeroDef> order = new List<HeroDef>(m.Roster);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                HeroDef t = order[i]; order[i] = order[j]; order[j] = t;
            }
            if (attempt == 1) order.Sort((x, y) => y.Stars.CompareTo(x.Stars));

            LineRules rules = condition != null ? condition.Line : new LineRules();
            int size = content.Balance.MaxTeamSize;
            if (rules.MaxHeroes > 0) size = Math.Min(size, rules.MaxHeroes);
            TeamSnapshot line = new TeamSnapshot();
            for (int i = 0; i < order.Count && line.Units.Count < size; i++)
            {
                HeroDef d = order[i];
                if (rules.MaxStars > 0 && d.Stars > rules.MaxStars) continue;
                if (rules.BannedRoles.Contains(d.Role)) continue;
                if (rules.MaxPerRole > 0)
                {
                    int same = 0;
                    for (int u = 0; u < line.Units.Count; u++)
                    {
                        HeroDef o = content.GetHero(line.Units[u].DefId);
                        if (o != null && o.Role == d.Role) same++;
                    }
                    if (same >= rules.MaxPerRole) continue;
                }
                line.Units.Add(new SnapshotUnit { DefId = d.Id, Level = content.Balance.GuildDungeonHeroLevel, Stars = d.Stars });
            }
            string broke;
            if (!GuildDungeon.LineLegal(content, condition, line, out broke)) line.Units.Clear();
            return line;
        }

        private static Derived Derive(Reading r)
        {
            Derived d = new Derived();
            d.QNew = Share(r.Cleared[0], r.Members[0]);
            d.QDeep = Share(r.Cleared[2], r.Members[2]);
            d.QAll = (Share(r.Cleared[0], r.Members[0]) + Share(r.Cleared[1], r.Members[1]) + Share(r.Cleared[2], r.Members[2])) / 3.0;

            // (P + Cl q_new) >= F (P + Cl q_deep), with Cl = kP:  k <= (1 - F) / (F q_deep - q_new).
            double denom = FairnessFloor * d.QDeep - d.QNew;
            d.KBound = denom > 0 ? (1 - FairnessFloor) / denom : double.PositiveInfinity;
            d.K = Math.Min(ClearAtMost, d.KBound);

            // The mean member's base is the cycle less the board share: P (1 + k q_all) = 80.
            double meanBase = MeanCycle * (1 - BoardShare);
            d.RawAttempt = meanBase / (1 + d.K * d.QAll);
            d.RawClear = d.K * d.RawAttempt;

            // Board scores averaged per size, then across the sizes that have boards to place on,
            // each size weighted once - so the thirty-member guild's many members do not decide
            // what a placement is worth in a guild of five.
            double total = 0; int sizes = 0;
            for (int s = 0; s < GuildSizes.Length; s++)
            {
                int size = GuildSizes[s];
                if (size < 2) continue;
                total += r.ScoreSum[size] / Math.Max(1, r.SizeMembers[size]);
                sizes++;
            }
            d.SBar = sizes == 0 ? 0 : total / sizes;
            d.RawBoardPoint = d.SBar > 0 ? MeanCycle * BoardShare / d.SBar : 0;

            d.Attempt = (int)Math.Round(d.RawAttempt, MidpointRounding.AwayFromZero);
            d.Clear = (int)Math.Round(d.RawClear, MidpointRounding.AwayFromZero);
            d.BoardPoint = (int)Math.Round(d.RawBoardPoint, MidpointRounding.AwayFromZero);
            return d;
        }

        private static void Report(ContentDatabase content, Reading r, Derived d)
        {
            GuildPathConfig cfg = content.GuildPath;
            Line("");
            Line("  MEASURED - " + r.Fights + " floor-" + cfg.BoardFloor + " fights, " + StudyCycles + " dungeons per guild size, "
                 + Attempts + " attempts per member, every line fought at level " + content.Balance.GuildDungeonHeroLevel);
            Line("    clear rate by roster depth (cleared floor " + cfg.BoardFloor + " at least once in the cycle):");
            for (int i = 0; i < 3; i++)
                Line("      " + DepthNames[i].PadRight(20) + Pct(Share(r.Cleared[i], r.Members[i])).PadLeft(5)
                     + "   (" + r.Cleared[i] + " of " + r.Members[i] + ")");
            Line("    board-score sum per member by guild size (best " + cfg.BoardsCounted + " of floor " + cfg.BoardFloor + "'s boards; "
                 + "a place is (n - rank) / (n - 1)):");
            Line("      " + "size".PadRight(6) + "members".PadRight(9) + "clear".PadRight(7) + "mean".PadRight(7)
                 + "p25".PadRight(7) + "median".PadRight(8) + "p90".PadRight(7) + "max");
            for (int s = 0; s < GuildSizes.Length; s++)
            {
                int size = GuildSizes[s];
                List<double> v = new List<double>(r.SizeScores[size]);
                v.Sort();
                Line("      " + size.ToString().PadRight(6) + r.SizeMembers[size].ToString().PadRight(9)
                     + Pct(Share(r.SizeCleared[size], r.SizeMembers[size])).PadRight(7)
                     + F2(r.ScoreSum[size] / Math.Max(1, r.SizeMembers[size])).PadRight(7)
                     + F2(Quantile(v, 0.25)).PadRight(7) + F2(Quantile(v, 0.5)).PadRight(8)
                     + F2(Quantile(v, 0.9)).PadRight(7) + F2(v.Count > 0 ? v[v.Count - 1] : 0));
            }

            Line("");
            Line("  DERIVED - design inputs: fairness floor " + FairnessFloor.ToString("0.00", CultureInfo.InvariantCulture)
                 + ", clear at most x" + ClearAtMost.ToString("0.0", CultureInfo.InvariantCulture)
                 + " the attempt, mean cycle " + MeanCycle + ", board share " + Pct(BoardShare));
            Line("    q_new " + F3(d.QNew) + "   q_deep " + F3(d.QDeep) + "   q_all " + F3(d.QAll) + "  (equal thirds)");
            Line("    clear/attempt ratio k: fairness allows k <= (1 - " + F2(FairnessFloor) + ") / (" + F2(FairnessFloor)
                 + " x " + F3(d.QDeep) + " - " + F3(d.QNew) + ") = "
                 + (double.IsInfinity(d.KBound) ? "unbounded" : F3(d.KBound)) + ";  k = min(" + F2(ClearAtMost) + ", that) = " + F3(d.K));
            Line("    attempt = " + MeanCycle + " x (1 - " + F2(BoardShare) + ") / (1 + k q_all) = " + F2(d.RawAttempt) + "  ->  " + d.Attempt);
            Line("    clear   = k x attempt = " + F2(d.RawClear) + "  ->  " + d.Clear);
            Line("    S-bar   = mean board-score sum, sizes 2 to 30 weighted once each = " + F3(d.SBar));
            Line("    board point = " + MeanCycle + " x " + F2(BoardShare) + " / S-bar = " + F2(d.RawBoardPoint) + "  ->  " + d.BoardPoint);

            Line("");
            Line("  WHAT THE DERIVED NUMBERS PAY (expected Waymarks per member per cycle)");
            Line("      " + "guild size".PadRight(12) + "base".PadRight(8) + "boards".PadRight(8) + "total".PadRight(8) + "of 100");
            for (int s = 0; s < GuildSizes.Length; s++)
            {
                int size = GuildSizes[s];
                double q = Share(r.SizeCleared[size], r.SizeMembers[size]);
                double b = d.Attempt + d.Clear * q;
                double bonus = d.BoardPoint * r.ScoreSum[size] / Math.Max(1, r.SizeMembers[size]);
                Line("      " + size.ToString().PadRight(12) + F1(b).PadRight(8) + F1(bonus).PadRight(8) + F1(b + bonus).PadRight(8)
                     + Pct((b + bonus) / MeanCycle));
            }
            for (int i = 0; i < 3; i++)
            {
                double q = Share(r.Cleared[i], r.Members[i]);
                Line("      roster " + DepthNames[i].PadRight(20) + "base " + F1(d.Attempt + d.Clear * q));
            }
            int most = d.Attempt + d.Clear + d.BoardPoint * cfg.BoardsCounted;
            Line("    most in one cycle, whatever a client claims: " + d.Attempt + " + " + d.Clear + " + " + d.BoardPoint + " x "
                 + cfg.BoardsCounted + " = " + most + " (x" + F2((double)most / MeanCycle) + " the mean cycle)");

            Line("");
            Line("  PRICES - a node costs its depth in mean cycles");
            int all = 0;
            for (int i = 0; i < cfg.Nodes.Count; i++)
            {
                GuildPathNodeDef n = cfg.Nodes[i];
                all += PriceOf(n);
                Line("      " + n.Id.PadRight(16) + "depth " + n.Depth + "   " + PriceOf(n) + (n.Cost == PriceOf(n) ? "" : "   (file says " + n.Cost + ")"));
            }
            double newBase = d.Attempt + d.Clear * Share(r.Cleared[0], r.Members[0]);
            Line("    the whole Path: " + all + " Waymarks, " + F1((double)all / MeanCycle) + " mean cycles; "
                 + F1(all / Math.Max(1, newBase)) + " cycles for a new-roster member paid the base and no board");
            Line("");
            Line("  guild-path.json today: attempt " + cfg.PayAttempt + ", clear " + cfg.PayClear + ", boardPoint " + cfg.PayBoardPoint);
        }

        // ---------------------------------------------------------------- the gate

        public static int Run(ContentDatabase content, string contentDir)
        {
            _failures = 0;
            Line("");
            Line("GUILD PATH");
            GuildPathConfig cfg = content.GuildPath;
            Check(cfg.IsUsable, "guild-path.json loads with " + cfg.Nodes.Count + " nodes");
            if (!cfg.IsUsable) return _failures;
            CheckBoardBits(contentDir);
            CheckPayRules(content);
            CheckDerivation(content);
            CheckServerAgrees(content, contentDir);
            CheckEveryNodeIsHorizontal(content);
            CheckLines(content);
            return _failures;
        }

        /// <summary>
        /// THE BOARD ORDER IS STORED NOW. guild_dungeon_attempts.boards holds one bit per board, 1
        /// shifted by its place in GuildDungeonBoard, and the migration checks four of them by
        /// number. Reordering the enum would silently move every stored claim to another board.
        /// </summary>
        private static void CheckBoardBits(string contentDir)
        {
            GuildDungeonBoard[] pinned =
            {
                GuildDungeonBoard.FourOrFewer, GuildDungeonBoard.NoFiveStar, GuildDungeonBoard.OneAspect,
                GuildDungeonBoard.NoHealer, GuildDungeonBoard.FirstAttempt, GuildDungeonBoard.Flawless
            };
            bool same = GuildDungeon.Boards.Length == pinned.Length;
            for (int i = 0; same && i < pinned.Length; i++)
                same = GuildDungeon.Boards[i] == pinned[i] && (int)pinned[i] == i && GuildPath.BoardBit(pinned[i]) == 1 << i;
            Check(same, "the six boards keep their stored bits: four or fewer 1, no five stars 2, one aspect 4, no healer 8, "
                  + "first attempt 16, nobody fell 32");

            string sql = ReadMigration(contentDir);
            Check(sql.Contains("jsonb_array_length(result.team) > 4 then bits := bits & ~1")
                  && sql.Contains("bits := bits & ~2") && sql.Contains("result.attempt <> 1 then bits := bits & ~16")
                  && sql.Contains("result.fallen is distinct from 0 then bits := bits & ~32")
                  && sql.Contains("values (1), (2), (4), (8), (16), (32)"),
                  "0011 checks four or fewer, no five stars, first attempt and nobody fell at those bits, and settles all six");
        }

        /// <summary>The three pay rules, on hand-built boards whose answer is known.</summary>
        private static void CheckPayRules(ContentDatabase content)
        {
            GuildPathConfig cfg = content.GuildPath;
            int seed = NoConditionSeed(content);
            TeamSnapshot four = new TeamSnapshot();
            // Four heroes of one Aspect, none above three stars and no Mender, so every row enters
            // every board its result allows and the expected scores can be written down.
            List<HeroDef> low = content.Heroes.FindAll(h => h.Stars <= 3 && h.Role != HeroRole.Mender);
            Aspect common = low.Count > 0 ? low[0].Aspect : Aspect.Ember;
            foreach (Aspect asp in Enum.GetValues(typeof(Aspect)))
                if (low.FindAll(h => h.Aspect == asp).Count >= 4) { common = asp; break; }
            low = low.FindAll(h => h.Aspect == common);
            for (int i = 0; i < 4 && i < low.Count; i++)
                four.Units.Add(new SnapshotUnit { DefId = low[i].Id, Level = content.Balance.GuildDungeonHeroLevel, Stars = low[i].Stars });

            List<GuildDungeonAttempt> rows = new List<GuildDungeonAttempt>
            {
                Row("a", 1, true, 9, 0, four, seed, 1),
                Row("b", 1, true, 11, 0, four, seed, 2),
                Row("c", 1, true, 14, 0, four, seed, 3),
                Row("c", 2, true, 8, 1, four, seed, 4),
                Row("d", 1, false, 30, 4, four, seed, 5)
            };
            // Four of the boards take each member's best clear: c's 8 turns, a's 9, b's 11, so a is
            // second of three (0.5) and c first (1). First attempt and nobody fell cannot take c's 8
            // (a second attempt that lost a hero): there a leads (1) and c is last of three (0).
            List<float> a = GuildPath.BoardScores(content, cfg, rows, "a", 1);
            List<float> c = GuildPath.BoardScores(content, cfg, rows, "c", 1);
            List<float> d = GuildPath.BoardScores(content, cfg, rows, "d", 1);
            Check(a.Count == 6 && c.Count == 6 && Near(Sum(a), 4 * 0.5 + 1 + 1) && Near(Sum(c), 4 * 1 + 0 + 0) && d.Count == 0,
                  "a place pays (n - rank) / (n - 1), each member's best row per board, a loss enters nothing "
                  + "(a " + F2(Sum(a)) + ", c " + F2(Sum(c)) + ", d " + d.Count + " boards)");

            List<GuildDungeonAttempt> alone = new List<GuildDungeonAttempt> { Row("solo", 1, true, 9, 0, four, seed, 1) };
            List<float> solo = GuildPath.BoardScores(content, cfg, alone, "solo", 1);
            Check(solo.Count > 0 && Near(Sum(solo), 0), "a board of one pays nothing: first of one is not a placement ("
                  + solo.Count + " boards entered, " + F2(Sum(solo)) + " points)");

            // Averaged over its entrants a board pays one half, at every size.
            bool half = true;
            for (int n = 2; n <= 30; n++)
            {
                List<GuildDungeonAttempt> board = new List<GuildDungeonAttempt>();
                for (int m = 0; m < n; m++) board.Add(Row("m" + m, 1, true, 5 + m, 0, four, seed, m + 1));
                double total = 0;
                for (int m = 0; m < n; m++)
                {
                    List<GuildDungeonAttempt> one = new List<GuildDungeonAttempt>(board);
                    total += GuildPath.BoardScores(content, cfg, one, "m" + m, 1)[0];
                }
                half &= Near(total / n, 0.5);
            }
            Check(half, "averaged over its entrants a board pays one half at every size from 2 to 30, so pay per standing does not grow with the guild");

            Check(GuildPath.BaseFor(cfg, rows, "d", 1) == cfg.PayAttempt && GuildPath.BaseFor(cfg, rows, "c", 1) == cfg.PayAttempt + cfg.PayClear
                  && GuildPath.BaseFor(cfg, rows, "nobody", 1) == 0,
                  "the base: a loss pays the attempt, a clear adds the clear, once a cycle however many rows");
            List<float> many = new List<float> { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
            Check(GuildPath.BonusFor(cfg, many) == cfg.PayBoardPoint * cfg.BoardsCounted,
                  "twelve first places (two guilds in one cycle) pay " + cfg.BoardsCounted + ", one guild's worth");
            Check(cfg.BoardsCounted == GuildDungeon.Boards.Length && cfg.BoardFloor == 1,
                  "the bonus counts one guild's six boards, on floor 1 - the one floor every guild has open");
        }

        /// <summary>
        /// THE NUMBERS ARE THE STUDY'S. Re-measures the whole population, re-derives, and fails when
        /// a written pay number sits more than PayTolerance from the derived one or a price breaks
        /// the price rule. Then the two properties the numbers exist for, asserted with the numbers
        /// AS WRITTEN: roster depth costs no more than the fairness floor allows, and no guild size
        /// from two up is paid more than a tenth away from the mean cycle.
        /// </summary>
        private static void CheckDerivation(ContentDatabase content)
        {
            GuildPathConfig cfg = content.GuildPath;
            Reading r = Measure(content, false);
            Derived d = Derive(r);
            Line("  derived from " + r.Fights + " fights: attempt " + F2(d.RawAttempt) + ", clear " + F2(d.RawClear)
                 + ", board point " + F2(d.RawBoardPoint) + "  (q_new " + F3(d.QNew) + ", q_deep " + F3(d.QDeep)
                 + ", S-bar " + F3(d.SBar) + ")");
            Check(Within(cfg.PayAttempt, d.RawAttempt) && Within(cfg.PayClear, d.RawClear) && Within(cfg.PayBoardPoint, d.RawBoardPoint),
                  "guild-path.json's pay is the derived pay within " + Pct(PayTolerance) + " (attempt " + cfg.PayAttempt
                  + ", clear " + cfg.PayClear + ", board point " + cfg.PayBoardPoint + ") - if this FAILS, run --guild-path and "
                  + "write what it derives here and in 0011");

            List<string> off = new List<string>();
            for (int i = 0; i < cfg.Nodes.Count; i++)
                if (cfg.Nodes[i].Cost != PriceOf(cfg.Nodes[i])) off.Add(cfg.Nodes[i].Id + " " + cfg.Nodes[i].Cost + " not " + PriceOf(cfg.Nodes[i]));
            Check(off.Count == 0, "every node costs its depth in mean cycles (" + cfg.Nodes.Count + " nodes"
                  + (off.Count > 0 ? "; " + string.Join(", ", off.ToArray()) : "") + ")");

            double qNew = Share(r.Cleared[0], r.Members[0]), qDeep = Share(r.Cleared[2], r.Members[2]);
            double fair = (cfg.PayAttempt + cfg.PayClear * qNew) / Math.Max(1e-9, cfg.PayAttempt + cfg.PayClear * qDeep);
            Check(fair >= FairnessFloor - 1e-9, "a new roster expects " + Pct(fair) + " of a deep roster's base, at least "
                  + Pct(FairnessFloor));

            // WHAT EVERY SIZE EARNS, REPORTED AND NO LONGER ASSERTED.
            //
            // This used to be the NO COLLECTIVE GATING gate: every guild size from two to thirty
            // had to expect the mean cycle within ten percent, or the build failed.
            // claude/DESIGN-GUILD-TECH.md RETIRES THAT GUARANTEE DELIBERATELY - G7 makes a large
            // guild level meaningfully faster per person than a small one, and that is now the
            // recruitment pressure a guild is supposed to have. The check that replaces it is
            // CheckGuildTechLevelCurve, which asserts the RATE RATIO is the one the square root
            // predicts and that the CEILING does not move with size.
            //
            // The DUNGEON'S OWN PAY is still flat across sizes, and these numbers still say so;
            // they are kept as a reading because the pay derivation above is solved against them.
            // What is gone is the assertion, not the measurement.
            List<string> sizes = new List<string>();
            for (int s = 0; s < GuildSizes.Length; s++)
            {
                int size = GuildSizes[s];
                double total = cfg.PayAttempt + cfg.PayClear * Share(r.SizeCleared[size], r.SizeMembers[size])
                             + cfg.PayBoardPoint * r.ScoreSum[size] / Math.Max(1, r.SizeMembers[size]);
                sizes.Add(size + ": " + F1(total));
            }
            Line("    what the dungeon pays per member per cycle, by guild size (reported; the flatness GATE is retired, see "
                 + "GUILD TECH's rate ratio): " + string.Join(", ", sizes.ToArray()));
        }

        /// <summary>
        /// The server charges and pays; the file predicts. Every pay constant and every node row
        /// in 0011 must be the file's.
        /// </summary>
        private static void CheckServerAgrees(ContentDatabase content, string contentDir)
        {
            GuildPathConfig cfg = content.GuildPath;
            string sql = ReadMigration(contentDir);
            if (sql.Length == 0) { Check(false, "0011_guild_path.sql is in Server/supabase/migrations"); return; }
            int attempt = SqlConstant(sql, "guild_path_pay_attempt"), clear = SqlConstant(sql, "guild_path_pay_clear");
            int point = SqlConstant(sql, "guild_path_pay_board_point"), counted = SqlConstant(sql, "guild_path_boards_counted");
            int floor = SqlConstant(sql, "guild_path_board_floor");
            Check(attempt == cfg.PayAttempt && clear == cfg.PayClear && point == cfg.PayBoardPoint
                  && counted == cfg.BoardsCounted && floor == cfg.BoardFloor,
                  "0011 pays what guild-path.json says: attempt " + attempt + ", clear " + clear + ", board point " + point
                  + ", " + counted + " boards counted, on floor " + floor);

            Dictionary<string, string[]> rows = new Dictionary<string, string[]>();
            foreach (Match m in Regex.Matches(sql, @"\('([a-z0-9-]+)',\s*(\d+),\s*(null|'([a-z0-9-]+)')\)"))
                rows[m.Groups[1].Value] = new[] { m.Groups[2].Value, m.Groups[4].Success ? m.Groups[4].Value : "" };
            List<string> wrong = new List<string>();
            for (int i = 0; i < cfg.Nodes.Count; i++)
            {
                GuildPathNodeDef n = cfg.Nodes[i];
                string[] row;
                if (!rows.TryGetValue(n.Id, out row)) { wrong.Add(n.Id + " missing"); continue; }
                if (row[0] != n.Cost.ToString(CultureInfo.InvariantCulture) || row[1] != n.Requires)
                    wrong.Add(n.Id + " is " + row[0] + "/" + (row[1].Length > 0 ? row[1] : "root"));
            }
            if (rows.Count != cfg.Nodes.Count) wrong.Add(rows.Count + " rows for " + cfg.Nodes.Count + " nodes");
            Check(wrong.Count == 0, "0011's guild_path_nodes rows are guild-path.json's nodes, price and requirement"
                  + (wrong.Count > 0 ? " - " + string.Join(", ", wrong.ToArray()) : ""));
        }

        /// <summary>
        /// THE FOUR NODES ARE A CLOSED SET NOW.
        ///
        /// This check had a second half: the same line fought the same fight, event for event, for
        /// a profile owning every node and one owning none - "owning the whole Path changes no
        /// fight". claude/DESIGN-GUILD-TECH.md RETIRES THAT RULE. Guild progression is real power,
        /// the guild tech tree is where it lives, and what replaces the rule is G5 -
        /// CheckGuildTechNeverGates, modelled on the fusion guard: guild tech accelerates a wall
        /// and never gates one, proved by walks that carry none of it and finish anyway.
        ///
        /// What survives is the ALLOWLIST, and it survives for a narrower reason than it had. These
        /// four nodes are built, are paid for, and are guild level rewards now (G9). They are not
        /// where power goes and nothing new should be added to them - a fifth kind belongs in the
        /// tech tree, where it is measured. So a kind added to GuildPathNodeKind fails the build
        /// until somebody has decided which of the two systems it is part of.
        /// </summary>
        private static void CheckEveryNodeIsHorizontal(ContentDatabase content)
        {
            string[] vetted = { "Line", "LineTalents" };
            string[] kinds = Enum.GetNames(typeof(GuildPathNodeKind));
            Check(string.Join(",", kinds) == string.Join(",", vetted),
                  "the Path's node kinds are the closed set the tech tree inherited - a saved line, a line that keeps talents ("
                  + string.Join(", ", kinds) + "); anything new belongs in guild-tech.json, where it is measured");
        }

        /// <summary>Lines: what each node allows, what a switch keeps, and version 17.</summary>
        private static void CheckLines(ContentDatabase content)
        {
            PlayerProfile p = GameSession.CreateNewProfile(content, 0);
            GameSession s = new GameSession(content, p);
            p.Shards = (long)content.Balance.SummonCost * 40;
            for (int i = 0; i < 30; i++) s.Summon(9000 + i, 0);
            HeroInstance talented = p.Heroes.Find(h => h.Def != null && h.Def.Talents.Count >= 2);

            Check(s.LinesAllowed == 1 && !s.SwitchLine(1), "with no node there is one line and nothing to switch to");

            GuildPathState st = new GuildPathState();
            st.Balance = 25;
            st.Nodes.Add("second-line");
            s.ApplyGuildPathState(st);
            string[] first = (string[])p.Team.Clone();
            bool switched = s.SwitchLine(1);
            // Build line II out of the bench.
            List<HeroInstance> bench = p.Heroes.FindAll(h => !p.IsDeployed(h.Uid));
            for (int i = 0; i < 2 && i < bench.Count; i++) p.AssignToTeam(i, bench[i].Uid);
            string[] second = (string[])p.Team.Clone();
            s.SwitchLine(0);
            bool backToFirst = Same(p.Team, first);
            s.SwitchLine(1);
            bool backToSecond = Same(p.Team, second);
            Check(switched && backToFirst && backToSecond && !Same(first, second),
                  "a Line node adds a line, and switching away and back restores each exactly");

            bool talentsHeld = true;
            if (talented != null)
            {
                // Ascended far enough to reach slot 2, because this check's subject is the LINE
                // node and not the ascension gate: SelectTalent refuses a slot the hero has not
                // unlocked (DESIGN-TALENTS T6), and a summoned hero starts at tier 0 with only
                // talent 1 available. CheckTalentsAreLockedByAscension is where the gate is read.
                talented.Ascension = HeroInstance.TierUnlockingTalent(2);
                // Standing on the tier's first Level, where Ascend would have put it. Left at
                // Level 1 it is under its own tier, which the save clamps up on load (version 18),
                // and the byte-for-byte round trip below would be reading that clamp, not the lines.
                // Only a 5-star has a tier to stand under; below five stars the cap ignores it.
                if (talented.Def.Stars >= 5)
                    talented.Level = Emberfall.Core.Progression.StatMath.TierBaseLevel(content.Balance, talented.Ascension);
                s.SwitchLine(0);
                p.AssignToTeam(0, talented.Uid);
                s.SelectTalent(talented, 1);
                s.SwitchLine(1);
                s.SelectTalent(talented, 2);
                s.SwitchLine(0);
                talentsHeld = talented.SelectedTalent == 2; // without the node, a switch leaves talents alone
                st.Nodes.Add("line-talents");
                s.ApplyGuildPathState(st);
                s.SwitchLine(0);
                s.SelectTalent(talented, 1);
                s.SwitchLine(1);
                s.SelectTalent(talented, 2);
                s.SwitchLine(0);
                talentsHeld &= talented.SelectedTalent == 1;
                s.SwitchLine(1);
                talentsHeld &= talented.SelectedTalent == 2 || !p.IsDeployed(talented.Uid);
            }
            Check(talented != null && talentsHeld,
                  "without Lines Keep Talents a switch leaves talents alone; with it each line puts back the picks it was left with");

            string json = SaveSerializer.Serialize(p);
            string err;
            PlayerProfile back = SaveSerializer.Deserialize(json, content, out err);
            Check(back != null && back.Waymarks == 25 && back.GuildPathNodes.Count == 2 && back.Lines.Count == p.Lines.Count
                  && back.ActiveLine == p.ActiveLine && Same(back.Team, p.Team) && SaveSerializer.Serialize(back) == json,
                  "save version " + SaveSerializer.BaseVersion + " carries the Waymarks, the nodes and every line, byte for byte");

            PlayerProfile plain = GameSession.CreateNewProfile(content, 0);
            string plainJson = SaveSerializer.Serialize(plain);
            Check(!plainJson.Contains("guildPath") && !plainJson.Contains("\"lines\""),
                  "a profile the Path never touched writes none of the new keys");
            string v16 = Regex.Replace(json, @"""version""(\s*):(\s*)" + SaveSerializer.BaseVersion, @"""version""$1:${2}16");
            v16 = v16.Replace("\"guildPath\"", "\"guildPathIgnored\"").Replace("\"lines\"", "\"linesIgnored\"");
            PlayerProfile up = SaveSerializer.Deserialize(v16, content, out err);
            Check(up != null && up.Waymarks == 0 && up.GuildPathNodes.Count == 0 && up.Lines.Count == 0 && up.ActiveLine == 0,
                  "a version 16 save loads with no Waymarks, no nodes and one line");

            st.Nodes.Clear();
            s.ApplyGuildPathState(st);
            Check(p.ActiveLine == 0 && s.LinesAllowed == 1, "a server answer owning fewer lines puts the formation back on line I");
        }

        private static GuildDungeonAttempt Row(string user, int attempt, bool victory, int turns, int fallen, TeamSnapshot team, int seed, long id)
        {
            GuildDungeonAttempt a = new GuildDungeonAttempt();
            a.UserId = user; a.Attempt = attempt; a.Victory = victory; a.Turns = turns; a.Fallen = fallen;
            a.Team = team; a.Seed = seed; a.Floor = 1; a.Cycle = 1; a.Id = id;
            return a;
        }

        /// <summary>A dungeon seed whose floor 1 carries no condition, so the hand-built boards
        /// above are read on the line alone.</summary>
        private static int NoConditionSeed(ContentDatabase content)
        {
            for (int c = 1; c < 500; c++)
            {
                int seed = GuildDungeon.SeedFor("path-rules", c);
                if (GuildDungeon.ConditionOf(content, seed, 1) == null) return seed;
            }
            return GuildDungeon.SeedFor("path-rules", 1);
        }

        private static string ReadMigration(string contentDir)
        {
            string dir = contentDir;
            for (int up = 0; up < 6 && dir != null; up++)
            {
                string path = Path.Combine(dir, Path.Combine("Server", Path.Combine("supabase", Path.Combine("migrations", "0011_guild_path.sql"))));
                if (File.Exists(path)) return File.ReadAllText(path);
                dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }
            return "";
        }

        private static int SqlConstant(string sql, string function)
        {
            Match m = Regex.Match(sql, "function public\\." + function + "\\(\\)[^$]*\\$\\$ select (\\d+); \\$\\$");
            return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : -1;
        }

        private static bool Within(int written, double derived)
        {
            return Math.Abs(written - derived) <= PayTolerance * Math.Max(1.0, derived);
        }

        private static bool Same(string[] a, string[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static double Sum(List<float> v) { double s = 0; for (int i = 0; i < v.Count; i++) s += v[i]; return s; }
        private static bool Near(double a, double b) { return Math.Abs(a - b) < 1e-4; }

        // ---------------------------------------------------------------- helpers

        private static double Share(int a, int b) { return b == 0 ? 0 : (double)a / b; }

        private static double Quantile(List<double> sorted, double q)
        {
            if (sorted.Count == 0) return 0;
            int i = (int)Math.Floor(q * (sorted.Count - 1));
            return sorted[i];
        }

        private static string F1(double v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }
        private static string F2(double v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        private static string F3(double v) { return v.ToString("0.000", CultureInfo.InvariantCulture); }
        private static string Pct(double v) { return (v * 100).ToString("0", CultureInfo.InvariantCulture) + "%"; }

        private static void Line(string text) { Console.WriteLine(text); }

        private static void Check(bool condition, string label)
        {
            if (condition) Line("  [ok]   " + label);
            else { _failures++; Line("  [FAIL] " + label); }
        }
    }
}
