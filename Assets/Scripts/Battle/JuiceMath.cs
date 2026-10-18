using System;
using System.Collections.Generic;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;

namespace Emberfall.Battle
{
    /// <summary>What a moment in the record is, for juice purposes.</summary>
    public enum MomentKind
    {
        None,
        /// <summary>A basic attack that hit and did not crit.</summary>
        Hit,
        /// <summary>A basic attack that crit.</summary>
        Crit,
        /// <summary>The damage of a cast: Damage events after an AbilityCast until the next
        /// BasicAttack or TurnStart, the same rule PlayDamageSound uses for SkillImpact.</summary>
        Skill,
        Heal,
        Death
    }

    /// <summary>Which pool a BattleVfx quad is drawn from, for the effect cap.</summary>
    public enum BudgetKind
    {
        Spark,
        Impact,
        Ring,
        Mote,
        Slash,
        Bolt,
        Aura,
        /// <summary>A crit impact, a kill, the skill ring, the glow of a crit: refused only when the
        /// pool is completely full.</summary>
        Priority
    }

    /// <summary>
    /// The decisions of the R12 hit-feedback pass, as pure functions of a JuiceProfile and the
    /// record. Free of UnityEngine on purpose: Tools/DevHarness compiles this file, so the flag,
    /// the rollback, the cap, the lanes and the curves are proven by build-check without the
    /// editor (JuiceChecks.cs).
    ///
    /// Every function given JuiceProfile.Legacy returns the answer the battle gave before the
    /// pass. The battle code still holds the old literals; these functions are what it asks when
    /// Juice.On, and what the harness pins.
    /// </summary>
    public static class JuiceMath
    {
        // ---------------------------------------------------------------- the record

        /// <summary>True for a damage-over-time tick: a Damage event that carries a status.</summary>
        public static bool IsTick(BattleEvent e)
        {
            return e != null && e.Type == BattleEventType.Damage && e.Status != StatusKind.None;
        }

        /// <summary>What one event is, given whether a cast is still the current action.
        /// skillActive follows BattleScreen: set by AbilityCast, cleared by BasicAttack and
        /// TurnStart.</summary>
        public static MomentKind Classify(BattleEvent e, bool skillActive)
        {
            if (e == null) return MomentKind.None;
            switch (e.Type)
            {
                case BattleEventType.Damage:
                    if (e.Status != StatusKind.None) return MomentKind.None;
                    if (skillActive) return MomentKind.Skill;
                    return e.Critical ? MomentKind.Crit : MomentKind.Hit;
                case BattleEventType.Heal:
                    return e.Value > 0 ? MomentKind.Heal : MomentKind.None;
                case BattleEventType.Death:
                    return MomentKind.Death;
            }
            return MomentKind.None;
        }

        /// <summary>The next value of the skillActive flag after an event.</summary>
        public static bool NextSkillActive(BattleEvent e, bool skillActive)
        {
            if (e == null) return skillActive;
            if (e.Type == BattleEventType.AbilityCast) return true;
            if (e.Type == BattleEventType.BasicAttack || e.Type == BattleEventType.TurnStart) return false;
            return skillActive;
        }

        /// <summary>Classify every event of a record, in order.</summary>
        public static MomentKind[] ClassifyAll(IList<BattleEvent> events)
        {
            MomentKind[] kinds = new MomentKind[events.Count];
            bool skill = false;
            for (int i = 0; i < events.Count; i++)
            {
                skill = NextSkillActive(events[i], skill);
                kinds[i] = Classify(events[i], skill);
            }
            return kinds;
        }

        /// <summary>The unit whose aspect a moment shows: the source of a hit, a cast or a heal,
        /// the unit that fell for a death. -1 when unknown.</summary>
        public static int AspectUnit(BattleEvent e, MomentKind kind)
        {
            if (e == null) return -1;
            return kind == MomentKind.Death ? e.TargetId : e.SourceId;
        }

        /// <summary>Index of the first event of a kind, optionally by a given aspect, or -1.
        /// Reads the record; recomputes nothing.</summary>
        public static int FindMoment(IList<BattleEvent> events, Func<int, Aspect> aspectOf, MomentKind kind, Aspect? aspect)
        {
            MomentKind[] kinds = ClassifyAll(events);
            for (int i = 0; i < events.Count; i++)
            {
                if (kinds[i] != kind) continue;
                if (aspect == null) return i;
                int unit = AspectUnit(events[i], kind);
                if (unit >= 0 && aspectOf != null && aspectOf(unit) == aspect.Value) return i;
            }
            return -1;
        }

