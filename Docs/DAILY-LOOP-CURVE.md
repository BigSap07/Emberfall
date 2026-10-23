# The daily loop, on by default: what it does to the curve

Measurement record. First measured 2026-10-22; **re-measured 2026-10-23**, after the owner turned the daily
loop and the free Wayfarer's Road track on by default, replaced the level order with "spend 1,000 Gold", and
the simulated player was fixed to date its level-ups and ascensions by the walk's clock instead of the device
clock. Every number below is the 2026-10-23 run of `DevHarness.exe --daily-loop-curve 60 --daily-loop-fuse`
against the content as committed, unless it says otherwise.

## The question, then and now

The daily orders (`DailyLoop`) used to be off by default, and the Wayfarer's Road free track was on but earned
nothing without them (`Docs/DESIGN-WAYFARERS-ROAD.md`). **Both are on by default since 2026-10-23**
(`DailyLoopSwitch`; `EMBERFALL_DAILY_LOOP_OFF` rolls back). The question this record answers is what that does
to the campaign curve.

## Why the gate cannot answer it

The 60 reference walks behind `CheckFusionNeverGates`, and the single walk the power band reads, never
claim an order. They start at unix time 0, which is before the Road's and the live events' epoch
(2026-01-05), so the Road has no season in them either. **With the loop on by default the gate's walks count
orders and are never paid for them**, so the default moves nothing the gate measures. This study is the
measurement.

## Method

`Tools/DevHarness/DailyLoopCurve.cs`, a measurement mode, never part of the gate and inert in every other run.
Three arms on the same 60 seeds (`WalkSeed(0)` up) and the same clock, the season calendar's
(`StoreWalkClockStart`, as the store curve uses), so the Road has seasons to run in:

- **off**: the loop forced off, the build before 2026-10-23.
- **on**: loop on and Road on, played by a diligent player: the build as it ships now.
- **loop**: loop on, Road off, to split what the orders pay from what the Road pays.

**The diligent player** claims at the start and the end of every sitting (two sittings a day, the walk's
cadence): every finished order, the daily chest, the weekly chest, the calendar, every reached Road tier.
The walk's own play does the waves, stage, summon and spend-Gold orders. Two orders need more than the walk
does: the arena order, done with Proving fights from the Brand bank once the Proving opens at 2-4; and the
spend-Gold order on a day the line is at its star cap and buys nothing, done **at the end of a sitting** with
real Gold, by the cheapest level of any hero below its cap and, if no hero can level, the cheapest Quartermaster
piece (`LoopFinishSpendGold`, shared with `LoopCurve.cs`). A selector takes the hero of its star count with the
highest level-1 power score, ties by id: a fixed rule, never a draw.

Both players: the **reference player** (never converts, never ascends: the one `recommendedPower` is solved
for) and the **fusing player** (converts and ascends).

## Results: the reference player

Median entry power over `recommendedPower` at the walls and chapter openers, and the day each is first attempted.
The power band's top is x2.00 for 1-1 to 1-6, x0.99 at 1-12 and x1.25 from 2-1 on.

| Stage | rec. power | off | on | loop | day off | day on |
|---|---|---|---|---|---|---|
| 1-6 | 6,276 | x1.076 | x1.092 | x1.092 | 3.0 | 3.0 |
| 1-12 | 9,639 | x0.939 | x0.969 | x0.962 | 9.3 | 9.3 |
| 2-1 | 8,604 | x1.194 | x1.239 | x1.238 | 10.8 | 10.8 |
| 2-6 | 9,688 | x1.188 | x1.182 | x1.193 | 21.1 | 21.1 |
| 2-12 | 10,045 | x1.176 | x1.171 | x1.178 | 33.5 | 33.5 |
| 3-12 | 10,150 | x1.201 | x1.204 | x1.195 | 58.7 | 58.4 |
| 4-12 | 10,555 | x1.175 | x1.182 | x1.179 | 83.5 | 83.2 |
| 5-12 | 10,618 | x1.191 | x1.190 | x1.191 | 108.3 | 107.9 |
| 6-12 | 11,349 | x1.122 | x1.129 | x1.121 | 133.0 | 132.6 |

| | off | on | loop |
|---|---|---|---|
| Campaign finished (median day) | 135.0 | 134.7 | 135.5 |
| Sittings | 262 | 261 | 263 |
| Stuck | 1 of 60, at 3-4 | 1 of 60, at 3-4 | 0 of 60 |
| Summons | 10,175 | 11,244 | 11,023 |
| Line levels bought with Gold | 103 | 104 | 103 |
| Final team power | 12,745 | 12,779 | 12,719 |

Every wall and opener above is inside its band in every arm; the tightest are 2-1 at x1.239 (top x1.25) and
1-12 at x0.969 (top x0.99), both in the on arm. Extra sittings at a wall are 0 everywhere except 1-12, which costs
1 in all three arms.

## Results: the fusing player

| | off | on | loop |
|---|---|---|---|
| 1-12 entry | x0.968 | x1.040 | x1.043 |
| 2-1 entry | x1.277 | x1.347 | x1.357 |
| 2-6 entry | x1.974 | x2.147 | x2.147 |
| 3-6 entry | x4.458 | x5.152 | x4.913 |
| Campaign finished (median day) | 132.5 | 132.1 | 132.1 |
| Stuck | 0 of 60 | 0 of 60 | 0 of 60 |
| Final team power | 74,226 | 74,654 | 74,309 |

## What the loop and the Road paid (the "on" arm, medians per walk)

