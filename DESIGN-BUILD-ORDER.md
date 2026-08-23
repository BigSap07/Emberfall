# Emberfall - build order

Supersedes the previous version, which predates DESIGN-ASPECTS, DESIGN-WORLD, DESIGN-COMBAT-STATS
and DESIGN-ART-BIBLE. Single ordered plan for the whole project, reconciled against those four plus
DESIGN-RELICS and the surviving originals (FIRST-TEN-MINUTES, IDLE-PROGRESSION, REPLAY-ECONOMY). No
new design here, sequencing only.

**Status, as of this revision.** `build-order-7-effect-hit-resist` and `sprite-path-orrin` were
already merged to `main` and green. `build-order-8-team-size-5` and `build-order-9-landscape` are
now merged too: task 10's `campaign.json` refill was committed directly onto
`build-order-9-landscape` rather than its own branch, so all three tasks (8, 9, 10) landed
together in one merge, exactly as this document already called for below. Tasks 8, 9 and 10 are
done. One branch remains unmerged: `build-order-1-3` (still local only, still unpushed). CI now
runs `build-check.ps1` on every push and pull request, so status claims below about compiling or
running are claims about an actual run, not a reading of a diff, wherever a branch has actually
been pushed through it.

Task 9 grew far beyond its original one-line description while unmerged, through several rounds
of Play mode feedback the document did not originally anticipate: portrait to landscape pulled in
a full projection change (perspective to orthographic), a full formation rebuild (a depth-staggered
V, then a flattened two-plane line, then left/right clusters in a three-row taper), a facing
correction (allies were rendering with their backs to the camera after the rearrangement, caught
late), and a battle background loader that did not exist in the plan at all. See task 9 below for
the full account. None of this changes what shipped structurally for task 8 or the content shipped
for task 10; it is scoped to presentation.

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
- DESIGN-RELICS, DESIGN-ASPECTS, DESIGN-WORLD, DESIGN-COMBAT-STATS, DESIGN-ART-BIBLE,
  DESIGN-HOOK-CONFLICT, DESIGN-RECOLLECTION. All committed to `main`. None of them implemented yet,
  all of them decided.
- CI. `.github/workflows/build-check.yml` runs `Tools/build-check.ps1` on every push and every
  pull request, on `windows-latest`. It doesn't stop at compiling, it runs DevHarness, so a green
  run covers content validation, determinism, stage winnability, ability potency and status expiry
  timing, the same checks listed above, on every push rather than whenever someone remembers to
  run it locally.
- All six aspect identities, wired into `CombatSimulation`:
  - **Ember**: each Burn from an Ember source stacks as its own entry via `AddStack` rather than
    overwriting (`AddOrRefresh`), so multiple Ember attackers pile independent Burn entries at
    their own magnitudes. Capped at `EmberBurnStackCap` (3); a fourth application refreshes the
    weakest existing entry instead of adding a new one.
  - **Tide**: energy overflow from a Tide-sourced grant redirects to the living ally furthest from
    full (`AddEnergy` keyed on `sourceId`); one hop, bounded by `_inRedirect`. The cascade guard
    is wired but latent — no current grant is large enough to overflow a recipient and trigger it.
  - **Verdant**: healing overflow from a Verdant source banks onto the recipient as Regen, split
    across `VerdantBankRounds` (2) so the bank pays out at most what overflowed. The Regen tick
    itself cannot bank, preventing a self-feeding loop.
  - **Gale**: a Gale unit's kill re-inserts it into the current round's live initiative list at
    the speed-sorted position among remaining turns, capped at `GaleExtraTurnsPerRound` (1) per
    unit per round. A Slow from a Gale source also shifts the target's pending turn later in the
    current round, not only the next. The Slow half is latent in shipped content (no Gale unit
    authors Slow today); proven in DevHarness against a constructed case.
  - **Umbra**: harmful statuses whose `sourceId` traces to an Umbra unit are skipped by
    `EffectKind.Cleanse` without consuming a Hits count. The persistence half — not ticking down
    while the applier lives — is explicitly not built (that belief belongs to Verdant in
    DESIGN-ASPECTS, not Umbra).
  - **Lumen**: a living Lumen unit unconditionally answers every single-target question aimed at
    its side (`FindTargetAnchor`). Taunt is a secondary tier inside the same method, not the
    primary rule. Emits `TargetAnchored` when Lumen (not Taunt) produced the redirect, so the
    record is distinguishable. Outranks Taunt from any non-Lumen ally to preserve determinism.
- **Orrin Kaal relabeled Ember → Lumen.** `heroes.json` `aspect` field changed; the harness
  proves the aspect-targeting rule holds under the new identity. The cost of the relabel — which
  harness checks had to be updated and which passed without changes — is recorded in `f6021a4`.
