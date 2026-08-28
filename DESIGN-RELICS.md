# Emberfall — relics

Relics come in **two grades**. A **common** relic is a bundle of flat and percentage stats and
touches nothing but `UnitStats`. A **legendary** relic is rare and breaks a rule in the effect
pipeline, and contains no numbers at all.

This replaces the single standard this document used to open with — *"A relic changes a rule in the
effect pipeline. No relic contains an attack percentage."* That standard is withdrawn deliberately,
decided 2026-08-26; see DESIGN-BUILD-ORDER, "Relics move to a two-tier model", for the argument.
It is not withdrawn everywhere: it is still exactly the rule the legendary grade is held to.

> **Status: built.** The grade split, both grades' content, and every mechanic below are shipped
> and covered by `Tools/build-check.ps1`. This document describes what the code does, not what it
> should do. Where something is *not* built, or where the code and this document disagree, it is
> called out inline with a ⚠️ — there are three, and they are the only ones.

## The word is "grade", and it is not "tier" or "rarity"

Both of the obvious words are already taken by something else in this project, and using either
here would silently overload it:

| Word | Already means | Where |
|---|---|---|
| **tier** | A relic's **upgrade level** — tier 1 to tier 2 of the *same* relic, bought with Relic Dust. | DESIGN-RELIC-PROGRESSION §2, `balance.json`'s `relicMaxTier`, the `tier` field on `ownedRelics`. |
| **rarity** | A **hero's** star rarity, the 1-to-5-star multiplier on base stats. | `StatMath.Build`'s `rarityMultiplier` parameter, `StarMultiplier`, DESIGN-COMBAT-STATS §1. |

So: **grade**. Common grade, legendary grade. The content field is `grade`, the enum is
`RelicGrade { Common, Legendary }`, and the display word on every relic card in the shop and the
inventory is "Common" / "Legendary" with no noun after it. A relic therefore has both a grade and a tier, and
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

**Built.** `StatMath.ApplyRelic(UnitStats, RelicDef)` (`StatMath.cs:96`) folds the bundle into an
already-built `UnitStats` in place, and `StatMath.ForHero(def, level, relic)` (`StatMath.cs:67`) is
the overload that calls `Build` and then it. That one function is the whole of the common grade.
Both readers go through it: `CombatSimulation` at setup (`CombatSimulation.cs:199`) and the hero
detail screen's stat preview (`HeroDetailScreen.cs:270`), so the numbers on the card are by
construction the numbers the simulation fights with. `ForEnemy` does not take a relic; enemies do
not wear them, and that is stated as a signature rather than as a comment on a call site.

A legendary relic passed to `ApplyRelic` is a deliberate no-op rather than an error, because the
two grades share one equipment slot and the caller should not have to test the grade first.

**Consequence to own: common relics move `PowerScore`, and legendary relics do not.** `PowerScore`
reads MaxHp, Attack, Defense, Speed, CritChance and CritDamage, so six of the eight relics above
change a hero's displayed power and a team's total. Effect Hit and Effect Resist are outside
`PowerScore` on purpose (DESIGN-COMBAT-STATS §1), so Hollow Reserve, Marksman's Coil and Ironroot
Ward contribute power only through their Max HP or Speed half. This lands on `recommendedPower`
comparisons on the campaign card, and on `CheckStageWinnability`, which levels a team *to* a power
budget — a relicced team reaches that budget at a lower level. Neither is a reason not to do it;
both are reasons task 13's retune must happen after the common grade exists, not before.

⚠️ **Known gap, and it is only half-true today.** `HeroInstance.Stats` and `HeroInstance.Power`
(`PlayerProfile.cs`) do **not** apply the relic, because that property has no `ContentDatabase`
handle to resolve `EquippedRelicId` against. So the hero detail screen shows the relicced line
while team power totals, the hero-list sort and the roster cards all still show the unrelicced one.
The two disagree today. Closing it means threading content into that property and updating its call
sites; it is its own pass and is deliberately not folded into the grade split.

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
specific **hero** is a different axis and is not forbidden by this rule — `RelicDef` keeps a
display-only `pairedHeroId` field for it — but **no shipped relic uses it any more.** The four that
did were cut; neither surviving legendary carries one, and the common grade is forbidden one
outright. The field is kept rather than removed because the axis is still legitimate, not because
anything is waiting on it.

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

