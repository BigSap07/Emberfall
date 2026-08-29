# Emberfall - build order

Supersedes the previous version, which predates DESIGN-ASPECTS, DESIGN-WORLD, DESIGN-COMBAT-STATS
and DESIGN-ART-BIBLE. Single ordered plan for the whole project, reconciled against those four plus
DESIGN-RELICS and the surviving originals (FIRST-TEN-MINUTES, IDLE-PROGRESSION, REPLAY-ECONOMY). No
new design here, sequencing only.

**Status, as of tonight's audit (2026-08-29).** Tasks **1 through 12 are all done on `main`**,
verified against the actual code and content and against a live `Tools/build-check.ps1` run
tonight (compiles clean, `RESULT: all checks passed`), not against this document's own prior
claims — several of those were found stale and are corrected in place below.
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
separate, smaller doc debt this pass does not fix. Task 13, the retune, is still not done: content
is unchanged since the last audit, and a live harness run tonight still reports "Authored values are
UNTUNED" and "Nothing here is tuned." A small, unmerged first pass exists on branch
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

13. **Full campaign retune. Once, last.** ⚠️ No longer twelve stages: chapter 2 shipped (`f1f5b71`,
    see "Already done" above), so `campaign.json` now holds **24 stages across two chapters**, and
    both need this pass — chapter 2's numbers are provisional by its own commit message, exactly
    like chapter 1's pre-retune numbers were. `[H]`, `RunCampaignBalancePass` is exactly the tool; a
    handful of spot-check battles in `[U]` are still worth watching.
    ⚠️ **`RunCampaignBalancePass` alone is no longer "exactly the tool".** See "Task 13's
    instrument was broken" below: it reported 100% on all twelve stages for reasons that had
    nothing to do with the content, and two prerequisites had to land before it could be tuned
    against at all. Both have now landed; the line above is kept because the retune is still one
    pass, still last — now over twice the stage count it was scoped against.
    Blocked by: 1, 3, 5, 6, 8, 9, 10, 11, 12a, 12b — all done, 12b included: it landed before this
    task rather than after, so the retune this task describes covers all ten shipped relics (8
    Legendary, 2 Mythic — see task 12b's naming correction), not four. Blocks: nothing, this is the
    last task.

    ⚠️ **The code itself still calls this task 14, not 13.** `Tools/DevHarness/Program.cs`'s own
    output ("Authored values are UNTUNED... task 14 is the retune", "this is the instrument task 14
    tunes against") uses the older numbering, and so does `6bd559c` ("docs: record task 14's two new
    blockers..." — this one is on `main`, verified an ancestor of `HEAD`) and the unmerged
    `a91a2f6` ("...task 14 pass 1"). All predate Opening Gambit being cut out of the numbered list
    and this task moving down from 14 to 13. Nobody has gone back to update those comments and
    commit messages for the renumbering; they are not wrong about the content, only about which
    number it answers to. Same shape as Known trap 3's `galeExtraTurnsPerRound` comment below, which
    has the identical mismatch for the identical reason.

Task 13 is the only step that produces a valid win-rate table. Nothing tuned before it should be
read as final. **Run tonight**, not merely inferred: `Tools/build-check.ps1` compiles and executes
`Tools/DevHarness` against a real C# compiler in this environment (`csc.exe`, found and used), and
its live output confirms "not done" directly rather than by absence of a commit —
`campaign.json`'s numbers are unchanged, and the harness prints "Authored values are UNTUNED" and,
in its final instrument-comparison table, "Nothing here is tuned. Task 14 is the retune; this table
is its input." One first, partial pass exists but is not on `main`: branch `retune/ch2-late-stages`
carries a single unmerged commit (`a91a2f6`, "balance(ch2): retune Tideturned boss + late ch2
stages (task 14 pass 1)") that raises the Tideturned boss's kit and levels on three late chapter-2
stages, moving that boss stage's five-body pinned win rate from 100% to 79.2% by its own commit
message. It does not change task 13's status here because it is not merged, but it is live evidence
someone has already started.

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
