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
        public float Height = 1.8f;

        public Material Primary;
        public Material Secondary;
        public Material Accent;
        public readonly List<Material> Materials = new List<Material>();

        private static readonly Vector3 NoRotation = Vector3.zero;

        public static UnitRig Build(string name, Transform parent, VisualDef visual, bool facingForward)
        {
            UnitRig rig = new UnitRig();

            GameObject rootObject = new GameObject(name);
            rig.Root = rootObject.transform;
            rig.Root.SetParent(parent, false);
            rig.Root.localRotation = Quaternion.Euler(0f, facingForward ? 0f : 180f, 0f);

            GameObject bodyObject = new GameObject("Body");
            rig.Body = bodyObject.transform;
            rig.Body.SetParent(rig.Root, false);

            // Sprite path: a unit with a non-empty VisualDef.Sprite renders a textured billboard
            // instead of the primitive body below, and skips the weapon (baked into the art).
            // Empty Sprite (every unit except Orrin today) takes the untouched procedural branch -
            // same code, same output, no behaviour change.
            if (!string.IsNullOrEmpty(visual.Sprite))
            {
                rig.BuildSprite(visual.Sprite, facingForward);
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
            Height = 1.85f;
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
            Height = 2.05f;
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
            Height = 1.25f;
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
            Height = 1.55f;
            Solid(PrimitiveType.Sphere, "Core", Body, new Vector3(0f, 1.05f, 0f), new Vector3(0.62f, 0.66f, 0.62f), NoRotation, Primary);
            Solid(PrimitiveType.Sphere, "Inner", Body, new Vector3(0f, 1.05f, 0.06f), new Vector3(0.36f, 0.38f, 0.36f), NoRotation, Accent);
            Solid(PrimitiveType.Cube, "ShardA", Body, new Vector3(-0.44f, 1.22f, 0f), new Vector3(0.10f, 0.30f, 0.10f), new Vector3(0f, 0f, 24f), Secondary);
            Solid(PrimitiveType.Cube, "ShardB", Body, new Vector3(0.44f, 1.22f, 0f), new Vector3(0.10f, 0.30f, 0.10f), new Vector3(0f, 0f, -24f), Secondary);
            Solid(PrimitiveType.Cube, "ShardC", Body, new Vector3(0f, 1.62f, -0.10f), new Vector3(0.10f, 0.26f, 0.10f), NoRotation, Secondary);
            Solid(PrimitiveType.Capsule, "Trail", Body, new Vector3(0f, 0.52f, -0.04f), new Vector3(0.26f, 0.22f, 0.26f), NoRotation, Secondary);
        }

        private void BuildGolem()
        {
            Height = 2.25f;
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
        /// Height matches BuildHumanoid's 1.85 exactly, so head/centre-position math elsewhere
        /// (floating text, camera bar tracking, VFX impact points, all keyed on Rig.Height) does
        /// not need to know or care which kind of rig it is looking at - "roughly the same visual
        /// footprint as the procedural version" is met by construction, not by eyeballing it.
        /// Width is derived from the loaded texture's own aspect ratio at that fixed height, not
        /// hardcoded, so a differently-proportioned placeholder or the eventual real art both
        /// render at their natural proportions without a code change.
        /// </summary>
        private void BuildSprite(string spriteId, bool facingForward)
        {
            Height = 1.85f;
            Texture2D tex = BattleAssets.UnitSprite(spriteId);
            float aspect = tex.height > 0 ? tex.width / (float)tex.height : 0.6f;
            float width = Height * aspect;

            Material material = BattleAssets.NewFx(tex, Color.white, false);
            // Primary/Secondary/Accent all point at the one sprite material, not three separate
            // ones - SetEmission (unlike SetFlash/SetFade) writes through these named fields
            // rather than the generic Materials list, and this keeps that call safe and
            // meaningful for a rig with only one material to glow.
            Primary = Secondary = Accent = material;
            Materials.Add(material);

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Sprite";
            StripCollider(quad);
            Transform t = quad.transform;
            t.SetParent(Body, false);
            t.localPosition = new Vector3(0f, Height * 0.5f, 0f);
            t.localScale = new Vector3(width, Height, 1f);

            // A default Unity Quad's front face points +Z. The battle camera always sits on the
            // -Z side of the arena for both ally and enemy slots, so the sprite's own local
            // rotation has to counter-rotate whatever Root already applied for ally/enemy facing
            // (0 or 180 around Y, set above) so the two sum to 180 either way and the front face
            // always ends up pointing at -Z, toward the camera, not mirrored on one side. This
            // reasoning has not been checked against the real camera - verify in Play mode.
            t.localRotation = Quaternion.Euler(0f, facingForward ? 180f : 0f, 0f);

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

        private void BuildShadow()
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Shadow";
            StripCollider(quad);
            Shadow = quad.transform;
            Shadow.SetParent(Root, false);
            Shadow.localPosition = new Vector3(0f, 0.015f, 0f);
            Shadow.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Shadow.localScale = new Vector3(1.35f, 1.05f, 1f);

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
