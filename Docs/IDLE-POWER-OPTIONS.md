# Idle power: three ways to make waiting turn into power, measured

Measurement record of 2026-10-23. It follows the campaign walls study of the same date (`Docs/WALLS-OPTIONS.md`,
measured on its own branch and not yet on main). That study found that from chapter 2 a non-spender gains only
0.06 to 0.5 percent power a day while waiting, because the line sits at its star-gated level caps and idle Gold
has nothing to buy. This study designs and measures three ways to fix that. It changes no game file. Each
option's data half is a patch in `Docs/idle-power/`, and so is the study's tooling.

## The short answer

**Recommended: option 2, the soft cap.** A hero at its cap keeps levelling, one Level at a time, at twice the
level curve's price, paid in earned Gold only.

- **Waiting becomes power.** All three options lift the non-spender from 0.06 to 0.47 percent a day to about
  1.3 to 2.9 percent a day over chapters 2 to 6. Option 2 gives 2.80, 2.47, 1.79, 1.47 and 1.33 percent.
- **Only option 2's power fights like its number.** Options 1 and 3 add stats without Levels. The content is
  re-solved in enemy Levels, which is what the solver moves. Re-solved that way, options 1 and 3 jam even
  with today's walls: 59 and 54 of 60 non-spender walks get stuck in chapters 4 to 6. Option 2 re-solved the
  same way walks the campaign in 134.2 days with no walk stuck, the same pace as today.
- **Harder walls become fair under option 2, and only under it.** On content re-solved for option 2, the
  walls study's option B costs the non-spender almost nothing (median 0). Raised 6 more Levels per wall
  ("B+6"), the finales cost a median of 4 to 9 extra sittings (2 to 4.5 days), with a p90 of 7 to 15 and
  **no walk stuck after chapter 1**. Today the walls study's B leaves 39 of 60 stuck.
- **The whale lead stays inside the R24 limit if bought Gold is refused.** If the Gold sinks (options 1 and 2)
  accept Sealed Gold, the reference whale leads by +36 to +49 percent from 4-12 on (limit: under 6). If they
  take earned Gold only, as the Quartermaster does, the lead is -1.1 to +2.9 percent. Option 3's material is
  never sold, so its lead stays at today's +2.2 to +4.5 percent.
- **Nothing bought is ever random** in any option. Every purchase is a fixed price for a fixed, shown effect.

None of the three is data only. Each needs code, a save change and a full re-solve of `recommendedPower`,
because each makes the reference player about 8 to 10 times stronger at 6-12 than the content now expects.
So each option's patch is its **data half**, inert until the code in its written plan is built. The three
patches touch the same line of `onboarding.json`, and options 1 and 3 both add `training.json`: apply one at
a time.

## What was measured, and how

- **The instrument** is the walls study's: the R24 whale curve (`WhaleCurveColumn` in
  `Tools/DevHarness/R24WhaleCurve.cs`) on the R16 reference walk (`SimulateEconomyUnder`), at `WalkSeed(0)`
  to `WalkSeed(59)`: **60 walks per buyer**, on the season calendar's clock (`StoreWalkClockStart`).
- **The buyers** are the walls study's: non-spender (`ref:non`), spender (`ref:spend`, the R19 set) and whale
  (`ref:whale`, everything R24 added). Every buyer spends its Relic Dust by the study's relic policy
  (`WhaleRelicStep`). The reference player never converts and never ascends.
- **Daily orders and the free Road are ON for every buyer** (`--whale-loop`: `_walkLoopMode` 2, the diligent
  player of `Docs/DAILY-LOOP-CURVE.md`).
- **The three options are simulated in the walk**, after its own level spending in each sitting, by
  `Tools/DevHarness/R24IdlePowerStudy.cs` (in the tooling patch). The walk buys levels first and then spends
  what is left on the option, cheapest purchase first, every sitting. Each option opens with `1-12` cleared,
  where the caps start to bind.
- **Units** are the walls study's. A sitting every 12 hours, 2 sittings a day, up to 40 battles a sitting.
  **Extra sittings** at a wall are sittings beyond its own ladder floor (`WallExtraSittings`). **Stuck** means
  14 sittings in a row with no power gain and no roster change (`EconomyStallSessions`).
- **Power gained per day of waiting** in a chapter is the walls study's reading: the median over walks of
  entry power at the chapter's stage 12 over entry power at its stage 1, per day between them. With today's
  walls a chapter is 24.3 to 24.8 days of ladder whatever the buyer, so this is the growth a player gets
  from idle time. **Growth a day while held** is the same reading inside one wall, from its entry power to
  its exit power over the days spent there, over the walks that paid at least one extra sitting.
