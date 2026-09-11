using System.Collections.Generic;
using UnityEngine;
using Emberfall.Core.Data;
using Emberfall.UI;

namespace Emberfall.Battle
{
    /// <summary>
    /// A character assembled from Unity primitives at runtime. Silhouette, weapon and palette
    /// come from the unit's VisualDef, so every hero and monster reads differently on the
    /// battlefield without a single imported model.
    /// </summary>
    public sealed class UnitRig
    {
        public Transform Root;      // placed and rotated by the battle stage
        public Transform Body;      // everything animated hangs off this
        public Transform WeaponPivot;
        public Transform Shadow;

        /// <summary>
        /// World Y of the TOP OF THE RENDERED SILHOUETTE above the unit's origin (its feet on the
        /// ground), before visual.Scale is applied. Head, not weapon: a raised staff or spear
        /// overshoots this deliberately, the same way it overshoots a procedural rig's head.
        ///
        /// This used to be a nominal per-silhouette guess that matched nothing. Measured against
        /// what each Build* method actually assembles - Unity's Capsule and Cylinder are 2 units
        /// tall natively, so localScale.y is their HALF height, while Cube, Sphere and Quad are 1
        /// unit and localScale.y is their FULL height - the old values were wrong by a different
        /// amount for every silhouette, and not even in a consistent direction:
        ///
        ///   humanoid  declared 1.85   real top 1.710 (Hood)     -0.140
        ///   brute     declared 2.05   real top 1.740 (Head)     -0.310
        ///   beast     declared 1.25   real top 1.080 (Spine)    -0.170
        ///   wisp      declared 1.55   real top 1.750 (ShardC)   +0.200
        ///   golem     declared 2.25   real top 1.900 (Head)     -0.350
        ///
        /// That mattered far more than a mislabelled field usually would, because the two rig
        /// paths use this number in opposite directions. A procedural rig ASSERTS it: the body is
        /// built from fixed offsets and Height is a label bolted on afterwards, so a wrong value
        /// changed nothing visible and only misplaced the health bars that track it. The sprite
        /// path DERIVES from it: BuildSprite sizes its quad so the body's top lands exactly here,
        /// so 1.85 was not a wrong label there, it was a wrong instruction, and it built every
        /// sprite hero 8% taller at the head than the 1.710 a procedural humanoid actually reaches.
        /// Orrin, at scale 1.12, stood 27% taller than a Hollow Husk he is supposed to match.
        ///
        /// One definition, both paths, and side-by-side heights follow by construction rather than
        /// by two numbers happening to agree.
        /// </summary>
        public float Height = HumanoidTop;

        /// <summary>
        /// The humanoid silhouette's rendered head top, and so the height a sprite hero is built
        /// to. Named rather than repeated because the whole point is that the two are the same
        /// number: a sprite stands in for a humanoid body, so it has to reach exactly as high as
        /// one. Nyx Serrow is the last procedural hero, and she is the direct comparison.
        /// </summary>
        public const float HumanoidTop = 1.71f;

        /// <summary>
        /// The other four silhouettes' measured rendered tops, from the table above. Named rather
        /// than left as literals inside each Build* method for the same reason HumanoidTop is:
        /// the sprite path has to resolve the SAME number the procedural path asserts, and two
        /// copies of a number that must agree is exactly how BuildSprite came to hardcode
        /// HumanoidTop for every silhouette in the first place.
        /// </summary>
        public const float BruteTop = 1.74f;
        public const float BeastTop = 1.08f;
        public const float WispTop = 1.75f;
        public const float GolemTop = 1.90f;

        /// <summary>
        /// The rendered top for a silhouette id, before visual.Scale. One resolver, read by both
        /// rig paths: the procedural Build* methods assign from these constants and BuildSprite
        /// looks the same value up, so a sprite stands exactly as tall as the rig it replaces
        /// rather than as tall as a humanoid.
        ///
        /// The default arm is humanoid on purpose, and it is not a guess: Build's own silhouette
        /// switch falls through to BuildHumanoid for any unrecognised string, so an unknown
        /// silhouette resolves to the same height on both paths by construction rather than by
        /// two switches happening to be written the same way. Keep the two defaults in step.
        /// </summary>
        public static float SilhouetteTop(string silhouette)
        {
            switch (silhouette)
            {
                case "brute": return BruteTop;
                case "beast": return BeastTop;
                case "wisp": return WispTop;
                case "golem": return GolemTop;
                default: return HumanoidTop;
            }
        }

