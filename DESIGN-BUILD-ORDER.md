# Emberfall - build order

**Current status only.** Everything this file used to carry about how the project got here now
lives in [`Docs/archive/BUILD-ORDER-HISTORY.md`](Docs/archive/BUILD-ORDER-HISTORY.md), verbatim and
unedited. The archive is the reasoning: why a mechanic is shaped the way it is, what a pass
measured, what it refused to build and why. This file is the state: what exists, where it lives,
what is moving, and what is owed.

Checked against the repository on 2026-09-21, against a live `Tools/build-check.ps1` run and the
files themselves, not against this document's own prior claims. Where this file and the archive
disagree, this file is right about status and the archive is right about reasoning.

## How to read this file

- A system entry says what is **built and observable in the tree**. If a system is not listed here,
  it does not exist.
- **In progress** names live branches. A branch name is only ever cited there, only while it is
  unmerged, and it goes away the moment the work lands and the files can speak for themselves.
- Numbers here are measurements from a real run, not estimates. The two that matter most are the
  harness failure count and the validator failure count, and both are named below.

## The instruments

Two runs decide whether the tree is healthy, and they measure different things.

- **`Tools/build-check.ps1`: 1 failure.** It lints the docs, compiles the core and the Unity-facing
  code against the shim in `Tools/UnityShim`, and runs `Tools/DevHarness`. The one red is the
  talent coverage note under **Known bugs**, and it is a standing instrument rather than a break.
  **A 2nd failure is a regression.** Diagnose it; do not add it to the list. Two caveats that are
  not regressions: the seventeen still-dormant guards listed under **Next** will fire as the
  lower-star kits adopt those features, and a turned-around tripwire goes red if the content it
  names stops using what it names.
- **`Tools/HeroValidator/validate_heroes.py Assets/Resources/Content/heroes.json`: 56 failures.**
  Every one is a star-shape finding on a hero still carrying a placeholder kit. This is the
  scoreboard for the authoring work under **Next** and it closes as kits land. It was 86 before any
  kit was written, 70 before the 5-star pass, 57 after it, and 56 once Cyrra's passive slots were
  straightened out.
- Two further instruments exist and are not run by `build-check.ps1`, because both need the live
  Supabase project: `Tools/supabase-rules-test.py` (accounts, saves, friends) and
  `Tools/supabase-guild-rules-test.py` (guilds). Both last reported clean.
- **A screen is not verified until it has been photographed.** Nothing in the harness can see a
  layout. `PlayModeScreenshot` and `ShotOnDemand` are the instruments for that, and a commit that
  moves a screen is not done without one.

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

### Content

Seven JSON files in `Assets/Resources/Content`. Balance and content changes go there, not into code.

- `heroes.json`: 94 heroes. 8 one-star, 12 two-star, 18 three-star, 26 four-star, 30 five-star.
- `enemies.json`: 12 enemy templates.
- `campaign.json`: 2 chapters, 12 stages each, 144 battles per stage through the wave generator.
- `relics.json`: 10 relics across two grades, Legendary and Mythic.
- `armor.json`: drop chance, primary and sub rolls, tempers, attunements, slot names.
- `events.json`: 2 events. Its two windows are real calendar dates sitting in the file, not
  sentinels, and the file's own comment still calls them first-pass and untuned. Nobody should
  treat them as an announcement schedule without deciding to.
- `balance.json`: every tuning constant the engine and the economy read.

### Heroes and kits

**All thirty 5-stars have real kits**: a basic, an active, the described passives (often authored as
more than two, one per trigger a described passive answers) and three talents each, 90 talents in
total. The other 64 heroes carry a one-effect basic and a one-effect active, and their designs are
written and waiting in `import/Heroes.json`.

The **star contract** is the authoritative kit rule and `validate_heroes.py` enforces it at import
time: 1 star is basicAttack only; 2 adds passive1; 3 adds activeSkill; 4 requires activeSkill and
passive1 with passive2 optional; 5 is the full kit including all three talents.

### Talents

A talent is an additional passive and nothing else. `HeroDef.Talents` holds up to three, the
selection is per-owned-hero save state on `HeroInstance.SelectedTalent`, and
`GameSession.SelectTalent` is the whole selection rule: free, no cooldown, refuses a slot the hero
does not author. It resolves once in `BuildUnits` and is appended as one more passive. A talent
selection travels into a Proving ghost and a friend duel, so a snapshot line fights with the talents
it names.

The talent sweep runs all 90 talents at five seeds each and pins 450 fingerprints in
`talent-sweep-baseline.txt`, each compared against the same hero at the same seed with nothing
picked. **The talent selection UI does not exist yet.**

### Campaign and progression

`CampaignWaveGenerator` resolves a stage into its 144-battle ladder. `GameSession` owns rank,
levels, purse and the progression API. `Debrief` reads a loss back into a reason a player can act
on. Account rank raises `HeroLevelCap`; gold buys levels.

### Gear

