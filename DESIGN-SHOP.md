# Emberfall — the Quartermaster (NPC refresh shop)

Design only. No C# exists for this and this document does not write any.

A rotating stock of items an NPC sells for currency the player already has, restocking on a
cadence. No player-to-player anything: there is no backend, and a trading system is not a feature
this project can half-build. Every decision below is constrained by `Resources.Load` and a local
save, exactly like the rest of the game.

DESIGN-ARMOR and DESIGN-RELICS are the two sinks that came before this one, and this follows their
shape: audit first, decide with the audit in hand, and name what still needs a person.

---

## 1. The audit this is built on

Before deciding what a shop sells, the question is what the economy is actually short of. Counted
directly against the code rather than assumed — every site where a currency is added or subtracted:

| Currency | Sources | Sinks | The only sink is |
|---|---|---|---|
| **Gold** | 5 | **1** | hero level-up (`GameSession.cs:279`) |
| **Shards** | 6 | **1** | summoning (`GameSession.cs:1386`) |
| **Relic Dust** | 3 | **1** | `BuyRelic` (`GameSession.cs:866`) |
| **Slag** | 1 | 2 | armor reroll and combine |

Two of those are structurally different from the other two, and it is the difference that decides
this document.

**Gold's sink is capped, and the cap is not Gold's to lift.** `LevelUpCost` is
`600 + 470L` — it was `55 + 26L + 3.2L²` when this document was written, and the economy-curve
pass replaced it — so a single hero from 1 to the hard cap of 80 costs about **1,533,000 Gold**
rather than the 622,000 this paragraph used to quote. The argument below is unchanged and the
sink got larger, not smaller. When this was written `HeroLevelCap` was `12 + 4 × (playerLevel − 1)`,
capped at 80. **It is no longer a function of account rank**: a hero's cap is set by its star tier and
its ascension tier (`StatMath.HeroLevelCap`, `GameSession.HeroLevelCapOf`; the rules are F7 in
`DESIGN-HERO-FUSION.md`), and the values sit in `balance.json`. The argument holds with ascension in
place of rank: a player levels every fielded hero to its cap and then **cannot spend Gold on levels at
all** until a hero ascends or a higher-capped hero joins the line. Gold does not stop arriving during
that window: idle income alone floors at 140/hour against a 10-hour cap, and four other sources keep
paying. The sink is periodically **blocked**, not exhausted.

**Relic Dust's sink terminates permanently.** `BuyRelic` refuses a relic the player already owns,
and there are ten relics. Once all ten are bought, Dust has **no sink at all, ever**. The obvious
answer — relic upgrades — is specified in DESIGN-RELIC-PROGRESSION section 2 and, per
`GameSession.cs:866`'s own comment, *"not built yet"*.

**Slag is the healthy one, and it is worth saying why**: one source (salvaging armor), two sinks
(reroll, combine), and both sinks are repeatable. It is the only currency in the game whose supply
and demand are both open-ended. That is the shape to aim at, not a coincidence to admire.

### How strong is the Gold claim, honestly

It is a **structural** argument, not a measured one, and this document will not pretend otherwise.
Nobody has instrumented a real player's Gold balance over a campaign.

One piece of corroboration exists and is worth exactly what it is worth. `build-check`'s
instrument-comparison table reports the simulated economy player's line power against each stage's
`recommendedPower`, and it decays to **×0.86, ×0.83, ×0.86** across 2-10 to 2-12. A player short of
*Gold* would be short of levels and short of power. A player at the **level cap** would also be
short of power, while sitting on Gold they cannot spend. The instrument cannot tell those two apart,
and the second is consistent with what the cap formula predicts.

**Open item, and the first thing to do if this is built:** log ending Gold in
`RunCampaignBalancePass`. It is a one-line addition to an instrument that already simulates the
whole campaign economy, and it converts the central premise of this document from an argument into
a number. Nothing here should ship without it.

---

## 2. What the shop sells

**Armor pieces. Rolled, not authored. Bought with Gold.**

The reasoning is a process of elimination against systems that already exist, because the wrong
answer here is a second shop that competes with a first.

**Not relics.** They already have a shop — `RelicShopScreen`, priced in Dust, one purchase per relic
forever. A second storefront selling the same category in a different currency on a different
cadence is two systems the player has to hold in their head to answer one question.

**Not hero copies or "hero shards".** There is no such concept: heroes arrive through summoning as
`HeroInstance.Copies`, governed by a pity counter. Selling copies would be a second acquisition path
running beside a tuned one, and it is the most monetization-adjacent thing on the candidate list.
Anything touching summoning belongs in its own document with its own sign-off.

