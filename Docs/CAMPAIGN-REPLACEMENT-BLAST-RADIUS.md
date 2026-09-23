# Replacing the shipped campaign with Pinoy's - the blast radius

Written 2026-09-23. **Decision already made:** Pinoy's campaign replaces the shipped one. Chapters 1
and 2 are reskinned onto his names and rosters, and the 12 shipped enemies are retired. This file
is the work list that decision creates. It proposes no migration and authors no content. The
campaign itself is specified in `CAMPAIGN-SPEC.md`.

Everything below was read from the tree at the time of writing. Symbols are cited, never line
numbers. "Checked" means the claim was re-read in the source during this pass.

**The stage job has since done the replacement** (`CAMPAIGN-SPEC.md` section 10): 72 stages, only
the 84 fielded, the 12 kept as data. Where this file describes the tree it now describes the tree
before that job. The claims the job changed are corrected in place below and marked **(stage
job)**; the rest is the work list as it was, and stays useful as the list of what moved.

---

## 1. The 24 shipped stages his chapters 1 and 2 replace

The shipped chapters are `ch1` "The Ashen Road" and `ch2` "The Wandering Coast" in
`campaign.json`. Pinoy's are "The Forgotten Road" and "The Abandoned Crossing". The table pairs
stages by index only. That is where each of his stages lands, not a mapping of content.

| shipped id | shipped name | his stage | his name | shipped flags vs his structure |
|---|---|---|---|---|
| ch1_1 | Shardfall Verge | 1-1 | The Roadside Shrine | |
| ch1_2 | Cracked Milestones | 1-2 | The Stranger on the Road | |
| ch1_3 | The Smoking Orchard | 1-3 | Alderwick | |
| ch1_4 | Wardenstone Ford | 1-4 | The Cold Forge | |
| ch1_5 | Emberwake Hollow | 1-5 | Beyond the Gate | |
| ch1_6 | The Kiln Gate | 1-6 | Those Who Left | **his miniboss stage** (miniboss unnamed); shipped has no wall here |
| ch1_7 | Drowned Causeway | 1-7 | Another Voice | |
| ch1_8 | Revenant's Landing | 1-8 | The Empty Hamlet | |
| ch1_9 | Mothlight Steps | 1-9 | Beneath the Hill | **shipped mid-chapter wall** (`MidChapterWallStageIds`); his structure has no wall here |
| ch1_10 | The Grey Bulwark | 1-10 | The Hollowed | |
| ch1_11 | Pyre of Names | 1-11 | The One Who Takes | |
| ch1_12 | The Sundered Colossus | 1-12 | The First Echo | `boss: true` in both |
| ch2_1 | Shorebreak Flats | 2-1 | *unnamed* | |
| ch2_2 | The Bleeding Shoal | 2-2 | *unnamed* | |
| ch2_3 | Saltwind Rise | 2-3 | *unnamed* | |
| ch2_4 | The Drifting Causeway | 2-4 | *unnamed* | |
| ch2_5 | Tidal Grottos | 2-5 | *unnamed* | |
| ch2_6 | Undertow Channel | 2-6 | *unnamed* (The Waykeeper) | **shipped mid-chapter wall** and **his miniboss**: the only stage where the two structures agree |
| ch2_7 | Kelp Vault | 2-7 | *unnamed* | |
| ch2_8 | Hollow Anchorage | 2-8 | *unnamed* | |
| ch2_9 | The Shifting Berm | 2-9 | *unnamed* | |
| ch2_10 | Wrackline Reach | 2-10 | *unnamed* | |
| ch2_11 | The Deep Reckoning | 2-11 | *unnamed* | |
| ch2_12 | The Tideturned Throne | 2-12 | *unnamed* (The Shepherd) | `boss: true` in both |

The shipped stage structure the harness pins, and where Pinoy's differs:

- **Size.** `CheckCampaignCurveSchema` asserted `stages == 24`. **(stage job)** It asserts 72 now,
  and `CheckCampaignLinesLegal` asserts six chapters of twelve.
- **Waves.** `ExpectedWaveCount` pins the waves: 3 per stage for 1-1 to 1-4, 6 for 1-5 to 1-12, and
  12 for every later stage. Each wave is 12 battles. His delivery says nothing about waves.
