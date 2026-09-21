using System;
using System.Collections.Generic;
using Emberfall.Core.Data;

namespace Emberfall.Core.Combat
{
    public static partial class CombatSimulation
    {
        /// <summary>
        /// Opens a fight and leaves it standing. Nothing advances until something asks it to.
        ///
        /// The same Sim Run() builds. This is not a second entry point into a second engine; it is
        /// the same object with its round loop paused, which is exactly why Run() itself is now
        /// written in terms of it.
        /// </summary>
        public static BattleSession Begin(BattleRequest request)
        {
            return new BattleSession(new Sim(request));
        }

        /// <summary>
        /// What a session may ask of the simulation. Sim is private - it has been since the engine
        /// was written and there is no reason to widen it - so the driving surface is named here
        /// and implemented there, rather than making the whole simulation public to expose six
        /// methods.
        /// </summary>
        public interface ISessionDriver
        {
            BattleResult Result { get; }
            bool Finished { get; }
            int Round { get; }

            void Begin();
            void StepNatural();
            void EndRound();
            ActionReport Act(int actorId, ManualChoice choice, int forcedPrimaryTargetId, bool ignoreEnergy);

            IList<UnitView> UnitViews();
            UnitView UnitView(int id);
            IList<int> Upcoming();

            void SetHp(int unitId, float hp);
            void SetHpFraction(int unitId, float fraction);
            void SetEnergy(int unitId, int energy);
            void Kill(int unitId);
            void Revive(int unitId);
        }

        private sealed partial class Sim : ISessionDriver
        {
            // ============================================================== one chosen action

            /// <summary>
            /// Resolves one action a person picked, through the same TakeTurn a natural turn uses.
            /// Everything it refuses, it refuses out loud: the alternative is a tool that silently
            /// substitutes a basic for a silenced active and leaves the person wondering why the
            /// kit "did nothing".
            /// </summary>
            public ActionReport Act(int actorId, ManualChoice choice, int forcedPrimaryTargetId, bool ignoreEnergy)
            {
                ActionReport r = new ActionReport();
                r.FirstEventIndex = _result.Events.Count;

                if (_phase == Phase.NotStarted) { r.Refusal = "the fight has not begun"; return r; }
                if (_phase == Phase.Done) { r.Refusal = "the fight is over"; return r; }

                CombatUnit unit = FindUnit(actorId);
                if (unit == null) { r.Refusal = "there is no unit with id " + actorId; return r; }
                if (!unit.Alive) { r.Refusal = unit.Name + " is dead"; return r; }

                if (unit.Has(StatusKind.Stun))
                {
                    r.Refusal = unit.Name + " is Stunned and cannot act this round";
                    return r;
                }

                if (choice == ManualChoice.Active)
                {
                    if (unit.Active == null) { r.Refusal = unit.Name + " has no active skill"; return r; }
                    if (unit.Has(StatusKind.Silence))
                    { r.Refusal = unit.Name + " is Silenced and cannot cast"; return r; }
                    if (!ignoreEnergy && unit.Energy < unit.Active.EnergyCost)
                    {
                        r.Refusal = unit.Name + " has " + unit.Energy + " energy and "
                                    + unit.Active.Name + " costs " + unit.Active.EnergyCost;
                        return r;
                    }
                }
                else if (choice == ManualChoice.Basic && unit.Basic == null)
                {
                    r.Refusal = unit.Name + " has no basic attack";
                    return r;
                }

                // A turn happens INSIDE a round. Driving the very first action before the natural
                // walk has opened round 1 would stamp the events round 0 and skip BeginRound, so
                // the round is opened here first - which is what StepNatural would have done.
                if (_phase == Phase.RoundBegin)
                {
                    _round++;
                    if (_round > _cfg.MaxRounds)
                    {
                        EndBattle();
                        r.Refusal = "the fight has run out of rounds";
                        return r;
                    }
                    OpenRound();
                    r.FirstEventIndex = _result.Events.Count;
                }

                _forcedNotes = r.TargetNotes;
                try
                {
                    TakeTurn(unit, choice, forcedPrimaryTargetId, ignoreEnergy);
                }
                finally
                {
                    _forcedNotes = null;
                    CloseForcedPick();
                }

                // The turn it just took is the turn it had. A unit driven by hand does not also act
                // on its own when the natural walk reaches it.
                _manualActed.Add(unit.Id);

                r.Accepted = true;
                r.EventCount = _result.Events.Count - r.FirstEventIndex;
                if (IsBattleOver()) EndBattle();
                return r;
            }

            // ============================================================== live state, for a UI

            public IList<UnitView> UnitViews()
            {
                List<UnitView> views = new List<UnitView>();
                for (int i = 0; i < _units.Count; i++) views.Add(MakeView(_units[i]));
                return views;
            }

            public UnitView UnitView(int id)
            {
                CombatUnit u = FindUnit(id);
                return u == null ? null : MakeView(u);
            }