Armor rolls through `ArmorRoller` with primaries, subs, tempers and attunements, shown on
`InventoryScreen` and `ForgeScreen`. Relics are a two-tier model: a Legendary changes a rule in the
effect pipeline, a Mythic changes a rule and carries a stat block on top of it. Relic progression
ships on top of that, with `RelicShopScreen` as its storefront.

### Summoning, idle income, save

Summoning with a pity rule that guarantees distinct unowned heroes. Idle income accrues against a
real clock and is capped. `SaveSerializer` writes the whole document; the format is at **save
version 14**. Every action that changes something a player owns ends in a `GameApp.SaveNow`, which
is why hooking the save is how the published team line stays current without any screen having to
remember to publish.

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

### UI

uGUI, built entirely in code, scene deliberately near-empty. `ScreenRouter` moves between the twenty
screens under `Assets/Scripts/UI/Screens`. `UITheme` owns the reference resolution and the palette;
`UIBuilder`, `Widgets`, `ScreenChrome` and `CurrencyStrip` are the shared furniture. Landscape,
touch-first, everything hung off `Screen.safeArea`.

### Battle presentation and art

`BattleStage` builds the world, `UnitActor` and `UnitRig` build a body, `SpriteAnimator` plays a
per-unit clip set (idle, basic, basic2, skill, hit reaction, death, controlled) by writing the
active frame's UV rect into a billboard quad. Per-clip `impactFraction` syncs the lunge and the
Damage event to the strike; per-clip `footFraction` stops a clip floating or sinking.
`BattleAssets` loads unit sprites and backdrops by id, and logs an error naming the hero and the
path it looked in when a unit declares art it does not have.

**31 of 94 heroes have drawn art.** The other 63 carry `visual.spriteStatus: "pending"` and render
as an aspect-tinted placeholder mannequin with their name under its feet, which makes "not drawn
yet" a thing the game can say separately from "this hero's art is broken".

### Dev Arena

`Assets/Dev/DevArena.unity`, with its scripts in `Assets/Scripts/Dev`, opened from the editor's
Emberfall menu. It presents a live `BattleSession` through the real battle presentation, hand-driven
or watching itself play, switchable mid-fight. A hand-driven action is a whole turn, spends that
unit's natural turn, and refuses out loud where Stun or Silence would otherwise have silently
substituted a basic.

**It cannot reach a release build, and that is checked rather than intended.** Every file under
`Assets/Scripts/Dev` is wrapped in a `UNITY_EDITOR || DEVELOPMENT_BUILD` guard, so a release player
does not contain the classes, and `build-check.ps1` asserts all of it from the files. The scene
reaches a development player because the Windows build path names it outright, gated on the same
Development condition.

### Tooling

`Tools/build-check.ps1` is the gate. `Tools/doc-lint.ps1` enforces the citation half of CLAUDE.md
across every tracked markdown file, with its known debt recorded in `Tools/doc-lint-baseline.txt`.
`Tools/DevHarness` holds every engine check, the 72 stage fingerprints, the 150-line hero sweep and
the 450-line talent sweep. CI runs `build-check.ps1` on every push and pull request.

### Build targets

A Windows desktop player builds today, Mono rather than IL2CPP because this editor install has no
Windows IL2CPP module. Windowed 1600x900, resizable, verified from the built exe rather than from
the editor. **Android is still the target**; no layout differs between them.

## In progress

**Nothing.** Both of the branches that were in flight have landed on `main`, and what they contain
is now observable in the files:

- The **chapter 2 repass** returned both balance instruments to green, which is what took the
  harness from 3 failures to 1. It moved `ceilingLevel` on nine chapter 2 stages and an authored
  enemy level only where a level was too coarse, left `2-1` and `2-9` alone because they were
  already inside the band, and touched no chapter 1 stage at all. `2-12`'s opener rose with its
  floor so the finale opens like one.
- **Seraphine's Volume Closed** was repaired in the same work. It laid BuffSuppression on a capped
  body in its first effect and the `MarkAlt` its own second half waits on in its second, and
  `ApplyStatus` was dropping a non-harmful status onto a body carrying BuffSuppression, so the
  passive suppressed its own flag. The engine rule was the one that was wrong: the gate's own
  comment says it prevents a BENEFICIAL status, the code said "not harmful", and those are
  different sets.
- The **Dev Arena build hook, the 16:10 hub label clip and a Windows rebuild** landed together. The
  hook had never put the arena scene in any player, because it amended the editor build settings
  from a pre-process hook that fires after the pipeline has already resolved the scene list.

## Next

In order. Item 1 is the whole of the near-term work.

1. **Author the 4-star kits: 26 of them, in batches.** Designs are written and waiting in
   `import/Heroes.json` with full skill descriptions. `validate_heroes.py` at **56 failures** is the
   scoreboard: every kit that lands takes it down.
2. **Light the tripwires the new kits need.** Seventeen dormancy tripwires are still guarded as
   dormant because nothing reaches them: `LinkedUnit`, `AllLinkedUnits`, `OnLinkedUnitTargeted`,
   `OnFunnelConvertFired`, `linkAmountPower`, `linkFromCaster`, `ScaleByLastMoved`,
   `ShieldBrokenByCaster`, `EvasionUp`, `AccuracyDown`, `GuaranteedCrit`, `Interrupt`, `Splash`,
   `OnDodge`, `PerTarget`, `LedgerMitigates`, `HpTransfer`. A tripwire fires the moment a kit adopts
   its feature; it is then **flipped, never deleted**, so it asserts the feature IS used and names
   the content using it, and goes red again the day that content stops.
