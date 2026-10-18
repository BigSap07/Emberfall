# Balance report R13 (2026-10-19)

Report only. Nothing here is a change that landed. Every item is a proposal for the owner, and for Pinoy where PvP is touched. The numbers come from seeded simulations. The CSVs sit in `Docs/balance/`.

How to read the numbers:
- A win rate here is a rank inside a role, not a probability. The reference line alone lands anywhere from 6.5 to 63 percent per pair.
- A win rate at n=1200 has a 95 percent interval of about +/-2.8 points.
- Flags are forced: the top and bottom 10 percent per role, rounded up, at least one each. A role of 3 to 9 heroes therefore always flags one hero each way, whether or not anything is wrong.
- Two views are kept apart. Equal stars (all heroes at 5 stars) shows kit power. Native stars (each hero at its own stars) shows what a player feels. Player-facing outliers come from native ranks.

## 1. Ranked proposals

Most valuable first.

**1. Decide the fusion plateau (section 4).**
- What: the fusing player sits at about x14 to x16 over the reference curve through chapter 5 and x15 to x18 through chapter 6 (x15.4 at 6-12). Candidate A is one number, the tier-2 multiplier in `ascensionCurveMultipliers` (6.175 to 1.5).
- Why: conversion alone is worth nothing (x1.04 at 6-12). All of the power is the top ascension tier.
- The number: 6-12 goes from x15.4 to x6.0. Chapter 2 does not move.
- Risk: `ContentVersion` moves and recorded guild dungeon replays retire. `CheckFusionEconomyDerivation` fails under A, so its derivation must be rewritten. A live tier-2 hero at Level 281 would be clamped to Level 103 on load and lose power (effect not simulated), so the owner must choose to grandfather or migrate.
- Effort: small for the number, medium for the derivation check.

**2. Fix the reference player's stuck rate (section 5).**
- What: 5 of 60 reference walks get stuck (14 sittings with no growth), about 8 percent. Lower `recommendedPower` on ten tight stages and re-solve enemy levels to the old CEIL rate.
- Why: the guard's own 20 seeds are all green, so `CheckFusionNeverGates` shows nothing. Stuck walks hold five capped Lv9 five-stars at x1.02 to x1.11.
- The number: each fix cures its own stage on paired walks (2-4 worst of 20 from 26 to 1, 3-10 from 21 to 4, 2-10 from 13 to 0). All ten together do not lower the stuck rate (5 to 7 of 60), and 5-3 stays stuck in the package.
- Risk: see the catch in section 5. It can turn `CheckFusionNeverGates` red, moves `ContentVersion`, and needs a real content re-run first.
- Effort: medium, plus a re-run.

**3. Soften or rethink the aspect wheel (section 3).**
- What: `AspectChart.Advantage` 1.10 and `Disadvantage` 0.92 (a ratio of 1.196).
- Why: DESIGN-ASPECTS meant a tiebreak. At mirror parity it is a counter.
- The number: with identical lines the advantaged colour wins 89.8 to 93.4 percent. Neutral pairs sit at 47.0 to 53.6. This is the worst case. That it shrinks where lines are not equal is expected, not shown.
- Risk: PvP and the Proving sit near parity, so the effect bites there. Needs Pinoy. Candidate values were not simulated.
- Effort: small to change, medium to re-run the grid.

**4. Look at Brenna (section 2).**
- What: the Bulwark top. Her stat passive `brenna_deep_resolve_stats` moves her win rate by 22 points in the ablation, and her active `brenna_stand_again` by 27.5.
- The number: 66.6 percent against 42.7 for the next Bulwark. At native stars 70.1 against 44.8.
- Risk: she is a 5-star chase hero, so a nerf is felt. A gap of 24 points is large next to the 2.8 interval.
- Effort: small.

**5. Review Umbra (section 3).**
- What: Umbra is in the bottom two aspects in all three hero pools (mean win rate against the others 21.4, 29.7, 7.8 percent). It has 9 heroes against 19 for each ring colour.
- Risk: partly the pool of heroes (9 against 19), partly the wheel, which Umbra sits outside. The data cannot split the two.
- Effort: depends on the owner's art and lore plans, which are not mine to judge.

