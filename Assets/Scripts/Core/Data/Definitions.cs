using System;
using System.Collections.Generic;
using System.Text;
using Emberfall.Core.Json;

namespace Emberfall.Core.Data
{
    /// <summary>Colour / silhouette recipe used by the procedural character builder.</summary>
    public sealed class VisualDef
    {
        public string Primary = "8A6BFF";
        public string Secondary = "3A2E63";
        public string Accent = "FFD36B";
        public string Silhouette = "humanoid"; // humanoid | brute | beast | wisp | golem
        public string Weapon = "sword";        // sword | greatsword | spear | staff | dagger | orb | hammer | claws | none
        public float Scale = 1f;
        public int Sigil = 0;                  // index of the emblem drawn on the portrait

        /// <summary>
        /// Optional sprite id for the battle screen rig. Empty (the default) means the unit
        /// stays fully procedural - no behaviour change from today. A non-empty value loads
        /// Resources/Sprites/Units/{Sprite}.png and the battle screen renders that image on a
        /// billboard quad instead of building the primitive body. Menu portraits (hero cards,
        /// hero detail, the summon reveal) are a separate rendering path and are unaffected
        /// either way - this only concerns the 3D battle rig.
        /// </summary>
        public string Sprite = "";

        /// <summary>
        /// Sprite only. Fraction of the canvas, measured from the bottom (the feet), that is the
        /// character's own body - head to feet, not counting anything raised above the head.
        /// Defaults to 1: the whole canvas is body, today's behaviour, unchanged for every hero
        /// that does not set this key. A raised weapon or prop needs the remaining top fraction
        /// reserved for it; see UnitRig.BuildSprite for how that fraction turns into world space,
        /// and DESIGN-ART-DELIVERY.md for how an artist measures it from their own file.
        /// </summary>
        public float SpriteBodyFraction = 1f;

        /// <summary>
        /// Sprite only. Fraction of the canvas, measured from the bottom, that is EMPTY margin
        /// below the character's own feet - the counterpart SpriteBodyFraction does not cover.
        /// Defaults to 0: feet touch the bottom edge of the canvas exactly, today's behaviour,
        /// unchanged for every hero that does not set this key (every hero but Kael).
        ///
        /// Distinct from SpriteBodyFraction's reserved headroom above the body: that space is
        /// legitimately used by some frames of a clip (a raised weapon mid-swing), so it stays
        /// part of the canvas the quad renders. A margin below the feet has no equivalent use -
        /// nothing in a battle animation extends below where a character stands - so instead of
        /// being sized into the canvas it is measured out of it: the quad shifts upward by this
        /// fraction of canvas height so the character's actual feet, not the transparent edge
        /// below them, land on the ground. See UnitRig.BuildSprite for the shift, and
        /// DESIGN-ART-DELIVERY.md for how an artist (or an automated sprite-sheet export) measures
        /// it off their own file the same way SpriteBodyFraction already is.
        /// </summary>
        public float SpriteFootFraction = 0f;

        /// <summary>
        /// Sprite only. True mirrors the quad horizontally. The battle rig turns every ally
        /// toward +X and every enemy toward -X by the same fixed angle in opposite directions
        /// (UnitRig.Build's ThreeQuarterTurn), which is what DESIGN-ART-DELIVERY.md's sprites are
        /// drawn against - a hero authored facing that convention needs nothing else. This exists
        /// for art that was authored facing the opposite way: rather than re-exporting the source
        /// frames, the quad flips instead. Defaults to false - unchanged for every hero whose art
        /// already matches the convention.
        /// </summary>
        public bool SpriteMirror;

        public static VisualDef FromJson(JsonValue j)
        {
            VisualDef v = new VisualDef();
            if (j == null || j.IsNull) return v;
            v.Primary = j.GetString("primary", v.Primary);
            v.Secondary = j.GetString("secondary", v.Secondary);
            v.Accent = j.GetString("accent", v.Accent);
            v.Silhouette = j.GetString("silhouette", v.Silhouette);
            v.Weapon = j.GetString("weapon", v.Weapon);
            v.Scale = j.GetFloat("scale", v.Scale);
            v.Sigil = j.GetInt("sigil", v.Sigil);
            v.Sprite = j.GetString("sprite", v.Sprite);
            v.SpriteBodyFraction = j.GetFloat("spriteBodyFraction", v.SpriteBodyFraction);
            v.SpriteFootFraction = j.GetFloat("spriteFootFraction", v.SpriteFootFraction);
            v.SpriteMirror = j.GetBool("spriteMirror", v.SpriteMirror);
            return v;
        }
    }

    /// <summary>One atomic thing an ability does. Abilities are just ordered lists of these.</summary>
    public sealed class EffectDef
    {
        public EffectKind Kind = EffectKind.Damage;
        public TargetMode Target = TargetMode.FrontEnemy;
        public float Power = 1f;      // multiplier applied to the caster's attack
        public float Flat;            // flat bonus added before mitigation
        public int Hits = 1;          // repeated applications
        public StatusKind Status = StatusKind.None;
        public int Duration = 2;      // rounds, for ApplyStatus
        public float Magnitude = 0.2f;// status strength (percentage or dot power)
        public float Chance = 1f;     // application chance, 0..1
        public float SelfHpRatio;     // for Shield/Heal scaling off the caster's max HP instead of attack

        // ---------- Resolve: one generic stacking counter, not four bespoke mechanics ----------
        // DESIGN-RELICS and DESIGN-ASPECTS both settled the same argument the same way: a shared
        // hook that content configures beats one-off code per owner, and whoever owns the hook owns
        // it for everybody. These three fields are that hook for "a counter that builds on a
        // trigger and is spent by a later effect", and they are the whole of it - no hero, relic or
        // aspect gets simulation code of its own out of this.
        //
        // MaxStacks caps a GainResolve. PerStack scales an effect's Power by (1 + PerStack * n)
        // where n is the CASTER's current Resolve count. ConsumesResolve clears the caster's
        // counter once that effect has finished resolving against every one of its targets, so an
        // AoE spends the stack once rather than once per enemy.
        public int MaxStacks;         // cap for GainResolve; 0 means 1
        public float PerStack;        // Power multiplier per stack of the caster's Resolve
        public bool ConsumesResolve;  // clear the caster's Resolve after this effect resolves

        // ---------- Conditions: one test on the effect, evaluated where it lands ----------
        // The same argument Resolve settled above, settled the same way. 111 of the 374 incoming
        // abilities carry a conditional clause, and the alternative to a field content configures
        // is a bespoke branch per hero inside the simulation.
        //
        // Evaluated in ResolveAbility AFTER targeting and BEFORE ApplyEffect, against the unit the
        // effect is landing on plus the caster. Per target, so a conditional AoE tests each enemy
        // on its own rather than testing one and applying to all. An effect whose condition fails
        // is skipped whole: no hit loop, no landing roll, no Block or crit or variance draw, and no
        // ConsumesResolve - see CombatSimulation.ConditionMet and its call site.
        //
        // Condition None is the default and is every effect in shipped content, which is what makes
        // this pass unable to move an existing battle: the gate is one comparison against None, and
        // nothing reaches the evaluator until content asks it to.
        public EffectCondition Condition = EffectCondition.None;
        public ConditionSubject ConditionOn = ConditionSubject.Target;
        public StatusKind ConditionStatus = StatusKind.None;  // for HasStatus/LacksStatus/StatusStacksAtLeast
        public float ConditionValue;  // HP ratio for HpBelow/HpAbove; stack count for StatusStacksAtLeast

        /// <summary>
        /// The status TargetMode.EnemyWithStatus looks for, and read by nothing else.
        ///
        /// Separate from ConditionStatus on purpose, even though both name a StatusKind: a
        /// condition decides whether an effect resolves against a target already chosen, while this
        /// decides WHICH target is chosen. An effect can legitimately want both - "hit a Forsaken
        /// enemy, but only if it is below 35% HP" is this field plus a condition, and folding them
        /// into one would make that ability unauthorable.
        /// </summary>
        public StatusKind TargetStatus = StatusKind.None;

        /// <summary>
        /// Where MoveDebuffs and MoveBuffs send what they take, resolved against the CASTER exactly
        /// as Target is. Read by nothing else.
        ///
        /// A second TargetMode rather than a second status field, because the descriptions choose
        /// the destination the same way they choose any target - "moves its debuffs to the
        /// highest-HP enemy", "transfers its buffs to the lowest-HP enemy". Defaults to Self, which
        /// is the one destination that always exists.
        /// </summary>
        public TargetMode SecondaryTarget = TargetMode.Self;

        /// <summary>
        /// Which counter PerStack reads, and on whom.
        ///
        /// PerStack shipped able to scale Power by the CASTER's Resolve and nothing else. These two
        /// widen it to any stacking status on either unit, which is what "each Record increases his
        /// damage against marked enemies by 4%" and "each Absence increases her damage by 5%" need.
        /// Both default to what PerStack already did, so an effect that sets neither is unchanged.
        ///
        /// PerStackOn Caster keeps the snapshot-once-per-effect behaviour PerStack has always had -
        /// every enemy in an AoE is scaled by the same count no matter what dies partway through.
        /// PerStackOn Target cannot: the count is a property of each target, so it is read per
        /// target. That is a real difference in meaning, not an implementation detail, and it is
        /// why Caster stays the default.
        /// </summary>
        public StatusKind PerStackStatus = StatusKind.Resolve;
        public ConditionSubject PerStackOn = ConditionSubject.Caster;

        /// <summary>
        /// The ceiling on what one Ledger entry may bank, as a multiple of the caster's Attack.
        /// Read when an ApplyStatus creates a Ledger and stored on the entry itself, because the
        /// cap belongs to the mark that is counting rather than to whatever later pays it out.
        /// Zero means uncapped.
        /// </summary>
        public float StoredCap;

        /// <summary>
        /// Uses stamped onto a charge-based status as it lands. Zero leaves the status on its
        /// duration alone, which is every status that existed before this pass.
        /// </summary>
        public int Charges;

        /// <summary>
        /// Scaling off the TARGET's health rather than the caster's. `SelfHpRatio` already covers
        /// the caster's own max HP; these three cover the other side of the hit, which is what
        /// "12% of its missing HP" and "heals them for 8% Max HP" need.
        ///
        /// All three are added on top of the Attack-scaled amount rather than replacing it, so an
        /// effect can be part flat, part Attack-scaled and part HP-scaled at once - which is how
        /// the descriptions that use them are written.
        /// </summary>
        public float TargetMaxHpRatio;
        public float TargetCurrentHpRatio;
        public float TargetMissingHpRatio;

        /// <summary>
        /// Which non-numeric fact `EffectKind.RecordFact` writes. `Status` names the entry on the
        /// target that carries it, so a kit can keep two records side by side on two marks.
        /// </summary>
        public RecordKind Record = RecordKind.None;

        /// <summary>
        /// Uses per round stamped onto a status as it lands, 0 for unlimited. The "first X each
        /// round" rider: the status stays, its counter resets every round, and
        /// `EffectCondition.StatusUseAvailable` is what reads it.
        /// </summary>
        public int UsesPerRound;

        /// <summary>
        /// A TurnShift's realised magnitude is banked onto the target as this status when it is not
        /// None, which is how "delays by 12% turn progress, then applies Turn Debt equal to the
        /// progress removed" is one effect rather than two that have to agree about a number
        /// neither of them computed. `StoredCap` caps the debt; `Magnitude` is the exchange rate
        /// the status is repaid at.
        /// </summary>
        public StatusKind ShiftBanksAs = StatusKind.None;

        /// <summary>
        /// When true, a single-enemy target pick expands to include the living enemies in the
        /// primary target's adjacent slots (Slot ± 1). The adjacency concept: units carry a Slot
        /// (0-4), and neighbours are one slot apart. The primary target is always included; the
        /// splash adds up to two more. Works with any single-enemy TargetMode.
        /// </summary>
        public bool Splash;

        /// <summary>
        /// Which stat `CasterStatAboveTarget` / `CasterStatBelowTarget` compares. Defaults to
        /// Speed because it is the stat the first ability to use this was built for.
        /// </summary>
        public ComparedStat ComparedStat = ComparedStat.Speed;

        // ---------- tier eight: AND conditions ----------
        //
        // Two conditions gating one effect. Both must pass. Condition is the first gate, Condition2
        // is the second. When Condition2 is None (the default), the effect is single-conditioned
        // exactly as before. When Condition2 is not None, both must hold for the effect to resolve.

        public EffectCondition Condition2 = EffectCondition.None;
        public ConditionSubject ConditionOn2 = ConditionSubject.Target;
        public StatusKind ConditionStatus2 = StatusKind.None;
        public float ConditionValue2;
        public ComparedStat ComparedStat2 = ComparedStat.Speed;

        // ---------- tier eight: ledger extensions ----------

        /// <summary>When true, LedgerRelease heals the target instead of damaging it.</summary>
        public bool ReleaseAsHeal;

        /// <summary>When true, a Ledger status auto-releases its Stored as damage to the holder
        /// when its duration expires, at Power multiplier. Stored on the StatusInstance.</summary>
        public bool LedgerAutoRelease;

        /// <summary>When true, the Ledger entry mitigates incoming damage by spending Stored
        /// instead of banking into it. Stored on the StatusInstance.</summary>
        public bool LedgerMitigates;

        /// <summary>Max number of times the Ledger entry may bank per round, 0 for unlimited.
        /// Stored on the StatusInstance.</summary>
        public int LedgerBankLimit;

        // ---------- tier eight: per-AoE-target ----------

        /// <summary>When true, this effect's Hits are multiplied by the number of targets the
        /// previous effect in the same ability resolved against. Used for "gain 1 Resolve per
        /// enemy hit".</summary>
        public bool PerTarget;

        // ---------- tier nine: parameterisation gaps ----------

        /// <summary>When true, Dispel only removes beneficial statuses whose SourceId matches the
        /// caster's Id. False (default) removes any beneficial status, the existing behaviour.</summary>
        public bool DispelOwnOnly;

        /// <summary>When true, ShieldTransfer and HpTransfer read the TARGET's pool instead of the
        /// caster's. False (default) keeps the existing caster-as-donor behaviour.</summary>
        public bool TransferFromTarget;

        /// <summary>When true, a Ledger entry banks OVERHEAL (the part of a heal above max HP)
        /// instead of incoming damage. The damage-side banking loop skips entries with this flag,
        /// and a mirrored loop in ApplyHealValue handles them. Stored on the StatusInstance.</summary>
        public bool LedgerBankHeals;

        /// <summary>"attack" (default) or "maxHp". Picks the basis for StoredCap scaling at Ledger
        /// apply time: source.EffectiveAttack * StoredCap vs source.MaxHp * StoredCap.</summary>
        public string StoredCapBasis = "attack";

        /// <summary>When true, a Ledger's auto-release on expiry heals the holder (or the
        /// AutoReleaseTarget) instead of damaging it. Stored on the StatusInstance.</summary>
        public bool LedgerAutoReleaseAsHeal;

        /// <summary>Who receives the auto-release payout on expiry. Self (default) means the holder.
        /// Any other TargetMode resolves against the applier at expiry time. Stored on the
        /// StatusInstance as a TargetMode ordinal.</summary>
        public TargetMode LedgerAutoReleaseTarget = TargetMode.Self;

        /// <summary>Only bank into this Ledger when the incoming damage's dotKind matches this
        /// status. None (default) means bank everything, the existing behaviour. Stored on the
        /// StatusInstance.</summary>
        public StatusKind LedgerBankOnlyFrom = StatusKind.None;

        /// <summary>Fraction of the Ledger's Stored that LedgerRelease pays out. 1.0 (default)
        /// pays the full amount, 0.5 pays half. Clamped to [0, 1].</summary>
        public float ReleaseFraction = 1f;

        // ---------- batch two ----------

        /// <summary>Ceiling on the TargetMissingHpRatio damage term, as a fraction of the target's
        /// max HP. Zero (default) means uncapped.</summary>
        public float MissingHpCap;

