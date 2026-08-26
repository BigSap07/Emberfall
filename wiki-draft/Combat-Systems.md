# Combat Systems

Source: `DESIGN-COMBAT-STATS.md` and `DESIGN-BATTLE-READABILITY.md`, checked against
`Assets/Scripts/Core/Combat/CombatSimulation.cs`. Combat resolves as a pure function
(`CombatSimulation.Run()`) that returns an ordered list of `BattleEvent`s up front; the battle
screen only plays that list back and never computes anything itself.

## The damage pipeline — implemented, in order

`ApplyDamageEffect` resolves a hit in this order (all confirmed live in current code):

| # | Step | What it does |
|---|---|---|
| 1 | Base | `source.EffectiveAttack * effect.Power + effect.Flat` |
| 2 | Aspect factor | multiplies by `AspectChart.Multiplier` (×1.10 favoured / ×0.92 disfavoured — see [Aspects](Aspects.md)) |
| 3 | Passive bonus vs. status | was Kalyx's `Kindling` only; now dead code since Kindling was cut |
| 4 | Crit | rolls `EffectiveCritChance`, then multiplies by `CritDamage` |
| 5 | Defence | multiplies by `1 - defense / (defense + DefenseConstant + DefensePerLevel * level)` |
| 6 | Damage variance | multiplies by a random spread |
| 7 | Round and floor | rounds away from zero, clamps up to a minimum damage |
| 8 | Shield, then HP | absorbs into Shield first, remainder to HP |

Worth knowing: mitigation (step 5) is multiplicative and lands *after* the crit multiplier, so a
crit and a non-crit hit are reduced by the same fraction — Defence doesn't blunt crit spikes
specifically, it scales everything equally.

## Defence — already existed, no change needed

The original proposal that spawned `DESIGN-COMBAT-STATS.md` asked for a "Defence" stat to be added
to the pipeline. It was **already there** (`EffectiveDefense` on `CombatUnit`, folding in
`DefenseUp`/`DefenseDown` with a floor), so this part of the proposal was **rejected as
redundant** — nothing was built. Armour penetration was also considered and rejected: it would just
be `DefenseDown` with extra steps, and the project's rule bans percentage stat sticks anyway.

## Block — proposed, rejected as specified

A "chance to reduce an incoming hit" stat was proposed and **rejected**: its expected value reduces
to a flat multiplier (exactly what the no-percentage-stat-stick rule forbids), and it's the most
expensive draw to add to the highest-frequency code path in the simulation. **Not built.** The doc
leaves the door open for a *rule*-shaped version later (`PassiveTrigger.OnBlock` making "blocks feed
a counterattack" writable, or Block negating a status rider rather than a damage slice) but notes
any such attempt would be competing directly with Lumen's identity and needs to be argued against
it specifically. `PassiveTrigger.OnBlock` exists in the enum but has no implementation or content
using it — see [Glossary](Glossary.md).

## Effect Hit / Effect Resist — implemented

Proposed as one opposed mechanic and **built**. Confirmed in code:
`CombatSimulation.cs:ResolveLandingChance` computes
`landing = baseChance + sourceEffectHit - targetEffectResist` (additive, not multiplicative — kept
additive deliberately so a point of the stat is worth the same regardless of a status's base
chance), clamped, and only rolled for **harmful** statuses (beneficial/self-applied statuses like
Taunt or Shield always apply as authored). `EffectHit`/`EffectResist` are flat stats (not scaled by
level or rarity, like `CritChance`) read straight from each hero/enemy's JSON — see
[Heroes](Heroes.md) for current values.

A new `BattleEventType.StatusResisted` event was added (confirmed in `BattleEvent.cs` and emitted
in `CombatSimulation.cs`) so a resisted debuff has a visible trace in the event record instead of
just silently not appearing, and `Debrief.cs` reads it for the results-screen wording.

**Why this mattered beyond the stat itself**: without it, Umbra's uncleansable-debuff identity (see
[Aspects](Aspects.md)) had *no counterplay at all* once a debuff landed — the only lever was
whether it landed, and that used to be a fixed constant nobody could influence. Effect Resist is
the defender's side of that roll.

## Team size 3 → 5 — implemented

Confirmed in current content and code: `balance.json`'s `maxTeamSize` is `5`,
`PlayerProfile.Team` is sized to it, and `CombatSimulation.BuildUnits` reads `_cfg.MaxTeamSize` for
both sides (including enemy spawns — the docs flagged this as a trap, since every stage in
`campaign.json` needed refilling to five spawns or the enemy side would stay at three and every
fight would trivially win). This work was coupled to the Effect Hit/Resist rollout because
`AllEnemies` effects resolve once per living target, so the value of a point of Effect Hit is a
direct function of team size — the docs insisted on shipping and retuning both together rather than
in two separate passes.

## Determinism

Combat runs on a single deterministic RNG stream (`DeterministicRng`, xorshift32) shared across an
entire battle. Adding or removing a single draw anywhere shifts every subsequent roll for the rest
of that fight — the doc leaned on this heavily to argue Effect Hit/Resist could ship in two phases:
phase A (stats present, defaulted to zero, arithmetic bypassed entirely when both are zero) is
provably a no-op against existing seeded battles; phase B (authoring non-zero values on real
heroes/enemies) invalidates every existing win-rate table and requires a full campaign retune.

## Battle readability — mostly implemented

`DESIGN-BATTLE-READABILITY.md`'s core rule: "most of what looks missing [on the battle screen] is
not missing, it is unread" — the event list already has it, the screen just needs to read it rather
than recompute it. Per that doc's own inline status markers, most of its asks have been fixed:
`TurnStart` now has a handled case, the real (not geometrically-guessed) target is resolved via
event lookahead, the turn strip reads the round's `TurnStart` events, and both `AspectFactor` and
`AbilityName` fields were added to the `Damage` event (read by the battle screen's WEAK/RESIST tags
and by `Debrief`). Two items remain only partially addressed per the doc's own notes: the killer
(`Death.SourceId`) is used in the post-battle Debrief screen but not shown inline during playback,
and Ember's Burn-stacking (see [Aspects](Aspects.md)) produces multiple simultaneous Burn instances
that the status bar still only renders as one `BRN` tag — a genuine display gap, not just an unread
record.
