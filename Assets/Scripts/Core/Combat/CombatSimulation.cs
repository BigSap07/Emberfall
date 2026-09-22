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
    public static partial class CombatSimulation
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
        /// <summary>Resolves which counter index an effect/condition refers to.
        /// 0 = Resolve, 1 = Resolve2, ≥ 2 = generalized.
        /// Returns -1 when the status is not a counter kind (e.g. Burn stacks).</summary>
        static int ResolveCounterIndex(int explicitIndex, StatusKind status)
        {
            if (explicitIndex > 0) return explicitIndex;
            if (status == StatusKind.Resolve2) return 1;
            if (status == StatusKind.Resolve || status == StatusKind.None) return 0;
            return -1;
        }

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
                    int ci = ResolveCounterIndex(effect.ConditionCounterIndex, effect.ConditionStatus);
                    if (ci >= 2)
                    {
                        int needed = (int)Math.Round(effect.ConditionValue, MidpointRounding.AwayFromZero);
                        if (needed < 1) needed = 1;
                        return subject.CountOfCounter(ci) >= needed;
                    }
                    if (effect.ConditionStatus == StatusKind.None) return false;
                    int needed1 = (int)Math.Round(effect.ConditionValue, MidpointRounding.AwayFromZero);
                    if (needed1 < 1) needed1 = 1;
                    return subject.CountOf(effect.ConditionStatus) >= needed1;
                }

                // Source-filtered variant: counts only entries applied by the caster, so two
                // heroes using the same status kind each see their own depth.
                case EffectCondition.MyStatusStacksAtLeast:
                {
                    if (effect.ConditionStatus == StatusKind.None || caster == null) return false;
                    int needed = (int)Math.Round(effect.ConditionValue, MidpointRounding.AwayFromZero);
                    if (needed < 1) needed = 1;
                    return subject.CountOfFrom(effect.ConditionStatus, caster.Id) >= needed;
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

                // The same window with the caster's own blows taken out of it. An OR over entries,
                // so the dictionary's enumeration order cannot change the answer.
                case EffectCondition.SubjectDamagedByOtherSinceMyTurn:
                {
                    if (caster == null) return false;
                    foreach (KeyValuePair<int, int> hit in subject.LastDamagedRoundBy)
                        if (hit.Key != caster.Id && hit.Value > 0 && hit.Value >= caster.LastTurnRound)
                            return true;
                    return false;
                }

                // The heal and shield siblings of the line above, round-granular in exactly the
                // same way and carrying the same approximation: two units acting inside one round
                // are not ordered against each other by it.
                case EffectCondition.SubjectHealedSinceMyTurn:
                    return caster != null && subject.LastHealedRound > 0
                        && subject.LastHealedRound >= caster.LastTurnRound;

                case EffectCondition.SubjectShieldedSinceMyTurn:
                    return caster != null && subject.LastShieldedRound > 0
                        && subject.LastShieldedRound >= caster.LastTurnRound;

                // Absolute Energy, read raw - nothing modifies it, and it is already the number
                // the player watches fill. CasterStatBelowTarget compares two units and could not
                // answer "more than 70", which is a question about one.
                case EffectCondition.EnergyAtLeast:
                    return subject.Energy >= effect.ConditionValue;

                case EffectCondition.EnergyBelow:
                    return subject.Energy <= effect.ConditionValue;

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

                // GAP 9. The exact mirror of the line above, reading the same list, because this
                // enum has no negation and the clause that needed one - "repeat the damage against
                // a lower-HP enemy, if ANOTHER enemy has lower HP than the target" - is asking for
                // a body the blow has not already landed on.
                //
                // True when the caster is null, matching how the positive form reads for a
                // casterless effect: nobody has damaged anything, so nothing has been damaged.
                case EffectCondition.SubjectNotDamagedByMeThisRound:
                    return caster == null || caster.TimesDamagedThisRound(subject.Id) == 0;

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

                // What the subject IS, as against what somebody wrote down about it. Pure - it
                // reads a field rebuilt from content at battle start - so it lives in the static
                // evaluator with the rest of the unit questions.
                case EffectCondition.SubjectRoleIs:
                    return subject.Role == (HeroRole)(int)effect.ConditionValue;

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
                        // Exclude inert / counter kinds that are neither buff nor debuff. Linked
                        // joins the list for the same reason Mark and Ledger are on it: a binding
                        // is a claim about two units, not something good happening to one, and a
                        // kit asking "does it have a buff on it" is not asking about a link.
                        if (StatusInfo.IsInert(k))
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
                // SubjectIsCasting is contextual for the same reason and in the same way: the
                // declared cast is Sim state, so the pure function cannot see one and says no.
                case EffectCondition.SubjectIsCasting:
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

        private sealed partial class Sim
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
            /// True while a share of a hit is being delivered somewhere other than where it landed,
            /// bounding every such move to ONE HOP. Shared by the Covered path and by both halves
            /// of StatusKind.Linked, which is why it is no longer named for Cover alone.
            ///
            /// Not latent: two units covering each other is an ordinary thing for a player to
            /// build, and a pair of units linked to each other is the entire shape of Talren's
            /// active. Without this the first hit between either pair passes a share back and forth
            /// until the stack gives out.
            ///
            /// ONE GUARD FOR ALL THREE, and that is the conservative choice stated rather than left
            /// to be discovered. A Covered hop landing on a protector who is himself Linked does
            /// not then echo, and a link redirect landing on a partner who is Covered does not
            /// split again. Separate guards would let a chain of alternating mechanics run as deep
            /// as the pairings allowed. Same shape as _inReflect.
            /// </summary>
            private bool _inRedirect;

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
            private readonly Dictionary<int, float> _lastLedgerRelease = new Dictionary<int, float>();
            private float _lastRedirectAmount;

            /// <summary>The two bodies the last SwapTurnOrder moved, named by the target modes that
            /// ask about them. Null outside the ability that performed the swap.</summary>
            /// <summary>
            /// THE CAST IN FLIGHT. Null between casts, which is every moment of every battle in
            /// shipped content outside the few lines a cast takes to resolve.
            ///
            /// An active used to be a statement: the energy went, the event was emitted, the
            /// effects ran, and at no point was there a THING that could be pointed at and stopped.
            /// These three fields are that thing. They are set at declaration, read by anything
            /// that wants to answer a cast, and cleared on the way out of the turn whether the cast
            /// resolved or not.
            /// </summary>
            private CombatUnit _castCaster;
            private AbilityDef _castAbility;
            private bool _castCancelled;
            private int _castCancelSourceId = -1;

            /// <summary>Closes the window. Called on every exit from a cast - resolved, cancelled,
            /// or the caster dying inside its own declaration - so nothing outside one can ever
            /// read a stale cast.</summary>
            private void ClearCast()
            {
                _castCaster = null;
                _castAbility = null;
                _castCancelled = false;
                _castCancelSourceId = -1;
            }

            private CombatUnit _swapAdvanced;
            private CombatUnit _swapDelayed;

            /// <summary>The turn-progress fraction each caster's last shift actually realised.
            /// Keyed per caster from the start, the way _lastMoved is, so two units shifting in one
            /// round cannot answer for each other.</summary>
            private readonly Dictionary<int, float> _lastTurnShift = new Dictionary<int, float>();

            /// <summary>
            /// A delay authored against a unit that had already acted, held until the round it CAN
            /// be spent in.
            ///
            /// A round is a speed sort walked once, so a unit that has taken its turn is not in
            /// the remaining order and there is no place left to push it to. Every "delay that
            /// enemy by X% turn progress" in the roster is written on a trigger that fires because
            /// the enemy just acted - Merek's whole kit is - so the one moment the effect is
            /// authored for was the one moment it could not land, and it silently did nothing for
            /// its entire life.
            ///
            /// Held rather than dropped, and spent at the top of the next round before anybody
            /// moves. "Delayed by 10% turn progress" is a claim about that unit's next turn, and
            /// its next turn is in the next round; the alternative reading - that a delay bought
            /// after the target acted is worth nothing - is not what any of the descriptions say.
            ///
            /// DELAYS ONLY. An advance aimed at a unit that has already acted is left alone: there
            /// is no text in the roster asking for one to be carried, and carrying it would mean
            /// "act sooner" quietly became "act sooner NEXT round", which is a different promise.
            ///
            /// One entry per authored delay rather than a per-target sum, so two delays land as
            /// two moves next round exactly as they would have landed as two moves this round.
            /// </summary>
            private sealed class CarriedDelay
            {
                public CombatUnit Source;
                public CombatUnit Target;
                public float Fraction;
                public StatusKind BanksAs;
                public float Cap, Rate;
                public int DebtDuration;
            }

            private readonly List<CarriedDelay> _carriedDelays = new List<CarriedDelay>();
            /// <summary>
            /// WHAT the last Cleanse, Dispel or RemoveStatus took off a unit, as a damage amount -
            /// not how many entries it took.
            ///
            /// IT USED TO BE A COUNT, AND THAT WAS THE DEFECT. `ScaleByLastRemoval` fed this into
            /// an effect's Attack term, so a Heal authored at power 0.8 against a removed Burn
            /// healed for `1 x 0.8` = 1 HP. Both descriptions in the roster that reach this flag
            /// say AMOUNT in as many words - "the removed damage is converted into healing equal to
            /// 100% of that amount", "converts only 60% of its removed damage" - and nothing
            /// anywhere reads it expecting a count, which is what made changing the meaning the
            /// right repair rather than adding a second scalar beside it. A sibling would have left
            /// the mis-named one in place for the next author to reach for.
            ///
            /// The authored JSON key keeps its spelling. Renaming `scaleByLastRemoval` would have
            /// broken the two effects this exists to fix, for no gain: the flag still means "scale
            /// by what the last removal took", and only the unit of measurement was wrong.
            /// </summary>
            private float _lastRemovedValue;

            /// <summary>
            /// The victim's HP fraction BEFORE the blow whose threshold broadcast is in flight, and
            /// the reason OnAllyHpBelowThreshold and OnEnemyHpBelowThreshold can say "fell below"
            /// rather than "was hit while low".
            ///
            /// A CROSSING NEEDS TWO READINGS AND ONLY ONE OF THEM SURVIVES THE BLOW. The watcher's
            /// own Threshold decides how low counts, but "fell below 35%" is a claim about a
            /// boundary being crossed, and the unit's live HpRatio alone cannot tell the crossing
            /// from the twenty chip hits that follow it. So the ratio as the blow was set up is
            /// carried here and compared against the ratio after it: strictly above, then at or
            /// below, is a crossing; anything else is not.
            ///
            /// Set immediately before each of the two broadcasts and restored after, the same way
            /// _broadcastSubject is, so a passive that deals damage while answering one of them
            /// cannot leave a stale reading behind for the outer blow.
            ///
            /// A HEAL BACK OVER THE LINE RE-ARMS IT. Nothing is remembered between blows - the two
            /// readings belong to one blow - so a unit healed from 20% to 60% and knocked to 30%
            /// again has crossed twice and the watcher fires twice. That is the reading the text
            /// asks for: an ally who recovered and fell again fell again.
            /// </summary>
            private float _thresholdCrossedFrom = -1f;

            /// <summary>
            /// The bodies THIS FIRING has taken a status off, read by
            /// EffectCondition.SubjectLostStatusThisFiring.
            ///
            /// Cleared at the boundary of an ability a unit CHOSE and inherited by the passives it
            /// wakes, which is the same rule _lastTarget and _lastDamageDealt follow and for the
            /// same reason: a passive answering a Dispel is asking about the Dispel that woke it.
            /// "Applies Fault Line to enemies from whom it removed a defensive effect" is a
            /// question about one firing, and nothing in the condition vocabulary could ask it.
            /// </summary>
            private readonly List<int> _removedFromThisFiring = new List<int>();

            /// <summary>
            /// The damage a status entry would still have dealt had it not been removed:
            /// per-tick Magnitude times the rounds left on it. Zero for anything that is not a
            /// damage-over-time, which is most of the enum.
            ///
            /// Zero rather than some stand-in for a Slow or an AttackDown, because those have no
            /// damage to convert and inventing one would be a number nobody authored. An effect
            /// scaling off the removal of a non-DoT therefore resolves to nothing, which is the
            /// honest answer - and is visible, since a heal of zero emits no event.
            /// </summary>
            private static float RemainingDotDamage(StatusInstance entry, CombatUnit carrier)
            {
                if (entry == null || entry.Kind != StatusKind.Burn) return 0f;
                int rounds = entry.RoundsLeft > 0 ? entry.RoundsLeft : 0;
                // A holder-HP Burn carries no Magnitude, so the per-tick amount has to be asked of
                // the body it is sitting on - which is also the only reading that makes "the
                // damage the cleanse prevented" true of such an entry. Zero on every Burn in
                // shipped content, which takes the Magnitude branch it always took.
                if (entry.HolderHpTick > 0f && carrier != null)
                {
                    float pool = entry.HolderHpTickCurrent ? carrier.Hp : carrier.EffectiveMaxHp;
                    return pool * entry.HolderHpTick * rounds;
                }
                return entry.Magnitude * rounds;
            }
            private float _lastLedgerBankedStored;

            private float GetLastLedgerRelease(int unitId)
            {
                float v;
                return _lastLedgerRelease.TryGetValue(unitId, out v) ? v : 0f;
            }

            /// <summary>
            /// The last amount each unit MOVED from one body to another: a redirected or echoed
            /// share its own rule caused, a Shield it transferred, HP it transferred.
            ///
            /// PER CASTER FROM THE START, joining `_lastLedgerRelease` rather than the three
            /// single-field scalars beside it. A global would answer the wrong unit the moment two
            /// heroes on one line both move something in the same round - the protector's own
            /// reading overwritten by an unrelated transfer between the write and the read - and
            /// there is no ordering discipline that makes a shared field safe for a quantity every
            /// unit can produce independently.
            ///
            /// The key is the unit whose rule CAUSED the move, which is also the unit that can ask
            /// about it: a Covered split is keyed to the protector, a link's echo to the hero who
            /// created the link, a ShieldTransfer to the caster that moved the pool. Each write
            /// site says which unit it is keying and why.
            /// </summary>
            private readonly Dictionary<int, float> _lastMoved = new Dictionary<int, float>();

            private float GetLastMoved(int unitId)
            {
                float v;
                return _lastMoved.TryGetValue(unitId, out v) ? v : 0f;
            }

            /// <summary>
            /// Whether the caster's last moved amount is at least `fraction` of the subject's
            /// EffectiveMaxHp. GAP 7's condition half.
            ///
            /// Two operands neither of which any existing condition could reach: the amount had no
            /// store at all, and every other condition compares against an authored constant
            /// rather than against a fraction of some body's health. The SUBJECT supplies the max
            /// HP, so pointing the condition at BroadcastSubject asks about the ally that was
            /// warded rather than about the caster.
            ///
            /// False when nothing has moved, rather than true-by-vacuity at a threshold of zero:
            /// a clause gated on how much moved did not happen if nothing moved.
            /// </summary>
            private bool LastMovedAboveFraction(CombatUnit caster, CombatUnit subject, float fraction)
            {
                if (caster == null || subject == null) return false;
                float moved = GetLastMoved(caster.Id);
                if (moved <= 0f) return false;
                float maxHp = subject.EffectiveMaxHp;
                if (maxHp <= 0f) return false;
                return moved >= maxHp * fraction;
            }

            private void NoteMoved(int unitId, float amount)
            {
                if (unitId < 0) return;
                _lastMoved[unitId] = amount;
            }

            /// <summary>
            /// The per-target budget of the passive currently resolving, or null. Set by
            /// TriggerPassive around its ResolveAbility call and restored afterwards, so a passive
            /// that fires inside another passive does not inherit or clobber its budget.
            ///
            /// It lives here rather than as a ResolveAbility parameter because ResolveAbility is
            /// also the path every Basic and Active takes, and threading a passive-only concept
            /// through both would put it in the signature of everything.
            /// </summary>
            /// <summary>Which passive is mid-firing, for the per-subject firing conditions.</summary>
            private int _firedPassiveIndex = -1;
            private CombatUnit _firedPassiveOwner;

            private CombatUnit _perTargetOwner;
            private int _perTargetIndex;
            private int _perTargetLimit;
            private bool _perTargetPerRound;

            /// <summary>
            /// The bodies the firing now resolving has already charged its per-target budget for.
            /// Null except while a budgeted passive is running; see the budget block in
            /// ResolveAbility for why one firing pays once.
            /// </summary>
            private List<int> _perTargetAdmitted;

            /// <summary>
            /// Whether the passive firing now resolving has reached ApplyEffect for any effect on
            /// any target. Set in ResolveAbility at the one line every effect passes on its way in,
            /// read by TriggerPassive to decide whether a limit the firing charged was spent on a
            /// clause that happened or on a trigger whose every condition said no. Saved and
            /// restored around each firing, so a passive that fires inside another answers for
            /// itself and cannot make its host look as if it landed.
            /// </summary>
            private bool _firingLanded;

            /// <summary>StatusKinds that expired in the current ExpireStatuses sweep. Set before
            /// TriggerPassive(OnStatusExpired) so a passive with TriggerStatus can filter.</summary>
            private readonly List<StatusKind> _expiredKinds = new List<StatusKind>();

            /// <summary>
            /// Who applied the statuses that just expired on one carrier, distinct, for the life of
            /// one expiry sweep.
            ///
            /// An expiry broadcast is the one window with no single causer: a unit can shed two
            /// entries from two different heroes in the same sweep, and the broadcast goes out once
            /// per SIDE rather than once per applier. _broadcastSourceId therefore carries the
            /// applier only when there is exactly one, and a passive asking "did I apply it" is
            /// answered from this list instead - which is exact however many there were, and does
            /// not multiply the firings by re-broadcasting per applier.
            /// </summary>
            private readonly List<int> _expiredSourceIds = new List<int>();

            /// <summary>The status kind being applied right now, or None outside an application.
            /// Read by the OnStatusGained TriggerStatus filter, and a single value rather than a
            /// list because an application lands one kind at a time where an expiry sweep can
            /// retire several at once.</summary>
            private StatusKind _gainedKind = StatusKind.None;

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

            // ---------- linked targeting ----------

            /// <summary>
            /// Whether any StatusKind.Linked entry has been created in this battle.
            ///
            /// THE WHOLE REASON THE LINK WORK CANNOT MOVE AN EXISTING FINGERPRINT. Three of the
            /// four link mechanics sit inside the damage funnel, which every point of damage in the
            /// game passes through, and a funnel that walked a status list twice more per hit would
            /// be a cost paid by every battle ever resolved. This bool is set exactly once, by
            /// LinkUnits, and every link dispatch point tests it first - so a battle in which
            /// nothing creates a link pays one already-loaded bool comparison and returns.
            ///
            /// One-way on purpose: it is never cleared when the last link expires. A link that
            /// came and went leaves the guard open for the rest of that battle, which costs the
            /// list walks it was meant to save and cannot change an outcome. Clearing it would
            /// need a live count kept in step with expiry, dispel, death, MoveStatus and
            /// TransferStatus, and getting that wrong would silently disable a live link.
            /// </summary>
            private bool _anyLinks;

            /// <summary>
            /// Whether any unit in this battle carries a passive with PassiveDef.IncludeSelf.
            /// Computed once at build, the same shape and for the same reason as _anyLinks: a
            /// battle nobody authored it into pays one already-loaded bool comparison per
            /// broadcast and the excluded unit never enters the walk.
            /// </summary>
            private bool _anyIncludeSelf;

            /// <summary>
            /// The unit an ally-facing broadcast would ordinarily skip - the victim, the recipient,
            /// the carrier - or null outside such a dispatch. Read in TriggerPassive, which lets
            /// only an IncludeSelf passive through on that one body.
            ///
            /// Saved and restored around every broadcast, like every other window field here, so a
            /// dispatch nested inside another cannot inherit or destroy the outer one's exclusion.
            /// </summary>
            private CombatUnit _broadcastExcept;

            /// <summary>Holds OnAllyEnergyGained to one level: a passive that answers a gain by
            /// granting Energy would otherwise raise it again from inside itself. The same
            /// discipline _inDrainTrigger keeps for the other direction.</summary>
            private bool _inEnergyGainTrigger;

            /// <summary>Holds OnEnemyAppliedStatus to one level, for the same reason: a passive
            /// that answers a debuff attempt by attempting one is a loop, not a wrong number.
            /// </summary>
            private bool _inStatusAttemptTrigger;

            /// <summary>
            /// Whether the Damage effect now resolving was aimed at more than one body.
            ///
            /// ENGINE-GAPS group 3. Nothing on a resolving hit recorded how many bodies it was
            /// aimed at, so no mitigation and no condition could ask - and both halves of "the
            /// first AREA hit each round" and "when an ally is SINGLED OUT" had to be written as
            /// though every blow were the same shape.
            ///
            /// Set and restored around the per-target loop in ApplyEffect, so it names the effect
            /// in flight and nothing else. Outside one - a Burn tick, a Ledger release on expiry,
            /// a redirect delivered from the funnel - it reads false, which is correct: none of
            /// those was aimed at a line.
            ///
            /// SPLASH COUNTS. An effect that names one body and strikes its neighbours has hit
            /// more than one, which is what every sentence asking about this means.
            /// </summary>
            private bool _hitIsArea;

            /// <summary>
            /// The shape of the blow THE FUNNEL IS CARRYING, latched from _hitIsArea for the whole
            /// of one DealRawDamage and everything it wakes.
            ///
            /// The second field is not redundancy. _hitIsArea names the effect currently
            /// resolving, which is what the ward gates want - and a passive woken BY the funnel
            /// resolves effects of its own, so by the time its condition is asked _hitIsArea has
            /// been rewritten to describe the passive's own single-target effect rather than the
            /// blow the passive is answering. Every condition in this group is written from inside
            /// such a window, so they read the latch.
            ///
            /// Set and restored around the DealRawDamage call in ApplyDamageEffect, which is the
            /// one place a damage effect becomes a blow. A redirected share re-enters the funnel
            /// from inside that window and correctly inherits it: a share of a sweep is part of a
            /// sweep. A Burn tick, a Ledger release on expiry and a Mirrored Wound reach the funnel
            /// from outside any damage effect and read false, which is correct - none of them was
            /// aimed at a line.
            /// </summary>
            private bool _funnelHitIsArea;

            /// <summary>
            /// The last unit to damage somebody on each side, or -1 before anybody has.
            ///
            /// ENGINE-GAPS group 8. Per SIDE rather than per victim, which is the whole of what
            /// LastAttackerId could not say: a hero asking "which of them just hurt one of us"
            /// gets one answer rather than five partial ones, and the answer does not depend on
            /// which ally it happens to ask.
            ///
            /// Not cleared when the named unit dies. TargetMode.EnemyThatLastDamagedAlly tests the
            /// body it resolves to for life, the same way every other mode does, so a dead
            /// attacker resolves to nobody rather than to a corpse.
            /// </summary>
            private int _lastPlayerSideAttackerId = -1;
            private int _lastEnemySideAttackerId = -1;

            /// <summary>
            /// What the most recent hit actually took off a bar, or 0 at an ability boundary.
            /// EffectKind.EchoDamage's whole input - see that member for why it reads a realised
            /// number rather than recomputing one.
            /// </summary>
            private int _lastDamageDealt;

            /// <summary>
            /// The far end of the link whose dispatch is in flight, or null outside one. Resolves
            /// TargetMode.LinkedUnit during a link trigger, so a passive answering "my partner was
            /// hit" can aim an effect at the partner without re-deriving which body that is.
            ///
            /// The same window pattern _defeated and _thresholdUnit use, and null outside it for
            /// the same reason: "the unit I am linked to" has a referent inside a link dispatch
            /// that it does not have on an ordinary turn. Outside a dispatch the mode falls back to
            /// reading the caster's own entry - see ResolveTargets.
            /// </summary>
            private CombatUnit _linkPartner;

            /// <summary>
            /// How deep the current chain of link dispatches is. Bounds a passive that answers "my
            /// partner was hit" by hitting its other partner, which walks back into the dispatch
            /// that raised it. See DispatchLinkPassive for why a funnel-raised dispatch has no
            /// caller depth to thread and therefore needs its own.
            /// </summary>
            private int _linkDispatchDepth;

            public Sim(BattleRequest request)
            {
                _request = request;
                _cfg = request.Content != null ? request.Content.Balance : new BalanceConfig();
                _rng = new DeterministicRng(request.Seed);
                _result.Seed = request.Seed;
                _result.StageId = request.Stage != null ? request.Stage.Id : "";
            }

            // ------------------------------------------------------------------ setup

            /// <summary>
            /// Where the walk has got to. The battle used to live entirely inside one call and its
            /// position was the program counter; now it is this, so the fight can be left standing
            /// between two actions and picked up again.
            /// </summary>
            private enum Phase { NotStarted, RoundBegin, Turns, Done }
            private Phase _phase = Phase.NotStarted;

            /// <summary>Ids that took a MANUAL action this round and therefore skip their natural
            /// turn. Empty in every battle the game plays, cleared at every round boundary.</summary>
            private readonly HashSet<int> _manualActed = new HashSet<int>();

            public bool Finished { get { return _phase == Phase.Done; } }
            public BattleResult Result { get { return _result; } }
            public List<CombatUnit> Units { get { return _units; } }
            public int Round { get { return _round; } }
            public List<CombatUnit> Order { get { return _order; } }
            public int OrderIndex { get { return _orderIndex; } }

            /// <summary>
            /// ONE ENGINE, TWO DOORS. A finished fight is this, and a fight being driven by hand is
            /// the same three methods called one at a time. There is deliberately no second
            /// resolution path: anything that resembled the engine without being it would disagree
            /// with the game the first time either changed.
            /// </summary>
            public BattleResult Execute()
            {
                Begin();
                while (_phase != Phase.Done) StepNatural();
                return _result;
            }

            /// <summary>Everything Execute() used to do before its first round: build the bodies,
            /// snapshot them, open the record, fire the BattleStart passives.</summary>
            public void Begin()
            {
                if (_phase != Phase.NotStarted) return;

                BuildUnits();
                for (int i = 0; i < _units.Count; i++) _result.Units.Add(_units[i].ToSnapshot());

                Emit(BattleEventType.BattleStart, -1, -1);

                List<CombatUnit> startOrder = TurnOrder();
                for (int i = 0; i < startOrder.Count; i++) TriggerPassive(startOrder[i], PassiveTrigger.BattleStart, 0);

                _round = 0;
                _phase = Phase.RoundBegin;
            }

            /// <summary>
            /// Advances exactly one natural action - one unit's whole turn - and returns. Round
            /// boundaries are crossed on the way and cost no step of their own, which is why this
            /// loops: a round that opens with nobody able to act moves straight on to the next.
            /// </summary>
            public void StepNatural()
            {
                while (_phase != Phase.Done)
                {
                    if (_phase == Phase.RoundBegin)
                    {
                        _round++;
                        if (_round > _cfg.MaxRounds) { EndBattle(); return; }
                        OpenRound();
                        continue;
                    }

                    // The list is live and the cursor is a field, so an effect resolving mid-round
                    // can move the part of the order that has not happened yet - that is what
                    // TurnShift and SwapTurnOrder do. Count is re-read every iteration on purpose.
                    //
                    // Termination is structural. Both of those operations REORDER the tail; neither
                    // adds an entry, so the list cannot grow while it is being walked and the walk
                    // is bounded by _units.Count. Gale's kill-driven re-entry, which did splice new
                    // entries in and needed a cap to bound it, is gone.
                    while (_orderIndex < _order.Count)
                    {
                        CombatUnit unit = _order[_orderIndex];
                        if (!unit.Alive || _manualActed.Contains(unit.Id)) { _orderIndex++; continue; }
                        TakeTurn(unit);
                        _orderIndex++;
                        if (IsBattleOver()) { EndBattle(); return; }
                        return;
                    }
                    CloseRound();
                }
            }

            /// <summary>The top of a round, unchanged from the body of the old for-loop.</summary>
            private void OpenRound()
            {
                BattleEvent roundEvent = Emit(BattleEventType.RoundStart, -1, -1);
                roundEvent.Value = _round;

                for (int i = 0; i < _units.Count; i++) _units[i].BeginRound();

                // The defense-ignore budget is per round and lives on the simulation rather
                // than on a unit, because it is keyed by the modifier as well as the attacker.
                // Empty in every battle that authors none.
                _ignoreDefenseUses.Clear();
                _manualActed.Clear();

                // The round's own boundary, before anybody acts. Raised on every living unit
                // rather than broadcast to a side, because "at the start of each round" is a
                // statement about the clock and not about either team.
                for (int i = 0; i < _units.Count; i++)
                    if (_units[i].Alive) TriggerPassive(_units[i], PassiveTrigger.RoundStart, 0);

                _order = TurnOrder();
                _orderIndex = 0;
                ApplyCarriedDelays();
                _phase = Phase.Turns;
            }

            /// <summary>
            /// Spends every delay held from a previous round, before anybody in this one moves.
            ///
            /// _orderIndex is set to -1 for the duration, which is not a trick: ApplyTurnShift
            /// reads `_orderIndex + 1` as the first turn that has not happened, and at this point
            /// nothing in the round has. Left at 0 the unit that sorts first could never be moved,
            /// which is exactly the unit a delay bought last round is usually aimed at.
            ///
            /// The list is copied and emptied FIRST. A carried delay lands on a unit that is in
            /// this round's order, so it cannot re-carry itself; taking the copy anyway means a
            /// passive woken by one of these moves - OnTurnDelayed - can bank a delay for the
            /// round after without it being spent inside this loop.
            /// </summary>
            private void ApplyCarriedDelays()
            {
                if (_carriedDelays.Count == 0) return;
                List<CarriedDelay> due = new List<CarriedDelay>(_carriedDelays);
                _carriedDelays.Clear();

                int prevIndex = _orderIndex;
                _orderIndex = -1;
                for (int i = 0; i < due.Count; i++)
                {
                    CarriedDelay c = due[i];
                    ApplyTurnShift(c.Source, c.Target, c.Fraction, c.BanksAs, c.Cap, c.Rate, c.DebtDuration);
                }
                _orderIndex = prevIndex;
            }

            private void CloseRound()
            {
                _order = null;
                if (IsBattleOver()) { EndBattle(); return; }

                // ENGINE-GAPS group 14: the round's other boundary. RoundStart has had no mirror,
                // so "at round end" had to be authored as OnEnemyTurnEnd with a once-a-round
                // limit - which answers one enemy's turn ending rather than the round's own edge,
                // and picks whichever enemy happens to move first.
                //
                // Raised on every living unit rather than broadcast to a side, exactly as
                // RoundStart is and for the same reason: the clock belongs to neither team. Before
                // _round advances, so a passive answering it reads the round it is closing.
                for (int i = 0; i < _units.Count; i++)
                {
                    if (IsBattleOver()) break;
                    if (_units[i].Alive) TriggerPassive(_units[i], PassiveTrigger.RoundEnd, 0);
                }
                if (IsBattleOver()) { EndBattle(); return; }

                _phase = Phase.RoundBegin;
            }

            private void EndBattle()
            {
                if (_phase == Phase.Done) return;
                _order = null;
                _phase = Phase.Done;
                Finish();
            }

            /// <summary>
            /// Ends the round where it stands and opens the next one. Units that had not acted do
            /// not act: "end the round" means the clock moved on, and the engine has no separate
            /// end-of-round work to run - durations come off in ExpireStatuses at the close of each
            /// unit's own turn, and the per-round bookkeeping happens in BeginRound at the top of
            /// the next one, which this calls.
            /// </summary>
            public void EndRound()
            {
                if (_phase == Phase.NotStarted || _phase == Phase.Done) return;
                _order = null;
                if (IsBattleOver()) { EndBattle(); return; }
                _round++;
                if (_round > _cfg.MaxRounds) { EndBattle(); return; }
                OpenRound();
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

                        // The selected talent, resolved from the SNAPSHOT rather than from a
                        // HeroInstance, and then forgotten - the same three lines the player loop
                        // above runs, in the same order, for the same reason. Three lines rather
                        // than a shared helper because what differs is only where the selection
                        // came from, and a helper taking (unit, def, selection) would read as
                        // though the two sides were doing something more alike than they are: the
                        // player's is live save state, this is a frozen record of somebody's.
                        //
                        // Appended after the def's own passives here too, so a talent resolves last
                        // on a shared trigger on BOTH sides of the fight. That ordering is what
                        // makes a duel symmetrical: if the opponent's talent resolved before their
                        // def passives and the player's after, two identical lines could beat each
                        // other, which is the one outcome a friendly duel must never produce.
                        //
                        // Zero - every generated Proving roster, every save written before this
                        // field existed, and every snapshot whose owner has picked nothing -
                        // resolves to null through TalentAt and adds nothing at all, which is why
                        // no line that fought without talents yesterday fights differently today.
                        PassiveDef opponentTalent = def.TalentAt(entry.SelectedTalent);
                        if (opponentTalent != null)
                        {
                            unit.AddPassive(opponentTalent);
                            ApplyTalentOverrides(unit, opponentTalent);
                            ApplyTalentAppends(unit, opponentTalent);
                        }

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

                // One walk, once, over a roster that is at most ten bodies and two passives each.
                // Everything downstream reads the bool - see _anyIncludeSelf - so a battle nobody
                // authored IncludeSelf into never pays for the flag again.
                for (int i = 0; i < _units.Count && !_anyIncludeSelf; i++)
                {
                    CombatUnit u = _units[i];
                    for (int pi = 0; pi < u.Passives.Count; pi++)
                        if (u.Passives[pi] != null && u.Passives[pi].IncludeSelf) { _anyIncludeSelf = true; break; }
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
                    else if (PassiveSlotIndex(unit, ab) >= 0)
                    {
                        int pi = PassiveSlotIndex(unit, ab);
                        if (pi >= unit.Passives.Count) continue;
                        PassiveDef passive = unit.Passives[pi];

                        // Threshold is on the passive itself, not on an effect
                        string pf = ov.Field.ToLowerInvariant();
                        if (pf == "threshold")
                        {
                            PassiveDef clone = ClonePassive(passive);
                            clone.Threshold += ov.Delta;
                            unit.Passives[pi] = clone;
                            continue;
                        }

                        // THE FIRING BUDGET, which is also on the passive rather than on an effect.
                        // A talent that says "this can happen twice per enemy instead of once a
                        // round" is changing how often the passive runs, and nothing about the
                        // numbers inside it - there was no way to say that before.
                        //
                        // limitOff is a switch rather than an addition, and is the one field here
                        // that is: PassiveLimit is a category, and "OncePerRound plus one" means
                        // nothing. Any positive delta turns the limit off.
                        if (pf == "firespersubject")
                        {
                            PassiveDef clone = ClonePassive(passive);
                            clone.FiresPerSubject += (int)Math.Round(ov.Delta, MidpointRounding.AwayFromZero);
                            if (clone.FiresPerSubject < 0) clone.FiresPerSubject = 0;
                            unit.Passives[pi] = clone;
                            continue;
                        }
                        if (pf == "limitoff")
                        {
                            if (ov.Delta <= 0f) continue;
                            PassiveDef clone = ClonePassive(passive);
                            clone.Limit = PassiveLimit.Unlimited;
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
                        InsertAppend(aclone.Effects, ap);
                        if (ab == "basic") unit.Basic = aclone; else unit.Active = aclone;
                    }
                    else if (PassiveSlotIndex(unit, ab) >= 0)
                    {
                        int pi = PassiveSlotIndex(unit, ab);
                        if (pi >= unit.Passives.Count) continue;
                        PassiveDef pclone = ClonePassive(unit.Passives[pi]);
                        InsertAppend(pclone.Effects, ap);
                        unit.Passives[pi] = pclone;
                    }
                }
            }

            /// <summary>
            /// THE PASSIVE A TALENT NAMED, found by its JSON KEY rather than its position.
            ///
            /// A bare "passiveN" prefers the FLAT key and falls back to "skills.passiveN", which
            /// is unambiguous for every hero that authors only one of the two shapes - that is all
            /// but two of them, and those two address the nested half explicitly.
            ///
            /// Returns -1 rather than throwing on a name that matches nothing, because the
            /// surrounding loop already treats an unresolvable slot as an override content may
            /// have authored against a kit that does not have it - the same tolerance TalentAt
            /// applies to an out-of-range selection. What catches a name that matches nothing is
            /// the build, not the battle.
            /// </summary>
            private static int PassiveSlotIndex(CombatUnit unit, string ability)
            {
                if (unit == null || string.IsNullOrEmpty(ability)) return -1;
                for (int i = 0; i < unit.Passives.Count; i++)
                    if (unit.Passives[i].SlotKey == ability) return i;
                if (!ability.StartsWith("passive") || ability.StartsWith("passives.")) return -1;
                string nested = "skills." + ability;
                for (int i = 0; i < unit.Passives.Count; i++)
                    if (unit.Passives[i].SlotKey == nested) return i;
                return -1;
            }

            /// <summary>
            /// Puts an appended effect where the talent asked for it. -1 and anything out of range
            /// means the end, which is where every append written before AtIndex existed goes and
            /// is why adding the field moved nothing.
            /// </summary>
            private static void InsertAppend(List<EffectDef> effects, TalentAppendDef ap)
            {
                EffectDef e = ap.Effect.Clone();
                if (ap.AtIndex < 0 || ap.AtIndex > effects.Count) effects.Add(e);
                else effects.Insert(ap.AtIndex, e);
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
                p.FiresPerSubject = src.FiresPerSubject;
                p.ChargeOnLanding = src.ChargeOnLanding;
                p.BuffsOnly = src.BuffsOnly;
                p.SlotKey = src.SlotKey;
                p.TriggerStatus = src.TriggerStatus;
                p.TriggerSourceIsSelf = src.TriggerSourceIsSelf;
                p.TriggerSourceIsAlly = src.TriggerSourceIsAlly;
                p.IncludeSelf = src.IncludeSelf;
                // THE SAME DEFECT EffectDef.Clone() HAD, found by running the same audit against
                // this method while adding the two lines below it. ClonePassive rebuilds a passive
                // a talent has appended to or overridden, and it was dropping the three fields an
                // OnStackThreshold passive needs - the status it watches, the count it waits for
                // and which counter it reads - so a talent touching such a passive silently turned
                // its threshold off. 27 fields, 24 copied.
                p.StackThresholdStatus = src.StackThresholdStatus;
                p.StackThresholdCount = src.StackThresholdCount;
                p.CounterIndex = src.CounterIndex;
                p.TriggerAbilityId = src.TriggerAbilityId;
                p.TargetBanKind = src.TargetBanKind;
                p.TargetBanStatus = src.TargetBanStatus;
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
                // The talent lists, which no passive in content carries - a passive that is the
                // TARGET of a talent is not itself a talent. Copied anyway, because a clone that
                // is complete except where somebody has checked that it does not matter is how the
                // other three of these got into the state they were in.
                for (int i = 0; i < src.Overrides.Count; i++) p.Overrides.Add(src.Overrides[i]);
                for (int i = 0; i < src.Appends.Count; i++) p.Appends.Add(src.Appends[i]);
                return p;
            }

            /// <summary>The field list lives in TalentMath.TryApplyDelta, so the talent screen
            /// prints a change exactly when this applies one. A field it does not know is
            /// dropped here as it always was.</summary>
            private static void ApplyDelta(EffectDef e, string field, float delta)
            {
                TalentMath.TryApplyDelta(e, field, delta);
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
                TakeTurn(unit, ManualChoice.Natural, -1, false);
            }

            /// <summary>
            /// ONE TURN, whoever asked for it. A natural turn and a hand-driven one run the same
            /// body in the same order: the DoT tick, the turn-debt repayment, the TurnStart
            /// passives, the ability, the after-action passives, the turn-end broadcasts and the
            /// status expiry. `choice` only replaces the line that reads "would this unit cast?",
            /// and `forcedTargetId` only replaces the single-enemy pick made by this unit's own
            /// ability. Both are inert on the natural path.
            /// </summary>
            private void TakeTurn(CombatUnit unit, ManualChoice choice, int forcedTargetId, bool ignoreEnergy)
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
                bool usedActive;
                if (choice == ManualChoice.Natural)
                    usedActive = !silenced && unit.Active != null && unit.Energy >= unit.Active.EnergyCost;
                else if (choice == ManualChoice.Active)
                    usedActive = !silenced && unit.Active != null
                                 && (ignoreEnergy || unit.Energy >= unit.Active.EnergyCost);
                else
                    usedActive = false;

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

                    // DECLARED. The event above is the declaration and has been for the life of
                    // the project - it was already emitted here, before a single effect ran, which
                    // is why this pass adds no event to an existing battle. What it did not do was
                    // leave anything behind that could be interrupted.
                    //
                    // DECLARATION IS THE CASTER AND THE ABILITY, NOT THE TARGETS. Targeting in this
                    // engine is per-effect and happens as each effect runs - AllEnemies resolves at
                    // the second effect against whoever is standing by then, and half the roster
                    // depends on that. Snapshotting "the cast's target" here would mean moving
                    // targeting out of the effects and into the declaration, which is a different
                    // engine and would move every fingerprint in the game.
                    _castCaster = unit;
                    _castAbility = unit.Active;
                    _castCancelled = false;

                    // Raised BEFORE the cast resolves, so a passive that answers an enemy active
                    // acts on the board the caster is about to hit rather than on the wreckage
                    // afterwards. That is what "when an Anticipated enemy uses an active skill"
                    // asks for, and it is also the only ordering that lets an answer matter.
                    {
                        CombatUnit prevSubject = _broadcastSubject;
                        int prevActiveSrc = _broadcastSourceId;
                        _broadcastSubject = unit;
                        _broadcastSourceId = unit.Id;
                        BroadcastPassive(!unit.IsPlayer, PassiveTrigger.OnEnemyActive, 0, null);
                        _broadcastSourceId = prevActiveSrc;
                        _broadcastSubject = prevSubject;
                    }
                    // The declaration window, open to everybody. OnEnemyActive above is the same
                    // moment shown to one side; this is the side-neutral hook, and anything it
                    // raises resolves before the cast does.
                    // WITH THE CASTER AS THE SUBJECT. Raised without one, the window told every
                    // unit that a cast was happening and gave none of them a way to say whose -
                    // so a passive could not test the caster's statuses and could not aim at it.
                    {
                        CombatUnit prevCastSubject = _broadcastSubject;
                        int prevCastSource = _broadcastSourceId;
                        _broadcastSubject = unit;
                        _broadcastSourceId = unit.Id;
                        for (int ci = 0; ci < _units.Count && !IsBattleOver(); ci++)
                            if (_units[ci].Alive) TriggerPassive(_units[ci], PassiveTrigger.OnCastDeclared, 0);
                        _broadcastSourceId = prevCastSource;
                        _broadcastSubject = prevCastSubject;
                    }

                    if (!unit.Alive || IsBattleOver()) { ClearCast(); ExpireStatuses(unit); return; }

                    // AND THE CAST EITHER SURVIVES THE WINDOW OR IT DOES NOT.
                    //
                    // A CANCELLED CAST REFUNDS NOTHING. The energy was spent at declaration, three
                    // lines above, and it stays spent: that is what makes an interrupt worth a turn
                    // rather than a delay, and it is the reading EffectKind.Interrupt already has -
                    // it empties a bar and gives nothing back. Refunding would also be the only
                    // path in this engine that hands energy to a unit nobody granted it to.
                    if (_castCancelled)
                    {
                        BattleEvent lost = Emit(BattleEventType.CastCancelled, _castCancelSourceId, unit.Id);
                        lost.AbilityId = unit.Active.Id;
                        lost.AbilityName = unit.Active.Name;
                        ClearCast();
                        ExpireStatuses(unit);
                        return;
                    }

                    {
                        CombatUnit prevLT = _lastTarget;
                        OpenForcedPick(unit, forcedTargetId);
                        ResolveAbility(unit, unit.Active, 0);
                        CloseForcedPick();
                        _lastTarget = prevLT;
                    }
                    ClearCast();
                    // The far side of the caster's own Active, mirroring AfterBasicAttack. No
                    // shipped passive answers it, so this walks an empty listener set today.
                    if (unit.Alive && !IsBattleOver())
                    {
                        int prevCastSrc = _broadcastSourceId;
                        _broadcastSourceId = unit.Id;
                        TriggerPassive(unit, PassiveTrigger.AfterActiveCast, 0);
                        _broadcastSourceId = prevCastSrc;
                    }

                    // AND THE OTHER END OF THE CAST. OnEnemyActive is raised before it, on purpose,
                    // so a passive answering it acts on the board the caster is about to hit; this
                    // is for the clauses whose text says "after the skill resolves", and the two
                    // see the board either side of everything the active did. Guarded on the
                    // battle still running, so a cast that ended the fight does not raise a
                    // trigger into a finished board.
                    if (unit.Alive && !IsBattleOver())
                    {
                        CombatUnit prevResolved = _broadcastSubject;
                        int prevResolvedSrc = _broadcastSourceId;
                        _broadcastSubject = unit;
                        _broadcastSourceId = unit.Id;
                        BroadcastPassive(!unit.IsPlayer, PassiveTrigger.OnEnemyActiveResolved, 0, null);
                        _broadcastSourceId = prevResolvedSrc;
                        _broadcastSubject = prevResolved;
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
                    OpenForcedPick(unit, forcedTargetId);
                    ResolveAbility(unit, basic, 0);
                    CloseForcedPick();
                    // _lastTarget now holds the basic's final target (depth-0 does not restore).
                    // AfterBasicAttack passives can reference it via ConditionSubject.LastTarget.

                    if (unit.Alive)
                    {
                        // -1: a unit's own basic-attack refill has no granter, so it never redirects.
                        AddEnergy(unit, _cfg.EnergyPerBasicAttack, -1);
                        int prevBasicSrc = _broadcastSourceId;
                        _broadcastSourceId = unit.Id;
                        TriggerPassive(unit, PassiveTrigger.AfterBasicAttack, 0);
                        _broadcastSourceId = prevBasicSrc;
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
                    int prevEndSrc = _broadcastSourceId;
                    _broadcastSubject = unit;
                    _broadcastSourceId = unit.Id;
                    BroadcastPassive(unit.IsPlayer, PassiveTrigger.OnAllyTurnEnd, 0, unit);
                    if (unit.Alive && !IsBattleOver())
                        BroadcastPassive(!unit.IsPlayer, PassiveTrigger.OnEnemyTurnEnd, 0, null);
                    _broadcastSourceId = prevEndSrc;
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
                        // ENGINE-GAPS group 12: a tick written in the HOLDER'S health.
                        //
                        // Magnitude is a multiple of the applier's Attack resolved once at apply
                        // time, which is the right unit for "deals 40% of Attack each turn" and
                        // the wrong one for "loses 4% current HP". Read fresh every tick rather
                        // than stamped, because a percentage of a bar that is draining has to be
                        // re-read as it drains. Zero on every Burn in shipped content, and a zero
                        // takes the branch the engine always took.
                        float burnDmg = s.HolderHpTick > 0f
                            ? (s.HolderHpTickCurrent ? unit.Hp : unit.EffectiveMaxHp) * s.HolderHpTick
                            : s.Magnitude;
                        CombatUnit dotSource = s.SourceId >= 0 ? FindUnit(s.SourceId) : null;
                        burnDmg *= OutputModMultiplier(dotSource, unit, OutputModScope.DoTOnly);
                        int damage = (int)Math.Round(burnDmg, MidpointRounding.AwayFromZero);
                        if (damage < 1) damage = 1;
                        // Per-tick recording: combines DoT damage with RecordFact's recording in a
                        // single status. None on every Burn in shipped content, so no existing tick
                        // records anything new. Runs the same logic RecordFact uses, inline rather
                        // than through RecordFact because RecordFact expects an EffectDef and here
                        // the recording is stamped on the StatusInstance.
                        //
                        // BEFORE THE TICK LANDS, NOT AFTER, and the order is the clause rather than
                        // a convenience: "records the target's last positive effect BEFORE it takes
                        // damage" is only true if the board is read first. A Shield the tick breaks
                        // is a positive effect the target HAD when the Bleed found it, and reading
                        // afterwards would record whatever outlived the wound instead.
                        if (s.TickRecord != RecordKind.None && dotSource != null)
                        {
                            StatusInstance recEntry = null;
                            for (int ri = 0; ri < unit.Statuses.Count; ri++)
                            {
                                if (unit.Statuses[ri] != s) continue;
                                recEntry = s;
                                break;
                            }
                            if (recEntry != null)
                            {
                                switch (s.TickRecord)
                                {
                                    case RecordKind.LastPositiveStatus:
                                        for (int ri = unit.Statuses.Count - 1; ri >= 0; ri--)
                                        {
                                            StatusKind k = unit.Statuses[ri].Kind;
                                            if (k == StatusKind.Burn || StatusInfo.IsHarmful(k)) continue;
                                            recEntry.StoredStatus = k;
                                            break;
                                        }
                                        break;
                                    case RecordKind.LastActionType:
                                        if (unit.ActionsTaken > 0) recEntry.StoredAction = unit.LastActionWasActive ? 2 : 1;
                                        break;
                                    case RecordKind.HpFractionBand:
                                        // The expiry-side recorder writes the same band, so an
                                        // entry that records on a clock and one that records on
                                        // a hit keep the same two numbers.
                                        break;
                                    case RecordKind.MinHpRatio:
                                        if (recEntry.StoredMin < 0f || unit.HpRatio < recEntry.StoredMin)
                                            recEntry.StoredMin = unit.HpRatio;
                                        break;
                                }
                            }
                        }

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
                                int prevDotSrc = _broadcastSourceId;
                                _broadcastSourceId = burnSource.Id;
                                BroadcastPassive(burnSource.IsPlayer, PassiveTrigger.OnAppliedDoTTick, 0, null);
                                _broadcastSourceId = prevDotSrc;
                                _broadcastSubject = prevSubject;
                                if (!unit.Alive || IsBattleOver()) return;
                            }
                        }
                    }
                    else if (s.Kind == StatusKind.Regen)
                    {
                        // The same reading from the other direction. "Recovery heals 3% Max HP
                        // each turn" is a number about the ally carrying it, and resolving it as
                        // the Mender's Attack makes it right at exactly one level against exactly
                        // one bar.
                        float regen = s.HolderHpTick > 0f
                            ? (s.HolderHpTickCurrent ? unit.Hp : unit.EffectiveMaxHp) * s.HolderHpTick
                            : s.Magnitude;
                        int heal = (int)Math.Round(regen, MidpointRounding.AwayFromZero);
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
                bool sameSideExpired = false;
                _expiredKinds.Clear();
                _expiredSourceIds.Clear();
                for (int i = unit.Statuses.Count - 1; i >= 0; i--)
                {
                    // THE LIST CAN SHRINK UNDER THIS LOOP. A Ledger's auto-release below deals
                    // damage or pays a heal, and what that sets off can take statuses off this
                    // same unit - a ward the payout broke, a passive that cleanses - so an index
                    // walked down from the old Count can land past the new end. It used to throw
                    // there, and a mirror match of one ordinary deployed line reached it in 3 of
                    // 200 seeds. Skipping only the indices that no longer exist changes nothing
                    // about any fight that did not throw.
                    if (i >= unit.Statuses.Count) continue;
                    StatusInstance s = unit.Statuses[i];
                    s.RoundsLeft--;
                    if (s.RoundsLeft > 0) continue;

                    // Ledger auto-release: pay out Stored as damage to the holder before the entry
                    // is removed. Power is baked into StoredCap at apply-time, so the full Stored
                    // amount is the payout. Only fires when the Ledger duration genuinely expires;
                    // a cleanse, a dispel and a death each remove statuses by their own path and
                    // none of them should auto-release.
                    // A BAND OUTLIVES ITS ENTRY BY EXACTLY ONE MOMENT. The clause that pays it
                    // out fires on the expiry broadcast, which happens after this loop has removed
                    // the entry, so the two numbers and the body they were recorded against are
                    // captured here - the same arrangement _lastLedgerRelease has, and for the
                    // same reason. Keyed by the unit that recorded them, so two markers keep two
                    // bands.
                    if (s.StoredLowHp >= 0f || s.StoredHighHp >= 0f)
                    {
                        _lastBandLow[s.SourceId] = s.StoredLowHp;
                        _lastBandHigh[s.SourceId] = s.StoredHighHp;
                        _lastBandHp[s.SourceId] = unit.Hp;
                    }

                    if (s.Kind == StatusKind.Ledger && s.AutoRelease && s.Stored > 0f)
                    {
                        int payout = (int)Math.Round(s.Stored, MidpointRounding.AwayFromZero);
                        _lastLedgerRelease[s.SourceId] = s.Stored;
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
                                // Split heal: a fraction of the stored amount heals the applier on
                                // top of the damage to the holder. Zero on every Ledger in shipped
                                // content, so no existing auto-release gains a heal nobody authored.
                                if (s.AutoReleaseSplitHeal > 0f && s.SourceId >= 0)
                                {
                                    CombatUnit applier = FindUnit(s.SourceId);
                                    if (applier != null && applier.Alive)
                                    {
                                        int splitHeal = (int)Math.Round(GetLastLedgerRelease(s.SourceId) * s.AutoReleaseSplitHeal,
                                            MidpointRounding.AwayFromZero);
                                        if (splitHeal > 0)
                                            ApplyHealValue(s.SourceId, applier, splitHeal, "heal", true);
                                    }
                                }
                            }
                            if (!unit.Alive) return;
                        }
                    }

                    // The same shrink, met at the removal: the payout above is the only thing in
                    // this loop that can move the list, and when it has moved it past i the entry
                    // is found by identity rather than by a position that no longer exists.
                    if (i < unit.Statuses.Count) unit.Statuses.RemoveAt(i);
                    else
                    {
                        int at = unit.Statuses.IndexOf(s);
                        if (at >= 0) unit.Statuses.RemoveAt(at);
                    }

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
                    // Same-side expiry is tracked separately for OnAppliedAllyStatusExpired.
                    if (s.SourceId >= 0)
                    {
                        CombatUnit source = FindUnit(s.SourceId);
                        if (source != null)
                        {
                            if (!_expiredSourceIds.Contains(source.Id)) _expiredSourceIds.Add(source.Id);
                            if (source.IsPlayer != unit.IsPlayer)
                            {
                                if (source.IsPlayer) expiredFromPlayer = true;
                                else expiredFromEnemy = true;
                            }
                            else
                            {
                                sameSideExpired = true;
                            }
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
                    int prevExpSrc = _broadcastSourceId;
                    _broadcastSubject = unit;
                    // One applier means a name; several mean the question is answered from the
                    // list instead, and BroadcastSource resolves to nobody rather than to a guess.
                    _broadcastSourceId = _expiredSourceIds.Count == 1 ? _expiredSourceIds[0] : -1;
                    if (expiredFromPlayer)
                        BroadcastPassive(true, PassiveTrigger.OnAppliedStatusExpired, 0, null);
                    if (expiredFromEnemy)
                        BroadcastPassive(false, PassiveTrigger.OnAppliedStatusExpired, 0, null);
                    _broadcastSourceId = prevExpSrc;
                    _broadcastSubject = prevSubject;
                }

                // Same-side applier lifecycle: broadcast to the carrier's own side when a
                // status applied by a SAME-SIDE unit (including self) expired naturally.
                // Darian counts same-side shield expiries. No shipped passive uses
                // OnAppliedAllyStatusExpired, so the broadcast walks empty listener sets.
                if (sameSideExpired)
                {
                    CombatUnit prevSubject = _broadcastSubject;
                    int prevAllyExpSrc = _broadcastSourceId;
                    _broadcastSubject = unit;
                    _broadcastSourceId = _expiredSourceIds.Count == 1 ? _expiredSourceIds[0] : -1;
                    BroadcastPassive(unit.IsPlayer, PassiveTrigger.OnAppliedAllyStatusExpired, 0, null);
                    _broadcastSourceId = prevAllyExpSrc;
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
                                   : effect.ConditionOn == ConditionSubject.BroadcastSource ? FindUnit(_broadcastSourceId)
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

                    // Whether an earlier effect of THIS firing took a status off this body. Sim
                    // state for the same reason the three above are: the list belongs to the
                    // ability in flight, and nothing a caller could pass in knows about it.
                    case EffectCondition.SubjectLostStatusThisFiring:
                        first = _removedFromThisFiring.Contains(subject.Id);
                        break;

                    // Whether this body has a cast declared and not yet resolved. Sim state, so it
                    // is answered here rather than in the pure evaluator, the same way the
                    // turn-progress members are.
                    case EffectCondition.SubjectIsCasting:
                        first = _castCaster != null && subject == _castCaster && !_castCancelled;
                        break;

                    // How many times the passive now firing has fired about this subject before.
                    // Reads the live firing index, so it answers for the passive that asked rather
                    // than for whichever one happens to sit at the same slot on another unit.
                    case EffectCondition.PassiveFiredOnSubjectAtLeast:
                    case EffectCondition.PassiveFiredOnSubjectBelow:
                    {
                        int need = (int)Math.Round(effect.ConditionValue, MidpointRounding.AwayFromZero);
                        int fired = _firedPassiveOwner == null || _firedPassiveIndex < 0 || subject == null
                                    ? 0
                                    : _firedPassiveOwner.PassiveFiresOnSubject(_firedPassiveIndex, subject.Id);
                        first = effect.Condition == EffectCondition.PassiveFiredOnSubjectAtLeast
                                ? fired >= need : fired < need;
                        break;
                    }

                    // GAP 7's condition half. Sim-side rather than in the pure ConditionMet
                    // because it reads _lastMoved, which is per-battle state the static function
                    // cannot see - the same reason the turn-progress members live here.
                    case EffectCondition.LastMovedAboveHpFraction:
                        first = LastMovedAboveFraction(caster, subject, effect.ConditionValue);
                        break;

                    // GAP GROUP 3, both directions. Sim state for the same reason: the shape of
                    // the effect in flight belongs to the ability resolving, not to two units.
                    case EffectCondition.IncomingHitIsArea:
                        first = _funnelHitIsArea;
                        break;

                    case EffectCondition.IncomingHitIsSingleTarget:
                        first = !_funnelHitIsArea;
                        break;

                    // GAP GROUP 8. Sim-side with the two above rather than in the pure
                    // ConditionMet, because it reads the caster's side - which the static function
                    // has, but the whole family of "since my turn" readings already lives beside
                    // its siblings there and splitting them would be the trap this file keeps
                    // warning about.
                    case EffectCondition.SubjectDamagedMySideSinceMyTurn:
                    {
                        int when = subject.LastHitSideRound(caster != null && caster.IsPlayer);
                        first = caster != null && when > 0 && when >= caster.LastTurnRound;
                        break;
                    }

                    // GAP GROUP 1. Sim-side with its neighbours rather than in the pure
                    // ConditionMet, so the whole family of readings that need the battle's own
                    // memory stays in one place.
                    case EffectCondition.SubjectTookHitAboveMaxHpFraction:
                    {
                        float bar = subject.EffectiveMaxHp;
                        first = bar > 0f && subject.LastDamageTaken > 0
                                && subject.LastDamageTaken >= bar * effect.ConditionValue;
                        break;
                    }

                    default:
                    {
                        // Everything else, including a Defeated or BroadcastSubject asking an ordinary
                        // question, is answered by the pure function - with the resolved subject
                        // substituted in.
                        if (effect.ConditionOn == ConditionSubject.Defeated
                            || effect.ConditionOn == ConditionSubject.BroadcastSubject
                            || effect.ConditionOn == ConditionSubject.LastAttacker
                            || effect.ConditionOn == ConditionSubject.LastTarget
                            || effect.ConditionOn == ConditionSubject.BroadcastSource)
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
                gate2.ConditionCounterIndex = effect.ConditionCounterIndex2;
                gate2.Condition2 = EffectCondition.None; // prevent infinite recursion

                CombatUnit subject2 = gate2.ConditionOn == ConditionSubject.Caster ? caster
                                    : gate2.ConditionOn == ConditionSubject.Defeated ? _defeated
                                    : gate2.ConditionOn == ConditionSubject.BroadcastSubject ? _broadcastSubject
                                    : gate2.ConditionOn == ConditionSubject.LastAttacker ? ResolveLastAttacker()
                                    : gate2.ConditionOn == ConditionSubject.LastTarget ? _lastTarget
                                    : gate2.ConditionOn == ConditionSubject.BroadcastSource ? FindUnit(_broadcastSourceId)
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
                    case EffectCondition.LastMovedAboveHpFraction:
                        return LastMovedAboveFraction(caster, subject2, gate2.ConditionValue);
                    case EffectCondition.SubjectLostStatusThisFiring:
                        return _removedFromThisFiring.Contains(subject2.Id);
                }

                if (gate2.ConditionOn == ConditionSubject.Defeated
                    || gate2.ConditionOn == ConditionSubject.BroadcastSubject
                    || gate2.ConditionOn == ConditionSubject.LastAttacker
                    || gate2.ConditionOn == ConditionSubject.LastTarget
                    || gate2.ConditionOn == ConditionSubject.BroadcastSource)
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
                gate3.ConditionCounterIndex = effect.ConditionCounterIndex3;
                gate3.Condition2 = EffectCondition.None;
                gate3.Condition3 = EffectCondition.None;

                CombatUnit subject3 = gate3.ConditionOn == ConditionSubject.Caster ? caster
                                    : gate3.ConditionOn == ConditionSubject.Defeated ? _defeated
                                    : gate3.ConditionOn == ConditionSubject.BroadcastSubject ? _broadcastSubject
                                    : gate3.ConditionOn == ConditionSubject.LastAttacker ? ResolveLastAttacker()
                                    : gate3.ConditionOn == ConditionSubject.LastTarget ? _lastTarget
                                    : gate3.ConditionOn == ConditionSubject.BroadcastSource ? FindUnit(_broadcastSourceId)
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
                    case EffectCondition.LastMovedAboveHpFraction:
                        return LastMovedAboveFraction(caster, subject3, gate3.ConditionValue);
                    case EffectCondition.SubjectLostStatusThisFiring:
                        return _removedFromThisFiring.Contains(subject3.Id);
                }

                if (gate3.ConditionOn == ConditionSubject.Defeated
                    || gate3.ConditionOn == ConditionSubject.BroadcastSubject
                    || gate3.ConditionOn == ConditionSubject.LastAttacker
                    || gate3.ConditionOn == ConditionSubject.LastTarget
                    || gate3.ConditionOn == ConditionSubject.BroadcastSource)
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
                int prevRepaidSrc = _broadcastSourceId;
                _broadcastSourceId = unit.Id;
                BroadcastPassive(!unit.IsPlayer, PassiveTrigger.OnTurnDebtRepaid, 0, null);
                _broadcastSourceId = prevRepaidSrc;
                _broadcastSubject = prevSubject;
            }

            private void ResolveAbility(CombatUnit caster, AbilityDef ability, int depth)
            {
                if (ability == null) return;
                CombatUnit prevLastTarget = _lastTarget;
                // ZEROED FOR AN ABILITY A UNIT CHOSE, KEPT FOR ONE A PASSIVE IS ANSWERING, which is
                // the same split _lastDamageDealt makes four lines down and for the same reason.
                //
                // A Basic or an Active must not read a body a previous turn struck: "the enemy I
                // just hit" has to mean this ability's own target or nobody. A PASSIVE is the
                // opposite case. It is running because a blow just landed, that blow's target is
                // the whole of its context, and ConditionSubject.LastTarget was built to mean
                // exactly "the body the ability that woke me struck". Clearing it at the passive's
                // own boundary made every effect gated on that subject resolve to null and answer
                // false - the comment at the foot of this method used to say so, and the repair is
                // this one word.
                //
                // MEASURED, in the dark-talent arena: Elyra's Slow Growth fires 24 times and now
                // banks 24 Patience where it banked 0, and Edrin's Guardian of What Remains fires
                // 22 times and banks 22 Record where it banked 0. Both of their third talents wait
                // on four of those stacks, and Kiva's Severance Paid is gated the same way.
                if (depth == 0) _lastTarget = null;
                // Cleared on the same boundary and inherited the same way: "this firing removed a
                // status from that body" is a question about the ability a unit chose, and a
                // passive it woke is part of that firing rather than a firing of its own.
                if (depth == 0) _removedFromThisFiring.Clear();
                // Cleared and restored on exactly the same boundary _lastTarget uses, and for the
                // same reason: an EchoDamage authored with no damage ahead of it in its own ability
                // must read zero rather than a number from a previous turn. An echo that could pick
                // up a stale reading would be a different ability every time it fired.
                int prevLastDamage = _lastDamageDealt;
                // ZEROED FOR AN ABILITY A UNIT CHOSE, KEPT FOR ONE A BLOW TRIGGERED.
                //
                // The reason for clearing it is above and still holds for a Basic or an Active: an
                // echo authored with no damage ahead of it in its own ability must read zero rather
                // than a number from a previous turn. A PASSIVE is the opposite case. It is running
                // because damage just landed, that damage is the whole of its context, and
                // ScaleByLastDamage was built in the nine-gap pass to mean exactly "the blow that
                // just happened" - so zeroing it at the passive's own boundary made the one flag
                // that reads it always read nothing.
                //
                // Not hypothetical: rovan_deflected_conversion is a Shield sized at 20% of
                // scaleByLastDamage inside an OnEnemyDamagedAlly passive, and it has been banking
                // zero for its whole life on top of a condition that could not pass either.
                if (depth == 0) _lastDamageDealt = 0;
                // Cleared and restored on the same boundary, for the same reason: an effect that
                // reads "the body the swap moved" must read this ability's swap or nobody, never
                // a body a previous turn moved.
                CombatUnit prevSwapAdvanced = _swapAdvanced;
                CombatUnit prevSwapDelayed = _swapDelayed;
                _swapAdvanced = null;
                _swapDelayed = null;
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
                        // A FromZero effect enters with a count of zero, deliberately: "for each
                        // buff" against a bare body resolves to nothing, and the guard below would
                        // otherwise let the base amount through. Every effect that does not set it
                        // takes the guard it always took, since a factor of 1 changes nothing.
                        int casterStacks = PerStackCount(effect, caster);
                        if (effect.PerStack != 0f && (casterStacks > 0 || effect.PerStackFromZero))
                        {
                            EffectDef scaled = effect.Clone();
                            float factor = PerStackFactor(effect, caster);
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

                    // GAP 3: how many bodies this effect was aimed at, published for the length
                    // of its own resolution. Saved and restored rather than set and cleared,
                    // because a passive firing inside one effect resolves effects of its own and
                    // must not leave the outer one's answer rewritten.
                    //
                    // Counted BEFORE the condition filter, on purpose: "an area hit" is a fact
                    // about what the ability aimed at, and an AoE that only one enemy qualified
                    // for was still a sweep. The alternative would make the same cast area or
                    // single-target depending on the board it met.
                    bool prevArea = _hitIsArea;
                    _hitIsArea = targets.Count > 1;

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
                                              : effect.ConditionOn == ConditionSubject.BroadcastSource ? FindUnit(_broadcastSourceId)
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
                            int targetStacks = PerStackCount(effect, target);
                            if (targetStacks > 0 || effect.PerStackFromZero)
                            {
                                EffectDef scaled = effect.Clone();
                                float factor = PerStackFactor(effect, target);
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
                        // CHARGED ONCE PER FIRING, NOT ONCE PER EFFECT. It used to charge on
                        // every effect that landed, so a passive with four effects and a budget of
                        // one spent the whole budget on its first effect and dropped the rest in
                        // silence - Hale never removed the Quarry he had just punished, Rheia never
                        // took the Energy beside her hit, Zella never extended the Residue she had
                        // just drained. The field's own name is how many times the passive may FIRE
                        // against one target, and every description behind it says "this TRIGGERS
                        // once per target": one budget for the clause, not one per line of it.
                        //
                        // _perTargetAdmitted remembers which bodies THIS FIRING has already paid
                        // for. It is non-null only while a budgeted passive resolves, so a Basic,
                        // an Active and every unbudgeted passive still reach one null test.
                        //
                        // A per-EFFECT budget is still expressible and is a different mechanism: a
                        // status carrying UsesPerRound, read through StatusUseAvailable, which is
                        // spent by the effect that reads it. That is where "this one line at most
                        // once" belongs, and no shipped kit asks for it here.
                        // A SELF-AIMED EFFECT IS NOT A FIRING AGAINST A TARGET, so it neither pays
                        // the budget nor can be refused by it. The field's own name is how many
                        // times the passive may fire against one TARGET, and every description
                        // behind it names an enemy: "this triggers once per enemy each round".
                        // The caster is not one of those enemies.
                        //
                        // Not hypothetical. Rheia's Second Draft carries a budget of one per enemy
                        // per round and two of its own effects - the 10 Energy beside the hit, the
                        // Revision it spends - land on Rheia. The first firing of a round admitted
                        // her own id and spent her slot against herself, and every LATER firing
                        // that round, against a different enemy, found that slot gone and dropped
                        // her Energy, her Revision and the DefenseUp her second talent appends.
                        // A talent whose only effect is aimed at Self could not land at all.
                        if (_perTargetOwner != null && _perTargetLimit > 0
                            && target != _perTargetOwner
                            && !_perTargetAdmitted.Contains(target.Id))
                        {
                            if (_perTargetOwner.PassiveFiresAgainst(_perTargetIndex, target.Id,
                                                                    _perTargetPerRound) >= _perTargetLimit)
                                continue;
                            _perTargetOwner.NotePassiveFireAgainst(_perTargetIndex, target.Id);
                            _perTargetAdmitted.Add(target.Id);
                        }

                        _firingLanded = true;
                        ApplyEffect(caster, target, landed, ability, depth);
                        _lastTarget = target;
                        if (IsBattleOver())
                        {
                            _hitIsArea = prevArea;
                            if (depth > 0) { _lastTarget = prevLastTarget; _lastDamageDealt = prevLastDamage; }
                            return;
                        }
                    }
                    _hitIsArea = prevArea;

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
                        int ci = ResolveCounterIndex(effect.CounterIndex, effect.Status);
                        if (ci >= 2)
                        {
                            if (caster.CountOfCounter(ci) > 0)
                            {
                                if (effect.ConsumeStacks > 0)
                                    caster.SpendCounter(ci, effect.ConsumeStacks);
                                else
                                    caster.ClearCounter(ci);
                                BattleEvent spent = Emit(BattleEventType.StatusExpired, caster.Id, caster.Id);
                                spent.Status = StatusKind.Resolve;
                            }
                        }
                        else
                        {
                            StatusKind resolveKind = ci == 1 ? StatusKind.Resolve2 : StatusKind.Resolve;
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
                }
                // At depth > 0 (passive-triggered abilities), restore _lastTarget so the outer
                // ability's chain is not clobbered. At depth 0 (basic / active), leave _lastTarget
                // set: AfterBasicAttack passives need to see the basic's last target via
                // ConditionSubject.LastTarget, and the caller restores it once it is done.
                //
                // BOTH HALVES WORK NOW. TakeTurn leaves _lastTarget set across the AfterBasicAttack
                // broadcast, and the clear at the top of this method is gated on depth == 0, so the
                // wrapper AbilityDef a passive's effects run inside no longer wipes the field on
                // its way in. The measurements that repair moved are at the top of this method.
                if (depth > 0)
                {
                    _lastTarget = prevLastTarget;
                    _lastDamageDealt = prevLastDamage;
                }
                // Restored at EVERY depth, unlike the two above: a swap the outer ability made is
                // still the swap that ability is about, and an inner passive that made none must
                // not leave the outer one reading nobody.
                _swapAdvanced = prevSwapAdvanced;
                _swapDelayed = prevSwapDelayed;
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
                        float amount = ScaledTerm(effect, source) * effect.Power + effect.Flat
                                     + source.MaxHp * effect.SelfHpRatio + TargetHpTerm(effect, target);
                        ApplyHealValue(source.Id, target, (int)Math.Round(amount, MidpointRounding.AwayFromZero), "heal", true);
                        break;
                    }
                    case EffectKind.Shield:
                    {
                        if (!target.Alive) break;
                        float amount = ScaledTerm(effect, source) * effect.Power + effect.Flat
                                     + source.MaxHp * effect.SelfHpRatio + TargetHpTerm(effect, target);
                        int value = (int)Math.Round(amount, MidpointRounding.AwayFromZero);
                        if (value <= 0) break;
                        target.Shield += value;
                        target.AddOrRefresh(StatusKind.Shield, Math.Max(1, effect.Duration), 0f, source.Id);
                        target.LastShieldedRound = _round;
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
                            int prevShieldSrc = _broadcastSourceId;
                            _broadcastSubject = target;
                            _broadcastSourceId = source.Id;
                            BroadcastPassive(target.IsPlayer, PassiveTrigger.OnAllyShielded, depth, target);
                            _broadcastSourceId = prevShieldSrc;
                            _broadcastSubject = prevSubject;
                        }
                        break;
                    }
                    case EffectKind.ApplyStatus:
                    {
                        if (!target.Alive || effect.Status == StatusKind.None) break;

                        // ENGINE-GAPS group 9: the other side hears a debuff being ATTEMPTED.
                        //
                        // BEFORE THE LANDING ROLL, deliberately. A debuff that was resisted was
                        // still attempted, and "when a Flustered enemy attempts a debuff" is a
                        // sentence about the attempt; raising it after the roll would make the
                        // clause silently depend on the victim's Effect Resist. It also means the
                        // trigger draws no RNG of its own and cannot shift the stream.
                        //
                        // Harmful only, read through the same StatusInfo.IsHarmful that Cleanse and
                        // Dispel read, so "debuff" means one thing across the engine - a Mark, a
                        // Ledger, a Link and every buff are not attempts at one.
                        //
                        // The APPLIER is the subject, because every clause asking for this then
                        // wants to do something to it; the body being aimed at is the victim. Held
                        // to one level by a flag, since the natural answer to a debuff attempt is a
                        // debuff.
                        if (StatusInfo.IsHarmful(effect.Status) && !_inStatusAttemptTrigger)
                        {
                            _inStatusAttemptTrigger = true;
                            CombatUnit prevAsSubject = _broadcastSubject;
                            CombatUnit prevAsVictim = _broadcastVictim;
                            int prevAsSource = _broadcastSourceId;
                            _broadcastSubject = source;
                            _broadcastVictim = target;
                            _broadcastSourceId = source.Id;
                            BroadcastPassive(!source.IsPlayer, PassiveTrigger.OnEnemyAppliedStatus, depth, null);
                            _broadcastSourceId = prevAsSource;
                            _broadcastVictim = prevAsVictim;
                            _broadcastSubject = prevAsSubject;
                            _inStatusAttemptTrigger = false;
                            // A passive answering the attempt can kill the applier, the target, or
                            // end the fight. Each makes the application meaningless, so it does not
                            // happen - the same three re-checks every other pre-resolution hook in
                            // this engine applies.
                            if (!target.Alive || !source.Alive || IsBattleOver()) break;
                        }

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
                        //
                        // BENEFICIAL, NOT "NOT HARMFUL", and that is the repair here. IsHarmful
                        // answers false for the bookkeeping kinds as well as for the buffs, so this
                        // gate used to eat a Mark, a Ledger, a Linked and a Resolve on its way past
                        // - none of which is a buff, and none of which this clause was ever
                        // described as stopping. StatusInfo.IsInert names that set, which the
                        // HasAnyBeneficialStatus arm of ConditionMet had already had to exclude
                        // inline for the same reason.
                        //
                        // It is the whole of why Seraphine's Volume Closed could not run: its first
                        // effect lays BuffSuppression on a capped body and its second lays the
                        // MarkAlt that its own second half waits on, so the passive suppressed its
                        // own flag and the "when that restriction ends" branch - the Mark and
                        // Ledger it removes, and the 15 Energy it pays - was unreachable for as
                        // long as the clause existed. The same gate was also dropping the Ledger
                        // Contradiction Filed moves onto a suppressed body, which is what Cross
                        // Reference had to have to increment.
                        //
                        // THREE EFFECTS IN THE CONTENT APPLY BuffSuppression: Seraphine's Volume
                        // Closed, which was the only one when this gate was written, and Toren's
                        // Lockstep and No Entry, whose "Sealed targets cannot receive new buffs"
                        // is the same sentence. The harness confirms the mechanic is reached and
                        // names its users rather than asserting a single one.
                        if (!StatusInfo.IsHarmful(effect.Status) && !StatusInfo.IsInert(effect.Status)
                            && target.Has(StatusKind.BuffSuppression))
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
                        //
                        // StackOnReapply: the effect wants each application by the same caster to
                        // deepen a counter rather than refreshing. MaxStacks caps it (0 → 1).
                        if (effect.StackOnReapply)
                        {
                            int cap = effect.MaxStacks > 0 ? effect.MaxStacks : 1;
                            target.AddStack(effect.Status, duration, magnitude, source.Id, cap);
                        }
                        else
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

                        // The area-only flag on a ward, stamped the way Charges and the
                        // per-round budget above are. False on every Guard and Covered entry in
                        // shipped content, so no existing ward gains a shape test nobody authored.
                        if (effect.AreaHitsOnly)
                            for (int ao = 0; ao < target.Statuses.Count; ao++)
                            {
                                StatusInstance aw = target.Statuses[ao];
                                if (aw.Kind == effect.Status && aw.SourceId == source.Id) aw.AreaHitsOnly = true;
                            }

                        // The holder-HP tick, stamped the way Charges and the per-round budget
                        // above are. Zero on every Burn and Regen in shipped content, so no
                        // existing tick changes what it reads.
                        if (effect.HolderHpTick > 0f
                            && (effect.Status == StatusKind.Burn || effect.Status == StatusKind.Regen))
                            for (int hh = 0; hh < target.Statuses.Count; hh++)
                            {
                                StatusInstance hs = target.Statuses[hh];
                                if (hs.Kind != effect.Status || hs.SourceId != source.Id) continue;
                                hs.HolderHpTick = effect.HolderHpTick;
                                hs.HolderHpTickCurrent = effect.HolderHpTickCurrent;
                            }

                        // The skill scope of a DamageTakenUp entry, stamped the way Charges and
                        // the per-round budget above are. False on every entry in shipped content,
                        // so no existing modifier gains a scope nobody authored.
                        if (effect.Status == StatusKind.DamageTakenUp && effect.DamageTakenSkillOnly)
                            for (int sk = 0; sk < target.Statuses.Count; sk++)
                            {
                                StatusInstance ds = target.Statuses[sk];
                                if (ds.Kind == StatusKind.DamageTakenUp && ds.SourceId == source.Id)
                                    ds.SkillOnly = true;
                            }

                        // PreventExtend on an APPLICATION as well as on a transfer, so the flag
                        // means one thing wherever it is authored. Zella and Sylven both want it
                        // on the transferred copy; an entry that is born unextendable is the same
                        // fact stamped one step earlier, and a flag that worked on only one of the
                        // two effects that create entries would be a trap.
                        if (effect.PreventExtend)
                            for (int ni = 0; ni < target.Statuses.Count; ni++)
                            {
                                StatusInstance ns = target.Statuses[ni];
                                if (ns.Kind == effect.Status && ns.SourceId == source.Id) ns.NoExtend = true;
                            }

                        if (effect.Status == StatusKind.Ledger)
                            for (int li = 0; li < target.Statuses.Count; li++)
                            {
                                StatusInstance led = target.Statuses[li];
                                if (led.Kind != StatusKind.Ledger || led.SourceId != source.Id) continue;
                                if (effect.StoredCap > 0f)
                                {
                                    // THE TWO BASES ANSWER TO DIFFERENT BODIES, and the difference
                                    // is in what the authored texts say rather than in a taste for
                                    // symmetry. "Records up to 80% of my Attack" is a number about
                                    // the applier; the holder is an enemy whose Attack has nothing
                                    // to do with the size of the record. "Each ally can record up
                                    // to 8% of THEIR Max HP" is a number about the holder, and one
                                    // cap is being written onto five different bars.
                                    //
                                    // Resolved against `source` for both, Brenna's Stand Again gave
                                    // every ally a cap of 8% of BRENNA's Max HP - a Bulwark's bar
                                    // over a Marksman's ledger - which is neither the text nor a
                                    // defensible approximation of it.
                                    float basis = effect.StoredCapBasis == "maxHp"
                                        ? target.EffectiveMaxHp : source.EffectiveAttack;
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
                                led.AutoReleaseSplitHeal = effect.LedgerAutoReleaseSplitHeal;
                                led.FunnelSource = effect.LedgerFunnelSource;
                                led.FunnelConvert = effect.LedgerFunnelConvert;
                            }

                        // DoTRecord: stamp TickRecord on Burn entries so TickDamageOverTime can
                        // record a fact each tick. None on every Burn in shipped content, so no
                        // existing tick gains recording nobody authored.
                        if (effect.Status == StatusKind.Burn && effect.DoTRecord != RecordKind.None)
                            for (int bi = 0; bi < target.Statuses.Count; bi++)
                            {
                                StatusInstance bs = target.Statuses[bi];
                                if (bs.Kind == StatusKind.Burn && bs.SourceId == source.Id)
                                    bs.TickRecord = effect.DoTRecord;
                            }

                        // Disrupted: StoredCap carries the turn shift fraction, raw rather than
                        // Attack-scaled. Zero means no shift, so an effect that does not set it
                        // is a drain-only Disrupted.
                        // A Restricted entry carries its rule, the way a Disrupted one carries
                        // its drain. Stamped here so two restrictions from two sources are two
                        // entries rather than one field they fight over.
                        if (effect.Status == StatusKind.Restricted && effect.TargetBanKind != TargetBan.None)
                            for (int ri = 0; ri < target.Statuses.Count; ri++)
                            {
                                StatusInstance rs = target.Statuses[ri];
                                if (rs.Kind != StatusKind.Restricted || rs.SourceId != source.Id) continue;
                                rs.BanKind = effect.TargetBanKind;
                                rs.BanStatus = effect.TargetBanStatus;
                            }

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


                        // GAP 6: the CARRIER hears that it gained one. The missing corner of the
                        // status lifecycle - every other hook fires on the remover, the applier or
                        // the other side. Filtered by PassiveDef.TriggerStatus the same way
                        // OnStatusExpired is, so "whenever she becomes Untargetable" is this with
                        // TriggerStatus Stealth.
                        //
                        // Raised here and NOT in MoveStatus or TransferStatus: a status arriving
                        // by either of those is a status changing carrier, which has its own event
                        // (StatusSpread) and is a different thing happening to the unit.
                        {
                            int prevGainSource = _broadcastSourceId;
                            StatusKind prevGained = _gainedKind;
                            _broadcastSourceId = source.Id;
                            _gainedKind = effect.Status;
                            TriggerPassive(target, PassiveTrigger.OnStatusGained, depth);
                            _gainedKind = prevGained;
                            _broadcastSourceId = prevGainSource;
                        }

                        // A buff landing is news to the other side. Raised after the status is on
                        // the unit, so a passive answering it sees the board it is answering; and
                        // handed `depth`, so a buff-gained passive that itself buffs somebody
                        // cascades no further than MaxPassiveDepth already allows.
                        //
                        // RAISED FOR "NOT HARMFUL", which includes the bookkeeping kinds - a Mark, a
                        // Ledger, a Link - that the applier writes on an enemy. So every listener
                        // hears its own hero's marks as an enemy gaining a buff. A listener that
                        // says PassiveDef.BuffsOnly hears real buffs only; the rest are unchanged,
                        // because the balance line was tuned with them hearing both. ENGINE-GAPS
                        // pass 2 has the measurement.
                        if (!StatusInfo.IsHarmful(effect.Status))
                        {
                            CombatUnit prevSubject = _broadcastSubject;
                            _broadcastSubject = target;
                            StatusKind prevBuff = _broadcastBuff;
                            _broadcastBuff = effect.Status;
                            int prevBuffSrc = _broadcastSourceId;
                            _broadcastSourceId = source.Id;
                            BroadcastPassive(!target.IsPlayer, PassiveTrigger.OnEnemyBuffGained, depth, null);
                            _broadcastSourceId = prevBuffSrc;
                            _broadcastBuff = prevBuff;
                            _broadcastSubject = prevSubject;
                        }
                        break;
                    }
                    case EffectKind.Cleanse:
                    {
                        if (!target.Alive) break;
                        int removed = 0;
                        float cleansedValue = 0f;
                        for (int i = target.Statuses.Count - 1; i >= 0 && removed < effect.Hits; i--)
                        {
                            if (!StatusInfo.IsHarmful(target.Statuses[i].Kind)) continue;
                            BattleEvent e = Emit(BattleEventType.StatusExpired, source.Id, target.Id);
                            e.Status = target.Statuses[i].Kind;
                            cleansedValue += RemainingDotDamage(target.Statuses[i], target);
                            target.Statuses.RemoveAt(i);
                            removed++;
                        }
                        if (removed > 0) { _lastRemovedValue = cleansedValue; NoteRemovedFrom(target); FireRemoval(source, target, depth); }
                        break;
                    }
                    case EffectKind.Dispel:
                    {
                        if (!target.Alive) break;
                        int removed = 0;
                        float dispelledValue = 0f;
                        for (int i = target.Statuses.Count - 1; i >= 0 && removed < effect.Hits; i--)
                        {
                            if (StatusInfo.IsHarmful(target.Statuses[i].Kind)) continue;
                            // BENEFICIAL, NOT "NOT HARMFUL" - the same repair ApplyStatus's
                            // BuffSuppression gate and CombatUnit's beneficial count already carry,
                            // and this was the last rule in the engine reading "not harmful" as
                            // "buff". IsHarmful answers false for the bookkeeping kinds as well as
                            // for the buffs, so a Dispel walking newest first took a Mark, a
                            // MarkAlt, a Ledger, a Linked or a Resolve counter before it reached
                            // anything a player would call a buff. Eight authored Dispels say
                            // "removes one buff" and not one of them wants a bookmark: Seraphine's
                            // Contrary Evidence was eating the MarkAlt her own Volume Closed writes,
                            // which is the finding that sent this here.
                            if (StatusInfo.IsInert(target.Statuses[i].Kind)) continue;
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
                            dispelledValue += RemainingDotDamage(target.Statuses[i], target);
                            target.Statuses.RemoveAt(i);
                            removed++;
                        }
                        if (removed > 0) { _lastRemovedValue = dispelledValue; NoteRemovedFrom(target); FireRemoval(source, target, depth); }
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
                        if (!target.Alive) break;
                        if (target.Has(StatusKind.StatusGainSuppression)) break;
                        int ci = ResolveCounterIndex(effect.CounterIndex, effect.Status);
                        int cap = effect.MaxStacks > 0 ? effect.MaxStacks : 1;
                        if (ci >= 2)
                        {
                            // Generalized counter: pure int on CombatUnit, no StatusInstance.
                            int before = target.CountOfCounter(ci);
                            if (before >= cap) break;
                            int gained = Math.Min(effect.Hits, cap - before);
                            target.GainCounter(ci, gained, cap);
                            BattleEvent re = Emit(BattleEventType.StatusApplied, source.Id, target.Id);
                            re.Status = StatusKind.Resolve;
                            re.Value = target.CountOfCounter(ci);
                        }
                        else
                        {
                            // Index 0 (Resolve) or 1 (Resolve2): existing StatusInstance path.
                            StatusKind resolveKind = ci == 1 ? StatusKind.Resolve2 : StatusKind.Resolve;
                            for (int g = 0; g < effect.Hits; g++)
                            {
                                if (!target.AddStack(resolveKind, Math.Max(1, effect.Duration),
                                                     effect.Magnitude, source.Id, cap))
                                    break;
                                BattleEvent re = Emit(BattleEventType.StatusApplied, source.Id, target.Id);
                                re.Status = resolveKind;
                                re.Duration = Math.Max(1, effect.Duration);
                                re.Value = target.CountOf(resolveKind);
                            }
                        }
                        if (target.Alive) TriggerPassive(target, PassiveTrigger.OnStackThreshold, depth);
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
                        float strippedValue = 0f;
                        for (int i = target.Statuses.Count - 1; i >= 0 && removed < effect.Hits; i--)
                        {
                            if (target.Statuses[i].Kind != effect.Status) continue;
                            // Umbra's uncleansable rule is deliberately NOT consulted here. That
                            // rule is about a Cleanse trying to shrug a mark off its own carrier;
                            // this is an attacker naming a status and stripping it, which is the
                            // opposite direction and a different question.
                            BattleEvent e = Emit(BattleEventType.StatusExpired, source.Id, target.Id);
                            e.Status = target.Statuses[i].Kind;
                            strippedValue += RemainingDotDamage(target.Statuses[i], target);
                            target.Statuses.RemoveAt(i);
                            removed++;
                        }
                        // Raised once per effect rather than once per entry, so a removal that
                        // takes three stacks answers the trigger once. Handed `depth`, so a
                        // removal passive that itself removes something is bounded by the same
                        // MaxPassiveDepth as any other passive calling a passive.
                        if (removed > 0) { _lastRemovedValue = strippedValue; NoteRemovedFrom(target); FireRemoval(source, target, depth); }
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
                            // A bookkeeping entry is not a buff, and a move that treats it as one
                            // carries the applier's own claim off to another body: Tavia's Cut the
                            // Wire takes her Exposed Frequency off the body it names, and Vanno's
                            // misdirection can give his own Mark to his ally. EffectDef.BuffsOnly
                            // asks the predicate Dispel and the BuffSuppression gate already read;
                            // opt-in, for the reason the OnEnemyBuffGained raise gives.
                            if (!wantHarmful && effect.BuffsOnly && StatusInfo.IsInert(entry.Kind)) continue;
                            // Shield is skipped by construction rather than by a special case: the
                            // absorbing pool is a float on the unit and does not travel with the
                            // status row, so moving the entry alone would hand the recipient a
                            // marker with nothing behind it.
                            if (entry.Kind == StatusKind.Shield) continue;
                            // MoveDuration re-clocks the entry as it lands; 0 keeps its own, which
                            // is every move authored before this one.
                            MoveStatus(target, to, entry, true, effect.MoveDuration);
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

                            // The flipped entry's clock. Zero - every inversion before this pass -
                            // copies the buff's own RoundsLeft, which makes the same ability last
                            // six rounds against a fresh buff and one against a spent one.
                            int flipRounds = effect.InvertDuration > 0 ? effect.InvertDuration : entry.RoundsLeft;
                            target.AddOrRefresh(opposite, flipRounds, entry.Magnitude, source.Id);
                            BattleEvent e = Emit(BattleEventType.StatusApplied, source.Id, target.Id);
                            e.Status = opposite;
                            e.Duration = flipRounds;
                            e.Value = target.CountOf(opposite);
                            flipped++;
                        }
                        break;
                    }

                    case EffectKind.LedgerRelease:
                    {
                        if (!target.Alive) break;
                        float stored = target.LedgerTotalFrom(source.Id);
                        _lastLedgerRelease[source.Id] = stored;
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

                    case EffectKind.CancelCast:
                    {
                        // Reaches only the unit whose cast is in flight, and only while it is.
                        // Outside the window _castCaster is null and this does nothing, the same
                        // way RedirectLethal does nothing outside an OnAllyWouldDie.
                        //
                        // A CAST THAT LOSES ITS TARGET MID-FLIGHT IS NOT CANCELLED BY THAT. Only
                        // this effect cancels one. Targeting is per-effect and already defined for
                        // a body that is gone: the effect resolves against nobody and the rest of
                        // the ability carries on, which is what every AoE that kills something
                        // with its first effect has always done.
                        if (_castCaster == null || target != _castCaster) break;
                        if (_castCancelled) break;
                        _castCancelled = true;
                        _castCancelSourceId = source != null ? source.Id : -1;
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
                            // An entry stamped NoExtend refuses this and refuses it SILENTLY -
                            // it is not counted as extended, so the event reports what actually
                            // moved. False on every entry in every battle before this pass.
                            if (entry.NoExtend) continue;
                            // The ceiling "up to 3 rounds" names. Zero - every extension before
                            // this pass - is uncapped. An entry already at or above the ceiling is
                            // left exactly where it is rather than shortened, and is not counted
                            // as extended, so the event reports what actually moved. That is the
                            // same silence NoExtend one line up keeps, for the same reason.
                            if (effect.ExtendCap > 0)
                            {
                                if (entry.RoundsLeft >= effect.ExtendCap) continue;
                                int room = effect.ExtendCap - entry.RoundsLeft;
                                entry.RoundsLeft += added > room ? room : added;
                            }
                            else entry.RoundsLeft += added;
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
                        // GAP GROUP 1: through ScaledTerm, which is how Damage, Heal and Shield
                        // already read their Attack term. HpTransfer was the one amount-bearing
                        // effect that read EffectiveAttack directly, so the whole ScaleBy family
                        // was invisible to it - and "redirects 25% of THAT hit" is scaleByLastDamage
                        // on a transfer and nothing else. An effect that sets none of them gets
                        // EffectiveAttack back, which is the expression that stood here.
                        int amount = (int)Math.Round(
                            ScaledTerm(effect, source) * effect.Power + effect.Flat
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
                        // GAP 7, keyed to the caster for the same reason ShieldTransfer's is.
                        NoteMoved(source.Id, amount);
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
                        // The authored clock on the pool that just arrived. A RE-CLOCK rather than
                        // an AddOrRefresh, because AddOrRefresh keeps the longer of the two and the
                        // entry created one line up is 99 rounds - so an authored 2 would be
                        // silently discarded and an ExtendStatus written beside it would be
                        // nominal. Zero, which is every transfer before this pass, leaves whatever
                        // clock the recipient had.
                        if (effect.ShieldTransferDuration > 0)
                            for (int shi = 0; shi < recipient.Statuses.Count; shi++)
                                if (recipient.Statuses[shi].Kind == StatusKind.Shield)
                                    recipient.Statuses[shi].RoundsLeft = effect.ShieldTransferDuration;
                        BattleEvent se = Emit(BattleEventType.ShieldGain, source.Id, recipient.Id);
                        se.Value = (int)Math.Round(moved, MidpointRounding.AwayFromZero);
                        // GAP 7: keyed to the CASTER - the unit whose effect moved the pool - and
                        // not to the donor or the recipient, which for a TransferFromTarget are
                        // two other bodies entirely. "Is the Shield I just moved at least 10% of
                        // that ally's Max HP" is a question the mover asks.
                        NoteMoved(source.Id, moved);
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
                                int prevLcSrc = _broadcastSourceId;
                                _broadcastSubject = target;
                                _broadcastSourceId = source.Id;
                                BroadcastPassive(source.IsPlayer, PassiveTrigger.OnLedgerCapped, 0, null);
                                _broadcastSourceId = prevLcSrc;
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
                        //
                        // A CORPSE INSIDE THE DEFEAT WINDOW IS A LEGAL SOURCE, and refusing it made
                        // this effect useless for the only thing shipped content uses it for.
                        // `!target.Alive` turned away every transfer off a defeated body, which is
                        // both of them: Zella's `Nothing Stays Settled` and Sylven's `Persistent
                        // Edge` name TargetMode.DefeatedUnit and nothing else. Hers has been in the
                        // game since batch five and has never once moved a status.
                        //
                        // The pre-clear window exists precisely so "a status survives its carrier's
                        // death" can be authored - the comment in the death branch says so in as
                        // many words, and lists six abilities that want it - and TransferStatus is
                        // the effect those abilities use. The engine had the window and the door
                        // into it was bolted.
                        //
                        // Narrow on purpose: the only dead unit accepted is the one the window is
                        // currently about. Outside it _defeated is null, so every other transfer
                        // refuses a corpse exactly as before.
                        if (effect.Status == StatusKind.None) break;
                        if (!target.Alive && target != _defeated) break;
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

                        // TransferDuration re-clocks the entry as it lands, exactly as MoveDuration
                        // does for MoveBuffs and MoveDebuffs. Zero keeps its own remaining rounds,
                        // which is what every transfer authored before this did.
                        //
                        // THE SIBLING THE MOVE-DURATION PASS MISSED. TransferStatus is the
                        // single-kind version of the same operation, and Zella's shipped passive
                        // says "transfer its Residue to the highest-HP enemy FOR 2 ROUNDS" - a
                        // clause that has been absent from her content since she was authored,
                        // because there was no field to put it in and the omission was silent.
                        if (effect.TransferDuration > 0) moving.RoundsLeft = effect.TransferDuration;

                        // And PreventExtend stamps the entry so ExtendStatus will not touch it.
                        // Set on the way in rather than tested at the extend site against some
                        // property of the mover, because "this particular entry cannot be
                        // extended" is a fact about the entry and has to travel with it.
                        if (effect.PreventExtend) moving.NoExtend = true;

                        // And the one resize a move is allowed, because an author asked for it in
                        // as many words. Everything else about the entry still crosses untouched.
                        if (effect.TransferMagnitude > 0f) moving.Magnitude *= effect.TransferMagnitude;

                        dest.Statuses.Add(moving);
                        BattleEvent applied = Emit(BattleEventType.StatusApplied, source.Id, dest.Id);
                        applied.Status = moving.Kind;
                        applied.Duration = moving.RoundsLeft;
                        applied.Value = dest.CountOf(moving.Kind);
                        break;
                    }

                    case EffectKind.LinkUnits:
                    {
                        // Binds two units. The whole of the linked-targeting primitive's creation
                        // half: everything else in this pass READS a link, and this is the only
                        // thing that makes one.
                        if (!target.Alive) break;
                        List<CombatUnit> partners = ResolveTargets(source, effect.SecondaryTarget, target,
                                                                   effect.TargetStatus);
                        if (partners.Count == 0) break;
                        CombatUnit partner = partners[0];
                        // A unit cannot be linked to itself. The transfer would deliver a share of
                        // a hit back to the body that took it, which is not a redirect and not an
                        // echo - it is a hit resolving twice.
                        if (!partner.Alive || partner == target) break;

                        int duration = Math.Max(1, effect.Duration);
                        StampLink(source, target, partner, effect, duration);
                        // MUTUAL: the reverse entry, so both bodies carry a link naming the other.
                        // Two entries rather than one bidirectional one - see EffectDef.LinkMutual
                        // for why breaking one half has to leave the other standing.
                        if (effect.LinkMutual) StampLink(source, partner, target, effect, duration);
                        break;
                    }

                    case EffectKind.EchoDamage:
                    {
                        // Repeats a fraction of what the previous damage in this ability actually
                        // took off a bar. Paid through DealRawDamage, not ApplyDamageEffect: the
                        // number has already been through Defense, crit, variance and the aspect
                        // chart once, and sending it round again would apply every one of them
                        // twice. That is what makes this an echo rather than a second attack.
                        //
                        // Zero when nothing has been damaged yet in this ability, because
                        // _lastDamageDealt is cleared at every ability boundary - so an echo
                        // authored ahead of its own damage does nothing rather than repeating
                        // something from a previous turn.
                        if (!target.Alive) break;
                        if (_lastDamageDealt <= 0) break;
                        int echoed = (int)Math.Round(_lastDamageDealt * effect.Power, MidpointRounding.AwayFromZero);
                        if (echoed <= 0) break;
                        string echoVfx = ability != null ? ability.Vfx : "impact";
                        string echoName = ability != null ? ability.Name : "";

                        // AN ECHO DOES NOT BECOME THE THING LATER ECHOES READ. The funnel writes
                        // _lastDamageDealt for every hit it carries, so without this an echo aimed
                        // at AllEnemies would read its own previous landing for the second enemy
                        // and compound down the line - the third body taking a fraction of a
                        // fraction of a fraction. Every target of one echo repeats the SAME blow,
                        // which is what "echoes 40% of its damage" says.
                        int spoken = _lastDamageDealt;
                        DealRawDamage(source.Id, target, echoed, false, echoVfx, StatusKind.None, 1f, echoName);
                        _lastDamageDealt = spoken;

                        // AND THE CASTER HEARS IT. An EchoDamage struck a second body and raised
                        // nothing, so a hero could cause one and never learn it had happened -
                        // which left "whenever his attack triggers against a secondary target
                        // through Redirecting Shot or Change the Course" hearing only the half
                        // that echoes through a link. Same trigger for both routes, because that
                        // sentence is one passive.
                        //
                        // Raised after the hit lands, on the caster, with the struck body as the
                        // subject so an effect can aim at it. `depth` is threaded, so a passive
                        // that answers an echo by echoing again is bounded by MaxPassiveDepth the
                        // way any passive calling a passive is.
                        NoteMoved(source.Id, echoed);
                        if (source.Alive && !IsBattleOver())
                        {
                            CombatUnit prevSubjectEd = _broadcastSubject;
                            CombatUnit prevVictimEd = _broadcastVictim;
                            int prevSourceEd = _broadcastSourceId;
                            _broadcastSubject = target;
                            _broadcastVictim = target;
                            _broadcastSourceId = source.Id;
                            TriggerPassive(source, PassiveTrigger.OnEchoFired, depth);
                            _broadcastSourceId = prevSourceEd;
                            _broadcastVictim = prevVictimEd;
                            _broadcastSubject = prevSubjectEd;
                        }
                        break;
                    }

                    case EffectKind.LedgerDeposit:
                    {
                        // Deposits an amount into existing Ledger entries on the target from the
                        // caster. Computes amount, finds Ledger entries, adds to Stored.
                        if (!target.Alive) break;
                        float baseAmt = effect.Power * source.Attack + effect.Flat
                                      + effect.SelfHpRatio * source.EffectiveMaxHp;
                        if (effect.ScaleByLastRedirect) baseAmt = _lastRedirectAmount;
                        if (effect.ScaleByLastRelease)
                        {
                            float rel;
                            baseAmt = _lastLedgerRelease.TryGetValue(source.Id, out rel) ? rel * effect.Power : 0f;
                        }
                        if (effect.ScaleByLastBanked) baseAmt = _lastLedgerBankedStored * effect.Power;
                        // The removed DoT's remaining damage, which this list did not have and
                        // ScaledTerm has had since the removed-value repair. Sylven's Scar Record
                        // is "the Bleed's remaining damage" banked into a Ledger, and without this
                        // the deposit falls back to Attack and stores a number from a different
                        // question.
                        //
                        // NOT routed through ScaledTerm, which would be the tidier fix: the inline
                        // list multiplies by Power for release and banked and does NOT for redirect,
                        // and brenna_deep_resolve is a shipped LedgerDeposit authoring
                        // scaleByLastRedirect. Sharing the resolver would multiply her deposit by
                        // her Power and change a live passive, so the divergence is left standing
                        // and named rather than fixed underneath her.
                        if (effect.ScaleByLastRemoval) baseAmt = _lastRemovedValue * effect.Power;
                        if (baseAmt <= 0f) break;
                        for (int li = 0; li < target.Statuses.Count; li++)
                        {
                            StatusInstance led = target.Statuses[li];
                            if (led.Kind != StatusKind.Ledger) continue;
                            if (led.SourceId != source.Id) continue;
                            float room = led.StoredCap > 0f ? led.StoredCap - led.Stored : float.MaxValue;
                            if (room <= 0f) continue;
                            float deposited = baseAmt;
                            if (deposited > room) deposited = room;
                            bool wasBelow = led.StoredCap > 0f && led.Stored < led.StoredCap;
                            led.Stored += deposited;
                            if (wasBelow && led.StoredCap > 0f && led.Stored >= led.StoredCap)
                            {
                                CombatUnit prevSubject = _broadcastSubject;
                                int prevLcSrc = _broadcastSourceId;
                                _broadcastSubject = target;
                                _broadcastSourceId = source.Id;
                                BroadcastPassive(source.IsPlayer, PassiveTrigger.OnLedgerCapped, 0, null);
                                _broadcastSourceId = prevLcSrc;
                                _broadcastSubject = prevSubject;
                            }
                            if (source.IsPlayer == target.IsPlayer)
                            {
                                _lastLedgerBankedStored = led.Stored;
                                CombatUnit prevSubjectB = _broadcastSubject;
                                _broadcastSubject = target;
                                int prevBcSrc3365 = _broadcastSourceId;
                                _broadcastSourceId = led.SourceId;
                                BroadcastPassive(target.IsPlayer, PassiveTrigger.OnAllyLedgerBanked, 0, target);
                                _broadcastSourceId = prevBcSrc3365;
                                _broadcastSubject = prevSubjectB;
                            }
                        }
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
                // -2. THE LINK HEARS THAT ITS PARTNER WAS AIMED AT, before anything about the blow
                // has been decided - before the miss check, before Block, before the crit roll. A
                // hit that goes on to miss or to be Blocked was still aimed, which is the whole
                // distinction between this trigger and OnLinkedUnitHit below the funnel.
                //
                // This is the one targeting-side hook in the engine and it is deliberately narrow:
                // it fires for a DAMAGE effect only, never for a heal, a buff or a cleanse naming a
                // target. ResolveTargets is the common point all of those pass through, and firing
                // a passive from inside it means firing between targets being chosen and the effect
                // resolving, where a passive that kills the chosen target leaves the caller holding
                // a corpse it has already committed to acting on. The same argument
                // OnEnemyDamagedAlly settled the same way, and "an attack aimed at" is what the
                // abilities asking for this actually say.
                //
                // Gated on _anyLinks, so a battle with no link pays one bool test per damage
                // effect and nothing else - which is what keeps every existing fingerprint where
                // it is, since this sits on the hottest path in the simulation.
                if (_anyLinks)
                {
                    DispatchLinkPassive(target, source.Id, PassiveTrigger.OnLinkedUnitTargeted, depth);
                    // A passive answering the aim can kill the target, or the caster, or end the
                    // fight. Each of those makes the blow that was about to land meaningless, so it
                    // does not land - the same three tests the Hits loop in ApplyEffect applies
                    // between repeats.
                    if (!target.Alive || !source.Alive || IsBattleOver()) return;
                }

                // GAP 6: the body being aimed at hears it too. The owner-centric half of the
                // trigger above, at the same moment and with the same three re-checks after it.
                //
                // NOT gated on _anyLinks - it has nothing to do with links, and gating it on one
                // would make "the next time that enemy targets me" silently depend on whether
                // somebody else in the fight happened to create a binding. It is instead gated on
                // a passive existing to hear it: TriggerPassive walks the target's own passive
                // list, which for most units is one or two entries and for an enemy usually none,
                // so the cost on the hot path is a list walk that finds no match. That is the same
                // cost TriggerPassive(OnDamaged) already pays one layer down.
                {
                    int prevSourceId = _broadcastSourceId;
                    _broadcastSourceId = source.Id;
                    TriggerPassive(target, PassiveTrigger.OnTargeted, depth);
                    _broadcastSourceId = prevSourceId;
                    if (!target.Alive || !source.Alive || IsBattleOver()) return;
                }

                // -1. Accuracy / Evasion - a miss check before anything else. EvasionUp on the
                // defender and AccuracyDown on the attacker both contribute to the miss chance.
                // Both default to 0, so hitChance is 1.0f and Chance(1f) returns true on line 41
                // of DeterministicRng without calling NextFloat — zero RNG draws consumed when
                // neither status exists, which is every battle until content authors one.
                float hitChance = 1f - target.SumMagnitude(StatusKind.EvasionUp)
                                     - source.SumMagnitude(StatusKind.AccuracyDown);
                if (!_rng.Chance(hitChance))
                {
                    {
                        int prevDodgeSrc = _broadcastSourceId;
                        _broadcastSourceId = source.Id;
                        TriggerPassive(target, PassiveTrigger.OnDodge, depth);
                        _broadcastSourceId = prevDodgeSrc;
                    }
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
                    {
                        int prevBlockSrc = _broadcastSourceId;
                        _broadcastSourceId = source.Id;
                        TriggerPassive(target, PassiveTrigger.OnBlock, depth);
                        _broadcastSourceId = prevBlockSrc;
                    }
                    return;
                }

                // 1. Base. The target-HP term joins here, before mitigation, so "12% of its
                // missing HP" is reduced by Defense like any other authored damage rather than
                // arriving as a second, unmitigated payload.
                float attackTerm = ScaledTerm(effect, source);
                float raw = attackTerm * effect.Power + effect.Flat + TargetHpTerm(effect, target)
                          + BandTerm(effect, source);
                // "Up to 130% of Attack" - a ceiling written in the CASTER's currency, where
                // MaxHpCap and MissingHpCap both ceiling against the TARGET's health. Applied to
                // the assembled amount rather than to one term, because that is what "up to"
                // means in the sentences asking for it, and before the output modifier for the
                // same reason: the cap is on what the ability is worth, not on what a stack of
                // Absence then does to it.
                if (effect.AttackCap > 0f)
                {
                    float ceiling = source.EffectiveAttack * effect.AttackCap;
                    if (raw > ceiling) raw = ceiling;
                }
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

                // 5a. A fraction of that Defense this hit does not see. It scales the INPUT, where
                // Armor Break below scales the ratio's OUTPUT - "ignores 10% of its Defense" and
                // "Armor Break 10%" are different sentences and this engine keeps them different
                // terms. Gated and budgeted by the modifier itself, because the only thing that
                // can carry it onto an existing blow is an override, and an override cannot add a
                // condition. Skipped entirely when the field is zero, which it is everywhere in
                // shipped content, so no existing hit changes.
                if (effect.IgnoreDefense > 0f && defense > 0f && SpendIgnoreDefense(effect, source, target))
                {
                    float ignore = effect.IgnoreDefense;
                    if (ignore > 1f) ignore = 1f;
                    defense *= 1f - ignore;
                }
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
                float rawBeforeDR = raw;
                float damageReduction = target.EffectiveDamageReduction;
                if (damageReduction > 0f)
                {
                    if (damageReduction > 1f) damageReduction = 1f;
                    raw *= 1f - damageReduction;
                }

                // Funnel banking: DamageMitigation entries bank the amount DamageReduction
                // prevented. No shipped content sets FunnelSource.DamageMitigation, so the loop
                // body is unreachable in every existing battle.
                {
                    float mitigated = rawBeforeDR - raw;
                    if (mitigated > 0f)
                    {
                        for (int fi = 0; fi < target.Statuses.Count; fi++)
                        {
                            StatusInstance led = target.Statuses[fi];
                            if (led.Kind != StatusKind.Ledger) continue;
                            if (led.FunnelSource != FunnelSource.DamageMitigation) continue;
                            if (led.Magnitude <= 0f) continue;
                            if (led.BankLimit > 0 && led.BankUsesThisRound >= led.BankLimit) continue;
                            float room = led.StoredCap > 0f ? led.StoredCap - led.Stored : float.MaxValue;
                            if (room <= 0f) continue;
                            float banked = mitigated * led.Magnitude;
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
                                    int prevBcSrc3621 = _broadcastSourceId;
                                    _broadcastSourceId = ledSource.Id;
                                    BroadcastPassive(ledSource.IsPlayer, PassiveTrigger.OnLedgerCapped, 0, null);
                                    _broadcastSourceId = prevBcSrc3621;
                                    _broadcastSubject = prevSubject;
                                }
                            }
                            CombatUnit bankSrcDR = FindUnit(led.SourceId);
                            if (bankSrcDR != null && bankSrcDR.IsPlayer == target.IsPlayer)
                            {
                                _lastLedgerBankedStored = led.Stored;
                                CombatUnit prevSubjectBDR = _broadcastSubject;
                                _broadcastSubject = target;
                                int prevBcSrc3631 = _broadcastSourceId;
                                _broadcastSourceId = led.SourceId;
                                BroadcastPassive(target.IsPlayer, PassiveTrigger.OnAllyLedgerBanked, 0, target);
                                _broadcastSourceId = prevBcSrc3631;
                                _broadcastSubject = prevSubjectBDR;
                            }
                        }
                    }
                }

                // 7.5. Vulnerable - SOURCE-KEYED, which is what makes it different from
                // DefenseDown. DefenseDown changes the input to the mitigation ratio and softens
                // the target against the whole field; this reads only the entries THIS attacker
                // applied, so "takes 12% more damage from Nadine" is 12% more from Nadine and
                // normal from everybody else. Exactly zero when the attacker has applied none,
                // and x + 0 is exact, so it cannot move a battle nobody authored it into.
                float vulnerable = target.VulnerableTo(source.Id);
                if (vulnerable != 0f) raw *= 1f + vulnerable;

                // 7.6. Blunted - THE MIRROR OF THE LINE ABOVE, and the only other per-source
                // entry in the game. "Takes 10% less from it" and "it deals 15% less damage to
                // Jory" are sentences about ONE attacker; DamageReductionUp, which both currently
                // use, softens the holder against the whole enemy line.
                //
                // Asked of the ATTACKER and keyed to the body being protected, which is the
                // opposite bookkeeping from Vulnerable one line up and the only direction a hero
                // can author - see StatusKind.Blunted. Zero when the attacker carries none, and
                // `raw * (1f - 0f)` is exact.
                //
                // Clamped at 0.9 rather than 1.0 so no depth of stacking makes an attacker
                // harmless - the same discipline the damageReduction step keeps one block up.
                float resistant = source.BluntedAgainst(target.Id);
                if (resistant != 0f)
                {
                    if (resistant > 0.9f) resistant = 0.9f;
                    raw *= 1f - resistant;
                }

                // 7.7. DamageTakenUp - the UNIVERSAL half Vulnerable never had, and the one thing
                // "takes 15% more Skill Damage" actually says. Skill-scoped per entry, read
                // against the same ability.Kind division step 4 makes, so the two cannot disagree
                // about what a skill is. Exactly zero when nothing carries the kind.
                float damageTakenUp = target.DamageTakenUpAgainst(ability.Kind != AbilityKind.BasicAttack);
                if (damageTakenUp != 0f) raw *= 1f + damageTakenUp;

                // 8. Variance, round, clamp
                raw *= 1f + _rng.Spread(_cfg.DamageVariance);

                int damage = (int)Math.Round(raw, MidpointRounding.AwayFromZero);
                if (damage < _cfg.MinDamage) damage = (int)_cfg.MinDamage;

                // PreviousTargetId first, and only when the target actually changes: an ability
                // that hits the same body twice in a row must not shuffle its own history and
                // report itself as having switched.
                bool changedTarget = source.LastTargetId >= 0 && source.LastTargetId != target.Id;
                if (source.LastTargetId != target.Id) source.PreviousTargetId = source.LastTargetId;
                source.LastTargetId = target.Id;
                // AND SOMEBODY CAN HEAR IT NOW. The line above is the engine's own definition of a
                // target having changed and was until now the only thing that knew. The switcher
                // is the source and the body it has turned on is the subject; a unit's first blow
                // is excluded, because acquiring a target is not changing one.
                if (changedTarget)
                {
                    CombatUnit prevTcSubject = _broadcastSubject;
                    int prevTcSource = _broadcastSourceId;
                    _broadcastSubject = target;
                    _broadcastSourceId = source.Id;
                    BroadcastPassive(!source.IsPlayer, PassiveTrigger.OnEnemyChangedTarget, 0, null);
                    _broadcastSourceId = prevTcSource;
                    _broadcastSubject = prevTcSubject;
                }
                string vfx = ability != null ? ability.Vfx : "impact";
                string abilityName = ability != null ? ability.Name : "";
                // GAP GROUP 3: the blow's shape, latched for the whole of the funnel and everything
                // it wakes. See _funnelHitIsArea for why the effect-level field cannot serve.
                bool prevFunnelArea = _funnelHitIsArea;
                _funnelHitIsArea = _hitIsArea;
                DealRawDamage(source.Id, target, damage, critical, vfx, StatusKind.None, aspectFactor, abilityName);
                _funnelHitIsArea = prevFunnelArea;

                if (critical && source.Alive)
                {
                    int prevCritSrc = _broadcastSourceId;
                    _broadcastSourceId = source.Id;
                    TriggerPassive(source, PassiveTrigger.OnCriticalHit, depth);
                    _broadcastSourceId = prevCritSrc;
                }
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

            /// <summary>
            /// The term an effect's Power multiplies: the caster's Attack, or whichever runtime
            /// amount a ScaleBy* flag redirects it to.
            ///
            /// ONE RESOLVER, SHARED BY DAMAGE, HEAL AND SHIELD, and that is a BEHAVIOUR CHANGE
            /// rather than an addition - the one in this pass. The ScaleBy family was read in
            /// ApplyDamageEffect and in LedgerDeposit and nowhere else, so a Heal or a Shield
            /// authoring one of these flags computed off plain Attack and the flag did nothing at
            /// all. That is not hypothetical: `darian_shared_burden`, a shipped talent, authors
            /// `"kind": "Shield"` with `scaleByLastRelease` and has been silently warding for half
            /// of Darian's Attack instead of half of what his Ledger just paid out. Routing all
            /// three through here makes that talent do what its own JSON says.
            ///
            /// It cannot move a fingerprint: a talent is inert until selected, `SelectedTalent`
            /// defaults to 0, and no profile in the sweep selects one. It does change what that
            /// talent does for a player who picks it, which is why it is called out here and in
            /// the branch notes rather than folded in quietly.
            ///
            /// Exactly `source.EffectiveAttack` when no flag is set, which is every effect in
            /// shipped content outside that one talent, so the substitution above is bit-identical
            /// for all of them.
            /// </summary>
            /// <summary>
            /// What the band an expiring record left behind is worth to this effect.
            ///
            ///   ScaleByBandGap  - how far the carrier stood ABOVE the lowest value it recorded,
            ///                     floored at zero. "Damage equal to the difference", and the floor
            ///                     is what makes "if its HP is higher than the lowest" a fact about
            ///                     the number rather than a condition that has to be written twice.
            ///   ScaleByBandHigh - the highest value recorded, times `magnitude` where one is set.
            ///
            /// Read from the capture rather than from the entry, because every clause that asks for
            /// a band asks at the moment the record ENDS. Returns an exact zero when neither flag
            /// is set, so every damage number in every existing battle is bit-identical.
            /// </summary>
            private float BandTerm(EffectDef effect, CombatUnit source)
            {
                if (!effect.ScaleByBandGap && !effect.ScaleByBandHigh) return 0f;
                if (source == null) return 0f;
                float term = 0f;
                if (effect.ScaleByBandGap)
                {
                    float low, hp;
                    if (_lastBandLow.TryGetValue(source.Id, out low) && low >= 0f
                        && _lastBandHp.TryGetValue(source.Id, out hp))
                    {
                        float gap = hp - low;
                        if (gap > 0f) term += gap;
                    }
                }
                if (effect.ScaleByBandHigh)
                {
                    float high;
                    if (_lastBandHigh.TryGetValue(source.Id, out high) && high >= 0f)
                        term += high * (effect.Magnitude > 0f ? effect.Magnitude : 1f);
                }
                return term;
            }

            private readonly Dictionary<int, float> _lastBandLow = new Dictionary<int, float>();
            private readonly Dictionary<int, float> _lastBandHigh = new Dictionary<int, float>();
            private readonly Dictionary<int, float> _lastBandHp = new Dictionary<int, float>();

            /// <summary>
            /// Whether this blow may use its defense-ignore, and spends the round's budget if so.
            /// Asked only when a modifier exists, so a battle with none never enters it.
            /// </summary>
            private bool SpendIgnoreDefense(EffectDef effect, CombatUnit source, CombatUnit target)
            {
                if (source == null) return false;
                if (effect.IgnoreDefenseVsStatus != StatusKind.None
                    && (target == null || !target.HasMarkFrom(effect.IgnoreDefenseVsStatus, source.Id)))
                    return false;
                if (effect.IgnoreDefenseUsesPerRound <= 0) return true;

                // Counted on the ATTACKER and keyed by the effect's own gate, so two different
                // modifiers on one unit do not share a budget. Cleared with the round.
                string key = source.Id + ":" + (int)effect.IgnoreDefenseVsStatus;
                int used;
                _ignoreDefenseUses.TryGetValue(key, out used);
                if (used >= effect.IgnoreDefenseUsesPerRound) return false;
                _ignoreDefenseUses[key] = used + 1;
                return true;
            }

            private readonly Dictionary<string, int> _ignoreDefenseUses = new Dictionary<string, int>();

            /// <summary>
            /// The three removal sites also record WHO they took it from, for
            /// EffectCondition.SubjectLostStatusThisFiring.
            ///
            /// Once per body rather than once per status: the question the condition asks is
            /// whether this firing stripped that enemy, and an ability that took two buffs off one
            /// body did not strip it twice as far as any authored clause is concerned.
            /// </summary>
            private void NoteRemovedFrom(CombatUnit target)
            {
                if (target == null) return;
                if (!_removedFromThisFiring.Contains(target.Id)) _removedFromThisFiring.Add(target.Id);
            }

            /// <summary>
            /// The three removal sites raise one trigger, name one causer - and now name the body
            /// it happened TO.
            ///
            /// ENGINE-GAPS group 18. The trigger fires on the REMOVER and published no subject, so
            /// a passive on it could pay the remover and could not reach the ally it had just
            /// cleansed. "When Lio removes a debuff, THAT ALLY recovers 5% missing HP" had to move
            /// the heal into the ability, where SubjectLostStatusThisFiring can see it, and pay the
            /// Energy to the lowest-HP ally - the body the cleanse happened to by construction
            /// rather than by statement.
            ///
            /// Null on no call site: every removal knows whose status it took.
            /// </summary>
            private void FireRemoval(CombatUnit source, CombatUnit subject, int depth)
            {
                if (source == null) return;
                int prev = _broadcastSourceId;
                CombatUnit prevSubject = _broadcastSubject;
                CombatUnit prevVictim = _broadcastVictim;
                _broadcastSourceId = source.Id;
                _broadcastSubject = subject;
                _broadcastVictim = subject;
                TriggerPassive(source, PassiveTrigger.OnStatusRemoved, depth);
                _broadcastVictim = prevVictim;
                _broadcastSubject = prevSubject;
                _broadcastSourceId = prev;
            }

            /// <summary>
            /// Both delay sites raise one trigger, name the unit that caused the delay - and now
            /// name the unit that was delayed.
            ///
            /// ENGINE-GAPS group 18, the same absence from the other end. "The FIRST ENEMY DELAYED
            /// each round gains 10% Defense Down" is a sentence about one body and the trigger
            /// could only reach the causer, so the shipped clause lands on the whole enemy line.
            ///
            /// For a SwapTurnOrder the subject is the body that went backwards, which is the one
            /// the word "delayed" names; _swapAdvanced and _swapDelayed already record both halves
            /// for the two target modes that read them.
            /// </summary>
            private void FireTurnDelayed(CombatUnit source, CombatUnit delayed)
            {
                int prev = _broadcastSourceId;
                CombatUnit prevSubject = _broadcastSubject;
                CombatUnit prevVictim = _broadcastVictim;
                _broadcastSourceId = source.Id;
                _broadcastSubject = delayed;
                _broadcastVictim = delayed;
                TriggerPassive(source, PassiveTrigger.OnTurnDelayed, 0);
                _broadcastVictim = prevVictim;
                _broadcastSubject = prevSubject;
                _broadcastSourceId = prev;
            }

            private float ScaledTerm(EffectDef effect, CombatUnit source)
            {
                if (effect.ScaleByLastRelease) return GetLastLedgerRelease(source.Id);
                if (effect.ScaleByLastRedirect) return _lastRedirectAmount;
                if (effect.ScaleByLastRemoval) return _lastRemovedValue;
                if (effect.ScaleByLastBanked) return _lastLedgerBankedStored;
                if (effect.ScaleByLastMoved) return GetLastMoved(source.Id);
                if (effect.ScaleByLastDamage) return _lastDamageDealt;
                // The odd one: a FRACTION rather than an amount, so it is turned into one here
                // against the caster's own Attack. "35% of the recorded amount as a percentage of
                // Attack" is Power 0.35 on top of this.
                if (effect.ScaleByLastShift)
                {
                    float frac;
                    return _lastTurnShift.TryGetValue(source.Id, out frac)
                        ? frac * source.EffectiveAttack : 0f;
                }
                return source.EffectiveAttack;
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

            /// <summary>
            /// Moves `share` of a hit off `target` and onto `destination`, through this same
            /// funnel, bounded to one hop. Returns false when the caller must stop processing the
            /// original hit - the whole of it moved, or the target did not survive the recursion.
            ///
            /// THE ONE REDIRECT PATH, called twice: once by Covered, where the destination is
            /// whoever applied the ward, and once by StatusKind.Linked with LinkTransfer.Redirect,
            /// where it is the far end of the link. The two mechanics differ only in how they name
            /// a body, which is why they could not justify two implementations of the same split.
            ///
            /// Extracted verbatim from the Covered block it used to be written inline in. Six
            /// effects in heroes.json apply Covered, so this is the only part of the linked-
            /// targeting work that shipped content reaches, and it had to move without changing:
            /// same AwayFromZero rounding, same clamp of share to 1.0, same refusal of a zero or
            /// negative move, same two early exits in the same order.
            /// </summary>
            private bool RedirectShare(int sourceId, CombatUnit target, CombatUnit destination, float share,
                                       ref int damage, string vfx, StatusKind dotKind,
                                       float aspectFactor, string abilityName, int ruleOwnerId)
            {
                if (destination == null || !destination.Alive || destination == target) return true;
                if (share <= 0f) return true;
                if (share > 1f) share = 1f;
                int moved = (int)Math.Round(damage * share, MidpointRounding.AwayFromZero);
                if (moved <= 0) return true;

                damage -= moved;
                _inRedirect = true;
                DealRawDamage(sourceId, destination, moved, false, vfx, dotKind, aspectFactor, abilityName);
                _inRedirect = false;

                // GAP 7: the share is an amount that MOVED, so both units that can reasonably ask
                // about it get it. The rule's owner is the protector or the link's creator - the
                // unit whose decision moved it - and the destination is the body that received it,
                // which for a ward is the same unit and for a link need not be. Keyed per unit, so
                // a second protector soaking a different blow in the same round cannot answer for
                // this one.
                NoteMoved(ruleOwnerId, moved);
                if (destination.Id != ruleOwnerId) NoteMoved(destination.Id, moved);

                // GAP 6: the destination can now tell a redirected share from an ordinary hit.
                // Raised after the share has landed, with the body it was moved OFF as the subject
                // and the original attacker as the source - neither of which OnDamaged carries.
                // Guarded on the destination still being alive, since a share can kill a protector.
                if (destination.Alive && !IsBattleOver())
                {
                    CombatUnit prevSubject = _broadcastSubject;
                    CombatUnit prevVictim = _broadcastVictim;
                    int prevSourceId = _broadcastSourceId;
                    _broadcastSubject = target;
                    _broadcastVictim = target;
                    _broadcastSourceId = sourceId;
                    TriggerPassive(destination, PassiveTrigger.OnRedirectReceived, 0);
                    _broadcastSourceId = prevSourceId;
                    _broadcastVictim = prevVictim;
                    _broadcastSubject = prevSubject;
                }

                // AND THE UNIT WHOSE RULE MOVED IT hears that it moved. A redirect has three
                // parties - the body it came off, the body it landed on, and the hero whose ward or
                // link did the moving - and only the second had a hook. That left the whole
                // "when I redirect damage" family with nothing to listen to, and the roster has
                // five of them: Marrow, Nereia twice, Linnet, Ruma, Toren.
                //
                // A SECOND TRIGGER RATHER THAN A SUBJECT ON THE FIRST, because the owner needs to
                // FIRE, and a subject only lets somebody already firing ask a question. The owner
                // is usually not the destination and often not on the same side as either of the
                // other two.
                //
                // Skipped when the owner IS the destination, which is every Covered ward: a
                // protector that caught its own ward's share has already been told by
                // OnRedirectReceived, and firing both on one unit would make a hero that listens to
                // each answer one event twice.
                //
                // `_lastMoved[ruleOwnerId]` was set just above, so ScaleByLastMoved on the owner
                // reads the share - which is what "damage equal to 80% of the redirected damage"
                // needs and had no way to say.
                CombatUnit ruleOwner = FindUnit(ruleOwnerId);
                if (ruleOwner != null && ruleOwner.Alive && ruleOwner != destination && !IsBattleOver())
                {
                    CombatUnit prevSubject = _broadcastSubject;
                    CombatUnit prevVictim = _broadcastVictim;
                    int prevSourceId = _broadcastSourceId;
                    _broadcastSubject = target;
                    _broadcastVictim = target;
                    _broadcastSourceId = sourceId;
                    TriggerPassive(ruleOwner, PassiveTrigger.OnRedirectSent, 0);
                    _broadcastSourceId = prevSourceId;
                    _broadcastVictim = prevVictim;
                    _broadcastSubject = prevSubject;
                }

                if (damage <= 0) return false;
                if (!target.Alive) return false;
                return true;
            }

            /// <summary>
            /// The deepest live link ENTRY on `holder` whose transfer mode matches and which is
            /// willing to fire for damage from `dealerId`. Null when there is none.
            ///
            /// Returns the entry rather than a bare fraction, which is what gaps 1, 2 and 3 needed:
            /// the caller has to spend a charge on it, read its flat LinkAmount and honour its
            /// from-caster filter, and a float could carry none of those.
            ///
            /// Deepest wins and the shares do NOT sum, which is CoverFraction's rule adopted
            /// deliberately rather than by accident: two links each taking a share could otherwise
            /// between them move more than the whole hit, and a holder that took nothing from a
            /// blow aimed squarely at it is not something any of the four descriptions asks for.
            /// "Deepest" ranks on Magnitude alone even now that an entry can also carry a flat
            /// amount, because a flat echo and a share echo are not on one scale and inventing a
            /// combined ranking would be a number nobody authored.
            ///
            /// GAP 3: an entry with LinkFromCasterOnly is skipped unless the damage was dealt by
            /// the unit that created it. The test is against the entry's own SourceId, the same
            /// comparison StatusInstance.BankFromCaster makes for a Ledger.
            /// </summary>
            private static StatusInstance FindLink(CombatUnit holder, LinkTransfer mode, int dealerId)
            {
                StatusInstance best = null;
                for (int i = 0; i < holder.Statuses.Count; i++)
                {
                    StatusInstance link = holder.Statuses[i];
                    if (link.Kind != StatusKind.Linked) continue;
                    if (link.LinkTransfer != mode) continue;
                    if (link.LinkedToId < 0) continue;
                    if (link.Magnitude <= 0f && link.LinkAmount <= 0f) continue;
                    if (link.LinkFromCasterOnly && dealerId != link.SourceId) continue;
                    if (best == null || link.Magnitude > best.Magnitude) best = link;
                }
                return best;
            }

            /// <summary>
            /// GAP 1: spends one of a link's charges and removes the entry at zero.
            ///
            /// `Charges` has been stamped onto a link since links existed and nothing ever spent
            /// it - the only decrement in the engine was the Guard branch's - so "the Link ends
            /// after triggering once" was unauthorable. A link at 0 charges is UNLIMITED, which is
            /// the convention Guard and the Ledger's BankLimit already use and is what every link
            /// authored before this pass has.
            ///
            /// Emits StatusExpired on removal so the status row and the record agree, exactly as a
            /// spent Guard charge does. Returns nothing: a link that ends still fired this time,
            /// which is what "ends AFTER triggering once" says.
            /// </summary>
            /// <summary>The holder's first Linked entry naming `partnerId`, or null. The entry the
            /// trigger dispatch just answered, so its charge can be the one taken.</summary>
            private static StatusInstance FindLinkTo(CombatUnit holder, int partnerId)
            {
                if (holder == null) return null;
                for (int i = 0; i < holder.Statuses.Count; i++)
                {
                    StatusInstance link = holder.Statuses[i];
                    if (link.Kind == StatusKind.Linked && link.LinkedToId == partnerId) return link;
                }
                return null;
            }

            private void SpendLinkCharge(CombatUnit holder, StatusInstance link)
            {
                if (link == null || link.Charges <= 0) return;
                link.Charges--;
                if (link.Charges > 0) return;
                holder.Statuses.Remove(link);
                BattleEvent spent = Emit(BattleEventType.StatusExpired, holder.Id, holder.Id);
                spent.Status = StatusKind.Linked;
            }

            /// <summary>Single funnel for every point of damage in the game.</summary>
            private void DealRawDamage(int sourceId, CombatUnit target, int damage, bool critical, string vfx, StatusKind dotKind,
                                        float aspectFactor, string abilityName)
            {
                if (!target.Alive || damage <= 0) return;

                // GAP 5: DEFLECT - the one rule in the funnel keyed on the ATTACKER's own statuses
                // rather than the victim's.
                //
                // "When either Linked enemy targets Nysera, the attack is redirected to the other
                // Linked enemy and deals 80% of the original damage instead." The attacker holds a
                // link created BY the victim; the blow lands on the attacker's partner instead.
                // Nothing else in the engine asks what the attacker is carrying except Vulnerable
                // (source-keyed on the victim) and AccuracyDown, so this is a new question, asked
                // once, at the top.
                //
                // FIRST, ahead of Guard, and that placement is the rule's meaning rather than a
                // convenience: the blow never reaches this body, so none of the victim's own
                // defences - Guard charges, a Covered ward, the shield pool, a mitigating Ledger -
                // may spend themselves on a hit that was moved away before it arrived. Redirect
                // and Cover sit lower precisely because those are the victim's own rules acting on
                // a blow that did reach it.
                //
                // It is NOT a Provoked variant. Provoked refuses a same-side destination because
                // "this attacker answers to whoever provoked it" stops being true the moment an
                // enemy is provoked into hitting its own ally; that guard is correct for what
                // Provoked means and is left alone. This is its own rule beside RedirectShare,
                // sharing that helper's arithmetic and its one-hop bound.
                //
                // Gated on _anyLinks, so a battle with no link pays one bool test here.
                if (_anyLinks && !_inRedirect && sourceId >= 0)
                {
                    CombatUnit attacker = FindUnit(sourceId);
                    if (attacker != null && attacker.Alive)
                    {
                        StatusInstance deflect = FindLink(attacker, LinkTransfer.Deflect, sourceId);
                        // The link has to have been created BY the unit being hit - that is what
                        // makes it "targets Nysera" rather than "attacks anyone at all".
                        if (deflect != null && deflect.SourceId == target.Id)
                        {
                            CombatUnit partner = FindUnit(deflect.LinkedToId);
                            if (partner != null && partner.Alive && partner != target && partner != attacker)
                            {
                                SpendLinkCharge(attacker, deflect);
                                // THROUGH RedirectShare, not past it. The deflect used to call
                                // DealRawDamage itself, which is the same divergence the extraction
                                // of this helper was meant to end: two mechanics moving a share of a
                                // blow, with only one of them announcing it. Routing it here gives
                                // the deflect the identical rounding, the identical clamp, the
                                // identical one-hop bound and - the point - the same
                                // OnRedirectReceived on whoever catches the share.
                                //
                                // RedirectShare's BODY IS UNTOUCHED by this, which is what keeps
                                // Covered's six shipped effects where they are: this adds a third
                                // caller, it does not change what the helper does for the two that
                                // were already there.
                                //
                                // Two consequences of going through it rather than around it, both
                                // deliberate. The share is delivered with `critical` false, as
                                // Cover's and Redirect's shares always have been - a moved share is
                                // not itself a critical hit. And the rule owner is recorded as the
                                // link's creator, so ScaleByLastMoved on that hero reads what the
                                // deflect moved on their behalf.
                                RedirectShare(sourceId, target, partner, deflect.Magnitude, ref damage,
                                              vfx, dotKind, aspectFactor, abilityName, deflect.SourceId);
                                // The blow is GONE from this body whatever the share was, which is
                                // what "instead" means: an 80% deflect is not a 20% hit that still
                                // lands. Returning here discards the remainder RedirectShare left
                                // behind, which is the one thing a deflect does that a ward does
                                // not - and is why the helper's return value is ignored rather than
                                // obeyed. A share of zero is a clean dodge, and the charge is spent
                                // either way because the deflect is what answered the attack.
                                return;
                            }
                        }
                    }
                }

                // Guard: a charge-based reduction, spent by USE rather than by time. Applied before
                // the shield pool, so a guarded ward meets a smaller hit. Walked backwards because
                // a spent charge removes its entry.
                int damageBeforeGuard = damage;
                // Who placed the charge that answered this blow, or -1 when none did. Read one
                // block down by OnGuardPrevented, and captured here because the entry is removed
                // inline the moment its last charge is spent.
                int guardPreventerId = -1;
                for (int gi = target.Statuses.Count - 1; gi >= 0; gi--)
                {
                    StatusInstance guard = target.Statuses[gi];
                    if (guard.Kind != StatusKind.Guard || guard.Magnitude <= 0f) continue;
                    // GAP GROUP 3: a ward raised against sweeps declines a single-target blow.
                    // False on every Guard in shipped content, which declines nothing.
                    if (guard.AreaHitsOnly && !_funnelHitIsArea) continue;
                    // ENGINE-GAPS group 2: a per-round budget, honoured here for the first time.
                    //
                    // Charges count DOWN to removal and never refill, which is the wrong shape for
                    // "it reduces the first area hit EACH ROUND by 20%" - one charge across a
                    // two-round ward answers one hit in total rather than one a round. A budgeted
                    // entry is gated on its budget instead and keeps its charges, if it has any;
                    // an entry with no budget still needs a charge, which is every Guard in
                    // shipped content and the exact test that stood here.
                    bool budgeted = guard.UsesPerRound > 0;
                    if (budgeted) { if (guard.UsesThisRound >= guard.UsesPerRound) continue; }
                    else if (guard.Charges <= 0) continue;
                    float cut = guard.Magnitude > 1f ? 1f : guard.Magnitude;
                    damage -= (int)Math.Round(damage * cut, MidpointRounding.AwayFromZero);
                    guardPreventerId = guard.SourceId;
                    if (budgeted) guard.UsesThisRound++;
                    // Charges are still spent when the entry has them, so a ward can carry both a
                    // per-round budget and a lifetime one and run out of either. Unbudgeted
                    // entries reach this with the same Charges > 0 they were gated on above.
                    if (guard.Charges > 0)
                    {
                        guard.Charges--;
                        if (guard.Charges <= 0)
                        {
                            target.Statuses.RemoveAt(gi);
                            BattleEvent spent = Emit(BattleEventType.StatusExpired, target.Id, target.Id);
                            spent.Status = StatusKind.Guard;
                        }
                    }
                    if (damage <= 0) { damage = 0; break; }
                    break;   // one guard a hit, so two cannot stack into immunity
                }

                // ENGINE-GAPS group 9: somebody can hear a Guard charge do its job.
                //
                // The charge is spent inline above and the entry is gone by the time anything else
                // runs, so the only thing that could observe a prevention was a Ledger with
                // FunnelSource.GuardMitigation - which banks the amount and pays out when the bank
                // FILLS, once per cap rather than once per prevention. "When Reserve prevents
                // damage, its holder recovers 4% Max HP" is a sentence about each prevention.
                //
                // Broadcast to the APPLIER'S side with the applier as the source, so
                // TriggerSourceIsSelf reads "a Guard I placed" and a hero cannot answer for
                // somebody else's ward. The holder is the subject and is excluded the way every
                // ally broadcast excludes the body the news is about; when the holder IS the
                // applier, PassiveDef.IncludeSelf is what lets the clause reach it.
                //
                // Raised only when the charge actually removed damage, and only when the applier is
                // still on the field. `_lastMoved` for the applier carries the amount prevented.
                if (guardPreventerId >= 0 && damageBeforeGuard - damage > 0)
                {
                    CombatUnit warder = FindUnit(guardPreventerId);
                    if (warder != null && warder.Alive)
                    {
                        NoteMoved(warder.Id, damageBeforeGuard - damage);
                        CombatUnit prevGpSubject = _broadcastSubject;
                        CombatUnit prevGpVictim = _broadcastVictim;
                        int prevGpSource = _broadcastSourceId;
                        _broadcastSubject = target;
                        _broadcastVictim = target;
                        _broadcastSourceId = warder.Id;
                        BroadcastPassive(warder.IsPlayer, PassiveTrigger.OnGuardPrevented, 0, target);
                        _broadcastSourceId = prevGpSource;
                        _broadcastVictim = prevGpVictim;
                        _broadcastSubject = prevGpSubject;
                        if (!target.Alive || IsBattleOver()) return;
                    }
                }

                // Funnel banking: GuardMitigation entries bank the amount Guard prevented.
                {
                    float guardMitigated = damageBeforeGuard - damage;
                    if (guardMitigated > 0f)
                    {
                        for (int fi = 0; fi < target.Statuses.Count; fi++)
                        {
                            StatusInstance led = target.Statuses[fi];
                            if (led.Kind != StatusKind.Ledger) continue;
                            if (led.FunnelSource != FunnelSource.GuardMitigation) continue;
                            if (led.Magnitude <= 0f) continue;
                            if (led.BankLimit > 0 && led.BankUsesThisRound >= led.BankLimit) continue;
                            float room = led.StoredCap > 0f ? led.StoredCap - led.Stored : float.MaxValue;
                            if (room <= 0f) continue;
                            float banked = guardMitigated * led.Magnitude;
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
                                    int prevBcSrc4154 = _broadcastSourceId;
                                    _broadcastSourceId = ledSource.Id;
                                    BroadcastPassive(ledSource.IsPlayer, PassiveTrigger.OnLedgerCapped, 0, null);
                                    _broadcastSourceId = prevBcSrc4154;
                                    _broadcastSubject = prevSubject;
                                }
                            }
                            CombatUnit bankSrc = FindUnit(led.SourceId);
                            if (bankSrc != null && bankSrc.IsPlayer == target.IsPlayer)
                            {
                                _lastLedgerBankedStored = led.Stored;
                                CombatUnit prevSubjectB = _broadcastSubject;
                                _broadcastSubject = target;
                                int prevBcSrc4164 = _broadcastSourceId;
                                _broadcastSourceId = led.SourceId;
                                BroadcastPassive(target.IsPlayer, PassiveTrigger.OnAllyLedgerBanked, 0, target);
                                _broadcastSourceId = prevBcSrc4164;
                                _broadcastSubject = prevSubjectB;
                            }
                        }
                    }
                }

                if (damage <= 0) return;

                // Cover: a share of the hit is taken by the protector instead. Split BEFORE the
                // holder's own shield pool, so the protector soaks its part and the ward underneath
                // meets only the remainder - which is what "25% of damage they take is redirected
                // to Rovan before Shields are checked" says.
                //
                // Bounded by _inRedirect: the redirected part goes through this same funnel, so a
                // protector who is itself Covered would otherwise pass its share on again. One hop,
                // the same rule Mirrored Wound holds to.
                //
                // THE ARITHMETIC MOVED INTO RedirectShare AND DID NOT CHANGE. Covered is authored
                // by six effects in heroes.json, so this is the one part of the link work that is
                // reachable by shipped content, and it is a pure extraction: the same rounding, the
                // same clamp, the same guard, the same two early returns, in the same order. The
                // Linked redirect immediately below is the second caller, which is what "one path,
                // used twice" means here.
                if (!_inRedirect)
                {
                    int protectorId;
                    StatusInstance ward;
                    float share = target.CoverFraction(out protectorId, out ward, _funnelHitIsArea);
                    CombatUnit protector = share > 0f ? FindUnit(protectorId) : null;
                    // ENGINE-GAPS group 2: the ward's per-round budget, charged BEFORE the share
                    // is delivered - the same discipline SpendLinkCharge keeps one block down, and
                    // for the same reason: the delivery re-enters this funnel, and a budget
                    // charged afterwards could be spent twice by one blow.
                    //
                    // Charged only when the ward is actually about to answer: a protector that is
                    // dead or missing leaves `protector` null and RedirectShare returns without
                    // moving anything, which is not a use.
                    if (ward != null && ward.UsesPerRound > 0 && protector != null)
                        ward.UsesThisRound++;
                    // The ward's owner IS the protector, so the rule owner and the destination are
                    // the same unit here and RedirectShare's second NoteMoved is skipped.
                    if (!RedirectShare(sourceId, target, protector, share, ref damage,
                                       vfx, dotKind, aspectFactor, abilityName, protectorId)) return;
                }

                // LINKED REDIRECT, the second use of that same path. Covered names its destination
                // with the applier's id; a Link names it with LinkedToId, which is the only
                // difference between the two mechanics. Nysera's "an attack aimed at her lands on
                // the other half of the pair" is this at a magnitude of 1.0, where the whole hit
                // moves and the `damage <= 0` return inside RedirectShare leaves her untouched.
                //
                // AFTER Cover rather than before it, and the order is a decision: Cover is the
                // shipped mechanic and its position relative to the shield pool is already
                // documented and measured, so the new rule takes the slot behind it rather than
                // displacing it. With the shared guard, at most one of the two ever fires on a
                // single hit anyway - whichever is reached first wins and the other sees
                // _inRedirect set on the recursive call, not on this one. So the ordering decides
                // which mechanic acts when a body carries BOTH, and Cover wins that.
                //
                // Gated on _anyLinks so a battle with no link in it walks no status list here.
                if (_anyLinks && !_inRedirect)
                {
                    StatusInstance redirect = FindLink(target, LinkTransfer.Redirect, sourceId);
                    CombatUnit partner = redirect != null ? FindUnit(redirect.LinkedToId) : null;
                    if (redirect != null && partner != null && partner.Alive && partner != target)
                    {
                        // Spent BEFORE the share is delivered, so a one-charge link cannot fire a
                        // second time from inside the recursion its own delivery starts.
                        SpendLinkCharge(target, redirect);
                        if (!RedirectShare(sourceId, target, partner, redirect.Magnitude, ref damage,
                                           vfx, dotKind, aspectFactor, abilityName, redirect.SourceId)) return;
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
                            // AND THE SUBJECT, which this site alone left unset. Eight lines
                            // below, OnAllyShieldBreak publishes the same body under both names
                            // for the same event; here only the victim was named, so a clause
                            // about "the ally whose Shield broke" resolved against whatever an
                            // earlier, unrelated window had left behind. One event, two triggers,
                            // and they disagreed about what they were telling you.
                            CombatUnit prevSubjectSB = _broadcastSubject;
                            _shieldBreakerId = sourceId;
                            _broadcastVictim = target;
                            _broadcastSubject = target;
                            int prevSbSrc = _broadcastSourceId;
                            _broadcastSourceId = sourceId;
                            TriggerPassive(target, PassiveTrigger.OnShieldBreak, 0);
                            _broadcastSourceId = prevSbSrc;
                            // Ally-side broadcast: the broken unit's allies hear about
                            // the break, so an applier like Darian can react. No shipped
                            // passive uses OnAllyShieldBreak, so the broadcast walks
                            // empty listener sets in every existing battle.
                            if (target.Alive && !IsBattleOver())
                            {
                                CombatUnit prevSubject2 = _broadcastSubject;
                                int prevAsbSrc = _broadcastSourceId;
                                _broadcastSubject = target;
                                _broadcastSourceId = sourceId;
                                BroadcastPassive(target.IsPlayer, PassiveTrigger.OnAllyShieldBreak, 0, target);
                                _broadcastSourceId = prevAsbSrc;
                                _broadcastSubject = prevSubject2;
                            }
                            _broadcastSubject = prevSubjectSB;
                            _shieldBreakerId = prevBreaker;
                            _broadcastVictim = prevVictimSB;
                            if (!target.Alive || IsBattleOver()) return;
                        }

                        // Funnel banking: ShieldAbsorb entries bank the amount the shield pool
                        // absorbed, at the same funnel point Guard and Cover have already passed.
                        // No shipped content sets FunnelSource.ShieldAbsorb, so this loop is
                        // unreachable in every existing battle.
                        for (int fi = 0; fi < target.Statuses.Count; fi++)
                        {
                            StatusInstance led = target.Statuses[fi];
                            if (led.Kind != StatusKind.Ledger) continue;
                            if (led.FunnelSource != FunnelSource.ShieldAbsorb) continue;
                            if (led.Magnitude <= 0f) continue;
                            if (led.BankLimit > 0 && led.BankUsesThisRound >= led.BankLimit) continue;
                            float room = led.StoredCap > 0f ? led.StoredCap - led.Stored : float.MaxValue;
                            if (room <= 0f) continue;
                            float banked = absorbed * led.Magnitude;
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
                                    int prevBcSrc4295 = _broadcastSourceId;
                                    _broadcastSourceId = ledSource.Id;
                                    BroadcastPassive(ledSource.IsPlayer, PassiveTrigger.OnLedgerCapped, 0, null);
                                    _broadcastSourceId = prevBcSrc4295;
                                    _broadcastSubject = prevSubject;
                                }
                            }
                            CombatUnit bankSrc = FindUnit(led.SourceId);
                            if (bankSrc != null && bankSrc.IsPlayer == target.IsPlayer)
                            {
                                _lastLedgerBankedStored = led.Stored;
                                CombatUnit prevSubjectB = _broadcastSubject;
                                _broadcastSubject = target;
                                int prevBcSrc4305 = _broadcastSourceId;
                                _broadcastSourceId = led.SourceId;
                                BroadcastPassive(target.IsPlayer, PassiveTrigger.OnAllyLedgerBanked, 0, target);
                                _broadcastSourceId = prevBcSrc4305;
                                _broadcastSubject = prevSubjectB;
                            }
                        }

                        // Cross-unit shield-absorb banking: for each Shield status the
                        // holder carries that was applied by a DIFFERENT same-side unit,
                        // bank the absorbed amount into the applier's own ShieldAbsorb
                        // Ledger. This lets Darian bank from shields he granted to allies.
                        // No shipped content sets FunnelSource.ShieldAbsorb, so the inner
                        // loop body is unreachable in every existing battle.
                        for (int si = 0; si < target.Statuses.Count; si++)
                        {
                            StatusInstance shield = target.Statuses[si];
                            if (shield.Kind != StatusKind.Shield) continue;
                            if (shield.SourceId < 0 || shield.SourceId == target.Id) continue;
                            CombatUnit applier = FindUnit(shield.SourceId);
                            if (applier == null || !applier.Alive) continue;
                            if (applier.IsPlayer != target.IsPlayer) continue;
                            for (int fi2 = 0; fi2 < applier.Statuses.Count; fi2++)
                            {
                                StatusInstance led2 = applier.Statuses[fi2];
                                if (led2.Kind != StatusKind.Ledger) continue;
                                if (led2.FunnelSource != FunnelSource.ShieldAbsorb) continue;
                                if (led2.Magnitude <= 0f) continue;
                                if (led2.BankLimit > 0 && led2.BankUsesThisRound >= led2.BankLimit) continue;
                                float room2 = led2.StoredCap > 0f ? led2.StoredCap - led2.Stored : float.MaxValue;
                                if (room2 <= 0f) continue;
                                float banked2 = absorbed * led2.Magnitude;
                                if (banked2 > room2) banked2 = room2;
                                bool wasBelow2 = led2.StoredCap > 0f && led2.Stored < led2.StoredCap;
                                led2.Stored += banked2;
                                if (led2.BankLimit > 0) led2.BankUsesThisRound++;
                                if (wasBelow2 && led2.StoredCap > 0f && led2.Stored >= led2.StoredCap)
                                {
                                    CombatUnit prevSubject = _broadcastSubject;
                                    _broadcastSubject = applier;
                                    int prevBcSrc4342 = _broadcastSourceId;
                                    _broadcastSourceId = applier.Id;
                                    BroadcastPassive(applier.IsPlayer, PassiveTrigger.OnLedgerCapped, 0, null);
                                    _broadcastSourceId = prevBcSrc4342;
                                    _broadcastSubject = prevSubject;
                                }
                                _lastLedgerBankedStored = led2.Stored;
                                CombatUnit prevSubjectB2 = _broadcastSubject;
                                _broadcastSubject = target;
                                int prevBcSrc4348 = _broadcastSourceId;
                                _broadcastSourceId = led2.SourceId;
                                BroadcastPassive(applier.IsPlayer, PassiveTrigger.OnAllyLedgerBanked, 0, target);
                                _broadcastSourceId = prevBcSrc4348;
                                _broadcastSubject = prevSubjectB2;
                            }
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
                        // Entries with a non-Damage FunnelSource bank at their own hook point.
                        if (led.BankHeals) continue;
                        if (led.FunnelSource != FunnelSource.Damage) continue;
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
                                int prevBcSrc4408 = _broadcastSourceId;
                                _broadcastSourceId = ledSource.Id;
                                BroadcastPassive(ledSource.IsPlayer, PassiveTrigger.OnLedgerCapped, 0, null);
                                _broadcastSourceId = prevBcSrc4408;
                                _broadcastSubject = prevSubject;
                            }
                        }
                        CombatUnit bankSrcD = FindUnit(led.SourceId);
                        if (bankSrcD != null && bankSrcD.IsPlayer == target.IsPlayer)
                        {
                            _lastLedgerBankedStored = led.Stored;
                            CombatUnit prevSubjectBD = _broadcastSubject;
                            _broadcastSubject = target;
                            int prevBcSrc4418 = _broadcastSourceId;
                            _broadcastSourceId = led.SourceId;
                            BroadcastPassive(target.IsPlayer, PassiveTrigger.OnAllyLedgerBanked, 0, target);
                            _broadcastSourceId = prevBcSrc4418;
                            _broadcastSubject = prevSubjectBD;
                        }
                    }
                }

                // Read before the bar moves, kept in a LOCAL rather than the field: this method
                // re-enters itself through echoes, redirects and passives, and a field written
                // here would be overwritten by the inner blow long before the outer one reaches
                // its own broadcast. The field is set from this local at each broadcast instead.
                float hpRatioBeforeBlow = target.HpRatio;

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
                // GAP GROUP 1: and HOW BIG it was. The realised number, written where the bar
                // moved, so every mitigation above this line has already taken its part.
                target.LastDamageTaken = damage;
                if (sourceId >= 0)
                {
                    CombatUnit dealer = FindUnit(sourceId);
                    if (dealer != null) dealer.DamagedThisRound.Add(target.Id);
                    // The same fact from the victim's side, kept per attacker so "somebody OTHER
                    // than me hit this" can be asked at all. A sourceless tick - a Burn whose
                    // applier is long dead - writes nothing, matching LastDamagedRound's own rule
                    // that a blow needs a body behind it to be attributable.
                    target.LastDamagedRoundBy[sourceId] = _round;
                    // GAP GROUP 8: and the same fact kept for the SIDE rather than for the body.
                    // Two ints on the attacker and one id on the simulation, which between them
                    // are everything "the enemy that most recently damaged an ally" and "if the
                    // target attacked an ally since my last turn" need. Written for every blow the
                    // funnel carries, a Burn tick and a redirected share included, because they
                    // are all one side hurting the other.
                    if (dealer != null)
                    {
                        if (target.IsPlayer) dealer.LastHitPlayerSideRound = _round;
                        else dealer.LastHitEnemySideRound = _round;
                    }
                    if (target.IsPlayer) _lastPlayerSideAttackerId = sourceId;
                    else _lastEnemySideAttackerId = sourceId;
                }

                // What actually came off the bar, for EffectKind.EchoDamage to repeat a fraction
                // of. Written here rather than where the damage was rolled because the whole point
                // of the echo is that it reads a realised number: everything above this line -
                // Guard, Cover, a link redirect, the shield pool, a mitigating Ledger - has already
                // taken its part out.
                _lastDamageDealt = damage;

                // ---------- the far end of a link hears about the hit ----------
                //
                // Both halves are gated on _anyLinks, so a battle with no link walks nothing here.
                // Placed AFTER the Damage event and after LastAttackerId is written, and BEFORE
                // the death check, which is three decisions worth stating:
                //
                //   After the event, so the record reads as cause then consequence: the blow that
                //   caused an echo appears ahead of the echo it caused.
                //
                //   After LastAttackerId, so ConditionSubject.LastAttacker resolves to whoever
                //   landed the blow while the trigger is in flight.
                //
                //   Before the death check, so a hit that kills the holder still reaches the
                //   partner. "A later hit on one strikes the other" does not stop being true
                //   because the first one died of it, and deferring the echo past the death branch
                //   would mean the corpse's link had already been cleared.
                if (_anyLinks)
                {
                    // ECHO: the partner takes a share AS WELL, and the holder keeps the full hit.
                    // The same split arithmetic Redirect uses with the opposite bookkeeping -
                    // nothing is subtracted from `damage` here, which is the entire difference
                    // between "instead" and "as well".
                    if (!_inRedirect)
                    {
                        // GAP 3 is inside FindLink: an entry with LinkFromCasterOnly is only
                        // returned when this blow came from the unit that created the link, which
                        // is what "the next time either recorded target is attacked BY TALREN"
                        // needs and what an attacker-agnostic funnel could not say.
                        StatusInstance echo = FindLink(target, LinkTransfer.Echo, sourceId);
                        CombatUnit echoTo = echo != null ? FindUnit(echo.LinkedToId) : null;
                        if (echoTo != null && echoTo.Alive && echoTo != target)
                        {
                            float echoShare = echo.Magnitude > 1f ? 1f : echo.Magnitude;
                            if (echoShare < 0f) echoShare = 0f;
                            // GAP 2: share PLUS flat. LinkAmount was scaled off the applier's
                            // Attack when the link was created, so "strikes for 70% of Attack" is
                            // a payload rather than a cut of the blow that triggered it. A link
                            // may carry either or both; Redirect and Deflect read only the share,
                            // because a split that added a flat amount would create value.
                            int copied = (int)Math.Round(damage * echoShare + echo.LinkAmount,
                                                         MidpointRounding.AwayFromZero);
                            int echoOwner = echo.SourceId;
                            // Spent before delivery, for the same reason the redirect's is.
                            SpendLinkCharge(target, echo);
                            if (copied > 0)
                            {
                                _inRedirect = true;
                                DealRawDamage(sourceId, echoTo, copied, false, vfx, dotKind, aspectFactor, abilityName);
                                _inRedirect = false;

                                // GAP 7, and GAP 6's echo trigger. The amount is keyed to the hero
                                // whose link caused the echo, not to the attacker who happened to
                                // land the blow - "whenever MY attack triggers against a secondary
                                // target" is a question about the link's owner.
                                NoteMoved(echoOwner, copied);
                                CombatUnit owner = FindUnit(echoOwner);
                                if (owner != null && owner.Alive && !IsBattleOver())
                                {
                                    CombatUnit prevSubjectE = _broadcastSubject;
                                    CombatUnit prevVictimE = _broadcastVictim;
                                    int prevSourceE = _broadcastSourceId;
                                    _broadcastSubject = echoTo;
                                    _broadcastVictim = echoTo;
                                    _broadcastSourceId = sourceId;
                                    TriggerPassive(owner, PassiveTrigger.OnEchoFired, 0);
                                    _broadcastSourceId = prevSourceE;
                                    _broadcastVictim = prevVictimE;
                                    _broadcastSubject = prevSubjectE;
                                }
                            }
                        }
                    }

                    // And the trigger, for a passive that wants to decide the consequence itself
                    // rather than take the engine's echo. Raised for every point of damage the
                    // funnel carries, an echoed share included - the partner really was hit.
                    DispatchLinkPassive(target, sourceId, PassiveTrigger.OnLinkedUnitHit, 0);
                    if (!target.Alive || IsBattleOver()) return;
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
                        int prevWdSrc = _broadcastSourceId;
                        _broadcastSourceId = sourceId;
                        BroadcastPassive(target.IsPlayer, PassiveTrigger.OnAllyWouldDie, 0, target);
                        _broadcastSourceId = prevWdSrc;
                        _broadcastSubject = prevSubject;
                        _broadcastVictim = prevVictim;
                        _inDeathCheck = false;
                    }

                    // Re-read, because the broadcast above may have healed it. With nothing
                    // listening - which is all of shipped content - Hp is untouched and this is the
                    // same test that guarded the block before.
                    if (target.Hp > 0f)
                    {
                        int prevHtSrc = _broadcastSourceId;
                        _broadcastSourceId = sourceId;
                        TriggerPassive(target, PassiveTrigger.OnHpBelowThreshold, 0);
                        _broadcastSourceId = prevHtSrc;
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
                        int prevDefSrc = _broadcastSourceId;
                        _broadcastSourceId = sourceId;
                        BroadcastPassive(target.IsPlayer, PassiveTrigger.OnAllyDefeated, 0, target);
                        BroadcastPassive(!target.IsPlayer, PassiveTrigger.OnEnemyDefeated, 0, null);
                        _broadcastSourceId = prevDefSrc;

                        _broadcastSubject = prevSubject;
                        _broadcastVictim = prevVictim;
                        _defeated = previous;
                        _inDefeatDispatch = false;
                    }

                    // ONKILL, INSIDE THE DEFEAT WINDOW AND BEFORE THE CORPSE IS STRIPPED.
                    //
                    // It used to fire after `Statuses.Clear()` and after `_defeated` had already
                    // been restored, which made it a trigger that could not ask a single question
                    // about the thing it had just killed: ConditionSubject.Defeated resolved to
                    // nobody, and HasMyMark on the body was false because the mark had been
                    // cleared one line earlier. "When Kiva defeats a FORSAKEN target" had no way
                    // to test the word Forsaken.
                    //
                    // The killer and the corpse were split across two hooks and neither carried
                    // both: OnEnemyDefeated has the corpse and does not say who killed it, OnKill
                    // has the killer and, until now, nothing else.
                    //
                    // Raised HERE rather than moved inside the `!_inDefeatDispatch` block above,
                    // because that block is skipped for a re-entrant death and OnKill must still
                    // fire for one - the existing comment promises exactly that. So the context is
                    // set around this call instead, which is the narrower change.
                    CombatUnit killer = FindUnit(sourceId);
                    if (killer != null && killer.Alive)
                    {
                        CombatUnit prevKillSubject = _broadcastSubject;
                        CombatUnit prevKillVictim = _broadcastVictim;
                        CombatUnit prevKillDefeated = _defeated;
                        _broadcastSubject = target;
                        _broadcastVictim = target;
                        _defeated = target;
                        int prevKillSrc = _broadcastSourceId;
                        _broadcastSourceId = killer.Id;
                        TriggerPassive(killer, PassiveTrigger.OnKill, 0);
                        _broadcastSourceId = prevKillSrc;
                        _defeated = prevKillDefeated;
                        _broadcastVictim = prevKillVictim;
                        _broadcastSubject = prevKillSubject;
                    }

                    // AFTER the kill trigger now. The corpse keeps what it died carrying until
                    // every hook that could read it has run.
                    target.Statuses.Clear();
                    return;
                }

                // -1: energy from being hit is the victim's own regen, not something the attacker
                // granted. A Tide attacker must not redirect its victim's line's energy.
                AddEnergy(target, _cfg.EnergyOnDamaged, -1);

                // The victim is its own subject here, so ConditionSubject.LastAttacker resolves at
                // OnDamaged to the unit that just hit it - the same thing it already means at
                // OnEnemyDamagedAlly eight lines below, which reads the victim's LastAttackerId
                // through exactly this field. Without it a passive on the body being hit cannot
                // ask anything about who is hitting it, while every ALLY of that body can.
                //
                // Reads nothing and changes nothing on its own: it moves a field that only a
                // condition subject consults, and no shipped passive at OnDamaged names one.
                {
                    CombatUnit prevDmgSubject = _broadcastSubject;
                    int prevDmgSrc = _broadcastSourceId;
                    _broadcastSubject = target;
                    _broadcastSourceId = sourceId;
                    TriggerPassive(target, PassiveTrigger.OnDamaged, 0);
                    _broadcastSourceId = prevDmgSrc;
                    _broadcastSubject = prevDmgSubject;
                }

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
                    int prevEdaSrc = _broadcastSourceId;
                    _broadcastSourceId = sourceId;
                    BroadcastPassive(target.IsPlayer, PassiveTrigger.OnEnemyDamagedAlly, 0, target);
                    _broadcastSourceId = prevEdaSrc;
                    _broadcastSubject = prevSubject;
                    _broadcastVictim = prevVictim;
                }

                // And separately, that an ally has dropped low. A distinct question from being hit
                // at all - a chip hit on a full bar is not what "when an ally falls below 35%" is
                // asking about - so it is its own trigger rather than a condition on that one. The
                // watcher's OWN Threshold decides how low counts, read in TriggerPassive where
                // every other threshold passive reads it, against the pair of readings carried in
                // _thresholdCrossedFrom. Both of those were claims this comment made and the code
                // did not: the broadcast went out on EVERY point of damage, whatever the watcher's
                // Threshold said and however long the victim had already been below it.
                {
                    CombatUnit prevSubject = _broadcastSubject;
                    CombatUnit prevVictim = _broadcastVictim;
                    _broadcastSubject = target;
                    _broadcastVictim = target;
                    int prevAhtSrc = _broadcastSourceId;
                    _broadcastSourceId = sourceId;
                    float prevCrossAht = _thresholdCrossedFrom;
                    _thresholdCrossedFrom = hpRatioBeforeBlow;
                    BroadcastPassive(target.IsPlayer, PassiveTrigger.OnAllyHpBelowThreshold, 0, target);
                    _thresholdCrossedFrom = prevCrossAht;
                    _broadcastSourceId = prevAhtSrc;
                    _broadcastSubject = prevSubject;
                    _broadcastVictim = prevVictim;
                }

                // Mirrored from the victim's side to the attacker's: that an enemy dropped below
                // threshold. Same trigger shape, same crossing rule, other audience.
                {
                    CombatUnit prevSubject = _broadcastSubject;
                    CombatUnit prevVictim = _broadcastVictim;
                    _broadcastSubject = target;
                    _broadcastVictim = target;
                    int prevEhtSrc = _broadcastSourceId;
                    _broadcastSourceId = sourceId;
                    float prevCrossEht = _thresholdCrossedFrom;
                    _thresholdCrossedFrom = hpRatioBeforeBlow;
                    BroadcastPassive(!target.IsPlayer, PassiveTrigger.OnEnemyHpBelowThreshold, 0, null);
                    _thresholdCrossedFrom = prevCrossEht;
                    _broadcastSourceId = prevEhtSrc;
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

                {
                    int prevHt2Src = _broadcastSourceId;
                    _broadcastSourceId = sourceId;
                    TriggerPassive(target, PassiveTrigger.OnHpBelowThreshold, 0);
                    _broadcastSourceId = prevHt2Src;
                }
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

                // Partial reduction, read immediately after the total one and before anything
                // else touches the amount. Deepest entry wins rather than the entries stacking:
                // two 20% reductions are 20%, not 36% or 40%, which is the reading every other
                // magnitude-bearing status in this engine takes.
                float healCut = 0f;
                for (int hi = 0; hi < target.Statuses.Count; hi++)
                {
                    StatusInstance hr = target.Statuses[hi];
                    if (hr.Kind != StatusKind.HealingReduction) continue;
                    if (hr.Magnitude > healCut) healCut = hr.Magnitude;
                }
                if (healCut > 0f)
                {
                    if (healCut > 1f) healCut = 1f;
                    amount = (int)Math.Round(amount * (1f - healCut), MidpointRounding.AwayFromZero);
                    if (amount <= 0) return;
                }

                // And the direction this pipeline never had. HealSuppression refuses a heal and
                // HealingReduction cuts one; nothing could raise one, so "Diagnosed allies receive
                // 18% more healing" had to become "Lio heals them 18% more" - a smaller clause
                // that also stops being true the moment a second Mender stands beside him.
                //
                // Deepest entry wins rather than the entries stacking, which is the reading the
                // block directly above takes and the one every other magnitude-bearing status in
                // this engine takes. Exactly zero when nothing carries the kind.
                float healBoost = 0f;
                for (int hu = 0; hu < target.Statuses.Count; hu++)
                {
                    StatusInstance hb = target.Statuses[hu];
                    if (hb.Kind != StatusKind.HealingReceivedUp) continue;
                    if (hb.Magnitude > healBoost) healBoost = hb.Magnitude;
                }
                if (healBoost > 0f)
                {
                    amount = (int)Math.Round(amount * (1f + healBoost), MidpointRounding.AwayFromZero);
                    if (amount <= 0) return;
                }

                target.LastHealedRound = _round;

                CombatUnit healerUnit = FindUnit(sourceId);
                float healMod = OutputModMultiplier(healerUnit, target, OutputModScope.HealingOnly);
                if (healMod != 1f) amount = (int)Math.Round(amount * healMod, MidpointRounding.AwayFromZero);
                if (amount <= 0) return;

                // Funnel banking: HealIntercept entries divert a fraction of the incoming heal
                // BEFORE it restores HP. No shipped content sets FunnelSource.HealIntercept, so
                // this loop is unreachable in every existing battle.
                for (int fi = 0; fi < target.Statuses.Count; fi++)
                {
                    StatusInstance led = target.Statuses[fi];
                    if (led.Kind != StatusKind.Ledger) continue;
                    if (led.FunnelSource != FunnelSource.HealIntercept) continue;
                    if (led.Magnitude <= 0f || amount <= 0) continue;
                    if (led.BankLimit > 0 && led.BankUsesThisRound >= led.BankLimit) continue;

                    int intercepted = (int)Math.Round(amount * led.Magnitude, MidpointRounding.AwayFromZero);
                    if (intercepted <= 0) continue;
                    if (intercepted > amount) intercepted = amount;
                    amount -= intercepted;

                    if (led.FunnelConvert)
                    {
                        // Convert path: deal the intercepted amount as damage immediately.
                        DealRawDamage(led.SourceId, target, intercepted, false, "impact",
                                      StatusKind.None, 1f, "");
                        // OnFunnelConvertFired: broadcast to the applier's side so the
                        // applier can react (e.g. Velis gains Remnant). No shipped content
                        // sets FunnelConvert, so the broadcast is unreachable in every
                        // existing battle.
                        CombatUnit convertSource = FindUnit(led.SourceId);
                        if (convertSource != null && convertSource.Alive && !IsBattleOver())
                        {
                            CombatUnit prevSubjectC = _broadcastSubject;
                            int prevFcSrc = _broadcastSourceId;
                            _broadcastSubject = target;
                            _broadcastSourceId = convertSource.Id;
                            BroadcastPassive(convertSource.IsPlayer, PassiveTrigger.OnFunnelConvertFired, 0, null);
                            _broadcastSourceId = prevFcSrc;
                            _broadcastSubject = prevSubjectC;
                        }
                        if (!target.Alive) return;
                    }
                    else
                    {
                        // Bank path: store the intercepted amount.
                        float room = led.StoredCap > 0f ? led.StoredCap - led.Stored : float.MaxValue;
                        if (room <= 0f) continue;
                        float banked = (float)intercepted;
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
                                int prevBcSrc4864 = _broadcastSourceId;
                                _broadcastSourceId = ledSource.Id;
                                BroadcastPassive(ledSource.IsPlayer, PassiveTrigger.OnLedgerCapped, 0, null);
                                _broadcastSourceId = prevBcSrc4864;
                                _broadcastSubject = prevSubject;
                            }
                        }
                        CombatUnit bankSrcHI = FindUnit(led.SourceId);
                        if (bankSrcHI != null && bankSrcHI.IsPlayer == target.IsPlayer)
                        {
                            _lastLedgerBankedStored = led.Stored;
                            CombatUnit prevSubjectBHI = _broadcastSubject;
                            _broadcastSubject = target;
                            int prevBcSrc4874 = _broadcastSourceId;
                            _broadcastSourceId = led.SourceId;
                            BroadcastPassive(target.IsPlayer, PassiveTrigger.OnAllyLedgerBanked, 0, target);
                            _broadcastSourceId = prevBcSrc4874;
                            _broadcastSubject = prevSubjectBHI;
                        }
                    }
                }
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
                            int prevHaSrc = _broadcastSourceId;
                            _broadcastSourceId = healer.Id;
                            TriggerPassive(healer, PassiveTrigger.OnHealApplied, 0);
                            _broadcastSourceId = prevHaSrc;
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

                        // OnEnemyHealed: broadcast to the OPPOSING side when an enemy is
                        // healed. No shipped passive uses OnEnemyHealed, so the broadcast
                        // walks empty listener sets in every existing battle.
                        {
                            CombatUnit prevSubject = _broadcastSubject;
                            CombatUnit prevVictim = _broadcastVictim;
                            _broadcastSubject = target;
                            _broadcastVictim = target;
                            int prevEhSrc = _broadcastSourceId;
                            _broadcastSourceId = healer.Id;
                            BroadcastPassive(!target.IsPlayer, PassiveTrigger.OnEnemyHealed, 0, null);
                            _broadcastSourceId = prevEhSrc;
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
                                    int prevBcSrc4976 = _broadcastSourceId;
                                    _broadcastSourceId = ledSource.Id;
                                    BroadcastPassive(ledSource.IsPlayer, PassiveTrigger.OnLedgerCapped, 0, null);
                                    _broadcastSourceId = prevBcSrc4976;
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

                // --- and the recipient's own side hears that the bar moved ---
                // ENGINE-GAPS group 9. The mirror of OnEnemyEnergyDrained below, which fires on
                // whoever TOOK Energy across the line; nothing fired for Energy arriving, so "when
                // an ally gains Energy" had to be folded into the passive that hands it out.
                //
                // Four things have to hold, and each drops a case the clause does not mean: a
                // gain rather than a drain, a bar that actually moved rather than one already
                // full, a named granter rather than a unit's own turn refill (which passes -1),
                // and one level of re-entrancy rather than a passive that answers a gain by
                // granting. The recipient is excluded the way every ally broadcast excludes the
                // body the news is about, and PassiveDef.IncludeSelf is how a clause says
                // otherwise.
                if (amount > 0 && energyBefore >= 0 && unit.Energy != energyBefore
                    && sourceId >= 0 && !_inEnergyGainTrigger)
                {
                    _inEnergyGainTrigger = true;
                    CombatUnit prevEgSubject = _broadcastSubject;
                    CombatUnit prevEgVictim = _broadcastVictim;
                    int prevEgSource = _broadcastSourceId;
                    _broadcastSubject = unit;
                    _broadcastVictim = unit;
                    _broadcastSourceId = sourceId;
                    BroadcastPassive(unit.IsPlayer, PassiveTrigger.OnAllyEnergyGained, 0, unit);
                    _broadcastSourceId = prevEgSource;
                    _broadcastVictim = prevEgVictim;
                    _broadcastSubject = prevEgSubject;
                    _inEnergyGainTrigger = false;
                }

                // --- and the mirror of it: somebody took Energy off an enemy ---
                // After the clamp, so "lost Energy" means the bar actually moved rather than a
                // drain that hit an empty one. Across the line only - a unit paying its own cast
                // cost, or an ally handing energy about, is not a drain and never reaches here.
                //
                // Re-entrancy is why the flag exists rather than a depth count: the passive this
                // fires can itself drain, and a Tempo that feeds itself is an endless battle
                // rather than a wrong number. One drain answers one trigger.
                if (amount < 0 && energyBefore > 0 && unit.Energy != energyBefore
                    && sourceId >= 0 && !_inDrainTrigger)
                {
                    CombatUnit drainer = FindUnit(sourceId);
                    if (drainer != null && drainer.Alive && drainer.IsPlayer != unit.IsPlayer)
                    {
                        _inDrainTrigger = true;
                        CombatUnit prevDrSubject = _broadcastSubject;
                        int prevDrSource = _broadcastSourceId;
                        _broadcastSubject = unit;
                        _broadcastSourceId = drainer.Id;
                        TriggerPassive(drainer, PassiveTrigger.OnEnemyEnergyDrained, 0);
                        _broadcastSourceId = prevDrSource;
                        _broadcastSubject = prevDrSubject;
                        _inDrainTrigger = false;
                    }
                }
            }

            /// <summary>Guards the drain trigger against a passive that drains. See AddEnergy.</summary>
            private bool _inDrainTrigger;

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
                int prevEetSrc = _broadcastSourceId;
                _broadcastSourceId = unit.Id;
                BroadcastPassive(!unit.IsPlayer, PassiveTrigger.OnEnemyEnergyThreshold, 0, null);
                _broadcastSourceId = prevEetSrc;
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
            /// <summary>
            /// Fires every passive on `owner` that answers `trigger`, and reports whether any of
            /// them did.
            ///
            /// The return value is new and exactly one caller reads it: DispatchLinkPassive, which
            /// has to know whether a link's record was actually SPENT before taking a charge off
            /// it. "The next time this body is attacked, do X" is a claim about X happening, so a
            /// holder with nothing listening must not burn its charge on an event it ignored.
            /// Every other call site ignores the value and is unchanged by it.
            /// </summary>
            /// <summary>
            /// Whether `owner` is the cause of the window now open. Reads the single source id,
            /// and the expiry sweep's list of appliers for the one window that can have several -
            /// see _expiredSourceIds. A list that is empty outside an expiry costs one count test.
            /// </summary>
            private bool BroadcastCausedBy(CombatUnit owner)
            {
                if (owner == null) return false;
                if (_broadcastSourceId == owner.Id) return true;
                return _expiredSourceIds.Count > 1 && _expiredSourceIds.Contains(owner.Id);
            }

            private bool TriggerPassive(CombatUnit owner, PassiveTrigger trigger, int depth)
            {
                bool firedAny = false;
                if (owner == null || !owner.Alive) return false;

                for (int i = 0; i < owner.Passives.Count; i++)
                {
                    // Between passives only. A passive that killed its owner, or ended the fight,
                    // stops the ones behind it rather than resolving into a decided battle - the
                    // same rule ResolveAbility already applies between an effect's own targets.
                    if (i > 0 && (!owner.Alive || IsBattleOver())) return firedAny;

                    PassiveDef passive = owner.Passives[i];
                    if (passive == null || passive.Trigger != trigger) continue;
                    // The other half of PassiveDef.IncludeSelf. This owner is in the walk only
                    // because SOME passive on it opted in; the ones that did not are still the
                    // excluded body and are skipped here. Null outside a broadcast, so the test is
                    // one reference comparison on every other dispatch in the engine.
                    if (owner == _broadcastExcept && !passive.IncludeSelf) continue;
                    if (passive.OncePerBattle && owner.PassiveHasFired(i)) continue;
                    if (!LimitAllows(owner, i, passive)) continue;
                    if (trigger == PassiveTrigger.OnHpBelowThreshold && owner.HpRatio > passive.Threshold) continue;

                    // The ally/enemy pair of the line above, asked of the VICTIM rather than of the
                    // watcher, and asked as a CROSSING rather than as a state.
                    //
                    // Neither of these read a Threshold at all before this. "When an ally falls
                    // below 35% HP" fired on every point of damage any ally took at any HP, which
                    // is a different sentence: it made Edrin's Still Here a once-per-round free
                    // attack off the first scratch of the round, and Velis's Nothing Held Back a
                    // permanent aura. Both now wait for the bar to actually cross the line.
                    //
                    // ONCE PER CROSSING falls out of the two readings rather than being counted:
                    // strictly above the line before the blow, at or below it after. The twentieth
                    // chip hit on a body that has been at 10% all fight was never a crossing, so
                    // nothing has to remember that the first one was. A heal back over the line
                    // re-arms it by the same arithmetic - a unit that recovered and fell again fell
                    // again - and no bookkeeping has to be cleared for that to be true.
                    //
                    // Inclusive at the boundary, matching OnHpBelowThreshold one line up.
                    if (trigger == PassiveTrigger.OnAllyHpBelowThreshold
                        || trigger == PassiveTrigger.OnEnemyHpBelowThreshold)
                    {
                        if (_broadcastVictim == null) continue;
                        if (_broadcastVictim.HpRatio > passive.Threshold) continue;
                        if (_thresholdCrossedFrom <= passive.Threshold) continue;
                    }
                    if (trigger == PassiveTrigger.OnTurnDebtThreshold && _thresholdUnit != null)
                    {
                        float debtStored = 0f;
                        for (int di = 0; di < _thresholdUnit.Statuses.Count; di++)
                            if (_thresholdUnit.Statuses[di].Kind == StatusKind.TurnDebt)
                                debtStored += _thresholdUnit.Statuses[di].Stored;
                        if (debtStored < passive.Threshold) continue;
                    }
                    // TriggerAbilityId filter: a passive that names an ability answers only a
                    // declaration of THAT ability. Empty on every passive in shipped content, so
                    // this check never rejects anything in an existing battle.
                    if (!string.IsNullOrEmpty(passive.TriggerAbilityId)
                        && (_castAbility == null || _castAbility.Id != passive.TriggerAbilityId))
                        continue;

                    // TriggerStatus filter: when a passive names a specific status, the trigger
                    // fires only if that status was among the ones that expired. No shipped content
                    // sets TriggerStatus, so this check never fires for existing battles.
                    if ((trigger == PassiveTrigger.OnStatusExpired
                         || trigger == PassiveTrigger.OnAppliedStatusExpired
                         || trigger == PassiveTrigger.OnAppliedAllyStatusExpired)
                        && passive.TriggerStatus != StatusKind.None
                        && !_expiredKinds.Contains(passive.TriggerStatus)) continue;
                    // The gained-status mirror of the same filter. Reads _gainedKind, the single
                    // status the application in flight is applying, rather than the expiry sweep's
                    // list - an application lands one kind at a time, a sweep can retire several.
                    if (trigger == PassiveTrigger.OnStatusGained
                        && passive.TriggerStatus != StatusKind.None
                        && passive.TriggerStatus != _gainedKind) continue;
                    // A real buff, not a Mark, Ledger, Link or Resolve counter the applier wrote.
                    // See PassiveDef.BuffsOnly; false on every shipped passive.
                    if (trigger == PassiveTrigger.OnEnemyBuffGained && passive.BuffsOnly
                        && StatusInfo.IsInert(_broadcastBuff)) continue;
                    // Stack-threshold filter: check the owner's stack count against the
                    // passive's threshold, and consume if met.
                    if (trigger == PassiveTrigger.OnStackThreshold)
                    {
                        int ci = passive.CounterIndex > 0 ? passive.CounterIndex
                               : ResolveCounterIndex(0, passive.StackThresholdStatus);
                        if (ci < 0 || passive.StackThresholdCount <= 0) continue;
                        if (ci >= 2)
                        {
                            if (owner.CountOfCounter(ci) < passive.StackThresholdCount) continue;
                            owner.SpendCounter(ci, passive.StackThresholdCount);
                        }
                        else
                        {
                            StatusKind sk = ci == 1 ? StatusKind.Resolve2 : StatusKind.Resolve;
                            if (owner.CountOf(sk) < passive.StackThresholdCount) continue;
                            owner.RemoveStacks(sk, passive.StackThresholdCount);
                        }
                    }
                    if (passive.TriggerSourceIsSelf && !BroadcastCausedBy(owner)) continue;
                    // The side half of the same filter. Read here rather than as a condition
                    // because no EffectCondition can ask which line a unit stands on - see
                    // PassiveDef.TriggerSourceIsAlly. An unset flag is the untouched path: false
                    // short-circuits before the lookup, so no existing passive pays for this.
                    if (passive.TriggerSourceIsAlly)
                    {
                        if (_broadcastSourceId < 0 || _broadcastSourceId == owner.Id) continue;
                        CombatUnit actor = FindUnit(_broadcastSourceId);
                        if (actor == null || actor.IsPlayer != owner.IsPlayer) continue;
                    }
                    if (passive.Effects.Count == 0) continue;
                    if (depth >= MaxPassiveDepth) continue;

                    // The per-subject budget. Checked here, where the subject is already known and
                    // the firing has not happened yet; charged below, after the effects have run,
                    // so a condition asking "have I fired on this one before" reads 0 during the
                    // first firing. Zero on every shipped passive, and a zero never enters.
                    if (passive.FiresPerSubject > 0)
                    {
                        if (_broadcastSubject == null) continue;
                        if (owner.PassiveFiresOnSubject(i, _broadcastSubject.Id) >= passive.FiresPerSubject)
                            continue;
                    }
                    CombatUnit budgetSubject = passive.FiresPerSubject > 0 ? _broadcastSubject : null;

                    // CHARGED HERE, and for a passive that says ChargeOnLanding, REFUNDED BELOW IF
                    // NOTHING LANDED. The charge has to be taken before the effects run, because a
                    // passive whose own effects raise its own trigger would otherwise fire again
                    // from inside itself before it had paid. What such a passive must not do is stay
                    // spent on a firing whose every condition said no: "the first time an ally WITH
                    // PROMISE would be defeated" is a sentence about the clause, and an allowance
                    // used up by the first ally to fall without Promise is a clause that can be gone
                    // before it has ever landed. ENGINE-GAPS group 34 and pass 2. Opt-in, so every
                    // passive that does not set the field is charged exactly as it always was.
                    int roundBefore = owner.PassiveLastRound(i);
                    if (passive.OncePerBattle) owner.MarkPassiveFired(i);
                    if (passive.Limit != PassiveLimit.Unlimited) owner.MarkPassiveRound(i, _round);

                    BattleEvent e = Emit(BattleEventType.PassiveTrigger, owner.Id, owner.Id);
                    e.AbilityId = passive.Id;
                    e.AbilityName = passive.Name;
                    e.Vfx = passive.Vfx;
                    int triggerEventCount = _result.Events.Count;

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

                    List<int> prevAdmitted = _perTargetAdmitted;
                    _perTargetOwner = passive.PerTargetLimit > 0 ? owner : null;
                    _perTargetIndex = i;
                    _perTargetLimit = passive.PerTargetLimit;
                    _perTargetPerRound = passive.PerTargetPerRound;
                    // A fresh sheet per firing, and only for a passive that has a budget at all.
                    _perTargetAdmitted = passive.PerTargetLimit > 0 ? new List<int>() : null;

                    int prevFired = _firedPassiveIndex;
                    _firedPassiveIndex = i;
                    CombatUnit prevFiredOwner = _firedPassiveOwner;
                    _firedPassiveOwner = owner;
                    bool prevLanded = _firingLanded;
                    _firingLanded = false;

                    ResolveAbility(owner, wrapper, depth + 1);

                    // The refund. A ChargeOnLanding firing that reached ApplyEffect for nothing -
                    // every effect's condition false, every target list empty, every body refused by
                    // its per-target budget - gives back the allowance it took. An unlimited passive
                    // never enters, and neither does one that did not ask.
                    //
                    // AND IT TAKES ITS PassiveTrigger EVENT BACK OUT, when that event is still the
                    // last thing in the record. Under the trigger-time charge a limited passive
                    // arrives at most once per window, so an empty firing costs one line in the
                    // record; once the allowance comes back it can arrive at every ally's turn end,
                    // and Kael's Unusable Cover would put its name on the screen five times a round
                    // for a clause that did nothing. A firing that emitted anything at all after its
                    // own event - a Resolve spent by an unconditional effect aimed at nobody - keeps
                    // it, because the record has to explain that entry.
                    //
                    // A refunded firing is also not a firing to whoever asked: DispatchLinkPassive
                    // spends a link's record only when its listener fired, and a listener that gave
                    // its allowance back has not used the record either.
                    //
                    // THE PER-SUBJECT BUDGET IS NOT REFUNDED, because it is a count as well as a
                    // cap: PassiveFiredOnSubjectAtLeast reads it, and "the second time this happens
                    // to one body" has to count a first firing whose only effect was waiting for
                    // the second. Giving it back would make every such clause unreachable.
                    bool refunded = passive.ChargeOnLanding && !_firingLanded
                        && (passive.OncePerBattle || passive.Limit != PassiveLimit.Unlimited);
                    if (!refunded) firedAny = true;
                    if (refunded)
                    {
                        if (passive.OncePerBattle) owner.ClearPassiveFired(i);
                        if (passive.Limit != PassiveLimit.Unlimited) owner.MarkPassiveRound(i, roundBefore);
                        if (_result.Events.Count == triggerEventCount && _result.Events[triggerEventCount - 1] == e)
                            _result.Events.RemoveAt(triggerEventCount - 1);
                    }
                    _firingLanded = prevLanded;

                    _firedPassiveIndex = prevFired;
                    _firedPassiveOwner = prevFiredOwner;
                    _perTargetAdmitted = prevAdmitted;
                    _perTargetOwner = prevOwner;
                    _perTargetIndex = prevIndex;
                    _perTargetLimit = prevLimit;
                    _perTargetPerRound = prevPerRound;

                    // CHARGED AFTER, not before. A condition asking how many times this passive
                    // has fired about this subject has to read 0 while the first firing is still
                    // running, or "only the first time" can never be written.
                    if (budgetSubject != null) owner.NotePassiveFireOnSubject(i, budgetSubject.Id);
                }
                return firedAny;
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
            /// <summary>
            /// How many entries the per-stack scaler counts on this subject.
            ///
            /// ENGINE-GAPS group 17. This used to be one line at each of two call sites -
            /// CountOf(PerStackStatus) - and widening it in one place is what stops the
            /// caster-side snapshot and the per-target read disagreeing about what a buff is or
            /// where the ceiling sits. An effect that names no class and no ceiling gets exactly
            /// the call that stood there.
            /// </summary>
            private static int PerStackCount(EffectDef effect, CombatUnit subject)
            {
                int count;
                switch (effect.PerStackClass)
                {
                    case StatusCount.AnyBeneficial: count = subject.BuffCount; break;
                    case StatusCount.AnyHarmful: count = subject.DebuffCount; break;
                    default: count = subject.CountOf(effect.PerStackStatus); break;
                }
                if (effect.PerStackMaxCount > 0 && count > effect.PerStackMaxCount)
                    count = effect.PerStackMaxCount;
                return count;
            }

            /// <summary>
            /// What the per-stack scaler multiplies Power, Magnitude and a GainResolve's Hits by.
            ///
            /// FROM ZERO means the count IS the multiplier rather than a bonus on top of one, and
            /// the two clauses that asked for this differ exactly there: "damage increases 10% per
            /// displayed buff" is a bonus on a blow that lands anyway, and "removes 6% turn
            /// progress for each target buff" is the whole of the amount, which against a bare
            /// target is nothing.
            ///
            /// Returns 1 + PerStack * CountOf(PerStackStatus) for an effect that sets none of the
            /// three new fields, which is the expression that stood at both call sites.
            /// </summary>
            private static float PerStackFactor(EffectDef effect, CombatUnit subject)
            {
                int count = PerStackCount(effect, subject);
                return effect.PerStackFromZero
                    ? effect.PerStack * count
                    : 1f + effect.PerStack * count;
            }

            private void BroadcastPassive(bool playerSide, PassiveTrigger trigger, int depth, CombatUnit except)
            {
                List<CombatUnit> side = new List<CombatUnit>();
                for (int i = 0; i < _units.Count; i++)
                {
                    CombatUnit u = _units[i];
                    if (!u.Alive || u.IsPlayer != playerSide) continue;
                    // THE EXCLUSION, AND THE ONE WAY PAST IT. The excluded unit enters the walk
                    // only when it carries a passive that both answers this trigger and says "and
                    // me" - see PassiveDef.IncludeSelf. Gated on _anyIncludeSelf first, so a
                    // battle nobody authored it into builds the identical list it always did and
                    // the unit keeps its natural position when somebody does.
                    if (u == except && !(_anyIncludeSelf && HearsAsSubject(u, trigger))) continue;
                    side.Add(u);
                }
                CombatUnit prevExcept = _broadcastExcept;
                _broadcastExcept = except;
                for (int i = 0; i < side.Count; i++)
                {
                    if (IsBattleOver()) break;
                    TriggerPassive(side[i], trigger, depth);
                }
                _broadcastExcept = prevExcept;
            }

            /// <summary>Whether this unit carries a passive that answers `trigger` AND opts in to
            /// hearing it about itself. Walks a list that is one or two entries long on a hero and
            /// usually empty on an enemy, and is reached only when _anyIncludeSelf is true.
            /// </summary>
            private static bool HearsAsSubject(CombatUnit u, PassiveTrigger trigger)
            {
                for (int i = 0; i < u.Passives.Count; i++)
                {
                    PassiveDef p = u.Passives[i];
                    if (p != null && p.IncludeSelf && p.Trigger == trigger) return true;
                }
                return false;
            }

            /// <summary>
            /// Fires one trigger on every living unit holding a StatusKind.Linked entry that names
            /// `subject`, wherever that unit stands.
            ///
            /// THE THIRD DISPATCH SHAPE IN THIS ENGINE, and it is not a broadcast. A broadcast is
            /// scoped to a SIDE, and a link is not a side: Rovan's partner is an ally, Talren binds
            /// two enemies to each other, and Vaelis binds herself to an enemy. Asking
            /// BroadcastPassive to serve those would mean guessing which side to walk and being
            /// wrong for at least one of them, so this walks the units once and lets the entry
            /// itself say who cares.
            ///
            /// `actorId` is whoever did the thing - the attacker who aimed or landed the blow - and
            /// is published as `_broadcastSourceId` so TriggerSourceIsSelf and TriggerSourceIsAlly
            /// can filter on it. `_linkPartner` is set to the subject rather than to the holder,
            /// because TargetMode.LinkedUnit asked from inside a holder's passive means "the far
            /// end", and from the holder's point of view the far end IS the subject.
            ///
            /// Walks a snapshot and restores every window field, exactly as BroadcastPassive does
            /// and for the same reasons: a passive firing inside the loop can kill somebody, and a
            /// dispatch nested inside another must not leave the outer one's fields rewritten.
            ///
            /// Callers gate on _anyLinks before entering, so this costs a battle with no links
            /// nothing at all.
            /// </summary>
            private void DispatchLinkPassive(CombatUnit subject, int actorId, PassiveTrigger trigger, int depth)
            {
                if (subject == null) return;

                // BOUNDED, and it has to be. A link dispatch is raised from inside DealRawDamage,
                // which has no depth of its own to thread, so every funnel-raised dispatch starts
                // at 0 the way the broadcasts beside it do - and a passive that answers "my partner
                // was hit" by damaging its OTHER partner walks straight back in here at 0 again.
                // That is the natural shape of the link triggers rather than an exotic authoring:
                // it is exactly what "record two targets, strike the other one" does, and it
                // overflowed the stack the first time an arena authored it with oncePerBattle
                // false.
                //
                // The counter is this dispatch's own nesting and is ALSO handed to TriggerPassive
                // as the depth, so a cascade is bounded by MaxPassiveDepth once rather than by two
                // independent limits that could disagree. Same discipline as _inReflect and
                // _inDeathCheck, counting instead of latching because a link chain is legitimately
                // allowed to be more than one deep.
                if (_linkDispatchDepth >= MaxPassiveDepth) return;

                List<CombatUnit> holders = null;
                for (int i = 0; i < _units.Count; i++)
                {
                    CombatUnit u = _units[i];
                    if (!u.Alive || u == subject) continue;
                    bool bound = false;
                    for (int s = 0; s < u.Statuses.Count; s++)
                    {
                        StatusInstance link = u.Statuses[s];
                        if (link.Kind == StatusKind.Linked && link.LinkedToId == subject.Id) { bound = true; break; }
                    }
                    if (!bound) continue;
                    if (holders == null) holders = new List<CombatUnit>();
                    holders.Add(u);
                }
                if (holders == null) return;

                CombatUnit prevSubject = _broadcastSubject;
                CombatUnit prevVictim = _broadcastVictim;
                CombatUnit prevPartner = _linkPartner;
                int prevSourceId = _broadcastSourceId;
                _broadcastSubject = subject;
                _broadcastVictim = subject;
                _linkPartner = subject;
                _broadcastSourceId = actorId;

                int effectiveDepth = depth > _linkDispatchDepth ? depth : _linkDispatchDepth;
                _linkDispatchDepth++;
                for (int i = 0; i < holders.Count; i++)
                {
                    if (IsBattleOver()) break;
                    if (!holders[i].Alive) continue;
                    bool fired = TriggerPassive(holders[i], trigger, effectiveDepth);

                    // THE RECORD IS SPENT BY BEING ANSWERED. A link's Charges were stamped from the
                    // day links existed and taken only by the funnel's Redirect, Echo and Deflect
                    // branches, so a link read by a TRIGGER kept its charge and fired again on the
                    // next hit - "the next time either recorded target is attacked" and "the next
                    // allied hit against it" were both unbounded, which is the shape both kits the
                    // trigger was built for actually need.
                    //
                    // Spent only when a passive ACTUALLY FIRED, which is what TriggerPassive's
                    // return value is for. A holder with nothing listening, or whose listener was
                    // refused by its own once-per-round clock, has not used its record and must not
                    // pay for the event. That is also what lets one link serve two separate
                    // watchers without the first to be asked silently consuming it.
                    //
                    // Charges of 0 are unlimited, as everywhere else, so every link authored before
                    // this keeps firing exactly as it did.
                    if (fired) SpendLinkCharge(holders[i], FindLinkTo(holders[i], subject.Id));
                }
                _linkDispatchDepth--;

                _broadcastSourceId = prevSourceId;
                _linkPartner = prevPartner;
                _broadcastVictim = prevVictim;
                _broadcastSubject = prevSubject;
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
            /// A unit that has already acted is not in the tail and is not moved THIS round, which
            /// is the termination argument for the round loop and the reason the "advances my own
            /// next turn" family in the roster is a Speed buff rather than this. A DELAY aimed at
            /// such a unit is carried to the top of the next round instead of being dropped - see
            /// CarriedDelay - and that carry adds no entry to the live order either, so the
            /// termination argument is untouched.
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
                int at = first < _order.Count ? _order.IndexOf(target, first) : -1;

                // NOT IN THE REMAINING ORDER, which is a unit that has already acted this round.
                // A delay carries to its next turn rather than evaporating - see CarriedDelay for
                // why, and for why an advance does not.
                if (at < 0)
                {
                    if (fraction < 0f)
                    {
                        CarriedDelay carry = new CarriedDelay();
                        carry.Source = source;
                        carry.Target = target;
                        carry.Fraction = fraction;
                        carry.BanksAs = banksAs;
                        carry.Cap = cap;
                        carry.Rate = rate;
                        carry.DebtDuration = debtDuration;
                        _carriedDelays.Add(carry);
                    }
                    return;
                }

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
                // The realised fraction, banked per caster so a later effect in the same ability
                // can read what the shift actually cost. The same number BankTurnDebt uses, and
                // for the same reason: it is what the move cost in places over the tail, after
                // the ceil and both clamps, rather than the fraction somebody authored.
                if (source != null)
                    _lastTurnShift[source.Id] = (destination - at) / (float)tail;

                if (banksAs != StatusKind.None && destination > at)
                {
                    float realised = (destination - at) / (float)tail;
                    BankTurnDebt(source, target, banksAs, realised, cap, rate, debtDuration);
                }

                if (destination > at && source != null) FireTurnDelayed(source, target);
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
                    int prevTdtSrc = _broadcastSourceId;
                    _broadcastSourceId = source.Id;
                    BroadcastPassive(source.IsPlayer, PassiveTrigger.OnTurnDebtThreshold, 0, null);
                    _broadcastSourceId = prevTdtSrc;
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

                // A SWAP HAS TWO SUBJECTS. `b` was last in the pending order and is now first, so
                // it moved forward; `a` went the other way. Recorded here because this is the only
                // place that knows which was which, and read by the two target modes that name
                // them. Cleared when the ability ends, so they resolve to nobody outside it.
                _swapAdvanced = b;
                _swapDelayed = a;

                BattleEvent e = Emit(BattleEventType.TurnOrderShift, source != null ? source.Id : -1, b.Id);
                e.Status = StatusKind.None;
                e.Value = earliest - latest;

                // `a` is the body that went backwards, which is what "delayed" names.
                if (source != null) FireTurnDelayed(source, a);
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
            /// <summary>
            /// WHAT A UNIT WITH NO LEGAL TARGET DOES: it picks anyway, with the restriction set
            /// aside for that one pick.
            ///
            /// The alternatives were a turn that evaporates and a pick made arbitrarily. A turn
            /// that evaporates is the worse of the two by a distance: a fight where one side cannot
            /// act does not end, it runs to maxRounds and reports a result no decision produced -
            /// and the campaign's whole difficulty model is built on battles that resolve. An
            /// arbitrary pick is not definable in a simulation that has to replay identically.
            ///
            /// So the restriction is dropped rather than the turn, which is EXACTLY the trade
            /// concealment already makes one method down: "the last body is always targetable",
            /// there for the same reason and decided the same way. A restriction is a tempo rule,
            /// not a way to make a unit inert, and it costs the last banned body its protection.
            ///
            /// The retry runs only when the ban actually refused somebody - `_targetBanRejections`
            /// is what distinguishes "the rule emptied this list" from "this mode had no answer
            /// anyway", so a LinkedUnit with no link does not resolve twice - and only for a unit
            /// carrying a restriction at all, so nothing in shipped content reaches the second call
            /// and no mode that draws from the RNG stream can draw from it twice.
            /// </summary>
            private List<CombatUnit> ResolveTargets(CombatUnit caster, TargetMode mode, CombatUnit excluded,
                                                    StatusKind targetStatus)
            {
                if (caster == null || _targetBanRelaxed || !HasAnyTargetBan(caster))
                    return ResolveTargetsInner(caster, mode, excluded, targetStatus);

                int before = _targetBanRejections;
                List<CombatUnit> picked = ResolveTargetsInner(caster, mode, excluded, targetStatus);
                if (picked.Count > 0 || _targetBanRejections == before) return picked;

                _targetBanRelaxed = true;
                List<CombatUnit> relaxed = ResolveTargetsInner(caster, mode, excluded, targetStatus);
                _targetBanRelaxed = false;
                return relaxed;
            }

            private List<CombatUnit> ResolveTargetsInner(CombatUnit caster, TargetMode mode, CombatUnit excluded,
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
                            if (!u.Alive || u.IsPlayer != caster.IsPlayer || u == excluded || Rejected(caster, u)) continue;
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

                    // The ally modes below return from HERE, above the anchor lookup, exactly
                    // as LowestHpAlly and RandomAlly already do. The anchor answers single-enemy
                    // questions aimed at one side; it has nothing to say about which of the
                    // caster's own allies an effect lands on.

                    case TargetMode.LowestEnergyAlly:
                    {
                        CombatUnit best = null;
                        for (int i = 0; i < _units.Count; i++)
                        {
                            CombatUnit u = _units[i];
                            if (!u.Alive || u.IsPlayer != caster.IsPlayer || u == excluded || Rejected(caster, u)) continue;
                            if (best == null || u.Energy < best.Energy
                                || (u.Energy == best.Energy && u.Slot < best.Slot)) best = u;
                        }
                        if (best != null) result.Add(best);
                        return result;
                    }

                    case TargetMode.HighestEnergyAlly:
                    {
                        // LowestEnergyAlly's mirror, and deliberately its exact mirror: the same
                        // raw Energy read, the same front-slot tie-break, the same return from
                        // above the anchor lookup. A donor and a recipient picked by two rules
                        // that disagreed in any detail would move Energy for reasons nobody wrote.
                        CombatUnit best = null;
                        for (int i = 0; i < _units.Count; i++)
                        {
                            CombatUnit u = _units[i];
                            if (!u.Alive || u.IsPlayer != caster.IsPlayer || u == excluded || Rejected(caster, u)) continue;
                            if (best == null || u.Energy > best.Energy
                                || (u.Energy == best.Energy && u.Slot < best.Slot)) best = u;
                        }
                        if (best != null) result.Add(best);
                        return result;
                    }

                    case TargetMode.LargestShieldAlly:
                    {
                        // The POOL, not the status entry. CombatUnit.Shield is how much is left to
                        // absorb; StatusKind.Shield is only the marker that says a pool exists and
                        // carries a duration instead of an amount.
                        //
                        // Nobody is the answer when no ally holds a pool. Returning the front body
                        // there would hand every "move the largest Shield" clause a silent no-op
                        // target that reads as a hit, and this engine already answers that kind of
                        // question with an empty list - see HighestPendingDoTEnemy.
                        CombatUnit best = null;
                        for (int i = 0; i < _units.Count; i++)
                        {
                            CombatUnit u = _units[i];
                            if (!u.Alive || u.IsPlayer != caster.IsPlayer || u == excluded || Rejected(caster, u)) continue;
                            if (u.Shield <= 0f) continue;
                            if (best == null || u.Shield > best.Shield
                                || (u.Shield == best.Shield && u.Slot < best.Slot)) best = u;
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
                            if (!u.Alive || u.IsPlayer != caster.IsPlayer || u == excluded || Rejected(caster, u)) continue;
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

                    // BEFORE THE ANCHOR, for the identical reason BroadcastVictim is: a Link names
                    // one specific body and that body may stand on either side - an ally for a ward,
                    // an enemy for a paired mark. A Taunt holder answering here would hand back a
                    // unit the link was never bound to, which would make the mode mean nothing.
                    //
                    // Two sources, in order. Inside a link dispatch _linkPartner is already the far
                    // end and is read straight: a passive answering "my partner was hit" does not
                    // have to re-derive which body that was, and reading the caster's own entry
                    // instead would pick the wrong one from a unit holding two links. Outside a
                    // dispatch it falls back to the caster's own first live link.
                    //
                    // Resolves to NOBODY when there is no link, rather than to a front-enemy
                    // fallback. "The unit I am linked to" has no referent when nothing is linked,
                    // and quietly aiming a bound effect at whoever happens to be in front would be
                    // the mode answering a question it was not asked.
                    case TargetMode.LinkedUnit:
                    {
                        if (_linkPartner != null && _linkPartner.Alive && _linkPartner != excluded)
                        {
                            result.Add(_linkPartner);
                            return result;
                        }
                        for (int i = 0; i < caster.Statuses.Count; i++)
                        {
                            StatusInstance link = caster.Statuses[i];
                            if (link.Kind != StatusKind.Linked || link.LinkedToId < 0) continue;
                            CombatUnit partner = FindUnit(link.LinkedToId);
                            if (partner == null || !partner.Alive || partner == excluded) continue;
                            result.Add(partner);
                            return result;
                        }
                        return result;
                    }

                    // GAP 4. The caster's link partner with the highest current HP, skipping the
                    // partner of the dispatch in flight - which is the body that was just hit and
                    // the one LinkedUnit already answers with. With two records that leaves
                    // exactly one candidate; with three it is "the recorded target with the
                    // highest present HP", which is the same sentence a talent asks for.
                    //
                    // Before the anchor with the other link modes.
                    case TargetMode.OtherLinkedUnit:
                    {
                        CombatUnit best = null;
                        for (int i = 0; i < caster.Statuses.Count; i++)
                        {
                            StatusInstance link = caster.Statuses[i];
                            if (link.Kind != StatusKind.Linked || link.LinkedToId < 0) continue;
                            CombatUnit partner = FindUnit(link.LinkedToId);
                            if (partner == null || !partner.Alive || partner == excluded) continue;
                            if (_linkPartner != null && partner == _linkPartner) continue;
                            if (best == null || partner.Hp > best.Hp
                                || (partner.Hp == best.Hp && partner.Slot < best.Slot)) best = partner;
                        }
                        if (best != null) result.Add(best);
                        return result;
                    }

                    // GAP 4's set half. Every living partner at once, deduplicated so a caster
                    // holding two links to one body reaches it once. Not skipped for concealment,
                    // like every other multi-target mode.
                    case TargetMode.AllLinkedUnits:
                    {
                        for (int i = 0; i < caster.Statuses.Count; i++)
                        {
                            StatusInstance link = caster.Statuses[i];
                            if (link.Kind != StatusKind.Linked || link.LinkedToId < 0) continue;
                            CombatUnit partner = FindUnit(link.LinkedToId);
                            if (partner == null || !partner.Alive || partner == excluded) continue;
                            if (result.Contains(partner)) continue;
                            result.Add(partner);
                        }
                        return result;
                    }

                    // GAP 8. The unit that CAUSED the broadcast, as a target rather than only as
                    // something to test. Before the anchor, because it can stand on either side -
                    // a healer, an attacker, the ally whose blow triggered a link.
                    case TargetMode.BroadcastSource:
                    {
                        CombatUnit causer = FindUnit(_broadcastSourceId);
                        if (causer != null && causer.Alive && causer != excluded) result.Add(causer);
                        return result;
                    }

                }

                // THE HAND-DRIVEN PICK, and the only line in this method that is not about the
                // game. `_forcedPrimary` is null in every battle the game resolves - the guard is a
                // null test, draws nothing from the RNG and cannot move a fingerprint - and is set
                // only while the Dev Arena is resolving an action a person chose.
                //
                // It replaces the SINGLE-ENEMY pick and nothing else, which is why it sits here:
                // every ally mode, every link mode and every multi-target mode has already
                // returned above, so an AoE resolves exactly as authored. It is subordinate to the
                // engine, not above it - Provoked and the Taunt anchor are checked below and both
                // win, and a pick the actor is Restricted from or that is Concealed is refused.
                // Each of those cases is reported rather than silently swallowed.
                if (_forcedPrimary != null && caster == _forcedCaster)
                {
                    CombatUnit f = _forcedPrimary;
                    string no = null;
                    if (f == excluded) no = "the ability excludes it";
                    else if (!f.Alive) no = "it is dead";
                    else if (f.IsPlayer == caster.IsPlayer) no = "it is not an enemy of " + caster.Name;
                    else if (TargetBanned(caster, f)) no = caster.Name + " is Restricted from targeting it";
                    else if (Concealed(f)) no = "it is concealed";
                    if (no != null)
                    {
                        NoteForcedPick("refused: " + no + " - the ability picked its own target");
                    }
                    else
                    {
                        // The anchor still answers for its side, exactly as it does for a natural
                        // pick. Checked here rather than left to the block below so the reason can
                        // be reported with the name of the body that intercepted.
                        CombatUnit taunt = FindTargetAnchor(!caster.IsPlayer);
                        if (taunt != null && taunt != excluded && !TargetBanned(caster, taunt) && taunt != f)
                        {
                            NoteForcedPick("redirected by Taunt: " + taunt.Name
                                           + " answered for its side instead of " + f.Name);
                        }
                        else
                        {
                            NoteForcedPick("landed on " + f.Name);
                            result.Add(f);
                            return result;
                        }
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
                    // A COMPULSION CANNOT REACH A BODY THE PICKER IS FORBIDDEN. Same placement as
                    // concealment, and the same reasoning: a provoke that outranked the restriction
                    // would let any enemy carrying Provoked walk a unit past its own rule.
                    if (TargetBanned(caster, provoker)) continue;
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
                // AND THE ANCHOR IS SUBJECT TO IT, exactly as it is to concealment. A Taunt holder
                // the picker may not pick stops answering for its side rather than forcing a blow
                // the picker's own rule forbids - which is the trade the paragraph above already
                // made for a Concealed taunter.
                if (anchor != null && TargetBanned(caster, anchor)) anchor = null;
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
                        // INSIDE A BROADCAST ABOUT SOMEBODY ELSE, THAT BODY'S ATTACKER - the one
                        // ConditionSubject.LastAttacker has always named. The two used to disagree:
                        // the condition read the broadcast subject and this read the caster, so one
                        // passive could gate on the enemy that just hit an ally and then strike the
                        // enemy that last hit its owner. Caldus's No Uncertain Ground is that
                        // passive - "when the Declared Target attacks another ally, Caldus deals 45%
                        // of Attack damage to THAT enemy" checked the Declared Target and hit
                        // whoever had last reached Caldus. At OnDamaged the subject IS the caster,
                        // so every counterattack written against the caster's own attacker is
                        // untouched.
                        CombatUnit about = _broadcastSubject != null && _broadcastSubject != caster
                                           ? _broadcastSubject : caster;
                        CombatUnit remembered = FindUnit(about.LastAttackerId);
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

                    // The one before the current one. A sibling of LastTarget in every respect -
                    // same anchor rule, same four gates, same FrontEnemy fallback - reading
                    // PreviousTargetId instead of LastTargetId.
                    //
                    // The two fields are genuinely different and this is the mode that shows why:
                    // ApplyDamageEffect overwrites LastTargetId as it sets a hit up, so by the time
                    // anything asks, "last" already names the body being hit right now. Previous is
                    // the one before that, and is only written when the target actually CHANGES, so
                    // an ability hitting the same body twice does not shuffle its own history and
                    // leave Previous pointing at that same body.
                    case TargetMode.PreviousTarget:
                    {
                        CombatUnit earlier = FindUnit(caster.PreviousTargetId);
                        if (earlier != null && earlier.Alive && earlier != excluded && earlier.IsPlayer != caster.IsPlayer)
                        {
                            result.Add(earlier);
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
                        // THE ONE SINGLE-TARGET PICK THAT GOES THROUGH THE MULTI-TARGET COLLECTOR,
                        // so the restriction has to be applied here rather than inside it -
                        // CollectSide also serves AllEnemies, which a ban must not thin. Filtered
                        // BEFORE the draw, so the roll is over the legal set rather than over a
                        // set it may have to reject afterwards; for a unit with no restriction the
                        // list is untouched and the draw is the same one.
                        for (int i = enemies.Count - 1; i >= 0; i--)
                            if (TargetBanned(caster, enemies[i])) enemies.RemoveAt(i);
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
                            if (!u.Alive || u.IsPlayer == caster.IsPlayer || u == excluded || Rejected(caster, u)) continue;
                            if (!u.HasMarkFrom(markKind, caster.Id)) continue;
                            if (picked == null || u.Slot < picked.Slot) picked = u;
                        }
                        break;
                    }

                    // Deepest-stack variant of EnemyWithMyMark: the enemy carrying the most
                    // entries of the caster's mark kind from this caster specifically. Ties broken
                    // by lowest slot. No marked enemy → nobody.
                    case TargetMode.HighestMyMarkStacksEnemy:
                    {
                        StatusKind markKind = MarkKindOf(targetStatus);
                        int bestCount = 0;
                        for (int i = 0; i < _units.Count; i++)
                        {
                            CombatUnit u = _units[i];
                            if (!u.Alive || u.IsPlayer == caster.IsPlayer || u == excluded || Rejected(caster, u)) continue;
                            int count = u.CountOfFrom(markKind, caster.Id);
                            if (count <= 0) continue;
                            if (count > bestCount || (count == bestCount && (picked == null || u.Slot < picked.Slot)))
                            {
                                picked = u;
                                bestCount = count;
                            }
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

                    // The two bodies a swap moved. Alive-gated where the corpse above is not:
                    // a swap moves the living, and a body that died between the swap and the
                    // effect reading it is not a body the effect can do anything to.
                    case TargetMode.SwapAdvancedUnit:
                        picked = _swapAdvanced != null && _swapAdvanced.Alive && _swapAdvanced != excluded
                            ? _swapAdvanced : null;
                        break;

                    case TargetMode.SwapDelayedUnit:
                        picked = _swapDelayed != null && _swapDelayed.Alive && _swapDelayed != excluded
                            ? _swapDelayed : null;
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
                            if (!u.Alive || u.IsPlayer == caster.IsPlayer || u == excluded || Rejected(caster, u)) continue;
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
                            if (!u.Alive || u.IsPlayer == caster.IsPlayer || u == excluded || Rejected(caster, u)) continue;
                            if (picked == null || u.DamageDealtThisRound > picked.DamageDealtThisRound
                                || (u.DamageDealtThisRound == picked.DamageDealtThisRound && u.Slot < picked.Slot))
                                picked = u;
                        }
                        break;
                    }

                    // GAP GROUP 8: the last act rather than the biggest total.
                    // HighestDamageDealtEnemy one case up is a career reading, so the enemy that
                    // opened hard and has done nothing since still wins it and the one that just
                    // struck an ally is not named.
                    //
                    // Resolves to NOBODY when no enemy has hurt the caster's line yet, or when the
                    // one that did is dead, excluded or refused by a target ban - the same four
                    // tests every mode beside it applies, rather than a fallback to the front
                    // body, which would answer a different question in the first round of every
                    // fight.
                    case TargetMode.EnemyThatLastDamagedAlly:
                    {
                        int lastId = caster.IsPlayer ? _lastPlayerSideAttackerId : _lastEnemySideAttackerId;
                        CombatUnit last = lastId >= 0 ? FindUnit(lastId) : null;
                        if (last != null && last.Alive && last.IsPlayer != caster.IsPlayer
                            && last != excluded && !Rejected(caster, last))
                            picked = last;
                        break;
                    }

                    case TargetMode.HighestDoTDealtEnemy:
                    {
                        for (int i = 0; i < _units.Count; i++)
                        {
                            CombatUnit u = _units[i];
                            if (!u.Alive || u.IsPlayer == caster.IsPlayer || u == excluded || Rejected(caster, u)) continue;
                            if (picked == null || u.DoTDamageDealtThisRound > picked.DoTDamageDealtThisRound
                                || (u.DoTDamageDealtThisRound == picked.DoTDamageDealtThisRound && u.Slot < picked.Slot))
                                picked = u;
                        }
                        break;
                    }

                    // The carried-DoT pick, ranked with the same helper a removal sums, so the
                    // enemy chosen as "the most to remove" and the amount credited for removing it
                    // are two readings of one number rather than two implementations of one idea.
                    //
                    // An enemy carrying nothing scores zero and is never preferred over one that
                    // carries something; when NO enemy carries any DoT the mode picks nobody,
                    // which is the honest answer. That is deliberately unlike the stat picks
                    // above, where every living enemy is a candidate because every one has a stat.
                    case TargetMode.HighestPendingDoTEnemy:
                    {
                        float best = 0f;
                        for (int i = 0; i < _units.Count; i++)
                        {
                            CombatUnit u = _units[i];
                            if (!u.Alive || u.IsPlayer == caster.IsPlayer || u == excluded || Rejected(caster, u)) continue;
                            float pending = 0f;
                            for (int s = 0; s < u.Statuses.Count; s++) pending += RemainingDotDamage(u.Statuses[s], u);
                            if (pending <= 0f) continue;
                            if (picked == null || pending > best || (pending == best && u.Slot < picked.Slot))
                            {
                                picked = u;
                                best = pending;
                            }
                        }
                        break;
                    }

                    default:
                    {
                        for (int i = 0; i < _units.Count; i++)
                        {
                            CombatUnit u = _units[i];
                            if (!u.Alive || u.IsPlayer == caster.IsPlayer || u == excluded || Rejected(caster, u)) continue;
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
                            else if (mode == TargetMode.LowestEnergyEnemy && u.Energy < picked.Energy) picked = u;
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
                    if (!u.Alive || u.IsPlayer != caster.IsPlayer || u == excluded || Rejected(caster, u)) continue;
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

                    case RecordKind.HpFractionBand:
                    {
                        // A fraction of where the carrier stands RIGHT NOW, folded into a running
                        // low and a running high. Both move on the first write, which is what
                        // makes a single recorded value its own lowest and highest rather than
                        // leaving one of them unset.
                        float band = target.Hp * effect.Magnitude;
                        if (band <= 0f) break;
                        if (entry.StoredLowHp < 0f || band < entry.StoredLowHp) entry.StoredLowHp = band;
                        if (entry.StoredHighHp < 0f || band > entry.StoredHighHp) entry.StoredHighHp = band;
                        break;
                    }

                    case RecordKind.MinHpRatio:
                        // A running minimum: it only ever moves down while the entry lives, which
                        // is the difference between "the lowest it went" and "where it is now".
                        if (entry.StoredMin < 0f || target.HpRatio < entry.StoredMin)
                            entry.StoredMin = target.HpRatio;
                        break;
                }
            }

            /// <summary>
            /// Writes one half of a link: a StatusKind.Linked entry on `holder` naming `partner`,
            /// applied by `source`.
            ///
            /// Three units, which is why this could not be an ApplyStatus. SourceId stays the hero
            /// that created the binding, exactly as it does for a Mark or a Ledger, so "my link"
            /// means the same thing "my mark" does; LinkedToId is the far end.
            ///
            /// Entries are told apart by (kind, source, partner), not by (kind, source) the way
            /// AddOrRefresh tells statuses apart. That is the one place a link genuinely differs
            /// from every other status: one hero can legitimately hold Talren bound to two separate
            /// enemies at once, and folding the second binding into the first would silently
            /// replace one partner with the other. So an existing entry naming the SAME partner is
            /// refreshed, and one naming a different partner is left alone and a second entry
            /// added.
            ///
            /// Sets `_anyLinks`. That bool is the gate every link dispatch point in the funnel
            /// tests first, and this is the only thing in the engine that opens it.
            /// </summary>
            private void StampLink(CombatUnit source, CombatUnit holder, CombatUnit partner,
                                   EffectDef effect, int duration)
            {
                StatusInstance entry = null;
                for (int i = 0; i < holder.Statuses.Count; i++)
                {
                    StatusInstance s = holder.Statuses[i];
                    if (s.Kind != StatusKind.Linked) continue;
                    if (s.SourceId != source.Id || s.LinkedToId != partner.Id) continue;
                    entry = s;
                    break;
                }

                if (entry == null)
                {
                    entry = new StatusInstance();
                    entry.Kind = StatusKind.Linked;
                    entry.SourceId = source.Id;
                    holder.Statuses.Add(entry);
                }

                // Refresh keeps the stronger and the longer, the rule AddOrRefresh already holds
                // to, so re-binding a pair cannot weaken a link already standing.
                entry.LinkedToId = partner.Id;
                if (effect.Magnitude > entry.Magnitude) entry.Magnitude = effect.Magnitude;
                if (duration > entry.RoundsLeft) entry.RoundsLeft = duration;
                entry.LinkTransfer = effect.LinkTransfer;
                entry.Charges = effect.Charges;
                // GAP 2: scaled once, here, off the applier's live Attack - the same convention
                // Burn's per-tick magnitude and the Ledger's StoredCap follow, so a link means the
                // same thing at every level and cannot re-read a stat buffed after it was made.
                entry.LinkAmount = effect.LinkAmountPower > 0f
                    ? source.EffectiveAttack * effect.LinkAmountPower : 0f;
                // GAP 3.
                entry.LinkFromCasterOnly = effect.LinkFromCaster;
                if (effect.UsesPerRound > 0)
                {
                    entry.UsesPerRound = effect.UsesPerRound;
                    entry.UsesThisRound = 0;
                }

                _anyLinks = true;

                BattleEvent e = Emit(BattleEventType.StatusApplied, source.Id, holder.Id);
                e.Status = StatusKind.Linked;
                e.Duration = entry.RoundsLeft;
                // The partner's id rather than a stack depth. Every other StatusApplied writes
                // CountOf here, but a link's identity is WHO it names, and a screen or a check
                // reading two Linked entries apart has no other way to tell them apart - a
                // StatusKind carries no per-instance name. No existing battle records a Linked
                // event, so nothing that reads Value for a stack depth is affected.
                e.Value = partner.Id;
            }

            private void MoveStatus(CombatUnit from, CombatUnit to, StatusInstance status, bool removeFromSource)
            {
                MoveStatus(from, to, status, removeFromSource, 0);
            }

            /// <summary>
            /// `newDuration` of 0 keeps the entry's own RoundsLeft, which is what every caller did
            /// before this and what makes a move create no value. A positive value RE-CLOCKS the
            /// entry as it lands.
            ///
            /// Six abilities in the roster move a set of statuses and name a duration in the same
            /// breath - "moves its debuffs to the highest-HP enemy FOR 2 ROUNDS", "transfers its
            /// buffs to the lowest-HP enemy FOR 1 ROUND" - and the set is unknown at authoring
            /// time, so the remove-and-reapply idiom Seraphine uses cannot serve them: you cannot
            /// re-apply statuses you cannot name, and doing so would lose whatever they recorded.
            ///
            /// It is still a move rather than a grant: magnitude, Stored, charges and every record
            /// cross untouched. Only the clock is authored, and only when an effect says so.
            /// </summary>
            private void MoveStatus(CombatUnit from, CombatUnit to, StatusInstance status,
                                    bool removeFromSource, int newDuration)
            {
                if (to == null || status == null) return;

                if (removeFromSource && from != null) from.Statuses.Remove(status);

                int rounds = newDuration > 0 ? newDuration : status.RoundsLeft;
                to.AddOrRefresh(status.Kind, rounds, status.Magnitude, status.SourceId);

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
                    landed.StoredLowHp = status.StoredLowHp;
                    landed.StoredHighHp = status.StoredHighHp;
                    landed.UsesPerRound = status.UsesPerRound;
                    landed.UsesThisRound = status.UsesThisRound;
                    landed.SkillOnly = status.SkillOnly;
                    landed.HolderHpTick = status.HolderHpTick;
                    landed.HolderHpTickCurrent = status.HolderHpTickCurrent;
                    landed.AreaHitsOnly = status.AreaHitsOnly;
                    landed.TickRecord = status.TickRecord;
                    landed.NoExtend = status.NoExtend;
                    landed.BanKind = status.BanKind;
                    landed.BanStatus = status.BanStatus;

                    // AND THE LEDGER'S RULES, NOT ONLY ITS BALANCE. These nine were the third
                    // field-by-field copy in this engine found dropping fields, and the only one
                    // of the three that shipped content can reach today: Ledger is not harmful, so
                    // MoveBuffs moves it, Vaelis's active moves positive effects between two
                    // enemies, and Sylven's Scar Record IS a Ledger. A Scar Record moved by him
                    // arrived with its stored damage intact and with AutoRelease off - a record
                    // that still remembers the number and has forgotten that it is supposed to pay
                    // it out. The paragraph above promised everything the entry recorded travels
                    // with it; what it recorded did, what it was told to DO with it did not.
                    landed.AutoRelease = status.AutoRelease;
                    landed.AutoReleaseAsHeal = status.AutoReleaseAsHeal;
                    landed.AutoReleaseTarget = status.AutoReleaseTarget;
                    landed.Mitigates = status.Mitigates;
                    landed.BankLimit = status.BankLimit;
                    landed.BankUsesThisRound = status.BankUsesThisRound;
                    landed.BankHeals = status.BankHeals;
                    landed.BankOnlyFrom = status.BankOnlyFrom;
                    landed.BankFromCaster = status.BankFromCaster;
                    landed.AutoReleaseSplitHeal = status.AutoReleaseSplitHeal;
                    landed.FunnelSource = status.FunnelSource;
                    landed.FunnelConvert = status.FunnelConvert;
                    // The link's far end travels too, for the same reason the ledger's balance
                    // does: a link that lost the body it named on the way across would be a link
                    // to nobody, and the entry would silently go inert rather than move.
                    //
                    // A moved link is NOT re-pointed at its new carrier. If a Link naming B moves
                    // from A to C, C is now linked to B. That is what moving a binding means, and
                    // it is also what makes moving one onto the partner itself (C == B) produce a
                    // self-link, which the funnel refuses - see RedirectShare's destination test.
                    landed.LinkedToId = status.LinkedToId;
                    landed.LinkTransfer = status.LinkTransfer;
                    landed.LinkAmount = status.LinkAmount;
                    landed.LinkFromCasterOnly = status.LinkFromCasterOnly;
                    break;
                }

                BattleEvent e = Emit(BattleEventType.StatusSpread, from != null ? from.Id : -1, to.Id);
                e.Status = status.Kind;
                // The clock the entry LANDED with, which is the one it left with unless the move
                // re-clocked it. Reporting the old number would have the record disagree with the
                // board for every authored move duration.
                e.Duration = rounds;
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
            /// <summary>
            /// Concealment and the picker's own restriction, asked as one question because they
            /// are the same kind of answer: this body is not a legal candidate for this pick.
            ///
            /// Every single-target site in the resolver asks it. The multi-target collectors do
            /// not, which is deliberate and is the rule concealment already set: a restriction
            /// governs what a unit PICKS, and AllEnemies picks nothing - it takes a side. A ban
            /// that thinned a sweep would be damage immunity wearing a targeting rule's clothes.
            /// </summary>
            private bool Rejected(CombatUnit caster, CombatUnit unit)
            {
                return Concealed(unit) || TargetBanned(caster, unit);
            }

            /// <summary>
            /// The picker's standing restriction, applied to one candidate.
            ///
            /// Returns false immediately for a unit carrying no restriction, which is every unit
            /// in shipped content - so this is one enum comparison on a path that every hero and
            /// every enemy walks, and it cannot reject a candidate it was never asked about.
            ///
            /// `_targetBanRelaxed` is the empty-list fallback, described on ResolveTargets.
            /// `_targetBanRejections` counts the refusals so the fallback can tell "the ban emptied
            /// this" apart from "this mode had no answer anyway".
            /// </summary>
            /// <summary>Whether this unit is restricted at all, by either form. One enum test for
            /// the standing form and, only if that is None, a walk of a status list that is empty
            /// of Restricted entries for every unit in shipped content.</summary>
            private bool HasAnyTargetBan(CombatUnit caster)
            {
                if (caster.TargetBan != TargetBan.None) return true;
                for (int i = 0; i < caster.Statuses.Count; i++)
                    if (caster.Statuses[i].Kind == StatusKind.Restricted
                        && caster.Statuses[i].BanKind != TargetBan.None) return true;
                return false;
            }

            private bool TargetBanned(CombatUnit caster, CombatUnit unit)
            {
                if (caster == null || unit == null) return false;
                if (_targetBanRelaxed) return false;

                // THE STANDING FORM AND THE APPLIED ONE, in that order, and the first match wins.
                // A passive-declared ban is a property of the unit and was the only shape there
                // was; an entry-borne one arrives mid-battle and leaves with its status. Both are
                // the same question asked of the same candidate, so they share the switch below
                // rather than growing a second copy of it that could disagree.
                TargetBan kind = caster.TargetBan;
                StatusKind banStatus = caster.TargetBanStatus;
                if (kind == TargetBan.None)
                    for (int i = 0; i < caster.Statuses.Count; i++)
                    {
                        StatusInstance rs = caster.Statuses[i];
                        if (rs.Kind != StatusKind.Restricted || rs.BanKind == TargetBan.None) continue;
                        kind = rs.BanKind;
                        banStatus = rs.BanStatus;
                        break;
                    }
                if (kind == TargetBan.None) return false;

                bool banned;
                switch (kind)
                {
                    case TargetBan.LastTarget:
                        banned = caster.LastTargetId >= 0 && unit.Id == caster.LastTargetId;
                        break;
                    case TargetBan.LastRoundTarget:
                        banned = caster.LastRoundTargetId >= 0 && unit.Id == caster.LastRoundTargetId;
                        break;
                    case TargetBan.CarryingStatus:
                        banned = banStatus != StatusKind.None && unit.Has(banStatus);
                        break;
                    default:
                        banned = false;
                        break;
                }
                if (banned) _targetBanRejections++;
                return banned;
            }

            /// <summary>True while a pick is being retried with the restriction set aside.</summary>
            // ---------------------------------------------------------------- hand-driven state
            //
            // All of it inert during Run(): the two fields below are null, the notes list is null,
            // and every read of them is a null test. No RNG draw, no event and no branch that a
            // battle the game plays can reach.

            private CombatUnit _forcedPrimary;
            private CombatUnit _forcedCaster;
            private List<string> _forcedNotes;

            private void OpenForcedPick(CombatUnit caster, int forcedTargetId)
            {
                if (forcedTargetId < 0) return;
                CombatUnit f = FindUnit(forcedTargetId);
                if (f == null) { NoteForcedPick("refused: no unit with id " + forcedTargetId); return; }
                _forcedPrimary = f;
                _forcedCaster = caster;
            }

            private void CloseForcedPick()
            {
                _forcedPrimary = null;
                _forcedCaster = null;
            }

            private void NoteForcedPick(string note)
            {
                if (_forcedNotes != null) _forcedNotes.Add(note);
            }

            private bool _targetBanRelaxed;

            /// <summary>Candidates the restriction has refused during the pick in flight.</summary>
            private int _targetBanRejections;

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
