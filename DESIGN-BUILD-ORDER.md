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
against the campaign as committed - against a live `Tools/build-check.ps1` run, a live
`validate_heroes.py` run and the files themselves, not against this document's own prior claims.
Where this file and the archive disagree, this file is right about status and the archive is right
about reasoning.

## How to read this file

- A system entry says what is **built and observable in the tree**. If a system is not listed here,
  it does not exist.
- **In progress** names live branches. A branch name is only ever cited there, only while it is
  unmerged, and it goes away the moment the work lands and the files can speak for themselves.
- Numbers here are measurements from a real run, not estimates. The two that matter most are the
  harness failure count and the validator failure count, and both are named below.

## The instruments

Two runs decide whether the tree is healthy, and they measure different things.

- **`Tools/build-check.ps1`: 5 failures on `main` as merged, 3 on the wall-resolve branch under In
  progress, both measured from a clean run.** It lints the docs, compiles the core and the
  Unity-facing code against the shim in `Tools/UnityShim`, and runs `Tools/DevHarness`. Two of the
  five were the arena matrix's coverage checks, one for talents and one for basics, actives and
  passives; the talent pass on `talents/make-them-work` turned both green (**Known bugs**). The
  third is the campaign power-band guard. On
  `main` it is twelve stages over their band, because the table there was derived for a player who
  could not fuse; on the branch the table is re-solved under the wall rule and it is two, `2-6` and
  `2-12` at x1.197, a reading rather than a regression (**The campaign power curve**). `main`'s last
  two are a merge seam the branch closes: `CheckSolverAndGateCompileOneList` found
  `Tools/DevHarness/ArenaMatrix.cs` in `build-check.ps1`'s file list and not in
  `solve-recommended-power.ps1`'s.  The geared curve's CEIL band is green. The
  balance line is Kael, Kiva, Isara, Adric and Orelle, and the pinned line the CEIL is read with is
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
  **A 6th failure is a regression.** Diagnose it; do not add it to the list. One caveat that is not
  a regression: a turned-around tripwire goes red if the content it names stops using what it
  names.
- **Doc lint is clean.** `Tools/doc-lint.ps1` scans 43 tracked markdown files and reports 122
  baselined violations and **0 new**. The baseline is the known debt in
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

- **`stage-fingerprint-baseline.txt`: 72 lines.** One per shipped stage per sampled seed, over the
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

Eight JSON files in `Assets/Resources/Content`. Balance and content changes go there, not into code.

- `heroes.json`: 94 heroes, **all 94 authored**. 8 one-star, 12 two-star, 18 three-star,
  26 four-star, 30 five-star.
- `enemies.json`: 96 enemy templates. **Pinoy's 84 are the campaign**: aspect, kind, stats and kit
  are ours and the mapping is `Docs/CAMPAIGN-SPEC.md` section 9. Each of the 84 names its future
  sprite and is `pending`, so it draws the mannequin. **The shipped 12 are retired but kept**: no
  stage fields them, so neither the guild dungeon nor the Deep Recital draws them, and they stay in
  the file because `PrologueScript` spawns seven of them by id.
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
`Docs/ABILITY-TEXT-STYLE.md`: all 427 basics, actives, passives and talents on the 94 heroes, with
29 exceptions, all of them drafting notes whose sentence and kit still disagree, each a yes or no
question in `Docs/PINOY-LIST.md` section 2f. Nothing is pinned as unwritten: Nysera's False Target,
the last, has text since defect pass 3 repaired it to strike the enemy her Link turns a blow onto
rather than Nysera herself. `CheckAbilityTextIsPlayerFacing` enforces
it: a description that is empty, holds a digit or a percent sign, or reads as a drafting note fails,
and so does a pinned exception that no longer applies. The card still does not show a description
anywhere; the text is ready for whichever screen shows it first.

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
- **Two are trades**, and they are the two whose purchase is real and is not made of output.
  Nysera I strips a buff off the body targeting her, which in the sweep is a taunting tank's
  Provoke, and frees the whole line's aim. Marrow I spreads Promise over a third ally for a smaller
  Promise heal. Three more were on the list and are not: Rheia I paid two Energy off every Applied
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

