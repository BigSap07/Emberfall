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

            // Sizing is read off the live camera rather than written out as constants. The camera
            // has moved three times in this session and every hardcoded backdrop number went
            // stale each time; taking orthographicSize, aspect and the rig's own rotation from
            // the Camera means this cannot drift again.
            //
            // Under orthographic a quad held perpendicular to the view direction covers exactly
            // the same frame at any distance: half-height is orthographicSize and half-width is
            // orthographicSize * aspect, with no perspective divide. At the current values that
            // is a frame of 2 * 4.74 = 9.48 tall by 2 * 4.74 * 1.778 = 16.85 wide.
            //
            // Margin: BattleStage.Shake moves the camera up to 0.18 world units in x and y
            // (BattleScreen asks for 0.14 on a crit and 0.18 on a burst), while the backdrop is
            // parented to the world and placed once, so it does not shake with the camera. A
            // world-x shake shifts the frame 0.18 across the quad; a world-y shake shifts it
            // 0.18 * cos(28) = 0.16. 0.30 per side covers both with room to spare.
            //   needed width  = 16.85 + 0.60 = 17.45
            //   needed height =  9.48 + 0.60 = 10.08
            //
            // Cover, not stretch: the quad is grown to the texture's own aspect ratio until it
            // covers both, so the painting is never squashed and the frame never sees past its
            // edge. battle_ash_marches.png is 1672 x 941, aspect 1.7768 against the frame's
            // 1.7778 - within 0.05% of 16:9, so the choice between cropping, letterboxing and
            // stretching is very nearly moot here. Cover is still the right rule for whatever
            // arrives next: height binds at this aspect, giving 10.08 x 17.91, and the frame
            // shows the middle 94% of the painting in each direction. The 6% lost is the price
            // of the shake margin, not of the aspect mismatch, which costs 0.05%.
            float frameH = Camera.orthographicSize * 2f;
            float frameW = frameH * Camera.aspect;
            const float ShakeMargin = 0.30f;
            float needW = frameW + ShakeMargin * 2f;
            float needH = frameH + ShakeMargin * 2f;

            float imageAspect = painting.height > 0 ? painting.width / (float)painting.height : Camera.aspect;
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
            // direction). 20 units out clears the deepest unit, whose feet sit 11.4 along that
            // axis, and sits well inside the 120 far clip.
            const float Distance = 20f;
            Vector3 forward = _cameraRig.localRotation * Vector3.forward;

            GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "Backdrop";
            Strip(backdrop);
            backdrop.transform.SetParent(_world, false);
            backdrop.transform.localPosition = _cameraHome + forward * Distance;
            backdrop.transform.localRotation = _cameraRig.localRotation * Quaternion.Euler(0f, 180f, 0f);
            backdrop.transform.localScale = new Vector3(quadW, quadH, 1f);
            MeshRenderer backdropRenderer = backdrop.GetComponent<MeshRenderer>();
            backdropRenderer.material = BattleAssets.NewFx(painting, Color.white, false);
            backdropRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            backdropRenderer.receiveShadows = false;
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
