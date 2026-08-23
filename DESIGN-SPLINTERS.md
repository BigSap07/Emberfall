# Emberfall — Splinters

This is a proposal, open for feedback. It is not a specification and nothing here is implemented.
DESIGN-ASPECTS and DESIGN-RELICS are decided documents; this one is not. The mechanic described
below is a candidate shape, not a committed one.

Nothing here changes a number. Exact diminishments are explicitly deferred to a balance pass.

---

## What a Splinter is

The First Light shattered into six aspects. Those aspects are the law now, and they are what they
are: whole beliefs, complete in their convictions, landing in full when a hero who held one stands on
the line.

A Splinter is something smaller. When the First Light broke, the break was not clean. Six major
fragments became six aspects. But the shattering moment itself left residue — splinters of the event,
not of any one belief. Each carries one belief, faintly, as an echo rather than a law. They are not
people. The Cinder did not recall them: there was nobody to recall, because a Splinter is not a
person. They are not relics either. DESIGN-WORLD is clear that the Cinder cannot remember relics at
all, and that whatever a relic is a fragment of, it is not a fragment of the First Light the Cinder
came from. A Splinter is a fragment of the First Light, but not of the kind that became an aspect
and not of the kind that became a relic. It is a third category, named here as one, because it is
one.

The Keeper of the Cinder can channel a Splinter through the team itself rather than through any
individual in it. That is the mechanic: a Splinter's effect is team-wide, and it requires no hero
of its aspect to be present.

---

## The slot

**One Splinter, per team, at a time.** It occupies a slot that is not one of the five hero slots
and is not a relic slot (relics belong to individual heroes). It appears on the team assembly screen
as its own row, separate from both.

There are no secondary slots, no stacking of Splinters, no exceptions.

---

## What Splinters do

Each of the six Splinters corresponds to one aspect and provides a **diminished, team-wide version
of that aspect's rule**, active regardless of which heroes are fielded.

The diminishment is the design guardrail and it is not optional. A Splinter must always deliver
less of the rule than a real hero of that aspect delivers. Its purpose is to give the Keeper a
trace of an aspect they are not currently fielding — not to let them skip fielding one. The
moment equipping a Splinter makes a dedicated hero of that aspect unnecessary, the Splinter is
too strong. Every balance decision about Splinters answers to this constraint first.

The exact numeric diminishments are not decided here and are explicitly deferred to a balance pass,
following the same convention this project applies to values left open in other design documents.

---

## The six Splinters and their hooks

Each entry names the Splinter, states what it does in plain language, and identifies the exact
existing code hook it rides. The hook citations come from DESIGN-ASPECTS §2 and the branches
verified during the aspect build passes; no new subsystem is required for any of them.

---

### Ember Splinter

**What it does:** Burn statuses applied by any ally stack as separate entries rather than
overwriting, for the duration of the battle.

**The full Ember rule:** `CombatUnit.AddOrRefresh` currently finds a matching `StatusKind`, keeps
the stronger magnitude, and discards the weaker application. For Ember heroes, that branch is
skipped and each Burn is kept as an independent `StatusInstance`. `Sim.TickDamageOverTime` already
ticks every entry independently and needs no change.

**The Splinter version:** The stacking behaviour applies, but the stacks are weaker — lower
magnitude, shorter duration, a tighter cap, or some combination. A real Ember hero contributes
full-magnitude Burn stacks from snapshotted `EffectiveAttack`; the Splinter's stacks carry
something less. Exact diminishment deferred.

**The guardrail:** Two real Ember heroes stacking full burns should still clearly outperform
the Splinter's team-wide trace. A line with no Ember hero and this Splinter should feel the
absence.

---

### Tide Splinter

**What it does:** Energy overflow from any ally source is redirected rather than discarded, for
the duration of the battle.

**The full Tide rule:** `Sim.AddEnergy` clamps to `EnergyMax` and drops the remainder. For Tide
sources, the remainder redirects to the living ally furthest from casting via `Sim.EmitEnergy`.
The rule keys on the granter: `AddEnergy` takes an explicit `sourceId`, and the redirect fires
only when that source is a living Tide unit.

**The Splinter version:** The redirect fires for all ally sources, but the redirected amount is
a fraction of the true overflow rather than the full remainder. A real Tide hero redirects every
point of overflow; the Splinter redirects a smaller share. Exact fraction deferred.

**The guardrail:** Maeve redirecting full overflow to the ally furthest from casting each turn
should remain meaningfully stronger than the Splinter's reduced share from any source.

---

### Verdant Splinter

**What it does:** A portion of healing that would be clamped on a full-health ally is banked
forward as Regen.

**The full Verdant rule:** `Sim.ApplyHealValue` returns early when `missing <= 0`. For Verdant
sources, that early return is intercepted and the clamped value is applied as a `StatusKind.Regen`
entry via `CombatUnit.AddOrRefresh`, ticked by the `else if (s.Kind == StatusKind.Regen)` branch
of `Sim.TickDamageOverTime`.

**The Splinter version:** The banking applies from any heal source, but only a fraction of the
clamped overflow becomes Regen. A real Verdant hero banks everything; the Splinter banks less.
Exact fraction deferred.

