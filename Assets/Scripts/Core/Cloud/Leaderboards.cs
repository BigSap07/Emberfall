using System;
using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Cloud
{
    /// <summary>
    /// The boards leaderboard_top serves (Server/supabase/migrations/0016_leaderboards.sql,
    /// Docs/DESIGN-LEADERBOARDS.md). Closed: the server refuses any other name with 400.
    /// </summary>
    public enum LeaderboardBoard
    {
        /// <summary>The Deep Recital's highest floor, all time.</summary>
        Recital,
        /// <summary>The highest floor held at the latest submission this week. Resets Monday 04:00 UTC.</summary>
        RecitalWeek,
        /// <summary>Floors climbed this week. Resets Monday 04:00 UTC.</summary>
        RecitalWeekClimb,
        /// <summary>The Proving's highest standing, all time.</summary>
        Proving,
        /// <summary>Campaign stages cleared.</summary>
        Campaign
    }

    /// <summary>One row of a board as leaderboard_top returns it. There is no user id, by design: the
    /// server never sends one.</summary>
    public sealed class LeaderboardRow
    {
        public long Rank;
        public string DisplayName = "";
        public long Value;
        public bool IsMe;
    }

    /// <summary>What leaderboard_submit did with one board.</summary>
    public enum LeaderboardVerdict
    {
        /// <summary>Not in the answer (an older server, or a malformed body).</summary>
        None,
        /// <summary>Higher than before and passed every check: stored.</summary>
        Raised,
        /// <summary>Not higher than what the board already holds: the stored value stays.</summary>
        Kept,
        /// <summary>Failed a check: the stored value stays, and the server logged why.</summary>
        Refused
    }

    public sealed class LeaderboardBoardResult
    {
        public LeaderboardVerdict Verdict = LeaderboardVerdict.None;
        public long Value;
        /// <summary>The most the rate allowed, for a refusal. -1 when not sent.</summary>
        public long Allowed = -1;
        /// <summary>The server's reason, for a refusal. Never shown to the player verbatim: the
        /// presenter words it (LeaderboardsPresenter.SubmitLine).</summary>
        public string Reason = "";
    }

    /// <summary>leaderboard_submit's answer.</summary>
    public sealed class LeaderboardSubmitResult
    {
        /// <summary>The server's clock when it answered, unix seconds. 0 when not sent.</summary>
        public long ServerTimeUnix;
        /// <summary>The Monday 04:00 UTC the server's current board week began. 0 when not sent.</summary>
        public long WeekStartUnix;
        public int SaveRevision = -1;
        public LeaderboardBoardResult Recital = new LeaderboardBoardResult();
        public LeaderboardBoardResult Proving = new LeaderboardBoardResult();
        public LeaderboardBoardResult Campaign = new LeaderboardBoardResult();
    }

    /// <summary>Why a leaderboard call did not answer, in the cases a screen words differently.</summary>
    public enum LeaderboardFailure
    {
        None,
        /// <summary>No cloud configured, no signal, or not signed in.</summary>
        Offline,
        /// <summary>PT401: the session is gone.</summary>
        NotSignedIn,
        /// <summary>PT404 on submit: the account has no cloud save yet, so there is nothing to read a score from.</summary>
        NoCloudSave,
        /// <summary>PT400 on submit: the cloud save is older than the server's min_save_version.</summary>
        SaveTooOld,
        /// <summary>PT429 on submit: submitted again inside a minute. An answer, not an error.</summary>
        TooSoon,
        /// <summary>PGRST202: the function does not exist on this server (0016 not applied).</summary>
        NotDeployed,
        /// <summary>Anything else: a 5xx, a timeout, a body that does not parse.</summary>
        Server
    }

    /// <summary>
    /// Reads and writes the bodies of the two leaderboard functions. Engine-free, so the harness runs
    /// it against the exact shapes 0016 returns (R28ScreenChecks); the transport in
    /// Assets/Scripts/Game/Cloud/LeaderboardService.cs sends the requests and hands the bodies here.
    /// </summary>
    public static class LeaderboardCodec
    {
        /// <summary>leaderboard_top's p_limit range, as the server clamps it.</summary>
        public const int MaxLimit = 100;
        public const int DefaultLimit = 50;

        /// <summary>The server's flood limit on leaderboard_submit (leaderboard_limits.submit_interval_seconds).
        /// The client waits at least this long between submissions so it is never refused for it.</summary>
        public const int SubmitIntervalSeconds = 60;

        /// <summary>The hour (UTC) on Monday the weekly board turns: week_reset_hour_utc, which matches
        /// balance.json dailyResetHourUtc.</summary>
        public const int WeekResetHourUtc = 4;

        public static string IdOf(LeaderboardBoard board)
        {
            switch (board)
            {
                case LeaderboardBoard.Recital: return "recital";
                case LeaderboardBoard.RecitalWeek: return "recital_week";
                case LeaderboardBoard.RecitalWeekClimb: return "recital_week_climb";
                case LeaderboardBoard.Proving: return "proving";
                default: return "campaign";
            }
        }

        /// <summary>True for the boards that reset every Monday.</summary>
        public static bool IsWeekly(LeaderboardBoard board)
        {
            return board == LeaderboardBoard.RecitalWeek || board == LeaderboardBoard.RecitalWeekClimb;
        }

        /// <summary>The JSON body of a leaderboard_top call.</summary>
        public static string TopArgs(LeaderboardBoard board, int limit)
        {
            JsonValue o = JsonValue.NewObject();
            o.Set("p_board", IdOf(board));
            o.Set("p_limit", Math.Max(1, Math.Min(MaxLimit, limit)));
            return o.ToJson(false);
        }

        /// <summary>leaderboard_top's rows, best first as the server ordered them. Rows without a rank
        /// are dropped. Null when the body is not a list at all.</summary>
        public static List<LeaderboardRow> ReadTop(string body)
        {
            JsonValue root;
            string error;
            if (!JsonValue.TryParse(body, out root, out error) || root == null || !root.IsArray) return null;
            List<LeaderboardRow> rows = new List<LeaderboardRow>();
            List<JsonValue> raw = root.AsList();
            for (int i = 0; i < raw.Count; i++)
            {
                JsonValue r = raw[i];
                if (r == null || !r.IsObject) continue;
                LeaderboardRow row = new LeaderboardRow();
                row.Rank = r.GetLong("rank", 0);
                if (row.Rank <= 0) continue;
                row.DisplayName = r.GetString("display_name", "");
                row.Value = Math.Max(0, r.GetLong("value", 0));
                row.IsMe = r.GetBool("is_me", false);
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>leaderboard_submit's answer. Null when the body is not an object.</summary>
        public static LeaderboardSubmitResult ReadSubmit(string body)
        {
            JsonValue root;
            string error;
            if (!JsonValue.TryParse(body, out root, out error) || root == null) return null;
            if (root.IsArray && root.Count > 0) root = root[0];
            if (!root.IsObject) return null;
            LeaderboardSubmitResult r = new LeaderboardSubmitResult();
            r.ServerTimeUnix = GuildCodec.ParseTimestamp(root.GetString("server_time", ""));
            r.WeekStartUnix = GuildCodec.ParseTimestamp(root.GetString("week_start", ""));
            r.SaveRevision = root.GetInt("save_revision", -1);
            r.Recital = ReadBoard(root["recital"]);
            r.Proving = ReadBoard(root["proving"]);
            r.Campaign = ReadBoard(root["campaign"]);
            return r;
        }

        private static LeaderboardBoardResult ReadBoard(JsonValue o)
        {
            LeaderboardBoardResult b = new LeaderboardBoardResult();
            if (o == null || !o.IsObject) return b;
            switch (o.GetString("status", ""))
            {
                case "raised": b.Verdict = LeaderboardVerdict.Raised; break;
                case "kept": b.Verdict = LeaderboardVerdict.Kept; break;
                case "refused": b.Verdict = LeaderboardVerdict.Refused; break;
                default: b.Verdict = LeaderboardVerdict.None; break;
            }
            b.Value = Math.Max(0, o.GetLong("value", 0));
            b.Allowed = o.Has("allowed") ? o.GetLong("allowed", -1) : -1;
            b.Reason = o.GetString("reason", "");
            return b;
        }

        /// <summary>
        /// Which failure an HTTP answer is. status 0 is a transport failure (no signal). 404 is read
        /// from the body's PostgREST code, because two different things answer 404 here: the function
        /// not existing (PGRST202) and leaderboard_submit's own "no cloud save" (PT404).
        /// </summary>
        public static LeaderboardFailure FailureOf(long status, string body)
        {
            if (status >= 200 && status < 300) return LeaderboardFailure.None;
            if (status == 0) return LeaderboardFailure.Offline;
            string code = CodeIn(body);
            if (code == "PGRST202") return LeaderboardFailure.NotDeployed;
            if (status == 401) return LeaderboardFailure.NotSignedIn;
            if (status == 404) return LeaderboardFailure.NoCloudSave;
            if (status == 400) return LeaderboardFailure.SaveTooOld;
            if (status == 429) return LeaderboardFailure.TooSoon;
            return LeaderboardFailure.Server;
        }

        /// <summary>The `code` field of a PostgREST error body, or empty.</summary>
        public static string CodeIn(string body)
        {
            if (string.IsNullOrEmpty(body)) return "";
            JsonValue root;
            string error;
            if (!JsonValue.TryParse(body, out root, out error) || root == null || !root.IsObject) return "";
            return root.GetString("code", "");
        }

        /// <summary>
        /// The next weekly board reset after a moment: the first Monday at WeekResetHourUtc strictly
        /// after it. Pure arithmetic on UTC, the same boundary the server's leaderboard_week_start draws.
        /// </summary>
        public static long NextWeekResetUnix(long unix)
        {
            const long Day = 86400L;
            long shifted = unix - (long)WeekResetHourUtc * 3600L;
            long days = shifted >= 0 ? shifted / Day : -(((-shifted) + Day - 1L) / Day);
            // 1970-01-01 was a Thursday, so day 4 after the epoch is the first Monday.
            long sinceMonday = ((days - 4L) % 7L + 7L) % 7L;
            long weekStartDay = days - sinceMonday;
            return (weekStartDay + 7L) * Day + (long)WeekResetHourUtc * 3600L;
        }
    }
}