- **Lead** is the median entry power at a wall over the non-spender's, as `WhaleReport` prints it. The R24
  limit is under 6 percent at every wall from 4-12 on (`DESIGN-BUILD-ORDER.md`, Late-game spending).

### Re-solving the content, by proxy

Every option makes the reference player much stronger than the content expects: on today's content its entry
power at 6-12 is 7.7 to 9.8 times today's (median over 60 walks). Today's walls then cost nothing, and B's
walls would be measured against a player they were never set for. A fair test of harder walls needs content
re-solved to the new curve. Running `Tools/solve-recommended-power.ps1` per option was out of scope, so the
study uses a proxy:

1. Walk the option on today's content (the "today's content" runs).
2. For each stage, take m = the option's median non-spender entry power over today's (1.00 at 2-1, 1.37 to
   1.68 at 2-12, then rising to 9.84, 8.27 and 7.74 at 6-12 for options 1, 2 and 3).
3. Walk again with every enemy of that stage made m times stronger. Two ways were measured:
   - **in Levels** (the solver's lever, `SolveStageLevelsAt`): each enemy stands at the Level whose HP,
     Attack and Defense are m times its authored Level's (`StatMath.StudyEnemyLevel`);
   - **in stats**: HP, Attack and Defense times m at the authored Level.

With the median player arriving where it arrives today, a wall is then as hard for it as it is today, and
the only new thing is how much waiting at the wall pays. The Levels proxy is the one that matches what a real
re-solve would do. The stats proxy is the generous reading for options 1 and 3, whose own power is stats.

### What "fair" means here

A harder wall is **fair** when (1) no reference walk is stuck (the design rule `CheckFusionNeverGates`
enforces), (2) the non-spender's median is 2 to 4 extra sittings at the finales with a p90 within about twice
the median, and (3) the whale's lead in days to finish the campaign stays small. The walls study's B fails all
three today: 39 of 60 stuck, p90 6 to 34, and a 27-day lead.

## Today (no option), for reference

From the walls study, reproduced exactly by this study's tooling on main as it stands (60 walks, loop and Road
on). Growth per day of entry power:

| Today | Ch 1 | Ch 2 | Ch 3 | Ch 4 | Ch 5 | Ch 6 |
|---|---|---|---|---|---|---|
| Non-spender | 11.08% | 0.47% | 0.27% | 0.08% | 0.08% | 0.06% |
| Spender | 12.29% | 0.28% | 0.30% | 0.06% | 0.08% | 0.06% |
| Whale | 12.49% | 0.35% | 0.34% | 0.09% | 0.05% | 0.03% |

A chapter takes 24.7 days, the campaign 134.4, the whale finishes 0.8 days earlier, and its lead from 4-12 on is
+2.8 to +4.7 percent. The non-spender holds 53 million unspendable Gold at 6-12. Under the walls study's B,
growth while held at a wall from 2-6 on is 0.00 to 0.17 percent a day: waiting at a B wall buys nothing, which is why 39 of
60 non-spenders stop there.

## Option 1: Training (a Gold sink past the cap)

**The design.** A Training yard, one track per guild tech family (`line`, `blade`, `grace`, `reach`, the four
of `guild-tech.json`, by role). Rank r of a family costs 20,000 x 1.10^r Gold. A family at rank r carries
(1.05)^r - 1 of its heroes' power, split equally by power over the family's rung stats with the guild tech
tree's own `GuildTech.ApplyShare`, applied after guild tech. Ranks are per account, so they survive line
changes. It reuses the guild tech families and the share code; the new parts are the price and the state.

**Data** (`Docs/idle-power/idle-option-1-training.patch`): a new `training.json` (Gold, refuses Sealed,
20,000, 1.10, 0.05, the four families) and a `training` row at `ch1_12` in `onboarding.json`.

**Code** (written plan):

1. `TrainingConfig` in `Assets/Scripts/Core/Data` (parse, warn and drop a bad file, as `RelicShopConfig.FromJson`
   does), loaded by a new `ContentDatabase.LoadTraining` after `Load`, as `LoadGuildTech` is, in the game, the
   harness and the solver. Not in `ContentVersion`, for `guild-tech.json`'s reason.
2. `Feature.Training` in `OnboardingConfig.cs`; `CheckFeatureUnlockGating` lists the new entry point.
3. `PlayerProfile.Training` (rank per family id). `SaveSerializer` writes a `training` block only when a rank is
   above 0, at `CurrentVersion` 28 with a `HoldsVersion28Data` in the style of `HoldsVersion25Data`, plus a
   server migration raising `save_limits` to 28.
