# Solve prerequisites: the instruments the campaign solve reads

Measured 2026-09-23, on the 72-stage campaign as merged, before any solve. **A measurement record
with one fix in it.** It changes the reference player's line selection (item 2) and retires three
dead 1-1 pins (item 3). It changes no content, no formula and no baseline.

The instrument is `DevHarness.exe <content> --solve-prereqs` (`Tools/DevHarness/SolvePrereqs.cs`). It
asserts nothing and writes nothing. `speed` and `refline` run one half each, `quick` runs everything
at a quarter of the samples, and the full run takes about five minutes.

**MEASURED** means a number this instrument or `Tools/build-check.ps1` produced on this tree. **READ**
means a claim taken from code or content with no run behind it. Every delta is **paired**: the same
seeds in both arms, the mean of the per-seed difference, plus or minus one standard error. "WR" is
win rate, and "the CEIL" is a stage's last battle, the one the CEIL band grades.

## The four answers

1. **Speed's price shape costs every power-score consumer little, and the solve inherits less of it
   than the others.** At equal score, moving a line's Speed by ±10% moves a close campaign fight by
   about ±3.5 points, a close guild floor by about ±1.5, and a Proving fight by nothing this
   instrument can separate from zero. The solve fits its levels by fighting, so the Speed error
   reaches `recommendedPower` as a number but not the CEIL band. Proposals and a recommendation
   are in section 1.
2. **The reference player now fields a role-sane line**, and it was the larger error by a wide
   margin. At equal power the old line won the CEIL 17 to 46 points less often than the instrument
   line the levels are tuned against. The new one wins 5 to 18 points less. 16 of 20 old walks stuck;
   0 of 20 new ones do.
3. **All three 1-1 pins are retired**, including the purse pin, which the task did not name. The claim that should outlive them
   is already a check.
4. **Two shipped formulas take the global stage index**, armor's temper weights and Relic Dust per repeat
   clear. Around them sit six more tables and comments sized to 24 stages, plus the harness's
   chapter-1-versus-later split. Section 4 lists each with its value at stage 24 and at stage 72.

---

## 1. Speed's price shape (measure and propose; the formula is not changed)

`StatMath.PowerScore` is `0.11 HP + 2.4 Atk + 1.35 Def + 1.9 Spd + 260 Crit + 120 (CritDmg - 1)`
(READ). `Docs/TEMPO-STUDY.md` found Speed's value is a rank against the opposing line, which pays
steeply until the line is faster and then goes flat, while the formula charges linearly.

### How it was measured

- **ISO-SCORE.** Each hero's built Speed is multiplied by 0.90 or 1.10 (`SpeedTimes`), and its
  `BaseAttack` is re-solved until that hero's score, armor included, is what it was. The largest
  residual over 72 lines is 13 points (MEASURED). The score cannot tell the arms apart, so any
  win-rate gap between them is difficulty the score does not see. For the guild dungeon the same
  move is made on the **enemies**, the side the floor builder scores.
- **The tempo study's own arms** are rebuilt on this content as a cross-check: +10% Speed with the
  score allowed to rise, and the Attack that raises the score by exactly as much.

### Where Speed sits (MEASURED)

- Hero Speed at Level 9, by star tier, runs from 72 to 127. Enemy `BaseSpeed` runs from 76 to 128,
  median 100.
- Built Speed grows 0.35 per Level for heroes and enemies alike, while HP, Attack and Defense grow by
  `growth` (READ: `StatMath.Build`). So Speed's share of a line's score falls as levels rise.
- The instrument line against each CEIL: the line's mean Speed is x0.94 to x0.98 of the enemies' in
  every chapter. Per stage it runs from x0.83 to x1.10 (10th to 90th percentile x0.87 to x1.08), and
  the line is the faster side at 22 of 72 stages. **Most of the campaign sits on the steep part of the
  curve**, as the tempo study found on 24 stages.

### What the mis-pricing costs each consumer

