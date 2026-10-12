using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Emberfall.Core.Combat;

namespace Emberfall.Battle
{
    /// <summary>
    /// The battlefield: camera, painted backdrop and the unit actors. Built fresh for each battle
    /// and torn down afterwards. The scene is no longer assembled from 3D pieces - one backdrop
    /// image carries the sky, ground and horizon that a sky billboard, an arena plane and a ridge
    /// of blocks used to build between them (see BuildWorld).
    /// </summary>
    public sealed class BattleStage : MonoBehaviour
    {
        // The ten slots and the camera's pose live in BattleFraming, with the reasoning that chose
        // each number, so the battle HUD's layout model reads the same figures this stage stands
        // units on. Nothing here restates one of them.

        public Camera Camera;
        public BattleVfx Vfx;

        /// <summary>
        /// Hit feedback: flash, hit stop, knockback, particles, flipbooks. Presentation only, and
        /// driven by the events the host is already playing. See VfxDirector.
        /// </summary>
        public VfxDirector Director;

        public readonly List<UnitActor> Actors = new List<UnitActor>();

        private Transform _world;
        private Transform _cameraRig;
        private Vector3 _cameraHome;
        private Coroutine _shake;

        /// <summary>The painted backdrop quad and its texture's aspect, kept so FitBackdrop can
        /// re-fit it to a new viewport without rebuilding the world.</summary>
        private Transform _backdrop;
        private float _backdropAspect;
        private float _shakeStrength;
        private float _shakeUntil;

        public void Initialise()
        {
            _world = new GameObject("World").transform;
            _world.SetParent(transform, false);

            GameObject cameraObject = new GameObject("BattleCamera");
            cameraObject.transform.SetParent(transform, false);
            Camera = cameraObject.AddComponent<Camera>();
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = new Color(0.04f, 0.03f, 0.07f, 1f);
            // Orthographic at 28 degrees of pitch. Every number and the derivation behind it is in
            // BattleFraming, next to the slots it was solved against.
            Camera.orthographic = true;
            Camera.orthographicSize = BattleFraming.OrthographicSize;
            Camera.nearClipPlane = 0.3f;
            Camera.farClipPlane = 120f;
            Camera.depth = -10f;
            _cameraRig = cameraObject.transform;
            _cameraHome = new Vector3(BattleFraming.CameraX, BattleFraming.CameraY, BattleFraming.CameraZ);
            _cameraRig.localPosition = _cameraHome;
            _cameraRig.localRotation = Quaternion.Euler(BattleFraming.CameraPitch, 0f, 0f);

            GameObject vfxHost = new GameObject("Vfx");
            vfxHost.transform.SetParent(transform, false);
            Vfx = vfxHost.AddComponent<BattleVfx>();
            Vfx.Cam = Camera;

            GameObject directorHost = new GameObject("VfxDirector");
            directorHost.transform.SetParent(transform, false);
            Director = directorHost.AddComponent<VfxDirector>();
            Director.Attach(this);
        }

        // ------------------------------------------------------------------ world

        /// <summary>
        /// One painted backdrop quad, and nothing else. Three pieces of built geometry used to
        /// stand here and all three are gone, because the painting carries what each of them was
        /// carrying and they would otherwise fight it:
        ///
        /// - The sky billboard. The painting has its own sky. The billboard sat further out than
        ///   the backdrop does, so it would be completely hidden behind it anyway.
        /// - The arena ground plane. The painting has its own ground and horizon. This one was
        ///   also the likeliest source of the hard diagonal edge with black beyond it: the plane
        ///   is finite (32 x 32 world units) and the ridge blocks standing on it were yaw-rotated
        ///   boxes, so an edge cutting across the frame at an angle can only have come from a
        ///   rotated box silhouette or the plane running out, and both are removed here.
        /// - The ridge blocks. The painting has its own background terrain. Six rotated boxes at
        ///   a fixed depth cannot agree with a painted horizon, so they go rather than get
        ///   re-fit a third time.
        ///
        /// Camera.backgroundColor is kept and repurposed: it is now the clear colour behind the
        /// backdrop rather than behind a sky quad, so it is what would show if the frame were ever
        /// wider than the quad covers. It is an environment tone, never black.
        /// </summary>
        public void BuildWorld(string environmentId)
        {
            ClearWorld();
            BattleAssets.Environment env = BattleAssets.GetEnvironment(environmentId);
            Camera.backgroundColor = env.SkyTop;

            Texture2D painting = BattleAssets.BackdropTexture(environmentId);

            GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "Backdrop";
            Strip(backdrop);
            backdrop.transform.SetParent(_world, false);
            MeshRenderer backdropRenderer = backdrop.GetComponent<MeshRenderer>();
            backdropRenderer.material = BattleAssets.NewFx(painting, Color.white, false);
            backdropRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            backdropRenderer.receiveShadows = false;

            _backdrop = backdrop.transform;
            _backdropAspect = painting.height > 0 ? painting.width / (float)painting.height : 0f;
            FitBackdrop();
        }

