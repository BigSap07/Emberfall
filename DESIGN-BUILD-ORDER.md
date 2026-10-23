# Emberfall - build order

**Current status only.** Everything this file used to carry about how the project got here now
lives in [`Docs/archive/BUILD-ORDER-HISTORY.md`](Docs/archive/BUILD-ORDER-HISTORY.md), verbatim and
unedited. The archive is the reasoning: why a mechanic is shaped the way it is, what a pass
measured, what it refused to build and why. This file is the state: what exists, where it lives,
what is moving, and what is owed.

Checked against the repository on 2026-09-23, after the roster reached 94 of 94, the five-star
review and the Dispel repair closed the open kit decisions, every talent was measured for what it
costs its own hero, and ENGINE-GAPS pass 3 said the nine clauses pass 2 left, turned `buffsOnly` on
and re-solved the campaign once at the end, and the economy model started summoning at the real
placeholder rates in `balance.json` and the campaign was re-solved against them, and then after the
fusion and ascension spine (star-gated level caps, conversions, ascension) was built and measured
against the campaign as committed, and then after the battle stats, the battle HUD, the 336 by 396
sprite cell and the first session with its unlock table were merged into one tree and measured
together - against a live `Tools/build-check.ps1` run, a live
`validate_heroes.py` run and the files themselves, not against this document's own prior claims.
Where this file and the archive disagree, this file is right about status and the archive is right
about reasoning.

## How to read this file

- A system entry says what is **built and observable in the tree**. If a system is not listed here,
  it does not exist.
- **In progress** names live branches. A branch name is only ever cited there, only while it is
  unmerged, and it goes away the moment the work lands and the files can speak for themselves.
  None is live now.
- Numbers here are measurements from a real run, not estimates. The two that matter most are the
  harness failure count and the validator failure count, and both are named below.

## The instruments

Two runs decide whether the tree is healthy, and they measure different things.

- **`Tools/build-check.ps1`: all checks pass, measured from a clean run of the tree after the R4
  economy proposal was merged.** Re-measured 2026-10-23 by the integration that merged the launch
  prep, the security review, guild dungeon size, leaderboards and notifications and the content pass,
  on Linux under Mono with PowerShell 7 (see **Tooling**), the full check with the 60-walk guard:
  **3,707 checks, 0 failures, exit 0** on `main` before it, and **3,778 checks, 0 failures, exit 0**
  after it, the owner's decisions of 2026-10-23 included. It lints the docs,
  compiles the core and the Unity-facing code against the shim in `Tools/UnityShim`, checks that
  nothing of the Dev Arena reaches a release build, checks that editor automation cannot reach the
  real cloud (the offline-switch stage, under **Tooling**), and runs `Tools/DevHarness`. Until the
  economy merge it carried two harness failures, both reading the 72-stage campaign against
  numbers carried by position from the shipped 24: **the geared CEIL band** failed first at `1-3`
  (100% against [78%, 92%]) and **the campaign power-band guard** had eleven stages out of their
  band. The merge re-solved the campaign from `1-7` on and re-expressed both checks (**The campaign
  power curve, and the walls**); the history below is how the two failures arose. The balance line is Kael, Kiva, Isara, Adric and Orelle, and the pinned line the CEIL is read with is
  Kael, Aeron, Nereia, Seraphine and Isolde. ENGINE-GAPS pass 3 turned `buffsOnly` on, which took
  chapter 1's finale CEIL from 68% to 27% and four walls out of the power band, so
  `Tools/solve-recommended-power.ps1` was re-run once, after every other change had landed: 22 of 24
  stages moved, 18 of them in the authored roster, and every `recommendedPower` rose by up to 9%.
  Two stages were then moved by hand with the solver's own kind of move: 1-11's Hollow Husk down
  one level, where the solver's 600-sample reading and the gate's 200-sample one disagreed at the
  CEIL band's edge, and at 1-10 the three units under its Stone Warden two levels down. 1-10 is
  floor-pinned, which the solver does not measure, and the re-solve had left it reading 55% at its
  ceiling; it reads 83%. Lowering 1-10's ceiling instead fixed that and pushed 2-12 over its band,
  because the simulated player's losses at 1-10 change the whole economy path after it: across the
  moves tried there, 2-12 read anywhere from x0.98 to x1.04.
  **The campaign was then re-solved against real summon rates.** Until then the economy model
  summoned a five-star every time, so every number above was measured against a roster no player
  gets. At the 20 / 30 / 30 / 17 / 3 rates in `balance.json` the simulated line is weaker, and
  `1-9` (x1.00), `1-12` (x0.96) and `2-1` (x1.10) had slid just under their bands. The solver moved
  18 stages and put all three back on their band middles. It left `1-12`'s geared CEIL at 53%,
  under the finale band's 55%, where its 600-sample reading said 62%, so one of `1-12`'s Grave Moths
  went down a level by hand, and it reads 57.5%. **The two readings were one instrument and the 53%
  was luck.** `DevHarness.exe --ceil-reconcile` fields both lines and reads each at the other's
  seed: the solver and the guard build the same five heroes at the same levels and power (8,627), in
  the same gear, against the same ceiling battle, and each reproduces the other's number exactly at
  the other's seed base. They differ only in seed base and sample count. A 5,000-sample reference
  read 60.8% before the hand move and 61.7% after it, so the Grave Moth moved the truth by under a
  point and moved the guard's draw from its low tail to 57.5%. Across all fourteen stages the guard
  grades, the solver sits within 1.5 standard errors of a 4,000-sample reference everywhere and the
  guard falls outside two once, at `1-9` (80% against a reference of 87.8%). That one level moved `2-12` from x0.98 to x0.95,
  just under its band, through the armor the simulated player happens to pick up on the way
  (4,807 to 3,772), so the solver was run again on top of the hand move. It changed only chapter 2,
  and `2-12` reads x0.99. The solver and `build-check.ps1` compile the harness from the same file
  list, and `CheckSolverAndGateCompileOneList` asserts it: it reads both scripts, expands every
  variable and glob against the tree, and fails if the two resolved lists differ or if any `.cs`
  file directly under `Tools/DevHarness` is missing from either.
  **The power-band guard allows 0.005 past either edge** (`BandTolerance`) **and prints ratios to
  three decimals.** `2-1` once failed the build by 0.09% and printed as "x1.1 under x1.1".
  **The unlock table moved both, and nothing was retuned to answer it** (`DESIGN-ONBOARDING.md`).
  Before it, the CEIL band failed first at `1-7` (62.7%) and the power band had ten stages out, both
  walls of every chapter from 2 to 6, from x1.097 over x1.091 at `4-12` to x1.189 over x1.135 at
  `3-12`. The walks and the pinned lines now spend a feature only once a player arriving at the
  stage has it (`FeatureGate.IsUnlockedArriving`): no conversion or ascension before 1-6 is
  cleared, no armor worn before 1-12, no guild tech before 2-1, and the walks never used relics or
  talents. Measured against the base run:
  **the geared CEIL band now fails first at `1-3`** (100% against [78%, 92%]): 1-3 to 1-5 were
  floor-pinned while the line wore armor and are graded now that it cannot. Chapter 1's CEIL moved
  1-6 86.8% to 71.8%, 1-7 62.7% to 74.1%, 1-8 87.2% to 94.1%, 1-9 83.6% to 71.4%, 1-10 83.8% to
  55.3%, 1-11 42.6% to 1.8% and 1-12 68% to 67.2%; chapter 2 onwards did not move. **The power
  band has eleven stages out, not ten**: `1-12` and `2-1` are new, `4-12` came back inside (x1.095
  within tolerance), and the other nine moved by up to x0.027 (3-12 x1.189 to x1.207, 6-6 x1.168
  to x1.195, 2-12 x1.142 to x1.118). The reference walk's 1-12 wall entry fell from x1.074 to
  x1.003. `CheckFusionNeverGates` still passes (all 20 reference walks finish; the 1-6 wall's
  worst case went from 3 extra sittings to 4), `CheckGuildTechNeverGates` and
  `CheckStageWinnability` still pass, and `CheckEconomyRespondsToIncome`'s response rose from 0.6
  to 0.89. No pinned baseline moved: the recall leaves a new player byte for byte on the profile
  every walk starts from. The merged tree reproduces the same two failures at the same values.
  **The owner accepted these values as the known failures until the economy merge retuned
  `recommendedPower` and the stage levels, which cleared both.**
  **Any failure now is a regression.** Diagnose it; do not add it to a list. One caveat that is not
  a regression: a turned-around tripwire goes red if the content it names stops using what it
  names.
- **Doc lint is clean.** `Tools/doc-lint.ps1` scans 80 tracked markdown files and reports 124
  baselined violations and **0 new** (measured 2026-10-22). The baseline is the known debt in
  `Tools/doc-lint-baseline.txt`; the zero is the number that has to stay zero.
- **`Tools/HeroValidator/validate_heroes.py Assets/Resources/Content/heroes.json`: 0 failures.**
  It was the scoreboard for the authoring work and the authoring work is finished. It read 86
  before any kit was written, 70 before the 5-star pass, 56 when the 4-star pass opened, 30 when
  all 26 of those were authored, 12 once the 3-star tier landed, and 0 now that the 2-stars and
  1-stars have real kits. **Every one of the 94 heroes carries an authored kit**, and no hero in
  `heroes.json` has a `placeholder` in an ability id any more.
- Two further instruments exist and are not run by `build-check.ps1`, because both need the live
  Supabase project: `Tools/supabase-rules-test.py` (accounts, saves, friends) and
  `Tools/supabase-guild-rules-test.py` (guilds). Both last reported clean.
- **A screen is not verified until it has been photographed.** Nothing in the harness can see a
  layout. `PlayModeScreenshot` and `ShotOnDemand` are the instruments for that, and a commit that
  moves a screen is not done without one.

### The pinned baselines

Three files in `Tools/DevHarness` hold what the harness compares against, and a moved line in any
of them needs a stated reason in the commit that moves it.

