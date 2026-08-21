using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Emberfall.Core.Combat;

namespace Emberfall.Battle
{
    /// <summary>
    /// The 3D battlefield: camera, sky, arena floor, scenery and the six unit actors.
    /// Built fresh for each battle and torn down afterwards.
    /// </summary>
    public sealed class BattleStage : MonoBehaviour
    {
        // Left-versus-right clusters, replacing the opposing-lines arrangement. Allies occupy
        // negative x, enemies occupy positive x, and the empty middle band that used to run
        // horizontally across the screen (the gap between a near row and a far row) now runs
        // vertically down the centre instead (the gap between the two x clusters). This is a
        // screen-space rearrangement only - IsPlayer still drives facing in UnitActor.Setup,
        // untouched here, so which way a unit turns to face the camera never changes.
        //
        // Each cluster's internal axes swap roles from the old design rather than staying the
        // same: x used to be the free/spread axis (three units across at one depth) and z
        // separated the two rows. Now x is spent separating the two sides and, within a side,
        // separating that side's own rows from the centreline outward, while z becomes the free
        // axis units within a row spread across. This matters for the occlusion argument below:
        // under this orthographic camera, screen-x is exactly world x (the right axis has no
        // pitch component) and screen-y is f(y, z) = y*cos(pitch) + z*sin(pitch) (see
        // Initialise). Two units only risk hiding one behind the other if BOTH their x and their
        // z are close at once - differing enough in either axis alone is sufficient separation.
        //
        // Five per side, not four: the reference this is drawn from shows four per side already
        // using most of the frame width in two rows of two. Five doesn't fit that shape without
        // either widening the frame past what a tight "cluster" (the word the brief uses, not
        // "line") should be, or thinning the middle gap to make room - so this goes to three
        // rows instead of two: 1 front, 2 middle, 2 back, tapering toward the centre. Slot 0
        // keeps the sole front position, preserving the FrontEnemy targeting read's meaning (the
        // single most-exposed, closest-to-the-enemy unit) even though it no longer sits at x 0.
        //
        // Row depth-from-centre steps 2.6 / 4.2 / 5.8 (a flat 1.6 apart, comfortably more than a
        // character's own ~1-unit footprint, so adjacent rows never sit close enough in x to
        // read as merged). The middle row spreads its two units to z +/-1.4 (2.8 apart); the
        // back row to z +/-0.9 (1.8 apart, deliberately narrower than the middle row so the grid
        // reads as offset rather than a rigid repeat, matching the "offset grid" the brief asks
        // for). Every same-row pair is separated by z alone; every cross-row pair is separated
        // by at least the 1.6 x-step alone; nothing needs both axes to be safe, but most pairs
        // get both anyway.
        //
        // The front row's depth (2.6) is chosen to make the middle gap wide on its own terms,
        // not incidentally: cluster width, edge to edge including a 0.5 character-silhouette
        // margin, is (5.8+0.5)-(2.6-0.5) = 4.2; setting the gap (2*(2.6-0.5) = 4.2) equal to that
        // gives a 1:1 ratio - the empty middle is exactly as wide as either army, not a
        // leftover strip next to a wide army.
        private static readonly Vector2[] AllySlots =
        {
            new Vector2(-2.6f, 0f), new Vector2(-4.2f, -1.4f), new Vector2(-4.2f, 1.4f),
            new Vector2(-5.8f, -0.9f), new Vector2(-5.8f, 0.9f)
        };

        private static readonly Vector2[] EnemySlots =
        {
            new Vector2(2.6f, 0f), new Vector2(4.2f, -1.4f), new Vector2(4.2f, 1.4f),
            new Vector2(5.8f, -0.9f), new Vector2(5.8f, 0.9f)
        };

        public Camera Camera;
        public BattleVfx Vfx;
        public readonly List<UnitActor> Actors = new List<UnitActor>();

        private Transform _world;
        private Transform _cameraRig;
        private Vector3 _cameraHome;
        private Coroutine _shake;

