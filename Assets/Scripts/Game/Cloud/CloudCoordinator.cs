using System;
using System.Collections;
using UnityEngine;
using Emberfall.Core.Cloud;
using Emberfall.Core.Model;
using Emberfall.Core.Save;
using Emberfall.Core.Session;

namespace Emberfall.Game.Cloud
{
    /// <summary>What the Account screen's status line is saying, reduced to the cases that differ.</summary>
    public enum CloudStatus
    {
        /// <summary>No config file, or an unfilled one. A fresh clone, and the normal state of any
        /// build nobody has pointed at a project - not an error and never reported as one.</summary>
        NotConfigured,

        /// <summary>Configured, but nothing has reached the server. Either the first sign-in has
        /// not happened yet or it failed, which from the player's side is one thing: offline.</summary>
        Offline,

        /// <summary>A request is in flight.</summary>
        Working,

        /// <summary>Signed in, and the last thing that was tried worked.</summary>
        Ready,

        /// <summary>Signed in, and the last thing that was tried did not. Message says what.</summary>
        Failed
    }

    /// <summary>
    /// WHEN the cloud is talked to, as distinct from HOW.
    ///
    /// AccountService, CloudSaveService, SocialService and GuildService each know how to perform
    /// one kind of request and nothing about the game around them - none of them knows what a
    /// screen is, when a player pauses, or that a save exists on disk. This class is the other
    /// half: it owns the policy, and it is the only thing in the project that both holds a GameApp
    /// and holds a service. Screens talk to it; it talks to them.
    ///
    /// THE RULE THAT SHAPES EVERY METHOD HERE: the local save is the source of truth while
    /// playing, and nothing below is allowed to make that less true. Every call is fire-and-forget
    /// from the game's point of view - no screen blocks on one, no battle waits on one, and a
    /// device with no network plays exactly as it did before any of this existed. The one place
    /// the player is ever stopped is a conflict, and that is not a network wait: it is a question
    /// that must not be answered by a machine.
    ///
    /// Coroutines with callbacks, like the services beneath it. There is no async/await in this
    /// project and this is not the place to introduce one.
    /// </summary>
    public sealed class CloudCoordinator
    {
        private readonly GameApp _app;
        private readonly SupabaseClient _client;
        private readonly AccountService _account;
        private readonly CloudSaveService _saves;
        private readonly SocialService _social;
        private readonly GuildService _guild;
        private readonly CloudLocalState _state;

        /// <summary>Set while any coroutine here is running, so two Sync Now taps cannot start two
        /// syncs against one revision and have the second refused by push_save for a conflict the
        /// first caused.</summary>
        private bool _busy;

        /// <summary>Raised whenever anything a screen shows about the cloud changes - status,
        /// profile, sync time. The Account screen subscribes; nothing else has to poll.</summary>
        public event Action Changed;

        public CloudCoordinator(GameApp app, SupabaseClient client, AccountService account,
                                CloudSaveService saves, SocialService social, GuildService guild,
                                CloudLocalState state)
        {
            _app = app;
            _client = client;
            _account = account;
            _saves = saves;
            _social = social;
            _guild = guild;
            _state = state;
        }

        public AccountService Account { get { return _account; } }
        public SocialService Social { get { return _social; } }
        public GuildService Guild { get { return _guild; } }
        public CloudSaveService Saves { get { return _saves; } }
        public CloudLocalState State { get { return _state; } }
        public SupabaseClient Client { get { return _client; } }

        public bool Configured { get { return _client != null && _client.Config.Configured; } }
        public bool SignedIn { get { return _account != null && _account.SignedIn; } }
        public bool Busy { get { return _busy; } }

        /// <summary>The account's own profile row, once fetched. Null until then; the Account
        /// screen renders the empty state rather than waiting.</summary>
        public AccountProfile Profile { get; private set; }

        public CloudStatus Status { get; private set; }

        /// <summary>The sentence under the status. Empty when there is nothing to add.</summary>
        public string Message { get; private set; }

