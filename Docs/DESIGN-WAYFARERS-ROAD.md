# DESIGN-WAYFARERS-ROAD: the free track of the season pass

Decision record, dated 2026-10-19 (R17). Source: `IDEAS-ECONOMY-PACKS.md` section 3. A 28 day season with 30
tiers, points only from the daily and weekly orders, every reward fixed and shown in advance. This builds the
FREE track. The paid track exists in data and as a locked row, and has no purchase path. The numbers live in
`wayfarers-road.json`; read that file for the truth about any one reward.

## What it is

- **A season** is 28 reward days (exactly four Monday to Sunday weeks), back to back from an epoch in the file
  (a Monday). Every player is in the same season. It turns over on the reward day (04:00 UTC, the clock the
  daily loop and the sinks use), and **unclaimed tiers lapse** with the season.
- **Points come only from the daily loop's claims** (`DailyLoop.ClaimOrder`, `ClaimDailyChest`,
  `ClaimWeeklyChest` call `WayfarersRoad.OnClaim`): 8 for an order, 12 for the daily chest, 40 for the weekly
  chest. Nothing else feeds it. `CheckPassWiring` proves `OnClaim` is called from those three places only.
- **A tier is 46 points**, 30 tiers are 1,380 points. An active day (five orders and the chest) is 52 points.
  **The track is sized on a player who also claims the weekly chest** (40 points on a week of five complete days,
  four whole weeks a season): **24 active days are 24 x 52 + 4 x 40 = 1,408 and finish it, 23 are 1,356 and do not**.
  Without the weekly chests 24 days reach 1,248, tier 27. The first sizing (tier 40 points, weekly chest left out)
  let a diligent player finish in 20 days; review caught it and the lead set 46. `CheckPassSizing` plays 24 and 23
  real days through the loop, weekly chests opened, and asserts both.
- **The selectors reset every season.** A player who finishes the track gets a free 4-star and a free 5-star every
  28 days, as plain earned copies. That is the price of "a 5-star selector at tier 30" in a repeating season, and
  it is the number to watch in the balance pass.
- **It needs the daily loop, and the loop is off by default.** The points are the loop's claims, so the pass earns
  nothing while `DailyLoopSwitch` is off, and it is off unless `EMBERFALL_DAILY_LOOP_ON` is defined. **The pass is
  on but inert until the build defines `EMBERFALL_DAILY_LOOP_ON`.** Its door is a button in the header of
  `OrdersScreen`, which exists only with the loop, so without the define there is no way to reach it either. This
  is the lead's default of 2026-10-19: the loop keeps its owner default (off) and turning it on is a build-define
  decision, not a change to this feature.

## The free track

| Tiers | Reward |
|---|---|
| 1, 4, 7, 10, 13, 16, 19, 22, 25, 28 | Hourglasses: 1, 1, 1.5, 2, 2, 2, 2.5, 3, 3, 4 hours of idle income at the frontier (22 hours in all) |
| 2, 5, 8, 11, 14, 17, 20, 23, 26, 29 | Slag: 20, 20, 30, 30, 40, 40, 50, 50, 60, 80 (420) |
| 3, 6, 9, 12, 18, 21, 24, 27 | Dust: 10, 10, 15, 15, 20, 20, 25, 25 sweeps of the frontier stage (140 sweeps) |
| 15 | A 4-star of the player's choice |
| 30 | A 5-star of the player's choice |

- An hourglass is paid by the one idle formula (`GameSession.IdleYield`), as every daily-loop reward is.
- A Dust reward is that many repeat-clear payouts of the frontier stage (`GameSession.RelicDustForRepeatClear`),
  so it scales with the player the way an hour does.
- **The selectors grant a plain earned copy through `GrantHeroDirect`, never Sealed.** A 4-star may be any
  non-starting 4-star; a 5-star any fusable 5-star (the conversion pool). A hero already owned gains a copy. The
  claim needs a hero of the tier's own star count, refused as `BadChoice` otherwise.
- The free track as a whole is worth about 22 hours of idle, 140 Dust sweeps and 420 Slag, with the two
  selectors, over a season of full activity.

## Buying a tier

A tier can be bought with **Valor or Coda** (earned currencies only), priced in days of that purse's income
(`SinkPricing.DayOf`, 2 days, rounded up to 5), at most **3 a season** across both purses, never past the top.
A bought tier raises the tier reached by one; its reward is claimed like any other. Valor and Coda are
earned, never bought, so this is not a route for money into anything.

## The paid track

Exists in `wayfarers-road.json` and as a **locked row** on the screen, drawn only with `PassPaidSwitch` on
(define `EMBERFALL_PASS_PAID`, which stays off). **There is no purchase path.** `WayfarersRoad.ClaimPaid` refuses
every call, and `WayfarersRoadState` has no field that could record an unlock, so no save can carry one.
`CheckPassPaidTrack` proves all three. A Sealed 5-star copy at tier 30 is listed there because a purchased hero
is Sealed (`DESIGN-SEALED.md`).

## The flag

`WayfarersRoadSwitch.Enabled`, **ON by default**, like `OnboardingSwitch`. Define `EMBERFALL_WAYFARERS_ROAD_OFF`
to roll back. Off means no point is counted, no screen is built, no door is drawn, every call refuses `Off` and the
save is the document the build before wrote. A season already in a save is kept and written back untouched.
`CheckPassOff` proves the same three days of loop play with the road on are the off save plus the `wayfarersRoad`
key and version 24, byte for byte.

## The season across a clock change (`CheckPassRollover`)

- The season is a block of reward days from the epoch; a minute before the turn is season 5, a minute after is
  season 6, and a season is exactly 28 days.
- The stamp (`WayfarersRoadState.SeasonIndex`) only moves forward. A clock **wound back** rolls nothing and reopens
  nothing: the later season stands and the lapsed season's tiers do not return. The loop's own stamps stop a
  wound-back day paying twice.
- A clock **jumped forward** lands in the season it names, empty. The seasons it skipped pay nothing.

## The save and the content

Key `wayfarersRoad`: the season, the points, a bit mask of claimed tiers and the bought count, written only once a
point has been earned. Version 24 (`SaveSerializer.HoldsVersion24Data`). `wayfarers-road.json` is **not hashed** into
`ContentVersion`, for `guild-path.json`'s reason.

## The screen

`WayfarersRoadScreen`: a summary strip (tier reached, points, a bar, days left, the two buy buttons), the 30 tiers in
three pages of ten with every reward and what it is worth today, a hero picker for the two selectors with a confirm
step that names the hero, and the locked paid row when its switch is on. The door is a button in the header of the
Orders page, since its points are that page's claims. It decides nothing: `WayfarersRoad.View`, `Claim` and
`BuyTier` do. Capture targets: `Screen:WayfarersRoad`, `WayfarersRoadPaid`, `WayfarersRoadPicker`.

## Limits

- Every number is a first pass. Whether 22 hours of idle and 140 Dust sweeps over a season is generous, and a
  5-star choice for a month of daily play is the right price, is a balance call; the only measured fact is the
  sizing (24 days).
- The pass does not count Proving, Recital or campaign progress, only the loop. A player who skips the orders gets
  nothing from it.