- **14 combat stats and their wired behaviors.** All read from JSON into `UnitDef`, propagated
  through `CombatUnit.EffectiveX` accessors, and exercised in `CombatSimulation`.
  Phase A (shipped with the combat pipeline):
  `hp` (health pool) · `attack` (scales damage and non-`selfHpRatio` healing) ·
  `defense` (mitigation: `DefenseConstant / (DefenseConstant + defense + DefensePerLevel·level)`) ·
  `speed` (turn-order sort key, jittered within `SpeedVariance`) ·
  `critChance` (RNG draw; on success multiplies by `critDamage`) · `critDamage` (crit multiplier) ·
  `effectHit` (flat addition to harmful `ApplyStatus` landing chance) ·
  `effectResist` (flat subtraction from harmful `ApplyStatus` landing chance).
  Phase B (added on `feat/stat-fields-phase-b`, all zero-defaulted so existing fingerprints are
  unaffected):
  `skillDamage` (source-side multiplier on Active/Passive damage only, never Basic Attack) ·
  `block` (target-side RNG draw before all other steps; negates the hit entirely and fires
  `OnBlock`) · `armorBreak` (source-side multiplicative reduction of the target's mitigation
  ratio) · `controlImmunity` (target-side flat reduction of Silence/Stun landing chance only,
  no other status) · `damageReduction` (target-side flat % reduction applied after Defense
  mitigation) · `holyDamage` (source-side flat bonus added after Defense/ArmorBreak, never
  reduced by Defense).
- **Silence, Stun, Dispel, Block, Control Immunity** wired into live combat behavior.
  Silence blocks the holder's Active on the turn it would resolve; Basic Attack fires instead.
  Stun skips the holder's entire turn — no Basic Attack, no Active, no energy.
  Both clear via `RoundsLeft` decay; Gale re-entry is gated so a Gale carrier does not shed
  statuses at double rate on its extra turn.
  Dispel (`EffectKind.Dispel`) removes beneficial statuses from the target, respecting the
  `Hits` count and skipping Umbra-sourced harmful entries correctly.
  Block and Control Immunity are stats (phase B above), not `StatusKind` entries.
- **HeroValidator** (`Tools/HeroValidator/validate_heroes.py`). Standalone Python tool for
  vetting ChatGPT-generated hero JSON before it enters the codebase. Validates stat keys against
  `VALID_STAT_KEYS` (updated each phase-B pass), ability shapes, aspect/role enum membership,
  required fields, and fixture format. Two fixture families: `chatgpt-batch-1.json` and
  `chatgpt-batch-2.json` (raw AI output under review) and `regression-batch-1.json` (known-good
  heroes from the shipped roster).
- **UI theme consolidation.** `UITheme` gained three semantic groups — `Ink*` (text edge
  treatments: `InkPanel`, `InkArt`, `InkStrong`), `Scrim*` (backdrop overlays), and
  `Interactive*` (tappable-element tints) — replacing 11 drifted raw `new Color(0,0,0,a)`
  literals at 11 different alphas across the screens. Two intentionally per-screen edges (main
  menu logo warm rim, battle result title) were left in place and documented as deliberate
  exclusions.

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
- **The loader.** Merged to `main`, green on CI. `[U]` only - nothing in the harness touches
  rendering, so a green run confirms it compiles and breaks nothing DevHarness checks, not that it
  looks right; the Play mode look is still completely unverified. Gives `VisualDef` an
  optional sprite field; when set, the battle screen renders that image on a billboard instead
  of building the procedural body. Wired to exactly one hero, Orrin Kaal, as a pipeline test,
  with a hand-built placeholder shipped alongside it. Every other hero and every enemy is
  untouched and stays procedural.

The loader landing before the check screen exists is a reordering, not the plan. The intended
sequence was spec, then a way to look at what the spec produces, then the loader that makes it
real. What happened is the loader shipped second and the check screen hasn't shipped at all.
Recorded here so it isn't quietly read back later as the order that was always intended.

**A second loader arrived alongside task 9, for battle backgrounds, not part of the original
three-part plan above but following the same convention on purpose.** `BattleAssets.BackdropTexture`
loads `Resources/Backgrounds/{environmentId}.png`, mirroring the sprite path's
`Resources/Sprites/Units/{spriteId}.png`, cached the same way. It replaces what used to be a
generated sky quad, an arena ground plane and a ridge of rotated blocks - one painted image now
carries sky, ground and horizon together, sized and positioned off the live camera
(`orthographicSize`, aspect, the rig's own rotation) rather than hardcoded, specifically so it
cannot go stale the way the ridge and sky quad did across three camera changes in one session. The
environment ids campaign.json actually authors are `ashen`, `ember` and `hollow`, four stages
each. Only `ashen.png` exists and is committed; `ember` and `hollow` have no painting yet and fall
back to a generated gradient built from that environment's existing colour palette, never to black
or to the sprite path's magenta (a missing environment painting is the ordinary case here, not a
content bug, so it does not get treated like one). Verified only against `build-check.ps1`, same
as the unit sprite loader - the Play mode look, and in particular whether painted ground lines up
with where units actually stand, is unconfirmed.

