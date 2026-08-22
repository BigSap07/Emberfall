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
        // Two ranks, not three: front 2 (slots 0, 1), back 3 (slots 2, 3, 4), replacing the
        // 1-2-2 taper. Method is unchanged from that pass - clear on x costs the sum of two
        // half-widths; clear on z costs H / sin(28) = H / 0.4695, since only 47% of a z offset
        // becomes screen height - but every dimension is recomputed, because two things moved
        // under it: rig dimensions now come from UnitRig.ThreeQuarterTurn's actual footprint
        // (turning a procedural silhouette widens it, turning a flat sprite quad narrows it,
        // since a quad has zero depth to rotate into the width axis - see UnitRig.BuildSprite),
        // and Wren Ilyra is a sprite now too, not the procedural humanoid+orb entry the last pass
        // measured her as.
        //
        // The tightest single requirement drove the shape: campaign.json puts the Sundered
        // Colossus (the tallest thing in the game) at slot 0 in every stage it appears, alongside
        // a Grave Moth at slot 1 - both front rank, both the same x, so that pair alone sets the
        // front rank's z spread (+/-4.10, gap 8.2), independent of anything about the back rank.
        //
        // These spacings were derived when UnitRig.Height was a nominal label rather than a
        // measured one, so every H below was inflated and every requirement with it. They are left
        // as they stand because the correction only ever adds clearance: the Colossus's real
        // rendered top is 2.945, not the 3.49 this was sized against, so the pair needs
        // Dz >= 2.945 / 0.4695 = 6.27 against the 8.20 it has, and the back rank's Stone Warden
        // needs 2.242 / 0.4695 = 4.78 against the 6.42 it has. Re-tightening the formation to the
        // true numbers would be a real change to the layout rather than a fix, so it is not made
        // here; what matters is that nothing is now under-spaced. Front-to-back x-step only has to clear the sum of two half-widths (about 2.29
        // worst case), a small number next to the z requirements above, so it added almost
        // nothing to orthographicSize - the ranks sit at x 2.4 and 4.9, a 2.5 step, well past what
        // clearing the x-axis alone needed, chosen instead to keep a genuine 2.2 world units of
        // clear ground between the two front ranks' own widest occupants (a deliberate margin,
        // not a byproduct - it cost nothing, since the vertical bound was already the binding one
        // before this margin was added, so widening it further didn't change orthographicSize).
        //
        // Checked exhaustively, as before: every slot subset a team of 1 to 5 can produce, every
        // authored enemy composition in campaign.json, every hero in every slot, at both 30 and 0
        // degrees of turn. Minimum screen-space separation is 0.359 world units (Stone Warden
        // against Hollow Husk, slots 3 and 4, at a full five-unit team); the tightest case at
        // smaller team sizes is the Colossus against a Grave Moth in slots 0 and 1, at 0.362.
        // Nothing overlaps at any team size.
        //
        // FrontEnemy (CombatSimulation.cs) targets whichever alive unit has the lowest Slot
        // index, nothing more - it is not aware of rank or position. With a single front slot
        // that was exactly "the physically front unit." With two, it reliably still targets a
        // front-rank unit as long as either survives (both are genuinely front), but of the two
        // it always prefers slot 0 over slot 1 while both are alive, an arbitrary tie-break
        // rather than anything about which is more exposed - they stand at the same depth and
        // read as equally exposed on screen. Once both front slots are dead it falls through to
        // whichever back-rank slot has the lowest index, the same tie-break one layer down.
        //
        // This is a real behaviour change, not a cosmetic one: a content author ordering an
        // enemy list in campaign.json, or a player choosing deploy order, is now implicitly
        // choosing which of two equally-front units draws single-target front fire, likely
        // without realising slot order carries that weight. Options, none taken here since all
        // of them touch CombatSimulation.cs (Core), which this pass does not touch:
        //   1. Leave it. Low stakes unless a specific hero or enemy identity depends on reliably
        //      drawing front-target fire (a designated tank), in which case that identity now
        //      only holds if it is placed in slot 0 specifically, not just "the front rank."
        //   2. Change the tie-break among units sharing the minimum rank (not raw slot index) to
        //      something that does not privilege one slot - lowest current HP, or random per
        //      resolution - so both front-rank units genuinely share exposure.
        //   3. Introduce an explicit rank concept FrontEnemy reads instead of inferring "front"
        //      from slot order at all, so the targeting rule and the visual layout stay in sync
        //      by construction rather than by the layout happening to put low slots first.
        // Reported so the choice is made deliberately, not decided here.
        private static readonly Vector2[] AllySlots =
        {
            new Vector2(-2.4f, -4.1f), new Vector2(-2.4f, 4.1f),
            new Vector2(-4.9f, -6.42f), new Vector2(-4.9f, 0f), new Vector2(-4.9f, 6.42f)
        };

        private static readonly Vector2[] EnemySlots =
        {
            new Vector2(2.4f, -4.1f), new Vector2(2.4f, 4.1f),
            new Vector2(4.9f, -6.42f), new Vector2(4.9f, 0f), new Vector2(4.9f, 6.42f)
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
            // orthographicSize and camera height re-derived AGAIN, because the previous pass had
            // two faults that both showed up in Play mode as units clipped by the frame edges.
            //
            // Fault 1, the big one: the bound was taken from UnitRig.Height, which at the time was
            // a nominal per-silhouette label that matched no rendered geometry (a Stone Warden's
            // declared 2.66 against a real 2.41 including its hammer, a golem's 2.25 against a
            // real 1.90). Height now means the measured top of the rendered silhouette, so the
            // bound can finally be taken from it honestly.
            //
            // Fault 2, the one that actually clipped: the lowest point in frame was taken as
            // "the back rank's outer feet, y 0", which ignored the blob shadow. BuildShadow lays a
            // 1.35 x 1.05 quad flat under every unit, and Root's three-quarter turn swings that
            // footprint out to 0.79 * scale of reach in z BEYOND the unit's own slot. At z -6.42
            // that put the shadow's near edge at f -3.42 against a frame bottom of -3.02: a
            // 0.40 world unit overhang, 53 pixels at 1080p, sliced off every frame. The feet
            // themselves sat at f -3.014 against that same -3.018 edge, a margin of 0.004, so the
            // idle bob (Body -0.035) pushed the unit itself over the line as well.
            //
            // Bounds now, over every hero in every slot and every enemy in the slots campaign.json
            // actually places it in, including the animation range rather than the rest pose only:
            //   highest  f  4.9194  Orrin Kaal, slot 4, mid-cast (PlayCast lifts Root +0.28 y)
            //   lowest   f -3.4157  Orrin Kaal, slot 2, shadow near edge
            //   span 8.3351, bare half-span 4.1675, centre 0.7518
            //
            // Horizontal: widest reach is a Stone Warden in the back rank's outer slot, 4.9 plus
            // its own shadow half-width 1.06 = 5.96. Add 0.5 of clear air:
            //   orthographicSize by width = (5.96 + 0.5) / 1.778 = 3.635
            //
            // Vertical binds, and by a wide margin (4.17 against 3.63), same as the last pass.
            //
            // Chosen: orthographicSize 4.25, leaving about 10 px of air at each edge at 1080p.
            // That is 4.05 -> 4.25, so procedural rigs render about 5% smaller than they did.
            // Deliberate and small next to the other half of this fix: sprite heroes were being
            // built to 1.85 against a humanoid rig's real 1.71, and now shrink about 8% on top of
            // this, which is the ratio that was making them read as the wrong size next to
            // everything else.
            //
            // Formation slots are NOT touched. The re-derivation only moved the camera, and the
            // slot spacing has more clearance than before rather than less: the z-gap rule is
            // Dz >= H / sin(28), and with H now the true rendered top instead of an inflated
            // label, every requirement shrank (the Colossus/Grave Moth pair that forced the front
            // rank's +/-4.10 now needs 6.27 rather than 7.43, against the 8.20 it has).
            //
            // Camera position: x 0, still symmetric. z -9.5 unchanged. y re-solved for the new
            // centre: camY = (0.7518 + 9.5*0.4695) / 0.8829 = 5.90, which lowers the aim by 0.32
            // and is what moves the slack from the top of the frame, where it was going unused, to
            // the bottom, where it was needed. Floors re-checked: camY (5.90) is still above
            // orthographicSize*cos(28) (3.75), and the nearest occupied point is 6.60 along the
            // view axis, well past the 0.3 near clip.
            Camera.orthographic = true;
            Camera.orthographicSize = 4.25f;
            Camera.nearClipPlane = 0.3f;
            Camera.farClipPlane = 120f;
            Camera.depth = -10f;
            _cameraRig = cameraObject.transform;
            _cameraHome = new Vector3(0f, 5.90f, -9.5f);
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
            // orthographicSize * aspect, with no perspective divide. Re-checked against the new
            // orthographicSize (4.25, up from 4.05) rather than assumed still correct: frame is
            // now 2 * 4.25 = 8.50 tall by 8.50 * 1.778 = 15.11 wide, both slightly larger.
            //
            // Margin: BattleStage.Shake moves the camera up to 0.18 world units in x and y
            // (BattleScreen asks for 0.14 on a crit and 0.18 on a burst), while the backdrop is
            // parented to the world and placed once, so it does not shake with the camera. A
            // world-x shake shifts the frame 0.18 across the quad; a world-y shake shifts it
            // 0.18 * cos(28) = 0.16. 0.30 per side covers both with room to spare.
            //   needed width  = 15.11 + 0.60 = 15.71
            //   needed height =  8.50 + 0.60 =  9.10
            //
            // Cover, not stretch: the quad is grown to the texture's own aspect ratio until it
            // covers both, so the painting is never squashed and the frame never sees past its
            // edge. ashen.png is 1672 x 941, aspect 1.7768 against the frame's 1.7778 - within
            // 0.05% of 16:9, so cropping, letterboxing and stretching are still nearly the same
            // choice here. Height is the exact fit this time (quadH = 9.10, no crop vertically);
            // width comes out to 9.10 * 1.7768 = 16.17, past the 15.71 needed, so the frame shows
            // the middle 97% of the painting horizontally. Which axis exactly-fits versus crops
            // changed from the three-rank pass (width fit exactly there) purely because the frame
            // shape changed, not because this logic did - it is still whichever axis the image's
            // own aspect ratio makes tightest against the frame's.
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
            // direction). Distance re-checked against the new formation rather than left at its
            // old value: the back rank's far-z occupant (x 4.9, z 6.42) is now the deepest point,
            // forward depth 16.83 along the camera's view axis - deeper than the old 11.4, since
            // the back rank's z reach grew from +/-3.25 to +/-6.42. 22 clears that with margin
            // and sits well inside the 120 far clip.
            const float Distance = 22f;
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
