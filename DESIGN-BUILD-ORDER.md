# Emberfall - build order

**Current status only.** Everything this file used to carry about how the project got here now
lives in [`Docs/archive/BUILD-ORDER-HISTORY.md`](Docs/archive/BUILD-ORDER-HISTORY.md), verbatim and
unedited. The archive is the reasoning: why a mechanic is shaped the way it is, what a pass
measured, what it refused to build and why. This file is the state: what exists, where it lives,
what is moving, and what is owed.

Checked against the repository on 2026-09-22, after the 2-star and 1-star kits landed, the roster
reached 94 of 94, the five-star review closed four of the open decisions, the Dispel repair
closed the fifth, and every talent was measured for what it costs its own hero, against a live
`Tools/build-check.ps1` run, a live `validate_heroes.py` run and the files themselves, not against
this document's own prior claims.
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

- **`Tools/build-check.ps1`: 1 failure.** It lints the docs, compiles the core and the
  Unity-facing code against the shim in `Tools/UnityShim`, and runs `Tools/DevHarness`. The one red
  is the talent coverage note under **Known bugs**, and it is a standing instrument reporting a
  real reading rather than a break. The campaign power-band guard was the second and is green, and
  so is the geared curve's CEIL band. The balance line is Kael, Kiva, Isara, Adric and Orelle, and
  the pinned line the CEIL is read with is Kael, Aeron, Nereia, Seraphine and Isolde. The
  ENGINE-GAPS content pass changed Kael and Nereia, and Nereia's Change of Course alone took the
  CEIL out of band, so `Tools/solve-recommended-power.ps1` was re-run: 19 of 24 stages moved, 15 of
  them in the authored roster, chapter 2's `recommendedPower` came down by 3% to 5%, and the stage
  fingerprint baseline was regenerated under the levels it wrote. The solver needed
  `BattleClips.cs` added to its own compile to run at all, the same file `build-check.ps1` gives the
  harness.
  **A 2nd failure is a regression.** Diagnose it; do not add it to the list. One caveat that is not
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
  stage's opening battle fought by a fresh profile's starting three. This used to be a recollection
  carried in prose; the campaign-curve pass pinned it to a file, with one line per stage
  attributing that pass's move. The Dispel repair moved 22 of them on its own, and the re-solve it
  forced moved 57 in total. The ENGINE-GAPS content pass moved all 72: Kael's Unusable Cover and
  Kiva's Broken Links are in every opening battle, and with those two kits put back and the committed
  campaign, all 72 held. Its re-solve re-levels 15 of those openings on top.
- **`hero-sweep-baseline.txt`: 470 lines.** Five seeds for each of the 94 authored heroes, which is
  the whole roster. Nothing is excluded from it any more. The Dispel repair and the Altheon retune
  moved 15 of them: all five of Altheon's, all five of Kael's and all five of Seraphine's. Kael and
  Seraphine both author a Dispel that had been taking their own bookkeeping; Altheon's basic was
  retuned. The ENGINE-GAPS content pass moved 127: every line of each kit it changed and none of
  any other, `Docs/ENGINE-GAPS.md` pass 2 names them by group.
- **`talent-sweep-baseline.txt`: 450 lines.** All 90 talents at five seeds each, every one compared
  against the same hero at the same seed with nothing picked. Talents are a 5-star's alone, so the
  lower tiers add none and this file stays at 450. The Dispel repair and the Altheon retune moved
  50 of them: all three talents of Altheon, Kael and Seraphine, plus all five seeds of Nysera I,
  which appends a Dispel of its own. The ENGINE-GAPS content pass moved 238: all three talents of
  every 5-star whose base kit it changed, plus Isara III, Mireth II and III, and one seed of Kiva's.

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
Mark, Ledger or Link as a buff. It also repaired `TargetMode.LastAttacker` inside a broadcast about
another body, which moved Caldus and nothing else. **The content pass after it set
`chargeOnLanding` on 27 shipped passives; `buffsOnly` is still set on none**, because made the rule
it needs a campaign retune. That pass added three enum members and no fields:
`EffectCondition.StatusStacksBelow` and `EffectCondition.SubjectHadStatusWhenStruck`, which let
Oris's sequence replace his basic and Elyra's Patience ask about the body her blow found, and
`PassiveTrigger.OnEnemyShielded`, the enemy-side mirror of `OnAllyShielded`, for Hale. The harness
section ENGINE GAPS PASS 2 - FIDELITY proves all six and measures every shipped clause they reach.

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
31 exceptions. 30 are drafting notes whose sentence and kit still disagree, each a yes or no
question in `Docs/PINOY-LIST.md` section 2f. One, Nysera's False Target, has no text because its
damage half hits Nysera herself, which is a kit defect. `CheckAbilityTextIsPlayerFacing` enforces
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
| 3-star | x0.30 to x0.60 | the sixteen graded 4-stars read a median of x0.61 | x0.30 to x0.45, median x0.365 |
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

