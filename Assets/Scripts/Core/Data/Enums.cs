using System;

namespace Emberfall.Core.Data
{
    /// <summary>Elemental identity of a unit. Drives the counter chart and colour identity.</summary>
    public enum Aspect
    {
        Ember,   // fire / aggression
        Tide,    // water / control
        Verdant, // growth / sustain
        Gale,    // wind / speed
        Umbra,   // shadow
        Lumen    // light
    }

    /// <summary>Combat archetype. Purely descriptive - behaviour comes from the ability data.</summary>
    public enum HeroRole
    {
        Bulwark, // tank
        Striker, // single target damage
        Breaker, // area damage
        Mender,  // healer
        Warden,  // support
        Stalker, // assassin
        Ranger   // ranged / mixed single-target and line damage
    }

    public enum AbilityKind
    {
        BasicAttack,
        Active,
        Passive
    }

    public enum TargetMode
    {
        Self,
        LowestHpEnemy,
        HighestHpEnemy,
        HighestAttackEnemy,
        FrontEnemy,
        RandomEnemy,
        AllEnemies,
        LowestHpAlly,
        AllAllies,
        RandomAlly,
        LastAttacker,
        LastTarget,

        // ---------- appended, never inserted ----------
        //
        // Same rule as StatusKind above, and the same reason with one extra edge: ResolveTargets
        // writes (int)mode into a TargetAnchored event's Value, so renumbering an existing mode
        // rewrites what every anchored battle recorded about why it was anchored.
        //
        // THE ANCHOR APPLIES TO EVERY SINGLE-ENEMY MODE BELOW, unchanged by this pass. A living
        // Lumen unit, or failing that a Taunt holder, answers every single-enemy targeting question
        // aimed at its side, so EnemyWithStatus and the three stat picks collapse onto it whenever
        // one exists. That is the Lumen identity working as designed rather than an oversight, and
        // it is deliberately NOT altered here - see DESIGN-BUILD-ORDER's entry for this branch,
        // which flags the one case where it reads badly and leaves the decision to a human.
        //
        // The four ALLY modes are unaffected, because the anchor only ever answers for the enemy
        // side: they return from the first switch in ResolveTargets, above the anchor lookup,
        // exactly as LowestHpAlly and RandomAlly already do.

        /// <summary>
        /// One enemy carrying EffectDef.TargetStatus, front-most first. The single-target half of
        /// the roster's largest targeting group ("deal 245% to a Forsaken enemy").
        ///
        /// There is deliberately no AllEnemiesWithStatus beside it: AllEnemies plus an
        /// EffectCondition of HasStatus already does exactly that, per target, and a second way to
        /// say the same thing is a second thing to keep in sync.
        /// </summary>
        EnemyWithStatus,

        HighestEnergyEnemy,
        HighestSpeedEnemy,

        /// <summary>Slowest living enemy, by EffectiveSpeed - so a Slow already applied counts,
        /// which is what "the slowest enemy" means to a player watching the turn order.</summary>
        LowestSpeedEnemy,

        LowestEnergyAlly,

        /// <summary>Both of the two lowest-HP allies, as one effect. Returns one unit on a line of
        /// one, and the same ordering SecondLowestHpAlly reads.</summary>
        TwoLowestHpAllies,

        SecondLowestHpAlly,

        /// <summary>The ally carrying the most harmful statuses, counted with StatusInfo.IsHarmful
        /// so it agrees with what Cleanse would remove.</summary>
        AllyWithMostDebuffs,

        /// <summary>
        /// The enemy carrying a Mark applied by the CASTER, front-most first. The targeting half of
        /// the named-mark concept: "deals 220% to the enemy with Quarry" is this, and it cannot be
        /// EnemyWithStatus because that would also answer an enemy some other hero had marked.
        ///
        /// Which mark is read comes from EffectDef.TargetStatus, so a hero carrying two says which.
        /// Defaults to Mark when TargetStatus names neither Mark nor MarkAlt.
        /// </summary>
        EnemyWithMyMark
    }

    public enum EffectKind
    {
        Damage,
        Heal,
        Shield,
        ApplyStatus,
        Cleanse,
        EnergyGain,
        Dispel,
        SwapPositions,
        GainResolve,

        // ---------- appended, never inserted ----------

        /// <summary>
        /// Moves the target earlier or later inside the CURRENT round's remaining turn order.
        /// Magnitude is signed and is a fraction of the remaining order: positive advances,
        /// negative delays. See CombatSimulation.ApplyTurnShift, which is where the whole of the
        /// "there is no ATB" argument lives.
        /// </summary>
        TurnShift,

        /// <summary>
        /// Swaps the unit closest to acting with the unit furthest from acting, among the target's
        /// own side and among the turns that have not happened yet. Needs no second target selector
        /// because both ends are derived from the order itself.
        /// </summary>
        SwapTurnOrder,

        /// <summary>Removes up to Hits instances of the NAMED Status from the target. Cleanse and
        /// Dispel remove by class and recency; this removes by name.</summary>
        RemoveStatus,

        /// <summary>Moves up to Hits harmful statuses from the target to SecondaryTarget.</summary>
        MoveDebuffs,

        /// <summary>Moves up to Hits beneficial statuses from the target to SecondaryTarget.</summary>
        MoveBuffs,

        /// <summary>Turns up to Hits beneficial statuses on the target into their paired harmful
        /// opposite, via StatusInfo.Opposite. A buff with no pairing is skipped, not removed.</summary>
        InvertStatus,

        /// <summary>
        /// Pays out the caster's Ledger entries on the target and zeroes them. Power scales the
        /// stored amount, so 1.0 pays it in full and 0.5 pays half. Positive payouts land as damage
        /// on the holder; a Heal-shaped release is authored by pointing SecondaryTarget at an ally
        /// and is deliberately not a second kind.
        /// </summary>
        LedgerRelease
    }

    /// <summary>
    /// The test an effect must pass before it resolves, or None for the effect to always resolve.
    ///
    /// One condition per effect, deliberately, and no AND/OR between them. Every conditional clause
    /// in the incoming roster that this pass is built for is a single clause - "if it is Cracked",
    /// "if the target is below 35% HP", "at 4 Patience" - and a composition operator is a small
    /// expression language that would have to be designed, parsed, printed on a hero card and
    /// tested against orderings nothing authors yet. A kit that genuinely needs two tests can carry
    /// two effects.
    ///
    /// Every member is a pure function of unit state at the moment the effect lands. Nothing here
    /// draws from the RNG and nothing here mutates: a condition that consumed a draw would shift
    /// the stream and desync every seeded replay of the battle it appeared in, and one that mutated
    /// would be an effect pretending to be a test.
    /// </summary>
    public enum EffectCondition
    {
        /// <summary>Always resolves. Every effect in shipped content, and the default.</summary>
        None,

