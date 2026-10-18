using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Emberfall.Battle
{
    /// <summary>
    /// Lightweight effect system: pooled camera-facing quads driven by coroutines.
    /// Deliberately not Unity's ParticleSystem - this keeps every effect scriptable,
    /// deterministic in appearance and free of serialised setup.
    /// </summary>
    public sealed class BattleVfx : MonoBehaviour
    {
        private sealed class Quad
        {
            public GameObject Go;
            public Transform T;
            public MeshRenderer Renderer;
            public Material Material;
        }

        private readonly Stack<Quad> _pool = new Stack<Quad>();
        private readonly List<Quad> _live = new List<Quad>();

        public Camera Cam;
        public float SpeedScale = 1f;

        // ------------------------------------------------------------------ pool

        private Quad Take(Texture texture, Color color, bool additive)
        {
            Quad quad;
            if (_pool.Count > 0)
            {
                quad = _pool.Pop();
                quad.Go.SetActive(true);
            }
            else
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "Vfx";
                Collider collider = go.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                go.transform.SetParent(transform, false);

                quad = new Quad();
                quad.Go = go;
                quad.T = go.transform;
                quad.Renderer = go.GetComponent<MeshRenderer>();
                quad.Material = BattleAssets.NewFx(texture, color, additive);
                quad.Renderer.material = quad.Material;
                quad.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                quad.Renderer.receiveShadows = false;
            }

            quad.Material.SetTexture("_MainTex", texture);
            quad.Material.SetColor("_Color", color);
            // OneMinusSrcAlpha is 10, not 6 - see the note in BattleAssets.NewFx. 6 is
            // OneMinusSrcColor, which leaves alpha out of the destination term, so a
            // non-additive quad leaks the backdrop through and washes out over bright
            // backgrounds. Do not "correct" the 10 below to a 6; that is the regression.
            quad.Material.SetFloat("_SrcBlend", 5f);                  // BlendMode.SrcAlpha
            quad.Material.SetFloat("_DstBlend", additive ? 1f : 10f); // BlendMode.One : BlendMode.OneMinusSrcAlpha
            _live.Add(quad);
            return quad;
        }

        /// <summary>
        /// The same, but asks the effect cap first (R12): with the flag on, a quad the pool has no
        /// room for is refused, cheap decoration before important effects (JuiceMath.Admit), and
        /// the routine that asked ends. Null means refused. With the flag off this is Take.
        /// </summary>
        private Quad Take(Texture texture, Color color, bool additive, BudgetKind kind)
        {
            if (Juice.On && !JuiceMath.Admit(Juice.Profile, kind, _live.Count)) return null;
            return Take(texture, color, additive);
        }

        private void Release(Quad quad)
        {
            _live.Remove(quad);
            quad.Go.SetActive(false);
            _pool.Push(quad);
        }

        private void FaceCamera(Transform t)
        {
            if (Cam == null) return;
            t.rotation = Cam.transform.rotation;
        }

        // ------------------------------------------------------------------ effects

        /// <summary>Bright pop plus a shockwave ring. The workhorse for basic attacks.</summary>
        public void Impact(Vector3 position, Color color, float scale)
        {
            StartCoroutine(ImpactRoutine(position, color, scale));
            StartCoroutine(RingRoutine(position, color, scale * 1.6f, 0.30f, false));
            for (int i = 0; i < 5; i++)
            {
                float angle = i * 72f + 12f;
                Vector3 direction = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad) * 0.8f + 0.4f, 0f);
                StartCoroutine(SparkRoutine(position, direction, color, scale * 0.35f));
            }
        }

        private IEnumerator ImpactRoutine(Vector3 position, Color color, float scale)
        {
            Quad quad = Take(BattleAssets.SoftDot, color, true, BudgetKind.Impact);
            if (quad == null) yield break;
            quad.T.position = position;
            FaceCamera(quad.T);

            float duration = 0.24f / SpeedScale;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float size = Mathf.Lerp(0.35f, 1.5f, k) * scale;
                quad.T.localScale = new Vector3(size, size, size);
                quad.Material.SetColor("_Color", new Color(color.r, color.g, color.b, (1f - k) * 1.4f));
                yield return null;
            }
            Release(quad);
        }

        private IEnumerator SparkRoutine(Vector3 position, Vector3 direction, Color color, float scale)
        {
            Quad quad = Take(BattleAssets.SoftDot, color, true, BudgetKind.Spark);
            if (quad == null) yield break;
            quad.T.position = position;
            FaceCamera(quad.T);

            float duration = 0.34f / SpeedScale;
            float distance = 0.9f + scale;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                quad.T.position = position + direction * (distance * k) + Vector3.down * (k * k * 0.5f);
                float size = Mathf.Lerp(scale, scale * 0.15f, k);
                quad.T.localScale = new Vector3(size, size, size);
                quad.Material.SetColor("_Color", new Color(color.r, color.g, color.b, 1f - k));
                yield return null;
            }
            Release(quad);
        }

        /// <summary>Expanding ring. Flat on the ground for area effects, upright for impacts.</summary>
        public IEnumerator RingRoutine(Vector3 position, Color color, float maxRadius, float duration, bool flat)
        {
            Quad quad = Take(BattleAssets.RingTexture, color, true, BudgetKind.Ring);
            if (quad == null) yield break;
            quad.T.position = position + (flat ? new Vector3(0f, 0.04f, 0f) : Vector3.zero);
            if (flat) quad.T.rotation = Quaternion.Euler(90f, 0f, 0f);
            else FaceCamera(quad.T);

            duration = duration / SpeedScale;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float size = Mathf.Lerp(0.2f, maxRadius, Mathf.Sqrt(k));
                quad.T.localScale = new Vector3(size, size, size);
                quad.Material.SetColor("_Color", new Color(color.r, color.g, color.b, (1f - k) * 0.95f));
                yield return null;
            }
            Release(quad);
        }

        /// <summary>Weapon arc drawn between attacker and target.</summary>
        public void Slash(Vector3 from, Vector3 to, Color color)
        {
            StartCoroutine(SlashRoutine(from, to, color));
        }

        private IEnumerator SlashRoutine(Vector3 from, Vector3 to, Color color)
        {
            Quad quad = Take(BattleAssets.Streak, color, true, BudgetKind.Slash);
            if (quad == null) yield break;
            Vector3 mid = Vector3.Lerp(from, to, 0.62f) + Vector3.up * 0.15f;
            quad.T.position = mid;
            FaceCamera(quad.T);
            quad.T.localRotation = quad.T.localRotation * Quaternion.Euler(0f, 0f, 34f);

            float duration = 0.20f / SpeedScale;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float length = Mathf.Lerp(0.6f, 2.9f, Mathf.Sqrt(k));
                quad.T.localScale = new Vector3(length, 0.9f, 1f);
                quad.Material.SetColor("_Color", new Color(color.r, color.g, color.b, (1f - k) * 1.6f));
                yield return null;
            }
            Release(quad);
        }

        /// <summary>Projectile that travels and then detonates. Yields until it lands.</summary>
        public IEnumerator Bolt(Vector3 from, Vector3 to, Color color)
        {
            Quad quad = Take(BattleAssets.SoftDot, color, true, BudgetKind.Bolt);
            if (quad == null) { Impact(to, color, 0.9f); yield break; }
            quad.T.position = from;
            FaceCamera(quad.T);
            quad.T.localScale = new Vector3(0.45f, 0.45f, 0.45f);

            float duration = 0.20f / SpeedScale;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                Vector3 position = Vector3.Lerp(from, to, k);
                position.y += Mathf.Sin(k * Mathf.PI) * 0.7f;
                quad.T.position = position;
                quad.Material.SetColor("_Color", new Color(color.r, color.g, color.b, 1.3f));
                yield return null;
            }
            Release(quad);
            Impact(to, color, 0.9f);
        }

        /// <summary>Big ground-anchored detonation for area abilities.</summary>
        public void Burst(Vector3 position, Color color, float radius)
        {
            StartCoroutine(RingRoutine(position, color, radius * 2.6f, 0.46f, true));
            StartCoroutine(RingRoutine(position + Vector3.up * 0.5f, color, radius * 1.7f, 0.34f, false));
            StartCoroutine(ImpactRoutine(position + Vector3.up * 0.6f, color, radius));
            for (int i = 0; i < 9; i++)
            {
                float angle = i * 40f;
                Vector3 direction = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0.9f, Mathf.Sin(angle * Mathf.Deg2Rad));
                StartCoroutine(SparkRoutine(position + Vector3.up * 0.3f, direction.normalized, color, 0.4f));
            }
        }

        /// <summary>Motes drifting upward - healing and regeneration.</summary>
        public void Motes(Vector3 position, Color color, int count, float rise)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i * (360f / Mathf.Max(1, count)) + 20f;
                Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * 0.42f;
                StartCoroutine(MoteRoutine(position + offset, color, rise, i * 0.045f));
            }
        }

        private IEnumerator MoteRoutine(Vector3 position, Color color, float rise, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay / SpeedScale);
            Quad quad = Take(BattleAssets.SoftDot, color, true, BudgetKind.Mote);
            if (quad == null) yield break;
            quad.T.position = position;
            FaceCamera(quad.T);

            float duration = 0.62f / SpeedScale;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                quad.T.position = position + Vector3.up * (rise * k);
                float size = Mathf.Lerp(0.34f, 0.06f, k);
                quad.T.localScale = new Vector3(size, size, size);
                quad.Material.SetColor("_Color", new Color(color.r, color.g, color.b, 1f - k * k));
                yield return null;
            }
            Release(quad);
        }

        /// <summary>Ring that sweeps up the body - buffs, shields, passive triggers.</summary>
        public void Aura(Vector3 groundPosition, Color color, float height)
        {
            StartCoroutine(AuraRoutine(groundPosition, color, height));
        }

        private IEnumerator AuraRoutine(Vector3 groundPosition, Color color, float height)
        {
            Quad quad = Take(BattleAssets.RingTexture, color, true, BudgetKind.Aura);
            if (quad == null) yield break;
            quad.T.rotation = Quaternion.Euler(90f, 0f, 0f);

            float duration = 0.55f / SpeedScale;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                quad.T.position = groundPosition + Vector3.up * (0.05f + height * k);
                float size = Mathf.Lerp(1.5f, 0.7f, k);
                quad.T.localScale = new Vector3(size, size, size);
                quad.Material.SetColor("_Color", new Color(color.r, color.g, color.b, (1f - k) * 1.2f));
                yield return null;
            }
            Release(quad);
        }

        /// <summary>Dark implosion for shadow abilities.</summary>
        public void Implode(Vector3 position, Color color)
        {
            StartCoroutine(ImplodeRoutine(position, color));
        }

        private IEnumerator ImplodeRoutine(Vector3 position, Color color)
        {
            Quad quad = Take(BattleAssets.SoftDot, color, true, BudgetKind.Impact);
            if (quad == null) { Impact(position, color, 1.15f); yield break; }
            quad.T.position = position;
            FaceCamera(quad.T);

            float duration = 0.26f / SpeedScale;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float size = Mathf.Lerp(2.4f, 0.2f, k * k);
                quad.T.localScale = new Vector3(size, size, size);
                quad.Material.SetColor("_Color", new Color(color.r, color.g, color.b, k * 1.2f));
                yield return null;
            }
            Release(quad);
            Impact(position, color, 1.15f);
        }

        /// <summary>
        /// R12: a soft additive glow, or with ring a ring, at a point: the glow behind an impact and
        /// the ring of a skill moment. Fades from alpha to 0 over seconds while it grows. radius is
        /// the half width at the end; priority quads are refused only by a full pool.
        /// </summary>
        public void Glow(Vector3 position, Color color, float radius, float seconds, float alpha, bool priority, bool ring)
        {
            if (!Juice.On) return;
            StartCoroutine(GlowRoutine(position, color, radius, seconds, alpha, priority, ring));
        }

        private IEnumerator GlowRoutine(Vector3 position, Color color, float radius, float seconds, float alpha, bool priority, bool ring)
        {
            Texture texture = ring ? (Texture)BattleAssets.RingTexture : (Texture)BattleAssets.SoftDot;
            Quad quad = Take(texture, color, true, priority ? BudgetKind.Priority : BudgetKind.Impact);
            if (quad == null) yield break;
            quad.T.position = position;
            FaceCamera(quad.T);

            float duration = Mathf.Max(0.01f, seconds) / SpeedScale;
            float from = ring ? 0.6f : 0.8f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float size = Mathf.Lerp(from, 1f, Mathf.Sqrt(k)) * radius * 2f;
                quad.T.localScale = new Vector3(size, size, size);
                quad.Material.SetColor("_Color", new Color(color.r, color.g, color.b, alpha * (1f - k)));
                yield return null;
            }
            Release(quad);
        }

        public void StopAll()
        {
            StopAllCoroutines();
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                _live[i].Go.SetActive(false);
                _pool.Push(_live[i]);
            }
            _live.Clear();
        }
    }
}