4. The stat stage: `StatMath.ForHero(def, level, relic, armor, config, tech, guild)` takes the ranks and calls
   `GuildTech.ApplyShare` after `ApplyRungs`. `PlayerProfile.EffectiveTeamPower` and `CombatSimulation`'s setup
   already share that overload. `GuildDungeon.FightUnder` keeps its empty profile, so the dungeon strips
   training as it strips guild tech; `CheckGuildDungeonStripsGuildTech` gains a training case.
5. `GameSession.TrainingRankCost`, `CanBuyTrainingRank` and `BuyTrainingRank(familyId, nowUnix)`: check first,
   then spend **earned Gold only** through a new `PlayerProfile.SpendFreeGold` (takes nothing Sealed, refuses
   when `FreeGold` is short), then raise the rank, and count the Gold for orders and live events as
   `LevelUpHero` does.
6. A screen of four rows (family, rank, share now and next, price, buy), with a door from the hero page when a
   hero is at its cap. A layout change, so a `PlayModeScreenshot` capture.
7. Checks: nothing random in the new files (the `CheckSinksNothingRandom` scan), a Sealed purse buys nothing
   and leaves the save byte for byte unchanged (as `CheckSealed` does), and the block's round trip.
8. The re-solve (below, under "What every option needs").

**Results on today's content** (60 walks per buyer; non-spender from the run that accepts Sealed Gold, which is
identical for a non-spender; spender and whale from the earned-Gold-only run):

| Option 1 | Ch 1 | Ch 2 | Ch 3 | Ch 4 | Ch 5 | Ch 6 |
|---|---|---|---|---|---|---|
| Non-spender | 11.08% | 2.43% | 2.93% | 2.09% | 1.67% | 1.41% |
| Spender | 12.29% | 2.49% | 2.67% | 2.02% | 1.67% | 1.52% |
| Whale | 12.49% | 2.58% | 2.67% | 1.99% | 1.61% | 1.47% |

Days per chapter do not move: 24.3 to 24.6 for every buyer, the campaign 132.7 days (non-spender), 132.4
(spender) and 132.3 (whale), because the ladder sets the pace on today's walls. No walk is stuck. Gold held at
6-12 falls from 53 million to 0.64 million.

Whale lead from 4-12 on: **+40.4 to +48.6 percent if Sealed Gold is accepted** (spender +15.5 to +19.8), and
**+0.9 to +2.5 percent if it is refused** (spender -2.5 to -1.5). Refusing it leaves the bought Gold unspent:
the spender holds 27 million and the whale 57 million at 6-12.

**Re-solved content:**

| Option 1, re-solved | Walls | Stuck (non / spender / whale) | Campaign finished, non-spender / whale (days) |
|---|---|---|---|
| in Levels | today's | 59 / not walked / not walked | 1 walk finishes, day 468.9 |
| in Levels | B | 60 / 60 / 60 | none finishes |
| in stats | today's | 0 / not walked / not walked | 236.7 / not walked |
| in stats | B | 1 / 1 / 0 (1-12 only) | 257.3 / 212.0 |

Re-solved in Levels, the yard's power does not fight like its number: 59 of 60 non-spenders stick at today's
walls, most at 5-1 (36), an ordinary stage. In stats, nobody sticks, but the campaign takes 236.7 days on today's
walls, and B's 4-6 costs a median of 41 extra sittings (20 days) with growth while held of only 0.95 percent a
day. The whale finishes B 45 days ahead. This is the ascension lesson again (`StatMath.LevelAt`): stats added
without Levels made a line of the same power clear 2-12's ceiling 39 percent of the time against 63.

## Option 2: the soft cap (levels past the cap at rising Gold cost)

**The design.** The cap stays where the fusion derivation puts it, but it stops being a wall: a hero at its cap
keeps levelling one Level at a time, at `overcapPremium` (2.0) times the price the level curve gives for that
Level (`levelCostA + levelCostB x Level`). The price rises with every Level because the curve does. The Level
stays the one number the stat line and the mitigation roll are built from. It reuses the level-up, the level
curve and Gold; the new parts are one number and the rule that reads it.

**Data** (`Docs/idle-power/idle-option-2-softcap.patch`): `balance.json` `overcapPremium` 2.0 and an `overcap`
row at `ch1_12` in `onboarding.json`. `balance.json` is hashed, so the key retires recorded guild dungeon
replays even before code reads it.

**Code** (written plan):

