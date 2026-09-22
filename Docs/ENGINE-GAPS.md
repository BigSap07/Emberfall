# Emberfall - engine gaps

**What this file is.** One row per place where a sentence Pinoy wrote in `import/Heroes.json`
cannot be said by the engine, together with what the shipped kit does instead. It is the running
account behind every "draft, Pinoy to confirm" note the 4-star authoring batches have produced, and
it exists so the same gap is not rediscovered once per batch.

**It is a record, not a work order.** Nothing here is scheduled. A row is a fact about the engine on
the date the row was written, and the authoring batches are explicitly forbidden from closing these
by adding engine features: a batch that meets one of these approximates it, marks the clause draft
and adds a row here.

**Rows are grouped by the capability that is missing**, not by hero, because that is the only
arrangement in which a repeat is visible. Eleven of the twenty-two groups below have more than one
occupant, and the largest has five.

**What is deliberately not here.** Numbers. Every authored 4-star's damage and healing values differ
from Pinoy's originals because `CheckStarBandByRole` moved them, and that is balance rather than
expressibility. Kiva III's wording is also out: its flag is a design change awaiting confirmation,
not a sentence the engine could not say. Two rows that appeared in the batch-two report are gone
because they were never gaps: that report's scan of `PassiveTrigger` found 46 of its 52 members and
dropped `RoundStart` and `AfterActiveCast`, so Celen's "at round start" and Siofra's second hook
were both authorable all along and are now authored.

**How to read the size column.** Small is a field on an existing type plus the code that reads it.
Medium is a new enum member with its own resolution and its own harness arena. Large touches the
damage funnel or the turn loop, which is where every fingerprint in the project lives.

---

## 6. Comparing a resource across two named units

`ConditionSubject` has seven members - Target, Caster, Defeated, BroadcastSubject, LastAttacker,
LastTarget, BroadcastSource - and none of them names "the other unit this effect is also about".
`EnergyAtLeast` and `EnergyBelow` each read one subject, so a gate on the GAP between two allies
would have to be two conditions on two subjects, which can disagree and then create or destroy the
resource.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Celen | "transfer 15 Energy from the highest-Energy ally to the lowest **if their difference is at least 30**" | a condition reading two units at once | the pair is ungated, and self-cancels when the fullest and emptiest bars are the same body | medium |

---

## 7. Negating a target mode

`EnemyWithMyMark` names a marked enemy and there is no negation of it. `LacksMyMark` exists as a
CONDITION, so "hit every enemy WITHOUT my mark" is authorable through `AllEnemies` plus a condition;
what is not authorable is a SINGLE unmarked enemy as a destination, because a `secondaryTarget`
carries no condition.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Mika | "moves each effect to **an unmarked enemy**" | a single-target mode for an enemy not carrying the caster's mark | Loose Parts moves to the highest-HP enemy, the answer Demos's No Appeal already gives | small |

---

## 8. Remembering who hit MY SIDE, rather than who hit ME

`LastAttackerId` is per-unit and means "the body that last hit me". Nothing keeps the same memory for
a side, so "the enemy that hurt one of us" has neither a target mode nor a condition.
`HighestDamageDealtEnemy` is the nearest standing answer and is a career total rather than a recent
act.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Maera | "if the target **attacked an ally** since Maera's last turn, deals 25% additional" | a condition asking whether the subject dealt damage to the caster's side | `SubjectActedThisRound`, which in this engine's turn shape means it has attacked | medium |
| Maera | "the enemy that **most recently damaged an ally**" | a target mode for the side's last attacker | `HighestDamageDealtEnemy`, the enemy that has dealt the most damage so far | medium |

---

## 10. Counting firings per subject on an owner-centric trigger

