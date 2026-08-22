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
        // Two ranks: front 2 (slots 0, 1), back 3 (slots 2, 3, 4).
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
        // Tightened again: front x 1.60 -> 1.40, back x 4.25 -> 3.95, front z +/-3.45 -> +/-3.25,
        // back z +/-5.25 -> +/-4.95. Cumulatively from where this started, front x 2.40 -> 1.40
        // and back z +/-6.42 -> +/-4.95.
        //
        // CAMERA PITCH WAS TESTED AS A LEVER FOR CHARACTER SIZE AND IT IS NOT ONE. The reasoning
        // that it should be is sound as far as it goes: a z gap costs H / tan(pitch), which falls
        // 24% between 28 and 35 degrees, on exactly the axis that binds. But the saving does not
        // survive projection. The z spread needed is proportional to H / tan(p), and what it
        // contributes to the VERTICAL FRAME is that spread times sin(p):
        //     (H / tan(p)) * sin(p) = H * cos(p)
        // and a character's own apparent height is also H * cos(p), since a vertical billboard
        // foreshortens by the cosine. Every term in the vertical budget carries the same cos(p),
        // so orthographicSize falls in exact proportion to how much shorter the character gets,
        // and apparent size is invariant. Pitch buys nothing by construction, not by coincidence.
        //
        // Measured, holding minimum separation fixed at 0.373 and re-solving the layout at each
        // pitch, character height in pixels at 1080p:
        //     24 deg 215.5   28 deg 213.0   32 deg 210.1   35 deg 207.9   40 deg 203.6   45 deg 198.7
        // Steeper is monotonically WORSE, and 35 degrees would have cost 2.4%. The residual slope
        // is the blob shadow: it lies flat, so its projected depth grows with sin(p) while
        // everything else shrinks with cos(p). Pitch stays at 28, which is also the shallow end of
        // the range that still reads as a battle line rather than a map. For the record the point
        // where it stops reading as a line is somewhere near 45, where a depth step and a height
        // step project equally (tan = 1) and rank separation stops being distinguishable from
        // height, but there is no reason to go looking for it: the direction is unprofitable from
        // the first degree.
        //
        // What did buy size, since pitch could not:
        //   the minimum separation target, 0.373 -> 0.232 world units (33 px at 1080p, still
        //     clearly a gap), which let the whole z spread come in;
        //   the blob shadow, 1.35 x 1.05 -> 1.10 x 0.85 (see UnitRig.ShadowWidth), which was
        //     wider than any body it sat under and was 5.5% of the vertical budget on its own.
        // Together those are orthographicSize 3.85 -> 3.65, characters 5% larger. That is the
        // honest ceiling for this content: the vertical budget at the shipped layout is 65% back
        // rank z spread, 22% the tallest hero, 5.5% shadow, 4.2% shake reserve, 2.8% cast reserve,
        // and the z spread is already at its floor for the separation above. Anything much larger
        // needs either overlap allowed, per-stage framing, or the two golem-scale enemies
        // (Stone Warden 1.18, Sundered Colossus 1.55) toned down in enemies.json.
        //
        // x closed as far as it goes; that air was free, since vertical binds either way.
        //
        // Every number re-derived rather than scaled down by eye, and one correction in the
        // method itself made most of the room the first time:
        //
        // The old rule was "clearing on z costs H / sin(28)". That is not the requirement. Two
        // units at the same x are separated on screen by dz*sin(28), and the near one's head
        // reaches H*cos(28) above its own feet, so what non-overlap actually costs is
        //   dz >= H * cos(28) / sin(28) = H / tan(28) = H * 1.881
        // not H / sin(28) = H * 2.130. The old formula over-charged every z gap by 13%.
        //
        // Binding pairs, over every authored composition in campaign.json and every hero in every
        // slot at team sizes 1 to 5:
        //   front rank  Sundered Colossus slot 0 next to a Grave Moth slot 1 (ch1_12). The
        //               Colossus's hammer tips out at 3.162, so 2*Zf*sin(28) must clear
        //               3.162*cos(28): Zf >= 3.208 at the current separation. Chosen 3.25.
        //   back rank   Stone Warden slot 3 next to a Hollow Husk slot 4 (ch1_10). Hammer top
        //               2.407, so Zb >= 4.925. Chosen 4.95, and this pair is what sets the final
        //               minimum separation.
        //   sides       Nyx Serrow slot 0 against the Colossus slot 0 across the middle. Their
        //               turned half-widths are 0.63 and 1.32, so Xf >= 1.114. Chosen 1.40.
        //   x-step      Colossus slot 0 against a Grave Moth slot 2, needing Xb - Xf >= 2.320.
        //               Chosen 2.55.
        //
        // Minimum screen separation over everything checked: 0.232 world units, 33 px at 1080p,
        // at the Stone Warden / Hollow Husk pair above. Nothing overlaps at any team size.
        //
        // Half-widths come from the assembled rig, not from a nominal figure: the local AABB of
        // every primitive a Build* method emits including its weapon, turned by
        // UnitRig.ThreeQuarterTurn, which widens a procedural rig (real depth rotates into the
        // width axis) and narrows a flat sprite quad (no depth to rotate). Shadows are excluded
        // from the overlap test on purpose - they are soft blobs on the ground and reading as
        // pooled is correct - but they ARE included in the frame bound below, which is where the
        // last pass lost them.
        private static readonly Vector2[] AllySlots =
        {
            new Vector2(-1.4f, -3.25f), new Vector2(-1.4f, 3.25f),
            new Vector2(-3.95f, -4.95f), new Vector2(-3.95f, 0f), new Vector2(-3.95f, 4.95f)
        };

        private static readonly Vector2[] EnemySlots =
        {
            new Vector2(1.4f, -3.25f), new Vector2(1.4f, 3.25f),
            new Vector2(3.95f, -4.95f), new Vector2(3.95f, 0f), new Vector2(3.95f, 4.95f)
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
            // orthographicSize re-derived against the tightened slots above. Bounds taken over
            // every hero in every slot and every enemy in the slots campaign.json actually places
            // it in, and over the animation range rather than the rest pose alone. Two extras are
            // reserved that earlier passes left out, because both were caught clipping:
            //   the blob shadow's rotated footprint, 0.79 * scale of z-reach past the slot, and
            //   0.85 * scale of x-reach, which is what made the bottom of frame slice shadows;
            //   the camera shake, +/-0.18 world (BattleScreen asks 0.14 on a crit, 0.18 on a
            //   burst), which moves the frame 0.18*cos(28) = 0.159 vertically. The backdrop
            //   already budgeted for shake; the frame bound never did.
            //
            //   highest  f  4.388   Orrin Kaal, slot 4, mid-cast, at shake extreme
            //   lowest   f -2.806   Orrin Kaal, slot 2, shadow near edge, at shake extreme
            //   span 7.194, half-span 3.597, centre 0.791
            //
            // Horizontal: widest reach is a Stone Warden in the back rank's outer slot, 3.95 plus
            // its own shadow half-width 0.81, plus shake, = 5.15.
            //   orthographicSize by width = 5.15 / 1.778 = 2.90
            //
            // Vertical binds, 3.60 against 2.90. Chosen 3.65, which leaves about 0.05 world (8 px
            // at 1080p) beyond the already-reserved shake and cast at each edge, and 1.34 world
            // (198 px) of unused air at each SIDE. Horizontal is not close and cannot become
            // close at this aspect: it would only bind below an aspect ratio of 5.15 / 3.65 =
            // 1.41, so anything at or above 3:2 is clear and 16:9 has room to spare.
            //
            // 3.85 -> 3.65 renders everything 5% larger again. See the slot comment above for why
            // that is the ceiling and what would have to change to beat it.
            //
            // Camera: x 0, symmetric. z -9.5 unchanged. y re-solved for the new centre:
            // camY = (0.791 + 9.5*0.4695) / 0.8829 = 5.95. Floors re-checked: camY (5.95) is
            // still above orthographicSize*cos(28) (3.22), and the nearest occupied point is well
            // past the 0.3 near clip.
            Camera.orthographic = true;
            Camera.orthographicSize = 3.65f;
            Camera.nearClipPlane = 0.3f;
            Camera.farClipPlane = 120f;
            Camera.depth = -10f;
            _cameraRig = cameraObject.transform;
            _cameraHome = new Vector3(0f, 5.95f, -9.5f);
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
            // old value: the back rank's far-z occupant (x 3.95, z 4.95) is now the deepest point,
            // forward depth 15.55 along the camera's view axis, down again now that the back
            // rank's z reach is +/-4.95 rather than +/-5.25. 22 clears that with margin
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
