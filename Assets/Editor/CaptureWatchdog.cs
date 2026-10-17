using System;

namespace Emberfall.EditorTools
{
    /// <summary>What the capture watchdog wants done on this editor tick.</summary>
    public enum WatchdogAction
    {
        /// <summary>Nothing. Healthy, switched off, or still inside a limit.</summary>
        Wait,
        /// <summary>No heartbeat in time: log the state and exit Play mode.</summary>
        Recover,
        /// <summary>The editor has sat in edit mode long enough after a Recover: enter Play again.</summary>
        ReEnter,
        /// <summary>The second play entry for this target also missed: mark the target FAILED.</summary>
        MarkFailed
    }

    /// <summary>
    /// The decision half of the capture watchdog (Docs/CAPTURE.md), kept free of every Unity call so
    /// Tools/DevHarness can run it. PlayModeScreenshot feeds it the facts it can read from the
    /// editor and does what it answers.
    ///
    /// The hang it exists for: right after a fresh editor launch and domain reload, entering Play
    /// sometimes goes quiet. The log ends after the game's Awake and Unity's "Loaded scene
    /// 'Temp/__Backupscenes/0.backup'", the editor sits idle, and no capture ever comes. A heartbeat
    /// is the first Pump tick of a play entry that finds the editor really playing. No heartbeat
    /// within the limit means this play entry is lost.
    ///
    /// One re-entry per target. The second entry gets a shorter limit than the first so that a
    /// target that misses twice is marked FAILED inside three minutes: 90 s, a few seconds for
    /// Unity to leave Play, 5 s in edit mode, then 60 s.
    /// </summary>
    public static class CaptureWatchdog
    {
        /// <summary>Set to 0 to switch the watchdog off. Read when a run begins.</summary>
        public const string SwitchVariable = "EMBERFALL_CAPTURE_WATCHDOG";

        /// <summary>Test hook: the named target's first play entry suppresses its heartbeat, so
        /// the watchdog has something to recover.</summary>
        public const string ForceMissVariable = "EMBERFALL_CAPTURE_FORCE_MISS";

        /// <summary>The batch launcher sets this to 2 on a relaunch, so a target that already
        /// missed once in the killed editor counts that as its first miss.</summary>
        public const string FirstAttemptVariable = "EMBERFALL_CAPTURE_FIRST_ATTEMPT";

        public const double FirstEntryLimitSeconds = 90.0;
        public const double RetryEntryLimitSeconds = 60.0;
        public const double EditModeWaitSeconds = 5.0;
        public const int MaxEntries = 2;

        /// <summary>The watchdog is on unless the variable is exactly "0".</summary>
        public static bool SwitchOn(string value)
        {
            return value == null || value.Trim() != "0";
        }

        /// <summary>True when the force-miss hook names this target and this is its first entry.
        /// The name may be the target as asked (with its @WxH or =stage tail) or without it.</summary>
        public static bool ForcesMiss(string hookTarget, string requested, string bare, int attempt)
        {
            if (attempt > 1) return false;
            if (hookTarget == null) return false;
            string wanted = hookTarget.Trim();
            if (wanted.Length == 0) return false;
            return string.Equals(wanted, requested, StringComparison.Ordinal)
                   || string.Equals(wanted, bare, StringComparison.Ordinal);
        }

        /// <summary>
        /// What to do now.
        /// attempt: which play entry this is for the current target, 1 or 2.
        /// recovering: a Recover was issued for this entry and the editor is on its way out of Play.
        /// secondsSinceEntry: since the play entry was requested.
        /// secondsInEditMode: while recovering, how long the editor has been out of Play (negative
        /// while it has not left yet).
        /// </summary>
        public static WatchdogAction Decide(bool enabled, bool heartbeatSeen, int attempt, bool recovering,
                                            double secondsSinceEntry, double secondsInEditMode)
        {
            if (!enabled) return WatchdogAction.Wait;
            if (heartbeatSeen) return WatchdogAction.Wait;
            if (recovering)
                return secondsInEditMode >= EditModeWaitSeconds ? WatchdogAction.ReEnter : WatchdogAction.Wait;

            bool lastEntry = attempt >= MaxEntries;
            double limit = lastEntry ? RetryEntryLimitSeconds : FirstEntryLimitSeconds;
            if (secondsSinceEntry < limit) return WatchdogAction.Wait;
            return lastEntry ? WatchdogAction.MarkFailed : WatchdogAction.Recover;
        }
    }
}
