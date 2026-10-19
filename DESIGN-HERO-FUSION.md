# Emberfall — Hero Fusion and Ascension

Fusion is the crafting path to a 5-star, and ascension is what a 5-star does with its duplicates.
Together they are the spine the roster is meant to grow along: levels were buying 70.7% of the
simulated player's power against a target of under 35%, the line it fielded at `2-12` was the line
it had at `1-3`, and fusion and ascension are the answer to both.

This document is a decision record like every other `DESIGN-*.md`, dated 2026-09-23. The rules in
section 1 are settled; the status of what is built lives in `DESIGN-BUILD-ORDER.md`.

---

## 1. The rules

These are the owner's, verbatim, and they are not relitigated here.

- **F1.** 1 to 4 star heroes never change star rating. Star rating means authored kit depth: 1 is
  basicAttack only, 2 adds passive1, 3 adds activeSkill, 4 is activeSkill plus passive1 with
  passive2 optional, 5 is the full kit with all three talents. Promoting a 3 star would be a request
  to author a skill, not a number change.
- **F2.** Only 5 stars ascend, 5 to 6 to 7 and up. Ascension adds power and level cap, never a new
  base kit.
- **F3.** Conversion below 5 stars outputs a RANDOM 5 star of that aspect, never a chosen one. The
  randomness is what manufactures the duplicate 5 stars that ascension eats. A chooser here kills
  ascension.
- **F4.** Class means ASPECT, not role. Five stars by role are Warden 1, Binder 1, Stalker 2,
  Tinkerer 2, so role pools would be vending machines: put fodder in, get the only Warden out. By
  aspect it is exactly 5 each across all six, so the roll is a real roll.
- **F5.** Above 5 stars only copies of that same hero count. No cross-hero substitution.
- **F6.** Ascension resets the hero to level 1 on a higher curve. Tier N+1 at level 1 out-powers
  tier N at its cap, so the reset reads as a promotion rather than a loss, and each ascension
  reopens the gold sink.
- **F7.** Level cap is a function of star tier and ascension tier, NOT of account rank. This is the
  load-bearing rule.
- **F8.** Umbra and Lumen 5-stars sit above their own role median. The existing check allows every
  5-star between x0.78 and x1.64 of its role median; give those two aspects a floor at x1.00 and
  leave the other four spread across the full band. They are deliberately rarer, and rarity with no
  payoff is a pool players learn to avoid.
- **F9.** Umbra and Lumen conversions take one mandatory seed hero of that aspect plus any-aspect
  fodder for the rest. Their fodder pools are 4 heroes against 14 for the other four aspects, so a
  same-aspect recipe would be a wall rather than a slower path, on a pool of only four heroes.

There is no fusion screen. The pity rule is untouched: it is a three-pull starter guarantee, not a
duplicate suppressor, and duplicates flow normally from pull four.

---

## 2. What the rules became

**A hero instance carries `HeroInstance.Ascension`**, 0 until it first ascends, and
`HeroInstance.Copies` counts its bodies, itself included. Save version 16 writes the tier per hero
and only when it is not 0 (`SaveSerializer.CurrentVersion`); a version 15 save loads every hero at
tier 0 at the level it had.

**Caps (F7)** are `StatMath.HeroLevelCap` of a star tier and an ascension tier. Account rank no
longer appears anywhere in them, and `heroLevelCapBase`, `heroLevelCapPerPlayerLevel` and
`heroLevelHardCap` are gone from `balance.json`.

**Ascension (F2, F5, F6)** is `GameSession.Ascend`, refused by `GameSession.AscensionBlocker` below
five stars, past the last tier, and without enough copies of the same hero. It spends the copies,
raises the tier and resets the tier level to 1. It never lowers a hero's Level: a hero carried over
from before caps, standing above its tier's cap, lands on the first step of the new tier above where
it stood.

**The curve is one ladder, walked at each tier's stride** (`StatMath.LevelAt`). A hero's `Level`
stays the single number its stat line is built from. Tier t's first level stands one Level past
tier t - 1's cap, so it out-powers that cap by exactly one Level (F6), and each of its tier levels
walks `ascensionCurveMultipliers[t]` Levels, rounded. Because stats are `1 + growth * (Level - 1)`,
a tier that starts at `1 + (M - 1) / growth` and steps `M` at a time has `M` times tier 0's curve
along its tier levels: the higher curve F6 asks for. The level cost is read off the tier level and
multiplied by the same `M` (`StatMath.LevelUpCost`), so a coin buys the same stride at every tier,
and each ascension reopens the sink at the level-1 price. `GameSession.TierLevelOf` is the level
inside the tier; `GameSession.NextLevelOf` is where one more puts the hero.

