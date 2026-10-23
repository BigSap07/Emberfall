using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Json;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;

namespace Emberfall.Core.Cloud
{
    /// <summary>
    /// The guild dungeon's boards. EACH IS A CONSTRAINT, not a measurement.
    ///
    /// The first version ranked three measurements: fewest turns, lowest power and lowest average
    /// stars. That set was broken three ways. Stars are a flat stat multiplier, so lowest power and
    /// lowest stars asked the same question twice. Fewest turns was won by whoever owned the
    /// strongest roster. And once every line fights at one level (GuildDungeon.NormaliseTeam),
    /// power stops meaning anything at all.
    ///
    /// A constraint is binary and nobody declares it: an attempt enters every board whose rule
    /// it happens to satisfy, and ranking inside a board is fewest turns. So a player with twelve
    /// heroes competes on NO FIVE STARS against a player with ninety, on the same terms, and wins
    /// it with the line they built rather than the roster they own.
    ///
    /// Appended, never inserted: the value is not stored anywhere yet, but the order is the order
    /// the boards screen draws them in and the harness pins it.
    /// </summary>
    public enum GuildDungeonBoard
    {
        /// <summary>Cleared with four heroes or fewer. An empty slot is a real decision.</summary>
        FourOrFewer,
        /// <summary>Cleared with no 5-star hero in the line.</summary>
        NoFiveStar,
        /// <summary>Cleared with every hero of one Aspect.</summary>
        OneAspect,
        /// <summary>Cleared with no Mender in the line.</summary>
        NoHealer,
        /// <summary>Cleared on the member's FIRST attempt at the floor this cycle: read the floor,
        /// commit once, win. The cap makes every attempt a decision; this board rewards the first.</summary>
        FirstAttempt,
        /// <summary>Cleared with every hero still standing at the end.</summary>
        Flawless
    }

    /// <summary>
    /// Whether a stored fight can be shown as the fight that happened.
    /// </summary>
    public enum GuildDungeonReplayVerdict
    {
        /// <summary>This build reproduces the record: same result, same turns, same losses.</summary>
        Exact,
        /// <summary>The record names no content version (it predates the field), so nothing can
        /// vouch for a replay of it.</summary>
        ContentUnknown,
        /// <summary>The record was fought under different content from this build's.</summary>
        ContentMoved,
        /// <summary>Same content, but this build produces a different fight from the record - the
        /// engine moved, or the record is not what it says it is.</summary>
        Diverged
    }

    /// <summary>What GuildDungeon.Replay hands back. Result is null unless the verdict is Exact:
    /// a replay that cannot be vouched for shows nothing rather than a fight that did not happen.</summary>
    public sealed class GuildDungeonReplay
    {
        public GuildDungeonReplayVerdict Verdict;
        public BattleResult Result;
        /// <summary>One or two plain sentences a player can read, saying why when it is not Exact.</summary>
        public string Explanation = "";

        public bool Playable { get { return Verdict == GuildDungeonReplayVerdict.Exact && Result != null; } }
    }

    /// <summary>
    /// One attempt at one floor, as the server stores it and as the client submits it.
    ///
    /// THIS RECORD IS A REPLAY. Combat is a pure function (CombatSimulation.Run), the floor is a
    /// pure function of (Seed, Floor, content) and the player side is carried whole in Team, so
    /// GuildDungeon.Replay(content, record) reproduces the fight event for event. That is the
    /// point of storing the team by value rather than a reference to anybody's save: the chat
    /// handover, a re-verification pass or a "watch how they did it" button all read this and
    /// nothing else.
    ///
    /// ContentVersion is what keeps that honest across a balance patch: the record names the
    /// content it was fought under, and a replay under anything else says so instead of playing.
    /// </summary>
    public sealed class GuildDungeonAttempt
    {
        /// <summary>The server's row id. What a chat message points at when an attempt is shared.
        /// Zero on a record not yet stored.</summary>
        public long Id;

        public string GuildId = "";
        public string UserId = "";

        /// <summary>The member's display name, read alongside the row. Not stored on it.</summary>
        public string DisplayName = "";

        public int Cycle;
        public int Floor = 1;

        /// <summary>The server's attempt number, 1 to the cap. Zero on a record not yet submitted.</summary>
        public int Attempt;

        /// <summary>The DUNGEON seed (GuildDungeon.SeedFor), not the fight seed - the fight seed is
        /// derived from it per floor, so one number describes the whole cycle.</summary>
        public int Seed;

        /// <summary>The line that fought, by value, AS IT FOUGHT: normalised to the dungeon level
        /// with no relics or armor, talents kept (GuildDungeon.NormaliseTeam). Encoded with
        /// TeamSnapshotCodec, the same shape team_snapshots.units holds.</summary>
        public TeamSnapshot Team = new TeamSnapshot();

        public bool Victory;

        /// <summary>BattleResult.Rounds - one round is every living unit acting once.</summary>
        public int Turns;

        /// <summary>Heroes of this line that died during the fight, -1 when the record predates the
        /// field. Read by the Flawless board and checked by Replay.</summary>
        public int Fallen = -1;

        /// <summary>The line's power as fought. No board ranks it any more - at one level it says
        /// little - but the server's column requires it and it costs nothing to keep.</summary>
        public int Power;

        /// <summary>Mean star rating of the units that fought, to two places. Kept for the same
        /// reason as Power; the NoFiveStar board reads the units' own stars instead.</summary>
        public float AverageStars;

