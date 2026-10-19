using System.Collections;
using UnityEngine;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;

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
        public int Energy;

        /// <summary>
        /// Set by BattleStage.Populate. Presentation feedback attaches here, at the moments the
        /// host already plays a hit, a shield absorbing and a death, so a host needs no changes to
        /// get them (see VfxDirector). Null in any rig built without a stage.
        /// </summary>
        public VfxDirector Director;

        private float _displayShield;

        /// <summary>
        /// The shield bar's value, which the host writes from the ShieldGain and ShieldAbsorb
        /// events. A property so a fall in it is observable: that fall is a shield absorbing a
        /// blow, and a fall to zero is the shield breaking. The record has no event for the second.
        /// </summary>
        public float DisplayShield
        {
            get { return _displayShield; }
            set
            {
                float before = _displayShield;
                _displayShield = value;
                if (value < before && Director != null) Director.NoteShield(this, before, value);
            }
        }

        private float _idlePhase;
        private float _hitStop;
        private Vector3 _knockDirection;
        private float _knockScale = 1f;
        private float _knockWorld;
        private float _flash;
        /// <summary>R15 v2: true while _flash is a hit's tint falling at _flashRate on the play
        /// clock, ahead of the hit-stop test. False is the R12 flash, held by hit stop.</summary>
        private bool _flashFree;
        private float _flashRate;
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
            // Name and aspect travel with the rig: the name is what a placeholder mannequin
            // wears instead of a face, and what a broken sprite is reported under; the aspect
            // is what tints the mannequin. Both come off the snapshot the simulation already
            // produced, so no screen has to look a unit up a second time to draw it.
            // StageFeel.Resolve is the snapshot's own VisualDef, the same object, unless the v2 look
            // is on: then a stand-in's art, a corrected facing and the larger scale (StagePresentation).
            Rig = UnitRig.Build("Rig", transform, StageFeel.Resolve(snapshot), facingForward,
                                snapshot.Name, snapshot.Aspect);
            // v2: clips whose art faces the other way from the rest of the sheet (null in v1).
            if (Rig.Animation != null) Rig.Animation.SetClipFlips(StageFeel.ClipFlips(snapshot.DefId));

            _idlePhase = (snapshot.Id * 0.73f) % 6.28f;
            _bobSpeed = snapshot.IsBoss ? 1.1f : 1.5f + (snapshot.Id % 3) * 0.16f;
            _bobHeight = snapshot.IsBoss ? 0.12f : 0.09f;
        }

        private void Update()
        {
            if (Rig == null || Rig.Body == null) return;

            // Hit stop: a blow freezes both units it touches for a few frames. Animation, bob and
            // the flash decay all wait, so the flash holds at its peak for the duration. Counted in
            // unscaled time so it is a real-time pause whatever the game clock is doing, and the
            // motion coroutines below wait on the same counter.
            // R15 v2: a hit's flash is a short tint on the real-time clock. It falls before the freeze
            // test, so hit stop can hold the motion but never the flash past its seconds.
            if (_flashFree && _flash > 0f)
            {
                // Real time, so slow motion cannot stretch it. A paused game (timeScale 0, which is
                // how a capture freezes a frame) holds it.
                float step = Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f;
                _flash = Mathf.Max(0f, _flash - step * _flashRate);
                Rig.SetFlash(_flash);
                if (_flash <= 0f) _flashFree = false;
            }

            if (_hitStop > 0f)
            {
                _hitStop -= Time.unscaledDeltaTime;
                return;
            }

            // Runs whether or not the unit is still alive - a death clip needs ticks after Alive
            // flips false to reach and then hold its final frame. A no-op for every unit besides
            // Kael today: Rig.Animation is null for every other rig (see UnitRig.Animation).
            if (Rig.Animation != null) Rig.Animation.Update(Time.deltaTime);

            if (Alive && Rig.Animation == null)
            {
                _idlePhase += Time.deltaTime * _bobSpeed;
                float bob = Mathf.Sin(_idlePhase) * _bobHeight;
                float sway = Mathf.Sin(_idlePhase * 0.6f) * 4.0f;
                Rig.Body.localPosition = new Vector3(0f, bob, 0f);
                Rig.Body.localRotation = Quaternion.Euler(0f, sway, Mathf.Sin(_idlePhase * 0.8f) * 3.0f);
            }

            if (_flash > 0f && !_flashFree)
            {
                if (Juice.On) _flash = Mathf.Max(0f, _flash - Time.deltaTime * JuiceMath.FlashDecayRate(Juice.Profile));
                else _flash = Mathf.Max(0f, _flash - Time.deltaTime * 5.5f);
                Rig.SetFlash(_flash);
            }
        }

        // ------------------------------------------------------------------ playback

        /// <summary>
        /// How far a melee unit travels toward its target and back over one basic attack. Named
        /// rather than left as a literal inside LungeRoutine because it is now a choice - a ranged
        /// unit is given 0 instead (see UnitRig.AttacksAtRange), and a magic number that means
        /// "melee" is worth being able to point at.
        /// </summary>
        public const float ApproachDistance = 0.85f;

        /// <summary>
        /// The fixed-timing lunge, for a unit with no "basic" clip to sync against. Takes distance
        /// for the same reason the clip-driven overload does - a staff-carrying enemy is ranged
        /// whether or not anyone has drawn it an attack animation yet - while keeping the 0.10/0.20
        /// out/back timing here rather than spilling those two literals into BattleScreen.
        /// </summary>
        public void PlayAttackLunge(Vector3 towards, float speed, float distance)
        {
            PlayAttackLunge(BattleClips.Basic, towards, 0.10f, 0.20f, speed, distance);
        }

        /// <summary>The fixed-timing lunge playing a named basic clip - the Dev Arena's path,
        /// which alternates basic and basic2 by the same rule as the battle screen.</summary>
        public void PlayAttackLunge(string clipId, Vector3 towards, float speed, float distance)
        {
            PlayAttackLunge(clipId, towards, 0.10f, 0.20f, speed, distance);
        }

        /// <summary>
        /// outSeconds/backSeconds are nominal (1x speed) - divided by speed here, the same way the
        /// two-argument overload's fixed 0.10f/0.20f always were, so a caller driving these from a
        /// SpriteAnimator clip (BattleScreen's BasicAttack case) and the plain instant-lunge caller
        /// stay on the same footing. Exists so the lunge's own timing can be synced to a clip's
        /// ImpactFraction - out to reach the target when the strike actually connects, back over
        /// the clip's remaining follow-through - instead of the fixed snap/return every unit
        /// without a SpriteAnimator still gets from the overload above.
        ///
        /// distance is how far to travel; ApproachDistance for a melee unit, 0 for one that
        /// attacks where it stands. Zero is deliberately routed through the SAME routine rather
        /// than short-circuited past it: the clip playback, the out/back timing and the final
        /// localPosition reset all still have to happen on the same clock, and the only difference
        /// worth having between a ranged attack and a melee one is how much ground it covers.
        /// </summary>
        public void PlayAttackLunge(Vector3 towards, float outSeconds, float backSeconds, float speed, float distance)
        {
            PlayAttackLunge(BattleClips.Basic, towards, outSeconds, backSeconds, speed, distance);
        }

        /// <summary>
        /// clipId is which basic clip this swing plays - BattleClips.BasicClipFor decides it from
        /// the record, and the caller times outSeconds/backSeconds off that same clip. A unit that
        /// never shipped it plays its fallback (SpriteAnimator.Resolve).
        /// </summary>
        public void PlayAttackLunge(string clipId, Vector3 towards, float outSeconds, float backSeconds, float speed, float distance)
        {
            if (!Alive) return;
            if (Rig != null && Rig.Animation != null) Rig.Animation.PlayOnce(clipId, false);
            _acting = true;
            Restart(LungeRoutine(towards, outSeconds, backSeconds, speed, distance));
        }

        /// <summary>
        /// True while this unit's own lunge or cast motion is running. A blow that lands in that
        /// window still flashes, but does not start a flinch: the flinch would replace the lunge
        /// coroutine and snap a unit that is still out at its target back home in one frame.
        /// </summary>
        private bool _acting;

        /// <summary>
        /// Shows `controlled` as the rest loop while the unit's turn has been taken away, and idle
        /// again once it has not. The battle screen calls this off the status mirror it keeps from
        /// StatusApplied / StatusExpired / StatusSpread; see BattleClips for why Stun is the only
        /// status that counts. A unit with no controlled clip rests on idle as before, and a
        /// placeholder mannequin has no animator and ignores it.
        /// </summary>
        public void SetControlled(bool controlled)
        {
            if (Rig == null || Rig.Animation == null) return;
            Rig.Animation.SetRest(controlled ? BattleClips.Controlled : BattleClips.Idle);
        }

        private IEnumerator LungeRoutine(Vector3 towards, float outSeconds, float backSeconds, float speed, float distance)
        {
            // v2: the front pairs stand 2.30 apart, not 2.80, and the units are 1.2x larger, so the
            // R12 step of 0.85 would put the bodies inside each other at contact. The step is scaled
            // to leave the gap R12 left in world units (2.80 - 0.85 = 1.95 = 2.30 - 0.34).
            if (StageFeel.V2) distance *= BattleFraming.ApproachScaleV2;
            Vector3 home = Vector3.zero;
            Vector3 direction = towards - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f) direction = direction.normalized;
            Vector3 forward = direction * distance;

            yield return Move(home, forward, outSeconds / speed, true);
            yield return Move(forward, home, backSeconds / speed, false);
            if (Rig != null) Rig.Root.localPosition = Vector3.zero;
            _acting = false;
        }

        public void PlayCast(float speed)
        {
            if (!Alive) return;
            if (Rig != null && Rig.Animation != null) Rig.Animation.PlayOnce(BattleClips.Skill, false);
            _acting = true;
            Restart(CastRoutine(speed));
        }

        private IEnumerator CastRoutine(float speed)
        {
            float duration = 0.34f / speed;
            float t = 0f;
            while (t < duration)
            {
                if (_hitStop > 0f) { yield return null; continue; }
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
            _acting = false;
        }

        public void PlayHit(bool critical)
        {
            // First, before the Alive test, so the director's view of which Damage event this is
            // never slips against the host's: it is told once per call, dead target or not.
            if (Director != null) Director.NoteHit(this, critical);

            // Consumed here whether or not a flinch follows, so a direction set for a blow that
            // did not flinch cannot leak into the next one.
            Vector3 knock = _knockDirection;
            float knockScale = _knockScale;
            float knockWorld = _knockWorld;
            _knockDirection = Vector3.zero;
            _knockScale = 1f;
            _knockWorld = 0f;

            if (!Alive) return;
            _flash = critical ? 1f : 0.75f;
            if (Juice.On) _flash = JuiceMath.FlashPeak(Juice.Profile, critical);
            // R15 v2: a tint (about 35 percent, crit about 55) that lasts FlashSeconds and is not
            // held by hit stop. The white-out above is what v1 plays.
            if (Feel.V2)
            {
                FeelProfile fp = Feel.Profile;
                _flash = FeelMath.FlashPeak(fp, critical);
                _flashRate = FeelMath.FlashDecayRate(fp, critical);
                _flashFree = true;
            }
            else _flashFree = false;
            if (Rig != null) Rig.SetFlash(_flash);
            // Priority, highest first: death, the unit's own action, the hit reaction. A blow
            // landing while this unit is still swinging or casting flashes it and nothing more.
            if (_acting) return;
            if (Rig != null && Rig.Animation != null && !Rig.Animation.PlayReaction(BattleClips.HitReaction)) return;
            // R12: the knock is a fixed pixel distance (VfxDirector sets it from the hit kind), so
            // a hit, a skill and a crit read as three clear steps at any camera size.
            if (Juice.On && knockWorld > 0f) { Restart(FlinchRoutine(knockWorld, knock)); return; }
            Restart(FlinchRoutine((critical ? 0.24f : 0.15f) * knockScale, knock));
        }

        /// <summary>
        /// A flash that is not a blow: a shield taking a hit, a status landing. Sets the colour the
        /// flash lerps toward, and the amount; it starts no flinch and no hit stop.
        /// </summary>
        public void Flash(float amount, Color color)
        {
            if (Rig == null || !Alive) return;
            Rig.SetFlashColor(color);
            _flashFree = false;                  // R15: only a blow's flash runs on its own clock
            _flash = Mathf.Max(_flash, amount);
            Rig.SetFlash(_flash);
        }

        /// <summary>
        /// R15 v2: a tint toward a colour that lasts `seconds` on the real-time clock, held by no hit
        /// stop: the debuff darkening of the body for a moment, the counterpart of a hit flash.
        /// </summary>
        public void TintFor(float amount, Color color, float seconds)
        {
            if (Rig == null || !Alive || amount <= 0f) return;
            Rig.SetFlashColor(color);
            _flash = Mathf.Max(_flash, amount);
            _flashRate = _flash / Mathf.Max(0.01f, seconds);
            _flashFree = true;
            Rig.SetFlash(_flash);
        }

        public void SetFlashColor(Color color)
        {
            if (Rig != null) Rig.SetFlashColor(color);
        }

        /// <summary>
        /// Freezes this unit for `seconds` of real time. Called on both ends of a blow. A longer
        /// request wins over one already running; two short ones do not add.
        /// </summary>
        public void HitStop(float seconds)
        {
            if (seconds > _hitStop) _hitStop = seconds;
        }

        /// <summary>
        /// Which way the next flinch goes: along `awayWorld`, the attacker-to-target line, instead
        /// of the fixed step toward the camera. `scale` stretches the nudge (a crit pushes harder).
        /// Used once, by the next PlayHit.
        /// </summary>
        public void SetKnock(Vector3 awayWorld, float scale)
        {
            awayWorld.y = 0f;
            _knockDirection = awayWorld.sqrMagnitude > 0.0001f ? awayWorld.normalized : Vector3.zero;
            _knockScale = scale;
        }

        /// <summary>
        /// R12: how far, in world units, the next flinch travels, replacing the legacy
        /// (0.15 or 0.24) * scale. Used once by the next PlayHit, like SetKnock.
        /// </summary>
        public void SetKnockDistance(float world)
        {
            _knockWorld = world;
        }

        private IEnumerator FlinchRoutine(float distance, Vector3 awayWorld)
        {
            Vector3 back = new Vector3(0f, 0f, -distance);
            if (awayWorld.sqrMagnitude > 0.0001f)
            {
                back = transform.InverseTransformVector(awayWorld * distance);
                back.y = 0f;
            }
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
                float clip = Rig.Animation.ClipDuration(BattleClips.Death);
                return clip > 0f ? clip + SpriteDeathFadeSeconds : 0f;
            }
        }

        public IEnumerator PlayDeath(float speed)
        {
            if (Director != null) Director.NoteDeath(this);
            Alive = false;
            _acting = false;
            _flashFree = false;                  // R15: the death flash is the R12 one, held by its hit stop
            if (_motion != null) StopCoroutine(_motion);
            _motion = null;
            if (Rig == null) yield break;

            // R12: the unit whitens and holds for a beat before it goes, then the flash decays at
            // the hit-flash rate. Update still ticks the decay after Alive flips (it never tests it).
            if (Juice.On)
            {
                Rig.SetFlashColor(Color.white);
                _flash = 1f;
                Rig.SetFlash(_flash);
                HitStop(Juice.Profile.DeathFlashSeconds / Mathf.Max(0.01f, speed));
            }

            if (Rig.Animation != null)
            {
                yield return PlaySpriteDeath(speed);
                yield break;
            }

            float duration = 0.75f / speed;
            if (Juice.On) duration = Juice.Profile.DeathSeconds / speed;
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
                if (Juice.On)
                {
                    JuiceProfile jp = Juice.Profile;
                    Rig.Body.localPosition = new Vector3(0f, -k * jp.DeathSink, 0f);
                    Rig.SetFade(1f - Mathf.Clamp01((k - jp.DeathFadeStart) / (1f - jp.DeathFadeStart)));
                }
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
            Rig.Animation.PlayOnce(BattleClips.Death, true);

            float holdSeconds = Rig.Animation.ClipDuration(BattleClips.Death) / Mathf.Max(0.01f, speed);
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
                if (Juice.On) Rig.Body.localPosition = new Vector3(0f, -(1f - shrink) * Juice.Profile.SpriteDeathSink, 0f);
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
            _acting = false;
            if (_motion != null) StopCoroutine(_motion);
            _motion = null;
            if (Rig == null) return;
            Rig.Root.gameObject.SetActive(true);
            Rig.Root.localPosition = Vector3.zero;
            Rig.Body.localRotation = Quaternion.identity;
            Rig.Body.localPosition = Vector3.zero;
            Rig.SetFade(1f);
            Rig.SetFlash(0f);
            if (Rig.Animation != null)
            {
                Rig.Animation.SetRest(BattleClips.Idle);
                Rig.Animation.PlayLoop(BattleClips.Idle);
            }
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
                if (_hitStop > 0f) { yield return null; continue; }
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
