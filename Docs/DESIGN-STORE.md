# DESIGN-STORE: the Store, its offers and the rule they all obey

Decision record, dated 2026-10-19 (R19). Source: `IDEAS-ECONOMY-PACKS.md` sections 2 and 3. A Store on the Hub with the season pass, the packs and the currency shops in one place, and deterministic packs sold through a purchase path that runs against a fake store in the editor, a debug store in a development build and "coming soon" in a release build. **No real money moves in this build and nothing in it can take any.** The numbers live in `store.json`; read that file for the truth about any one offer.

## The rule

Belgium treats a paid random reward as a loot box. Summon and Conversion are random. So:

> **Everything a purchase grants is Sealed. Sealed never enters a summon, a Conversion, the forge gamble or any roll. Every pack's contents are fixed and shown in full, with the price, before it is bought.**

How the build enforces it, and what proves it:

- **One grant path.** Every good a purchase or a payout of something bought gives is made by `PurchaseGrants.Apply`, which marks it through `SealedGrants`. That covers the purchase itself (`PurchaseLedger.Complete`), an Ember Ledger day, a Growth Fund milestone, the ledger's share of an idle claim and a paid Wayfarer's Road tier. `CheckSealedNothingCallsTheGrant` proves no other file calls `SealedGrants`.
- **No random call in the path.** `CheckOfferNothingRandom` reads `StoreOffers.cs`, `StoreConfig.cs`, `PurchaseGrants.cs` and `SealedGrants.cs` and fails on a random, a summon, a roll, a conversion or a pity word. `StoreConfig` has no field that could hold a chance, and `store.json` is read for the words "chance", "roll", "pool", "pity", "weight" and "probability" too.
- **A closed set of grants.** `GrantKind` is a closed list (Gold, Slag, Dust, DustSweeps, IdleGoldHours, HeroCopy, HeroChoice, OwnedHeroCopy, PassPremium, Activate, and since R24 RelicChoice and Skin). None rolls. `StoreOfferKind` in `store.json` is closed too (pack, starter, ledger, growth, pass, contract, and since R24 relicChoice; a skin offer is derived from `skins.json`, never written by hand); an unknown kind, tier or reset is dropped with a warning.
- **Deterministic.** `CheckOfferFixedAndDeterministic`: two sessions in the same state read the same card and, buying the same offers, hold byte-identical saves.
- **Sealed on every grant.** `CheckOfferSealedGrants`: after each pack the Gold that arrived is exactly the Sealed Gold part, and the Gold, Shards and Slag a random mechanic may spend did not move.
- **Refused by the random mechanics.** `CheckOfferRefusedByRandomMechanics`: a bought 5-star is a Sealed copy and counts as no spare body for a Conversion, and a basket of every pack leaves no Shard to summon with.

What a Sealed good is worth, honestly: **Sealed Gold** buys level-ups (the deterministic sink). **Sealed Shards** buy nothing, because the only Shard sink is the summon, so no offer pays Shards. **Sealed Slag** buys nothing yet, because a reroll and a combine both refuse it, so the shipped offers carry no Slag (the `slag` field works and is tested; a deterministic Slag sink would make it worth selling). **Dust** is not marked, because its only sink is the relic shop, which sells fixed items. **A Sealed hero copy** feeds that hero's own ascension. That last one is the only thing in the Store that moves power.

## The offers

All in `store.json` (not hashed into `ContentVersion`, loaded after `Load` like `sinks.json`). Each has a fixed content, a placeholder euro tier, a placeholder product id, a purchase limit and a reset. The value of every offer in days of frontier income per chapter is in `D:\Emberfall\handoff\R19-STORE\data\offer-values.md`.