**A 5-star is not graded by the band, and the two that were furthest from their role's middle have
now been read.** Hale was at x0.36 of the Marksman median and is at x0.79; Altheon was and remains
the highest reading in the Breaker role. Neither move touched a median, so no hero below five stars
changed ratio. **Every graded hero on every rung is inside its band**: 16 four-stars, 13
three-stars, 8 two-stars and 6 one-stars. The ungraded ones are printed and four of them sit under where the floor would be
if their role had one - Corin at x0.20, Demos at x0.28 and Iolan at x0.37 against a single-hero
Binder median, and Fenn and Till at x0.09 against a two-hero Tinkerer one. Each wants re-grading
the day a second and third 5-star is authored in that role, and none of them is a statement about
the hero.

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
`talent-sweep-baseline.txt`. **The talent selection UI does not exist yet.**

**A talent a player picks should not make their hero worse, and `CheckTalentsAreWorthPicking` says
which ones do.** It reads every talent against the same hero with nothing picked, paired seed for
seed over 40 seeds, in the sweep arena wherever the talent changes a sweep fight and in its dark
room wherever it has one (`DarkTalentRooms`, the same 29 pairs the coverage note counts). The
quantity is the star band's: damage, healing and shielding, credited by source. A talent loses when
its mean sits more than two standard errors below zero. Five seeds could not answer this: the sweep
rolls damage variance, and a talent that shifts the dice stream moves a five-seed total by more than
most talents are worth. The reading is **36 gain, 46 sit within the noise or change nothing the
output total can see, and 8 lose**, and all 8 are pinned in `KnownCostlyTalents` with a reason. The
check fails both when a talent starts losing without being listed and when a listed one stops.

- **Two are traps**, both a conflict between the talent and its own kit, so neither could be mended
  by a number and both are in `Docs/PINOY-LIST.md` section 2e. Kael III pays 10% of his active for
  Rubble that What Breaks Forward consumes on the next basic, so its retention and cap never act.
  Mireth I trades a Missing Page that now lands every round for two landings per enemy per battle.
  Velis III was the second trap until the content pass gated No Second Breath, and now reads inside
  the noise.
- **Six are trades.** Kiva III keeps her alive to the end of every fight in its room, where she
  otherwise dies in every one. Nysera I strips a buff off the body targeting her, which in the sweep
  is a taunting tank's Provoke. Rheia I is fewer casts that each hit harder, as its sentence says.
  Rheia III, Mireth III and Marrow I each pay a price their own sentence names: Energy per strike,
  her own Focus, a smaller Promise spread over a third ally.
- **Altheon III is not on the list.** The dark-talent note had printed it as a loss, 103,377 against
  107,349, and that was one seed's crit draw. It fires in every seed and is worth +865 a battle over
  the check's 40 seeds and +1,025 over 150.

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
idle income is 85.7% of all gold earned and 52.9% of all power.

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
each chapter, jumps 25% into `1-9`, 27% into `1-12`, 36% into `2-6` and 36% into `2-12`, and takes a
9.6% to 10.6% relief after a wall that never falls below what the previous ordinary stage demanded.

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

### The Deep Recital, The Proving, events, shop

`RecitalGenerator` builds an endless floor ladder with a Guardian every tenth floor and an unbroken
Aspect cycle. Coda is the Recital's own currency and accrues on every clear. **There is no Coda sink
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

`Tools/build-check.ps1` is the gate. `Tools/doc-lint.ps1` enforces the citation half of CLAUDE.md
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

**`content/ability-text`** carries the ability text above: every basic, active, passive and talent
rewritten or written so it says what happens without quoting a number, the house style in
`Docs/ABILITY-TEXT-STYLE.md`, `CheckAbilityTextIsPlayerFacing`, and `Docs/PINOY-LIST.md` section
2f. It changes descriptions only, so no fingerprint and no baseline moved. Every sentence that
had text before the pass is listed before and after in `Docs/archive/ABILITY-TEXT-REWRITES.md`.

**`balance/engine-gaps-content`** carries the content pass ENGINE-GAPS pass 2 wrote up: C1 to C12
landed or corrected, C9's Dispel measured and not written, `chargeOnLanding` on 27 passives with
Aster raised 3.3% to hold the 3-star band, G4 and G5 built so Elyra's Slow Growth and Oris's No
Fixed Refrain say their sentences, `OnEnemyShielded` for Hale, fixes for Mireth's Absence, Rheia's
Revision and Marrow's own Promise, verdicts on all nine suspected entries, the campaign re-solved,
and all three baselines regenerated with each move attributed. `Docs/ENGINE-GAPS.md` pass 2 is the
record, and every number above that changed describes the tree with this branch in it.

**`balance/talent-traps`** carries `CheckTalentsAreWorthPicking` and its pinned list of the five
talents that cost their hero output, described under **Talents** above, with the two traps sent to
`Docs/PINOY-LIST.md` section 2e. It changes no content: neither trap could be mended by a number,
so no hero, no baseline and nothing the balance line fields moved. Everything else that was in
flight has landed on `main` and is observable in the files, the Dispel repair and the five-star
review included.

## Next

In order.

