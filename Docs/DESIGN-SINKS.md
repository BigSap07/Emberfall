# DESIGN-SINKS: the Armory and the Archive

This is a decision record, dated 2026-10-18. It says what the two sink shops sell, what they charge,
and why. The numbers live in `sinks.json` and `balance.json`; read those files for the truth about any
one price.

## What it is and why

Valor (paid by the Proving) and Coda (paid by the Deep Recital) were two purses with nothing to spend
on. `DESIGN-PVP.md` section 6 left the Valor sink as an open decision. This run made it, and made the
same call for Coda.

- **The Armory** sells for Valor. It is opened from the Proving screen.
- **The Archive** sells for Coda. It is opened from the Deep Recital screen.

Both sit behind one flag, default off. With the flag off the build is the build before them.

## The Belgian rule

Nothing bought with money may enter a random mechanic. These shops sell for **earned currency only**
(Valor and Coda come from play, never from a purchase) and they sell **nothing random**. Every item is
fixed. There is no roll, no pull, no pity counter, no shuffle. `kind` in `sinks.json` is a closed set of
four: `armor`, `brand`, `echo`, `heroCopy`. An unknown kind is dropped with a warning, so a random item
cannot be written into the file by accident. `CheckSinksNothingRandom` reads the sink source files and
fails on any random call.

## What each shop sells

| Shop | Item | Days of income | Cap |
|---|---|---|---|
| Armory | Six Honed armor presets, one per slot | 6 each | 3 a month, shared by all six |
| Armory | Brand, +1 Proving attempt | 0.25 | 3 a day |
| Armory | Echo, +1 Recital attempt | 1 | 1 a day |
| Armory | One copy of a four-star hero the player picks (14 listed) | 12 | 1 a month |
| Archive | One copy of a four-star hero the player picks (12 listed) | 12 | 1 a month |

- **Armor presets** are built by hand from `sinks.json`: Proving Edge (Weapon, Ember), Proving Casque
  (Helm, Verdant), Proving Mail (Plate, Lumen), Proving Treads (Boots, Gale), Proving Ring (AccessoryA,
  Umbra), Proving Pendant (AccessoryB, Tide). All are Honed, which is `ArmorInfo.MaxDropTemper`. The
  aspects rotate so no preset completes an attunement set alone. Stat values sit inside `armor.json`'s
  ranges, and `LoadSinks` drops a preset that does not, so a preset is never better than a roll could
  have been. The shop never calls `ArmorRoller`.
- **Brand and Echo** each add one attempt to the bank through `AddProvingBrand` and `AddRecitalEcho` in
  `GameSession`. Both refuse when the bank is full, keep the regeneration remainder, and never go above
  the bank's size.
- **Hero copies** go through `GrantHeroDirect`: a copy for an owned hero, a new hero otherwise. It
  touches no summon count, pity, Shards or Gold. The lists are explicit ids, four star heroes only,
  never a starting hero, and no hero is in both lists. No five-star is sold. A roster addition never
  changes a shop silently.
- **Titles were skipped.** No title or profile field exists to put one in. Say so if one is wanted.

## The price rule

A price is **days of income**, never a currency amount. One day of income is a full day of attempts,
every one won, at the **highest** standing or floor the player has reached.

- `AttemptsPerDay` is the smaller of the bank's size and `24 / recharge hours`, floored. Both banks give
  5 today.
- `ValorDay(S) = 5 x (provingRewardValorBase + provingRewardValorPerStanding x S)`, where S is
  `ProvingHighestStanding`. At the shipped 30 and 2 that is `5 x (30 + 2 x S)`.
- `CodaDay(F) = 5 x (recitalCodaBase + recitalCodaPerFloor x (F + 1))`, where F is
  `RecitalHighestFloor`. At the shipped 5 and 1 that is `5 x (6 + F)`. The `+ 1` is because the next
  attempt is fought one floor above the highest cleared.
- `Price = days x Day`, rounded **up** to `priceRound` (5), never below 5.

Both formulas copy `GrantProvingResult` and `GrantRecitalResult` in `GameSession`. `CheckSinksPriceFormula`
ties them to the real grants. The Coda day ignores the Guardian floor bonus, which prices slightly
high. That is the safe side.