- **`stage-fingerprint-baseline.txt`: 216 lines.** One per shipped stage per sampled seed (72 stages,
  three seeds), over the
  stage's opening battle fought by a fresh profile's starting three. The ENGINE-GAPS content pass
  moved all 72, Kael's Unusable Cover and Kiva's Broken Links being in every opening battle.
  **ENGINE-GAPS pass 3 moved all 72 again**, attributed by regenerating at each step: `buffsOnly`
  moved 69 (Kiva's Broken Links stopped banking off her own Forsaken), and the re-solve and the two
  hand moves after it moved the other 3 and 51 of the 69 again. Its nine clauses and its Taunt-anchor repair moved none.
  **Defect pass 3 moved none**: no body it touched stands in a fresh profile's opening battles.
- **`hero-sweep-baseline.txt`: 470 lines.** Five seeds for each of the 94 authored heroes, which is
  the whole roster. **ENGINE-GAPS pass 3 moved 55**: its clauses and two renumbers 17 (Aminah 5,
  Marrow 5, Tavia 5, Velis 2); the Taunt-anchor repair 8 (Hod 5, Wynn 2, Clara 1) and Marrow's 5
  again; `buffsOnly` 30 (Hale, Kiva, Mireth, Seraphine, Sylven and Vanno, 5 each) and Tavia's 5 again.
  **Defect pass 3 moved 18**: Corin 5 (Settling reaching its limit), Darian 5 (Hidden Strength
  striking the enemy it names), Hale 5 (Tracking Habit at round end) and Seraphine 3 (a named body
  no longer answered by the sweep's Taunt holder). **The Quarry clock repair moved Hale's 5 again.**
  **The round-end repair moved Finn's 5**, Aftershock Clock delaying his Ringing enemies at last,
  and moved no talent line and no stage fingerprint.
- **`talent-sweep-baseline.txt`: 450 lines.** All 90 talents at five seeds each, every one compared
  against the same hero at the same seed with nothing picked. **ENGINE-GAPS pass 3 moved 134**: its
  clauses and renumbers 59 (Aeron III, all three of Aminah's and Marrow's, Arden I, Cyrra II, Nereia
  II, and Velis I, II and III); `buffsOnly` 75 (all three talents of Hale, Kiva, Mireth, Seraphine
  and Sylven); the Taunt-anchor repair moved Marrow's 15 again. `Docs/ENGINE-GAPS.md` pass 3 has
  every group. **Defect pass 3 moved 51**: Darian's and Hale's 15 each, for the reasons their sweep
  lines moved; Nysera II's 5, False Target striking the enemy her Link turned the blow onto; and
  the named-body repair, Seraphine's 9, Mireth III's 4 and Aeron I's 3. Defect pass 3's lines are
  attributed by which change touches each kit, not by regenerating after each step; the
  named-body ones are the only kits in either file that change touched alone. **The Quarry clock
  repair moved Hale's 15 again**, and nothing else.
- **The fusion spine moves 10 hero-sweep lines and 30 talent-sweep lines and no stage
  fingerprint**: Aminah's and Caldus's five seeds each, and all three of each one's talents, from
  the F8 lifts under **The star band**. Caps and ascension move none, because a fingerprint is a
  fresh starting three at level 1 and a sweep fights at a fixed level.
- **The wall-resolve branch moves all 72 stage fingerprints, 10 hero-sweep lines and 30
  talent-sweep lines**, regenerated as its last commit from a fresh rebase on `main`. The sweep
  lines are Kiva's and Velis's five seeds and all three of each one's talents, from the Striker
  pass. The fingerprints are two causes that were not regenerated apart: Kiva stands in every
  opening battle, and the re-solve moved the authored roster of 20 of the 24 stages.

## Systems

### Combat

`Assets/Scripts/Core/Combat`. `CombatSimulation.Run()` resolves a whole fight up front and returns
an ordered list of `BattleEvent`s; the battle screen is a player for that list and computes nothing.
`CombatSimulation.Begin()` returns a live `BattleSession` for step-by-step resolution, and `Run()`
is literally `Begin()` followed by stepping to the end, so there is no second resolution path.
Deterministic: a seed plus two lines through `DeterministicRng` is the whole fight.

The engine grew to meet the authored kits rather than the kits being trimmed to fit it. Statuses,
named marks, ledgers, charges, turn shifts, concealment, per-target budgets, broadcast triggers,
death dispatch, two-condition gating and a talent layer are all in. The archive carries the full
account of each, including what was deliberately not built: no ATB gauge, no accuracy system, no
apply-a-status-by-hand entry point.

The 4-star pass added exactly two engine members, `HighestEnergyAlly` and `LargestShieldAlly` on
`TargetMode`, appended at the end of the enum so no existing ordinal moved. **The 3-star and
low-star passes added none at all.** Everything else those tiers needed already had a shipped
occupant. Where Pinoy's text could not be said, each batch approximated it, marked the clause draft
and recorded a row rather than growing the engine; `Docs/ENGINE-GAPS.md` is the whole record and is
now a closed list, because no hero is left to author.

**The fidelity pass (ENGINE-GAPS pass 2) added three opt-in fields**: `PassiveDef.ChargeOnLanding`,
which gives a limited passive its allowance back when a firing lands nothing, and
`PassiveDef.BuffsOnly` and `EffectDef.BuffsOnly`, which stop a listener or a buff move treating a
Mark, Ledger or Link as a buff. `chargeOnLanding` is set on 27 shipped passives, and **`buffsOnly`
on twelve since pass 3**: the eight listeners whose text names a buff and the four moves C13 named.
Pass 2 also repaired `TargetMode.LastAttacker` inside a broadcast about another body, which moved
Caldus and nothing else, and the content pass after it added `EffectCondition.StatusStacksBelow`
and `EffectCondition.SubjectHadStatusWhenStruck`, which let Oris's sequence replace his basic and
Elyra's Patience ask about the body her blow found, and `PassiveTrigger.OnEnemyShielded`, the
enemy-side mirror of `OnAllyShielded`, for Hale.

**Pass 3 added, all appended so no ordinal moved:** `PassiveTrigger.OnAllyTurnStart`;
`EffectCondition.LastReleaseAtLeast`, `SubjectRepaidDebtAtLeast` and `HarmfulStatusesAtLeast`;
`EffectDef.DispelTakesTrigger`; and a `SubjectLostStatusThisFiring` that can name the kind it asks
about. It repaired three rules: an auto-release that expires empty records zero rather than the
last release's figure; a converting Ledger's limit is spent per application (`StatusInstance`'s
`ConvertUses`), so Hollowed converts once; and **a `BroadcastSubject` on the caster's own side is
returned before the other side's Taunt, Provoke and concealment**, which had been sending heals
aimed at an ally to the enemy Taunt holder. `attackCap` joined the fields `TalentMath.TryApplyDelta`
changes. The harness sections ENGINE GAPS PASS 2 - FIDELITY and ENGINE GAPS PASS 3 prove all of it.

**Defect pass 3 added, appended so no ordinal moved:** `TargetMode.RedirectDestination`, the body a
redirected share lands on, which Nysera's False Target aims at instead of Nysera; and
`EffectDef.RefreshStacks`, which makes a new stack refresh every stack its caster already holds on
that body, so Corin's Settling can reach its limit of three and Weight of Time can stun. It repaired
two rules: **a damage-shaped Ledger payout strikes the target it names** (`LedgerAutoReleaseTarget`),
as a heal-shaped one always did, which had Darian's Hidden Strength striking Darian; and **five
modes that name one body the engine already chose return before the Provoke and Taunt anchor on
either side** - `BroadcastSubject`, `DefeatedUnit`, `ThresholdUnit`, `SwapAdvancedUnit` and
`SwapDelayedUnit` - completing the own-side repair above. `CheckNoAbilityHitsItsOwnCaster` fails if
any ability lands damage on its own caster without naming Self.

**R16 counterattack rule: a counterattack answers the attacker, not Taunt.** `TargetMode.LastAttacker`
is the strike-back mode (Jory's Worn Hinges, Caldus's Defined Duty and No Uncertain Ground, the guild
line's Answer in Kind). In `ResolveTargetsInner` a live, legal attacker now returns before the
Dev Arena's forced pick, the Provoke and the Taunt anchor, exactly as the five named-body modes above
do. When the attacker is dead, excluded or was never recorded, the effect falls back to `FrontEnemy`,
and that fallback is an ordinary pick that respects Taunt. `LastTarget` is deliberately not in the rule:
it names the body the caster last hit, which is a follow-up and not a strike back, so the anchor still
outranks it. `EnemyThatLastDamagedAlly` is not in it either: it is an authored active (Maera's Keep the
Beacon) that picks, and its card says which body it picks. The cards keep their text, because the
text always said the attacker. `CheckCounterAnswersTheAttackerNotTaunt` proves it on a synthetic arena
(a counter goes to the attacker with a Taunt holder standing; the same hero's ordinary attack still
goes to the Taunt holder; the control with no Taunt agrees). Compiling with the define
`EMBERFALL_COUNTER_ANSWERS_TAUNT` restores the old resolution exactly (the anchor outranks
`LastAttacker` again); the default build has the new rule and needs no define.

**The round-end repair: `RoundEnd` reads the round it closes.** `CloseRound` used to release the turn
order before raising `RoundEnd`, and `ApplyTurnShift` returns on a missing order ahead of the branch
that carries a delay into the next round, so Finn's Aftershock Clock fired every round and never
delayed anyone. The order now outlives the boundary, with the cursor past its end, so every unit
reads as having acted: a delay is carried to the top of the next round, an advance is dropped for
the reason `CarriedDelay` gives, and turn progress reads 0. `BattleSession.EndRound`, which ends a
round by hand, had skipped `RoundEnd` outright. It now closes the round through `CloseRound`.
**The sweep for the same shape was a measurement, not a reading**: every battle the harness fights
was run with each turn-order reader (`ApplyTurnShift`, `ApplySwapTurnOrder`, `PendingPositionOf`,
which `TurnProgressOf` goes through, and `NextAllyInOrder`), every condition and every target mode
recorded at `BattleStart`, `RoundStart` and `RoundEnd`. Before the repair exactly one rule reached a
turn-order reader with no order: Aftershock Clock, 326 delays dropped. The only other rule answering
`RoundEnd` is Hale's Tracking Habit, and what it reads (`DamageDealtThisRound`) is cleared in
`CombatUnit.BeginRound`, after the boundary, so it already worked. **One latent hole is left
unrepaired, on purpose**: `OpenRound` raises `RoundStart` before building the order, so a turn shift
authored at round start would be dropped the same way. No shipped rule does that. Repairing it means
choosing whether a round-start buff to Speed reorders the round it opens, which is a rules decision
rather than a defect.

### Content

Twelve JSON files in `Assets/Resources/Content`. Balance and content changes go there, not into code.
**Eight are hashed into `ContentDatabase.ContentVersion`** (heroes, enemies, campaign, balance,
relics, events, armor and floor conditions), so an edit to any of them retires every recorded guild
dungeon replay. **Four are not**, because no fight reads them and each loads after `Load`:
`guild-path.json` (`LoadGuildPath`), `guild-tech.json`, `onboarding.json` (`LoadOnboarding`, read
through `ContentService`) and `vfx.json` (`VfxConfig`, which `ContentDatabase` never reads).
`guild-dungeon-scale.json` (`LoadGuildDungeonScale`) is not hashed either, for the same reason: it
says how many floors a guild has, never what a floor is.

- `heroes.json`: 94 heroes, **all 94 authored**. 8 one-star, 12 two-star, 18 three-star,
  26 four-star, 30 five-star.
- `enemies.json`: 96 enemy templates. **Pinoy's 84 are the campaign**: aspect, kind, stats and kit
  are ours and the mapping is `Docs/CAMPAIGN-SPEC.md` section 9. Each of the 84 names its sprite.
  **The ones with a battle sheet in `Assets/Resources/Sprites/Units` carry no `spriteStatus`; the
  rest are `pending`** and draw the mannequin. **The shipped 12 are retired but kept**: no
  stage fields them, so neither the guild dungeon nor the Deep Recital draws them, and they stay in
  the file because `PrologueScript` spawns seven of them by id. **The visual `scale`,
  `spriteBodyFraction` and `spriteFootFraction` fields are inside the hash**: the 336 by 396 sprite
  cell changed the `visual` block of 22 enemies and nothing else in the file, and that retired every
  recorded guild dungeon replay once. It is presentation only and no fight reads it.
- `campaign.json`: **Pinoy's six chapters of twelve, 72 stages** (The Forgotten Road, The Abandoned
  Crossing, City of Strangers, Beneath the Old Kingdom, The Divided Realm, The Forgotten Sanctuary),
  fielding only the 84. Stage 6 of each chapter is `"miniboss": true` (`StageDef.IsMiniboss`) and
  stage 12 `"boss": true`. Written by `Tools/_author_campaign_stages.py`, which validates every
  line; every stage's `"proposed"` names what in it is ours rather than Pinoy's
  (`CAMPAIGN-SPEC.md` 10.5 is the list for him). **Nothing mechanical is solved**:
  `recommendedPower` and every level are seeds carried by position, and chapters 3-6 pay 2-12's
  rewards flat (`CAMPAIGN-SPEC.md` 10.3). A stage's ladder is `waveCount` times 12 battles: 36 on
  `1-1` to `1-4`, 72 on `1-5` to `1-12`, and 144 from chapter 2 on, 9,360 in all.
- `relics.json`: 10 relics across two grades, Legendary and Mythic.
- `armor.json`: drop chance, primary and sub rolls, tempers, attunements, slot names.
- `events.json`: 2 events. Its two windows are real calendar dates sitting in the file, not
  sentinels, and the file's own comment still calls them first-pass and untuned. Nobody should
  treat them as an announcement schedule without deciding to.
- `balance.json`: every tuning constant the engine and the economy read.
- `floor-conditions.json`: the guild dungeon's floor conditions. There are 6, across the resource, healing
  and composition families, and each lists the floor themes the harness measured it passing on. See
  **Guilds, phase one**.

### Heroes and kits

**Every hero has a real kit. The roster is authored at 94 of 94**, and `import/Heroes.json` has no
design left waiting in it.

- A **5-star** is a basic, an active, the described passives (often authored as more than two, one
  per trigger a described passive answers), a stat-block passive and three talents. 90 talents in
  total.
- A **4-star** is a basic, an active and `passive1`, plus the same overflow `passives` list where
  one described clause answers a trigger of its own. **No talents and no stat-block passive**,
  because those are what a 5-star has and a 4-star does not.
- A **3-star** is a basic, an active and `passive1`, and nothing else.
- A **2-star** is a basic and `passive1`. It has no active skill, which also means it has nothing
  to spend Energy on; four of Pinoy's low-star designs pay the hero Energy, and each shipped kit
  keeps the amount and hands it somewhere it can be used. That is an open question for Pinoy rather
  than a rewording, and `Docs/PINOY-LIST.md` section 2c carries it.
- A **1-star** is a basic and nothing else.

The **star contract** is the authoritative kit rule and `validate_heroes.py` enforces it at import
time: 1 star is basicAttack only; 2 adds passive1; 3 adds activeSkill; 4 requires activeSkill and
passive1 with passive2 optional; 5 is the full kit including all three talents.

**Every ability carries player-facing text with no number in it**, written to
`Docs/ABILITY-TEXT-STYLE.md`: all 428 basics, actives, passives and talents on the 94 heroes, **with
no exceptions since the content pass of 2026-10-22**. The 28 that were still drafting notes (the
sentence Pinoy wrote and the kit that ships disagree) now say what their effects do, each sentence
checked against the effect data, and `AbilityTextPinnedDraftingNotes` is empty; whether any of those
kits should change to Pinoy's sentence instead is still his call, `Docs/PINOY-LIST.md` section 2f,
and the card text follows the kit if one does. The same pass audited every other description against
its effects (each status an ability applies is named, in the battle's word or the hero's own, and
every area effect says so) and found nothing to correct. Only descriptions changed in `heroes.json`,
so no fight, fingerprint, sweep, band or walk moved (a full `build-check.ps1` differs from the one
before only in those pinned counts and in `ContentVersion`, which hashes the file's text and so
retired recorded guild dungeon replays once). Nothing is pinned as unwritten: Nysera's False Target,
the last, has text since defect pass 3 repaired it to strike the enemy her Link turns a blow onto
rather than Nysera herself. `CheckAbilityTextIsPlayerFacing` enforces
it: a description that is empty, holds a digit or a percent sign, or reads as a drafting note fails,
and so does a pinned exception that no longer applies. **The text is shown now**, on the ability card
(below).

**The ability card (R17).** `AbilityCardSheet` is one layer over whatever screen asked, and it is the
same card from two doors: a tap on an ability on the hero page, and a tap on a unit in battle, which
pauses the fight while it is up. Per ability it shows the sentence (`AbilityDef.Description` or
`PassiveDef.Description`, read through `AbilityTextConfig.Prose`), then the facts `AbilityFacts` builds
(Energy cost, when a passive fires, how often, how many rounds a status or Ward lasts, how big a
status is), then one row per damage, healing or Ward figure from `AbilityMath`. It holds no game
logic. An ability whose authored description is a drafting note would read its sentence from
`ability-text.json` instead (`AbilityTextConfig`, not hashed into `ContentVersion`); **the file is empty
since the content pass of 2026-10-22**, which moved all 28 of its lines, checked against each kit, into
`heroes.json`. A description that already reads as player text never consults the file. `CheckAbilityCardTextIsComplete` proves every ability a hero owns has
non-empty text, and `CheckAbilityFactsMatchTheSimulation` (beside `CheckAbilityNumbersMatchTheSimulation`)
proves each printed duration, threshold, cost and status size equals what `CombatSimulation` does with
the same effect.

### The star band, and its floor

`validate_heroes.py` enforces the star contract's **shape**. `CheckStarBandByRole` in the harness
enforces its **size**, and without it a 4-star with a 300% active satisfies every other check in
the project while being a better hero than the 5-star beside it.

The band measures each authored hero's own output - damage dealt plus healing paid plus shielding
granted, credited by source - over the hero sweep's five seeds in the hero sweep's arena, and gives
each tier below five its own pair of edges against that role's authored 5-star median. Talents are
excluded because the sweep arena fields none, which is also why the 4-star ceiling is 0.85 rather
than 1.00: a 4-star reading nine tenths of a 5-star without that 5-star's three talents is not a
smaller hero.

**One rule sets every rung, and it was read off the tiers already authored rather than invented for
the ones below them: a tier's ceiling is the measured median of the tier above it, and its floor is
half that ceiling.**

| tier | band | where the ceiling came from | where the tier actually reads |
|---|---|---|---|
| 4-star | x0.45 to x0.85 | the talent argument above, not this rule | x0.49 to x0.78, median x0.61 |
| 3-star | x0.30 to x0.60 | the sixteen graded 4-stars read a median of x0.61 | x0.30 to x0.45, median x0.37 |
| 2-star | x0.18 to x0.36 | the thirteen graded 3-stars read a median of x0.365 | x0.20 to x0.30, median x0.225 |
| 1-star | x0.10 to x0.20 | the twelve 2-stars read a median of x0.205 | x0.11 to x0.13 |

The band's arena reads output where nothing is close. `Docs/PARITY-STUDY.md` reads the same 94
heroes in matched fights. Its rank agrees with the band's (Spearman 0.84), but parity compresses the
lower tiers, so these edges do not transfer to it. Heroes whose kits carry enemy-facing control or
debuffs sit about 0.16 of a 5-star median higher there than here, and tempo kits do not.

The floor is half the ceiling on the three lower rungs and 0.53 of it on the 4-star one, which is
where the halving was read from. The ladder of ceilings is 0.85, 0.60, 0.36, 0.20, a step of x0.71,
x0.60 and x0.56: **the step widens as the tier falls, and that is the shape of the roster rather
than a choice.** Three stars to two is the rung where the active skill disappears and two stars to
one is where the passive does, and a tier that loses a whole ability loses more than a tier that
loses a clause.

**A second reading asks the ordering question the band cannot**, because adjacent bands overlap -
x0.45 to x0.60 for four against three, x0.30 to x0.36 for three against two, x0.18 to x0.20 for two
against one. Every hero must also sit under its own role's median one tier up. That reading needs
no minimum sample, because it is a claim about order rather than about a middle, so it runs in the
ungraded roles too and it is the only thing holding the six Tinkerers below five stars that the
band does not grade.

**Why the low tiers' ability numbers are so much larger than Pinoy's.** The shipped 2-star and
1-star basics run to about 350% of Attack where the sentence in `import/Heroes.json` says 105% to
110%. Two things push the same way: a hero with no active skill spends every turn on one ability,
and it carries 0.64 of its role's 5-star stat total at two stars and 0.52 at one, so it takes fewer
turns before it dies. The tier ladder the roster already had says the same - a 3-star Bulwark's
basic is 251% against a 5-star's 110% - and this is its last two rungs rather than a new rule.

**The floor is enforced only where the median is one.** A role with fewer than three authored
5-stars prints its ratio and is named ungraded, because a floor is a claim about where the middle
of a role sits and one or two readings have no middle. That is Warden (1), Stalker (2), Binder (1)
and Tinkerer (2), and it will not change now that the roster is complete. The ceiling keeps running
for them, and so does the ordering reading.

**The 5-stars are the yardstick, and since ENGINE-GAPS pass 3 the yardstick is graded too.** Every
rung below five reads against its role's 5-star median, and until pass 3 nothing read the 5-stars
themselves, so one could move 44% - Marrow did - and redefine "in band" for every hero under it with
nothing going red. His move was toward his median (a Mender at x0.63, then x0.91 of Isara's
187832), so it happened to move nothing; the harness would not have said so either way. Now every
5-star in a role of two or more reads between **x0.78 and x1.64** of its own role's 5-star median.
One tier step is the measured x0.61; the floor is half a step down, below which a 5-star reads
closer to its role's 4-stars than to its own tier, and the ceiling a whole step up, because a high
5-star breaks no ordering and moves no median unless it crosses the middle. The derivation is
beside `FiveStarFloor`.

**It failed one hero on its first run: Aminah, at x0.50, under two of the three Mender 4-stars.**
Her numbers were Pinoy's; her kit does less than Marrow's on the same stat line. Her basic, its heal
and her active went from 105%, 55% and 155% of Attack to 170%, 115% and 230%, and she reads x0.86.
Nearest the edges now: Kiva at x0.83 since the Striker pass below, and Edrin x1.53, Darian x1.32 and Cyrra x1.37 above. **Hale
reads x0.84.** Defect pass 3 moved his Tracking Habit to round end and he fell to x0.61, under the
floor. The cause was the Quarry's clock, not his numbers: a Quarry named at round end kept whatever
it had left from the turn it was laid, so it often ran out before Hale's next turn. Tracking Habit
now lays it for two rounds whether it moves or stays (`transferDuration`), and he was not retuned.

**Umbra and Lumen 5-stars have a floor of x1.00 since the fusion spine (F8 in
`DESIGN-HERO-FUSION.md`)**: they are the aspects whose conversions take a seed, so their 5-stars
are the slow ones to fuse, and rarity with no payoff is a pool players learn to avoid. The aspects
are read off `balance.json`'s `conversionSeedAspects` rather than named in the check. Three read
under it. **Aminah and Caldus were lifted** by the smallest uniform step on every Damage and Heal
number in their kits - x1.12 and x1.05 - that puts each above the hero holding their role's median,
so each is the median now and it moved by under 1%. **Velis and Kiva were lifted together, and
`KnownF8Exceptions` is empty.** The Striker role has four 5-stars, so its median is the mean of the
middle two, and a hero inside that pair drags the yardstick he is graded against: Velis's smallest
whole-kit lift left him in the pair at x1.01 and dropped Kiva to x0.72. Each lift now carries its
hero out of the pair, so the median is Sylven and Vaelis alone (44,942) and neither can move it:
Kiva took x1.15 on every Damage number and reads x0.83, and Velis took x1.45 on his basic alone and
reads x1.04, because the same step on his whole kit put three of his clauses dark in the arena
matrix. Kiva's Unclaimed Victory crossed into a measured loss with it (-807 a battle, t -2.9, from
-410, t -1.7) and was on `KnownCostlyTalents` and `TalentAudit.Costly` as a trade. The talent pass
took it off both: two of its four effects were authored against a rate her kit never reaches, and it
answered OnKill, which is rarely hers. It answers a body falling now and reads +3491, t 7.8.
The check still fails if a pinned exception reaches the floor or any rarer-aspect 5-star falls
under it.
**One median moved in pass 3**: the Breaker median fell from Seraphine's 90536 to Rheia's 81541
when `buffsOnly` took the bookkeeping out of Seraphine's Contrary Evidence, which raised every graded
Breaker below five - Siofra x0.66, Tamsin x0.57, Aster and Avera x0.34 - and left all of them in
band. **Every graded hero on every rung is inside its band**: 16 four-stars, 13 three-stars, 8
two-stars and 6 one-stars, and 28 five-stars against their own medians. Tavia fell to x0.33 under
`buffsOnly`, because Dead Channel had been striking her own Exposed Frequency, and her basic and Cut
the Wire were raised to put her back at x0.49. The ungraded ones are printed and four of them sit
under where the floor would be if their role had one - Corin at x0.21, Demos at x0.30 and Iolan at
x0.40 against a single-hero Binder median, and Fenn and Till at x0.10 and x0.09 against a two-hero
Tinkerer one. Vanno, a 4-star in the ungraded Stalker role, reads x0.86 after `buffsOnly`, which is
under his median and above where a 4-star ceiling would be.

**The sweep's flimsiest filler has to be flimsier than the flimsiest hero**, and `CheckSweepFillerIsFlimsiest`
now says so. The arena's hitter aims at `LowestHpEnemy`, so the smallest body on the player line is
the one it works on, and the four fillers exist to stand beside the subject and contribute nothing.
FillerD's declared 600 built to 804 through `StatMath.StarMultiplier`, and Moss, Fenn and Till
build to 785, 790 and 790 - so for exactly those three the stat-stick was handing the hitter to the
hero being measured. All three died in round one of every seed and read 0 output at any ability
number: the subject was not small, it was absent. FillerD is 580 now, which builds to 777 and puts
the whole roster above it.

### Rarity reaches the file, not just the engine

Rarity used to reach a hero's numbers in exactly one place: the `rarityMultiplier` that
`StatMath.ForHero` hands `Build`, out of `StatMath.StarMultiplier`. `Build` multiplies three fields
by it - MaxHp, Attack and Defense - while Speed, CritChance, CritDamage, EffectHit and EffectResist
are flat pass-throughs. `heroes.json` then held its own star ladder in hp, attack and defense, so
those three were scaled twice and the flat five not at all, and a 4-star out-read a 5-star on every
one of the five.

The rule now written into the file is that **a non-5-star's stat total is a fixed fraction of its
role's 5-star mean**: 0.88 at four stars, then 0.76, 0.64 and 0.52 below that. Stat total is
`StatMath.PowerScore` of the line `StatMath.ForHero` builds at level 1, which already contains
`StarMultiplier`. The ladder sits within 0.02 of the ratios `StarMultiplier` already produced on HP,
Attack and Defence; what changed is that the whole line obeys it. `Tools/_rarity_stat_pass.py`
carries the derivation.

### Talents

A talent is an additional passive and nothing else. `HeroDef.Talents` holds up to three, the
selection is per-owned-hero save state on `HeroInstance.SelectedTalent`, and
`GameSession.SelectTalent` is the whole selection rule: free, no cooldown, refuses a slot the hero
does not author. It resolves once in `BuildUnits` and is appended as one more passive. A talent
selection travels into a Proving ghost and a friend duel, so a snapshot line fights with the talents
it names.

**Ascension unlocks the slots.** `HeroInstance.TalentSlotsUnlocked` is `min(MaxTalents,
Ascension + 1)`: tier 0 runs talent 1, tier 1 adds talent 2, tier 2 adds talent 3. It is derived
from a tier the save already carries, so no save version moved and every hero already owned is
gated retroactively with nothing to migrate. `CombatSimulation.BuildUnits` reads
`HeroInstance.LiveTalent`, so a locked slot is absent from a battle exactly as an unselected one
is - the whole record byte-identical to the same fight with nothing picked. `SelectTalent` refuses
a locked slot, but a selection already stored is KEPT and comes back whole when the tier does, the
rule an unresolvable relic id already follows. A snapshot carries no tier and can never re-derive
the gate, so it is applied at CAPTURE - `TeamSnapshot.Capture`, `ProvingGenerator` and
`GuildDungeon.LineOf` publish `LiveTalentSelection` - and `GuildDungeon.FightUnder` replays a
stored record with exactly the pick it holds. `CheckAscensionGatesTalents` measures every slot at
every tier as nine fights, off passive firings and whole-record comparison rather than off a flag,
and `CheckAscensionGateKeepsThePickAndTheRecord` covers the pick, the save, the publish and the
replay.

The talent sweep runs all 90 talents at five seeds each and pins 450 fingerprints in
`talent-sweep-baseline.txt`. **The talent selection UI is built**: `TalentsScreen` shows each talent's
sentence, what it changes in numbers through `TalentMath`, and what it did to this hero measured by
`TalentTrial`, with `TalentAudit` naming the audited traps and trades. `TalentAudit.Costly` is a copy
of the harness's list, and `CheckTalentAuditMatchesHarness` holds the two equal.

**A forty-fight trial could not see a gain of 1 to 4 percent.** A mirror fight is noisy: on the
reference save Kael I read +468 at t 1.69 and Kael II +33 at t 1.91, both under the two-standard-error
line, so the screen printed "no clear change" while the harness measured a real gain. `TalentTrial`
now runs in batches of forty and decides only at the end of a batch: a talent that reads clear, gain
or loss, is not fought again, and one that is still unclear gets another batch, to 120 fights. The
reading depends on the save and the content and on nothing else, so the same save gives the same
reading at any frame rate, and `CheckTalentTrialConfidence` holds that and holds Kael I as a clear
gain on the reference save. The cap costs a desktop CLR under a quarter of a second for a hero in
the worst case, about 115 ms on average. A reading still unclear at the cap prints as an estimate
with how often the talent changed a fight, "+3% (unclear, changed 11 of 120 fights)", never as a
bare "no clear change".

**A talent a player picks should not make their hero worse, and `CheckTalentsAreWorthPicking` says
which ones do.** It reads every talent against the same hero with nothing picked, paired seed for
seed over 40 seeds, in the sweep arena wherever the talent changes a sweep fight and in its dark
room wherever it has one (`DarkTalentRooms`, 30 pairs). The
quantity is the star band's: damage, healing and shielding, credited by source. A talent loses when
its mean sits more than two standard errors below zero. Five seeds could not answer this: the sweep
rolls damage variance, and a talent that shifts the dice stream moves a five-seed total by more than
most talents are worth. The reading is **88 gain, 0 sit within the noise or change nothing the
output total can see, and 2 lose**, and both are pinned in `KnownCostlyTalents` with a reason. The
check fails both when a talent starts losing without being listed and when a listed one stops. It
read 38 / 45 / 7 before the talent pass on `talents/make-them-work`; `DESIGN-TALENTS.md`, in the
`claude` folder, has what the forty-five were doing and why the output total could not see it.

**Every room it reads is a lineup a player can field, and that is asserted now.** Brenna I's room
fielded two Brennas, which `PlayerProfile.AssignToTeam` never allows, and her value was read off
that fight: +364 a battle, t 0.6. The same stage with the copy removed never lets her cap bind. A
search of 29,016 legal fights found it binding in 43, and her room is now the one where it binds
most often, beside Darian, Nereia and Rovan. It reads inside the noise there, and so does the
sweep now, and she gains in both since the talent pass deepened her own Lasting Guard
rather than the line's (+1011, t 2.8 in the sweep).
Hers was the only illegal room in the harness. That was measured by recording every battle the full
harness fights, not by reading the room lists.

- **There are no traps.** Both were rewritten rather than relabelled, because in each the
  PURCHASE was empty rather than the price too high. Kael III bought Rubble that lasts the battle
  while What Breaks Forward empties the pile on the next basic; it salvages a piece from every blow
  he lands now and reads +6535, t 8.5, where it read -1726, t -36.6. Mireth I bought two landings
  per enemy, and `firesPerSubject` is a whole-battle budget where the sentence meant one Focus's
  duration, so the trade was fewer landings than the once-a-round limit it replaced; it takes two
  entries off the page per landing now and reads +3317. Turning that limit off instead was measured
  and buys nothing at all: against a body that buffs every turn the passive already lands once a
  round, so unlimited lands the same fourteen times.
- **One is a trade**, the only entry on `KnownCostlyTalents`: its purchase is real and is not made
  of output. Nysera I strips a buff off the body targeting her, which in the sweep is a taunting
  tank's Provoke, and frees the whole line's aim. Marrow I, which spreads Promise over a third ally
  for a smaller Promise heal, was the second trade and is off the list: it gains in the sweep now
  (+3725, t 5.0). Three more were on the list and are not: Rheia I paid two Energy off every Applied
  Revision - her whole cast rate - for a rider on one clause; Rheia III's own entry said her casts
  refill the Revision it was buying back; Kiva III had two clauses no rate her kit banks at can
  reach, and answered OnKill, which is rarely hers. All three are gains now.
- **At parity the list reads differently** (`Docs/PARITY-STUDY.md`), and that study is what the
  1%-of-Attack materiality floor comes from: a sham talent worth +0.2% Attack cleared the plain
  t >= 2 rule for 24 of 30 heroes, so a reading has to be worth 1% of Attack in the same direction
  before it counts as a talent rather than a dice shift.

### Campaign and progression

`CampaignWaveGenerator` resolves a stage into its ladder, which is `waveCount` times 12 battles.
`GameSession` owns rank, levels, purse and the progression API. `Debrief` reads a loss back into a
reason a player can act on. Gold buys levels up to a cap that the hero's star tier and ascension
tier set and account rank does not (`GameSession.HeroLevelCapOf`); see **Fusion and ascension**.

**A wave resolves in one tap.** `GameSession.ResolveWave` fights the rest of the current wave
through `ResolveLadderBattle`, the helper `SweepClearedStage` shares, so every campaign battle has
one resolution path, and returns a `WaveResolution` that `WaveResultScreen` only reads: the
outcome, what the wave paid or the stage-clear spoils, the line that fought as `Squad` snapshots,
and on a loss the lost battle with its `Debrief` cause. A loss stops at the lost battle and the
next resolve starts there. **Every campaign win goes through `GameSession.RecordLadderWin`**, which
`ResolveWave` and the harness's economy walk both call: on a first clear a won mid-stage wave pays
an equal share of `GameSession.WaveDripPercent` (40, a named constant rather than a `balance.json`
key because that file is hashed into `ContentVersion` and no fight reads it) percent of the
stage's gold and Wayfarer XP, and the stage clear pays the rest through `GrantBattleResult`, with
the rounding remainder, the first-clear bonus, shards, drop and unlock. A stage's whole payout is
unchanged to the unit; repeat clears and sweeps drip nothing. Paid waves are read off the existing
cursor (`WavesPaidBefore`), so the save gained no field. `CheckWaveDrip` proves the totals for all
72 stages, no double pay across save reloads, and no pay on a replay, a repeat or a sweep. `CampaignFlow.Launch`
is the one door into a stage for `CampaignScreen`, `FirstRun` and `BattleScreen`'s CONTINUE: a
cleared stage is swept, a stage with a wave curve opens `WaveResultScreen`, and only a stage with
no ladder reaches `BattleScreen`. `ReportResolvesPerFirstClear` prints what a first clear costs;
`DESIGN-CAMPAIGN-STRUCTURE.md`'s 2026-10-11 amendment is the reasoning. **Cleared stages are
recorded by id** (`PlayerProfile.ClearedStageIds`): `CampaignProgress` derives the frontier, the
furthest stage, unlocks, idle income and the count from them, and `stagesCleared` is a derived
count kept for the summary readers. `CheckWaveResolve`, `CheckClearedStagesById` and
`CheckStageResumeAfterWaveResolve` prove it.

**Wall chests (R17).** Stage 6 and stage 12 of every chapter carry a fixed one-time chest, shown on the
campaign stage card before the fight and on both result screens. `WallChests` reads
`wall-chests.json` (`WallChestConfig`, not hashed into `ContentVersion`): the wall pays 1 hour of
frontier Gold and 12 Slag a chapter, the finale 3 hours and 30 Slag a chapter, the hours paid by
`GameSession.IdleYield` (its Gold half only) at the frontier the clear reaches. **A chest pays no Shards**:
with Shards in it, the extra Shards re-drew every later summon of the reference player and 1 of the 60 walks
that never convert and never ascend stuck at 3-4 (walk 51, seed 51000154: entry power 11,476 and 179 losses in
293 battles, where without Shards it finishes at 12,163 with none); `CheckFusionNeverGates` is the guard and a
chest's size is not a safe lever, since smaller Shard-paying chests each stuck a different walk. **It pays on the first clear, from
`GameSession.GrantBattleResult`, and there is no save field**: a chest is opened when its stage is in
`PlayerProfile.ClearedStageIds`, so a stage cleared before this shipped reads as opened and pays
nothing, and there are no retroactive chests. **The chests ship ON at 1 hour and 3 hours.**
At 1.5 and 4 hours, even paying Gold only, the reference walk 0's power band went over x1.25 at 2-7, 3-4 and
3-6 (x1.261 to x1.267), so the hours were cut: at 1 and 3 the band's highest is x1.226 at 2-6 (2-7 x1.209, 3-4
x1.188, 3-6 x1.183) and 0 of the 60 reference walks stick (worst 139.04 days). `"enabled": false` is the rollback
and a first clear then pays what it always paid, with no chest line on the stage card.
`CheckWallChestsContentAndCard`, `CheckWallChestsPayOnce` and `CheckWallChestsRollBack` assert the shipped default
is on at 1 and 3 hours, that exactly the 12 wall and finale stages show a chest line, and prove the payout and the
rollback, all on throwaway databases.

**Battle stats.** `WaveResolution.Stats` (a `WaveStats`) holds what each hero dealt, took, healed,
shielded and killed, per battle and summed over the battles that resolve fought. `GameSession.ResolveWave`
fills it with `WaveStats.Record` for every battle, won or lost, from the `BattleEvent`s before the
result leaves scope; `BattleTally.Of` in `BattleStats.cs` is the pure tally, crediting by source over
Damage, Heal and ShieldGain as `TalentTrial` does. It is memory only: not in the save, no version
moved, and no fight reads it. `BattleStatsView` is what the panel draws (rows, bar fractions, MVP,
scope, default scope) and `BattleStatsPanel` only paints it; `WaveResultScreen` opens it from STATS.
`Docs/ui-specs/BATTLE-STATS.md` is the spec and its two interpretations. `CheckBattleStats` proves
the totals against a straight sum over the events (`CheckBattleStatsClearedWave`,
`CheckBattleStatsLostWave`, `CheckBattleStatsStageClear`), the save byte-identical after the view
(`CheckBattleStatsLeavesSaveAlone`) and determinism (`CheckBattleStatsDeterministic`).

### Fusion and ascension

`DESIGN-HERO-FUSION.md` holds the rules, F1 to F9, and the reasoning; this is what the files show.

- **Caps** are `StatMath.HeroLevelCap` of star tier and ascension tier: 5, 6, 7 and 8 for one to four
  stars, and 9, 18 and 39 tier levels for a 5-star at tiers 0, 1 and 2. Account rank sets none of it.
- **Ascension** is `GameSession.Ascend`: copies of the same hero only (1 for the first step, 4 for
  the second), and the tier level resets to 1. **A hero's Level stays the one number its stat line
  is built from** - each tier walks the one level ladder at its own stride, x2.035 and x1.5
  Levels per tier level (`StatMath.LevelAt`) - because combat reads Level for Defense mitigation, and
  a multiplier on the stats instead made an ascended line of the same power clear `2-12`'s ceiling
  39% of the time against 63%. The level cost is read off the tier level, times the same multiplier.
- **Tier 2's stride is x1.5, the owner's decision of 2026-10-19 (R16), not the x6.175 the
  continuity rule gives.** A tier 2 five-star walks Levels 46 to 103 where it walked 46 to 281. The
  caps (9, 18 and 39 tier levels), where each tier starts (Levels 1, 10 and 46) and
  `ascensionCopies` (1 and 4) did not move; only how many Levels a tier 2 tier level buys did, and
  the level price with it. Candidate A of `Docs/BALANCE-REPORT-R13.md` section 4, one number in
  `balance.json`: it takes the fusing player's plateau over the reference curve from x13.9 to x15.6
  at the finales of chapters 4 to 6 down to x6.0 to x6.4 (x6.6 to x6.9 once the loosened curve of
  R16 asks the reference player for less; `Docs/BALANCE-REPORT-R16.md`, Fusion A).
  It moved `ContentVersion`, so recorded guild dungeon replays retired. It was migrated and not
  grandfathered, there being no players: version 18's level clamp takes a tier 2 hero saved above
  Level 103 to Level 103 on load, and `CheckTierTwoAboveTheNewCapLoadsClampsAndPlays` proves it
  through the real serializer (a tier 2 five-star saved at Level 281 loads at 103, power 31006 to
  12075, round-trips, and plays 1-1 and the last battle of 6-12).
- **Conversion** is `GameSession.Convert`: heroes below five stars worth 100 points (15, 10, 10 and
  18 per body by star tier) for a random 5-star of the chosen aspect. Ember, Tide, Verdant and Gale
  take same-aspect fodder; Umbra and Lumen take one seed of their own and fodder of any aspect.
