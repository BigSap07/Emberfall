# Glossary

Every `StatusKind`, `PassiveTrigger`, and `EffectKind` defined in `Assets/Scripts/Core/Data/Enums.cs`,
alphabetized within each category. Definitions synthesized from the enum, its inline comments, and
usage across `CombatSimulation.cs` and the design docs.

## StatusKind

- **AttackDown** — reduces the holder's effective Attack for its duration. Harmful; contestable by
  Effect Resist.
- **AttackUp** — increases the holder's effective Attack for its duration. Beneficial.
- **Burn** — damage-over-time, ticked each round on the holder's own turn. Ember sources stack
  multiple Burn instances instead of overwriting (see [Aspects](Aspects.md)). Harmful; contestable.
- **DefenseDown** — reduces the holder's effective Defence for its duration. Harmful; contestable.
- **DefenseUp** — increases the holder's effective Defence for its duration. Beneficial.
- **Focus** — increased critical chance for its duration. Beneficial.
- **None** — placeholder / "no status" value.
- **Regen** — heal-over-time; the mechanic Verdant's overflow-banking rule feeds into instead of
  discarding wasted healing. Beneficial.
- **Shield** — absorbs incoming damage before it reaches HP. Beneficial. Central to Lumen's
  interception identity via `DealRawDamage`'s absorb block.
- **Silence** — blocks the holder's Active on their next turn; a Basic Attack resolves instead.
  Harmful; contestable.
- **Slow** — reduces the holder's effective Speed. Gale's version pushes a target later within the
  *same* round it's applied, not just the next one. Harmful; contestable.
- **Stealth** — inert placeholder in the current codebase; no targeting-visibility logic is wired to
  it yet.
- **Stun** — skips the holder's entire next turn (no Basic Attack, no Active). Harmful; contestable.
- **Taunt** — forces enemy single-target abilities to resolve onto the holder. Applied to `Self` in
  every current use (Orrin, and the enemy Stone Warden). Central to Lumen's targeting-override
  identity via `FindTargetAnchor`/`FindTaunter`.

Only harmful statuses (`AttackDown`, `DefenseDown`, `Burn`, `Slow`, and by category also `Silence`
and `Stun`) are rolled against `EffectResist`; beneficial statuses always apply as authored. See
[Combat Systems](Combat-Systems.md).

## PassiveTrigger

- **AfterBasicAttack** — fires after the holder's Basic Attack resolves. Used by Nyx's `Hollowing`.
- **BattleStart** — fires once, at the start of the battle. Used by Wren's `Rootbound`.
- **None** — placeholder / no trigger.
- **OnBlock** — fires when the holder blocks a hit. Defined in the enum but currently unused by any
  content — the proposed (and rejected-as-specified) Block stat would have needed this to earn its
  place. See [Combat Systems](Combat-Systems.md).
- **OnCriticalHit** — fires when the holder lands a critical hit. Was used by Ryn's old
  `Follow-Through` (now moved to `OnKill` — see [Heroes](Heroes.md)); currently defined but unused
  by shipped content.
- **OnDamaged** — fires when the holder takes damage. Defined but not used by any current hero or
  enemy passive.
- **OnHpBelowThreshold** — fires once the holder's HP drops below an authored threshold. Used by
  Orrin's `Emberskin`.
- **OnKill** — fires when the holder's hit kills its target. Used by Ryn's `Follow-Through`. Central
  to Gale's turn-re-entry identity, which is keyed on `killer.Aspect == Aspect.Gale` at the same
  call site rather than being a passive itself.
- **TurnStart** — fires at the start of the holder's own turn. Used by Maeve's `Rising Chorus`.

## EffectKind

- **ApplyStatus** — applies a `StatusKind` to a target, gated by a chance roll (now modified by
  Effect Hit/Effect Resist for harmful statuses — see [Combat Systems](Combat-Systems.md)).
- **Cleanse** — removes one or more harmful statuses from a target. Filters on `StatusInfo.IsHarmful`
  and cannot remove an uncleansable Umbra-applied status.
- **Damage** — deals damage through the full pipeline described in
  [Combat Systems](Combat-Systems.md).
- **Dispel** — removes beneficial statuses from a target (the counterpart to `Cleanse`).
- **EnergyGain** — grants energy toward a unit's Active cost. Subject to Tide's overflow-redirect
  rule when granted by a living Tide source.
- **Heal** — restores HP. Subject to Verdant's overflow-banking rule (converts wasted healing to
  `Regen`) via `ApplyHealValue`.
- **Shield** — grants an absorb-shield value, added unconditionally to the target's existing Shield
  (no cap or discard exists on this path — see [Open Questions](Open-Questions.md) for why two
  relics were cut after being designed around a discard here that didn't actually exist).
- **SwapPositions** — swaps two units' battle-line positions.
