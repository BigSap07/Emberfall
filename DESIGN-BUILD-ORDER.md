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
placeholder rates in `balance.json` and the campaign was re-solved against them - against a live `Tools/build-check.ps1` run, a live
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

- **`Tools/build-check.ps1`: 2 failures.** It lints the docs, compiles the core and the
  Unity-facing code against the shim in `Tools/UnityShim`, and runs `Tools/DevHarness`. Both reds
  are under **Known bugs**: the talent coverage note, a standing instrument reporting a real reading
  rather than a break, and Hale reading under the 5-star floor in the sweep arena since defect
  pass 3 moved his Tracking Habit to round end, which is a balance call nobody has made yet. The campaign power-band guard and the geared curve's CEIL band are green. The
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
  went down a level by hand, and it reads 57.5%. That one level moved `2-12` from x0.98 to x0.95,
  just under its band, through the armor the simulated player happens to pick up on the way
  (4,807 to 3,772), so the solver was run again on top of the hand move. It changed only chapter 2,
  and `2-12` reads x0.99. The solver and `build-check.ps1` compile the harness from the same file
  list now, `SummonTiming.cs`, `GuildDungeonChecks.cs` and `CloudChecks.cs` included.
  **A 3rd failure is a regression.** Diagnose it; do not add it to the list. One caveat that is not
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
  no longer answered by the sweep's Taunt holder).
- **`talent-sweep-baseline.txt`: 450 lines.** All 90 talents at five seeds each, every one compared
  against the same hero at the same seed with nothing picked. **ENGINE-GAPS pass 3 moved 134**: its
  clauses and renumbers 59 (Aeron III, all three of Aminah's and Marrow's, Arden I, Cyrra II, Nereia
  II, and Velis I, II and III); `buffsOnly` 75 (all three talents of Hale, Kiva, Mireth, Seraphine
  and Sylven); the Taunt-anchor repair moved Marrow's 15 again. `Docs/ENGINE-GAPS.md` pass 3 has
  every group. **Defect pass 3 moved 51**: Darian's and Hale's 15 each, for the reasons their sweep
  lines moved; Nysera II's 5, False Target striking the enemy her Link turned the blow onto; and
  the named-body repair, Seraphine's 9, Mireth III's 4 and Aeron I's 3. Defect pass 3's lines are
  attributed by which change touches each kit, not by regenerating after each step; the
  named-body ones are the only kits in either file that change touched alone.

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

### Content

Seven JSON files in `Assets/Resources/Content`. Balance and content changes go there, not into code.

- `heroes.json`: 94 heroes, **all 94 authored**. 8 one-star, 12 two-star, 18 three-star,
  26 four-star, 30 five-star.
- `enemies.json`: 12 enemy templates.
- `campaign.json`: 2 chapters, 12 stages each. `waveCount` is authored per stage now rather than 12
  everywhere, so a stage's ladder is `waveCount` times 12 battles: 36 on `1-1` to `1-4`, 72 on
  `1-5` to `1-12`, and 144 across the whole of chapter 2.
- `relics.json`: 10 relics across two grades, Legendary and Mythic.
- `armor.json`: drop chance, primary and sub rolls, tempers, attunements, slot names.
- `events.json`: 2 events. Its two windows are real calendar dates sitting in the file, not
  sentinels, and the file's own comment still calls them first-pass and untuned. Nobody should
  treat them as an announcement schedule without deciding to.
- `balance.json`: every tuning constant the engine and the economy read.

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
Nearest the edges now: Kiva and Hale at x0.79, and Edrin x1.53, Darian x1.48 and Cyrra x1.37 above.
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

The talent sweep runs all 90 talents at five seeds each and pins 450 fingerprints in
`talent-sweep-baseline.txt`. **The talent selection UI is built**: `TalentsScreen` shows each talent's
sentence, what it changes in numbers through `TalentMath`, and what it did to this hero measured by
`TalentTrial`, with `TalentAudit` naming the audited traps and trades. `TalentAudit.Costly` is a copy
of the harness's list, and `CheckTalentAuditMatchesHarness` holds the two equal.

