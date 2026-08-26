# Emberfall — relics

Relics come in **two grades**. A **common** relic is a bundle of flat and percentage stats and
touches nothing but `UnitStats`. A **legendary** relic is rare and breaks a rule in the effect
pipeline, and contains no numbers at all.

This replaces the single standard this document used to open with — *"A relic changes a rule in the
effect pipeline. No relic contains an attack percentage."* That standard is withdrawn deliberately,
decided 2026-08-26; see DESIGN-BUILD-ORDER, "Relics move to a two-tier model", for the argument.
It is not withdrawn everywhere: it is still exactly the rule the legendary grade is held to.

## The word is "grade", and it is not "tier" or "rarity"

Both of the obvious words are already taken by something else in this project, and using either
here would silently overload it:

| Word | Already means | Where |
|---|---|---|
| **tier** | A relic's **upgrade level** — tier 1 to tier 2 of the *same* relic, bought with Relic Dust. | DESIGN-RELIC-PROGRESSION §2, `balance.json`'s `relicMaxTier`, the `tier` field on `ownedRelics`. |
| **rarity** | A **hero's** star rarity, the 1-to-5-star multiplier on base stats. | `StatMath.Build`'s `rarityMultiplier` parameter, `StarMultiplier`, DESIGN-COMBAT-STATS §1. |

So: **grade**. Common grade, legendary grade. The content field is `grade`, the enum is
`RelicGrade { Common, Legendary }`, and the display word on the hero card and in the shop is
"Common" / "Legendary" with no noun after it. A relic therefore has both a grade and a tier, and
they are orthogonal: a common relic can be upgraded from tier 1 to tier 2 exactly like a legendary
one, and nothing about the grade changes how tiers work. Do not write "tier 2 relic" when you mean
"legendary relic". Nothing in this document uses "tier" for anything but the upgrade level.

## Common grade

Flat and percentage stat bundles. Numerous, cheap to author, easy to balance, and — this is the
point of the grade — they need **no simulation hooks at all**. A common relic resolves entirely in
`StatMath` / `UnitStats` and never reaches `CombatSimulation`. Nothing in the common grade can
contest a hook with an aspect, with a legendary relic, or with anything else, because it never
enters the effect pipeline in the first place.

They are also **aspect-neutral and universal**: no common relic is gated to an aspect, a role or a
faction, and none is flavoured toward one. Any hero can wear any of them, and none carries a
`pairedHeroId` — pairing is a legendary-grade affordance at most, and the common grade does not use
it. The aspect half of that rule is shared with the legendary grade; the rest is specific to this
one, because a stat bundle that only fits one kind of hero is a worse stat bundle, not a design.

### The starting set

**This is a starting set, not a final one.** The values below are first-pass and unbalanced; they
have not been run through `RunCampaignBalancePass` and are not the output of a tuning pass. They
exist to give the grade a shape and to give the shop something to sell. Expect all sixteen numbers
to move, and expect the set to grow — eight is a floor for a grade whose whole justification is
being numerous, not a target.

| Relic | Effect |
|---|---|
| **Warband Standard** | +14% Attack, +10% Speed |
| **Bastion Core** | +18% Defense, +12% Max HP |
| **Cutting Edge** | +12% Crit Chance, +20% Crit Damage |
| **Hollow Reserve** | +12% Max HP, +10% Effect Resist |
| **Marksman's Coil** | +12% Effect Hit, +8% Speed |
| **Warlord's Momentum** | +10% Attack, +15% Crit Damage |
| **Ironroot Ward** | +16% Defense, +10% Effect Resist |
| **Vanguard's Pulse** | +10% Speed, +10% Crit Chance |

Every stat named above already exists on `UnitStats` and is already read by the simulation. This
set adds no new stat.

### What "+12%" means, per stat, and why it is not one answer

The eight relics above use one "%" sign for two different operations, because the stats they touch
are stored in two different shapes. Getting this wrong makes half the set worthless, so it is
decided here rather than left to the implementation pass:

- **Attack, Defense, Max HP, Speed — multiply.** These are large scaled numbers built by
  `StatMath.Build` off `BaseAttack * scale * rarityMultiplier` and friends. `+14% Attack` means
  `Attack *= 1.14`.
- **Crit Chance, Crit Damage, Effect Hit, Effect Resist — add percentage points.** These are stored
  as small fractions or as a multiplier, are deliberately *not* scaled by level or star rarity
  (DESIGN-COMBAT-STATS §1 argues that at length), and multiplying them does not work. Shipped
  heroes carry `critChance` 0.08–0.20, `critDamage` 1.5–1.7, `effectHit` **0.00**–0.10 and
  `effectResist` 0.05–0.20. A multiplicative `+12% Crit Chance` on 0.08 is +0.0096, which is
  nothing; a multiplicative `+12% Effect Hit` on Kalyx's 0.00 is exactly zero, and Marksman's Coil
  would be a dead relic on him specifically. So `+12% Crit Chance` means `CritChance += 0.12`, and
  `+20% Crit Damage` means `CritDamage += 0.20` — 1.5 becomes 1.7, not 1.8.