### Fusion and ascension

`DESIGN-HERO-FUSION.md` holds the rules, F1 to F9, and the reasoning; this is what the files show.

- **Caps** are `StatMath.HeroLevelCap` of star tier and ascension tier: 5, 6, 7 and 8 for one to four
  stars, and 9, 18 and 39 tier levels for a 5-star at tiers 0, 1 and 2. Account rank sets none of it.
- **Ascension** is `GameSession.Ascend`: copies of the same hero only (1 for the first step, 4 for
  the second), and the tier level resets to 1. **A hero's Level stays the one number its stat line
  is built from** - each tier walks the one level ladder at its own stride, x2.035 and x6.175
  Levels per tier level (`StatMath.LevelAt`) - because combat reads Level for Defense mitigation, and
  a multiplier on the stats instead made an ascended line of the same power clear `2-12`'s ceiling
  39% of the time against 63%. The level cost is read off the tier level, times the same multiplier.
- **Conversion** is `GameSession.Convert`: heroes below five stars worth 100 points (15, 10, 10 and
  18 per body by star tier) for a random 5-star of the chosen aspect. Ember, Tide, Verdant and Gale
  take same-aspect fodder; Umbra and Lumen take one seed of their own and fodder of any aspect.
- **Every number is derived, not declared**: `CheckFusionEconomyDerivation` rederives each one from
  the summon weights, the hero growth rate, the starting line and a campaign growth of x7.611,
  prints the F6 crossover for every 5-star, and fails if `balance.json` disagrees. The growth was
  the finale's entry over the starting line until the wall rule made the finale an output of the
  caps; it is pinned at the value the caps were derived at (`CapDerivationGrowth`).
  `CheckFusionRules` holds `GameSession` to each rule and to save version 16.
- **There is no fusion screen.** The hero card's level readout counts Levels, so an ascended hero
  reads its Level on the ladder over the Level its cap is at.

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
asserted by the generator rather than eyeballed**. There are deliberately no finale bumps: a x1.25
finale has to be given back at the next chapter's opener, which is the non-monotonic step both
shapes `CAMPAIGN-SPEC.md` 10.3 rejected died on.

`ReportPaceTargets` grades the shape the campaign is meant to have against the one it has, and is
reported rather than failing. `ReportFusionWalk` shows every line change the walk made and the line
at each stage's first attempt, and `ReportPaceAcrossSeeds` reads both targets over eight walks with
eight first seeds, because a conversion's roll makes one walk's roster one draw. See open
decision 4.

**The economy responds to shards now, not gold.** `CheckEconomyRespondsToIncome` scales gold and
shards together on the back half of the campaign; the late-campaign ratio moves 2.36. Gold alone,
still printed, moves it 0.08, because gold stops at each hero's cap.

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

**The simulated player here is the wall rule's reference player, not the one who fuses.**
FUSION ACCELERATES A WALL, IT NEVER GATES ONE: every wall must be passable with levels at the cap and
no conversion and no ascension at all. So the band is read on that player's walk,
`recommendedPower` is derived from it, and `CheckFusionNeverGates` walks twenty of them and fails if
any one sticks anywhere. The player who fuses meets every stage above its band, and that is the rule
working rather than a failure of it.

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

**Two costs were measured rather than argued, and both are written into `campaign.json` so the next
pass does not rediscover them.** They are both open decisions now; see below.

### Gear

Armor rolls through `ArmorRoller` with primaries, subs, tempers and attunements, shown on
`InventoryScreen` and `ForgeScreen`. Relics are a two-tier model: a Legendary changes a rule in the
effect pipeline, a Mythic changes a rule and carries a stat block on top of it. Relic progression
ships on top of that, with `RelicShopScreen` as its storefront.

### Summoning, idle income, save