**Why on Level and not as a multiplier on the stats, because it was built that way first and
measured.** Combat reads a hero's level for more than its stats: Defense mitigation is
`defense / (defense + constant + perLevel * attacker's level)`. A tier-1 hero whose stats were
doubled by a multiplier stood at a lower level than a tier-0 hero of the same power, so its blows
were mitigated harder. With the level reset to 1 an even pinned line of tier-1 heroes at `2-6`'s
power cleared its ceiling 37% of the time against the tier-0 line's 85%; with the level carried on
across tiers but the stats still multiplied, `2-12` read 39% against 63%. Power had stopped
predicting the fight, and every instrument and every `recommendedPower` rests on it predicting the
fight. Walking the one ladder keeps equal power an equal fight at every tier, and it needed no
battle code: a snapshot fighter in the Proving is rebuilt from its Level and fights at the same
strength for the same reason.

**Conversion (F3, F4, F9)** is `GameSession.Convert`, refused by `GameSession.ConversionBlocker`.
The bodies must all be below five stars; they must be worth `conversionPointsRequired` at
`conversionFodderPoints` per star tier; they must all be of the target aspect, or, for an aspect in
`conversionSeedAspects`, at least one of them must be; and a deployed hero keeps the body standing
in the line. The output is a uniform roll over `GameSession.ConversionPool` - every fusable 5-star
of that aspect - and the method has no parameter that could choose. A hero whose last body is spent
leaves the collection. A 5-star that comes out already owned gains a copy and nothing else, because
that copy is what ascension spends. `HeroDef.Fusable` from the original design below is honoured by
the pool.

**F8** is a second floor inside `CheckStarBandByRole`: a 5-star of a seed-recipe aspect reads at or
above its role's median. Aminah and Caldus were lifted to it (`Docs/PINOY-LIST.md` section 2d).
**Velis and Kiva were then lifted together, and `KnownF8Exceptions` is empty.** The Striker role
has four 5-stars, so its median is the mean of the middle two, and a hero inside that pair drags
the yardstick he is graded against: Velis's smallest whole-kit lift (x1.30) left him in the pair at
x1.01 and dropped Kiva, a starting hero, to x0.72. Each lift now carries its hero out of the pair -
Velis above it and Kiva still below - so the median is Sylven and Vaelis alone (44,942) and neither
can move it. Kiva took x1.15 on every Damage number in her kit and reads x0.83; Velis took x1.45 on
his basic alone and reads x1.04, because the same step on his whole kit put three of his clauses
dark in the arena matrix. The lift moves every stage fingerprint, which the pass that made it
regenerates.

---

## 3. Where every number comes from

`CheckFusionEconomyDerivation` derives each number and fails if `balance.json` says anything else.
Six rules, each applied to numbers measured off the tree:

| what | rule | today |
|---|---|---|
| 5-star cap at tier 0 | levels may be 35% of the campaign's growth (pace target A): the finale's entry at its band middle over the starting line was x7.611 when this was derived, x7.611^0.35 = x2.035, and the cap is the last level inside it. **The x7.611 is pinned now** (`CapDerivationGrowth`): under the wall rule the finale is set by what a line at these caps reaches, so reading the caps back off the finale is a loop that settles near 9,000, under chapter 1's own finale | 9 |
| 5-star caps above | the same 35%, as if the line sat at that tier, on top of every ascension below it, so they rise because the rule holds still | 18, 39 |
| tiers authored | up to and including the first whose level 1 out-powers the finale's per-hero demand | 3 (tier 0, 1, 2) |
| curve multiplier | tier t's first Level is one past tier t - 1's cap; M = 1 + growth * (that Level - 1) | x1, x2.035, x1.5 (Levels 1-9, 10-45, 46-103). **Tier 2's x1.5 is not derived: it is the owner's decision of 2026-10-19 (R16), where the rule gives x6.175 and Levels 46-281.** See `Docs/BALANCE-REPORT-R16.md`, Fusion A |
| copies to ascend | an ascension costs the bodies whose level-1 power it adds: M(t+1) - M(t), rounded | 1, 4 |
| caps below five stars | the tier's measured share of a 5-star's level-1 power, times the 5-star tier-0 cap | 5, 6, 7, 8 |
| fodder points | a body is worth what it cost to pull against a 5-star: 100 * w5 / ws, so each lower tier's pulls make one conversion per 5-star's worth of pulls | 15, 10, 10, 18 of 100 |
| seed aspects | an aspect whose same-aspect conversion takes over twice the median aspect's pulls to fill | Umbra, Lumen (192 pulls against 36) |

