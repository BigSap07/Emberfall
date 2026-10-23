using System;
using System.Collections.Generic;
using Emberfall.Core.Cloud;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;

namespace Emberfall.Core.Screens
{
    public sealed class LeaderboardTabView
    {
        public LeaderboardBoard Board;
        public string Label = "";
        public bool Selected;
    }

    public sealed class LeaderboardRowView
    {
        public long Rank;
        /// <summary>"#1". Tied players share a rank, as the server ranks them.</summary>
        public string RankText = "";
        public string Name = "";
        public string ValueText = "";
        /// <summary>The caller's own row: drawn highlighted.</summary>
        public bool IsMe;
    }

    /// <summary>Everything the Leaderboards screen draws, and nothing it has to work out.</summary>
    public sealed class LeaderboardsView
    {
        public ScreenPhase Phase;
        public string Title = "LEADERBOARDS";
        public readonly List<LeaderboardTabView> Tabs = new List<LeaderboardTabView>();
        public LeaderboardBoard Board;
        /// <summary>What the selected board measures: "Highest floor reached in the Deep Recital".</summary>
        public string Caption = "";
        /// <summary>The value column's header: "FLOOR", "STANDING", "STAGES", "CLIMBED".</summary>
        public string ValueHeader = "";
        /// <summary>Best first. Never holds the caller's row when it sits below the top list; that one is PinnedSelf.</summary>
        public readonly List<LeaderboardRowView> Rows = new List<LeaderboardRowView>();
        /// <summary>The caller's own row when it ranks below the top list, drawn pinned under it. Null otherwise.</summary>
        public LeaderboardRowView PinnedSelf;
        /// <summary>"You are #12", "You are not on this board yet". Empty while there are no rows to judge by.</summary>
        public string MyRankLine = "";
        /// <summary>The whole-screen sentence for Offline, Error and Empty. Empty in Ready and Loading.</summary>
        public string Status = "";
        /// <summary>A one-line notice over rows that are still shown: a refresh that failed. Empty otherwise.</summary>
        public string Banner = "";
        /// <summary>"Resets in 2d 4h" on a weekly board, "All time" on the others.</summary>
        public string ResetLine = "";
        /// <summary>Seconds to the weekly reset, -1 on an all-time board. The screen ticks ResetLine by rebuilding.</summary>
        public long ResetInSeconds = -1;
        /// <summary>"Updated just now", "Updated 3m ago". Empty before the first answer.</summary>
        public string UpdatedLine = "";
        /// <summary>What happened to the player's own result on this board at the last submission. Empty when nothing to say.</summary>
        public string SubmitLine = "";
        /// <summary>"UNLOCKS AT 2-8" when the player cannot climb this board yet. The board is still shown.</summary>
        public string LockLine = "";
        public bool ShowSpinner;
        public ButtonModel Refresh = ButtonModel.Hidden();
        public ButtonModel Retry = ButtonModel.Hidden();
    }

    /// <summary>
    /// THE LEADERBOARDS SCREEN'S STATE (Docs/ui-specs/R28-SCREENS.md, Docs/DESIGN-LEADERBOARDS.md).
    ///
    /// Engine-free and stateful. The Game-side controller (Assets/Scripts/Game/Cloud/LeaderboardService.cs,
    /// LeaderboardsController) makes the calls and reports each answer here; the screen calls Build and
    /// draws exactly what comes back. Nothing in the screen decides a state, a label or whether a button
    /// can be tapped.
    ///
    /// THE CLIENT NEVER SENDS A SCORE. Submitting is leaderboard_submit with no argument; the server reads
    /// the player's own cloud save. So there is no "post my score" button: Refresh submits (at most once a
    /// minute, LeaderboardCodec.SubmitIntervalSeconds) and then reads the selected board.
    ///
    /// Boards pay nothing (Docs/DESIGN-LEADERBOARDS.md, "Rewards"), so nothing here names a reward, and no
    /// rank can be bought.
    /// </summary>
    public sealed class LeaderboardsPresenter
    {
        /// <summary>
        /// Which weekly Recital board the THIS WEEK tab shows: highest floor (RecitalWeek) or floors climbed
        /// (RecitalWeekClimb). Owner decision 1 in Docs/DESIGN-LEADERBOARDS.md; the server serves both, so
        /// switching is this one value.
        /// </summary>
        public static LeaderboardBoard WeeklyBoard = LeaderboardBoard.RecitalWeek;

        /// <summary>How many rows a board asks for.</summary>
        public const int Limit = LeaderboardCodec.DefaultLimit;