1. **What the fidelity pass left.** Its content pass is on the branch named under **In progress**,
   and three things remain after it, none of them wiring. **C13**: `buffsOnly` on the eight
   listeners and four buff moves whose text names a buff, which needs a campaign retune with it and
   a fresh reading, because the 63%-to-22% CEIL figure predates the re-solve. **G1, G2 and G3**, the
   three proposed primitives not built, which would close groups 25, 30 and 23. And the findings
   the content pass left in `Docs/ENGINE-GAPS.md`: Hollowed's convert path never charging
   `ledgerBankLimit`, a Dispel that takes the buff that raised it (what Mireth's Missing Page needs),
   an empty auto-release recorded as zero (Nereia's two Reserve talents), and a counter refused by
   the per-target budget still spending the Resolve its effect carries. The file holds 25
   capabilities and 36 clauses. **Groups 20 and 22 stay marked as different in kind rather than as
   debt**, because an information layer and a movement system are games this one is not.
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

- **The talent selection UI.** No longer blocked on content: it was held back because the roster's
  lower-star kits were placeholders, and they are not any more. It is the next thing that can be
  built without waiting on anybody.
- **Chapters 3 to 6**, on art from Pinoy. `DESIGN-WORLD`'s contested-region proposal for chapter 3
  is moot until they exist.
- **An APK**, on having an Android device to run it on.
- **iOS**, on a Mac and an Apple developer account. Neither exists.

## Open decisions

Three. None of them is a number to nudge, and neither the economy question nor the power-band one
that used to head this list is still here. The five-star review closed four more - the three
5-star kit questions and the low-tier Energy one - and the Dispel repair closed the Dispel one,
which was open decision 3, all recorded below. The numbers on the three that remain were reassigned
when it closed, so the Hub rail is number 3 now.

**The economy decision is resolved.** It read "a simulated player arrives at every stage between
1.55 and 3.2 times its `recommendedPower`, and either progression is too fast or the targets are
too low". It was both, and both halves were answered: the economy pass slowed income and the level
cost, and the campaign-curve pass re-derived the whole `recommendedPower` table under the shortened
chapter 1 ladder.

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
2. **The CEIL band is blind on `1-1` to `1-8`.** The geared curve is what content is tuned against
   and its band assertion covers non-pinned stages only. The instrument's own geared level-1 line
   is worth about 7,000 to 7,400 power and chapter 1 asks for 2,977 to 8,753, so ten of its
   twelve stages report FLOOR-PINNED: the pin cannot level down to the target, so those rows measure
   the five-body floor rather than `recommendedPower`. Seven of the ten read 100% at their ceiling
   and the other three read 99.5%, 94.5% and 77.5%, all of them measurements of the instrument
   rather than of the stage, and all of them excluded from the ordinary band of 78% to 92% rather
   than failing it. Only `1-9` and `1-12` are measured against what they ask for. Chapter 1 is
   therefore tuned against nothing for five sixths of its length. Closing it would need chapter 1
   income to roughly quadruple, which is a different game, so the alternative is a second cheaper
   yardstick for the opening stages.
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

## Known bugs

The one harness red first, then the rest.

1. **21 of 90 talents execute but are never observed doing anything** in the sweep arena. Each waits
   on something that arena does not raise: a kill, a mark, a host passive resolving first, a
   particular matchup, a buff worth dispelling. Every one is named in the report with what it waits
   on. **18 of them are proved separately in the dark-talent arenas**, each built to raise its own gate,
   and the note that says so counts its own overlap now rather than carrying a number: it had read
   "23 of the 27" for two passes after the list stopped being 27 long. The check is red because the
   sweep refuses to call "nothing happened" a pass. It read 29 when the roster was authored, 27
   after the dark-talent pass, 20 after the kit-defect pass, and 20 again after the five-star
   review with different membership.
   **The ENGINE-GAPS content pass took it to 21, three joining and one leaving, and each move is the
   content saying its sentence.** Brenna III now rides a Survival save and Aminah III a Priority
   ally, and the sweep raises neither; Caldus III had only ever been seen through the empty firing
   that `chargeOnLanding` now takes back out of the record; and Mireth II left, because her Pages
   fill now. The three new ones are the three the dark-talent arenas do not reach, and the note
   under the check names them.
   **The Dispel repair had taken it to 19, and two talents left as one joined.** Seraphine I and
   Seraphine III both left: her Contrary Evidence had been dispelling the bookmark her own Volume
   Closed writes, so the book could not close on the bodies those two talents are about, and with
   the bookmark surviving both are observable in the sweep again. **Kael II joined**, and it is the
   repair working rather than a defect: his Take Down the Wall was stripping the Fault Line his own
   basic had just laid, which is the loss his talent answers, and the sweep's enemy line carries one
   AttackUp that lasts a single round and has expired by the time he casts - so his Dispel now finds
   nothing to take there. It is proved in `s_triple`, against a body holding four nine-round buffs,
   at 26 Fault Lines against 20, one per strip. **Altheon III stays off the list** and is a live
   check now rather than a note. That note used to report the talent losing on its own trade; the
   loss was one seed, and `CheckTalentsAreWorthPicking` reads it as a small gain.

Beyond the harness:

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
