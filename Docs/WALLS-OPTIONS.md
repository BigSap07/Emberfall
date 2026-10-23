# Campaign walls: three options, measured

Measurement record of 2026-10-23. It answers the owner's question: should the campaign walls stay nearly free,
or should each one cost a non-spender time? It changes no game file. Each option is a patch in `Docs/walls/`.

## The short answer

**Keep the walls as they are (option A).** Options B and C were built and measured. Each one reaches its target
median at most walls, but the tail does not hold. Under B, 39 of 60 non-spender walks stop for good at some wall.
Under C, 59 of 60 do. The whale passes nearly every wall in B (5 of 60 stuck). On the gate's own 60 walks, B and C
leave every one of them stuck. The reason is structural, not a tuning miss: from chapter 2 on, the reference non-spender's power grows by
0.06 to 0.5 percent a day. One or two days of idle buy the non-spender less than 1 percent of power. Between
walks, entry power at the same wall spreads by 5.5 to 8 percent (p10 to p90). So a wall that takes "1 to 2 days of idle and upgrades" is a
roster lottery, and buying power is the reliable way through it. The power lead stays under the R24 limits in
every option. The lead in outcomes does not.

## What was measured, and how

- **The instrument** is the R24 whale curve in `Tools/DevHarness/R24WhaleCurve.cs` (`WhaleCurveColumn`), run on
  the R16 reference walk (`SimulateEconomyUnder`) at `WalkSeed(0)` to `WalkSeed(59)`: **60 walks per column**.
  It uses the season calendar's clock (`StoreWalkClockStart`), so weeks, months and Road seasons are real.
- **The buyers** are the study's three columns. *Non-spender* = `ref:non`. *Spender* = `ref:spend`, the R19 set
  (Starter Pact, Growth Fund, Ember Ledger, Weekly and Monthly Packs, pass premium). *Whale* = `ref:whale`: the
  spender plus everything R24 added (Hero Contract with its cap, relic packs, paid Event Tracks, event packs,
  skins). Every buyer spends the Relic Dust they hold by the study's relic policy (`WhaleRelicStep`), and the
  non-spender's Dust comes from play and the free Road.
- **The daily loop and the free Road are ON for every buyer.** The study as committed does not claim orders, so
  this run adds one measurement flag, `--whale-loop`. It sets `_walkLoopMode` to 2 for the column, which turns on
  the diligent player of `Docs/DAILY-LOOP-CURVE.md` (`DailyLoopWalkStep`): every order, both chests, the calendar
  and every reached free Road tier, claimed at the start and the end of each sitting. The flag is in
  `Docs/walls/walls-study-tooling.patch`; it is not committed to `Tools/`.
- **The reference player** never converts and never ascends: the wall rule's player, the one `recommendedPower`
  is solved for. The fusing player (`fuse:*`) was also walked for A and B, for the R24 fusing limits.
- **Units.** A sitting comes every 12 hours (`EconomyAwaySeconds`), so 2 sittings make 1 day. A sitting fights
  up to 40 battles (`EconomySessionBattleBudget`) and ends after 8 losses in a row. **Attempts per wall** means
  *extra sittings*: sittings at the wall beyond its own ladder floor (`WallExtraSittings`, the number pace
  target B grades, and the one that went "from 30 to 1" in R16). **Stuck** means the walk gave up: 14 sittings
  (7 days) in a row with no power gain and no roster change (`EconomyStallSessions`). A real player would not
  quit, but would wait on a lucky summon with nothing else to do. The design rule says that must never happen to
  the reference player (`CheckFusionNeverGates`).
- **The walls** are the 11 stages the CEIL band grades as walls: `1-12`, and stages 6 and 12 of chapters 2 to 6.
  `1-6` is a miniboss, but the band reads chapter 1's first six stages as breezy, so it is left alone in every
  option.
- **Days per chapter** is the median over walks of the day the chapter's last stage was cleared, and of the days
  between entering the chapter and clearing it. Where some walks are stuck, the median is over the walks that
  got there, and the count is given.
- **Lead** is the median entry power at the wall over the non-spender's median entry power, as `WhaleReport`
  prints it. Where walks are stuck, it is over the walks that reached the wall.

## Where the lever is: not in balance.json

