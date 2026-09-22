#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Game;
using Emberfall.UI;
using Emberfall.UI.Summon;

namespace Emberfall.Dev
{
    /// <summary>
    /// POSES THE GAME FOR THE TEASER CAMERA, ONE FRAME AT A TIME. Driven by
    /// Emberfall.EditorTools.TeaserCapture, which owns the recorder; this owns only what is on
    /// screen while it records.
    ///
    /// It changes presentation and nothing else. The summon is already a pure function of
    /// (pull, phase, time), so a take is SummonSequence.FreezeAt called once per frame with a
    /// clock this component chooses - which is how the buildup is shot slower than it plays,
    /// natively, with no interpolated frames. The battle is the real BattleScreen playing a real
    /// event list; this only slows the world clock and switches the UI canvas off.
    ///
    /// NO GAME UI REACHES A TAKE. The summon's chrome (hint, counter, skip control, preview tag,
    /// the title line, the star row and the NEW ALLY note) is switched off after every pose,
    /// every other screen on the root canvas is switched off for the take, and the room dim is
    /// forced to opaque black so nothing behind the sequence can show through it. The hero's
    /// name is the one piece of type left, and only on the takes that ask for it.
    ///
    /// Runs in LateUpdate, after SummonSequence.Update has posed the frame and before the canvas
    /// rebuilds, so these overrides are what gets drawn.
    /// </summary>
    public sealed class TeaserDirector : MonoBehaviour
    {
        public enum Take { None, Summon, Battle }

        public Take Mode = Take.None;
        public bool Running;
        public int Frame;

        // ---- summon takes
        /// <summary>Sequence seconds per recorded frame. 1/60 is real time at 60fps.</summary>
        public float Rate = 1f / 60f;
        /// <summary>Sequence time at frame 0. Negative is pre-roll: black, before the press.</summary>
        public float StartTime;
        /// <summary>The pull in the preview batch whose hero is revealed.</summary>
        public int RevealIndex;
        public bool ShowName = true;

        // ---- battle takes
        public float WorldSlow = 1f;
        /// <summary>0 leaves the battle camera where BattleStage put it. Otherwise the take is
        /// framed tighter than the game's own camera: this orthographic half-height, looking at
        /// CameraTarget along the rig's unchanged 28-degree pitch. BattleStage.Shake still reads,
        /// because the shake's offset from the rig's home is carried onto the new position.</summary>
        public float CameraSize;
        public Vector3 CameraTarget;

        private readonly List<GameObject> _hidden = new List<GameObject>();
        private Canvas _disabledCanvas;
        private float _timeScaleBefore = 1f;

        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        // ================================================================== summon

        public void BeginSummon()
        {
            GameApp app = GameApp.Instance;
            SummonSequence seq = app.Router.Summon.DebugSequence;
            HideAllBut(seq.transform, app.RootCanvas.transform);
            Mode = Take.Summon;
        }

        private void PoseSummon()
        {
            SummonSequence seq = GameApp.Instance.Router.Summon.DebugSequence;
            if (seq == null) return;

            SummonTiming t = SummonTiming.Full;
            float time = StartTime + Frame * Rate;
            float press = t.Dim;
            float build = press + t.BuildupFor(5);
            float flash = build + t.FlashIn;

            // Every pull in the batch is a 5-star, so the light is the same whichever pull's
            // buildup plays. Pull 0 is used for it because it is the only one that gets the full
            // buildup rather than the mini tell; the revealed hero is swapped in at the flash.
            if (time < press) seq.FreezeAt(0, SummonSequence.Phase.Press, Mathf.Max(0f, time));
            else if (time < build) seq.FreezeAt(0, SummonSequence.Phase.Buildup, time - press);
            else if (time < flash) seq.FreezeAt(0, SummonSequence.Phase.Flash, time - build);
            else seq.FreezeAt(RevealIndex, SummonSequence.Phase.Reveal, time - flash);

            StripChrome(seq, time < 0f);
        }