- **Tutorial stages.** `EconomyTutorialStages = 4`.
- **Team size.** `CheckTeamSizeStructure` asserts the first stage fields fewer than 5 enemies and
  the last fields exactly 5. His distributions name 2 to 4 distinct enemies per stage, not a
  formation size.
- **Walls.** `MidChapterWallStageIds = { "ch1_9", "ch2_6" }`. His miniboss is at stage 6 in every
  chapter, so chapter 1's wall position moves from stage 9 to stage 6. **(stage job)** The list is
  gone: `MidChapterWallStageIdsOf` reads `StageDef.IsMiniboss`, so the walls are stage 6 of all six
  chapters.
- **Minibosses.** There is no miniboss flag in the stage schema. `StageDef` has `IsBoss` only.
  **(stage job)** `StageDef.IsMiniboss` exists now, read from `"miniboss": true`; nothing at runtime
  reads it, the harness's power band does.

Other places that name a shipped stage id:

| id | named by |
|---|---|
| ch1_1 | `CheckCampaignMigrationRegression` pins its exact opener: `hollow_husk` 2 ×2 and `grave_moth` 2. It also pins its purse at 120 gold and 6 shards, and `RecommendedPower == 2977`. `CheckCampaignSweep` also uses it. `Tools/playmode-audit.cs.txt`. |
| ch1_4, ch2_7 | `CheckCampaignProgressSaveRoundTrip` |
| ch1_5, ch2_3 | `CheckCampaignRetrySingleBattle`, which needs ch2_3 to have at least 4 waves |
| ch2_1 | `CheckCampaignInterpolation`, which needs a 12-wave stage. `ReportPowerSources` (a report, not a check). |
| ch2_12 | `ReportPowerSources` |
| ch1_9, ch2_6 | `MidChapterWallStageIds`, which feeds `BandKindOf`, then `CheckCampaignPowerCurve` and `SolverBandMiddle` |
| ch1_8 | `Assets/Editor/AspectVerification.cs` (`GetStage("ch1_8")`) |
| ch1_12 | `DevArenaShots.FacingStageId`, `Tools/playmode-audit.cs.txt` |
| ch2_6 | `Tools/playmode-audit.cs.txt` |
| display codes 1-1 to 2-12 | `Tools/DevHarness/stage-fingerprint-baseline.txt`, and the stage argument to `--ceil-reconcile` |

Save data is keyed on stages. Profiles store `StagesCleared` by global index, and
`ClearedStageIds`, `CampaignCurrentStageId` and `StageClearsToday` by stage id. Whether existing
saves keep their ids is part of the migration this file does not propose.

---

## 2. The 12 shipped enemies retired, and everywhere each is named

`enemies.json` has 12 templates. There is no role field and no faction field; `kind` plus `aspect`
is all there is.

| id | name | kind | aspect | art in repo |
|---|---|---|---|---|
| hollow_husk | Hollow Husk | minion | Umbra | `Sprites/Units/hollow_husk.png` (still) |
| cinder_imp | Cinder Imp | minion | Ember | procedural |
| grave_moth | Grave Moth | minion | Gale | `Sprites/Units/grave_moth.png` (still) |
| blight_hound | Blight Hound | minion | Verdant | procedural |
| stone_warden | Stone Warden | elite | Verdant | procedural |
| ashen_revenant | Ashen Revenant | elite | Ember | procedural |
| tidecaller_wretch | Tidecaller Wretch | elite | Tide | procedural |
| riptide_channeler | Riptide Channeler | elite | Tide | procedural |
| sundered_colossus | Sundered Colossus | boss | Umbra | `Sprites/Units/sundered_colossus.png` (still) |
| tideturned | The Tideturned | boss | Tide | procedural |
| fractured_chorister | Fractured Chorister | minion | Lumen | procedural. **No stage fields it.** It exists so the Deep Recital has a Lumen enemy. |
| the_unmade | The Unmade | boss | Umbra | procedural. Fielded only by the prologue (section 3). |

References outside `enemies.json`:

