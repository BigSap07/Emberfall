# Emberfall — Hero Fusion

Fusion is the crafting path for obtaining 5-star heroes. It exists alongside summoning and event
distribution as a third acquisition route: deterministic where summoning is probabilistic, and
roster-hungry rather than currency-hungry.

---

## Hard blocker

**Fusion cannot be built until the star-tier migration lands.**

The recipe is defined in terms of star values (3-star, 4-star, 5-star). The current data model
stores `rarity: Common | Rare | Epic` per hero; that field carries no star value, and none of the
acquisition tiers or the recipe below mean anything until every hero has a star field instead.
Star-tier migration — replacing the `rarity` field with a `stars: 1..5` field across
`heroes.json`, `HeroDef`, `StatMath.RarityMultiplier`, `SaveSerializer`, and any UI that renders
rarity — is the next required task before any fusion code can be written. This document does not
specify that migration; it depends on it. Nothing in this document overrides or combines with it.

---

## 1. The recipe

```
4 × 4-star heroes of the same class
+
6 × 3-star heroes (any class)
=
player's free choice of any fusable 5-star hero of that same class
```

**Class** is the hero's `role` field in `heroes.json` — the six current values are Bulwark,
Breaker, Mender, Striker, Warden, Stalker. Class only carries meaning at 4-star and 5-star; 1-star
through 3-star heroes are classless by design. This is not an oversight or a gap to fill later:
classes at low star tiers would imply class-based fodder restrictions that don't exist, restrict the
fusion-fodder pool unnecessarily, and add authoring overhead to heroes that serve no class-typed
mechanical role. Keep them classless.

The class constraint for the recipe is: the four 4-star fodder must share one class, and the
resulting 5-star must belong to that same class. The six 3-star fodder are unconstrained — any
class (or none, being classless by design).

**No gating.** Fusion is available from the moment a player holds the required ten heroes. There
is no chapter unlock, no battle milestone, and no prior fusion required. If the fodder is in the
inventory, the recipe is open.

---

## 2. Fodder consumption

All ten heroes — the four 4-stars and the six 3-stars — are **permanently destroyed** the moment
the fusion resolves. There is no partial refund, no shard or gold recovery, and no undo. The
player's roster shrinks by ten entries and gains one.

**No additional currency cost.** The heroes are the entire price. Gold and shards are not charged.

---

## 3. Content authoring: heroes.json

The recipe is class-generic: to reach a 5-star Bulwark, a player needs four 4-star Bulwarks, not
four copies of a specific hero. The `role` field on each hero already carries the class value; no
new field is needed for the class matching half of the recipe.

The one new field required is a fusability flag:

```json
{
  "id": "example_hero",
  "role": "Bulwark",
  "fusable": true
}
```

`fusable` is a boolean, default `true` for all heroes not explicitly excluded. A hero with
`"fusable": false` does not appear in the fusion result pool under any class, regardless of whether
the player holds the correct fodder. The flag lives on the hero definition, not on a recipe list,
because recipes are class-generic: there is no per-hero recipe to attach an exclusion to.

**Who gets `fusable: false`:** event-exclusive and summon-only 5-star heroes. These are acquired
through event rewards or probability-gated banners specifically designed around their scarcity.
Making them fusable would undercut that design by turning their classmates into indirect farming
targets. When an event hero is authored, set `"fusable": false` on that entry.

Current roster (all prototype heroes are fusable — no event exclusives exist yet):

| Hero | Role | fusable |
|---|---|---|
| Orrin Kaal | Bulwark | true |
| Kalyx Emberwright | Breaker | true |
| Wren Ilyra | Mender | true |
| Ryn Solvarr | Striker | true |
| Maeve Corrin | Warden | true |
| Nyx Serrow | Stalker | true |

`fusable` defaults to `true` so the field can be omitted on ordinary heroes; it only needs to be
explicitly set on the exceptions.

---

## 4. Acquisition path reconciliation

Three routes lead to a 5-star hero:

| Route | Mechanism | Class-targeting | Excludes |
|---|---|---|---|
| Summon | Shards, weighted RNG | No | Nothing |
| Summon pity | First 1–3 pulls, unowned pool | No | Nothing |
| Fusion | Consume 4×4-star + 6×3-star | Yes — class-matched 4-stars | `fusable: false` heroes |
| Event / distribution | Direct grant | N/A | N/A |

Fusion is the only route that lets a player choose which 5-star they receive within a class.
Summoning remains probabilistic and class-agnostic. Event-exclusive heroes are available only
through their own distribution path; they appear in neither the summon pool outcome list nor the
fusion result pool.

A hero obtained through any route behaves identically in combat. Acquisition path is not a
gameplay attribute.

---

## 5. Confirmation UX requirement

Fusion permanently destroys ten owned heroes in a single action. Before this ships, the UI must
implement an unambiguous confirmation flow that:

- Shows exactly which ten heroes will be consumed — names, star tier, class — before any
  irreversible action occurs.
- Requires the player to take an explicit confirm action (not a passive timeout or a single tap).
- Makes the resulting 5-star choice clear before the fodder is committed.

This document does not specify the screen layout or the step sequence; that is a UI design
decision for when fusion is built. The requirement is that no hero is destroyed before the player
has seen exactly what they are giving up and confirmed that choice.

---

## 6. PowerScore and recommendedPower interaction

`DESIGN-COMBAT-STATS` defines `PowerScore` as a weighted sum across stats, and `recommendedPower`
per stage gates `CheckStageWinnability`. Fusion changes a player's roster by removing ten heroes
and adding one. The net effect on team power depends on which heroes are consumed:

- The six 3-star fodder are likely below the player's current top-five deployed team, so their
  removal may not change the team that actually enters a battle.
- The four 4-star fodder may include heroes the player was deploying, depending on roster depth.
- The resulting 5-star is the strongest unit on the player's roster.

Whether this net change can push a player below a stage's gating threshold — or past it — is worth
checking during the star migration or fusion implementation pass. The concern is a player who
destroys their second-best deployed hero as fodder and loses the power margin a gated stage
required. This document does not resolve the interaction; it flags it for whoever builds fusion to
verify against `CheckStageWinnability` on representative roster states.

---

## Out of scope for this document

- Star-tier migration. Specified elsewhere; a hard prerequisite.
- The fusion screen layout, step sequence, or animation.
- Rarity multipliers at each star tier (belongs in star migration spec).
- Multi-fusion or batch recipes.
- How event-exclusive heroes are distributed (belongs in an event system spec).