Summoning with a pity rule that guarantees distinct unowned heroes. Idle income accrues against a
real clock and is capped. `SaveSerializer` writes the whole document, at
`SaveSerializer.CurrentVersion` 17, and a save from a newer build is refused rather than read.
Version 15 keeps records this build cannot resolve; version 16 adds each hero's ascension tier;
version 17 adds the Guild Path's copy of the server's answer and the formation's saved lines.
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
- **The Path.** Second, Third and Fourth Line (saved formation lines, `GameSession.SwitchLine`, tabs
  on `FormationScreen`) and Lines Keep Talents, at 100, 200, 300 and 200: each node costs its depth
  in mean cycles. `GuildPathScreen` is the storefront, reached from the formation screen and the
  dungeon. Every node is breadth: `CheckEveryNodeIsHorizontal` allowlists the node kinds and fights
  the same stage identically with every node owned and with none.
- **Server.** `Server/supabase/migrations/0011_guild_path.sql` - **written, not applied.** It adds the
  ledger, the unlocks and the node table, `guild_dungeon_attempt` (submit, board bits, base pay),
  `guild_path_state` (settles closed cycles' board bonus, answers) and `guild_path_unlock`. Keyed by
  account, not guild. Until it is applied the client files attempts through
  `guild_dungeon_submit` and pays nothing.
- **Content.** `guild-path.json` is a ninth content file and the one that is **not** hashed into
  `ContentVersion`, because nothing in it is read by a fight.
- **Not done:** the live guild rules test does not cover 0011; `guild_dungeon_submit` is still
  callable; Waymarks have no sink once the four nodes are bought.

### The Deep Recital, The Proving, events, shop

`RecitalGenerator` builds an endless floor ladder with a Guardian every tenth floor and an unbroken
Aspect cycle. **It never draws the prologue's scripted loss**: `RecitalGenerator.ScriptedLossEnemies`
reads the prologue's Boss step and skips whatever it fields that no campaign stage does, which is The
Unmade. Before that every Umbra floor fielded it, twice on floors 5 and 11, and
`CheckRecitalDrawsNoScriptedLoss` now climbs 120 floors to say none does. It draws the templates of the
floor's Aspect **that some campaign stage fields** (`GuildDungeonGenerator.TemplatePool`, the guild's
rule), bosses included, so the retired shipped 12 are out of it; `Docs/CAMPAIGN-SPEC.md` 9.6 and 10.7
measure what each change did to the floors. Coda is the Recital's own currency and accrues on every clear. **There is no Coda sink
yet.** `ProvingGenerator` builds opponent lines. The events engine is content-driven through
`events.json`. Shop screens are wired.

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
**Valor and Coda both say, in the bag, that nothing spends them yet**, because a currency banked
towards nothing should say so where a player goes to look for it.

**The Campaign chip row sizes itself from the chapter count.** `CampaignScreen.RefreshChips` shares
the row's 1780 units out between however many chapters `campaign.json` holds, clamped to [200, 300]
per chip, and boxes each chip's labels at the chip's real width with best-fit rather than at a
literal 280 with Overflow. Eight chapters stand still; past that the row scrolls, on
`UIBuilder.ScrollStrip`. The old row was 1600 with every chip a flat 300, which fits five - and the
failure at six was not a row spilling over its edges but a `HorizontalLayoutGroup` dividing the
width it had, squeezing the plates while their labels stayed 280 wide and printed across the seams.

**The Hub is four layers, with one rule each.** It had grown by bolting controls onto the map:
three slabs over the top left with the HEROES marker behind them, GUILD twice a thumb apart, DEV
overlapping both the PROVING marker and the button it was stacked under, an idle panel holding a
quarter of the painting to show five lines about a number, and a read-only copy of the squad along
the bottom of a screen that has a formation page for exactly that.

- **0, the map.** Every structure that leads somewhere carries a marker, and nothing above may
  cover a building or its label. The market stall is the RELIC SHOP rather than a locked
  MARKETPLACE sign, because a door to a built screen beats a sign for an unbuilt one.
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

