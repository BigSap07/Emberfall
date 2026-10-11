#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Emberfall.Battle;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// CAPTURES THE HIT FEEDBACK (VfxDirector) FROM THE DEV ARENA'S STAGE.
    ///
    /// Opens an unsaved scene holding the Dev Arena, enters Play Mode, freezes the arena's own
    /// fight, then plays chosen events through the director exactly as DevArena.Handle does
    /// (Director.Play, then PlayHit / PlayDeath) and renders the stage camera to a PNG at fixed
    /// delays after each one. Rendering the camera itself rather than the Game view means the
    /// frame does not depend on which editor window has focus, and shows the effect without the
    /// arena's panels over it.
    ///
    ///   Unity.exe -projectPath &lt;worktree&gt; -executeMethod Emberfall.EditorTools.VfxShots.Run
    ///
    /// Writes Tools/Screenshots/hit-vfx/*.png (git-ignored) and quits the editor when done.
    /// State lives in SessionState and the pump is re-subscribed by InitializeOnLoad, for the
    /// reason PlayModeScreenshot documents: entering Play Mode reloads the domain.
    /// </summary>
    [InitializeOnLoad]
    public static class VfxShots
    {
        private const string ActiveKey = "VfxShots.Active";
        public const string OutputDir = "Tools/Screenshots/hit-vfx";
        /// <summary>A throwaway 4x4 sheet written under Resources for the flipbook shot, so the
        /// sheet-playing path is exercised before any real art exists. Deleted after the run.</summary>
        public const string TestSheetAsset = "Assets/Resources/Vfx/_vfxshots_test_sheet.png";
        private const string TestSheetResource = "Vfx/_vfxshots_test_sheet";
        private const string TestAbility = "vfxshots_test_ability";

        private const int Width = 1600;
        private const int Height = 900;

        private sealed class Step
        {
            public Action Do;
            public string Shot;
            public int After;         // game frames to wait before the next step (60 per second)
        }

        private static List<Step> _queue;
        private static int _next;
        private static int _readyFrame;
        private static double _startedAt;
        private static BattleStage _stage;
        private static MonoBehaviour _arena;

        static VfxShots()
        {
            EditorApplication.update += Pump;
        }

        [MenuItem("Emberfall/Capture/Hit VFX (Dev Arena stage)", false, 80)]
        public static void Run()
        {
            Directory.CreateDirectory(OutputDir);
            MakeTestSheet();
            SessionState.SetBool(ActiveKey, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject host = new GameObject("DevArena");
            Type arena = null;
            foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                arena = asm.GetType("Emberfall.Dev.DevArena");
                if (arena != null) break;
            }
            if (arena == null) { Debug.LogError("[vfx-shots] Emberfall.Dev.DevArena not found"); SessionState.SetBool(ActiveKey, false); return; }
            host.AddComponent(arena);
            Debug.Log("[vfx-shots] entering Play Mode");
            EditorApplication.isPlaying = true;
        }

        private static void Pump()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (!EditorApplication.isPlaying) return;

            Application.runInBackground = true;
            // Fixed 60 fps game time whatever the editor's real frame rate is, and a player-loop
            // update forced on every tick. Without both, an unfocused editor runs a handful of
            // frames a second and "50ms after the hit" is not a moment that can be photographed.
            Time.captureFramerate = 60;
            EditorApplication.QueuePlayerLoopUpdate();
            double now = EditorApplication.timeSinceStartup;
            if (_startedAt == 0) _startedAt = now;

            if (_queue == null)
            {
                _stage = UnityEngine.Object.FindFirstObjectByType<BattleStage>();
                if (_stage == null || _stage.Actors.Count < 2 || _stage.Director == null)
                {
                    if (now - _startedAt > 120) Finish("[vfx-shots] no stage after 120s, giving up");
                    return;
                }
                // let the arena finish its first frames, then stop its own fight so only the
                // events below move anything
                if (now - _startedAt < 6) return;
                foreach (MonoBehaviour m in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                    if (m.GetType().FullName == "Emberfall.Dev.DevArena") _arena = m;
                if (_arena != null) _arena.StopAllCoroutines();
                Build();
                _next = 0;
                _readyFrame = Time.frameCount;
                Debug.Log("[vfx-shots] stage ready: " + _stage.Actors.Count + " actors, " + _queue.Count + " steps");
            }

            if (Time.frameCount < _readyFrame) return;
            if (_next >= _queue.Count) { Finish("[vfx-shots] done"); return; }

            Step s = _queue[_next++];
            try
            {
                if (s.Do != null) s.Do();
                if (!string.IsNullOrEmpty(s.Shot)) Capture(s.Shot);
            }
            catch (Exception e)
            {
                Debug.LogError("[vfx-shots] step failed: " + e);
            }
            _readyFrame = Time.frameCount + Mathf.Max(1, s.After);
        }

        private static void Finish(string message)
        {
            Debug.Log(message);
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += delegate { EditorApplication.Exit(0); };
        }

        // ------------------------------------------------------------------ the sequence

        private static UnitActor Ally(int n)
        {
            int seen = 0;
            for (int i = 0; i < _stage.Actors.Count; i++)
                if (_stage.Actors[i].Snapshot.IsPlayer && seen++ == n) return _stage.Actors[i];
            return _stage.Actors[0];
        }

        private static UnitActor Foe(int n)
        {
            int seen = 0;
            for (int i = 0; i < _stage.Actors.Count; i++)
                if (!_stage.Actors[i].Snapshot.IsPlayer && seen++ == n) return _stage.Actors[i];
            return _stage.Actors[_stage.Actors.Count - 1];
        }

        private static void Q(Action act, string shot, int after)
        {
            Step s = new Step();
            s.Do = act;
            s.Shot = shot;
            s.After = after;
            _queue.Add(s);
        }

        // One effect, photographed at fixed frame offsets after it fires (60 frames a second):
        // +2 (33ms), +5 (83ms, past the 50ms hit stop), +10 (167ms), +20 (333ms), then settle.
        private static void Strip(string prefix, Action fire)
        {
            Q(fire, null, 2);
            Q(null, prefix + "-a", 3);
            Q(null, prefix + "-b", 5);
            Q(null, prefix + "-c", 10);
            Q(null, prefix + "-d", 90);
        }

        private static void Build()
        {
            _queue = new List<Step>();
            for (int i = 0; i < _stage.Actors.Count; i++)
                Debug.Log("[vfx-shots] actor " + i + ": id " + _stage.Actors[i].Snapshot.Id + " "
                          + _stage.Actors[i].Snapshot.Name + " " + _stage.Actors[i].Snapshot.Aspect
                          + (_stage.Actors[i].Snapshot.IsPlayer ? " ally" : " foe"));
            Q(delegate { Capture("0-before"); }, null, 30);

            Strip("hit-normal", delegate { Hit(Ally(0), Foe(0), false, 120, "slash"); });
            Strip("hit-crit", delegate { Hit(Ally(0), Foe(1), true, 310, "slash"); });
            Strip("hit-impact", delegate { Hit(Ally(1), Foe(0), false, 90, "impact"); });
            Strip("hit-burst", delegate { Hit(Ally(1), Foe(1), false, 200, "burst"); });
            Strip("shield-absorb", delegate { Shield(Foe(2), 90f, 30); });
            Strip("shield-break", delegate { Shield(Foe(2), 25f, 25); });
            Strip("status-harmful", delegate { Status(Foe(2), StatusKind.Burn, Ally(0)); });
            Strip("status-beneficial", delegate { Status(Ally(2), StatusKind.AttackUp, Ally(0)); });

            // one hit from each of the six aspects on the same target, to read the tint. The
            // default board is all Ember, so the attacker's snapshot aspect is set per shot; that
            // is read by the director for the accent and by nothing that resolves a fight.
            foreach (Aspect aspect in Enum.GetValues(typeof(Aspect)))
            {
                Aspect chosen = aspect;
                string label = "aspect-" + aspect.ToString().ToLowerInvariant();
                Q(delegate { Ally(1).Snapshot.Aspect = chosen; Hit(Ally(1), Foe(0), false, 80, "impact"); }, null, 4);
                Q(null, label, 60);
            }
            Strip("kill", delegate { Kill(Foe(1), Ally(1)); });

            // The flipbook path: an ability whose vfx.json entry names a sheet that exists. The
            // entry is injected here, at run time, so vfx.json is not touched.
            Q(delegate
            {
                VfxEntry sheet = new VfxEntry();
                sheet.Id = TestAbility;
                sheet.Sheet = TestSheetResource;
                sheet.Columns = 4;
                sheet.Rows = 4;
                sheet.Frames = 16;
                sheet.Fps = 24f;
                sheet.Scale = 1.6f;
                VfxDirector.Config.Abilities[TestAbility] = sheet;
            }, null, 1);
            Strip("flipbook", delegate { Hit(Ally(0), Foe(0), false, 100, "impact", TestAbility); });

            // The TAP path, which is what BattleScreen uses: ExactFeed off, a record bound, and
            // the same calls the screen makes (PlayHit, the DisplayShield setter, PlayDeath). The
            // effects must come out tinted by the attacker named in the RECORD, with nothing
            // passed in by hand.
            Q(delegate
            {
                _stage.Director.ExactFeed = false;
                Ally(0).Snapshot.Aspect = Aspect.Tide;
                BattleResult r = new BattleResult();
                BattleEvent dmg = BattleEvent.Make(BattleEventType.Damage, 1);
                dmg.SourceId = Ally(0).Snapshot.Id; dmg.TargetId = Foe(0).Snapshot.Id; dmg.Critical = true; dmg.Value = 300; dmg.Vfx = "slash";
                r.Events.Add(dmg);
                BattleEvent abs = BattleEvent.Make(BattleEventType.ShieldAbsorb, 1);
                abs.TargetId = Foe(1).Snapshot.Id; abs.Value = 25;
                r.Events.Add(abs);
                BattleEvent dead = BattleEvent.Make(BattleEventType.Death, 1);
                dead.SourceId = Ally(0).Snapshot.Id; dead.TargetId = Foe(0).Snapshot.Id;
                r.Events.Add(dead);
                _stage.Director.Bind(r);
                Foe(0).Revive();
                Foe(0).PlayHit(true);
            }, null, 2);
            Q(null, "tap-crit-a", 3);
            Q(null, "tap-crit-b", 40);
            Q(delegate { Foe(1).Revive(); Foe(1).DisplayShield = 25f; Foe(1).DisplayShield = 0f; }, null, 5);
            Q(null, "tap-shield-break-a", 40);
            Q(delegate { Foe(0).StartCoroutine(Foe(0).PlayDeath(1f)); }, null, 5);
            Q(null, "tap-kill-a", 5);
            Q(null, "tap-kill-b", 30);
        }
        private static void Hit(UnitActor src, UnitActor tgt, bool crit, int value, string vfxTag)
        {
            Hit(src, tgt, crit, value, vfxTag, null);
        }

        private static void Hit(UnitActor src, UnitActor tgt, bool crit, int value, string vfxTag, string abilityId)
        {
            tgt.Revive();
            BattleEvent e = BattleEvent.Make(BattleEventType.Damage, 1);
            e.SourceId = src.Snapshot.Id;
            e.TargetId = tgt.Snapshot.Id;
            e.Value = value;
            e.Critical = crit;
            e.Vfx = vfxTag;
            e.AbilityId = abilityId;
            _stage.Director.Play(e);
            tgt.PlayHit(crit);
            if (crit) _stage.Shake(0.10f, 0.18f);
        }

        private static void Shield(UnitActor tgt, float have, int absorb)
        {
            tgt.Revive();
            tgt.DisplayShield = have;
            BattleEvent e = BattleEvent.Make(BattleEventType.ShieldAbsorb, 1);
            e.TargetId = tgt.Snapshot.Id;
            e.Value = absorb;
            _stage.Director.Play(e);
            tgt.DisplayShield = Mathf.Max(0f, tgt.DisplayShield - absorb);
        }

        private static void Status(UnitActor tgt, StatusKind kind, UnitActor src)
        {
            tgt.Revive();
            BattleEvent e = BattleEvent.Make(BattleEventType.StatusApplied, 1);
            e.SourceId = src.Snapshot.Id;
            e.TargetId = tgt.Snapshot.Id;
            e.Status = kind;
            _stage.Director.Play(e);
        }

        private static void Kill(UnitActor tgt, UnitActor killer)
        {
            tgt.Revive();
            BattleEvent e = BattleEvent.Make(BattleEventType.Death, 1);
            e.SourceId = killer.Snapshot.Id;
            e.TargetId = tgt.Snapshot.Id;
            _stage.Director.Play(e);
            tgt.StartCoroutine(tgt.PlayDeath(1f));
        }

        private static void MakeTestSheet()
        {
            const int cell = 64;
            Texture2D tex = new Texture2D(cell * 4, cell * 4, TextureFormat.RGBA32, false);
            Color32[] px = new Color32[cell * 4 * cell * 4];
            for (int f = 0; f < 16; f++)
            {
                int col = f % 4;
                int row = f / 4;                                    // row 0 is the TOP row of the sheet
                float radius = 6f + f * 1.7f;
                for (int y = 0; y < cell; y++)
                {
                    for (int x = 0; x < cell; x++)
                    {
                        float dx = x - cell * 0.5f, dy = y - cell * 0.5f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        bool ring = Mathf.Abs(d - radius) < 2.5f;
                        // a marker in the cell's top-left corner, so a flipped or transposed sheet shows
                        bool corner = x < 10 && y > cell - 11;
                        byte a = (byte)((ring || corner) ? 255 : 0);
                        int pxX = col * cell + x;
                        int pxY = (3 - row) * cell + y;             // texture y is bottom-up
                        px[pxY * cell * 4 + pxX] = new Color32(255, 255, 255, a);
                    }
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(TestSheetAsset));
            File.WriteAllBytes(TestSheetAsset, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TestSheetAsset);
        }

        // ------------------------------------------------------------------ capture

        private static void Capture(string name)
        {
            Camera cam = _stage.Camera;
            RenderTexture rt = new RenderTexture(Width, Height, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(OutputDir, name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = previous;
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log("[vfx-shots] wrote " + name + ".png");
        }
    }
}
#endif
