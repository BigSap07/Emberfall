# DESIGN-GUILD-FLOOR-CONDITIONS.md

Floor conditions for the guild dungeon. Sections 1 to 8 are the design as written, numbers deliberately
absent. It was built on 2026-09-23: C7's rule is now stated under C7, one claim in section 4 is corrected
against the code, and section 9 records what was built and what the harness measured.

## 1. The problem this solves

The dungeon plays the same every time, which is correct and deliberate: the fight is seeded from the guild id and the cycle, every hero is normalised to level 40 with no relic and no armor, and the same line on the same floor fights the same battle. That rule is what makes the replay honest, the leaderboards meaningful and the three attempts a decision rather than three lottery tickets.

What it lacks is not randomness, it is a reason to bring a different squad. Adding per-attempt variance would break the replay, turn the fastest-clear boards into a reroll contest and cost the attempt budget its meaning. Conditions add the decision without touching the dice.

## 2. The shape

**C1.** Each floor carries exactly one condition, so five per cycle. One, because a condition has to be readable in a single sentence on the floor card. Two stacked conditions stop being a puzzle and become a wall.

**C2.** Conditions are drawn from the existing guild-and-cycle seed, the same pure function of (guild id, cycle) that builds the floors. Everyone in the guild sees the same five all week, a different guild sees different ones, and the whole thing stays deterministic. Nothing about replay, sharing or the boards changes.

**C3.** A condition must be visible on the floor card BEFORE the player commits a line. A condition the player discovers mid-fight is a trap, not a tactic.

**C4.** Conditions belong to content, so the content version covers them. A replay recorded under one condition set must refuse to play under another, exactly as it already refuses when the numbers move.

## 3. The test a condition has to pass

**C5.** A condition must change WHICH LINE you bring, not just how hard the fight is. "Enemies have more health" is a difficulty knob and does not qualify. "Healing is halved" does, because it argues for a second mender or for none at all.

**C6.** No condition may require a specific hero. This is the standing guild rule: finding a good guild must not reduce to owning one unit. The dungeon's existing guardrails are the check, and they must still hold with conditions applied: every floor cleared by at least 40% of sample rosters, at least 25% of rosters missing an obvious answer, and no single hero in more than 35% of a floor's clears.

**C7.** A condition must not trivially satisfy a leaderboard. Six boards exist: four or fewer heroes, no five stars, one aspect, no healer, first attempt, nobody fell. A condition that caps the team at four hands everybody the four-or-fewer board for free. Either the condition and the board may not coincide, or the board is suspended on a floor whose condition grants it. Pick one rule and write it down.

**The rule, decided 2026-09-23: the board is SUSPENDED.** A board that a floor's condition hands to every line that may legally be fielded is suspended on that floor for the cycle. Nobody enters it, it ranks nothing, and the floor card, the dungeon row, the boards screen and the result screen all say which condition suspended it. The other five boards stand as normal.

- **Why suspension and not "may not coincide".** Forbidding the overlap would cut the composition family's bluntest tool, which section 4 calls the one that most reliably changes a squad, just to protect one board of six. A suspension costs that floor one board and keeps the question the condition asks.
- **"Grants" is read off the condition's line rules, never declared by the condition.** A cap of four heroes or fewer grants FOUR OR FEWER. A star cap under five grants NO FIVE STARS. A Mender ban grants NO HEALER. Because it is computed, a condition added later cannot forget to suspend what it grants. FIRST ATTEMPT and NOBODY FELL are about the attempt, not the line, so no condition grants them. No line rule that exists can force ONE ASPECT.
- **A line that breaks a condition's line rules cannot be committed**, and a record of one enters no board. It is kept off the boards, not guessed about.
- **Gate check.** The harness asserts from both ends that a condition suspends exactly the boards every legal sample line would enter anyway.

## 4. Condition families

Shapes, not numbers. The engine already has every primitive each of these needs.

*Corrected 2026-09-23:* it had the statuses (Shield, Taunt, Slow, SpeedUp, Regen and the rest), but not a way for a fight to carry a standing rule. There was no opening-energy offset, no scale on the Energy a body refills on its own, no scale on healing received and no "enemies act first". Those are now `BattleRules`, carried on `BattleRequest.Rules`, and each is a small branch at the one place the engine already did that thing. When `Rules` is null, as it is on every caller except a conditioned guild floor, nothing runs, so no pinned fingerprint moved.