| Consumer | What the score builds | What the score implies | Measured as built | ±10% Speed at equal score |
|---|---|---|---|---|
| **The campaign** (`recommendedPower`, the solve) | the instrument line levelled to a stage's `recommendedPower` (`BuildPinnedLine`) | the CEIL band: 85%, 65% at a finale | 74.4% over 72 stages (58% to 84% by chapter), because the seeds are unsolved, not because of Speed | all 72 stages: -0.8 ± 0.5 / +0.5 ± 0.5. **The 13 close stages** (15% to 95% as built): **-3.5 ± 2.6 / +3.7 ± 2.6**, spread +7.1 ± 2.7 |
| **The Proving** (`ProvingGenerator.Generate`) | five random heroes, one Level solved to the player's bare score (`ProvingTargetPower`) | at standing 0, an even fight | 47% to 89% by player line and depth, 69% pooled over six rows (1,440 fights) | player side, pooled: +1.2 ± 1.2 / -0.5 ± 1.1. Not separable from zero |
| **The guild floor builder** (`GuildDungeonGenerator.BuildFloor`) | one enemy Level solved to `TargetPowerForFloor` | no stated target | 95.9% at the Level members fight at (40); 28.9% at Level 25 | enemy side, at Level 40: +0.5 ± 0.2 / -0.2 ± 0.2. **At Level 25: +1.8 ± 0.5 / -1.1 ± 0.4** (heroes' view) |

The last column is the pair (x0.90, x1.10). The campaign and the Proving read it on the player side,
and the guild reads it on the enemy side.

Reading the rows (MEASURED unless marked):

- **The campaign.** Speed moves a winner only in a fight near even (`TEMPO-STUDY.md` section 4). Most
  seeded stages read 0% or 100%, which is why the 72-stage pool reads near zero. The 13 close stages
  are what every stage becomes after a solve. So on this content the Speed error is about ±3.5 points
  of CEIL rate for ±10% of line Speed, and the stage spread (x0.87 to x1.08) makes that about ±3 to 4 points between a typical stage and one at either end. **The solve inherits less of it than that**
  (READ: `SolveStageLevels` fits each stage's levels by measuring the CEIL rate, not by trusting a
  score). The Speed error reaches `recommendedPower` only as a number: the power band's ratio and
  the solver's entry ratio. It does not reach the CEIL band.
- **The Proving.** Across the six rows, win rate at "an even fight" ranges from 47% to 89% by which
  line the player fields. That is a composition error, and it is far larger than the Speed error. Opponents
  drawn faster than the player were beaten more often than slow ones: 63% against 38% in the pinned
  line's 1-12 row, rank correlation +0.24 pooled. **The iso-score arm, which is the clean test, does
  not attribute that to Speed.** Speed-heavy templates carry less of everything else at one score,
  and which heroes were drawn is confounded with it. Not established.
- **The guild dungeon.** The enemy lines are x1.06 to x1.30 of the member lines' Speed, which is past
  parity from the enemy side. Even there, trading enemy Speed for Attack makes the floor slightly
  easier rather than harder. A pure "past parity Speed is worth nothing" knee on the line mean is
  therefore **not** what this reads: interleaving is per unit, not per line mean.

**Other readers of the score, found by looking** (READ):

- `PlayerProfile.TeamPower`, the bare score, is what `CampaignScreen` and `FormationScreen` display.
  `EffectiveTeamPower` (armor and relics included) is read only by the harness. No screen displays
  `recommendedPower` any more.
- `ProvingGenerator.PowerOf` scores ghosts and snapshots, and `TeamSnapshot` sends the result to the
  server as `power_score`. `FriendTeamScreen` and `GuildScreen` display it. It is display only: no
  query orders or matches on it.
- `RecitalGenerator` does not read power. Its level is a formula of floor and cycle.
- In the harness, beside the solver and the power band, `CheckFusionEconomyDerivation`'s cap
  derivation reads entry power through the score. So does the wall rule, through its entry ratios
  (`WallResolve.cs`).

### Correction to the premise, volunteered

The task's premise is that "+10% Speed bought +7.2pp where the same power in Attack bought +4.2". On all
72 stages the rebuilt arms read **+1.4 ± 0.5 against +1.6 ± 0.3** (MEASURED), which would say there
is no gap. **That pooled reading is misleading, and an interim version of this report leaned on
it.** On the 13 close stages the arms read +7.9 ± 2.6 against +6.9 ± 1.4. Chapter 2 alone reads
+5.4 against +2.5. Both are consistent, within noise, with the tempo study's 3-point gap. The
premise holds where the fights are close. It is smaller than the headline numbers suggest, because
the steep-part price of 1.9 is close to right. The error sits past parity.

### Proposals

Each changes `StatMath.PowerScore` for heroes and enemies alike.

**A. A soft knee against a reference Speed.** Charge `1.9 * min(S, R) + k * max(0, S - R)`, where
`R` is a reference enemy Speed at the unit's own Level. One definition: the median enemy `BaseSpeed`
in `enemies.json` (100) plus 0.35 per Level, which is exactly how built Speed grows. `k` is well
under 1.9 (0.5 as a start). This keeps one number per line with no opponent needed, prices the
steep part as now, and discounts only what the tempo study measured as flat.

**B. A hard cap at the same reference.** `k = 0` in A. It is simpler, but it prices past-parity
Speed at nothing, and the guild reading shows past-parity Speed still pays a little. B would
underprice fast enemy lines in the guild dungeon.

**C. Price Speed against the actual opposing line.** Charge Speed by how many opposing units it
outspeeds. That is only possible where the opponent is known when the score is taken: the Proving
target, where the player's line is known. It is not possible for a guild floor, which is built from
a seed before anyone fights it. It also makes power a function of two lines, so the number on the
Formation screen stops being one number. **Not recommended.**

**D. Leave the formula.** The campaign levels are fitted by fighting, and the Proving and guild
readings are small against their other errors. Spend the solve's effort there instead.

**Recommendation: D for the solve, and A if the shape is fixed at all, after the solve and not
inside it.** The measured cost is about ±3.5 points in the closest campaign fights, and the solve's
CEIL fit already absorbs it. Folding a formula change into the solve would move every
`recommendedPower` for two reasons at once, and the solve's report could not say which moved what.
If A is picked, fit `R` and `k` against this instrument's close-stage and guild readings, not
against the tempo study's 24.

**What each would break** (READ, from the code paths named):

| | A or B | C | D |
|---|---|---|---|
| **Content version** | **Does not move.** The formula is code, and `ContentDatabase.ContentVersion` hashes the eight JSON files only. Putting `R` and `k` in `balance.json` would move it, and should. | same | nothing |
| **Stored guild replays** | Every floor's Level is solved to a score (`GuildDungeonGenerator.SolveLevel`), so the same seed builds different floors. With the version unmoved, only `GuildDungeon.Replay`'s outcome comparison stands between an old record and a replay of the wrong fight. It shows "Diverged" when victory, rounds or fallen disagree, and would show a different fight on a coincidental match. A live cycle's floors also change mid-cycle for members who have already fought them. **Ship with a version move.** | the Proving only; floors untouched | nothing |
| **Existing `recommendedPower`** | Every value moves: entry power is scored. The power band's ratios and the cap derivation (`CheckFusionEconomyDerivation`, x7.611 pinned from an entry power) move with it. The solve re-derives all of it anyway. | untouched | nothing |
| **Pending Proving opponents** | A saved `ProvingOpponent` keeps its units, levels and stored `PowerScore` (`SaveSerializer`), so the fight is unchanged. The power it displays is stale until the next opponent is generated, and snapshots on the server keep their old `power_score` until republished. | same | nothing |

---

## 2. The reference player (fixed)

### What changed

The economy walk, which is both the reference player the solver derives `recommendedPower` from and
the fusing player, filled its line with its five highest-ceiling heroes regardless of role
(`EconomyDeployHighestCeilings`). It now fields `WantedLine` (`Program.cs`), deployed by
`EconomyDeployLine`:

- first the highest-ceiling **front** hero, a Bulwark or Vanguard (the tank pair, as `HeroRole`
  names it);
- then the highest-ceiling **Mender**;
- then the highest ceilings left, any role.

A job the roster cannot fill goes to the next ceiling. A challenger never takes the last front
hero's or the last Mender's slot unless it does that job too, and one that does that job replaces an
unwanted holder of it first. The old policy remains behind `_walkRoleBlind`, set by the instrument
and by `--role-blind` for measurement.

### What "sane" means, argued

It is not the best line. It is the least a new player does, read off what the game hands them.

- **The three starters are a Bulwark, a Striker and a Mender** (READ: `heroes.json`'s `startingHero`,
  Kael, Kiva and Isara). The first line anyone fields has someone in front and someone healing.
  Swapping a starter for a better hero of the same job keeps that line. Dropping the only healer
  for a fourth damage dealer is a choice the game never shows them. The old policy made it at 535
  of 848 stage entries (MEASURED).
- **Every line the campaign is tuned against keeps both jobs** (READ). The instrument line
  (`PinnedLine`) is a Bulwark, a Vanguard, a Mender, a Breaker and a Stalker. The balance line is two
  Bulwarks, two Menders and a Striker, and chapter 1's is a Bulwark, a Vanguard, a Mender, a Breaker
  and a Stalker. If the reference fields a line with no healer, a stage's number partly measures
  that line's missing healer.
- **Nothing beyond the two jobs is asked.** The other three slots still go by ceiling. A smarter
  player picks for synergy and would read easier than a floor player should.
- **Seating is not asked either.** The game seats a new account's starters by power (READ:
  `PlayerProfile.NormaliseTeam`), which puts Kiva, the Striker, in slot 1, the front. `FrontEnemy`,
  the default target mode, takes the lowest slot. No walk entry line in the measurement below had
  its tank in slot 1 (MEASURED, 70 of 70). A floor player plausibly never reorders, so the
  reference does not either. Seating the tank first is worth **+0.2 to +6.3 points** of CEIL rate
  at equal power: chapter 1 +6.3 ± 2.1, chapter 2 +2.1 ± 1.7, 3 +0.2, 4 +1.0, 5 +3.1, 6 +5.0 ± 1.8
  (MEASURED). If Matthias wants the reference to read the Formation screen's "slot 1 is the front",
  that is one more rule in `EconomyDeployLine`, and the solve should decide it before it runs.