        /// <summary>The subject carries at least one instance of ConditionStatus.</summary>
        HasStatus,

        /// <summary>The subject carries no instance of ConditionStatus.</summary>
        LacksStatus,

        /// <summary>
        /// The subject's HP ratio is at or below ConditionValue.
        ///
        /// INCLUSIVE, matching PassiveTrigger.OnHpBelowThreshold, which fires when HpRatio is at or
        /// below its Threshold. Two different meanings of "below" inside one engine would be a trap
        /// worth more than the tiny expressive difference, so the newer conditional adopts the
        /// older one's boundary rather than the other way round.
        /// </summary>
        HpBelow,

        /// <summary>The subject's HP ratio is at or above ConditionValue. The mirror of HpBelow,
        /// inclusive at the boundary for the same reason.</summary>
        HpAbove,

        /// <summary>
        /// The subject has a live shield pool absorbing for it.
        ///
        /// Reads the POOL (CombatUnit.Shield), not StatusKind.Shield on the status row, and the two
        /// can genuinely disagree: the Shield status expires on its own round clock while the pool
        /// is only ever emptied by absorption or death, so a unit can hold a pool with no status
        /// and vice versa. The pool is what actually stops damage, so it is what "has a shield"
        /// means here. A kit that wants the status row instead can author HasStatus with
        /// ConditionStatus Shield, and get a different and equally well defined answer.
        /// </summary>
        HasShield,

        /// <summary>No shield pool is absorbing for the subject. The mirror of HasShield, and the
        /// same pool.</summary>
        LacksShield,

        /// <summary>
        /// The subject carries at least ConditionValue instances of ConditionStatus, counted the
        /// way Ember's Burn stacking and EffectKind.GainResolve already count - CombatUnit.CountOf,
        /// one entry per stack. ConditionValue is rounded away from zero to an integer, because a
        /// stack count is a count.
        /// </summary>
        StatusStacksAtLeast,

        /// <summary>The subject carries a Mark (or MarkAlt, per ConditionStatus) applied by the
        /// CASTER specifically - "if it is Quarry" rather than "if anything has marked it".</summary>
        HasMyMark,

        /// <summary>The mirror of HasMyMark.</summary>
        LacksMyMark,

        /// <summary>The subject's Ledger entries applied by the caster hold at least
        /// ConditionValue between them.</summary>
        LedgerAtLeast,

        // ---------- action history ----------
        //
        // All of it is BATTLE-TIME state on CombatUnit and NONE of it reaches the save. A
        // CombatUnit is built fresh from a HeroDef and a HeroInstance at the start of every fight;
        // the save stores the instance - uid, defId, level, copies, relic, armor, selected talent -
        // and nothing about how a battle went. So this costs zero save bytes and needs no version
        // bump, which is why there is none in this branch.

        /// <summary>The subject has already taken its turn this round.</summary>
        SubjectActedThisRound,

        /// <summary>The subject has not yet acted this round.</summary>
        SubjectHasNotActedThisRound,

        /// <summary>
        /// The subject has taken damage since the CASTER's own last turn.
        ///
        /// Round-granular: it compares the round the subject was last damaged in against the round
        /// the caster last acted in. Two units acting in the same round are not ordered against
        /// each other by this, which is an approximation of "since my last turn" rather than the
        /// thing itself, and is stated here rather than hidden.
        /// </summary>
        SubjectDamagedSinceMyTurn,

        /// <summary>The subject is the one the caster's previous attack landed on.</summary>
        SubjectIsMyPreviousTarget,

        /// <summary>The subject is NOT the one the caster's previous attack landed on - "a
        /// different target than last turn".</summary>
        SubjectIsNotMyPreviousTarget,

        /// <summary>The caster used the same kind of action - Basic or Active - on each of its last
        /// two turns.</summary>
        CasterRepeatedActionType,

        /// <summary>The caster has already damaged this subject at least once this round.</summary>
        AlreadyDamagedSubjectThisRound
    }

    /// <summary>
    /// Which unit an EffectCondition asks about: the one the effect is landing on, or the one that
    /// cast it. Two members and no third, because those are the two units an effect has at the
    /// moment it resolves.
    ///
    /// Target is the default, so an unqualified condition reads as a statement about the thing
    /// being hit, which is how the descriptions this was built for are written ("if IT is below
    /// 35% HP"). Caster is what "while Hale is below 50% HP" needs.
    ///
    /// Note what this cannot express and is not trying to: a comparison BETWEEN the two, such as
    /// "the target has more Attack than the caster". That needs a stat selector and a two-sided
    /// operand model rather than a subject and a constant, and it is 2 of the 374 incoming
    /// abilities against dozens for the members above.
    /// </summary>
    public enum ConditionSubject
    {
        Target,
        Caster
    }

    public enum StatusKind
    {
        None,
        AttackUp,
        AttackDown,
        DefenseUp,
        DefenseDown,
        Burn,
        Regen,
        Shield,
        Taunt,
        Focus,   // increased critical chance
        Slow,    // reduced speed
        Silence, // blocks the Active on the holder's next turn; a Basic Attack resolves instead
        Stun,    // skips the holder's entire next turn - no Basic Attack, no Active
        Stealth, // inert placeholder — no targeting-visibility logic yet
        Resolve, // generic personal stacking counter - see EffectKind.GainResolve

