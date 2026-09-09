# Emberfall - build order

## Now / Next

Current state, checked against the repo on 2026-09-09, not against the rest of this document.

- **Both balance-instrument branches have landed on `main`.** They were written from the same base
  and merged economy-first, ladder-second. Between them they close the last of the floor-pinned
  instruments and re-run the retune against what those instruments now actually measure.
  - **The geared curve, the construction guard, the task 13 retune.** The GEARED CURVE reports
    FLOOR/CEIL/BLOCK at 200 samples instead of one number describing battle 1 of 144;
    `BuildBattleRequest` refuses any unresolved multi-battle stage, so floor-pinning is impossible
    by construction rather than caught by audit; 18 stage ceilings are retuned against the CEIL
    column. **0 of 24 stages block, against 4 before.** Eleven stages sit below the 80-90% band and
    cannot be raised into it without re-authoring rosters - `floorLevel` is not a lever, and CEIL is
    bounded above by FLOOR.
  - **The economy re-model.** `RunCampaignBalancePass` derived session count, idle income and the
    gold sink per ATTEMPT, when an attempt was one battle and a stage is now 144. It simulated the
    whole campaign in 54 battles and finished at rank 9 with a hero cap of 44; it now walks the real
    ladder through the game's own progression API - 3594 battles, 96 sittings, 49 days, rank 14,
    hero cap 64. The late-chapter "underpowered player" symptom was the instrument: 2-10 through
    2-12 read x0.88/x0.87/x0.85 before and x1.79/x1.70/x1.58 after, with no content changed.
    `CheckEconomyRespondsToIncome` asserts the model responds to its own input.
  - **One claim did not survive the merge, and it is called out where it appears below.** The
    retune branch recorded that it left the economy column alone because the economy model was
    "still one-session-per-attempt and wrong for a 144-battle stage". That was true when written.
    The re-model is precisely that fix and landed alongside it, so the reasoning is preserved as
    the diagnostic it was, not as current status.
  - **Still open:** whether the corrected economy ratios need content tuning at all. They are a
    different set of numbers from the ones any earlier entry argues about.

- **Task 13 (campaign retune): DONE, on `main`.** The 2026-08-29 status line below still says it
  is not; that sentence is stale, task 13's own entry is right.
- **Open: `fix/balance-instrument-ladder`** - unmerged, CI green. Closes the last floor-pinned
  instrument and re-runs the retune against what it now measures. Three parts: the GEARED CURVE
  reports FLOOR/CEIL/BLOCK at 200 samples instead of one number describing battle 1 of 144;
  `BuildBattleRequest` refuses any unresolved multi-battle stage, so floor-pinning is impossible
  by construction rather than caught by audit; and 18 stage ceilings are retuned against the
  CEIL column. **0 of 24 stages block, against 4 before.** Eleven stages sit below the 80-90%
  band and cannot be raised into it without re-authoring rosters - `floorLevel` is not a lever,
  and CEIL is bounded above by FLOOR. The economy column was investigated and left byte-identical
  to `main`: 2.5x gold moves it 3 points and 3x XP moves it none, because the economy model is
  still one-session-per-attempt and wrong for a 144-battle stage. See task 13's entry.
- **Open: `retune/ch2-late-stages`** - unmerged. One commit changing 6 lines of `campaign.json`
  and 6 of `enemies.json`: a first pass at the `ch2_12` boss defect recorded under task 13. Its
  own message says "task 14", which is the OLD numbering for the retune, i.e. today's task 13.
- **Parked: `wip/battlefield-bg`** - unmerged. One edited `ashen.png` and a note that it needs
  more work.
- **Known gap: the Campaign chip row does not fit 12 chapters.** `CampaignScreen.BuildChipRow`
  sizes the row at 1600px wide (`UIBuilder.Box(_chipRow, 0f, ..., 1600f, 84f, ...)`) and
  `RefreshChips` gives every chip `UIBuilder.Sized(chip, 300f, 84f)` inside an `HStack` with 16px
  spacing. That is `12 * 300 + 11 * 16` = 3776px for a full 12-chapter roster against 1600px of
  row: it fits five chapters exactly (`5 * 300 + 4 * 16` = 1564) and overflows at six. Not a
  problem today with two chapters shipped, and not fixed here - it needs either a scrolling row or
  narrower chips, which is a design call rather than a number to nudge.
- **Hero portraits: DONE, on `main`.** Framed portrait art shipped for the five Ember heroes:
  Kael, Marrow, Seraphine, Veska, Orren. Observable in `Resources/Sprites/Portraits`, which holds
  exactly those five files and no others. It adds a SECOND
  portrait convention rather than replacing the first, and the folder is what distinguishes them:
  `Resources/Sprites/Portraits/{heroId}.png` is a finished badge that carries its own gold frame,
  while the older `Resources/Sprites/Units/portrait_{heroId}.png` is a frameless crop that
  `Widgets.Portrait` grounds itself with a disc, glows and an inset. Both are resolved in
  `Widgets.cs`; dropping a file into either folder is what decides how a hero renders, so there is
  no flag in `heroes.json` to keep in sync.
  - **Framed art skips the aspect ring, by design.** It also skips the disc and the 14.65% inset.
    The art already has a frame and four cardinal spikes that reach the edge of the image, so a
    game-drawn ring around it would sit immediately outside a gold border and clip the spikes. The
    cost is real and accepted rather than overlooked: these five lose the aspect-colour read every
    other badge carries. All five are Ember, so the ring was telling the player nothing the art
    does not. **Revisit this if a non-Ember hero is ever delivered framed.**
  - **Still sigil-only, pending art:** Orrin Kaal, Kalyx Emberwright, Wren Ilyra, Ryn Solvarr,
    Maeve Corrin, Nyx Serrow. They render an aspect-tinted sigil disc with the aspect ring, exactly
    as before - the fallback is untouched and is what any hero without a portrait file gets.
  - The five superseded 256px `portrait_*` files were deleted with it, which leaves
    `Sprites/Units/portrait_*` with **no files at all**. The frameless tier is still live code and
    still the right home for any future frameless delivery, but note that `Tools/make_portraits.py`
    now writes into a path that framed art shadows: re-running it would regenerate five files that
    nothing would display.
- **Superseded:** one commit carrying two branch names, `feat/sprite-sheet-poc` and
  `fix/battlevfx-blend-one-minus-src-alpha`. Its `SpriteSheetPlayer` lost to the shipped
  `SpriteAnimator`, and its "blend fix" turned out to be comment-only - main's blend value was
  never wrong. Nothing in it is worth landing.
- **Do not merge:** `feat/sprite-sheet-player` - 244 MB of raw source sheets under
  `Assets/_Incoming/`, which is now gitignored. That branch is the only copy of those sheets;
  keep it, do not land it.

Supersedes the previous version, which predates DESIGN-ASPECTS, DESIGN-WORLD, DESIGN-COMBAT-STATS
and DESIGN-ART-BIBLE. Single ordered plan for the whole project, reconciled against those four plus
DESIGN-RELICS and the surviving originals (FIRST-TEN-MINUTES, IDLE-PROGRESSION, REPLAY-ECONOMY). No
new design here, sequencing only.

**Status, as of tonight's audit (2026-08-29).** Tasks **1 through 12, and now 14, are done on
`main`; 13 is not** — verified against the actual code and content and against a live
`Tools/build-check.ps1` run tonight (compiles clean, `RESULT: all checks passed`, re-run again
just now after task 14 landed, still all-clear), not against this document's own prior claims —
several of those were found stale and are corrected in place below. Task 14, new since the last
pass through this document, is the events engine and its two starter events — content-driven,
not a special case, and folded into the numbered list below rather than tracked separately the
way Chapter 2 and the art pipeline are, per instruction. See task 14 for the full account,
including one thing worth flagging up front: `events.json`'s two windows are real calendar dates
already sitting in the file, not sentinel placeholders, but the file's own comment still calls
them first-pass and un-tuned — they are not a confirmed announcement schedule, and nobody should
treat them as one without a deliberate decision to do so.
`build-order-7-effect-hit-resist` and `sprite-path-orrin` were merged and green;
`build-order-8-team-size-5` and `build-order-9-landscape` merged together with task 10's
`campaign.json` refill riding on `build-order-9-landscape` rather than its own branch, exactly as
this document called for. `build-order-1-3` (task 1, the starting purse) also landed. Task 12 was
split into 12a and 12b in a prior revision of this document; both are now done. **12b was wrongly
marked "written, not built" until tonight** — that was stale by the time it was written: `RelicGrade`
exists (`Enums.cs:111`), `relics.json` carries a `grade` field on all ten relics, and a live harness
run passes 51 dedicated RELICS checks. It has also moved again since DESIGN-RELICS was last
reconciled with the code: the two grades were renamed **Legendary/Mythic** (`7819bf2`, `dff5f22`),
and the two Mythic (formerly Legendary) relics were promoted to also carry a stat block on top of
their hook. See task 12b below for the full account; DESIGN-RELICS.md still describes the
superseded Common/Legendary naming and has not been re-reconciled against this rename — that is a
separate, smaller doc debt this pass does not fix. Task 13, the retune, **has since been done** (2026-09-01,
`retune/task-13-campaign-balance`; the sentence that stood here said it was not, and was true when
written). A small, unmerged first pass exists on branch
`retune/ch2-late-stages` (one commit, `a91a2f6`, touching only the Tideturned boss and three late
chapter-2 stages) — not on `main`, so it does not change task 13's status, but it is active work
worth knowing about. Chapter 2 shipped (12 more stages, `f1f5b71`), entirely outside this document's
numbered order until a prior audit added it — see the chapter 2 entry under "Already done." CI runs
`build-check.ps1` on every push and pull request (`.github/workflows/build-check.yml`, confirmed
present), so status claims below about compiling or running are claims about an actual run, not a
reading of a diff, wherever a branch has actually been pushed through it.

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

**Audit stamp, 2026-09-01.** All seventeen numbered tasks and the four systems below were checked
against code rather than against this document's own prior claims. The result was that this
section was already accurate: fifteen of seventeen tasks were done as claimed, task 6 is correctly
recorded as superseded-and-never-built, and task 13 was correctly recorded as outstanding at the
time (it has since been done - see task 13). Two
things were corrected, both below — the Umbra death-spread was described as living on an unmerged
branch when it is on `main`, and task 13 had no explicit status marker of its own. Nothing else
needed changing.

The audit also confirmed, directly rather than by inference, that four systems this document
describes as built do have working screens wired into `ScreenRouter` (`ScreenRouter.cs:84-94`
creates all eleven), because "the engine exists" and "a player can reach it" have drifted apart on
this project before:

- **Relic shop** — `RelicShopScreen.cs` (197 lines). Reads `Session.RelicPrice`, calls
  `Session.BuyRelic`. Both grades purchasable.
- **Deep Recital** — `RecitalScreen.cs` (210 lines). Shows current and highest floor, the
  floor's generated Aspect and level, and Echo affordability via `PeekRecitalEchoes`; the fight
  button routes through `Battle.SetRecitalFloor` into a real battle, and `GameSession.
  GrantRecitalResult` pays it out. A complete loop, not a placeholder.
- **Inventory** — `InventoryScreen.cs` (541 lines). `AssignRelic`/`UnassignRelic` and
  `EquipArmor`/`UnequipArmor`. Carries both the relic and the armor halves.