### Measured (20 walks under each policy, never converting and never ascending)

| | role-blind (was) | role-sane (now) |
|---|---|---|
| walks stuck | **16 of 20**: 2-12 x6, 2-11 x5, 6-12 x4, 2-1 x1 | **0 of 20** |
| stages reached, median | 24 | 72 |
| stage entries with no Mender | 535 of 848 | 0 of 1,440 |
| stage entries with no front hero | 472 of 848 | 0 of 1,440 |

Chapter 1 is identical under both. The top ceilings there are the starters and two 4-stars, and
the line already holds both jobs. The policies part at 2-4. On walk 1 the old line was Kiva, Nysera,
Sylven, Merek and Altheon, with no Mender and no tank. The new one is Sylven, Kael, Isara, Merek and
Altheon.

**At equal power** (R3, MEASURED). Each policy's entry line is rebuilt at the stage's
`recommendedPower` and geared as the instrument line is. It fights the CEIL 40 times per stage:

| chapter | instrument line | role-blind | role-sane | sane minus blind |
|---|---|---|---|---|
| 1 (10 stages) | 49.8% | 62.3% | 62.3% | 0.0 |
| 2 | 65.2% | 47.9% (-17.3) | 57.5% (-7.7) | **+9.6 ± 1.5** |
| 3 | 83.8% | 53.1% (-30.6) | 74.8% (-9.0) | **+21.7 ± 2.2** |
| 4 | 83.3% | 48.8% (-34.6) | 78.1% (-5.2) | **+29.4 ± 2.4** |
| 5 | 77.1% | 51.5% (-25.6) | 68.5% (-8.5) | **+17.1 ± 1.7** |
| 6 | 79.0% | 32.7% (-46.3) | 60.8% (-18.1) | **+28.1 ± 2.1** |

