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

## 1. Answering the blow that is landing

No `EffectCondition` asks about the magnitude of the hit being resolved: the enum's 50 members ask
about HP, statuses, stats, energy, action history and firing counts, and the damage funnel does not
publish the number it is carrying. Nor can anything reach back into a hit that has already resolved,
because every ally-facing hook fires after it.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Rhys | "When an ally takes a hit exceeding 25% Max HP, Rhys takes 30% of it instead" | a condition on the incoming damage as a fraction of the victim's Max HP | the guard goes up on the FIRST strike of each round whatever its size, and covers that ally for the rest of the round | medium |
| Toren | "redirects 25% of **that hit**" | a way to move a share of a blow that has already landed; `OnEnemyDamagedAlly` fires afterwards | the ward goes up for the rest of the round and answers the next one | medium |

---

## 2. Per-round budgets on a standing status

`StatusInstance` carries `UsesPerRound` and `UsesThisRound`, and exactly two places read them:
`InterceptDisrupted` and the `StatusUseAvailable` condition. `CoverFraction` reads only `Magnitude`,
so a Covered ward cannot be told to answer once a round; `Guard` spends `Charges`, which do not
refill at a round boundary.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Rhys | "Rhys takes 30% of it instead, **once per round**" | a per-round use budget honoured by the Covered redirect | `limit: OncePerRound` on the passive, so the ward is APPLIED once a round and then answers every hit for that round | small |
| Eira | "it reduces the first area hit **each round** by 20%" | the same budget on a Guard charge | one Guard charge for the whole 2-round duration, so it answers one hit rather than one a round | small |

---

## 3. Telling an area hit from a single-target one

Nothing on a resolving Damage effect records how many bodies it was aimed at, so no condition and no
mitigation can ask.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Eira | "it reduces the first **area** hit each round by 20%" | a flag on a resolving hit saying it came from a multi-target effect | the Guard charge answers whichever hit lands first, area or not | medium |
| Eira | "When an ally with Mobile Cover is **singled out**" | the same flag, read from the other side | `OnEnemyDamagedAlly` gated on the victim carrying the ward, so "singled out" becomes "hit" | medium |

---

## 5. Duration control when a status is moved, inverted or extended

Three separate effect kinds each decide a duration by a rule of their own, and none takes the
authored `duration` as an override.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Celen | "transfers the largest ally Shield to the lowest-HP ally and **extends it 1 round**" | a duration on `ShieldTransfer`, which re-seats the recipient's Shield entry at a hardcoded 99 rounds | the `ExtendStatus` is authored and lands, but against a 99-round entry it is nominal | small |
| Siofra | "changes one target buff into its matching debuff **for 2 rounds**" | a duration override on `InvertStatus`, which copies the flipped entry's own `RoundsLeft` | the reversal lasts as long as the buff had left, and the text says so | small |
| Demos | "extends Arrears by 1 round, **up to 3 rounds**" | a ceiling on `ExtendStatus`, which does `RoundsLeft += added` with no clamp | the cap is dropped; the clause extends without limit | small |

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

Several magnitudes in this roster are written as "N% Max HP" of a body that is not the caster. The
engine pays a DoT in the applier's Attack and caps a percent-of-HP damage term against the target's
own health, which is the wrong unit for a sentence of that shape.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Maren | "Recovery heals **3% Max HP** each turn" | a Regen tick scaled off the holder's Max HP; `ApplyStatus` resolves a Regen magnitude as `EffectiveAttack * magnitude` | the tick is written in Maren's Attack at the value that comes to about 3% of an ally's bar at the level the number was set against | small |
| Demos | "At turn end, Arrears loses 4% current HP" | a percent-of-current-HP tick bounded in the caster's own currency; `MaxHpCap` ceilings against the TARGET's Max HP and so cannot bind one | an `attackCap` was added that Pinoy's sentence does not mention, holding the clause at the relationship it was written with | small |

---

## 13. Statuses the enum does not have

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Maera | "and **she cannot be controlled**" | a status that raises the `ControlImmunity` stat; `StatusKind` has no `ControlImmunityUp`, and the stat is authored on `UnitDef` only | the clause is dropped; the guaranteed critical half of the passive is authored and the immunity half is not | small |

---

## 14. Round-end triggers

`PassiveTrigger` gained `RoundStart` and has no mirror. `OnAllyTurnEnd` and `OnEnemyTurnEnd` are
per-unit, so "at round end" has to become "as each of them finishes".

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Finn | "**At round end**, Ringing enemies have a 35% chance to be delayed" | a `RoundEnd` trigger beside `RoundStart` | `OnEnemyTurnEnd`, once a round, so it answers a Ringing enemy's own turn ending | small |

---

## 15. Chance on anything but a status landing

`EffectDef.Chance` is read in exactly one place, the `ApplyStatus` landing roll, and nowhere else.
Damage, healing, energy and turn-shift effects either happen or do not.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Finn | "have a **35% chance** to be delayed by 15% turn progress" | a probability gate on a non-status effect | the delay is certain and the passive is limited to once a round instead | small |

---

## 16. Redirecting an enemy onto somebody other than the caster

`Provoked` constrains one attacker's single-enemy targeting onto whoever applied it, which the enum
calls the opposite direction to `Taunt` and a genuinely different rule. Neither can send a named
enemy at a named third body.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Niko | "forces its next Basic Attack onto **a random ally**" | a redirect naming a destination other than the applier | `Provoked`, so the blow lands on Niko | medium |

---

## 17. Counting how many statuses of a class a body carries

`StatusStacksAtLeast` and `MyStatusStacksAtLeast` count entries of ONE named kind.
`HasAnyBeneficialStatus` answers whether a body has one at all and not how many, so "N per buff" has
nothing to multiply.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Tamsin | "removes 6% turn progress **for each target buff**, up to 18%" | a count of beneficial statuses usable as a scalar | 6% flat, and 18% against a body carrying any buff at all | medium |
| Leon | "Damage increases **10% per displayed buff**, up to 40%" | the same count | the whole 40% lands against a body carrying any buff at all | medium |

---

## 18. Naming the unit an event happened TO, when the trigger names the causer

Two triggers fire on the unit that caused something and publish no subject, so a passive on either
can pay the causer and cannot reach the body it happened to. `OnTurnDelayed` sets only
`_broadcastSourceId`; `OnStatusRemoved` sets the remover and nothing else.

| hero | clause as Pinoy wrote it | what the engine lacks | how it is approximated now | size |
|---|---|---|---|---|
| Tamsin | "**The first enemy delayed** each round gains 10% Defense Down" | a broadcast subject on `OnTurnDelayed` | the whole enemy line takes it, once a round | small |
| Lio | "When Lio removes a debuff, **that ally** recovers 5% missing HP; control debuffs also grant 10 Energy" | a broadcast subject on `OnStatusRemoved` | the heal moves into the ability, where `SubjectLostStatusThisFiring` can see it; the Energy pays the lowest-HP ally, which is the body Tested Touch cleansed by construction | small |

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
| 5 | duration on a moved, inverted or extended status | 3 |
| 1 | answering the blow that is landing | 2 |
| 2 | per-round budgets on a standing status | 2 |
| 3 | telling an area hit from a single-target one | 2 |
| 8 | remembering who hit MY SIDE rather than who hit ME | 2 |
| 12 | amounts written as a percentage of the HOLDER's health | 2 |
| 17 | counting how many statuses of a class a body carries | 2 |
| 18 | naming the unit an event happened TO | 2 |
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