| enemy | campaign.json | prologue (`PrologueScript.BuildStage`) | harness / tools | art | comments only |
|---|---|---|---|---|---|
| hollow_husk | 14 stages, 17 slots | Combat, Formation, default step | `CheckCampaignMigrationRegression` | still + `.meta` | `BattleStage.cs`, `UnitRig.cs`, `DevArenaShots.cs` |
| grave_moth | 21 stages, 29 slots | Combat, Aspects, FullBattle | `CheckCampaignMigrationRegression` | still + `.meta` | `FirstRun.cs`, `BattleStage.cs`, `UnitRig.cs` |
| sundered_colossus | ch1_12 | Skills ×2 | - | still + `.meta` | `DevArenaShots.FacingStageId` summary, `BattleStage.cs` (slot spacing was measured against it), `UnitRig.cs`, `BattleScreen.cs`, `Definitions.cs` |
| cinder_imp | 9 stages, 13 slots | FullBattle ×2 | - | - | `Assets/Editor/AspectVerification.cs` |
| ashen_revenant | ch1_8 to ch1_11, 6 slots | FullBattle | - | - | `Assets/Editor/AspectVerification.cs` |
| blight_hound | ch1_3, ch1_4 | Aspects | - | - | `FirstRun.cs` |
| stone_warden | ch1_4, ch1_6, ch1_10 | - | - | - | `GuildDungeonGenerator.WallDefense` comment. It is the only non-boss enemy that classifies as a Wall (section 4). |
| tidecaller_wretch | ch2_3, ch2_5 | - | - | - | - |
| riptide_channeler | 11 stages, 22 slots | - | - | - | - |
| tideturned | ch2_12 | - | - | - | `tideturned_passive` / Backwash comments in `CombatSimulation.cs` and `Enums.cs`; the Champion-case comment in `GuildDungeonGenerator` |
| fractured_chorister | - | - | - | - | `RecitalGenerator` |
| the_unmade | - | Boss step | see section 3 | - | - |

Design and delivery documents that name shipped enemies:

- `DESIGN-ART-DELIVERY`, `DESIGN-ART-STATE`, `DESIGN-ASPECTS`, `DESIGN-CAMPAIGN-SHAPE`,
  `DESIGN-CHAPTER-2`, `DESIGN-HOOK-CONFLICT`, `DESIGN-RELICS`, `DESIGN-WORLD`.
- `Docs/archive/BUILD-ORDER-HISTORY.md`.
- `Docs/PINOY-ART.md` names them by display name, and is the artist's current list of enemies to
  draw. **All nine enemies it asks him to draw are the ones being retired.**

Checked: `events.json`, `relics.json`, `Server/` and `HeroValidator` name no enemy and no stage. The
hero sweep, talent sweep, arena matrix and dark rooms use enemies the harness authors itself
(`sweep_arena`, `DarkEnemies`), and none of those are content enemies.

---

## 3. The prologue and `the_unmade`

The prologue is **not** in `campaign.json`. `PrologueScript.BuildStage` builds synthetic
`prologue_<step>` stages (chapter 0, environment "hollow") in code, and the fixed hero squad is
level 42 (`BuildSquad`, `SquadLevel`). It spawns shipped enemies by id (checked):

| step | spawns |
|---|---|
| Combat | hollow_husk 34, grave_moth 34 |
| Formation | hollow_husk 36 ×2 |
| Skills | sundered_colossus 48 ×2 |
| Aspects | blight_hound 36, grave_moth 36 |
| FullBattle | ashen_revenant 38, cinder_imp 37 ×2, grave_moth 36 |
| Boss | **the_unmade 60**, `IsBoss = true`, stage name "The Unmade" |
| any other step | hollow_husk 34 |

- **Silent failure.** Retiring any of these seven ids without touching `PrologueScript` does not
  fail loudly. `CombatSimulation.BuildUnits` skips an enemy id it cannot find. The prologue would
  field partial or empty lines. The run is driven by `FirstRun.cs` (`FirstRun.BeforeBoss` runs the
  Boss step) through `BattleScreen.SetPrologueStep`.
- **The Collector's hook.** Pinoy's chapter 1 opens straight out of the prologue's rewind, so the
  prologue is narratively his too.

Assertions that `the_unmade` never appears in generated content:

- **`CheckRecitalDrawsNoScriptedLoss`** (`Program.cs`, checked). It computes the set of enemies
  that the prologue boss fight fields and no campaign stage fields. It asserts that set **contains
  `"the_unmade"`**. So it fails if the_unmade is removed, and also if any campaign stage fields it.
  It then asserts none of the first 120 Recital floors fields any member of that set, and that
  every floor fields a full line of 5.
- **`GuildDungeonChecks.CheckFloors`** asserts no floor's `EnemyId == "the_unmade"` (checked). It
  covered one seed ("floors-check", cycle 1) and its 5 floors when this was written. The roster
  job widened it to cycles 1-1000 of that seed, 5,000 floors (`CAMPAIGN-SPEC.md` 9.5).

The code side excludes it in two places:

- `RecitalGenerator.ScriptedLossEnemies` computes the same set the check does. **(stage job)**
  `RecitalGenerator.BuildFloor` now also draws only what some campaign stage fields
  (`GuildDungeonGenerator.TemplatePool`), which is what keeps all twelve shipped templates out of
  the Recital now that no stage fields them.
- `GuildDungeonGenerator.TemplatePool` excludes it because no stage fields it, and
  `GuildDungeonGenerator.BuildFloor` also skips every `kind == "boss"`.

The Proving fields no enemies at all (section 4). Nothing asserts that the prologue fight is
actually lost.

---

## 4. Every system that reads the enemy pool, and what each needs from the new roster

The 84 new enemies have race, role and concept (except chapter 1, which has none of the three). None
has an aspect, stats or a kit. This is what consumes them, found by grepping every `.Enemies` read
in `Assets/Scripts` and every check in `Tools/DevHarness`.

**Two systems read `enemies.json` as a pool at runtime:** the Deep Recital and the guild dungeon.
Every other runtime reader reads a stage's own list.

### 4.1 The Deep Recital - `RecitalGenerator.BuildFloor`

- **What it reads:** every `enemies.json` template whose `aspect` equals the floor's aspect. It
  subtracts `ScriptedLossEnemies`. It does *not* restrict to campaign-fielded enemies, and it
  does *not* skip bosses: a `kind: boss` template is drawn as an ordinary floor enemy. (Added by
  the roster job, which measured it: `CAMPAIGN-SPEC.md` 9.6.) **(stage job)** It restricts to
  campaign-fielded enemies now, the guild's rule; it still draws bosses.
- **Floor order:** floors cycle Ember, Tide, Verdant, Gale, Umbra, Lumen (`Cycle`,
  `AspectForFloor`).
- **Requirement: at least one non-scripted template per aspect, all six.** An aspect with none
  produces an **empty floor**, which is a sixth of every cycle. The generator's own comment says
  so. `CheckRecitalContent` asserts `allAspectsCovered`. `CheckRecitalDrawsNoScriptedLoss` asserts
  a full line of 5 on each of the first 120 floors.
- **Today:** Lumen is covered only by `fractured_chorister`, which was added for this reason and
  no stage fields. **(stage job)** The Chorister is retired with the rest; Lumen is covered by the
  14 Lumen templates the campaign fields.
- **With the old roster retired:** the new roster must carry all six aspects, or keep a stand-in
  per aspect.
- **Difficulty** scales by floor level (`RecitalLevelsPerFloor`, `RecitalBonusLevelsPerCycle`),
  not by template choice.
- **Environment:** `EnvironmentForAspect` maps each aspect to a painted background. None of the
  backgrounds were authored with an aspect in mind.

### 4.2 The guild dungeon - `GuildDungeonGenerator`

- **Which enemies:** `TemplatePool` takes only enemies that **some campaign stage fields**, so
  its pool follows the campaign.
- **Bosses:** `BuildFloor` skips every `kind == "boss"`.
- **Themes need behaviours.** Swarm, Wall, Hunt, Attrition and Champion are filled by behaviour.
  `Classify` reads each enemy's kit:
  - `BaseDefense >= WallDefense` (90) or a Taunt status makes a **Wall**.
  - An `AllEnemies` target makes a **Spreader**.
  - A `LowestHpEnemy` or `HighestAttackEnemy` target makes a **Hunter**. No other targeting
    rule does: `HighestHpEnemy` and `RandomEnemy` do not. (Corrected by the roster job, which
    read `ClassifyAbility`.)