        // ---------------------------------------------------------------- hit-stop and flash

        /// <summary>Seconds both units freeze for. A skill impact takes the larger of the skill
        /// number and its crit or normal number. Legacy has no skill kind.</summary>
        public static float HitStopSeconds(JuiceProfile p, bool crit, bool skill)
        {
            float basic = crit ? p.HitStopCrit : p.HitStopNormal;
            if (p.On && skill && p.HitStopSkill > basic) return p.HitStopSkill;
            return basic;
        }

        /// <summary>The longest freeze any kind of hit can ask for.</summary>
        public static float HitStopMax(JuiceProfile p)
        {
            return Math.Max(p.HitStopNormal, Math.Max(p.HitStopCrit, p.On ? p.HitStopSkill : 0f));
        }

        /// <summary>How long the timeline holds after a Damage event. Only when the flag is on,
        /// never for a tick, and once per cast or attack: alreadyHeld is true after the first
        /// damage of the action, so a five-target skill holds once, not five times.</summary>
        public static float HoldFor(JuiceProfile p, bool tick, bool alreadyHeld, bool crit, bool skill)
        {
            if (!p.On || tick || alreadyHeld) return 0f;
            return Math.Min(HitStopSeconds(p, crit, skill), HitStopMax(p));
        }

        /// <summary>The hold, per event, for a whole record. Harness and capture planning only.</summary>
        public static float[] HoldsFor(IList<BattleEvent> events, JuiceProfile p)
        {
            float[] holds = new float[events.Count];
            bool skill = false;
            bool held = false;
            for (int i = 0; i < events.Count; i++)
            {
                BattleEvent e = events[i];
                skill = NextSkillActive(e, skill);
                if (e.Type == BattleEventType.AbilityCast || e.Type == BattleEventType.BasicAttack || e.Type == BattleEventType.TurnStart)
                    held = false;
                if (e.Type != BattleEventType.Damage) continue;
                holds[i] = HoldFor(p, IsTick(e), held, e.Critical, skill);
                if (!IsTick(e)) held = true;
            }
            return holds;
        }

        /// <summary>The white lerp a hit sets.</summary>
        public static float FlashPeak(JuiceProfile p, bool crit)
        {
            return crit ? p.FlashPeakCrit : p.FlashPeakNormal;
        }

        /// <summary>Flash lerp lost per second. Legacy keeps the old rate.</summary>
        public static float FlashDecayRate(JuiceProfile p)
        {
            if (p.On && p.FlashSeconds > 0f) return 1f / p.FlashSeconds;
            return JuiceProfile.LegacyFlashRate;
        }

        // ---------------------------------------------------------------- shake and knock

        /// <summary>Camera shake for a blow: the strongest of the kinds that apply. A normal hit
        /// that does not kill asks for none. Strength 0 means no shake.</summary>
        public static void ShakeFor(JuiceProfile p, bool crit, bool skill, bool kill, out float strength, out float seconds)
        {
            strength = 0f;
            seconds = 0f;
            if (crit) Raise(p.ShakeCritStrength, p.ShakeCritSeconds, ref strength, ref seconds);
            if (skill) Raise(p.ShakeSkillStrength, p.ShakeSkillSeconds, ref strength, ref seconds);
            if (kill) Raise(p.ShakeKillStrength, p.ShakeKillSeconds, ref strength, ref seconds);
        }

        private static void Raise(float s, float t, ref float strength, ref float seconds)
        {
            if (s > strength) { strength = s; seconds = t; }
        }

        /// <summary>Knock distance in screen pixels at 1080p; 0 keeps the old world-unit distance.</summary>
        public static float KnockPx(JuiceProfile p, bool crit, bool skill)
        {
            if (!p.On) return 0f;
            if (crit) return p.KnockPxCrit;
            return skill ? p.KnockPxSkill : p.KnockPxNormal;
        }