Neither is paired to a hero and neither is flavoured to an aspect. Both got a new `RelicHook`
member — `BypassEffectResist` and `ReflectDamage` — and **those two are now the only members the
enum has.** The three that existed when this was written (`Retarget`, `SplitWideDamage`,
`WidenPassiveTrigger`) were the four cut relics' hooks and went with them, along with
`ShapeByRelic`, the method that dispatched them. No relic reshapes an authored effect any more.

### Where each one hooks, verified against the code

Both hook assumptions were checked against `CombatSimulation.cs` before this was written, and
**both needed correcting**. Neither correction sank the relic; both changed what had to be built,
and both are built as corrected.

**The Unbroken Ledger — the gate is no longer `_rng.Chance(effect.Chance)`.** It was, when the
original document listed `effect.Chance` as a modifiable. Effect Hit and Effect Resist have since
shipped, and the landing roll now reads:

```
float landing = ResolveLandingChance(effect.Chance, effect.Status,
                                     source.EffectiveEffectHit, target.EffectiveEffectResist);
// ... Control Immunity subtracts again, for Silence/Stun only
if (!_rng.Chance(landing)) { ... }
```

The gate is `_rng.Chance(landing)` at `CombatSimulation.cs:573`, and `landing` is built by
`ResolveLandingChance` at `:59`. **It was unclaimed by any relic and by any aspect** — no aspect
identity modifies a landing chance (Ember stacks a Burn *after* the roll succeeds, Umbra governs
cleanse resistance, not landing). What claims it is the stat layer: Effect Hit, Effect Resist, and
Control Immunity. So the relic is not "hook the chance gate" — it is "pass this effect's landing
through `ResolveLandingChance` with `targetEffectResist` forced to zero", and that is what
`ApplyLedger` (`CombatSimulation.cs:1057`) does, called at `:551`.

One decision this forces, stated rather than left ambiguous: **Control Immunity is not bypassed.**
The relic says Effect Resist, and Control Immunity is a deliberately separate, narrower layer that
only ever touches Silence and Stun. A relic that erased both would make hard control unanswerable
by any defensive stat in the game, which is a much larger change than the one being asked for. If
that is wanted, it should be argued on its own.

**That decision is load-bearing on call order, not just on intent.** `ApplyLedger` runs *before*
the Control Immunity subtraction, never after. Recomputing the landing chance after it would
silently discard that subtraction too and quietly implement the larger change this section
declines. The harness pins it in three arms rather than two, because two cannot tell a working
Control Immunity apart from a Stun that never fired: 0 stuns land without the relic, 6 with it, and
14 with the same relic against a target carrying no Control Immunity at all.

And a determinism note specific to this relic: forcing Effect Resist to zero can push `landing` to
exactly 1.0, and `Chance(p >= 1f)` returns true **without drawing**. This relic will remove draws
from the stream, reliably, not incidentally.

**Mirrored Wound — `PassiveTrigger.OnDamaged` is fired, but it is not unused.** It is dispatched at
`CombatSimulation.cs:851`, immediately after the on-damaged energy grant. It already has one
consumer in shipped content: `tideturned_passive` ("Backwash") on The Tideturned, in
`enemies.json`. That is not a collision — a hero passive listening on a trigger and a relic
reflecting damage at the same dispatch point are different things — but "unused" is wrong, and the
reflect is built to coexist with Backwash rather than to assume an empty trigger.
`ApplyMirroredWound` (`CombatSimulation.cs:1090`) is called at `:860`, deliberately *after* the
passive, so a passive that heals or shields its owner resolves before the reflect can kill the
attacker out from under it — the passive behaves identically with and without the relic.

The reflect goes through `DealRawDamage`, the single funnel for every point of damage in the game,
rather than around it. A reflect that kills its attacker therefore fires their death, their
killer's `OnKill` and Gale re-entry, exactly as any other killing blow does.

⚠️ **One correction this document got wrong, and it matters.** The claim that recursion is already
bounded by `MaxPassiveDepth` (2) is **false for this relic**. That bound is read inside
`TriggerPassive`, and a reflect is not a passive — it never enters that method, so the depth
argument never sees it. Two Mirrored Wound wearers facing each other would reflect into each other
until the stack gave out. The relic carries its own guard, `_inReflect`, which bounds it to one
hop; it is the same shape as the `_inRedirect` guard Tide uses one method over, and for the same
reason. Unlike Tide's, it is not latent — two wearers on opposite sides is an ordinary thing for a
player to build toward, and the harness runs that fight.