- **Missing behaviours fail silently.** `Fill(... Or(With(rank, behaviour), rank))` falls back to
  any enemy when no enemy has the behaviour. So if no new enemy classifies as a Wall, every Wall
  floor still builds and shows the text "something holds the front", and nothing holds the front.
- **Today:** Stone Warden is the only non-boss Wall. **(stage job)** It is retired; the fielded
  pool has 9 non-boss Walls, 25 Hunters and 12 Spreaders (measured, `CAMPAIGN-SPEC.md` 10.7).
- **The new roster needs, among its campaign-fielded non-bosses, at least one Wall, one Hunter and
  one Spreader.** This is a kit requirement, not an art one.
- **Power targets:** the floor power targets in `balance.json` are calibrated against the current
  pool, and `GuildDungeonChecks.CheckNotAHeroCheck` asserts that calibration.
- **Replays:** `ContentDatabase.ContentVersion` hashes `campaign.json` and `enemies.json`, so any
  edit makes stored guild dungeon attempts refuse to replay ("content moved").

### 4.3 The Proving - reads no enemies

**This corrects the brief.** The Proving does not generate from the enemy pool. Its opponent is a
player team snapshot. The comment on `ProvingGenerator` says so, and so do `BuildProvingRequest` in
`GameSession.cs` and `CheckProvingContent`, which asserts the Proving stage authors **no enemies**
because `CombatSimulation` ignores `Stage.Enemies` whenever an opponent snapshot is present. The
same check asserts its id is not a campaign stage id. The new roster asks nothing of it.

### 4.4 The prologue - `PrologueScript`

It reads seven shipped ids by name (section 3), and there is no loud failure if they go.

### 4.5 The campaign - `CampaignWaveGenerator`

It reads each stage's own enemy list and copies `recommendedPower`. It is covered by sections 1
and 5.

### 4.6 Harness checks that read every enemy

- **Kit-feature guards.** About 20 checks scan all shipped content to confirm no enemy or hero uses
  an engine feature that is built but not yet allowed. Examples: `CheckNothingShippedUsesTheFourthTier`,
  `CheckNoDormantSentinels`, `CheckNoShippedCastUse`, `CheckLinkNoShippedUsage`. **Any new kit that
  uses one of those features turns its check red.** Whoever writes the 84 kits must run the gate
  on each batch.
- **Art.** `CheckSpriteDelivery` requires every enemy that declares `visual.sprite` to resolve to a
  file. Otherwise the enemy is BROKEN and renders as a magenta square. `CheckEveryDeliveredSheetIsPlayed`
  and `CheckPortraitDelivery` also read every enemy (section 6).
- **Content minimums.** `CheckContent` requires at least 10 stages and at least 6 enemies.
- **The solver.** `RunRecommendedPowerSolver` **aborts if any enemy's `growth` is not 0.12**.
  Every new enemy must carry that growth, or the solver must change.
- **Stage curve.** `CheckStageWinnability`, `CheckPinnedFiveBodyCurve`, `CheckArmoredCurveDelta`
  and `CheckInstrumentsSeeTheWholeCurve` read every stage and so every fielded enemy.
- **Positional readers.** Some code reads stages by position, not id. `RepeatCap`, `RelicDust`,
  `SaveRoundTrip` and `CloudChecks` use `AllStages[0]`, `AllStages[1]` or the last stage.
  `CheckFusionEconomyDerivation` treats the last stage of the whole campaign as the finale, so
  adding chapters 3-6 moves it.

### 4.7 What the new roster therefore needs

Each of the 84 needs these before a build job can run. None of them is in the delivery.

- An `aspect`. All six aspects must be covered by non-scripted enemies.
- Base stats: `hp`, `attack`, `defense`, `speed`, crit, effect hit/resist and the rest of the
  common unit fields, plus `startEnergy`.
- `growth` of 0.12.
- A `basic` and an `active` ability.
- A `kind` (minion, elite or boss).
- A `visual`: silhouette, and a sprite id or none.
- For the guild dungeon, among the campaign-fielded non-bosses: at least one Wall, one Hunter and
  one Spreader.

