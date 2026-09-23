# Emberfall — the idle layer

> **Later correction, from the economy-curve pass.** Two claims below are now wrong against the
> files and are marked where they appear. The rate rule this document proposes was built and then
> abandoned: `idleGoldPerHour` became twenty-four hand-authored numbers whose implied multiple of
> the stage purse slid from 1.63 at 1-1 down to 0.51 at 2-12, so the cache paid relatively less
> the deeper a player got. The economy-curve pass restored the rule, with a chapter 2 exponent on
> top of it, and the derivation now lives in `campaign.json`'s header. The level-cost paragraph is
> also superseded: the curve is linear now, and the reasoning is in `balance.json`. Everything
> else here — why the rate is derived from content rather than from two constants, what the cap
> protects, the session shape — is intact and is why the pass could be done at all.

**Correction first.** The idle layer is not missing. `PeekIdle`/`ClaimIdle` accrue gold and
shards against `Profile.LastIdleClaimUnix`, the rate derives from `StagesCleared`,
`balance.json` carries four idle constants and an 8h cap, the save round-trips
`lastIdleClaimUnix`, and the main screen has an EMBER CACHE card with a claim button. What is
missing is what makes it matter: the rate is too small to notice, and the claim is exactly the
button-that-only-adds-numbers this brief rules out. This retunes what exists.

## What accrues, and how the rate is derived

Today: `(140 + 55 × stagesCleared)` gold/hour and `(7 + 2.2 × stagesCleared)` shards/hour. At
stage 6 that is 470 gold an hour, so a full 8h cache pays 3,760. Replaying The Kiln Gate pays
320 gold in roughly 25 seconds at 4x — **ten minutes of active farming beats the entire
overnight cache, twice over.** Two constants cannot track twelve hand-tuned stage payouts.

Derive the rate from the content instead. The cache runs **your best cleared stage** — the
`StageDef` at global index `StagesCleared - 1`, already resolvable from `ContentDatabase` — and
pays that stage's own `gold` and `shards` at a fixed notional clear rate:

```
idleGoldPerHour   = bestStage.RewardGold   × idleClearsPerHourGold    (4.0)
idleShardsPerHour = bestStage.RewardShards × idleClearsPerHourShards  (0.5)
```

**Superseded.** The gold half of that is live again but with different constants and a second
term, and it lives in `campaign.json` per stage rather than in two balance constants: 0.80 × the
stage's own gold through chapter 1, and 0.85 × it × 1.16 per stage into chapter 2. The exponent
exists because `recommendedPower` accelerates and a cache that grows only with the purse falls
behind it. The shard half was never built as a formula and the per-stage shard rates are still
hand-authored.

At stage 6 that is 1,280 gold and 8.5 shards an hour. Over a 10h night: **~12,800 gold and ~85
shards, about five hero levels and half a summon.** That is the design target; retune the two
multipliers against it.

Gold and shards accrue at different rates by design. Gold has an infinite sink (levels); shards
buy heroes, and with six heroes an overnight that pays a full summon empties the roster in a
week. **Account XP must not accrue at all** — when this was written it raised the hero level cap,
which should be earned by fighting. Caps are set by star and ascension tier now rather than by
account rank (F7 in `DESIGN-HERO-FUSION.md`), so that reason is gone; rank is still earned by
fighting and nothing here re-opens idle XP.

The state needs no new home. `LastIdleClaimUnix` lives on `PlayerProfile`, is written by
`SaveSerializer` as `lastIdleClaimUnix`, and the rate is a pure function of the already-saved
`StagesCleared`. **No new save field, no save version bump, no new event.**

## The return moment

After 8 hours the player opens to a filled cache card with the stage name on it — *The Kiln Gate
fed your cache: 12,800 gold, 85 shards, full for the last two hours.* Naming the stage makes the
number legible: the rate is visibly the campaign, so pushing one stage further raises it.

The decision is what to do with it, and there are two buttons:

- **Collect** — gold and shards into the purse, spend them anywhere.
- **Forge** — spend the entire cache on levels for **one hero you pick**, at 0.75× the normal
  `LevelUpCost`, shards included at the duplicate-conversion rate. Nothing reaches the purse.

A real trade: flexibility now, or a 25% discount for committing the whole night to one hero. It
uses `LevelUpCost` and the existing hero list, adds no currency, and teaches what the first-ten-
minutes doc wants taught — focused investment beats spread investment.

## The cap

Raise `idleCapHours` from **8 to 10**. Eight hours punishes a normal night: phone down at 23:00,
picked up at 08:30, and the last ninety minutes were thrown away silently.

It protects three things. The daily rhythm — the cache must be reliably full at breakfast, or
the ritual doesn't form. The campaign, since a week away must not fund a jump past stages never
fought. And the summon loop, because uncapped shards against a six-hero roster finish the
collection before the player learns what a Mender does.

Show the cap being hit — "full for 2h" — rather than clamping silently. A player who learns the
cap comes back on time; one who never sees it loses value invisibly.

## Session shape

**Two minutes.** Open → cache card → one decision (Collect or Forge) → one stage at 4x → close.
Complete because it contains a decision and a fight, and it ends on a natural stop cue: the
cache is empty and refilling. Say that on the card. A session that ends because the player ran
out of things to *resolve* feels finished; one that ends on the clock feels truncated.

**Twenty minutes.** Cache → Forge → push the next uncleared stage → lose → debrief → adjust the
line → re-clear → summon → push again. There is no stamina gate, so the long session is bounded
by content, not energy. Correct for now — and it means the real cliff is stage 12, where the
campaign ends and the idle rate stops climbing. Chapter 2 is the pressure valve, not a new
system.

## What the economy needs

- `startingGold` **1500 → 400** and `startingShards` **260 → 40**, as argued in the first-ten-
  minutes doc — both paper over a game with no income.
- Retire `idleGoldPerHourBase`, `idleGoldPerHourPerStage`, `idleShardsPerHourBase`,
  `idleShardsPerHourPerStage`. Replace with `idleClearsPerHourGold` (4.0) and
  `idleClearsPerHourShards` (0.5).
- `idleCapHours` **8 → 10**.
- `summonCost` 120 holds: at stage 6 an overnight is half a summon and an active session two or
  three, keeping summoning something you play toward rather than wake up to.
- ~~Level costs (`55 + 26L + 3.2L²`) hold. At level 20 a level is ~1,855 gold, so an overnight
  Forge buys five or six — visible, not decisive. Protect that ratio if anything else moves.~~
  **Superseded.** The curve is `600 + 470L` and the quadratic term is deliberately gone: a hero's
  power is linear in level, so a quadratic cost makes the gold needed to hold a power ratio grow
  as the cube of it, and no idle curve grows that fast. At level 20 a level is now 10,000 gold and
  an overnight cache at 2-6 buys about three. The ratio this line asks to protect was the thing
  that had to move.

## What must not be added yet

Guilds, events, PvP and awakening are the same mistake in four costumes: each attaches a second
economy to a game whose first economy has twelve stages and six heroes. PvP needs a roster deep
enough to build against, and six heroes means one correct team. Awakening adds a hero-power axis
before hero *identity* is readable in battle. Events need a live calendar, which needs a server.
Guilds need players.

The honest next thing is chapter 2 — more stages raise the idle rate, extend the campaign wall,
and cost one JSON file. Everything above stays true when it lands.

## New fields, stated

Two new balance constants (`idleClearsPerHourGold`, `idleClearsPerHourShards`), two retuned
(`idleCapHours`, the starting purse), four retired. **No new save field, no new content field on
heroes or stages, no new battle event.** The screen work is one card and one button.