        /// <summary>World units that cover px pixels of a 1080-high screen, for a camera that
        /// fills rectHeight of it at orthographic size orthoSize.</summary>
        public static float PixelsToWorld(float px, float rectHeight, float orthoSize)
        {
            if (rectHeight <= 0f) return 0f;
            return px * 2f * orthoSize / (1080f * rectHeight);
        }

        // ---------------------------------------------------------------- numbers and dim

        /// <summary>Scale multiplier of a damage number t seconds after it spawns: PopScale
        /// easing out (cubic) to 1 over PopSeconds. 1 with the flag off.</summary>
        public static float PopScale(JuiceProfile p, float t)
        {
            if (!p.On || p.PopSeconds <= 0f || p.PopScale <= 1f) return 1f;
            if (t <= 0f) return p.PopScale;
            if (t >= p.PopSeconds) return 1f;
            float k = 1f - t / p.PopSeconds;
            return 1f + (p.PopScale - 1f) * k * k * k;
        }

        /// <summary>Brightness of the painted backdrop t seconds into a skill moment, 1 being
        /// undimmed: down to DimTo, held, back up.</summary>
        public static float DimAlpha(JuiceProfile p, float t)
        {
            if (!p.On || p.DimTo >= 1f) return 1f;
            float a = p.DimInSeconds;
            float b = a + p.DimHoldSeconds;
            float c = b + p.DimOutSeconds;
            if (t <= 0f || t >= c) return 1f;
            if (t < a) return a <= 0f ? p.DimTo : 1f + (p.DimTo - 1f) * (t / a);
            if (t < b) return p.DimTo;
            return p.DimOutSeconds <= 0f ? 1f : p.DimTo + (1f - p.DimTo) * ((t - b) / p.DimOutSeconds);
        }

        public static float DimSeconds(JuiceProfile p)
        {
            return p.DimInSeconds + p.DimHoldSeconds + p.DimOutSeconds;
        }

        /// <summary>Where a new number on a unit starts, above its anchor: the smallest offset at
        /// or above 0 that sits at least a line height from every number still live on that unit
        /// (liveYs are their current heights above the anchor). 0 with the flag off.</summary>
        public static float NextNumberOffset(JuiceProfile p, IList<float> liveYs)
        {
            if (!p.On || p.NumberLineHeight <= 0f || liveYs == null || liveYs.Count == 0) return 0f;
            List<float> sorted = new List<float>(liveYs);
            sorted.Sort();
            float candidate = 0f;
            for (int i = 0; i < sorted.Count; i++)
            {
                if (Math.Abs(candidate - sorted[i]) < p.NumberLineHeight) candidate = sorted[i] + p.NumberLineHeight;
            }
            return candidate;
        }

        /// <summary>True when a new number must retire the oldest one on its unit first.</summary>
        public static bool RetireOldestOnTarget(JuiceProfile p, int liveOnTarget)
        {
            return p.On && p.MaxNumbersPerTarget > 0 && liveOnTarget >= p.MaxNumbersPerTarget;
        }

        /// <summary>True when a new number must retire the oldest one overall first.</summary>
        public static bool RetireOldestOverall(JuiceProfile p, int liveTotal)
        {
            return p.On && p.MaxNumbers > 0 && liveTotal >= p.MaxNumbers;
        }

        // ---------------------------------------------------------------- the cap

        /// <summary>Whether BattleVfx may start one more quad of a kind with live quads already
        /// out. Cheap decoration is refused first (sparks at 65 percent of the pool, then impacts,
        /// rings and motes at 85, then slashes, bolts and auras at 95); a Priority quad is refused
        /// only when the pool is full. No cap with the flag off.</summary>
        public static bool Admit(JuiceProfile p, BudgetKind kind, int live)
        {
            if (!p.On || p.MaxQuads <= 0) return true;
            if (live >= p.MaxQuads) return false;
            switch (kind)
            {
                case BudgetKind.Spark: return live < p.MaxQuads * 13 / 20;
                case BudgetKind.Impact:
                case BudgetKind.Ring:
                case BudgetKind.Mote: return live < p.MaxQuads * 17 / 20;
                case BudgetKind.Slash:
                case BudgetKind.Bolt:
                case BudgetKind.Aura: return live < p.MaxQuads * 19 / 20;
                default: return true;
            }
        }
    }
}
