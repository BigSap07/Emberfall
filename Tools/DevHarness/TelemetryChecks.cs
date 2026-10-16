using System;
using System.Collections.Generic;
using System.IO;
using Emberfall.Core.Data;
using Emberfall.Core.Json;
using Emberfall.Core.Model;
using Emberfall.Core.Save;
using Emberfall.Core.Session;
using Emberfall.Core.Telemetry;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE PLAY STATS (Docs/DESIGN-TELEMETRY.md), as checks.
    ///
    /// The privacy promise is checked as a property, not as a list of examples: every event the
    /// schema can build is compared with a HARD-CODED copy of the list the owner decided, so a field
    /// added to TelemetrySchema fails here until this copy is changed by hand in the same commit. The
    /// state machine is walked through its whole life: undecided (nothing happens), No (one word on
    /// disk), Yes (an id, a queue that survives a restart), and Yes to No (the id and the queue gone,
    /// and no event for it). The rest proves the caps, the batches, and that a disk that throws or a
    /// switch that is off never reaches the game.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The decided schema, 2026-10-16. Copied by hand on purpose: it is the owner's
        /// promise, and the code under test must not be the thing that defines it.</summary>
        private static readonly string[][] DecidedTelemetry =
        {
            new[] { "session_start", "platform", "saveVersion" },
            new[] { "session_end", "seconds" },
            new[] { "onboarding_step", "step" },
            new[] { "stage_attempt", "stage", "wave", "battle", "teamPower", "recommendedPower" },
            new[] { "wave_lost", "stage", "wave", "battle", "teamPower", "recommendedPower" },
            new[] { "stage_clear", "stage", "wave", "battle", "teamPower", "recommendedPower" },
            new[] { "hero_level_up", "count" },
            new[] { "summon", "count" },
            new[] { "feature_opened", "screen" },
            new[] { "stats_opt_change", "value" }
        };

        private const string TelemetryBuild = "0.3.0 (abc1234)";

        private static int _telemetryIds;

        private static string TelemetryTestId()
        {
            _telemetryIds++;
            return "00000000-0000-4000-8000-" + _telemetryIds.ToString("D12");
        }

        private static JsonValue TelemetrySample(string name)
        {
            switch (name)
            {
                case "session_start": return TelemetrySchema.SessionStartPayload("WindowsPlayer", 21);
                case "session_end": return TelemetrySchema.SecondsPayload(600);
                case "onboarding_step": return TelemetrySchema.StepPayload(2);
                case "stage_attempt":
                case "wave_lost":
                case "stage_clear": return TelemetrySchema.CampaignPayload("ch1_3", 2, 5, 1200, 1100);
                case "hero_level_up":
                case "summon": return TelemetrySchema.CountPayload(1);
                case "feature_opened": return TelemetrySchema.ScreenPayload("Heroes");
                case "stats_opt_change": return TelemetrySchema.OptOnPayload();
            }
            return JsonValue.NewObject();
        }

        private static TelemetryRecorder NewTelemetry(ITelemetryStore store)
        {
            return new TelemetryRecorder(store, TelemetryTestId, TelemetryBuild);
        }

        private static string TelemetryTempPath()
        {
            string dir = Path.Combine(Path.GetTempPath(), "emberfall-telemetry-check-" + Guid.NewGuid().ToString("N"));
            return Path.Combine(dir, "emberfall_telemetry.json");
        }

        private static void CheckTelemetry(ContentDatabase db)
        {
            Line("");
            Line("PLAY STATS (Docs/DESIGN-TELEMETRY.md: consent first, anonymous, a whitelist, insert only)");

            CheckTelemetrySchema();
            CheckTelemetryConsent();
            CheckTelemetryQueueAndCaps();
            CheckTelemetryRobustness();
            CheckTelemetryFlagOff();
            CheckTelemetryHooks(db);
        }

        // ------------------------------------------------------------------ the whitelist

        private static void CheckTelemetrySchema()
        {
            List<string> names = TelemetrySchema.EventNames;
            bool sameNames = names.Count == DecidedTelemetry.Length;
            for (int i = 0; sameNames && i < DecidedTelemetry.Length; i++)
                if (names[i] != DecidedTelemetry[i][0]) sameNames = false;
            Check(sameNames, "the schema allows exactly the " + DecidedTelemetry.Length + " decided events, in the decided order (" + string.Join(", ", names.ToArray()) + ")");

            bool sameFields = true, builderKeys = true, accepted = true, extraRefused = true, missingRefused = true;
            string firstProblem = "";
            for (int i = 0; i < DecidedTelemetry.Length; i++)
            {
                string name = DecidedTelemetry[i][0];
                List<string> have = TelemetrySchema.FieldNames(name);
                if (have.Count != DecidedTelemetry[i].Length - 1) sameFields = false;
                for (int k = 1; k < DecidedTelemetry[i].Length; k++)
                    if (!have.Contains(DecidedTelemetry[i][k])) sameFields = false;

                JsonValue sample = TelemetrySample(name);
                int keys = 0;
                foreach (string key in sample.Keys)
                {
                    keys++;
                    if (Array.IndexOf(DecidedTelemetry[i], key) < 1) builderKeys = false;
                }
                if (keys != DecidedTelemetry[i].Length - 1) builderKeys = false;

                string why = TelemetrySchema.Why(name, sample, TelemetryBuild);
                if (why.Length > 0) { accepted = false; firstProblem = name + ": " + why; }

                // A NEW FIELD MUST FAIL: the same valid payload with one more key is refused.
                JsonValue wider = JsonValue.Parse(sample.ToJson(false));
                wider.Set("deviceId", "x");
                if (TelemetrySchema.TryBuild(name, wider, TelemetryBuild) != null) extraRefused = false;
                // and with one key short.
                JsonValue shorter = JsonValue.NewObject();
                int skip = 0;
                foreach (string key in sample.Keys) { if (skip++ > 0) shorter.Set(key, sample[key]); }
                if (TelemetrySchema.TryBuild(name, shorter, TelemetryBuild) != null) missingRefused = false;
            }
            Check(sameFields, "each event carries exactly its decided payload keys and no others (the check reads a hard-coded copy of the owner's list)");
            Check(builderKeys, "and the payload builders the game calls produce exactly those keys");
            Check(accepted, "every decided event builds with a valid payload" + (firstProblem.Length > 0 ? " (" + firstProblem + ")" : ""));
            Check(extraRefused, "a payload with ONE EXTRA FIELD is refused for every event (a new field fails the build)");
            Check(missingRefused, "a payload with a key missing is refused for every event");

            Check(TelemetrySchema.TryBuild("device_info", TelemetrySample("session_end"), TelemetryBuild) == null
                  && TelemetrySchema.TryBuild("", TelemetrySample("session_end"), TelemetryBuild) == null
                  && TelemetrySchema.TryBuild(null, TelemetrySample("session_end"), TelemetryBuild) == null,
                  "an event name that is not on the list cannot be built");

            // free text and wrong kinds
            Check(TelemetrySchema.TryBuild("feature_opened", TelemetrySchema.ScreenPayload("my name is Sam"), TelemetryBuild) == null
                  && TelemetrySchema.TryBuild("feature_opened", TelemetrySchema.ScreenPayload(new string('a', TelemetrySchema.MaxIdChars + 1)), TelemetryBuild) == null
                  && TelemetrySchema.TryBuild("feature_opened", TelemetrySchema.ScreenPayload(""), TelemetryBuild) == null
                  && TelemetrySchema.TryBuild("feature_opened", TelemetrySchema.ScreenPayload("Heroes"), TelemetryBuild) != null
                  && TelemetrySchema.TryBuild("stage_clear", TelemetrySchema.CampaignPayload("ch1_3", 1, 1, 10, 10), TelemetryBuild) != null,
                  "a string field holds an id and nothing else: no space, no sentence, no empty value, at most " + TelemetrySchema.MaxIdChars + " characters");
            Check(TelemetrySchema.TryBuild("hero_level_up", TelemetrySchema.CountPayload(-1), TelemetryBuild) == null
                  && TelemetrySchema.TryBuild("session_end", JsonValue.NewObject().Set("seconds", "12"), TelemetryBuild) == null
                  && TelemetrySchema.TryBuild("session_end", JsonValue.NewObject().Set("seconds", 1.5), TelemetryBuild) == null
                  && TelemetrySchema.TryBuild("session_end", TelemetrySchema.SecondsPayload(TelemetrySchema.MaxInt + 1), TelemetryBuild) == null,
                  "a number field is a whole number from 0: not text, not fractional, not negative, not absurd");
            Check(TelemetrySchema.TryBuild("stats_opt_change", JsonValue.NewObject().Set("value", "off"), TelemetryBuild) == null
                  && TelemetrySchema.TryBuild("stats_opt_change", JsonValue.NewObject().Set("value", "on"), TelemetryBuild) != null,
                  "stats_opt_change can only say on: there is no off to send");
            Check(TelemetrySchema.TryBuild("session_end", TelemetrySample("session_end"), "") == null
                  && TelemetrySchema.TryBuild("session_end", TelemetrySample("session_end"), new string('b', TelemetrySchema.MaxBuildChars + 1)) == null
                  && TelemetrySchema.TryBuild("session_end", TelemetrySample("session_end"), new string('b', TelemetrySchema.MaxBuildChars)) != null,
                  "the build label is 1 to " + TelemetrySchema.MaxBuildChars + " printable characters, the server's own limit");
        }

        // ------------------------------------------------------------------ consent

        private static void CheckTelemetryConsent()
        {
            // ---- the default is No
            MemoryTelemetryStore mem = new MemoryTelemetryStore();
            TelemetryRecorder fresh = NewTelemetry(mem);
            Check(fresh.Consent == TelemetryConsent.Undecided && !fresh.IsOn && fresh.Pending && fresh.InstallId.Length == 0,
                  "a new install is Undecided: stats are off, no install id exists, the prompt is owed");
            bool recorded = fresh.Record("session_start", TelemetrySample("session_start"));
            Check(!recorded && fresh.Queued == 0 && fresh.TakeBatch() == null && mem.Writes == 0 && mem.Text == null,
                  "before Yes nothing is recorded, queued, written or sent (queue " + fresh.Queued + ", writes " + mem.Writes + ")");

            // ---- declining
            Check(fresh.SetConsent(false) && fresh.Consent == TelemetryConsent.No && !fresh.Pending && !fresh.IsOn,
                  "No is an answer: stats stay off and the prompt does not come back");
            JsonValue declined = JsonValue.Parse(mem.Text);
            int declinedKeys = 0;
            foreach (string key in declined.Keys) declinedKeys++;
            Check(declinedKeys == 1 && declined.GetString("consent", "") == "no" && !fresh.Record("summon", TelemetrySample("summon")),
                  "and the document holds that one word: no id, no queue, and nothing records");
            TelemetryRecorder reloadedNo = NewTelemetry(mem);
            Check(reloadedNo.Consent == TelemetryConsent.No && !reloadedNo.Pending, "a restart remembers the No and does not ask again");

            // ---- Yes
            MemoryTelemetryStore store = new MemoryTelemetryStore();
            TelemetryRecorder rec = NewTelemetry(store);
            Check(rec.SetConsent(true) && rec.IsOn && !rec.Pending, "Yes turns stats on");
            Guid parsed;
            Check(Guid.TryParse(rec.InstallId, out parsed) && rec.InstallId.Length == 36,
                  "and makes the install id only now, a random GUID (" + rec.InstallId + ")");
            Check(rec.Queued == 1 && rec.TakeBatch().Events[0].Name == "stats_opt_change"
                  && rec.TakeBatch().Events[0].Payload.GetString("value", "") == "on",
                  "Yes queues stats_opt_change {value: on} and nothing else");
            Check(!rec.SetConsent(true) && rec.Queued == 1, "answering Yes twice changes nothing and queues nothing more");
            rec.Record("session_start", TelemetrySample("session_start"));
            rec.Record("stage_clear", TelemetrySample("stage_clear"));
            rec.Record("not_an_event", TelemetrySample("stage_clear"));
            rec.Record("summon", JsonValue.NewObject().Set("count", 1).Set("name", "Kael"));
            Check(rec.Queued == 3, "an event off the list or with an extra field is refused at Record (queue " + rec.Queued + ", wanted 3)");

            // ---- Yes to No: the id and the queue go, and nothing is queued for it
            string oldId = rec.InstallId;
            TelemetryBatch inFlight = rec.TakeBatch();
            Check(rec.SetConsent(false) && !rec.IsOn && rec.Consent == TelemetryConsent.No, "turning stats off turns them off");
            JsonValue offDoc = JsonValue.Parse(store.Text);
            int offKeys = 0;
            foreach (string key in offDoc.Keys) offKeys++;
            Check(rec.InstallId.Length == 0 && rec.Queued == 0 && rec.TakeBatch() == null
                  && offKeys == 1 && !store.Text.Contains(oldId) && !store.Text.Contains("stage_clear"),
                  "and deletes the install id and the unsent queue at once: the id is gone, the queue is empty, the document is one word, no off event is queued");
            rec.ReportBatch(inFlight, SendOutcome.Sent);
            Check(rec.Queued == 0 && !rec.IsOn, "a batch that was in flight when stats went off is ignored when it reports");
            Check(rec.SetConsent(true) && rec.InstallId != oldId && rec.Queued == 1,
                  "turning them on again makes a NEW id: the old one is not brought back");
        }

        // ------------------------------------------------------------------ queue, restart, caps, batches

        private static void CheckTelemetryQueueAndCaps()
        {
            // ---- the queue survives a restart: written, reloaded, same id, same events in order
            string path = TelemetryTempPath();
            try
            {
                TelemetryRecorder a = NewTelemetry(new FileTelemetryStore(path));
                a.SetConsent(true);
                a.Record("session_start", TelemetrySample("session_start"));
                a.Record("onboarding_step", TelemetrySchema.StepPayload(1));
                a.Record("stage_attempt", TelemetrySample("stage_attempt"));
                string id = a.InstallId;

                TelemetryRecorder b = NewTelemetry(new FileTelemetryStore(path));
                TelemetryBatch batch = b.TakeBatch();
                Check(b.IsOn && b.InstallId == id && b.Queued == 4 && batch.Events.Count == 4
                      && batch.Events[0].Name == "stats_opt_change" && batch.Events[1].Name == "session_start"
                      && batch.Events[2].Payload.GetInt("step", -1) == 1 && batch.Events[3].Payload.GetString("stage", "") == "ch1_3"
                      && batch.Events[3].Build == TelemetryBuild,
                      "the queue survives a restart: same install id, same four events in the same order, same build label");
                Check(b.SetConsent(false) && !File.ReadAllText(path).Contains(id) && !File.ReadAllText(path).Contains("queue"),
                      "and opting out rewrites the file on disk without the id or the queue");
                Check(NewTelemetry(new FileTelemetryStore(path)).Consent == TelemetryConsent.No, "and a restart after that is still No");
            }
            finally
            {
                try { Directory.Delete(Path.GetDirectoryName(path), true); } catch (Exception) { }
            }

            // ---- a hand-edited queue cannot smuggle a field out
            MemoryTelemetryStore doctored = new MemoryTelemetryStore();
            doctored.Text = "{\"consent\":\"yes\",\"installId\":\"" + TelemetryTestId() + "\",\"queue\":["
                + "{\"e\":\"summon\",\"p\":{\"count\":1},\"b\":\"" + TelemetryBuild + "\"},"
                + "{\"e\":\"summon\",\"p\":{\"count\":1,\"playerName\":\"Sam\"},\"b\":\"" + TelemetryBuild + "\"},"
                + "{\"e\":\"chat\",\"p\":{\"text\":\"hi\"},\"b\":\"" + TelemetryBuild + "\"}]}";
            TelemetryRecorder loaded = NewTelemetry(doctored);
            Check(loaded.IsOn && loaded.Queued == 1, "rows with an extra field or an unknown event are dropped when the queue is loaded (kept " + loaded.Queued + " of 3)");

            // ---- the per-session cap
            MemoryTelemetryStore capStore = new MemoryTelemetryStore();
            TelemetryRecorder cap = NewTelemetry(capStore);
            cap.SetConsent(true);
            int accepted = 1;
            for (int i = 0; i < TelemetryRecorder.SessionCap + 100; i++)
                if (cap.Record("summon", TelemetrySample("summon"))) accepted++;
            Check(accepted == TelemetryRecorder.SessionCap,
                  "the session cap holds: " + accepted + " accepted of " + (TelemetryRecorder.SessionCap + 101) + " tried, the cap is " + TelemetryRecorder.SessionCap);
            Check(cap.Record("session_end", TelemetrySample("session_end")), "and a session that hit the cap can still end");
            Check(cap.Queued <= TelemetryQueue.StoredCap, "the stored queue never passes " + TelemetryQueue.StoredCap + " (" + cap.Queued + ")");

            // ---- the stored cap drops the OLDEST across restarts
            MemoryTelemetryStore longStore = new MemoryTelemetryStore();
            TelemetryRecorder first = NewTelemetry(longStore);
            first.SetConsent(true);
            for (int i = 0; i < TelemetryRecorder.SessionCap; i++) first.Record("hero_level_up", TelemetrySchema.CountPayload(i % 7));
            for (int run = 0; run < 3; run++)
            {
                TelemetryRecorder again = NewTelemetry(longStore);
                for (int i = 0; i < TelemetryRecorder.SessionCap; i++) again.Record("summon", TelemetrySample("summon"));
            }
            TelemetryRecorder final = NewTelemetry(longStore);
            TelemetryBatch head = final.TakeBatch();
            Check(final.Queued == TelemetryQueue.StoredCap && head != null && head.Events[0].Name == "summon",
                  "after many sessions offline the queue holds " + final.Queued + " (cap " + TelemetryQueue.StoredCap + ") and the oldest rows are the ones dropped");

            // ---- batches: size, body, outcomes
            MemoryTelemetryStore sendStore = new MemoryTelemetryStore();
            TelemetryRecorder send = NewTelemetry(sendStore);
            send.SetConsent(true);
            for (int i = 0; i < 59; i++) send.Record("summon", TelemetrySample("summon"));
            TelemetryBatch one = send.TakeBatch();
            Check(one.Events.Count == TelemetryRecorder.BatchSize && send.Queued == 60,
                  "a batch is at most " + TelemetryRecorder.BatchSize + " events and taking one removes nothing");

            JsonValue body = JsonValue.Parse(send.BodyFor(one));
            bool onlyColumns = body.IsArray && body.Count == one.Events.Count;
            for (int i = 0; onlyColumns && i < body.Count; i++)
            {
                int cols = 0;
                foreach (string key in body[i].Keys)
                {
                    cols++;
                    if (key != "install_id" && key != "event" && key != "payload" && key != "build") onlyColumns = false;
                }
                if (cols != 4 || body[i].GetString("install_id", "") != send.InstallId) onlyColumns = false;
            }
            Check(onlyColumns, "the request body is an array of rows with exactly install_id, event, payload and build: never id, never created_at");

            send.ReportBatch(one, SendOutcome.Sent);
            Check(send.Queued == 60 - TelemetryRecorder.BatchSize, "a batch the server took is removed from the queue");
            TelemetryBatch two = send.TakeBatch();
            send.ReportBatch(two, SendOutcome.Retry);
            send.ReportBatch(two, SendOutcome.Retry);
            int afterTwo = send.Queued;
            send.ReportBatch(two, SendOutcome.Retry);
            Check(afterTwo == 35 && send.Queued == 10,
                  "a batch that cannot be sent is kept through " + (TelemetryRecorder.MaxAttempts - 1) + " tries and dropped on try " + TelemetryRecorder.MaxAttempts + " (" + afterTwo + " queued, then " + send.Queued + ")");
            TelemetryBatch three = send.TakeBatch();
            send.ReportBatch(three, SendOutcome.Reject);
            Check(three.Events.Count == 10 && send.Queued == 0, "a batch the server refuses is dropped at once so it cannot jam the queue");

            Check(TelemetryRecorder.OutcomeFor(204) == SendOutcome.Sent && TelemetryRecorder.OutcomeFor(201) == SendOutcome.Sent
                  && TelemetryRecorder.OutcomeFor(0) == SendOutcome.Retry && TelemetryRecorder.OutcomeFor(503) == SendOutcome.Retry
                  && TelemetryRecorder.OutcomeFor(429) == SendOutcome.Retry && TelemetryRecorder.OutcomeFor(408) == SendOutcome.Retry
                  && TelemetryRecorder.OutcomeFor(400) == SendOutcome.Reject && TelemetryRecorder.OutcomeFor(403) == SendOutcome.Reject
                  && TelemetryRecorder.OutcomeFor(42501) == SendOutcome.Retry,
                  "HTTP statuses map as: 2xx sent, no network or 5xx or 408 or 429 retry, any other 4xx refused");
        }

        // ------------------------------------------------------------------ robustness

        private static void CheckTelemetryRobustness()
        {
            // ---- offline: no cloud config, and the editor, send nothing and raise nothing
            MemoryTelemetryStore store = new MemoryTelemetryStore();
            TelemetryRecorder rec = NewTelemetry(store);
            rec.SetConsent(true);
            rec.Record("session_start", TelemetrySample("session_start"));
            Check(!rec.MaySend(false, false) && !rec.MaySend(true, true) && rec.MaySend(false, true),
                  "no cloud config means nothing is sent, the editor never sends, a configured player build with Yes does");
            TelemetryRecorder undecided = NewTelemetry(new MemoryTelemetryStore());
            Check(!undecided.MaySend(false, true), "and nothing is sent before Yes even with a config");

            // ---- a disk that throws never reaches the game
            MemoryTelemetryStore broken = new MemoryTelemetryStore();
            broken.Throw = true;
            bool threw = false;
            TelemetryRecorder onBroken = null;
            try
            {
                onBroken = NewTelemetry(broken);
                onBroken.SetConsent(true);
                onBroken.Record("summon", TelemetrySample("summon"));
                onBroken.ReportBatch(onBroken.TakeBatch(), SendOutcome.Sent);
                onBroken.SetConsent(false);
            }
            catch (Exception) { threw = true; }
            Check(!threw && onBroken.Consent == TelemetryConsent.No, "a store that throws on read and write is swallowed: no exception, the state in memory stays right");

            MemoryTelemetryStore junk = new MemoryTelemetryStore();
            junk.Text = "{ this is not json";
            bool junkOk;
            try { TelemetryRecorder j = NewTelemetry(junk); junkOk = j.Consent == TelemetryConsent.Undecided && !j.IsOn; }
            catch (Exception) { junkOk = false; }
            Check(junkOk, "a corrupt document reads as Undecided: stats off, asked again, no exception");

            bool nullOk;
            try
            {
                TelemetryRecorder nullStore = new TelemetryRecorder(null, TelemetryTestId, TelemetryBuild);
                nullStore.SetConsent(true);
                nullStore.Record("summon", TelemetrySample("summon"));
                nullOk = true;
            }
            catch (Exception) { nullOk = false; }
            Check(nullOk, "a recorder with no store at all does not throw");
        }

        // ------------------------------------------------------------------ the taps on GameSession

        /// <summary>What the harness's stand-in for the game's TelemetryService does with the five
        /// taps: the same schema builders, into a real TelemetryRecorder. Counts every call so the
        /// checks can say what was observed, and every refusal so a stage id the whitelist would not
        /// accept is a failure rather than a silent drop.</summary>
        private sealed class HarnessSink : ITelemetrySink
        {
            public TelemetryRecorder Recorder;
            public int Attempts, Losses, Clears, LevelUps, LevelsGained, Summons, Refused, Calls;
            public readonly List<string> CampaignOrder = new List<string>();
            public int FirstPower = -1, FirstRecommended = -1, FirstWave = -1, FirstBattle = -1;
            public string FirstStage = "";

            public void Campaign(string eventName, string stage, int wave, int battle, int teamPower, int recommendedPower)
            {
                Calls++;
                if (eventName == TelemetrySchema.StageAttempt) Attempts++;
                else if (eventName == TelemetrySchema.WaveLost) Losses++;
                else if (eventName == TelemetrySchema.StageClear) Clears++;
                CampaignOrder.Add(eventName);
                if (FirstStage.Length == 0)
                {
                    FirstStage = stage;
                    FirstPower = teamPower;
                    FirstRecommended = recommendedPower;
                    FirstWave = wave;
                    FirstBattle = battle;
                }
                if (Recorder != null && !Recorder.Record(eventName, TelemetrySchema.CampaignPayload(stage, wave, battle, teamPower, recommendedPower))) Refused++;
            }

            public void HeroLevelUp(int count)
            {
                Calls++;
                LevelUps++;
                LevelsGained += count;
                if (Recorder != null && !Recorder.Record(TelemetrySchema.HeroLevelUp, TelemetrySchema.CountPayload(count))) Refused++;
            }

            public void Summon(int count)
            {
                Calls++;
                Summons += count;
                if (Recorder != null && !Recorder.Record(TelemetrySchema.Summon, TelemetrySchema.CountPayload(count))) Refused++;
            }
        }

        private sealed class ThrowingSink : ITelemetrySink
        {
            public void Campaign(string eventName, string stage, int wave, int battle, int teamPower, int recommendedPower)
            {
                throw new InvalidOperationException("sink told to fail");
            }
            public void HeroLevelUp(int count) { throw new InvalidOperationException("sink told to fail"); }
            public void Summon(int count) { throw new InvalidOperationException("sink told to fail"); }
        }

        /// <summary>What a scripted session did, counted from the game's own return values and not
        /// from the sink, so the two can be compared.</summary>
        private sealed class TelemetryScript
        {
            public string Save = "";
            public int Resolves, LostWaves, ClearedStages, LevelCalls, LevelsGained, Summons;
            public int PowerBeforeFirstResolve;
            public string FirstStageId = "";
            public int FirstRecommended;
        }

        /// <summary>A Keeper's profile played through the real calls: the recall, the first two
        /// stages a wave at a time, three level ups, four summons. Deterministic, so two runs differ
        /// only in what was listening.</summary>
        private static TelemetryScript PlayTelemetryScript(ContentDatabase db, HeroDef kael)
        {
            TelemetryScript t = new TelemetryScript();
            PlayerProfile p = Onboarding.CreateKeeperProfile(db, OnboardingNow);
            GameSession ws = new GameSession(db, p);
            Onboarding.CompleteStep(p, Onboarding.StepWake);
            Onboarding.Recall(ws, OnboardingNow);
            Onboarding.CompleteStep(p, Onboarding.StepPrologue);
            Onboarding.EnsureRecallHeroDeployed(ws);

            StageDef[] stages = { Onboarding.StageFor(db, Onboarding.StepWaveResult), Onboarding.StageFor(db, Onboarding.StepSecondStage) };
            t.FirstStageId = stages[0].Id;
            t.FirstRecommended = stages[0].RecommendedPower;
            t.PowerBeforeFirstResolve = p.EffectiveTeamPower(db);
            int seed = 7100;
            for (int s = 0; s < stages.Length; s++)
            {
                for (int n = 0; n < 14 && !p.HasClearedStage(stages[s].Id); n++)
                {
                    WaveResolution r = ws.ResolveWave(stages[s], seed, OnboardingNow + t.Resolves);
                    seed += 101;
                    if (r.Refused != WaveRefusal.None) break;
                    t.Resolves++;
                    if (r.LostBattle > 0) t.LostWaves++;
                    if (r.StageCleared) t.ClearedStages++;
                }
            }

            p.Gold += 100000;
            HeroInstance hero = p.FindHeroByDefId(Onboarding.RecallHero(db).Id);
            for (int i = 0; i < 3; i++)
            {
                LevelUpResult up = ws.LevelUpHero(hero, 1);
                if (up.Success) { t.LevelCalls++; t.LevelsGained += up.LevelsGained; }
            }
            for (int i = 0; i < 4; i++)
            {
                p.Shards = db.Balance.SummonCost;
                if (ws.Summon(9100 + i, OnboardingNow + 50 + i).Success) t.Summons++;
            }
            t.Save = SaveSerializer.Serialize(p);
            return t;
        }

        private static void CheckTelemetryHooks(ContentDatabase db)
        {
            HeroDef kael = Onboarding.RecallHero(db);
            if (kael == null) { Check(false, "the recall hero exists, so the telemetry taps can be exercised"); return; }

            // Every campaign stage id the taps will hand over must pass the whitelist, or its events would vanish silently.
            bool idsOk = db.AllStages.Count > 0;
            string badId = "";
            for (int i = 0; i < db.AllStages.Count; i++)
            {
                JsonValue payload = TelemetrySchema.CampaignPayload(db.AllStages[i].Id, 12, 12, 999999, db.AllStages[i].RecommendedPower);
                if (TelemetrySchema.TryBuild("stage_clear", payload, TelemetryBuild) == null) { idsOk = false; badId = db.AllStages[i].Id; }
            }
            Check(idsOk, "every one of the " + db.AllStages.Count + " campaign stage ids passes the whitelist as a stage" + (badId.Length > 0 ? " (refused: " + badId + ")" : ""));

            bool wasEnabled = TelemetrySwitch.Enabled;
            ITelemetrySink wasSink = TelemetryHooks.Sink;
            try
            {
                // ---- A: nothing listening. The session as origin/main plays it.
                TelemetryHooks.Sink = null;
                TelemetrySwitch.Enabled = true;
                TelemetryScript plain = PlayTelemetryScript(db, kael);

                // ---- B: a sink is attached but the switch is off: it must never be called.
                HarnessSink offSink = new HarnessSink();
                TelemetryHooks.Sink = offSink;
                TelemetrySwitch.Enabled = false;
                TelemetryScript off = PlayTelemetryScript(db, kael);
                TelemetrySwitch.Enabled = true;
                Check(offSink.Calls == 0, "with the switch off no tap reaches the sink (" + offSink.Calls + " calls)");
                Check(off.Save == plain.Save, "and the session with the switch off and a sink attached saves byte for byte what the session with nothing attached saves");

                // ---- C: the switch on, stats on, a recorder behind the sink.
                MemoryTelemetryStore store = new MemoryTelemetryStore();
                TelemetryRecorder rec = NewTelemetry(store);
                rec.SetConsent(true);
                HarnessSink sink = new HarnessSink();
                sink.Recorder = rec;
                TelemetryHooks.Sink = sink;
                TelemetryScript watched = PlayTelemetryScript(db, kael);
                Check(watched.Save == plain.Save,
                      "OBSERVATION CHANGES NOTHING: the session played with the taps live and stats on saves byte for byte what the unobserved session saves");

                Check(sink.Attempts == watched.Resolves && sink.Losses == watched.LostWaves && sink.Clears == watched.ClearedStages
                      && watched.Resolves > 0 && watched.ClearedStages > 0,
                      "the taps see exactly what the game did: " + sink.Attempts + " attempts = " + watched.Resolves + " wave resolves, "
                      + sink.Losses + " lost = " + watched.LostWaves + ", " + sink.Clears + " clears = " + watched.ClearedStages);
                Check(sink.LevelUps == watched.LevelCalls && sink.LevelsGained == watched.LevelsGained && sink.Summons == watched.Summons
                      && watched.LevelCalls > 0 && watched.Summons > 0,
                      "and the level ups (" + sink.LevelsGained + " levels in " + sink.LevelUps + " calls) and the summons (" + sink.Summons + ") are counted as the game did them");
                Check(sink.FirstStage == watched.FirstStageId && sink.FirstWave == 1 && sink.FirstBattle == 1
                      && sink.FirstPower == watched.PowerBeforeFirstResolve && sink.FirstRecommended == watched.FirstRecommended,
                      "the first stage_attempt carries the stage id, wave 1 battle 1, the line's EffectiveTeamPower (" + sink.FirstPower
                      + ") and the stage's recommendedPower (" + sink.FirstRecommended + ")");
                Check(sink.CampaignOrder.Count > 0 && sink.CampaignOrder[0] == TelemetrySchema.StageAttempt,
                      "an attempt is observed before the wave it starts is fought");
                int expectedQueued = 1 + sink.Calls;
                Check(sink.Refused == 0 && rec.Queued == expectedQueued,
                      "every observed moment became one queued event and none was refused by the whitelist (" + rec.Queued + " queued, " + expectedQueued + " expected)");

                // ---- D: a sink behind stats that are OFF records nothing, and the session is still the same one.
                TelemetryRecorder quiet = NewTelemetry(new MemoryTelemetryStore());
                quiet.SetConsent(false);
                HarnessSink quietSink = new HarnessSink();
                quietSink.Recorder = quiet;
                TelemetryHooks.Sink = quietSink;
                TelemetryScript declined = PlayTelemetryScript(db, kael);
                Check(declined.Save == plain.Save && quiet.Queued == 0 && quiet.TakeBatch() == null && quietSink.Refused == quietSink.Calls,
                      "with stats off the taps are heard, nothing is queued, and the session is the same session");

                // ---- E: a sink that throws cannot break the game.
                TelemetryHooks.Sink = new ThrowingSink();
                bool threw = false;
                TelemetryScript angry = null;
                try { angry = PlayTelemetryScript(db, kael); } catch (Exception) { threw = true; }
                Check(!threw && angry != null && angry.Save == plain.Save, "a sink that throws on every tap is swallowed: the session plays and saves the same");
            }
            finally
            {
                TelemetryHooks.Sink = wasSink;
                TelemetrySwitch.Enabled = wasEnabled;
            }
        }

        // ------------------------------------------------------------------ the switch

        private static void CheckTelemetryFlagOff()
        {
            bool was = TelemetrySwitch.Enabled;
            TelemetrySwitch.Enabled = false;
            try
            {
                MemoryTelemetryStore store = new MemoryTelemetryStore();
                store.Text = "{\"consent\":\"yes\",\"installId\":\"" + TelemetryTestId() + "\",\"queue\":[]}";
                TelemetryRecorder off = NewTelemetry(store);
                bool consented = off.SetConsent(true);
                bool recorded = off.Record("summon", TelemetrySample("summon"));
                Check(!off.IsOn && !off.Pending && !consented && !recorded && off.TakeBatch() == null
                      && store.Reads == 0 && store.Writes == 0 && store.Deletes == 0,
                      "with the switch off the recorder is inert: not on, no prompt owed, nothing recorded, and the store is never touched (reads " + store.Reads + ", writes " + store.Writes + ")");
                Check(!off.MaySend(false, true), "and nothing is sent");
            }
            finally
            {
                TelemetrySwitch.Enabled = was;
            }
        }
    }
}
