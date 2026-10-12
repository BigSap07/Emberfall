# Battle stats: the locked design

Approved by the owner on 2026-10-12 for the campaign result views. A placeholder look: final art
comes later from the designer, so layout and logic are kept apart from styling, and the panel
reuses `UITheme` and the existing builders. No extra UI or explanatory text.

## The spec

1. A STATS button on every campaign result view (wave cleared, stage cleared and wave lost), next
   to WATCH, in the secondary style.
2. STATS opens a panel over the darkened battlefield:
   - Title "Battle stats" plus the stage and the result line, for example `2-1 · WAVE 5 LOST`.
   - One row per hero in the deployed line: name, position and aspect, then damage dealt, damage
     taken, healed and shielded (each a number plus a bar scaled to the highest value in that
     column) and kills.
   - A scope toggle: WHOLE WAVE, or the single battle. On a loss, open on the lost battle;
     otherwise on the whole wave.
   - An MVP line: the hero with the highest damage dealt, and their share of the line's damage.
   - A close control. No explanatory prose.
3. Data: aggregated per hero from the `BattleEvent` records the wave already produces, inside
   `GameSession`'s resolution path, in memory only. Not in the save, no save version bump, no
   effect on outcomes; combat stays a pure function. The per-battle breakdown is kept in memory
   too so the toggle can show the single battle.
4. Harness checks: totals equal the sum of the matching `BattleEvent`s for a known seed; the
   serialized save is byte-identical before and after opening stats; totals are deterministic
   across runs.

## Two interpretations, recorded

- **The single battle on a wave with no loss.** A loss has an obvious battle (the lost one). A
  cleared wave and a stage clear have none, so the toggle's second scope is the battle
  `WaveResolution.Replay` already points at (the one WATCH plays: the win that left the least HP),
  labelled with its number, `BATTLE 7`. The toggle is therefore always WHOLE WAVE against
  BATTLE n.
- **What "whole wave" covers.** The battles this resolve fought, from `FirstBattle` onward. After a
  loss and a RETRY the next resolve starts at the lost battle, so its whole-wave scope covers only
  the battles from there. Nothing is carried across resolves: that would need persistence or
  session state, which the spec rules out.

## Crediting

Follows `TalentTrial` and the star band, over Damage, Heal and ShieldGain only, so the panel agrees
with the instruments.

- Damage dealt: `Damage` value with a line hero as `SourceId` and an enemy as the target (HP damage
  after shield absorption; the absorbed part is a separate `ShieldAbsorb` event and is not added).
- Damage taken: `Damage` value with a line hero as `TargetId`, whoever sourced it.
- Healed: `Heal` value by `SourceId` (effective healing only, self-heals count to the healer).
- Shielded: `ShieldGain` value by `SourceId`.
- Kills: `Death` events with a line hero as `SourceId` and an enemy as `TargetId`; no killer earns
  nobody a kill.
- Rows are keyed by `UnitSnapshot.DefId`, stable across the battles of a wave. Position is the
  Formation screen's convention: `1 FRONT`, `5 BACK`, the middle slots by number.