        /// <summary>
        /// Whether a unit strikes from where it stands instead of closing on its target. Read by
        /// BattleScreen's BasicAttack choreography, which otherwise slides EVERY unit
        /// UnitActor.ApproachDistance toward whoever it is hitting - written when every attack in
        /// the game was a melee one, and wrong the moment a bow or a spellbook turned up: Orren
        /// walked his arrow most of the way to the target before firing it.
        ///
        /// Keyed off the weapon rather than HeroRole because the role does not carry this.
        /// Seraphine is a Breaker and so is Kael; she reads from a spellbook at range and he
        /// swings a greatsword. The weapon is the thing that actually says where the attack
        /// happens, which is also why BuildWeapon switches on the same field.
        ///
        /// Only the approach is removed, never the attack. The clip still plays, the impact still
        /// lands on ImpactFraction, and the follow-through still defers to _pendingAnimationTail -
        /// a ranged unit just covers zero distance while doing it, so its choreography stays on
        /// exactly the same clock as a melee unit's rather than becoming a second timing path.
        ///
        /// The default is melee on purpose, matching BuildWeapon's own default arm: an unknown or
        /// absent weapon keeps the behaviour every unit had before this existed, so a new weapon
        /// string can only fail toward the old look rather than silently rooting a unit in place.
        /// </summary>
        public static bool AttacksAtRange(string weapon)
        {
            switch (weapon)
            {
                case "bow":
                case "spellbook":
                case "orb":
                case "staff":
                    return true;
                default:
                    return false;
            }
        }

        public Material Primary;
        public Material Secondary;
        public Material Accent;
        public readonly List<Material> Materials = new List<Material>();

        /// <summary>
        /// Non-null only for a sprite rig whose VisualDef.Sprite has a matching animation clip set
        /// (an "idle" clip found via BattleAssets.UnitAnimClip - Kael today, nobody else). Null for
        /// every procedural rig and for every sprite rig without clip data, which is every other
        /// unit in the game right now; UnitActor checks this before driving playback, so nothing
        /// downstream needs a separate "does this unit animate" flag.
        /// </summary>
        public SpriteAnimator Animation;

        private static readonly Vector3 NoRotation = Vector3.zero;

        /// <summary>
        /// How far a unit turns off camera-square to face the opposing side, in degrees, mirrored
        /// by side. Derived, not picked: a unit turned by phi has cos(phi) of its facing still
        /// pointing at the camera and sin(phi) pointing at the enemy. At 30 the camera-facing
        /// component is 0.87 and the opposing component is 0.50 - the turn reads clearly as
        /// "these two lines face each other" while the face stays dominantly toward the viewer
        /// and the shoulder line only foreshortens to 87% of square-on. 45 would split the two
        /// evenly and read as true profile-ish three quarter, which is more than the slight turn
        /// wanted here; below about 20 the turn stops reading at chibi scale. BattleStage's slot
        /// spacing is verified against this exact angle, since turning a unit widens its
        /// projected silhouette (a Grave Moth's nose-to-tail depth is three times its width, so
        /// it widens most); the spacing still clears with zero overlap anywhere in 0 to 40, so
        /// this value can be retuned within that range without redoing the layout.
        /// </summary>
        public const float ThreeQuarterTurn = 30f;

        public static UnitRig Build(string name, Transform parent, VisualDef visual, bool facingForward)
        {
            UnitRig rig = new UnitRig();

            GameObject rootObject = new GameObject(name);
            rig.Root = rootObject.transform;
            rig.Root.SetParent(parent, false);
            // Both sides now start from 180 (facing the camera, which sits on -Z) and turn off it
            // by ThreeQuarterTurn, mirrored: allies (facingForward, on the -X side) turn toward
            // +X, enemies on the +X side turn toward -X, so the two clusters face each other.
            //
            // The ally half of this is a real change of facing, not just an added turn. The old
            // value was 0 for allies, which points a unit's face along +Z - correct back when
            // BattleStage put allies at negative z and enemies at positive z, because +Z was
            // then "toward the enemy", and allies were deliberately seen from behind. The
            // left-versus-right rearrangement moved the enemy to +X and left this convention
            // behind: +Z stopped being toward anything and became simply away from the camera,
            // so allies were facing nothing, with their backs to the viewer. 180 +/- 30 restores
            // the intent (face the camera, lean toward the opponent) under the new geometry.
            rig.Root.localRotation = Quaternion.Euler(0f, facingForward ? 180f - ThreeQuarterTurn : 180f + ThreeQuarterTurn, 0f);

            GameObject bodyObject = new GameObject("Body");
            rig.Body = bodyObject.transform;
            rig.Body.SetParent(rig.Root, false);

            // Sprite path: a unit with a non-empty VisualDef.Sprite renders a textured billboard
            // instead of the primitive body below, and skips the weapon (baked into the art).
            // Empty Sprite takes the untouched procedural branch - same code, same output, no
            // behaviour change. That branch used to cover every unit but Orrin Kaal; with the
            // placeholder roster deleted, all five remaining heroes carry sheets and the procedural
            // body is now only reached by enemies that have no art yet.
            if (!string.IsNullOrEmpty(visual.Sprite))
            {
                rig.BuildSprite(visual.Sprite, visual.SpriteBodyFraction, visual.SpriteFootFraction, visual.SpriteMirror, visual.Silhouette);
            }
            else
            {
                Color primary = UITheme.Hex(visual.Primary);
                Color secondary = UITheme.Hex(visual.Secondary);
                Color accent = UITheme.Hex(visual.Accent);

                rig.Primary = BattleAssets.NewLit(primary, primary * 0.85f, 0f);
                rig.Secondary = BattleAssets.NewLit(secondary, primary * 0.5f, 0f);
                rig.Accent = BattleAssets.NewLit(accent, accent, 0.55f);
                rig.Materials.Add(rig.Primary);
                rig.Materials.Add(rig.Secondary);
                rig.Materials.Add(rig.Accent);

                switch (visual.Silhouette)
                {
                    case "brute": rig.BuildBrute(); break;
                    case "beast": rig.BuildBeast(); break;
                    case "wisp": rig.BuildWisp(); break;
                    case "golem": rig.BuildGolem(); break;
                    default: rig.BuildHumanoid(); break;
                }

                rig.BuildWeapon(visual.Weapon);
            }

            rig.BuildShadow();

            float scale = Mathf.Max(0.3f, visual.Scale);
            rig.Root.localScale = new Vector3(scale, scale, scale);
            rig.Height *= scale;
            return rig;
        }

