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
        // Chibi arena formation: two flat rows per side, front 3 / back 2, unchanged from the
        // orthographic switch (see Initialise) - the split, the front-centre slot 0 targeting
        // read, and the x spread (front -4/0/4, back -2/2) all stay exactly as they were. Only
        // the row gap and the side gap changed, and both changed because the camera's pitch did
        // (see Initialise: 12 degrees was too shallow, corrected to 28).
        //
        // Row gap (front z to back z, same side) is 2.5, up from 1.0. The old 1.0 was real
        // ground, but barely more than a character's own footprint (units read roughly a unit
        // wide/deep), so even once separated on screen it read as crowding rather than a second
        // rank. 2.5 gives about 2.5x a character's footprint of clear ground between the ranks.
        //
        // Side gap (centre to front row) is 4.0, up from 3.0, so the two front rows sit 8.0
        // apart rather than 6.0 - proportionate to the 8-wide front rank (x -4 to 4) rather than
        // narrower than it.
        //
        // Both numbers are chosen for real, physical separation first; see Initialise for the
        // actual screen-space separation each one produces at the new pitch, computed rather
        // than assumed. Depth variation within a row would undo the "read as equals" job the
        // single shared z per row is doing, so there still isn't any - each array carries
        // exactly two distinct z values.
        private static readonly Vector2[] AllySlots =
        {
            new Vector2(0f, -4.0f), new Vector2(-4.0f, -4.0f), new Vector2(4.0f, -4.0f),
            new Vector2(-2.0f, -6.5f), new Vector2(2.0f, -6.5f)
        };

        private static readonly Vector2[] EnemySlots =
        {
            new Vector2(0f, 4.0f), new Vector2(-4.0f, 4.0f), new Vector2(4.0f, 4.0f),
            new Vector2(-2.0f, 6.5f), new Vector2(2.0f, 6.5f)
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
            // Orthographic, not perspective (unchanged, this part was right): apparent size
            // depends only on orthographicSize below, never on distance from the camera, which
            // is what lets the far (enemy) row read at the same size as the near (ally) row.
            //
            // Pitch is 28 degrees, corrected up from 12. 12 was a large cut from the old
            // perspective camera's 32 and it overshot: at that shallow an angle the camera sat
            // almost level, looking across the ground rather than down onto it, which is not
            // the reference target - Idle Heroes / AFK Arena hold a moderate downward angle
            // from well above, high enough to see the ground plane receding behind the
            // characters. 28 sits in the middle of that 25-30 degree range.
            //
            // A pitched orthographic camera maps a world point's screen height to
            // f(y, z) = y * cos(pitch) + z * sin(pitch), a straight linear mix (see the row/side
            // gap comment above the slot arrays for what this does to the two rows and the two
            // sides). Set directly as a rotation rather than by LookAt-ing a computed target,
            // since pitch is the derived value here, not a side effect of a look-at point.
            //
            // orthographicSize is derived from the arrangement's real bounds at this pitch, with
            // margin, checked against both axes.
            //
            // Vertical: lowest point in frame is the ally back row's feet (y 0, z -6.5, nearest
            // the camera); highest is the enemy back row's head plus headroom (y 1.85 + 0.28,
            // z 6.5).
            //   f(y, z) = y * cos(28) + z * sin(28) = y * 0.8829 + z * 0.4695
            //   f(feet) = 0 * 0.8829 + (-6.5) * 0.4695   = -3.052
            //   f(head) = 2.13 * 0.8829 + 6.5 * 0.4695   =  4.932
            //   span    = f(head) - f(feet)              =  7.984
            //   half-span with 30% margin                =  7.984 / 2 * 1.30 = 5.190
            //
            // Horizontal: half-width needed is orthographicSize * aspect (16:9 = 1.778). Widest
            // point is the front row's outer slot at x 4, plus the same 0.6 character-and-margin
            // clearance as before (the x spread didn't change):
            //   half-width needed = 4.0 + 0.6 = 4.6, orthographicSize = 4.6 / 1.778 = 2.587
            //
            // 5.190 (vertical) now exceeds 2.587 (horizontal) - the opposite of the old framing,
            // and expected: deepening the formation and steepening the pitch both grow the
            // vertical span, while the x spread, and so the horizontal requirement, never
            // changed. orthographicSize = 5.2 covers the binding vertical case with margin to
            // spare on the horizontal axis, and happens to land almost exactly on double the old
            // 2.6, which matches the brief without having been targeted directly.
            //
            // Screen separation the row gap and side gap actually produce at this pitch, stated
            // rather than assumed (full frame height = 2 * orthographicSize = 10.4):
            //   front-to-front (the two sides facing each other) = 2 * 4.0 * sin(28) = 3.756,
            //     3.756 / 10.4 = 36% of frame height of clear ground between the front ranks.
            //   front-to-back, one side (back rank standing behind) = 2.5 * sin(28) = 1.174,
            //     1.174 / 10.4 = 11% of frame height, visibly separated without reading as its
            //     own distant group.
            //
            // Camera position: x 0 for left-right symmetry. z -9.5 sits 3.0 behind the ally back
            // row (z -6.5), clearance for VFX and the near clip plane; under orthographic this
            // only has to clear geometry, not satisfy an angle budget. y is solved from the
            // vertical centring requirement: K = (f(head) + f(feet)) / 2 = 0.940, and
            // K = camY * cos(28) + camZ * sin(28), so camY = (K - camZ * sin(28)) / cos(28) =
            // (0.940 - (-9.5) * 0.4695) / 0.8829 = 6.12.
            Camera.orthographic = true;
            Camera.orthographicSize = 5.2f;
            Camera.nearClipPlane = 0.3f;
            Camera.farClipPlane = 120f;
            Camera.depth = -10f;
            _cameraRig = cameraObject.transform;
            _cameraHome = new Vector3(0f, 6.12f, -9.5f);
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
            // be at least 5.2 * 2 = 10.4 tall and 5.2 * 1.778 * 2 = 18.5 wide - sized to 12 / 22
            // here for margin. Positioned 30 units out along the camera's own forward direction
            // (0, -sin(28), cos(28)) from the camera at (0, 6.12, -9.5): (0, -7.97, 16.99). That
            // puts it comfortably behind the ridge below (which reaches to roughly z 12.2 at its
            // deepest), so the ridge silhouettes read against it rather than in front of it.
            GameObject sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sky.name = "Sky";
            Strip(sky);
            sky.transform.SetParent(_world, false);
            sky.transform.localPosition = new Vector3(0f, -7.97f, 16.99f);
            sky.transform.localScale = new Vector3(22f, 12f, 1f);
            sky.transform.localRotation = Quaternion.Euler(28f, 180f, 0f);
            MeshRenderer skyRenderer = sky.GetComponent<MeshRenderer>();
            skyRenderer.material = BattleAssets.NewFx(BattleAssets.SkyTexture(environmentId), Color.white, false);
            skyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // arena floor. Unlike the sky, this one is genuinely meant to be a real world-space
            // horizontal plane (units stand on it), so it stays axis-aligned and gets checked
            // with an actual ray-to-ground intersection instead of being turned into a billboard.
            // Tracing the frame's top edge ray (from the top of the frustum at the camera, along
            // the camera's forward direction) down to y 0 lands at z 13.08 - the farthest ground
            // the new camera can see - which is still inside this plane's own z -16 to 16 extent,
            // with the retuned ridge below sitting at z 7.8 to 12.2 and screening the plane's own
            // far edge from ever coming into view regardless. No change needed.
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

            // distant ridge line, re-checked against the new pitch and pulled in on both axes.
            //
            // Depth and height: f(y, z) = y * cos(28) + z * sin(28) (see Initialise). Targeting
            // the ridge tops at roughly 85% of the way to the top of frame (K + 0.85 * 5.2 =
            // 5.36, leaving clear sky above), the height budget at each of this array's three z
            // values works out to:
            //   z 7.8  (just past the enemy back row at z 6.5): (5.36 - 7.8*0.4695)/0.8829 = 1.92
            //   z 9.0:                                          (5.36 - 9.0*0.4695)/0.8829 = 1.29
            //   z 10.2:                                         (5.36 - 10.2*0.4695)/0.8829 = 0.65
            // Heights below sit under each of those with margin. The old array's z 9 to 11.4 and
            // heights 1.0 to 1.8 were sized against pitch 12; at 28 the same positions would
            // push the tops well past the top edge, since sin(28) = 0.470 versus sin(12) = 0.208
            // more than doubles how fast height near the camera's boresight, so height has to
            // come down to compensate.
            //
            // Horizontal: under orthographic the visible half-width is a flat orthographicSize *
            // aspect = 5.2 * 1.778 = 9.25 at any depth, not a growing frustum the way perspective
            // gave the old array room to place blocks at x -14 and 13.5 and still catch them in a
            // wide far-field FOV. Pulled the spread in to fit inside that fixed width instead.
            float[] ridgeX = { -9f, -5.5f, -2f, 2f, 5.5f, 9f };
            float[] ridgeH = { 1.4f, 1.0f, 0.5f, 1.3f, 0.9f, 0.45f };
            for (int i = 0; i < ridgeX.Length; i++)
            {
                Block("Ridge" + i, new Vector3(ridgeX[i], ridgeH[i] * 0.5f - 0.15f, 7.8f + (i % 3) * 1.2f),
                      new Vector3(6.5f + (i % 2) * 2f, ridgeH[i] + 0.3f, 4f),
                      new Vector3(0f, (i * 23f) % 40f - 20f, (i % 2 == 0 ? 3f : -4f)), far);
            }

            // The arena boundary stone ring is gone. It cannot be made to work as foreground
            // scenery under this projection with this formation depth: any radius wide enough to
            // clear the new back row's outer slot (x +/-2, z +/-6.5, distance 6.80 from the
            // origin) has to be at least that, with margin - but the camera sits at z -9.5 and
            // the ally back row at z -4 to -6.5, so a stone at radius 6.8+ near the angles
            // closest to the camera's heading (258 and 285 degrees, both close to due -Z) lands
            // at roughly z -7.6 to -7.8 - inside the 3-unit gap between the camera and the back
            // row, not behind the formation. That is exactly the "huge foreground object
            // standing between the camera and the characters" this was flagged for; it is a
            // structural mismatch between a wide ring and a camera that now sits close behind a
            // deep formation, not a tuning error, so there is no radius that fixes it. The ridge
            // above still gives the shot its depth and frame; the ring added nothing that
            // survives removing it.
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
