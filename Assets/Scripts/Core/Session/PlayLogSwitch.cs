namespace Emberfall.Core.Session
{
    /// <summary>
    /// THE PLAY LOG'S DEFINE (R27, Docs/FRIENDS-BUILD.md): a build compiled with EMBERFALL_PLAYLOG keeps a small
    /// human-readable text file on the device (emberfall_playlog.txt) and, with the command line argument
    /// -emberfallSmoke, runs the cloud smoke test. Off by default: without the define no file is written, no
    /// hook does anything and no log line is printed. Read only; the define is the only way to turn it on.
    /// Independent of the play stats choice, because the file is local and the player sends it by hand.
    /// </summary>
    public static class PlayLogSwitch
    {
#if EMBERFALL_PLAYLOG
        public static readonly bool Enabled = true;
#else
        public static readonly bool Enabled = false;
#endif

        /// <summary>The on/off rule as a pure function, so the harness proves it without recompiling: on exactly
        /// when the define is set.</summary>
        public static bool Resolve(bool defineSet)
        {
            return defineSet;
        }
    }
}