**None of the three options changes `balance.json`.** It has no per-stage or per-wall key. Its global keys
cannot make a wall cost idle time, because by chapter 2 the reference line is at its star-gated level caps: more
Gold buys nothing (`Docs/DAILY-LOOP-CURVE.md`: 104 line levels against 103, "capped, not poor"). The caps
themselves (`fiveStarLevelCaps`, `levelCapsBelowFiveStars`) are derived and asserted by
`CheckFusionEconomyDerivation`. A wall's difficulty lives in `campaign.json`: its `floorLevel`, its
`ceilingLevel` and its enemies' levels. Battle `b` of a stage fields each enemy at its authored level plus
`LevelForBattle - floorLevel` (`CampaignWaveGenerator.BuildBattle`).

**The shape used for B and C is a flat wall at a raised level.** Two shapes were tried:

- **Raise only the ceiling.** This makes the last battle a cliff, and the cost is a pure lottery. With every
  wall's ceiling 6 levels up, 2-12 cost a median 23 extra sittings over 8 walks and 3 of the 8 stuck at 1-12. With
  the minibosses' ceilings 10 levels up (finales 1 level), 8 of 12 walks stuck.
- **A flat wall.** Floor and ceiling both go to the old ceiling plus `d`, and each enemy rises by the same amount
  as the floor. Every battle is then as hard as the old last battle plus `d`, so the cost is spread over 144 fights
  instead of one. Tails are shorter at the same median, so B and C use this shape.

`recommendedPower` is unchanged in every option. Entry power does not move when a wall gets harder, so the power
band (`CheckCampaignPowerCurve`) reads the same.

## Option A: keep as is

**Changes:** none (`Docs/walls/walls-option-A.patch` is intentionally empty).

Stuck walks (of 60): non-spender 0, spender 0, whale 0.

| Chapter | Non-spender: finished on day / days in chapter | Spender | Whale |
|---|---|---|---|
| 1 | 10.5 / 10.5 | 10.3 / 10.3 | 10.2 / 10.2 |
| 2 | 35.6 / 24.8 | 35.0 / 24.8 | 35.0 / 24.7 |
| 3 | 60.3 / 24.8 | 59.8 / 24.8 | 59.7 / 24.7 |
| 4 | 85.0 / 24.7 | 84.5 / 24.7 | 84.3 / 24.7 |
| 5 | 109.7 / 24.7 | 109.1 / 24.7 | 109.0 / 24.6 |
| 6 | 134.4 / 24.7 | 133.8 / 24.7 | 133.6 / 24.7 |

| Wall | Non-spender extra sittings: median / p90 / worst | Spender | Whale | Whale lead | Spender lead |
|---|---|---|---|---|---|
| 1-12 | 0 / 2 / 4 | 0 / 0 / 1 | 0 / 0 / 0 | +11.6% | +9.8% |
| 2-6 | 0 / 0 / 0 | 0 / 0 / 1 | 0 / 0 / 0 | +3.4% | -0.2% |
| 2-12 | 0 / 0 / 1 | 0 / 0 / 0 | 0 / 0 / 0 | +4.4% | +1.0% |
| 3-6 | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 | +4.8% | +0.7% |
| 3-12 | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 | +4.4% | +0.8% |
| 4-6 | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 | +4.3% | -0.6% |
| 4-12 | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 | +4.3% | -0.6% |
| 5-6 | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 | +4.7% | +0.4% |
| 5-12 | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 | +3.5% | -0.1% |
| 6-6 | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 | +3.7% | +0.1% |
| 6-12 | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 | +2.8% | -0.4% |

The pace comes from the ladder. A stage of 144 battles at 40 battles a sitting takes 4 sittings, which is
2 days, so a chapter takes about 24.7 days for every buyer. The whale finishes the campaign 0.8 days before the
non-spender. With the loop and the Road on, the whale's lead from 4-12 on is +2.8 to +4.7 percent. The R24 record
without the loop was +5.6 at 4-12, so the loop narrows the lead, because it pays the non-spender too.

## Option B: moderate, 1 to 2 days per wall

**Changes** (`Docs/walls/walls-option-B.patch`, `campaign.json` only): each wall becomes flat at its old ceiling
plus `d`. Every enemy of the stage rises by the floor's rise, so the floor battle is as hard as the new ceiling.

