using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Emberfall.Core.Session
{
    /// <summary>
    /// THE PLAY LOG'S RULES (R27, Docs/FRIENDS-BUILD.md): names, caps and the pure text functions. Engine-free, so the
    /// harness proves them without Unity. Nothing here is read by a fight or written to a save.
    /// </summary>
    public static class PlayLogRules
    {
        public const string FileName = "emberfall_playlog.txt";
        public const string PrevFileName = "emberfall_playlog.prev.txt";

        /// <summary>A file larger than this is moved to the prev name when the next session starts.</summary>
        public const long MaxFileBytes = 200 * 1024;

        /// <summary>Errors one session may write. The next one writes a single "errors capped" line.</summary>
        public const int MaxErrorsPerSession = 25;

        /// <summary>Lines one session may write, so a loop of events cannot fill the file in one sitting.</summary>
        public const int MaxLinesPerSession = 2500;

        public const int MaxLineChars = 220;

        /// <summary>What the share text may hold. Well under what an Android intent extra safely carries.</summary>
        public const int MaxShareChars = 12000;

        public const string Banner =
            "# Emberfall play log. This file stays on this device. The game never sends it anywhere, whatever you chose for the "
          + "play stats: it is local, and you send it by hand (Settings, SHARE PLAY LOG). It holds screens opened, stage "
          + "results, test store purchases, cloud status and errors. No save data and no account details.";

        public static bool NeedsRotation(long fileBytes)
        {
            return fileBytes > MaxFileBytes;
        }

        /// <summary>True when the argument list holds the flag, compared without regard to case.</summary>
        public static bool HasFlag(string[] args, string flag)
        {
            if (args == null || string.IsNullOrEmpty(flag)) return false;
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>One line of text: control characters become spaces, runs of spaces collapse, and it is cut at max
        /// characters with three dots. Null is empty.</summary>
        public static string Clean(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            StringBuilder b = new StringBuilder(Math.Min(text.Length, max + 4));
            bool space = true;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c < ' ' || c == '\u007f') c = ' ';
                if (c == ' ')
                {
                    if (space) continue;
                    space = true;
                }
                else space = false;
                b.Append(c);
            }
            string s = b.ToString().TrimEnd();
            if (max > 3 && s.Length > max) s = s.Substring(0, max - 3) + "...";
            return s;
        }

        /// <summary>
        /// The text the Share button hands over: a first line naming the last session summary, then the tail of the
        /// file, the whole result cut at maxChars. The tail starts on a line boundary so no line is half a line.
        /// </summary>
        public static string ShareText(string fileText, int maxChars)
        {
            if (maxChars < 200) maxChars = 200;
            if (string.IsNullOrEmpty(fileText)) return "Emberfall play log: empty.";
            string text = fileText.Replace("\r\n", "\n");
            string[] lines = text.Split('\n');
            string summary = "(no session summary yet)";
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (lines[i].IndexOf(" summary ", StringComparison.Ordinal) >= 0) { summary = lines[i].Trim(); break; }
            }
            string head = "Emberfall play log. Last session summary: " + summary + "\n\n";
            if (head.Length > maxChars / 2) head = head.Substring(0, maxChars / 2) + "\n\n";
            int room = maxChars - head.Length;
            string tail = text;
            if (tail.Length > room)
            {
                tail = tail.Substring(tail.Length - room);
                int nl = tail.IndexOf('\n');
                if (nl >= 0 && nl + 1 < tail.Length) tail = "..." + tail.Substring(nl);
                else tail = "..." + tail;
                if (tail.Length > room) tail = tail.Substring(tail.Length - room);
            }
            return head + tail;
        }
    }

    /// <summary>
    /// The scripting defines of a build, parsed from the text the build scripts pass ("A;B"). Pure, so the harness
    /// proves what EmberfallBuild applies to a player build and what the play log header prints.
    /// </summary>
    public static class BuildDefines
    {
        /// <summary>Splits on ';' and ',', trims, drops empties and duplicates, keeps the order. Only names made of
        /// letters, digits and underscores that do not start with a digit survive: anything else is not a define.</summary>
        public static string[] Parse(string text)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(text)) return result.ToArray();
            string[] parts = text.Split(new char[] { ';', ',' });
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (!IsName(p) || result.Contains(p)) continue;
                result.Add(p);
            }
            return result.ToArray();
        }

        public static string Join(string[] defines)
        {
            return defines == null ? "" : string.Join(";", defines);
        }

        public static bool IsName(string s)
        {
            if (string.IsNullOrEmpty(s) || (s[0] >= '0' && s[0] <= '9')) return false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// THE FILE (append-only, capped by rotation). Created disabled it never touches the disk, which is what "off means
    /// no file" is. An IO failure is swallowed: a log that cannot be written must never break the game.
    /// </summary>
    public sealed class PlayLogWriter
    {
        private readonly bool _enabled;
        private readonly string _path;
        private readonly string _prevPath;
        private int _lines;
        private bool _opened;

        public PlayLogWriter(bool enabled, string directory)
            : this(enabled, directory, PlayLogRules.FileName, PlayLogRules.PrevFileName)
        {
        }

        /// <summary>With the file names given: a smoke run writes emberfall_playlog.smoke.txt (SmokeStorage), never the real log.</summary>
        public PlayLogWriter(bool enabled, string directory, string fileName, string prevFileName)
        {
            _enabled = enabled && !string.IsNullOrEmpty(directory);
            _path = _enabled ? Path.Combine(directory, fileName) : "";
            _prevPath = _enabled ? Path.Combine(directory, prevFileName) : "";
        }

        public bool Enabled { get { return _enabled; } }
        public string FilePath { get { return _path; } }
        public int Lines { get { return _lines; } }

        /// <summary>Moves a file past the cap to the prev name (replacing an older prev; the log itself is never
        /// deleted), and writes the banner when the file is new. Returns true when it rotated.</summary>
        public bool Open()
        {
            if (!_enabled) return false;
            _opened = true;
            bool rotated = false;
            try
            {
                if (File.Exists(_path) && PlayLogRules.NeedsRotation(new FileInfo(_path).Length))
                {
                    if (File.Exists(_prevPath)) File.Replace(_path, _prevPath, null);
                    else File.Move(_path, _prevPath);
                    rotated = true;
                }
                if (!File.Exists(_path)) File.WriteAllText(_path, PlayLogRules.Banner + "\n", new UTF8Encoding(false));
            }
            catch (Exception) { }
            return rotated;
        }

        /// <summary>Appends one line. False when disabled, over the session's line budget, or on an IO failure.</summary>
        public bool Write(string line)
        {
            return Append(line, true);
        }

        /// <summary>Appends a line that is not charged to the session's line budget (the summary).</summary>
        public bool WriteAlways(string line)
        {
            return Append(line, false);
        }

        private bool Append(string line, bool budgeted)
        {
            if (!_enabled || line == null) return false;
            if (budgeted && _lines >= PlayLogRules.MaxLinesPerSession) return false;
            try
            {
                if (!_opened) Open();
                File.AppendAllText(_path, line + "\n", new UTF8Encoding(false));
                _lines++;
                return true;
            }
            catch (Exception) { return false; }
        }

        /// <summary>The file's text, or "" when it is not there or cannot be read.</summary>
        public string ReadAll()
        {
            if (!_enabled) return "";
            try { return File.Exists(_path) ? File.ReadAllText(_path) : ""; }
            catch (Exception) { return ""; }
        }
    }

    /// <summary>
    /// ONE SESSION OF THE LOG: the counts, the line formats and the writes. The clock is a function (seconds since the
    /// session started) so the harness can drive it. A session is one run of the game; the summary line is cumulative
    /// and is written at every pause and at quit.
    /// </summary>
    public sealed class PlayLogSession
    {
        private readonly PlayLogWriter _writer;
        private readonly Func<double> _clock;
        private int _errors;
        private bool _errorsCapped;
        private bool _closed;
        private bool _summaryWritten;
        private int _events;

        /// <summary>The campaign event names the stage tap hands over. Mirrors of the stats schema's constants (the harness
        /// proves they are equal), kept as text here so the play log has no dependency on the stats code.</summary>
        public const string StageAttemptEvent = "stage_attempt";
        public const string StageClearEvent = "stage_clear";
        public const string WaveLostEvent = "wave_lost";

        public string SessionId { get; private set; }
        public int ScreensOpened { get; private set; }
        public int StagesCleared { get; private set; }
        public int Purchases { get; private set; }
        public int PurchasesRefused { get; private set; }
        public int Errors { get { return _errors; } }

        public PlayLogSession(PlayLogWriter writer, Func<double> clock, string sessionId)
        {
            _writer = writer;
            _clock = clock ?? delegate () { return 0.0; };
            SessionId = sessionId ?? "";
        }

        public double Seconds { get { return _clock(); } }

        // ---------------------------------------------------------------- the line formats (pure)

        public static string StampOf(double seconds)
        {
            return "+" + seconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s";
        }

        public static string HeaderLines(string sessionId, string utcStart, string build, string commit, string platform,
                                         string device, string defines)
        {
            return "== session " + PlayLogRules.Clean(sessionId, 16) + " started " + PlayLogRules.Clean(utcStart, 24) + " ==\n"
                 + "build " + PlayLogRules.Clean(build, 24) + " (" + PlayLogRules.Clean(commit, 24) + ") | platform "
                 + PlayLogRules.Clean(platform, 24) + " | device " + PlayLogRules.Clean(device, 60)
                 + " | defines " + PlayLogRules.Clean(defines, 120);
        }

        public static string ScreenLine(double t, string screenId)
        {
            return StampOf(t) + " screen " + PlayLogRules.Clean(screenId, 40);
        }

        /// <summary>result is attempt, win or lose; wave and battle are given for a lose.</summary>
        public static string StageLine(double t, string stageId, string result, int wave, int battle)
        {
            string line = StampOf(t) + " stage " + PlayLogRules.Clean(stageId, 40) + " " + result;
            if (result == "lose") line += " wave " + wave + " battle " + battle;
            return line;
        }

        public static string PurchaseLine(double t, string offerId, bool bought, string reason)
        {
            string line = StampOf(t) + " purchase offer " + PlayLogRules.Clean(offerId, 60) + " result " + (bought ? "bought" : "refused");
            if (!bought && !string.IsNullOrEmpty(reason)) line += " reason " + PlayLogRules.Clean(reason, 80);
            return line;
        }

        public static string CloudLine(double t, string status, string message)
        {
            string line = StampOf(t) + " cloud " + PlayLogRules.Clean(status, 24);
            string m = PlayLogRules.Clean(message, 80);
            if (m.Length > 0) line += " (" + m + ")";
            return line;
        }

        public static string ErrorLine(double t, string message)
        {
            return StampOf(t) + " error " + PlayLogRules.Clean(message, PlayLogRules.MaxLineChars);
        }

        public static string SummaryLine(double t, int screens, int stagesCleared, int purchases, int errors)
        {
            return StampOf(t) + " summary length " + ((long)Math.Round(t)) + "s screens " + screens + " stages_cleared " + stagesCleared
                 + " purchases " + purchases + " errors " + errors;
        }

        // ---------------------------------------------------------------- the events

        public void Header(string utcStart, string build, string commit, string platform, string device, string defines)
        {
            _writer.Write(HeaderLines(SessionId, utcStart, build, commit, platform, device, defines));
        }

        public void Screen(string screenId)
        {
            if (_closed) return;
            _events++;
            ScreensOpened++;
            _writer.Write(ScreenLine(_clock(), screenId));
        }

        public void Stage(string eventName, string stageId, int wave, int battle)
        {
            string result;
            if (eventName == StageAttemptEvent) result = "attempt";
            else if (eventName == StageClearEvent) { if (!_closed) StagesCleared++; result = "win"; }
            else if (eventName == WaveLostEvent) result = "lose";
            else return;
            if (_closed) return;
            _events++;
            _writer.Write(StageLine(_clock(), stageId, result, wave, battle));
        }

        public void Purchase(string offerId, bool bought, string reason)
        {
            if (_closed) return;
            _events++;
            if (bought) Purchases++; else PurchasesRefused++;
            _writer.Write(PurchaseLine(_clock(), offerId, bought, reason));
        }

        public void Cloud(string status, string message)
        {
            if (_closed) return;
            _events++;
            _writer.Write(CloudLine(_clock(), status, message));
        }

        public void Error(string message)
        {
            if (_closed) return;
            _events++;
            if (_errors >= PlayLogRules.MaxErrorsPerSession)
            {
                if (_errorsCapped) return;
                _errorsCapped = true;
                _writer.Write(StampOf(_clock()) + " error (more errors this session are not logged)");
                return;
            }
            _errors++;
            _writer.Write(ErrorLine(_clock(), message));
        }

        /// <summary>A free line (the smoke test's), stamped like the others.</summary>
        public void Note(string text)
        {
            if (_closed) return;
            _events++;
            _writer.Write(StampOf(_clock()) + " " + PlayLogRules.Clean(text, PlayLogRules.MaxLineChars));
        }

        /// <summary>
        /// The session end summary, cumulative, written even when the line budget is spent. WRITTEN ONCE PER END: the
        /// first call after the session started (or after Resume) writes it and CLOSES the session, so a quit that
        /// follows a pause writes nothing more, and a late event (the cloud syncing as the app leaves) is dropped
        /// instead of landing after the summary. After Resume (the app came back) the next summary is a new line only
        /// if events happened since the previous one.
        /// </summary>
        public void Summary()
        {
            if (_closed) return;
            if (!_summaryWritten || _events > 0)
            {
                _writer.WriteAlways(SummaryLine(_clock(), ScreensOpened, StagesCleared, Purchases, _errors));
                _summaryWritten = true;
            }
            _events = 0;
            _closed = true;
        }

        /// <summary>The app came back to the foreground: events are recorded again.</summary>
        public void Resume()
        {
            _closed = false;
            _events = 0;
        }

        public bool Closed { get { return _closed; } }
    }

    /// <summary>
    /// THE TAPS (like TelemetryHooks): the game calls these where a stage is attempted, a purchase is refused and the
    /// cloud changes status. Active is null unless the define is on and the Game installed a session, so with the
    /// define off every call is one comparison and nothing is written. A tap never throws.
    /// </summary>
    public static class PlayLogHooks
    {
        public static PlayLogSession Active;

        public static void Stage(string eventName, string stageId, int wave, int battle)
        {
            PlayLogSession s = Active;
            if (s == null) return;
            try { s.Stage(eventName, stageId, wave, battle); } catch (Exception) { }
        }

        public static void Cloud(string status, string message)
        {
            PlayLogSession s = Active;
            if (s == null) return;
            try { s.Cloud(status, message); } catch (Exception) { }
        }

        public static void Purchase(string offerId, bool bought, string reason)
        {
            PlayLogSession s = Active;
            if (s == null) return;
            try { s.Purchase(offerId, bought, reason); } catch (Exception) { }
        }
    }
}
