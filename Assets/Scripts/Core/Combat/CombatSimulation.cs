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

            /// <summary>
            /// The round's live initiative list, and the index of the turn being taken out of it.
            /// Null outside the round loop - Execute() fires the BattleStart passives before any
            /// round exists, and a kill there has no round order to re-enter. Both Gale hooks check
            /// for that rather than assuming a round is in progress.
            /// </summary>
            private List<CombatUnit> _order;
            private int _orderIndex;

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

                    for (int i = 0; i < _units.Count; i++)
                    {
                        _units[i].ExtraTurnsThisRound = 0;
                        _units[i].StatusesExpiredThisRound = false;
                    }

                    // Gale (DESIGN-ASPECTS section 2): this was a plain `for` over a snapshot, which
                    // is exactly what had to change. The list is now live and the cursor is a field,
                    // so TryGaleReentry can splice the round's own initiative list while it is being
                    // walked and TryGaleSlowShift can re-sort the part of it that has not happened
                    // yet. Count is re-read every iteration on purpose - `order.Count` grows.
                    //
                    // Termination does not rest on the cap. Every insertion requires a kill and
                    // there are only ever _units.Count units to kill, so the list is finite even if
                    // GaleExtraTurnsPerRound is misconfigured upward. The cap is a balance bound
                    // (section 8), not a hang guard, and the harness proves it by removing it.
                    _order = TurnOrder();
                    for (_orderIndex = 0; _orderIndex < _order.Count; _orderIndex++)
                    {
                        CombatUnit unit = _order[_orderIndex];
                        if (!unit.Alive) continue;
                        TakeTurn(unit);
                        if (IsBattleOver()) break;
                    }
                    _order = null;
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
                    CombatUnit unit = CombatUnit.FromDef(hero.Def, stats, hero.Level, true, i, id++, _cfg.EnergyMax);
                    // DESIGN-RELICS "Entering the simulation": a relic is data on the request,
                    // exactly like a hero's level, resolved once here. An id that resolves to
                    // nothing leaves Relic null, which is the same path as an empty slot - content
                    // can drop a relic without bricking a save that still names it.
                    if (_request.Content != null && !string.IsNullOrEmpty(hero.EquippedRelicId))
                        unit.Relic = _request.Content.GetRelic(hero.EquippedRelicId);
                    _units.Add(unit);
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

            // ------------------------------------------------------------------ Gale

            /// <summary>
            /// Gale, half one (DESIGN-ASPECTS section 1): a Gale unit that lands a kill re-enters
            /// the current round's turn order.
            ///
            /// Keyed on the killer's own aspect, the same way Ember keys on the Burn's source, Tide
            /// on the energy's granter, Verdant on the healer and Umbra on the applier. No content
            /// authors this and no content can: there is no EffectKind that grants a turn, so the
            /// rule is a property of being Gale rather than a passive anyone could write. That is
            /// also why section 2's "the kill hook already exists and is already fired" is about the
            /// call site, not about PassiveTrigger.OnKill - the trigger above fires whatever passive
            /// the killer authored, and this fires regardless of whether it authored one.
            ///
            /// Re-entry position uses CompareTurnOrder, which is why section 2 names it and
            /// TurnOrderSpeed, RoundSpeedJitter and EffectiveSpeed as the sort inputs: the unit
            /// slots back in where its speed says it belongs among the turns that have not happened
            /// yet, so a fast Gale finisher acts again almost immediately and a slow one waits. The
            /// round's jitter is deliberately not re-rolled - it is a per-round value, and drawing
            /// again here would consume from _rng and desync every seeded replay.
            ///
            /// **The bound (DESIGN-ASPECTS section 8, DESIGN-BUILD-ORDER known trap 3).** At 5v5 an
            /// unbounded re-entry lets one Gale unit chain four kills and resolve the round on its
            /// own, which is the failure MaxPassiveDepth prevents one level down in passive
            /// recursion. GaleExtraTurnsPerRound is the equivalent guard and it is enforced here,
            /// per unit per round. It ships in the same change as the mechanic, never after.
            /// </summary>
            private void TryGaleReentry(CombatUnit killer)
            {
                if (_order == null) return;
                if (killer == null || !killer.Alive) return;
                if (killer.Aspect != Aspect.Gale) return;
                if (killer.ExtraTurnsThisRound >= _cfg.GaleExtraTurnsPerRound) return;
                if (IsBattleOver()) return;

                killer.ExtraTurnsThisRound++;

                int at = _order.Count;
                for (int i = _orderIndex + 1; i < _order.Count; i++)
                {
                    if (CompareTurnOrder(killer, _order[i]) >= 0) continue;
                    at = i;
                    break;
                }
                _order.Insert(at, killer);

                BattleEvent e = Emit(BattleEventType.ExtraTurn, killer.Id, killer.Id);
                e.Value = killer.ExtraTurnsThisRound;
            }

            /// <summary>
            /// Gale, half two (DESIGN-ASPECTS section 1): a Slow applied by a Gale source pushes the
            /// target's turn later inside the round it is applied in, not merely the next one.
            ///
            /// Without this the Slow is inert for the current round no matter how early it lands,
            /// because the order was sorted before it existed. The shift is done by re-sorting only
            /// the turns that have not been taken yet, with the same comparator that built the list:
            /// EffectiveSpeed already folds in the new Slow, so the target falls to wherever its
            /// reduced speed puts it and nothing else moves. Re-sorting an already-sorted tail with
            /// a total-ordering comparator cannot reorder anything whose sort key did not change,
            /// which is what keeps this from being a general shuffle. CompareTurnOrder breaks every
            /// tie on Id, so the result does not depend on List.Sort being stable, and nothing here
            /// draws from _rng.
            ///
            /// A target that has already acted this round is not in the tail and so is not moved -
            /// there is no turn left to push. That is a natural bound, not an omission: "later in
            /// this round" has no meaning once the turn is spent.
            ///
            /// **Unreachable in shipped content today, and deliberately not fixed here.** The only
            /// unit in the game that applies Slow is the Sundered Colossus, which is Umbra, and
            /// neither Gale unit (Ryn Solvarr, Grave Moth) applies it. Section 7's content list does
            /// not add one. The harness proves this half against a constructed Gale slower and
            /// asserts the shipped-content gap separately, so the day a Gale unit is authored with a
            /// Slow, the rule is already correct rather than newly written.
            /// </summary>
            private void TryGaleSlowShift(CombatUnit source, CombatUnit target)
            {
                if (_order == null) return;
                if (source == null || source.Aspect != Aspect.Gale) return;
                if (target == null || !target.Alive) return;

                int first = _orderIndex + 1;
                if (first >= _order.Count) return;

                int before = _order.IndexOf(target, first);
                if (before < 0) return;

                List<CombatUnit> tail = _order.GetRange(first, _order.Count - first);
                tail.Sort(CompareTurnOrder);
                for (int i = 0; i < tail.Count; i++) _order[first + i] = tail[i];

                int after = _order.IndexOf(target, first);
                if (after <= before) return;

                BattleEvent e = Emit(BattleEventType.TurnOrderShift, source.Id, target.Id);
                e.Status = StatusKind.Slow;
                e.Value = after - before;
            }

            private void TakeTurn(CombatUnit unit)
            {
                Emit(BattleEventType.TurnStart, unit.Id, unit.Id);

                TickDamageOverTime(unit);
                if (!unit.Alive || IsBattleOver()) { ExpireStatuses(unit); return; }

                TriggerPassive(unit, PassiveTrigger.TurnStart, 0);
                if (!unit.Alive || IsBattleOver()) { ExpireStatuses(unit); return; }

                // Stun: the whole turn is skipped - no Basic Attack, no Active, no energy gain.
                // Cleared by the ExpireStatuses() decrement below, the same RoundsLeft clock every
                // other status uses; there is no separate timer to keep in sync.
                //
                // Re-entry, re-stated for Gale. The previous version of this comment argued that a
                // skipped turn could not become a second turn because `order` was built once per
                // round and never mutated, so there was no call path back into TakeTurn to bound.
                // Gale makes the premise false: `_order` is now spliced mid-walk by TryGaleReentry,
                // and TakeTurn can be entered twice in one round for the same unit.
                //
                // The conclusion survives, on a different argument. Re-entry is granted only in
                // DealRawDamage's death branch, to the unit credited with the kill, and a stunned
                // unit returns from here having dealt no damage at all - it cannot have killed
                // anything on this turn, so nothing requeues it. What does need bounding is the
                // path that exists now, and that bound is GaleExtraTurnsPerRound in TryGaleReentry,
                // the turn-order equivalent of MaxPassiveDepth one level down.
                if (unit.Has(StatusKind.Stun))
                {
                    ExpireStatuses(unit);
                    return;
                }

                // Silence: the Active is blocked for this turn only; a Basic Attack resolves in its
                // place exactly as if that had been the unit's normal choice (same energy gain, same
                // AfterBasicAttack passive). Cleared the same way as Stun above - RoundsLeft reaching
                // zero in ExpireStatuses, not a manual removal here.
                bool silenced = unit.Has(StatusKind.Silence);
                bool usedActive = !silenced && unit.Active != null && unit.Energy >= unit.Active.EnergyCost;
                if (usedActive)
                {
                    unit.Energy = 0;
                    BattleEvent cast = Emit(BattleEventType.AbilityCast, unit.Id, unit.Id);
                    cast.AbilityId = unit.Active.Id;
                    cast.AbilityName = unit.Active.Name;
                    cast.Vfx = unit.Active.Vfx;
                    EmitEnergy(unit);
                    ResolveAbility(unit, unit.Active, 0);

                    // Verdict Sigil (DESIGN-RELICS). Placed here rather than inside ResolveAbility
                    // so it fires once per CAST, not once per effect, and so it mirrors the
                    // AfterBasicAttack call in the else-branch below exactly - same depth 0, same
                    // aliveness guard, same position after the ability has fully resolved.
                    TryRelicPassiveAfterActive(unit);
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

            /// <summary>
            /// Once per round per carrier, not once per turn taken.
            ///
            /// Gale's re-entry is the reason this needs saying. RoundsLeft is a round counter that
            /// happens to be decremented at the end of the carrier's turn, which was the same thing
            /// right up until a unit could take two turns in one round. Left ungated, a Gale unit
            /// that kills would shed its own Slow, Silence, Stun, DefenseDown and buffs at double
            /// rate as a side effect of the extra turn - a second, undesigned reward riding on the
            /// first, and one that would quietly break CheckStatusExpiry's "expires exactly on
            /// schedule" rule for any Gale carrier. Gale's rule is that it acts out of order, not
            /// that it ages faster.
            ///
            /// The flag is reset at the top of each round alongside ExtraTurnsThisRound. No unit
            /// could take two turns in a round before this pass, so this gate is a no-op for every
            /// battle that contains no Gale re-entry.
            /// </summary>
            private void ExpireStatuses(CombatUnit unit)
            {
                if (unit.StatusesExpiredThisRound) return;
                unit.StatusesExpiredThisRound = true;

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
                    // Relics rewrite the effect BEFORE targets resolve, which is the placement
                    // DESIGN-RELICS specifies ("rewrite a TargetMode before targets resolve") and
                    // is load bearing rather than stylistic: ResolveTargets is where the Lumen
                    // anchor and Taunt override live, so rewriting after it would let a relic
                    // target around the anchor. Rewriting before means the relic proposes a mode
                    // and the anchor still gets the final say, which is the correct precedence -
                    // an aspect identity outranks a piece of equipment.
                    // Warden's Debt's exclusion is passed INTO resolution rather than filtered
                    // out of its result, and the difference is the whole mechanic. LowestHpAlly
                    // returns exactly one unit, the single lowest; striking the caster out of that
                    // one-element answer afterwards yields an empty list, never the second-lowest.
                    // The relic would then lose the ward on exactly the turns the wearer is most
                    // hurt - which, for the Lumen anchor it was written for, is most turns. Asked
                    // during selection instead, "lowest ally other than me" is a question with a
                    // real answer. Measured, not reasoned: filtering afterwards shipped for one
                    // harness run and the ward went to nobody in both forced and shipped content.
                    bool excludeCaster;
                    EffectDef effect = ShapeByRelic(caster, ability.Effects[i], out excludeCaster);
                    List<CombatUnit> targets = ResolveTargets(caster, effect.Target, excludeCaster ? caster : null);
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

                        // Control Immunity: a second, narrower resist than Effect Resist - it only
                        // ever reduces Silence/Stun landing chance, never any other harmful status
                        // (Burn, Slow, AttackDown, DefenseDown stay governed by Effect Resist alone).
                        // Chosen over a duration reduction because chance is where Effect Resist
                        // already lives, so a defender's full anti-control kit (Effect Resist plus
                        // Control Immunity) resolves as one landing roll instead of a roll plus a
                        // separate post-hoc duration edit. Applied as a second subtraction with the
                        // same additive-then-clamp shape ResolveLandingChance uses, rather than as a
                        // 5th parameter on that method: ResolveLandingChance is called directly, with
                        // its existing 4-argument signature, from CheckEffectHitResistNoOp and
                        // CheckEffectHitResistDirection in the harness, and every one of those calls
                        // would need updating for a parameter that only Silence/Stun ever use.
                        if ((effect.Status == StatusKind.Silence || effect.Status == StatusKind.Stun)
                            && target.EffectiveControlImmunity != 0f)
                        {
                            landing -= target.EffectiveControlImmunity;
                            if (landing < MinEffectLanding) landing = MinEffectLanding;
                            if (landing > MaxEffectLanding) landing = MaxEffectLanding;
                        }

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

                        // Gale (DESIGN-ASPECTS section 1): a Slow from a Gale source takes effect in
                        // the round it lands in, not only in the next one's sort. Emitted after
                        // StatusApplied so the record reads in causal order: the Slow landed, then
                        // the order moved because of it.
                        if (effect.Status == StatusKind.Slow) TryGaleSlowShift(source, target);
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
                    case EffectKind.Dispel:
                    {
                        if (!target.Alive) break;
                        int removed = 0;
                        for (int i = target.Statuses.Count - 1; i >= 0 && removed < effect.Hits; i--)
                        {
                            if (StatusInfo.IsHarmful(target.Statuses[i].Kind)) continue;
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

            /// <summary>
            /// The order, in the sequence below: (0) Block roll, (1) base, (2) aspect factor,
            /// (3) crit, (4) Skill Damage, (5) Defense mitigation ratio, (6) Armor Break,
            /// (6.5) Holy Damage, (7) Damage Reduction, (8) variance + round + clamp.
            /// Shield-then-HP happens afterward, inside DealRawDamage, same as before - it was
            /// never part of this method's own steps.
            ///
            /// Steps 5 and 6 are computed as two separate terms - `mitigation` from Defense, then
            /// Armor Break scaling that ratio - but applied to `raw` in one multiply. That is
            /// deliberate, not a merge of the two ideas: computing `1f - mitigation` and then a
            /// second `raw *= 1f - armorBreakAdjustment` would not be bit-identical to the single
            /// multiply below when Armor Break is zero, and DESIGN-COMBAT-STATS' Known trap on this
            /// exact pipeline is "relying on float identity across a formula you will later edit is
            /// a trap - bypass explicitly" instead. Bypassed explicitly here: when armorBreak is 0,
            /// `mitigation` is untouched and `raw *= 1f - mitigation` is the exact expression that
            /// shipped before this change, so every fingerprint with ArmorBreak unauthored (all of
            /// them today) stays byte-identical.
            ///
            /// Holy Damage (step 6.5) is a flat bonus that must be unaffected by Defense - the two
            /// cannot both be true of one insertion point, since anything folded into `raw` before
            /// the step-5/6 multiply gets scaled down by mitigation same as everything else. So its
            /// value is read here, held aside in `holyBonus`, and added back to `raw` immediately
            /// after that multiply (see below) rather than before it: "before Defense" describes
            /// when its value is decided (a flat number off the source's own stat, independent of
            /// the target entirely), "unaffected by Defense" describes where it lands in the actual
            /// arithmetic. It still passes through Damage Reduction and variance afterward - neither
            /// of those was asked to exempt it, only Defense/Armor Break was.
            /// </summary>
            private void ApplyDamageEffect(CombatUnit source, CombatUnit target, EffectDef effect, AbilityDef ability, int depth)
            {
                // 0. Block - a distinct RNG draw from the crit roll below, resolved on the
                // defender before anything else runs. Success negates the whole hit: no damage, no
                // crit (never rolled), no OnCriticalHit, nothing rides along - "zero effects from
                // that hit", not a partial reduction. Block defaults to 0 on every unit today, and
                // DeterministicRng.Chance(0) returns false without drawing (the same short-circuit
                // Effect Hit/Resist rely on for their own no-op guarantee), so this new draw changes
                // nothing about any existing seeded battle's RNG stream.
                if (_rng.Chance(target.EffectiveBlock))
                {
                    TriggerPassive(target, PassiveTrigger.OnBlock, depth);
                    return;
                }

                // 1. Base
                float raw = source.EffectiveAttack * effect.Power + effect.Flat;
                float holyBonus = source.EffectiveHolyDamage;

                // 2. Aspect factor
                float aspectFactor = AspectChart.Multiplier(source.Aspect, target.Aspect);
                raw *= aspectFactor;

                // 3. Crit
                bool critical = _rng.Chance(source.EffectiveCritChance);
                if (critical) raw *= source.CritDamage;

                // 4. Skill Damage - Active and passive-sourced Damage effects only. Gated on
                // ability.Kind rather than on the stat being non-zero, so a Basic Attack is provably
                // untouched by this term regardless of what SkillDamage is authored - not merely
                // unaffected when nobody has authored one yet.
                if (ability.Kind != AbilityKind.BasicAttack && source.EffectiveSkillDamage != 0f)
                    raw *= 1f + source.EffectiveSkillDamage;

                // 5. Defense mitigation ratio (EffectiveDefense already folds in DefenseUp/DefenseDown)
                float defense = target.EffectiveDefense;
                float mitigation = defense / (defense + _cfg.DefenseConstant + _cfg.DefensePerLevel * source.Level);

                // 6. Armor Break - its own multiplicative reduction of the mitigation RATIO, not of
                // `defense`/EffectiveDefense. Mathematically distinguishable from DefenseDown:
                // DefenseDown changes the INPUT to the ratio (via SumMagnitude on EffectiveDefense),
                // Armor Break scales the ratio's OUTPUT. Same Defense value, different ArmorBreak ->
                // different final damage, and the reverse - two independent terms, not one value
                // written from two places.
                float armorBreak = source.EffectiveArmorBreak;
                if (armorBreak > 0f)
                {
                    if (armorBreak > 1f) armorBreak = 1f;
                    mitigation *= 1f - armorBreak;
                }

                raw *= 1f - mitigation;

                // 6.5. Holy Damage - added back in only now, after the Defense/Armor Break
                // multiply above, so it is not part of what that multiply scaled down. A flat
                // add is an exact no-op when holyBonus is 0 (IEEE 754 x + 0 == x always), so this
                // needs no zero-guard the way the multiplicative steps above do to stay bit-identical.
                raw += holyBonus;

                // 7. Damage Reduction - flat percentage off the damage that already survived
                // Defense + Armor Break above (Holy Damage included - only Defense was asked to
                // skip it). Its own step, strictly after step 5/6, never folded into `mitigation` itself.
                float damageReduction = target.EffectiveDamageReduction;
                if (damageReduction > 0f)
                {
                    if (damageReduction > 1f) damageReduction = 1f;
                    raw *= 1f - damageReduction;
                }

                // 8. Variance, round, clamp
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
                    TryGaleReentry(killer);
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

            // ------------------------------------------------------------------ relics

            /// <summary>
            /// Runs one authored effect through the caster's relic, returning either the original
            /// effect unchanged or a rewritten COPY. Never mutates the argument: the EffectDef
            /// handed in belongs to the AbilityDef in the ContentDatabase and is shared by every
            /// unit in every battle for the process lifetime, so an in-place edit would leak one
            /// wearer's relic onto everybody's future casts. EffectDef.Clone exists for this.
            ///
            /// Draws nothing from _rng. Relics still change a battle's RNG stream, because they
            /// change how many draws happen downstream (Splitting Prism most obviously - one
            /// landing roll instead of one per enemy), which is the caveat DESIGN-RELICS states
            /// plainly: "the same seed with a different relic is a different battle".
            /// </summary>
            private EffectDef ShapeByRelic(CombatUnit caster, EffectDef effect, out bool excludeCaster)
            {
                excludeCaster = false;
                RelicDef relic = caster.Relic;
                if (relic == null) return effect;

                switch (relic.Hook)
                {
                    case RelicHook.Retarget:
                    {
                        if (effect.Target != relic.FromTarget) return effect;
                        if (relic.HasFilterKind && effect.Kind != relic.FilterKind) return effect;
                        AnnounceRelic(caster, relic);
                        excludeCaster = relic.ExcludeSelf;
                        EffectDef retargeted = effect.Clone();
                        retargeted.Target = relic.ToTarget;
                        return retargeted;
                    }

                    case RelicHook.SplitWideDamage:
                    {
                        if (effect.Target != TargetMode.AllEnemies) return effect;

                        // How many the wide version WOULD have struck, counted the same way
                        // ResolveTargets counts it (living units on the far side) so the multiplied
                        // Hits is exactly the output the spread cast would have produced,
                        // concentrated rather than inflated. Draws no RNG.
                        int wide = CountLiving(!caster.IsPlayer);
                        if (wide <= 0) return effect;

                        AnnounceRelic(caster, relic);
                        EffectDef split = effect.Clone();
                        split.Target = TargetMode.FrontEnemy;

                        // Damage concentrates; everything else merely narrows. That asymmetry IS
                        // the relic's cost, per DESIGN-HOOK-RESOLUTION section 4: Kalyx's Cinder
                        // Bloom aims a Damage effect and a Burn rider at AllEnemies, and after this
                        // the Damage lands `wide` times on one target while the Burn gets exactly
                        // one landing roll for the whole cast instead of one per enemy. Multiplying
                        // Hits on the rider too would hand it `wide` rolls at the same target and
                        // delete the cost, leaving the pure-upside relic the design forbids.
                        if (split.Kind == EffectKind.Damage) split.Hits = effect.Hits * wide;
                        return split;
                    }
                }

                return effect;
            }

            private int CountLiving(bool playerSide)
            {
                int n = 0;
                for (int i = 0; i < _units.Count; i++)
                    if (_units[i].Alive && _units[i].IsPlayer == playerSide) n++;
                return n;
            }

            /// <summary>
            /// Verdict Sigil. Fires the wearer's AfterBasicAttack passive after an Active resolves,
            /// in addition to wherever it already fires - it widens the trigger, it does not move
            /// it, so a turn spent on a Basic still marks exactly as before.
            ///
            /// Routed through the ordinary TriggerPassive rather than a parallel path, so every
            /// guard that method already enforces still applies: the once-per-battle flag, the
            /// HP-threshold gate, the empty-effects check, and MaxPassiveDepth. TriggerPassive's
            /// own `passive.Trigger != trigger` test is what makes this safe to call blindly -
            /// a wearer whose passive is not an AfterBasicAttack one is a no-op here.
            /// </summary>
            private void TryRelicPassiveAfterActive(CombatUnit unit)
            {
                RelicDef relic = unit.Relic;
                if (relic == null || relic.Hook != RelicHook.WidenPassiveTrigger) return;
                if (relic.FromTrigger == PassiveTrigger.None) return;
                if (!unit.Alive || IsBattleOver()) return;

                // Checked before announcing so the banner means "the relic did something", not
                // "the wearer cast an Active while holding a relic". A wearer with no passive, or
                // with a passive on a different trigger, never announces.
                PassiveDef passive = unit.Passive;
                if (passive == null || passive.Trigger != relic.FromTrigger) return;

                AnnounceRelic(unit, relic);
                TriggerPassive(unit, relic.FromTrigger, 0);
            }

            /// <summary>
            /// DESIGN-RELICS "Presentation": relics announce through the EXISTING PassiveTrigger
            /// event with the relic name in AbilityName, so the battle screen's existing
            /// "NAME - PASSIVE" banner covers them and no new BattleEventType is added. Once per
            /// battle, the first time the relic actually alters something, not on every tick.
            ///
            /// Emitting draws no RNG, so a relic that announces cannot desync a seeded replay
            /// relative to the same relic not announcing.
            /// </summary>
            private void AnnounceRelic(CombatUnit wearer, RelicDef relic)
            {
                if (wearer.RelicAnnounced) return;
                wearer.RelicAnnounced = true;
                BattleEvent e = Emit(BattleEventType.PassiveTrigger, wearer.Id, wearer.Id);
                e.AbilityId = relic.Id;
                e.AbilityName = relic.Name;
                e.Vfx = "buff";
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
                return ResolveTargets(caster, mode, null);
            }

            /// <summary>
            /// `excluded` is a unit that cannot be picked no matter what the mode asks for, or null
            /// for every caller that is not a relic. Only Warden's Debt passes one today.
            ///
            /// Null is the untouched path by construction, not by care: every guard added for this
            /// is `u == excluded`, which is false for every real unit when excluded is null, so the
            /// four-mode switch below and the anchor and pick loops that follow it all behave
            /// exactly as they did before relics existed. No RNG draw changes either - the one
            /// draw site in here (RandomAlly / RandomEnemy) only sees a shortened candidate list
            /// when a relic is actually excluding somebody from it.
            /// </summary>
            private List<CombatUnit> ResolveTargets(CombatUnit caster, TargetMode mode, CombatUnit excluded)
            {
                List<CombatUnit> result = new List<CombatUnit>();
                switch (mode)
                {
                    case TargetMode.Self:
                        if (caster.Alive && caster != excluded) result.Add(caster);
                        return result;

                    case TargetMode.AllAllies:
                        CollectSide(result, caster.IsPlayer, excluded);
                        return result;

                    case TargetMode.AllEnemies:
                        CollectSide(result, !caster.IsPlayer, excluded);
                        return result;

                    case TargetMode.LowestHpAlly:
                    {
                        CombatUnit best = null;
                        for (int i = 0; i < _units.Count; i++)
                        {
                            CombatUnit u = _units[i];
                            if (!u.Alive || u.IsPlayer != caster.IsPlayer || u == excluded) continue;
                            if (best == null || u.HpRatio < best.HpRatio) best = u;
                        }
                        if (best != null) result.Add(best);
                        return result;
                    }

                    case TargetMode.RandomAlly:
                    {
                        List<CombatUnit> allies = new List<CombatUnit>();
                        CollectSide(allies, caster.IsPlayer, excluded);
                        if (allies.Count > 0) result.Add(allies[_rng.Range(0, allies.Count)]);
                        return result;
                    }

                }

                // Every remaining mode targets a single enemy, and the anchor overrides all of them
                // including LastAttacker/LastTarget, so the unit that answers for a side cannot be
                // targeted around. ("Provoke" in the previous version of this comment was the old
                // name for StatusKind.Taunt and had outlived it.)
                //
                // Note for anyone reading the RNG stream: returning here short-circuits the switch
                // below, so RandomEnemy does not draw when a side has an anchor. That was already
                // true for Taunt; Lumen simply makes it true far more often.
                CombatUnit anchor = FindTargetAnchor(!caster.IsPlayer);
                if (anchor == excluded) anchor = null;
                if (anchor != null)
                {
                    // Recorded only where the Lumen rule is what produced the answer and Taunt does
                    // not already explain it on the target's own status bar. The redirect is
                    // otherwise unreadable rather than merely unread: the Damage event shows the hit
                    // landing on the Lumen unit, and nothing in the record distinguishes "it
                    // answered for its side" from "it was genuinely the lowest-HP or front-most
                    // pick". Emitting costs no RNG draw, so it cannot desync a seeded battle.
                    if (anchor.Aspect == Aspect.Lumen && !anchor.HasTaunt)
                    {
                        BattleEvent anchored = Emit(BattleEventType.TargetAnchored, caster.Id, anchor.Id);
                        anchored.Value = (int)mode;
                    }
                    result.Add(anchor);
                    return result;
                }

                switch (mode)
                {
                    case TargetMode.LastAttacker:
                    {
                        CombatUnit remembered = FindUnit(caster.LastAttackerId);
                        if (remembered != null && remembered.Alive && remembered != excluded && remembered.IsPlayer != caster.IsPlayer)
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
                        if (remembered != null && remembered.Alive && remembered != excluded && remembered.IsPlayer != caster.IsPlayer)
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
                        CollectSide(enemies, !caster.IsPlayer, excluded);
                        if (enemies.Count > 0) picked = enemies[_rng.Range(0, enemies.Count)];
                        break;
                    }
                    default:
                    {
                        for (int i = 0; i < _units.Count; i++)
                        {
                            CombatUnit u = _units[i];
                            if (!u.Alive || u.IsPlayer == caster.IsPlayer || u == excluded) continue;
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
                CollectSide(into, playerSide, null);
            }

            private void CollectSide(List<CombatUnit> into, bool playerSide, CombatUnit excluded)
            {
                for (int i = 0; i < _units.Count; i++)
                {
                    CombatUnit u = _units[i];
                    if (u.Alive && u.IsPlayer == playerSide && u != excluded) into.Add(u);
                }
            }

            /// <summary>
            /// The unit that answers every single-enemy targeting question aimed at `playerSide`, or
            /// null when the question resolves by its own TargetMode. Was `FindTaunter`; renamed
            /// because Taunt is no longer the only way to qualify.
            ///
            /// **Lumen (DESIGN-ASPECTS section 1, re-weighted by DESIGN-WORLD section 1).** A living
            /// Lumen unit answers unconditionally, carrying Taunt or not. That unconditional is the
            /// whole rule. DESIGN-ASPECTS wrote Lumen as "Lumen intercepts", absorption first;
            /// DESIGN-WORLD overrides that ordering and says the identity is that targeting becomes
            /// deterministic - "a description of a function that returns the same answer no matter
            /// what was asked of it", and "absorption is not the identity, it is what happens when
            /// the answer is always you". A rule that only held while a duration-2 Taunt was up
            /// would be a function whose answer changes every other round, which is the opposite of
            /// the belief it is supposed to be. So this method is the identity, and DealRawDamage's
            /// shield-absorb block - which section 2 also names - gets no new code at all: it
            /// already absorbs unconditionally, and absorption is the consequence of being the
            /// answer, not a second co-equal rule.
            ///
            /// **Lumen outranks Taunt, and the order matters.** If a non-Lumen ally's Taunt could
            /// displace a Lumen unit, the answer would change whenever anyone else taunted - again
            /// the non-determinism the aspect exists to remove. Both tiers break ties on Slot, and
            /// nothing here draws from _rng, so the answer is stable within a fight and across
            /// replays of it.
            ///
            /// **No aliveness question beyond the loop's own.** A dead Lumen unit stops answering
            /// and targeting immediately reverts to normal, which is the cost the rule carries: the
            /// line has exactly one knowable place until it doesn't.
            /// </summary>
            private CombatUnit FindTargetAnchor(bool playerSide)
            {
                CombatUnit lumen = null;
                CombatUnit taunter = null;
                for (int i = 0; i < _units.Count; i++)
                {
                    CombatUnit u = _units[i];
                    if (!u.Alive || u.IsPlayer != playerSide) continue;
                    if (u.Aspect == Aspect.Lumen && (lumen == null || u.Slot < lumen.Slot)) lumen = u;
                    if (u.HasTaunt && (taunter == null || u.Slot < taunter.Slot)) taunter = u;
                }
                return lumen != null ? lumen : taunter;
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
