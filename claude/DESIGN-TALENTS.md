# DESIGN-TALENTS.md

What a talent is, what it has to do, and the order the three fixes happen in.

## 1. The measured state

Of 90 authored talents, across 30 five-star heroes, as TALENTS WORTH PICKING reads them at forty paired seeds:

- **88 gain** beyond two standard errors.
- **0 change nothing** the output total can see, or sit inside the noise.
- **2 lose**, and both are trades with a named purchase: Marrow I and Nysera I.

There are **no traps**. Every talent is observed changing a fight somewhere in the arena matrix, and every rule the matrix once observed nowhere is observed now.

**What it said before this pass, and what was wrong with it.** The reading was 37 gain, 47 flat, 6 lose - more than half the system doing nothing. Almost none of the forty-seven was inert in the sense of never firing; most fired often and paid in a currency the reading cannot hold. Energy taken off an enemy, Effect Resist lowered, turn progress lost, a Silence landed on a body with no active to silence, a Block Down against bodies that carry no Block, Guard handed to an ally: every one of those is a real thing happening in the fight and none of them moves damage, healing or shielding, which is what the output total is. The pass did not make those clauses louder. It gave each talent one more clause its own sentence already implied and that the fight can read - a source-keyed Vulnerable, a broken guard, a deeper crit, a Ward - and left the utility where it was.

**Three shapes of failure recur, and each is worth knowing before authoring the next talent.**

- *A cost with no purchase.* Kael III bought Rubble that lasts the battle while What Breaks Forward empties the pile on the very next basic; Mireth I bought two landings per enemy where `firesPerSubject` is a whole-battle budget and the limit it replaced was per round; Vaelis II capped how many buffs a swap moves, which is strictly less than moving all of them. All three said what they cost on the card and none of them said what it bought, because there was nothing to say.
- *A payout into an account the fight cannot read.* Brenna III deposited into her own Memory, which releases as a heal, and a Bulwark standing at full HP is healed for nothing - a deposit of half her Max HP measured an exact zero. Edrin I healed him the same way. Sylven I raised a ceiling on a Ledger whose deposit is `scaleByLastRemoval` against a removal that banks nothing.
- *A gate no arena arranges.* Aminah III hung off a passive observed nowhere. Caldus III waited on a Declared Target falling under a third of its HP, once per battle. Darian III needed Darian to be holding a Ward at the moment an ally's ran out. Kiva II and Kiva III answered OnKill, and the killing blow in these arenas is rarely hers.

**Two facts constrain any future talent pass, and both were measured.** Being hit grants Energy, so a talent that adds hits can feed the enemy ultimates that kill the line it was meant to help - which is why every clause added here raises damage per existing blow rather than adding blows. And a sham talent worth +0.2% Attack cleared the old t >= 2 rule for twenty-four of thirty heroes, so the parity study's materiality floor of 1% of Attack is what separates a talent from a dice shift.

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

**T6. Built.** A five-star at tier 0 has talent 1. The first ascension unlocks talent 2, the second unlocks talent 3.

Only five-stars author talents and only five-stars ascend, so the two systems line up with no new concept. It answers the fusion doc's open question about whether ascension tiers grant anything beyond stats and a cap: they grant kit depth. It gives talents the progression feeling without building a tree.

The two ceilings meet exactly, and that is worth knowing before either moves. `HeroDef.MaxTalents` is 3, and `balance.json` authors three `fiveStarLevelCaps`, so `StatMath.MaxAscensionTier` is 2: the last ascension unlocks the last talent with nothing left over on either side. `CheckAscensionGatesTalents` asserts that rather than trusting it, so a fourth tier or a fourth slot is caught where it happens.

**Derived, never stored.** `HeroInstance.TalentSlotsUnlocked` is `min(MaxTalents, Ascension + 1)`, read off a tier the save format has carried since long before talents existed. No save version moved and none should: a persisted "unlocked" field could only ever disagree with the tier that produced it, and the first save that disagreed would be a hero whose card said one thing and whose fight did another. It applies retroactively to every hero already owned, because there is nothing to migrate - the tier is already there.

**The gate lives on one line of the simulation.** `CombatSimulation.BuildUnits` reads `HeroInstance.LiveTalent`, which resolves the selection through both the def and the tier. That is the only line in the game that turns a selection into something the simulation can see, so a locked slot is absent from a battle in exactly the way an unselected one is - no passive, no extra draw, nothing the record can see, the whole fight byte-identical to the same fight with nothing picked.

**The pick is kept, not wiped.** `GameSession.SelectTalent` refuses a locked slot, because a player cannot choose what the hero cannot run. But a selection already stored - a tier that moved under it, a hand-edited save - is kept, resolves to nothing, and comes back whole when the tier does. That is the rule an unresolvable relic id and an unauthored talent slot already follow, for the same reason: the pick is a statement of intent and the tier is a fact about the hero today.

**A record is the authority on what fought.** A snapshot carries a def, a level and a pick and has never carried an ascension tier, so nothing downstream can re-derive the gate. The gate is therefore applied once, at CAPTURE: `TeamSnapshot.Capture`, `ProvingGenerator` and `GuildDungeon.LineOf` all publish `LiveTalentSelection`, so a locked pick never reaches a record and a tier-0 owner cannot hand a duelling friend a talent it cannot run. `GuildDungeon.FightUnder` then replays a record with exactly the pick it holds. Nothing about how a stored guild dungeon record or a Proving ghost replays changed.

**On the page, locked is a fifth state and the card is not hidden.** A player deciding whether to ascend is deciding on the strength of what ascending buys, so a locked card keeps its name, its sentence, its figures and its reading, and says three things instead: the chip reads LOCKED, the button names the tier that opens it rather than offering to run it, and the footer says the rule once. The frame means "your pick" and the chip means "its state", which locking is the first thing to pull apart - a kept-but-locked pick stays framed and reads LOCKED, and neither half lies.

## 5. The order

1. **Make them work. Done.** All forty-seven were classified and rewritten, both traps were rewritten rather than relabelled, three of the five trades stopped paying for something that was not there, and the three talents and four kit rules the arena matrix observed nowhere are observed now. `KnownCostlyTalents` and `TalentAudit.Costly` carry two entries, both trades.
2. **Fix the screen.** Every talent does something and the costly ones are labelled, so this is now unblocked.
3. **Build the ascension gate. Done.** The unlock is derived from the ascension tier, the simulation reads it on the one line that resolves a selection, the pick survives a tier that drops under it, published lines carry only what their hero would really fight with, and stored records replay unchanged. `CheckAscensionGatesTalents` measures the whole grid - every slot at every tier - as fights rather than as a flag.

## 6. What the harness decides

- whether a talent is observed changing a fight at all: the authored-talent sweep and the arena matrix, together, and neither counts a clause whose only trace is announcing itself
- which way it changed the hero's own output, and by how many standard errors: TALENTS WORTH PICKING, forty paired seeds, in the sweep arena and in the talent's own room where it has one
- whether a room can weigh a talent at all. Some cannot: a clause that shuts down a mender shortens the fight, and a shorter fight is fewer blows, so s_mender read Velis III doing exactly what its sentence says as a loss of eighteen thousand output. That room is no longer one of his readings, and the comment where it was says why.
- whether a costly talent's trade is worth its cost - still a judgement, but the list is two entries and each names its purchase

## 7. Open decisions

- Whether one to four star heroes ever get talents. Today only five-stars author them, which follows from the star contract, and nothing argues for changing that yet.
- Whether a talent may be locked per game mode, the way the guild dungeon normalises everything else.
