using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;

namespace Emberfall.Core.Combat
{
    public sealed class StatusInstance
    {
        public StatusKind Kind;
        public int RoundsLeft;
        /// <summary>Fraction for stat modifiers, flat per-tick value for Burn / Regen.</summary>
        public float Magnitude;
        public int SourceId = -1;

        /// <summary>
        /// What a StatusKind.Ledger entry has banked so far, and 0 on every other kind.
        ///
        /// On the entry rather than on the unit, because a ledger in the roster is always a mark
        /// that happens to be counting: it expires, moves and gets dispelled with the status that
        /// carries it, and a parallel per-unit store would need its own lifetime rules kept in
        /// step with it by hand. StoredCap of 0 means uncapped.
        /// </summary>
        public float Stored;
        public float StoredCap;

        /// <summary>
        /// Uses left on a charge-based status, or 0 on every other kind.
        ///
        /// A separate field from Stored rather than a reuse of it, even though both are "a number
        /// on a status entry". Stored is a float the Ledger accumulates UP toward a cap; this is an
        /// int that counts DOWN to removal, and one status can legitimately want both - a mark that
        /// records damage and also wears off after three hits. Sharing one field would make that
        /// unauthorable and would make either meaning ambiguous on a status carrying the other.
        /// </summary>
        public int Charges;

        // ---------------------------------------------------------------- the non-numeric ledger
        //
        // Four TYPED fields rather than four conventions packed into Stored. Stored is a float: it
        // can hold "247 damage" and cannot hold "the buff was AttackUp", "the last action was an
        // Active", "the ally was a Mender" or "the lowest this ever went was 38%". Ten abilities in
        // the incoming roster want exactly those four, and encoding an enum's ordinal into a float
        // would be a convention nothing enforces and every future reader has to be told about.
        //
        // All four travel with the entry through MoveStatus, for the same reason Stored does: a
        // record that loses what it recorded when it changes carrier is not a record.

        /// <summary>A status identity this entry has recorded, or None.</summary>
        public StatusKind StoredStatus = StatusKind.None;

        /// <summary>What the recorded unit last did: 0 nothing yet, 1 a Basic, 2 an Active. An int
        /// rather than a bool because "has not acted" is a third answer and the abilities that read
        /// this ask whether the action type CHANGED, which needs to know there was a previous.</summary>
        public int StoredAction;

        /// <summary>A HeroRole this entry has recorded. Meaningless unless StoredRoleSet.</summary>
        public HeroRole StoredRole;
        public bool StoredRoleSet;

        /// <summary>The lowest HP ratio the recorded unit has been seen at while this entry lived.
        /// A running minimum, not a snapshot, which is the distinction Arden's kit turns on.
        /// Negative means nothing has been recorded yet, so 0% is distinguishable from unset.</summary>
        public float StoredMin = -1f;

        // ---------------------------------------------------------------- per-round uses
        //
        // "The first area hit each round", "the first time that enemy damages an ally each round",
        // "the first positive status it gains each round". That is an ordinal on the STATUS, not on
        // the passive: PassiveLimit.OncePerRound already answers "this passive fires once a round",
        // and it is the wrong instrument here because the thing being counted happens to a status's
        // carrier and the status can move, expire or be dispelled mid-round.
        //
        // Counts UP and is reset in BeginRound, unlike Charges which counts DOWN to removal and
        // never resets. A status with UsesPerRound 0 is unlimited, which is every status in
        // shipped content.
        public int UsesThisRound;
        public int UsesPerRound;

        // ---------------------------------------------------------------- tier eight: ledger extensions
        //
        // Carried on the Ledger entry alongside Stored and StoredCap, because they are properties
        // of the individual mark rather than of the unit or the kind.

        /// <summary>When true, the Ledger auto-releases its Stored as damage to the holder when
        /// its duration expires.</summary>
        public bool AutoRelease;

        /// <summary>When true, the Ledger mitigates incoming damage by spending Stored instead
        /// of banking into it.</summary>
        public bool Mitigates;

        /// <summary>Max banking events per round, 0 for unlimited. Reset in BeginRound.</summary>
        public int BankLimit;
        public int BankUsesThisRound;

        // ---------------------------------------------------------------- tier nine: ledger parameterisation

        /// <summary>When true, this Ledger banks overheal instead of incoming damage.</summary>
        public bool BankHeals;

