using UnityEngine;

namespace Emberfall.Game
{
    /// <summary>
    /// WHICH BUILD THIS IS, in the two words a bug report needs: the version and the commit.
    ///
    /// Two people testing on two machines have to name a build in every report, and asking a
    /// person which commit they ran does not work - so the game says it, small, in a corner of
    /// the Hub and at the foot of the settings page.
    ///
    /// The version is Application.version, which is PlayerSettings.bundleVersion as the build saw
    /// it. The commit is not something a player can know about itself, so the build writes it:
    /// EmberfallBuild puts the short hash in Resources/BuildStamp.txt immediately before
    /// BuildPlayer and deletes it straight after. The file is gitignored, so writing it cannot
    /// dirty the tree whose hash it records.
    ///
    /// THREE ANSWERS, told apart rather than flattened. A stamped player names its commit. The
    /// editor says "editor", because it runs whatever is on disk and no commit describes that. A
    /// player built through any door that did not stamp it says "unstamped", which is true, and
    /// is also the prompt to build it through Tools/build-windows.ps1 instead.
    /// </summary>
    public static class BuildStamp
    {
        /// <summary>The Resources path EmberfallBuild writes and this reads, without extension.</summary>
        public const string ResourceName = "BuildStamp";

        private static string _commit;

        /// <summary>The short hash the player was built from, "editor", or "unstamped". Read
        /// once: the stamp is baked into the player and cannot change while it runs.</summary>
        public static string Commit
        {
            get
            {
                if (_commit != null) return _commit;
                TextAsset stamp = Resources.Load<TextAsset>(ResourceName);
                string text = stamp != null && stamp.text != null ? stamp.text.Trim() : "";
                _commit = text.Length > 0 ? text : (Application.isEditor ? "editor" : "unstamped");
                return _commit;
            }
        }

        /// <summary>
        /// "BUILD 0.3.0 (hash)", with DEV after it on a development player. A development player
        /// and a release player built from the same commit are different builds - one has the
        /// Dev Arena and the telemetry lines, the other does not - so a report has to be able to
        /// tell them apart as well.
        /// </summary>
        public static string Label
        {
            get
            {
                string label = "BUILD " + Application.version + " (" + Commit + ")";
#if DEVELOPMENT_BUILD
                label += " DEV";
#endif
                return label;
            }
        }
    }
}
