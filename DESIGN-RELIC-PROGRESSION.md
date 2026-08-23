# Emberfall — relic progression

Relic mechanics (what each relic does, how hooks fire in the effect pipeline, how the simulation
stays a pure function) are decided and shipped. See DESIGN-RELICS and DESIGN-HOOK-RESOLUTION.
This document covers everything else: how the player acquires relics, how relics grow, and how
the inventory and assignment model works. None of this exists in code yet.

The four relics in `relics.json` are: **Splitting Prism**, **Warden's Debt**, **Stormlance Oath**,
**Verdict Sigil**. (Tidewrought Metronome and Green Hour Vessel were cut; see
`relics.json` comment and DESIGN-HOOK-RESOLUTION sections 2 and 3.)

---

## 1. Acquisition

### Relic Dust

**Relic Dust** is a new resource. It drops from repeat-clearing stages — stages the player has
already cleared at least once, replayed for continued reward.

The repeat-clear mechanic that governs this is **DESIGN-BUILD-ORDER task 5**, not yet built.
When task 5 ships it will add `dailyResetUnix` and `stageClearsToday` to `PlayerProfile`, and
`repeatFullClearsPerDay` (3, per DESIGN-BUILD-ORDER's "Numbers already decided" section) to
`balance.json`. Those fields do not exist in either file today. Relic Dust is acquired through
that loop once it exists; it does not introduce a separate mode or a parallel repeat system.

**Relic Dust drops only from repeat-clears, not from first clears.** First-clear rewards are
already designed and tuned; adding Relic Dust to them would require rebalancing them. Repeat
clears are the grind loop and the natural home for a resource meant to be accumulated over time.

**Drop amount scales with stage difficulty.** Later stages, which are harder to field a team
against, drop more Relic Dust per clear. The specific curve is a balance pass, not a design
decision; the same deference-to-tuning convention DESIGN-BUILD-ORDER applies to task 14 applies
here.

### No stage-gating for now

Chapter 1 is the only chapter that exists. Gating relic shop access behind a chapter-2 clear
would lock every current player out entirely. The gating question should be revisited once
chapter 2 exists and there is a real second boundary to gate against. Until then, any cleared
stage generates Relic Dust on repeat and the shop is accessible from the start.

### Relic shop screen

Relic Dust is spent at a new **`RelicShopScreen`**, accessible from a location to be decided
(hero detail, a new menu tab, or a dedicated shop entry — not decided here).

The shop lists the current relic pool at a Relic Dust cost each:

| Relic | Paired hero |
|---|---|
| Splitting Prism | Kalyx Emberwright |
| Warden's Debt | Orrin Kaal |
| Stormlance Oath | Ryn Solvarr |
| Verdict Sigil | Nyx Serrow |

Costs are a balance pass. Relative costs may differ (a relic that is broadly useful versus one
that is narrow) but that is for tuning to determine.

Buying a relic adds it to the player's relic inventory in an unassigned state. It is not
automatically equipped to its paired hero; pairing is flavour and a suggested starting point, not
a constraint. The player assigns it explicitly.

A relic the player already owns should be visible in the shop as owned but not re-purchasable.
Whether the shop then offers a second copy (for a different tier-2 path) or simply greys it out
is not decided here; the tier model below does not require a second copy.

---

## 2. Tiers

Each relic has **two tiers**.

**Tier 1** is the version already shipped in `relics.json` and `claude/relics-rework`. No
changes to those definitions; tier 1 is the baseline.

**Tier 2** is a stronger version of the exact same rule — the hook is identical, only its
magnitude, reach, or threshold changes. It is not a separate item. The player upgrades an
already-owned relic to tier 2 by spending additional Relic Dust on it in `RelicShopScreen`.

**Specific tier-2 values are not decided here.** They are a balance pass, in the same way that
`recommendedPower` values and drop-rate curves across campaign stages are deferred to
DESIGN-BUILD-ORDER task 14 rather than fixed in a design document. Writing numbers here would
anchor the balance pass to a document rather than to actual playtesting.

Two examples of what "stronger version of the exact same rule" means in practice, stated without
committing to numbers:

- **Stormlance Oath** tier 1: all `LowestHpEnemy` targeting becomes `HighestHpEnemy`. Tier 2
  might extend the retargeting to a second targeting mode as well, or might not — the point is
  that the rule stays about targeting priority, not that a second stat appears.
- **Verdict Sigil** tier 1: `AfterBasicAttack` passives also fire after an Active. Tier 2
  might broaden the trigger window further, or might not.

These are illustrations, not decisions. The balance pass owns the tier-2 definitions.

---

## 3. Inventory and assignment

### The owned-but-unassigned state

A relic must be able to exist without being assigned to any hero. The current save structure does
not represent this: `HeroInstance.RelicId` bakes a relic's identity directly onto a hero
instance, which means a relic can only exist as "equipped to hero X" and has no independent
existence in the save.

The new model requires a **separate owned-relics collection** at the profile level:

```
PlayerProfile
  ownedRelics: [ { relicId, tier }, ... ]   // one entry per owned relic
  heroes[i].equippedRelicId: string | ""    // references ownedRelics, or empty
```

An owned relic with no hero's `equippedRelicId` pointing at it is unassigned. A hero with an
empty `equippedRelicId` has no relic equipped. Both states are valid and must be representable
simultaneously.

The field on `HeroInstance` should be renamed from `relicId` to `equippedRelicId` (or similar)
at the same time, to make the reference nature explicit in the serialised JSON.

### Free reassignment

A player can assign an owned relic to any hero and reassign it to a different hero at will.
No cost, no cooldown. Moving a relic between heroes is a team-building decision, not a resource
expenditure. The assignment is purely presentation in the hero detail and banner screens; it has
no save-version consequence of its own.

**Only one hero can hold a given relic at a time.** Assigning it to hero B unassigns it from
hero A. `relicSlotsPerHero` remains 1, per `balance.json`. No multi-relic builds.

### Save version bump

`SaveSerializer.CurrentVersion` is 3. Adding `ownedRelics` to the profile and changing
`relicId` to `equippedRelicId` on `HeroInstance` is a breaking change to the save format:

- A version-3 save read by version-4 code has no `ownedRelics` array and no
  `equippedRelicId` field. The upgrade path is: read `relicId` off each hero instance and
  synthesise an `ownedRelics` entry for each non-empty one, at tier 1, marking that hero as
  equipped. Heroes with no `relicId` contribute nothing. A player who had a relic equipped keeps
  it equipped at tier 1; a player with no relics starts with an empty collection.
- A version-4 save read by version-3 code would silently misread `equippedRelicId` as empty
  and lose all relic assignments, which is the direction the version guard prevents. Older builds
  refuse saves above their `CurrentVersion`; this bump ensures they do.

**This save format change bumps `CurrentVersion` from 3 to 4.**