        /// <summary>Unix seconds of the last successful sync, or 0. Read from device state rather
        /// than tracked here, so it survives a restart the way the base revision does.</summary>
        public long LastSyncUnix { get { return _state != null ? _state.LastSyncUnix : 0L; } }

        // ================================================================== boot

        /// <summary>
        /// Everything that happens about accounts at launch, in one coroutine so the order is
        /// visible rather than spread across callbacks: sign in, learn who that is, sync, publish.
        ///
        /// NOTHING HERE IS ON THE PATH TO THE MAIN MENU. GameApp starts this and carries on
        /// building screens; if it never finishes, or finishes badly, the game is already playable
        /// and stays that way. That is the whole of the offline story - there is no offline MODE,
        /// there is just a game that works and a network that may or may not be there.
        /// </summary>
        public IEnumerator Begin()
        {
            if (!Configured)
            {
                Set(CloudStatus.NotConfigured, _client != null ? _client.Config.Problem : "no cloud config");
                yield break;
            }

            Set(CloudStatus.Working, "signing in");
            bool ok = false;
            string error = "";
            yield return _account.Resume(delegate (bool success, string message) { ok = success; error = message; });

            if (!ok)
            {
                // Deliberately quiet. A failed sign-in at boot is a player with no signal, which
                // is not an event worth a toast on the main menu - the Account screen says
                // "offline" when they go looking, and the next sync attempt tries again.
                Set(CloudStatus.Offline, error);
                yield break;
            }

            Set(CloudStatus.Ready, "");
            yield return RefreshProfile(null);
            yield return RefreshGuild(null);
            yield return SyncRoutine(false, null);
        }

        /// <summary>
        /// Which guild this account is in, if any, and what it may do there.
        ///
        /// Fetched at boot rather than when the guild screen opens, for one reason: the main menu
        /// draws a GUILD button whose subtitle is the guild's name, and a button that says nothing
        /// until you press it has told the player to press it to find out. It is two small reads
        /// and it is off the path to a playable menu like everything else here.
        /// </summary>
        public IEnumerator RefreshGuild(Action<string> done)
        {
            if (_guild == null) { Report(done, "no guild service"); yield break; }
            if (!SignedIn) { Report(done, "not signed in"); yield break; }

            string error = "";
            yield return _guild.Refresh(delegate (string message) { error = message; });
            Raise();
            Report(done, error);
        }

        /// <summary>The display name and friend code. Cheap, and every screen that shows either
        /// wants both, so there is one fetch rather than two.</summary>
        public IEnumerator RefreshProfile(Action<string> done)
        {
            if (!SignedIn) { Report(done, "not signed in"); yield break; }

            string error = "";
            yield return _account.FetchOwnProfile(delegate (AccountProfile profile, string message)
            {
                if (profile != null) Profile = profile;
                error = message;
            });
            if (!string.IsNullOrEmpty(error)) Set(CloudStatus.Failed, error);
            else Raise();
            Report(done, error);
        }

        // ================================================================== sync

        /// <summary>
        /// A sync the player asked for. Reports through the callback so the button that started it
        /// can say what happened, which the silent boot sync has no need of.
        /// </summary>
        public IEnumerator SyncNow(Action<string> done)
        {
            return SyncRoutine(true, done);
        }

