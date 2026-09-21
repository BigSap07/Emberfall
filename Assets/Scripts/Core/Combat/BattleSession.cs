using System;
using System.Collections.Generic;
using Emberfall.Core.Data;

namespace Emberfall.Core.Combat
{
    /// <summary>Which ability a hand-driven turn uses. Natural is what the engine would have
    /// chosen on its own, and is the only value a battle the game plays ever sees.</summary>
    public enum ManualChoice
    {
        Natural,
        Basic,
        Active
    }

    /// <summary>
    /// What came of one hand-driven action. Deliberately NOT part of the battle record: the record
    /// is the permanent thing the game replays and saves, and "why did the engine refuse my pick"
    /// is a question about a tool, not about a fight. Keeping it here is what let the whole session
    /// land without adding a single BattleEventType.
    /// </summary>
    public sealed class ActionReport
    {
        /// <summary>False when the action never ran at all; Refusal says why.</summary>
        public bool Accepted;

        /// <summary>Empty when accepted. Otherwise a sentence for a person, not a code.</summary>
        public string Refusal = "";

        /// <summary>Index into the session's event list where this action's events begin.</summary>
        public int FirstEventIndex;

        /// <summary>How many events this action produced.</summary>
        public int EventCount;

        /// <summary>
        /// What the engine did with a forced target: that it landed, that a Taunt answered
        /// instead, or that a restriction or concealment refused it. Empty when no target was
        /// forced. One line per single-enemy pick the action made.
        /// </summary>
        public readonly List<string> TargetNotes = new List<string>();
    }

    /// <summary>
    /// A FIGHT THAT IS STILL HAPPENING.
    ///
    /// CombatSimulation.Run resolves a whole battle and hands back the record. That is the right
    /// shape for the game - the battle screen is a player for a finished list and cannot change
    /// the outcome - and the wrong shape for a workbench, where the whole point is to stop the
    /// fight between two actions and push it somewhere by hand.
    ///
    /// So the same Sim is driven one action at a time. Run() is now literally Begin() followed by
    /// StepNatural() until the fight ends, which means "auto" in a tool is not a second engine
    /// that resembles the first: it IS the first, stopped between actions. There is no second
    /// resolution path anywhere in here, and a hand-driven action runs the same TakeTurn body a
    /// natural one does.
    ///
    /// THE SEMANTICS OF A HAND-DRIVEN ACTION, stated once here and shown once in the tool:
    ///   * It is a TURN, not a free swing. It ticks that unit's damage-over-time, repays its turn
    ///     debt, fires its TurnStart passives, resolves the ability, fires the after-action
    ///     passives, raises the turn-end broadcasts and expires that unit's statuses - the whole
    ///     pipeline, in the order a natural turn uses it.
    ///   * It SPENDS that unit's natural turn for the round. A unit driven by hand does not then
    ///     also act on its own when the walk reaches it; acting twice in a round would not be a
    ///     fight worth debugging. A unit that has ALREADY acted naturally can still be driven, and
    ///     that is an extra turn on purpose.
    ///   * Stun and Silence still refuse. A stunned unit cannot be driven and a silenced one
    ///     cannot be made to cast; the session says so rather than quietly substituting a basic,
    ///     which is what the natural path does and what would hide the status from the person
    ///     asking why the active did not happen.
    ///   * An active still empties the bar. IgnoreEnergy lets a cast happen below cost; it does
    ///     not make the cast free.
    ///   * A forced target replaces the single-enemy pick made by that unit's own ability, and
    ///     nothing else. Every ally mode, link mode and multi-target mode resolves as authored,
    ///     Provoked and the Taunt anchor still win, and a Restricted or Concealed body is refused.
    ///     Every one of those outcomes is reported.
    ///   * EndRound ends the round where it stands and opens the next. Units that had not acted do
    ///     not act. This engine has no separate end-of-round work - durations come off at the close
    ///     of each unit's own turn and the per-round bookkeeping happens at the top of the next
    ///     round - so opening the next round IS the round-end processing.
    ///   * A direct state edit (set HP, set energy, kill, revive) is a CHEAT, not an event in the
    ///     fiction. It moves the number and emits the plain event so the view keeps up, and it
    ///     fires no passive and no death dispatch. Nothing in the game can produce one.
    /// </summary>
    public sealed class BattleSession
    {
        private readonly CombatSimulation.ISessionDriver _sim;

        internal BattleSession(CombatSimulation.ISessionDriver sim)
        {
            _sim = sim;
            _sim.Begin();
        }

        /// <summary>The live record, appended to as the fight happens. The view plays it
        /// incrementally by remembering how far it has got.</summary>
        public BattleResult Result { get { return _sim.Result; } }

        public IList<BattleEvent> Events { get { return _sim.Result.Events; } }

        public bool Finished { get { return _sim.Finished; } }

        public int Round { get { return _sim.Round; } }

        /// <summary>Whether a hand-driven active may be cast below its energy cost. On by default
        /// in the tool, because "does this kit do what its text says" is not a question about
        /// whether the bar happens to be full.</summary>
        public bool IgnoreEnergyForManualCasts = true;

        /// <summary>Advances exactly one natural action, whatever the engine would have done next.
        /// Crossing a round boundary costs no step of its own.</summary>
        public void StepNatural() { _sim.StepNatural(); }

        /// <summary>Runs the fight to its end the way Run() does.</summary>
        public void RunToEnd() { while (!_sim.Finished) _sim.StepNatural(); }

        /// <summary>Resolves one action chosen by hand. See the semantics on this class.</summary>
        public ActionReport Act(int actorId, ManualChoice choice, int forcedPrimaryTargetId)
        {
            return _sim.Act(actorId, choice, forcedPrimaryTargetId, IgnoreEnergyForManualCasts);
        }

        public void EndRound() { _sim.EndRound(); }

        // ------------------------------------------------------------------ live state, for a UI

        public IList<UnitView> Units { get { return _sim.UnitViews(); } }

        public UnitView Unit(int id) { return _sim.UnitView(id); }

        /// <summary>The ids of the units yet to act this round, in the order they will act.</summary>
        public IList<int> UpcomingTurns { get { return _sim.Upcoming(); } }

        // ------------------------------------------------------------------ direct edits

        public void SetHp(int unitId, float hp) { _sim.SetHp(unitId, hp); }
        public void SetHpFraction(int unitId, float fraction) { _sim.SetHpFraction(unitId, fraction); }
        public void SetEnergy(int unitId, int energy) { _sim.SetEnergy(unitId, energy); }
        public void Kill(int unitId) { _sim.Kill(unitId); }
        public void Revive(int unitId) { _sim.Revive(unitId); }
    }

    /// <summary>
    /// A unit as a screen needs it: everything that changes during a fight, flattened. UnitSnapshot
    /// is the battle-start picture and does not move; this is the live one.
    /// </summary>
    public sealed class UnitView
    {
        public int Id;
        public bool IsPlayer;
        public int Slot;
        public string Name = "";
        public string DefId = "";
        public bool Alive;
        public float Hp;
        public float MaxHp;
        public float Shield;
        public int Energy;
        public int EnergyMax;
        public float Attack;
        public float Defense;
        public float Speed;
        public float CritChance;
        public float CritDamage;
        public string BasicName = "";
        public string ActiveName = "";
        public int ActiveCost;
        public readonly List<StatusView> Statuses = new List<StatusView>();
    }

    public sealed class StatusView
    {
        public StatusKind Kind;
        public int RoundsLeft;
        public int Stacks;
        public float Magnitude;
        public int SourceId;
        public int Counter;
        public float Stored;
    }
}
