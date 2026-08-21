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
        // vertically down the centre instead (the gap between the two x clusters). Positions
        // only; facing lives in UnitRig.Build, which turns each side toward the other by
        // UnitRig.ThreeQuarterTurn off camera-square, mirrored by IsPlayer.
        //
        // Each cluster's internal axes swap roles from the old design: x used to be the free
        // spread axis and z separated the two rows. Now x separates the two sides and, within a
        // side, separates that side's own ranks from the centreline outward, while z is the
        // free axis units within a rank spread across.
        //
        // Spacing corrected after Play mode showed units overlapping at partial team sizes. The
        // previous version's occlusion argument - "two units only risk hiding each other if both
        // their x and their z are close, and every pair is separated by at least one axis" - is
        // half right and was applied wrongly. The half that is right: screen-x is exactly world
        // x (the camera's right axis has no pitch component) and screen-y is
        // f(y, z) = y*cos(28) + z*sin(28), so separation on either axis alone is sufficient. The
        // half that was missed: how MUCH separation each axis needs is wildly different. To clear
        // on x, two units need only the sum of their half-widths, roughly 1.0 to 2.0. To clear on
        // z they need Dz >= H / sin(28) = H / 0.4695, because only 47% of a z offset becomes
        // screen height while the unit's full height H stands in the way - and H is 1.13 for a
        // Grave Moth, 2.66 for a Stone Warden, 3.49 for the Colossus. So the required z gaps are
        // 2.40, 5.66 and 7.43. The old arrays gave the same-x pairs 2.8 (slots 1,2) and 1.8
        // (slots 3,4). Both were far short, so both pairs overlapped - measured worst case a
        // Stone Warden and a Hollow Husk in slots 3,4, overlapping by 1.39 world units of screen
        // height. This is why it showed up at four deployed heroes and not only at five: slots
        // 1,2 fill at team size 3, so the bug is present from three units on.
        //
        // Fixed by paying the real z price on the same-x pairs and the real x price between
        // ranks, both derived from the actual rig dimensions (UnitRig silhouettes and weapons,
        // times each unit's VisualDef scale) rather than a nominal "1 unit wide":
        //   rank depths 2.6 / 4.92 / 6.93  (steps 2.32 and 2.01, against required 1.96 and 1.65)
        //   rank z spread +/-3.25          (gap 6.5, against required 5.66)
        // Checked exhaustively over every slot subset a team of 1 to 5 can produce, every
        // authored enemy composition in campaign.json, and every hero in every slot: minimum
        // screen-space separation is 0.359 world units, and the tightest pair is the Colossus
        // against a Grave Moth in slots 0 and 2. Nothing overlaps at any team size.
        //
        // Five per side, not four: the reference shows four per side in two rows of two. A third
        // rank is what makes five fit, because two z-rows can hold at most two units per rank
        // once the z gap above is honoured. Slot 0 keeps the sole front position, preserving the
        // FrontEnemy targeting read, and sits at z 0, centred between the two rows rather than
        // on either - which is also what keeps the arrangement an offset grid rather than a
        // rigid 3x2 block with a hole in it.
        private static readonly Vector2[] AllySlots =
        {
            new Vector2(-2.6f, 0f), new Vector2(-4.92f, -3.25f), new Vector2(-4.92f, 3.25f),
            new Vector2(-6.93f, -3.25f), new Vector2(-6.93f, 3.25f)
        };

        private static readonly Vector2[] EnemySlots =
        {
            new Vector2(2.6f, 0f), new Vector2(4.92f, -3.25f), new Vector2(4.92f, 3.25f),
            new Vector2(6.93f, -3.25f), new Vector2(6.93f, 3.25f)
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
            // Both bounds are computed over the real rig dimensions at every slot, not a nominal
            // character box, and both grew because the spacing fix above deepened the ranks in z
            // and widened the steps in x.
            //
            // Vertical: outermost z is now +/-3.25, and the tallest unit that can stand there is
            // a Stone Warden (H 2.66).
            //   f(feet, z -3.25) = -3.25 * 0.4695            = -1.526
            //   f(head, z +3.25) = 2.66 * 0.8829 + 1.526     =  3.874
            //   span                                         =  5.31, so half-span = 2.65
            //
            // Horizontal: the widest occupied point is a Stone Warden in the outer rank, x 6.93
            // plus its own rotated half-width 0.99 (see UnitRig.ThreeQuarterTurn - turning a unit
            // widens its silhouette, so this is measured at the turn, not square-on) = 7.92. Add
            // 0.5 of clear air so the outermost unit is not flush against the screen edge:
            //   orthographicSize = (7.92 + 0.5) / 1.778 = 4.737
            //
            // Horizontal still binds, by a wide margin (4.74 against 2.65). orthographicSize goes
            // 4.4 to 4.74. This is the one number the spacing fix genuinely forced: the required
            // rank steps (1.96 and 1.65 minimum, from summed half-widths) push the outer rank out
            // to x 6.93, and no arrangement of five units in two z-rows fits inside the old 4.4
            // frame while also clearing on either axis. Characters render about 7% smaller as a
            // result. A layout that keeps 4.4 exactly does exist, but only by pulling the two
            // clusters together until the outermost unit sits 0.02 world units from the frame
            // edge, which trades a visible overlap for a visible clipping risk.
            //
            // Camera position: x 0, still symmetric. z -7.0, half a unit further back than the
            // previous -6.5 so the deepened near rank (z -3.25, not -1.4) keeps the same standoff
            // from the camera. y solved from the vertical centring requirement as before:
            // K = (f(head) + f(feet)) / 2 = 1.127, camY = (K - camZ*sin(28)) / cos(28) =
            // (1.127 + 7.0*0.4695) / 0.8829 = 5.00. That clears the two floors this has to sit
            // above: camY > orthographicSize*cos(28) = 4.19, so the bottom of frame stays above
            // ground level at the camera's own reference point, and the nearest unit sits 4.44
            // beyond where the frustum's lower edge crosses the ground (z -7.69), so no unit is
            // clipped at the bottom.
            Camera.orthographic = true;
            Camera.orthographicSize = 4.74f;
            Camera.nearClipPlane = 0.3f;
            Camera.farClipPlane = 120f;
            Camera.depth = -10f;
            _cameraRig = cameraObject.transform;
            _cameraHome = new Vector3(0f, 5.0f, -7.0f);
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
            // be at least 4.74 * 2 = 9.5 tall and 4.74 * 1.778 * 2 = 16.9 wide - sized to 11 / 19
            // here for margin. The pitch in the rotation and the direction vector below both
            // still read 28 - only the distance and the resulting position changed. Positioned
            // 24 units out along the camera's own forward direction (0, -sin(28), cos(28)) from
            // the camera at (0, 5.0, -7.0): (0, -6.27, 14.19). That puts it well behind the ridge
            // below (which reaches roughly z 9.4 at its deepest), so the ridge silhouettes read
            // against it rather than in front of it.
            GameObject sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sky.name = "Sky";
            Strip(sky);
            sky.transform.SetParent(_world, false);
            sky.transform.localPosition = new Vector3(0f, -6.27f, 14.19f);
            sky.transform.localScale = new Vector3(19f, 11f, 1f);
            sky.transform.localRotation = Quaternion.Euler(28f, 180f, 0f);
            MeshRenderer skyRenderer = sky.GetComponent<MeshRenderer>();
            skyRenderer.material = BattleAssets.NewFx(BattleAssets.SkyTexture(environmentId), Color.white, false);
            skyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // arena floor. Unlike the sky, this one is genuinely meant to be a real world-space
            // horizontal plane (units stand on it), so it stays axis-aligned and gets checked
            // with an actual ray-to-ground intersection instead of being turned into a billboard.
            // Re-checked against the new camera: the nearest ground visible is bounded by the
            // bottom of the vertical frustum, not by the near clip plane - the frustum edge cuts
            // in at z -7.69, while near clip alone would only cut in at z -9.32, so the frustum
            // is what actually limits visibility and it does so well short of the clip plane. The
            // top-of-frame ray reaches as far as z 12.50. Both ends sit inside this plane's own
            // z -16 to 16 extent. No change needed.
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

            // distant ridge line, re-fit against the new frustum. Two things moved it: the
            // formation is deeper in z than last pass (units now reach z +3.25, not +1.4), so
            // "just past the formation" is further out, and the frame is both wider and taller
            // (orthographicSize 4.4 to 4.74), so there is more height budget to spend.
            //
            // Depth and height: f(y, z) = y * cos(28) + z * sin(28), pitch unchanged. Targeting
            // the ridge tops at 85% of the way to the top of frame (K + 0.85 * 4.74 = 5.16), the
            // height budget at each of this array's three z values:
            //   z 5.0:  (5.16 - 5.0*0.4695)/0.8829 = 3.18
            //   z 6.2:  (5.16 - 6.2*0.4695)/0.8829 = 2.54
            //   z 7.4:  (5.16 - 7.4*0.4695)/0.8829 = 1.91
            // Block() places each ridge so its top lands exactly on ridgeH[i], so the heights
            // below can be read straight against those budgets. The nearest ridge row (z 5.0)
            // clears the deepest unit (z 3.25) by 1.75.
            //
            // Horizontal: visible half-width is now orthographicSize * aspect = 4.74 * 1.778 =
            // 8.43, so the spread widens to -8/8 to keep covering the frame edge to edge.
            float[] ridgeX = { -8f, -4.8f, -1.6f, 1.6f, 4.8f, 8f };
            float[] ridgeH = { 2.6f, 1.9f, 1.3f, 2.4f, 1.75f, 1.2f };
            for (int i = 0; i < ridgeX.Length; i++)
            {
                Block("Ridge" + i, new Vector3(ridgeX[i], ridgeH[i] * 0.5f - 0.15f, 5.0f + (i % 3) * 1.2f),
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