        /// <summary>
        /// Fetch, decide, and act - or stop and ask.
        ///
        /// The local save is written to disk FIRST and then read back, so what is uploaded is the
        /// exact document this device holds rather than a second serialisation of the same profile
        /// that would differ by a timestamp. See SaveService.ReadDocument.
        /// </summary>
        private IEnumerator SyncRoutine(bool playerAsked, Action<string> done)
        {
            if (!Configured) { Report(done, "there is no cloud configured for this build"); yield break; }
            if (!SignedIn)
            {
                // A player who taps Sync Now while offline gets one retry of the sign-in, because
                // "offline" at boot is very often "the wifi came back since".
                bool signedIn = false;
                string signInError = "";
                Set(CloudStatus.Working, "signing in");
                yield return _account.Resume(delegate (bool ok, string message) { signedIn = ok; signInError = message; });
                if (!signedIn)
                {
                    Set(CloudStatus.Offline, signInError);
                    Report(done, signInError);
                    yield break;
                }
                Set(CloudStatus.Ready, "");
                yield return RefreshProfile(null);
            }

            if (_busy) { Report(done, "a sync is already running"); yield break; }
            _busy = true;
            Set(CloudStatus.Working, "syncing");

            _app.SaveNow();
            string local = SaveService.ReadDocument();

            SyncOutcome outcome = null;
            yield return _saves.Sync(local, delegate (SyncOutcome result) { outcome = result; });

            _busy = false;

            if (outcome == null)
            {
                Set(CloudStatus.Failed, "the sync returned nothing");
                Report(done, "the sync returned nothing");
                yield break;
            }

            if (outcome.Failed)
            {
                Set(CloudStatus.Failed, outcome.Error);
                Report(done, outcome.Error);
                yield break;
            }

            switch (outcome.Action)
            {
                case SyncAction.AskPlayer:
                    // Nothing has moved in either direction and nothing will until a person
                    // answers. The chooser is opened here rather than by the caller so every path
                    // into a sync - boot, pause, the button - reaches the same question.
                    Set(CloudStatus.Ready, "waiting for you to choose which save to keep");
                    AskWhichSave(outcome, local, done);
                    yield break;

                case SyncAction.DownloadCloud:
                    AdoptCloudSave(outcome.Record);
                    Set(CloudStatus.Ready, "");
                    Report(done, "");
                    yield break;

                case SyncAction.RefuseCloudNewerVersion:
                    Set(CloudStatus.Failed, outcome.Reason);
                    Report(done, outcome.Reason);
                    yield break;
            }

            Set(CloudStatus.Ready, "");
            // The line lives on this device, so the line the cloud advertises is only current
            // while this device's save is. Publishing after an upload rather than beside it keeps
            // the two in the same order they are in fact in.
            yield return PublishSnapshotRoutine(null);
            Report(done, "");
            if (playerAsked && _app != null) _app.Notify("Synced.");
        }

        /// <summary>
        /// The one question this system asks, and the one place a save is ever overwritten.
        ///
        /// Both saves are described - name, level, stages cleared, heroes owned, when each was
        /// last written - because a conflict card describing one side is not a choice, it is a
        /// confirmation dialog wearing a choice's clothes. There is no default, no timeout and no
        /// "remember this", all three of which are ways of answering it without a person.
        /// </summary>
        private void AskWhichSave(SyncOutcome outcome, string localJson, Action<string> done)
        {
            if (_app == null || _app.Conflict == null)
            {
                Report(done, "a conflict needs answering but there is no screen to ask on");
                return;
            }

            _app.Conflict.Ask(outcome, delegate (ConflictChoice choice)
            {
                _client.Run(ResolveRoutine(choice, outcome, localJson, done));
            });
        }

        private IEnumerator ResolveRoutine(ConflictChoice choice, SyncOutcome outcome, string localJson,
                                           Action<string> done)
        {
            _busy = true;
            Set(CloudStatus.Working, choice == ConflictChoice.KeepCloud
                ? "taking the cloud save" : "sending this device's save");

            SyncOutcome resolved = null;
            yield return _saves.Resolve(choice, outcome, localJson,
                                        delegate (SyncOutcome result) { resolved = result; });
            _busy = false;

            if (resolved != null && resolved.Failed)
            {
                Set(CloudStatus.Failed, resolved.Error);
                Report(done, resolved.Error);
                yield break;
            }

            if (choice == ConflictChoice.KeepCloud)
            {
                AdoptCloudSave(outcome.Record);
                Set(CloudStatus.Ready, "");
                Report(done, "");
                yield break;
            }

            Set(CloudStatus.Ready, "");
            yield return PublishSnapshotRoutine(null);
            Report(done, "");
        }