**6. Look at three weak 5-stars (section 2).**
- What: Cyrra (Mender, 4.7 percent), Nysera (Marksman, 27.6) and Arden (Tinkerer, 9.0 percent, last, 13th of 13 at equal stars; 9th of 13 at native stars) sit in a bottom slot at equal stars. Arden's rules each move the win rate by 3.5 points or less.
- Risk: three kits to read, no stat tweak will fix a kit. Clara heals 0 percent and Lio 8 percent of their team's healing in these lines: Mender label, little healing.
- Effort: medium.

**7. Decide what to do with dead rules (section 2).**
- What: 5 rules never fire or change nothing in these lines, 2 more change under 1 fight in 20 (Rovan's two) and Roan only just clears it (5.5 percent), and 11 actives change under half the fights they are cast in.
- Risk: these are dead in these lines, not in the engine. The arena matrix shows some fire in built rooms.
- Effort: small each, an owner call on each.

## 2. Hero outliers

Source: `hero-table.csv` (equal stars), `hero-table-native-stars.csv` (native), `hero-rule-ablation.csv`.

**Equal stars, kit power (forced flags, see above).**

| Role (size) | Top | Bottom |
|---|---|---|
| Bulwark (14) | Brenna 66.6, Kael 42.7 | Hal 1*, Ulric 1* |
| Striker (5) | Velis 65.0 | Kest 25.7 |
| Breaker (9) | Avera 56.5 | Rheia 16.5 (noise, role median 18.5) |
| Mender (13) | Nara 2*, Iri 1* | Cyrra 4.7, Lio 0.8 |
| Warden (3) | Dahlia 53.0 | Eira 22.9 |
| Stalker (6) | Vanno 35.2 | Niko 15.0 |
| Vanguard (14) | Arno 2*, Nadine 26.8 | Bex 1*, Lydia 3* |
| Binder (5) | Elyra 58.5 | Linnet 17.0 |
| Marksman (12) | Hale 57.1, Edrin 54.0 | Nysera 27.6, Moss 1* |
| Tinkerer (13) | Oris 35.9 (borderline noise), Hod 2* | Fenn 1*, Arden 9.0 |

- Five stars in a bottom slot: Cyrra, Nysera, Arden. Rheia is not on this list, her gap is noise.
- Oris is borderline noise.
- Equal-stars flags for 1 and 2 star heroes are a star artefact. Nara, Iri, Arno, Hod, Hal, Ulric, Bex, Moss and Fenn flip at native stars. The cause: their basics deal damage power 3.6 on average against 1.6 for a 5 star, and the star multiplier that would offset it is removed. Nara is the clearest case: 87.8 percent equal stars, 2.5 percent native.

**Native stars, what a player feels.** The top of each role is a 5-star except Avera (3*, Breaker). The five-star tops: Brenna 70.1, Velis 68.9, Elyra 67.3, Hale 64.8, Dahlia 61.0, Aminah 50.2 (Mender), Oris 38.3. Darian (Vanguard) and Isolde (Stalker) lead their roles at 27.4 and 28.7. Every native bottom is a 1 to 4 star hero. The five-star ranking is stable between the two views.

**Dead kits (these lines, not the engine).**
- Never fires in 1200 fights: `nadine_proper_place`, `hale_no_safe_distance`. Fires once: `velis_what_was_taken`.
- Fires every fight, changes none (0 of 200): `arno_rubble_resolve`, `kora_first_opening`, `hale_no_safe_distance_unseen`.
- Changes under 1 fight in 20: `rovan_adaptive_guard` 2.0 percent, `rovan_return_the_guard_death` 1.5, `roan_leave_before_asked` 5.5 (just over).
- The 11 actives that change the fight in under half the fights they are cast in (ablation n=200, SE about 3.5 points): Linnet 16.5, Vanno 17.0, Niko 23.5, Zella 32.0, Finn 33.0, Kiva 39.0, Demos 44.5, Leon 44.5, Petra 46.0, Roan 46.0, Selka 46.5 percent.
- No hero with an active fails to cast it.

## 3. Aspects

Source: `aspect-grid.csv` and its roster-only and wheel-control twins, plus the min4 and min1 pool variants. 36 cells, 500 fights each. Standard error about 2.2 points per cell, so do not read a single control cell within about 6 points.

**Is any aspect dominant? No aspect is dominant across the whole roster.** It depends on the hero pool.
- 3-star and up pool: Ember is strictly dominant, 61.0 to 99.0 against all five. With the chart off it is still 61.0 to 91.6, so this is roster strength, not the wheel.
- 4-star and up pool: Ember is not dominant (loses to Gale, 21.6). Lumen is, 56.4 to 93.2.
- Every hero in the pool: nobody is dominant (Ember loses to Gale, 44.6).
- Never quote "Ember is strictly dominant" without the pool.

**The wheel is a counter at parity.** Wheel control, one identical line relabelled: Ember over Verdant 93.2, Tide over Gale 92.8, Verdant over Tide 93.4, Gale over Ember 89.8. Neutral pairs span 47.0 to 53.6. Lumen against Umbra is mutual and stays near 50. In the roster grid the chart moves ring pairs by 14 to 34 points (3-star pool) and up to 47 (4-star pool). This is the worst case: equal lines, level 40, five stars, no gear. "The effect shrinks where lines are not equal" is expected, not shown.

**Umbra and Lumen** are neutral against the four ring colours. They gain and lose nothing there, which is a different deal from plus 10 and minus 8 percent.

**Roster counts today** (`heroes.json`): Ember 19, Tide 19, Verdant 19, Gale 19, Umbra 9, Lumen 9. Ember is not over-represented. DESIGN-ASPECTS saying so is stale, and DESIGN-WORLD cites it.

## 4. Fusion

Source: `fusion.csv`, 20 walks, median ratio of entry power to recommended power at the first attempt of each stage. Main already carries the R6 economy content, so the before numbers match R6.

**The lever is the top ascension tier.**

| Stage | No fusion | Convert only | Ascend only | Both |
|---|---|---|---|---|
| 2-6 | x1.10 | x1.08 | x1.46 | x1.87 |
| 4-12 | x1.08 | x1.09 | x8.22 | x13.91 |
| 6-12 | x1.03 | x1.04 | x15.25 | x15.41 |

Copy income only sets when the plateau arrives. Raising `ascensionCopies` to [1, 9] moves it about one chapter later. It does not lower it. A tier-2 hero at cap is 33.2 times a Lv1 hero against 1.92 for tier 0.

**Candidates (fusing player, finale of each chapter).**

| Finale | Before | A: tier-2 multiplier 1.5 | B: tier-2 opens at 5-1 |
|---|---|---|---|
| 2-12 | x2.94 | x2.94 | x2.92 |
| 3-12 | x8.75 | x6.18 | x3.30 |
| 4-12 | x13.91 | x6.29 | x3.08 |
| 5-12 | x15.56 | x6.35 | x16.37 |
| 6-12 | x15.41 | x6.03 | x15.44 |

- A flattens the plateau to x6 to x7. One number in `balance.json`. `CheckFusionEconomyDerivation` fails (1 check) and its derivation would need rewriting.
- B is a pacing delay, not a fix. It does not lower the plateau, it overshoots it (x17.95 at 6-6, x16.4 at 5-12, against 15.4 before) and has a cliff (x3.1 at 4-12 to x16.2 at 5-6). It needs game code in `AscensionBlocker` and a UI line.
- A and C move `ContentVersion`, so guild dungeon replays retire. A live tier-2 hero at Level 281 would be clamped to Level 103 on load by the save's level clamp (claim verified, effect not simulated). That player's hero loses power. It needs an owner call: grandfather or migrate.
- C (noted, not asked for): A plus `ascensionCopies` [1, 14] gives a ramp: x3.3 at 4-6, x5.5 at 4-12, x6.0 at 6-12.
- `CheckFusionNeverGates` stays green under A, B and C. The fusing player pays 0 extra sittings at every wall, before and after. Whether walls should bind a fusing player is still open decision 3 of ECONOMY-PROPOSAL-R4.
- Walks are 20 seeds on one fusion policy (every spare body converts, every affordable ascension happens): the fastest plausible fusing player. Last-digit differences between the raw text and the CSV are formatting only.

## 5. Economy fixes

Source: `economy-tails.csv`. Player: the reference player (never converts, never ascends), 60 walks, `WalkSeed(0..59)`. Walks 0 to 19 are the set `CheckFusionNeverGates` reads. A tail is extra sittings on a stage.

**What main shows.**
- Worst of the 20 guard walks, ordinary stages over 12: 2-4 = 26, 2-10 = 13, 3-10 = 21 (new on main). 2-5 reads 11.
- Over 60 walks the list is longer: 2-3 21, 2-5 16, 2-8 29, 2-10 47, 3-7 15, 3-10 21.
- 5 of 60 walks get STUCK at 2-4, 2-9, 2-6, 4-2 and 5-3. The guard's 20 seeds are lucky, all green. The true stuck rate is about 8 percent.
- Cause: a capped player sits at x1.03 to x1.10 over 72 stages. A poor armor draw tips the walk over a cliff. At 2-9, walk 46 stays stuck with the enemies 2 levels lower and clears at 4 levels lower.

**Smallest fix per stage.** The change is `recommendedPower` down, with enemy levels re-solved (`SolveStageLevelsAt`) aiming at the old CEIL rate. CEIL moves a little (2-10 84.9 to 81.7, 3-7 84.7 to 80.1, 3-10 87.2 to 83.4). Every after-reading (79 to 90 percent) stays inside the 78 to 92 band: inside the band, not held. Paired walks, same seeds up to the stage. n is 56 to 60 per stage, because a walk stuck earlier never reaches the stage. A stuck walk is counted apart and never mixed into a worst.

The bar, stated once: the smallest of -3, -6 or -10 percent where no walk is stuck and the worst is 9 extra sittings or less. That is why 2-9 takes -6 (at -3 its worst is 12). The step below leaves 30 at 2-4, 14 at 2-10, 13 at 3-7 and 14 at 3-10.

| Stage | n | Change (`recommendedPower`) | Worst of 20 | Worst of n | Stuck | Floor/ceiling | Enemies | CEIL % |
|---|---|---|---|---|---|---|---|---|
| 2-4 | 60 | -6% (10986 to 10327) | 26 to 1 | STUCK to 3 | 1 to 0 | 55/67 to 51/63 | 49/51/49 | 85.8 to 85 |
| 2-10 | 57 | -10% (10868 to 9781) | 13 to 0 | 47 to 0 | 0 to 0 | 34/36 to 29/31 | 29/29/28/27/28 | 84.9 to 81.7 |
| 3-10 | 57 | -6% (11243 to 10568) | 21 to 4 | 21 to 4 | 0 to 0 | 40/42 to 37/39 | 36/37/36/37/37 | 87.2 to 83.4 |
| 2-3 | 60 | -3% (10764 to 10441) | 1 to 1 | 21 to 9 | 0 to 0 | 60/69 to 58/67 | 56/57/58 | 88.9 to 89.7 |
| 2-5 | 59 | -3% (11140 to 10806) | 11 to 4 | 16 to 4 | 0 to 0 | 37/44 to 35/42 | 35/33/34/35/34 | 81.8 to 79 |
| 2-8 | 58 | -3% (10793 to 10469) | 4 to 1 | 29 to 4 | 0 to 0 | 45/47 to 43/45 | 43/43/42/43 | 86.6 to 83.7 |
| 2-9 | 58 | -6% (10688 to 10047) | 4 to 0 | STUCK to 1 | 1 to 0 | 35/37 to 32/34 | 32/32/31/31/31 | 83.6 to 83.8 |
| 3-7 | 57 | -6% (11516 to 10825) | 5 to 1 | 15 to 3 | 0 to 0 | 50/54 to 48/52 | 48/47/47/48 | 84.7 to 80.1 |
| 4-2 | 57 | -3% (11638 to 11289) | 4 to 1 | STUCK to 2 | 1 to 0 | 55/62 to 53/60 | 53/52/52 | 88.9 to 87.7 |
| 5-3 | 56 | -3% (11917 to 11559) | 6 to 3 | STUCK to 5 | 1 to 0 | 67/75 to 65/73 | 64/63/65 | 87.9 to 87.3 |

- The strong cases are 2-4, 2-10 (4 of 57 walks over 12, worst 47), 2-8 and 2-5. The first three rows were asked for. The other seven were found on seeds 20 to 59.
- Six stages (2-3, 2-9, 3-7, 3-10, 4-2, 5-3) had a single event over 12 in 60 walks (95 percent interval for 1 in 60 is about 0.04 to 9 percent). Their before-readings rest on one walk each.
- Plain `ceilingLevel` trims (-1 to -3) also cure the tails but push CEIL to 93 to 100 percent on 7 of 10 stages, out of band, so they are not proposed.
- The same numbers are in `economy-tails.csv`, which also carries p90 and the step below. Neighbour stages' p90 and median were not measured separately.

**The catch.** All ten together on 60 walks: ordinary-stage events over 12 fall from 16 to 9, but stuck walks go from 5 to 7 of 60, and from 0 to 2 on the guard's own 20 seeds (5-3, 5-6). In the package 5-3 is still stuck (walk 14, which arrived at x0.997) and 4-11 is newly stuck (walk 21). The 5-3 fix does not hold once earlier stages are edited. A content edit re-draws every later seed, so new tails appear (2-11 27, 3-4 15, 3-11 15, 5-4 13). Spot fixes cure their stage, not the tightness. The stuck rate stays 8 to 12 percent either way, not distinguishable at 60 walks. The package could turn `CheckFusionNeverGates` red. It moves `ContentVersion` (replays retire) and the fingerprints of the stages touched. It needs a real content re-run before landing. The deeper fix is the curve, not ten stages.

**Echo leak (R9, flag off).** Sized, but the price is not evaluated: this report gives no verdict on whether one day of Valor is right. An Echo is one extra Recital fight a day for a day of Valor. A win at floor F pays 200 + 20F Gold and 10 + F Shards (`GrantRecitalResult`). Dust comes only on milestone floors: 150 times floor/10 at floors 10 to 40, a relic or 1,000 Dust at 50. Floor 10 a day is 400 Gold, 20 Shards, 150 Dust. Floor 50 is 1,200 Gold, 60 Shards, a relic or 1,000 Dust. Idle income is about 1,100 Gold an hour at 2-4, so Gold and Shards are small. For comparison only: one repeat clear of a stage pays 6 + 2 x stage index Dust (about 66 at stage 30), one Echo win on floor 40 pays 600 Dust, and the Valor cost is 5 x (30 + 2S). The milestone Dust is the leak to watch. No price change is proposed. The Echo is a cheap route to milestone Dust and should be priced against Dust before the flag turns on (DESIGN-SINKS).

## Method

- Hero power: 94 heroes. 2 reference lines x 4 enemy lines (campaign stages ch2_9, ch3_9, ch5_9, ch6_9) x 150 seeds = 1200 fights per hero, 112,800 in all. Same seeds for every hero. Stars forced to 5, no talent, relic or armor, one shared hero level per line pair. Role score is points above the role median. Ablation: 200 fights per rule, with and without it.
- Aspects: 6 x 6 cells x 500 fights, 18,000 per grid, three hero pools. Level 40, stars forced to 5. Half the fights put the row aspect on the opponent side, so any side edge cancels.
- Economy: 60 reference walks (`WalkSeed(0..59)`), paired before and after on the same seeds. Instrument `--r13-tails`.
- Fusion: 20 walks (`WalkSeed(k)`) of the harness economy walk. Candidates are in-memory `BalanceConfig` overrides. Nothing was written to content.
- Every run is seeded. A rerun at a different thread count gave byte-identical hero tables.
- The harness (`R13Balance.cs`, `R13Economy.cs`) lives on a local scratch branch and is not on main. Raw outputs stay in the handoff folder.
- Not measured: talents (three per hero), gear, relics, PvP-specific modes.
