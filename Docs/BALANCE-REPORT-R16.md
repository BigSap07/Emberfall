# Balance report R16 (2026-10-19)

FIRST DRAFT. Sections 4 (after column) and the merged-tree rerun of section 6 are placeholders, marked below. Every number here is read from a file named next to it. Files are under `D:\Emberfall\handoff\R16-CORE-BALANCE\` unless a path says otherwise: `data\` holds the simulation outputs, `logs\` the build-check and harness runs.

The owner took four decisions on 2026-10-19 after `Docs/BALANCE-REPORT-R13.md`. This report is what they did. Sections 1, 2 and 5 are changes that landed on the run branch. Section 3 is the solver. Section 4 is the curve. Sections 6 and 7 are report only.

How to read the numbers:
- A ratio is the power a simulated player brings to the first attempt of a stage, over that stage's `recommendedPower`.
- A walk is one seeded run of the harness economy player over the whole campaign. `WalkSeed(k)` is `1 + k * 1000003`. The reference player never converts and never ascends. The fusing player does both as soon as it can.
- A tail is the extra sittings a stage costs beyond its own ladder floor. A walk is stuck when it makes no progress for 14 sittings (the walk takes one swap undo first, so a stuck walk shows 14 or 16 sittings).
- The rest of the method is in `Docs/BALANCE-REPORT-R13.md`. The instruments are the same.

## 1. Fusion A (owner decision 1)

What changed: `ascensionCurveMultipliers` in `balance.json` goes from `[1, 2.035, 6.175]` to `[1, 2.035, 1.5]` (and the `BalanceConfig` default). A tier 2 five-star walks Levels 46 to 103 where it walked 46 to 281. The caps `[9, 18, 39]`, the Level each tier starts at (1, 10, 46) and `ascensionCopies [1, 4]` did not move. Source: `logs\build-check-builder-task1b.log`, FUSION ECONOMY section.

**Fusing player, median entry power over `recommendedPower`.** Source: `data\fusion-r16.csv`, taken on the simulation tree WITHOUT the counterattack rule (20 walks). On the run branch with the counterattack rule and the 60 walks of the guard, the fusing player reads 2-12 x2.926, 3-12 x6.211, 4-12 x6.274, 5-12 x6.360 and 6-12 x6.019 (`data\review\review5-tasks1-2-fusion-guard.md`), within 0.04 of the table. The first run of the new content in the harness's own 20 fusing walks (`logs\build-check-builder-task1b.log`) reads x6.182, x6.250, x6.338 and x6.019 at 3-12, 4-12, 5-12 and 6-12, within 0.05 of this table.

| Finale | Before (x6.175) | After (x1.5) |
|---|---|---|
| 1-12 | 0.980 | 0.980 |
| 2-12 | 2.939 | 2.939 |
| 3-12 | 8.752 | 6.183 |
| 4-12 | 13.909 | 6.287 |
| 5-12 | 15.563 | 6.350 |
| 6-12 | 15.406 | 6.026 |

| Chapter, stage 6 | Before | After |
|---|---|---|
| 3-6 | 5.037 | 4.196 |
| 4-6 | 11.634 | 6.337 |
| 5-6 | 15.119 | 6.267 |
| 6-6 | 17.848 | 6.990 |

Chapters 1 and 2 do not move. The plateau is now x6.0 to x7.0. It equals R13's candidate A (x2.94, x6.18, x6.29, x6.35, x6.03).

**ContentVersion** moves from the version beginning `c1-3015` to the one beginning `c1-e4c0` (`logs\build-check-baseline.log`, `logs\build-check-builder-task1b.log`). Recorded guild dungeon replays retire. Accepted by the owner. No stage, hero sweep or talent fingerprint moved (the same two logs).

**The derivation.** `CheckFusionEconomyDerivation` cannot derive 1.5. No rule in it gives that number: it encodes a target, the fusing player's plateau at about x6, and a target is a decision. So tier 2's multiplier is pinned in the harness as the owner's decision of 2026-10-19. The derivation computes the continuity value (x6.175, Levels 46 to 281), prints it beside the pin, derives every other number around it, and a check asserts the pin is the only departure. A hero at tier 2 at its cap is x12.73 a Level 1 hero, and x6.63 a tier 0 hero at its cap (`logs\build-check-builder-task1b.log`).

**Migration, not grandfather.** A save holding a tier 2 five-star at Level 281 loads through the real serializer and is clamped to Level 103 by the version 18 level clamp. Its power falls from 31006 to 12075 (x0.389). The hero keeps its tier, round-trips, and plays 1-1 and 6-12. The check is `CheckTierTwoAboveTheNewCapLoadsClampsAndPlays` (`logs\build-check-builder-task1b.log`).

**Another check moved with it, and its guard changed.** `CheckEconomyRespondsToIncome` read 0.12 over 8 players (needs more than 0.50) after the change. Its window opened at 4-1, which a fusing player now reaches already on the plateau: under decision 1 a fusing player's power stops depending on income from about 3-12, so 1x and 2.5x income gave the same roster and levels. That is the plateau working, not the income link being severed. The window now opens a third of the way in, at 3-1, before the plateau. The three readings: 0.12 at 4-1 (spread -0.37 to 0.50, fails), 0.70 at 3-1 (spread 0.57 to 1.17, kept) and 0.32 at 2-1 (spread 0.23 to 0.41, fails). The bar of 0.50 is unchanged. This is a default taken, traced to owner decision 1. Source: `logs\build-check-builder-task1.log` (the 0.12), `logs\build-check-builder-task1b.log` (the 0.70) and `logs\task1-income-window.md` (all three windows).

## 2. Guard (owner decision 2)

`CheckFusionNeverGates` reads 60 walks (`WalkSeed` 0 to 59) where it read 20. A stuck walk is now named by stage, walk and seed. Full table: `logs\task2-guard.md`.

| Tree | Stuck walks of 60 | Where |
|---|---|---|
| Before this run | 5 | walk 23 at 4-2, 46 at 2-9, 48 at 5-3, 51 at 2-6, 59 at 2-4 |
| After the counterattack rule and fusion A | 8 | walk 1 at 4-2, 6 at 5-4, 16 at 3-7, 20 at 5-9, 23 at 4-2, 46 at 2-9, 51 at 2-6, 59 at 2-4 |

Sources: `logs\fng60-before-r16-seeds.log` (before) and `logs\task2-guard.md` (after).

Four stuck walks are in both (23, 46, 51, 59). Walk 48 is cured. Walks 1, 6, 16 and 20 are new. Walks 0 to 19, the old guard, hold three of the new ones (1, 6, 16), which were green before and are the reds after the counterattack rule (section 5). Fusion A cannot move a reference walk: that player never ascends. The check is red until the curve is loosened (section 4). That is the point of widening it.

The solver's stuck scan reads the same 60 walks, so it costs three times as much per iteration.

## 3. Solver (task 3)

Source: `data\sim-progress.md`, `data\solver-whatif-final.log`.

- The solver now aims at `SolverTargetRatios`, the per-stage entry ratio of the shipped curve (all 72 inside the power band; tightest margins 1-6 0.0116, 1-12 0.0174, 1-7 0.0183). It is a snapshot of the content before this run.
- The `2-1` armor 0 is a reading artifact. The entry reading was taken before the sitting that wears armor (armor opens at 1-12). `ArrivalTeamPower` now reads `2-1`, the stage right after armor opens, as the player stands once they wear what they hold. Only the reading changed: 60 walks are identical. The `2-1` median goes from x1.061 to x1.146.
- Run on the shipped content as a what-if: 0 of 72 `recommendedPower` moved. 2 rosters moved by one unit: 1-6 from 20/20/21/20 to 19/20/21/20, and 2-12 from 22/22/22/22/22 to 22/21/22/22/22. The solver's own reading at those two rosters is 84 and 62 percent CEIL. No CEIL before is quoted: the solver's reading and the harness's CEIL check are two instruments and their numbers do not compare. Nothing was written.
- **The table is a snapshot, not a fixed point.** On the run branch with the counterattack rule, the same what-if moves 42 stages in iteration 1 and 59 in iteration 2, and had not converged after 590 s (review 4, `data\review\review4-task3-solver.md`). The median ratios per stage are the same to 3 digits. The first-seed walk re-draws from 2-11 on, and the solver follows that noise. So the table is refreshed with the content and not trusted across an engine change.

## 4. Curve (task 4)

Target (prompt): over 60 reference walks, 0 stuck, no ordinary stage with a worst tail over 12 extra sittings, every banded stage inside its band, the walls binding as designed.

Two before columns, because the counterattack rule moved the walks (section 5). The honest before for the curve lever is the second one.

| | R16 start: old engine, old curve | New engine, old curve |
|---|---|---|
| Stuck walks of 60 | 5 | 8 |
| Stuck stages | 2-4, 2-6, 2-9, 4-2, 5-3 | 2-4, 2-6, 2-9, 3-7, 4-2 (twice), 5-4, 5-9 |
| Ordinary stages with a worst tail over 12 | 2-3 21, 2-4 26, 2-5 16, 2-8 29, 2-10 47, 3-7 15, 3-10 21 | 2-3 21, 2-4 26, 2-5 16, 2-8 29, 2-10 47, 2-11 17, 3-4 45, 6-3 17 |
| Walls with a worst tail over 12 | 2-6 30, 2-12 28, 4-12 13 | 2-6 30, 2-12 28 |
| CEIL, graded stages inside band | 70 of 70 (1-1 and 1-2 are floor pinned, not graded) | not measured |

Sources: `data\tails-before.csv` and `data\tails-before-summary.md` (left column, byte identical to R13's baseline per review 2, `data\review\review2-task4-before-table.md`); `data\tails-new-engine-old-curve.csv` (right column). Worst tail counts stuck walks as 9999 in the raw rows and is listed here for cleared walks only.

**PLACEHOLDER: the after column.** The lead fills in the lever, the package (recommended power down by one shared percentage per chapter from chapter 2, enemy levels re-solved), the after table of stuck walks, worst tails and CEIL per stage, and the guard on 60 seeds.

## 5. Counterattacks (owner decision 4, task 5)

**The rule.** A counterattack answers the attacker and ignores Taunt. In `ResolveTargetsInner`, `TargetMode.LastAttacker` with a live, legal attacker returns before the Dev Arena's forced pick, the Provoke and the Taunt anchor. With no such attacker it falls back to `FrontEnemy`, an ordinary pick that respects Taunt. The card texts of Caldus's No Uncertain Ground and Defined Duty and Jory's Worn Hinges are unchanged, and the proposals to reword them are withdrawn.

**What it covers.** Four places in the content use the mode: `jory_worn_hinges`, `caldus_defined_duty`, `caldus_no_uncertain_ground` and the guild tech line `line_answer_in_kind` (review 3, `data\review\review3-task5-counter-rule.md`). Not covered, on purpose: `LastTarget` (a follow-up on the caster's own blow) and Maera's Keep the Beacon (`EnemyThatLastDamagedAlly`, an authored active whose card names its target). Maera's active stays under Taunt. The owner can overrule that.

**The define.** Compiling with `EMBERFALL_COUNTER_ANSWERS_TAUNT` restores the old resolution exactly. The default build is the new rule. Proven once, outside the repo: with the define, check (a) (a counter goes to the attacker with a Taunt holder standing) and check (d) (an ally's strike-back at the enemy that hit another ally) fail as they do on the old engine, and all 470 sweep fingerprints match the old baseline (`logs\counter-rollback.md`).

**The harness check.** `CheckCounterAnswersTheAttackerNotTaunt` (a counter and an ally's strike-back go to the attacker with a Taunt holder standing; the same hero's ordinary attack still goes to the Taunt holder; a control with no Taunt agrees). It fails on the old engine and passes on the new (review 3).

**What moved.**
- Hero sweep: only `jory_the_old_gate`, seeds 1 to 5 (5 of 470). Damage 69495 to 72836, power 80420 to 83761, x0.77 to x0.80 of the 5-star median, still in band. Sources: `logs\build-check-baseline.log` and `logs\build-check-builder-task5b.log`. Caldus did not move in the sweep.
- Talent fingerprints: 450 of 450 match. Stage fingerprints: 216 of 216 match. ContentVersion unchanged (review 3, reading `logs\build-check-builder-task5b.log`).
- Arena matrix output: identical.
- Real campaign (72 stages, first and last battle, 4 seeds, Caldus with Kael, Brenna, Nara and Hale at level 30, 576 battles; review 3): counters that landed on a Taunt holder that was not the attacker. Caldus: 3 of about 190 before, 0 after. Jory: 88 of 149 before, 0 of 152 after. So the fix is large for Jory and small for Caldus on today's content.

**What it cost the walks.** The reference line picks Caldus, and 56 of 60 walks diverge at a stage with a Taunt enemy, after which gold and armor drops re-draw. The paired mean change in sittings per stage over the common cleared prefix is -0.024 (sd 0.238, n 60), so no systematic weakening, and the median entry ratio per stage agrees to 3 digits (review 3). But the stuck count moved from 5 to 8 of 60 (section 2), and the guild tech no-tech check (G5) reads 1 of 6 walks stuck at 4-2 where it read none (`logs\build-check-builder-task5b.log`). Both are the same re-draw, and are cured by section 4 if it works.

## 6. Wheel at 1.05 and 0.95 (report only, task 6)

`AspectChart` stays 1.10 and 0.92 in the game. Source for this whole section: `data\wheel-summary.md`, with the CSVs `data\aspect-grid-r16*.csv` and full tables in `data\wheel\grids.md`. Same method and seeds as R13; the run at 1.10 and 0.92 reproduces all 9 R13 aspect CSVs byte for byte. Noise: 2.2 points per cell, 1.4 per hero.

**PLACEHOLDER: these numbers were taken on a tree without the counterattack rule.** The sim reruns them on the merged tree (about 1 minute); replace them if they move.

Mirror win rate of the advantaged colour (the wheel control, identical lines):

| Pair | R13, 1.10 and 0.92 | R16, 1.05 and 0.95 |
|---|---|---|
| Ember over Verdant | 93.2 | 78.0 |
| Tide over Gale | 92.8 | 75.4 |
| Verdant over Tide | 93.4 | 80.4 |
| Gale over Ember | 89.8 | 73.6 |
| Range | 89.8 to 93.4 | 73.6 to 80.4 |

- At parity the wheel is still a counter, about 3 wins in 4 against 9 in 10. It is not a tiebreak. Neutral pairs read 47.0 to 53.6 in both runs, as they must. A tiebreak would need a gap well under 1.05 and 0.95, which was not simulated.
- The mean absolute chart effect over the eight directed ring cells falls from 22.5 to 13.8 points (3 star pool), 33.7 to 23.7 (4 star pool) and 27.1 to 16.1 (every hero).
- Dominance: in the every-hero pool nobody dominates at 1.10 and 0.92, but Ember does at 1.05 and 0.95 (it beats Gale 56.4). Never quote a dominant aspect without the pool and the chart.
- Umbra, mean win rate against the other five (roster grid): 3 star pool 21.4 to 22.0, 4 star pool 29.7 to 30.2, every hero 7.8 to 8.4. The wheel does not explain Umbra. The chart-off twin reads 21.6, 30.0 and 8.0. The cause is the pool (9 Umbra heroes against 19 per ring colour) and the kits.
- Brenna and the Bulwark gap: equal stars 23.9 to 22.3 (Brenna 66.6 to 65.2, Kael 42.7 to 42.9). Native stars 25.3 to 23.9 (Brenna 70.1 to 69.7, Kael 44.8 to 45.8). Her lead is her kit, not her colour. Across all 94 heroes the mean absolute move is 0.86 points (equal stars), and the largest single move is 3.5 (Elin).
- Near-parity modes (the Proving, PvP) are where the gap bites. Read 75 as decisive at parity, not as the PvE effect.

## 7. Player size (report only, task 7)

Source: `logs\player-size.md`; sizes file and editor log named there. The reviewer recomputed the sizes from disk (review 1, `data\review\review1-task7-player-size.md`).

- Windows release, offline player: **3237.5 MB unzipped, 676.1 MB zipped** (3,394,798,931 and 708,993,753 bytes). Before: 318 MB and 188.5 MB. Growth about 2.9 GB. The zip wins back 4.8x. Over 1 GB unzipped, under 1 GB zipped.
- Build report: 3132.8 MB packed. The 338 battle sheets are 3018.3 MB of it (12072.9 MB raw, all BC7, up to about 9.4 MB each and 8.9 on average). Other sprites 24.8 MB, backgrounds 19.6 MB, everything else 63.4 MB. Textures are 98.0 percent of the editor's report.
- Top 10 assets: all battle sheets of 9.4 MB (`nereia_the_field_medic` and `celen_the_open_hand` sheets, and one `rheia_the_revision_scribe` sheet). The report sorts ties unordered, so the top of the list is dozens of 9.3 to 9.4 MB sheets.
- **Proposal, not applied, an owner call.** Set `win_format` of the Units group in `Tools/texture-import-policy.py` to DXT5 Crunched (format 29). The policy chose BC7 on purpose, so this reverses a recorded decision. Measured on one sheet (`kael_the_unyielding_idle.png`): 9.4 MB packed to 1,004,264 bytes. The link from the cache artifact to the sheet is by size and time, not proven (review 1). Extrapolated over 338 sheets the player falls to about 0.4 GB unzipped. That is an extrapolation from one sheet. The result is visibly softer: hair and armour trim smear (`shots\player-size\kael_before_after.png`).
- Not recommended: lowering `maxTextureSize`. The policy script's comment says it shears the frame grid. That claim is unverified: nobody measured it, and by the script's own model a halving is an integer 2x and does not shear. What it certainly does is halve the texel density, which is the blotch problem again (review 1).
- The importer metas are owned by `Tools/texture-import-policy.py`, so the durable form of the change is the policy edit and not a hand edit of the metas, which the next policy run would undo.
- Real save hash before and after every Unity run: unchanged.

## Method notes

- Fusion table: `data\fusion-r16.csv` (the simulation's in-memory candidate on 20 walks). The harness figures in section 1 are the harness's own 20 fusing walks.
- Guard and curve tables: 60 reference walks, `WalkSeed(0..59)`, instrument `--r13-tails`; CEIL from `CheckArmoredCurveDelta`.
- The R13 instruments live on a local scratch branch and are not on main.
- Not measured: the CEIL column on the new engine, the curve after, the wheel on the merged tree.