In chapters 3 to 6 the role-blind line comes from the first walk that reached the stage. Only 8 of 20
did, the lucky ones, so this **understates** the old line's gap.

### The size of the error the solve was about to inherit

**`recommendedPower` itself barely moves on the first iteration** (R2 and R2b, MEASURED). Proposed
from the median entry power over every walk that reached the stage, the role-sane proposal over the
role-blind one averages:

| chapter | 1 | 2 | 3 | 4 | 5 | 6 |
|---|---|---|---|---|---|---|
| sane / blind, first iteration | x1.000 | x1.056 | x0.976 | x0.986 | x0.980 | x0.993 |

Entry power is what a walk has bought, and both policies buy about the same. Chapter 2's x1.056
comes from the stuck steps: five blind walks stuck at 2-11 have already lowered its ask by 1.05^5 =
x1.28 (x1.31 read at that stage).

**The error is in how the line fights at that power, and the solver meets it as stuck walks.** The
solver fits CEIL levels to the instrument line at `recommendedPower`. A reference line that fights
worse at the same power sticks, and the solver answers each stuck stage by lowering its ask one
`SolverStuckStep` (x1.05) per iteration, until the walk passes. R4 reads how far that would have
gone. It finds the multiple of `recommendedPower` at which each policy's line wins the CEIL as often
as the instrument line does at x1.00:

| chapter | instrument line at x1.00 | role-blind needs | role-sane needs | a blind-player solve lands at |
|---|---|---|---|---|
| 1 | 48.0% | x1.00 or less | x1.00 or less | same |
| 2 | 65.0% | **x1.122** | x1.022 | about x0.91 |
| 3 | 83.3% | **x1.131** | x1.034 | about x0.91 |
| 4 | 83.3% | **x1.150** | x1.042 | about x0.91 |
| 5 | 77.5% | **x1.156** | x1.040 | about x0.90 |
| 6 | 78.8% | **x1.131** | x1.066 | about x0.94 |

MEASURED: 20 seeds per stage per rung, at rungs x1.00, x1.05, x1.10, x1.15, x1.20, x1.30 and x1.45,
interpolated. The last column is DERIVED, not measured: role-sane's need over role-blind's.

**So the error the solve was about to inherit was about 9% of `recommendedPower`, in chapters 2 to
6, on every stage where a walk stuck.** The old reference needed 12 to 16% more power than the
instrument line to win the same CEIL. The solver would have granted that as two or three
`SolverStuckStep`s (1.05^3 = x1.158), asking about x0.90 to x0.94 of what it now will. It would
have been calibrating the campaign to a player who fields no healer. The role-sane reference still
needs 2 to 7% more than the instrument line: roughly one step, and part of that is seating (above).
Chapter 1 is untouched, because both policies field the same line there.

The old reference also stuck in chapter 2 on walk 1, and the solver takes proposals from walk 1
only. Against the old player, the solve would have produced **no proposal at all for 49 stages**
until chapter 2's asks had been stepped down far enough to let walk 1 through.

**What it does to the gate** (MEASURED, full `Tools/build-check.ps1` on this branch against the 7
failures of the merge): **5 failures, down from 7.**
- **Retired:** the opener pin (item 3).
- **Now green:** `CheckFusionNeverGates`. All 20 walks that never convert or ascend finish the
  campaign; 16 of 20 stuck before.
- **Still red, and reading much more:** `CheckCampaignPowerCurve`. Walk 1 no longer sticks at 2-12,
  so the band now reads every stage, and **38 are out of band**. Four are the ones it saw before:
  1-6 x1.182, 1-9 x1.007, 2-6 x1.227 (x1.207 before) and 2-12 x1.202 (not readable before). The
  other 34 are in chapters 3 to 6, and **every one is over its band**, by up to x1.496 at 6-1. The
  reference player arrives richer than the chapter 2 seeds those stages carry. That is what the
  solve moves.
- **Unchanged:** the two arena-matrix reds, Rough against Honed, and the geared CEIL band (first
  miss 1-6 at 99.5%).

---

## 3. The dead 1-1 pins (retired)

`CheckCampaignMigrationRegression` carried three spot-checks on `ch1_1`. All three were about the
retired 24-stage campaign's first stage, and Pinoy's Roadside Shrine replaced that stage under the
same id:

- **the opener**, two level-2 hollow husks and a level-2 grave moth. It has failed since the merge,
  because no stage fields either template;
- **`recommendedPower` 2977**, the campaign-curve pass's derivation for that stage. It passed only
  because `_author_campaign_stages.py` carries every number by position from the retired stage, so
  the seed happens to equal the old answer;
