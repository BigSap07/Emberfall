# DESIGN-STORE: the Store, its offers and the rule they all obey

Decision record, dated 2026-10-19 (R19). Source: `IDEAS-ECONOMY-PACKS.md` sections 2 and 3. A Store on the Hub with the season pass, the packs and the currency shops in one place, and deterministic packs sold through a purchase path that runs against a fake store in the editor, a debug store in a development build and "coming soon" in a release build. **No real money moves in this build and nothing in it can take any.** The numbers live in `store.json`; read that file for the truth about any one offer.

## The rule

Belgium treats a paid random reward as a loot box. Summon and Conversion are random. So:

> **Everything a purchase grants is Sealed. Sealed never enters a summon, a Conversion, the forge gamble or any roll. Every pack's contents are fixed and shown in full, with the price, before it is bought.**

How the build enforces it, and what proves it:

- **One grant path.** Every good a purchase or a payout of something bought gives is made by `PurchaseGrants.Apply`, which marks it through `SealedGrants`. That covers the purchase itself (`PurchaseLedger.Complete`), an Ember Ledger day, a Growth Fund milestone, the ledger's share of an idle claim and a paid Wayfarer's Road tier. `CheckSealedNothingCallsTheGrant` proves no other file calls `SealedGrants`.
- **No random call in the path.** `CheckOfferNothingRandom` reads `StoreOffers.cs`, `StoreConfig.cs`, `PurchaseGrants.cs` and `SealedGrants.cs` and fails on a random, a summon, a roll, a conversion or a pity word. `StoreConfig` has no field that could hold a chance, and `store.json` is read for the words "chance", "roll", "pool", "pity", "weight" and "probability" too.
- **A closed set of grants.** `GrantKind` is a closed list (Gold, Slag, Dust, DustSweeps, IdleGoldHours, HeroCopy, HeroChoice, OwnedHeroCopy, PassPremium, Activate). None rolls. `StoreOfferKind` in `store.json` is closed too (pack, starter, ledger, growth, pass, contract); an unknown kind, tier or reset is dropped with a warning.
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
| Hero Contract | one copy of a 5-star the buyer already owns, for that hero's ascension | 2 per hero | month | EUR 19.99 |

- **Hours and sweeps scale with the buyer.** An hour is `GameSession.IdleYield` at the buyer's own frontier (Gold only, never the Shards the same hour pays). A Dust sweep is one repeat-clear payout of the frontier stage, as the pass counts it. So nothing needs retuning when the frontier moves.
- **Limits and resets** (`StoreOffers.Check`, `Record`): a week is Monday to Sunday by reward day (`DailyLoop.WeekOf`), a month is the calendar month of the reward day (`SinkShop.MonthKeyOf`), a season is Wayfarer's Road's. The reward day is 04:00 UTC, the clock the daily loop, the sinks and the pass use.
- **A clock wound back reopens nothing.** Every read and write uses the later of the real reward day and the store's own stamp (the `clock` offer record in `PlayerProfile.Store`, which only moves forward). `CheckOfferClockWoundBack`, `CheckOfferLedger` and `CheckOfferLimitsAndResets` prove a week, a month, a card and a claimed day stay closed across a wound-back clock, a year back and 1970.
- **The Ember Ledger** is a card: buy it, then claim each reward day's hours once. A missed day is not made up. The card's end is 30 reward days from the day it was bought. The 10 percent share is paid by `GameSession.ClaimIdle` through `StoreOffers.OnIdleClaim` as Sealed Gold, and only while the card runs.
- **The Growth Fund** pays as the player reaches each stage. A milestone already passed when the fund is bought is claimable at once. Each reward is FIXED at its milestone's own stage (`StoreOffers.MilestoneGold`, `MilestoneDust`), not at the frontier the claim is made from, so hoarding claims gains nothing: in all it pays 5,343,840 Gold and 11,200 Dust. **It is worth most to an early buyer**: about 400 days of chapter 1 income (417 with Dust) bought at 1-12, 96 at 2-12, 38 at 3-12, 19 at 4-12, 11 at 5-12 and 6.6 at 6-12 (`offer-values.md`). The owner may want the price tier or the amounts to reflect that.
- **The Hero Contract** sits behind its own define, `EMBERFALL_HERO_CONTRACT`, **OFF**. With it off the offer is not listed, cannot be bought, and `CheckOfferHeroContract` proves it writes nothing. The owner turns it on after the curve is re-measured with paid copies.
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

`DevHarness.exe --store-curve [walks]` (`StoreCurve.cs`) runs the R16 reference walk and the fusing walk twice each, 20 walks a column: as a non-spender and as a player who buys the Starter Pact, the Growth Fund, the Ember Ledger, the Weekly and Monthly Packs and the pass premium as each period reopens (the walk's clock is the season calendar's, and the non-spender is the control on that same clock, so only the purchases differ). The spender buys through the harness's direct fulfilment, so every good is Sealed. The pass is walked as a diligent player's: the road is filled each season and every paid tier claimed. With `--store-contracts` a fifth column adds two Hero Contracts a month for the hero the spender concentrates on. The full output is in `D:\Emberfall\handoff\R19-STORE\data\store-curve-20walks.txt` and `store-curve-20walks-with-contracts.txt`.

Median entry power over `recommendedPower`, 20 walks:

| Stage | Reference, non-spender | Reference, spender | Fusing, non-spender | Fusing, spender | Fusing, spender + contracts |
|---|---|---|---|---|---|
| 1-6 (wall) | x1.077 | x1.253 | x1.077 | x1.253 | x1.253 |
| 1-12 (wall) | x0.939 | x1.055 | x0.980 | x1.186 | x1.582 |
| 2-1 | x1.192 | x1.289 | x1.293 | x1.442 | x1.888 |
| 2-6 (wall) | x1.189 | x1.195 | x1.961 | x2.251 | x2.672 |
| 2-12 (wall) | x1.168 | x1.191 | x3.165 | x3.261 | x3.625 |
| 3-6 (wall) | x1.199 | x1.207 | x4.395 | x4.918 | x5.266 |
| 3-12 (wall) | x1.202 | x1.205 | x6.379 | x6.429 | x6.344 |
| 4-12 (wall) | x1.178 | x1.183 | x6.804 | x6.793 | x6.854 |
| 5-12 (wall) | x1.192 | x1.191 | x6.922 | x6.906 | x6.928 |
| 6-12 (wall) | x1.128 | x1.121 | x6.547 | x6.557 | x6.549 |

What it says:

- **The walls still bind for the spender.** No walk is stuck in any column, the total sittings are 260 to 262 in every column, and no wall costs an extra sitting for any player in this instrument (a wall here is a power band, not a count of sittings). The spender's reference ratio stays inside the reference band from 2-6 on (x1.12 to x1.21, the non-spender's own range). It is x1.253 at 1-6 and x1.289 at 2-1, 0.3 and 3 percent over the x1.25 the band allows a reference walk, and the spender is ahead by 6 to 21 percent only in chapters 1 to 3.
- **The advantage is early and it closes.** The fusing spender leads the fusing non-spender by 21 percent at 1-12 (the Starter Pact's 5-star and the first weeks of Gold), 12 percent at 2-6 and 12 percent at 3-6, then by under 2 percent from 3-12. From chapter 4 the plateau (x6.3 to x7.0) is the same for both, because the fusing player's power is capped by the ascension tiers and not by income.
- **The Hero Contract is the offer that moves the curve.** With two contracts a month on one hero the fusing spender reads x1.582 at 1-12 (against x0.980 for the non-spender and x1.186 for the spender without it), x1.888 at 2-1 and x3.625 at 2-12. It also closes by 3-12. This is why the contract sits behind its own define, OFF: the early walls (1-12, 2-x) would be crossed with a large margin by anyone who buys it.
- **No pack was scaled down.** No wall broke, so the packs are as sized in `store.json`. If the owner wants the early lead smaller, the levers are the Starter Pact's hero list (a 5-star is most of its early value), and the Growth Fund's and the pass's selector, not the hours.

## The purchase path

Two thin layers over Core, so the harness proves every rule without Unity (`CheckStore`).

- **Core.** `PurchaseGrants` (the one grant path), `PurchaseLedger` (the rules of a purchase), `PurchaseFlow` (wires a store to the ledger), `SimulatedStore` (the debug store), `StoreBackend` (the store interface), `StoreModes` (what a build does). **Game.** `PurchaseService` builds the flow and ticks it; `UnityIapBackend` is the Unity IAP adapter behind `EMBERFALL_UNITY_IAP` (set for the project by `Assets/csc.rsp`; `com.unity.purchasing` 5.4.4 is in the manifest and the lock).
- **Which store (`StoreModes.Resolve`, `CheckStoreModes`).** The editor runs Unity IAP's fake store (a dialog, no money) or, for scripted captures, the debug store (`PurchaseService.ForceDebugStore`). A development build runs the debug store, never the phone's real store. **A release build is COMING SOON**: no backend is built, every offer is shown in full with its price tier, and there is no buy button (`PurchaseFlow.CanBuy` is false). The live store needs a release build, the IAP package and the define `EMBERFALL_STORE_LIVE`, which nothing sets.
- **A purchase writes nothing until the store completes it.** `PurchaseLedger.Begin` remembers the pick in memory only. A purchase that is cancelled, fails, or is still pending after `PurchaseFlow.PendingTimeoutSeconds` (120) grants nothing and leaves the save byte for byte as it was (`CheckStoreFlow`).
- **Once per receipt.** `Complete` keeps the receipt id in `PlayerProfile.Store` (id, product, offer, time; no key, no signature, no account detail; at most 500) and a receipt seen again grants nothing, whoever delivers it. The store is told the purchase is finished (`Confirm`) only after it was granted or was granted before, so an ungranted purchase stays open in the store and is delivered again at the next start.
- **A charged purchase is granted.** `Complete` does not ask the limit again: the player paid. The limit is asked at `Begin`.
- **Restore.** Only `starter_pact` and `growth_fund` are restorable (`"restorable": true` in `store.json`; `CheckStoreShippedRestorable`). `Restore` on a new profile grants the purchase once, marks the receipt restored and counts the offer so it cannot be bought again. A profile that already holds any receipt for the offer owes nothing, whatever transaction id the store restores it under.
- **A paid purchase with no pick** (delivered after a restart, or after the timeout, for a Starter Pact) grants nothing and is not confirmed. `PurchaseFlow.PurchaseWaiting` lists it, the Store screen asks for the hero, and `FinishPurchase` grants it once and confirms. A restore that needs a pick waits the same way (`RestoreWaiting`, `FinishRestore`).
- **The save.** Version 25 (`SaveSerializer.HoldsVersion25Data`), stamped only when the `store` block holds a receipt or a counter, or a paid Wayfarer's Road track is bought; every other save keeps 22, 23 or 24. `save_limits_25.sql` (R19 handoff, never applied) raises the server's limit.
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
