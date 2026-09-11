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

        /// <summary>
        /// The profile the team came from, or null. Read for ONE thing: resolving each hero's armor
        /// uids into the pieces they name, which only the profile's OwnedArmor collection can do.
        ///
        /// Optional on purpose. Every synthetic request the harness builds leaves it null, and a
        /// null profile means no armor, which is an exact no-op through StatMath.ApplyArmor - so
        /// every existing seeded fingerprint is untouched by armor's existence. Relics did not need
        /// this because a relic id resolves against content; an armor uid resolves against the save.
        /// </summary>
        public PlayerProfile Profile;

        /// <summary>
        /// The enemy side as a plain snapshot instead of an authored line (DESIGN-PVP section 3).
        /// Null for every caller that existed before The Proving - campaign stages, Recital floors,
        /// every synthetic harness request - and when it is null nothing about this request behaves
        /// differently, so no existing seeded fingerprint moves.
        ///
        /// Takes precedence over Stage.Enemies when both are set. Stage stays readable either way
        /// because the fight still needs its environment, its rewards and its display code; only
        /// who stands on the far side changes.
        /// </summary>
        public BattleLoadout Opponent;
    }

    /// <summary>
    /// The whole fight, resolved up front into a list of events.
    /// No Unity types, no coroutines, no frame timing - which is exactly why it can be
    /// unit tested, replayed and fast-forwarded.
    /// </summary>
    public static class CombatSimulation
    {
        /// <summary>
        /// Fired once after every completed fight. Subscribe in editor-only code to receive the
        /// full event list for debugging. Never assigned in production — invocation is a no-op when null.
        /// </summary>
        public static Action<BattleResult> OnBattleComplete;

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
            BattleResult result = sim.Execute();
            if (OnBattleComplete != null) OnBattleComplete(result);
            return result;
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

        /// <summary>
        /// Whether an effect's authored condition holds right now, for this caster and this target.
        ///
        /// Public and static for the same reason ResolveLandingChance is: it is a pure function of
        /// its arguments, and the harness tests the arithmetic directly rather than only inferring
        /// it from a battle.
        ///
        /// PURE, in the two senses that matter to a seeded simulation. It draws nothing from the
        /// RNG, so a conditional effect cannot shift the stream relative to an unconditional one
        /// that resolves the same way; and it mutates nothing, so evaluating it twice is the same
        /// as evaluating it once. Both are load bearing: the condition is asked once per target per
        /// effect, and a battle that replayed differently because a test had been asked would not
        /// be a replay.
        ///
        /// An effect with no condition returns true before anything else is read, which is the
        /// whole of why this pass cannot move an existing battle - every effect in shipped content
        /// takes that first line and nothing below it runs.
        ///
        /// A null subject is false rather than true: a condition is a statement that something is
        /// the case, and about a unit that is not there nothing is the case.
        /// </summary>
        public static bool ConditionMet(EffectDef effect, CombatUnit caster, CombatUnit target)
        {
            if (effect == null || effect.Condition == EffectCondition.None) return true;

            CombatUnit subject = effect.ConditionOn == ConditionSubject.Caster ? caster : target;
            if (subject == null) return false;

            switch (effect.Condition)
            {
                case EffectCondition.HasStatus:
                    return effect.ConditionStatus != StatusKind.None && subject.Has(effect.ConditionStatus);

                case EffectCondition.LacksStatus:
                    return effect.ConditionStatus != StatusKind.None && !subject.Has(effect.ConditionStatus);

                // Inclusive at the boundary, matching PassiveTrigger.OnHpBelowThreshold - see
                // EffectCondition.HpBelow for why the newer conditional adopts the older one's edge.
                case EffectCondition.HpBelow:
                    return subject.HpRatio <= effect.ConditionValue;

                case EffectCondition.HpAbove:
                    return subject.HpRatio >= effect.ConditionValue;

                // The absorbing pool, not the status row. They can disagree, and the pool is what
                // stops damage - see EffectCondition.HasShield.
                case EffectCondition.HasShield:
                    return subject.Shield > 0f;

                case EffectCondition.LacksShield:
                    return subject.Shield <= 0f;

                case EffectCondition.StatusStacksAtLeast:
                {
                    if (effect.ConditionStatus == StatusKind.None) return false;
                    int needed = (int)Math.Round(effect.ConditionValue, MidpointRounding.AwayFromZero);
                    if (needed < 1) needed = 1;
                    return subject.CountOf(effect.ConditionStatus) >= needed;
                }
            }

            // An unknown member is refused rather than waved through. A condition nobody has taught
            // this method about must not silently become "always fires" - that turns a content typo
            // into a hero doing more than it says, which is the harder direction to notice.
            return false;
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
            /// True while a Mirrored Wound reflect is being delivered, bounding it to one hop.
            ///
            /// Not latent, unlike _inRedirect: two Mirrored Wound wearers on opposite sides is an
            /// ordinary thing for a player to build toward, and without this the first hit between
            /// them recurses until the stack gives out. The reflect cannot use MaxPassiveDepth for
            /// this because it is not a passive and never passes through TriggerPassive.
            /// </summary>
            private bool _inReflect;

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
                    // DESIGN-RELICS "Entering the simulation": a relic is data on the request,
                    // exactly like a hero's level, resolved once here. An id that resolves to
                    // nothing leaves relic null, which is the same path as an empty slot - content
                    // can drop a relic without bricking a save that still names it.
                    RelicDef relic = null;
                    if (_request.Content != null && !string.IsNullOrEmpty(hero.EquippedRelicId))
                        relic = _request.Content.GetRelic(hero.EquippedRelicId);

                    // A relic's stat block, whichever grade carries one, is folded into the stat
                    // line here and then forgotten - the simulation below never learns it existed,
                    // and every damage number it touches is already inside the numbers ApplyRelic
                    // returned. A MYTHIC relic is ALSO carried onto the unit, because its hook is
                    // read at its own resolution point further in - the two entry points are not
                    // mutually exclusive on a mythic relic the way they were before it could carry
                    // both a hook and a stat block.
                    // Armor, DESIGN-ARMOR section 9: resolved here and then forgotten, exactly the
                    // way a legendary relic's stat block is. Nothing below this line learns armor
                    // exists - there is no armor field on CombatUnit and no armor branch anywhere
                    // in the effect pipeline, because every bonus a piece can carry is already
                    // inside the numbers ApplyArmor returned.
                    //
                    // The equipped list comes from the profile on the request rather than being
                    // assembled here, so the battle and the hero-detail preview cannot build it two
                    // different ways. A null profile - every synthetic harness request - leaves it
                    // null, which ApplyArmor treats as an exact no-op.
                    List<ArmorPiece> armor = _request.Profile != null ? _request.Profile.EquippedArmorOf(hero) : null;
                    ArmorConfig armorConfig = _request.Content != null ? _request.Content.Armor : null;

                    UnitStats stats = StatMath.ForHero(hero.Def, hero.Level, relic, armor, armorConfig);
                    CombatUnit unit = CombatUnit.FromDef(hero.Def, stats, hero.Level, true, i, id++, _cfg.EnergyMax);
                    if (relic != null && relic.Grade == RelicGrade.Mythic) unit.Relic = relic;

                    // The selected talent, resolved here and then forgotten, exactly the way a
                    // relic id is resolved one block up: the selection is save state, the talent it
                    // names is content, and only this line knows that. Below it the talent is one
                    // more entry in CombatUnit.Passives and nothing in the simulation asks whether
                    // a passive came from the def or from a selection.
                    //
                    // Appended AFTER the def's own passives, so a talent resolves last on a shared
                    // trigger. A selection of 0 - which is every hero in every save that predates
                    // this, and the default for every hero summoned after it - resolves to null and
                    // adds nothing, which is what makes the talent path unreachable until a player
                    // picks one.
                    PassiveDef talent = hero.Def.TalentAt(hero.SelectedTalent);
                    if (talent != null) unit.AddPassive(talent);

                    _units.Add(unit);
                }

                // The far side, from ONE of two sources. Both end at the same CombatUnit.FromDef
                // call with isPlayer false, which is what makes this a branch on where the data
                // came from rather than two different kinds of fight: FromDef takes the abstract
                // UnitDef base, so a HeroDef and an EnemyDef are equally at home there, and
                // StatMath.Build is shared underneath ForHero, ForEnemy and ForOpponent alike.
                // That pre-existing symmetry is why supporting a snapshot opponent is this small.
                if (_request.Opponent != null && _request.Content != null)
                {
                    List<LoadoutUnit> line = _request.Opponent.Units;
                    for (int i = 0; i < line.Count && i < _cfg.MaxTeamSize; i++)
                    {
                        LoadoutUnit entry = line[i];
                        if (entry == null) continue;
                        HeroDef def = _request.Content.GetHero(entry.DefId);
                        // A snapshot naming a hero this build does not have is skipped, not fatal.
                        // A loadout can outlive the content it was taken against - that is the
                        // whole premise of it being a snapshot - and a missing hero must cost the
                        // player one body on the far side rather than the whole fight.
                        if (def == null) continue;
                        RelicDef relic = string.IsNullOrEmpty(entry.RelicId)
                            ? null : _request.Content.GetRelic(entry.RelicId);
                        UnitStats stats = StatMath.ForOpponent(def, entry.Level, entry.Stars, relic,
                                                               entry.Armor, _request.Content.Armor);
                        CombatUnit unit = CombatUnit.FromDef(def, stats, entry.Level, false, i, id++, _cfg.EnergyMax);
                        // Same rule the player side applies one loop up: only a Mythic relic's hook
                        // reaches the simulation, a Legendary's whole contribution is already in
                        // the stats above. Kept identical so an opponent's relic behaves exactly as
                        // the player's would.
                        if (relic != null && relic.Grade == RelicGrade.Mythic) unit.Relic = relic;
                        _units.Add(unit);
                    }
                }
                else if (_request.Stage != null && _request.Content != null)
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
                    // No relic reshapes an effect any more. Both legendary relics in the current
                    // set hook further in - The Unbroken Ledger at the landing roll, Mirrored Wound
                    // at the damage funnel - so an authored effect reaches ResolveTargets exactly
                    // as written. The three hooks that did rewrite effects here (Retarget,
                    // SplitWideDamage, WidenPassiveTrigger) went with the four relics that were
                    // their only users; git has them if a future legendary relic wants one back.
                    EffectDef effect = ability.Effects[i];

                    // Resolve scaling, snapshotted once per effect rather than once per target, so
                    // every enemy in an AoE is struck by the same stack count no matter what dies
                    // partway through. Cloned, never mutated in place: the EffectDef here belongs to
                    // the ContentDatabase and is shared by every unit in every battle for the life
                    // of the process, which is the same trap EffectDef.Clone's own comment records
                    // for relics.
                    int resolveStacks = caster.CountOf(StatusKind.Resolve);
                    if (effect.PerStack != 0f && resolveStacks > 0)
                    {
                        EffectDef scaled = effect.Clone();
                        scaled.Power = effect.Power * (1f + effect.PerStack * resolveStacks);
                        effect = scaled;
                    }

                    // Targeting runs FIRST and the condition is tested against what it picked,
                    // which is the only order that can answer "is the unit this is landing on
                    // below 35% HP". It also means a conditional RandomEnemy effect draws for its
                    // target exactly as an unconditional one does, whether or not the condition
                    // then holds: the draw belongs to targeting, not to the test, and moving the
                    // test in front of it would make a condition consume or skip an RNG draw.
                    List<CombatUnit> targets = ResolveTargets(caster, effect.Target, null);

                    // Per target, not per effect. A conditional AoE asks about each enemy on its
                    // own, so "deal damage to all enemies below 40% HP" is one effect rather than a
                    // targeting mode nobody has to invent.
                    bool anyTargetPassed = false;
                    for (int t = 0; t < targets.Count; t++)
                    {
                        CombatUnit target = targets[t];
                        if (target == null) continue;
                        if (!ConditionMet(effect, caster, target)) continue;
                        anyTargetPassed = true;
                        ApplyEffect(caster, target, effect, ability, depth);
                        if (IsBattleOver()) return;
                    }

                    // An effect whose condition held for nobody did not happen, so it does not pay
                    // its Resolve either - spending the counter emits a StatusExpired, and a
                    // skipped effect that still moved the status row would be observable, which is
                    // exactly what "skipped entirely" is supposed to rule out.
                    //
                    // Gated on the effect carrying a condition at all, not on anyTargetPassed
                    // alone, and that is deliberate rather than defensive: an UNCONDITIONAL effect
                    // that resolves against an empty target list spends its Resolve today, and
                    // making that case stop spending would be a behaviour change to shipped
                    // content wearing the costume of a new feature.
                    bool suppressed = effect.Condition != EffectCondition.None && !anyTargetPassed;

                    // Spent after the effect has finished with every target, so an AoE pays the
                    // stack once. Emitted as StatusExpired for the same reason Cleanse does: the
                    // status row has to stop showing a counter the unit no longer holds.
                    if (!suppressed && effect.ConsumesResolve && caster.CountOf(StatusKind.Resolve) > 0)
                    {
                        caster.Remove(StatusKind.Resolve);
                        BattleEvent spent = Emit(BattleEventType.StatusExpired, caster.Id, caster.Id);
                        spent.Status = StatusKind.Resolve;
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

                        // The Unbroken Ledger, applied BEFORE Control Immunity below and never
                        // after it. The relic erases Effect Resist only, and the ordering is what
                        // makes that true: recomputing the landing chance after the Control
                        // Immunity subtraction would silently discard that subtraction too, and
                        // hard control would stop being answerable by any defensive stat.
                        landing = ApplyLedger(source, target, effect, landing);

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

                        // Ember (DESIGN-ASPECTS section 6): how deep the stack is now. The status
                        // row is the one place in this project where the information is genuinely
                        // absent from the display rather than merely unread - three Burn entries and
                        // one Burn entry both render as a single BRN - and the doc's own method does
                        // not close that on its own. It is still not a new event: StatusApplied
                        // already fires per application, it just never said how many there were.
                        //
                        // Counted here rather than on the screen because the screen cannot recount
                        // it. AddOrRefresh folds an ordinary re-application into the existing entry
                        // and fires this event anyway, so a screen tallying events would show a
                        // five-deep Guard Down that the simulation never created.
                        e.Value = target.CountOf(effect.Status);

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
                    case EffectKind.GainResolve:
                    {
                        // Resolve is a personal counter and nothing else. Nothing in the damage,
                        // heal, defence or targeting path reads StatusKind.Resolve - it is inert
                        // until an effect asks for it with PerStack, which is what makes it safe to
                        // hand to any hero without touching the simulation again. It stacks through
                        // the same AddStack/CountOf pair Ember's Burn already uses rather than a
                        // second counting mechanism living beside that one.
                        if (!target.Alive) break;
                        int cap = effect.MaxStacks > 0 ? effect.MaxStacks : 1;
                        for (int g = 0; g < effect.Hits; g++)
                        {
                            if (!target.AddStack(StatusKind.Resolve, Math.Max(1, effect.Duration),
                                                 effect.Magnitude, source.Id, cap))
                                break;   // already at cap; do not emit a second event for a no-op
                            BattleEvent re = Emit(BattleEventType.StatusApplied, source.Id, target.Id);
                            re.Status = StatusKind.Resolve;
                            re.Duration = Math.Max(1, effect.Duration);
                            // Value carries stack depth here for the same reason Ember's Burn
                            // stacking uses it: StatusApplied already fires per application, it
                            // just never said how many there were.
                            re.Value = target.CountOf(StatusKind.Resolve);
                        }
                        break;
                    }
                    case EffectKind.SwapPositions:
                    {
                        // target is one unit drawn by ResolveTargets (authored as RandomEnemy).
                        // Pick one more living unit from the same side (not target) and swap slots.
                        // Swapping slots reshuffles who FrontEnemy resolves to; turn order is
                        // speed-sorted, not slot-sorted, so initiative is unaffected.
                        if (!target.Alive) break;
                        List<CombatUnit> candidates = new List<CombatUnit>();
                        for (int i = 0; i < _units.Count; i++)
                        {
                            CombatUnit u = _units[i];
                            if (u != target && u.Alive && u.IsPlayer == target.IsPlayer) candidates.Add(u);
                        }
                        if (candidates.Count == 0) break;
                        CombatUnit other = candidates[_rng.Range(0, candidates.Count)];
                        int tmp = target.Slot;
                        target.Slot = other.Slot;
                        other.Slot = tmp;
                        BattleEvent swapEvent = Emit(BattleEventType.PositionSwapped, source.Id, target.Id);
                        swapEvent.Value = other.Id;
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

                    // Umbra's spread rule reads the dying unit's own list, so the marks have to be
                    // taken before the clear below empties it - the clear is precisely what they
                    // are escaping. Null when this unit carried none, which is the common case.
                    List<StatusInstance> marks = CollectUmbraMarks(target);

                    target.Statuses.Clear();
                    target.Shield = 0f;
                    Emit(BattleEventType.Death, sourceId, target.Id);

                    // Before OnKill and Gale re-entry, so the record reads in causal order: the unit
                    // died, its mark moved, and only then does anything act on the new state. A Gale
                    // killer that re-enters immediately finds the jumped debuff already standing.
                    TryUmbraSpread(target, marks);

                    CombatUnit killer = FindUnit(sourceId);
                    if (killer != null && killer.Alive) TriggerPassive(killer, PassiveTrigger.OnKill, 0);
                    TryGaleReentry(killer);
                    return;
                }

                // -1: energy from being hit is the victim's own regen, not something the attacker
                // granted. A Tide attacker must not redirect its victim's line's energy.
                AddEnergy(target, _cfg.EnergyOnDamaged, -1);
                TriggerPassive(target, PassiveTrigger.OnDamaged, 0);

                // Mirrored Wound shares this dispatch point with the OnDamaged passive above and
                // deliberately runs AFTER it. That trigger is not free real estate - Backwash
                // (tideturned_passive) already listens on it in enemies.json - and a hero passive
                // listening on a trigger is a different thing from a relic reflecting damage, so
                // the two coexist rather than compete. Placed second so a passive that heals or
                // shields its owner resolves before the reflect can kill the attacker out from
                // under it, which keeps the passive's behaviour identical with and without a relic.
                ApplyMirroredWound(sourceId, target, damage);

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
                // Carried here too, so every StatusApplied in the list answers the same question.
                // Always 1 - the bank uses AddOrRefresh and never stacks - but a screen that has to
                // ask which emit site produced an event is a screen that recomputes.
                banked.Value = target.CountOf(StatusKind.Regen);
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
            /// The Unbroken Ledger. Rebuilds a harmful effect's landing chance with the target's
            /// Effect Resist forced to zero, leaving every other term of it alone.
            ///
            /// Only the wearer's OWN Effect Hit and the effect's authored chance survive into the
            /// recomputation, which is the relic's card text read literally: harmful effects from
            /// the wearer bypass the target's Effect Resist entirely. Control Immunity is
            /// deliberately untouched - it is applied by
            /// the caller AFTER this returns, so a Silence or a Stun from a Ledger wearer still
            /// meets it. That is the decision DESIGN-RELICS states rather than leaves ambiguous:
            /// erasing both layers would make hard control unanswerable by any defensive stat in
            /// the game, which is a much larger change than the one the relic asks for.
            ///
            /// Beneficial statuses are excluded by ResolveLandingChance itself (only harmful ones
            /// are ever contested), so a Ledger wearer's Shield or Taunt resolves on its authored
            /// chance exactly as it did before - the relic cannot accidentally buff its own buffs.
            ///
            /// Determinism: forcing Effect Resist to zero can push landing to exactly 1.0, and
            /// DeterministicRng.Chance returns true at p >= 1 WITHOUT drawing. This relic removes
            /// draws from the stream reliably, not incidentally. That is the DESIGN-RELICS caveat
            /// working as stated ("the same seed with a different relic is a different battle"),
            /// not a defect.
            /// </summary>
            private float ApplyLedger(CombatUnit source, CombatUnit target, EffectDef effect, float landing)
            {
                RelicDef relic = source.Relic;
                if (relic == null || relic.Hook != RelicHook.BypassEffectResist) return landing;
                if (!StatusInfo.IsHarmful(effect.Status)) return landing;

                // Nothing to shrug off means nothing to announce. Checked against the target's
                // resist rather than against the recomputed number so the banner means "this
                // target's resist was erased", not "the wearer cast at somebody".
                if (target.EffectiveEffectResist == 0f) return landing;

                AnnounceRelic(source, relic);
                return ResolveLandingChance(effect.Chance, effect.Status, source.EffectiveEffectHit, 0f);
            }

            /// <summary>
            /// Mirrored Wound. Returns a fraction of the damage the wearer just took to whoever
            /// dealt it, rounded away from zero so a small hit still reflects at least a point.
            ///
            /// Routed through DealRawDamage like every other point of damage in the game, so the
            /// reflected hit is shielded, banked, killed off and reported by exactly the same code
            /// as any other - a reflect that kills its attacker fires their death, their killer's
            /// OnKill and Gale re-entry, because it went through the funnel rather than around it.
            ///
            /// Bounded by _inReflect rather than by MaxPassiveDepth, and it has to be: the reflect
            /// is not a passive and never enters TriggerPassive, so the depth argument that bounds
            /// passive recursion never sees it. Two Mirrored Wound wearers hitting each other would
            /// otherwise reflect into each other without limit. Same shape as the _inRedirect guard
            /// Tide uses one method over, for the same reason.
            ///
            /// Self-damage cannot start a loop either: sourceId is the attacker's, and an attacker
            /// who is also the target is skipped outright.
            /// </summary>
            private void ApplyMirroredWound(int sourceId, CombatUnit target, int damage)
            {
                if (_inReflect || damage <= 0 || sourceId < 0) return;
                RelicDef relic = target.Relic;
                if (relic == null || relic.Hook != RelicHook.ReflectDamage) return;

                CombatUnit attacker = FindUnit(sourceId);
                if (attacker == null || !attacker.Alive || attacker == target) return;

                float fraction = _cfg.MirroredWoundReflectFraction;
                if (fraction <= 0f) return;
                int reflected = (int)Math.Round(damage * fraction, MidpointRounding.AwayFromZero);
                if (reflected <= 0) return;

                AnnounceRelic(target, relic);
                _inReflect = true;
                DealRawDamage(target.Id, attacker, reflected, false, "impact", StatusKind.None, 1f, relic.Name);
                _inReflect = false;
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

            /// <summary>
            /// Fires every passive on this unit that listens on this trigger, in the order the
            /// unit carries them - authored passives first, the selected talent last.
            ///
            /// A loop where there was a single read, and each of the four gates below moved inside
            /// it rather than staying above it, which is the whole of the multi-passive change.
            /// Each passive answers the trigger, the threshold, the empty-effects test and its own
            /// oncePerBattle allowance for itself: two passives on one unit are two abilities that
            /// happen to share an owner, and one of them being spent says nothing about the other.
            /// MaxPassiveDepth is unchanged and still bounds recursion, and it bounds it per
            /// passive: each fires at `depth`, each resolves at `depth + 1`, so a unit with three
            /// passives recurses no deeper than a unit with one - it merely does so three times.
            /// Nothing here can recurse more times than the effects content authored, and content
            /// cannot author a passive that retriggers its own trigger without hitting the same
            /// cap it hit before.
            ///
            /// **Identical for one passive, provably.** Every unit in shipped content authors
            /// exactly one, so this walks a one-element list, and the only statement that is not
            /// the old body verbatim is the continuation guard at the top of the loop, which is
            /// gated on `i > 0` and therefore cannot execute at all. That guard is deliberately
            /// NOT hoisted above the loop: TriggerPassive is called for a killer whose kill ended
            /// the fight (DealRawDamage's death branch) and for a unit that is already dead in no
            /// path at all, so an IsBattleOver test before the first passive would suppress an
            /// OnKill passive that fires today and move every fingerprint containing one.
            /// </summary>
            private void TriggerPassive(CombatUnit owner, PassiveTrigger trigger, int depth)
            {
                if (owner == null || !owner.Alive) return;

                for (int i = 0; i < owner.Passives.Count; i++)
                {
                    // Between passives only. A passive that killed its owner, or ended the fight,
                    // stops the ones behind it rather than resolving into a decided battle - the
                    // same rule ResolveAbility already applies between an effect's own targets.
                    if (i > 0 && (!owner.Alive || IsBattleOver())) return;

                    PassiveDef passive = owner.Passives[i];
                    if (passive == null || passive.Trigger != trigger) continue;
                    if (passive.OncePerBattle && owner.PassiveHasFired(i)) continue;
                    if (trigger == PassiveTrigger.OnHpBelowThreshold && owner.HpRatio > passive.Threshold) continue;
                    if (passive.Effects.Count == 0) continue;
                    if (depth >= MaxPassiveDepth) continue;

                    if (passive.OncePerBattle) owner.MarkPassiveFired(i);

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
            /// Umbra, the team-size-5 half (DESIGN-ASPECTS section 8): the mark jumps on the marked
            /// unit's death, so Umbra's single-target subtraction keeps covering a line that got
            /// wider instead of being diluted by it.
            ///
            /// Section 8 leaves this as one of two open options - build a spread rule, or accept
            /// Umbra as a boss-stage specialist - and the spread rule is the one that is built. The
            /// dilution it answers is real and runs one way only: at team size 3 a permanent debuff
            /// on the enemy that matters removes a third of the opposing offence, at 5 it removes a
            /// fifth, and Umbra is the only one of the six identities that gets quietly worse as the
            /// game grows. Every other identity in section 8 holds steady or improves at 5.
            ///
            /// Harmful and Umbra-sourced, using exactly the predicate the Cleanse branch uses. The
            /// jump and the uncleansable rule are two halves of one belief - what Umbra marks stays
            /// marked - and they must not disagree about what counts as a mark.
            ///
            /// No aliveness test on the applier, for the same reason IsUmbraSourced has none: the
            /// persistence half of the original rule was cut (DESIGN-WORLD section 1), so a mark is
            /// bounded by its own duration whether its author still stands or not.
            ///
            /// **Creates no value.** RoundsLeft and Magnitude cross unchanged and the entry is added
            /// with AddOrRefresh, so a jump moves a subtraction that already existed onto a unit
            /// that is still standing - it never deepens one. A chain across a five-enemy line is
            /// the intended behaviour and is what answers the dilution, and it needs no cap the way
            /// Gale's re-entry does: every hop costs a death, there are only ever as many deaths as
            /// units, and unlike an extra turn a hop hands out nothing that was not already on the
            /// board. AddOrRefresh rather than AddStack is also deliberate - Ember's stacking keys
            /// on an Ember source, and an Umbra-sourced Burn is not an Ember Burn.
            /// </summary>
            private List<StatusInstance> CollectUmbraMarks(CombatUnit dying)
            {
                List<StatusInstance> marks = null;
                for (int i = 0; i < dying.Statuses.Count; i++)
                {
                    StatusInstance s = dying.Statuses[i];
                    if (!StatusInfo.IsHarmful(s.Kind)) continue;
                    if (!IsUmbraSourced(s)) continue;
                    if (marks == null) marks = new List<StatusInstance>();
                    marks.Add(s);
                }
                return marks;
            }

            private void TryUmbraSpread(CombatUnit dying, List<StatusInstance> marks)
            {
                if (marks == null || marks.Count == 0) return;

                CombatUnit heir = FindMarkHeir(dying);
                if (heir == null) return;

                for (int i = 0; i < marks.Count; i++)
                {
                    StatusInstance s = marks[i];
                    heir.AddOrRefresh(s.Kind, s.RoundsLeft, s.Magnitude, s.SourceId);

                    BattleEvent e = Emit(BattleEventType.StatusSpread, dying.Id, heir.Id);
                    e.Status = s.Kind;
                    e.Duration = s.RoundsLeft;
                    e.Value = s.SourceId;
                }
            }

            /// <summary>
            /// Which unit a mark jumps to: the living enemy that matters most, by attack. That is
            /// Umbra's own targeting verb rather than a fresh rule invented for the jump - Nyx aims
            /// HighestAttackEnemy with every ability she has, and section 1's identity is "pick the
            /// enemy that matters and subtract it", so the mark lands where she would have aimed it
            /// next.
            ///
            /// The heir is drawn from the dying unit's own side, which is the side the mark was
            /// always pointed at: a harmful status reached its carrier by being aimed across the
            /// line, so "another living enemy" is another unit standing where that one stood. It is
            /// deliberately not keyed on the applier's side - the applier may be dead, and reading
            /// its side would make the rule silently depend on a unit no longer in the fight.
            ///
            /// Deterministic without touching _rng: EffectiveAttack, then the lower Slot, then the
            /// lower Id. Slot and Id cannot both tie between two distinct units, so the ordering is
            /// total and the answer does not depend on _units' iteration order. Drawing here would
            /// consume from the stream and desync every seeded replay whose fight contains an Umbra
            /// death - the same reason Gale's re-entry does not re-roll the round's jitter.
            /// </summary>
            private CombatUnit FindMarkHeir(CombatUnit dying)
            {
                CombatUnit best = null;
                for (int i = 0; i < _units.Count; i++)
                {
                    CombatUnit u = _units[i];
                    if (!u.Alive || u == dying) continue;
                    if (u.IsPlayer != dying.IsPlayer) continue;
                    if (best == null) { best = u; continue; }

                    float mine = u.EffectiveAttack;
                    float theirs = best.EffectiveAttack;
                    if (mine > theirs) { best = u; continue; }
                    if (mine < theirs) continue;
                    if (u.Slot < best.Slot) { best = u; continue; }
                    if (u.Slot > best.Slot) continue;
                    if (u.Id < best.Id) best = u;
                }
                return best;
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