| Wall | Now: floor to ceiling | Now: enemy levels | B: floor = ceiling (d) | B: each enemy | C: floor = ceiling (d) | C: each enemy |
|---|---|---|---|---|---|---|
| 1-12 | 14 to 18 | 13/13/13/14/13 | 19 (+1) | +5 | 20 (+2) | +6 |
| 2-6 | 25 to 37 | 24/23/23/25/23 | 42 (+5) | +17 | 43 (+6) | +18 |
| 2-12 | 20 to 21 | 19/20/20/20/20 | 25 (+4) | +5 | 26 (+5) | +6 |
| 3-6 | 31 to 43 | 31/30/30/31 | 49 (+6) | +18 | 50 (+7) | +19 |
| 3-12 | 22 to 23 | 21/21/22/22/22 | 28 (+5) | +6 | 30 (+7) | +8 |
| 4-6 | 31 to 44 | 30/31/31/31 | 51 (+7) | +20 | 52 (+8) | +21 |
| 4-12 | 24 to 25 | 21/22/21/24/22 | 31 (+6) | +7 | 33 (+8) | +9 |
| 5-6 | 32 to 46 | 32/31/29/30 | 54 (+8) | +22 | 56 (+10) | +24 |
| 5-12 | 21 to 22 | 21/19/21/19/20 | 28 (+6) | +7 | 30 (+8) | +9 |
| 6-6 | 32 to 44 | 32/31/32/31 | 51 (+7) | +19 | 53 (+9) | +21 |
| 6-12 | 23 to 24 | 21/21/22/21/23 | 31 (+7) | +8 | 33 (+9) | +10 |

The `d` values were calibrated in steps of 12 walks toward a median of 2 to 4 extra sittings per wall, then
measured at 60.

Stuck walks (of 60): **non-spender 39**, spender 31, whale 5. The non-spender is stuck at every one of the 11
walls in some walk, most often at 6-6 (14 walks). The whale is stuck only at 6-6 and 6-12.

| Chapter | Non-spender: finished on day / days in chapter | Spender | Whale |
|---|---|---|---|
| 1 | 11.8 / 11.8 (59 walks) | 10.8 / 10.8 (59 walks) | 10.8 / 10.8 |
| 2 | 45.3 / 34.4 (53 walks) | 40.9 / 30.1 (54 walks) | 37.1 / 26.3 |
| 3 | 75.2 / 28.7 (48 walks) | 71.0 / 26.9 (53 walks) | 62.2 / 25.2 |
| 4 | 106.6 / 27.3 (43 walks) | 102.3 / 28.6 (46 walks) | 87.8 / 25.2 |
| 5 | 132.9 / 27.3 (41 walks) | 127.3 / 26.2 (45 walks) | 113.2 / 24.7 |
| 6 | 166.2 / 32.0 (21 walks) | 154.9 / 29.3 (29 walks) | 139.4 / 25.8 (55 walks) |

| Wall | Non-spender extra sittings: median / p90 / worst | Spender | Whale | Whale lead | Spender lead |
|---|---|---|---|---|---|
| 1-12 | 3 / 8 / 22 (+1 stuck) | 1 / 3 / 7 (+1 stuck) | 1 / 1 / 12 | +11.6% | +9.8% |
| 2-6 | 3 / 34 / 69 (+2 stuck) | 2.5 / 29 / 65 (+1 stuck) | 1 / 5 / 24 | +3.4% | +0.9% |
| 2-12 | 7 / 26 / 57 (+4 stuck) | 3 / 20 / 44 (+4 stuck) | 1 / 4 / 10 | +3.5% | -0.2% |
| 3-6 | 2 / 18 / 39 (+3 stuck) | 2 / 18 / 35 (+1 stuck) | 0 / 1 / 34 | +3.8% | +0.2% |
| 3-12 | 3 / 12 / 26 (+2 stuck) | 2 / 14 / 33 | 0 / 1 / 3 | +3.2% | -0.2% |
| 4-6 | 2 / 7 / 29 (+3 stuck) | 3 / 12 / 34 (+2 stuck) | 0 / 2 / 8 | +3.2% | -1.0% |
| 4-12 | 3 / 7 / 61 (+2 stuck) | 3 / 13 / 28 (+5 stuck) | 1 / 2 / 4 | +3.1% | -0.7% |
| 5-6 | 2 / 10 / 16 (+1 stuck) | 1 / 3 / 5 | 0 / 1 / 3 | +2.9% | -0.2% |
| 5-12 | 2 / 6 / 14 (+1 stuck) | 1 / 3 / 5 (+1 stuck) | 0 / 2 / 5 | +2.7% | -0.5% |
| 6-6 | 5 / 23 / 25 (+14 stuck) | 4 / 25 / 31 (+14 stuck) | 1 / 5 / 19 (+4 stuck) | +2.5% | -0.1% |
| 6-12 | 6 / 18 / 20 (+6 stuck) | 4 / 9 / 27 (+2 stuck) | 1 / 3 / 10 (+1 stuck) | +2.4% | -0.1% |

