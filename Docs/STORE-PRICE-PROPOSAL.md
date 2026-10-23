# Store price proposal: a euro price for every offer

Decision record, dated 2026-10-22. **Decided 2026-10-23: the owner took all five changes, and `store.json` carries them** (Starter Pact 2.99, Growth Fund 14.99, Weekly Event Track 2.99, Weekly Event Pack 4.99, Relic of Choice 19.99; two new tiers, `entry` 2.99 and `large` 14.99). The product ids are still placeholders and the Play Console products must be created at the same prices. Every number here was measured with the harness, and the market prices were read on 2026-10-22.

## How the prices were set

**The value of an offer** is `offer-values.md`'s measure: days of frontier income at the end of each chapter, where a day is 20 hours of idle Gold (the chest is collected twice a day), a Dust day is the capped repeat clears of every cleared stage, and a hero copy is the days of frontier Shards one 5-star pull costs at the base rate. `offer-values.md` lives in the owner's R19 and R24 handoff folders. The same table is printed by the harness on every run (`StoreOfferChecks.cs`, the OFFER VALUES block), and the numbers below are that block from this branch's run, so they match the handoff file for the current `store.json`.

**The price ladder** is euros per day of income, with three bands:

- **Best value, about EUR 0.80 a day or less:** the subscription-like card and the one-time growth offers. These are the offers a player should buy first, as the monthly card and the growth fund are in every comparable game.
- **Standard, EUR 1.20 to 1.90 a day:** every repeatable pack, track and event pack. The same day of income should cost about the same whichever repeatable offer it comes in.
- **Targeted, above the band:** goods the day measure undervalues because the buyer chooses them (a named hero copy, a named relic). See "Two offers the day measure undervalues".

The current placeholders broke the standard band in three places. The Weekly Event Pack cost EUR 3.33 to 3.83 a day, twice the Monthly Event Pack. The Weekly Event Track cost EUR 2.43 to 2.94 a day. The Relic of Choice cost more per Dust than the Satchel it is meant to be the premium of.

**EU prices.** The Play Console can hold one euro price for the whole EU with tax included ("Default price includes tax"), so EUR 4.99 is EUR 4.99 in Belgium and in Germany alike. Every price below is a tax-included euro price, and Google's fee and the VAT come out of it.

## The proposal

Value is in days of frontier income at the end of each chapter (`offer-values`). "EUR a day" is the proposed price over the value.

| Offer | Now | Proposed | 1-12 | 2-12 | 3-12 | 4-12 | 5-12 | 6-12 | EUR a day, proposed (3-12 / 6-12) |
|---|---|---|---|---|---|---|---|---|---|
| Starter Pact (once) | 4.99 | **2.99** | 6.18 | 3.00 | 1.90 | 1.38 | 1.10 | 0.92 | 0.48 at 1-12, where it is bought |
| Weekly Pack | 1.99 | 1.99 | 1.66 | 1.45 | 1.37 | 1.33 | 1.31 | 1.29 | 1.45 / 1.54 |
| Monthly Pack | 9.99 | 9.99 | 8.75 | 7.49 | 7.03 | 6.79 | 6.64 | 6.53 | 1.42 / 1.53 |
| Ember Ledger (30 days) | 4.99 | 4.99 | 6.00 | 6.00 | 6.00 | 6.00 | 6.00 | 6.00 | 0.83 / 0.83 |
| Growth Fund (once) | 9.99 | **14.99** | 417 | 95.6 | 37.8 | 18.8 | 10.6 | 6.60 | 0.16 at 2-12, 0.40 / 2.27 |
| Wayfarer's Road premium (season) | 9.99 | 9.99 | 6.63 | 3.45 | 2.35 | 1.83 | 1.55 | 1.37 | 4.25 / 7.29 (see below) |
| Weekly Event Track | 4.99 | **2.99** | 2.51 | 2.05 | 1.88 | 1.79 | 1.73 | 1.70 | 1.59 / 1.76 |
| Monthly Event Track | 9.99 | 9.99 | 9.66 | 7.99 | 7.37 | 7.05 | 6.85 | 6.71 | 1.36 / 1.49 |
| Weekly Event Pack | 9.99 | **4.99** | 3.50 | 3.00 | 2.81 | 2.71 | 2.65 | 2.61 | 1.78 / 1.91 |
| Monthly Event Pack | 9.99 | 9.99 | 7.55 | 6.29 | 5.83 | 5.59 | 5.44 | 5.33 | 1.71 / 1.87 |
| Hero Contract (from 4-1) | 19.99 | 19.99 | (5.88) | (2.70) | (1.60) | 1.08 | 0.80 | 0.62 | targeted, see below |
| Relic Dust Satchel, 5,000 Dust | 4.99 | 4.99 | | | 2.14* | 1.56* | 1.23* | 1.01* | EUR 1.00 per 1,000 Dust |
| Relic Dust Chest, 25,000 Dust | 19.99 | 19.99 | | | 10.7* | 7.80* | 6.15* | 5.07* | EUR 0.80 per 1,000 Dust |
| Relic of Choice, at most 20,000 Dust | 29.99 | **19.99** | | | 2.1 to 8.5* | 1.6 to 6.2* | 1.2 to 4.9* | 1.0 to 4.1* | at most EUR 1.00 per 1,000 Dust |
| Skins | 4.99 / 7.99 / 9.99 | unchanged | | | | | | | cosmetic, at most +5 percent of one stat on one hero |

