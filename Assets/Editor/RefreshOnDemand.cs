#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// MAKES AN UNFOCUSED EDITOR COMPILE, without asking Windows for anything.
    ///
    /// The capture pipeline's hard requirement is that the editor has compiled the current sources
    /// before a screenshot is taken - capture-hub.sh refuses to fire otherwise, because a capture
    /// of a stale assembly is a picture of the previous build that looks exactly like a picture of
    /// this one. Until now the only way to make that happen was to give the editor window OS
    /// focus, because Unity's automatic refresh runs on focus rather than on a timer.
    ///
    /// That is a fragile thing to depend on and it failed outright the first time two editors were
    /// open at once, which is the ordinary state of this repository: a worktree is a second
    /// checkout, and a second checkout wants a second editor. Windows' foreground lock refused the
    /// switch, focus-unity.ps1 reported it honestly, and the capture could not proceed at all.
    ///
    /// EditorApplication.update ticks whether or not the window has focus - that is the whole
    /// reason the trigger-file pattern works in the first place - so the refresh can simply be
    /// ASKED FOR from in here instead of being coaxed out of the window manager. Dropping a file
    /// at Tools/Screenshots/.refresh-trigger makes this call AssetDatabase.Refresh and request a
    /// script compilation, which is exactly what focusing the window would have caused.
    ///
    /// It imports and compiles; it does not enter Play Mode and does not capture anything. The
    /// tools that do those things keep their own triggers.
    /// </summary>
    [InitializeOnLoad]
    public static class RefreshOnDemand
    {
        private const string TriggerPath = "Tools/Screenshots/.refresh-trigger";

        static RefreshOnDemand()
        {
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            // Not during a compile or a play-mode change: Refresh in the middle of either is at
            // best ignored and at worst a reload in the middle of a reload.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            string path = Path.Combine(Application.dataPath, "../" + TriggerPath);
            if (!File.Exists(path)) return;
            File.Delete(path);

            Debug.Log("RefreshOnDemand: refreshing the asset database and requesting a script "
                    + "compilation, because something outside the editor asked for one.");
            AssetDatabase.Refresh();
            CompilationPipeline.RequestScriptCompilation();
        }
    }
}
#endif