        /// <summary>
        /// Sizes and places the backdrop for the camera's viewport as it is now. Split out of
        /// BuildWorld so a host whose viewport changes mid-fight (the battle screen's top bar
        /// follows the safe area) can re-fit the painting without BuildWorld's ClearWorld, which
        /// would destroy every actor standing on it. It touches the quad and nothing else.
        /// </summary>
        public void FitBackdrop()
        {
            if (_backdrop == null) return;

            // Sizing is read off the live camera rather than written out as constants. The camera
            // has moved three times in this session and every hardcoded backdrop number went
            // stale each time; taking orthographicSize, aspect and the rig's own rotation from
            // the Camera means this cannot drift again.
            //
            // Under orthographic a quad held perpendicular to the view direction covers exactly
            // the same frame at any distance: half-height is orthographicSize and half-width is
            // orthographicSize * aspect, with no perspective divide. Re-checked against the new
            // orthographicSize (3.65, down from 3.85) rather than assumed still correct: frame is
            // now 2 * 3.65 = 7.30 tall by 7.30 * 1.778 = 12.98 wide, both smaller.
            //
            // Margin: BattleStage.Shake moves the camera up to 0.18 world units in x and y
            // (BattleScreen asks for 0.14 on a crit and 0.18 on a burst), while the backdrop is
            // parented to the world and placed once, so it does not shake with the camera. A
            // world-x shake shifts the frame 0.18 across the quad; a world-y shake shifts it
            // 0.18 * cos(28) = 0.16. 0.30 per side covers both with room to spare.
            //   needed width  = 12.98 + 0.60 = 13.58
            //   needed height =  7.30 + 0.60 =  7.90
            //
            // Cover, not stretch: the quad is grown to the texture's own aspect ratio until it
            // covers both, so the painting is never squashed and the frame never sees past its
            // edge. ashen.png is 1672 x 941, aspect 1.7768 against the frame's 1.7778 - within
            // 0.05% of 16:9, so cropping, letterboxing and stretching are still nearly the same
            // choice here. Height is the exact fit this time (quadH = 7.90, no crop vertically);
            // width comes out to 7.90 * 1.7768 = 14.04, past the 13.58 needed, so the frame shows
            // the middle 97% of the painting horizontally. The backdrop therefore covers the frame
            // with 0.46 world units to spare on width, which is worth stating because it rules the
            // backdrop out as a source of anything that looks like a cut-off edge. Which axis
            // exactly-fits versus crops
            // changed from the three-rank pass (width fit exactly there) purely because the frame
            // shape changed, not because this logic did - it is still whichever axis the image's
            // own aspect ratio makes tightest against the frame's.
            //
            // The battle screen's top bar now takes the top of the screen out of the viewport, so
            // its frame is wider than 16:9 (12.98 / 7.30 becomes about 1.96 at 1920x1080 under a
            // 100-unit bar) and WIDTH is the exact fit there, with the painting cropped top and
            // bottom. Same rule, different frame; nothing here changed for it.
            float frameH = Camera.orthographicSize * 2f;
            float frameW = frameH * Camera.aspect;
            const float ShakeMargin = 0.30f;
            float needW = frameW + ShakeMargin * 2f;
            float needH = frameH + ShakeMargin * 2f;

            float imageAspect = _backdropAspect > 0f ? _backdropAspect : Camera.aspect;
            float quadH = needH;
            float quadW = quadH * imageAspect;
            if (quadW < needW)
            {
                quadW = needW;
                quadH = quadW / imageAspect;
            }

            // Placed along the camera's own forward axis, so it is centred in frame by
            // construction, and rotated to face straight back down that axis (the rig's rotation
            // turned 180 about its own up axis puts the quad's +Z front face against the view
            // direction). Distance re-checked against the new formation rather than left at its
            // old value: the back rank's far-z occupant (x 3.95, z 4.95) is now the deepest point,
            // forward depth 15.55 along the camera's view axis, down again now that the back
            // rank's z reach is +/-4.95 rather than +/-5.25. 22 clears that with margin
            // and sits well inside the 120 far clip.
            const float Distance = 22f;
            Vector3 forward = _cameraRig.localRotation * Vector3.forward;

            _backdrop.localPosition = _cameraHome + forward * Distance;
            _backdrop.localRotation = _cameraRig.localRotation * Quaternion.Euler(0f, 180f, 0f);
            _backdrop.localScale = new Vector3(quadW, quadH, 1f);
        }