        private sealed class Cached
        {
            public List<LeaderboardRow> Rows;
            public long LoadedUnix;
        }

        private LeaderboardBoard _selected = LeaderboardBoard.Recital;
        private readonly Dictionary<LeaderboardBoard, Cached> _cache = new Dictionary<LeaderboardBoard, Cached>();
        private bool _loading;
        private bool _offline;
        private string _offlineReason = "";
        private bool _offlineCanRetry;
        private LeaderboardFailure _failure = LeaderboardFailure.None;
        private bool _submitting;
        private long _lastSubmitUnix = long.MinValue;
        private LeaderboardSubmitResult _lastSubmit;
        private LeaderboardFailure _submitFailure = LeaderboardFailure.None;
        /// <summary>Server clock minus device clock, from the last submission's server_time. 0 until known.</summary>
        private long _serverOffset;
        private bool _serverOffsetKnown;

        /// <summary>The tabs, left to right.</summary>
        public static LeaderboardBoard[] TabOrder()
        {
            return new LeaderboardBoard[] { LeaderboardBoard.Recital, WeeklyBoard, LeaderboardBoard.Proving, LeaderboardBoard.Campaign };
        }

        public LeaderboardBoard Selected { get { return _selected; } }
        public bool Loading { get { return _loading; } }

        // ------------------------------------------------------------------ what the controller reports

        /// <summary>A tab was tapped. Returns true when the controller should load it (nothing cached).</summary>
        public bool Select(LeaderboardBoard board)
        {
            _selected = board;
            _failure = LeaderboardFailure.None;
            return !_cache.ContainsKey(board);
        }

        /// <summary>No server to ask. canRetry is false when trying again cannot help (no cloud config in this build).</summary>
        public void SetOffline(string reason, bool canRetry)
        {
            _offline = true;
            _offlineReason = reason ?? "";
            _offlineCanRetry = canRetry;
            _loading = false;
            _submitting = false;
        }

        /// <summary>The backend is ready again. Clears the offline state so a submission can be due.</summary>
        public void Online()
        {
            _offline = false;
            _offlineReason = "";
        }

        public void BeginLoad(LeaderboardBoard board)
        {
            _offline = false;
            _failure = LeaderboardFailure.None;
            if (board == _selected) _loading = true;
        }

        public void TopLoaded(LeaderboardBoard board, List<LeaderboardRow> rows, long nowUnix)
        {
            Cached c = new Cached();
            c.Rows = rows ?? new List<LeaderboardRow>();
            c.LoadedUnix = nowUnix;
            _cache[board] = c;
            if (board == _selected) { _loading = false; _failure = LeaderboardFailure.None; }
        }

        public void TopFailed(LeaderboardBoard board, LeaderboardFailure failure)
        {
            if (board != _selected) return;
            _loading = false;
            if (failure == LeaderboardFailure.Offline) { SetOffline("You are offline, so the boards cannot be reached.", true); return; }
            _failure = failure == LeaderboardFailure.None ? LeaderboardFailure.Server : failure;
        }

        /// <summary>True when a submission is due: online, none in flight, and the last one at least a minute ago.</summary>
        public bool ShouldSubmit(long nowUnix)
        {
            if (_offline || _submitting) return false;
            return _lastSubmitUnix == long.MinValue || nowUnix - _lastSubmitUnix >= LeaderboardCodec.SubmitIntervalSeconds;
        }

        public void SubmitStarted(long nowUnix)
        {
            _submitting = true;
            _lastSubmitUnix = nowUnix;
        }

        public void SubmitAnswered(LeaderboardSubmitResult result, long nowUnix)
        {
            _submitting = false;
            _submitFailure = LeaderboardFailure.None;
            if (result == null) { _submitFailure = LeaderboardFailure.Server; return; }
            _lastSubmit = result;
            if (result.ServerTimeUnix > 0)
            {
                _serverOffset = result.ServerTimeUnix - nowUnix;
                _serverOffsetKnown = true;
            }
        }

        public void SubmitFailed(LeaderboardFailure failure)
        {
            _submitting = false;
            _submitFailure = failure;
            if (failure == LeaderboardFailure.Offline) SetOffline("You are offline, so the boards cannot be reached.", true);
        }

        // ------------------------------------------------------------------ what the screen draws