**Not a Gold-for-Shards exchange.** This is the tempting one and it is the most dangerous. Shards
buy summons; summons are the game's rarity economy and carry a pity system. An exchange sets a
price on rarity in a currency that accumulates passively while the player is offline, which makes
idle income a summon faucet. It also makes the Gold surplus disappear into the one system least able
to absorb it. **A currency converter is not a sink — it is a redirect**, and the difference matters:
a sink removes value from the economy, a converter moves it somewhere that has to be retuned.

**Armor, because the gap it fills is real and specific.** DESIGN-ARMOR section 8 covers where a
piece comes from, and the answer today is drops. A drop is untargetable: a player who needs Boots
cannot pursue Boots. Six slots, four tempers, rolled substats and an aspect tag per piece make the
search space wide enough that "keep fighting and hope" is the only strategy available. **A shop is
the natural counterweight to a random drop system** — not more pieces, but *visible* pieces, where
the player can see what is on offer and decide whether it is worth the Gold.

It also closes a loop that is currently open at both ends:

```
Gold ──buy──> armor piece ──salvage──> Slag ──reroll/combine──> better armor
```

Slag's only source is `SalvageArmor`, so Slag supply is entirely hostage to drop luck. Routing
surplus Gold into the front of that chain gives Slag a second-order source without inventing a
currency, and gives Gold a sink that does not terminate. Neither system gains a new mechanic; they
gain a connection.

### What it does not sell, and why that is a rule not an omission

**No consumables, no boosts, no XP items, no stat potions.** Every one of those is a power source
priced in a currency, and section 6 explains why that is the exact thing this design must not
become before someone has measured what it does to the campaign.

---

## 3. What buys it

**Gold.** Section 1 is the whole argument: it is the currency with five sources and one periodically
blocked sink.

**No new currency.** The constraint asked for airtight reasoning before adding one, and there is
none available. A new currency would need its own source, and the only sensible source would be
converting an existing currency — which is the redirect-not-sink problem again, with an extra step.

**Relic Dust is deliberately not the answer here, and that is a deferral rather than a decision.**
Dust has the more clearly broken sink of the two — terminal, not blocked. But the fix for it is
relic upgrades, which DESIGN-RELIC-PROGRESSION already specifies and which nobody has built. Having
a shop sell something else for Dust would be building a second answer to a question that already has
a first one waiting. **Flagged for sign-off in section 8.**

---

## 4. Refresh cadence

**Daily, on the existing reset clock. No second timer.**

`GameSession.DayStartUnix` and `RollDailyReset` already exist, already drive `StageClearsToday`,
and are configured by `balance.json`'s `dailyResetHourUtc` (currently 4). The shop reuses them.

This is not just convenience. That clock has one property a fresh timer would have to re-earn, and
its own doc comment states it: the stamp **only ever moves forward**, so a device clock wound
backwards cannot buy a fresh set of paid clears. With no server the device clock is the only source
of truth, and a shop that restocks on a rewindable timer is an infinite item faucet for anyone who
changes their system time. Reusing the proven clock inherits that immunity for free.

It also means the shop restocks at the same moment the repeat-clear allowance does — one daily beat
rather than two the player has to track separately.

**Implementation shape:** a `shopStockDayUnix` stamp alongside the stock itself. On open, if
`DayStartUnix(now) > shopStockDayUnix`, roll new stock and write the new stamp. Same structure
`RollDailyReset` already uses, and it means the shop needs no update loop at all — it is evaluated
when looked at.

---

## 5. Stock rules

**Per-item, one purchase each, and the slot stays visible as sold until the next refresh.**

The alternative is unlimited purchases within a window, and it is worse for a reason that is not
obvious: **unlimited stock turns the shop into an exchange rate.** If a Gilded piece always costs
N Gold and can be bought as many times as the player can afford, the shop stops being a set of
choices and becomes a formula — Gold in, armor out, at a fixed ratio, until Gold runs out. That
also makes the daily drain **unbounded**, which destroys the one thing this design most needs:
the ability to say how much Gold a shop removes per day.

With per-item limits, the maximum daily drain is the sum of the stock's prices. That is a number
someone can reason about, tune, and check against the Gold-logging measurement section 1 asks for.

Showing sold slots rather than removing them is a small thing that matters: an empty row reads as
a bug, and a greyed row reads as a decision the player already made.

**Illustrative stock size: 6 slots.** Not a decision — six is one per armor slot, which is a
convenient coincidence rather than a reason, and the right number depends on the drain target that
does not exist yet.

