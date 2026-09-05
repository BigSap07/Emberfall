# Emberfall — The Proving (asynchronous PvP)

Design only. No C# exists for this yet and this document does not write any. It decides five
things — where an opponent comes from, how attempts are gated, what a win pays, what the player's
progress number is, and what the save has to grow — and it grounds each one in what
`CombatSimulation` already does today rather than in what a PvP mode is usually shaped like.

DESIGN-ARMOR and DESIGN-DEEP-RECITAL are the two documents this follows for shape: Recital for the
attempt-gated resource and the honest treatment of a rank with nothing to rank against, Armor for
the save bump and for storing resolved values rather than references that cannot be resolved.

---

## 1. The constraint everything here is built around

**There is no backend. There is no account system. There is no networking code of any kind in this
project** — no `UnityWebRequest`, no sockets, no HTTP client, no service layer. Save data is local
(`SaveService`/`SaveSerializer`), content is local (`ContentDatabase`), progression is local
(`PlayerProfile`). DESIGN-DEEP-RECITAL section 5 already established this for its leaderboard and
concluded that a real one "begins with an infrastructure decision that is out of scope."

That is a constraint on where an opponent's **data** comes from. It is not a constraint on what an
opponent **is**. The two get conflated, and conflating them is what would make this system
expensive to convert later.

So the load-bearing rule of this design:

> **An opponent is a plain data snapshot — hero ids, levels, stars, relic ids and resolved armor —
> that the simulation consumes exactly the way it already consumes an authored enemy line. Nothing
> in the combat path may know, or be able to ask, where that snapshot came from.**

Get this right and swapping local generation for fetched-from-a-server opponents is one function
returning a different instance of the same struct. Get it wrong — let the generator reach into
`ContentDatabase` or `PlayerProfile` from inside the fight — and the mode has to be rebuilt when a
backend arrives, because the thing it fights is not a snapshot, it is a live query.

This is the same instinct DESIGN-ARMOR section 10 applied to save data: a piece is written with its
**resolved rolls**, not a roll seed, because a seed is a promise that the thing it resolves against
never changes. An opponent carrying references into the local player's own collections is that same
broken promise, one layer out.

---

## 2. What the simulation accepts today

Audited before deciding anything. All line references are against the code as it stands.

`BattleRequest` (`CombatSimulation.cs:9`) carries five fields:

```
ContentDatabase   Content
List<HeroInstance> Team      // the PLAYER side
StageDef          Stage      // the ENEMY side, via Stage.Enemies
int               Seed
PlayerProfile     Profile    // optional
```

The two sides are materialised by different paths (`CombatSimulation.cs:~222–242`):

| | Player side | Enemy side |
|---|---|---|
| Input | `HeroInstance` | `EnemySpawn { EnemyId, Level }` |
| Def lookup | already resolved | `Content.GetEnemy(id)` → `EnemyDef` |
| Stats | `StatMath.ForHero(def, level, relic, armor, armorConfig)` | `StatMath.ForEnemy(def, level)` |
| Rarity | `StarMultiplier(def.Stars)` | hardcoded `1f` |
| Relic | yes | **no** |
| Armor | yes | **no** |
| Unit | `CombatUnit.FromDef(..., isPlayer: true, ...)` | `CombatUnit.FromDef(..., isPlayer: false, ...)` |

Three findings from that table matter more than the rest.

**The combat core is already side-agnostic.** `CombatUnit.FromDef` (`CombatUnit.cs:282`) takes the
abstract base `UnitDef`, and `HeroDef` and `EnemyDef` both derive from it
(`Definitions.cs:217, 296, 319`). `StatMath.Build(UnitDef, level, rarityMultiplier)` is likewise
shared; `ForHero` and `ForEnemy` are thin wrappers that differ only in what they fold in before
calling it. **Nothing in the simulation prevents a hero standing on the enemy side.** Only the
materialisation loop does, by hardcoding `GetEnemy` and `ForEnemy`.