- **Every number is derived, not declared, except one**: `CheckFusionEconomyDerivation` rederives each
  one from the summon weights, the hero growth rate, the starting line and a campaign growth of x7.611,
  prints the F6 crossover for every 5-star, and fails if `balance.json` disagrees. The growth was
  the finale's entry over the starting line until the wall rule made the finale an output of the
  caps; it is pinned at the value the caps were derived at (`CapDerivationGrowth`). The exception is tier 2's stride: no rule in the derivation gives
  1.5, because it encodes a target (the plateau at about x6) and not a continuity rule. So the
  derivation pins it (`OwnerPinnedStrideTier`, `OwnerPinnedStride`, dated 2026-10-19), prints the
  continuity value beside it, derives everything else around it, and a check asserts the pin is the
  only departure. `ascensionCopies` is priced off the power of a tier's first Level, not off the
  stride, so it did not move.
  `CheckFusionRules` holds `GameSession` to each rule, to save version 16 and to version 18's
  level clamp.
- **The fusion screen is built.** `FusionScreen` is two tabs on one page, because fusion's output
  is ascension's input: the ascend tab is the player's five-stars beside a card carrying the copy
  cost and two before-and-after rows, the convert tab is the six aspects, the whole roll pool
  face by face, a two-line recipe and a grid of bodies against a points meter. `GameSession` owns
  every number and every refusal on it - `PreviewAscension`, `ConversionPool`,
  `ConversionFodderRoster`, `AscensionBlocker`, `ConversionBlocker` - and the page draws what they
  return. `AscensionLanding` is extracted from `Ascend` rather than copied, so the landing the
  preview promises is the landing the ascension makes. Two doors reach it: `HeroDetailScreen`'s
  action bar and a pressable locked talent card on `TalentsScreen`.
- **The fusion screen counts in tier levels only; the absolute Level appears nowhere on that
  screen.** The ascend card has two rows, TIER LEVEL and TIER CAP, bound to `TierLevelBefore/After`
  and `TierCapBefore/After` on `AscensionPreview`, and the hero list beside it prints the same pair
  by name from `GameSession.TierLevelOf` and `StatMath.HeroLevelCap`. A tier-0 five-star at the top
  of its tier reads `TIER LEVEL 9` and `TIER CAP 9` in the list and `9 -> 1` over a cap of
  `9 -> 18` on the card. `AscensionPreview` still carries `LevelBefore/After`, Levels on the one
  stat ladder, for callers that are not this screen, and `FusionScreen` leaves them unread: printed
  beside the cap, Level `9 -> 10` read as "10 of 18". The preview does not carry the absolute Level
  cap (`StatMath.CapLevel`) at all.

**The chapter 1 ladder is shorter than chapter 2's, deliberately.** It was 12 waves everywhere,
which is 144 battles, which at a 40-battle sitting costs four sittings before difficulty has been
consulted at all: chapter 1 took about 26 days of a twice-a-day player's time and none of it was
the game being hard. `ExpectedWaveCount` in the harness is where the authored shape lives, and the
checks that had 12 and 144 written into them read it instead. **Chapter 1 falls from 52 sittings to
20.** A save written under the old shape does not strand: `ResumePointFor` already clamped, and
`CheckCampaignShortenedLadderResume` proves it does for every shortened stage, through the real
serializer.

### The economy curve

Two levers were retuned, and the reasoning is that both were hand-authored numbers nobody had
chosen.

- **`idleGoldPerHour` is derived from the stage's own purse** rather than hand-authored, which
  restores `DESIGN-IDLE-PROGRESSION`'s own rule. The hand-authored table had let the implied
  multiple slide from 1.63 at `1-1` to 0.51 at `2-12`, so the cache paid relatively less the deeper
  a player got. **The derivation is now one multiple, 0.80, at every stage in the game**: the
  chapter 2 exponent that pass added on top of the purse rule had let the multiple slide the other
  way, to 4.35 at `2-12`, and the 49 stages after it were flat. See the reward curve below.
- **The hero level cost loses its quadratic term and gains a constant**, from `55+26L+3.2L^2` to
  `600+470L`, because power is linear in level and a quadratic cost makes the gold needed to hold a
  ratio grow as the cube of it.
- **`duplicateGold` drops to 150**, because a duplicate's refund was an eighth of all gold and the
  only large channel whose size nobody chose.

`ReportPowerSources` splits the simulated player's power into roster, levels and armor at `2-1` and
at `2-12` and counts every channel a coin arrives through, because "where does the power come from"
had only ever been answered by reading the content files, and reading them got it wrong. At `2-12`
idle income is 86% of all gold earned and 56.3% of all power.

**The reward curve is one law across all 72 stages** (`Tools/_author_campaign_stages.py`,
`rewards_at`). Fit each of the seven reward channels over the authored 24 to
`v(i) = v(0) * (1 + i/k)^p` in the GLOBAL stage index and six come back as clean power laws, 3 to 9%
rms: the shipped economy was already one curve and it stopped at stage 24. The seventh,
`idleGoldPerHour`, is the one no power law fits, which is the chapter 2 exponent read from the other
side. The law that replaced both it and the plateau is `gold = 120 * (1 + i/13.2)^3.20`, with shards
at `gold/20`, idle gold at `0.80 * gold`, idle shards on their own much flatter
`9 * (1 + i/8.4)^1.60` (a level's price climbs with the level; a summon's never does), `accountXp` at
exponent 2.60, and the first-clear bonus a decaying multiple of the same stage's purse. `(k, p)` is
near-degenerate over 24 stages, so the exponent is set by the campaign's TOTAL instead: 3.20 holds
the whole campaign's gold where the plateau already put it. Every channel rises at all 71 steps and
the growth rate only ever falls, +25.0% at `1-2` to +3.9% at `6-12` - **no dead stretch and no spike,
asserted by the generator rather than eyeballed**.

**The one bump is the finale's FIRST CLEAR, x1.50** (`FINALE_FIRST_CLEAR_BUMP`), and only because a
first clear is paid once. No channel a player can earn twice is bumped: a x1.25 finale on the purse
has to be given back at the next chapter's opener, which is the non-monotonic step both shapes
`CAMPAIGN-SPEC.md` 10.3 rejected died on, and a first clear has no step to give back. It exists
because the curve marked the finale - the hardest stage in its chapter, derived at a 65% CEIL rather
than 85% - with nothing: `1-12`'s whole first payout was 835 + 1,440 = 2,275 against `2-1`'s
950 + 1,630 = 2,580, so the hardest stage in the chapter paid LESS than the ordinary one after it.
x1.22 is the smallest bump that clears that; x1.50 leads the next opener by 16% at `1-12` rising to
25% by `5-12`, and puts the finale's first clear at 2.4 to 2.6 of its own purse against an ordinary
stage's 1.62. It is an event, not an economy change: the whole bump adds 81,250 gold and 12,145
shards over 72 stages, a quarter of one percent of the campaign's gold. The generator asserts all
three halves of the claim - the first-clear channels still rise at every step across the ordinary
stages, each finale leads both its neighbours, and each finale's whole first payout leads the next
chapter's opener.

`ReportPaceTargets` grades the shape the campaign is meant to have against the one it has, and is
reported rather than failing. `ReportFusionWalk` shows every line change the walk made and the line
at each stage's first attempt, and `ReportPaceAcrossSeeds` reads both targets over eight walks with
eight first seeds, because a conversion's roll makes one walk's roster one draw. See open
decision 4.

**The economy responds to shards now, not gold.** `CheckEconomyRespondsToIncome` scales gold and
shards together from `IncomeProbeFirstStage` on (a third of the way into the campaign, `3-1`, since
R16; it was the back half, from `4-1`); the late-campaign ratio responds 0.6. Gold alone, still
printed, responds 0.22, because gold stops at each hero's cap.
**Since the economy merge the check reads the early window.** `CheckEconomyRespondsToIncome` walks
8 matched players at 1x and 2.5x income and guards the mean entry ratio over the first six scaled
stages, before level caps bind, at a bar of 0.50. Before the merge's content that window read +0.94
(at `4-1` to `4-6`), after it +1.00. The late reading is printed only: it read +0.31 before and -0.11
after, with a per-player spread of about 0.5, which is draw noise for players at their caps.
**R16 moved the window from `4-1` to `3-1`, and the reason is the fusion plateau (owner decision 1,
tier 2 stride x1.5).** Under the lower top tier a fusing player stops depending on income from about
`3-12` on: its roster and levels read the same at 1x and 2.5x, so the response at `4-1` fell to 0.12
(spread -0.37 to 0.50), which is the plateau working and not the link between income and power being
severed. The window now sits before the plateau. The three readings: 0.12 with the window at `4-1`,
0.70 at `3-1` (spread 0.57 to 1.17, kept) and 0.32 at `2-1` (fails, because the early ratios are
small). The bar is unchanged.

**The R4 economy proposal is applied** (`Docs/ECONOMY-PROPOSAL-R4.md`, candidate F; owner sign-off
2026-10-17). `1-1` to `1-6`, purses, shards, account XP, the per-wave drip and fusion pacing are
untouched. First-clear gold is pulled down from `1-7` to `2-12` (half from `1-12` to `2-3`) and is
back on the reward law at `3-1`. Idle gold rises from 0.80 purses an hour to 1.00 by `3-1`.
`recommendedPower` and the enemy levels are re-solved from `1-7` on. The numbers come from
`Tools/economy_r4_rewards.py` and `economy_r4_levers.py` (with `economy_r4_lib.py`), fed by
`Tools/economy-r4-candidate-F.txt`, the converged `recommendedPower` of all 72 stages. The ratchet
that converged it, `Tools/economy_r4_ratchet.py`, has not been re-run since it moved into the repo.
Measured over 8 walks of the reference player: `1-7` to `1-12` read x1.150, 1.110, 1.079, 1.029,
1.000 and x0.936 (before: x1.270 to x1.184, and x0.999); chapters 2 to 6 read x1.018 to x1.214, and
21 of the 48 stages in chapters 3 to 6 are above x1.1. A wall's median cost stays 0 or 1 extra
sittings, but the tail is longer (worst over the 20 walks of `CheckFusionNeverGates`: `2-12` 28,
`2-6` and `3-6` 12). The fusing player pays no extra sittings from `2-6` on.

### The campaign power curve, and the walls

`CheckCampaignPowerCurve` reads the power a simulated player brings to their **first attempt** of
each stage - not the exit reading the balance table shows, which by construction can never show a
wall - and fails when a stage leaves its band.

- The **first four stages** have no band.
- An **ordinary** stage is x1.10 to x1.30.
- A **wall** is x1.00 to x1.05 in chapter 1 and x0.95 to x1.02 from chapter 2 on, and the per-chapter
  split was measured off how many extra sittings a wall at each ratio actually costs.
- A wall is a chapter finale or a chapter's miniboss stage, stage 6 of every chapter, read from
  `StageDef.IsMiniboss` (`MidChapterWallStageIdsOf`). Until Pinoy's campaign landed the two
  mid-chapter walls were a hard-coded `1-9` and `2-6`.

**Those were the band rules until the economy merge.** The power band is now read per stage
(`PowerBandFor`): `1-1` to `1-6` [x1.00, x2.00], with stage 6 no wall in chapter 1; `1-7` [x1.05,
x1.25] falling to `1-11` [x0.93, x1.08]; `1-12` [x0.88, x0.99]; and [x0.97, x1.25] for every stage
from chapter 2 on, walls included, because a level-capped player's ratio cannot tell a wall from
its neighbour. The CEIL band is read by kind (`IsCeilWall`, `IsCeilBreezy`, `CeilKindOf`): breezy
`1-1` to `1-6` [70%, 100%], walls (`1-12`, and stages 6 and 12 from chapter 2) [55%, 72%], the rest
[78%, 92%]. Both checks print every stage's verdict. They were rewritten with the content, so read
the result against the owner's target table in the proposal, not against itself.
The solver aims at `SolverTargetRatios`, the per-stage entry ratio of the content it was taken on (a
snapshot of the content before R16: on a changed engine or content its single-seed walk moves
stages, so the table is refreshed with the content, not trusted across it). The `2-1` armor 0 was
the entry reading taken before the sitting that wears armor; `ArrivalTeamPower` reads `2-1`, the stage
right after armor opens, as the player stands once they wear what they hold. Run on the shipped content the solver moves 0 of 72 `recommendedPower` and 2
rosters by one unit (`Docs/BALANCE-REPORT-R16.md`, Solver).

**The simulated player here is the wall rule's reference player, not the one who fuses.**
FUSION ACCELERATES A WALL, IT NEVER GATES ONE: every wall must be passable with levels at the cap and
no conversion and no ascension at all. So the band is read on that player's walk,
`recommendedPower` is derived from it, and `CheckFusionNeverGates` walks sixty of them (`WalkSeed` 0
to 59, owner decision 2 of R16; it read twenty, and all twenty were green while 5 of 60 stuck) and
fails if any one sticks anywhere. The player who fuses meets every stage above its band, and that is the rule
working rather than a failure of it.

**A walk that stalls takes the stage's swaps back once before it is called STUCK.** The per-wave
drip (see **Campaign and progression**) pays first-clear gold sooner, so one non-fusing walk bought
a level a sitting early at `1-7`; its fights ended sooner, the walk's clock moved, and
`RollArmorDrop` seeds from that clock, so the drops, the battles and the summon stream all landed
differently. That walk pulled Zella at `3-4` instead of `4-1`, and the line policy
(`EconomyDeployLine`, which ranks by `CeilingPower` and never reads a fight) swapped her in for
Isolde. With the roster the walk held, the Isolde line wins 98% of `3-4`'s 144 battles and 97% of
battle 11.3; the Zella line wins 0% of 11.3, and the walk was called STUCK there after fourteen
sittings while holding over a million gold it could not spend. So `SimulateEconomy` now calls
`UndoStageSwaps`: the line returns to the one the stage was first attempted with, and the heroes
that came in since are benched for the rest of the walk (`EconomyRun.Benched`). A bench that ended
at the next stage only let the ceiling rule field Zella again at `3-5`. Only the branch that used
to end a walk changed, so every walk that already finished walks as before: 39 of 40 readings at
0 and 40 percent drip are identical, and the one that stuck now finishes and pays for the swap
where the table can see it (15 extra sittings at `3-4`, 5 at `3-6`). `CheckFusionNeverGates` is
green with the drip at 40.

**Everything from here to Gear describes the shipped 24, which Pinoy's 72 replaced; the 72 carry
those numbers by position as unsolved seeds** (`CAMPAIGN-SPEC.md` 10.3 and 10.8).
`recommendedPower` was re-derived for all 24 stages from the reference player's entry power against
the middle of each stage's own band, with `floorLevel`, `ceilingLevel` and the authored enemy
levels following so the CEIL band still reads in range at the new number. A stage any of the twenty
walks sticks on is aimed 5% higher per stick (`SolverStuckStep`), because that player fielded its
five highest-ceiling heroes whatever their roles and did not fight like the instrument line at one
power. It now fields a role-sane line (`WantedLine`: a front hero and a Mender first, then
ceiling), which sticks nowhere at the carried seeds (`Docs/SOLVE-PREREQS.md` item 2). **Chapter 2 is nearly flat under the rule**: a line of 5-stars at the tier-0 cap is worth
about 12,000, so chapter 2 runs 8,681 to 10,322 and `2-12` asks 9,984 where it asked 27,601. `2-6`
and `2-12` settled at x1.197 of their `recommendedPower`, over their wall bands, because at the band
some of the twenty walks stuck there; they are the band guard's two reds, and a reading rather
than a regression. `2-12`'s roster was set by hand (see `campaign.json`) because moving it as one
piece clamped it at level 1 reading 0.8% at its ceiling.

A player's power never goes down, so once an earlier stage's band has demanded a power, no later
stage can be asked for less than it. `recommendedPower` is not monotonic on its own, so the high end
of a band is raised to the running maximum of every earlier low, marked with a star in the table,
and each relaxation prints the `recommendedPower` that would retire it. At the derivation nothing
needed relaxing.

**R16 loosened the curve from chapter 2 on, and the reference player no longer sticks anywhere.** The
pre-R16 engine on the pre-R16 curve left 5 of 60 reference walks stuck (seeds 23, 46, 48, 51 and 59,
at 4-2, 2-9, 5-3, 2-6 and 2-4); the counterattack rule re-drew the walks and left 8 on the same curve.
`recommendedPower` from `2-1` to `6-12` is now 8 percent lower, capped so that a stage the step moves is
not taken past about x1.20 on the median of the 60 walks (the band tops out at x1.25; the cap is not a
ceiling on every median, and `3-4`, which it holds, reads x1.215): 56 of the 60 chapter 2 to 6 stages move,
`ch3_4`, `ch5_11`, `ch6_7` and `ch6_10` are held by the cap, and chapter 1 is untouched, so walls 1-6
to 1-12 are as designed. The enemy levels of every moved stage are re-solved with
`SolveStageLevelsAt` to the CEIL rate the stage read before, and `SolverTargetRatios` is re-read on
the loosened content. Result over 60 walks: 0 stuck, no stage with a tail over 12 extra sittings (the
largest is 8, at `2-10`; walls 2-6, 2-12 and 4-12 fall from 30, 28 and 10 to 1, 0 and 0), walk 0
inside the power band at every stage (highest x1.237, `3-4`), CEIL 70 of 70 graded stages in band, and
`CheckFusionNeverGates` green on 60 walks. The walls bind in power and CEIL, less in sittings. The 56
moved stages' opening rosters move, so their stage fingerprints and `ContentVersion` moved, and
`stage-fingerprint-baseline.txt` was regenerated for them with a stated reason. **The lever is minus 8
percent with a x1.20 median cap, not one literal shared percentage: 20 of the 56 moved stages get
less, and six variants were tried against the prompt's three steps.** After the re-solve 10 moved
stages read x1.202 to x1.210, inside the x1.25 band. **The walls still bind by CEIL (58 to 68
percent, inside 55 to 72) but no longer cost the reference player sittings** (the median and the 90th
percentile are 0 at every wall from chapter 2 on, the largest tail is 4, at `4-6`), so pace target B (2 to 4 extra sittings at the finales, reported and
not failing) reads further off than before. Numbers and tables:
`Docs/BALANCE-REPORT-R16.md`, Curve. The package was measured whole: R13 proved spot fixes interact.

**Two costs were measured rather than argued, and both are written into `campaign.json` so the next
pass does not rediscover them.** They are both open decisions now; see below.

### Gear

Armor rolls through `ArmorRoller` with primaries, subs, tempers and attunements, shown on
`InventoryScreen` and `ForgeScreen`. Relics are a two-tier model: a Legendary changes a rule in the
effect pipeline, a Mythic changes a rule and carries a stat block on top of it. Relic progression
ships on top of that, with `RelicShopScreen` as its storefront, now priced as endgame content and
upgradeable to tier 3 (**Sealed, the Quartermaster, and relics as endgame**, below).

### Sealed, the Quartermaster, and relics as endgame

Three R17 pieces of the Gold, Dust and purchase rules, each with a decision record in `Docs/`.

**Sealed** (`Docs/DESIGN-SEALED.md`). Anything a future purchase grants is marked Sealed in the save, and a
summon, a conversion or a forge gamble refuses it: `PlayerProfile.Sealed` (`SealedState`) is the Sealed
part of the Gold, Shards and Slag purses, `HeroInstance.SealedCopies` a count inside `Copies`, and
`ArmorPiece.Sealed` a flag. A random mechanic spends only `PlayerProfile.FreeGold`, `FreeShards` and
`FreeSlag`: `GameSession.Summon` and `CanSummon` read free Shards, `GameSession.ConversionSpare`
(the conversion screen and `ConversionBlocker` both read it) never counts a Sealed copy,
`RerollArmor`, `CombineArmor` and `PlayerProfile.ArmorCombineCandidates` refuse a Sealed piece and Sealed Slag, and the
Quartermaster refuses Sealed Gold. A deterministic sink takes it: `SpendGoldDeterministic` (a level-up)
and `Ascend` take the Sealed part first, equip and salvage accept a Sealed piece, and salvaging one pays
Sealed Slag so a purchase cannot be washed into Slag a reroll accepts. **A purchase path exists since R19,
behind `StoreSwitch` (off).** `SealedGrants` is called by `PurchaseGrants` alone, the one place a purchase
becomes goods (`CheckStoreNoRandomPath` proves it, and that no file of the purchase path names a random
mechanic). The Sealed rule itself is a rule and has no flag. `CheckSealed` is one check per refusal, each
proving the save is byte for byte unchanged after it.

**The Quartermaster** (`Docs/DESIGN-QUARTERMASTER.md`, the build of `DESIGN-SHOP.md`). A daily shelf of six
rolled armor pieces, one per slot, sold for Gold, restocked at the reward day. `Quartermaster.Open` rolls a
new day's stock from the day, `View` shows it and `Buy` is the only door that sells. A price is hours of
the player's own idle Gold at their frontier (`Quartermaster.PriceOf`, Rough 30 hours, Honed 90, rounded
up), worked out when asked and never stored; the stock is stored as resolved pieces
(`QuartermasterState`). **It opens when 3-12 is cleared (from 4-1)**, in its own unlock row
(`Feature.Quartermaster` in `onboarding.json`), reached from a door on `ForgeScreen`. The sizing was
measured first from the reference walk (`QuartermasterSizing`): Gold is level fuel until chapter 3 and a
pile from chapter 4 (35.9 million held at 6-12), a chapter's surplus buys about 0.6, 1.4 and 1.4 days of
stock in chapters 4, 5 and 6 and under 0.04 in chapters 1 to 3, so the shop is a chapter 4 and later sink
by construction. `quartermaster.json` is not in `ContentVersion`. `QuartermasterSwitch` is on by default
and `EMBERFALL_QUARTERMASTER_OFF` rolls it back to the build before it. `CheckQuartermaster` proves it.

**Relics as endgame** (`Docs/DESIGN-RELIC-ENDGAME.md`). The relic list is data: `RelicShop.View` lists
`relics.json` and a relic added there is on the shelf at its grade's price, can be bought and fights with its own
numbers, with no code (`CheckRelicShopIsDataDriven` adds a test relic in memory). Prices moved to
`relic-shop.json` (not hashed): Legendary 5,000 and Mythic 20,000 Dust, measured against the Dust a committed
player earns (1,548 a day at 3-1, 3,204 at 4-12, 4,932 at 6-12), so buying all ten takes 51.7, 25 and 16 days
from those frontiers where the old prices took under four. `relicMaxTier` is 3 with the endgame on:
`GameSession.UpgradeRelic` raises a tier for the relic's price times 2 then 4 and scales its stat block by
1.25 then 1.5 in a fight, a preview and the power score through one resolver,
`PlayerProfile.EquippedRelicOf`. A Mythic's hook never scales, and a tier 1 relic resolves to the content's
own object, so a fight that uses no upgrade is the fight it always was. `balance.json` still carries 80, 2400
and 1 for the rollback: `RelicEndgameSwitch` is on by default and `EMBERFALL_RELIC_ENDGAME_OFF` restores them.
`CheckRelicEndgame` proves it; `CheckRelicInventoryModel` runs with the switch off on purpose, pinning the
pre-endgame rules.

### Summoning, idle income, save

