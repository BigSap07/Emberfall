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
        /// NO LONGER EMITTED. Gale's kill-driven turn re-entry was the only thing that produced it,
        /// and that rule has been removed along with the rest of the engine-owned aspect rules; the
        /// aspect is the damage chart, armor attunement and flavour now.
        ///
        /// The member stays. It is in the permanent record of every battle resolved before the
        /// removal, the member's ORDINAL is what a saved event carries, and deleting it would
        /// renumber every type declared after it. Anything that grants a unit a second turn in a
        /// round in future should re-use this rather than appending a synonym.
        ///
        /// When it was emitted: SourceId killed something and was put back into the current round's
        /// order, emitted inside the victim's death rather than at the extra turn itself, with
        /// Value carrying how many extra turns that unit had taken this round.
        /// </summary>
        ExtraTurn,

        /// <summary>
        /// TargetId's turn moved inside the round now being walked, among the units that have not
        /// acted yet. Value is signed - negative advanced, positive delayed - and SourceId is the
        /// caster whose effect moved it, or -1 where there is none. Status is None: no status
        /// caused this, an authored TurnShift or SwapTurnOrder effect did.
        ///
        /// Genuinely absent rather than unread: the resulting turn sequence is a valid sequence on
        /// its own, so nothing in the event list distinguishes "something pushed this unit back"
        /// from "this unit was simply slower this round" unless the shift is recorded when it
        /// happens.
        ///
        /// Introduced for Gale's intra-round Slow shift, which is gone. The event is not: authored
        /// turn manipulation emits it, and reusing it keeps one vocabulary for one idea.
        /// </summary>
        TurnOrderShift,

        /// <summary>
        /// NO LONGER EMITTED. It existed for the Lumen targeting anchor alone - a redirect onto a
        /// unit whose only claim was its aspect, which the record could not otherwise tell from a
        /// genuine lowest-HP or front-most pick. That rule has been removed. The anchor that
        /// remains is Taunt, and a Taunt holder wears its own status tag, so the redirect explains
        /// itself on the target's bar and this would be a second, redundant telling of it.
        ///
        /// The member stays for the same reason ExtraTurn's does: it is in the permanent record of
        /// every battle resolved before the removal, and deleting it would renumber every type
        /// declared after it.
        ///
        /// When it was emitted: SourceId's single-enemy ability resolved onto TargetId because
        /// TargetId was Lumen, with Value carrying the TargetMode the ability had been aiming at.
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

        /// <summary>
        /// A status entry moved from one carrier to another. SourceId is the unit it came from,
        /// TargetId the unit it landed on; Status and Duration are the entry as it crossed, and
        /// Value carries the Id of whoever originally applied it, which is otherwise unrecoverable
        /// from the event alone once the old carrier's list has been cleared.
        ///
        /// Genuinely absent from the record rather than unread. A debuff appearing on a fresh unit
        /// with no attack and no cast anywhere near it is indistinguishable from a screen bug
        /// unless something in the list says it was deliberate and says where it came from.
        ///
        /// Distinct from StatusApplied on purpose: nobody applied this. Reporting a move as an
        /// application would make Debrief's status clause count it as a second debuff somebody
        /// landed and never cast, and would put a cause in the record that did not happen.
        ///
        /// Introduced for Umbra's mark jumping off a dying carrier, which is gone. The authored
        /// MoveDebuffs and MoveBuffs operations emit it now, through the one MoveStatus mover.
        /// </summary>
        StatusSpread,

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
