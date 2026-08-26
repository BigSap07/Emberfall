# Emberfall — relics

⚠️ **Needs a full rewrite. The standard on the line below is withdrawn.** DESIGN-BUILD-ORDER's
"One conflict to resolve, not silently pick" records a deliberate reversal, decided 2026-08-26:
relics move to a **two-tier model** — a common tier of flat and percentage stat relics that never
touch the simulation and live in `StatMath` / `UnitStats`, and a rare legendary tier of
rule-breaking relics that may not compete for a hook an aspect owns and are never paired to a
specific aspect. The no-percentage rule stated immediately below is the specific thing that was
reversed. Everything in this document still accurately describes what is **built** today, so read
it as a record of that, not as the target. The rewrite is its own task and has not been done.

A relic changes a **rule** in the effect pipeline. No relic contains an attack percentage.

## What is actually modifiable

`ResolveAbility` walks an ability's `EffectDef` list; for each one it calls
`ResolveTargets(caster, effect.Target)` and then `ApplyEffect` per target. Every hook below is an
existing branch in that path:

- **`effect.Target`** — rewrite a `TargetMode` before targets resolve.
- **`effect.Hits`** — the repeat loop on `Damage`.
- **`effect.Chance`** — the `_rng.Chance` gate on `ApplyStatus`.
- **`effect.Status` / `effect.Duration`** — substitute or extend what lands.
- **The heal clamp** — `ApplyHealValue` returns early when `missing <= 0`; overflow is discarded.
- **The energy clamp** — `AddEnergy` clamps to `EnergyMax`; the excess is discarded.
- **`PassiveTrigger`** — which condition fires a passive.
- **`AspectChart.Multiplier`** — which Aspect the wearer counts as.

The two discard paths are the richest, because today they silently throw value away.

## Four relics

Two of the six relics designed here were cut during the implementation pass. Their entries below
carry a **Cut** note and the reason. The four that shipped are Splitting Prism, Warden's Debt,
Stormlance Oath, and Verdict Sigil; see `relics.json` for their final hook parameters.

**Splitting Prism** — Damage effects aimed at `AllEnemies` retarget to `FrontEnemy`, with `Hits`
multiplied by the number of enemies they would have struck. *Kalyx.* Cinder Bloom stops being a
board wipe and becomes a drill. The cost is real: the Burn no longer spreads, and every enemy that
would have been burning is left unscorched. (`Kindling`, originally named here as a second cost
because the relic would starve its own passive, was cut during the Ember implementation — see
DESIGN-ASPECTS §3 and §6. The downside is now purely about spread lost.)

**Warden's Debt** — `Shield` effects targeting `Self` instead target the lowest-HP ally.
*Orrin.* Cinderguard's ward leaves the tank and lands on Ryn or Kalyx. Orrin becomes a protector
who protects nobody but the line, and drops below half health faster — which is where Emberskin
wants him. Deliberately double-edged.

**Green Hour Vessel** — healing above a target's missing HP becomes a ward instead of being
discarded. *Wren.* Verdant Tide on a healthy line is currently worth nothing; with this it is a
team-wide ward, and Wren stops being a reactive button.

**Cut.** Verdant's identity claims the same early return in `ApplyHealValue`, banking overflow to
`Regen` instead of a ward. One hook, one wearer (Wren), and the aspect owns it. See
DESIGN-HOOK-RESOLUTION §3.

**Stormlance Oath** — the wearer's `LowestHpEnemy` targeting becomes `HighestHpEnemy`. *Ryn.*
Lance Jab and Piercing Gale stop cleaning up chaff and start opening the Sundered Colossus. She
loses her finisher role entirely; against three minions she is now the worst pick on the banner.

**Tidewrought Metronome** — `EnergyGain` the wearer grants is not clamped at 100; the excess
banks toward the next cast. *Maeve.* Tidal Cadence plus Rising Chorus wastes energy constantly at
cap. Banked, the line can cast on consecutive rounds. Pure tempo, zero stats.

**Cut.** The Tide identity claims the same discard in `AddEnergy`, redirecting overflow to the living
ally furthest from casting. One hook, one wearer (Maeve), and the aspect owns it. See
DESIGN-HOOK-RESOLUTION §2.

**Verdict Sigil** — `AfterBasicAttack` passives also fire after an Active. *Nyx.* Hollowing
currently skips every turn she casts Umbral Step, so her best turn is her least debilitating.
With the Sigil, casting also marks.

## Equipping

**One slot, per hero, on the hero detail screen** beside the level and banner-slot rows. Not per
team: a team-wide relic is an aura, an aura is a global multiplier, and a global multiplier is
the stat stick this design exists to avoid. Per-hero is also what makes the pairing legible —
the relic is part of that hero's sentence, and moving it changes two heroes at once.

## Entering the simulation

`BattleRequest.Team` is a `List<HeroInstance>`. `HeroInstance` gains `RelicId`; `Sim` resolves it
to a `RelicDef` on the `CombatUnit` during setup, and the hooks are read at the existing
resolution points. No new state, no clock, no ambient lookup — a relic is data on the request,
exactly like a hero's level. `Run(request)` remains a pure function of `(content, team, stage,
seed)`.

One honest caveat: relics change how many RNG draws a battle consumes, so the same seed with a
different relic is a different battle. Determinism is per-request and always was; the README's
replay guarantee is unaffected as long as the relic id is part of the request.

## Presentation

Relics announce themselves through the **existing** `PassiveTrigger` event, with the relic name
in `AbilityName` — the battle screen already banners that as "NAME — PASSIVE". **No new event
type**, which keeps this clear of the Core event work in flight. Emit one the first time a relic
alters an effect in a fight, not on every tick.

Statically, the hero detail card states the rule in rule language — *"Wasted healing becomes a
ward"* — never a percentage. If a relic cannot be written as one sentence without a number, it is
a stat stick and should be cut.

## New fields

**Content** — `Assets/Resources/Content/relics.json` and a `RelicDef`: `id`, `name`, `rule`
(display text), `hook` (enum), and a small `params` block per hook (`fromTarget`/`toTarget`,
`fromTrigger`/`toTrigger`). No change to `heroes.json` or `enemies.json`.

**Save** — `HeroInstance.RelicId`, written into the existing `heroes` array beside `uid`/`defId`.
⚠️ **Save version bump.** Relics shipped in a separate pass from the team-size change: the save
went 1 → 2 for `PlayerProfile.Team` growing to 5 slots, then 2 → 3 when `HeroInstance.RelicId`
landed. `SaveSerializer.CurrentVersion` is 3. Missing `relicId` reads as empty.

**Balance** — `relicSlotsPerHero` (1). `relicEnergyBankMax` is absent from `balance.json` on
purpose: it bounded the Tidewrought Metronome's carry, and that relic is cut.
