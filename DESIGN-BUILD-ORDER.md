# Emberfall - build order

**Current status only.** Everything this file used to carry about how the project got here now
lives in [`Docs/archive/BUILD-ORDER-HISTORY.md`](Docs/archive/BUILD-ORDER-HISTORY.md), verbatim and
unedited. The archive is the reasoning: why a mechanic is shaped the way it is, what a pass
measured, what it refused to build and why. This file is the state: what exists, where it lives,
what is moving, and what is owed.

Checked against the repository on 2026-09-22, against a live `Tools/build-check.ps1` run, a live
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
  Unity-facing code against the shim in `Tools/UnityShim`, and runs `Tools/DevHarness`. The two
  reds are the talent coverage note and the campaign power-band guard, both under **Known bugs**,
  and both are standing instruments reporting a real reading rather than a break. **A 3rd failure
  is a regression.** Diagnose it; do not add it to the list. Two caveats that are not regressions:
  the still-dormant guards listed under **Next** will fire as the lower-star kits adopt those
  features, and a turned-around tripwire goes red if the content it names stops using what it
  names.
- **Doc lint is clean.** `Tools/doc-lint.ps1` scans 37 tracked markdown files and reports 122
  baselined violations and **0 new**. The baseline is the known debt in
  `Tools/doc-lint-baseline.txt`; the zero is the number that has to stay zero.
- **`Tools/HeroValidator/validate_heroes.py Assets/Resources/Content/heroes.json`: 30 failures.**
  Every one is a star-shape finding on a hero still carrying a placeholder kit, and the 30 are
  exactly the twelve 2-stars and the eighteen 3-stars. The eight 1-stars pass, because a basic and
  nothing else is their whole contract. This is the scoreboard for the authoring work under
  **Next** and it closes as kits land. It was 86 before any kit was written, 70 before the 5-star
  pass, 56 when the 4-star pass opened, and 30 now that all 26 of them are authored.
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
  attributing that pass's move.
- **`hero-sweep-baseline.txt`: 280 lines.** Five seeds for each of the 56 authored heroes, which is
  all thirty 5-stars and all twenty-six 4-stars. The 38 placeholder heroes are not swept.
- **`talent-sweep-baseline.txt`: 450 lines.** All 90 talents at five seeds each, every one compared
  against the same hero at the same seed with nothing picked.

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
`TargetMode`, appended at the end of the enum so no existing ordinal moved. Everything else the
tier needed already had a shipped occupant. Where Pinoy's text could not be said at all, the batch
approximated it, marked the clause draft and recorded a row rather than growing the engine; see
`Docs/ENGINE-GAPS.md`.

### Content

Seven JSON files in `Assets/Resources/Content`. Balance and content changes go there, not into code.

- `heroes.json`: 94 heroes. 8 one-star, 12 two-star, 18 three-star, 26 four-star, 30 five-star.
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

**All thirty 5-stars and all twenty-six 4-stars have real kits.** That is 56 of 94 authored and 38
placeholders left, all of them at three stars and below, and their designs are written and waiting
in `import/Heroes.json`.

- A **5-star** is a basic, an active, the described passives (often authored as more than two, one
  per trigger a described passive answers), a stat-block passive and three talents. 90 talents in
  total.
- A **4-star** is a basic, an active and `passive1`, plus the same overflow `passives` list where
  one described clause answers a trigger of its own. **No talents and no stat-block passive**,
  because those are what a 5-star has and a 4-star does not.
- The other 38 carry a one-effect basic and a one-effect active.

The **star contract** is the authoritative kit rule and `validate_heroes.py` enforces it at import
time: 1 star is basicAttack only; 2 adds passive1; 3 adds activeSkill; 4 requires activeSkill and
passive1 with passive2 optional; 5 is the full kit including all three talents.

### The star band, and its floor

`validate_heroes.py` enforces the star contract's **shape**. `CheckStarBandByRole` in the harness
enforces its **size**, and without it a 4-star with a 300% active satisfies every other check in
the project while being a better hero than the 5-star beside it.

The band measures each authored hero's own output - damage dealt plus healing paid plus shielding
granted, credited by source - over the hero sweep's five seeds in the hero sweep's arena, and
asserts that **every graded 4-star reads between x0.45 and x0.85 of its own role's authored 5-star
median.** Talents are excluded because the sweep arena fields none, which is also why the ceiling
is 0.85 rather than 1.00: a 4-star reading nine tenths of a 5-star without that 5-star's three
talents is not a smaller hero.

**The floor is enforced only where the median is one.** A role with fewer than three authored
5-stars prints its ratio and is named ungraded, because a floor is a claim about where the middle
of a role sits and one or two readings have no middle. Today that is Warden (1), Stalker (2),
Binder (1) and Tinkerer (2). The ceiling keeps running for them. Four stars only: the tiers below
are unauthored and their floors are numbers nobody has decided.