1. `BalanceConfig.OvercapPremium` (default 0: the hard cap, as now).
2. `GameSession.LevelUpHero` and `CanLevelUp`: at or past `HeroLevelCapOf`, with the premium above 0 and the
   feature open, a level costs `StatMath.LevelUpCost(cfg, Level, 0) x OvercapPremium` and adds one Level,
   spending **earned Gold only** (`SpendFreeGold`, as in option 1). Below the cap nothing changes.
3. `GameSession.LevelUpCost`, `NextLevelOf` and `TierLevelOf` read past the cap. `StatMath.TierLevelOf` stops at
   the cap today, which would price every past-cap Level at the cap's.
4. `SaveSerializer`'s version 18 clamp sets every hero to at most its cap on every load. With the soft cap on it
   must keep past-cap Levels, or every load erases what was bought; `CheckLevelClampOnLoad` changes with it.
   No new save key: the Level is the record.
5. Ascension: `GameSession.AscensionLanding` already keeps the higher of the hero's Level and the next tier's
   landing, so nothing is lost. But the hero then stands above its new tier's ladder. The rule needs a decision
   (suggested: past-cap Levels count toward the next tier at its stride). The reference player never ascends,
   so this study does not measure it.
6. The hero page and `LevelCapText` say "LEVEL 14, 5 past the cap" and the price. A capture.
7. `CheckFusionEconomyDerivation` derives the caps from "levels are at most 35 percent of the campaign's growth"
   (F2). Past-cap Levels break that premise on purpose; the derivation must take the soft cap as an input (the
   caps then mark where the premium starts) or it fails every build.
8. The harness's line policy ranks heroes by `CeilingPower` (power at the cap). The tooling reads
   `Math.Max(Level, cap)` there, which is a no-op without the soft cap. A player who swaps a hero out keeps its
   Levels on the bench, but the new hero starts the climb again: past-cap Levels are per hero.
9. The re-solve (below).

**Results on today's content** (non-spender identical in both runs; spender and whale earned-Gold only):

| Option 2 | Ch 1 | Ch 2 | Ch 3 | Ch 4 | Ch 5 | Ch 6 |
|---|---|---|---|---|---|---|
| Non-spender | 11.08% | 2.80% | 2.47% | 1.79% | 1.47% | 1.33% |
| Spender | 12.29% | 2.69% | 2.41% | 1.74% | 1.44% | 1.31% |
| Whale | 12.49% | 2.67% | 2.43% | 1.75% | 1.40% | 1.28% |

Days per chapter 24.3 to 24.6; campaign 132.6 (non-spender), 132.2 (spender), 132.2 (whale). No walk is
stuck. Gold held at 6-12 falls from 53 million to 0.17 million.

Whale lead from 4-12 on: **+36.1 to +42.2 percent if Sealed Gold is accepted** (spender +15.5 to +20.2), and
**-1.1 to +1.6 percent if it is refused** (spender -1.9 to -0.5).

**Re-solved in Levels, today's walls** (earned Gold only): no walk stuck for any buyer; 24.7 days a chapter;
the campaign 134.2 days (non-spender), 133.8 (spender), 133.7 (whale), against 134.4 today. Whale lead from 4-12
on +2.0 to +2.9 percent (spender +0.2 to +1.7). Re-solved in Levels, option 2 is today's campaign with
growth while waiting added.

**Re-solved in Levels, harder walls:** see "Harder walls" below. B, B+3 and B+6 stick no walk after chapter 1.

## Option 3: Essence (idle income of an upgrade material)

**The design.** A material that only the idle claim pays: Essence, at the frontier's idle Gold per hour over 100,
on the same 10-hour cap. No offer, pack, pass, chest, order, ad or event grants it, so money cannot reach it.
It buys the same family ranks as option 1, at 200 x 1.10^r Essence. It reuses the idle claim
(`GameSession.IdleYield`) and option 1's yard.

**Why not an existing material.** Relic Dust is the existing upgrade material, and relic tiers past 3 are data
only (`relic-shop.json`'s `upgrades.tiers`; `RelicShopConfig` accepts any number of tiers). That route was set
aside on arithmetic. A tier must cost more than the one before (tier 3 of a Legendary is 20,000 Dust), and one
more tier on one relic adds about a quarter of a 0.10 to 0.18 multiply to one stat of one hero, about 0.3
percent of the line. Two percent a day would take well over 100,000 Dust a day, against the 1,548 to 4,932 a
day a committed player earns. That would make every shelf relic a day's income. The Add stats (crit, effect
hit, resist) would also need a cap past tier 3 in `RelicDef.AtScale`.