        /// <summary>Only bank when the incoming damage's dotKind matches this status. None means
        /// bank everything. Ignored when BankHeals is true.</summary>
        public StatusKind BankOnlyFrom = StatusKind.None;

        /// <summary>When true, auto-release on expiry heals instead of dealing damage.</summary>
        public bool AutoReleaseAsHeal;

        /// <summary>Who receives the auto-release payout. Self (default) means the holder.</summary>
        public TargetMode AutoReleaseTarget = TargetMode.Self;

        /// <summary>When true, only bank damage whose SourceId matches this entry's SourceId.</summary>
        public bool BankFromCaster;
    }

    /// <summary>Mutable battle-time state for one combatant.</summary>
    public sealed class CombatUnit
    {
        public int Id;
        public bool IsPlayer;
        public int Slot;
        public string DefId = "";
        public string Name = "";
        public int Level = 1;
        public Aspect Aspect = Aspect.Ember;
        public HeroRole Role = HeroRole.Striker;
        public int Stars = 5;
        public bool IsBoss;
        public VisualDef Visual = new VisualDef();

        public float MaxHp;
        public float Hp;
        public float Attack;
        public float Defense;
        public float Speed;
        public float CritChance;
        public float CritDamage;
        public float EffectHit;
        public float EffectResist;
        public float SkillDamage;
        public float Block;
        public float ArmorBreak;
        public float ControlImmunity;
        public float DamageReduction;
        public float HolyDamage;

        public int Energy;
        public int EnergyMax = 100;
        public float Shield;
        public bool Alive = true;

        /// <summary>Re-rolled once per round by the sim; keeps turn order from being the same every fight.</summary>
        public float RoundSpeedJitter;

        /// <summary>
        /// Whether this unit's statuses have already had their once-per-round RoundsLeft decrement.
        /// Set by Sim.ExpireStatuses, cleared at the top of every round. Nothing reaches
        /// ExpireStatuses twice in one round today; see the comment there for why the gate stays.
        /// </summary>
        public bool StatusesExpiredThisRound;

        public AbilityDef Basic;
        public AbilityDef Active;
        /// <summary>
        /// Every passive on this unit, in resolution order: the def's own, then the selected
        /// talent if the hero has one. Appended through AddPassive so the fired-flags below cannot
        /// drift out of step with it.
        ///
        /// A talent is not distinguished from a passive here, and that is the point - the engine
        /// has no talent branch at all. CombatSimulation.BuildUnits resolves the selection once,
        /// during setup, exactly as it resolves a relic id, and hands the result over as one more
        /// passive. An unselected talent resolves to null and is never added, so it does nothing.
        /// </summary>
        public readonly List<PassiveDef> Passives = new List<PassiveDef>();

        /// <summary>
        /// Whether each passive's oncePerBattle allowance is spent, indexed in step with Passives.
        ///
        /// Per passive, not per unit - which is the change, and it is a behaviour change for any
        /// unit carrying more than one. The single `PassiveFired` bool that stood here was written
        /// when a unit had exactly one passive, so "this unit's passive has fired" and "this
        /// passive has fired" were the same sentence. With two, the bool means the first passive to
        /// fire silences the second for the rest of the battle even though they are unrelated
        /// abilities on different triggers, which is not a rule anyone authored. A hero with a
        /// BattleStart buff and a once-per-battle OnHpBelowThreshold save would lose the save
        /// outright.
        ///
        /// No unit in shipped content carries two passives, so every flag list here has length one
        /// and behaves exactly as the bool did.
        /// </summary>
        private readonly List<bool> _passiveFired = new List<bool>();

        /// <summary>
        /// The round each passive last fired in, indexed in step with Passives, or 0 for one that
        /// has not fired. Read only by PassiveLimit.OncePerRound and EveryNRounds; a passive with
        /// no Limit never consults it, which is every passive in shipped content.
        /// </summary>
        private readonly List<int> _passiveLastRound = new List<int>();

        /// <summary>
        /// The equipped relic, resolved from HeroInstance.EquippedRelicId during setup, or null. Only ever
        /// non-null on a player unit: enemies come from EnemyDef, which has no relic slot, and
        /// DESIGN-RELICS scopes the slot to an owned hero on the hero detail screen.
        /// </summary>
        public RelicDef Relic;

