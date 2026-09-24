using System;
using System.Collections.Generic;
using System.Globalization;
using Emberfall.Core.Cloud;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// EVERY GUILD TALENT ACROSS THE FLOOR THEMES AND CONDITIONS, AND THE RULE THAT READS THE
    /// RESULT (claude/DESIGN-GUILD-TECH.md, G10).
    ///
    /// WHY THIS EXISTS, and what it replaces. GuildTalentParity.cs grades every guild talent the
    /// way a hero talent is graded: one arena, one pinned line, worth in % Attack. That reading is
    /// the right one for G8 - "can this be felt at all" - and it is the wrong one for G4, because
    /// one arena can only ever hand back one ordering. It did: Line read Dig In, Hold the Wall,
    /// Answer in Kind in that order and Grace read the same shape, and the rule written against it
    /// - the strongest option within x3 of the weakest - passed both while a guild would still have
    /// taken the biggest number every time. A ratio cannot tell a real choice from a dominated one;
    /// only a second dimension can, and the dungeon already has one.
    ///
    /// THE DIMENSION. A guild dungeon floor draws a THEME - Swarm, Wall, Hunt, Attrition, Champion
    /// - and a CONDITION from the pool that theme allows. The condition pool was built, and is
    /// re-checked on every build by GuildDungeonChecks.CheckConditionShape, specifically so that it
    /// changes WHICH LINE YOU BRING rather than how hard the floor is (C5). A talent read against
    /// that spread either answers some floors better than others, in which case the choice between
    /// it and its siblings is real, or it answers all of them best, in which case it is the pick
    /// regardless of any ratio.
    ///
    /// THE ARENA IS THE ONE A GUILD FIGHTS IN, not the campaign ladder. The lines are
    /// GuildDungeonChecks' own sample rosters, the floors are generated dungeon floors, and the
    /// talent arrives the way it arrives in play: on the PROFILE, applying to every hero of its
    /// family in the line at once. No rung is bought in any arm, so what is read is the talent and
    /// not the stat track it sits on.
    ///
    /// THE POPULATION IS PER FAMILY. A line holding no Bulwark and no Vanguard reads nothing from
    /// any Line talent, and averaging those zeroes in would measure how many rosters own a tank
    /// rather than which talent is the better pick. Membership is decided AFTER the line is
    /// conformed to the floor's rules, which is the point: under No Menders a Grace line is Binders
    /// and Wardens, and a Grace talent that keys on healing should read weaker there. That is the
    /// situationality this instrument exists to find.
    ///
    /// EVERY NUMBER IS DERIVED. The reading is the dungeon's own board order; the bar it is read against is what
    /// the SAME floors move by when nothing changes at all - the unconditioned floor re-fought
    /// under a second combat seed, which is the chance floor the condition study already reads
    /// every condition against. Nothing here is authored but the sample sizes.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>
        /// Dungeons the themes are drawn from. One dungeon deals every theme - four open themes
        /// over its first four floors and Champion last - so a handful of seeds gives each theme
        /// several floors at several powers rather than one floor at one.
        ///
        /// THE GATE AND THE STUDY READ THE SAME ARENAS, which is why there is one number here and
        /// not two. Every other study in this harness runs the gate smaller, and that is right when
        /// the gate asks a question with a fixed answer. This one does not: the bar a lead has to
        /// clear is DERIVED from how many arenas were fought, so a smaller gate has a higher bar,
        /// and a family can pass the study a content author is reading and fail the build on the
        /// same content. That disagreement is not something anybody can act on. The gate costs
        /// about forty seconds and reads exactly what `--guild-talent-themes` prints.
        /// </summary>
        private const int GtThemeGuilds = 8;

        /// <summary>Sample roster sets per arena, each GuildDungeonChecks.SampleRosters' own
        /// spread of 66 lines.</summary>
        private const int GtThemePopulations = 4;

        private const int GtThemeSeed = 0x6C17;

        /// <summary>The second combat seed the chance floor is read under. The condition study's
        /// own salt, for the same measurement.</summary>
        private const int GtThemeReseed = 0x5A5A;

        private static readonly GuildDungeonTheme[] GtThemes =
        {
            GuildDungeonTheme.Swarm, GuildDungeonTheme.Wall, GuildDungeonTheme.Hunt,
            GuildDungeonTheme.Attrition, GuildDungeonTheme.Champion
        };

        /// <summary>One floor fought one way: a generated floor of some theme, under one condition
        /// its theme may draw or under none, against lines already conformed to that condition's
        /// rules so that every arm fights the SAME line.</summary>
        private sealed class GtArena
        {
            public int Seed, Floor, FightSeed;
            public GuildDungeonTheme Theme;
            public FloorConditionDef Condition;
            public List<GuildDungeonChecks.Sample> Lines;
            /// <summary>[family][line]: whether that line holds a hero of that family at all.</summary>
            public bool[][] Holds;
            public string Label { get { return Condition == null ? "(none)" : Condition.Id; } }
        }

        /// <summary>One talent's reading on one theme, pooled over that theme's arenas.</summary>
        private sealed class GtThemeRow
        {
            public string Family = "", Id = "", Name = "";
            /// <summary>THE READING, indexed by GtThemes: the net share of that family's lines the
            /// talent moves up the dungeon's own board. Margin is carried beside it for the table,
            /// because a reader wants to know whether a talent won by killing or by surviving.</summary>
            public double[] Board = new double[GtThemes.Length];
            public double[] Margin = new double[GtThemes.Length];
            public int[] Arenas = new int[GtThemes.Length];
            /// <summary>Themes this talent is the family's best pick on, by more than the floor.</summary>
            public readonly List<GuildDungeonTheme> Wins = new List<GuildDungeonTheme>();
        }

        /// <summary>
        /// The arenas, built once and in order so the reading does not depend on how the fights are
        /// scheduled. Every floor of every sampled dungeon, once with no condition and once per
        /// condition that floor's theme may draw.
        /// </summary>
        private static List<GtArena> GtBuildArenas(ContentDatabase db, int guilds, int populations)
        {
            GuildDungeonChecks.ReferenceLevel = db.Balance.GuildDungeonHeroLevel;
            int floors = Math.Max(1, db.Balance.GuildDungeonFloors);

            List<GuildDungeonChecks.Sample> population = new List<GuildDungeonChecks.Sample>();
            for (int k = 0; k < populations; k++)
                population.AddRange(GuildDungeonChecks.SampleRosters(db, GtThemeSeed + k));

            GuildTechConfig tree = db.GuildTech;
            List<GtArena> arenas = new List<GtArena>();
            for (int g = 0; g < guilds; g++)
            {
                int seed = GuildDungeon.SeedFor("guild-talent-theme-" + g, 1);
                GuildDungeonTheme[] themes = GuildDungeonGenerator.ThemesFor(floors, seed);
                for (int f = 1; f <= floors; f++)
                {
                    GuildDungeonTheme theme = themes[f - 1];
                    arenas.Add(GtMakeArena(db, tree, population, seed, f, theme, null));
                    for (int c = 0; c < db.FloorConditions.Count; c++)
                        if (db.FloorConditions[c].AllowsTheme(theme))
                            arenas.Add(GtMakeArena(db, tree, population, seed, f, theme, db.FloorConditions[c]));
                }
            }
            return arenas;
        }

        private static GtArena GtMakeArena(ContentDatabase db, GuildTechConfig tree, List<GuildDungeonChecks.Sample> population,
                                       int seed, int floor, GuildDungeonTheme theme, FloorConditionDef condition)
        {
            GtArena a = new GtArena();
            a.Seed = seed;
            a.Floor = floor;
            a.Theme = theme;
            a.Condition = condition;
            a.FightSeed = GuildDungeon.FightSeedFor(seed, floor);

            // Conformed once, here, so every arm fights the identical line and the reading is the
            // talent rather than a different draw. A player answers a line rule by changing their
            // line; the arms all answer it the same way, because what is being compared is what
            // the talent does to that answer.
            DeterministicRng rng = new DeterministicRng(GuildDungeon.SeedFor("conform-" + (condition != null ? condition.Id : "none"), floor) ^ seed);
            a.Lines = new List<GuildDungeonChecks.Sample>(population.Count);
            for (int i = 0; i < population.Count; i++)
                a.Lines.Add(GuildDungeonChecks.Conform(db, population[i], condition, rng));

            a.Holds = new bool[tree.Families.Count][];
            for (int f = 0; f < tree.Families.Count; f++)
            {
                a.Holds[f] = new bool[a.Lines.Count];
                for (int i = 0; i < a.Lines.Count; i++)
                    a.Holds[f][i] = GtLineHolds(db, tree.Families[f], a.Lines[i]);
            }
            return a;
        }

        private static bool GtLineHolds(ContentDatabase db, GuildTechFamilyDef family, GuildDungeonChecks.Sample line)
        {
            for (int i = 0; i < line.Team.Units.Count; i++)
            {
                HeroDef d = db.GetHero(line.Team.Units[i].DefId);
                if (d != null && family.Roles.Contains(d.Role)) return true;
            }
            return false;
        }

        /// <summary>A profile carrying one guild talent and nothing else: the guild at the level
        /// that opens every slot, one family's talent picked, no rung bought anywhere. Null talent
        /// gives the empty tree, which is the control arm and the reference player's own state
        /// (G5).</summary>
        private static PlayerProfile GtThemeProfile(ContentDatabase db, GuildTechFamilyDef family, string talentId)
        {
            PlayerProfile p = GameSession.CreateNewProfile(db, 0);
            if (family != null && !string.IsNullOrEmpty(talentId))
            {
                p.GuildTech.Level = db.GuildTech.LevelCeiling;
                p.GuildTech.Talents[family.Id] = talentId;
            }
            return p;
        }

        /// <summary>
        /// ONE ATTEMPT, AS THE DUNGEON RECORDS IT. Exactly the three fields
        /// GuildDungeon.RecordOf writes and GuildDungeon.Compare then ranks a board by, plus the
        /// margin for the table.
        /// </summary>
        private struct GtAttempt
        {
            public bool Cleared;
            public int Turns, Fallen;
            public float Margin;
        }

        /// <summary>One arm's attempt on every line of one arena.</summary>
        private static void GtFightArena(ContentDatabase db, GtArena a, PlayerProfile profile, int fightSeed, GtAttempt[] into)
        {
            for (int i = 0; i < a.Lines.Count; i++)
            {
                BattleResult r = GtDungeonFightAs(db, a.Lines[i].Team, a.Seed, a.Floor, profile, a.Condition, fightSeed);
                into[i].Cleared = r.Victory;
                into[i].Turns = r.Rounds;
                into[i].Fallen = GuildDungeon.FallenIn(r);
                into[i].Margin = r.PlayerHpRatio - r.EnemyHpRatio;
            }
        }

        /// <summary>
        /// WHETHER ONE ATTEMPT IS BETTER THAN ANOTHER, AND THE GAME DECIDES IT, NOT THIS FILE.
        /// GuildDungeon.Compare is the guild dungeon's own board order - fewer turns first, then
        /// fewer fallen - and a floor that was not cleared is behind every floor that was, because
        /// an uncleared floor posts no attempt at all. +1 better, -1 worse, 0 neither.
        ///
        /// THE READING IS THIS AND NOT THE MARGIN, and that was a correction rather than a choice.
        /// Margin is hero HP kept less enemy HP kept, so for a won fight it IS hero HP kept, and
        /// every defensive talent in the tree read enormous against every offensive one: a ward on
        /// the whole line measured three times what killing faster did. Read that way the
        /// instrument does not compare three options, it asks which of them is a defensive one.
        /// The board asks for speed first and losses second, which is what a guild is actually
        /// playing for, and it prices the two halves without this harness inventing a weight.
        /// </summary>
        private static int GtBetter(GtAttempt with, GtAttempt without)
        {
            if (with.Cleared != without.Cleared) return with.Cleared ? 1 : -1;
            if (!with.Cleared) return 0;
            if (with.Turns != without.Turns) return with.Turns < without.Turns ? 1 : -1;
            if (with.Fallen != without.Fallen) return with.Fallen < without.Fallen ? 1 : -1;
            return 0;
        }

        /// <summary>The net share of that family's lines the arm moves UP the board: better minus
        /// worse, over the lines that hold a hero of the family.</summary>
        private static double GtBoardOver(GtAttempt[] with, GtAttempt[] without, bool[] holds)
        {
            int net = 0, n = 0;
            for (int i = 0; i < with.Length; i++)
            {
                if (!holds[i]) continue;
                n++;
                net += GtBetter(with[i], without[i]);
            }
            return n == 0 ? 0 : (double)net / n;
        }

        private static double GtMarginOver(GtAttempt[] value, bool[] holds)
        {
            double sum = 0; int n = 0;
            for (int i = 0; i < value.Length; i++) if (holds[i]) { sum += value[i].Margin; n++; }
            return n == 0 ? 0 : sum / n;
        }

        /// <summary>
        /// READS EVERY GUILD TALENT ON EVERY THEME, and in the gate fails the build for a family
        /// whose choice is not one (G10). Prints the per-theme table and the per-condition
        /// breakdown when `study`.
        ///
        /// THE RULE. On each theme, the family's best pick is the talent whose reading leads every
        /// sibling's by more than what chance alone moves that theme by. A family passes when at
        /// least two of its talents are the best pick on some theme; it fails when fewer are, which
        /// is one of exactly two shapes:
        ///
        ///   * DOMINANT - one talent is never out-picked. A guild reads the tree once, takes it,
        ///     and its siblings are flavour text. This is the failure the x3 ratio rule was written
        ///     to catch and could not: a talent can lead every theme by 20% and sit well inside x3.
        ///   * UNDIFFERENTIATED - no theme separates them at all. Three options nobody can tell
        ///     apart, which G4 calls one option with three names.
        ///
        /// A ratio is deliberately absent. Two talents may measure a factor apart and still both be
        /// real picks, as long as each answers a floor the other does not; two talents a hair apart
        /// are still a fake choice if the same one always leads. What matters is whether a guild
        /// facing a Wall cycle and a guild facing a Swarm cycle would reasonably pick differently,
        /// and that is a question about themes, not about magnitudes.
        /// </summary>
        private static void GuildTalentThemes(ContentDatabase db, bool study)
        {
            GuildTechConfig tree = db.GuildTech;
            int talents = 0;
            for (int f = 0; f < tree.Families.Count; f++) talents += tree.Families[f].Talents.Count;
            if (talents == 0) return;

            List<GtArena> arenas = GtBuildArenas(db, GtThemeGuilds, GtThemePopulations);

            // The arms, in a fixed order: the control, then every talent of every family, then the
            // control again under a second combat seed. Profiles are read-only inside the
            // simulation, so one of each is built here and shared by every arena.
            List<PlayerProfile> profiles = new List<PlayerProfile> { GtThemeProfile(db, null, null) };
            List<int> armFamily = new List<int> { -1 };
            List<GtThemeRow> rows = new List<GtThemeRow>();
            for (int f = 0; f < tree.Families.Count; f++)
                for (int t = 0; t < tree.Families[f].Talents.Count; t++)
                {
                    PassiveDef talent = tree.Families[f].Talents[t];
                    profiles.Add(GtThemeProfile(db, tree.Families[f], talent.Id));
                    armFamily.Add(f);
                    GtThemeRow row = new GtThemeRow();
                    row.Family = tree.Families[f].Name;
                    row.Id = talent.Id;
                    row.Name = talent.Name;
                    rows.Add(row);
                }
            int arms = profiles.Count;

            if (study)
            {
                Line("  EVERY GUILD TALENT ACROSS THE FLOOR THEMES (G10) - the arena a guild actually fights in");
                Line("    " + GtThemeGuilds + " dungeons x " + db.Balance.GuildDungeonFloors + " floors, each fought with no condition and under every");
                Line("    condition its theme may draw: " + arenas.Count + " arenas, " + arenas[0].Lines.Count + " sample lines each, level "
                     + db.Balance.GuildDungeonHeroLevel);
                Line("    reading: the net share of that family's lines the talent moves UP THE DUNGEON'S OWN BOARD -");
                Line("             GuildDungeon.Compare, fewer turns then fewer fallen, a clear ahead of a wipe - against the same");
                Line("             floors with an empty tree. Over the lines of that family only; no rung is bought in any arm.");
                Line("             The margin line under each is hero HP kept less enemy HP kept, for colour, and is NOT the rule");
                Line("    the bar: what chance alone puts in a reading this size - the unconditioned floors re-fought under a");
                Line("             second combat seed, their spread, pooled over the same arenas and doubled for the two talents");
                Line("             being compared. A lead smaller than the bar is not a lead. A * marks the theme's best pick");
                Line("");
            }

            // Every arena is independent - combat is a pure function - so they are fought in
            // parallel and merged in arena order afterwards, which keeps every printed number
            // identical to a one-thread run. The condition study is parallelised the same way.
            double[][] board = new double[arenas.Count][];    // [arena][arm * families + family]
            double[][] margin = new double[arenas.Count][];
            double[] chance = new double[arenas.Count * tree.Families.Count];
            int fams = tree.Families.Count;
            int done = 0;
            System.Threading.Tasks.Parallel.For(0, arenas.Count, index =>
            {
                GtArena a = arenas[index];
                GtAttempt[] bare = new GtAttempt[a.Lines.Count];
                GtAttempt[] arm0 = new GtAttempt[a.Lines.Count];
                double[] ab = new double[arms * fams], am = new double[arms * fams];
                GtFightArena(db, a, profiles[0], a.FightSeed, bare);
                for (int f = 0; f < fams; f++) am[f] = GtMarginOver(bare, a.Holds[f]);
                for (int arm = 1; arm < arms; arm++)
                {
                    GtFightArena(db, a, profiles[arm], a.FightSeed, arm0);
                    for (int f = 0; f < fams; f++)
                    {
                        ab[arm * fams + f] = GtBoardOver(arm0, bare, a.Holds[f]);
                        am[arm * fams + f] = GtMarginOver(arm0, a.Holds[f]);
                    }
                }
                // The chance floor: the control arm again, one combat seed different. Nothing about
                // the fight changed, so whatever this moves the reading by is chance, and it is the
                // scale every talent's reading has to be measured against. Kept SIGNED, because
                // what the bar is built from is its spread and not its sign.
                GtFightArena(db, a, profiles[0], a.FightSeed ^ GtThemeReseed, arm0);
                for (int f = 0; f < fams; f++)
                    chance[index * fams + f] = GtBoardOver(arm0, bare, a.Holds[f]);
                board[index] = ab;
                margin[index] = am;
                int n = System.Threading.Interlocked.Increment(ref done);
                if (study) Console.Error.WriteLine("  themes: arena " + n + " of " + arenas.Count);
            });

            // ---- pooled per theme, in arena order
            double[,] floorSum = new double[fams, GtThemes.Length];
            int[,] floorN = new int[fams, GtThemes.Length];
            for (int index = 0; index < arenas.Count; index++)
            {
                int th = Array.IndexOf(GtThemes, arenas[index].Theme);
                if (th < 0) continue;
                for (int arm = 1; arm < arms; arm++)
                {
                    int f = armFamily[arm];
                    GtThemeRow row = rows[arm - 1];
                    row.Board[th] += board[index][arm * fams + f];
                    row.Margin[th] += margin[index][arm * fams + f] - margin[index][f];
                    row.Arenas[th]++;
                }
                for (int f = 0; f < fams; f++)
                {
                    double r = chance[index * fams + f];
                    floorSum[f, th] += r * r;
                    floorN[f, th]++;
                }
            }
            for (int i = 0; i < rows.Count; i++)
                for (int th = 0; th < GtThemes.Length; th++)
                    if (rows[i].Arenas[th] > 0)
                    {
                        rows[i].Board[th] /= rows[i].Arenas[th];
                        rows[i].Margin[th] /= rows[i].Arenas[th];
                    }

            // THE BAR, DERIVED AND POOLED THE WAY THE READING IS. One arena's chance delta has a
            // spread; the root-mean-square of them is that spread, and a reading pooled over n
            // arenas carries it n times smaller. Two talents are compared to each other rather than
            // to the control, and each one's chaos is its own, so the difference carries it twice
            // over - the root two. Nothing here is authored: the spread is measured on these very
            // floors and n is how many of them there were.
            double[,] floor = new double[fams, GtThemes.Length];
            for (int f = 0; f < fams; f++)
                for (int th = 0; th < GtThemes.Length; th++)
                    floor[f, th] = floorN[f, th] > 0
                        ? Math.Sqrt(2.0 * floorSum[f, th] / floorN[f, th] / floorN[f, th]) : 0;

            // ---- who answers which theme
            for (int f = 0; f < fams; f++)
            {
                List<GtThemeRow> mine = new List<GtThemeRow>();
                for (int arm = 1; arm < arms; arm++) if (armFamily[arm] == f) mine.Add(rows[arm - 1]);
                for (int th = 0; th < GtThemes.Length; th++)
                {
                    int lead = -1;
                    double best = double.MinValue, second = double.MinValue;
                    for (int i = 0; i < mine.Count; i++)
                    {
                        if (mine[i].Arenas[th] == 0) continue;
                        double v = mine[i].Board[th];
                        if (v > best) { second = best; best = v; lead = i; }
                        else if (v > second) second = v;
                    }
                    if (lead >= 0 && second > double.MinValue && best - second > floor[f, th])
                        mine[lead].Wins.Add(GtThemes[th]);
                }
            }

            if (study) GtReportThemes(db, tree, rows, armFamily, floor, arenas, board, margin, arms, fams);

            // ---- the rule
            List<string> broken = new List<string>();
            for (int f = 0; f < fams; f++)
            {
                GuildTechFamilyDef family = tree.Families[f];
                if (family.Talents.Count < 2) continue;
                List<GtThemeRow> mine = new List<GtThemeRow>();
                for (int arm = 1; arm < arms; arm++) if (armFamily[arm] == f) mine.Add(rows[arm - 1]);

                int answerers = 0;
                GtThemeRow only = null;
                for (int i = 0; i < mine.Count; i++) if (mine[i].Wins.Count > 0) { answerers++; only = mine[i]; }
                if (answerers >= 2) continue;

                if (answerers == 1)
                    broken.Add(family.Name + ": " + only.Name + " is the best pick on every theme it can be drawn against ("
                               + GtThemeList(only.Wins) + "), and no sibling answers one");
                else
                    broken.Add(family.Name + ": no theme separates its three options at all");
            }
            Check(broken.Count == 0,
                  "G10 - EVERY FAMILY'S CHOICE IS A REAL ONE: on each floor theme the best pick is the talent that leads its "
                  + "siblings by more than what chance alone moves that theme by, and every family has at least two talents that "
                  + "are the best pick on some theme - so a guild facing a Wall cycle and a guild facing a Swarm cycle pick "
                  + "differently" + (broken.Count > 0 ? " - " + string.Join("; ", broken.ToArray()) : ""));

            if (!study)
            {
                List<string> answered = new List<string>();
                for (int f = 0; f < fams; f++)
                {
                    List<string> per = new List<string>();
                    for (int th = 0; th < GtThemes.Length; th++)
                    {
                        string who = "-";
                        for (int arm = 1; arm < arms; arm++)
                            if (armFamily[arm] == f && rows[arm - 1].Wins.Contains(GtThemes[th])) who = rows[arm - 1].Name;
                        per.Add(GtThemeShort(GtThemes[th]) + " " + who);
                    }
                    answered.Add(tree.Families[f].Name + " [" + string.Join(", ", per.ToArray()) + "]");
                }
                Line("    who answers which theme: " + string.Join("  ", answered.ToArray()));
            }
        }

        private static void GtReportThemes(ContentDatabase db, GuildTechConfig tree, List<GtThemeRow> rows, List<int> armFamily,
                                           double[,] floor, List<GtArena> arenas, double[][] board, double[][] margin,
                                           int arms, int fams)
        {
            Line("    " + "family / talent".PadRight(34) + GtHeader());
            for (int f = 0; f < fams; f++)
            {
                for (int arm = 1; arm < arms; arm++)
                {
                    if (armFamily[arm] != f) continue;
                    GtThemeRow r = rows[arm - 1];
                    string text = "    " + (r.Family + "/" + r.Name).PadRight(34);
                    for (int th = 0; th < GtThemes.Length; th++)
                        text += (r.Arenas[th] == 0 ? "-" : GtPp(r.Board[th]) + (r.Wins.Contains(GtThemes[th]) ? "*" : "")).PadLeft(10);
                    Line(text + "    answers " + (r.Wins.Count == 0 ? "no theme" : GtThemeList(r.Wins)));
                    string mt = "    " + "".PadRight(34);
                    for (int th = 0; th < GtThemes.Length; th++)
                        mt += (r.Arenas[th] == 0 ? "-" : GtPp(r.Margin[th])).PadLeft(10);
                    Line(mt + "      (margin)");
                }
                string bar = "    " + ("  the bar a lead must clear").PadRight(34);
                for (int th = 0; th < GtThemes.Length; th++) bar += F(floor[f, th] * 100, "0.0").PadLeft(10);
                Line(bar);
                Line("");
            }

            // The same reading split by condition, which is the other half of what the pool does:
            // a theme is a shape of enemy, a condition is the rule the floor is fought under, and
            // a talent can answer one without answering the other.
            Line("    BY THEME AND CONDITION - margin points, per family's own lines");
            for (int f = 0; f < fams; f++)
            {
                for (int th = 0; th < GtThemes.Length; th++)
                {
                    List<string> labels = new List<string>();
                    for (int index = 0; index < arenas.Count; index++)
                        if (arenas[index].Theme == GtThemes[th] && !labels.Contains(arenas[index].Label)) labels.Add(arenas[index].Label);
                    for (int l = 0; l < labels.Count; l++)
                    {
                        string text = "    " + (tree.Families[f].Name + " " + GtThemeShort(GtThemes[th]) + " / " + labels[l]).PadRight(34);
                        for (int arm = 1; arm < arms; arm++)
                        {
                            if (armFamily[arm] != f) continue;
                            double sum = 0; int n = 0;
                            for (int index = 0; index < arenas.Count; index++)
                            {
                                if (arenas[index].Theme != GtThemes[th] || arenas[index].Label != labels[l]) continue;
                                sum += board[index][arm * fams + f];
                                n++;
                            }
                            text += (rows[arm - 1].Name + " " + GtPp(n == 0 ? 0 : sum / n)).PadLeft(28);
                        }
                        Line(text);
                    }
                }
                Line("");
            }
        }

        private static string GtHeader()
        {
            string s = "";
            for (int i = 0; i < GtThemes.Length; i++) s += GtThemeShort(GtThemes[i]).PadLeft(10);
            return s;
        }

        private static string GtThemeShort(GuildDungeonTheme t)
        {
            return t == GuildDungeonTheme.Attrition ? "Attrit" : t == GuildDungeonTheme.Champion ? "Champ" : t.ToString();
        }

        private static string GtThemeList(List<GuildDungeonTheme> themes)
        {
            List<string> s = new List<string>();
            for (int i = 0; i < themes.Count; i++) s.Add(GtThemeShort(themes[i]));
            return string.Join("+", s.ToArray());
        }

        /// <summary>A margin delta in margin points, signed.</summary>
        private static string GtPp(double v)
        {
            return (v >= 0 ? "+" : "") + (v * 100).ToString("0.0", CultureInfo.InvariantCulture);
        }
    }
}
