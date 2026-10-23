# DESIGN-REWARDED-ADS: where rewarded ads fit, their caps, and what they do to the curve

Decision record, dated 2026-10-22. A proposal: nothing in the game shows an ad, and no ads package is in the project. Since R28 the rules below are code behind `AdsSwitch`, which is off: `AdRewards`, `ads.json`, the save's `ads` block (version 27, written only once an ad is granted), `RewardedAdPresenter` and a simulated ad backend (`Docs/ui-specs/R28-SCREENS.md`). The numbers below were measured with the harness's reference walks (60 walks a column). The LevelPlay integration is a plan at the end of this file, not code.

## The rules

Rewarded ads sit under the same firewall as the Store (`Docs/DESIGN-STORE.md`, `Docs/DESIGN-SEALED.md`), and two rules of their own:

1. **Never random.** An ad pays a fixed line, shown in full with its exact amount before the player taps: "Watch an ad: +2 hours of idle Gold = 41,200 Gold". No ad grants a summon, a Shard, a reroll, a Quartermaster refresh, a forge attempt, a conversion, a mystery box, a wheel or a chest whose contents vary. The reward is computed before the ad starts and does not depend on anything that happens while it plays.
2. **Ad rewards are Sealed.** They are granted through the one grant path (`PurchaseGrants.Apply`, or an `AdGrants` twin that calls the same `SealedGrants` marks), so ad Gold buys level-ups and nothing random. A watched ad is not a payment, and Belgian law does not require this mark. It is proposed because the owner's rule is "nothing monetised is ever random", and an ad is monetised. Whether ad goods are Sealed or plain earned goods is owner decision 1.
3. **Never in place of a purchase.** No ad unlocks, discounts, extends, refreshes or substitutes for a Store offer:
   - no ad on the Store screen, on an offer card, on the Events screen's paid track or pack, or on the pass's paid track;
   - no ad reopens a purchase limit, adds an Ember Ledger day, claims a Growth Fund milestone or opens an Event Track tier;
   - no ad pays a good the Store sells as its headline: no hero copy, no relic, no Relic Dust pack, no skin, no event points;
   - a player who buys is never asked to watch, and a player who watches is never told that paying would skip the ad. There is nothing to "remove ads" from, because no ad is forced.
4. **Opt-in only.** Rewarded ads, started by the player. No interstitials, no banners, no ad on launch, no ad between battles, no ad as the price of continuing.
5. **Nothing waits on an ad.** If no ad is available (offline, no fill, consent refused), the button is not drawn and nothing is lost. No progress depends on an ad loading.
6. **One clock, forward only.** The daily caps count reward days (04:00 UTC, `GameSession.DayStartUnix`), with the Store's forward-only stamp (`StoreOffers.EffectiveDay`), so a clock wound back reopens nothing. 0018's `clock_check` covers a clock wound forward once the client uses it.

## The placements

| Id | Where | Reward | Cap a reward day | Why it is safe |
|---|---|---|---|---|
| A1 | The idle claim (after the chest is collected) | That claim's Gold again, Sealed, **capped at 2 hours of the player's own idle Gold** | 1 | Gold only, never the Shards the same hours pay, so it feeds no summon. The cap keeps it under the cheapest card (see "Against the Store" below). |
| A2 | The Proving, when Brands are below the maximum | One Brand | 1 | A Brand is a fight, and its Valor is fixed by the result (`provingRewardValorBase`, the loss fraction). Valor buys only fixed Armory items. |
| A3 | A cleared stage, after the day's `repeatFullClearsPerDay` (3) full-pay repeat clears are used | One more full-pay repeat clear: what that clear pays in Gold, Sealed, plus its Dust | 1 | The same payout the free loop already pays three times a day. Dust is unmarked because its only sink sells fixed relics. |

**At most three ads a reward day.** Not proposed, and why:

| Not an ad | Reason |
|---|---|
| A free summon, an extra pull, Shards | Random. The Belgian rule. |
| A Quartermaster refresh, an armor reroll, a forge attempt, a conversion | Random. |
| A mystery reward, a wheel, a chest of varying contents | Random. |
| A hero copy, a relic, Relic Dust beyond A3's one sweep, a skin, event points | Store goods. Rule 3. |
| A pass tier, an Event Track tier, a Ledger day | Paid track adjacency. Rule 3. |
| Skip a wall, a stage, a timer on a paid offer | A wall is the economy's pacing (`DESIGN-BUILD-ORDER.md`, the campaign power curve). |
| A1 uncapped (the whole claim again), or twice a day | Measured below: it moves the chapter 1 and 2 curve past the band a spender is held to. |

