using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Emberfall.Core.Session;
using Emberfall.Core.Telemetry;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// R27 FRIENDS BUILD (Docs/FRIENDS-BUILD.md): the play log's pure rules, the defines parse, the build scripts and
    /// the wiring. The log is behind EMBERFALL_PLAYLOG: off by default, off means no file. The harness compiles with no
    /// defines, so what it proves is the shipped state plus the pure functions the define-on build runs.
    /// </summary>
    internal static partial class Program
    {
        private static void CheckR27PlayLog(string contentDir)
        {
            Line("");
            Line("R27 PLAY LOG AND FRIENDS BUILD (EMBERFALL_PLAYLOG)");
            R27Switch();
            R27OffMeansNoFile();
            R27Formats();
            R27Session();
            R27SummaryOnce();
            R27Caps();
            R27Rotation();
            R27Share();
            R27Defines();
            R27Smoke();
            R27SmokeIdentity(contentDir);
            R27Wiring(contentDir);
        }

        private static string R27TempDir(string name)
        {
            string dir = Path.Combine(Path.GetTempPath(), "emberfall-r27-" + name + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void R27Switch()
        {
            Check(!PlayLogSwitch.Enabled, "PlayLogSwitch.Enabled is FALSE in the shipped compile: the define is the only way to turn it on");
            Check(!PlayLogSwitch.Resolve(false) && PlayLogSwitch.Resolve(true), "PlayLogSwitch.Resolve is on exactly when the define is set");
            Check(PlayLogHooks.Active == null, "no session is installed by default, so every tap is one comparison");
            // The taps do nothing without a session and never throw.
            PlayLogHooks.Stage("stage_attempt", "1-1", 1, 1);
            PlayLogHooks.Cloud("Ready", "");
            PlayLogHooks.Purchase("x", false, "Off");
            Check(PlayLogHooks.Active == null, "a tap with no session does nothing");
        }

        private static void R27OffMeansNoFile()
        {
            string dir = R27TempDir("off");
            PlayLogWriter off = new PlayLogWriter(false, dir);
            bool opened = off.Open();
            bool wrote = off.Write("+1.0s screen Heroes") || off.WriteAlways("+1.0s summary");
            Check(!off.Enabled && !opened && !wrote && Directory.GetFiles(dir).Length == 0, "OFF MEANS NO FILE: a disabled writer creates nothing and writes nothing");
            PlayLogSession session = new PlayLogSession(off, delegate () { return 5.0; }, "abcd1234");
            session.Header("2026-10-23T12:00:00Z", "0.3.0", "abc1234", "WindowsPlayer", "pc", "");
            session.Screen("Heroes");
            session.Error("boom");
            session.Summary();
            Check(Directory.GetFiles(dir).Length == 0, "and a session on a disabled writer leaves the folder empty too");
            PlayLogWriter noDir = new PlayLogWriter(true, "");
            Check(!noDir.Enabled, "a writer with no folder is off as well");
        }

        private static void R27Formats()
        {
            Check(PlayLogSession.StampOf(12.34) == "+12.3s" && PlayLogSession.StampOf(0) == "+0.0s", "the time stamp is seconds since the start with one decimal");
            Check(PlayLogSession.ScreenLine(3.0, "Heroes") == "+3.0s screen Heroes", "screen line: " + PlayLogSession.ScreenLine(3.0, "Heroes"));
            Check(PlayLogSession.StageLine(8.0, "1-3", "attempt", 0, 0) == "+8.0s stage 1-3 attempt"
                  && PlayLogSession.StageLine(9.0, "1-3", "win", 0, 0) == "+9.0s stage 1-3 win"
                  && PlayLogSession.StageLine(10.0, "1-3", "lose", 2, 1) == "+10.0s stage 1-3 lose wave 2 battle 1", "stage lines: attempt, win, lose with the wave and battle");
            Check(PlayLogSession.PurchaseLine(1.0, "pack_a", true, "") == "+1.0s purchase offer pack_a result bought"
                  && PlayLogSession.PurchaseLine(2.0, "pack_a", false, "ComingSoon") == "+2.0s purchase offer pack_a result refused reason ComingSoon", "purchase lines: bought, and refused with its reason");
            Check(PlayLogSession.CloudLine(1.0, "Working", "signing in") == "+1.0s cloud Working (signing in)" && PlayLogSession.CloudLine(2.0, "Ready", "") == "+2.0s cloud Ready",
                  "cloud lines: the status, and the message in brackets only when there is one");
            Check(PlayLogSession.SummaryLine(300.4, 7, 2, 1, 0) == "+300.4s summary length 300s screens 7 stages_cleared 2 purchases 1 errors 0", "summary line: " + PlayLogSession.SummaryLine(300.4, 7, 2, 1, 0));
            string header = PlayLogSession.HeaderLines("ab12cd34", "2026-10-23T12:00:00Z", "0.3.0", "abc1234", "Android", "SM-X200", "EMBERFALL_TESTSTORE;EMBERFALL_PLAYLOG");
            Check(header.Contains("== session ab12cd34 started 2026-10-23T12:00:00Z ==") && header.Contains("build 0.3.0 (abc1234) | platform Android | device SM-X200 | defines EMBERFALL_TESTSTORE;EMBERFALL_PLAYLOG"),
                  "header: session id, start time UTC, build, commit, platform, device, define set");
            Check(PlayLogRules.Clean("a\r\nb\t c   d", 50) == "a b c d" && PlayLogRules.Clean(null, 5) == "" && PlayLogRules.Clean(new string('x', 300), 40).Length == 40 && PlayLogRules.Clean(new string('x', 300), 40).EndsWith("..."),
                  "Clean: control characters become spaces, runs collapse, a long text is cut with three dots, null is empty");
            Check(PlayLogRules.Banner.Contains("stays on this device") && PlayLogRules.Banner.Contains("never sends it") && PlayLogRules.Banner.Contains("whatever you chose"),
                  "the file banner says it is local, independent of the play stats choice, and sent by hand");
            Check(PlayLogSession.StageAttemptEvent == TelemetrySchema.StageAttempt && PlayLogSession.StageClearEvent == TelemetrySchema.StageClear && PlayLogSession.WaveLostEvent == TelemetrySchema.WaveLost,
                  "the three campaign event names the stage tap maps are the stats schema's own");
        }

        private static void R27Session()
        {
            string dir = R27TempDir("on");
            PlayLogWriter writer = new PlayLogWriter(true, dir);
            double now = 0.0;
            PlayLogSession s = new PlayLogSession(writer, delegate () { return now; }, "deadbeef");
            writer.Open();
            s.Header("2026-10-23T12:00:00Z", "0.3.0", "abc1234", "WindowsPlayer", "pc", "EMBERFALL_PLAYLOG");
            now = 2.0; s.Screen("MainMenu");
            now = 5.0; s.Screen("Campaign");
            now = 6.0; s.Stage("stage_attempt", "1-1", 0, 0);
            now = 20.0; s.Stage("wave_lost", "1-1", 1, 0);
            now = 40.0; s.Stage("stage_clear", "1-1", 2, 0);
            now = 41.0; s.Stage("something_else", "1-1", 0, 0);
            now = 50.0; s.Purchase("pack_a", true, "");
            now = 51.0; s.Purchase("pack_b", false, "ComingSoon");
            now = 52.0; s.Cloud("Offline", "no signal");
            now = 53.0; s.Error("Exception: boom\nat X.Y()");
            now = 60.0; s.Summary();
            string text = File.ReadAllText(writer.FilePath);
            Check(text.StartsWith("# Emberfall play log."), "the file starts with the banner");
            Check(s.ScreensOpened == 2 && s.StagesCleared == 1 && s.Purchases == 1 && s.PurchasesRefused == 1 && s.Errors == 1,
                  "counts: 2 screens, 1 stage cleared, 1 purchase bought, 1 refused, 1 error");
            Check(text.Contains("+2.0s screen MainMenu") && text.Contains("+6.0s stage 1-1 attempt") && text.Contains("+20.0s stage 1-1 lose wave 1 battle 0") && text.Contains("+40.0s stage 1-1 win")
                  && text.Contains("+50.0s purchase offer pack_a result bought") && text.Contains("+51.0s purchase offer pack_b result refused reason ComingSoon")
                  && text.Contains("+52.0s cloud Offline (no signal)") && text.Contains("+53.0s error Exception: boom at X.Y()"), "every event kind lands as one readable line");
            Check(!text.Contains("something_else"), "an unknown campaign event is ignored");
            Check(text.TrimEnd().EndsWith("+60.0s summary length 60s screens 2 stages_cleared 1 purchases 1 errors 1"), "the file ends with the session summary line");

            // The hooks reach the active session (the app came back, so the session records again).
            s.Resume();
            PlayLogHooks.Active = s;
            try
            {
                now = 61.0;
                PlayLogHooks.Stage("stage_clear", "1-2", 0, 0);
                PlayLogHooks.Cloud("Ready", "");
                PlayLogHooks.Purchase("pack_c", false, "Off");
            }
            finally { PlayLogHooks.Active = null; }
            string after = File.ReadAllText(writer.FilePath);
            Check(after.Contains("+61.0s stage 1-2 win") && after.Contains("+61.0s cloud Ready") && after.Contains("+61.0s purchase offer pack_c result refused reason Off") && s.StagesCleared == 2,
                  "PlayLogHooks.Stage, Cloud and Purchase write through the active session");
        }

        private static void R27Caps()
        {
            string dir = R27TempDir("caps");
            PlayLogWriter writer = new PlayLogWriter(true, dir);
            PlayLogSession s = new PlayLogSession(writer, delegate () { return 1.0; }, "cap00000");
            for (int i = 0; i < PlayLogRules.MaxErrorsPerSession + 10; i++) s.Error("e" + i);
            string text = File.ReadAllText(writer.FilePath);
            int errorLines = 0;
            foreach (string line in text.Split('\n')) if (line.Contains(" error ")) errorLines++;
            Check(s.Errors == PlayLogRules.MaxErrorsPerSession && errorLines == PlayLogRules.MaxErrorsPerSession + 1 && text.Contains("more errors this session are not logged"),
                  "ERRORS ARE CAPPED: " + PlayLogRules.MaxErrorsPerSession + " lines then one 'not logged' line (" + errorLines + " error lines in the file)");

            string dir2 = R27TempDir("budget");
            PlayLogWriter w2 = new PlayLogWriter(true, dir2);
            PlayLogSession s2 = new PlayLogSession(w2, delegate () { return 1.0; }, "bud00000");
            for (int i = 0; i < PlayLogRules.MaxLinesPerSession + 50; i++) s2.Screen("S" + i);
            s2.Summary();
            string t2 = File.ReadAllText(w2.FilePath);
            Check(w2.Lines == PlayLogRules.MaxLinesPerSession + 1 && t2.TrimEnd().EndsWith("errors 0") && !t2.Contains("screen S" + (PlayLogRules.MaxLinesPerSession + 10)),
                  "THE LINE BUDGET holds (" + PlayLogRules.MaxLinesPerSession + " event lines a session) and the summary is still written after it");
            Check(new FileInfo(w2.FilePath).Length < PlayLogRules.MaxFileBytes, "a session at the line budget stays under the file cap");
        }

        private static void R27Rotation()
        {
            Check(!PlayLogRules.NeedsRotation(PlayLogRules.MaxFileBytes) && PlayLogRules.NeedsRotation(PlayLogRules.MaxFileBytes + 1) && PlayLogRules.MaxFileBytes == 200 * 1024,
                  "ROTATION RULE: a file over 200 KB rotates, at 200 KB it does not");
            string dir = R27TempDir("rotate");
            string path = Path.Combine(dir, PlayLogRules.FileName);
            string prev = Path.Combine(dir, PlayLogRules.PrevFileName);
            File.WriteAllText(path, new string('a', (int)PlayLogRules.MaxFileBytes + 10));
            PlayLogWriter w = new PlayLogWriter(true, dir);
            bool rotated = w.Open();
            Check(rotated && File.Exists(prev) && new FileInfo(prev).Length == PlayLogRules.MaxFileBytes + 10 && File.ReadAllText(path).StartsWith("# Emberfall play log."),
                  "an oversize file is MOVED to emberfall_playlog.prev.txt (content kept) and a fresh file starts with the banner");
            File.WriteAllText(path, new string('b', (int)PlayLogRules.MaxFileBytes + 20));
            PlayLogWriter w2 = new PlayLogWriter(true, dir);
            bool again = w2.Open();
            Check(again && File.ReadAllText(prev).StartsWith("bbbb") && File.Exists(path), "a second rotation replaces the older prev file with the newer one and nothing is lost but the oldest");
            string small = R27TempDir("small");
            File.WriteAllText(Path.Combine(small, PlayLogRules.FileName), "keep me\n");
            PlayLogWriter w3 = new PlayLogWriter(true, small);
            w3.Open();
            w3.Write("+1.0s screen Heroes");
            string kept = File.ReadAllText(Path.Combine(small, PlayLogRules.FileName));
            Check(kept.StartsWith("keep me") && kept.Contains("screen Heroes") && !File.Exists(Path.Combine(small, PlayLogRules.PrevFileName)),
                  "a file under the cap is appended to, never rewritten, and not rotated");
        }

        private static void R27Share()
        {
            StringBuilder b = new StringBuilder(PlayLogRules.Banner + "\n");
            for (int i = 0; i < 4000; i++) b.Append("+" + i + ".0s screen Heroes\n");
            b.Append("+4000.0s summary length 4000s screens 4000 stages_cleared 3 purchases 1 errors 0\n");
            b.Append("+4001.0s screen Last\n");
            string share = PlayLogRules.ShareText(b.ToString(), PlayLogRules.MaxShareChars);
            Check(share.Length <= PlayLogRules.MaxShareChars, "SHARE TEXT IS CAPPED: " + share.Length + " of " + PlayLogRules.MaxShareChars + " characters from a " + b.Length + " character file");
            Check(share.StartsWith("Emberfall play log. Last session summary: +4000.0s summary length 4000s") && share.TrimEnd().EndsWith("+4001.0s screen Last"),
                  "it opens with the last session summary and ends with the last line of the file");
            string[] lines = share.Split('\n');
            bool whole = true;
            for (int i = 3; i < lines.Length; i++) if (lines[i].Length > 0 && !lines[i].StartsWith("+") && !lines[i].StartsWith("...")) whole = false;
            Check(whole, "the tail starts on a line boundary: no half line");
            string small = PlayLogRules.ShareText("# banner\n+1.0s screen A\n", 5000);
            Check(small.Contains("(no session summary yet)") && small.Contains("+1.0s screen A") && !small.Contains("..."), "a short file is shared whole, and says so when there is no summary yet");
            Check(PlayLogRules.ShareText("", 5000) == "Emberfall play log: empty." && PlayLogRules.ShareText(null, 5000) == "Emberfall play log: empty.", "an empty file shares a one line note");
            Check(PlayLogRules.ShareText(b.ToString(), 10).Length <= 200 + 100 && PlayLogRules.MaxShareChars <= 20000, "a tiny cap is lifted to a usable floor, and the real cap is well under an intent extra's limit");
        }

        private static void R27Defines()
        {
            string[] d = BuildDefines.Parse("EMBERFALL_TESTSTORE;EMBERFALL_PLAYLOG");
            Check(d.Length == 2 && d[0] == "EMBERFALL_TESTSTORE" && d[1] == "EMBERFALL_PLAYLOG", "DEFINES PARSE: 'A;B' gives A then B");
            string[] messy = BuildDefines.Parse(" A , B;;A; 9bad ;C-D;E F;_ok ");
            Check(BuildDefines.Join(messy) == "A;B;_ok", "it trims, drops empties, duplicates and anything that is not a define name (got '" + BuildDefines.Join(messy) + "')");
            Check(BuildDefines.Parse(null).Length == 0 && BuildDefines.Parse("").Length == 0 && BuildDefines.Parse(";;").Length == 0, "no flag, an empty flag and only separators all give no defines");
            Check(BuildDefines.IsName("EMBERFALL_PLAYLOG") && !BuildDefines.IsName("a b") && !BuildDefines.IsName("1A") && !BuildDefines.IsName(""), "IsName accepts letters, digits and underscores, not starting with a digit");
        }

        private static void R27Smoke()
        {
            string[] with = new string[] { "Emberfall.exe", "-batchmode", "-emberfallSmoke" };
            string[] upper = new string[] { "-EMBERFALLSMOKE" };
            Check(PlayLogRules.HasFlag(with, "-emberfallSmoke") && PlayLogRules.HasFlag(upper, "-emberfallSmoke") && !PlayLogRules.HasFlag(new string[] { "-emberfallSmokeX" }, "-emberfallSmoke")
                  && !PlayLogRules.HasFlag(null, "-emberfallSmoke") && !PlayLogRules.HasFlag(with, ""), "the smoke flag is found without regard to case, and only as a whole argument");
        }

        private static int R27Count(string text, string needle)
        {
            int n = 0, at = 0;
            while ((at = text.IndexOf(needle, at, StringComparison.Ordinal)) >= 0) { n++; at += needle.Length; }
            return n;
        }

        private static void R27SummaryOnce()
        {
            string dir = R27TempDir("once");
            PlayLogWriter w = new PlayLogWriter(true, dir);
            double now = 0.0;
            PlayLogSession s = new PlayLogSession(w, delegate () { return now; }, "once0000");
            now = 1.0; s.Screen("MainMenu");
            now = 2.0; s.Cloud("Ready", "");
            now = 10.0; s.Summary();                       // the pause
            now = 10.1; s.Cloud("Working", "syncing");     // the cloud syncing as the app leaves
            now = 10.2; s.Summary();                       // the quit that follows the pause
            string text = File.ReadAllText(w.FilePath);
            Check(R27Count(text, " summary ") == 1 && text.TrimEnd().EndsWith("errors 0") && !text.Contains("syncing"),
                  "THE SUMMARY IS WRITTEN ONCE per session end, and a late cloud event after it is dropped (pause then quit gives one summary, nothing after it)");
            Check(s.Closed, "after the summary the session is closed until the app comes back");

            now = 20.0; s.Resume();
            now = 21.0; s.Summary();                       // came back, nothing happened, paused again
            Check(R27Count(File.ReadAllText(w.FilePath), " summary ") == 1, "resume then pause with no new event adds no second summary");
            now = 30.0; s.Resume();
            now = 31.0; s.Screen("Heroes");
            now = 32.0; s.Summary();
            string later = File.ReadAllText(w.FilePath);
            Check(R27Count(later, " summary ") == 2 && later.TrimEnd().EndsWith("screens 2 stages_cleared 0 purchases 0 errors 0"),
                  "a later summary is a new line only because more events happened since the previous one, and it is cumulative");
        }

        private static void R27SmokeIdentity(string contentDir)
        {
            string real = Path.Combine("data", "persistent");
            bool was = SmokeStorage.Active;
            try
            {
                SmokeStorage.Active = false;
                Check(SmokeStorage.SaveDirectory(real) == real && SmokeStorage.SavePrefsKey("emberfall.save.v1") == "emberfall.save.v1"
                      && SmokeStorage.CloudPrefsKey("emberfall.cloud.v1") == "emberfall.cloud.v1" && SmokeStorage.PlayLogFile == PlayLogRules.FileName
                      && SmokeStorage.PlayLogPrevFile == PlayLogRules.PrevFileName,
                      "SMOKE STORAGE OFF resolves every real name unchanged (a normal run is exactly as before)");
                SmokeStorage.Active = true;
                string dir = SmokeStorage.SaveDirectory(real);
                Check(dir != real && dir.StartsWith(real) && dir.EndsWith(SmokeStorage.FolderName) && Path.Combine(dir, "emberfall_save.json") != Path.Combine(real, "emberfall_save.json"),
                      "SMOKE STORAGE ON never resolves the real save path: the file is inside " + SmokeStorage.FolderName);
                Check(SmokeStorage.SavePrefsKey("emberfall.save.v1") != "emberfall.save.v1" && SmokeStorage.CloudPrefsKey("emberfall.cloud.v1") != "emberfall.cloud.v1",
                      "and neither the save mirror key nor the cloud session key is the real one");
                Check(SmokeStorage.PlayLogFile == "emberfall_playlog.smoke.txt" && SmokeStorage.PlayLogFile != PlayLogRules.FileName && SmokeStorage.PlayLogPrevFile != PlayLogRules.PrevFileName,
                      "and the play log is emberfall_playlog.smoke.txt, never the real log");
                string logDir = R27TempDir("smokelog");
                PlayLogWriter sw = new PlayLogWriter(true, logDir, SmokeStorage.PlayLogFile, SmokeStorage.PlayLogPrevFile);
                sw.Open();
                sw.Write("+1.0s [smoke] start");
                Check(File.Exists(Path.Combine(logDir, "emberfall_playlog.smoke.txt")) && !File.Exists(Path.Combine(logDir, PlayLogRules.FileName)), "a smoke writer creates only the smoke log file");
            }
            finally { SmokeStorage.Active = was; }
            Check(!SmokeStorage.ShouldActivate(false, new string[] { "-emberfallSmoke" }) && !SmokeStorage.ShouldActivate(true, new string[] { "-batchmode" })
                  && SmokeStorage.ShouldActivate(true, new string[] { "x", "-emberfallSmoke" }), "smoke storage activates only with the define compiled in AND the flag on the command line");
            Check(!SmokeStorage.Active, "smoke storage is off by default");

            string root = SealedRepoRoot(contentDir);
            if (root == null) { Check(false, "R27 smoke: the repository root was not found"); return; }
            string save = R27Read(root, "Assets", "Scripts", "Game", "SaveService.cs");
            string cloud = R27Read(root, "Assets", "Scripts", "Game", "Cloud", "CloudLocalState.cs");
            string svc = R27Read(root, "Assets", "Scripts", "Game", "PlayLog", "PlayLogService.cs");
            System.Text.RegularExpressions.Regex rawKey = new System.Text.RegularExpressions.Regex(@"PlayerPrefs\.\w+\(\s*PrefsKey");
            Check(save != null && save.Contains("SmokeStorage.SaveDirectory(Application.persistentDataPath)") && save.Contains("SmokeStorage.SavePrefsKey(PrefsKey)") && !rawKey.IsMatch(save)
                  && !save.Contains("Path.Combine(Application.persistentDataPath, name)"),
                  "SOURCE SCAN SaveService: the file path, the unreadable-copy path and every PlayerPrefs call go through SmokeStorage, none through the raw key");
            Check(cloud != null && cloud.Contains("SmokeStorage.CloudPrefsKey(PrefsKey)") && !rawKey.IsMatch(cloud), "SOURCE SCAN CloudLocalState: the session key goes through SmokeStorage, none through the raw key");
            bool noOther = true;
            string[] dirs = new string[] { Path.Combine(root, "Assets", "Scripts") };
            foreach (string f in Directory.GetFiles(dirs[0], "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(f);
                if (name == "SaveService.cs" || name == "SmokeStorage.cs") continue;
                string t = File.ReadAllText(f);
                if (t.Contains("emberfall_save") || t.Contains("SaveService.FilePath") || t.Contains("\"emberfall.save.v1\"") || t.Contains("\"emberfall.cloud.v1\"")) { if (name != "CloudLocalState.cs") noOther = false; }
            }
            Check(noOther, "no other runtime script names the real save file, its mirror key or the cloud session key");
            Check(svc != null && svc.Contains("DecideSmokeStorage") && svc.Contains("BeforeSceneLoad") && svc.Contains("SmokeStorage.PlayLogFile") && svc.Contains("SmokeStorage.Active = false"),
                  "PlayLogService decides smoke storage before any scene loads (before the save is read) and opens the smoke play log");
            string tele = R27Read(root, "Assets", "Scripts", "Game", "Telemetry", "TelemetryService.cs");
            Check(tele != null && tele.Contains("SmokeStorage.Active) return null"), "a smoke run installs no play stats, so it cannot queue or send the real install's events");
        }

        private static string R27Read(string root, params string[] parts)
        {
            string[] all = new string[parts.Length + 1];
            all[0] = root;
            Array.Copy(parts, 0, all, 1, parts.Length);
            string path = Path.Combine(all);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        private static void R27Wiring(string contentDir)
        {
            string root = SealedRepoRoot(contentDir);
            if (root == null) { Check(false, "R27: the repository root was not found from " + contentDir); return; }

            string service = R27Read(root, "Assets", "Scripts", "Game", "PlayLog", "PlayLogService.cs");
            string app = R27Read(root, "Assets", "Scripts", "Game", "GameApp.cs");
            Check(service != null && service.StartsWith("#if EMBERFALL_PLAYLOG") && service.TrimEnd().EndsWith("#endif"), "PlayLogService is compiled only under EMBERFALL_PLAYLOG (the whole file is inside the define)");
            Check(app != null && app.Contains("PlayLogService.Install(this)") && app.Contains("PlayLogService.OnPause(paused)") && app.Contains("PlayLogService.OnQuit()"), "GameApp installs the play log and writes the summary at pause and at quit");
            Check(service != null && service.Contains("Application.logMessageReceived") && service.Contains("LogType.Error") && service.Contains("-emberfallSmoke") && service.Contains("[smoke] cloud signed in anonymously (status Ready)")
                  && service.Contains("[smoke] done") && service.Contains("[smoke] ") && service.Contains("ACTION_SEND") && service.Contains("text/plain") && !service.Contains("FileProvider"),
                  "the service listens for errors, runs the smoke test with its grep-able lines, and shares through ACTION_SEND text/plain with no FileProvider");
            Check(service != null && service.Contains("Substring(0, 8)") && !service.Contains("AccessToken") && !service.Contains("supabase"), "the smoke test prints at most an 8 character id prefix and touches no token or config");
            string settings = R27Read(root, "Assets", "Scripts", "UI", "Screens", "SettingsScreen.cs");
            Check(settings != null && settings.Contains("#if EMBERFALL_PLAYLOG") && settings.Contains("SHARE PLAY LOG"), "Settings has the SHARE PLAY LOG button only under EMBERFALL_PLAYLOG");
            string purchase = R27Read(root, "Assets", "Scripts", "Game", "Store", "PurchaseService.cs");
            string cloud = R27Read(root, "Assets", "Scripts", "Game", "Cloud", "CloudCoordinator.cs");
            string flow = R27Read(root, "Assets", "Scripts", "Core", "Progression", "PurchaseFlow.cs");
            string hooks = R27Read(root, "Assets", "Scripts", "Core", "Telemetry", "TelemetryHooks.cs");
            Check(purchase != null && purchase.Contains("WatchPurchases") && cloud != null && cloud.Contains("PlayLogHooks.Cloud") && flow != null && flow.Contains("PlayLogHooks.Purchase") && hooks != null && hooks.Contains("PlayLogHooks.Stage"),
                  "the hooks are in place: purchase service and flow, cloud status, stage taps");
            Check(cloud != null && cloud.Contains("if (PlayLogSwitch.Enabled)") && flow != null && flow.Contains("PlayLogSwitch.Enabled && !r.Success") && hooks != null && hooks.Contains("PlayLogSwitch.Enabled && stage != null"),
                  "each Core and cloud hook is guarded by PlayLogSwitch.Enabled, so it costs nothing when the define is off");

            // The build plumbing.
            string build = R27Read(root, "Assets", "Editor", "EmberfallBuild.cs");
            Check(build != null && build.Contains("-emberfallDefines") && build.Contains("BuildDefines.Parse") && build.Contains("extraScriptingDefines") && !build.Contains("SetScriptingDefineSymbols"),
                  "EmberfallBuild reads -emberfallDefines and applies it through extraScriptingDefines, never SetScriptingDefineSymbols");
            Check(build != null && build.Contains("BuildAndroidPlaytest") && build.Contains("ApplyReleaseBackend") && build.Contains("BuildTarget.Android"), "EmberfallBuild has the dialog free Android playtest entry that applies the release backend");
            string win = R27Read(root, "Tools", "build-windows.ps1");
            string and = R27Read(root, "Tools", "build-android.ps1");
            string friends = R27Read(root, "Tools", "build-friends.ps1");
            Check(win != null && win.Contains("[string]$Defines") && win.Contains("-emberfallDefines") && win.Contains("-Cloud"), "build-windows.ps1 has the optional -Defines parameter and keeps -Cloud");
            Check(and != null && and.Contains("-buildTarget") && and.Contains("BuildAndroidPlaytest") && and.Contains("-emberfallDefines") && and.Contains("supabase.json") && and.Contains(".sizes.txt") && and.Contains("Get-TreeProblems") && and.Contains("-logFile"),
                  "build-android.ps1 builds for the Android target through BuildAndroidPlaytest with defines, the stash rules, a tree check, a size report and a kept log");
            Check(friends != null && friends.Contains("EMBERFALL_TESTSTORE;EMBERFALL_PLAYLOG") && friends.Contains("-Cloud") && friends.Contains("[switch]$WhatIf") && friends.Contains("friends-") && friends.Contains("Get-AndroidInstallSteps"),
                  "build-friends.ps1 builds both with -Cloud and the two defines into friends-<date>, has -WhatIf, and prints the Android install steps");
            bool noDelete = true;
            string[] scripts = new string[] { friends, and };
            for (int i = 0; i < scripts.Length; i++)
            {
                if (scripts[i] == null) { noDelete = false; continue; }
                if (scripts[i].Contains("Remove-Item") || scripts[i].Contains("rmdir") || scripts[i].Contains("Delete(")) noDelete = false;
            }
            Check(noDelete, "build-friends.ps1 and build-android.ps1 delete nothing (no Remove-Item, rmdir or Delete)");

            // The ps1 files that call a build never print the cloud config.
            Check(friends != null && !friends.Contains("Get-Content $config") && !friends.Contains("ConvertFrom-Json"), "build-friends.ps1 never opens supabase.json");

            // The new files hold no em or en dash.
            string[] mine = new string[] {
                Path.Combine("Assets", "Scripts", "Core", "Session", "PlayLog.cs"), Path.Combine("Assets", "Scripts", "Core", "Session", "PlayLogSwitch.cs"),
                Path.Combine("Assets", "Scripts", "Game", "PlayLog", "PlayLogService.cs"), Path.Combine("Tools", "build-android.ps1"), Path.Combine("Tools", "build-friends.ps1"),
                Path.Combine("Tools", "unity-editor.ps1"), Path.Combine("Tools", "DevHarness", "R27PlayLogChecks.cs"), Path.Combine("Docs", "FRIENDS-BUILD.md") };
            bool noDash = true;
            string where = "";
            for (int i = 0; i < mine.Length; i++)
            {
                string full = Path.Combine(root, mine[i]);
                if (!File.Exists(full)) continue;
                string t = File.ReadAllText(full);
                if (t.IndexOf((char)0x2014) >= 0 || t.IndexOf((char)0x2013) >= 0) { noDash = false; where += mine[i] + " "; }
            }
            Check(noDash, "the R27 files hold no em or en dash " + where);

            R27DryRun(root);
        }

        // Runs build-friends.ps1 -WhatIf where Windows PowerShell exists: it must print both commands and launch nothing.
        private static void R27DryRun(string root)
        {
            if (Path.DirectorySeparatorChar != '\\') { Line("  [skip] build-friends dry run: Windows PowerShell only"); return; }
            string script = Path.Combine(root, "Tools", "build-friends.ps1");
            if (!File.Exists(script)) { Check(false, "Tools/build-friends.ps1 is missing"); return; }
            try
            {
                ProcessStartInfo start = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" -WhatIf -ProjectRoot \"" + root + "\"");
                start.UseShellExecute = false;
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;
                start.CreateNoWindow = true;
                string output;
                int exit;
                using (Process p = Process.Start(start))
                {
                    output = p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(60000)) { try { p.Kill(); } catch (Exception) { } Check(false, "the build-friends dry run did not finish in 60 s"); return; }
                    exit = p.ExitCode;
                }
                Check(exit == 0 && output.Contains("DRY RUN") && output.Contains("build-windows.ps1") && output.Contains("-Cloud") && output.Contains("EMBERFALL_TESTSTORE;EMBERFALL_PLAYLOG")
                      && output.Contains("friends-"),
                      "THE DRY RUN prints the Windows command (-Cloud, both defines, friends-<date> folder) and exits 0 without launching Unity");
            }
            catch (Exception ex)
            {
                Line("  [skip] build-friends dry run: " + ex.Message);
            }
        }
    }
}