Pinoy's role is a strong hint for the kit, but the enemy schema has no field to store it in.

---

## 5. What breaks, and must be regenerated, when chapters 1 and 2 are reskinned

### 5.1 `recommendedPower` - generated

- **The tool:** `pwsh Tools/solve-recommended-power.ps1` (`-WhatIf` runs on a copy). It calls
  `DevHarness.exe <content> --solve-recommended-power`, which is `RunRecommendedPowerSolver`.
- **What it does:** it walks the economy up to 25 times and sets each stage's power to the
  simulated entry power divided by its band middle (`SolverBandMiddle`). It also moves
  `floorLevel`, `ceilingLevel` and the enemy levels, measured against a ceiling-battle win rate of
  85%, or 65% for a finale.
- **How it writes:** it rewrites `campaign.json` line by line (`WriteCampaignJson`). That requires
  `"id"` and `"index"` on one line and each stage's enemies on one line, so new content must keep
  that layout.
- **Runtime:** only `CampaignWaveGenerator` reads it, and no UI shows it.
- **What fails when it is stale:** `CheckCampaignPowerCurve`, the geared ceiling band in
  `CheckArmoredCurveDelta`, and the ch1_1 pin to 2977 in `CheckCampaignMigrationRegression`.
- **The solver does not compile today (checked by reading, not by running it).** Its `$sources`
  list leaves out `Tools/DevHarness/ArenaMatrix.cs`. `build-check.ps1` includes that file, and
  `Main` calls `CheckArenaMatrix`, which is defined in it. `CheckSolverAndGateCompileOneList` exists
  to catch exactly this drift. This has to be fixed before recommendedPower can be regenerated at
  all.

### 5.2 The 72 stage fingerprints - generated

- **The file:** `Tools/DevHarness/stage-fingerprint-baseline.txt`. It holds 24 stages × 3 seeds
  = 72 lines of `DisplayCode|seed|FNV-1a-64`.
- **What is fingerprinted:** the opening battle (`Opening(stage)`, wave 1 battle 1), fought by a
  fresh profile's three starters.
- **Seeds:** `3000000 + globalIndex*13 + k`, with k from 1 to 3.
- **Produced by** `CheckEffectHitResistNoOp`, and **compared by** `CheckStageFingerprintBaseline`.
  It fails as "N of 72 MOVED", "not in the baseline", or "no longer resolve to a shipped stage".
- **To regenerate:** run build-check with `EMBERFALL_REGEN_STAGE_FP=1`.
  `EMBERFALL_DUMP_STAGE_FP=1` dumps the event streams.
- **Every line moves.** A reskin changes every opener, and adding chapters 3-6 makes it 72 stages
  × 3 = 216 lines. Regenerating is therefore not evidence that behaviour is unchanged. It is a new
  baseline.

### 5.3 The campaign balance walk - recomputed every run

- **Where it lives:** `RunCampaignBalancePass` runs `SimulateEconomy`. It is recomputed on every
  harness run, and nothing is stored. Its heading still says "144-battle ladder", which is stale.
- **Consumers:** `ReportPowerSources`, `CheckCampaignPowerCurve`, `ReportPaceTargets`,
  `ReportFusionWalk`, `CheckInstrumentsSeeTheWholeCurve`, and the solver.
- **What breaks:** nothing needs regenerating, but every check it feeds re-evaluates against the
  new content and the new stage count.

### 5.4 The power bands - hand-authored constants, and nothing regenerates them

- **`CheckCampaignPowerCurve`:**
  - Ordinary stages: 1.10 to 1.30.
  - Walls: 1.00 to 1.05 in chapter 1, 0.95 to 1.02 from chapter 2 on.
  - `BandTolerance` of 0.005 past either edge.
  - The first 4 stages have no band.
  - A wall is any stage with `IsBoss` or listed in `MidChapterWallStageIds`.
  - When stale it fails with "N stage(s) left the power band".
- **The geared ceiling band** (`GearedCeilBand*`): 0.78 to 0.92, and 0.55 to 0.72 for a finale.
- **Wall ids are hard-coded.** `MidChapterWallStageIds` lists ch1_9 and ch2_6. Pinoy's minibosses
  are at stage 6 of every chapter, and chapters 3-6 need walls of their own.