        /// <summary>OR-condition: (C1 AND C2) OR C3. When Condition3 is None (the default), no OR
        /// branch exists and the effect is unchanged.</summary>
        public EffectCondition Condition3 = EffectCondition.None;
        public ConditionSubject ConditionOn3 = ConditionSubject.Target;
        public StatusKind ConditionStatus3 = StatusKind.None;
        public float ConditionValue3;
        public ComparedStat ComparedStat3 = ComparedStat.Speed;

        /// <summary>When true, the Attack term in the damage/heal formula is replaced by the amount
        /// of the last LedgerRelease or auto-release.</summary>
        public bool ScaleByLastRelease;

        /// <summary>When true, the Attack term is replaced by the amount of the last
        /// RedirectLethal.</summary>
        public bool ScaleByLastRedirect;

        /// <summary>
        /// When true, the Attack term is replaced by the DAMAGE the last Cleanse, Dispel or
        /// RemoveStatus took off a unit - the remaining damage of the removed damage-over-time
        /// entries, summed.
        ///
        /// It carried a COUNT until the nine-gap pass, which was a defect rather than a choice:
        /// every description in the roster that reaches this flag says amount, and a Heal authored
        /// at power 0.8 against one removed Burn healed for 1 HP. The flag's name and JSON spelling
        /// are unchanged, because "scale by what the last removal took" was always what it meant
        /// and only the unit was wrong. Zero when what was removed carries no damage.
        /// </summary>
        public bool ScaleByLastRemoval;

        /// <summary>When true, the Attack term is replaced by the total Stored on the Ledger
        /// that just banked, as carried by the OnAllyLedgerBanked broadcast.</summary>
        public bool ScaleByLastBanked;

        /// <summary>When true, a Ledger entry only banks damage whose SourceId matches the
        /// entry's own SourceId. Stored on the StatusInstance.</summary>
        public bool LedgerBankFromCaster;

        /// <summary>When true, the ApplyStatus call site uses the authored Chance directly,
        /// bypassing the Effect Resist formula entirely. Kit-defining debuffs that are not
        /// meant to be contested use this instead of out-scaling Effect Resist with a high
        /// authored chance.</summary>
        public bool BypassResist;

        // ---------- batch three ----------

        /// <summary>Ceiling on the TargetMaxHpRatio damage term, as a fraction of the target's
        /// max HP. Zero (default) means uncapped. Mirrors MissingHpCap for the max-HP term.</summary>
        public float MaxHpCap;

        /// <summary>When ConsumesResolve is true and this is > 0, removes exactly this many stacks
        /// instead of all. Zero (default) means remove all, the existing behaviour.</summary>
        public int ConsumeStacks;

        // ---------- batch four ----------

        /// <summary>When true, ApplyStatus calls AddStack instead of AddOrRefresh, so reapplication
        /// by the same caster deepens a counter rather than refreshing duration. MaxStacks caps it.
        /// False (default) keeps existing AddOrRefresh behaviour for every shipped status.</summary>
        public bool StackOnReapply;

        /// <summary>Fraction of the Ledger's Stored that auto-release pays as healing to the
        /// APPLIER, on top of the damage it deals to the holder. Zero (default) means no split
        /// heal fires, which is the existing behaviour.</summary>
        public float LedgerAutoReleaseSplitHeal;

        /// <summary>Which non-numeric fact a Burn tick records on the carrier each time it ticks,
        /// combining DoT damage with RecordFact's recording. None (default) means no recording
        /// happens in ticks, which is the existing behaviour.</summary>
        public RecordKind DoTRecord = RecordKind.None;

        // ---------- funnel-banking primitives ----------

        /// <summary>Which funnel point this Ledger banks from. Damage (default) is the existing
        /// behaviour. Stored on the StatusInstance.</summary>
        public FunnelSource LedgerFunnelSource = FunnelSource.Damage;

        /// <summary>When true and LedgerFunnelSource is HealIntercept, the intercepted heal is
        /// dealt as damage to the holder immediately instead of banked. Stored on the
        /// StatusInstance.</summary>
        public bool LedgerFunnelConvert;

        // ---------- batch five: generalized counters ----------

        /// <summary>Which counter index GainResolve grants / ConsumesResolve spends. Zero (default)
        /// means "derive from Status": None→Resolve (index 0), Resolve2→index 1. Values ≥ 2 address
        /// generalized counters stored on CombatUnit.Counters, independent of the StatusKind enum.
        /// StatusStacksAtLeast and OnStackThreshold read the same index.</summary>
        public int CounterIndex;

        /// <summary>Counter index for Condition (StatusStacksAtLeast). Zero means derive from
        /// ConditionStatus as above. ≥ 2 reads a generalized counter.</summary>
        public int ConditionCounterIndex;

        /// <summary>Counter index for Condition2 (StatusStacksAtLeast). Same semantics.</summary>
        public int ConditionCounterIndex2;

        /// <summary>Counter index for Condition3 (StatusStacksAtLeast). Same semantics.</summary>
        public int ConditionCounterIndex3;

        // ---------- linked targeting ----------

        /// <summary>
        /// What the StatusKind.Linked entry EffectKind.LinkUnits creates does to damage arriving at
        /// its holder. None (the default) makes the link a pure record that only the two link
        /// triggers read. Stored on the StatusInstance.
        ///
        /// Read by LinkUnits and by nothing else, which is why it is its own field rather than a
        /// reuse of Status or Record: those name WHAT the entry is, this names what the engine does
        /// with it at the funnel, and one effect can legitimately want the entry without the
        /// transfer.
        /// </summary>
        public LinkTransfer LinkTransfer = LinkTransfer.None;

        /// <summary>
        /// When true, LinkUnits writes the reverse entry as well, so both bodies carry a link
        /// naming the other. False (the default) writes one entry, on the Target, naming the
        /// SecondaryTarget.
        ///
        /// Two entries rather than one bidirectional entry, so that a Dispel, a Cleanse or a death
        /// can break one half and leave the other standing - which is what a description means when
        /// it says the mark sits on both.
        /// </summary>
        public bool LinkMutual;

        // ---------- the nine-gap pass ----------

        /// <summary>
        /// Attack multiplier for the FLAT amount an Echo link pays on top of its Magnitude share.
        /// Resolved against the applier's Attack once, at creation, and stored on the
        /// StatusInstance as LinkAmount. Zero (the default) is a pure share echo, which is what
        /// every link before this pass was.
        /// </summary>
        public float LinkAmountPower;

        /// <summary>When true, the created link only fires for damage dealt by the unit that
        /// created it. Stored on the StatusInstance as LinkFromCasterOnly.</summary>
        public bool LinkFromCaster;

        /// <summary>
        /// When true, the Attack term is replaced by the last amount the CASTER moved - a
        /// redirected or echoed share it caused, a Shield it transferred, HP it transferred.
        ///
        /// GAP 7, and KEYED PER CASTER rather than held in one global, which is the whole
        /// difference between this and three of the four scalars beside it.
        /// `_lastRedirectAmount`, `_lastRemovedStacks` and `_lastLedgerBankedStored` are single
        /// fields shared by every unit on the field, so a second hero moving something between
        /// the write and the read silently answers for the first. `_lastLedgerRelease` is already
        /// a Dictionary keyed by unit id for exactly that reason, and this follows it rather than
        /// the majority.
        /// </summary>
        public bool ScaleByLastMoved;

        /// <summary>
        /// When true, the Attack term is replaced by the damage the most recent hit actually
        /// dealt - `Sim._lastDamageDealt`, the same realised number EchoDamage repeats.
        ///
        /// A BROADCAST-WINDOW READ, and deliberately not per caster. It is meaningful only inside
        /// a dispatch raised from the funnel, where it names the one hit that raised it - the same
        /// discipline `_broadcastSubject` keeps, and the same reason neither needs a key. Outside
        /// such a window it holds whatever the last hit of the current ability dealt, and zero at
        /// an ability boundary.
        /// </summary>
        public bool ScaleByLastDamage;

        /// <summary>
        /// Rounds to put on a status MoveDebuffs or MoveBuffs relocates. Zero (the default) keeps
        /// the entry's own remaining clock, which is what every move did before this and what keeps
        /// a move from creating value.
        ///
        /// Six abilities across the roster move an UNKNOWN set of statuses and name a duration in
        /// the same breath - "moves its debuffs to the highest-HP enemy for 2 rounds", "transfers
        /// its buffs to the lowest-HP enemy for 1 round". The remove-and-reapply idiom cannot serve
        /// them: an ApplyStatus has to name the kind it applies, and these do not know it.
        /// </summary>
        public int MoveDuration;

        /// <summary>
        /// Rounds to put on the status TransferStatus relocates. Zero (the default) keeps the
        /// entry's own remaining clock, which is what every transfer authored before this did.
        ///
        /// The single-kind sibling of MoveDuration, and it was missed when that one was built.
        /// Zella's shipped `Nothing Stays Settled` says "transfer its Residue and recorded action
        /// history to the highest-HP enemy FOR 2 ROUNDS" and her content has never carried the
        /// duration, because there was no field to put it in.
        /// </summary>
        public int TransferDuration;

        /// <summary>
        /// Multiplies the magnitude of the status TransferStatus relocates. Zero (the default)
        /// leaves it alone, which is what every transfer authored before this did.
        ///
        /// The one thing a move was not allowed to do, and one clause asks for it in as many
        /// words: "transferred Bleed deals 20% more damage". A move creating value is exactly what
        /// MoveStatus's own comment forbids - so this is authored, explicit, and on the single-kind
        /// transfer only, where the author knows what they are re-sizing.
        /// </summary>
        public float TransferMagnitude;

        /// <summary>
        /// Stamps the relocated or applied entry so ExtendStatus will not lengthen it.
        ///
        /// A fact about the ENTRY rather than about whoever tries to extend it, so it travels with
        /// the status: "Transferred Residue cannot be extended" is a property the transferred copy
        /// carries for the rest of its life, not a rule about one caster.
        ///
        /// Refused silently at the extend site - the entry is skipped and not counted - so the
        /// event that reports an extension reports what actually moved.
        /// </summary>
        public bool PreventExtend;

        /// <summary>
        /// Ceiling on this effect's resolved amount, as a multiple of the CASTER's Attack. Zero
        /// (the default) is uncapped.
        ///
        /// MaxHpCap and MissingHpCap both ceiling a term against the TARGET's health, which is the
        /// wrong unit for "up to 130% of Attack" - a cap a description writes in the caster's own
        /// currency. Applied to the whole resolved amount rather than to one term, because that is
        /// what "up to" means in the sentences that ask for it.
        /// </summary>
        public float AttackCap;

        /// <summary>
        /// Scales this effect off the turn progress the caster's last shift actually realised, as
        /// a fraction, times Attack.
        ///
        /// The odd one of the ScaleBy family: the others carry an absolute amount and this carries
        /// a fraction, so it is multiplied by Attack to become one. That is what the clause asking
        /// for it says - "35% of the recorded amount as a percentage of Attack".
        /// </summary>
        public bool ScaleByLastShift;

        /// <summary>
        /// The restriction a Restricted entry carries, and the status TargetBan.CarryingStatus
        /// names. Read at apply time and stamped onto the entry, the way Disrupted's Magnitude and
        /// StoredCap are.
        /// </summary>
        /// <summary>
        /// A FRACTION OF THE TARGET'S DEFENSE THIS HIT DOES NOT SEE. Applied to the Defense value
        /// before the mitigation ratio is computed, which is what "ignores 10% of its Defense"
        /// says - Armor Break, one step below, scales the ratio's OUTPUT instead and is a
        /// different sentence.
        ///
        /// A MODIFIER ON AN EXISTING HIT, NOT A HIT OF ITS OWN, and that is the whole reason it is
        /// a field. The clause that wanted it says the ATTACK ignores Defense; a talent can only
        /// append an effect or override a number, and an appended effect is a second blow rather
        /// than a change to the first. A number on the blow itself is the only shape that says it.
        ///
        /// It carries its own gate and its own budget because of the same limitation: an override
        /// can change a number and cannot add a condition, so a modifier that must only apply to
        /// marked targets and only once a round has to bring both with it.
        ///   VsStatus     - only when the target carries the CASTER's entry of this kind. None
        ///                  means no gate.
        ///   UsesPerRound - how many hits a round may use it, 0 for unlimited. Counted on the
        ///                  attacker, so two blows in one turn spend one budget.
        /// Zero on every effect in shipped content, so the damage path is unchanged by it.
        /// </summary>
        public float IgnoreDefense;
        public StatusKind IgnoreDefenseVsStatus = StatusKind.None;
        public int IgnoreDefenseUsesPerRound;

        /// <summary>
        /// Damage terms read off the band a RecordKind.HpFractionBand entry left behind when it
        /// expired - see BandTerm, which explains why they are read from a capture and not from
        /// the entry.
        ///
        ///   ScaleByBandGap  - how far the carrier stood above the lowest value it recorded,
        ///                     floored at zero. "Damage equal to the difference."
        ///   ScaleByBandHigh - the highest recorded value. Multiply it with `magnitude` for a
        ///                     fraction of it.
        ///
        /// Both add into the damage beside TargetHpTerm rather than replacing the Attack term, so
        /// an effect can still carry a power if it wants both. Both are false everywhere in
        /// shipped content, and a false one contributes an exact zero.
        /// </summary>
        public bool ScaleByBandGap;
        public bool ScaleByBandHigh;

        public TargetBan TargetBanKind = TargetBan.None;
        public StatusKind TargetBanStatus = StatusKind.None;

