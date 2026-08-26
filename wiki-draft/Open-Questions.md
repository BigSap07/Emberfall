# Open Questions

Everything across the `DESIGN-*.md` docs marked undecided, flagged, parked, or needing a human
decision, grouped by topic. This is meant to be a running list — update it as docs resolve items or
new ones get flagged.

## World / fiction

- **What shattered the First Light.** Explicitly and permanently open — may simply have ended. Do
  not resolve in a chapter. See [The World](The-World.md), DESIGN-WORLD.md §4.
- **Whether the aspects are conscious**, or just behave consistently. Undecided by design.
  DESIGN-WORLD §4.
- **Whether a recalled hero is the person or a copy of their pattern.** DESIGN-WORLD §4.
- **Who else holds or held the office of Keeper**, and whether any are hostile. Unwritten.
  DESIGN-WORLD §4.
- **What relics are fragments of**, if not the First Light. Named as a deliberate clue, not
  answered. DESIGN-WORLD §4.
- **Whether the First Light can or should be restored.** No win condition is implied. DESIGN-WORLD
  §4.
- **What the Hollow are** — an aspect's work, a byproduct of the Emberfall, or people who lost
  their pattern. The content already uses the word like a faction (`hollow_husk`, "the Hollow
  Archive"), but this is explicitly flagged as undecided rather than left quiet. DESIGN-WORLD §4;
  reiterated in DESIGN-CAMPAIGN-SHAPE §6/§9 and DESIGN-DEEP-RECITAL §6 as still open.
- **Whether shards are literally First Light fragments.** Suggestive in the content (summoning
  currency, Shardfall Verge, a stage line about a shard "standing up"), explicitly not canon.
  DESIGN-WORLD §4.
- **Whether a hero can hold more than one aspect.** Undecided in fiction and currently impossible
  in code (`UnitDef.Aspect` is a single enum). DESIGN-WORLD §4.
- **Whether anywhere exists outside the six named regions.** Unwritten. DESIGN-WORLD §4.

## Campaign structure

- **Which belief wins Chapter 3's contest** (proposed: Verdant vs. Ember), and whether that boss is
  Verdant- or Ember-aligned. Not decided by DESIGN-WORLD or DESIGN-CAMPAIGN-SHAPE.
  DESIGN-CAMPAIGN-SHAPE §10.
- **Whether `sundered_colossus` is reused as the Chapter 5 boss or replaced.**
  DESIGN-CAMPAIGN-SHAPE §6/§10.
- **The relic/aspect hook conflict being upstream of all six aspects being real in a fight** — see
  Systems/code section below; DESIGN-CAMPAIGN-SHAPE §10 calls this out as blocking the chapter
  fiction actually being felt in play. (Note: per [Aspects](Aspects.md), the aspects themselves are
  now implemented — re-check whether this specific campaign-shape dependency is still live.)
- **Chapters 2–6 don't exist yet** — `campaign.json` currently has Chapter 1 only, so the
  contested-region proposal and chapter-specific questions above are moot until they're authored.
  DESIGN-BUILD-ORDER "Undecided, needs a human".

## Systems / code (effect pipeline)

- **Umbra at team size 5 — dilution.** Umbra's "subtract the enemy that matters" rule is a third of
  the enemy line at team size 3 but only a fifth at 5, weakening as the only aspect that does. Two
  options on the table: a spread-on-death rule, or explicitly accepting Umbra as a boss/elite
  specialist (where team size doesn't matter, since there are only 1–2 enemies anyway). **Decide
  when task 12 reaches Umbra, not before.** DESIGN-ASPECTS §8; DESIGN-BUILD-ORDER "Undecided, needs
  a human".
- **Opening Gambit — explicitly cut**, not just deferred. A mandatory per-battle choice doesn't fit
  an idle/auto-battle game (fights resolve unattended, there's no moment to surface a choice).
  Reframings (standing per-stage loadout, pre-stage modifiers) were considered and rejected as not
  worth building at this stage. Do not silently re-add without revisiting whether the core loop
  changed. DESIGN-BUILD-ORDER "Undecided, needs a human".

## Relics

- **What replaces Tidewrought Metronome and Green Hour Vessel**, the two relics cut outright when
  Tide and Verdant claimed their hooks. `DESIGN-HOOK-RESOLUTION` names open hook space (the
  `_rng.Chance` gate on `ApplyStatus`, already partly claimed by Splitting Prism's new cost) but
  does not design replacements — that's separate, unstarted work. DESIGN-HOOK-RESOLUTION §6.
- **Where the relic shop screen lives** — hero detail, a new menu tab, or a dedicated shop entry.
  Not decided. DESIGN-RELIC-PROGRESSION §1.
- **Whether an already-owned relic can be re-purchased as a second copy** for a different tier-2
  path, or is simply greyed out. Not decided; the tier model doesn't require an answer either way.
  DESIGN-RELIC-PROGRESSION §1.
- **Specific tier-2 relic values.** Deliberately deferred to an actual balance pass rather than
  anchored to a design doc. DESIGN-RELIC-PROGRESSION §2.

## Splinters (proposed Aspect-fragment items)

Two genuinely open questions the doc explicitly declines to answer:

- **Acquisition path** — found, earned, summoned, or purchased; whether they have a rarity tier.
- **Leveling** — whether Splinters level at all, what resource drives it if so, and whether
  leveling changes *which half* of a two-part rule applies (e.g. the proposed Umbra Splinter) or
  only its magnitude. Also open: how a player learns what a Splinter does before equipping it
  (subject to the same no-percentage-in-description rule as relics).

DESIGN-SPLINTERS "Acquisition and leveling".

## Hero generation / progression overlap

- **1 and 2-star "fodder" heroes** are named as a smaller, separate authoring job but not sized,
  designed, or decided — including whether fusing fodder feeds the 3→4→5 star ladder directly or
  only converts to currency. DESIGN-HERO-GENERATOR §8.
- **Star-tier progression vs. DESIGN-RECOLLECTION's flat-stat track are not reconciled.** Both are
  per-hero power systems layered on top of level, both eventually draw from the same gold economy,
  and which one a player reaches for (or whether they end up stacking/duplicating each other) is
  not decided by either document. DESIGN-HERO-GENERATOR §8.
- **Whether "Class" is a genuinely separate axis from Aspect/Role** is not decided.
  DESIGN-HERO-GENERATOR §7 area (near line 118).

## Art / UI

- **The sprite pipeline has no painted-in dynamic lighting** the way procedural character rigs get
  from the engine — a real gap between what the art bible describes and what the loader currently
  does. Flagged as an open question for whoever owns the art pipeline, not something each artist
  should re-solve per hero. DESIGN-ART-DELIVERY (near line 244).
- **No established size cap or compression setting for background/hub art** — the character
  pipeline has one, backgrounds never needed one before. Genuinely open. DESIGN-HUB-DELIVERY (near
  line 115).
- **Whether the hub sits in an aspect-specific region** (and can therefore read as ash-and-ember,
  etc.) is a separate, still-open question. DESIGN-HUB-DELIVERY §7 area (near line 185).
- **The battle screen's text-outline treatment** may want its own visual tier or a different
  technique (soft backing plate vs. outline) rather than being grouped with the rest of the UI —
  flagged, not resolved. DESIGN-UI-THEME "Flagged for a human, not decided here".
- **The campaign screen's locked-stage hatch alpha (0.22)** is far lighter than every other
  locked/disabled state in the project (0.72) — could be intentional (texture is doing the work) or
  could be drift making locked stages look available. A judgement call, not a migration one.
  DESIGN-UI-THEME "Flagged for a human, not decided here".

## Resolved (kept for history)

These were flagged as unresolved in earlier docs and have since been decided — listed here so
nobody re-litigates them:

- **The relic/aspect hook conflict** (4 collisions: Tide vs. Tidewrought Metronome, Verdant vs.
  Green Hour Vessel, Kalyx's Kindling vs. Splitting Prism, Orrin's aspect vs. Warden's Debt) — all
  four resolved in `DESIGN-HOOK-RESOLUTION.md`: aspects keep both overflow hooks, both competing
  relics cut, Kindling cut, Orrin relabelled to Lumen, Warden's Debt survives with updated
  justification text. See [Aspects](Aspects.md) and [Heroes](Heroes.md).
- **The Kalyx Kindling issue** — his `+30%` vs.-burning passive was a flat percentage bonus the
  project's own no-percentage rule forbids. Cut; confirmed absent from current `heroes.json`
  (`"passive": null`). DESIGN-ASPECTS §3, DESIGN-HOOK-RESOLUTION §4.