**The enemy side cannot currently express gear.** `ForEnemy(def, level)` passes a rarity multiplier
of `1f` and takes no relic and no armor. An opponent that is a real player's team without its gear
is not that team; the armor and relic layers are most of what distinguishes two rosters at the same
levels. This is the one real engine change the mode needs.

**`BattleRequest.Profile` cannot serve an opponent, and must not be made to.** Its doc comment is
explicit that it exists for exactly one thing: "resolving each hero's armor uids into the pieces
they name, which only the profile's `OwnedArmor` collection can do." An opponent's armor uids do not
exist in the local player's collection and never will. Trying to route opponent armor through
`Profile` would either fail to resolve or, worse, silently resolve against the *player's* pieces and
fight a hybrid. **The opponent snapshot must carry resolved armor stats, not uids.**

---

## 3. The boundary: `BattleLoadout`

One new plain-data type, in `Core`, with no Unity types and no reach into content or profile:

```
BattleLoadout
    string          Label          // display only ("Keeper of the Ninth Ash")
    int             PowerScore     // precomputed, for matchmaking and display
    List<LoadoutUnit> Units        // up to MaxTeamSize

LoadoutUnit
    string           DefId         // resolves against ContentDatabase heroes
    int              Level
    int              Stars
    string           RelicId       // content id, resolves against ContentDatabase; may be empty
    List<ArmorPiece> Armor         // the pieces BY VALUE, not uids into anyone's collection
```