`FiresPerSubject` and the `PassiveFiredOnSubjectAtLeast` condition are the engine's answer to "the
second time this happens to one body", and both are charged against `_broadcastSubject`. An
owner-centric trigger has no broadcast subject, so the counter never increments and the condition
always reads zero.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Jory | "If the **same enemy hits Jory twice** in a round, he counterattacks" | a per-subject firing count on an owner-centric trigger such as `OnDamaged` | `OnDamaged` limited to once a round, so the first enemy to strike him is counterattacked | small |

---

## 11. Swapping a value, rather than moving it one way

Every transfer primitive in the engine is one-directional. `HpTransfer` and `ShieldTransfer` move a
pool from one body to another and there is no exchange.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Rhys | "**swaps HP percentage** with the lowest-HP ally if they are below 50%" | a two-way HP exchange; `HpTransfer` only moves HP one way | Rhys pays 45% of what the ally is missing out of his own bar, clamped by the engine to leave him at 1 HP | medium |

---

## 12. Amounts written as a percentage of the HOLDER's health

One row left, and it is no longer an expressibility gap. `holderHpTick` pays a Regen or Burn as a
fraction of its CARRIER's bar, read fresh at every tick, which is what Maren's clause asked for and
what closed her row.

**Demos's row stays, and what it records has changed.** The clause is authorable: a Damage effect
with `targetCurrentHpRatio` says "4% of its current HP" exactly, and the percentage is now Pinoy's
4% where the shipped kit carried 7%. What cannot go is the `attackCap` Pinoy's sentence does not
mention. Removing it was tried and measured: Demos reads **x3.40** of his role's 5-star median
against **x0.28** with the bound in place, because a percentage of a current bar grows with the
board while a hero's Attack does not. That is not a number a retune reaches - it is a different
hero.

So the departure is real and it is a BALANCE one rather than a missing capability, and no engine
feature closes it. A clause of this shape needs either a bound the text does not name or a roster
whose bars do not outrun a 4-star's Attack, and choosing between those is Pinoy's call.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Demos | "At turn end, Arrears loses 4% current HP" | nothing - the clause is authorable, and its unbounded form is what the band cannot carry | the percentage is Pinoy's, and an `attackCap` of 105% of Attack that his sentence does not mention bounds it | none |

---

## 13. Statuses the enum does not have

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Maera | "and **she cannot be controlled**" | a status that raises the `ControlImmunity` stat; `StatusKind` has no `ControlImmunityUp`, and the stat is authored on `UnitDef` only | the clause is dropped; the guaranteed critical half of the passive is authored and the immunity half is not | small |

---

## 15. Chance on anything but a status landing

`EffectDef.Chance` is read in exactly one place, the `ApplyStatus` landing roll, and nowhere else.
Damage, healing, energy and turn-shift effects either happen or do not.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Finn | "have a **35% chance** to be delayed by 15% turn progress" | a probability gate on a non-status effect | the delay is certain. It used to be held to once a round as well, which stood in for the chance; that limit went when `RoundEnd` closed group 14, so the clause is now certain against every Ringing enemy once a round | small |

---

## 16. Redirecting an enemy onto somebody other than the caster

`Provoked` constrains one attacker's single-enemy targeting onto whoever applied it, which the enum
calls the opposite direction to `Taunt` and a genuinely different rule. Neither can send a named
enemy at a named third body.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Niko | "forces its next Basic Attack onto **a random ally**" | a redirect naming a destination other than the applier | `Provoked`, so the blow lands on Niko | medium |

---

## 18. Telling WHICH KIND of status a removal took

Both triggers in this group now publish the body the event happened to, so Tamsin's row is gone
and Lio's has narrowed to its second clause.

Nothing says what a removal REMOVED. `OnStatusRemoved` names the remover and now the body it was
taken from, and `_lastRemovedValue` carries the pending DoT damage that left with it, but no field
and no condition carries the KIND - so a rider that is supposed to answer only a control debuff
answers every debuff. The nearest shapes are `TriggerStatus`, which filters the EXPIRY triggers
against `_expiredKinds`, and `SubjectLostStatusThisFiring`, which answers whether anything was
taken at all.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Lio | "**control debuffs** also grant 10 Energy" | the kind a removal took, published for the trigger it raises - the removal-side sibling of `PassiveDef.TriggerStatus` | the 10 Energy pays out for any debuff Lio strips, not only a control one | small |

