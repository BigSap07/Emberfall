# Emberfall — Chapter 2: The Wandering Coast

Design spec and authoring guide for chapter 2. Covers structure, roster, provisional numbers,
and the two new enemy entries. Nothing here is balanced; task 13 (the full twelve-stage retune)
is the validation step. This doc exists so the content can be authored now and revised with real
numbers later.

Read DESIGN-WORLD, DESIGN-ASPECTS, DESIGN-BUILD-ORDER, and the existing chapter 1 content in
`enemies.json` and `campaign.json` before editing anything.

---

## Region and theme

**Region:** The Wandering Coast. **Dominant aspect:** Tide.

DESIGN-WORLD §2 makes the status of each chapter explicit: "Chapters 2, 4 and 6 stay
single-aspect." That means Tide-themed, not Tide-only. The distinction matters because
DESIGN-WORLD §2 also provides the evidence: chapter 1 — the Ash Marches — contains Umbra husks,
Gale moths, Verdant hounds, and a Tide wretch, and its boss is Umbra. The shipped chapter that
exists is not mono-aspect and is better for it. Chapter 2 follows the same pattern.

A chapter that fielded only Tide enemies would have a single counter-colour, which is precisely
the failure DESIGN-ASPECTS §4 argued the matchup wheel must not amplify. The chapter's identity
is Tide in feel and in roster weight. It is not a compliance check.

Tide's belief (DESIGN-WORLD §1): **nothing should remain fixed. Redirects.** The combat
mechanic (DESIGN-ASPECTS §1): energy overflow from a Tide source is handed to the living ally
furthest from casting rather than discarded. Enemy design here reflects that belief — not raw
attrition, but redirection of force, energy, and momentum.

---

## Structure

12 stages, `ch2_1` through `ch2_12`. Same spawn-count staircase as chapter 1:

| Stages | Spawn count |
|--------|-------------|
| 2-1 through 2-4 | 3 |
| 2-5 through 2-8 | 4 |
| 2-9 through 2-11 | 5 |
| 2-12 (boss) | 5 |

Stage 2-12 is the chapter boss stage.

---

## Level range — PROVISIONAL PLACEHOLDER

Levels run roughly **20 through 38**, picking up immediately after chapter 1's ceiling (non-boss
enemies top out around level 20-21 in the final stages).

