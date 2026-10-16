using System;
using System.IO;

namespace Emberfall.Core.Telemetry
{
    /// <summary>
    /// Where the telemetry document lives between launches: one text blob, read whole and written
    /// whole. The document is the consent answer, the install id and the unsent queue, and it is
    /// DELIBERATELY NOT THE SAVE: nothing about it travels with a save copied to another phone, and
    /// it costs no save version. The game stores it as a file in the persistent data path; the
    /// harness uses the same file class on a temp path, or a memory store.
    /// </summary>
    public interface ITelemetryStore
    {
        /// <summary>The whole document, or null when there is none yet.</summary>
        string Read();
        void Write(string text);
        void Delete();
    }

    /// <summary>A file. Written beside itself and moved into place, so a launch that dies mid-write
    /// leaves the last whole document rather than half of the new one.</summary>
    public sealed class FileTelemetryStore : ITelemetryStore
    {
        private readonly string _path;

        public FileTelemetryStore(string path) { _path = path; }

        public string Path { get { return _path; } }

        public string Read()
        {
            return File.Exists(_path) ? File.ReadAllText(_path) : null;
        }

        public void Write(string text)
        {
            string folder = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) Directory.CreateDirectory(folder);
            string temp = _path + ".tmp";
            File.WriteAllText(temp, text);
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(temp, _path);
        }

        public void Delete()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
    }

    /// <summary>A store in memory, for the harness: counts what was done to it and can be told to
    /// fail, so a check can prove a broken disk never reaches the game as an exception.</summary>
    public sealed class MemoryTelemetryStore : ITelemetryStore
    {
        public string Text;
        public int Reads, Writes, Deletes;
        public bool Throw;

        public string Read()
        {
            Reads++;
            if (Throw) throw new IOException("memory store told to fail");
            return Text;
        }

        public void Write(string text)
        {
            Writes++;
            if (Throw) throw new IOException("memory store told to fail");
            Text = text;
        }

        public void Delete()
        {
            Deletes++;
            if (Throw) throw new IOException("memory store told to fail");
            Text = null;
        }
    }
}