        public static EffectDef FromJson(JsonValue j)
        {
            EffectDef e = new EffectDef();
            e.Kind = EnumUtil.Parse(j.GetString("kind", "Damage"), EffectKind.Damage);
            e.Target = EnumUtil.Parse(j.GetString("target", "FrontEnemy"), TargetMode.FrontEnemy);
            e.Power = j.GetFloat("power", e.Kind == EffectKind.Damage ? 1f : 0f);
            e.Flat = j.GetFloat("flat", 0f);
            e.Hits = Math.Max(1, j.GetInt("hits", 1));
            e.Status = EnumUtil.Parse(j.GetString("status", "None"), StatusKind.None);
            e.Duration = j.GetInt("duration", 2);
            e.Magnitude = j.GetFloat("magnitude", 0.2f);
            e.Chance = j.GetFloat("chance", 1f);
            e.SelfHpRatio = j.GetFloat("selfHpRatio", 0f);
            e.MaxStacks = Math.Max(0, j.GetInt("maxStacks", 0));
            e.PerStack = j.GetFloat("perStack", 0f);
            e.ConsumesResolve = j.GetBool("consumesResolve", false);
            e.Condition = EnumUtil.Parse(j.GetString("condition", "None"), EffectCondition.None);
            e.ConditionOn = EnumUtil.Parse(j.GetString("conditionOn", "Target"), ConditionSubject.Target);
            e.ConditionStatus = EnumUtil.Parse(j.GetString("conditionStatus", "None"), StatusKind.None);
            e.ConditionValue = j.GetFloat("conditionValue", 0f);
            e.TargetStatus = EnumUtil.Parse(j.GetString("targetStatus", "None"), StatusKind.None);
            e.SecondaryTarget = EnumUtil.Parse(j.GetString("secondaryTarget", "Self"), TargetMode.Self);
            e.PerStackStatus = EnumUtil.Parse(j.GetString("perStackStatus", "Resolve"), StatusKind.Resolve);
            e.PerStackOn = EnumUtil.Parse(j.GetString("perStackOn", "Caster"), ConditionSubject.Caster);
            e.StoredCap = j.GetFloat("storedCap", 0f);
            e.Charges = Math.Max(0, j.GetInt("charges", 0));
            e.TargetMaxHpRatio = j.GetFloat("targetMaxHpRatio", 0f);
            e.TargetCurrentHpRatio = j.GetFloat("targetCurrentHpRatio", 0f);
            e.TargetMissingHpRatio = j.GetFloat("targetMissingHpRatio", 0f);
            e.Record = EnumUtil.Parse(j.GetString("record", "None"), RecordKind.None);
            e.UsesPerRound = Math.Max(0, j.GetInt("usesPerRound", 0));
            e.ShiftBanksAs = EnumUtil.Parse(j.GetString("shiftBanksAs", "None"), StatusKind.None);
            e.Splash = j.GetBool("splash", false);
            e.ComparedStat = EnumUtil.Parse(j.GetString("comparedStat", "Speed"), ComparedStat.Speed);
            e.Condition2 = EnumUtil.Parse(j.GetString("condition2", "None"), EffectCondition.None);
            e.ConditionOn2 = EnumUtil.Parse(j.GetString("conditionOn2", "Target"), ConditionSubject.Target);
            e.ConditionStatus2 = EnumUtil.Parse(j.GetString("conditionStatus2", "None"), StatusKind.None);
            e.ConditionValue2 = j.GetFloat("conditionValue2", 0f);
            e.ComparedStat2 = EnumUtil.Parse(j.GetString("comparedStat2", "Speed"), ComparedStat.Speed);
            e.ReleaseAsHeal = j.GetBool("releaseAsHeal", false);
            e.LedgerAutoRelease = j.GetBool("ledgerAutoRelease", false);
            e.LedgerMitigates = j.GetBool("ledgerMitigates", false);
            e.LedgerBankLimit = Math.Max(0, j.GetInt("ledgerBankLimit", 0));
            e.PerTarget = j.GetBool("perTarget", false);
            e.DispelOwnOnly = j.GetBool("dispelOwnOnly", false);
            e.TransferFromTarget = j.GetBool("transferFromTarget", false);
            e.LedgerBankHeals = j.GetBool("ledgerBankHeals", false);
            e.StoredCapBasis = j.GetString("storedCapBasis", "attack");
            e.LedgerAutoReleaseAsHeal = j.GetBool("ledgerAutoReleaseAsHeal", false);
            e.LedgerAutoReleaseTarget = EnumUtil.Parse(j.GetString("ledgerAutoReleaseTarget", "Self"), TargetMode.Self);
            e.LedgerBankOnlyFrom = EnumUtil.Parse(j.GetString("ledgerBankOnlyFrom", "None"), StatusKind.None);
            e.ReleaseFraction = j.GetFloat("releaseFraction", 1f);
            e.MissingHpCap = j.GetFloat("missingHpCap", 0f);
            e.Condition3 = EnumUtil.Parse(j.GetString("condition3", "None"), EffectCondition.None);
            e.ConditionOn3 = EnumUtil.Parse(j.GetString("conditionOn3", "Target"), ConditionSubject.Target);
            e.ConditionStatus3 = EnumUtil.Parse(j.GetString("conditionStatus3", "None"), StatusKind.None);
            e.ConditionValue3 = j.GetFloat("conditionValue3", 0f);
            e.ComparedStat3 = EnumUtil.Parse(j.GetString("comparedStat3", "Speed"), ComparedStat.Speed);
            e.ScaleByLastRelease = j.GetBool("scaleByLastRelease", false);
            e.ScaleByLastRedirect = j.GetBool("scaleByLastRedirect", false);
            e.ScaleByLastRemoval = j.GetBool("scaleByLastRemoval", false);
            e.ScaleByLastBanked = j.GetBool("scaleByLastBanked", false);
            e.LedgerBankFromCaster = j.GetBool("ledgerBankFromCaster", false);
            e.BypassResist = j.GetBool("bypassResist", false);
            e.MaxHpCap = j.GetFloat("maxHpCap", 0f);
            e.ConsumeStacks = Math.Max(0, j.GetInt("consumeStacks", 0));
            e.StackOnReapply = j.GetBool("stackOnReapply", false);
            e.LedgerAutoReleaseSplitHeal = j.GetFloat("ledgerAutoReleaseSplitHeal", 0f);
            e.DoTRecord = EnumUtil.Parse(j.GetString("doTRecord", "None"), RecordKind.None);
            e.LedgerFunnelSource = EnumUtil.Parse(j.GetString("ledgerFunnelSource", "Damage"), FunnelSource.Damage);
            e.LedgerFunnelConvert = j.GetBool("ledgerFunnelConvert", false);
            e.CounterIndex = Math.Max(0, j.GetInt("counterIndex", 0));
            e.ConditionCounterIndex = Math.Max(0, j.GetInt("conditionCounterIndex", 0));
            e.ConditionCounterIndex2 = Math.Max(0, j.GetInt("conditionCounterIndex2", 0));
            e.ConditionCounterIndex3 = Math.Max(0, j.GetInt("conditionCounterIndex3", 0));
            e.LinkTransfer = EnumUtil.Parse(j.GetString("linkTransfer", "None"), LinkTransfer.None);
            e.LinkMutual = j.GetBool("linkMutual", false);
            e.LinkAmountPower = j.GetFloat("linkAmountPower", 0f);
            e.LinkFromCaster = j.GetBool("linkFromCaster", false);
            e.ScaleByLastMoved = j.GetBool("scaleByLastMoved", false);
            e.ScaleByLastDamage = j.GetBool("scaleByLastDamage", false);
            e.MoveDuration = Math.Max(0, j.GetInt("moveDuration", 0));
            e.TransferDuration = Math.Max(0, j.GetInt("transferDuration", 0));
            e.TransferMagnitude = j.GetFloat("transferMagnitude", 0f);
            e.AttackCap = j.GetFloat("attackCap", 0f);
            e.ScaleByLastShift = j.GetBool("scaleByLastShift", false);
            e.IgnoreDefense = j.GetFloat("ignoreDefense", 0f);
            e.IgnoreDefenseVsStatus = EnumUtil.Parse(j.GetString("ignoreDefenseVsStatus", "None"), StatusKind.None);
            e.IgnoreDefenseUsesPerRound = j.GetInt("ignoreDefenseUsesPerRound", 0);
            e.ScaleByBandGap = j.GetBool("scaleByBandGap", false);
            e.ScaleByBandHigh = j.GetBool("scaleByBandHigh", false);
            e.TargetBanKind = EnumUtil.Parse(j.GetString("targetBan", "None"), TargetBan.None);
            e.TargetBanStatus = EnumUtil.Parse(j.GetString("targetBanStatus", "None"), StatusKind.None);
            e.PreventExtend = j.GetBool("preventExtend", false);
            return e;
        }

        /// <summary>
        /// A field-for-field copy. Relics rewrite Target and Hits, and the EffectDef they are
        /// handed is the one owned by the AbilityDef in the ContentDatabase - shared by every unit
        /// in every battle for the life of the process. Mutating it in place would leak one
        /// wearer's relic onto every future cast of that ability by anybody, including the enemy
        /// side, and would survive across battles. Every relic rewrite goes through this.
        /// </summary>
        public EffectDef Clone()
        {
            EffectDef c = new EffectDef();
            c.Kind = Kind;
            c.Target = Target;
            c.Power = Power;
            c.Flat = Flat;
            c.Hits = Hits;
            c.Status = Status;
            c.Duration = Duration;
            c.Magnitude = Magnitude;
            c.Chance = Chance;
            c.SelfHpRatio = SelfHpRatio;
            c.MaxStacks = MaxStacks;
            c.PerStack = PerStack;
            c.ConsumesResolve = ConsumesResolve;
            c.Condition = Condition;
            c.ConditionOn = ConditionOn;
            c.ConditionStatus = ConditionStatus;
            c.ConditionValue = ConditionValue;
            c.TargetStatus = TargetStatus;
            c.SecondaryTarget = SecondaryTarget;
            c.PerStackStatus = PerStackStatus;
            c.PerStackOn = PerStackOn;
            c.StoredCap = StoredCap;
            c.Charges = Charges;
            c.TargetMaxHpRatio = TargetMaxHpRatio;
            c.TargetCurrentHpRatio = TargetCurrentHpRatio;
            c.TargetMissingHpRatio = TargetMissingHpRatio;
            c.Record = Record;
            c.UsesPerRound = UsesPerRound;
            c.ShiftBanksAs = ShiftBanksAs;
            c.Splash = Splash;
            c.ComparedStat = ComparedStat;
            c.Condition2 = Condition2;
            c.ConditionOn2 = ConditionOn2;
            c.ConditionStatus2 = ConditionStatus2;
            c.ConditionValue2 = ConditionValue2;
            c.ComparedStat2 = ComparedStat2;
            c.ReleaseAsHeal = ReleaseAsHeal;
            c.LedgerAutoRelease = LedgerAutoRelease;
            c.LedgerMitigates = LedgerMitigates;
            c.LedgerBankLimit = LedgerBankLimit;
            c.PerTarget = PerTarget;
            c.DispelOwnOnly = DispelOwnOnly;
            c.TransferFromTarget = TransferFromTarget;
            c.LedgerBankHeals = LedgerBankHeals;
            c.StoredCapBasis = StoredCapBasis;
            c.LedgerAutoReleaseAsHeal = LedgerAutoReleaseAsHeal;
            c.LedgerAutoReleaseTarget = LedgerAutoReleaseTarget;
            c.LedgerBankOnlyFrom = LedgerBankOnlyFrom;
            c.ReleaseFraction = ReleaseFraction;
            c.MissingHpCap = MissingHpCap;
            c.Condition3 = Condition3;
            c.ConditionOn3 = ConditionOn3;
            c.ConditionStatus3 = ConditionStatus3;
            c.ConditionValue3 = ConditionValue3;
            c.ComparedStat3 = ComparedStat3;
            c.ScaleByLastRelease = ScaleByLastRelease;
            c.ScaleByLastRedirect = ScaleByLastRedirect;
            c.ScaleByLastRemoval = ScaleByLastRemoval;
            c.ScaleByLastBanked = ScaleByLastBanked;
            c.LedgerBankFromCaster = LedgerBankFromCaster;
            c.BypassResist = BypassResist;
            c.MaxHpCap = MaxHpCap;
            c.ConsumeStacks = ConsumeStacks;
            c.StackOnReapply = StackOnReapply;
            c.LedgerAutoReleaseSplitHeal = LedgerAutoReleaseSplitHeal;
            c.DoTRecord = DoTRecord;
            c.LedgerFunnelSource = LedgerFunnelSource;
            c.LedgerFunnelConvert = LedgerFunnelConvert;
            c.CounterIndex = CounterIndex;
            c.ConditionCounterIndex = ConditionCounterIndex;
            c.ConditionCounterIndex2 = ConditionCounterIndex2;
            c.ConditionCounterIndex3 = ConditionCounterIndex3;
            // THE NINE THIS METHOD USED TO DROP, and the drop was live. Clone is not a debug
            // helper: ApplyTalentOverrides and ApplyTalentAppends rebuild the modified ability
            // through it, so a hero who SELECTS a talent that touches an ability loses every
            // field missing from this list on that ability - silently, and only for the players
            // who pick that talent. Vaelis's talent I appends to his basic, whose LinkUnits sets
            // linkMutual and linkTransfer, so Departure stopped being a mutual link for anyone
            // who took it; his talent II overrides the two MoveBuffs effects that carry
            // moveDuration, which is the field talent II exists to modify.
            //
            // Enumerated against the field list rather than recalled: 81 fields, 72 were copied.
            c.LinkTransfer = LinkTransfer;
            c.LinkMutual = LinkMutual;
            c.LinkAmountPower = LinkAmountPower;
            c.LinkFromCaster = LinkFromCaster;
            c.ScaleByLastMoved = ScaleByLastMoved;
            c.ScaleByLastDamage = ScaleByLastDamage;
            c.MoveDuration = MoveDuration;
            c.TransferDuration = TransferDuration;
            c.TransferMagnitude = TransferMagnitude;
            c.AttackCap = AttackCap;
            c.ScaleByLastShift = ScaleByLastShift;
            c.IgnoreDefense = IgnoreDefense;
            c.IgnoreDefenseVsStatus = IgnoreDefenseVsStatus;
            c.IgnoreDefenseUsesPerRound = IgnoreDefenseUsesPerRound;
            c.ScaleByBandGap = ScaleByBandGap;
            c.ScaleByBandHigh = ScaleByBandHigh;
            c.TargetBanKind = TargetBanKind;
            c.TargetBanStatus = TargetBanStatus;
            c.PreventExtend = PreventExtend;
            return c;
        }
    }

    public sealed class AbilityDef
    {
        public string Id = "";
        public string Name = "";
        public string Description = "";
        public AbilityKind Kind = AbilityKind.Active;
        public int EnergyCost = 100;
        public string Vfx = "impact";     // impact | slash | burst | heal | buff | shadow | bolt
        public List<EffectDef> Effects = new List<EffectDef>();

        public static AbilityDef FromJson(JsonValue j, AbilityKind fallbackKind)
        {
            AbilityDef a = new AbilityDef();
            if (j == null || j.IsNull) return null;
            a.Id = j.GetString("id", "");
            a.Name = j.GetString("name", "Unnamed");
            a.Description = j.GetString("description", "");
            a.Kind = EnumUtil.Parse(j.GetString("kind", fallbackKind.ToString()), fallbackKind);
            a.EnergyCost = j.GetInt("energyCost", a.Kind == AbilityKind.Active ? 100 : 0);
            a.Vfx = j.GetString("vfx", "impact");
            List<JsonValue> effects = j.GetList("effects");
            for (int i = 0; i < effects.Count; i++) a.Effects.Add(EffectDef.FromJson(effects[i]));
            return a;
        }
    }

    /// <summary>
    /// One parameter override a talent applies to another ability on the same hero. A talent that
    /// carries these modifies the named ability's named field by an additive delta at unit-build
    /// time, the same moment the talent itself is resolved.
    ///
    /// The override is applied by cloning the affected EffectDef (and its parent AbilityDef) on
    /// the CombatUnit that carries the talent, exactly the way relics already clone before
    /// rewriting Target and Hits. The content-database copy is never touched.
    ///
    /// An override with no matching ability or effect index is silently ignored - content may
    /// name a slot the hero has not authored yet, which is the same tolerance TalentAt already
    /// applies to an out-of-range selection.
    ///
    /// AND THE SILENCE STAYS, DELIBERATELY, because it was asked whether this should refuse
    /// loudly instead. It should not, HERE. Eight authored targets did survive authoring because
    /// nothing said a word, so the question is a fair one - but the cost of answering it in the
    /// engine is paid by players rather than by authors:
    ///
    ///   - SelectedTalent is SAVE state and the effect list it indexes is CONTENT. A balance patch
    ///     that shortens an effect list invalidates an index that was legal when the player picked
    ///     it. Refusing here turns that patch into a crash at battle start for everyone holding
    ///     that talent, which is a worse failure than a talent doing less than its text.
    ///   - Run() is a pure function with no logging channel, by design - it returns an event list
    ///     and the screen reads it. A warning would be the first impurity in it, and a throw would
    ///     take the idle resolver down with the battle screen.
    ///
    /// The loudness belongs where the mistake is MADE, not where it lands, and that is build time:
    /// CheckTalentTargetsResolve fails on an index that does not exist, and
    /// CheckTalentOverridesNameALiveField fails on one that exists but names a field the effect
    /// never reads - the quieter half of the same fault, and the half that looks alive.
    ///
    /// Nothing relies on the drop today: as of this pass every authored target resolves.
    /// </summary>
    public sealed class TalentOverrideDef
    {
        /// <summary>
        /// "basic", "active", or the JSON KEY of a passive - "passive", "passive1", "passive2",
        /// and where a hero authors both shapes, "skills.passive1" / "skills.passive2" or
        /// "passives.N". Not a position: see PassiveDef.SlotKey.
        /// </summary>
        public string Ability = "";

        /// <summary>0-based index into the ability's Effects list.</summary>
        public int EffectIndex;

        /// <summary>"power", "magnitude", "maxStacks", "duration", "flat", "chance",
        /// "selfHpRatio", "targetMaxHpRatio", "targetMissingHpRatio", "storedCap",
        /// "hits", "perStack", "conditionValue", "ignoreDefense" - and on the ABILITY rather than
        /// an effect, "energyCost" for a basic or active and "threshold", "firesPerSubject" or
        /// "limitOff" for a passive.</summary>
        public string Field = "";

        /// <summary>Additive delta applied to the field's current value.</summary>
        public float Delta;