- **Resource**: everyone opens at reduced energy, or energy gain is slowed. Argues for heroes who do their work through basics and passives.
- **Healing**: healing reduced, or capped, or converted. Argues for shields and prevention over recovery.
- **Composition**: a hard cap on line size, or at most one of a role, or at least one of an aspect. The bluntest family and the one that most reliably changes a squad.
- **Enemy opening**: enemies start carrying a Ward, or one body opens with Taunt, or the line gains a status each round. Argues for dispel, for burst, or for patience.
- **Aspect**: one aspect deals or takes more. Note this is the one place the existing design deliberately says no, see section 6.
- **Tempo**: enemies act first, or everyone opens Slowed. Argues for speed and for turn-order manipulation, which is a whole authored family currently underused.

## 5. What the boards become

The six boards already are the tactical layer, and they are currently invisible until after the player has committed. Show them on the floor card next to the condition and the two systems reinforce each other: the condition says what this week is about, the boards say what is worth chasing inside it. This costs nothing to build, because the scoring already works.

## 6. The aspect decision, reopened

The dungeon currently guarantees that no floor is drawn from a single aspect, specifically so that a floor is never a counter-pick check. Conditions make that worth revisiting: exactly one single-aspect floor per cycle would make counter-picking a real weekly decision, and the aspect chart already exists to support it.

This is a decision, not a recommendation. Single-aspect floors reward a wide roster, which cuts against C6 for a small or new guild. If it goes in, it goes in for one floor only and it is measured against the same guardrails as everything else.

## 7. What the harness decides, not this document

- how many conditions exist in the pool
- the magnitude of each one
- which floors may draw which families, if any restriction is needed
- whether the Champion floor draws from a separate pool

These come out of the harness once the shape exists, the same way the star bands and the dungeon's own clear rates were derived rather than declared.

## 8. Open decisions

- Whether guild leaders can see next cycle's conditions in advance. It would be a real planning tool and it belongs to the guild phase two conversation rather than to this one.
- Whether the Champion floor gets a harder condition or a thematic one.
- What happens to the boards when a condition grants one, per C7.
- Whether a condition ever applies guild-wide rather than per floor.

## 9. Built and measured, 2026-09-23

Everything below marked **MEASURED** comes from a run of the named instrument on the tree as committed. Anything else is **READ**, from the code or from this document.

### What was built

- **The condition model.** `FloorConditionDef` in `FloorConditions.cs` holds a name, the one sentence for the floor card (C1), a family, the floor themes that may draw it, fight rules (`BattleRules`) and line rules (`LineRules`).
  - Line rules are caps and bans only: at most N heroes, at most N per role, at most N stars, and banned roles. None of them can require a hero, a role or an Aspect (C6).
- **Content.** The pool is `Assets/Resources/Content/floor-conditions.json`, an eighth content file. `ContentDatabase` loads it and hashes it into `ContentVersion` (C4). The loader refuses any condition in the Aspect family, per section 6.
- **The draw (C1, C2).** `GuildDungeonGenerator.ConditionsFor` gives one condition per floor, drawn from the same (guild id, cycle) seed as the floors.
  - It uses a stream of its own, so drawing conditions moves no enemy.
  - The most constrained floor chooses first.
  - Within a cycle there are no repeats, and distinct families are preferred.
  - Empty content means no conditions, so every floor plays exactly as it did before.
- **The fight.** `GuildDungeon.Fight` applies the drawn condition's fight rules. A replay goes through the same call, so it plays under the same condition.
- **The line.**
  - `GuildDungeon.LineLegal` refuses a line that breaks the line rules and names the reason.
  - The floor card will not let such a line be attempted.
  - `GuildDungeon.Satisfies` keeps a record of one off every board.
- **C7.** `GuildDungeon.Grants` and `GuildDungeon.Suspended`; the rule is stated under C7.
- **C3 and section 5, on screen.**
  - The dungeon row shows the condition's name and sentence under the enemy line, plus any board it suspends.
  - The floor view opens with a band across the top: the condition, then whether your line may enter it and, if not, why.
  - Under the line are the six boards as tiles. Each shows whether this line would enter it and what it is up against: the guild's current lead, nobody yet, or suspended.
  - The boards screen marks a suspended board as suspended.
  - Captured with `GuildDungeonShots` at 1920x1080 and 2400x1080. **MEASURED:** all checks passed, including a formation the condition refuses being refused on the card with ATTEMPT disabled, then tapped into a legal line and cleared.

