# DESIGN-TALENTS.md

What a talent is, what it has to do, and the order the three fixes happen in.

## 1. The measured state

Of 90 authored talents, across 30 five-star heroes:

- **37 gain** beyond two standard errors.
- **47 change nothing** the output total can see, or sit inside the noise.
- **6 lose**, seven counting Kiva III since her lift.

So more than half of the system does nothing. Every complaint about talents feeling vague or messy starts here, and no screen redesign fixes it.

## 2. Talents are a loadout, not a tree

**T1.** A hero authors three talents. The player picks one, or none. The pick is free to change at any time.

This is deliberate and it stays. A tree means investment, prerequisites and several nodes live at once, which converts a tactical choice into progression and multiplies the authoring from three per hero to N per hero. For an auto-battler where the player's only lever is what they bring, a free per-fight choice is worth more than a spent one. The guild dungeon already proves the point: a talent pick survives dungeon normalisation precisely because it is a real decision rather than an investment.

The reason the system does not currently feel like anything is T2, not T1.

## 3. What every talent owes the player

**T2.** A talent must do something a player can notice. Not "it fires", which the harness can already prove for most of them, but a change in the fight a person watching would see. A talent that fires 154 times and changes no battle is not a quiet talent, it is a broken one.

**T3.** A talent may cost its hero output, but only as a legible trade. Utility bought with damage is good design. The card has to say so, and the game already has the machinery: `TalentAudit.Costly` is a Core data file the talent screen reads.

**T4.** No traps. A trap is a talent that reads as an upgrade and is strictly worse, with nothing bought in return. Two are currently named: Kael III buys Rubble that his own basic consumes before it can expire or stack, and Mireth I trades a once-per-round limit for fewer landings since the chargeOnLanding repair. Both are kit conflicts and both need authoring, not tuning.

**T5.** Talents are measured in normal play, not only in a purpose-built room. A talent that only works when handed a specific ally line is a talent a real player never sees. The arena matrix's `EMBERFALL_MATRIX_NORMAL=both` reading is the instrument for this.

## 4. Ascension unlocks talents

**T6.** A five-star at tier 0 has talent 1. The first ascension unlocks talent 2, the second unlocks talent 3.

Only five-stars author talents and only five-stars ascend, so the two systems line up with no new concept. It answers the fusion doc's open question about whether ascension tiers grant anything beyond stats and a cap: they grant kit depth. It gives talents the progression feeling without building a tree.

This is built LAST. Gating a system where half the options do nothing would only make the unlock disappointing.

## 5. The order

1. **Make them work.** Classify all 47, fix everything that is an ordering or engine problem, and produce the exact list of what needs authoring for Pinoy. This is the job.
2. **Fix the screen.** Once every talent does something and the costly ones are labelled, the screen is easy. Before that, it is decorating an empty system.
3. **Build the ascension gate.** Small, self-contained, and only worth doing once there is something worth unlocking.

## 6. What the harness decides

- which of the 47 are dead on arrival, situational but real, or genuinely inert
- how much a talent has to move a fight to count as noticeable under T2
- whether a costly talent's trade is worth its cost

## 7. Open decisions

- Whether one to four star heroes ever get talents. Today only five-stars author them, which follows from the star contract, and nothing argues for changing that yet.
- Whether the ascension gate applies retroactively to heroes a player already owns, or only to newly fused ones.
- Whether a talent may be locked per game mode, the way the guild dungeon normalises everything else.
