using System.Collections;
using UnityEngine;
using Emberfall.Core.Combat;

namespace Emberfall.Battle
{
    /// <summary>
    /// The visual half of one combatant. Holds no combat rules - it only plays back what the
    /// simulation already decided, and tracks display-side health for the bars.
    /// </summary>
    public sealed class UnitActor : MonoBehaviour
    {
        public UnitRig Rig;
        public UnitSnapshot Snapshot;

        public bool Alive = true;
        public float DisplayHp;
        public float MaxHp;
        public float DisplayShield;
        public int Energy;

        private float _idlePhase;
        private float _flash;
        private float _bobSpeed = 1.6f;
        private float _bobHeight = 0.09f;
        private Coroutine _motion;

        // Both fall back to the humanoid silhouette's own head top rather than a loose 1.8, so a
        // rigless actor anchors where a default-built one would. See UnitRig.Height for what that
        // number now means: the top of the rendered silhouette, measured, not a nominal label.
        public Vector3 HeadPosition
        {
            get { return transform.position + Vector3.up * (Rig != null ? Rig.Height : UnitRig.HumanoidTop); }
        }

        public Vector3 CenterPosition
        {
            get { return transform.position + Vector3.up * (Rig != null ? Rig.Height : UnitRig.HumanoidTop) * 0.55f; }
        }

        public void Setup(UnitSnapshot snapshot, Vector3 position, bool facingForward)
        {
            Snapshot = snapshot;
            MaxHp = snapshot.MaxHp;
            DisplayHp = snapshot.MaxHp;
            Energy = snapshot.StartEnergy;
            Alive = true;

            transform.localPosition = position;
            Rig = UnitRig.Build("Rig", transform, snapshot.Visual, facingForward);

            _idlePhase = (snapshot.Id * 0.73f) % 6.28f;
            _bobSpeed = snapshot.IsBoss ? 1.1f : 1.5f + (snapshot.Id % 3) * 0.16f;
            _bobHeight = snapshot.IsBoss ? 0.12f : 0.09f;
        }