The medians mostly land on target: 2 to 3 extra sittings (1 to 1.5 days) at 8 of the 11 walls, and 5 to 7 at
2-12, 6-6 and 6-12. The p90 is 6 to 34 sittings (3 to 17 days), and two thirds of the non-spenders never finish.
The spender is barely better. The whale pays a median of 0 to 1 extra sitting and finishes on day 139.4.

## Option C: classic idle RPG, 2 to 4 days per wall

**Changes** (`Docs/walls/walls-option-C.patch`, `campaign.json` only): the same flat shape, one or two levels
harder than B at each wall. See the C columns of the table under option B.

Stuck walks (of 60): **non-spender 59**, spender 57, whale 39.

| Chapter | Non-spender: finished on day / days in chapter | Spender | Whale |
|---|---|---|---|
| 1 | 13.3 / 13.3 (59 walks) | 13.1 / 13.1 (50 walks) | 11.3 / 11.3 (56 walks) |
| 2 | 55.5 / 37.8 (52 walks) | 48.8 / 36.6 (41 walks) | 40.7 / 27.9 (48 walks) |
| 3 | 87.9 / 31.4 (33 walks) | 80.8 / 30.8 (27 walks) | 67.4 / 26.3 (44 walks) |
| 4 | 115.1 / 28.9 (19 walks) | 110.2 / 27.8 (13 walks) | 94.8 / 26.8 (41 walks) |
| 5 | 145.5 / 27.8 (12 walks) | 140.2 / 29.3 (10 walks) | 120.4 / 26.2 (37 walks) |
| 6 | 161.0 / 37.9 (1 walk) | 168.9 / 29.8 (3 walks) | 149.2 / 29.3 (21 walks) |

| Wall | Non-spender extra sittings: median / p90 / worst | Spender | Whale | Whale lead | Spender lead |
|---|---|---|---|---|---|
| 1-12 | 5 / 36 / 44 (+1 stuck) | 5.5 / 16 / 28 (+10 stuck) | 2 / 9 / 11 (+4 stuck) | +11.6% | +9.8% |
| 2-6 | 6 / 38 / 66 | 3.5 / 35 / 79 | 1 / 18 / 84 (+1 stuck) | +2.7% | +0.4% |
| 2-12 | 10 / 46 / 96 (+7 stuck) | 11 / 39 / 63 (+9 stuck) | 2.5 / 13 / 41 (+7 stuck) | +2.5% | -0.2% |
| 3-6 | 3 / 19 / 45 (+1 stuck) | 3 / 11 / 34 (+1 stuck) | 0 / 2 / 14 | +3.3% | +0.2% |
| 3-12 | 13 / 44 / 61 (+18 stuck) | 8 / 25 / 49 (+13 stuck) | 3 / 7 / 10 (+4 stuck) | +3.6% | -0.4% |
| 4-6 | 2 / 4 / 7 (+2 stuck) | 3 / 8 / 63 (+2 stuck) | 0 / 2 / 3 | +2.9% | -0.4% |
| 4-12 | 7 / 31 / 40 (+12 stuck) | 4 / 16 / 30 (+12 stuck) | 3 / 8 / 16 (+3 stuck) | +2.3% | -0.2% |
| 5-6 | 2 / 6 / 46 (+1 stuck) | 1 / 3 / 3 | 1 / 1 / 3 (+1 stuck) | +0.8% | -0.7% |
| 5-12 | 3.5 / 7 / 9 (+6 stuck) | 7.5 / 13 / 15 (+3 stuck) | 3 / 5 / 20 (+3 stuck) | +1.5% | -1.0% |
| 6-6 | 3.5 / 19 / 19 (+4 stuck) | 4 / 7 / 7 (+5 stuck) | 4 / 12 / 17 (+9 stuck) | +1.1% | -0.9% |
| 6-12 | 25 / 25 / 25 (+7 stuck) | 7 / 9 / 9 (+2 stuck) | 4 / 10 / 16 (+7 stuck) | -1.0% | -3.8% |