        /// <summary>Everything that talks about the summon rather than being it.</summary>
        private void StripChrome(SummonSequence seq, bool preRoll)
        {
            Image dim = Get<Image>(seq, "_dim");
            if (dim != null) dim.color = Color.black;

            Off(Get<Text>(seq, "_hint"));
            Off(Get<Text>(seq, "_counter"));
            Off(Get<Text>(seq, "_previewTag"));
            Off(Get<Text>(seq, "_title"));
            Off(Get<Text>(seq, "_note"));
            Off(Get<Image>(seq, "_holdRing"));
            UIButton skip = Get<UIButton>(seq, "_skipAll");
            if (skip != null) skip.Root.gameObject.SetActive(false);
            Image[] stars = Get<Image[]>(seq, "_stars");
            if (stars != null && stars.Length > 0 && stars[0] != null)
                stars[0].transform.parent.gameObject.SetActive(false);
            if (!ShowName) Off(Get<Text>(seq, "_name"));

            // Pre-roll is black, not the first frame of the press: the light is off in Press
            // anyway, but the flash image and the stage are forced off too so a stale pose
            // from the previous take cannot leak into the first frames of this one.
            if (preRoll)
            {
                Image flash = Get<Image>(seq, "_flash");
                if (flash != null) flash.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        // ================================================================== battle

        public void BeginBattle(float slow)
        {
            GameApp app = GameApp.Instance;
            WorldSlow = slow;
            _timeScaleBefore = Time.timeScale;
            Time.timeScale = slow;
            _disabledCanvas = app.RootCanvas;
            _disabledCanvas.enabled = false;
            Mode = Take.Battle;
        }

        // ================================================================== frame

        private void FrameBattleCamera()
        {
            if (CameraSize <= 0f) return;
            Battle.BattleStage stage = FindAnyObjectByType<Battle.BattleStage>();
            if (stage == null || stage.Camera == null) return;
            Transform rig = stage.Camera.transform;
            FieldInfo homeField = typeof(Battle.BattleStage).GetField("_cameraHome", Private);
            Vector3 home = homeField != null ? (Vector3)homeField.GetValue(stage) : rig.localPosition;
            // Shake writes the rig's local position every frame as home plus a jitter; keep the
            // jitter, move the home.
            Vector3 shake = rig.localPosition - home;
            if (shake.sqrMagnitude > 1f) shake = Vector3.zero;   // already moved by this method
            _lastShakeFree = rig.parent != null ? rig.parent.InverseTransformPoint(CameraTarget - rig.forward * 12f)
                                                : CameraTarget - rig.forward * 12f;
            stage.Camera.orthographicSize = CameraSize;
            rig.localPosition = _lastShakeFree + shake;
        }

        private Vector3 _lastShakeFree;

        private void LateUpdate()
        {
            if (Mode == Take.Summon) PoseSummon();
            if (Mode == Take.Battle) FrameBattleCamera();
            if (Running) Frame++;
        }

        /// <summary>Puts back everything a take switched off.</summary>
        public void End()
        {
            Running = false;
            for (int i = 0; i < _hidden.Count; i++) if (_hidden[i] != null) _hidden[i].SetActive(true);
            _hidden.Clear();
            if (_disabledCanvas != null) _disabledCanvas.enabled = true;
            _disabledCanvas = null;
            if (Mode == Take.Battle) Time.timeScale = _timeScaleBefore;
            CameraSize = 0f;
            Mode = Take.None;
        }

        // ================================================================== helpers

        /// <summary>Switches off every sibling along the path from <paramref name="keep"/> up to
        /// <paramref name="root"/>, so only the kept branch draws.</summary>
        private void HideAllBut(Transform keep, Transform root)
        {
            Transform node = keep;
            while (node != null && node != root)
            {
                Transform parent = node.parent;
                if (parent == null) break;
                for (int i = 0; i < parent.childCount; i++)
                {
                    GameObject sibling = parent.GetChild(i).gameObject;
                    if (sibling.transform == node || !sibling.activeSelf) continue;
                    sibling.SetActive(false);
                    _hidden.Add(sibling);
                }
                node = parent;
            }
        }

        private static void Off(Component c)
        {
            if (c != null) c.gameObject.SetActive(false);
        }

        private static T Get<T>(object owner, string field) where T : class
        {
            FieldInfo f = owner.GetType().GetField(field, Private);
            return f != null ? f.GetValue(owner) as T : null;
        }
    }
}
#endif
