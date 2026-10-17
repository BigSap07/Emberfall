# Economy proposal R4: the progression curve

Evidence: `D:\Emberfall\handoff\R4-ECONOMY-DESIGN\`. Logs and simulation outputs live there, outside the repo, including `new-table-F.txt` (all 72 stages of candidate F).

Status: proposal. Nothing here is applied to the shipped content. It is a decision record from 2026-10-17. The reasoning is durable. The numbers are the measurements of this run and the candidate it ended with.

Ratio means the reference player's power on arrival divided by the stage's `recommendedPower`. The reference player never converts and never ascends. That is the player `CheckCampaignPowerCurve` and `CheckFusionNeverGates` read.

## 1. The owner's target

Decided by the owner on 2026-10-11. The source note is `DECISION-ECONOMY-CURVE.md`.

| Stretch | Target ratio | Meaning |
|---|---|---|
| 1-1 to 1-6 | about x1.5 to x2.0 | breezy, no losses |
| 1-7 to 1-11 | falling to about x1.0 | first friction |
| 1-12 | below x1.0 | first real wall, needs a levelling or gear session |
| Chapter 2 onward | about x0.9 to x1.1, walls at stages 6 and 12 | steady pressure, one session of progress per wall |

The owner's direction: keep 1-1 to 1-6, pull first-clear payouts down from 1-7 through early chapter 2, raise idle income modestly, keep the 40 percent drip, and re-express the CEIL and power band checks against this curve.

## 2. Measured today

Source: the full `Tools/build-check.ps1` log of origin/main at the start of the run, kept with the run's evidence. Seeds: 20 for the pace reports. The ratio column is the single-walk "CAMPAIGN POWER CURVE" line (seed 1).

Two readings of "today" exist. The table below is the seed 1 walk. Section 6 uses the median of 8 walks, which reads higher in chapter 1 (1-7 is x1.270 by median and x1.179 by seed 1). Compare like with like.

| stage | arrival ratio | authored band | verdict | geared CEIL | wall sittings (20 seeds) |
|---|---|---|---|---|---|
| 1-1 | x1.200 | tutorial, none | - | 100% (floor-pinned) | - |
| 1-2 | x1.200 | tutorial, none | - | 100% (floor-pinned) | - |
| 1-3 | x1.200 | tutorial, none | - | 100% | - |
| 1-4 | x1.197 | tutorial, none | - | 100% | - |
| 1-5 | x1.185 | ordinary x1.100 to x1.300 | ok | 100% | - |
| 1-6 | x1.012 | wall x1.000 to x1.050 | ok | 71.8% | 2 (extra 0 in 10 seeds, 1 in 7, 2 in 2, 4 in 1) |
| 1-7 | x1.179 | ordinary | ok | 74.1% | - |
| 1-8 | x1.159 | ordinary | ok | 94.1% | - |
| 1-9 | x1.152 | ordinary | ok | 71.4% | - |
| 1-10 | x1.146 | ordinary | ok | 55.3% | - |
| 1-11 | x1.143 | ordinary | ok | 1.8% | - |
| 1-12 | x0.958 | wall, floor x1.000 | UNDER | 67.2% | 2 (extra 0 in 18 seeds, 1 in 2) |
| 2-1 | x1.061 | ordinary | UNDER | 25% | - |
| 2-2 | x1.172 | ordinary | ok | 84.3% | - |
| 2-3 | x1.157 | ordinary | ok | 100% | - |
| 2-4 | x1.158 | ordinary | ok | 82.5% | - |
| 2-5 | x1.169 | ordinary | ok | 35.7% | - |
| 2-6 | x1.106 | wall x0.950 to x1.063 (relaxed) | OVER | 82.3% | 4 (extra 0 in all 20) |
| 2-7 | x1.186 | ordinary | ok | 85.9% | - |
| 2-8 | x1.177 | ordinary | ok | 100% | - |
| 2-9 | x1.174 | ordinary | ok | 83.8% | - |
| 2-10 | x1.210 | ordinary | ok | 85.3% | - |
| 2-11 | x1.197 | ordinary | ok | 85.5% | - |
| 2-12 | x1.118 | wall x0.950 to x1.054 (relaxed) | OVER | 67.6% | 4 (extra 0 in all 20) |

Chapters 3 to 6. Every wall takes 4 sittings, with no extra.

| chapter | arrival ratio range | out of band | geared CEIL range |
|---|---|---|---|
| 3 | x1.134 to x1.223 | 3-6, 3-12 OVER | 18.2% (3-1) to 100% |
| 4 | x1.095 to x1.217 | 4-6 OVER (4-12 within tolerance) | 4% (4-1) to 100% |
| 5 | x1.110 to x1.247 | 5-6, 5-12 OVER | 32.2% (5-1) to 100% |
| 6 | x1.130 to x1.221 | 6-6, 6-12 OVER | 14.4% (6-1) to 100% |

Two checks already fail. The CEIL band check (ordinary [78%, 92%], finale [55%, 72%]) reports only its first miss, 1-3 at 100%. The power band check lists 11 stages: 1-12, 2-1, 2-6, 2-12, 3-6, 3-12, 4-6, 5-6, 5-12, 6-6 and 6-12. Ten bands are already relaxed, because a player's power cannot go down.

## 3. Why rewards alone cannot get there

Five findings, measured in the first phase and confirmed by the simulation.

1. **1-1 to 1-6 sit at about x1.20 and rewards cannot lift them.** Stage 1-1 is the starting line against its recommended power, and no reward touches it. The solver sets the ratio: `SolverMiddleOrdinaryChapter1` is 1.20 and also covers the four tutorial stages. Rewards reach the ratio only through levels bought: 0 percent of power at 1-1, 3 percent at 1-2, 14 percent at 1-6.
2. **First clears are a small channel.** At 2-1, idle is 69.5 percent of gold, first clears 15.9 percent and repeat farming 12 percent. At 2-12 idle is 75.2 percent and first clears 8 percent. The 40 percent drip only moves timing.
3. **The monotone floor holds 1-7 up.** Power never falls, and `recommendedPower` dipped after the 1-6 wall (6276 at 1-6, 5638 at 1-7, 6162 at 1-8). Under any economy 1-7 could not read below about x1.126 against that recommended power.
4. **The reference player is level-capped from 2-4.** Its levels stay flat from 2-4 to 6-12, so gold does nothing for it there. Its power is roster, caps and armor drops. From chapter 2 on, its ratio is set by `recommendedPower` and by armor drops from farming.
5. **The fusing player leaves the reference player at 1-9.** Conversions and ascension cause that, and no first-clear or idle edit can pull it down.

CEIL is also worse than its first miss. 38 of the 70 graded stages sit outside their band. CEIL is the line pinned at `recommendedPower` against the enemy levels. It does not read the economy.

The simulation confirmed finding 2 with numbers. Even the upper bound of the owner's levers (first clears cut to 5 percent and idle gold cut by 40 percent through 1-7 to 2-3, breaking the generator's rules on purpose) moved the reference ratio by only 4 to 6 points on 1-9 to 1-12 and 7 points on 2-1 and 2-2. It moved 1-7 and 1-8 not at all, because their power is already bought by 1-6. The version that obeys the generator's rising-payout rules moved 1-12 and 2-1 by about 0.6 points each.

## 4. The proposal

Three levers, used together. Lever E (economy) moves rewards. Lever R (recommended power) reshapes `recommendedPower` from 1-7 on. Lever R2 searches the enemy levels again so CEIL lands in band. Lever E alone is worth about 1 point. Lever R and R2 do the work.

### Lever E

- First-clear gold is pulled down but kept rising by about 2 percent per ordinary stage (at least 1.6 percent, from the generator's rounding), so it cannot drop at once after 610 at 1-6. In candidate F it is x0.87 of today at 1-7, x0.76 at 1-8, x0.67 at 1-9, x0.60 at 1-10 and x0.54 at 1-11. It is x0.50 from 1-12 to 2-3, then rises (2-4 is x0.55) to x1.0 at 3-1. Finales still lead their neighbours and the opener.
- Idle income rises from 0.80 purses an hour to 1.00 by 3-1 and holds. In candidate F, idle gold per hour at 3-1 is 3300 against 2640 before, and 45100 at 6-12 against 36100.
- Purse, shards, account XP and the per-wave drip are unchanged.
- The edit patches the seven reward fields in `campaign.json` in place. The generator's `main()` in `Tools/_author_campaign_stages.py` must not be run, because it rewrites `recommendedPower` and every enemy level from seeds.
- Why only about 1 point: a rule that keeps every first-clear step rising cannot pull 1-7 down by more than about 14 percent, so the lever is spent almost at once.

### Lever R

`recommendedPower` becomes the reference player's median entry power divided by a target ratio per stage. The floor level, ceiling level and enemy levels follow by the solver's analytic inversion (scale = 1 + 0.12 x (level - 1)).

Chapter 1, 1-7 to 1-12, today against candidate F:

| stage | recommendedPower today | recommendedPower F |
|---|---|---|
| 1-7 | 5638 | 6225 |
| 1-8 | 6162 | 6699 |
| 1-9 | 6479 | 7308 |
| 1-10 | 6861 | 7982 |
| 1-11 | 7226 | 8555 |
| 1-12 | 9028 | 9639 |

### Lever R2

The enemy levels of all 66 stages from 1-7 are searched again until the geared CEIL lands at 85 percent for ordinary stages and 65 percent for walls. All 66 landed within 7 points. The search needed 1,358 probes. Levels moved by up to 9 on the stages whose CEIL was off (2-1 from 28 to 23, 2-3 from 50 to 59, 2-8 from 42 to 48, 1-11 from 22 to 19).

### The rule for chapter 2 and later, and why it is a ratchet

The target ratio could not be set in advance for chapter 2 on. With levels moved by the analytic inversion only, no combination of an ordinary ratio and a wall ratio finished all 20 walks (the best finished 19 of 20, at ordinary x1.14 and walls x1.12). A level search to CEIL left 0 of 20 finishing at walls near x0.94, and walls at x1.00 or x1.03 finished 1 of 20. Twenty points of CEIL are only 1 to 2 enemy levels, about 4 percent of power. The ratio decides, not the CEIL target.

So the target ratio per stage is found by the solver's own stuck-step rule, run by hand because the real solver takes hours. After each reading of 20 walks, every stage a walk stuck on has its target raised by x1.04 (x1.08 if 3 or more walks stuck, x1.05 and x1.10 from round 6). Its `recommendedPower` is derived again from the walk's median entry power, and its levels are inverted and searched again. It converged after 16 rounds. Two ratchet loops overlapped for one round, so the converged table was not trusted from the log. It was read again from scratch three times, and each reading showed 0 of 20 walks stuck. Only the ratchet result, candidate F, finishes 20 of 20. This counts as one iteration (I4).

The rule for any future retune of chapter 2 and later: `recommendedPower` = round(median reference entry power / T), levels by inversion then searched to CEIL 85 percent (ordinary) and 65 percent (walls). T is per stage from the ratchet. The observed ratios on F are x1.02 to x1.21 for ordinary stages and x1.03 to x1.20 for walls. The per-stage T values are not tabulated beyond the ratios in `new-table-F.txt`.

### Kept as is

- The 40 percent per-wave drip (`WaveDripPercent` in `GameSession`). It moves timing inside a stage, not totals.
- Fusion still accelerates walls and never gates. `CheckFusionNeverGates` keeps holding: all 20 walks that never convert and never ascend finish the campaign.
- Stages 1-1 to 1-6: `recommendedPower`, levels and rewards untouched (Option 1, section 10).

## 5. Band rules

Power band (`CheckCampaignPowerCurve`) becomes a per-stage target table.

| stage | band |
|---|---|
| 1-1 to 1-5 | [1.00, 2.00] |
| 1-6 | [1.00, 2.00]. Stage 6 is no longer a wall in chapter 1. |
| 1-7 | [1.05, 1.25] |
| 1-8 | [1.02, 1.20] |
| 1-9 | [0.99, 1.16] |
| 1-10 | [0.96, 1.12] |
| 1-11 | [0.93, 1.08] |
| 1-12 | [0.88, 0.99] |
| Chapter 2 onward, ordinary and walls alike | [0.97, 1.25] |

With these bands no band needs relaxing. Every stage's `recommendedPower` rises fast enough to carry its own band.

CEIL band, graded by kind. The check now reports every stage, not only the first miss.

| kind | stages | band |
|---|---|---|
| breezy | 1-1 to 1-6 | [70%, 100%] |
| wall | 1-12, and stages 6 and 12 from chapter 2 | [55%, 72%] |
| everything else | 1-7 to 1-11, ordinary stages from chapter 2 | [78%, 92%] |

Result on candidate F: 0 of 70 graded stages out of band. Today it is 38 of 70.

## 6. Simulated result

All simulations ran on a local scratch branch that is never pushed. Readings are the median reference ratio over 8 walks (seeds 1 + k x 1000003, k = 0 to 7), except where 20 walks are named. Iterations:

- **I0** control, content unchanged.
- **I1** lever E only (the version that obeys the generator's rising-payout rule).
- **I2** lever E plus lever R, enemy levels by analytic inversion only, target 1-7 to 1-11 at x1.15, 1.11, 1.07, 1.03, 1.00, then 1-12 at x0.93 and chapter 2 on ordinary x1.04, walls x0.94.
- **I3** lever E, R and R2 (level search to CEIL), all 66 stages from 1-7.
- **I4** the ratchet of section 4 to convergence, then one full `Tools/build-check.ps1`. This is candidate F.

An "S" marks a stage where at least one walk stuck for 14 sittings.

| stage | I0 | I1 | I2 | I3 | I4 (F) |
|---|---|---|---|---|---|
| 1-7 | x1.270 | x1.270 | x1.150 | x1.155 | x1.150 |
| 1-9 | x1.207 | x1.207 | x1.070 | x1.081 | x1.079 |
| 1-11 | x1.184 | x1.184 | x1.028 | x1.010 | x1.000 |
| 1-12 | x0.999 | x0.993 | x0.960 | x0.939 | x0.936 |
| 2-1 | x1.104 | x1.098 | x1.077 | x1.047 | x1.037 |
| 2-6 | x1.097 | x1.128 | x0.930 S | x0.936 S | x1.076 |
| 2-12 | x1.116 | x1.130 | x0.943 | x0.956 S | x1.084 |
| 3-6 | x1.112 | x1.138 | x0.942 S | not read | x1.140 |

| iteration | stuck walks (reference player) | note |
|---|---|---|
| I0 | none | all walks finish |
| I1 | none reported | 1-1 to 1-5 and 1-7 to 1-8 do not move |
| I2 | 8 walks: 2-5 x1, 2-6 x4, 3-6 x2, 3-12 x1. Sweep of 9 ratio pairs on 20 walks: best finish 19 of 20 | chapter 1 lands on the owner's shape, chapter 2 on sticks |
| I3 | 20 walks: finish 0 of 20, stuck 2-6 x9, 2-12 x7, 3-4 x1, 3-6 x3 | wall pairs tried: L_A (walls x0.94 to x0.98) 0 of 20, L_B (wall CEIL 85%) 0 of 20, L_C (walls x1.03) 1 of 20, L_D (walls x1.00, CEIL 85%) 1 of 20 |
| I4 | 20 walks: 0 stuck after 16 rounds, all 20 finish | candidate F |

First-clear and idle share of income was not re-measured in the simulation. Lever E moved the ratio by about 1 point (I1), with an upper bound of 4 to 6 points.

### Candidate F per stage, 1-1 to 2-12

Ratio is the median over 8 walks. "Today" is the I0 control on the same walks. CEIL is the geared CEIL from the build-check on F. The check's own seed 1 reading on F is lower in places: 1-7 x1.068, 1-8 x1.066, 1-9 x1.022, 1-10 x0.985, 1-11 x0.965, 1-12 x0.897, and chapter 2 x1.005 to x1.101. The 8-walk medians are the headline numbers. The seed 1 reading is what the check itself sees, and it sits inside every band.

| stage | owner target | ratio today | ratio F | recommendedPower | first clear gold | idle gold per hour | CEIL F |
|---|---|---|---|---|---|---|---|
| 1-1 | x1.5 to x2.0 (not met) | x1.200 | x1.200 | 2977 to 2977 | 260 to 260 | 96 to 96 | 100% |
| 1-2 | x1.5 to x2.0 (not met) | x1.246 | x1.246 | 3797 to 3797 | 305 to 305 | 120 to 120 | 100% |
| 1-3 | x1.5 to x2.0 (not met) | x1.249 | x1.249 | 4733 to 4733 | 365 to 365 | 150 to 150 | 100% |
| 1-4 | x1.5 to x2.0 (not met) | x1.272 | x1.272 | 4897 to 4897 | 435 to 435 | 185 to 185 | 100% |
| 1-5 | x1.5 to x2.0 (not met) | x1.237 | x1.237 | 5112 to 5112 | 515 to 515 | 225 to 225 | 100% |
| 1-6 | x1.5 to x2.0 (not met) | x1.075 | x1.075 | 6276 to 6276 | 610 to 610 | 270 to 270 | 71.8% |
| 1-7 | falling to x1.0 | x1.270 | x1.150 | 5638 to 6225 | 710 to 620 | 320 to 320 | 86% |
| 1-8 | falling to x1.0 | x1.207 | x1.110 | 6162 to 6699 | 830 to 630 | 375 to 375 | 89.4% |
| 1-9 | falling to x1.0 | x1.207 | x1.079 | 6479 to 7308 | 960 to 645 | 435 to 435 | 82.9% |
| 1-10 | falling to x1.0 | x1.200 | x1.029 | 6861 to 7982 | 1100 to 660 | 505 to 505 | 83.4% |
| 1-11 | falling to x1.0 | x1.184 | x1.000 | 7226 to 8555 | 1260 to 675 | 585 to 585 | 86.1% |
| 1-12 | below x1.0 | x0.999 | x0.936 | 9028 to 9639 | 2160 to 1080 | 670 to 670 | 59.3% |
| 2-1 | x0.9 to x1.1 | x1.104 | x1.037 | 8536 to 9009 | 1630 to 815 | 760 to 760 | 84.8% |
| 2-2 | x0.9 to x1.1 | x1.173 | x1.030 | 9188 to 10481 | 1840 to 920 | 860 to 860 | 87.2% |
| 2-3 | x0.9 to x1.1 | x1.110 | x1.041 | 9932 to 10764 | 2060 to 1030 | 970 to 970 | 88.9% |
| 2-4 | x0.9 to x1.1 | x1.162 | x1.026 | 9591 to 10986 | 2310 to 1270 | 1090 to 1120 | 85.8% |
| 2-5 | x0.9 to x1.1 | x1.160 | x1.018 | 9678 to 11140 | 2570 to 1540 | 1220 to 1280 | 81.8% |
| 2-6 | x0.9 to x1.1 | x1.097 | x1.076 | 10274 to 10508 | 2860 to 1860 | 1360 to 1460 | 62.7% |
| 2-7 | x0.9 to x1.1 | x1.174 | x1.070 | 9765 to 10675 | 3160 to 2210 | 1510 to 1660 | 86.1% |
| 2-8 | x0.9 to x1.1 | x1.167 | x1.068 | 9883 to 10793 | 3490 to 2620 | 1670 to 1880 | 86.6% |
| 2-9 | x0.9 to x1.1 | x1.161 | x1.096 | 10032 to 10688 | 3840 to 3070 | 1840 to 2120 | 83.6% |
| 2-10 | x0.9 to x1.1 | x1.199 | x1.085 | 9751 to 10868 | 4220 to 3590 | 2020 to 2370 | 84.9% |
| 2-11 | x0.9 to x1.1 | x1.200 | x1.126 | 9768 to 10490 | 4620 to 4160 | 2210 to 2650 | 84.1% |
| 2-12 | x0.9 to x1.1 | x1.116 | x1.084 | 10467 to 10919 | 7570 to 7190 | 2420 to 2960 | 56.5% |

Chapters 3 to 6 on F: ordinary stages x1.04 to x1.21, walls x1.03 to x1.20 (3-6 x1.140, 3-12 x1.148, 4-6 x1.133, 4-12 x1.079, 5-6 x1.080, 5-12 x1.097, 6-6 x1.196, 6-12 x1.027). Chapter 2 ordinary stages read x1.02 to x1.13, and the two chapter 2 walls x1.08.

### What was reached and what was not

- **Reached.** Chapter 1 has the owner's shape: 1-1 to 1-6 untouched, 1-7 x1.150, falling to x1.000 at 1-11, and 1-12 at x0.936, below x1.0.
- **Not reached.** The owner's note says chapter 2 onward about x0.9 to x1.1, with walls at stages 6 and 12. Phase A aimed walls at x0.93 to x0.95, which was the builder's target, not the owner's. Walls at those ratios, and even x1.00 to x1.03 everywhere, cannot be done for the reference player. It is level-capped from 2-4, so only armor drops move it, and it could not finish 20 of 20 walks at those ratios. The ratchet is the measured minimum: chapter 2 on reads x1.02 to x1.21 for ordinary stages and x1.03 to x1.20 for walls. The chapter 2 walls (x1.076 and x1.084) are inside the owner's range but not below their neighbours. Four of the eight chapter 3 to 6 walls (3-6, 3-12, 4-6 and 6-6, x1.13 to x1.20) are above that range. The other four (4-12, 5-6, 5-12 and 6-12, x1.03 to x1.10) are inside it.
- **Not reached.** 1-1 to 1-6 at x1.5 to x2.0 (Option 2, section 10).

### Wall cost on F

Extra sittings over the ladder floor, 20 walks of the reference player, median / p90 / max, from `CheckFusionNeverGates`. For the fusing player, in brackets, the extra sittings and the median entry ratio.

| wall | reference extra | reference entry ratio | fusing extra, entry ratio |
|---|---|---|---|
| 1-6 | 0 / 2 / 4 | x1.077 | 0 / 2 / 4, x1.077 |
| 1-12 | 1 / 2 / 4 | x0.939 | 0 / 1 / 1, x0.980 |
| 2-6 | 0 / 5 / 12 | x1.098 | 0 / 0 / 0, x1.871 |
| 2-12 | 1 / 5 / 28 | x1.084 | 0 / 0 / 0, x2.938 |
| 3-6 | 0 / 1 / 12 | x1.134 | 0 / 0 / 0, x5.037 |
| 3-12 | 0 / 1 / 1 | x1.151 | 0 / 0 / 0, x8.752 |
| 4-6 | 0 / 1 / 3 | x1.136 | 0 / 0 / 0, x11.633 |
| 4-12 | 1 / 4 / 5 | x1.082 | 0 / 0 / 0, x13.909 |
| 5-6 | 0 / 1 / 8 | x1.083 | 0 / 0 / 0, x15.118 |
| 5-12 | 1 / 2 / 5 | x1.086 | 0 / 0 / 0, x15.562 |
| 6-6 | 0 / 0 / 1 | x1.187 | 0 / 0 / 0, x17.848 |
| 6-12 | 1 / 3 / 6 | x1.027 | 0 / 0 / 0, x15.406 |

Today's `CheckFusionNeverGates` table (same check, origin/main) reads 2-6 0/0/1, 2-12 0/1/1 and 3-6 0/1/5, and the max is 1 at 3-12, 5-6, 5-12 and 6-6. So from 2-6 on, today's max is 1, except 5 at 3-6. On F the max is 28 at 2-12 and 12 at 2-6 and 3-6, and the median is 0 or 1. The fusing player pays nothing from 2-6 on, on F and today. The pace report's own finale target (median 2 to 4 extra, p90 6 or less) is still missed for the fusing player: 0 of 120 finale readings cost 2 to 4 extra sittings.

The fusing player reads x1.86 at 2-6, x2.91 at 2-12, x8.8 at 3-12, x13.9 at 4-12 and x15.4 at 6-12 over 8 walks (the table above, over 20 walks, reads x1.871 and x2.938 at 2-6 and 2-12). This is for information only.

## 7. What moves

Result of one full `Tools/build-check.ps1` on candidate F: exactly 2 checks fail, both explained.

- **Stage fingerprints (216).** 195 of 216 move, every stage from 1-7 on except 3-1. The moved list skips 3-1. Its enemy levels (33, 33, 34) are unchanged, while its `recommendedPower` and `ceilingLevel` moved. The rest is deliberate. Lever E alone would move none, because fingerprints read enemy levels, not rewards. The change comes from lever R and R2. They would be regenerated on purpose.
- **`CheckEconomyRespondsToIncome`.** It reads -0.18 against a requirement of more than 0.1. The result is dominated by the draw. The builder reports, added after the I4 log, that unchanged content reads +0.89 with one seed, and that averaged over four seeds it reads +0.28 on unchanged content and +0.01 on F. These are builder readings taken after the full run. They are not in the build-check log. That makes it an instrument question, not a content bug (decision 4, section 10).
- **Hero (470) and talent (450) fingerprints.** Unmoved.
- **`ContentVersion`.** It moves on any `campaign.json` or `balance.json` edit. That retires every recorded guild dungeon replay.
- **Checks that read the economy.** `CheckCampaignPowerCurve`, `CheckArmoredCurveDelta`, the `ReportPace` reports, `CheckFusionNeverGates`, `CheckEconomyRespondsToIncome`, `CheckInstrumentsSeeTheWholeCurve`, `CheckWaveDrip` and the Guild Path checks.
- **Passing on F.** The power band check passes with no band relaxed. The CEIL check passes on all 70 graded stages. `CheckFusionNeverGates` passes.

## 8. Risks

- **Replays retire.** Every recorded guild dungeon replay stops matching. This is a cost of any content edit.
- **Fingerprint churn.** 195 of 216 stage fingerprints move. A review must confirm they moved because of the intended `recommendedPower` and level changes and not by drift.
- **Chapter 2 on cannot be a wall for the capped player.** The reference player cannot get through walls at x0.93 to x1.03 at all. Walls on F sit at x1.03 to x1.20. Chapter 2 walls are inside the owner's x0.9 to x1.1 but not below their neighbours, and four of the eight chapter 3 to 6 walls (3-6, 3-12, 4-6 and 6-6, x1.13 to x1.20) are above it.
- **A long tail on two walls.** On F the worst reference walk spent 28 extra sittings at 2-12 and 12 at 2-6 and 3-6. Median cost is 0 or 1. These tails are the stuck-step rule's limit, not a typical experience.
- **Fusing players are not gated at all.** They read x1.86 at 2-6 and x15.4 at 6-12 and pay 0 extra sittings at every wall from 2-6 on (1-6 reads 0/2/4 and 1-12 reads 0/1/1). Walls tuned for the reference player never stop them. That is a pacing question (decision 3, section 10).
- **Chapter 1 cannot reach the owner's number under Option 1.** Option 2 gets 1-2 to 1-6 to about x1.75 but 1-1 stops near x1.34, and it risks a cliff into 1-7.
- **Idle raise.** Idle is already about 75 to 80 percent of income. Raising it to 1.00 purses an hour makes waiting stronger and flattens the feel of a wall for players who wait.
- **The economy-response check.** It fails on F by a draw-dominated reading. If left as is, the build stays red.
- **Slow solver.** The full `solve-recommended-power.ps1` is too slow to use (a recorded 4 hour abort). The ratchet stands in for it, so the numbers are right for the walks measured, not proven for every roll.
- **Band rewrite hides regressions.** New bands pass by design. Review must check them against the owner's target table in section 1, not against themselves.

## 9. What the player feels

**1-1 to 1-6.** Unchanged. Breezy and loss free. The player is 20 to 27 percent stronger than each of 1-1 to 1-5 asks, and 1-6 reads x1.075, a light check and no longer a wall.

**1-7 to 1-11.** Friction builds, as the owner wanted. Each stage asks a little more than the last. The player starts 15 percent ahead at 1-7 and is level with the stage by 1-11. First clears pay 13 to 46 percent less gold from 1-7 to 1-11, so the roster and levels do not run ahead. Enemies are a few levels higher than before.

**1-12.** The first real wall. The player is 6 percent short. Most players stop here for a sitting or two, then level and gear and go on. First clears pay half as much from 1-12 to 2-3. Waiting pays, because idle income is the main source.

**Chapter 2 on.** Steady pressure, a little looser than the owner asked for. The reference player is 2 to 21 percent ahead of the ordinary stages (2 to 13 percent in chapter 2) and 3 to 20 percent ahead of the walls. Walls differ from their neighbours by CEIL (55 to 72 percent against 78 to 92), not by ratio: 2-6 reads x1.076, above 2-4 and 2-5. For most players a wall costs one extra sitting at most. A few rolls are stopped for much longer, and those tails also hit ordinary stages (2-4 reaches a p90 of 26 extra sittings and 2-10 a p90 of 13 in the 8-walk run). A player who fuses and ascends runs far ahead of all of it and will not feel a wall.

## 10. Decisions for the owner

The main decision: **yes, change, or no.** Apply candidate F, apply it with changes, or leave the economy as it is.

Three defaults were taken to keep the run moving. Each can be reversed.

1. **Keep 1-1 to 1-6 at about x1.2 (Option 1).** The owner's "keep 1-1 to 1-6" and "x1.5 to x2.0" conflict, and rewards cannot lift them. The solver sets x1.2: `SolverMiddleOrdinaryChapter1` is 1.20 and also covers the four tutorial stages. `SolverMiddleWallChapter1` is 1.025, and 1-6 gets it because `StageDef.IsMiniboss` is set on stage 6 of every chapter, so it reads x1.012. Option 1 changes only the check: the band becomes [1.00, 2.00] for 1-1 to 1-6, and stage 6 is no longer a wall in chapter 1. It moves no content, no fingerprint and not `ContentVersion`. x1.2 meets "breezy, no losses" (CEIL 100% on 1-3 to 1-5, no losses in the walk). It does not meet "about x1.5 to x2.0".
2. **Lever R is allowed.** `recommendedPower` and enemy levels move, on the simulation branch first.
3. **The reference player is the target player.** The curve is tuned for the player who never converts or ascends. The fusing player is reported for information.

**Option 2, for the owner to choose instead of default 1.** It was not simulated. Every number below is an analytic estimate. Lower `recommendedPower` and the enemy levels of 1-1 to 1-6 to a x1.75 solver middle. Enemies, lines and rewards stay the same.

- 1-2 to 1-6 reach about x1.75. 1-1 tops out near x1.34, because its enemies cannot go below level 1.
- The stages get easier. 1-6 to 1-7 becomes a cliff unless 1-7 to 1-11 are reshaped, which means enemies climbing about 20 percent a stage there to fall from x1.8 to x1.0.
- It moves 18 of 216 stage fingerprints for 1-1 to 1-6, plus 15 if 1-7 to 1-11 are reshaped. `ContentVersion` moves too.
- The onboarding checks may pin 1-1's opening fight. That is untested.

Decisions the simulation raised:

1. **1-1 to 1-6 at x1.5 to x2.0 (Option 2) or keep (Option 1).**
2. **Chapter 2 on.** Accept walls at x1.03 to x1.20 for the reference player, or change what progresses a capped player: raise the level caps, add a gold sink, or make walls bind only the fusing player.
3. **Fusion pacing.** The fusing player runs far ahead of the reference player and pays no extra at any wall. Whether walls should stop them is a separate decision. `CheckFusionNeverGates` keeps fusion as an accelerator and never a gate.
4. **The economy-response check's instrument.** `CheckEconomyRespondsToIncome` is draw-dominated. Decide whether to average it over more seeds or to retire it for this question.
