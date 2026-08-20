using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;

namespace Emberfall.Core.Combat
{
    public sealed class BattleRequest
    {
        public ContentDatabase Content;
        public List<HeroInstance> Team = new List<HeroInstance>();
        public StageDef Stage;
        public int Seed;
    }

    /// <summary>
    /// The whole fight, resolved up front into a list of events.
    /// No Unity types, no coroutines, no frame timing - which is exactly why it can be
    /// unit tested, replayed and fast-forwarded.
    /// </summary>
    public static class CombatSimulation
    {
        private const int MaxPassiveDepth = 2;

        public static BattleResult Run(BattleRequest request)
        {
            Sim sim = new Sim(request);
            return sim.Execute();
        }

        private sealed class Sim
        {
            private readonly BattleRequest _request;
            private readonly BalanceConfig _cfg;
            private readonly DeterministicRng _rng;
            private readonly List<CombatUnit> _units = new List<CombatUnit>();
            private readonly BattleResult _result = new BattleResult();
            private int _round;

            public Sim(BattleRequest request)
            {
                _request = request;
                _cfg = request.Content != null ? request.Content.Balance : new BalanceConfig();
                _rng = new DeterministicRng(request.Seed);
                _result.Seed = request.Seed;
                _result.StageId = request.Stage != null ? request.Stage.Id : "";
            }

            // ------------------------------------------------------------------ setup

            public BattleResult Execute()
            {
                BuildUnits();
                for (int i = 0; i < _units.Count; i++) _result.Units.Add(_units[i].ToSnapshot());

                Emit(BattleEventType.BattleStart, -1, -1);

                List<CombatUnit> startOrder = TurnOrder();
                for (int i = 0; i < startOrder.Count; i++) TriggerPassive(startOrder[i], PassiveTrigger.BattleStart, 0);

                for (_round = 1; _round <= _cfg.MaxRounds; _round++)
                {
                    BattleEvent roundEvent = Emit(BattleEventType.RoundStart, -1, -1);
                    roundEvent.Value = _round;

                    List<CombatUnit> order = TurnOrder();
                    for (int i = 0; i < order.Count; i++)
                    {
                        CombatUnit unit = order[i];
                        if (!unit.Alive) continue;
                        TakeTurn(unit);
                        if (IsBattleOver()) break;
                    }
                    if (IsBattleOver()) break;
                }

                Finish();
                return _result;
            }

            private void BuildUnits()
            {
                int id = 0;
                List<HeroInstance> team = _request.Team;
                int teamSize = Math.Min(team.Count, _cfg.MaxTeamSize);
                for (int i = 0; i < teamSize; i++)
                {
                    HeroInstance hero = team[i];
                    if (hero == null || hero.Def == null) continue;
                    UnitStats stats = StatMath.ForHero(hero.Def, hero.Level);
                    _units.Add(CombatUnit.FromDef(hero.Def, stats, hero.Level, true, i, id++, _cfg.EnergyMax));
                }

                if (_request.Stage != null && _request.Content != null)
                {
                    List<EnemySpawn> spawns = _request.Stage.Enemies;
                    for (int i = 0; i < spawns.Count && i < _cfg.MaxTeamSize; i++)
                    {
                        EnemyDef def = _request.Content.GetEnemy(spawns[i].EnemyId);
                        if (def == null) continue;
                        UnitStats stats = StatMath.ForEnemy(def, spawns[i].Level);
                        _units.Add(CombatUnit.FromDef(def, stats, spawns[i].Level, false, i, id++, _cfg.EnergyMax));
                    }
                }
            }

            // ------------------------------------------------------------------ turn loop

            private List<CombatUnit> TurnOrder()
            {
                List<CombatUnit> order = new List<CombatUnit>();
                for (int i = 0; i < _units.Count; i++) if (_units[i].Alive) order.Add(_units[i]);
                // re-rolled every call (once per round) so who acts first isn't fixed for the whole battle
                for (int i = 0; i < order.Count; i++) order[i].RoundSpeedJitter = _rng.Spread(_cfg.SpeedVariance);
                order.Sort(CompareTurnOrder);
                return order;
            }

            private static int CompareTurnOrder(CombatUnit a, CombatUnit b)
            {
                int bySpeed = b.TurnOrderSpeed.CompareTo(a.TurnOrderSpeed);
                if (bySpeed != 0) return bySpeed;
                if (a.IsPlayer != b.IsPlayer) return a.IsPlayer ? -1 : 1;
                if (a.Slot != b.Slot) return a.Slot.CompareTo(b.Slot);
                return a.Id.CompareTo(b.Id);
            }

