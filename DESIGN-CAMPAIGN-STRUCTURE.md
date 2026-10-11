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
- **Auto-advance through a fresh stage.** A first clear needs a chain mode, not 144
  individual manual taps: an Auto toggle that automatically starts the next battle the
  instant the current one resolves in a win, stopping only on defeat or a manual stop, the
  same pattern Idle Heroes and AFK Arena use for exactly this genre. This is a UI loop on
  top of the NEXT BATTLE call path the wave-structure work already built, not a new
  battle-resolution system. Without this, the 144x playtime increase this doc already
  accepted becomes active tap-time instead of idle clock-time, which was never the trade
  being made.
## Amended 2026-09-21: waveCount is per stage, not a constant

The decision above fixes `waveCount` at 12 for every stage, and the campaign-curve pass
found that number to be a pacing fault rather than a structural one. A sitting is 40
battles in the economy model, so a 12x12 stage costs four sittings before difficulty has
been consulted at all: chapter 1's four tutorial stages cost four sittings each, the whole
chapter cost about 24, and none of that time was the game being hard. It was ladder.

`waveCount` is now authored per stage and chapter 1 is the short chapter it always read
as: **3 waves for 1-1 to 1-4** (36 battles, one sitting) and **6 waves for 1-5 to 1-12**
(72 battles, two sittings). **Chapter 2 and everything after it keep the full 12.**
`battleCount` stays at 12 everywhere, because it is the number the battle screen counts up
to inside a wave and a wave of a different length in one chapter is a readout
inconsistency for no gain.

Nothing about the generator changes. `CampaignWaveGenerator.Progress` already divided by
`waveCount * battleCount - 1` rather than by a literal 143, so a stage of any length
interpolates from its own floor to its own ceiling, and the ceiling battle is still the
last one whatever that is. What did change is every harness check that had the 12 written
into it; `ExpectedWaveCount` in `Tools/DevHarness/Program.cs` is now where the authored
shape lives, and the checks read it rather than a constant.

**A save written under the old shape does not strand.** `GameSession.ResumePointFor`
already clamped a saved `(wave, battle)` into the stage it belongs to, so a save sitting at
wave 9 of a stage that now ends at wave 6 resumes at that stage's last battle: no progress
is taken away, and no stage is given away, because the battle still has to be won.
`CheckCampaignShortenedLadderResume` is the proof, and it round-trips through the real
serializer rather than setting the profile field by hand, because the value that would
strand a player arrives from a file.

The 20,736 arithmetic in the sections above is therefore a statement about the 12-chapter
shape this document proposed, not about what ships. What ships today is 24 stages over two
chapters and 2,448 battles: 4 x 36 plus 8 x 72 in chapter 1, and 12 x 144 in chapter 2.

## Amended 2026-09-22: the recommendedPower table is re-derived by a command

`recommendedPower` is not authored. It is the simulated player's ENTRY power at a stage
divided by the middle of that stage's difficulty band, which makes it a function of what a
player can field and therefore stale the moment anything a player fields changes. The
campaign-curve pass derived the whole table that way by hand and it was out of band two
merges later, because the kit and rarity passes made the simulated line a few percent
stronger while the table stood still. **Run `pwsh Tools/solve-recommended-power.ps1` after
any change to a hero the balance line fields, and after any change to the economy** - a
purse, a first-clear reward, an idle rate, the sitting budget, the starting roster. It
walks the economy, sets every stage's `recommendedPower` to entry power over its band
middle, re-solves `floorLevel`, `ceilingLevel` and the authored enemy levels underneath
the answer, and repeats the whole walk on what it just wrote until a walk proposes
nothing. It touches **four fields per stage in `campaign.json` and nothing else**: no
purse, no reward, no idle rate, no `waveCount`, no roster composition, no code, and not
one line of that file's commentary. `-WhatIf` runs it against a copy and writes nothing.
It asserts nothing about its own output, so run `Tools/build-check.ps1` afterwards:
`CheckCampaignPowerCurve` is the guard on the numbers and the geared curve's CEIL band is
the guard on the levels under them. Where it moved authored enemy levels it moved the
stage fingerprints with them, and regenerating that baseline stays a separate deliberate
act (`EMBERFALL_REGEN_STAGE_FP=1`).

## Amended 2026-10-11: a wave resolves in one tap

A first clear is now one resolve per wave, not one fight per battle. `GameSession.ResolveWave`
runs the rest of the current wave through `ResolveLadderBattle`, the helper
`SweepClearedStage` already used, so there is one resolution path for every campaign battle,
and returns a `WaveResolution` that `WaveResultScreen` only reads. `CampaignFlow.Launch` is the
single door a stage start goes through: a cleared stage is swept, a stage with a wave curve
opens `WaveResultScreen`, and only a stage with no ladder reaches `BattleScreen`.

What this supersedes: the per-battle Auto chain decision above. `BattleScreen`'s Auto and
NEXT BATTLE remain in the code, but nothing in the campaign reaches them.

What it leaves alone:

- The battle count. A stage is still `waveCount` x `battleCount` battles and each still runs
  through `CombatSimulation`; the resolve only stops asking the player to start each one.
- Defeat retries the single lost battle. The resolve stops at it, `RecordBattleLost` leaves
  the cursor on it, and the next resolve starts there, not at the wave.
- Rewards stay at stage granularity. A won mid-stage wave pays nothing and the wave that
  finishes the stage calls `GrantBattleResult` once.

What a first clear costs the player is now one resolve per wave plus one for every lost
battle: 3 on stages 1-1 to 1-4, 6 on 1-5 to 1-12, and 12 from chapter 2 on.
`ReportResolvesPerFirstClear` in the harness prints the count for the reference player's walk.

Cleared stages are recorded by id from save version 20: `CampaignProgress` derives the
frontier, the furthest stage and the count from `ClearedStageIds`, and a save older than that
migrates its position through the frozen `SaveSerializer.PreIdStageOrder`, never through the
live `campaign.json`.