C is not a playable campaign for the reference player: 1 non-spender in 60 finishes. From 3-12 on, the late rows
cover only a few survivors, so their medians and leads are survivor readings. A milder C is the region between B
and A, measured below.

## Between A and B: there is no gentle middle

Two weaker versions of B, every `d` lowered by 1 and by 2 (1-12 kept at +1), 60 non-spender walks each:

| Version | Non-spender walks that finish | Median finish day (finishers) | Wall medians, extra sittings (1-12, 2-6, 2-12, ... 6-12) |
|---|---|---|---|
| A | 60 of 60 | 134.4 | 0 at every wall |
| B minus 2 | 42 of 60 | 143.9 | 3, 1, 1, 2, 0, 1, 1, 1, 0, 2, 1 |
| B minus 1 | 30 of 60 | 152.4 | 3, 1, 1.5, 2, 1, 2, 2, 2, 1, 4, 3 |
| B | 21 of 60 | 166.2 | 3, 3, 7, 2, 3, 2, 3, 2, 2, 5, 6 |

**One wall alone, 30 non-spender walks**, with every other stage as shipped. The walks are identical to A up to
the wall, so this is exactly that wall's response:

| Wall made flat at old ceiling + d | d = 4 | d = 6 | d = 8 | d = 10 |
|---|---|---|---|---|
| 4-12: extra sittings median / p90 / worst; stuck | 1 / 2 / 2; 0 | 5.5 / 15 / 23; 2 | 19 / 51 / 63; 17 | none pass; 30 |
| 2-6: extra sittings median / p90 / worst; stuck | 1 / 6 / 39; 0 | 10 / 33 / 87; 1 | 23 / 64 / 83; 1 | 33 / 78 / 134; 15 |

At 4-12, two enemy levels separate "free for 40 percent of players" from "a median of 3 days with a 1 week
tail", and two more leave over half of them stuck. No setting gives every player 1 to 2 days.

## Why: the non-spender does not grow while waiting

Median growth in entry power per day over each chapter, option A, 60 walks:

| | Ch 1 | Ch 2 | Ch 3 | Ch 4 | Ch 5 | Ch 6 |
|---|---|---|---|---|---|---|
| Non-spender | 11.1% | 0.47% | 0.27% | 0.08% | 0.08% | 0.06% |
| Whale | 12.5% | 0.35% | 0.34% | 0.09% | 0.05% | 0.03% |

A classic idle wall is a power threshold that idle income carries the player across in a known number of days.
Here the line is level-capped by chapter 2, and idle Gold has nothing to buy. What still grows is the roster
(summons, Road selectors, armor drops, relics), and it grows in lumps, differently in every walk. So one or two
days of waiting raise the non-spender by under 1 percent, while entry power at the same wall spreads by 5.5 to 8
percent between walks (p10 to p90; 9 to 14 percent from the weakest walk to the strongest), and the line's
composition matters on top of its power. A wall
set to cost 1 to 2 days is therefore set inside the roster spread. A strong roster walks through it, and a weak
one waits for a summon that may not come within a week. The whale's 3 percent and its relics are enough to be on
the right side of that edge nearly every time.

## Effect on the spender and whale lead (the R24 limits)

The R24 limit (`DESIGN-BUILD-ORDER.md`, Late-game spending): from 4-12 on, a whale who buys everything leads a
non-spender by **under 6 percent at every wall**, and every wall still binds. The power lead, from 4-12 on:

| Option | Reference whale | Reference spender | Fusing whale | Fusing spender | Under 6%? |
|---|---|---|---|---|---|
| A | +2.8 to +4.7% | -0.6 to +0.4% | +3.8 to +4.7% | +1.6 to +2.1% | yes |
| B | +2.4 to +3.1% | -0.7 to -0.1% | +3.0 to +4.0% | +0.4 to +0.9% | yes |
| C | -1.0 to +2.3% | -3.8 to -0.2% | not walked | not walked | yes (survivor readings) |

Every option stays under the limit on paper. **But in B and C the power lead stops measuring the advantage.**
Entry power does not move when a wall gets harder, while the outcome splits: in B the whale finishes on day 139.4
with 55 of 60 walks through, and the non-spender on day 166.2 with 21 of 60. That is a 27-day lead for the walks
that finish, and a 34-walk lead in walks that finish at all. In A the same lead is 0.8 days and 0 walks. "Every
wall still binds" holds for the whale in B in the R24 sense (it still pays the 4-sitting ladder), but a wall the
whale passes in a day while two thirds of non-spenders stop there is a paid gate. B or C would need a lead limit
in days or in walks finished, not in power, before either could ship.

**The fusing player is untouched by B.** It meets the walls far above them (x1.98 at 2-6, x6.5 and up from
chapter 4). With B it pays 1 extra sitting at 1-12 (median) and 0 everywhere else, and finishes on day 132.6
against 132.1. Harder walls land only on the player who does not fuse, which is the player the wall rule exists
to protect.

## What the patches do to `Tools/build-check.ps1`

Full check (60 walks) on Linux, with the local workarounds below:

- **Main as it stands (option A): all harness checks pass** (4,057 `[ok]` lines). The run exits 1 only on
  doc-lint's dead-branch findings, which come from this branchless clone (CLAUDE.md, "A shallow or branchless
  clone").
- **Option B and option C each fail the same seven harness checks** ("RESULT: 7 check(s) FAILED" in both):
  1. **`CheckFusionNeverGates`: 60 of 60 reference walks STUCK**, in both B and C. These are the gate's own walks,
     on unix-0 time, with no loop, no Road and no relics, so they are harsher than this study's non-spender
     (39 of 60 stuck in B). In B the median extra sittings is already 13 at 3-6 and 16 at 3-12, and the fusing
     walks pay 0 at every wall from 2-6 on.
  2. and 3. **The G5 guild tech guard**: "6 of 6 walks with no guild tech are STUCK" (B at 6-6, 4-12, 3-6, 3-6,
     6-6 and 5-12; C at 2-12 and five times at 3-12), and its companion check that a walk with the full tree
     finishes.
  4. **The CEIL band**: all 11 walls fall under the wall band's 55 percent. In B the pinned line wins the
     ceiling battle 0 to 3.8 percent of the time from 2-6 on, and 32.5 percent at 1-12. In C it is 0 to 1.9
     percent, and 12.2 percent at 1-12. At `recommendedPower`, a B wall is nearly unwinnable. The walks pass it
     only because they arrive at x1.2 and retry.
  5. **The power band at 2-1**: x1.341 in B and x1.345 in C, over the x1.25 top. The longer stay at 1-12 delivers
     a stronger player to 2-1.
  6. **33 of 216 stage fingerprints moved**: the 11 walls times 3 seeds. Any wall change does this; the baseline
     would be regenerated with a stated reason.
  7. **Every stage's ceiling is above its floor**: a flat wall has floor = ceiling. A shippable version would
     set the ceiling one level above the floor. This was not measured.

**Local workarounds needed to run the gate on Linux at all** (not committed; they are in
`Docs/walls/walls-study-tooling.patch`). Main's full check does not run on Linux with .NET as `CLAUDE.md`
describes, for two reasons that came in after that note was written:

- `R26UiChecks.cs` uses `System.Drawing.Bitmap`, which is not in the .NET reference pack. The harness does not
  compile (CS1069). Under .NET 8 it then crashes in `CheckR26KitPolish` unless System.Drawing.Common 6.0 is given
  `System.Drawing.EnableUnixSupport` and libgdiplus. Libgdiplus comes with Mono, which is why the Mono install
  mattered here.
- The R26 define probe in `build-check.ps1` runs its exes with `& $exe`, not `Invoke-BuiltExe`, so on Linux
  they never run and `[define-probe]` fails.

