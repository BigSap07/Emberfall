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
        private static readonly Vector2[] AllySlots =
        {
            new Vector2(0f, -2.5f), new Vector2(-2.0f, -2.5f), new Vector2(2.0f, -2.5f),
            new Vector2(-4.0f, -2.5f), new Vector2(4.0f, -2.5f)
        };

        private static readonly Vector2[] EnemySlots =
        {
            new Vector2(0f, 2.5f), new Vector2(-2.0f, 2.5f), new Vector2(2.0f, 2.5f),
            new Vector2(-4.0f, 2.5f), new Vector2(4.0f, 2.5f)
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
            // Framing re-derived for the flattened formation (fov 40 at y 5.8, z -7.4 was set
            // against the old staggered V and never checked against this spread - the outer
            // slots at x +/-4 fell outside the frustum). Worked from the actual geometry:
            // ally plane z -2.5, enemy plane z 2.5, x +/-4, plus ~0.5 for character half-width
            // and another 0.5 of clear air, so the framed edge is x +/-5. Vertically a unit
            // spans y 0 (feet) to y 1.85 (UnitRig.Height), and the tracked health bar sits at
            // HeadPosition + 0.28 (BattleScreen.cs), so the top of the chrome is y 2.13; add
            // 0.2 of headroom above that and 0.2 below the feet. With camera at (0, 4.6, -10.5)
            // looking at (0, 1.0, 0), the tightest point turns out to be the ally plane (nearer
            // to camera than the enemy plane) at its outer x, at bar height rather than at the
            // feet - being closer to the camera's own height shortens its distance along the
            // view axis more than being lower does. That point needs a vertical field of view
            // of ~37.4 degrees to stay inside frame; 42 leaves about 12% of headroom to spare
            // there, and far more on the enemy plane and everywhere else, since every other
            // point sits further from the camera along the view axis than that one does. Depth
            // staggering is gone, so the steep 31 degree pitch the old V needed to compress
            // itself vertically is gone too - this sits at roughly 19 degrees, the flattest
            // angle that still clears the arena boundary stones and keeps both planes read as
            // two distinct facing lines rather than one flat card.
            Camera.fieldOfView = 42f;
            Camera.nearClipPlane = 0.3f;
            Camera.farClipPlane = 120f;
            Camera.depth = -10f;
            _cameraRig = cameraObject.transform;
            _cameraHome = new Vector3(0f, 4.6f, -10.5f);
            _cameraRig.localPosition = _cameraHome;
            _cameraRig.LookAt(new Vector3(0f, 1.0f, 0f));

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