`DefId` and `RelicId` stay as **ids** because both resolve against `ContentDatabase`, which every
build has and which a server would also key against. `Armor` is carried **by value** because armor
resolves against a save that only its owner has. That split is not a compromise; it is the same one
`HeroInstance` already makes (`DefId` and `EquippedRelicId` are ids, and the doc comment on
`EquippedRelicId` says why: "a save holds ids, exactly like `DefId`, so content can change under it
without bricking it").

**No new type is needed for the armor.** `ArmorPiece` (`PlayerProfile.cs:142`) is already a flat
record of resolved values — slot, aspect, temper, and `ArmorStatRoll` primary and substats — with a
`Uid` that only matters for collection ownership and is simply unused here. It already serialises,
because version 8 already writes it. Carrying the pieces themselves rather than a summed stat blob
also keeps attunement working: `ApplyArmor` counts matching aspects across the equipped set
(DESIGN-ARMOR section 6), which a pre-summed bundle would have thrown away.

**`BattleRequest` grows one optional field:**

```
BattleLoadout Opponent   // null for every existing caller
```

When `Opponent` is non-null the enemy-side loop materialises from it instead of from
`Stage.Enemies`, through one new entry point:

```
StatMath.ForOpponent(HeroDef def, int level, int stars, RelicDef relic, List<ArmorPiece> armor, ArmorConfig config)
```

which is `ForHero(def, level, relic, armor, config)`'s existing body with two differences: the star
multiplier comes from the snapshot rather than from `def.Stars`, and the armor list arrives already
in hand rather than being resolved out of a profile's `OwnedArmor`. It calls the **existing**
`ApplyRelic` and `ApplyArmor` unchanged — so an opponent's gear goes through exactly the arithmetic
the player's does, which is the property that makes a fight against one meaningful.

When `Opponent` is null — every campaign fight, every Recital floor, every harness request — nothing
changes at all. That is the same additive shape armor used to reach the simulation without moving a
single existing fingerprint.

**The conversion story, stated plainly.** Replacing generated opponents with real ones is: a
function that returns a `BattleLoadout` stops calling the local generator and starts deserialising
one off the wire. `CombatSimulation`, `StatMath`, `CombatUnit` and the whole battle presentation
layer are untouched, because none of them can tell the difference — which is the entire point of
building it this way rather than the shorter way.

---

## 4. Where an opponent comes from today

Three candidates were considered. The decision is stated first, then the two rejections, because
the rejections are the argument.

### Decision: synthetic rosters from real hero templates

An opponent is generated by drawing **real `HeroDef`s from `heroes.json`**, assigning levels near
the player's own, rolling stars within a band, optionally attaching a real `RelicDef` id, and
rolling armor through the existing `ArmorRoller` and immediately resolving it to stat bundles.

**Why.** It is the only one of the three that produces the texture of a real opponent: a roster the
player does not field, at a power level close to theirs, with gear that is theirs alone. It also
exercises the exact code path a server snapshot would — same `BattleLoadout`, same materialisation,
same `ForOpponent` — so the day real snapshots arrive, the path is already proven in production
rather than being tried for the first time. The other two options both work by *avoiding* that
path, which means the path stays untested until the moment it matters most.

Generation is a **pure function of `(content, balance, standing, seed)`**, in the same shape as
`RecitalGenerator`, whose doc comment is the precedent: "A pure function of (content, floor): the
same floor number always produces the same enemy lineup and level, deterministically." The same
rule here means a given opponent is stable across app restarts until the player actually beats or
rerolls it, and it means the harness can assert against a named opponent without pinning a random
seed.

**What has to change to swap in real snapshots.** Only the source. `BattleLoadout` is already the
wire format — it is a flat list of ids, ints and stat bundles with no object graph and no local
references, so it serialises to JSON with the existing `MiniJson` and reuses version 8's own armor writer. A server
integration adds a fetch and a cache; it does not touch generation's consumers, because they consume
the type, not the generator.

### Rejected: an authored ladder of rival teams

Closest to Recital's precedent, and rejected for that exact reason: **it would be Recital again.**
Recital is already a hand-shaped ladder of authored lines that cycles aspects and scales by level,
and it is already the mode that answers "give me a fixed sequence of increasingly hard fights." A
second one differs from it only in the name over the door. It also does the thing this document's
section 1 warns against — the opponents are content, so the mode is coupled to content authoring
forever, and a server would have nothing to replace because there would be no snapshot boundary at
all. It is the cheapest option to build and the most expensive one to convert.

### Rejected as primary, accepted as a bootstrap: ghost of yourself

Snapshotting the player's own team from a past point is genuinely appealing — it needs no roster
generation, no power matching and no tuning, and it is fiction-perfect for this world, where the
Cinder's whole nature is recalling what has already been. It is rejected as the primary source
because it degrades exactly where the mode needs to work: a player with one strong team fights that
team forever, the variety ceiling is one roster wide, and a player who has never had a *different*
good team has no opponent at all.

It is worth keeping as the **first-run bootstrap**, before the player has enough standing for the
generator's power band to mean anything: the first two or three opponents can be the player's own
line from an earlier session, clearly labelled as such. This costs almost nothing, because a
snapshot of the player's own team is a `BattleLoadout` built from data already in the profile, and
it is the same type the generator emits. **This is a recommendation, not a decision — see sign-off.**

---

## 5. Attempt gating

**Follows the Echo bank exactly.** DESIGN-DEEP-RECITAL section 3 specified a stamina-like resource
that regenerates over real time whether or not the game is open, and the shipped implementation is
the pattern to copy, not to reinterpret:

- Two profile fields: a banked count and a clock (`RecitalEchoesBanked`, `RecitalEchoesClockUnix`)
- Two balance keys (`recitalEchoesMax`, `recitalEchoesRechargeHours`)
- A settle-then-spend entry point (`GameSession.TryStartRecitalAttempt`, `GameSession.cs:995`) that
  calls `SettleRecitalEchoes(now)`, refuses if the bank is empty, and decrements **before** the
  fight runs
- A result path (`GrantRecitalResult`) whose doc comment states it "does NOT touch Echoes —
  `TryStartRecitalAttempt` already spent one before the fight ran, regardless of what this call
  reports"

That last rule is the important one and it carries over unchanged: **an attempt is spent on entry,
not on outcome.** A loss costs the same as a win. Anything else makes the resource a tax on failure
and quietly incentivises save-scumming a mode whose whole appeal is low-stakes repetition.

The Proving's resource is a **Brand** — a burnt mark, in a world whose central object is a cinder.
Deliberately not "stamina", "energy" or "ticket": `Energy` is already a per-unit combat resource
(`UITheme.Energy`, `CombatUnit` energy, `_cfg.EnergyMax`) and reusing the word across two unrelated
systems is how the `idle*` prefix collision DESIGN-DEEP-RECITAL warned about starts.

| Field / key | Shape | Illustrative |
|---|---|---|
| `PlayerProfile.ProvingBrandsBanked` | `int` | — |
| `PlayerProfile.ProvingBrandsClockUnix` | `long` | — |
| `balance.provingBrandsMax` | `int` | 5 |
| `balance.provingBrandsRechargeHours` | `float` | 3 |

Illustrative values only, matching how DESIGN-DEEP-RECITAL presented its own. Five Brands at three
hours is a full refill in fifteen hours and roughly two sessions a day, which is a pacing decision
this document does not have the data to make. **Needs sign-off.**

---

## 6. Rewards

**Superseded, 2026-09-03.** This section originally specified Gold and Relic Dust and no new
currency. That call was flagged for sign-off and was **overruled**: The Proving now pays a currency
of its own, **Valor**, and pays nothing else. The original reasoning is kept below the line because
the risk it identified is real and did not go away — it is now managed differently rather than
avoided.

### What ships

A win pays `provingRewardValorBase + provingRewardValorPerStanding × standing`. A loss pays
`provingLossRewardFraction` of that — see the consolation argument below, which is unchanged.
**No Gold, no Shards, no Relic Dust, no Slag.** The harness asserts all four are untouched, because
"the mode touches no existing economy" is only true for as long as nothing quietly starts paying
one.

### Why the reversal is defensible

The original objection was: a new currency needs a sink, a sink means a shop, and a shop that sells
power is a second progression track beside armor and relics — the surface the task 13 retune had
just stabilised.

Paying an isolated currency turns out to be the **safer** of the two options against that risk, not
the more dangerous one, and the original section had it backwards:

- Paying **Gold and Relic Dust** injects into two live economies immediately, on day one, at
  whatever rate the mode ends up being played at. Relic Dust in particular is a relic-progression
  input, so a repeatable mode feeding it is a second faucet into a system already tuned around one.
- Paying **Valor** injects into nothing. Until it has a sink it is inert, which means the mode can
  be played, observed and tuned without moving any existing balance surface at all.

The risk therefore moves from "how much does this distort the economy today" to "what will Valor be
allowed to buy", which is a decision that can be made later, with play data, instead of a
consequence that lands now.

### The sink is the open decision, and it is deliberately not made here

**Valor currently buys nothing.** That is a holding position, not an oversight. What it is allowed
to buy is the whole question, and it is an economy decision that wants its own pass:

- **Cosmetic or vanity only** — safest, changes no balance surface ever, but weakest as a reason to
  play.
- **Convenience** — extra Brands, an opponent reroll. Contained, and a reroll in particular has a
  natural home on the Proving screen. Still touches nothing that fights.
- **Power** — armor, relics, hero copies. This is the option the original section was written to
  prevent, and nothing here weakens that argument. Doing it would need the task 13 surface
  re-examined, not assumed safe.

Until that is decided the number accumulates and is displayed, which is honest, and the mode is
playable without it.

### Consolation on a loss (unchanged)

A loss pays a reduced amount rather than nothing. Recital pays nothing on a loss, which is correct
for a ladder where a loss means you did not advance. The Proving is a repeatable sink for a
regenerating resource; paying zero makes a lost Brand feel like a bug rather than a result.
`provingLossRewardFraction`, illustrative 0.25.

---

## 7. Standing

`ProvingStanding` is an integer that goes up on a win and down on a loss, floored so it cannot go
negative, with `ProvingHighestStanding` recording the best ever reached.

**It is a personal-progress number, not a ranking.** This document states that as plainly as
DESIGN-DEEP-RECITAL section 5 did about its leaderboard, and for the same reason: there is nothing
to rank against. Every opponent is generated locally. No other player's standing exists anywhere in
the system, so a "rank" would be a number compared to nothing, and presenting it as competitive
would be a straightforward lie to the player.

Three consequences follow, and all three are design constraints, not implementation notes:

1. **The UI must not say "rank", "leaderboard", "top %" or show a ladder of other names.** It shows
   a tier and a number, the way a personal best is shown.
2. **Tiers are thresholds on the player's own number**, not population buckets. A tier that claims
   to be "top 10%" is claiming a population that does not exist.
3. **Standing drives opponent power**, which is what makes it mean something without a population:
   it is the difficulty dial, so climbing it produces harder fights and stalling produces winnable
   ones. That is a real feedback loop that needs no other players at all.

When real opponents arrive, standing is already the right quantity to match on, and the honest
label changes from "your standing" to "your rank" — a UI string and a matchmaking source, not a
redesign.

---

## 8. Save

**Next version: 9.** `SaveSerializer.CurrentVersion` is currently `8`
(`SaveSerializer.cs:96`, the armor bump).

Additive, following every prior bump in this project:

| Key | Type | Default for a version 8 save |
|---|---|---|
| `valor` | long | `0` |
| `provingBrandsBanked` | int | `balance.provingBrandsMax` (see below) |
| `provingBrandsClockUnix` | long | `0`, settled forward on first read |
| `provingStanding` | int | `0` |
| `provingHighestStanding` | int | `0` |
| `provingOpponent` | object, nullable | absent — generated on first entry |

**Upgrade needs no migration.** A version 8 save simply has none of these keys, which read as
`PlayerProfile`'s own field defaults — exactly a player who has never seen the mode. There is no
prior format for Proving progress to translate out of, the way a hero-level `relicId` had to become an
owned-relics entry at the version 4 → 5 bump.

**One deliberate exception, copied from version 7.** `provingBrandsBanked` defaults to
`balance.provingBrandsMax` rather than to the bare field default of `0`, so an upgrading player can
attempt immediately instead of waiting out a full recharge for a resource their save never had an
opinion on. The Echo bank did exactly this and the version 7 comment says why; the reasoning is
identical here and the deviation is called out for the same reason.

**`provingOpponent` is a persisted `BattleLoadout`**, so the opponent a player is looking at
survives closing the app. It is written the same way an armor piece is written — **resolved values,
not a generator seed** — for the reason DESIGN-ARMOR section 10 gives: a seed is smaller, but
`heroes.json`, `armor.json` and `balance.json` are all tuning data expected to move, and a seed
would mean every retune silently rerolled the opponent every player was mid-way through facing.
This is also the field that becomes a cached server response later, unchanged in shape.

**Downgrade is guarded the way every bump above is.** Version 8 code reads none of these keys, so it
would silently drop a player's standing, their Brand bank and their pending opponent; the existing
version check (`SaveSerializer.cs:291`) refuses a newer save rather than mangling it.

---

## 9. What this does not do

- **It is not multiplayer.** No opponent in this system is a real person, and the document takes
  care not to imply otherwise anywhere it would reach a player.
- **It introduces a currency but no shop and no power source.** Valor is paid and displayed and
  buys nothing; see section 6 for why the sink is deliberately still open, and why paying an inert
  currency disturbs less than paying two live ones.
- **It does not specify numbers.** Every value in this document is illustrative and marked as such.
  Brand recharge rate, reward scale, standing gain/loss, opponent power band and tier thresholds are
  all a balance pass, and that pass needs the mode running to have anything to measure.
- **It does not touch `CombatSimulation`'s turn loop, targeting, aspects or determinism.** The only
  engine change proposed is an optional field on `BattleRequest` and one new `StatMath` entry point,
  both additive, both inert when unused — so every existing seeded fingerprint stays valid.
- **It does not design the UI.** A screen, a Hub entry point and an opponent card are needed and are
  not specified here.
- **It does not decide whether the ghost bootstrap ships.** Section 4 recommends it; it is flagged
  for sign-off rather than assumed.