**Data** (`Docs/idle-power/idle-option-3-essence.patch`): `balance.json` `essencePerIdleGold` 0.01;
`training.json` as option 1 with `currency` Essence and `firstRankCost` 200; the `training` unlock row.

**Code** (written plan): everything in option 1, with Essence in place of earned Gold, plus:

1. `PlayerProfile.Essence` (long), in the same version 28 block.
2. `IdleReport.Essence`: `GameSession.IdleYield` pays `floor(IdleGoldPerSecond x essencePerIdleGold x seconds)`
   once Training is open, so it reads the same frontier stage and the same cap. `ClaimIdle` adds it.
3. A check that no grant path names Essence (`StoreOffers`, `PurchaseGrants`, `SealedGrants`, `WayfarersRoad`,
   `DailyLoop`, `AdRewards`, `WallChests`, live events), in the shape of `CheckStoreNoRandomPath`.
4. The Hub's idle chest shows the Essence line.
5. The re-solve (below).

**Results on today's content:**

| Option 3 | Ch 1 | Ch 2 | Ch 3 | Ch 4 | Ch 5 | Ch 6 |
|---|---|---|---|---|---|---|
| Non-spender | 11.08% | 1.89% | 2.56% | 2.06% | 1.74% | 1.39% |
| Spender | 12.29% | 1.76% | 2.54% | 2.03% | 1.74% | 1.38% |
| Whale | 12.49% | 1.83% | 2.56% | 2.03% | 1.70% | 1.36% |

Days per chapter 24.4 to 24.7; campaign 132.8 (non-spender), 132.5 (spender), 132.4 (whale). No walk is stuck.
Whale lead from 4-12 on +2.2 to +4.5 percent, spender -0.1 to +0.9: today's lead, because nothing bought
reaches Essence. Gold is untouched, so the 53 million at 6-12 still has nothing to buy.

**Re-solved content:**

| Option 3, re-solved | Walls | Stuck (non / spender / whale) | Campaign finished, non-spender / whale (days) |
|---|---|---|---|
| in Levels | today's | 54 / not walked / not walked | 6 walks finish, day 396.3 |
| in Levels | B | 54 / 58 / 48 | 6 and 12 walks finish, days 420.3 and 401.4 |
| in stats | today's | 0 / not walked / not walked | 198.7 / not walked |
| in stats | B | 1 / 1 / 0 (1-12 only) | 215.1 / 184.1 |

The same failure as option 1, a little milder because the material arrives more slowly: re-solved in Levels the
line jams in chapters 5 and 6, and in stats the campaign stretches to 199 days and the whale finishes B 31 days
ahead.

## Harder walls: does B become fair?

Today's B on today's content stops 39 of 60 non-spenders. Add any option without re-solving and B stops none
after chapter 1 and costs nothing: from 2-6 on every wall's median is 0 and its worst is at most 8 extra
sittings, and the campaign takes 133.9 to 134.1 days (non-spender, 60 walks per option). That only says the
player outgrew the walls. The table reads each option on content re-solved to its own curve. B+3 and B+6 raise every wall from 2-6 on 3 and 6 more Levels, floor, ceiling and every enemy
(1-12 stays as in B: it comes before any option opens).

| Walls on re-solved content | Stuck after chapter 1 (non / spender / whale) | Finale medians, extra sittings (2-12, 3-12, 4-12, 5-12, 6-12) | Worst finale p90 | Non-spender finishes | Whale finishes |
|---|---|---|---|---|---|
| Today, B, no option | 38 / 30 / 5 | 7, 3, 3, 2, 6 | 26 | day 166.2 (21 walks) | day 139.4 (55 walks) |
| Option 1, B, in Levels | 59 / 59 / 60 | 3, 3, 4.5, none pass, none pass | 11 | none | none |
| Option 1, B, in stats | 0 / 0 / 0 | 2, 4, 2, 4, 3 | 9 | day 257.3 | day 212.0 |
| Option 3, B, in Levels | 53 / 57 / 48 | see text | see text | 6 walks | 12 walks |
| Option 3, B, in stats | 0 / 0 / 0 | 2, 3, 2, 4, 3 | 10 | day 215.1 | day 184.1 |
| **Option 2, B, in Levels** | **0 / 0 / 0** | 0, 0, 0, 0, 0 | 1 | day 136.4 | day 134.7 |
| **Option 2, B+3, in Levels** | **0 / 0 / 0** | 3, 2, 1, 1, 2 | 7 | day 143.4 | day 140.5 |
| **Option 2, B+6, in Levels** | **0 / 0 / 0** | 9, 7, 6, 4, 9 | 15 | day 158.4 | day 153.9 |