**These levels are a structural placeholder, not balance.** The player EXP / level-gate system
is not yet designed and is not in the current build order. Without it there is no derived answer
for what level a player's heroes will be when they reach any chapter 2 stage. The levels here
extrapolate chapter 1's progression curve mechanically — enemies scale by the same `growth:
0.12` coefficient, so the multiplier tables are consistent — but the target power window is a
guess, not a measurement.

The star multiplier curve for hero power is in the same state (no per-tier multipliers exist
yet). The treatment is the same: author the structure, accept re-derivation when the missing
system lands, task 13 retunes regardless.

---

## Enemy roster

### Reused enemies — rescaled, no new content

Three chapter 1 enemies carry forward at higher levels. Same id, different level per stage —
identical to how `hollow_husk` appears at levels 1, 3, 6, 8, 13, and 15 across chapter 1
without being separate entries.

| Enemy id | Aspect | Chapter 1 role | Chapter 2 function |
|----------|--------|----------------|--------------------|
| `tidecaller_wretch` | Tide | Elite support: heals all allies + DefenseUp, `startEnergy: 25` | Primary Tide presence; appears from 2-3 onward; threat-multiplied when paired with the Channeler |
| `grave_moth` | Gale | Minion: fast, AoE AttackDown active, Gale re-entry disrupts turn order | Disruption foil across the chapter; Gale pressure without dominating the read |
| `hollow_husk` | Umbra | Minion: basic attacker, Umbra debuffs uncleansable | Umbra presence prevents pure Tide counter-lining |

`blight_hound`, `cinder_imp`, `ashen_revenant`, and `stone_warden` do not appear in chapter 2.
They are Ashen Road (chapter 1) enemies. Carrying them forward would flatten the distinction
between chapters.

---

### New enemy — ⚠️ PROPOSED DESIGN, REQUIRES MATTHIAS'S SIGN-OFF

**`riptide_channeler` — Riptide Channeler, Tide, elite**

A second Tide elite distinct from `tidecaller_wretch` by aggression and function. The Wretch
heals and protects; the Channeler pressures. Its active delivers moderate AoE damage while
granting significant energy to all allies simultaneously, accelerating the whole enemy line's
ability timing. The mechanic is Tide's belief enacted offensively: force is not absorbed, it
is redirected into the allied line as momentum.

**Basic — "Surge Strike":** Single-target (FrontEnemy), power 0.90. Applies AttackDown 20%
for 2 rounds (60% chance). Tide redirecting force: the hit diminishes what the target can
return, not just what it has left.

**Active — "Riptide Surge" (energyCost 100):**
1. Damage all enemies, power 0.70.
2. EnergyGain 45 to all allies (existing `EffectKind.EnergyGain` + `"flat": 45`).

Effect 2 is the tactical pressure: a Riptide Surge on a line where the Tidecaller and other
enemies are near 100 energy hands them immediate active windows. The Channeler + Tidecaller
Wretch pairing accelerates the entire enemy line — the Wretch heals and defends, the Channeler
charges everyone and chips. Neither alone is oppressive; together they are the chapter's core
Tide threat.

`startEnergy: 30` — arrives partially charged, active comes within the first few rounds.

**Provisional stats (pre-task-13, not balanced):**
hp 1280 · attack 152 · defense 58 · speed 105 · critChance 0.08 · critDamage 1.45 ·
growth 0.12 · effectHit 0.10 · effectResist 0.12

**No art.** Ships without a sprite entry. This is the same state as `sundered_colossus` and
`blight_hound` in the existing content — both are playable, just invisible on the battle
screen. Real art is a separate future task blocked on the ongoing art-pipeline conversation.

**This design needs Matthias's review before the content branch merges.** The Channeler is the
only fully new content in chapter 2 that is not a boss and was not specified by the task. If
the energy-grant active reads wrong in play, or if the Channeler + Wretch pairing is too
oppressive or too easy to counter, the fix is in this enemy's kit. The rest of chapter 2's
structure survives a redesign here.

---

### Boss — The Tideturned

**id:** `tideturned` · **name:** "The Tideturned" (working name) · **kind:** `boss` ·
**aspect:** Tide · **stage:** 2-12

**Basic — "Surge Ebb":** FrontEnemy, power 1.10. Applies Slow (magnitude 0.20, duration 2,
chance 0.85). Even the basic redirects — slowing what comes next.

**Active — "Undertow" (energyCost 100):**
1. Damage all enemies, power 1.25. (Same AoE weight as Sundered Colossus's Gravewake at 1.30.)
2. Swap two random player-side positions. ⚠️ **Requires new `SwapPositions` EffectKind** — see
   "New code required" section below.

**Passive — "Backwash" (trigger: OnDamaged):** Every time the Tideturned takes any hit, it
deals damage to a random player hero (RandomEnemy from the boss's perspective), power 0.30.
No `oncePerBattle` restriction — this is an ongoing tactical constraint, not a one-time phase
shift (contrast with Sundered Colossus's `Fracture`, which is `oncePerBattle: true`).

The mechanic: burst-firing or focus-firing the boss returns partial damage to a random ally on
every hit. Spread damage reduces Backwash's blowback. This is Tide's belief in its defensive
form — every force that strikes the boss is partially redirected. Implemented using existing
`EffectKind.Damage`, `PassiveTrigger.OnDamaged`, and `TargetMode.RandomEnemy` — no new
vocabulary required for this passive.

**Provisional stats (pre-task-13, not balanced):**
hp 2650 · attack 248 · defense 110 · speed 90 · startEnergy 20 · critChance 0.15 ·
critDamage 1.7 · growth 0.12 · effectHit 0.15 · effectResist 0.20

**No art.** Same situation as the Channeler. No sprite, no visual asset beyond colour palette
and sigil index. Art is a separate future task.

---

## Stage composition

Chapter name: "The Wandering Coast" · chapter subtitle: "Follow the redirected tide south, and
find out what chose to change."

Environments: `coast` (stages 1–4) · `tidal` (stages 5–8) · `deep` (stages 9–12).
**None of these environment ids have background assets.** The loader will fall back to whatever
it renders with a missing background. Background art for all three is a separate future task,
blocked on the art-pipeline conversation.

Spawn order is slot order: first entry stands in front.

| Stage | Name | Subtitle | Spawns | Enemies (id, level) |
|-------|------|----------|--------|---------------------|
| 2-1 | Shorebreak Flats | "The tide doesn't leave. It waits." | 3 | hollow_husk(20), grave_moth(20), hollow_husk(21) |
| 2-2 | The Bleeding Shoal | "Red water, always moving somewhere else." | 3 | grave_moth(21), hollow_husk(22), grave_moth(22) |
| 2-3 | Saltwind Rise | "Everything carries the smell of a place it left." | 3 | tidecaller_wretch(22), grave_moth(23), hollow_husk(22) |
| 2-4 | The Drifting Causeway | "The road was here last month." | 3 | tidecaller_wretch(24), hollow_husk(24), grave_moth(23) |
| 2-5 | Tidal Grottos | "The sea decides which passages stay open." | 4 | riptide_channeler(25), grave_moth(25), tidecaller_wretch(24), hollow_husk(24) |
| 2-6 | Undertow Channel | "Strong swimmers have disappeared here." | 4 | riptide_channeler(27), tidecaller_wretch(27), grave_moth(26), hollow_husk(26) |
| 2-7 | Kelp Vault | "Something owns what was once a road." | 4 | tidecaller_wretch(28), grave_moth(28), grave_moth(28), riptide_channeler(27) |
| 2-8 | Hollow Anchorage | "The ships that turned around, and the ones that didn't." | 4 | riptide_channeler(30), tidecaller_wretch(29), grave_moth(29), hollow_husk(28) |
| 2-9 | The Shifting Berm | "Four hours ago this was solid ground." | 5 | riptide_channeler(32), tidecaller_wretch(31), grave_moth(31), grave_moth(31), hollow_husk(30) |
| 2-10 | Wrackline Reach | "What the Tide discards, it keeps." | 5 | riptide_channeler(33), riptide_channeler(32), tidecaller_wretch(32), grave_moth(32), hollow_husk(31) |
| 2-11 | The Deep Reckoning | "Far enough out that land is a theory." | 5 | tidecaller_wretch(35), riptide_channeler(34), grave_moth(35), grave_moth(34), hollow_husk(33) |
| 2-12 | The Tideturned Throne | "It did not break. It redirected." | 5 | tideturned(36), tidecaller_wretch(33), riptide_channeler(34), hollow_husk(32), grave_moth(32) |

Stage 2-12 is marked `"boss": true`.

---

## Provisional economy numbers

Mechanical extrapolation from chapter 1's final values. Task 13 retunes all of them.

| Stage | RecPower | Gold | Shards | AcctXP | IdleGold/hr | IdleShards/hr | 1stClearGold | 1stClearShards |
|-------|----------|------|--------|--------|-------------|---------------|--------------|----------------|
| 2-1  | 8000  | 960  | 48  | 160 | 860  | 35 | 1600 | 260  |
| 2-2  | 9000  | 1080 | 54  | 175 | 930  | 38 | 1800 | 290  |
| 2-3  | 10000 | 1210 | 60  | 192 | 1000 | 41 | 2000 | 320  |
| 2-4  | 11500 | 1360 | 68  | 212 | 1080 | 44 | 2250 | 360  |
| 2-5  | 13000 | 1520 | 76  | 233 | 1160 | 48 | 2500 | 400  |
| 2-6  | 14500 | 1700 | 85  | 256 | 1250 | 52 | 2780 | 445  |
| 2-7  | 16000 | 1900 | 95  | 280 | 1340 | 56 | 3070 | 490  |
| 2-8  | 17500 | 2100 | 105 | 306 | 1430 | 60 | 3380 | 540  |
| 2-9  | 19500 | 2340 | 117 | 335 | 1530 | 65 | 3720 | 600  |
| 2-10 | 21500 | 2600 | 130 | 365 | 1630 | 69 | 4090 | 660  |
| 2-11 | 23500 | 2880 | 144 | 398 | 1730 | 74 | 4480 | 725  |
| 2-12 | 26000 | 3600 | 180 | 500 | 1850 | 78 | 6100 | 1040 |

---

## New code required

### `SwapPositions` EffectKind

Used in the Tideturned's "Undertow" active. Swaps the formation positions of two randomly-
selected units on the player's side. Nothing in the existing `EffectKind` vocabulary handles
positional manipulation — this is a new enum value and a new branch in `Sim.ApplyEffect`.

The JSON entry for this effect is present in the `tideturned` active's effects array
(flagged by comment inline). Until `SwapPositions` is implemented, the entry is either
a no-op or a parse warning depending on how `ContentLoader` handles an unknown `kind` string.
Chapter 2 is not fully testable in Play mode until this is implemented.

No other new EffectKind is required. `Backwash` uses `OnDamaged` + `Damage` +
`RandomEnemy` — entirely existing vocabulary.

---

## Art status

`riptide_channeler` and `tideturned` have no sprite entries and no assets in
`Assets/Resources/Art/`. Both ship with a `visual` block containing colour palette and sigil
index only. This matches the current state of `sundered_colossus` and `blight_hound`, which
are both playable without sprites. The three new environment backgrounds (`coast`, `tidal`,
`deep`) also have no assets and will fall back to whatever the loader renders for a missing
background file. All art is a separate future task blocked on the art-pipeline conversation.

---

## Relationship to the build order

This doc and its companion content branch are an authoring pass only. They produce a chapter
that exists in the game's data, is navigable, and has the correct structural shape. The numbers
are not right and are not intended to be. Task 13 (the full twelve-stage retune) covers all
stages of all chapters when it runs; this authoring pass gives task 13 something to tune
against.

The `SwapPositions` EffectKind is a code task that must land before chapter 2 is fully
testable. It is not in the current build order. Whoever picks it up should add it to the order
or note it as a parallel task.