One real dependency worth naming despite this not being a numbered task: the loader fixes its
sprite's height to a constant chosen to read correctly against the original portrait camera. Task
9 turned out to change far more than that camera's distance (see task 9 below for the full
account: it went to orthographic projection, then through three re-framings, ending at
`orthographicSize` 4.74). The loader doesn't reference anything task 9 or task 8 touch, so nothing
broke structurally, but two things now need a look once someone can see Orrin in Play mode: his
sprite's fixed height against the final framing, and a second, newer mismatch task 9 introduced on
its own - `UnitRig.Build` now turns every unit `UnitRig.ThreeQuarterTurn` (30 degrees) off
camera-square so the two sides face each other, and a flat sprite turned that way foreshortens
while the art underneath still depicts a square-on character. `DESIGN-ART-DELIVERY.md` already
documents the fix (draw at 30 degrees toward the character's own left) for whenever Orrin's
placeholder gets redrawn; until then it will read visibly flatter than the procedural rigs beside
it, which now turn correctly because the rotation is geometric, not baked into art. Neither check
exists yet.

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

**Resolved.** Aspects keep the hooks; the two conflicting relics are cut outright rather than
redirected: Tidewrought Metronome cut (Tide owns `AddEnergy` overflow), Green Hour Vessel cut
(Verdant owns `ApplyHealValue` overflow). `Kindling` is cut per §3, changing Splitting Prism's
stated cost to Burn-spread-lost. Orrin is relabelled to Lumen; `Warden's Debt` redirects `Shield`
effects, not targeting, so the relic is compatible with the Lumen identity. Task 12 is in progress
on `claude/relics-rework`. See DESIGN-HOOK-RESOLUTION for the full account.

## Known traps, carried forward

1. **Resolved by task 10, kept for the record.** `MaxTeamSize` gates enemy spawns too, not just
   the player side. `BuildUnits` reads it for both. DESIGN-COMBAT-STATS predicted that raising it
   to 5 with `campaign.json` untouched makes
   every stage 5-versus-3: trivially winnable, near-total win rates in the balance pass, and
   `CheckUnderlevelledLoses` failing. That prediction assumed task 8 would ship with zero content
   changes and task 10 would do the refill afterward, alone. It did not ship that way, and CI now
   shows what actually happened instead of what was predicted.

   **What the real branch and a real run show.** `build-order-8-team-size-5` bundles a partial
   `campaign.json` refill into task 8 itself, commented in the diff as a first pass and explicitly
   not balanced: stage 1-1 drops from three enemies to two, 1-2 through 1-4 are untouched, 1-5
   through 1-8 gain one enemy each, and 1-9 through 1-12 gain two, reaching five.
   `recommendedPower` was not touched anywhere. Two different checks then disagree, for two
   different reasons.

   `RunCampaignBalancePass` confirms the prediction exactly, on the metric it actually describes:
   as the simulated player's roster grows toward five deployed heroes, every one of the twelve
   stages shows 100% win rate at roughly double `recommendedPower`. Trivially winnable, near-total
   win rates, as predicted.

   `CheckStageWinnability` is the one that fails, on the five stages named under task 8, and it is
   a stricter, different test: it only ever levels the profile's three starting heroes.
   `CreateNewProfile` still adds exactly `content.StartingHeroes`, three, and `NormaliseTeam` does
   not conjure heroes into the two empty slots on a fresh profile with nothing to summon, which the
   check's own log confirms directly ("a new profile deploys its 3 starters," "slots 4 and 5 start
   empty"). A hypothesis was raised that levelling a five-hero team to the same `recommendedPower`
   budget produces five much lower-level heroes than three would, and that `EffectiveDefense`'s
   `DefensePerLevel * source.Level` term compounds that. Checked against the actual code, this is
   refuted, not confirmed: `StatMath.cs` and `CombatSimulation.cs` have zero diff between `main`
   and this branch, and the team `CheckStageWinnability` levels is three heroes both before and
   after, so no per-hero power dilution happens in the check that is actually failing. The real
   mechanism is plainer: `BuildUnits` clamps enemy spawn count to `MaxTeamSize`, which this task
   also raised to 5, so the newly authored fourth and fifth enemy entries on the affected stages
   can now actually spawn where they were previously truncated at three. A three-hero team still
   levelled to the old, 3v3-tuned `recommendedPower` meets four or five enemies at that same figure
   on exactly the stages whose enemy count grew, 1-6 and 1-9 through 1-12, and loses.

   Neither document is factually wrong about a mechanism. DESIGN-COMBAT-STATS is confirmed for the
   balance pass and was never written to cover `CheckStageWinnability`'s fixed-three-hero test at
   all, its only comment on that check is about a different hazard, Effect Hit/Resist entering
   `PowerScore`. What is actually incorrect is the implicit premise that task 8 and task 10 stay
   cleanly separated: task 8 already contains a first, uneven slice of task 10's job, unbalanced by
   its own admission, and that slice, not a `StatMath` effect, is what `CheckStageWinnability` is
   catching. Task 10 was already the fix for this trap, and has since landed: all twelve stages
   pass `CheckStageWinnability`, including the five named above, and CI is green on `main`. The
   self-applied status duration failure named alongside it in task 8's entry below turned out to
   be a separate, unrelated bug (stage 1-1's enemy count, not team size), fixed in the same pass.
2. **`Debrief.AspectFactorThreshold` (1.2f) must move in the same change as the wheel narrowing**
   (`AspectChart.Advantage` 1.20 to 1.10). Miss it and the Aspect debrief clause silently stops
   firing, no test fails, no error appears. Re-check `AspectDominantShare` (0.35) at the same time.
   Task 12.
3. **Gale's re-entry-on-kill must ship with `galeExtraTurnsPerRound` (1) in the same change**, never
   after. Team size will already be 5 by the time this is built; uncapped, a single Gale unit can
   resolve a round unilaterally, the same failure `MaxPassiveDepth` prevents one level down with no
   equivalent guard on turn re-entry. Task 12.
4. **Only Nyx's Hollowing gains an RNG draw when Effect Hit/Resist land.** Audited: it is the one
   `ApplyStatus` in shipped content whose `chance` defaults to 1.0 today. Every other harmful status
   already rolls. A zero-default phase is therefore bit-identical to today's battles, provable
   against the existing `BattleFingerprint`. Prove it; don't assume it. Tasks 7 and 11.
5. **Kalyx's Kindling is resolved.** Cut in the Ember implementation pass; both docs updated.
   Splitting Prism's stated downside is now about Burn spread lost, not a passive left hungry.
   See "One conflict to resolve" above for the full account.

## Audit: what build-order-8-team-size-5 actually contains

**Historical.** Written while task 8 was still unmerged and task 10 was still unstarted; kept for
the reasoning, not as current status. Everything named as missing, unverified or still-to-be-done
below has since been done: task 10 landed the full refill (as commits directly on
`build-order-9-landscape`, not a branch of its own), task 9's Play mode pass (across several
rounds, well past a single eyeball check) is recorded in task 9's own entry further down, and both
`build-order-8-team-size-5` and `build-order-9-landscape` are merged to `main` and green. Do not
read anything below as an open question.

Read in full against `main`, ten files, every change categorised against the document's own
description of task 8: `maxTeamSize`, `PlayerProfile.Team`, `SaveSerializer` version,
`HeroDetailScreen._slotButtons`, `BattleStage` slot arrays. Inventory only, nothing here proposes
a fix or a re-sequence.

### Matches the document exactly

- `balance.json` / `Definitions.cs`: `maxTeamSize` 3 to 5, both the content value and the
  `BalanceConfig` field it deserialises into.
- `PlayerProfile.cs`: `Team` grows from `new string[3]` to `new string[TeamSize]`, `TeamSize` a
  new `public const int` at 5.
- `SaveSerializer.cs`: `CurrentVersion` 1 to 2, with a new downgrade guard, a save from a newer
  build is refused rather than silently truncated, which is what the document already called for.
- `HeroDetailScreen.cs`: `_slotButtons` sized to `PlayerProfile.TeamSize`, the deploy-button loop
  bound the same way.
- `BattleStage.cs`: two new ally and two new enemy slot positions, the original three left
  numerically untouched. The branch's own comment flags the new x-extents (2.6) as unverified
  against the real camera.
- `Tools/DevHarness/Program.cs`: a new `CheckTeamSizeStructure`, and `CheckDeterminism`'s
  hardcoded `6` replaced with a computed deploys-plus-spawns figure. Both directly verify task 8's
  own claims (profile shape, both save paths, the version guard, the fresh-account winnability
  floor) and belong to it.

### Present, and not in the document's description of task 8

**`galeExtraTurnsPerRound` (`balance.json`, `Definitions.cs`).** A new `BalanceConfig` field and
constant, 1, capping Gale's kill re-entry turns per round. This is Known trap 3's constant, and
Known trap 3 assigns it to task 12, not task 8. It ships two tasks early, for a mechanic (Gale
re-entry-on-kill) that does not exist anywhere in `CombatSimulation` yet, so the constant is
currently unread by anything. The code comment attached to it says "task 11," which matches
neither task 8 (where it shipped) nor task 12 (where the document assigns it), task 11 is Effect
Hit/Resist phase B and has nothing to do with Gale.

**`CampaignScreen.cs` and `MainMenuScreen.cs`.** Both re-lay out their team display for five slots
instead of three, real changes, not renames: `CampaignScreen`'s team strip shrinks each portrait
from 84f to 80f and loops to `PlayerProfile.TeamSize`; `MainMenuScreen`'s team row shrinks each
cell from 270f to 158f and narrows the name and power labels to match. Neither file appears
anywhere in the document's description of task 8, which names only `HeroDetailScreen._slotButtons`
for UI. Whether five cells fit either strip without clipping is exactly the kind of thing the
document already flags as unverified Play mode work for task 8's `[U]` half, it is simply
unverified in two more places than written down.

**Part of task 10 is already here.** See `campaign.json` below in full. In short: this branch does
not ship structural only, it ships a partial, admittedly unbalanced enemy-count refill on eight of
twelve stages, which is specifically task 10's job and not task 8's.

### campaign.json, stage by stage

Twelve stages, four categories of change.

**Untouched: 1-2, 1-3, 1-4.** Still three enemies each, same ids, same levels.

**Reduced: 1-1, Shardfall Verge.** Three enemies to two: `hollow_husk` L1, `hollow_husk` L1,
`grave_moth` L1 becomes `hollow_husk` L1, `grave_moth` L1. One `hollow_husk` removed outright,
nothing else changed. The only stage in either direction that got easier.

**Plus one, formulaically: 1-5, 1-6, 1-7, 1-8.** Each gains exactly one enemy, and the new enemy's
level is the stage's existing lowest level minus one, every time: 1-5 adds `hollow_husk` L8
(existing low was `ashen_revenant` L9), 1-6 adds `cinder_imp` L12 (existing low L13), 1-7 adds
`hollow_husk` L13 (existing low L14), 1-8 adds `cinder_imp` L14 (existing low L15). The original
three enemies on each of these four stages are untouched, same ids and levels. This is a
mechanical, formula-shaped pass, not four separately composed encounters.

**Plus two, uneven: 1-9, 1-10, 1-11, 1-12.** Each goes from three enemies to five, and this is
where the two stages worth naming specifically sit.

- **1-9, Mothlight Steps, subtitled "far too many of them."** Was three `grave_moth`, all level
  30, a deliberately uniform swarm the subtitle is written about. The three L30 moths are
  untouched. Two more `grave_moth` are added, both level 22, eight levels under the original trio.
  The stage stays monotype, so the subtitle's premise reads more true if anything, but the swarm
  is no longer level-uniform, it is a three-plus-two composition, not five equals, and the two
  additions read as a numeric top-up rather than a redesign of the swarm.
- **1-12, The Sundered Colossus, boss stage.** Was the boss (`sundered_colossus` L17) plus two
  `grave_moth` L16 escorts, a clean one-boss-two-adds shape. The boss and both original escorts
  are untouched. Two more enemies are added, both `hollow_husk` L14, the roster's lowest-tier
  trash, below even the existing escorts. The stage is now the boss plus four escorts, not two.
  That is a real change to what the encounter is, a boss fight framed by a small crowd instead of
  a boss fight framed by a pair. Nothing in the diff or its comments acknowledges this as a
  deliberate re-design; it reads as the same formulaic top-up as 1-5 through 1-8, applied twice
  instead of once, on the one stage where the original two-escort count was specifically doing
  work.

**One incidental fix, inside 1-10.** The Grey Bulwark's subtitle is "Two wardens. One narrow
bridge," but the original three-enemy roster spawned exactly one `stone_warden`. The new
five-enemy roster adds a second `stone_warden` (L24) alongside a `hollow_husk` (L22), so the enemy
list finally matches its own subtitle. Whether this was noticed and intended, or arrived as a side
effect of the count going up, cannot be determined from the diff.

**1-11, Pyre of Names.** Adds `hollow_husk` L23 and `cinder_imp` L23, both well under the existing
L26 to L27 trio (`ashen_revenant`, two `grave_moth`, untouched). Two different low-tier types
rather than one repeated, closer to the 1-5 through 1-8 pattern than to 1-9's monotype top-up, but
still filler relative to the stage's existing roster.

Total spawns: 36 before, 47 after, not the 60 a full ramp to five everywhere would need.
`recommendedPower` was not touched on any of the twelve stages.

### What this means for the 8/9/10 stack

Task 8 as written is structural only, and it is not that. It ships part of task 10's job,
unreviewed, alongside its own structural work, and it ships one constant
(`galeExtraTurnsPerRound`) that belongs to task 12's mechanic and is currently dead code. The
existing note that 8, 9 and 10 now land as one merge unit already covers the practical
consequence: this branch cannot go to `main` alone regardless, because its own
`CheckStageWinnability` failures are partly caused by the very enemy counts it already contains.
What this audit adds is that "10" in that stack is not entirely still to be written: a first,
explicitly unbalanced pass at it is already sitting in this branch, and whoever picks up task 10
needs to know they are revising eight already-changed stages, not writing net-new content for all
twelve.

### What could not be determined from the diff alone

Whether the new `BattleStage` slot x-extents, the resized `CampaignScreen` portraits, or the
resized `MainMenuScreen` cells actually render without overlap or clipping. All three are already
flagged `[U]` by the document, and nothing here confirms or refutes them without opening the
editor. Whether the specific enemy and level choices in the campaign.json additions were
deliberately composed or are closer to placeholder values chosen to hit a count, beyond what the
level-delta pattern itself suggests. Whether the 1-10 second `stone_warden` fixing the subtitle
mismatch was intentional. Whether `galeExtraTurnsPerRound`'s value of 1 reflects any actual
playtesting of a mechanic that does not exist yet in code. And whether the comment citing "task
11" for the Gale constant, instead of task 12, reflects a task renumbering that predates this
revision of the document or is simply a mistake, nothing in the diff says.

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
   **Merged to `main`, green.** Both stats added at zero default, the gate wired, and a harness
   check proves the bypass is exact and that Nyx's Hollowing - the one status in shipped content
   that would otherwise gain a draw - still draws nothing. This was previously proven by
   construction and by the check's own logic, not by execution, because this environment has no
   C# compiler to run it with. CI does have one: it ran the check on `windows-latest` against
   `BattleFingerprint` across all twelve stages, and it passed. The no-op claim is proven by
   execution now, not only by construction.
   Blocked by: nothing, can run before team size. Blocks: 11.

8. **Team size 3 to 5, structural only, as written below.** See the audit section after Known
   traps for what the branch actually contains, which is more than this line describes.
   `maxTeamSize` stays a constant at 5. Every player has 5
   banner slots from the start, new account or old; a player does not need 5 heroes to deploy, an
   empty slot is simply empty, nobody needs granting extra starters. `PlayerProfile.Team`,
   `SaveSerializer` version (bumped, to protect the downgrade path: a 5-entry team read by 3-slot
   code truncates silently, dropping two heroes with no error), `HeroDetailScreen._slotButtons`,
   `BattleStage` ally/enemy slot arrays. `[H]` for Core and save, `[U]` for slot layout, which
   overlaps units silently rather than erroring.
   **Done. Merged to `main`, green on CI.** Shipped on `build-order-8-team-size-5`, which stayed
   unmerged until 9 and 10 landed alongside it (see below for why), then merged as one unit. The
   two DevHarness failures this branch carried while unmerged - the self-applied status duration
   sampler, and stage winnability failing on five stages - are both resolved; see Known trap 1 and
   task 10. The `[U]` half (five deploys fitting without overlap, an empty slot reading as open) is
   covered by task 9's Play mode pass below, which went through several rounds specifically because
   the first layout did not clear it.
   Blocks: 9, 10, 11, 12, 14 - all now unblocked on task 8's side.

**8, 9 and 10 landed together as one merge unit, not 8 and 9 alone, as planned.** Build order
already paired 8 and 9 for the slot layout, 9 re-lays out exactly the positions 8 builds, so
shipping one without the other would have left a broken screen either way. CI is why 10 joined
them: task 8 merged alone would have left `main` red on every push until 10 fixed the
recommendedPower-versus-enemy-count mismatch Known trap 1 described. All three merged together,
confirmed green on CI on `main` after the merge.

9. **Orientation: portrait to landscape.** Five-a-side is ten characters across a battle line;
   1080 of portrait width does not carry that read. Touches every screen, not only the battle
   screen, since the UI is built in code rather than laid out visually - there is no separate
   layout pass to redo, every screen's code is the layout. `[U]` heavy: nearly everything here
   fails silently or just looks wrong, long before it fails to compile.
   **Done. Merged to `main`, green on CI.** Grew well past what this line describes, through
   several rounds of Play mode feedback after the branch was actually seen rendering for the first
   time. In order:
   - **Portrait to landscape**, as planned: canvas reference resolution, camera reoriented, every
     screen's code-built layout redone for the wider frame.
   - **Perspective to orthographic projection.** Not in the original plan. Under perspective the
     far (enemy) side necessarily rendered smaller than the near (ally) side regardless of camera
     angle; orthographic removes that falloff so both sides read at identical size, matching the
     mobile-idle-RPG reference this was aimed at.
   - **The formation itself was rebuilt three times** as the camera and projection changed under
     it: a depth-staggered V (the original portrait shape), then a single flattened plane per
     side, then the shipped shape - left and right clusters, each a three-row taper (1 front, 2
     middle, 2 back) with the empty middle band running vertically down the screen rather than
     horizontally across it. Slot spacing was corrected once more after Play mode showed units
     overlapping at partial team sizes (three and four deployed, not only five) - the fix pays a
     real, derived screen-space clearance on both axes rather than assuming either axis alone was
     enough.
   - **The arena boundary stone ring was removed outright**, not retuned. Under orthographic
     projection, at any radius wide enough to clear the formation, the ring ends up sitting between
     the camera and the characters rather than framing them from behind - a structural mismatch
     with this projection and this formation depth, not a tuning error, so no radius fixes it.
   - **Unit facing was corrected**, catching a real bug: the left/right rearrangement above left
     allies rendering with their backs to the camera, unnoticed until specifically checked. Both
     sides now turn `UnitRig.ThreeQuarterTurn` (30 degrees, derived, mirrored by side) toward each
     other rather than standing camera-square, so the two lines read as facing off rather than
     standing to attention. `DESIGN-ART-DELIVERY.md` was updated on `main` with the resulting art
     spec: sprites are drawn at 30 degrees toward the character's own left, and the enemy side
     displays the same file mirrored, not separate art.
   - **A battle background loader was added**, not part of the original plan either: painted
     per-environment images replace the sky quad, ground plane and ridge geometry that used to
     dress the 3D scene. See "Art pipeline, outside the numbered order" above for the loader
     itself and the new `Resources/Backgrounds/{environmentId}.png` convention.
   Blocks: 10, 11, 12, 13, 14 - all now unblocked on task 9's side. Two things are still genuinely
   unverified and belong to whoever looks next, not to this document: whether the painted
   backdrop's ground line matches where units actually stand, and Orrin's sprite against both the
   final framing and the new three-quarter rotation (see "Art pipeline" above).

10. **Refill `campaign.json` with enemy counts that ramp toward five.** Enemy count per stage is
    the scaling variable, not a flat five everywhere: early stages spawn fewer enemies, later
    stages fill out toward the `maxTeamSize` cap. Chapter 1 must stay winnable by the 3 starting
    heroes against whatever stage 1 spawns. Known trap 1. `[H+U]`
    **Done. Merged to `main`, green on CI.** Landed as commits directly on
    `build-order-9-landscape` rather than its own branch, alongside 8 and 9, then merged with
    them. All twelve stages pass `CheckStageWinnability`, including the five named in Known trap
    1. Enemy count ramps 3/3/3/3 (chapter 1), 4/4/4/4 (chapter 1 continued), 5/5/5/5 (the last
    third), not flat five everywhere. `recommendedPower` was raised on six of the twelve stages
    where the composition demanded it; nothing here is final, task 14 is still the real retune.
    Blocks: 11, 12, 14 - all now unblocked on task 10's side.

11. **Effect Hit / Effect Resist, phase B.** Author values on six heroes and eight enemies, add
    `StatusResisted`, wire it into the battle screen and Debrief, add the direction check to the
    harness. `[H]` for the logic, `[U]` for the new event's presentation, which renders on the
    battle screen and so wants orientation settled first.
    **Unblocked.** 7, 8, 9 and 10 have all landed on `main`. Nothing is stopping this from
    starting.
    Blocked by: nothing remaining. Blocks: 14.

12. **Resolve the relic/aspect hook conflict**, then build whichever of Relics and Aspects follows
    from that. `[D]` for the decision, `[H+U]` for the code after, which renders in hero detail and
    on the battle screen.
    Blocked by: the conflict resolution itself, the one blocker left - 8, 9 and 10 have landed.
    Blocks: 14. If the resolution follows DESIGN-ASPECTS'
    suggested direction, build Aspects before Relics: two of six relics need rewriting once Aspects
    lands and two more need re-arguing, so building Relics first means doing part of that work twice.

13. **Opening Gambit**, one pre-battle choice, every battle. Touches `BattleRequest`. `[H]` for
    Core, `[U]` for the pre-battle choice screen, which is new UI and should be built once, in
    whichever orientation ships.
    Blocked by: nothing remaining, 9 has landed. Blocks: 14, it changes win rates.

14. **Full twelve-stage retune. Once, last.** `[H]`, `RunCampaignBalancePass` is exactly the tool; a
    handful of spot-check battles in `[U]` are still worth watching.
    ⚠️ **`RunCampaignBalancePass` alone is no longer "exactly the tool".** See "Task 14's
    instrument was broken" below: it reported 100% on all twelve stages for reasons that had
    nothing to do with the content, and two prerequisites had to land before it could be tuned
    against at all. Both have now landed; the line above is kept because the retune is still one
    pass, still last.
    Blocked by: 1, 3, 5, 6, 8, 9, 10, 11, 12 (whatever of it shipped this pass), 13, everything
    above that changes a win rate or an economy number. Blocks: nothing, this is the last task.

Task 14 is the only step that produces a valid win-rate table. Nothing tuned before it should be
read as final.

## Task 14's instrument was broken, and what was done about it

Found while checking why the wheel narrowing did not move any win rate: `RunCampaignBalancePass`
reported **100% on all twelve stages**, and had been doing so since well before that change. An
instrument reporting a flat line measures nothing, and tuning against it would have been tuning
blind. Neither of the two fixes below is tuning, and neither was in this document's dependency
list, because nobody knew.

**It is not collapsed reporting.** The figure is `measureWins / 24`, resolution 4.2%, and the same
code printed 20.8%, 58.3%, 54.2% and 95.8% on the last four stages at commit `3afd83b`. It
genuinely wins 24 of 24.

**Task 8 caused it, and task 10 did not compensate.** Running the harness at historical commits:
at `3afd83b`, stage 1-9 read teamPwr 5558 against recPwr 5550, a ratio of 1.00, and a win rate of
20.8%. At `c1d5aed`, the team size 3 to 5 commit, the same stage read teamPwr **9232** against the
same 5550. `5558 x 5/3 = 9263`: team power scaled almost exactly with slot count. Task 10 then
raised `recommendedPower` 5550 to 6000, 8%, against a 66% move.

The mechanism is not only the two extra bodies. `PlayerProfile.NormaliseTeam` fills empty slots
with the highest-power benched hero, so the new slots went to Nyx (the roster's only 5-star) and
Maeve (4-star), who had been sitting at Lv1 on the bench in the 3-slot world. Nyx alone now does
46% to 58% of team damage. The pre-change ratio of team power to recommendedPower **decayed** from
1.42 to 1.00 across the campaign, and the win rate broke exactly where it reached 1.00. After
task 8 it never fell below 1.53.

### Blocker A: re-base `recommendedPower` for 5 slots. **Done.**

A five-body line cannot go below **5684** power (best five heroes at level 1, measured), so eight
of twelve stages named a target no five-slot team can be under and pinning a team to it was a
no-op. Every value was multiplied by `floor5 / floor3 = 5684 / 3739 = 1.52019`, the measured ratio
of the minimum deployable line before and after the slot change, rounded to the nearest 50. That
is mechanical: it preserves every stage's ratio to the floor, so no stage's difficulty claim moves
relative to any other's, only the scale it is expressed on. Scaling by the slot ratio 5/3 would
have been wrong, because the two added slots take the weakest two heroes, not average ones.

Nine of twelve stages now sit above the floor, up from four. **1-1 to 1-4 remain under it, and
that predates task 8**: their old values were already below the three-body floor of 3739. No
scaling fixes that; enemy levels do, and that is task 14's.

One consequence to own: `CheckStageWinnability` is the only check `recommendedPower` gates, and it
levels a team **to** that number, so raising the target makes it **strictly easier to pass**. It
stops being the accidental canary it had become. No check changed pass/fail state in the same
commit, but do not read that as the content improving.

### Blocker B: a controlled win-rate curve. **Done.**

`CheckPinnedFiveBodyCurve` fields five bodies levelled to each stage's own `recommendedPower`,
which is the case neither existing instrument covered. Both existing instruments are unchanged,
and `PrintInstrumentComparison` now prints all three side by side, because kept apart the
disagreement is invisible. It reports rather than asserting a target, with one exception: it fails
if the curve is flat, since a curve reading identically on every stage has stopped measuring.

### The two instruments were miscalibrated in opposite directions

This is the finding worth carrying forward. The three checks disagree because each fields a
different team, not because the content is ambiguous.

| Instrument | Team | Bias |
|---|---|---|
| `CheckStageWinnability` | 3 bodies levelled to `recommendedPower` | Harsher than any real player: three bodies against a five-enemy line |
| `RunCampaignBalancePass` | 5 bodies at whatever the simulated economy produced | More generous than one: ran 1.5x to 2.0x over `recommendedPower` |
| `CheckPinnedFiveBodyCurve` | 5 bodies levelled to `recommendedPower` | The closest of the three to what a 5-slot player actually fields |

**`PowerScore` is badly non-linear across body counts, and this is why the first two can invert.**
At stage 1-12, three bodies at 10217 power (Lv33/33/32) win 24 of 24, while five bodies at 10238
power (Lv11/11/11/10/10) win 4 of 24. Same scalar, opposite outcome. A single `recommendedPower`
number cannot describe difficulty for a variable-size team, and any retune that treats it as if it
can will be wrong for one team shape or the other.

### Enemy level is the variable that has not moved

Task 10 refilled `campaign.json` to a 3/4/5 enemy-count ramp, but per this document's own audit of
`build-order-8-team-size-5` the additions were authored at the stage's existing lowest level minus
one: filler below the existing roster. Counts went up, levels did not. Measured against the enemy
line each stage actually spawns, `recommendedPower` before the re-base ranged from 1.27x the enemy
line's power on 1-1 down to 0.70x on 1-10, with the break falling exactly where spawn counts go
from three to four. **Five weak enemies lose to five strong heroes**, so enemy count alone will not
restore the curve. Enemy levels are task 14's first lever, not its last.
