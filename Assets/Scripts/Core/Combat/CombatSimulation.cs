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

        /// <summary>
        /// Rounds a Verdant overflow bank is spread across. Two, not one: a status decrements at the
        /// end of its carrier's own turn, so a bank applied during the carrier's own turn - a Verdant
        /// unit overhealing itself - would expire at the end of that same turn having never reached a
        /// TickDamageOverTime. Two rounds guarantees at least one tick from either side of the turn.
        /// </summary>
        private const int VerdantBankRounds = 2;

        private const float MinEffectLanding = 0.05f;
        private const float MaxEffectLanding = 1f;

        public static BattleResult Run(BattleRequest request)
        {
            Sim sim = new Sim(request);
            return sim.Execute();
        }

        /// <summary>
        /// The landing chance for an ApplyStatus effect, before the RNG roll. Bypasses the Effect
        /// Hit / Effect Resist arithmetic entirely when both are exactly zero, which every unit in
        /// shipped content is today - this keeps every battle bit-identical to the pre-stat
        /// pipeline, since DeterministicRng.Chance short-circuits without drawing when its argument
        /// is untouched. Only harmful statuses (StatusInfo.IsHarmful) are ever contested; beneficial
        /// ones - Taunt, Shield, buffs - always resolve on the authored chance as-is.
        /// </summary>
        public static float ResolveLandingChance(float baseChance, StatusKind status, float sourceEffectHit, float targetEffectResist)
        {
            if (!StatusInfo.IsHarmful(status)) return baseChance;
            if (sourceEffectHit == 0f && targetEffectResist == 0f) return baseChance;

            float landing = baseChance + sourceEffectHit - targetEffectResist;
            if (landing < MinEffectLanding) landing = MinEffectLanding;
            // The ceiling is exactly 1f, not the 0.95f used elsewhere (EffectiveCritChance), and
            // that is load bearing. Nyx's Hollowing is the one ApplyStatus in shipped content with
            // no authored "chance" field, so it defaults to 1.0 and DeterministicRng.Chance never
            // draws for it. A ceiling below 1f would clamp that 1.0 down, turn the zero-draw
            // short-circuit into a real draw, and desync every battle she appears in from that
            // point on. Do not lower this without re-deriving every seeded fingerprint that includes her.
            if (landing > MaxEffectLanding) landing = MaxEffectLanding;
            return landing;
        }

        private sealed class Sim
        {
            private readonly BattleRequest _request;
            private readonly BalanceConfig _cfg;
            private readonly DeterministicRng _rng;
            private readonly List<CombatUnit> _units = new List<CombatUnit>();
            private readonly BattleResult _result = new BattleResult();
            private int _round;

            /// <summary>
            /// True while a Tide hand-off is being delivered. Bounds the redirect to one hop even if
            /// the hand-off is ever routed back through AddEnergy.
            ///
            /// Measured, not assumed: with the hand-off deliberately rewritten to recurse, removing
            /// this guard changes nothing in shipped content - the recipient is chosen as the
            /// largest gap on the line, and no grant today (8 and 25, from Rising Chorus and Tidal
            /// Cadence) is big enough to overflow it. So the cascade is currently unreachable and
            /// this guard is latent. It stops being latent the moment grants get bigger, which
            /// DESIGN-ASPECTS section 8 says is coming ("her numbers get retuned regardless of this
            /// document"): at 4x grants the same rewrite cascades to 6 hand-offs inside one grant,
            /// and with this guard in place it stays at 4.
            /// </summary>
            private bool _inRedirect;

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
                        // -1: a unit's own basic-attack refill has no granter, so it never redirects.
                        AddEnergy(unit, _cfg.EnergyPerBasicAttack, -1);
                        TriggerPassive(unit, PassiveTrigger.AfterBasicAttack, 0);
                    }
                }

                ExpireStatuses(unit);
            }

            /// <summary>
            /// Walks a snapshot, not the live list, because two things mutate unit.Statuses from
            /// inside this loop: DealRawDamage removes the Shield entry when a tick breaks the ward,
            /// and clears the list entirely on death. Indexing the live list meant a shield break
            /// shifted every later entry down one and the walk stepped straight over it, so a Burn or
            /// Regen sitting behind a Shield could silently never tick.
            ///
            /// Latent, not observed: reverting this method to the indexed walk produces byte-identical
            /// harness output across all twelve stages, so the ordering needed to trigger it does not
            /// occur in shipped content today. It is guarded rather than left alone because Ember's
            /// stacking raises the number of DoT entries a single unit can sit behind one ward, which
            /// moves the case from unreachable toward merely rare. Any behavioural change here would
            /// therefore be a regression, not a fix - the identical output is the point.
            ///
            /// The Contains guard stops an entry that was removed mid-walk from ticking after the
            /// fact. StatusInstance is a reference type with no Equals override, so that is identity
            /// comparison, which is what is wanted here.
            /// </summary>
            private void TickDamageOverTime(CombatUnit unit)
            {
                List<StatusInstance> ticking = new List<StatusInstance>(unit.Statuses);
                for (int i = 0; i < ticking.Count; i++)
                {
                    StatusInstance s = ticking[i];
                    if (!unit.Statuses.Contains(s)) continue;

                    if (s.Kind == StatusKind.Burn)
                    {
                        int damage = (int)Math.Round(s.Magnitude, MidpointRounding.AwayFromZero);
                        if (damage < 1) damage = 1;
                        // named for what it actually is - not whatever the source last cast, so the
                        // Debrief and any on-screen attribution can't misname a burn tick as a fresh hit
                        DealRawDamage(s.SourceId, unit, damage, false, "burn", StatusKind.Burn, 1f, StatusInfo.DisplayName(StatusKind.Burn));
                        if (!unit.Alive) return;
                    }
                    else if (s.Kind == StatusKind.Regen)
                    {
                        int heal = (int)Math.Round(s.Magnitude, MidpointRounding.AwayFromZero);
                        if (heal < 1) heal = 1;
                        ApplyHealValue(s.SourceId, unit, heal, "heal", false);
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
                        ApplyHealValue(source.Id, target, (int)Math.Round(amount, MidpointRounding.AwayFromZero), "heal", true);
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
                        float landing = ResolveLandingChance(effect.Chance, effect.Status,
                                                              source.EffectiveEffectHit, target.EffectiveEffectResist);
                        if (!_rng.Chance(landing))
                        {
                            // Only harmful statuses are contested, so only harmful statuses can be
                            // resisted. A beneficial status losing its own authored roll is not a
                            // resist and must not be reported as one. Emitting here costs no RNG
                            // draw, so it cannot shift the stream or desync a seeded battle.
                            if (StatusInfo.IsHarmful(effect.Status))
                            {
                                BattleEvent miss = Emit(BattleEventType.StatusResisted, source.Id, target.Id);
                                miss.Status = effect.Status;
                            }
                            break;
                        }
                        float magnitude = effect.Magnitude;
                        if (effect.Status == StatusKind.Burn || effect.Status == StatusKind.Regen)
                            magnitude = source.EffectiveAttack * effect.Magnitude;
                        int duration = Math.Max(1, effect.Duration);

                        // Ember (DESIGN-ASPECTS section 1): a Burn from an Ember source stacks as
                        // its own entry instead of folding into the one already there, so a second
                        // Ember deepens the same fire rather than replacing it. The magnitude was
                        // snapshotted off this source's own EffectiveAttack just above, so a strong
                        // Ember's stack and a weak Ember's are already correctly different sizes.
                        if (effect.Status == StatusKind.Burn && source.Aspect == Aspect.Ember)
                            target.AddStack(effect.Status, duration, magnitude, source.Id, _cfg.EmberBurnStackCap);
                        else
                            target.AddOrRefresh(effect.Status, duration, magnitude, source.Id);

                        BattleEvent e = Emit(BattleEventType.StatusApplied, source.Id, target.Id);
                        e.Status = effect.Status;
                        e.Duration = duration;
                        break;
                    }
                    case EffectKind.Cleanse:
                    {
                        if (!target.Alive) break;
                        int removed = 0;
                        for (int i = target.Statuses.Count - 1; i >= 0 && removed < effect.Hits; i--)
                        {
                            if (!StatusInfo.IsHarmful(target.Statuses[i].Kind)) continue;
                            // Umbra (DESIGN-ASPECTS section 1, as corrected by DESIGN-WORLD section
                            // 1): a harmful status from an Umbra source cannot be cleansed. Only the
                            // uncleansable half of the original rule is built. The other half - "does
                            // not tick down while the applier lives" - is persistence, which is
                            // Verdant's belief, not Umbra's, so ExpireStatuses is deliberately left
                            // alone and these entries still expire on schedule.
                            //
                            // Skipping rather than counting: an uncleansable status does not consume
                            // one of the cleanse's Hits, so a Cleanse still clears what it legitimately
                            // can instead of being absorbed by the entry it was never allowed to take.
                            if (IsUmbraSourced(target.Statuses[i])) continue;
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
                        // The one granting site: source handed this out, so Tide's redirect applies.
                        AddEnergy(target, amount, source.Id);
                        break;
                    }
                }
            }

            private void ApplyDamageEffect(CombatUnit source, CombatUnit target, EffectDef effect, AbilityDef ability, int depth)
            {
                float raw = source.EffectiveAttack * effect.Power + effect.Flat;

                float aspectFactor = AspectChart.Multiplier(source.Aspect, target.Aspect);
                raw *= aspectFactor;

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
                string abilityName = ability != null ? ability.Name : "";
                DealRawDamage(source.Id, target, damage, critical, vfx, StatusKind.None, aspectFactor, abilityName);

                if (critical && source.Alive) TriggerPassive(source, PassiveTrigger.OnCriticalHit, depth);
            }

            /// <summary>Single funnel for every point of damage in the game.</summary>
            private void DealRawDamage(int sourceId, CombatUnit target, int damage, bool critical, string vfx, StatusKind dotKind,
                                        float aspectFactor, string abilityName)
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
                e.AspectFactor = aspectFactor;
                e.AbilityName = abilityName;

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

                // -1: energy from being hit is the victim's own regen, not something the attacker
                // granted. A Tide attacker must not redirect its victim's line's energy.
                AddEnergy(target, _cfg.EnergyOnDamaged, -1);
                TriggerPassive(target, PassiveTrigger.OnDamaged, 0);
                TriggerPassive(target, PassiveTrigger.OnHpBelowThreshold, 0);
            }

            /// <summary>
            /// allowBank is Verdant's gate and is never defaulted, because the one call site that
            /// must pass false is the Regen tick itself. Banking a Regen tick's own overflow would
            /// re-apply Regen to a full-health carrier every turn, refreshing the status off its own
            /// output and leaving it on the unit permanently - a self-feeding loop. Verdant's rule is
            /// about healing *output* finding somewhere to go, not about a bank recycling itself.
            /// </summary>
            private void ApplyHealValue(int sourceId, CombatUnit target, int amount, string vfx, bool allowBank)
            {
                if (!target.Alive || amount <= 0) return;

                float missing = target.MaxHp - target.Hp;
                int healed = missing > 0f ? (int)Math.Min(missing, amount) : 0;
                if (healed > 0)
                {
                    target.Hp += healed;
                    BattleEvent e = Emit(BattleEventType.Heal, sourceId, target.Id);
                    e.Value = healed;
                    e.Vfx = vfx;
                }

                // Verdant (DESIGN-ASPECTS section 1): healing with nowhere to go is banked onto the
                // recipient as Regen instead of being clamped away. Split across VerdantBankRounds
                // so the bank pays out at most what overflowed and never more - Magnitude on a Regen
                // entry is a flat per-tick heal, so handing it the whole overflow with a duration
                // above 1 would pay the overflow out once per round and create value.
                int overflow = amount - healed;
                if (!allowBank || overflow <= 0) return;

                CombatUnit source = FindUnit(sourceId);
                if (source == null || source.Aspect != Aspect.Verdant) return;

                float perTick = overflow / (float)VerdantBankRounds;
                if (perTick < 1f) return;

                target.AddOrRefresh(StatusKind.Regen, VerdantBankRounds, perTick, sourceId);
                BattleEvent banked = Emit(BattleEventType.StatusApplied, sourceId, target.Id);
                banked.Status = StatusKind.Regen;
                banked.Duration = VerdantBankRounds;
            }

            /// <summary>
            /// Tide (DESIGN-ASPECTS section 1): energy granted by a Tide source is never discarded.
            /// Overflow past EnergyMax is handed to the living ally furthest from casting.
            ///
            /// sourceId is the unit that *granted* this energy, or -1 where there is no granter.
            /// That distinction is the one DESIGN-HOOK-CONFLICT section 1 flags as missing from the
            /// rule's wording: two of the three call sites are a unit's own regen (its own basic
            /// attack, and being hit), which nobody hands out, and "Tide never discards energy" read
            /// literally would redirect those too. Keying on the granter matches how Ember and
            /// Verdant already key on source.Aspect, and matches the rule's own justification in
            /// DESIGN-ASPECTS section 3 - "a line sitting near 100 energy throws away most of what
            /// she produces" is about Maeve's output, not about other units' self-regen.
            ///
            /// One hop, no chaining, per section 2's explicit hazard. Two things enforce it, and the
            /// second exists because the first is easy to undo by accident. Structurally, the
            /// hand-off goes through AddEnergyClamped, which has no redirect branch, so there is no
            /// code path from the hand-off back into this method. _inRedirect then holds the bound
            /// even if someone later routes the hand-off through AddEnergy instead - the obvious,
            /// tempting refactor, and silently a cascade. The cost of the bound is that a recipient
            /// which also caps out discards its own remainder; that is what "one hop" means.
            ///
            /// Chain depth is deliberately guarded here rather than checked in the event list,
            /// because it is not observable there: a recipient filled to the cap emits nothing for
            /// its own next grant, so a genuine chain and two consecutive one-hop redirects produce
            /// byte-identical events. The harness bounds hand-offs per grant instead.
            /// </summary>
            private void AddEnergy(CombatUnit unit, int amount, int sourceId)
            {
                int overflow = AddEnergyClamped(unit, amount);
                if (overflow <= 0) return;
                if (_inRedirect) return;

                CombatUnit source = FindUnit(sourceId);
                if (source == null || source.Aspect != Aspect.Tide) return;

                CombatUnit recipient = FurthestFromCasting(unit);
                if (recipient == null) return;

                _inRedirect = true;
                AddEnergyClamped(recipient, overflow, unit.Id);
                _inRedirect = false;
            }

            /// <summary>
            /// The original clamping AddEnergy, returning what it would otherwise have thrown away.
            /// EmitEnergy fires here, so both sides of a Tide hand-off are recorded in the event
            /// list. Deliberately has no redirect branch: this is the method that makes chaining
            /// impossible rather than merely bounded.
            /// </summary>
            private int AddEnergyClamped(CombatUnit unit, int amount, int handedFromId)
            {
                if (!unit.Alive || amount == 0) return 0;
                int before = unit.Energy;
                unit.Energy += amount;
                int overflow = 0;
                if (unit.Energy > unit.EnergyMax)
                {
                    overflow = unit.Energy - unit.EnergyMax;
                    unit.Energy = unit.EnergyMax;
                }
                if (unit.Energy < 0) unit.Energy = 0;
                if (unit.Energy != before) EmitEnergy(unit, handedFromId);
                return overflow;
            }

            private int AddEnergyClamped(CombatUnit unit, int amount)
            {
                return AddEnergyClamped(unit, amount, -1);
            }

            /// <summary>
            /// Tide's recipient: the living ally with the most energy still to find before its own
            /// active can fire. Excludes the unit that just overflowed, which is at cap by
            /// definition, and excludes units with no active, since energy handed to a unit that can
            /// never spend it is the same discard Tide exists to prevent. Side is taken from the
            /// unit that overflowed rather than from the granter, so overflow stays on the line it
            /// landed on even if some future ability grants energy across the field.
            ///
            /// Ties break on Id, and nothing here draws from _rng: this runs inside a seeded
            /// simulation, and consuming a draw would shift the stream and desync replays.
            /// </summary>
            private CombatUnit FurthestFromCasting(CombatUnit overflowed)
            {
                CombatUnit best = null;
                int bestGap = 0;
                for (int i = 0; i < _units.Count; i++)
                {
                    CombatUnit u = _units[i];
                    if (!u.Alive || u == overflowed) continue;
                    if (u.IsPlayer != overflowed.IsPlayer) continue;
                    if (u.Active == null) continue;

                    int gap = u.Active.EnergyCost - u.Energy;
                    if (gap <= 0) continue;
                    if (best == null || gap > bestGap || (gap == bestGap && u.Id < best.Id))
                    {
                        best = u;
                        bestGap = gap;
                    }
                }
                return best;
            }

            private void EmitEnergy(CombatUnit unit)
            {
                EmitEnergy(unit, -1);
            }

            /// <summary>
            /// handedFromId is the unit a Tide redirect took this energy from, or -1 for ordinary
            /// energy change. It is written into the event's SourceId, so on an EnergyChanged event
            /// SourceId != TargetId means "this energy was handed over" and SourceId == TargetId
            /// means "this unit's own energy changed" - which is what every pre-Tide path emits, so
            /// their events are unchanged byte for byte.
            ///
            /// Without this the hand-off would be invisible: EmitEnergy fires for both units, but a
            /// consumer reading the list could not tell the recipient's gain apart from any other
            /// energy gain, and DESIGN-BATTLE-READABILITY's rule is that the screen reads the record
            /// rather than recomputing the fight. The causality has to be in the record to be read.
            /// </summary>
            private void EmitEnergy(CombatUnit unit, int handedFromId)
            {
                int from = handedFromId >= 0 ? handedFromId : unit.Id;
                BattleEvent e = Emit(BattleEventType.EnergyChanged, from, unit.Id);
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

                }

                // every remaining mode targets a single enemy - Provoke overrides all of them,
                // including LastAttacker/LastTarget, so a taunting tank can't be targeted around.
                CombatUnit taunter = FindTaunter(!caster.IsPlayer);
                if (taunter != null) { result.Add(taunter); return result; }

                switch (mode)
                {
                    case TargetMode.LastAttacker:
                    {
                        CombatUnit remembered = FindUnit(caster.LastAttackerId);
                        if (remembered != null && remembered.Alive && remembered.IsPlayer != caster.IsPlayer)
                        {
                            result.Add(remembered);
                            return result;
                        }
                        mode = TargetMode.FrontEnemy;
                        break;
                    }

                    case TargetMode.LastTarget:
                    {
                        CombatUnit remembered = FindUnit(caster.LastTargetId);
                        if (remembered != null && remembered.Alive && remembered.IsPlayer != caster.IsPlayer)
                        {
                            result.Add(remembered);
                            return result;
                        }
                        mode = TargetMode.FrontEnemy;
                        break;
                    }
                }

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

            /// <summary>
            /// Whether a status entry was applied by an Umbra source. No aliveness test on the
            /// applier: DESIGN-ASPECTS section 2 attached "while the applier lives" specifically to
            /// the non-expiry rule, and DESIGN-WORLD section 1 cuts that rule. What is left is
            /// bounded by the status's own duration either way, so a dead Umbra's debuff runs out on
            /// schedule rather than lasting forever.
            /// </summary>
            private bool IsUmbraSourced(StatusInstance status)
            {
                CombatUnit applier = FindUnit(status.SourceId);
                return applier != null && applier.Aspect == Aspect.Umbra;
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