        public static TalentOverrideDef FromJson(JsonValue j)
        {
            if (j == null || j.IsNull) return null;
            TalentOverrideDef o = new TalentOverrideDef();
            o.Ability = j.GetString("ability", "");
            o.EffectIndex = j.GetInt("effectIndex", 0);
            o.Field = j.GetString("field", "");
            o.Delta = j.GetFloat("delta", 0f);
            return o;
        }
    }

    /// <summary>
    /// A full effect a talent appends to a named ability or passive, rather than modifying an
    /// existing effect's numeric field. Applied at unit-build time alongside TalentOverrideDef.
    /// The effect is added to the end of the named ability's effect list, so it resolves last.
    /// </summary>
    public sealed class TalentAppendDef
    {
        /// <summary>"basic", "active", or a passive's JSON key. See TalentOverrideDef.Ability.</summary>
        public string Ability = "";

        /// <summary>The full effect to append.</summary>
        public EffectDef Effect;

        /// <summary>
        /// WHERE IN THE CLAUSE THE LINE GOES. -1, the default and the shape of every append
        /// written before this, puts it at the end.
        ///
        /// The end is wrong whenever the clause SPENDS something the added line wants to read.
        /// Mireth's Missing Page pays out at three Pages and then clears them in its last effect;
        /// a talent that adds "and also take 10 Energy when that happens" tested the Pages after
        /// the clause had already consumed them, so it could never once pass - on the right host,
        /// with the right subject, and still dead. Position is part of what the append means.
        ///
        /// Clamped into range at build time, so a talent naming a position the ability does not
        /// have lands at the end rather than throwing.
        /// </summary>
        public int AtIndex = -1;

        public static TalentAppendDef FromJson(JsonValue j)
        {
            if (j == null || j.IsNull) return null;
            TalentAppendDef a = new TalentAppendDef();
            a.Ability = j.GetString("ability", "");
            a.Effect = EffectDef.FromJson(j["effect"]);
            a.AtIndex = j.GetInt("atIndex", -1);
            if (a.Effect == null) return null;
            return a;
        }
    }

    public sealed class PassiveDef
    {
        public string Id = "";

        /// <summary>
        /// THE JSON KEY THIS PASSIVE WAS READ FROM: "passive", "passive1", "passive2",
        /// "skills.passive1", "skills.passive2", or "passives.N" for the overflow list.
        ///
        /// It exists so a talent can address a passive BY THE NAME ITS AUTHOR WROTE. A talent slot
        /// used to be a position in the built list, and the list begins with the stat block a
        /// described passive was split into - so "passive2" meant the SECOND ENTRY, which is the
        /// author's passive1, and every talent on the roster was written one short. That produced
        /// the same defect three times: dropped overrides, overrides setting a field the effect it
        /// landed on never reads, and appends reading a subject their host never publishes. Three
        /// detectors were built for it, and none could see the fourth case - a wrong slot that
        /// happens to be a real passive, which fires and looks entirely correct.
        ///
        /// A key cannot be off by one. That is the whole argument.
        /// </summary>
        public string SlotKey = "";
        public string Name = "";
        public string Description = "";
        public PassiveTrigger Trigger = PassiveTrigger.None;
        public float Threshold = 0.5f;        // for OnHpBelowThreshold
        public bool OncePerBattle = true;     // for threshold style passives

        /// <summary>
        /// A finer limit than OncePerBattle, and layered ON it rather than replacing it.
        ///
        /// OncePerBattle keeps its trigger-dependent default and keeps being checked, because every
        /// piece of shipped content relies on that default and on nothing else. Limit is consulted
        /// afterwards and is Unlimited unless content says otherwise, so an unchanged passive
        /// answers exactly the question it always did.
        ///
        /// LimitRounds is the N in EveryNRounds and is ignored by every other member.
        /// </summary>
        public PassiveLimit Limit = PassiveLimit.Unlimited;
        public int LimitRounds = 2;

        /// <summary>
        /// How many times this passive may fire AGAINST ONE TARGET, 0 for unlimited. A different
        /// question from `Limit`, and not expressible through it: Limit is answered from the
        /// owner's own clock when the passive FIRES, while this is answered once an effect has
        /// resolved its targets and needs the target's identity carried into the check.
        ///
        /// `PerTargetPerRound` picks the window. False counts over the whole battle ("once per
        /// target each battle", three of the four abilities that want this); true resets every
        /// round ("once per enemy each round", Rheia's).
        ///
        /// The limit gates the EFFECT, not the trigger, so a passive whose per-target budget is
        /// spent against one enemy still fires normally against another. That is the shape all
        /// four descriptions have, and it is why this could not be folded into PassiveLimit.
        /// </summary>
        public int PerTargetLimit;
        public bool PerTargetPerRound;

        /// <summary>
        /// How many times this passive may fire AGAINST ONE BROADCAST SUBJECT, 0 for unlimited.
        ///
        /// A THIRD BUDGET, and it exists because neither of the other two can say "twice per
        /// enemy". PassiveLimit is answered from the owner's clock and knows nothing about who the
        /// news was about. PerTargetLimit is spent inside the effect loop, once per effect that
        /// lands, so a passive with four effects and a budget of one spends the whole budget on
        /// its first effect and silently drops the other three - which is a separate defect, left
        /// alone here because repairing it moves five shipped kits.
        ///
        /// This is charged ONCE PER FIRING, against the broadcast subject, and charged AFTER the
        /// effects have resolved so a condition asking "have I fired on this one before" reads 0
        /// during the first firing rather than 1.
        ///
        /// Only meaningful on a broadcast trigger, which is the only kind that has a subject. Zero
        /// on every passive in shipped content, and a zero is never charged and never checked.
        /// </summary>
        public int FiresPerSubject;

        /// <summary>
        /// Parameter overrides this passive applies to another ability on the same unit, if any.
        /// Only meaningful on a talent - a passive that is appended at unit-build time - because
        /// that is the only moment the override is applied.
        ///
        /// Empty for every passive in shipped content. An empty list is the whole of why existing
        /// battles cannot move: the override path is never entered.
        /// </summary>
        public readonly List<TalentOverrideDef> Overrides = new List<TalentOverrideDef>();

        /// <summary>
        /// Full effects this talent appends to a named ability or passive. Each entry names an
        /// ability slot and carries a complete EffectDef that is added to the end of that ability's
        /// effect list at unit-build time. Parallel to Overrides, not a replacement for it.
        ///
        /// Empty for every passive in shipped content until a talent authors one. An empty list
        /// is inert: the append path is never entered.
        /// </summary>
        public readonly List<TalentAppendDef> Appends = new List<TalentAppendDef>();

        /// <summary>
        /// Filters OnStatusExpired to fire only when the specific status named here expired.
        /// None means no filter — the trigger fires for any expiry, which is the existing behaviour.
        /// </summary>
        public StatusKind TriggerStatus = StatusKind.None;

        /// <summary>
        /// Fires only for a declaration of the ability with this id. Empty (the default) answers
        /// any cast, which is every passive in shipped content.
        ///
        /// "A reaction that fires in response to a SPECIFIC ability" needs the ability named, and
        /// an id is the only thing that names one uniquely - a name is display text and a kind is
        /// a category. Read by the cast triggers and ignored by every other one, the same way
        /// TriggerStatus is read only by the status triggers.
        /// </summary>
        public string TriggerAbilityId = "";

        // ---------- batch two: per-stack output modifier ----------

        /// <summary>Which stacking status on the owner drives the output modifier. None means no
        /// modifier — the whole system is inert.</summary>
        /// <summary>
        /// A standing target restriction this passive gives its owner. None on every passive in
        /// shipped content, so no unit in any existing battle is restricted and the filter cannot
        /// reject a candidate it was never asked about.
        /// </summary>
        public TargetBan TargetBanKind = TargetBan.None;

        /// <summary>The status TargetBan.CarryingStatus names. Ignored by the other kinds.</summary>
        public StatusKind TargetBanStatus = StatusKind.None;

        public StatusKind OutputModStatus = StatusKind.None;

        /// <summary>Per-stack multiplier added to outgoing damage or healing.</summary>
        public float OutputModPerStack;

        /// <summary>Which outgoing value to scale: all damage, DoT only, or healing only.</summary>
        public OutputModScope OutputModScope = OutputModScope.AllDamage;

        /// <summary>When true, the modifier only applies against targets carrying the owner's
        /// Mark or MarkAlt.</summary>
        public bool OutputModMarkedOnly;

        /// <summary>When true on a broadcast trigger, fires only if the broadcast source (healer,
        /// attacker, etc.) is the passive owner.</summary>
        public bool TriggerSourceIsSelf;

        /// <summary>
        /// When true, fires only if the source that caused the dispatch is on the OWNER'S OWN SIDE
        /// and is not the owner itself. The sibling of TriggerSourceIsSelf, read at the same place
        /// and from the same `_broadcastSourceId`.
        ///
        /// It exists because "when an ALLY hits the unit I am bound to" is not expressible with the
        /// two filters that were here: TriggerSourceIsSelf is the wrong half, and no condition can
        /// ask about a unit's side - ConditionSubject.LastAttacker names the attacker but every
        /// EffectCondition then asks about its HP, its statuses or its stats, never about which
        /// line it stands on.
        ///
        /// Excludes the owner deliberately. Every ally-facing broadcast in this engine excludes the
        /// unit that already has its own owner-centric trigger for the same event, and a hero
        /// answering its OWN blow is a different ability from one answering an ally's.
        ///
        /// Setting both flags is authorable and always false, since a source cannot be the owner
        /// and not the owner at once. That is a content error this does not try to catch: it makes
        /// the passive inert rather than wrong, which is the same way a mis-authored condition
        /// behaves everywhere else here.
        /// </summary>
        public bool TriggerSourceIsAlly;

        // ---------- batch three: resolve-to-stat scaling ----------

        /// <summary>Which stacking status on the owner adds to a base stat. None means no scaling
        /// — the whole system is inert.</summary>
        public StatusKind StatModStatus = StatusKind.None;

        /// <summary>Per-stack additive contribution to the stat named by StatModKind.</summary>
        public float StatModPerStack;

        /// <summary>Which stat each stack modifies. Only Speed is supported in this pass.</summary>
        public ComparedStat StatModKind = ComparedStat.Speed;

        // ---------- funnel-banking primitives: stack-threshold auto-trigger ----------

        /// <summary>Which stacking status on the owner this OnStackThreshold passive watches.
        /// None means no threshold — the trigger is inert.</summary>
        public StatusKind StackThresholdStatus = StatusKind.None;

        /// <summary>How many stacks of StackThresholdStatus must accumulate before the trigger
        /// fires and consumes them.</summary>
        public int StackThresholdCount;

        /// <summary>Counter index for OnStackThreshold. Zero means derive from StackThresholdStatus.
        /// ≥ 2 watches a generalized counter on CombatUnit.Counters.</summary>
        public int CounterIndex;

        public string Vfx = "buff";
        public List<EffectDef> Effects = new List<EffectDef>();

        public static PassiveDef FromJson(JsonValue j)
        {
            if (j == null || j.IsNull) return null;
            PassiveDef p = new PassiveDef();
            p.Id = j.GetString("id", "");
            p.Name = j.GetString("name", "Passive");
            p.Description = j.GetString("description", "");
            p.Trigger = EnumUtil.Parse(j.GetString("trigger", "None"), PassiveTrigger.None);
            p.Threshold = j.GetFloat("threshold", 0.5f);
            p.OncePerBattle = j.GetBool("oncePerBattle", p.Trigger == PassiveTrigger.OnHpBelowThreshold || p.Trigger == PassiveTrigger.BattleStart);
            p.Limit = EnumUtil.Parse(j.GetString("limit", "Unlimited"), PassiveLimit.Unlimited);
            p.LimitRounds = Math.Max(1, j.GetInt("limitRounds", 2));
            p.PerTargetLimit = Math.Max(0, j.GetInt("perTargetLimit", 0));
            p.PerTargetPerRound = j.GetBool("perTargetPerRound", false);
            p.FiresPerSubject = j.GetInt("firesPerSubject", 0);
            p.TriggerStatus = EnumUtil.Parse(j.GetString("triggerStatus", "None"), StatusKind.None);
            p.TriggerAbilityId = j.GetString("triggerAbilityId", "");
            p.TargetBanKind = EnumUtil.Parse(j.GetString("targetBan", "None"), TargetBan.None);
            p.TargetBanStatus = EnumUtil.Parse(j.GetString("targetBanStatus", "None"), StatusKind.None);
            p.OutputModStatus = EnumUtil.Parse(j.GetString("outputModStatus", "None"), StatusKind.None);
            p.OutputModPerStack = j.GetFloat("outputModPerStack", 0f);
            p.OutputModScope = EnumUtil.Parse(j.GetString("outputModScope", "AllDamage"), OutputModScope.AllDamage);
            p.OutputModMarkedOnly = j.GetBool("outputModMarkedOnly", false);
            p.TriggerSourceIsSelf = j.GetBool("triggerSourceIsSelf", false);
            p.TriggerSourceIsAlly = j.GetBool("triggerSourceIsAlly", false);
            p.StatModStatus = EnumUtil.Parse(j.GetString("statModStatus", "None"), StatusKind.None);
            p.StatModPerStack = j.GetFloat("statModPerStack", 0f);
            p.StatModKind = EnumUtil.Parse(j.GetString("statModKind", "Speed"), ComparedStat.Speed);
            p.StackThresholdStatus = EnumUtil.Parse(j.GetString("stackThresholdStatus", "None"), StatusKind.None);
            p.StackThresholdCount = Math.Max(0, j.GetInt("stackThresholdCount", 0));
            p.CounterIndex = Math.Max(0, j.GetInt("counterIndex", 0));
            p.Vfx = j.GetString("vfx", "buff");
            List<JsonValue> effects = j.GetList("effects");
            for (int i = 0; i < effects.Count; i++) p.Effects.Add(EffectDef.FromJson(effects[i]));

            List<JsonValue> overrides = j.GetList("overrides");
            for (int i = 0; i < overrides.Count; i++)
            {
                TalentOverrideDef o = TalentOverrideDef.FromJson(overrides[i]);
                if (o != null) p.Overrides.Add(o);
            }

            List<JsonValue> appends = j.GetList("appends");
            for (int i = 0; i < appends.Count; i++)
            {
                TalentAppendDef a = TalentAppendDef.FromJson(appends[i]);
                if (a != null) p.Appends.Add(a);
            }
            return p;
        }
    }

    /// <summary>Shared shape for anything that can stand on the battlefield.</summary>
    public abstract class UnitDef
    {
        public string Id = "";
        public string Name = "";
        public Aspect Aspect = Aspect.Ember;
        public float BaseHp = 1000;
        public float BaseAttack = 100;
        public float BaseDefense = 60;
        public float BaseSpeed = 100;
        public float CritChance = 0.15f;
        public float CritDamage = 1.6f;
        /// <summary>Flat landing-chance bonus/penalty for harmful ApplyStatus effects. Phase A: always 0, authored nowhere.</summary>
        public float EffectHit;
        public float EffectResist;
        /// <summary>Phase B flat stat: multiplies activeSkill/passive-sourced Damage, never Basic Attack. See ApplyDamageEffect step 4.</summary>
        public float SkillDamage;
        /// <summary>Phase B flat stat: target-side chance to fully negate an incoming hit. See ApplyDamageEffect step 0 (before Base).</summary>
        public float Block;
        /// <summary>Phase B flat stat: source-side multiplicative reduction of the target's mitigation ratio. See ApplyDamageEffect step 6.</summary>
        public float ArmorBreak;
        /// <summary>Phase B flat stat: target-side chance reduction against Silence/Stun landing only. See ApplyEffect's ApplyStatus branch.</summary>
        public float ControlImmunity;
        /// <summary>Phase B flat stat: flat percentage reduction of incoming damage, applied after Defense mitigation. See ApplyDamageEffect step 7.</summary>
        public float DamageReduction;
        /// <summary>Phase B flat stat: source-side flat bonus damage added after Defense/Armor Break mitigation, so Defense never reduces it. See ApplyDamageEffect step 6.5.</summary>
        public float HolyDamage;
        public float Growth = 0.11f;   // per level, applied to hp / attack / defense
        public int StartEnergy;
        public VisualDef Visual = new VisualDef();
        public AbilityDef BasicAttack;
        public AbilityDef Active;