        // ---------- stat statuses: the eight stats no status could move ----------
        //
        // APPENDED, never inserted, and that is not a style preference. BattleEvent.Status is
        // written into the event list and read back as an int by anything fingerprinting a battle,
        // so renumbering an existing member rewrites the record of every battle ever resolved.
        // Every member below therefore goes at the end, and any future one goes after them.
        //
        // TWO SHAPES, because UnitStats stores these stats two different ways - the same split
        // RelicStatMode and ArmorStatMode already encode, and for the same reason.
        //
        //   MULTIPLY: Speed alone, here. It is a large scaled number, so a buff scales it:
        //   Speed * (1 + SpeedUp - Slow), exactly as Attack reads AttackUp/AttackDown.
        //
        //   ADD: everything else. They are small fractions (or, for Crit Damage, a multiplier)
        //   that are never scaled by level or star rarity, so multiplying them does nothing - a
        //   multiplicative +12% on a 0.00 Block is exactly zero. A buff therefore ADDS:
        //   Block + BlockUp - BlockDown, exactly as Crit Chance already reads Focus.
        //
        // THERE IS NO SpeedDown, deliberately. Slow is already the speed debuff, it is what
        // EffectiveSpeed subtracts, and Gale's intra-round turn-order shift keys on
        // StatusKind.Slow specifically. A second speed debuff would be a duplicate mechanism that
        // silently bypasses that rule, so SpeedUp joins Slow's term rather than bringing its own
        // mirror. Crit Chance is absent for the same reason one layer over: Focus is already its
        // buff, and nothing in the incoming roster asks to reduce it.
        SpeedUp,

        BlockUp,
        BlockDown,
        DamageReductionUp,
        DamageReductionDown,
        EffectHitUp,
        EffectHitDown,
        EffectResistUp,
        EffectResistDown,
        CritDamageUp,
        CritDamageDown,
        SkillDamageUp,
        SkillDamageDown,
        ArmorBreakUp,
        ArmorBreakDown,

        // ---------- marks and the ledger ----------

        /// <summary>
        /// A hero's own mark on a unit. INERT: nothing in the engine reads it except the targeting
        /// and condition members that ask about it by name.
        ///
        /// ONE KIND FOR EVERY HERO'S MARK, told apart by StatusInstance.SourceId. Hale's Quarry and
        /// Kael's Fault Line are both a Mark; they coexist as two entries with different appliers,
        /// and "the enemy carrying MY mark" is Mark plus a source test. That is why there is no
        /// MarkA..MarkH bank: 48 abilities across 36 heroes need marks that must not collide, any 5
        /// of 94 heroes can stand on one team, and a fixed slot pool would need 94 members and a
        /// global assignment nobody could keep unique. Source identity is already carried on every
        /// status entry and is already unique per hero.
        ///
        /// The cost is real and worth stating: every mark renders as "Mark" on a status bar,
        /// because a StatusKind has no per-instance display name. That is a UI problem for the day
        /// a screen wants to print "Quarry", and it does not change what the simulation can do.
        /// </summary>
        Mark,

        /// <summary>A second mark for a hero that carries two at once - Nysera's Vanishing Mark
        /// alongside her Linked, say. Two is what the roster actually uses; a third would be
        /// another member here rather than a redesign.</summary>
        MarkAlt,

        /// <summary>
        /// A running numeric store attached to a status entry rather than to a unit.
        ///
        /// Its Magnitude is the FRACTION of incoming damage the holder banks into
        /// StatusInstance.Stored, capped by StoredCap - see CombatSimulation.DealRawDamage, the one
        /// funnel every point of damage in the game already passes through. EffectKind.LedgerRelease
        /// pays it out, and EffectCondition.LedgerAtLeast asks how deep it is.
        ///
        /// On the status rather than on the unit because that is what the descriptions describe:
        /// a Footnote, a Watcher's Ledger, a Scar Record and an Immaculate Record are all a mark
        /// that happens to be counting, and they expire, move and get dispelled exactly as any
        /// other status does. A parallel per-unit store would need its own lifetime rules and would
        /// then have to be kept in step with the status that represents it.
        /// </summary>
        Ledger,

        // ---------- redirection, charges, per-source modifiers ----------

        /// <summary>
        /// A share of the holder's incoming damage is taken by whoever applied it. Magnitude is the
        /// fraction, SourceId is the protector, and the split happens in DealRawDamage before the
        /// holder's own shield pool is touched - so a protector soaks its share first and the ward
        /// underneath only meets what is left.
        /// </summary>
        Covered,

        /// <summary>
        /// The holder's single-enemy targeting resolves onto whoever applied this, and nobody else.
        ///
        /// The OPPOSITE DIRECTION to Taunt, and a genuinely different rule rather than a variant of
        /// it. Taunt sits on the protector and makes it answer for its whole side against every
        /// attacker; this sits on one ATTACKER and constrains that attacker alone. Four abilities
        /// in the roster say "Taunts the front enemy", meaning exactly one enemy is provoked, and
        /// Taunt on Self would have been wrong in both directions at once - too wide, because every
        /// other enemy would also be redirected, and too narrow, because it says nothing about what
        /// that one enemy does if the caster is not the anchor.
        /// </summary>
        Provoked,

        /// <summary>
        /// Reduces the next StatusInstance.Charges hits the holder takes by Magnitude, then leaves.
        /// The charge-based counterpart of DamageReductionUp, which is time-based: "the next hit is
        /// reduced 18%" is a use count, not a duration, and a duration cannot express it.
        /// </summary>
        Guard,

        /// <summary>
        /// The holder takes Magnitude more damage FROM WHOEVER APPLIED IT, and normal damage from
        /// everybody else. Source-keyed, which is the whole point: DefenseDown is the unconditional
        /// near-miss and makes a target softer to the entire field.
        /// </summary>
        Vulnerable
    }

    /// <summary>
    /// How often a passive may fire. Sits alongside PassiveDef.OncePerBattle rather than replacing
    /// it: that bool has a trigger-dependent default that every piece of shipped content relies on,
    /// so it stays the authority and this refines it only where content asks.
    ///
    /// Deliberately does NOT include "once per target". That reads like a sibling of these but is
    /// not one: these are all answered when the passive FIRES, from the owner's own clock, while a
    /// per-target limit is answered when an effect LANDS and needs the target's identity carried
    /// into the check. It is a limit on an effect, not on a trigger, and inventing it here would
    /// put it in the wrong place.
    /// </summary>
    public enum PassiveLimit
    {
        /// <summary>No limit of its own - PassiveDef.OncePerBattle still applies.</summary>
        Unlimited,

        /// <summary>Once for the whole battle. What OncePerBattle already means, named here so a
        /// content author can say it explicitly.</summary>
        OncePerBattle,

        /// <summary>Once in any given round, and again next round.</summary>
        OncePerRound,

        /// <summary>Once, then not again for PassiveDef.LimitRounds rounds.</summary>
        EveryNRounds
    }

    public enum PassiveTrigger
    {
        None,
        BattleStart,
        TurnStart,
        AfterBasicAttack,
        OnCriticalHit,
        OnKill,
        OnHpBelowThreshold,
        OnDamaged,
        OnBlock,