\* Relic packs in days of a committed player's own Dust income (12 highest stages, 3 sweeps each), the `offer-values` section "Relic packs (R24)" and the harness's RELIC PACK SIZING block. They open with the Relic Shop at 3-1.

The five changes, and why:

1. **Starter Pact EUR 4.99 to 2.99.** It is the first purchase, and comparable games put theirs at about one dollar (below). Ours carries a named 5-star, which is worth more than their ten pulls, so it sits above that and below the EUR 4.99 card. The lead it buys does not depend on its price: the spender leads by +12 to +21 percent at 1-6 and 1-12 (the whale curve below), and `DESIGN-STORE.md` names the Starter Pact's 5-star as the main early lever, so a cheaper pact puts that lead in more hands. That is owner decision 2.
2. **Growth Fund EUR 9.99 to 14.99.** It is the best-value offer in the store by an order of magnitude early on (about 400 days of income bought at 1-12, 96 at 2-12), and `DESIGN-STORE.md` already notes the owner may want its price to reflect that. At EUR 14.99 it is still the best value for any buyer before 4-12. Comparable growth funds are USD 11 to 20.
3. **Weekly Event Track EUR 4.99 to 2.99.** It brings the track into the standard band (EUR 1.59 to 1.76 a day). The monthly track is already there.
4. **Weekly Event Pack EUR 9.99 to 4.99.** It was the worst value in the store, twice the Monthly Event Pack per day of income. At EUR 4.99 it is in the band.
5. **Relic of Choice EUR 29.99 to 19.99.** At EUR 29.99 a pick worth at most 20,000 Dust cost EUR 1.50 per 1,000 Dust, against EUR 1.00 for the Satchel and EUR 0.80 for the Chest. At EUR 19.99 the choice never costs more per Dust than the Satchel. The premium is the choice, not a worse rate.

Unchanged and why: the Weekly and Monthly Packs and the Monthly Event Track and Pack are already in the band. The Ember Ledger at EUR 4.99 is the best repeatable value (EUR 0.83 a day), which is the monthly card's job. The skins are cosmetic first.

## Two offers the day measure undervalues

The day measure values any hero copy as one 5-star pull: the Shards for a random 5-star at the base rate. Two offers sell a **targeted** copy, a hero the buyer chose, and for ascension only copies of that hero count. With 30 5-stars on the roster, a specific one costs on average up to 30 times a random one, so the honest value lies between the two:

| | 4-12 | 6-12 |
|---|---|---|
| Hero Contract (one copy of an owned 5-star), random-pull value | 1.08 days | 0.62 days |
| Hero Contract, as a targeted copy (x30) | up to 32 days | up to 19 days |
| EUR a day at EUR 19.99, targeted | from 0.62 | from 1.07 |

The pass premium's headline is a Sealed 5-star selector copy at tier 30, the same kind of good, plus 15 hours of Gold. Read as a random pull it is the weakest offer in the store (EUR 4.25 a day at 3-12). Read as a targeted copy it is one of the best. **Both are left at their current price** and flagged for the owner (decision 3). The contract is also the offer that moves the curve most (R24: +50 to +77 percent early, which is why it opens at 4-1). A lower price would put that lead in more hands, and a higher one would push the whale's spend further from the rest of the store.

## The market

Comparable idle RPGs, as published in spending guides and store pages (USD list prices; EU store prices are usually the same figure in euros, tax included):