        /// <summary>
        /// Takes a cloud save and becomes it.
        ///
        /// Written to disk and then RELOADED THROUGH THE ORDINARY LOAD PATH rather than
        /// deserialised straight into a session. The two are not the same: a launch reads the
        /// document through SaveService.Load, which refuses an unreadable save and an empty
        /// roster, and adopting by any other route would produce a running game that the next
        /// launch would not reproduce. Whatever this device is after a download must be exactly
        /// what it would be if it were relaunched, or the download is a state nobody can get back.
        ///
        /// The router is sent home afterwards because live screens hold references INTO the old
        /// profile - a hero list, a selected armor piece - and swapping the session under them
        /// leaves those pointing at heroes that no longer belong to anybody.
        /// </summary>
        private void AdoptCloudSave(CloudSaveRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.Data)) { ForgetBaseAfterFailedAdopt(); return; }
            if (_app == null) return;

            // READ BEFORE WRITE. This used to write the document over the local save first and
            // find out afterwards whether it loaded, so a cloud save this build cannot use - no
            // hero it knows, or not a save at all - replaced the file and survived only until
            // the next autosave put the old profile back, or until a crash made it permanent.
            string readError;
            PlayerProfile readable = SaveSerializer.Deserialize(record.Data, _app.Content, out readError);
            if (readable == null || readable.Heroes.Count == 0)
            {
                ForgetBaseAfterFailedAdopt();
                Set(CloudStatus.Failed, "the cloud save cannot be used by this build ("
                    + (readable == null ? readError : "no hero in it exists in this build") + ")");
                return;
            }

            if (!SaveService.WriteDocument(record.Data))
            {
                ForgetBaseAfterFailedAdopt();
                Set(CloudStatus.Failed, "the cloud save could not be written to this device");
                return;
            }

            if (!_app.AdoptSaveFromDisk())
            {
                ForgetBaseAfterFailedAdopt();
                Set(CloudStatus.Failed, "the cloud save was written but could not be read back");
                return;
            }

            // The team that is now on this device is not the team the cloud is advertising -
            // taking somebody's save wholesale changes the line completely. Forgetting the
            // fingerprint and publishing forces the snapshot to catch up now rather than waiting
            // for whatever the player next does to happen to change the team.
            _publishedFingerprint = "";
            _client.Run(PublishSnapshotRoutine(null));

