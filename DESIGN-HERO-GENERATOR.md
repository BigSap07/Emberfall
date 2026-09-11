# Emberfall - hero generator

A reusable prompt that produces a new hero, ready to drop into `heroes.json`. Someone brought a
generator written against Idle Heroes and AFK Arena conventions: the design intent (a lore block,
one or two core mechanics, a design-then-JSON structure) is good, and this document keeps all of
it. The numbers and the fields are wrong against this codebase, because they were never read off
it. This document replaces the generator with one derived from what Emberfall actually contains:
`heroes.json`, `enemies.json`, `Enums.cs`, `Definitions.cs`, `StatMath.cs`, `balance.json`. Every
value and every constraint below is either a fact read from one of those files or a decision
recorded as a decision, and the two are never presented as the same kind of claim.

Four decisions are made in this document and treated as decided. None of them load today. Section
7 says exactly what has to be built first.

## 1. What a hero actually is, read off the six that exist

This section was written against the six original placeholder heroes - `orrin_kaal`,
`kalyx_emberwright`, `wren_ilyra`, `ryn_solvarr`, `maeve_corrin`, `nyx_serrow` - all six of which
have since been deleted ahead of the roster import. **The shape below is unchanged and is still
what `heroes.json` holds**; only the names it was read off are gone. Every hero in `heroes.json`
has the same shape:

- One `id`, `name`, `title`, `lore` string, `role`, `aspect`, `rarity`, an optional
  `startingHero` flag.
- Four base stats (`hp`, `attack`, `defense`, `speed`), `critChance`, `critDamage`, `growth`,
  `startEnergy`.
- A `visual` block (colours, silhouette, weapon, scale, sigil, an optional sprite id).
- Exactly one `basic` attack, exactly one `active` ability, and **exactly one `passive`**.
  `UnitDef.Passive` in `Definitions.cs` is a single object, not a list, and every one of the six
  heroes authors exactly one. There is no talent field anywhere in `HeroDef` or `UnitDef`. A
  generator that produces three passives and a talent block is inventing a schema this game
  doesn't have.
- Each ability is a short ordered list of effects (one to three per ability across the whole
  roster; nobody stacks more than that), each effect one `EffectKind`, one `TargetMode`, a
  `power` multiplier against the caster's Attack, and optionally a `StatusKind` with a magnitude,
  duration and chance.

That's the whole shape. Section 5 lists the exact valid values for every enum a generated hero is
allowed to use.

## 2. Decision 1: stars replace rarity

**Decided.** Common, Rare and Epic go away. Heroes exist at 1 to 5 stars. 1 and 2 star heroes are
fusion fodder only, thrown away for currency, and are not real designed heroes; a different,
smaller authoring job than the rest of this document, and not this document's job either, flagged
again in section 8. 3 star heroes fuse into 4, 4 fuse into 5. Some 5 star heroes can't be fused
into and are summon-only, which is what makes them the good ones.

**What the code does today.** `StatMath.RarityMultiplier(Rarity rarity)` is a three-case switch
(`Common` 1.00, `Rare` 1.16, `Epic` 1.34) called once inside `StatMath.ForHero`, before the stat
formula runs. Generalising the switch itself to five cases instead of three is cheap; that part
of the mechanism carries the star tiers with almost no change.

**What actually costs something is where the value lives, and it moves to a different kind of
field, not just a bigger enum.** `Rarity` is authored once per `HeroDef`, in content: Orrin is
always Common, Nyx is always Epic, fixed forever by what's in `heroes.json`. But decision 4 says
a hero is authored once, at 5 stars, and a player's owned copy can sit at any star from 1 to 5
depending on how far they've fused it. That's not content-authored data any more, that's player
progression, the same shape `HeroInstance.Level` already is, not the shape `HeroDef.Rarity` is
today. Concretely:

- `HeroDef.Rarity` is deleted from content. There's nothing left for it to mean once every hero
  can exist at any star.
- `HeroInstance` (`Assets/Scripts/Core/Model/PlayerProfile.cs`) needs a new field for the owned
  star tier, next to `Level`. `HeroInstance.Copies`, which already exists and already tracks
  "duplicates pulled from summoning", is the natural home for banked fusion material; the star
  tier itself is a second number, the way `Level` and `LevelUpCost` are two different things.