**31 of 94 heroes have drawn battle art**, and they are the thirty 5-stars plus Celen. The other 63
carry `visual.spriteStatus: "pending"` and render as an aspect-tinted placeholder mannequin with
their name under its feet, which makes "not drawn yet" a thing the game can say separately from
"this hero's art is broken". All 94 profile badges are drawn and wired, so the battle sheets are
the only art the roster is still missing, and with every kit authored they are **the last thing
between the roster and being finished**. `Docs/PINOY-LIST.md` section 5 is the list.

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
the sweep does not. CI runs `build-check.ps1` on every push and pull request.
`DevHarness.exe --ceil-reconcile [stage [levels]]` is a second development aid: it reads the solver's
and the guard's CEIL side by side against a 4,000-sample reference, and it is how the `1-12`
disagreement under **The instruments** was settled.

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
verified from the built exe rather than from the editor. **Android is still the target**; no layout
differs between them.

## In progress

**`ui/fusion-screen`** gives fusion and ascension the screen they had rules for and no door to.
`GameSession.Ascend` and `GameSession.Convert` had no caller outside `DevHarness` and
`PlayModeScreenshot`, there was no `ScreenId` for either, and two screens already told the player
to use them - `HeroDetailScreen`'s cap note on a maxed five-star and `TalentsScreen`'s
`ASCEND TO TIER N` on a locked card. It holds:
- **`FusionScreen`**, two tabs on one page because fusion's output is ascension's input. The
  ascend tab is a list of the player's five-stars and a card carrying the copy cost and three
  before-and-after rows; the convert tab is the six aspects, the whole roll pool drawn face by
  face, a two-line recipe block and a grid of bodies against a points meter.
- **`GameSession.PreviewAscension`** and the `AscensionPreview` it returns, so the page can say
  what a step costs and what it moves before the player commits. `AscensionLanding` is extracted
  from `Ascend` rather than copied, so preview and result cannot disagree.
- **`GameSession.ConversionFodderRoster`**, the per-aspect low-star count F9's seed rule is made
  of, so the screen can print the reason next to the rule.
- **the two doors**: an ASCEND/CONVERT button on `HeroDetailScreen`'s action bar with a cap note
  that names the copies the step costs, and a locked talent card that is pressable at last.
- **`Fusion` and `FusionConvert` targets** in `PlayModeScreenshot`, one per tab, seeded through
  `GrantHeroDirect` the way the `Talents` subject already is.

No save version moved, no content JSON changed and no baseline was regenerated. The build check
reads **4 failures**, the four already on `main`: the moved stage fingerprints, the CEIL band, the
campaign power band and the economy model's response to authored income.

**`talents/ascension-gate`** builds DESIGN-TALENTS T6: ascension tier unlocks talent slots, one
per tier. It holds the derived gate on `HeroInstance`, the single simulation line that reads it,
the capture-side rule that keeps a locked pick out of every published record, the locked card on
`TalentsScreen` and a `Talents` target for `PlayModeScreenshot`, which had none. No save version
moved, and the stage-fingerprint, hero-sweep and talent-sweep baselines are all unmoved. The build
check reads **3 failures**, the three already on `main`: armor's Rough slope, the geared CEIL and
the campaign power band.

**`content/campaign-stages`** replaces the shipped 24 stages with Pinoy's 72:
- only his 84 fielded, the shipped 12 kept as data and excluded from the guild dungeon and the Deep
  Recital;
- a miniboss flag on stage 6 that the power band reads as the chapter's wall;
- `CheckCampaignLinesLegal` and `CheckPrologueStillFights`;
- `CAMPAIGN-SPEC.md` section 10, with the placeholder list for Pinoy.

It solves nothing on purpose. The build check reads **7 failures**, measured after its last commit
regenerated the stage fingerprints (216 lines) from a fresh rebase on `main`. Three are main's (the
two arena-matrix reds and the power band, now at 1-6, 1-9 and 2-6), and four it breaks deliberately:
the retired 1-1 opener pin, armor's Rough slope, the CEIL band, and 16 of 20 non-fusing walks stuck.
Each is listed with its cause in `CAMPAIGN-SPEC.md` 10.8.