- **Forge** — `ForgeScreen.cs` (396 lines). `SalvageArmor`, `RerollArmor` and `CombineArmor`
  with their three cost queries. This is task 16/17's armor Forge, not task 6's dead
  idle-income mechanic — see task 6 for why the word moved.

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
- DESIGN-RELICS, DESIGN-ASPECTS, DESIGN-WORLD, DESIGN-COMBAT-STATS, DESIGN-ART-BIBLE,
  DESIGN-HOOK-CONFLICT, DESIGN-RECOLLECTION. All committed to `main`, all decided. DESIGN-RELICS
  is now fully implemented, both halves: task 12a (the relic/aspect hook resolution and the
  original four-relic implementation) and task 12b (the grade split). ⚠️ Corrected: a prior
  revision of this document called 12b "design-only, not yet implemented." Verified tonight against
  a live harness run, it is built — see task 12b below. One thing DESIGN-RELICS.md itself has not
  caught up to: the grade names it documents (Common/Legendary) were renamed to Legendary/Mythic
  after it was last reconciled with the code (`7819bf2`); that document is stale on naming, not on
  substance. The rest of the list above is still design-only.
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
    DESIGN-ASPECTS, not Umbra). The team-size-5 half — the mark spreading on the
    marked unit's death — was left open here and is **now built and merged to `main`** (a prior
    revision of this document called it "built on `feat/aspect-identities` (unmerged)"; corrected
    by the 2026-09-01 audit, which found `BattleEventType.StatusSpread` at `BattleEvent.cs:104`
    and its emit at `CombatSimulation.cs:1464` on `main`, and no `feat/aspect-identities` branch
    left anywhere in the repo): the dying unit's Umbra-sourced harmful statuses jump to the
    highest-attack living unit on its own side, carrying duration and magnitude unchanged, and
    chain on each further death. Uncapped on purpose — a hop costs a death and grants nothing new, unlike Gale's
    re-entry. Emits a distinct `StatusSpread` event rather than a second `StatusApplied`, so
    Debrief's status clause does not count a jump as an Umbra unit landing a debuff it never cast.
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
- **Chapter 2: The Wandering Coast.** 12 stages (`ch2_1`–`ch2_12`), Tide-themed per DESIGN-WORLD's
  "single-aspect chapter, not single-aspect roster" rule, shipped in `f1f5b71`
  ("build-chapter-2: content authoring pass"). Two new enemies in `enemies.json`: `riptide_channeler`
  (Tide elite) and `tideturned` (chapter 2 boss — `Backwash`, an `OnDamaged` passive, and
  `Undertow`, an Active built on a new `EffectKind`). `campaign.json` now carries **24 stages
  total**, not twelve; chapter 2 mirrors chapter 1's enemy-count ramp exactly (3/3/3/3, 4/4/4/4,
  5/5/5/5). Numbers are provisional by the shipping commit's own admission ("provisional economy
  numbers throughout") — chapter 2 has not been through the retune (task 13) any more than chapter
  1 has. This entry did not previously exist anywhere in this document; the whole chapter landed
  outside the numbered order.
  - **`EffectKind.SwapPositions`**, added for Undertow (`c597e29`, "implement SwapPositions
    EffectKind for Tideturned Undertow") and **implemented, not a stub**:
    `CombatSimulation.cs:652`. Picks a second living unit on the same side as the resolved target
    and swaps their `Slot`. Reshuffles who `FrontEnemy` resolves to; turn order is speed-sorted,
    not slot-sorted, so initiative is unaffected. Emits `BattleEventType.PositionSwapped`.
- **Events engine, and two starter events (The Recollection Rite, The Ledger's Week).** Task 14,
  below - a generic, content-driven engine (`events.json`, an `EventActivityKind` enum
  `GameSession.RecordEventActivity` dispatches on rather than switches over), not two hardcoded
  special cases. Save version 5 to 6 for `PlayerProfile.EventProgress`. See task 14 for the full
  evidentiary account, including the two starter events' actual `events.json` windows and the
  DevHarness EVENTS section (46 checks) that proves the activity hooks, the isolation between
  events, and the milestone-versus-currency claim semantics.

## `build-order-1-3` landed. This section is history, not status

Kept only so the correction is on the record. This section used to claim the branch was "local
only, never pushed," blocking task 3 and the final retune. That was wrong by the time of tonight's
audit: it was pushed and merged (`c5f54ce`), `startingGold`/`startingShards` are live on `main`
today, and task 1 above is marked done. Nothing is actually blocked on a push. See task 1 for the
current numbers, including the later `startingShards` retune to 120.

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
  of building the procedural body. ⚠️ **No longer wired to exactly one hero.** That was true when
  this was written, as a pipeline test on Orrin Kaal alone; verified tonight, `heroes.json` now
  carries a `"sprite"` field on all **7 of 7 heroes** (Kael the Ashbound, added since the last
  count, carries one too — see his own entry below), and `enemies.json` on **3** of its 10 enemies
  (`hollow_husk`, `grave_moth`, `sundered_colossus`) — every file named has a matching PNG
  committed under `Assets/Resources/Sprites/Units/`. The remaining 7 enemies stay procedural — with
  one flagged exception: `campaign.json`'s own comment on chapter 2 says `riptide_channeler` and
  `tideturned` "have no sprite art" and "render as invisible units," which does not match how every
  other spriteless enemy (including chapter 1's own `tidecaller_wretch`, same "wisp" silhouette)
  falls back to the procedural rig. That claim is unverified either way without opening the editor
  — recorded here because it is the content's own comment, not a guess added by this audit.

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
environment ids chapter 1 authors are `ashen`, `ember` and `hollow`, four stages each (chapter 2
adds `coast`, `tidal` and `deep`, four stages each, and has no painting for any of them). ⚠️
**`ember.png` and `hollow.png` are no longer missing.** All three chapter 1 backgrounds are
committed under `Assets/Resources/Backgrounds/` (`ashen.png`, `ember.png`, `hollow.png`), plus an
undocumented `hub.png` this section never mentioned. Chapter 2's three environments still fall
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

**A third loader arrived after this, for animated sprite-sheet clips - also outside the original
three-part plan, following the same convention on purpose.** `SpriteAnimator`
(`Assets/Scripts/Battle/SpriteAnimator.cs`) plays a per-unit set of clips - idle (looped), basic
attack, skill, hit reaction, death - by writing the active frame's UV rect into the same billboard
quad the static-sprite loader builds; `UnitRig.BuildSprite` takes the animated path only when a
unit's `{spriteId}_idle` clip exists, so every unit without one takes the exact static-sprite path,
unchanged. Two authored fields ride on the clip's own JSON sidecar rather than in `heroes.json`
(animation timing, not gameplay data): `ImpactFraction` (where in the clip the strike connects,
syncing the lunge and the Damage BattleEvent to that point instead of the clip's start or end) and
`PlaybackSpeed` (a per-clip speed multiplier, since a raw 30fps export runs far slower than this
game's attack pace). Two new `VisualDef` fields cover art the static-sprite loader's assumptions
don't fit: `SpriteFootFraction` (empty margin below the feet, which `spriteBodyFraction` alone
doesn't cover) and `SpriteMirror` (for art authored facing the opposite of what the rig's turn
expects). Merged to `main` from `claude/kael-hero-animations-se66qk`. `[U]` - and actually
Play-mode verified this time, on Kael, not left as the open question the static-sprite and
background loaders above still are.

Kael the Ashbound (`kael_the_ashbound`) shipped alongside this as the roster's **7th hero** and
the pipeline's first real animated hero, not a placeholder - Ember aspect, 5 stars, Breaker role,
`startingHero: false`. His kit is deliberately simplified to what `CombatSimulation` actually
supports today: Heavy Cleave (single-target Damage, 110% power) and Breaking Point (`AllEnemies`
Damage, 220% power, 100 energy). **Not built, and not scoped or sequenced as a task yet:** his full
designed kit's Ruin stacking resource, the HP-conditional bonus on Heavy Cleave, Breaking Point's
execute-below-25% bonus, both passives, and all three talents - none of that exists in code or
content today, and none of it is assumed by anything above.

## Undecided, needs a human

**Opening Gambit — explicitly cut.** A mandatory per-battle choice (one pre-battle decision,
every fight) is structurally incompatible with idle/auto-battle: the game resolves fights
unattended, and there is no moment to surface a choice. Reframings considered (per-stage
standing loadout, pre-stage modifier selection) were not judged worth building at this stage
of the prototype. This was a deliberate cut, not an oversight — do not re-add it without
revisiting whether the core loop has changed enough to make active pre-battle decisions viable.

**Smaller forks, not blocking yet.** Umbra at team size 5: DESIGN-ASPECTS flags it as diluted
(a third of the enemy line removed at team size 3, a fifth at 5) and offers a spread-on-death rule
or accepting Umbra as a boss-stage specialist. Decide when task 12 reaches Umbra, not before.
DESIGN-WORLD's chapter 3 (and optionally chapter 5) contested-region proposal: moot until chapters
3 to 6 exist, which they don't yet. ⚠️ Corrected: chapter 2 shipped (see "Already done" above),
so `campaign.json` is no longer chapter-1-only — the proposal is still moot, just one chapter
closer.

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
effects, not targeting, so the relic is compatible with the Lumen identity. Task 12 is done:
`claude/relics-rework` merged to `main` (`f3d4936`), with relic progression shipped on top of it.
See task 12 below for the commit list, and DESIGN-HOOK-RESOLUTION for the full account.

### Relics move to a two-tier model. Decided 2026-08-26, and a deliberate reversal

⚠️ **The grade names below (Common/Legendary) are what was decided on 2026-08-26 and what
DESIGN-RELICS still documents. They are no longer what's shipped.** `relics.json` and the code
were renamed a second time, to **Legendary/Mythic** (`7819bf2`, "Rename relic grades to
Legendary/Mythic and promote two relics to Mythic", plus `dff5f22`) — what this section calls
"Common" is now called Legendary in the content and the code, and what this section calls
"Legendary" is now called Mythic and additionally carries a stat block on top of its hook. Nothing
about the mechanics below changed in that rename except that promotion; only the labels moved. See
task 12b for the shipped state, verified against a live harness run. This section is kept as
written because it is the record of the decision and its reasoning, not a description of current
naming.

This replaces DESIGN-RELICS' founding standard — *"A relic changes a **rule** in the effect
pipeline. No relic contains an attack percentage."* That standard is withdrawn knowingly, not
drifted away from. Relics are now two grades (see the naming note below; the axis is not
called "tier").

**Common** (now **Legendary** in the shipped content and code — see the ⚠️ above). Flat and
percentage stat relics — Attack%, HP%, Crit% and their like. Simple, numerous, easy to balance, and
needing no simulation hooks at all: they live entirely in `StatMath` / `UnitStats` and never reach
`CombatSimulation`. This grade exists to solve the two things the rule-only standard did not —
legibility for a player who does not read the effect pipeline, and enough distinct drops for a pull
to feel like it paid out.

**Legendary, and rare** (now **Mythic** in the shipped content and code — see the ⚠️ above).
Rule-breaking relics that alter simulation behaviour instead of adding a
number — the same *kind* of thing an aspect identity is, but never flavoured or paired to a
specific aspect. That last clause reaffirms the existing rule rather than bending it: a relic is
equipment on a hero, not a second way to be Ember. The one-slot-per-hero rule and the
state-it-as-one-sentence presentation rule both survive the reversal. Only the no-percentage
clause is gone, and only for the common (now legendary-named) grade — the rule-breaking (now
mythic-named) grade was later promoted to also carry a stat block (see task 12b), so as shipped the
no-percentage guarantee applies to that grade's `Rule` text specifically, not to the relic as a
whole.

**Hook contention is unchanged, and the resolution above is the tiebreak.** Aspects own the four
previously contested hooks by default: Tide's `AddEnergy` overflow redirect, Verdant's
`ApplyHealValue` overflow banking, Ember's Burn stacking, and Orrin relabelled to Lumen. A
legendary relic that needs simulation access must find a distinct hook, or justify one, and may
not compete for an aspect's. Common relics never enter that argument at all, which is a large part
of why the grade is worth having.

**Task 12 is not blocked by this and does not reopen.** ⚠️ One correction against the decision as
handed down: it unblocks task 12 to "build Aspects as specified", but Aspects are already built.
All six identities are live in `CombatSimulation.cs` today — Gale (`TryGaleReentry` at `:268`,
`TryGaleSlowShift` at `:316`), Ember's Burn stacking (`AddStack` at `:599`), Verdant's heal banking
(`AddOrRefresh(StatusKind.Regen...)` at `:902`), Tide's energy redirect (`_inRedirect` guard at
`:946`), Lumen's interception (`FindTargetAnchor` called at `:1226`, defined at `:1343`) and Umbra
(`IsUmbraSourced` at `:1364`). Line numbers drift as later commits (chiefly the relic hooks) add
code above them; re-verified tonight against the current file, not copied from a prior pass.
Nothing in the two-tier reversal touches any of them, so the intended effect holds either way:
Aspects stand as specified and stay built.

**DESIGN-RELICS has been rewritten to this model. Done, 2026-08-26.** This entry is the decision;
DESIGN-RELICS is the specification, and it now carries the grade split, a starting set of eight
common and two legendary relics (explicitly a starting set, unbalanced by admission), the
arithmetic rules, both entry points, and the presentation split. The ⚠️ banner that stood at the
top of it while it was stale is gone. ⚠️ **Since then, DESIGN-RELICS has fallen behind the code
again, on naming only.** The grades it documents as Common/Legendary shipped, then were renamed to
Legendary/Mythic (`7819bf2`) with the former-Legendary grade promoted to also carry a stat block —
DESIGN-RELICS was not updated for either change. This document's task 12b entry below describes the
current, shipped naming; DESIGN-RELICS.md is a separate cleanup this audit did not do.

**The axis is called "grade", not "tier" and not "rarity".** Both obvious words were already taken:
"tier" is a relic's upgrade level (DESIGN-RELIC-PROGRESSION §2, `balance.json`'s `relicMaxTier`),
and "rarity" is a hero's star rarity (`StatMath.Build`'s `rarityMultiplier`). So: common grade,
legendary grade, `RelicGrade { Common, Legendary }` at the time of this decision — see the ⚠️ above
for what it's actually called today. Grade and tier are orthogonal — a common relic
still upgrades tier 1 to tier 2 like any other. Do not write "tier 2 relic" for "legendary relic".
The heading above keeps its original wording because two documents cite it by name; read
"two-tier" there as "two-grade".

**Three code findings came out of that rewrite, and two of them moved the design:**

- **The status landing gate is no longer `_rng.Chance(effect.Chance)`.** Effect Hit and Effect
  Resist shipped; it is now `_rng.Chance(landing)` at `CombatSimulation.cs:575` (`:565` when this
  was written; line numbers drift as later commits add code above them), with `landing` built by
  `ResolveLandingChance` (`:59`) and further reduced by Control Immunity for Silence and Stun. It
  was unclaimed by any relic or aspect at the time this was written; it has since been claimed —
  The Unbroken Ledger hooks it (`ApplyLedger`, `CombatSimulation.cs:1060`, called at `:553`), by
  design, not by collision — it rebuilds `landing` via `ResolveLandingChance` with the target's
  Effect Resist forced to zero, rather than hooking the raw chance.
- **`PassiveTrigger.OnDamaged` is fired but is not unused.** Dispatched at `:853` (`:843` when this
  was written), and already consumed by `tideturned_passive` ("Backwash") in `enemies.json`. Since
  built to coexist with it, not just designed to: Mirrored Wound's `ApplyMirroredWound`
  (`CombatSimulation.cs:1093`) is called at `:862`, after Backwash, so a passive that heals or
  shields its owner resolves before the reflect can undo it.
- **Block is already implemented**, and not as DESIGN-COMBAT-STATS rejected it: full negation of
  the hit, with `PassiveTrigger.OnBlock` fired at `CombatSimulation.cs:726` (the RNG draw is at
  `:724`), which is the shape that document itself said would earn its place. Inert at 0 on every
  unit today; the shipped relic set does not touch it either — see task 12b.

**DESIGN-COMBAT-STATS.md still carries a banner, now saying something different.** Its Block
rejection is superseded twice — by the code above, and by the reversal recorded here, since half
its argument was that Block is "precisely the stat the constraint forbids" and that constraint is
gone. The banner records the open question (should Block be a common-grade relic stat?) with the
case both ways and **does not decide it**; DESIGN-RELICS' starting common set deliberately excludes
Block. The RNG-cost half of the original argument still stands on its own merits. The Effect Hit /
Effect Resist half of that document shipped and is accurate.

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
2. **Resolved. `Debrief.AspectFactorThreshold` has already moved.** It reads 1.01f today
   (`Debrief.cs` line 64), matching the narrowed wheel (`AspectChart.Advantage` 1.10,
   `AspectChart.Disadvantage` 0.92, `Enums.cs` lines 139-140), not the stale 1.2f this item used
   to warn about. `AspectDominantShare` is likewise already 0.35f (`Debrief.cs` line 54). No
   further action needed; kept here for the record rather than deleted.
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
6. **Content is `Resources.Load`, baked in at build time - there is no remote/live-ops channel.**
   Came up while building task 14 (events) and is written down here so the next person planning an
   event finds out going in, not by discovering it mid-plan. Confirmed tonight: `ContentService.cs`
   reads every content file - `heroes.json`, `enemies.json`, `campaign.json`, `balance.json`,
   `relics.json`, `events.json` - through `Resources.Load<TextAsset>` (`ReadText`/`ReadTextOptional`,
   `ContentService.cs:36/42`), and nothing under `Assets/Scripts` calls `UnityWebRequest` or any
   remote-config API - grepped for both, zero matches. A `Resources` folder is compiled straight
   into the build; scheduling or changing a live event therefore means shipping a new app build and
   getting it through platform review, not pushing a JSON update to players already on an older
   build. `events.json`'s own two windows (see task 14) already assume this: they are set weeks out
   from tonight, not tomorrow, which only makes sense if whoever set them already knew a build has
   to ship and reach players before the window opens. Nothing about this is a defect - it is simply
   how far a `Resources`-only content pipeline reaches, and it bounds what "schedule an event" can
   mean until a remote-config or CDN-fetched content path exists, which nothing today provides.

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

`startingGold` 400 · `startingShards` 120 (retuned from 40 by `af13bb6`, one guaranteed pity
summon on day 0 — see task 1) · `idleCapHours` 10 · `repeatFullClearsPerDay` 3 ·
`repeatRewardFloor` 0.0 · `dailyResetHourUtc` 4 · Forge multiplier 0.9 (re-check it's still a real
choice against Collect, not always correct) · `maxTeamSize` 5 · `AspectChart.Advantage` 1.10 ·
`AspectChart.Disadvantage` 0.92 · `Debrief.AspectDominantShare` 0.35 ·
`Debrief.AspectFactorThreshold` 1.01. Unchanged: `summonCost` 120, the level curve, duplicate
conversion, summon weights.

## The build order

1. **Push `build-order-1-3`**, land the starting purse change it already contains. `[H]`
   **Done. On `main`.** ⚠️ Corrects a stale claim carried by this document until tonight's audit:
   the "Blocked on a push, not lost" framing this task used to have was wrong, and has been
   removed (see below) — the branch was pushed and merged (`c5f54ce`, "balance: retune starting
   purse"). `startingGold` is 400 in `balance.json`. `startingShards` is **120**, not the 40 this
   document quoted elsewhere: a later, deliberate commit (`af13bb6`, "startingShards 40 -> 120,
   one guaranteed pity summon on day 0") retuned it again, tying it to task 3's pity guarantee so
   the day-0 free pull can actually afford to hit it. See "Numbers already decided" below, also
   corrected.
   Blocked by: nothing remaining. Blocks: 3, and the final retune absorbs its win-rate shift.

2. **`idleCapHours` 8 to 10.** JSON only. `[H]`
   **Done.** `balance.json`: `idleCapHours` is 10.
   Blocked by: nothing. Blocks: nothing hard, bundle with 4 or 5 if convenient.

3. **Summon pity**: pulls 1 to 3 guaranteed from unowned heroes, plus enemy Aspects and a slot-1
   label on the campaign card. `[H+U]`
   **Done.** `GameSession.PityGuaranteedSummons` is 3 (`GameSession.cs:593`); `TotalSummons` is
   tracked and saved (`PlayerProfile.cs:125`, `SaveSerializer.cs:80/166`), and `RollUnownedHero`
   gates on it (`GameSession.cs:560`).
   Blocked by: 1, the guarantee fires on the day-0 free pull and a wrong purse wastes it. Both are
   done now, in the right order. Blocks: nothing hard.

4. **Idle rate resolved from best cleared stage.** Retire the four flat idle constants, add two,
   resolve the `StageDef` at `StagesCleared - 1`. `[H]`
   **Done.** `StatMath.IdleGoldPerSecond` / `IdleShardsPerSecond` resolve off
   `Content.GetStageByGlobalIndex(Profile.StagesCleared - 1)`, read in `GameSession.cs:500-503`.
   Blocked by: nothing. Blocks: 5.

5. **Repeat cap** (`dailyResetUnix`, `stageClearsToday`, cleared stages only, save version 1 to 2).
   `[H+U]` for the daily-reset UI feedback.
   **Done.** `SaveSerializer.cs` writes and reads both `dailyResetUnix` (line 78/164) and
   `stageClearsToday` (line 128/233), and `CurrentVersion` documents the 3 -> 4 bump for exactly
   this field pair (line 30, "the repeat cap's dailyResetUnix and stageClearsToday map
   (DESIGN-REPLAY-ECONOMY)"). `balance.json` carries `repeatFullClearsPerDay` (3). Note the save
   version has moved further since - `CurrentVersion` is 5 today, not 2; task 8's team-size bump
   (2) and this task's repeat-cap bump (4) are two separate version increments, not the same one,
   with a relic-inventory bump (3, then 5) landing between and after them. See `SaveSerializer.cs`
   lines 15-56 for the full version history.
   Blocked by: 4, reversed order leaves a stuck player no grind path. Blocks: 6.

6. **Collect vs Forge.** `[H+U]`
   ⚠️ **Superseded, not built.** This was an idle-income discount mechanic - "Forge" here meant
   collecting idle Gold/Shards at a reduced instant rate versus waiting for the full amount. It
   never got built, and nothing downstream ever ended up blocked on it in practice. The word
   itself has since been reclaimed: task 16's Armor system named its reroll-and-forge Anvil sink
   "The Forge" once this task's own use of the word was confirmed dead, and DESIGN-ARMOR.md
   documents that screen now. If an idle-income discount mechanic is ever built, it needs a
   different name - "Forge" belongs to the armor screen from here on.
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
   Blocks: 9, 10, 11, 12, 13 - all now unblocked on task 8's side.

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
   Blocks: 10, 11, 12, 13 - all now unblocked on task 9's side. Two things are still genuinely
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
    where the composition demanded it; nothing here is final, task 13 is still the real retune.
    Blocks: 11, 12a, 13 - all now unblocked on task 10's side.

11. **Effect Hit / Effect Resist, phase B.** Author values on six heroes and eight enemies, add
    `StatusResisted`, wire it into the battle screen and Debrief, add the direction check to the
    harness. `[H]` for the logic, `[U]` for the new event's presentation, which renders on the
    battle screen and so wants orientation settled first.
    **Done.** `heroes.json` carries non-zero `effectHit`/`effectResist` on all six heroes and
    `enemies.json` carries them on all ten enemies (more than the eight the task line named, not
    fewer). `StatusResisted` is emitted, not just declared: `CombatSimulation.cs` line 573
    (`Emit(BattleEventType.StatusResisted, ...)`), and read on the presentation side in
    `BattleScreen.cs` line 729, `Debrief.cs` line 210 (the resisted-status clause), and
    `CombatLogger.cs` line 135.
    Blocked by: nothing remaining. Blocks: 13.

12a. **Resolve the relic/aspect hook conflict**, then build whichever of Relics and Aspects follows
    from that. `[D]` for the decision, `[H+U]` for the code after, which renders in hero detail and
    on the battle screen.
    **Done.** `claude/relics-rework` merged to `main` (`f3d4936`), and relic progression shipped on
    top of it: the relic data model and the four surviving relics (`75f430f`), the relic equipped
    in the save at version 3 (`b5ae037`), relics resolved in the effect pipeline (`ca27105`),
    the owned-relics inventory model with `RelicId` -> `EquippedRelicId` (`dddde91`), the save
    version 4 -> 5 migration for it (`f1284a2`), Relic Dust and tier constants in `balance.json`
    (`09bc8fd`), the dust drop plus relic API and harness checks (`6fbe951`), a Relic Dust counter
    in the header (`c6a8d6a`), and the relic shop/inventory screens (`5fee3b0`,
    `Assets/Scripts/UI/Screens/RelicShopScreen.cs`). ⚠️ Stale by task 12b, corrected here:
    `CombatSimulation.cs` no longer resolves relic hooks through `ShapeByRelic` — that method, and
    the three hooks it dispatched for the relics task 12b went on to cut, were removed along with
    them. What ships today calls `ApplyLedger` / `ApplyMirroredWound` directly at each hook's own
    resolution point (see task 12b for exact lines); confirmed absent from `CombatSimulation.cs`
    tonight by search. All of this is on `main` today, not on an unmerged branch.
    Blocked by: nothing remaining. Blocks: 12b, 13.

12b. **The two-grade relic model** — Common (flat/percentage stats, no simulation hooks) and
    Legendary (rule-breaking, aspect-neutral), per "Relics move to a two-tier model" above and
    the DESIGN-RELICS rewrite (`9ed2879`). `[D]` for the decision and the document, `[H+U]` for
    the code.
    **Done.** ⚠️ Corrects a stale claim this document carried until tonight: a prior revision marked
    this "written, not built," citing no `RelicGrade` type and an unchanged `relics.json`. That was
    checked freshly tonight and found wrong — the grade split is built, on `main`, and covered by a
    live harness run (51 checks under "RELICS", all passing). Evidence: `RelicGrade` enum at
    `Assets/Scripts/Core/Data/Enums.cs:111`; `relics.json` carries a `grade` field on all ten
    entries; `StatMath`/`Definitions.cs` (`ApplyRelic`, `RelicDef.CardText()`, `DescribeStats()`)
    resolve the common-style grade before the simulation starts; `RelicHook.BypassEffectResist` and
    `RelicHook.ReflectDamage` resolve inside it (`ApplyLedger` at `CombatSimulation.cs:1060`,
    `ApplyMirroredWound` at `:1093`); `InventoryScreen.cs` and `RelicShopScreen.cs` render a grade
    badge per card. The starting set matches DESIGN-RELICS: 8 stat-bundle relics and The Unbroken
    Ledger / Mirrored Wound, with all six previously-cut relics confirmed absent by name in the
    harness.
    ⚠️ **What's built is not what either DESIGN-RELICS.md or this document's own "Relics move to a
    two-tier model" section above call it.** The grade split shipped as Common/Legendary, exactly
    as decided, then was renamed a second time to **Legendary/Mythic** (`7819bf2`, "Rename relic
    grades to Legendary/Mythic and promote two relics to Mythic", card-text follow-up in `dff5f22`)
    — no design doc records this second rename, and it is not just a label swap: the promoted
    Mythic grade (formerly Legendary) now also carries a `stats` block on top of its hook (The
    Unbroken Ledger gained Speed + Effect Resist; Mirrored Wound gained Effect Hit + Crit Damage),
    so the old "a legendary relic contains no number" guarantee now applies to its `Rule` text
    specifically, not to the relic as a whole. `RelicGrade` is `{ Legendary, Mythic }` today, not
    `{ Common, Legendary }`. `RelicMythicShopPrice` (2400, thirty times `RelicShopPrice`'s 80) prices
    the promoted grade. This document has been updated to reflect the shipped naming; DESIGN-RELICS
    and DESIGN-HOOK-RESOLUTION have not been re-reconciled and still describe Common/Legendary — a
    real, separate doc debt this audit did not close.
    Blocked by: 12a (done). Blocks: nothing hard — the starting sets are explicitly provisional and
    don't need to land before 13, but landing after 13 (which it already has) means the retune
    covers all ten shipped relics, not the four task 12a alone would have left.

13. **Full campaign retune. Once, last.** **Done.** Ran over all 24 stages, both chapters, on
    `retune/task-13-campaign-balance`. Twelve `build-check.ps1` runs against a real compiler, each
    one read before the next change; the final run is green with zero failures. `campaign.json`'s
    header comment no longer says "NOT balanced" because it no longer is.

    **What the baseline actually was.** Every stage read 100% on all three instruments except
    1-12, which read 66.7% on the five-body pin. Not a curve, a wall with one notch in it. The
    enemy line measured 0.50x to 0.94x its stage's `recommendedPower` - chapter 2 sat at 0.55x to
    0.65x for most of its length - which is the arithmetic left over from blocker A multiplying
    every `recommendedPower` by 1.52019 while enemy levels stayed where task 8 put them.

    **Enemy level alone does not work, and this pass proved it rather than assuming it.** The
    first attempt raised every enemy until each stage's line power equalled its own
    `recommendedPower` - chapter 2 reaching level 60. Twenty-one of twenty-four stages still read
    100%. The three that moved did not move gently: 1-12 went 66.7% to 0% and took
    `CheckStageWinnability` down with it, and 2-12 went 100% to 54.2%. That is the whole finding.
    The document's standing line - "enemy levels are task 13's first lever, not its last" - is
    correct about the first half and understated the second: level is not a weak lever, it is an
    inert one until something else is true first.

    **What is actually true first: Lumen eats single-target damage.** Orrin is Lumen, and
    `FindTargetAnchor` gives a living Lumen unit every single-target question aimed at its side.
    Audited against `enemies.json`: `hollow_husk`, `tidecaller_wretch` and `riptide_channeler` all
    open on `FrontEnemy`, `grave_moth` on `LowestHpEnemy`, `cinder_imp` on `RandomEnemy`. Every
    one of those is single-target, so every one of them lands on the tank and gets healed off -
    the harness prints "healer changed outcome: 0 of 24" on stage after stage of the baseline. The
    only damage in the entire enemy roster that Lumen does not absorb is `AllEnemies`, and the
    only enemy carrying it on a BASIC - fires every turn rather than on a 100-energy active - was
    `sundered_colossus`. That is the whole reason 1-12 was the single stage in the game that
    discriminated at all. `a91a2f6` reached the same conclusion independently for the chapter 2
    boss; see below.

    **So difficulty is authored as AoE presence, and level scales it once present.** Chapter 1
    grades on Ember AoE actives, phased in by power: `cinder_imp`'s Sputter (0.70) from 1-2, then
    `ashen_revenant`'s Ash Nova (1.05) from 1-8. Measured sensitivity, one revenant at a time:
    zero revenants reads 100%, one reads 79.2% at level 19 and 58.3% at level 20, two read 62.5%
    at levels 24/22 and 0% at 26/24. A single level on one enemy is worth twenty points of win
    rate at that end of the curve, which is why every number below is a measured point and not an
    extrapolation.

    **Chapter 2 needed a kit change, and this is the explicit call-out that it is one.**
    `riptide_channeler`'s basic was `FrontEnemy` 0.90 with an AttackDown; it is now `AllEnemies`
    0.45 with the same debuff at a lower chance (0.60 to 0.35, since it now lands on five targets
    instead of one). Level alone was tried and failed here twice - channelers at level 60 against
    a line power equal to `recommendedPower` still read 100% - because the whole chapter 2 roster
    is single-target and Lumen was absorbing all of it. The channeler appears in chapter 2 only
    (verified by parsing every stage's roster), so this touches no chapter 1 content. With an
    `AllEnemies` basic, chapter 2 grades on channeler count, one through three, which is a lever
    with enough resolution to author against.

    `tidecaller_wretch` is the other composition finding. Its active heals its whole line and adds
    DefenseUp, which lengthens the fight, which multiplies how many AoE volleys land - so a wretch
    stage tuned to its neighbours' channeler level reads far harder than them. Measured: 2-3 with
    a wretch and a level-38 channeler read 12.5% while 2-4 with a level-42 channeler and no wretch
    read 100%. Both stages that field a wretch (2-3, 2-5) hold the channeler beside it four to six
    levels lower to pay for it.

    **`a91a2f6` was built on, not superseded.** Its Tideturned change is adopted verbatim - HP
    2650 to 4200, attack 248 to 480, basic `FrontEnemy` 1.10 to `AllEnemies` 0.80 - because it is
    the same Lumen-bypass fix this pass arrived at independently, and it was right. Its stage
    levels on 2-9/2-10/2-11 are superseded, since this pass re-derives all 24 stages against a
    curve rather than three stages against a boss. Its own conclusion, that "level tuning alone
    cannot move the economy instrument; this is a kit-design debt for a future content pass", is
    exactly what the channeler change above pays off. The branch stays unmerged; nothing is lost
    by that now.

    **Enemy counts were not touched.** Task 10's 3/3/3/3, 4/4/4/4, 5/5/5/5 ramp is intact on both
    chapters, asserted by the tool that wrote these rosters. `recommendedPower`, the relic set,
    hero kits and team size were not touched either.

    **One thing this pass could not fix, and it is not enemy content.** Late chapter 2's
    `recommendedPower` is not reachable by a real progressing player. The economy instrument's
    simulated account plateaus at roughly 17,900 power by 2-11 against a `recommendedPower` of
    23,500, and 23,600 at 2-12 against 26,000 - hero level cap Lv44 at rank 9 is the binding
    constraint, not content. Two consequences, both visible in the final table. The economy column
    falls to 12.5% and 16.7% on the last two stages while the five-body column reads 62.5% and
    83.3%, because the five-body pin fields a team the campaign's own economy never produces. And
    2-12 reads easier on the pinned instrument than 2-11 does, which is not a tuning error - it is
    `recommendedPower` climbing 23,500 to 26,000 across a stage where the content cannot follow it
    without enemy levels in the seventies. An earlier iteration solved the levels needed to match
    that curve exactly and got level 101 on 2-11. That is a `recommendedPower` problem for whoever
    picks it up, deliberately left alone here per this task's own scope.

    **A second thing this pass could not have known, found 2026-09-04.** Every number in the table
    above was computed by a line wearing NO ARMOR, and nobody noticed for the same reason task 13's
    instrument was broken before it: armor shipped after these instruments were written and nothing
    connected the two. `CheckStageWinnability` and `CheckPinnedFiveBodyCurve` build profiles through
    `CreateNewProfile`, which grants none. `RunCampaignBalancePass` actually EARNS pieces -
    `GrantBattleResult` rolls a drop and `RollArmorDrop` adds it to `OwnedArmor` - but never equipped
    any, and `StatMath.ApplyArmor` reads a hero's EQUIPPED uids, so it was an exact no-op there too.

    The `ARMOR DELTA` section now printed beside the five-body curve is the size of it: **sixteen of
    twenty-four stages go from losable to a flat 100%** once the same line wears median drops. Worst
    are 1-12 (41.7% to 100%), 1-10 and 2-9 (54.2% to 100%) and 2-7 (58.3% to 100%).

    **A retune against that was attempted, six measured passes, and abandoned deliberately.** The
    finding is that no setting of enemy level or composition satisfies both columns:

    - **+1 enemy level** costs the unarmored line **8 to 42 points** of win rate and costs the
      armored line **zero**, on every stage tested. That is the whole problem in one number.
    - **+3 levels** puts chapter 1's unarmored column at 0% while the armored column is still 100%.
      The two never overlap.
    - **+8 levels with AoE swaps** finally moves the armored column (1-12 to 20.8%), but the
      unarmored line has been unwinnable for five levels by then and three checks fail, including
      "a normal player can clear the whole campaign".

    The unarmored line's entire range, 100% to 0%, spans about three enemy levels - and all three
    fit inside the armored line's margin without moving it once. This is consistent with the
    twenty-points-per-level sensitivity measured above; armor simply buys more than the whole band.

    **The root cause is `recommendedPower`, and it is the same problem this entry already flags one
    paragraph up.** `HeroInstance.Stats` is `StatMath.ForHero(Def, Level)` - no relic, no armor - so
    `TeamPower()` is BARE power and `recommendedPower` is a bare-power target. A line pinned to it
    *and* geared is over-geared for its own stage by construction, which is why it reads 100%
    everywhere. Nothing in content can separate the two lines, because gear is a straight power
    increase and no enemy lever hits a geared team harder than an ungeared one.

    Two defects were found on the way and are recorded rather than fixed:
    - **`ch1_12`'s composition is load-bearing for two other checks** - `db.GetStage("ch1_12")` feeds
      the Umbra two-sided rule and `MeasureStageDelta` feeds the Lumen relabel check. Its level is
      adjustable; its enemy list is not.
    - **`ch2_12`'s boss is authored below its own minions** - `tideturned` at level 32 against adds
      at 41 to 47, which is why the throne reads easier than 2-11. Raising it alone to 48 drove the
      stage to 0% on both the unarmored and the economy columns.

    **`recommendedPower` now means something task 13 never measured. Audited 2026-09-07.** The
    Chapter -> Stage -> Wave -> Battle restructure (DESIGN-CAMPAIGN-STRUCTURE.md) landed after this
    entry was written. A stage is no longer one fixed fight; it is a 12x12 ladder interpolating from
    a `floorLevel` to a `ceilingLevel`, 144 battles, and `recommendedPower` is a single number
    attached to the whole range. Task 13 tuned it against ONE encounter, which is now the first
    battle of 144.

    This is not a documentation nicety - it is measurable, and it affected every instrument:

    - **FIXED for two of them, 2026-09-07.** `CheckPinnedFiveBodyCurve` and `RunCampaignBalancePass`
      both used to call `session.BuildBattleRequest` on the raw `StageDef`, whose `Enemies` list is
      by construction the wave 1 / battle 1 roster, so both silently reported the FLOOR battle while
      appearing to report the stage. Both now resolve through `CampaignWaveGenerator.BuildBattle`:
      the pinned curve reports FLOOR, CEIL and the first blocking battle, and the economy pass gates
      a stage clear on the ceiling battle rather than paying out for one won floor.
      `CheckInstrumentsSeeTheWholeCurve` asserts this and fails loudly if either is ever re-pinned.
    - **`RunCampaignBalancePass`'s economy half was wrong by a factor of 144, and is no longer.**
      A single won battle used to call `GrantBattleResult` and break the attempt loop as cleared,
      paying a full stage clear - gold, shards, account XP, first-clear bonus - for 1/144th of the
      work and advancing the simulated player at 144x the real rate. The clear is now gated on the
      ceiling battle, matching the sweep rule the game itself uses. The session count, the idle
      income between sessions and the gold sink were still modelled at one session per attempt
      and were still wrong for a 144-battle stage. That was the separate, larger piece of work,
      and it has since been done - see **The economy half is fixed** below. This sentence is left
      in place, corrected, because the bullet it closes is the audit that identified the defect.

    - **The GEARED CURVE was floor-pinned too, and is not any more. Fixed 2026-09-09.**
      `CheckArmoredCurveDelta` reported through `PinnedLineWinRate`, which fought
      `BuildBattleRequest` on the raw `StageDef` - battle 1 of 144. The proof was in the printed
      output: that table's `ungeared` column and the pinned curve's `FLOOR` column were the same
      measurement and read within a few points of each other on every stage, while the pinned
      curve's `CEIL` column - the same line at battle 144 - read 0% on eight of them.

      Re-pointing needed a decision rather than a fix, because a single number for a 144-battle
      curve has to be a chosen point on it, and which point is chosen sets the difficulty standard
      for the campaign. **The standard chosen is CEIL**, wave 12 battle 12. The reason is that
      defeat retries the battle that was lost rather than the stage, so the ceiling is the only
      reading that answers "can this stage be finished". The column now prints FLOOR, CEIL and
      BLOCK, the same shape as the pinned curve, at 200 samples. `PinnedLineWinRate` became
      `BuildPinnedLine`, which returns the geared session **without fighting anything**: choosing
      the battle is the caller's job now and cannot be defaulted.

    - **Floor-pinning is now impossible by construction, not merely discouraged. 2026-09-09.**
      `StageDef.ResolvedBattleOrdinal` is stamped by `CampaignWaveGenerator.BuildBattle` and by
      nothing else, and `GameSession.BuildBattleRequest` throws on any stage whose `TotalBattles`
      exceeds 1 while that field is still 0. The authored `Enemies` list IS the wave 1 battle 1
      roster, so the old mistake never failed - it returned an ordinary, ordinary-looking result
      for the opening battle - which is the entire reason three instruments made it for months.
      The unresolved case is now loud at the call site that caused it. Single-battle stages
      (Recital floors, Proving matches, prologue steps) are unaffected.

      It paid for itself immediately: on first compile it caught a fourth floor-pinned call site,
      in `CheckAspectEmberVerdant`, that no audit had found. Seventeen harness call sites that were
      genuinely asking for the opening battle now say so through an `Opening(stage)` helper.
      `CheckInstrumentsSeeTheWholeCurve` stays as a backstop.

    **GEARED CURVE after the retune, 200 samples, measured against live code 2026-09-09.**
    FLOOR is wave 1 battle 1, CEIL is wave 12 battle 12, BLOCK is the first battle with no win in
    200 samples. CEIL is the tuned column; FLOOR is reported, not tuned.

    | stage | recPwr | FLOOR | CEIL | BLOCK | ceiling lv |
    |-------|--------|-------|------|-------|------------|
    | 1-1 to 1-5 | 3950 to 6100 | 100% | 100% | none | untouched |
    | 1-6   |  7150 | 100%  | 83%   | none | 21 -> 24 |
    | 1-7   |  7200 | 100%  | 90%   | none | 24 -> 29 |
    | 1-8   |  7450 | 94%   | 82.5% | none | 27 -> 25 |
    | 1-9   |  9100 | 69.5% | 38.5% | none | 32 -> 28 |
    | 1-10  |  9250 | 72%   | 44.5% | none | 27, at minimum span |
    | 1-11  |  9750 | 74%   | 46.5% | none | 32 -> 28 |
    | 1-12  | 10200 | 48.5% | 23.5% | none | 33 -> 23 |
    | 2-1   |  8000 | 100%  | 88.5% | none | 36 -> 42 |
    | 2-2   |  9000 | 97.5% | 89%   | none | 40 -> 38 |
    | 2-3   | 10000 | 99%   | 89.5% | none | 53 -> 42 |
    | 2-4   | 11500 | 87.5% | 72.5% | none | 56 -> 54 |
    | 2-5   | 13000 | 89%   | 73%   | none | 61 -> 57 |
    | 2-6   | 14500 | 73%   | 66%   | none | 57 -> 52 |
    | 2-7   | 16000 | 81%   | 76.5% | none | 62 -> 58 |
    | 2-8   | 17500 | 98%   | 82%   | none | 60 -> 57 |
    | 2-9   | 19500 | 85%   | 75.5% | none | 70 -> 61 |
    | 2-10  | 21500 | 83%   | 76.5% | none | 76 -> 71 |
    | 2-11  | 23500 | 94%   | 81%   | none | 81 -> 77 |
    | 2-12  | 26000 | 88%   | 47.5% | none | 54 -> 51 |

    **What the retune achieved, and what it could not.**

    - **The hard requirement is met: 0 of 24 stages BLOCK, against 4 before** (1-9 at b103, 1-11 at
      b103, 1-12 at b47, 2-3 at b101). Every stage in the campaign is now completable end to end by
      the geared five-body line at its own `recommendedPower`.
    - **Eight stages land in the 80-90% band**: 1-6, 1-7, 1-8, 2-1, 2-2, 2-3, 2-8, 2-11. 1-1
      through 1-5 were left untouched at 100% as instructed.
    - **Eleven stages sit below the band and cannot be raised into it by this lever.** The
      difficulty model is extremely steep at these power levels - one enemy level is worth 15 to 30
      percentage points - and `floorLevel` is not a tuning lever (see the trap below), so a stage's
      CEIL is bounded above by its FLOOR. With minimum span being 1, a stage can only reach 80-90%
      at its ceiling if its floor is around 95% or better. Eleven stages open below that. Raising
      them means moving the floor, which means re-authoring rosters, not editing two integers.
    - **Chapter 2's arc conflicts with the "finale around 65%" guide, and the arc won.** "No
      ordinary stage harder than its own chapter's finale" and "finale around 65%" are not
      simultaneously satisfiable at this granularity: the ordinary stages that cannot be raised
      (2-6 at 66%) already sit at the finale's target. 2-12 is set to CEIL 47.5%, making it
      chapter 2's hardest stage by a clear margin. The arc defect was the named problem; the 65%
      figure was a guide.
    - **Eleven stages are flagged FLOOR UNDER 90%** by the instrument: 1-9, 1-10, 1-11, 1-12, 2-4,
      2-5, 2-6, 2-7, 2-9, 2-10, 2-12. Reported, not tuned, as specified.

    **The economy column was investigated and deliberately not touched here. 2026-09-09.**
    *Superseded by the economy re-model below, which landed alongside this work. Kept because it
    is the diagnostic that motivated that re-model, and because its numbers are the clearest
    statement of what a broken instrument looks like. Every ratio in this subsection is an
    OLD-MODEL read and does not match what the corrected pass prints.*
    Chapter 2's power ratio declines monotonically from x1.52 at 2-1 to x0.82 at 2-11, and the task
    asked for 2-10 through 2-12 specifically. Neither half of the gold/EXP curve is the lever:

    - **2.5x gold on `ch2_5` through `ch2_12` moved the ratio about 3 points** (2-10 x0.86 to
      x0.89, 2-11 x0.82 to x0.85, 2-12 x0.83 unchanged).
    - **3x account XP on the same stages moved it zero points** (2-10 x0.86, 2-11 x0.83, 2-12
      x0.80 - i.e. baseline).
    - The sim is not cap-blocked: `0 of 46 sessions ended CAP-BLOCKED`, peak balance 3402 against
      an ending balance of 13397, so it spends roughly as it earns and is not sitting on unspent
      gold it cannot use.

    An instrument that does not respond to a 2.5x change in its own input is not measuring what the
    lever controls, and the reason is recorded three bullets above: **the economy model's session
    count, idle income and gold sink are all still modelled at one session per attempt and are all
    still wrong for a 144-battle stage.** Scaling content rewards against it would be tuning against
    a known-broken instrument - the same mistake this branch exists to undo, one column over. The
    economy fields in `campaign.json` are byte-identical to `main`. Fixing the model is the
    prerequisite, and it is the separate larger piece of work already named - which was then done.
    See **The economy half is fixed** immediately below.


    **The economy half is fixed. 2026-09-09.**

    The paying-for-1/144th-of-the-work bug above was patched earlier by gating the clear on the
    ceiling battle, but that fix said in its own comment that "the session count, the idle income
    between sessions and the gold sink are all still modelled at one-session-per-attempt and are
    all still wrong for a 144-battle stage". They were. This is that larger piece of work.

    **What the old model actually did, measured before changing anything:** it simulated the entire
    24-stage, 3456-battle campaign in **54 battles and 54 hours of game clock**, because it advanced
    the clock one hour per *attempt* and an attempt was one battle. Twenty of twenty-four stages
    resolved in a single attempt. The simulated player finished the campaign at **rank 9 with a hero
    level cap of 44** against a hard cap of 80 - the instrument was starving the player it was
    measuring, then reporting the starvation as a content problem.

    **The granularity ledger**, written before the rewrite. DESIGN-CAMPAIGN-STRUCTURE decides the
    reward rows: "the repeat cap and idle income stay at Stage granularity while wave and battle
    exist only inside an attempt".

    | quantity | was | is now |
    |---|---|---|
    | elapsed clock | +1h per attempt | **per battle**, from its round count |
    | idle income claim | per attempt | **per session**, capped by `IdleCapHours` |
    | gold spend, summons, armor | per attempt | **per session** |
    | the fight | 1 battle standing for a stage | **per battle**, down the real 144-battle ladder |
    | reward purse | per stage clear | still per stage, unchanged |
    | idle rate per hour | keyed to stages cleared | still per stage, unchanged |
    | repeat cap unit | a stage clear | still per stage, unchanged |
    | wave / battle position | did not exist | only inside an attempt |

    The reward rows were already correct and were deliberately left alone: `RecordBattleWon` moves a
    cursor for 143 of a stage's 144 wins and pays nothing, and the caller grants the purse once on
    the win that finishes the stage. Paying per battle would multiply every stage's income by 144,
    which is exactly what the design rules out.

    **The rewrite drives the game's own progression API** - `ResumePointFor`, `RecordBattleWon`,
    `RecordBattleLost`, `SweepClearedStage` - instead of a hand-rolled proxy for it. That is the
    point: the proxy is what drifted away from the game in the first place, so the instrument now
    breaks loudly if that API changes rather than quietly disagreeing with it. It walks 3594 battles
    across 96 sittings and 49 simulated days, finishing at rank 14 with a hero cap of 64.

    Three assumptions are now named constants with doc comments instead of being implicit: a sitting
    is 40 battles, sittings are 12 hours apart, and a battle costs 8s plus 3.5s per round.

    **Economy power-vs-`recommendedPower`, before and after.** Content was NOT touched - this is the
    same `campaign.json`, measured by a corrected instrument.

    | stage | before | after |  | stage | before | after |
    |---|---|---|---|---|---|---|
    | 1-1 | x1.32 | x2.21 | | 2-1 | x1.62 | x3.34 |
    | 1-4 | x1.41 | x2.54 | | 2-4 | x1.26 | x2.67 |
    | 1-8 | x1.33 | x2.79 | | 2-8 | x0.95 | x2.05 |
    | 1-12 | x1.24 | x2.51 | | 2-9 | x0.94 | x1.91 |
    | | | | | 2-10 | x0.88 | **x1.79** |
    | | | | | 2-11 | x0.87 | **x1.70** |
    | | | | | 2-12 | x0.85 | **x1.58** |

    **The reported symptom was the instrument, not the content.** "A simulated player arrives at the
    late chapter underpowered" was x0.85 to x0.94 on 2-8 through 2-12. Corrected, those stages read
    x1.58 to x2.05. Nobody was arriving underpowered; the model was declining to pay them for 143 of
    every 144 battles they fought. **No content was tuned to achieve this**, deliberately - whether
    the corrected numbers now need tuning is a separate decision.

    **What survives the correction is the SHAPE.** Chapter 2 still declines monotonically, x3.34 at
    2-1 down to x1.58 at 2-12, so `recommendedPower` in chapter 2 still grows faster than the
    modelled player's power does. That slope is the real finding and it is invariant: at 20, 40 and
    80 battles per sitting the late-campaign ratios read x2.31/x2.15/x1.99, x1.79/x1.70/x1.58 and
    x1.51/x1.42/x1.37 - the level moves with the assumed play cadence, the decline does not.
    Read the absolute number as "given a twice-a-day, 40-battle player"; read the slope as a
    property of the content.

    **`CheckEconomyRespondsToIncome` is the guard, and it exists because the old model would have
    failed it.** It multiplies every gold channel on the back half of the campaign by 2.5x, re-runs
    the whole simulation and requires the late-campaign power ratio to move. The old model moved
    0.03 on that input, and moved 0.00 when account XP was tripled; the corrected model moves 0.16.
    The bar is 0.10 - more than three times the broken signature, well clear of the working value.

    It is not set higher because 0.16 is the honest ceiling, for a reason the check now prints
    beside it: at 2.5x gold **49% of sittings end CAP-BLOCKED against 7.3% at 1x**. Gold buys hero
    levels, hero levels stop at `HeroLevelCap`, and only account rank raises that. Half the extra
    gold has nowhere to go. That is the design working as written, and it is also the answer to why
    the previous session's 2.5x-gold and 3x-XP probes both looked inert: gold saturates against the
    level cap, and the old model's XP was so far below the cap's needs that tripling a late-chapter
    fraction of it changed no rank at all.

    The check scales the loaded `StageDef`s in memory and restores them in a `finally`. Nothing is
    written to `campaign.json`, and the silent probe runs are barred from publishing to the
    instrument comparison table so they cannot contaminate what other checks read.

    **Curve shape at five sampled stages, 2026-09-07**, pinned five-body line levelled to each
    stage's `recommendedPower`, 200 samples per point, run against live code:

    | stage | lv floor->ceil | W1B1 | W4B1 | W6B6 | W9B1 | W12B12 |
    |-------|----------------|------|------|------|------|--------|
    | 1-3   | 10 -> 12       | 100% | 100% | 100% | 100% | 100%   |
    | 1-9   | 27 -> 32       |   7% |   0% |   0% |   0% |   0%   |
    | 1-11  | 27 -> 32       |  37% |  15% |   2% |   0% |   0%   |
    | 2-4   | 53 -> 56       |  74% |  56% |  60% |  34% |  25%   |
    | 2-11  | 76 -> 81       |  12% |   8% |   6% |   5% |   0%   |

    Three things follow, and none of them are tuning opinions:

    1. **On an easy stage the curve does nothing.** 1-3 spans two enemy levels across 144 battles and
       reads 100% at every point. A +2 span is below the resolution of the difficulty model.
    2. **Three of the five sampled stages reach 0% before the ceiling.** Because defeat retries the
       single battle that was lost, a 0% battle is not "hard" - it is a hard stop, retried forever.
       1-9 blocks at battle 37 of 144, 1-11 at battle 97, 2-11 at battle 144. At `recommendedPower`,
       those stages cannot be completed at all.
    3. **Only 2-4 has a usable gradient** (74% down to 25%), and it is a stage the ceiling rule
       handled normally rather than one of the five that took the inversion fallback.

    So the retune's target is no longer a number per stage. It is a curve per stage that has to stay
    above zero at its ceiling for the stage to be finishable, and the two instruments cannot see
    either end of it. **Re-point the instruments before re-tuning anything** - retuning against a
    floor-only reading would tune the one battle in 144 that is already fine.

    Blocked by: 1, 3, 5, 6, 8, 9, 10, 11, 12a, 12b - all done. Blocks: nothing.

**One retune pass is still unmerged.** `retune/ch2-late-stages` (`a91a2f6`, "balance(ch2): retune
Tideturned boss + late ch2 stages (task 14 pass 1)") is not on `main` and carries 6 changed lines
in `campaign.json` and 6 in `enemies.json` - a first attempt at the `ch2_12` boss defect recorded
just above. Recorded here so it is not lost; whether it should land is a balance decision, not a
merge decision.

Note the label: that commit says "task 14" in the OLD numbering, where task 14 meant the campaign
retune. Under this document's numbering that is task 13 - this entry - and not the events engine
that is task 14 today. Same trap the warning in task 14's own entry describes.

Task 13 is the only step that produces a valid win-rate table, and it has now been run. The
five-body column below is that table. The instrument-comparison block `build-check.ps1` prints is
still the place to read it; its closing line ("Nothing here is tuned. Task 14 is the retune") is
now stale in the same way the code comments below are - it predates this pass and predates the
renumbering, and nobody has gone back to update the harness's own prose.

14. **Events engine, and two starter events on top of it.** Not part of the original numbered plan
    - new system work that arrived after task 13 was written, on the same footing Chapter 2 and the
    art pipeline landed on outside the numbered order, but folded into the list here per instruction
    rather than tracked separately. `[H]` for the engine, the save format and both starter events'
    content; `[U]` for the Events screen's rendering, which is Play-mode verified per its own commit
    but not re-verified by tonight's pass.

    ⚠️ **Do not confuse this with the stale "task 14" already on record elsewhere in this document**
    (task 13's own entry above, and the code comments and commit messages it quotes). Those all
    predate this task's existence and mean the campaign retune - today's task 13 - under an old
    numbering from before Opening Gambit was cut. This task 14 is unrelated content: the events
    engine, numbered here for the first time.

    **Done. Merged to `main`.** Landed on `feat/events-engine`, fast-forwarded into `main`
    (`b66a952`, "feat(events): generic events engine, two starter events, and an Events screen") -
    confirmed by `git merge-base --is-ancestor feat/events-engine main` tonight, not assumed from
    the branch name.

    **The engine.** `Enums.EventActivityKind` (`SummonPerformed`, `RelicDustSpent` today) is the
    only enum in the loop, and nothing switches on it: `GameSession.RecordEventActivity`
    (`GameSession.cs:533`) walks every event in `ContentDatabase.Events` and compares each one's own
    `Activity` field against the kind passed in, so a third activity kind is one new enum member
    plus one call to `RecordEventActivity` at that activity's own call site, not a new case
    anywhere. Confirmed today's two call sites are exactly that: `GameSession.Summon` calls it
    directly (`:702`), and `GameSession.BuyRelic` (`:460`) routes through a new
    `SpendRelicDust` (`:480`) which calls it (`:483`) - `SpendRelicDust` is documented as the one
    choke point any future relic-upgrade spend must also route through, since no upgrade spend path
    exists yet (`RelicMaxTier` is still pinned to 1). Content types - `EventDef`, `EventMilestone`,
    `EventCurrencyTier`, `EventReward` - live in `Definitions.cs` (`:559`, `:594`, `:604`, `:620`),
    same file and shape as `RelicDef`. `ContentDatabase.ParseEvents` (`:111`) loads `events.json`
    exactly like `ParseRelics` loads `relics.json` - optional content, a build with none loads with
    zero events rather than erroring - and `ContentService.cs` reads it through
    `Resources.Load<TextAsset>` via `ReadTextOptional("events")` (`:21`), the same path every other
    content file takes. `GameSession.ClaimEventMilestone` (`:562`) and `RedeemEventCurrency` (`:584`)
    are the two player-facing actions; neither re-checks the event's window, only earning is
    window-gated, so a milestone or currency balance already earned stays claimable after the event
    closes.

    **Save version 5 to 6, additive.** `SaveSerializer.CurrentVersion` is `6`
    (`SaveSerializer.cs:66`). `PlayerProfile.EventProgress` (points, claimed-milestone thresholds, a
    currency balance, per event id) is serialised at `:134-149` and read back at `:264-273`.
    Confirmed tonight, harness-covered, not asserted from the diff alone: a hand-built version 5
    document with no `eventProgress` key at all loads under version 6 code and reads as zero events
    touched - "a version 5 save - an existing player who has never seen an event - loads under
    version 6 code" and "reads as zero events touched, not a phantom entry or a crash (0)", both
    `[ok]` in tonight's run. The newer-build guard is the same one every bump since version 2 relies
    on: a hand-bumped version 7 document is refused with "save is from a newer build (version 7,
    this build reads up to 6)", also `[ok]` tonight. No migration was needed for the upgrade
    direction, unlike the version 4 to 5 relic bump - there is no prior format for event progress to
    translate out of.

    **The two starter events, as content.** The Recollection Rite: `SummonPerformed`, no currency,
    a pure point ladder ("Recollection Rite has no event currency - a pure point ladder, per spec",
    `[ok]`). The Ledger's Week: `RelicDustSpent`, plus its own currency, Ledger Marks, with a small
    redeemable ladder ("Ledger's Week has its own event currency (Ledger Marks)", `[ok]`). Both
    verified structurally sound by the harness (well-formed windows, non-empty ladders) and both
    read from `Assets/Resources/Content/events.json` on `main` tonight, not from memory of an
    earlier pass:

    | Event | `startUnix` | `endUnix` | As a date (UTC) |
    |---|---|---|---|
    | `recollection_rite` | 1788220800 | 1788825600 | 2026-09-01 00:00 to 2026-09-08 00:00 |
    | `ledgers_week` | 1789430400 | 1790035200 | 2026-09-15 00:00 to 2026-09-22 00:00 |

    Both are real calendar dates, not placeholder sentinels like `0` or a far-future year - but
    `events.json`'s own `_comment` field states plainly that "every number in this file - both
    windows, every threshold, every reward, both conversion rates - is a first-pass placeholder, not
    tuned content." Read those two facts together rather than one without the other: the dates are
    concrete enough to load and test against, but nothing has decided they are the real launch
    dates. Do not announce them as such without that decision being made explicitly first.

    **DevHarness EVENTS section**, 46 checks tonight, all `[ok]`. Beyond structural content checks,
    it proves: both activity hooks fire through the real `GameSession.Summon` /
    `GameSession.BuyRelic` call sites, not through calling `RecordEventActivity` directly ("SummonPerformed
    fires through the real GameSession.Summon call site"; "RelicDustSpent fires through the real
    GameSession.BuyRelic call site"); activity-kind isolation both directions ("Ledger's Week,
    listening for a different activity, earned nothing from those summons"; "the earlier
    Recollection Rite points are untouched by spending Dust"); the window gates earning but not
    claiming ("a summon one hour before the window opens earns no Recollection Rite points at all");
    a milestone cannot be claimed twice ("claiming the same milestone a second time is refused", "the
    second attempt paid nothing - this is the double-pay guard the milestone ladder depends on");
    and a currency tier is deliberately different - redeemable again once the balance allows it,
    because redeeming spends the balance rather than setting a claimed flag ("redeeming the SAME
    tier again succeeds - unlike a milestone, this is a purchase, not a one-time claim").

    Blocked by: nothing remaining - summoning (task 3) and the relic shop (task 12a/12b) already
    exist for the two starter events to hook. Blocks: nothing, including task 13: the two systems
    are independent, and Events does not need the retune to have happened first.

15. **The Deep Recital.** Not part of the original numbered plan - new system work on the same
    footing as task 14, an endless PvE mode that gives a player with a full campaign clear
    something to keep doing. `[H+U]`

    **Done. Merged to `main`** (`adf4038`, "feat(recital): The Deep Recital - endless floor ladder,
    aspect cycling, Echoes"; a follow-up fix `40a0439` corrected a capture-tooling double-spend and
    is verification-only, no gameplay change). `DESIGN-DEEP-RECITAL.md` is the spec.

    A persistent, one-way floor ladder - never resets, only climbs. `RecitalGenerator.
    AspectForFloor` cycles the six Aspects every `AspectCycleLength` (6) floors
    (`RecitalGenerator.cs:33`); `LevelForFloor` derives enemy level from `BalanceConfig`'s Recital
    curve. Attempts are gated by Echoes, a capped-and-regenerating resource
    (`PlayerProfile.RecitalEchoesBanked` / `RecitalEchoesClockUnix`), spent by
    `GameSession.TryStartRecitalAttempt` and only ever actually deducted from
    `BattleScreen.OnShow` - `RecitalScreen.OnFight` only peeks affordability, so a Rematch and a
    first attempt both spend exactly one Echo, never two.

    **Save version 6 to 7**, additive (`SaveSerializer.cs:66`): `RecitalCurrentFloor`,
    `RecitalHighestFloor`, `RecitalEchoesBanked`, `RecitalEchoesClockUnix`. The Echo bank is the one
    field here that does not default to the bare-field zero on upgrade - it defaults to
    `BalanceConfig.RecitalEchoesMax`, so a player updating into this version can attempt a floor
    immediately rather than staring at a multi-hour recharge for a resource their save never had an
    opinion on.

    Milestones (`recitalMilestoneInterval`, `recitalDeepMilestoneInterval` in `balance.json`) pay
    Relic Dust on ordinary floors and can grant an unowned relic outright on deep ones, falling back
    to Dust once nothing is left to grant. `RecitalScreen` is the new screen (`ScreenId.Recital`),
    reachable from `MainMenuScreen`'s second nav row.

    **DevHarness DEEP RECITAL section**, ~44 checks, all `[ok]`: aspect cycling, difficulty
    escalation, Echo spend/regen including a backwards-clock-immunity check (the same discipline
    the daily repeat cap already proves), save round-trip, and every Aspect having at least one
    enemy template to draw from.

    Blocked by: nothing remaining - the full campaign (task 13) gives a player a reason to want an
    endless mode once it's cleared, but Recital does not require the retune to exist first. Blocks:
    nothing hard.

16. **Armor.** Not part of the original numbered plan - new system work commissioned after an audit
    found six `UnitDef` stats (Skill Damage, Block, Armor Break, Control Immunity, Damage
    Reduction, Holy Damage) live in `CombatSimulation` and harness-tested, but reachable by no
    relic and no other player-facing system. `[H+U]`

    **Done. Merged to `main`** across four commits: `5f980b0` (`DESIGN-ARMOR.md`, the audit and the
    design), `861221e` (engine: `ArmorSlot`/`ArmorTemper`/`ArmorStat`, `ArmorRoller`,
    `StatMath.ApplyArmor`, save v8), `8d53624` (UI: the Inventory screen's armor section and the
    hero-detail six-slot block), `ce392ab` (Editor verification tooling and its save-isolation
    discipline).

    Six equip slots per hero (`ArmorSlot`: Weapon, Helm, Plate, Boots, AccessoryA, AccessoryB), each
    guaranteeing its own primary stat. A rarity ladder, Temper (`ArmorTemper`: Rough/Honed/Gilded/
    Radiant - see task 17, since that ladder's top two rungs changed how they're reached shortly
    after this task shipped), gates how many random substats roll alongside the primary, drawn from
    the full fourteen-stat pool `CombatSimulation` actually reads - the six otherwise-unreachable
    stats included, which is this system's whole reason to exist. No unique effects, no pipeline
    hooks: `ArmorStatRoll` has three fields (stat, mode, value) and nowhere to put one.

    Resolves entirely in `StatMath.ApplyArmor`, `ApplyRelic`'s sibling, before the simulation
    starts - `CombatSimulation.cs` itself was not touched. An Attunement set bonus
    (`ArmorConfig.AttunementsFor`) counts equipped pieces whose own Aspect matches the wearer's,
    cumulative at 2 and 4 pieces.

    **Save version 7 to 8**, additive (`SaveSerializer.cs:76`): `slag` (a new purse, no shared code
    path with Relic Dust), `ownedArmor` (pieces store their resolved rolls, not a seed - a
    `armor.json` retune must never silently reroll what a player already owns), `equippedArmor` per
    hero, positional against `ArmorInfo.AllSlots`.

    **DevHarness ARMOR section**, ~69 checks, all `[ok]`, including the one that matters most for
    every OTHER fingerprint in this harness: an unarmored profile resolves a battle byte-identically
    to no profile at all, proven because armor genuinely CAN move a seeded battle (Block adds an
    RNG draw the moment it's non-zero) - so what has to be proven is that its absence is exact, not
    that it never does anything.

    Blocked by: nothing - relics (task 12a/12b) exist as the sibling equipment system this reuses
    the `StatMath` mechanism from, but Armor does not modify or depend on relic code. Blocks: 17.

17. **Forge Tiers.** A same-day follow-up to task 16, after playing the shipped system surfaced two
    decisions: Temper's top two rungs were reachable by plain RNG, and there was no sink that
    produced a stronger piece rather than just rerolling or destroying one. `[H+U]`

    **Done. Merged to `main`** across five commits: `83e08ca` (`DESIGN-ARMOR.md` sections 4/7/8/12
    updated), `1cb635f` (engine: the drop ceiling and `GameSession.CombineArmor`), `aaf2f12` (UI:
    the COMBINE button), `d223b4c` (harness), `4ce82a4` (Editor verification: a real forge-chain
    demo, plus a fix for a stale-scratch-save false alarm the first verification run surfaced).

    `ArmorInfo.MaxDropTemper` (Honed) is a **code constant** `ArmorRoller.RollTemper` filters
    against before it reads `armor.json`'s drop weights at all - not merely today's shipped content
    happening to zero out Gilded and Radiant. A harness check proves this the hard way: it hands
    those two rungs a hostile `dropWeight` of 100000 each and confirms the ceiling still holds.
    Gilded and Radiant are reachable only through `GameSession.CombineArmor`: three same-slot,
    same-Temper pieces consumed, one fresh piece (new uid, freshly rolled substats and Aspect) one
    rung up produced. Cannot skip a rung by construction - the output is always
    `ArmorInfo.NextTemper` of the shared input Temper, and there is no parameter to ask for
    anything else. Priced by `ArmorTemperDef.CombineSlag`, keyed by the OUTPUT temper, the same
    per-temper field pattern Salvage and Reroll already use.

    No save version bump - this landed inside version 8, adding a field (`ArmorTemperDef.
    CombineSlag`) and a code-level ceiling, not a new save shape.

    **DevHarness ARMOR - FORGE TIERS section**, ~24 checks, all `[ok]`: the structural drop-ceiling
    proof above, Combine consuming exactly three matching inputs (with every refusal path proven to
    leave the profile untouched), the ladder walked one rung at a time from Rough to Radiant across
    three successive real combines, and the Slag pricing pattern.

    Blocked by: 16, this extends Armor's own Temper ladder rather than standing alone. Blocks:
    nothing hard - the Forge screen (a UI-only relocation of Reroll/Salvage/Combine off the
    Inventory screen's per-row buttons into their own hub, reclaiming the word "Forge" - see task 6
    above) is a separate pass, not tracked as its own numbered task here.

18. **Four Ember heroes, and Resolve - one generic stacking counter under all four passives.** Not
    part of the original numbered plan - roster work that arrived with an external hero import, on
    the same footing tasks 14 to 17 landed on. `[H]` for the mechanic, the content and the harness;
    the four heroes have no art, so there is no `[U]` half to run.

    **Done. On `feat/ember-heroes-import`**, `build-check.ps1` green with zero failures.

    **The import, and the two things it broke on arrival.** Four heroes - Seraphine (Breaker),
    Marrow (Mender), Veska (Striker), Orren (Ranger) - appended to `heroes.json`, taking the roster
    from 7 to 11. `HeroRole` gained `Ranger` after `Stalker` for Orren; nothing was reordered or
    renamed, and `CheckContent`'s "every combat role has a hero" passed on the first run because
    Orren covers the new value. Two checks did fail, both content invariants rather than anything
    about Ranger: the hero count is asserted as an exact literal (7, now 11), and Veska and Orren
    shipped `armorBreak` 0.05 and 0.06, which trips the guard that keeps Kael the only unit
    authoring a phase-B stat - a non-zero value there adds an RNG draw and can move a seeded
    `BattleFingerprint`. Both zeroed.

    **The real problem was six passive-less heroes against a cap of two.** All four imports shipped
    `"passive": null`, joining Kalyx and Kael. That cap is not arbitrary bookkeeping - `Program.cs`
    documents it as "a third one cannot appear unnoticed", written when Kael became the second. Six
    is not a near miss on that, it is the check doing exactly its job. Kalyx and Kael keep the
    bucket: Kalyx's identity is the Ember Burn-stacking aspect rule (`CombatSimulation.cs:622`,
    confirmed live this pass - `AddStack` with `EmberBurnStackCap`, keyed on `source.Aspect ==
    Aspect.Ember`), and Kael's passives are still blocked on a Ruin resource that does not exist.

    **Resolve, and why it is one mechanism rather than four.** Nothing in `EffectKind`, `StatusKind`
    or `PassiveTrigger` supported a personal counter. The obvious cheap answer - four bespoke
    passives, four bits of simulation code - is the exact trade DESIGN-RELICS and DESIGN-ASPECTS
    both already refused: a shared hook that content configures, owned once, beats per-owner code,
    and whoever owns the hook owns it for everybody. So one mechanism, and no hero gets simulation
    code of its own out of it:

      - `StatusKind.Resolve` - a personal counter, stacking through the same `AddStack`/`CountOf`
        pair Ember's Burn already uses rather than a second counting mechanism beside it. Nothing
        in the damage, heal, defence or targeting path reads it. It is inert until an effect asks.
      - `EffectKind.GainResolve` - banks stacks, capped by a new `maxStacks` field.
      - `perStack` on any effect - scales that effect's Power by `(1 + perStack * n)` off the
        CASTER's count, snapshotted once per effect so an AoE strikes every target with the same
        number no matter what dies partway through, and cloned rather than mutated because the
        `EffectDef` is the ContentDatabase's own and shared by every unit in every battle - the
        same trap `EffectDef.Clone`'s comment already records for relics.
      - `consumesResolve` on any effect - clears the caster's counter after that effect has
        finished with every target, so an AoE spends the stack once rather than once per enemy.

    No new `PassiveTrigger` was needed. `AfterBasicAttack`, `OnDamaged` and `OnKill` already
    existed and carry all four heroes between them.

    **The four passives, each against the mechanism rather than around it.** Seraphine's *Kindling
    Script* banks on `AfterBasicAttack` to 5 and Final Chapter spends them at `perStack` 0.18
    (her burn-ramp). Marrow's *The Burning Vow* banks on `OnDamaged` to 4 and Final Offering pays
    out at 0.22 (sacrifice into healing). Veska's *Oathbreaker* banks on `OnKill` to 3 and Cut the
    Ties spends at 0.35. Orren's *Marked Quarry* banks on `AfterBasicAttack` to 4 and Final
    Quarry's single-target half collects at 0.25 before the volley goes wide.

    One honest deviation worth naming rather than leaving to be discovered: Veska's brief was
    "execute-scaling", which normally means damage rising as the target's health falls. A counter
    cannot express that - it counts events, not target state. What shipped is execution
    *chaining*: each kill sharpens the next cut. Her basic already targets `LowestHpEnemy`, so the
    fantasy survives the substitution, but it is a different rule from the one the brief described.

    **Proven, not assumed.** A green build only says nothing else broke, and three of the four
    heroes sit on the bench in the economy sim where their passives would never fire. So the
    mechanic gets its own harness section, `RESOLVE`, on synthetic content plus a control run
    differing by exactly `perStack`: stacks bank (8 applications), the cap holds (peak 3 of a
    maximum 3, never 4), `consumesResolve` actually clears it (2 spends), and the scaling is
    measured rather than asserted - the same active at the same seed landed **134 in the control
    and 536 with three stacks at `perStack` 1.0, x4.00 against an expected (1 + 1.0*3)**. The live
    roster is checked too: four shipped heroes carry a `GainResolve` passive. Veska also appears in
    the economy sim's deployed line at 33-49% of team damage, and the harness now reports
    `OnKill is authored in shipped content for the first time: ryn_solvarr/Follow-Through,
    veska_the_broken_oath/Oathbreaker`.

    Blocked by: nothing. Blocks: nothing hard. Two follow-ups this pass deliberately did not do -
    the four heroes have no sprite art and fall back to the procedural rig, and `Resolve` is
    strippable by `EffectKind.Dispel` like any other non-harmful status, which is defensible for a
    resource buff but nothing in shipped content exercises it either way.

19. **`CheckPinnedFiveBodyCurve` was picking its own team, and task 18 changed who won.** A fix to
    the instrument, not to content - no campaign number moved. `[H]`

    **Done. On `fix/pinned-curve-roster-drift`**, `build-check.ps1` green with zero failures.

    **The symptom.** After task 18 merged, the five-body curve went from the 41-83% band task 13
    left it at to 0% on seventeen of twenty-four stages - 100% through 1-6, 83.3% at 1-7, then a
    cliff to zero from 1-8 that never recovered. Task 13's whole retune reads as broken content.

    **The check stayed GREEN through all of it, and that is the more serious half.** Its only
    assertion is that the curve is not perfectly flat, and a cliff is not flat. An instrument that
    silently changes what it measures and still reports success is worse than one that fails: the
    two chapters of content calibrated against it were quietly invalidated with nothing to say so.

    **What it was actually doing.** The check grants every hero in the ContentDatabase at Lv1,
    sorts the entire roster by `PowerScore` descending, and deploys the top five - then levels the
    cheapest of those five until team power reaches the stage's `recommendedPower`. The sort is a
    function of how many heroes exist, so the team is re-picked whenever content is added. It had
    been correct by luck on a 7-hero roster and stopped being correct on an 11-hero one.

    Lv1 `PowerScore`, the order that sort uses, after task 18:

    | | hero | role | Lv1 power | |
    |---|---|---|---|---|
    | 1 | nyx_serrow | Stalker | 1458 | deployed |
    | 2 | veska_the_broken_oath | Striker | 1379 | deployed |
    | 3 | ryn_solvarr | Striker | 1360 | deployed |
    | 4 | seraphine_the_last_ember | Breaker | 1295 | deployed |
    | 5 | orren_the_cinder_hunter | Ranger | 1276 | deployed |
    | 6 | kalyx_emberwright | Breaker | 1217 | benched |
    | 7 | **orrin_kaal** | **Bulwark** | **1205** | **benched** |
    | 8 | maeve_corrin | Warden | 1171 | benched |
    | 9 | wren_ilyra | Mender | 1094 | benched |
    | 10 | marrow_the_burning_vow | Mender | 1025 | benched |
    | 11 | kael_the_ashbound | Breaker | 600 | benched |

    Four imported heroes all landed between 1025 and 1379 and pushed Orrin from 4th to 7th. The
    instrument was fielding five damage dealers, no tank and no healer. Confirmed against the
    harness rather than by arithmetic alone: the printed floor moved from 6411 to **6768**, which
    is exactly the sum of that new top five, and is back to 6411 with the fix in.

    **Why that specific absence is the whole collapse.** Orrin is the Lumen anchor - a living Lumen
    unit answers every single-target question aimed at its side (`FindTargetAnchor`) - and task 13's
    retune was authored on top of that interception, in its own words "difficulty is authored as AoE
    presence, because that is the only damage Lumen does not absorb". Remove Orrin and every
    single-target attack in the game stops being absorbed by a 2600-HP tank and spreads across five
    squishy bodies with no healer behind them. The content did not get harder; the yardstick lost
    the mechanism it was calibrated against.

    **Kael is not the cause, and fixing him first would have made it worse.** His stat scale is
    genuinely wrong - hp 480, attack 33, defense 24 against a roster authoring 1300-2600 hp and
    105-220 attack, giving him 600 Lv1 power against 1025-1458 for every other five-star. It is a
    real bug and it is filed here as one. But he ranks 11th of 11 and is never selected, so he
    contributes nothing to this. Scaling him to roster norms was modelled before deciding: a
    corrected Kael lands about 1235, which puts him 6th and pushes Orrin from 7th to **8th**. The
    obvious suspect is innocent, and treating him first would have deepened the fault.

    **The fix, and the argument for it over the alternative.** The five are now pinned by name -
    `nyx_serrow`, `ryn_solvarr`, `kalyx_emberwright`, `orrin_kaal`, `maeve_corrin` - with a check
    that fails loudly if any of them is missing from content, because a silently incomplete line is
    the exact failure this replaced. Those are not a fresh design choice: they are precisely the old
    sort's top five on the 7-hero roster, floor 6411, which is the line task 13's 24-stage table was
    measured against. Any other five would be defensible in the abstract and would still invalidate
    that table, which is the same damage the drift did. A yardstick's first duty is to stop moving.

    The alternative - keep the sort, fix Kael, hope a fair pin with correct numbers resolves it - is
    refuted above by the modelled reordering, and would leave the underlying defect in place
    regardless: the team would still be a function of roster size, and hero number twelve would
    break it again. Kael's scale still wants fixing on its own merits; it is not this.

    **Verified by exact restoration, not by "looks better".** With the pin in, all 24 rows return to
    the values task 13 recorded, digit for digit: floor 6411 on 1-1 to 1-5, then 1-8 79.2%, 1-9
    70.8%, 1-10 54.2%, 1-11 62.5%, 1-12 41.7%, 2-2 91.7% descending to 2-7 58.3%, and 2-12 83.3%.
    A curve that climbs, not a flat line and not a cliff. No file under `Assets/Resources/Content`
    was touched by this task.

    Blocked by: nothing. Blocks: nothing. Two things deliberately left: Kael's stat scale (since
    done, see task 20), and the fact that this check's pass condition is still only "not perfectly
    flat" - it would not have caught this on its own, and a curve-shape assertion (monotonic-ish,
    bounded) is the check that would have. That one is still open.

20. **Kael's stat scale: he was never converted off the generator's numbers.** Content only, four
    numbers. `[H]`

    **Done. On `fix/kael-stat-scale`**, `build-check.ps1` green with zero failures.

    Kael shipped at hp 480, attack 33, defense 24, speed 162, giving him 600 Lv1 power against
    1025-1458 for every other five-star - flagged as a real bug by task 19 and left there.

    `heroes_ember.json`, the generator-format source, turns out to carry those exact numbers, so it
    is not the fix; it is the evidence. The whole file is on a low scale (hp 325-480, attack 32-39),
    and the four heroes imported from it in task 18 were rescaled on the way in - roughly x3.9 hp,
    x5 attack, x3.1 defense, x0.5 speed, per-hero by role. Kael predates that import and was
    authored straight from generator output, so he never got the conversion. Not a typo, a missing
    step.

    Fixed by applying the same conversion the cohort received, at the mean per-axis ratio rather
    than by inventing numbers: **hp 1850, attack 165, defense 75, speed 82**, giving 1167 Lv1 power.
    That lands him between Maeve (1171) and Wren (1094) and below Orrin (1205), so no
    power-ordering assumption anywhere moves. The pinned five-body curve is unchanged digit for
    digit, which is the point of task 19's pin - Kael's numbers can move without the yardstick
    moving.

    His role stays `Breaker`. The generator file calls him `Vanguard`, which is not in `HeroRole`
    and would parse to the `Striker` fallback silently; adding it is a separate decision, and his
    `passive: null` bucket entry is unchanged. Also unchanged and worth naming: that file authors a
    Ruin stacking resource for him across two passives and three talents, which `EffectKind.
    GainResolve` from task 18 could now actually express - the first time that has been true.

    **Three incompatible stat scales exist in this project and only one is load-bearing.** Shipped
    heroes run hp 1300-2600 / attack 105-227; shipped enemies run hp 720-4200 / attack 105-480; the
    generator's JSON output runs hp 325-480; and DESIGN-HERO-GENERATOR's own documented ranges run
    hp 13000-30000 / attack 950-2300, about ten times ABOVE shipped rather than below. Heroes and
    enemies are the pair that has to agree, because that is what combat resolves between and what
    `defenseConstant` 400 is calibrated for, and they do. The other two scales are proposals nothing
    reads. Moving to either is a whole-game rescale - every hero, every enemy, `defenseConstant`,
    the level curve, and all 24 stages task 13 just tuned - not a hero fix, and not done here.

    Blocked by: nothing. Blocks: nothing.

21. **Kael's real kit, and per-hero proof for all five Ember kits.** Content plus harness; no engine
    surface added. `[H]`

    **Done. On `feat/kael-ruin-kit`**, `build-check.ps1` green with zero failures.

    Kael shipped basic-and-active-only with `passive: null` since the animation-pipeline pass that
    added him, on the standing note that his Ruin resource "does not exist yet". Task 18 built
    Resolve, which is a Ruin in everything but name, so this is the follow-up that entry was waiting
    for. The source design is schema-incompatible and was translated, not imported - the same way
    task 18 handled the other four.

    **Two audits ran before anything was written, and both changed the design.**

    *Is there a persistent per-stack stat buff?* Half of one. `EffectiveAttack` reads
    `SumMagnitude(StatusKind.AttackUp)`, and `SumMagnitude` totals across stacked entries, so the
    READ side would handle "+5% Attack per stack" correctly. The WRITE side cannot produce it:
    `AddStack` has exactly two call sites in the whole engine - Ember-sourced `Burn`, and
    `GainResolve`, which always writes `StatusKind.Resolve`. Every other `ApplyStatus` goes through
    `AddOrRefresh`, which folds re-application into one entry keeping the stronger magnitude. So
    stacked `AttackUp` is unauthorable, and a persistent per-stack stat buff cannot be expressed
    without new engine surface. Folded into `perStack` power scaling instead, per instruction.

    *Does `OnDamaged` fire for self-inflicted HP loss?* It would - the trigger sits in the shared
    damage funnel and fires on whatever the damage lands on, so `Damage` at `TargetMode.Self` would
    reach it. It is still the wrong hook, for two reasons found in the same read. First, the trigger
    cannot tell self-inflicted loss from an enemy's hit, so hanging Ruin on it would bank a stack
    every time anything hurt him. Second, there is no primitive that costs a unit a fraction of its
    own HP at all: `Damage` computes `EffectiveAttack * Power + Flat` and never reads `SelfHpRatio`
    (only `Heal` and `Shield` do, `CombatSimulation.cs:574/581`). Routing a cost through
    `Damage`/`Self` would put it through a Block roll, a crit roll, the aspect multiplier and his own
    Defence mitigation - a number that is dodgeable, occasionally doubled, and never 8% of anything.
    So the Ruin gain went inline into Breaking Point's own effects array, where the ability that
    causes the cost grants its own stack deterministically and no trigger is involved.

    **What shipped.** Breaking Point banks its stack first so the cast that earns it also benefits,
    then scales, then finishes:

        GainResolve  Self         maxStacks 4
        Damage       AllEnemies   power 2.20, perStack 0.17
        Damage       LowestHpEnemy power 0.55

    `perStack` 0.17 is the two payoffs folded into one number: the design's +12% per-Ruin skill
    scaling plus the +5% per-Ruin Attack buff that has no persistent expression. Ruin is kept, not
    spent - no `consumesResolve` anywhere in his kit - which matches the source design, where only
    an unbuilt talent made Breaking Point consume. Blood for Power is a `BattleStart`,
    `oncePerBattle` passive applying `DefenseUp` 0.12 for the battle.

    **Three pieces were deliberately not carried over.** The 8%-of-current-HP self cost, because no
    primitive expresses a proportion of current HP and faking it via `Damage`/`Self` produces a
    mitigated, blockable number that is not a percentage - so Breaking Point ships without its
    downside, and its numbers are held modest to pay for that. The `+8% Block` half of the passive,
    because Block is a phase-B STAT and has no `StatusKind`; it is instead already authored in his
    base stats as `"block": 0.08`, where the harness's phase-B guard has always named him as the one
    permitted carrier - the buff exists, it is just baked rather than granted. And the execute's
    `below 25% HP` condition, because no primitive reads a target's HP fraction: `threshold` belongs
    to `OnHpBelowThreshold` and tests the HOLDER's health, not a target's. The finisher instead hits
    `LowestHpEnemy` unconditionally at 0.55 rather than 0.80, the same substitution and the same
    reasoning as Veska's "execution chaining" in task 18. No new `EffectKind`, `StatusKind` or
    `PassiveTrigger` value was added for any of this.

    **Per-hero proof, because the aggregate was not proof.** The RESOLVE section from task 18 shows
    the mechanism works; it cannot show that any particular authored kit reaches it, and three of
    these five sit on the bench in the economy sim and cast nothing there. New `EMBER HERO KITS`
    section: the shipped `heroes.json` read verbatim off disk into a synthetic arena built to
    satisfy every trigger the five use at once - `BattleStart` (Kael), `AfterBasicAttack`
    (Seraphine, Orren), `OnDamaged` (Marrow, so the enemies must hit back) and `OnKill` (Veska, so
    an enemy must be killable), which is why the line is five modest mooks rather than one anvil.
    Across 24 seeds all five fire BOTH halves by name - `kael_blood_for_power` /
    `kael_breaking_point`, `seraphine_kindling_script` / `seraphine_final_chapter`,
    `marrow_burning_vow` / `marrow_final_offering`, `veska_oathbreaker` / `veska_cut_the_ties`,
    `orren_marked_quarry` / `orren_final_quarry` - with 584 damage events and no null refs.

    The arena needed one correction worth recording: at the first attempt every passive fired and
    every active failed, because five levelled heroes cleared five mooks before anyone banked 100
    energy. That is the check working - a kit that never casts is exactly what it exists to catch.
    Enemy health and energy gain were raised until the fight lasts long enough to reach the actives.

    Independence is asserted rather than assumed: five separate units hold Resolve in the same
    battles, and **every carrier's first stack reads 1**, which a shared or clobbered counter could
    not produce - a second carrier's first application would already read 2 or more.

    The pinned five-body curve is unchanged digit for digit, confirmed explicitly rather than
    predicted: Kael is not on that pinned line, and task 19's pin is what makes that a non-event.

    Blocked by: nothing. Blocks: nothing. Not touched, deliberately: animation, sprites and the art
    pipeline. Still open from the source design and needing engine surface that does not exist -
    the HP cost, a persistent per-stack stat buff, target-HP-conditional effects, and all three of
    Kael's talents.

## Task 13's instrument was broken, and what was done about it

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
scaling fixes that; enemy levels do, and that is task 13's.

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
restore the curve. Enemy levels are task 13's first lever, not its last.