- **"Finale" means three different things:**
  - The geared band and the solver use `IsChapterFinale` (highest index in the chapter).
  - The power band uses `IsBoss`.
  - `CheckFusionEconomyDerivation` uses the last stage of the whole campaign.

  Adding chapters moves the last of these.

### 5.5 Also stale after a reskin

- `idleGoldPerHour` follows a rule written in a `campaign.json` comment, applied by hand. No
  tool produces it and no check enforces it.
- The stage-id-keyed checks in section 1 (`CheckCampaignMigrationRegression`,
  `CheckCampaignRetrySingleBattle`, `CheckCampaignInterpolation`, `CheckCampaignProgressSaveRoundTrip`)
  and `ReportPowerSources` pin shipped stage contents, wave counts and power.
- `DevArenaShots.FacingStageId` and `AspectVerification.cs` load shipped stages by id.
  `Tools/playmode-audit.cs.txt` names ch1_1, ch1_12 and ch2_6.
- Guild dungeon attempts stored before the content change stop replaying (`ContentVersion`, 4.2).

---

## 6. The art: what the loader needs, and what was measured

### 6.1 What was READ in the loader and harness

**Where art lives and how it is named.** Everything is in `Assets/Resources/Sprites/Units/`, and
the name comes from the unit's `visual.sprite`. A unit is one of two shapes, and
`UnitRig.BuildSprite` tries them in this order (mirrored in the harness's `SpriteResolves`):

1. per-clip pairs, `{sprite}_{clip}.png` plus `{sprite}_{clip}.json`, where `{sprite}_idle`
   must exist
2. one still, `{sprite}.png`

**What happens when art is missing:**
- A unit that names no sprite is drawn procedurally.
- `spriteStatus: "pending"` renders the tinted `placeholder_dummy.png`.
- A unit that names a sprite with no file behind it renders a magenta square, logs an error, and
  fails `CheckSpriteDelivery`.

**Clips** (`BattleClips.Played`, checked): `idle`, `basic`, `basic2`, `skill`, `hit_reaction`,
`death`, `controlled`.
- **Only `idle` is required** to animate (`SpriteAnimator.TryCreate`).
- `basic2` falls back to `basic`, and `controlled` falls back to `idle`. A missing `skill` keeps
  the rest loop.
- `basic`/`basic2` alternate per unit, counted from the event record.
- `hit_reaction` never interrupts the unit's own action clip.
- `death` holds on its last frame.
- `controlled` plays while stunned.

**Sidecar fields parsed** (`BattleAssets.UnitAnimClip`, checked):
- `frameWidth`, `frameHeight` (defaults are the texture size; they set the aspect ratio)
- `sheet.cols`, `sheet.rows`
- `defaultAnimation.start`, `end`, `fps` (default 30), `impactFraction` (default 0.5),
  `playbackSpeed`, `footFraction`
- Frames are read row by row from the top left.
- `loop` and `impactSetBy` are present in the files but ignored.
- A sidecar without its png is a warning and the clip is skipped.

**Size.** There is no pixels-per-unit in code and no fixed frame count. On-screen height is
`UnitRig.SilhouetteTop(silhouette) / spriteBodyFraction`.

**The house standard, measured on the tree** (checked on `kael_the_unyielding_idle`):
- 220×260 frames at 30 fps in a 9×8 grid, a 1980×2080 sheet. This is 176 of the 204 hero sheets.
- One hero's 7 clips come to 24.9 MB.
- The whole `Units` folder is 593 MB (566 MiB; sizes in this section are decimal).
- `.gitattributes` does not route PNGs to LFS.

**What the harness asserts about delivered sheets:**
- `CheckEveryDeliveredSheetIsPlayed` fails on a clip name that is not in `BattleClips.Played`, and
  on a png without its json or a json without its png.
- It assigns a file to the longest declared sprite id it starts with plus `_`, using an **ordinal,
  case-sensitive** `StartsWith` (`FindUnreachableSheets`).
- `CheckSpriteDelivery` requires a declared sprite to resolve. It also fails when a unit is still
  marked pending but has art.

**How Pinoy's hero sheets have come in so far** (read from `D:\Emberfall\import\sprites\Aeron.zip`,
outside the repo):
- One zip per unit, holding `<unit>_<clip>.png` sheets at **880×1040 frames** and one
  `sprite_sheets.json` manifest from the Sorceress Sprite Analyzer.