Every graded 4-star is inside the band. The ungraded ones are printed and three of them sit under
where the floor would be if their role had one - Corin at x0.21, Demos at x0.28 and Iolan at x0.37,
all three against a single-hero Binder or Warden median. Each wants re-grading the day a second and
third 5-star is authored in that role, and none of them is a statement about the 4-star.

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

**31 of 94 heroes have drawn art**, and they are the thirty 5-stars plus Celen. The other 63 carry
`visual.spriteStatus: "pending"` and render as an aspect-tinted placeholder mannequin with their
name under its feet, which makes "not drawn yet" a thing the game can say separately from "this
hero's art is broken".

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
sweep. CI runs `build-check.ps1` on every push and pull request.

### Build targets

A Windows desktop player builds today through `EmberfallBuild.BuildWindows`, Mono rather than
IL2CPP because this editor install has no Windows IL2CPP module. Windowed 1600x900, resizable,
verified from the built exe rather than from the editor. **Android is still the target**; no layout
differs between them.

## In progress

**Nothing.** Every branch that was in flight has landed on `main`, and what it contains is now
observable in the files: the 4-star tier across four authoring batches, the rarity stat pass, the
star band's floor, the economy retune, the campaign curve re-derivation, the Hub's four layers,
Arden's caps and `Docs/ENGINE-GAPS.md`.

## Next

In order.

1. **The engine gaps pass.** `Docs/ENGINE-GAPS.md` is the input: 22 capabilities, 39 clauses, 20
   heroes, grouped by what is missing rather than by hero so a repeat is visible. It is a record
   rather than a work order, so the first job is deciding which groups are debt and which are
   games this one is not. Two are already named. **Group 9 is the one to close first and most of
   it is one line of code**: three of its five rows are the same exclusion in `OnEnemyDamagedAlly`,
   met by Branik, Halwen and Toren and answered three times by pasting the passive onto `OnDamaged`
   as well, so a flag on `PassiveDef` saying "and me" would delete three overflow passives from the
   shipped roster and stop the next batch writing a fourth. **Group 4 is the next best value**:
   four clauses across four heroes all asking for a per-source stat modifier, which is one status
   kind beside `Vulnerable` pointing the other way. **Groups 20 and 22 are marked as different in
   kind rather than as debt**, because an information layer and a movement system are games this
   one is not.
2. **Then the 3-star kits (18), the 2-star kits (12) and the 1-star kits (8).**
   `validate_heroes.py` at **30 failures** is the scoreboard, and the 3-star tier is 18 of those
   30.
3. **Then the UI design system.** The Hub now has one, `HubStyle`, and the other twenty screens do
   not: they read `UITheme` for colour and reference resolution and then write their own metrics at
   the call site. `HubStyle`'s own scope note says it is the Hub's chrome language rather than the
   game's, which is the honest description of a dialect that has not been generalised yet.
4. **Light the tripwires the new kits need.** Fifteen dormancy tripwires are still guarded as
   dormant because nothing reaches them: `LinkedUnit`, `AllLinkedUnits`, `OnLinkedUnitTargeted`,
   `OnFunnelConvertFired`, `linkAmountPower`, `linkFromCaster`, `ScaleByLastMoved`,
   `ShieldBrokenByCaster`, `EvasionUp`, `AccuracyDown`, `Interrupt`, `Splash`, `OnDodge`,
   `PerTarget` and `LedgerMitigates`. `HpTransfer` and `GuaranteedCrit` came off that list during
   the 4-star pass, lit by Rhys's Crossing Cut and Maera's Watch the Water, and `maxHpCap` went
   back onto it when Arden's caps were moved to `attackCap`. A tripwire fires the moment a kit
   adopts its feature; it is then **flipped, never deleted**, so it asserts the feature IS used and
   names the content using it, and goes red again the day that content stops.
5. **Re-run the campaign retune once the lower tiers carry real kits.** The curve is measured
   against a partly-stubbed bench and will shift when that bench fills; the detail is under **Open
   decisions**, because the shift is already visible and is one of the two harness reds.

## Blocked

- **The talent selection UI.** Not blocked technically, but not worth building against a roster
  whose lower-star kits are still placeholders.
- **Chapters 3 to 6**, on art from Pinoy. `DESIGN-WORLD`'s contested-region proposal for chapter 3
  is moot until they exist.
- **An APK**, on having an Android device to run it on.
- **iOS**, on a Mac and an Apple developer account. Neither exists.

## Open decisions

Six. None of them is a number to nudge, and none of them is the economy question that used to head
this list.

**The economy decision is resolved.** It read "a simulated player arrives at every stage between
1.55 and 3.2 times its `recommendedPower`, and either progression is too fast or the targets are
too low". It was both, and both halves were answered: the economy pass slowed income and the level
cost, and the campaign-curve pass re-derived the whole `recommendedPower` table under the shortened
chapter 1 ladder. The simulated player now arrives between x1.02 and x1.30. What is left is the six
stages that sit marginally outside their band, which is the first decision below.