            /// <summary>
            /// Who is still to act this round, in order. Empty between rounds, which is honest:
            /// the order is re-rolled at the top of every round and does not exist before then.
            /// </summary>
            public IList<int> Upcoming()
            {
                List<int> ids = new List<int>();
                if (_order == null) return ids;
                for (int i = _orderIndex; i < _order.Count; i++)
                {
                    CombatUnit u = _order[i];
                    if (u.Alive && !_manualActed.Contains(u.Id)) ids.Add(u.Id);
                }
                return ids;
            }

            private static UnitView MakeView(CombatUnit u)
            {
                UnitView v = new UnitView();
                v.Id = u.Id;
                v.IsPlayer = u.IsPlayer;
                v.Slot = u.Slot;
                v.Name = u.Name;
                v.DefId = u.DefId;
                v.Alive = u.Alive;
                v.Hp = u.Hp;
                v.MaxHp = u.EffectiveMaxHp;
                v.Shield = u.Shield;
                v.Energy = u.Energy;
                v.EnergyMax = u.EnergyMax;
                v.Attack = u.EffectiveAttack;
                v.Defense = u.EffectiveDefense;
                v.Speed = u.EffectiveSpeed;
                v.CritChance = u.CritChance;
                v.CritDamage = u.CritDamage;
                v.BasicName = u.Basic != null ? u.Basic.Name : "";
                v.ActiveName = u.Active != null ? u.Active.Name : "";
                v.ActiveCost = u.Active != null ? u.Active.EnergyCost : 0;

                // Stacks in this engine are separate entries of the same kind, which is what
                // CountOf counts, so the view folds them rather than inventing a stack field the
                // simulation does not have.
                for (int i = 0; i < u.Statuses.Count; i++)
                {
                    StatusInstance st = u.Statuses[i];
                    StatusView found = null;
                    for (int j = 0; j < v.Statuses.Count; j++)
                        if (v.Statuses[j].Kind == st.Kind) { found = v.Statuses[j]; break; }
                    if (found == null)
                    {
                        found = new StatusView();
                        found.Kind = st.Kind;
                        found.Magnitude = st.Magnitude;
                        found.SourceId = st.SourceId;
                        v.Statuses.Add(found);
                    }
                    found.Stacks++;
                    if (st.RoundsLeft > found.RoundsLeft) found.RoundsLeft = st.RoundsLeft;
                    found.Counter += st.Charges;
                    found.Stored += st.Stored;
                }
                return v;
            }

            // ============================================================== direct edits
            //
            // A CHEAT, NOT AN EVENT IN THE FICTION. Each of these moves a number and emits the
            // plain event that says the number moved, so a view replaying the list keeps up. None
            // of them fires a passive, opens a death dispatch or touches the RNG. Nothing the game
            // can do produces one, and nothing in the engine can be reached through one.

            public void SetHp(int unitId, float hp)
            {
                CombatUnit u = FindUnit(unitId);
                if (u == null) return;
                float max = u.EffectiveMaxHp;
                if (hp < 0f) hp = 0f;
                if (hp > max) hp = max;
                float delta = hp - u.Hp;
                u.Hp = hp;

                if (delta < 0f)
                {
                    BattleEvent e = Emit(BattleEventType.Damage, -1, unitId);
                    e.Value = (int)Math.Round(-delta);
                }
                else if (delta > 0f)
                {
                    BattleEvent e = Emit(BattleEventType.Heal, -1, unitId);
                    e.Value = (int)Math.Round(delta);
                }

                if (hp <= 0f && u.Alive) KillQuietly(u);
                else if (hp > 0f && !u.Alive) ReviveQuietly(u, hp);
            }

            public void SetHpFraction(int unitId, float fraction)
            {
                CombatUnit u = FindUnit(unitId);
                if (u == null) return;
                SetHp(unitId, u.EffectiveMaxHp * fraction);
            }

            public void SetEnergy(int unitId, int energy)
            {
                CombatUnit u = FindUnit(unitId);
                if (u == null) return;
                if (energy < 0) energy = 0;
                if (energy > u.EnergyMax) energy = u.EnergyMax;
                u.Energy = energy;
                EmitEnergy(u);
            }

            public void Kill(int unitId)
            {
                CombatUnit u = FindUnit(unitId);
                if (u == null || !u.Alive) return;
                float had = u.Hp;
                u.Hp = 0f;
                BattleEvent d = Emit(BattleEventType.Damage, -1, unitId);
                d.Value = (int)Math.Round(had);
                KillQuietly(u);
            }

            public void Revive(int unitId)
            {
                CombatUnit u = FindUnit(unitId);
                if (u == null || u.Alive) return;
                ReviveQuietly(u, u.EffectiveMaxHp);
                BattleEvent h = Emit(BattleEventType.Heal, -1, unitId);
                h.Value = (int)Math.Round(u.Hp);
            }

            private void KillQuietly(CombatUnit u)
            {
                u.Hp = 0f;
                u.Alive = false;
                u.Shield = 0f;
                u.Statuses.Clear();
                Emit(BattleEventType.Death, -1, u.Id);
                if (IsBattleOver()) EndBattle();
            }

            private void ReviveQuietly(CombatUnit u, float hp)
            {
                u.Alive = true;
                u.Hp = hp <= 0f ? 1f : hp;
            }
        }
    }
}
