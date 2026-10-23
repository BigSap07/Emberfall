using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Emberfall.Core.Cloud;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Json;
using Emberfall.Core.Progression;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// The guild dungeon's harness checks. A file of its own so the guild branch and whoever owns
    /// Program.cs never edit the same lines; Program.Main calls Run once and adds what it returns
    /// to its failure count.
    ///
    /// The section that matters is NOT A HERO CHECK. Everything else here guards plumbing - seeds,
    /// records, boards, the server's copy of two constants. That section guards the idea: a floor
    /// thirty people solve with thirty rosters is a puzzle only if many rosters can solve it. If it
    /// fails, the generator is wrong, not the check, and the fix goes in GuildDungeonGenerator or
    /// the guildDungeon* levels in balance.json.
    /// </summary>
    internal static class GuildDungeonChecks
    {
        private static int _failures;

        /// <summary>
        /// The level every sample roster fights at, which is now simply the level EVERY line fights
        /// the dungeon at: balance.json's guildDungeonHeroLevel, read in Run. GuildDungeon.Fight
        /// normalises every line to it, so a sample drawn at any other level would be fought at
        /// this one anyway; the samples carry it so the table this prints says what was fought.
        ///
        /// Sample rosters carry NO relics, armor or talents. Relics and armor are stripped in the
        /// dungeon anyway (GuildDungeon.NormaliseTeam); talents are not, and a real line may run
        /// one, so a floor these rosters can clear is a floor a real guild can clear and the check
        /// still errs toward calling a floor too hard.
        /// </summary>
        internal static int ReferenceLevel = 40;

        /// <summary>Synthetic guilds the property is asserted over. The generator is what is under
        /// test, so it is asked for several dungeons, not one.</summary>
        private const int SampleGuilds = 6;

        /// <summary>
        /// How many dungeons CheckFloors builds to assert floor composition: 1,000 cycles of five
        /// floors, 5,000 floors. Sized against the rate actually seen: single-Aspect floors turned up
        /// at 1 to 2 in 1,500 when Pinoy's 84 were fielded beside the shipped 12. At 1 in 750 per
        /// floor, 5,000 floors miss every one about 0.1% of the time (0.99867^5000); a 1-in-100
        /// failure is missed about e^-50 of the time. The single seed this replaced built 5 floors,
        /// which misses a 1-in-750 failure 99.3% of the time. Building a floor solves one level, so
        /// this is cheap.
        /// </summary>
        private const int CompositionCycles = 1000;

        /// <summary>A floor must be cleared by at least this share of ALL sample rosters.</summary>
        private const float MinClearShare = 0.40f;

        /// <summary>
        /// And by at least this share of EACH "missing the obvious answer" group - no tank, no
        /// healer, no area damage, no support, no single-target damage, only 1-3 star heroes. A
        /// floor that only a line with a healer can clear is a healer check, and a healer check is
        /// a hero check with the names filed off.
        /// </summary>
        private const float MinGroupClearShare = 0.25f;

        /// <summary>No single hero may appear in more than this share of a floor's clearing lines.
        /// With 5 slots and ~90 heroes, an unremarkable hero turns up in about 6% of random lines.</summary>
        private const float MaxHeroShareOfClears = 0.35f;

        public static int Run(ContentDatabase content, string contentDir)
        {
            _failures = 0;
            ReferenceLevel = content.Balance.GuildDungeonHeroLevel;
            Line("");
            Line("GUILD DUNGEON");
            CheckSeeds();
            CheckFloors(content);
            CheckLevelNormalisation(content);
            CheckRecordReplays(content);
            CheckContentVersion(content, contentDir);
            CheckBoards(content);
            CheckTwoMembers(content);
            CheckServerAgrees(content, contentDir);
            CheckConditionContent(content, contentDir);
            CheckConditionDraw(content, contentDir);
            CheckConditionFight(content, contentDir);
            CheckConditionBoards(content);
            CheckConditionGuardrails(content);
            CheckConditionShape(content);
            CheckNotAHeroCheck(content);
            // SIZE AND ATTEMPTS (phase two, system 1's size rule): the ladder scales with the guild,
            // the floors do not, and a member's attempts are bounded the same at every size.
            CheckLadderTable(content);
            CheckBaseBlockUnmoved(content);
            CheckFloorIsLadderBlind(content);
            CheckDeepFloorsMatchBase(content);
            CheckAttemptBounds(content);
            CheckSizeReach(content);
            CheckRewardPlaceholder(content);
            CheckServerAgreesOnSize(content, contentDir);
            return _failures;
        }

        // ---------------------------------------------------------------- floor conditions

        private static readonly FloorConditionFamily[] AuthoredFamilies =
        {
            FloorConditionFamily.Resource, FloorConditionFamily.Healing, FloorConditionFamily.Composition,
            FloorConditionFamily.EnemyOpening, FloorConditionFamily.Tempo
        };

        private static readonly GuildDungeonTheme[] AllThemes =
        {
            GuildDungeonTheme.Swarm, GuildDungeonTheme.Wall, GuildDungeonTheme.Hunt,
            GuildDungeonTheme.Attrition, GuildDungeonTheme.Champion
        };

        /// <summary>
        /// THE POOL AS CONTENT. Every family the design names except Aspect is authored; Aspect is
        /// not (section 6). The file names no enemy and no hero - the campaign it draws templates
        /// from is about to be replaced, and a condition keyed to one unit is a hero check (C6).
        /// Every number on a card is a number in the rules, so the card cannot promise one fight
        /// and the engine play another.
        /// </summary>
        private static void CheckConditionContent(ContentDatabase content, string contentDir)
        {
            List<FloorConditionDef> pool = content.FloorConditions;
            Line("  floor conditions: " + pool.Count + " in the pool");
            string raw = ReadContent(contentDir, "floor-conditions") ?? "";
            Check(raw.Length > 0 && pool.Count > 0, "floor-conditions.json loads a pool (" + pool.Count + ")");
            Check(!content.Warnings.Exists(w => w.StartsWith("floor-conditions.json")),
                  "and loads without a warning");

            // Every family the design names was authored and measured (DESIGN-GUILD-FLOOR-CONDITIONS
            // section 9). A family is in the pool only if one of its conditions passed C5, so this is
            // a reading, not a requirement: a family the study cut is absent and says so.
            List<string> present = new List<string>(), cut = new List<string>();
            foreach (FloorConditionFamily fam in AuthoredFamilies)
                (pool.Exists(c => c.Family == fam) ? present : cut).Add(fam.ToString());
            Line("    families in the pool: " + string.Join(", ", present.ToArray())
                 + (cut.Count > 0 ? "; none of " + string.Join(", ", cut.ToArray()) + " passed C5, so none is pooled" : ""));
            Check(present.Count >= 3, "the pool spans at least three families, so a cycle's five floors do not all ask one question ("
                  + present.Count + ")");
            Check(!pool.Exists(c => c.Family == FloorConditionFamily.Aspect),
                  "and no Aspect condition is - section 6 of the design is a separate decision");

            string lower = raw.ToLowerInvariant();
            List<string> named = new List<string>();
            foreach (EnemyDef e in content.Enemies)
                if (lower.Contains("\"" + e.Id.ToLowerInvariant() + "\"") || lower.Contains(e.Name.ToLowerInvariant())) named.Add(e.Id);
            foreach (HeroDef h in content.Heroes)
                if (lower.Contains(h.Id.ToLowerInvariant()) || lower.Contains(h.Name.ToLowerInvariant())) named.Add(h.Id);
            Check(named.Count == 0, "no condition names an enemy template or a hero - they are written against themes and primitives"
                  + (named.Count > 0 ? " - names " + string.Join(", ", named.ToArray()) : ""));

            List<string> badText = new List<string>(), noFloor = new List<string>(), requires = new List<string>();
            foreach (FloorConditionDef c in pool)
            {
                string why = TextAgreesWithRules(c);
                if (why.Length > 0) badText.Add(c.Id + " (" + why + ")");
                if (c.Themes.Count == 0) noFloor.Add(c.Id);
                if (c.Line.BannedRoles.Count > 0 && c.Line.BannedRoles.Count >= Enum.GetValues(typeof(HeroRole)).Length - 1)
                    requires.Add(c.Id);
            }
            Check(badText.Count == 0, "every number on a condition's card is a number in its rules"
                  + (badText.Count > 0 ? " - " + string.Join("; ", badText.ToArray()) : ""));
            Check(noFloor.Count == 0, "every condition names at least one floor theme that may draw it"
                  + (noFloor.Count > 0 ? " - " + string.Join(", ", noFloor.ToArray()) : ""));
            Check(requires.Count == 0, "no line rule bans its way to a single allowed role - that would require one kind of hero");
        }

        /// <summary>Empty when every number in the card's sentence is a number the rules carry,
        /// else the first number that is not.</summary>
        private static string TextAgreesWithRules(FloorConditionDef c)
        {
            HashSet<int> allowed = new HashSet<int>();
            Action<float> pct = delegate (float x)
            {
                if (x == 1f) return;
                allowed.Add((int)Math.Round(x * 100f));
                allowed.Add((int)Math.Round((1f - x) * 100f));
                allowed.Add((int)Math.Round((x - 1f) * 100f));
            };
            BattleRules r = c.Fight;
            pct(r.HeroEnergyGain); pct(r.EnemyEnergyGain); pct(r.HeroHealing); pct(r.EnemyHealing);
            pct(r.HeroMaxHp); pct(r.EnemyMaxHp);
            allowed.Add(r.HeroStartEnergy); allowed.Add(r.EnemyStartEnergy); allowed.Add(r.EnemiesActFirstRounds);
            foreach (RuleEffect e in r.Effects)
            {
                if (e.Effect == null) continue;
                allowed.Add((int)Math.Round(e.Effect.Magnitude * 100f));
                allowed.Add((int)Math.Round(e.Effect.SelfHpRatio * 100f));
                allowed.Add(e.Effect.Duration);
            }
            allowed.Add(c.Line.MaxHeroes); allowed.Add(c.Line.MaxPerRole); allowed.Add(c.Line.MaxStars);
            if (c.Line.MaxStars > 0) allowed.Add(c.Line.MaxStars + 1);
            foreach (Match m in Regex.Matches(c.Text ?? "", @"\d+"))
            {
                int n = int.Parse(m.Value, CultureInfo.InvariantCulture);
                if (!allowed.Contains(n)) return "says " + n;
            }
            return "";
        }

        /// <summary>
        /// C1 AND C2. One condition per floor, drawn from the same (guild, cycle) seed as the floors:
        /// the same seed draws the same five every time, a different guild or cycle draws others,
        /// every theme has a condition it may draw, and drawing them moves no enemy on any floor.
        /// </summary>
        private static void CheckConditionDraw(ContentDatabase content, string contentDir)
        {
            int floors = content.Balance.GuildDungeonFloors;
            List<string> emptyThemes = new List<string>();
            foreach (GuildDungeonTheme theme in AllThemes)
                if (!content.FloorConditions.Exists(c => c.AllowsTheme(theme))) emptyThemes.Add(theme.ToString());
            Check(emptyThemes.Count == 0, "every floor theme has at least one condition it may draw"
                  + (emptyThemes.Count > 0 ? " - none for " + string.Join(", ", emptyThemes.ToArray()) : ""));

            bool same = true, full = true, noRepeat = true, differs = false;
            int cycles = 200, fiveFamilies = 0;
            Dictionary<string, int> drawnCount = new Dictionary<string, int>();
            string previous = null;
            for (int s = 0; s < cycles; s++)
            {
                int seed = GuildDungeon.SeedFor("draw-check", s);
                FloorConditionDef[] a = GuildDungeonGenerator.ConditionsFor(content, seed);
                FloorConditionDef[] b = GuildDungeonGenerator.ConditionsFor(content, seed);
                GuildDungeonTheme[] themes = GuildDungeonGenerator.ThemesFor(floors, seed);
                HashSet<string> ids = new HashSet<string>();
                HashSet<FloorConditionFamily> fams = new HashSet<FloorConditionFamily>();
                for (int f = 0; f < floors; f++)
                {
                    if (a[f] == null) { full = false; continue; }
                    if (b[f] == null || a[f].Id != b[f].Id) same = false;
                    if (!a[f].AllowsTheme(themes[f])) full = false;
                    if (!ids.Add(a[f].Id)) noRepeat = false;
                    fams.Add(a[f].Family);
                    int n; drawnCount.TryGetValue(a[f].Id, out n); drawnCount[a[f].Id] = n + 1;
                    if (GuildDungeonGenerator.ConditionFor(content, seed, f + 1) != a[f]) same = false;
                }
                if (fams.Count == floors) fiveFamilies++;
                string print = string.Join(",", Array.ConvertAll(a, c => c == null ? "-" : c.Id));
                if (previous != null && print != previous) differs = true;
                previous = print;
            }
            Check(same, "the same guild and cycle draw the same condition on every floor, every time (" + cycles + " cycles)");
            Check(full, "every floor of every cycle carries exactly one condition, and one its theme may draw");
            Check(noRepeat, "no cycle hands two floors the same condition");
            Check(differs, "different guilds and cycles draw different conditions");
            Line("    " + fiveFamilies + " of " + cycles + " cycles draw five different families; draws per condition: "
                 + string.Join(" ", new List<string>(Keys(drawnCount)).ToArray()));
            List<string> neverDrawn = content.FloorConditions.FindAll(c => !drawnCount.ContainsKey(c.Id)).ConvertAll(c => c.Id);
            Check(neverDrawn.Count == 0, "every condition in the pool is drawn somewhere in " + cycles + " cycles"
                  + (neverDrawn.Count > 0 ? " - never: " + string.Join(", ", neverDrawn.ToArray()) : ""));

            ContentDatabase bare = LoadWith(contentDir, ReadContent(contentDir, "balance"), null);
            bool unmoved = bare != null && bare.FloorConditions.Count == 0;
            for (int s = 0; s < 20 && unmoved; s++)
            {
                int seed = GuildDungeon.SeedFor("unmoved-check", s);
                for (int f = 1; f <= floors; f++)
                    if (Describe(GuildDungeonGenerator.BuildFloor(content, seed, f)) != Describe(GuildDungeonGenerator.BuildFloor(bare, seed, f)))
                        unmoved = false;
            }
            Check(unmoved, "drawing conditions moves no enemy on any floor - the floors are built exactly as they were without them");
            bool noneWithout = true;
            for (int s = 0; s < 20 && bare != null; s++)
                foreach (FloorConditionDef c in GuildDungeonGenerator.ConditionsFor(bare, GuildDungeon.SeedFor("unmoved-check", s)))
                    if (c != null) noneWithout = false;
            Check(noneWithout, "and content with no conditions file draws none, so every floor plays as it did before them");
        }

        private static IEnumerable<string> Keys(Dictionary<string, int> counts)
        {
            List<string> keys = new List<string>(counts.Keys);
            keys.Sort(string.CompareOrdinal);
            foreach (string k in keys) yield return k + ":" + counts[k];
        }

        /// <summary>
        /// C4 AND THE ENGINE. The conditions file is content: editing one number in it is a new
        /// content version, and a replay recorded before the edit is refused, exactly as for a
        /// balance patch. A fight under a condition is a different fight from the same line without
        /// one, and a BattleRules left at its defaults is the same fight as none at all.
        /// </summary>
        private static void CheckConditionFight(ContentDatabase content, string contentDir)
        {
            string file = ReadContent(contentDir, "floor-conditions") ?? "";
            string balance = ReadContent(contentDir, "balance");
            string edited = Regex.Replace(file, "\"duration\": (\\d+)", m => "\"duration\": " + (int.Parse(m.Groups[1].Value) + 1), RegexOptions.None);
            if (edited == file) edited = file.Replace("}\n  ]", "}\n  ] ").Replace("}\r\n  ]", "}\r\n  ] ");
            ContentDatabase moved = LoadWith(contentDir, balance, edited);
            ContentDatabase without = LoadWith(contentDir, balance, null);
            Check(moved.ContentVersion != content.ContentVersion && without.ContentVersion != content.ContentVersion,
                  "the conditions file is inside the content version - one edited number, or no file, is a different version");

            int seed = GuildDungeon.SeedFor("condition-replay-check", 3);
            BattleResult live;
            GuildDungeonAttempt record = GuildDungeon.Attempt(content, Conform(content, SampleRosters(content, 0xC0DE)[2],
                GuildDungeon.ConditionOf(content, seed, 2), new DeterministicRng(1)).Team, seed, 3, 2, out live);
            GuildDungeonAttempt stored = ThroughTheWire(record, "u-cond", 1, 11);
            Check(GuildDungeon.Replay(content, stored).Playable,
                  "a record fought under this cycle's condition replays exactly under the same content");
            GuildDungeonReplay underOther = GuildDungeon.Replay(moved, stored);
            Check(underOther.Verdict == GuildDungeonReplayVerdict.ContentMoved && underOther.Result == null,
                  "and is REFUSED under a different condition set, with a reason and no fight: \"" + underOther.Explanation + "\"");
            Check(GuildDungeon.Replay(without, stored).Verdict == GuildDungeonReplayVerdict.ContentMoved,
                  "and under a build with no conditions at all");

            List<string> inert = new List<string>();
            TeamSnapshot line = SampleRosters(content, 0xF1E1D)[0].Team;
            foreach (FloorConditionDef c in content.FloorConditions)
            {
                if (c.Fight.Effects.Count == 0 && IsDefault(c.Fight)) continue;
                bool changed = false;
                for (int s = 0; s < 6 && !changed; s++)
                {
                    int sd = GuildDungeon.SeedFor("condition-bites", s);
                    for (int f = 1; f <= content.Balance.GuildDungeonFloors && !changed; f++)
                        if (Fingerprint(GuildDungeon.FightUnder(content, line, sd, f, c)) != Fingerprint(GuildDungeon.FightUnder(content, line, sd, f, null)))
                            changed = true;
                }
                if (!changed) inert.Add(c.Id);
            }
            Check(inert.Count == 0, "every condition with fight rules changes the fight it is applied to"
                  + (inert.Count > 0 ? " - inert: " + string.Join(", ", inert.ToArray()) : ""));

            FloorConditionDef defaults = new FloorConditionDef();
            bool noop = true;
            for (int f = 1; f <= content.Balance.GuildDungeonFloors; f++)
                if (Fingerprint(GuildDungeon.FightUnder(content, line, seed, f, defaults)) != Fingerprint(GuildDungeon.FightUnder(content, line, seed, f, null)))
                    noop = false;
            Check(noop, "a BattleRules left at its defaults fights exactly the fight no rules do - null is not the only no-op");
        }

        private static bool IsDefault(BattleRules r)
        {
            return r.HeroEnergyGain == 1f && r.EnemyEnergyGain == 1f && r.HeroStartEnergy == 0 && r.EnemyStartEnergy == 0
                && r.HeroHealing == 1f && r.EnemyHealing == 1f && r.HeroMaxHp == 1f && r.EnemyMaxHp == 1f
                && r.EnemiesActFirstRounds == 0;
        }

        /// <summary>
        /// C7, AS BUILT: a board a condition hands to every legal line is suspended on that floor.
        /// Asserted from both ends - Grants must be true exactly when every legal sample line
        /// satisfies the board's rule, and a clear on a floor whose condition grants a board enters
        /// every other board it earns and not that one, with the reason on the card.
        /// </summary>
        private static void CheckConditionBoards(ContentDatabase content)
        {
            GuildDungeonBoard[] lineBoards = { GuildDungeonBoard.FourOrFewer, GuildDungeonBoard.NoFiveStar, GuildDungeonBoard.NoHealer };
            List<Sample> samples = SampleRosters(content, 0xB0A2D);
            List<string> wrong = new List<string>(), suspends = new List<string>();
            foreach (FloorConditionDef c in content.FloorConditions)
            {
                List<Sample> legal = ConformAll(content, samples, c, 0x51);
                foreach (GuildDungeonBoard b in lineBoards)
                {
                    bool all = true;
                    foreach (Sample s in legal)
                    {
                        GuildDungeonAttempt probe = new GuildDungeonAttempt { Victory = true, Team = s.Team, Attempt = 2, Fallen = 1 };
                        // Asked of the rule alone: a seed with no condition on this floor.
                        if (!Satisfies(content, probe, b)) all = false;
                    }
                    if (all != GuildDungeon.Grants(c, b)) wrong.Add(c.Id + "/" + b);
                    if (GuildDungeon.Grants(c, b)) suspends.Add(c.Id + " suspends " + GuildDungeon.BoardTitle(b));
                }
            }
            Check(wrong.Count == 0, "a condition suspends exactly the boards every legal line would enter anyway"
                  + (wrong.Count > 0 ? " - disagrees: " + string.Join(", ", wrong.ToArray()) : " (" + (suspends.Count == 0 ? "none in the pool" : string.Join("; ", suspends.ToArray())) + ")"));

            FloorConditionDef granting = content.FloorConditions.Find(c => GuildDungeon.Grants(c, GuildDungeonBoard.FourOrFewer)
                                                                           || GuildDungeon.Grants(c, GuildDungeonBoard.NoHealer)
                                                                           || GuildDungeon.Grants(c, GuildDungeonBoard.NoFiveStar));
            if (granting == null) { Line("    (no condition in the pool grants a board, so the suspension is not exercised)"); return; }
            int seed = -1, floor = -1;
            for (int s = 0; s < 4000 && seed < 0; s++)
            {
                int sd = GuildDungeon.SeedFor("suspend-check", s);
                FloorConditionDef[] drawn = GuildDungeonGenerator.ConditionsFor(content, sd);
                for (int f = 0; f < drawn.Length; f++) if (drawn[f] == granting) { seed = sd; floor = f + 1; break; }
            }
            Check(seed >= 0, "a cycle that draws " + granting.Id + " exists to test the suspension on");
            if (seed < 0) return;

            Sample legalLine = Conform(content, samples[0], granting, new DeterministicRng(3));
            GuildDungeonAttempt clear = new GuildDungeonAttempt { Victory = true, Team = legalLine.Team, Attempt = 1, Fallen = 0, Seed = seed, Floor = floor, Cycle = 1 };
            List<string> met = GuildDungeon.BoardsMet(content, clear).ConvertAll(b => b.ToString());
            bool suspended = true, others = met.Contains("FirstAttempt") && met.Contains("Flawless");
            string said = "";
            foreach (GuildDungeonBoard b in lineBoards)
            {
                if (!GuildDungeon.Grants(granting, b)) continue;
                if (met.Contains(b.ToString()) || !GuildDungeon.Suspended(content, seed, floor, b)) suspended = false;
                said = GuildDungeon.Unmet(content, clear, b);
                if (!said.StartsWith("suspended")) suspended = false;
            }
            Check(suspended && others, "on a floor drawing " + granting.Name + ", a clear enters FIRST ATTEMPT and NOBODY FELL but not the board it grants: \""
                  + said + "\" [" + string.Join(",", met.ToArray()) + "]");

            // And a line that breaks the rule is on no board at all.
            if (!granting.Line.IsEmpty)
            {
                // Five heroes, a Mender, a 5-star and two sharing a role: it breaks every kind of line
                // rule the file can write, so whichever one the granting condition carries, it bites.
                List<HeroDef> worst = new List<HeroDef>();
                worst.Add(content.Heroes.Find(h => h.Role == HeroRole.Mender));
                worst.Add(content.Heroes.Find(h => h.Stars >= 5 && !worst.Contains(h)));
                HeroDef pairA = content.Heroes.Find(h => !worst.Contains(h) && h.Role == HeroRole.Striker);
                worst.Add(pairA);
                worst.Add(content.Heroes.Find(h => !worst.Contains(h) && h.Role == HeroRole.Striker));
                worst.Add(content.Heroes.Find(h => !worst.Contains(h)));
                TeamSnapshot broke = Line(worst);
                string why;
                bool legal = GuildDungeon.LineLegal(content, granting, broke, out why);
                GuildDungeonAttempt cheat = new GuildDungeonAttempt { Victory = true, Team = broke, Attempt = 1, Fallen = 0, Seed = seed, Floor = floor, Cycle = 1 };
                Check(!legal && why.Length > 0 && GuildDungeon.BoardsMet(content, cheat).Count == 0,
                      "a line that breaks the floor's condition is refused with a reason (\"" + why + "\") and a record of one enters no board");
            }
        }

        /// <summary>Board membership asked of the line alone, on a floor with no condition.</summary>
        private static bool Satisfies(ContentDatabase content, GuildDungeonAttempt a, GuildDungeonBoard b)
        {
            a.Seed = NeutralSeed(content, a.Floor);
            return GuildDungeon.Satisfies(content, a, b);
        }

        private static int NeutralSeed(ContentDatabase content, int floor)
        {
            for (int s = 0; s < 10000; s++)
            {
                int seed = GuildDungeon.SeedFor("neutral", s);
                FloorConditionDef c = GuildDungeon.ConditionOf(content, seed, floor);
                if (c == null || c.Line.IsEmpty) return seed;
            }
            return 0;
        }

        /// <summary>
        /// C6 WITH CONDITIONS APPLIED, FOR EVERY CONDITION ON EVERY FLOOR IT MAY BE DRAWN ON - not
        /// only the ones the six sample guilds below happen to draw. Each (condition, theme) pair is
        /// fought on two floors of that theme against the gate's spread, made legal under the
        /// condition the way a player would, and all three guardrails must hold on each.
        /// </summary>
        private static void CheckConditionGuardrails(ContentDatabase content)
        {
            List<Sample> samples = SampleRosters(content, 0x6D1C);
            int floors = content.Balance.GuildDungeonFloors;
            Dictionary<GuildDungeonTheme, List<int[]>> where = new Dictionary<GuildDungeonTheme, List<int[]>>();
            for (int s = 0; s < 40; s++)
            {
                int seed = GuildDungeon.SeedFor("guard-check", s);
                GuildDungeonTheme[] themes = GuildDungeonGenerator.ThemesFor(floors, seed);
                for (int f = 1; f <= floors; f++)
                {
                    List<int[]> list;
                    if (!where.TryGetValue(themes[f - 1], out list)) where[themes[f - 1]] = list = new List<int[]>();
                    if (list.Count < 2) list.Add(new[] { seed, f });
                }
            }
            int pairs = 0, held = 0;
            List<string> failures = new List<string>();
            foreach (FloorConditionDef c in content.FloorConditions)
            {
                foreach (GuildDungeonTheme theme in c.Themes)
                {
                    List<int[]> at;
                    if (!where.TryGetValue(theme, out at)) continue;
                    foreach (int[] sf in at)
                    {
                        pairs++;
                        string guard = Guardrails(FightAdapted(content, samples, sf[0], sf[1], c, 0x6D1C));
                        if (guard.Length == 0) held++;
                        else Note(failures, c.Id + " on " + theme + ": " + guard);
                    }
                }
            }
            for (int i = 0; i < failures.Count; i++) Line("    ! " + failures[i]);
            Check(held == pairs, "every condition, on every floor theme it may be drawn on, keeps all three guardrails ("
                  + held + " of " + pairs + " floors): 40% of rosters clear, 25% of every group missing an obvious answer, no hero in over 35% of clears");
        }

        // ---------------------------------------------------------------- seeds

        private static void CheckSeeds()
        {
            const string guild = "3f2a9c1e-7b4d-4e8a-9c21-5d6e7f8a9b0c";
            int a = GuildDungeon.SeedFor(guild, 12);
            Check(a == GuildDungeon.SeedFor(guild, 12), "a guild's dungeon seed is a pure function of (guild id, cycle)");
            Check(a == GuildDungeon.SeedFor(" " + guild.ToUpperInvariant() + " ", 12),
                  "and ignores case and surrounding space in the id, so every client derives the same one");
            Check(a != GuildDungeon.SeedFor(guild, 13), "a new cycle is a new dungeon");
            Check(a != GuildDungeon.SeedFor("3f2a9c1e-7b4d-4e8a-9c21-5d6e7f8a9b0d", 12),
                  "a different guild in the same cycle gets a different dungeon");
            Check(a >= 0, "the seed is non-negative, so it fits the server's integer column as sent");
            // PINNED. Every member of a live guild must derive this number the same way on every
            // build. Changing the hash rotates every guild's dungeon mid-cycle under their
            // recorded attempts, and each of those records would stop replaying.
            Check(a == PinnedSeed, "the derivation still produces the pinned value (" + a + ") - changing it breaks every live cycle's replays");
        }

        private const int PinnedSeed = 417005212;

        // ---------------------------------------------------------------- floors

        private static void CheckFloors(ContentDatabase content)
        {
            BalanceConfig cfg = content.Balance;
            int floors = cfg.GuildDungeonFloors;
            int seed = GuildDungeon.SeedFor("floors-check", 1);

            Check(floors == 5, "a cycle is five floors (" + floors + ")");
            Check(cfg.GuildDungeonAttemptsPerFloor == 3, "three attempts per floor per cycle, read from balance.json (" + cfg.GuildDungeonAttemptsPerFloor + ")");

            bool lastIsChampion = true, rising = true, sameTwice = true;
            HashSet<GuildDungeonTheme> early = new HashSet<GuildDungeonTheme>();
            GuildDungeonTheme[] themes = GuildDungeonGenerator.ThemesFor(floors, seed);
            int previous = 0;
            for (int f = 1; f <= floors; f++)
            {
                StageDef a = GuildDungeonGenerator.BuildFloor(content, seed, f);
                StageDef b = GuildDungeonGenerator.BuildFloor(content, seed, f);
                if (Describe(a) != Describe(b)) sameTwice = false;
                int power = GuildDungeonGenerator.TargetPowerForFloor(cfg, f);
                if (power <= previous) rising = false;
                previous = power;
                if (f < floors) early.Add(themes[f - 1]);
                Line("    floor " + f + "  " + themes[f - 1].ToString().PadRight(10) + Describe(a));
            }
            lastIsChampion = themes[floors - 1] == GuildDungeonTheme.Champion;

            Check(sameTwice, "building a floor twice from the same seed builds the same floor");
            Check(lastIsChampion, "the last floor is the Champion floor");
            Check(early.Count == Math.Min(4, floors - 1), "the floors before it are four different themes");
            Check(rising, "the power target climbs every floor");

            // COMPOSITION IS A PROBABILITY, SO IT IS ASSERTED OVER MANY DUNGEONS, NOT ONE. Nothing in
            // the generator forbids a single-Aspect line: Fill avoids repeating a template, not an
            // Aspect, so a mono floor is a draw that happens at some rate. Built from the seed above
            // alone this check once passed content that made five Gale bodies on 2 floors in 1,500,
            // because neither was in its five. CompositionCycles is sized so a failure that rare is
            // still caught: see the constant.
            int built = 0, shortLines = 0, missedTarget = 0, monoAspect = 0, scripted = 0;
            string firstBad = "";
            // Every floor any guild can reach, not only the base five: a deep floor is built by the
            // same code from another block's stream, and it is held to the same four rules.
            int ladder = GuildDungeonScale.MaxLadder(content);
            for (int cycle = 1; cycle <= CompositionCycles; cycle++)
            {
                int s = GuildDungeon.SeedFor("floors-check", cycle);
                for (int f = 1; f <= ladder; f++)
                {
                    StageDef a = GuildDungeonGenerator.BuildFloor(content, s, f);
                    built++;
                    bool bad = false;
                    if (a.Enemies.Count != cfg.MaxTeamSize) { shortLines++; bad = true; }
                    if (EnemyPower(content, a) < GuildDungeonGenerator.TargetPowerForFloor(cfg, f)) { missedTarget++; bad = true; }
                    HashSet<Aspect> aspects = new HashSet<Aspect>();
                    bool hasScripted = false;
                    for (int i = 0; i < a.Enemies.Count; i++)
                    {
                        EnemyDef def = content.GetEnemy(a.Enemies[i].EnemyId);
                        if (def != null) aspects.Add(def.Aspect);
                        if (a.Enemies[i].EnemyId == "the_unmade") hasScripted = true;
                    }
                    if (aspects.Count < 2) { monoAspect++; bad = true; }
                    if (hasScripted) { scripted++; bad = true; }
                    if (bad && firstBad == "") firstBad = " (first: cycle " + cycle + " floor " + f + ", " + Describe(a) + ")";
                }
            }
            string over = " - over " + built + " floors, " + CompositionCycles + " dungeons of " + ladder + " (the longest ladder)";
            Check(shortLines == 0, "every floor fields a full line of " + cfg.MaxTeamSize + over
                  + (shortLines == 0 ? "" : " (" + shortLines + " short)" + firstBad));
            Check(missedTarget == 0, "and every floor's line reaches its target - the level is solved for it, not guessed" + over
                  + (missedTarget == 0 ? "" : " (" + missedTarget + " under)" + firstBad));
            Check(monoAspect == 0, "no floor is drawn from a single Aspect - an Aspect floor is a counter-pick check" + over
                  + (monoAspect == 0 ? "" : " (" + monoAspect + " single-Aspect)" + firstBad));
            Check(scripted == 0, "the prologue's scripted boss never appears - only templates a campaign stage fields are drawn" + over
                  + (scripted == 0 ? "" : " (" + scripted + " floors)" + firstBad));

            bool differs = false;
            int other = GuildDungeon.SeedFor("floors-check", 2);
            for (int f = 1; f <= floors; f++)
                if (Describe(GuildDungeonGenerator.BuildFloor(content, seed, f)) != Describe(GuildDungeonGenerator.BuildFloor(content, other, f)))
                    differs = true;
            Check(differs, "a different seed builds a different dungeon");
        }

        private static int EnemyPower(ContentDatabase content, StageDef stage)
        {
            int total = 0;
            for (int i = 0; i < stage.Enemies.Count; i++)
            {
                EnemyDef def = content.GetEnemy(stage.Enemies[i].EnemyId);
                if (def != null) total += StatMath.PowerScore(StatMath.ForEnemy(def, stage.Enemies[i].Level));
            }
            return total;
        }

        private static string Describe(StageDef stage)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < stage.Enemies.Count; i++) parts.Add(stage.Enemies[i].EnemyId + "@" + stage.Enemies[i].Level);
            return string.Join(", ", parts.ToArray());
        }

        // ---------------------------------------------------------------- one level for everybody

        /// <summary>
        /// THE DUNGEON IS A PUZZLE, NOT A POWER CHECK. Every hero fights it at one level with no
        /// relic and no armor, and keeps its talent. Asserted three ways: the same heroes at level
        /// 5 and at level 120, geared and bare, fight the SAME battle; the normalised line carries
        /// the talent pick; and a talent pick still changes the fight for some line, because a
        /// normalisation that quietly dropped talents would pass the first two.
        /// </summary>
        private static void CheckLevelNormalisation(ContentDatabase content)
        {
            int seed = GuildDungeon.SeedFor("level-check", 3);
            TeamSnapshot bare = SampleRosters(content, 0x1E7E)[0].Team;

            TeamSnapshot low = CopyLine(bare, 5);
            TeamSnapshot high = CopyLine(bare, 120);
            // Gear on the high line: a relic on every unit and a rolled piece on the first.
            string relic = content.Relics.Count > 0 ? content.Relics[0].Id : "";
            for (int i = 0; i < high.Units.Count; i++) high.Units[i].RelicId = relic;
            if (content.Armor.IsUsable && high.Units.Count > 0)
            {
                HeroDef first = content.GetHero(high.Units[0].DefId);
                high.Units[0].Armor.Add(ArmorRoller.Roll(content.Armor, ArmorSlot.Weapon, ArmorTemper.Radiant,
                                                         first != null ? first.Aspect : Aspect.Ember, 77));
            }

            string lowPrint = Fingerprint(GuildDungeon.Fight(content, low, seed, 2));
            string highPrint = Fingerprint(GuildDungeon.Fight(content, high, seed, 2));
            Check(lowPrint == highPrint,
                  "a level-5 line and the same heroes at level 120 with relics and armor fight the same battle - one level for everybody");

            TeamSnapshot normal = GuildDungeon.NormaliseTeam(content, high);
            bool allAtLevel = true, noGear = true;
            for (int i = 0; i < normal.Units.Count; i++)
            {
                if (normal.Units[i].Level != content.Balance.GuildDungeonHeroLevel) allAtLevel = false;
                if (!string.IsNullOrEmpty(normal.Units[i].RelicId) || normal.Units[i].Armor.Count > 0) noGear = false;
            }
            Check(allAtLevel && noGear && normal.Units.Count == bare.Units.Count,
                  "the recorded line is the line that fought: every unit at level " + content.Balance.GuildDungeonHeroLevel + ", no relic, no armor");
            Check(Describe(GuildDungeon.NormaliseTeam(content, normal)) == Describe(normal),
                  "normalising is idempotent, so a stored record can be normalised again on replay without changing");

            TeamSnapshot dup = CopyLine(bare, 40);
            dup.Units.Add(CopyLine(bare, 40).Units[0]);
            dup.Units.Add(new SnapshotUnit { DefId = "no_such_hero", Level = 40, Stars = 5 });
            Check(GuildDungeon.NormaliseTeam(content, dup).Units.Count == bare.Units.Count,
                  "a repeated hero and a hero this build does not know are dropped, not fought");

            // Talents stay. Five-star heroes with talents, every pick tried on one line.
            List<HeroDef> talented = content.Heroes.FindAll(h => h.TalentAt(1) != null);
            bool kept = true, changes = false;
            int lines = 0;
            for (int start = 0; start + 5 <= talented.Count && lines < 6 && !changes; start += 5, lines++)
            {
                TeamSnapshot none = new TeamSnapshot();
                for (int i = 0; i < 5; i++) none.Units.Add(new SnapshotUnit { DefId = talented[start + i].Id, Level = 40 });
                string basePrint = Fingerprint(GuildDungeon.Fight(content, none, seed, 1));
                for (int pick = 1; pick <= HeroDef.MaxTalents && !changes; pick++)
                {
                    TeamSnapshot with = CopyLine(none, 40);
                    for (int i = 0; i < with.Units.Count; i++)
                        if (talented[start + i].TalentAt(pick) != null) with.Units[i].SelectedTalent = pick;
                    TeamSnapshot n = GuildDungeon.NormaliseTeam(content, with);
                    for (int i = 0; i < n.Units.Count; i++)
                        if (n.Units[i].SelectedTalent != with.Units[i].SelectedTalent) kept = false;
                    if (Fingerprint(GuildDungeon.Fight(content, with, seed, 1)) != basePrint) changes = true;
                }
            }
            Check(talented.Count >= 5, "there are talented heroes to test with (" + talented.Count + ")");
            Check(kept, "the talent pick survives normalisation - it is free to change, so it is a real choice");
            Check(changes, "and a talent pick changes the fight for at least one line - normalisation did not quietly drop it");
        }

        private static TeamSnapshot CopyLine(TeamSnapshot line, int level)
        {
            TeamSnapshot copy = new TeamSnapshot();
            copy.Label = line.Label;
            for (int i = 0; i < line.Units.Count; i++)
            {
                SnapshotUnit u = line.Units[i];
                copy.Units.Add(new SnapshotUnit { DefId = u.DefId, Level = level, Stars = u.Stars, SelectedTalent = u.SelectedTalent });
            }
            return copy;
        }

        private static string Describe(TeamSnapshot line)
        {
            return TeamSnapshotCodec.WriteUnits(line).ToJson(false);
        }

        // ---------------------------------------------------------------- the record

        private static void CheckRecordReplays(ContentDatabase content)
        {
            int seed = GuildDungeon.SeedFor("replay-check", 4);
            List<Sample> rosters = SampleRosters(content, 0x5EED);
            TeamSnapshot team = rosters[0].Team;

            BattleResult live;
            GuildDungeonAttempt record = GuildDungeon.Attempt(content, team, seed, 4, 2, out live);
            BattleResult again = GuildDungeon.Fight(content, team, seed, 2);
            Check(Fingerprint(live) == Fingerprint(again),
                  "the same line on the same floor fights the same battle every time - an attempt is a decision, not a reroll");
            Check(record.Turns == live.Rounds && record.Victory == live.Victory,
                  "the record carries the fight's own result and turn count");
            Check(record.Fallen == GuildDungeon.FallenIn(live) && record.Fallen >= 0,
                  "and how many heroes fell (" + record.Fallen + "), read off the event list");
            Check(record.ContentVersion == content.ContentVersion && record.ContentVersion.StartsWith("c1-"),
                  "and the content it was fought under (" + record.ContentVersion + ")");
            Check(Math.Abs(record.AverageStars - GuildDungeon.AverageStars(record.Team)) < 0.001f && record.AverageStars >= 1f,
                  "and its average star rating (" + record.AverageStars.ToString("0.00", CultureInfo.InvariantCulture) + ")");

            GuildDungeonAttempt stored = ThroughTheWire(record, "0b1c2d3e-4f50-4617-8293-a4b5c6d7e8f9", 1, 501);
            Check(stored.Seed == record.Seed && stored.Floor == record.Floor && stored.Cycle == record.Cycle
                  && stored.Team.Units.Count == record.Team.Units.Count && stored.Fallen == record.Fallen
                  && stored.ContentVersion == record.ContentVersion,
                  "a record survives the trip to the server's row shape and back, content version and losses included");
            GuildDungeonReplay replay = GuildDungeon.Replay(content, stored);
            Check(replay.Playable && Fingerprint(replay.Result) == Fingerprint(live),
                  "AND REPLAYS EXACTLY - the stored record alone reproduces the fight, event for event ("
                  + live.Events.Count + " events)");
        }

        /// <summary>
        /// A record through the wire: guild_dungeon_submit's arguments, reshaped into the row the
        /// server returns (with the id, attempt number and member it assigns), serialised, parsed
        /// and read back. The same trip a real attempt makes.
        /// </summary>
        private static GuildDungeonAttempt ThroughTheWire(GuildDungeonAttempt record, string userId, int attempt, long id)
        {
            JsonValue args = JsonValue.Parse(GuildDungeon.WriteRpcArgs(record).ToJson(false));
            JsonValue row = RowFrom(args, "3f2a9c1e-7b4d-4e8a-9c21-5d6e7f8a9b0c", userId, attempt, id);
            return GuildDungeon.ReadRow(JsonValue.Parse(row.ToJson(false)));
        }

        private static JsonValue RowFrom(JsonValue args, string guildId, string userId, int attempt, long id)
        {
            JsonValue row = JsonValue.NewObject();
            row.Set("id", id);
            row.Set("guild_id", guildId);
            row.Set("user_id", userId);
            row.Set("cycle", args.GetInt("p_cycle", -1));
            row.Set("floor", args.GetInt("p_floor", -1));
            row.Set("attempt", attempt);
            row.Set("seed", args.GetInt("p_seed", -1));
            row.Set("team", args["p_team"]);
            row.Set("victory", args.GetBool("p_victory", false));
            row.Set("turns", args.GetInt("p_turns", -1));
            row.Set("power", args.GetInt("p_power", -1));
            row.Set("avg_stars", args.GetFloat("p_avg_stars", -1f));
            row.Set("fallen", args.GetInt("p_fallen", -1));
            row.Set("content_version", args.GetString("p_content_version", ""));
            row.Set("created_at", "2026-09-22T21:00:" + (id % 60).ToString("00", CultureInfo.InvariantCulture) + "+00:00");
            return row;
        }

        private static string Fingerprint(BattleResult r)
        {
            return r.Victory + "|" + r.Rounds + "|" + r.Events.Count + "|"
                 + r.PlayerHpRatio.ToString("0.0000", CultureInfo.InvariantCulture) + "|"
                 + r.EnemyHpRatio.ToString("0.0000", CultureInfo.InvariantCulture);
        }

        // ---------------------------------------------------------------- content version

        /// <summary>
        /// A REPLAY SAYS PLAINLY WHEN IT CANNOT BE REPRODUCED. The version is a function of the
        /// content text and nothing else a checkout can vary (line endings, a byte-order mark);
        /// any real edit moves it; and a record fought under other content, or under none, or
        /// that this build fights differently, is refused with a reason and no fight.
        /// </summary>
        private static void CheckContentVersion(ContentDatabase content, string contentDir)
        {
            string lf = "{\n  \"a\": 1\n}\n";
            string crlf = "{\r\n  \"a\": 1\r\n}\r\n";
            Check(ContentDatabase.VersionOf(lf) == ContentDatabase.VersionOf(crlf),
                  "the content version ignores line endings - the same file on two checkouts is the same content");
            Check(ContentDatabase.VersionOf(lf) == ContentDatabase.VersionOf("﻿" + lf),
                  "and a byte-order mark");
            Check(ContentDatabase.VersionOf(lf) != ContentDatabase.VersionOf(lf.Replace("1", "2")),
                  "and moves on any real edit");
            Check(ContentDatabase.VersionOf("ab", "c") != ContentDatabase.VersionOf("a", "bc"),
                  "and knows which file a character is in");
            Check(!string.IsNullOrEmpty(content.ContentVersion), "the shipped content has a version (" + content.ContentVersion + ")");

            // The shipped content, reloaded with one balance number moved: a balance patch.
            ContentDatabase patched = null;
            string balancePath = Path.Combine(contentDir ?? "", "balance.json");
            if (File.Exists(balancePath))
            {
                string balance = ReadContent(contentDir, "balance").Replace("\"guildDungeonChampionLevelBonus\": 3", "\"guildDungeonChampionLevelBonus\": 4");
                patched = LoadWith(contentDir, balance);
                ContentDatabase same = LoadWith(contentDir, ReadContent(contentDir, "balance"));
                Check(same.ContentVersion == content.ContentVersion,
                      "loading the same files again gives the same version - it is a function of the content, not of the load");
            }
            Check(patched != null && patched.ContentVersion != content.ContentVersion,
                  "a one-number balance patch is a new content version");

            int seed = GuildDungeon.SeedFor("version-check", 9);
            BattleResult live;
            GuildDungeonAttempt record = GuildDungeon.Attempt(content, SampleRosters(content, 0xC0DE)[1].Team, seed, 9, 5, out live);

            if (patched != null)
            {
                GuildDungeonReplay moved = GuildDungeon.Replay(patched, record);
                Check(moved.Verdict == GuildDungeonReplayVerdict.ContentMoved && moved.Result == null
                      && moved.Explanation.Length > 0,
                      "a record replayed after that patch is REFUSED as content moved, with a reason and no fight: \"" + moved.Explanation + "\"");
            }

            GuildDungeonAttempt old = ThroughTheWire(record, "u-old", 1, 7);
            old.ContentVersion = "";
            GuildDungeonReplay unknown = GuildDungeon.Replay(content, old);
            Check(unknown.Verdict == GuildDungeonReplayVerdict.ContentUnknown && unknown.Result == null,
                  "a record from before the field is refused as unknown, not played on trust");

            JsonValue legacyRow = JsonValue.Parse(GuildDungeon.WriteRpcArgs(record).ToJson(false));
            JsonValue row0007 = RowFrom(legacyRow, "g", "u-legacy", 1, 8);
            // A 0007-shaped row: neither column exists.
            JsonValue stripped = JsonValue.NewObject();
            foreach (string key in new[] { "id", "guild_id", "user_id", "cycle", "floor", "attempt", "seed", "team", "victory", "turns", "power", "avg_stars", "created_at" })
                stripped.Set(key, row0007[key]);
            GuildDungeonAttempt legacy = GuildDungeon.ReadRow(JsonValue.Parse(stripped.ToJson(false)));
            Check(legacy.ContentVersion == "" && legacy.Fallen == -1
                  && GuildDungeon.Replay(content, legacy).Verdict == GuildDungeonReplayVerdict.ContentUnknown,
                  "a row stored by 0007, with neither new column, reads as unknown content and unknown losses");

            GuildDungeonAttempt forged = ThroughTheWire(record, "u-forged", 1, 9);
            forged.Turns += 1;
            GuildDungeonReplay diverged = GuildDungeon.Replay(content, forged);
            Check(diverged.Verdict == GuildDungeonReplayVerdict.Diverged && diverged.Result == null,
                  "same content but a different fight from the record is refused too: \"" + diverged.Explanation + "\"");
            Check(GuildDungeon.Replay(content, ThroughTheWire(record, "u-true", 1, 10)).Verdict == GuildDungeonReplayVerdict.Exact,
                  "and the untouched record plays");
        }

        // ---------------------------------------------------------------- boards

        /// <summary>
        /// THE CONSTRAINT BOARDS. Lines are built to satisfy chosen rules, so every membership
        /// below is known in advance: an attempt enters exactly the boards it satisfies, nobody
        /// declares anything, a loss enters none, one entry per member, fewest turns first.
        /// </summary>
        private static void CheckBoards(ContentDatabase content)
        {
            _madeSeed = NeutralSeed(content, 2);
            Check(GuildDungeon.Boards.Length == 6
                  && string.Join(",", Array.ConvertAll(GuildDungeon.Boards, b => b.ToString()))
                     == "FourOrFewer,NoFiveStar,OneAspect,NoHealer,FirstAttempt,Flawless",
                  "six boards, in drawing order: four or fewer, no five stars, one aspect, no healer, first attempt, nobody fell");

            HeroDef mender = content.Heroes.Find(h => h.Role == HeroRole.Mender && h.Stars < 5);
            HeroDef five = content.Heroes.Find(h => h.Stars == 5 && h.Role != HeroRole.Mender);
            List<HeroDef> oneAspect = null;
            foreach (Aspect aspect in Enum.GetValues(typeof(Aspect)))
            {
                List<HeroDef> of = content.Heroes.FindAll(h => h.Aspect == aspect && h.Role != HeroRole.Mender && h.Stars < 5);
                if (of.Count >= 4) { oneAspect = of.GetRange(0, 4); break; }
            }
            List<HeroDef> mixed = new List<HeroDef>();
            foreach (HeroDef h in content.Heroes)
                if (h.Role != HeroRole.Mender && h.Stars < 5 && !mixed.Exists(m => m.Aspect == h.Aspect)) mixed.Add(h);
            if (mender == null || five == null || oneAspect == null || mixed.Count < 5)
            {
                Check(false, "the shipped roster has the heroes the board checks are built from");
                return;
            }

            // ana: five mixed, first attempt, nobody fell, 12 turns   -> NoFiveStar, NoHealer, FirstAttempt, Flawless
            // ana: four of one aspect, attempt 2, one fell, 9 turns   -> FourOrFewer, NoFiveStar, OneAspect, NoHealer
            // bo:  five-star + mender + 3 mixed, attempt 1, 8 turns   -> FirstAttempt only (one fell)
            // cy:  a LOSS in 3 turns with a four-hero line            -> nothing
            // cy:  four mixed, attempt 2, nobody fell, 10 turns       -> FourOrFewer, NoFiveStar, NoHealer, Flawless
            List<GuildDungeonAttempt> attempts = new List<GuildDungeonAttempt>
            {
                Made("ana", 1, true, 12, 0, Line(mixed.GetRange(0, 5))),
                Made("ana", 2, true, 9, 1, Line(oneAspect)),
                Made("bo", 1, true, 8, 1, Line(new List<HeroDef> { five, mender, mixed[0], mixed[1], mixed[2] })),
                Made("cy", 1, false, 3, 4, Line(mixed.GetRange(0, 4))),
                Made("cy", 2, true, 10, 0, Line(mixed.GetRange(1, 4))),
            };
            GuildDungeonAttempt wrongFloor = Made("dee", 1, true, 1, 0, Line(mixed.GetRange(0, 4))); wrongFloor.Floor = 3;
            GuildDungeonAttempt wrongCycle = Made("eli", 1, true, 1, 0, Line(mixed.GetRange(0, 4))); wrongCycle.Cycle = 6;
            attempts.Add(wrongFloor);
            attempts.Add(wrongCycle);

            Check(Met(content, attempts[0]) == "NoFiveStar,NoHealer,FirstAttempt,Flawless",
                  "a five-hero mixed line cleared first time with nobody lost enters four boards (" + Met(content, attempts[0]) + ")");
            Check(Met(content, attempts[1]) == "FourOrFewer,NoFiveStar,OneAspect,NoHealer",
                  "a four-hero single-aspect line enters its four (" + Met(content, attempts[1]) + ")");
            Check(Met(content, attempts[2]) == "FirstAttempt",
                  "a line with a five-star and a Mender that lost a hero enters only FIRST ATTEMPT (" + Met(content, attempts[2]) + ")");
            Check(Met(content, attempts[3]) == "", "a loss enters no board at all");

            Check(BoardOrder(content, attempts, GuildDungeonBoard.FourOrFewer) == "ana:2 cy:2", "FOUR OR FEWER: " + BoardOrder(content, attempts, GuildDungeonBoard.FourOrFewer));
            Check(BoardOrder(content, attempts, GuildDungeonBoard.NoFiveStar) == "ana:2 cy:2", "NO FIVE STARS keeps each member's fastest qualifying clear: " + BoardOrder(content, attempts, GuildDungeonBoard.NoFiveStar));
            Check(BoardOrder(content, attempts, GuildDungeonBoard.OneAspect) == "ana:2", "ONE ASPECT: " + BoardOrder(content, attempts, GuildDungeonBoard.OneAspect));
            Check(BoardOrder(content, attempts, GuildDungeonBoard.NoHealer) == "ana:2 cy:2", "NO HEALER: " + BoardOrder(content, attempts, GuildDungeonBoard.NoHealer));
            Check(BoardOrder(content, attempts, GuildDungeonBoard.FirstAttempt) == "bo:1 ana:1", "FIRST ATTEMPT, fewest turns first: " + BoardOrder(content, attempts, GuildDungeonBoard.FirstAttempt));
            Check(BoardOrder(content, attempts, GuildDungeonBoard.Flawless) == "cy:2 ana:1", "NOBODY FELL: " + BoardOrder(content, attempts, GuildDungeonBoard.Flawless));
            Check(!BoardOrder(content, attempts, GuildDungeonBoard.FourOrFewer).Contains("dee") && !BoardOrder(content, attempts, GuildDungeonBoard.FourOrFewer).Contains("eli"),
                  "other floors and other cycles are never counted");
            Check(!BoardOrder(content, attempts, GuildDungeonBoard.FirstAttempt).Contains("cy:1"),
                  "cy's 3-turn wipe is not a 3-turn clear, even on a board it would otherwise lead");

            // The same member leading one board with one attempt and sitting on another with a different one.
            List<GuildDungeonStanding> anaTwo = GuildDungeon.StandingsOf(content, attempts, attempts[1]);
            List<GuildDungeonStanding> anaOne = GuildDungeon.StandingsOf(content, attempts, attempts[0]);
            GuildDungeonStanding one = anaOne.Find(s => s.Board == GuildDungeonBoard.NoFiveStar);
            GuildDungeonStanding flawless = anaOne.Find(s => s.Board == GuildDungeonBoard.Flawless);
            Check(anaTwo.Count == 4 && anaTwo.TrueForAll(s => s.Rank == 1 && s.ThisAttempt),
                  "ana's second attempt holds first place on all four of its boards");
            Check(one != null && one.Rank == 1 && !one.ThisAttempt && flawless != null && flawless.Rank == 2 && flawless.ThisAttempt && flawless.Of == 2,
                  "and her first attempt is told its NO FIVE STARS place is held by her later one, and that it is 2nd of 2 on NOBODY FELL");

            Check(GuildDungeon.AttemptsUsed(attempts, "cy", 7, 2) == 2 && GuildDungeon.AttemptsUsed(attempts, "dee", 7, 2) == 0,
                  "attempts used are counted per member, floor and cycle");
            List<GuildDungeonAttempt> climb = new List<GuildDungeonAttempt>(attempts);
            Check(GuildDungeon.FloorOpen(climb, 7, 1) && GuildDungeon.FloorOpen(climb, 7, 4) && !GuildDungeon.FloorOpen(climb, 7, 5),
                  "floor N opens when anybody in the guild clears floor N-1 - dee's floor 3 clear opens floor 4 for everyone, and nothing opens 5");
            Check(GuildDungeon.HighestCleared(climb, null, 7) == 3 && GuildDungeon.HighestCleared(climb, "bo", 7) == 2
                  && GuildDungeon.HighestCleared(climb, "eli", 7) == 0,
                  "how far the guild has pushed and how far one member has are separate numbers");
        }

        private static long _madeId;

        /// <summary>A dungeon seed whose floor 2 carries no line rule, so the board checks above read
        /// the boards' own rules and not a condition's. The conditions' part is CheckConditionBoards'.</summary>
        private static int _madeSeed;

        private static string BoardOrder(ContentDatabase content, List<GuildDungeonAttempt> attempts, GuildDungeonBoard b)
        {
            return Order(GuildDungeon.Rank(content, attempts, 7, 2, b));
        }

        private static string ReadContent(string dir, string name)
        {
            string p = Path.Combine(dir, name + ".json");
            return File.Exists(p) ? File.ReadAllText(p) : null;
        }

        private static ContentDatabase LoadWith(string dir, string balance)
        {
            return LoadWith(dir, balance, ReadContent(dir, "floor-conditions"));
        }

        private static ContentDatabase LoadWith(string dir, string balance, string floorConditions)
        {
            return ContentDatabase.Load(ReadContent(dir, "heroes"), ReadContent(dir, "enemies"), ReadContent(dir, "campaign"),
                                        balance, ReadContent(dir, "relics"), ReadContent(dir, "events"), ReadContent(dir, "armor"),
                                        floorConditions);
        }

        private static TeamSnapshot Line(List<HeroDef> heroes)
        {
            TeamSnapshot t = new TeamSnapshot();
            for (int i = 0; i < heroes.Count; i++)
                t.Units.Add(new SnapshotUnit { DefId = heroes[i].Id, Level = 40, Stars = heroes[i].Stars });
            return t;
        }

        private static GuildDungeonAttempt Made(string user, int attempt, bool victory, int turns, int fallen, TeamSnapshot team)
        {
            GuildDungeonAttempt a = new GuildDungeonAttempt();
            a.UserId = user; a.Attempt = attempt; a.Victory = victory; a.Turns = turns; a.Fallen = fallen;
            a.Team = team; a.Cycle = 7; a.Floor = 2; a.Id = ++_madeId; a.Seed = _madeSeed;
            return a;
        }

        private static string Met(ContentDatabase content, GuildDungeonAttempt a)
        {
            return string.Join(",", GuildDungeon.BoardsMet(content, a).ConvertAll(b => b.ToString()).ToArray());
        }

        private static string Order(List<GuildDungeonAttempt> board)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < board.Count; i++) parts.Add(board[i].UserId + ":" + board[i].Attempt);
            return string.Join(" ", parts.ToArray());
        }

        // ---------------------------------------------------------------- two members, one floor

        /// <summary>
        /// THE HANDOVER, end to end on the wire shapes. Two members of one guild, two rosters, one
        /// floor. Both attempts go through the submit call's arguments and back out of a row; both
        /// are scored onto the boards they satisfy; one is shared into chat as a message row with
        /// the attempt embedded the way the chat read embeds it; the other member reads that
        /// message, replays it event for event, and then fights the same floor with their own line.
        ///
        /// The server here is a stand-in that assigns ids and attempt numbers. The rules it holds
        /// (the cap, one level, floors in order) are 0008's and are proven by the live rules test,
        /// not by this.
        /// </summary>
        /// <summary>Assigns what guild_dungeon_submit assigns - the row id and the attempt number -
        /// and hands the row back through the codec. Holds none of the server's rules.</summary>
        private sealed class StandInServer
        {
            public readonly List<GuildDungeonAttempt> Stored = new List<GuildDungeonAttempt>();
            private readonly ContentDatabase _content;
            private readonly string _guild;
            private readonly int _seed, _cycle;
            private long _nextId = 1000;

            public StandInServer(ContentDatabase content, string guild, int seed, int cycle)
            {
                _content = content; _guild = guild; _seed = seed; _cycle = cycle;
            }

            public GuildDungeonAttempt Submit(TeamSnapshot line, string user, string name, out BattleResult fought)
            {
                GuildDungeonAttempt made = GuildDungeon.Attempt(_content, line, _seed, _cycle, 1, out fought);
                int number = GuildDungeon.AttemptsUsed(Stored, user, _cycle, 1) + 1;
                JsonValue args = JsonValue.Parse(GuildDungeon.WriteRpcArgs(made).ToJson(false));
                JsonValue row = RowFrom(args, _guild, user, number, _nextId++);
                JsonValue profile = JsonValue.NewObject();
                profile.Set("display_name", name);
                row.Set("profiles", profile);
                GuildDungeonAttempt back = GuildDungeon.ReadRow(JsonValue.Parse(row.ToJson(false)));
                Stored.Add(back);
                return back;
            }
        }

        private static void CheckTwoMembers(ContentDatabase content)
        {
            const string guild = "9d0c1b2a-3e4f-4a5b-8c6d-7e8f9a0b1c2d";
            const string mira = "11111111-aaaa-4aaa-8aaa-111111111111";
            const string tomas = "22222222-bbbb-4bbb-8bbb-222222222222";
            const int cycle = 38;
            int seed = GuildDungeon.SeedFor(guild, cycle);

            // Both lines made legal under floor 1's condition, the way each of them would.
            FloorConditionDef floorOne = GuildDungeon.ConditionOf(content, seed, 1);
            List<Sample> pool = ConformAll(content, SampleRosters(content, 0x7A11), floorOne, 0x7A11);
            TeamSnapshot miraLine = pool[3].Team;
            TeamSnapshot tomasLine = CopyLine(pool.Find(s => s.Group == "3 stars or fewer").Team, 40);
            if (tomasLine.Units.Count == 5) tomasLine.Units.RemoveAt(tomasLine.Units.Count - 1);

            StandInServer server = new StandInServer(content, guild, seed, cycle);
            List<GuildDungeonAttempt> stored = server.Stored;

            BattleResult miraFight, tomasFight;
            GuildDungeonAttempt miraAttempt = server.Submit(miraLine, mira, "Mira", out miraFight);
            GuildDungeonAttempt tomasAttempt = server.Submit(tomasLine, tomas, "Tomas", out tomasFight);

            Line("    two members, floor 1: Mira " + (miraAttempt.Victory ? "cleared" : "fell") + " in " + miraAttempt.Turns
                 + " turns with " + miraAttempt.Team.Units.Count + " [" + Met(content, miraAttempt) + "]; Tomas "
                 + (tomasAttempt.Victory ? "cleared" : "fell") + " in " + tomasAttempt.Turns + " turns with "
                 + tomasAttempt.Team.Units.Count + " [" + Met(content, tomasAttempt) + "]");
            Check(miraAttempt.Attempt == 1 && tomasAttempt.Attempt == 1 && miraAttempt.DisplayName == "Mira",
                  "two members, two rosters, one floor: both attempts stored and read back with their names");

            bool boardsRight = true;
            foreach (GuildDungeonBoard b in GuildDungeon.Boards)
            {
                List<GuildDungeonAttempt> board = GuildDungeon.Rank(content, stored, cycle, 1, b);
                bool miraOn = board.Exists(a => a.UserId == mira), tomasOn = board.Exists(a => a.UserId == tomas);
                if (miraOn != GuildDungeon.Satisfies(content, miraAttempt, b)) boardsRight = false;
                if (tomasOn != GuildDungeon.Satisfies(content, tomasAttempt, b)) boardsRight = false;
            }
            Check(boardsRight, "each appears on exactly the boards its line satisfies, and on no other");
            Check(Met(content, tomasAttempt).Contains("FourOrFewer") == (tomasAttempt.Victory && !GuildDungeon.Grants(floorOne, GuildDungeonBoard.FourOrFewer))
                  && Met(content, tomasAttempt).Contains("NoFiveStar") == (tomasAttempt.Victory && !GuildDungeon.Grants(floorOne, GuildDungeonBoard.NoFiveStar)),
                  "Tomas's four-hero, three-star line enters FOUR OR FEWER and NO FIVE STARS exactly when it clears"
                  + " and the floor's condition (" + (floorOne != null ? floorOne.Name : "none") + ") has not suspended them");

            // Mira shares. The message row as the chat read returns it: the line, the author's name,
            // the attempt id and the attempt row embedded under "attempt".
            GuildDungeonAttempt shared = miraAttempt;
            JsonValue share = GuildDungeon.WriteShareArgs(shared);
            Check(share.GetLong("p_attempt_id", 0) == shared.Id && share.GetString("p_body", "").Length > 0 && share.GetString("p_body", "").Length <= 200,
                  "sharing sends the attempt's id and a line that stands on its own for an old client: \"" + share.GetString("p_body", "") + "\"");

            JsonValue message = JsonValue.NewObject();
            message.Set("id", 77L);
            message.Set("user_id", mira);
            message.Set("body", share.GetString("p_body", ""));
            message.Set("created_at", "2026-09-22T21:30:00+00:00");
            message.Set("attempt_id", shared.Id);
            JsonValue author = JsonValue.NewObject();
            author.Set("display_name", "Mira");
            message.Set("profiles", author);
            JsonValue embedded = JsonValue.Parse(GuildDungeon.WriteRpcArgs(shared).ToJson(false));
            message.Set("attempt", RowFrom(embedded, guild, mira, shared.Attempt, shared.Id));
            GuildMessageEntry seen = GuildCodec.ReadMessage(JsonValue.Parse(message.ToJson(false)), tomas);
            Check(seen != null && !seen.IsMine && seen.AttemptId == shared.Id && seen.Attempt != null
                  && seen.Attempt.DisplayName == "Mira",
                  "Tomas reads it as a card carrying Mira's whole attempt, not as a line of text");

            // Tomas watches it.
            GuildDungeonReplay watched = GuildDungeon.Replay(content, seen.Attempt);
            Check(watched.Playable && Fingerprint(watched.Result) == Fingerprint(miraFight)
                  && watched.Result.Events.Count == miraFight.Events.Count,
                  "AND WATCHES THAT EXACT FIGHT - " + watched.Result.Events.Count + " events, the same result, the same turns");
            bool frameForFrame = watched.Playable;
            for (int i = 0; frameForFrame && i < miraFight.Events.Count; i++)
            {
                BattleEvent x = miraFight.Events[i], y = watched.Result.Events[i];
                if (x.Type != y.Type || x.SourceId != y.SourceId || x.TargetId != y.TargetId
                    || x.Value != y.Value || x.Critical != y.Critical || x.Status != y.Status) frameForFrame = false;
            }
            Check(frameForFrame, "frame for frame: every event's kind, actor, target and amount match the fight Mira had");

            // Then runs the same floor with his own line, from the replay: the record names the floor and the seed.
            BattleResult tomasSecondFight;
            GuildDungeonAttempt tomasSecond = server.Submit(pool[7].Team, tomas, "Tomas", out tomasSecondFight);
            Check(tomasSecond.Attempt == 2 && tomasSecond.Floor == seen.Attempt.Floor && tomasSecond.Seed == seen.Attempt.Seed,
                  "then fights the same floor with his own roster, as his second attempt ("
                  + (tomasSecond.Victory ? "cleared in " : "fell after ") + tomasSecond.Turns + " turns)");
            Check(GuildDungeon.AttemptsUsed(stored, tomas, cycle, 1) == 2 && GuildDungeon.AttemptsUsed(stored, mira, cycle, 1) == 1,
                  "and it is his attempt that was spent, not hers");
        }

        // ---------------------------------------------------------------- the server's copy

        /// <summary>
        /// The attempt cap and the floor count each live twice: in balance.json, where the client
        /// reads them, and in migration 0007, where the server enforces them. Neither can read the
        /// other at runtime, so the harness reads both and refuses a disagreement.
        /// </summary>
        private static void CheckServerAgrees(ContentDatabase content, string contentDir)
        {
            string migration = FindMigration(contentDir, "0007_guild_dungeon.sql");
            if (migration == null)
            {
                Check(false, "Server/supabase/migrations/0007_guild_dungeon.sql is findable from the content folder");
                return;
            }
            string sql = File.ReadAllText(migration);
            int cap = SqlConstant(sql, "guild_dungeon_attempt_cap");
            int floors = SqlConstant(sql, "guild_dungeon_floor_count");
            Check(cap == content.Balance.GuildDungeonAttemptsPerFloor,
                  "the server's attempt cap (" + cap + ") is balance.json's (" + content.Balance.GuildDungeonAttemptsPerFloor + ")");
            Check(floors == content.Balance.GuildDungeonFloors,
                  "the server's floor count (" + floors + ") is balance.json's (" + content.Balance.GuildDungeonFloors + ")");

            // 0008 refuses any submitted unit not at this level, so a disagreement would make
            // every attempt this build fights bounce off the server.
            string playable = FindMigration(contentDir, "0008_guild_dungeon_playable.sql");
            int level = playable == null ? -1 : SqlConstant(File.ReadAllText(playable), "guild_dungeon_hero_level");
            Check(level == content.Balance.GuildDungeonHeroLevel,
                  "the server's dungeon hero level (" + level + ", 0008) is balance.json's (" + content.Balance.GuildDungeonHeroLevel + ")");
        }

        private static int SqlConstant(string sql, string function)
        {
            Match m = Regex.Match(sql, @"function\s+public\." + function + @"\(\)[\s\S]*?\$\$\s*select\s+(\d+)\s*;\s*\$\$",
                                  RegexOptions.IgnoreCase);
            return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : -1;
        }

        private static string FindMigration(string contentDir, string file)
        {
            DirectoryInfo dir = string.IsNullOrEmpty(contentDir) ? null : new DirectoryInfo(contentDir);
            for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, Path.Combine("Server", Path.Combine("supabase",
                                   Path.Combine("migrations", file))));
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }

        // ---------------------------------------------------------------- THE PROPERTY

        internal sealed class Sample
        {
            public string Group = "";
            public TeamSnapshot Team = new TeamSnapshot();
        }

        /// <summary>The role families a floor might be "obviously" answered by. Each sample group
        /// below is a line drawn with one family removed entirely.</summary>
        private static readonly string[] FamilyNames = { "no tank", "no healer", "no area", "no support", "no single-target" };

        private static int FamilyOf(HeroRole role)
        {
            switch (role)
            {
                case HeroRole.Bulwark: case HeroRole.Vanguard: return 0;
                case HeroRole.Mender: return 1;
                case HeroRole.Breaker: case HeroRole.Binder: return 2;
                case HeroRole.Warden: case HeroRole.Tinkerer: return 3;
                default: return 4; // Striker, Stalker, Marksman
            }
        }

        /// <summary>
        /// The spread: random lines, plus lines each missing one role family outright, plus lines of
        /// nothing above three stars. Deterministic in the seed, so a failure names a line that can
        /// be rebuilt and looked at.
        /// </summary>
        internal static List<Sample> SampleRosters(ContentDatabase content, int seed)
        {
            DeterministicRng rng = new DeterministicRng(seed);
            List<Sample> samples = new List<Sample>();
            List<HeroDef> all = new List<HeroDef>(content.Heroes);

            for (int i = 0; i < 24; i++) samples.Add(Draw("random", all, rng, content));
            for (int family = 0; family < FamilyNames.Length; family++)
            {
                List<HeroDef> without = all.FindAll(h => FamilyOf(h.Role) != family);
                for (int i = 0; i < 6; i++) samples.Add(Draw(FamilyNames[family], without, rng, content));
            }
            List<HeroDef> humble = all.FindAll(h => h.Stars <= 3);
            for (int i = 0; i < 12; i++) samples.Add(Draw("3 stars or fewer", humble, rng, content));
            return samples;
        }

        private static Sample Draw(string group, List<HeroDef> from, DeterministicRng rng, ContentDatabase content)
        {
            Sample s = new Sample();
            s.Group = group;
            List<HeroDef> pool = new List<HeroDef>(from);
            int size = Math.Min(content.Balance.MaxTeamSize, pool.Count);
            for (int i = 0; i < size; i++)
            {
                int index = rng.Range(0, pool.Count);
                HeroDef def = pool[index];
                pool.RemoveAt(index);
                SnapshotUnit u = new SnapshotUnit();
                u.DefId = def.Id;
                u.Level = ReferenceLevel;
                u.Stars = def.Stars;
                s.Team.Units.Add(u);
            }
            s.Team.Label = group;
            return s;
        }

        /// <summary>
        /// NOT A HERO CHECK. Every floor of several generated dungeons, against the whole spread.
        /// Asserted per generated floor, not on average: one bad floor in one guild's week is the
        /// failure this exists to catch, and an average would hide it.
        ///
        /// Over THE LONGEST LADDER, floors 1 to GuildDungeonScale.MaxLadder: a floor only a guild of
        /// thirty sees is still a floor thirty rosters have to be able to solve.
        /// </summary>
        private static void CheckNotAHeroCheck(ContentDatabase content)
        {
            int floors = GuildDungeonScale.MaxLadder(content);
            int baseLadder = GuildDungeonScale.BaseLadder(content);
            Line("  NOT A HERO CHECK - floors 1 to " + floors + " of " + SampleGuilds + " dungeons against a spread of rosters at level " + ReferenceLevel);
            List<Sample> samples = SampleRosters(content, 0x6D1C);
            List<string> groups = new List<string>();
            for (int i = 0; i < samples.Count; i++) if (!groups.Contains(samples[i].Group)) groups.Add(samples[i].Group);

            int badShare = 0, badGroup = 0, deepGroup = 0, badHero = 0, total = 0;
            List<string> firstFailures = new List<string>();

            Line("    " + "guild".PadRight(7) + "floor".PadRight(7) + "theme".PadRight(11) + "all".PadRight(6)
                 + string.Join("", groups.ConvertAll(g => Short(g).PadRight(8)).ToArray()) + "top hero share");

            for (int g = 0; g < SampleGuilds; g++)
            {
                int seed = GuildDungeon.SeedFor("sample-guild-" + g, 1);
                GuildDungeonTheme[] themes = GuildDungeonGenerator.ThemesFor(floors, seed);
                for (int f = 1; f <= floors; f++)
                {
                    total++;
                    // The floor's own condition this cycle, with each sample made legal under it -
                    // the guardrails are asserted WITH CONDITIONS APPLIED, as the design requires.
                    FloorConditionDef condition = GuildDungeon.ConditionOf(content, seed, f);
                    List<Sample> lines = ConformAll(content, samples, condition, 0x6D1C);
                    Dictionary<string, int[]> byGroup = new Dictionary<string, int[]>();
                    Dictionary<string, int> heroInClears = new Dictionary<string, int>();
                    int clears = 0;
                    for (int s = 0; s < samples.Count; s++)
                    {
                        int[] tally;
                        if (!byGroup.TryGetValue(lines[s].Group, out tally)) byGroup[lines[s].Group] = tally = new int[2];
                        tally[1]++;
                        if (!GuildDungeon.Fight(content, lines[s].Team, seed, f).Victory) continue;
                        tally[0]++;
                        clears++;
                        for (int u = 0; u < lines[s].Team.Units.Count; u++)
                        {
                            string id = lines[s].Team.Units[u].DefId;
                            int n; heroInClears.TryGetValue(id, out n); heroInClears[id] = n + 1;
                        }
                    }

                    float share = (float)clears / samples.Count;
                    string topHero = "-"; float topShare = 0f;
                    foreach (KeyValuePair<string, int> kv in heroInClears)
                    {
                        float hs = clears == 0 ? 0f : (float)kv.Value / clears;
                        if (hs > topShare) { topShare = hs; topHero = kv.Key; }
                    }

                    List<string> cells = new List<string>();
                    string worstGroup = null; float worstShare = 1f;
                    for (int i = 0; i < groups.Count; i++)
                    {
                        int[] t = byGroup[groups[i]];
                        float gs = (float)t[0] / t[1];
                        cells.Add(Pct(gs).PadRight(8));
                        if (gs < worstShare) { worstShare = gs; worstGroup = groups[i]; }
                    }
                    Line("    " + g.ToString().PadRight(7) + f.ToString().PadRight(7) + themes[f - 1].ToString().PadRight(11)
                         + Pct(share).PadRight(6) + string.Join("", cells.ToArray())
                         + Pct(topShare) + " " + topHero + "   [" + (condition != null ? condition.Id : "no condition") + "]");

                    string where = "guild " + g + " floor " + f + " (" + themes[f - 1] + ", " + (condition != null ? condition.Id : "no condition") + ")";
                    if (share < MinClearShare) { badShare++; Note(firstFailures, where + ": only " + Pct(share) + " of rosters clear"); }
                    if (worstShare < MinGroupClearShare)
                    {
                        // The group guardrail is asserted per floor on the BASE ladder, as it always
                        // was. A deep floor's breach is printed, and deep floors are judged by
                        // CheckDeepFloorsMatchBase against the base block at the same position: see
                        // that check for why the per-floor form cannot be applied there.
                        if (f <= baseLadder) { badGroup++; Note(firstFailures, where + ": '" + worstGroup + "' lines clear only " + Pct(worstShare)); }
                        else { deepGroup++; Note(firstFailures, where + ": '" + worstGroup + "' lines clear only " + Pct(worstShare) + " (deep floor: see the block comparison below)"); }
                    }
                    if (clears > 0 && topShare > MaxHeroShareOfClears) { badHero++; Note(firstFailures, where + ": " + topHero + " is in " + Pct(topShare) + " of clears"); }
                }
            }

            int baseTotal = SampleGuilds * Math.Min(baseLadder, floors);
            for (int i = 0; i < firstFailures.Count; i++) Line("    ! " + firstFailures[i]);
            Check(badShare == 0, "every floor is cleared by at least " + Pct(MinClearShare) + " of all sample rosters ("
                  + (total - badShare) + " of " + total + " floors, every ladder floor)");
            Check(badGroup == 0, "AND by at least " + Pct(MinGroupClearShare) + " of every roster missing an obvious answer - no tank, no healer, no area, no support, no single-target, 3 stars or fewer ("
                  + (baseTotal - badGroup) + " of " + baseTotal + " base floors; " + deepGroup + " deep-floor breach(es) here, judged by the block comparison)");
            Check(badHero == 0, "and no single hero is in more than " + Pct(MaxHeroShareOfClears) + " of any floor's clears ("
                  + (total - badHero) + " of " + total + " floors, every ladder floor)");
        }

        private static void Note(List<string> notes, string text) { if (notes.Count < 12) notes.Add(text); }

        private static string Short(string group)
        {
            switch (group)
            {
                case "random": return "rand";
                case "no tank": return "-tank";
                case "no healer": return "-heal";
                case "no area": return "-area";
                case "no support": return "-supp";
                case "no single-target": return "-single";
                case "3 stars or fewer": return "<=3*";
            }
            return group;
        }

        // ---------------------------------------------------------------- line rules on a sample

        /// <summary>The heroes a sample group draws from, by the group's own name - the same
        /// filters SampleRosters applied when it drew the group.</summary>
        private static List<HeroDef> GroupPool(ContentDatabase content, string group)
        {
            List<HeroDef> all = new List<HeroDef>(content.Heroes);
            for (int family = 0; family < FamilyNames.Length; family++)
                if (group == FamilyNames[family]) return all.FindAll(h => FamilyOf(h.Role) != family);
            if (group == "3 stars or fewer") return all.FindAll(h => h.Stars <= 3);
            return all;
        }

        /// <summary>
        /// A sample made legal under a condition's line rules, THE WAY A PLAYER WOULD: keep every
        /// hero the rule allows, in slot order, and refill the freed slots from the same group's
        /// pool (so a "no healer" sample stays one) with heroes that keep the line legal, up to the
        /// cap. A condition with no line rules returns the sample itself, so a fight-only condition
        /// is measured on exactly the lines the unconditioned floor was.
        /// </summary>
        internal static Sample Conform(ContentDatabase content, Sample sample, FloorConditionDef condition, DeterministicRng rng)
        {
            if (condition == null || condition.Line.IsEmpty) return sample;
            LineRules rules = condition.Line;
            int size = content.Balance.MaxTeamSize;
            if (rules.MaxHeroes > 0) size = Math.Min(size, rules.MaxHeroes);

            Sample s = new Sample();
            s.Group = sample.Group;
            s.Team.Label = sample.Team.Label;
            for (int i = 0; i < sample.Team.Units.Count && s.Team.Units.Count < size; i++)
                if (Fits(content, rules, s.Team, sample.Team.Units[i].DefId)) s.Team.Units.Add(sample.Team.Units[i]);

            List<HeroDef> pool = GroupPool(content, sample.Group);
            while (s.Team.Units.Count < size)
            {
                List<HeroDef> ok = pool.FindAll(h => !s.Team.Units.Exists(u => u.DefId == h.Id) && Fits(content, rules, s.Team, h.Id));
                if (ok.Count == 0) break;
                HeroDef def = ok[rng.Range(0, ok.Count)];
                s.Team.Units.Add(new SnapshotUnit { DefId = def.Id, Level = ReferenceLevel, Stars = def.Stars });
            }
            return s;
        }

        private static bool Fits(ContentDatabase content, LineRules rules, TeamSnapshot line, string defId)
        {
            HeroDef d = content.GetHero(defId);
            if (d == null) return false;
            if (rules.MaxStars > 0 && d.Stars > rules.MaxStars) return false;
            if (rules.BannedRoles.Contains(d.Role)) return false;
            if (rules.MaxPerRole > 0)
            {
                int same = 0;
                for (int i = 0; i < line.Units.Count; i++)
                {
                    HeroDef o = content.GetHero(line.Units[i].DefId);
                    if (o != null && o.Role == d.Role) same++;
                }
                if (same >= rules.MaxPerRole) return false;
            }
            return true;
        }

        private static List<Sample> ConformAll(ContentDatabase content, List<Sample> samples, FloorConditionDef condition, int seed)
        {
            if (condition == null || condition.Line.IsEmpty) return samples;
            DeterministicRng rng = new DeterministicRng(seed);
            List<Sample> result = new List<Sample>(samples.Count);
            for (int i = 0; i < samples.Count; i++) result.Add(Conform(content, samples[i], condition, rng));
            return result;
        }

        // ---------------------------------------------------------------- THE CONDITION STUDY

        /// <summary>
        /// A DIFFICULTY KNOB, BUILT TO FAIL C5. "Enemies have more health" is the design's own
        /// example of a condition that changes how hard a floor is and not which line clears it.
        /// The study fights every floor on a LADDER of these, so each real condition can be compared
        /// with a pure knob that stops the SAME NUMBER of lines: if the condition stops the same
        /// lines the knob does, it is a knob too.
        /// </summary>
        private static FloorConditionDef Knob(string id, float enemyHp, float heroHp)
        {
            FloorConditionDef c = new FloorConditionDef();
            c.Id = id;
            c.Name = id;
            c.Text = "control";
            c.Fight.EnemyMaxHp = enemyHp;
            c.Fight.HeroMaxHp = heroHp;
            return c;
        }

        private static readonly float[] EnemyHpLadder = { 0.7f, 0.85f, 1f, 1.15f, 1.3f, 1.5f, 1.75f, 2f, 2.4f, 2.9f, 3.5f, 4.2f };
        private static readonly float[] HeroHpLadder = { 1.3f, 1.15f, 1f, 0.9f, 0.8f, 0.7f, 0.6f, 0.5f, 0.42f, 0.35f, 0.28f, 0.22f };

        /// <summary>One floor's outcomes for one population: who fought, who cleared, and by how
        /// much - the MARGIN, the share of HP the heroes kept minus the share the enemies kept, in
        /// [-1, 1]. A clear is a margin above zero.</summary>
        private sealed class Outcome
        {
            public List<Sample> Lines;
            public bool[] Cleared;
            public float[] Margin;
            public float Share;
            public float MeanMargin;
        }

        private static Outcome FightAll(ContentDatabase content, List<Sample> lines, int seed, int floor, FloorConditionDef condition)
        {
            return FightAll(content, lines, seed, floor, condition, GuildDungeon.FightSeedFor(seed, floor));
        }

        private static Outcome FightAll(ContentDatabase content, List<Sample> lines, int seed, int floor, FloorConditionDef condition,
                                        int fightSeed)
        {
            Outcome o = new Outcome();
            o.Lines = lines;
            o.Cleared = new bool[lines.Count];
            o.Margin = new float[lines.Count];
            int clears = 0;
            double sum = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                BattleResult r = GuildDungeon.FightUnder(content, lines[i].Team, seed, floor, condition, fightSeed);
                o.Cleared[i] = r.Victory;
                o.Margin[i] = MarginOf(r);
                sum += o.Margin[i];
                if (o.Cleared[i]) clears++;
            }
            o.Share = lines.Count == 0 ? 0f : (float)clears / lines.Count;
            o.MeanMargin = lines.Count == 0 ? 0f : (float)(sum / lines.Count);
            return o;
        }

        private static float MarginOf(BattleResult r)
        {
            return r.PlayerHpRatio - r.EnemyHpRatio;
        }

        /// <summary>
        /// Spearman's rank correlation between two readings of the same rosters: 1 when the
        /// condition leaves every roster in the order it stood, lower the more it reorders them.
        /// Ties share their mean rank.
        /// </summary>
        internal static float RankCorrelation(float[] a, float[] b)
        {
            double[] ra = Ranks(a), rb = Ranks(b);
            int n = a.Length;
            if (n < 3) return 1f;
            double ma = 0, mb = 0;
            for (int i = 0; i < n; i++) { ma += ra[i]; mb += rb[i]; }
            ma /= n; mb /= n;
            double cov = 0, va = 0, vb = 0;
            for (int i = 0; i < n; i++)
            {
                cov += (ra[i] - ma) * (rb[i] - mb);
                va += (ra[i] - ma) * (ra[i] - ma);
                vb += (rb[i] - mb) * (rb[i] - mb);
            }
            return va <= 0 || vb <= 0 ? 1f : (float)(cov / Math.Sqrt(va * vb));
        }

        private static double[] Ranks(float[] v)
        {
            int n = v.Length;
            int[] idx = new int[n];
            for (int i = 0; i < n; i++) idx[i] = i;
            Array.Sort(idx, (x, y) => { int c = v[x].CompareTo(v[y]); return c != 0 ? c : x.CompareTo(y); });
            double[] r = new double[n];
            for (int i = 0; i < n; )
            {
                int j = i;
                while (j + 1 < n && v[idx[j + 1]] == v[idx[i]]) j++;
                double mean = (i + j) / 2.0;
                for (int k = i; k <= j; k++) r[idx[k]] = mean;
                i = j + 1;
            }
            return r;
        }

        /// <summary>The ladder rung whose MEAN MARGIN is nearest a condition's: the pure knob that
        /// bites exactly as hard.</summary>
        private static Outcome NearestBite(List<Outcome> ladder, float meanMargin)
        {
            Outcome best = ladder[0];
            for (int i = 1; i < ladder.Count; i++)
                if (Math.Abs(ladder[i].MeanMargin - meanMargin) < Math.Abs(best.MeanMargin - meanMargin)) best = ladder[i];
            return best;
        }

        /// <summary>
        /// A player's answer to a line rule: of several legal versions of their line (each one
        /// dropping or replacing a DIFFERENT hero), the one that does best on this floor. Returns
        /// the outcome with each roster fighting its best version. A condition with no line rules
        /// fights the roster's own line.
        /// </summary>
        private static Outcome FightAdapted(ContentDatabase content, List<Sample> population, int seed, int floor,
                                            FloorConditionDef condition, int salt)
        {
            if (condition == null || condition.Line.IsEmpty) return FightAll(content, population, seed, floor, condition);
            Outcome best = null;
            for (int v = 0; v < AdaptVariants; v++)
            {
                DeterministicRng rng = new DeterministicRng(salt * 31 + v);
                List<Sample> lines = new List<Sample>(population.Count);
                for (int i = 0; i < population.Count; i++) lines.Add(ConformFrom(content, population[i], condition, rng, v));
                Outcome o = FightAll(content, lines, seed, floor, condition);
                if (best == null) { best = o; continue; }
                for (int i = 0; i < population.Count; i++)
                {
                    if (o.Margin[i] <= best.Margin[i]) continue;
                    best.Margin[i] = o.Margin[i];
                    best.Cleared[i] = o.Cleared[i];
                    best.Lines[i] = o.Lines[i];
                }
            }
            int clears = 0; double sum = 0;
            for (int i = 0; i < population.Count; i++) { if (best.Cleared[i]) clears++; sum += best.Margin[i]; }
            best.Share = (float)clears / population.Count;
            best.MeanMargin = (float)(sum / population.Count);
            return best;
        }

        private const int AdaptVariants = 5;

        /// <summary>Conform, but walking the line from slot `start` round, so each variant keeps a
        /// different subset when the rule makes the line drop someone.</summary>
        private static Sample ConformFrom(ContentDatabase content, Sample sample, FloorConditionDef condition, DeterministicRng rng, int start)
        {
            Sample rotated = new Sample();
            rotated.Group = sample.Group;
            rotated.Team.Label = sample.Team.Label;
            int n = sample.Team.Units.Count;
            for (int i = 0; i < n; i++) rotated.Team.Units.Add(sample.Team.Units[(i + start) % n]);
            return Conform(content, rotated, condition, rng);
        }

        /// <summary>The line features the guardrails already name - the "obvious answers" - plus
        /// the star extremes. The study reports which of them a condition moves among the best lines.</summary>
        private static readonly string[] FeatureNames = { "tank", "healer", "area", "support", "single", "all<=3*", "any 5*" };

        private static bool[] Features(ContentDatabase content, TeamSnapshot line)
        {
            bool[] f = new bool[FeatureNames.Length];
            bool humble = line.Units.Count > 0;
            for (int i = 0; i < line.Units.Count; i++)
            {
                HeroDef d = content.GetHero(line.Units[i].DefId);
                if (d == null) continue;
                f[FamilyOf(d.Role)] = true;
                if (d.Stars > 3) humble = false;
                if (d.Stars >= 5) f[6] = true;
            }
            f[5] = humble;
            return f;
        }

        /// <summary>Among the best quarter of rosters by margin, the share carrying each feature.</summary>
        private static float[] TopMix(ContentDatabase content, Outcome o)
        {
            int n = o.Lines.Count;
            int[] idx = new int[n];
            for (int i = 0; i < n; i++) idx[i] = i;
            Array.Sort(idx, (x, y) => { int c = o.Margin[y].CompareTo(o.Margin[x]); return c != 0 ? c : x.CompareTo(y); });
            int top = Math.Max(1, n / 4);
            float[] mix = new float[FeatureNames.Length];
            for (int k = 0; k < top; k++)
            {
                bool[] f = Features(content, o.Lines[idx[k]].Team);
                for (int j = 0; j < f.Length; j++) if (f[j]) mix[j]++;
            }
            for (int j = 0; j < mix.Length; j++) mix[j] /= top;
            return mix;
        }

        /// <summary>The guardrails of C6 on one floor's outcome: all rosters, the worst group, the
        /// most-used hero among clears. Empty when they hold, else the first that failed.</summary>
        private static string Guardrails(Outcome o)
        {
            if (o.Share < MinClearShare) return "only " + Pct(o.Share) + " clear";
            Dictionary<string, int[]> byGroup = new Dictionary<string, int[]>();
            Dictionary<string, int> heroInClears = new Dictionary<string, int>();
            int clears = 0;
            for (int i = 0; i < o.Lines.Count; i++)
            {
                int[] t;
                if (!byGroup.TryGetValue(o.Lines[i].Group, out t)) byGroup[o.Lines[i].Group] = t = new int[2];
                t[1]++;
                if (!o.Cleared[i]) continue;
                t[0]++;
                clears++;
                for (int u = 0; u < o.Lines[i].Team.Units.Count; u++)
                {
                    string id = o.Lines[i].Team.Units[u].DefId;
                    int n; heroInClears.TryGetValue(id, out n); heroInClears[id] = n + 1;
                }
            }
            foreach (KeyValuePair<string, int[]> kv in byGroup)
            {
                float gs = (float)kv.Value[0] / kv.Value[1];
                if (gs < MinGroupClearShare) return "'" + kv.Key + "' clears " + Pct(gs);
            }
            foreach (KeyValuePair<string, int> kv in heroInClears)
                if (clears > 0 && (float)kv.Value / clears > MaxHeroShareOfClears)
                    return kv.Key + " in " + Pct((float)kv.Value / clears) + " of clears";
            return "";
        }

        /// <summary>One condition's reading on one floor. See RunConditionStudy for each column.</summary>
        private sealed class FloorReading
        {
            public float Base, Cond, Bite, Shift, KnobA, KnobB, Chaos, Forced;
            public float[] MixShift = new float[FeatureNames.Length];
            public string Guard = "";
        }

        private static FloorReading[] ReadFloor(ContentDatabase content, List<Sample> population, List<FloorConditionDef> ladderA,
                                                List<FloorConditionDef> ladderB, List<FloorConditionDef> studied, int seed, int f)
        {
            Outcome baseline = FightAll(content, population, seed, f, null);
            List<Outcome> rungsA = new List<Outcome>(), rungsB = new List<Outcome>();
            for (int i = 0; i < ladderA.Count; i++) rungsA.Add(FightAll(content, population, seed, f, ladderA[i]));
            for (int i = 0; i < ladderB.Count; i++) rungsB.Add(FightAll(content, population, seed, f, ladderB[i]));
            // CHANCE ALONE: the same floor, the same lines, no condition, a different combat seed.
            // What that does to the order of the rosters is the floor under every reading below.
            Outcome reseeded = FightAll(content, population, seed, f, null, GuildDungeon.FightSeedFor(seed, f) ^ 0x5A5A);
            float chaos = 1f - RankCorrelation(baseline.Margin, reseeded.Margin);

            FloorReading[] result = new FloorReading[studied.Count];
            for (int c = 0; c < studied.Count; c++)
            {
                FloorConditionDef cond = studied[c];
                Outcome under = FightAdapted(content, population, seed, f, cond, StudySeed ^ GuildDungeon.SeedFor(cond.Id, 0));
                Outcome matchA = NearestBite(rungsA, under.MeanMargin);
                Outcome matchB = NearestBite(rungsB, under.MeanMargin);

                int forced = 0, baseClears = 0;
                for (int i = 0; i < population.Count; i++)
                {
                    if (!baseline.Cleared[i]) continue;
                    baseClears++;
                    string why;
                    if (!GuildDungeon.LineLegal(content, cond, population[i].Team, out why)) forced++;
                }

                FloorReading r = new FloorReading();
                r.Base = baseline.Share;
                r.Cond = under.Share;
                r.Bite = baseline.MeanMargin - under.MeanMargin;
                r.Shift = 1f - RankCorrelation(baseline.Margin, under.Margin);
                r.KnobA = 1f - RankCorrelation(baseline.Margin, matchA.Margin);
                r.KnobB = 1f - RankCorrelation(baseline.Margin, matchB.Margin);
                r.Chaos = chaos;
                r.Forced = baseClears == 0 ? 0f : (float)forced / baseClears;
                float[] mc = TopMix(content, under), mk = TopMix(content, matchA);
                for (int k = 0; k < mc.Length; k++) r.MixShift[k] = mc[k] - mk[k];
                r.Guard = Guardrails(under);
                result[c] = r;
            }
            return result;
        }

        /// <summary>The study's per-(condition, theme) tally, summed over every floor of that theme.</summary>
        private sealed class Tally
        {
            public int Floors, GuardFails;
            public float Base, Cond, Bite, Shift, KnobA, KnobB, Chaos, Forced;
            public float[] MixShift = new float[FeatureNames.Length];
            public string FirstGuardFail = "";
        }

        internal const int StudyGuilds = 12;
        internal const int StudySeed = 0x57D1;

        /// <summary>
        /// C5's PASS MARK: a condition reorders the rosters by at least this multiple of what the
        /// harder-reordering of two pure difficulty knobs does at the same bite. Two knobs of
        /// different kinds (enemy HP up, hero HP down) already reorder the rosters somewhat, because
        /// no knob is perfectly neutral and a fight is chaotic; a condition that reorders no more
        /// than they do is, measurably, a knob.
        /// </summary>
        internal const float ShapeFactor = 1.5f;

        /// <summary>
        /// THE CONDITION STUDY: DevHarness.exe --guild-conditions. Not a check and not run by the gate;
        /// the instrument the pool, each condition's magnitude and each condition's "floors" list
        /// are read from (DESIGN-GUILD-FLOOR-CONDITIONS section 7).
        ///
        /// For every floor of StudyGuilds dungeons it fights a large sample population (the gate's
        /// spread, four times over) with no condition, on two ladders of pure difficulty knobs, and
        /// under every condition in content. A roster under a condition with line rules fields the
        /// best of AdaptVariants legal versions of its line, as a player would. Per condition and
        /// floor theme it prints:
        ///
        ///   base / cond   the clear rate without and with the condition
        ///   bite          how far the mean margin (hero HP kept minus enemy HP kept) fell
        ///   SHIFT         1 - the rank correlation of every roster's margin with and without the
        ///                 condition: 0 = the rosters stand in the same order, higher = reordered.
        ///                 THE C5 READING: which rosters do best, not how many clear
        ///   knobA / knobB the same number for a pure enemy-HP knob and a pure hero-HP knob biting
        ///                 exactly as hard - what "only harder" reads
        ///   chaos         the same number for the unconditioned floor fought under a second combat
        ///                 seed: what chance alone does to the order, with nothing changed at all
        ///   ratio         SHIFT over the largest of knobA, knobB and chaos; C5 passes at ShapeFactor
        ///   forced        for a line rule, the share of the unconditioned floor's clearing lines the
        ///                 rule makes illegal - lines their owner must change
        ///   guard         floors of the theme on which all three C6 guardrails held
        ///   mix           the feature that moved most among the best quarter of rosters, against the
        ///                 enemy-HP knob biting as hard
        /// </summary>
        internal static int RunConditionStudy(ContentDatabase content)
        {
            List<FloorConditionDef> studied = new List<FloorConditionDef>(content.FloorConditions);
            // The knob as a candidate in its own right, so the table shows a C5 failure next to the
            // conditions it is read against.
            studied.Insert(0, Knob("CONTROL: enemies +75% HP", 1.75f, 1f));

            Dictionary<string, Tally> tallies = Study(content, studied, true);

            Line("  " + "condition".PadRight(26) + "theme".PadRight(11) + "base".PadRight(6) + "cond".PadRight(6) + "bite".PadRight(7)
                 + "SHIFT".PadRight(7) + "knobA".PadRight(7) + "knobB".PadRight(7) + "chaos".PadRight(7) + "ratio".PadRight(7) + "C5".PadRight(6)
                 + "forced".PadRight(8) + "guard".PadRight(7) + "mix (best quarter vs knob)");
            for (int c = 0; c < studied.Count; c++)
            {
                for (int th = 0; th < AllThemes.Length; th++)
                {
                    Tally t;
                    if (!tallies.TryGetValue(studied[c].Id + "|" + AllThemes[th], out t) || t.Floors == 0) continue;
                    float n = t.Floors;
                    int top = 0;
                    for (int k = 1; k < t.MixShift.Length; k++) if (Math.Abs(t.MixShift[k]) > Math.Abs(t.MixShift[top])) top = k;
                    float shift = t.MixShift[top] / n;
                    float ratio = RatioOf(t);
                    string verdict = ratio >= ShapeFactor ? (t.GuardFails == 0 ? "PASS" : "guard") : "knob";
                    Line("  " + studied[c].Id.PadRight(26) + AllThemes[th].ToString().PadRight(11) + Pct(t.Base / n).PadRight(6)
                         + Pct(t.Cond / n).PadRight(6) + F2(t.Bite / n).PadRight(7) + F2(t.Shift / n).PadRight(7)
                         + F2(t.KnobA / n).PadRight(7) + F2(t.KnobB / n).PadRight(7) + F2(t.Chaos / n).PadRight(7) + F2(Math.Min(ratio, 99f)).PadRight(7)
                         + verdict.PadRight(6) + Pct(t.Forced / n).PadRight(8) + ((t.Floors - t.GuardFails) + "/" + t.Floors).PadRight(7)
                         + FeatureNames[top] + " " + (shift >= 0 ? "+" : "") + (shift * 100f).ToString("0", CultureInfo.InvariantCulture) + "pp"
                         + (t.FirstGuardFail.Length > 0 ? "   ! " + t.FirstGuardFail : ""));
                }
            }
            return 0;
        }

        /// <summary>SHIFT over the largest of the two matched knobs and chance alone.</summary>
        private static float RatioOf(Tally t)
        {
            float knob = Math.Max(Math.Max(t.KnobA, t.KnobB), t.Chaos) / t.Floors;
            return knob <= 0.0001f ? 99f : (t.Shift / t.Floors) / knob;
        }

        /// <summary>
        /// The study's measurement, shared by the study table and the gate: every floor of
        /// StudyGuilds dungeons, the population, both knob ladders and chance, and every condition
        /// in `studied`, summed per (condition id, theme).
        /// </summary>
        private static Dictionary<string, Tally> Study(ContentDatabase content, List<FloorConditionDef> studied, bool progress)
        {
            ReferenceLevel = content.Balance.GuildDungeonHeroLevel;
            int floors = content.Balance.GuildDungeonFloors;
            List<Sample> population = new List<Sample>();
            for (int k = 0; k < 4; k++) population.AddRange(SampleRosters(content, StudySeed + k));

            List<FloorConditionDef> ladderA = new List<FloorConditionDef>(), ladderB = new List<FloorConditionDef>();
            for (int i = 0; i < EnemyHpLadder.Length; i++) ladderA.Add(Knob("enemy hp x" + EnemyHpLadder[i], EnemyHpLadder[i], 1f));
            for (int i = 0; i < HeroHpLadder.Length; i++) ladderB.Add(Knob("hero hp x" + HeroHpLadder[i], 1f, HeroHpLadder[i]));

            if (progress)
                Line("GUILD FLOOR CONDITIONS - STUDY: " + StudyGuilds + " dungeons x " + floors + " floors, "
                     + population.Count + " sample lines per floor, level " + ReferenceLevel);

            // Every floor is independent - combat is a pure function and the engine holds no mutable
            // static state - so the floors are fought in parallel and their readings MERGED IN FLOOR
            // ORDER afterwards, which keeps every sum, and so every printed number, identical to a
            // one-thread run.
            int total = StudyGuilds * floors;
            FloorReading[][] readings = new FloorReading[total][];
            GuildDungeonTheme[] floorTheme = new GuildDungeonTheme[total];
            int done = 0;
            System.Threading.Tasks.Parallel.For(0, total, index =>
            {
                int g = index / floors, f = index % floors + 1;
                int seed = GuildDungeon.SeedFor("condition-study-" + g, 1);
                floorTheme[index] = GuildDungeonGenerator.ThemesFor(floors, seed)[f - 1];
                readings[index] = ReadFloor(content, population, ladderA, ladderB, studied, seed, f);
                int n = System.Threading.Interlocked.Increment(ref done);
                if (progress) Console.Error.WriteLine("  study: floor " + n + " of " + total);
            });

            Dictionary<string, Tally> tallies = new Dictionary<string, Tally>();
            for (int index = 0; index < total; index++)
            {
                int g = index / floors, f = index % floors + 1;
                for (int c = 0; c < studied.Count; c++)
                {
                    FloorReading r = readings[index][c];
                    string key = studied[c].Id + "|" + floorTheme[index];
                    Tally t;
                    if (!tallies.TryGetValue(key, out t)) tallies[key] = t = new Tally();
                    t.Floors++;
                    t.Base += r.Base; t.Cond += r.Cond; t.Bite += r.Bite; t.Shift += r.Shift;
                    t.KnobA += r.KnobA; t.KnobB += r.KnobB; t.Chaos += r.Chaos; t.Forced += r.Forced;
                    for (int k = 0; k < r.MixShift.Length; k++) t.MixShift[k] += r.MixShift[k];
                    if (r.Guard.Length > 0)
                    {
                        t.GuardFails++;
                        if (t.FirstGuardFail.Length == 0) t.FirstGuardFail = "g" + g + "f" + f + ": " + r.Guard;
                    }
                }
            }
            return tallies;
        }

        /// <summary>
        /// C5 IN THE GATE. The study is the instrument the pool was read from; this re-reads it on
        /// every build, so a content or engine change that turns a pooled condition into a knob -
        /// or breaks a guardrail on a floor it may be drawn on - fails the build rather than
        /// quietly shipping. Every condition must pass on EVERY theme its "floors" list names.
        /// Fully seeded, so the reading is the same on every run.
        /// </summary>
        private static void CheckConditionShape(ContentDatabase content)
        {
            Dictionary<string, Tally> tallies = Study(content, new List<FloorConditionDef>(content.FloorConditions), false);
            List<string> knobs = new List<string>(), guards = new List<string>(), read = new List<string>();
            foreach (FloorConditionDef c in content.FloorConditions)
            {
                foreach (GuildDungeonTheme theme in c.Themes)
                {
                    Tally t;
                    if (!tallies.TryGetValue(c.Id + "|" + theme, out t) || t.Floors == 0) { knobs.Add(c.Id + " on " + theme + " (never measured)"); continue; }
                    float ratio = RatioOf(t);
                    read.Add(c.Id + "/" + theme + " x" + F2(Math.Min(ratio, 99f)));
                    if (ratio < ShapeFactor) knobs.Add(c.Id + " on " + theme + " x" + F2(ratio));
                    if (t.GuardFails > 0) guards.Add(c.Id + " on " + theme + ": " + t.FirstGuardFail);
                }
            }
            Line("    C5 readings (SHIFT over the larger of two matched knobs and chance): " + string.Join(", ", read.ToArray()));
            Check(knobs.Count == 0, "C5: every condition reorders the rosters by at least x" + F2(ShapeFactor)
                  + " what a pure difficulty knob or chance alone does, on every floor theme it may be drawn on"
                  + (knobs.Count > 0 ? " - a knob: " + string.Join("; ", knobs.ToArray()) : ""));
            Check(guards.Count == 0, "and on the study's larger population every guardrail holds on all " + StudyGuilds
                  + " dungeons' floors of each of those themes" + (guards.Count > 0 ? " - " + string.Join("; ", guards.ToArray()) : ""));
        }
        private static string F2(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }

        // ---------------------------------------------------------------- SIZE AND ATTEMPTS
        //
        // claude/DESIGN-GUILDS-PHASE-TWO.md, "Size": scale the CONTENT to the guild, not the payout
        // per head; "Bounded": a handful of attempts, and the cap, not the reward, is what stops the
        // mode overtaking the game loop. GuildDungeonScale holds the rules; these checks hold
        // GuildDungeonScale, the generator under it and the server's copy of both.

        /// <summary>
        /// The ladder table as content: it loads clean, starts at one member on the base ladder,
        /// never gives a growing guild fewer floors, and never gives any guild fewer floors than
        /// every guild had before ladders scaled.
        /// </summary>
        private static void CheckLadderTable(ContentDatabase content)
        {
            Line("");
            Line("  SIZE - the ladder scales with the guild, the floors do not");
            GuildDungeonScaleConfig scale = content.GuildDungeonScale;
            int baseLadder = GuildDungeonScale.BaseLadder(content);
            List<string> warned = content.Warnings.FindAll(w => w.StartsWith("guild-dungeon-scale.json", StringComparison.Ordinal));
            Check(scale.IsUsable && warned.Count == 0, "guild-dungeon-scale.json loads with " + scale.Bands.Count + " ladder rows and no warning"
                  + (warned.Count > 0 ? " - " + warned[0] : ""));
            Check(GuildDungeonGenerator.BlockFloors == baseLadder,
                  "a block is " + GuildDungeonGenerator.BlockFloors + " floors (four themes and a Champion) and the base ladder is balance.json's "
                  + baseLadder + " - the client asks for a theme by ladder length, so the two must be one number");
            Check(scale.IsUsable && scale.Bands[0].Members == 1 && scale.Bands[0].Floors == baseLadder,
                  "the first row is a guild of one on the base ladder (" + (scale.IsUsable ? scale.Bands[0].Members + " member, " + scale.Bands[0].Floors + " floors" : "none") + ")");

            bool rising = true, neverShort = true;
            int previous = 0;
            List<string> table = new List<string>();
            for (int m = 1; m <= GuildRules.MemberCap; m++)
            {
                int ladder = GuildDungeonScale.LadderFor(content, m);
                if (ladder < previous) rising = false;
                if (ladder < baseLadder) neverShort = false;
                if (ladder != previous) table.Add(m + "+ members: " + ladder + " floors");
                previous = ladder;
            }
            Line("    " + string.Join("; ", table.ToArray()));
            Check(rising, "a guild that grows never has fewer floors (1 to " + GuildRules.MemberCap + " members)");
            Check(neverShort, "no guild has fewer floors than the base " + baseLadder + " - nothing was taken from a small guild");
            Check(GuildDungeonScale.LadderFor(content, GuildRules.MemberCap) == GuildDungeonScale.MaxLadder(content)
                  && GuildDungeonScale.MaxLadder(content) > baseLadder,
                  "a guild at the " + GuildRules.MemberCap + "-member cap gets the longest ladder (" + GuildDungeonScale.MaxLadder(content)
                  + "), and it is longer than the base - bigger guilds clear more");
        }

        /// <summary>
        /// PINNED: floors 1 to 5 - theme, condition, every enemy and its level - are exactly the
        /// five-floor dungeon that existed before ladders scaled, over 200 cycles. The value was
        /// taken from the generator BEFORE the block change, so a recorded replay of any live cycle
        /// still plays. If this moves, every guild's recorded attempts stop replaying.
        /// </summary>
        private static void CheckBaseBlockUnmoved(ContentDatabase content)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int c = 1; c <= 200; c++)
            {
                int seed = GuildDungeon.SeedFor("base-block-pin", c);
                GuildDungeonTheme[] themes = GuildDungeonGenerator.ThemesFor(5, seed);
                for (int f = 1; f <= 5; f++)
                {
                    StageDef s = GuildDungeonGenerator.BuildFloor(content, seed, f);
                    FloorConditionDef cond = GuildDungeonGenerator.ConditionFor(content, seed, f);
                    sb.Append(c).Append('/').Append(f).Append(':').Append(themes[f - 1]).Append(':').Append(cond != null ? cond.Id : "-").Append(':');
                    foreach (EnemySpawn e in s.Enemies) sb.Append(e.EnemyId).Append('@').Append(e.Level).Append(',');
                    sb.Append('|');
                }
            }
            uint h = 2166136261u;
            foreach (byte b in System.Text.Encoding.UTF8.GetBytes(sb.ToString())) { unchecked { h ^= b; h *= 16777619u; } }
            Check(h == PinnedBaseBlock, "floors 1 to 5 are the five-floor dungeon from before ladders scaled, enemy for enemy, over 200 cycles ("
                  + h + ") - no recorded replay moved. Content edits move this too; re-pin only together with a ContentVersion change");
        }

        /// <summary>Taken from the unchanged generator against the content in the tree at the
        /// time. A content edit that moves the floors (any hashed file the floors read) moves it,
        /// and retires replays anyway; that edit re-pins it, nothing else may.</summary>
        private const uint PinnedBaseBlock = 3526086078u;

        /// <summary>
        /// A FLOOR DOES NOT KNOW HOW LONG ITS LADDER IS. The client asks for a floor's theme with
        /// whatever floor count the server reported (GuildDungeonScreen, GuildFloorScreen), and builds
        /// the floor with BuildFloor, which takes no count at all; the two must agree on every ladder
        /// length. Plus the block shape: four different themes, then a Champion, and the power climb
        /// starting again, which is what the not-a-hero-check measurement asked for.
        /// </summary>
        private static void CheckFloorIsLadderBlind(ContentDatabase content)
        {
            BalanceConfig cfg = content.Balance;
            int max = GuildDungeonScale.MaxLadder(content);
            int block = GuildDungeonGenerator.BlockFloors;
            bool blind = true, champions = true, distinct = true, climb = true;
            int differs = 0, compared = 0;
            for (int c = 1; c <= 200; c++)
            {
                int seed = GuildDungeon.SeedFor("ladder-blind", c);
                for (int f = 1; f <= max; f++)
                {
                    GuildDungeonTheme theme = GuildDungeonGenerator.ThemeOf(seed, f);
                    for (int n = f; n <= max; n++)
                        if (GuildDungeonGenerator.ThemesFor(n, seed)[f - 1] != theme) blind = false;
                    if (GuildDungeonGenerator.TargetPowerForFloor(cfg, f) != GuildDungeonGenerator.TargetPowerForFloor(cfg, GuildDungeonGenerator.PositionInBlock(f)))
                        climb = false;
                    if (GuildDungeonGenerator.PositionInBlock(f) == block && theme != GuildDungeonTheme.Champion) champions = false;
                    if (f > block)
                    {
                        compared++;
                        if (Describe(GuildDungeonGenerator.BuildFloor(content, seed, f)) != Describe(GuildDungeonGenerator.BuildFloor(content, seed, f - block)))
                            differs++;
                    }
                }
                for (int start = 1; start + block - 1 <= max; start += block)
                {
                    HashSet<GuildDungeonTheme> open = new HashSet<GuildDungeonTheme>();
                    for (int f = start; f < start + block - 1; f++) open.Add(GuildDungeonGenerator.ThemeOf(seed, f));
                    if (open.Count != block - 1 || open.Contains(GuildDungeonTheme.Champion)) distinct = false;
                }
            }
            Check(blind, "a floor's theme is the same on every ladder long enough to hold it, floors 1 to " + max + " over 200 cycles - the screens and BuildFloor cannot disagree");
            Check(champions && distinct, "every block of " + block + " is four different themes and then a Champion");
            Check(climb, "the power climb starts again each block: floor " + (block + 1) + " is floor 1's target, floor " + (2 * block) + " floor " + block + "'s");
            float share = compared == 0 ? 0f : (float)differs / compared;
            Check(compared > 0 && share >= 0.9f, "and a deep floor is a new fight, not a copy: " + Pct(share) + " of floors past the first block differ from the floor one block up");
        }

        /// <summary>
        /// THE BOUNDS, on GuildDungeonScale's own functions. Three a floor; fifteen a cycle per
        /// account whatever the ladder, so a member of a big guild has more floors to spend the
        /// same attempts on, never more attempts; the server's count wins over the rows when a member
        /// changed guild mid-cycle; and nothing past the ladder or before the guild opens it.
        /// </summary>
        private static void CheckAttemptBounds(ContentDatabase content)
        {
            Line("");
            Line("  ATTEMPTS - bounded the same at every guild size");
            BalanceConfig cfg = content.Balance;
            int perFloor = cfg.GuildDungeonAttemptsPerFloor;
            int cap = GuildDungeonScale.CycleAttemptCap(cfg);
            int baseLadder = GuildDungeonScale.BaseLadder(content);
            int max = GuildDungeonScale.MaxLadder(content);
            const string me = "member-a", mate = "member-b";
            const int cycle = 50;

            Check(cap == perFloor * baseLadder, "the cycle cap is " + cap + ": three a floor times the BASE ladder, derived from balance.json, not typed");
            Check(cap <= perFloor * GuildDungeonScale.LadderFor(content, 1) && cap < perFloor * max,
                  "so a guild of one loses nothing it had (" + perFloor * GuildDungeonScale.LadderFor(content, 1) + " on its ladder) and a guild of "
                  + GuildRules.MemberCap + " gains floors, not attempts (" + cap + " of the " + perFloor * max + " its ladder would allow at three a floor)");

            List<GuildDungeonAttempt> rows = new List<GuildDungeonAttempt>();
            string why;
            Check(GuildDungeonScale.CanAttempt(content, rows, me, cycle, 1, max, -1, out why), "floor 1 is open to anybody at the start of a cycle");
            Check(!GuildDungeonScale.CanAttempt(content, rows, me, cycle, 2, max, -1, out why) && why.Contains("opens when"),
                  "floor 2 is not, until somebody clears floor 1 (\"" + why + "\")");
            for (int i = 1; i <= perFloor; i++) rows.Add(Row(me, cycle, 1, i, i == perFloor));
            Check(!GuildDungeonScale.CanAttempt(content, rows, me, cycle, 1, max, -1, out why) && why.Contains("floor 1"),
                  "a fourth attempt at floor 1 is refused (\"" + why + "\")");
            Check(GuildDungeonScale.CanAttempt(content, rows, me, cycle, 2, max, -1, out why) && GuildDungeonScale.CanAttempt(content, rows, mate, cycle, 2, max, -1, out why),
                  "and floor 2 is now open for both members - the guild pushes together");
            Check(GuildDungeonScale.AttemptsLeft(cfg, rows, me, cycle, 1, -1) == 0 && GuildDungeonScale.AttemptsLeft(cfg, rows, me, cycle, 2, -1) == perFloor
                  && GuildDungeonScale.AttemptsLeft(cfg, rows, mate, cycle, 1, -1) == perFloor,
                  "attempts left: none on floor 1, all three on floor 2, and the guildmate's own three untouched");

            // Spend the whole cycle: three on each base floor, every one a clear so the next opens.
            rows.Clear();
            for (int f = 1; f <= baseLadder; f++)
                for (int i = 1; i <= perFloor; i++) rows.Add(Row(me, cycle, f, i, true));
            Check(GuildDungeonScale.CycleAttemptsUsed(rows, me, cycle) == cap
                  && !GuildDungeonScale.CanAttempt(content, rows, me, cycle, baseLadder + 1, max, -1, out why) && why.Contains("this cycle ("),
                  "after " + cap + " attempts a member of a " + max + "-floor guild cannot start floor " + (baseLadder + 1) + ", open as it is (\"" + why + "\")");
            Check(GuildDungeonScale.CanAttempt(content, rows, mate, cycle, baseLadder + 1, max, -1, out why),
                  "while a guildmate with attempts left can - the deep floors are reached by the guild, not by one member's week");
            Check(GuildDungeonScale.AttemptsLeft(cfg, rows, me, cycle, baseLadder + 1, -1) == 0,
                  "and attempts left reads the cycle's remainder, not only the floor's");

            Check(!GuildDungeonScale.CanAttempt(content, rows, mate, cycle, baseLadder + 1, baseLadder, -1, out why) && why.Contains(baseLadder + " floors"),
                  "a floor past this guild's ladder is refused even when the floor below is cleared (\"" + why + "\")");
            Check(!GuildDungeonScale.CanAttempt(content, rows, mate, cycle, max + 1, max, -1, out why) && !GuildDungeonScale.CanAttempt(content, rows, mate, cycle, 0, max, -1, out why),
                  "floor 0 and floor " + (max + 1) + " exist for nobody");

            List<GuildDungeonAttempt> fresh = new List<GuildDungeonAttempt>();
            Check(!GuildDungeonScale.CanAttempt(content, fresh, me, cycle, 1, max, cap, out why) && GuildDungeonScale.AttemptsLeft(cfg, fresh, me, cycle, 1, cap) == 0,
                  "THE SERVER'S COUNT WINS: a member who spent the cycle in another guild has no rows here and still no attempts (\"" + why + "\")");
            Check(GuildDungeonScale.AttemptsLeft(cfg, fresh, me, cycle, 1, cap - 1) == 1 && GuildDungeonScale.AttemptsLeft(cfg, fresh, me, cycle + 1, 1, -1) == perFloor,
                  "one left at " + (cap - 1) + " of " + cap + ", and a new cycle is a new budget");
        }

        private static GuildDungeonAttempt Row(string user, int cycle, int floor, int attempt, bool victory)
        {
            GuildDungeonAttempt a = new GuildDungeonAttempt();
            a.UserId = user; a.Cycle = cycle; a.Floor = floor; a.Attempt = attempt; a.Victory = victory;
            a.Fallen = victory ? 0 : 1; a.Id = ++_madeId;
            return a;
        }

        /// <summary>Dungeons per block CheckDeepFloorsMatchBase measures: 100, so 500 floors a block,
        /// about 40 seconds of fights. Enough to put each position's mean within a couple of points
        /// of the 600-dungeon reading.</summary>
        private const int BlockCompareGuilds = 100;

        /// <summary>How far a deep floor's mean clear share for 3-star-or-fewer rosters may sit under
        /// the base floor at the same position in its block. Chosen: a tenth of the rosters.</summary>
        private const float BlockShareTolerance = 0.10f;

        /// <summary>
        /// A DEEP FLOOR IS NO HARDER THAN THE BASE FLOOR IT MIRRORS, measured over 100 dungeons a block.
        ///
        /// Why the group guardrail is not asserted per deep floor the way it is per base floor: the
        /// base block itself does not hold it everywhere. Measured when ladders were added, over
        /// three seed families of 600 dungeons each, the base floors broke "25% of 3-star-or-fewer
        /// rosters clear" on 155 of 9,000 floors (1.7%), almost all Champion floors, and the deep
        /// floors on 181 of 9,000 (2.0%); one family read the deep block lower than the base. The
        /// six fixed dungeons of CheckNotAHeroCheck never happen to draw a base breach. Asserting
        /// the guardrail per floor across ten floors only re-samples that existing margin and would
        /// block the ladder on a property of floors 1 to 5. The margin is reported here every run
        /// as a FINDING for the owner, because closing it moves a hashed balance number and retires
        /// every replay.
        ///
        /// Breach COUNTS are printed, not compared. Every floor is fought by the same twelve
        /// 3-star-or-fewer sample lines, so breaches are correlated across floors and a count
        /// swings far more than a binomial would: one family read 44 base against 73 deep at 3,000
        /// floors each, another 59 against 49. A count comparison would go red on noise.
        ///
        /// What IS asserted: no deep floor breaks the all-rosters guardrail; and at every position
        /// in a block, the deep floors' MEAN clear share for 3-star-or-fewer rosters is within
        /// BlockShareTolerance of the base floor's - the same difficulty band, read on the
        /// statistic that does not swing.
        /// </summary>
        private static void CheckDeepFloorsMatchBase(ContentDatabase content)
        {
            int block = GuildDungeonGenerator.BlockFloors;
            int max = GuildDungeonScale.MaxLadder(content);
            int blocks = (max + block - 1) / block;
            if (blocks < 2) { Check(true, "the longest ladder is one block, so there is no deep floor to compare"); return; }
            Line("");
            Line("  DEEP FLOORS AGAINST THE BASE - " + BlockCompareGuilds + " dungeons, every floor of the longest ladder");
            List<Sample> samples = SampleRosters(content, 0x6D1C);
            int[] groupBreaks = new int[blocks], allBreaks = new int[blocks], floorsIn = new int[blocks];
            double[,] lowShare = new double[blocks, block];
            int[,] lowCount = new int[blocks, block];
            for (int g = 0; g < BlockCompareGuilds; g++)
            {
                int seed = GuildDungeon.SeedFor("block-compare-" + g, 1);
                for (int f = 1; f <= max; f++)
                {
                    int b = GuildDungeonGenerator.BlockOf(f), p = GuildDungeonGenerator.PositionInBlock(f) - 1;
                    FloorConditionDef condition = GuildDungeon.ConditionOf(content, seed, f);
                    List<Sample> lines = ConformAll(content, samples, condition, 0x6D1C);
                    Dictionary<string, int[]> byGroup = new Dictionary<string, int[]>();
                    int clears = 0;
                    for (int s = 0; s < lines.Count; s++)
                    {
                        int[] t;
                        if (!byGroup.TryGetValue(lines[s].Group, out t)) byGroup[lines[s].Group] = t = new int[2];
                        t[1]++;
                        if (GuildDungeon.Fight(content, lines[s].Team, seed, f).Victory) { t[0]++; clears++; }
                    }
                    float worst = 1f;
                    foreach (KeyValuePair<string, int[]> kv in byGroup) worst = Math.Min(worst, (float)kv.Value[0] / kv.Value[1]);
                    int[] low;
                    if (byGroup.TryGetValue("3 stars or fewer", out low)) { lowShare[b, p] += (double)low[0] / low[1]; lowCount[b, p]++; }
                    floorsIn[b]++;
                    if (worst < MinGroupClearShare) groupBreaks[b]++;
                    if ((float)clears / lines.Count < MinClearShare) allBreaks[b]++;
                }
            }

            int n = floorsIn[0];
            Line("    ! FINDING (pre-existing, base floors 1 to " + block + "): the 3-stars-or-fewer guardrail breaks on " + groupBreaks[0] + " of " + n
                 + " base floors over " + BlockCompareGuilds + " dungeons. Closing it needs a hashed balance change (owner decision, DESIGN-BUILD-ORDER.md).");
            bool allOk = true, shareOk = true;
            List<string> detail = new List<string>();
            for (int b = 0; b < blocks; b++)
            {
                List<string> pos = new List<string>();
                for (int p = 0; p < block; p++)
                {
                    if (lowCount[b, p] == 0) continue;
                    double mean = lowShare[b, p] / lowCount[b, p];
                    double baseMean = lowShare[0, p] / Math.Max(1, lowCount[0, p]);
                    pos.Add(Pct((float)mean));
                    if (b > 0 && mean < baseMean - BlockShareTolerance) { shareOk = false; detail.Add("block " + b + " position " + (p + 1) + " " + Pct((float)mean) + " against " + Pct((float)baseMean)); }
                }
                Line("    block " + b + " (floors " + (b * block + 1) + " to " + Math.Min(max, (b + 1) * block) + "): group guardrail broken on " + groupBreaks[b]
                     + " of " + floorsIn[b] + " (printed, not compared), all-rosters on " + allBreaks[b] + "; 3-stars-or-fewer clear by position " + string.Join(" ", pos.ToArray()));
                if (b > 0 && allBreaks[b] > 0) allOk = false;
            }
            Check(allOk, "no deep floor is cleared by fewer than " + Pct(MinClearShare) + " of all sample rosters");
            Check(shareOk, "at every position in a block, deep floors are within " + Pct(BlockShareTolerance) + " of the base floor for 3-stars-or-fewer rosters - more floors, not harder ones"
                  + (detail.Count > 0 ? " - " + string.Join("; ", detail.ToArray()) : ""));
        }

        /// <summary>Guilds of each size simulated by CheckSizeReach.</summary>
        private const int SizeGuilds = 8;

        /// <summary>The share of simulated guilds of every size that must reach the end of their
        /// own ladder inside their attempt budget. Chosen before the first reading, like the
        /// not-a-hero-check shares: a ladder most guilds of its size cannot finish is content
        /// scaled to a guild that does not exist.</summary>
        private const float MinLadderFinishShare = 0.75f;

        private static readonly int[] SizesSimulated = { 1, 2, 5, 9, 10, 15, 20, 25, 30 };

        /// <summary>
        /// THE COLLECTIVE PUSH, FOUGHT. Guilds of 1 to 30, each member holding three lines drawn
        /// from the not-a-hero-check spread (random, missing a role family, 3 stars or fewer), made
        /// legal under each floor's condition the way a player would. Floor by floor, members try
        /// their lines in turn through GuildDungeonScale.CanAttempt, so the per-floor and cycle caps
        /// are the real functions; a floor that nobody can clear ends the guild's week. Every fight
        /// is GuildDungeon.Fight, so CombatSimulation.Run.
        ///
        /// Asserted: every size finishes its own ladder in at least MinLadderFinishShare of its
        /// guilds (the content is scaled to a guild that can clear it); bigger guilds clear more
        /// floors; and no simulated member ever passes a cap.
        /// </summary>
        private static void CheckSizeReach(ContentDatabase content)
        {
            Line("");
            Line("  SIZE, FOUGHT - " + SizeGuilds + " guilds of each size push their own ladder with lines from the roster spread");
            BalanceConfig cfg = content.Balance;
            int cap = GuildDungeonScale.CycleAttemptCap(cfg);
            List<Sample> spread = SampleRosters(content, 0x512E);
            Dictionary<string, bool> fought = new Dictionary<string, bool>();
            const int cycle = 9;

            Line("    " + "size".PadRight(6) + "ladder".PadRight(8) + "finished".PadRight(10) + "floors cleared, mean".PadRight(22)
                 + "attempts per member, mean / max");
            bool everyFinishes = true, capsHeld = true;
            List<string> short_ = new List<string>();
            float smallestMean = -1f, largestMean = -1f;
            for (int si = 0; si < SizesSimulated.Length; si++)
            {
                int size = SizesSimulated[si];
                int ladder = GuildDungeonScale.LadderFor(content, size);
                int finished = 0, clearedSum = 0, attemptSum = 0, attemptMax = 0, members = 0;
                for (int g = 0; g < SizeGuilds; g++)
                {
                    string guild = "size-" + size + "-" + g;
                    int seed = GuildDungeon.SeedFor(guild, cycle);
                    DeterministicRng rng = new DeterministicRng(GuildDungeonGenerator.Mix(seed, size));
                    List<List<Sample>> lines = new List<List<Sample>>();
                    for (int m = 0; m < size; m++)
                    {
                        List<Sample> own = new List<Sample>();
                        for (int k = 0; k < cfg.GuildDungeonAttemptsPerFloor; k++) own.Add(spread[rng.Range(0, spread.Count)]);
                        lines.Add(own);
                    }

                    List<GuildDungeonAttempt> rows = new List<GuildDungeonAttempt>();
                    int cleared = 0;
                    for (int f = 1; f <= ladder; f++)
                    {
                        FloorConditionDef condition = GuildDungeon.ConditionOf(content, seed, f);
                        bool won = false;
                        for (int m = 0; m < size && !won; m++)
                        {
                            string user = guild + "-m" + m;
                            List<Sample> legal = ConformAll(content, lines[m], condition, seed ^ f ^ (m << 8));
                            for (int k = 0; k < legal.Count && !won; k++)
                            {
                                string why;
                                if (!GuildDungeonScale.CanAttempt(content, rows, user, cycle, f, ladder, -1, out why)) break;
                                string key = seed + "/" + f + "/" + Describe(legal[k].Team);
                                bool victory;
                                if (!fought.TryGetValue(key, out victory))
                                    fought[key] = victory = GuildDungeon.Fight(content, legal[k].Team, seed, f).Victory;
                                rows.Add(Row(user, cycle, f, GuildDungeon.AttemptsUsed(rows, user, cycle, f) + 1, victory));
                                won = victory;
                            }
                        }
                        if (!won) break;
                        cleared = f;
                    }
                    if (cleared == ladder) finished++;
                    clearedSum += cleared;
                    for (int m = 0; m < size; m++)
                    {
                        string user = guild + "-m" + m;
                        int used = GuildDungeonScale.CycleAttemptsUsed(rows, user, cycle);
                        for (int f = 1; f <= ladder; f++) if (GuildDungeon.AttemptsUsed(rows, user, cycle, f) > cfg.GuildDungeonAttemptsPerFloor) capsHeld = false;
                        if (used > cap) capsHeld = false;
                        attemptSum += used; attemptMax = Math.Max(attemptMax, used); members++;
                    }
                }
                float finishShare = (float)finished / SizeGuilds;
                float meanCleared = (float)clearedSum / SizeGuilds;
                if (si == 0) smallestMean = meanCleared;
                if (si == SizesSimulated.Length - 1) largestMean = meanCleared;
                if (finishShare < MinLadderFinishShare) { everyFinishes = false; short_.Add(size + " members: " + Pct(finishShare) + " of guilds finish " + ladder); }
                Line("    " + size.ToString().PadRight(6) + ladder.ToString().PadRight(8) + (finished + "/" + SizeGuilds).PadRight(10)
                     + meanCleared.ToString("0.0", CultureInfo.InvariantCulture).PadRight(22)
                     + ((float)attemptSum / members).ToString("0.0", CultureInfo.InvariantCulture) + " / " + attemptMax);
            }
            Check(everyFinishes, "every guild size finishes its own ladder in at least " + Pct(MinLadderFinishShare) + " of its guilds - the content fits the guild"
                  + (short_.Count > 0 ? " - SHORT: " + string.Join("; ", short_.ToArray()) : ""));
            Check(largestMean > smallestMean, "bigger guilds clear more floors (" + largestMean.ToString("0.0", CultureInfo.InvariantCulture) + " at "
                  + SizesSimulated[SizesSimulated.Length - 1] + " members against " + smallestMean.ToString("0.0", CultureInfo.InvariantCulture) + " at " + SizesSimulated[0] + ")");
            Check(capsHeld, "and no simulated member passed three on a floor or " + cap + " in the cycle");
        }

        /// <summary>
        /// THE REWARD IS A PLACEHOLDER AND SAYS SO. While GuildDungeonRewards.IsPlaceholder is up,
        /// every floor on every ladder is worth nothing, marked as undecided. Whatever is decided,
        /// it must stay a pure function with no seed (nothing random) and nothing a player can buy
        /// may name the dungeon (nothing bought), and the Waymark pay must stay blind to ladders.
        /// </summary>
        private static void CheckRewardPlaceholder(ContentDatabase content)
        {
            Line("");
            Line("  REWARDS - a placeholder until the economy decision lands");
            int max = GuildDungeonScale.MaxLadder(content);
            bool empty = true, pure = true;
            for (int ladder = GuildDungeonScale.BaseLadder(content); ladder <= max; ladder++)
                for (int f = 1; f <= ladder; f++)
                {
                    GuildDungeonFloorReward a = GuildDungeonRewards.ForFirstClear(content.Balance, f, ladder);
                    GuildDungeonFloorReward b = GuildDungeonRewards.ForFirstClear(content.Balance, f, ladder);
                    if (!a.Placeholder || a.Amount != 0 || a.Currency.Length != 0 || !a.Note.StartsWith("PLACEHOLDER", StringComparison.Ordinal)) empty = false;
                    if (a.Placeholder != b.Placeholder || a.Amount != b.Amount || a.Currency != b.Currency || a.Note != b.Note) pure = false;
                }
            Check(GuildDungeonRewards.IsPlaceholder, "GuildDungeonRewards is still the placeholder - the economy decision for deep floors is open (DESIGN-BUILD-ORDER.md)");
            Check(!GuildDungeonRewards.IsPlaceholder || empty,
                  "and while it is, every floor of every ladder pays nothing and is marked PLACEHOLDER - no number ships before the decision");
            Check(pure, "a floor's reward is the same every time it is asked for");

            System.Reflection.MethodInfo method = typeof(GuildDungeonRewards).GetMethod("ForFirstClear");
            List<string> forbidden = new List<string>();
            foreach (System.Reflection.ParameterInfo p in method.GetParameters())
            {
                string n = p.Name.ToLowerInvariant();
                if (n.Contains("seed") || n.Contains("rng") || n.Contains("roll") || n.Contains("member") || p.ParameterType == typeof(DeterministicRng))
                    forbidden.Add(p.Name);
            }
            Check(forbidden.Count == 0, "NOTHING RANDOM, NOTHING BY SIZE: the reward takes no seed, no roll and no member count, only the floor and the ladder"
                  + (forbidden.Count > 0 ? " - takes: " + string.Join(", ", forbidden.ToArray()) : ""));

            Type[] bought = { typeof(GrantKind), typeof(StoreOfferKind), typeof(SinkKind), typeof(PassRewardKind), typeof(EventRewardKind) };
            List<string> named = new List<string>();
            foreach (Type t in bought)
                foreach (string v in Enum.GetNames(t))
                    if (Regex.IsMatch(v, "Guild|Dungeon|Waymark|Attempt", RegexOptions.IgnoreCase)) named.Add(t.Name + "." + v);
            Check(named.Count == 0, "NOTHING BOUGHT: no store, shop, pass or event grant kind names the guild dungeon, its attempts or Waymarks, so the caps cannot be bought past"
                  + (named.Count > 0 ? " - " + string.Join(", ", named.ToArray()) : ""));
            Check(content.GuildPath.BoardFloor == 1,
                  "the Waymark pay is blind to ladders: only floor " + content.GuildPath.BoardFloor + "'s boards pay, so a longer ladder is more floors to clear and no more pay per member");
        }

        /// <summary>
        /// The size rules live twice, here and in 0022, and neither can read the other at runtime.
        /// The harness reads both and refuses a disagreement: the ladder table band for band, the
        /// longest ladder, the cycle cap's derivation, and the shape of the rules the server holds.
        /// </summary>
        private static void CheckServerAgreesOnSize(ContentDatabase content, string contentDir)
        {
            string path = FindMigration(contentDir, "0022_guild_dungeon_size.sql");
            if (path == null)
            {
                Check(false, "Server/supabase/migrations/0022_guild_dungeon_size.sql is findable from the content folder");
                return;
            }
            string sql = File.ReadAllText(path);
            int max = SqlConstant(sql, "guild_dungeon_floor_max");
            Check(max == GuildDungeonScale.MaxLadder(content),
                  "the server's longest ladder (" + max + ", 0022) is guild-dungeon-scale.json's (" + GuildDungeonScale.MaxLadder(content) + ")");

            Match body = Regex.Match(sql, @"function\s+public\.guild_dungeon_floors_for\(p_members integer\)[\s\S]*?\$\$([\s\S]*?)\$\$", RegexOptions.IgnoreCase);
            List<string> server = new List<string>(), client = new List<string>();
            int fallback = -1;
            if (body.Success)
            {
                foreach (Match w in Regex.Matches(body.Groups[1].Value, @"when\s+p_members\s*>=\s*(\d+)\s+then\s+(\d+)", RegexOptions.IgnoreCase))
                    server.Add(w.Groups[1].Value + ":" + w.Groups[2].Value);
                Match e = Regex.Match(body.Groups[1].Value, @"else\s+(\d+)", RegexOptions.IgnoreCase);
                if (e.Success) fallback = int.Parse(e.Groups[1].Value, CultureInfo.InvariantCulture);
            }
            server.Sort(StringComparer.Ordinal);
            GuildDungeonScaleConfig scale = content.GuildDungeonScale;
            for (int i = 0; i < scale.Bands.Count; i++)
                if (scale.Bands[i].Members > 1) client.Add(scale.Bands[i].Members + ":" + scale.Bands[i].Floors);
            client.Sort(StringComparer.Ordinal);
            Check(body.Success && string.Join(",", server.ToArray()) == string.Join(",", client.ToArray())
                  && fallback == GuildDungeonScale.LadderFor(content, 1),
                  "guild_dungeon_floors_for holds the same bands as guild-dungeon-scale.json (server " + string.Join(",", server.ToArray())
                  + " else " + fallback + "; client " + string.Join(",", client.ToArray()) + " else " + GuildDungeonScale.LadderFor(content, 1) + ")");

            Check(Regex.IsMatch(sql, @"function\s+public\.guild_dungeon_cycle_attempt_cap\(\)[\s\S]*?\$\$\s*select\s+public\.guild_dungeon_attempt_cap\(\)\s*\*\s*public\.guild_dungeon_floor_count\(\)\s*;\s*\$\$", RegexOptions.IgnoreCase),
                  "the server's cycle cap is derived the same way: guild_dungeon_attempt_cap() * guild_dungeon_floor_count(), the per-floor cap times the base ladder");

            string stripped = Regex.Replace(sql, @"--[^\n]*", "");
            Check(Regex.IsMatch(stripped, @"alter table public\.guild_dungeon_cycles enable row level security", RegexOptions.IgnoreCase)
                  && Regex.IsMatch(stripped, @"revoke all on public\.guild_dungeon_cycles from anon, authenticated", RegexOptions.IgnoreCase)
                  && Regex.IsMatch(stripped, @"using \(guild_id = public\.my_guild\(\)\)", RegexOptions.IgnoreCase)
                  && !Regex.IsMatch(stripped, @"grant [^;]*\b(insert|update|delete|all)\b[^;]* on public\.guild_dungeon_cycles", RegexOptions.IgnoreCase),
                  "guild_dungeon_cycles has RLS on, revokes both client roles, is read by members of its own guild only, and grants no client write");
            Check(Regex.IsMatch(stripped, @"check \(floor between 1 and public\.guild_dungeon_floor_max\(\)\)", RegexOptions.IgnoreCase),
                  "the attempts table's floor constraint is widened to the longest ladder, not removed");
            Match submit = Regex.Match(stripped, @"function public\.guild_dungeon_submit\([\s\S]*?\$\$([\s\S]*?)\$\$", RegexOptions.IgnoreCase);
            Check(submit.Success && submit.Groups[1].Value.Contains("public.guild_dungeon_cycle_floors(mine, now_cycle)")
                  && submit.Groups[1].Value.Contains("public.guild_dungeon_cycle_attempt_cap()")
                  && submit.Groups[1].Value.Contains("public.guild_dungeon_seed_for(mine, now_cycle)")
                  && submit.Groups[1].Value.Contains("public.guild_dungeon_attempt_cap()"),
                  "guild_dungeon_submit holds the ladder, the cycle cap, the per-floor cap and 0014's derived seed");
        }

        // ---------------------------------------------------------------- output

        private static string Pct(float v) { return (v * 100f).ToString("0", CultureInfo.InvariantCulture) + "%"; }

        private static void Line(string text) { Console.WriteLine(text); }

        private static void Check(bool condition, string label)
        {
            if (condition) { Line("  [ok]   " + label); return; }
            _failures++;
            Line("  [FAIL] " + label);
        }
    }
}
