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

    /// <summary>
    /// Combat archetype. Purely descriptive - behaviour comes from the ability data, and nothing in
    /// `CombatSimulation` branches on a role.
    ///
    /// TEN, IN FIVE PAIRS, matching the taxonomy the incoming 94-hero roster is authored against:
    /// Bulwark/Vanguard hold a line, Binder/Breaker break one, Warden/Mender keep it standing,
    /// Marksman/Tinkerer work at range, Striker/Stalker end single targets. The first of each pair
    /// is the older, more defensive or more direct reading; the second is its variant.
    ///
    /// ORDINALS: the six roles that survive the widening keep theirs, and the four new ones are
    /// appended. `Ranger` was REMOVED rather than kept as a deprecated member, which is the one
    /// deviation from the append-only rule every other enum in this project follows, and it is
    /// safe for one specific reason: it was the LAST member, so nothing before it renumbers, and a
    /// role reaches no persisted record at all - it is absent from the save format and from every
    /// battle event, living only on `HeroDef`, `CombatUnit` and the per-battle `UnitSnapshot`,
    /// each of which is rebuilt from content on load. `CheckHeroRoleOrdinals` in the harness pins
    /// both halves of that: the surviving ordinals, and the claim that no save carries a role.
    /// Ranger had exactly one holder in content, Orren, who is a Marksman under this taxonomy.
    ///
    /// A role that content names but this enum does not know parses to `Striker` silently
    /// (`EnumUtil.Parse` falls back rather than throwing), so a roster authored ahead of this enum
    /// would look fine and be wrong. `CheckContent` asserts every authored role round-trips.
    /// </summary>
    public enum HeroRole
    {
        Bulwark,  // 0  holds the line, absorbs and redirects
        Striker,  // 1  single-target damage
        Breaker,  // 2  area damage
        Mender,   // 3  healing
        Warden,   // 4  support, energy and buffs
        Stalker,  // 5  single-target assassin, debuff-led

        // ---------- appended for the 94-hero taxonomy, never inserted ----------
        Vanguard, // 6  the offensive half of the tank pair: a durable body that also deals damage
        Binder,   // 7  the control half of the break pair: debuffs and denial over raw area damage
        Marksman, // 8  ranged single-target, mark-led
        Tinkerer  // 9  ranged utility: gadgets, energy manipulation, conditional riders
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
        // wrote (int)mode into a TargetAnchored event's Value while the Lumen anchor existed, so
        // renumbering an existing mode rewrites what every anchored battle already recorded about
        // why it was anchored. TargetAnchored is no longer emitted; the records are permanent.
        //
        // THE ANCHOR APPLIES TO EVERY SINGLE-ENEMY MODE BELOW. A living Taunt holder answers every
        // single-enemy targeting question aimed at its side, so EnemyWithStatus and the three stat
        // picks collapse onto it whenever one exists. The Lumen half of that rule - an aspect
        // answering unconditionally, with no status and no ability behind it - has been removed;
        // what is left is the protector mechanic, which says so on the target's own status bar.
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
        EnemyWithMyMark,

        /// <summary>
        /// The unit whose death is being dispatched right now, and NOTHING outside that window.
        ///
        /// It resolves only while `OnEnemyDefeated` / `OnAllyDefeated` are being broadcast, because
        /// that is the only moment a dead unit still has a status list to read: the death branch
        /// holds its `Statuses.Clear()` until the broadcast returns. Outside that window it
        /// resolves to nobody, which is correct rather than merely safe - "the unit that just died"
        /// has no referent on an ordinary turn.
        ///
        /// The one TargetMode that can name a unit with `Alive == false`, so every effect reading
        /// it has to be one that makes sense against a corpse. Moving a status off it does; healing
        /// it does not, and `ApplyHealValue` already refuses a dead target.
        /// </summary>
        DefeatedUnit,

        /// <summary>
        /// The enemy that just crossed the active-skill energy threshold while carrying TurnDebt.
        /// Resolves only while `OnEnemyEnergyThreshold` is being broadcast, via `_thresholdUnit`;
        /// outside that window it resolves to nobody, following the same pattern as DefeatedUnit.
        /// </summary>
        ThresholdUnit,

        /// <summary>
        /// The unit a broadcast is about — the healed ally, the dying ally, the repaying enemy,
        /// the shielded ally, the buffed enemy, etc. Resolves to `_broadcastSubject` while any
        /// broadcast is in flight and to nobody outside one, following the same pattern as
        /// DefeatedUnit and ThresholdUnit.
        ///
        /// Distinct from DefeatedUnit (which resolves to the dead unit via `_defeated`) and from
        /// ThresholdUnit (which resolves via `_thresholdUnit`): both of those predate this and
        /// keep their own field because content already names them and their semantics are
        /// narrower. BroadcastSubject is the general-purpose version for every other broadcast
        /// that fires because something happened to a specific unit.
        /// </summary>
        BroadcastSubject,

        // ---------- batch two ----------

        /// <summary>The three lowest-HP allies by ratio, same ordering AlliesByHp uses.</summary>
        ThreeLowestHpAllies,

        /// <summary>The living enemy that has DEALT the most total damage this round. Hale's
        /// "move Quarry to the enemy that dealt the most damage that round".</summary>
        HighestDamageDealtEnemy,

        /// <summary>
        /// The living enemy that has DEALT the most damage-over-time this round - damage it
        /// inflicted, reading CombatUnit.DoTDamageDealtThisRound.
        ///
        /// RENAMED FROM HighestDoTDamageEnemy, same ordinal, because that name was the trap and
        /// it caught somebody. "DoT damage enemy" reads just as naturally as "the enemy with the
        /// most DoT ON it", which is the opposite question and the one Cyrra's active actually
        /// asks - it was authored against this mode and stripped a Burn off whichever enemy had
        /// been burning the party hardest, not off the one carrying the most Burn. For the
        /// carried-DoT question see HighestPendingDoTEnemy.
        ///
        /// The meaning is unchanged and deliberately so: it is the DoT half of a pair whose total
        /// half Hale uses correctly, and redefining one half would leave a pair where one member
        /// means "dealt" and the other "carried". No shipped content uses this member now.
        /// </summary>
        HighestDoTDealtEnemy,

        // ---------- batch three ----------

        /// <summary>
        /// The unit that was just damaged, healed or otherwise affected in a broadcast, as a
        /// target rather than only a condition subject. Resolves from _broadcastVictim, which is
        /// set at every broadcast site that has a clear victim or recipient.
        ///
        /// BEFORE THE ANCHOR, alongside the ally modes: the whole point is to reach a specific
        /// unit regardless of which side it is on, and a Taunt holder must not intercept it.
        /// BroadcastSubject in the post-anchor section has the same anchor-override problem but
        /// is left where it is for backward compatibility.
        /// </summary>
        BroadcastVictim,

        // ---------- batch four ----------

        /// <summary>The living enemy carrying the most entries of the caster's mark kind from the
        /// caster specifically. Ties broken by lowest slot. No marked enemy → nobody.</summary>
        HighestMyMarkStacksEnemy,

        // ---------- linked targeting ----------

        /// <summary>
        /// The enemy the caster's attack landed on BEFORE its current one, read off
        /// CombatUnit.PreviousTargetId.
        ///
        /// A sibling of LastTarget, and placed in the same post-anchor family for the same reasons:
        /// it names a single enemy, so the side's anchor outranks it, and it falls back to
        /// FrontEnemy when the remembered unit is dead, excluded or was never set. PreviousTargetId
        /// is only written when the target actually CHANGES - ApplyDamageEffect refuses to shuffle
        /// a unit's own history when it hits the same body twice - so "the one before this one"
        /// means a genuinely different body, never the same one twice.
        ///
        /// With no previous target at all this resolves to the front enemy rather than to nobody,
        /// which is the same answer LastTarget gives in the same situation. The alternative -
        /// resolving to nobody - would make an ability silently do nothing on its owner's first
        /// two turns, and the condition family (SubjectIsMyPreviousTarget) is what a kit uses when
        /// it needs "only if there WAS one".
        /// </summary>
        PreviousTarget,

        /// <summary>
        /// The unit on the other end of the caster's own Link - see StatusKind.Linked.
        ///
        /// BEFORE THE ANCHOR, alongside BroadcastVictim and the ally modes, and for the identical
        /// reason: a Link names one specific body, which may be an ally (Rovan's ward) or an enemy
        /// (Talren's pair), and a Taunt holder intercepting it would make the link name somebody it
        /// is not bound to. A link is a claim about two units; letting a third answer for it would
        /// make the mode mean nothing.
        ///
        /// Resolves the FIRST live Link entry on the caster, front-most by nothing but list order,
        /// and to nobody when the caster holds none or the partner is dead. Resolving to nobody is
        /// the honest answer here rather than a fallback: "the unit I am linked to" has no referent
        /// when there is no link, and a FrontEnemy fallback would quietly aim a bound effect at a
        /// body the link never named.
        /// </summary>
        LinkedUnit,

        // ---------- the nine-gap pass ----------

        /// <summary>
        /// The caster's link partner with the HIGHEST current HP, excluding the partner of the
        /// link dispatch currently in flight. Nobody when the caster holds no other link.
        ///
        /// GAP 4: "the OTHER recorded target". A hero holding two links had no way to name the one
        /// that was not just hit - `LinkedUnit` resolves to `_linkPartner`, which inside a dispatch
        /// is precisely the body that WAS hit.
        ///
        /// One mode serves both shapes that asked for it, which is why it is defined by highest HP
        /// rather than by "the one that is not it". With two records there is exactly one other
        /// partner and "highest HP among one" is that partner, so the base case is unambiguous;
        /// with three - a talent that records an extra enemy - the same rule reads "the recorded
        /// target with the highest present HP", which is what such a talent asks for in as many
        /// words. A second mode for the two-record case would have been two spellings of one
        /// question.
        ///
        /// BEFORE THE ANCHOR, alongside LinkedUnit and for the same reason: a link names specific
        /// bodies on either side, and a Taunt holder answering for them would make the mode lie.
        /// </summary>
        OtherLinkedUnit,

        /// <summary>
        /// EVERY living unit the caster holds a link to. The SET the pairwise link had no concept
        /// of - "deal damage to both recorded targets" needs all of them at once, and resolving the
        /// partners one at a time cannot express it.
        ///
        /// Returned in unit order and deduplicated, so a caster holding two links to one body hits
        /// it once. Empty when the caster holds no link, which is the honest answer rather than a
        /// fallback, exactly as LinkedUnit's is.
        ///
        /// NOT skipped for concealment, which is the one place it parts company with the
        /// single-target link modes. It is a multi-target mode and `AllEnemies` is not skipped
        /// either; beyond that, a link is a claim already made on a specific body, and hiding from
        /// somebody you are already bound to is not what concealment means.
        /// </summary>
        AllLinkedUnits,

        /// <summary>
        /// The unit that CAUSED the broadcast in flight - the healer, the attacker, the hitter - as
        /// opposed to BroadcastSubject and BroadcastVictim, which both name the unit a broadcast is
        /// ABOUT.
        ///
        /// GAP 8. The distinction had no target-mode side at all: a passive could TEST the causing
        /// unit through ConditionSubject.LastAttacker, and only in the one case where the subject
        /// had been damaged by it, and could never aim an effect at it. "Record the triggering
        /// ally's role" has to point RecordFact at that ally, and there was no way to name it.
        ///
        /// Resolves from `_broadcastSourceId` and to nobody outside a broadcast that carries one,
        /// the same window discipline DefeatedUnit and ThresholdUnit keep. Before the anchor,
        /// because the causing unit can stand on either side.
        /// </summary>
        BroadcastSource,

        /// <summary>
        /// The living enemy carrying the most PENDING damage-over-time: the damage its DoT
        /// entries would still deal if left alone, summed - per-tick magnitude times rounds left.
        ///
        /// The carried-DoT question, as against HighestDoTDealtEnemy's dealt-DoT one. "Removes
        /// Burden from the enemy with the highest remaining Burden damage" is this and has no
        /// other spelling: every other enemy pick ranks on a stat, a status count or damage
        /// output, and none of them can see how much a DoT has left to do.
        ///
        /// It matters to the kit that asked for it rather than being a nicety. Cyrra strips a
        /// Burden and converts what it WOULD have dealt into healing, so the pick decides the
        /// size of the conversion; choosing the wrong enemy does not merely retarget her active,
        /// it changes how much she heals.
        ///
        /// Ranks with Sim.RemainingDotDamage, the same helper _lastRemovedValue sums when a
        /// removal happens, so "which enemy has the most to remove" and "how much was removed"
        /// cannot drift apart. An enemy carrying no DoT contributes zero and is never picked over
        /// one that does; nobody at all when no enemy carries any, which is the honest answer
        /// rather than falling through to the front body.
        /// </summary>
        HighestPendingDoTEnemy,

        /// <summary>
        /// The living enemy with the least Energy, ties going to the front slot.
        ///
        /// THE MISSING CORNER OF A SET OF THREE. The enum has `HighestEnergyEnemy` and
        /// `LowestEnergyAlly` and has never had this one, so a kit that wanted to hit whoever was
        /// furthest from casting had nothing to name - and two clauses of one hero ask for it in as
        /// many words.
        ///
        /// Energy is read raw, the same way `HighestEnergyEnemy` reads it: nothing modifies it, and
        /// it is already the number the player watches fill.
        /// </summary>
        LowestEnergyEnemy,

        /// <summary>
        /// The unit the last SwapTurnOrder moved FORWARD, and the one it moved BACK.
        ///
        /// A swap has two subjects and the event carried only one of them, so "the enemy moved
        /// backward gains Skill Damage Down" and "the enemy moved forward gains Defense Down" had
        /// no way to name what they were about. Both resolve to nobody outside the ability that
        /// performed the swap, the same way DefeatedUnit resolves to nobody outside the defeat
        /// window: they are set by ApplySwapTurnOrder and cleared when the ability ends.
        /// </summary>
        SwapAdvancedUnit,

        /// <summary>The unit the last SwapTurnOrder moved BACK. Sibling of SwapAdvancedUnit.</summary>
        SwapDelayedUnit
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
        LedgerRelease,

        /// <summary>
        /// Write a NON-NUMERIC fact onto a status entry: a status identity, an action type, a role,
        /// or the running minimum of an HP series. `EffectDef.Record` picks which, and
        /// `EffectDef.Status` picks which entry on the target carries it. `Stored` is a float and
        /// can say none of those, which is the whole reason this exists - see RecordKind.
        /// </summary>
        RecordFact,

        /// <summary>Sets the target's energy to zero. Distinct from a negative EnergyGain in
        /// two ways: it is all-or-nothing (no partial drain), and it is a no-op when the target
        /// has no energy to lose.</summary>
        Interrupt,

        /// <summary>Adds effect.Duration rounds to every matching status on the target. Filters
        /// by harmful/beneficial (via effect.Status == None) or by a named StatusKind (via
        /// effect.Status != None). Does not apply a new status — only extends one already
        /// there.</summary>
        ExtendStatus,       // 18

        /// <summary>
        /// Consumes all TurnDebt on the target, calculates the repayment cost (owed * rate),
        /// delays the target by Magnitude fraction, and grants the calculated energy to the
        /// SecondaryTarget (typically LowestEnergyAlly).
        ///
        /// The three operations are one effect rather than three because the energy amount is
        /// derived from the TurnDebt being consumed: a separate EnergyGain would need to know a
        /// number that no longer exists once RemoveStatus has taken it. Packaging them lets the
        /// calculation happen between the removal and the grant, which is the only order that works.
        /// </summary>
        ConsumeTurnDebt,    // 19

        // ---------- tier eight: transfers, ledger increment, lethal redirect ----------

        /// <summary>
        /// Moves HP from the source to the target. The source loses min(Power * Attack, Hp - 1)
        /// HP and the target is healed for the same amount. Self-damage, not through DealRawDamage,
        /// so Guard/Cover/Shield/Ledger are not involved and the source cannot die from it.
        /// </summary>
        HpTransfer,         // 20

        /// <summary>
        /// Moves a fraction (Magnitude) of the source's Shield pool to the target. The source's
        /// pool is reduced and the target's is increased; if the source's pool empties, the Shield
        /// status is removed. The target receives a Shield status if it does not already have one.
        /// </summary>
        ShieldTransfer,     // 21

        /// <summary>
        /// Adds Power * Attack directly to the caster's Ledger Stored on the target, capped by
        /// StoredCap. The banking half of a Ledger that is filled by an explicit effect rather
        /// than by damage accrual.
        /// </summary>
        LedgerIncrement,    // 22

        /// <summary>
        /// Intercepts lethal damage on the target (whose Hp is at or below zero during an
        /// OnAllyWouldDie broadcast). Sets the target's Hp to 1, then deals the overkill as
        /// damage to the source (passive owner) through DealRawDamage, so the redirect itself
        /// can kill the saviour and triggers cascade normally.
        /// </summary>
        RedirectLethal,     // 23

        // ---------- tier nine: parameterisation gaps ----------

        /// <summary>
        /// Records the target's current energy as the stack count of a status on the target.
        /// Status names the entry, Duration its lifetime. If the status already exists it is
        /// refreshed; if it does not, it is created. The stack count is clamped to [0, 99].
        /// </summary>
        SnapshotEnergy,     // 24

        /// <summary>
        /// Moves a status entry from the target to SecondaryTarget. Status names the kind to
        /// move; only the first matching entry is transferred. Stored, StoredCap, Magnitude,
        /// Charges, BankHeals, BankOnlyFrom, AutoRelease, AutoReleaseAsHeal and every other
        /// field travel with it. A no-op when the target does not carry the named status or
        /// SecondaryTarget resolves to nobody alive.
        /// </summary>
        TransferStatus,     // 25

        // ---------- batch two ----------

        /// <summary>
        /// Stores the target's current HP on CombatUnit.SnapshotHp, so a later
        /// HpAboveSnapshot condition can compare against it.
        /// </summary>
        SnapshotHp,             // 26

        // ---------- batch five ----------

        /// <summary>
        /// Deposits an amount into existing Ledger entries on the target from the caster.
        /// Amount is computed from Power * Attack + Flat + selfHpRatio * casterMaxHp, then
        /// optionally scaled by ScaleByLastRedirect / ScaleByLastRelease / ScaleByLastBanked.
        /// Stored is added up to StoredCap; OnLedgerCapped and OnAllyLedgerBanked fire as usual.
        /// </summary>
        LedgerDeposit,          // 27

        // ---------- linked targeting ----------

        /// <summary>
        /// Binds the effect's Target to its SecondaryTarget with a StatusKind.Linked entry - see
        /// that member for what the engine then does with the pair.
        ///
        /// Its own kind rather than an ApplyStatus of Linked, because a link needs TWO units named
        /// and ApplyStatus resolves one. Every other field an ApplyStatus would stamp is stamped
        /// here too - Duration, Chance, UsesPerRound, Charges - so a Link is an ordinary status in
        /// every respect except how it is created.
        ///
        /// `LinkMutual` decides whether the reverse entry is written as well. False writes one
        /// entry, on the Target, naming the SecondaryTarget: that is a one-way binding, which is
        /// what a ward is ("a share of what THEY take comes to ME", and nothing flows back).
        /// True writes both, which is what a paired mark is ("a hit on either strikes the other").
        /// A mutual link is two entries rather than one bidirectional entry, so a Dispel, a Cleanse
        /// or a death can break one half and leave the other standing - which is what the
        /// descriptions mean when they say the link is on both bodies.
        ///
        /// `Magnitude` is the transfer fraction and `LinkTransfer` is what the fraction does. A
        /// LinkTransfer of None makes the entry a pure record that only the two link triggers read,
        /// which is a legitimate authoring - not every link moves damage.
        /// </summary>
        LinkUnits,              // 28

        /// <summary>
        /// Repeats a fraction of the damage ALREADY DEALT in this ability, onto the effect's own
        /// Target. `Power` is the fraction.
        ///
        /// It reads a realised number rather than recomputing one, and that is the whole point.
        /// The amount is `Sim._lastDamageDealt`, which is what the most recent hit actually took
        /// off a bar after Guard, Cover, the shield pool, the Ledger and every mitigation step -
        /// so "echoes 40% of its damage" echoes 40% of what landed, not 40% of what was rolled.
        /// It is paid through DealRawDamage rather than ApplyDamageEffect for the same reason: the
        /// number has already been through the pipeline once, and sending it round again would
        /// apply Defense, crit, variance and the aspect chart to it twice.
        ///
        /// `_lastDamageDealt` is cleared at every ability boundary, exactly as `_lastTarget` is, so
        /// an echo authored with no damage ahead of it in the same ability reads zero and does
        /// nothing. An echo that could read a number from three turns ago would be a different
        /// ability every time it fired.
        /// </summary>
        EchoDamage,              // 29

        /// <summary>
        /// Cancels the target's declared cast, if it has one in flight.
        ///
        /// The interrupt half of cast cancellation. It reaches only a cast that has been DECLARED
        /// and has not yet resolved - which is a window that did not exist before this pass, and
        /// is the whole point of it. Outside that window there is nothing to cancel and the effect
        /// does nothing, the same way RedirectLethal does nothing outside an OnAllyWouldDie.
        /// </summary>
        CancelCast
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
        AlreadyDamagedSubjectThisRound,

        // ---------- appended: turn progress read as a QUANTITY ----------
        //
        // Progress is DERIVED FROM POSITION, not held in a gauge - see Sim.TurnProgressOf. A unit
        // about to act reads near 1, a unit at the back of the remaining order reads near 0, and a
        // unit that has already acted reads 0, because its next turn is a whole round away. These
        // are the first conditions in this enum that the pure static ConditionMet cannot answer:
        // they need the round's live order, so the Sim answers them and the static function returns
        // false rather than guessing.
        TurnProgressAbove,
        TurnProgressBelow,

        /// <summary>The CASTER reaches its turn before the subject does, this round. "Acts before
        /// the slowest enemy", with the effect aimed at LowestSpeedEnemy.</summary>
        CasterActsBeforeSubject,

        // ---------- appended: reading a non-numeric ledger back out ----------
        StoredStatusMatches,
        StoredActionMatches,
        StoredRoleMatches,
        SubjectAboveStoredMin,

        /// <summary>The subject's named status still has a use left THIS ROUND: the "first X each
        /// round" family, counted on the entry rather than on the passive. See
        /// StatusInstance.UsesThisRound.</summary>
        StatusUseAvailable,

        // ---------- cross-unit stat comparison ----------
        //
        // The two-sided comparison DESIGN-BUILD-ORDER deferred: "the target has more Attack than
        // the caster". ConditionValue is unused; ComparedStat names the stat. Strict inequality
        // so a tie is false, which is the only reading that makes "has more" unambiguous.

        /// <summary>The caster's named stat is strictly above the target's.</summary>
        CasterStatAboveTarget,

        /// <summary>The caster's named stat is strictly below the target's.</summary>
        CasterStatBelowTarget,

        // ---------- action-type history on the subject ----------
        //
        // Reading WHAT a unit did, not just THAT it did. CasterRepeatedActionType already exists
        // but is pinned to the caster; these four ask the same family of questions about the
        // subject, which is what Zella's kit needs.

        /// <summary>The subject's most recent turn was a basic attack.</summary>
        SubjectLastActionWasBasic,

        /// <summary>The subject's most recent turn was an active.</summary>
        SubjectLastActionWasActive,

        /// <summary>The subject used a DIFFERENT action type on its last two turns.</summary>
        SubjectChangedActionType,

        /// <summary>The subject used the SAME action type on its last two turns. The Subject
        /// mirror of CasterRepeatedActionType.</summary>
        SubjectRepeatedActionType,

        // ---------- tier eight: AND conditions and ledger-at-cap ----------

        /// <summary>
        /// The caster's Ledger on the subject has reached its StoredCap. Checks every Ledger entry
        /// whose SourceId matches the caster; true if any has Stored >= StoredCap with StoredCap > 0.
        /// The complement of LedgerAtLeast, which checks an absolute threshold; this checks the
        /// variable, Attack-scaled cap that was set when the Ledger was applied.
        /// </summary>
        LedgerAtCap,

        // ---------- batch two ----------

        /// <summary>The subject's current HP is strictly above its SnapshotHp.</summary>
        HpAboveSnapshot,

        // ---------- batch three ----------

        /// <summary>The subject carries at least one beneficial status (anything StatusInfo.IsHarmful
        /// returns false for, excluding None, Resolve, Resolve2, Ledger and the inert marks).</summary>
        HasAnyBeneficialStatus,

        /// <summary>The subject carries at least one harmful status (anything StatusInfo.IsHarmful
        /// returns true for).</summary>
        HasAnyHarmfulStatus,

        /// <summary>The unit that broke the subject's shield (stored as _shieldBreakerId during
        /// OnShieldBreak) matches the caster. Meaningful only inside an OnShieldBreak trigger;
        /// outside that window it is false.</summary>
        ShieldBrokenByCaster,

        // ---------- batch four ----------

        /// <summary>Like StatusStacksAtLeast, but counts only entries whose SourceId matches the
        /// caster. "My stacks of X on this target ≥ N" vs "any stacks of X on this target ≥ N".</summary>
        MyStatusStacksAtLeast,

        // ---------- the nine-gap pass ----------

        /// <summary>
        /// The caster has NOT damaged this subject yet this round. The exact mirror of
        /// AlreadyDamagedSubjectThisRound, reading the same CombatUnit.DamagedThisRound list.
        ///
        /// GAP 9, and it exists because this enum has no negation. "Repeat 40% of the damage
        /// against a lower-HP enemy, if ANOTHER enemy has lower HP than the target" needs the
        /// secondary pick to be a body the blow has not already landed on, and
        /// AlreadyDamagedSubjectThisRound was the right test with precisely the wrong sign.
        ///
        /// A GENERAL `negate` FLAG WAS CONSIDERED AND REJECTED. One bool per condition slot would
        /// have been three fields that silently re-point all forty conditions, including the
        /// twenty-odd whose negation nobody has reasoned about - what "not TurnProgressAbove" means
        /// for a unit with no position left, or "not StoredRoleMatches" when nothing was recorded.
        /// A mirror member answers the clause that asked and changes the meaning of nothing else.
        /// </summary>
        SubjectNotDamagedByMeThisRound,

        /// <summary>
        /// The last amount the CASTER moved - see EffectDef.ScaleByLastMoved - is at least
        /// ConditionValue times the SUBJECT's EffectiveMaxHp.
        ///
        /// GAP 7's condition half. "If the Shield moved is at least 10% of the ally's Max HP" is a
        /// comparison between a runtime amount and a fraction of a body's health, and neither
        /// operand was reachable: the amount had no store, and every existing condition compares
        /// against an authored constant. ConditionValue is the fraction; the subject supplies the
        /// max HP, so pointing it at BroadcastSubject reads the ally rather than the caster.
        ///
        /// False when the caster has moved nothing, rather than true-by-vacuity at a threshold of
        /// zero: a clause gated on how much moved did not happen if nothing moved.
        /// </summary>
        LastMovedAboveHpFraction,


        /// <summary>
        /// The subject's HeroRole is the one named by ConditionValue, read as a role ordinal.
        ///
        /// THE ONE QUESTION THE ENGINE COULD NOT ASK ABOUT A UNIT. It can test HP, statuses, stack
        /// depth, energy, speed, shields, action history, turn position and what a mark recorded,
        /// and it could not ask what a unit IS. The nearest thing, StoredRoleMatches, compares a
        /// role somebody previously RECORDED against a constant - so a kit could ask "is this the
        /// role I wrote down" and never "is this a Mender".
        ///
        /// Reads CombatUnit.Role, which is rebuilt from content at the start of every battle and
        /// reaches no save byte, so this costs nothing to store and cannot go stale.
        ///
        /// ConditionValue is a role ordinal, the same way StoredRoleMatches already takes one.
        /// That is a raw number in JSON rather than a name, which is ugly and is the existing
        /// convention; changing it would mean changing both.
        /// </summary>
        SubjectRoleIs,

        /// <summary>The subject's Energy is at or above ConditionValue, read as a raw number.</summary>
        EnergyAtLeast,

        /// <summary>The subject's Energy is at or below ConditionValue, read as a raw number.</summary>
        EnergyBelow,

        /// <summary>
        /// The subject has been healed since the CASTER's last turn. The heal sibling of
        /// SubjectDamagedSinceMyTurn, round-granular in exactly the same way and for the same
        /// reason - it compares the round the subject was last healed in against the round the
        /// caster last acted in.
        /// </summary>
        SubjectHealedSinceMyTurn,

        /// <summary>The subject has gained a Shield since the caster's last turn. The other half
        /// of "gained a Shield OR was healed", which is one clause wanting both.</summary>
        SubjectShieldedSinceMyTurn,

        /// <summary>
        /// The subject has a cast declared and not yet resolved.
        ///
        /// "An ability that changes based on what the enemy is about to do" is this: an ordinary
        /// effect can ask whether the body in front of it is mid-cast and take a different shape
        /// if it is. False for everybody outside a declaration window, which is every moment of
        /// every battle in shipped content.
        /// </summary>
        SubjectIsCasting
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
        Caster,

        /// <summary>
        /// The unit whose death is being dispatched, for a passive that has to ask something about
        /// the corpse rather than about who it is now acting on: "did the enemy that just died
        /// carry MY mark". Resolves to nobody outside a death broadcast, and a condition with no
        /// subject reads false, so an effect authored with this subject is inert elsewhere rather
        /// than wrong.
        /// </summary>
        Defeated,

        /// <summary>
        /// The unit a broadcast is about, for a passive that has to ask something about the unit
        /// that CAUSED the broadcast rather than about the effect's own target: "did the enemy
        /// whose buff just expired carry my mark", "is the enemy whose DoT ticked below 30% HP".
        ///
        /// Resolves to `_broadcastSubject` inside a BroadcastPassive and to nobody outside one,
        /// following the same pattern as Defeated and ThresholdUnit.
        /// </summary>
        BroadcastSubject,

        // ---------- batch two ----------

        /// <summary>
        /// The unit that last attacked the broadcast subject. Resolves via
        /// FindUnit(_broadcastSubject.LastAttackerId) inside a broadcast, and to nobody outside
        /// one. Used by passives that need to condition on the attacker's state.
        /// </summary>
        LastAttacker,

        /// <summary>
        /// The last unit that an effect in the current ability resolved against. Set after each
        /// effect's per-target loop, cleared at ability boundaries so it cannot leak across
        /// passives or between a basic and a passive it triggers. Lets a Self-targeted effect
        /// (EnergyGain, Heal, GainResolve) condition on the state of whatever the prior effect
        /// in the same ability just hit.
        /// </summary>
        LastTarget,

        /// <summary>
        /// The unit that CAUSED the broadcast in flight, read from `_broadcastSourceId`.
        ///
        /// GAP 8's condition half, and distinct from LastAttacker, which is the nearest thing that
        /// existed. LastAttacker resolves through `_broadcastSubject.LastAttackerId` - the last
        /// unit to have DAMAGED the subject - so it answers correctly only for a broadcast raised
        /// by a hit, and silently names the wrong body for one raised by a heal, a shield or a
        /// status landing. This names the causing unit directly, whatever the broadcast was about.
        ///
        /// Nobody outside a broadcast that carries a source, so a condition authored with this
        /// subject reads false elsewhere rather than wrong.
        /// </summary>
        BroadcastSource
    }

    /// <summary>
    /// What `EffectKind.RecordFact` writes onto a status entry.
    ///
    /// Every one of these is a fact `StatusInstance.Stored` cannot hold. Stored is a float: it can
    /// say "247 damage", and it cannot say "the buff was AttackUp", "the last action was an
    /// Active", "the ally was a Mender", or "the lowest this ever went was 38%". Ten abilities in
    /// the incoming roster ask for exactly those four shapes, so they get four typed fields on the
    /// entry rather than four bit-packing conventions on one float.
    /// </summary>
    public enum RecordKind
    {
        None,

        /// <summary>The most recent BENEFICIAL status on the subject, into
        /// `StatusInstance.StoredStatus`. Sylven's Scar Record, which detonates when the target
        /// regains the same one.</summary>
        LastPositiveStatus,

        /// <summary>Whether the subject's last turn was an Active or a Basic, into
        /// `StatusInstance.StoredAction`. Zella's Residue, which reads whether the action type
        /// changed.</summary>
        LastActionType,

        /// <summary>The subject's `HeroRole`, into `StatusInstance.StoredRole`. Vaelis records the
        /// role of whoever triggered Departure.</summary>
        SubjectRole,

        /// <summary>The LOWEST HP ratio the subject has been seen at while the entry lived, into
        /// `StatusInstance.StoredMin`. A running minimum rather than a snapshot, which is the
        /// distinction Arden's Everything Has a Limit turns on.</summary>
        MinHpRatio
    }

    /// <summary>
    /// Which stat a cross-unit comparison reads. Used by `EffectCondition.CasterStatAboveTarget`
    /// and `CasterStatBelowTarget`. A small closed set matching the stats the incoming roster
    /// actually compares: Speed (Oris, Talren), Energy (Dahlia), Attack and Hp.
    /// </summary>
    public enum ComparedStat
    {
        Speed,
        Energy,
        Attack,
        Hp
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
        Vulnerable,

        /// <summary>
        /// Turn progress taken from a unit and owed back, carried in `StatusInstance.Stored` as a
        /// fraction of a round rather than in Magnitude. The engine reads this one, the way it
        /// already reads Burn, Regen and Shield: at the debtor's own turn start it pays Energy in
        /// proportion to the debt and the entry is removed, which is the "repay" half every Turn
        /// Debt description names. Magnitude is the exchange rate, Energy lost per full round of
        /// debt.
        ///
        /// Harmful, and deliberately cleansable: nothing in the roster says otherwise, and a debt a
        /// Mender can wipe is what makes it a tempo tool rather than a tax.
        /// </summary>
        TurnDebt,

        // ---------- accuracy and crit override ----------
        //
        // APPENDED, never inserted, for the same reason every member above is. EvasionUp and
        // AccuracyDown together form the miss check in ApplyDamageEffect step -1: hitChance =
        // 1 - evasion - accuracyLoss, and Chance(1f) returns true without drawing, so no battle
        // without these statuses consumes an RNG draw.

        /// <summary>Target-side dodge chance. ADD-shaped: sum of EvasionUp magnitudes on the
        /// defender is the raw miss chance the attacker faces.</summary>
        EvasionUp,

        /// <summary>Source-side accuracy penalty. ADD-shaped: sum of AccuracyDown magnitudes on
        /// the attacker further reduces hit chance.</summary>
        AccuracyDown,

        /// <summary>Forces the next hit from this unit to crit. Consumed on the first damage
        /// effect that checks crit, skipping the RNG draw entirely.</summary>
        GuaranteedCrit,

        /// <summary>Prevents beneficial status application on the holder. A status that lands
        /// on the target is not applied if the target carries BuffSuppression and the incoming
        /// status is not harmful.</summary>
        BuffSuppression,

        /// <summary>
        /// Energy-gain interceptor. While on a unit, the first energy gain each round (per
        /// UsesPerRound) is reversed: the unit loses Magnitude energy instead of gaining, its
        /// turn is delayed by StoredCap fraction, and the disruptor's next ally in turn order
        /// is advanced by the same fraction.
        ///
        /// Engine-read, the same pattern as Burn/Regen/TurnDebt: the status sits on the target
        /// and the interceptor fires inside AddEnergy before AddEnergyClamped touches the unit's
        /// energy. Harmful, so Cleanse can remove it.
        /// </summary>
        Disrupted,

        // ---------- tier eight: max HP and holy damage ----------

        /// <summary>
        /// MULTIPLY-shaped max-HP buff, the same shape AttackUp is. EffectiveMaxHp reads it:
        /// MaxHp * (1 + SumMagnitude(MaxHpUp)). HpRatio and the healing cap both use
        /// EffectiveMaxHp, so the buff genuinely expands the bar rather than just raising a
        /// number nothing reads.
        /// </summary>
        MaxHpUp,

        /// <summary>
        /// ADD-shaped holy-damage buff. EffectiveHolyDamage reads it: HolyDamage +
        /// SumMagnitude(HolyDamageUp). The same shape Focus uses for Crit Chance.
        /// </summary>
        HolyDamageUp,

        // ---------- batch two: heal and gain suppression ----------

        /// <summary>
        /// Prevents healing on the holder. ApplyHealValue checks for this and skips the heal
        /// entirely, emitting no event. Harmful, so Cleanse can remove it.
        /// </summary>
        HealSuppression,

        /// <summary>
        /// Prevents Resolve gain on the holder. GainResolve checks for this and skips the stack
        /// addition. Harmful, so Cleanse can remove it.
        /// </summary>
        StatusGainSuppression,

        // ---------- batch three: second independent counter ----------

        /// <summary>
        /// A second independent stacking counter, identical in mechanics to Resolve but tracked
        /// separately. GainResolve reads effect.Status to pick which counter to grant (Resolve when
        /// None, Resolve2 when explicitly named), and ConsumesResolve spends the counter named by
        /// the same field. Dual-resource kits stack and spend the two counters independently.
        /// </summary>
        Resolve2,

        // ---------- linked targeting: two units bound together ----------

        /// <summary>
        /// A binding to one OTHER unit, named by StatusInstance.LinkedToId.
        ///
        /// THE ONLY STATUS THAT NAMES A THIRD UNIT. Every other entry in this enum is about its
        /// holder, told apart from another holder's copy by SourceId - who applied it. A Link needs
        /// both: SourceId is still the hero that created it, and LinkedToId is the partner the
        /// binding points at. Those are usually three different bodies (Talren binds two enemies to
        /// each other), which is why the partner could not be folded into SourceId.
        ///
        /// What the engine does with it is StatusInstance.LinkTransfer, and it is read at exactly
        /// one place - the damage funnel in DealRawDamage:
        ///
        ///   Redirect: a Magnitude share of the holder's incoming damage lands on the partner
        ///   INSTEAD. The holder takes that much less. This is Covered's rule with the destination
        ///   named by the link rather than by the applier, and it runs through the same code -
        ///   see Sim.RedirectShare.
        ///
        ///   Echo: a Magnitude share of the holder's incoming damage ALSO lands on the partner. The
        ///   holder takes the hit in full. The same split arithmetic as Redirect and the opposite
        ///   bookkeeping, which is the whole difference between "instead" and "as well".
        ///
        ///   None: nothing at the funnel. The entry is a pure record, and only the two link
        ///   triggers read it.
        ///
        /// WHAT A REDIRECT MOVES IS THE DAMAGE, NOT THE CALCULATION, and that is worth stating
        /// because it is the one place a reader will expect otherwise. DealRawDamage is handed a
        /// number that has already been through the attacker's crit roll, the aspect chart and the
        /// HOLDER's Defense, Armor Break and Damage Reduction. Redirecting it does not re-run any
        /// of that against the partner: the partner's own Defense is not applied, and its own
        /// Guard, Cover, shield pool and Ledger are, because those all live inside the funnel. That
        /// is exactly how Covered has always behaved, and a link that recomputed instead would be a
        /// second, differently-shaped redirect rather than the same one used twice.
        ///
        /// Neither harmful nor beneficial: IsHarmful returns false, like Mark and Ledger, so a
        /// Cleanse does not take it. Dispel does, since it removes anything not harmful - a link is
        /// a claim on a body and answering it is what Dispel is for.
        /// </summary>
        Linked,

        /// <summary>
        /// Incoming healing is reduced by Magnitude, as a fraction.
        ///
        /// HealSuppression is the same idea at 100% and was the only one of the pair the engine
        /// had, so "receives 20% less healing" had nowhere to go and two clauses were waiting on
        /// it - Sylven's talent III and Demos's `No Appeal`. A second status rather than a
        /// magnitude on the first, because HealSuppression is authored as a flag with no magnitude
        /// by four shipped effects and giving it one would have made every one of them mean
        /// something new.
        ///
        /// Harmful, so a Cleanse takes it and a Dispel does not - the same side of that line as
        /// every other stat-down.
        /// </summary>
        HealingReduction,

        /// <summary>
        /// A target restriction the holder carries for as long as the entry lives.
        ///
        /// TARGETBAN WAS THE RIGHT RULE ATTACHED TO THE WRONG THING. It is adopted from a
        /// PassiveDef in CombatUnit.AddPassive and written nowhere else, so it is a standing
        /// property of a unit decided at battle start - and the first clause that wanted one
        /// wanted it to ARRIVE mid-battle, last two rounds and leave.
        ///
        /// The shape is copied from Disrupted, which had the same problem and solved it: a status
        /// instance carrying its own parameters, read at the site that enforces them. The entry's
        /// BanKind and BanStatus are what TargetBanned reads, exactly as InterceptDisrupted reads
        /// the Magnitude and StoredCap off a Disrupted entry.
        ///
        /// Harmful: it is done TO a unit, so a Cleanse takes it and a Dispel does not.
        /// </summary>
        Restricted
    }

    /// <summary>
    /// What a StatusKind.Linked entry does to damage at its holder - arriving for the first three
    /// members, LEAVING for the fourth.
    ///
    /// None is a real authoring rather than an absence: Vaelis's binding exists so her passive can
    /// hear about a hit on the other end, and a link that also moved damage would be a different
    /// ability. It is the default, so an entry created without a transfer named is inert at the
    /// funnel.
    ///
    /// The outgoing direction (Deflect) joined this enum rather than becoming a second status
    /// because it is the same binding answering a different question about the same pair. A
    /// separate kind would have meant two bodies of link bookkeeping - creation, charges, the
    /// from-caster filter, MoveStatus carriage - kept in step by hand.
    /// </summary>
    public enum LinkTransfer
    {
        /// <summary>The link moves no damage. Only OnLinkedUnitTargeted and OnLinkedUnitHit read
        /// the entry.</summary>
        None,

        /// <summary>A share of the holder's incoming damage lands on the partner INSTEAD of on the
        /// holder. The holder takes that much less.</summary>
        Redirect,

        /// <summary>A share of the holder's incoming damage ALSO lands on the partner. The holder
        /// takes the hit in full.</summary>
        Echo,

        /// <summary>
        /// GAP 5. An attack the HOLDER makes against the link's CREATOR lands on the holder's
        /// partner instead, for a Magnitude share of what it would have dealt.
        ///
        /// THE ONLY MEMBER THAT READS DAMAGE LEAVING ITS HOLDER RATHER THAN ARRIVING AT IT, which
        /// is why the enum's own summary had to widen. Redirect and Echo both ask "what happens to
        /// a blow aimed at this body"; this asks "what happens to a blow this body aims at the
        /// hero who bound it". Same primitive, other direction.
        ///
        /// It is its OWN RULE rather than a loosened Provoked, which was the alternative and was
        /// rejected deliberately. Provoked refuses a same-side destination
        /// (`provoker.IsPlayer == caster.IsPlayer` is skipped) because it means "this attacker
        /// answers to the unit that provoked it", and an enemy cannot be provoked into attacking
        /// its own ally without that sentence stopping being true. Widening it to serve a deflect
        /// would weaken a guard that is correct for what Provoked means, so the deflect sits beside
        /// the redirect path instead and shares its arithmetic.
        ///
        /// THREE CONDITIONS, all of which must hold: the damage is arriving at the link's SourceId
        /// (the hero who created it), the attacker is the entry's holder, and the partner is alive
        /// and is not the victim. Nothing else in the funnel keys on the attacker's own statuses
        /// except Vulnerable and AccuracyDown, so this is a new question asked at one point.
        /// </summary>
        Deflect
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
        OnAllyWouldDie,

        // ---------- appended: a unit died and somebody other than its killer cares ----------
        //
        // OnKill reaches the killer and nobody else, which is the wrong shape for every ability
        // that watches its own mark rather than its own kills: the mark's owner is usually not the
        // unit that landed the blow. These two broadcast instead, split by side because the two
        // cases in the roster are opposite - four abilities watch an ENEMY die carrying their mark,
        // one watches an ALLY die carrying its ward.
        //
        // Both fire inside a window where the dead unit still has its statuses; see
        // TargetMode.DefeatedUnit.
        OnEnemyDefeated,
        OnAllyDefeated,

        /// <summary>A unit paid off a TurnDebt at its own turn start. Broadcast to the other side,
        /// since the ability watching a debt is always the one that applied it.</summary>
        OnTurnDebtRepaid,

        /// <summary>The owner dodged an attack (the accuracy/evasion miss check triggered).
        /// Fires on the DODGER, not on the attacker.</summary>
        OnDodge,

        /// <summary>
        /// An enemy with TurnDebt gained enough energy to use its active skill. Broadcast to the
        /// opposing side after the energy has been added, with `_thresholdUnit` set to the enemy.
        /// TargetMode.ThresholdUnit resolves to that enemy during the broadcast and to nobody
        /// outside it.
        ///
        /// The trigger fires ONCE per crossing: the before-energy was below the active's cost and
        /// the after-energy is at or above it. A per-target limit on the passive handles the
        /// "once per enemy each battle" rider.
        /// </summary>
        OnEnemyEnergyThreshold,

        // ---------- applier-side status lifecycle ----------
        //
        // Every trigger above fires on the CARRIER of a status or on the unit something happened
        // TO. These two fire on the unit that APPLIED a status, because the descriptions that need
        // them are written from the applier's perspective: "when a Burn she applied ticks",
        // "when a debuff he applied expires".
        //
        // Broadcast to the applier's side, with _broadcastSubject set to the carrier, so an effect
        // conditioned on the carrier (BroadcastSubject) can read the carrier's status list or HP.
        // The applier is identified by StatusInstance.SourceId, which is already stored on every
        // status entry.

        /// <summary>A status the owner applied to a unit on the OTHER side expired naturally
        /// (duration ran out). Broadcast to the applier's side after the status has been removed.
        /// _broadcastSubject is the unit whose status expired.</summary>
        OnAppliedStatusExpired,

        /// <summary>A Burn the owner applied to a unit on the OTHER side ticked. Broadcast to the
        /// applier's side after the tick's damage has been dealt. _broadcastSubject is the unit
        /// that took the tick.</summary>
        OnAppliedDoTTick,

        // ---------- tier eight: turn-end triggers ----------

        /// <summary>
        /// An ally of the owner finished its turn. Broadcast to the acting unit's own side after
        /// ability resolution and before ExpireStatuses, with _broadcastSubject set to the acting
        /// unit. The acting unit is EXCLUDED (except parameter), since it already has TurnStart
        /// and AfterBasicAttack of its own.
        /// </summary>
        OnAllyTurnEnd,

        /// <summary>
        /// An enemy of the owner finished its turn. Broadcast to the OTHER side after ability
        /// resolution and before ExpireStatuses, with _broadcastSubject set to the acting enemy.
        /// The natural home for "when an enemy finishes its turn while carrying TurnDebt".
        /// </summary>
        OnEnemyTurnEnd,

        // ---------- tier nine: parameterisation gaps ----------

        /// <summary>
        /// A Ledger entry the owner applied to an enemy just reached its StoredCap. Broadcast
        /// to the applier's side after the banking completes, with _broadcastSubject set to the
        /// unit carrying the Ledger. Fires once per cap event, not once per hit that happens to
        /// push past it.
        /// </summary>
        OnLedgerCapped,

        /// <summary>
        /// A TurnDebt entry the owner applied to an enemy accumulated Stored past the passive's
        /// Threshold. Broadcast to the applier's side, with _thresholdUnit set to the debtor.
        /// Per-target limits on the passive handle "once per enemy".
        /// </summary>
        OnTurnDebtThreshold,

        // ---------- batch two ----------

        /// <summary>
        /// The holder's shield pool was depleted by incoming damage. Raised in DealRawDamage
        /// when target.Shield reaches zero, on the unit whose shield broke.
        /// </summary>
        OnShieldBreak,

        // ---------- batch three ----------

        /// <summary>
        /// An enemy dropped to or below PassiveDef.Threshold of its max HP. Broadcast to the
        /// OTHER side (the one that dealt the damage) after the hit lands, mirroring
        /// OnAllyHpBelowThreshold but for enemy units. _broadcastVictim is set to the enemy.
        /// </summary>
        OnEnemyHpBelowThreshold,

        // ---------- funnel-banking primitives ----------

        /// <summary>
        /// A stacking status on the owner reached PassiveDef.StackThresholdCount entries.
        /// Raised after GainResolve adds the crossing stack. The matching stacks are consumed
        /// inside TriggerPassive before the passive's effects resolve, so the effects see the
        /// unit without the spent counter.
        /// </summary>
        OnStackThreshold,

        // ---------- batch five ----------
        //
        // Four broadcast triggers plus one same-side applier lifecycle, all serving heroes
        // whose kits react to events on allies or enemies that the existing triggers cannot
        // reach. No shipped content sets any of these.

        /// <summary>An ally of the owner had its shield pool broken by incoming damage.
        /// Broadcast to the broken unit's OWN side (excluding the broken unit itself, which
        /// already has OnShieldBreak). _broadcastSubject and _broadcastVictim are the broken
        /// unit; _shieldBreakerId is the attacker. Darian reacts to an ally's shield
        /// breaking.</summary>
        OnAllyShieldBreak,

        /// <summary>A Ledger entry on a same-side unit just banked. Broadcast to the
        /// applier's side (excluding the carrier), with _broadcastSubject set to the carrier.
        /// _lastLedgerBankedStored holds the Ledger's total Stored after the banking event.
        /// Brenna copies a fraction of an ally's recorded Memory.</summary>
        OnAllyLedgerBanked,

        /// <summary>An enemy was healed (actual HP restored). Broadcast to the OPPOSING side,
        /// with _broadcastSubject and _broadcastVictim set to the healed enemy. Velis reacts
        /// to enemy healing.</summary>
        OnEnemyHealed,

        /// <summary>A FunnelConvert Ledger entry the owner applied just converted a heal into
        /// damage. Broadcast to the applier's side, with _broadcastSubject set to the unit
        /// that was hit by the converted damage. Velis gains Remnant when Hollowed
        /// fires.</summary>
        OnFunnelConvertFired,

        /// <summary>A status the owner applied to a unit on the SAME side (including self)
        /// expired naturally. Broadcast to the carrier's side after the status has been
        /// removed. _broadcastSubject is the unit whose status expired. Filtered by
        /// TriggerStatus the same way OnAppliedStatusExpired is. Darian counts same-side
        /// shield expiries.</summary>
        OnAppliedAllyStatusExpired,

        // ---------- linked targeting: the far end of a Link ----------
        //
        // A NEW DISPATCH SHAPE, and the third one in this enum. The owner-centric triggers fire on
        // the unit something happened to; the broadcasts fire on a whole side; these two fire on
        // whoever holds a StatusKind.Linked entry NAMING the unit something happened to, wherever
        // that holder stands. Sim.DispatchLinkPassive walks the units once looking for that entry.
        // Neither is a broadcast, because a link is not a side: Rovan's partner is an ally and
        // Talren's are two enemies, and a side-scoped dispatch would answer the wrong question in
        // one of those two cases.
        //
        // Both are gated on Sim._anyLinks, a bool that is false until the first Link lands. No
        // battle without a link pays more than one bool test at either dispatch point, which is
        // what keeps every existing fingerprint where it is.
        //
        // SPLIT INTO TWO, not folded into one with a flag, because "targeted" and "hit" are
        // genuinely different moments and a kit needs to say which. A blow that misses, is Blocked
        // or is fully absorbed was aimed and never landed.

        /// <summary>
        /// The owner's linked partner was SELECTED as the target of a Damage effect. Raised at the
        /// top of ApplyDamageEffect, before the miss check, so it fires for a hit that goes on to
        /// miss, to be Blocked, or to be soaked whole by a ward.
        ///
        /// Deliberately NOT raised for every kind of targeting. A heal, a buff or a cleanse also
        /// names a target, and ResolveTargets is reached by all of them - but firing a passive from
        /// inside targeting means firing it after the targets are chosen and before the effect has
        /// resolved, where a passive that kills the target leaves the caller holding a corpse it
        /// already decided to act on. The same argument OnEnemyDamagedAlly settled: the damage half
        /// has one funnel and one well-defined moment, so that is the half raised correctly rather
        /// than four call sites with four meanings. "An attack aimed at" is what the abilities
        /// asking for this say, and an attack is a Damage effect.
        ///
        /// `_broadcastSourceId` carries the attacker, so TriggerSourceIsSelf and
        /// TriggerSourceIsAlly can filter by who aimed it.
        /// </summary>
        OnLinkedUnitTargeted,

        /// <summary>
        /// The owner's linked partner TOOK DAMAGE. Raised in DealRawDamage once the hit has landed
        /// and the Damage event is in the record, so the passive answers the damage that actually
        /// happened rather than the damage that was attempted.
        ///
        /// Raised for every point of damage the funnel carries, a Burn tick and a redirected share
        /// included, because they are all the partner being hurt and the funnel does not rank them.
        /// `_broadcastSourceId` carries whoever dealt it - which for a link's own echo is the
        /// original attacker, not the link - so TriggerSourceIsAlly is what "when an ALLY hits the
        /// unit I am bound to" reads.
        /// </summary>
        OnLinkedUnitHit,

        // ---------- the nine-gap pass: four moments nothing could hear ----------
        //
        // GAP 6. Each fires on the unit the thing happened TO - the owner-centric shape - and each
        // publishes `_broadcastSourceId`, so TriggerSourceIsSelf, TriggerSourceIsAlly and the new
        // BroadcastSource subject and target mode can all read who caused it.

        /// <summary>
        /// The owner was SELECTED as the target of a Damage effect. Raised at the top of
        /// ApplyDamageEffect beside the link's OnLinkedUnitTargeted and under the same reasoning:
        /// it fires for a blow that goes on to miss, to be Blocked or to be soaked whole, because
        /// being aimed at is not the same event as being hit.
        ///
        /// The owner-centric half of a pair whose broadcast half already existed.
        /// OnLinkedUnitTargeted reaches a unit BOUND to the body being aimed at; this reaches the
        /// body itself, which is what "the next time that enemy targets me" needs and what nothing
        /// could hear before - OnDamaged was the nearest and answers a strictly later question.
        ///
        /// DAMAGE EFFECTS ONLY, the same narrowness OnLinkedUnitTargeted is held to, and for the
        /// same reason: a heal, a buff and a cleanse all name a target, and ResolveTargets is the
        /// point they share, but firing a passive from inside targeting means firing after the
        /// targets are chosen and before the effect resolves, where a passive that kills the
        /// target leaves the caller holding a corpse it has committed to acting on.
        /// </summary>
        OnTargeted,

        /// <summary>
        /// The owner GAINED a status. Raised in the ApplyStatus branch on the unit that received
        /// it, after the entry is on the unit so a passive answering it reads the board it is
        /// answering. Filtered by PassiveDef.TriggerStatus exactly as OnStatusExpired is, so
        /// "whenever she becomes Untargetable" is this trigger with TriggerStatus Stealth.
        ///
        /// The missing corner of a lifecycle that had every other one covered: OnStatusRemoved
        /// fires on the remover, OnStatusExpired on the carrier at expiry, OnEnemyBuffGained on
        /// the other side, OnAppliedStatusExpired and OnAppliedAllyStatusExpired on the applier.
        /// Nothing fired on the CARRIER at the moment it gained one.
        ///
        /// Fires for an application that REFRESHES an existing entry as well as one that creates
        /// it, because "becomes Untargetable" is true of a unit whose concealment was renewed and
        /// the two are indistinguishable to whoever is watching. Does NOT fire for a status that
        /// arrives by MoveStatus or TransferStatus: those are a status changing CARRIER, which is
        /// its own event with its own record (StatusSpread), and folding them in would make a
        /// passive fire on somebody else's misfortune arriving second-hand.
        /// </summary>
        OnStatusGained,

        /// <summary>
        /// AN ECHO THE OWNER CAUSED just struck a second body - either a StatusKind.Linked entry
        /// the owner created firing its echo at the funnel, or an EffectKind.EchoDamage the owner
        /// resolved. Not a broadcast: it is raised directly on the one unit the event is about,
        /// the link's SourceId or the effect's caster. _broadcastSubject and _broadcastVictim are
        /// the body the echo landed on, so an effect can aim at it, and `_lastMoved` for the owner
        /// holds the echoed amount.
        ///
        /// RENAMED FROM OnLinkEchoFired AND WIDENED TO COVER BOTH, because the kit that wants it
        /// wants both in one passive: "whenever his attack triggers against a secondary target
        /// through Redirecting Shot OR Change the Course" is one sentence, and those two abilities
        /// echo by the two different routes. A trigger that heard the link half and not the effect
        /// half would have split one description across two passives, or left half of it silent -
        /// which is what it did before this. Same ordinal; nothing authors it.
        ///
        /// Fires once per echo, an echo that killed its victim included.
        /// </summary>
        OnEchoFired,

        /// <summary>
        /// The owner RECEIVED a redirected share - a Covered ward's split or a link's Redirect -
        /// rather than being hit directly. Raised on the destination once the share has landed,
        /// with _broadcastSubject set to the body it was moved OFF, _broadcastSourceId to the
        /// original attacker, and `_lastMoved` for the owner holding the share.
        ///
        /// A protector could not previously tell a redirected share from any other hit: both
        /// arrive as OnDamaged with the attacker as source, and the body being protected appears
        /// nowhere in what the protector can read. That is the whole of what "when Rovan receives
        /// redirected damage from Covered" has to know.
        ///
        /// Raised for BOTH mechanics rather than for the link alone, because they are one path -
        /// Sim.RedirectShare - and a trigger that fired for one and not the other would be the two
        /// drifting apart at their first opportunity. No shipped Covered has a listener, so the
        /// dispatch walks an empty passive list in every existing battle.
        /// </summary>
        OnRedirectReceived,

        /// <summary>
        /// A redirect the owner's OWN RULE performed just moved a share of a blow - a Covered ward
        /// it applied, a Linked entry it created, a Deflect it placed on an attacker. Raised on
        /// that owner, with _broadcastSubject and _broadcastVictim set to the body the share came
        /// OFF and _broadcastSourceId to the attacker, and with `_lastMoved` for the owner holding
        /// the share itself.
        ///
        /// THE THIRD PARTY TO A REDIRECT. The move has a body it came off, a body it landed on and
        /// a hero whose decision moved it, and only the second had a hook - so "when I redirect
        /// damage" had nothing to listen to at all. Six abilities across five heroes ask for it,
        /// three of them already shipped and currently approximated onto OnDamaged, which fires on
        /// every hit the hero takes rather than on the redirects it caused.
        ///
        /// NOT raised when the owner is also the destination, which is every ordinary Covered ward:
        /// that unit has already been told by OnRedirectReceived, and raising both on one body
        /// would have a hero listening to each answer a single event twice.
        /// </summary>
        OnRedirectSent,

        /// <summary>
        /// The start of a round, raised on every living unit before any of them acts.
        ///
        /// TurnStart is per-unit and fires when that unit's own turn comes up, which is a
        /// different moment and a different number of times. "At the start of each round,
        /// designate the fastest living enemy" needs the round's own boundary: designating at
        /// Merek's turn names whoever is fastest by then, after everything before him has moved
        /// the order about.
        /// </summary>
        RoundStart,

        /// <summary>
        /// An enemy's active skill has finished resolving.
        ///
        /// OnEnemyActive is raised BEFORE the cast, deliberately, so a passive answering it acts
        /// on the board the caster is about to hit. This is the other end of the same event, for
        /// the clauses whose text says "after the skill resolves" - and the difference is not
        /// cosmetic, because the board those two see is the one either side of everything the
        /// active did.
        /// </summary>
        OnEnemyActiveResolved,

        /// <summary>
        /// A cast has been declared and has not resolved. Raised on EVERY living unit, not
        /// broadcast to one side.
        ///
        /// OnEnemyActive is the same moment seen from the other team and is the older, narrower
        /// hook: it reaches the caster's opponents only. A declaration is a fact about the board,
        /// and the three things this primitive exists for - an interrupt, a reaction to one named
        /// ability, a blow that lands before the cast it answers - are all wanted by allies as
        /// often as by enemies.
        ///
        /// Anything this raises resolves BEFORE the cast does, which is what makes "damage that
        /// lands before the cast it answers" a matter of ordering rather than of a new mechanism.
        /// </summary>
        OnCastDeclared,

        /// <summary>
        /// An enemy has aimed at a different body than the one it last hit. Broadcast to the side
        /// opposite the switcher, with the switcher as the broadcast SOURCE and the body it has
        /// just turned on as the SUBJECT - so "the targeted ally" is nameable and "the enemy that
        /// changed" is testable, which are the two halves every clause asking for this wants.
        ///
        /// Raised where PreviousTargetId is written, which is the engine's own definition of the
        /// target having changed and was until now the only thing that knew: the field updates
        /// silently inside DealRawDamage and is readable only by the picker, through
        /// SubjectIsNotMyPreviousTarget. An observer had no hook at all.
        ///
        /// A unit's FIRST blow does not raise it. Acquiring a target is not changing one, and
        /// LastTargetId being unset is the difference; every clause in the roster says "changes".
        /// </summary>
        OnEnemyChangedTarget
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
    /// <summary>
    /// A standing rule forbidding a unit one class of target.
    ///
    /// THE PRIMITIVE, NOT ONE HERO'S CLAUSE. Three descriptions in the roster say a unit may not
    /// pick something - "cannot pick the same target twice in a row", "cannot pick a unit carrying
    /// X", "cannot pick the unit it picked last round" - and each of them is the same shape: a
    /// predicate on a candidate, owned by the unit doing the picking, asked at pick time. Building
    /// any one of them as its own rule would have produced three rules that had to agree about the
    /// anchor, about concealment and about what happens when nothing is left.
    ///
    /// Declared on a PassiveDef and adopted onto the unit the way OutputModStatus already is,
    /// because a rule of this kind is a property of the unit rather than of one ability: "Kest
    /// cannot change targets while his target lives" is true of Kest, not of his Basic Attack.
    /// </summary>
    public enum TargetBan
    {
        /// <summary>No restriction. Every unit in shipped content.</summary>
        None,

        /// <summary>The body this unit's last attack landed on - CombatUnit.LastTargetId.</summary>
        LastTarget,

        /// <summary>The body it was aimed at as the previous round ended - LastRoundTargetId. A
        /// sibling of LastTarget with a round's granularity rather than a blow's.</summary>
        LastRoundTarget,

        /// <summary>Any body carrying the status named by TargetBanStatus, whoever applied it.</summary>
        CarryingStatus
    }

    /// <summary>
    /// What kind of outgoing value a per-stack output modifier scales.
    /// </summary>
    public enum OutputModScope
    {
        /// <summary>All outgoing damage, both direct and DoT.</summary>
        AllDamage,

        /// <summary>Only DoT tick damage (Burn).</summary>
        DoTOnly,

        /// <summary>Only outgoing healing.</summary>
        HealingOnly
    }

    /// <summary>
    /// Which point in the damage/heal pipeline a Ledger entry banks from. Defaults to Damage,
    /// which is the existing behaviour: banks from incoming damage in DealRawDamage. The three
    /// new members read amounts at other funnel points and bank or convert them.
    /// </summary>
    public enum FunnelSource
    {
        /// <summary>Banks from incoming damage hitting the holder (existing Ledger behaviour).</summary>
        Damage,

        /// <summary>Banks from the amount a shield pool absorbed in DealRawDamage.</summary>
        ShieldAbsorb,

        /// <summary>Banks from the amount DamageReduction prevented in ApplyDamageEffect.</summary>
        DamageMitigation,

        /// <summary>Banks from the incoming heal amount before it restores HP. When
        /// FunnelConvert is true on the entry, the intercepted amount is dealt as damage
        /// immediately instead of banked.</summary>
        HealIntercept,

        /// <summary>Banks from the amount Guard charges prevented in DealRawDamage.</summary>
        GuardMitigation
    }

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
        /// relic's text - which is how a legendary relic states itself with no number and still
        /// has a magnitude.
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
                || kind == StatusKind.Vulnerable
                || kind == StatusKind.TurnDebt
                // AccuracyDown lowers the holder's hit chance; BuffSuppression prevents buff
                // application. Both are debuffs — cleansable, and Umbra locks them.
                || kind == StatusKind.AccuracyDown
                || kind == StatusKind.BuffSuppression
                // Partial healing reduction, on the same side of the line as HealSuppression's
                // total: done TO a unit, so a Cleanse takes it and an Umbra source locks it.
                || kind == StatusKind.HealingReduction
                // A restriction is done TO a unit, so it sits with Provoked and Vulnerable.
                || kind == StatusKind.Restricted
                // Disrupted reverses energy gains. A debuff — cleansable, same as TurnDebt.
                || kind == StatusKind.Disrupted
                // HealSuppression blocks healing; StatusGainSuppression blocks Resolve gain.
                || kind == StatusKind.HealSuppression
                || kind == StatusKind.StatusGainSuppression;
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
                case StatusKind.TurnDebt: return "Turn Debt";
                case StatusKind.EvasionUp: return "Evasion";
                case StatusKind.AccuracyDown: return "Accuracy Down";
                case StatusKind.GuaranteedCrit: return "Guaranteed Crit";
                case StatusKind.BuffSuppression: return "Nullify";
                case StatusKind.Disrupted: return "Disrupted";
                case StatusKind.MaxHpUp: return "Max HP Up";
                case StatusKind.HolyDamageUp: return "Holy Damage Up";
                case StatusKind.HealSuppression: return "Heal Suppression";
                case StatusKind.StatusGainSuppression: return "Gain Suppression";
                case StatusKind.Resolve2: return "Resolve II";
                case StatusKind.Linked: return "Linked";
                case StatusKind.Restricted: return "Displaced";
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
                case StatusKind.TurnDebt: return "DEBT";
                case StatusKind.EvasionUp: return "EVA";
                case StatusKind.AccuracyDown: return "ACC-";
                case StatusKind.GuaranteedCrit: return "CRIT!";
                case StatusKind.BuffSuppression: return "NULL";
                case StatusKind.Disrupted: return "DIS";
                case StatusKind.MaxHpUp: return "HP+";
                case StatusKind.HolyDamageUp: return "HLY+";
                case StatusKind.HealSuppression: return "H-SUP";
                case StatusKind.StatusGainSuppression: return "G-SUP";
                case StatusKind.Resolve2: return "RSV2";
                // One tag for every link, the same cost the marks already pay: a StatusKind has no
                // per-instance display name, so a bar cannot say WHO the holder is bound to. That
                // is the same UI problem Mark carries and it is not made worse here.
                case StatusKind.Linked: return "LNK";
                case StatusKind.Restricted: return "DSP";
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