        /// <summary>
        /// Whether this unit's relic has already announced itself this battle. DESIGN-RELICS:
        /// "Emit one the first time a relic alters an effect in a fight, not on every tick" -
        /// Splitting Prism would otherwise banner on every cast and Verdict Sigil on every turn.
        /// </summary>
        public bool RelicAnnounced;

        public int LastAttackerId = -1;
        public int LastTargetId = -1;

        // ---------- action history ----------
        //
        // NONE OF THIS REACHES THE SAVE. A CombatUnit is built fresh from a HeroDef and a
        // HeroInstance at the start of every fight, and the save stores the instance - uid, defId,
        // level, copies, relic, armor, selected talent - and nothing about how a battle went. So
        // the history below costs zero save bytes and needs no version bump, which is why this
        // branch has none.

        /// <summary>The target of the attack BEFORE the current one. LastTargetId is overwritten in
        /// ApplyDamageEffect as the hit is set up, so "a different target than last turn" cannot be
        /// answered from it alone - by the time anything asks, it already names the new target.</summary>
        public int PreviousTargetId = -1;

        /// <summary>Whether this unit has taken its turn in the round now being walked. Reset in
        /// BeginRound with everything else the round clears.</summary>
        public bool ActedThisRound;

        /// <summary>The round this unit last took a turn in, and the round it was last damaged in.
        /// Together they answer "took damage since my last turn" at round granularity.</summary>
        public int LastTurnRound;
        public int LastDamagedRound;

        /// <summary>Whether the last two turns this unit took were both an Active, or both a Basic.
        /// Two flags rather than a list: no description asks about more than the last two.</summary>
        public bool LastActionWasActive;
        public bool PreviousActionWasActive;
        public int ActionsTaken;

        /// <summary>Ids this unit has damaged in the round now being walked, cleared each round.
        /// Answers "already damaged this round" and "more than once in a round".</summary>
        public readonly List<int> DamagedThisRound = new List<int>();

        // ---------- batch two ----------

        /// <summary>HP snapshot stored by EffectKind.SnapshotHp, read by HpAboveSnapshot.</summary>
        public int SnapshotHp;

        /// <summary>Total damage this unit dealt this round, for HighestDamageDealtEnemy.</summary>
        public float DamageDealtThisRound;

        /// <summary>DoT tick damage this unit dealt this round, for HighestDoTDamageEnemy.</summary>
        public float DoTDamageDealtThisRound;

        /// <summary>Per-stack output modifier, copied from PassiveDef at build time.</summary>
        public StatusKind OutputModStatus = StatusKind.None;
        public float OutputModPerStack;
        public OutputModScope OutputModScope = OutputModScope.AllDamage;
        public bool OutputModMarkedOnly;

        /// <summary>How many times this unit has damaged that one this round.</summary>
        public int TimesDamagedThisRound(int targetId)
        {
            int n = 0;
            for (int i = 0; i < DamagedThisRound.Count; i++) if (DamagedThisRound[i] == targetId) n++;
            return n;
        }

        /// <summary>Everything a new round resets. One place, so the round loop cannot reset half
        /// of it and leave the rest reading last round's answers.</summary>
        public void BeginRound()
        {
            StatusesExpiredThisRound = false;
            ActedThisRound = false;
            DamagedThisRound.Clear();
            DamageDealtThisRound = 0f;
            DoTDamageDealtThisRound = 0f;

            // Per-round status uses, and the per-round half of the per-target limits. Both are
            // "how many times has this already happened THIS round", so both belong to the one
            // place a round is declared to have started.
            for (int i = 0; i < Statuses.Count; i++)
            {
                Statuses[i].UsesThisRound = 0;
                Statuses[i].BankUsesThisRound = 0;
            }
            _passiveTargetsThisRound.Clear();
        }

        // ---------------------------------------------------------------- per-target limits
        //
        // PassiveLimit answers a question about the OWNER's clock: has this passive fired yet this
        // round, this battle, since N rounds ago. Four abilities need a different question - has
        // this passive fired against THIS TARGET - and no amount of owner-side bookkeeping answers
        // it, because the count is per (passive, target) pair and the target's identity is only
        // known once an effect has resolved its targets.
        //
        // Keyed "passiveIndex:targetId" rather than by a nested dictionary: the count is tiny (a
        // passive fires at most a handful of times a battle against at most five bodies), and a
        // flat string key keeps the reset in BeginRound a single Clear.
        private readonly Dictionary<string, int> _passiveTargetsBattle = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _passiveTargetsThisRound = new Dictionary<string, int>();