        public void Initialise()
        {
            _world = new GameObject("World").transform;
            _world.SetParent(transform, false);

            GameObject cameraObject = new GameObject("BattleCamera");
            cameraObject.transform.SetParent(transform, false);
            Camera = cameraObject.AddComponent<Camera>();
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = new Color(0.04f, 0.03f, 0.07f, 1f);
            // Orthographic (unchanged) at 28 degrees of pitch (unchanged, held fixed across this
            // pass by instruction): apparent size still depends only on orthographicSize, never
            // distance, and f(y, z) = y*cos(28) + z*sin(28) = y*0.8829 + z*0.4695 is still the
            // formula mapping a world point to screen height (see the slot comment above for
            // what it implies about occlusion in this new arrangement).
            //
            // orthographicSize re-derived from scratch against the cluster bounds, checked on
            // both axes exactly as before, because the shape of the arrangement inverted: it
            // used to be wide and shallow (five across, two rows deep in z), now it is narrow in
            // z but wide in x (two clusters pushed apart left-right).
            //
            // Vertical: with the row-gap concept gone, the only z values in play are the
            // within-row spreads, +/-1.4 at most (the middle row). Lowest point in frame is any
            // unit's feet at that outer z; highest is the same z with a unit's head plus
            // headroom (y 1.85 + 0.28).
            //   f(feet) = 0 * 0.8829 + (-1.4) * 0.4695   = -0.657
            //   f(head) = 2.13 * 0.8829 + 1.4 * 0.4695   =  2.538
            //   span    = f(head) - f(feet)              =  3.195
            //   half-span with 25% margin                =  3.195 / 2 * 1.25 = 1.997
            //
            // Horizontal: half-width needed is the back row's depth (5.8) plus the same 0.5
            // character-silhouette clearance used to size the middle gap above, with the same
            // 25% margin applied for comfort rather than left as a bare fit:
            //   half-width needed = (5.8 + 0.5) * 1.25 = 7.875, orthographicSize = 7.875 / 1.778
            //                     = 4.430
            //
            // 4.430 (horizontal) now exceeds 1.997 (vertical) - it flipped back, as expected:
            // pushing the two clusters apart in x grew the horizontal requirement far more than
            // collapsing the row-gap concept into a shallow +/-1.4 z-spread grew the vertical
            // one. orthographicSize = 4.4 covers the binding horizontal case; this is smaller
            // than the previous pass's 5.2 despite the wider arrangement, because that 5.2 was
            // set by a vertical requirement this arrangement no longer has - the frame is wider
            // and shorter, exactly as anticipated, and characters read somewhat larger as a
            // result of the shorter half-height.
            //
            // Camera position: x 0, still symmetric between the two clusters. z -6.5 clears the
            // formation's now-shallow z-spread (+/-1.4) with real margin, much closer than the
            // previous -9.5 since there is no deep back row to clear any more; -5.0 was tried
            // first and rejected because it put camY below orthographicSize * cos(28), which
            // pushes the bottom of frame below world Y 0 at the camera's own reference point -
            // -6.5 keeps camY comfortably above that floor. y is solved the same way as before:
            // K = (f(head) + f(feet)) / 2 = 0.940, camY = (K - camZ*sin(28)) / cos(28) =
            // (0.940 - (-6.5)*0.4695) / 0.8829 = 4.52.
            Camera.orthographic = true;
            Camera.orthographicSize = 4.4f;
            Camera.nearClipPlane = 0.3f;
            Camera.farClipPlane = 120f;
            Camera.depth = -10f;
            _cameraRig = cameraObject.transform;
            _cameraHome = new Vector3(0f, 4.52f, -6.5f);
            _cameraRig.localPosition = _cameraHome;
            _cameraRig.localRotation = Quaternion.Euler(28f, 0f, 0f);

            GameObject vfxHost = new GameObject("Vfx");
            vfxHost.transform.SetParent(transform, false);
            Vfx = vfxHost.AddComponent<BattleVfx>();
            Vfx.Cam = Camera;
        }