- **the purse, 120 gold and 6 shards**. The task did not name it, but it is the same case: a
  position-carried seed that the reward curve is meant to replace. It is retired with the other two.

Re-pinning any of them to today's values would pin a seed. The comment left in their place in
`CheckCampaignMigrationRegression` says so.

**What replaces them** (READ):

- The opener pin was a hand-readable instance of the all-stage check above it: wave 1 battle 1
  spawns the stage's own list, ids, order and levels, on all 72. That check stays and reads the
  opener from `campaign.json` itself.
- `recommendedPower` and the purse belong to the solve: the solver's table and the reward curve.
  From 1-5 onward the power band guards `recommendedPower`. **1-1 to 1-4 are the tutorial
  (`EconomyTutorialStages`), and no band reads them.**
- The one claim about 1-1 that is design rather than a number is already a check:
  `CheckTeamSizeStructure`'s "a brand new account's 3 starters can win stage 1". It reads the opener
  only and asks for one win in 20.

**Proposed for the solve, not built:** give the tutorial a band. The natural claim is that the three
starters at the Level the starting purse buys clear each of 1-1 to 1-4's CEIL at the ordinary
target, rather than winning one opener in twenty. That is a design claim about the first ten minutes
for Matthias to make.

---

## 4. Tables tuned against 24 stages (measure and list; nothing retuned)

Stage 24 is 2-12, global index 23. Stage 72 is 6-12, global index 71. All READ, with the arithmetic
done by hand from the numbers quoted.

### Formulas of the global stage index, in shipped code

| # | Where | Formula | Stage 24 | Stage 72 | Where it breaks |
|---|---|---|---|---|---|
| 1 | `armor.json` `tempers[].dropWeight` + `dropWeightPerStageIndex`, read by `ArmorRoller.RollTemper` | `w = dropWeight + slope * index`; a `w` under 1 **is floored to 1**, never 0 | Rough 72 - 0.42 x 23 = 62.3, so 62. Honed 20 + 0.16 x 23 = 23.7, so 24. Rough is **72.1%** of drops | Rough 42.2, so 42. Honed 31.4, so 31. Rough is **57.5%** | Nothing now. Rough never reaches its floor: the shallowest it gets is 42 against 31 at 6-12. |
| 2 | `balance.json` `relicDustPerRepeatClear` 6 + `relicDustPerStageIndex` 2, read by `GameSession.RelicDustForRepeatClear` | `6 + 2 * index`, no cap | 52 a clear, 156 a day at the 3-clear cap | 148 a clear, **444** a day | `relicMythicShopPrice` 2400 was sized in its comment as "156/day at the final stage (globalIndex 23)", about 15 days. At 6-12 it is 5.4 days. `relicShopPrice` 80 goes from 1.5 clears to 0.54. |

**One more, found by the solve itself: `SolverMaxIterations`.** It was 40, the 24-stage campaign's
number, and on 72 stages it cut a *converging* run off five passes short - stages-moved fell
monotonically from 56 at iteration 10 to 5 at iteration 40, and the run then aborted and wrote
nothing. It is a guard against divergence rather than a budget (the loop stops the moment a walk
proposes nothing), so it is now 120 and a converging run pays nothing for the headroom.

**And its twin, `SolverMaxProbesPerStage`.** It was 60, and it is the same mistake one level down:
a guard against a search that will not converge, sized as though it were a budget. The level search
spends one probe per unit per step, so 60 buys twelve single-unit steps on a five-unit roster and
fifteen on a four-unit one - enough when a seed lands near the band, and nothing like enough when it
does not. On the solved table, seven of the 67 graded stages spent the whole 60 without one probe
ever reading under the band's high edge - 3-8, 3-9, 4-3, 4-4, 4-7, 5-3, 5-7 - and because the search
only keeps a candidate that strictly improves its margin, all seven kept the levels they started
from and reported no movement at all. Unbounded, all 67 converge; the worst, 3-8, needs 68
single-unit steps and 171 probes to walk 100% down to 85.8%. It is now 400. A whole pass over the
graded stages costs 1,672 probes unbounded against 1,189 at 60, because only the truncated seven pay
anything.

