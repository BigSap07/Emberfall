# Design: the daily loop

Decision record from 2026-10-17 (run R5). The reasoning is durable. Names and numbers are not.
`DESIGN-BUILD-ORDER.md` says what is built.

## What it is and why

Five small orders a day, a chest for finishing them, a weekly chest for keeping at it, and a
7-day login calendar. It tells a new player what to do tomorrow. It is the free system in
section 4 of `IDEAS-ECONOMY-PACKS.md`, an ideas document the owner supplied (it lives outside
the repo), that also helps the playtest.

It pays in **hours of idle income at the player's own frontier**, not in fixed amounts. A fixed
shard payout is generous at chapter 2 and worthless at chapter 4, because shard income grows
about 36 times (the owner's `IDEAS-ECONOMY-PACKS.md`) while a summon stays at 120 shards. An hour of income scales with the player and
never needs retuning.

It is gated by `DailyLoopSwitch.Enabled`, **on by default since the owner's decision of 2026-10-23**;
defining `EMBERFALL_DAILY_LOOP_OFF` rolls it back. Off, the game is the game that existed before this run.

## The rules

Rules live in `DailyLoop` (Core/Progression). State lives in `DailyLoopState`. Config lives in
`OrdersConfig`. `GameSession` reports what happened through `OrderEvent` at the moment it
happens, never a screen.

- **Five orders a day, from a fixed pool.** The pool has five slots: clear waves, win a stage,
  spend Gold, summon once, and fight in the Proving or the Recital. **Spend Gold replaced "level up
  a hero" on 2026-10-23** (owner): it counts Gold spent on hero levels and at the Quartermaster
  (`OrderKind.Gold`), and its 1,000 Gold is at most one level of any hero (1,070 at the cheapest) and
  one Quartermaster piece, so a roster whose line, or whose every hero, is at its cap can still finish
  it (`CheckDailyLoopSpendGold`). `DailyLoop.BuildBoard`
  takes one entry of each slot, chosen by the day alone, so the same day is the same board for
  everyone with the same unlocks.
- **Unlock awareness.** An entry whose features are all locked is never offered.
  `DailyLoop.Unlocked` asks the one unlock table through `FeatureEntryPoints`: it takes
  `FeatureEntryPoints.Of(feature)` and asks `FeatureEntryPoints.Allows` of the feature's first door. The arena
  slot needs the Proving or the Recital. Until one is open, the slot falls back to a plain
  "clear 6 waves" entry marked `fallback` in `orders.json`.
- **The board is stored, not re-derived.** It is generated once when the reward day rolls and its
  ids are kept in `DailyLoopState`. Retuning `orders.json` mid-day cannot reorder or remove an
  order a player is halfway through.
- **Claim once.** `DailyLoop.ClaimOrder` pays an order once. It refuses an order that is not on
  today's board, not finished, or already paid. Progress after a claim does not reopen it.
- **What counts.** A lost wave, a lost sweep and a watched replay count nothing. A sweep or a
  ladder win past the repeat cap pays nothing and counts nothing for the stage-win order, and a
  capped sweep counts no waves. The ladder's mid-wave hooks are not cap-gated, but a cleared
  stage cannot be fought the long way: `GameSession.ResolveWave` refuses it and the campaign
  sends it to the sweep. A Proving or Recital fight counts won or lost.
- **Daily chest.** `DailyLoop.ClaimDailyChest` opens once every order on the board is claimed.
  An order whose definition has left `orders.json` cannot be claimed, so `DailyLoop.AllClaimed`
  ignores it and it does not hold the chest shut.
- **Weekly chest.** It opens once five days of one week are counted. A day counts only when
  every order that day is **claimed**, which is stricter than done. Weeks run Monday to Sunday by
  reward day (`DailyLoop.WeekOf`). The days need not be consecutive.
- **Lapse.** Unclaimed orders and chests lapse at reset. A weekly chest not opened by the end
  of its week is gone.
- **7-day calendar.** It repeats. Rewards are fixed and shown in advance. It is claim driven:
  at most one claim per reward day (`DailyLoop.CanClaimCalendar`), and a missed day delays the
  next reward and skips none. After day 7 it starts again at day 1.
- **Rollover.** The day changes on the existing reset clock, not a second timer. This follows
  `DESIGN-SHOP.md` section 4: `GameSession.DayStartUnix` and `GameSession.RollDailyReset`
  already drive the repeat cap and read `dailyResetHourUtc` from `balance.json`.
  `DailyLoop.Roll` uses `DayStartUnix` and acts only on a strictly later day.
- **A wound-back clock reopens nothing.** The loop keeps its own stamps in `DailyLoopState`:
  `DayUnix`, `WeekIndex` and `CalendarDayUnix`. Each only moves forward. An earlier clock
  compares as "not a later day" and changes nothing, so it cannot reopen a board, a chest or the
  calendar. It is the same immunity `RollDailyReset` gives the repeat cap. A player who forces
  the clock forward only advances their own calendar. There is nothing to sell and no
  leaderboard.

## Rewards

Every reward is hours of idle income at the frontier, paid by `GameSession.IdleYield`, the
function the idle chest itself is paid by. `DailyLoop.Preview` asks it for the frontier
(`GameSession.FurthestClearedStage`) and `DailyLoop.Pay` grants the result. The rate is read at
**claim time**, so a reward claimed after a new stage clear pays at the new frontier.

Loop rewards are **not capped by `Balance.IdleCapHours`**. That cap limits what sits unclaimed
in `GameSession.PeekIdle`. A loop reward is granted, not accrued.

**The hours below are final as of the content pass of 2026-10-22, measured rather than chosen** (see
**Measured on the curve**, below). Nothing in code reads the size of a number.

| Reward | Hours |
|---|---|
| Each order | 0.25 to 0.5 (set per entry in `orders.json`) |
| Daily chest | 0.75 |
| Weekly chest | 4 |
| Calendar days 1 to 3 | 0.5 each |
| Calendar days 4 to 6 | 1 each |
| Calendar day 7 | 2 (labelled "Day 7 bonus") |

As set, a player who finishes the loop every day earns about 2.4 hours of idle income a day from
orders and the daily chest, 17 hours a week, plus 4 for the weekly chest and 6.5 for one lap of
the calendar. That is 27.5 hours a week, about two fifths of the 70 hours a week one full idle chest a day
pays at its cap (`idleCapHours` is 10 in `balance.json`). The walk measured 26.8 a week (below).
No item rewards ship. The 4-star selector `IDEAS-ECONOMY-PACKS.md` puts on day 7 is left out.

## Measured on the curve (2026-10-22)

`DevHarness.exe --loop-curve [walks] [scale]` (`Tools/DevHarness/LoopCurve.cs`) walks the reference
player, the one `recommendedPower` and the power band are derived from, three ways on one clock: loop
off (the control), loop on, and loop on with Wayfarer's Road. The loop player is diligent: at the end
of every sitting they claim every finished order, both chests and the calendar. The walk's own waves,
stage wins, level ups and summons progress the orders through `GameSession`, exactly as in the game;
the arena order is credited, because the walk never fights the Proving or the Recital. The level order
was credited too until 2026-10-23; its replacement, spend Gold, is finished with real Gold off the line
when the capped line buys nothing (`LoopFinishSpendGold`). The arena credit makes the loop's income an
upper bound for a twice-a-day player.

Over 8 walks, with the hours as shipped:

| | off | loop | loop and road |
|---|---|---|---|
| loop income a day | | 3.84 hours | 3.84 hours |
| banded stages over their band | 0 of 72 | 0 | 0 |
| stuck walks | 0 | 0 | 0 |
| 1-12, the chapter 1 finale (band top x0.99) | x0.937 | x0.960 | x0.960 |
| 2-1 (band top x1.25) | x1.173 | x1.193 | x1.232 |
| 2-6 wall | x1.184 | x1.191 | x1.220 |
| free track seasons finished | | | 34 (74 hero selectors) |

**Re-measured 2026-10-23**, with the loop on by default, spend Gold in place of the level orders and the
walk on its own clock (`--loop-curve 8`): 0 of 72 banded stages over their top in every column, no walk
stuck, 261 sittings in each; the loop pays 3.81 hours a day (3.83 with the road, 26.8 a week); 1-12 reads
x0.937, x0.935 and x0.938, and 2-1 x1.173, x1.192 and x1.208 (off, loop, loop and road); the road finished
34 seasons with 74 hero selectors, as before. Spending real Gold on the order where the first run credited
it is what takes a little off chapter 1 and 2-1.

At twice the hours (first run, `--loop-curve 8 2`) nothing leaves its band either, but 1-12 reads x0.988 and 2-1
x1.243, both at their edge. That is why the hours were kept rather than raised. From chapter 4 the
reference player piles up Gold it cannot spend, so loop Gold buys little power late, and the
differences between the columns there (a hundredth or two either way) are draw noise between walks.
**The free track moves the curve more than the hours do**: its 4-star and 5-star selectors every
season are what lift 2-1 and the chapter 2 walls.

**One harness defect the study found, fixed 2026-10-23.** `GameSession.LevelUpHero(hero, times)` and
`Ascend(hero)` read the device clock, and the economy walk called both forms. With the loop on, the
first level up recorded an order at today's real date, which rolled the board months ahead of the
walk's clock and froze it: the first run claimed one board in 134 reward days. The walk passes its own
clock to both now (`SpendGold` and `EconomyConvertAscendAndDeploy` in `Program.cs`). The R24 whale
study was re-run after the fix and did not move: it models events as fixed offers and reads no order
or event counter. `LoopClockSeason` still starts this study decades ahead, so its columns stay
comparable with the first run.

## Turning it on by default: what changed (done 2026-10-23)

The owner turned it on. Item by item, against the list written before:

1. **The switch.** Done: `DailyLoopSwitch` defaults to on and `EMBERFALL_DAILY_LOOP_OFF` rolls it back.
   No harness check asserts a default; the daily loop checks force it on and put it back.
2. **The screen.** `OrdersScreen` is a working placeholder built from the Emberforge kit, and its door
   on the Hub rail appears only with the switch. It needs its final layout and a capture of every
   `Screen:Orders*` target, which is client UI work.
3. **The economy.** The reference walk does not play the loop, so the curve was solved without it.
   The table above says the shipped hours keep every stage in its band, which means the curve can stay
   as it is; it also means the walls cost the loop player even less than they cost today (they already
   cost the reference player no extra sittings at the median). If the owner wants walls to bite for a
   loop player, the loop has to enter the reference walk and `recommendedPower` re-solved, which is a
   balance pass, not a switch.
4. **The level order.** Done: retargeted to "spend 1,000 Gold" (above). In the 60-walk daily loop curve
   it was claimed on 100% of the days it was on the board, for both players, by a player who spends one
   bench level or Quartermaster piece when the line cannot (`Docs/DAILY-LOOP-CURVE.md`). A player who spends
   only on the line finishes it on 13.9% of its days, as with the level order, so the Orders screen should
   say where Gold can go.
5. **Wayfarer's Road wakes up.** It is on by default (`WayfarersRoadSwitch`) but earns nothing while the
   loop is off, because its points are the loop's claims. With the loop on, every season's free track
   pays its hourglasses, Slag and Dust and a free 4-star at tier 15 and a free 5-star at tier 30. Sized
   on 24 active days with the weekly chests, the diligent player above finished 34 of about 38
   seasons. Retuning the loop's hours does not change the road's pace: it counts claims, not hours.
6. **The paid pass becomes sellable.** The pass premium in the Store refuses to sell while the loop is
   off; with it on, the PASS tab's premium can be bought once real purchases exist.
7. **The save and the server.** Version 22 adds the `dailyLoop` block, and nearly every active save is
   already version 26 for the live events, so the server's `save_limits` row must say 26 in any case.
8. **The live events.** Their quests already count the same moments with the loop off; the
   claim-an-order quest kind only starts counting once orders can be claimed.

## Content: orders.json

`Assets/Resources/Content/orders.json` holds the pool, the daily and weekly chests and the
calendar. Each pool entry has `slot`, `kind`, `target`, `hours`, `text`, optional `requires`
(features of which at least one must be open) and optional `fallback`. There is no gold or
shard amount in the file on purpose.

It is **not part of `ContentVersion`**. `ContentVersion` retires every recorded guild dungeon
replay when it changes, and nothing in a fight reads this file. Retuning a reward must not
retire every guild's replays. It loads after `Load` through `ContentDatabase.LoadOrders`, the
way `guild-path.json` loads through `ContentDatabase.LoadGuildPath`.
`CheckDailyLoopNotHashed` proves `ContentVersion` is still the hash of the eight fight files.

## Save

`SaveSerializer.CurrentVersion` goes from 21 to 22. Version 22 adds a `dailyLoop` block, written
only when `DailyLoopState.IsEmpty` is false. It holds the day stamp, each order's id, progress
and claimed state, the daily chest flag, the week index, counted days and weekly chest flag, and
the calendar position and last claim day. A profile that never switched the loop on serialises
exactly as before. A version 21 save loads with an empty loop. State already in a save is kept
and written back untouched when the switch is off, so turning it off never costs a player what
they earned with it on. `DailyLoopState.Read` clamps every number to what the game could have
written. `CheckDailyLoopSave` proves it.

**The server must accept it before a cloud player ships.** `push_save` refuses a version 22 save
until the `save_limits` row is raised: `update save_limits set max_save_version = 22;`. This is
the owner's action.

## The flag

`DailyLoopSwitch.Enabled` is true unless the scripting define `EMBERFALL_DAILY_LOOP_OFF` is set
(on by default since 2026-10-23; before that it was off unless `EMBERFALL_DAILY_LOOP_ON` was set).
It follows the pattern of `StoreSwitch`. Off, no order is generated, no progress is counted,
no key is written and the hub shows no door.

The field is writable so the harness and the capture targets can force it. Whatever sets it puts
it back. Rollback is `EMBERFALL_DAILY_LOOP_OFF` under Player Settings, Scripting Define Symbols.
`CheckDailyLoopOff` proves the off state.

## The screen and the hub entry

`OrdersScreen` (`Assets/Scripts/UI/Screens/OrdersScreen.cs`) holds all of its layout in that one
file. It reads only `DailyLoop.Board` and calls only the four `DailyLoop` claims. It uses the
shared top bar. The hub has an Orders door on the `MainMenuScreen` rail, built only when
`DailyLoopSwitch.Enabled`, with a dot while anything can be claimed. `ScreenRouter` builds the
Orders page only when the switch is on, and `ScreenId.Orders` is in `FeatureGate.OpenScreens`.

Capture targets: `Screen:Orders`, `OrdersHalf`, `OrdersClaimed`, `OrdersCalendar1`,
`OrdersCalendar4`, `HubLoopOn` and `HubLoopOff`. They force the switch through
`PlayModeScreenshot`'s SessionState key, editor only, and the nightly sweep picks them up.

## What is a placeholder

The UI. (The numbers in `orders.json` were placeholders until the content pass of 2026-10-22.) The orders screen is built in code from the Emberforge
kit and carries no final art or copy. The rules, the data and the save are meant to last. The
screen is meant to be redone.

## Checks

`DailyLoopChecks.cs` in `Tools/DevHarness` holds `CheckDailyLoopContent`,
`CheckDailyLoopBoard`, `CheckDailyLoopProgress`, `CheckDailyLoopClaims`,
`CheckDailyLoopChests`, `CheckDailyLoopCalendar`, `CheckDailyLoopClock`,
`CheckDailyLoopSave`, `CheckDailyLoopNotHashed`, `CheckDailyLoopOff` and
`CheckDailyLoopWiring`. The last is a tripwire that reads source (the hub door, the router gate,
the thin screen and the capture forcing). It is not behavioural proof.