        /// <summary>ContentDatabase.ContentVersion at the moment of the fight. Empty on a record
        /// stored before the field existed.</summary>
        public string ContentVersion = "";

        /// <summary>
        /// The boards this client claims the attempt entered, as guild_dungeon_attempts.boards'
        /// bits (GuildPath.BoardMask), or -1 when none was computed. Sent as p_boards and never read
        /// back: the boards screen still decides membership with Satisfies, and the server keeps
        /// only the part of the claim it can check (0011).
        /// </summary>
        public int Boards = -1;

        public string CreatedAt = "";
    }

    /// <summary>
    /// Where the guild's dungeon stands, as guild_dungeon_status() reports it. The server owns the
    /// clock: the client never computes a cycle, it asks for one.
    /// </summary>
    public sealed class GuildDungeonStatus
    {
        public string GuildId = "";
        public int Cycle;
        public long StartsUnix;
        public long EndsUnix;
        public int AttemptCap = 3;
        /// <summary>THIS GUILD'S ladder this cycle, as 0022 snapshots it from the member count when
        /// the cycle opened (GuildDungeonScale). Before 0022 the server sends the base five.</summary>
        public int FloorCount = 5;
        /// <summary>Attempts one account may make this cycle across every floor (0022). -1 when the
        /// server predates it, and then GuildDungeonScale.CycleAttemptCap is the client's prediction.</summary>
        public int CycleAttemptCap = -1;
        /// <summary>The caller's attempts this cycle by the server's count, every guild included.
        /// -1 when the server predates 0022.</summary>
        public int AttemptsUsed = -1;
        /// <summary>The member count the ladder was set from. -1 when unknown.</summary>
        public int LadderMembers = -1;

        public int Seed { get { return GuildDungeon.SeedFor(GuildId, Cycle); } }

        /// <summary>One row of the status call. Total: a malformed row reads as cycle -1, which
        /// every screen treats as "not known yet".</summary>
        public static GuildDungeonStatus Read(JsonValue row)
        {
            GuildDungeonStatus s = new GuildDungeonStatus();
            if (row != null && row.IsArray) row = row.Count > 0 ? row[0] : null;
            if (row == null || !row.IsObject) { s.Cycle = -1; return s; }
            s.GuildId = row.GetString("guild_id", "");
            s.Cycle = row.GetInt("cycle", -1);
            s.StartsUnix = GuildCodec.ParseTimestamp(row.GetString("starts_at", ""));
            s.EndsUnix = GuildCodec.ParseTimestamp(row.GetString("ends_at", ""));
            s.AttemptCap = row.GetInt("attempt_cap", s.AttemptCap);
            s.FloorCount = row.GetInt("floor_count", s.FloorCount);
            s.CycleAttemptCap = row.GetInt("cycle_attempt_cap", s.CycleAttemptCap);
            s.AttemptsUsed = row.GetInt("attempts_used", s.AttemptsUsed);
            s.LadderMembers = row.GetInt("ladder_members", s.LadderMembers);
            return s;
        }
    }

    /// <summary>One member's place on one board: the attempt that put them there and its rank.</summary>
    public sealed class GuildDungeonStanding
    {
        public GuildDungeonBoard Board;
        /// <summary>1-based. Zero when the attempt did not enter this board.</summary>
        public int Rank;
        public int Of;
        /// <summary>True when THIS attempt is the member's entry on the board, rather than an
        /// earlier, better one of theirs.</summary>
        public bool ThisAttempt;
    }

    /// <summary>
    /// The guild dungeon's rules that are not the floor generator: whose seed, which fight, what
    /// counts, and who is ahead. Engine-free and pure, like everything under Core.
    /// </summary>
    public static class GuildDungeon
    {
        /// <summary>Every board, in drawing order.</summary>
        public static readonly GuildDungeonBoard[] Boards =
        {
            GuildDungeonBoard.FourOrFewer, GuildDungeonBoard.NoFiveStar, GuildDungeonBoard.OneAspect,
            GuildDungeonBoard.NoHealer, GuildDungeonBoard.FirstAttempt, GuildDungeonBoard.Flawless
        };