        // ---------- appended, never inserted ----------
        //
        // THE FIRST TRIGGERS THAT ARE NOT OWNER-CENTRIC. Every member above fires on the unit the
        // thing happened TO, or that did it; the three below fire on every living unit of a whole
        // side because something the OTHER side did is what they are watching for. That is a new
        // dispatch shape - CombatSimulation.BroadcastPassive - and not new state: each one is
        // raised at a point the simulation already reaches and already knows everything about.
        //
        // Recursion is bounded by the existing MaxPassiveDepth, because the broadcast is handed the
        // depth of whatever raised it. A buff-gained passive that itself applies a buff therefore
        // cascades at most as far as a passive calling a passive already could.

        /// <summary>An enemy of the owner cast its Active. Raised in TakeTurn, where the cast
        /// already emits AbilityCast.</summary>
        OnEnemyActive,

        /// <summary>An enemy of the owner gained a beneficial status. Raised in the ApplyStatus
        /// branch, on the side opposite the unit that gained it.</summary>
        OnEnemyBuffGained,

        /// <summary>An enemy damaged one of the owner's allies. Raised in DealRawDamage on the
        /// victim's own side, excluding the victim - which already has OnDamaged of its own.
        ///
        /// This is "damaged an ally", not the "targets an ally" several descriptions ask for, and
        /// the difference is real: a heal, a debuff or a miss all target without damaging. Damage
        /// is the half the simulation has a single funnel for, so it is the half that can be raised
        /// once and correctly rather than at four call sites with four meanings.</summary>
        OnEnemyDamagedAlly,

        /// <summary>The owner delayed somebody's turn. Fires on the SOURCE of the shift rather than
        /// on the unit delayed, because "when Jax delays an enemy, Jax gains Speed" is the shape
        /// the descriptions use; an ability that wants the DELAYED unit to suffer something can
        /// simply carry that as another effect beside the shift.</summary>
        OnTurnDelayed,

        // ---------- own-outcome triggers ----------
        //
        // AfterBasicAttack, OnCriticalHit and OnKill were the only hooks that fired on a unit
        // because of something IT had just done. These three widen that set, and all three fire on
        // the ACTOR: "whenever Cyrra removes Burden", "whenever she heals an ally below 40%".

        /// <summary>The owner healed somebody. Raised in ApplyHealValue, on the healer.</summary>
        OnHealApplied,

        /// <summary>The owner removed a status from somebody - a Cleanse, a Dispel or a
        /// RemoveStatus. Raised on the remover, once per removal.</summary>
        OnStatusRemoved,

        /// <summary>A status on the OWNER ran out its clock. Raised in ExpireStatuses, on the unit
        /// that was carrying it, and only for a genuine duration expiry - not for a status that was
        /// cleansed, dispelled, absorbed away or cleared by death, each of which is a different
        /// thing happening and has its own path.</summary>
        OnStatusExpired,

        // ---------- ally-event triggers ----------
        //
        // Broadcast, the same shape OnEnemyActive and its siblings use: they fire on every living
        // unit of a side because the thing that happened, happened to somebody else. The victim or
        // recipient is excluded from each, since it already has its own owner-centric trigger for
        // the same event.

        /// <summary>An ally of the owner dropped to or below PassiveDef.Threshold of its max HP.
        /// Raised in DealRawDamage after the hit lands, so the passive reads the HP it is
        /// answering.</summary>
        OnAllyHpBelowThreshold,

        /// <summary>An ally of the owner was healed.</summary>
        OnAllyHealed,

        /// <summary>An ally of the owner gained a shield.</summary>
        OnAllyShielded,

        /// <summary>
        /// An ally of the owner is about to die, raised BEFORE the death resolves.
        ///
        /// The one trigger here that intercepts rather than observes. DealRawDamage raises it while
        /// the unit is still alive and its HP is at or below zero, then RE-CHECKS: a passive that
        /// healed it above zero has saved it, and a passive that did nothing leaves the death to
        /// resolve exactly as it did before. No new effect kind was needed for that - an ordinary
        /// Heal lands because the unit is still flagged alive at the moment the trigger runs.
        /// </summary>
        OnAllyWouldDie
    }

    /// <summary>
    /// One kind of player activity a live event can award points for. Deliberately closed and
    /// small today, but the events engine never switches on this enum anywhere - GameSession.
    /// RecordEventActivity walks every active EventDef and compares its own Activity field against
    /// the kind passed in, so adding a member here is never itself a behaviour change. What makes a
    /// new kind actually earn points is one call to RecordEventActivity at that activity's own
    /// natural call site - e.g. StageCleared would go inside GameSession.GrantBattleResult - not a
    /// new case anywhere in the engine. Neither member below needed a CombatSimulation change to
    /// wire, because neither activity happens inside a battle; a future kind that does would be the
    /// first one to test that promise.
    /// </summary>
    public enum EventActivityKind
    {
        /// <summary>One completed summon, successful or a duplicate - GameSession.Summon.</summary>
        SummonPerformed,

        /// <summary>Relic Dust spent, amount-scaled - GameSession.SpendRelicDust, the one choke
        /// point every Dust-spending path (today: GameSession.BuyRelic) routes through.</summary>
        RelicDustSpent
    }

    /// <summary>
    /// A relic's grade, per DESIGN-RELICS "The word is grade, and it is not tier or rarity" -
    /// with a rename layered on top of that same axis, not a new axis.
    ///
    /// Deliberately NOT called tier or rarity, because both words are already taken and either
    /// would silently overload one: "tier" is a relic's upgrade level (OwnedRelic.Tier,
    /// BalanceConfig.RelicMaxTier), and "rarity" is a hero's star rarity (StatMath.StarMultiplier,
    /// an int, not an enum - hero rarity has no "Legendary" value to collide with). Checked before
    /// this rename: no shared Rarity-style enum exists between relics and heroes, so no separate
    /// RelicTier type was needed - this enum was renamed in place instead.
    ///
    /// The two members are a straight relabel of what used to be Common and Legendary, plus one
    /// new value above them. What was Common is now LEGENDARY; what was Legendary (the two
    /// hook-based relics) is now MYTHIC, and Mythic additionally gained a stat block on top of its
    /// hook - the one shape change that came with the rename, not a pure label swap. A relic
    /// therefore has a grade and a tier and they are orthogonal - either grade upgrades from tier 1
    /// to tier 2 exactly the same way.
    /// </summary>
    public enum RelicGrade
    {
        /// <summary>
        /// A bundle of flat and percentage stats, resolved entirely in StatMath before the
        /// simulation starts. Carries RelicStatBonus entries and Hook is always None: this grade
        /// never enters the effect pipeline, so it cannot contest a hook with an aspect or with a
        /// mythic relic. Numerous, cheap to author, aspect-neutral and universal. This is what the
        /// grade named Common was before the rename - the eight starting stat bundles are
        /// unchanged in every value, only the label moved.
        /// </summary>
        Legendary,