1. **The power-band guard is red on six stages, all of them over and all of them marginal**, and
   the cause is the roster rather than the tuning. The readings are `1-9` at x1.07 against a x1.05
   ceiling, `1-12` at x1.06 against x1.05, `2-6` at x1.05 against x1.02, `2-7` and `2-8` a
   thousandth over x1.30, and `2-12` at x1.02 against x1.02. The curve was derived on a tree
   carrying five authored 4-stars; the tree now carries twenty-six. The line the balance pass
   fields is **not pinned and is not re-picked**: `PlayerProfile.NormaliseTeam` fills an empty slot
   with the strongest bench hero by Power and never displaces a hero already deployed, so the five
   are sticky from whenever each slot was filled. They currently read Kael, Kiva, Isara, Adric and
   Orelle, and **Adric is the only placeholder kit left among them**, with Orelle now authored.
   Re-deriving the table is cheap and would go green; doing it before the 3-star tier lands means
   doing it twice, because Adric is a 3-star. The decision is whether to re-derive now or wait.
2. **All four walls resolve in zero extra sittings, in simulation.** A stage is
   `waveCount` times 12 wins even for a player who never loses, so every stage costs a floor of
   `ceil(ladder / 40)` sittings whatever its difficulty, and the wall is what it costs on top of
   that. The table reads 0 extra sittings and 0 extra hours on `1-9`, `1-12`, `2-6` and `2-12`. A
   mid-chapter wall cannot bite under the two bands together: two sittings carry eight spare
   battles on a 72-battle stage, and an ordinary CEIL of 78% to 92% costs a player arriving at
   x1.00 to x1.05 about one. **The decision is to validate this with real playtests rather than
   with more simulation.** The simulated player never loses to anything the model does not price,
   and "a wall that costs nothing" is either a correct reading of a generous economy or a model
   that cannot see what a wall is; another harness pass cannot tell those apart.
3. **The CEIL band is blind on `1-1` to `1-8`.** The geared curve is what content is tuned against
   and its band assertion covers non-pinned stages only. The instrument's own geared level-1 line
   is worth about 7,000 to 7,400 power and chapter 1 now asks for 2,977 to 8,748, so eight of its
   twelve stages report FLOOR-PINNED: the pin cannot level down to the target, so those rows measure
   the five-body floor rather than `recommendedPower`. All eight read 100% at their ceiling, which
   is outside the ordinary band of 78% to 92% and is excluded from it rather than failing. Chapter 1
   is therefore tuned against nothing for two thirds of its length. Closing it would need chapter 1
   income to roughly quadruple, which is a different game, so the alternative is a second cheaper
   yardstick for the opening stages.
4. **Seraphine's Cross Reference works against her own Volume Closed.** Cross Reference, her talent
   II, appends a `LedgerIncrement` worth 30% of her Attack to the active. Volume Closed fires on
   `LedgerAtCap` and, when it does, strips the Mark and the Ledger it was reading. So the talent
   that fills the record faster also empties it sooner, and talent I says the same thing out loud:
   the harness proves it records 35% instead of 25% "so Volume Closed shuts the book a round
   earlier". Whether that is the intended tension of the kit or two clauses fighting is a design
   call, not a number.
5. **Altheon III is unreachable in the shipped roster.** Immaculate Record's Ledger banks 30% of one
   hit a round against 1.2x his Attack on an entry that lives two rounds, and Altheon at Speed 109
   applies it late, so capping it takes a **single blow of about 5,700 on the Ledger holder**. The
   biggest single damage effect on any 5-star is about 3,600 and the biggest aimed at
   `HighestAttackEnemy` about 3,230. Arden's percent-of-Max-HP terms used to cap it outright, and
   that reading was the defect rather than the talent. Swapping Vaelis in was tried and measured
   and the Ledger still capped zero times, so this is not a squad that needs picking better. The
   talent needs either a lower cap or a roster that can land the blow.
6. **Hale reads x0.36 of the Marksman median.** He is a 5-star at 30,071 against a role median of
   82,975, which puts him below every authored 4-star Marksman: Leon at 51,231, Maera at 50,276 and
   Tavia at 41,993. The star band only grades heroes below five stars, so nothing currently fails on
   this. Either his kit is under-tuned or the sweep arena cannot raise what he is built for, and the
   two want telling apart before the 3-star pass makes the comparison worse.
7. **The Hub's right rail sits on painted architecture.** The delivered `hub.png` puts the Proving's
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

The two harness reds first, then the rest.

1. **20 of 90 talents execute but are never observed doing anything** in the sweep arena. Each waits
   on something that arena does not raise: a kill, a mark, a host passive resolving first, a
   particular matchup, an `OnLedgerCapped`. Every one is named in the report with what it waits on,
   and 23 of them are proved separately in the dark-talent arenas, each built to raise its own gate.
   The check is red because the sweep refuses to call "nothing happened" a pass. It read 29 when the
   roster was authored, 27 after the dark-talent pass, and 20 after the kit-defect pass.
2. **6 of 24 stages leave the campaign power band.** Open decision 1 above carries the readings and
   the cause.

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

## Later

Not scheduled and not blocked on anything in **Next**, listed so it is not rediscovered.

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