        /// <summary>
        /// Every passive this unit carries, in the order they resolve. A list rather than the
        /// single PassiveDef that stood here because the incoming roster authors passive1 AND
        /// passive2 on every 5-star, and one field per passive would have meant the simulation
        /// learning how many there are - see CombatSimulation.TriggerPassive, which now walks this
        /// list and is the only thing that decides what fires.
        ///
        /// Order is authoring order and is load bearing: two passives on the same trigger resolve
        /// first-authored-first, so the record reads the way the hero card does. Every unit in
        /// shipped content authors exactly one, which makes this a one-element list and every walk
        /// over it identical to the single-field read it replaces.
        /// </summary>
        public readonly List<PassiveDef> Passives = new List<PassiveDef>();

        protected void ReadCommon(JsonValue j)
        {
            Id = j.GetString("id", "");
            Name = j.GetString("name", Id);
            Aspect = EnumUtil.Parse(j.GetString("aspect", "Ember"), Aspect.Ember);
            BaseHp = j.GetFloat("hp", BaseHp);
            BaseAttack = j.GetFloat("attack", BaseAttack);
            BaseDefense = j.GetFloat("defense", BaseDefense);
            BaseSpeed = j.GetFloat("speed", BaseSpeed);
            CritChance = j.GetFloat("critChance", CritChance);
            CritDamage = j.GetFloat("critDamage", CritDamage);
            EffectHit = j.GetFloat("effectHit", EffectHit);
            EffectResist = j.GetFloat("effectResist", EffectResist);
            SkillDamage = j.GetFloat("skillDamage", SkillDamage);
            Block = j.GetFloat("block", Block);
            ArmorBreak = j.GetFloat("armorBreak", ArmorBreak);
            ControlImmunity = j.GetFloat("controlImmunity", ControlImmunity);
            DamageReduction = j.GetFloat("damageReduction", DamageReduction);
            HolyDamage = j.GetFloat("holyDamage", HolyDamage);
            Growth = j.GetFloat("growth", Growth);
            StartEnergy = j.GetInt("startEnergy", 0);
            Visual = VisualDef.FromJson(j["visual"]);
            BasicAttack = AbilityDef.FromJson(j["basic"], AbilityKind.BasicAttack);
            Active = AbilityDef.FromJson(j["active"], AbilityKind.Active);
            ReadPassives(j);
            if (BasicAttack == null) BasicAttack = DefaultBasicAttack();
        }

        /// <summary>
        /// Every passive this unit authors, from every shape content has ever used, in one fixed
        /// order. Three shapes are read, not one, because the incoming roster is nested and the
        /// shipped files are flat, and both have to keep loading out of the same parser:
        ///
        ///   "passive":  { ... }                          - the single flat key, every shipped unit
        ///   "passive1": { ... }, "passive2": { ... }      - flat, two named slots
        ///   "skills": { "passive1": ..., "passive2": ... } - nested, as the new roster authors it
        ///   "passives": [ { ... }, { ... } ]               - the overflow list, read last
        ///
        /// THE FIFTH SHAPE EXISTS BECAUSE FOUR NAMED SLOTS IS A CEILING, NOT A DESIGN. A described
        /// passive routinely becomes several authored ones - one per trigger it answers - and the
        /// named slots ran out at five while a single hero's two described passives and one active
        /// wanted seven between them. The list is read after the named slots and appended in
        /// order, so a hero using only the named ones is parsed exactly as before.
        ///
        /// A unit authoring only the first - which is every hero and every enemy in content today -
        /// gets a one-element list, so nothing about how its passive resolves changes.
        ///
        /// The duplicate-id guard is for the migration specifically: a file half-converted to the
        /// nested shape can name the same passive twice, and a passive resolving twice per trigger
        /// is a doubled effect rather than a parse error anyone would notice. Only non-empty ids
        /// are compared - an unnamed passive is not evidence of anything.
        /// </summary>
        private void ReadPassives(JsonValue j)
        {
            AddPassive(PassiveDef.FromJson(j["passive"]), "passive");
            AddPassive(PassiveDef.FromJson(j["passive1"]), "passive1");
            AddPassive(PassiveDef.FromJson(j["passive2"]), "passive2");

            JsonValue skills = j["skills"];
            AddPassive(PassiveDef.FromJson(skills["passive1"]), "skills.passive1");
            AddPassive(PassiveDef.FromJson(skills["passive2"]), "skills.passive2");

            // The overflow list, last so the named slots keep the indices they have always had -
            // talents address a passive BY INDEX, and reading this first would renumber every
            // talent in the file. Empty on every unit in content until a kit needs a sixth.
            List<JsonValue> extra = j.GetList("passives");
            for (int i = 0; i < extra.Count; i++)
                AddPassive(PassiveDef.FromJson(extra[i]), "passives." + i);
        }

        private void AddPassive(PassiveDef passive, string slotKey)
        {
            if (passive == null) return;
            passive.SlotKey = slotKey;
            if (!string.IsNullOrEmpty(passive.Id))
                for (int i = 0; i < Passives.Count; i++)
                    if (Passives[i].Id == passive.Id) return;
            Passives.Add(passive);
        }

        public static AbilityDef DefaultBasicAttack()
        {
            AbilityDef a = new AbilityDef();
            a.Id = "basic_strike";
            a.Name = "Strike";
            a.Description = "A standard attack.";
            a.Kind = AbilityKind.BasicAttack;
            a.EnergyCost = 0;
            a.Vfx = "impact";
            EffectDef e = new EffectDef();
            e.Kind = EffectKind.Damage;
            e.Target = TargetMode.FrontEnemy;
            e.Power = 1f;
            a.Effects.Add(e);
            return a;
        }
    }

    public sealed class HeroDef : UnitDef
    {
        public string Title = "";
        public string Lore = "";
        public HeroRole Role = HeroRole.Striker;
        public int Stars = 5;
        public bool Fusable = true;
        public bool StartingHero;

        /// <summary>
        /// How many talents a hero may author. Three, per the incoming roster, and a hard ceiling
        /// rather than a convention: the parser stops reading at this many and
        /// HeroInstance.SelectedTalent is validated against it, so a fourth authored talent is
        /// ignored instead of becoming a slot no save can name.
        /// </summary>
        public const int MaxTalents = 3;

        /// <summary>
        /// The three talents this hero offers, in authoring order. A talent is a PassiveDef and
        /// nothing else - the same fields, the same triggers, the same effects - because a selected
        /// talent IS an additional passive on the unit and giving it its own type would mean a
        /// second thing for the simulation to learn (DESIGN-BUILD-ORDER: one hook, configured by
        /// content, beats one-off code per owner).
        ///
        /// Authored, not selected. Which one is live is per-player state and lives on
        /// HeroInstance.SelectedTalent, because two players owning the same hero must be able to
        /// run different talents - a def is shared by every unit in every battle for the life of
        /// the process, exactly as EffectDef.Clone's own comment records.
        ///
        /// An empty list is the norm today: no shipped hero authors a talent, so nothing in
        /// content can reach the talent path at all.
        /// </summary>
        public readonly List<PassiveDef> Talents = new List<PassiveDef>();

        /// <summary>
        /// The talent a 1-based selection names, or null for "none" (0) and for any index this hero
        /// does not actually author. Null is the whole of what an unselected talent does: the
        /// simulation appends nothing, so there is no talent-shaped branch anywhere below it.
        ///
        /// Tolerant of an out-of-range index on purpose. A save can name talent 3 for a hero whose
        /// third talent was later removed from content, which is the same situation as a save
        /// naming a relic that no longer exists - the id is kept, the resolve returns nothing, and
        /// re-adding the content restores the selection rather than having silently wiped it.
        /// </summary>
        public PassiveDef TalentAt(int selection)
        {
            if (selection < 1 || selection > Talents.Count) return null;
            return Talents[selection - 1];
        }

        /// <summary>
        /// The key spellings one talent slot answers to, most-specific first. Roman numerals are
        /// what the incoming roster authors (talentI, talentII, talentIII); the arabic aliases are
        /// read too so a hand-written test fixture does not have to guess which the parser wants.
        /// </summary>
        private static readonly string[][] TalentKeys = new string[][]
        {
            new string[] { "talentI", "talent1" },
            new string[] { "talentII", "talent2" },
            new string[] { "talentIII", "talent3" }
        };

        public static HeroDef FromJson(JsonValue j)
        {
            HeroDef h = new HeroDef();
            h.ReadCommon(j);
            h.Title = j.GetString("title", "");
            h.Lore = j.GetString("lore", "");
            h.Role = EnumUtil.Parse(j.GetString("role", "Striker"), HeroRole.Striker);
            h.Stars = Math.Max(1, Math.Min(5, j.GetInt("stars", 5)));
            h.Fusable = j.GetBool("fusable", true);
            h.StartingHero = j.GetBool("startingHero", false);
            h.ReadTalents(j);
            return h;
        }

        /// <summary>
        /// Reads the talents block, in either shape content might carry it: an object of named
        /// slots, which is how the incoming roster authors it, or a plain array.
        ///
        /// A null entry is a hero who has not been given that talent yet - the incoming file has 64
        /// of them - and it is skipped rather than being stored as a hole, so Talents.Count is the
        /// count of talents that actually exist and TalentAt's range check is the only bounds test
        /// anything needs. That does mean a hero authoring only talentIII would have it selectable
        /// as talent 1; a hero authoring a gap is a content bug, and ContentDatabase warns about it
        /// rather than this silently inventing a position for it.
        /// </summary>
        private void ReadTalents(JsonValue j)
        {
            JsonValue block = j["talents"];
            if (block.IsNull) return;

            if (block.IsArray)
            {
                List<JsonValue> list = block.AsList();
                for (int i = 0; i < list.Count && Talents.Count < MaxTalents; i++)
                    AddTalent(PassiveDef.FromJson(list[i]));
                return;
            }

            for (int slot = 0; slot < TalentKeys.Length && Talents.Count < MaxTalents; slot++)
            {
                string[] spellings = TalentKeys[slot];
                for (int k = 0; k < spellings.Length; k++)
                {
                    PassiveDef talent = PassiveDef.FromJson(block[spellings[k]]);
                    if (talent == null) continue;
                    AddTalent(talent);
                    break;
                }
            }
        }

        private void AddTalent(PassiveDef talent)
        {
            if (talent == null || Talents.Count >= MaxTalents) return;
            Talents.Add(talent);
        }

        /// <summary>
        /// Which authored talent slots this hero left empty below a filled one - the content bug
        /// ReadTalents deliberately does not paper over. Empty for every hero that authors a
        /// contiguous run of talents from the first slot, which is every well-formed hero.
        /// </summary>
        public static List<string> TalentGaps(JsonValue j)
        {
            List<string> gaps = new List<string>();
            JsonValue block = j["talents"];
            if (block.IsNull || block.IsArray) return gaps;

            bool seenFilled = false;
            for (int slot = TalentKeys.Length - 1; slot >= 0; slot--)
            {
                bool filled = false;
                for (int k = 0; k < TalentKeys[slot].Length; k++)
                    if (PassiveDef.FromJson(block[TalentKeys[slot][k]]) != null) filled = true;

                if (filled) { seenFilled = true; continue; }
                if (seenFilled) gaps.Add(TalentKeys[slot][0]);
            }
            return gaps;
        }
    }

    public sealed class EnemyDef : UnitDef
    {
        public string Kind = "minion"; // minion | elite | boss

        public static EnemyDef FromJson(JsonValue j)
        {
            EnemyDef e = new EnemyDef();
            e.ReadCommon(j);
            e.Kind = j.GetString("kind", "minion");
            return e;
        }
    }

    public sealed class EnemySpawn
    {
        public string EnemyId = "";
        public int Level = 1;

        public static EnemySpawn FromJson(JsonValue j)
        {
            EnemySpawn s = new EnemySpawn();
            s.EnemyId = j.GetString("id", "");
            s.Level = Math.Max(1, j.GetInt("level", 1));
            return s;
        }
    }

    public sealed class StageDef
    {
        public string Id = "";
        public string ChapterId = "";
        public string Name = "";
        public string Subtitle = "";
        public int Index;            // 1-based within the chapter
        public int GlobalIndex;      // 0-based across the whole campaign
        public bool IsBoss;
        public string Environment = "ashen";
        public List<EnemySpawn> Enemies = new List<EnemySpawn>();
        public int RewardGold = 100;
        public int RewardShards = 5;
        public int RewardAccountXp = 20;
        public int FirstClearGold = 200;
        public int FirstClearShards = 25;
        public int RecommendedPower = 100;
        public int IdleGoldPerHour;
        public int IdleShardsPerHour;

        /// <summary>
        /// THE WAVE CURVE. A stage is no longer one fixed fight; it is a definition that
        /// generates waveCount x battleCount of them, per DESIGN-CAMPAIGN-STRUCTURE.md.
        ///
        /// Enemies above is the roster AT THE FLOOR - wave 1, battle 1 spawns exactly what the
        /// flat pre-wave stage used to spawn, which is what makes the migration a restructure
        /// rather than a retune. Every later battle is the SAME roster at a higher level:
        /// composition never changes across a stage, only level, so there is one scaling
        /// language here and it is the one Recital already uses.
        ///
        /// FloorLevel is the highest authored level in the roster, and CeilingLevel is where
        /// that unit lands on the stage's last battle. Every enemy moves by the same delta, so
        /// a roster's internal spread (13/14/15) is carried up the curve unchanged rather than
        /// being flattened or re-spread.
        ///
        /// Nothing per-battle is stored. CampaignWaveGenerator computes a battle on demand from
        /// these four numbers; 20,736 battles exist as arithmetic, not as content.
        /// </summary>
        public int WaveCount = 1;
        public int BattleCount = 1;

        /// <summary>
        /// Which battle of the stage's ladder this StageDef IS, 1-based, or 0 for the authored
        /// stage that has not been resolved to a battle yet.
        ///
        /// Only CampaignWaveGenerator.BuildBattle sets it, and GameSession.BuildBattleRequest
        /// refuses any stage that expands to more than one battle while this is still 0. That
        /// pairing is what makes floor-pinning impossible rather than merely discouraged: before
        /// it, handing the authored StageDef straight to BuildBattleRequest silently fought wave 1
        /// battle 1 - the Enemies list is by construction the opening roster - and three separate
        /// balance instruments did exactly that for months while appearing to measure whole stages.
        /// A caller now has to say which battle it means, and cannot get the floor by accident.
        /// </summary>
        public int ResolvedBattleOrdinal;
        public int FloorLevel;
        public int CeilingLevel;

        /// <summary>True when this stage carries a real wave curve rather than being a single
        /// fixed fight. False for synthetic stages - Recital floors, Proving matches, prologue
        /// steps - which build their own enemies and must never be run through the generator.</summary>
        public bool HasWaveCurve
        {
            get { return WaveCount > 1 || BattleCount > 1; }
        }

        /// <summary>Total battles in this stage. 144 for a migrated campaign stage, 1 for a
        /// synthetic one, which is what lets callers treat both the same way.</summary>
        public int TotalBattles
        {
            get { return Math.Max(1, WaveCount) * Math.Max(1, BattleCount); }
        }

        public string DisplayCode
        {
            get { return ChapterNumber + "-" + Index; }
        }

        public int ChapterNumber = 1;