## The measurement

`DevHarness --store-curve 60 --store-ads lean|light|full|heavy` (`StoreCurve.cs`) runs the reference walk (never converts, never ascends) and the fusing walk, each with and without the ad watcher, on the season calendar's clock the Store curve uses. The ad watcher takes A1 after every claim the cap allows and A3 after every walled sitting the cap allows, through `PurchaseGrants.Apply`, Sealed, with nothing random in the step. A2 is not in the walk, because the walk plays the campaign and not the Proving (see "A2 in the economy" below).

The four variants:

| Variant | A1 | A3 |
|---|---|---|
| **lean (proposed)** | 1 a day, capped at 2 hours of idle Gold | 1 a day |
| light | 1 a day, capped at 4 hours | 1 a day |
| full | 1 a day, the whole claim again (up to the 10 hour idle cap) | 1 a day |
| heavy | 2 a day, the whole claim each time | 2 a day |

The non-watcher columns reproduce the R19 Store curve's control to the third decimal (x1.076 at 1-6, x0.939 at 1-12, x1.194 at 2-1, 1 stuck reference walk), so the ad columns differ from the control only by the ads.

Median entry power over `recommendedPower`, 60 walks a column. "ref" is the reference walk, "fuse" the fusing walk:

| Stage | ref | ref + lean | ref + light | ref + full | ref + heavy | fuse | fuse + lean | fuse + full | fuse + heavy |
|---|---|---|---|---|---|---|---|---|---|
| 1-6 (wall) | x1.076 | x1.092 | x1.093 | x1.110 | x1.146 | x1.076 | x1.092 | x1.110 | x1.146 |
| 1-12 (wall) | x0.939 | x0.962 | x0.977 | x1.000 | x1.040 | x0.968 | x0.982 | x1.010 | x1.068 |
| 2-1 | x1.194 | x1.222 | x1.247 | x1.261 | x1.278 | x1.277 | x1.274 | x1.339 | x1.374 |
| 2-6 (wall) | x1.188 | x1.192 | x1.207 | x1.191 | x1.194 | x1.974 | x2.031 | x2.229 | x2.449 |
| 2-12 (wall) | x1.176 | x1.180 | x1.191 | x1.179 | x1.185 | x3.198 | x3.190 | x3.132 | x3.221 |
| 3-6 (wall) | x1.204 | x1.203 | x1.205 | x1.209 | x1.196 | x4.458 | x4.665 | x4.862 | x5.229 |
| 3-12 (wall) | x1.201 | x1.200 | x1.200 | x1.203 | x1.196 | x6.382 | x6.497 | x6.337 | x6.788 |
| 4-12 (wall) | x1.175 | x1.179 | x1.180 | x1.179 | x1.174 | x6.806 | x6.825 | x6.812 | x6.849 |
| 5-12 (wall) | x1.191 | x1.193 | x1.187 | x1.188 | x1.183 | x6.913 | x6.944 | x6.895 | x6.957 |
| 6-12 (wall) | x1.122 | x1.132 | x1.123 | x1.119 | x1.120 | x6.535 | x6.527 | x6.554 | x6.577 |

| | ref | ref + lean | ref + light | ref + full | ref + heavy |
|---|---|---|---|---|---|
| 1-12 extra sittings (median) | 1 | **1** | 0 | 0 | 0 |
| Stuck walks of 60 | 1 | 0 | 1 | 0 | 1 |
| Total sittings (median) | 262 | 261 | 261 | 260 | 260 |
| Ads watched a day (median) | | 2.0 | 2.0 | 2.0 | 3.9 |
| Ad Gold, share of all Gold (median) | | 10.8 percent | 16.9 percent | 30.8 percent | 46.5 percent |

Every other wall reads 0 extra sittings in every column, as it does for the non-watcher.

What it says:

- **Lean keeps every wall binding.** The one wall that costs the reference player a sitting (1-12, median 1) still costs it with lean ads. Light, full and heavy each remove it. Lean's largest lead is +2.8 points at 2-1 (x1.222 against x1.194), inside the x1.25 a reference walk is allowed, where the R19 spender reads x1.286. From 2-6 on, lean is within one percent of the control on the reference walk.
- **Full and heavy break the band.** Full puts 2-1 at x1.261 and heavy at x1.278, both over x1.25, and heavy gives the fusing player +24 percent at 2-6 and +17 percent at 3-6. Ads would then pay 31 to 47 percent of all Gold, more than idle income does for a player who does not watch.
- **The fusing player's plateau does not move** (x6.5 to x7.0 from chapter 4 in every column), for the reason the Store curve found: the plateau is capped by ascension tiers, not by income.
- **The one stuck reference walk** (walk 51, the calendar clock's armor reroll, `DESIGN-STORE.md`) is cured by lean and full and not by light or heavy. It is a draw, not a trend.

## Against the Store

"Never in place of a purchase" is also a size rule: watching every ad must be worth less than the cheapest recurring purchase, or the ad replaces it. In days of frontier income (a day is 20 hours of idle Gold, the unit `offer-values` uses):

| | Per 30 days |
|---|---|
| Lean A1: 2 hours a day | 3.0 days |
| Lean A3: one full repeat clear a day (a clear pays about one purse, and idle Gold is 1.00 purse an hour from 3-1) | about 1.5 days |
| **Lean ads, all of it** | **about 4.5 days** |
| Ember Ledger, EUR 4.99 (2 hours a claimed day plus 10 percent of every idle claim) | 6.0 days |
| The R19 spender, every recurring offer (EUR 34 a month) | 19.5 to 29 days |

Lean ads are worth three quarters of the cheapest card and a sixth to a quarter of what a light spender buys, so they sit below every purchase and stack with it. A1 at the full claim (15 days a month) would be worth more than the Ledger and the Monthly Pack together, which is the other reason full is not proposed.

## A2 in the economy

The walk does not play the Proving, so A2 is sized by hand. Brands hold 5 and refill one per 3 hours (`provingBrandsMax`, `provingBrandsRechargeHours`), so a player who checks in twice a day fights about 8 times a day. One more Brand is an eighth more of that day's Valor, and the Armory prices in days of Valor income (`SinkPricing`), so A2 is worth about 3.75 days of Valor a month. Valor buys only fixed items with their own caps (`Docs/DESIGN-SINKS.md`), and the Proving pays the same Valor for the same result whatever the Brand came from. The Proving opens at 2-4 (`onboarding.json`), so A2 does not exist in chapter 1.

## Abuse and the save

The save is client-authoritative (`Docs/SECURITY-REVIEW-SUPABASE.md`). A modified client can skip the ad and take the reward, as it can take anything. That cheats only the player: ad rewards are Sealed Gold, a Brand and one Dust sweep, and none of them reaches another player (guild boards fight normalised lines, `guild_dungeon_submit`'s level 40 and no relics or armor). LevelPlay's server-to-server callback (below) is the instrument if ad fraud ever matters, for example if ad revenue is shared or an ad reward feeds a leaderboard. It is not proposed for launch.

## The LevelPlay integration plan

No package is added by this record. When the owner decides to ship ads, the work is this, in order.

### Owner setup (outside the repo)

1. A Unity LevelPlay account, the Android app registered in it, an **app key**, and one **rewarded ad unit** per placement (A1, A2, A3), or one ad unit with three named placements. Configure the dashboard's per-placement capping to the same caps as the game's (1 a day each), as a second lock.
2. Payment and tax details in the LevelPlay dashboard, and an `app-ads.txt` on the developer website listed in the Play Console.
3. Start with LevelPlay's own bidding demand (Unity Ads). Add networks (AdMob, AppLovin and so on) later, each as its own adapter, each one a new Data safety line.
4. The Play Console: the Data safety form changes. The launch draft says so itself (an ads SDK "will very likely collect" the Advertising ID). Answer for Device or other IDs (Advertising ID) collected and shared with ad partners for advertising, and declare "Contains ads" on the store listing. The privacy policy gains an advertising section. If the game is ever listed for children, the Families policy applies and this plan does not.

### The code (owner: code), in the shape the Store already has

1. **The package.** `com.unity.services.levelplay` (Ads Mediation) through the Package Manager, the 9.x API (`LevelPlay.Init(appKey, userId)`, `LevelPlayRewardedAd`). Behind a scripting define, `EMBERFALL_LEVELPLAY`, set the way `Assets/csc.rsp` sets `EMBERFALL_UNITY_IAP`, so the build-check's shim compile stays independent of the SDK.
2. **Core, engine-free and proven by the harness:**
   - `ads.json` (not hashed into `ContentVersion`, loaded after `Load` like `store.json`): each placement's id, its reward line (`IdleGoldHours` with a cap, `Brand`, `RepeatClear`), its daily cap and its ad unit's placement name. No field that could hold a chance.
   - `AdRewards`: `View(placement)` (what the button would pay, computed now, read-only), `Check(placement, now)` (the cap and the reward day, forward-only clock), `Grant(placement, adImpressionId, now)` (once per impression id, through the one grant path, Sealed, all or nothing).
   - `AdsState` in the save: per placement the reward day and the count, and the last 50 impression ids so a reward delivered twice grants once. Written only once it holds something, with a save version bump and a `save_limits` raise on the server before a cloud player holds one (the Store's rule).
   - `AdsSwitch`, OFF by default, with `EMBERFALL_ADS_OFF` as the rollback once it is on. Off means no button, no SDK init, no key in the save.
3. **Game:**
   - `AdBackend`, an interface like `StoreBackend`: `SimulatedAdBackend` for the editor and development builds (it "plays" an ad and answers rewarded, no network), and `LevelPlayAdBackend` behind `EMBERFALL_LEVELPLAY`.
   - Init only after consent. Pass the consent result with `LevelPlay.SetConsent` before `Init`. Create the `LevelPlayRewardedAd` objects after `OnInitSuccess`, load one, and reload after every show.
   - Show only when `IsAdReady()` is true and the placement is not capped (`LevelPlayRewardedAd.IsPlacementCapped`) **and** `AdRewards.Check` allows it. The game's cap is the one that counts.
   - Grant on `OnAdRewarded` only. It may fire after `OnAdClosed`, so the grant must not depend on the close. Use `LevelPlayAdInfo`'s ad id as the impression id. A close without a reward grants nothing and says "No reward: the ad was not finished".
   - Pause the game while an ad plays (`SetPauseGame`).
   - Release builds show no ad button until a define the owner sets (`EMBERFALL_ADS_LIVE`), exactly as the Store's live mode works.
4. **UI** (owner: code, under the UI rules; this record touches no screen): one button per placement, drawn only when `AdRewards.View` says it can pay, showing the exact reward and "1 left today". Never on the Store, Events, pass or offer screens (rule 3), and a harness check proves it the way `CheckOfferWiring` proves the Store screen grants nothing.
5. **Harness checks** (a new `AdChecks.cs`, a partial of `Program`, added to `build-check.ps1`'s list and to the solver's list together, because `CheckSolverAndGateCompileOneList` asserts the two agree):
   - nothing random: `AdRewards.cs`, `ads.json` and the backend are read for the random, summon, roll, reroll, forge, conversion and pity words, as `CheckOfferNothingRandom` reads the Store's files;
   - Sealed: after each placement the Gold that arrived is exactly the Sealed part, and the free Gold and Shards did not move;
   - caps and the clock: one a day each, a day, a week, a year and 1970 back reopen nothing;
   - once per impression: the same ad id delivered twice grants once;
   - never in the Store: no Store, Events or pass screen names `AdRewards`;
   - off equals before: with the switch off the same play gives a byte-identical save;
   - the curve: `--store-ads lean` stays a measurement, not a gate, like `--store-curve`.
6. **Testing:** LevelPlay's Test Suite (`LevelPlay.LaunchTestSuite`) and integration validation on a test device, with test mode on and real ads off until release.

### Server-to-server callbacks (later, optional)

LevelPlay can call a URL after each completed ad, with an event id to deduplicate on, and a signature that is the MD5 of timestamp, event id, user id, rewards and a private key. If ad fraud ever matters, an Edge Function `levelplay-reward` can verify that signature, deduplicate the event id in a table, and record the reward for audit. Set the dynamic user id (`SetDynamicUserId`) to the account's hashed id before each show. Because the save is client-side, the callback can audit rewards, not grant them. It is not proposed for launch.

## What the owner must decide

1. **Whether ad rewards are Sealed.** Proposed: yes. If no, ad Gold could reach the Quartermaster's rolled armor, which is legal for an unpaid good but blurs the rule the game advertises.
2. **The variant.** Proposed: lean. Light is the most that keeps chapter 2's opener inside the band, but it removes the one wall sitting chapter 1's finale costs.
3. **Whether to ship ads at all at launch.** They add a consent dialog, an SDK, a Data safety change and an "ads" label on the listing. They can be added later without touching the economy, because lean sits under every purchase.
4. **A2.** Whether an extra Proving attempt is wanted, given PvP fairness: a watcher gets one more fight a day than a non-watcher, though never a better reward per fight.