Summoning with a pity rule that guarantees distinct unowned heroes. Idle income accrues against a
real clock and is capped. `SaveSerializer` writes the whole document, at
`SaveSerializer.CurrentVersion` 24, and a save from a newer build is refused rather than read.
Version 15 keeps records this build cannot resolve; version 16 adds each hero's ascension tier;
version 17 adds the Guild Path's copy of the server's answer and the formation's saved lines, and
later the guild tech tree's copy of it - deliberately without a further bump, because the server
holds every part of that and a version 17 build dropping the cache loses nothing it is the only
copy of (`SaveSerializer.CurrentVersion` says so at length). Version 18 clamps every hero's level
into its tier on load, to its cap and, for an ascended hero, up to its tier's first Level. Saves
from before the star-gated caps carried heroes at Level 60 against a cap of 9, and the fusion
screen's ascension card printed it. The clamp runs on every load, not only on version 17 saves, so
the next cap change is migrated without anyone having to remember. `CurrentVersion` records why a
cap change is a migration even when no key moves. `CheckLevelClampOnLoad` proves it.
Version 19 records an event milestone claim by the milestone's authored id, not its threshold.
A claim keyed by threshold reopened when the threshold was retuned, and the milestone paid again.
A claim saved by threshold is migrated through a frozen table of the eight rungs that existed
before ids (`SaveSerializer.PreIdMilestones`), never through the live `events.json`. A claim whose
milestone cannot be resolved is kept, so it stays claimed rather than reopening.
`CheckEventMilestoneRetuneDoesNotRepay` retunes `events.json` under a claimed save and proves it.
Version 20 records cleared stages by id (`clearedStages`) and makes `stagesCleared` a derived
count. It is the first migration step conditioned on the version: a pre-20 position becomes the
first N ids of the frozen `SaveSerializer.PreIdStageOrder`, never of the live `campaign.json`, and
an id the loaded content does not name is kept and not counted. The Echo and Brand banks are
clamped to their caps on every load. `CheckClearedStagesById` and `CheckRegenClampOnLoad` prove
it. **The server must accept it before a cloud player ships**: 0013 inserts its `save_limits` row
at `max_save_version` 19, and `push_save` refuses a version 20 save until that row is raised to 20.
Version 21 records the first session: `tutorialStep` (the onboarding steps completed) and
`seenTips` (the free-play tips shown and the features opened since they unlocked). A pre-21 save
with any campaign progress loads tutorial-complete with every tip seen; one with none starts at
the wake. `CheckOnboarding` proves it. **The owner must raise the server's `save_limits` row to 21
before a cloud build is played**: `update save_limits set max_save_version = 21;`, because
`push_save` refuses a version 21 save while the row says 19 or 20. No saved version 21 exists outside
development machines.
Version 22 records the daily loop: a `dailyLoop` block, written only when `DailyLoopState` holds
something. A version 21 save loads with an empty loop. `CheckDailyLoopSave` proves it. **The owner must
raise the server's `save_limits` row to 22 before a cloud build is played**:
`update save_limits set max_save_version = 22;`.
Version 23 records the sinks: a `sinks` block (`SinksState`, the Armory and Archive cap counters),
written only when it holds something. `Serialize` stamps 23 only when the block is written and 22
otherwise (`SaveSerializer.VersionOf`), so a save with no purchase is the version 22 document and the
cloud push sends the document's own version. `CheckSinksSave` and `CheckSinksOff` prove it. **The owner
must raise the server's `save_limits` row to 23 before a cloud player with the flag on buys anything**:
`update public.save_limits set max_save_version = 23, updated_at = now();`.
Version 24 is R17's one bump and carries four new things, each written only once it holds something:
the Sealed marks (`sealed` on the profile, `sealedCopies` on a hero, `sealed` on an armor piece), the
Quartermaster's shelf (`quartermaster`), Wayfarer's Road's season (`wayfarersRoad`) and an upgraded relic
(an `ownedRelics` tier above 1). `SaveSerializer.HoldsVersion24Data` is the one place that decides a document
is a version 24 document; a document with none of them is stamped 22 (or 23 with a sinks block), so a save
that never touched R17 is byte for byte the document the build before wrote and the cloud push claims the
document's own version. Absence of every key is the empty state, so there is no migration step.
`CheckSealed`, `CheckQuartermaster`, `CheckRelicEndgame` and `CheckWayfarersRoad` each prove their
round trip, the version and a rollback. **The owner must raise the server's `save_limits` row to 24 before a
cloud player holds any of it**: `update public.save_limits set max_save_version = 24, updated_at = now();`
(`save_limits_24.sql` in the R17 handoff, not applied).
Version 25 is R19's one bump (the Store): the `store` block (`StoreState`: one counter record per offer and every
purchase receipt) and, inside `wayfarersRoad`, `paid` and `paidClaimed` (a bought paid track).
`SaveSerializer.HoldsVersion25Data` is the one place that decides a document is a version 25 document, exactly as
`HoldsVersion24Data` is for 24: a document holding only R17 data still says 24, and one holding none says 22 or
23. `CheckStoreSave` proves the three cases, the round trip, damaged blocks and the receipt cap. **The owner must
raise the server's `save_limits` row to 25 before a cloud player holds any of it**:
`update public.save_limits set max_save_version = 25, updated_at = now();` (`save_limits_25.sql` in the R19
handoff, never in the tree; `Server/supabase/migrations/0025_save_limits_26.sql` now raises the row to 26, which covers 25).
Every action that changes
something a player owns ends in a `GameApp.SaveNow`, which is why hooking the save is how the
published team line stays current without any screen having to remember to publish.

### Accounts, cloud save, friends, duels

`Assets/Scripts/Core/Cloud` decides, `Assets/Scripts/Game/Cloud` talks. Supabase over plain REST, no
vendored SDK. Three migrations under `Server/supabase/migrations` create `profiles`, `saves`,
`team_snapshots` and `friendships`, all protected twice: Row Level Security for which rows, grants
for which verbs and columns.

Accounts are anonymous first, so no player ever meets a login wall; an email attaches later and
keeps the same id. Concurrency is compare-and-swap inside `push_save`, with the new revision
computed server-side so a client can never name one. **Nothing is ever silently overwritten**: every
case where both sides hold work the other does not ends at `ConflictChooser` with both saves
described side by side, and a save from a newer build is refused rather than read. A build with no
`Assets/Resources/Config/supabase.json` has no cloud object at all, so offline is the absence of the
online code rather than a path through it.

A friendly duel costs nothing and pays nothing: no Brand, no Valor, no standing, no attempt, no
write to the save.

**A display name is asked for on demand, never at first launch.** `NamePrompt` opens once, the
first time a player without a chosen name (`CloudLocalState.ChosenNameFor`) opens Friends or
founds or joins a guild; skipping changes nothing on Friends and drops the guild action.
`DisplayNameRules.Validate` checks the name with the guild name's constants and helpers - trimmed,
3 to 20 characters counted as `char_length` counts them (`GuildRules.CharacterCount`), no control
character and no Unicode line or paragraph separator - which is stricter than the column's 1 to 24
and 0013's text rule, so the client never sends a name the server refuses. `AccountService` sends
the cleaned name through the existing profile update, and a refused name no longer reads as a
failed sync. `FriendsScreen` opens with the player's own friend code and a copy button. With no
cloud config neither the prompt nor the code card exists. `CheckDisplayNameRules` proves the rule;
`NameOnDemandShots` captures it on a scratch identity with no network.

**Leaderboards: written, not applied to a project, not called (`Docs/DESIGN-LEADERBOARDS.md`).**
`Server/supabase/migrations/0023_leaderboards.sql` holds three boards (the Deep Recital's highest floor,
with a weekly board that turns on Monday at 04:00 UTC and ranks the highest floor this week, the Proving's
highest standing and campaign progress; both rankings are the owner's decisions of 2026-10-23, and the
floors-climbed board written beside the weekly one is gone) as five tables with RLS on, no policy and no
grant, and two callable functions.
`leaderboard_submit()` takes no argument: it reads the caller's own cloud save, checks each value against
the same save, against ceilings and against server time since the server last raised it, stamps
everything with `now()` and logs every refusal. `leaderboard_top()` returns rank, display name, value and
`is_me`, never a user id. `Tools/supabase-leaderboard-rules-test.py` proves the rules once 0023 is applied;
it has never been run against a project. `Tools/supabase-local-check.sh` applies it locally and checks the
boards, the refusals and the closed doors. The client side is built (R28, **The R28 screens**): `LeaderboardsScreen` shows the four boards, reached from
the Profile, the Recital and the Proving, and says so honestly while 0023 is not applied ("The leaderboards are not open yet.").

