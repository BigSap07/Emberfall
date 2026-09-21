using System;

namespace Emberfall.Core.Cloud
{
    /// <summary>What a sync should do, decided before anything is uploaded or downloaded.</summary>
    public enum SyncAction
    {
        /// <summary>Neither side has a save. A brand-new account on a brand-new device.</summary>
        NothingToDo,

        /// <summary>Send the local save. Either the cloud has none, or it holds exactly the
        /// revision this device last saw, so local is a descendant of it and cannot lose work.</summary>
        UploadLocal,

        /// <summary>Take the cloud save. This device has nothing of its own to lose.</summary>
        DownloadCloud,

        /// <summary>Both sides hold work the other does not. The player decides; nothing moves
        /// until they do.</summary>
        AskPlayer,

        /// <summary>The cloud save was written by a newer build than this one can read. Neither
        /// direction is safe, so neither happens.</summary>
        RefuseCloudNewerVersion
    }

    /// <summary>The decision, plus the sentence that explains it to whoever is reading a log.</summary>
    public struct SyncPlan
    {
        public SyncAction Action;
        public string Reason;

        public SyncPlan(SyncAction action, string reason)
        {
            Action = action;
            Reason = reason;
        }
    }

    /// <summary>Which save a player chose when they were asked. There is no third answer, and in
    /// particular there is no merge: two saves of this game have no meaningful merge, and one that
    /// pretended to would lose work more quietly than picking a side does.</summary>
    public enum ConflictChoice
    {
        KeepLocal,
        KeepCloud
    }

    /// <summary>
    /// Everything the decision is made from. A struct of plain numbers on purpose - the whole
    /// point of this type is that the rule below can be exercised for every case without a
    /// network, a database or a Unity runtime, and it is exercised for every case in the harness.
    /// </summary>
    public struct SyncInputs
    {
        /// <summary>Whether this device has a local save at all.</summary>
        public bool HasLocal;

        /// <summary>The highest save version THIS BUILD can read - SaveSerializer.CurrentVersion.
        /// Passed in rather than read here so the rule stays engine-free and so a test can ask
        /// what an older build would have decided.</summary>
        public int ReaderVersion;

        public bool HasCloud;
        public int CloudSaveVersion;
        public int CloudRevision;

        /// <summary>
        /// The cloud revision this device last successfully synced with, or 0 for never.
        ///
        /// This is the load-bearing number, and it is why a plain "which timestamp is newer"
        /// comparison is not enough. It is the common ancestor: if the cloud is still at the
        /// revision this device last saw, then whatever is local was built ON that cloud save and
        /// uploading it loses nothing. The moment the cloud has moved past it, another device has
        /// written work this one has never seen, and no clock comparison can tell you whether that
        /// work matters - so nothing is guessed and the player is asked.
        ///
        /// Kept in device-local sync state, NOT in the save. It describes this device's
        /// relationship with the server, not the player's progress, and putting it in the save
        /// would mean a save version bump plus the odd result that downloading a save also
        /// overwrites the bookkeeping that decided to download it.
        /// </summary>
        public int BaseRevision;
    }

    /// <summary>
    /// The one place that decides whether a sync uploads, downloads, refuses or asks.
    ///
    /// Two rules govern the whole thing:
    ///
    /// **Nothing is ever silently overwritten.** Every case where both sides hold work the other
    /// does not ends in AskPlayer. There is no timestamp tie-break, no "the bigger save wins", no
    /// newest-writer-wins - all three are ways of picking a loser without telling anybody.
    ///
    /// **A save from a newer build is refused, not read.** SaveSerializer.Deserialize already
    /// refuses one locally, for the reason its version history sets out at length: reading a
    /// newer save with older code does not fail, it silently drops whatever the older code has no
    /// key for. The same refusal has to happen here, and one step earlier - before the document is
    /// parsed at all - which is why save_version is its own column in the database rather than a
    /// field inside the blob.
    /// </summary>
    public static class SyncPlanner
    {
        public static SyncPlan Decide(SyncInputs input)
        {
            if (!input.HasCloud)
            {
                if (!input.HasLocal)
                    return new SyncPlan(SyncAction.NothingToDo, "neither side has a save");
                return new SyncPlan(SyncAction.UploadLocal, "no cloud save yet - this is the first upload");
            }

            // Checked BEFORE anything else, including before the has-local branches. A newer save
            // in the cloud is unsafe to download (this build would drop what it cannot read) AND
            // unsafe to overwrite (the player's newer progress is the thing that would be lost),
            // so the answer is the same whatever this device holds: stop, and say why.
            if (input.CloudSaveVersion > input.ReaderVersion)
            {
                return new SyncPlan(SyncAction.RefuseCloudNewerVersion,
                    "the cloud save is version " + input.CloudSaveVersion + " and this build reads up to "
                    + input.ReaderVersion + " - update the game to sync");
            }

            if (!input.HasLocal)
                return new SyncPlan(SyncAction.DownloadCloud, "this device has no save of its own");

            if (input.CloudRevision == input.BaseRevision)
            {
                return new SyncPlan(SyncAction.UploadLocal,
                    "the cloud is still at revision " + input.CloudRevision
                    + ", which is what this device last synced - local descends from it");
            }

            if (input.CloudRevision > input.BaseRevision)
            {
                return new SyncPlan(SyncAction.AskPlayer,
                    "the cloud has moved to revision " + input.CloudRevision + " since this device last saw "
                    + input.BaseRevision + " - another device has played");
            }

            // The cloud is BEHIND what this device last synced with. Nothing the client does
            // produces this: push_save only ever increments. It means the row was restored from a
            // backup, deleted and rebuilt, or edited by hand. Local looks newer, and taking local
            // would usually be right - which is exactly why it is not done automatically. A
            // database that went backwards is the last situation in which to trust an inference.
            return new SyncPlan(SyncAction.AskPlayer,
                "the cloud is at revision " + input.CloudRevision + ", BEHIND the " + input.BaseRevision
                + " this device last synced - the cloud save was restored or rebuilt");
        }

        /// <summary>
        /// The revision to send with an upload, once the situation is known.
        ///
        /// push_save refuses anything but the revision actually stored, so this is not a
        /// preference - it is a statement of what the client believes, which the database then
        /// agrees with or rejects. There are only two values it can be: the cloud's current
        /// revision when there is a row, and 0 when there is not.
        ///
        /// Note what this means for a player who chose KeepLocal out of a conflict: the upload
        /// carries the cloud's revision, so it DOES overwrite the other device's work. That is the
        /// player's decision, made with both saves described in front of them, and it is the only
        /// path in this file that can overwrite anything.
        /// </summary>
        public static int ExpectedRevisionForUpload(SyncInputs input)
        {
            return input.HasCloud ? input.CloudRevision : 0;
        }
    }
}