            private void TakeTurn(CombatUnit unit)
            {
                Emit(BattleEventType.TurnStart, unit.Id, unit.Id);

                TickDamageOverTime(unit);
                if (!unit.Alive || IsBattleOver()) { ExpireStatuses(unit); return; }

                TriggerPassive(unit, PassiveTrigger.TurnStart, 0);
                if (!unit.Alive || IsBattleOver()) { ExpireStatuses(unit); return; }

                bool usedActive = unit.Active != null && unit.Energy >= unit.Active.EnergyCost;
                if (usedActive)
                {
                    unit.Energy = 0;
                    BattleEvent cast = Emit(BattleEventType.AbilityCast, unit.Id, unit.Id);
                    cast.AbilityId = unit.Active.Id;
                    cast.AbilityName = unit.Active.Name;
                    cast.Vfx = unit.Active.Vfx;
                    EmitEnergy(unit);
                    ResolveAbility(unit, unit.Active, 0);
                }
                else
                {
                    AbilityDef basic = unit.Basic;
                    BattleEvent attack = Emit(BattleEventType.BasicAttack, unit.Id, unit.Id);
                    attack.AbilityId = basic.Id;
                    attack.AbilityName = basic.Name;
                    attack.Vfx = basic.Vfx;
                    ResolveAbility(unit, basic, 0);

                    if (unit.Alive)
                    {
                        AddEnergy(unit, _cfg.EnergyPerBasicAttack);
                        TriggerPassive(unit, PassiveTrigger.AfterBasicAttack, 0);
                    }
                }

                ExpireStatuses(unit);
            }

            private void TickDamageOverTime(CombatUnit unit)
            {
                for (int i = 0; i < unit.Statuses.Count; i++)
                {
                    StatusInstance s = unit.Statuses[i];
                    if (s.Kind == StatusKind.Burn)
                    {
                        int damage = (int)Math.Round(s.Magnitude, MidpointRounding.AwayFromZero);
                        if (damage < 1) damage = 1;
                        DealRawDamage(s.SourceId, unit, damage, false, "burn", StatusKind.Burn);
                        if (!unit.Alive) return;
                    }
                    else if (s.Kind == StatusKind.Regen)
                    {
                        int heal = (int)Math.Round(s.Magnitude, MidpointRounding.AwayFromZero);
                        if (heal < 1) heal = 1;
                        ApplyHealValue(s.SourceId, unit, heal, "heal");
                    }
                }
            }

            private void ExpireStatuses(CombatUnit unit)
            {
                for (int i = unit.Statuses.Count - 1; i >= 0; i--)
                {
                    StatusInstance s = unit.Statuses[i];
                    s.RoundsLeft--;
                    if (s.RoundsLeft > 0) continue;
                    unit.Statuses.RemoveAt(i);
                    BattleEvent e = Emit(BattleEventType.StatusExpired, unit.Id, unit.Id);
                    e.Status = s.Kind;
                }
            }

            // ------------------------------------------------------------------ abilities

            private void ResolveAbility(CombatUnit caster, AbilityDef ability, int depth)
            {
                if (ability == null) return;
                for (int i = 0; i < ability.Effects.Count; i++)
                {
                    EffectDef effect = ability.Effects[i];
                    List<CombatUnit> targets = ResolveTargets(caster, effect.Target);
                    for (int t = 0; t < targets.Count; t++)
                    {
                        CombatUnit target = targets[t];
                        if (target == null) continue;
                        ApplyEffect(caster, target, effect, ability, depth);
                        if (IsBattleOver()) return;
                    }
                }
            }

