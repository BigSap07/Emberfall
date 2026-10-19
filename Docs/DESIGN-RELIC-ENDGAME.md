# DESIGN-RELIC-ENDGAME: relics as endgame, and upgrades as the long tail

Decision record, dated 2026-10-19 (R17). Owner direction: relics are endgame content (they open at 3-1) and more
will be designed later, so Dust must stay worth earning between relic batches without running dry or piling up.
This builds the upgrade path `DESIGN-RELIC-PROGRESSION.md` section 2 specifies. The numbers live in
`relic-shop.json`; the measurement they were sized from is `RelicDustStudy` in `RelicEndgameChecks.cs`.

## The relic list is data

`ContentDatabase.Relics` is `relics.json` and nothing else. `RelicShop.View` lists it; `RelicShopScreen` draws that
view; a relic added to `relics.json` is on the shelf at its grade's price, can be bought and fights with its own
numbers, with no entry in `relic-shop.json` and no code. `CheckRelicShopIsDataDriven` adds a test relic to the file
in memory and proves each step, and proves no shipped relic id is a string anywhere under `Assets/Scripts`. A relic
can be priced on its own with an `overrides` entry. A new Mythic still needs code for its hook; a new Legendary (a
stat bundle) needs only the entry and art.

## The prices

Measured on the R16 curve, a committed player (12 highest cleared stages, 3 sweeps each) earns 1,548 Dust a day at
3-1, 3,204 at 4-12 and 4,932 at 6-12. The old prices (Legendary 80, Mythic 2,400) bought every relic in under four
days. New prices: **Legendary 5,000, Mythic 20,000**, all ten relics 80,000 Dust, which takes a committed player
**51.7 days from 3-1, 25 from 4-12 and 16 from 6-12**. A casual player (4 stages) takes months. The full table is
in `D:\Emberfall\handoff\R17-CORE-GAPS\data\relic-pricing.md`.

## The upgrades

`GameSession.RelicMaxTier` is 3 with the endgame on. Tier 1 is the purchase. `GameSession.RelicUpgradePrice` is the
relic's own price times the tier's multiplier (x2 for tier 2, x4 for tier 3), so each tier is about double the one
before. `GameSession.UpgradeRelic` checks everything first, takes the Dust through `SpendRelicDust` (so event
activity still sees it) and raises the tier. Every current relic to tier 3 is 560,000 Dust, seven times the
purchase: the tail that absorbs Dust between relic batches.

A tier is worth something in a fight. `PlayerProfile.EquippedRelicOf` is the one resolver for a hero's relic and
returns the relic with its stat block scaled by the tier's `statScale` (1.25, 1.5); the simulation's setup, the power
score and every preview read it, so the number on a card is the number the fight uses. A Mythic's hook and rule never
scale. **Tier 1 resolves to the content's own object**, so a fight that uses no upgrade is the fight it always was.

## The flag

`RelicEndgameSwitch.Enabled`, **ON by default**, like `OnboardingSwitch`. Define `EMBERFALL_RELIC_ENDGAME_OFF` to roll
back. Off means the shop charges balance.json's `relicShopPrice` and `relicMythicShopPrice` (80 and 2,400), the top
tier is balance.json's 1, an upgrade is refused as off, and an owned tier is ignored in a fight and in the power
score: the build before. A tier already in a save is kept and written back untouched.

`CheckRelicInventoryModel` (the existing mechanics check) runs with the switch off on purpose: it pins the relic
rules as they were, and passing is the rollback proof. `CheckRelicEndgameOff` adds the rest.

## The save and the content

An upgraded relic (`OwnedRelic.Tier` above 1) makes the document claim version 24 (`SaveSerializer.HoldsVersion24Data`);
a tier 1 relic adds nothing. `relic-shop.json` is **not hashed** into `ContentVersion`: the guild dungeon fights with no
relics, so no recorded replay reads it. relics.json, balance.json and the other six hashed files are untouched.

## The checks

`CheckRelicEndgame`: the file, the data-driven relic, the Dust study and its bands, the upgrade prices and effects,
every refusal leaving the save byte for byte unchanged, combat not moving for a tier 1 relic, a tier 3 relic being a
different fight, the save, not hashed, the rollback and the wiring.

## Limits

- The tier scales (1.25, 1.5) are a first pass. The R16 curve walk reads no relic, so they do not move it, but the
  balance of an upgraded line against the Proving and the Recital has not been walked.
- A Mythic's hook does not improve with tier. A hook that scales is a design call for the relic that has one.
- The Recital's milestone Dust is a one-off bonus and is not counted in the daily income.