**A legendary relic contains no number, so the reflected fraction lives in `balance.json`, not in
the relic's text**, exactly as `emberBurnStackCap` and `galeExtraTurnsPerRound` do for aspects. The
card says "part of that damage returns"; the constant, `mirroredWoundReflectFraction`, says how
much. It is named for the relic rather than for the grade, so a second reflecting relic gets its
own constant instead of silently inheriting this one's tuning. It ships at 0.25, which is a
placeholder like every other number in this pass.

## What was removed, and what the roster is now

**All six relics that predate the two-grade model are cut. None of them is provisional, and none is
coming back.** This is settled — signed off by Matthias — and is not an open question for a later
pass to revisit. The cuts landed in `relics.json` alongside the grade split itself, so the design
and the content agree rather than the document describing an intention.

- **Tidewrought Metronome** and **Green Hour Vessel** — cut before this rewrite, by
  DESIGN-HOOK-RESOLUTION §§2–3, because Tide and Verdant own the two overflow discards they were
  built on. Their entries are gone rather than carried as tombstones; DESIGN-HOOK-RESOLUTION is
  where that reasoning lives.
- **Splitting Prism** and **Warden's Debt** — **cut for good.** Both were shipped code, and both
  are now removed from `relics.json` as well as from this specification. Their write-ups were
  arguments about the hook conflict; that conflict is resolved, and the relics that existed to
  carry the argument went with it. Splitting Prism's invented cost (roll the status once, on the
  first target only) and Warden's Debt's `ExcludeSelf` clamp were both real and both worked — the
  harness proved each on shipped content — and neither is a reason to keep a relic the two-grade
  model has no place for.
- **Stormlance Oath** and **Verdict Sigil** — **cut as well.** An earlier draft of this section
  carried them under a flagged assumption that they survived at legendary grade, on the argument
  that removing them would leave live content with no design record. That assumption asked to be
  resolved explicitly if the intent was to cut them, and it was: they are cut, in the same pass,
  and the concern it raised is answered by the cut being total rather than partial. No live content
  is left undocumented, because no live content refers to them.

Their three hooks went with them. `RelicHook.Retarget`, `RelicHook.SplitWideDamage` and
`RelicHook.WidenPassiveTrigger`, the `ShapeByRelic` dispatch that read them, its call site in
`ResolveAbility`, and the `RelicDef` parameter fields that fed it (`FromTarget`, `ToTarget`,
`FilterKind`, `ExcludeSelf`, `FromTrigger`) are all removed. Nothing could reach them once the
relics were gone, and the codebase does not carry unreachable machinery on the chance a future
relic wants it — git has all of it if one ever does.

**The roster is therefore ten relics: the eight common ones above, and two legendary ones — The
Unbroken Ledger and Mirrored Wound.** The harness asserts the count, the split (8 and 2), each
relic's presence at its stated grade, and the absence of all six cut ids by name, so re-adding one
fails loudly instead of silently.

## Equipping

**One slot, per hero.** Unchanged by the grade split, and the reason is unchanged: a team-wide
relic is an aura, an aura is a global multiplier, and per-hero is what makes the pairing legible.

The one slot is **shared** between the grades — a hero wears one relic, common or legendary, not
one of each. A second slot reserved for a common relic would make the common grade mandatory rather
than chosen, and would turn "which relic" into "which two relics", which is a different design.
`relicSlotsPerHero` stays at 1.

⚠️ **Where assignment actually happens is not what this section used to say.** It said "on the hero
detail screen, beside the level and banner-slot rows". It is not there and never was: assignment
lives in `InventoryScreen`, which lists every owned relic and opens an in-screen hero picker per
relic, and `HeroDetailScreen` carries no relic UI at all. That divergence predates the grade split
and the split did not change it. Recorded as the state of things rather than quietly corrected in
either direction — moving the affordance onto the hero card is a defensible piece of work, but it
is a UI change nobody has argued for yet, and the doc should not imply it already happened.

## Entering the simulation

There are now **two** entry points, one per grade, and they are deliberately far apart.

**Common** resolves before the simulation starts. `StatMath.ForHero` produces the `UnitStats`,
`ApplyRelic` folds the bundle in, and `CombatSimulation` receives a hero whose numbers already
include the relic. The simulation never learns a common relic exists.