Two clamps already sit downstream and are not being changed: `EffectiveCritChance` clamps at
**0.95**, so Cutting Edge on a 0.20 hero lands at 0.32 and nowhere near it; and
`ResolveLandingChance` clamps the status landing roll to `[MinEffectLanding, MaxEffectLanding]`, so
stacked Effect Hit cannot run away.

### Where a common relic actually applies

`StatMath.ForHero(def, level)` returns a `UnitStats`, and `CombatSimulation` calls it once per hero
at setup (`CombatSimulation.cs:176`). The natural entry point is a `StatMath.ApplyRelic(UnitStats,
RelicDef)` called immediately after `Build`, so that **one** function is the whole of the common
grade and the hero detail screen's stat preview (`HeroDetailScreen.cs:262`) gets the same answer the
simulation does for free. `ForEnemy` does not take relics; enemies do not wear them.

**Consequence to own: common relics move `PowerScore`, and legendary relics do not.** `PowerScore`
reads MaxHp, Attack, Defense, Speed, CritChance and CritDamage, so six of the eight relics above
change a hero's displayed power and a team's total. Effect Hit and Effect Resist are outside
`PowerScore` on purpose (DESIGN-COMBAT-STATS §1), so Hollow Reserve, Marksman's Coil and Ironroot
Ward contribute power only through their Max HP or Speed half. This lands on `recommendedPower`
comparisons on the campaign card, and on `CheckStageWinnability`, which levels a team *to* a power
budget — a relicced team reaches that budget at a lower level. Neither is a reason not to do it;
both are reasons task 13's retune must happen after the common grade exists, not before.

### Determinism

Common relics change stat values, not the number of RNG draws, with one edge worth naming:
`DeterministicRng.Chance` short-circuits at `p <= 0` and `p >= 1` **without drawing**. A relic that
pushes a landing chance across either boundary therefore adds or removes a draw. Marksman's Coil's
Effect Hit is the one in this set that can do it. That is not a bug and not new — the existing
caveat below already says a relic changes which battle a seed produces — but it means "common
relics are stat-only, so they are seed-safe" would be false, and nobody should write it down.

## Legendary grade

Rare. Rule-breaking. **The old standard applies here in full**: a legendary relic changes a rule in
the effect pipeline, states itself as one sentence, and contains no number. If it cannot be written
as one sentence without a number, it is a common relic wearing the wrong grade.

A legendary relic is the same *kind* of thing an aspect identity is — it alters how the simulation
behaves rather than what a number reads. That similarity is the reason for the hardest rule on the
grade:

**Never flavoured or paired to an aspect, even informally.** Not gated to one, not named after one,
not written so its flavour text only makes sense on one, not balanced on the assumption that its
wearer is one. A relic is equipment a player chooses to put on a hero; an aspect is what a hero
*is*, on every battle, whether or not anything is equipped. A legendary relic that reads as "the
Ember one" is a second, optional, worse way to be Ember, and it makes both weaker. Pairing to a
specific **hero** is a different axis and is not forbidden by this rule — the shipped relics carry
a display-only `pairedHeroId` — but neither new relic below uses it, and the grade is better off
without it.

**Hook contention: aspects win by default.** The four hooks contested between the two systems are
settled and are not reopened — Tide owns the `AddEnergy` overflow redirect, Verdant owns the
`ApplyHealValue` overflow banking, Ember owns Burn stacking, and Orrin is Lumen. All six aspect
identities are **already built and live** in `CombatSimulation.cs`. A legendary relic that needs
simulation access must find a hook none of them holds, or argue explicitly for a new one. It may
not compete for an aspect's. See DESIGN-HOOK-RESOLUTION for the full account.

### The starting set

Two, and **this is a starting set, not a final one** — the same caveat as the common grade, for the
same reason. A grade whose whole premise is being rare does not need eight, but it needs more
than two before it reads as a grade rather than as an exception.

**The Unbroken Ledger** — *"What this hero inflicts cannot be shrugged off."* Harmful effects from
the wearer bypass the target's Effect Resist entirely on the landing roll.

**Mirrored Wound** — *"Every wound dealt to the wearer is a wound taken."* When the wearer is
damaged, part of that damage returns to whoever dealt it.

Neither is paired to a hero and neither is flavoured to an aspect. Both need a new `RelicHook`
member; neither can reuse one of the three that exist.

### Where each one hooks, verified against the code

