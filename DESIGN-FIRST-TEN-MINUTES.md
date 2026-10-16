# Emberfall — the first ten minutes

**SUPERSEDED, 2026-10-12, by `DESIGN-ONBOARDING.md`.** This describes the old six-hero 3v3 game
and must not be used as evidence. It is kept as the record of an argument that was had.

Scope: the opening session only, on a fresh save, with the six heroes, twelve stages and
systems that exist today.

## The problem, stated from the data

A new profile starts with 1500 gold, 260 shards and exactly three heroes for exactly three
banner slots, and `NormaliseTeam()` fills the slots for the player. The first campaign tap is
preceded by no choice at all, and the battle that follows is a pure function the player cannot
touch. Worse, the summon they can already afford rolls Common 62% of the time — and all three
Commons are their starters, so the likeliest first pull is an "Echo of an ally you already
command."

The opening ten minutes currently teach: *things happen, then numbers go up.*

## What the player should understand

**By minute 1 — "these are mine, and I set them up."**
Three names, each with an Aspect and a role. Slot 1 is the one who gets hit. They made one
choice before the fight started.

**By minute 5 — "the team is a choice, and it's mine to get wrong."**
A fourth hero exists and only three can fight. They have benched somebody. They know Ember
loses to Gale because a battle told them in the moment it happened, not because a tooltip
listed a triangle.

**By minute 10 — "I lost, I know why, and I fixed it."**
They have failed a stage, read a one-line cause, changed something specific — a level, a slot,
a swap — and beaten the same fight. The loop is legible: fight → currency → a better line →
the wall moves.

## The first real decision: minute 4, and it is the bench

Make shards earned. Set `startingShards` to **40**. Stages 1 and 2 pay 41 and 48 shards with
first-clear bonuses, so the first summon (120) becomes affordable the moment stage 2 resolves —
roughly minute 4. The player summons because they saved for it, gets a fourth hero, and now
three slots hold four candidates. Someone sits out: the first decision with a cost.

Everything before minute 4 is teaching; everything after is the player choosing.

`startingGold` at 1500 buys about ten hero levels and makes the first spend meaningless too.
**400** makes "which of the three do I feed" a real minute-2 question.

## Making the first summon matter

Two changes in `GameSession.Summon`:

1. **Summons 1–3 roll only from unowned heroes.** With this roster that guarantees Ryn, Maeve
   or Nyx — an unseen ability in an Aspect the player doesn't have. Duplicate-to-gold is a fine
   long-term valve and a terrible first impression.
2. **The summon lands the player somewhere.** After the reveal, go to the new hero's detail
   screen with the slot row live, not back to a shard counter. The pull's meaning is the choice
   it forces.

Nyx at 9% needs no special casing; Ryn (a 2.4× nuke) and Maeve (a team energy engine) are loud
enough to visibly change the next fight.

## The first loss: stage 6, by design

The Kiln Gate (rec. power 4300) is the first double-elite line-up and the right place to lose:
late enough that the team is one the player chose, early enough that the fix is cheap. If the
sim harness shows a naturally levelled player clearing it, raise the Stone Warden and Ashen
Revenant until the honest win rate sits near 40%.

The lesson is *attributable cause*, and defeat currently teaches nothing — no readout, no
retry. One sentence generated from the battle log does the work.

## Systems that already support this

- **Slot assignment.** `AssignToTeam` and the slot row exist. `FrontEnemy` targeting picks the
  lowest living slot and Taunt overrides it, so slot order already matters and is already
  editable. It is just never explained.
- **Aspect.** `AspectChart.Multiplier` already swings every hit ×1.20 / ×0.85.
- **First-clear bonuses, stage unlock, rank-up, hero level cap** — each early victory already
  has a distinct thing to say.

## Systems that do not

- `BattleEvent` carries `Critical` but no Aspect factor, so the mechanic that already decides
  fights is invisible to the battle screen. Put the multiplier on the damage event and float
  "WEAK" / "RESIST" beside the number. Surfacing, not a new mechanic.
- The campaign card shows `REC. POWER` and nothing about who you're fighting. Show the enemy
  Aspects. Power is a number to compare; Aspects are a reason to change the line.
- Defeat is a dead end — back button only.
- Idle income is invisible in session one and should stay that way. It is the reason to reopen
  tomorrow, not part of today.

## Three new mechanics

**1. Opening Gambit — one pre-battle choice, every battle.**
Before `CombatSimulation.Run`, the player picks one free effect: *Guard* (slot 1 starts with a
ward worth 15% of its health) or *Surge* (slot 3 starts at 60 energy). It is an input to the
simulation, so combat stays a pure deterministic function. Minute 1 is otherwise a spectator
experience; this is the smallest thing that makes the first battle something the player set up,
and it teaches that slots have jobs before an enemy punishes them for not knowing.

**2. First-summon guarantee (pulls 1–3 draw from unowned heroes).**
As built, the likeliest first summon duplicates a hero already on the banner. That one outcome
removes the fourth hero, which removes the bench decision, which removes minute 5.

**3. Debrief — a one-line cause on the results screen, win or lose.**
Derived from the event list: *"Orrin fell in round 4. Ash Nova hit your line for 2,100. You
were outsped, not outmatched."* Paired with **Adjust Line** (straight to heroes, stage
remembered) and **Rematch**. The stage 6 loss is the session's most instructive moment and
today it produces silence and a back button. A named cause plus a one-tap return turns a wall
into a puzzle.

Aspect readouts, enemy Aspects on the stage card and the slot-1 explanation are existing systems
being shown, and cost no mechanic slot.
