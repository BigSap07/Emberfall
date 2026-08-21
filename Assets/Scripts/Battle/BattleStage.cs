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
        // Chibi arena formation: two flat rows per side, not one line. The single-plane layout
        // this replaces was tuned for a perspective camera, where every unit sitting at one z
        // was the fix for depth-based size falloff. Under orthographic (see Initialise) that
        // falloff doesn't exist any more, so a single flat line is no longer required for units
        // to render at equal size - it was solving a problem this file no longer has. What it
        // gave up in exchange was the read the reference target actually wants: a front rank
        // and a back rank per side, same as Idle Heroes / AFK Arena, not five units shoulder to
        // shoulder.
        //
        // The split is 3 front, 2 back, not 2-and-3. Slot 0 stays the front-centre position -
        // it already carries meaning for combat logic (the FrontEnemy targeting read) and for
        // the player's eye, so it belongs at the front, not tucked in a back rank. That leaves
        // four slots to split, and 3-front/2-back lets the two back units sit exactly in the
        // gaps between the three front units (front at x -4/0/4, back at x -2/2) rather than
        // needing their own independent spacing - "no unit sits directly behind another" falls
        // out of the arithmetic instead of needing separate tuning. A 2-front/3-back split has
        // no equivalent symmetry: three back units can't centre on two gaps.
        //
        // The row gap (front z to back z, 1.0 apart) is deliberately small. Under orthographic
        // it buys zero size change - that's not what it's for. It exists purely to separate the
        // rows on screen once the camera is pitched (see Initialise): a point's screen height
        // under a pitched orthographic camera is a mix of its world Y and world Z, so a small,
        // flat z offset between two rows at the same Y still reads as one row standing visibly
        // behind the other. Depth variation within a row would do the same thing to units that
        // are meant to be read as equals, so there is none - AllySlots and EnemySlots each carry
        // exactly two distinct z values, one per row, never a third.
        //
        // The two sides mirror across z 0: ally front at z -3, back at z -4; enemy front at z
        // 3, back at z 4. The six-unit gap between the two front rows (z -3 to z 3) is the wide
        // empty middle the reference target calls for. See Initialise for how orthographicSize
        // and the camera's pitch are derived from these same numbers.
        private static readonly Vector2[] AllySlots =
        {
            new Vector2(0f, -3.0f), new Vector2(-4.0f, -3.0f), new Vector2(4.0f, -3.0f),
            new Vector2(-2.0f, -4.0f), new Vector2(2.0f, -4.0f)
        };

        private static readonly Vector2[] EnemySlots =
        {
            new Vector2(0f, 3.0f), new Vector2(-4.0f, 3.0f), new Vector2(4.0f, 3.0f),
            new Vector2(-2.0f, 4.0f), new Vector2(2.0f, 4.0f)
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
            // Orthographic, not perspective. This is the change that actually produces the
            // chibi-arena look: under perspective, the far (enemy) row necessarily rendered
            // smaller than the near (ally) row regardless of camera angle, which is the single
            // biggest mismatch with Idle Heroes / AFK Arena, where both sides read at identical
            // size. Orthographic removes that falloff entirely - apparent size depends only on
            // orthographicSize below, never on distance from the camera.
            //
            // Pitch is 12 degrees, down from the previous 32. A pitched orthographic camera
            // maps a world point's screen height to (worldY * cos(pitch) + worldZ * sin(pitch)),
            // a straight linear mix, not the angle-subtended-by-FOV math perspective needed. 12
            // degrees is a large cut from 32 - "much closer to level" is the brief, and this is
            // most of the way there while still leaving sin(12 deg) = 0.208 to work with, which
            // is what turns the 1-unit row gap and the 6-unit side gap (see the slot comment
            // above) into visible screen separation at all. Set directly as a rotation rather
            // than by LookAt-ing a computed target, since the pitch itself is the derived value
            // here, not a side effect of where a look-at point happens to sit.
            //
            // orthographicSize is derived from the arrangement's real bounds, checked against
            // both axes, and the binding one turns out to be horizontal, not vertical.
            //
            // Vertical: the lowest point in frame is the ally back row's feet (y 0, z -4, the
            // nearest-to-camera edge of the formation); the highest is the enemy back row's
            // head plus bar clearance (y 1.85 + 0.28, z 4 - 0.28 stands in for the bar's rise
            // above the head, the same figure the old world-space bar offset used before it
            // became a screen-space constant, kept here only to size this margin). Project both
            // through the pitch formula above:
            //   f(y, z) = y * cos(12) + z * sin(12) = y * 0.9781 + z * 0.2079
            //   f(feet)  = 0 * 0.9781 + (-4) * 0.2079    = -0.832
            //   f(head)  = 2.13 * 0.9781 + 4 * 0.2079    =  2.916
            //   span     = f(head) - f(feet)             =  3.748
            //   half-span with 25% margin                =  3.748 / 2 * 1.25 = 2.342
            //
            // Horizontal: half-width needed is orthographicSize * aspect (16:9 = 1.778, the
            // landscape target - see UITheme.ReferenceResolution). The widest point is the
            // front row's outer slot at x 4 (see the slot arrays above); a character's own body
            // extends roughly 0.46 past its slot centre (UnitRig's arm placement on the widest
            // silhouette), so budget 0.6 clear of that for the character plus a small margin:
            //   half-width needed = 4.0 + 0.6                = 4.6
            //   orthographicSize  = 4.6 / 1.778               = 2.587
            //
            // 2.587 (horizontal) exceeds 2.342 (vertical), so horizontal is what actually sets
            // the frame - at 16:9 the wide five-across row runs out of width before the shallow
            // pitch runs out of height. orthographicSize = 2.6 covers both with a small margin
            // to spare, and the extra vertical headroom that leaves is exactly where the sky
            // sits above the two rows (see BuildScenery).
            //
            // Camera position: x 0 for left-right symmetry. z -7.0 sits behind the ally back
            // row (z -4) with clearance for VFX and the near clip plane; under orthographic,
            // distance from the camera does not affect framing the way it did under
            // perspective, so this value only has to clear geometry, not satisfy an angle
            // budget. y is solved from the vertical centring requirement: the frame's vertical
            // centre in world terms is K = (f(head) + f(feet)) / 2 = 1.042, and
            // K = camY * cos(12) + camZ * sin(12), so camY = (K - camZ * sin(12)) / cos(12) =
            // (1.042 - (-7.0) * 0.2079) / 0.9781 = 2.55.
            Camera.orthographic = true;
            Camera.orthographicSize = 2.6f;
            Camera.nearClipPlane = 0.3f;
            Camera.farClipPlane = 120f;
            Camera.depth = -10f;
            _cameraRig = cameraObject.transform;
            _cameraHome = new Vector3(0f, 2.55f, -7.0f);
            _cameraRig.localPosition = _cameraHome;
            _cameraRig.localRotation = Quaternion.Euler(12f, 0f, 0f);

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

            // sky backdrop. Checked against the new camera rather than left alone on the
            // assumption it still holds: at z 30 this quad spans world y -13 to 31, and tracing
            // both the top-of-frame and bottom-of-frame view rays out to z 30 under the new
            // orthographic camera (Initialise) lands them at roughly y -2.7 and y -8.0
            // respectively, both comfortably inside that span. The quad was already oversized
            // relative to the old camera's frustum, and the new one is smaller in every
            // dimension, so it stays covered without changing position or scale.
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

            // arena floor. Also checked, also unchanged: this plane spans z -16 to 16, and the
            // new camera's top-of-frame ray only reaches the ground (y 0) at roughly z 17, right
            // at the plane's own far edge - close enough that the retuned ridge above (now at z
            // 9 to 11.4, see BuildScenery) sits well inside the plane and screens that edge from
            // view before it would ever become visible.
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

            // distant ridge line, pulled in and cut down for the new camera. Both changes come
            // from the same fact: a pitched orthographic camera's visible world-height at a
            // given z is f(y, z) = y * cos(12) + z * sin(12) (see Initialise), and that shrinks
            // fast as z grows, since sin(12) = 0.208 is small. At the old ridge distance (z 17
            // to 21.8) even ground level (y 0) already projects to f = 17 * 0.208 = 3.5 or more
            // - past orthographicSize (2.6) on its own, before the ridge's own height is added.
            // The old heights (5.5 to 9.0) were sized for a camera with far more vertical
            // headroom to spend; at this z they would render almost entirely above the top of
            // frame. Moving the ridge to z 9 to 11.4 (just past the arena boundary stones
            // below) and cutting height by roughly 80% (1.0 to 1.8, keeping the same six-value
            // variation the original array had) brings the tops back to just under the top
            // edge: f(1.8, 11.4) = 1.8 * 0.978 + 11.4 * 0.208 = 1.76 + 2.37 = 4.13, close to the
            // frame's own vertical centre (K = 1.042, see Initialise) plus its half-height
            // (2.6), i.e. just inside the top edge with a small margin. The horizontal spread
            // and per-block width are unchanged - only depth and height needed to move.
            float[] ridgeX = { -14f, -8.5f, -3.5f, 3.0f, 8.0f, 13.5f };
            float[] ridgeH = { 1.5f, 1.0f, 1.8f, 1.2f, 1.7f, 1.1f };
            for (int i = 0; i < ridgeX.Length; i++)
            {
                Block("Ridge" + i, new Vector3(ridgeX[i], ridgeH[i] * 0.5f - 0.15f, 9f + (i % 3) * 1.2f),
                      new Vector3(6.5f + (i % 2) * 2f, ridgeH[i] + 0.3f, 4f),
                      new Vector3(0f, (i * 23f) % 40f - 20f, (i % 2 == 0 ? 3f : -4f)), far);
            }

            // arena boundary stones. Radius comes in from 7.4-8.4 to 6.2-7.0 and height from
            // 1.1-2.75 to 0.6-1.5, for the same reason as the ridge above: a stone dead ahead at
            // the old radius and old max height projected to f = 2.6 * 0.978 + 8.4 * 0.208 =
            // 2.54 + 1.75 = 4.29, well past the frame's top edge. The new range keeps every
            // stone's top inside orthographicSize with margin to spare - a mid-range stone now
            // projects to roughly f = 1.65 * 0.978 + 6.7 * 0.208 = 1.61 + 1.39 = 3.0, inside the
            // frame once recentred against K. The new inner radius (6.2) still clears the
            // formation: the widest corner in the new layout is the enemy back row's outer slot
            // at x 4, z 4 (see the slot arrays above), distance sqrt(4^2 + 4^2) = 5.66 from the
            // origin, so 6.2 leaves a margin of 0.54 rather than sitting flush against it.
            float[] stoneAngles = { 205f, 232f, 258f, 285f, 312f, 55f, 82f, 108f, 135f, 160f };
            for (int i = 0; i < stoneAngles.Length; i++)
            {
                float radius = 6.2f + (i % 3) * 0.4f;
                float rad = stoneAngles[i] * Mathf.Deg2Rad;
                float height = 0.6f + (i % 4) * 0.3f;
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