(One walk of 60 sticks at B's 1-12 in every B row for the non-spender and the spender; it is the same walk,
and 1-12 comes before any option opens.)

What the rows say:

- **Waiting pays at the wall under every option.** Growth while held at a wall is 1.0 to 2.6 percent a day
  under option 2 (on B+6: 2.41 at 2-6 falling to 1.02 at 6-12), and 0.2 to 2.5 under options 1 and 3 in
  stats (falling below 1 percent from 4-6 on), against 0.00 to 0.17 today. That is what turns a roster
  lottery into a wait.
- **Options 1 and 3 still fail, for the other reason.** In stats nobody sticks, but B's 4-6 costs 34 (option 3)
  to 41 (option 1) extra sittings, chapters 4 and 5 take 52 to 73 days, and the whale finishes 31 to 45 days
  ahead. In Levels the line jams. Their power is not the power the fight reads.
- **Option 2 makes B fair, and B is then too easy.** Re-solved for option 2, B costs a median of 0 at every
  wall from 2-6 on and a p90 of at most 1. B+6 is the first version that costs time: the finales a median of 4 to 9 extra
  sittings (2 to 4.5 days), p90 7 to 15, worst 26 (6-12), no walk stuck, and the campaign 158.4 days for the
  non-spender. B+3 sits between, with finales at 1 to 3. The minibosses (stage 6) stay at a median of 0 or 1
  in both, so a shipped version would set each wall's d on its own. 2-12 at 9 extra sittings in B+6 is over
  the 2 to 4 target and would come down.
- **Under option 2 the whale pays the walls too.** Its lead to the end of the campaign is 1.7 days in B, 2.9
  in B+3 and 4.5 in B+6, and every walk of every buyer that passes 1-12 finishes. For today's B it is 27 days
  and 34 more finishing walks. Its power lead from 4-12 on is +1.3 to +3.3 percent in B, +1.0 to +2.7 in B+3 and +1.1 to
  +3.8 in B+6, inside the R24 limit. At the B+6 finales the whale pays a median of 4 to 8 extra sittings
  where the non-spender pays 4 to 9.

Option 2, B+6, content re-solved in Levels, earned Gold only (60 walks per buyer):

| Wall | Non-spender extra sittings: median / p90 / worst | Spender | Whale | Non-spender growth a day while held | Whale lead | Spender lead |
|---|---|---|---|---|---|---|
| 1-12 | 3 / 8 / 22 (+1 stuck) | 1 / 3 / 7 (+1 stuck) | 1 / 1 / 12 | 2.67% | +11.6% | +9.8% |
| 2-6 | 4 / 9 / 12 | 3 / 8 / 10 | 2 / 6 / 9 | 2.41% | +6.9% | +5.1% |
| 2-12 | 9 / 14 / 16 | 9 / 12 / 17 | 8 / 11 / 16 | 2.02% | +4.3% | +2.9% |
| 3-6 | 0 / 0 / 3 | 0 / 1 / 2 | 0 / 1 / 3 | 1.68% | +3.1% | +1.1% |
| 3-12 | 7 / 11 / 15 | 5 / 9 / 15 | 4 / 7 / 9 | 1.62% | +4.4% | +2.6% |
| 4-6 | 0 / 0 / 2 | 0 / 1 / 2 | 0 / 1 / 2 | 1.41% | +3.1% | +1.3% |
| 4-12 | 6 / 11 / 16 | 5 / 9 / 16 | 5 / 9 / 14 | 1.39% | +2.5% | +1.5% |
| 5-6 | 0 / 1 / 8 | 0 / 2 / 5 | 0 / 1 / 6 | 1.30% | +1.1% | +0.1% |
| 5-12 | 4 / 7 / 11 | 5 / 8 / 11 | 6 / 11 / 15 | 1.16% | +2.4% | +1.3% |
| 6-6 | 1 / 4 / 7 | 1 / 4 / 9 | 2 / 5 / 8 | 1.05% | +2.6% | +1.2% |
| 6-12 | 9 / 15 / 26 | 8 / 13 / 18 | 8 / 12 / 20 | 1.02% | +3.8% | +2.4% |