**The launch server pieces: written, not applied to a project (`Server/README.md`, `Docs/LAUNCH-CHECKLIST.md`).**
`0016_account_deletion.sql` (`delete_my_account`, `admin_delete_account`), `0017_chat_safety.sql` (block,
mute, report), `0018_server_time.sql` (`server_time`, `clock_check`) and `0019_purchase_receipts.sql` (the
product list and the receipt ledger), with the two Edge Functions under `Server/supabase/functions`:
`verify-google-purchase` counts each call against the account's limit, asks Google, records the receipt and
**acknowledges every verified purchase itself, consumables included** (security review L1, the High
finding), and `google-play-rtdn` revokes a refunded receipt. The security review's fixes are
`0020_security_review.sql` and `0021_security_review_launch.sql`; in 0021 **a block also ends an accepted
friendship** (owner, 2026-10-23). **Refunds claw back unspent goods, server side** (owner, 2026-10-23):
`0024_purchase_clawback.sql` takes the refunded purchase's unspent Sealed Gold and Slag, its Dust and one
Sealed hero copy out of the cloud save and out of every later save of that account, reading what the
purchase gave from its receipt in the save (`PurchaseReceipt` records `Gold`, `Slag`, `Dust`, `HeroId` and
keeps the server's `ClawedBack` mark; `StoreChecks` proves both). The order to apply is 0016 to 0024 after
whatever of 0012, 0014 and 0015 a project lacks. Every file 0001 to 0024 was applied to a local PostgreSQL
with stand-in Supabase roles on 2026-10-23, each from 0004 twice, and `Tools/supabase-local-check.sh` passes
71 checks; none has been applied to the live project. Design records: `Docs/SECURITY-REVIEW-SUPABASE.md`,
`Docs/DESIGN-RECEIPT-VALIDATION.md`.

### Guilds, phase one

Three more migrations covering guilds, members, requests and guild team snapshots, with RLS on all
four tables and `anon` revoked on all four. A player can found a guild, hand out its code, let
people in or leave it open, promote, demote, hand it over, remove somebody, leave, disband, talk to
everybody in it, and fight any guildmate's line. `GuildScreen`, `GuildChatScreen` and
`GuildRequestsScreen` are the client; `GuildService` is the transport.

Three rules are constraints rather than code: one guild per player is a primary key, one leader per
guild is a partial unique index, and the code alphabet is a CHECK. Two are locks: the 30-member cap
and the one-message-every-two-seconds chat limit. No table carries an INSERT grant, so every
membership change goes through a function. Guilds are deliberately not readable by every signed-in
user; another guild is found through an exact code lookup, one at a time.

**Phase one excludes** a guild shop, currency, raids, rewards, guild levels and leaderboards, and
chat has no edit and no delete for anybody, including a leader.

**The guild dungeon core is built underneath it.** `GuildDungeonGenerator` builds a seeded five-floor
fight per guild per cycle, each floor a shape of enemy behaviour drawn only from templates some
campaign stage fields, and `GuildDungeon` scores an attempt three ways and stores it as a replayable
record. `GuildDungeonChecks.Run` is wired into the harness. Its screens are being built separately.

**Floor conditions (DESIGN-GUILD-FLOOR-CONDITIONS).** Every floor carries one condition, drawn by
`GuildDungeonGenerator.ConditionsFor` from the same (guild, cycle) seed as the floors.

- **The fight half** is `BattleRules` on `BattleRequest.Rules`. It is null everywhere else, and no pinned
  fingerprint moved.
- **The line half** is `LineRules`, checked by `GuildDungeon.LineLegal` before a line can be attempted.
- **The conditions file is hashed into `ContentVersion`**, so a replay under another condition set is refused.
- **C7 is decided: a board a condition grants every legal line is suspended on that floor**
  (`GuildDungeon.Grants`).
- **On screen:** the condition shows on the dungeon row, across the top of the floor view, and next to the
  six boards with their current leads, all before a line is committed.
- **Measured:** `DevHarness.exe --guild-conditions` is the study the pool was read from.
  `GuildDungeonChecks.CheckConditionShape` re-asserts on every build that each pooled condition reorders
  the rosters by at least x1.5 what a matched difficulty knob or a reseed does, with the three
  guardrails held, on every theme it may draw.
- **Cut by that test:** every enemy-opening and tempo candidate. The design record lists all 30
  candidates and their readings.
- **Must be re-run when the new campaign lands:** the floors are built from whatever templates the
  campaign fields.

**The Guild Path (phase two, system 2).** The guild dungeon pays now, in Waymarks, and the Guild Path
spends them. `DESIGN-GUILDS-PHASE-TWO.md` in the `claude` folder ("The Guild Path, as built") holds the rules and
the full working; this is what the files show.

- **Pay.** 40 on a member's first attempt of a cycle, won or lost; 40 on their first clear of any
  floor; up to 15 for each of floor 1's six boards once the cycle closes, at (n - rank) / (n - 1)
  of the board beaten. Nothing grows with floor depth, because depth is opened by the guild. All
  three numbers are derived by `DevHarness.exe --guild-path` from 3,264 real floor-1 fights over
  guilds of 1 to 30 and rosters built by `GameSession.Summon`, and `GuildPathChecks` re-derives
  them on every build-check. Guilds of 2 to 30 expect 96 to 102 Waymarks a cycle against the
  100-Waymark unit; a guild of one expects 80.
- **The four nodes.** Second, Third and Fourth Line (saved formation lines,
  `GameSession.SwitchLine`, tabs on `FormationScreen`) and Lines Keep Talents. They were sold for
  100, 200, 300 and 200 Waymarks under 0011 and are **granted by guild level now** - see the tech
  tree below. An account that bought one keeps it; `GameSession.OwnsGuildPathNode` reads the two
  sources as an OR, so nothing was migrated and nothing taken away.
- **Server.** `Server/supabase/migrations/0011_guild_path.sql` - **applied.** It adds the ledger,
  the unlocks and the node table, `guild_dungeon_attempt` (submit, board bits, base pay),
  `guild_path_state` (settles closed cycles' board bonus, answers) and `guild_path_unlock`. Keyed
  by account, not guild.
- **Content.** `guild-path.json` is a ninth content file and one of the two that are **not** hashed
  into `ContentVersion`, because nothing in it is read by a fight.
- **Not done:** the live guild rules test does not cover 0011; `guild_dungeon_submit` is still
  callable.

**The guild tech tree (phase two, system 3; it replaced the Path's storefront).**
`DESIGN-GUILD-TECH.md` in the `claude` folder is the design and holds rules G1 to G9; `DevHarness.exe --guild-tech`
prints every number's derivation and reads every guild talent at parity. Two rules the Path was
built under are **retired** by it and their checks are deleted rather than softened: that owning
the whole Path changes no fight, and that a member of a guild of 2 earns within 10% of a member of
a guild of 30.

- **The shape.** Four family tracks - Line (Bulwark, Vanguard; 28 heroes), Blade (Striker, Breaker,
  Stalker; 20), Grace (Mender, Binder, Warden; 21), Reach (Marksman, Tinkerer; 25). A hero belongs
  by **role and nothing else** (G1). Each track carries three stat rungs the member buys and one
  guild talent slot the guild fills from three. Names are placeholders until Pinoy names them.
- **Derived, not authored.** Three inputs are chosen and named as chosen: a whole track is worth one
  hero level, a guild of 10 opens the tree in 52 cycles, and G7's square root. Everything else is
  read - one level at level 40 measures 1.954% of a hero's power over 94 heroes, so a track is
  1.954% and a rung 0.651%; the ceiling is the tree's own shape, 4 x (3 + 1) = 16; the rung unit is
  217 Waymarks and a level costs 1028 XP before the member-count scaling. `GuildTechChecks`
  re-derives all of it on every build-check.
- **One rung is worth the same on every track**, because a rung costs the same everywhere. What
  differs is which stats carry it, split equally by power through `StatMath`'s own weights.
- **The twelve talents** are `PassiveDef`s in the hero talents' own vocabulary and resolve where a
  hero's selected talent does, so there is no guild-talent code path. All twelve clear the parity
  study's 1%-of-Attack material floor, measured by the instrument that grades hero talents, and no
  family has an option worth more than three times its siblings.
- **Guild level** raises ceilings and opens slots and nothing else reads it (G6). XP required per
  level scales with the **square root of member count** (G7): a guild of 30 levels x3.77 as fast
  per person as a guild of 2, which is what the square root predicts (x3.87) rather than a target
  anybody typed, and **every size reaches the same ceiling** - 31 cycles for a guild of 30, 117 for
  a guild of 2, 165 for a guild of one.
- **It accelerates a wall and never gates one** (G5). `CheckGuildTechNeverGates` is
  `CheckFusionNeverGates` read a second time: walks carrying no guild tech all finish, and walks
  carrying the whole tree arrive at a wall with about 3% more power and also finish. The reference
  player `recommendedPower` is derived for has none, and the gate **asserts** that rather than
  arranging it.
- **Server.** `Server/supabase/migrations/0012_guild_tech.sql` - **written, not applied.** Runs
  after 0011 and stays after it. Guild XP is not stored and not minted: it is summed out of
  `guild_path_ledger` joined to the guild each member fought for that cycle, so applying it needs
  no backfill. It replaces `guild_path_balance` so a rung's spend is subtracted. It carries
  `guild_tech_dev_set`, granted to no client role.
- **Content.** `guild-tech.json` is the tenth content file and the second that is not hashed into
  `ContentVersion` - a guild dungeon fight cannot read it, because `GuildDungeon.FightUnder` hands
  the simulation an empty profile, and `CheckGuildDungeonStripsGuildTech` proves that rather than
  asserting it.
- **Not done:** the live guild rules test does not cover 0012, and no screen has been captured with
  `PlayModeScreenshot` since the tree replaced the Path screen.

**The rule gaps (0014).** `Server/supabase/migrations/0014_guild_rule_gaps.sql` - **written, not
applied.** The guild dungeon seed is derived and checked by the server, `guild_touch` writes at most
once per 30 seconds, and `guild_by_code` refuses an account after ten misses in ten minutes, with
`guild_request_send` taking only a found code. `Tools/supabase-guild-rules-test.py` asserts all
three. Operator steps are in `Docs/CLOUD-GAPS.md`, **Fixed, pending apply: 0014**.

**Guild dungeon size and attempts (phase two, system 1's "Size" and "Bounded" rules).** System 1
was built with five floors for every guild, whatever its size. This makes the ladder's length scale
with the guild and bounds a member's attempts the same at every size. **Unmerged: it is delivered
as a git bundle with a PR description, not pushed.** Measured with a Linux stand-in for
`Tools/build-check.ps1` (Roslyn and .NET 8, because the script needs the Windows .NET Framework
compiler), so it is **unverified by the real build check**.

- **The ladder.** `guild-dungeon-scale.json` maps member count to floors: 5 under ten members, then
  one more per five members, 10 at the 30-member cap. `GuildDungeonScale.LadderFor` reads it. The
  smallest ladder is the base five, so no guild has fewer floors than before. The server snapshots
  the member count when the cycle opens, and the ladder is fixed until the cycle rolls.
- **A floor never depends on the ladder.** `GuildDungeonGenerator` builds floors in blocks of five
  (`BlockFloors`: four shuffled themes, then a Champion), and a floor is a function of (seed,
  floor) alone, so `ThemesFor(n, seed)` agrees with `BuildFloor` for every ladder length the
  screens pass. **Floors 1 to 5 are unchanged, enemy for enemy**: `CheckBaseBlockUnmoved` pins
  them over 200 cycles against a value taken from the generator before the change. No hashed file
  moved, so `ContentVersion` did not move and no recorded replay retired.
- **Deeper floors are more floors, not harder ones.** Each block's power climb starts again from
  floor 1's target (`TargetPowerForFloor` reads `PositionInBlock`). Measured first: letting the
  climb run on past floor 5 took the 3-star-or-fewer rosters under the not-a-hero-check guardrail
  from floor 6, and to 0% clears on several floors from 7. That is a star check.
- **Attempts.** Three a floor, as before, and a cycle cap of 15 per account
  (`GuildDungeonScale.CycleAttemptCap`, derived as `guildDungeonAttemptsPerFloor` x
  `guildDungeonFloors`). A member of a 10-floor guild has more floors to spend 15 attempts on,
  never more than 15. `CanAttempt` and `AttemptsLeft` give the client the server's order of
  refusals and its sentences. The server's count wins over the rows, because a member who changed
  guild cannot read their old guild's rows.
- **Rewards: a placeholder, clearly marked.** `GuildDungeonRewards.ForFirstClear` returns
  `Placeholder` with Amount 0 for every floor, and the server mints nothing for a deep floor. The
  Waymark pay is untouched and stays blind to depth (only floor 1's boards pay). Whatever is
  decided must stay a pure function with no seed and no member count, and nothing bought may name
  the dungeon; `CheckRewardPlaceholder` holds all three now.
- **Generator fix.** `NeverOneAspect` redraws the last slot of a line that came out single-Aspect.
  The ten-floor ladder found one such floor in 10,000. It draws only when the line is
  single-Aspect, so every mixed floor is unchanged (the pin covers it).
- **Server.** `Server/supabase/migrations/0022_guild_dungeon_size.sql` - **written, not applied to a
  project.** Applied on 2026-10-23 to a local PostgreSQL with stand-in Supabase roles, twice in a row,
  and its ladder and cycle cap checked there (`Tools/supabase-local-check.sh`). It needs 0014 applied
  first and refuses to apply without it.
  It adds `guild_dungeon_cycles` (the snapshot: RLS on, read by members of the guild, no client
  write), `guild_dungeon_floor_max`, `guild_dungeon_floors_for`, `guild_dungeon_cycle_attempt_cap`,
  an internal `guild_dungeon_cycle_floors`, a `cycle_attempt` column with a CHECK and a unique key
  (so a sixteenth attempt cannot exist), a widened floor constraint, `guild_dungeon_status` with
  three new columns, and `guild_dungeon_submit` as 0014's body plus the ladder and the cycle cap.
  It was parsed with the Postgres parser (pglast), statements and PL/pgSQL bodies; nothing
  resolved names against a schema. The existing live test's assertions still hold for a guild
  under ten members ("floor 6 does not exist", five floors).
- **Rules test.** `Tools/supabase-guild-dungeon-size-rules-test.py`, **written, not run.** Reads
  credentials from `SUPABASE_URL` and `SUPABASE_ANON_KEY` only, never from `supabase.json`, and
  creates about fourteen tagged throwaway accounts.
- **Harness.** `GuildDungeonChecks` gains `CheckLadderTable`, `CheckBaseBlockUnmoved`,
  `CheckFloorIsLadderBlind`, `CheckDeepFloorsMatchBase`, `CheckAttemptBounds`, `CheckSizeReach`,
  `CheckRewardPlaceholder` and `CheckServerAgreesOnSize`. `CheckFloors`' composition rules and
  `CheckNotAHeroCheck` now run over all ten floors. `CheckSizeReach` fights guilds of 1 to 30, each
  member holding three lines from the not-a-hero-check spread, through the real caps: every size
  finished its own ladder in 7 or 8 of 8 guilds, and no member passed a cap.
- **FINDING, pre-existing, for the owner.** The base floors themselves break the not-a-hero-check
  group guardrail (25% of 3-star-or-fewer rosters clear) on about 1.7% of floors, almost all
  Champion floors: 155 of 9,000 over three seed families of 600 dungeons. `CheckNotAHeroCheck`'s
  six fixed dungeons never happen to draw one, which is why it has always passed. The deep floors
  read 181 of 9,000, the same band. Deep floors are therefore judged against the base at the same
  block position (mean clear share within 10 points), and the per-floor guardrail stays asserted
  on the base floors exactly as before. Closing the margin means moving `guildDungeonFloorOnePower`
  or `guildDungeonChampionLevelBonus` in `balance.json`, which retires every replay.
- **Client.** No screen is changed. The screens already read the server's `floor_count`, so a
  longer ladder appears once 0022 is applied, but nothing shows the cycle cap, the ladder's
  origin or the placeholder. `Docs/ui-specs/GUILD-DUNGEON-SIZE.md` is the spec for that run.
- **Owner decisions:** open decision 5.

### The Deep Recital, The Proving, events, shop

`RecitalGenerator` builds an endless floor ladder with a Guardian every tenth floor and an unbroken
Aspect cycle. **It never draws the prologue's scripted loss**: `RecitalGenerator.ScriptedLossEnemies`
reads the prologue's Boss step and skips whatever it fields that no campaign stage does, which is The
Unmade. Before that every Umbra floor fielded it, twice on floors 5 and 11, and
`CheckRecitalDrawsNoScriptedLoss` now climbs 120 floors to say none does. It draws the templates of the
floor's Aspect **that some campaign stage fields** (`GuildDungeonGenerator.TemplatePool`, the guild's
rule), bosses included, so the retired shipped 12 are out of it; `Docs/CAMPAIGN-SPEC.md` 9.6 and 10.7
measure what each change did to the floors. Coda is the Recital's own currency and accrues on every clear.
Two shops now spend the two purses, behind `SinksSwitch` (on by default since R19; define `EMBERFALL_SINKS_OFF`
and the flag off is the build before them): the Armory sells for Valor and the Archive for Coda, from
`sinks.json`, which is not in `ContentVersion`. Prices are days of income, `SinkPricing` is the one place
days become currency, and `SinkShop.Buy` is the only door that sells. Nothing in them is random and only
earned currency pays. `Docs/DESIGN-SINKS.md` has the items, caps and the Echo note.
`ProvingGenerator` builds opponent lines, and every Proving entry, REMATCH included, goes through `GameSession.TryBeginProvingFight` (Brand spent, opponent drawn fresh), so a rematch never replays the line just beaten. The events engine is content-driven through
`events.json`, and each milestone carries a permanent `id` that a claim is saved against, so a
threshold can be retuned freely. Shop screens are wired.

### The daily loop

Five daily orders from a fixed pool, a daily chest for claiming them all, a weekly chest at five
days of fully claimed orders in a Monday to Sunday week, and a repeating 7-day login calendar. The
rules are `DailyLoop` in `Assets/Scripts/Core/Progression`, the saved state is `DailyLoopState`, and
`GameSession` reports events through `OrderEvent`. Every reward is hours of idle income at the
frontier, paid by `GameSession.IdleYield` at claim time and not capped by `Balance.IdleCapHours`.
The day rolls on `GameSession.DayStartUnix` and only moves forward, so a wound-back clock reopens
nothing. The pool, targets and hours are in `orders.json`, loaded through `ContentDatabase.LoadOrders`
and not part of `ContentVersion`; `CheckDailyLoopNotHashed` proves it. **It is on by default since the
owner's decision of 2026-10-23**: `DailyLoopSwitch.Enabled` is false only when the scripting define
`EMBERFALL_DAILY_LOOP_OFF` is set, and with it off no order is generated, no key is written and the hub
shows no door. `OrdersScreen` is a placeholder, reached from an Orders door on the `MainMenuScreen` rail.
**The level order became "spend 1,000 Gold"** the same day (`OrderKind.Gold`, counted on hero levels in
`GameSession.LevelUpHero` and on purchases in `Quartermaster.Buy`): 1,000 is at most one level of any hero
(1,070 at the cheapest) and one Quartermaster piece, so a capped roster can always finish it, and
`CheckDailyLoopSpendGold` asserts both bounds. **The hours in `orders.json`
are final since the content pass of 2026-10-22, measured rather than chosen**: `DevHarness.exe
--loop-curve [walks] [scale]` (`LoopCurve.cs`) walks the reference player with the loop on, claiming
everything every sitting, and over 8 walks the shipped hours pay 3.84 hours of frontier idle income a
day (26.8 a week against the idle chest's 70) and leave every banded stage inside its power band, with
or without Wayfarer's Road, with no walk stuck; the tightest are 1-12 at x0.960 (top x0.99) and 2-1 at
x1.232 with the road (top x1.25), and at twice the hours both sit at their edge. Re-run on 2026-10-23 with
the loop on by default and spend Gold in place of the level orders: 3.81 hours a day (3.83 with the road),
still 0 of 72 stages over their band, 1-12 x0.938 and 2-1 x1.208 with the road. What switching the loop
on changed, item by item, is in `Docs/DESIGN-DAILY-LOOP.md`, **Turning it on by default**. The study also
found that the economy walk dated its level-ups and ascensions by the device clock
(`GameSession.LevelUpHero(hero, times)` and `Ascend(hero)`); since 2026-10-23 the walk passes its own clock
to both (`SpendGold`, `EconomyConvertAscendAndDeploy`). The twelve `CheckDailyLoop*` checks are in
`DailyLoopChecks.cs`. Design: `Docs/DESIGN-DAILY-LOOP.md`.

**Wayfarer's Road, the free track (R17, `Docs/DESIGN-WAYFARERS-ROAD.md`).** A 28 day season of 30 tiers
whose only points are this loop's claims: `DailyLoop.ClaimOrder`, `ClaimDailyChest` and `ClaimWeeklyChest`
call `WayfarersRoad.OnClaim` (8, 12 and 40 points), and nothing else does. A tier is 46 points and the track is
sized on a player who also claims the weekly chest: 24 active days (52 points each, plus four weekly chests)
finish it and 23 do not (`CheckPassSizing` plays both through the real loop). Selectors reset every season, so a
full-track player gets a free 4-star and 5-star every 28 days.
Every reward is fixed and shown up front in `wayfarers-road.json` (`WayfarersRoadConfig`, not hashed):
hourglasses, Slag, Dust in sweeps of the frontier stage, a 4-star of the player's choice at tier 15 and a
5-star at tier 30, both plain earned copies and **never Sealed**. A tier can be bought with Valor or Coda
(`WayfarersRoad.BuyTier`, 2 days of that purse's income, 3 a season). A season is a block of reward days from
a Monday epoch, so every player is in the same one; its stamp only moves forward, a wound-back clock reopens
nothing and unclaimed tiers lapse (`CheckPassRollover`). The paid track is in the data and a locked row on
`WayfarersRoadScreen`, drawn only with `PassPaidSwitch` (define `EMBERFALL_PASS_PAID`, off); `ClaimPaid`
refuses every call unless the Store (**The Store (R19)**, below) is on and the player bought this season's premium
through it (`WayfarersRoadState.PaidSeason`); the paid track is then claimable on the Store's PASS tab and pays through
`PurchaseGrants.Apply`, so every paid reward is Sealed, a 5-star copy at tier 30 included (`CheckStorePass`). `WayfarersRoadSwitch` is on
by default (`EMBERFALL_WAYFARERS_ROAD_OFF` rolls it back), and **since 2026-10-23 the free track earns by
default**, because the loop that feeds it is on (owner's decision); its door is a button in the header of
`OrdersScreen`, which exists only with the loop.

**What the loop on by default does, measured (`Docs/DAILY-LOOP-CURVE.md`, re-run 2026-10-23).** The gate's
walks cannot say: they start before the Road's epoch and claim no order, so the default moves nothing they
read. `DevHarness.exe --daily-loop-curve 60 --daily-loop-fuse` (`DailyLoopCurve.cs`, a measurement, inert in
every other run) walks the 60 reference seeds on the season calendar's clock with the loop off, on with the
Road, and on without it, as a player who claims everything twice a day. For the reference player the largest
move is 2-1, x1.194 to x1.239 (band top x1.25), then 1-12, x0.939 to x0.969 (top x0.99); from 2-6 on every
wall is within 0.01 of the off arm, no wall gains a sitting and the campaign ends within half a day. The
loop pays about 13% of all Gold, the Road completes nearly every season (a five-star selector every 28 days
from about day 25), and the spend-Gold order is claimed on 100% of its days, finished off the line with 121
bench levels a walk (0.3% of all Gold) and never a Quartermaster piece. A player who spends only on the line
finishes it on 13.9% of its days (`--daily-loop-no-bench`), the level order's old rate: the order can no
longer trap a capped roster, but it still asks for one action off the line, which the Orders screen has to
teach. The off arm reproduces the first run exactly.

### Local notifications

**The rule is built, the Unity side is not (`Docs/DESIGN-NOTIFICATIONS.md`).** `NotificationPlanner` in
`Assets/Scripts/Core/Progression` plans when each local notification fires, given the save, the clock and
the device's UTC offset: the idle chest at its cap, the next orders turnover (only with the daily loop on),
and an event's end for a player who has taken part (8 hours before a weekly event, 24 before a monthly).
Quiet hours 22:00 to 09:00 local, at most 2 a local day, 4 hours apart, nothing past 48 hours, nothing
before the first stage is cleared, and no notification names anything sold. It writes nothing and holds
no state in the save. `NotificationChecks.cs` proves it, including a sweep of about 1,500 plans. The
scheduler glue, the settings presenter and the REMINDERS card on the Settings page are built (R28, **The R28 screens**);
the package and an Android backend are not, so nothing is scheduled on a device and the card says reminders are not
available on this device yet.

### The Store (R19)

`Docs/DESIGN-STORE.md` is the decision record; `Docs/DESIGN-SEALED.md` is the rule it obeys. No real money moves in
this build and no real product id, store key or receipt is in the tree.

**What a player sees.** A gold STORE button on the Hub rail opens `StoreScreen` with three tabs. PASS shows Wayfarer's
Road, the free and the paid track one above the other, with the premium unlock. PACKS lists the offers, the chosen
one with its full contents, price tier, limit and when it comes back, a hero picker for a pick, the Ember Ledger's
days left and CLAIM TODAY, the Growth Fund's milestones with a claim on each, and RESTORE PURCHASES. SHOPS links
the Proving Armory, the Recital Archive, the Quartermaster and the Relic Shop. A banner says TEST STORE in the editor
and a development build, and a release build says COMING SOON and draws no buy button. The screen decides and grants
nothing; `CheckOfferWiring` proves it names no grant, no record and no random call.

**The flags.** `StoreSwitch` is on by default and `EMBERFALL_STORE_OFF` rolls it back to the build before R19 (no
door, every call refuses `Off`, the save is the document the build before wrote; `CheckStoreOff`, `CheckOfferOff`).
`SinksSwitch` is now on by default too, so the Armory and the Archive exist for the Shops tab, and
`EMBERFALL_SINKS_OFF` rolls it back. `HeroContractSwitch` is ON since R24 (define `EMBERFALL_HERO_CONTRACT_OFF` rolls it
back): two a month per hero, four a month for the whole account, listed from 4-1 on (see Docs/DESIGN-STORE.md).

**The seven offers** are in `store.json` (`StoreConfig`, not in `ContentVersion`; every product id is a placeholder, and
the euro prices are the owner's since 2026-10-23: Starter Pact 2.99, Growth Fund 14.99, Weekly Event Track 2.99, Weekly
Event Pack 4.99, Relic of Choice 19.99, the rest unchanged, `Docs/STORE-PRICE-PROPOSAL.md`): the Starter Pact (once, one of three named 5-stars and 6 hours of idle Gold), the Weekly Pack (resets
weekly), the Monthly Pack (resets monthly), the Ember Ledger (a 30 day card, hours each reward day claimed and 10 percent
more idle Gold), the Growth Fund (once, fixed rewards at 2-12 to 6-12), the Wayfarer's Road premium (per season) and the
Hero Contract (off). Hours are `GameSession.IdleYield` at the buyer's own frontier, Gold only, and Dust is counted in
sweeps of the frontier stage, so nothing needs retuning when the frontier moves. `StoreOffers` holds the limits, resets
and claims; its clock only moves forward, so a wound-back clock reopens nothing (`CheckOfferClockWoundBack`). The value
of each offer in days of frontier income per chapter is in the R19 handoff's `offer-values.md`.

**THE RULE.** Everything a purchase grants is Sealed, and no purchase path reaches a random mechanic.
`PurchaseGrants.Apply` is the one place a purchase becomes goods: a fixed `PurchaseGrant` made of a closed list of
`GrantKind` lines (none random, no Shards, Dust unmarked) is checked first and written in one block, through
`SealedGrants`, which no other game file calls. That covers the purchase, a Ledger day, a Growth Fund milestone, the
Ledger's share of an idle claim and a paid pass tier. `CheckStoreNoRandomPath` reads the purchase-path files for any
random, summon, roll, forge or conversion word, and `CheckStoreRandomMechanicsRefuse` shows a profile holding only
purchased goods is refused by the summon, the Quartermaster, the forge and the conversion while a level-up still takes
the Gold.

**The purchase path.** Core and proven without Unity (`CheckStore`): `PurchaseLedger` (`Begin` writes nothing and the
pick is held in memory; `Complete` grants once per receipt id; `Restore`), `PurchaseFlow` (wires a store to the ledger
and abandons a purchase the store has not answered in 120 seconds, which grants nothing), `SimulatedStore` (the debug
store), `StoreBackend` and `StoreModes.Resolve`. Game: `PurchaseService` builds the flow, and `UnityIapBackend` is the
thin Unity IAP 5.4.4 adapter behind `EMBERFALL_UNITY_IAP` (set by `Assets/csc.rsp`). The editor uses Unity IAP's fake
store (the debug store for scripted captures), a development build uses the debug store, and a release build is
coming soon with no backend built; the live store needs `EMBERFALL_STORE_LIVE`, which nothing sets. Only the Starter
Pact and the Growth Fund restore, once, and a profile that already holds a receipt for the offer owes nothing under any
transaction id. A paid purchase delivered with no pick waits in `PurchaseFlow.PurchaseWaiting` and the Store screen
asks for the hero (`FinishPurchase`, `FinishRestore`).

**The pass depends on the daily loop.** The premium is sold only while the daily orders run, because the pass earns its
points from them; with `DailyLoopSwitch` off the offer says so and cannot be bought, and the paid track stays locked.

**The save is version 25** (`SaveSerializer.HoldsVersion25Data`, described with version 24 above). `save_limits_25.sql`
in the R19 handoff never reached the tree: **the owner applies `0025_save_limits_26.sql`, which raises the server's
`save_limits` row to 26, before a cloud player holds a receipt or a bought paid track.**

**The curve.** The spender's reference walk and fusing walk against the non-spender, per chapter, are measured by
`--store-curve` (`StoreCurve.cs`); the table is in `Docs/DESIGN-STORE.md`, and its control columns were reproduced to the
third decimal on 2026-10-22. `--store-curve N --store-ads lean|light|full|heavy` walks a rewarded-ad watcher on the same
clock instead of the spender (a measurement, inert in every other run); the result and the proposed caps are in
`Docs/DESIGN-REWARDED-ADS.md`. No ad exists in the game and no ads package is in the project. The euro prices proposed
in `Docs/STORE-PRICE-PROPOSAL.md` were taken by the owner on 2026-10-23 and are in `store.json`.

### Late-game spending (R24)

R24 raised what a committed late-game player can buy, inside the Belgian rule: everything bought is
Sealed, goes through `PurchaseGrants.Apply`, shows fixed contents with its price, and never enters a
random mechanic. Every part sits behind its own switch, on by default, with a harness check that the
switch off equals the build before. Docs/DESIGN-STORE.md has a section for each.

- **The Hero Contract is on, capped.** Two a month per hero and four a month for the account
  (`StoreOfferDef.AccountLimit`, `StoreRefusal.AccountCap`, derived from the per-hero counters), listed
  from 4-1 on because it alone gave a whale +77 percent at 1-12. `EMBERFALL_HERO_CONTRACT_OFF` rolls back.
- **Relic packs** (`RelicPacks`, `EMBERFALL_RELIC_PACKS_OFF`): the Relic Dust Satchel (5,000 Dust,
  EUR 4.99, one a week), the Relic Dust Chest (25,000, EUR 19.99, two a month) and the Relic of Choice
  (EUR 19.99 since 2026-10-23, one a month: a named relic the player lacks or one tier on an owned one, at most 20,000
  Dust of shop value). They open with the Relic Shop.
- **Live events** (`LiveEvents`, `live-events.json`, NOT hashed, `EMBERFALL_LIVE_EVENTS_OFF`): a
  weekly and a monthly event side by side, each with quests counted at the real call sites, points, a
  free track (which may name a skin, with a fixed fallback) and a paid Event Track (EUR 2.99 weekly,
  EUR 9.99 monthly), plus one pack per event (EUR 4.99 weekly, EUR 9.99 monthly; the weekly prices are
  the owner's of 2026-10-23). The paid offers open at 3-1 (the event Gold gave a whale +48 percent
  at 2-6); free events are open from the start. A payment in flight across a turnover is still
  delivered in full and never twice; sales close an hour before an event ends.
- **Hero skins** (`HeroSkins`, `skins.json`, NOT hashed, `EMBERFALL_SKINS_OFF`): one per hero, default
  +2 percent Attack and +3 percent HP, never above 5 percent in any stat, applied in
  `CombatSimulation.BuildUnits` and shown on the Hero screen by the same call. No recorded fight reads
  a skin (the guild dungeon fights scratch heroes; the Proving and duels skip skins), so
  `ContentVersion` did not move. Two hue-shifted test skins prove the art path and are hidden outside
  the editor and development builds; real skins wait on art.
- **Save version 26**, written only when a save holds live event state or skins
  (`SaveSerializer.HoldsVersion26Data`). Because quests count from the first stage win, nearly every
  active player's save becomes 26 (from the first stage clear while an event runs):
  `Server/supabase/migrations/0025_save_limits_26.sql` must be applied before this build reaches a cloud player.
  `CheckServerAcceptsWhatThisBuildWrites` reads the migrations and fails while they leave the row below
  `SaveSerializer.CurrentVersion`.
- **The whale curve** (`R24WhaleCurve.cs`, opt-in, 60 walks): from 4-12 on a whale who buys everything
  leads a non-spender by under 6 percent at every wall and every wall still binds. The study reads every
  offer's price from the content's `store.json` (`WhalePrice`, `WhaleSkinPrice`), so a scratch content folder
  re-prices it. **Re-run on 2026-10-23, all six columns at 60 walks, at the owner's prices and with the walk on
  its own clock:** the reference whale leads by +5.6 percent at 4-12, +4.4 at 5-12 and +4.3 at 6-12 (the fusing
  whale +4.2, +3.3, +3.1), the spender by at most +0.2 from 2-12 on; no walk stuck, every wall from 2-6 on costs
  its 4 sittings for every buyer. The reference whale spends EUR 235.6 per 30 days over chapters 2 to 6 and EUR
  311.7 over chapters 4 to 6 (the Hero Contract EUR 129.6 of it), the spender EUR 32.9 recurring and EUR 18.0
  one-time. The power table is identical to the price proposal's, and the spend is its proposed column: a price
  changes what a buyer pays, not what they get, and the clock fix changes nothing here because the study models
  events as fixed offers and reads no order or event counter. The handoff folder holds whale-curve.md and
  offer-values.md from R24.

### Test store and player size (R26)

- **Test store.** `EMBERFALL_TESTSTORE` (`TestStoreSwitch` in `StoreSwitch.cs`) makes a release build the test store: purchases are free, the
  goods are the offer's fixed contents through `PurchaseGrants.Apply` (Sealed, limits honoured), and the Store and Events screens draw a
  TEST STORE chip (`TestStoreBanner`). Without the define a release build is COMING SOON as before; with it together with
  `EMBERFALL_STORE_LIVE` the build resolves to COMING SOON, never to free goods. `CheckR26Store` and the `release-plain` and
  `release-teststore` compiles in `Tools/build-check.ps1` prove it. See `Docs/DESIGN-STORE.md`, "The test store define".
- **Player size.** `Tools/texture-import-policy.py` has an Emberforge sprites group (BC7 on Windows, ASTC 4x4 on Android; the 31 files that
  were uncompressed RGBA32, 1.27 MB raw to 0.32 MB packed; `forge_rule.png` at 32x6 stays as it is). The player is 97 percent the Units sheets
  (3018 MB packed, BC7), and `Tools/sheet-trim-report.py` (read only) measures that 81 percent of their 4x4 blocks are fully transparent. The
  only big lever is re-cutting the sheets, which is an art and content change left as an owner decision; the numbers are in
  `handoff/R26-POLISH/texture-size.md`.

### The R28 screens (presenters, transport, rewarded ads)

**Four screens built below the uGUI, and the uGUI of two of them (`Docs/ui-specs/R28-SCREENS.md`).** Each screen has a presenter in
`Assets/Scripts/Core/Screens` that exposes exactly what it draws (phase, rows, buttons with enabled and reason, every
sentence and countdown) and a controller in `Assets/Scripts/Game` that makes the calls. `R28ScreenChecks.cs` drives
every presenter through every state, reads the codecs against the shapes of migrations 0022 and 0023, and scans every
string for an em or en dash and for selling words. **Built in uGUI:** the Leaderboards screen and the REMINDERS card on
Settings (below), checked by `R28UiChecks.cs` (`CheckR28Ui`). **Not built in uGUI:** the guild dungeon ladder's floor list
and the rewarded ad blocks, which have presenters, controllers and checks and no screen yet.

- **Leaderboards.** `LeaderboardCodec` (Core) and `LeaderboardService.cs` (transport, in-memory override, controller)
  for `leaderboard_submit` and `leaderboard_top`; `LeaderboardsPresenter`. Submits at most once a minute, never sends
  a score, words a refusal neutrally, and runs the weekly countdown on the server's clock once known. The THIS
  WEEK tab is the highest-floor board (owner decision of 2026-10-23: the floors-climbed board is gone from the server).
  **The screen** is `LeaderboardsScreen` (`ScreenId.Leaderboards`, built by `ScreenRouter.Build`, named open in
  `FeatureEntryPoints.OpenScreens`): four tabs, the caller's own row highlighted and pinned under the list when it ranks
  below the top, every state of the spec's table, no medals, rows that are not buttons. Its doors are a LEADERBOARDS button
  on `ProfileScreen` and a BOARDS button on `RecitalScreen` and `ProvingScreen` (`LeaderboardsScreen.OpenBoard`: `SelectTab`
  before `Open`). It is drawn only from `LeaderboardsView` and taps call only `Open`, `Refresh` and `SelectTab`. Where its
  bands sit is `LeaderboardsLayout` in `R28ScreenLayout.cs`, engine-free so the harness runs it.
- **Guild dungeon ladder.** `GuildDungeonSizeService` for the size migration's three functions and its snapshot row;
  `GuildDungeonLadderPresenter` reads the ladder, the cycle cap and every refusal sentence from `GuildDungeonScale`
  and the reward from `GuildDungeonRewards` (still the placeholder).
- **Notification settings.** `NotificationSettingsPresenter` over `NotificationPlanner`; the switches in `PlayerPrefs`;
  `NotificationScheduler` wired into `GameApp`'s pause and quit hooks, inert with the only backend there is
  (`UnsupportedNotificationBackend`). **The card** is `RemindersCard` on `SettingsScreen`, directly under the audio card:
  the permission line and button, Reminders with its three topics indented under it, COMING UP, the rule lines. It is drawn
  only from `NotificationSettingsView` and taps call only `Toggle` and `PermissionButton`. With no notification package
  installed (the real state today) it reads "Reminders are not available on this device yet." with every switch disabled
  and no list. `RemindersCardLayout` in `R28ScreenLayout.cs` holds its band heights.
- **Captures owed.** Both screens changed layout and have no capture yet. Every state is a target under
  `Emberfall/Capture/R28 screens/` in `PlayModeScreenshot` (`R28aCaptures`, offline through `LeaderboardBackends.Override`
  and `NotificationBackends.Override`), listed at both shapes in `Tools/capture-r28a-targets.txt`.
- **Rewarded ads, behind `AdsSwitch`, OFF.** `ads.json` (the lean variant: A1 idle Gold capped at 2 hours, A2 one
  Brand, A3 one more full-pay repeat clear, each once a reward day), `AdRewards` (read-only `View`, `Grant` once per
  impression through `PurchaseGrants.Apply`, so the Gold is Sealed), `AdsState` in the save from **version 27**
  (`SaveSerializer.CurrentVersion` is 27; a save is stamped 27 only once an ad was granted, so with the switch off
  every save is byte for byte what version 26 wrote), `RewardedAdPresenter`, and `Game/Ads` with a simulated backend
  for the editor and development builds. **Before the switch goes on the owner applies
  `Server/supabase/migrations/0026_save_limits_27.sql`.** No ads package is in the project.

### The friends build (R27)

- **Defines and scripts.** A friends build is the Windows zip and the Android APK with `EMBERFALL_TESTSTORE` and `EMBERFALL_PLAYLOG`,
  passed per build through `EmberfallBuild.RequestedDefines` (`-emberfallDefines`, `BuildDefines.Parse`) into
  `extraScriptingDefines`, never into `ProjectSettings.asset`. `Tools/build-windows.ps1 -Defines`, the new `Tools/build-android.ps1`
  (`EmberfallBuild.BuildAndroidPlaytest`, IL2CPP ARM64) and `Tools/build-friends.ps1` (with `-WhatIf`) build it; see `Docs/FRIENDS-BUILD.md`.
- **Play log and smoke mode.** `PlayLogSwitch` (off by default), `PlayLogSession` and `PlayLogWriter` in `PlayLog.cs` (Core) and
  `PlayLogService` (Game, compiled only with the define) write `emberfall_playlog.txt`, independent of the stats consent, with a
  SHARE PLAY LOG button in `SettingsScreen`. `-emberfallSmoke` checks the cloud sign in and opens the Friends and Guild screens
  on a throw-away identity: `SmokeStorage` redirects the save file, its mirror, the cloud session and the play log.
  `CheckR27PlayLog` and the `release-playlog` compile prove it; a real Unity build is still owed.

### The UI kit (R20)

A generated art kit (panels, buttons, tabs, pills, currency and navigation icons, aspect badges, relic icons, ability
icons, Store card art) that skins the existing screens behind one switch. It is a skin, not a redesign: no layout
moved, and the kit only replaces the sprite, tint or text colour at the call sites listed below.

**The switch.** `UiKitSwitch` (Core, engine-free) is **on by default** (R26). `EMBERFALL_UIKIT_OFF` is the opt-out
that builds the old look (the same shape as `EMBERFALL_STORE_OFF` and `EMBERFALL_SINKS_OFF`); no build script sets a
define, so a normal build ships the kit. `UiKitSwitch.ShippedDefault` says what the compile ships, and
`PlayModeScreenshot` restores to it. The field is writable so a harness check or a capture can force it, and the old
look stays reachable three ways: the define, the field, and the `+plain` capture suffix. `build-check` proves the
defines by running them: the `[define-probe]` step compiles `Tools/DefineProbe` with the Core files with no defines
and with `EMBERFALL_UIKIT_OFF;EMBERFALL_TESTSTORE`, runs both and asserts the printed flags. Off is the old look
exactly: every lookup in `UiKitMap` answers null, every call site keeps the drawing it always
had as its fallback, and no kit sprite is loaded. The switch reads nothing from a save, writes nothing to one, never
reaches a fight, and the kit's art is named in none of the eight hashed content files (`ContentVersion` did not move).

**The map and the loader.** `UiKitMap` (Core) is the one place a slot or an id meets a kit piece, as plain strings:
HudSkin slots, EmberforgeArt slots, purses, Store offer ids, relic ids, ability ids, aspects, card frames. `UiKit`
(UI) turns a key into a sprite with `Resources.Load`, caches hits and misses, and warns once per missing key. It is
asked first by `EmberforgeSkin.Get` and `HudSkin.Authored`, which is how every screen built from the Emberforge
components takes the kit without a code change. Ability icons are one texture: `ability_atlas.png` holds the 109
distinct icons that cover all 254 abilities (basic, active and passive1 of every hero), and `UiKit` cuts a sprite
from it with `Sprite.Create` using the cells in `ability_atlas_map.json`. A hero's flat-stats passive (an id ending
`_stats`) wears its passive1 icon.

**What each screen gets with the kit on.**
- **Hub:** navigation icons (the Bag uses the relic pendant, More uses the gear), the Store door, the idle chest, the
  close button, currency pills with the gold, shards and dust icons, the currency strip icons, panels, popups,
  buttons, tabs and round discs. A fixed-size panel under 170 units on its short side takes the kit's small panel.
- **Battle HUD:** the generic pill (callout, turn order) is the kit's dark plate, and the callout shows the
  ability or passive icon off its left end, outside the box `BattleHudLayout` places.
- **Ability card** (`AbilityCardSheet`, the battle unit card and the Hero page): the icon at the end of each header; a
  one-ability card gains a header only when there is an icon.
- **Hero screen:** a medallion on the corner of each basic, active and passive1 card, and the aspect badges on the
  hero's aspect and the six-aspect row.
- **Store:** the Wayfarer's Road header as the Pass tab's summary backdrop, each offer's card on its row and large in
  the detail pane, purse icons on the Pass tiles and the Shop doors.
- **Relics:** one icon per relic id in `ItemCard.ForRelic` (Inventory) and on the Relic Shop card.
- **Victory:** the gold, shards and slag tiles. **Summon:** the shard icon on PULL and the chest on the opening
  guarantee.
- **Buttons:** the kit's plates have no pressed art, so a press darkens the plate; the primary plate is a real warm
  orange (R26, no overlay any more); the selected tab's outline is off so its dark text is legible.
- **Panels (R26):** with the kit on, a panel carries a filigree corner (`corner_ornament`, drawn 48 units, mirrored into
  the four corners by `EmberforgeUI.Ornaments`, skipped on a small fixed panel whose bolts are its own ornament), and
  `Emberforge.OrnamentClearance` grows with it so a control placed by the clearance stays clear. Kit off keeps the
  26-unit diamond and its old clearance.

**The R26 polish.** `Tools/uikit-polish-gen.py` (PIL, code-drawn, deterministic, `--check` proves the committed PNGs are
what a run writes) makes `corner_ornament.png` and re-exports five pieces from the pristine copies in
`Tools/uikit-polish-src` (so it is re-runnable): `btn_primary` in a clear warm orange, `btn_secondary` and `btn_disabled`
lighter with a gold hairline so they separate from a dark panel, and `panel_large` and `panel_small` with a stronger
gilded rim. Sizes and 9-slice borders are unchanged, so no layout moves. A disabled button dims its label
(`Emberforge.TextDisabled`) with the kit on. The reference images under `ui-gen/concepts/picked` were looked at only and
are never imported, copied or traced. `CheckR26Ui` measures the result (orange hue, plate against panel brightness,
label contrast, rim brightness, borders and sizes) rather than trusting it.

**The Marketplace (R26).** The Hub's market stall is labelled MARKETPLACE and opens `MarketplaceScreen`, an overview of
four shops in the order Relic Shop, Quartermaster, Proving Armory, Recital Archive, with an "N of 4 open" count. The
four cards are `ShopDoors` (UI), shared with the Store's SHOPS tab, over `MarketplaceShops` (Core: order, words, door
ids). The stall opens at 2-4 (`Feature.Marketplace`, `marketplace` in `onboarding.json`); each card passes its own door
id (`marketplace.RelicShop`, `.Quartermaster`, `.Armory`, `.Archive`) bound to its shop's feature, so the Relic Shop
card stays locked until 3-1 while the Armory is open at 2-4. The one-time new dot sits on the stall and on each card.

**The Hero screen at its level cap (R26).** `LevelCapText` (Core) words it: the owner's sentence "Level cap reached, raise
your rank." first, in every variant, then the guidance the page already gave (what ascending costs, the highest
ascension, or that a hero below five stars never ascends). The button keeps the label LEVEL UP and Level Up and x10 are
both disabled from the same flag, which reads `HeroLevelCapOf` (stars and ascension, not account rank).

**Import.** `Tools/uikit-import.py` copies the generator's import-ready set into `Assets/Resources/Art/UIKit` and
writes the `.meta` files, so the settings do not depend on what Unity guesses. 68 textures: Sprite (2D and UI), single,
mips off, bilinear, no NPOT scale, full rect, borders from the nine-slice table (a border pair that would meet keeps
six pixels of flat plate so a stretched plate has no hairline), and a pixels-per-unit per piece that draws a panel's
corner block at 34 units. **Explicit BC7 (Windows) and ASTC 4x4 (Android) on every meta**, because
`DefaultTexturePlatform` ships uncompressed: about 9 MB on a device instead of about 37 MB. Everything under Resources
ships whether the switch is on or off. `Tools/texture-import-policy.py` carries three UI kit groups and reports 0 files
to write.

**Capture targets.** Any `Screen:<Name>` target takes `+kit` (switch forced on for the capture only) or `+plain` (forced
off; the before shot and the rollback proof), before any `=param` and `@WxH`, for example
`Screen:MainMenu+kit@2400x1080`. The menu `Emberfall/Capture/R20 kit/` lists Hub, Hub with the Store door, Battle, Battle
unit card, Hero detail, Hero ability card, Store pass tab, Store packs, Victory, Battle result and Summon, each on and
off. The switch is set in edit mode before Play starts and again on the Play domain load, so the screens are built
after it; `PlayModeScreenshot` puts it back when the run ends.

**The check.** `CheckUiKit`: shipped off, off answers null for every lookup, on maps every relic and every ability,
every key has a PNG and a meta that imports as a single sprite with BC7 and ASTC stated, every ability has a cell inside
the atlas, the sliced pieces carry borders, the kit is absent from the hashed files, the wiring, the switch put back. It
reads the metas with line endings normalised, because a fresh checkout lands CRLF.

**Not wired.** Card frames as portrait backing (all 94 heroes ship round framed badges and a portrait-shaped frame does
not fit one without a layout change; the five frames and their open versions are imported and mapped). Kit bars on
unit plates (10 units tall, no room for a sliced trough). The title ribbon (the kit banner needs about 100 units of
height and the ribbon is 56). A Unity sprite atlas asset (the abilities are already one texture, the other pieces are
single sprites). Kit art on screens not named above (Guild, Talents, Fusion, Forge and the rest keep their look
except where they build panels, buttons and tabs through the shared components, which the kit also skins).

### The first session, and the unlock table

`Onboarding` in `Assets/Scripts/Core/Session` decides the first session and `OnboardingCoach` in
`Assets/Scripts/UI` draws it. The flow is one counter, `PlayerProfile.TutorialStep`, holding how many
steps are complete, so quitting mid-tutorial resumes after the last completed step and nothing else
is stored. The steps are the wake, the Cinder's recall (free and scripted, it gives Kael and never
touches the summon pool, pity, rates or `TotalSummons`), the existing prologue battle, then the
guided core loop with a forced arrow and a pip each, then free play. `Onboarding.Skip` ends the flow
and still gives Kael. Free play shows one tip the first time each screen opens, recorded in
`PlayerProfile.SeenTips`. `OnboardingConfig` parses `onboarding.json` for the words and the table.

**The guided order is six steps, one pip each:** team (`Onboarding.StepTeam`), stage 1-1
(`StepCampaign`), the wave result (`StepWaveResult`), stage 1-2 (`StepSecondStage`), the level up
(`StepLevelUp`) and the idle claim (`StepIdle`), then `Complete`. `GuidedSteps` is 6. The old order
asked for a level up straight after 1-1, which a new player could not afford, so the coach detoured
down the campaign (`CanLevelAnyone`). That detour is gone: with the explicit 1-2 step a new player
holds 1,235 gold at the level up against a 1,070 first level, which `CheckOnboarding` prints and
asserts. No content number changed.

**The off switch is `OnboardingSwitch.Enabled`, default true.** Defining `EMBERFALL_ONBOARDING_OFF`
makes it default false. It is read by `Onboarding.IsComplete`, `Onboarding.CreateKeeperProfile`,
`OnboardingCoach.Install` and `FeatureGate.UnlockStage`. With it off a new save is
`GameSession.CreateNewProfile`'s profile marked complete, no coach is installed and every feature is
open. `CheckOnboardingSwitch` in `Tools/DevHarness/OnboardingChecks.cs` proves that profile equal,
byte for byte, to the one `main` made before onboarding existed, excluding only `tutorialStep` and
`seenTips`. A fresh start with the switch off runs `FirstRun.Begin` exactly as it did before
onboarding existed, from the restored `FirstRun.cs`. With it on, `GameApp` installs
`OnboardingCoach` and `FirstRun` is not shown.

**Locked, not hidden.** Every feature is visible from the first minute and `FeatureGate` says only
whether it can be entered. A feature opens the moment its stage is cleared, by stage id, so a
campaign reshape moves an unlock with its stage. A feature with no stage, a stage the content does
not name, or no table at all is open. `FeatureEntryPoints` lists every door (hub buildings, nav
cells, rail buttons, panel tabs, hero page buttons, inventory tabs, and the router's own screens),
the UI asks through `FeatureGates`, and a locked door says "UNLOCKS AT 1-6" and nothing else.
`ScreenRouter.Show` refuses every locked screen, which covers a door no surface draws.
`CheckFeatureUnlockGating` enumerates the entry points and proves each against the table.

The table in `onboarding.json` `unlocks`:

| opens at start | friends | fusion | talents, armor, forge | guilds | proving, marketplace | recital | relics | quartermaster |
|---|---|---|---|---|---|---|---|---|
| campaign, summon, heroes, idle | 1-4 | 1-6 | 1-12 | 2-1 | 2-4 | 2-8 | 3-1 | 3-12 |

`marketplace` (R26) is the Hub's market stall, which opens at 2-4 with the first of the four shops behind it; each
shop card inside keeps its own row (see **The UI kit (R20)**, the Marketplace paragraph).

`onboarding.json` is not in `ContentVersion`, so a moved unlock or a rewritten line retires no
replay. **Its text is final since the content pass of 2026-10-22**: the wake keeps Pinoy's own lore
lines, the six steps and the recall say what the game does, and nine more screens have a first-visit
tip than before, 24 in all (the guild dungeon and tech, the live events, the Armory,
the Archive, the Quartermaster, the Store, Orders and Wayfarer's Road), each checked against what
the screen does: two ranks in the formation, conversion rolling a random five-star of the chosen
aspect, the Forge's salvage, reroll and combine. No numbers and no dashes. The harness walks and
pinned lines read the table through `FeatureGate.IsUnlockedArriving`, which is what moved the two
failing checks under **The instruments**. `PlayModeScreenshot` has six `Screen:Onboard*` targets;
a capture of a feature's own screen uses `FeatureGates.CaptureBypass` (and `CaptureForceLocks` keeps the gate live for
the one Marketplace-with-locks target) and the onboarding targets
clear it.

### Play stats

Opt-in play counts for the playtest, so we can see where players stop. The design is
`Docs/DESIGN-TELEMETRY.md` and the tester-facing text is `Docs/PRIVACY-PLAYTEST.md`.
`TelemetrySwitch.Enabled` is on in code and `EMBERFALL_TELEMETRY_OFF` builds it out. Consent
defaults to No: `TelemetryConsentPrompt` asks once (after Wake, or at the Hub for a save past its first session), and the Settings PLAY STATS card changes it.
`TelemetrySchema` is the only whitelist of what can be sent. Nothing is sent from the editor or
from an offline build, and nothing without a cloud config: `GameApp.CreateAnonymousClient` is null
then. Turning stats off wipes the install id and the unsent queue. Consent and the id live in
`emberfall_telemetry.json` next to the save, so the save version did not move.
`0015_play_events.sql` is the owner's to apply by hand. Until it is applied the server refuses the
inserts and the game stays silent and drops those batches, so counts sent before it is applied
are lost. Apply 0015 before handing out a cloud build. `TelemetryChecks` and
`CloudChecks.CheckMigrationGrants` prove
the schema, the consent rules and the INSERT-only grants.

### Real audio

`Assets/Resources/Content/audio.json` maps 50 cues to 114 of the owner's sound files and 9 music files through
`AudioService` behind the `Sfx` door. Music plays the owner's five tracks (hub, battle_ch1,
battle_ch2, boss, summon) as loops in `Assets/Audio/Resources/music`, four with an intro, at -20
LUFS and cue volume 0.5. Chapter 1 and 2 fights get their own track, walls and finales play boss,
a screen change crossfades over 1.0 s, the same file carries on with no restart, and a skill
moment or a victory ducks the music 6 dB. The rollback is `"music": false` in `audio.json`
(every music cue silent, as before). Both stingers are still empty. The
settings screen has music, effects and interface sliders plus a mute (`AudioPrefs`, `PlayerPrefs`,
60/80/70), and `credits.txt` names the packs. The rollback is `realAudio` false in `audio.json`.
See `Docs/DESIGN-AUDIO.md`.

### UI, and the Hub's four layers

uGUI, built entirely in code, scene deliberately near-empty. `ScreenRouter` moves between the
twenty-one screens under `Assets/Scripts/UI/Screens`. `UITheme` owns the reference resolution and
the palette; `UIBuilder`, `Widgets`, `ScreenChrome` and `CurrencyStrip` are the shared furniture.
Landscape, touch-first, everything hung off `Screen.safeArea`.

**What is on the currency strip is a rule, not a list.** A currency on the strip is one you spend
ANYWHERE; a currency off the strip LIVES WHERE IT IS SPENT. So `CurrencyStrip` carries Gold and
Shards and nothing else, on the four screens that hide the top bar, and `BagPanel` - opened from
the strip, and from anywhere on it - is the one place that shows every purse at once with the
sentence that says where each one goes. This replaced a six-cell strip that was already full: the
seventh currency (Coda) did not fit, and an eighth (Waymarks) arrived while that was being
recorded. Four of the six that came off were already obeying the rule before it was written down -
`ForgeScreen` draws Slag, `ProvingScreen` draws Valor, `GuildPathScreen` draws Waymarks,
`RecitalScreen` draws Coda and Echoes, `EventsScreen` names each event's purse in its own heading.
The Relic Shop was the one screen with nowhere to read its own currency and now has a Dust plate.
**Valor and Coda each name their shop in the bag**: "Spent in the Armory" and "Spent in the
Archive", read from `SinksSwitch` in `BagPanel`, which has been on by default since R19. With
`EMBERFALL_SINKS_OFF` defined they say there is nothing to spend them on yet, because a currency
banked towards nothing should say so where a player goes to look for it.

**The Campaign chip row sizes itself from the chapter count.** `CampaignScreen.RefreshChips` shares
the row's 1780 units out between however many chapters `campaign.json` holds, clamped to [200, 300]
per chip, and boxes each chip's labels at the chip's real width with best-fit rather than at a
literal 280 with Overflow. Eight chapters stand still; past that the row scrolls, on
`UIBuilder.ScrollStrip`. The old row was 1600 with every chip a flat 300, which fits five - and the
failure at six was not a row spilling over its edges but a `HorizontalLayoutGroup` dividing the
width it had, squeezing the plates while their labels stayed 280 wide and printed across the seams.

**The campaign flow has a HOME disc, and back steps one screen.** `ScreenChrome.HomeDisc` and
`ForgedHeader`'s `home` overload draw the disc the back control uses, with HOME in the glyph slot.
`CampaignScreen` takes it in place of its chevron; `WaveResultScreen` and `BattleScreen` draw it in
the corner for a campaign fight or a campaign wave's replay, marked by `GuildBattleRun.Home`.
`ScreenRouter.GoHome` clears the history and shows the main menu, and resolves, pays and saves
nothing. `GameApp.HandleBack` asks `UIScreen.TryStepBack` before the history, so the wave result
steps back to the campaign map and a campaign replay takes its BACK button's step to the wave
result: both are shown without being remembered, so the history cannot name them. Leaving
mid-stage keeps what the save already holds (`CampaignCurrentStageId`, wave and battle); no save
field was added. `PlayModeScreenshot`'s `WaveReplayResult` target photographs the replay's result
panel.

**The campaign result follows a locked design**, `Docs/ui-specs/WAVE-RESULTS.md`, and
`WaveResultScreen` only reads the `WaveResolution` it is handed.

- **Wave cleared** stands the line that fought in idle on the stage's battlefield (`BattleStage`,
  fed the record's `Squad` snapshots, no enemies), with the field camera's viewport stopping at the
  band so no hero stands behind it. The band carries the stage line, the title, a 12-pip track with
  the count, a chip per currency the wave paid, and NEXT WAVE, WATCH and an AUTO checkbox. AUTO
  fires NEXT WAVE after 2 seconds, any tap on the band cancels it for that wave, and it is a
  PlayerPrefs setting, not a save field.
- **Stage cleared** darkens the field under VICTORY, with a FIRST CLEAR tag, one tile per reward
  (gold, Ember Shards, Wayfarer XP, the armor drop through `ItemCard`), the unlock line, CONTINUE
  and WATCH.
- **Wave lost** is the same band in red with the real battle and wave numbers, RETRY, ADJUST LINE
  and WATCH. A loss in wave 1 leaves out the "progress up to wave N is kept" sentence, because no
  wave is kept.
- **The HP chart and HP KEPT are gone**, and `WaveResolution.Margin` with them; the Dev Arena never
  read it.

`PlayModeScreenshot` photographs `WaveResultAuto`, and a stage clear that is a first clear with an
armor drop, found by pinning the clock and the armor count the probe and the photograph share. No
save version moved and no content JSON changed.

**The Hub is four layers, with one rule each.** It had grown by bolting controls onto the map:
three slabs over the top left with the HEROES marker behind them, GUILD twice a thumb apart, DEV
overlapping both the PROVING marker and the button it was stacked under, an idle panel holding a
quarter of the painting to show five lines about a number, and a read-only copy of the squad along
the bottom of a screen that has a formation page for exactly that.

- **0, the map.** Every structure that leads somewhere carries a marker, and nothing above may
  cover a building or its label. The market stall is the MARKETPLACE (R26): it opens the
  overview of the four shops, where it used to open the Relic Shop alone.
- **1, the HUD frame, edges only.** The player plate and the currencies at the top corners, a rail
  of four icon buttons down one edge, five nav cells across the bottom, and the campaign card with
  the idle chest beside it in the bottom right. All of it inside `Screen.safeArea`, every target at
  least 108 logical units, which is 44dp on the narrowest phone this game targets.
- **2, panels.** Sheets over a dimmed map, hosting screens the router already owns. The plate opens
  PROFILE, ACCOUNT, FRIENDS and SETTINGS as tabs; the nav bar opens the guild the same way. No
  screen was rewritten: `UIScreen.Embedded` hides the page header the sheet already draws, and that
  is the whole of it.
- **3, popups.** The idle claim, the rail's overflow menu, the confirm dialog, and the save-conflict
  chooser.

One back stack for all of it, `UiLayerStack`. Escape and the Android back key - legacy Input
reports both as Escape - close the topmost layer and nothing else; on the bare Hub they do nothing.
`ConflictChooser` registers as BLOCKING, so back is swallowed rather than escaping a decision the
sync is waiting on.

Every sprite in the frame resolves through `HudSkin`, so dropping a PNG at
`Resources/Sprites/Hud/<slot>.png` reskins one element with no code change and no recompile. Every
metric comes from `HubStyle`. There is no art and no style literal in a call site.

**The Dev Arena is a row of the rail's overflow menu now, reading DEV ARENA**, and it is compiled
out rather than hidden: the rows that name the arena sit inside `#if UNITY_EDITOR ||
DEVELOPMENT_BUILD`, so a release player has no menu row because it has no method.

Two tooling fixes came with the captures that verified it. `RefreshOnDemand` makes an unfocused
editor compile on request, because Unity's automatic refresh runs on window focus and two checkouts
mean two editors, and `focus-unity.ps1` picks the editor whose window title matches this project
folder rather than whichever Windows listed first.

### Battle presentation and art

`BattleStage` builds the world, `UnitActor` and `UnitRig` build a body, `SpriteAnimator` plays a
per-unit clip set (idle, basic, basic2, skill, hit reaction, death, controlled) by writing the
active frame's UV rect into a billboard quad. Per-clip `impactFraction` syncs the lunge and the
Damage event to the strike; per-clip `footFraction` stops a clip floating or sinking.
`BattleAssets` loads unit sprites and backdrops by id, and logs an error naming the hero and the
path it looked in when a unit declares art it does not have.

**Hit feedback is fed from the event list.** `VfxDirector` (flash, hit stop, knockback, shake, a
particle burst, and `Flipbook`, which plays a sheet on a billboard quad) receives every event from
`BattleScreen.Handle` through `Play`, with `ExactFeed` set when the stage is built, so it sees a
`StatusApplied` that `UnitActor`'s taps never could; nothing uses the taps now. It reads
`vfx.json`, which is not part of `ContentVersion`. Presentation only: nothing in
`CombatSimulation`, `GameSession` or the save reads it. All seven `types` entries now carry a
flipbook `sheet` from `Assets/Resources/Art/Vfx` (six white-on-alpha sheets, `buff` sharing
`heal`'s, credited in that folder's `README.md`), so the procedural numbers beside each sheet
play only where a sheet is missing. The sheets are white so the attacker's aspect accent tints
them; `Tools/texture-import-policy.py` has a group for the folder.

**Hits land harder, behind one flag.** The `juice` block in `vfx.json` (not hashed; `enabled`
false is the rollback, `EMBERFALL_JUICE` overrides it for one editor launch) sets a 0.08 s white
hit flash, hit stop of 0.05 s, 0.09 s on a crit and 0.12 s on a skill impact (the timeline holds
once per action, never `Time.timeScale`), shake only on crits and skill impacts, a 10 to 16 px
knockback, damage numbers that pop from 1.4x and stack upward, additive flipbooks tinted by the
caster's aspect accent with a glow, a skill moment on a cast's first impact (backdrop to 70 percent
for 0.30 s and an aspect ring behind the caster), and a death flash, sink and smooth HP drain.
`JuiceProfile` and `JuiceMath` are engine free, so `JuiceChecks.cs` proves flag off equals the
old battle, the effect cap (40 live quads, 62 in all) and that the simulation baselines are
unchanged. Settings has a SCREEN SHAKE toggle (`ScreenShakePrefs`, device only). The capture
target `Screen:BattleMoment=<stage>.<kind>[.<aspect>][.off][.v1|.v2]` stops on a hit, crit, skill,
heal, buff, debuff, shield or death. See `Docs/DESIGN-JUICE.md`.

**A fight reads clean at 1x, behind one switch.** `"feelPreset"` in `vfx.json` (not hashed) ships
`"v2"`; `"v1"` is the R12 battle exactly, and `EMBERFALL_FEEL` overrides it for one editor launch.
v2 replaces the white-out with a 0.06 s tint, gives each event class (hit, crit, skill, heal, buff,
debuff, shield, death) one look and its own cue with no geometric rings, caps effects at 2 per unit,
draws numbers 1.25 times larger, plays one sound per basic hit with at most 6 effect voices, draws
units 1.2 times larger on a re-solved formation (the R15 slots, still in `BattleFraming` as the rollback to the formation below), shows Drifting Pilgrim and Thornback Boar for the two
chapter 1 placeholders, and fixes facing (six heroes flipped, four with per-clip flips, enemy mannequins
turned to face the heroes). Who a unit looks like (stand-ins, flips, a per-unit scale) lives in the
unhashed `stage-v2.json`; the R15 formation, camera and unit scale are `BattleFraming` constants. `FeelChecks.cs` and
`StageChecks.cs` prove v1 equals R12 and that ids, stats, `ContentVersion` and the fingerprints are
unchanged. `Screen:RosterSheet=<side>[.page][.clip][.v1|.v2]` photographs a whole side for the facing
proof. See the v2 section of `Docs/DESIGN-JUICE.md`.

**WATCH replays the hardest battle of a resolved wave.** `GameSession.ResolveWave` keeps a
`WaveReplay` on the `WaveResolution`, in memory only: the lost battle, else the win that left the
least HP, with its seed from `BattleSeed` and copies of the line that fought it
(`HeroInstance.Copy`, taken before a stage clear pays). `GameSession.ReplayBattle` runs it through
`CombatSimulation.Run` again, so the replay is the resolved fight and not a second roll, and
`BattleScreen` plays it as a `GuildBattleRun` flagged `IsReplay`, which reaches no grant and asks
for no save. `CheckWaveReplay` proves the replay matches the recorded outcome, that the copy
carries every `HeroInstance` field, and that replaying leaves the serialized save byte for byte as
it was. `PlayModeScreenshot`'s `WaveReplay` target presses WATCH and freezes a frame while a hit
effect is alive.

**The battle HUD is presentation only and placed by one engine-free layout.** An opaque top bar
(`BattleHudMetrics.BarHeight`) with the battle camera's viewport stopping under it, so nothing on
the field is drawn beneath it. One plate per unit (`UnitPlate`: HP bar, energy bar, status icons, no name over the head,
`StatusIcons.Compose`), standing on the unit's head in the formation, the ability callout as a pill under the bar, and the turn order as a NEXT
pill at the bottom centre (`TurnOrderPill`). Every position comes from `BattleHudLayout` over
`BattleFraming`, and every size from `BattleHudMetrics`, which has no `UnityEngine` reference so the
harness compiles the same file. `CheckBattleHud` in `BattleHudChecks` builds every plate, both
pills and the bar at every formation slot and three phone aspects, and fails if two of them
overlap. Nothing in it reads or changes a fight.

**The battle stands on an Idle Heroes style formation table.** FormationLayout (engine free, one array,
`Cells`) says where each side's five units stand as fractions of the safe area, measured from its left and
its top: back row B1 0.18,0.64, B2 0.13,0.77, B3 0.09,0.90, front row F1 0.32,0.69, F2 0.29,0.84, the right
team mirrored (x becomes 1 minus x). Game slot 0 is F2, 1 is F1, 2 is B3, 3 is B2, 4 is B1 (the order the
slots always stood in on screen, SlotCell), a side with fewer than five keeps the cells it occupies, and a unit
whose slot is taken goes to the nearest free one (ResolveSlots). BattleStage.FitFormation solves the world
position that puts each unit's feet on its cell for the camera's viewport and the safe area
(FormationLayout.ToWorld, the inverse of BattleFraming.Project) and sets the orthographic size so a unit is
0.20 of the safe height tall; UnitActor.Setup scales every rig so its VISIBLE top (the art's own, from sprite-extents.json, read by SpriteExtents; the body rule where an entry is missing or is not the body, the Carrion Vulture's wings) is that one height (a boss is up to 1.3 times
taller, held under the bars ceiling), so heroes and enemies read the same size. The lower unit is drawn in
front (the unit quads are transparent and sort by distance from the camera, which with every unit one height is feet order; `FormationLayout.DrawOrder` is the proved statement of it). Damage and heal numbers start on the target's own upper body and the buff chip is drawn over the upper body, so neither reads as the neighbour's; the turn dot and the inert status icons are not drawn over a head. The HP and energy bars stand just over the
head (BattleHudLayout.PlateOriginAboveHead), the highest stays below 0.38 of the height from the top, the
centre stays open, and attack steps are FormationLayout.ApproachScale of the old ones. R29FormationChecks.cs
proves all of it at 16:9, 18:9, 20:9 and 4:3 with a notch and a gesture bar. **Rollback:**
FormationLayout.Enabled false returns the stage and the plates to the R15 geometry. Not done, on purpose: a
boss does not move to B2, because campaign.json lists every boss first (slot 0) and the sim's front-target rule
picks the lowest slot, so B2 would draw the front unit behind two others. Captures:
Tools/capture-formation-targets.txt and Tools/capture-formation-moments.txt through capture-batch.ps1,
Screen:Battle=<stage>+five@<W>x<H> (+five, and .five on a BattleMoment, fields a full five hero line on the scratch identity only).

**31 of 94 heroes have drawn battle art**, and they are the thirty 5-stars plus Celen. The other 63
carry `visual.spriteStatus: "pending"` and render as an aspect-tinted placeholder mannequin with
their name under its feet, which makes "not drawn yet" a thing the game can say separately from
"this hero's art is broken". All 94 profile badges are drawn and wired, so the battle sheets are
the only art the roster is still missing, and with every kit authored they are **the last thing
between the roster and being finished**. `Docs/PINOY-LIST.md` section 5 is the list.

**A hero sheet keeps the proportions it was drawn at.** `fit_frame` in `Tools/rewire_sprites.py`,
which `process_hero` runs on every frame, scales a delivered frame uniformly to 396 tall and
centres it in a transparent 336 by 396 cell, fitting by width instead only when that would still
be wider than the cell. It replaced a plain resize to the cell that stretched the five Ember
5-stars (Kael, Kiva, Seraphine, Hale and Marrow), delivered at 720 by 1280, to between 1.3 and
1.65 times their width.

**Every unit sheet is 336 by 396 a frame, imported as BC7 on Windows and ASTC 6x6 on Android.**
All 334 sheets of the 57 drawn units were re-exported from the original deliveries in
`D:/Emberfall/import` by `rewire_sprites.py --reexport`, which writes a delivered file only into
the shipped clip it reproduces pixel for pixel, so no clip mapping was guessed from a file name.
At the old 220 by 260 cell the median hero was drawn at 1.57 screen pixels per texel and the
DXT5 blocks showed; it is 1.03 now. The cell is not the plain 1.5x of 330 by 390 because BC7
needs both sheet dimensions to be multiples of 4 and ASTC 6x6 wants frame edges on 6-pixel block
edges; `DESIGN-ART-DELIVERY.md` section 2 has the arithmetic. Brenna's square sheets are 360 by
360. Only the PNGs and each sidecar's `frameWidth` and `frameHeight` changed; no `.meta` guid
moved, and every fraction in the sidecars and content files holds, re-measured to within 0.003
(feet) and 0.007 (bodies). A 5v5 battle holds 575 MB of unit texture on Windows and 255 MB on
Android, from 247 MB and 158 MB, and the sheet PNGs in the repository went from 756 MB to
1,568 MB.

**Enemies draw from the same sheets, and 26 of Pinoy's 84 have them**: 12 of chapter 1's 14 and all
14 of chapter 2's, in the five clips the battle plays (idle, basic, skill, hit reaction, death) at
336 by 396 a frame, the size every hero sheet is. Each carries the measured `spriteBodyFraction`
and `spriteFootFraction` in its `visual` block, because `UnitRig.BuildSprite` sizes the quad from
the first. **Their `scale` is derived from the art**, not set by kind: `BuildSprite` stretches
every body to its silhouette's height, which throws away how large the figure was drawn, so each
chapter 1 and 2 enemy's scale is the one that draws it at the median hero's screen pixels per
texel, and the four named bosses (the Collector, the Shepherd, the Forsaken, the Waykeeper) take
x1.10 on top. The goblins already matched and are unchanged. The Carrion Vulture's
`spriteBodyFraction` is 0.308, its body from neck ruff to feet; the 0.85 it carried counted the
raised wings. `lost_villager` and `wild_bear` have no sheet, chapter 3 has none, and the Hollowed
Hunter's sheets arrived under the stem `hollowed_archer`. `Docs/PINOY-LIST.md` section 5b has the
discrepancies.

### Dev Arena

`Assets/Dev/DevArena.unity`, with its scripts in `Assets/Scripts/Dev`, opened from the editor's
Emberfall menu and from the Hub's overflow menu in a development player. It presents a live
`BattleSession` through the real battle presentation, hand-driven or watching itself play,
switchable mid-fight. A hand-driven action is a whole turn, spends that unit's natural turn, and
refuses out loud where Stun or Silence would otherwise have silently substituted a basic.

**It cannot reach a release build, and that is checked rather than intended.** Every file under
`Assets/Scripts/Dev` is wrapped in a `UNITY_EDITOR || DEVELOPMENT_BUILD` guard, so a release player
does not contain the classes, and `build-check.ps1` asserts all of it from the files. The scene
reaches a development player because `EmberfallBuild.ScenesFor` names it outright, gated on the
same Development condition, and shared with the Android path so both targets answer it the same way.

### Tooling

`Tools/build-check.ps1` is the gate. The harness also runs four checks other branches wrote and could
not add, in `CheckWhatOtherBranchesWrote`: `SummonTiming`'s invariants for Full and Fast, the talent
screen's audit list against the harness's, every talent override naming a field the engine applies,
and 280 mirror matches so the old `ExpireStatuses` crash cannot return. `DevHarness.exe --check A,B`
runs named checks alone, a development aid the gate never uses. `Tools/doc-lint.ps1` enforces the
citation half of CLAUDE.md
across every tracked markdown file, with its known debt recorded in `Tools/doc-lint-baseline.txt`.
`Tools/DevHarness` holds every engine check, the stage fingerprints, the hero sweep and the talent
sweep. **`CheckEveryAuthoredActiveIsCast` is new**: it walks all 74 authored actives over the
sweep's own arena and seeds, asserts that the set never cast is exactly Roan's - a second hero going
quiet is a failure now - and then proves his in a dark arena that lets him live to round 4, which
the sweep does not. CI runs `build-check.ps1` on every push and pull request, on `ubuntu-latest`
(a Windows job runs by hand only; see CLAUDE.md, "build-check on Linux and macOS").
`bash Tools/supabase-local-check.sh` applies every migration to a throwaway local PostgreSQL with the
stand-in Supabase roles in `Server/supabase/local/supabase-standin.sql`, pastes each file from 0004 twice,
and runs `Server/supabase/local/rules-check.sql`; it takes no project URL and no key.
`DevHarness.exe --loop-curve [walks] [scale]` (`LoopCurve.cs`) is a study, not part of the gate: what
switching the daily loop on does to the reference walk, with and without Wayfarer's Road (**The daily
loop**). `python Tools/text_audit.py` writes `Docs/TEXT-AUDIT.md`, the list of every placeholder string
still in content and code.
`DevHarness.exe --ceil-reconcile [stage [levels]]` is a second development aid: it reads the solver's
and the guard's CEIL side by side against a 4,000-sample reference, and it is how the `1-12`
disagreement under **The instruments** was settled.
`--relevel` re-searches enemy levels to the CEIL targets (ordinary 85%, walls 65%) in a content
copy, and `--wall-modes` picks which players `--wall-study` walks. Neither is part of the gate.
`Tools/solve-recommended-power.ps1` must not be re-run until its bands are retuned to the economy
proposal.

**The nightly run needs a desktop, and its task now says so.** The 03:30 task was
registered with an S4U logon, which has no interactive desktop, so since 2026-10-13 the GUI editor
never wrote its log and every night produced 0 of 43 captures. `Tools/register-nightly.ps1` now
registers an Interactive logon, which runs only while its user is logged on: with nobody logged on
at 03:30 there is no report that night, and `StartWhenAvailable` may fire it at the next logon.
`Tools/nightly.ps1` checks `Test-InteractiveDesktop` and, with no desktop, skips the capture sweep
with a note and launches nothing. If the editor has written no log within `LogStartMinutes` (5) of
launch, it stops that Unity by PID instead of waiting out the ceiling.

**The cause is proven and the fix is in; the scheduled run awaits the re-register.** The nightly's
own capture command line (`ScratchCaptures.Run`, three targets: MainMenu, Campaign and Heroes),
started by hand from a logged-on desktop against the merged tree, wrote its log and all three PNGs
in under two minutes and left the real save unchanged. So captures work whenever an interactive
desktop exists, and the S4U logon was what broke them. The scheduled task itself is proven only
once the owner re-registers it from a logged-on PowerShell and a night runs.

**Captures run in batches, and a stuck play entry recovers by itself.** `Tools/capture-batch.ps1`
takes a list of targets and runs all of them in one editor session through `ScratchCaptures.Run`,
with the real save hashed before and after and one RESULT line per target. A run captures its
targets in batches, never one launch per target; a single launch is only for retrying a target.
`CaptureWatchdog` decides and `PlayModeScreenshot` acts: with no Pump heartbeat 90 seconds after a
play entry it logs the editor state, leaves Play (which is how Unity removes its own scene backup)
and re-enters once; a second miss marks the target FAILED and the batch moves on, inside three
minutes. If the editor stops answering, or quits by itself with targets left, the batch script
kills or waits out only the editor it started, moves the scene backup aside, restores the project
settings files from its own copies if they changed, and relaunches once with the targets left.
`EMBERFALL_CAPTURE_WATCHDOG=0` switches the watchdog off, and `EMBERFALL_CAPTURE_FORCE_MISS` forces
one miss to show the recovery. Measured on one evening: about 90 batch play entries without a
natural hang, a forced miss recovered after 90 seconds and then captured, and about 11 to 17
seconds per target in a batch against about 24 seconds for a single launch. Entering Play without
a domain reload (`EMBERFALL_CAPTURE_SKIP_RELOAD=1`) made entries eleven times faster but broke
captures through destroyed cached textures, so it is marked experimental and not used. The
nightly still runs its own one-session sweep. See `Docs/CAPTURE.md`.

**The play-entry stall was the capture tool's own focus raise (R23).** About half of all capture
launches from R19 to R22 stopped right after `entered play mode`. Native stacks of stalled editors
showed the main thread blocked in a cross-thread `SendMessage` under Unity's window procedure.
`CaptureFocus.Raise` compared only `MainWindowHandle` with the foreground window, and the editor has
a second top-level window (the floating Game view), so it pressed Alt and switched windows every
three seconds while Unity already held the foreground. It now counts any window of the editor's
process as in front and never raises between a play entry request and its first heartbeat
(`NightlyFocus.Raise` the same). Measured: 3 of 5 launches stalled before, 7 of 9 with the AI
Assistant bridge held off, 0 of 20 after the fix with focus on. Capture sessions also hold the
bridge off through `Tools/mcp-bridge-pref.ps1` and restore the owner's setting after; it was not the
cause. See `Docs/CAPTURE.md`.

**The gate runs its walks on threads (R23).** `SimulateEconomies` in `WallResolve.cs` runs the
reference walks on logical cores minus 2 threads and reads the results back in seed order; the
curve samples (`CurveRate`), the ladder calibration cells and the solver's stuck scan do the same,
and the harness runs with server GC. Output is byte-identical to the serial run
(`EMBERFALL_WALK_WORKERS=1`). Compiles are skipped when a SHA256 stamp over every input matches
(`Tools/compile-cache.ps1`). The full check went from 18 min 19 s to about 4 minutes on a 12-core
machine. `build-check.ps1 -Quick` runs every check with 20 reference walks instead of 60, for work in
progress; it says QUICK in its output and in `STATUS.md`, and a merge always uses the full check.

**Editor captures are offline by construction.** The working tree holds a gitignored
`supabase.json`, so a Play session that did nothing about it signed in as the real account; a
capture once came back covered by the save-conflict chooser. `SupabaseConfig.ForcedOffline`, a
`SessionState` switch that belongs to one editor process, makes the loader behave as if the file
were absent, so `GameApp.StartCloud` returns at its `Configured` check before any `SupabaseClient`
is built. Every Play-mode entry under `Assets/Editor` goes through
`EditorCaptureGuard.EnterPlayMode`, which arms it, and the switch is cleared when Play mode ends.
`EditorScratchIdentity` puts `PlayModeScreenshot` on a scratch company name, so a photograph does
not write the real local save either. All of it sits inside `UNITY_EDITOR`. `build-check.ps1`'s
offline-switch stage asserts four things: `Tools/OfflineSwitchCheck` runs the real
`SupabaseConfig`, `GameApp.StartCloud` and `EditorCaptureGuard` against the shim with a stand-in
file and shows the file is never read and no client is built; no Play-mode entry under
`Assets/Editor` bypasses the guard; nothing about the switch exists outside `UNITY_EDITOR` in
`Assets/Scripts`; and `GameApp.StartCloud`, after its `Configured` return, is the only place
shipped code builds a `SupabaseClient`.

**A battle capture can name its stage.** `PlayModeScreenshot`'s Battle target fights
`Session.NextStage`, wherever the scratch profile stands, unless the target names a stage:
`Tools/capture-screen.sh "Battle=ch2_9" out.png` fights `ch2_9` and writes
`screen_Battle=ch2_9.png`, the stage riding in the file name for the same reason the frame size
does.

**The arena matrix** (`Tools/DevHarness/ArenaMatrix.cs`) measures every basic, active, passive
and talent in the roster, 428 rules, by ablation. It fights the same hero in the same room at the
same seed with that one rule taken out, and leaves the rule's own announcement out of the
comparison. A rule is (a) seen changing the sweep arena, (b) seen only in a purpose-built room,
which has to repeat byte for byte, beat its control and move the tally its own sentence names, or
(c) seen nowhere, and then named with what it waits on. `CheckArenaMatrix` and the talent coverage
check both read it. Three opt-in readings sit beside it and are never part of the gate:
`EMBERFALL_MATRIX_SEARCH=1` searches for a room for each dark rule, `EMBERFALL_MATRIX_PROBE` runs
named rooms, and **`EMBERFALL_MATRIX_NORMAL=pinned|balance|both` reads every class (b) rule in a line
a player actually fields**. That is the pinned line or the balance line with the rule's hero in it,
levelled and geared to each shipped stage's recommended power, at 12 battles a stage and two seeds.
A room proves a rule can work; this says whether a playtester will ever see it. Run it with
`--check CheckArenaMatrix`.

### Build targets

A Windows desktop player builds today through `EmberfallBuild.BuildWindows`, Mono rather than
IL2CPP because this editor install has no Windows IL2CPP module. Windowed 1600x900, resizable,
Alt+Enter to full screen, one instance at a time; quitting is the window's own close button, as
there is no quit control in the game. **Android is still the target**; no layout differs between
them.

**The playtest players are `Tools/build-windows.ps1`**, which drives `BuildWindowsPlaytest` in
one of four variants: release or `-Development`, offline or `-Cloud`. The variant is the first line
of output and part of the folder name, `Emberfall-VERSION-HASH-VARIANT-win`, and nothing else
differs between them. **Offline is the default and the script owns the cloud config**:
`supabase.json` is gitignored, so it is moved out of the project for an offline build and put back
afterwards, on failure and on Ctrl+C too, and a run that was killed outright is put right by the
next run before it reads anything. `BuildWindowsPlaytest` refuses a variant that disagrees with the
file on disk. With no config, `GameApp` builds no cloud and every cloud page - Account, Friends,
Guild, guild dungeon, guild chat, guild tech - says LOCAL ONLY with its buttons off. The script
refuses a dirty tree (tracked changes anywhere, untracked files under `Assets`, `Packages` or
`ProjectSettings`, any other gitignored file inside a `Resources` folder), checks the tree is still
clean after Unity exits, and reads the player back: `level1` and the development-only class names
in `Assembly-CSharp.dll` present exactly when the variant is a development one, the commit packed
into `resources.assets`, and the cloud project's host packed in exactly when the variant is a cloud
one. It writes outside the working tree, zips the player with `Docs/PLAYTEST-README.md` inside as
`READ-ME-FIRST.txt`, moves Burst's DoNotShip symbols beside it, and prints the folder size, the zip
size and `EmberfallBuild`'s size report, which reads the build report for what each texture group
packed to against the same pixels as raw RGBA.

**Every player names its build.** `Game.BuildStamp` prints `BUILD <version> (<commit>)` in the Hub's
bottom-left corner and at the foot of Settings, with DEV after it on a development player. The
commit comes from `Resources/BuildStamp.txt`, which `EmberfallBuild` writes for the length of one
`BuildPlayer` call and deletes afterwards, gitignored so writing it cannot dirty the tree. The
editor prints `editor` and a player built through a door that did not stamp it prints `unstamped`.
**`EmberfallBuild.Version` is the one version** - the Windows and Android settings and the folder
names read it, and `ProjectSettings.asset` carries the same `bundleVersion`, which is `0.3.0`.

**A Windows offline playtest package exists.** `Tools/build-windows.ps1` builds a release player
and, with `-Development`, a development player with the Dev Arena. Both are offline by default and
each zip carries `Docs/PLAYTEST-README.md` as `READ-ME-FIRST.txt`. `Docs/PLAYTEST-FEEDBACK.md` is
the form a tester answers after session one and after day two. The package holds the landed state
of the first session and its unlock table, the battle HUD, the battle stats and the 336 by 396
sprites, each described above.

**The heal and buff disc is fixed.** Frame 2 of `Art/Vfx/heal_ring.png` is a solid filled circle,
and the heal and buff effects played the sheet from its first frame, so a hero briefly wore an
opaque green disc over the torso. `VfxEntry` now has a start frame (`start` in `vfx.json`, default
0), and the `heal` and `buff` entries, played by `VfxDirector.Flipbook`, start at the ring frames
of that sheet. The ring is now about a third of a second long.

**Open visual issues:** units with no sheet draw as placeholder figures (63 of 94 heroes, and the
enemies without sheets). **Every chapter has a painted battle background since R21**: chapter 1's
ashen, ember and hollow, and `crossing`, `city`, `old_kingdom`, `divided_realm` and `sanctuary` for
chapters 2 to 6, each with an `<id>_boss` painting that stage 12 of its chapter names in
`campaign.json`'s `environment` field. `BattleAssets` loads them from `Resources/Backgrounds`.

## In progress

A branch is named here when it holds work worth naming, and only while it is unmerged.

**R26 polish, the UI half** (an unmerged branch of run R26, not yet pushed so not named here; the store half is on a sibling branch of the same run). It
holds: the UI kit on by default with the `EMBERFALL_UIKIT_OFF` opt-out and the `[define-probe]` build-check step; the
Hub's market stall as the Marketplace over four shop doors shared with the Store; kit polish (filigree corners, an orange
primary button, a stronger gilded frame, lighter secondary and disabled plates, drawn by `Tools/uikit-polish-gen.py`);
and the Hero screen's level cap sentence. All four are described under **The UI kit (R20)**. Their harness checks are
`CheckR26Ui`. **Owed before it counts:** the screens that changed layout (Hub, Marketplace both ways, Hero at the cap,
Store shops) have no capture yet; the tester takes them from the `Emberfall/Capture/R26 polish/` menu.

Otherwise none. The R4 economy proposal is merged and described under **The economy curve** and **The
campaign power curve, and the walls**.

## Next

In order.

1. **What the fidelity passes left.** Pass 3 closed C13 and every finding pass 2 recorded as left
   for later. What remains is **G1, G2 and G3**, the three proposed primitives not built, which
   would close groups 25, 30 and 23, and one unmeasured suspicion: a counter the per-target budget
   refuses still spends the Resolve its effect carries (Caldus's Defined Duty is the shipped shape).
   The file holds 25 capabilities and 36 clauses. **Groups 20 and 22 stay marked as different in
   kind rather than as debt**, because an information layer and a movement system are games this
   one is not.
2. **Then the UI design system.** The Hub now has one, `HubStyle`, and the other twenty screens do
   not: they read `UITheme` for colour and reference resolution and then write their own metrics at
   the call site. `HubStyle`'s own scope note says it is the Hub's chrome language rather than the
   game's, which is the honest description of a dialect that has not been generalised yet.
3. **The dormancy tripwires that will now stay dormant.** Twelve are still guarded as dormant
   because nothing reaches them: `LinkedUnit`, `AllLinkedUnits`, `OnLinkedUnitTargeted`,
   `linkAmountPower`, `linkFromCaster`, `ScaleByLastMoved`, `ShieldBrokenByCaster`, `EvasionUp`,
   `Interrupt`, `PerTarget`, `LedgerMitigates` and `maxHpCap`. `HpTransfer` and `GuaranteedCrit`
   came off the list during the 4-star pass, lit by Rhys's Crossing Cut and Maera's Watch the
   Water; `AccuracyDown`, `Splash` and `OnDodge` came off during the 3-star pass, lit by Adric,
   Aster and Cal; and the standing `TargetBan` came off with Yara's Second Bearing. **The roster is
   authored, so no future kit will light the remainder** - each is now a feature the engine has and
   the content does not use, which is a decision to make rather than a wait, and
   `ShieldBrokenByCaster` in particular is the near-miss: `Docs/ENGINE-GAPS.md` group 32 is Arno
   asking for exactly it, from a side of the event where the condition cannot be read. A tripwire
   is **flipped, never deleted**, so a lit one asserts the feature IS used and names the content
   using it, and goes red again the day that content stops.

## Blocked

- **Chapters 3 to 6's art**, on Pinoy. The stages exist and field his enemies as pending
  mannequins; no enemy sheet has been delivered for chapters 3 to 6, and chapter 1's
  `lost_villager` and `wild_bear` are still owed. `DESIGN-WORLD`'s
  contested-region proposal for chapter 3 is moot: his chapter 3 is City of Strangers.
- **An APK**, on having an Android device to run it on.
- **iOS**, on a Mac and an Apple developer account. Neither exists.

## Open decisions

Five. None of them is a number to nudge, and neither the economy question nor the power-band one
that used to head this list is still here. The five-star review closed four more - the three
5-star kit questions and the low-tier Energy one - and the Dispel repair closed the Dispel one,
which was open decision 3, all recorded below. The numbers on the three that remain were reassigned
when it closed, so the Hub rail is number 3 now. Number 5, the guild dungeon's size rules, was
added with them.

**The economy decision is resolved, and what replaced it is open decision 4.** The economy and
curve passes, the retune after them and the re-solve against real summon rates put a simulated
player's first attempt at x1.15 to x1.20 of `recommendedPower` on every ordinary stage, x1.02 and
x1.03 on chapter 1's two walls and x0.99 on chapter 2's two, with every banded stage inside its band. The entry ratio is no longer the question. What the player's power is made of
is.

**And so is the power-band one.** It read "the guard is red on six stages, all of them over and all
of them marginal", and the cause was the roster rather than the tuning: the curve had been derived
on a tree carrying five authored 4-stars. `recommendedPower` was re-solved once the eighteen 3-star
kits landed and the guard is green on all 24 stages. The low-star batch could not move it again:
the line the balance pass fields is Kael, Kiva, Isara, Adric and Orelle, none of them below three
stars, and `PlayerProfile.NormaliseTeam` only ever fills an empty slot and never displaces a hero
already deployed, so a newly authored 1-star cannot walk into it. `Tools/solve-recommended-power.ps1`
was therefore not re-run for this batch.

**And so is the low-tier Energy one.** It read "four of the twelve low-star Energy clauses mean
something other than what Pinoy wrote, and the choice is his", the choice being whether to give the
1-star and 2-star tiers an active skill. **The decision is that the star contract does not move and
the redirect stands**: one star is a basic and nothing else, two stars is a basic and one passive,
and the Energy Piri, Wynn and Orenna are written to gain goes to an ally who can spend it, while
Yara's is paid in Attack. The reason is that the alternative was never about four heroes. An active
skill at those tiers changes the contract `validate_heroes.py` enforces for all twenty of them,
moves both tiers' star-band rungs - a hero with an active does not spend every turn on one ability,
which is half of why the low-star basics run to about 350% of Attack against Pinoy's 105% to 110% -
and re-opens the two ceilings the band read off the tiers above. `Docs/PINOY-LIST.md` section 2c
carries the four rows as a record of what the kits do rather than as a question.

**The three 5-star kit questions are resolved, and all three were arithmetic rather than taste.**

- **Hale read x0.36 of the Marksman median.** It was both halves of the question the decision
  asked. Half was the arena: his second passive answers a Quarry target gaining a Shield, and the
  word Shield does not appear anywhere in `enemies.json`, so it is dark against every enemy in the
  game; his active's missing-HP rider needs an ally to have struck the Quarry and fired zero times;
  and he takes 19 turns over the five seeds against Talren's 27, Edrin's 24 and Nysera's 34,
  because he has no survival clause. Per turn he read 1,583 against a role per-turn median of about
  2,875. The other half was the kit: his basic is one blow and his active is one blow at one body,
  where his three 5-star peers each carry a second damage clause or hit the whole line, and every
  4-star and 3-star Marksman had a bigger single-target active than his 220%. His two numbers are
  250% and 570% now and he reads x0.79. The Marksman median did not move, because it is the mean of
  Talren's 77,618 and Nysera's 88,333 and he sits under both.
- **Altheon III was unreachable, and no roster could have reached it.** The ceiling was 120% of his
  Attack and Immaculate Record banks 30% of one blow a round, so closing it needed a single blow of
  four times his own Attack; his largest authored effect is 185% and it resolves before the same
  cast applies the record it would feed. The Subject is also faster than he is, so its clock ticks
  before his turn and a 2-round Subject contained exactly one of his turns, and the one blow it
  recorded was whichever hit landed first rather than his. The Subject lives 3 rounds now, records
  only Altheon's own blows, and the ceiling is 50% of his Attack. It closes in 4 of the 5 sweep
  seeds where it closed in none, which lights Altheon III and his second passive with it.
- **Seraphine's Cross Reference worked against her own Volume Closed**, and the reason was that a
  record reaching its ceiling was thrown away unread: only Contradiction Filed paid it out, and
  Volume Closed removed the same Mark and Ledger without releasing them. Underneath that, the
  closing half could never run at all - it was gated on a MarkAlt that Contrary Evidence, her own
  Dispel, strips on the same trigger that fills the record. The book closes in one firing now and
  files what it read: suppression, turn shift, Footnote off, the record released as damage, and the
  15 Energy. Cross Reference makes the payout arrive sooner and start 30% of her Attack higher, and
  Volume Closed keeps everything it did. The Dispel half of the finding is open decision 3 below.

**And the Dispel decision, open decision 3, is closed: the repair was spent.** It read "a Mark counts as a buff to
`Dispel`, and repairing that is an engine pass rather than a balance edit", and the open half was
never the reading - it was whether to pay for it. `Dispel` now asks `StatusInfo.IsInert` before it
takes anything, which is the same question `ApplyStatus`'s BuffSuppression gate and `CombatUnit`'s
beneficial count already ask, and it was the last rule in the engine reading "not harmful" as
"buff". The bill was what the decision said it would be and all of it was paid: 22 of 72 stage
fingerprints moved on the repair alone, `Tools/solve-recommended-power.ps1` was re-run because Kael
is one of the five bodies the balance line fields, 13 of 24 stages moved with it and 57 of 72
fingerprints in total, and the geared CEIL band that went red on `2-4` at 94.5% reads 91% under the
re-solved levels. Three dark-talent arenas moved rather than three being rewritten: Kael II to a
body that holds four long buffs, Seraphine I to a squad that leaves the record under its cap, and
Seraphine II to a room where the book does not close before the record is read. **Kael II is the
one talent that went dark in the sweep and stayed dark**, because the sweep's enemy line carries a
single one-round AttackUp that has expired by the time he casts; it is proved in the dark arena
instead and is named in the coverage note under **Known bugs**.

1. **All four walls resolve in zero extra sittings, in simulation.** A stage is
   `waveCount` times 12 wins even for a player who never loses, so every stage costs a floor of
   `ceil(ladder / 40)` sittings whatever its difficulty, and the wall is what it costs on top of
   that. The table reads 0 extra sittings and 0 extra hours on `1-9`, `1-12`, `2-6` and `2-12`. A
   mid-chapter wall cannot bite under the two bands together: two sittings carry eight spare
   battles on a 72-battle stage, and an ordinary CEIL of 78% to 92% costs a player arriving at
   x1.00 to x1.05 about one. **The decision is to validate this with real playtests rather than
   with more simulation.** The simulated player never loses to anything the model does not price,
   and "a wall that costs nothing" is either a correct reading of a generous economy or a model
   that cannot see what a wall is; another harness pass cannot tell those apart. **The fusion spine
   did not change the answer against the committed curve**: over eight walks with eight first seeds
   the four walls cost 0 extra sittings in 31 of 32 readings and 1 in the other, because the player
   who fuses meets them above their bands. See open decision 4.
2. **Chapter 1 has a yardstick of its own now, and it disagrees with the old one.** The pinned
   geared line is five 5-stars worth about 6,900 to 7,400 power at level 1, more than most of
   chapter 1 asks for, so ten of chapter 1's twelve stages were FLOOR-PINNED and only its two walls
   were graded. `ChapterOneLine` is the pinned line's five roles at three stars (Petra, Bren, Elin,
   Avera, Selka), the lowest tier with the whole kit shape, held to the caps the game gives it.
   `InstrumentLineFor` reads every chapter 1 stage with it wherever it can stand at the stage's
   `recommendedPower` (about 5,200 to 8,200) and falls back to the pinned line where it cannot; the
   solver, the guard and `--ceil-reconcile` all build their line through it, so they cannot disagree
   about which line a stage was read with. **On the committed campaign it grades 6 of chapter 1's 12
   stages where the pinned line graded 2**: `1-7` to `1-11` on the 3-star line and `1-12`, at 8,623,
   on the pinned line, because it asks for more than the 3-star line can reach. Chapter 2 grades all
   twelve, as before.
   **The two lines are not interchangeable, and that was measured before it was built on.** Power
   does not carry across lines with different Level profiles, because combat reads Level itself for
   Defense mitigation: at `1-9`'s power the pinned line stands at about Level 2 and the 3-star line at
   about Level 6, and the 3-star line clears `1-9`'s ceiling 100% of the time against the pinned
   line's 87.9% (`--line-calibration`, 2,000 samples). The economy walk's player meets `1-9` with
   three 5-stars and two 4-stars at Levels 4 and 5, so the 3-star line is the one whose Levels look
   like a player's there, and the pinned line at its own floor was reading chapter 1 as harder than a
   player finds it. Read past its caps the 3-star line cleared every CEIL in the game at 100%, which
   is why it is held to them. **What it showed on the campaign before the wall re-solve was a chapter
   1 with a hole in its shape**: `1-6` read 0% at its ceiling with a blocking battle at 64, `1-7`
   58.4%, `1-8` 90.9%, and `1-9` to `1-11` 99.6% to 100%. The re-solve measured those stages against
   it, and **chapter 1 now grades 7 of 12, `1-6` to `1-12`, every one inside its band** (`1-6` 83.8%
   to `1-11` 88.8%, `1-12` 63.8% on the pinned line); `1-1` to `1-5` ask for less than even the
   3-star line at level 1 and stay pinned. Chapter 2 grades 12 of 12.
   **The guard reads a graded CEIL at 2,000 samples (`GuardCeilSamples`), not 200.** At 200 it read
   `1-9` at 80% against a 4,000-sample reference of 87.8%, and `1-12` at 53% against a true 60.8%,
   which was answered with a hand move and a whole chapter 2 re-solve for a content change worth
   under a point. At 2,000 its standard error is 0.8 points at 85% and 1.1 at 65%. It costs about
   ten seconds of `build-check`.
3. **The Hub's right rail sits on painted architecture.** The delivered `hub.png` puts the Proving's
   gatehouse hard against the right edge, roughly x 0.82 to 0.97 of the painting, and the rail's
   discs land on its right tower at every supported aspect. No marker and no label is covered -
   `HubMarkerClamp` reserves the rail's column and slides the Proving's plate left while its tail
   stays on the gate's door - but chrome on a painted building is what the layer 0 rule exists to
   prevent. **The proposal is to move the rail to the left edge**, where the painting has a rock
   face and nothing built until the barracks at about x 0.08, which is the one placement that
   satisfies the rule outright. `HubStyle.RailOnRightEdge` is the switch and it costs one line
   either way; a right-hand rail is what the brief asked for, so which of the two rules wins is not
   the layout's call. The other two ways out are repainting the right 7% as landscape, or accepting
   it.
4. **The fusion spine moved the power mix and did not make walls bite, and the committed campaign
   is now the wrong yardstick for it.** Before the spine the campaign was a gold-into-levels
   treadmill: at the first attempt of `2-12` the POWER SOURCES block read levels 64.4% of team
   power, roster 20.1% and armor 15.5%, with 86% of the gold that bought those levels idle income.
   The intended shape is roster-led with levels as a top-up, the way an ascension-gated idle RPG
   works. `ReportPaceTargets` grades two targets on every run, **reported and never failing**, and
   `ReportPaceAcrossSeeds` reads them over twenty walks:
   - **Target A was reported wrong, and the real improvement is much smaller.** The spine first
     reported roster 49.7% to 56.9% against a pre-spine 21.0%. That split counted the line at TIER
     level 1 as roster, and under F6 an ascended hero starts its new tier one Level past the old cap,
     so every Level gold bought below a tier turned into roster the moment the hero ascended: the
     measure could not tell copies from gold, and it is not like-for-like with 21.0%, which had no
     ascension in it. `SplitPace` now attributes every Level to what bought it - a level-up is gold,
     the Level an ascension grants is copies - and `ReportPaceTargets` grades that; the standing split
     is printed beside it. **By what bought it, over twenty walks, with the Striker pass in: roster
     30.1% to 37.9% and levels 58.0% to 66.3%** (targets over 50% and under 35%, met in none),
     against the standing split's roster 50.0% to 62.5% and levels 34.0% to 45.7%, with no Level the
     walk could not attribute. Against the pre-spine 21.0% roster and 70.7% levels, fusion raised
     roster's share by 9 to 17 points, not the 29 to 36 the standing split claimed. An ascension grants one Level;
     the tier's other Levels are bought with gold at the tier's price, so they count as gold.
   - **With the spine, on the committed campaign**: both finales cost 0 extra sittings in 39 of 40
     readings over twenty walks and 1 in the other (target 2 to 4, met in none). The simulated player
     swaps a 5-star made by a conversion into the line in every walk, and in seven of eight does it
     in the same sitting the conversion made it. It meets chapter 2 at x1.3 to x1.8 of
     `recommendedPower`, which is the power-band guard's red.
   - **Why A stops short**: the caps put levels at 35% only if the rest of the growth is roster, and
     the roster a walk builds by `2-12` is a line of tier-1 heroes, x3.1 its starting line at level 1
     against the x5.9 A needs at the levels it carries. Tier 2 costs four more copies of the same
     hero. (The split this sentence first quoted was partly definitional; see the correction above.)
   - **Why B stops short**: walls cost sittings when a player arrives under them and has to grow;
     this player arrives over them, because the curve was derived for one who could not fuse.
   - **Re-solving does not rescue B.** Solved against a throwaway copy with the spine in place (16
     iterations, `2-12` from 27,601 to 31,591, nothing committed), `2-12` cost 0 extra sittings in
     five of the eight walks and 8, 19 and 21 in the other three. A wall under fusion costs the
     time to the next lucky conversion roll, and none of the sixteen finale readings landed in 2 to
     4. Star-gated caps do not deliver target B on either curve.
   - **The campaign is now re-solved under the wall rule, and B is unreachable on this fusion
     economy - measured, not argued.** The rule (fusion accelerates a wall, it never gates one) puts
     every wall within reach of a player who never converts and never ascends, whose line tops out
     at the tier-0 cap near 12,000 power; the player who fuses reaches `2-12` at about 33,000. Twenty
     walks of each: the reference player finishes the campaign every time, paying 3/6/7 extra
     sittings (median / 90th percentile / worst) at `1-9`, 0/0/1 at `2-6` and 0/0/7 at `2-12`; the
     player who fuses pays 0 at every wall past `1-9` and meets `2-12` at x3.3. B needs fusion to be
     an accelerant worth a few sittings of gold, and today it is a 2 to 3 times multiplier. Raising
     the tier-0 cap does not close that gap, it widens it: on throwaway copies with a 5-star cap of
     18 and of 27, the player who fuses met `2-12` at 48,300 and 53,900 (median of twenty), while
     the reference player met `2-6` at 15,700 and never reached `2-12` at a cap of 18, and met
     `2-12` at 22,900 and stuck there in all seventeen walks that reached it at a cap of 27 - on the
     campaign as it stood before this re-solve - because every ascended tier starts one Level past
     the tier-0 cap. **What would make B
     reachable is a smaller fusion multiplier** - the ascension curve, the tier jump or the copies
     in `balance.json` and their derivation - which is a change to the fusion spec, not to the
     campaign. It was offered and not chosen; the strict rule was.
   The rest of this entry is the record from before the spine:
   - **A, the power mix at the last stage: levels under 35%, roster over 50%.** It is graded on a
     multiplicative split rather than on POWER SOURCES' blocks, because `StatMath.Build` multiplies
     level growth and the star multiplier together: the additive roster share is about
     `1 / (1 + 0.115 * (L - 1))` whatever the stars are, so fusion could never move it. The split
     used is roster (the deployed line at Lv1 over the starting line at Lv1), levels (bare power
     over the line at Lv1) and armor, each as its log share of the growth since `1-1`. It reads
     levels 70.7%, roster 21.0%. Meeting both at today's levels needs the deployed line at Lv1 to
     be x12.15 the starting line; it is x1.53. Those readings are at real summon rates, and were
     reported rather than tuned toward when the campaign was re-solved against them.
   - **B, wall cost: a chapter finale costs 2 to 4 sittings beyond its own ladder floor.** Both
     finales read 0, two short. This grades the same table as open decision 1.

   **What is open is whether these are the right targets.** Fusion as `DESIGN-HERO-FUSION` specifies
   it produces a 5-star, the top tier the star multiplier has, and the line the balance pass fields
   is three 5-stars, a 4-star and a 3-star; swapping in a better body costs its levels, so the
   simulated player never does. Fusion to five stars can lift the Lv1 line by about 15%, not
   by the x8 that target A asks for, so A is reachable only if fusion or an ascension track goes
   above five stars with multipliers far past today's x1.34, or if the level cap is gated by stars
   rather than by account rank. That is a decision for the fusion spec, not for a tuning pass. On
   B, chapter 1's wall band was set at x1.00 to x1.05 precisely so a chapter 1 wall clears on the
   next claim rather than two days later, which is one extra sitting; 2 to 4 contradicts it on
   `1-12`, and a finale at x0.99 costs nothing today, so B may not be reachable inside the wall
   band the curve check enforces. **The proposal is to keep A on the multiplicative split and let
   the fusion spec say which of the two levers it pulls, and to grade B at 1 to 2 on chapter 1's
   finale and 2 to 4 from chapter 2 on**, pending the playtests open decision 1 already calls for.
5. **The guild dungeon's size rules** (**Guild dungeon size and attempts**, under **Guilds, phase
   one**). Built as proposed below, every one reversible in content or one SQL function:
   - **What a deep floor pays.** `GuildDungeonRewards` is a placeholder that pays nothing. The
     design asks for breadth or recognition, never more pay per member for depth, and nothing
     random or bought.
   - **The bands.** 5 floors under ten members, one more per five, 10 at the cap. The alternative
     the design text reads as, fewer floors for a small guild, would take floors away from guilds
     that have them today.
   - **The cycle cap's scope.** 15 per account across guilds, so changing guild mid-cycle does not
     reset it. Per guild is the other reading, and it lets a player who hops guilds attempt more.
   - **When the snapshot is taken.** On the cycle's first status call or attempt by anyone in the
     guild. A guild that fills up just before that gets the longer ladder all week; nothing extra
     is paid for it.
   - **The pre-existing guardrail margin.** About 1.7% of base floors, mostly Champion floors, are
     cleared by under 25% of 3-star-or-fewer rosters. Accept it, or move `guildDungeonFloorOnePower`
     or `guildDungeonChampionLevelBonus` and retire every replay.