        // ------------------------------------------------------------------ silhouettes

        private void BuildHumanoid()
        {
            // Hood, a Capsule at y 1.55 with localScale.y 0.16, so half-height 0.16: top 1.710.
            // Head (Sphere, y 1.48, scale.y 0.44) reaches 1.700, just under it.
            Height = HumanoidTop;
            Solid(PrimitiveType.Capsule, "Torso", Body, new Vector3(0f, 0.92f, 0f), new Vector3(0.52f, 0.40f, 0.44f), NoRotation, Secondary);
            Solid(PrimitiveType.Capsule, "Chest", Body, new Vector3(0f, 1.08f, 0.02f), new Vector3(0.56f, 0.26f, 0.46f), NoRotation, Primary);
            Solid(PrimitiveType.Sphere, "Head", Body, new Vector3(0f, 1.48f, 0.02f), new Vector3(0.42f, 0.44f, 0.42f), NoRotation, Secondary);
            Solid(PrimitiveType.Capsule, "Hood", Body, new Vector3(0f, 1.55f, -0.02f), new Vector3(0.46f, 0.16f, 0.46f), NoRotation, Primary);

            Solid(PrimitiveType.Sphere, "ShoulderL", Body, new Vector3(-0.30f, 1.14f, 0f), new Vector3(0.26f, 0.24f, 0.26f), NoRotation, Primary);
            Solid(PrimitiveType.Sphere, "ShoulderR", Body, new Vector3(0.30f, 1.14f, 0f), new Vector3(0.26f, 0.24f, 0.26f), NoRotation, Primary);
            Solid(PrimitiveType.Capsule, "ArmL", Body, new Vector3(-0.33f, 0.90f, 0f), new Vector3(0.15f, 0.24f, 0.15f), NoRotation, Secondary);
            Solid(PrimitiveType.Capsule, "ArmR", Body, new Vector3(0.33f, 0.90f, 0f), new Vector3(0.15f, 0.24f, 0.15f), NoRotation, Secondary);

            Solid(PrimitiveType.Capsule, "LegL", Body, new Vector3(-0.14f, 0.34f, 0f), new Vector3(0.18f, 0.30f, 0.18f), NoRotation, Secondary);
            Solid(PrimitiveType.Capsule, "LegR", Body, new Vector3(0.14f, 0.34f, 0f), new Vector3(0.18f, 0.30f, 0.18f), NoRotation, Secondary);

            Solid(PrimitiveType.Cube, "Cloak", Body, new Vector3(0f, 0.94f, -0.24f), new Vector3(0.52f, 0.86f, 0.06f), new Vector3(6f, 0f, 0f), Primary);

            Solid(PrimitiveType.Sphere, "EyeL", Body, new Vector3(-0.11f, 1.50f, 0.20f), new Vector3(0.09f, 0.06f, 0.05f), NoRotation, Accent);
            Solid(PrimitiveType.Sphere, "EyeR", Body, new Vector3(0.11f, 1.50f, 0.20f), new Vector3(0.09f, 0.06f, 0.05f), NoRotation, Accent);
        }