| Offer | Contents | Limit | Reset | Placeholder tier |
|---|---|---|---|---|
| Starter Pact | one 5-star copy of three named (Brenna, Vaelis, Nereia) + 6 hours of idle Gold | 1 | never | EUR 4.99 |
| Weekly Pack | 24 hours of idle Gold + 10 Dust sweeps | 1 | week | EUR 1.99 |
| Monthly Pack | 120 hours of idle Gold + 60 Dust sweeps | 1 | month | EUR 9.99 |
| Ember Ledger | 2 hours of idle Gold on each reward day claimed, for 30 days, and +10 percent Gold on every idle claim while it runs | one card at a time | expiry | EUR 4.99 |
| Growth Fund | at 2-12, 3-12, 4-12, 5-12 and 6-12: 12, 24, 36, 48 and 72 hours of idle Gold and 10, 15, 20, 25 and 30 Dust sweeps | 1 | never | EUR 9.99 |
| Wayfarer's Road premium | the paid track of the season it is bought in (see `DESIGN-WAYFARERS-ROAD.md`) | 1 | season | EUR 9.99 |
| Hero Contract | one copy of a 5-star the buyer already owns, for that hero's ascension | 2 per hero and 4 per account | month | EUR 19.99 |
| Relic Dust Satchel | 5,000 Relic Dust | 1 | week | EUR 4.99 |
| Relic Dust Chest | 25,000 Relic Dust | 2 | month | EUR 19.99 |
| Relic of Choice | one relic the buyer names, worth at most 20,000 Relic Dust at the shop | 1 | month | EUR 29.99 |
| Skins | one hero skin each (one offer per skin in `skins.json`) | 1 | never | EUR 4.99 to 9.99 by rarity |

- **Hours and sweeps scale with the buyer.** An hour is `GameSession.IdleYield` at the buyer's own frontier (Gold only, never the Shards the same hour pays). A Dust sweep is one repeat-clear payout of the frontier stage, as the pass counts it. So nothing needs retuning when the frontier moves.
- **Limits and resets** (`StoreOffers.Check`, `Record`): a week is Monday to Sunday by reward day (`DailyLoop.WeekOf`), a month is the calendar month of the reward day (`SinkShop.MonthKeyOf`), a season is Wayfarer's Road's. The reward day is 04:00 UTC, the clock the daily loop, the sinks and the pass use.
- **A clock wound back reopens nothing.** Every read and write uses the later of the real reward day and the store's own stamp (the `clock` offer record in `PlayerProfile.Store`, which only moves forward). `CheckOfferClockWoundBack`, `CheckOfferLedger` and `CheckOfferLimitsAndResets` prove a week, a month, a card and a claimed day stay closed across a wound-back clock, a year back and 1970.
- **The Ember Ledger** is a card: buy it, then claim each reward day's hours once. A missed day is not made up. The card's end is 30 reward days from the day it was bought. The 10 percent share is paid by `GameSession.ClaimIdle` through `StoreOffers.OnIdleClaim` as Sealed Gold, and only while the card runs.
- **The Growth Fund** pays as the player reaches each stage. A milestone already passed when the fund is bought is claimable at once. Each reward is FIXED at its milestone's own stage (`StoreOffers.MilestoneGold`, `MilestoneDust`), not at the frontier the claim is made from, so hoarding claims gains nothing: in all it pays 5,343,840 Gold and 11,200 Dust. **It is worth most to an early buyer**: about 400 days of chapter 1 income (417 with Dust) bought at 1-12, 96 at 2-12, 38 at 3-12, 19 at 4-12, 11 at 5-12 and 6.6 at 6-12 (`offer-values.md`). The owner may want the price tier or the amounts to reflect that.
- **The Hero Contract** was OFF behind `EMBERFALL_HERO_CONTRACT` until R24. See "The Hero Contract with a cap (R24)" below: it is now ON, capped at four a month for the whole account, and opens at 4-1.
- **The pass premium** is sold only while the daily orders run (the pass earns its points from them): with `DailyLoopSwitch` off the offer reads "the pass needs the daily orders" and cannot be bought. The paid track is claimed through `WayfarersRoad.ClaimPaid`, and a Sealed 5-star copy comes at tier 30.

## The Store screen

`StoreScreen`, reached from a gold STORE button on the Hub rail (the slot above Orders; a dot says something bought can be claimed). Three tabs.