        /// <summary>
        /// Rare, rule-breaking, and NOT number-free any more - that is what promotion to this
        /// grade changed. Carries a Hook, exactly as the grade named Legendary did before the
        /// rename, and the rule-changing half is held to DESIGN-RELICS' original founding standard
        /// without exception: the hook changes a rule in the effect pipeline and states itself as
        /// one sentence with no number in it, in Rule. What is new is that a mythic relic ALSO
        /// carries RelicStatBonus entries on top of that hook - a stat block stacked onto an
        /// unchanged unique passive, not a replacement for either half. The no-numbers guarantee
        /// therefore now applies to Rule specifically, not to the relic as a whole.
        /// </summary>
        Mythic
    }

    /// <summary>
    /// Which rule in the effect pipeline a MYTHIC relic bends. One relic, one hook.
    ///
    /// The "a relic changes a RULE, never a number" constraint that shapes this enum is the mythic
    /// grade's bar specifically, not a blanket rule for all relics - DESIGN-RELICS withdrew the
    /// blanket version on 2026-08-26 when the stat-bundle grade was introduced. It is undiminished
    /// here: there is still deliberately no "add N%" member, because a relic that wants one belongs
    /// in RelicStatBonus, which a mythic relic can also carry but this enum never reaches.
    ///
    /// Both members below are hooks no aspect holds. DESIGN-RELICS "Hook contention: aspects win
    /// by default" - Tide owns the AddEnergy overflow redirect, Verdant owns the ApplyHealValue
    /// overflow banking, Ember owns Burn stacking, Orrin is Lumen - and a relic hook must find one
    /// none of them claims rather than compete for one. That conflict is unchanged and out of
    /// scope for the grade rename: nothing here reopens it.
    /// </summary>
    public enum RelicHook
    {
        None,

        /// <summary>
        /// The Unbroken Ledger. A harmful effect from the wearer passes through
        /// ResolveLandingChance with the target's Effect Resist forced to zero.
        ///
        /// Not "hook the chance gate": the gate stopped being _rng.Chance(effect.Chance) when
        /// Effect Hit and Effect Resist shipped. It is the landing chance that gets rebuilt, and
        /// that arithmetic is claimed by the stat layer rather than by any aspect, which is what
        /// makes it available. Control Immunity is deliberately NOT bypassed - it is a separate,
        /// narrower layer that only ever touches Silence and Stun, and erasing both would make
        /// hard control unanswerable by any defensive stat in the game.
        /// </summary>
        BypassEffectResist,

        /// <summary>
        /// Mirrored Wound. When the wearer takes damage, a fraction of it returns to whoever dealt
        /// it. The fraction lives in balance.json (MirroredWoundReflectFraction), never in the
        /// relic's text, exactly as emberBurnStackCap and galeExtraTurnsPerRound do for aspects -
        /// which is how a legendary relic states itself with no number and still has a magnitude.
        ///
        /// Shares PassiveTrigger.OnDamaged's dispatch point but not its mechanism, and that trigger
        /// is NOT unused: tideturned_passive ("Backwash") already listens on it in enemies.json.
        /// The reflect is built to coexist with it rather than to assume an empty trigger.
        /// </summary>
        ReflectDamage
    }

    /// <summary>
    /// How one RelicStatBonus combines with the stat it names. Which mode a stat takes is not one
    /// answer, and DESIGN-RELICS decides it per stat rather than leaving it to the content author:
    /// relic stat bonuses use one "%" sign for two different operations because the stats they
    /// touch are stored in two different shapes. The rule is per-stat, not per-grade, so it applies
    /// identically whether the bonus sits on a legendary stat bundle or stacked onto a mythic hook.
    /// </summary>
    public enum RelicStatMode
    {
        /// <summary>
        /// Attack, Defense, MaxHp, Speed. Large scaled numbers built off BaseAttack * scale *
        /// rarityMultiplier, so "+14% Attack" means Attack *= 1.14.
        /// </summary>
        Multiply,

        /// <summary>
        /// CritChance, CritDamage, EffectHit, EffectResist. Stored as small fractions or as a
        /// multiplier and deliberately never scaled by level or star rarity, so multiplying them
        /// does not work: a multiplicative +12% on a 0.08 crit chance is +0.0096, which is nothing,
        /// and on Kalyx's 0.00 Effect Hit it is exactly zero. So "+12% Crit Chance" means
        /// CritChance += 0.12, and "+20% Crit Damage" means CritDamage += 0.20 - 1.5 becomes 1.7.
        /// </summary>
        Add
    }

    /// <summary>
    /// Which UnitStats field a RelicStatBonus moves. Deliberately a closed enum rather than a
    /// string field name: relic stat bonuses add no new stat, every member below already exists on
    /// UnitStats and is already read by the simulation, and a typo'd stat name should not parse.
    /// </summary>
    public enum RelicStat
    {
        MaxHp,
        Attack,
        Defense,
        Speed,
        CritChance,
        CritDamage,
        EffectHit,
        EffectResist
    }

    /// <summary>
    /// One of the six equipment slots a hero carries (DESIGN-ARMOR section 3). The slot decides
    /// the piece's primary stat outright - see ArmorInfo.PrimaryStat - so a Weapon is always an
    /// Attack piece and only its magnitude and its substats vary.
    ///
    /// The chest slot is PLATE, not Armor. One word naming both the system and one of its slots is
    /// the collision DESIGN-BUILD-ORDER already carries for Forge (task 6's idle-income mechanic),
    /// and reproducing it inside the system commissioned to avoid it would be worse than an odd
    /// slot name.
    ///
    /// The two accessory slots are deliberately a hard A/B pair rather than "two accessory slots":
    /// each guarantees its own primary (Crit Chance, Crit Damage), so a hero cannot wear two Crit
    /// Chance accessories, and every slot in the enum has exactly one answer to "what does this
    /// guarantee".
    /// </summary>
    public enum ArmorSlot
    {
        Weapon,
        Helm,
        Plate,
        Boots,
        AccessoryA,
        AccessoryB
    }