At the 20 / 30 / 30 / 17 / 3 summon weights a 1-star is worth more fodder than a 2-star, 15 points
against 10, because a 1-star is the rarer pull. That is the rule reading the placeholder rates
rather than a choice, and it will move when the rates do.

The crossover table (F6) is printed for every 5-star on every run. The tightest margin is Marrow at
tier 0, by 100 power.

---

## 4. What it did to the walk

Measured on the economy walk against the committed campaign, and across eight walks with eight first
seeds (`ReportPaceAcrossSeeds`), because a conversion's roll makes one walk's roster one draw of the
economy.

- **The simulated player swaps fused 5-stars in.** The line rule is the five heroes with the highest
  ceiling, and a deployed hero gives up its slot only when it is at its own cap or the newcomer is
  already stronger - a capped hero has nothing left to throw away. The walk that the tables read
  swaps eleven times; seven of those are 5-stars a conversion made, the first of them Aeron and
  Elyra, already ascended, in at `1-11` for two 4-stars they out-powered at level 1, and later ones
  in for 5-stars standing at their cap. Seven of the eight walks swap a 5-star in during the very
  sitting a conversion first made it.
- **Target A moved and is not met.** Roster reads 49.7% to 56.9% of the growth to `2-12` across the
  eight walks against a target over 50%, from 21.0%; levels read 40.0% to 45.7% against under 35%,
  from 70.7%. The cap rule puts levels at 35% only if the rest of the growth is roster, and the
  roster the walk actually builds by `2-12` is a tier-1 line: tier 2 costs four more copies of the
  same hero on top of the first, and the line at `2-12` in the walk the tables read is five tier-1
  heroes.
- **Target B is not met: walls cost nothing.** No finale in any of the eight walks costs 2 to 4
  extra sittings. The player is stronger than the committed curve: the walk the tables read meets
  chapter 2's ordinary stages at x1.30 to x1.84 of `recommendedPower`, where the curve guard wants
  x1.10 to x1.30, and across the eight walks `2-12` is met at x1.16 to x1.59 against x0.95 to x1.02.
- **Gold stopped being the lever and shards became it.** Scaling the back half's gold by 2.5x moves
  the late-campaign ratio by 0.08; scaling gold and shards together moves it by 2.36.

**Re-deriving the curve for this economy does not put B in range either; it makes walls a lottery.**
`Tools/solve-recommended-power.ps1` was run against a throwaway copy of the content with the spine
in place - nothing it wrote was committed - and converged in 16 iterations, raising `2-12`'s
`recommendedPower` from 27,601 to 31,591 and its enemies eight levels. Against that curve, over the
same eight walks, `2-12` cost 0 extra sittings in five walks and 8, 19 and 21 in the other three;
`1-12` cost 0 or 1. None landed in 2 to 4. A walk either fused and ascended into the copies its
line needed before the finale and walked through it, or it did not and waited days for a
conversion to roll a duplicate of a hero it deployed. That is what F3's randomness does to a wall
when power comes in steps: the wait is the time to the next lucky roll, not a size anyone can set.
**So star-gated caps do not deliver target B, on either curve.** Roster share on the re-solved
curve read 49.7% to 61.5% and levels 34.9% to 45.9%.

**Target A in this split is partly definitional, and that should be said.** It grades the line at
level 1 against the levels on top of it, and F6 makes an ascended hero's first level stand where the
old tier's cap was. The levels a player bought below a tier are counted as roster the moment the
hero ascends. The split cannot tell a promotion paid for in copies from levels paid for in gold, and
no split built on "the line at level 1" can.

---

## 5. From the original document

The original recipe was four 4-stars of one class plus six 3-stars of any class for the player's
choice of a 5-star of that class, with class meaning role. F3 and F4 replace it: the output is a
roll, and class is aspect. Three parts of the original stand:

- **Fodder is destroyed.** No refund, no shard or gold recovery, no undo, and no currency cost:
  the heroes are the price.
- **`fusable` on a hero** excludes it from the conversion pool, for event-exclusive and summon-only
  5-stars. No shipped hero is excluded.
- **The confirmation requirement.** Before a fusion screen ships, it must show exactly which heroes
  will be consumed - names, star tier, aspect - before anything irreversible happens, and take an
  explicit confirm. What the player gets is a roll now, so the screen shows the pool it is rolled
  from rather than a chosen result.