**`harness/solve-prereqs`** fixes the instruments the big solve reads, so that it is run once.
It is based on `main` with `content/campaign-stages` merged, and holds:
- **the reference player's line made role-sane**: `WantedLine` and `EconomyDeployLine` in
  `Program.cs`;
- **the three dead 1-1 pins retired** from `CheckCampaignMigrationRegression`;
- **`--solve-prereqs`** (`Tools/DevHarness/SolvePrereqs.cs`), a measurement instrument that asserts
  nothing;
- **`Docs/SOLVE-PREREQS.md`**: what Speed's linear price costs each power-score consumer, with four
  proposals for Matthias to pick from; the reference player's error per chapter; and every table
  tuned against 24 stages, valued at stage 24 and at stage 72.

It changes no content, no formula and no baseline. The build check reads **5 failures**, down from 7:
the dead opener pin is retired, and `CheckFusionNeverGates` is green (0 of 20 walks stick). The
power band still fails, but now reads all 72 stages, and 38 are out of band.

**`balance/wall-resolve`** carries what the fusion spine left: target A re-measured by what bought
each Level (open decision 4), the Striker pass that lifted Velis and Kiva together and emptied
`KnownF8Exceptions` (**The star band**), the CEIL guard at 2,000 samples and the chapter 1 line
(open decision 2), a `--wall-study` and a `--line-calibration` development aid, and the re-solve of
`campaign.json` under the wall rule: `recommendedPower` derived from the player who never converts
and never ascends, `CheckFusionNeverGates` failing if any of twenty such walks sticks, the cap
derivation's growth pinned, and chapter 2 nearly flat as the price (**The campaign power curve**,
open decision 4). Its stage, sweep and talent fingerprints move with the Kiva lift and the
re-solve, and are regenerated as its last commit.
**`guild/floor-conditions`** carries the guild dungeon's floor conditions described under **Guilds,
phase one**:

- `BattleRules`, `floor-conditions.json` and the seeded draw;
- the C7 suspension rule and line legality;
- the condition band and board tiles on the floor view, and the condition on the dungeon row;
- the `--guild-conditions` study and the gate checks that re-assert it;
- this file's and the design record's edits.

It moves no pinned baseline, and it adds no harness failure to the five `main` already has.

**`engine/round-end-and-rooms`** carries the round-end repair described under **Combat** (Finn's
Aftershock Clock delays at last, and `BattleSession.EndRound` raises `RoundEnd`), Brenna I's room
rebuilt as a legal lineup with the harness now asserting every talent room is one, the Aftershock
Clock's stated reason taken out of the matrix now that it is lit, and the `EMBERFALL_MATRIX_NORMAL`
reading under **Tooling**. Its last commit regenerates the pinned baselines from a fresh rebase on
`main`, and only Finn's five hero-sweep lines move. The defect pass, the Quarry clock repair, the
fusion spine and the arena matrix have all landed on `main` and are described above.

**`engine/dead-clauses`** carries the dead-clause pass (`Docs/PINOY-LIST.md` section 2i). It makes
two ordering repairs in `CombatSimulation.cs`:

- **The `LastTarget` condition subject is moved only by an effect that lands on the other side.**
  Kora's First Opening lands 649 blows across 1,152 campaign battles where it landed 0, and Velis's
  Denied Return, the fourth clause in the same trap, now reads the enemy's HP rather than her own.
- **In an `OnAllyWouldDie` window, a Guard the killing blow spent is still counted as held**
  (`SpentByLethalBlow`). Brenna's Survival of the Stubborn saves where it saved nobody, and Brenna
  III banks Resolve 26 times where it banked 2.

The same branch reads named rules in normal play through `EMBERFALL_MATRIX_NORMAL_RULES`. It
registers a room for Kora, moves Velis's What Was Taken room to seed 5, and puts the Finn
experiment into his stated reason. It adds one harness failure: guild 3 floor 4's "3 stars or
fewer" rosters clear 2 of 12 where they cleared 3, because the one roster that flipped fields Kora.
Its last commit regenerates the pinned baselines from a fresh rebase on `main`, and only Brenna's
lines move.

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
  mannequins; no enemy sheet has been delivered for any chapter. `DESIGN-WORLD`'s
  contested-region proposal for chapter 3 is moot: his chapter 3 is City of Strangers.