- **The board is finished every day.** Reference player: 671 orders, 131 daily chests and 19 weekly chests over
  a 135 day walk. **The spend-Gold order is claimed on 100% of the 129 days it is on the board**, for both
  players; the other orders on 95.5% (`waves_8`) to 100% of their days.
- **What finishing it off the line costs:** 121 bench levels a walk for the reference player and 65 for the
  fusing player, 0.3% and 0.1% of all Gold, and no Quartermaster piece was ever needed. The level order it
  replaced took 244 bench levels and 0.7% of all Gold for the same player.
- **The loop paid 13.1% of all Gold and 5.7% of all Shards** the reference walk earned (13.5% and 5.8% with
  the Road off). **The Road added 2.9% of Gold and 1.2% of Shards**, plus about 57,600 Relic Dust and 2,060
  Slag.
- **The Road was completed in nearly every season** (median tier 30, lowest 27): 5 four-star and 4 five-star
  selector copies per walk, the first four-star on day 12.3 and the first five-star on day 24.7. With the fixed
  pick rule the four-star was always Niko and the five-star always Zella, and a selector hero was in the final
  line in 60 of 60 walks.
- **The arena order** took 176 Proving fights over the walk.

## What changed since 2026-10-22, and why

- **The off arm did not move at all** (every number the same as the first run), for either player. The clock
  fix changed only the date a level-up or an ascension is counted on, and the off arm counts nothing.
- **The on arm moved up early**: 1-6 x1.078 to x1.092, 1-12 x0.955 to x0.969, 2-1 x1.227 to x1.239. The
  diagnostic arm (nothing spent off the line) reads the same early numbers (1-12 x0.969, 2-1 x1.241), so the
  early lift does not come from the bench levels; it comes with the new slot 2 itself (one order at 0.3 hours
  where two alternated at 0.25 and 0.35). Off the line, the spend-Gold order needs one bench level where the
  level orders needed one or three, so half the bench levels (121 a walk against 244). From 2-6 on the arms
  differ by a hundredth or less, as before.
- **The fusing player's on arm** reads x2.147 at 2-6 (x2.175 before) and x5.152 at 3-6 (x5.140), the same
  shape: far above its band, which no wall reads for this player.

## What it means

1. **The curve does not move enough to re-solve.** For the reference player entry power at the walls moves
   by at most +0.045 (2-1, still under its x1.25 top) and is within 0.01 everywhere from 2-6 on, the campaign ends within half a day of
   the off arm, and no wall gains or loses a sitting. `recommendedPower` does not need re-solving.
2. **Why it does not move.** The walk's pace is set by the ladder (battles per stage over 40 a sitting) and,
   for the reference player, by star-gated level caps: extra Gold buys 104 line levels against 103 because the
   line is capped, not poor. The loop's Gold becomes surplus and its Shards become about 10% more summons.
3. **The spend-Gold order cannot trap a capped line.** One level of any hero (1,070 Gold at the cheapest) or
   one Quartermaster piece (227,400 Gold at the cheapest, at 3-12 where it opens) finishes it, and
   `CheckDailyLoopSpendGold` asserts both bounds. **But it still asks the player to act off the line.** In
   the diagnostic arm (`--daily-loop-no-bench`, nothing spent off the line) the reference player finishes it
   on 13.9% of its days, the same rate the level order had: the capped line buys nothing on most days, the
   daily chest opens 17 times in 135 days, the weekly chest twice, and the Road stops at a median tier 19 a
   season (lowest 16), with no five-star selector. A real player can level a bench hero or buy a
   Quartermaster piece in one action; whether they think to is a UI question for the Orders screen.
4. **The five-star selector is the number to watch.** A diligent player gets a free five-star copy every 28
   days from about day 25. In a walk that never converts it changes nothing measurable. For a player who fuses
   or relics it is a steady supply, and the study does not spend Dust on relics, so the 57,600 Dust is
   unmeasured here (the whale study's relic policy, `WhaleRelicStep`, is the instrument for that).

## Limits

- **A diligent player is the ceiling, not the median.** Real players miss days. The Road's sizing (24 of 28
  days finishes it) means a player who misses five days a season gets no five-star.
- **The Road's Dust and Slag are unspent** in both players' walks; their effect on the curve is not in these
  numbers.
- **The calendar clock leaves one walk stuck at 3-4** in the off and on arms (a different walk in each). That
  is the clock, not the loop: armor drops are timed by the clock (`StoreCurve.cs`), and the gate's own walks
  run on unix 0.
- **The device-clock quirk the first run found is fixed.** The walk's Gold spending and ascensions called the
  `GameSession` forms that read the device clock (`TimeUtil.NowUnix`); `SpendGold` and
  `EconomyConvertAscendAndDeploy` in `Program.cs` pass the walk's clock now, in every walk.

## Reproduce

    DevHarness.exe --daily-loop-curve 60 --daily-loop-fuse      # this record
    DevHarness.exe --daily-loop-curve 60 --daily-loop-no-bench  # the line alone, nothing spent off it for the spend-Gold order

On Linux under Mono with PowerShell 7 the first took about 55 minutes on 4 cores shared with other work.

## Decisions for the owner

1. ~~Flip the default or not.~~ Decided 2026-10-23: on.
2. ~~The level order.~~ Decided 2026-10-23: retargeted to "spend Gold", 1,000 Gold.
3. **Measure the Dust.** Run the Road's Dust through the relic policy before deciding on the five-star
   selector's price.
