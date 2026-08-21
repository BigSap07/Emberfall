# Emberfall - build order

Supersedes the previous version, which predates DESIGN-ASPECTS, DESIGN-WORLD, DESIGN-COMBAT-STATS
and DESIGN-ART-BIBLE. Single ordered plan for the whole project, reconciled against those four plus
DESIGN-RELICS and the surviving originals (FIRST-TEN-MINUTES, IDLE-PROGRESSION, REPLAY-ECONOMY). No
new design here, sequencing only.

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
  narrowing in DESIGN-ASPECTS 4 and everything below it, and gets folded into task 13, not treated
  as sacred.
- DESIGN-RELICS, DESIGN-ASPECTS, DESIGN-WORLD, DESIGN-COMBAT-STATS, DESIGN-ART-BIBLE. All committed
  to `main`. None of them implemented yet, all of them decided.

## Blocked on a push, not lost

**`build-order-1-3`**, local only, never pushed. Contains the starting purse change (1500/260 to
400/40) and it already shifted chapter 1 win rates out of band. `[H]`

Blocked by: a push. Blocks: task 3, and the final retune (13), which needs to absorb that shift
anyway. Do not re-author the purse change from scratch once the branch is pushed; fold it in.

## Undecided, needs a human

**Q1. The five-slot banner starts with three heroes owned. What fills slots 4 and 5?**
- Grant five starters instead of three.
- Unlock slots 4 and 5 as account-level rewards.
- Scale enemy count to the player's filled slots instead of always fielding five.

Blocks: finishing task 8, and everything downstream of it. Without an answer, a new account fights
3-versus-5, the opposite of the trivial-win problem team size otherwise creates.

**Q2. Does the save version bump for team size, separately from the repeat cap bump?**
- Bump it. `PlayerProfile.Team` growing to 5 is tolerant on the upgrade path already, but a newer
  save read by older code silently truncates two heroes off the banner with no error. A version
  number is how anything downstream would catch that.
- Don't. The upgrade path doesn't need it and it's one more number to track.

Affects: the exact save version at task 8, and the final order of the three tasks that touch the
save (5, 8, 11).

**Smaller forks, not blocking yet.** Umbra at team size 5: DESIGN-ASPECTS flags it as diluted
(a third of the enemy line removed at team size 3, a fifth at 5) and offers a spread-on-death rule
or accepting Umbra as a boss-stage specialist. Decide when task 11 reaches Umbra, not before.
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
either Relics or Aspects is implemented** (task 11), not discovered mid-build.

## Known traps, carried forward

1. **`MaxTeamSize` gates enemy spawns too**, not just the player side. `BuildUnits` reads it for
   both. Raise it to 5 without refilling `campaign.json` and every stage becomes 5-versus-3. Task 9.
2. **`Debrief.AspectFactorThreshold` (1.2f) must move in the same change as the wheel narrowing**
   (`AspectChart.Advantage` 1.20 to 1.10). Miss it and the Aspect debrief clause silently stops
   firing, no test fails, no error appears. Re-check `AspectDominantShare` (0.35) at the same time.
   Task 11.
3. **Gale's re-entry-on-kill must ship with `galeExtraTurnsPerRound` (1) in the same change**, never
   after. Team size will already be 5 by the time this is built; uncapped, a single Gale unit can
   resolve a round unilaterally, the same failure `MaxPassiveDepth` prevents one level down with no
   equivalent guard on turn re-entry. Task 11.
4. **Only Nyx's Shadowmark gains an RNG draw when Effect Hit/Resist land.** Audited: it is the one
   `ApplyStatus` in shipped content whose `chance` defaults to 1.0 today. Every other harmful status
   already rolls. A zero-default phase is therefore bit-identical to today's battles, provable
   against the existing `BattleFingerprint`. Prove it; don't assume it. Tasks 7 and 10.
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
   Blocked by: nothing but the push. Blocks: 3, and the final retune (13) absorbs its win-rate shift.

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
   Blocked by: nothing, can run before team size. Blocks: 10.

8. **Team size 3 to 5, structural only.** `maxTeamSize`, `PlayerProfile.Team`, `SaveSerializer`
   version, `HeroDetailScreen._slotButtons`, `BattleStage` ally/enemy slot arrays. `[H]` for Core and
   save, `[U]` for slot layout, which overlaps units silently rather than erroring.
   Blocked by: Q1, `NormaliseTeam` has nothing to fill slots 4 to 5 with until that's answered.
   Blocks: 9, 10, 11, 13.

9. **Refill `campaign.json` to five spawns per stage** (24 new spawns). Known trap 1. `[H+U]`
   Blocked by: 8. Blocks: 10, 11, 13.

10. **Effect Hit / Effect Resist, phase B.** Author values on six heroes and eight enemies, add
    `StatusResisted`, wire it into the battle screen and Debrief, add the direction check to the
    harness. `[H]` for the logic, `[U]` for the new event's presentation.
    Blocked by: 7, 8, 9. Blocks: 13.

11. **Resolve the relic/aspect hook conflict**, then build whichever of Relics and Aspects follows
    from that. `[D]` for the decision, `[H+U]` for the code after.
    Blocked by: 8 and 9 (both reference `AllEnemies` resolution at team size 5), and the conflict
    resolution itself. Blocks: 13. If the resolution follows DESIGN-ASPECTS' suggested direction,
    build Aspects before Relics: two of six relics need rewriting once Aspects lands and two more
    need re-arguing, so building Relics first means doing part of that work twice.

12. **Opening Gambit**, one pre-battle choice, every battle. Touches `BattleRequest`. `[H]` for
    Core, `[U]` for the pre-battle choice screen.
    Blocked by: nothing structurally. Blocks: 13, it changes win rates.

13. **Full twelve-stage retune. Once, last.** `[H]`, `RunCampaignBalancePass` is exactly the tool; a
    handful of spot-check battles in `[U]` are still worth watching.
    Blocked by: 1, 3, 5, 6, 8, 9, 10, 11 (whatever of it shipped this pass), 12, everything above
    that changes a win rate or an economy number. Blocks: nothing, this is the last task.

Task 13 is the only step that produces a valid win-rate table. Nothing tuned before it should be
read as final.
