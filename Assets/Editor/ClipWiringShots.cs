#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Emberfall.Battle;
using Emberfall.Game;
using Emberfall.UI;
using Emberfall.UI.Screens;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// FRAME STRIPS OF DRAWN HEROES AT THEIR BATTLE BEATS, for checking which clip plays when.
    ///
    /// A still screenshot cannot say whether a clip lands on its beat, so this plays one real
    /// fight through the real BattleScreen - ten heroes who all ship a full sheet set, five a side,
    /// as a friendly duel - and photographs the battle camera every 0.15s through the first three
    /// actions of each watched hero, marking the frame on which a target's health actually drops.
    /// Beside the images it writes clips.csv: every clip change and every health drop, with the
    /// clip and frame the acting unit was showing at that instant. That log is the timing
    /// evidence; the images are what it looks like.
    ///
    /// After the fight it revives the watched heroes and shows the `controlled` rest loop, then a
    /// hit taken while controlled, then the release back to idle, by calling UnitActor directly.
    /// That half is driven by hand rather than by the record because no shipped battle can reach
    /// it: Stun is the only status that controls, and the only Stun in content never lands.
    ///
    /// Runs under a scratch company/product so the quit-time save cannot touch a real save - the
    /// ArmorVerification pattern - and with the first-run prologue suppressed. Output goes to
    /// Docs/ui-shots/clip-wiring/{label}/, which is gitignored.
    ///
    ///   Unity.exe -projectPath . -executeMethod Emberfall.EditorTools.ClipWiringShots.Run -clipShotsLabel after
    ///
    /// Launched that way it quits the editor when done. From the menu it leaves the editor open.
    /// </summary>
    [InitializeOnLoad]
    public static class ClipWiringShots
    {
        private const string ActiveKey = "ClipWiringShots.Active";
        private const string LabelKey = "ClipWiringShots.Label";
        private const string RestoreKey = "ClipWiringShots.Restore";
        private const string ExitKey = "ClipWiringShots.ExitWhenDone";
        private const string OrigCompanyKey = "ClipWiringShots.OrigCompany";
        private const string OrigProductKey = "ClipWiringShots.OrigProduct";
        private const string OrigSuppressKey = "ClipWiringShots.OrigSuppress";

        private const string ScratchCompany = "Emberfall Scratch";
        private const string ScratchProduct = "Emberfall Clip Shots";

        private static readonly string[] Team =
        {
            "kael_the_unyielding", "altheon_the_first_witness", "hale_the_relentless_hunter",
            "nereia_the_field_medic", "caldus_the_appointed_guard"
        };

        private static readonly string[] Opponents =
        {
            "brenna_the_remaining_wall", "sylven_the_unfading_blade", "isara_the_second_chance",
            "rovan_the_adaptive_commander", "velis_the_hollow_vow"
        };

        /// <summary>The heroes whose actions are photographed. All three ship all seven clips.</summary>
        private static readonly string[] Watched =
        {
            "kael_the_unyielding", "altheon_the_first_witness", "hale_the_relentless_hunter"
        };

        private const int ActionsPerHero = 3;
        private const float StripInterval = 0.15f;
        private const float StripCeiling = 2.7f;
        private const float BattleCeiling = 120f;

        // Play-mode state. Plain statics are fine here: the one domain reload is the one entering
        // Play Mode, and everything that must cross it is in SessionState above.
        private static int _stage;
        private static float _stageStart;
        private static BattleStage _world;
        private static StreamWriter _log;
        private static RenderTexture _rt;
        private static Texture2D _read;
        private static readonly Dictionary<UnitActor, string> _lastClip = new Dictionary<UnitActor, string>();
        private static readonly Dictionary<UnitActor, float> _lastHp = new Dictionary<UnitActor, float>();
        private static readonly Dictionary<UnitActor, int> _actionsShot = new Dictionary<UnitActor, int>();
        private static UnitActor _stripActor;
        private static string _stripClip;
        private static int _stripIndex;
        private static float _stripStart;
        private static float _nextShot;
        private static int _stripFrame;

        static ClipWiringShots()
        {
            EditorApplication.update += Pump;
            EditorApplication.update += PumpRestore;
        }

        public static void Run()
        {
            string label = "shots";
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-clipShotsLabel") label = args[i + 1];
            SessionState.SetBool(ExitKey, true);
            Begin(label);
        }

        [MenuItem("Emberfall/Capture/Clip wiring frame strips", false, 80)]
        public static void RunFromMenu()
        {
            SessionState.SetBool(ExitKey, false);
            Begin("manual");
        }

        private static void Begin(string label)
        {
            SessionState.SetString(OrigCompanyKey, PlayerSettings.companyName);
            SessionState.SetString(OrigProductKey, PlayerSettings.productName);
            SessionState.SetBool(OrigSuppressKey, EditorPrefs.GetBool(GameApp.SuppressFirstRunKey, false));

            PlayerSettings.companyName = ScratchCompany;
            PlayerSettings.productName = ScratchProduct;
            EditorPrefs.SetBool(GameApp.SuppressFirstRunKey, true);

            string dataPath = Application.persistentDataPath;
            if (dataPath.IndexOf(ScratchProduct, StringComparison.OrdinalIgnoreCase) < 0)
            {
                Debug.LogError("ClipWiringShots: persistentDataPath is '" + dataPath
                               + "', not the scratch identity. Refusing to enter Play Mode.");
                SessionState.SetBool(RestoreKey, true);
                return;
            }
            string save = Path.Combine(dataPath, "emberfall_save.json");
            if (File.Exists(save)) File.Delete(save);
            PlayerPrefs.DeleteAll();

            string outDir = OutDir(label);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            Debug.Log("ClipWiringShots: capturing '" + label + "' into " + outDir);
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            SessionState.SetString(LabelKey, label);
            SessionState.SetBool(ActiveKey, true);
            _stage = 0;
            EditorCaptureGuard.EnterPlayMode();
        }

        private static string OutDir(string label)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/ui-shots/clip-wiring/" + label));
        }

        private static void Pump()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (!EditorApplication.isPlaying) return;

            GameApp app = GameApp.Instance;
            if (_stage == 0)
            {
                if (app == null || !GameApp.BootCompleted) return;
                StartDuel(app);
                _stage = 1;
                _stageStart = Time.time;
                return;
            }

            if (_stage == 1)
            {
                if (_world == null) _world = UnityEngine.Object.FindFirstObjectByType<BattleStage>();
                if (_world == null || _world.Actors.Count == 0) return;
                SampleBattle();
                BattleScreen battle = app != null ? app.Router.Battle : null;
                bool done = battle != null && battle.Finished && _stripActor == null;
                if (done || Time.time - _stageStart > BattleCeiling)
                {
                    Note("battle-over", null, done ? "finished" : "ceiling");
                    // Playback halted, so no hit from the fight still running lands on a watched
                    // hero mid-demo and restarts its reaction.
                    if (battle != null) battle.StopAllCoroutines();
                    _stage = 2;
                    _stageStart = Time.time;
                    _stripFrame = 0;
                    _nextShot = 0f;
                }
                return;
            }

            if (_stage == 2)
            {
                if (ControlledDemo()) { _stage = 3; }
                return;
            }

            if (_stage == 3) Finish();
        }

        // ------------------------------------------------------------------ the fight

        private static void StartDuel(GameApp app)
        {
            ContentDatabase content = app.Session.Content;
            PlayerProfile profile = app.Session.Profile;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // Granted first and seated after, because each grant normalises the team and would
            // shuffle a seat assigned between two grants.
            List<HeroInstance> granted = new List<HeroInstance>();
            for (int i = 0; i < Team.Length; i++) granted.Add(app.Session.GrantHeroDirect(content.GetHero(Team[i]), now));
            for (int i = 0; i < granted.Count; i++) profile.AssignToTeam(i, granted[i].Uid);

            BattleLoadout line = new BattleLoadout();
            line.Label = "Clip Shots";
            for (int i = 0; i < Opponents.Length; i++)
            {
                LoadoutUnit unit = new LoadoutUnit();
                unit.DefId = Opponents[i];
                unit.Stars = content.GetHero(Opponents[i]).Stars;
                line.Units.Add(unit);
            }

            string path = Path.Combine(OutDir(SessionState.GetString(LabelKey, "shots")), "clips.csv");
            _log = new StreamWriter(path, false, new UTF8Encoding(false));
            _log.WriteLine("t,unit,what,clip,frame,detail");

            _rt = new RenderTexture(1600, 900, 24);
            _read = new Texture2D(1600, 900, TextureFormat.RGB24, false);

            // An unfocused editor otherwise stops ticking Play Mode while EditorApplication.update
            // keeps running, which reads as a fight that stalled. Every clock here is game time.
            Application.runInBackground = true;
            app.Router.Battle.SetDuelOpponent(line, "Clip Shots");
            app.Router.Show(ScreenId.Battle);
            Debug.Log("ClipWiringShots: duel started");
        }

        private static void SampleBattle()
        {
            UnitActor acting = null;
            for (int i = 0; i < _world.Actors.Count; i++)
            {
                UnitActor a = _world.Actors[i];
                string clip = ClipOf(a);
                string was;
                _lastClip.TryGetValue(a, out was);
                if (clip != was)
                {
                    _lastClip[a] = clip;
                    Note("clip", a, clip);
                    if (IsWatched(a) && BattleClips.IsAction(clip)) BeginStrip(a, clip);
                }
                if (BattleClips.IsAction(clip)) acting = a;
            }

            bool hitThisFrame = false;
            for (int i = 0; i < _world.Actors.Count; i++)
            {
                UnitActor a = _world.Actors[i];
                float was;
                if (_lastHp.TryGetValue(a, out was) && a.DisplayHp < was - 0.5f)
                {
                    string by = acting != null ? acting.Snapshot.Name + " " + ClipOf(acting) + " frame "
                                                 + acting.Rig.Animation.CurrentFrame : "no action clip playing";
                    Note("hp-drop", a, "-" + Mathf.RoundToInt(was - a.DisplayHp) + " while " + by);
                    if (_stripActor != null && acting == _stripActor) hitThisFrame = true;
                }
                _lastHp[a] = a.DisplayHp;
            }

            if (_stripActor == null) return;
            float t = Time.time - _stripStart;
            bool clipOver = ClipOf(_stripActor) != _stripClip;
            if (clipOver || t > StripCeiling)
            {
                Shoot(StripName("end"));
                _stripActor = null;
                return;
            }
            if (hitThisFrame) Shoot(StripName("HIT"));
            else if (Time.time >= _nextShot)
            {
                Shoot(StripName(""));
                _nextShot = Time.time + StripInterval;
            }
        }

        private static void BeginStrip(UnitActor a, string clip)
        {
            int shot;
            _actionsShot.TryGetValue(a, out shot);
            if (shot >= ActionsPerHero || _stripActor != null) return;
            _actionsShot[a] = shot + 1;
            _stripActor = a;
            _stripClip = clip;
            _stripIndex = shot + 1;
            _stripStart = Time.time;
            _stripFrame = 0;
            _nextShot = Time.time;
        }

        private static string StripName(string tag)
        {
            string hero = _stripActor.Snapshot.DefId.Split('_')[0];
            string name = hero + "_action" + _stripIndex + "_" + _stripClip + "_"
                          + (_stripFrame++).ToString("00") + "_f"
                          + _stripActor.Rig.Animation.CurrentFrame.ToString("000")
                          + (string.IsNullOrEmpty(tag) ? "" : "_" + tag);
            return name;
        }

        // ------------------------------------------------------------------ controlled

        /// <summary>
        /// Revive, control, hit, release - on a fixed clock, photographed throughout. Returns true
        /// when finished. SetControlled is looked up rather than called, so this file compiles
        /// against the tree from before that method existed and photographs what that tree does.
        /// </summary>
        private static bool ControlledDemo()
        {
            float t = Time.time - _stageStart;
            List<UnitActor> watched = new List<UnitActor>();
            for (int i = 0; i < _world.Actors.Count; i++) if (IsWatched(_world.Actors[i])) watched.Add(_world.Actors[i]);
            MethodInfo setControlled = typeof(UnitActor).GetMethod("SetControlled", new[] { typeof(bool) });

            if (_stripFrame == 0 && _nextShot == 0f)
            {
                foreach (UnitActor a in watched) a.Revive();
                foreach (UnitActor a in watched)
                    if (setControlled != null) setControlled.Invoke(a, new object[] { true });
                Note("controlled-on", null, setControlled != null ? "SetControlled(true)" : "no SetControlled in this tree");
                _nextShot = 0.0001f;
            }

            if (t >= 1.2f && _stripFrame < 1000)
            {
                foreach (UnitActor a in watched) a.PlayHit(false);
                Note("hit-while-controlled", null, "");
                _stripFrame = 1000;
            }
            if (t >= 4.2f && _stripFrame < 2000)
            {
                foreach (UnitActor a in watched)
                    if (setControlled != null) setControlled.Invoke(a, new object[] { false });
                Note("controlled-off", null, "");
                _stripFrame = 2000;
            }

            foreach (UnitActor a in watched)
            {
                string clip = ClipOf(a);
                string was;
                _lastClip.TryGetValue(a, out was);
                if (clip != was) { _lastClip[a] = clip; Note("clip", a, clip); }
            }

            if (t < 5.0f && Mathf.FloorToInt(t / 0.3f) != _lastDemoTick)
            {
                _lastDemoTick = Mathf.FloorToInt(t / 0.3f);
                string first = watched.Count > 0 ? ClipOf(watched[0]) : "none";
                Shoot("controlled_" + _lastDemoTick.ToString("00") + "_t" + (t).ToString("0.0") + "_" + first);
            }
            return t >= 5.0f;
        }

        private static int _lastDemoTick = -1;

        // ------------------------------------------------------------------ helpers

        private static bool IsWatched(UnitActor a)
        {
            return a != null && a.Snapshot != null && a.Snapshot.IsPlayer
                   && Array.IndexOf(Watched, a.Snapshot.DefId) >= 0;
        }

        private static string ClipOf(UnitActor a)
        {
            if (a == null || a.Rig == null || a.Rig.Animation == null) return "(no animator)";
            return a.Rig.Animation.CurrentClip ?? "(none)";
        }

        private static void Note(string what, UnitActor a, string detail)
        {
            if (_log == null) return;
            string unit = a != null ? a.Snapshot.Name + (a.Snapshot.IsPlayer ? " (P)" : " (E)") : "";
            string frame = a != null && a.Rig != null && a.Rig.Animation != null ? a.Rig.Animation.CurrentFrame.ToString() : "";
            string clip = a != null ? ClipOf(a) : "";
            _log.WriteLine(Time.time.ToString("0.000") + "," + unit + "," + what + "," + clip + "," + frame + ","
                           + (detail ?? "").Replace(",", ";"));
            _log.Flush();
        }

        private static void Shoot(string name)
        {
            if (_world == null || _world.Camera == null || _rt == null) return;
            Camera cam = _world.Camera;
            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = _rt;
            cam.Render();
            cam.targetTexture = previous;
            RenderTexture active = RenderTexture.active;
            RenderTexture.active = _rt;
            _read.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
            _read.Apply();
            RenderTexture.active = active;
            string path = Path.Combine(OutDir(SessionState.GetString(LabelKey, "shots")), name + ".png");
            File.WriteAllBytes(path, _read.EncodeToPNG());
        }

        private static void Finish()
        {
            if (_log != null) { _log.Close(); _log = null; }
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(RestoreKey, true);
            Debug.Log("ClipWiringShots: done");
            EditorApplication.isPlaying = false;
        }

        /// <summary>Puts the real identity and first-run setting back once Play Mode has ended, so
        /// the quit-time save lands on the scratch file and never the real one.</summary>
        private static void PumpRestore()
        {
            if (!SessionState.GetBool(RestoreKey, false)) return;
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(RestoreKey, false);

            string company = SessionState.GetString(OrigCompanyKey, "");
            string product = SessionState.GetString(OrigProductKey, "");
            if (!string.IsNullOrEmpty(product))
            {
                PlayerSettings.companyName = company;
                PlayerSettings.productName = product;
                AssetDatabase.SaveAssets();
            }
            EditorPrefs.SetBool(GameApp.SuppressFirstRunKey, SessionState.GetBool(OrigSuppressKey, false));
            Debug.Log("ClipWiringShots: identity restored to '" + company + "/" + product + "'");
            if (SessionState.GetBool(ExitKey, false)) EditorApplication.Exit(0);
        }
    }
}
#endif
