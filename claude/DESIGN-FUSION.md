# DESIGN-FUSION.md

Fusion and ascension. Design only, nothing built. Numbers are deliberately absent, see section 8.

## 1. Scope

Two systems, one economy:

- **Fusion**: converting low star heroes into a five star.
- **Ascension**: taking a five star past five, to six, seven and up.

They are the same ladder. Fusion's output is ascension's input, and that is the entire reason fusion produces a random hero rather than a chosen one.

## 2. Star rating means kit depth, which is why only five stars ascend

The star contract, unchanged:

| Stars | Kit |
|---|---|
| 1 | basicAttack only |
| 2 | adds passive1 |
| 3 | adds activeSkill |
| 4 | activeSkill and passive1, passive2 optional |
| 5 | full kit, all three talents |

A star rating is a statement about how much kit has been authored. Promoting a 3 star to 4 stars is not a number change, it is a request to write a skill. So:

**F1.** 1 to 4 star heroes never change star rating. They are early roster and fodder, permanently.

**F2.** Only 5 stars ascend, 5 to 6 to 7 and up. Ascension adds power and level cap, never a new base kit.

This is what stops the system from costing 94 hero kits per tier.

## 3. Layer one: conversion, below five stars

**Input**: 1 to 4 star heroes, aspect matched, in a recipe that consumes the lower tiers on the way up (a 4 star is itself built from 3 stars, Idle Heroes style).

**Output**: a random 5 star of that aspect.

**F3.** The output is random within the aspect pool, never chosen. The randomness is the point: it is what manufactures the duplicate five stars that layer two eats. A chooser here kills ascension.

**F4.** Class means ASPECT, not role. Five stars by role are Warden 1, Binder 1, Stalker 2, Tinkerer 2, so role pools would be vending machines: put fodder in, get the only Warden out. Five stars by aspect are exactly 5 each across all six aspects, so the roll is a real roll.

## 4. Layer two: ascension, five stars and up

**Input**: copies of that specific hero, plus a material still to be decided.

**F5.** Above five stars, only copies of that same hero count. No cross-hero substitution. This is the sink the whole fusion layer feeds.

**F6.** Ascension resets the hero to level 1 on a higher curve. Tier N+1 at level 1 out-powers tier N at its cap, so the reset reads as a promotion rather than a loss, and every ascension reopens the gold sink.

Consequence, accepted: a level number stops being comparable between heroes. Level 40 at six stars beats level 100 at five. Every surface that shows a level must show the tier beside it, and any sort by level sorts by tier first.

## 5. Level caps are gated by star tier, and this is the pace gate

**F7.** A hero's level cap is a function of its star tier, not the account's rank.

This is the most important rule in the document and it comes from a measurement, not a preference. The pace pass found that levels bought with gold are 65 to 71 percent of team power, that 86 percent of gold is idle income, and that levels are welded to the hero they were bought on. The simulated player therefore never swaps a bench five star in, because the swap throws the investment away. Fusion alone does not fix that. Star gated caps do: the cap is what makes a freshly fused five star worth levelling, and what makes the over invested four star stop growing.

Corollary: raising the cap is ascension's main job in the economy. The stat bump is secondary.

## 6. Umbra and Lumen are rare on purpose, and rarity has to be paid for

Roster per aspect: Ember 19, Tide 19, Verdant 19, Gale 19, Umbra 9, Lumen 9. Five stars are exactly 5 each, so the fodder pools are 14, 14, 14, 14, 4, 4.

The scarcity is deliberate, the Light and Dark shape from Idle Heroes. Two things follow, and both have to be written down or the next balance pass quietly undoes them.

**F8.** Umbra and Lumen five stars sit in the upper half of the existing five star band. The harness already checks every five star between x0.78 and x1.64 of its role median. Those two aspects get a floor at the role median, the other four spread across the full band. No new check is needed. Rarity with no payoff is just a pool players learn to avoid.

**F9.** The fodder bottleneck is set by recipe, not left to the roster count. Four fodder heroes against fourteen means Umbra and Lumen conversions arrive roughly three and a half times more slowly before anything is tuned. Left alone that is not "a little rarer", it is a wall.

The rule: **Umbra and Lumen conversions take a seed plus generic fodder.** One hero of that aspect at the required tier is mandatory (the seed, which is what makes them slow), the rest of the recipe accepts any aspect. The four hero pool then gates the rate honestly without asking the player to farm ten duplicates out of four heroes.

The alternative was authoring ten more low star Umbra and Lumen heroes. That is ten kits and ten art sets to solve something a recipe rule solves. Not worth it now. If the roster grows later the seed rule still holds and simply stops binding.

## 7. Summoning and pity, unchanged

Pity stays. The summon test showed it is a three pull starter guarantee, not a duplicate suppressor: duplicates flow normally from pull four, which is exactly what ascension needs. The earlier claim in this project that pity had to go for fusion to work was wrong, taken from the build order's phrasing instead of the measured behaviour.

Current rates, 20 / 30 / 30 / 17 / 3 from one to five stars, are still marked placeholder in balance.json pending this economy. They are a fusion input now, so they get retuned with the recipes, not before them.

## 8. What the harness decides, not this document

Deliberately absent, the same way the star bands were derived rather than declared:

- fodder count per conversion, per tier
- copies per ascension, per tier
- where each tier's level cap sits
- the level curve multiplier at each tier
- the Umbra and Lumen seed count
- retuned summon rates

These come out of the harness once the shape exists. Writing them here first means guessing, then defending the guess.

## 9. Open decisions

- How far ascension goes. Six and seven are assumed, whether it stops there is not decided.
- Whether ascension tiers grant passives of their own, or stay pure stats plus cap. Pure stats first, passives are a later content lever.
- What the non copy ascension material is and where it is farmed. Guild dungeon output is the obvious candidate and would give guilds a second reason to exist that is not a leaderboard.
- Whether fusion unlocks before or after the current mid game campaign wall.

## 10. Build order impact

Fusion and ascension touch hero data (tier field, per tier caps), the level curve, the power formula, the roster screen, a new fusion screen, summon rates, and the harness (a conversion and ascension economy check alongside the existing sweeps). This is not a screen, it is a pass through the progression spine. It goes after the current merge queue clears, and its harness check gets written in the same job, not afterwards.
