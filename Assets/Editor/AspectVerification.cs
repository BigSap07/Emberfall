#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Game;
using Emberfall.UI;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// Proves the two things this aspect pass actually changed that a harness run cannot reach, in a
    /// real Play Mode battle driven through the real router:
    ///
    ///   1. **The Ember stack count reaches the screen.** DESIGN-ASPECTS section 6 calls this the one
    ///      thing in the project the status row genuinely could not show. The harness proves the
    ///      simulation emits the depth; only Play Mode proves BattleScreen renders it, because
    ///      RefreshStatusText and SyncStatusCount live on the UI side of the event list.
    ///   2. **The Umbra spread fires in a real battle**, not only in a synthetic harness arena.
    ///
    /// Neither is screenshot-timed. Both are polled every frame across the whole playback and
    /// latched, because the moment a second Burn is up is a few frames wide and a fixed-frame
    /// capture would be a coin toss reported as a result. The screenshot is taken at the moment the
    /// stacked tag is actually on screen, so the image is evidence rather than illustration.
    ///
    /// THE SAVE ISOLATION IS THE POINT OF THIS FILE, exactly as it is in ArmorVerification, and it
    /// is copied from there deliberately rather than being reasoned out again. Exiting Play Mode
    /// persists whatever is in memory through GameApp's quit path, and persistentDataPath is derived
    /// from companyName/productName, so any automation that enters Play Mode writes to the live save
    /// unless something changes that identity FIRST and VERIFIES it SECOND. Begin() sets a scratch
    /// identity before isPlaying is ever set; Pump() refuses to touch the profile at all unless the
    /// resolved path actually contains that scratch product name.
    /// </summary>
    [InitializeOnLoad]
    public static class AspectVerification
    {
        private const string ActiveKey = "AspectVerification.Active";
        private const string FrameKey = "AspectVerification.Frame";
        private const string StageKey = "AspectVerification.Stage";
        private const string RestoreKey = "AspectVerification.Restore";
        private const string OrigCompanyKey = "AspectVerification.OrigCompany";
        private const string OrigProductKey = "AspectVerification.OrigProduct";
        private const string SawStackKey = "AspectVerification.SawStack";
        private const string SawSpreadKey = "AspectVerification.SawSpread";
        private const string StackTextKey = "AspectVerification.StackText";

        private const string TriggerPath = "Tools/Screenshots/.aspect-trigger";

        /// <summary>Deliberately unlike anything the live install uses - this project ships as
        /// DefaultCompany/Game and the older build as Emberfall Studio/Emberfall, and both have a
        /// real save on this machine.</summary>
        private const string ScratchCompany = "Emberfall Scratch";
        private const string ScratchProduct = "Emberfall Aspect Verify";

        private const string BuildMarker = "ASPECTV1-ember-stack-umbra-spread";

        /// <summary>How long to let the battle play before giving up. A stage this lopsided resolves
        /// well inside it; the cap exists so a hang reports a failure instead of idling forever.</summary>
        private const int TimeoutFrames = 1400;

        static AspectVerification()
        {
            Debug.Log("AspectVerification: loaded, build marker " + BuildMarker);
            EditorApplication.update += Pump;
            EditorApplication.update += PumpRestore;
            EditorApplication.update += PollTriggerFile;
        }

        private static void PollTriggerFile()
        {
            if (SessionState.GetBool(ActiveKey, false)) return;
            string path = Path.Combine(Application.dataPath, "../" + TriggerPath);
            if (!File.Exists(path)) return;
            File.Delete(path);
            Debug.Log("AspectVerification: trigger found (build marker " + BuildMarker + "), starting.");
            Begin();
        }

        [MenuItem("Emberfall/Debug/Verify Aspect Identities", false, 23)]
        public static void Begin()
        {
            SessionState.SetString(OrigCompanyKey, PlayerSettings.companyName);
            SessionState.SetString(OrigProductKey, PlayerSettings.productName);

            PlayerSettings.companyName = ScratchCompany;
            PlayerSettings.productName = ScratchProduct;
            AssetDatabase.SaveAssets();

            Debug.Log("AspectVerification: identity switched from '" + SessionState.GetString(OrigCompanyKey, "?")
                    + "/" + SessionState.GetString(OrigProductKey, "?") + "' to '"
                    + ScratchCompany + "/" + ScratchProduct + "' before entering Play Mode.");

            ClearScratchSave();
            string shot = PathFor("aspect_ember_stack.png");
            if (File.Exists(shot)) File.Delete(shot);

            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(FrameKey, 0);
            SessionState.SetInt(StageKey, 0);
            SessionState.SetBool(SawStackKey, false);
            SessionState.SetBool(SawSpreadKey, false);
            SessionState.SetString(StackTextKey, "");
            EditorApplication.isPlaying = true;
        }

        private static void Stop()
        {
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
            Debug.Log("AspectVerification: identity restored to '" + company + "/" + product + "'.");
        }

        private static string PathFor(string fileName)
        {
            string path = Path.Combine(Application.dataPath, "../Tools/Screenshots/" + fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            return path;
        }

        /// <summary>
        /// Same guard shape as ArmorVerification.ClearScratchSave, and for the same reason: the path
        /// deleted must contain ScratchProduct, so a later edit cannot turn this into something that
        /// deletes a real save. GameApp.Awake() calls SaveService.Load() unconditionally, so a
        /// leftover scratch save from an earlier run would otherwise be loaded straight back in.
        /// </summary>
        private static void ClearScratchSave()
        {
            string dataPath = Application.persistentDataPath;
            if (dataPath.IndexOf(ScratchProduct, System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                Debug.LogError("AspectVerification: refusing to clear a scratch save - persistentDataPath is '"
                    + dataPath + "', which is not the scratch path.");
                return;
            }

            string savePath = Path.Combine(dataPath, "emberfall_save.json");
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
                Debug.Log("AspectVerification: deleted a leftover scratch save at " + savePath);
            }
            if (PlayerPrefs.HasKey(SaveService.PrefsKey))
            {
                PlayerPrefs.DeleteKey(SaveService.PrefsKey);
                PlayerPrefs.Save();
                Debug.Log("AspectVerification: deleted a leftover scratch save mirrored in PlayerPrefs.");
            }
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

        /// <summary>
        /// The line is chosen for what it makes happen, not for what it can beat: two Ember heroes
        /// so two independent Burns can sit on one enemy at once (one Ember can only ever refresh
        /// its own entry, so a single-Ember line cannot produce the thing being verified), plus Nyx
        /// so an Umbra-sourced mark is on the board for the spread to move. Levelled well past the
        /// stage so the fight resolves inside the frame budget and enemies actually die, which is
        /// what a spread needs.
        /// </summary>
        private static void PopulateLine(GameApp app)
        {
            string[] wanted = { "kalyx_emberwright", "kael_the_ashbound", "nyx_serrow", "ryn_solvarr", "orrin_kaal" };
            PlayerProfile profile = app.Session.Profile;

            for (int i = 0; i < wanted.Length; i++)
            {
                HeroDef def = app.Session.Content.GetHero(wanted[i]);
                if (def == null) { Debug.LogWarning("AspectVerification: no hero '" + wanted[i] + "'"); continue; }

                HeroInstance owned = profile.FindHeroByDefId(wanted[i]);
                if (owned == null)
                {
                    owned = app.Session.GrantHeroDirect(def, TimeUtil.NowUnix());
                    if (owned == null) { Debug.LogWarning("AspectVerification: could not grant " + wanted[i]); continue; }
                }
                owned.Level = 30;
                if (i < profile.Team.Length) profile.AssignToTeam(i, owned.Uid);
            }
            Debug.Log("AspectVerification: line set to " + string.Join(", ", wanted));
        }

        /// <summary>
        /// Reads the live status rows off the battle screen by scanning Text components for a tag
        /// that carries a stack suffix. Deliberately not an accessor added to BattleScreen:
        /// production code should not grow an API purely so a verification script can read a label.
        ///
        /// Matches on the rendered string rather than on any internal count, because the rendered
        /// string is the thing the player sees and therefore the thing being verified.
        /// </summary>
        private static string FindTextContaining(string needle)
        {
            Text[] texts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < texts.Length; i++)
            {
                Text label = texts[i];
                if (label == null || !label.gameObject.activeInHierarchy) continue;
                string t = label.text;
                if (!string.IsNullOrEmpty(t) && t.Contains(needle)) return t;
            }
            return null;
        }

        private static void Pump()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (!EditorApplication.isPlaying) return;

            ForceGameViewRepaint();

            int frame = SessionState.GetInt(FrameKey, 0) + 1;
            SessionState.SetInt(FrameKey, frame);
            int stage = SessionState.GetInt(StageKey, 0);

            if (stage == 0 && frame >= 10)
            {
                // THE GUARD. Checked against the actual resolved path rather than trusting that the
                // PlayerSettings write took effect, and it runs before the profile is touched, so
                // there is nothing in memory for the quit path to persist if it aborts.
                string dataPath = Application.persistentDataPath;
                if (dataPath.IndexOf(ScratchProduct, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    Debug.LogError("AspectVerification: ABORTING - persistentDataPath is '" + dataPath
                        + "', which is not the scratch path. Refusing to run rather than risk the live save.");
                    Stop();
                    return;
                }
                Debug.Log("AspectVerification: save path isolated at " + dataPath);

                GameApp app = GameApp.Instance;
                if (app == null) { Debug.LogError("AspectVerification: GameApp.Instance never appeared."); Stop(); return; }

                PopulateLine(app);

                // A late chapter-1 stage: five enemies, so there is somebody for a mark to jump to
                // when one of them dies. A three-enemy stage would run the spread's own guard rather
                // than the spread.
                StageDef stage10 = app.Session.Content.GetStage("ch1_10");
                if (stage10 == null) { Debug.LogError("AspectVerification: stage ch1_10 not found."); Stop(); return; }

                app.Router.Battle.SetStage(stage10);
                app.Router.Show(ScreenId.Battle);
                Debug.Log("AspectVerification: launched ch1_10 through the real CampaignScreen path.");
                SessionState.SetInt(StageKey, 1);
                return;
            }

            if (stage != 1) return;

            // ---- polled every frame, latched. Both of these are a few frames wide.
            if (!SessionState.GetBool(SawStackKey, false))
            {
                string tag = FindTextContaining("BRNx");
                if (!string.IsNullOrEmpty(tag))
                {
                    SessionState.SetBool(SawStackKey, true);
                    SessionState.SetString(StackTextKey, tag);
                    ScreenCapture.CaptureScreenshot(PathFor("aspect_ember_stack.png"), 1);
                    Debug.Log("AspectVerification: EMBER STACK VISIBLE on the status row - \"" + tag
                            + "\" (frame " + frame + "). Screenshot requested.");
                }
            }

            if (!SessionState.GetBool(SawSpreadKey, false))
            {
                string spread = FindTextContaining(" spreads");
                if (!string.IsNullOrEmpty(spread))
                {
                    SessionState.SetBool(SawSpreadKey, true);
                    Debug.Log("AspectVerification: UMBRA SPREAD visible in a real battle - \"" + spread
                            + "\" (frame " + frame + ").");
                }
            }

            bool sawStack = SessionState.GetBool(SawStackKey, false);
            bool sawSpread = SessionState.GetBool(SawSpreadKey, false);

            if ((sawStack && sawSpread) || frame > TimeoutFrames)
            {
                Debug.Log("AspectVerification: RESULT emberStackRendered=" + sawStack
                        + " (\"" + SessionState.GetString(StackTextKey, "") + "\")"
                        + " umbraSpreadFired=" + sawSpread
                        + " after " + frame + " frames.");
                if (!sawStack)
                    Debug.LogError("AspectVerification: FAILED - no stacked Burn tag ever reached the status row.");
                if (!sawSpread)
                    Debug.LogWarning("AspectVerification: no Umbra spread fired in this battle. Not necessarily a defect -"
                        + " it needs a marked enemy to die while an ally still stands - but it means this run"
                        + " did not exercise it and the harness arena is the only evidence.");
                Stop();
            }
        }
    }
}
#endif