        public LeaderboardsView Build(ContentDatabase content, PlayerProfile profile, long nowUnix)
        {
            LeaderboardsView v = new LeaderboardsView();
            v.Board = _selected;
            LeaderboardBoard[] order = TabOrder();
            for (int i = 0; i < order.Length; i++)
            {
                LeaderboardTabView t = new LeaderboardTabView();
                t.Board = order[i];
                t.Label = TabLabel(order[i]);
                t.Selected = order[i] == _selected;
                v.Tabs.Add(t);
            }
            v.Caption = Caption(_selected);
            v.ValueHeader = ValueHeader(_selected);
            v.LockLine = LockLineOf(content, profile, _selected);
            v.SubmitLine = SubmitLine(_selected);

            long serverNow = nowUnix + (_serverOffsetKnown ? _serverOffset : 0L);
            if (LeaderboardCodec.IsWeekly(_selected))
            {
                long reset = 0;
                if (_lastSubmit != null && _lastSubmit.WeekStartUnix > 0) reset = _lastSubmit.WeekStartUnix + 7L * 86400L;
                if (reset <= serverNow) reset = LeaderboardCodec.NextWeekResetUnix(serverNow);
                v.ResetInSeconds = reset - serverNow;
                v.ResetLine = "Resets in " + ScreenText.Countdown(v.ResetInSeconds);
            }
            else
            {
                v.ResetLine = "All time";
            }

            Cached cached;
            bool haveRows = _cache.TryGetValue(_selected, out cached);

            if (_offline && !haveRows)
            {
                v.Phase = ScreenPhase.Offline;
                v.Status = _offlineReason.Length > 0 ? _offlineReason : "You are offline, so the boards cannot be reached.";
                v.Retry = _offlineCanRetry ? ButtonModel.On("TRY AGAIN") : ButtonModel.Hidden();
                v.Refresh = ButtonModel.Hidden();
                return v;
            }

            if (!haveRows)
            {
                if (_failure != LeaderboardFailure.None)
                {
                    v.Phase = ScreenPhase.Error;
                    v.Status = FailureSentence(_failure);
                    v.Retry = CanRetry(_failure) ? ButtonModel.On("TRY AGAIN") : ButtonModel.Hidden();
                    v.Refresh = ButtonModel.Hidden();
                    return v;
                }
                v.Phase = ScreenPhase.Loading;
                v.ShowSpinner = true;
                v.Refresh = ButtonModel.Off("REFRESH", "Loading the board.");
                return v;
            }

            // Rows are known: draw them, whatever is happening on top.
            FillRows(v, cached.Rows);
            v.UpdatedLine = Updated(nowUnix - cached.LoadedUnix);
            v.Phase = v.Rows.Count == 0 && v.PinnedSelf == null ? ScreenPhase.Empty : ScreenPhase.Ready;
            if (v.Phase == ScreenPhase.Empty)
                v.Status = LeaderboardCodec.IsWeekly(_selected) ? "Nobody has climbed this week yet." : "Nobody is on this board yet.";

            if (_offline) v.Banner = "Offline. Showing the board as it was " + Ago(nowUnix - cached.LoadedUnix) + ".";
            else if (_failure != LeaderboardFailure.None) v.Banner = "Could not refresh. Showing the board as it was " + Ago(nowUnix - cached.LoadedUnix) + ".";

            if (_loading)
            {
                v.ShowSpinner = true;
                v.Refresh = ButtonModel.Off("REFRESHING", "Loading the board.");
            }
            else if (_offline && !_offlineCanRetry)
            {
                v.Refresh = ButtonModel.Off("REFRESH", _offlineReason);
            }
            else
            {
                v.Refresh = ButtonModel.On("REFRESH");
            }
            return v;
        }

        private static void FillRows(LeaderboardsView v, List<LeaderboardRow> rows)
        {
            bool meSeen = false;
            for (int i = 0; i < rows.Count; i++)
            {
                LeaderboardRow r = rows[i];
                LeaderboardRowView rv = new LeaderboardRowView();
                rv.Rank = r.Rank;
                rv.RankText = "#" + ScreenText.Group(r.Rank);
                rv.Name = r.DisplayName.Length > 0 ? r.DisplayName : "Unnamed";
                rv.ValueText = ValueText(v.Board, r.Value);
                rv.IsMe = r.IsMe;
                if (r.IsMe)
                {
                    meSeen = true;
                    v.MyRankLine = "You are " + rv.RankText;
                    // leaderboard_top returns the caller's row after the top list when it ranks below it.
                    if (r.Rank > Limit) { v.PinnedSelf = rv; continue; }
                }
                v.Rows.Add(rv);
            }
            if (!meSeen) v.MyRankLine = "You are not on this board yet";
        }

