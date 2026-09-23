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

**Pass 2, at the end of this file, asked a different question.** Pass one asked which sentences the
engine could not say. Pass 2 asked which shipped clauses the engine says wrongly - more or less often
than written, on the wrong body, against a limit nothing reaches - and measured each one. Its
findings that need a new capability joined the groups below; the rest are either repaired in the
engine or written up as content instructions in that section. **The content pass that followed
it** carried those instructions out, built G4 and G5, and closed groups 34, 36 and 37; what it
landed, what it rejected and what it found is the last part of that section. **Pass 3**, the
last section of this file, said the nine clauses that content pass confirmed and left, turned
`buffsOnly` on (C13), and corrected two of pass 2's readings that the record had wrong.

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
| Lydia | "Whenever Lydia **acts before the slowest enemy**, she gains 10% Damage Reduction" | a condition comparing the caster against a body other than the one the effect lands on; `CasterActsBeforeSubject` reads the target, and this effect targets Lydia | the Damage Reduction goes up at the start of every one of her turns, ungated | medium |

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
| Sena | "Sena gains **10% Critical Chance** against slowed enemies" | a status that raises Critical Chance; `StatusKind` carries `CritDamageUp` and `CritDamageDown` and has no Critical Chance entry in either direction, and `GuaranteedCrit` is the only crit-facing entry there is | Unfixed Aim pays the advantage as damage instead: a rider worth 85% of Attack against an enemy carrying Slow | small |

---

## 15. Chance on anything but a status landing

`EffectDef.Chance` is read in exactly one place, the `ApplyStatus` landing roll, and nowhere else.
Damage, healing, energy and turn-shift effects either happen or do not.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Finn | "have a **35% chance** to be delayed by 15% turn progress" | a probability gate on a non-status effect | the delay is certain. It used to be held to once a round as well, which stood in for the chance; that limit went when `RoundEnd` closed group 14, so the clause is now certain against every Ringing enemy once a round. **Until the round-end repair it was certain and never landed**: `CloseRound` released the turn order before raising `RoundEnd`, and `ApplyTurnShift` returns on a missing order ahead of the branch that carries a delay into the next round. The order now outlives the boundary | small |
| Avera | "has a **50% chance** to delay each by 15% turn progress" | the same probability gate, on the same kind of effect | the delay is certain against every Disrupted enemy the cast reaches | small |

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
and Lio's has narrowed to its second clause. Clara's is that same clause written for a second
hero, which is the repeat this file's grouping exists to show.