- **PASS.** The free track and the paid track one above the other, ten tiers a page, every reward shown. The premium unlock button sits in the summary strip, with a link to the road (`WayfarersRoadScreen`) for the free selectors and for buying tiers with Valor or Coda.
- **PACKS.** A list of the offers on the left, the chosen one on the right with its full contents, its price, its limit and, when it is used up, the time it returns. A Starter Pact opens a picker with the three named heroes. The Ember Ledger shows its days left and CLAIM TODAY. The Growth Fund lists its five milestones with a claim on each.
- **SHOPS.** The Proving Armory, the Recital Archive, the Quartermaster and the Relic Shop, each with the purse it spends and its lock line. They keep their own screens; the tab links them. `SinksSwitch` is now on by default (define `EMBERFALL_SINKS_OFF` rolls it back) so the Armory and the Archive exist.

The screen decides nothing and grants nothing: it reads `StoreOffers.View` and `WayfarersRoad.View`, claims through `StoreOffers.ClaimLedger`, `ClaimGrowth` and `WayfarersRoad.Claim` and `ClaimPaid`, and buys only through `PurchaseService`'s flow. `CheckOfferWiring` proves it names no grant, no record and no random call. The mode of the build shows in a banner: TEST STORE in the editor and a development build, COMING SOON in a release build, where every offer is shown in full with its price tier and **no buy button is drawn**.

Capture targets (in `PlayModeScreenshot`): `Screen:HubStore`, `StorePacks`, `StorePacksPicker`, `StorePacksLedger`, `StorePacksGrowth`, `StorePurchase`, `StoreComingSoon`, `StorePass`, `StorePassPremium` and `StoreShops`.

Placeholder icons (listed for Pinoy): the Store rail button (a coin), the offer icons on the Packs tab (a crown for the Starter Pact, a chest for the packs, an hourglass for the Ledger, a gem for the Growth Fund, a sigil for the pass, crossed swords for the Contract), the reward icons on the pass tiles (hourglass, spiral, ingot, crown, sigil) and the four shop icons (plate, spiral, chest, diamond). All are `TextureLab` drawings. No art was required.

## The curve check (R19 task 4)

`DevHarness.exe --store-curve [walks]` (`StoreCurve.cs`) runs the R16 reference walk and the fusing walk twice each: as a non-spender and as a player who buys the Starter Pact, the Growth Fund, the Ember Ledger, the Weekly and Monthly Packs and the pass premium as each period reopens (the walk's clock is the season calendar's, and the non-spender is the control on that same clock, so only the purchases differ). The spender buys through the harness's direct fulfilment, so every good is Sealed, and the Growth Fund pays at each milestone's own stage. The pass is walked as a diligent player's: the road is filled each season and every paid tier claimed. With `--store-contracts` a fifth column adds two Hero Contracts a month for the hero the spender concentrates on. Output: `D:\Emberfall\handoff\R19-STORE\data\store-curve-60walks.txt` (60 walks, the table below, and the stuck counts), `store-curve-20walks.txt` and `store-curve-20walks-with-contracts.txt` (20 walks, the contract column). Summary: `spender-curve.md` beside them.

Median entry power over `recommendedPower`. The first four columns are 60 walks, the contract column 20:

| Stage | Reference, non-spender | Reference, spender | Fusing, non-spender | Fusing, spender | Fusing, spender + contracts (20 walks) |
|---|---|---|---|---|---|
| 1-6 (wall) | x1.076 | x1.254 | x1.076 | x1.254 | x1.253 |
| 1-12 (wall) | x0.939 | x1.056 | x0.968 | x1.164 | x1.582 |
| 2-1 | x1.194 | x1.286 | x1.277 | x1.468 | x1.888 |
| 2-6 (wall) | x1.188 | x1.194 | x1.974 | x2.349 | x2.672 |
| 2-12 (wall) | x1.176 | x1.167 | x3.198 | x3.164 | x3.625 |
| 3-6 (wall) | x1.204 | x1.196 | x4.458 | x5.016 | x5.266 |
| 3-12 (wall) | x1.201 | x1.203 | x6.382 | x6.496 | x6.344 |
| 4-12 (wall) | x1.175 | x1.184 | x6.806 | x6.838 | x6.854 |
| 5-12 (wall) | x1.191 | x1.192 | x6.913 | x6.908 | x6.928 |
| 6-12 (wall) | x1.122 | x1.126 | x6.535 | x6.552 | x6.549 |