        // ------------------------------------------------------------------ world

        public void BuildWorld(string environmentId)
        {
            ClearWorld();
            BattleAssets.Environment env = BattleAssets.GetEnvironment(environmentId);
            Camera.backgroundColor = env.SkyTop;

            // sky backdrop. A flat quad fixed at a constant world Z, checked against a ray
            // traced out to that Z, is the wrong model once the camera has real pitch: under
            // orthographic every screen edge traces a ray in the camera's own forward direction,
            // and those rays are parallel, not converging, so a plane that isn't perpendicular
            // to them is intersected at a shifted, skewed position that has to be re-verified
            // by hand every time the pitch changes - which is exactly what caught the old
            // version out here. Rebuilt instead as a billboard that faces the camera directly.
            // Its rotation matches the camera's own pitch (Euler(28, 180, 0): the existing 180
            // Y-flip so its front face points back toward the camera, plus the same 28 degree
            // X-tilt the camera itself uses, which is the rotation that turns the quad's normal
            // into exactly the negative of the camera's forward vector). Once the quad's plane
            // is perpendicular to the view direction, orthographic guarantees a distortion-free
            // fit: the frame's extent is exactly orthographicSize vertically and
            // orthographicSize * aspect horizontally at any distance, so the quad only needs to
            // be at least 4.4 * 2 = 8.8 tall and 4.4 * 1.778 * 2 = 15.6 wide - sized to 10 / 19
            // here for margin. The pitch in the rotation and the direction vector below both
            // still read 28 - only the distance and the resulting position changed. Positioned
            // 22 units out along the camera's own forward direction (0, -sin(28), cos(28)) from
            // the camera at (0, 4.52, -6.5): (0, -5.81, 12.92). That puts it comfortably behind
            // the ridge below (which reaches to roughly z 9.9 at its deepest), so the ridge
            // silhouettes read against it rather than in front of it.
            GameObject sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sky.name = "Sky";
            Strip(sky);
            sky.transform.SetParent(_world, false);
            sky.transform.localPosition = new Vector3(0f, -5.81f, 12.92f);
            sky.transform.localScale = new Vector3(19f, 10f, 1f);
            sky.transform.localRotation = Quaternion.Euler(28f, 180f, 0f);
            MeshRenderer skyRenderer = sky.GetComponent<MeshRenderer>();
            skyRenderer.material = BattleAssets.NewFx(BattleAssets.SkyTexture(environmentId), Color.white, false);
            skyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // arena floor. Unlike the sky, this one is genuinely meant to be a real world-space
            // horizontal plane (units stand on it), so it stays axis-aligned and gets checked
            // with an actual ray-to-ground intersection instead of being turned into a billboard.
            // Re-checked against the new, much closer camera: the nearest ground visible is
            // bounded by the bottom of the vertical frustum, not by the near clip plane - the
            // frustum edge cuts in at z -7.37, while near clip alone would only cut in at
            // z -8.56, so the frustum is what actually limits visibility, and it does so well
            // short of ever reaching the near clip plane. The top-of-frame ray reaches as far as
            // z 11.38. Both ends sit well inside this plane's own z -16 to 16 extent. No change
            // needed.
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            Strip(ground);
            ground.transform.SetParent(_world, false);
            ground.transform.localPosition = Vector3.zero;
            ground.transform.localScale = new Vector3(3.2f, 1f, 3.2f);
            MeshRenderer groundRenderer = ground.GetComponent<MeshRenderer>();
            Material groundMaterial = BattleAssets.NewLit(Color.white, env.Accent * 0.35f, 0f);
            groundMaterial.SetTexture("_MainTex", BattleAssets.GroundTexture(environmentId));
            groundMaterial.SetFloat("_RimPower", 6f);
            groundRenderer.material = groundMaterial;
            groundRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            BuildScenery(env);
        }

