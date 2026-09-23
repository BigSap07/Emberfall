# DESIGN-GUILD-FLOOR-CONDITIONS.md

Floor conditions for the guild dungeon. Design only, nothing built. Numbers deliberately absent.

## 1. The problem this solves

The dungeon plays the same every time, which is correct and deliberate: the fight is seeded from the guild id and the cycle, every hero is normalised to level 40 with no relic and no armor, and the same line on the same floor fights the same battle. That rule is what makes the replay honest, the leaderboards meaningful and the three attempts a decision rather than three lottery tickets.

What it lacks is not randomness, it is a reason to bring a different squad. Adding per-attempt variance would break the replay, turn the fastest-clear boards into a reroll contest and cost the attempt budget its meaning. Conditions add the decision without touching the dice.

## 2. The shape

**C1.** Each floor carries exactly one condition, so five per cycle. One, because a condition has to be readable in a single sentence on the floor card. Two stacked conditions stop being a puzzle and become a wall.

**C2.** Conditions are drawn from the existing guild-and-cycle seed, the same pure function of (guild id, cycle) that builds the floors. Everyone in the guild sees the same five all week, a different guild sees different ones, and the whole thing stays deterministic. Nothing about replay, sharing or the boards changes.

**C3.** A condition must be visible on the floor card BEFORE the player commits a line. A condition the player discovers mid-fight is a trap, not a tactic.

**C4.** Conditions belong to content, so the content version covers them. A replay recorded under one condition set must refuse to play under another, exactly as it already refuses when the numbers move.

## 3. The test a condition has to pass

**C5.** A condition must change WHICH LINE you bring, not just how hard the fight is. "Enemies have more health" is a difficulty knob and does not qualify. "Healing is halved" does, because it argues for a second mender or for none at all.

**C6.** No condition may require a specific hero. This is the standing guild rule: finding a good guild must not reduce to owning one unit. The dungeon's existing guardrails are the check, and they must still hold with conditions applied: every floor cleared by at least 40% of sample rosters, at least 25% of rosters missing an obvious answer, and no single hero in more than 35% of a floor's clears.

**C7.** A condition must not trivially satisfy a leaderboard. Six boards exist: four or fewer heroes, no five stars, one aspect, no healer, first attempt, nobody fell. A condition that caps the team at four hands everybody the four-or-fewer board for free. Either the condition and the board may not coincide, or the board is suspended on a floor whose condition grants it. Pick one rule and write it down.

## 4. Condition families

Shapes, not numbers. The engine already has every primitive each of these needs.

- **Resource**: everyone opens at reduced energy, or energy gain is slowed. Argues for heroes who do their work through basics and passives.
- **Healing**: healing reduced, or capped, or converted. Argues for shields and prevention over recovery.
- **Composition**: a hard cap on line size, or at most one of a role, or at least one of an aspect. The bluntest family and the one that most reliably changes a squad.
- **Enemy opening**: enemies start carrying a Ward, or one body opens with Taunt, or the line gains a status each round. Argues for dispel, for burst, or for patience.
- **Aspect**: one aspect deals or takes more. Note this is the one place the existing design deliberately says no, see section 6.
- **Tempo**: enemies act first, or everyone opens Slowed. Argues for speed and for turn-order manipulation, which is a whole authored family currently underused.

## 5. What the boards become

The six boards already are the tactical layer, and they are currently invisible until after the player has committed. Show them on the floor card next to the condition and the two systems reinforce each other: the condition says what this week is about, the boards say what is worth chasing inside it. This costs nothing to build, because the scoring already works.

## 6. The aspect decision, reopened

The dungeon currently guarantees that no floor is drawn from a single aspect, specifically so that a floor is never a counter-pick check. Conditions make that worth revisiting: exactly one single-aspect floor per cycle would make counter-picking a real weekly decision, and the aspect chart already exists to support it.

This is a decision, not a recommendation. Single-aspect floors reward a wide roster, which cuts against C6 for a small or new guild. If it goes in, it goes in for one floor only and it is measured against the same guardrails as everything else.

## 7. What the harness decides, not this document

- how many conditions exist in the pool
- the magnitude of each one
- which floors may draw which families, if any restriction is needed
- whether the Champion floor draws from a separate pool

These come out of the harness once the shape exists, the same way the star bands and the dungeon's own clear rates were derived rather than declared.

## 8. Open decisions

- Whether guild leaders can see next cycle's conditions in advance. It would be a real planning tool and it belongs to the guild phase two conversation rather than to this one.
- Whether the Champion floor gets a harder condition or a thematic one.
- What happens to the boards when a condition grants one, per C7.
- Whether a condition ever applies guild-wide rather than per floor.