    /// <summary>
    /// An armor piece's quality rung (DESIGN-ARMOR section 4). It gates how many substats roll
    /// alongside the guaranteed primary, and scales the primary itself. It never changes WHICH
    /// stats can roll.
    ///
    /// Deliberately NOT called rarity, grade or tier, and its rungs deliberately avoid Legendary
    /// and Mythic: "grade" and both of those labels belong to RelicGrade, "tier" belongs to
    /// OwnedRelic.Tier, and hero rarity is HeroDef.Stars (an int, which is why hero rarity has no
    /// enum member to collide with - see RelicGrade's own note on that check). "Common" is
    /// technically free but is the pre-rename label of RelicGrade.Legendary, so reusing it would
    /// point a reader at a word that moved. Temper is new to the project and pairs with the Forge.
    ///
    /// Fixed at the moment a piece drops. There is no upgrade path in this pass - a better piece is
    /// a better drop, not a fed one.
    /// </summary>
    public enum ArmorTemper
    {
        Rough,
        Honed,
        Gilded,
        Radiant
    }

    /// <summary>
    /// Which UnitStats field an armor roll moves. Closed, like RelicStat, and for the same reason:
    /// armor adds no new stat, every member below already exists on UnitStats and is already read
    /// by the simulation, and a typo'd stat name should not parse.
    ///
    /// This is a SEPARATE enum from RelicStat rather than a reuse of it, per DESIGN-ARMOR section 9.
    /// RelicStat has eight members and reaches none of the six stats at the end of this list;
    /// extending it to fourteen would put armor's pool inside the enum ContentDatabase.ParseRelics
    /// validates relic content against, which means an armor stat could break relic loading. The
    /// two systems share the MECHANISM (StatMath folding a bonus list into an already-built
    /// UnitStats) and share no type.
    ///
    /// The last six members are the point of the whole system: they are live in CombatSimulation,
    /// each has its own harness check, and before armor no relic and no player-reachable path could
    /// grant any of them.
    /// </summary>
    public enum ArmorStat
    {
        MaxHp,
        Attack,
        Defense,
        Speed,
        CritChance,
        CritDamage,
        EffectHit,
        EffectResist,
        SkillDamage,
        Block,
        ArmorBreak,
        ControlImmunity,
        DamageReduction,
        HolyDamage
    }

    /// <summary>
    /// How one armor roll combines with the stat it names. The rule is per STAT, not per system:
    /// it is a fact about how UnitStats stores that stat, so armor and relics necessarily agree on
    /// it and RelicStatMode's own reasoning applies verbatim.
    ///
    /// Multiply: MaxHp, Attack, Defense, Speed - large numbers built off base * scale * starMult,
    /// so "+14% Attack" means Attack *= 1.14.
    /// Add: everything else - small fractions (or a multiplier, for Crit Damage) that are never
    /// scaled by level or star rarity, where multiplying does nothing. A multiplicative +12% on a
    /// 0.15 Crit Chance is +0.018, and on a 0.00 Block it is exactly zero.
    ///
    /// Holy Damage is the one member of the Add family that is not a fraction at all - it is flat
    /// damage in the same units as a damage number. See ArmorInfo.IsPercentStat, which is what the
    /// UI reads to decide whether to print a "%" sign.
    /// </summary>
    public enum ArmorStatMode
    {
        Multiply,
        Add
    }

    /// <summary>
    /// Display and rule helpers for the armor types above. The slot-to-primary table lives here
    /// rather than in content because it is the guarantee, not a tuning value: DESIGN-ARMOR section
    /// 3 makes the primary a property of the slot, so a Weapon that granted Defense would not be a
    /// mistuned drop, it would be a broken one.
    /// </summary>
    public static class ArmorInfo
    {
        /// <summary>Every slot, in display order. One array so the UI, the roller, the profile and
        /// the save all iterate the same six in the same order rather than each writing their own
        /// loop - the save in particular reads a hero's slots positionally.</summary>
        public static readonly ArmorSlot[] AllSlots =
        {
            ArmorSlot.Weapon, ArmorSlot.Helm, ArmorSlot.Plate,
            ArmorSlot.Boots, ArmorSlot.AccessoryA, ArmorSlot.AccessoryB
        };

        /// <summary>The stat a slot guarantees. Total over ArmorSlot - every member has an answer,
        /// and the trailing return exists only to satisfy the compiler, not as a real fallback.</summary>
        public static ArmorStat PrimaryStat(ArmorSlot slot)
        {
            switch (slot)
            {
                case ArmorSlot.Weapon: return ArmorStat.Attack;
                case ArmorSlot.Helm: return ArmorStat.MaxHp;
                case ArmorSlot.Plate: return ArmorStat.Defense;
                case ArmorSlot.Boots: return ArmorStat.Speed;
                case ArmorSlot.AccessoryA: return ArmorStat.CritChance;
                case ArmorSlot.AccessoryB: return ArmorStat.CritDamage;
            }
            return ArmorStat.Attack;
        }

        /// <summary>
        /// The arithmetic mode a stat takes, derived rather than authored. RelicStatBonus stores
        /// its mode in the data so a content author can see the two rules explicitly; armor derives
        /// it, because armor rolls are GENERATED rather than authored and a generator that could
        /// emit the wrong mode is a generator with a bug in it. Content still names a mode on its
        /// roll ranges (armor.json) for readability, and the roller passes it through here rather
        /// than trusting it - see ArmorRoller.
        /// </summary>
        public static ArmorStatMode ModeFor(ArmorStat stat)
        {
            switch (stat)
            {
                case ArmorStat.MaxHp:
                case ArmorStat.Attack:
                case ArmorStat.Defense:
                case ArmorStat.Speed:
                    return ArmorStatMode.Multiply;
            }
            return ArmorStatMode.Add;
        }

        /// <summary>
        /// Whether a stat's value reads as a percentage on a card. False for Holy Damage alone: it
        /// is flat damage in damage units, so "+40 Holy Damage" is right and the "+4000% Holy
        /// Damage" that printing every Add stat as a percentage produces is not.
        /// </summary>
        public static bool IsPercentStat(ArmorStat stat)
        {
            return stat != ArmorStat.HolyDamage;
        }

