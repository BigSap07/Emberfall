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

                // The mark conditions read the CASTER's identity, never the subject's, which is the
                // whole point of a mark being one kind told apart by who applied it.
                case EffectCondition.HasMyMark:
                    return caster != null && subject.HasMarkFrom(MarkKindOf(effect.ConditionStatus), caster.Id);

                case EffectCondition.LacksMyMark:
                    return caster != null && !subject.HasMarkFrom(MarkKindOf(effect.ConditionStatus), caster.Id);

                case EffectCondition.LedgerAtLeast:
                    return caster != null && subject.LedgerTotalFrom(caster.Id) >= effect.ConditionValue;

                case EffectCondition.SubjectActedThisRound:
                    return subject.ActedThisRound;

                case EffectCondition.SubjectHasNotActedThisRound:
                    return !subject.ActedThisRound;

                // Round-granular, and the comparison says so: the subject was damaged in a round at
                // or after the one the caster last acted in. Two units acting inside the same round
                // are not ordered against each other by this.
                case EffectCondition.SubjectDamagedSinceMyTurn:
                    return caster != null && subject.LastDamagedRound > 0
                        && subject.LastDamagedRound >= caster.LastTurnRound;

                case EffectCondition.SubjectIsMyPreviousTarget:
                    return caster != null && caster.PreviousTargetId >= 0
                        && caster.PreviousTargetId == subject.Id;

                // False when there IS no previous target, not true. A hero that has attacked
                // nobody has not switched targets; reading "no history" as "different" would make
                // every opening attack claim a change that did not happen.
                case EffectCondition.SubjectIsNotMyPreviousTarget:
                    return caster != null && caster.PreviousTargetId >= 0
                        && caster.PreviousTargetId != subject.Id;

                // Needs two turns on record before it can say anything, which is why ActionsTaken
                // is counted at all: without it a hero's first turn compares against a default and
                // reports a repeat it never made.
                case EffectCondition.CasterRepeatedActionType:
                    return caster != null && caster.ActionsTaken >= 2
                        && caster.LastActionWasActive == caster.PreviousActionWasActive;

                case EffectCondition.AlreadyDamagedSubjectThisRound:
                    return caster != null && caster.TimesDamagedThisRound(subject.Id) > 0;

                // ---------- the non-numeric ledger, read back ----------
                //
                // Each of these finds the CASTER'S OWN entry of the named kind on the subject and
                // asks what it recorded. Source-keyed like every other mark question here, so two
                // heroes running the same mark on the same body read their own record and not each
                // other's.
                case EffectCondition.StoredStatusMatches:
                {
                    // "Gains a positive effect MATCHING the recorded effect": the comparison is
                    // between what the record remembers and what the subject is carrying NOW, not
                    // between the record and a constant. A constant would be a condition whose
                    // answer the author already knew, and would make the record decorative.
                    StatusInstance rec = FindRecord(subject, effect.ConditionStatus, caster);
                    return rec != null && rec.StoredStatus != StatusKind.None
                           && subject.Has(rec.StoredStatus);
                }

                case EffectCondition.StoredActionMatches:
                {
                    StatusInstance rec = FindRecord(subject, effect.ConditionStatus, caster);
                    if (rec == null || rec.StoredAction == 0) return false;
                    // ConditionValue is the action being asked about: 1 a Basic, 2 an Active.
                    return rec.StoredAction == (int)effect.ConditionValue;
                }

                case EffectCondition.StoredRoleMatches:
                {
                    StatusInstance rec = FindRecord(subject, effect.ConditionStatus, caster);
                    return rec != null && rec.StoredRoleSet && rec.StoredRole == (HeroRole)(int)effect.ConditionValue;
                }

                // A running minimum is only meaningful once something has been recorded, and the
                // unset sentinel is negative precisely so "never recorded" and "recorded 0%" are
                // different answers rather than the same one.
                case EffectCondition.SubjectAboveStoredMin:
                {
                    StatusInstance rec = FindRecord(subject, effect.ConditionStatus, caster);
                    return rec != null && rec.StoredMin >= 0f && subject.HpRatio > rec.StoredMin;
                }

                case EffectCondition.StatusUseAvailable:
                {
                    StatusInstance rec = FindRecord(subject, effect.ConditionStatus, caster);
                    if (rec == null) return false;
                    return rec.UsesPerRound <= 0 || rec.UsesThisRound < rec.UsesPerRound;
                }

                // ---------- cross-unit stat comparison ----------
                //
                // ConditionValue is unused; the test is whether the caster's stat is strictly
                // above/below the target's. ComparedStat on the effect names which stat.
                case EffectCondition.CasterStatAboveTarget:
                    return caster != null && StatValue(caster, effect.ComparedStat) > StatValue(target, effect.ComparedStat);

                case EffectCondition.CasterStatBelowTarget:
                    return caster != null && StatValue(caster, effect.ComparedStat) < StatValue(target, effect.ComparedStat);

                // ---------- action-type history (Zella) ----------
                //
                // These read LastActionWasActive, which is set at the START of the unit's own
                // turn. ActionsTaken >= 1 guards the "never acted" case so a hero's opening
                // turn does not claim it knows what kind its last turn was.
                case EffectCondition.SubjectLastActionWasBasic:
                    return subject.ActionsTaken >= 1 && !subject.LastActionWasActive;

                case EffectCondition.SubjectLastActionWasActive:
                    return subject.ActionsTaken >= 1 && subject.LastActionWasActive;

                case EffectCondition.SubjectChangedActionType:
                    return subject.ActionsTaken >= 2
                        && subject.LastActionWasActive != subject.PreviousActionWasActive;

                case EffectCondition.SubjectRepeatedActionType:
                    return subject.ActionsTaken >= 2
                        && subject.LastActionWasActive == subject.PreviousActionWasActive;

                case EffectCondition.HpAboveSnapshot:
                    return subject.SnapshotHp > 0 && subject.Hp > subject.SnapshotHp;

                // ---------- batch three ----------

                case EffectCondition.HasAnyBeneficialStatus:
                {
                    for (int i = 0; i < subject.Statuses.Count; i++)
                    {
                        StatusKind k = subject.Statuses[i].Kind;
                        // Exclude inert / counter kinds that are neither buff nor debuff.
                        if (k == StatusKind.None || k == StatusKind.Resolve || k == StatusKind.Resolve2
                            || k == StatusKind.Ledger || k == StatusKind.Mark || k == StatusKind.MarkAlt)
                            continue;
                        if (!StatusInfo.IsHarmful(k)) return true;
                    }
                    return false;
                }

                case EffectCondition.HasAnyHarmfulStatus:
                {
                    for (int i = 0; i < subject.Statuses.Count; i++)
                        if (StatusInfo.IsHarmful(subject.Statuses[i].Kind)) return true;
                    return false;
                }

                // ShieldBrokenByCaster is contextual — answered by the Sim's _shieldBreakerId.
                // In the pure function (harness, outside a battle) it is always false.
                case EffectCondition.ShieldBrokenByCaster:
                    return false;

                case EffectCondition.LedgerAtCap:
                {
                    if (caster == null) return false;
                    for (int i = 0; i < subject.Statuses.Count; i++)
                    {
                        StatusInstance s = subject.Statuses[i];
                        if (s.Kind == StatusKind.Ledger && s.SourceId == caster.Id
                            && s.StoredCap > 0f && s.Stored >= s.StoredCap)
                            return true;
                    }
                    return false;
                }

                // ---------- answered by the Sim, not here ----------
                //
                // Turn progress is a property of the round's live order, which a static function
                // has no access to. Returning false rather than throwing keeps this usable as the
                // pure arithmetic half the harness tests directly, and the Sim's own gate answers
                // these three before it ever reaches this switch - so the only way to land here
                // with one of them is to call this function from outside a battle, where "false"
                // is the honest answer.
                case EffectCondition.TurnProgressAbove:
                case EffectCondition.TurnProgressBelow:
                case EffectCondition.CasterActsBeforeSubject:
                    return false;
            }

            // An unknown member is refused rather than waved through. A condition nobody has taught
            // this method about must not silently become "always fires" - that turns a content typo
            // into a hero doing more than it says, which is the harder direction to notice.
            return false;
        }

        /// <summary>
        /// The caster's own entry of `kind` on `holder`, or null. A record belongs to whoever wrote
        /// it: two heroes running the same mark on one body each read their own.
        ///
        /// A null caster matches any source, which is the path a condition authored on a unit-less
        /// context takes; it is not reachable from an ability, since every ability has a caster.
        /// </summary>
        private static StatusInstance FindRecord(CombatUnit holder, StatusKind kind, CombatUnit caster)
        {
            if (holder == null || kind == StatusKind.None) return null;
            for (int i = 0; i < holder.Statuses.Count; i++)
            {
                StatusInstance s = holder.Statuses[i];
                if (s.Kind != kind) continue;
                if (caster != null && s.SourceId != caster.Id) continue;
                return s;
            }
            return null;
        }

        /// <summary>
        /// Which mark kind a field naming a status means. Mark unless it explicitly says MarkAlt,
        /// so an effect that does not care about the distinction gets the common one and a typo
        /// naming some unrelated status cannot silently point a mark rule at a Burn.
        /// </summary>
        public static StatusKind MarkKindOf(StatusKind named)
        {
            return named == StatusKind.MarkAlt ? StatusKind.MarkAlt : StatusKind.Mark;
        }

        /// <summary>
        /// Read one of the four comparable stats from a unit. Used by CasterStatAboveTarget /
        /// CasterStatBelowTarget to answer cross-unit comparisons without branching in the
        /// condition switch.
        /// </summary>
        private static float StatValue(CombatUnit unit, ComparedStat stat)
        {
            switch (stat)
            {
                case ComparedStat.Speed:   return unit.EffectiveSpeed;
                case ComparedStat.Energy:  return unit.Energy;
                case ComparedStat.Attack:  return unit.EffectiveAttack;
                case ComparedStat.Hp:      return unit.Hp;
                default:                   return 0f;
            }
        }

        /// <summary>Public mirror of the Sim's TargetHpTerm, exposed for the harness. Pure
        /// arithmetic on an EffectDef and a CombatUnit — no Sim state needed.</summary>
        public static float TargetHpTerm(EffectDef effect, CombatUnit target)
        {
            if (target == null) return 0f;
            float maxHp = target.EffectiveMaxHp;
            float missingTerm = (maxHp - target.Hp) * effect.TargetMissingHpRatio;
            if (effect.MissingHpCap > 0f)
            {
                float cap = maxHp * effect.MissingHpCap;
                if (missingTerm > cap) missingTerm = cap;
            }
            float maxHpTerm = maxHp * effect.TargetMaxHpRatio;
            if (effect.MaxHpCap > 0f)
            {
                float cap = maxHp * effect.MaxHpCap;
                if (maxHpTerm > cap) maxHpTerm = cap;
            }
            return maxHpTerm
                 + target.Hp * effect.TargetCurrentHpRatio
                 + missingTerm;
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
            /// True while a Mirrored Wound reflect is being delivered, bounding it to one hop.
            ///
            /// Not latent, unlike _inRedirect: two Mirrored Wound wearers on opposite sides is an
            /// ordinary thing for a player to build toward, and without this the first hit between
            /// them recurses until the stack gives out. The reflect cannot use MaxPassiveDepth for
            /// this because it is not a passive and never passes through TriggerPassive.
            /// </summary>
            private bool _inReflect;

            /// <summary>
            /// True while a Covered share is being delivered to a protector, bounding the redirect
            /// to one hop. Not latent: two units covering each other is an ordinary thing for a
            /// player to build, and without this the first hit between them passes a share back and
            /// forth until the stack gives out. Same shape as _inRedirect and _inReflect.
            /// </summary>
            private bool _inCover;

            /// <summary>True while an OnAllyWouldDie broadcast is being delivered, bounding the
            /// interception to one attempt per hit.</summary>
            private bool _inDeathCheck;

            /// <summary>
            /// The round's live initiative list, and the index of the turn being taken out of it.
            /// Null outside the round loop - Execute() fires the BattleStart passives before any
            /// round exists, and an effect that fires there has no round order to reorder. Both
            /// ApplyTurnShift and ApplySwapTurnOrder check for that rather than assuming a round is
            /// in progress.
            /// </summary>
            private List<CombatUnit> _order;
            private int _orderIndex;

            /// <summary>
            /// The unit whose death is being dispatched, non-null only while OnEnemyDefeated /
            /// OnAllyDefeated are being broadcast. TargetMode.DefeatedUnit and
            /// ConditionSubject.Defeated both resolve through this and to nobody outside the
            /// window, which is why neither is reachable on an ordinary turn.
            /// </summary>
            private CombatUnit _defeated;
            private bool _inDefeatDispatch;

            /// <summary>
            /// The enemy that just crossed the active-skill energy threshold while carrying
            /// TurnDebt. Non-null only while OnEnemyEnergyThreshold is being broadcast.
            /// TargetMode.ThresholdUnit resolves through this and to nobody outside the window,
            /// the same pattern _defeated / DefeatedUnit uses.
            /// </summary>
            private CombatUnit _thresholdUnit;

            /// <summary>
            /// The unit a broadcast is about - the carrier of a status that expired, the target
            /// of a DoT tick, etc. Non-null only while a BroadcastPassive is in flight.
            /// ConditionSubject.BroadcastSubject resolves through this and to nobody outside the
            /// window, the same pattern _defeated and _thresholdUnit use.
            /// </summary>
            private CombatUnit _broadcastSubject;

            /// <summary>The source unit id of the current broadcast (e.g. healer for OnAllyHealed),
            /// or -1 outside a broadcast that carries source identity.</summary>
            private int _broadcastSourceId = -1;

            /// <summary>The unit that was just damaged, healed or otherwise affected in a broadcast.
            /// Set at every broadcast site that has a clear victim or recipient. Resolves
            /// TargetMode.BroadcastVictim — the target-mode counterpart of BroadcastSubject that
            /// resolves BEFORE the Taunt anchor so a provoke cannot intercept it.</summary>
            private CombatUnit _broadcastVictim;

            /// <summary>The unit that broke the current shield, or -1 outside OnShieldBreak.
            /// Read by EffectCondition.ShieldBrokenByCaster.</summary>
            private int _shieldBreakerId = -1;

            // ---------- runtime values: set at the moment they happen, read by ScaleBy* ----------
            private float _lastLedgerRelease;
            private float _lastRedirectAmount;
            private float _lastRemovedStacks;

            /// <summary>
            /// The per-target budget of the passive currently resolving, or null. Set by
            /// TriggerPassive around its ResolveAbility call and restored afterwards, so a passive
            /// that fires inside another passive does not inherit or clobber its budget.
            ///
            /// It lives here rather than as a ResolveAbility parameter because ResolveAbility is
            /// also the path every Basic and Active takes, and threading a passive-only concept
            /// through both would put it in the signature of everything.
            /// </summary>
            private CombatUnit _perTargetOwner;
            private int _perTargetIndex;
            private int _perTargetLimit;
            private bool _perTargetPerRound;

            /// <summary>StatusKinds that expired in the current ExpireStatuses sweep. Set before
            /// TriggerPassive(OnStatusExpired) so a passive with TriggerStatus can filter.</summary>
            private readonly List<StatusKind> _expiredKinds = new List<StatusKind>();

            /// <summary>The status kind that was just applied in an OnEnemyBuffGained broadcast,
            /// or None outside that window. Dispel reads this to skip the triggering buff when
            /// the just-gained exclusion is needed.</summary>
            private StatusKind _broadcastBuff = StatusKind.None;

            /// <summary>How many targets the previous effect in the current ability resolved
            /// against. Read by PerTarget to scale Hits.</summary>
            private int _lastEffectTargetCount;

            /// <summary>The last unit an effect in the current ability resolved against. Set after
            /// each effect's per-target loop, cleared at ability boundaries. Lets a Self-targeted
            /// effect condition on the target of the previous effect via ConditionSubject.LastTarget.
            /// </summary>
            private CombatUnit _lastTarget;

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

                    for (int i = 0; i < _units.Count; i++) _units[i].BeginRound();

                    // The list is live and the cursor is a field rather than a loop local, so an
                    // effect resolving mid-round can move the part of the order that has not
                    // happened yet - that is what TurnShift and SwapTurnOrder do. Count is re-read
                    // every iteration on purpose.
                    //
                    // Termination is structural. Both of those operations REORDER the tail; neither
                    // adds an entry, so the list cannot grow while it is being walked and the walk
                    // is bounded by _units.Count. Gale's kill-driven re-entry, which did splice new
                    // entries in and needed a cap to bound it, is gone.
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
                    if (talent != null)
                    {
                        unit.AddPassive(talent);
                        ApplyTalentOverrides(unit, talent);
                        ApplyTalentAppends(unit, talent);
                    }

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

            // ------------------------------------------------------------------ talent overrides

            /// <summary>
            /// Applies a talent's parameter overrides to the unit's abilities. Each override names
            /// an ability ("basic", "active", "passive1", "passive2"), an effect index, a field
            /// and an additive delta. The affected EffectDef (and its parent AbilityDef for basic /
            /// active) is cloned before mutation, exactly the way relics clone before rewriting
            /// Target and Hits, so the content-database copy is never touched.
            ///
            /// Silently ignores an override that names an ability or index the unit does not have,
            /// which is the same tolerance TalentAt applies to an out-of-range selection.
            /// </summary>
            private static void ApplyTalentOverrides(CombatUnit unit, PassiveDef talent)
            {
                if (talent.Overrides.Count == 0) return;

                for (int i = 0; i < talent.Overrides.Count; i++)
                {
                    TalentOverrideDef ov = talent.Overrides[i];
                    if (string.IsNullOrEmpty(ov.Ability) || string.IsNullOrEmpty(ov.Field)) continue;

                    string ab = ov.Ability.ToLowerInvariant();
                    if (ab == "basic" || ab == "active")
                    {
                        AbilityDef src = ab == "basic" ? unit.Basic : unit.Active;
                        if (src == null) continue;

                        // energyCost is on the ability, not on an effect
                        if (ov.Field.ToLowerInvariant() == "energycost")
                        {
                            AbilityDef clone = CloneAbility(src);
                            clone.EnergyCost += (int)Math.Round(ov.Delta, MidpointRounding.AwayFromZero);
                            if (ab == "basic") unit.Basic = clone; else unit.Active = clone;
                            continue;
                        }

                        if (ov.EffectIndex < 0 || ov.EffectIndex >= src.Effects.Count) continue;
                        AbilityDef aclone = CloneAbility(src);
                        ApplyDelta(aclone.Effects[ov.EffectIndex], ov.Field, ov.Delta);
                        if (ab == "basic") unit.Basic = aclone; else unit.Active = aclone;
                    }
                    else if (ab == "passive1" || ab == "passive2")
                    {
                        int pi = ab == "passive1" ? 0 : 1;
                        if (pi >= unit.Passives.Count) continue;
                        PassiveDef passive = unit.Passives[pi];

                        // Threshold is on the passive itself, not on an effect
                        if (ov.Field.ToLowerInvariant() == "threshold")
                        {
                            PassiveDef clone = ClonePassive(passive);
                            clone.Threshold += ov.Delta;
                            unit.Passives[pi] = clone;
                            continue;
                        }

                        if (ov.EffectIndex < 0 || ov.EffectIndex >= passive.Effects.Count) continue;
                        PassiveDef pclone = ClonePassive(passive);
                        ApplyDelta(pclone.Effects[ov.EffectIndex], ov.Field, ov.Delta);
                        unit.Passives[pi] = pclone;
                    }
                }
            }

            /// <summary>
            /// Applies a talent's appended effects to the unit's abilities. Each append names an
            /// ability slot and carries a full EffectDef that is added to the end of that ability's
            /// effect list. The ability is cloned before mutation, same as overrides.
            /// </summary>
            private static void ApplyTalentAppends(CombatUnit unit, PassiveDef talent)
            {
                if (talent.Appends.Count == 0) return;

                for (int i = 0; i < talent.Appends.Count; i++)
                {
                    TalentAppendDef ap = talent.Appends[i];
                    if (string.IsNullOrEmpty(ap.Ability) || ap.Effect == null) continue;

                    string ab = ap.Ability.ToLowerInvariant();
                    if (ab == "basic" || ab == "active")
                    {
                        AbilityDef src = ab == "basic" ? unit.Basic : unit.Active;
                        if (src == null) continue;
                        AbilityDef aclone = CloneAbility(src);
                        aclone.Effects.Add(ap.Effect.Clone());
                        if (ab == "basic") unit.Basic = aclone; else unit.Active = aclone;
                    }
                    else if (ab == "passive1" || ab == "passive2")
                    {
                        int pi = ab == "passive1" ? 0 : 1;
                        if (pi >= unit.Passives.Count) continue;
                        PassiveDef pclone = ClonePassive(unit.Passives[pi]);
                        pclone.Effects.Add(ap.Effect.Clone());
                        unit.Passives[pi] = pclone;
                    }
                }
            }

            private static AbilityDef CloneAbility(AbilityDef src)
            {
                AbilityDef a = new AbilityDef();
                a.Id = src.Id;
                a.Name = src.Name;
                a.Description = src.Description;
                a.Kind = src.Kind;
                a.EnergyCost = src.EnergyCost;
                a.Vfx = src.Vfx;
                for (int i = 0; i < src.Effects.Count; i++) a.Effects.Add(src.Effects[i].Clone());
                return a;
            }

            private static PassiveDef ClonePassive(PassiveDef src)
            {
                PassiveDef p = new PassiveDef();
                p.Id = src.Id;
                p.Name = src.Name;
                p.Description = src.Description;
                p.Trigger = src.Trigger;
                p.Threshold = src.Threshold;
                p.OncePerBattle = src.OncePerBattle;
                p.Limit = src.Limit;
                p.LimitRounds = src.LimitRounds;
                p.PerTargetLimit = src.PerTargetLimit;
                p.PerTargetPerRound = src.PerTargetPerRound;
                p.TriggerStatus = src.TriggerStatus;
                p.TriggerSourceIsSelf = src.TriggerSourceIsSelf;
                p.OutputModStatus = src.OutputModStatus;
                p.OutputModPerStack = src.OutputModPerStack;
                p.OutputModScope = src.OutputModScope;
                p.OutputModMarkedOnly = src.OutputModMarkedOnly;
                p.StatModStatus = src.StatModStatus;
                p.StatModPerStack = src.StatModPerStack;
                p.StatModKind = src.StatModKind;
                p.Vfx = src.Vfx;
                p.Effects = new List<EffectDef>();
                for (int i = 0; i < src.Effects.Count; i++) p.Effects.Add(src.Effects[i].Clone());
                return p;
            }

            private static void ApplyDelta(EffectDef e, string field, float delta)
            {
                switch (field.ToLowerInvariant())
                {
                    case "power":      e.Power += delta; break;
                    case "magnitude":  e.Magnitude += delta; break;
                    case "maxstacks":  e.MaxStacks += (int)Math.Round(delta, MidpointRounding.AwayFromZero); break;
                    case "duration":   e.Duration += (int)Math.Round(delta, MidpointRounding.AwayFromZero); break;
                    case "flat":       e.Flat += delta; break;
                    case "chance":     e.Chance += delta; break;
                    case "selfhpratio": e.SelfHpRatio += delta; break;
                    case "targetmaxhpratio": e.TargetMaxHpRatio += delta; break;
                    case "targetmissinghpratio": e.TargetMissingHpRatio += delta; break;
                    case "storedcap":  e.StoredCap += delta; break;
                    case "hits":       e.Hits += (int)Math.Round(delta, MidpointRounding.AwayFromZero); break;
                    case "perstack":   e.PerStack += delta; break;
                    case "conditionvalue": e.ConditionValue += delta; break;
                    case "conditionvalue3": e.ConditionValue3 += delta; break;
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

                // History, stamped as the turn opens rather than as it closes: a passive firing
                // inside this turn should see the turn it is part of as having happened.
                unit.ActedThisRound = true;
                unit.LastTurnRound = _round;

                TickDamageOverTime(unit);
                if (!unit.Alive || IsBattleOver()) { ExpireStatuses(unit); return; }

                RepayTurnDebt(unit);
                if (!unit.Alive || IsBattleOver()) { ExpireStatuses(unit); return; }

                TriggerPassive(unit, PassiveTrigger.TurnStart, 0);
                if (!unit.Alive || IsBattleOver()) { ExpireStatuses(unit); return; }

                // Stun: the whole turn is skipped - no Basic Attack, no Active, no energy gain.
                // Cleared by the ExpireStatuses() decrement below, the same RoundsLeft clock every
                // other status uses; there is no separate timer to keep in sync.
                //
                // A skipped turn cannot become a second turn. `_order` is mutated mid-walk by
                // ApplyTurnShift and ApplySwapTurnOrder, but both REORDER the tail and neither
                // inserts, so no unit gains an entry it did not already have and there is no call
                // path back into TakeTurn to bound. A unit moved backwards past the cursor after it
                // has acted is the one way a second entry could be reached, and both operations
                // work on `_orderIndex + 1` onward for exactly that reason.
                //
                // Gale's kill-driven re-entry, which did splice entries in and needed a cap, has
                // been removed along with the rest of the engine-owned aspect rules.
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

                // Two flags and a count, not a list: no description in the roster asks about more
                // than the last two actions, and "the same action type on each of its last two
                // turns" needs exactly this much.
                unit.PreviousActionWasActive = unit.LastActionWasActive;
                unit.LastActionWasActive = usedActive;
                unit.ActionsTaken++;
                if (usedActive)
                {
                    unit.Energy = 0;
                    BattleEvent cast = Emit(BattleEventType.AbilityCast, unit.Id, unit.Id);
                    cast.AbilityId = unit.Active.Id;
                    cast.AbilityName = unit.Active.Name;
                    cast.Vfx = unit.Active.Vfx;
                    EmitEnergy(unit);

                    // Raised BEFORE the cast resolves, so a passive that answers an enemy active
                    // acts on the board the caster is about to hit rather than on the wreckage
                    // afterwards. That is what "when an Anticipated enemy uses an active skill"
                    // asks for, and it is also the only ordering that lets an answer matter.
                    {
                        CombatUnit prevSubject = _broadcastSubject;
                        _broadcastSubject = unit;
                        BroadcastPassive(!unit.IsPlayer, PassiveTrigger.OnEnemyActive, 0, null);
                        _broadcastSubject = prevSubject;
                    }
                    if (!unit.Alive || IsBattleOver()) { ExpireStatuses(unit); return; }

                    {
                        CombatUnit prevLT = _lastTarget;
                        ResolveAbility(unit, unit.Active, 0);
                        _lastTarget = prevLT;
                    }
                }
                else
                {
                    AbilityDef basic = unit.Basic;
                    BattleEvent attack = Emit(BattleEventType.BasicAttack, unit.Id, unit.Id);
                    attack.AbilityId = basic.Id;
                    attack.AbilityName = basic.Name;
                    attack.Vfx = basic.Vfx;
                    CombatUnit prevLastTarget = _lastTarget;
                    ResolveAbility(unit, basic, 0);
                    // _lastTarget now holds the basic's final target (depth-0 does not restore).
                    // AfterBasicAttack passives can reference it via ConditionSubject.LastTarget.

                    if (unit.Alive)
                    {
                        // -1: a unit's own basic-attack refill has no granter, so it never redirects.
                        AddEnergy(unit, _cfg.EnergyPerBasicAttack, -1);
                        TriggerPassive(unit, PassiveTrigger.AfterBasicAttack, 0);
                    }
                    _lastTarget = prevLastTarget;
                }

                // Turn-end broadcasts. No shipped passive uses OnAllyTurnEnd or OnEnemyTurnEnd,
                // so these broadcasts walk empty listener sets and are pure no-ops for every
                // existing battle. Placed after ability resolution and before ExpireStatuses,
                // which is the natural "end of the turn" insertion point.
                if (unit.Alive && !IsBattleOver())
                {
                    CombatUnit prevSubject = _broadcastSubject;
                    _broadcastSubject = unit;
                    BroadcastPassive(unit.IsPlayer, PassiveTrigger.OnAllyTurnEnd, 0, unit);
                    if (unit.Alive && !IsBattleOver())
                        BroadcastPassive(!unit.IsPlayer, PassiveTrigger.OnEnemyTurnEnd, 0, null);
                    _broadcastSubject = prevSubject;
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
            /// occur in shipped content today. It is guarded rather than left alone because any
            /// content that lands two DoTs on one unit behind a ward reaches it, which moves the case
            /// from unreachable toward merely rare. Any behavioural change here would therefore be a
            /// regression, not a fix - the identical output is the point.
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
                        float burnDmg = s.Magnitude;
                        CombatUnit dotSource = s.SourceId >= 0 ? FindUnit(s.SourceId) : null;
                        burnDmg *= OutputModMultiplier(dotSource, unit, OutputModScope.DoTOnly);
                        int damage = (int)Math.Round(burnDmg, MidpointRounding.AwayFromZero);
                        if (damage < 1) damage = 1;
                        // named for what it actually is - not whatever the source last cast, so the
                        // Debrief and any on-screen attribution can't misname a burn tick as a fresh hit
                        DealRawDamage(s.SourceId, unit, damage, false, "burn", StatusKind.Burn, 1f, StatusInfo.DisplayName(StatusKind.Burn));
                        if (!unit.Alive) return;

                        // Applier-side lifecycle: the unit that applied this Burn hears about the
                        // tick. Broadcast to the source's side with _broadcastSubject set to the
                        // carrier, so a condition on BroadcastSubject can read the carrier's HP.
                        if (s.SourceId >= 0)
                        {
                            CombatUnit burnSource = FindUnit(s.SourceId);
                            if (burnSource != null && burnSource.IsPlayer != unit.IsPlayer)
                            {
                                CombatUnit prevSubject = _broadcastSubject;
                                _broadcastSubject = unit;
                                BroadcastPassive(burnSource.IsPlayer, PassiveTrigger.OnAppliedDoTTick, 0, null);
                                _broadcastSubject = prevSubject;
                                if (!unit.Alive || IsBattleOver()) return;
                            }
                        }
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
            /// RoundsLeft is a round counter that happens to be decremented at the end of the
            /// carrier's turn, which is the same thing only while a unit takes exactly one turn per
            /// round. The turn-order operations make that assumption worth stating: a unit moved
            /// backwards past the cursor after it has acted would otherwise shed its own Slow,
            /// Silence, Stun, DefenseDown and buffs at double rate, and break CheckStatusExpiry's
            /// "expires exactly on schedule" rule for that carrier.
            ///
            /// The flag is reset in CombatUnit.BeginRound. Both movers work on the not-yet-acted
            /// tail, so nothing reaches here twice today and the gate is a no-op for every battle
            /// in shipped content - it is the guarantee that keeps it one.
            /// </summary>
            private void ExpireStatuses(CombatUnit unit)
            {
                if (unit.StatusesExpiredThisRound) return;
                unit.StatusesExpiredThisRound = true;

                bool expired = false;
                bool expiredFromPlayer = false;
                bool expiredFromEnemy = false;
                _expiredKinds.Clear();
                for (int i = unit.Statuses.Count - 1; i >= 0; i--)
                {
                    StatusInstance s = unit.Statuses[i];
                    s.RoundsLeft--;
                    if (s.RoundsLeft > 0) continue;

                    // Ledger auto-release: pay out Stored as damage to the holder before the entry
                    // is removed. Power is baked into StoredCap at apply-time, so the full Stored
                    // amount is the payout. Only fires when the Ledger duration genuinely expires;
                    // a cleanse, a dispel and a death each remove statuses by their own path and
                    // none of them should auto-release.
                    if (s.Kind == StatusKind.Ledger && s.AutoRelease && s.Stored > 0f)
                    {
                        int payout = (int)Math.Round(s.Stored, MidpointRounding.AwayFromZero);
                        _lastLedgerRelease = s.Stored;
                        s.Stored = 0f;
                        if (payout > 0 && unit.Alive)
                        {
                            if (s.AutoReleaseAsHeal)
                            {
                                // Heal-shaped auto-release. Defaults to the holder; an authored
                                // AutoReleaseTarget routes to someone else.
                                CombatUnit healTarget = unit;
                                if (s.AutoReleaseTarget != TargetMode.Self)
                                {
                                    CombatUnit applier = FindUnit(s.SourceId);
                                    if (applier != null)
                                    {
                                        List<CombatUnit> targets = ResolveTargets(applier, s.AutoReleaseTarget);
                                        if (targets.Count > 0) healTarget = targets[0];
                                    }
                                }
                                ApplyHealValue(s.SourceId, healTarget, payout, "heal", true);
                            }
                            else
                            {
                                DealRawDamage(s.SourceId, unit, payout, false, "burst",
                                              StatusKind.None, 1f, "Ledger");
                            }
                            if (!unit.Alive) return;
                        }
                    }

                    unit.Statuses.RemoveAt(i);

                    // THE SHIELD POOL FOLLOWS ITS OWN STATUS. Without this the entry is removed and
                    // CombatUnit.Shield keeps absorbing: the tag leaves the bar while the ward is
                    // still working, which is the one place in the engine where what the player is
                    // shown and what the simulation does genuinely disagree. Measured in shipped
                    // content before the fix: 26 absorptions across 16 of 144 battles happened
                    // after the status was already gone, soaking 2673 damage invisibly.
                    //
                    // The reverse direction was already true - DealRawDamage removes the status the
                    // moment the pool empties - so this is the missing half of a rule the engine
                    // already half-kept, not a new rule.
                    if (s.Kind == StatusKind.Shield) unit.Shield = 0f;

                    BattleEvent e = Emit(BattleEventType.StatusExpired, unit.Id, unit.Id);
                    e.Status = s.Kind;
                    e.Duration = 1;  // marks a natural duration-based expiry, not a removal
                    expired = true;
                    if (!_expiredKinds.Contains(s.Kind)) _expiredKinds.Add(s.Kind);

                    // Track which side the expired status was applied FROM, for the applier-side
                    // lifecycle broadcast below. Only statuses applied by the OTHER side matter:
                    // a hero whose own buff expired is not what "when a debuff I applied expires"
                    // means. The source side is determined by looking up SourceId.
                    if (s.SourceId >= 0)
                    {
                        CombatUnit source = FindUnit(s.SourceId);
                        if (source != null && source.IsPlayer != unit.IsPlayer)
                        {
                            if (source.IsPlayer) expiredFromPlayer = true;
                            else expiredFromEnemy = true;
                        }
                    }
                }

                // Raised once after the whole sweep rather than per status, so a unit shedding
                // three at once answers the trigger once. Only a genuine duration expiry reaches
                // here: a cleanse, a dispel, an absorbed ward and a death each remove statuses by
                // their own path and are each a different thing happening.
                if (expired) TriggerPassive(unit, PassiveTrigger.OnStatusExpired, 0);

                // Applier-side lifecycle: broadcast to the side that APPLIED each expired status.
                // A unit on the player side whose Burn expired fires OnAppliedStatusExpired on the
                // enemy side (the applier's side), with _broadcastSubject set to the carrier.
                // Collected per-source-side, so one broadcast per side rather than one per status.
                if (expiredFromPlayer || expiredFromEnemy)
                {
                    CombatUnit prevSubject = _broadcastSubject;
                    _broadcastSubject = unit;
                    if (expiredFromPlayer)
                        BroadcastPassive(true, PassiveTrigger.OnAppliedStatusExpired, 0, null);
                    if (expiredFromEnemy)
                        BroadcastPassive(false, PassiveTrigger.OnAppliedStatusExpired, 0, null);
                    _broadcastSubject = prevSubject;
                }
            }

            // ------------------------------------------------------------------ abilities

            /// <summary>
            /// ConditionMet plus the three conditions that need the round's live turn order.
            ///
            /// The split is deliberate rather than awkward. `ConditionMet` is public, static and
            /// pure, which is what lets the harness test its arithmetic without building a battle,
            /// and turn progress is the first condition in the enum that is not a property of two
            /// units alone - it is a property of where they sit in a list the Sim owns. Rather than
            /// hand the Sim's internals to a static function, the contextual three are answered
            /// here and everything else falls through unchanged.
            ///
            /// ConditionSubject.Defeated is resolved here too, for the same reason: the corpse is
            /// Sim state, not something a caller could pass in.
            /// </summary>
            private bool ConditionMetHere(EffectDef effect, CombatUnit caster, CombatUnit target)
            {
                if (effect == null || effect.Condition == EffectCondition.None)
                    return Condition2MetHere(effect, caster, target);

                CombatUnit subject = effect.ConditionOn == ConditionSubject.Caster ? caster
                                   : effect.ConditionOn == ConditionSubject.Defeated ? _defeated
                                   : effect.ConditionOn == ConditionSubject.BroadcastSubject ? _broadcastSubject
                                   : effect.ConditionOn == ConditionSubject.LastAttacker ? ResolveLastAttacker()
                                   : effect.ConditionOn == ConditionSubject.LastTarget ? _lastTarget
                                   : target;
                if (subject == null) return false;

                bool first;
                switch (effect.Condition)
                {
                    // Inclusive at the boundary, matching HpBelow / HpAbove one file over: two
                    // meanings of "above" in one engine is a worse trap than the lost precision.
                    case EffectCondition.TurnProgressAbove:
                        first = TurnProgressOf(subject) >= effect.ConditionValue; break;

                    case EffectCondition.TurnProgressBelow:
                        first = TurnProgressOf(subject) <= effect.ConditionValue; break;

                    // Position, not progress, and the two disagree: a unit that has already acted
                    // reads 0 progress but is not "after" anybody, it is out of the round. Both
                    // sides must still be waiting for this to mean anything.
                    case EffectCondition.CasterActsBeforeSubject:
                    {
                        int mine = PendingPositionOf(caster);
                        int theirs = PendingPositionOf(subject);
                        first = mine >= 0 && theirs >= 0 && mine < theirs;
                        break;
                    }

                    case EffectCondition.ShieldBrokenByCaster:
                        first = _shieldBreakerId >= 0 && caster != null && _shieldBreakerId == caster.Id;
                        break;

                    default:
                    {
                        // Everything else, including a Defeated or BroadcastSubject asking an ordinary
                        // question, is answered by the pure function - with the resolved subject
                        // substituted in.
                        if (effect.ConditionOn == ConditionSubject.Defeated
                            || effect.ConditionOn == ConditionSubject.BroadcastSubject
                            || effect.ConditionOn == ConditionSubject.LastAttacker
                            || effect.ConditionOn == ConditionSubject.LastTarget)
                        {
                            EffectDef asTarget = effect.Clone();
                            asTarget.ConditionOn = ConditionSubject.Target;
                            first = ConditionMet(asTarget, caster, subject);
                        }
                        else
                        {
                            first = ConditionMet(effect, caster, target);
                        }
                        break;
                    }
                }

                bool andResult = first && Condition2MetHere(effect, caster, target);
                if (andResult) return true;
                return Condition3MetHere(effect, caster, target);
            }

            /// <summary>
            /// The second gate of an AND-conditioned effect. When Condition2 is None (the default),
            /// returns true without reading anything, so every single-conditioned effect is unchanged.
            /// Follows the same subject-resolution and pure/contextual split as the first gate.
            /// </summary>
            private bool Condition2MetHere(EffectDef effect, CombatUnit caster, CombatUnit target)
            {
                if (effect == null || effect.Condition2 == EffectCondition.None) return true;

                // Build a synthetic EffectDef carrying Condition2 in the Condition slot, so both
                // ConditionMet and the three contextual cases above can answer it unchanged. Reuses
                // Clone so the synthesis cannot diverge from the fields ConditionMet reads.
                EffectDef gate2 = effect.Clone();
                gate2.Condition = effect.Condition2;
                gate2.ConditionOn = effect.ConditionOn2;
                gate2.ConditionStatus = effect.ConditionStatus2;
                gate2.ConditionValue = effect.ConditionValue2;
                gate2.ComparedStat = effect.ComparedStat2;
                gate2.Condition2 = EffectCondition.None; // prevent infinite recursion

                CombatUnit subject2 = gate2.ConditionOn == ConditionSubject.Caster ? caster
                                    : gate2.ConditionOn == ConditionSubject.Defeated ? _defeated
                                    : gate2.ConditionOn == ConditionSubject.BroadcastSubject ? _broadcastSubject
                                    : gate2.ConditionOn == ConditionSubject.LastAttacker ? ResolveLastAttacker()
                                    : gate2.ConditionOn == ConditionSubject.LastTarget ? _lastTarget
                                    : target;
                if (subject2 == null) return false;

                switch (gate2.Condition)
                {
                    case EffectCondition.TurnProgressAbove:
                        return TurnProgressOf(subject2) >= gate2.ConditionValue;
                    case EffectCondition.TurnProgressBelow:
                        return TurnProgressOf(subject2) <= gate2.ConditionValue;
                    case EffectCondition.CasterActsBeforeSubject:
                    {
                        int mine = PendingPositionOf(caster);
                        int theirs = PendingPositionOf(subject2);
                        return mine >= 0 && theirs >= 0 && mine < theirs;
                    }
                }

                if (gate2.ConditionOn == ConditionSubject.Defeated
                    || gate2.ConditionOn == ConditionSubject.BroadcastSubject
                    || gate2.ConditionOn == ConditionSubject.LastAttacker
                    || gate2.ConditionOn == ConditionSubject.LastTarget)
                {
                    gate2.ConditionOn = ConditionSubject.Target;
                    return ConditionMet(gate2, caster, subject2);
                }
                return ConditionMet(gate2, caster, target);
            }

            /// <summary>
            /// OR branch: when Condition3 is not None, this is an independent second chance for
            /// the effect to resolve. Evaluation is (C1 AND C2) OR C3. Follows the same
            /// subject-resolution and pure/contextual split as the first two gates.
            /// </summary>
            private bool Condition3MetHere(EffectDef effect, CombatUnit caster, CombatUnit target)
            {
                if (effect == null || effect.Condition3 == EffectCondition.None) return false;

                EffectDef gate3 = effect.Clone();
                gate3.Condition = effect.Condition3;
                gate3.ConditionOn = effect.ConditionOn3;
                gate3.ConditionStatus = effect.ConditionStatus3;
                gate3.ConditionValue = effect.ConditionValue3;
                gate3.ComparedStat = effect.ComparedStat3;
                gate3.Condition2 = EffectCondition.None;
                gate3.Condition3 = EffectCondition.None;

                CombatUnit subject3 = gate3.ConditionOn == ConditionSubject.Caster ? caster
                                    : gate3.ConditionOn == ConditionSubject.Defeated ? _defeated
                                    : gate3.ConditionOn == ConditionSubject.BroadcastSubject ? _broadcastSubject
                                    : gate3.ConditionOn == ConditionSubject.LastAttacker ? ResolveLastAttacker()
                                    : gate3.ConditionOn == ConditionSubject.LastTarget ? _lastTarget
                                    : target;
                if (subject3 == null) return false;

                switch (gate3.Condition)
                {
                    case EffectCondition.TurnProgressAbove:
                        return TurnProgressOf(subject3) >= gate3.ConditionValue;
                    case EffectCondition.TurnProgressBelow:
                        return TurnProgressOf(subject3) <= gate3.ConditionValue;
                    case EffectCondition.CasterActsBeforeSubject:
                    {
                        int mine = PendingPositionOf(caster);
                        int theirs = PendingPositionOf(subject3);
                        return mine >= 0 && theirs >= 0 && mine < theirs;
                    }
                }

                if (gate3.ConditionOn == ConditionSubject.Defeated
                    || gate3.ConditionOn == ConditionSubject.BroadcastSubject
                    || gate3.ConditionOn == ConditionSubject.LastAttacker
                    || gate3.ConditionOn == ConditionSubject.LastTarget)
                {
                    gate3.ConditionOn = ConditionSubject.Target;
                    return ConditionMet(gate3, caster, subject3);
                }
                return ConditionMet(gate3, caster, target);
            }

            /// <summary>
            /// How far through the round a unit is, as a fraction from 0 to 1. DERIVED FROM
            /// POSITION - there is no gauge, and this pass deliberately did not add one.
            ///
            /// WHY NOT A GAUGE. An ATB gauge is a per-unit float that fills with Speed and empties
            /// on a turn, and it is the natural home for "50% turn progress". Adding one means the
            /// round stops being a sort walked once and becomes a scheduler: turn order stops being
            /// a pure function of Speed, every existing fingerprint moves, and the balance table
            /// that 24 stages are tuned against is measured against a different game. That is a
            /// larger change than all six mechanics in this pass put together, and nothing in the
            /// roster needs the resolution it would buy.
            ///
            /// WHAT IT COSTS INSTEAD. Progress is quantised to the number of turns left in the
            /// round: with four units still to act, the only readable values are 1, 0.75, 0.5, 0.25
            /// and 0. "Above 50%" means "in the front half of what is left", which is what the two
            /// abilities asking for it actually want, and 51% and 74% are indistinguishable. A unit
            /// that has already acted reads 0, because its next turn is a whole round away rather
            /// than imminent. Outside the round loop, and at BattleStart, everything reads 0.
            ///
            /// Draws nothing from _rng, so reading progress cannot desync a seeded replay.
            /// </summary>
            private float TurnProgressOf(CombatUnit unit)
            {
                int at = PendingPositionOf(unit);
                if (at < 0) return 0f;

                int first = _orderIndex + 1;
                int tail = _order.Count - first;
                if (tail <= 1) return 1f;
                return 1f - (at - first) / (float)(tail - 1);
            }

            /// <summary>
            /// A unit's index in the part of the round that has not happened yet, or -1 if it has
            /// already acted, is not in the order, or there is no round in progress.
            /// </summary>
            private int PendingPositionOf(CombatUnit unit)
            {
                if (_order == null || unit == null) return -1;
                int first = _orderIndex + 1;
                if (first >= _order.Count) return -1;
                return _order.IndexOf(unit, first);
            }

            /// <summary>
            /// A debtor pays at its own turn start: Energy in proportion to what it owes, and the
            /// debt is gone.
            ///
            /// The engine reads this status directly, the way it already reads Burn, Regen and
            /// Shield, rather than leaving repayment to a passive. A passive would have to sit on
            /// the DEBTOR, and the debtor is an enemy that knows nothing about the hero who pushed
            /// it - the ability describing the debt belongs to the pusher, so the rule has to live
            /// where the clock is.
            ///
            /// Fires after the damage-over-time tick and before TurnStart passives, so a hero
            /// answering a repayment acts on a board where the Energy has already moved.
            /// Magnitude is the exchange rate, Energy per full round of debt, stamped when the
            /// debt was banked. Stored is a fraction of a round.
            ///
            /// Unreachable in shipped content: nothing applies TurnDebt, so this returns on the
            /// first line of every battle in the game today.
            /// </summary>
            private void RepayTurnDebt(CombatUnit unit)
            {
                if (unit == null || !unit.Alive || !unit.Has(StatusKind.TurnDebt)) return;

                float owed = 0f;
                float rate = 0f;
                int creditor = -1;
                for (int i = unit.Statuses.Count - 1; i >= 0; i--)
                {
                    StatusInstance d = unit.Statuses[i];
                    if (d.Kind != StatusKind.TurnDebt) continue;
                    owed += d.Stored;
                    if (d.Magnitude > rate) rate = d.Magnitude;
                    if (creditor < 0) creditor = d.SourceId;
                    unit.Statuses.RemoveAt(i);

                    BattleEvent gone = Emit(BattleEventType.StatusExpired, d.SourceId, unit.Id);
                    gone.Status = StatusKind.TurnDebt;
                }
                if (owed <= 0f || rate <= 0f) return;

                int cost = (int)Math.Round(owed * rate, MidpointRounding.AwayFromZero);
                if (cost > 0) AddEnergy(unit, -cost, -1);

                // Broadcast to the side that did NOT pay, which is always the side the debt came
                // from: an ability watching a repayment is the ability that applied the debt.
                // The repaying unit is stored as _broadcastSubject so an effect can target or
                // condition on it via TargetMode.BroadcastSubject / ConditionSubject.BroadcastSubject.
                CombatUnit prevSubject = _broadcastSubject;
                _broadcastSubject = unit;
                BroadcastPassive(!unit.IsPlayer, PassiveTrigger.OnTurnDebtRepaid, 0, null);
                _broadcastSubject = prevSubject;
            }

            private void ResolveAbility(CombatUnit caster, AbilityDef ability, int depth)
            {
                if (ability == null) return;
                CombatUnit prevLastTarget = _lastTarget;
                _lastTarget = null;
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
                    // PerStackOn Caster keeps the snapshot this always had: counted once, before
                    // targeting, so every enemy in an AoE is scaled by the same number no matter
                    // what dies partway through. PerStackStatus widens WHICH counter is read and
                    // defaults to Resolve, so an effect that names neither field is unchanged -
                    // CountOf(Resolve) is exactly the call that stood here.
                    // PerTarget: if this effect says "once per target from the previous effect",
                    // multiply Hits by the count the previous effect resolved against. Inert when
                    // PerTarget is false (the default) or when _lastEffectTargetCount is <= 1.
                    if (effect.PerTarget && _lastEffectTargetCount > 1)
                    {
                        EffectDef perT = effect.Clone();
                        perT.Hits = effect.Hits * _lastEffectTargetCount;
                        effect = perT;
                    }

                    bool perStackOnTarget = effect.PerStackOn == ConditionSubject.Target;
                    if (!perStackOnTarget)
                    {
                        int casterStacks = caster.CountOf(effect.PerStackStatus);
                        if (effect.PerStack != 0f && casterStacks > 0)
                        {
                            EffectDef scaled = effect.Clone();
                            float factor = 1f + effect.PerStack * casterStacks;
                            scaled.Power = effect.Power * factor;
                            scaled.Magnitude = effect.Magnitude * factor;
                            if (effect.Kind == EffectKind.GainResolve)
                                scaled.Hits = Math.Max(1, (int)Math.Round(effect.Hits * factor,
                                              MidpointRounding.AwayFromZero));
                            effect = scaled;
                        }
                    }

                    // Targeting runs FIRST and the condition is tested against what it picked,
                    // which is the only order that can answer "is the unit this is landing on
                    // below 35% HP". It also means a conditional RandomEnemy effect draws for its
                    // target exactly as an unconditional one does, whether or not the condition
                    // then holds: the draw belongs to targeting, not to the test, and moving the
                    // test in front of it would make a condition consume or skip an RNG draw.
                    List<CombatUnit> targets = ResolveTargets(caster, effect.Target, null, effect.TargetStatus);

                    // Splash: if the effect is marked Splash, add alive neighbours (slot ± 1)
                    // of every originally-resolved target. Deduplicated so a target adjacent to
                    // two resolved targets is not struck twice.  No RNG draw: adjacency is
                    // deterministic state.  Empty when Splash is false — the loop never fires —
                    // so no existing battle is changed.
                    if (effect.Splash && targets.Count > 0)
                    {
                        List<CombatUnit> extras = new List<CombatUnit>();
                        for (int si = 0; si < targets.Count; si++)
                        {
                            CombatUnit primary = targets[si];
                            if (primary == null) continue;
                            for (int d = -1; d <= 1; d += 2)
                            {
                                CombatUnit neighbour = FindAliveInSlot(primary.IsPlayer, primary.Slot + d);
                                if (neighbour != null && !targets.Contains(neighbour) && !extras.Contains(neighbour))
                                    extras.Add(neighbour);
                            }
                        }
                        targets.AddRange(extras);
                    }

                    // Per target, not per effect. A conditional AoE asks about each enemy on its
                    // own, so "deal damage to all enemies below 40% HP" is one effect rather than a
                    // targeting mode nobody has to invent.
                    bool anyTargetPassed = false;
                    for (int t = 0; t < targets.Count; t++)
                    {
                        CombatUnit target = targets[t];
                        if (target == null) continue;
                        if (!ConditionMetHere(effect, caster, target)) continue;
                        anyTargetPassed = true;

                        // A use is SPENT by the effect that read it, not by the round. Without this
                        // StatusUseAvailable would be a condition that is true every time it is
                        // asked, which is a rider with no ordinal rather than a first-each-round
                        // one. Spent here, after the test and before the effect, so an effect that
                        // kills its own target has still consumed the use it claimed.
                        if (effect.Condition == EffectCondition.StatusUseAvailable)
                        {
                            CombatUnit holder = effect.ConditionOn == ConditionSubject.Caster ? caster
                                              : effect.ConditionOn == ConditionSubject.Defeated ? _defeated
                                              : effect.ConditionOn == ConditionSubject.BroadcastSubject ? _broadcastSubject
                                              : effect.ConditionOn == ConditionSubject.LastTarget ? _lastTarget
                                              : target;
                            StatusInstance used = FindRecord(holder, effect.ConditionStatus, caster);
                            if (used != null) used.UsesThisRound++;
                        }

                        // The per-TARGET half, which cannot be snapshotted: the count is a property
                        // of each target, so it is read here and the clone is per target. Only an
                        // effect that explicitly says PerStackOn Target reaches this, so the
                        // snapshot guarantee above is untouched for everything else.
                        EffectDef landed = effect;
                        if (perStackOnTarget && effect.PerStack != 0f)
                        {
                            int targetStacks = target.CountOf(effect.PerStackStatus);
                            if (targetStacks > 0)
                            {
                                EffectDef scaled = effect.Clone();
                                float factor = 1f + effect.PerStack * targetStacks;
                                scaled.Power = effect.Power * factor;
                                scaled.Magnitude = effect.Magnitude * factor;
                                if (effect.Kind == EffectKind.GainResolve)
                                    scaled.Hits = Math.Max(1, (int)Math.Round(effect.Hits * factor,
                                                  MidpointRounding.AwayFromZero));
                                landed = scaled;
                            }
                        }

                        // THE PER-TARGET BUDGET, spent here rather than at the trigger.
                        //
                        // PassiveLimit is answered from the owner's clock before a passive fires;
                        // this is answered once targeting has run, because it is a limit on where
                        // an effect LANDS and the target's identity does not exist any earlier. A
                        // passive whose budget is spent against one enemy still resolves normally
                        // against another, which is the shape all four descriptions have and the
                        // reason this could not be folded into PassiveLimit.
                        //
                        // Null owner is every Basic and every Active, which take this branch as a
                        // single null test and are unchanged by it.
                        if (_perTargetOwner != null && _perTargetLimit > 0)
                        {
                            if (_perTargetOwner.PassiveFiresAgainst(_perTargetIndex, target.Id,
                                                                    _perTargetPerRound) >= _perTargetLimit)
                                continue;
                            _perTargetOwner.NotePassiveFireAgainst(_perTargetIndex, target.Id);
                        }

                        ApplyEffect(caster, target, landed, ability, depth);
                        _lastTarget = target;
                        if (IsBattleOver()) { if (depth > 0) _lastTarget = prevLastTarget; return; }
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
                    // Track how many targets this effect resolved against, for the next effect's
                    // PerTarget. Only counts targets that passed the condition, so a conditional
                    // AoE that hit 2 of 5 enemies reports 2.
                    _lastEffectTargetCount = 0;
                    for (int tc = 0; tc < targets.Count; tc++)
                        if (targets[tc] != null) _lastEffectTargetCount++;

                    bool suppressed = effect.Condition != EffectCondition.None && !anyTargetPassed;

                    // Spent after the effect has finished with every target, so an AoE pays the
                    // stack once. Emitted as StatusExpired for the same reason Cleanse does: the
                    // status row has to stop showing a counter the unit no longer holds.
                    //
                    // Batch three: effect.Status picks which counter to spend (same logic as
                    // GainResolve). ConsumeStacks > 0 removes exactly that many instead of all.
                    if (!suppressed && effect.ConsumesResolve)
                    {
                        StatusKind resolveKind = effect.Status == StatusKind.Resolve2
                                                 ? StatusKind.Resolve2 : StatusKind.Resolve;
                        if (caster.CountOf(resolveKind) > 0)
                        {
                            if (effect.ConsumeStacks > 0)
                                caster.RemoveStacks(resolveKind, effect.ConsumeStacks);
                            else
                                caster.Remove(resolveKind);
                            BattleEvent spent = Emit(BattleEventType.StatusExpired, caster.Id, caster.Id);
                            spent.Status = resolveKind;
                        }
                    }
                }
                // At depth > 0 (passive-triggered abilities), restore _lastTarget so the outer
                // ability's chain is not clobbered. At depth 0 (basic / active), leave _lastTarget
                // set: AfterBasicAttack passives need to see the basic's last target via
                // ConditionSubject.LastTarget, and the caller restores it once it is done.
                if (depth > 0)
                    _lastTarget = prevLastTarget;
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
                        float amount = source.EffectiveAttack * effect.Power + effect.Flat
                                     + source.MaxHp * effect.SelfHpRatio + TargetHpTerm(effect, target);
                        ApplyHealValue(source.Id, target, (int)Math.Round(amount, MidpointRounding.AwayFromZero), "heal", true);
                        break;
                    }
                    case EffectKind.Shield:
                    {
                        if (!target.Alive) break;
                        float amount = source.EffectiveAttack * effect.Power + effect.Flat
                                     + source.MaxHp * effect.SelfHpRatio + TargetHpTerm(effect, target);
                        int value = (int)Math.Round(amount, MidpointRounding.AwayFromZero);
                        if (value <= 0) break;
                        target.Shield += value;
                        target.AddOrRefresh(StatusKind.Shield, Math.Max(1, effect.Duration), 0f, source.Id);
                        BattleEvent e = Emit(BattleEventType.ShieldGain, source.Id, target.Id);
                        e.Value = value;
                        e.Vfx = "buff";

                        // Charges, stamped as the ward lands, so a Shield can also be a
                        // charge-limited one. Zero on every shield in shipped content.
                        if (effect.Charges > 0)
                            for (int si = 0; si < target.Statuses.Count; si++)
                                if (target.Statuses[si].Kind == StatusKind.Shield)
                                    target.Statuses[si].Charges = effect.Charges;

                        {
                            CombatUnit prevSubject = _broadcastSubject;
                            _broadcastSubject = target;
                            BroadcastPassive(target.IsPlayer, PassiveTrigger.OnAllyShielded, depth, target);
                            _broadcastSubject = prevSubject;
                        }
                        break;
                    }
                    case EffectKind.ApplyStatus:
                    {
                        if (!target.Alive || effect.Status == StatusKind.None) break;
                        float landing = effect.BypassResist
                            ? effect.Chance
                            : ResolveLandingChance(effect.Chance, effect.Status,
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
                        // Buff Suppression: if the target carries BuffSuppression and the
                        // incoming status is beneficial, the application is silently prevented.
                        // Checked AFTER the landing roll succeeds so the RNG draw that tested
                        // landing still fires - no stream shift when BuffSuppression is absent.
                        if (!StatusInfo.IsHarmful(effect.Status) && target.Has(StatusKind.BuffSuppression))
                            break;

                        float magnitude = effect.Magnitude;
                        if (effect.Status == StatusKind.Burn || effect.Status == StatusKind.Regen)
                            magnitude = source.EffectiveAttack * effect.Magnitude;
                        int duration = Math.Max(1, effect.Duration);

                        // One rule for every source. Ember's Burn used to stack as its own entry
                        // while everybody else's folded into the one already there; a status now
                        // behaves the same way whoever applied it. AddStack is still here and still
                        // used - GainResolve stacks through it - but nothing reaches it because of
                        // an aspect any more, and the emberBurnStackCap that bounded it is gone.
                        target.AddOrRefresh(effect.Status, duration, magnitude, source.Id);

                        // A Ledger's cap belongs to the mark that is counting, so it is stamped on
                        // the entry as it lands rather than being looked up by whatever pays it
                        // out. Scaled off the applier's Attack, like every other authored
                        // magnitude, so a cap means the same thing at every level.
                        // Charges, stamped as the status lands. Zero for every status in shipped
                        // content, so no entry gains a use count nobody authored.
                        if (effect.Charges > 0)
                            for (int ci = 0; ci < target.Statuses.Count; ci++)
                            {
                                StatusInstance ch = target.Statuses[ci];
                                if (ch.Kind == effect.Status && ch.SourceId == source.Id)
                                    ch.Charges = effect.Charges;
                            }

                        // Per-round uses, stamped the same way and for the same reason. Zero on
                        // every status in shipped content, so no entry gains a per-round budget
                        // nobody authored - and a budget of zero means unlimited, which is what
                        // every existing status already had.
                        if (effect.UsesPerRound > 0)
                            for (int ui = 0; ui < target.Statuses.Count; ui++)
                            {
                                StatusInstance us = target.Statuses[ui];
                                if (us.Kind == effect.Status && us.SourceId == source.Id)
                                {
                                    us.UsesPerRound = effect.UsesPerRound;
                                    us.UsesThisRound = 0;
                                }
                            }

                        if (effect.Status == StatusKind.Ledger)
                            for (int li = 0; li < target.Statuses.Count; li++)
                            {
                                StatusInstance led = target.Statuses[li];
                                if (led.Kind != StatusKind.Ledger || led.SourceId != source.Id) continue;
                                if (effect.StoredCap > 0f)
                                {
                                    float basis = effect.StoredCapBasis == "maxHp"
                                        ? source.MaxHp : source.EffectiveAttack;
                                    led.StoredCap = basis * effect.StoredCap;
                                }
                                else led.StoredCap = 0f;
                                led.AutoRelease = effect.LedgerAutoRelease;
                                led.Mitigates = effect.LedgerMitigates;
                                led.BankLimit = effect.LedgerBankLimit;
                                led.BankHeals = effect.LedgerBankHeals;
                                led.BankOnlyFrom = effect.LedgerBankOnlyFrom;
                                led.BankFromCaster = effect.LedgerBankFromCaster;
                                led.AutoReleaseAsHeal = effect.LedgerAutoReleaseAsHeal;
                                led.AutoReleaseTarget = effect.LedgerAutoReleaseTarget;
                            }

                        // Disrupted: StoredCap carries the turn shift fraction, raw rather than
                        // Attack-scaled. Zero means no shift, so an effect that does not set it
                        // is a drain-only Disrupted.
                        if (effect.Status == StatusKind.Disrupted && effect.StoredCap > 0f)
                            for (int di = 0; di < target.Statuses.Count; di++)
                            {
                                StatusInstance ds = target.Statuses[di];
                                if (ds.Kind != StatusKind.Disrupted || ds.SourceId != source.Id) continue;
                                ds.StoredCap = effect.StoredCap;
                            }

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


                        // A buff landing is news to the other side. Raised after the status is on
                        // the unit, so a passive answering it sees the board it is answering; and
                        // handed `depth`, so a buff-gained passive that itself buffs somebody
                        // cascades no further than MaxPassiveDepth already allows.
                        if (!StatusInfo.IsHarmful(effect.Status))
                        {
                            CombatUnit prevSubject = _broadcastSubject;
                            _broadcastSubject = target;
                            StatusKind prevBuff = _broadcastBuff;
                            _broadcastBuff = effect.Status;
                            BroadcastPassive(!target.IsPlayer, PassiveTrigger.OnEnemyBuffGained, depth, null);
                            _broadcastBuff = prevBuff;
                            _broadcastSubject = prevSubject;
                        }
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
                        if (removed > 0) { _lastRemovedStacks = removed; TriggerPassive(source, PassiveTrigger.OnStatusRemoved, depth); }
                        break;
                    }
                    case EffectKind.Dispel:
                    {
                        if (!target.Alive) break;
                        int removed = 0;
                        for (int i = target.Statuses.Count - 1; i >= 0 && removed < effect.Hits; i--)
                        {
                            if (StatusInfo.IsHarmful(target.Statuses[i].Kind)) continue;
                            // DispelOwnOnly: only strip buffs this caster applied.
                            if (effect.DispelOwnOnly && target.Statuses[i].SourceId != source.Id) continue;
                            // Exclude the buff that triggered OnEnemyBuffGained, so "when an enemy
                            // gains a buff, dispel it" does not strip the very buff that raised the
                            // trigger before anything else can read it.
                            if (_broadcastBuff != StatusKind.None && target.Statuses[i].Kind == _broadcastBuff) continue;
                            BattleEvent e = Emit(BattleEventType.StatusExpired, source.Id, target.Id);
                            e.Status = target.Statuses[i].Kind;
                            // Same rule as the expiry path: a dispelled ward stops absorbing. A
                            // Dispel that stripped the tag and left the pool would be the same
                            // disagreement, reached by a different door.
                            if (target.Statuses[i].Kind == StatusKind.Shield) target.Shield = 0f;
                            target.Statuses.RemoveAt(i);
                            removed++;
                        }
                        if (removed > 0) { _lastRemovedStacks = removed; TriggerPassive(source, PassiveTrigger.OnStatusRemoved, depth); }
                        break;
                    }
                    case EffectKind.EnergyGain:
                    {
                        if (!target.Alive) break;
                        int amount = (int)Math.Round(effect.Flat + effect.Power * 100f, MidpointRounding.AwayFromZero);
                        if (amount == 0) break;
                        // The one granting site: source handed this out, so the granter is recorded
                        // on the event rather than the recipient being credited with its own gain.
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
                        //
                        // Batch three: effect.Status picks which counter to grant. None (the
                        // default and every shipped effect) maps to Resolve, preserving the
                        // existing behaviour. Resolve2 grants the second independent counter.
                        if (!target.Alive) break;
                        if (target.Has(StatusKind.StatusGainSuppression)) break;
                        StatusKind resolveKind = effect.Status == StatusKind.Resolve2
                                                 ? StatusKind.Resolve2 : StatusKind.Resolve;
                        int cap = effect.MaxStacks > 0 ? effect.MaxStacks : 1;
                        for (int g = 0; g < effect.Hits; g++)
                        {
                            if (!target.AddStack(resolveKind, Math.Max(1, effect.Duration),
                                                 effect.Magnitude, source.Id, cap))
                                break;   // already at cap; do not emit a second event for a no-op
                            BattleEvent re = Emit(BattleEventType.StatusApplied, source.Id, target.Id);
                            re.Status = resolveKind;
                            re.Duration = Math.Max(1, effect.Duration);
                            // Value carries stack depth here for the same reason Ember's Burn
                            // stacking uses it: StatusApplied already fires per application, it
                            // just never said how many there were.
                            re.Value = target.CountOf(resolveKind);
                        }
                        break;
                    }
                    case EffectKind.TurnShift:
                    {
                        if (!target.Alive) break;
                        // ShiftBanksAs is None for every shift in shipped content and for every
                        // shift authored before this pass, so the three-argument call below is the
                        // one-argument call it replaced.
                        ApplyTurnShift(source, target, effect.Magnitude,
                                       effect.ShiftBanksAs, effect.StoredCap, effect.Magnitude == 0f ? 0f : effect.Power,
                                       effect.Duration);
                        break;
                    }

                    case EffectKind.RecordFact:
                    {
                        if (!target.Alive) break;
                        RecordFact(source, target, effect);
                        break;
                    }

                    case EffectKind.SwapTurnOrder:
                    {
                        if (!target.Alive) break;
                        ApplySwapTurnOrder(source, target);
                        break;
                    }

                    case EffectKind.RemoveStatus:
                    {
                        // By NAME, where Cleanse and Dispel remove by class and recency. Walked
                        // backwards for the same reason those two are: removing shifts every later
                        // entry down one, and a forward walk steps over the next match.
                        if (!target.Alive || effect.Status == StatusKind.None) break;
                        int removed = 0;
                        for (int i = target.Statuses.Count - 1; i >= 0 && removed < effect.Hits; i--)
                        {
                            if (target.Statuses[i].Kind != effect.Status) continue;
                            // Umbra's uncleansable rule is deliberately NOT consulted here. That
                            // rule is about a Cleanse trying to shrug a mark off its own carrier;
                            // this is an attacker naming a status and stripping it, which is the
                            // opposite direction and a different question.
                            BattleEvent e = Emit(BattleEventType.StatusExpired, source.Id, target.Id);
                            e.Status = target.Statuses[i].Kind;
                            target.Statuses.RemoveAt(i);
                            removed++;
                        }
                        // Raised once per effect rather than once per entry, so a removal that
                        // takes three stacks answers the trigger once. Handed `depth`, so a
                        // removal passive that itself removes something is bounded by the same
                        // MaxPassiveDepth as any other passive calling a passive.
                        if (removed > 0) { _lastRemovedStacks = removed; TriggerPassive(source, PassiveTrigger.OnStatusRemoved, depth); }
                        break;
                    }

                    case EffectKind.MoveDebuffs:
                    case EffectKind.MoveBuffs:
                    {
                        // THE ONE EFFECT THAT MAY READ A CORPSE. Every other kind refuses a dead
                        // target because acting on one is meaningless; taking a status OFF one is
                        // the entire point of the defeat window, and that window is the only place
                        // a dead unit still has statuses to take. Outside it _defeated is null and
                        // this is the Alive gate it has always been.
                        if (!target.Alive && target != _defeated) break;
                        bool wantHarmful = effect.Kind == EffectKind.MoveDebuffs;

                        List<CombatUnit> destination = ResolveTargets(source, effect.SecondaryTarget, target,
                                                                      effect.TargetStatus);
                        // `target` is passed as the exclusion, so a move can never resolve onto the
                        // unit it is taking from - that would be a no-op wearing the costume of an
                        // effect, and it would still cost the event.
                        if (destination.Count == 0) break;
                        CombatUnit to = destination[0];
                        if (to == null || !to.Alive) break;

                        int moved = 0;
                        for (int i = target.Statuses.Count - 1; i >= 0 && moved < effect.Hits; i--)
                        {
                            StatusInstance entry = target.Statuses[i];
                            if (StatusInfo.IsHarmful(entry.Kind) != wantHarmful) continue;
                            // Shield is skipped by construction rather than by a special case: the
                            // absorbing pool is a float on the unit and does not travel with the
                            // status row, so moving the entry alone would hand the recipient a
                            // marker with nothing behind it.
                            if (entry.Kind == StatusKind.Shield) continue;
                            MoveStatus(target, to, entry, true);
                            moved++;
                        }
                        break;
                    }

                    case EffectKind.InvertStatus:
                    {
                        if (!target.Alive) break;
                        int flipped = 0;
                        for (int i = target.Statuses.Count - 1; i >= 0 && flipped < effect.Hits; i--)
                        {
                            StatusInstance entry = target.Statuses[i];
                            if (StatusInfo.IsHarmful(entry.Kind)) continue;
                            StatusKind opposite = StatusInfo.Opposite(entry.Kind);
                            // A buff with no pairing is left alone rather than removed, so an
                            // inversion can never double as a stealth Dispel.
                            if (opposite == StatusKind.None) continue;

                            target.Statuses.RemoveAt(i);
                            BattleEvent gone = Emit(BattleEventType.StatusExpired, source.Id, target.Id);
                            gone.Status = entry.Kind;

                            target.AddOrRefresh(opposite, entry.RoundsLeft, entry.Magnitude, source.Id);
                            BattleEvent e = Emit(BattleEventType.StatusApplied, source.Id, target.Id);
                            e.Status = opposite;
                            e.Duration = entry.RoundsLeft;
                            e.Value = target.CountOf(opposite);
                            flipped++;
                        }
                        break;
                    }

                    case EffectKind.LedgerRelease:
                    {
                        if (!target.Alive) break;
                        float stored = target.LedgerTotalFrom(source.Id);
                        _lastLedgerRelease = stored;
                        // Zeroed whether or not it paid out, so a release against an empty ledger
                        // is spent rather than saved up - the descriptions all read "consume".
                        for (int li = 0; li < target.Statuses.Count; li++)
                        {
                            StatusInstance led = target.Statuses[li];
                            if (led.Kind == StatusKind.Ledger && led.SourceId == source.Id) led.Stored = 0f;
                        }
                        if (stored <= 0f) break;

                        // ReleaseFraction: pay out only this fraction of the stored amount.
                        float fraction = effect.ReleaseFraction;
                        if (fraction <= 0f) fraction = 1f;
                        if (fraction > 1f) fraction = 1f;
                        int payout = (int)Math.Round(stored * effect.Power * fraction, MidpointRounding.AwayFromZero);
                        if (payout <= 0) break;
                        if (effect.ReleaseAsHeal)
                        {
                            // Heal-shaped release. Routes to SecondaryTarget when it is not Self,
                            // so "release my ledger on this enemy as healing for an ally" is one
                            // effect rather than two that disagree about a number.
                            CombatUnit healTarget = target;
                            if (effect.SecondaryTarget != TargetMode.Self)
                            {
                                List<CombatUnit> healTargets = ResolveTargets(source, effect.SecondaryTarget);
                                if (healTargets.Count > 0) healTarget = healTargets[0];
                            }
                            ApplyHealValue(source.Id, healTarget, payout, "heal", true);
                        }
                        else
                        {
                            // Through the same funnel as every other point of damage, so a release that
                            // kills fires the death and the killer's OnKill exactly as any hit would -
                            // and banks into anybody else's ledger on the way.
                            DealRawDamage(source.Id, target, payout, false, "burst", StatusKind.None, 1f,
                                          ability != null ? ability.Name : "");
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

                    case EffectKind.Interrupt:
                    {
                        // Resets the target's energy to 0, stopping an active mid-charge.
                        // Distinct from energy drain: this is an all-or-nothing reset, not a
                        // subtraction, and the event is typed so the UI can show the interruption.
                        // No RNG draw: energy is deterministic state, not a roll.
                        if (!target.Alive) break;
                        if (target.Energy <= 0) break;
                        target.Energy = 0;
                        EmitEnergy(target);
                        break;
                    }

                    case EffectKind.ExtendStatus:
                    {
                        // Adds effect.Duration rounds to every matching status on the target.
                        // Negative values shorten rather than extend (duration reduction).
                        // When effect.Status is None, extends ALL harmful statuses (the Demos/
                        // Celen shape: "extend all debuffs by N rounds"). When effect.Status
                        // names a specific kind, extends only that kind. No RNG draw: duration
                        // is deterministic state. Does not apply a new status — a target with
                        // nothing to extend sees nothing happen.
                        if (!target.Alive) break;
                        int added = effect.Duration;
                        bool namedKind = effect.Status != StatusKind.None;
                        int extended = 0;
                        for (int ei = 0; ei < target.Statuses.Count; ei++)
                        {
                            StatusInstance entry = target.Statuses[ei];
                            if (namedKind)
                            {
                                if (entry.Kind != effect.Status) continue;
                            }
                            else
                            {
                                if (!StatusInfo.IsHarmful(entry.Kind)) continue;
                            }
                            entry.RoundsLeft += added;
                            extended++;
                        }
                        if (extended > 0)
                        {
                            BattleEvent ext = Emit(BattleEventType.StatusApplied, source.Id, target.Id);
                            ext.Status = namedKind ? effect.Status : StatusKind.None;
                            ext.Duration = added;
                            ext.Value = extended;
                        }
                        break;
                    }

                    case EffectKind.ConsumeTurnDebt:
                    {
                        // Consumes all TurnDebt on the target, delays it, and grants the energy
                        // the debt would have cost to the SecondaryTarget.
                        //
                        // The three pieces are one effect because the energy amount is derived
                        // from the TurnDebt being consumed: it ceases to exist once removed, so
                        // calculating first and removing second is the only order that works.
                        //
                        // Unreachable in shipped content: nothing applies TurnDebt.
                        if (!target.Alive) break;
                        if (!target.Has(StatusKind.TurnDebt)) break;

                        // Calculate repayment cost, then remove all TurnDebt entries
                        float owed = 0f;
                        float rate = 0f;
                        for (int td = target.Statuses.Count - 1; td >= 0; td--)
                        {
                            StatusInstance d = target.Statuses[td];
                            if (d.Kind != StatusKind.TurnDebt) continue;
                            owed += d.Stored;
                            if (d.Magnitude > rate) rate = d.Magnitude;
                            target.Statuses.RemoveAt(td);
                            BattleEvent gone = Emit(BattleEventType.StatusExpired, d.SourceId, target.Id);
                            gone.Status = StatusKind.TurnDebt;
                        }
                        int lostEnergy = owed > 0f && rate > 0f
                            ? (int)Math.Round(owed * rate, MidpointRounding.AwayFromZero) : 0;

                        // Delay the target
                        if (effect.Magnitude != 0f)
                            ApplyTurnShift(source, target, -Math.Abs(effect.Magnitude));

                        // Redistribute the calculated energy to the secondary target
                        if (lostEnergy > 0)
                        {
                            List<CombatUnit> secondaries = ResolveTargets(source, effect.SecondaryTarget);
                            for (int si = 0; si < secondaries.Count; si++)
                            {
                                if (secondaries[si].Alive)
                                {
                                    AddEnergy(secondaries[si], lostEnergy, source.Id);
                                    break;
                                }
                            }
                        }
                        break;
                    }

                    // ---------- tier eight: transfers, ledger increment, lethal redirect ----------

                    case EffectKind.HpTransfer:
                    {
                        // Moves HP from one unit to the other. Self-damage, not through DealRawDamage,
                        // so Guard/Cover/Shield/Ledger/death cascade are not involved and the
                        // donor cannot die from it (clamped to Hp - 1).
                        // TransferFromTarget: true = target is donor, false (default) = caster is donor.
                        if (!source.Alive || !target.Alive) break;
                        CombatUnit donor = effect.TransferFromTarget ? target : source;
                        CombatUnit recipient = effect.TransferFromTarget ? source : target;
                        int amount = (int)Math.Round(
                            source.EffectiveAttack * effect.Power + effect.Flat
                            + TargetHpTerm(effect, target),
                            MidpointRounding.AwayFromZero);
                        if (amount <= 0) break;
                        int available = (int)Math.Max(0f, donor.Hp - 1f);
                        if (amount > available) amount = available;
                        if (amount <= 0) break;
                        donor.Hp -= amount;
                        BattleEvent dmg = Emit(BattleEventType.Damage, source.Id, donor.Id);
                        dmg.Value = amount;
                        dmg.Vfx = "transfer";
                        ApplyHealValue(source.Id, recipient, amount, "heal", true);
                        break;
                    }

                    case EffectKind.ShieldTransfer:
                    {
                        // Moves Magnitude fraction of a unit's Shield pool to the other.
                        // TransferFromTarget: true = target is donor, false (default) = caster is donor.
                        if (!source.Alive || !target.Alive) break;
                        CombatUnit donor = effect.TransferFromTarget ? target : source;
                        CombatUnit recipient = effect.TransferFromTarget ? source : target;
                        float pool = donor.Shield;
                        if (pool <= 0f) break;
                        float frac = effect.Magnitude > 0f ? effect.Magnitude : 1f;
                        if (frac > 1f) frac = 1f;
                        float moved = pool * frac;
                        if (moved < 0.5f) break;
                        donor.Shield -= moved;
                        if (donor.Shield < 0.5f)
                        {
                            donor.Shield = 0f;
                            donor.Remove(StatusKind.Shield);
                            BattleEvent gone = Emit(BattleEventType.StatusExpired, donor.Id, donor.Id);
                            gone.Status = StatusKind.Shield;
                        }
                        recipient.Shield += moved;
                        if (!recipient.Has(StatusKind.Shield))
                            recipient.AddOrRefresh(StatusKind.Shield, 99, 0f, source.Id);
                        BattleEvent se = Emit(BattleEventType.ShieldGain, source.Id, recipient.Id);
                        se.Value = (int)Math.Round(moved, MidpointRounding.AwayFromZero);
                        break;
                    }

                    case EffectKind.LedgerIncrement:
                    {
                        // Adds Power * Attack directly to the caster's Ledger Stored on the target.
                        if (!target.Alive) break;
                        float add = source.EffectiveAttack * effect.Power + effect.Flat;
                        if (add <= 0f) break;
                        for (int li = 0; li < target.Statuses.Count; li++)
                        {
                            StatusInstance led = target.Statuses[li];
                            if (led.Kind != StatusKind.Ledger || led.SourceId != source.Id) continue;
                            float room = led.StoredCap > 0f ? led.StoredCap - led.Stored : float.MaxValue;
                            if (room <= 0f) continue;
                            float banked = add > room ? room : add;
                            bool wasBelow = led.StoredCap > 0f && led.Stored < led.StoredCap;
                            led.Stored += banked;
                            if (wasBelow && led.StoredCap > 0f && led.Stored >= led.StoredCap)
                            {
                                CombatUnit prevSubject = _broadcastSubject;
                                _broadcastSubject = target;
                                BroadcastPassive(source.IsPlayer, PassiveTrigger.OnLedgerCapped, 0, null);
                                _broadcastSubject = prevSubject;
                            }
                        }
                        break;
                    }

                    case EffectKind.RedirectLethal:
                    {
                        // Only meaningful inside an OnAllyWouldDie broadcast, where target.Hp <= 0
                        // but target.Alive is still true. Saves the target at 1 HP and deals the
                        // overkill to the source (passive owner).
                        if (!target.Alive || target.Hp > 0f) break;
                        float overkill = -target.Hp;
                        _lastRedirectAmount = overkill;
                        target.Hp = 1f;
                        BattleEvent saveEvt = Emit(BattleEventType.Heal, source.Id, target.Id);
                        saveEvt.Value = 1 + (int)Math.Round(overkill, MidpointRounding.AwayFromZero);
                        saveEvt.Vfx = "redirect";
                        int redirected = (int)Math.Round(overkill * effect.Power,
                                                         MidpointRounding.AwayFromZero);
                        if (redirected > 0 && source.Alive)
                            DealRawDamage(-1, source, redirected, false, "redirect",
                                          StatusKind.None, 1f,
                                          ability != null ? ability.Name : "");
                        break;
                    }

                    // ---------- tier nine: parameterisation gaps ----------

                    case EffectKind.SnapshotEnergy:
                    {
                        // Records the target's current energy as the stack count of a status.
                        if (!target.Alive || effect.Status == StatusKind.None) break;
                        int stacks = Math.Max(0, Math.Min(99, target.Energy));
                        // Remove existing entries and re-add with the right count.
                        target.Remove(effect.Status);
                        for (int si = 0; si < stacks; si++)
                            target.AddStack(effect.Status, Math.Max(1, effect.Duration), 0f, source.Id, stacks);
                        if (stacks > 0)
                        {
                            BattleEvent e = Emit(BattleEventType.StatusApplied, source.Id, target.Id);
                            e.Status = effect.Status;
                            e.Duration = Math.Max(1, effect.Duration);
                            e.Value = stacks;
                        }
                        break;
                    }

                    case EffectKind.SnapshotHp:
                    {
                        if (!target.Alive) break;
                        target.SnapshotHp = (int)Math.Round(target.Hp, MidpointRounding.AwayFromZero);
                        break;
                    }

                    case EffectKind.TransferStatus:
                    {
                        // Moves the first matching status entry from the target to SecondaryTarget.
                        if (!target.Alive || effect.Status == StatusKind.None) break;
                        List<CombatUnit> dests = ResolveTargets(source, effect.SecondaryTarget);
                        if (dests.Count == 0) break;
                        CombatUnit dest = dests[0];
                        if (!dest.Alive || dest == target) break;
                        StatusInstance moving = null;
                        int movingIdx = -1;
                        for (int i = 0; i < target.Statuses.Count; i++)
                        {
                            if (target.Statuses[i].Kind != effect.Status) continue;
                            moving = target.Statuses[i];
                            movingIdx = i;
                            break;
                        }
                        if (moving == null) break;
                        target.Statuses.RemoveAt(movingIdx);
                        BattleEvent gone = Emit(BattleEventType.StatusExpired, source.Id, target.Id);
                        gone.Status = moving.Kind;
                        if (moving.Kind == StatusKind.Shield) target.Shield = 0f;
                        dest.Statuses.Add(moving);
                        BattleEvent applied = Emit(BattleEventType.StatusApplied, source.Id, dest.Id);
                        applied.Status = moving.Kind;
                        applied.Duration = moving.RoundsLeft;
                        applied.Value = dest.CountOf(moving.Kind);
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
                // -1. Accuracy / Evasion - a miss check before anything else. EvasionUp on the
                // defender and AccuracyDown on the attacker both contribute to the miss chance.
                // Both default to 0, so hitChance is 1.0f and Chance(1f) returns true on line 41
                // of DeterministicRng without calling NextFloat — zero RNG draws consumed when
                // neither status exists, which is every battle until content authors one.
                float hitChance = 1f - target.SumMagnitude(StatusKind.EvasionUp)
                                     - source.SumMagnitude(StatusKind.AccuracyDown);
                if (!_rng.Chance(hitChance))
                {
                    TriggerPassive(target, PassiveTrigger.OnDodge, depth);
                    return;
                }

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

                // 1. Base. The target-HP term joins here, before mitigation, so "12% of its
                // missing HP" is reduced by Defense like any other authored damage rather than
                // arriving as a second, unmitigated payload.
                float attackTerm = source.EffectiveAttack;
                if (effect.ScaleByLastRelease) attackTerm = _lastLedgerRelease;
                else if (effect.ScaleByLastRedirect) attackTerm = _lastRedirectAmount;
                else if (effect.ScaleByLastRemoval) attackTerm = _lastRemovedStacks;
                float raw = attackTerm * effect.Power + effect.Flat + TargetHpTerm(effect, target);
                raw *= OutputModMultiplier(source, target, OutputModScope.AllDamage);
                float holyBonus = source.EffectiveHolyDamage;

                // 2. Aspect factor
                float aspectFactor = AspectChart.Multiplier(source.Aspect, target.Aspect);
                raw *= aspectFactor;

                // 3. Crit — GuaranteedCrit forces the outcome and skips the RNG draw, so no
                // existing battle can be shifted by a draw that never fires. Consumed on use:
                // one instance is removed and its StatusExpired emitted.
                bool critical;
                if (source.Has(StatusKind.GuaranteedCrit))
                {
                    critical = true;
                    for (int gc = source.Statuses.Count - 1; gc >= 0; gc--)
                    {
                        if (source.Statuses[gc].Kind == StatusKind.GuaranteedCrit)
                        {
                            source.Statuses.RemoveAt(gc);
                            BattleEvent gcEvt = Emit(BattleEventType.StatusExpired, source.Id, source.Id);
                            gcEvt.Status = StatusKind.GuaranteedCrit;
                            break;
                        }
                    }
                }
                else
                {
                    critical = _rng.Chance(source.EffectiveCritChance);
                }
                // EffectiveCritDamage, not the raw field: Crit Damage was the one stat in this
                // pipeline still read straight off the unit, which is why no status could move it.
                // Exactly equal to the raw field until content authors a Crit Damage status.
                if (critical) raw *= source.EffectiveCritDamage;

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

                // 7.5. Vulnerable - SOURCE-KEYED, which is what makes it different from
                // DefenseDown. DefenseDown changes the input to the mitigation ratio and softens
                // the target against the whole field; this reads only the entries THIS attacker
                // applied, so "takes 12% more damage from Nadine" is 12% more from Nadine and
                // normal from everybody else. Exactly zero when the attacker has applied none,
                // and x + 0 is exact, so it cannot move a battle nobody authored it into.
                float vulnerable = target.VulnerableTo(source.Id);
                if (vulnerable != 0f) raw *= 1f + vulnerable;

                // 8. Variance, round, clamp
                raw *= 1f + _rng.Spread(_cfg.DamageVariance);

                int damage = (int)Math.Round(raw, MidpointRounding.AwayFromZero);
                if (damage < _cfg.MinDamage) damage = (int)_cfg.MinDamage;

                // PreviousTargetId first, and only when the target actually changes: an ability
                // that hits the same body twice in a row must not shuffle its own history and
                // report itself as having switched.
                if (source.LastTargetId != target.Id) source.PreviousTargetId = source.LastTargetId;
                source.LastTargetId = target.Id;
                string vfx = ability != null ? ability.Vfx : "impact";
                string abilityName = ability != null ? ability.Name : "";
                DealRawDamage(source.Id, target, damage, critical, vfx, StatusKind.None, aspectFactor, abilityName);

                if (critical && source.Alive) TriggerPassive(source, PassiveTrigger.OnCriticalHit, depth);
            }

            /// <summary>
            /// The part of an amount that scales off the TARGET's health rather than the caster's.
            ///
            /// Exactly zero for every effect that sets none of the three fields, which is all of
            /// shipped content - three multiplications by 0f summed, which is 0f, and adding 0f to
            /// a float is exact. That is what keeps this out of every existing battle.
            /// </summary>
            /// <summary>Resolves ConditionSubject.LastAttacker: the unit that last attacked the
            /// broadcast subject, or null outside a broadcast / when nobody attacked it.</summary>
            private CombatUnit ResolveLastAttacker()
            {
                if (_broadcastSubject == null || _broadcastSubject.LastAttackerId < 0) return null;
                return FindUnit(_broadcastSubject.LastAttackerId);
            }

            /// <summary>
            /// Per-stack output modifier multiplier. Returns 1f when the source has no modifier
            /// or when the scope/mark filter excludes this context.
            /// </summary>
            private static float OutputModMultiplier(CombatUnit source, CombatUnit target, OutputModScope context)
            {
                if (source == null || source.OutputModStatus == StatusKind.None || source.OutputModPerStack == 0f)
                    return 1f;
                if (source.OutputModScope != context && source.OutputModScope != OutputModScope.AllDamage)
                    return 1f;
                if (context == OutputModScope.HealingOnly && source.OutputModScope != OutputModScope.HealingOnly)
                    return 1f;
                if (source.OutputModMarkedOnly && target != null
                    && !target.HasMarkFrom(StatusKind.Mark, source.Id)
                    && !target.HasMarkFrom(StatusKind.MarkAlt, source.Id))
                    return 1f;
                int stacks = source.CountOf(source.OutputModStatus);
                if (stacks <= 0) return 1f;
                return 1f + source.OutputModPerStack * stacks;
            }

            private static float TargetHpTerm(EffectDef effect, CombatUnit target)
            {
                if (target == null) return 0f;
                float maxHp = target.EffectiveMaxHp;
                float missingTerm = (maxHp - target.Hp) * effect.TargetMissingHpRatio;
                if (effect.MissingHpCap > 0f)
                {
                    float cap = maxHp * effect.MissingHpCap;
                    if (missingTerm > cap) missingTerm = cap;
                }
                float maxHpTerm = maxHp * effect.TargetMaxHpRatio;
                if (effect.MaxHpCap > 0f)
                {
                    float cap = maxHp * effect.MaxHpCap;
                    if (maxHpTerm > cap) maxHpTerm = cap;
                }
                return maxHpTerm
                     + target.Hp * effect.TargetCurrentHpRatio
                     + missingTerm;
            }

            /// <summary>Single funnel for every point of damage in the game.</summary>
            private void DealRawDamage(int sourceId, CombatUnit target, int damage, bool critical, string vfx, StatusKind dotKind,
                                        float aspectFactor, string abilityName)
            {
                if (!target.Alive || damage <= 0) return;

                // Guard: a charge-based reduction, spent by USE rather than by time. Applied before
                // the shield pool, so a guarded ward meets a smaller hit. Walked backwards because
                // a spent charge removes its entry.
                for (int gi = target.Statuses.Count - 1; gi >= 0; gi--)
                {
                    StatusInstance guard = target.Statuses[gi];
                    if (guard.Kind != StatusKind.Guard || guard.Charges <= 0 || guard.Magnitude <= 0f) continue;
                    float cut = guard.Magnitude > 1f ? 1f : guard.Magnitude;
                    damage -= (int)Math.Round(damage * cut, MidpointRounding.AwayFromZero);
                    guard.Charges--;
                    if (guard.Charges <= 0)
                    {
                        target.Statuses.RemoveAt(gi);
                        BattleEvent spent = Emit(BattleEventType.StatusExpired, target.Id, target.Id);
                        spent.Status = StatusKind.Guard;
                    }
                    if (damage <= 0) return;
                    break;   // one guard a hit, so two cannot stack into immunity
                }

                // Cover: a share of the hit is taken by the protector instead. Split BEFORE the
                // holder's own shield pool, so the protector soaks its part and the ward underneath
                // meets only the remainder - which is what "25% of damage they take is redirected
                // to Rovan before Shields are checked" says.
                //
                // Bounded by _inCover: the redirected part goes through this same funnel, so a
                // protector who is itself Covered would otherwise pass its share on again. One hop,
                // the same rule Mirrored Wound holds to.
                if (!_inCover)
                {
                    int protectorId;
                    float share = target.CoverFraction(out protectorId);
                    CombatUnit protector = share > 0f ? FindUnit(protectorId) : null;
                    if (protector != null && protector.Alive && protector != target)
                    {
                        if (share > 1f) share = 1f;
                        int moved = (int)Math.Round(damage * share, MidpointRounding.AwayFromZero);
                        if (moved > 0)
                        {
                            damage -= moved;
                            _inCover = true;
                            DealRawDamage(sourceId, protector, moved, false, vfx, dotKind, aspectFactor, abilityName);
                            _inCover = false;
                            if (damage <= 0) return;
                            if (!target.Alive) return;
                        }
                    }
                }

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
                            int prevBreaker = _shieldBreakerId;
                            CombatUnit prevVictimSB = _broadcastVictim;
                            _shieldBreakerId = sourceId;
                            _broadcastVictim = target;
                            TriggerPassive(target, PassiveTrigger.OnShieldBreak, 0);
                            _shieldBreakerId = prevBreaker;
                            _broadcastVictim = prevVictimSB;
                            if (!target.Alive || IsBattleOver()) return;
                        }
                    }
                }

                if (damage <= 0) return;

                // Ledger accrual and mitigation, at the one funnel every point of damage in the
                // game passes through. Each Ledger entry on the victim either BANKS (the original
                // behaviour) or MITIGATES (tier eight), depending on the Mitigates flag copied from
                // the effect that applied it. Walks the live list without mutating structure - both
                // paths change a float on an entry and never add or remove one - so it needs none of
                // the snapshot care TickDamageOverTime documents.
                for (int li = 0; li < target.Statuses.Count; li++)
                {
                    StatusInstance led = target.Statuses[li];
                    if (led.Kind != StatusKind.Ledger) continue;

                    if (led.Mitigates)
                    {
                        // Mitigation: spend Stored to absorb incoming damage, the same direction a
                        // Shield pool works but on the Ledger rather than on the unit.
                        if (led.Stored > 0f && damage > 0)
                        {
                            float absorbed = Math.Min(led.Stored, (float)damage);
                            led.Stored -= absorbed;
                            damage -= (int)Math.Round(absorbed, MidpointRounding.AwayFromZero);
                            if (damage <= 0) return;
                        }
                    }
                    else
                    {
                        // Banking: the original behaviour. Banks Magnitude fraction of the hit.
                        // Entries with BankHeals are skipped here; they bank in ApplyHealValue.
                        if (led.BankHeals) continue;
                        if (led.Magnitude <= 0f) continue;
                        if (led.BankLimit > 0 && led.BankUsesThisRound >= led.BankLimit) continue;
                        // BankOnlyFrom: skip unless the damage source matches.
                        if (led.BankOnlyFrom != StatusKind.None && dotKind != led.BankOnlyFrom) continue;
                        // BankFromCaster: only bank damage dealt by the same unit that applied this entry.
                        if (led.BankFromCaster && sourceId != led.SourceId) continue;
                        float room = led.StoredCap > 0f ? led.StoredCap - led.Stored : float.MaxValue;
                        if (room <= 0f) continue;
                        float banked = damage * led.Magnitude;
                        if (banked > room) banked = room;
                        bool wasBelow = led.StoredCap > 0f && led.Stored < led.StoredCap;
                        led.Stored += banked;
                        if (led.BankLimit > 0) led.BankUsesThisRound++;
                        // OnLedgerCapped: fire once the moment Stored reaches StoredCap.
                        if (wasBelow && led.StoredCap > 0f && led.Stored >= led.StoredCap)
                        {
                            CombatUnit ledSource = FindUnit(led.SourceId);
                            if (ledSource != null)
                            {
                                CombatUnit prevSubject = _broadcastSubject;
                                _broadcastSubject = target;
                                BroadcastPassive(ledSource.IsPlayer, PassiveTrigger.OnLedgerCapped, 0, null);
                                _broadcastSubject = prevSubject;
                            }
                        }
                    }
                }

                target.Hp -= damage;
                BattleEvent e = Emit(BattleEventType.Damage, sourceId, target.Id);
                e.Value = damage;
                e.Critical = critical;
                e.Vfx = vfx;
                e.Status = dotKind;
                e.AspectFactor = aspectFactor;
                e.AbilityName = abilityName;

                if (sourceId >= 0)
                {
                    target.LastAttackerId = sourceId;
                    CombatUnit attacker = FindUnit(sourceId);
                    if (attacker != null)
                    {
                        attacker.DamageDealtThisRound += damage;
                        if (dotKind == StatusKind.Burn) attacker.DoTDamageDealtThisRound += damage;
                    }
                }

                target.LastDamagedRound = _round;
                if (sourceId >= 0)
                {
                    CombatUnit dealer = FindUnit(sourceId);
                    if (dealer != null) dealer.DamagedThisRound.Add(target.Id);
                }

                if (target.Hp <= 0f)
                {
                    // THE ONE INTERCEPTING TRIGGER. Raised while the unit is still flagged alive
                    // and its HP is at or below zero, then the condition is re-checked: a passive
                    // that healed it above zero has saved it, and one that did nothing leaves the
                    // death to resolve exactly as it did before.
                    //
                    // No new effect kind was needed for the save. ApplyHealValue refuses a dead
                    // target, and this unit is not dead yet - Alive is still true - so an ordinary
                    // Heal lands and lifts HP back over the line.
                    //
                    // _inDeathCheck bounds it to one attempt. Without it a passive that kills
                    // somebody else while answering this could re-enter here for a second unit and
                    // unwind in an order nothing reasons about.
                    if (!_inDeathCheck)
                    {
                        _inDeathCheck = true;
                        CombatUnit prevSubject = _broadcastSubject;
                        CombatUnit prevVictim = _broadcastVictim;
                        _broadcastSubject = target;
                        _broadcastVictim = target;
                        BroadcastPassive(target.IsPlayer, PassiveTrigger.OnAllyWouldDie, 0, target);
                        _broadcastSubject = prevSubject;
                        _broadcastVictim = prevVictim;
                        _inDeathCheck = false;
                    }

                    // Re-read, because the broadcast above may have healed it. With nothing
                    // listening - which is all of shipped content - Hp is untouched and this is the
                    // same test that guarded the block before.
                    if (target.Hp > 0f)
                    {
                        TriggerPassive(target, PassiveTrigger.OnHpBelowThreshold, 0);
                        return;
                    }

                    target.Hp = 0f;
                    target.Alive = false;
                    target.Shield = 0f;
                    Emit(BattleEventType.Death, sourceId, target.Id);

                    // THE PRE-CLEAR WINDOW, and the reason this pass is three times the work the
                    // same feature was before the aspect rules came out.
                    //
                    // Umbra's mark-jump used to live here: the death branch snapshotted the dying
                    // unit's harmful statuses before emptying the list, moved the Umbra-sourced
                    // ones, and cleared. Removing the rule took the snapshot AND the dispatch point
                    // with it, so `Statuses.Clear()` ran with nothing having read the list. Six
                    // abilities in the incoming roster want a status to survive its carrier's
                    // death, so both have to come back - and not in the shape they left in.
                    //
                    // What changed, and why the old shape would not have served: the old rule moved
                    // marks to the highest-ATTACK unit on the dead unit's own side, and every one
                    // of the six wants the highest-HP or the lowest-HP enemy instead. None of the
                    // six is Umbra. So the selector could not be reused either; what comes back is
                    // a dispatch point and a way to name the corpse, with the choice of heir left
                    // to content.
                    //
                    // The list is cleared AFTER the broadcast rather than before it. Nothing else
                    // in the engine reads a dead unit's statuses - every accessor that could is
                    // gated on Alive - so the window is observable only to a passive that asked to
                    // be in it. _defeated is what makes the corpse nameable while it lasts, and is
                    // nulled on the way out so TargetMode.DefeatedUnit resolves to nobody the
                    // moment the window closes.
                    //
                    // Re-entrant deaths are bounded by _inDefeatDispatch rather than by depth: a
                    // passive answering one death can kill somebody else, and the second corpse
                    // would otherwise overwrite _defeated while the first broadcast is still
                    // walking units. The inner death still resolves - it emits, it clears, it
                    // fires OnKill - it simply does not raise a second defeat broadcast.
                    if (!_inDefeatDispatch)
                    {
                        _inDefeatDispatch = true;
                        CombatUnit previous = _defeated;
                        _defeated = target;
                        CombatUnit prevSubject = _broadcastSubject;
                        CombatUnit prevVictim = _broadcastVictim;
                        _broadcastSubject = target;
                        _broadcastVictim = target;

                        // The dead unit's OWN side hears OnAllyDefeated; the other side hears
                        // OnEnemyDefeated. Split rather than one trigger with a condition, because
                        // the two cases in the roster are opposite and a passive that had to test
                        // which one it got would be answering a question content already knows.
                        BroadcastPassive(target.IsPlayer, PassiveTrigger.OnAllyDefeated, 0, target);
                        BroadcastPassive(!target.IsPlayer, PassiveTrigger.OnEnemyDefeated, 0, null);

                        _broadcastSubject = prevSubject;
                        _broadcastVictim = prevVictim;
                        _defeated = previous;
                        _inDefeatDispatch = false;
                    }

                    target.Statuses.Clear();

                    CombatUnit killer = FindUnit(sourceId);
                    if (killer != null && killer.Alive) TriggerPassive(killer, PassiveTrigger.OnKill, 0);
                    return;
                }

                // -1: energy from being hit is the victim's own regen, not something the attacker
                // granted. A Tide attacker must not redirect its victim's line's energy.
                AddEnergy(target, _cfg.EnergyOnDamaged, -1);
                TriggerPassive(target, PassiveTrigger.OnDamaged, 0);

                // The victim's own allies hear about it; the victim does not, because OnDamaged
                // one line up is already its own version of this news. Raised only for damage that
                // came from somewhere - a Burn tick whose source is long dead still counts, a
                // sourceless one does not.
                if (sourceId >= 0)
                {
                    CombatUnit prevSubject = _broadcastSubject;
                    CombatUnit prevVictim = _broadcastVictim;
                    _broadcastSubject = target;
                    _broadcastVictim = target;
                    BroadcastPassive(target.IsPlayer, PassiveTrigger.OnEnemyDamagedAlly, 0, target);
                    _broadcastSubject = prevSubject;
                    _broadcastVictim = prevVictim;
                }

                // And separately, that an ally has dropped low. A distinct question from being hit
                // at all - a chip hit on a full bar is not what "when an ally falls below 35%" is
                // asking about - so it is its own trigger rather than a condition on that one. The
                // watcher's OWN Threshold decides how low counts, read where every other threshold
                // passive reads it.
                {
                    CombatUnit prevSubject = _broadcastSubject;
                    CombatUnit prevVictim = _broadcastVictim;
                    _broadcastSubject = target;
                    _broadcastVictim = target;
                    BroadcastPassive(target.IsPlayer, PassiveTrigger.OnAllyHpBelowThreshold, 0, target);
                    _broadcastSubject = prevSubject;
                    _broadcastVictim = prevVictim;
                }

                // Mirrored from the victim's side to the attacker's: that an enemy dropped below
                // threshold. Same trigger shape, other audience.
                {
                    CombatUnit prevSubject = _broadcastSubject;
                    CombatUnit prevVictim = _broadcastVictim;
                    _broadcastSubject = target;
                    _broadcastVictim = target;
                    BroadcastPassive(!target.IsPlayer, PassiveTrigger.OnEnemyHpBelowThreshold, 0, null);
                    _broadcastSubject = prevSubject;
                    _broadcastVictim = prevVictim;
                }

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
            /// `allowBank` outlived the Verdant overheal bank it was named for and is kept because
            /// it still does the other half of its old job: it is false for a Regen tick, and that
            /// is what stops a heal-over-time from re-raising its caster's OnHealApplied and its
            /// allies' OnAllyHealed once a round for the rest of the battle. Renaming it is a
            /// separate, cosmetic change and is not made here.
            /// </summary>
            private void ApplyHealValue(int sourceId, CombatUnit target, int amount, string vfx, bool allowBank)
            {
                if (!target.Alive || amount <= 0) return;

                if (target.Has(StatusKind.HealSuppression)) return;

                CombatUnit healerUnit = FindUnit(sourceId);
                float healMod = OutputModMultiplier(healerUnit, target, OutputModScope.HealingOnly);
                if (healMod != 1f) amount = (int)Math.Round(amount * healMod, MidpointRounding.AwayFromZero);
                if (amount <= 0) return;

                float missing = target.EffectiveMaxHp - target.Hp;
                int healed = missing > 0f ? (int)Math.Min(missing, amount) : 0;
                if (healed > 0)
                {
                    target.Hp += healed;
                    BattleEvent e = Emit(BattleEventType.Heal, sourceId, target.Id);
                    e.Value = healed;
                    e.Vfx = vfx;

                    // On the HEALER, and only when healing actually landed - a heal into a full
                    // bar restores nothing and is not an outcome to answer. Skipped for a Regen
                    // tick, whose allowBank is false, so a heal-over-time does not re-raise its
                    // caster's trigger once per round for the rest of the battle.
                    if (allowBank)
                    {
                        CombatUnit healer = FindUnit(sourceId);
                        if (healer != null && healer.Alive)
                        {
                            // The healed ally is the broadcast subject so a condition on the
                            // healer's passive can read the recipient's HP via BroadcastSubject.
                            CombatUnit prevSubject = _broadcastSubject;
                            CombatUnit prevVictim = _broadcastVictim;
                            _broadcastSubject = target;
                            _broadcastVictim = target;
                            TriggerPassive(healer, PassiveTrigger.OnHealApplied, 0);
                            _broadcastSubject = prevSubject;
                            _broadcastVictim = prevVictim;
                        }

                        // The recipient's allies hear about it too; the recipient is excluded,
                        // since a hero answering its own healing is a different ability from one
                        // answering somebody else's. _broadcastSourceId carries the healer so
                        // TriggerSourceIsSelf can filter by it.
                        {
                            CombatUnit prevSubject = _broadcastSubject;
                            CombatUnit prevVictim = _broadcastVictim;
                            int prevSourceId = _broadcastSourceId;
                            _broadcastSubject = target;
                            _broadcastVictim = target;
                            _broadcastSourceId = sourceId;
                            BroadcastPassive(target.IsPlayer, PassiveTrigger.OnAllyHealed, 0, target);
                            _broadcastSourceId = prevSourceId;
                            _broadcastSubject = prevSubject;
                            _broadcastVictim = prevVictim;
                        }
                    }
                }

                // Overheal banking: the heal-side mirror of the damage-side Ledger loop.
                // Only entries with BankHeals bank here; the damage-side loop skips them.
                if (allowBank)
                {
                    int overheal = amount - healed;
                    if (overheal > 0)
                    {
                        for (int li = 0; li < target.Statuses.Count; li++)
                        {
                            StatusInstance led = target.Statuses[li];
                            if (led.Kind != StatusKind.Ledger) continue;
                            if (!led.BankHeals) continue;
                            if (led.Magnitude <= 0f) continue;
                            if (led.BankLimit > 0 && led.BankUsesThisRound >= led.BankLimit) continue;
                            float room = led.StoredCap > 0f ? led.StoredCap - led.Stored : float.MaxValue;
                            if (room <= 0f) continue;
                            float banked = overheal * led.Magnitude;
                            if (banked > room) banked = room;
                            bool wasBelow = led.StoredCap > 0f && led.Stored < led.StoredCap;
                            led.Stored += banked;
                            if (led.BankLimit > 0) led.BankUsesThisRound++;
                            if (wasBelow && led.StoredCap > 0f && led.Stored >= led.StoredCap)
                            {
                                CombatUnit ledSource = FindUnit(led.SourceId);
                                if (ledSource != null)
                                {
                                    CombatUnit prevSubject = _broadcastSubject;
                                    _broadcastSubject = target;
                                    BroadcastPassive(ledSource.IsPlayer, PassiveTrigger.OnLedgerCapped, 0, null);
                                    _broadcastSubject = prevSubject;
                                }
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// Adds energy and discards the overflow.
            ///
            /// `sourceId` is the unit that GRANTED this energy, or -1 where there is no granter.
            /// Tide used to read it: energy granted by a Tide source was handed to the ally furthest
            /// from casting rather than being thrown away. That rule is gone, so the parameter is
            /// now carried only so the granter is still recorded in the event the clamp emits, and
            /// _inRedirect went with it.
            /// </summary>
            private void AddEnergy(CombatUnit unit, int amount, int sourceId)
            {
                // --- Disrupted: intercept the first energy gain each round ---
                // Checked before AddEnergyClamped so the gain never reaches the unit. The drain
                // that replaces it calls AddEnergyClamped directly, bypassing this method, so
                // the interceptor cannot re-enter itself. No shipped content applies Disrupted,
                // so this branch is unreachable in every existing battle.
                if (amount > 0 && InterceptDisrupted(unit, amount))
                    return;

                int energyBefore = unit.Alive ? unit.Energy : -1;
                AddEnergyClamped(unit, amount);

                // --- Momentum Breaker: threshold-crossing on TurnDebt carriers ---
                // Checked after AddEnergyClamped so the energy has already moved. The broadcast
                // that follows can grant energy to an ally, which re-enters AddEnergy, but the
                // ally carries no TurnDebt, so CheckEnergyThreshold returns immediately. No
                // shipped content applies TurnDebt, so this branch is unreachable today.
                if (amount > 0 && energyBefore >= 0)
                    CheckEnergyThreshold(unit, energyBefore);
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

            // ------------------------------------------------------------------ energy-gain interception

            /// <summary>
            /// Disrupted: the first energy gain each round is reversed into a drain, the target
            /// is delayed, and the disruptor's next ally is advanced.
            ///
            /// Returns true if the gain was intercepted, in which case AddEnergy must not call
            /// AddEnergyClamped for the original amount. False if no Disrupted entry with uses
            /// remaining was found, letting the gain through unchanged.
            ///
            /// Unreachable in shipped content: nothing applies Disrupted.
            /// </summary>
            private bool InterceptDisrupted(CombatUnit unit, int amount)
            {
                if (!unit.Alive || !unit.Has(StatusKind.Disrupted)) return false;

                for (int i = 0; i < unit.Statuses.Count; i++)
                {
                    StatusInstance d = unit.Statuses[i];
                    if (d.Kind != StatusKind.Disrupted) continue;
                    if (d.UsesPerRound > 0 && d.UsesThisRound >= d.UsesPerRound) continue;

                    d.UsesThisRound++;

                    // Drain: lose Magnitude energy instead of gaining
                    int drain = (int)Math.Round(d.Magnitude, MidpointRounding.AwayFromZero);
                    if (drain > 0) AddEnergyClamped(unit, -drain);

                    // Delay the disrupted enemy and advance the disruptor's next ally
                    float shiftFrac = d.StoredCap;
                    CombatUnit disruptor = FindUnit(d.SourceId);
                    if (shiftFrac > 0f)
                    {
                        ApplyTurnShift(disruptor, unit, -shiftFrac);

                        if (disruptor != null && disruptor.Alive)
                        {
                            CombatUnit nextAlly = NextAllyInOrder(disruptor);
                            if (nextAlly != null)
                                ApplyTurnShift(disruptor, nextAlly, shiftFrac);
                        }
                    }

                    return true;
                }
                return false;
            }

            /// <summary>
            /// Momentum Breaker: if the energy that was just added pushed the unit past its
            /// active skill's energy cost, and the unit carries TurnDebt, broadcast
            /// OnEnemyEnergyThreshold to the opposing side.
            ///
            /// Unreachable in shipped content: nothing applies TurnDebt.
            /// </summary>
            private void CheckEnergyThreshold(CombatUnit unit, int energyBefore)
            {
                if (!unit.Alive || unit.Active == null) return;
                int cost = unit.Active.EnergyCost;
                if (energyBefore >= cost || unit.Energy < cost) return;
                if (!unit.Has(StatusKind.TurnDebt)) return;

                CombatUnit prev = _thresholdUnit;
                _thresholdUnit = unit;
                BroadcastPassive(!unit.IsPlayer, PassiveTrigger.OnEnemyEnergyThreshold, 0, null);
                _thresholdUnit = prev;
            }

            /// <summary>
            /// The next unit on `source`'s own side in the remaining turn order, excluding
            /// source itself. Null if none remain or the order is not being walked.
            /// </summary>
            private CombatUnit NextAllyInOrder(CombatUnit source)
            {
                if (_order == null || source == null) return null;
                int first = _orderIndex + 1;
                for (int i = first; i < _order.Count; i++)
                {
                    CombatUnit u = _order[i];
                    if (u.Alive && u.IsPlayer == source.IsPlayer && u != source) return u;
                }
                return null;
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
            /// as any other - a reflect that kills its attacker fires their death and their
            /// killer's OnKill, because it went through the funnel rather than around it.
            ///
            /// Bounded by _inReflect rather than by MaxPassiveDepth, and it has to be: the reflect
            /// is not a passive and never enters TriggerPassive, so the depth argument that bounds
            /// passive recursion never sees it. Two Mirrored Wound wearers hitting each other would
            /// otherwise reflect into each other without limit. Same shape as the _inCover guard
            /// one method over, for the same reason.
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
                    if (!LimitAllows(owner, i, passive)) continue;
                    if (trigger == PassiveTrigger.OnHpBelowThreshold && owner.HpRatio > passive.Threshold) continue;
                    if (trigger == PassiveTrigger.OnTurnDebtThreshold && _thresholdUnit != null)
                    {
                        float debtStored = 0f;
                        for (int di = 0; di < _thresholdUnit.Statuses.Count; di++)
                            if (_thresholdUnit.Statuses[di].Kind == StatusKind.TurnDebt)
                                debtStored += _thresholdUnit.Statuses[di].Stored;
                        if (debtStored < passive.Threshold) continue;
                    }
                    // TriggerStatus filter: when a passive names a specific status, the trigger
                    // fires only if that status was among the ones that expired. No shipped content
                    // sets TriggerStatus, so this check never fires for existing battles.
                    if ((trigger == PassiveTrigger.OnStatusExpired
                         || trigger == PassiveTrigger.OnAppliedStatusExpired)
                        && passive.TriggerStatus != StatusKind.None
                        && !_expiredKinds.Contains(passive.TriggerStatus)) continue;
                    if (passive.TriggerSourceIsSelf && _broadcastSourceId != owner.Id) continue;
                    if (passive.Effects.Count == 0) continue;
                    if (depth >= MaxPassiveDepth) continue;

                    if (passive.OncePerBattle) owner.MarkPassiveFired(i);
                    if (passive.Limit != PassiveLimit.Unlimited) owner.MarkPassiveRound(i, _round);

                    BattleEvent e = Emit(BattleEventType.PassiveTrigger, owner.Id, owner.Id);
                    e.AbilityId = passive.Id;
                    e.AbilityName = passive.Name;
                    e.Vfx = passive.Vfx;

                    AbilityDef wrapper = new AbilityDef();
                    wrapper.Id = passive.Id;
                    wrapper.Name = passive.Name;
                    wrapper.Vfx = passive.Vfx;
                    wrapper.Effects = passive.Effects;

                    // Saved and restored rather than set and cleared, so a passive that fires
                    // inside this one neither inherits this budget nor destroys it on the way out.
                    CombatUnit prevOwner = _perTargetOwner;
                    int prevIndex = _perTargetIndex;
                    int prevLimit = _perTargetLimit;
                    bool prevPerRound = _perTargetPerRound;

                    _perTargetOwner = passive.PerTargetLimit > 0 ? owner : null;
                    _perTargetIndex = i;
                    _perTargetLimit = passive.PerTargetLimit;
                    _perTargetPerRound = passive.PerTargetPerRound;

                    ResolveAbility(owner, wrapper, depth + 1);

                    _perTargetOwner = prevOwner;
                    _perTargetIndex = prevIndex;
                    _perTargetLimit = prevLimit;
                    _perTargetPerRound = prevPerRound;
                }
            }

            /// <summary>
            /// Whether a passive's own PassiveLimit lets it fire right now.
            ///
            /// Layered ON OncePerBattle rather than replacing it - the caller checks that first and
            /// this second - so a passive that sets no limit answers exactly the question it always
            /// did. Unlimited returns true before reading anything, which is every passive in
            /// shipped content, so no existing battle reaches the clock below.
            ///
            /// Rounds are compared against _round, the live round counter. A passive that fires at
            /// BattleStart records round 0, which is genuinely "before round 1" and therefore
            /// correctly lets a OncePerRound passive fire again in round 1.
            /// </summary>
            private bool LimitAllows(CombatUnit owner, int index, PassiveDef passive)
            {
                switch (passive.Limit)
                {
                    case PassiveLimit.Unlimited:
                        return true;

                    case PassiveLimit.OncePerBattle:
                        // Reads the same flag the bool does, so saying it explicitly in content and
                        // relying on the default cannot disagree.
                        return !owner.PassiveHasFired(index);

                    case PassiveLimit.OncePerRound:
                        return owner.PassiveLastRound(index) != _round;

                    case PassiveLimit.EveryNRounds:
                    {
                        int last = owner.PassiveLastRound(index);
                        if (last == 0) return true;   // never fired
                        return _round - last >= Math.Max(1, passive.LimitRounds);
                    }
                }
                return true;
            }

            /// <summary>
            /// Fires one trigger on every living unit of a side, rather than on one owner.
            ///
            /// The three OnEnemy* triggers need this and nothing else does: they are about
            /// something the OTHER side did, so the unit that acts and the units that care are
            /// different units. Everything past this point is the ordinary TriggerPassive path, so
            /// a broadcast passive answers the same oncePerBattle allowance, the same threshold and
            /// the same MaxPassiveDepth as any other.
            ///
            /// `depth` is threaded from whatever raised the broadcast rather than restarting at 0,
            /// which is what bounds the cascade: a buff-gained passive that itself applies a buff
            /// raises the broadcast again one level deeper, and the existing cap stops it at the
            /// same place it stops a passive calling a passive.
            ///
            /// `except` skips one unit - the damage victim, which has OnDamaged of its own and does
            /// not also need to hear that one of its allies was hit. Null for every other caller.
            ///
            /// Walks a snapshot, because a passive firing inside the loop can kill somebody and
            /// _units is not safe to index live across that.
            /// </summary>
            private void BroadcastPassive(bool playerSide, PassiveTrigger trigger, int depth, CombatUnit except)
            {
                List<CombatUnit> side = new List<CombatUnit>();
                for (int i = 0; i < _units.Count; i++)
                {
                    CombatUnit u = _units[i];
                    if (!u.Alive || u.IsPlayer != playerSide || u == except) continue;
                    side.Add(u);
                }
                for (int i = 0; i < side.Count; i++)
                {
                    if (IsBattleOver()) return;
                    TriggerPassive(side[i], trigger, depth);
                }
            }

            // ------------------------------------------------------------------ turn order effects

            /// <summary>
            /// Moves a unit earlier or later inside the CURRENT round's remaining turn order.
            ///
            /// **There is no ATB, and this does not pretend there is one.** A round is a speed sort
            /// walked once, so a unit has no continuous gauge to move a percentage of - it has a
            /// POSITION among the turns that have not happened yet. So an authored "delay by 25%"
            /// is read as a fraction of that remaining order and applied as a move of that many
            /// places. The translation is lossy in one direction and honest about it: on a short
            /// tail every nonzero shift is one place, so 5% and 30% are the same thing late in a
            /// round and differ only when there is enough order left to tell them apart.
            ///
            /// Ceil rather than round, so a nonzero shift always moves at least one place. A tempo
            /// ability that silently did nothing would be worse than one that is coarse, and
            /// several kits in the incoming roster are built on the delay actually landing.
            ///
            /// **This is the only way an effect moves a turn.** Gale used to move a unit later in
            /// the round a Slow landed in, and to splice a killer back into the remaining tail;
            /// both were this operation with the decision made by the engine. They are gone, and
            /// this is the authored version - the effect makes the decision.
            ///
            /// A unit that has already acted is not in the tail and is not moved, which is both the
            /// termination argument for the round loop and the reason the "advances my own next
            /// turn" family in the roster is a Speed buff rather than this.
            ///
            /// Draws nothing from _rng, so a shift cannot desync a seeded replay.
            /// </summary>
            private void ApplyTurnShift(CombatUnit source, CombatUnit target, float fraction)
            {
                ApplyTurnShift(source, target, fraction, StatusKind.None, 0f, 0f);
            }

            private void ApplyTurnShift(CombatUnit source, CombatUnit target, float fraction,
                                        StatusKind banksAs, float cap, float rate, int debtDuration = 2)
            {
                if (_order == null || target == null || !target.Alive || fraction == 0f) return;

                int first = _orderIndex + 1;
                if (first >= _order.Count) return;

                int at = _order.IndexOf(target, first);
                if (at < 0) return;

                int tail = _order.Count - first;
                float magnitude = fraction < 0f ? -fraction : fraction;
                int places = (int)Math.Ceiling(magnitude * tail);
                if (places < 1) places = 1;

                int destination = fraction > 0f ? at - places : at + places;
                if (destination < first) destination = first;
                if (destination > _order.Count - 1) destination = _order.Count - 1;
                if (destination == at) return;

                _order.RemoveAt(at);
                _order.Insert(destination, target);

                BattleEvent e = Emit(BattleEventType.TurnOrderShift, source != null ? source.Id : -1, target.Id);
                e.Status = StatusKind.None;   // no status caused this; an authored effect did
                e.Value = destination - at;   // signed: negative advanced, positive delayed

                // The REALISED shift, banked as a debt, which is the only way "Turn Debt equal to
                // the progress removed" can be one effect. The number is not the authored fraction:
                // it is what the move actually cost, measured in places over the tail, after the
                // ceil and after both clamps. An effect that asked for 12% on a two-unit tail moved
                // one place and took 50%, and the debt has to say 50% or it is describing a
                // different event than the one that happened.
                if (banksAs != StatusKind.None && destination > at)
                {
                    float realised = (destination - at) / (float)tail;
                    BankTurnDebt(source, target, banksAs, realised, cap, rate, debtDuration);
                }

                if (destination > at && source != null) TriggerPassive(source, PassiveTrigger.OnTurnDelayed, 0);
            }

            /// <summary>
            /// Accumulates a debt on the target and stamps the rate it will be repaid at.
            ///
            /// Accumulates rather than refreshes: two delays land two debts, and a debtor that was
            /// pushed twice owes for both. Capped at `cap` where one is authored, so "up to 30%"
            /// is a property of the debt rather than of how many times it was applied.
            /// </summary>
            private void BankTurnDebt(CombatUnit source, CombatUnit target, StatusKind kind,
                                      float amount, float cap, float rate, int duration = 2)
            {
                if (target == null || !target.Alive || amount <= 0f) return;

                int sourceId = source != null ? source.Id : -1;
                StatusInstance entry = null;
                for (int i = 0; i < target.Statuses.Count; i++)
                {
                    if (target.Statuses[i].Kind != kind) continue;
                    if (target.Statuses[i].SourceId != sourceId) continue;
                    entry = target.Statuses[i];
                    break;
                }

                if (entry == null)
                {
                    target.AddOrRefresh(kind, Math.Max(1, duration), rate, sourceId);
                    for (int i = 0; i < target.Statuses.Count; i++)
                        if (target.Statuses[i].Kind == kind && target.Statuses[i].SourceId == sourceId)
                            entry = target.Statuses[i];
                    if (entry == null) return;
                    entry.StoredCap = cap;

                    BattleEvent applied = Emit(BattleEventType.StatusApplied, sourceId, target.Id);
                    applied.Status = kind;
                    applied.Duration = entry.RoundsLeft;
                }

                float storedBefore = entry.Stored;
                entry.Stored += amount;
                if (entry.StoredCap > 0f && entry.Stored > entry.StoredCap) entry.Stored = entry.StoredCap;

                // OnTurnDebtThreshold: broadcast when Stored crosses any passive's Threshold.
                // Fires once per accumulation event that pushes past the line, the same shape
                // OnLedgerCapped uses.
                if (entry.Stored > storedBefore && source != null)
                {
                    CombatUnit prev = _thresholdUnit;
                    _thresholdUnit = target;
                    CombatUnit prevSubject = _broadcastSubject;
                    _broadcastSubject = target;
                    BroadcastPassive(source.IsPlayer, PassiveTrigger.OnTurnDebtThreshold, 0, null);
                    _broadcastSubject = prevSubject;
                    _thresholdUnit = prev;
                }
            }

            /// <summary>
            /// Swaps the unit closest to acting with the unit furthest from acting, among the
            /// target's own side and among the turns not yet taken. Both ends come from the order
            /// itself, so this needs no second target selector.
            ///
            /// A no-op when fewer than two of that side are still waiting, which is the same
            /// natural bound ApplyTurnShift has: there is no ordering left to change.
            /// </summary>
            private void ApplySwapTurnOrder(CombatUnit source, CombatUnit target)
            {
                if (_order == null || target == null || !target.Alive) return;

                int first = _orderIndex + 1;
                int earliest = -1, latest = -1;
                for (int i = first; i < _order.Count; i++)
                {
                    if (!_order[i].Alive || _order[i].IsPlayer != target.IsPlayer) continue;
                    if (earliest < 0) earliest = i;
                    latest = i;
                }
                if (earliest < 0 || latest <= earliest) return;

                CombatUnit a = _order[earliest];
                CombatUnit b = _order[latest];
                _order[earliest] = b;
                _order[latest] = a;

                BattleEvent e = Emit(BattleEventType.TurnOrderShift, source != null ? source.Id : -1, b.Id);
                e.Status = StatusKind.None;
                e.Value = earliest - latest;

                if (source != null) TriggerPassive(source, PassiveTrigger.OnTurnDelayed, 0);
            }

            // ------------------------------------------------------------------ targeting

            private List<CombatUnit> ResolveTargets(CombatUnit caster, TargetMode mode)
            {
                return ResolveTargets(caster, mode, null, StatusKind.None);
            }

            private List<CombatUnit> ResolveTargets(CombatUnit caster, TargetMode mode, CombatUnit excluded)
            {
                return ResolveTargets(caster, mode, excluded, StatusKind.None);
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
            /// <summary>
            /// `targetStatus` is read by TargetMode.EnemyWithStatus and by nothing else, and is
            /// StatusKind.None for every caller that does not use that mode - which is the two
            /// overloads above and therefore every relic and every internal lookup. It comes from
            /// EffectDef.TargetStatus, threaded rather than looked up, so this method stays a
            /// function of its arguments instead of reaching back into the effect.
            /// </summary>
            private List<CombatUnit> ResolveTargets(CombatUnit caster, TargetMode mode, CombatUnit excluded,
                                                    StatusKind targetStatus)
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
                            if (!u.Alive || u.IsPlayer != caster.IsPlayer || u == excluded || Concealed(u)) continue;
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

                    // The four ally modes below return from HERE, above the anchor lookup, exactly
                    // as LowestHpAlly and RandomAlly already do. The anchor answers single-enemy
                    // questions aimed at one side; it has nothing to say about which of the
                    // caster's own allies an effect lands on.

                    case TargetMode.LowestEnergyAlly:
                    {
                        CombatUnit best = null;
                        for (int i = 0; i < _units.Count; i++)
                        {
                            CombatUnit u = _units[i];
                            if (!u.Alive || u.IsPlayer != caster.IsPlayer || u == excluded || Concealed(u)) continue;
                            if (best == null || u.Energy < best.Energy
                                || (u.Energy == best.Energy && u.Slot < best.Slot)) best = u;
                        }
                        if (best != null) result.Add(best);
                        return result;
                    }

                    case TargetMode.TwoLowestHpAllies:
                    {
                        List<CombatUnit> ranked = AlliesByHp(caster, excluded);
                        for (int i = 0; i < ranked.Count && i < 2; i++) result.Add(ranked[i]);
                        return result;
                    }

                    case TargetMode.ThreeLowestHpAllies:
                    {
                        List<CombatUnit> ranked = AlliesByHp(caster, excluded);
                        for (int i = 0; i < ranked.Count && i < 3; i++) result.Add(ranked[i]);
                        return result;
                    }

                    case TargetMode.SecondLowestHpAlly:
                    {
                        List<CombatUnit> ranked = AlliesByHp(caster, excluded);
                        // Empty on a line of one, rather than falling back to the lowest. An
                        // effect that names the second-lowest ally is asking for a specific body,
                        // and silently retargeting it onto the first would be a different ability.
                        if (ranked.Count >= 2) result.Add(ranked[1]);
                        return result;
                    }

                    case TargetMode.AllyWithMostDebuffs:
                    {
                        CombatUnit best = null;
                        for (int i = 0; i < _units.Count; i++)
                        {
                            CombatUnit u = _units[i];
                            if (!u.Alive || u.IsPlayer != caster.IsPlayer || u == excluded || Concealed(u)) continue;
                            if (best == null || u.DebuffCount > best.DebuffCount
                                || (u.DebuffCount == best.DebuffCount && u.Slot < best.Slot)) best = u;
                        }
                        // Deliberately still answers when nobody is debuffed at all: the effect is
                        // a heal or a cleanse aimed at whoever needs it most, and "most" of zero is
                        // the front-most ally rather than nobody.
                        if (best != null) result.Add(best);
                        return result;
                    }

                    // BroadcastVictim resolves BEFORE the anchor, alongside the ally modes: the
                    // whole point is to reach a specific unit regardless of which side it is on,
                    // and a Taunt holder must not intercept it.
                    case TargetMode.BroadcastVictim:
                    {
                        if (_broadcastVictim != null && _broadcastVictim.Alive && _broadcastVictim != excluded)
                            result.Add(_broadcastVictim);
                        return result;
                    }

                }

                // Every remaining mode targets a single enemy, and the anchor overrides all of them
                // including LastAttacker/LastTarget, so the unit that answers for a side cannot be
                // targeted around. ("Provoke" in the previous version of this comment was the old
                // name for StatusKind.Taunt and had outlived it.)
                //
                // Note for anyone reading the RNG stream: returning here short-circuits the switch
                // below, so RandomEnemy does not draw when a side has an anchor. Only a Taunt
                // holder is an anchor now, so that is as rare as Taunt is.
                // PROVOKED, checked before the anchor and after every ally mode has returned.
                //
                // The opposite direction to Taunt, and its own rule rather than a variant: Taunt
                // sits on a protector and makes it answer for its whole side against everyone,
                // while this sits on ONE attacker - the caster standing here - and constrains that
                // attacker alone. Four abilities in the roster say "Taunts the front enemy",
                // meaning exactly one enemy is provoked, and Taunt on Self is wrong in both
                // directions for them: too wide, because every other enemy is redirected too, and
                // too narrow, because it says nothing about what that one enemy does when the
                // caster is not its side's anchor.
                //
                // The ANCHOR STILL WINS where both apply. A Taunt is a deliberate, visible claim
                // on every single-enemy question aimed at that side, and a provoke that outranked
                // it would make a protector's own ability conditional on what the attacker happens
                // to be carrying. Unreachable today either way, since nothing in content applies
                // Provoked.
                for (int i = 0; i < caster.Statuses.Count; i++)
                {
                    StatusInstance p = caster.Statuses[i];
                    if (p.Kind != StatusKind.Provoked) continue;
                    CombatUnit provoker = FindUnit(p.SourceId);
                    if (provoker == null || !provoker.Alive || provoker == excluded) continue;
                    if (provoker.IsPlayer == caster.IsPlayer) continue;
                    if (FindTargetAnchor(!caster.IsPlayer) != null) break;   // the anchor answers instead
                    result.Add(provoker);
                    return result;
                }

                // CONCEALMENT, applied once and before the anchor, so a Concealed taunter stops
                // answering for its side rather than becoming an unhittable wall.
                //
                // StatusKind.Stealth was declared for this and left inert; it costs no ordinal and
                // no save byte. The rule is narrow on purpose: a Concealed unit is skipped by every
                // SINGLE-TARGET pick and is not skipped by AllEnemies or AllAllies, which is what
                // "cannot be targeted directly" says and what keeps concealment from being immunity.
                //
                // THE LAST BODY IS ALWAYS TARGETABLE. A side whose every living unit is Concealed
                // would make single-target attacks resolve onto nobody, and a fight where one side
                // cannot be hit does not end - it runs to maxRounds and reports a loss that no
                // decision produced. So the exclusion is dropped when it would empty a side, which
                // costs the last concealed unit its hiding place and is the correct trade.
                CombatUnit anchor = FindTargetAnchor(!caster.IsPlayer);
                if (anchor == excluded) anchor = null;
                if (anchor != null)
                {
                    // No TargetAnchored event any more. It existed for the Lumen rule alone: a
                    // Taunt holder wears its own status tag, so the redirect explains itself on the
                    // target's bar, and Lumen answering silently was the case the record could not
                    // otherwise tell from a genuine lowest-HP pick. With Lumen gone there is nothing
                    // left that the status row does not already say.
                    //
                    // BattleEventType.TargetAnchored is deliberately NOT removed from the enum. It
                    // is in the permanent record of every battle resolved before this change, and
                    // deleting a member would renumber every event after it.
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
                        CollectSide(enemies, !caster.IsPlayer, excluded, true);
                        if (enemies.Count > 0) picked = enemies[_rng.Range(0, enemies.Count)];
                        break;
                    }

                    // Its own case rather than a line in the loop below, because it FILTERS where
                    // the others COMPARE: that loop takes the first living enemy unconditionally
                    // and then improves on it, which would hand back an unmarked enemy whenever the
                    // first one it saw was unmarked. Front-most among the marked wins, and a mode
                    // naming no status matches nobody rather than everybody.
                    // The mark half of EnemyWithStatus: same shape, but the entry has to have been
                    // applied by THIS caster, which is what makes one hero's mark its own.
                    case TargetMode.EnemyWithMyMark:
                    {
                        StatusKind markKind = MarkKindOf(targetStatus);
                        for (int i = 0; i < _units.Count; i++)
                        {
                            CombatUnit u = _units[i];
                            if (!u.Alive || u.IsPlayer == caster.IsPlayer || u == excluded || Concealed(u)) continue;
                            if (!u.HasMarkFrom(markKind, caster.Id)) continue;
                            if (picked == null || u.Slot < picked.Slot) picked = u;
                        }
                        break;
                    }

                    // THE ONE MODE THAT CAN NAME A DEAD UNIT, and only inside the window where
                    // that means something. Outside a defeat broadcast _defeated is null and this
                    // resolves to nobody, which is the honest answer rather than a fallback.
                    //
                    // The Alive gate every other mode applies is deliberately NOT applied: the
                    // whole point is to reach a corpse whose statuses have not been cleared yet.
                    // Effects that cannot act on a corpse refuse it themselves - ApplyHealValue
                    // returns on a dead target, DealRawDamage on a dead target does nothing - so a
                    // mis-authored effect aimed here is inert rather than wrong.
                    case TargetMode.DefeatedUnit:
                        picked = _defeated != null && _defeated != excluded ? _defeated : null;
                        break;

                    case TargetMode.ThresholdUnit:
                        picked = _thresholdUnit != null && _thresholdUnit.Alive && _thresholdUnit != excluded
                            ? _thresholdUnit : null;
                        break;

                    case TargetMode.BroadcastSubject:
                        // Like ThresholdUnit: alive, not excluded, null outside a broadcast.
                        picked = _broadcastSubject != null && _broadcastSubject.Alive && _broadcastSubject != excluded
                            ? _broadcastSubject : null;
                        break;

                    case TargetMode.EnemyWithStatus:
                    {
                        if (targetStatus == StatusKind.None) break;
                        for (int i = 0; i < _units.Count; i++)
                        {
                            CombatUnit u = _units[i];
                            if (!u.Alive || u.IsPlayer == caster.IsPlayer || u == excluded || Concealed(u)) continue;
                            if (!u.Has(targetStatus)) continue;
                            if (picked == null || u.Slot < picked.Slot) picked = u;
                        }
                        break;
                    }

                    case TargetMode.HighestDamageDealtEnemy:
                    {
                        for (int i = 0; i < _units.Count; i++)
                        {
                            CombatUnit u = _units[i];
                            if (!u.Alive || u.IsPlayer == caster.IsPlayer || u == excluded || Concealed(u)) continue;
                            if (picked == null || u.DamageDealtThisRound > picked.DamageDealtThisRound
                                || (u.DamageDealtThisRound == picked.DamageDealtThisRound && u.Slot < picked.Slot))
                                picked = u;
                        }
                        break;
                    }

                    case TargetMode.HighestDoTDamageEnemy:
                    {
                        for (int i = 0; i < _units.Count; i++)
                        {
                            CombatUnit u = _units[i];
                            if (!u.Alive || u.IsPlayer == caster.IsPlayer || u == excluded || Concealed(u)) continue;
                            if (picked == null || u.DoTDamageDealtThisRound > picked.DoTDamageDealtThisRound
                                || (u.DoTDamageDealtThisRound == picked.DoTDamageDealtThisRound && u.Slot < picked.Slot))
                                picked = u;
                        }
                        break;
                    }

                    default:
                    {
                        for (int i = 0; i < _units.Count; i++)
                        {
                            CombatUnit u = _units[i];
                            if (!u.Alive || u.IsPlayer == caster.IsPlayer || u == excluded || Concealed(u)) continue;
                            if (picked == null) { picked = u; continue; }
                            if (mode == TargetMode.LowestHpEnemy && u.Hp < picked.Hp) picked = u;
                            else if (mode == TargetMode.HighestHpEnemy && u.Hp > picked.Hp) picked = u;
                            else if (mode == TargetMode.HighestAttackEnemy && u.EffectiveAttack > picked.EffectiveAttack) picked = u;
                            else if (mode == TargetMode.FrontEnemy && u.Slot < picked.Slot) picked = u;
                            // Speed is read through EffectiveSpeed, so a Slow already standing
                            // counts - "the slowest enemy" means the one acting last, not the one
                            // with the lowest number on its card. Energy is raw: nothing modifies
                            // it, and it is already the number the player watches fill.
                            else if (mode == TargetMode.HighestEnergyEnemy && u.Energy > picked.Energy) picked = u;
                            else if (mode == TargetMode.HighestSpeedEnemy && u.EffectiveSpeed > picked.EffectiveSpeed) picked = u;
                            else if (mode == TargetMode.LowestSpeedEnemy && u.EffectiveSpeed < picked.EffectiveSpeed) picked = u;
                        }
                        break;
                    }
                }
                if (picked != null) result.Add(picked);
                return result;
            }

            /// <summary>
            /// The caster's living allies, lowest HP ratio first - the one ordering both
            /// TwoLowestHpAllies and SecondLowestHpAlly read, so the pair cannot disagree about who
            /// is second.
            ///
            /// Ratio, not absolute HP, matching LowestHpAlly: a 400/2600 tank is worse off than a
            /// 500/1450 mage even though it has more HP left. Ties break on Slot and then Id, which
            /// cannot both tie between two distinct units, so the ordering is total and nothing
            /// here draws from _rng - the same determinism rule FindMarkHeir follows.
            /// </summary>
            private List<CombatUnit> AlliesByHp(CombatUnit caster, CombatUnit excluded)
            {
                List<CombatUnit> allies = new List<CombatUnit>();
                for (int i = 0; i < _units.Count; i++)
                {
                    CombatUnit u = _units[i];
                    if (!u.Alive || u.IsPlayer != caster.IsPlayer || u == excluded || Concealed(u)) continue;
                    allies.Add(u);
                }
                allies.Sort(CompareByHpRatio);
                return allies;
            }

            private static int CompareByHpRatio(CombatUnit a, CombatUnit b)
            {
                int byHp = a.HpRatio.CompareTo(b.HpRatio);
                if (byHp != 0) return byHp;
                if (a.Slot != b.Slot) return a.Slot.CompareTo(b.Slot);
                return a.Id.CompareTo(b.Id);
            }

            /// <summary>
            /// The living unit occupying `slot` on the given side, or null. Used by Splash to
            /// find neighbours at slot ± 1. No RNG draw: slot identity is deterministic.
            /// </summary>
            private CombatUnit FindAliveInSlot(bool playerSide, int slot)
            {
                for (int i = 0; i < _units.Count; i++)
                {
                    CombatUnit u = _units[i];
                    if (u.Alive && u.IsPlayer == playerSide && u.Slot == slot) return u;
                }
                return null;
            }

            private void CollectSide(List<CombatUnit> into, bool playerSide)
            {
                CollectSide(into, playerSide, null);
            }

            private void CollectSide(List<CombatUnit> into, bool playerSide, CombatUnit excluded)
            {
                CollectSide(into, playerSide, excluded, false);
            }

            /// <summary>
            /// `skipConcealed` is set by the SINGLE-TARGET callers and left false by the wide ones,
            /// which is the whole of the concealment rule: "cannot be targeted directly" hides a
            /// unit from a pick and not from an area hit.
            /// </summary>
            private void CollectSide(List<CombatUnit> into, bool playerSide, CombatUnit excluded, bool skipConcealed)
            {
                for (int i = 0; i < _units.Count; i++)
                {
                    CombatUnit u = _units[i];
                    if (!u.Alive || u.IsPlayer != playerSide || u == excluded) continue;
                    if (skipConcealed && Concealed(u)) continue;
                    into.Add(u);
                }
            }

            /// <summary>
            /// The unit that answers every single-enemy targeting question aimed at `playerSide`, or
            /// null when the question resolves by its own TargetMode.
            ///
            /// TAUNT, AND ONLY TAUNT. A living Lumen unit used to answer here unconditionally, which
            /// made targeting deterministic for its whole side; that rule is removed, and the aspect
            /// is now the damage chart and nothing else. What is left is the mechanic that was here
            /// before Lumen arrived: a unit carrying StatusKind.Taunt answers for its side while the
            /// status is up, and stops when it expires.
            ///
            /// Ties break on Slot and nothing here draws from _rng, so the answer is stable within a
            /// fight and across replays of it. A dead taunter stops answering immediately.
            ///
            /// Still serves Provoked as well, one caller up: ResolveTargets checks a provoke first
            /// and yields to this when a side has an anchor, so removing Lumen narrows what this
            /// returns without changing who consults it.
            /// </summary>
            private CombatUnit FindTargetAnchor(bool playerSide)
            {
                CombatUnit taunter = null;
                for (int i = 0; i < _units.Count; i++)
                {
                    CombatUnit u = _units[i];
                    if (!u.Alive || u.IsPlayer != playerSide) continue;
                    if (Concealed(u)) continue;   // a hidden protector is not answering for anybody
                    if (u.HasTaunt && (taunter == null || u.Slot < taunter.Slot)) taunter = u;
                }
                return taunter;
            }

            /// <summary>
            /// Lands one status entry on a new carrier and records it, with its remaining duration,
            /// its magnitude and its original applier all carried across unchanged.
            ///
            /// THE ONE MOVER. Umbra's death-jump wrote this inline and was the only transfer in the
            /// game; MoveDebuffs and MoveBuffs need exactly the same operation, so they call this
            /// rather than growing a second one beside it. That matters for more than tidiness: the
            /// jump's guarantee is that a move CREATES NO VALUE - RoundsLeft and Magnitude cross
            /// untouched and the entry is added with AddOrRefresh, so a transfer relocates a
            /// subtraction that already existed and never deepens one - and a second mover would be
            /// a second place for that guarantee to quietly stop holding.
            ///
            /// `removeFromSource` is false for the death-jump, whose carrier is being cleared
            /// anyway, and true for an authored move, which has to take the status off the unit it
            /// came from or the move would be a copy.
            ///
            /// Reported as StatusSpread rather than StatusApplied for the reason that event's own
            /// note gives: nobody applied this. Value carries the original applier's id, which is
            /// otherwise unrecoverable once the source's list is gone.
            /// </summary>
            /// <summary>
            /// Writes a non-numeric fact onto the caster's own entry of `effect.Status` on the
            /// target. No entry, no record - this never creates a status, because the record is a
            /// property OF a mark rather than a mark of its own, and creating one silently would
            /// make a mistyped `status` field look like it worked.
            ///
            /// Emits nothing. A record is not an outcome the screen has to explain: what the player
            /// sees is the mark, which already has its own StatusApplied, and the detonation later,
            /// which has its own event. An event per record would narrate bookkeeping.
            /// </summary>
            private void RecordFact(CombatUnit source, CombatUnit target, EffectDef effect)
            {
                if (source == null || target == null || effect.Record == RecordKind.None) return;

                StatusInstance entry = null;
                for (int i = 0; i < target.Statuses.Count; i++)
                {
                    if (target.Statuses[i].Kind != effect.Status) continue;
                    if (target.Statuses[i].SourceId != source.Id) continue;
                    entry = target.Statuses[i];
                    break;
                }
                if (entry == null) return;

                switch (effect.Record)
                {
                    case RecordKind.LastPositiveStatus:
                    {
                        // The most recent beneficial entry, walked backwards because AddOrRefresh
                        // appends: the last match is the newest. Nothing is written when the target
                        // carries no buff at all, so a record keeps the last thing it saw rather
                        // than being wiped by a moment of having nothing.
                        for (int i = target.Statuses.Count - 1; i >= 0; i--)
                        {
                            StatusKind k = target.Statuses[i].Kind;
                            if (k == effect.Status || StatusInfo.IsHarmful(k)) continue;
                            entry.StoredStatus = k;
                            break;
                        }
                        break;
                    }

                    case RecordKind.LastActionType:
                        // 0 stays 0 until the subject has actually acted, so "changed its action
                        // type" is false on a unit that has taken one turn rather than true.
                        if (target.ActionsTaken > 0) entry.StoredAction = target.LastActionWasActive ? 2 : 1;
                        break;

                    case RecordKind.SubjectRole:
                        entry.StoredRole = target.Role;
                        entry.StoredRoleSet = true;
                        break;

                    case RecordKind.MinHpRatio:
                        // A running minimum: it only ever moves down while the entry lives, which
                        // is the difference between "the lowest it went" and "where it is now".
                        if (entry.StoredMin < 0f || target.HpRatio < entry.StoredMin)
                            entry.StoredMin = target.HpRatio;
                        break;
                }
            }

            private void MoveStatus(CombatUnit from, CombatUnit to, StatusInstance status, bool removeFromSource)
            {
                if (to == null || status == null) return;

                if (removeFromSource && from != null) from.Statuses.Remove(status);

                to.AddOrRefresh(status.Kind, status.RoundsLeft, status.Magnitude, status.SourceId);

                // EVERYTHING THE ENTRY RECORDED TRAVELS WITH IT. A ledger that forgets its balance
                // when it changes carrier is not a ledger, and a record that forgets what it
                // recorded is not a record - which is precisely what the four abilities moving a
                // Scar Record or a Residue off a corpse are asking to keep. AddOrRefresh created
                // or refreshed a bare entry, so the carried fields are copied onto it here.
                //
                // This creates no value, the same guarantee the rest of MoveStatus holds to:
                // every field is copied across unchanged, never combined with what was already
                // there, so a move relocates a record rather than deepening one.
                for (int i = 0; i < to.Statuses.Count; i++)
                {
                    StatusInstance landed = to.Statuses[i];
                    if (landed.Kind != status.Kind || landed.SourceId != status.SourceId) continue;
                    landed.Stored = status.Stored;
                    landed.StoredCap = status.StoredCap;
                    landed.Charges = status.Charges;
                    landed.StoredStatus = status.StoredStatus;
                    landed.StoredAction = status.StoredAction;
                    landed.StoredRole = status.StoredRole;
                    landed.StoredRoleSet = status.StoredRoleSet;
                    landed.StoredMin = status.StoredMin;
                    landed.UsesPerRound = status.UsesPerRound;
                    landed.UsesThisRound = status.UsesThisRound;
                    break;
                }

                BattleEvent e = Emit(BattleEventType.StatusSpread, from != null ? from.Id : -1, to.Id);
                e.Status = status.Kind;
                e.Duration = status.RoundsLeft;
                e.Value = status.SourceId;
            }

            /// <summary>
            /// Whether this unit is hidden from single-target picks right now.
            ///
            /// THE LAST BODY IS NEVER CONCEALED. A side whose every living unit carried Stealth
            /// would make single-target attacks resolve onto nobody, and a fight where one side
            /// cannot be hit does not end: it runs to maxRounds and reports a loss no decision
            /// produced. So the answer is false whenever saying true would empty a side. That costs
            /// the last hidden unit its hiding place, which is the correct trade and is also the
            /// only way concealment stays a tempo tool rather than immunity.
            ///
            /// StatusKind.Stealth was declared for this in an earlier pass and left inert, so it
            /// costs no ordinal and no save byte.
            /// </summary>
            private bool Concealed(CombatUnit unit)
            {
                if (unit == null || !unit.Alive || !unit.Has(StatusKind.Stealth)) return false;
                for (int i = 0; i < _units.Count; i++)
                {
                    CombatUnit u = _units[i];
                    if (!u.Alive || u.IsPlayer != unit.IsPlayer) continue;
                    if (!u.Has(StatusKind.Stealth)) return true;   // somebody else can be hit
                }
                return false;
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