            private void ApplyEffect(CombatUnit source, CombatUnit target, EffectDef effect, AbilityDef ability, int depth)
            {
                switch (effect.Kind)
                {
                    case EffectKind.Damage:
                    {
                        for (int hit = 0; hit < effect.Hits; hit++)
                        {
                            if (!target.Alive || !source.Alive) break;
                            ApplyDamageEffect(source, target, effect, ability, depth);
                        }
                        break;
                    }
                    case EffectKind.Heal:
                    {
                        if (!target.Alive) break;
                        float amount = source.EffectiveAttack * effect.Power + effect.Flat + source.MaxHp * effect.SelfHpRatio;
                        ApplyHealValue(source.Id, target, (int)Math.Round(amount, MidpointRounding.AwayFromZero), "heal");
                        break;
                    }
                    case EffectKind.Shield:
                    {
                        if (!target.Alive) break;
                        float amount = source.EffectiveAttack * effect.Power + effect.Flat + source.MaxHp * effect.SelfHpRatio;
                        int value = (int)Math.Round(amount, MidpointRounding.AwayFromZero);
                        if (value <= 0) break;
                        target.Shield += value;
                        target.AddOrRefresh(StatusKind.Shield, Math.Max(1, effect.Duration), 0f, source.Id);
                        BattleEvent e = Emit(BattleEventType.ShieldGain, source.Id, target.Id);
                        e.Value = value;
                        e.Vfx = "buff";
                        break;
                    }
                    case EffectKind.ApplyStatus:
                    {
                        if (!target.Alive || effect.Status == StatusKind.None) break;
                        if (!_rng.Chance(effect.Chance)) break;
                        float magnitude = effect.Magnitude;
                        if (effect.Status == StatusKind.Burn || effect.Status == StatusKind.Regen)
                            magnitude = source.EffectiveAttack * effect.Magnitude;
                        target.AddOrRefresh(effect.Status, Math.Max(1, effect.Duration), magnitude, source.Id);
                        BattleEvent e = Emit(BattleEventType.StatusApplied, source.Id, target.Id);
                        e.Status = effect.Status;
                        e.Duration = Math.Max(1, effect.Duration);
                        break;
                    }
                    case EffectKind.Cleanse:
                    {
                        if (!target.Alive) break;
                        int removed = 0;
                        for (int i = target.Statuses.Count - 1; i >= 0 && removed < effect.Hits; i--)
                        {
                            if (!StatusInfo.IsHarmful(target.Statuses[i].Kind)) continue;
                            BattleEvent e = Emit(BattleEventType.StatusExpired, source.Id, target.Id);
                            e.Status = target.Statuses[i].Kind;
                            target.Statuses.RemoveAt(i);
                            removed++;
                        }
                        break;
                    }
                    case EffectKind.EnergyGain:
                    {
                        if (!target.Alive) break;
                        int amount = (int)Math.Round(effect.Flat + effect.Power * 100f, MidpointRounding.AwayFromZero);
                        if (amount == 0) break;
                        AddEnergy(target, amount);
                        break;
                    }
                }
            }

            private void ApplyDamageEffect(CombatUnit source, CombatUnit target, EffectDef effect, AbilityDef ability, int depth)
            {
                float raw = source.EffectiveAttack * effect.Power + effect.Flat;

                raw *= AspectChart.Multiplier(source.Aspect, target.Aspect);

                if (source.Passive != null && source.Passive.BonusVsStatus != StatusKind.None
                    && target.Has(source.Passive.BonusVsStatus))
                {
                    raw *= 1f + source.Passive.BonusMultiplier;
                }

                bool critical = _rng.Chance(source.EffectiveCritChance);
                if (critical) raw *= source.CritDamage;

                float defense = target.EffectiveDefense;
                float mitigation = defense / (defense + _cfg.DefenseConstant + _cfg.DefensePerLevel * source.Level);
                raw *= 1f - mitigation;

                raw *= 1f + _rng.Spread(_cfg.DamageVariance);

                int damage = (int)Math.Round(raw, MidpointRounding.AwayFromZero);
                if (damage < _cfg.MinDamage) damage = (int)_cfg.MinDamage;

                source.LastTargetId = target.Id;
                string vfx = ability != null ? ability.Vfx : "impact";
                DealRawDamage(source.Id, target, damage, critical, vfx, StatusKind.None);

                if (critical && source.Alive) TriggerPassive(source, PassiveTrigger.OnCriticalHit, depth);
            }

            /// <summary>Single funnel for every point of damage in the game.</summary>
            private void DealRawDamage(int sourceId, CombatUnit target, int damage, bool critical, string vfx, StatusKind dotKind)
            {
                if (!target.Alive || damage <= 0) return;

                if (target.Shield > 0f)
                {
                    int absorbed = (int)Math.Min(target.Shield, damage);
                    if (absorbed > 0)
                    {
                        target.Shield -= absorbed;
                        damage -= absorbed;
                        BattleEvent shieldEvent = Emit(BattleEventType.ShieldAbsorb, sourceId, target.Id);
                        shieldEvent.Value = absorbed;
                        if (target.Shield <= 0f)
                        {
                            target.Shield = 0f;
                            target.Remove(StatusKind.Shield);
                            BattleEvent expired = Emit(BattleEventType.StatusExpired, target.Id, target.Id);
                            expired.Status = StatusKind.Shield;
                        }
                    }
                }

                if (damage <= 0) return;

                target.Hp -= damage;
                BattleEvent e = Emit(BattleEventType.Damage, sourceId, target.Id);
                e.Value = damage;
                e.Critical = critical;
                e.Vfx = vfx;
                e.Status = dotKind;

                if (sourceId >= 0) target.LastAttackerId = sourceId;

                if (target.Hp <= 0f)
                {
                    target.Hp = 0f;
                    target.Alive = false;
                    target.Statuses.Clear();
                    target.Shield = 0f;
                    Emit(BattleEventType.Death, sourceId, target.Id);

                    CombatUnit killer = FindUnit(sourceId);
                    if (killer != null && killer.Alive) TriggerPassive(killer, PassiveTrigger.OnKill, 0);
                    return;
                }

                AddEnergy(target, _cfg.EnergyOnDamaged);
                TriggerPassive(target, PassiveTrigger.OnDamaged, 0);
                TriggerPassive(target, PassiveTrigger.OnHpBelowThreshold, 0);
            }