Why days: a fixed amount would be generous in week one and worthless by week ten. Days follow the
player. The highest standing is used, not the current one, because the current standing can be lowered
by losing on purpose, and a price that follows it could be lowered by throwing fights. A retune of
`balance.json` moves every price with it. `SinkPricing` is the only place days become currency, and the
price is computed at display and at purchase from that one function, never stored.

### Worked prices

Armory (Valor), day = `5 x (30 + 2 x S)`:

| Highest standing | One day | Armor (6 d) | Hero copy (12 d) | Brand (0.25 d) | Echo (1 d) |
|---|---|---|---|---|---|
| 0 | 150 | 900 | 1800 | 40 | 150 |
| 10 | 250 | 1500 | 3000 | 65 | 250 |
| 30 | 450 | 2700 | 5400 | 115 | 450 |

Archive (Coda), day = `5 x (6 + F)`:

| Highest floor | One day | Hero copy (12 d) |
|---|---|---|
| 0 | 30 | 360 |
| 10 | 80 | 960 |
| 50 | 280 | 3360 |
| 100 | 530 | 6360 |

## The caps

| Cap key | Shared by | Limit | Period |
|---|---|---|---|
| `armor` | the six presets | 3 | month |
| `brand` | Brand | 3 | day |
| `echo` | Echo | 1 | day |
| `armoryHero` | Armory hero copy | 1 | month |
| `archiveHero` | Archive hero copy | 1 | month |

- A **day** is the reward day, the last `dailyResetHourUtc` boundary (04:00 UTC), the same clock as the
  daily loop. A **month** is the calendar month of that reward day, so a monthly cap comes back at the
  first reward day of the next month.
- Stamps only move forward, like `DailyLoopState`. A clock wound back compares as "not a later period"
  and reopens nothing.
- Items that share a cap key share one counter.

## The Echo note

**The Echo is the one item that turns Valor into Gold, Shards and Dust.** An extra Recital attempt pays
those. It is priced at a full day of Valor and capped at one a day so the leak stays small, but it is
the one place the two economies touch. **R13 should review the Echo price.** Everything else the shops
sell is inert against those three.

## The flag

- Define `EMBERFALL_SINKS_ON` in the scripting defines. `SinksSwitch.Enabled` is a writable static,
  default false, a copy of `DailyLoopSwitch`. The harness and the capture targets force it and put it
  back.
- **How the owner turns it on:** add `EMBERFALL_SINKS_ON` to the scripting define symbols for the
  build. **Rollback:** remove the define.
- Off means: neither screen is built (`ScreenRouter` builds them only inside the switch guard), no door
  leads to one, `SinkShop.Buy` refuses with `Off`, and `SinkShop.View` answers inactive. State already
  in a save is read and written back untouched, so switching off never costs a player what they bought.
- Off also leaves `BagPanel`'s Valor and Coda lines as they were. On, they say where each is spent.
- The static differences with the flag off are inert: two `ScreenId` members and four
  `FeatureEntryPoints` rows.

## The save

- The profile gains `Sinks` (`SinksState`): the day's and the month's cap counters, with the day stamp
  and the month key. It is saved under the key `sinks`, only when it holds something, and read on every
  load whatever the switch says. A damaged block reads empty and counts are clamped.
- `SaveSerializer.CurrentVersion` is 23, the highest version this build reads. `BaseVersion` is 22.
  **`Serialize` stamps 23 only when the `sinks` block is written, and 22 otherwise** (`VersionOf`). A
  save with no purchase is byte for byte the document the build before wrote, version number included,
  so with the flag off nothing changes for the server.
- The cloud push sends the document's own version (`SaveSerializer.VersionOfJson`), never the highest
  this build reads, so a flag-off push still claims 22.
- A version 22 build drops the `sinks` key on write, which would erase the caps. The bump to 23 refuses
  that, but only for a save that holds a purchase.
- **The owner must raise the server's limit before a cloud player with the flag on buys anything.** Run
  this once:

```sql
update public.save_limits set max_save_version = 23, updated_at = now();
```

