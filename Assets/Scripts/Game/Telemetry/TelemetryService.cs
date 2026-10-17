using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Emberfall.Core.Json;
using Emberfall.Core.Model;
using Emberfall.Core.Save;
using Emberfall.Core.Session;
using Emberfall.Core.Telemetry;
using Emberfall.Game.Cloud;
using Emberfall.UI;

namespace Emberfall.Game.Telemetry
{
    /// <summary>
    /// THE PLAY STATS, WIRED (Docs/DESIGN-TELEMETRY.md). Everything that decides what may be
    /// recorded, queued or sent lives in Emberfall.Core.Telemetry and is proved by the harness;
    /// this class is the thin glue: it owns the device file, listens to the game, and sends.
    ///
    /// IT ONLY WATCHES. The campaign, level up and summon moments arrive through TelemetryHooks
    /// from GameSession; the screens through the router and the panel host events the onboarding
    /// coach already uses; the onboarding step by reading Profile.TutorialStep about once a
    /// second. Nothing here changes a profile, a screen or a fight.
    ///
    /// A SESSION is one stretch in the foreground: it starts at launch and at resume and ends at
    /// pause and at quit, so the median in the owner's queries is the length of a sitting.
    ///
    /// SENDING is a no-op unless every one of these holds: stats are on (the player said Yes), this
    /// is a player build and not the editor, supabase.json is present and configured. The request
    /// uses its own SupabaseClient with NO user token, so it goes out as the anonymous role and
    /// cannot be tied to an account. It is silent on every failure: no log, no toast, no retry
    /// beyond the recorder's three tries per batch, and at most MaxSendsPerSession requests.
    /// </summary>
    public sealed class TelemetryService : ITelemetrySink
    {
        public const string FileName = "emberfall_telemetry.json";

        /// <summary>Requests per run of the game. Events are capped separately by the recorder.</summary>
        public const int MaxSendsPerSession = 40;

        /// <summary>Editor only: SessionState key the capture tool sets to let the consent prompt
        /// appear. Off by default so the existing capture targets show what they always showed.</summary>
        public const string PromptInEditorKey = "Emberfall.TelemetryPrompt";

        private const float PollSeconds = 1f;
        private const float FlushSeconds = 60f;

        public static TelemetryService Instance { get; private set; }

        private readonly GameApp _app;
        private readonly TelemetryRecorder _rec;
        private SupabaseClient _client;
        private bool _sending, _sessionOpen;
        private int _sends;
        private int _lastStep = -1;
        private float _stretchStart;
        private readonly HashSet<ScreenId> _opened = new HashSet<ScreenId>();

        /// <summary>Builds the service once and starts listening. Never throws: a failure here
        /// leaves the game without stats and otherwise untouched.</summary>
        public static TelemetryService Install(GameApp app)
        {
            if (!TelemetrySwitch.Enabled || app == null) return null;
            if (Instance != null) return Instance;
            try
            {
                Instance = new TelemetryService(app);
            }
            catch (Exception)
            {
                Instance = null;
                TelemetryHooks.Sink = null;
            }
            return Instance;
        }

        private TelemetryService(GameApp app)
        {
            _app = app;
            _rec = new TelemetryRecorder(new FileTelemetryStore(Path.Combine(Application.persistentDataPath, FileName)),
                                         NewInstallId, BuildLabel());
            _lastStep = CurrentStep();

            TelemetryHooks.Sink = this;
            if (app.Router != null) app.Router.Shown += OnShown;
            if (app.Panels != null) app.Panels.Shown += OnShown;

            if (_rec.IsOn) OpenSession();
            app.StartCoroutine(Loop());
            // One line at boot, like the cloud's, so a log says whether the stats were installed and
            // what the player had answered. It says nothing about any event.
            Debug.Log("Emberfall: play stats installed, answer on file: " + _rec.Consent + ".");
        }

        private static string NewInstallId()
        {
            return Guid.NewGuid().ToString();
        }

        /// <summary>The row's build column: the version and the commit the player was built from,
        /// DEV after them on a development player, cut to the server's 64 characters.</summary>
        private static string BuildLabel()
        {
            string label = Application.version + " (" + BuildStamp.Commit + ")";
#if DEVELOPMENT_BUILD
            label += " DEV";
#endif
            if (label.Length > TelemetrySchema.MaxBuildChars) label = label.Substring(0, TelemetrySchema.MaxBuildChars);
            return label;
        }

        // ------------------------------------------------------------------ what the screens ask

        public bool IsOn { get { return _rec.IsOn; } }

        /// <summary>The player has not answered, so the prompt is owed. False with the switch off.</summary>
        public bool Pending { get { return _rec.Pending; } }

        /// <summary>
        /// Whether the consent prompt should be up now. In the editor it is held off unless a
        /// capture asked for it, so the existing capture targets are untouched.
        /// </summary>
        public bool ShouldPrompt(bool onHub, bool nothingOpen)
        {
#if UNITY_EDITOR
            if (!UnityEditor.SessionState.GetBool(PromptInEditorKey, false)) return false;
#endif
            if (_app.Session == null) return false;
            return TelemetryPromptRule.ShouldShow(_rec, _app.Session.Profile, onHub, nothingOpen);
        }

