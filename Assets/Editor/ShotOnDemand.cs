#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// Photographs whatever the Game view is showing RIGHT NOW, without restarting anything.
    ///
    /// PlayModeScreenshot is the other tool and it answers a different question. That one drives a
    /// scripted run: it opens the scene, enters Play mode, walks a fixed schedule of screens and
    /// stops. It cannot photograph a session that is already running, because Begin() starts by
    /// re-entering Play mode - which is exactly right for "capture the Hub, unattended" and
    /// exactly wrong for "capture the state I have spent four network round trips arriving at".
    ///
    /// Every screen in the accounts feature is the second kind. A signed-in Account screen needs a
    /// live account; a Friends list with a pending request needs a SECOND live account that has
    /// sent one; the conflict chooser needs a third device to have written a save in between. None
    /// of that can be reconstructed by a schedule, and all of it is lost the moment Play mode
    /// restarts. So: a capture that takes the session as it finds it.
    ///
    /// TRIGGERED BY A FILE, for the reason PlayModeScreenshot's own trigger note sets out at
    /// length - a file is the one signal that reaches already-running code with no UI interaction
    /// and no window focus fight. Here there is a second reason: the Editor's MCP bridge refuses
    /// to execute ScreenCapture at all from a tool call, so an agent driving the session cannot
    /// take the photograph itself however it asks. It can write a file.
    ///
    ///     Tools/Screenshots/.shot        <- contains the file name to write, e.g. "acct_account"
    ///
    /// Read and deleted in one tick, so one drop is one photograph.
    /// </summary>
    [InitializeOnLoad]
    public static class ShotOnDemand
    {
        private const string TriggerPath = "Tools/Screenshots/.shot";

        /// <summary>Bumped on every real edit and logged on load, so a trigger dropped before a
        /// background compile finishes can be told apart from one that reached the new code -
        /// PlayModeScreenshot's own marker exists for the same reason and records what it cost to
        /// learn.</summary>
        private const string BuildMarker = "SHOTV1";

        /// <summary>Frames to wait between noticing the trigger and taking the photograph.
        ///
        /// Not zero, and not a guess. ScreenCapture hands back whatever the Game view LAST
        /// RENDERED, so a capture in the same tick as a Repaint request catches the frame before
        /// the one that was asked for. Two ticks is enough for the repaint to have happened and
        /// short enough that a caller polling for the file does not notice the wait.</summary>
        private const int SettleFrames = 2;

        private static string _pending;
        private static int _countdown;

        static ShotOnDemand()
        {
            Debug.Log("ShotOnDemand: loaded, build marker " + BuildMarker);
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (_pending != null)
            {
                Repaint();
                if (--_countdown > 0) return;

                string name = _pending;
                _pending = null;
                Capture(name);
                return;
            }

            string trigger = Path.Combine(Root, TriggerPath);
            if (!File.Exists(trigger)) return;

            string requested;
            try
            {
                requested = File.ReadAllText(trigger).Trim();
                File.Delete(trigger);
            }
            catch (Exception ex)
            {
                // A half-written trigger file is read again on the next tick rather than treated
                // as a failure: the writer is another process and this can genuinely race it.
                Debug.LogWarning("ShotOnDemand: could not read the trigger (" + ex.Message + ")");
                return;
            }

            if (string.IsNullOrEmpty(requested)) requested = "shot";
            _pending = requested;
            _countdown = SettleFrames;
            Repaint();
        }

        private static string Root
        {
            get { return Path.GetDirectoryName(Application.dataPath); }
        }

        /// <summary>
        /// Asks the Game view to draw. Without this a Game view whose OS window is not focused
        /// never repaints, and ScreenCapture has nothing to hand back - three straight runs of
        /// PlayModeScreenshot produced empty files before that was understood, and its own comment
        /// records the whole list of OS-level focus tricks that did not work. This is the one that
        /// does, because it is in-process and does not need the window to be focused at all.
        /// </summary>
        private static void Repaint()
        {
            Type gameView = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameView == null) return;
            UnityEngine.Object[] views = Resources.FindObjectsOfTypeAll(gameView);
            for (int i = 0; i < views.Length; i++)
            {
                EditorWindow window = views[i] as EditorWindow;
                if (window != null) window.Repaint();
            }
        }

        private static void Capture(string name)
        {
            string dir = Path.Combine(Root, "Tools/Screenshots");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name.EndsWith(".png") ? name : name + ".png");
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("ShotOnDemand: could not clear " + path + " (" + ex.Message + ")");
            }

            Canvas.ForceUpdateCanvases();
            ScreenCapture.CaptureScreenshot(path, 1);
            Debug.Log("ShotOnDemand: requested " + path);
        }
    }
}
#endif
