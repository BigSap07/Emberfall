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
            for (int cycle = 1; cycle <= CompositionCycles; cycle++)
            {
                int s = GuildDungeon.SeedFor("floors-check", cycle);
                for (int f = 1; f <= floors; f++)
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
            string over = " - over " + built + " floors, " + CompositionCycles + " dungeons of " + floors;
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
            return ContentDatabase.Load(ReadContent(dir, "heroes"), ReadContent(dir, "enemies"), ReadContent(dir, "campaign"),
                                        balance, ReadContent(dir, "relics"), ReadContent(dir, "events"), ReadContent(dir, "armor"));
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
            a.Team = team; a.Cycle = 7; a.Floor = 2; a.Id = ++_madeId;
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

            List<Sample> pool = SampleRosters(content, 0x7A11);
            TeamSnapshot miraLine = pool[3].Team;
            TeamSnapshot tomasLine = pool.Find(s => s.Group == "3 stars or fewer").Team;
            tomasLine.Units.RemoveAt(tomasLine.Units.Count - 1);

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
            Check(Met(content, tomasAttempt).Contains("FourOrFewer") == tomasAttempt.Victory
                  && Met(content, tomasAttempt).Contains("NoFiveStar") == tomasAttempt.Victory,
                  "Tomas's four-hero, three-star line enters FOUR OR FEWER and NO FIVE STARS exactly when it clears");

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