- There is no per-clip engine sidecar. **The engine sidecars are made on our side** by
  `Tools/rewire_sprites.py`, which downsamples to 220×260, trims trailing empty frames, measures
  foot and body fractions, and writes png + json + `.meta`.
- Clip filenames vary by delivery (`basic1`, `basic`, `basic_2`), and `rewire_sprites.py` maps them
  per unit by hand.
- `Tools/import-sprite-sheets.ps1` is the older importer. It is stale: five clips, 288×512 frames.

### 6.2 What was MEASURED in the delivery

See `CAMPAIGN-SPEC.md` section 7 for the full list.

- **The OG images:** 84 single images, 8-bit RGBA, 139.9 MiB. They come in seven canvas sizes
  (66 at 1312×1199).
- **No sidecars.**
- **Body alpha is 252-253, not 255.**
- **24 figures are cropped by their canvas edge.**
- **Every file carries an OpenAI C2PA `caBX` manifest.**
- **Every `SPRITES/` and `ANIMATIONS/` folder in the export is empty**, chapters 1 and 2 included.

### 6.3 What that means for getting enemies into the game

**What an enemy needs.** Only `idle` is needed to animate. The full set a battle can request is
idle, basic, basic2, skill, hit_reaction, death and controlled. **Or it needs nothing animated at
all**: one still `{sprite}.png` is a legal enemy today, as `grave_moth`, `hollow_husk` and
`sundered_colossus` are.

**Can the OGs be cut into sheets?** No. Each OG is one pose. Frames of motion cannot be cut from a
single pose, so sheets must be drawn, which Pinoy is doing. What an OG *can* be is a still for an
enemy whose sheets are not in yet. It would need:
- a lower-snake-case name (`dire_wolf.png`) matching the enemy's `visual.sprite`
- downsampling (existing enemy stills are 1024-1536 px)
- a decision on the canvas-edge crops and the 252-253 body alpha, both of which carry straight
  onto the screen

**What the repo should store.** The processed output, not the 140 MB of source. Heroes already work
this way: their source zips live in `D:\Emberfall\import\sprites`, outside the repo, and the repo
holds only the 220×260 sheets, sidecars and `.meta` files.
- Stills: one downsampled png per enemy.
- Sheets: what `rewire_sprites.py` emits.
- **Scale:** at Kael's measured 24.9 MB per 7-clip hero, 84 fully animated enemies would add about
  **2.1 GB** to a repo whose `Units` folder is 593 MB today. That is arithmetic from one
  measurement, not a measurement of enemy sheets.

### 6.4 Do his delivered sheets match? - NOT YET INSPECTED

The export in hand has no sheets (6.2). **No verdict is given here, either way.** Run this list
against the real sheet folder the moment its path is known:

1. **Clip names.** Every clip maps to one of `idle basic basic2 skill hit_reaction death
   controlled`. `idle` is present for every enemy that has any sheet.
2. **Pairing.** Either a per-clip png plus an engine sidecar with the fields in 6.1, or the hero
   delivery shape: `<unit>_<clip>.png` plus one `sprite_sheets.json` from the Sorceress Sprite
   Analyzer, which `rewire_sprites.py` converts.
3. **Naming.** Stems are consistent per enemy and map to the roster name. Note `The_sheperd` and
   `The_Forsaken`. The harness's case-sensitive match means the final in-repo names must be lower
   snake case.
4. **Frames.** A uniform frame size per sheet. The heroes are 880×1040, which downsamples to
   220×260. There should be a grid with no partial last row, or the sidecar's `end` set correctly,
   and frames ordered row-major from the top left.
5. **Registration.** Feet on a consistent baseline across frames and clips (`footFraction` is
   measured from it), and the figure not cropped by the frame edge.
6. **Alpha.** Body opaque at 255, with no faint halo.
7. **Coverage.** A sheet set exists for every roster entry in that chapter, and no sheet exists
   for a unit not on the roster.