| | AFK Arena | AFK Journey | Legend of Mushroom | Emberfall, proposed |
|---|---|---|---|---|
| First purchase | USD 0.99 pop-up, starter sets from 0.99 | USD 1 (10 Epic Invite Letters) | | Starter Pact EUR 2.99 |
| Monthly card | USD 4.99 and 14.99 | USD 5 (Classic Gazette) and 15 (Premium Gazette) | | Ember Ledger EUR 4.99 |
| Weekly or small subscription | USD 0.99 a week, 2.99 a month (privileges) | | | Weekly Pack EUR 1.99 |
| Growth fund | USD 19.99 (Advancement Rewards) | Growth Bundle, gated on stage 951 | USD 11 (level fund) | Growth Fund EUR 14.99 |
| Battle pass | USD 19.99 to 24.99 | Secret Travelogue (two tiers) | about USD 100 a month of passes in all (an analyst's estimate) | Wayfarer's Road EUR 9.99, event tracks EUR 2.99 and 9.99 |
| Bundle tiers | USD 14.99 to 99.99 | up to about USD 90 for a hero pack | | top EUR 19.99 |

Emberfall's ladder is at or below the market on every rung, and its top price (EUR 19.99) is a fifth of the market's top. That is deliberate: nothing here is a gamble, so there is no reason for a EUR 99.99 bundle of pulls to exist.

## The spender and whale curves, re-run

`R24WhaleCurve.cs` now prices every offer from the content's `store.json` (`WhalePrice`, `WhaleSkinPrice`). Before this change, the R24 offers' prices were literals in the study. So the re-run is the same 60 walks, once on the shipped prices and once on a scratch copy of the content with the five proposed prices. The power, wall and relic tables of the two runs are **identical**: a price changes what a buyer pays, not what they get. They also reproduce the R24 record (the reference whale leads by +5.6 percent at 4-12, every wall still binds, no walk stuck).

**Power, 60 walks, median lead over the non-spender** (identical under both price sets):

| Stage | Reference spender | Reference whale | Fusing spender | Fusing whale |
|---|---|---|---|---|
| 1-6 (wall) | +15.2% | +17.0% | +15.2% | +17.0% |
| 1-12 (wall) | +11.6% | +13.5% | +21.0% | +24.2% |
| 2-6 (wall) | -0.8% | +3.0% | +18.2% | +22.6% |
| 2-12 (wall) | +0.1% | +3.7% | -1.1% | +3.4% |
| 3-6 (wall) | +0.5% | +7.9% | +12.8% | +30.5% |
| 3-12 (wall) | +0.7% | +6.3% | -0.6% | +9.4% |
| 4-12 (wall) | 0.0% | +5.6% | +0.8% | +4.2% |
| 5-12 (wall) | -0.1% | +4.4% | +0.6% | +3.3% |
| 6-12 (wall) | +0.2% | +4.3% | +0.6% | +3.1% |

Walls: every wall still costs its ladder sittings for every buyer (2 at 1-6 and 1-12, 4 from 2-6 on, median). No walk is stuck in any column.

**Spend, mean over the walks, euros per 30 days of the recurring offers, and one-time purchases in total:**

| Buyer, window | Now | Proposed |
|---|---|---|
| Spender (the R19 set), recurring, chapters 2 to 6 | 32.9 | 32.9 |
| Spender, one-time (Starter Pact and Growth Fund) | 15.0 | 18.0 |
| Whale, recurring, chapters 2 to 6 (reference walk) | 270.9 | 235.6 |
| Whale, recurring, chapters 4 to 6 (reference walk) | 355.1 | 311.7 |
| Whale, recurring, chapters 4 to 6 (fusing walk) | 351.3 | 310.2 |
| Whale, one-time, sum of the six chapter windows (reference walk; Starter, Growth, skins) | 106.1 | 109.1 |

`DESIGN-BUILD-ORDER.md`'s R24 entry quotes about EUR 289 a month for the whale. This run reads EUR 270.9 over chapters 2 to 6 and EUR 355.1 over chapters 4 to 6 at the shipped prices, so EUR 289 is a mid-game window of the same study. The window matters more than the figure.

What the whale buys in chapters 4 to 6 under the proposal (reference walk, euros per 30 days): Hero Contract 129.6, Relic Dust Chest 48.6, Relic of Choice 24.3, Satchel 22.2, Weekly Event Pack 22.2, Weekly Event Track 13.3, Monthly Pack 12.1, Weekly Pack 8.9, pass 8.1, Monthly Event Track 8.1, Monthly Event Pack 8.1, Ember Ledger 6.0. The contract is 42 percent of a late whale's spend.

How to re-run it: copy `Assets/Resources/Content` to a scratch folder, edit its `store.json`, and run `DevHarness <folder> --whale-curve 60 --whale-col <player>:<buyer> --whale-out <file>` for each of `ref` and `fuse` and each of `non`, `spend` and `whale`, then `DevHarness <folder> --whale-report <dir>`.

## The Belgian rule

Unchanged by any price. Every offer is still fixed, shown in full with its price before it is bought, Sealed, and refused by every random mechanic. A price change is a number in `store.json`, which is not hashed into `ContentVersion`, so it retires no replay.

## What the owner must decide

1. **The five price changes**, or any of them. Applying one is a tier name in `store.json` (the two new tiers, EUR 2.99 and EUR 14.99, go in `tiers`) and the matching product price in the Play Console.
2. **The Starter Pact's price against its early lead.** A cheaper pact means more players carry the named 5-star's lead through chapter 1.
3. **The Hero Contract and the pass premium**, which the day measure cannot price. They are left unchanged and need the owner's judgement on what a targeted copy is worth.
4. **Whether a whale's late spend (about EUR 310 a month under the proposal, 42 percent of it contracts) is the intended ceiling.** The contract cap (4 a month) is the lever, not the price.