        private void BuildBrute()
        {
            // Head (Sphere, y 1.56, scale.y 0.36) tops at 1.740; Helm (Cube, y 1.64, scale.y 0.20)
            // reaches 1.740 too. The old 2.05 was above both by 0.31.
            Height = BruteTop;
            Solid(PrimitiveType.Capsule, "Torso", Body, new Vector3(0f, 0.98f, 0f), new Vector3(0.72f, 0.44f, 0.58f), NoRotation, Secondary);
            Solid(PrimitiveType.Cube, "Plate", Body, new Vector3(0f, 1.14f, 0.14f), new Vector3(0.62f, 0.42f, 0.22f), NoRotation, Primary);
            Solid(PrimitiveType.Sphere, "Head", Body, new Vector3(0f, 1.56f, 0.02f), new Vector3(0.38f, 0.36f, 0.38f), NoRotation, Secondary);
            Solid(PrimitiveType.Cube, "Helm", Body, new Vector3(0f, 1.64f, 0f), new Vector3(0.44f, 0.20f, 0.44f), NoRotation, Primary);

            Solid(PrimitiveType.Sphere, "PauldronL", Body, new Vector3(-0.44f, 1.24f, 0f), new Vector3(0.40f, 0.34f, 0.38f), NoRotation, Primary);
            Solid(PrimitiveType.Sphere, "PauldronR", Body, new Vector3(0.44f, 1.24f, 0f), new Vector3(0.40f, 0.34f, 0.38f), NoRotation, Primary);
            Solid(PrimitiveType.Capsule, "ArmL", Body, new Vector3(-0.46f, 0.92f, 0f), new Vector3(0.21f, 0.26f, 0.21f), NoRotation, Secondary);
            Solid(PrimitiveType.Capsule, "ArmR", Body, new Vector3(0.46f, 0.92f, 0f), new Vector3(0.21f, 0.26f, 0.21f), NoRotation, Secondary);

            Solid(PrimitiveType.Capsule, "LegL", Body, new Vector3(-0.20f, 0.36f, 0f), new Vector3(0.24f, 0.32f, 0.24f), NoRotation, Secondary);
            Solid(PrimitiveType.Capsule, "LegR", Body, new Vector3(0.20f, 0.36f, 0f), new Vector3(0.24f, 0.32f, 0.24f), NoRotation, Secondary);

            Solid(PrimitiveType.Sphere, "EyeL", Body, new Vector3(-0.10f, 1.58f, 0.19f), new Vector3(0.10f, 0.05f, 0.05f), NoRotation, Accent);
            Solid(PrimitiveType.Sphere, "EyeR", Body, new Vector3(0.10f, 1.58f, 0.19f), new Vector3(0.10f, 0.05f, 0.05f), NoRotation, Accent);
        }

        private void BuildBeast()
        {
            // Spine is a Capsule turned 90 about X, so its 0.52 scale.y becomes depth and its 0.46
            // scale.x becomes the height that counts: top 0.62 + 0.46 = 1.080. A quadruped, and
            // the only silhouette whose top is its back rather than its head.
            Height = BeastTop;
            Solid(PrimitiveType.Capsule, "Spine", Body, new Vector3(0f, 0.62f, 0f), new Vector3(0.46f, 0.52f, 0.46f), new Vector3(90f, 0f, 0f), Secondary);
            Solid(PrimitiveType.Sphere, "Hump", Body, new Vector3(0f, 0.80f, -0.16f), new Vector3(0.44f, 0.34f, 0.46f), NoRotation, Primary);
            Solid(PrimitiveType.Sphere, "Head", Body, new Vector3(0f, 0.72f, 0.58f), new Vector3(0.36f, 0.32f, 0.42f), NoRotation, Secondary);
            Solid(PrimitiveType.Cube, "Snout", Body, new Vector3(0f, 0.66f, 0.80f), new Vector3(0.20f, 0.16f, 0.26f), NoRotation, Primary);

            Solid(PrimitiveType.Capsule, "LegFL", Body, new Vector3(-0.24f, 0.28f, 0.34f), new Vector3(0.14f, 0.28f, 0.14f), NoRotation, Secondary);
            Solid(PrimitiveType.Capsule, "LegFR", Body, new Vector3(0.24f, 0.28f, 0.34f), new Vector3(0.14f, 0.28f, 0.14f), NoRotation, Secondary);
            Solid(PrimitiveType.Capsule, "LegBL", Body, new Vector3(-0.24f, 0.28f, -0.36f), new Vector3(0.14f, 0.28f, 0.14f), NoRotation, Secondary);
            Solid(PrimitiveType.Capsule, "LegBR", Body, new Vector3(0.24f, 0.28f, -0.36f), new Vector3(0.14f, 0.28f, 0.14f), NoRotation, Secondary);

            Solid(PrimitiveType.Cube, "Tail", Body, new Vector3(0f, 0.78f, -0.62f), new Vector3(0.10f, 0.10f, 0.52f), new Vector3(-18f, 0f, 0f), Primary);
            Solid(PrimitiveType.Sphere, "EyeL", Body, new Vector3(-0.13f, 0.80f, 0.74f), new Vector3(0.09f, 0.07f, 0.05f), NoRotation, Accent);
            Solid(PrimitiveType.Sphere, "EyeR", Body, new Vector3(0.13f, 0.80f, 0.74f), new Vector3(0.09f, 0.07f, 0.05f), NoRotation, Accent);
        }