### The instrument, and the test it applies (C5)

`DevHarness.exe --guild-conditions` runs the study; `GuildDungeonChecks.RunConditionStudy` is its code. It covers every floor of 12 generated dungeons (60 floors), each fought by 264 sample lines at the dungeon level: the gate's spread of random lines, lines missing one role family, and lines of three stars or fewer. Each floor is fought:

- with no condition;
- on two ladders of pure difficulty knobs, one raising enemy Max HP and one lowering hero Max HP;
- once more under a different combat seed, which is chance alone;
- under every candidate condition.

Under a condition with line rules, each roster fields the best of five legal versions of its own line. Each version drops or replaces a different hero, the way a player would answer the rule.

**The reading is which rosters do best, not how many clear.** Every roster gets a margin: hero HP kept minus enemy HP kept. SHIFT is one minus the rank correlation between the rosters' margins with and without the condition.

- A difficulty knob stops the weakest lines first and leaves the order standing.
- A condition that changes which line to bring reorders it.

Each condition is compared with three things:

- the enemy-HP knob that bites exactly as hard, by mean margin;
- the hero-HP knob that bites as hard;
- chance.

**C5 passes when SHIFT is at least 1.5 times the largest of those three.** The 1.5 is a chosen bar, not a measured one. The control, "enemies +75% HP", reads x0.79 to x1.00 against it: the design's own example of a knob, measured failing. Win or loss alone could not be used as the reading, because 86 to 99 percent of random lines clear these floors without any condition, which leaves too few losers to compare.

C6's three guardrails are asserted on the same populations: 40% of all rosters clear, 25% of every group missing an obvious answer clear, and no hero appears in more than 35% of clears. A theme joins a condition's `floors` list only if C5 passed and the guardrails held on all 12 of that theme's floors. `GuildDungeonChecks.CheckConditionShape` re-runs exactly this on every `build-check`, so a change that turns a pooled condition into a knob fails the build.

### The pool: six conditions, three families (MEASURED)

Columns: `base` and `cond` are clear rates without and with the condition. `ratio` is C5's reading. `forced` is the share of the unconditioned floor's clearing lines that the rule makes illegal. `moved` is the feature that shifted most among the best quarter of rosters, measured against the matched knob.

| condition | family | rules | floors that draw it | base / cond | ratio | forced | moved |
|---|---|---|---|---|---|---|---|
| Dead Air | Resource | heroes refill 25% of Energy from attacking and being hit | Wall | 96% / 90% | x2.02 | 0% | single-target lines +11pp |
| Primed to Burst | Resource | every hero and enemy opens with its Active ready | Swarm, Hunt | 99/95, 99/92 | x1.87, x1.69 | 0% | Mender lines -6 to -7pp |
| No Mending | Healing | heroes cannot be healed | Swarm, Wall, Hunt | 99/99, 96/94, 99/98 | x2.36 to x2.37 | 0% | Mender lines -30pp |
| One of Each | Composition | no two heroes share a role | Swarm, Wall, Hunt, Champion | 86-99 / 97-100 | x1.86 to x2.07 | 77-78% | area damage +19 to +24pp |
| Unattended | Composition | no Mender; suspends NO HEALER | Swarm, Wall, Hunt | 96-99 / 98-100 | x2.71 to x3.14 | 52-54% | Mender lines -88pp |
| No Legends | Composition | no 5-star hero; suspends NO FIVE STARS | all five | 86-99 / 91-100 | x1.75 to x4.21 | 71-78% | 5-star lines -91 to -94pp |

Two notes on the table:

- **Composition clear rates rise.** A roster answering a line rule re-picks the slots the rule empties, and the best of five re-picks beats a random line. For these three conditions the reading that matters is the reorder and the forced change, not the clear rate.
- **The ratio is the C5 reading.** The rest of the table describes the condition; it is not the test.

### Cut, by the test rather than tuned (MEASURED)

Each candidate below was authored, fought through the same study, and cut. The number is its best C5 ratio on any theme.

