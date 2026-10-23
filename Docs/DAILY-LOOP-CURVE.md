# The daily loop, on by default: what it does to the curve

Measurement record from 2026-10-22. The numbers are a run of `DevHarness.exe --daily-loop-curve 60
--daily-loop-fuse` against the content as committed. **The default was not flipped**: `DailyLoopSwitch`
is still off unless `EMBERFALL_DAILY_LOOP_ON` is defined, and `WayfarersRoadSwitch` is still on and
therefore still inert.

## The question

The daily orders (`DailyLoop`) are off by default, and the Wayfarer's Road free track is on but earns
nothing without them (`Docs/DESIGN-WAYFARERS-ROAD.md`). Turning both on by default means defining
`EMBERFALL_DAILY_LOOP_ON`. What would that do to the campaign curve?

## Why the gate cannot answer it

The 60 reference walks behind `CheckFusionNeverGates`, and the single walk the power band reads, never
claim an order. They start at unix time 0, which is before the Road's and the live events' epoch
(2026-01-05), so the Road has no season in them either. **Flipping the define would change nothing the gate
measures**: orders would be counted and never paid. This study is the measurement.

## Method

`Tools/DevHarness/DailyLoopCurve.cs`, a measurement mode, never part of the gate and inert in every other run.
Three arms on the same 60 seeds (`WalkSeed(0)` up) and the same clock, the season calendar's
(`StoreWalkClockStart`, as the store curve uses), so the Road has seasons to run in:

- **off**: as shipped. Loop off, Road on and inert.
- **on**: loop on and Road on, played by a diligent player.
- **loop**: loop on, Road off, to split what the orders pay from what the Road pays.

**The diligent player** claims at the start and the end of every sitting (two sittings a day, the walk's
cadence): every finished order, the daily chest, the weekly chest, the calendar, every reached Road tier.
The walk's own play does the waves, stage, level and summon orders. Two orders need more than the walk
does: the arena order, done with Proving fights from the Brand bank once the Proving opens at 2-4; and the
level order, which a line at its star cap cannot finish, done by levelling the cheapest bench hero **at the
end of a sitting** only. A selector takes the hero of its star count with the highest level-1 power score,
ties by id: a fixed rule, never a draw.

Both players: the **reference player** (never converts, never ascends: the one `recommendedPower` is solved
for) and the **fusing player** (converts and ascends).

## Results: the reference player

Median entry power over `recommendedPower` at every wall, and the day it is first attempted:

| Stage | rec. power | off | on | loop | day off | day on |
|---|---|---|---|---|---|---|
| 1-6 | 6,276 | x1.076 | x1.078 | x1.078 | 3.0 | 3.0 |
| 1-12 | 9,639 | x0.939 | x0.955 | x0.951 | 9.3 | 9.3 |
| 2-1 | 8,604 | x1.194 | x1.227 | x1.216 | 10.8 | 10.8 |
| 2-12 | 10,045 | x1.176 | x1.180 | x1.177 | 33.5 | 33.5 |
| 3-12 | 10,150 | x1.201 | x1.202 | x1.202 | 58.7 | 58.8 |
| 4-12 | 10,555 | x1.175 | x1.178 | x1.179 | 83.5 | 83.6 |
| 5-12 | 10,618 | x1.191 | x1.189 | x1.190 | 108.3 | 108.3 |
| 6-12 | 11,349 | x1.122 | x1.125 | x1.126 | 133.0 | 133.0 |

| | off | on | loop |
|---|---|---|---|
| Campaign finished (median day) | 135.0 | 135.1 | 134.8 |
| Sittings | 262 | 262 | 261 |
| Stuck | 1 of 60, at 3-4 | 1 of 60, at 3-4 | 0 of 60 |
| Summons | 10,175 | 11,200 | 11,026 |
| Line levels bought with Gold | 103 | 101 | 102 |
| Final team power | 12,745 | 12,788 | 12,809 |

Every stage stays inside its power band in every arm (later stages 0.97 to 1.25, chapter 1 walls 0.88 to
0.99). Extra sittings at a wall are 0 everywhere except 1-12, which costs 1 in all three arms.

## Results: the fusing player

| | off | on | loop |
|---|---|---|---|
| 1-12 entry | x0.968 | x1.048 | x1.038 |
| 2-6 entry | x1.974 | x2.175 | x2.094 |
| 3-6 entry | x4.458 | x5.140 | x4.995 |
| Campaign finished (median day) | 132.5 | 132.1 | 132.5 |
| Stuck | 0 of 60 | 0 of 60 | 0 of 60 |
| Final team power | 74,226 | 74,312 | 74,230 |