- **An APK**, on having an Android device to run it on.
- **iOS**, on a Mac and an Apple developer account. Neither exists.

## Open decisions

Four. None of them is a number to nudge, and neither the economy question nor the power-band one
that used to head this list is still here. The five-star review closed four more - the three
5-star kit questions and the low-tier Energy one - and the Dispel repair closed the Dispel one,
which was open decision 3, all recorded below. The numbers on the three that remain were reassigned
when it closed, so the Hub rail is number 3 now.

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

- **Counterattacks still go to a Taunt holder.** Defect pass 3 swept every effect named by a
  broadcast for the wrong-body shape and repaired the named-body modes; `LastAttacker` and
  `LastTarget` it measured and left, because the engine documents the anchor outranking them as a
  rule. Caldus's No Uncertain Ground still strikes the Taunt holder when the enemy that hit an ally
  is not it (126 times in 240 campaign battles he fought in, 72 in his 20 sweep-arena runs), and
  his Defined Duty and Jory's Worn Hinges do the same. Whether a counterattack answers to Taunt is
  a rules decision rather than a defect. The rule stays, so it is Caldus's card that is wrong:
  replacement text is proposed in `Docs/PINOY-LIST.md` section 2i.
- **Marn's Relief After Loss healed an enemy once in 8,832 campaign battles**, because the
  Coverguard it answers was on an enemy when it was spent. How the Guard got there was not traced.

- **Rheia has no skill clip.** Her delivery included an `idle2` where `skill` should be, so her
  active plays with no animation and the idle continues. Silent, not an error.
- **Six orphaned sprite PNGs** remain under `Assets/Resources/Sprites/Units` for heroes deleted in
  the roster swap, referenced by nothing.

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

**Section 2e is the two talents that cost the hero who picks them**, Kael III and Mireth I, each a
conflict with its own kit that a number cannot mend. Velis III was the second until the content pass
gated No Second Breath; it reads inside the noise now, and its design question stays in 2e as a note. `CheckTalentsAreWorthPicking` pins both, so
the day either kit changes the harness says whether the talent became worth picking.

## Later

Not scheduled and not blocked on anything in **Next**, listed so it is not rediscovered.

- **Showing the ability text.** The card's number line from
  [`Docs/DESIGN-DAMAGE-DISPLAY.md`](Docs/DESIGN-DAMAGE-DISPLAY.md) is built: `AbilityText` prints a
  flat value with the authored percentage under it, and `CheckAbilityNumbersMatchTheSimulation`
  proves the figures. `AbilityDef.Description` and `PassiveDef.Description` are still rendered
  nowhere. They now hold player text, so putting them on the ability breakdown is a UI decision
  rather than a content one. Durations, thresholds, Energy amounts and status sizes are printed
  nowhere either, and the text no longer carries them.
- **Guilds phase two, systems 3 to 5.** Systems 1 and 2, the dungeon and the Guild Path, are built
  (see **Guilds, phase one**). Guild levels, the fodder exchange and (parked) guild wars remain,
  as sequenced in `DESIGN-GUILDS-PHASE-TWO.md`.
- **A marketplace.** Nothing built, nothing designed.
- **Chapters 3 to 6's art and backgrounds**, when they exist. The stages are built.
- **Texture compression.** The delivered art is effectively uncompressed, which is fine in a desktop
  player and will not be fine in an APK. Reported at 855 MB of 881 MB uncompressed; the tracked
  `Assets` folder measures 578 MB in this tree, so that figure is a player measurement rather than a
  repository one and wants re-taking against a real build before anybody sizes the work.
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

Every other `DESIGN-*.md` in this repository is a decision record from its date. The reasoning in
them is durable; the status claims, names and numbers are not. Never conclude from one that a system
is unbuilt.