| Chapter | Non-spender: finished on day / days in chapter | Spender | Whale |
|---|---|---|---|
| 1 | 11.8 / 11.8 (59 walks) | 10.8 / 10.8 (59 walks) | 10.8 / 10.8 |
| 2 | 44.2 / 31.4 (59 walks) | 42.1 / 30.9 (59 walks) | 41.1 / 30.3 |
| 3 | 72.0 / 28.3 (59 walks) | 69.7 / 27.8 (59 walks) | 67.9 / 26.8 |
| 4 | 100.0 / 27.8 (59 walks) | 97.4 / 27.8 (59 walks) | 95.7 / 27.3 |
| 5 | 128.2 / 26.8 (59 walks) | 125.3 / 27.3 (59 walks) | 123.7 / 27.8 |
| 6 | 158.4 / 29.9 (59 walks) | 154.9 / 29.4 (59 walks) | 153.9 / 29.9 |

## Comparison

| | Option 1, Training (Gold) | Option 2, soft cap (Gold) | Option 3, Essence |
|---|---|---|---|
| Growth a day while waiting, non-spender, ch 2 to 6 | 2.43, 2.93, 2.09, 1.67, 1.41% | 2.80, 2.47, 1.79, 1.47, 1.33% | 1.89, 2.56, 2.06, 1.74, 1.39% |
| Days per chapter, today's walls | 24.3 to 24.6 | 24.3 to 24.6 | 24.4 to 24.7 |
| Whale lead from 4-12, bought currency accepted | +40.4 to +48.6% | +36.1 to +42.2% | (none can be bought) |
| Whale lead from 4-12, as proposed | +0.9 to +2.5% | -1.1 to +1.6% (re-solved: +2.0 to +2.9%) | +2.2 to +4.5% |
| Re-solved in Levels, today's walls | 59 of 60 stuck | 0 stuck, 134.2 days | 54 of 60 stuck |
| Harder walls fair? | no | yes (B+3 to B+6) | no |
| Gold held at 6-12 (non-spender) | 0.64 million | 0.17 million | 53 million |
| Bought Gold | unspendable past the caps | unspendable past the caps | as today |
| New save state | ranks block, version 28 | none (the clamp changes) | ranks and purse, version 28 |
| New screen | yes | a line on the hero page | yes |
| Derivation (F2) | intact | must take the soft cap as input | intact |
| Survives a line change | yes (per family) | no (per hero) | yes (per family) |

## What every option needs

1. **A full re-solve.** Each option puts the reference player at about 8 to 10 times today's entry power by 6-12,
   while `recommendedPower` from chapter 2 on runs only from about 9,700 to 11,300. On today's content nearly every
   stage from mid chapter 2 on leaves its power band (x0.97 to x1.25) and its CEIL band. `recommendedPower` and
   the enemy levels would be re-solved with `Tools/solve-recommended-power.ps1` on the new reference walk, with
   `SolverTargetRatios` re-read, the 216 stage fingerprints regenerated with a stated reason, and
   `CheckEconomyRespondsToIncome`'s window re-read (Gold matters again under options 1 and 2). The proxy
   here multiplied each stage by one number; the solver will not, and its result must be walked again.
2. **A decision about bought Gold.** Refusing Sealed Gold is what keeps options 1 and 2 inside the R24 limit,
   and it leaves every Gold grant in the store worth little past chapter 2 (under option 1, 27 and 57
   million Sealed Gold unspent by the spender and the whale at 6-12). The store's Gold offers would be re-read. Accepting Sealed
   Gold is allowed by the Belgian rule (a level is deterministic) but breaks the R24 limit seven times over.
3. **A lead limit in days for walled content**, as the walls study asked, if harder walls ship.

## Considered and set aside

- **Relic tiers past 3** (the data-only form of options 1 and 3): see option 3; the Dust economy is about two
  orders of magnitude too small, and the Add stats need a code cap.
- **Caps that rise with account rank** (the other form of option 2): F7 says caps depend on star and ascension
  tier only, and account XP comes from battles, not from idle, so it does not turn waiting into power; it
  turns playing into power. Not measured.
- **Raising `fiveStarLevelCaps` and `levelCapsBelowFiveStars` in data**: they are derived and asserted by
  `CheckFusionEconomyDerivation`, and raising tier 0's cap moves where tiers 1 and 2 start (F6), so every
  ascended hero jumps. Not measured.

## Recommendation: option 2, the soft cap

1. **It is the only option whose power the campaign can be re-solved against.** It adds Levels, and Level is
   the one number combat reads for stats and mitigation, so power keeps predicting the fight. Re-solved the
   solver's way, it walks today's pace exactly (134.2 days, no walk stuck). Options 1 and 3 jam under the same
   re-solve (59 and 54 of 60 stuck), and even under the generous stats proxy they stretch the campaign to 199
   to 237 days.