            _app.Notify("Cloud save loaded.");
            Raise();
        }

        /// <summary>
        /// Undoes the base revision a download or a KeepCloud choice recorded BEFORE handing the
        /// document here. Left in place after a failed adopt it says "this device holds cloud
        /// revision N" while the device still holds its own save, so the next sync saw cloud ==
        /// base and uploaded that save over the one the player had just chosen to keep. Zero makes
        /// the next sync ask again instead. See Docs/CLOUD-GAPS.md.
        /// </summary>
        private void ForgetBaseAfterFailedAdopt()
        {
            if (_state == null) return;
            _state.BaseRevision = 0;
            _state.Save();
        }

        // ================================================================== the team snapshot

        /// <summary>
        /// The published line, as it was last sent. Compared against rather than remembered as a
        /// flag, so the upload happens when the LINE changed and not merely when something did.
        /// </summary>
        private string _publishedFingerprint = "";

        /// <summary>Set for the duration of a publish. Separate from _busy, which gates the
        /// player-facing actions: a snapshot upload is background work that must not make Sync Now
        /// report "a sync is already running", and equally must not stack two uploads because two
        /// saves landed inside one round trip.</summary>
        private bool _publishing;

        /// <summary>
        /// Called after every local save - see GameApp.SaveNow.
        ///
        /// THE HOOK IS THE SAVE, and that is the whole design of this. Every action that can
        /// change a published line - levelling a hero, equipping armor, rerolling it, assigning a
        /// relic, picking a talent, changing the team - is already followed by a save, because
        /// that is how this project has always recorded that something the player owns moved. So
        /// hooking the save catches all of them and cannot be missed by a screen that forgets to
        /// call something, which a dozen individual hooks certainly would be. It also cannot fire
        /// for a change that was not saved, which is the other half of being correct.
        ///
        /// What stops it being an upload per save is the fingerprint: the snapshot is rebuilt and
        /// compared, and the network is only touched when the bytes differ. Clearing idle income
        /// saves; it does not change the line, so it does not publish.
        /// </summary>
        public void PublishSnapshotIfChanged()
        {
            if (!SignedIn || _busy || _publishing) return;
            if (_app == null || _app.Session == null) return;

            string fingerprint = FingerprintOfCurrentLine();
            if (fingerprint == _publishedFingerprint) return;

            _client.Run(PublishSnapshotRoutine(null));
        }

        /// <summary>
        /// Sends the line, whether or not it moved. The sync path calls this rather than the
        /// conditional one above, because a sync is also the moment to repair a snapshot that a
        /// previous upload failed to land - the fingerprint would say "unchanged" and be right
        /// about this device while being wrong about the server.
        /// </summary>
        public IEnumerator PublishSnapshotRoutine(Action<string> done)
        {
            if (!SignedIn || _app == null || _app.Session == null) { Report(done, "not signed in"); yield break; }

            PlayerProfile profile = _app.Session.Profile;
            string label = string.IsNullOrEmpty(profile.PlayerName) ? "Wayfarer" : profile.PlayerName;
            TeamSnapshot snapshot = TeamSnapshotBuilder.From(_app.Session.Content, profile, label);
            if (snapshot.Units.Count == 0) { Report(done, "there is no line to publish"); yield break; }

            string fingerprint = FingerprintOf(snapshot);
            string error = "";
            _publishing = true;
            yield return _social.PublishSnapshot(snapshot,
                delegate (bool ok, string message) { error = ok ? "" : message; });
            _publishing = false;

            // Recorded only on success, so a failed upload is retried by the next change rather
            // than being remembered as done.
            if (string.IsNullOrEmpty(error)) _publishedFingerprint = fingerprint;
            Report(done, error);
        }

        private string FingerprintOfCurrentLine()
        {
            PlayerProfile profile = _app.Session.Profile;
            string label = string.IsNullOrEmpty(profile.PlayerName) ? "Wayfarer" : profile.PlayerName;
            return FingerprintOf(TeamSnapshotBuilder.From(_app.Session.Content, profile, label));
        }

        /// <summary>
        /// The snapshot as the column would hold it, plus the two columns beside it.
        ///
        /// The published document itself rather than a hash of it. It is a few hundred bytes for
        /// five heroes, it is built out of the same writer the upload uses - so nothing can differ
        /// between what is compared and what is sent - and a hash would buy nothing but the
        /// possibility of two different lines agreeing.
        /// </summary>
        private static string FingerprintOf(TeamSnapshot snapshot)
        {
            if (snapshot == null) return "";
            return snapshot.Label + "|" + snapshot.PowerScore + "|"
                 + TeamSnapshotCodec.WriteUnits(snapshot).ToJson(false);
        }

        // ================================================================== account actions

        /// <summary>
        /// True once this account has a display name the player chose, rather than the one the
        /// database gave it. False while the profile has not been fetched, so a caller that is
        /// about to decide whether to ask must have called RefreshProfile first.
        /// </summary>
        public bool NameChosen
        {
            get
            {
                if (Profile == null) return false;
                if (Profile.DisplayName != DisplayNameRules.Default) return true;
                return !string.IsNullOrEmpty(_state.UserId) && _state.ChosenNameFor == _state.UserId;
            }
        }

        /// <summary>Renames the account, then re-reads the profile so the screen shows what the
        /// server stored rather than what was typed.</summary>
        public IEnumerator SetDisplayName(string name, Action<string> done)
        {
            if (!SignedIn) { Report(done, "not signed in"); yield break; }
            _busy = true;
            CloudStatus before = Status;
            string beforeMessage = Message;
            Set(CloudStatus.Working, "saving your name");

            string error = "";
            yield return _account.SetDisplayName(name, delegate (bool ok, string message) { error = ok ? "" : message; });
            _busy = false;

            if (!string.IsNullOrEmpty(error))
            {
                // A refused name is an answer to the player, not a failed sync: the save is
                // exactly as synced as it was, so the status goes back to what it was rather than
                // reading LAST SYNC FAILED on the Account screen because a name had a newline in it.
                Set(before == CloudStatus.Working ? CloudStatus.Ready : before, beforeMessage);
                Report(done, error);
                yield break;
            }

            _state.ChosenNameFor = _state.UserId;
            _state.Save();
            Set(CloudStatus.Ready, "");
            yield return RefreshProfile(null);
            // The name travels with the line, so a rename that did not republish would leave a
            // friend's list showing the old one until something else happened to change the team.
            yield return PublishSnapshotRoutine(null);
            Report(done, "");
        }

        public IEnumerator LinkEmail(string email, string password, Action<string> done)
        {
            if (!SignedIn) { Report(done, "not signed in"); yield break; }
            _busy = true;
            Set(CloudStatus.Working, "linking your email");

            string note = "";
            bool ok = false;
            yield return _account.LinkEmail(email, password, delegate (bool success, string message)
            {
                ok = success;
                note = message;
            });
            _busy = false;

            if (!ok) { Set(CloudStatus.Failed, note); Report(done, note); yield break; }
            Set(CloudStatus.Ready, note);
            Raise();
            Report(done, "");
        }

        /// <summary>
        /// Signs in as somebody else on this device, then syncs.
        ///
        /// The sync afterwards is not a convenience. Signing in as a different account drops the
        /// base revision - see AccountService.SignInWithEmail - so the very next sync has no
        /// common ancestor between whatever is on this device and whatever that account holds,
        /// which is a conflict, which is the question. Running it immediately is what makes the
        /// player answer it now, while they remember signing in, rather than three screens later.
        /// </summary>
        public IEnumerator SignInWithEmail(string email, string password, Action<string> done)
        {
            _busy = true;
            Set(CloudStatus.Working, "signing in");

            bool ok = false;
            string error = "";
            yield return _account.SignInWithEmail(email, password, delegate (bool success, string message)
            {
                ok = success;
                error = message;
            });
            _busy = false;

            if (!ok) { Set(CloudStatus.Failed, error); Report(done, error); yield break; }

            Set(CloudStatus.Ready, "");
            _publishedFingerprint = "";
            if (_guild != null) _guild.Forget();
            yield return RefreshProfile(null);
            yield return RefreshGuild(null);
            yield return SyncRoutine(false, null);
            Report(done, "");
        }

        public IEnumerator SignOut(Action<string> done)
        {
            _busy = true;
            Set(CloudStatus.Working, "signing out");

            bool ok = false;
            string note = "";
            yield return _account.SignOut(delegate (bool success, string message) { ok = success; note = message; });
            _busy = false;

            if (!ok) { Set(CloudStatus.Failed, note); Report(done, note); yield break; }

            Profile = null;
            _publishedFingerprint = "";
            // The guild belongs to the account, not to the device. Leaving it cached would show
            // the next signed-in player somebody else's roster until the first refresh answered.
            if (_guild != null) _guild.Forget();
            Set(CloudStatus.Offline, note);
            Report(done, "");
        }

        // ================================================================== lifecycle

        /// <summary>
        /// A sync on the way out, from OnApplicationPause and OnApplicationQuit.
        ///
        /// Started, not awaited, and that is not carelessness - it is the only honest thing
        /// available. Android gives an app no guaranteed window at pause and none at all at kill,
        /// so a coroutine started here may be cut off mid-request. What makes that safe is that it
        /// is safe by construction: the local save is already written by the time this runs, an
        /// upload that does not land leaves the cloud exactly as it was, and the base revision
        /// only moves when the server has answered. A sync that dies halfway costs one sync.
        /// </summary>
        public void SyncOnLeaving()
        {
            if (!SignedIn || _busy) return;
            _client.Run(SyncRoutine(false, null));
        }

        // ================================================================== plumbing

        private void Set(CloudStatus status, string message)
        {
            Status = status;
            Message = message == null ? "" : message;
            Raise();
        }

        private void Raise()
        {
            if (Changed != null) Changed();
        }

        private static void Report(Action<string> done, string error)
        {
            if (done != null) done(error == null ? "" : error);
        }
    }
}