**The guardrail:** Wren banking the full output of Verdant Tide on a healthy line should remain
clearly better than the Splinter's reduced bank from any healer.

---

### Gale Splinter

**What it does:** Once per round per unit, any ally that lands a kill may re-enter the current
round's turn order.

**The full Gale rule:** `Sim.DealRawDamage` calls `TriggerPassive(killer, PassiveTrigger.OnKill, 0)`
in its death branch. The aspect rule keys on `killer.Aspect == Aspect.Gale` at that site. The
re-entry inserts the killer back into `Sim.Execute`'s turn list for the current round.
`galeExtraTurnsPerRound` in `balance.json` caps re-entries per unit per round.

**The Splinter version:** The re-entry fires for any ally, not only Gale units, but the cap is
set lower than the full-Gale cap, or the re-entry requires a higher-threshold kill condition (a
kill achieved against an enemy above a health percentage, for example — exact condition and cap
deferred). The hook site is the same `OnKill` call.

**The guardrail:** Ryn taking a free extra turn on every Gale kill should remain a stronger and
more reliable re-entry than whatever the Splinter offers the rest of the line.

---

### Umbra Splinter

**What it does:** Harmful statuses applied by any ally source are uncleansable.

**The full Umbra rule:** Two places. `Sim.ExpireStatuses` uses `StatusInstance.SourceId` to
check whether the applier is alive before decrementing `RoundsLeft` — the non-expiry clause.
The `EffectKind.Cleanse` branch of `Sim.ApplyEffect` filters on `StatusInfo.IsHarmful` — the
uncleansable clause.

**The Splinter version:** The uncleansable clause only. The non-expiry clause — debuffs that do
not tick down while the applier lives — is not granted by the Splinter. That clause belongs
distinctly to a real Umbra hero; the Splinter carries the removal-of-an-option without the
persistence. Both halves together are the full Umbra rule; the Splinter carries one half.

**The guardrail:** Nyx applying an uncleansable `DefenseDown` that also does not tick while she
lives is strictly more powerful than any ally's debuffs being merely uncleansable. The Splinter
makes debuffs harder to answer; Nyx makes one debuff permanent.

---

### Lumen Splinter

**What it does:** A fraction of incoming single-target damage aimed at any ally is redirected
to the ally with the highest current HP.

**The full Lumen rule:** `Sim.ResolveTargets`' `FindTargetAnchor` override — renamed from
`FindTaunter` during `claude/aspects-lumen` — answers every single-enemy targeting question
aimed at the player's side, routing the hit to the living Lumen unit regardless of `TargetMode`.
A living Lumen unit is unconditionally the answer; absorption follows as a consequence of always
being the answer.

**The Splinter version:** The Splinter does not reroute targeting wholesale. Instead, a fraction
of resolved single-target damage is transferred from the intended target to the highest-current-HP
ally after the target resolves normally. This is a damage-share rather than a targeting override,
and it is a weaker intervention: it does not protect the target from crits, on-hit effects, or
status applications — it only shares a portion of the raw damage. The hook lives in
`Sim.DealRawDamage`. Exact share fraction deferred.

**The guardrail:** Orrin unconditionally answering every single-target ability aimed at the line
— routing the hit, catching crits, catching status applications — must remain decisively stronger
than a post-hoc damage share. A Lumen hero protects the line's sequence of decisions; the
Splinter only softens one number.

---

## Acquisition and leveling

Both are **open questions**, and this document does not answer them.

Whether Splinters are found, earned, summoned, or purchased; whether they have a rarity tier;
whether they level up and how; what resource, if any, governs their progression — none of this is
decided here. The shape above is mechanic-only. Economy and progression belong to a future decision
that can see the full feature in context. Inventing a resource or a shop in this document would
be premature and probably wrong.

**Open questions to flag explicitly:**

- What is the acquisition path? Discovery in the campaign, a separate summon banner, a shop, a
  mission reward, something else?
- Is there one Splinter of each aspect or multiple (implying leveling or rarity within an aspect)?
- If they level, what drives leveling and does the leveling change which half of the rule applies
  (as with the Umbra Splinter's two-half structure) or only the magnitude?
- How does the player know what a Splinter does before equipping it? The no-percentage constraint
  from DESIGN-RELICS applies here too: a Splinter's text must describe a rule change, not a stat.

---

## What this is not

**Not a person.** The Cinder recalled nobody. A Splinter is not in the roster, has no name in
the narrative sense, and carries no lore of a life lived. The fictional category is new and is
named as such above.

**Not a relic.** DESIGN-WORLD is explicit: whatever a relic is a fragment of, it is not a
fragment of the First Light the Cinder came from. A Splinter is a fragment of the First Light.
They are incompatible categories in the fiction and in the slot structure.

**Not a hero slot.** The team can hold one Splinter and five heroes simultaneously. A Splinter
does not take a hero slot and is not an alternative to filling one.

**Not a substitute for the aspect on the line.** This point is also stated above under the
guardrail language and is worth restating plainly here: the Splinter of an aspect must always be
weaker than a real hero of that aspect. A player who fields the Ember Splinter and no Ember hero
should feel that absence. The Splinter is a trace, not a replacement.