2. **It makes harder walls fair.** It is the only option under which walls harder than the walls study's C
   (B+6) stick no walk after chapter 1, with finales at 2 to 4.5 days and a short tail.
3. **It turns waiting into power at the size an idle RPG needs**: 2.8 percent a day in chapter 2 to 1.3 in
   chapter 6, against 0.47 to 0.06 today, and 1.0 to 2.4 percent a day while held at a wall.
4. **It keeps the whale lead inside the R24 limit** (-1.1 to +2.9 percent from 4-12 on) when it takes earned
   Gold only.
5. **It is the smallest change and uses what exists**: the level-up, the level curve, Gold, the hero page. No new
   currency, no new screen, no new save block. It gives the 53 million idle Gold something to buy.
6. **Nothing bought is random**: one Level, one price, shown before the tap.

Its costs, to decide with it: the F2 derivation must take the soft cap as an input; the load clamp changes;
ascension of an over-levelled hero needs a rule; past-cap Levels are per hero, so a new 5-star starts its climb
again; bought Gold loses most of its use; and the campaign must be re-solved before any of it ships.

## Limits

- **The re-solve is a proxy.** One multiplier per stage from the median non-spender, applied to every enemy. A
  real solver run sets each stage's levels and rosters on its own and may move the walls' cost in either
  direction. B, B+3 and B+6 were not re-tuned per wall.
- **The options were sized, not tuned.** Each was set to about 2 to 3 percent a day in chapter 2 in one 4-walk
  sizing pass, then measured at 60. The three are within about 25 percent of each other at 6-12, not equal.
- **The reference player never ascends**, so option 2's interaction with ascension and options 1 and 3's with
  the fusing player were not walked. The fusing player already meets every stage far above its band.
- **The walk buys levels first and then the option, every sitting, cheapest first.** A real player may hoard or
  spread differently.
- **The diligent player is the ceiling**, as in the walls study.
- **Spender trailing the non-spender** by up to 2.5 percent under option 1 (earned Gold only) was not traced.

## Reproduce

Apply `Docs/idle-power/idle-power-tooling.patch` to main. It carries the walls study's tooling (the
`--whale-loop` flag and the Linux workarounds) and adds `Tools/DevHarness/R24IdlePowerStudy.cs`, the walk hooks,
`StatMath.StudyHeroHook`, `StatMath.StudyEnemyScale` and `StatMath.StudyEnemyLevel`. All are exact no-ops
unless a flag asks for them: walk 0 of `ref:non` writes the same rows as main. On Linux install PowerShell 7,
.NET 8 and Mono (for libgdiplus), set `EMBERFALL_EXTRA_REFS` to `lib/net6.0/System.Drawing.Common.dll` of the
System.Drawing.Common 6.0 package, put its `runtimes/unix` build beside `DevHarness.exe`, and build with
`pwsh Tools/build-check.ps1 -NoRun`. Then, in four 15-walk ranges per column:

    dotnet Tools/DevHarness/bin/DevHarness.exe <content> --whale-curve 60 --whale-col ref:non --whale-loop --whale-range 0 15 --whale-out <dir>/ref_non_0.tsv <option flags>

Option flags: `--idle-opt 1`, `--idle-opt 2` or `--idle-opt 3`; `--idle-p freeGoldOnly=1` for earned Gold only;
`--enemy-scale <file>` (72 multipliers, one per stage) for the re-solve proxy, with `--idle-p enemyByLevel=1`
for Levels. Sizes are `--idle-p trainCost=20000 trainGrowth=1.10 trainShare=0.05 overcapPremium=2
essenceDivisor=100 essenceCost=200` (one `--idle-p` per value; these are the defaults). The S rows now also
carry exit power and Gold held. B is the walls study's `Docs/walls/walls-option-B.patch`, copied here as
`Docs/idle-power/walls-option-B.patch`. A column of 60 walks took 2.5 to 5 minutes on 4 cores, longer
where walks stick; the whole study took about 2.5 hours of compute.

## Decisions for the owner

1. **Which option.** Recommended: option 2, the soft cap, at a premium of 2.0, earned Gold only.
2. **Bought Gold.** Refuse it in the new sink (recommended, keeps the R24 limit), and re-read the store's Gold
   offers; or accept it and replace the R24 limit.
3. **Then the walls.** With option 2 and a re-solve, walls between B+3 and B+6, set per wall, cost the
   non-spender 1 to 4 days and stick no one. That is the walls study's open question answered with a growth
   channel, and it would go through the solver, not by hand.
