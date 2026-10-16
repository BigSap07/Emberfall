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
   4. campaign 1-2 (added as built, see "As built": the level up is unaffordable after 1-1)
   5. level up a hero once
   6. collect idle rewards
5. **Free play.** From here, light touch only: a one-line tip the first time each major screen
   opens, dismissed by any tap, never shown again.

## Rules for the forced steps

- Only the highlighted control accepts input. Everything else is dimmed and inert.
- A six-pip step indicator shows progress through steps 1 to 6 (five when this was decided).
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

## As built

Recorded with the code that builds it. Where the build had to decide something the spec left
open, or found the spec at odds with the content, it is said here.

**Where it lives.** `Onboarding` in `Assets/Scripts/Core/Session` holds the flow: a counter,
`PlayerProfile.TutorialStep`, of steps COMPLETED (wake, recall, prologue, then the six guided
steps), saved the moment each one finishes. `OnboardingCoach` in `Assets/Scripts/UI` draws
whichever step the profile is on and notices when it is done; it holds no state of its own, so a
relaunch shows exactly the step after the last completed one. Every word is in
`Assets/Resources/Content/onboarding.json`, as is the unlock table. That file is read by no fight
and is not hashed into `ContentDatabase.ContentVersion`, for `guild-path.json`'s reason.

**The recall.** Kael (`kael_the_unyielding`) is a starting hero in `heroes.json`, so a new player
would own him before the Cinder recalled anyone. `Onboarding.CreateKeeperProfile` is the ordinary
new profile with Kael held back, and `Onboarding.Recall` puts him back exactly where
`GameSession.CreateNewProfile` had him: his uid, his place in the roster, the starting banner.
After the recall the save is byte for byte the ordinary new profile, which is why no economy walk,
stage fingerprint or sweep moved. It never reads or writes `TotalSummons`, shards, gold or the
rates, and the pulls after it match an ordinary profile's pull for pull.

**The prologue battle** is one of `PrologueScript`'s existing steps with its existing squad,
named in `onboarding.json` (`"prologue": { "step": ... }`). The spec says "the existing prologue
battle" and the prologue has five fights; the build plays the full-line battle (`fullBattle`),
which is winnable, rather than the boss, which is the scripted loss. Change the one word to play
another. The old eight-step prologue sequence and its cards are gone.

**The guided steps** light one control per screen and route the player there, screen by screen:
team (the Hub's HEROES, then back out of the banner), campaign 1-1 (CONTINUE CAMPAIGN, ENTER
BATTLE), the wave result (NEXT WAVE until 1-1 is cleared), campaign 1-2 (`Onboarding.StepSecondStage`,
the same route as 1-1), a level up (HOME, HEROES, VIEW ALL
HEROES, the first hero, LEVEL UP) and the idle claim (back to the Hub, the chest, CLAIM). Kael
already stands in the line after the recall, so the team step's "placed automatically" holds
with nothing done; if he is taken off the banner, he is placed again when the step completes.
Where a screen has no control on the route, the step says its line and leaves the screen usable
rather than lock the player out. The back key does nothing during the forced steps.

**RESOLVED: the level-up step was unaffordable where the spec put it.** All three starters are
5-stars whose first level costs 1070 gold. A new player holds 400, and 780 after clearing 1-1.
The first build routed on down the campaign while no level was affordable (a `short` line in
`onboarding.json`, the arrow on CONTINUE, `CanLevelAnyone`) and came back to LEVEL UP after 1-2,
when the player holds 1235. The decision was to move the step, not cheapen the level: the
second stage is now its own guided step (`Onboarding.StepSecondStage`) between the wave result and
the level up, the detour and `CanLevelAnyone` are gone, and no content number changed. The order is
team, 1-1, wave result, 1-2, level up, idle. `CheckOnboarding` prints the gold at the level up step
(1235 against 1070) and asserts it is enough.

**The off switch.** `OnboardingSwitch.Enabled` is true by default, and the scripting define
`EMBERFALL_ONBOARDING_OFF` makes it false. `Onboarding.IsComplete`,
`Onboarding.CreateKeeperProfile`, `OnboardingCoach.Install` and `FeatureGate.UnlockStage` read it.
Off, a new save is `GameSession.CreateNewProfile`'s profile marked complete, no coach appears and
every feature is open. `CheckOnboardingSwitch` proves that profile byte for byte equal to the one
`main` made before onboarding, excluding only `tutorialStep` and `seenTips`. A fresh start with the switch off runs
`FirstRun.Begin` exactly as before onboarding, from the restored `FirstRun.cs`; with it on,
`GameApp` installs `OnboardingCoach` and `FirstRun` is not shown.

**SKIP** ends the forced flow from any step, marks it complete and still gives Kael once. It
leaves the free-play tips to come, since a player who skipped the guided loop is the one a tip is
most for. The migration's "complete with all tips seen" is for progressed saves only.

**Tips** are keyed by screen name. A screen met inside the forced flow spends its tip, because
the flow taught it. Seen tips and opened features share `PlayerProfile.SeenTips`: a feature's
one-time "new" dot clears on its first opening (`FeatureGate.OpenedKey`), so no third field was
needed.

**The save** is version 21: `tutorialStep` and `seenTips`. A pre-21 save with any cleared stage
or a stage in progress loads complete, with `*` in `seenTips` standing for every tip including
ones added later. A pre-21 save with no progress starts at the wake; it already owns Kael, so its
recall gives nothing. The server's `save_limits` row must be raised to 21 before a cloud player
ships this.

**Unlocks.** A feature opens the moment its stage is CLEARED. `FeatureGate` reads the table and
nothing else decides. `FeatureEntryPoints` lists every door into every feature (the router's
screens, the Hub's buildings, nav cells, rail and card, the panel tabs, the hero page's TALENTS,
ASCEND and armor slots, the talent page's locked cards, the inventory's RELIC tab, the
formation's GUILD TECH), and the UI passes each door's id to `FeatureGates`. `ScreenRouter.Show`
refuses every screen of a locked feature whoever asks, which is the door a deep link would use;
this build has no deep-link handler. Armor is the Inventory (the Bag) as a whole; relics are its
RELIC tab and the Relic Shop.

**Editor captures** keep their old behaviour: the first-run suppression that every capture tool
already sets also opens the gate, so a capture of the Forge on a fresh scratch profile is still a
capture of the Forge. `PlayModeScreenshot`'s `OnboardWake`, `OnboardRecall`,
`OnboardRecallResult`, `OnboardArrow`, `OnboardLocks` and `OnboardTip` targets leave the first run
on, delete the scratch save and refuse a session that did not boot as a new player.

**Captures, 2026-10-12.** `PlayModeScreenshot` on a new scratch player. The wake is captured at
both sizes and is current. The recall, the recall result, a forced-arrow step, the Hub with
locked features and a first-time tip were each photographed correctly once at 1920x1080 and
looked at; a later batch meant to retake all six at both sizes after three small layout fixes
(SKIP moved below the top bar, the dim deepened to the reveal scrim, Friends' dot moved onto its
disc, the tip's scrim deepened) came back as stale frames of the wake, because the editor's Game
view stopped repainting. The recall then came back stale a second time and the run was stopped
there, by the rule that a capture showing the wrong screen twice is reported rather than retried.
Those five are owed at both sizes, from a focused editor.
