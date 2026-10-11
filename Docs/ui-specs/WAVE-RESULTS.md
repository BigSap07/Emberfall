# Wave results: the locked design

Approved by the owner on 2026-10-11 for the campaign result screens. This is the design as
approved, recorded before the code that builds it. Implement it as written: no extra UI, copy or
explanatory text. Where something here proves impossible, report it rather than improvise.

Look and feel: reuse the game's existing fonts, colours, button styles and panel ornaments
(`UITheme` and the existing button builders). Match the mockup's layout, not its exact hex values.

## A. Wave cleared: an overlay, not a screen

- **Behind it:** the stage's battlefield background with the player's squad standing in idle
  poses, using the existing battle rig. No enemies (the wave is beaten). No battle HUD labels.
- **A bottom band**, full width, about 330 of 1080 tall: near-opaque dark brown, with a 2 px gold
  rule on its top edge. Everything is centred inside it, top to bottom:
  1. A small caps line: `STAGE 2-1 · LEAVING ALDERWICK` (stage id and name).
  2. A large gold serif title: `WAVE 4 CLEARED`.
  3. One row: a 12-pip progress track (done pips gold, the current pip wider and brighter, future
     pips dark) followed by `4 / 12`. Then, to its right, reward chips for what this wave paid (a
     coin icon with `+32`, an XP star with `+4 XP`). Only the chips for currencies actually paid
     are shown.
  4. A button row: NEXT WAVE (primary, orange) with a small countdown digit inside the button when
     auto-advance is on; WATCH (secondary, teal); an AUTO checkbox.
- **AUTO** on means NEXT WAVE fires automatically after 2 seconds. Any tap on the band cancels the
  countdown for this wave. AUTO is a player setting: it is stored in the existing settings or
  preferences mechanism, NOT in the save, and the save version does not move.
- CAMPAIGN is removed from this view; HOME covers it.

## B. Stage cleared: the only full screen

- The battlefield background, heavily darkened, with HOME top left.
- A centred column: a gold rule, a large `VICTORY`, the line `STAGE 2-1 · LEAVING ALDERWICK
  CLEARED`, a gold rule.
- A `FIRST CLEAR` tag, only on a first clear.
- A reward tile row: one square tile per reward (an icon, then the amount, then a small caps
  label): gold, Ember Shards, Wayfarer XP, and the armor drop (item name plus quality) when there
  is one. The existing currency icons and item art are used where they exist.
- One line, `STAGE 2-2 UNLOCKED`, with a small unlock icon, only when a stage was unlocked.
- Buttons: CONTINUE (primary), WATCH (secondary).

## C. Wave lost: the same band as A, in a red tone

- The title `WAVE 5 LOST` in muted red. The current pip is red.
- One line: `Fell in battle 7 of 12. Progress up to wave 4 is kept.` with the real numbers.
- Buttons: RETRY (primary), ADJUST LINE, WATCH. No auto-advance on a loss.

## D. Removed everywhere

The per-battle HP bar chart and the `HP KEPT` percentage. The data stays available to the Dev
Arena only if something there already uses it.

## E. The per-wave reward drip (economy change)

- Before: a mid-stage wave pays nothing and the stage clear pays everything.
- **New rule:** each wave cleared on a first clear pays an equal share of 40 percent of the
  stage's gold and Wayfarer XP. The stage clear pays the remaining 60 percent, plus everything
  else as before: the first-clear bonus, shards, drops, unlocks.
- The total per stage is identical to before, to the unit. Any rounding remainder goes on the
  stage clear.
- Deterministic, and done in `GameSession`, the one resolution path. Paid waves are tracked by the
  existing wave cursor, so a resumed stage never pays a wave twice, and an abandoned stage keeps
  what its waves paid.
- Repeat clears and sweeps are unchanged.
- The 40 percent lives in content config if there is a natural home for it; otherwise it is a
  named constant cited in `DESIGN-BUILD-ORDER.md`.
- If this needs a save field, stop and report it before adding one.
- Harness checks:
  - the full-stage total is equal before and after for every stage in `campaign.json`;
  - no double pay on resume;
  - no pay on a replay or WATCH.
- The economy checks are run and any movement in `CheckEconomyRespondsToIncome` or the power band
  numbers is reported. Movement is acceptable if explained; a new failure is not.

## Verification

1. `Tools/build-check.ps1`, with no failure beyond those already known on the base.
2. `PlayModeScreenshot` captures at 1920x1080 and 2400x1080 of: wave cleared with AUTO on, wave
   cleared with AUTO off, stage cleared on a first clear with an armor drop, and wave lost.
