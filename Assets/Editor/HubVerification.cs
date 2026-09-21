#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Emberfall.Game;
using Emberfall.UI;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// Drives a real Play Mode session to the Hub (MainMenuScreen) and captures a screenshot, for a
    /// before/after comparison of the Cinder theme pass rather than a description of it. Two runs of
    /// the same tool: once against the untouched screen, once after the redesign, into two different
    /// files - the trigger's own text picks which.
    ///
    /// THE SAVE ISOLATION IS THE POINT OF THIS FILE, exactly as it is in ArmorVerification and
    /// AspectVerification, copied from there deliberately rather than reasoned out again. Exiting
    /// Play Mode persists whatever is in memory through GameApp's quit path, and persistentDataPath
    /// is derived from companyName/productName, so any automation that enters Play Mode writes to
    /// the live save unless something changes that identity FIRST and VERIFIES it SECOND.
    ///
    /// Also carries the Play-Mode-guard fix AspectVerification needed after its first live run:
    /// EditorSceneManager.OpenScene throws if the editor is already playing, and if that throw lands
    /// after the identity switch but before the run is armed, nothing is left to restore it. Begin()
    /// refuses to run mid-play (stops it and reschedules itself) and wraps the switch-to-open-scene
    /// sequence in a try/catch that restores the identity on any failure.
    /// </summary>
    [InitializeOnLoad]
    public static class HubVerification
    {
        private const string ActiveKey = "HubVerification.Active";
        private const string FrameKey = "HubVerification.Frame";
        private const string StageKey = "HubVerification.Stage";
        private const string RestoreKey = "HubVerification.Restore";
        private const string OrigCompanyKey = "HubVerification.OrigCompany";
        private const string OrigProductKey = "HubVerification.OrigProduct";
        private const string PendingKey = "HubVerification.Pending";
        private const string OutputNameKey = "HubVerification.OutputName";

        /// <summary>Which of the Hub's layers to open before the shot, and whether to pretend the
        /// device has a notch. Both are parsed out of the trigger file and carried across the
        /// domain reload that entering Play Mode causes, like everything else here.</summary>
        private const string StateNameKey = "HubVerification.State";
        private const string SafeAreaKey = "HubVerification.SafeArea";

        private const string TriggerPath = "Tools/Screenshots/.hub-trigger";

        /// <summary>Deliberately unlike anything the live install uses - this project ships as
        /// DefaultCompany/Game and the older build as Emberfall Studio/Emberfall, and both have a
        /// real save on this machine.</summary>
        private const string ScratchCompany = "Emberfall Scratch";
        private const string ScratchProduct = "Emberfall Hub Verify";

        private const string BuildMarker = "HUBV1-before-after";

        private const int TimeoutFrames = 600;

        static HubVerification()
        {
            Debug.Log("HubVerification: loaded, build marker " + BuildMarker);
            EditorApplication.update += Pump;
            EditorApplication.update += PumpRestore;
            EditorApplication.update += PollTriggerFile;
            EditorApplication.update += PumpPending;
        }

        /// <summary>Trigger file content picks the output name: "before" or "after". Anything else,
        /// including empty, falls back to "after" so an empty drop still produces something rather
        /// than silently doing nothing.</summary>
        private static void PollTriggerFile()
        {
            if (SessionState.GetBool(ActiveKey, false)) return;
            string path = Path.Combine(Application.dataPath, "../" + TriggerPath);
            if (!File.Exists(path)) return;
            string requested = File.ReadAllText(path).Trim().ToLowerInvariant();
            File.Delete(path);

            // "<name>[@<width>x<height>][#<state>][#notch]".
            //
            // The optional size pins the Game view before the run, so a capture is taken at a
            // chosen aspect instead of whatever the editor window was last dragged to. Without it
            // the logical canvas size silently follows the window - see GameViewSize for why that
            // quietly invalidates a layout check.
            //
            // A #state opens one of the Hub's layers before the shot - profile, account, settings,
            // guild, claim, menu - because "nothing overlaps" is only proven for a state that was
            // actually captured, and three of those layers cover most of the screen. #notch
            // simulates a notched phone's safe area, which is the only way to check the inset
            // without owning one.
            string state = "";
            bool notch = false;
            string sizePart = "";

            int at = requested.IndexOf('@');
            if (at > 0)
            {
                sizePart = requested.Substring(at + 1);
                requested = requested.Substring(0, at);
            }

            // The tokens may ride on either half, so both are scanned. The name keeps only what is
            // in front of its first '#', because the name becomes a FILE NAME and a state is not
            // part of one.
            string[] nameTokens = requested.Split('#');
            string[] sizeTokens = sizePart.Split('#');
            requested = nameTokens[0];
            sizePart = sizeTokens[0];
            for (int i = 1; i < nameTokens.Length; i++)
            {
                if (nameTokens[i] == "notch") notch = true;
                else if (nameTokens[i].Length > 0) state = nameTokens[i];
            }
            for (int i = 1; i < sizeTokens.Length; i++)
            {
                if (sizeTokens[i] == "notch") notch = true;
                else if (sizeTokens[i].Length > 0) state = sizeTokens[i];
            }

            SessionState.SetString(StateNameKey, state);
            SessionState.SetString(SafeAreaKey, "");

            if (sizePart.Length > 0)
            {
                string size = sizePart;
                int by = size.IndexOf('x');
                int w, h;
                if (by > 0 && int.TryParse(size.Substring(0, by), out w) && int.TryParse(size.Substring(by + 1), out h))
                {
                    if (!GameViewSize.Apply(w, h))
                    {
                        Debug.LogError("HubVerification: refusing to capture - the Game view could not be set to "
                                     + w + "x" + h + ", so the result would not be at the requested aspect.");
                        return;
                    }

                    if (notch)
                    {
                        // A 44pt cutout on each short edge and a 21pt home indicator, at 3x, which
                        // is what a 2532x1170 phone reports in landscape. Symmetric on purpose: a
                        // device can be held either way up, so a layout that only clears the side
                        // the notch happens to be on today is not cleared at all.
                        SessionState.SetString(SafeAreaKey, "132," + 63 + "," + (w - 264) + "," + (h - 63));
                    }
                }
            }

            string outputName = requested == "before" ? "hub_before" : requested;
            if (outputName != "hub_before" && !outputName.StartsWith("hub_")) outputName = "hub_after";
            Debug.Log("HubVerification: trigger found (build marker " + BuildMarker + "), capturing " + outputName + ".png");
            Begin(outputName);
        }

        /// <summary>
        /// Manual fallback for when the automated trigger file cannot reach this session - the file
        /// poller needs EditorApplication.update to actually be ticking with some regularity, which
        /// is not guaranteed for an editor window that cannot be given OS-level focus (this exact
        /// gap is why PlayModeScreenshot documents two trigger paths rather than one). A person
        /// sitting at the machine can always reach a menu item directly, no focus trick required.
        /// </summary>
        [MenuItem("Emberfall/Debug/Capture Hub Before Screenshot")]
        public static void CaptureBefore() { Begin("hub_before"); }

        [MenuItem("Emberfall/Debug/Capture Hub After Screenshot")]
        public static void CaptureAfter() { Begin("hub_after"); }

        public static void Begin(string outputName)
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.Log("HubVerification: editor is already in Play Mode - stopping it first and"
                        + " retrying once it has exited. OpenScene cannot run during play.");
                SessionState.SetString(OutputNameKey, outputName);
                SessionState.SetBool(PendingKey, true);
                EditorApplication.isPlaying = false;
                return;
            }

            SessionState.SetString(OrigCompanyKey, PlayerSettings.companyName);
            SessionState.SetString(OrigProductKey, PlayerSettings.productName);

            PlayerSettings.companyName = ScratchCompany;
            PlayerSettings.productName = ScratchProduct;
            AssetDatabase.SaveAssets();

            Debug.Log("HubVerification: identity switched from '" + SessionState.GetString(OrigCompanyKey, "?")
                    + "/" + SessionState.GetString(OrigProductKey, "?") + "' to '"
                    + ScratchCompany + "/" + ScratchProduct + "' before entering Play Mode.");

            // The scratch profile is always a NEW account, so the first-run sequence fires on
            // every capture and puts a welcome card over the screen being photographed. Suppressed
            // for the run and cleared again in PumpRestore, next to the identity restore, so a
            // crashed run cannot leave it set.
            EditorPrefs.SetBool(GameApp.SuppressFirstRunKey, true);

            try
            {
                ClearScratchSave();
                string shot = PathFor(outputName + ".png");
                if (File.Exists(shot)) File.Delete(shot);

                EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            }
            catch (System.Exception ex)
            {
                PlayerSettings.companyName = SessionState.GetString(OrigCompanyKey, "");
                PlayerSettings.productName = SessionState.GetString(OrigProductKey, "");
                AssetDatabase.SaveAssets();
                Debug.LogError("HubVerification: aborted during setup and restored the original identity. " + ex.Message);
                return;
            }

            SessionState.SetString(OutputNameKey, outputName);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(FrameKey, 0);
            SessionState.SetInt(StageKey, 0);
            EditorApplication.isPlaying = true;
        }

        private static void PumpPending()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(PendingKey, false);
            Debug.Log("HubVerification: Play Mode has exited, starting the run now.");
            Begin(SessionState.GetString(OutputNameKey, "after"));
        }

        private static void Stop()
        {
            SafeAreaRect.Simulated = new Rect(0f, 0f, 0f, 0f);
            SessionState.SetString(SafeAreaKey, "");
            SessionState.SetString(StateNameKey, "");
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(RestoreKey, true);
            EditorApplication.isPlaying = false;
        }

        private static void PumpRestore()
        {
            if (!SessionState.GetBool(RestoreKey, false)) return;
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;

            string company = SessionState.GetString(OrigCompanyKey, "");
            string product = SessionState.GetString(OrigProductKey, "");
            SessionState.SetBool(RestoreKey, false);
            if (string.IsNullOrEmpty(product)) return;

            PlayerSettings.companyName = company;
            PlayerSettings.productName = product;
            AssetDatabase.SaveAssets();
            EditorPrefs.SetBool(GameApp.SuppressFirstRunKey, false);
            Debug.Log("HubVerification: identity restored to '" + company + "/" + product + "'.");
        }

        private static string PathFor(string fileName)
        {
            string path = Path.Combine(Application.dataPath, "../Tools/Screenshots/" + fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            return path;
        }

        /// <summary>Same guard shape as ArmorVerification/AspectVerification's ClearScratchSave, and
        /// for the same reason: the path deleted must contain ScratchProduct, so a later edit cannot
        /// turn this into something that deletes a real save.</summary>
        private static void ClearScratchSave()
        {
            string dataPath = Application.persistentDataPath;
            if (dataPath.IndexOf(ScratchProduct, System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                Debug.LogError("HubVerification: refusing to clear a scratch save - persistentDataPath is '"
                    + dataPath + "', which is not the scratch path.");
                return;
            }

            string savePath = Path.Combine(dataPath, "emberfall_save.json");
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
                Debug.Log("HubVerification: deleted a leftover scratch save at " + savePath);
            }
            if (PlayerPrefs.HasKey(SaveService.PrefsKey))
            {
                PlayerPrefs.DeleteKey(SaveService.PrefsKey);
                PlayerPrefs.Save();
                Debug.Log("HubVerification: deleted a leftover scratch save mirrored in PlayerPrefs.");
            }
        }

        /// <summary>
        /// Pretends the device has a notch, for the one capture that has to prove the safe-area
        /// inset does something. Nothing but this tool ever writes SafeAreaRect.Simulated, it is
        /// compiled out of any build, and Stop clears it - so a simulated notch cannot leak into
        /// an ordinary editor session, let alone a player.
        /// </summary>
        private static void ApplySimulatedSafeArea()
        {
            string packed = SessionState.GetString(SafeAreaKey, "");
            if (string.IsNullOrEmpty(packed)) { SafeAreaRect.Simulated = new Rect(0f, 0f, 0f, 0f); return; }

            string[] parts = packed.Split(',');
            float x, y, w, h;
            if (parts.Length != 4
                || !float.TryParse(parts[0], out x) || !float.TryParse(parts[1], out y)
                || !float.TryParse(parts[2], out w) || !float.TryParse(parts[3], out h))
            {
                Debug.LogError("HubVerification: could not read the simulated safe area '" + packed
                             + "', so the capture would silently be an ordinary one. Not applied.");
                return;
            }

            SafeAreaRect.Simulated = new Rect(x, y, w, h);
            Debug.Log("HubVerification: simulating a safe area of " + SafeAreaRect.Simulated
                    + " against a " + Screen.width + "x" + Screen.height + " screen.");
        }

        /// <summary>Opens the Hub layer the trigger asked for, if it asked for one.</summary>
        private static void OpenRequestedLayer()
        {
            string state = SessionState.GetString(StateNameKey, "");
            if (string.IsNullOrEmpty(state)) return;

            GameApp app = GameApp.Instance;
            if (app == null || app.Router == null || app.Router.MainMenu == null) return;

            app.Router.MainMenu.OpenLayerForCapture(state);
            Debug.Log("HubVerification: opened the '" + state + "' layer for this capture.");
        }

        private static void ForceGameViewRepaint()
        {
            System.Type gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null) return;
            EditorWindow window = EditorWindow.GetWindow(gameViewType, false, null, false);
            if (window == null) return;
            window.Focus();
            window.Repaint();
        }

        private static void Pump()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (!EditorApplication.isPlaying) return;

            ForceGameViewRepaint();

            int frame = SessionState.GetInt(FrameKey, 0) + 1;
            SessionState.SetInt(FrameKey, frame);
            int stage = SessionState.GetInt(StageKey, 0);
            string outputName = SessionState.GetString(OutputNameKey, "after");

            if (stage == 0 && frame >= 10)
            {
                string dataPath = Application.persistentDataPath;
                if (dataPath.IndexOf(ScratchProduct, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    Debug.LogError("HubVerification: ABORTING - persistentDataPath is '" + dataPath
                        + "', which is not the scratch path. Refusing to run rather than risk the live save.");
                    Stop();
                    return;
                }
                Debug.Log("HubVerification: save path isolated at " + dataPath);

                if (GameApp.Instance == null) { Debug.LogError("HubVerification: GameApp.Instance never appeared."); Stop(); return; }

                // A fresh profile is already showing MainMenu with its 3 starting heroes deployed -
                // no extra population needed. Shown explicitly anyway rather than relying on the
                // boot default, so this tool keeps working if that default ever changes.
                GameApp.Instance.Router.Show(ScreenId.MainMenu);
                Debug.Log("HubVerification: on the Hub screen.");

                ApplySimulatedSafeArea();

                SessionState.SetInt(StageKey, 1);
                stage = 1;
            }

            // The layer opens a few frames before the shot rather than in the same frame: a panel
            // reparents a screen into itself and that screen's OnShow may start a coroutine, so a
            // capture taken immediately would photograph a half-attached sheet.
            if (stage == 1 && frame == 30) OpenRequestedLayer();

            if (stage == 1 && frame == 40) Canvas.ForceUpdateCanvases();

            string shotPath = PathFor(outputName + ".png");
            if (stage == 1 && frame == 50)
            {
                ScreenCapture.CaptureScreenshot(shotPath, 1);
                Debug.Log("HubVerification: requested " + outputName + ".png");
                SessionState.SetInt(StageKey, 2);
            }

            if (stage == 2)
            {
                if (File.Exists(shotPath) || frame >= TimeoutFrames)
                {
                    Debug.Log("HubVerification: done. exists=" + File.Exists(shotPath) + " after " + frame + " frames.");
                    Stop();
                }
            }
        }
    }
}
#endif