`DevHarness --probe-study` is the instrument that says this: it re-runs the level search alone on
every graded stage from the committed table and prints, per stage, the probes spent and whether the
exit was `converged` or `BUDGET`. Re-run it before changing that constant again.

**The escalating grant, and why the solver does not use it.** 400 on every stage reads like a
tax on the 60 that never needed it, and the obvious repair is a ladder: start every stage at 60,
grant double to a stage that spends its grant without reaching the band, up to 400. That ladder is
built - `SolverProbeGrants` and `SolveStageLevels`, with the single-grant search under it as
`SolveStageLevelsAt` - and `--probe-study` runs it and a flat ceiling over the same table and
prints both totals. The solver itself calls the flat path, for two reasons that are structural
rather than a matter of taste.

**A flat ceiling is never charged to a stage that converges.** The grant is read in exactly one
place, the `while` in `SolveStageLevelsAt`, and the search leaves it the instant a candidate crosses
the target or sits `SolverBandCushion` inside both edges. It is a stopping condition, not a quota.
60 of the 67 graded stages converge inside 55 probes and spend those same 55 at any ceiling above
55. There is no tax on those 60 to repair.

**And on the seven that do not converge, the ladder is strictly worse, because a grant can only be
found too small by spending it.** A retry restarts the walk from the seed - the walk is
deterministic in the stage's own seed, so a stage re-solved at 120 re-fights the identical 60 probes
it already fought before it sees new ground. Against the per-stage figures measured in the previous
row, taken from the flat-400 run:

| stage | probes it needs | flat | ladder | wasted |
|---|---|---|---|---|
| 3-8 | 171 | 171 | 60 + 120 + 171 = 351 | 180 |
| 4-7 | 145 | 145 | 60 + 120 + 145 = 325 | 180 |
| 3-9 | 136 | 136 | 60 + 120 + 136 = 316 | 180 |
| 5-7 | 125 | 125 | 60 + 120 + 125 = 305 | 180 |
| 4-3 | 114 | 114 | 60 + 114 = 174 | 60 |
| 5-3 | 112 | 112 | 60 + 112 = 172 | 60 |
| 4-4 | 100 | 100 | 60 + 100 = 160 | 60 |

Over a whole pass: **1,672 probes flat against 2,572 under the ladder, for identical landings** -
the ladder costs 900 more, 1.54x. The flat-60 and flat-400 totals are measured (`--probe-study` at
each budget, 2026-09-24); the ladder total is arithmetic on that run's per-stage spends, and it is
exact rather than an estimate, because a truncated search stops at precisely its grant and a restart
reproduces the walk it already made.

**The one escalation that wastes nothing is not an escalation.** Resuming a walk instead of
restarting it would drop the 900, and it would drop the ladder with it: since the grant only gates
that one loop, a walk resumed under a raised ceiling visits exactly the probes, in exactly the
order, that a single flat search at the final rung would have visited. Escalate-by-resume is not
cheaper than a flat ceiling; it is a flat ceiling, spelled across four calls.

The ladder is kept anyway. It costs nothing unrun, and it is what `--probe-study` measures the flat
ceiling against, so the next session that finds 400 suspicious can re-derive this rather than take
it on trust.

**Correction to `CAMPAIGN-SPEC.md` 10.8 row 6, and to the task that commissioned this.** Both said Rough reached zero at
3-11 and that no later stage dropped it. The weight is floored at 1 (`ArmorRoller.RollTemper`), so
every stage still dropped Rough - at 4.2% at 3-11 and 5.9% at 6-12. Either reading gave the same
failing check: Rough 537 against Honed 1,599 over the drop sweep.

**Since fixed.** The slopes were the bug, not the floor. Rough is now 72 with a slope of -0.42 and
Honed 20 with +0.16, so depth still tilts the draw toward Honed - 78% Rough at 1-1, 58% at 6-12 -
without ever inverting the ladder. The sweep reads about 2.2 Rough for every Honed, and
`CheckArmorDropDiversity` passes. The row above is stated against the fixed weights.

### Per-stage tables and derivations sized to 24 stages