        public static StageDef FromJson(JsonValue j)
        {
            StageDef s = new StageDef();
            s.Id = j.GetString("id", "");
            s.Name = j.GetString("name", "Stage");
            s.Subtitle = j.GetString("subtitle", "");
            s.Index = j.GetInt("index", 1);
            s.IsBoss = j.GetBool("boss", false);
            s.Environment = j.GetString("environment", "ashen");
            s.RewardGold = j.GetInt("gold", 100);
            s.RewardShards = j.GetInt("shards", 5);
            s.RewardAccountXp = j.GetInt("accountXp", 20);
            s.FirstClearGold = j.GetInt("firstClearGold", s.RewardGold * 2);
            s.FirstClearShards = j.GetInt("firstClearShards", s.RewardShards * 5);
            s.RecommendedPower = j.GetInt("recommendedPower", 100);
            s.IdleGoldPerHour = j.GetInt("idleGoldPerHour", 0);
            s.IdleShardsPerHour = j.GetInt("idleShardsPerHour", 0);
            List<JsonValue> enemies = j.GetList("enemies");
            for (int i = 0; i < enemies.Count; i++) s.Enemies.Add(EnemySpawn.FromJson(enemies[i]));

            // Defaults of 1 rather than 12 on purpose: a stage with no curve authored is a
            // single fixed fight, which is exactly what every stage was before this schema and
            // what a hand-built synthetic stage still is. Content opts IN to the curve.
            s.WaveCount = Math.Max(1, j.GetInt("waveCount", 1));
            s.BattleCount = Math.Max(1, j.GetInt("battleCount", 1));

            // Floor defaults to the roster's own top level, so a stage that declares a ceiling
            // and forgets a floor still describes a real curve instead of scaling from zero.
            int rosterTop = 0;
            for (int i = 0; i < s.Enemies.Count; i++)
                if (s.Enemies[i].Level > rosterTop) rosterTop = s.Enemies[i].Level;
            s.FloorLevel = j.GetInt("floorLevel", rosterTop);
            s.CeilingLevel = j.GetInt("ceilingLevel", s.FloorLevel);
            return s;
        }
    }

    public sealed class ChapterDef
    {
        public string Id = "";
        public string Name = "";
        public string Subtitle = "";
        public int Number = 1;
        public List<StageDef> Stages = new List<StageDef>();
    }

    /// <summary>
    /// One stat a relic moves, and how. Numbers live HERE and only here.
    ///
    /// This type is the whole reason RelicDef can carry a stat bundle without a hook-based relic's
    /// unique passive losing its own no-numbers guarantee. That guarantee is no longer "this relic
    /// carries no numbers anywhere" - a mythic relic's Stats can be non-empty - it is narrower and
    /// just as real: Rule itself is still required to be a number-free sentence for any relic that
    /// carries a Hook, and the digit test is enforced against Rule specifically, in the harness and
    /// at content load. A relic with no Hook is required to carry a non-empty Stats list instead, or
    /// it does nothing. Both rules are enforced by ContentDatabase at load, not by review.
    /// </summary>
    public sealed class RelicStatBonus
    {
        public RelicStat Stat = RelicStat.Attack;
        public RelicStatMode Mode = RelicStatMode.Multiply;

        /// <summary>
        /// Read against Mode, never on its own. 0.14 with Multiply is "+14% Attack" and means
        /// Attack *= 1.14; 0.12 with Add is "+12% Crit Chance" and means CritChance += 0.12. The
        /// mode is stored rather than inferred from Stat so the two arithmetic rules are explicit
        /// in the data instead of implied by the stat name.
        /// </summary>
        public float Value;

        public static RelicStatBonus FromJson(JsonValue j)
        {
            if (j == null || j.IsNull) return null;
            RelicStatBonus b = new RelicStatBonus();
            b.Stat = EnumUtil.Parse(j.GetString("stat", "Attack"), RelicStat.Attack);
            b.Mode = EnumUtil.Parse(j.GetString("mode", "Multiply"), RelicStatMode.Multiply);
            b.Value = j.GetFloat("value", 0f);
            return b;
        }
    }

    /// <summary>
    /// One relic, at one of two grades (DESIGN-RELICS, renamed since). The grade decides which
    /// fields are live, enforced by validation rather than by convention:
    ///
    /// - A LEGENDARY relic (formerly "Common" - the label moved, the eight starting relics and
    ///   their values did not) carries Stats and leaves Hook at None. It resolves entirely in
    ///   StatMath before the simulation starts, and CombatSimulation never learns it exists.
    /// - A MYTHIC relic (formerly "Legendary") carries a Hook and states itself as one sentence in
    ///   Rule with no number in it, exactly as the grade named Legendary always did. What changed
    ///   at the rename is that a mythic relic can ALSO carry Stats now - a stat block stacked on
    ///   top of its unique passive, not a replacement for it. It resolves inside the simulation at
    ///   the hook's own resolution point, and its Stats fold in at the same setup step a legendary
    ///   relic's do.
    ///
    /// DESIGN-RELICS' founding "no relic contains an attack percentage" standard used to be
    /// enforced by this type's SHAPE - there was no numeric field anywhere on it, so a relic that
    /// wanted to be "+15% attack" had nowhere to put the 15. That standard was withdrawn as a
    /// blanket rule on 2026-08-26 and narrowed rather than removed: it now binds Rule specifically,
    /// for any relic carrying a Hook. The 15 still cannot go in Rule; it can now go in Stats, on
    /// either grade, which is the whole point of promoting a relic to mythic.
    /// </summary>
    public sealed class RelicDef
    {
        public string Id = "";
        public string Name = "";

        /// <summary>
        /// Set only on a relic that carries a Hook (today: mythic grade): the rule, stated as one
        /// sentence and never as a number. Empty on a relic with no Hook, which has no rule to
        /// state - its card shows DescribeStats() instead, and writing prose for it would be
        /// dressing up a stat bundle as something it is not.
        /// </summary>
        public string Rule = "";

        /// <summary>
        /// The hero this relic was designed around. Display only - nothing enforces it. A
        /// legendary (stat-only) relic never carries one: the grade is aspect-neutral and universal
        /// by design, and a stat bundle that only fits one kind of hero is a worse stat bundle, not
        /// a design.
        /// </summary>
        public string PairedHeroId = "";

        public RelicGrade Grade = RelicGrade.Legendary;

        /// <summary>Mythic only. None on every legendary relic - see the class summary.</summary>
        public RelicHook Hook = RelicHook.None;

        /// <summary>
        /// The stat bundle. Required to be non-empty on a legendary relic, since a legendary relic
        /// with no Hook and no Stats does nothing. Optional on a mythic relic - both shipped mythic
        /// relics carry one, stacked on top of their Hook, but nothing requires a future one to.
        /// </summary>
        public readonly List<RelicStatBonus> Stats = new List<RelicStatBonus>();

        /// <summary>
        /// The stat half of a card's text, built from the stat block rather than authored: "+14%
        /// Attack, +10% Speed". Lives here rather than in either screen so the shop and the
        /// inventory cannot drift into describing the same relic two different ways.
        ///
        /// One "%" for both modes is deliberate and is the player-facing half of the decision
        /// RelicStatMode documents: "+12% Crit Chance" reads the same whether the 12 multiplies or
        /// adds percentage points, and the difference is not something a player can act on.
        /// </summary>
        public string DescribeStats()
        {
            if (Stats.Count == 0) return "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Stats.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                int pct = (int)Math.Round(Stats[i].Value * 100f, MidpointRounding.AwayFromZero);
                sb.Append(pct >= 0 ? "+" : "").Append(pct).Append("% ").Append(StatLabel(Stats[i].Stat));
            }
            return sb.ToString();
        }

        /// <summary>Display spelling of a RelicStat. "Max HP" and "Crit Chance", not the enum name.</summary>
        public static string StatLabel(RelicStat stat)
        {
            switch (stat)
            {
                case RelicStat.MaxHp: return "Max HP";
                case RelicStat.Attack: return "Attack";
                case RelicStat.Defense: return "Defense";
                case RelicStat.Speed: return "Speed";
                case RelicStat.CritChance: return "Crit Chance";
                case RelicStat.CritDamage: return "Crit Damage";
                case RelicStat.EffectHit: return "Effect Hit";
                case RelicStat.EffectResist: return "Effect Resist";
            }
            return stat.ToString();
        }

        /// <summary>
        /// What a card shows for this relic. A legendary (stat-only) relic shows its numbers and
        /// nothing else - there is no rule to state. A mythic relic shows its rule AND its numbers,
        /// on two lines, because a mythic relic now genuinely has both halves and hiding either one
        /// would misrepresent what the player is equipping. `Rule` alone, with no Stats, still
        /// renders as just the rule - the second line only appears when there is something for it
        /// to say.
        /// </summary>
        public string CardText()
        {
            if (Grade == RelicGrade.Legendary) return DescribeStats();

            string stats = DescribeStats();
            return string.IsNullOrEmpty(stats) ? Rule : Rule + "\n" + stats;
        }