        private void BuildWisp()
        {
            // ShardC (Cube, y 1.62, scale.y 0.26) tops at 1.750. The only silhouette whose old
            // value was too LOW - bars sat inside a wisp rather than above it.
            Height = WispTop;
            Solid(PrimitiveType.Sphere, "Core", Body, new Vector3(0f, 1.05f, 0f), new Vector3(0.62f, 0.66f, 0.62f), NoRotation, Primary);
            Solid(PrimitiveType.Sphere, "Inner", Body, new Vector3(0f, 1.05f, 0.06f), new Vector3(0.36f, 0.38f, 0.36f), NoRotation, Accent);
            Solid(PrimitiveType.Cube, "ShardA", Body, new Vector3(-0.44f, 1.22f, 0f), new Vector3(0.10f, 0.30f, 0.10f), new Vector3(0f, 0f, 24f), Secondary);
            Solid(PrimitiveType.Cube, "ShardB", Body, new Vector3(0.44f, 1.22f, 0f), new Vector3(0.10f, 0.30f, 0.10f), new Vector3(0f, 0f, -24f), Secondary);
            Solid(PrimitiveType.Cube, "ShardC", Body, new Vector3(0f, 1.62f, -0.10f), new Vector3(0.10f, 0.26f, 0.10f), NoRotation, Secondary);
            Solid(PrimitiveType.Capsule, "Trail", Body, new Vector3(0f, 0.52f, -0.04f), new Vector3(0.26f, 0.22f, 0.26f), NoRotation, Secondary);
        }

        private void BuildGolem()
        {
            // Head (Cube, y 1.72, scale.y 0.36) tops at 1.900. The old 2.25 was the worst overshoot
            // of the five, and the Sundered Colossus (scale 1.55) magnified it into 0.54 of empty
            // air that BattleStage's spacing and framing were both sized against.
            Height = GolemTop;
            Solid(PrimitiveType.Cube, "Torso", Body, new Vector3(0f, 1.10f, 0f), new Vector3(0.86f, 0.86f, 0.62f), NoRotation, Secondary);
            Solid(PrimitiveType.Cube, "Chest", Body, new Vector3(0f, 1.28f, 0.16f), new Vector3(0.66f, 0.40f, 0.36f), NoRotation, Primary);
            Solid(PrimitiveType.Cube, "Head", Body, new Vector3(0f, 1.72f, 0.02f), new Vector3(0.44f, 0.36f, 0.42f), NoRotation, Primary);

            Solid(PrimitiveType.Cube, "ShoulderL", Body, new Vector3(-0.58f, 1.42f, 0f), new Vector3(0.34f, 0.34f, 0.44f), new Vector3(0f, 0f, 12f), Primary);
            Solid(PrimitiveType.Cube, "ShoulderR", Body, new Vector3(0.58f, 1.42f, 0f), new Vector3(0.34f, 0.34f, 0.44f), new Vector3(0f, 0f, -12f), Primary);
            Solid(PrimitiveType.Cube, "ArmL", Body, new Vector3(-0.60f, 0.92f, 0f), new Vector3(0.26f, 0.60f, 0.28f), NoRotation, Secondary);
            Solid(PrimitiveType.Cube, "ArmR", Body, new Vector3(0.60f, 0.92f, 0f), new Vector3(0.26f, 0.60f, 0.28f), NoRotation, Secondary);

            Solid(PrimitiveType.Cube, "LegL", Body, new Vector3(-0.26f, 0.34f, 0f), new Vector3(0.32f, 0.68f, 0.34f), NoRotation, Secondary);
            Solid(PrimitiveType.Cube, "LegR", Body, new Vector3(0.26f, 0.34f, 0f), new Vector3(0.32f, 0.68f, 0.34f), NoRotation, Secondary);

            Solid(PrimitiveType.Cube, "Core", Body, new Vector3(0f, 1.22f, 0.34f), new Vector3(0.18f, 0.18f, 0.08f), new Vector3(0f, 0f, 45f), Accent);
            Solid(PrimitiveType.Cube, "Visor", Body, new Vector3(0f, 1.72f, 0.22f), new Vector3(0.30f, 0.07f, 0.04f), NoRotation, Accent);
        }