## What the loop and the Road paid (the "on" arm, medians per walk)

- **The board is finishable every day once the bench is used.** 668 orders, 130 daily chests and 19 weekly
  chests over a 135 day walk. Every order kind is claimed on 94% to 100% of the days it is offered;
  `waves_8` and `stage_2` are the hardest (about 95%).
- **The loop paid 13.1% of all Gold and 5.7% of all Shards** the reference walk earned (13.5% and 5.7% with
  the Road off). **The Road added 2.9% of Gold and 1.2% of Shards**, plus about 57,600 Relic Dust and 2,060
  Slag.
- **The Road was completed every season** (tier 30 in all 60 walks): 5 four-star and 4 five-star selector
  copies per walk, the first four-star on day 12 and the first five-star on day 25. With the fixed pick rule
  the four-star was always Niko and the five-star always Zella, and a selector hero was in the final line in
  60 of 60 walks.
- **The arena order** took 176 Proving fights over the walk. **The level order** took 244 bench levels,
  costing 0.7% of all Gold.

## What it means

1. **The curve does not move.** For the reference player, entry power at the walls moves by at most +0.033
   (2-1) and is within 0.006 everywhere from 2-6 on, the campaign ends within a third of a day of the off arm,
   and no wall gains or loses a sitting. `recommendedPower` does not need re-solving for the loop.
2. **Why it does not move.** The walk's pace is set by the ladder (battles per stage over 40 a sitting) and,
   for the reference player, by star-gated level caps: extra Gold buys 101 line levels against 103 because the
   line is capped, not poor. The loop's Gold becomes surplus and its Shards become about 10% more summons,
   which widen the roster without lifting the capped line.
3. **Where it does show: the fusing player's early chapters.** Entry power rises by up to 15% at 3-6 (x4.46
   to x5.14) and 2-1 is reached half a day sooner. These stages are far above their band already for that
   player, so it changes no wall.
4. **The level order is a trap for a capped line.** Without bench levelling (`--daily-loop-no-bench`) the
   reference player finishes the level order on 13% to 14% of days, so the daily chest opens 16 times in 135
   days, the weekly chest twice, and the Road stops at a median tier 19 a season (at best 27): no five-star. A real player can level a bench hero;
   whether they will think to is a UI question. A diligent player who levels the bench at the START of a
   sitting takes Gold the line needed and reaches 1-12 half a day later, so the order of play matters.
5. **The five-star selector is the number to watch, again.** A diligent player gets a free five-star copy
   every 28 days from day 25. In a walk that never converts it changes nothing measurable. For a player who
   fuses or relics it is a steady supply, and the study does not spend Dust on relics, so the 57,600 Dust is
   unmeasured here (the whale study's relic policy, `WhaleRelicStep`, is the instrument for that).

## Limits

- **A diligent player is the ceiling, not the median.** Real players miss days. The Road's sizing (24 of 28
  days finishes it) means a player who misses five days a season gets no five-star.
- **The Road's Dust and Slag are unspent** in both players' walks; their effect on the curve is not in these
  numbers.
- **The calendar clock leaves one walk stuck at 3-4 in the off arm too.** That is the clock, not the loop:
  armor drops are timed by the clock (`StoreCurve.cs`), and the gate's own walks run on unix 0.
- **A quirk found on the way:** the walk's Gold spending calls the two-argument `GameSession.LevelUpHero`,
  which dates each level-up by the wall clock (`TimeUtil.NowUnix`), not the walk's. Only order and event
  counters read that date, so it moves no gate number, but on a loop arm it froze the board on the real date.
  The loop arms pass the walk clock (`SpendGold` with a clock); every other walk is unchanged.

## Reproduce

    DevHarness.exe --daily-loop-curve 60 --daily-loop-fuse      # this record: about 5 minutes on 4 workers
    DevHarness.exe --daily-loop-curve 60 --daily-loop-no-bench  # the level-order diagnostic, reference player

## Decisions for the owner

1. **Flip the default or not.** The curve gives no reason against it. The reasons left are product ones: the
   loop is the only thing that feeds the Road, and the Road hands out a five-star every 28 days.
2. **The level order.** Keep it and teach the bench, retarget it ("spend Gold", which the live events already
   count), or drop it from the pool. As it stands it silently blocks the daily chest for a capped line.
3. **Measure the Dust.** Run the Road's Dust through the relic policy before deciding on the five-star
   selector's price.