| candidate | family | rules | best ratio | why it was cut |
|---|---|---|---|---|
| Slow Charge | Resource | 50% refill | x1.02 | knob |
| No Charge | Resource | 0% refill | x2.26 (Wall only) | passed, but on exactly Dead Air's one theme; the gentler magnitude was kept. **This was a choice, not a measurement.** |
| Primed | Resource | everyone opens at 50 Energy | x1.14 | knob |
| Thin Blood | Healing | 50% healing | x0.58 | knob |
| Four Stand | Composition | at most 4 heroes | x1.15 | knob (100% forced, but every roster drops its weakest hero and the order stands). This is the condition C7 was written about. |
| Warded / Bulwarked | Enemy opening | Shield worth 20% / 40% of Max HP | x0.88 / x1.02 | knob |
| Standard Bearer (3 rounds / whole fight) | Enemy opening | toughest enemy Taunts | x1.16 | bites almost nothing: the toughest enemy is usually the one hit first anyway |
| Loaded | Enemy opening | enemies open with Active ready | x1.31 | knob |
| Knitting Dark / Deep Knitting | Enemy opening | enemies gain Regen every round | x0.44 / x1.15 | knob |
| Iron Ranks | Enemy opening | 35% damage reduction until dispelled | x1.12 | knob |
| War Cry | Enemy opening | 40% Attack until dispelled | x1.19 | knob |
| Warded Minds | Enemy opening | 50% Effect Resist | x0.08 | no measurable effect |
| Turned Blades | Enemy opening | 30% Block | x1.41 | knob, the nearest miss |
| Ambush (1 round / 3 rounds / always) | Tempo | enemies act before every hero | x0.55 / x0.99 / x0.94 | bites almost nothing |
| Heavy Air / Leaden Air | Tempo | heroes open Slowed 30% for 2 rounds / 50% for 4 | x0.75 / x0.99 | bites almost nothing |
| Quickened / Racing Dark | Tempo | enemies faster | x1.01 / x1.02 | bites almost nothing |
| Caught Napping | Tempo | heroes open Stunned for 1 round | x1.25 | knob |

**No enemy-opening and no tempo condition survived.** That is a finding about this engine on these floors, not a verdict on those families:

- **Enemy opening.** A stat buff on the enemy line hurts every roster roughly equally. Dispel is not common enough in the roster to reorder who does best.
- **Tempo.** A round is every living unit acting once, so turn order within a round barely changes outcomes. Three full rounds of enemies acting first moved the mean margin by 0.00 to 0.06.

Section 4's argument for those families stands; the shapes tried here did not deliver it.

### What the harness decided for section 7 (MEASURED)

- **How many conditions:** six.
- **Their magnitudes:** as in the pool table.
- **Which floors draw which:** the `floors` lists. Attrition may draw only No Legends. Champion may draw only One of Each or No Legends.
- **Whether Champion needs a separate pool.** In effect yes, but not by decree: every condition that bites on Champion breaks the three-stars-or-fewer guardrail there, because that group clears only 25 to 67 percent of Champion floors before any condition.

A consequence to read plainly. In 200 sampled cycles, every cycle drew No Legends (always on Attrition), One of Each (always on Champion) and No Mending. The other two floors drew from Dead Air, Primed to Burst and Unattended. With three families surviving, no cycle can draw five different ones.

### Section 8, answered and not

- **What happens to a board a condition grants:** answered under C7.
- **The Champion floor:** answered above by measurement, not by choice.
- **Still open:** whether leaders see next cycle's conditions in advance, and whether a condition ever applies guild-wide.

### What has to be re-measured, and what is not covered

- **The campaign is about to be replaced**: 72 stages and 84 enemies instead of 24 and 12.
  - No condition names an enemy or a hero, and the harness asserts it.
  - But the floors are built from whatever the campaign fields, and every number above was measured against today's 12 templates.
  - When the new campaign lands, `--guild-conditions` must be re-run and the `floors` lists re-read from it. The gate's `CheckConditionShape` will fail on any theme that no longer passes.
- **The server does not know the line rules.** Migration 0008 checks the hero level and gear, not whether a line fits the floor's condition. A client that skipped `LineLegal` could store an illegal attempt.
  - It would enter no board, because `Satisfies` refuses it.
  - It would still count against the member's attempts and could be a "first clear".
  - Enforcing the line rules server-side would need the server to know the conditions, which means shipping content to it. That is a separate decision.
- **Section 6 is still a separate decision.** The Aspect family is parsed and refused at load.