Both hook assumptions were checked against `CombatSimulation.cs` before this was written, and
**both need correcting**. Neither correction sinks the relic; both change what has to be built.

**The Unbroken Ledger — the gate is no longer `_rng.Chance(effect.Chance)`.** It was, when the
original document listed `effect.Chance` as a modifiable. Effect Hit and Effect Resist have since
shipped, and the landing roll now reads:

```
float landing = ResolveLandingChance(effect.Chance, effect.Status,
                                     source.EffectiveEffectHit, target.EffectiveEffectResist);
// ... Control Immunity subtracts again, for Silence/Stun only
if (!_rng.Chance(landing)) { ... }
```

The gate is `_rng.Chance(landing)` at `CombatSimulation.cs:565`, and `landing` is built by
`ResolveLandingChance` at `:59`. **It is unclaimed by any relic and by any aspect** — no
`RelicHook` member touches it, `ShapeByRelic` does not reach it, and no aspect identity modifies a
landing chance (Ember stacks a Burn *after* the roll succeeds, Umbra governs cleanse resistance,
not landing). What claims it is the stat layer: Effect Hit, Effect Resist, and Control Immunity.
The relic is therefore compatible, but it is not "hook the chance gate" — it is "pass this effect's
landing through `ResolveLandingChance` with `targetEffectResist` forced to zero".

One decision this forces, stated rather than left ambiguous: **Control Immunity is not bypassed.**
The relic says Effect Resist, and Control Immunity is a deliberately separate, narrower layer that
only ever touches Silence and Stun. A relic that erased both would make hard control unanswerable
by any defensive stat in the game, which is a much larger change than the one being asked for. If
that is wanted, it should be argued on its own.

And a determinism note specific to this relic: forcing Effect Resist to zero can push `landing` to
exactly 1.0, and `Chance(p >= 1f)` returns true **without drawing**. This relic will remove draws
from the stream, reliably, not incidentally.

**Mirrored Wound — `PassiveTrigger.OnDamaged` is fired, but it is not unused.** It is dispatched at
`CombatSimulation.cs:843`, immediately after the on-damaged energy grant. It already has one
consumer in shipped content: `tideturned_passive` ("Backwash") on The Tideturned, in
`enemies.json`. That is not a collision — a hero passive listening on a trigger and a relic hooking
the same trigger are different things, and Verdict Sigil's `WidenPassiveTrigger` hook touches
`AfterBasicAttack`, not this — but "unused" is wrong and the reflect must be built to coexist with
Backwash rather than assume an empty trigger.

Two things already in place that this relic needs, and neither has to be added: recursion is
bounded by `MaxPassiveDepth` (2), so a reflect that damages an attacker and re-enters their own
`OnDamaged` terminates; and `TriggerPassive` already takes the `depth` argument that bound is read
from.

The word "portion" in the relic's description is the one thing that is not yet a rule. **A
legendary relic contains no number, so the reflected fraction lives in `balance.json`, not in the
relic's text**, exactly as `emberBurnStackCap` and `galeExtraTurnsPerRound` do for aspects. The
card says "part of that damage returns"; the constant says how much.

## What was removed from this document, and what survives

Removed, per the two-grade decision:

- **Tidewrought Metronome** and **Green Hour Vessel** — already cut before this rewrite, by
  DESIGN-HOOK-RESOLUTION §§2–3, because Tide and Verdant own the two overflow discards they were
  built on. Their entries are gone rather than carried as tombstones; DESIGN-HOOK-RESOLUTION is
  where that reasoning lives.
- **Splitting Prism** and **Warden's Debt** — both are shipped code (`relics.json`, `RelicHook`
  `SplitWideDamage` and `Retarget`), and both are removed from this document's specification. Their
  write-ups were arguments about the hook conflict, and that conflict is resolved.

⚠️ **Assumption, flagged rather than silently applied.** The two shipped relics *not* named for
removal — **Stormlance Oath** (`Retarget`, `LowestHpEnemy` → `HighestHpEnemy`) and **Verdict
Sigil** (`WidenPassiveTrigger`) — are treated here as **surviving, at legendary grade**. Both meet
the legendary bar already: each changes a rule, each states itself in one sentence with no number,
and neither is flavoured to an aspect. Removing them from the design while they run in shipped code
would leave live content with no design record, which is worse than carrying them. If the intent
was to cut them too, that is a content change as well as a document change, and it should be made
explicitly.

