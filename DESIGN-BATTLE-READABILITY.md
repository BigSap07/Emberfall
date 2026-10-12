# Emberfall — battle readability

Scope: the battle screen only. Combat stays a pure function; the screen stays a player for
`BattleResult.Events`. Every change below is either playback ordering or a field on an event,
and the two new fields are named and justified.

The screen already owns the **whole list before the first frame**. Most of what looks missing is
not missing, it is unread. Looking ahead is not cheating the simulation; it is reading the
record it was handed.

## Priority order, at every moment

1. **Whose turn.** Nothing else parses without it. `TurnStart` is emitted every turn and the
   screen has no case for it — the most important signal is dropped on the floor.
2. **Who is being hit, before they are hit.** The player must see the arrow leave the bow.
3. **Who is about to die.** A hero at 12% is the only thing worth watching, and today looks the
   same as one at 80% except for a bar the eye is not pointed at.
4. **Why that number was big.** Crit, Aspect, or a ward that just broke — three different
   lessons that currently look identical.
5. **What lasts.** Status icons exist; durations are discarded.

## What the screen throws away

- **`TurnStart`** — no case in `Handle`. Adding one costs nothing and fixes priority 1.
  *Fixed. `BattleScreen.Handle` has a `TurnStart` case: the actor rises and the turn order advances.*
- **The real target.** `BasicAttack` and `AbilityCast` carry `SourceId == TargetId == self`; the
  target is only revealed by the `Damage` event that follows. So the screen guesses with
  `NearestOpponent()` — geometry. That is wrong for Ryn (LowestHpEnemy), Nyx (HighestAttackEnemy),
  Cinder Imp (RandomEnemy), and **every Taunt redirect**: the lunge points at one unit and the
  number lands on another. Fix by lookahead, not by a new field — scan forward from the
  `BasicAttack`/`AbilityCast` to the `Damage`/`Heal`/`ShieldGain` events before the next
  `TurnStart`, and you have the true target set, taunts included.
  *Fixed. `LookaheadTargets()` scans forward from `BasicAttack`/`AbilityCast` to the next `TurnStart` and returns the real target set.*
- **`Death.SourceId`** — the killer. Never used.
  *Partly used. `Debrief.Fallback` names the killer; the playback screen does not show it inline.*
- **`StatusApplied.Duration`** - the plate stores only the kind and its depth.
- **The round's turn order.** Between one `RoundStart` and the next, the `TurnStart` events *are*
  the initiative list. Enough to draw a turn strip with no new data at all.
  *Fixed. `RebuildTurnStrip`/`AdvanceTurnStrip` in `BattleScreen` read the round's `TurnStart` events and step
  through them per turn, and `TurnOrderPill` draws the result as a pill at the bottom centre: the acting
  unit's portrait disc enlarged, then NEXT and up to six units still to act this round.*
- **Lethal lookahead.** Each unit's `Death` index is knowable up front — use it to *pace*, not
  to spoil: hold the beat before a killing blow rather than flagging it early.

## What must be added

**1. `AspectFactor` (float) on `Damage`.** `ApplyDamageEffect` multiplies `raw` by
`AspectChart.Multiplier` and discards it. Crit, defense mitigation, the passive bonus and ±6%
variance all fold into one `int`, so ×1.20 is unrecoverable from the list. Without it the
mechanic that decides fights can never be shown or learned. This is the whole of "why was that
hit big."

*Done. `BattleEvent.AspectFactor` is set by `CombatSimulation` and read by both `BattleScreen`
(the WEAK/RESIST tag) and `Debrief` (priority 3 clause).*

**2. `AbilityName` on `Damage`.** Present on `BasicAttack`/`AbilityCast`, absent on the damage
they cause. Back-scanning misattributes Burn ticks and passive damage to whatever the unit last
cast. The Debrief names a killing blow out loud; it must not lie.

*Done. `BattleEvent.AbilityName` is set on `Damage` events by `CombatSimulation`, read by
`Debrief.TryAbilityDominant` for priority 2 and by the Fallback clause.*

Nothing else. Ward breaks, crits, deaths, rounds and energy are already in the list.

## One round, as the player experiences it

**Round banner** (0.3s) — the number, and the turn strip repopulates in initiative order. Then
per turn:

1. **Turn pip lights** on the acting unit's plate; the actor rises slightly. *Cause, 0.12s.*
2. **Intent** — the actor turns toward its real target (lookahead) and the target's bar pulses
   once. If the unit is casting, the ability banner appears **now**, before anything moves.
   *The banner is a slim callout pill centred under the top bar, "Unusable Cover · KAEL" for a
   cast and "Unusable Cover · KAEL · PASSIVE" for a passive (`BattleScreen.ShowCallout`). The top
   bar is opaque and the battle camera's viewport stops at its lower edge, so no unit, plate or
   number is drawn under it.*
3. **Motion** — lunge or projectile travels. 0.16s minimum; this is the beat that says *this one,
   not that one*.
4. **Impact** — flash and shake on the target.
5. **Number floats**, one frame after impact — never simultaneously. Ward absorption first in
   ward blue, then health damage in white; `AspectFactor > 1` carries a small **WEAK** tag,
   `< 1` a **RESIST** tag, and a crit is prefixed, not merely tinted (the existing
   `(e.Critical ? "" : "")` prefix is a no-op someone meant to fill).
6. **Bar drains** to the new value; the ghost bar trails behind it. Effect, last.
7. **Status icon** appears on the plate with its duration; **energy** fills.
   *Built without the duration. Each unit has one plate (`UnitPlate`), hung under its feet: the
   name on one line, the HP bar with the ward over it, a thin energy bar, the turn pip to the left,
   and above the feet a row of status icons that is empty at rest. One icon per kind, harmful kinds
   first, then Shield, then buffs, then the inert bookkeeping kinds, three at most and "+N" for the
   rest, with a stack's depth as a small digit (`StatusIcons.Compose`). The placeholder glyphs are
   a green chevron up, a red chevron down, the shield and a grey diamond, each reskinnable through
   `HudSkin`'s `hud_status_<kind>` and `hud_status_<role>` slots. `BattleHudChecks` in the harness
   proves no two plates overlap at any formation slot at 16:9, 18:9 and 20:9.*
8. On death: everything else stops, 0.5s, then the fall.

The rule is one sentence long: *the source moves before the target reacts, and the target reacts
before the number appears.* Today steps 4–7 all happen in the same frame.

## Speed

The ladder is 1x/2x/4x. Speed should compress **dead time**, never **state changes**.

Compress freely: inter-event waits, banner dwell for an ability already seen this fight, ghost
bar trailing, repeated basic attacks, ambient VFX.

Never skip, at any speed: any **Death** (full beat, at 1x pacing), the **first cast of each
ability** in a battle, the **Aspect and crit tags**, and the **bar arriving at its true value**.
Floating numbers must never be culled — at 4x, batch a multi-target hit into one stacked column
rather than dropping six numbers on top of each other.

Put a floor of ~0.05s real time under every wait so a hit is always one perceptible event.
`Skip` is a different promise — resolve now, no playback — and should stay that.

## The Debrief line

Query the list once at `BattleEnd`, in this priority order, and emit the **first** clause the
numbers actually support:

1. **Timeout** — `TimedOut` plus `EnemyHpRatio`.
2. **One ability did the damage** — group `Damage` on player units by `AbilityName`; if the top
   one is >35% of damage taken, name it and its total.
3. **Aspect** — share of damage taken with `AspectFactor >= 1.01`; if >35%, say the line was the
   wrong colour.
4. **Resisted** — the share of the player's own harmful statuses the enemy shrugged off
   (`StatusApplied` against `StatusResisted`); over half of at least four attempts names Effect Hit.
5. **Speed** — turn order is re-rolled each round (`RoundSpeedJitter`), so one round proves
   nothing. Count enemy `TurnStart` events preceding each player unit's turn across the whole
   fight; a consistent majority makes "you were outsped" a fact, not a flourish.
6. **Unspent active** — a unit alive at `BattleEnd` that never emitted `AbilityCast`.
7. **Fallback** — the first player `Death` (`Round`, `TargetId`, `SourceId` as killer) and the
   largest single `Damage`. Two true numbers beat a confident wrong cause.

**Clauses 2 to 5 are asked only on a loss.** Each explains what went wrong for the line, and the
numbers they read do not know who won: a line can be outsped and win anyway, and a VICTORY used to
print "you were outsped, not outmatched". On a win, `Debrief.Summarize` goes from Timeout straight
to Unspent active and the Fallback, which read true either way. `CheckDebriefRealVictories` in the
harness fights real guild-dungeon wins that the numbers alone call outsped and holds that none of
them prints a loss's reason.

All fields above now exist, including `AspectFactor` and `AbilityName` on `Damage`.