        /// <summary>Silhouetted rocks and pillars that frame the arena and give the shot depth.</summary>
        private void BuildScenery(BattleAssets.Environment env)
        {
            Material far = BattleAssets.NewLit(env.GroundFar, env.Accent * 0.25f, 0f);

            // distant ridge line, re-checked and re-fit again: the frame got wider and shorter
            // this pass (orthographicSize 5.2 to 4.4, and horizontal is now the binding axis, see
            // Initialise), the opposite shape of change from last time, so both axes move again.
            //
            // Depth and height: f(y, z) = y * cos(28) + z * sin(28), pitch unchanged. Targeting
            // the ridge tops at 85% of the way to the top of frame (K + 0.85 * 4.4 = 4.68), the
            // height budget at each of this array's three z values:
            //   z 5.5:  (4.68 - 5.5*0.4695)/0.8829 = 2.38
            //   z 6.7:  (4.68 - 6.7*0.4695)/0.8829 = 1.74
            //   z 7.9:  (4.68 - 7.9*0.4695)/0.8829 = 1.10
            // Pulled the whole ridge closer (base z 7.8 to 5.5) because the formation it needs to
            // sit behind is now shallow (max z +/-1.4, not +/-6.5), so "just past the formation"
            // is a much smaller z than before; heights raised back up somewhat versus last pass
            // since the closer position gives more headroom to spend before the horizon.
            //
            // Horizontal: visible half-width is now orthographicSize * aspect = 4.4 * 1.778 =
            // 7.82, narrower than last pass's 9.25, so the spread pulls in slightly to -7.5/7.5.
            float[] ridgeX = { -7.5f, -4.5f, -1.5f, 1.5f, 4.5f, 7.5f };
            float[] ridgeH = { 1.9f, 1.4f, 0.8f, 1.8f, 1.3f, 0.75f };
            for (int i = 0; i < ridgeX.Length; i++)
            {
                Block("Ridge" + i, new Vector3(ridgeX[i], ridgeH[i] * 0.5f - 0.15f, 5.5f + (i % 3) * 1.2f),
                      new Vector3(6.5f + (i % 2) * 2f, ridgeH[i] + 0.3f, 4f),
                      new Vector3(0f, (i * 23f) % 40f - 20f, (i % 2 == 0 ? 3f : -4f)), far);
            }

            // The arena boundary stone ring stays removed, by instruction - it was cut last pass
            // because no radius cleared the formation without also sitting in the camera's
            // foreground, and this pass's formation, while shallower in z, is wider in x, so
            // nothing here reopened the case for bringing it back. The ridge above still gives
            // the shot its depth and frame on its own.
        }

        private Transform Block(string name, Vector3 position, Vector3 scale, Vector3 euler, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Strip(go);
            go.transform.SetParent(_world, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler.x, euler.y, euler.z);
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
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
                Vector2 slot = snapshot.IsPlayer
                    ? AllySlots[Mathf.Clamp(snapshot.Slot, 0, AllySlots.Length - 1)]
                    : EnemySlots[Mathf.Clamp(snapshot.Slot, 0, EnemySlots.Length - 1)];

                GameObject host = new GameObject((snapshot.IsPlayer ? "Ally_" : "Enemy_") + snapshot.Name);
                host.transform.SetParent(_world, false);
                UnitActor actor = host.AddComponent<UnitActor>();
                actor.Setup(snapshot, new Vector3(slot.x, 0f, slot.y), snapshot.IsPlayer);
                Actors.Add(actor);
            }
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
        }

        public void Teardown()
        {
            if (Vfx != null) Vfx.StopAll();
            ClearWorld();
        }

        // ------------------------------------------------------------------ camera

        public void Shake(float strength, float duration)
        {
            if (_shake != null) StopCoroutine(_shake);
            _shake = StartCoroutine(ShakeRoutine(strength, duration));
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
