using System;
using System.Collections;
using UnityEngine;
using Emberfall.Core.Cloud;
using Emberfall.Core.Save;
using Emberfall.Core.Session;

namespace Emberfall.Game.Cloud
{
    /// <summary>
    /// What a sync decided, and everything a screen needs to act on it.
    ///
    /// Carries BOTH summaries whenever the action is AskPlayer, because a conflict card that
    /// describes only one side is not a choice. They are filled in even when the plan is not a
    /// conflict - a sync log that says which two saves it compared is worth the two parses.
    /// </summary>
    public sealed class SyncOutcome
    {
        public SyncAction Action;
        public string Reason = "";
        public string Error = "";

        public SaveSummary Local;
        public SaveSummary Cloud;

        /// <summary>The cloud row, when there was one. Held so a KeepCloud choice does not have to
        /// fetch it a second time and risk taking a different revision than the one described.</summary>
        public CloudSaveRecord Record;

        public bool Failed { get { return !string.IsNullOrEmpty(Error); } }
    }

    /// <summary>
    /// Uploading, downloading, and deciding which of the two should happen.
    ///
    /// THE LOCAL SAVE IS THE SOURCE OF TRUTH WHILE PLAYING. Nothing in this class is on the path
    /// of a battle, a summon or a level-up; the game writes to disk exactly as it always has and
    /// this pushes a copy afterwards. That is what keeps the game playable with no network and no
    /// account, which is not a fallback mode - it is the normal one, and the online path is the
    /// addition.
    ///
    /// The decision itself is NOT here. It is SyncPlanner, in Core, where it can be exercised for
    /// every case by the harness without a network or a Unity runtime - which is the only way a
    /// rule about data loss gets to be trusted. This class fetches the inputs, hands them over,
    /// and does what it is told.
    /// </summary>
    public sealed class CloudSaveService
    {
        private readonly SupabaseClient _client;
        private readonly AccountService _account;
        private readonly CloudLocalState _state;

        public CloudSaveService(SupabaseClient client, AccountService account, CloudLocalState state)
        {
            _client = client;
            _account = account;
            _state = state;
        }

        /// <summary>The cloud row for this user, or null when there is none. A missing row is an
        /// empty array from PostgREST rather than a 404, so "no save yet" arrives as success.</summary>
        public IEnumerator Fetch(Action<CloudSaveRecord, string> done)
        {
            if (!_account.SignedIn) { if (done != null) done(null, "not signed in"); yield break; }

            bool fresh = false;
            string tokenError = "";
            yield return _account.EnsureFreshToken(delegate (bool ok, string message) { fresh = ok; tokenError = message; });
            if (!fresh) { if (done != null) done(null, tokenError); yield break; }

            string path = "rest/v1/saves?select=user_id,save_version,revision,device_id,data,updated_at"
                        + "&user_id=eq." + _state.UserId;
            yield return _client.Send("GET", path, null, delegate (SupabaseResponse response)
            {
                if (!response.Ok) { if (done != null) done(null, response.Error); return; }
                if (done != null) done(CloudSaveCodec.ReadFirstRow(response.Body), "");
            });
        }

        /// <summary>
        /// Sends a save, claiming the revision the caller believes is stored.
        ///
        /// The database decides whether that claim is still true and refuses with a 409 if it is
        /// not - see push_save in 0002_saves.sql. A refusal here is not an error to retry blindly:
        /// it means somebody else wrote while this was being prepared, so the right answer is to
        /// fetch again and re-run the decision, which is what Sync does.
        ///
        /// On success the new revision becomes this device's base, which is the single write that
        /// makes the next sync's "local descends from the cloud" test mean anything.
        /// </summary>
        public IEnumerator Push(string saveJson, int saveVersion, int expectedRevision,
                                Action<CloudSaveRecord, string> done)
        {
            if (!_account.SignedIn) { if (done != null) done(null, "not signed in"); yield break; }
            if (string.IsNullOrEmpty(saveJson)) { if (done != null) done(null, "nothing to upload"); yield break; }

            // Built before the token refresh so a document that cannot be sent costs no request.
            // Null means the local file is not a JSON object - see WritePushBody for what sending
            // it anyway used to do to the cloud copy.
            string body = CloudSaveCodec.WritePushBody(expectedRevision, saveVersion, _state.DeviceId, saveJson);
            if (body == null)
            {
                if (done != null) done(null, "the save on this device could not be read, so it was not uploaded");
                yield break;
            }

            bool fresh = false;
            string tokenError = "";
            yield return _account.EnsureFreshToken(delegate (bool ok, string message) { fresh = ok; tokenError = message; });
            if (!fresh) { if (done != null) done(null, tokenError); yield break; }

            yield return _client.Send("POST", "rest/v1/rpc/push_save", body, delegate (SupabaseResponse response)
            {
                if (!response.Ok)
                {
                    if (done != null)
                        done(null, response.IsConflict
                            ? "the cloud save moved while this one was being sent - sync again"
                            : response.Error);
                    return;
                }

                CloudSaveRecord record = CloudSaveCodec.ReadFirstRow(response.Body);
                if (record == null) { if (done != null) done(null, "the upload returned no row"); return; }

                _state.BaseRevision = record.Revision;
                _state.LastSyncUnix = TimeUtil.NowUnix();
                _state.Save();
                if (done != null) done(record, "");
            });
        }

