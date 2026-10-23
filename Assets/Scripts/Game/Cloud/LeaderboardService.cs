using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Emberfall.Core.Cloud;
using Emberfall.Core.Screens;
using Emberfall.Core.Session;

namespace Emberfall.Game.Cloud
{
    /// <summary>
    /// The two leaderboard functions of Server/supabase/migrations/0023_leaderboards.sql, as one surface.
    ///
    /// AN INTERFACE for GuildDungeonBackends' reason: editor tooling can put an in-memory board in front of
    /// the screen (LeaderboardBackends.Override) and capture every state without a live project. Bodies are
    /// read by LeaderboardCodec in Core either way, so both answer in the same shape.
    ///
    /// Coroutines with callbacks, like every service beside it. failure is None on success.
    /// </summary>
    public interface ILeaderboardBackend
    {
        /// <summary>True when there is a signed-in account to ask on behalf of.</summary>
        bool Ready { get; }

        /// <summary>Why not, in one sentence, when Ready is false.</summary>
        string NotReadyReason { get; }

        /// <summary>False when no attempt can ever reach a server in this build (no cloud config), so the
        /// screen draws no TRY AGAIN.</summary>
        bool CanEverConnect { get; }

        /// <summary>leaderboard_submit(): no argument. The server reads the caller's stored cloud save.</summary>
        IEnumerator Submit(Action<LeaderboardSubmitResult, LeaderboardFailure> done);

        /// <summary>leaderboard_top(board, limit): the top rows and the caller's own row after them.</summary>
        IEnumerator Top(LeaderboardBoard board, int limit, Action<List<LeaderboardRow>, LeaderboardFailure> done);
    }

    /// <summary>Which backend the Leaderboards screen uses.</summary>
    public static class LeaderboardBackends
    {
        /// <summary>Set only by editor tooling. Nothing in a player build writes it.</summary>
        public static ILeaderboardBackend Override;

        private static SupabaseLeaderboardBackend _live;

        /// <summary>The backend for this app, or null when the build has no cloud at all.</summary>
        public static ILeaderboardBackend For(GameApp app)
        {
            if (Override != null) return Override;
            if (app == null || app.Cloud == null) return null;
            if (_live == null || !_live.Wraps(app.Cloud)) _live = new SupabaseLeaderboardBackend(app.Cloud);
            return _live;
        }
    }

    /// <summary>The live backend: 0023's leaderboard_submit and leaderboard_top through PostgREST RPC.</summary>
    public sealed class SupabaseLeaderboardBackend : ILeaderboardBackend
    {
        private readonly CloudCoordinator _cloud;

        public SupabaseLeaderboardBackend(CloudCoordinator cloud) { _cloud = cloud; }

        public bool Wraps(CloudCoordinator cloud) { return ReferenceEquals(cloud, _cloud); }

        public bool Ready { get { return _cloud.SignedIn; } }

        public bool CanEverConnect { get { return _cloud.Configured; } }

        public string NotReadyReason
        {
            get
            {
                if (!_cloud.Configured) return "This build has no online features, so there are no boards.";
                return "You are offline, so the boards cannot be reached.";
            }
        }

        public IEnumerator Submit(Action<LeaderboardSubmitResult, LeaderboardFailure> done)
        {
            if (!Ready) { done(null, LeaderboardFailure.Offline); yield break; }
            yield return _cloud.Client.Send("POST", "rest/v1/rpc/leaderboard_submit", "{}",
                delegate (SupabaseResponse response)
                {
                    LeaderboardFailure failure = LeaderboardCodec.FailureOf(response.Status, response.Body);
                    if (failure != LeaderboardFailure.None || !response.Ok)
                    {
                        done(null, failure == LeaderboardFailure.None ? LeaderboardFailure.Server : failure);
                        return;
                    }
                    LeaderboardSubmitResult result = LeaderboardCodec.ReadSubmit(response.Body);
                    done(result, result == null ? LeaderboardFailure.Server : LeaderboardFailure.None);
                });
        }