---

## 6. Interaction with task 13 — stated explicitly

**It does not require re-touching a single tuned number. It does widen a gap those numbers were
never measured against, and that gap already exists.**

Both halves matter.

**Why nothing needs retuning.** Task 13 retuned `campaign.json` — enemy levels on all 24 stages and
composition on 19 — and `enemies.json`. It tuned *difficulty*, expressed as how much AoE pressure a
line carries. Gold flow was not an input to it and is not an output of it. A Gold sink changes
nothing the retune measured.

**Why that is not the end of the answer.** Armor is not inert: `StatMath.ApplyArmor` folds every
equipped piece into `UnitStats` before the fight. Armor is a **power source**, and this shop sells
it.

And here is the part worth stating plainly, because it was checked rather than assumed: **none of
the three balance instruments equip any armor.** `CheckStageWinnability`, `CheckPinnedFiveBodyCurve`
and `RunCampaignBalancePass` all build profiles with an empty `OwnedArmor`, which makes `ApplyArmor`
an exact no-op — the same no-op `BattleRequest.Profile`'s doc comment describes for a null profile.
Every win rate in task 13's tables is therefore **an un-armored win rate**.

So a real player who has equipped drops is already stronger than every instrument in the project
believes. That gap is not created by this shop; it was created when armor shipped. What this shop
does is make the gap **reliable and targetable** rather than dependent on drop luck — which is
worse in the specific sense that it can no longer be waved off as noise.

**The recommendation is not to retune anything. It is to make one instrument model armor** before
this shop's stock is tuned, so the question "how much power does a purchasable piece add to a real
line" has an answer. Until then, tuning the shop's prices is tuning against a measurement that
cannot see the thing being sold.

---

## 7. Save

**Additive, following every prior bump.** New keys: the current stock (a small array of rolled
`ArmorPiece` records plus a price and a purchased flag each) and the `shopStockDayUnix` stamp.

Stock is stored as **resolved pieces, not a roll seed** — the same rule DESIGN-ARMOR section 10
gives and DESIGN-PVP section 8 repeats. `armor.json` and `balance.json` are tuning data expected to
move, and a seed would mean every retune silently rerolled the stock a player was looking at.

An upgrading save has none of these keys, reads as an empty stock with a zero stamp, and rolls fresh
stock the first time the shop is opened. No migration.

**A version-number collision to be aware of.** `SaveSerializer.CurrentVersion` is **8** on `main`
today, but the unmerged `feat/the-proving` branch already takes **9**. Whichever of the two lands
second takes 10. This document deliberately does not claim a number, because claiming one would put
a wrong constant into a doc that outlives the branch race.

---

## 8. Open decisions that need sign-off

1. **Paid manual refresh: recommended OUT of v1.** Two reasons, and only one is about monetization.
   It is monetization-adjacent, and it deserves that conversation on its own rather than riding in
   on a shop. But the stronger reason is measurement: a paid refresh makes the daily Gold drain
   **unbounded**, which destroys the bounded-drain property section 5 was designed to preserve and
   makes the Gold-logging measurement from section 1 unable to answer the question it exists for.
   Build the shop, measure the drain, *then* decide whether a refresh is needed — at which point
   there is a number to price it against. **Recommendation, not a decision.**

2. **Relic Dust's terminal sink.** Section 1 found it and section 3 declined to solve it here.
   Confirm that relic upgrades (DESIGN-RELIC-PROGRESSION section 2) are still the intended answer,
   or say the shop should absorb it after all.

3. **Whether armor is the right thing to sell at all**, given section 6. Selling a power source into
   a campaign whose difficulty was tuned without that power source is defensible — the gap predates
   this — but it is a judgement about how much slack the campaign has, and that is not a judgement a
   design document should make alone.

4. **Every number here is illustrative**: six slots, the price curve, the drain target. They are
   placeholders in the same sense DESIGN-DEEP-RECITAL section 3's stamina figures were, and they
   need the shop running against a Gold-instrumented economy pass before any of them mean anything.

---

## 9. What this does not do

- **No player-to-player anything.** Not deferred — excluded. It needs a backend this project does
  not have and is not close to having.
- **No new currency**, and section 3 gives the reasoning rather than just the constraint.
- **No consumables, boosts or stat items.** Section 2 makes that a rule.
- **No paid refresh in v1.** See section 8.
- **It does not decide the UI.** A screen, a Hub entry and a stock card are all needed and none are
  specified here.
- **It does not tune anything**, and section 6 explains why it cannot yet: the instrument that would
  have to validate the tuning is currently blind to the category being sold.
