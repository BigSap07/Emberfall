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
    /// The three lenses a guild dungeon floor is scored through. Same fight, different questions:
    /// how fast, how cheaply, and with how humble a line. Lower is better on all three.
    /// </summary>
    public enum GuildDungeonBoard
    {
        FewestTurns,
        LowestPower,
        LowestStars
    }

    /// <summary>
    /// One attempt at one floor, as the server stores it and as the client submits it.
    ///
    /// THIS RECORD IS A REPLAY. Combat is a pure function (CombatSimulation.Run), the floor is a
    /// pure function of (Seed, Floor) and the player side is carried whole in Team, so
    /// GuildDungeon.Replay(content, record) reproduces the fight event for event. That is the
    /// point of storing the team by value rather than a reference to anybody's save: a handover
    /// feature, a re-verification pass or a "watch how they did it" button all read this and
    /// nothing else. The scores are stored beside it so a board can be read without running a
    /// single battle; Replay is what makes them checkable.
    ///
    /// The one thing that breaks a replay is content moving under it. A balance patch mid-cycle
    /// changes what the same team does on the same seed. The record does not carry a content
    /// version yet. That is a known gap, left open because the prototype has no content version
    /// to carry; a replay across a patch will disagree with its record and should be read as
    /// "content moved", not as "the client lied".
    /// </summary>
    public sealed class GuildDungeonAttempt
    {
        public string GuildId = "";
        public string UserId = "";
        public int Cycle;
        public int Floor = 1;

        /// <summary>The server's attempt number, 1 to the cap. Zero on a record not yet submitted.</summary>
        public int Attempt;

        /// <summary>The DUNGEON seed (GuildDungeon.SeedFor), not the fight seed - the fight seed is
        /// derived from it per floor, so one number describes the whole cycle.</summary>
        public int Seed;

        /// <summary>The line that fought, by value: heroes, levels, stars, relics, talents and armor.
        /// Encoded with TeamSnapshotCodec, the same shape team_snapshots.units holds.</summary>
        public TeamSnapshot Team = new TeamSnapshot();

        public bool Victory;

        /// <summary>BattleResult.Rounds - one round is every living unit acting once.</summary>
        public int Turns;

        /// <summary>The line's power as fought, relics and armor included (ProvingGenerator.PowerOf).</summary>
        public int Power;

        /// <summary>Mean star rating of the units that fought, rounded to two places - the precision
        /// the server column holds, so the value ranked locally is the value stored.</summary>
        public float AverageStars;

        public string CreatedAt = "";

        public float Metric(GuildDungeonBoard board)
        {
            switch (board)
            {
                case GuildDungeonBoard.FewestTurns: return Turns;
                case GuildDungeonBoard.LowestPower: return Power;
                case GuildDungeonBoard.LowestStars: return AverageStars;
            }
            return 0f;
        }
    }

    /// <summary>
    /// The guild dungeon's rules that are not the floor generator: whose seed, which fight, what
    /// score, and who is ahead. Engine-free and pure, like everything under Core.
    /// </summary>
    public static class GuildDungeon
    {
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

        /// <summary>
        /// Fights one floor with one line. The live attempt and the replay both come through here,
        /// so they cannot build the player side two different ways.
        ///
        /// The player side is materialised from the snapshot into a scratch profile that owns only
        /// the armor the snapshot carries - the same trick the ghost and duel paths avoid needing
        /// because they fight on the OPPONENT side. The team here is the player's side, which
        /// resolves armor through BattleRequest.Profile, so a profile is what it gets.
        /// </summary>
        public static BattleResult Fight(ContentDatabase content, TeamSnapshot team, int dungeonSeed, int floor)
        {
            BattleRequest request = new BattleRequest();
            request.Content = content;
            request.Stage = GuildDungeonGenerator.BuildFloor(content, dungeonSeed, floor);
            request.Seed = FightSeedFor(dungeonSeed, floor);

            PlayerProfile scratch = new PlayerProfile();
            for (int i = 0; team != null && i < team.Units.Count; i++)
            {
                SnapshotUnit u = team.Units[i];
                HeroDef def = u == null ? null : content.GetHero(u.DefId);
                if (def == null) continue;
                HeroInstance hero = new HeroInstance();
                hero.Uid = "guild_dungeon_" + i;
                hero.DefId = u.DefId;
                hero.Def = def;
                hero.Level = Math.Max(1, u.Level);
                hero.EquippedRelicId = u.RelicId ?? "";
                hero.SelectedTalent = u.SelectedTalent;
                for (int a = 0; a < u.Armor.Count; a++)
                {
                    ArmorPiece piece = u.Armor[a];
                    if (piece == null) continue;
                    int slot = PlayerProfile.SlotIndex(piece.Slot);
                    if (slot < 0 || slot >= hero.EquippedArmor.Length) continue;
                    scratch.OwnedArmor.Add(piece);
                    hero.EquippedArmor[slot] = piece.Uid;
                }
                request.Team.Add(hero);
            }
            request.Profile = scratch;
            return CombatSimulation.Run(request);
        }

        /// <summary>
        /// Fights a floor and scores it into a record ready to submit. Attempt, UserId, GuildId and
        /// CreatedAt are the caller's and the server's to fill.
        /// </summary>
        public static GuildDungeonAttempt Attempt(ContentDatabase content, TeamSnapshot team, int dungeonSeed,
                                                  int cycle, int floor, out BattleResult result)
        {
            result = Fight(content, team, dungeonSeed, floor);
            GuildDungeonAttempt record = new GuildDungeonAttempt();
            record.Cycle = cycle;
            record.Floor = floor;
            record.Seed = dungeonSeed;
            record.Team = team;
            record.Victory = result.Victory;
            record.Turns = result.Rounds;
            record.Power = ProvingGenerator.PowerOf(content, team.ToLoadout());
            record.AverageStars = AverageStars(team);
            return record;
        }

        /// <summary>Runs the fight a record describes. Equal Victory and Turns to the record is what
        /// "this attempt is what it says it was" means.</summary>
        public static BattleResult Replay(ContentDatabase content, GuildDungeonAttempt record)
        {
            return Fight(content, record.Team, record.Seed, record.Floor);
        }

        public static float AverageStars(TeamSnapshot team)
        {
            if (team == null || team.Units.Count == 0) return 0f;
            int total = 0;
            for (int i = 0; i < team.Units.Count; i++) total += team.Units[i].Stars;
            return (float)Math.Round((double)total / team.Units.Count, 2);
        }

        // ---------------------------------------------------------------- boards

        /// <summary>
        /// One board: each member's best CLEAR of this floor in this cycle under the given lens,
        /// best first. A loss is recorded but never ranked - it has no turn count worth comparing
        /// and ranking it would reward giving up early.
        ///
        /// Ties fall through the other two lenses in board order, then the earlier attempt number,
        /// then the user id, so the order is total and every device draws the same board.
        /// </summary>
        public static List<GuildDungeonAttempt> Rank(List<GuildDungeonAttempt> attempts, int cycle, int floor,
                                                     GuildDungeonBoard board)
        {
            Dictionary<string, GuildDungeonAttempt> best = new Dictionary<string, GuildDungeonAttempt>();
            for (int i = 0; attempts != null && i < attempts.Count; i++)
            {
                GuildDungeonAttempt a = attempts[i];
                if (a == null || !a.Victory || a.Cycle != cycle || a.Floor != floor) continue;
                GuildDungeonAttempt held;
                if (!best.TryGetValue(a.UserId, out held) || Compare(a, held, board) < 0) best[a.UserId] = a;
            }
            List<GuildDungeonAttempt> ranked = new List<GuildDungeonAttempt>(best.Values);
            ranked.Sort((x, y) => Compare(x, y, board));
            return ranked;
        }

        private static int Compare(GuildDungeonAttempt x, GuildDungeonAttempt y, GuildDungeonBoard board)
        {
            int c = x.Metric(board).CompareTo(y.Metric(board));
            if (c != 0) return c;
            for (int b = 0; b < 3; b++)
            {
                GuildDungeonBoard other = (GuildDungeonBoard)b;
                if (other == board) continue;
                c = x.Metric(other).CompareTo(y.Metric(other));
                if (c != 0) return c;
            }
            c = x.Attempt.CompareTo(y.Attempt);
            if (c != 0) return c;
            return string.CompareOrdinal(x.UserId, y.UserId);
        }

        // ---------------------------------------------------------------- the wire

        /// <summary>
        /// The arguments of guild_dungeon_record (Server/supabase/migrations/0007), named as the
        /// function names them. The server assigns the attempt number, the user and the guild;
        /// sending them would only invite a client to choose.
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
            return j;
        }

        /// <summary>One row of guild_dungeon_attempts as PostgREST returns it. Total, like
        /// TeamSnapshotCodec.Read: a malformed row reads as an empty record rather than throwing.</summary>
        public static GuildDungeonAttempt ReadRow(JsonValue row)
        {
            GuildDungeonAttempt a = new GuildDungeonAttempt();
            if (row == null || !row.IsObject) return a;
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
            a.CreatedAt = row.GetString("created_at", "");
            a.Team = TeamSnapshotCodec.Read(row["team"], "", a.Power);
            return a;
        }
    }
}
