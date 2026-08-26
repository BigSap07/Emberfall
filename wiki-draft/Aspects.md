# Aspects

Source: `DESIGN-ASPECTS.md`, reconciled with `DESIGN-WORLD.md`, `DESIGN-HOOK-CONFLICT.md`, and
`DESIGN-HOOK-RESOLUTION.md`. **Status: implemented.** All six identities are wired into
`Assets/Scripts/Core/Combat/CombatSimulation.cs` and confirmed in this repo's current code and
content (verified independently while writing this page, not just trusted from the doc).

Originally an Aspect was only a colour and a damage multiplier (`AspectChart.Multiplier`, ×1.20 /
×0.85) read by almost nothing else in the codebase. Each aspect now owns one real rule in the
combat effect pipeline, chosen from what the existing hero kits were already doing rather than
invented fresh. The old matchup-wheel multiplier still exists as a secondary layer, narrowed to
**×1.10 / ×0.92** (see "The matchup wheel" below) so it doesn't out-argue the new identities.

## Ember — escalates

**Belief:** nothing changes without destruction.

**Mechanic:** Burn instances from Ember sources stack as separate entries instead of overwriting
each other — a second Ember application deepens the existing fire rather than replacing it. Ember
buys damage that lands later, on the victim's own turn. Hook: `CombatUnit.AddOrRefresh`.

**Fit:** Kalyx Emberwright's `Cinder Bloom` (AllEnemies damage + Burn) is clean Ember. His old
passive `Kindling` (+30% damage vs. burning targets) was a flat percentage bonus, exactly what the
design rule bans — **it has been cut** from `heroes.json`.

**Status:** implemented and content-clean — Kindling is gone.

## Tide — redirects

**Belief:** nothing should remain fixed.

**Mechanic:** energy never gets discarded. Overflow past `EnergyMax` is handed to the living ally
furthest from casting instead of being thrown away. Hook: `Sim.AddEnergy`, keyed on the *granter*
(a unit's own basic-attack/on-damage regen doesn't redirect — only energy explicitly granted by a
living Tide unit does).

**Fit:** Maeve Corrin fits completely — `Tidal Cadence` and `Rising Chorus` are already about
other people's cast timing, and she's the hero who most obviously wastes the old clamped system.

**Status:** implemented. The competing relic **Tidewrought Metronome** (which wanted the same
clamp) was **cut outright**, not relocated — see [Open Questions](Open-Questions.md) and
`DESIGN-HOOK-RESOLUTION.md`.

## Verdant — persists

**Belief:** what survives deserves to continue.

**Mechanic:** healing/warding with nowhere to go banks forward as a `Regen` status instead of being
clamped away. Hook: `Sim.ApplyHealValue`'s early return on `missing <= 0`.

**Fit:** Wren Ilyra's `Verdant Tide` (heal AllAllies) is Verdant, but two of her three kit pieces
aren't: the `Cleanse` on the same ability is anti-Umbra, and `Rootbound` (a Shield at battle start)
is Lumen's rule. She reads as a healer with a protection kit stapled on.

**Status:** implemented. The competing relic **Green Hour Vessel** was also cut, for the same
reason as Metronome — the `Shield` effect handler was found to have no discard to intercept at all,
so there was nowhere to relocate it to.

## Gale — disrupts sequencing

**Belief:** nothing should be held in place.

**Mechanic:** Gale manipulates initiative directly. A Gale unit that lands a kill re-enters the
current round's turn order; Gale's `Slow` pushes a target later within the *same* round rather than
just the next one. Hook: `Sim.TurnOrder()`/`Execute`'s turn loop, and the existing
`PassiveTrigger.OnKill` call site in `DealRawDamage`'s death branch (which nothing used before this).

**Fit:** Ryn Solvarr's targeting (`LowestHpEnemy` on both attacks) is perfect Gale-as-finisher, but
her old passive `Follow-Through` triggered `OnCriticalHit` and granted flat energy — a Tide-shaped
passive on a Gale hero, the clearest cross-wiring in the roster. **Fixed**: the trigger is now
`OnKill`. (The effect itself is still `EnergyGain`, Tide's verb — the doc notes this only partially
un-cross-wires her.)

