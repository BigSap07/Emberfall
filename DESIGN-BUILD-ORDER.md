# Emberfall - build order

Supersedes the previous version, which predates DESIGN-ASPECTS, DESIGN-WORLD, DESIGN-COMBAT-STATS
and DESIGN-ART-BIBLE. Single ordered plan for the whole project, reconciled against those four plus
DESIGN-RELICS and the surviving originals (FIRST-TEN-MINUTES, IDLE-PROGRESSION, REPLAY-ECONOMY). No
new design here, sequencing only.

**Status, as of this revision.** Five branches exist against `main`: `build-order-1-3`,
`build-order-7-effect-hit-resist`, `build-order-8-team-size-5`, `build-order-9-landscape`, and
`sprite-path-orrin`. None are merged. None have been compiled or run. Every status claim below is
a claim about what a diff contains, not about anything that has been seen working.

**Legend.** `[H]` build-check.ps1 / DevHarness only, no Unity needed. `[U]` needs Unity Play mode to
verify, because it fails silently rather than with a compile error. `[D]` a decision, not code.
`[H+U]` the logic is harness-provable, the presentation or feel still wants a look in the editor.

## Already done, do not rebuild

- The full playable loop: collection, leveling, banner, campaign, summon, save, idle cache.
- Combat pipeline: speed-jitter turn order, the Provoke/LastAttacker bypass fix.
- DevHarness checks: content validation, determinism, stage winnability, ability potency, status
  expiry timing, aspect-direction, per-hero damage share and healer-impact probing.
- Battle readability: the round turn strip, `AspectFactor`/`AbilityName` on `Damage` events,
  WEAK/RESIST tags, cause-before-effect targeting.
- Debrief: the one-line cause sentence, Adjust Line, Rematch.
- Chapter 1's first aspect-identity tuning pass. Superseded, not final: it predates the wheel
  narrowing in DESIGN-ASPECTS 4 and everything below it, and gets folded into task 14, not treated
  as sacred.
- DESIGN-RELICS, DESIGN-ASPECTS, DESIGN-WORLD, DESIGN-COMBAT-STATS, DESIGN-ART-BIBLE. All committed
  to `main`. None of them implemented yet, all of them decided.

## Blocked on a push, not lost

**`build-order-1-3`**, local only, never pushed. Contains the starting purse change (1500/260 to
400/40) and it already shifted chapter 1 win rates out of band. `[H]`

Still local-only, still unpushed, unchanged since the last revision of this document. It's the
one branch of the five named in the status line above that can't be inspected from here at all -
it exists only on a machine outside this repository's remotes, so nothing about it can be
confirmed beyond what was already known.

Blocked by: a push. Blocks: task 3, and the final retune (14), which needs to absorb that shift
anyway. Do not re-author the purse change from scratch once the branch is pushed; fold it in.

## Art pipeline, outside the numbered order

The battle-sprite work is not one of the fourteen tasks below and should not become one. It sits
outside the win-rate/economy chain the numbered order sequences, so it doesn't get a number; it
gets tracked here instead. Deliberately not numbered, so nothing here is ever mistaken for a
task-order cross-reference. Three parts, meant to land in this order:

- **The delivery spec.** `DESIGN-ART-DELIVERY.md`. Done, merged to `main`. The technical
  contract for anyone handing over a sprite: file location and naming, format, how a fixed
  height and a derived width replace a fixed box to draw into, where the ground line sits, and
  what the hit-flash/cast-glow/death-fade effects do to the art at runtime.
- **A standalone art check screen.** Not built. The intended second step: somewhere to load and
  look at one sprite in isolation before it's ever wired into a real battle.
- **The loader.** Implemented on `sprite-path-orrin`, unmerged. `[U]` only - nothing in the
  harness touches rendering, so there is no `[H]` half to this at all. Gives `VisualDef` an
  optional sprite field; when set, the battle screen renders that image on a billboard instead
  of building the procedural body. Wired to exactly one hero, Orrin Kaal, as a pipeline test,
  with a hand-built placeholder shipped alongside it. Every other hero and every enemy is
  untouched and stays procedural.