| # | Where | Stage 24 | Stage 72 | What is 24-shaped |
|---|---|---|---|---|
| 3 | Idle income, `campaign.json` `idleGoldPerHour` / `idleShardsPerHour`, read by `StatMath.IdleGoldPerSecond` at the last cleared stage | 15,659 gold, 78 shards an hour | the same 15,659 and 78 | The derivation rule in `campaign.json`'s comment is written for chapters 1 and 2 only (0.80 x gold; 0.85 x gold x 1.16^(index - 1)). Chapters 3 to 6 are flat, so idle income stops growing at stage 24. Carrying chapter 2's exponent on is not an option: it compounds past any sane rate. |
| 4 | Stage purse and first-clear rewards, `campaign.json` | 3,600 gold, 180 shards, 500 account XP; first clear 6,100 gold, 1,040 shards | identical on all 48 stages of chapters 3 to 6 | The placeholder plateau in `CAMPAIGN-SPEC.md` 10.3. |
| 5 | Level caps and ascension, `balance.json` `fiveStarLevelCaps` [9, 18, 39] and `ascensionCurveMultipliers` [1, 2.035, 6.175] | derived from 2-12's entry of 27,187 over the 3,572 starting line: x7.611 | `CheckFusionEconomyDerivation` now reads 6-12 as the finale, but the growth stays **pinned** at 2-12's x7.611 (`CapDerivationGrowth`) | Every cap is 35% of a 24-stage campaign's growth. |
| 6 | `levelCostA` / `levelCostB` comment, `balance.json` | "ends the campaign around level 30 ... cleared all 24 ceilings" | not measured | The cost curve is a function of level, not stage. The claim that justifies it is about a 24-stage campaign. |
| 7 | Main menu chapter card, `MainMenuScreen.RefreshChapter` | reads `chapters[0]`: "12 / 12" | still chapter 1, "12 / 12" | It never shows chapters 2 to 6. |
| 8 | Campaign chapter tabs, `CampaignScreen.BuildChipRow` | a 1,600-wide row holding 2 tabs of 300 with gaps of 16: 616 | 6 tabs: 1,880, wider than the row | Not captured; READ from the metrics only. |

**Not stage-dependent** (READ): summon rates and pity (counted in summons), `events.json`,
`relics.json`, `floor-conditions.json` and `heroes.json` (no stage keys). The Proving scales from
the player's own line. The Deep Recital's level is `20 + 2 x (floor - 1) + 3 x cycle`. The guild
dungeon uses fixed values: floor one 21,000, +5% a floor, members at Level 40. No feature is gated on
`StagesCleared`. `CampaignWaveGenerator.LevelForBattle` ramps within a stage and never reads the
global index.

### In the harness (secondary, but the solve reads them)

| Where | At 24 | At 72 |
|---|---|---|
| `BandFor` and `SolverBandMiddle` split chapter 1 from "later" | "later" meant chapter 2: walls x0.95 to x1.02, solver middles ordinary x1.15, wall x0.985 | chapters 3 to 6 all inherit chapter 2's band and middles |
| `IsChapterFinale`: `CeilTargetFinale` 0.65, band 0.55 to 0.72; `PaceFinaleExtraMin/Max` 2 to 4 sittings | two finales | six finales, one target |
| `CheckEconomyRespondsToIncome` scales `AllStages[Count / 2 ..]` | indices 12 to 23, chapter 2 | indices 36 to 71, chapters 4 to 6 |
| `TempoPowerPrice`'s mid-stage sample at `Count / 2` | 2-1 | 4-1 |
| `ExpectedWaveCount` | 3 waves at 1-1 to 1-4, 6 in the rest of chapter 1, 12 after | unchanged, 12 everywhere past chapter 1 |

## Limits

- The Speed arms read only the CEIL battle, with instrument and reference lines, and 13 close stages
  is a small pool, hence ±2.6.
- The guild's close pass lowers members to Level 25, which is not where they fight. It shows the
  slope, not the live error.
- The Proving rows are three depths and two lines, at standing 0 only.
- The reference-player gap in chapters 3 to 6 compares the new line against the old line's luckiest
  walks.
- Nothing here was solved. Every number marked "stage 72" reads unsolved seeds.