That leaves the removals as a **document** change and the relic **content** as: four shipped relics
today, two of which (Splitting Prism, Warden's Debt) are now unspecified here and are candidates
for removal from `relics.json` in the implementation pass, two of which (Stormlance Oath, Verdict
Sigil) are legendary-grade and stay.

## Equipping

**One slot, per hero, on the hero detail screen**, beside the level and banner-slot rows. Unchanged
by the grade split, and the reason is unchanged: a team-wide relic is an aura, an aura is a global
multiplier, and per-hero is what makes the pairing legible.

The one slot is **shared** between the grades — a hero wears one relic, common or legendary, not
one of each. A second slot reserved for a common relic would make the common grade mandatory rather
than chosen, and would turn "which relic" into "which two relics", which is a different design.
`relicSlotsPerHero` stays at 1.

## Entering the simulation

There are now **two** entry points, one per grade, and they are deliberately far apart.

**Common** resolves before the simulation starts. `StatMath.ForHero` produces the `UnitStats`,
`ApplyRelic` folds the bundle in, and `CombatSimulation` receives a hero whose numbers already
include the relic. The simulation never learns a common relic exists.

**Legendary** resolves inside the simulation, as it already does. `BattleRequest.Team` is a
`List<HeroInstance>`; `HeroInstance` carries `RelicId`; `Sim` resolves it to a `RelicDef` on the
`CombatUnit` during setup, and the hooks are read at the existing resolution points. No new state,
no clock, no ambient lookup — a relic is data on the request, exactly like a hero's level.
`Run(request)` remains a pure function of `(content, team, stage, seed)`.

The honest caveat is unchanged and now applies to both grades: relics change how many RNG draws a
battle consumes, so the same seed with a different relic is a different battle. Determinism is
per-request and always was; the README's replay guarantee holds as long as the relic id is part of
the request.

## Presentation

**Legendary relics announce themselves in the battle log; common relics do not.** A legendary relic
emits through the **existing** `PassiveTrigger` event with the relic name in `AbilityName`, which
the battle screen already banners as "NAME — PASSIVE". No new event type. Emit one the first time
the relic alters an effect in a fight, not on every tick. A common relic has nothing to announce —
its contribution is already inside every damage number on screen, and a banner saying "+14% Attack"
fires once and tells the player nothing they can act on.

Statically, the hero detail card states each grade in its own language, and the split is the point:

- A **legendary** card states the rule, in rule language — *"What this hero inflicts cannot be
  shrugged off"* — and never a number. The old test still applies to this grade unchanged: if it
  cannot be written as one sentence without a number, it does not belong here.
- A **common** card states the numbers, plainly — *"+14% Attack, +10% Speed"*. There is no rule to
  state, and writing one would be dressing up a stat bundle as something it is not.

The card should show the grade, because it is what tells a player which of those two things they
are reading.

## New fields

**Content** — `Assets/Resources/Content/relics.json` gains `grade` on every entry
(`"Common"` / `"Legendary"`), and `RelicDef` gains `RelicGrade Grade`. Common entries carry a stat
block — the shape wants deciding in the implementation pass, but a flat list of
`{ stat, mode, value }` where `mode` is `Multiply` or `Add` is enough to express all eight relics
above and keeps the two arithmetic rules explicit in the data instead of implied by the stat name.
Legendary entries keep the existing `hook` plus its per-hook `params` block, and gain two new
`RelicHook` members for the two relics above. No change to `heroes.json` or `enemies.json`.

**Save** — no new field and **no save version bump**. `HeroInstance.RelicId` already exists and
already holds any relic id; `ownedRelics` in the profile already holds `{ relicId, tier }`. Grade is
a property of the definition, not of the save, so it is looked up from `relics.json` and never
written down. `SaveSerializer.CurrentVersion` stays at 5.

**Stale code comments this rewrite does not touch.** Two places in the code still assert the
withdrawn standard as a constraint, and will read as contradicting this document until the
implementation pass reaches them: the `RelicHook` doc comment in `Enums.cs` ("a relic changes a
RULE, never a number, so there is deliberately no 'add N%' member here"), and the `_comment` at the
top of `relics.json` ("Every 'rule' below is one sentence with no number in it, which is the test
DESIGN-RELICS sets"). Both are correct about the legendary grade and wrong as a blanket rule.
Flagged, not edited — this is a document change.

**Balance** — `relicSlotsPerHero` stays 1. `relicMaxTier` is unchanged and still means upgrade
level. Mirrored Wound's reflected fraction needs a new constant; name it for the relic, not for the
grade. Nothing else.

## This is a starting set

Both lists above are first drafts. Eight common relics and two legendary ones are enough to build
the grade split against and not enough to ship a game on: the common grade is justified by being
numerous and the legendary grade by being rare, and neither of those is true at these counts yet.
The numbers are unbalanced by admission, not by oversight — task 13's retune is the only pass that
produces real values, and it has to run after this lands, not before.

Two things that are **not** provisional and should not be re-opened casually: the grade split
itself, and aspects owning the four contested hooks. Everything else here is a first pass.
