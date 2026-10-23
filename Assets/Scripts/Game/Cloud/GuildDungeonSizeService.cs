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
    /// The functions Server/supabase/migrations/0022_guild_dungeon_size.sql adds beside the widened
    /// guild_dungeon_status (which GuildDungeonStatus.Read already reads through IGuildDungeonBackend.Status):
    /// the ladder for a member count, the longest ladder, the cycle cap, and the cycle snapshot row.
    ///
    /// Each one answers -1 (or null) with a sentence when the server predates 0022, so the ladder screen falls
    /// back to GuildDungeonScale's prediction rather than going blank. A plain class rather than a new member
    /// of IGuildDungeonBackend, so the editor's in-memory guild (GuildDungeonShots) does not have to grow.
    /// </summary>
    public sealed class GuildDungeonSizeService
    {
        private readonly CloudCoordinator _cloud;

        public GuildDungeonSizeService(CloudCoordinator cloud) { _cloud = cloud; }

        public bool Ready { get { return _cloud != null && _cloud.SignedIn; } }

        /// <summary>guild_dungeon_floors_for(p_members): the server's ladder for a guild of that size.</summary>
        public IEnumerator FloorsFor(int members, Action<int, string> done)
        {
            yield return Scalar("guild_dungeon_floors_for", GuildDungeonSizeCodec.FloorsForArgs(members), done);
        }

        /// <summary>guild_dungeon_floor_max(): the longest ladder any guild can have.</summary>
        public IEnumerator FloorMax(Action<int, string> done)
        {
            yield return Scalar("guild_dungeon_floor_max", "{}", done);
        }

        /// <summary>guild_dungeon_cycle_attempt_cap(): attempts one account may make in one cycle.</summary>
        public IEnumerator CycleAttemptCap(Action<int, string> done)
        {
            yield return Scalar("guild_dungeon_cycle_attempt_cap", "{}", done);
        }

        /// <summary>This guild's guild_dungeon_cycles row for a cycle (members read their own guild's rows).</summary>
        public IEnumerator CycleSnapshot(string guildId, int cycle, Action<GuildDungeonCycleSnapshot, string> done)
        {
            if (!Ready) { done(null, "You are offline, so the guild cannot be reached."); yield break; }
            string path = "rest/v1/guild_dungeon_cycles?select=" + GuildDungeonSizeCodec.CycleColumns
                        + "&guild_id=eq." + guildId + "&cycle=eq." + cycle;
            yield return _cloud.Client.Send("GET", path, null, delegate (SupabaseResponse response)
            {
                if (response.Status == 404) { done(null, "The guild dungeon's ladder sizes are not open on the server yet."); return; }
                if (!response.Ok) { done(null, response.Error); return; }
                done(GuildDungeonSizeCodec.ReadSnapshot(response.Body), "");
            });
        }

        private IEnumerator Scalar(string function, string args, Action<int, string> done)
        {
            if (!Ready) { done(-1, "You are offline, so the guild cannot be reached."); yield break; }
            yield return _cloud.Client.Send("POST", "rest/v1/rpc/" + function, args, delegate (SupabaseResponse response)
            {
                if (response.Status == 404) { done(-1, "The guild dungeon's ladder sizes are not open on the server yet."); return; }
                if (!response.Ok) { done(-1, response.Error); return; }
                int value = GuildDungeonSizeCodec.ReadScalarInt(response.Body);
                done(value, value < 0 ? "the server's answer was not a number" : "");
            });
        }
    }

    /// <summary>
    /// The ladder screen's half that talks: it owns a GuildDungeonLadderPresenter, loads the status, this
    /// cycle's attempts and the server's ladder for the live member count, and records an attempt. The screen
    /// reads View and calls the tap methods; it never calls a backend. Docs/ui-specs/R28-SCREENS.md, "Guild dungeon".
    /// </summary>
    public sealed class GuildDungeonLadderController
    {
        private readonly GuildDungeonLadderPresenter _presenter = new GuildDungeonLadderPresenter();
        private readonly IGuildDungeonBackend _backend;
        private readonly CloudCoordinator _cloud;
        private readonly GuildDungeonSizeService _size;
        private readonly MonoBehaviour _runner;
        private readonly GameSession _session;

        public event Action Changed;

        public GuildDungeonLadderController(IGuildDungeonBackend backend, CloudCoordinator cloud, MonoBehaviour runner, GameSession session)
        {
            _backend = backend;
            _cloud = cloud;
            _size = cloud != null ? new GuildDungeonSizeService(cloud) : null;
            _runner = runner;
            _session = session;
        }

        public GuildDungeonLadderPresenter Presenter { get { return _presenter; } }

        public GuildDungeonLadderView View
        {
            get
            {
                string self = _backend != null ? _backend.SelfUserId : "";
                return _presenter.Build(_session.Content, self, TimeUtil.NowUnix());
            }
        }

        /// <summary>The screen opened, REFRESH, TRY AGAIN.</summary>
        public void Refresh()
        {
            if (_backend == null)
            {
                _presenter.SetOffline("This build has no online features, so there is no guild dungeon.", false);
                Raise();
                return;
            }
            if (!_backend.Ready)
            {
                if (NotInGuild()) _presenter.SetNoGuild();
                else _presenter.SetOffline(_backend.NotReadyReason, _cloud == null || _cloud.Configured);
                Raise();
                return;
            }
            if (_runner != null) _runner.StartCoroutine(Load());
        }

        /// <summary>
        /// Files a fought attempt (the line-up screen fights it; this records it) and reloads either way: a stored
        /// attempt changes the counts, and a refused one means the copy on screen was stale.
        /// </summary>
        public void RecordAttempt(GuildDungeonAttempt attempt)
        {
            if (_backend == null || !_backend.Ready || attempt == null || _runner == null) return;
            _runner.StartCoroutine(Record(attempt));
        }

        private IEnumerator Record(GuildDungeonAttempt attempt)
        {
            _presenter.AttemptStarted(attempt.Floor);
            Raise();
            yield return _backend.Record(attempt, delegate (GuildDungeonAttempt stored, string error)
            {
                if (stored != null) _presenter.AttemptRecorded();
                else _presenter.AttemptRefused(error);
            });
            Raise();
            yield return Load();
        }

        private IEnumerator Load()
        {
            _presenter.BeginLoad();
            Raise();
            GuildDungeonStatus status = null;
            string error = "";
            yield return _backend.Status(delegate (GuildDungeonStatus s, string e) { status = s; error = e; });
            if (status == null)
            {
                if (NotInGuild()) _presenter.SetNoGuild();
                else _presenter.Failed(error);
                Raise();
                yield break;
            }

            List<GuildDungeonAttempt> attempts = null;
            yield return _backend.ListAttempts(status.Cycle, delegate (List<GuildDungeonAttempt> rows, string e) { attempts = rows; error = e; });
            if (attempts == null)
            {
                _presenter.Failed(error);
                Raise();
                yield break;
            }

            int live = LiveMembers();
            _presenter.Loaded(status, attempts, _backend.GuildName, live);
            Raise();

            // The server's ladder for the live count, for the next-cycle line. Optional: on a server before 0022
            // it answers -1 and the line keeps GuildDungeonScale's prediction.
            if (_size != null && _size.Ready && live > 0)
            {
                yield return _size.FloorsFor(live, delegate (int floors, string e)
                {
                    if (floors > 0) _presenter.ServerLadderFor(live, floors);
                });
                Raise();
            }
        }

        private bool NotInGuild()
        {
            return _cloud != null && _cloud.SignedIn && _cloud.Guild.Known && _cloud.Guild.Current == null;
        }

        private int LiveMembers()
        {
            if (_cloud == null || _cloud.Guild.Current == null) return -1;
            return _cloud.Guild.Current.MemberCount;
        }

        private void Raise()
        {
            if (Changed != null) Changed();
        }
    }
}