        /// <summary>How many times passive `index` has already fired against `targetId`, over the
        /// whole battle or within this round.</summary>
        public int PassiveFiresAgainst(int index, int targetId, bool thisRoundOnly)
        {
            Dictionary<string, int> book = thisRoundOnly ? _passiveTargetsThisRound : _passiveTargetsBattle;
            int n;
            return book.TryGetValue(index + ":" + targetId, out n) ? n : 0;
        }

        /// <summary>Records one firing of passive `index` against `targetId`, in both books.</summary>
        public void NotePassiveFireAgainst(int index, int targetId)
        {
            string key = index + ":" + targetId;
            int n;
            _passiveTargetsBattle[key] = _passiveTargetsBattle.TryGetValue(key, out n) ? n + 1 : 1;
            _passiveTargetsThisRound[key] = _passiveTargetsThisRound.TryGetValue(key, out n) ? n + 1 : 1;
        }

        /// <summary>The share of one hit a protector takes for this unit, and who takes it.
        /// Zero when nobody is covering it.</summary>
        public float CoverFraction(out int protectorId)
        {
            protectorId = -1;
            float best = 0f;
            for (int i = 0; i < Statuses.Count; i++)
            {
                StatusInstance c = Statuses[i];
                if (c.Kind != StatusKind.Covered || c.Magnitude <= 0f) continue;
                // The deepest cover wins rather than the shares summing, so two protectors cannot
                // between them take more than the whole hit and leave the holder untouched.
                if (c.Magnitude > best) { best = c.Magnitude; protectorId = c.SourceId; }
            }
            return best;
        }

        /// <summary>Extra damage this unit takes from that attacker specifically, summed over every
        /// Vulnerable entry that attacker applied. Zero from anybody else.</summary>
        public float VulnerableTo(int sourceId)
        {
            if (sourceId < 0) return 0f;
            float total = 0f;
            for (int i = 0; i < Statuses.Count; i++)
                if (Statuses[i].Kind == StatusKind.Vulnerable && Statuses[i].SourceId == sourceId)
                    total += Statuses[i].Magnitude;
            return total;
        }

        public readonly List<StatusInstance> Statuses = new List<StatusInstance>();

        /// <summary>
        /// MaxHp expanded by MaxHpUp, the same MULTIPLY shape AttackUp uses. Identical to MaxHp
        /// when no unit carries MaxHpUp: SumMagnitude returns 0f, and MaxHp * 1f is MaxHp exactly
        /// in IEEE 754 for every finite float.
        /// </summary>
        public float EffectiveMaxHp
        {
            get
            {
                float m = 1f + SumMagnitude(StatusKind.MaxHpUp);
                if (m < 0.2f) m = 0.2f;
                return MaxHp * m;
            }
        }

        public float HpRatio
        {
            get { return EffectiveMaxHp <= 0f ? 0f : Hp / EffectiveMaxHp; }
        }

        // ---------- derived combat values ----------

        public float EffectiveAttack
        {
            get
            {
                float m = 1f + SumMagnitude(StatusKind.AttackUp) - SumMagnitude(StatusKind.AttackDown);
                if (m < 0.2f) m = 0.2f;
                return Attack * m;
            }
        }

        public float EffectiveDefense
        {
            get
            {
                float m = 1f + SumMagnitude(StatusKind.DefenseUp) - SumMagnitude(StatusKind.DefenseDown);
                if (m < 0.2f) m = 0.2f;
                return Defense * m;
            }
        }

        /// <summary>
        /// Speed is the one MULTIPLY-shaped stat among the ones a status can now move, so SpeedUp
        /// scales it rather than adding to it - the AttackUp/AttackDown shape, not the Focus shape.
        ///
        /// SpeedUp joins Slow's existing term instead of bringing a SpeedDown mirror with it. Slow
        /// already IS the speed debuff, and a second speed debuff would be a duplicate mechanism
        /// that every predicate reading StatusKind.Slow would silently miss.
        ///
        /// Identical to the pre-SpeedUp expression when nothing carries SpeedUp: SumMagnitude
        /// returns 0f, and `1f + 0f - x` is exactly `1f - x` in IEEE 754 for every finite x.
        /// </summary>
        public float EffectiveSpeed
        {
            get
            {
                float m = 1f + SumMagnitude(StatusKind.SpeedUp) - SumMagnitude(StatusKind.Slow);
                if (m < 0.3f) m = 0.3f;
                return Speed * m;
            }
        }

