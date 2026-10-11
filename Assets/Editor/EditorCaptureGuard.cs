#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Emberfall.Game;
using Emberfall.Game.Cloud;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// The only door into Play mode for editor automation, and the one place the forced-offline
    /// switch (SupabaseConfig.ForcedOffline) is turned on.
    ///
    /// Why this exists: the worktree holds the gitignored Assets/Resources/Config/supabase.json,
    /// so a Play session that does nothing about it builds the cloud stack and signs in as the real
    /// account. A capture once came back covered by the real account's "TWO SAVES" conflict
    /// chooser. Agents stayed safe only by moving the file by hand. Now every capture and every
    /// editor-driven run enters Play mode through EnterPlayMode, which arms the switch first, so
    /// the game behaves as if supabase.json were absent whatever files exist.
    ///
    /// The switch is scoped to ONE Play session: it is cleared again when Play mode ends. It lives
    /// in SessionState (this editor process only), so another agent's Unity on the same machine
    /// can neither turn it off under a running capture nor inherit it.
    ///
    /// Tools/build-check.ps1 fails the build if any file under Assets/Editor sets
    /// EditorApplication.isPlaying = true other than this one, so a new capture tool cannot forget.
    /// Nothing here is compiled into a player: the file is editor-only and the switch it sets
    /// exists only under UNITY_EDITOR.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorCaptureGuard
    {
        static EditorCaptureGuard()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        /// <summary>Holds the cloud off for the next Play session without entering it. For the
        /// rare tool that has to hand Play mode to something else; everything else calls
        /// EnterPlayMode.</summary>
        public static void Arm()
        {
            SupabaseConfig.ForcedOffline = true;
            // A fresh count for the session about to start, so Verify measures this session only
            // (with domain reload off, statics would otherwise carry over from the last one).
            SupabaseConfig.ClientsConstructed = 0;
        }

        /// <summary>Arms the switch, then enters Play mode. The only place in Assets/Editor
        /// allowed to assign EditorApplication.isPlaying = true.</summary>
        public static void EnterPlayMode()
        {
            Arm();
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            // Back in edit mode means the armed session is over. Cleared here rather than by each
            // tool so a tool that crashes mid-run cannot leave a later, human Play session offline.
            if (change == PlayModeStateChange.EnteredEditMode)
                SupabaseConfig.ForcedOffline = false;
        }

        /// <summary>
        /// Checks, from the running game, that this Play session really is offline and (when asked)
        /// on a scratch identity. Logs [ok] or [FAIL] lines and returns false on any failure, so a
        /// capture can refuse to photograph a run that could reach the real account or write the
        /// real save. Prints only booleans and counts, never anything read from supabase.json.
        /// </summary>
        public static bool Verify(string tool, bool requireScratch)
        {
            bool ok = true;
            GameApp app = GameApp.Instance;

            bool armed = SupabaseConfig.ForcedOffline;
            ok &= Report(tool, armed, "forced-offline switch is set (SupabaseConfig.ForcedOffline=" + armed + ")");

            bool noCloud = app != null && app.Cloud == null;
            ok &= Report(tool, noCloud, "GameApp.Cloud is null (no sign-in, no sync, no conflict chooser)");

            int built = SupabaseConfig.ClientsConstructed;
            ok &= Report(tool, built == 0, "SupabaseClient instances constructed this session = " + built);

            if (requireScratch)
            {
                bool scratch = EditorScratchIdentity.IsScratch;
                ok &= Report(tool, scratch, "running on a scratch identity, so the real local save is not written");
            }
            return ok;
        }

        private static bool Report(string tool, bool pass, string what)
        {
            if (pass) Debug.Log(tool + ": [ok] " + what);
            else Debug.LogError(tool + ": [FAIL] " + what);
            return pass;
        }
    }
}
#endif