        public IEnumerator Top(LeaderboardBoard board, int limit, Action<List<LeaderboardRow>, LeaderboardFailure> done)
        {
            if (!Ready) { done(null, LeaderboardFailure.Offline); yield break; }
            yield return _cloud.Client.Send("POST", "rest/v1/rpc/leaderboard_top", LeaderboardCodec.TopArgs(board, limit),
                delegate (SupabaseResponse response)
                {
                    LeaderboardFailure failure = LeaderboardCodec.FailureOf(response.Status, response.Body);
                    if (failure != LeaderboardFailure.None || !response.Ok)
                    {
                        // leaderboard_top raises PT400 only for an unknown board name, which this client never
                        // sends; read it as a server fault rather than as a save that is too old.
                        if (failure == LeaderboardFailure.SaveTooOld || failure == LeaderboardFailure.NoCloudSave
                            || failure == LeaderboardFailure.None) failure = LeaderboardFailure.Server;
                        done(null, failure);
                        return;
                    }
                    List<LeaderboardRow> rows = LeaderboardCodec.ReadTop(response.Body);
                    done(rows, rows == null ? LeaderboardFailure.Server : LeaderboardFailure.None);
                });
        }
    }

    /// <summary>
    /// The Leaderboards screen's half that talks: it owns a LeaderboardsPresenter, makes the calls the taps
    /// ask for, and reports every answer to the presenter. The screen reads View and calls the four tap
    /// methods below; it never calls a backend itself. Docs/ui-specs/R28-SCREENS.md, "Leaderboards".
    /// </summary>
    public sealed class LeaderboardsController
    {
        private readonly LeaderboardsPresenter _presenter = new LeaderboardsPresenter();
        private readonly ILeaderboardBackend _backend;
        private readonly MonoBehaviour _runner;
        private readonly GameSession _session;

        /// <summary>Raised after every state change, so the screen redraws from View.</summary>
        public event Action Changed;

        public LeaderboardsController(ILeaderboardBackend backend, MonoBehaviour runner, GameSession session)
        {
            _backend = backend;
            _runner = runner;
            _session = session;
        }

        public LeaderboardsPresenter Presenter { get { return _presenter; } }

        public LeaderboardsView View
        {
            get
            {
                return _presenter.Build(_session != null ? _session.Content : null,
                                        _session != null ? _session.Profile : null, TimeUtil.NowUnix());
            }
        }

        /// <summary>The screen opened: submit if due, then load the selected board.</summary>
        public void Open() { Refresh(); }

        /// <summary>REFRESH and TRY AGAIN.</summary>
        public void Refresh()
        {
            if (!CheckReady()) return;
            if (_runner != null) _runner.StartCoroutine(SubmitThenLoad(_presenter.Selected));
        }

        /// <summary>A tab. Loads only a board that has not been loaded on this visit.</summary>
        public void SelectTab(LeaderboardBoard board)
        {
            bool load = _presenter.Select(board);
            Raise();
            if (load && CheckReady() && _runner != null) _runner.StartCoroutine(Load(board));
        }

        private bool CheckReady()
        {
            if (_backend == null)
            {
                _presenter.SetOffline("This build has no online features, so there are no boards.", false);
                Raise();
                return false;
            }
            if (!_backend.Ready)
            {
                _presenter.SetOffline(_backend.NotReadyReason, _backend.CanEverConnect);
                Raise();
                return false;
            }
            _presenter.Online();
            return true;
        }

        private IEnumerator SubmitThenLoad(LeaderboardBoard board)
        {
            long now = TimeUtil.NowUnix();
            if (_presenter.ShouldSubmit(now))
            {
                _presenter.SubmitStarted(now);
                yield return _backend.Submit(delegate (LeaderboardSubmitResult result, LeaderboardFailure failure)
                {
                    if (failure == LeaderboardFailure.None) _presenter.SubmitAnswered(result, TimeUtil.NowUnix());
                    else _presenter.SubmitFailed(failure);
                });
            }
            yield return Load(board);
        }

        private IEnumerator Load(LeaderboardBoard board)
        {
            _presenter.BeginLoad(board);
            Raise();
            yield return _backend.Top(board, LeaderboardsPresenter.Limit, delegate (List<LeaderboardRow> rows, LeaderboardFailure failure)
            {
                if (failure == LeaderboardFailure.None) _presenter.TopLoaded(board, rows, TimeUtil.NowUnix());
                else _presenter.TopFailed(board, failure);
            });
            Raise();
        }

        private void Raise()
        {
            if (Changed != null) Changed();
        }
    }
}