        /// <summary>EffectiveSpeed plus this round's jitter - what turn order is actually sorted by.</summary>
        public float TurnOrderSpeed
        {
            get { return EffectiveSpeed * (1f + RoundSpeedJitter); }
        }

        public float EffectiveCritChance
        {
            get
            {
                float c = CritChance + SumMagnitude(StatusKind.Focus);
                if (c < 0f) c = 0f;
                if (c > 0.95f) c = 0.95f;
                return c;
            }
        }

        // ---------- ADD-shaped stats ----------
        //
        // Every getter below is `raw + up - down`, which is the Focus shape (Crit Chance) rather
        // than the AttackUp shape (Attack). These stats are small fractions, or in Crit Damage's
        // case a multiplier, and none of them is scaled by level or star rarity - so multiplying
        // does nothing useful and, on a stat sitting at 0, does nothing at all. RelicStatMode and
        // ArmorStatMode already split the stat table this exact way.
        //
        // ALL OF THEM ARE EXACT NO-OPS UNTIL CONTENT AUTHORS ONE. Both SumMagnitude calls return
        // 0f when no unit carries the status, and `x + 0f - 0f` is exactly `x` in IEEE 754 for
        // every finite x, so no battle can move on the arithmetic alone.

        public float EffectiveEffectHit
        {
            get { return EffectHit + SumMagnitude(StatusKind.EffectHitUp) - SumMagnitude(StatusKind.EffectHitDown); }
        }

        public float EffectiveEffectResist
        {
            get { return EffectResist + SumMagnitude(StatusKind.EffectResistUp) - SumMagnitude(StatusKind.EffectResistDown); }
        }

        /// <summary>
        /// Floored at -0.9 rather than at 0, because ApplyDamageEffect uses this as `raw *= 1f + x`
        /// and an x at or below -1 would zero or invert a hit. The floor is unreachable with
        /// today's content - nothing authors SkillDamageDown and the raw stat is never negative -
        /// so it cannot change an existing battle; it exists because the moment content CAN author
        /// a Skill Damage debuff, a deep enough stack of them would otherwise heal the target.
        /// </summary>
        public float EffectiveSkillDamage
        {
            get
            {
                float v = SkillDamage + SumMagnitude(StatusKind.SkillDamageUp) - SumMagnitude(StatusKind.SkillDamageDown);
                if (v < -0.9f) v = -0.9f;
                return v;
            }
        }

        public float EffectiveBlock
        {
            get { return Block + SumMagnitude(StatusKind.BlockUp) - SumMagnitude(StatusKind.BlockDown); }
        }

        public float EffectiveArmorBreak
        {
            get { return ArmorBreak + SumMagnitude(StatusKind.ArmorBreakUp) - SumMagnitude(StatusKind.ArmorBreakDown); }
        }

        public float EffectiveControlImmunity { get { return ControlImmunity; } }

        public float EffectiveDamageReduction
        {
            get
            {
                return DamageReduction
                     + SumMagnitude(StatusKind.DamageReductionUp)
                     - SumMagnitude(StatusKind.DamageReductionDown);
            }
        }

        /// <summary>
        /// New getter, not a new field. ApplyDamageEffect multiplied by the raw CritDamage - the
        /// only stat in the damage pipeline still read straight off the unit - so a Crit Damage
        /// status needed a getter to exist before it could be read at all.
        ///
        /// Floored at 0 so a deep enough debuff stack cannot turn a critical hit into healing.
        /// Unreachable today: nothing authors CritDamageDown and every shipped Crit Damage sits
        /// near 1.5, so this is `CritDamage + 0f - 0f`, which is exactly CritDamage.
        /// </summary>
        public float EffectiveCritDamage
        {
            get
            {
                float v = CritDamage + SumMagnitude(StatusKind.CritDamageUp) - SumMagnitude(StatusKind.CritDamageDown);
                if (v < 0f) v = 0f;
                return v;
            }
        }

        public float EffectiveHolyDamage { get { return HolyDamage + SumMagnitude(StatusKind.HolyDamageUp); } }