- `StatMath.ForHero(HeroDef def, int level)` needs a third parameter, the star tier, since it can
  no longer read the multiplier off `def`.
- `HeroDef` needs the one new field the decision explicitly calls for: a boolean marking a hero
  as summon-only at 5 stars, unreachable through fusion. That one is genuinely a `HeroDef` field,
  since it's authored content (a fact about the hero), not player state.
- The duplicate-pull behaviour in `GameSession.SummonOne` (`existing.Copies++`, then an automatic
  gold and shard refund) has to stop auto-liquidating and instead bank into fusion material,
  which is a real behaviour change to code that already ships, not an addition next to it.
- The summon roll itself (`GameSession.RollHero`, `ContentDatabase.HeroesOfRarity`) is built
  around exactly three weighted buckets (`summonWeightCommon/Rare/Epic` in `balance.json`). Five
  tiers, with fodder at the bottom and signature-only heroes at the top, is a different pool
  shape, not a bigger version of the same three buckets.
- Every UI call site that reads `Rarity` today (`UITheme.RarityColor`, `RarityName`, `Stars`,
  `Widgets.Portrait`, `Widgets.StarRow`, `HeroDetailScreen`, `SummonScreen`, `CampaignScreen`,
  `MainMenuScreen`, `HeroesScreen`'s sort order) has to move to whatever field replaces it. One
  useful fact for whoever does that work: `Rarity`'s own enum values are already `Common = 3`,
  `Rare = 4`, `Epic = 5`, and `UITheme.Stars` / `Widgets.StarRow` already render a star count by
  casting the enum straight to an int. The display mechanism already thinks in stars. It's the
  meaning and the ownership of the underlying field that has to change, not the star rendering.

**This document's own proposal for the five multipliers**, since decision 1 sets the tier count
but not the numbers, chosen to keep the two tiers that already exist unchanged and extend
downward for the two new fodder tiers:

| Star | Multiplier | Note |
|---|---|---|
| 1 | 0.70 | fodder |
| 2 | 0.85 | fodder |
| 3 | 1.00 | the floor for a real hero, replaces Common |
| 4 | 1.16 | unchanged from today's Rare |
| 5 | 1.34 | unchanged from today's Epic |

Proposed, not decided elsewhere. Whether 1 and 2 star fodder feed the 3-to-4-to-5 fusion ladder
directly or exist purely as a currency source is not settled by the decision as given and is not
resolved here either, per section 8.

## 3. Decision 2: class, kept strictly separate from aspect

**Decided.** A new field, Class: Warrior, Assassin, Mage, Priest, Ranger. Class shapes stat
distribution and role only. **It never changes how an ability resolves.** Aspect already owns
that, per DESIGN-ASPECTS and DESIGN-WORLD: Ember escalates, Tide redirects, Verdant persists,
Gale disrupts sequencing, Umbra removes and conceals, Lumen marks and defines. The generator must
never produce a class-driven mechanic (a Priest ability that revives, an Assassin ability that
executes) because mechanic ownership is aspect's job, decided elsewhere, and Class doesn't get a
second vote.

**Worth naming plainly:** the game already has an enum that does almost exactly this job.
`HeroRole` (`Bulwark`, `Striker`, `Breaker`, `Mender`, `Warden`, `Stalker`) is documented in
`Enums.cs` as "purely descriptive, behaviour comes from ability data", the same boundary Class is
being given here. The five Class names and the six Role names aren't the same list and don't map
one-to-one (`Warrior` reads as `Bulwark`, `Mage` as `Breaker`, `Priest` as `Mender`, and both
`Assassin` and `Stalker`/`Striker` want the same slot, while `Ranger` and `Warden` don't correspond
to anything in the other list at all). Whether Class is meant to replace Role or sit alongside it
as a genuinely separate axis is not decided here; the decision as given only says Class is new and
separate from Aspect, not what it does to Role. Recorded as a real, open design question, not
resolved by generating heroes around it.

**Code cost, and it's genuinely small.** Neither `Role` nor the proposed `Class` field drives any
branch in `StatMath` or `CombatSimulation` today; a hero's stats are freely authored numbers with
no per-role formula. Adding `class` as a parsed, stored, displayed string on `HeroDef` is the same
shape of change `role` already is: a field read in `ReadCommon`, shown in the UI, touching no
combat code. The real work in this decision is design work (the stat ranges in section 4), not
code work.

## 4. Decision 3: the whole stat scale moves up by ten

**Decided, and the one to get right.** HP and Attack should read in the thousands and tens of
thousands, the way the rest of the genre does. HP, Attack, and every flat damage or heal value in
an effect are linear and scale cleanly: multiply the number by ten and the game behaves exactly
as it did before, because nothing else in the formula reads those numbers except as one term in a
product or a sum.

**Defence is not linear, because it doesn't sit in a product, it sits inside a ratio.**
`CombatSimulation.ApplyDamageEffect` computes mitigation as:

```
mitigation = defense / (defense + defenseConstant + defensePerLevel * attackerLevel)
raw *= (1 - mitigation)
```

Scale `defense` by ten and leave `defenseConstant` (400) and `defensePerLevel` (24) where they
are, and the ratio itself moves, not just the number. Concretely, using Orrin's base defence
(130) at level 1 against a level-1 attacker:

- **Today:** `130 / (130 + 400 + 24) = 130 / 554 = 0.235`. About 23.5% of raw damage is mitigated.
- **Scale defence alone, ×10, leave the constants:** `1300 / (1300 + 400 + 24) = 1300 / 1724 =
  0.754`. About 75.4% mitigated. A tank that took 23% off every hit now takes 75% off every hit,
  for no reason anyone decided; that's the source prompt's own mistake, restated with this
  game's actual constant instead of a made-up one (it proposed 1,700 defence against a
  defenceConstant of 400, mitigation `1700 / 2124 = 0.80`, the same failure).
