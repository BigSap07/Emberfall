using System;
using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Telemetry
{
    public enum TelemetryConsent { Undecided, Yes, No }

    /// <summary>What a send came to, reduced to what the recorder acts on. Sent: the server took
    /// the batch. Retry: nothing is known to have landed (no network, a 5xx, a rate limit), so keep
    /// it and try later. Reject: the server refused the rows themselves, so retrying the same bytes
    /// can never work and one bad batch must not jam the queue.</summary>
    public enum SendOutcome { Sent, Retry, Reject }

    /// <summary>One batch on its way out: the events as they were when taken, the install id they
    /// belong to, and the generation that was current. A batch whose generation is stale (the
    /// player turned stats off while it was in flight) is ignored when it is reported.</summary>
    public sealed class TelemetryBatch
    {
        public List<TelemetryEvent> Events;
        public string InstallId = "";
        public int Generation;
    }

    /// <summary>
    /// THE PLAY-STATS STATE MACHINE (Docs/DESIGN-TELEMETRY.md). Pure: it holds the consent answer,
    /// the install id and the unsent queue, keeps them in an ITelemetryStore, and decides what may
    /// be recorded and sent. It knows nothing about Unity, the network or a screen.
    ///
    ///   Undecided  the starting state, and the one after a document that could not be read. The
    ///              effect is No: nothing is recorded, nothing is queued, nothing is written.
    ///   No         the player declined or turned stats off. The document holds that one word and
    ///              nothing else, so there is no id and no queue to find, and the prompt does not
    ///              come back.
    ///   Yes        the player agreed. A random install id was made at that moment. Events are
    ///              recorded, queued, persisted at once, and sent in batches.
    ///
    /// TURNING STATS OFF SENDS NOTHING. It deletes the id and the unsent queue and queues no event,
    /// so no "off" ever leaves the device. Turning them on again makes a NEW id.
    ///
    /// Every public member is safe to call whatever the disk does: a store that throws is
    /// swallowed and the state in memory stays right. With TelemetrySwitch off the recorder is
    /// inert and never touches its store, not even to read it.
    /// </summary>
    public sealed class TelemetryRecorder
    {
        /// <summary>Events accepted per run of the game, session_end aside. Past it Record says no.</summary>
        public const int SessionCap = 300;
        /// <summary>session_end has its own allowance, so a session that hit the cap still ends.</summary>
        public const int SessionEndReserve = 50;
        public const int BatchSize = 25;
        /// <summary>Retries of one batch before it is dropped.</summary>
        public const int MaxAttempts = 3;

        private readonly ITelemetryStore _store;
        private readonly Func<string> _newId;
        private readonly string _build;

        private TelemetryConsent _consent = TelemetryConsent.Undecided;
        private string _installId = "";
        private TelemetryQueue _queue = new TelemetryQueue();
        private int _gameplayRecorded, _endsRecorded, _generation, _attempts;

        public TelemetryRecorder(ITelemetryStore store, Func<string> newId, string build)
        {
            _store = store;
            _newId = newId;
            _build = build;
            if (!TelemetrySwitch.Enabled || store == null) return;
            Load();
        }

        public TelemetryConsent Consent { get { return _consent; } }
        public string InstallId { get { return _installId; } }
        public int Queued { get { return _queue.Count; } }
        public string Build { get { return _build; } }

        /// <summary>Stats are on: switch on, answer Yes, an id in hand.</summary>
        public bool IsOn
        {
            get { return TelemetrySwitch.Enabled && _consent == TelemetryConsent.Yes && _installId.Length > 0; }
        }

        /// <summary>The player has not answered yet, so the prompt is owed.</summary>
        public bool Pending
        {
            get { return TelemetrySwitch.Enabled && _consent == TelemetryConsent.Undecided; }
        }

        // ------------------------------------------------------------------ consent

        /// <summary>
        /// The player's answer. True when the state changed. Yes makes the install id and queues
        /// stats_opt_change "on"; No (or turning off) deletes the id and the queue and queues nothing.
        /// </summary>
        public bool SetConsent(bool yes)
        {
            if (!TelemetrySwitch.Enabled) return false;

            if (yes)
            {
                if (IsOn) return false;
                _consent = TelemetryConsent.Yes;
                _installId = NewId();
                _queue.Clear();
                _generation++;
                _attempts = 0;
                Persist();
                Record(TelemetrySchema.StatsOptChange, TelemetrySchema.OptOnPayload());
                return true;
            }

            if (_consent == TelemetryConsent.No) return false;
            _consent = TelemetryConsent.No;
            _installId = "";
            _queue.Clear();
            _generation++;
            _attempts = 0;
            Persist();
            return true;
        }

        // ------------------------------------------------------------------ recording

        /// <summary>Queues one event. False, and nothing stored, when stats are off, the event is
        /// not allowed by TelemetrySchema, or the session's cap is spent.</summary>
        public bool Record(string name, JsonValue payload)
        {
            if (!IsOn) return false;
            bool isEnd = name == TelemetrySchema.SessionEnd;
            if (isEnd ? _endsRecorded >= SessionEndReserve : _gameplayRecorded >= SessionCap) return false;
            TelemetryEvent ev = TelemetrySchema.TryBuild(name, payload, _build);
            if (ev == null) return false;
            if (isEnd) _endsRecorded++; else _gameplayRecorded++;
            _queue.Add(ev);
            Persist();
            return true;
        }

        // ------------------------------------------------------------------ sending

        /// <summary>The next batch to send, or null when stats are off or nothing is waiting. The
        /// events stay in the queue until ReportBatch says they landed.</summary>
        public TelemetryBatch TakeBatch()
        {
            if (!IsOn || _queue.Count == 0) return null;
            TelemetryBatch batch = new TelemetryBatch();
            batch.Events = _queue.Peek(BatchSize);
            batch.InstallId = _installId;
            batch.Generation = _generation;
            return batch;
        }

        /// <summary>
        /// The request body for a batch: a JSON array of rows with the four columns the client may
        /// write and nothing else. id and created_at are the server's.
        /// </summary>
        public string BodyFor(TelemetryBatch batch)
        {
            JsonValue rows = JsonValue.NewArray();
            if (batch != null)
            {
                for (int i = 0; i < batch.Events.Count; i++)
                {
                    JsonValue row = JsonValue.NewObject();
                    row.Set("install_id", batch.InstallId);
                    row.Set("event", batch.Events[i].Name);
                    row.Set("payload", batch.Events[i].Payload);
                    row.Set("build", batch.Events[i].Build);
                    rows.Add(row);
                }
            }
            return rows.ToJson(false);
        }

        public void ReportBatch(TelemetryBatch batch, SendOutcome outcome)
        {
            if (batch == null || !IsOn || batch.Generation != _generation) return;
            if (outcome == SendOutcome.Retry)
            {
                _attempts++;
                if (_attempts < MaxAttempts) return;
            }
            _attempts = 0;
            _queue.Remove(batch.Events);
            Persist();
        }

        /// <summary>The outcome a bare HTTP status stands for: 2xx landed, 0 (no network), 5xx, 408
        /// and 429 are worth another try, any other 4xx is the server refusing the rows.</summary>
        public static SendOutcome OutcomeFor(long status)
        {
            if (status >= 200 && status < 300) return SendOutcome.Sent;
            if (status == 0 || status >= 500 || status == 408 || status == 429) return SendOutcome.Retry;
            if (status >= 400 && status < 500) return SendOutcome.Reject;
            return SendOutcome.Retry;
        }

        /// <summary>
        /// May anything be sent right now. Never from the editor (a developer's own play must not
        /// reach the table), never without a cloud config, never with stats off.
        /// </summary>
        public bool MaySend(bool isEditor, bool cloudConfigured)
        {
            return IsOn && !isEditor && cloudConfigured;
        }

        // ------------------------------------------------------------------ the document

        private string NewId()
        {
            string id = "";
            try { id = _newId != null ? _newId() : ""; } catch (Exception) { id = ""; }
            Guid parsed;
            return Guid.TryParse(id, out parsed) ? parsed.ToString() : Guid.NewGuid().ToString();
        }

        private void Load()
        {
            try
            {
                string text = _store.Read();
                if (string.IsNullOrEmpty(text)) return;
                JsonValue root;
                string error;
                if (!JsonValue.TryParse(text, out root, out error) || root == null || !root.IsObject) return;

                string consent = root.GetString("consent", "");
                if (consent == "no")
                {
                    _consent = TelemetryConsent.No;
                    return;
                }
                if (consent != "yes") return;

                _consent = TelemetryConsent.Yes;
                Guid parsed;
                string id = root.GetString("installId", "");
                _installId = Guid.TryParse(id, out parsed) ? parsed.ToString() : NewId();
                _queue = TelemetryQueue.FromJson(root["queue"]);
            }
            catch (Exception)
            {
                _consent = TelemetryConsent.Undecided;
                _installId = "";
                _queue = new TelemetryQueue();
            }
        }

        private void Persist()
        {
            try
            {
                JsonValue root = JsonValue.NewObject();
                if (_consent == TelemetryConsent.Yes)
                {
                    root.Set("consent", "yes");
                    root.Set("installId", _installId);
                    root.Set("queue", _queue.ToJson());
                }
                else if (_consent == TelemetryConsent.No)
                {
                    root.Set("consent", "no");
                }
                else
                {
                    return;
                }
                _store.Write(root.ToJson(false));
            }
            catch (Exception)
            {
                // Silent by design: the state in memory is right, and a disk that will not take a
                // write must not reach the player.
            }
        }
    }
}