        private void Update()
        {
            if (Rig == null || Rig.Body == null) return;

            // Runs whether or not the unit is still alive - a death clip needs ticks after Alive
            // flips false to reach and then hold its final frame. A no-op for every unit besides
            // Kael today: Rig.Animation is null for every other rig (see UnitRig.Animation).
            if (Rig.Animation != null) Rig.Animation.Update(Time.deltaTime);

            if (Alive)
            {
                _idlePhase += Time.deltaTime * _bobSpeed;
                float bob = Mathf.Sin(_idlePhase) * _bobHeight;
                float sway = Mathf.Sin(_idlePhase * 0.6f) * 4.0f;
                Rig.Body.localPosition = new Vector3(0f, bob, 0f);
                Rig.Body.localRotation = Quaternion.Euler(0f, sway, Mathf.Sin(_idlePhase * 0.8f) * 3.0f);
            }

            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - Time.deltaTime * 5.5f);
                Rig.SetFlash(_flash);
            }
        }

        // ------------------------------------------------------------------ playback

        public void PlayAttackLunge(Vector3 towards, float speed)
        {
            if (!Alive) return;
            if (Rig != null && Rig.Animation != null) Rig.Animation.PlayOnce("basic", false);
            Restart(LungeRoutine(towards, speed));
        }

        private IEnumerator LungeRoutine(Vector3 towards, float speed)
        {
            Vector3 home = Vector3.zero;
            Vector3 direction = towards - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f) direction = direction.normalized;
            Vector3 forward = direction * 0.85f;

            yield return Move(home, forward, 0.10f / speed, true);
            yield return Move(forward, home, 0.20f / speed, false);
            if (Rig != null) Rig.Root.localPosition = Vector3.zero;
        }

        public void PlayCast(float speed)
        {
            if (!Alive) return;
            if (Rig != null && Rig.Animation != null) Rig.Animation.PlayOnce("skill", false);
            Restart(CastRoutine(speed));
        }

        private IEnumerator CastRoutine(float speed)
        {
            float duration = 0.34f / speed;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float lift = Mathf.Sin(k * Mathf.PI) * 0.28f;
                if (Rig != null)
                {
                    Rig.Root.localPosition = new Vector3(0f, lift, -lift * 0.25f);
                    Rig.SetEmission(Mathf.Sin(k * Mathf.PI) * 0.9f);
                }
                yield return null;
            }
            if (Rig != null)
            {
                Rig.Root.localPosition = Vector3.zero;
                Rig.SetEmission(0f);
            }
        }

        public void PlayHit(bool critical)
        {
            if (!Alive) return;
            _flash = critical ? 1f : 0.75f;
            if (Rig != null) Rig.SetFlash(_flash);
            if (Rig != null && Rig.Animation != null) Rig.Animation.PlayOnce("hit_reaction", false);
            Restart(FlinchRoutine(critical ? 0.24f : 0.15f));
        }

        private IEnumerator FlinchRoutine(float distance)
        {
            Vector3 back = new Vector3(0f, 0f, -distance);
            yield return Move(Vector3.zero, back, 0.06f, false);
            yield return Move(back, Vector3.zero, 0.16f, false);
            if (Rig != null) Rig.Root.localPosition = Vector3.zero;
        }

        /// <summary>
        /// Seconds a sprite death holds fully visible after its clip's own play-through, before
        /// fading. Shared with BattleScreen (DeathHoldNominalSeconds below) so the event pacing
        /// and the actual fade this method runs agree on the same number rather than two literals
        /// that could drift apart.
        /// </summary>
        public const float SpriteDeathFadeSeconds = 0.4f;

        /// <summary>
        /// Nominal (1x speed) seconds BattleScreen's Death event pacing should hold beyond its own
        /// existing 0.28s, so a unit with a death clip finishes playing AND fading before the next
        /// BattleEvent starts. Zero for a unit with no SpriteAnimator or no death clip - the exact
        /// same "no clip data, no behaviour change" fold-back every other trigger here uses.
        /// </summary>
        public float DeathHoldNominalSeconds
        {
            get
            {
                if (Rig == null || Rig.Animation == null) return 0f;
                float clip = Rig.Animation.ClipDuration("death");
                return clip > 0f ? clip + SpriteDeathFadeSeconds : 0f;
            }
        }

        public IEnumerator PlayDeath(float speed)
        {
            Alive = false;
            if (_motion != null) StopCoroutine(_motion);
            _motion = null;
            if (Rig == null) yield break;

            if (Rig.Animation != null)
            {
                yield return PlaySpriteDeath(speed);
                yield break;
            }

            float duration = 0.75f / speed;
            float t = 0f;
            Quaternion from = Rig.Body.localRotation;
            Quaternion to = Quaternion.Euler(-78f, 0f, 14f);

            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                Rig.Body.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, k * 1.6f)));
                Rig.Body.localPosition = new Vector3(0f, -k * 0.18f, 0f);
                Rig.SetFade(1f - Mathf.Clamp01((k - 0.25f) / 0.75f));
                if (Rig.Shadow != null)
                {
                    float shrink = 1f - k;
                    Rig.Shadow.localScale = new Vector3(UnitRig.ShadowWidth * shrink, UnitRig.ShadowDepth * shrink, 1f);
                }
                yield return null;
            }

            Rig.SetFade(0f);
            if (Rig.Root != null) Rig.Root.gameObject.SetActive(false);
        }

        /// <summary>
        /// A unit with its own death clip already conveys the fall in its art - the generic
        /// rotate/drop/fade tumble above is a stand-in for a unit with none, so it does not also
        /// run here (rotating the whole billboard while its own frames separately show a fall
        /// would just be two conflicting motions). This is the fix for the actual reported bug:
        /// that tumble's fixed 0.75s/speed duration used to race the clip's real ~4.8s play-through
        /// (145 frames at 30fps) and lost - the rig faded to nothing and deactivated while the clip
        /// had barely started, so the death was never actually seen. Held fully visible for the
        /// clip's own duration instead (ticked by the ordinary Update() below, which already runs
        /// regardless of Alive - see there), then a short fade using SpriteDeathFadeSeconds, then
        /// the same deactivation the generic path ends on.
        /// </summary>
        private IEnumerator PlaySpriteDeath(float speed)
        {
            Rig.Animation.PlayOnce("death", true);

            float holdSeconds = Rig.Animation.ClipDuration("death") / Mathf.Max(0.01f, speed);
            float t = 0f;
            while (t < holdSeconds)
            {
                t += Time.deltaTime;
                yield return null;
            }

            float fadeSeconds = SpriteDeathFadeSeconds / Mathf.Max(0.01f, speed);
            float f = 0f;
            while (f < fadeSeconds)
            {
                f += Time.deltaTime;
                float shrink = 1f - Mathf.Clamp01(f / fadeSeconds);
                Rig.SetFade(shrink);
                // Same shrink-with-fade the generic tumble above already does, so the shadow
                // doesn't sit there full-size under a character that has otherwise vanished.
                if (Rig.Shadow != null)
                    Rig.Shadow.localScale = new Vector3(UnitRig.ShadowWidth * shrink, UnitRig.ShadowDepth * shrink, 1f);
                yield return null;
            }

            Rig.SetFade(0f);
            if (Rig.Root != null) Rig.Root.gameObject.SetActive(false);
        }

        public void Revive()
        {
            Alive = true;
            if (Rig == null) return;
            Rig.Root.gameObject.SetActive(true);
            Rig.Root.localPosition = Vector3.zero;
            Rig.Body.localRotation = Quaternion.identity;
            Rig.Body.localPosition = Vector3.zero;
            Rig.SetFade(1f);
            Rig.SetFlash(0f);
            if (Rig.Animation != null) Rig.Animation.PlayLoop("idle");
        }

        private void Restart(IEnumerator routine)
        {
            if (_motion != null) StopCoroutine(_motion);
            _motion = StartCoroutine(routine);
        }

        private IEnumerator Move(Vector3 from, Vector3 to, float duration, bool ease)
        {
            if (duration <= 0f) { if (Rig != null) Rig.Root.localPosition = to; yield break; }
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                if (ease) k = k * k;
                else k = Mathf.SmoothStep(0f, 1f, k);
                if (Rig != null) Rig.Root.localPosition = Vector3.Lerp(from, to, k);
                yield return null;
            }
            if (Rig != null) Rig.Root.localPosition = to;
        }

        private void OnDestroy()
        {
            if (Rig != null) Rig.Destroy();
        }
    }
}
