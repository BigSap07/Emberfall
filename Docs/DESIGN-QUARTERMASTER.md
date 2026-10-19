# DESIGN-QUARTERMASTER: the Gold sink

Decision record, dated 2026-10-19 (R17). It builds `DESIGN-SHOP.md`, which stays the reasoning for
what the shop sells and why. The numbers live in `quartermaster.json`; read that file for the truth
about any one price.

## What it is

A daily stock of **rolled armor, one piece per slot (six), sold for Gold**, restocked at the reward day
(04:00 UTC, the same clock as the daily loop and the sinks). A slot stays on the shelf as SOLD until
the next restock. It is opened from the Forge (`QUARTERMASTER` button, entry id `forge.Quartermaster`).

Rolled armor bought with Gold is fine: Gold is earned. Rolled armor bought with **Sealed** Gold is
not (a roll), so the shop spends `PlayerProfile.FreeGold` only and refuses Sealed Gold as `SealedGold`
(see `DESIGN-SEALED.md`).

## The sizing, measured first (DESIGN-SHOP section 1's open item)

`QuartermasterSizing` in `QuartermasterChecks.cs` reads the reference walk (`SimulateEconomy`, the
player the curve band is graded on). Per chapter, Gold earned less Gold spent on levels is the surplus:

| Chapter | Days | Earned | Spent on levels | Surplus | Held at end | Idle Gold per hour | Surplus in idle hours |
|---|---|---|---|---|---|---|---|
| 1 | 10 | 87,755 | 83,051 | 4,704 | 5,104 | 670 | 7 |
| 2 | 24 | 905,045 | 890,327 | 14,718 | 19,822 | 2,960 | 5 |
| 3 | 24 | 2,909,860 | 2,902,797 | 7,063 | 26,885 | 7,580 | 1 |
| 4 | 24 | 6,445,620 | 3,665,565 | 2,780,055 | 2,806,940 | 15,400 | 181 |
| 5 | 24 | 12,230,100 | 0 | 12,230,100 | 15,037,040 | 27,600 | 443 |
| 6 | 24 | 20,894,300 | 0 | 20,894,300 | 35,931,340 | 45,100 | 463 |

The finding: **Gold is level fuel until chapter 3 and a pile from chapter 4**, because every deployed
hero is at its level cap. By 6-12 the walk holds 35.9 million Gold and 122 of 260 sittings end
cap-blocked. That is the gap DESIGN-SHOP argued for, now a number.

## The price rule

A price is **hours of the player's own idle Gold at their frontier**, never a Gold amount:

`Price = ceil(hours x IdleYield(3600).Gold / priceRound) x priceRound`, never below `priceRound` (10).

`IdleYield` is the one formula behind the idle chest and every daily-loop reward, read at the furthest
stage cleared. Prices are worked out when asked and never stored, so a retune of the economy moves
every price with it. Hours: **Rough 30, Honed 90**. Temper comes from the same roll a drop uses
(`ArmorRoller.RollTemper`, tilted by the frontier), so the shop never sells above `ArmorInfo.MaxDropTemper`.

A day's stock averages 270 to 333 idle hours across the campaign. Against the table: a chapter's
surplus buys **0.6, 1.4 and 1.4 days of stock in chapters 4, 5 and 6**, and under 0.04 in chapters
1 to 3, where the Gold is level fuel and the shop cannot compete with levelling. `QuartermasterSizing`
asserts both bands. It is a slow drain by design: about one day's stock per chapter, with 24 shelves
to choose from.

A bought piece salvages for 4 to 9 Slag, so the shop is a sink and never a faucet.

## The stock

Rolled when the shop is opened (`Quartermaster.Open`) on a new reward day, from a seed made of the day
and the shelf, so every player sees the same pieces on the same day and a check can reproduce them.
Stored as **resolved pieces** (`QuartermasterState`), not a seed, so an `armor.json` retune never
rerolls a shelf a player is looking at. The stamp only moves forward: a clock wound back rolls nothing
and reopens nothing. `Buy` never rolls, so a purchase is only ever of a shelf the player was shown.

## The flag

`QuartermasterSwitch.Enabled`, **ON by default**, like `OnboardingSwitch`. Define
`EMBERFALL_QUARTERMASTER_OFF` to roll back. Off means no screen is built, no door is drawn,
`Quartermaster.Buy` refuses `Off`, and the save is the document the build before wrote (version 22,
40 keys). A shelf already in a save is kept and written back untouched.

## The save

Key `quartermaster`, version 24 (the one R17 bump, `SaveSerializer.HoldsVersion24Data`), written only
once a stock exists. Absence is no stock; no migration. `quartermaster.json` is **not hashed** into
`ContentVersion`, for `guild-path.json`'s reason: no fight reads it.

## The checks

`CheckQuartermaster` in `QuartermasterChecks.cs`: content, price, stock, sale, every refusal leaving
the save byte for byte unchanged (Locked, Stale, NoSuchPiece, NotEnough, Sold, SealedGold), the clock,
the save, not hashed, combat does not move, the rollback (`CheckQuartermasterOff`) and the wiring.

## Limits

- No paid refresh in v1 (DESIGN-SHOP section 8): a refresh would make the daily drain unbounded.
- The instruments still field an unarmored or drop-armored line; a bought piece is a power source the
  curve band does not model. The shop is priced so a player cannot afford a day's stock before
  chapter 4, which is where the walk's Gold stops being level fuel.
