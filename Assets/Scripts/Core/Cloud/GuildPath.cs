using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Json;

namespace Emberfall.Core.Cloud
{
    /// <summary>
    /// What the guild dungeon pays, and what the server's copy of the rule pays - the same rule
    /// written twice (claude/DESIGN-GUILDS-PHASE-TWO.md, system 2, and
    /// Server/supabase/migrations/0011_guild_path.sql).
    ///
    /// THE SERVER PAYS; THIS FILE ONLY PREDICTS. The client never sends an amount. The server
    /// credits the base inside guild_dungeon_attempt, as the row is stored, and the board bonus
    /// inside guild_path_settle, once the cycle has closed and the boards are final. This copy is
    /// what the result screen and the Path screen print before the server answers, and what the
    /// harness derives the numbers with.
    ///
    /// THREE RULES, each one written against a named failure in the design record:
    ///
    ///   1. THE BASE IS SCORED ON THE MEMBER'S OWN ATTEMPT, AND ON NO FLOOR IN PARTICULAR. A first
    ///      attempt of the cycle pays PayAttempt, won or lost; a first clear of any floor pays
    ///      PayClear. Floor N opens only when somebody in the guild clears floor N-1, so a payout
    ///      that grew with the floor would be a payout the rest of the guild set - the collective
    ///      gating the record forbids. Floor 1 is always open, so every member can earn the whole
    ///      base whatever the guild has done.
    ///
    ///   2. ONLY FLOOR 1'S BOARDS PAY, for the same reason. The deeper boards are still boards and
    ///      still recognition; they carry no material value, because a guild that opened more
    ///      floors would otherwise hand its members more chances to be paid.
    ///
    ///   3. A PLACEMENT PAYS ITS SHARE OF THE BOARD BEATEN: first of n entrants is 1, last is 0,
    ///      (n - rank) / (n - 1). Averaged over a board's entrants that is exactly one half at
    ///      every size from two up, so a member of a big guild and a member of a small one are paid
    ///      the same on average for the same standing. A board with one entrant pays nothing:
    ///      being first of one is not a placement. The best BoardsCounted scores of a cycle are
    ///      paid, which is one guild's worth, so changing guild mid-cycle pays no more.
    /// </summary>
    public static class GuildPath
    {
        /// <summary>
        /// A board's bit in guild_dungeon_attempts.boards: 1 shifted by the board's place in
        /// GuildDungeonBoard. THE ENUM ORDER IS NOW STORED, so it is appended to and never
        /// reordered; GuildPathChecks pins it against the migration.
        /// </summary>
        public static int BoardBit(GuildDungeonBoard board)
        {
            return 1 << (int)board;
        }

        /// <summary>The boards an attempt entered, as the bit mask the server stores. Read off
        /// GuildDungeon.BoardsMet, so a suspended board (C7) and an illegal line enter nothing.</summary>
        public static int BoardMask(ContentDatabase content, GuildDungeonAttempt attempt)
        {
            int mask = 0;
            List<GuildDungeonBoard> met = GuildDungeon.BoardsMet(content, attempt);
            for (int i = 0; i < met.Count; i++) mask |= BoardBit(met[i]);
            return mask;
        }

        /// <summary>
        /// The claim to send with a new attempt. FIRST ATTEMPT reads the attempt number, which the
        /// server has not assigned yet, so the mask is taken as the attempt it will be - the
        /// attempts this member has already spent on the floor, plus one. If that guess is wrong the
        /// server drops the bit; it never adds one.
        /// </summary>
        public static int ClaimMask(ContentDatabase content, GuildDungeonAttempt record, int attemptNumber)
        {
            if (record == null) return 0;
            int kept = record.Attempt;
            record.Attempt = attemptNumber;
            int mask = BoardMask(content, record);
            record.Attempt = kept;
            return mask;
        }

        /// <summary>The base a member has earned in one cycle: rule 1, off their own rows alone.</summary>
        public static int BaseFor(GuildPathConfig config, List<GuildDungeonAttempt> attempts, string userId, int cycle)
        {
            if (config == null) return 0;
            bool attempted = false, cleared = false;
            for (int i = 0; attempts != null && i < attempts.Count; i++)
            {
                GuildDungeonAttempt a = attempts[i];
                if (a == null || a.UserId != userId || a.Cycle != cycle) continue;
                attempted = true;
                if (a.Victory) cleared = true;
            }
            return (attempted ? config.PayAttempt : 0) + (cleared ? config.PayClear : 0);
        }

        /// <summary>
        /// The member's placement score on every paying board of one guild's cycle (rules 2 and
        /// 3), one entry per board they entered, highest first. Ranked by GuildDungeon.Rank, the
        /// same order the boards screen draws, and the same order guild_path_settle sorts in.
        /// </summary>
        public static List<float> BoardScores(ContentDatabase content, GuildPathConfig config,
                                              List<GuildDungeonAttempt> attempts, string userId, int cycle)
        {
            List<float> scores = new List<float>();
            if (config == null) return scores;
            for (int b = 0; b < GuildDungeon.Boards.Length; b++)
            {
                List<GuildDungeonAttempt> board = GuildDungeon.Rank(content, attempts, cycle, config.BoardFloor, GuildDungeon.Boards[b]);
                int n = board.Count;
                for (int r = 0; r < n; r++)
                {
                    if (board[r].UserId != userId) continue;
                    scores.Add(n < 2 ? 0f : (float)(n - 1 - r) / (n - 1));
                    break;
                }
            }
            scores.Sort((x, y) => y.CompareTo(x));
            return scores;
        }

        /// <summary>The bonus the best scores pay, rounded half away from zero as Postgres's
        /// round(numeric) does.</summary>
        public static int BonusFor(GuildPathConfig config, List<float> scores)
        {
            if (config == null || scores == null) return 0;
            double sum = 0;
            for (int i = 0; i < scores.Count && i < config.BoardsCounted; i++) sum += scores[i];
            return (int)Math.Round(sum * config.PayBoardPoint, MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>What guild_path_state returns: the balance, and the nodes owned.</summary>
    public sealed class GuildPathState
    {
        public long Balance;
        public long Earned;
        public readonly List<string> Nodes = new List<string>();

        public static GuildPathState Read(JsonValue row)
        {
            GuildPathState s = new GuildPathState();
            if (row == null) return s;
            if (row.IsArray) row = row.AsList().Count > 0 ? row.AsList()[0] : new JsonValue();
            s.Balance = Math.Max(0, row.GetLong("balance", 0));
            s.Earned = Math.Max(0, row.GetLong("earned", 0));
            List<JsonValue> nodes = row.GetList("nodes");
            for (int i = 0; i < nodes.Count; i++)
            {
                string id = nodes[i].AsString("");
                if (id.Length > 0 && !s.Nodes.Contains(id)) s.Nodes.Add(id);
            }
            return s;
        }
    }
}