        /// <summary>How many substats a temper rolls. The ladder itself, in one place.</summary>
        public static int SubStatCount(ArmorTemper temper)
        {
            switch (temper)
            {
                case ArmorTemper.Rough: return 1;
                case ArmorTemper.Honed: return 2;
                case ArmorTemper.Gilded: return 3;
                case ArmorTemper.Radiant: return 4;
            }
            return 1;
        }

        public static string SlotLabel(ArmorSlot slot)
        {
            switch (slot)
            {
                case ArmorSlot.Weapon: return "Weapon";
                case ArmorSlot.Helm: return "Helm";
                case ArmorSlot.Plate: return "Plate";
                case ArmorSlot.Boots: return "Boots";
                case ArmorSlot.AccessoryA: return "Accessory I";
                case ArmorSlot.AccessoryB: return "Accessory II";
            }
            return slot.ToString();
        }

        /// <summary>Display spelling of an ArmorStat. "Max HP" and "Crit Chance", not the enum
        /// name - the same job RelicDef.StatLabel does for RelicStat, over a wider pool.</summary>
        public static string StatLabel(ArmorStat stat)
        {
            switch (stat)
            {
                case ArmorStat.MaxHp: return "Max HP";
                case ArmorStat.Attack: return "Attack";
                case ArmorStat.Defense: return "Defense";
                case ArmorStat.Speed: return "Speed";
                case ArmorStat.CritChance: return "Crit Chance";
                case ArmorStat.CritDamage: return "Crit Damage";
                case ArmorStat.EffectHit: return "Effect Hit";
                case ArmorStat.EffectResist: return "Effect Resist";
                case ArmorStat.SkillDamage: return "Skill Damage";
                case ArmorStat.Block: return "Block";
                case ArmorStat.ArmorBreak: return "Armor Break";
                case ArmorStat.ControlImmunity: return "Control Immunity";
                case ArmorStat.DamageReduction: return "Damage Reduction";
                case ArmorStat.HolyDamage: return "Holy Damage";
            }
            return stat.ToString();
        }

        public static string TemperLabel(ArmorTemper temper)
        {
            return temper.ToString().ToUpperInvariant();
        }

        /// <summary>
        /// The highest temper a stage-clear drop may ever produce. A CODE invariant, not a content
        /// default: ArmorRoller.RollTemper filters candidates against this constant before it even
        /// looks at armor.json's drop weights, so a content edit that gives Gilded or Radiant a
        /// nonzero dropWeight cannot make either reachable by RNG. DESIGN-ARMOR section 8 - Gilded
        /// and Radiant exist only through GameSession.CombineArmor.
        ///
        /// Declared as the rung itself (Honed), not as a count or an index, so the comparison at
        /// the call site reads as what it means: "this temper is no higher than the ceiling."
        /// </summary>
        public static readonly ArmorTemper MaxDropTemper = ArmorTemper.Honed;

        /// <summary>
        /// The rung directly above this one, or null at the top of the ladder (Radiant) - the one
        /// place "one tier higher" needs an answer outside a drop. GameSession.CombineArmor is the
        /// only caller: a Combine's output temper IS this, by DESIGN-ARMOR's "cannot skip a tier"
        /// rule, so there is nowhere else in the game that needs to ask "what comes next."
        ///
        /// Safe to express as a bare +1 on the underlying int because the four rungs are declared
        /// above in ascending, contiguous order - Radiant is excluded first specifically so that
        /// invariant only has to hold for the three casts this method can actually reach.
        /// </summary>
        public static ArmorTemper? NextTemper(ArmorTemper temper)
        {
            if (temper == ArmorTemper.Radiant) return null;
            return (ArmorTemper)((int)temper + 1);
        }
    }

    public static class AspectChart
    {
        // Narrowed from 1.20/0.85 per DESIGN-ASPECTS section 4: a 41% swing between an advantaged
        // and a disadvantaged attacker out-argues every aspect identity, which forces one correct
        // counter-colour per stage and turns team building into a compliance check. At 1.10/0.92
        // the wheel is a tiebreak between two lines you'd already be happy with, which is what a
        // secondary layer is. Nothing reads these as literals - see Debrief.AspectFactorThreshold
        // and DevHarness CheckDebriefAspect* for the two places that used to.
        public const float Advantage = 1.10f;
        public const float Disadvantage = 0.92f;

        /// <summary>Ember &gt; Verdant &gt; Tide &gt; Gale &gt; Ember. Lumen and Umbra counter each other.</summary>
        public static float Multiplier(Aspect attacker, Aspect defender)
        {
            if (Beats(attacker, defender)) return Advantage;
            if (Beats(defender, attacker)) return Disadvantage;
            return 1f;
        }

        public static bool Beats(Aspect a, Aspect b)
        {
            switch (a)
            {
                case Aspect.Ember: return b == Aspect.Verdant;
                case Aspect.Verdant: return b == Aspect.Tide;
                case Aspect.Tide: return b == Aspect.Gale;
                case Aspect.Gale: return b == Aspect.Ember;
                case Aspect.Lumen: return b == Aspect.Umbra;
                case Aspect.Umbra: return b == Aspect.Lumen;
            }
            return false;
        }

        public static string DisplayName(Aspect a)
        {
            return a.ToString().ToUpperInvariant();
        }
    }

    public static class StatusInfo
    {
        public static bool IsHarmful(StatusKind kind)
        {
            return kind == StatusKind.AttackDown
                || kind == StatusKind.DefenseDown
                || kind == StatusKind.Burn
                || kind == StatusKind.Slow
                || kind == StatusKind.Silence
                || kind == StatusKind.Stun
                // Every stat DOWN is harmful, so Cleanse can remove it, Dispel cannot, and an
                // Umbra source makes it uncleansable - the three rules that all read this one
                // predicate. The matching UPs are deliberately absent: they are buffs, which is
                // what makes them Dispel's business and not Cleanse's. SpeedUp has no Down here
                // because Slow, already listed above, is the speed debuff.
                || kind == StatusKind.BlockDown
                || kind == StatusKind.DamageReductionDown
                || kind == StatusKind.EffectHitDown
                || kind == StatusKind.EffectResistDown
                || kind == StatusKind.CritDamageDown
                || kind == StatusKind.SkillDamageDown
                || kind == StatusKind.ArmorBreakDown
                // Provoked and Vulnerable are things done TO a unit, so a Cleanse can take them and
                // an Umbra source makes them stick. Covered and Guard are protections and are
                // Dispel's business instead.
                || kind == StatusKind.Provoked
                || kind == StatusKind.Vulnerable;
        }

