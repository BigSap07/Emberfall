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
        private const string PendingKey = "AspectVerification.Pending";

        private const string TriggerPath = "Tools/Screenshots/.aspect-trigger";

        /// <summary>Deliberately unlike anything the live install uses - this project ships as
        /// DefaultCompany/Game and the older build as Emberfall Studio/Emberfall, and both have a
        /// real save on this machine.</summary>
        private const string ScratchCompany = "Emberfall Scratch";
        private const string ScratchProduct = "Emberfall Aspect Verify";

        private const string BuildMarker = "ASPECTV7-ch1-8-lvl6";

        /// <summary>How long to let the battle play before giving up. A stage this lopsided resolves
        /// well inside it; the cap exists so a hang reports a failure instead of idling forever.</summary>
        private const int TimeoutFrames = 40000;

        static AspectVerification()
        {
            Debug.Log("AspectVerification: loaded, build marker " + BuildMarker);
            EditorApplication.update += Pump;
            EditorApplication.update += PumpRestore;
            EditorApplication.update += PollTriggerFile;
            EditorApplication.update += PumpPending;
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

        /// <summary>
        /// Refuses to start while the editor is already in Play Mode, and this is not a nicety.
        ///
        /// EditorSceneManager.OpenScene throws InvalidOperationException during Play Mode. The first
        /// live run of this tool hit exactly that: the editor happened to already be playing, Begin
        /// threw at the OpenScene line, and because the throw landed AFTER the identity switch but
        /// BEFORE ActiveKey was set, the run left the editor pointed at the scratch identity with
        /// nothing armed to put it back - Stop() is the only thing that sets RestoreKey, and Stop()
        /// was never reached. An abort path that leaves the project half-switched is worse than no
        /// abort path, because the next person to press Play writes their session somewhere else.
        ///
        /// Two changes rather than one. The identity switch is now wrapped so that ANY failure
        /// between it and a successful arm restores it on the way out, and the whole body refuses to
        /// run in Play Mode in the first place by requesting a stop and rescheduling itself. Either
        /// alone would have prevented this; both, because the first is a general guarantee and the
        /// second removes the specific known cause.
        /// </summary>
        [MenuItem("Emberfall/Debug/Verify Aspect Identities", false, 23)]
        public static void Begin()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.Log("AspectVerification: editor is already in Play Mode - stopping it first and"
                        + " retrying once it has exited. OpenScene cannot run during play.");
                SessionState.SetBool(PendingKey, true);
                EditorApplication.isPlaying = false;
                return;
            }

            SessionState.SetString(OrigCompanyKey, PlayerSettings.companyName);
            SessionState.SetString(OrigProductKey, PlayerSettings.productName);

            PlayerSettings.companyName = ScratchCompany;
            PlayerSettings.productName = ScratchProduct;
            AssetDatabase.SaveAssets();

            Debug.Log("AspectVerification: identity switched from '" + SessionState.GetString(OrigCompanyKey, "?")
                    + "/" + SessionState.GetString(OrigProductKey, "?") + "' to '"
                    + ScratchCompany + "/" + ScratchProduct + "' before entering Play Mode.");

            try
            {
                ClearScratchSave();
                string shot = PathFor("aspect_ember_stack.png");
                if (File.Exists(shot)) File.Delete(shot);

                EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            }
            catch (System.Exception ex)
            {
                // Put the identity back before rethrowing, so a failure here can never leave the
                // project pointed at the scratch save the way the first live run did.
                PlayerSettings.companyName = SessionState.GetString(OrigCompanyKey, "");
                PlayerSettings.productName = SessionState.GetString(OrigProductKey, "");
                AssetDatabase.SaveAssets();
                Debug.LogError("AspectVerification: aborted during setup and restored the original identity. "
                             + ex.Message);
                return;
            }

            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(FrameKey, 0);
            SessionState.SetInt(StageKey, 0);
            SessionState.SetBool(SawStackKey, false);
            SessionState.SetBool(SawSpreadKey, false);
            SessionState.SetString(StackTextKey, "");
            EditorCaptureGuard.EnterPlayMode();
        }

        /// <summary>Restarts a run that had to stop Play Mode first, once it has actually exited.</summary>
        private static void PumpPending()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(PendingKey, false);
            Debug.Log("AspectVerification: Play Mode has exited, starting the run now.");
            Begin();
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
            // The default three starters at level 6, which is not a guess - it is the configuration
            // a separate sweep of every stage, seven levels and eight seeds identified as the one a
            // real player actually reaches an Ember stack in. Deliberately NOT a summoned five-hero
            // line: adding heroes changes the damage race that produces the stack in the first place,
            // and the whole point of this run is to catch the tag on a line the game hands out.
            PlayerProfile profile = app.Session.Profile;
            List<HeroInstance> team = profile.TeamHeroes();
            for (int i = 0; i < team.Count; i++) team[i].Level = 6;
            Debug.Log("AspectVerification: line is the " + team.Count + " default starters at level 6.");
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

                // ch1_8 at level 6, and both halves of that are measured rather than picked.
                // Three earlier runs of this tool failed on the arena alone, and what they turned up
                // is a content fact worth recording rather than a tooling one.
                //
                // AN EMBER STACK IS UNREACHABLE FROM THE PLAYER SIDE. Kalyx is the only hero in the
                // game who applies Burn at all - Orrin was relabelled to Lumen and Kael's Breaking
                // Point carries no status - so a stack cannot come from two Ember heroes the way
                // DESIGN-ASPECTS section 1 pictures it. It would need Kalyx to land Cinder Bloom
                // twice over, and its Burn runs 3 rounds against a 100-energy cast he can afford
                // about every fourth, so the first entry expires before the second can join it.
                //
                // Stacking is an ENEMY-side event: cinder_imp and ashen_revenant are both Ember and
                // both throw an AllEnemies Burn, so a hero standing in front of the pair carries two
                // independent entries at once - which is the case the status row could not draw
                // before this pass, and it happens on the PLAYER's own bar. A sweep of every stage
                // at seven levels and eight seeds put the cleanest instance of that on ch1_8 at
                // level 6: depth 2 on a player unit in seven seeds out of eight, and still a win, so
                // the battle does not end in a defeat wipe before the tag can be caught.
                StageDef arena = app.Session.Content.GetStage("ch1_8");
                if (arena == null) { Debug.LogError("AspectVerification: stage ch1_8 not found."); Stop(); return; }

                app.Router.Battle.SetStage(arena);
                app.Router.Show(ScreenId.Battle);
                Debug.Log("AspectVerification: launched ch1_8 through the real CampaignScreen path.");
                SessionState.SetInt(StageKey, 1);
                return;
            }

            if (stage != 1) return;

            // Drive the battle to 4x through its own Speed button. The first three runs of this tool
            // all timed out mid-round-one: playback is animated in real time and an editor update
            // tick is not a game frame, so 6000 ticks bought about one round. A Burn stack needs
            // Kalyx to cast Cinder Bloom twice, which is several rounds away at 1x.
            //
            // Clicked rather than set. _speed and CycleSpeed are private, and the rule this file
            // works to is that production code does not grow an API so a verification script can
            // reach into it - invoking the button's own onClick is the same path a player takes.
            if (frame == 120 || frame == 150)
            {
                Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < buttons.Length; i++)
                {
                    Button b = buttons[i];
                    if (b == null || !b.gameObject.activeInHierarchy) continue;
                    Text label = b.GetComponentInChildren<Text>(true);
                    if (label == null) continue;
                    if (label.text != "1x" && label.text != "2x") continue;
                    b.onClick.Invoke();
                    Debug.Log("AspectVerification: clicked the Speed button (was " + label.text + ").");
                    break;
                }
            }

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

            // Diagnostic, not decoration. Two runs found neither rule and there is no way to tell
            // "the battle played and never stacked" from "the battle never started" without looking
            // at what is actually on screen. Short labels only - the status row, the tags, the
            // floats - so this stays readable rather than dumping every string in the UI.
            if (frame % 2000 == 0)
            {
                Text[] all = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                string sample = "";
                int shown = 0;
                for (int i = 0; i < all.Length && shown < 14; i++)
                {
                    Text label = all[i];
                    if (label == null || !label.gameObject.activeInHierarchy) continue;
                    string t = label.text;
                    if (string.IsNullOrEmpty(t) || t.Length > 24) continue;
                    sample += "[" + t + "] ";
                    shown++;
                }
                Debug.Log("AspectVerification: frame " + frame + " texts=" + all.Length + " onscreen: " + sample);
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
