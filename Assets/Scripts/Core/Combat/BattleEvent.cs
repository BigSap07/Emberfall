using System.Collections.Generic;
using Emberfall.Core.Data;

namespace Emberfall.Core.Combat
{
    public enum BattleEventType
    {
        BattleStart,
        RoundStart,
        TurnStart,
        BasicAttack,
        AbilityCast,
        PassiveTrigger,
        Damage,
        Heal,
        ShieldGain,
        ShieldAbsorb,
        StatusApplied,

        /// <summary>
        /// A harmful status was rolled for and did not land. Carries SourceId, TargetId and Status,
        /// the same three fields StatusApplied carries, so the screen can say which affliction
        /// failed and who shrugged it off.
        ///
        /// This exists because DESIGN-BATTLE-READABILITY requires the screen to read the record
        /// rather than recompute it, and without this event a resist is invisible: the debuff
        /// simply never appears and nothing says why. Emitted only for harmful statuses, which are
        /// the only ones Effect Resist contests - a beneficial status failing its own authored
        /// chance is a whiff, not a resist, and calling it one would be a lie in the record.
        /// </summary>
        StatusResisted,

        StatusExpired,
        EnergyChanged,
        Death,

        /// <summary>
        /// Gale (DESIGN-ASPECTS section 1): the unit named by SourceId killed something and has been
        /// put back into the current round's turn order. Emitted at the moment of re-entry, which is
        /// inside the victim's death, not at the extra turn itself - the extra turn emits its own
        /// TurnStart later, exactly like a scheduled one.
        ///
        /// This exists because the re-entry is otherwise unreadable, not merely unread. A second
        /// TurnStart for the same unit in the same round is the only trace it leaves, and that is
        /// indistinguishable from a screen bug without a record saying it was deliberate.
        /// Value carries how many extra turns that unit has taken this round, so the screen can say
        /// which one of a bounded sequence this is.
        /// </summary>
        ExtraTurn,

        /// <summary>
        /// Gale (DESIGN-ASPECTS section 1): a Slow from a Gale source moved TargetId's turn later
        /// inside the round it was applied in, rather than only affecting the next round's sort.
        /// Value is how many places later it moved among the units that have not acted yet.
        ///
        /// Also genuinely absent rather than unread: the resulting turn sequence is a valid sequence
        /// on its own, so nothing in the event list distinguishes "Gale pushed this unit back" from
        /// "this unit was simply slower this round" unless the shift is recorded when it happens.
        /// </summary>
        TurnOrderShift,

        /// <summary>
        /// Lumen (DESIGN-ASPECTS section 1, as re-weighted by DESIGN-WORLD section 1): SourceId's
        /// single-enemy ability was resolved onto TargetId because TargetId is Lumen and answers for
        /// its whole side, not because the ability's own TargetMode picked it. Value carries that
        /// TargetMode, so the screen can say what the ability was aiming at before the answer
        /// overrode it.
        ///
        /// Emitted only where the Lumen rule alone caused the redirect. When the anchor is also
        /// carrying Taunt, the status tag on its bar already explains the hit and this would be a
        /// second, redundant telling of it.
        ///
        /// Genuinely absent from the record rather than unread: the Damage event that follows shows
        /// the hit landing on the Lumen unit, and nothing else distinguishes "it answered for its
        /// side" from "it really was the lowest-HP or front-most pick".
        /// </summary>
        TargetAnchored,

        /// <summary>
        /// Two units on the same side swapped formation slots. SourceId is the caster.
        /// TargetId is the first swapped unit; Value holds the Id of the second.
        /// Without this, the swap is invisible: units silently reposition and nothing
        /// in the record explains why — violating DESIGN-BATTLE-READABILITY's rule that
        /// the screen reads the record rather than recomputing the fight.
        /// </summary>
        PositionSwapped,

        BattleEnd
    }

    /// <summary>
    /// The simulation's only output. The battle view walks this list and animates it;
    /// it never recomputes anything.
    /// </summary>
    public sealed class BattleEvent
    {
        public BattleEventType Type;
        public int Round;
        public int SourceId = -1;
        public int TargetId = -1;
        public int Value;
        public bool Critical;
        public string AbilityId;
        public string AbilityName;
        public string Vfx;
        public StatusKind Status = StatusKind.None;
        public int Duration;
        public bool Victory;

        /// <summary>Damage only: AspectChart.Multiplier(source.Aspect, target.Aspect) at the moment of the hit. 1 for non-aspect damage (e.g. burn ticks).</summary>
        public float AspectFactor = 1f;

        public static BattleEvent Make(BattleEventType type, int round)
        {
            BattleEvent e = new BattleEvent();
            e.Type = type;
            e.Round = round;
            return e;
        }
    }

    /// <summary>Immutable picture of a unit at battle start, used to build the visual scene.</summary>
    public sealed class UnitSnapshot
    {
        public int Id;
        public bool IsPlayer;
        public int Slot;
        public string DefId = "";
        public string Name = "";
        public int Level = 1;
        public float MaxHp;
        public float Attack;
        public float Defense;
        public float Speed;
        public int Power;
        public Aspect Aspect = Aspect.Ember;
        public HeroRole Role = HeroRole.Striker;
        public int Stars = 5;
        public bool IsBoss;
        public VisualDef Visual = new VisualDef();
        public string ActiveAbilityName = "";
        public int EnergyMax = 100;
        public int StartEnergy;
    }

    public sealed class BattleResult
    {
        public bool Victory;
        public bool TimedOut;
        public int Rounds;
        public int Seed;
        public string StageId = "";
        public float PlayerHpRatio;
        public float EnemyHpRatio;
        public readonly List<BattleEvent> Events = new List<BattleEvent>();
        public readonly List<UnitSnapshot> Units = new List<UnitSnapshot>();

        public UnitSnapshot FindUnit(int id)
        {
            for (int i = 0; i < Units.Count; i++) if (Units[i].Id == id) return Units[i];
            return null;
        }
    }
}