        /// <summary>
        /// The player's answer, from the prompt and from Settings. A Yes queues, in this order,
        /// stats_opt_change, session_start, and (if the first session is not over) the step the
        /// profile is on, so a Yes at the Recall boundary reports step 1. A No deletes the id and
        /// the unsent queue and sends nothing.
        /// </summary>
        public void Answer(bool yes)
        {
            try
            {
                if (!_rec.SetConsent(yes)) return;
                if (!yes)
                {
                    _sessionOpen = false;
                    return;
                }
                OpenSession();
                int step = CurrentStep();
                _lastStep = step;
                if (step >= 0 && !Onboarding.IsComplete(_app.Session.Profile))
                    Record(TelemetrySchema.OnboardingStep, TelemetrySchema.StepPayload(step));
                StartFlush();
            }
            catch (Exception) { }
        }

        // ------------------------------------------------------------------ the sink (GameSession taps)

        public void Campaign(string eventName, string stage, int wave, int battle, int teamPower, int recommendedPower)
        {
            Record(eventName, TelemetrySchema.CampaignPayload(stage, wave, battle, teamPower, recommendedPower));
        }

        public void HeroLevelUp(int count)
        {
            Record(TelemetrySchema.HeroLevelUp, TelemetrySchema.CountPayload(count));
        }

        public void Summon(int count)
        {
            Record(TelemetrySchema.Summon, TelemetrySchema.CountPayload(count));
        }

        // ------------------------------------------------------------------ what it listens to

        private void OnShown(ScreenId id)
        {
            try
            {
                if (!_rec.IsOn || !_opened.Add(id)) return;
                Record(TelemetrySchema.FeatureOpened, TelemetrySchema.ScreenPayload(id.ToString()));
            }
            catch (Exception) { }
        }

        private int CurrentStep()
        {
            return _app.Session != null && _app.Session.Profile != null ? _app.Session.Profile.TutorialStep : -1;
        }

        private void Poll()
        {
            int step = CurrentStep();
            if (step < 0 || step == _lastStep) return;
            _lastStep = step;
            if (_rec.IsOn) Record(TelemetrySchema.OnboardingStep, TelemetrySchema.StepPayload(step));
        }

        private IEnumerator Loop()
        {
            WaitForSecondsRealtime wait = new WaitForSecondsRealtime(PollSeconds);
            // The first send comes a few seconds after launch, then once a minute.
            float sinceFlush = FlushSeconds - 5f;
            bool firstTick = true;
            while (true)
            {
                yield return wait;
                if (firstTick)
                {
                    // Proves the player loop is running frames at all, for a log that otherwise goes quiet.
                    firstTick = false;
                    Debug.Log("Emberfall: play stats loop is ticking.");
                }
                try
                {
                    Poll();
                    sinceFlush += PollSeconds;
                    if (sinceFlush >= FlushSeconds)
                    {
                        sinceFlush = 0f;
                        StartFlush();
                    }
                }
                catch (Exception) { }
            }
        }

        private void Record(string name, JsonValue payload)
        {
            try { _rec.Record(name, payload); } catch (Exception) { }
        }

        // ------------------------------------------------------------------ sessions

        private void OpenSession()
        {
            if (_sessionOpen || !_rec.IsOn) return;
            _sessionOpen = true;
            // The stretch is timed from the moment stats are on (launch with Yes on file, a resume,
            // or the Yes itself), never from before consent: seconds is time since this event.
            _stretchStart = Time.realtimeSinceStartup;
            Record(TelemetrySchema.SessionStart,
                   TelemetrySchema.SessionStartPayload(Application.platform.ToString(), SaveSerializer.CurrentVersion));
        }

        private void CloseSession()
        {
            if (!_sessionOpen) return;
            _sessionOpen = false;
            int seconds = Mathf.Max(0, Mathf.RoundToInt(Time.realtimeSinceStartup - _stretchStart));
            Record(TelemetrySchema.SessionEnd, TelemetrySchema.SecondsPayload(seconds));
        }

        /// <summary>The app left the foreground (paused) or came back. Called from GameApp.</summary>
        public static void OnPause(bool paused)
        {
            TelemetryService self = Instance;
            if (self == null) return;
            try
            {
                if (paused)
                {
                    self.CloseSession();
                    self.StartFlush();
                }
                else
                {
                    self.OpenSession();
                }
            }
            catch (Exception) { }
        }

        /// <summary>The app is quitting. The session is closed and queued on disk; there is no time to send.</summary>
        public static void OnQuit()
        {
            TelemetryService self = Instance;
            if (self == null) return;
            try { self.CloseSession(); } catch (Exception) { }
        }

        // ------------------------------------------------------------------ sending

        private void StartFlush()
        {
            if (_sending || _sends >= MaxSendsPerSession || !_rec.IsOn) return;
            // The editor never sends: checked before the config is even asked for, so a forced
            // offline capture builds no client and reads no supabase.json.
            if (Application.isEditor) return;
            SupabaseConfig config = SupabaseConfig.Current;
            if (!_rec.MaySend(false, config != null && config.Configured)) return;
            _app.StartCoroutine(Flush());
        }

        private IEnumerator Flush()
        {
            _sending = true;
            try
            {
                if (_client == null) _client = _app.CreateAnonymousClient();
                while (_client != null && _sends < MaxSendsPerSession)
                {
                    TelemetryBatch batch = _rec.TakeBatch();
                    if (batch == null) break;
                    _sends++;
                    SupabaseResponse response = null;
                    yield return _client.Send("POST", "rest/v1/play_events", _rec.BodyFor(batch), "return=minimal",
                                              delegate (SupabaseResponse r) { response = r; });
                    SendOutcome outcome = response != null ? TelemetryRecorder.OutcomeFor(response.Status) : SendOutcome.Retry;
                    _rec.ReportBatch(batch, outcome);
                    if (outcome == SendOutcome.Retry) break;
                }
            }
            finally
            {
                _sending = false;
            }
        }
    }
}