The loader landing before the check screen exists is a reordering, not the plan. The intended
sequence was spec, then a way to look at what the spec produces, then the loader that makes it
real. What happened is the loader shipped second and the check screen hasn't shipped at all.
Recorded here so it isn't quietly read back later as the order that was always intended.

One real dependency worth naming despite this not being a numbered task: the loader fixes its
sprite's height to a constant chosen to read correctly against the current portrait camera.
Task 9 moves that camera closer specifically because characters read smaller in a wide frame.
Nothing breaks structurally when 9 lands - the loader doesn't reference anything task 9 or task 8
touch - but a fixed real-world height renders larger against a closer camera, and Orrin's sprite
scale wants a second look once orientation ships. That check does not exist yet either.

## Undecided, needs a human

**Smaller forks, not blocking yet.** Umbra at team size 5: DESIGN-ASPECTS flags it as diluted
(a third of the enemy line removed at team size 3, a fifth at 5) and offers a spread-on-death rule
or accepting Umbra as a boss-stage specialist. Decide when task 12 reaches Umbra, not before.
DESIGN-WORLD's chapter 3 (and optionally chapter 5) contested-region proposal: moot until chapters
2 to 6 exist, which they don't yet, `campaign.json` has chapter 1 only.

## One conflict to resolve, not silently pick

DESIGN-RELICS and DESIGN-ASPECTS both claim hooks in the same effect pipeline, and DESIGN-ASPECTS
section 6 names four direct collisions:

| Hook | Claimed by the relic | Claimed by the aspect | Collision |
|---|---|---|---|
| `AddEnergy` overflow clamp | Tidewrought Metronome (banks the excess) | Tide (redirects the excess) | Same hook, same wearer (Maeve). Metronome is a no-op if Tide gets it first. |
| `ApplyHealValue` overflow clamp | Green Hour Vessel (turns overflow into a ward) | Verdant (turns overflow into Regen) | Same hook, same wearer (Wren). |
| Kalyx's `Kindling` passive | Splitting Prism's stated cost depends on it existing | Cut outright, as a percentage stat stick | Cutting Kindling erases Prism's downside and leaves a pure-upside relic. |
| Orrin's aspect | Warden's Debt assumes a self-shielding Ember tank | Relabel Orrin to Lumen | A relic built to remove his interception stops being double-edged once interception is his identity, not a side effect. |

