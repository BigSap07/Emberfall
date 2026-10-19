using System;
using System.Collections.Generic;
using Emberfall.Core.Data;

namespace Emberfall.Battle
{
    /// <summary>
    /// The decisions of the R15 feel pass, as pure functions of a FeelProfile. Free of UnityEngine
    /// on purpose: Tools/DevHarness compiles this file, so the switch, the rollback and the numbers
    /// are proven by build-check without the editor (FeelChecks.cs).
    ///
    /// Every function given FeelProfile.V1 returns the answer the R12 battle gave, so the battle code
    /// can ask these when Feel.V2 and the harness can pin both sides.
    /// </summary>
    public static class FeelMath
    {
        // ---------------------------------------------------------------- the eight classes

        /// <summary>The event classes the pass gives one look and one cue each. Death keeps R12.</summary>
        public static readonly string[] Classes = { "hit", "crit", "skill", "heal", "buff", "debuff", "shield", "death" };

        /// <summary>The look class of a record moment, or "" for a moment with no class.</summary>
        public static string ClassOf(MomentKind kind)
        {
            switch (kind)
            {
                case MomentKind.Hit: return "hit";
                case MomentKind.Crit: return "crit";
                case MomentKind.Skill: return "skill";
                case MomentKind.Heal: return "heal";
                case MomentKind.Buff: return "buff";
                case MomentKind.Debuff: return "debuff";
                case MomentKind.Shield: return "shield";
                case MomentKind.Death: return "death";
            }
            return "";
        }

        /// <summary>The class a cue id belongs to, for the cue log: battle.hit is "hit", battle.crit
        /// "crit", battle.impact.* "skill", battle.cast and battle.cast.* "cast", and so on. "" for a
        /// cue that is not a battle one.</summary>
        public static string CueClass(string cueId)
        {
            if (cueId == null || !cueId.StartsWith("battle.", StringComparison.Ordinal)) return "";
            if (cueId.StartsWith("battle.impact.", StringComparison.Ordinal)) return "skill";
            if (cueId == "battle.cast" || cueId.StartsWith("battle.cast.", StringComparison.Ordinal)) return "cast";
            return cueId.Substring("battle.".Length);
        }

        /// <summary>Priority of a class when a unit is at its effect cap: a stronger class retires
        /// the oldest weaker one, a weaker one is dropped.</summary>
        public static int ClassPriority(string cls)
        {
            switch (cls)
            {
                case "skill": return 3;
                case "crit": return 2;
            }
            return 1;   // hit, heal, buff, debuff, shield: the oldest of them goes first when a unit is full
        }

        /// <summary>
        /// The per-unit effect cap. live holds the priorities of the effects alive on one unit and
        /// born their start times. Returns -1 when the new effect may start, an index when that live
        /// effect must be retired to make room, and -2 when the new effect is dropped. A cap of 0 is
        /// no cap.
        /// </summary>
        public static int CapDecision(int cap, IList<int> live, IList<float> born, int priority)
        {
            if (cap <= 0 || live.Count < cap) return -1;
            int weakest = -1;
            for (int i = 0; i < live.Count; i++)
            {
                if (live[i] > priority) continue;
                if (weakest < 0 || live[i] < live[weakest] || (live[i] == live[weakest] && born[i] < born[weakest])) weakest = i;
            }
            return weakest < 0 ? -2 : weakest;
        }

        /// <summary>A colour channel pulled toward white by amount (0 leaves it alone).</summary>
        public static float TowardWhite(float channel, float amount)
        {
            return channel + (1f - channel) * Math.Max(0f, Math.Min(1f, amount));
        }

        // ---------------------------------------------------------------- numbers

        /// <summary>Scale of a floating number. v2 grows it; v1 is R12.</summary>
        public static float NumberScale(FeelProfile p, float scale)
        {
            return scale * (p.V2 ? p.NumberScale : 1f);
        }

        // ---------------------------------------------------------------- hit flash

        /// <summary>The lerp toward white a hit sets: a tint, not a fill.</summary>
        public static float FlashPeak(FeelProfile p, bool crit)
        {
            return crit ? p.FlashPeakCrit : p.FlashPeakNormal;
        }

        /// <summary>Flash lerp lost per second so that a hit's flash, whatever its peak, reaches zero
        /// in exactly FlashSeconds. Hit stop does not pause it (UnitActor decays it before the
        /// freeze test), so the flash is never held past this.</summary>
        public static float FlashDecayRate(FeelProfile p, bool crit)
        {
            float seconds = Math.Max(0.001f, p.FlashSeconds);
            return FlashPeak(p, crit) / seconds;
        }

        /// <summary>The flash lerp t seconds after a hit, run through the same arithmetic the actor
        /// runs frame by frame: a straight fall from the peak to zero.</summary>
        public static float FlashAt(FeelProfile p, bool crit, float t)
        {
            float v = FlashPeak(p, crit) - FlashDecayRate(p, crit) * Math.Max(0f, t);
            return v < 0f ? 0f : v;
        }
    }
}
