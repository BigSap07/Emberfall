using System;
using System.Collections.Generic;
using System.IO;
using Emberfall.Core.Data;
using Emberfall.EditorTools;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE CAPTURE WATCHDOG (Docs/CAPTURE.md), as checks.
    ///
    /// The editor side cannot run here, so what is executed is the decision class the editor side
    /// asks (CaptureWatchdog, which has no Unity calls) and what is read is the source that does what
    /// it answers: PlayModeScreenshot, ScratchCaptures, the batch launcher and the doc.
    /// </summary>
    internal static partial class Program
    {
        private static void CheckCaptureWatchdog(ContentDatabase db, string contentDir)
        {
            Line("");
            Line("CAPTURE WATCHDOG (Docs/CAPTURE.md: a lost play entry is recovered once, then the target is marked FAILED)");

            string root = Path.GetFullPath(Path.Combine(contentDir, "..", "..", ".."));

            // ---- the decision, executed
            const double limit = CaptureWatchdog.FirstEntryLimitSeconds;
            Check(limit == 90.0, "a first play entry gets 90 s to send a heartbeat");
            Check(CaptureWatchdog.Decide(true, false, 1, false, 0.0, -1.0) == WatchdogAction.Wait,
                  "no heartbeat at 0 s: Wait");
            Check(CaptureWatchdog.Decide(true, false, 1, false, 89.0, -1.0) == WatchdogAction.Wait,
                  "no heartbeat at 89 s: Wait");
            Check(CaptureWatchdog.Decide(true, false, 1, false, 89.9, -1.0) == WatchdogAction.Wait,
                  "no heartbeat at 89.9 s: Wait");
            Check(CaptureWatchdog.Decide(true, false, 1, false, 90.0, -1.0) == WatchdogAction.Recover,
                  "no heartbeat at 90 s: Recover");
            Check(CaptureWatchdog.Decide(true, false, 1, false, 400.0, -1.0) == WatchdogAction.Recover,
                  "no heartbeat long after 90 s: still Recover (until it has been done)");

            Check(CaptureWatchdog.Decide(true, false, 1, true, 95.0, -1.0) == WatchdogAction.Wait,
                  "recovering and the editor has not left Play yet: Wait");
            Check(CaptureWatchdog.Decide(true, false, 1, true, 100.0, 4.9) == WatchdogAction.Wait,
                  "recovering, 4.9 s in edit mode: Wait");
            Check(CaptureWatchdog.Decide(true, false, 1, true, 100.0, 5.0) == WatchdogAction.ReEnter,
                  "recovering, 5 s in edit mode: ReEnter");

            Check(CaptureWatchdog.Decide(true, false, 2, false, 59.0, -1.0) == WatchdogAction.Wait,
                  "second play entry, no heartbeat at 59 s: Wait");
            Check(CaptureWatchdog.Decide(true, false, 2, false, 60.0, -1.0) == WatchdogAction.MarkFailed,
                  "second play entry, no heartbeat at 60 s: MarkFailed (the second miss on the same target)");
            Check(CaptureWatchdog.Decide(true, false, 2, false, 5.0, -1.0) == WatchdogAction.Wait,
                  "second play entry, no heartbeat at 5 s: Wait");

            bool healthy = true;
            double[] times = { 0.0, 10.0, 89.0, 90.0, 91.0, 500.0 };
            for (int attempt = 1; attempt <= 2; attempt++)
                foreach (double t in times)
                {
                    if (CaptureWatchdog.Decide(true, true, attempt, false, t, -1.0) != WatchdogAction.Wait) healthy = false;
                    if (CaptureWatchdog.Decide(true, true, attempt, true, t, 30.0) != WatchdogAction.Wait) healthy = false;
                }
            Check(healthy, "a heartbeat seen at any time, on either entry: always Wait");

            bool inert = true;
            foreach (double t in times)
                for (int attempt = 1; attempt <= 2; attempt++)
                {
                    if (CaptureWatchdog.Decide(false, false, attempt, false, t, -1.0) != WatchdogAction.Wait) inert = false;
                    if (CaptureWatchdog.Decide(false, false, attempt, true, t, 30.0) != WatchdogAction.Wait) inert = false;
                }
            Check(inert, "the watchdog switched off: never acts, whatever the time and the attempt");

            // The whole timeline, one tick a second, the way WatchdogTick drives it: both entries
            // miss. Unity needs a few seconds to leave Play; take 10 as a generous figure.
            {
                double recoverAt = -1, reEnterAt = -1, failedAt = -1;
                double entryAt = 0.0, editSince = -1.0;
                int attempt = 1; bool recovering = false; double leavesPlayAt = -1;
                for (double now = 0.0; now < 600.0 && failedAt < 0; now += 1.0)
                {
                    bool inEdit = leavesPlayAt >= 0 && now >= leavesPlayAt;
                    double inEditMode = -1.0;
                    if (recovering && inEdit) { if (editSince < 0) editSince = now; inEditMode = now - editSince; }
                    switch (CaptureWatchdog.Decide(true, false, attempt, recovering, now - entryAt, inEditMode))
                    {
                        case WatchdogAction.Recover:
                            recoverAt = now; recovering = true; leavesPlayAt = now + 10.0; editSince = -1.0;
                            break;
                        case WatchdogAction.ReEnter:
                            reEnterAt = now; recovering = false; attempt = 2; entryAt = now; leavesPlayAt = -1;
                            break;
                        case WatchdogAction.MarkFailed:
                            failedAt = now;
                            break;
                    }
                }
                Check(recoverAt == 90.0, "timeline: the first miss recovers at 90 s (" + recoverAt + ")");
                Check(reEnterAt >= 0 && reEnterAt - recoverAt >= 15.0 && reEnterAt - recoverAt <= 16.0,
                      "timeline: the re-entry follows Unity leaving Play plus 5 s in edit mode (" + (reEnterAt - recoverAt) + " s after the recover)");
                Check(failedAt > 0 && failedAt < 180.0,
                      "timeline: a target that misses twice is marked FAILED inside 3 minutes (" + failedAt + " s)");
            }

            // ---- the switch and the hook
            Check(CaptureWatchdog.SwitchOn(null) && CaptureWatchdog.SwitchOn("") && CaptureWatchdog.SwitchOn("1")
                  && CaptureWatchdog.SwitchOn("on"), "EMBERFALL_CAPTURE_WATCHDOG unset, empty, 1 or on: the watchdog is ON");
            Check(!CaptureWatchdog.SwitchOn("0") && !CaptureWatchdog.SwitchOn(" 0 "), "EMBERFALL_CAPTURE_WATCHDOG=0: the watchdog is OFF");
            Check(CaptureWatchdog.SwitchVariable == "EMBERFALL_CAPTURE_WATCHDOG"
                  && CaptureWatchdog.ForceMissVariable == "EMBERFALL_CAPTURE_FORCE_MISS"
                  && CaptureWatchdog.FirstAttemptVariable == "EMBERFALL_CAPTURE_FIRST_ATTEMPT",
                  "the three environment variable names are the ones the doc and the launcher use");
            Check(CaptureWatchdog.ForcesMiss("Screen:Summon", "Screen:Summon", "Screen:Summon", 1)
                  && CaptureWatchdog.ForcesMiss("Screen:Summon", "Screen:Summon@2400x1080", "Screen:Summon", 1)
                  && CaptureWatchdog.ForcesMiss("Screen:Summon@2400x1080", "Screen:Summon@2400x1080", "Screen:Summon", 1),
                  "the force-miss hook hits the named target, as asked or without its size tail, on the first entry");
            Check(!CaptureWatchdog.ForcesMiss("Screen:Summon", "Screen:Summon", "Screen:Summon", 2),
                  "the force-miss hook never fires on the second entry");
            Check(!CaptureWatchdog.ForcesMiss("Screen:Summon", "Screen:Hub", "Screen:Hub", 1)
                  && !CaptureWatchdog.ForcesMiss(null, "Screen:Hub", "Screen:Hub", 1)
                  && !CaptureWatchdog.ForcesMiss("  ", "Screen:Hub", "Screen:Hub", 1),
                  "the force-miss hook leaves other targets, and an unset or blank hook, alone");

            // ---- the wiring, read from source
            string pms = ReadIfThere(Path.Combine(root, "Assets", "Editor", "PlayModeScreenshot.cs"));
            string scratch = ReadIfThere(Path.Combine(root, "Assets", "Editor", "ScratchCaptures.cs"));
            string dog = ReadIfThere(Path.Combine(root, "Assets", "Editor", "CaptureWatchdog.cs"));
            Check(pms.Contains("CaptureWatchdog.ForceMissVariable") && pms.Contains("CaptureWatchdog.ForcesMiss(")
                  && pms.Contains("FORCE-MISS hook"),
                  "WIRING (reads source): PlayModeScreenshot reads the force-miss hook and logs when it is active");
            Check(pms.Contains("CaptureWatchdog.SwitchVariable") && pms.Contains("CaptureWatchdog.SwitchOn(")
                  && pms.Contains("watchdog mode ON") && pms.Contains("watchdog mode OFF"),
                  "WIRING (reads source): PlayModeScreenshot reads the off switch at Begin and logs which mode is active");
            Check(dog.Contains("\"EMBERFALL_CAPTURE_WATCHDOG\"") && dog.Contains("\"EMBERFALL_CAPTURE_FORCE_MISS\""),
                  "WIRING (reads source): the variable names are spelled as the doc says");
            Check(pms.Contains("EditorApplication.update += WatchdogTick") && pms.Contains("CaptureWatchdog.Decide(")
                  && pms.Contains("EditorApplication.ExitPlaymode()"),
                  "WIRING (reads source): the watchdog is checked from EditorApplication.update and recovers with ExitPlaymode");
            Check(pms.Contains("PlayModeScreenshot: FAILED ") && pms.Contains("a missed heartbeat on two play entries"),
                  "WIRING (reads source): the second miss logs 'FAILED <target> after a missed heartbeat on two play entries'");
            Check(pms.Contains("play entry requested") && pms.Contains("play heartbeat, target ") && pms.Contains("watchdog RECOVER"),
                  "WIRING (reads source): the three lines the batch launcher reads (entry, heartbeat, recover) are printed");
            Check(pms.Contains("SessionState.SetString(WdEntryAtKey") && pms.Contains("PlayModeStateChange.EnteredPlayMode"),
                  "WIRING (reads source): the entry time is kept in SessionState and the entered-play event is logged");
            // ---- the skip-reload A/B switch (task 5)
            Check(CaptureWatchdog.SkipReloadOn("1") && CaptureWatchdog.SkipReloadOn(" 1 "),
                  "EMBERFALL_CAPTURE_SKIP_RELOAD=1: skipping the domain reload is ON for the session");
            Check(!CaptureWatchdog.SkipReloadOn(null) && !CaptureWatchdog.SkipReloadOn("") && !CaptureWatchdog.SkipReloadOn("0")
                  && !CaptureWatchdog.SkipReloadOn("true"), "EMBERFALL_CAPTURE_SKIP_RELOAD unset, empty, 0 or anything but 1: nothing changes");
            Check(CaptureWatchdog.SkipReloadVariable == "EMBERFALL_CAPTURE_SKIP_RELOAD", "the skip-reload variable is spelled as the doc says");
            Check(scratch.Contains("CaptureWatchdog.SkipReloadOn(") && scratch.Contains("EnterPlayModeOptions.DisableDomainReload")
                  && scratch.Contains("SessionState.SetBool(OrigPlayOptionsEnabledKey, EditorSettings.enterPlayModeOptionsEnabled)"),
                  "WIRING (reads source): ScratchCaptures remembers the Enter Play Mode options before it turns the domain reload off");
            Check(scratch.Contains("EditorSettings.enterPlayModeOptionsEnabled = enabled;")
                  && scratch.Contains("EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)options;")
                  && scratch.Contains("Enter Play Mode options restored") && scratch.Contains("RestorePlayOptions();"),
                  "WIRING (reads source): ScratchCaptures puts the exact previous Enter Play Mode options back and says so");
            string batchSrc = ReadIfThere(Path.Combine(root, "Tools", "capture-batch.ps1"));
            Check(batchSrc.Contains("EditorSettings.asset") && batchSrc.Contains("Restore-EditorSettings") && !batchSrc.Contains("Remove-Item"),
                  "WIRING (reads source): capture-batch.ps1 copies EditorSettings.asset aside and restores it if it changed");

            // Pump must stand down while a Recover is under way: the guard sits after the isPlaying
            // check and before the force-miss hook and the heartbeat.
            int playingCheck = pms.IndexOf("if (!EditorApplication.isPlaying) return;", StringComparison.Ordinal);
            int recoverGuard = pms.IndexOf("if (SessionState.GetBool(WdRecoveringKey, false)) return;", StringComparison.Ordinal);
            int heartbeatMark = pms.IndexOf("SessionState.SetBool(WdBeatKey, true);", StringComparison.Ordinal);
            Check(playingCheck >= 0 && recoverGuard > playingCheck && heartbeatMark > recoverGuard,
                  "WIRING (reads source): Pump records no heartbeat and does no work while a Recover is under way");
            Check(scratch.Contains("PlayModeScreenshot.FailedKey") && scratch.Contains("CaptureWatchdog.FirstAttemptVariable"),
                  "WIRING (reads source): ScratchCaptures carries on after a FAILED target and honours a relaunch's first attempt");

            // Unity's own backup-scene folder is only ever removed by Unity: the editor tools name it nowhere in code.
            List<string> touching = new List<string>();
            foreach (string file in Directory.GetFiles(Path.Combine(root, "Assets", "Editor"), "*.cs", SearchOption.AllDirectories))
                foreach (string raw in File.ReadAllLines(file))
                    if (raw.Contains("__Backupscenes") && !raw.TrimStart().StartsWith("//") && !raw.TrimStart().StartsWith("///"))
                        touching.Add(Path.GetFileName(file));
            Check(touching.Count == 0, "no editor tool names Temp/__Backupscenes in code (Unity removes it itself when Play ends)"
                  + (touching.Count > 0 ? ": " + string.Join(", ", touching.ToArray()) : ""));

            // ---- the tester rule, in all three places it must be
            const string rule = "A run captures its targets in batches in one editor session, never one launch per target. A single launch is only for retrying a target.";
            string doc = ReadIfThere(Path.Combine(root, "Docs", "CAPTURE.md"));
            string batch = ReadIfThere(Path.Combine(root, "Tools", "capture-batch.ps1"));
            Check(doc.Contains(rule), "Docs/CAPTURE.md carries the tester rule word for word");
            Check(Collapse(batch).Contains(rule), "Tools/capture-batch.ps1's usage text carries the tester rule");
            Check(Collapse(scratch).Contains("a run captures its targets in batches in one editor session, never one launch per target. A single launch is only for retrying a target."),
                  "ScratchCaptures' usage text carries the tester rule");
            Check(batch.Contains("RESULT ") && batch.Contains("SUMMARY ") && batch.Contains("EMBERFALL_CAPTURE_FIRST_ATTEMPT")
                  && batch.Contains("Move-Item") && !batch.Contains("Remove-Item"),
                  "Tools/capture-batch.ps1 prints RESULT and SUMMARY lines, moves (never deletes) a stale backup-scene folder, and has no Remove-Item");
        }

        private static string ReadIfThere(string path)
        {
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }

        /// <summary>Joins a wrapped comment block back into running text: comment markers and line
        /// breaks become single spaces, so a sentence wrapped over two lines still matches.</summary>
        private static string Collapse(string text)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim().TrimStart('#', '/', '<', '>', '!', ' ');
                if (line.Length == 0) continue;
                sb.Append(line).Append(' ');
            }
            return sb.ToString().Replace("  ", " ");
        }
    }
}
