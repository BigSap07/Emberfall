#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Game;
using Emberfall.UI;
using Emberfall.Core.Session;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// CAPTURE THE DEV ARENA AT A GIVEN RESOLUTION.
    ///
    /// A layout that "fits" is a claim about pixels at a size, so checking it needs the Game view
    /// actually rendering at that size rather than at whatever the editor window happens to be.
    /// Unity's fixed-resolution list is the only thing that does that, and it is internal, so this
    /// reflects into it - the same trick PlayModeScreenshot already uses to repaint the Game view.
    ///
    /// Kept in the repo rather than driven ad hoc from outside, because "does the arena fit 20:9"
    /// is a question that will be asked again every time the layout moves.
    /// </summary>
    public static class DevArenaShots
    {
        public const string OutputDir = "Tools/DevArenaShots";

        /// <summary>
        /// Adds a fixed resolution to the Game view's list if it is not already there, and selects
        /// it. The size takes effect on the next repaint, so a capture has to wait a frame.
        /// </summary>
        public static void SetSize(int width, int height)
        {
            Assembly asm = typeof(EditorWindow).Assembly;
            Type sizesType = asm.GetType("UnityEditor.GameViewSizes");
            Type singleton = asm.GetType("UnityEditor.ScriptableSingleton`1").MakeGenericType(sizesType);
            object instance = singleton.GetProperty("instance").GetValue(null, null);
            object group = sizesType.GetProperty("currentGroup").GetValue(instance, null);

            Type sizeType = asm.GetType("UnityEditor.GameViewSize");
            Type sizeKind = asm.GetType("UnityEditor.GameViewSizeType");
            string label = "DEVARENA " + width + "x" + height;

            MethodInfo total = group.GetType().GetMethod("GetTotalCount");
            MethodInfo at = group.GetType().GetMethod("GetGameViewSize");
            PropertyInfo baseText = sizeType.GetProperty("baseText");

            int index = -1;
            int count = (int)total.Invoke(group, null);
            for (int i = 0; i < count; i++)
            {
                object size = at.Invoke(group, new object[] { i });
                if ((string)baseText.GetValue(size, null) == label) { index = i; break; }
            }
            if (index < 0)
            {
                ConstructorInfo ctor = sizeType.GetConstructor(
                    new[] { sizeKind, typeof(int), typeof(int), typeof(string) });
                object made = ctor.Invoke(new object[]
                    { Enum.Parse(sizeKind, "FixedResolution"), width, height, label });
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { made });
                index = (int)total.Invoke(group, null) - 1;
            }

            Type gameView = asm.GetType("UnityEditor.GameView");
            EditorWindow view = EditorWindow.GetWindow(gameView, false, "Game", true);
            PropertyInfo selected = gameView.GetProperty("selectedSizeIndex",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (selected != null) selected.SetValue(view, index, null);
            view.Show();
            view.Focus();
            view.Repaint();
        }

        /// <summary>Writes one PNG. The file appears a frame or two later - the caller waits.</summary>
        public static void Shot(string fileName)
        {
            Directory.CreateDirectory(OutputDir);
            ForceGameViewRepaint();
            ScreenCapture.CaptureScreenshot(Path.Combine(OutputDir, fileName), 1);
        }


        // ============================================================== the step runner
        //
        // A capture needs frames between its steps - a resolution change lands on the next
        // repaint, and ScreenCapture hands back the frame the view last drew - so the sequence is
        // a queue pumped by EditorApplication.update rather than a straight-line method.

        private sealed class Step
        {
            public Action Do;
            public string Shot;
            public int Wait;
        }

        private static readonly List<Step> _queue = new List<Step>();
        private static int _wait;
        private static bool _running;

        public static bool Running { get { return _running; } }
        public static int Remaining { get { return _queue.Count; } }

        private static void Queue(Action act, string shot, int wait)
        {
            Step s = new Step();
            s.Do = act;
            s.Shot = shot;
            s.Wait = wait;
            _queue.Add(s);
        }

        private static void Start()
        {
            if (_running) return;
            _running = true;
            _wait = 0;
            EditorApplication.update += Pump;
        }

        private static void Pump()
        {
            // The editor only runs the player loop when the Game view actually repaints, and with
            // nothing on screen changing it will happily sit at the same frame for seconds. Asking
            // for a repaint on every tick is what makes the fight run while a capture is in
            // progress - without it the arena is frozen on frame one and every shot is identical.
            Application.runInBackground = true;
            ForceGameViewRepaint();
            if (_wait > 0) { _wait--; return; }
            if (_queue.Count == 0)
            {
                EditorApplication.update -= Pump;
                _running = false;
                Debug.Log("[dev-arena-shots] done");
                return;
            }
            Step s = _queue[0];
            _queue.RemoveAt(0);
            try
            {
                if (s.Do != null) s.Do();
                if (!string.IsNullOrEmpty(s.Shot)) Shot(s.Shot);
            }
            catch (Exception e)
            {
                Debug.LogError("[dev-arena-shots] step failed: " + e.Message);
            }
            _wait = s.Wait;
        }

        /// <summary>
        /// A strip of shots at a fixed spacing. Catching a clip mid-swing is a matter of luck
        /// against a queue whose ticks are editor updates rather than game frames, so the honest
        /// way to catch it is to take a strip and keep the frames that show it.
        /// </summary>
        public static void Burst(string prefix, int count, int gap)
        {
            _queue.Clear();
            for (int i = 0; i < count; i++)
                Queue(null, prefix + "-" + (i < 10 ? "0" : "") + i + ".png", gap);
            Start();
        }

        /// <summary>Presses a button in the running arena by its GameObject name.</summary>
        public static bool Press(string name)
        {
            foreach (Button b in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (b.name == name) { b.onClick.Invoke(); return true; }
            return false;
        }

        /// <summary>Presses a unit's tap target, e.g. Tap(0) for the unit with id 0.</summary>
        public static bool Tap(int unitId)
        {
            foreach (Button b in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (b.name == "Tap" && b.transform.parent != null
                    && b.transform.parent.name == "Unit_" + unitId)
                { b.onClick.Invoke(); return true; }
            return false;
        }

        private static void CloseDrawers()
        {
            if (!Press("SetupClose")) { }
            if (!Press("RightClose")) { }
        }

        /// <summary>
        /// Every layout shot the branch has to justify: four screen sizes, drawers closed and the
        /// Setup drawer open at each, plus the Unit drawer and the Animation tab at 1600x900.
        /// </summary>
        [MenuItem("Emberfall/Dev Arena Shots/Capture the layout matrix", false, 40)]
        public static void CaptureMatrix()
        {
            _queue.Clear();
            int[,] sizes = { { 1600, 900 }, { 1920, 1080 }, { 1280, 720 }, { 2400, 1080 } };
            for (int i = 0; i < sizes.GetLength(0); i++)
            {
                int w = sizes[i, 0], h = sizes[i, 1];
                string tag = w + "x" + h;
                Queue(delegate { SetSize(w, h); CloseDrawers(); }, null, 12);
                Queue(null, tag + "-default.png", 8);
                Queue(delegate { Press("SetupEdge"); }, null, 8);
                Queue(null, tag + "-setup.png", 8);
                Queue(delegate { Press("SetupClose"); }, null, 6);

                if (w == 1600)
                {
                    Queue(delegate { Press("UnitEdge"); Press("UnitTab"); }, null, 8);
                    Queue(null, tag + "-unit.png", 8);
                    Queue(delegate { Press("AnimTab"); }, null, 8);
                    Queue(null, tag + "-animation.png", 8);
                    Queue(delegate { Press("RightClose"); }, null, 6);
                }
            }
            Start();
            Debug.Log("[dev-arena-shots] queued " + _queue.Count + " steps");
        }

        /// <summary>Kael basic on Marrow, caught two frames in while the clip is still swinging.</summary>
        [MenuItem("Emberfall/Dev Arena Shots/Capture a manual action mid-animation", false, 41)]
        public static void CaptureManualAction()
        {
            _queue.Clear();
            Queue(delegate { SetSize(1600, 900); CloseDrawers(); }, null, 10);
            Queue(delegate { Tap(0); Press("Basic"); Tap(4); }, null, 6);
            Queue(null, "1600x900-manual-midaction.png", 6);
            Queue(null, "1600x900-manual-midaction-2.png", 6);
            Queue(null, "1600x900-manual-midaction-3.png", 6);
            Start();
        }


        /// <summary>The nth button with this name, for lists where every row names its parts the
        /// same way (the board rows, the roster).</summary>
        public static bool PressNth(string name, int index)
        {
            // Hierarchy order, not instance-id order: the board rows are siblings under BoardRows
            // and "the second ally slot" means the second row, which is a statement about the
            // scene tree rather than about allocation order.
            Transform host = null;
            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name == "BoardRows") { host = t; break; }
            if (host == null) return false;

            int seen = 0;
            for (int i = 0; i < host.childCount; i++)
            {
                Transform row = host.GetChild(i);
                Button found = null;
                for (int c = 0; c < row.childCount; c++)
                {
                    Button b = row.GetChild(c).GetComponent<Button>();
                    if (b != null && b.name == name) { found = b; break; }
                }
                if (found == null) continue;
                if (seen == index) { found.onClick.Invoke(); return true; }
                seen++;
            }
            return false;
        }

        public static bool Search(string text)
        {
            foreach (InputField f in UnityEngine.Object.FindObjectsByType<InputField>(FindObjectsSortMode.None))
                if (f.name == "RosterSearch")
                {
                    f.text = text;
                    f.onValueChanged.Invoke(text);
                    return true;
                }
            return false;
        }

        /// <summary>Dumps what is on screen, so a claim about a fight can be checked against the
        /// numbers rather than against a description of the picture.</summary>
        public static void Report(string tag)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[dev-arena-shots] ").Append(tag).Append("\n");
            foreach (Text t in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
            {
                if (t.name == "Round") sb.Append("  bar: ").Append(t.text).Append("\n");
                if (t.name == "ToastText" && t.text.Length > 0) sb.Append("  toast: ").Append(t.text).Append("\n");
                if (t.name == "UnitStats" && t.text.Length > 0)
                    sb.Append("  unit stats: ").Append(t.text.Replace("\n", " | ")).Append("\n");
                if (t.name == "UnitTitle" && t.text.Length > 0) sb.Append("  unit: ").Append(t.text).Append("\n");
            }
            foreach (Transform tr in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (tr.name != "LogContent") continue;
                int from = Mathf.Max(0, tr.childCount - 14);
                for (int i = from; i < tr.childCount; i++)
                {
                    Text lt = tr.GetChild(i).GetComponent<Text>();
                    if (lt != null) sb.Append("  log: ").Append(lt.text).Append("\n");
                }
            }
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// The hand-driven sequence: Kael's basic on Marrow, Hale's active, end the round, drop
        /// Marrow to a tenth, then Kiva's basic on Marrow. Shots at every step, and the numbers
        /// logged so "did that kill him" has an answer with figures behind it.
        /// </summary>
        [MenuItem("Emberfall/Dev Arena Shots/Capture the manual sequence", false, 42)]
        public static void CaptureManualSequence()
        {
            _queue.Clear();
            Queue(delegate { SetSize(1600, 900); CloseDrawers(); }, null, 10);

            Queue(delegate { Tap(0); Press("Basic"); Tap(4); }, null, 3);
            Queue(null, "seq-1a-kael-basic-midaction.png", 3);
            Queue(null, "seq-1b-kael-basic-impact.png", 14);
            Queue(delegate { Report("after Kael BASIC -> Marrow"); }, "seq-1c-kael-basic-done.png", 6);

            Queue(delegate { Tap(3); Press("Active"); Tap(0); }, null, 6);
            Queue(null, "seq-2a-hale-active.png", 16);
            Queue(delegate { Report("after Hale ACTIVE -> Kael"); }, "seq-2b-hale-active-done.png", 6);

            Queue(delegate { Press("EndRound"); }, null, 10);
            Queue(delegate { Report("after END ROUND"); }, "seq-3-end-round.png", 6);

            Queue(delegate { Tap(4); Press("UnitEdge"); Press("UnitTab"); }, null, 8);
            Queue(delegate { Press("Hp10"); }, null, 8);
            Queue(delegate { Report("after Marrow -> 10% HP"); }, "seq-4-marrow-10pct.png", 6);
            Queue(delegate { Press("RightClose"); }, null, 6);

            Queue(delegate { Tap(1); Press("Basic"); Tap(4); }, null, 6);
            Queue(null, "seq-5a-kiva-basic.png", 18);
            Queue(delegate { Report("after Kiva BASIC -> Marrow"); }, "seq-5b-kiva-basic-done.png", 6);
            Queue(delegate { Tap(4); Press("UnitEdge"); Press("UnitTab"); }, null, 8);
            Queue(delegate { Report("Marrow afterwards"); }, "seq-5c-marrow-after.png", 6);
            Start();
        }

        /// <summary>Three rounds of Auto at 1x, a switch to Manual mid-round, one hand-driven
        /// action, and a switch back.</summary>
        [MenuItem("Emberfall/Dev Arena Shots/Capture auto then manual", false, 43)]
        public static void CaptureAutoThenManual()
        {
            _queue.Clear();
            Queue(delegate { SetSize(1600, 900); CloseDrawers(); Press("Reset"); }, null, 10);
            Queue(delegate { Press("Mode"); Press("Speed2"); }, null, 10);   // AUTO at 1x
            Queue(delegate { Report("auto running"); }, "auto-1-running.png", 120);
            Queue(delegate { Report("auto, later"); }, "auto-2-running.png", 120);
            Queue(delegate { Report("auto, later still"); }, "auto-3-running.png", 60);
            Queue(delegate { Press("Mode"); Report("switched to MANUAL mid-round"); },
                  "auto-4-switched-to-manual.png", 10);
            Queue(delegate { Tap(0); Press("Basic"); Tap(3); }, null, 16);
            Queue(delegate { Report("acted by hand while paused"); }, "auto-5-manual-action.png", 8);
            Queue(delegate { Press("Mode"); Report("back to AUTO"); }, "auto-6-back-to-auto.png", 90);
            Queue(delegate { Report("auto continued from where it stood"); }, "auto-7-continued.png", 6);
            Start();
        }

        /// <summary>
        /// Puts Elyra on the ally line (she is the only kit in the roster that applies Taunt),
        /// casts her active so an enemy carries it, then forces a blow at the OTHER enemy and
        /// catches the toast that says the anchor answered instead.
        /// </summary>
        [MenuItem("Emberfall/Dev Arena Shots/Capture a Taunt redirect", false, 44)]
        public static void CaptureTauntRedirect()
        {
            _queue.Clear();
            Queue(delegate { SetSize(1600, 900); CloseDrawers(); }, null, 8);
            Queue(delegate { Press("SetupEdge"); }, null, 8);
            Queue(delegate { PressNth("Pick", 1); Search("elyra"); }, null, 8);
            Queue(delegate { Press("Hero_elyra_the_patient_warden"); }, null, 8);
            Queue(delegate { Report("board with Elyra"); }, "taunt-1-board.png", 6);
            Queue(delegate { Press("ApplyBoard"); }, null, 16);
            Queue(delegate { Press("SetupClose"); }, null, 8);
            Queue(delegate { Tap(1); Press("Active"); Tap(3); }, null, 24);   // Elyra casts
            Queue(delegate { Report("after Elyra's active"); }, "taunt-2-elyra-cast.png", 8);
            Queue(delegate { Tap(0); Press("Basic"); Tap(4); }, null, 4);
            Queue(delegate { Report("forced Kael at the other enemy"); }, "taunt-3-toast.png", 10);
            Queue(null, "taunt-4-toast-after.png", 6);
            Start();
        }

        /// <summary>
        /// Kael's basic beside a hero whose clips were not written by the same importer pass.
        /// Kael is one of the five that carry a hardcoded playbackSpeed of 1.30; Aeron is not, so
        /// the same nominal 30 fps plays out at two different speeds.
        /// </summary>
        [MenuItem("Emberfall/Dev Arena Shots/Capture the clip pacing comparison", false, 45)]
        public static void CaptureClipPacing()
        {
            _queue.Clear();
            Queue(delegate { SetSize(1600, 900); CloseDrawers(); }, null, 8);
            Queue(delegate { Press("SetupEdge"); }, null, 8);
            Queue(delegate { PressNth("Pick", 2); Search("aeron"); }, null, 8);
            Queue(delegate { Press("Hero_aeron_the_unbroken_stride"); }, null, 8);
            Queue(delegate { Press("ApplyBoard"); }, null, 16);
            Queue(delegate { Press("SetupClose"); }, null, 8);

            // Kael's basic, four frames apart
            Queue(delegate { Tap(0); Press("UnitEdge"); Press("AnimTab"); Press("Clip_basic"); }, null, 2);
            for (int i = 0; i < 4; i++) Queue(null, "pacing-kael-" + i + ".png", 4);
            Queue(delegate { Press("Details"); Report("Kael clip data"); }, "pacing-kael-details.png", 6);
            Queue(delegate { Press("Details"); }, null, 4);

            // Aeron's, the same four frames apart
            Queue(delegate { Tap(2); Press("Clip_basic"); }, null, 2);
            for (int i = 0; i < 4; i++) Queue(null, "pacing-aeron-" + i + ".png", 4);
            Queue(delegate { Press("Details"); Report("Aeron clip data"); }, "pacing-aeron-details.png", 6);
            Start();
        }


        /// <summary>
        /// THE REAL BATTLE, with drawn heroes on both sides.
        ///
        /// This is BattleScreen, not the arena: the hub boots, a Proving match is launched against
        /// a line this method names, and the capture happens inside the game's own battle view. It
        /// exists so "which way do enemies face" can be answered about the GAME rather than about
        /// the tool, which is the question that decides where a facing bug lives.
        /// </summary>
        [MenuItem("Emberfall/Dev Arena Shots/Capture the REAL battle (heroes both sides)", false, 46)]
        public static void CaptureRealBattle()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[dev-arena-shots] leave Play Mode first"); return; }
            SessionState.SetBool(RealBattleKey, true);
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            EditorApplication.isPlaying = true;
        }

        private const string RealBattleKey = "Emberfall.DevArenaShots.RealBattle";

        /// <summary>Called once Play Mode is up. Entering Play Mode reloads the domain, so the
        /// two halves cannot be one call.</summary>
        [MenuItem("Emberfall/Dev Arena Shots/Launch the REAL battle now (already playing)", false, 47)]
        public static void QueueRealBattle()
        {
            _queue.Clear();
            Queue(delegate { SetSize(1600, 900); }, null, 30);
            Queue(delegate { LaunchProvingAgainstDrawnHeroes(); }, null, 120);
            Queue(null, "real-battle-1.png", 90);
            Queue(null, "real-battle-2.png", 90);
            Queue(null, "real-battle-3.png", 10);
            Start();
        }

        private static void LaunchProvingAgainstDrawnHeroes()
        {
            GameApp app = GameApp.Instance;
            if (app == null) { Debug.LogError("[dev-arena-shots] no GameApp"); return; }

            long now = TimeUtil.NowUnix();
            int max = app.Session.Balance.ProvingBrandsMax;
            app.Session.Profile.ProvingBrandsBanked = max;
            app.Session.Profile.ProvingBrandsClockUnix = now;

            // Named on purpose: two heroes that actually ship sprite sheets, one of them from the
            // seven whose art is authored facing the other way (SpriteMirror false), so the shot
            // shows both cases at once.
            BattleLoadout line = new BattleLoadout();
            line.Label = "Facing check";
            string[] ids = { "kael_the_unyielding", "nysera_the_missing_arrow", "kiva_the_forsaken_oath" };
            for (int i = 0; i < ids.Length; i++)
            {
                HeroDef d = app.Session.Content.GetHero(ids[i]);
                if (d == null) { Debug.LogWarning("[dev-arena-shots] missing hero " + ids[i]); continue; }
                LoadoutUnit u = new LoadoutUnit();
                u.DefId = d.Id;
                u.Level = 40;
                u.Stars = d.Stars;
                u.RelicId = "";
                line.Units.Add(u);
            }
            app.Router.Battle.SetProvingOpponent(line);
            app.Router.Show(ScreenId.Battle);
            Debug.Log("[dev-arena-shots] launched a real Proving match against " + line.Units.Count + " drawn heroes");
        }

        /// <summary>Focuses and repaints the Game view so the capture has a rendered frame to take.
        /// ScreenCapture hands back whatever the view last drew, and an unfocused view has nothing.
        /// Same reasoning as PlayModeScreenshot's copy of this.</summary>
        public static void ForceGameViewRepaint()
        {
            Type gameView = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameView == null) return;
            EditorWindow view = EditorWindow.GetWindow(gameView, false, "Game", true);
            if (view == null) return;
            view.Show();
            view.Focus();
            view.Repaint();
        }
    }
}
#endif