        /// <summary>
        /// The harmful counterpart of a beneficial status, or None where there is not one.
        ///
        /// The table is what EffectKind.InvertStatus means by "its matching debuff", and it is
        /// deliberately partial: Shield, Taunt and Focus have no opposite in this engine, and
        /// inverting them would mean inventing one. A buff with no pairing is skipped rather than
        /// removed, so an inversion never doubles as a stealth Dispel.
        ///
        /// SpeedUp pairs with Slow rather than with a SpeedDown, because Slow IS the speed debuff -
        /// the same asymmetry EffectiveSpeed reads and Gale's turn-order shift keys on.
        /// Regen pairs with Burn: both are per-tick magnitudes read by TickDamageOverTime, so the
        /// pairing swaps which direction the tick runs and nothing else.
        /// </summary>
        public static StatusKind Opposite(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.AttackUp: return StatusKind.AttackDown;
                case StatusKind.DefenseUp: return StatusKind.DefenseDown;
                case StatusKind.SpeedUp: return StatusKind.Slow;
                case StatusKind.Regen: return StatusKind.Burn;
                case StatusKind.BlockUp: return StatusKind.BlockDown;
                case StatusKind.DamageReductionUp: return StatusKind.DamageReductionDown;
                case StatusKind.EffectHitUp: return StatusKind.EffectHitDown;
                case StatusKind.EffectResistUp: return StatusKind.EffectResistDown;
                case StatusKind.CritDamageUp: return StatusKind.CritDamageDown;
                case StatusKind.SkillDamageUp: return StatusKind.SkillDamageDown;
                case StatusKind.ArmorBreakUp: return StatusKind.ArmorBreakDown;
            }
            return StatusKind.None;
        }

        public static string DisplayName(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.AttackUp: return "Attack Up";
                case StatusKind.AttackDown: return "Attack Down";
                case StatusKind.DefenseUp: return "Guard Up";
                case StatusKind.DefenseDown: return "Guard Down";
                case StatusKind.Burn: return "Cinder";
                case StatusKind.Regen: return "Renewal";
                case StatusKind.Shield: return "Ward";
                case StatusKind.Taunt: return "Provoke";
                case StatusKind.Focus: return "Focus";
                case StatusKind.Slow: return "Mire";
                case StatusKind.Silence: return "Silence";
                case StatusKind.Stun: return "Stun";
                case StatusKind.Stealth: return "Stealth";
                case StatusKind.Resolve: return "Resolve";
                case StatusKind.SpeedUp: return "Quickened";
                case StatusKind.BlockUp: return "Block Up";
                case StatusKind.BlockDown: return "Block Down";
                case StatusKind.DamageReductionUp: return "Bulwark";
                case StatusKind.DamageReductionDown: return "Exposed";
                case StatusKind.EffectHitUp: return "Effect Hit Up";
                case StatusKind.EffectHitDown: return "Effect Hit Down";
                case StatusKind.EffectResistUp: return "Effect Resist Up";
                case StatusKind.EffectResistDown: return "Effect Resist Down";
                case StatusKind.CritDamageUp: return "Crit Damage Up";
                case StatusKind.CritDamageDown: return "Crit Damage Down";
                case StatusKind.SkillDamageUp: return "Skill Damage Up";
                case StatusKind.SkillDamageDown: return "Skill Damage Down";
                case StatusKind.ArmorBreakUp: return "Armor Break Up";
                case StatusKind.ArmorBreakDown: return "Armor Break Down";
                case StatusKind.Mark: return "Mark";
                case StatusKind.MarkAlt: return "Mark II";
                case StatusKind.Ledger: return "Record";
                case StatusKind.Covered: return "Covered";
                case StatusKind.Provoked: return "Provoked";
                case StatusKind.Guard: return "Guard";
                case StatusKind.Vulnerable: return "Vulnerable";
            }
            return kind.ToString();
        }

        /// <summary>Short tag drawn on the unit health bar.</summary>
        public static string ShortTag(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.AttackUp: return "ATK+";
                case StatusKind.AttackDown: return "ATK-";
                case StatusKind.DefenseUp: return "DEF+";
                case StatusKind.DefenseDown: return "DEF-";
                case StatusKind.Burn: return "BRN";
                case StatusKind.Regen: return "REG";
                case StatusKind.Shield: return "WRD";
                case StatusKind.Taunt: return "PRV";
                case StatusKind.Focus: return "FOC";
                case StatusKind.Slow: return "MIR";
                case StatusKind.Silence: return "SIL";
                case StatusKind.Stun: return "STN";
                case StatusKind.Stealth: return "STH";
                case StatusKind.Resolve: return "RSV";
                case StatusKind.SpeedUp: return "SPD+";
                case StatusKind.BlockUp: return "BLK+";
                case StatusKind.BlockDown: return "BLK-";
                case StatusKind.DamageReductionUp: return "DR+";
                case StatusKind.DamageReductionDown: return "DR-";
                case StatusKind.EffectHitUp: return "EHT+";
                case StatusKind.EffectHitDown: return "EHT-";
                case StatusKind.EffectResistUp: return "ERS+";
                case StatusKind.EffectResistDown: return "ERS-";
                case StatusKind.CritDamageUp: return "CDM+";
                case StatusKind.CritDamageDown: return "CDM-";
                case StatusKind.SkillDamageUp: return "SKD+";
                case StatusKind.SkillDamageDown: return "SKD-";
                case StatusKind.ArmorBreakUp: return "ABK+";
                case StatusKind.ArmorBreakDown: return "ABK-";
                case StatusKind.Mark: return "MRK";
                case StatusKind.MarkAlt: return "MRK2";
                case StatusKind.Ledger: return "REC";
                case StatusKind.Covered: return "COV";
                case StatusKind.Provoked: return "PRV!";
                case StatusKind.Guard: return "GRD";
                case StatusKind.Vulnerable: return "VUL";
            }
            return "?";
        }
    }

    public static class EnumUtil
    {
        public static T Parse<T>(string text, T fallback) where T : struct
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            try
            {
                return (T)Enum.Parse(typeof(T), text.Trim(), true);
            }
            catch (Exception)
            {
                return fallback;
            }
        }
    }
}