The CI job on `ubuntu-latest` presumably fails the same way. This needs its own fix on a code branch.

## Recommendation: A

1. **A is the only option where no reference walk gets stuck.** That is the design rule (fusion accelerates a
   wall, it never gates one), and the gate enforces it through `CheckFusionNeverGates` and the G5 guard. B and C
   both break it: 60 of 60 of the gate's walks stuck, and 39 of 60 of this study's non-spenders in B.
2. **B and C turn walls into roster lotteries, then into paid gates.** The non-spender grows under 0.5 percent a
   day from chapter 2 on, so a 1 to 2 day wall is set inside a 5.5 to 8 percent spread between players. The whale's small
   power lead becomes a 27-day lead and 34 more finishing walks out of 60. That is the kind of gap the R24 limits
   exist to prevent, and their power metric does not see it.
3. **A already has the pace an idle RPG wants, from a different source.** Every stage costs its 2-day ladder, a
   chapter takes 24.7 days, the campaign 134 days, and buyers finish within a day of each other. The walls still
   bind by CEIL (55 to 72 percent at the pinned line's ceiling battle), so they read as walls on the screen
   without costing a stuck week.
4. **If the owner wants walls that cost days, the prerequisite is a growth channel, not harder content.** The
   reference player needs something that idle time reliably converts into power at a wall, at several percent a
   day. For example: level-cap headroom that Gold can fill, or a wall-local resource that accrues while
   walled. Such a channel would change `CheckFusionEconomyDerivation`'s inputs and the R24 sizing, so it is a
   design decision and would be re-solved with `Tools/solve-recommended-power.ps1`, not edited by hand. Only then
   would a B-shaped wall have a threshold that time crosses rather than luck.

## Limits

- **The diligent player is the ceiling.** Every buyer claims every order and Road tier. A real player who misses
  days grows slower and would be stuck more in B and C.
- **The spender's Road** is filled to full points by the study's pass model (`StoreSpenderStep`) and its free
  tiers are then claimed at once, so its free track lands earlier in a season than the non-spender's. The effect
  is small (the spender trails the whale at every wall).
- **Leads and days in B and C are read over the walks that reach each wall.** Stuck walks are counted in the
  stuck line, not in the medians.
- **`campaign.json` is generated and solved.** Its header says `Tools/_author_campaign_stages.py` and the solver
  write the stage levels. A hand edit like these patches would be overwritten by either, so an adopted B would go
  through the solver's own kind of move.
- **The fusing player was walked for A and B only,** and C was not re-measured after the stuck counts made it
  moot.
- The study's clock (season calendar) differs from the gate's (unix 0). The armor drops follow the clock, so
  single walks differ between the two, not the shape.

## Reproduce

Apply `Docs/walls/walls-study-tooling.patch` (the `--whale-loop` flag and the Linux workarounds). On Linux, set
`EMBERFALL_EXTRA_REFS` to a System.Drawing.Common 6.0 reference assembly, put its `runtimes/unix` build beside
`DevHarness.exe`, and install Mono for libgdiplus. Build with `pwsh Tools/build-check.ps1 -NoRun`. Then, for a
content folder holding the option's `campaign.json`, run each column in four 15-walk ranges:

    dotnet Tools/DevHarness/bin/DevHarness.exe <content> --whale-curve 60 --whale-col ref:non --whale-loop --whale-range 0 15 --whale-out <dir>/ref_non_0.tsv
    (ref:spend, ref:whale, fuse:non, fuse:spend, fuse:whale the same way)
    dotnet Tools/DevHarness/bin/DevHarness.exe <content> --whale-report <dir>

`--whale-report` prints the power, lead and wall tables. The day and extra-sitting tables above were read from
the same TSV files (`S` rows: entry power, sittings, extra sittings, cleared, elapsed seconds). A column of 60
reference walks took about 10 minutes on 4 cores. The whole study took about 3.5 hours.

## Decisions for the owner

1. **Which option.** Recommended: A.
2. **If B is still wanted:** first decide the growth channel that makes a wall a wait rather than a lottery,
   and replace the R24 power-lead limit for walled content with a lead measured in days and in walks finished.