3. **Fix Kiva III, Unclaimed Victory, in the same pass.** It pays 0 Shield today. All three of its
   clauses gate on a Resolve stack she no longer has: Abandoned Support banks at most one Broken
   Link a round and Severance Paid spends one on every basic against a Forsaken body, the same basic
   whose own mark banked it, so she opens every turn at zero and a kill has nothing to consume. The
   fix is a **base 12% shield on a Forsaken kill, with the two gated clauses kept on top of it**,
   and the text updated to match. **Pinoy to confirm the wording.**
4. **Then the 3-star kits (18), the 2-star kits (12) and the 1-star kits (8).**
5. **Re-run the campaign retune once Adric and Maera carry real kits.** Two of the pinned
   yardstick's five bodies, `adric_the_blank_map` (3-star) and `maera_the_tidewatcher` (4-star), are
   still placeholders, so the whole curve is measured against a partly-stubbed bench and will shift
   upward when they are authored. The pinned line's composition is recorded in the archive precisely
   so that re-pass can be measured against this one. **Do not change it.**

## Blocked

- **The talent selection UI.** Not blocked technically, but not worth building against a roster
  whose lower-star kits are still placeholders.
- **Chapters 3 to 6**, on art from Pinoy. `DESIGN-WORLD`'s contested-region proposal for chapter 3
  is moot until they exist.
- **An APK**, on having an Android device to run it on.
- **iOS**, on a Mac and an Apple developer account. Neither exists.

## Open decisions

Two, both about the economy, and neither is a number to nudge.

- **A simulated player arrives at every stage between 1.55 and 3.2 times its `recommendedPower`.**
  The harness prints the ratio per stage: x2.1 to x2.5 across chapter 1, a peak of x3.2 at 2-1, and
  a decline to x1.55 at 2-12. Either progression is too fast or the targets are too low, and those
  are different games. Picking one is a human decision, and it should be made before the
  post-authoring retune rather than discovered during it.
- **The flat pinned five-body curve leaves stages 1-1 to 1-5 without an instrument.** The
  under-geared curve reads 100% on 23 of 24 stages; only `2-12`, moved by the chapter 2 repass,
  reads anything else, at 75%. On 1-1 through 1-4 the pin sits at its own floor, so that column is
  measuring the floor rather than `recommendedPower` at all. Chapter 1's opening stages are
  over-geared enough to read 100% whatever happens, which means nothing currently measures them.
  The geared curve is what content is tuned against and it still works; what is missing is the
  second opinion.

## Known bugs

The harness red first, then the rest.

1. **20 of 90 talents execute but are never observed doing anything.** Each waits on something the
   sweep arena does not raise: a kill, a mark, a host passive resolving first, a particular matchup,
   an `OnLedgerCapped`. Every one is named in the report with what it waits on. The check is red
   because the sweep refuses to call "nothing happened" a pass. It read 29 when the roster was
   authored, 27 after the dark-talent pass, and 20 after the kit-defect pass.

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

- **Whose art is in `Celen.zip`.** Celen and Nereia currently ship **byte-identical sprite sheets**:
  every clip file for `celen_the_open_hand` has the same checksum as the matching file for
  `nereia_the_field_medic`. One of the two deliveries is the other one's art under a different name,
  and which way round it goes is Pinoy's answer, not a guess to make here. Nereia is in the prologue
  squad, so the prologue's first battle is the most visible place a wrong body would show.
- **Strike frames for Mireth, Hale, Rheia, Oris and Arden.** A hand-set strike frame reads
  `impactSetBy: "hand"` in a clip's sidecar; every clip without one takes a fraction derived from
  the clip's shape, which is a guess that happens to be close. These five are the ones whose attack
  clips read wrong.
- **Kiva III's wording**, once the shield is repaired. See item 3 under **Next**.

## Later

Not scheduled and not blocked on anything in **Next**, listed so it is not rediscovered.

- **Guilds phase two**, after the economy decision lands. A guild shop, currency, raids, rewards,
  guild levels and leaderboards all price against an economy that is currently an open question.
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
- Every engine pass, with what it measured, what it deferred and why, and what it refused to build.
- The aspect-rule removal, by far the largest behavioural change in the project, and its measured
  cost.
- Task 13's campaign retune and the full derived `recommendedPower` table.
- The eight kit defects, and the five engine rules the engine claimed in a comment and did not keep.
- The four-star-prep pass that turned thirty-four fired tripwires around and took the harness from
  37 failures to 3.
- The known traps carried forward, the numbers already decided, and the relic and aspect hook
  conflict with its resolution.

Every other `DESIGN-*.md` in this repository is a decision record from its date. The reasoning in
them is durable; the status claims, names and numbers are not. Never conclude from one that a system
is unbuilt.
