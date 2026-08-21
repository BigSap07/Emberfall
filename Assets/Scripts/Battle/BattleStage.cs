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
        // Landscape formation: wide and flat, not staggered in depth. The old V-shaped ranks
        // (front-centre closer, corners further back) were a portrait-era holdover - in
        // landscape, depth staggering costs the one thing there is least of: five units at
        // different z render at different screen heights and different apparent sizes, so a
        // side scatters vertically instead of reading as a row, and the taller back corners
        // push straight into the top UI band. Each side now sits on a single z plane and only
        // spreads on x, same as Idle Heroes / AFK Arena. Slot order still matters for combat
        // logic (slot 0 is the FrontEnemy targeting read), it just no longer maps to depth.
        //
        // The planes are not symmetric about z 0. At the flattened 5-unit-apart spacing the
        // two sides read as one crowd: the near (ally) row's own on-screen height already
        // reaches up to almost where the far (enemy) row's feet sit. Widening the gap has to
        // come mostly from pushing the enemy plane back rather than pulling the ally plane in,
        // because the ally plane's outer slot at bar height was already the tightest point in
        // the camera's frustum (see Initialise) - moving it closer eats that margin directly,
        // while the enemy plane had roughly 60% more frustum budget to spare than it needed.
        // The ceiling on how far back the enemy plane can go is the arena boundary ring in
        // BuildScenery: those stones sit at radius 7.4 to 8.4 from the origin, and the outer
        // enemy slot at x 4 is the corner closest to them, at distance sqrt(4^2 + z^2). Solving
        // sqrt(16 + z^2) = 7.4 puts the absolute limit at z = 6.23; z = 5.7 leaves about half a
        // stone's width of clearance under that (distance 6.96), which is as close as felt safe
        // to sit without units reading as planted on the boundary.
        private static readonly Vector2[] AllySlots =
        {
            new Vector2(0f, -2.5f), new Vector2(-2.0f, -2.5f), new Vector2(2.0f, -2.5f),
            new Vector2(-4.0f, -2.5f), new Vector2(4.0f, -2.5f)
        };

        private static readonly Vector2[] EnemySlots =
        {
            new Vector2(0f, 5.7f), new Vector2(-2.0f, 5.7f), new Vector2(2.0f, 5.7f),
            new Vector2(-4.0f, 5.7f), new Vector2(4.0f, 5.7f)
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
            // Framing re-derived from scratch against the separated planes (ally z -2.5, enemy
            // z 5.7, x +/-4 with the same +0.5/+0.5 character-width and clear-air margin as
            // before, so the framed edge is still x +/-5). Health bars no longer add to the
            // required vertical world extent - they are a fixed screen-space offset now (see
            // BattleScreen.cs), so the only vertical extent that matters here is the body: y 0
            // at the feet to y 1.85 at the head (UnitRig.Height), plus 0.2 of margin on each
            // end.
            //
            // Field of view stays at 42 - it already had headroom, the problem here was where
            // the camera looks, not how wide. Composition wants the ally row's feet and the
            // enemy row's head near the bottom and top of frame respectively, with even margin
            // on both sides rather than the old setup's near-quarter of the screen wasted as
            // pure foreground below the ally row. That is exactly two angles: the depression
            // angle from the camera down to the ally feet, and the (much shallower) depression
            // angle down to the enemy head-plus-margin. Aiming the camera exactly at the
            // midpoint of those two angles centres both extremes in frame by construction, and
            // checking the actual projected pixels confirms it lands within half a percent of
            // dead-centre on both sides: ally feet fall at roughly 18% up from the bottom edge,
            // enemy head clears the top by roughly the same 18%, so the two rows occupy the
            // middle third-plus of a 1080-tall frame instead of the top two-thirds.
            //
            // Camera distance still answers to the arena boundary stones (radius 7.4 to 8.4,
            // same ring as the slot spacing above) - z -9.2 clears them with a margin similar
            // to the slot spacing's, and the camera sits at x 0, which happens to fall in the
            // angular gap between the two stones nearest that heading anyway. With that
            // distance fixed, camera height is what has room left to move, and pushing it up to
            // y 7.0 is what buys the angular separation the composition needs - lower than that
            // and the ally-feet-to-enemy-head angle span undershoots what 42 degrees can use
            // well, higher and it barely gains more (the far plane's angle grows too, capping
            // the achievable spread). The tightest point for x-coverage is still the ally
            // plane's outer slot, still at the top of its margin rather than at the feet, for
            // the same reason as before - it needs about 37.4 degrees of the 42 available,
            // leaving about 13%, close to the 12% the previous framing carried. Pitch comes out
            // to about 32 degrees, close to the original pre-flatten V's 31 - not because
            // within-row depth scatter is back, each plane is still perfectly flat, but because
            // the gap between the two planes is now wider (8.2 total) than the old V's own
            // front-to-back spread (7.0) was, and that gap is what the pitch now has to cover.
            Camera.fieldOfView = 42f;
            Camera.nearClipPlane = 0.3f;
            Camera.farClipPlane = 120f;
            Camera.depth = -10f;
            _cameraRig = cameraObject.transform;
            _cameraHome = new Vector3(0f, 7.0f, -9.2f);
            _cameraRig.localPosition = _cameraHome;
            _cameraRig.LookAt(new Vector3(0f, 0.17f, 1.6f));

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

            // sky backdrop
            GameObject sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sky.name = "Sky";
            Strip(sky);
            sky.transform.SetParent(_world, false);
            sky.transform.localPosition = new Vector3(0f, 9f, 30f);
            sky.transform.localScale = new Vector3(90f, 44f, 1f);
            sky.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            MeshRenderer skyRenderer = sky.GetComponent<MeshRenderer>();
            skyRenderer.material = BattleAssets.NewFx(BattleAssets.SkyTexture(environmentId), Color.white, false);
            skyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // arena floor
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
            Material near = BattleAssets.NewLit(Color.Lerp(env.GroundNear, env.GroundFar, 0.4f), env.Accent * 0.3f, 0f);

            // distant ridge line
            float[] ridgeX = { -14f, -8.5f, -3.5f, 3.0f, 8.0f, 13.5f };
            float[] ridgeH = { 7.5f, 5.0f, 9.0f, 6.0f, 8.5f, 5.5f };
            for (int i = 0; i < ridgeX.Length; i++)
            {
                Block("Ridge" + i, new Vector3(ridgeX[i], ridgeH[i] * 0.5f - 1.2f, 17f + (i % 3) * 2.4f),
                      new Vector3(6.5f + (i % 2) * 2f, ridgeH[i], 4f),
                      new Vector3(0f, (i * 23f) % 40f - 20f, (i % 2 == 0 ? 3f : -4f)), far);
            }

            // arena boundary stones
            float[] stoneAngles = { 205f, 232f, 258f, 285f, 312f, 55f, 82f, 108f, 135f, 160f };
            for (int i = 0; i < stoneAngles.Length; i++)
            {
                float radius = 7.4f + (i % 3) * 0.5f;
                float rad = stoneAngles[i] * Mathf.Deg2Rad;
                float height = 1.1f + (i % 4) * 0.55f;
                Block("Stone" + i, new Vector3(Mathf.Cos(rad) * radius, height * 0.5f - 0.15f, Mathf.Sin(rad) * radius),
                      new Vector3(0.9f + (i % 3) * 0.35f, height, 0.9f),
                      new Vector3(0f, stoneAngles[i], (i % 2 == 0 ? 5f : -6f)), near);
            }
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