            private void ApplyHealValue(int sourceId, CombatUnit target, int amount, string vfx)
            {
                if (!target.Alive || amount <= 0) return;
                float missing = target.MaxHp - target.Hp;
                if (missing <= 0f) return;
                int healed = (int)Math.Min(missing, amount);
                if (healed <= 0) return;
                target.Hp += healed;
                BattleEvent e = Emit(BattleEventType.Heal, sourceId, target.Id);
                e.Value = healed;
                e.Vfx = vfx;
            }

            private void AddEnergy(CombatUnit unit, int amount)
            {
                if (!unit.Alive || amount == 0) return;
                int before = unit.Energy;
                unit.Energy += amount;
                if (unit.Energy > unit.EnergyMax) unit.Energy = unit.EnergyMax;
                if (unit.Energy < 0) unit.Energy = 0;
                if (unit.Energy != before) EmitEnergy(unit);
            }

            private void EmitEnergy(CombatUnit unit)
            {
                BattleEvent e = Emit(BattleEventType.EnergyChanged, unit.Id, unit.Id);
                e.Value = unit.Energy;
            }

            // ------------------------------------------------------------------ passives

            private void TriggerPassive(CombatUnit owner, PassiveTrigger trigger, int depth)
            {
                if (owner == null || !owner.Alive) return;
                PassiveDef passive = owner.Passive;
                if (passive == null || passive.Trigger != trigger) return;
                if (passive.OncePerBattle && owner.PassiveFired) return;
                if (trigger == PassiveTrigger.OnHpBelowThreshold && owner.HpRatio > passive.Threshold) return;
                if (passive.Effects.Count == 0) return;
                if (depth >= MaxPassiveDepth) return;

                if (passive.OncePerBattle) owner.PassiveFired = true;

                BattleEvent e = Emit(BattleEventType.PassiveTrigger, owner.Id, owner.Id);
                e.AbilityId = passive.Id;
                e.AbilityName = passive.Name;
                e.Vfx = passive.Vfx;

                AbilityDef wrapper = new AbilityDef();
                wrapper.Id = passive.Id;
                wrapper.Name = passive.Name;
                wrapper.Vfx = passive.Vfx;
                wrapper.Effects = passive.Effects;
                ResolveAbility(owner, wrapper, depth + 1);
            }

            // ------------------------------------------------------------------ targeting

