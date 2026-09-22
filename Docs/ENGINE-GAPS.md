# Emberfall - engine gaps

**What this file is.** One row per place where a sentence Pinoy wrote in `import/Heroes.json`
cannot be said by the engine, together with what the shipped kit does instead. It is the running
account behind every "draft, Pinoy to confirm" note the 4-star authoring batches have produced, and
it exists so the same gap is not rediscovered once per batch.

**It is a record, not a work order.** Nothing here is scheduled. A row is a fact about the engine on
the date the row was written, and the authoring batches are explicitly forbidden from closing these
by adding engine features: a batch that meets one of these approximates it, marks the clause draft
and adds a row here. An ENGINE pass may close them, and one has - eleven groups and twenty-five
clauses went that way, each as its own capability with its own harness arena, and then each affected
kit was put back to the sentence Pinoy wrote for it. A row leaves this file only when the kit that
carried it has been restored, never when the capability alone is built.

**Rows are grouped by the capability that is missing**, not by hero, because that is the only
arrangement in which a repeat is visible. That arrangement did its job: eleven of the original
twenty-two groups had more than one occupant, and the engine pass that closed the cheap repeated
ones took every group with two or more, leaving thirteen groups and fourteen clauses. Exactly one
of those groups still has a repeat, and it is the movement one, which is out of scope by design.

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

`HpTransfer` also only ever moves HP BETWEEN THE CASTER AND ITS TARGET, so a clause naming two
bodies that are both somebody else cannot be said one way either.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Rhys | "**swaps HP percentage** with the lowest-HP ally if they are below 50%" | a two-way HP exchange; `HpTransfer` only moves HP one way | Rhys pays 20% of what the ally is missing out of his own bar, clamped by the engine to leave him at 1 HP | medium |
| Bren | "swaps the target's present HP percentage with the **highest-HP enemy**" | a two-way exchange, and a one-way one between two bodies neither of which is the caster | Take Another Route is aimed at the highest-HP enemy instead, so the blow goes where the swap would have sent the health | medium |

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

## 23. A damage modifier scoped to one kind of hit, or spent by one hit

`Vulnerable`, `Blunted` and `DamageTakenUp` are the three entries that change what a blow is worth,
and all three answer every blow for as long as they last. `DamageTakenUp` can narrow itself to
skill damage, which is the only scope any of them has. Nothing narrows one to a CRITICAL hit, and
nothing spends one on a single blow: `charges` is stamped on a status as it lands and is read by
`Guard` and by the `Covered` redirect, not by the damage modifiers.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Aster | "Fracture causes the **next critical hit** to deal 15% more damage" | a crit-scoped damage modifier, and a per-hit charge on one | Fracture is a `DamageTakenUp` of 15% that answers every blow for its 2 rounds | small |
| Quin | "Delivered targets take 8% more damage from the **next** active skill" | a per-hit charge on a damage modifier; the skill scope itself is authorable | Delivered carries the skill-scoped 8% for its 2 rounds rather than for one cast | small |

---

## 24. A miss that somebody other than the dodger can hear

`OnDodge` fires on the body that was missed and publishes the attacker as its source, which is the
right shape for "when she evades, she counters". Nothing raises it on the applier of the accuracy
debuff that caused the miss, and no trigger reaches a third party at all.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Adric | "When an enemy with Lost Route **misses**, Adric gains 8 Energy" | a miss window the applier can hear, rather than only the body that dodged | the 8 Energy pays only when the missed blow was aimed at Adric himself | small |

---

## 25. An amount that counts the bodies an effect reached

`perStack` scales an effect by how many entries of a named status, or of a whole class, sit on one
named subject. Nothing scales an amount by HOW MANY TARGETS the effect resolved onto, so "per
target" has no arithmetic behind it. `_hitIsArea` records that a blow reached more than one body
and is a boolean, not a count.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Quin | "removes Delivered to give Quin **12 Energy per target**" | an amount multiplied by the number of bodies an effect resolved onto | a flat 12 Energy for the cast, whatever it reached | small |

---

## 26. Changing what an ability costs

`energyCost` is a field on `AbilityDef` and the one thing that moves it is a `TalentOverrideDef`,
which is applied at unit-build time from the SELECTED TALENT. A passive carries an `overrides`
list in its JSON and nothing reads it outside the talent path, so a hero below five stars has no
way to author a cost change at all.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Quin | "Quin's **first active skill each battle costs 15 less Energy**" | an ability cost modifier reachable from a passive rather than from a talent override | a `BattleStart` passive that banks 15 Energy, which is the same first cast one turn earlier and a different thing on every cast after it | small |

---

## 27. Refusing a cleanse

`Cleanse` walks the target's harmful entries newest first and takes the first `hits` of them.
Nothing on a `StatusInstance` and no condition anywhere says "not this one". `NoExtend` refuses an
`ExtendStatus` and is the only refusal an entry can carry.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Selka | "Overdue **cannot be cleansed** during the round it was applied" | a per-entry cleanse immunity | No Extension writes Overdue back onto its carrier at every round start, so it cannot be shaken off at all rather than for one round | small |

---

## 28. Which effect of a cast landed the blow

A defeat publishes the killer and the defeated body. It does not publish which EFFECT of the
killer's ability did it, so a clause that answers only one line of a multi-effect cast cannot tell
itself apart from the rest of the cast. `TriggerAbilityId` narrows a defeat trigger to one ABILITY,
which is as fine as the record gets.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Cal | "When **Offset splash** defeats an enemy, Cal gains a guaranteed Critical Hit" | the effect within a cast that landed a killing blow | any defeat Cal lands on an Offset enemy pays the guaranteed critical | small |