**Status:** implemented, with a guard: a re-entering Gale unit could otherwise shed its own
statuses at double rate, so status expiry is now once-per-round-per-carrier rather than
once-per-turn. Also capped per unit per round (`galeExtraTurnsPerRound`) to stop a single Gale unit
from resolving an entire round unilaterally at team size 5 — measured and confirmed necessary
during implementation. At team size 3 this was safe without a cap; at 5 it required one.

## Umbra — removes and conceals

**Belief:** not everything should be known.

**Mechanic:** harmful statuses applied by an Umbra source **cannot be cleansed**. (An earlier
version also made them not tick down while the applier lived — `DESIGN-WORLD` judged that half
**wrong**, since "doesn't expire" is persistence, which is Verdant's word, not Umbra's. That clause
was cut; only uncleansable survived.) Hook: `Sim.ExpireStatuses`, the `Cleanse` branch of
`ApplyEffect`, `AddOrRefresh`.

**Fit:** Nyx Serrow fits well — she targets `HighestAttackEnemy` and applies uncleansable
`DefenseDown`. One stray: her `Focus` self-buff on `Umbral Step` is a crit-chance bonus, which is
Gale's axis, not Umbra's — left as-is, noted as the one line of her kit that would logically move.

**Status:** implemented (uncleansable-only version). Weakens at team size 5 — see
[Combat Systems](Combat-Systems.md) and [Open Questions](Open-Questions.md) for the
still-undecided team-size-5 dilution question.

## Lumen — marks and defines

**Belief:** everything should have a knowable place.

**Mechanic:** originally specified as "Lumen intercepts" (absorption-first). `DESIGN-WORLD`
**re-weighted this**: the actual identity is that a living Lumen unit answers *every* single-enemy
targeting question aimed at its side — it outranks even a non-Lumen ally's Taunt. Absorption is
just what happens once you're always the answer, not a separate rule. Hook: `Sim.ResolveTargets`'
`FindTaunter` override (renamed `FindTargetAnchor`), which needed no new code for the absorption
half — the Shield-absorb block in `DealRawDamage` already worked unconditionally.

**Fit:** Orrin Kaal was originally labelled Ember with a kit of Shield/Taunt/DefenseUp — the Lumen
rule three times over. **He has been relabelled** from `"aspect": "Ember"` to `"aspect": "Lumen"`
in `heroes.json` (confirmed in the current content file) — a one-string content change, kit
untouched. This activated the Lumen↔Umbra matchup pair for the first time in the game (previously
inert with zero Lumen units). A predicted difficulty spike on the Umbra-heavy tutorial stage was
measured and **did not appear** — the stage actually got easier, because Orrin's routing benefit
(enemy hits anchor onto him) outweighed the multiplier penalty.

**Status:** implemented, built last as planned (it was considered the least-trustworthy identity,
derived by elimination rather than read off an existing kit). Orrin is currently the only Lumen
unit in the game on either side.

## The matchup wheel

Kept as a secondary layer rather than replaced (replacing it would have deleted shipped work across
three files). Narrowed from ×1.20/×0.85 to **×1.10/×0.92** so the six new identities aren't
drowned out by a single colour-vs-colour roll. One coupled constant had to move with it:
`Debrief.AspectFactorThreshold` (was 1.2, which would have silently stopped the results-screen
"you were the wrong colour" clause from ever firing again at the new 1.10 multiplier).

## Roster fit summary

Per `DESIGN-ASPECTS` section 3: of the six starting/roster heroes, only **Maeve (Tide) was a clean
fit** from the start. Two were near-fits with one stray line (**Nyx**, **Kalyx**). Three needed a
relabel or a passive fix (**Orrin** relabelled to Lumen, **Ryn**'s passive trigger fixed, **Wren**
still split across Verdant/anti-Umbra/Lumen with no plan to un-split her). See
[Heroes](Heroes.md) for the per-hero detail and current kit data.