        /// <summary>
        /// How many harmful statuses this unit carries, counted with the same StatusInfo.IsHarmful
        /// predicate Cleanse and Dispel read - so TargetMode.AllyWithMostDebuffs
        /// picks the ally a Cleanse would do the most for, rather than answering a subtly different
        /// question about what counts as a debuff. Stacks count individually, as CountOf does.
        /// </summary>
        public int DebuffCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Statuses.Count; i++) if (StatusInfo.IsHarmful(Statuses[i].Kind)) n++;
                return n;
            }
        }

        public bool HasTaunt
        {
            get { return Alive && Has(StatusKind.Taunt); }
        }

        public float SumMagnitude(StatusKind kind)
        {
            float total = 0f;
            for (int i = 0; i < Statuses.Count; i++) if (Statuses[i].Kind == kind) total += Statuses[i].Magnitude;
            return total;
        }

        public bool Has(StatusKind kind)
        {
            for (int i = 0; i < Statuses.Count; i++) if (Statuses[i].Kind == kind) return true;
            return false;
        }

        public void AddOrRefresh(StatusKind kind, int rounds, float magnitude, int sourceId)
        {
            for (int i = 0; i < Statuses.Count; i++)
            {
                StatusInstance s = Statuses[i];
                if (s.Kind != kind) continue;
                // refresh: keep the stronger magnitude and the longer duration
                if (magnitude > s.Magnitude) s.Magnitude = magnitude;
                if (rounds > s.RoundsLeft) s.RoundsLeft = rounds;
                s.SourceId = sourceId;
                return;
            }
            StatusInstance added = new StatusInstance();
            added.Kind = kind;
            added.RoundsLeft = rounds;
            added.Magnitude = magnitude;
            added.SourceId = sourceId;
            Statuses.Add(added);
        }

        public int CountOf(StatusKind kind)
        {
            int n = 0;
            for (int i = 0; i < Statuses.Count; i++) if (Statuses[i].Kind == kind) n++;
            return n;
        }

        /// <summary>
        /// Add a second entry of the same kind instead of folding it into the first, so two
        /// applications deepen a counter rather than one silently discarding the other. Bounded by
        /// cap; at the cap this degrades to AddOrRefresh, which keeps the stronger magnitude and
        /// longer duration on an existing entry. Returns true if a genuinely new entry was added.
        ///
        /// Written for Ember's Burn stacking, which is gone; GainResolve is what uses it now, and
        /// it is the only caller. Deliberately a separate method rather than a flag on
        /// AddOrRefresh: every other status in the game wants refresh semantics, and this keeps
        /// those call sites provably unchanged.
        /// SumMagnitude already totals across duplicate entries, so nothing downstream needs to know
        /// which of the two paths produced the list.
        /// </summary>
        public bool AddStack(StatusKind kind, int rounds, float magnitude, int sourceId, int cap)
        {
            if (cap < 1) cap = 1;
            if (CountOf(kind) >= cap)
            {
                AddOrRefresh(kind, rounds, magnitude, sourceId);
                return false;
            }
            StatusInstance added = new StatusInstance();
            added.Kind = kind;
            added.RoundsLeft = rounds;
            added.Magnitude = magnitude;
            added.SourceId = sourceId;
            Statuses.Add(added);
            return true;
        }

        /// <summary>
        /// Adds one passive and its own fired flag. The only way a passive reaches a unit, so
        /// Passives and _passiveFired are the same length by construction rather than by a
        /// convention someone has to remember.
        /// </summary>
        public void AddPassive(PassiveDef passive)
        {
            if (passive == null) return;
            Passives.Add(passive);
            _passiveFired.Add(false);
            _passiveLastRound.Add(0);

            if (passive.OutputModStatus != StatusKind.None)
            {
                OutputModStatus = passive.OutputModStatus;
                OutputModPerStack = passive.OutputModPerStack;
                OutputModScope = passive.OutputModScope;
                OutputModMarkedOnly = passive.OutputModMarkedOnly;
            }
        }

        /// <summary>Whether the passive at this index has spent its oncePerBattle allowance.</summary>
        public bool PassiveHasFired(int index)
        {
            return index >= 0 && index < _passiveFired.Count && _passiveFired[index];
        }

        public void MarkPassiveFired(int index)
        {
            if (index >= 0 && index < _passiveFired.Count) _passiveFired[index] = true;
        }

        public int PassiveLastRound(int index)
        {
            return index >= 0 && index < _passiveLastRound.Count ? _passiveLastRound[index] : 0;
        }

        public void MarkPassiveRound(int index, int round)
        {
            if (index >= 0 && index < _passiveLastRound.Count) _passiveLastRound[index] = round;
        }

        /// <summary>
        /// Whether this unit carries a mark of the given kind applied by that specific unit. The
        /// whole of what makes one hero's mark its own: the kind says "a mark", SourceId says whose.
        /// </summary>
        public bool HasMarkFrom(StatusKind kind, int sourceId)
        {
            if (sourceId < 0) return false;
            for (int i = 0; i < Statuses.Count; i++)
                if (Statuses[i].Kind == kind && Statuses[i].SourceId == sourceId) return true;
            return false;
        }

        /// <summary>Everything this unit's Ledger entries from one applier have banked between
        /// them. Summed rather than taking the first, so two ledgers from the same hero read as one
        /// number - which is what "the stored amount" means in every description that uses it.</summary>
        public float LedgerTotalFrom(int sourceId)
        {
            float total = 0f;
            for (int i = 0; i < Statuses.Count; i++)
                if (Statuses[i].Kind == StatusKind.Ledger && Statuses[i].SourceId == sourceId)
                    total += Statuses[i].Stored;
            return total;
        }

        public void Remove(StatusKind kind)
        {
            for (int i = Statuses.Count - 1; i >= 0; i--) if (Statuses[i].Kind == kind) Statuses.RemoveAt(i);
        }

        public UnitSnapshot ToSnapshot()
        {
            UnitSnapshot s = new UnitSnapshot();
            s.Id = Id;
            s.IsPlayer = IsPlayer;
            s.Slot = Slot;
            s.DefId = DefId;
            s.Name = Name;
            s.Level = Level;
            s.MaxHp = MaxHp;
            s.Attack = Attack;
            s.Defense = Defense;
            s.Speed = Speed;
            s.Aspect = Aspect;
            s.Role = Role;
            s.Stars = Stars;
            s.IsBoss = IsBoss;
            s.Visual = Visual;
            s.EnergyMax = EnergyMax;
            s.StartEnergy = Energy;
            s.ActiveAbilityName = Active != null ? Active.Name : "";
            UnitStats stats = new UnitStats();
            stats.MaxHp = MaxHp;
            stats.Attack = Attack;
            stats.Defense = Defense;
            stats.Speed = Speed;
            stats.CritChance = CritChance;
            stats.CritDamage = CritDamage;
            s.Power = stats.Power;
            return s;
        }

        public static CombatUnit FromDef(UnitDef def, UnitStats stats, int level, bool isPlayer, int slot, int id, int energyMax)
        {
            CombatUnit u = new CombatUnit();
            u.Id = id;
            u.IsPlayer = isPlayer;
            u.Slot = slot;
            u.DefId = def.Id;
            u.Name = def.Name;
            u.Level = level;
            u.Aspect = def.Aspect;
            u.Visual = def.Visual;
            u.MaxHp = stats.MaxHp;
            u.Hp = stats.MaxHp;
            u.Attack = stats.Attack;
            u.Defense = stats.Defense;
            u.Speed = stats.Speed;
            u.CritChance = stats.CritChance;
            u.CritDamage = stats.CritDamage;
            u.EffectHit = stats.EffectHit;
            u.EffectResist = stats.EffectResist;
            u.SkillDamage = stats.SkillDamage;
            u.Block = stats.Block;
            u.ArmorBreak = stats.ArmorBreak;
            u.ControlImmunity = stats.ControlImmunity;
            u.DamageReduction = stats.DamageReduction;
            u.HolyDamage = stats.HolyDamage;
            u.EnergyMax = energyMax;
            u.Energy = Math.Min(energyMax, def.StartEnergy);
            u.Basic = def.BasicAttack != null ? def.BasicAttack : UnitDef.DefaultBasicAttack();
            u.Active = def.Active;
            for (int i = 0; i < def.Passives.Count; i++) u.AddPassive(def.Passives[i]);

            HeroDef hero = def as HeroDef;
            if (hero != null)
            {
                u.Role = hero.Role;
                u.Stars = hero.Stars;
            }
            EnemyDef enemy = def as EnemyDef;
            if (enemy != null)
            {
                u.IsBoss = enemy.Kind == "boss";
                u.Role = enemy.Kind == "boss" ? HeroRole.Bulwark : HeroRole.Striker;
            }
            return u;
        }
    }
}