**A talent a player picks should not make their hero worse, and `CheckTalentsAreWorthPicking` says
which ones do.** It reads every talent against the same hero with nothing picked, paired seed for
seed over 40 seeds, in the sweep arena wherever the talent changes a sweep fight and in its dark
room wherever it has one (`DarkTalentRooms`, the same 29 pairs the coverage note counts). The
quantity is the star band's: damage, healing and shielding, credited by source. A talent loses when
its mean sits more than two standard errors below zero. Five seeds could not answer this: the sweep
rolls damage variance, and a talent that shifts the dice stream moves a five-seed total by more than
most talents are worth. The reading is **37 gain, 47 sit within the noise or change nothing the
output total can see, and 6 lose**, and all 6 are pinned in `KnownCostlyTalents` with a reason. The
check fails both when a talent starts losing without being listed and when a listed one stops.

- **Two are traps**, both a conflict between the talent and its own kit, so neither could be mended
  by a number and both are in `Docs/PINOY-LIST.md` section 2e. Kael III pays 10% of his active for
  Rubble that What Breaks Forward consumes on the next basic, so its retention and cap never act.
  Mireth I trades a Missing Page that lands every round for two landings per enemy per battle; it is
  read in `s_buffer` since `buffsOnly`, because the sweep no longer raises The Missing Page.
- **Four are trades.** Nysera I strips a buff off the body targeting her, which in the sweep is a
  taunting tank's Provoke. Rheia I is fewer casts that each hit harder, as its sentence says. Rheia
  III and Marrow I each pay a price their own sentence names: Energy per strike, a smaller Promise
  spread over a third ally.
- **Kiva III and Mireth III left the list in pass 3**: once both heroes' listeners heard real buffs
  only, Kiva III reads inside the noise and Mireth III gains in its own room.

### Campaign and progression

`CampaignWaveGenerator` resolves a stage into its ladder, which is `waveCount` times 12 battles.
`GameSession` owns rank, levels, purse and the progression API. `Debrief` reads a loss back into a
reason a player can act on. Account rank raises `HeroLevelCap`; gold buys levels.

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
  a player got.
- **The hero level cost loses its quadratic term and gains a constant**, from `55+26L+3.2L^2` to
  `600+470L`, because power is linear in level and a quadratic cost makes the gold needed to hold a
  ratio grow as the cube of it.
- **`duplicateGold` drops to 150**, because a duplicate's refund was an eighth of all gold and the
  only large channel whose size nobody chose.

`ReportPowerSources` splits the simulated player's power into roster, levels and armor at `2-1` and
at `2-12` and counts every channel a coin arrives through, because "where does the power come from"
had only ever been answered by reading the content files, and reading them got it wrong. At `2-12`
idle income is 86% of all gold earned and 56.3% of all power.

`ReportPaceTargets` grades the shape the campaign is meant to have against the one it has, and is
reported rather than failing because both of its targets wait on fusion. See open decision 4.

### The campaign power curve, and the walls

`CheckCampaignPowerCurve` reads the power a simulated player brings to their **first attempt** of
each stage - not the exit reading the balance table shows, which by construction can never show a
wall - and fails when a stage leaves its band.

- The **first four stages** have no band.
- An **ordinary** stage is x1.10 to x1.30.
- A **wall** is x1.00 to x1.05 in chapter 1 and x0.95 to x1.02 from chapter 2 on, and the per-chapter
  split was measured off how many extra sittings a wall at each ratio actually costs.
- A wall is a chapter finale or one authored mid-chapter spike. The two spikes are `1-9` and `2-6`.

`recommendedPower` is re-derived for all 24 stages from the simulated player's entry power against
the middle of each stage's own band, with `floorLevel`, `ceilingLevel` and the authored enemy
levels following so the CEIL band still reads in range at the new number. The curve rises inside
each chapter, jumps 25% into `1-9`, 22% into `1-12`, 31% into `2-6` and 28% into `2-12`, and takes a
6.4% to 9.9% relief after a wall that never falls below what the previous ordinary stage demanded.

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
`SaveSerializer.CurrentVersion` 14, and a save from a newer build is refused rather than read.
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