DESIGN-ASPECTS proposes a direction (aspects keep the hook, relics move off it: Metronome to
`EnergyGain` cost rather than cap, Green Hour Vessel to the `Shield` path Verdant doesn't take) but
that is a suggestion in the source doc, not a confirmed decision. **This needs resolving before
either Relics or Aspects is implemented** (task 12), not discovered mid-build.

## Known traps, carried forward

1. **`MaxTeamSize` gates enemy spawns too**, not just the player side. `BuildUnits` reads it for
   both. Raise it to 5 without refilling `campaign.json` and every stage becomes 5-versus-3. Task 10.
2. **`Debrief.AspectFactorThreshold` (1.2f) must move in the same change as the wheel narrowing**
   (`AspectChart.Advantage` 1.20 to 1.10). Miss it and the Aspect debrief clause silently stops
   firing, no test fails, no error appears. Re-check `AspectDominantShare` (0.35) at the same time.
   Task 12.
3. **Gale's re-entry-on-kill must ship with `galeExtraTurnsPerRound` (1) in the same change**, never
   after. Team size will already be 5 by the time this is built; uncapped, a single Gale unit can
   resolve a round unilaterally, the same failure `MaxPassiveDepth` prevents one level down with no
   equivalent guard on turn re-entry. Task 12.
4. **Only Nyx's Shadowmark gains an RNG draw when Effect Hit/Resist land.** Audited: it is the one
   `ApplyStatus` in shipped content whose `chance` defaults to 1.0 today. Every other harmful status
   already rolls. A zero-default phase is therefore bit-identical to today's battles, provable
   against the existing `BattleFingerprint`. Prove it; don't assume it. Tasks 7 and 11.
5. **Kalyx's Kindling is unresolved between two docs.** See "One conflict to resolve" above. Do not
   let it get cut or kept as a side effect of building something else.

## Numbers already decided

`startingGold` 400 · `startingShards` 40 · `idleCapHours` 10 · `repeatFullClearsPerDay` 3 ·
`repeatRewardFloor` 0.0 · `dailyResetHourUtc` 4 · Forge multiplier 0.9 (re-check it's still a real
choice against Collect, not always correct) · `maxTeamSize` 5 · `AspectChart.Advantage` 1.10 ·
`AspectChart.Disadvantage` 0.92. Unchanged: `summonCost` 120, the level curve, duplicate conversion,
summon weights.

## The build order

1. **Push `build-order-1-3`**, land the starting purse change it already contains. `[H]`
   Blocked by: nothing but the push. Blocks: 3, and the final retune (14) absorbs its win-rate shift.

2. **`idleCapHours` 8 to 10.** JSON only. `[H]`
   Blocked by: nothing. Blocks: nothing hard, bundle with 4 or 5 if convenient.

3. **Summon pity**: pulls 1 to 3 guaranteed from unowned heroes, plus enemy Aspects and a slot-1
   label on the campaign card. `[H+U]`
   Blocked by: 1, the guarantee fires on the day-0 free pull and a wrong purse wastes it.
   Blocks: nothing hard.

4. **Idle rate resolved from best cleared stage.** Retire the four flat idle constants, add two,
   resolve the `StageDef` at `StagesCleared - 1`. `[H]`
   Blocked by: nothing. Blocks: 5.

5. **Repeat cap** (`dailyResetUnix`, `stageClearsToday`, cleared stages only, save version 1 to 2).
   `[H+U]` for the daily-reset UI feedback.
   Blocked by: 4, reversed order leaves a stuck player no grind path. Blocks: 6.

6. **Collect vs Forge.** `[H+U]`
   Blocked by: 4 and 5, the discount is only tunable once income is capped. Blocks: nothing hard.

7. **Effect Hit / Effect Resist, phase A.** Add both stats at zero default, wire the existing
   `_rng.Chance` gate to read them, prove the change is a no-op against the existing
   `BattleFingerprint`. `[H]`
   **Implemented, unmerged, on `build-order-7-effect-hit-resist`.** Both stats added at zero
   default, the gate wired, and a new harness check proves the bypass is exact and that Nyx's
   Shadowmark - the one status in shipped content that would otherwise gain a draw - still draws
   nothing. Proven by construction and by the check's own logic, not by an actual harness run;
   this environment has no C# compiler to run it with. Merging is the only step left.
   Blocked by: nothing, can run before team size. Blocks: 11.

8. **Team size 3 to 5, structural only.** `maxTeamSize` stays a constant at 5. Every player has 5
   banner slots from the start, new account or old; a player does not need 5 heroes to deploy, an
   empty slot is simply empty, nobody needs granting extra starters. `PlayerProfile.Team`,
   `SaveSerializer` version (bumped, to protect the downgrade path: a 5-entry team read by 3-slot
   code truncates silently, dropping two heroes with no error), `HeroDetailScreen._slotButtons`,
   `BattleStage` ally/enemy slot arrays. `[H]` for Core and save, `[U]` for slot layout, which
   overlaps units silently rather than erroring.
   **Implemented, unmerged, on `build-order-8-team-size-5`.** Never compiled, never opened in the
   editor. The `[H]` half (Core, save) is provable by reading the diff; the `[U]` half - do five
   deploys actually fit without overlapping on screen, does an empty slot read as open rather than
   broken - is still completely outstanding. Nobody has looked at this.
   Blocked by: nothing, both open questions this task depended on are resolved.
   Blocks: 9 (orientation re-lays out these same slot positions), 10, 11, 12, 14.

9. **Orientation: portrait to landscape.** Five-a-side is ten characters across a battle line;
   1080 of portrait width does not carry that read. Touches every screen, not only the battle
   screen, since the UI is built in code rather than laid out visually - there is no separate
   layout pass to redo, every screen's code is the layout. `[U]` heavy: nearly everything here
   fails silently or just looks wrong, long before it fails to compile.
   **Implemented, unmerged, on `build-order-9-landscape`, stacked directly on top of
   `build-order-8-team-size-5`.** Unverified in every sense - not compiled, not opened, not seen.
   The two branches merge together and in order (8 then 9): this diff assumes 8's slot arrays
   already exist and doesn't make sense applied on its own.
   Blocked by: 8, it re-lays out the `BattleStage` slot positions and banner UI task 8 just built;
   land them together so the battle screen gets laid out once, not laid out for 5v5 portrait and
   then redone for landscape. Blocks: 10 (its Unity eyeball pass on the new spawn compositions
   should happen once, in the shipped orientation), 11, 12, 13, and in general every later task
   with a `[U]` or `[H+U]` tag, since their Play mode checks all run in whatever orientation ships.
   Also settle this before any background or hub art starts (outside this list) - orientation
   changes what the artist draws, and that work should not start twice either.

10. **Refill `campaign.json` with enemy counts that ramp toward five.** Enemy count per stage is
    the scaling variable, not a flat five everywhere: early stages spawn fewer enemies, later
    stages fill out toward the `maxTeamSize` cap. Chapter 1 must stay winnable by the 3 starting
    heroes against whatever stage 1 spawns. Known trap 1. `[H+U]`
    Blocked by: 8, 9 (the eyeball pass belongs in the shipped orientation, not the portrait one).
    Blocks: 11, 12, 14.

11. **Effect Hit / Effect Resist, phase B.** Author values on six heroes and eight enemies, add
    `StatusResisted`, wire it into the battle screen and Debrief, add the direction check to the
    harness. `[H]` for the logic, `[U]` for the new event's presentation, which renders on the
    battle screen and so wants orientation settled first.
    Blocked by: 7, 8, 9, 10. Blocks: 14.

12. **Resolve the relic/aspect hook conflict**, then build whichever of Relics and Aspects follows
    from that. `[D]` for the decision, `[H+U]` for the code after, which renders in hero detail and
    on the battle screen.
    Blocked by: 8 and 10 (both reference `AllEnemies` resolution at team size 5), 9 for its UI half,
    and the conflict resolution itself. Blocks: 14. If the resolution follows DESIGN-ASPECTS'
    suggested direction, build Aspects before Relics: two of six relics need rewriting once Aspects
    lands and two more need re-arguing, so building Relics first means doing part of that work twice.

13. **Opening Gambit**, one pre-battle choice, every battle. Touches `BattleRequest`. `[H]` for
    Core, `[U]` for the pre-battle choice screen, which is new UI and should be built once, in
    whichever orientation ships.
    Blocked by: 9. Blocks: 14, it changes win rates.

14. **Full twelve-stage retune. Once, last.** `[H]`, `RunCampaignBalancePass` is exactly the tool; a
    handful of spot-check battles in `[U]` are still worth watching.
    Blocked by: 1, 3, 5, 6, 8, 9, 10, 11, 12 (whatever of it shipped this pass), 13, everything
    above that changes a win rate or an economy number. Blocks: nothing, this is the last task.

Task 14 is the only step that produces a valid win-rate table. Nothing tuned before it should be
read as final.
