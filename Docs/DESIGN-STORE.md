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
- **The Growth Fund** pays as the player reaches each stage. A milestone already passed when the fund is bought is claimable at once.
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

## The flag

`StoreSwitch.Enabled`, **ON by default**, like `QuartermasterSwitch`. Define `EMBERFALL_STORE_OFF` to roll back. Off means no screen is built, no door is drawn, `StoreOffers` answers Off and writes nothing, the idle claim is the claim it always was even with a card in the save, and the save is the document the build before wrote. State already in a save is kept and written back untouched. `CheckOfferOff` proves it.

## The save and the content

The save is the purchase path's (`PurchaseLedger`, `StoreState`, the version bump). The offer rules write only through `PlayerProfile.Store`: one `OfferRecord` per offer (its period, its count, a card's days, a fund's claimed milestones) and a `clock` record. `store.json` is **not hashed** into `ContentVersion`, for `guild-path.json`'s reason, and `CheckOfferNotHashed` proves a pack retune leaves it alone.

## Limits and what is not decided

- Every number is a first pass. A buyer of every period gets 19.5 to 29 days of income per 30 days (`offer-values.md`), which is a large share of an economy; whether the walls still bind for that player is the curve check's question.
- The prices are placeholders. Real prices, the Hero Contract, store accounts and a deterministic Slag sink are the owner's.
- The Pass tab claims the free track's non-selector tiers; the free selectors (tiers 15 and 30) are chosen on the road screen, which has the picker for them.