## Known bugs

The harness reads first, then the rest.

1. **The arena matrix's two coverage checks are green.** They were the two standing reds, and the
   talent pass on `talents/make-them-work` closed both. The talent half read (c) 3 - Aeron I,
   Aminah III and Caldus III - and reads (a) 70, (b) 20, (c) 0. The rule half read (c) 4 and reads
   zero: every basic, active and passive in the roster is observed changing a fight, in the sweep
   arena or in a named room.

   **`MatrixWaits` is empty, and that is the finding rather than a list nobody filled in.** Three of
   the four rules were re-authored, because a rule that fires and changes nothing is broken and not
   quiet, and the fourth only ever needed a different room:

   - `finn_ringing_toll` took Energy off the bar of the enemy whose cast raised it, and `TakeTurn`
     sets that bar to 0 for the cast before the trigger goes out. That is not an ordering bug and it
     was measured as one: every active costs a full bar against a cap of one, so the toll lands on an
     empty bar whichever happens first. The bell is heard by the OTHER Ringing enemies now.
   - `eira_leave_no_corner` asked for an ally holding Mobile Cover to be singled out. Each gate lands
     alone in its room and the pair never did in 96 rooms - one firing with only the Cover gate, six
     with only the single-target gate, none with both. It keeps the gate it is named for.
   - `vaelis_opening_moves` asked for the DEFEATED body to carry Departure, and none did in 360
     rooms, because an ally's hit on a linked body spends the link before a killing blow finds it.
     The link is mutual, so the clause reads it from the end that survives.
   - `aminah_emergency_release` is the one where the rule was right and the ROOM was wrong, which is
     what this entry used to suspect when it said the three rooms broke rather than the rules.
     Beside Eira AND Isara nothing holding Priority ever crossed the line; beside Isara alone it
     crosses once. Eira's Mobile Cover stood between them. The hero is untouched.

   Aminah III and Caldus III were the other two of those three rooms, and both turned out to be the
   talent rather than the room: Aminah III hung off `aminah_emergency_release` itself and now hangs
   off the Priority expiring, and Caldus III waited on a Declared Target falling under a third of its
   HP, once per battle, which no arena arranges.

   **What a class (b) pass is worth in play is still measured rather than assumed.**
   `EMBERFALL_MATRIX_NORMAL=both` fields each class (b) rule's hero in the pinned line and in the
   balance line across every shipped stage, 1,152 battles a rule. That reading is a report and not a
   check, and it is the slow half of the matrix by a wide margin - the coverage checks alone run in
   about two seconds. The list of rules that fire in a room and never change a normal-play fight was
   last taken before the talent pass and eight of its ten entries are talents the pass rewrote, so it
   is worth retaking rather than quoting.