Stuck walks of 60: reference non-spender 1, reference spender 1, fusing non-spender 0, fusing spender 0. The reference walk that sticks is walk 51 (seed 51000154) at 3-4, with and without purchases (the spender neither adds a stuck walk nor cures it). It belongs to the CALENDAR clock the curve check runs on, which rerolls the armor drops: on the plain clock the guard uses (`CheckFusionNeverGates`, same seeds) the same 60 reference walks are 0 stuck. Total sittings (median) are 260 to 262 in every column.

What it says:

- **The walls still bind for the spender.** No spender walk is stuck that the non-spender's is not, the total sittings are the same, and no wall costs an extra sitting for any player in this instrument (a wall here is a power band, not a count of sittings). The spender's reference ratio is the non-spender's own from 2-6 on (x1.12 to x1.21). It is x1.254 at 1-6 and x1.286 at 2-1, 0.3 and 3 percent over the x1.25 the band allows a reference walk, so the spender is ahead only in chapters 1 to 3.
- **The advantage is early and it closes.** The fusing spender leads the fusing non-spender by 20 percent at 1-12 (the Starter Pact's 5-star and the first weeks of Gold), 19 percent at 2-6 and 12 percent at 3-6, then by 2 percent at 3-12 and under 1 percent from 4-12. From chapter 4 the plateau (x6.5 to x7.0) is the same for both, because the fusing player's power is capped by the ascension tiers and not by income.
- **The Hero Contract is the offer that moves the curve.** With two contracts a month on one hero the fusing spender reads x1.582 at 1-12 (against x0.968 for the non-spender and x1.164 for the spender without it), x1.888 at 2-1 and x3.625 at 2-12. It also closes by 3-12. This is why the contract sits behind its own define, OFF: the early walls (1-12, 2-x) would be crossed with a large margin by anyone who buys it.
- **No pack was scaled down.** No wall broke, so the packs are as sized in `store.json`. If the owner wants the early lead smaller, the levers are the Starter Pact's hero list (a 5-star is most of its early value), and the Growth Fund's and the pass's selector, not the hours.

## The Hero Contract with a cap (R24)

`HeroContractSwitch` is **ON by default**. Define `EMBERFALL_HERO_CONTRACT_OFF` to roll back to the build where the offer was not listed. The contract keeps its **2 a month per hero** (`StoreOfferDef.Limit`) and adds **4 a month for the whole account** (`StoreOfferDef.AccountLimit`, `accountLimit` in `store.json`). The account total is not stored: `StoreOffers.ContractsInPeriod` adds up the per-hero counters of the month the store's clock is in, so no save key is added and a counter from an earlier month counts for nothing. The refusal is `StoreRefusal.AccountCap`. The month and the forward-only clock are the offer's own (`SinkShop.MonthKeyOf`, `StoreOffers.EffectiveDay`), so a month, a year or the epoch back reopens nothing.

The contract opens at **4-1** (`requiresStage`, `StoreOfferDef.RequiresStage`): the first whale read put the early lead of a contract at +50 to +77 percent at 1-12 and 2-12, so it waits until chapter 4. It is listed and locked ("UNLOCKS AT 4-1") before that, like the relic packs. Value per chapter in days is in `offer-values.md`, section "Hero Contract with a cap (R24)". `CheckR24Store` proves the caps, the monthly reset, a clock wound back, the purchase path, the reload and the rollback.

## Relic packs (R24)

Three fixed offers in `store.json` (`group` `relicPacks`, switch `RelicPacksSwitch`, define `EMBERFALL_RELIC_PACKS_OFF` rolls back), all open with the Relic Shop (`requires` `Relics`, whose stage is the one in `onboarding.json`'s table, not repeated here) and listed locked before it:

- **Relic Dust Satchel**, EUR 4.99, 1 a week: 5,000 Relic Dust, the shop price of one Legendary relic.
- **Relic Dust Chest**, EUR 19.99, 2 a month: 25,000 Relic Dust, five times the Satchel. The first whale read cut it from 1 a week to 2 a month.
- **Relic of Choice**, EUR 29.99, 1 a month (`StoreOfferKind.RelicChoice`, `GrantKind.RelicChoice`): the buyer NAMES one relic from the list `RelicPacks.Picks` builds, the shop's own list in the shop's own order. A relic the buyer does not own is added at tier 1; an owned relic below the top tier is raised one tier. Both are what `GameSession.BuyRelic` and `UpgradeRelic` sell for Dust, so the card prints what each pick is worth at the shop and the offer carries `pickCapDust` 20,000: a pick worth more (a Mythic's tier 2 or 3) is not on the list. The choice is made before the purchase (`PurchaseLedger.Begin`), and a delivery with no pick is refused as "Pick a relic first." and left unconfirmed, exactly as a Starter Pact's is.

Why Sealed holds here without a mark: Dust and relics are not marked because no random mechanic can consume either. `CheckR24RelicPackRefusedByGambles` reads the sources and proves Dust is lowered in exactly one place (`SpendRelicDust`, called only by `BuyRelic` and `UpgradeRelic`) and that no code removes an owned relic, and that a summon after buying all three is refused with no Shard, Gold or Slag moved. The grants still go through `PurchaseGrants.Apply`. The sizing, in days of a committed player's Dust income and in weeks for a whale, is in `offer-values.md`, section "Relic packs (R24)"; at 6-12 a whale buying all three every period still needs 2.9 weeks to take every relic one tier up and 5.5 for the next.
## Live events: the weekly and the monthly event (R24)

A **weekly event** (seven days, Monday to Sunday by reward day) and a **monthly event** (28 days from the file's epoch, a Monday) run side by side. Each has a quest list, event points, a free reward track, a paid **Event Track** and one fixed **event pack**. The data is `live-events.json`: not hashed into `ContentVersion` (nothing in it is read by a fight), loaded after `Load` through `ContentDatabase.LoadLiveEvents`, and separate from `events.json` and its limited-time engine, which are untouched. The events of a slot take turns in file order, one per period, so adding an event is adding an entry (`LiveEventsConfig.EventFor`). The shipped examples are the Ember Trials (weekly, Ember) and the Tidecaller's Month (monthly, Tide). Nothing runs before the file's epoch.

- **Quests reuse the orders' counting.** `GameSession.OrderEvent` hands every order kind (waves, stage, level, summon, arena) to `LiveEvents.RecordOrder` at the call site that already counts it, whether or not the daily loop is on. The kinds orders do not have are counted at their own sites: `proving` (`GrantProvingResult`), `spendGold` (`LevelUpHero`), `ascend` (`Ascend`), `claimOrder` (`DailyLoop.ClaimOrder`) and `aspectWin` (`LiveEvents.RecordStageWin`: a stage won with at least `minHeroes` heroes of the event's aspect on the team). A capped repeat clear counts for nothing, as for orders. A claim pays the quest's event points; points open the tiers.
- **Two tracks on one ladder.** Each tier has a points threshold, an optional free reward and an optional paid reward, each a fixed number of hours of idle income, Dust sweeps or Slag (`EventRewardKind`, a closed set: no hero, no pull). Free rewards are plain earned goods (`LiveEvents.ClaimFree`). The paid track opens when the Event Track of this event is bought, and each tier is claimed through `PurchaseGrants.Apply` (`LiveEvents.ClaimPaid`), so it is **Sealed**.
- **What is sold.** Four store offers of kind `event` in `store.json`: the weekly track (EUR 4.99), the monthly track (EUR 9.99) and a pack of each kind (EUR 9.99). An offer has no contents of its own: the grant is `GrantKind.EventTrack` or `GrantKind.EventPack`, and `PurchaseGrants.Apply` reads the event that runs now. The pack is one bundle per event (`EventPackDef`: hours of idle Gold and Dust sweeps), once per event, and is applied by `Apply` as the lines it is, so it is Sealed too. Both are bought through the same purchase path as every offer (`PurchaseLedger`), shown in full with the price on the Events screen before the buy button (`LiveEventView.TrackLines`, `PackLines`). The Packs tab does not list them (`StoreOffers.View` skips the kind).
- **One clock, forward only.** A period is the reward day's week (`DailyLoop.WeekOf`) or 28 day block (`LiveEvents.PeriodOf`). `LiveEventSlotState.Period` only moves forward. A later period replaces everything (points, claims, the track and the pack are for sale again); an earlier one, a clock wound back, changes nothing: a claimed quest or tier stays claimed, a bought track or pack stays bought and is not for sale a second time, and no older event comes back. A clock jumped forward lands in the period it names and pays nothing for the periods it skipped. Reading never writes (`LiveEvents.View`, `LiveEvents.AnythingToClaim` roll nothing), so seeing the Hub moves no save.
- **A charged payment is always delivered, exactly what the card showed, never twice.** `StoreRules.CanBuy` remembers, in memory only (`LiveEventsState.Pending`, never saved), the event and period a purchase was begun for (`LiveEvents.Bind`). `PurchaseGrants.Apply` asks `LiveEvents.DeliveryOf`: **normal** when that event is still running and has not got the part, so the track is unlocked or the pack applied and marked bought; **stale** when the period turned over in flight, when the part is already the buyer's (two payments for one event, the same as every charged purchase: the limit is asked at `Begin`, not at `Complete`), or when the running event has no pack. A stale delivery pays the goods the card showed in full as Sealed lines (a pack: its hours and sweeps; a track: its paid rewards summed, because its quests belong to an event that is over), from the event it was begun for, or with no memory of it (a delivery after a restart) from the running event, else the first event of the slot that has them, and marks nothing, so the running event's own track and pack stay for sale. The receipt line says "paid out in full". The receipt is kept once (`PurchaseLedger`), so a second delivery of it grants nothing. `CheckLivePaymentInFlight` proves each case.
- **The screen.** `LiveEventsScreen` (`ScreenId.LiveEvents`): a Weekly and a Monthly tab, a summary strip with the points bar, the Event Track and the pack with their contents and prices, the free and the paid row, and the quest list. It decides and grants nothing. The Hub's Events door opens it and carries a dot while `LiveEvents.AnythingToClaim` is true; the limited-time events page is one tap away.
- **The flag.** `LiveEventsSwitch.Enabled`, **ON by default**; define `EMBERFALL_LIVE_EVENTS_OFF` to roll back. Off means no screen is built, the Hub's door opens the limited-time events page as before, every call here answers Off, nothing is counted or written, and the save is the document the build before wrote. State already in a save is kept. `CheckLiveOff` proves it: the same play with the switch off and with no `live-events.json` at all gives byte-identical saves.
- **The save.** Version 26 (`SaveSerializer.HoldsVersion26Data`), the `liveEvents` block (`LiveEventsState`: per slot the period, event, points, quest progress and claimed masks, and whether the track and the pack are bought), written only once it holds something. The harness file is `R24LiveEventChecks.cs` (`CheckLiveEvents`).
- **Not built.** A free skin on a track (waits on the skins), a quest for every game action, a currency shop, and any hero or pull as a reward.

## The purchase path

Two thin layers over Core, so the harness proves every rule without Unity (`CheckStore`).

- **Core.** `PurchaseGrants` (the one grant path), `PurchaseLedger` (the rules of a purchase), `PurchaseFlow` (wires a store to the ledger), `SimulatedStore` (the debug store), `StoreBackend` (the store interface), `StoreModes` (what a build does). **Game.** `PurchaseService` builds the flow and ticks it; `UnityIapBackend` is the Unity IAP adapter behind `EMBERFALL_UNITY_IAP` (set for the project by `Assets/csc.rsp`; `com.unity.purchasing` 5.4.4 is in the manifest and the lock).
- **Which store (`StoreModes.Resolve`, `CheckStoreModes`).** The editor runs Unity IAP's fake store (a dialog, no money) or, for scripted captures, the debug store (`PurchaseService.ForceDebugStore`). A development build runs the debug store, never the phone's real store. **A release build is COMING SOON**: no backend is built, every offer is shown in full with its price tier, and there is no buy button (`PurchaseFlow.CanBuy` is false). The live store needs a release build, the IAP package and the define `EMBERFALL_STORE_LIVE`, which nothing sets.
- **A purchase writes nothing until the store completes it.** `PurchaseLedger.Begin` remembers the pick in memory only. A purchase that is cancelled, fails, or is still pending after `PurchaseFlow.PendingTimeoutSeconds` (120) grants nothing and leaves the save byte for byte as it was (`CheckStoreFlow`).
- **Once per receipt.** `Complete` keeps the receipt id in `PlayerProfile.Store` (id, product, offer, time; no key, no signature, no account detail; at most 500) and a receipt seen again grants nothing, whoever delivers it. The store is told the purchase is finished (`Confirm`) only after it was granted or was granted before, so an ungranted purchase stays open in the store and is delivered again at the next start.
- **A charged purchase is granted.** `Complete` does not ask the limit again: the player paid. The limit is asked at `Begin`.
- **Restore.** Only `starter_pact` and `growth_fund` are restorable (`"restorable": true` in `store.json`; `CheckStoreShippedRestorable`). `Restore` on a new profile grants the purchase once, marks the receipt restored and counts the offer so it cannot be bought again. A profile that already holds any receipt for the offer owes nothing, whatever transaction id the store restores it under.
- **A paid purchase with no pick** (delivered after a restart, or after the timeout, for a Starter Pact) grants nothing and is not confirmed. `PurchaseFlow.PurchaseWaiting` lists it, the Store screen asks for the hero, and `FinishPurchase` grants it once and confirms. A restore that needs a pick waits the same way (`RestoreWaiting`, `FinishRestore`).
- **The save.** Version 25 (`SaveSerializer.HoldsVersion25Data`), stamped only when the `store` block holds a receipt or a counter, or a paid Wayfarer's Road track is bought; every other save keeps 22, 23 or 24. `save_limits_25.sql` (R19 handoff, never applied) raises the server's limit. R24 adds version 26 (`SaveSerializer.HoldsVersion26Data`, the same rule) for the data of the monetisation run; the relic packs and the contract cap add none of their own (an upgraded relic is already version 24 data).
- **Editor side effects of the package.** On a Windows editor `com.unity.purchasing` opens a modal dialog offering the Microsoft GDK packages (Xbox Store only; optional, the package falls back to the fake store without them). `IapGdkOptOut` answers it for the machine before it can open, so no Xbox dependency enters the manifest and an automated editor is not stalled. The package also writes `Assets/Resources/BillingMode.json` (the Android store, GooglePlay), which is committed so the tree stays stable.
- **What is not built.** No real product id, store key, receipt validation (a server check of the store's signed receipt) or account. Those come with the store accounts and are the owner's.

## The flag

`StoreSwitch.Enabled`, **ON by default**, like `QuartermasterSwitch`. Define `EMBERFALL_STORE_OFF` to roll back. Off means no screen is built, no door is drawn, `StoreOffers` answers Off and writes nothing, the idle claim is the claim it always was even with a card in the save, and the save is the document the build before wrote. State already in a save is kept and written back untouched. `CheckOfferOff` proves it.

## The save and the content

The save is the purchase path's (`PurchaseLedger`, `StoreState`, the version bump). The offer rules write only through `PlayerProfile.Store`: one `OfferRecord` per offer (its period, its count, a card's days, a fund's claimed milestones) and a `clock` record. `store.json` is **not hashed** into `ContentVersion`, for `guild-path.json`'s reason, and `CheckOfferNotHashed` proves a pack retune leaves it alone.

## Limits and what is not decided

- Every number is a first pass. A buyer of every period gets 19.5 to 29 days of income per 30 days (`offer-values.md`), which is a large share of an economy; whether the walls still bind for that player is the curve check's question.
- The prices are placeholders. Real prices, the Hero Contract, store accounts and a deterministic Slag sink are the owner's.
- The Pass tab claims the free track's non-selector tiers; the free selectors (tiers 15 and 30) are chosen on the road screen, which has the picker for them.
