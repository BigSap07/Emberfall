using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Emberfall.Game
{
    /// <summary>
    /// A record of every cue the audio service was asked for, with the time it fired: the tester's
    /// play proof (R10-AUDIO task 5). The two defects the proof looks for are both readable here:
    /// a cue that fires with no file (a NOFILE or UNKNOWN line) and a cue that fires more than
    /// eight times in one second (a RATE line, written the moment it happens).
    ///
    /// Always kept in memory (the last MaxLines), and written to a text file when the environment
    /// variable EMBERFALL_CUE_LOG names one, or when the player was started with -cuelog (the file
    /// is then audio-cue-log.txt in the persistent data path). When a file is on, each line is also
    /// written to the Unity log behind an [AudioCue] prefix. Nothing here changes what plays.
    /// </summary>
    public static class AudioCueLog
    {
        public const string EnvVar = "EMBERFALL_CUE_LOG";
        public const int MaxLines = 4000;
        public const int RateLimitPerSecond = 8;

        public static readonly List<string> Lines = new List<string>();

        /// <summary>Count of lines by kind (PLAY, SILENT, NOFILE, ...), for the end-of-session summary.</summary>
        public static readonly Dictionary<string, int> Counts = new Dictionary<string, int>();

        private static readonly Dictionary<string, Queue<float>> Recent = new Dictionary<string, Queue<float>>();
        private static string _path;
        private static bool _pathResolved;

        public static string FilePath
        {
            get { Resolve(); return _path; }
        }

        /// <summary>Test seam: aim the log at a file, or at nothing with null.</summary>
        public static void SetFile(string path)
        {
            _path = path;
            _pathResolved = true;
        }

        public static void Record(string kind, string cue, string detail)
        {
            float t = Time.realtimeSinceStartup;
            StringBuilder b = new StringBuilder();
            b.Append("t=").Append(t.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
            b.Append(' ').Append(kind).Append(' ').Append(cue);
            if (!string.IsNullOrEmpty(detail)) b.Append(' ').Append(detail);
            Write(b.ToString());

            int n;
            Counts.TryGetValue(kind, out n);
            Counts[kind] = n + 1;

            if (kind == "PLAY") CheckRate(cue, t);
        }

        /// <summary>The kind counts as one line each, for the end of a session.</summary>
        public static string Summary()
        {
            StringBuilder b = new StringBuilder("summary");
            foreach (KeyValuePair<string, int> pair in Counts) b.Append(' ').Append(pair.Key).Append('=').Append(pair.Value);
            return b.ToString();
        }

        public static void WriteSummary()
        {
            if (Counts.Count > 0) Write("t=" + Time.realtimeSinceStartup.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " " + Summary());
        }

        private static void CheckRate(string cue, float t)
        {
            Queue<float> q;
            if (!Recent.TryGetValue(cue, out q)) { q = new Queue<float>(); Recent[cue] = q; }
            q.Enqueue(t);
            while (q.Count > 0 && t - q.Peek() > 1f) q.Dequeue();
            if (q.Count > RateLimitPerSecond)
            {
                // Written once per excess play, so the log shows how long the burst ran.
                Write("t=" + t.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                      + " RATE " + cue + " fired " + q.Count + " times in one second");
                int n;
                Counts.TryGetValue("RATE", out n);
                Counts["RATE"] = n + 1;
            }
        }

        private static void Write(string line)
        {
            if (Lines.Count >= MaxLines) Lines.RemoveAt(0);
            Lines.Add(line);
            Resolve();
            if (string.IsNullOrEmpty(_path)) return;
            Debug.Log("[AudioCue] " + line);
            try { File.AppendAllText(_path, line + "\n"); }
            catch (Exception) { /* a log that cannot be written must never stop a sound */ }
        }

        private static void Resolve()
        {
            if (_pathResolved) return;
            _pathResolved = true;
            try
            {
                string env = Environment.GetEnvironmentVariable(EnvVar);
                if (!string.IsNullOrEmpty(env)) { _path = env; return; }
                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "-cuelog")
                    {
                        _path = Path.Combine(Application.persistentDataPath, "audio-cue-log.txt");
                        return;
                    }
                }
            }
            catch (Exception) { _path = null; }
        }
    }
}