Beyond the harness:

- **Marn's Relief After Loss healed an enemy once in 8,832 campaign battles**, because the
  Coverguard it answers was on an enemy when it was spent. How the Guard got there was not traced.

- **Rheia has no skill clip.** Her delivery included an `idle2` where `skill` should be, so her
  active plays with no animation and the idle continues. Silent, not an error.

- **The talent screen reads "no clear change" for talents the harness reads as clear gains, and
  it is resolution, not a sampling bug.** On the local editor save of 2026-10-11 (Kael at Level 10 beside
  Kiva, Isara, Kest and Oris), `TalentTrial` at its 40 seeds reads Kael I +468, t 1.69 and Kael II
  +33, t 1.91, both `Unclear`, and Kael III +2307, t 9.7. The harness reads the same three as +757,
  t 18.4, +4311, t 53.7 (in `s_triple`, because the sweep arena never raises Kael II) and +6535,
  t 8.5. Seeds, pairing, the talent copy and the ascension gate all check out: both arms are the
  same event for event until the talent first fires. The difference is the arena. The mirror is an
  even fight (21 of 40 won), so the first firing flips who wins and how long it runs, and Kael I's
  bare and talent outputs correlate at 0.09 across seeds where a stat-stick sweep keeps them
  paired. Kael II stays paired, but changes only 11 of 40 fights in that line. At 120 seeds all
  three read `Gains` (Kael I +451, t 3.0; II +65, t 4.4), for 370 ms of desktop simulation against
  189 ms. The fix is a call about what the screen shows: more seeds for an unclear reading, and an
  `Unclear` line that prints its estimate and how many fights it changed, rather than a word that
  reads as zero.