        /// <summary>
        /// The whole sync: fetch, decide, and carry out anything that does not need the player.
        ///
        /// UploadLocal and DownloadCloud happen here because neither can lose work - the first
        /// only when local provably descends from the cloud, the second only when this device has
        /// no save at all. AskPlayer stops and hands both summaries back; nothing is written in
        /// either direction until Resolve is called with an answer.
        ///
        /// `localJson` is passed in rather than read from SaveService, so this class has no
        /// opinion about where the local save lives and the harness can exercise the same path
        /// with a string it made up.
        /// </summary>
        public IEnumerator Sync(string localJson, Action<SyncOutcome> done)
        {
            SyncOutcome outcome = new SyncOutcome();

            CloudSaveRecord record = null;
            string fetchError = "";
            yield return Fetch(delegate (CloudSaveRecord r, string e) { record = r; fetchError = e; });
            if (!string.IsNullOrEmpty(fetchError))
            {
                outcome.Error = fetchError;
                if (done != null) done(outcome);
                yield break;
            }

            SyncInputs inputs = new SyncInputs();
            inputs.HasLocal = !string.IsNullOrEmpty(localJson);
            inputs.ReaderVersion = SaveSerializer.CurrentVersion;
            inputs.HasCloud = record != null;
            inputs.CloudSaveVersion = record != null ? record.SaveVersion : 0;
            inputs.CloudRevision = record != null ? record.Revision : 0;
            inputs.BaseRevision = _state.BaseRevision;

            SyncPlan plan = SyncPlanner.Decide(inputs);
            outcome.Action = plan.Action;
            outcome.Reason = plan.Reason;
            outcome.Record = record;
            outcome.Local = SaveSummaryReader.From(localJson);
            outcome.Cloud = record != null ? SaveSummaryReader.From(record.Data) : new SaveSummary();

            if (plan.Action == SyncAction.UploadLocal)
            {
                string pushError = "";
                yield return Push(localJson, SaveSerializer.CurrentVersion,
                                  SyncPlanner.ExpectedRevisionForUpload(inputs),
                                  delegate (CloudSaveRecord r, string e) { outcome.Record = r != null ? r : outcome.Record; pushError = e; });
                outcome.Error = pushError;
            }
            else if (plan.Action == SyncAction.DownloadCloud)
            {
                // Nothing is written to disk here. The caller owns the local save and the running
                // session, and handing it the document to adopt is a smaller surface than this
                // class reaching into GameApp to swap a profile out from under a live screen.
                _state.BaseRevision = record.Revision;
                _state.LastSyncUnix = TimeUtil.NowUnix();
                _state.Save();
            }

            if (done != null) done(outcome);
        }

        /// <summary>
        /// Carries out the player's answer to a conflict.
        ///
        /// KeepLocal uploads against the cloud's CURRENT revision, so it overwrites the other
        /// device's work - deliberately, because that is what the player was asked and answered.
        /// It is the only overwrite anywhere in this system, and it needs a person to have seen
        /// both saves described before it can happen.
        ///
        /// KeepCloud writes nothing to the server. It records the revision as this device's base
        /// and hands the document back; adopting it is the caller's job, for DownloadCloud's
        /// reason.
        /// </summary>
        public IEnumerator Resolve(ConflictChoice choice, SyncOutcome outcome, string localJson,
                                   Action<SyncOutcome> done)
        {
            if (outcome == null || outcome.Record == null)
            {
                if (done != null) done(outcome);
                yield break;
            }

            if (choice == ConflictChoice.KeepCloud)
            {
                _state.BaseRevision = outcome.Record.Revision;
                _state.LastSyncUnix = TimeUtil.NowUnix();
                _state.Save();
                outcome.Action = SyncAction.DownloadCloud;
                outcome.Reason = "the player chose the cloud save";
                if (done != null) done(outcome);
                yield break;
            }

            string pushError = "";
            yield return Push(localJson, SaveSerializer.CurrentVersion, outcome.Record.Revision,
                              delegate (CloudSaveRecord r, string e)
                              {
                                  if (r != null) outcome.Record = r;
                                  pushError = e;
                              });
            outcome.Action = SyncAction.UploadLocal;
            outcome.Reason = "the player chose this device's save";
            outcome.Error = pushError;
            if (done != null) done(outcome);
        }
    }
}
