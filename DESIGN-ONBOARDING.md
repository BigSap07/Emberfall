# Emberfall, onboarding

Decided by the owner on 2026-10-12. This is the spec as decided, recorded before the code that
builds it. It replaces `DESIGN-FIRST-TEN-MINUTES.md`, which describes the old six-hero 3v3 game.

The look is a placeholder for the designer's later art pass. Layout and logic stay separate from
styling, and the styling reuses `UITheme`.

## Why

The first playtest showed that a new player lands in roster, fusion, relics and guilds with
nothing explained.

## The fiction does the teaching

The player is the Keeper (`DESIGN-WORLD.md`, **The Cinder and the Keeper**). The Keeper's act is
recalling heroes through the Cinder, so the first guided tap is a recall.

## The flow

1. **Wake.**
   - A dark screen over the dimmed hub, with three lines: "Wake up." / "Silence. Birds.
     Morning." / "And a village that has stopped caring."
   - One tap continues. A small SKIP sits top right.
   - The lines are placeholders. They live in content data so the designer can rewrite them
     without code.
2. **First tap: the Cinder.**
   - The hub with everything dimmed except the Summon obelisk, ringed and spotlit.
   - A short card: "The Cinder remembers." / "Call back someone who once stood here."
   - One RECALL button. It is a free, scripted recall that always yields Kael (the current Kael
     id in `heroes.json`, `kael_the_unyielding`).
   - It costs nothing and never touches the normal summon pool, pity, rates or summon counters.
3. **The prologue battle.** The existing prologue battle and squad, unchanged. The player
   watches. One line: "Your heroes fight on their own."
4. **Guided core loop**, with forced arrows, one step at a time:
   1. set team (Kael is placed automatically if the player does nothing)
   2. campaign 1-1
   3. the wave result screen
   4. level up a hero once
   5. collect idle rewards
5. **Free play.** From here, light touch only: a one-line tip the first time each major screen
   opens, dismissed by any tap, never shown again.

## Rules for the forced steps

- Only the highlighted control accepts input. Everything else is dimmed and inert.
- A five-pip step indicator shows progress through steps 1 to 5.
- Each step is saved as completed the moment it finishes, so quitting mid-tutorial resumes at the
  step after the last completed one.
- This needs a save field. Add the minimum: a tutorial step index and a set of seen tips. Bump the
  save version to 21 with a migration, and mark any existing save that already has campaign
  progress as tutorial-complete with all tips seen.
- SKIP (top right, from step 1) ends the forced flow, marks it complete, and still grants Kael.

## Locked, not hidden

- Every feature is visible from day one.
- A locked feature shows its building label greyed with a lock icon plus a gold tag
  "UNLOCKS AT 1-6". Tapping it shows that same line, nothing else.
- On unlock, a one-time "new" dot appears on the feature until it is first opened.
- The unlock table lives in ONE content config file, not in scattered checks:

| Unlocks at | Feature |
|---|---|
| Start | Campaign, Summon, Heroes, idle rewards |
| 1-4 | Friends |
| 1-6 | Fusion |
| 1-12 | Talents, Armor and Forge |
| 2-1 | Guilds |
| 2-4 | Proving |
| 2-8 | Recital |
| 3-1 | Relics (endgame by design) |

- Gating covers every entry point to a feature: hub buildings, bottom nav, hero detail buttons,
  deep links. A harness check enumerates the entry points and proves each respects the table.

## Instruments

- The economy walks and the power-band and CEIL measurements respect the unlock table: no relics
  before 3-1, no fusion before 1-6, and so on.
- Report exactly how the harness numbers move. Do not retune content to compensate; a separate
  retune follows this work.
- If `CheckFusionNeverGates` or another check newly fails because of the unlocks, report it with
  numbers rather than changing the walk.

## Verification

1. `Tools/build-check.ps1`, reported against the two known failures (the CEIL band and the power
   band).
2. `PlayModeScreenshot` captures, forced offline, on the scratch identity starting as a NEW
   player, at 1920x1080 and 2400x1080: wake, the Cinder recall, the recall result showing Kael,
   one forced-arrow step, the hub with locked features, and a first-time tip.
3. Harness checks:
   - a new save runs the scripted flow and gets Kael exactly once;
   - SKIP grants Kael and completes;
   - resume after quitting mid-step;
   - an existing progressed save never sees the tutorial;
   - the summon pool and pity state are untouched by the recall.