## Waiting on Pinoy

[`Docs/PINOY-LIST.md`](Docs/PINOY-LIST.md) is the whole list and the only place it is maintained:
the Celen and Nereia deliveries, every kit clause the engine could not say as written, the strike
frames, and the note about the right edge of a future hub painting. It is one file so it can be
sent as one message.

**Section 2d carries the departures on 5-star heroes.** Every number moved below five stars so far
was the star band's and is recorded as a class rather than row by row; these five are a sentence the
engine says perfectly well that a measurement caught doing nothing, or doing something nobody could
have meant. Altheon's Subject and its ceiling, both halves of Seraphine's Volume Closed, Hale's two
percentages, and Altheon's Witness Script rider - 35% where 42% shipped, and resolving before the
blow it rides instead of after it, so that its condition can be false. A sixth reading sits under
that section and changes nothing: Altheon's Final Record pays for Records that Preserved Moment is
already spending, and it had been reported as losing on that trade; over 150 seeds it is a gain of
about 1%.

**Section 2g is the newest**: the eleven sentences ENGINE-GAPS pass 3 rewrote, nine of them Pinoy's
sentence said at last and two of them real questions - Cyrra's store also pays when it runs out,
and Isolde's rider counts debuffs because Cinder cannot stack. Section 2d gained Aminah's three
numbers, the first moved by the 5-star check.

**Section 2e is the talents that cost the hero who picks them, and there is one**: Nysera I, a
trade. Kael III and Mireth I were the two it was opened for, and both were rewritten until they
gain. `CheckTalentsAreWorthPicking` reads all 90 and fails if a talent starts losing without being
listed, so the day a kit change makes one a trap the harness says so.

## Later

Not scheduled and not blocked on anything in **Next**, listed so it is not rediscovered.

- **What text is still a placeholder.** `Docs/TEXT-AUDIT.md`, generated by `python
  Tools/text_audit.py`, lists every remaining placeholder string in content and code with file and
  line: stage names proposed by us and awaiting Pinoy, the Store's coming-soon and placeholder-price
  labels, content numbers their own files call placeholders, and the units still drawing a mannequin.
  Regenerate it rather than edit it.
- **Summon inflation, measured and not changed.** A summon costs 120 Shards at every stage while idle
  Shards a day grow x9.6 from 1-12 to 6-12 (680 to 6,500) and, with a committed player's repeat-clear
  purses, x51 (1,418 to 72,380; those purses are 79% to 91% of their Shards from 3-12 on). Days per random
  5-star fall from 5.9 at 1-12 to 0.62 at 6-12 on idle alone and to 0.06 with the sweeps. Two options, both
  of which move a hashed file: a summon cost per chapter (120, 120, 200, 300, 410, 530 at 1-12 to 6-12 holds
  an idle player at 2.7 days per 5-star), or idle Shards flattened to the 2-12 level (74 an hour, 23% of
  today's at 6-12). Neither touches the repeat-clear purses, the larger driver, and the cost option cannot
  serve an idle and a committed player at once. `SummonInflationStudy` (`--check SummonInflationStudy`)
  prints the tables; the note is `summon-inflation.md` in the R17 handoff data folder.
- **Guilds phase two, systems 4 and 5.** Systems 1 to 3 - the dungeon, the Waymark economy and the
  guild tech tree that replaced the four-node Path - are built (see **Guilds, phase one**). The
  fodder exchange and (parked) guild wars remain, as sequenced in `DESIGN-GUILDS-PHASE-TWO.md`.
  Guild level exists now and gates the tree; `DESIGN-GUILD-TECH.md` in the `claude` folder G6 lists what could read
  it later (a marketplace, a sixth floor, a fourth attempt, a guild hall) and says that none of
  them is designed today. A sixth to tenth floor now exists by member count, not guild level
  (**Guild dungeon size and attempts**), so a level-gated floor would be a second, separate rule.
- **A marketplace.** Nothing built, nothing designed.
- **Chapters 3 to 6's enemy art**, when it exists. The stages are built, and their backgrounds
  are painted (R21).
- **Texture compression.** The 855 MB figure this entry once carried was re-taken against a real
  Windows player twice. The first player, before the 336 by 396 sheets, was 318 MB unzipped and
  188.5 MB zipped, with 208 sheets packed as DXT5 Crunched. **The second, taken in R16 with the
  336 by 396 sheets in, is 3237.5 MB unzipped and 676.1 MB zipped**; its build report packs
  3132.8 MB, of which the 338 battle sheets in `Sprites/Units` are 3018.3 MB, all BC7 (12072.9 MB
  as raw RGBA, each sheet up to about 9.4 MB packed, 8.9 on average). The earlier prediction of close to 2.9 GB of growth
  held. Portraits, badges and HUD pack to 24.8 MB and the backgrounds to 19.6 MB, so the Units group
  is the whole of the growth. The zip wins back 4.8x, and 676.1 MB zipped is still under 1 GB; only
  the unzipped player is over. **The cheapest fix, not applied and an owner call**: set `win_format` of
  the Units group in `Tools/texture-import-policy.py` from BC7 to DXT5 Crunched. Measured on one
  sheet, 9.4 MB packed falls to 1,004,264 bytes, and extrapolated over 338 sheets the player falls to
  about 0.4 GB unzipped (an extrapolation, not measured across all sheets). It is visibly softer:
  hair strands, face and armour trim smear into blotches in a battle capture, which is why the
  policy chose BC7 on purpose and why this reverses a recorded decision. Lowering
  `maxTextureSize` is not a good alternative: it halves the texel density, which is the blotch
  problem again (that it also shears the frame grid, as the policy comment says, is unverified).
  The importer metas are owned by the policy script, so the durable form of the change is the policy
  edit and not a hand edit of the metas.
  Source: `Tools/build-windows.ps1` at the R16 measurement (`Docs/BALANCE-REPORT-R16.md`, Player
  size). `EmberfallBuild` writes this report beside every Windows player, and
  `Tools/build-windows.ps1` prints it. The APK has not been measured: Android reads its own importer
  overrides, and the 855 MB may still be true there. Measure an APK the same way before sizing the
  work.
- **An APK**, once there is a device.
- **iOS**, once there is a Mac and a developer account.

## Where the reasoning lives

[`Docs/archive/BUILD-ORDER-HISTORY.md`](Docs/archive/BUILD-ORDER-HISTORY.md) holds all of it,
unedited, including:

- The eleven authoring efforts that finished the 5-star roster, kit by kit, and the nine engine gaps
  that blocked the first passes.
- The four batches that finished the 4-star roster, the instrument that says a 4-star is smaller
  than a 5-star, and the floor that says how much smaller.
- Every engine pass, with what it measured, what it deferred and why, and what it refused to build.
- The aspect-rule removal, by far the largest behavioural change in the project, and its measured
  cost.
- The campaign retunes, the full derived `recommendedPower` table, and the economy retune under it.
- The eight kit defects, and the five engine rules the engine claimed in a comment and did not keep.
- The four-star-prep pass that turned thirty-four fired tripwires around and took the harness from
  37 failures to 3.
- The known traps carried forward, the numbers already decided, and the relic and aspect hook
  conflict with its resolution.

The security review of every Supabase migration and the receipt functions, with its findings and the two fix
migrations (`0020_security_review.sql`, `0021_security_review_launch.sql`, neither applied), is
`Docs/SECURITY-REVIEW-SUPABASE.md`.

Every other `DESIGN-*.md` in this repository is a decision record from its date. The reasoning in
them is durable; the status claims, names and numbers are not. Never conclude from one that a system
is unbuilt.