- **Scale defence, `defenseConstant` and `defensePerLevel` together, all ×10:** `1300 / (1300 +
  4000 + 240) = 1300 / 5540 = 0.235`. Identical to today, because multiplying every term in
  `defense + defenseConstant + defensePerLevel * level` by the same constant `k` leaves
  `defense / (defense + defenseConstant + defensePerLevel * level)` unchanged; it's the same
  fraction with numerator and denominator both scaled by `k`. This is the whole rule: **defence
  only scales safely if `defenseConstant` and `defensePerLevel` scale with it, by the same
  factor, in the same change.**

**What moves, and what doesn't.**

| Scales ×10 | Stays exactly as-is |
|---|---|
| `hp`, `attack`, `defense` on every hero and enemy | `speed` (turn order is relative comparison, not a currency) |
| `defenseConstant`, `defensePerLevel` in `balance.json` | `critChance`, `critDamage` (already unitless ratios) |
| Any nonzero `effect.flat` value (none exist in shipped content today, but the field is live and any future one must be authored at the new scale) | `effectHit`, `effectResist` (flat 0 to 1 pass-throughs per `StatMath`'s own comment, unrelated to the HP/Attack/Defense economy) |
| | `damageVariance`, `speedVariance` (percentages, scale-invariant) |
| | The star multipliers in section 2, and the aspect chart's 1.20/0.85 (ratios, scale-invariant) |
| | Gold and shard economy constants (`levelCostA/B/C`, idle rates, summon cost); a different economy, untouched by this change |

**Every file this actually touches.** Six heroes' `hp`/`attack`/`defense` in `heroes.json` (18
numbers). Eight enemies' `hp`/`attack`/`defense` in `enemies.json` (24 numbers).
`defenseConstant` and `defensePerLevel` in `balance.json`, moved together, in the same change,
never one without the other. And `campaign.json`'s twelve `recommendedPower` values, which are
not a clean ×10 of today's numbers: `StatMath.PowerScore` weights `MaxHp * 0.11 + Attack * 2.4 +
Defense * 1.35 + Speed * 1.9 + crit terms`, and since `MaxHp`/`Attack`/`Defense` scale but
`Speed` and the crit terms don't, the weighted sum doesn't move by a single clean multiplier. The
twelve numbers need to be recomputed from the new stats, not multiplied by ten and assumed
correct. `PowerScore`'s own weighting coefficients don't need to change; the formula is fine,
only its inputs move, which is exactly why the output has to be re-measured rather than
recalculated by hand.

**This is the twelve-stage win rate table, invalidated a fourth time.** DESIGN-BUILD-ORDER
already tracks three changes that each invalidate it on their own (team size 3 to 5, Effect Hit
and Effect Resist, the aspect identities) and states plainly that task 14, the full retune, is
the only step that produces a valid table, with nothing tuned before it meant to be read as
final. A ×10 stat rebase belongs in exactly that same pass, for exactly the same reason: tuning
`campaign.json` against numbers that are about to change again is tuning against content that
won't exist by the time it ships.

## 5. Decision 4: heroes are authored at 5 stars

**Decided.** A hero's `hp`, `attack`, `defense`, `speed`, `critChance` and `critDamage` in
`heroes.json` describe the 5-star version. A 3-star or 4-star owned copy of the same hero is that
same authored block scaled down by the star multiplier in section 2, computed at read time by
`StatMath`, the same way rarity is a multiplier applied at read time today rather than baked into
six separately-authored stat blocks per hero. Nothing new to build once section 2's plumbing
exists: this decision is free once the star field lives on `HeroInstance` instead of `HeroDef`,
since "scale the one authored block by a per-owned-copy multiplier" is the exact mechanism decision
1 already requires. There is no separate 3-star, 4-star and 5-star JSON to author for the same
hero. One block, one hero, five possible read-time multipliers.

## 6. What a generated hero is allowed to use

Every value below is copied directly from `Enums.cs`. A generated hero that uses anything not on
these four lists does not load, or loads with a value silently coerced to a default, because
nothing in the simulation resolves it.

**EffectKind** (what an effect does): `Damage`, `Heal`, `Shield`, `ApplyStatus`, `Cleanse`,
`EnergyGain`.

**TargetMode** (who an effect hits): `Self`, `LowestHpEnemy`, `HighestHpEnemy`,
`HighestAttackEnemy`, `FrontEnemy`, `RandomEnemy`, `AllEnemies`, `LowestHpAlly`, `AllAllies`,
`RandomAlly`, `LastAttacker`, `LastTarget`.

**StatusKind** (what `ApplyStatus` can apply): `AttackUp`, `AttackDown`, `DefenseUp`,
`DefenseDown`, `Burn`, `Regen`, `Shield`, `Taunt`, `Focus` (increased crit chance), `Slow`
(reduced speed). (`None` also exists, meaning no status; never author it on purpose.)

**PassiveTrigger** (when a passive fires): `BattleStart`, `TurnStart`, `AfterBasicAttack`,
`OnCriticalHit`, `OnKill`, `OnHpBelowThreshold`, `OnDamaged`. (`None` also exists, for a passive
with no trigger, which is how `Kalyx`'s `Kindling` is authored today, a standing bonus rather
than a triggered one.)

**Explicitly excluded, and why.** The source prompt invites revives, summons, damage reflection,
stat stealing, silence and executions. None of the six exist in this list, and none can be
authored by combining values that do; each one is a distinct kind of thing the simulation simply
doesn't have a branch for. A revive needs an `EffectKind` that resurrects a dead unit, and none
exists; healing in this engine has no defined behaviour against a dead target. A summon needs a
runtime path that creates a new combat unit mid-battle, and the sim only ever builds units once,
from the battle request, at the start. Damage reflection needs a status or effect that redirects
part of an attack back at its source; `Shield` absorbs, it doesn't reflect. Stat stealing needs a
mechanism that reads one unit's stat and transfers it to another; nothing in `ApplyStatus` does
that, it only applies a named, pre-defined status with a magnitude. Silence needs a status that
blocks ability use, and none of the ten `StatusKind` values do that, they only modify numbers or
apply `Taunt`/`Focus`/`Slow`. An execution needs a damage path that bypasses the normal formula
below some HP threshold, and every point of damage in the game goes through the same single
funnel with no such branch. Every one of the six would be a new `EffectKind`, meaning a new
branch in `CombatSimulation.ApplyEffect`, and that file is exactly where task 12 is currently
blocked, over which of two systems (relics, aspects) gets to claim its remaining hooks. Adding six
more speculative claims to a file already contested between two finished designs is the wrong
moment for a generator prompt to be making them. Excluded outright, not deferred.

## 7. What has to be built before a generated hero actually loads

In order, since later ones depend on earlier ones:

1. **The star field has to move from `HeroDef` to `HeroInstance`**, per section 2. Until it does,
   there's nowhere for a generated hero's star tier to live.
2. **The class field has to be parsed and stored**, per section 3. Cheap, but not automatic:
   unrecognised JSON keys are silently ignored by this project's loaders, so a `"class"` key in a
   generated hero's JSON today does nothing at all, good or bad, until `ReadCommon` reads it.
3. **The ×10 stat rebase has to land across every existing hero, enemy and stage**, per section 4,
   in the same pass as task 14. A hero generated at the new scale cannot be dropped into today's
   `heroes.json` as-is; it would fight six heroes and eight enemies still authored at the old
   scale, which is not a content bug, it's two different games sharing one save file.
4. **The fusion system has to exist**: banking duplicate pulls instead of auto-converting them to
   gold, a UI to spend banked copies on a star fuse, and the summon-only flag from section 2
   actually gating which 5-star heroes that fusion path can reach.
5. **The summon pool has to support more than three weighted tiers.**

None of the five exist today. Generated heroes are content waiting on a system, the same relation
DESIGN-CAMPAIGN-SHAPE already has to the aspect mechanics its own chapters lean on.

## 8. Flagged, not resolved

**1 and 2 star fodder heroes are content nobody has counted.** Section 2 names them as a separate,
smaller authoring job from a real hero, and this document, built for generating real heroes, does
not size that job, design fodder heroes, or decide whether fusing fodder feeds the 3-to-5 ladder
directly or only converts to currency.

**DESIGN-RECOLLECTION overlaps with what a star ladder does, and the two are not reconciled.**
DESIGN-RECOLLECTION, already on `main`, proposes a per-hero flat-stat progression track paid in
gold and a new material, sitting on top of level and rarity. A star tier that scales a hero's
entire stat block by a read-time multiplier is a second axis doing adjacent work: both are
per-hero power that isn't level, and both eventually want to be paid for out of the same gold
economy. Which one a player reaches for, and whether they end up stacking or duplicating each
other's job, is not decided by either document and is not decided here.

## 9. The generator prompt

Paste this whole section. Fill in only the two blanks: the aspect, and how many heroes to
generate.

---

You are designing new heroes for Emberfall, an original mobile idle RPG. Aspect is a belief, not
an element: Ember escalates (nothing changes without destruction), Tide redirects (nothing should
remain fixed), Verdant persists (what survives deserves to continue), Gale disrupts sequencing
(nothing should be held in place), Umbra removes and conceals (not everything should be known),
Lumen marks and defines (everything should have a knowable place). None of the six is evil and
none is the antagonist.

**Aspect for this batch:** ____________
**Number of heroes to generate:** ____________

For each hero, write a short design paragraph before any JSON: who they are, what one or two core
mechanics define them (never more than two; the six shipped heroes each carry one to three effects
total per ability and no hero does everything), and how their kit expresses the aspect's belief
above rather than a literal element (no flame for Ember, no water for Tide, no leaves for Verdant;
the belief, not the weather). Do not reuse a name, title, or core mechanic concept from any
existing hero: Orrin Kaal (Bulwark, Cinderguard/self-shield-and-taunt), Kalyx Emberwright
(Breaker, Cinder Bloom/AoE-burn), Wren Ilyra (Mender, Verdant Tide/heal-and-cleanse), Ryn Solvarr
(Striker, Piercing Gale/execute-lowest-HP), Maeve Corrin (Warden, Tidal Cadence/team energy and
buff), Nyx Serrow (Stalker, Umbral Step/double-strike-and-mark).

Assign a Class: Warrior, Assassin, Mage, Priest, or Ranger. Class sets stat distribution and role
only. It must never change how an ability resolves; that belongs to Aspect alone. Use these
level-1, 5-star baseline ranges, derived from the six shipped heroes at ten times their current
scale, as your stat budget:

| Class | HP | Attack | Defense | Speed | Crit Chance | Crit Damage |
|---|---|---|---|---|---|---|
| Warrior | 24000-30000 | 950-1150 | 1200-1450 | 82-95 | 0.05-0.10 | 1.4-1.5 |
| Assassin | 13000-15500 | 1950-2300 | 500-650 | 112-130 | 0.18-0.30 | 1.6-1.9 |
| Mage | 13500-15500 | 1750-2000 | 550-680 | 88-100 | 0.10-0.16 | 1.5-1.65 |
| Priest | 15000-17500 | 1300-1500 | 620-750 | 96-106 | 0.08-0.13 | 1.45-1.55 |
| Ranger | 14500-17000 | 1550-1800 | 600-750 | 100-115 | 0.12-0.18 | 1.5-1.65 |

(Ranger has no clean analog among the six shipped heroes; its range is interpolated between
Assassin, Mage and Priest rather than read directly off one hero the way the other four are. Treat
it as the least certain row.)

Give every hero exactly one basic attack, exactly one active ability, and exactly one passive.
Never write two passives, never write a talent block; this game's hero definition has room for
one of each and nothing else. Each ability is one to three effects, no more. Every effect must use
only these values, exactly as spelled, nothing else:

- **EffectKind:** Damage, Heal, Shield, ApplyStatus, Cleanse, EnergyGain.
- **TargetMode:** Self, LowestHpEnemy, HighestHpEnemy, HighestAttackEnemy, FrontEnemy,
  RandomEnemy, AllEnemies, LowestHpAlly, AllAllies, RandomAlly, LastAttacker, LastTarget.
- **StatusKind** (only inside an ApplyStatus effect): AttackUp, AttackDown, DefenseUp,
  DefenseDown, Burn, Regen, Shield, Taunt, Focus, Slow.
- **PassiveTrigger** (only on the passive): BattleStart, TurnStart, AfterBasicAttack,
  OnCriticalHit, OnKill, OnHpBelowThreshold, OnDamaged, or None for a standing bonus with no
  trigger.

Do not invent a revive, a summon, damage reflection, stat stealing, a silence, or an execution.
None of the six exist in this game and none can be built from the values above.

After the design paragraph, output the hero as JSON in this exact shape, matching an entry in
`heroes.json`:

```json
{
  "id": "lowercase_with_underscores",
  "name": "Display Name",
  "title": "A short title",
  "role": "one of Bulwark, Striker, Breaker, Mender, Warden, Stalker",
  "class": "Warrior | Assassin | Mage | Priest | Ranger",
  "aspect": "the aspect assigned above",
  "lore": "one to two sentences, specific, no generic fantasy filler",
  "hp": 0, "attack": 0, "defense": 0, "speed": 0,
  "critChance": 0.0, "critDamage": 0.0, "growth": 0.115, "startEnergy": 0,
  "visual": { "primary": "hex", "secondary": "hex", "accent": "hex", "silhouette": "humanoid | brute | beast | wisp | golem", "weapon": "sword | greatsword | spear | staff | dagger | orb | hammer | claws | none", "scale": 1.0, "sigil": 0 },
  "basic": { "id": "x_basic", "name": "", "description": "", "vfx": "impact | slash | burst | heal | buff | shadow | bolt", "effects": [ ] },
  "active": { "id": "x_active", "name": "", "energyCost": 100, "description": "", "vfx": "impact | slash | burst | heal | buff | shadow | bolt", "effects": [ ] },
  "passive": { "id": "x_passive", "name": "", "trigger": "", "description": "", "effects": [ ] }
}
```

`growth` stays 0.115 for every hero, matching every shipped hero exactly; it is not a design lever
for this pass. Leave `rarity` out entirely; it does not exist in this hero's field set once
decision 1 lands, and `class` and star tier take its place. `startingHero` still exists as a field
but is never true for a generated hero, so omit it and let it default to false.

---