        private string SubmitLine(LeaderboardBoard board)
        {
            switch (_submitFailure)
            {
                case LeaderboardFailure.NoCloudSave: return "Sync your save from the Account screen to appear on the boards.";
                case LeaderboardFailure.SaveTooOld: return "Update the game to appear on the boards.";
                case LeaderboardFailure.NotSignedIn: return "Sign in again from the Account screen to appear on the boards.";
            }
            if (_lastSubmit == null) return "";
            LeaderboardBoardResult r = board == LeaderboardBoard.Proving ? _lastSubmit.Proving
                                     : board == LeaderboardBoard.Campaign ? _lastSubmit.Campaign
                                     : _lastSubmit.Recital;
            string noun = Noun(board);
            switch (r.Verdict)
            {
                case LeaderboardVerdict.Raised: return "Your new best, " + noun + " " + ScreenText.Group(r.Value) + ", is on the board.";
                case LeaderboardVerdict.Kept: return r.Value > 0 ? "Your best on this board is " + noun + " " + ScreenText.Group(r.Value) + "." : "";
                case LeaderboardVerdict.Refused: return "Your latest result could not be added yet. It is checked again on your next visit.";
            }
            return "";
        }

        private static string LockLineOf(ContentDatabase content, PlayerProfile profile, LeaderboardBoard board)
        {
            if (board == LeaderboardBoard.Campaign) return "";
            Feature f = board == LeaderboardBoard.Proving ? Feature.Proving : Feature.Recital;
            if (content == null) return "";
            // Asked through the feature's own door, the way every screen asks (OnboardingChecks holds it).
            List<FeatureEntryPoint> doors = FeatureEntryPoints.Of(f);
            if (doors.Count == 0 || FeatureEntryPoints.Allows(content, profile, doors[0].Id)) return "";
            return FeatureGate.LockLine(content, f);
        }

        public static string TabLabel(LeaderboardBoard board)
        {
            switch (board)
            {
                case LeaderboardBoard.Recital: return "DEEP RECITAL";
                case LeaderboardBoard.RecitalWeek:
                case LeaderboardBoard.RecitalWeekClimb: return "THIS WEEK";
                case LeaderboardBoard.Proving: return "THE PROVING";
                default: return "CAMPAIGN";
            }
        }

        public static string Caption(LeaderboardBoard board)
        {
            switch (board)
            {
                case LeaderboardBoard.Recital: return "Highest floor reached in the Deep Recital";
                case LeaderboardBoard.RecitalWeek: return "Highest Deep Recital floor this week";
                case LeaderboardBoard.RecitalWeekClimb: return "Deep Recital floors climbed this week";
                case LeaderboardBoard.Proving: return "Highest standing in the Proving";
                default: return "Campaign stages cleared";
            }
        }

        public static string ValueHeader(LeaderboardBoard board)
        {
            switch (board)
            {
                case LeaderboardBoard.Recital:
                case LeaderboardBoard.RecitalWeek: return "FLOOR";
                case LeaderboardBoard.RecitalWeekClimb: return "CLIMBED";
                case LeaderboardBoard.Proving: return "STANDING";
                default: return "STAGES";
            }
        }

        private static string Noun(LeaderboardBoard board)
        {
            switch (board)
            {
                case LeaderboardBoard.Proving: return "standing";
                case LeaderboardBoard.Campaign: return "stages cleared";
                default: return "floor";
            }
        }

        private static string ValueText(LeaderboardBoard board, long value)
        {
            return board == LeaderboardBoard.RecitalWeekClimb ? "+" + ScreenText.Group(value) : ScreenText.Group(value);
        }

        public static string FailureSentence(LeaderboardFailure failure)
        {
            switch (failure)
            {
                case LeaderboardFailure.NotSignedIn: return "Your session has ended. Sign in again from the Account screen.";
                case LeaderboardFailure.NotDeployed: return "The leaderboards are not open yet.";
                case LeaderboardFailure.Offline: return "You are offline, so the boards cannot be reached.";
                default: return "The board could not be loaded. Check your connection and try again.";
            }
        }

        private static bool CanRetry(LeaderboardFailure failure)
        {
            return failure != LeaderboardFailure.NotDeployed;
        }

        private static string Updated(long age)
        {
            return age < 60 ? "Updated just now" : "Updated " + ScreenText.Countdown(age) + " ago";
        }

        private static string Ago(long age)
        {
            return age < 60 ? "a moment ago" : ScreenText.Countdown(age) + " ago";
        }
    }
}