        /// <summary>
        /// A single textured quad standing where the procedural silhouette would have stood.
        /// Height stays the body's own top regardless of what this method scales the quad to -
        /// head/centre-position math elsewhere (floating text, bar tracking, VFX impact points,
        /// all keyed on Rig.Height) reads that field, not the quad, so it keeps anchoring to the
        /// top of the body even when the quad extends above it for a raised weapon. See
        /// bodyFraction below for what does extend it, and why.
        /// </summary>
        private void BuildSprite(string spriteId, float bodyFraction, float footFraction, bool mirror, string silhouette)
        {
            // The silhouette's own top, not a number of its own, and not HumanoidTop. This is the
            // line the whole sprite pipeline hangs off: the quad below is sized so the body's top
            // lands exactly here, so this value IS the rendered height of every sprite unit.
            // Resolving it from the silhouette the sprite replaces is what makes a sprite unit and
            // the procedural rig it stands in for reach the same height.
            //
            // It read HumanoidTop unconditionally before, which was correct only by luck: every
            // sprite in the project was a hero, and every hero is humanoid, so the one silhouette
            // it assumed was the only one it ever met. The first non-humanoid sprite broke it in
            // both directions at once and silently, because a wrong height throws no error and
            // renders perfectly happily. A Grave Moth is a beast (1.080) and would have rendered
            // at 1.710, 58% too tall, going from 60% of a Hollow Husk's height to 95% and ceasing
            // to read as a low skittering thing at all. A Sundered Colossus is a golem (1.900) and
            // would have rendered 10% short, on the boss whose scale of 1.55 magnifies it most.
            //
            // Same class of bug as the one the Height table above records, and it survived that
            // fix for the same reason it was introduced: a per-silhouette number collapsed to a
            // single constant that happened to be right for the only case anyone had.
            Height = SilhouetteTop(silhouette);

            // An animated unit sizes off one FRAME, not the whole sheet - the sheet's own aspect
            // (all frames laid out in a cols x rows grid) has nothing to do with the character's
            // own proportions. A unit with no clip data (everyone but Kael today) takes the exact
            // path this always took, unchanged.
            SpriteAnimator animator = SpriteAnimator.TryCreate(spriteId);
            Texture2D tex;
            float frameWidth, frameHeight;
            if (animator != null)
            {
                tex = animator.Texture;
                frameWidth = animator.FrameWidth;
                frameHeight = animator.FrameHeight;
            }
            else
            {
                tex = BattleAssets.UnitSprite(spriteId);
                frameWidth = tex.width;
                frameHeight = tex.height;
            }
            float aspect = frameHeight > 0 ? frameWidth / frameHeight : 0.6f;

            // Canvas height was previously just Height, which assumes the whole image is body -
            // true for a plain full-bleed figure, false for anything drawn holding a raised weapon:
            // Wren's staff reaches above her own head, so mapping the FULL canvas to Height mapped
            // her actual body to something shorter than Height, the same class of bug WeaponPivot
            // was built to avoid for procedural rigs (a weapon there extends past the torso
            // capsule without moving Height). bodyFraction is the sprite equivalent: the artist
            // reports what fraction of their own canvas, from the bottom, is body - not an
            // engine number tuned by trial and error, a measurement taken directly off their own
            // file the same way they already report its pixel dimensions. Canvas height becomes
            // Height / bodyFraction, taller than Height whenever bodyFraction < 1, so the BOTTOM
            // bodyFraction slice of that taller canvas still lands at exactly Height - the same
            // target every procedural body already hits - and the remaining top slice is where
            // the weapon's overflow goes, extending the quad upward rather than shrinking the
            // body to fit inside it.
            //
            // bodyFraction defaults to 1 when a hero's data does not set it, which reduces this
            // to canvasHeight = Height / 1 = Height - algebraically identical to the previous
            // formula, not just similar. Orrin's entry sets no such value, so this is a proven
            // no-op for him, not an assumed one; the same holds for every other sprite until one
            // is actually authored with a raised weapon and a real fraction.
            float canvasHeight = Height / Mathf.Max(0.05f, bodyFraction);
            float width = canvasHeight * aspect;

            Material material = BattleAssets.NewFx(tex, Color.white, false);
            // Primary/Secondary/Accent all point at the one sprite material, not three separate
            // ones - SetEmission (unlike SetFlash/SetFade) writes through these named fields
            // rather than the generic Materials list, and this keeps that call safe and
            // meaningful for a rig with only one material to glow.
            Primary = Secondary = Accent = material;
            Materials.Add(material);

            // Bind after the material exists, not before: Bind's first ApplyFrame writes into it
            // immediately, landing the idle clip's first frame's UV rect rather than the whole
            // sheet the quad was about to render un-cropped.
            if (animator != null)
            {
                animator.Bind(material);
                Animation = animator;
            }

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Sprite";
            StripCollider(quad);
            Transform t = quad.transform;
            t.SetParent(Body, false);
            // Bottom edge of the quad sits at y (-footFraction * canvasHeight), not always y 0:
            // footFraction is exactly how much of the canvas, below the character's own feet, is
            // empty margin. Shifting the whole quad DOWN by that amount pushes the transparent
            // margin below ground (hidden - there is nothing there to hide) so the ACTUAL feet,
            // not the canvas edge, land at y 0. Centring on half of canvasHeight (as before)
            // still lets a raised weapon's reserved headroom (SpriteBodyFraction < 1) extend
            // upward past the feet-to-head span, never downward past it. footFraction defaults
            // to 0 for every hero that doesn't set it, so this is an exact no-op there:
            // canvasHeight * 0.5f - 0f * canvasHeight is algebraically identical to the old line.
            float footShift = footFraction * canvasHeight;
            t.localPosition = new Vector3(0f, canvasHeight * 0.5f - footShift, 0f);
            // Cull is off in EmberfallFx (see the shader), so a negative X scale flips the quad's
            // rendered image without going invisible from reversed winding - the only visible
            // effect is the mirror. Mirror is authored per-hero (VisualDef.SpriteMirror), not
            // per-side: heroes are never spawned as enemies, so there is only ever one facing to
            // get right for a given sprite, unlike the ally/enemy turn direction above which
            // already differs by side on its own.
            t.localScale = new Vector3(mirror ? -width : width, canvasHeight, 1f);

            // No local rotation any more: the quad inherits Root's turn (180 -/+ ThreeQuarterTurn,
            // see Build) and so leans with the procedural rigs instead of staying square to the
            // camera. It previously counter-rotated to cancel Root out, which was right while
            // Root's two values were 0 and 180 and the goal was "always flat on to the camera";
            // it would now single this unit out as the only one not turned.
            //
            // The quad's front face is +Z, so at either turn its normal keeps a -Z component
            // (dot with the camera's forward is -0.76 both ways) and stays visible, foreshortened
            // to cos(30) = 87% of its square-on width. Because the two sides turn opposite ways,
            // the enemy side shows the same texture mirrored, which is deliberate and is what
            // DESIGN-ART-DELIVERY.md now specifies sprites be drawn for.
            //
            // Not verifiable here: this is a flat billboard, so turning it foreshortens art that
            // still depicts a square-on character. Orrin's placeholder will read slightly wrong
            // until it is redrawn to the three-quarter spec. Which side ends up un-mirrored is
            // also a Play mode check, not something this reasoning settles.
            t.localRotation = Quaternion.identity;

            MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ------------------------------------------------------------------ weapons

        private void BuildWeapon(string weapon)
        {
            GameObject pivotObject = new GameObject("WeaponPivot");
            WeaponPivot = pivotObject.transform;
            WeaponPivot.SetParent(Body, false);
            WeaponPivot.localPosition = new Vector3(0.42f, 0.95f, 0.10f);

            switch (weapon)
            {
                case "sword":
                    Solid(PrimitiveType.Cube, "Blade", WeaponPivot, new Vector3(0f, 0.42f, 0f), new Vector3(0.07f, 0.72f, 0.14f), NoRotation, Accent);
                    Solid(PrimitiveType.Cube, "Guard", WeaponPivot, new Vector3(0f, 0.06f, 0f), new Vector3(0.26f, 0.06f, 0.09f), NoRotation, Primary);
                    Solid(PrimitiveType.Cube, "Grip", WeaponPivot, new Vector3(0f, -0.08f, 0f), new Vector3(0.06f, 0.20f, 0.06f), NoRotation, Secondary);
                    break;
                case "greatsword":
                    Solid(PrimitiveType.Cube, "Blade", WeaponPivot, new Vector3(0f, 0.62f, 0f), new Vector3(0.11f, 1.06f, 0.20f), NoRotation, Accent);
                    Solid(PrimitiveType.Cube, "Guard", WeaponPivot, new Vector3(0f, 0.06f, 0f), new Vector3(0.40f, 0.08f, 0.12f), NoRotation, Primary);
                    Solid(PrimitiveType.Cube, "Grip", WeaponPivot, new Vector3(0f, -0.14f, 0f), new Vector3(0.08f, 0.32f, 0.08f), NoRotation, Secondary);
                    break;
                case "spear":
                    Solid(PrimitiveType.Cylinder, "Shaft", WeaponPivot, new Vector3(0f, 0.36f, 0f), new Vector3(0.05f, 0.72f, 0.05f), NoRotation, Secondary);
                    Solid(PrimitiveType.Cube, "Tip", WeaponPivot, new Vector3(0f, 1.10f, 0f), new Vector3(0.09f, 0.34f, 0.05f), new Vector3(0f, 45f, 0f), Accent);
                    break;
                case "staff":
                    Solid(PrimitiveType.Cylinder, "Shaft", WeaponPivot, new Vector3(0f, 0.34f, 0f), new Vector3(0.05f, 0.76f, 0.05f), NoRotation, Secondary);
                    Solid(PrimitiveType.Sphere, "Orb", WeaponPivot, new Vector3(0f, 1.16f, 0f), new Vector3(0.24f, 0.24f, 0.24f), NoRotation, Accent);
                    Solid(PrimitiveType.Cube, "RingA", WeaponPivot, new Vector3(0f, 1.16f, 0f), new Vector3(0.36f, 0.03f, 0.04f), new Vector3(0f, 0f, 22f), Primary);
                    break;
                case "dagger":
                    Solid(PrimitiveType.Cube, "Blade", WeaponPivot, new Vector3(0f, 0.22f, 0f), new Vector3(0.05f, 0.34f, 0.10f), NoRotation, Accent);
                    Solid(PrimitiveType.Cube, "Grip", WeaponPivot, new Vector3(0f, -0.02f, 0f), new Vector3(0.05f, 0.14f, 0.05f), NoRotation, Secondary);
                    Solid(PrimitiveType.Cube, "BladeOff", Body, new Vector3(-0.42f, 1.05f, 0.08f), new Vector3(0.05f, 0.30f, 0.09f), new Vector3(0f, 0f, 18f), Accent);
                    break;
                case "orb":
                    Solid(PrimitiveType.Sphere, "Orb", WeaponPivot, new Vector3(0.06f, 0.32f, 0.14f), new Vector3(0.28f, 0.28f, 0.28f), NoRotation, Accent);
                    Solid(PrimitiveType.Cube, "Shard", WeaponPivot, new Vector3(0.06f, 0.56f, 0.14f), new Vector3(0.07f, 0.20f, 0.07f), new Vector3(0f, 0f, 18f), Primary);
                    break;
                case "hammer":
                    Solid(PrimitiveType.Cylinder, "Haft", WeaponPivot, new Vector3(0f, 0.30f, 0f), new Vector3(0.07f, 0.60f, 0.07f), NoRotation, Secondary);
                    Solid(PrimitiveType.Cube, "Head", WeaponPivot, new Vector3(0f, 0.94f, 0f), new Vector3(0.34f, 0.30f, 0.34f), NoRotation, Primary);
                    Solid(PrimitiveType.Cube, "Band", WeaponPivot, new Vector3(0f, 0.94f, 0f), new Vector3(0.38f, 0.08f, 0.38f), NoRotation, Accent);
                    break;
                case "claws":
                    for (int i = 0; i < 3; i++)
                    {
                        float offset = (i - 1) * 0.09f;
                        Solid(PrimitiveType.Cube, "ClawR" + i, WeaponPivot, new Vector3(offset, 0.06f, 0.18f), new Vector3(0.035f, 0.26f, 0.035f), new Vector3(58f, 0f, 0f), Accent);
                        Solid(PrimitiveType.Cube, "ClawL" + i, Body, new Vector3(-0.42f + offset, 1.01f, 0.18f), new Vector3(0.035f, 0.26f, 0.035f), new Vector3(58f, 0f, 0f), Accent);
                    }
                    break;
            }
        }

        /// <summary>
        /// Footprint of the blob shadow, in local units before visual.Scale. Was 1.35 x 1.05,
        /// which is wider than any body it sits under: a sprite hero's quad is 0.98 across and a
        /// humanoid rig 1.12, so the shadow stuck out past the feet on every side.
        ///
        /// It is a named constant because it is not only cosmetic. The shadow lies flat, so the
        /// three-quarter turn swings its footprint out in z past the unit's own slot, and that
        /// overhang is the LOWEST thing in frame for the back rank's near-z units. At 1.35 x 1.05
        /// it was 5.5% of the entire vertical framing budget, spent on the edge of a soft blob.
        /// UnitActor.PlayDeath shrinks the same numbers, so both read from here rather than
        /// repeating literals that have already drifted apart once.
        /// </summary>
        public const float ShadowWidth = 1.10f;
        public const float ShadowDepth = 0.85f;

        private void BuildShadow()
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Shadow";
            StripCollider(quad);
            Shadow = quad.transform;
            Shadow.SetParent(Root, false);
            Shadow.localPosition = new Vector3(0f, 0.015f, 0f);
            Shadow.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Shadow.localScale = new Vector3(ShadowWidth, ShadowDepth, 1f);

            MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
            renderer.material = BattleAssets.NewFx(BattleAssets.BlobShadow, new Color(0f, 0f, 0f, 0.55f), false);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ------------------------------------------------------------------ helpers

        private Transform Solid(PrimitiveType type, string name, Transform parent, Vector3 position,
                                Vector3 scale, Vector3 euler, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            StripCollider(go);

            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            t.localScale = scale;
            t.localRotation = Quaternion.Euler(euler.x, euler.y, euler.z);

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return t;
        }

        private static void StripCollider(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
        }

        // ------------------------------------------------------------------ material state

        public void SetFlash(float amount)
        {
            for (int i = 0; i < Materials.Count; i++) Materials[i].SetFloat("_Flash", amount);
        }

        public void SetFade(float amount)
        {
            for (int i = 0; i < Materials.Count; i++) Materials[i].SetFloat("_Fade", amount);
        }

        public void SetEmission(float primaryEmission)
        {
            // Null-guarded: a sprite rig assigns all three to the same material (see BuildSprite),
            // but this stays defensive against any future rig that leaves one unset.
            if (Primary != null) Primary.SetFloat("_Emission", primaryEmission);
            if (Secondary != null) Secondary.SetFloat("_Emission", primaryEmission * 0.5f);
            if (Accent != null) Accent.SetFloat("_Emission", 0.55f + primaryEmission);
        }

        public void Destroy()
        {
            if (Root != null) Object.Destroy(Root.gameObject);
            for (int i = 0; i < Materials.Count; i++) Object.Destroy(Materials[i]);
            Materials.Clear();
        }
    }
}