        public static RelicDef FromJson(JsonValue j)
        {
            if (j == null || j.IsNull) return null;
            RelicDef r = new RelicDef();
            r.Id = j.GetString("id", "");
            r.Name = j.GetString("name", "Relic");
            r.Rule = j.GetString("rule", "");
            r.PairedHeroId = j.GetString("pairedHero", "");
            r.Grade = EnumUtil.Parse(j.GetString("grade", "Legendary"), RelicGrade.Legendary);
            r.Hook = EnumUtil.Parse(j.GetString("hook", "None"), RelicHook.None);

            JsonValue stats = j["stats"];
            if (stats != null && stats.IsArray)
            {
                for (int i = 0; i < stats.Count; i++)
                {
                    RelicStatBonus b = RelicStatBonus.FromJson(stats[i]);
                    if (b != null) r.Stats.Add(b);
                }
            }
            return r;
        }
    }

    /// <summary>
    /// One rollable stat line in armor.json: a stat, and the closed range its value is drawn from.
    /// Used for both the primary and the substat tables, and for the attunement bonuses, which is
    /// why Min and Max are allowed to be equal - an attunement bonus is a fixed number, expressed
    /// as a degenerate range rather than as a second type.
    ///
    /// Weight is read only by the substat table. A primary is chosen by the slot, not rolled, and
    /// an attunement bonus is chosen by the aspect, so neither of those competes for a draw.
    /// </summary>
    public sealed class ArmorRollDef
    {
        public ArmorStat Stat = ArmorStat.Attack;
        public float Min;
        public float Max;

        /// <summary>Relative draw weight in the substat table. Clamped to at least 0 - an entry at
        /// zero is authored-but-disabled, which is a legitimate thing to want and is why this is
        /// not forced to 1.</summary>
        public int Weight = 10;

        public static ArmorRollDef FromJson(JsonValue j)
        {
            if (j == null || j.IsNull) return null;
            ArmorRollDef r = new ArmorRollDef();
            r.Stat = EnumUtil.Parse(j.GetString("stat", "Attack"), ArmorStat.Attack);
            r.Min = j.GetFloat("min", 0f);
            r.Max = j.GetFloat("max", r.Min);
            if (r.Max < r.Min) { float swap = r.Min; r.Min = r.Max; r.Max = swap; }
            r.Weight = Math.Max(0, j.GetInt("weight", 10));
            return r;
        }
    }

    /// <summary>
    /// The economy and generosity numbers for one temper rung. PrimaryScale multiplies whatever
    /// the primary table rolled, which is what makes a Radiant Weapon strictly better than a Rough
    /// one without needing a second primary table per rung.
    ///
    /// SalvageSlag and RerollSlag are the Forge's two prices. DESIGN-ARMOR section 7 requires
    /// salvage to pay less than reroll costs at every rung so the loop consumes pieces instead of
    /// printing Slag; ContentDatabase.Validate enforces it rather than trusting the author.
    /// </summary>
    public sealed class ArmorTemperDef
    {
        public ArmorTemper Temper = ArmorTemper.Rough;
        public float PrimaryScale = 1f;
        public int SalvageSlag;
        public int RerollSlag;

        /// <summary>
        /// Slag one Combine into THIS temper costs - i.e. combining three pieces one rung below
        /// costs the OUTPUT rung's CombineSlag, read via GameSession.CombineCost(outputTemper).
        /// Same per-temper field pattern SalvageSlag and RerollSlag already use, not a new pricing
        /// curve: DESIGN-ARMOR's Forge scales every one of its actions by temper this same way.
        ///
        /// Meaningless on Rough - nothing combines INTO the bottom rung, since there is no rung
        /// below it to combine FROM. Left at its zero default there; content validation does not
        /// require it to be authored on that one entry, only on Honed/Gilded/Radiant.
        /// </summary>
        public int CombineSlag;

        /// <summary>Relative draw weight for a drop at stage index 0. See ArmorDropWeightPerStageIndex
        /// for how deep stages tilt away from this.</summary>
        public int DropWeight = 10;

        /// <summary>
        /// How much this rung's drop weight moves per point of StageDef.GlobalIndex. Positive on
        /// the high rungs and negative on the low ones, so deep stages skew toward Gilded and
        /// Radiant without any rung ever becoming impossible - the roller floors the resulting
        /// weight at 1 rather than at 0, so a Rough piece stays reachable at the final stage and a
        /// Radiant one stays reachable at the first.
        /// </summary>
        public float DropWeightPerStageIndex;

        public static ArmorTemperDef FromJson(JsonValue j)
        {
            if (j == null || j.IsNull) return null;
            ArmorTemperDef t = new ArmorTemperDef();
            t.Temper = EnumUtil.Parse(j.GetString("temper", "Rough"), ArmorTemper.Rough);
            t.PrimaryScale = j.GetFloat("primaryScale", 1f);
            t.SalvageSlag = Math.Max(0, j.GetInt("salvageSlag", 0));
            t.RerollSlag = Math.Max(0, j.GetInt("rerollSlag", 0));
            t.CombineSlag = Math.Max(0, j.GetInt("combineSlag", 0));
            t.DropWeight = Math.Max(0, j.GetInt("dropWeight", 10));
            t.DropWeightPerStageIndex = j.GetFloat("dropWeightPerStageIndex", 0f);
            return t;
        }
    }

    /// <summary>
    /// One attunement threshold for one aspect (DESIGN-ARMOR section 6): "an Ember hero wearing at
    /// least 2 Ember pieces gets these bonuses". Thresholds are CUMULATIVE, not exclusive - a hero
    /// at 4 matching pieces receives the 2-piece entry and the 4-piece entry both - so the 4-piece
    /// entry lists only what it adds on top.
    ///
    /// Bonuses are ArmorRollDef with Min == Max. That is deliberate reuse rather than a fixed-value
    /// type of its own: it makes it structurally impossible for a set bonus to express anything a
    /// substat could not, which is the whole "no unique effects, ever" guarantee restated for the
    /// set layer.
    /// </summary>
    public sealed class ArmorAttunementDef
    {
        public Aspect Aspect = Aspect.Ember;
        public int Pieces = 2;
        public string Name = "";
        public readonly List<ArmorRollDef> Bonuses = new List<ArmorRollDef>();

        public static ArmorAttunementDef FromJson(JsonValue j)
        {
            if (j == null || j.IsNull) return null;
            ArmorAttunementDef a = new ArmorAttunementDef();
            a.Aspect = EnumUtil.Parse(j.GetString("aspect", "Ember"), Aspect.Ember);
            a.Pieces = Math.Max(1, j.GetInt("pieces", 2));
            a.Name = j.GetString("name", "");
            List<JsonValue> bonuses = j.GetList("bonuses");
            for (int i = 0; i < bonuses.Count; i++)
            {
                ArmorRollDef b = ArmorRollDef.FromJson(bonuses[i]);
                if (b != null) a.Bonuses.Add(b);
            }
            return a;
        }
    }

    /// <summary>
    /// Everything armor.json carries. Deliberately NOT a per-item catalogue the way relics.json is:
    /// an armor piece is generated, not authored, so what content owns is the roll tables, the
    /// naming pools and the prices. There is no ArmorDef for an individual piece and there is no
    /// id to reference, which is also why a saved piece stores its resolved values rather than a
    /// reference into here (DESIGN-ARMOR section 10).
    ///
    /// A build with no armor.json loads an empty config, which ArmorRoller treats as "this build
    /// cannot roll armor" - the same shape relics.json and events.json already have.
    /// </summary>
    public sealed class ArmorConfig
    {
        public readonly List<ArmorRollDef> PrimaryRolls = new List<ArmorRollDef>();
        public readonly List<ArmorRollDef> SubRolls = new List<ArmorRollDef>();
        public readonly List<ArmorTemperDef> Tempers = new List<ArmorTemperDef>();
        public readonly List<ArmorAttunementDef> Attunements = new List<ArmorAttunementDef>();

        /// <summary>Flavour names per slot, drawn from at roll time. Purely cosmetic - a piece's
        /// behaviour is its slot, temper, aspect and rolls, never its name.</summary>
        public readonly Dictionary<ArmorSlot, List<string>> SlotNames = new Dictionary<ArmorSlot, List<string>>();

        /// <summary>Chance, 0..1, that a full-purse stage clear drops a piece at all. One at the
        /// default so an unauthored armor.json is obvious rather than silently stingy.</summary>
        public float DropChance = 1f;

        /// <summary>True once the config carries enough to roll a piece. Everything downstream tests
        /// this instead of testing four collections for emptiness in four places.</summary>
        public bool IsUsable
        {
            get { return PrimaryRolls.Count > 0 && SubRolls.Count > 0 && Tempers.Count > 0; }
        }

        public ArmorTemperDef GetTemper(ArmorTemper temper)
        {
            for (int i = 0; i < Tempers.Count; i++) if (Tempers[i].Temper == temper) return Tempers[i];
            return null;
        }

        /// <summary>The primary roll range for a slot, resolved through the slot's guaranteed stat
        /// rather than by looking a slot up - the guarantee lives in ArmorInfo, and this reads it
        /// rather than restating it.</summary>
        public ArmorRollDef GetPrimaryRoll(ArmorSlot slot)
        {
            ArmorStat wanted = ArmorInfo.PrimaryStat(slot);
            for (int i = 0; i < PrimaryRolls.Count; i++) if (PrimaryRolls[i].Stat == wanted) return PrimaryRolls[i];
            return null;
        }

        public List<string> NamesFor(ArmorSlot slot)
        {
            List<string> names;
            return SlotNames.TryGetValue(slot, out names) ? names : null;
        }

        /// <summary>
        /// Every attunement entry an aspect has at or below a matching-piece count, in ascending
        /// threshold order. Cumulative by construction: the caller applies all of them, which is
        /// what makes the 4-piece entry an addition to the 2-piece one rather than a replacement.
        /// </summary>
        public List<ArmorAttunementDef> AttunementsFor(Aspect aspect, int matchingPieces)
        {
            List<ArmorAttunementDef> result = new List<ArmorAttunementDef>();
            for (int i = 0; i < Attunements.Count; i++)
            {
                ArmorAttunementDef a = Attunements[i];
                if (a.Aspect == aspect && a.Pieces <= matchingPieces) result.Add(a);
            }
            result.Sort(delegate (ArmorAttunementDef x, ArmorAttunementDef y) { return x.Pieces.CompareTo(y.Pieces); });
            return result;
        }

        public static ArmorConfig FromJson(JsonValue j)
        {
            ArmorConfig c = new ArmorConfig();
            if (j == null || j.IsNull) return c;

            c.DropChance = j.GetFloat("dropChance", 1f);
            if (c.DropChance < 0f) c.DropChance = 0f;
            if (c.DropChance > 1f) c.DropChance = 1f;

            List<JsonValue> primaries = j.GetList("primaryRolls");
            for (int i = 0; i < primaries.Count; i++)
            {
                ArmorRollDef r = ArmorRollDef.FromJson(primaries[i]);
                if (r != null) c.PrimaryRolls.Add(r);
            }

            List<JsonValue> subs = j.GetList("subRolls");
            for (int i = 0; i < subs.Count; i++)
            {
                ArmorRollDef r = ArmorRollDef.FromJson(subs[i]);
                if (r != null) c.SubRolls.Add(r);
            }

            List<JsonValue> tempers = j.GetList("tempers");
            for (int i = 0; i < tempers.Count; i++)
            {
                ArmorTemperDef t = ArmorTemperDef.FromJson(tempers[i]);
                if (t != null && c.GetTemper(t.Temper) == null) c.Tempers.Add(t);
            }

            List<JsonValue> attunements = j.GetList("attunements");
            for (int i = 0; i < attunements.Count; i++)
            {
                ArmorAttunementDef a = ArmorAttunementDef.FromJson(attunements[i]);
                if (a != null) c.Attunements.Add(a);
            }

            JsonValue names = j["slotNames"];
            if (names != null && names.IsObject)
            {
                foreach (string key in names.Keys)
                {
                    ArmorSlot slot;
                    try { slot = (ArmorSlot)Enum.Parse(typeof(ArmorSlot), key, true); }
                    catch (Exception) { continue; }   // an unknown slot key is dropped, not defaulted
                    List<string> pool = new List<string>();
                    List<JsonValue> entries = names.GetList(key);
                    for (int i = 0; i < entries.Count; i++)
                    {
                        string n = entries[i].AsString(null);
                        if (!string.IsNullOrEmpty(n)) pool.Add(n);
                    }
                    if (pool.Count > 0) c.SlotNames[slot] = pool;
                }
            }

            return c;
        }
    }

    /// <summary>
    /// A flat bundle of currency an event can hand out - a milestone claim or a currency-ladder
    /// redemption, same shape either way. Deliberately as small as StageDef's own reward fields:
    /// no hero grants, no relic grants, nothing content does not already know how to apply. All
    /// three fields default to zero, and IsEmpty exists so content validation can catch an entry
    /// that would silently hand out nothing.
    /// </summary>
    public sealed class EventReward
    {
        public int Gold;
        public int Shards;
        public int RelicDust;

        public bool IsEmpty { get { return Gold <= 0 && Shards <= 0 && RelicDust <= 0; } }

        /// <summary>Player-facing summary, e.g. "200 Gold, 50 Shards" - built rather than authored,
        /// same reasoning as RelicDef.DescribeStats: one source, so a card and a claim confirmation
        /// cannot drift into describing the same reward two different ways.</summary>
        public string Describe()
        {
            List<string> parts = new List<string>();
            if (Gold > 0) parts.Add(Gold + " Gold");
            if (Shards > 0) parts.Add(Shards + " Shards");
            if (RelicDust > 0) parts.Add(RelicDust + " Relic Dust");
            return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "Nothing";
        }

        public static EventReward FromJson(JsonValue j)
        {
            EventReward r = new EventReward();
            if (j == null || j.IsNull) return r;
            r.Gold = j.GetInt("gold", 0);
            r.Shards = j.GetInt("shards", 0);
            r.RelicDust = j.GetInt("relicDust", 0);
            return r;
        }
    }

    /// <summary>One rung of an event's points ladder: reach Threshold cumulative points, claim
    /// Reward once. Threshold is the rung's own identity - GameSession.ClaimEventMilestone and the
    /// save format both key off it rather than a list index, so re-ordering milestones in content
    /// never repoints an already-claimed entry at a different reward.</summary>
    public sealed class EventMilestone
    {
        public long Threshold;
        public EventReward Reward = new EventReward();
    }

    /// <summary>One rung of an event's optional currency ladder: spend Cost of the event's own
    /// currency, receive Reward. Unlike a milestone this is a genuine purchase - the currency is
    /// deducted - so nothing tracks whether a tier was redeemed before; redeeming it again is a
    /// second purchase, not a double-claim, and simply costs the currency a second time.</summary>
    public sealed class EventCurrencyTier
    {
        public long Cost;
        public EventReward Reward = new EventReward();
    }

    /// <summary>
    /// One limited-time event: a UTC window (same startUnix/endUnix style as
    /// BalanceConfig.DailyResetHourUtc, but an absolute pair rather than an hour-of-day), one
    /// EventActivityKind it earns points from, a milestone ladder every event has, and an optional
    /// event-specific currency with its own small redeemable ladder.
    ///
    /// Content, not code: a new event is a new entry in events.json, never a new class or a new
    /// case anywhere in GameSession. The two starter events (The Recollection Rite,
    /// The Ledger's Week) are both ordinary EventDef entries - nothing about either is special-cased.
    /// </summary>
    public sealed class EventDef
    {
        public string Id = "";
        public string Name = "";

        /// <summary>UTC Unix seconds. Points are earned only while IsActive; a milestone or
        /// currency tier already earned stays claimable after EndUnix passes - see GameSession.</summary>
        public long StartUnix;
        public long EndUnix;

        public EventActivityKind Activity = EventActivityKind.SummonPerformed;

        /// <summary>Display name for the point currency this event's ladder climbs, e.g.
        /// "Memory Fragments" - never hardcoded as "points" anywhere UI-facing.</summary>
        public string PointsLabel = "Points";

        /// <summary>Points earned per unit of Activity's own amount - 1 for a per-occurrence
        /// activity like SummonPerformed, or a rate against a quantity like RelicDustSpent.</summary>
        public float PointsPerActivity = 1f;

        public readonly List<EventMilestone> Milestones = new List<EventMilestone>();

        /// <summary>Empty means this event has no currency - a pure point ladder, per The
        /// Recollection Rite. Non-empty is both the display name and the flag that HasCurrency
        /// reads, so there is exactly one source of truth for whether the currency half exists.</summary>
        public string CurrencyName = "";

        /// <summary>Currency earned per unit of Activity's own amount, at the same dispatch point
        /// and the same time as Points - both accrue from one RecordEventActivity call, at their
        /// own independent rates. Meaningless while CurrencyName is empty.</summary>
        public float CurrencyPerActivity = 0f;

        public readonly List<EventCurrencyTier> CurrencyRewards = new List<EventCurrencyTier>();

        public bool HasCurrency { get { return !string.IsNullOrEmpty(CurrencyName); } }

        public bool IsActive(long nowUnix) { return nowUnix >= StartUnix && nowUnix < EndUnix; }
        public bool HasEnded(long nowUnix) { return nowUnix >= EndUnix; }
        public bool HasNotStarted(long nowUnix) { return nowUnix < StartUnix; }

        public EventMilestone FindMilestone(long threshold)
        {
            for (int i = 0; i < Milestones.Count; i++) if (Milestones[i].Threshold == threshold) return Milestones[i];
            return null;
        }

        public EventCurrencyTier FindCurrencyTier(long cost)
        {
            for (int i = 0; i < CurrencyRewards.Count; i++) if (CurrencyRewards[i].Cost == cost) return CurrencyRewards[i];
            return null;
        }

        /// <summary>The highest milestone threshold this event's points can ever reach a claim
        /// against - the ladder's ceiling, for a progress bar that wants a denominator.</summary>
        public long HighestThreshold
        {
            get { return Milestones.Count > 0 ? Milestones[Milestones.Count - 1].Threshold : 0; }
        }

        public static EventDef FromJson(JsonValue j)
        {
            if (j == null || j.IsNull) return null;
            EventDef e = new EventDef();
            e.Id = j.GetString("id", "");
            e.Name = j.GetString("name", "Event");
            e.StartUnix = j.GetLong("startUnix", 0);
            e.EndUnix = j.GetLong("endUnix", 0);
            e.Activity = EnumUtil.Parse(j.GetString("activity", "SummonPerformed"), EventActivityKind.SummonPerformed);
            e.PointsLabel = j.GetString("pointsLabel", "Points");
            e.PointsPerActivity = j.GetFloat("pointsPerActivity", 1f);

            List<JsonValue> milestones = j.GetList("milestones");
            for (int i = 0; i < milestones.Count; i++)
            {
                JsonValue mj = milestones[i];
                EventMilestone m = new EventMilestone();
                m.Threshold = mj.GetLong("threshold", 0);
                m.Reward = EventReward.FromJson(mj);
                e.Milestones.Add(m);
            }

            e.CurrencyName = j.GetString("currencyName", "");
            e.CurrencyPerActivity = j.GetFloat("currencyPerActivity", 0f);

            List<JsonValue> currencyRewards = j.GetList("currencyRewards");
            for (int i = 0; i < currencyRewards.Count; i++)
            {
                JsonValue cj = currencyRewards[i];
                EventCurrencyTier t = new EventCurrencyTier();
                t.Cost = cj.GetLong("cost", 0);
                t.Reward = EventReward.FromJson(cj);
                e.CurrencyRewards.Add(t);
            }

            return e;
        }
    }

    /// <summary>Every tuning number the prototype uses, in one editable place.</summary>
    public sealed class BalanceConfig
    {
        public float DefenseConstant = 320f;
        public float DefensePerLevel = 18f;
        public int EnergyMax = 100;
        public int EnergyPerBasicAttack = 28;
        public int EnergyOnDamaged = 9;
        public int MaxRounds = 30;
        public float DamageVariance = 0.06f;
        public float MinDamage = 1f;
        public float SpeedVariance = 0.08f;

        /// <summary>Gates BOTH sides of a battle: player deploys and enemy spawns. Keep equal to PlayerProfile.TeamSize.</summary>
        public int MaxTeamSize = 5;

        /// <summary>
        /// Relic slots on one hero (DESIGN-RELICS "Equipping"). One, and the document argues the
        /// number rather than picking it: more than one slot stacks rule changes into a build, and
        /// a team-wide slot would be an aura, which is a global multiplier, which is the stat stick
        /// the design exists to avoid.
        ///
        /// DESIGN-RELICS also specifies a second balance key, relicEnergyBankMax, as "the ceiling
        /// on the Metronome's carry so it cannot spiral". It is deliberately absent: Tidewrought
        /// Metronome is cut (DESIGN-HOOK-RESOLUTION section 2), there is no bank to bound, and a
        /// tuning key that no code reads is worse than no key - it reads as a live lever.
        /// </summary>
        public int RelicSlotsPerHero = 1;

        /// <summary>
        /// Fraction of the damage Mirrored Wound returns to the attacker.
        ///
        /// Lives here rather than in the relic's text because a LEGENDARY relic contains no number
        /// (DESIGN-RELICS): the card says "part of that damage returns", this constant says how
        /// much.
        /// Named for the relic rather than for the grade, so a second reflecting relic gets its own
        /// constant instead of silently inheriting this one's tuning.
        ///
        /// Placeholder, like every other number the relic pass ships - task 13's retune owns the
        /// real value.
        /// </summary>
        public float MirroredWoundReflectFraction = 0.25f;

        // hero levelling
        public float LevelCostA = 55f;
        public float LevelCostB = 26f;
        public float LevelCostC = 3.2f;
        public int HeroLevelCapBase = 12;
        public int HeroLevelCapPerPlayerLevel = 4;
        public int HeroLevelHardCap = 80;

        // account levelling
        public float PlayerXpBase = 120f;
        public float PlayerXpExponent = 1.35f;

        // idle income
        public float IdleGoldPerHourFloor = 140f;
        public float IdleShardsPerHourFloor = 7f;
        public float IdleCapHours = 8f;

        // replay economy - see DESIGN-REPLAY-ECONOMY.md
        /// <summary>Repeat clears of an already-cleared stage that still pay the full purse,
        /// per stage per UTC day. A player's first clear of a stage is never counted here.</summary>
        public int RepeatFullClearsPerDay = 3;
        /// <summary>Fraction of the purse a repeat clear pays once the allowance is spent.
        /// Zero by design: any positive floor still out-earns the idle cache many times over.</summary>
        public float RepeatRewardFloor = 0f;
        /// <summary>Hour of the UTC day the per-stage repeat counters roll over.</summary>
        public int DailyResetHourUtc = 4;

        // relic progression - see DESIGN-RELIC-PROGRESSION.md
        /// <summary>
        /// Relic Dust paid by a repeat clear of the campaign's first stage. Later stages add
        /// RelicDustPerStageIndex per step of StageDef.GlobalIndex, which is the difficulty
        /// scaling the design doc asks for and leaves to tuning.
        /// </summary>
        public int RelicDustPerRepeatClear = 6;

        /// <summary>Extra Relic Dust per step of StageDef.GlobalIndex.</summary>
        public int RelicDustPerStageIndex = 2;

        /// <summary>
        /// Highest tier a relic can be owned at. Pinned to 1 until the balance pass authors
        /// tier 2; nothing in the simulation branches on tier, so raising this alone changes
        /// no relic's behaviour.
        /// </summary>
        public int RelicMaxTier = 1;

        /// <summary>Relic Dust cost to purchase one LEGENDARY-grade relic from the shop.</summary>
        public int RelicShopPrice = 80;

        /// <summary>
        /// Relic Dust cost to purchase one MYTHIC-grade relic from the shop. Deliberately steep and
        /// deliberately Dust-only - no second currency track - so a mythic relic reads as a long
        /// grinding goal rather than a purchase, per DESIGN-RELICS' Mythic pricing section. At
        /// today's repeat-clear rates this is roughly 2-4 weeks of normal, single-stage daily
        /// farming, not a five-minute buy. A first-pass placeholder like every other relic number.
        /// </summary>
        public int RelicMythicShopPrice = 2400;

        // summoning
        public int SummonCost = 120;
        public float SummonWeight1 = 0f;
        public float SummonWeight2 = 0f;
        public float SummonWeight3 = 62f;
        public float SummonWeight4 = 29f;
        public float SummonWeight5 = 9f;
        public int DuplicateGold = 450;
        public int DuplicateShards = 30;

        // new account
        public int StartingGold = 1500;
        public int StartingShards = 260;

        // The Deep Recital - see DESIGN-DEEP-RECITAL.md. That document specifies an endless PvE
        // mode in fiction and shape (floor structure, aspect bands, a stamina-like gate, milestone
        // rewards) but explicitly leaves every number illustrative. It also names the gate
        // "stamina" (recitalStaminaMax/recitalStaminaRechargeHours) and describes a continuous
        // multi-floor run with no HP/energy reset between floors, ended by a wipe. What shipped
        // here is a different shape, decided in the session that built it: a persistent one-way
        // floor LADDER (PlayerProfile.RecitalHighestFloor is the permanent record, one floor is
        // attempted at a time, each attempt is its own fresh-HP battle exactly like every other
        // fight in the game) rather than a continuous run, and the gate is named Echoes rather than
        // Stamina - which collides with the document's own "echoes" (the hostile degraded patterns
        // you fight, not a resource you spend). Both are real, unreconciled conflicts with
        // DESIGN-DEEP-RECITAL.md, not oversights; see DESIGN-BUILD-ORDER's Deep Recital entry for
        // the full account. Every number below is a first-pass placeholder regardless of shape,
        // same discipline as Mythic relic pricing and the two starter events.

        /// <summary>Enemy level at floor 1. StatMath.ForEnemy(EnemyDef, level) needed no change to
        /// reach this - the level parameter alone already does everything floor-scaling needs.</summary>
        public int RecitalBaseLevel = 20;

        /// <summary>Enemy level added per floor climbed, on top of RecitalBaseLevel.</summary>
        public int RecitalLevelsPerFloor = 2;

        /// <summary>
        /// Extra enemy levels added once per completed 6-floor Aspect cycle (Ember, Tide, Verdant,
        /// Gale, Umbra, Lumen, repeat - RecitalGenerator.AspectCycleLength), stacked on top of the
        /// per-floor levels above. This is what makes difficulty step up at each cycle boundary
        /// rather than climbing at the same flat per-floor rate forever.
        /// </summary>
        public int RecitalBonusLevelsPerCycle = 3;

        /// <summary>Maximum Echoes banked at once. Regeneration pauses at this cap, the same shape
        /// idleCapHours already uses for idle income.</summary>
        public int RecitalEchoesMax = 5;

        /// <summary>Hours to regenerate one Echo. DESIGN-DEEP-RECITAL section 3's illustrative
        /// values (5 max, 4 hours) are kept as a starting point even though the resource's name and
        /// per-attempt shape changed.</summary>
        public float RecitalEchoesRechargeHours = 4f;

        // ---------------------------------------------------------------- The Proving (PvP)
        // Every value here is ILLUSTRATIVE and DESIGN-PVP says so in section 5 and section 9. They
        // are starting points chosen so the mode runs, not tuning - the balance pass that firms
        // them up needs the mode playable to have anything to measure. Kept out of task 13's
        // retuned surface entirely: nothing below touches campaign.json or any existing key.

        /// <summary>Maximum Brands stored. DESIGN-PVP section 5's illustrative 5.</summary>
        public int ProvingBrandsMax = 5;

        /// <summary>Hours to regenerate one Brand. DESIGN-PVP section 5's illustrative 3.</summary>
        public float ProvingBrandsRechargeHours = 3f;

        /// <summary>Base Valor for a Proving win, before the per-standing scaling below.</summary>
        public int ProvingRewardValorBase = 30;

        /// <summary>Extra Valor per point of standing on top of ProvingRewardValorBase.</summary>
        public int ProvingRewardValorPerStanding = 2;

        /// <summary>
        /// Fraction of the win reward a LOSS still pays (DESIGN-PVP section 6). Recital pays
        /// nothing on a loss, which is right for a ladder where losing means not advancing; The
        /// Proving is a repeatable sink for a regenerating resource, and paying zero would make a
        /// spent Brand read as a bug rather than a result. Illustrative 0.25.
        /// </summary>
        public float ProvingLossRewardFraction = 0.25f;

        /// <summary>Standing gained on a win.</summary>
        public int ProvingStandingPerWin = 3;

        /// <summary>Standing lost on a loss. Standing floors at zero and never goes negative.</summary>
        public int ProvingStandingPerLoss = 2;

        /// <summary>
        /// How many opponents at the start of a player's history are ghosts of their own past team
        /// rather than generated rosters (DESIGN-PVP section 4's bootstrap). Small on purpose: the
        /// ghost exists to cover the cold start, where standing is still 0 and the generator's
        /// power band has nothing meaningful to scale against.
        /// </summary>
        public int ProvingGhostBootstrapCount = 3;

        /// <summary>Base gold on any floor clear, before the per-floor scaling below.</summary>
        public int RecitalRewardGoldBase = 200;

        /// <summary>Extra gold per floor number on top of RecitalRewardGoldBase.</summary>
        public int RecitalRewardGoldPerFloor = 20;

        /// <summary>Base Ember Shards on any floor clear, before per-floor scaling.</summary>
        public int RecitalRewardShardsBase = 10;

        /// <summary>Extra shards per floor number on top of RecitalRewardShardsBase.</summary>
        public int RecitalRewardShardsPerFloor = 1;

        /// <summary>Every floor number divisible by this (and not also a deep milestone below)
        /// grants a Relic Dust bonus on top of the ordinary gold/shard reward.</summary>
        public int RecitalMilestoneInterval = 10;

        /// <summary>Relic Dust granted at a milestone floor, times the milestone's own number
        /// (floor / RecitalMilestoneInterval) - milestone 1 (floor 10) grants this much, milestone
        /// 2 (floor 20) grants double, and so on.</summary>
        public int RecitalMilestoneDustPerTier = 150;

        /// <summary>Every floor number divisible by this grants a guaranteed relic instead of the
        /// ordinary milestone's Dust bonus - "something rarer" at deep milestones, per spec. Falls
        /// back to a large Dust grant if every relic in content is already owned, so the reward is
        /// never a silent no-op.</summary>
        public int RecitalDeepMilestoneInterval = 50;

        /// <summary>The Relic Dust fallback when a deep milestone has no unowned relic left to
        /// grant.</summary>
        public int RecitalDeepMilestoneDustFallback = 1000;

        // ---- Guardians: boss-plus-escorts encounter on every Nth floor.
        // The spec names the interval as 10; the value lives here so it can be tuned alongside
        // the rest of the Recital curve rather than hardcoded in RecitalGenerator.

        /// <summary>Every floor divisible by this is a Guardian floor (boss + escorts at a
        /// raised level). The Aspect cycle is unbroken across Guardians.</summary>
        public int RecitalGuardianInterval = 10;

        /// <summary>Extra levels the Guardian boss gets above the floor's base level. The
        /// escorts stay at the base level, matching the campaign finales' shape.</summary>
        public int RecitalGuardianBossLevelBonus = 5;

        // ---- Coda: The Deep Recital's own currency, isolated from Gold/Shards/Dust/Slag/Valor.
        // No sink yet; the purse exists so the accrual is visible and the save field is in place.
        // See DESIGN-DEEP-RECITAL.md for what it is expected to buy later.

        /// <summary>Base Coda earned per non-Guardian floor clear.</summary>
        public int RecitalCodaBase = 5;

        /// <summary>Extra Coda per floor number on top of RecitalCodaBase.</summary>
        public int RecitalCodaPerFloor = 1;

        /// <summary>Base Coda earned on a Guardian floor clear (replaces, not stacks with, the
        /// regular rate).</summary>
        public int RecitalCodaGuardianBase = 25;

        /// <summary>Extra Coda per floor number on a Guardian clear.</summary>
        public int RecitalCodaGuardianPerFloor = 3;

        public static BalanceConfig FromJson(JsonValue j)
        {
            BalanceConfig b = new BalanceConfig();
            if (j == null || j.IsNull) return b;
            b.DefenseConstant = j.GetFloat("defenseConstant", b.DefenseConstant);
            b.DefensePerLevel = j.GetFloat("defensePerLevel", b.DefensePerLevel);
            b.EnergyMax = j.GetInt("energyMax", b.EnergyMax);
            b.EnergyPerBasicAttack = j.GetInt("energyPerBasicAttack", b.EnergyPerBasicAttack);
            b.EnergyOnDamaged = j.GetInt("energyOnDamaged", b.EnergyOnDamaged);
            b.MaxRounds = j.GetInt("maxRounds", b.MaxRounds);
            b.DamageVariance = j.GetFloat("damageVariance", b.DamageVariance);
            b.SpeedVariance = j.GetFloat("speedVariance", b.SpeedVariance);
            b.MaxTeamSize = j.GetInt("maxTeamSize", b.MaxTeamSize);
            b.RelicSlotsPerHero = j.GetInt("relicSlotsPerHero", b.RelicSlotsPerHero);
            b.MirroredWoundReflectFraction = j.GetFloat("mirroredWoundReflectFraction", b.MirroredWoundReflectFraction);
            b.LevelCostA = j.GetFloat("levelCostA", b.LevelCostA);
            b.LevelCostB = j.GetFloat("levelCostB", b.LevelCostB);
            b.LevelCostC = j.GetFloat("levelCostC", b.LevelCostC);
            b.HeroLevelCapBase = j.GetInt("heroLevelCapBase", b.HeroLevelCapBase);
            b.HeroLevelCapPerPlayerLevel = j.GetInt("heroLevelCapPerPlayerLevel", b.HeroLevelCapPerPlayerLevel);
            b.HeroLevelHardCap = j.GetInt("heroLevelHardCap", b.HeroLevelHardCap);
            b.PlayerXpBase = j.GetFloat("playerXpBase", b.PlayerXpBase);
            b.PlayerXpExponent = j.GetFloat("playerXpExponent", b.PlayerXpExponent);
            b.IdleGoldPerHourFloor = j.GetFloat("idleGoldPerHourFloor", b.IdleGoldPerHourFloor);
            b.IdleShardsPerHourFloor = j.GetFloat("idleShardsPerHourFloor", b.IdleShardsPerHourFloor);
            b.IdleCapHours = j.GetFloat("idleCapHours", b.IdleCapHours);
            b.RepeatFullClearsPerDay = j.GetInt("repeatFullClearsPerDay", b.RepeatFullClearsPerDay);
            b.RepeatRewardFloor = j.GetFloat("repeatRewardFloor", b.RepeatRewardFloor);
            b.DailyResetHourUtc = j.GetInt("dailyResetHourUtc", b.DailyResetHourUtc);
            b.RelicDustPerRepeatClear = j.GetInt("relicDustPerRepeatClear", b.RelicDustPerRepeatClear);
            b.RelicDustPerStageIndex = j.GetInt("relicDustPerStageIndex", b.RelicDustPerStageIndex);
            b.RelicMaxTier = j.GetInt("relicMaxTier", b.RelicMaxTier);
            b.RelicShopPrice = j.GetInt("relicShopPrice", b.RelicShopPrice);
            b.RelicMythicShopPrice = j.GetInt("relicMythicShopPrice", b.RelicMythicShopPrice);
            b.SummonCost = j.GetInt("summonCost", b.SummonCost);
            b.SummonWeight1 = j.GetFloat("summonWeight1", b.SummonWeight1);
            b.SummonWeight2 = j.GetFloat("summonWeight2", b.SummonWeight2);
            b.SummonWeight3 = j.GetFloat("summonWeight3", b.SummonWeight3);
            b.SummonWeight4 = j.GetFloat("summonWeight4", b.SummonWeight4);
            b.SummonWeight5 = j.GetFloat("summonWeight5", b.SummonWeight5);
            b.DuplicateGold = j.GetInt("duplicateGold", b.DuplicateGold);
            b.DuplicateShards = j.GetInt("duplicateShards", b.DuplicateShards);
            b.StartingGold = j.GetInt("startingGold", b.StartingGold);
            b.StartingShards = j.GetInt("startingShards", b.StartingShards);
            b.RecitalBaseLevel = j.GetInt("recitalBaseLevel", b.RecitalBaseLevel);
            b.RecitalLevelsPerFloor = j.GetInt("recitalLevelsPerFloor", b.RecitalLevelsPerFloor);
            b.RecitalBonusLevelsPerCycle = j.GetInt("recitalBonusLevelsPerCycle", b.RecitalBonusLevelsPerCycle);
            b.RecitalEchoesMax = j.GetInt("recitalEchoesMax", b.RecitalEchoesMax);
            b.RecitalEchoesRechargeHours = j.GetFloat("recitalEchoesRechargeHours", b.RecitalEchoesRechargeHours);
            b.ProvingBrandsMax = j.GetInt("provingBrandsMax", b.ProvingBrandsMax);
            b.ProvingBrandsRechargeHours = j.GetFloat("provingBrandsRechargeHours", b.ProvingBrandsRechargeHours);
            b.ProvingRewardValorBase = j.GetInt("provingRewardValorBase", b.ProvingRewardValorBase);
            b.ProvingRewardValorPerStanding = j.GetInt("provingRewardValorPerStanding", b.ProvingRewardValorPerStanding);
            b.ProvingLossRewardFraction = j.GetFloat("provingLossRewardFraction", b.ProvingLossRewardFraction);
            b.ProvingStandingPerWin = j.GetInt("provingStandingPerWin", b.ProvingStandingPerWin);
            b.ProvingStandingPerLoss = j.GetInt("provingStandingPerLoss", b.ProvingStandingPerLoss);
            b.ProvingGhostBootstrapCount = j.GetInt("provingGhostBootstrapCount", b.ProvingGhostBootstrapCount);
            b.RecitalRewardGoldBase = j.GetInt("recitalRewardGoldBase", b.RecitalRewardGoldBase);
            b.RecitalRewardGoldPerFloor = j.GetInt("recitalRewardGoldPerFloor", b.RecitalRewardGoldPerFloor);
            b.RecitalRewardShardsBase = j.GetInt("recitalRewardShardsBase", b.RecitalRewardShardsBase);
            b.RecitalRewardShardsPerFloor = j.GetInt("recitalRewardShardsPerFloor", b.RecitalRewardShardsPerFloor);
            b.RecitalMilestoneInterval = j.GetInt("recitalMilestoneInterval", b.RecitalMilestoneInterval);
            b.RecitalMilestoneDustPerTier = j.GetInt("recitalMilestoneDustPerTier", b.RecitalMilestoneDustPerTier);
            b.RecitalDeepMilestoneInterval = j.GetInt("recitalDeepMilestoneInterval", b.RecitalDeepMilestoneInterval);
            b.RecitalDeepMilestoneDustFallback = j.GetInt("recitalDeepMilestoneDustFallback", b.RecitalDeepMilestoneDustFallback);
            b.RecitalGuardianInterval = j.GetInt("recitalGuardianInterval", b.RecitalGuardianInterval);
            b.RecitalGuardianBossLevelBonus = j.GetInt("recitalGuardianBossLevelBonus", b.RecitalGuardianBossLevelBonus);
            b.RecitalCodaBase = j.GetInt("recitalCodaBase", b.RecitalCodaBase);
            b.RecitalCodaPerFloor = j.GetInt("recitalCodaPerFloor", b.RecitalCodaPerFloor);
            b.RecitalCodaGuardianBase = j.GetInt("recitalCodaGuardianBase", b.RecitalCodaGuardianBase);
            b.RecitalCodaGuardianPerFloor = j.GetInt("recitalCodaGuardianPerFloor", b.RecitalCodaGuardianPerFloor);
            return b;
        }
    }
}