---

## 19. Negating a whole-class condition

`LacksStatus` and `LacksMyMark` negate a NAMED status. `HasAnyBeneficialStatus` and
`HasAnyHarmfulStatus` have no negation, and neither does `SubjectLostStatusThisFiring`, so "if
nothing was removed" cannot gate the fallback that answers it.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Iolan | "removes one target buff; **if none is removed**, applies 10% Effect Hit Down" | the negation of `HasAnyBeneficialStatus` or of `SubjectLostStatusThisFiring` | the rider lands whether or not the Dispel found anything | small |

---

## 20. An information layer

The battle screen is a player for a complete event record and already knows everything about every
unit, so "reveal it to allies" has nothing to change. The clause is cosmetic in this engine.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Leon | "assigns Case Number ..., **revealing current HP, Energy, and buffs to allies**" | per-unit visibility, which the game does not model | carried in the text only; the Mark does the mechanical half | none |

---

## 21. Banning the gain of ONE named status

`BuffSuppression` refuses every beneficial status at once and `StatusGainSuppression` refuses
Resolve. Nothing refuses a single named kind, so "cannot become Concealed" has to be either far
wider than written or answered after the fact.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Nadine | "Registered targets **cannot become Concealed or Untargetable**" | a per-status gain ban | Proper Place strips the concealment the moment it lands instead of preventing it | small |

---

## 22. Position, and anything that would hold a unit in one

`SwapPositions` is the whole of movement in this engine and it swaps a unit with a random body on
its own side. There is no placement, no ordering and nothing that forbids either.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Nadine | "marks the target position for 2 rounds. **It cannot move**" | a movement lock, and a movement system for it to lock | the clause is dropped; Hold the Line keeps its damage rider | small |
| Eira | "moves the target **to the back** if another enemy is alive" | placement at a named slot; `SwapPositions` picks the partner at random | `SwapPositions` on the target, which displaces it but not reliably to the back | small |

---

## Where the counts stand

Twenty-two capabilities, thirty-nine clauses, across twenty heroes and four authoring batches, which
is the whole of the 4-star tier plus the four 5-stars whose text met the same walls. Eleven groups
have more than one occupant:

| group | capability | clauses |
|---|---|---|
| 8 | remembering who hit MY SIDE rather than who hit ME | 2 |
| 22 | position, and anything that would hold a unit in one | 2 |

**Group 9 is closed.** Two of its five rows were the same exclusion in `OnEnemyDamagedAlly`, met
twice by Branik and Halwen and answered twice by pasting the passive onto `OnDamaged` as well;
`PassiveDef.IncludeSelf` deleted both overflow passives from the shipped roster. The other three
were moments nothing raised at all, and are now `OnAllyEnergyGained`, `OnGuardPrevented` and
`OnEnemyAppliedStatus`. Group 4 was the next best value: four clauses across four heroes all asking
for a per-source stat modifier, which is one status kind beside `Vulnerable` pointing the other
way - and it turned out to need the entry on the OTHER body, because no effect can stamp a
`SourceId` that is not its own caster.

**Groups 20 and 22 are different in kind and should not be read as debt.** An information layer and
a movement system are games this one is not; the clauses that ask for them are two sentences out of
thirty-nine, and both heroes work without them.

One caution, from the pass that produced several of these rows. `MaxHpCap` reads like the ceiling a
percent-of-HP clause wants and is not: it ceilings against the TARGET's Max HP, so `0.02` of Max HP
capped at `1.0` of Max HP is uncapped. `AttackCap` is the one that bounds an amount in the caster's
own currency. Arden carried five of the first and none of the second and read seven and a half times
the roster median in the sweep arena until they were moved across; `no shipped effect sets maxHpCap`
is a standing guard again because of it.