### The Deep Recital, The Proving, events, shop

`RecitalGenerator` builds an endless floor ladder with a Guardian every tenth floor and an unbroken
Aspect cycle. **It never draws the prologue's scripted loss**: `RecitalGenerator.ScriptedLossEnemies`
reads the prologue's Boss step and skips whatever it fields that no campaign stage does, which is The
Unmade. Before that every Umbra floor fielded it, twice on floors 5 and 11, and
`CheckRecitalDrawsNoScriptedLoss` now climbs 120 floors to say none does. Coda is the Recital's own currency and accrues on every clear. **There is no Coda sink
yet.** `ProvingGenerator` builds opponent lines. The events engine is content-driven through
`events.json`. Shop screens are wired.

### UI, and the Hub's four layers

uGUI, built entirely in code, scene deliberately near-empty. `ScreenRouter` moves between the
twenty-one screens under `Assets/Scripts/UI/Screens`. `UITheme` owns the reference resolution and
the palette; `UIBuilder`, `Widgets`, `ScreenChrome` and `CurrencyStrip` are the shared furniture.
Landscape, touch-first, everything hung off `Screen.safeArea`.

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

### Build targets

A Windows desktop player builds today through `EmberfallBuild.BuildWindows`, Mono rather than
IL2CPP because this editor install has no Windows IL2CPP module. Windowed 1600x900, resizable,
verified from the built exe rather than from the editor. **Android is still the target**; no layout
differs between them.

## In progress

**`engine/defect-pass-3`** carries four combat defects and the sweep for their shape: Nysera's False
Target striking Nysera, Corin's Stun that could never land, Hale's Tracking Habit moving Quarry at
the start of his turn rather than at round end, and the effects named by a broadcast or by the
engine's own record that a Taunt holder could intercept. The engine, content, harness and
documentation changes are described above as the files on that branch show them, and the three
pinned baselines were regenerated as its last commit, from a fresh rebase on `main`. The campaign
re-solve against real summon rates has landed on `main` and is described under **The instruments**.

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

- **Chapters 3 to 6**, on art from Pinoy. `DESIGN-WORLD`'s contested-region proposal for chapter 3
  is moot until they exist.
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
   that cannot see what a wall is; another harness pass cannot tell those apart.
2. **The CEIL band is blind on most of chapter 1.** The geared curve is what content is tuned
   against and its band assertion covers non-pinned stages only. The instrument's own geared level-1
   line is worth about 6,900 to 7,400 power and chapter 1 asks for 2,977 to 8,623, so ten of its
   twelve stages report FLOOR-PINNED: the pin cannot level down to the target, so those rows measure
   the five-body floor rather than `recommendedPower`. Six of the ten read 100% at their ceiling and
   the other four read 98.5%, 98%, 96.5% and 90.5%, all excluded from the ordinary band of 78% to 92%
   rather than failing it. Only `1-9` and `1-12` are measured against what they ask for; `1-11`
   joined the pinned rows when the re-solve against real summon rates lowered it to 7,062. **The blind
   spot bit once in pass 3**: the solver does not measure a pinned stage, and its re-solve raised
   `1-10`'s ceiling until that stage read 55%, the relief stage after the `1-9` wall reading as the
   hardest finish in the chapter; the three units under the top of its line were taken two levels
   down by hand, and it read 83%. After the re-solve against real summon rates it reads 98%. Closing the gap
   would need chapter 1 income to roughly quadruple, which is a different game, so the alternative
   is a second cheaper yardstick for the opening stages, or a solver that measures a pinned stage
   whenever the instrument sits within a few percent of what it asks.
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
4. **The campaign is a gold-into-levels treadmill, and the targets that say how far off it is are
   not settled.** At the first attempt of `2-12` the POWER SOURCES block reads levels 64.4% of team
   power, roster 20.1% and armor 15.5%, and 86% of the gold that bought those levels is idle income.
   The intended shape is roster-led with levels as a top-up, the way an ascension-gated idle RPG
   works, and fusion is meant to deliver it. `ReportPaceTargets` grades two targets on every run,
   **reported and never failing**, because they will read off until fusion lands:
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