        private static void Strip(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        // ------------------------------------------------------------------ units

        public void Populate(BattleResult result)
        {
            ClearActors();
            for (int i = 0; i < result.Units.Count; i++)
            {
                UnitSnapshot snapshot = result.Units[i];
                BattleFraming.SlotPoint slot = BattleFraming.Slot(snapshot.IsPlayer, snapshot.Slot);

                GameObject host = new GameObject((snapshot.IsPlayer ? "Ally_" : "Enemy_") + snapshot.Name);
                host.transform.SetParent(_world, false);
                UnitActor actor = host.AddComponent<UnitActor>();
                actor.Setup(snapshot, new Vector3(slot.X, 0f, slot.Z), snapshot.IsPlayer);
                actor.Director = Director;
                Actors.Add(actor);
            }
            if (Director != null) Director.Bind(result);
        }

        public UnitActor Find(int unitId)
        {
            for (int i = 0; i < Actors.Count; i++) if (Actors[i].Snapshot.Id == unitId) return Actors[i];
            return null;
        }

        public void ClearActors()
        {
            for (int i = 0; i < Actors.Count; i++) if (Actors[i] != null) Destroy(Actors[i].gameObject);
            Actors.Clear();
        }

        private void ClearWorld()
        {
            if (_world == null) return;
            for (int i = _world.childCount - 1; i >= 0; i--) Destroy(_world.GetChild(i).gameObject);
            Actors.Clear();
            _backdrop = null;
        }

        public void Teardown()
        {
            if (Vfx != null) Vfx.StopAll();
            if (Director != null) Director.Clear();
            ClearWorld();
        }

        // ------------------------------------------------------------------ camera

        public void Shake(float strength, float duration)
        {
            if (_shake != null) StopCoroutine(_shake);
            _shakeStrength = strength;
            _shakeUntil = Time.time + duration;
            _shake = StartCoroutine(ShakeRoutine(strength, duration));
        }

        /// <summary>
        /// A shake that never weakens one already running. The host shakes the camera on a crit
        /// itself, and a second call from the director a frame apart must not replace its 0.14
        /// with a smaller number, or cut its duration short.
        /// </summary>
        public void ShakeAtLeast(float strength, float duration)
        {
            if (_shake != null && _shakeStrength >= strength && _shakeUntil >= Time.time + duration) return;
            Shake(strength, duration);
        }

        private IEnumerator ShakeRoutine(float strength, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float falloff = 1f - Mathf.Clamp01(t / duration);
                float x = Mathf.Sin(t * 61f) * strength * falloff;
                float y = Mathf.Cos(t * 47f) * strength * falloff;
                _cameraRig.localPosition = _cameraHome + new Vector3(x, y, 0f);
                yield return null;
            }
            _cameraRig.localPosition = _cameraHome;
            _shake = null;
        }

        public Vector3 ScreenPoint(Vector3 worldPosition)
        {
            if (Camera == null) return Vector3.zero;
            return Camera.WorldToScreenPoint(worldPosition);
        }
    }
}