Nothing says what a removal REMOVED. `OnStatusRemoved` names the remover and now the body it was
taken from, and `_lastRemovedValue` carries the pending DoT damage that left with it, but no field
and no condition carries the KIND - so a rider that is supposed to answer only a control debuff
answers every debuff. The nearest shapes are `TriggerStatus`, which filters the EXPIRY triggers
against `_expiredKinds`, and `SubjectLostStatusThisFiring`, which since pass 3 can name ONE kind
the firing took (Mireth's No Record Remains asks for her Mark that way). "A control debuff" is a
class of kinds rather than one, and a gate names one, so neither row closes on it.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Lio | "**control debuffs** also grant 10 Energy" | the kind a removal took, published for the trigger it raises - the removal-side sibling of `PassiveDef.TriggerStatus` | the 10 Energy pays out for any debuff Lio strips, not only a control one | small |
| Clara | "When Clara removes a **control debuff**, the ally gains 12 Energy" | the same absence, and the same sentence a second time | the 12 Energy pays out for any debuff Clara strips, not only a control one | small |

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
| Hale | "The **first attack against a new Quarry target** has 20% additional Critical Damage" | the same per-hit charge, on `CritDamageUp` rather than on a damage-taken entry | Fresh Trail is a `TurnStart` passive that gives Hale `CritDamageUp` for every attack of every turn. Pass 2 measured it at 19 grants against 14 new Quarry bodies across the five sweep seeds, and each grant covers the whole turn | small |

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
| Kael | "Remove one Fragile defensive effect from each enemy, then **gain one Rubble for each effect removed**, up to 4" | the same count, taken from the bodies an effect actually CHANGED rather than the bodies it was aimed at | Take Down the Wall grants one Rubble a cast whatever its Dispel took. Pass 2 measured 10 Rubble from 10 casts that removed nothing, and every Rubble feeds What Breaks Forward | small |

`EffectDef.PerTarget` looks like it answers this and does not. It multiplies `Hits` by how many
bodies the previous effect was aimed at, its own comment says it counts only the bodies that passed
the condition while the code counts every body resolved, and `EnergyGain` does not read `Hits` at
all. It is also dormant: no shipped effect sets it.

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
`InvertStatus` is the same absence from a fourth site: it takes a beneficial entry off and writes a
harmful one back, and records neither half as a removal, so "whenever Siofra changes a buff into a
debuff" has nothing to ask.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Oswin | "**Allies who lose a debuff to Oswin** gain 8% Defense for 2 rounds" | a move recorded as a removal, so the gate has something to read | every ally gains the 8% after Reposition the Hurt, whether or not a debuff left them | small |
| Linnet | "**When Crossed Line redirects a debuff**, Linnet gains 10 Energy" | the same record, read as a gate on the payout rather than on a rider | the 10 Energy pays at round start whether or not the reroute found anything to move | small |
| Siofra | "**Whenever Siofra changes a buff into a debuff**, that target loses 8 Energy once per round" | an inversion recorded as a removal, read the same way | Prediction Cost takes 10 Energy after every basic, and a second passive takes it off every enemy after every active. Pass 2 measured 9 of 11 basic-attack drains following a basic that inverted nothing | small |

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

## 32. A shield break only its own side can hear

A Shield reaching zero raises `OnShieldBreak` on the body that lost it and broadcasts
`OnAllyShieldBreak` to that body's own side. Both windows are on the defending side, and nothing is
raised on the unit whose blow took the Shield down, so "when an enemy Shield is removed" has no
hook on the breaker. `ShieldBrokenByCaster` exists as a CONDITION and reads `_shieldBreakerId`,
which is only set inside those two windows, so it can only be read by a passive the breaker does
not own. This is the same shape as group 24, where a miss is only audible to the body that dodged.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Arno | "**When an enemy Shield is removed**, Arno gains 8% Attack for 2 rounds" | a break the breaker hears, or a `ShieldBrokenByCaster` window a watcher can open | Rubble Resolve answers a strike landing on an enemy whose Shield is still standing, which is the case before the break rather than after it | small |

---

## 33. A hero cannot hear its own change of target

`OnEnemyChangedTarget` is raised the moment `LastTargetId` moves, and it is broadcast to the
opposing side only, so the switcher's own passives never see it. The two conditions that look like
they answer it do not: `SubjectIsMyPreviousTarget` and its negation both read `PreviousTargetId`,
which is the last DIFFERENT body and is not written at all when a unit strikes the same one twice,
so a hero repeating a target reads "no history" rather than "no change".

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Yara | "When Yara **attacks a different target than last turn**, she gains 10 Energy" | a condition, or an owner-centric trigger, saying that this cast went somewhere new | Second Bearing makes the switch a standing rule instead of a gate: a `TargetBan` of `LastTarget` forbids her the body she just hit, and the reward is paid on every basic. It is paid in Attack rather than Energy for a second reason, which is not an engine gap and is in `Docs/PINOY-LIST.md` section 2c | medium |

---

## 35. A damage modifier gated on a condition rather than on a status

The engine has two ways to raise damage and both are counted off an entry a body carries:
`PassiveDef.OutputModStatus` scales the owner's output per stack of a named status, and
`DamageTakenUp` raises what a target receives. Neither takes a condition, so "more damage against
an enemy that has not acted this round" cannot be written as a modifier; it can only be written as
a second blow whose own condition gates it. The difference is real rather than cosmetic: an added
blow rolls its own critical and its own variance, where a modifier would ride the first one.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Kora | "Kora deals **10% more damage** to enemies that have not acted this round" | a damage modifier a condition can gate | First Opening is an added blow worth 38% of Attack, gated on `SubjectHasNotActedThisRound`. A 2-star has no active skill, so her basic is all the damage she has and the added blow covers the same ground the modifier would have | small |

---

## Where the counts stand

Twenty-five capabilities, thirty-six clauses, across thirty-two heroes. **The authoring
question is closed and the fidelity one added five rows**: every one of the 94 heroes has a shipped
kit, so no authoring batch can add a row, but pass 2 read every shipped clause against what the
engine does with it and found five that were approximations nobody had recorded. It began at
twenty-two and thirty-nine; the engine pass closed groups 1, 2, 3, 4, 5, 8, 9, 12, 14, 17 and 18
and restored the eighteen kits that carried them, leaving thirteen and fourteen. The three 3-star
batches added nine groups and fifteen clauses, the low-star batch added four groups and five
clauses, and pass 2 added two groups (36 and 37) and five clauses (Hale in 23, Kael in 25, Siofra
in 30, Elyra, Oris). The content pass after it closed groups 34, 36 and 37 and restored Nara, Elyra
and Oris, which is the three groups and three clauses that separate twenty-eight and thirty-nine
from the figures above.

Nine groups have more than one occupant, where before the 3-star tier there was one:

| group | capability | clauses |
|---|---|---|
| 6 | comparing a resource across two named units | 2 |
| 11 | swapping a value, rather than moving it one way | 2 |
| 13 | statuses the enum does not have | 2 |
| 15 | chance on anything but a status landing | 2 |
| 18 | telling which kind of status a removal took | 2 |
| 22 | position, and anything that would hold a unit in one | 2 |
| 23 | a damage modifier scoped to one kind of hit, or spent by one hit | 3 |
| 25 | an amount that counts the bodies an effect reached | 2 |
| 30 | a moved status is not recorded as a removal | 3 |

**The shape of the repeats is the finding.** Three of the eight pair a 3-star with a 4-star or a
5-star that asked for the same thing first - Lydia beside Celen, Avera beside Finn, Clara beside
Lio - and each of those three would close two kits at once. Group 13 is the fourth of that shape
and the low-star batch made it one: Sena's Critical Chance joins Maera's control immunity, and
both are one missing `StatusKind` member away. Nothing here is scheduled; the file is still a
record.

**Three of the four new groups are the same absence seen from a new side**, which is worth reading
next to group 24. An event in this engine is raised where it happens and broadcast to the side it
happened to, and a clause written from the other side of it has nowhere to listen: a miss is
audible only to the body that dodged (24), a Shield break only to the side that lost it (32), and a
change of target only to the side being turned onto (33). One shape, three groups, three heroes.

**Group 35 is the one to weigh against group 23**, because they ask for opposite halves of the same
thing. Group 23 wants an existing damage modifier narrowed to one hit; group 35 wants one widened
to take a condition. Three clauses across three heroes sit between them, and a single conditioned,
chargeable damage modifier would answer all three.

**Group 23 is the one with a repeat that arrived in a single batch**, which is what this file's
grouping exists to make visible: Aster's Fracture and Quin's Delivered are the same sentence
written twice, and one per-hit charge on the damage modifiers would close both. Pass 2 added Hale's
Fresh Trail, the same charge on `CritDamageUp`, so a charge that any stat entry can carry would close
all three.

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

Pass 2 and the content pass after it closed three more, each with its kit restored in the same pass:

| group | what closed it |
|---|---|
| 34 | `PassiveDef.ChargeOnLanding`, built by pass 2 and set on Nara's Carry On with `oncePerBattle` |
| 36 | `EffectCondition.SubjectHadStatusWhenStruck` (G4): the body the ability's first blow struck, as that blow found it |
| 37 | `EffectCondition.StatusStacksBelow` (G5): the mirror of `StatusStacksAtLeast`, on a status count or a generalized counter |

**Two rows survived their group's closure and are still here**, because each turned out to be about
something else. Demos's is a balance finding rather than a capability - group 12 records the
measurement. Lio's narrowed to its second clause, which is a different absence - group 18 was
retitled around it.

**Groups 20 and 22 are different in kind and should not be read as debt.** An information layer and
a movement system are games this one is not; the clauses that ask for them are three sentences out
of the thirty-nine, and all three heroes work without them.

One caution, from the pass that produced several of these rows. `MaxHpCap` reads like the ceiling a
percent-of-HP clause wants and is not: it ceilings against the TARGET's Max HP, so `0.02` of Max HP
capped at `1.0` of Max HP is uncapped. `AttackCap` is the one that bounds an amount in the caster's
own currency. Arden carried five of the first and none of the second and read seven and a half times
the roster median in the sweep arena until they were moved across; `no shipped effect sets maxHpCap`
is a standing guard again because of it.

---

## Pass 2 - fidelity, not authoring

### What fidelity means here

Pass one asked whether the engine could SAY a sentence. That question is closed. Pass 2 asked
whether the engine DOES what a shipped kit says, and treated a clause as faithful only when four
things hold:

- **the trigger fires as often as the sentence says** - not on every blow when the text names a
  redirect, not on the first scratch of a round when the text names a Forsaken enemy's buff;
- **the scalar is the amount the sentence names** - not Attack where the text names what was
  redirected, not the last release anybody made where the text names this one;
- **the subject is the body the sentence names** - not the owner's attacker where the text names
  the enemy that struck the ally;
- **a limit or cap can be reached** - not a per-Resolve bonus that nothing in the kit ever grants.

The redirect family was the model: three clauses wired to `OnDamaged` that fired on every hit their
hero took, found only by fighting each one twice. Every finding below was fought twice as well.

### What was measured, and how

Every clause of the 94 kits and 90 talents was read against Pinoy's sentence in
`import/Heroes.json` and against the kit's own description where it carries one, effect by effect,
including the `skills` blocks some kits carry beside `passive1` and `passive2`. Four engine facts
decided most of the reading: when `TriggerPassive` charges a limit, which body each broadcast
publishes as source and subject, which effects record a removal, and which kinds
`StatusInfo.IsHarmful` and `StatusInfo.IsInert` put on which side of "buff".

Each finding is proved in the harness section **ENGINE GAPS PASS 2 - FIDELITY**
(`CheckFidelityPassTwo` in `Tools/DevHarness/Program.cs`):

- **Engine rules** get a synthetic arena each, zero variance, with the old count stated beside the
  new one - the KIT DEFECTS standard.
- **Shipped clauses** are fought in the hero-sweep arena at the sweep's own five seeds and level,
  once as they ship and once patched in memory, and the pair is the evidence - the redirect
  family's standard. Content pairs FORCE both halves rather than trusting either to be what ships,
  so each check keeps holding after the content pass restores the kit, and each line says which
  half the shipped kit currently reads as.
- **Where the engine cannot say the sentence at all**, there is no faithful half to force, and the
  count is read off the record: how often the clause answered something its text does not name.
- **Output** is the subject's damage, healing and shielding over the five seeds, the sum
  `SubjectOutput` reads for the star band.

Two numbers come from runs outside the committed harness and are marked where they appear: the
whole-roster effect of making a fix the rule, read from a full `build-check.ps1` run with the fix
applied and then reverted, and a one-off probe of Kael and Kiva in the 24 live campaign stages,
three seeds a stage, with the balance line's five at a level that climbs with the stage.

### Findings, ranked by how much they change a real fight

| # | clause | mismatch | as it ships -> as it reads | output | resolution |
|---|---|---|---|---|---|
| 1 | **bookkeeping heard as a buff** - `OnEnemyBuffGained` and `MoveBuffs` treat a Mark, Ledger or Link as an enemy buff | trigger fires more; subject wrong | 7 of 10 listeners answer their own side's marks: Seraphine's Contrary Evidence 46 of 61 firings, Hale 14 of 29, Nadine 13 of 23, Sylven 5 of 20, Kiva 4 of 15, Mireth 4 of 14, Tavia 3 of 13. The shipped moves carry 10 marks off: Tavia's Cut the Wire 5, Vanno's misdirection 5, onto his own ally | listeners read as buffs-only: Tavia -34.0%, Seraphine -15.3%, Mireth +8.7%; Vanno's move buffs-only +66.1%. Made the rule for the whole roster (a run, not committed): finale 1-12 CEIL 63% -> 22%, 2-12 61.5% -> 50.5%, Tavia x0.33 below the 4-star band | engine capability built, opt-in: `buffsOnly` on a passive and on a MoveBuffs effect; turning it on is content plus a campaign retune. **On since pass 3 (C13)**, on the eight listeners and four moves, with the campaign re-solved |
| 2 | **limits spent by the trigger** - group 34's shape on 24 shipped passives | trigger lands less | 7 raised by the sweep arena, all land more: Isara III saves 0 -> 5, Maren saves 0 -> 5, Mireth's Missing Page blows 5 -> 18, Branik's Damage Reduction 3 -> 15, Tavia's Dead Channel 6 -> 13, Vanno's misdirection 5 -> 11, Dahlia's Broken Rhythm shields 16 -> 23. The other 17 wait on cases the arena never raises | Isara III +29.8%, Mireth +14.7%, Maren +14.0%. Made the rule: 72 of 72 stage fingerprints move and Mireth lifts the Breaker 5-star median from 81541 to 90536, dropping Aster to x0.29 | engine capability built, opt-in: `chargeOnLanding`. **Set on all 24 by the content pass (C11)**, with Aster raised 3.3% (Attack 182 to 188), which puts her at x0.302 of the new Breaker median |
| 3 | Kael, Unusable Cover | trigger fires more | 5 Rubble spent on firings that moved no Shield -> 0 | +13.9%; in the live campaign (probe) +6.8% of Kael's damage and shielding, wins unchanged at 50 of 72 | content C1, **landed**, with the Ward pass gated on the ally's Ward too |
| 4 | Nereia, Change of Course | subject wrong | 25 lethal saves -> 8; 17 were allies Relief Circuit never covered | -6.8% (the shipped clause is the stronger one) | content C2, **landed**, gated on HER Covered; it moved the geared CEIL and forced the solver |
| 5 | Velis, No Second Breath | trigger fires more | 110 Energy from heals on enemies at or above half HP -> 0 | -5.9% | content C3, **landed on the two Energy lines only**: the Remnant has no HP in its sentence |
| 6 | Aminah, the emergency release | trigger fires more; scalar stale | 5 releases on allies without Priority, each paying the last release anybody made as damage -> 0 | -4.0% | content C4, **landed reordered**: as written, the strike's gate read a Ledger the line before it removed |
| 7 | Kael, Take Down the Wall | scalar | 10 Rubble from 10 casts that removed nothing; the text pays one per effect removed | not measurable until the count exists; the Rubble feeds What Breaks Forward | primitive G1 (group 25) |
| 8 | Siofra, Prediction Cost | trigger fires more | 9 of 11 basic-attack drains follow a basic that inverted nothing | not measurable until an inversion is recorded | primitive G2 (group 30) |
| 9 | Caldus, No Uncertain Ground | subject wrong | 5 of 5 blows aimed at Caldus's own last attacker, a different body from the enemy that struck the ally | -0.03% in the sweep arena: the blow is small | **repaired in the engine** |
| 10 | Nereia, Reversed Flow (III) | trigger fires more; scalar stale | 39 blows against 27 Change of Course saves: 12 read a redirect Change of Course did not make | -0.1% | content C5, **landed**; the blow is gated on Covered too, since Change of Course is |
| 11 | Elyra, Slow Growth | trigger fires more | 5 of 26 Patience against a target her own basic had just slowed | **-6.7%** once measurable: 184540 to 172177, 26 Patience to 19 | primitive G4 (group 36), **built and landed** |
| 12 | Oris, No Fixed Refrain | adds rather than replaces | 4 of 4 altered attacks also threw the ordinary blow | **-3.3%** against the kit as it shipped (99830 to 96575); -7.3% against the same sequence with the ordinary gate off | primitive G5 (group 37), **built and landed** |
| 13 | Hale, Fresh Trail (I) | trigger fires more | 19 Critical Damage grants, each for a whole turn, against 14 new Quarry bodies | not measurable until a per-hit charge exists | primitive G3 (group 23) |
| 14 | Darian, Unseen Guard | subject wrong | beside Brenna, 6 Veil Charges -> 4; the other 2 came from a Shield Darian did not give | -0.2% | content C7, **landed** |
| 15 | Mireth, The Missing Page | clause dropped; a counter nominal | 0 statuses stripped -> 1; Absence has nothing to count without it | +0.4% | content C9, **half landed**: the Absence gate. The Dispel was not written, see the content pass |
| 16 | Brenna, What Held Once (III) | trigger fires more | 5 charges banked against 0 Survival saves -> 0 against 0 | none | content C6, **landed** |
| 17 | Caldus, Defined Duty | a counter nominal | 0 Resolve banked by his counterattacks -> 2 | none: 2 counters in five fights | content C10, **landed on `LastTarget`** rather than `LastAttacker` |

**Rows 1 and 2 are the pass.** Both are engine rules that reach a whole class of clause rather than
one hero, both were written against by content that assumed the other reading, and both move the
balance line when made the rule. That is why both are built as opt-in fields rather than repairs:
the capability exists and is proved, and turning it on is a balance decision the content pass makes
with the solver open.

### What was repaired in the engine

- **`TargetMode.LastAttacker` inside a broadcast about another body** now names that body's attacker,
  which is the unit `ConditionSubject.LastAttacker` names in the same window. They disagreed: one
  passive could gate on the enemy that struck an ally and strike the enemy that last struck its
  owner. At `OnDamaged` the subject is the owner, so every counterattack written against the owner's
  own attacker is untouched. `CheckLastAttackerInABroadcast` proves it; the old engine put all 4 of
  that arena's answers on the wrong body. Caldus's No Uncertain Ground is the only shipped passive it
  reaches, and it moved his 4 hero-sweep lines and 13 talent-sweep lines and nothing else: no stage
  fingerprint, no band, no balance line. Both baselines were regenerated for that reason alone.

### What was built, opt-in, and why not made the rule

- **`PassiveDef.ChargeOnLanding`** (`"chargeOnLanding"`). A limited passive that sets it gives its
  `OncePerBattle` or `Limit` allowance back when a firing reaches `ApplyEffect` for nothing, and takes
  that firing's `PassiveTrigger` event back out of the record, so the battle screen does not announce
  a clause that did nothing. `FiresPerSubject` is deliberately NOT refunded, because it is also the
  count `PassiveFiredOnSubjectAtLeast` reads and "the second time" has to count a first firing that
  was only waiting; making it refundable broke the per-subject check that proves exactly that.
  `CheckChargeOnLandingRule` proves it (0 landings in 6 rounds charged at the trigger, 6 charged on
  landing, 6 events), and the 24-row table beside it measures every shipped passive with the shape.
  Cost: one field, one parse line, one clone line, the refund block, one unit setter. **The content
  pass set it on 27 shipped passives**: the 24, Nara's Carry On, Nereia's Change of Course and
  Aminah's No Wasted Recovery.
- **`PassiveDef.BuffsOnly` and `EffectDef.BuffsOnly`** (`"buffsOnly"` on either). The listener half
  refuses an `OnEnemyBuffGained` whose status `StatusInfo.IsInert` names; the move half leaves an
  inert entry where it is. The engine already carries this repair at two other sites - the
  BuffSuppression gate and Dispel's walk - and these were the two that did not. `CheckBuffsOnlyRule`
  proves both halves. Cost: two fields, two parse lines, two clone lines, two tests.

Made the rule, each moves shipped fights far enough that the fix is a content decision: measured
above, row 2 moves every stage fingerprint and a band, and row 1 moves 69 of 72 stage fingerprints
and takes the finale's CEIL from 63% to 22%, because the balance line was tuned with Kiva banking
Broken Links off the Forsaken her own basic lays.

### Proposed primitives, grouped, with costs

| group | primitive | clauses it serves | enum members | fields | checks | size |
|---|---|---|---|---|---|---|
| G1 | an amount scaled by how many bodies the PREVIOUS effect changed - removed something from, or landed on - and honoured by `EnergyGain` as well as `GainResolve` | Quin's 12 Energy per target (25), Kael's Rubble per removal (25) | 0 | 1 on `EffectDef`, 1 counter on the simulation set at the four removal and landing sites | 1 synthetic arena; the clone check covers the field | small |
| G2 | an inversion and a move recorded as a removal for `SubjectLostStatusThisFiring` - `NoteRemovedFrom` only, not `FireRemoval`, so `OnStatusRemoved` listeners do not start hearing moves | Oswin (30), Linnet (30), Siofra (30) | 0 | 0 | 1 arena; four call sites (`MoveStatus` for both move kinds, `TransferStatus`, `InvertStatus`) | small |
| G3 | a per-hit charge on any stat entry, spent by the blow that reads it, and a crit-only scope | Aster (23), Quin (23), Hale (23) | 0 | `StatusInstance.Charges` exists; 1 crit-scope field on `EffectDef` | 3 read-and-spend sites in the damage funnel, 1 arena | large - it touches the funnel, though a no-op wherever nothing sets a charge |
| G4 | a condition asked of the struck body as it stood when the ability began | Elyra (36) | 1 `EffectCondition` member | 1 snapshot of the first struck body's status kinds, taken at the ability's first Damage | 1 arena, validator parity | small, **built**: `SubjectHadStatusWhenStruck` |
| G5 | the negation of `StatusStacksAtLeast` | Oris (37) | 1 `EffectCondition` member | 0 | 1 arena, validator parity | small, **built**: `StatusStacksBelow` |

G1 and G2 are the cheap pair and each closes a group this file already carried: G1 answers group 25
whole, G2 answers group 30 whole. G3 is the one group 23 has always wanted, now with a third
occupant.

### Content instructions for the next pass

One line each. Every one is proved by a pair in the harness, and the pair's status note flips from
"ships as the approximation" to "ships as the sentence" when the line is made. **The content pass
carried them out**, and each line now ends in what happened to it. Four were not followed as
written, because the kit said otherwise when it was opened; each says why.

- **C1** `kael_unusable_cover`: give its `RemoveStatus` of Resolve a `condition2` of `HasShield` on
  `BroadcastSubject`, and set `chargeOnLanding`. **Landed**, and the second `ShieldTransfer`, which
  passes Kael's Ward on, now asks for the ally's Ward rather than his own: Pinoy's sentence gates the
  whole clause on that ally holding one. The sweep reads the same either way.
- **C2** `nereia_change_of_course`: gate both effects on `HasStatus` `Covered` on `BroadcastSubject`,
  and set `chargeOnLanding`. **Landed on `MyStatusStacksAtLeast` rather than `HasStatus`**: Marrow's
  Promise writes the same `Covered` kind, and "under Relief Circuit" means hers.
- **C3** `velis_no_second_breath`: gate all three effects on `HpBelow` 0.5 on `BroadcastSubject`.
  **Landed on the two `EnergyGain` lines only.** The third, the Remnant, is What Was Taken's clause -
  "whenever Hollowed converts healing into damage" - and has no HP in it; gating it would have cost
  her Remnants the sentence pays.
- **C4** `aminah_emergency_release`: gate all three effects on `HasStatus` `Ledger` on
  `BroadcastSubject`, and set `chargeOnLanding`. **Landed reordered and source-filtered.** As
  written the strike came after the `RemoveStatus` of the Ledger, so its gate read a Ledger the line
  before it had just taken and it could never land; the static read-after-strip check caught it. The
  order is release, strike, remove, and the gate is `MyStatusStacksAtLeast`, because Nereia's Reserve
  and Brenna's Memory are Ledgers too. Her talents II and III append to the same passive, ungated,
  and would have spent the once-per-battle allowance on an ally without Priority all over again; both
  appends now sit before the removal with the same gate.
- **C5** `nereia_reversed_flow` (talent III): make its Damage an append on `passive2` and give the
  talent trigger `None`. **Landed**, with the appended blow carrying C2's gate, since it rides the
  same firing.
- **C6** `brenna_what_held_once` (talent III): make its `GainResolve` an append on `passive2` gated on
  `SubjectLostStatusThisFiring` on `BroadcastSubject`, and give the talent trigger `None`. **Landed.**
- **C7** `darian_unseen_guard`: set `triggerSourceIsSelf`. **Landed.** It moved the room Brenna I's
  dark check was fought in, which is recorded beside that check.
- **C8** `brenna_survival_of_the_stubborn`: gate its `LedgerDeposit` on `SubjectLostStatusThisFiring`
  on `BroadcastSubject`, and set `chargeOnLanding`. **Landed.**
- **C9** `mireth_the_missing_page`: add a `Dispel` of 1 on `BroadcastSubject`, gated on `HasMyMark`,
  as its first effect; and gate `mireth_absence_has_weight` on `HasMyMark` on `BroadcastSubject`, so
  her own Page spend stops counting as a Focused enemy losing a status. **The Absence gate landed;
  the Dispel was not written.** Inside an `OnEnemyBuffGained` window Dispel skips the kind that raised
  it, and since the Dispel repair it also skips the bookkeeping kinds, so it strips an OLDER buff
  when there is one and nothing when there is not - never "that status". Forced in, it stripped 5
  statuses across 10 firings and 0 of them the one that arrived, and all 10 firings answered
  Mireth's own Mark. With the gate and without the Dispel, Absence is honest and has nothing in her
  kit to count; the smallest thing that would let the sentence be said is a Dispel that takes the
  triggering buff rather than skipping it. **Written in pass 3**, with exactly that:
  `EffectDef.DispelTakesTrigger`.
- **C10** `caldus_defined_duty`: append a `GainResolve` of 1, `maxStacks` 3, gated on `HasMyMark` on
  `LastAttacker`. **Landed on `LastTarget`.** A Self-aimed line is never refused by the per-target
  budget, so gated on the attacker it banked Resolve on every counter the once-a-round budget turned
  away. `LastTarget` is the attacker only when the counter struck it. 2 banked against 2 counters
  landed.
- **C11** set `chargeOnLanding` on the 24 limited passives in the table: Isara III, Maren, Mireth's
  Missing Page, Branik, Tavia's Dead Channel, Vanno's misdirection, Dahlia's Broken Rhythm switch,
  Kiva's Broken Links, Marrow's Promise Survives, Brenna's Survival, Rusk, Rhys, Toren, Nadine,
  Isolde's Unfinished Sentence, Cyrra's Carry the Burden, Vaelis's Opening Moves, Merek's Priority
  Inversion, Oris III, Caldus III, Kael's Unusable Cover, Hale's Tracking Habit, Edrin's Still Here
  and Altheon III - then raise Aster about 3%, or accept the Breaker median move, and rerun the
  star band and `Tools/solve-recommended-power.ps1`. **Landed on all 24.** Mireth's Missing Page
  took the Breaker median from 81541 to 90536 (Seraphine's line), which needs Aster at 27161; her
  Attack went from 182 to 188 and she reads 27347, x0.302. Both scripts were rerun; the content pass
  section has the tables.
- **C12** `nara_carry_on`: set `oncePerBattle` and `chargeOnLanding` - "the first time" - which
  closes group 34's one row. **Landed**, the drafting note came off, and it left
  `Docs/PINOY-LIST.md` section 2f. In an arena where the first heals land on a healthy ally, the
  trigger-time charge pays 0 grants and the landing charge 1.
- **C13** set `buffsOnly` on the eight listeners whose text names a buff or positive effect (Kiva,
  Tavia, Mireth, Seraphine, Hale, Vanno, Sylven, Nadine) and on the `MoveBuffs` effects of Cut the
  Wire, Passed Position, Change Sides and Vanno's misdirection - **only together with a campaign
  retune**, because made the rule it takes the finale's CEIL from 63% to 22%. **Not done, by
  instruction: `buffsOnly` stays opt-in and off.** Two things about it moved. Hale's buff listener
  is `hale_no_safe_distance_unseen` now, because his Shield half answers `OnEnemyShielded` and has
  no buff to filter. And the 63%-to-22% reading was taken before the content pass re-solved the
  campaign, so it has to be taken again when this is done. **Done in pass 3**, re-read against the
  re-solved campaign: chapter 1's finale CEIL fell from 68% to 27%, and the solver brought it back to 66.5%.

### Suspected, not measured

A finding without a measurement is a guess, and these are the guesses. Each is read from the data
and, where marked, confirmed in the engine's code; none has a pair behind it. **The content pass
measured all nine**, and each entry now ends in its verdict. The measurements are
`CheckContentPassTwo` and `CheckSuspectedPassTwo` in the harness section ENGINE GAPS PASS 2 -
FIDELITY: three confirmed and fixed, one confirmed and half fixed, four confirmed and left, one not
as suspected.

- **Hale, No Safe Distance** (code confirmed): a Shield from `EffectKind.Shield` never raises
  `OnEnemyBuffGained` - only an `ApplyStatus` does - so "when a Quarry target gains a Shield" is
  answered only when a Quarry body already holding a Shield gains some other status, and "or becomes
  Untargetable" is not wired at all. The sweep's enemies never shield. **Confirmed and fixed**, with
  the one engine addition this pass made beyond G4 and G5: `PassiveTrigger.OnEnemyShielded`, the
  enemy-side mirror of `OnAllyShielded`, raised from the same line. No Safe Distance answers it,
  gated on Quarry alone, and a second passive, `hale_no_safe_distance_unseen`, answers a Quarry body
  gaining Stealth. With every sweep enemy warding itself, the old reading landed 2 times and answered
  Hale's own Quarry landing 13 times; the new one lands 8. No campaign enemy wards, so no PvE fight
  moves; a Proving line of heroes does.
- **Rheia, Applied Revision**: `OnEnemyBuffGained` hears beneficial statuses only, so the debuffs she
  applies and the statuses she removes grant no Revision, where the text counts any status "gained or
  lost from Rheia's abilities". Neither of her listeners fired once in the sweep. **Confirmed and
  fixed from content.** No trigger hears a caster's own debuff landing, so the Revision rides the two
  abilities that make the changes: two on a basic that strips Attack Up (the Guard Down it lays and
  the Attack Up it takes), and four on Rewrite the Outcome, which lays Guard Down on every enemy. The
  four is one per enemy capped at her limit, written as a flat four because the per-target count
  (`perTarget`) is a dormant primitive this pass did not light; below four enemies it over-pays. 0
  Revision in the sweep before, 20 after, output unchanged at 81541 - the Speed it buys does not
  reorder that arena. **Pass 3 made the four one per enemy**, with `perTarget`.
- **Nereia, Deeper Reservoir and Shared Reserve (I, II)**: both answer the Ledger's expiry whatever
  it held. Deeper Reservoir pays its 10 Energy at every expiry, where the text pays when Reserve is
  consumed for healing; Shared Reserve pays a Shield of a fixed 10% of her Max HP, where the text pays
  50% of what was stored. **Confirmed, and left.** In the sweep Deeper Reservoir answered all 15
  expiries and all 15 Reserves had released nothing; Shared Reserve paid 16 Wards worth 20864 against
  0 released. Neither is a content line: nothing can ask whether an expiry paid out, and the
  auto-release leaves `_lastLedgerRelease` at the LAST release when a Reserve expires empty, so a
  Shield on `scaleByLastRelease` would pay a stale figure. Both want the auto-release to record an
  empty release as zero, and Deeper Reservoir wants a condition on it. **Pass 3 built both, and
  found this entry's count wrong.** The count read a Reserve as empty unless a heal was the very
  event before its expiry, and the passives the release heal wakes write events in between: in the
  sweep every one of the Reserves released, and Shared Reserve's Wards had been paying a tenth of
  her Max HP where the sentence pays half of what was released. The empty case is real elsewhere:
  beside a lone bruiser no Covered ally is hit, and every Reserve expires holding nothing.
- **Cyrra, Hidden Reserve (II)**: `OnShieldBreak` fires on Cyrra's own Shield breaking, where the
  text is about the ally's Bury the Burden Shield. **Confirmed, and left.** 13 firings, each on one of
  her own 13 broken Wards, while 13 Wards broke on other allies unanswered. Moving it to
  `OnAllyShieldBreak` would fix the body and not the shape: the sentence stores from the Ward's
  creation and pays when that Ward breaks, and a store keyed to one Ward is not something a Ledger
  can be - two of her Ledgers on one ally fold into one entry. **Pass 3 said it from the other
  end**: Bury the Burden opens the store as it gives the Ward, before its own heal, and the talent
  listens on `OnAllyShieldBreak` with `includeSelf` and releases that store. The folding stays, and
  is harmless: the store's flags match the full-Weight store the base kit already opens.
- **Velis, Starve the Light (II)**: answers every heal on a Hollowed enemy, including after Hollowed's
  one conversion is spent. **Not as suspected, and something else is.** Hollowed never spends: the
  HealIntercept convert path skips a spent `ledgerBankLimit` but never charges it, so one Hollowed
  with Velis's limit of 1 converted 5 heals in a row, and a listener on `OnEnemyHealed` and one on
  `OnFunnelConvertFired` both answered all 5. There is no spent conversion for the talent to answer
  after. The defect is Hollowed's, "it can trigger once before it expires" in the sentence, and
  repairing it moves Velis's whole kit, so it is recorded here rather than made. **Made in pass
  3**: a converting Ledger's limit is spent per application, and the Remnant and Starve the Light
  answer `OnFunnelConvertFired`.
- **Isolde, Unfinished Sentence**: the "three or more damage-over-time effects" rider needs three
  Burn entries on one body, and Isolde refreshes her own; it never landed in the sweep, and outside a
  team with two other Burn appliers it is a cap nothing reaches. **Confirmed, and worse than
  suspected.** `AddOrRefresh` folds every application of a kind into one entry whoever applied it,
  and no shipped Burn sets `stackOnReapply`, so three heroes Burning one enemy leave one entry: the
  rider is unreachable in any team. Burn is the only damage-over-time kind, so "three effects" has no
  engine meaning at all; which number it should read is Pinoy's call. **Pass 3 counted debuffs
  instead** (`HarmfulStatusesAtLeast`), and found the whole passive dark in the sweep, not only
  the rider: Revelation lands on a straggler that dies while her Cinder ticks elsewhere.
- **Mireth, Absence Has Weight** (code confirmed): `OnStatusRemoved` fires on the remover for any
  removal, so Mireth spending her own Pages counts as a Focused enemy losing a status once the Missing
  Page lands often enough to fill them. C9 carries the gate. **Fixed**: 4 Absence banked ungated,
  every one her own Page spend, and 0 gated. See C9 for why nothing else in her kit feeds it.
- **Marrow, The Cost Is Named**: the Promise heal lands at the ally's turn END rather than at the
  start of their next turn, and a Promise on Marrow himself is never consumed, because
  `OnAllyTurnEnd` excludes the unit whose turn it is. **Confirmed; the self half fixed.** A second
  passive, `marrow_promise_heal_self`, spends a Promise Marrow holds at his own `TurnStart`, which is
  the start of his next turn as the sentence says, and Shared Oath's smaller heal reaches it too. 19
  Promises he laid on himself were never spent; 20 of 25 are now. **It is a large move**: his sweep
  output goes from 117914 to 170309 (+44.4%), because he is the lowest-HP ally his own basic Promises
  most often. The ally half stays - 17 Promise heals, all at the ally's turn end - because no trigger
  opens at an ally's turn start. **Pass 3 opened one**, `OnAllyTurnStart`. The +44.4% was toward
  his role's median rather than away from it: he is a Mender, and that median is Isara's 187832
  before and after, so he read x0.63 and then x0.91.
- **Aeron, Unpaid Action (III)**: "repay at least 20% Turn Debt" is not gated; any repayment pays.
  **Confirmed, and left.** 31 Silences, at least 22 of them after a debt one Press Forward banked on
  its own, which is 12% at most. Nothing can ask the size of a repayment: the entries are gone and
  the Energy moved before `OnTurnDebtRepaid` is raised. A second mismatch sits beside it: the Silence
  lands AT the repayment, at the start of the debtor's own turn, where the sentence has it after that
  turn ends. **Both halves said in pass 3**: `SubjectRepaidDebtAtLeast`, asked at `OnEnemyTurnEnd`.

### The content pass

The pass that carried out C1 to C12, built G4 and G5, and measured the nine suspected entries. C13
and `buffsOnly` were left off on purpose, and G1, G2 and G3 were not in its scope. Every clause it
touched has a pair in ENGINE GAPS PASS 2 - FIDELITY, and every pair now reads "ships as the
sentence".

**What it built in the engine, and nothing else.** Two `EffectCondition` members and one
`PassiveTrigger` member, all appended so no ordinal moved:

- `StatusStacksBelow` (G5), the mirror of `StatusStacksAtLeast`, in the pure `ConditionMet`.
  `CheckStatusStacksBelowRule` proves it on a Resolve count and on generalized counter 2: every
  basic answers exactly one of the pair.
- `SubjectHadStatusWhenStruck` (G4), answered by the Sim beside `SubjectLostStatusThisFiring`, on
  the same window: a snapshot of the first body a chosen ability's first Damage struck, taken before
  the blow, cleared at depth 0 and inherited by the passives it wakes. `CheckStruckBodySnapshotRule`
  proves it: `HasStatus` pays 6 of 6 basics in its arena, this pays 5.
- `OnEnemyShielded`, for Hale. `CheckEnemyShieldedRule` proves it: 5 of 5 enemy Wards answered,
  against 0 on Hale's old reading.

**How the kits it restored moved**, in the sweep arena's output total:

| hero | what changed | output |
|---|---|---|
| Kael | C1 and C11 on Unusable Cover | 106380 -> 121118 (+13.9%) |
| Nereia | C2 on Change of Course | 246733 -> 230023 (-6.8%) |
| Velis | C3, Energy lines only | 39646 -> 37312 (-5.9%) |
| Aminah | C4, reordered and source-filtered | 97650 -> 93704 (-4.0%) |
| Elyra | G4 on Slow Growth | 184540 -> 172177 (-6.7%) |
| Oris | G5 on No Fixed Refrain | 99830 -> 96575 (-3.3%) |
| Mireth | C11 on The Missing Page, the Absence gate | 80728 -> 92261 (+14.3%) |
| Marrow | his own Promise, spent at his own turn start | 117914 -> 170309 (+44.4%) |
| Aster | Attack 182 -> 188 | 26472 -> 27347 (+3.3%) |

**The star band is green in every tier.** Two 5-star medians moved: Breaker from 81541 to 90536,
because Mireth passed Seraphine, and Striker from 42012 to 40845, because Velis fell. Aster needed
27161 against the new Breaker median and makes 27347, x0.302; nothing else left its band, and the
nearest to an edge is Avera at x0.31.

**The campaign was re-solved, because Nereia is on the pinned line.** C2 alone took the geared CEIL
out of band - 1-9 to 60.5%, 2-1 to 58% - because Change of Course had been saving three times as many
allies as its sentence covers. `Tools/solve-recommended-power.ps1` (which needed `BattleClips.cs`
added to its compile to run at all) moved 19 of 24 stages, 15 of them in the authored roster, and
brought chapter 2's `recommendedPower` down by 3% to 5%. Every campaign band is green: ordinary
stages at x1.15 to x1.2, the chapter 1 walls at x1.02 and x1.03, the later walls at x0.98, and the
geared CEIL between 79% and 90% on the stages it can measure, 68% and 65% on the finales. The pinned
five-body line now blocks one stage at `recommendedPower`, 1-8, which is floor-pinned and reported
rather than failed.

**The baselines, and why each moved.**

- **Stage fingerprints: 72 of 72.** Kael's Unusable Cover and Kiva's Broken Links, both starters,
  are in every opening battle; with those two kits restored and the committed campaign, all 72 held.
  The solver's roster moves on 15 stages re-level those openings on top.
- **Hero sweep: 127 of 470.** Every line of a kit the pass changed and none of any other: the kits
  in the table above, the 24 C11 passives that ever fire for nothing (Branik, Brenna with C8 beside it,
  Cyrra, Dahlia, Isolde, Kiva, Maren, Nadine, Rhys, Rusk, Tavia, Toren, Vaelis, Vanno), Nara (C12), Rheia (Revision)
  and Hale (the new trigger), and Caldus on the two seeds where a counter lands. Darian's C7 moves
  nothing here because no second shielder stands beside him.
- **Talent sweep: 238 of 450.** All three talents of every 5-star whose base kit moved, plus Isara
  III (C11), Mireth II and III, and Kiva's one seed.

**What the talent checks read now.** 36 talents gain, 46 sit in the noise, 8 lose, where it was 38,
47 and 5. Velis III left the costly list: once No Second Breath paid only for low enemies, Final
Denial's cost fell to -341, t -1.4. Four joined, each pinned with its reason: Mireth I is a new trap
(C11 lets the base passive land every round, which is more than the talent's two per enemy), and
Marrow I, Mireth III and Rheia III are trades their own sentences name. The coverage note went from
19 to 21: Brenna III now rides a Survival save and Aminah III a Priority ally, and the sweep raises
neither; Caldus III had only ever been seen through the empty firing C11 takes back out of the
record; and Mireth II left, because Pages fill now.

**Found, and left for the next pass.** Each is recorded above where it belongs, and none is built:

- Hollowed's convert path never charges `ledgerBankLimit`, so "once before it expires" converts every
  heal (Starve the Light, above). **Repaired in pass 3.**
- A Dispel that takes the buff that raised its window is what C9 needs, and what Absence needs to
  have anything to count. **Built in pass 3.**
- An auto-released Ledger that expires empty leaves the last release's figure behind (Nereia I and
  II, above). **Repaired in pass 3.**
- "Three or more damage-over-time effects" has no engine meaning (Isolde, above). **Read as debuffs
  in pass 3.**
- Rheia's four Revision on Rewrite the Outcome is one per enemy only while four stand; `perTarget`
  would say it exactly. **Said in pass 3.**
- **Suspected, not measured**: a counter the per-target budget refuses still pays a
  `consumesResolve` its effect carries, because the condition that passed is what `ResolveAbility`
  reads, and the budget turns the body away after. Caldus's Defined Duty is the shipped effect with
  that shape, and it spends a Resolve that way whenever he holds one.

---

## Pass 3 - the nine pass 2 left, and buffsOnly

The content pass above confirmed nine clauses doing something other than their sentence and left
them, each for a stated reason. Pass 3 said all nine, turned `buffsOnly` on as C13 specified, and
then re-solved the campaign once, at the end. The order was the point: changes like these are not
independent, so tuning between them would have measured a build that does not ship.

Every clause is proved in the harness section **ENGINE GAPS PASS 3** (`CheckEngineGapsPassThree` in
`Tools/DevHarness/Program.cs`) the pass 2 way: an engine rule in a synthetic arena with zero
variance, a shipped clause as a pair fought with the pass 2 reading forced back in memory beside
the kit as it ships. Where a clause is dark in the sweep, the pair is fought in the dark room that
raises it, and the line says so.

### The nine, and what each measured

| # | clause | what it does now | measured |
|---|---|---|---|
| 1 | Marrow, The Cost Is Named (allies) | an ally's Promise is spent as that ally's next turn opens, on the new trigger `OnAllyTurnStart` | pass 2 reading: 16 Promise heals on allies, every one after the ally had acted. Now 16 as the turn opens and none after. Sweep output 170309 -> 174195 (+2.3%) with the Taunt repair below |
| 2 | Nereia, Deeper Reservoir (I) and Shared Reserve (II) | an empty auto-release records zero; I pays its Energy only when `LastReleaseAtLeast` says the store paid out; II's Ward is half of what was released, on `scaleByLastRelease` | beside a lone bruiser every Reserve expires empty: I paid on 7 of 8 and II on 5 of 5 of them, now 0 of 5 each. In the sweep every Reserve released, so I is unchanged there, and II's Wards fell from 20864 at a tenth of her Max HP to 10532 at half the release. Talent value: II +8498 -> +6254 a battle |
| 3 | Cyrra, Hidden Reserve (II) | Bury the Burden opens a store on each ally it wards, before its own heal; the talent listens on `OnAllyShieldBreak` with `includeSelf` and releases it | pass 2 answered 13 of her own broken Wards and none of her allies'. Now 13 allies' and 16 of her own. The store holds overflow, the two lowest-HP allies are seldom full, and none of the breaks in the sweep found anything stored, so most of it pays when the store runs out. Talent value: 0 -> +3175 a battle (t 4.0) |
| 4 | Aeron, Unpaid Action (III) | `SubjectRepaidDebtAtLeast` reads the fraction of a turn repaid, kept on the debtor as `CombatUnit.DebtRepaidThisTurn`; the talent answers `OnEnemyTurnEnd` and lays a Silence that holds through the next turn | synthetic: debt capped at 12% of a turn, 0 Silences; at 25%, 6, all after the debtor acted. Sweep: pass 2 laid 31, every one before the debtor acted; now 15, none before |
| 5 | Velis, Hollowed and Starve the Light (II) | a converting Ledger's `ledgerBankLimit` is spent per application (`StatusInstance.ConvertUses`); the Remnant and Starve the Light answer `OnFunnelConvertFired` | one Hollowed converted 5 heals and now converts 1. Alone against a mender, pass 2's kit converted 5 heals off 3 Hollowed and paid 5 Remnants; now 3 off 3, one Remnant each. Sweep output unchanged at 37312: the sweep's healer seldom heals a Hollowed body |
| 6 | Isolde, Unfinished Sentence | the rider counts debuffs rather than Burn entries: `HarmfulStatusesAtLeast` 3 | Burn folds to one entry per body whoever applies it, so "three damage-over-time effects" was unreachable in any team. Beside Hale and Elyra in her dark room, the rider read as Cinders lands on 0 of 3 firings, read as debuffs on 3 of 3, and on 0 of 63 with nobody laying one. The whole passive is dark in the sweep, not only the rider. **A rewrite, not a restoration: section 2g of `Docs/PINOY-LIST.md` asks** |
| 7 | Rheia, Rewrite the Outcome's Revision | `perTarget` on the Revision gain: one per enemy the cast reached | with two enemies standing, the flat four paid 20 Revision over 10 casts; now 10. The sweep fields five, and her cap is four, so it moves nothing there |
| 8 | Mireth, The Missing Page (C9) and Absence Has Weight | the Missing Page's first effect is a Dispel with `dispelTakesTrigger`: it strips the buff that raised the firing and nothing else | against an enemy that buffs every turn, 42 firings stripped 42 buffs, every one the buff that raised it; without the Dispel, none. Absence banks 9 from those strips and none from her own Page spend. Talent sweep: every Mireth line moved |
| 9 | Arden, Deeper Measure | `attackCap` is a field `TalentMath.TryApplyDelta` changes | "up to a higher limit" is said: the cap on Find the Measure's Max HP blow is 2.1x Attack with the talent, 2.0x without, where the delta was dropped. Talent value +2107 -> +2344 a battle |

### What was built in the engine

Appended enum members only, so no ordinal moved:

- `PassiveTrigger.OnAllyTurnStart`, raised in `TakeTurn` straight after the turn-taker's own
  `TurnStart` passives and before the Stun check, with the turn-taker as subject and source.
- `EffectCondition.LastReleaseAtLeast` (Sim state), `SubjectRepaidDebtAtLeast` (pure) and
  `HarmfulStatusesAtLeast` (pure).
- `EffectDef.DispelTakesTrigger`, and `SubjectLostStatusThisFiring` answering for one named kind
  when its gate names a status. Mireth needed the second: her Dispel now runs first in the firing,
  and No Record Remains strikes "on that same firing" only if the Mark was what left.
- An empty auto-release records zero in `_lastLedgerRelease`; a converting Ledger's limit is spent
  per application; `attackCap` joins `TalentMath.TryApplyDelta`.

**One repair nobody asked for, found by item 1.** `TargetMode.BroadcastSubject` resolved in the
single-enemy branch of `ResolveTargetsInner`, after the other side's Taunt anchor, the caster's
Provoke and concealment, all of which are rules about which ENEMY a pick may reach. So a heal aimed
at the ally a broadcast was about landed on the enemy Taunt holder instead: once Marrow's Promise
moved to the ally's turn start, the sweep's Warden took 5 of his 17 Promise heals. A subject on the
caster's own side now returns before those rules. It moved Hod, Wynn and Clara in the hero sweep as
well as Marrow, and no stage fingerprint.

### C13, turned on

`buffsOnly` is set on the eight listeners whose text names a buff (Kiva, Tavia, Mireth,
Seraphine, Hale's `hale_no_safe_distance_unseen`, Vanno, Sylven, Nadine) and the five `MoveBuffs`
effects of Cut the Wire, Passed Position, Change Sides and Vanno's misdirection. Read against the
re-solved campaign, before any retune:

- 8 of 10 listeners had been answering their own side's bookkeeping: Seraphine 46 of 61 firings,
  Hale 14 of 29, Nadine 13 of 23, Sylven 5 of 20, Kiva, Mireth and Vanno 4 each, Tavia 3.
- 69 of 72 stage fingerprints moved, and chapter 1's finale CEIL fell from 68% to 27% (chapter 2's held at 66%). Four
  walls left the power band: 1-9 and 1-12 at x1.07, 2-6 at x1.05, 2-12 at x1.04.
- In the sweep: Seraphine -15.3%, Tavia -31.6%, Vanno +43.1%, Mireth -4.8%. **Tavia and Vanno are
  4-stars**, so only Seraphine moved a median: the Breaker median fell from her 90536 to Rheia's
  81541, which raised every Breaker below five - Siofra x0.59 -> x0.66, Tamsin x0.51 -> x0.57,
  Aster x0.30 -> x0.34, Avera x0.31 -> x0.34 - and left all of them in band.
- Tavia fell to x0.33, below the 4-star floor, because Dead Channel had been striking her own
  Exposed Frequency. Her basic went from 175% to 275% of Attack and Cut the Wire from 370% to 460%,
  which puts her at x0.49, where she read before.
- Vanno reads x0.86 of the Stalker median. The Stalker band is not enforced (two 5-stars) and he
  sits under the median, so nothing fails; he is the nearest ungraded hero to where a ceiling would be.

### The 5-star yardstick, and what it caught

`CheckStarBandByRole` now grades every 5-star against its own role's 5-star median, x0.78 to
x1.64, wherever the role has two or more. The derivation is beside `FiveStarFloor`: one tier step
is the measured x0.61, the floor is half a step down and the ceiling a whole step up. It failed one
hero on its first run: **Aminah at x0.50, under two of the three Mender 4-stars.** Pinoy's numbers
are what shipped and she has Marrow's stat line; her kit does less, at 0.63 of the Mender per-turn
median over fewer turns. Her basic went from 105% to 170% of Attack, its heal from 55% to 115%, and
Restore the Balance from 155% to 230%, which reads x0.86. Nearest the edges now: Kiva and Hale at
x0.79, Edrin x1.53 and Darian x1.48.

### The retune, once

`Tools/solve-recommended-power.ps1` converged in 17 iterations and moved 22 of 24 stages, 18 in the
authored roster; every `recommendedPower` rose, by 0% to 9%. Two stages were then moved by hand with
the solver's own kind of move. At 1-11 its 600-sample reading and the gate's 200-sample one disagreed
at the band's edge, and the Hollow Husk went down one level (CEIL 77.5% -> 81.5%). 1-10 is
floor-pinned, which the solver does not measure and the band excludes, but the instrument's line
sits one percent over what it asks, and the re-solve had taken its CEIL from 96% to 55% - the relief
stage after the 1-9 wall reading as the hardest finish in chapter 1. The three units under its
Stone Warden went two levels down, and it reads 83%. **Its ceiling was the first thing tried, and
it is the wrong lever**: one level lower read 85.5% at 1-10 and pushed 2-12 over its band, because
the simulated player's losses at 1-10 change every armor drop after it. Across the moves tried,
2-12 read anywhere from x0.98 to x1.04, so a late wall's band is only as steady as everything the
simulated player fights before it.

Bands against the documented ones: ordinary stages x1.14 to x1.21 (x1.10 to x1.30); chapter 1 walls
x1.02 (x1.00 to x1.05); later walls x1.00 and x0.98 (x0.95 to x1.02); geared CEIL on the stages it
measures 78.5% to 89.5% (78% to 92%), finales 66.5% and 69.5% (55% to 72%).

### The baselines, and what moved each line

Measured by regenerating each baseline at four cumulative states and diffing them, so every line
has the change that first moved it:

- **Stage fingerprints, 72 of 72.** `buffsOnly` moved 69 (Kiva's Broken Links are in every opening
  battle); the re-solve and the two hand moves after it moved the other 3 and moved 51 of the 69
  again. The nine clauses and the Taunt repair moved none.
- **Hero sweep, 55 of 470.** The nine and the two renumbers, 17: Aminah 5, Marrow 5, Tavia 5, Velis 2.
  The Taunt repair, 8: Hod 5, Wynn 2, Clara 1, and Marrow's 5 again. `buffsOnly`, 30: Hale, Kiva,
  Mireth, Seraphine, Sylven and Vanno, 5 each, and Tavia's 5 again. The re-solve, none.
- **Talent sweep, 134 of 450.** The nine and the renumbers, 59: Aeron III, all three of Aminah and
  Marrow, Arden I, Cyrra II, Nereia II, and Velis I, II and III. `buffsOnly`, 75: all three talents
  of Hale, Kiva, Mireth, Seraphine and Sylven. The Taunt repair moved Marrow's 15 again.

### What the talent checks read now

37 gain, 47 sit in the noise, 6 lose, where it was 36, 46 and 8. Kiva III left the costly list
(-1528 -> -410, t -1.7) and so did Mireth III, which gains in its room now. Mireth I stays a trap and
is read in `s_buffer`, because the sweep no longer raises The Missing Page: -31145 of 96401, t -92.
Velis III has a new room, alone against a small self-mending enemy, because a Remnant now needs
three separate Hollowed each landed low and each healed. `TalentAudit.Costly` was brought to the
same six, and the harness checks the two equal.

The coverage note went from 21 to 23: Mireth II and Seraphine III joined, both hanging off a buff
listener the sweep's enemies never raise with a real buff. Both are proved in their dark rooms.
None left.

### Not done, and why

- **G1, G2 and G3** are still proposals: this pass was the nine clauses and C13.
- **A counter refused by the per-target budget still spends the Resolve its effect carries** (the
  suspected Caldus entry): unmeasured, unchanged.
- **Cyrra, Darian and Edrin read x1.37, x1.48 and x1.53 of their medians.** Inside the ceiling the
  new check sets; named here and in `DESIGN-BUILD-ORDER.md` as the nearest to it.
- **Vanno's x0.86** of an ungraded role, above.
- **Velis III's design question** in `Docs/PINOY-LIST.md` section 2e stands: Final Denial's Heal
  Suppression also stops the heal Hollowed would convert.