            private List<CombatUnit> ResolveTargets(CombatUnit caster, TargetMode mode)
            {
                List<CombatUnit> result = new List<CombatUnit>();
                switch (mode)
                {
                    case TargetMode.Self:
                        if (caster.Alive) result.Add(caster);
                        return result;

                    case TargetMode.AllAllies:
                        CollectSide(result, caster.IsPlayer);
                        return result;

                    case TargetMode.AllEnemies:
                        CollectSide(result, !caster.IsPlayer);
                        return result;

                    case TargetMode.LowestHpAlly:
                    {
                        CombatUnit best = null;
                        for (int i = 0; i < _units.Count; i++)
                        {
                            CombatUnit u = _units[i];
                            if (!u.Alive || u.IsPlayer != caster.IsPlayer) continue;
                            if (best == null || u.HpRatio < best.HpRatio) best = u;
                        }
                        if (best != null) result.Add(best);
                        return result;
                    }

                    case TargetMode.RandomAlly:
                    {
                        List<CombatUnit> allies = new List<CombatUnit>();
                        CollectSide(allies, caster.IsPlayer);
                        if (allies.Count > 0) result.Add(allies[_rng.Range(0, allies.Count)]);
                        return result;
                    }

                    case TargetMode.LastAttacker:
                    {
                        CombatUnit remembered = FindUnit(caster.LastAttackerId);
                        if (remembered != null && remembered.Alive && remembered.IsPlayer != caster.IsPlayer)
                        {
                            result.Add(remembered);
                            return result;
                        }
                        return ResolveTargets(caster, TargetMode.FrontEnemy);
                    }

                    case TargetMode.LastTarget:
                    {
                        CombatUnit remembered = FindUnit(caster.LastTargetId);
                        if (remembered != null && remembered.Alive && remembered.IsPlayer != caster.IsPlayer)
                        {
                            result.Add(remembered);
                            return result;
                        }
                        return ResolveTargets(caster, TargetMode.FrontEnemy);
                    }
                }

                // remaining modes are single-target against the opposing side
                CombatUnit taunter = FindTaunter(!caster.IsPlayer);
                if (taunter != null) { result.Add(taunter); return result; }

                CombatUnit picked = null;
                switch (mode)
                {
                    case TargetMode.RandomEnemy:
                    {
                        List<CombatUnit> enemies = new List<CombatUnit>();
                        CollectSide(enemies, !caster.IsPlayer);
                        if (enemies.Count > 0) picked = enemies[_rng.Range(0, enemies.Count)];
                        break;
                    }
                    default:
                    {
                        for (int i = 0; i < _units.Count; i++)
                        {
                            CombatUnit u = _units[i];
                            if (!u.Alive || u.IsPlayer == caster.IsPlayer) continue;
                            if (picked == null) { picked = u; continue; }
                            if (mode == TargetMode.LowestHpEnemy && u.Hp < picked.Hp) picked = u;
                            else if (mode == TargetMode.HighestHpEnemy && u.Hp > picked.Hp) picked = u;
                            else if (mode == TargetMode.HighestAttackEnemy && u.EffectiveAttack > picked.EffectiveAttack) picked = u;
                            else if (mode == TargetMode.FrontEnemy && u.Slot < picked.Slot) picked = u;
                        }
                        break;
                    }
                }
                if (picked != null) result.Add(picked);
                return result;
            }

            private void CollectSide(List<CombatUnit> into, bool playerSide)
            {
                for (int i = 0; i < _units.Count; i++)
                {
                    CombatUnit u = _units[i];
                    if (u.Alive && u.IsPlayer == playerSide) into.Add(u);
                }
            }

            private CombatUnit FindTaunter(bool playerSide)
            {
                CombatUnit found = null;
                for (int i = 0; i < _units.Count; i++)
                {
                    CombatUnit u = _units[i];
                    if (!u.Alive || u.IsPlayer != playerSide || !u.HasTaunt) continue;
                    if (found == null || u.Slot < found.Slot) found = u;
                }
                return found;
            }

            private CombatUnit FindUnit(int id)
            {
                if (id < 0) return null;
                for (int i = 0; i < _units.Count; i++) if (_units[i].Id == id) return _units[i];
                return null;
            }

            // ------------------------------------------------------------------ end state

            private bool SideAlive(bool playerSide)
            {
                for (int i = 0; i < _units.Count; i++) if (_units[i].Alive && _units[i].IsPlayer == playerSide) return true;
                return false;
            }

            private bool IsBattleOver()
            {
                return !SideAlive(true) || !SideAlive(false);
            }

            private float SideHpRatio(bool playerSide)
            {
                float hp = 0f;
                float max = 0f;
                for (int i = 0; i < _units.Count; i++)
                {
                    CombatUnit u = _units[i];
                    if (u.IsPlayer != playerSide) continue;
                    hp += u.Hp;
                    max += u.MaxHp;
                }
                return max <= 0f ? 0f : hp / max;
            }

            private void Finish()
            {
                _result.Rounds = Math.Min(_round, _cfg.MaxRounds);
                _result.PlayerHpRatio = SideHpRatio(true);
                _result.EnemyHpRatio = SideHpRatio(false);

                bool playersLeft = SideAlive(true);
                bool enemiesLeft = SideAlive(false);

                if (!enemiesLeft && playersLeft) _result.Victory = true;
                else if (!playersLeft) _result.Victory = false;
                else
                {
                    _result.TimedOut = true;
                    _result.Victory = _result.PlayerHpRatio > _result.EnemyHpRatio;
                }

                BattleEvent e = Emit(BattleEventType.BattleEnd, -1, -1);
                e.Victory = _result.Victory;
                e.Value = _result.Rounds;
            }

            private BattleEvent Emit(BattleEventType type, int sourceId, int targetId)
            {
                BattleEvent e = BattleEvent.Make(type, _round);
                e.SourceId = sourceId;
                e.TargetId = targetId;
                _result.Events.Add(e);
                return e;
            }
        }
    }
}
