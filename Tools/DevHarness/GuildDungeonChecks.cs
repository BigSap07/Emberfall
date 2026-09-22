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
        /// The level every sample roster fights at. It stands in for "a guild member", which is a
        /// guess until guilds have members: roughly a player a few chapters into the campaign, with
        /// the hero level cap at heroLevelCapBase + 4 per player level. The floors are calibrated
        /// against it, so moving it without re-reading the table this prints moves every threshold.
        ///
        /// Sample rosters carry NO relics, armor or talents. A real line is stronger than these, so
        /// a floor these rosters can clear is a floor a real guild can clear; the check errs toward
        /// calling a floor too hard.
        /// </summary>
        internal const int ReferenceLevel = 40;

        /// <summary>Synthetic guilds the property is asserted over. The generator is what is under
        /// test, so it is asked for several dungeons, not one.</summary>
        private const int SampleGuilds = 6;

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
            Line("");
            Line("GUILD DUNGEON");
            CheckSeeds();
            CheckFloors(content);
            CheckRecordReplays(content);
            CheckBoards();
            CheckServerAgrees(content, contentDir);
            CheckNotAHeroCheck(content);
            return _failures;
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

            bool allFull = true, lastIsChampion = true, rising = true, nearTarget = true, sameTwice = true, mixed = true, noScripted = true;
            HashSet<GuildDungeonTheme> early = new HashSet<GuildDungeonTheme>();
            GuildDungeonTheme[] themes = GuildDungeonGenerator.ThemesFor(floors, seed);
            int previous = 0;
            for (int f = 1; f <= floors; f++)
            {
                StageDef a = GuildDungeonGenerator.BuildFloor(content, seed, f);
                StageDef b = GuildDungeonGenerator.BuildFloor(content, seed, f);
                if (a.Enemies.Count != cfg.MaxTeamSize) allFull = false;
                if (Describe(a) != Describe(b)) sameTwice = false;
                int power = GuildDungeonGenerator.TargetPowerForFloor(cfg, f);
                if (power <= previous) rising = false;
                previous = power;
                if (EnemyPower(content, a) < power) nearTarget = false;
                if (f < floors) early.Add(themes[f - 1]);

                HashSet<Aspect> aspects = new HashSet<Aspect>();
                for (int i = 0; i < a.Enemies.Count; i++)
                {
                    EnemyDef def = content.GetEnemy(a.Enemies[i].EnemyId);
                    if (def != null) aspects.Add(def.Aspect);
                    if (a.Enemies[i].EnemyId == "the_unmade") noScripted = false;
                }
                if (aspects.Count < 2) mixed = false;
                Line("    floor " + f + "  " + themes[f - 1].ToString().PadRight(10) + Describe(a));
            }
            lastIsChampion = themes[floors - 1] == GuildDungeonTheme.Champion;

            Check(allFull, "every floor fields a full line of " + cfg.MaxTeamSize);
            Check(sameTwice, "building a floor twice from the same seed builds the same floor");
            Check(lastIsChampion, "the last floor is the Champion floor");
            Check(early.Count == Math.Min(4, floors - 1), "the floors before it are four different themes");
            Check(rising, "the power target climbs every floor");
            Check(nearTarget, "and every floor's line reaches its target - the level is solved for it, not guessed");
            Check(mixed, "no floor is drawn from a single Aspect - an Aspect floor is a counter-pick check");
            Check(noScripted, "the prologue's scripted boss never appears - only templates a campaign stage fields are drawn");

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
            Check(record.Power == ProvingGenerator.PowerOf(content, team.ToLoadout()) && record.Power > 0,
                  "and the line's power as fought (" + record.Power + ")");
            Check(Math.Abs(record.AverageStars - GuildDungeon.AverageStars(team)) < 0.001f && record.AverageStars >= 1f,
                  "and its average star rating (" + record.AverageStars.ToString("0.00", CultureInfo.InvariantCulture) + ")");

            // Through the wire and back: the RPC's arguments, reshaped into the row the server
            // returns, read back, and replayed. What comes out must be the fight that went in.
            JsonValue args = JsonValue.Parse(GuildDungeon.WriteRpcArgs(record).ToJson(false));
            JsonValue row = JsonValue.NewObject();
            row.Set("guild_id", "3f2a9c1e-7b4d-4e8a-9c21-5d6e7f8a9b0c");
            row.Set("user_id", "0b1c2d3e-4f50-4617-8293-a4b5c6d7e8f9");
            row.Set("cycle", args.GetInt("p_cycle", -1));
            row.Set("floor", args.GetInt("p_floor", -1));
            row.Set("attempt", 1);
            row.Set("seed", args.GetInt("p_seed", -1));
            row.Set("team", args["p_team"]);
            row.Set("victory", args.GetBool("p_victory", !record.Victory));
            row.Set("turns", args.GetInt("p_turns", -1));
            row.Set("power", args.GetInt("p_power", -1));
            row.Set("avg_stars", args.GetFloat("p_avg_stars", -1f));
            GuildDungeonAttempt stored = GuildDungeon.ReadRow(JsonValue.Parse(row.ToJson(false)));

            Check(stored.Seed == record.Seed && stored.Floor == record.Floor && stored.Cycle == record.Cycle
                  && stored.Team.Units.Count == team.Units.Count,
                  "a record survives the trip to the server's row shape and back");
            BattleResult replay = GuildDungeon.Replay(content, stored);
            Check(Fingerprint(replay) == Fingerprint(live),
                  "AND REPLAYS EXACTLY - the stored record alone reproduces the fight, event for event ("
                  + live.Events.Count + " events)");
        }

        private static string Fingerprint(BattleResult r)
        {
            return r.Victory + "|" + r.Rounds + "|" + r.Events.Count + "|"
                 + r.PlayerHpRatio.ToString("0.0000", CultureInfo.InvariantCulture) + "|"
                 + r.EnemyHpRatio.ToString("0.0000", CultureInfo.InvariantCulture);
        }

        // ---------------------------------------------------------------- boards

        private static void CheckBoards()
        {
            List<GuildDungeonAttempt> attempts = new List<GuildDungeonAttempt>
            {
                Made("ana", 1, true, 9, 4000, 3.4f),
                Made("ana", 2, true, 7, 5200, 4.2f),
                Made("bo",  1, true, 12, 2600, 2.0f),
                Made("cy",  1, false, 3, 1000, 1.0f),
                Made("cy",  2, true, 8, 3100, 3.0f),
            };
            // Someone else's floor and someone else's cycle, which no board may count.
            GuildDungeonAttempt wrongFloor = Made("dee", 1, true, 1, 1, 1f); wrongFloor.Floor = 3;
            GuildDungeonAttempt wrongCycle = Made("eli", 1, true, 1, 1, 1f); wrongCycle.Cycle = 6;
            attempts.Add(wrongFloor);
            attempts.Add(wrongCycle);

            List<GuildDungeonAttempt> turns = GuildDungeon.Rank(attempts, 7, 2, GuildDungeonBoard.FewestTurns);
            List<GuildDungeonAttempt> power = GuildDungeon.Rank(attempts, 7, 2, GuildDungeonBoard.LowestPower);
            List<GuildDungeonAttempt> stars = GuildDungeon.Rank(attempts, 7, 2, GuildDungeonBoard.LowestStars);

            Check(Order(turns) == "ana:2 cy:2 bo:1", "FEWEST TURNS ranks each member's fastest clear (" + Order(turns) + ")");
            Check(Order(power) == "bo:1 cy:2 ana:1", "LOWEST POWER ranks each member's cheapest clear (" + Order(power) + ")");
            Check(Order(stars) == "bo:1 cy:2 ana:1", "LOWEST STARS ranks each member's humblest clear (" + Order(stars) + ")");
            Check(turns.Count == 3, "one entry per member, other floors and other cycles excluded");
            Check(!turns.Exists(a => !a.Victory), "a loss is recorded but never ranked - cy's 3-turn wipe is not a 3-turn clear");
            Check(turns[0].Attempt == 2 && power[2].Attempt == 1,
                  "the same member can lead one board with one attempt and sit on another with a different one");
        }

        private static GuildDungeonAttempt Made(string user, int attempt, bool victory, int turns, int power, float stars)
        {
            GuildDungeonAttempt a = new GuildDungeonAttempt();
            a.UserId = user; a.Attempt = attempt; a.Victory = victory; a.Turns = turns; a.Power = power;
            a.AverageStars = stars; a.Cycle = 7; a.Floor = 2;
            return a;
        }

        private static string Order(List<GuildDungeonAttempt> board)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < board.Count; i++) parts.Add(board[i].UserId + ":" + board[i].Attempt);
            return string.Join(" ", parts.ToArray());
        }

        // ---------------------------------------------------------------- the server's copy

        /// <summary>
        /// The attempt cap and the floor count each live twice: in balance.json, where the client
        /// reads them, and in migration 0007, where the server enforces them. Neither can read the
        /// other at runtime, so the harness reads both and refuses a disagreement.
        /// </summary>
        private static void CheckServerAgrees(ContentDatabase content, string contentDir)
        {
            string migration = FindMigration(contentDir);
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
        }

        private static int SqlConstant(string sql, string function)
        {
            Match m = Regex.Match(sql, @"function\s+public\." + function + @"\(\)[\s\S]*?\$\$\s*select\s+(\d+)\s*;\s*\$\$",
                                  RegexOptions.IgnoreCase);
            return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : -1;
        }

        private static string FindMigration(string contentDir)
        {
            DirectoryInfo dir = string.IsNullOrEmpty(contentDir) ? null : new DirectoryInfo(contentDir);
            for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, Path.Combine("Server", Path.Combine("supabase",
                                   Path.Combine("migrations", "0007_guild_dungeon.sql"))));
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
        /// </summary>
        private static void CheckNotAHeroCheck(ContentDatabase content)
        {
            Line("  NOT A HERO CHECK - every floor of " + SampleGuilds + " dungeons against a spread of rosters at level " + ReferenceLevel);
            List<Sample> samples = SampleRosters(content, 0x6D1C);
            List<string> groups = new List<string>();
            for (int i = 0; i < samples.Count; i++) if (!groups.Contains(samples[i].Group)) groups.Add(samples[i].Group);

            int floors = content.Balance.GuildDungeonFloors;
            int badShare = 0, badGroup = 0, badHero = 0, total = 0;
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
                    Dictionary<string, int[]> byGroup = new Dictionary<string, int[]>();
                    Dictionary<string, int> heroInClears = new Dictionary<string, int>();
                    int clears = 0;
                    for (int s = 0; s < samples.Count; s++)
                    {
                        int[] tally;
                        if (!byGroup.TryGetValue(samples[s].Group, out tally)) byGroup[samples[s].Group] = tally = new int[2];
                        tally[1]++;
                        if (!GuildDungeon.Fight(content, samples[s].Team, seed, f).Victory) continue;
                        tally[0]++;
                        clears++;
                        for (int u = 0; u < samples[s].Team.Units.Count; u++)
                        {
                            string id = samples[s].Team.Units[u].DefId;
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
                         + Pct(topShare) + " " + topHero);

                    string where = "guild " + g + " floor " + f + " (" + themes[f - 1] + ")";
                    if (share < MinClearShare) { badShare++; Note(firstFailures, where + ": only " + Pct(share) + " of rosters clear"); }
                    if (worstShare < MinGroupClearShare) { badGroup++; Note(firstFailures, where + ": '" + worstGroup + "' lines clear only " + Pct(worstShare)); }
                    if (clears > 0 && topShare > MaxHeroShareOfClears) { badHero++; Note(firstFailures, where + ": " + topHero + " is in " + Pct(topShare) + " of clears"); }
                }
            }

            for (int i = 0; i < firstFailures.Count; i++) Line("    ! " + firstFailures[i]);
            Check(badShare == 0, "every floor is cleared by at least " + Pct(MinClearShare) + " of all sample rosters ("
                  + (total - badShare) + " of " + total + " floors)");
            Check(badGroup == 0, "AND by at least " + Pct(MinGroupClearShare) + " of every roster missing an obvious answer - no tank, no healer, no area, no support, no single-target, 3 stars or fewer ("
                  + (total - badGroup) + " of " + total + " floors)");
            Check(badHero == 0, "and no single hero is in more than " + Pct(MaxHeroShareOfClears) + " of any floor's clears ("
                  + (total - badHero) + " of " + total + " floors)");
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