---

## 29. A debuff landing on an enemy

`OnEnemyBuffGained` is the window for a BENEFICIAL status arriving on the other side, and Vanno
and Tavia both build on it. There is no harmful mirror of it. `OnStatusGained` fires on the
CARRIER, so a hero watching the enemy line for an incoming debuff has nothing to listen to, and
`OnEnemyAppliedStatus` answers an enemy CASTING one rather than one arriving.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Linnet | "Crossed Line: the target's **first debuff each round** goes to a random enemy" | a window for a harmful status arriving on an enemy, the mirror of `OnEnemyBuffGained` | Open Circuit reroutes one debuff already on the Crossed Line body at round start instead of catching one as it lands | small |

---

## 30. A moved status is not recorded as a removal

`NoteRemovedFrom` is what `SubjectLostStatusThisFiring` reads and what `FireRemoval` raises
`OnStatusRemoved` on, and exactly three effects call it: `Cleanse`, `Dispel` and `RemoveStatus`.
`MoveDebuffs`, `MoveBuffs` and `TransferStatus` take an entry off a body without either, so a
clause that pays the bodies a move took something from cannot tell which ones they were.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Oswin | "**Allies who lose a debuff to Oswin** gain 8% Defense for 2 rounds" | a move recorded as a removal, so the gate has something to read | every ally gains the 8% after Reposition the Hurt, whether or not a debuff left them | small |
| Linnet | "**When Crossed Line redirects a debuff**, Linnet gains 10 Energy" | the same record, read as a gate on the payout rather than on a rider | the 10 Energy pays at round start whether or not the reroute found anything to move | small |

---

## 31. Negating a miss chance

The miss check subtracts `EvasionUp` on the defender and `AccuracyDown` on the attacker from a hit
chance of 1. Nothing sets it back to 1, and nothing exempts one attacker or one target from the
roll, so "cannot miss" has no term to write. Adric's Lost Route made this reachable for the first
time in the same batch that found it unanswerable.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Brio | "Attacks against Accounted **cannot miss**" | an exemption from the miss roll | carried in the text only; the Mark does the rest of Accounted's work | small |

---

## Where the counts stand

Twenty-two capabilities, twenty-six clauses, across twenty-two heroes. It began at twenty-two and
thirty-nine; the engine pass closed groups 1, 2, 3, 4, 5, 8, 9, 12, 14, 17 and 18 and restored the
eighteen kits that carried them, leaving thirteen and fourteen. The 3-star batches have added nine
groups and twelve clauses since, which is the count going the other way for the first time since
the pass.

Four groups have more than one occupant:

| group | capability | clauses |
|---|---|---|
| 11 | swapping a value, rather than moving it one way | 2 |
| 22 | position, and anything that would hold a unit in one | 2 |
| 23 | a damage modifier scoped to one kind of hit, or spent by one hit | 2 |
| 30 | a moved status is not recorded as a removal | 2 |

**Group 23 is the one with a repeat that arrived in a single batch**, which is what this file's
grouping exists to make visible: Aster's Fracture and Quin's Delivered are the same sentence
written twice, and one per-hit charge on the damage modifiers would close both.

**What the pass built**, in the order the groups were taken:

| group | what closed it |
|---|---|
| 9 | `PassiveDef.IncludeSelf`, and the `OnAllyEnergyGained`, `OnGuardPrevented` and `OnEnemyAppliedStatus` triggers |
| 4 | `StatusKind.Blunted`, `DamageTakenUp` with a skill scope, and `HealingReceivedUp` |
| 5 | `shieldTransferDuration`, `invertDuration` and `extendCap` |
| 12 | `holderHpTick`, which pays a Burn or Regen in its carrier's own bar |
| 14 | the `RoundEnd` trigger |
| 18 | a broadcast subject on `OnStatusRemoved` and `OnTurnDelayed` |
| 17 | `perStackClass`, `perStackMaxCount` and `perStackFromZero` on the existing scaler |
| 2 | `UsesPerRound` honoured by the Covered redirect and by a Guard |
| 3 | `areaHitsOnly` on a ward, and the two `IncomingHitIs...` conditions |
| 8 | `EnemyThatLastDamagedAlly` and `SubjectDamagedMySideSinceMyTurn` |
| 1 | `SubjectTookHitAboveMaxHpFraction`, and `HpTransfer` reading the `ScaleBy` family |

**Two rows survived their group's closure and are still here**, because each turned out to be about
something else. Demos's is a balance finding rather than a capability - group 12 records the
measurement. Lio's narrowed to its second clause, which is a different absence - group 18 was
retitled around it.

**Groups 20 and 22 are different in kind and should not be read as debt.** An information layer and
a movement system are games this one is not; the clauses that ask for them are three sentences out
of the fourteen left, and all three heroes work without them.

One caution, from the pass that produced several of these rows. `MaxHpCap` reads like the ceiling a
percent-of-HP clause wants and is not: it ceilings against the TARGET's Max HP, so `0.02` of Max HP
capped at `1.0` of Max HP is uncapped. `AttackCap` is the one that bounds an amount in the caster's
own currency. Arden carried five of the first and none of the second and read seven and a half times
the roster median in the sweep arena until they were moved across; `no shipped effect sets maxHpCap`
is a standing guard again because of it.