**Legendary** resolves inside the simulation, as it already did. `BattleRequest.Team` is a
`List<HeroInstance>`; `HeroInstance` carries `EquippedRelicId`; `Sim` resolves it to a `RelicDef`
during setup and carries it on the `CombatUnit`, and the hooks are read at their own resolution
points. No new state, no clock, no ambient lookup — a relic is data on the request, exactly like a
hero's level. `Run(request)` remains a pure function of `(content, team, stage, seed)`.

`BuildUnits` is where the two paths visibly separate: a common relic is folded into the stat line
and then dropped, and only a legendary one is attached to the unit. The simulation cannot see a
common relic even if it wanted to, because nothing holds a reference to it past setup.

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

Statically, a relic card states its grade in its own language, and the split is the point:

- A **legendary** card states the rule, in rule language — *"What this hero inflicts cannot be
  shrugged off"* — and never a number. The old test still applies to this grade unchanged: if it
  cannot be written as one sentence without a number, it does not belong here.
- A **common** card states the numbers, plainly — *"+14% Attack, +10% Speed"*. There is no rule to
  state, and writing one would be dressing up a stat bundle as something it is not.

The card shows the grade, because it is what tells a player which of those two things they are
reading. Both the relic shop and the inventory carry a grade badge on every card — gold for
legendary, muted for common, and deliberately never an aspect colour, since a legendary relic is
never flavoured to an aspect and the badge is the one place a player actually looks.

Which of the two texts a card shows is decided by `RelicDef.CardText()`, not by either screen. A
common relic's line is *built* from its stat block by `DescribeStats()` rather than authored, so
there is no second, hand-written description of the same relic to drift out of step — and
`ContentDatabase` warns if a common relic authors `rule` text that nothing will ever read.

## Fields, as built

**Content** — every entry in `Assets/Resources/Content/relics.json` carries `grade`
(`"Common"` / `"Legendary"`), and `RelicDef` carries `RelicGrade Grade`. Common entries carry a
`stats` array of `{ stat, mode, value }` where `mode` is `Multiply` or `Add`, which expresses all
eight relics above and keeps the two arithmetic rules explicit in the data instead of implied by
the stat name. Legendary entries carry `hook` and no stat block. The per-hook `params` block an
earlier draft expected is **gone**, not kept: it existed for the three cut hooks' arguments, and
neither surviving hook takes a parameter. No change to `heroes.json` or `enemies.json`.

**The grades are mutually exclusive, and it is enforced at load rather than by review.**
`ContentDatabase` drops a legendary relic that carries any stat block, a legendary with no hook, a
common that declares a hook, and a common with an empty stat block. This is what replaces the
guarantee `RelicDef` used to get from its own shape. The founding standard was previously enforced
structurally — there was no `Power`, `Magnitude`, `Multiplier` or `Bonus` field anywhere on the
class, so a relic that wanted to be "+15% attack" had nowhere to put the 15. Numbers now live on a
separate `RelicStatBonus`, and a legendary relic's stat block is required to be empty, so the 15
still has nowhere to go. **The guarantee moved mechanism; it did not weaken.**

**Save** — no new field and **no save version bump**. `HeroInstance.EquippedRelicId` already exists
and already holds any relic id; `ownedRelics` in the profile already holds `{ relicId, tier }`.
Grade is a property of the definition, not of the save, so it is looked up from `relics.json` and
never written down. `SaveSerializer.CurrentVersion` stays at 5, as designed.

**Balance** — `relicSlotsPerHero` stays 1. `relicMaxTier` is unchanged and still means upgrade
level. `mirroredWoundReflectFraction` is the one new constant, named for the relic rather than for
the grade. Nothing else.

**The two stale code comments an earlier draft flagged are fixed.** Both asserted the withdrawn
blanket standard as a constraint: the `RelicHook` doc comment in `Enums.cs` and the `_comment` at
the top of `relics.json`. Each now scopes that standard to the legendary grade, which is where it
still holds in full. Nothing in the code now contradicts this document.

## This is a starting set

Both lists above are first drafts, and shipping them did not make them final. Eight common relics
and two legendary ones are enough to build the grade split against and not enough to ship a game
on: the common grade is justified by being numerous and the legendary grade by being rare, and
neither of those is true at these counts yet. The numbers are unbalanced by admission, not by
oversight — task 13's retune is the only pass that produces real values, and it now has what it
was waiting for, since the grade split has landed.

Three things that are **not** provisional and should not be re-opened casually: the grade split
itself, aspects owning the four contested hooks, and the six cut relics staying cut. Everything
else here — the counts, all sixteen common numbers, the reflect fraction, and which relics exist —
is a first pass and is expected to move.
