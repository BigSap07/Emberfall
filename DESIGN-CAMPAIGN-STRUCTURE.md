# Emberfall - campaign structure

Decided 2026-09-07. Replaces the flat Chapter -> Stage model everywhere it appears in
DESIGN-BUILD-ORDER.md and in campaign.json today. This document is the decision and the
technical shape; DESIGN-BUILD-ORDER.md's numbered tasks need to be re-read against it before
task 13 (the retune) starts, not after.

## The decision

Chapter -> Stage -> Wave -> Battle. 12 chapters, 12 stages per chapter, 12 waves per stage,
12 battles per wave. 12 x 12 x 12 x 12 = 20,736 battles.

Stage is the real, authored, tuned unit. That is 144 stages total (12 x 12), six times
today's 24. Wave and Battle are not hand-placed content. They are a generated difficulty
ladder inside a stage, built the same way Deep Recital already generates its endless floors:
one formula interpolating from a floor to a ceiling, not 20,736 individually composed
encounters. Every one of the 20,736 is a real, fought battle, not a cosmetic number, but
nobody ever hand-tunes more than 144 curves.

## Why this shape and not literal 20,736 hand-authored encounters

The goal behind the big number is a year of progression runway, stated directly by the
person who owns this call. That is a legitimate goal. It is not, on its own, a reason to
hand-author 20,736 encounters.

In an idle RPG, how long content lasts is governed by two things this project already has:
how fast player power grows against `recommendedPower` (task 13's whole job), and whether
there is an endless mode once the authored campaign is cleared (Deep Recital, already
shipped). Raw stage count is not the lever. A player can blow through 20,736 badly-paced
stages in a month, or make 144 well-paced ones last a year. The pacing work is the same
work either way, and task 13's own audit already calls that work hard at 24 stages -
`PowerScore` is non-linear across team sizes, and the two existing balance instruments
disagree with each other. Multiplying the hand-tuned surface by 864x before that problem is
solved once is how a project stalls on content nobody can balance. Generating the ladder
inside each stage from a floor/ceiling curve, the same pattern Recital already proves out,
gets the scale without multiplying the tuning burden.

## What changes

**campaign.json.** Today a stage entry is one fixed enemy list. It becomes a stage
definition carrying a wave curve instead: a floor and ceiling for enemy level/composition,
`waveCount` (12) and `battleCount` (12), and whatever composition rule the stage wants
(reuse the stage's existing enemy roster, scaled, is the simplest starting rule and matches
how Recital already does it - no reason to invent a second scaling language on day one).

**A new generator.** Extend the `RecitalGenerator` pattern rather than invent a parallel
one: `LevelForFloor`-style interpolation, but keyed on `(wave, battle)` inside a stage's own
floor/ceiling instead of an endless climb. This is the piece that actually produces the
20,736 - computed on demand, not stored per-battle in content.

**Save format.** `PlayerProfile` needs progress *within* the current stage, not just which
stages are fully cleared - a `currentWave`/`currentBattle` pair (or equivalent), the same
shape Recital's `RecitalCurrentFloor` already is. Additive field, next `SaveSerializer`
version bump, same pattern every prior bump has used.

**Repeat cap and idle income stay at Stage granularity.** `stageClearsToday` and the idle
rate resolved off `StagesCleared` should not become `battleClearsToday` at 20,736 resolution
- that is real accounting blowup for no player-facing benefit. A Stage clearing is still the
unit those two systems care about; Wave/Battle only exist inside the attempt.

## What does NOT change

The Campaign screen's chapter/stage picker does not need a four-level menu and does not need
rebuilding. It still shows 12 stages per chapter, exactly like today, just six chapters'
worth more of them existing across the game (144 instead of 24) - the per-chapter stepper
already handles a 12-item range fine. What's new lives one layer down, inside a stage: a
progress readout during the run itself ("Wave 4 of 12", or similar) and the ability to
resume a stage that was left partway through. That is a real screen need, but it is scoped
to the battle/stage-run screen, not a Campaign screen redesign.

## Sequencing against DESIGN-BUILD-ORDER

This has to land before task 13, not after it, and probably replaces what task 13 tunes
against rather than sitting beside it. Retuning the current flat 24-stage model and then
restructuring underneath the retuned numbers throws the retune away.

## Decided, 2026-09-07

- **Mid-stage exit and resume.** Resumes at the last cleared wave/battle, persists
  indefinitely, same as any other save field.
- **Defeat inside a stage.** Retry the single battle that was lost, not the wave and not the
  stage.
- **Repeat-clear behavior.** A cleared stage becomes a one-tap sweep, resolved instantly, not
  a full 144-battle real-time replay.
- **Composition variety inside a stage.** Ship the cheap rule: scale the stage's existing
  enemy roster by level, same as Recital. No compositional (enemy-count/type) change across
  waves.
- **Chapters 1 and 2, already shipped.** Retrofit both into the new Stage-plus-wave-curve
  schema rather than leaving them on the old flat format.