## sinks.json is not hashed

`sinks.json` is the ninth content file and is deliberately outside `ContentDatabase.ContentVersion`,
for `guild-path.json`'s reason: nothing in it is read by a fight. `ContentDatabase.LoadSinks` reads it
after `Load`, from `ContentService.Load` in the game and from the harness loader. Retuning a price must
not retire every recorded guild dungeon replay. Do not edit any of the eight hashed files, comments
included, for a shop change. `CheckSinksNotHashed` proves a price retune moves prices and not
`ContentVersion`, and `CheckSinksNoCombatChange` proves a fixed battle fingerprint is the same with
`sinks.json` loaded and removed.

## The screens and their doors

- `SinkShopScreen` is the base, on `RelicShopScreen`'s furniture: a purse plate, a paged card row, a
  cost chip that goes red when unaffordable, a Buy button, a footer with the cap resets. `ArmoryScreen`
  and `ArchiveScreen` are thin subclasses titled THE ARMORY and THE ARCHIVE. A screen only calls
  `SinkShop.View` and `SinkShop.Buy`; it works out nothing.
- Card states: buyable, NOT ENOUGH, SOLD OUT with the reset time, BANK FULL, LOCKED. A hero card opens
  a picker inside the same screen, with a confirm step.
- Doors: an ARMORY button on `ProvingScreen` beside the Valor purse, an ARCHIVE button on
  `RecitalScreen` top right, outside the banner. Both are built only inside the switch guard. The
  `FeatureEntryPoints` rows are `screen:Armory`, `screen:Archive`, `proving.Armory` and
  `recital.Archive`, so `CheckFeatureUnlockGating` knows every door.
- Unlocks follow the Proving and the Recital. `SinkShop` asks `FeatureEntryPoints`, the one table that
  decides an unlock, and an armor item also needs the armor bag open.
- Capture targets in `PlayModeScreenshot`, each forcing the switch on and restoring it:
  `Screen:Armory`, `ArmoryBought`, `ArmorySoldOut`, `ArmoryPicker`, `Screen:Archive`, `ArchiveBought`,
  `ArchiveSoldOut`, `ArchivePicker`, `Screen:ProvingSinksOn` and `Screen:RecitalSinksOn`. They seed the
  scratch identity only and every purchase goes through `SinkShop.Buy`.

## The harness checks

`SinkChecks.cs` in `Tools/DevHarness`, wired by `CheckSinks`, listed in `Tools/build-check.ps1`:

- `CheckSinksContent`: the shipped file loads clean, prices are positive multiples of 5, a corrupted
  preset or a five-star in a list is dropped with a warning.
- `CheckSinksPriceFormula`: the day formulas equal the real grants.
- `CheckSinksBuyDeterministic`: two sessions, same buys, byte-identical saves.
- `CheckSinksSpendsOnlyItsPurse`: only the shop's own purse falls, by exactly the price.
- `CheckSinksCaps`: day and month caps hold and reopen on the reward day boundary; a wound-back clock
  reopens nothing.
- `CheckSinksBanks`: refuse when full, add exactly one, never above the bank's size.
- `CheckSinksRefusals`: each refusal reason, with a save that is byte-identical afterwards.
- `CheckSinksNothingRandom`: no random call in the sink sources, the kind set is exactly four.
- `CheckSinksWiring`: screens and doors only inside the switch guard, capture targets force the switch.
- `CheckSinksSave`: round trip, old saves load empty, damaged blocks load empty, flag off keeps the block.
- `CheckSinksNotHashed` and `CheckSinksNoCombatChange`: as above.
- `CheckSinksOff`: the rollback. Flag off, `Buy` refuses with `Off`, the save equals the one the build
  before wrote, version 22 included.

## Limits

- The Archive sells one card. A player who has stalled in the Recital earns no Coda.
- Hero copies feed conversion, which is why they are capped at one a month per shop.
- The Coda day prices the Guardian floors as ordinary floors, a little high.
- Prices read `Balance` at runtime. If an economy branch changes the Valor or Coda grants, the prices
  follow it.
- Armor presets salvage for far less than their price, so a purchase cannot be turned into profit.