The two harness reds first, then the rest.

1. **24 of 90 talents execute but are never observed doing anything** in the sweep arena. Each waits
   on something that arena does not raise: a kill, a mark, a host passive resolving first, a
   particular matchup, a buff worth dispelling. Every one is named in the report with what it waits
   on. **20 of them are proved separately in the dark-talent arenas**, each built to raise its own
   gate, and the note that says so counts its own overlap. The check is red because the sweep
   refuses to call "nothing happened" a pass. It read 29 when the roster was authored, 27 after the
   dark-talent pass, 20 after the kit-defect pass, 19 after the Dispel repair and 21 after the
   ENGINE-GAPS content pass.
   **ENGINE-GAPS pass 3 took it to 23: two joined and none left, and both are `buffsOnly` doing
   what it says.** Mireth II appends to The Missing Page and Seraphine III rides Volume Closed, and
   both hosts listen for an enemy gaining a buff; in the sweep the only "buff" either ever heard
   was their own Mark, and its enemies grant themselves none. Both are proved in their dark rooms.
   The three the dark rooms still do not reach are Aminah III, Brenna III and Caldus III.
   **Defect pass 3 took it to 24: Hale II joined**, because Cornered rides Closing Radius's
   missing-HP blow and, with Quarry moving at round end, Closing Radius found a Quarry on one of
   Hale's five sweep casts rather than three. It is proved in its dark room, so 21 of the 24 are.
2. **Hale reads x0.61 of the Marksman 5-star median in the sweep arena, under the floor of x0.78.**
   Defect pass 3 moved his Tracking Habit to round end, as Pinoy wrote it, after measuring that at
   the start of his turn 37% of its picks landed on an enemy that had dealt no damage yet and so
   went to the front slot on the tie. His sweep output rests on five Closing Radius casts, and a
   Quarry handed over at round end with a round left expires on a faster carrier's turn before two
   of them. Over 1,440 campaign battles built around him his damage per battle is unchanged (7,869
   against 7,838) and he wins slightly more. **Not retuned**: whether to raise his numbers, keep
   him, or read the band over more than five casts is a balance call.

Beyond the harness:

- **Counterattacks still go to a Taunt holder.** Defect pass 3 swept every effect named by a
  broadcast for the wrong-body shape and repaired the named-body modes; `LastAttacker` and
  `LastTarget` it measured and left, because the engine documents the anchor outranking them as a
  rule. Caldus's No Uncertain Ground still strikes the Taunt holder when the enemy that hit an ally
  is not it (126 times in 240 campaign battles he fought in, 72 in his 20 sweep-arena runs), and
  his Defined Duty and Jory's Worn Hinges do the same. Whether a counterattack answers to Taunt is
  a rules decision rather than a defect.
- **Marn's Relief After Loss healed an enemy once in 8,832 campaign battles**, because the
  Coverguard it answers was on an enemy when it was spent. How the Guard got there was not traced.

- **The Campaign chip row does not fit more than five chapters.** `CampaignScreen.BuildChipRow`
  sizes the row at 1600 units and `RefreshChips` gives every chip 300 units inside a 16-unit-spaced
  stack, so five chapters fit exactly and six overflow. Harmless with two chapters shipped. Fixing
  it needs a scrolling row or narrower chips, which is a design call rather than a number to nudge.
- **`CurrencyStrip` has no Coda cell.** Coda is shown on the Recital screen only. A seventh cell
  exceeds the strip's width and needs layout work.
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
- **Guilds phase two.** A guild shop, currency, raids, rewards, guild levels and leaderboards all
  price against the economy, and the economy's shape only settled with the curve re-derivation.
- **A marketplace.** Nothing built, nothing designed.
- **Chapters 3 to 6**, when the art exists.
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