        /// <summary>
        /// The seed for one guild's dungeon in one cycle. Every member of the guild computes it
        /// from two values they all hold - the guild's id and the cycle number the server reports -
        /// so every member builds the same floors without the seed ever being sent.
        ///
        /// FNV-1a over the id's canonical text, lower-cased and trimmed so a uuid that reaches the
        /// client in upper case (some JSON paths do that) still names the same dungeon. Not
        /// string.GetHashCode: that is randomised per process on some runtimes and is not a
        /// function of its input across devices, which is the one thing this must be.
        /// </summary>
        public static int SeedFor(string guildId, int cycle)
        {
            string key = (guildId ?? "").Trim().ToLowerInvariant() + ":" + cycle.ToString(CultureInfo.InvariantCulture);
            byte[] bytes = Encoding.UTF8.GetBytes(key);
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < bytes.Length; i++)
                {
                    hash ^= bytes[i];
                    hash *= 16777619u;
                }
                int seed = (int)(hash & 0x7FFFFFFF);
                return seed;
            }
        }

        /// <summary>The CombatSimulation seed for one floor. Fixed, like the floor itself: the same
        /// team on the same floor fights the same battle every time, which is what makes an attempt
        /// a decision rather than a reroll.</summary>
        public static int FightSeedFor(int dungeonSeed, int floor)
        {
            return GuildDungeonGenerator.Mix(dungeonSeed ^ 0x2F6B, floor);
        }

        // ---------------------------------------------------------------- the line

        /// <summary>
        /// The line as the dungeon fights it. EVERY hero at balance.json's guildDungeonHeroLevel,
        /// with no relic and no armor, and with its selected talent kept.
        ///
        /// LEVEL, because a guild spans every level there is. A floor tuned for level 40 was a
        /// formality at 90 and a wall at 20, so the same floor asked thirty different questions.
        /// At one level it asks one.
        ///
        /// GEAR IS STRIPPED, relics and armor both, for the same reason level is. Armor is rolled
        /// at random off campaign drops and relics are bought with dust over weeks (a mythic is
        /// "deliberately a long grind"), so both measure time spent rather than a decision made
        /// for this floor. Leaving either in would hand the floor back to whoever has played
        /// longest, which is exactly what normalising level took away. The alternatives were
        /// weighed: a standard issue kit for everyone adds nothing to decide and a second thing to
        /// tune; letting relics through as "choices" re-opens the roster-wealth gap in the one
        /// slot where a mythic hook can decide a fight. Gear-free is also exactly the line the
        /// floors were calibrated against, so no floor moves.
        ///
        /// TALENTS STAY, because a talent costs nothing to change (GameSession.SelectTalent is the
        /// whole rule), so which one a hero runs is a real choice about this floor.
        ///
        /// STARS STAY TOO, and cannot not: a hero's star rating is part of what the hero is. That
        /// is what the NO FIVE STARS board is for. Stars are re-read from the def rather than
        /// trusted from the input, so the record's stars are the stars that fought.
        ///
        /// Heroes this build does not know are dropped, duplicates are dropped, and the line is
        /// cut to MaxTeamSize. Idempotent: normalising a normalised line changes nothing, which is
        /// what lets Fight apply it to a stored record without caring whether it was applied before.
        /// </summary>
        public static TeamSnapshot NormaliseTeam(ContentDatabase content, TeamSnapshot team)
        {
            TeamSnapshot line = new TeamSnapshot();
            if (team == null || content == null) return line;
            line.Label = team.Label;
            int level = Math.Max(1, content.Balance.GuildDungeonHeroLevel);
            int size = Math.Max(1, content.Balance.MaxTeamSize);
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < team.Units.Count && line.Units.Count < size; i++)
            {
                SnapshotUnit u = team.Units[i];
                if (u == null || string.IsNullOrEmpty(u.DefId) || seen.Contains(u.DefId)) continue;
                HeroDef def = content.GetHero(u.DefId);
                if (def == null) continue;
                seen.Add(u.DefId);
                SnapshotUnit n = new SnapshotUnit();
                n.DefId = u.DefId;
                n.Level = level;
                n.Stars = def.Stars;
                n.RelicId = "";
                n.SelectedTalent = def.TalentAt(u.SelectedTalent) != null ? u.SelectedTalent : 0;
                line.Units.Add(n);
            }
            line.PowerScore = ProvingGenerator.PowerOf(content, line.ToLoadout());
            return line;
        }

        /// <summary>A dungeon line from owned heroes: the heroes and their talent picks, nothing
        /// else, since NormaliseTeam discards everything else anyway.</summary>
        public static TeamSnapshot LineOf(ContentDatabase content, IList<HeroInstance> heroes, string label)
        {
            TeamSnapshot raw = new TeamSnapshot();
            raw.Label = label ?? "";
            for (int i = 0; heroes != null && i < heroes.Count; i++)
            {
                HeroInstance h = heroes[i];
                if (h == null) continue;
                SnapshotUnit u = new SnapshotUnit();
                u.DefId = h.DefId;
                // What the hero would actually fight with. A dungeon record carries no ascension
                // tier - NormaliseTeam keeps the def, the level and the pick and nothing else - so
                // the gate is applied here, at capture, and never again. Everything downstream of
                // this line treats the recorded pick as the truth about what fought.
                u.SelectedTalent = h.LiveTalentSelection;
                raw.Units.Add(u);
            }
            return NormaliseTeam(content, raw);
        }

        // ---------------------------------------------------------------- the fight

        /// <summary>
        /// Fights one floor with one line. The live attempt and the replay both come through here,
        /// so they cannot build the player side two different ways - and both are normalised
        /// here, so a record that claims a level-90 line is still fought at the dungeon level.
        ///
        /// The player side is the PLAYER side of CombatSimulation, which resolves relics and armor
        /// through BattleRequest.Profile. A normalised line has neither, so the scratch profile it
        /// gets is empty and exists only so nothing downstream reads a null.
        /// </summary>
        public static BattleResult Fight(ContentDatabase content, TeamSnapshot team, int dungeonSeed, int floor)
        {
            // The floor's condition, from the same seed as the floor itself (C2). The fight half
            // only: the line half is checked before a line is committed (LineLegal), and a fight is
            // fought with whatever it is handed, so a replay shows what was actually sent.
            return FightUnder(content, team, dungeonSeed, floor, ConditionOf(content, dungeonSeed, floor));
        }

        /// <summary>
        /// Fights one floor under a condition NAMED BY THE CALLER rather than drawn by the seed, or
        /// under none. The live game never calls this - an attempt and a replay both go through
        /// Fight, which asks the seed - it exists so the harness can fight the same floor under every
        /// condition in the pool and measure what each one does to it.
        /// </summary>
        public static BattleResult FightUnder(ContentDatabase content, TeamSnapshot team, int dungeonSeed, int floor,
                                              FloorConditionDef condition)
        {
            return FightUnder(content, team, dungeonSeed, floor, condition, FightSeedFor(dungeonSeed, floor));
        }

        /// <summary>The same, with the combat seed named too. Harness only: the study fights an
        /// unchanged floor under a second seed to measure what chance alone does to the order of
        /// the rosters, which is the floor every condition's reading has to clear.</summary>
        public static BattleResult FightUnder(ContentDatabase content, TeamSnapshot team, int dungeonSeed, int floor,
                                              FloorConditionDef condition, int fightSeed)
        {
            TeamSnapshot line = NormaliseTeam(content, team);
            BattleRequest request = new BattleRequest();
            request.Content = content;
            request.Stage = GuildDungeonGenerator.BuildFloor(content, dungeonSeed, floor);
            request.Seed = fightSeed;
            request.Rules = condition != null ? condition.Fight : null;

            for (int i = 0; i < line.Units.Count; i++)
            {
                SnapshotUnit u = line.Units[i];
                HeroInstance hero = new HeroInstance();
                hero.Uid = "guild_dungeon_" + i;
                hero.DefId = u.DefId;
                hero.Def = content.GetHero(u.DefId);
                hero.Level = u.Level;
                hero.SelectedTalent = u.SelectedTalent;
                // THE RECORD IS THE AUTHORITY ON WHAT FOUGHT, so this scratch instance is given
                // the tier that authorises the pick it carries rather than the zero a default
                // HeroInstance has. A snapshot records a def, a level and a talent and has never
                // recorded an ascension tier, so re-deriving the gate here could only invent one -
                // and a record fought yesterday with talent III would then replay without it,
                // which is precisely the silent re-fight a replay may never be.
                //
                // Ascension is invisible to the fight in every other way: StatMath.ForHero builds
                // the stat line from Level alone, and this line is already normalised to the
                // dungeon's level. So the field is doing exactly one job here and no other.
                //
                // The gate is not skipped, it is applied earlier: LineOf records
                // HeroInstance.LiveTalentSelection, so a pick the owner's ascension has not
                // unlocked never enters a record and there is nothing here for it to replay.
                hero.Ascension = HeroInstance.TierUnlockingTalent(u.SelectedTalent);
                request.Team.Add(hero);
            }
            request.Profile = new PlayerProfile();
            return CombatSimulation.Run(request);
        }

        /// <summary>Player heroes that died in this fight. Read off the event list, never
        /// recomputed: a Death event naming a player unit is one fallen hero.</summary>
        public static int FallenIn(BattleResult result)
        {
            if (result == null) return 0;
            int fallen = 0;
            for (int i = 0; i < result.Events.Count; i++)
            {
                BattleEvent e = result.Events[i];
                if (e.Type != BattleEventType.Death) continue;
                UnitSnapshot unit = result.FindUnit(e.TargetId);
                if (unit != null && unit.IsPlayer) fallen++;
            }
            return fallen;
        }

        /// <summary>
        /// Fights a floor and scores it into a record ready to submit. Attempt, UserId, GuildId,
        /// Id and CreatedAt are the server's to fill.
        /// </summary>
        public static GuildDungeonAttempt Attempt(ContentDatabase content, TeamSnapshot team, int dungeonSeed,
                                                  int cycle, int floor, out BattleResult result)
        {
            TeamSnapshot line = NormaliseTeam(content, team);
            result = Fight(content, line, dungeonSeed, floor);
            GuildDungeonAttempt record = new GuildDungeonAttempt();
            record.Cycle = cycle;
            record.Floor = floor;
            record.Seed = dungeonSeed;
            record.Team = line;
            record.Victory = result.Victory;
            record.Turns = result.Rounds;
            record.Fallen = FallenIn(result);
            record.Power = line.PowerScore;
            record.AverageStars = AverageStars(line);
            record.ContentVersion = content.ContentVersion ?? "";
            return record;
        }

        /// <summary>
        /// Plays back what a record describes, IF this build can vouch that what it plays is what
        /// happened.
        ///
        /// Two gates, in order. The first is the content version: a record fought under other
        /// numbers is refused before a single event is computed, and says so. The second is the
        /// fight itself: under the same content, this build must reproduce the record's result,
        /// turn count and losses, or the replay is refused as Diverged. The second gate is what
        /// catches the thing a content version cannot see - an engine change between builds - and
        /// it is why a replay is never shown on the strength of the version check alone.
        /// </summary>
        public static GuildDungeonReplay Replay(ContentDatabase content, GuildDungeonAttempt record)
        {
            GuildDungeonReplay replay = new GuildDungeonReplay();
            if (record == null || content == null)
            {
                replay.Verdict = GuildDungeonReplayVerdict.Diverged;
                replay.Explanation = "There is no fight to show.";
                return replay;
            }
            if (string.IsNullOrEmpty(record.ContentVersion))
            {
                replay.Verdict = GuildDungeonReplayVerdict.ContentUnknown;
                replay.Explanation = "This attempt was recorded before fights carried a content version, "
                                   + "so this game cannot promise to show the fight that happened.";
                return replay;
            }
            if (record.ContentVersion != content.ContentVersion)
            {
                replay.Verdict = GuildDungeonReplayVerdict.ContentMoved;
                replay.Explanation = "This fight was recorded under an earlier set of game numbers. They "
                                   + "have changed since, so it cannot be shown as it happened.";
                return replay;
            }

            BattleResult result = Fight(content, record.Team, record.Seed, record.Floor);
            int fallen = FallenIn(result);
            bool agrees = result.Victory == record.Victory && result.Rounds == record.Turns
                          && (record.Fallen < 0 || fallen == record.Fallen);
            if (!agrees)
            {
                replay.Verdict = GuildDungeonReplayVerdict.Diverged;
                replay.Explanation = "This version of the game fights it differently from the record ("
                                   + Outcome(record.Victory, record.Turns) + " recorded, "
                                   + Outcome(result.Victory, result.Rounds) + " here), so it is not shown.";
                return replay;
            }
            replay.Verdict = GuildDungeonReplayVerdict.Exact;
            replay.Result = result;
            return replay;
        }

        private static string Outcome(bool victory, int turns)
        {
            return (victory ? "a clear" : "a loss") + " in " + turns + " turns";
        }

        public static float AverageStars(TeamSnapshot team)
        {
            if (team == null || team.Units.Count == 0) return 0f;
            int total = 0;
            for (int i = 0; i < team.Units.Count; i++) total += team.Units[i].Stars;
            return (float)Math.Round((double)total / team.Units.Count, 2);
        }

        // ---------------------------------------------------------------- floor conditions

        /// <summary>The condition a floor carries this cycle, or null. See
        /// GuildDungeonGenerator.ConditionsFor.</summary>
        public static FloorConditionDef ConditionOf(ContentDatabase content, int dungeonSeed, int floor)
        {
            return content == null ? null : GuildDungeonGenerator.ConditionFor(content, dungeonSeed, floor);
        }

        /// <summary>
        /// Whether a line may be fielded under a condition, and if not, the sentence that says what
        /// to change - "two Strikers; this floor allows one of each role". Null condition, or one
        /// with no line rules: always legal. Unknown heroes are skipped, as NormaliseTeam drops them.
        /// </summary>
        public static bool LineLegal(ContentDatabase content, FloorConditionDef condition, TeamSnapshot team, out string reason)
        {
            reason = "";
            if (condition == null || condition.Line.IsEmpty || team == null) return true;
            LineRules rules = condition.Line;
            List<SnapshotUnit> units = team.Units;
            if (rules.MaxHeroes > 0 && units.Count > rules.MaxHeroes)
            {
                reason = units.Count + " heroes; this floor allows " + rules.MaxHeroes;
                return false;
            }
            Dictionary<HeroRole, int> perRole = new Dictionary<HeroRole, int>();
            for (int i = 0; i < units.Count; i++)
            {
                HeroDef d = content != null ? content.GetHero(units[i].DefId) : null;
                if (d == null) continue;
                if (rules.MaxStars > 0 && d.Stars > rules.MaxStars)
                {
                    reason = d.Name + " is " + d.Stars + "-star; this floor allows " + rules.MaxStars + " stars at most";
                    return false;
                }
                if (rules.BannedRoles.Contains(d.Role))
                {
                    reason = d.Name + " is a " + d.Role + "; this floor allows no " + d.Role;
                    return false;
                }
                int n; perRole.TryGetValue(d.Role, out n); perRole[d.Role] = ++n;
                if (rules.MaxPerRole > 0 && n > rules.MaxPerRole)
                {
                    reason = n + " " + d.Role + "s; this floor allows " + rules.MaxPerRole + " of each role";
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// DESIGN-GUILD-FLOOR-CONDITIONS C7, THE RULE CHOSEN: a board that a floor's condition hands
        /// to EVERY legal line is SUSPENDED on that floor - nobody enters it, and the floor card
        /// says why. The other rule on offer, forbidding such a condition outright, would have cut
        /// the composition family's bluntest tool (a four-hero cap) to protect one board of six; a
        /// suspension costs that floor one board and keeps the question the cap asks.
        ///
        /// Read off the condition's LINE RULES, never declared by it, so a condition added later
        /// cannot forget to suspend what it grants. Only the three boards a line rule can grant are
        /// asked about: FIRST ATTEMPT and NOBODY FELL are about the attempt, not the line, and no
        /// line rule here can make every line one Aspect.
        /// </summary>
        public static bool Grants(FloorConditionDef condition, GuildDungeonBoard board)
        {
            if (condition == null) return false;
            LineRules rules = condition.Line;
            switch (board)
            {
                case GuildDungeonBoard.FourOrFewer: return rules.MaxHeroes > 0 && rules.MaxHeroes <= 4;
                case GuildDungeonBoard.NoFiveStar: return rules.MaxStars > 0 && rules.MaxStars < 5;
                case GuildDungeonBoard.NoHealer: return rules.BannedRoles.Contains(HeroRole.Mender);
            }
            return false;
        }

        /// <summary>Whether a board is suspended on this floor this cycle. See Grants.</summary>
        public static bool Suspended(ContentDatabase content, int dungeonSeed, int floor, GuildDungeonBoard board)
        {
            return Grants(ConditionOf(content, dungeonSeed, floor), board);
        }

        // ---------------------------------------------------------------- constraints

        /// <summary>
        /// Whether a CLEAR satisfies a board's rule. A loss satisfies nothing: it has no turn count
        /// worth comparing, and ranking it would reward giving up early.
        ///
        /// Aspect and role are read from the def through content, stars from the line by value.
        /// A hero this build cannot resolve fails OneAspect and NoHealer rather than being guessed
        /// about - a board entry this client cannot justify is not drawn.
        /// </summary>
        public static bool Satisfies(ContentDatabase content, GuildDungeonAttempt a, GuildDungeonBoard board)
        {
            if (a == null || !a.Victory || a.Team == null || a.Team.Units.Count == 0) return false;
            // The floor's condition first. A board the condition hands to everybody is suspended
            // (C7), and a line that broke the condition's line rules is on no board at all: the
            // client refuses to commit one, so a record carrying one did not come from a fair try.
            FloorConditionDef condition = ConditionOf(content, a.Seed, a.Floor);
            if (Grants(condition, board)) return false;
            string broke;
            if (!LineLegal(content, condition, a.Team, out broke)) return false;
            List<SnapshotUnit> units = a.Team.Units;
            switch (board)
            {
                case GuildDungeonBoard.FourOrFewer:
                    return units.Count <= 4;
                case GuildDungeonBoard.NoFiveStar:
                    for (int i = 0; i < units.Count; i++) if (units[i].Stars >= 5) return false;
                    return true;
                case GuildDungeonBoard.OneAspect:
                {
                    HeroDef first = content != null ? content.GetHero(units[0].DefId) : null;
                    if (first == null) return false;
                    for (int i = 1; i < units.Count; i++)
                    {
                        HeroDef d = content.GetHero(units[i].DefId);
                        if (d == null || d.Aspect != first.Aspect) return false;
                    }
                    return true;
                }
                case GuildDungeonBoard.NoHealer:
                    for (int i = 0; i < units.Count; i++)
                    {
                        HeroDef d = content != null ? content.GetHero(units[i].DefId) : null;
                        if (d == null || d.Role == HeroRole.Mender) return false;
                    }
                    return true;
                case GuildDungeonBoard.FirstAttempt:
                    return a.Attempt == 1;
                case GuildDungeonBoard.Flawless:
                    return a.Fallen == 0;
            }
            return false;
        }

        /// <summary>Every board this attempt enters, in drawing order. Nobody declares these.</summary>
        public static List<GuildDungeonBoard> BoardsMet(ContentDatabase content, GuildDungeonAttempt a)
        {
            List<GuildDungeonBoard> met = new List<GuildDungeonBoard>();
            for (int i = 0; i < Boards.Length; i++) if (Satisfies(content, a, Boards[i])) met.Add(Boards[i]);
            return met;
        }

        /// <summary>
        /// Why an attempt did NOT enter a board, as a short phrase naming the thing that kept it
        /// out: "fielded Kael, a 5-star", "2 heroes fell". Empty when it did enter. The result
        /// screen prints it under each board it missed, because "not met" tells a player nothing
        /// about what to change and the name of the hero does.
        /// </summary>
        public static string Unmet(ContentDatabase content, GuildDungeonAttempt a, GuildDungeonBoard board)
        {
            if (Satisfies(content, a, board)) return "";
            if (a == null || !a.Victory) return "the floor was not cleared";
            FloorConditionDef condition = ConditionOf(content, a.Seed, a.Floor);
            if (Grants(condition, board)) return "suspended on this floor: " + condition.Name + " grants it to every line";
            string broke;
            if (!LineLegal(content, condition, a.Team, out broke)) return "the line broke this floor's condition: " + broke;
            List<SnapshotUnit> units = a.Team.Units;
            switch (board)
            {
                case GuildDungeonBoard.FourOrFewer:
                    return units.Count + " heroes fought";
                case GuildDungeonBoard.NoFiveStar:
                    for (int i = 0; i < units.Count; i++)
                        if (units[i].Stars >= 5) return "fielded " + NameOf(content, units[i].DefId) + ", a 5-star";
                    break;
                case GuildDungeonBoard.OneAspect:
                {
                    HashSet<Aspect> aspects = new HashSet<Aspect>();
                    for (int i = 0; i < units.Count; i++)
                    {
                        HeroDef d = content != null ? content.GetHero(units[i].DefId) : null;
                        if (d != null) aspects.Add(d.Aspect);
                    }
                    return aspects.Count + " aspects in the line";
                }
                case GuildDungeonBoard.NoHealer:
                    for (int i = 0; i < units.Count; i++)
                    {
                        HeroDef d = content != null ? content.GetHero(units[i].DefId) : null;
                        if (d != null && d.Role == HeroRole.Mender) return "fielded " + d.Name + ", a Mender";
                    }
                    break;
                case GuildDungeonBoard.FirstAttempt:
                    return "this was attempt " + a.Attempt;
                case GuildDungeonBoard.Flawless:
                    return a.Fallen < 0 ? "losses were not recorded" : a.Fallen + (a.Fallen == 1 ? " hero fell" : " heroes fell");
            }
            return "not met";
        }

        private static string NameOf(ContentDatabase content, string defId)
        {
            HeroDef d = content != null ? content.GetHero(defId) : null;
            return d != null ? d.Name : defId;
        }

        public static string BoardTitle(GuildDungeonBoard board)
        {
            switch (board)
            {
                case GuildDungeonBoard.FourOrFewer: return "FOUR OR FEWER";
                case GuildDungeonBoard.NoFiveStar: return "NO FIVE STARS";
                case GuildDungeonBoard.OneAspect: return "ONE ASPECT";
                case GuildDungeonBoard.NoHealer: return "NO HEALER";
                case GuildDungeonBoard.FirstAttempt: return "FIRST ATTEMPT";
                case GuildDungeonBoard.Flawless: return "NOBODY FELL";
            }
            return board.ToString().ToUpperInvariant();
        }

        public static string BoardRule(GuildDungeonBoard board)
        {
            switch (board)
            {
                case GuildDungeonBoard.FourOrFewer: return "Cleared with four heroes or fewer.";
                case GuildDungeonBoard.NoFiveStar: return "Cleared with no 5-star hero.";
                case GuildDungeonBoard.OneAspect: return "Cleared with every hero of one Aspect.";
                case GuildDungeonBoard.NoHealer: return "Cleared with no Mender in the line.";
                case GuildDungeonBoard.FirstAttempt: return "Cleared on your first attempt at the floor this cycle.";
                case GuildDungeonBoard.Flawless: return "Cleared with every hero still standing.";
            }
            return "";
        }

        // ---------------------------------------------------------------- boards

        /// <summary>
        /// One board: each member's best qualifying clear of this floor in this cycle, fewest turns
        /// first. One entry per member.
        ///
        /// Ties fall through fewer heroes lost, then fewer attempts spent getting there, then the
        /// earlier row, then the user id, so the order is total and every device draws the same
        /// board.
        /// </summary>
        public static List<GuildDungeonAttempt> Rank(ContentDatabase content, List<GuildDungeonAttempt> attempts,
                                                     int cycle, int floor, GuildDungeonBoard board)
        {
            Dictionary<string, GuildDungeonAttempt> best = new Dictionary<string, GuildDungeonAttempt>();
            for (int i = 0; attempts != null && i < attempts.Count; i++)
            {
                GuildDungeonAttempt a = attempts[i];
                if (a == null || a.Cycle != cycle || a.Floor != floor) continue;
                if (!Satisfies(content, a, board)) continue;
                GuildDungeonAttempt held;
                if (!best.TryGetValue(a.UserId, out held) || Compare(a, held) < 0) best[a.UserId] = a;
            }
            List<GuildDungeonAttempt> ranked = new List<GuildDungeonAttempt>(best.Values);
            ranked.Sort(Compare);
            return ranked;
        }

        private static int Compare(GuildDungeonAttempt x, GuildDungeonAttempt y)
        {
            int c = x.Turns.CompareTo(y.Turns);
            if (c != 0) return c;
            c = FallenKey(x).CompareTo(FallenKey(y));
            if (c != 0) return c;
            c = x.Attempt.CompareTo(y.Attempt);
            if (c != 0) return c;
            c = x.Id.CompareTo(y.Id);
            if (c != 0) return c;
            return string.CompareOrdinal(x.UserId, y.UserId);
        }

        private static int FallenKey(GuildDungeonAttempt a) { return a.Fallen < 0 ? int.MaxValue : a.Fallen; }

        /// <summary>
        /// Where one attempt puts its member on every board it entered: their rank on the board,
        /// the board's size, and whether it is THIS attempt that holds the place or an earlier,
        /// better one of theirs.
        /// </summary>
        public static List<GuildDungeonStanding> StandingsOf(ContentDatabase content, List<GuildDungeonAttempt> attempts,
                                                            GuildDungeonAttempt attempt)
        {
            List<GuildDungeonStanding> standings = new List<GuildDungeonStanding>();
            if (attempt == null) return standings;
            List<GuildDungeonBoard> met = BoardsMet(content, attempt);
            for (int b = 0; b < met.Count; b++)
            {
                List<GuildDungeonAttempt> board = Rank(content, attempts, attempt.Cycle, attempt.Floor, met[b]);
                GuildDungeonStanding s = new GuildDungeonStanding();
                s.Board = met[b];
                s.Of = board.Count;
                for (int i = 0; i < board.Count; i++)
                {
                    if (board[i].UserId != attempt.UserId) continue;
                    s.Rank = i + 1;
                    s.ThisAttempt = SameAttempt(board[i], attempt);
                    break;
                }
                standings.Add(s);
            }
            return standings;
        }

        private static bool SameAttempt(GuildDungeonAttempt x, GuildDungeonAttempt y)
        {
            if (x.Id != 0 && y.Id != 0) return x.Id == y.Id;
            return x.UserId == y.UserId && x.Cycle == y.Cycle && x.Floor == y.Floor && x.Attempt == y.Attempt;
        }

        // ---------------------------------------------------------------- progress

        public static int AttemptsUsed(List<GuildDungeonAttempt> attempts, string userId, int cycle, int floor)
        {
            int used = 0;
            for (int i = 0; attempts != null && i < attempts.Count; i++)
            {
                GuildDungeonAttempt a = attempts[i];
                if (a != null && a.UserId == userId && a.Cycle == cycle && a.Floor == floor) used++;
            }
            return used;
        }

        /// <summary>The highest floor cleared this cycle, by one member (userId) or by anyone in the
        /// guild (null). Zero when nothing is cleared.</summary>
        public static int HighestCleared(List<GuildDungeonAttempt> attempts, string userId, int cycle)
        {
            int best = 0;
            for (int i = 0; attempts != null && i < attempts.Count; i++)
            {
                GuildDungeonAttempt a = attempts[i];
                if (a == null || !a.Victory || a.Cycle != cycle) continue;
                if (userId != null && a.UserId != userId) continue;
                if (a.Floor > best) best = a.Floor;
            }
            return best;
        }

        /// <summary>
        /// Whether a floor is open to attempt. THE GUILD PUSHES TOGETHER: floor 1 is always open,
        /// and floor N opens for everybody the moment anybody in the guild clears floor N-1. So a
        /// newer member is never locked out of the floor the guild is actually working on, and the
        /// question on it is the same one for all of them. guild_dungeon_submit (0008) holds the
        /// same rule; this copy decides what to draw.
        /// </summary>
        public static bool FloorOpen(List<GuildDungeonAttempt> attempts, int cycle, int floor)
        {
            if (floor <= 1) return true;
            return HighestCleared(attempts, null, cycle) >= floor - 1;
        }

        /// <summary>The first clear of a floor in the guild this cycle, by row order, or null. Not a
        /// board - it is a race, not a constraint - but worth a line on the boards screen.</summary>
        public static GuildDungeonAttempt FirstGuildClear(List<GuildDungeonAttempt> attempts, int cycle, int floor)
        {
            GuildDungeonAttempt first = null;
            for (int i = 0; attempts != null && i < attempts.Count; i++)
            {
                GuildDungeonAttempt a = attempts[i];
                if (a == null || !a.Victory || a.Cycle != cycle || a.Floor != floor) continue;
                if (first == null || a.Id < first.Id) first = a;
            }
            return first;
        }

        // ---------------------------------------------------------------- the wire

        /// <summary>
        /// The arguments of guild_dungeon_attempt (Server/supabase/migrations/0011), named as the
        /// function names them - or of 0008's guild_dungeon_submit when the record carries no board
        /// claim, since the two differ only by p_boards. The server assigns the attempt number, the
        /// user and the guild; sending them would only invite a client to choose.
        /// </summary>
        public static JsonValue WriteRpcArgs(GuildDungeonAttempt a)
        {
            JsonValue j = JsonValue.NewObject();
            j.Set("p_cycle", a.Cycle);
            j.Set("p_floor", a.Floor);
            j.Set("p_seed", a.Seed);
            j.Set("p_team", TeamSnapshotCodec.WriteUnits(a.Team));
            j.Set("p_victory", a.Victory);
            j.Set("p_turns", a.Turns);
            j.Set("p_power", a.Power);
            j.Set("p_avg_stars", (double)a.AverageStars);
            j.Set("p_fallen", Math.Max(0, a.Fallen));
            j.Set("p_content_version", a.ContentVersion ?? "");
            if (a.Boards >= 0) j.Set("p_boards", a.Boards);
            return j;
        }

        /// <summary>The columns every read of guild_dungeon_attempts asks for, the member's name
        /// included. Shared by the boards read and the chat card's embed, so the two cannot read a
        /// different record out of the same row.</summary>
        public const string RowColumns = "id,guild_id,user_id,cycle,floor,attempt,seed,team,victory,turns,"
                                       + "power,avg_stars,fallen,content_version,created_at";

        /// <summary>One row of guild_dungeon_attempts as PostgREST returns it. Total, like
        /// TeamSnapshotCodec.Read: a malformed row reads as an empty record rather than throwing.
        /// A row stored before 0008 has no fallen and no content_version, and reads as -1 and "".</summary>
        public static GuildDungeonAttempt ReadRow(JsonValue row)
        {
            GuildDungeonAttempt a = new GuildDungeonAttempt();
            if (row == null || !row.IsObject) return a;
            a.Id = row.GetLong("id", 0);
            a.GuildId = row.GetString("guild_id", "");
            a.UserId = row.GetString("user_id", "");
            a.Cycle = row.GetInt("cycle", 0);
            a.Floor = row.GetInt("floor", 1);
            a.Attempt = row.GetInt("attempt", 0);
            a.Seed = row.GetInt("seed", 0);
            a.Victory = row.GetBool("victory", false);
            a.Turns = row.GetInt("turns", 0);
            a.Power = row.GetInt("power", 0);
            a.AverageStars = row.GetFloat("avg_stars", 0f);
            a.Fallen = row.Has("fallen") ? row.GetInt("fallen", -1) : -1;
            a.ContentVersion = row.GetString("content_version", "");
            a.CreatedAt = row.GetString("created_at", "");
            a.Team = TeamSnapshotCodec.Read(row["team"], "", a.Power);
            JsonValue profile = row["profiles"];
            if (profile != null && profile.IsObject) a.DisplayName = profile.GetString("display_name", "");
            return a;
        }

        /// <summary>
        /// The text a shared attempt carries in guild_messages.body. It is what an OLD client, one
        /// that knows nothing of attempt cards, shows in place of the card - so it has to stand on
        /// its own as a sentence. Well under the 200-character rule.
        /// </summary>
        public static string ShareLine(GuildDungeonAttempt a)
        {
            if (a == null) return "";
            return "Floor " + a.Floor + ": " + (a.Victory ? "cleared in " + a.Turns + " turns" : "fell after " + a.Turns + " turns")
                 + " with " + a.Team.Units.Count + (a.Team.Units.Count == 1 ? " hero" : " heroes") + ". Watch it.";
        }

        /// <summary>The arguments of guild_share_attempt (0008).</summary>
        public static JsonValue WriteShareArgs(GuildDungeonAttempt a)
        {
            JsonValue j = JsonValue.NewObject();
            j.Set("p_attempt_id", a == null ? 0L : a.Id);
            j.Set("p_body", ShareLine(a));
            return j;
        }
    }
}
