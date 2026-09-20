# Emberfall - build order

## Now / Next

Current state, checked against the repo on 2026-09-13, not against the rest of this document.

### `main`'s harness is DELIBERATELY RED on exactly one check

1. **The CEIL band check.** Removing the six engine-owned aspect rules moved every difficulty
   number in the game, and the retune is deliberately deferred rather than smeared across the
   change that caused it. **Closes at a single retune after the roster's abilities are
   authored** - not before. Retuning against placeholder kits would bake a measurement of the
   wrong game into `campaign.json`.

**A second failure is a regression.** If the count goes to two, something broke - treat it as a
real finding and diagnose it rather than adding it to this list.

Previously three checks were red (role coverage, summon pity, CEIL). The 94-hero import closed
the first two: all ten roles are now filled, and 91 non-starter heroes satisfy the pity
guarantee.

### Open: `engine/linked-targeting` — unmerged

**The linked-targeting primitive. Engine only; no hero is authored on it.** One concept covering
the four uses the 14-hero survey identified, so it is a primitive rather than a mechanism built
for whoever asked first: **two units bound together so that an action on one causes an effect on
the other.** `StatusKind.Linked` is the binding, and it is the only status in the enum that names
a THIRD unit — `SourceId` is still the hero that created it, and `StatusInstance.LinkedToId` is
the partner, which for Talren are three different bodies.

- **All 24 stage fingerprints are unmoved: 72 of 72 byte-identical** (24 stages × 3 seeds, the
  set `CheckEffectHitResistNoOp` reproduces, dumped as text before and after and compared). The
  harness report is a strict superset — 43 new checks, no existing line changed except the
  PassiveTrigger parity label, which reads 41 members instead of 39. The 11 red checks are the
  same 11, line for line: the CEIL band plus ten guards the authored batch-five kits already
  tripped.
- **Three of the four uses touch the damage funnel, and that is what the `_anyLinks` bool is
  for.** Every link dispatch point tests one bool that is false until a link is created, so a
  battle with no link in it pays nothing. Redirect, echo and the hit trigger all sit inside
  `DealRawDamage`, which every point of damage in the game passes through; without the gate the
  cost would be paid by every battle ever resolved.
- **Rovan's Covered and Nysera's Linked redirect are one path, and the harness measures the
  claim rather than asserting it.** `Sim.RedirectShare` is the split; Covered names its
  destination with the applier's id and a link names it with `LinkedToId`, which is the entire
  difference. The arithmetic was extracted from the Covered block **without changing** — same
  rounding, same clamp, same two early exits in the same order — which mattered because
  **Covered is authored by six effects in `heroes.json`** and is the one part of this work that
  shipped content reaches. `CheckCoverAndLinkAreOnePath` runs the same arena twice at the same
  seed, once with each mechanic, and asserts every unit's damage total matches.
- **Redirect and Echo are the same split with opposite bookkeeping.** Redirect subtracts the
  moved share from the holder; Echo does not. That is the whole of "instead" versus "as well",
  and it is why they are one `LinkTransfer` enum rather than two mechanics. A third member,
  `None`, is a real authoring and not an absence: Vaelis's binding exists so her passive can hear
  about a hit on the far end, and a link that also moved damage would be a different ability.
- **`_inCover` became `_inRedirect` and is now shared by all three.** One guard, so a Covered hop
  onto a Linked protector does not then echo and a link redirect onto a Covered partner does not
  split again. Separate guards would let alternating mechanics chain as deep as the pairings
  allowed, and a pair of units linked to each other is exactly Talren's active. The echo arena
  builds that mutual pair deliberately and proves the bound with a number: the front enemy's
  total is the control's exactly, not more.
- **What a redirect moves is the damage, not the calculation.** `DealRawDamage` is handed a
  number already through the attacker's crit roll, the aspect chart and the HOLDER's Defense, so
  redirecting it does not re-run any of that against the partner. That is precisely how Covered
  has always behaved; a link that recomputed would be a second differently-shaped redirect rather
  than the same one twice. The aspect source scan lists the three new lines that carry
  `aspectFactor` into a redirected or echoed share, because keeping the original blow's chart
  reading is a decision and the scan is meant to make one say so out loud.
- **"Targeted" and "hit" are two triggers, not one with a flag.** `OnLinkedUnitTargeted` fires at
  the top of `ApplyDamageEffect` before the miss check; `OnLinkedUnitHit` fires below the funnel
  once damage has landed. An arena whose bound enemy carries `EvasionUp` at 1.0 separates them
  with counts: the aim trigger fires four times and nothing lands at all.
  - **The targeted half is deliberately narrow and covers DAMAGE effects only.** A heal, a buff
    and a cleanse all name a target too, and `ResolveTargets` is the point they share — but
    firing a passive from inside targeting means firing between targets being chosen and the
    effect resolving, where a passive that kills the target leaves the caller holding a corpse it
    has committed to acting on. Same argument `OnEnemyDamagedAlly` settled the same way.
- **Neither is a broadcast, which is a third dispatch shape.** `Sim.DispatchLinkPassive` walks the
  units looking for an entry naming the subject, because a link is not a side: Rovan's partner is
  an ally, Talren binds two enemies to each other, and a side-scoped dispatch would be wrong for
  at least one of them.
- **`PassiveDef.TriggerSourceIsAlly`, because no condition can ask which side a unit is on.**
  Vaelis's "when an ALLY hits the bound enemy" needed it: `TriggerSourceIsSelf` is the wrong
  half, and `ConditionSubject.LastAttacker` names the attacker while every `EffectCondition` then
  asks about its HP, statuses or stats. The arena is a differential — the same fight run twice,
  differing only in whether the watcher attacks the bound enemy herself — and her fire count is
  the ally's alone in both, so the filter is measured excluding something rather than untested.
- **`EffectKind.EchoDamage` reads a realised number, not a recomputed one.** It repeats a fraction
  of `_lastDamageDealt`, which is what the last hit actually took off a bar after Guard, Cover,
  the pool and the Ledger, and it pays through `DealRawDamage` so the pipeline is not applied
  twice. Cleared at every ability boundary exactly as `_lastTarget` is. The arena gives the echo's
  victim real Defense, so a recomputed hit would land on a different figure and the 40% equality
  would fail.
  - **An echo does not become what later echoes read.** The funnel writes `_lastDamageDealt` for
    every hit it carries, so without saving and restoring it an echo aimed at `AllEnemies` would
    compound down the line — the third body taking a fraction of a fraction of a fraction.
- **`TargetMode.PreviousTarget` and `TargetMode.LinkedUnit`.** Previous is a sibling of
  `LastTarget` in every respect and sits in the same post-anchor family; `LinkedUnit` resolves
  BEFORE the anchor alongside `BroadcastVictim`, because a link names one specific body that may
  stand on either side and a Taunt holder intercepting it would make the mode mean nothing. The
  PreviousTarget arena puts a bystander in slot 0 that the mode's FrontEnemy fallback would hit:
  it takes zero in the treatment and the whole third effect in the control.
- **A drift was found in `validate_heroes.py` and the check that should have caught it could
  not.** `CheckEnumParity` compares a list hardcoded in the harness against the C# enum, which
  answers whether the harness knows the enum and NOT the question its label asks.
  `HighestMyMarkStacksEnemy` was in `Enums.cs` and in the harness list and had never reached the
  python file, so a kit authoring it validated as a bad TargetMode while the section reported
  parity. Fixed, and `CheckValidatorFileParity` now **reads `validate_heroes.py` itself** and
  parses its five sets — the same lesson the role-ordinal scan that passed by comparing nothing
  already taught once.
- **No new `BattleEventType` for a sixth pass.** A link reuses `StatusApplied` and an echo reuses
  `Damage`. The one deviation is recorded rather than left to be found: a Linked `StatusApplied`
  writes the PARTNER'S ID into `Value` where every other status writes a stack depth, because a
  link's identity is who it names and a `StatusKind` carries no per-instance name. No existing
  battle records a Linked event, so nothing reading `Value` for a stack depth is affected.
- **Not built, and named rather than approximated:** targeting-time redirection. Nysera's
  "an attack aimed at her is redirected" is served at the funnel at a magnitude of 1.0, which
  produces the same outcome; a second, targeting-level redirect would be two ways to say one
  thing, which this engine has refused before (see the absent `AllEnemiesWithStatus`). The
  visible cost is that the log shows the blow aimed at her and the damage landing on the partner.

### Open: `engine/nine-gaps` — unmerged, off `engine/linked-targeting`

**All nine gaps the blocked authoring attempt named. Engine only; still no hero authored.** 72 of
72 fingerprints unmoved, the same 11 harness reds line for line, 42 new checks with a count-based
arena per gap. It sits on `engine/linked-targeting` and needs it.

- **Gap 1, charges.** `SpendLinkCharge` decrements and removes at zero, emitting `StatusExpired`
  the way a spent Guard charge does. 0 charges stays UNLIMITED, which every link authored before
  this has. Spent BEFORE the share is delivered, so a one-charge link cannot fire again from
  inside the recursion its own delivery starts.
- **Gap 2, a flat echo.** `StatusInstance.LinkAmount`, Attack-scaled once at creation like Burn's
  magnitude and the Ledger's cap. An echo pays `share + flat`. **Echo only** — a redirect is a
  SPLIT that must create no value, and a flat addend on one would hand the destination more than
  was ever struck.
- **Gap 3, the attacker filter.** `LinkFromCasterOnly`, the same test and deliberately the same
  spelling as the Ledger's `BankFromCaster`, folded into `FindLink` so every link reader honours
  it at once.
- **Gap 4, the other end and the set.** `OtherLinkedUnit` is the caster's highest-HP partner
  excluding the one the dispatch is about — one mode for both shapes that asked, because with two
  records "highest HP among one" IS the other one, and with three it is the sentence a talent asks
  for in as many words. `AllLinkedUnits` is the set, deduplicated.
- **Gap 5, attacker-keyed deflect.** `LinkTransfer.Deflect`, its own rule beside `RedirectShare`
  rather than a loosened Provoked: Provoked's same-side refusal is correct for what Provoked means
  and is untouched. It fires FIRST in the funnel, ahead of Guard, and that placement is the rule's
  meaning rather than a convenience — the blow never reaches this body, so none of the victim's
  own defences may spend themselves on it.
- **Gap 6, four triggers.** `OnTargeted`, `OnStatusGained` (TriggerStatus-filtered, and
  deliberately NOT raised by MoveStatus or TransferStatus — a status changing carrier is its own
  event with its own record), `OnLinkEchoFired` on the link's creator, `OnRedirectReceived` on the
  protector. The last fires for Covered as well as for a link, because they are one path and a
  trigger serving only one would be the two drifting apart at the first opportunity.
- **Gap 7, the moved amount.** `ScaleByLastMoved` reading `_lastMoved`, a **Dictionary keyed per
  caster from the start**, joining `_lastLedgerRelease` rather than the three single-field scalars
  beside it. `LastMovedAboveHpFraction` is the threshold half. A second scalar,
  `ScaleByLastDamage`, reads the broadcast-window `_lastDamageDealt`.
  - **What the arena can and cannot prove, stated rather than glossed.** It proves the scalar
    tracks the amount: halving the ward's share halves the follow-up strike, and the strike equals
    the share. It does NOT prove the per-caster keying, and no arena can — every write immediately
    precedes its own read, so one global would answer identically in any ordering a battle can
    produce. The keying is structural, against a failure mode this harness cannot construct.
- **Gap 8, the causing unit.** `TargetMode.BroadcastSource` and `ConditionSubject.BroadcastSource`.
  Distinct from `LastAttacker`, which resolves through the subject's `LastAttackerId` and so
  answers correctly only for a broadcast raised by a hit, naming the wrong body for one raised by
  a heal or a shield.
- **Gap 9, the wrong sign.** `SubjectNotDamagedByMeThisRound`, the exact mirror of
  `AlreadyDamagedSubjectThisRound`. **A general `negate` flag was rejected**: three fields that
  silently re-point all forty conditions, including the twenty-odd whose negation nobody has
  reasoned about — what "not TurnProgressAbove" means for a unit with no position left, or "not
  StoredRoleMatches" when nothing was recorded.

**A bug in the previous pass, found by an arena rather than by reading.** `DispatchLinkPassive`
was handed depth 0 at the funnel, matching the broadcasts beside it — but a passive answering "my
partner was hit" by striking its OTHER partner walks straight back in at 0 again. The gap-4 arena
overflowed the stack the first time it authored that with `oncePerBattle` false, which is the
natural shape of the link triggers rather than an exotic case. `_linkDispatchDepth` bounds it and
is handed to `TriggerPassive` as the depth, so one cap governs instead of two that could disagree.

**`ScaleByLastRemoval` carried a COUNT where every description says an amount, and that was the
real defect.** Verified before repairing, on real content at level 40: Cyrra's active healed **791**
before the `ScaledTerm` change (0.8 × Attack, the flag inert) and **1** after (0.8 × one entry
removed). Her text says *"the removed damage is converted into healing equal to 100% of that
amount"*; her talent I says *"converts only 60% of its removed damage"*. **Audited the whole
roster: two users, both hers, both wanting an amount, and nothing anywhere reading it as a
count** — which is what made changing the meaning the right repair rather than adding a sibling
scalar. A sibling would have left the mis-named one in place for the next author to reach for.
- `_lastRemovedStacks` became `_lastRemovedValue`, summed at all three removal sites (Cleanse,
  Dispel, RemoveStatus) as `RemainingDotDamage` — per-tick Magnitude × rounds left. Zero for
  anything carrying no damage, rather than a stand-in nobody authored.
- The JSON key keeps its spelling. `scaleByLastRemoval` always meant "scale by what the last
  removal took"; only the unit was wrong, and renaming it would have broken the two effects the
  repair exists to fix.
- `CheckRemovedValueScalar` measures it: a Burn of known magnitude and duration stripped and
  converted, healing **3216** at two rounds and **1608** at one. Halving the duration halves the
  heal, which is what proves it reads the REMAINING damage rather than one tick or a constant.

**Cyrra's active was stripping the FRONT enemy, not the most-burdened one.** `HighestDoTDamageEnemy`
meant "the enemy that has DEALT the most DoT this round"; her text asks for *"the enemy with the
highest remaining Burden damage"*. Different question — and because she converts what the removed
DoT would have dealt into healing, the pick decided how much she healed, not merely where she aimed.
- **Audited both damage-dealt modes.** `HighestDamageDealtEnemy` has one user, Hale, and he reads it
  correctly: *"move Quarry to the enemy that dealt the most damage that round"*. `HighestDoTDamageEnemy`
  had only Cyrra, who wanted the opposite. **So the meaning was sound and its use was wrong** — the
  reverse of the `ScaleByLastRemoval` case, and the audit is what told them apart.
- **Worse than a near miss.** `DoTDamageDealtThisRound` resets for everybody at `BeginRound` and only
  fills when a unit carrying that enemy's Burn takes its own turn, so at Cyrra's turn it is normally
  zero across the whole enemy line and the mode collapses onto the first living body. She was not
  stripping the wrong burning enemy; she was stripping the front one, and often one carrying nothing.
- **Added `TargetMode.HighestPendingDoTEnemy`** — the enemy carrying the most DoT damage still
  queued on it, ranked with `Sim.RemainingDotDamage`, the same helper `_lastRemovedValue` sums when a
  removal happens, so "which enemy has the most to remove" and "how much was removed" cannot drift
  apart. Nobody when no enemy carries any, rather than falling through to the front body.
- **Renamed `HighestDoTDamageEnemy` to `HighestDoTDealtEnemy`**, same ordinal, meaning untouched.
  That name was the trap and it caught somebody; it now cannot be misread for the carried-DoT
  question sitting next to it. No content uses it.
- **A new guard, because the rename made a silent failure live.** `EnumUtil.Parse` falls back to the
  default rather than throwing, so a file still naming the old spelling would aim at the front enemy
  without a word. `CheckTargetModeStringsParse` reads the content JSON as text — the parsed database
  is where the evidence has already been destroyed — and asserts every authored target string
  round-trips through `TargetMode`. It read 554 strings, and asserts it read some before reporting,
  for the reason the role scan does.

**The sweep was blind to all of single-target targeting, and the Cyrra fix is what found it.**
Correcting her mode moved **none** of her sweep lines. Measured, not assumed, by reverting her and
re-running. Two faults in the arena, both now repaired, and all 105 baseline lines move as a result:
- **The warden's Taunt was permanent** — re-applied every turn — and **the anchor overrides every
  single-enemy target mode there is**. Every hero that picks one enemy was resolving onto the warden
  regardless of what its kit asked for. Its Taunt is now a BattleStart application lasting two
  rounds and never renewed: the anchor path is still exercised, and the rest of the fight lets a kit
  aim where it says it aims.
- **Every enemy carried identical DoT**, so a carried-DoT pick and a front-enemy fallthrough
  resolved to the same body. The back enemy now carries a long shallow self-Burn nobody else has,
  which costs it about 2000 HP of 300000 across a fight and makes a whole axis of targeting visible.
- **Re-verified after the repair:** reverting Cyrra's mode now moves exactly her five lines and
  nobody else's. That is the sweep doing the job it was built for, on the change that exposed it.

**Shipped: the authored-hero sweep, and the reason it had to exist.** The 24-stage fingerprint
sweep fields a fresh profile, which deploys three starters — so it measured **three of the
twenty-one authored heroes**. "0 of 72 fingerprints moved" was therefore true of a change that
altered two other heroes outright. The criterion was weaker than it read.
- `Tools/DevHarness/hero-sweep-baseline.txt`, 105 lines: every authored hero × 5 seeds, in one
  fixed arena, hashed FNV-1a over the whole event stream with the hero id in plain text.
- **A golden file, deliberately.** The stage sweep re-runs a seed twice and asserts the two agree,
  which proves reproducibility within one build and says nothing about whether this build differs
  from the last. Only a committed record answers that. It sits beside `doc-lint-baseline.txt`.
- **A MOVED LINE IS A BEHAVIOUR CHANGE AND NEEDS A STATED REASON, NOT A REGENERATE** — which hero,
  which clause, why the new behaviour is right. `EMBERFALL_REGEN_HERO_SWEEP=1` exists so an
  intended change lands in one step; reaching for it to quiet a surprise is how a guard stops
  guarding.
- **Neutral purpose-built fillers, not heroes from the roster.** Four stat-sticks with no passive,
  no talent and an unaffordable active, loaded from their own database and handed over on the
  `HeroInstance`, so they never enter `heroes.json`. Real heroes as fillers would mean a change to
  any filler's kit moved all 21 lines at once. A standing check asserts no two heroes share a
  fingerprint at the same seed, which is what proves each line measures its subject rather than
  the squad around it.
- **Real balance, not a zero-variance one** — the opposite of what the small mechanic arenas use
  and right for the opposite reason. Those isolate one rule and want no noise; this wants
  sensitivity, so every hit draws from the RNG stream and a change that merely shifts consumption
  moves the hash instead of hiding inside a zeroed term.
- **Cost, measured:** 105 battles, **~25 ms**, against a harness that runs in 6.9 s. Full
  build-check went 9.0 s to **10.4 s**. The baseline file is 4.6 KB.
- **The honest limit, stated in the check's own output:** it covers each hero's behaviour IN THAT
  ARENA — one squad, one enemy line, five seeds. A clause needing a Taunt enemy, a particular
  aspect matchup, a second copy of its own hero or an ally it does not have here is still
  unmeasured. It raises coverage from three heroes to twenty-one; it is not proof that a kit does
  everything its description says.

**THE OTHER BEHAVIOUR CHANGE, and it is larger than it first looked.** `Heal` and `Shield` now
honour the `ScaleBy*` family through a shared `ScaledTerm`. They read it nowhere before, so a Heal
or Shield authoring one of those flags computed off plain Attack and the flag did nothing at all.
**Six shipped effects author one, across four heroes, and TWO ARE LIVE ABILITIES** —
`brenna_deep_resolve` (passive1, a Shield off `scaleByLastBanked`) and `cyrra_bury_the_burden`
(active, a Heal off `scaleByLastRemoval`). The other four are talents, inert until selected. Every
battle Brenna or Cyrra fights changes; no fingerprint moves only because neither stands in the
sweep's starter squad, which is coverage rather than safety. The count is pinned, so another
cannot appear unnoticed. Left in rather than reverted, because the flags are authored
intent and silently ignoring them is the older bug.

**The pin moved from 6 to 7 when Rovan was authored**, which is what a pin is for.
`rovan_deflected_conversion` is a live passive Shield off `scaleByLastDamage` and the first entry
on that list written AFTER the flag started working, rather than caught by it. An eighth still
means somebody authored a flag that used to be inert.

### Open: `content/batch-six-four-heroes` — unmerged, off `engine/nine-gaps`

Two things shipped on it, in order: the three loose ends the nine-gap pass left unwired, and
then the four heroes those fixes unblocked.

**First, the three loose ends.**

Three of the eleven were the same mistake in the nine-gap pass — a mechanism built, its hook left
unwired — and they are fixed here. 72 of 72 fingerprints unmoved, 105 sweep lines unchanged, the
same 11 harness reds, three new count-based arenas.

- **A link's charge is now spent by the TRIGGER path.** `SpendLinkCharge` was called only from the
  Redirect, Echo and Deflect funnel branches, so a link read by `OnLinkedUnitHit` kept its charge
  and answered every hit for the link's whole life. `TriggerPassive` now reports whether anything
  fired — one caller reads it — and `DispatchLinkPassive` takes a charge **only when a passive
  actually answered**. A holder with nothing listening, or whose listener was refused by its own
  once-per-round clock, has not used its record and does not pay for the event. Measured: one fire
  against the uncharged control's five, one `Linked` expiry, and **zero** expiries in a third run
  where the link is charged and nobody listens.
- **`EffectKind.EchoDamage` now raises `OnEchoFired`** on its caster, and the trigger is renamed
  from `OnLinkEchoFired` and widened to cover both routes — same ordinal, nothing authors it.
  "Whenever his attack triggers against a secondary target through Redirecting Shot **or** Change
  the Course" is one sentence and one passive; a trigger that heard the link half and not the
  effect half split one description in two. Measured: four fires against four echoes, and zero in
  a one-enemy control where the echo never fires.
- **`Deflect` now goes through `RedirectShare`** instead of calling the funnel itself, which is the
  divergence the extraction existed to end. **Covered's six shipped effects are unaffected: the
  helper's body is untouched, this adds a third caller.** Two deliberate consequences of going
  through it — the share is delivered with `critical` false, as Cover's and Redirect's always have
  been, and the rule owner is recorded as the link's creator. The remainder is still discarded
  after the call, which is the one thing a deflect does that a ward does not. Measured: four
  `OnRedirectReceived` fires against four shares, and the aimed-at hero still takes zero.

### Then, on the same branch: the four heroes ARE authored

Talren, Vaelis, Nysera and Rovan carry full kits. The ten clauses two passes stopped on were
closed three ways — **three engine additions, five description edits, two authoring idioms** — and
what could not be closed is named at the end rather than approximated into the content.

**72 of 72 stage fingerprints unmoved**, and that claim is now reproducible rather than
remembered: `EMBERFALL_DUMP_STAGE_FP=1` prints all 72, so before-and-after is a diff of two runs.
**The sweep went from 105 lines to 125** — the four new kits, each distinct from every other hero
at every seed. Harness reds went from 11 to 32; all 21 new ones are dormancy tripwires reading
"no shipped effect uses X", and every one of them now has a user, which is what authoring these
four means. No check that measures behaviour is red.

#### Three built, and each one is measured

- **The redirect's OWNER hears it: `PassiveTrigger.OnRedirectSent`.** A redirect has three parties
  — the body it came off, the body it landed on, and the hero whose ward or link moved it — and
  only the second had a hook. Raised in `RedirectShare` beside `OnRedirectReceived`, with
  `_lastMoved` for the owner already holding the share, and **skipped when the owner IS the
  destination**, which is every ordinary Covered ward: that unit has already been told, and firing
  both would have one hero answer one event twice. Measured at four fires against four shares in
  the split arena and zero in the ward control.
- **`moveDuration`: a moved status can be re-clocked as it lands.** Zero keeps the entry's own
  remaining rounds, which is every move authored before this and the guarantee that a move creates
  no value. Six abilities move an UNKNOWN set of statuses and name a duration in the same breath,
  and the remove-and-reapply idiom cannot serve them — an `ApplyStatus` has to name the kind it
  applies, and these do not know it. Magnitude, `Stored`, charges and every record still cross
  untouched: only the clock is authored. Measured at 1 round against the control's 6, same
  magnitude both ways.
- **`EffectCondition.SubjectRoleIs`: the engine can ask what a unit IS.** It could test HP,
  statuses, stacks, energy, speed, shields, action history, turn position and what a mark recorded,
  and it could not ask whether a body is a Mender. The nearest thing, `StoredRoleMatches`, compares
  a role somebody previously WROTE DOWN against a constant. Reads `CombatUnit.Role`, rebuilt from
  content every battle, so it reaches no save byte. Measured by a role-gated bonus landing on the
  Bulwark and not on the Striker in the same blow.

#### Five description edits, in `import/Heroes.json`

Pinoy's words, changed only where the clause asked for something the engine does not have and the
nearest true sentence is as good a hero. Each is a REWORDING, not a deletion.

- **Talren II** recorded "one additional enemy with the lowest HP" — a third body that the first
  two records must not already have taken, and nothing can exclude an already-recorded body from a
  pick. Now records **the highest-HP enemy**, which no other clause of his competes for, and the
  extra strike goes to the recorded target with the highest present HP.
- **Nysera's first passive** read "each Absence increases her damage **against enemies below 50%
  HP** by 5%". `OutputModStatus` filters on marks and nothing else, and expressing the HP gate as
  a separate additive hit runs into the linear-per-stack problem instead. Now "each Absence
  increases her damage by 5%", which is the in-pipeline modifier and so rides a crit.
- **Rovan's basic** turned part of a Deflected enemy's blow into a Shield on him **instead** — an
  attacker-keyed damage REDUCTION, which nothing in the engine does (`Vulnerable` is source-keyed
  and only increases). The "instead" is gone: he gains a Shield equal to 20% of the damage dealt,
  the first time that enemy damages an ally each round.
- **Rovan III** sent the expiring Shield to the next-lowest-HP ally "instead of returning it to
  himself". `ApplyDelta` moves numeric fields only, so a talent cannot override a `TargetMode`.
  Now he **also** grants that ally a Shield worth 8% of his Max HP, and the return still happens.
- **Vaelis's first passive and talent III.** A fact about one unit cannot be stored on another —
  `RecordFact` writes the subject's fact onto the subject's own entry — so "records the triggering
  ally's role" and then "an ally in a different role than the previously recorded role" could not
  be brought together. The passive now names its three role groups outright
  (Bulwark/Vanguard, Mender/Warden, any damage role), which `SubjectRoleIs` banks into three
  counters at the moment Departure triggers, and talent III fires **from a Mender or Warden ally**.

#### Two closed by authoring rather than by engine work

- **Linear per-stack scaling.** `PerStack` is `Power x (1 + p*n)` and "35% of Attack per Route" is
  `0.35n`. Talren III is three gated effects at 0.35 each, one per Route threshold — the shape
  Kael's Rubble already uses. No engine change, and no description change either.
- **`EnergyGain` reads no `ScaleBy*`.** Rovan II grants "8 Energy for every 10% Max HP Shield
  moved, up to 16", which is two grants gated on `LastMovedAboveHpFraction` at 0.10 and 0.20. The
  cap is that there are two of them.

#### The three live approximations, measured before and after

Each was a shipped clause saying "when damage is redirected" wired to `OnDamaged`, which fires on
every blow. `CheckRedirectApproximations` runs each on the real roster in the sweep arena, twice —
as it ships, and with the same definition patched back in memory to what it replaced. The pair is
the evidence; a fire count alone says nothing.

- **Marrow II, "the attacker loses 5 Energy":** 105 fires against 110 blows before — every blow
  that did not kill him — and **16** after. Eighty-nine drains the clause never asked for.
- **Nereia's first passive, "receives redirected damage OR heals an ally below 40% HP":** one
  `OnDamaged` passive counting 171 blows and **no heal at all**. Now two halves of one sentence:
  **22** on shares received, **87** on heals to an ally under 40%.
- **Nereia III, "80% of the redirected damage":** it was 80% of ATTACK, the flag absent. 19275
  total across five seeds before, **12255** after, now that `scaleByLastRedirect` reads what
  Change of Course actually moved.

#### The sweep's seed is keyed to the hero ID now, and that cost one regenerate

The seed was `4400000 + index * 97 + seed`, and the list is sorted by id — so **authoring a hero
re-seeded every hero alphabetically after it**. These four moved twenty lines belonging to Rheia,
Seraphine, Velis and Zella, whose kits nobody had touched. Measured rather than assumed: with the
four still placeholders, the same engine change moved **exactly Nereia's five lines and nobody
else's**, which is also the proof that the Marrow/Nereia rewiring touched only its own heroes.
Marrow's own lines did not move because his is a talent and the sweep selects none.

A guard that cries wolf on every insertion gets regenerated, and a baseline regenerated to quiet a
surprise has stopped guarding. Keyed to the id, a hero's seeds are its own for as long as it keeps
its id. Every line in the baseline moved once, on this pass, and that is the last time an
insertion moves one.

#### Named, not approximated

- **Nereia III's cap.** "This damage cannot exceed 15% of Nereia's Max HP" has no field to live
  in: `MaxHpCap` ceilings the `TargetMaxHpRatio` term against the TARGET's health, and nothing
  bounds an amount against the CASTER's. Measured before leaving it: at level 40 in the sweep
  arena the cap is 1955 and the largest single hit the talent dealt is 1383, so it does not bind
  there — but it is uncapped, and an overkill big enough would pass it.
- **Vaelis's basic re-arms on a stale body.** "If Vaelis attacked a different enemy on his previous
  turn" is guarded with `SubjectIsMyPreviousTarget`, which tests that a previous target EXISTS.
  `PreviousTargetId` is only written when the target actually changes, so on a turn where he
  strikes the same body as last turn the engine cannot tell that from having struck it once, and
  Departure re-arms on the older body. The engine's own target history collapses repeats; the
  difference is not expressible against it.
- **Nysera's concealment chain is unexercised by the sweep arena.** `AUTHORED-KIT EXERCISE` reports
  `nysera_unaccounted_for 0`: her Mark goes on the highest-Attack enemy, that enemy aims at the
  lowest-HP ally, and at level 40 she is never it — so Vanishing Mark's condition is false all 80
  times its trigger reaches her. The concealment rule itself is engine code with its own arena;
  what is unmeasured is her chain through it. `vaelis_opening_moves 0` is the same kind of fact:
  no enemy dies in that arena.

#### Tooling repaired along the way

- **The enum-string scan covered one enum of fourteen.** It existed because a `TargetMode` rename
  made a silent failure live — `EnumUtil.Parse` falls back to the default rather than throwing —
  and the same trap is set on `condition`, `trigger`, `status` and eleven others: a misspelled
  condition never gates, a misspelled trigger never fires, and both look authored. It is
  structural now rather than textual, because `kind` means `EffectKind` inside an effect and
  something else on a unit. **554 strings scanned before, 2534 now**, all of them members.
- **`validate_heroes.py` was blind to every hero using the nested `skills` block.** `_get_skills`
  returned that block wholesale, so an engine-format hero carrying one had no `basicAttack` as far
  as the validator could see — it reported that, then checked the star shape against a hero it
  could not read. Seven heroes were authored that way before anyone noticed. The block is an
  overlay that cannot shadow a flat slot now: 70 findings became 62, and all seven read OK.
- **`AUTHORED-KIT EXERCISE`**, a report and deliberately not a guard. The sweep proves a line is a
  kit's own and has not moved; it cannot say the kit DID anything, because a passive wired to a
  trigger nothing raises fingerprints exactly like one that fires every round. Four kits on six
  triggers this roster had never used is where that distinction matters most.

### Open: `content/batch-seven-four-heroes` — unmerged, off `content/batch-six-four-heroes`

Kiva, Sylven, Merek and Oris carry full kits, and Zella's shipped `Nothing Stays Settled` was
repaired alongside them. Off batch six with `main` merged in, so the roster source file is
tracked here.

**The 72 stage fingerprints MOVED for the first time in these passes, and the reason is Kiva.**
She is one of the three starters the 24-stage sweep deploys, so replacing her placeholder with a
real kit necessarily moves every stage battle. Attributed rather than assumed: reverting Kiva
alone to her placeholder and re-running reproduces batch six's 72 fingerprints **byte for byte**,
so nothing else in this pass — five engine changes, three other heroes, Zella — moves a stage
battle. **The sweep went 125 → 145 lines**, the twenty being the four new kits, with no existing
line moved.

#### Four gaps built, each measured

- **`TargetMode.LowestEnergyEnemy`.** The enum had `HighestEnergyEnemy` and `LowestEnergyAlly` and
  never the third corner. Two of Oris's clauses ask for it by name.
- **`transferDuration`.** The single-kind sibling of `moveDuration`, missed when that was built.
  Measured at 2 rounds against a control's 7.
- **`preventExtend`.** Stamps the entry, not the caster, so the refusal travels with the status.
  Refused silently: a skipped entry is not counted, so the extension event still reports what
  actually moved.
- **`OnKill` fires inside the defeat window.** It used to fire after `Statuses.Clear()` and after
  `_defeated` had been restored, so it could not ask one question about the body it had just
  killed. Raised at the call site rather than moved into the `!_inDefeatDispatch` block, because
  that block is skipped for a re-entrant death and OnKill must still fire for one. Measured: a
  payout gated on the corpse's mark pays out, and pays nothing when the mark is absent.

**Nothing authors OnKill in shipped content, and that was measured rather than asserted:** with
all four gaps built and the content untouched, 72 of 72 stage fingerprints and 125 of 125 sweep
lines were unchanged.

#### Two things the build found that were not on the list

- **`EffectDef.Clone()` was dropping nine fields.** Clone is not a debug helper — talent overrides
  and appends rebuild a modified ability through it — so a field missing from it is silently lost
  for exactly the players who select that talent. Vaelis's talent I appends to a basic whose
  `LinkUnits` sets `linkMutual` and `linkTransfer`, so Departure stopped being a mutual link for
  anyone who took it; his talent II overrides the two `MoveBuffs` effects carrying `moveDuration`,
  the field that talent is about. A reflection guard now sets all 81 public fields to non-default
  values, clones, and compares, so this class of bug cannot come back.
- **`TransferStatus` refused a corpse, which is the only thing content uses it for.** `!target.Alive`
  turned away every transfer off a defeated body — and `TargetMode.DefeatedUnit` is the only target
  Zella's and Sylven's transfers name. **Her passive has been in the game since batch five and has
  never once moved a status.** The pre-clear defeat window exists precisely so a status can survive
  its carrier's death; the door into it was bolted. Now the one dead unit accepted is the one the
  window is currently about, so every other transfer refuses a corpse exactly as before.

#### Zella's repair, measured — and why her sweep lines did not move

Her text: *"transfer its Residue and recorded action history to the highest-HP enemy **for 2
rounds**. Transferred Residue **cannot be extended**."* Neither clause was in her content, because
`TransferStatus` had no field for either, and the omission was silent.

On her real kit, in an arena where a body actually dies, with the two fields stripped back off in
memory for the control:

- the transfer fires in both runs (1 / 1)
- **BEFORE: the moved Residue landed with 6 rounds. AFTER: 2.**
- **BEFORE: an ExtendStatus stretched it — 1 entry. AFTER: 0**, with the other Burn on the same
  body still extended, so the refusal is the entry's and not the caster's.

**Her five sweep lines did not move, and that is a fact about the arena rather than the repair.**
The sweep's enemies have 180,000 HP and more; nothing dies in it, so `OnEnemyDefeated` is never
raised and her passive is never reached. The authored-kit exercise report says the same about
Sylven's `persistent_edge 0`. **The sweep is blind to the whole defeat-triggered family** — Kiva's
two talents, Zella, Sylven, Vaelis, Merek, Rovan — which is the same shape of blindness the Cyrra
pass found in single-target targeting. It is named here and deliberately NOT fixed in this commit:
giving the arena a killable body moves all 145 lines, and this commit's claim that nothing but the
four new kits moved would stop being checkable.

#### One reorder, asked rather than chosen

Sylven's Bleed *"records the target's last positive effect **before** it takes damage"*, and the
engine recorded after the tick. Put to the author as a four-line reorder or a two-word edit; the
reorder was chosen, so the record is now taken on the board as it stood before the tick landed and
a Shield the Bleed destroys is still what gets recorded. No Burn in shipped content carries a
`TickRecord`, so nothing moved underneath anyone.

#### One scalar, because Sylven's core needed it

`LedgerDeposit` kept its own inline list of `ScaleBy` flags and had never gained
`ScaleByLastRemoval`, so a Scar Record deposited "the Bleed's remaining damage" would have banked
a number from a different question. **Not** routed through the shared `ScaledTerm`, which would be
the tidier fix: the inline list multiplies by Power for release and banked and does not for
redirect, and `brenna_deep_resolve` is a shipped `LedgerDeposit` authoring `scaleByLastRedirect`.
Sharing the resolver would have changed a live passive. The divergence is left standing and named.

#### Five description edits, in `import/Heroes.json`

Eight of the file's 374 descriptions moved; the diff is eight lines.

- **Merek's basic** drained 15 Energy after an active resolved. Casting sets Energy to 0, so it
  took nothing from an empty bar: now a 10% turn-progress delay. **His talent I fell with it** —
  it answered "when Anticipated removes Energy" and gated on an absolute Energy threshold, which
  is a gap left unbuilt.
- **Kiva's talent II** named an ability she does not have: *No Way Back* → *Cut the Escort*.
- **Kiva's basic** gave Forsaken immunity to redirection "from another enemy"; nothing in
  `enemies.json` applies Covered or creates a link, so it guarded nothing. Forsaken is a marker,
  which is what the rest of her kit reads it as.
- **Oris** measured time in "% of the normal turn interval" where every other hero says "% turn
  progress", and the engine has one primitive behind both.
- **Oris's talent III** asked for the enemy whose Energy moved most during the sequence — per-unit
  delta tracking plus a ranking mode, the most expensive gap on the list, for one talent. It now
  names the body the second altered attack struck, which is that body.

#### Ten sub-clauses authored as nothing, each blocked on a gap left unbuilt

Named rather than approximated into the content, and listed in `Tools/_author_batch_seven.py`:
Kiva's "since Kiva's last turn" and her "up to 130% of Attack" cap; Merek's two swap riders (one
of them his whole talent II), his recorded-progress payout, and his round-start designation;
Merek's basic answering `OnEnemyActive`, which is raised before the cast resolves where his text
says after; Oris's talent II, both halves gating on absolute Energy; Oris's "once every 2 rounds";
Sylven's Healing Reduction and his "+20% on a transferred Bleed".

### Then, on the same branch: the sweep can see a defeat

**All 145 sweep lines moved, and the cause is the arena.** The commit contains one source file
and the baseline it regenerates — no content, no engine — so nothing else could have moved one.
Proved rather than argued: the PREVIOUS harness binary, run against this same content, still
reports *"all 145 sweep fingerprints match"*. Old arena plus this content is the old baseline; new
arena plus the same content is 145 new lines.

**Nothing ever died in it.** Every body in the enemy line had 180,000 HP or more, so
`OnEnemyDefeated` was raised in none of the 145 runs and the sweep was blind to every clause that
answers a defeat. Three shipped passives live there — Zella's `Nothing Stays Settled`, Sylven's
`Persistent Edge`, Vaelis's `Opening Moves` — and all three read **0** in the exercise report.
Zella's had been inert in the engine for two batches and the sweep had no opinion about the repair,
because it had never once reached the rule.

**The self-buffer is now the body that dies, rather than a sixth body being added.**
`_cfg.MaxTeamSize` caps an enemy line at five and takes the first five, so a sixth entry is not a
sixth enemy — it is the warden silently dropped, and with it the Taunt anchor and the long shallow
Burn the previous two repairs to this arena put there. That was tried first and measured: the line
kept five bodies and the warden was gone. The straggler carries the buffer's self-AttackUp, so the
buff-gained path is raised as often as before; it stands first in the line and lowest in HP, which
is what makes it the body `FrontEnemy` and `LowestHpEnemy` clauses find, so the corpse carries the
marks and Bleeds that the defeat clauses then read.

**It dies on a schedule, not on hero damage, and that is the part worth stating.** Hero output in
this arena is far smaller and far more varied than it looks — a whole fight puts about 7,000 damage
into a front body — so no fixed HP both survives a Striker's opening blow and falls to a Mender's
whole fight. Measured at 15,000, 40,000 and 80,000 authored HP: nothing died at any of them. It now
carries a `TurnStart` passive that deals four times its own Attack to itself, so it falls on its
second turn in **every** run regardless of who is standing opposite, and a new check asserts that
per run rather than in aggregate — one hero failing to finish it is exactly the failure this is
tuned against. Self-damage rather than a self-Burn, deliberately: a heavy Burn on it would have
made it the enemy carrying the most pending DoT and taken that distinction off the warden, undoing
the Cyrra repair.

**What now fires that did not:** `zella_nothing_stays_settled` 0 → **5**, `sylven_persistent_edge`
0 → **5**, `vaelis_opening_moves` 0 → **5**, once per seed. Rovan's `OnAllyDefeated` half already
fired and still does (18). **Kiva's and Merek's defeat clauses are talents and still cannot fire
here**, and no arena change fixes that: the sweep selects no talent, which is why the exercise
report excludes them.

The 72 stage fingerprints are untouched — the hero-sweep arena is not the campaign — and the harness
red list is identical, line for line, at 35.

### Open: `engine/target-restriction` — unmerged, off `main`

**A rule saying a unit may not pick a particular body.** Engine only; no hero is authored on it.
**72 of 72 stage fingerprints unmoved and 145 of 145 sweep lines unchanged**, both dumped and
diffed against `main` rather than recalled — which is the claim that matters, because this sits on
the path every fingerprint measures and all 29 authored heroes and every enemy resolve targets
through it.

**The primitive, not one clause.** Three descriptions say a unit may not pick something — the same
target twice in a row, a unit carrying a status, the unit it picked last round — and all three are
one shape: a predicate on a candidate, owned by the picker, asked at pick time. `TargetBan` is
`None / LastTarget / LastRoundTarget / CarryingStatus`, declared on a `PassiveDef` and adopted onto
the unit the way `OutputModStatus` already is, because a rule of this kind is a property of the
unit and not of one ability.

`LastRoundTarget` needed a third memory. `LastTargetId` is overwritten as each blow is set up, so
it names the body being hit right now; `PreviousTargetId` is the last DIFFERENT body and is not
written at all when a unit hits the same one twice. `LastRoundTargetId` is snapshotted in
`BeginRound`. The arena proves the two are not one rule twice: over the same fight, `LastTarget`
changes body **7** times and `LastRoundTarget` **3**.

#### Where it sits: with concealment, before the anchor, and able to veto it

The filter rides with `Concealed` at all eleven single-target sites — one helper, `Rejected`, asks
both questions — and is applied to the Provoked branch and to the Taunt anchor as well.

**A Taunt holder the picker may not pick stops answering for its side.** That is the placement
concealment already has in the same method, for the same reason; a restriction the anchor outranked
would be a rule any enemy could suspend by taunting, and a compulsion to attack a body you are
forbidden to attack is a contradiction the engine already resolves in favour of the exclusion.
Measured: with the front enemy taunting, the control puts 1608 into it and 0 into the back body;
with the restriction naming the taunter, 0 and 1608.

**It governs picks, not sweeps.** `AllEnemies` takes a side rather than picking, and a ban that
thinned a sweep would be damage immunity wearing a targeting rule's clothes — again the rule
concealment already set. `RandomEnemy` is the one single-target mode that goes through the
multi-target collector, so it filters before the draw: the roll is over the legal set.

#### What a unit with no legal target does: it picks anyway

The restriction is dropped for that one pick. The alternatives were a turn that evaporates and a
pick made arbitrarily; a fight where one side cannot act does not end, it runs to `maxRounds` and
reports a result no decision produced, and an arbitrary pick is not definable in a simulation that
must replay identically. **This is exactly the trade concealment makes one method down** — "the
last body is always targetable" — decided the same way and for the same reason.

The retry runs only when the ban actually refused somebody, so a mode that legitimately resolves to
nobody does not resolve twice, and only for a unit carrying a restriction at all, so nothing in
shipped content reaches the second call and no mode that draws from the RNG stream can draw twice.
Measured: with every enemy carrying the forbidden status, the blow lands for **1608**, the same as
the unrestricted control — the rule is dropped, not bent.

#### It was not a second copy. It was a third, and now the audit is standing

Asked before merge whether `ClonePassive` and `EffectDef.Clone` were the complete set, the answer
was no. Searching for the SIGNATURE of the bug — a run of assignments whose left and right sides
name the same member — turns up sixteen such runs in six files. Split by whether the copy is
same-type or a projection:

**Same-type copies, where every field must be assigned — four of them, now all complete:**

| copier | type | was |
|---|---|---|
| `EffectDef.Clone` | EffectDef, 81 fields | dropped 9 |
| `ClonePassive` | PassiveDef, 26 fields | dropped 3, then 2 more |
| `CloneAbility` | AbilityDef, 7 fields | complete |
| `MoveStatus`'s carry block | StatusInstance, 32 fields | **dropped 9** |

**`MoveStatus` is the one shipped content can reach.** `Ledger` is not harmful, so `MoveBuffs`
moves it; Vaelis's active moves positive effects between two enemies and Sylven's Scar Record IS a
Ledger. A moved one arrived with its stored damage and with `AutoRelease` off, along with every
other rule about how it banks and what it mitigates — **a record that still remembered the number
and had forgotten it was supposed to pay it out.** The carry block's own comment promised that
everything the entry recorded travels with it; what it recorded did, what it was told to do with
it did not. Measured now: a Ledger left alone pays 500 into its holder, and a Ledger moved to
another body pays the same 500 into the new one.

The other two, `Overrides` and `Appends` on `ClonePassive`, no passive in content carries — a
passive that is the TARGET of a talent is not itself a talent. Copied anyway, because "complete
except where somebody has checked that it does not matter" is how the other three got into the
state they were in.

**Cross-type projections are deliberately NOT in the table** — `CombatUnit.FromDef`,
`StatMath.Build`, `CampaignWaveGenerator.BuildBattle` (21 fields) and `ProvingGenerator.GhostOf`
all read one type and write another, and a field they leave behind may be correct. They are named
here so the next reader knows they were looked at rather than missed.

The audit that found the third one is kept as a check: it reads each type's public fields and each
copier's body out of the sources and asserts every field is assigned — 149 fields across the four —
with the four `AddOrRefresh` sets whitelisted for `MoveStatus` and a list counted as copied when
the body adds to it.

### Open: `content/batch-eight-unblock` — unmerged, off `main`

**Every clause batch seven authored as nothing is authored now.** 72 of 72 stage fingerprints
unmoved; **5 of 145 sweep lines moved, all of them Merek's**, and that is the whole of the
movement in this pass.

#### Ten gaps, built because a clause asked and not because enough clauses asked

The judgement that some of these were not worth building for a single want was withdrawn, so
where a gap serves one clause it was built anyway and is marked here:

| gap | built as | wants |
|---|---|---|
| absolute Energy | `EnergyAtLeast` / `EnergyBelow` | Oris II, both halves |
| partial healing reduction | `StatusKind.HealingReduction`, read beside `HealSuppression` | Sylven III, Demos |
| readable turn-shift amount | `_lastTurnShift` per caster + `scaleByLastShift` | Merek p1 — **one clause** |
| naming the unit a swap moved | `SwapAdvancedUnit` / `SwapDelayedUnit` | Merek's active and II |
| magnitude change on transfer | `transferMagnitude` | Sylven p1 — **one clause** |
| healed-or-shielded since my turn | `SubjectHealedSinceMyTurn` / `SubjectShieldedSinceMyTurn` | Kiva's active — **one clause** |
| a cap in the caster's currency | `attackCap` | Kiva's active, Merek p1 |
| the round's own boundary | `PassiveTrigger.RoundStart` | Merek p2 — **one clause** |
| the far end of an enemy cast | `PassiveTrigger.OnEnemyActiveResolved` | Merek's basic — **one clause** |
| a cadence on a one-off blow | **no engine work** — `PassiveLimit.EveryNRounds` already existed, and the talent carries both the marker it reads and the limit | Oris III |

**`SubjectRoleIs` stays at ordinal 40.** The first attempt inserted the four new conditions before
it and moved it to 44; ten shipped effects of Vaelis's read that member, so they were moved to the
end of the enum and a pin now asserts it.

#### What moved, and what did not

**Merek's five lines**, for four changed clauses at once: First Claim is designated at the start of
the round rather than at his turn (`merek_first_claim` 25 → 28 fires), Anticipated answers the far
end of a cast, the swap now marks the body it pushed back, and his basic carries the
recorded-progress blow (`merek_unsettled_timing` fires twice in the sweep).

**Kiva is a starter and her stage fingerprints did not move**, which is worth stating rather than
assuming: her changed gate is "shielded or healed since my last turn", and she acts before the
arena's healer every round, so the stamp it reads is always the previous round's and the gate is
false either way. The `attackCap` under it does not bind at that scale either.

**Sylven's and Oris's lines did not move** because everything they gained is a talent, and the
sweep selects none.

#### Two gaps from the list that are NOT here

Gaps 12 and 13 of the batch-seven survey — redirect immunity and Energy-delta ranking — serve
clauses that no longer exist: batch seven's description edits removed Kiva's "cannot receive
damage redirection" (nothing in `enemies.json` can protect another enemy, so it guarded nothing)
and reworded Oris III away from "the enemy whose Energy changed the most". Building them would
mean restoring two clauses of Pinoy's text that were deliberately retired. Named here rather than
done quietly; if those clauses come back, both gaps are still unbuilt.

### Placeholder-period notes (until abilities are authored)

- **`validate_heroes.py` is red on 62 findings, all of them star-shape.** Every hero still on a
  placeholder is missing passives and/or talents the star contract requires. This is the expected
  state while abilities are placeholders and closes as real kits are authored: it was 86 before
  any kit was written and 70 before this pass. Every authored hero now reads OK, including the
  seven the validator could not previously see at all.
- **Twelve 2-star heroes carry neither an active nor a passive.** The star contract says 2-star
  heroes should have passive1; placeholder bodies have no passive at all. These twelve are a
  subset of the findings above.
- **Two heroes carry weapon `"spellbook"` which is not in `UnitRig.BuildWeapon`'s switch.**
  `AttacksAtRange` recognises spellbook for range detection, but `BuildWeapon` does not build a
  visual for it — they fall through to the default. Either add a spellbook case to `BuildWeapon`
  or remap them to `staff` in the content.
- **OnKill, Dispel and Resolve** are proven synthetically. Shipped content does not reach them
  (no hero authors OnKill, no hero applies a beneficial status, no hero uses GainResolve). Each
  harness section prints a `[note]` for the gap. The checks resume against shipped content when
  abilities land.
- **Resolve counter independence** (two carriers on one line, each starting at stack 1) was
  covered by CheckEmberHeroKits, now deleted. The synthetic Resolve arena proves the mechanic
  for one carrier; independence resumes when authored kits put two carriers on the same line.
- **CheckEmberHeroKits is deleted.** Its five named subjects were the old Ember kits. The
  equivalent for 94 heroes is a different check written after abilities are authored.

**Open items, recorded so they are not rediscovered:**

- **25 heroes have animated sprites; Nereia does not.** The sprite delivery covered 24 non-Ember
  5-stars plus Celen (4-star Tide). `nereia_the_field_medic` (5-star Tide Mender) was not in the
  delivery and remains procedural. She is in the prologue squad, so the prologue's step-1 battle
  shows a procedural body alongside four sprite-based allies. Pinoy: Nereia needs a sprite
  delivery or a placeholder sheet before the prologue can ship.
- **All 25 sprite heroes need `spriteBodyFraction`, `spriteFootFraction` and `spriteMirror`
  hand-measured.** They currently share Aeron's placeholder values (0.53 / 0.23 / true), which
  means every hero whose proportions or facing differ from Aeron will float, sink, or face
  backward. Per-clip `impactFraction` is also unset (defaulting to 0.5). This is a separate
  art-measurement pass, not a code task.
- **Rheia has no skill clip.** Her delivery included `idle2` where `skill` should be. The game
  requests `"skill"` for active abilities — Rheia's active will play with no animation (silent
  no-op, idle continues) until a replacement clip is delivered.
- Six orphaned sprite PNGs remain under `Assets/Resources/Sprites/Units` for the deleted heroes,
  referenced by nothing now that their `visual.sprite` entries are gone.
- The pinned five-body line spans five aspects (Ember, Gale, Tide, Lumen, Ember) and five roles
  (Bulwark, Vanguard, Mender, Breaker, Stalker). Its FLOOR/CEIL/BLOCK numbers are a real
  measurement now, unlike the all-Ember line it replaced.

- **Shipped: `HeroRole` widened from seven members to ten.** The taxonomy the incoming 94-hero
  roster is authored against, in five pairs: Bulwark/Vanguard, Binder/Breaker,
  Warden/Mender, Marksman/Tinkerer, Striker/Stalker. Verified against the roster itself, which
  uses exactly those ten and no others (Bulwark 14, Vanguard 14, Mender 13, Tinkerer 13,
  Marksman 12, Breaker 9, Stalker 6, Striker 5, Binder 5, Warden 3).
  - **`Ranger` was removed, not deprecated in place**, which is the one deviation from the
    append-only rule every other enum here follows. Two things make it safe and both are now
    asserted rather than argued: it was the LAST member, so nothing declared before it renumbers,
    and **a role reaches no persisted record at all** - it is absent from the save format and from
    every battle event, living only on `HeroDef`, `CombatUnit` and the per-battle `UnitSnapshot`,
    each rebuilt from content on load. `CheckHeroRoleOrdinals` pins the six surviving ordinals,
    pins the four new ones at 6-9, and serialises a real profile to confirm no role appears in it.
    The day a role does get written to a save, that check fails and reusing ordinal 6 has to be
    answered properly.
  - **Two of the five heroes were re-roled.** Kael Breaker -> **Vanguard**: he is the fattest body
    left (1850 HP, the highest Defense of the five) and his only passive is a BattleStart self
    DefenseUp, which is a durability passive rather than a damage one; his HP-to-Attack ratio of
    11.2 sits on the roster's Vanguard band (11.7) rather than its Breaker band (9.3), and calling
    him Breaker alongside Seraphine was the one duplicated role among five heroes. Orren Ranger ->
    **Marksman**, because Ranger no longer exists and the roster's Marksman exemplar carries a bow
    and leads with a mark, which is Orren exactly. Seraphine stays **Breaker** (area damage plus
    Burn on both basic and active), Marrow stays **Mender** (the only healer in the game), Veska
    stays **Striker** (highest Attack, lowest HP, hook blades, an OnKill trigger).
  - **One consumer assumed exactly seven, and it was prose, not code.** `FirstRun.ShowRoles` said
    the starting squad "does NOT cover all seven roles" and its on-screen card read "Five heroes,
    four jobs"; both were corrected, and with Kael on Vanguard the squad now fills five distinct
    roles. Everything else was already shape-agnostic: `UITheme.RoleName` and `CombatLogger`
    stringify, `CheckContent` iterates `Enum.GetValues`, and nothing anywhere indexes an array by
    role ordinal. `UITheme.RoleColor` was a switch over six of the seven - Ranger fell through to
    grey - and now names all ten in paired hues, with the fallthrough kept but unreachable.
  - **A new guard, because the failure mode here is silence.** `EnumUtil.Parse` falls back to
    `Striker` rather than throwing, so a roster authoring a role this enum does not know loads
    clean and is wrong. The check reads the `role` strings straight out of `heroes.json` and
    asserts each round-trips through the enum. **It shipped broken once and the fix is part of
    this branch:** the first version paired each role with the nearest preceding `"id"`, which
    after the first hero is an ABILITY id, so it resolved no `HeroDef`, compared nothing and
    passed. A guard against a silent fallback that can itself go quiet is worth nothing, so the
    scan now asserts it read every hero before it reports a verdict.
  - **Harness: still the same three reds, and the first is now wider by design.** Role coverage
    reports 5 of 10 empty (Bulwark, Warden, Stalker, Binder, Tinkerer) where it reported 3 of 7.
    It was not weakened and must not be: the two new empties are roles the taxonomy added that no
    current hero fills, which is the same gap the import closes. Summon pity and the CEIL band are
    unchanged.
- **Shipped: tier-five ability mechanics.** The six mechanics the read-only pass scoped,
  and nothing else. All six are a no-op against shipped content: **0 of 678 fingerprints move**,
  and the harness report is a strict superset with no existing line changed. 35 new checks,
  six standing guards, ordinal pins extended to five enums.
  - **Turn progress is a DERIVED POSITION, not a gauge, and that was the decision worth making.**
    `Sim.TurnProgressOf` reads a unit's place in the round's live order: the unit about to act is
    1, the one at the back of the remaining tail is 0, and a unit that has already acted is 0
    because its next turn is a round away. **An ATB gauge was rejected**, and the cost of rejecting
    it is stated rather than hidden: a gauge is a per-unit float filling with Speed, which turns
    the round from a sort walked once into a scheduler, moves every existing fingerprint, and
    invalidates the 24-stage balance table outright. That is a larger change than all six
    mechanics together. **What the derivation costs instead:** progress is quantised to the turns
    left, so with four still to act the only readable values are 1, 0.75, 0.5, 0.25 and 0, and
    51% is indistinguishable from 74%. "Above 50% turn progress" becomes "in the front half of
    what is left", which is what the two abilities asking for it want.
  - **Turn Debt is a status the engine reads**, like Burn and Regen, not a passive. `StatusKind.
    TurnDebt` carries the debt in `Stored` and the exchange rate in `Magnitude`; the debtor pays
    Energy at its own turn start and the entry clears. It has to be an engine rule because the
    repayment happens on the DEBTOR, an enemy that knows nothing about the hero who pushed it.
    `EffectDef.ShiftBanksAs` is what makes "delays by 12%, applies Turn Debt equal to the progress
    removed" one effect: the debt is the REALISED shift after the ceil and both clamps, not the
    authored fraction, because an effect that asked for 12% on a two-unit tail moved one place and
    took 50%.
  - **The round ordinal is a counter on the STATUS, not on the passive.** `PassiveLimit.
    OncePerRound` already answers "this passive fires once a round" and is the wrong instrument:
    the thing being counted happens to a status's carrier, and the status can move, expire or be
    dispelled mid-round. `StatusInstance.UsesThisRound` counts up and resets in `BeginRound`,
    against `EffectDef.UsesPerRound`; `EffectCondition.StatusUseAvailable` reads it and SPENDS it,
    because a condition that is true every time it is asked is not an ordinal.
  - **Concealment reuses `StatusKind.Stealth`**, declared inert in an earlier pass, so it costs no
    ordinal and no save byte. A Concealed unit is skipped by every single-target pick and by the
    Taunt anchor, and is NOT skipped by `AllEnemies` - which is what "cannot be targeted directly"
    says and what keeps concealment from being immunity. **The last living body is never
    concealed:** a side nobody can hit produces a battle that runs to maxRounds and reports a
    defeat no decision caused, so the exclusion is dropped when it would empty a side.
  - **Per-target limits gate where an effect LANDS.** `PassiveDef.PerTargetLimit` with
    `PerTargetPerRound` for the window. It could not be folded into `PassiveLimit` because that is
    answered from the owner's clock before a passive fires, and this needs the target's identity,
    which does not exist until targeting has run. A passive whose budget is spent against one body
    still resolves normally against another, which is the shape all four descriptions have.
    **Caldus's identity condition was already expressible** and is not part of this: `HasMyMark`
    on a `LastAttacker` target is "whenever the Declared Target attacks me".
  - **The non-numeric ledger is four TYPED fields, not four conventions on one float.** `Stored`
    can say "247 damage" and cannot say "the buff was AttackUp", "the last action was an Active",
    "the ally was a Mender" or "the lowest this ever went was 38%". `EffectKind.RecordFact` plus
    `RecordKind` writes them; four conditions read them back. `StoredStatusMatches` compares the
    record against what the subject holds NOW rather than against a constant - a constant would be
    a condition whose answer the author already knew, and would make the record decorative.
    Everything recorded travels through `MoveStatus`, because a record that forgets what it
    recorded when it changes carrier is not a record.
  - **Mark-jump-on-death cost what the read-only pass said it would, and the shortcut it offered
    does not exist.** The suggestion was that a non-death trigger might serve five of the six.
    **It serves one.** Only Rovan's says "when Covered expires OR its bearer is defeated"; the
    other five say defeated and nothing else. So the death path was built. What it took: the
    `Statuses.Clear()` moved after a new dispatch point, two broadcast triggers
    (`OnEnemyDefeated`, `OnAllyDefeated` - split because four abilities watch an enemy die and one
    watches an ally), `TargetMode.DefeatedUnit` and `ConditionSubject.Defeated` to name the corpse,
    a re-entrancy guard so a death inside a death does not overwrite the corpse being dispatched,
    and the one Alive gate in the engine that a corpse may pass - `MoveDebuffs`/`MoveBuffs`, since
    taking a status off a dead unit is the entire point. `OnKill` could not serve any of them: it
    reaches the killer, and the mark's owner is usually not the unit that landed the blow.
  - **Measured expressibility: 277 of 374 before, 353 after (74.1% to 94.4%).** Re-derived against
    this branch rather than carried over. The 21 that remain are 17 the read-only pass put out of
    scope as description changes at import time - adjacency, accuracy, guaranteed crit, buff
    suppression, interrupt, cross-unit stat comparison and Edrin's four marks - and **4 genuinely
    blocked**: intercepting a would-be event before it resolves (2, Oris and Aeron), and extending
    a live status's duration (2, Demos and Celen). Counterattack is not among them:
    `OnDamaged` plus `TargetMode.LastAttacker` is a counterattack today.
  - **Note on the earlier estimate.** The read-only pass reported 287 expressible and an 87-ability
    gap. Re-measuring with the same method against this branch gives 277 and 97. The difference is
    tighter matching on the round-ordinal and record families, not work that went missing; the
    earlier number was the estimate, this one is the measurement.
- **Shipped: the first half of the roster swap.** Six placeholder heroes are deleted from
  `heroes.json` - `orrin_kaal`, `kalyx_emberwright`, `wren_ilyra`,
  `ryn_solvarr`, `maeve_corrin`, `nyx_serrow`. Five remain: Kael, Seraphine, Marrow, Veska, Orren.
  A 94-hero roster replaces the six; nothing here is retuned for the gap in between.
  - **Three starters, one per role: Kael (Breaker), Marrow (Mender), Veska (Striker).** All three
    original starters were deleted, so a fresh account had none. Marrow is not optional - he is the
    only healer in the game now, a new account has to be able to win stage 1, and the economy pass
    measures healing by fielding him twice. Kael is the fattest body left (1850 HP and a BattleStart
    DefenseUp) and is the nearest thing to a front line; Veska is the single-target finisher.
    Seraphine duplicates Kael's role and Orren duplicates Veska's shape, so neither adds coverage.
  - **What named a deleted hero, and what happened to each.** The pinned five-body line (both curve
    instruments, now one shared `PinnedLine`), the three `startingHero` flags, the economy pass's
    healer clone, `CheckDispel`'s two heroes, `CheckOnKillPassive`'s Ryn, the Effect Hit block's
    Nyx, the version-1 save fixture's three `defId`s, the hero-count assertion, and five prose
    references in `UnitRig`, `MainMenuScreen`, `SummonScreen`, `make_portraits.py` and
    DESIGN-HERO-GENERATOR. No check was deleted.
  - **Two checks could not survive as written, and say so rather than being re-pointed at a
    stand-in.** The Effect Hit block asserted that Nyx held the roster's highest Effect Hit, so her
    mark landing was a stat rather than an unfilled field; the five heroes left have no specialist
    at all (highest hero value 0.15, below four enemies), and "somebody is highest" is true of any
    list, so it is now a loud `[note]` instead of a passing assertion. Known trap 4 - an implicit
    1.0 chance evaluating to 0.99999994 when Effect Hit equals Effect Resist - was written about
    Nyx's Hollowing because it was the only harmful status in the game authored that way. It is
    restated against content rather than a name, and reports that it currently has **no subject**:
    every harmful `ApplyStatus` left carries an explicit chance below 1.0. It starts checking again
    by itself the moment the roster authors a certain harmful status.
  - **Three checks were widened, and two engine-independent flaws fell out of that.** `CheckDispel`,
    `CheckProvokeTargeting` and `CheckStatusExpiry` were all pinned to stage 1 and to a hero that no
    longer exists, and all three now sweep stages until they find their subject. Provoke also became
    side-agnostic - `FindTargetAnchor` answers for whichever side holds the Taunt, and the Stone
    Warden is the only Taunt left in the game - and moving it to the enemy side exposed two bugs in
    the CHECK that had been unreachable while it watched a player tank who rarely died: it never
    dropped a dead taunter from its active list, and it read an ability's target set as a contiguous
    run of `Damage` events, so any multi-target ability whose first victim died read as
    single-target and was graded a violation. It grades per turn now. Neither was an engine bug.
  - **Harness state: three red, and only one of them is a test artefact.** The CEIL band check is
    inherited from the aspect removal and was not retuned alongside. The other two are genuine
    content gaps, left failing deliberately: **three combat roles - Bulwark, Warden, Stalker - now
    have no hero at all**, and **summon pity cannot be satisfied**, because it guarantees three
    distinct unowned heroes and a fresh profile leaves two. Both are arithmetic consequences of a
    five-hero roster and both close when the import lands. Weakening either would hide the thing
    the import most has to fix. These are the three reds listed at the top of this section.
  - **The balance numbers are stale on purpose and the yardstick is worse than it was.** The pinned
    line is now Veska, Seraphine, Orren, Kael and Marrow - **all five Ember**, because the roster is.
    A six-aspect game measured by a line that can be neither at an advantage nor at a disadvantage
    against anything is measuring with an axis missing, and `PinnedLine`'s own comment says so at
    length. Against the line the aspect removal was measured on, 17 of 24 stages read easier, 2
    harder, 5 unchanged; 4 of the 19 non-pinned rows sit inside their band, against 5 before; 2-1 now blocks
    at battle 119 where nothing blocked on that line. **None of it is a statement about the game.**
    The table task 13 was tuned against was measured on a Stalker, a Striker, a Breaker, a Bulwark
    and a Warden across five aspects, and all five of those heroes are now deleted, so the
    continuity the pin existed to protect is already broken. Retuning against a mono-aspect
    five-hero roster would bake that in.
  - **Orphaned art, reported and not deleted.** Six single-image sprites remain in
    `Assets/Resources/Sprites/Units` for the deleted heroes, referenced by nothing now that their
    `visual.sprite` entries are gone. All five remaining heroes have full sheets and portraits.
    `Tools/HeroValidator/fixtures/regression-batch-1.json` still carries a synthetic hero with
    Orrin's id; it is self-contained and reads nothing from `heroes.json`, so it still passes.
- **Shipped: the six engine-owned aspect rules are gone.** **NOT a no-op, and by far the largest
  behavioural change of the whole sequence.** They are deleted. An Aspect is now the damage chart,
  armor attunement and flavour, and nothing else.
  - **What came out.** Ember's Burn stacking (a status now refreshes for every source alike);
    Tide's energy redirect on overflow (overflow is discarded again); Verdant's overheal banking
    as Regen; Gale's turn re-entry on kill AND its intra-round Slow shift; Umbra's uncleansable
    statuses AND the mark jumping to the highest-Attack survivor on death; Lumen's targeting
    anchor. Seven private methods went with them - `TryGaleReentry`, `TryGaleSlowShift`,
    `CollectUmbraMarks`, `TryUmbraSpread`, `FindMarkHeir`, `IsUmbraSourced`,
    `FurthestFromCasting` - along with `AddEnergy`'s redirect body, the `_inRedirect` guard and
    `VerdantBankRounds`.
  - **Dead config removed, and it is removed rather than left inert:** `emberBurnStackCap` and
    `galeExtraTurnsPerRound` are gone from `balance.json` and from `BalanceConfig`.
    `CombatUnit.ExtraTurnsThisRound` went too - nothing could write it once re-entry was gone.
  - **What stayed, deliberately.** `FindTargetAnchor` still exists and still answers for a side;
    only the Lumen clause came out, so **Taunt and Provoked behave exactly as they did**.
    `MoveStatus` stayed - Umbra's death-jump wrote it, but `MoveDebuffs` and `MoveBuffs` are its
    callers now. `AddStack` stayed; `GainResolve` is its only caller. `BattleEventType.ExtraTurn`
    and `TargetAnchored` stay in the enum with their ordinals: they are in the permanent record of
    every battle resolved before this, and deleting a member renumbers every type after it.
    Neither is emitted any more, and both say so in their own doc comment.
  - **18 harness functions deleted, 76 assertions with them** - not commented out. `CheckOnKillPassive`
    survived the deletion of its caller and is called from `Main` now: Ryn's OnKill wiring is a
    property of the passive, not of Gale. `CheckAnchorStillOverridesNewModes` was rewritten to
    vary a Taunt instead of an aspect, and its arena's `lumenAnchor` flag became `tauntAnchor`.
  - **The standing guard that replaces them is two-part, because neither half is sufficient.**
    *Aspect-swap invariance*: one arena is built six times, once per aspect, with every unit on
    both sides carrying that one aspect. `AspectChart.Multiplier(X, X)` is 1.0 - asserted, not
    assumed - so the chart cancels and all six fights must be byte-identical. Any rule keyed on a
    unit's aspect shows up as a run that diverges, and the failure names which aspect. The arena
    is built to REACH each removed rule's trigger, and each one is asserted reached rather than
    assumed: two Burns from two sources on one body, a heal that lands short of what was authored,
    granted energy hitting the cap, a Slow on a unit that has not acted, a Cleanse stripping a
    harmful status, a unit dying while carrying one, and no Taunt anywhere on the field.
    *Source scan*: the engine's own text is read, and every mention of the `Aspect` type in
    `CombatSimulation.cs` and `CombatUnit.cs` must be one of a fixed, named list - the chart
    lookup, the multiply, the plumbing that carries that one float into the event, and three
    assignments in `CombatUnit`. The scan exists because invariance cannot see a rule keyed on the
    RELATIONSHIP between two aspects ("if mine equals my target's"), which a single-aspect field
    satisfies identically in all six runs. It is deliberately brittle: it matches whole trimmed
    lines, so reformatting an allowed line brings a human here to re-approve it. It degrades to a
    `[note]` when the source is not found next to the binary; invariance does not degrade.
  - **Measured cost, and it is severe. 675 of 714 fingerprints move** (94.5%): every one of the
    648 campaign fingerprints, and 27 of the 66 solo-hero ones. 21 battles flip outcome, all of
    them win-to-loss. On the geared curve, **19 of 24 stages get harder, none gets easier, and two
    now BLOCK** - 1-12 at battle 41 and 2-5 at battle 46 - which violates the standing "no stage
    may BLOCK" requirement. The five unchanged rows are 1-1 to 1-5, which are over-geared enough to
    sit at 100% either way. Chapter 1 CEILs fall from a 64.5-90% band to 0-49%; 2-2 goes 89% to 3%,
    2-5 goes 90.5% to 0%; the smallest move is 2-7 at -2.5 points. Eleven stages now open below a
    90% FLOOR, against zero before. **The harness is
    committed red on exactly one check** - the CEIL band - and the retune was deliberately NOT
    landed with it, so the cost is legible as one number rather than smeared across a tuning pass.
  - **The Lumen anchor is the bulk of it**, and that was visible before the change: across the 144
    battles of shipped content it fired 1177 times and put 36.2% of all damage onto a Lumen body.
    Removing it stops routing single-target enemy damage onto Orrin, the line's only tank, and
    chapter 1 was tuned with that routing in place. Verdant's banking (115), Ember's stacking (58),
    Gale's re-entry (54), Tide's hand-off (27) and Umbra's jump (5) are real but small beside it.
    Gale's intra-round Slow shift fired **0** times in shipped content and was already unreachable.
  - **One content consequence to own:** Kalyx Emberwright's identity was the Ember stacking rule -
    DESIGN-ASPECTS 3 cut his passive and left the aspect to carry him. He now has nothing on his
    sheet but numbers and targeting. Giving him a passive is content work this change did not do,
    and he has since been deleted with the rest of the placeholder roster, so it is moot.
- **Shipped: named marks, trigger limits, a stored ledger and own-outcome triggers.** Third-tier
  inventory work, all of it a no-op against the content that existed when it landed.
  - **Named marks: one kind, told apart by who applied it.** `StatusKind.Mark` and `MarkAlt` are
    inert, and "my mark" is the kind plus `StatusInstance.SourceId`, which every status entry
    already carries. **`StatusKind.Stealth` was NOT used and no MarkA..MarkH bank was added:**
    Stealth gives exactly one anonymous mark, and 48 abilities across 36 heroes need marks that
    must not collide, with any 5 of 94 heroes able to stand on one team. A fixed slot pool would
    need ~94 members and a global assignment nobody could keep unique; source identity is already
    unique per hero and costs nothing. `TargetMode.EnemyWithMyMark` and the `HasMyMark` /
    `LacksMyMark` conditions are the targeting half. The cost, stated plainly: every mark renders
    as "Mark" on a status bar, because a StatusKind has no per-instance display name. That is a UI
    problem for the day a screen wants to print "Quarry".
  - **Trigger limits: `PassiveLimit` layered on `OncePerBattle`, not replacing it.** Unlimited,
    OncePerBattle, OncePerRound, EveryNRounds, with the bool keeping its trigger-dependent default
    and still being checked first. **Deferred: "once per target"** - it reads like a sibling of
    these but is not one. These are answered when a passive FIRES, from the owner's own clock; a
    per-target limit is answered when an effect LANDS and needs the target's identity carried into
    the check. It is a limit on an effect, not on a trigger.
  - **The ledger lives on the status, not on the unit.** `StatusKind.Ledger` banks its Magnitude
    fraction of every hit its holder takes, into `StatusInstance.Stored`, capped by `StoredCap`,
    accrued at the single damage funnel. `EffectKind.LedgerRelease` pays it out through that same
    funnel and zeroes it; `EffectCondition.LedgerAtLeast` thresholds on it. On the status because
    that is what the descriptions describe - a Footnote, a Watcher's Ledger and a Scar Record are
    all a mark that happens to be counting, and they expire, move and get dispelled exactly as any
    status does. A parallel per-unit store would need its own lifetime rules kept in step by hand.
  - **Stacking counters: `PerStack` widened, not replaced.** `PerStackStatus` picks which counter
    and `PerStackOn` picks whose. Caster keeps the snapshot-once-per-effect behaviour PerStack
    always had; Target cannot and is read per target, which is a real difference in meaning and why
    Caster stays the default. Threshold payoffs were already reachable through
    `StatusStacksAtLeast`, which the conditionals work below had already added.
  - **Own-outcome triggers:** `OnHealApplied`, `OnStatusRemoved` and `OnStatusExpired`, all firing
    on the actor. The heal trigger is skipped for a Regen tick, so a heal-over-time does not
    re-raise its caster's trigger every round; the expiry trigger fires only for a genuine duration
    expiry, since a cleanse, a dispel, an absorbed ward and a death are each a different thing
    happening on their own path.
  - **Expire hooks: the expiry half only.** `OnStatusExpired` is built. The "or breaks" half of
    those 33 abilities waits on the Shield decision below, because a break hook on a pool that
    outlives its own status would fire while the ward is still absorbing.
  - **No existing battle moved:** the same 714-fingerprint sweep, byte-identical to the merge base,
    and the whole harness report a strict superset with no existing line changed. 36 new checks,
    seven standing guards, and the ordinal pins extended again. No new `BattleEventType`.
- **Shipped: action history, ally triggers, forced targeting, charges and target-HP scaling.**
  Fourth-tier inventory work, all of it a no-op against the content that existed when it landed.
  It was built deliberately clear of the Shield fix below, which is a balance change whose harness
  was red by design and which no-op work must not inherit; both are on `main` now.
  - **Action history costs the save nothing.** A `CombatUnit` is built fresh from a `HeroDef` and a
    `HeroInstance` at the start of every fight, and the save stores the instance - uid, defId,
    level, copies, relic, armor, selected talent - and nothing about how a battle went. So the new
    per-unit state (`PreviousTargetId`, `ActedThisRound`, `LastTurnRound`, `LastDamagedRound`,
    the two action-kind flags, `ActionsTaken`, and a per-round `DamagedThisRound` list) is zero
    save bytes and **needed no version bump, which is why this pass carried none.** Seven new
    `EffectCondition` members read it.
  - **`PreviousTargetId` had to exist.** `LastTargetId` is overwritten in `ApplyDamageEffect` as
    the hit is set up, so by the time anything asks "a different target than last turn" it already
    names the new target. Note also that with no previous target at all the condition reads FALSE:
    a hero that has attacked nobody has not switched.
  - **"Since my last turn" is round-granular** and the code says so. It compares the round the
    subject was last damaged in against the round the caster last acted in, so two units acting
    inside one round are not ordered against each other.
  - **Ally triggers reuse the broadcast**, and **"would die" is built, not deferred.**
    `OnAllyWouldDie` is raised in `DealRawDamage` while the unit is still flagged alive and its HP
    is at or below zero, and the condition is then RE-CHECKED: a passive that healed it above zero
    has saved it. No new effect kind was needed, because `ApplyHealValue` refuses a dead target and
    this one is not dead yet. The harness proves the interception as an outcome rather than an
    event: the same battle kills one unit with no watcher and none with one.
  - **A single-target provoke, not Taunt on Self.** `StatusKind.Provoked` sits on one ATTACKER and
    constrains that attacker alone, which is the opposite direction to Taunt - Taunt sits on a
    protector and makes it answer for its whole side against everyone. For the four abilities that
    say "Taunts the front enemy", Taunt on Self is wrong in both directions at once: too wide,
    since every other enemy is redirected too, and too narrow, since it says nothing about what
    that one enemy does when the caster is not its side's anchor. **The anchor still outranks it**,
    consistent with every earlier pass, and that is recorded at the call site.
  - **`Covered` splits a hit before the shield pool**, so a protector soaks its share and the ward
    underneath meets only the remainder. Bounded to one hop by `_inCover`, the same shape Tide's
    energy redirect used and Mirrored Wound still uses.
  - **Charges are their own field, not the ledger's.** `StatusInstance.Charges` is an int counting
    DOWN to removal; `Stored` is a float accumulating UP toward a cap. One status can legitimately
    want both - a mark that records damage and also wears off after three hits - and sharing one
    field would make that unauthorable. `StatusKind.Guard` is the first carrier.
  - **`Vulnerable` is source-keyed**, which is the whole of what makes it different from
    `DefenseDown`: the harness shows the applier's damage rising while a bystander's is unchanged.
  - **Target-HP scaling** is three fields added on top of the Attack-scaled amount, so an effect
    can be part flat, part Attack-scaled and part HP-scaled at once.
  - **Deferred, with counts:** "cannot target the same ally twice" (1 ability) needs the exclusion
    threaded through every pick path and interacts with the anchor; "ends its turn" as an ally
    trigger (1 ability) has no turn-end dispatch point that is not also the status-expiry sweep.
  - **No existing battle moved:** the same 714-fingerprint sweep, byte-identical to the merge base,
    and the whole harness report a strict superset with no existing line changed. 44 new checks,
    five standing guards, ordinal pins extended, and `BattleEventType` untouched for a third pass.

- **Shipped: the Shield pool and the Shield status now empty together.** Two commits: a behaviour
  change, then the retune it forced. **This is the first time a fix in this project has moved the balance numbers,**
  so both halves are recorded here rather than left to be rediscovered from a diff.

  **Commit 1, the behaviour change.** A Shield status removed by duration expiry or by Dispel left
  `CombatUnit.Shield` behind; only absorption and death emptied the pool. So the ward tag left the
  status bar while the ward kept working - the one place in the engine where what the player is
  shown and what the simulation does genuinely disagreed. The reverse direction was already
  correct, since the damage funnel removes the status the moment the pool empties, so this is the
  missing half of a rule the engine already half-kept. **Measured before the fix: 26 absorptions
  across 16 of 144 shipped battles happened after the status was already gone, soaking 2673 damage
  invisibly.**

  **What it cost, and why the fingerprint count was the wrong instrument.** 78 of 714 fingerprints
  moved, one victory flipped and that one was a synthetic solo sweep - which reads like a small
  change and is not. The geared curve is where the cost showed: 11 of 24 rows moved, all downward,
  concentrated in late chapter 2. Chapter 2 is the Tide chapter and Orrin's ward was quietly
  carrying those fights.

  ```
  stage                    CEIL            FLOOR
  2-7   Kelp Vault         94%  -> 73.5%   94%   -> 82.5%
  2-8   Hollow Anchorage   96%  -> 60%     84%   -> 91.5%
  2-9   The Shifting Berm  94.5% -> 73.5%  82.5% -> 92%
  2-10  Wrackline Reach    97%  -> 73.5%   87.5% -> 89%
  2-11  The Deep Reckoning 96.5% -> 84%    85.5% -> 70.5%
  ```

  Five ordinary stages left the [78%, 92%] CEIL band and `build-check.ps1` went red on 2-7.

  **Commit 2, the retune.** `recommendedPower` was re-derived for all 24 stages against the
  corrected engine, using the definition this document already carries: the power at which a
  geared five-body line clears the stage's CEILING battle about 85% of the time, 65% for a finale.
  **The number alone was enough for five of the six broken stages.** No roster was touched.

  Only the six stages the fix actually broke were moved. The full derived table also proposed new
  numbers for 1-12, 2-2, 2-4, 2-5 and 2-12, and applying those was tried and reverted: each of
  those stages was already in band, and re-seating it on a fresh reading of the same 200-sample
  measurement pushed 1-12 to 74.5% and 2-5 to 93.5%, both out of band. **Two independent
  200-sample reads of the same stage differ by up to nine points**, so moving an in-band stage is
  the random walk over a noisy measurement that `DeriveRecommendedPower`'s own notes warn against.
  1-1 through 1-5 are untouched and are floor-pinned anyway: a level-1 geared line already clears
  them, so their derived number is the line's own minimum rather than a target.

  **One stage still needs roster work: 2-7 Kelp Vault.** Its CEIL is back in band at 84%, but its
  FLOOR is 89% and cannot reach 95% by the number alone. Measured at three powers:

  ```
  15993  FLOOR 89%    CEIL 84%     ceiling in band, floor short
  16400  FLOOR 97.5%  CEIL 95%     floor met, ceiling now above the band
  16800  FLOOR 100%   CEIL 98.5%   stage stops discriminating at all
  ```

  Power is quantised by hero level, and for 2-7 there is no setting between "floor too low" and
  "ceiling too high". That is roster information: 2-7's wave-1 roster is too close in difficulty to
  its wave-12 roster for one power to satisfy both ends. Worth noting it never met the 95% floor -
  it read 94% before the fix - so this is a pre-existing property the fix exposed rather than
  created.

  **Where it landed:** 0 of 24 stages BLOCK, every CEIL in band, soft floors down from 3 to 1, and
  `build-check.ps1` green at the time. It deliberately MOVES fingerprints, unlike every other
  change in this sequence - it is a content retune, and byte-identity is not the standard it is
  held to. Those numbers are superseded: the aspect removal landed after it and moved every one of
  them, which is why the CEIL band is red on `main` today.

- **Shipped: turn-order effects, status operations, and enemy-action triggers.** Second-tier
  inventory work: turn order, status operations, and the first triggers that are not owner-centric.
  - **Turn progress, and the honest account of it.** There is no ATB. A round is a speed sort
    walked once, so a unit has no continuous gauge to move a percentage of - it has a POSITION
    among the turns not yet taken. `EffectKind.TurnShift` reads a signed fraction as a fraction of
    that remaining order and moves the unit that many places, ceiled so a nonzero shift always
    moves at least one. **This is the Gale rule generalised, not a new concept:**
    `TryGaleSlowShift` already moves a unit later inside the round a Slow lands in, and
    `TryGaleReentry` already splices a unit into the tail. `EffectKind.SwapTurnOrder` exchanges
    the side's earliest and latest remaining turns, which is what Merek's active asks for.
  - **Of the 34 turn-progress abilities, roughly 19 are a shift, 6 are a Speed buff, and 9 need
    something this pass did not build.** The 6 are the "advances my own NEXT turn" family: the
    caster has already acted, so it has no position left to move, and a round-scoped `SpeedUp`
    from the stat-status work below is what "my next turn comes sooner" actually means. The 9 that
    do not
    fit are Aeron's Turn Debt (5), which is a stored quantity with a turn-start payout hook and
    therefore the ledger mechanic rather than turn progress; and reading progress as a QUANTITY
    (2: "targets above 50% turn progress", "acts before the slowest enemy"), which needs a gauge
    that does not exist. The remaining 2 record how much progress was lost, which is again a
    ledger. **The translation is lossy in one direction and says so:** on a short remaining order
    every nonzero shift is one place, so 5% and 30% differ only when there is enough round left
    to tell them apart.
  - **Status operations reuse Umbra's mover rather than adding a second one.** `MoveStatus` is
    extracted from `TryUmbraSpread`, and both the death-jump and the new `MoveDebuffs` /
    `MoveBuffs` go through it. That preserves the jump's guarantee that a move CREATES NO VALUE -
    duration and magnitude cross untouched - in one place instead of two. Also
    `EffectKind.RemoveStatus` (by name, where Cleanse and Dispel remove by class and recency; 28
    abilities, the largest group here) and `EffectKind.InvertStatus`, which flips a buff into its
    pair through a new `StatusInfo.Opposite` table. A buff with no pairing is skipped, never
    removed, so an inversion cannot quietly act as a Dispel. **Deferred: swapping two units'
    statuses** (1 ability), which is a compound of moves.
  - **Three broadcast triggers, none of which needs new state.** `OnEnemyActive`,
    `OnEnemyBuffGained` and `OnEnemyDamagedAlly` fire on every living unit of a side rather than
    on one owner - a new dispatch shape, `BroadcastPassive`, at points the simulation already
    reaches. Depth is threaded from whatever raised the broadcast, so the existing
    `MaxPassiveDepth` bounds the cascade. `OnTurnDelayed` fires on the source of a shift.
  - **Deferred, with counts, rather than inventing state:** "an enemy misses" (2 abilities) needs
    an accuracy system that does not exist at all - Block is the only avoidance and it is not a
    miss. "An enemy changes target" (5) needs the previous target compared before
    `ApplyDamageEffect` overwrites `LastTargetId`, which is the history mechanic rather than a
    trigger. "An enemy loses a buff" (2) would need a hook in every removal path - Cleanse,
    Dispel, expiry and the new RemoveStatus - with different meanings in each.
  - **`OnEnemyDamagedAlly` is "damaged", not "targets".** Six abilities say targets; a heal, a
    debuff or a miss all target without damaging. Damage is the half with a single funnel, so it
    is the half that can be raised once and correctly rather than at four call sites with four
    meanings.
  - **No existing battle moved:** the same 714-fingerprint sweep, byte-identical to the merge
    base, and the whole harness report a strict superset with no existing line changed. 43 new
    checks, both standing guards, and the ordinal pins extended to `EffectKind`, `PassiveTrigger`
    and `BattleEventType`. This pass adds **no** new `BattleEventType`: an authored shift reuses
    `TurnOrderShift` and is told apart from Gale's by carrying `Status` None.
- **Shipped: 15 stat statuses and 8 new target modes.** Two additive gaps from the inventory, no
  structural change to anything.
  - **15 new stat statuses, in TWO shapes, not one.** The eight stats asked for split along the
    line `RelicStatMode` and `ArmorStatMode` already draw. **Speed is the only MULTIPLY stat**:
    `Speed * (1 + SpeedUp - Slow)`, the AttackUp shape. The other seven - Block, Damage Reduction,
    Effect Hit, Effect Resist, Crit Damage, Skill Damage, Armor Break - are **ADD**:
    `raw + Up - Down`, the Focus shape, because they are small fractions never scaled by level or
    rarity and a multiplicative bonus on a stat sitting at 0 is exactly 0.
  - **There is no SpeedDown, deliberately.** Slow already is the speed debuff, it is the term
    `EffectiveSpeed` subtracts, and Gale's intra-round turn-order shift keys on `StatusKind.Slow`
    specifically. A second speed debuff would be a duplicate mechanism silently bypassing that
    rule, so SpeedUp joins Slow's term instead of bringing a mirror.
  - **Crit Damage needed a getter before it could be buffed at all.** It was the one stat in the
    damage pipeline still read straight off the unit (`raw *= source.CritDamage`), which is why no
    status could move it. `EffectiveCritDamage` now exists and the pipeline reads it.
  - **8 new target modes**, all appended: `EnemyWithStatus` (plus `EffectDef.TargetStatus`),
    `HighestEnergyEnemy`, `HighestSpeedEnemy`, `LowestSpeedEnemy`, `LowestEnergyAlly`,
    `TwoLowestHpAllies`, `SecondLowestHpAlly`, `AllyWithMostDebuffs`.
  - **There is deliberately no `AllEnemiesWithStatus`.** `AllEnemies` plus an `EffectCondition` of
    `HasStatus` already does exactly that, per target, from the conditionals work below. Only the
    single-target half needed a mode.
  - **Deferred: "the enemy that last damaged an ally"** (1 ability) - it needs a side-level memory
    the Sim does not keep; `LastAttackerId` is per unit, so this is new state rather than a new
    pick rule. **Deferred: "adjacent enemies"** (3 abilities) - adjacency is relative to a primary
    target the effect does not know, so it is the splash mechanic rather than a target mode.
  - **THE ANCHOR, reported and NOT changed here.** Every single-enemy mode is overridden by the
    side's anchor, and the four new enemy-side modes inherit that, while the four ally-side modes
    are untouched because the anchor only answers for the enemy side. The harness proves both
    halves: with no anchor `EnemyWithStatus` picks the marked body, and with an anchor standing the
    same mode collapses onto it. **This pass flagged that it reads badly for `EnemyWithStatus`
    specifically** - "deal 245% to a Forsaken enemy" hitting an unmarked tank is an ability doing
    something other than what it says - and left the decision to a human, because the anchor was
    then a living Lumen unit first and a Taunt holder second, so an exemption would have been a
    change to an aspect rule. **That has since been answered by removing the Lumen half outright**
    (see the aspect-removal entry above). The anchor is Taunt and
    nothing else now, which is a deliberate, visible claim by an ability rather than a property of
    a unit's colour, and the check here varies a Taunt instead of an aspect.
  - **Enum ordinals are pinned by a check.** Both enums were appended to, never inserted into,
    because `BattleEvent.Status` and the `(int)mode` a `TargetAnchored` event carries are in the
    permanent record of every battle. A renumbering would rewrite what past battles said with no
    simulation change at all, so the pre-existing ordinals are now asserted.
  - **No existing battle moved:** the same 714-fingerprint sweep, byte-identical to the merge base,
    and the whole harness report a strict superset with no existing line changed. 77 new checks,
    including both standing guards.
- **Shipped: a condition on `EffectDef`, and nothing else.** It was built on top of the
  multi-passive work below rather than separately, because both touch `CombatSimulation` and
  `Definitions`. 111 of the incoming roster's 374 abilities
  carry a conditional clause, the single largest gap in the inventory, and the only conditional
  the engine had was `PassiveTrigger.OnHpBelowThreshold` reading the passive owner's own HP.
  - **Seven condition kinds, one shape.** `EffectCondition` is `HasStatus`, `LacksStatus`,
    `HpBelow`, `HpAbove`, `HasShield`, `LacksShield`, `StatusStacksAtLeast`, plus `None`. Every one
    is one subject, one property, one comparison against an authored constant, and
    `ConditionSubject` picks whether the subject is the effect's target or its caster - so "if it
    is below 35% HP" and "while Hale is below 50% HP" are the same kind with a different subject.
  - **Evaluated after targeting, before the effect.** `CombatSimulation.ConditionMet` is public and
    static like `ResolveLandingChance`, reads no RNG and mutates nothing, and is asked once per
    target, so a conditional AoE tests each enemy on its own. A failing condition skips the effect
    whole: no hit loop, no landing roll, no Block, crit or variance draw, and no `ConsumesResolve`,
    since spending the counter emits a `StatusExpired` and a skipped effect must not move the
    status row.
  - **Two boundary decisions worth knowing.** `HpBelow` and `HpAbove` are INCLUSIVE at the
    threshold, matching `PassiveTrigger.OnHpBelowThreshold`, because two meanings of "below" in one
    engine is a worse trap than the expressive difference. `HasShield` reads the absorbing POOL,
    not `StatusKind.Shield` on the status row; the two genuinely disagree, since the status expires
    on a round clock while the pool is only emptied by absorption or death, and the pool is what
    stops damage. A kit wanting the status row authors `HasStatus` with `conditionStatus` Shield.
  - **Deferred: cross-unit stat comparison** ("the target has more Attack than the caster"). It
    needs a stat selector and a two-sided operand model rather than a subject and a constant, and
    it is 2 of the 374 incoming abilities - Dahlia reading Energy and Oris reading Speed - against
    dozens for every kind above. Also deferred, and for the same
    proportionality reason: composing two conditions on one effect. Every conditional clause in the
    roster is a single clause, and a kit that needs two tests can carry two effects.
  - **No existing battle moved, measured against the merge base.** The same sweep the multi-passive
    multi-passive work used - 24 stages at nine resolved ladder positions each at 3 seeds, plus
    every hero
    fought solo across 6 stages, 714 fingerprints - all byte-identical. The whole harness report is
    a strict superset of the base's: not one existing line changed, because no save version moved
    this time either. 71 new checks, including the standing guard that no shipped effect carries a
    condition.
- **Shipped: multiple passives per unit, and a selectable talent layer.** The base everything
  above was built on. It removes the structural gate in front of the 94-hero roster: a unit held exactly one passive and the engine had no talent concept at all,
  which blocked 120 of that roster's abilities (30 second passives, 90 talents) before any new
  combat mechanic was even considered. It adds **no new combat mechanic** - a talent is an
  additional passive and nothing else, so everything a talent can express today is what a passive
  could already express.
  - **`UnitDef.Passives` is a list**, and the single `PassiveDef` field is gone. Parsing reads the
    flat `passive` key every shipped unit uses, flat `passive1`/`passive2`, and the nested
    `skills.passive1`/`skills.passive2` the new roster authors, so current content loads
    unchanged. `CombatSimulation.TriggerPassive` walks the list; every gate that used to sit above
    the single read (trigger, threshold, empty effects, `oncePerBattle`) moved inside the walk.
  - **`oncePerBattle` is now per passive, not per unit.** `CombatUnit.PassiveFired` was one bool,
    written when a unit had one passive, so "this unit's passive has fired" and "this passive has
    fired" were the same sentence. With two they are not: the first to fire would have silenced
    the second for the rest of the battle even on an unrelated trigger, so a hero with a
    `BattleStart` buff and a once-per-battle survival passive would lose the save outright. It is
    a parallel flag list now, appended in step with the passive list by `CombatUnit.AddPassive`.
  - **`HeroDef.Talents`**, up to `HeroDef.MaxTalents` (3), authored in the same shape as a passive
    because a selected talent IS a passive. Which one is live is per-owned-hero save state
    (`HeroInstance.SelectedTalent`, 0 for none), resolved once in `BuildUnits` exactly where a
    relic id is resolved and appended as one more passive. An unselected talent is not merely
    quiet: the harness proves a hero with three talents authored and none selected fingerprints
    byte-identically to a hero authoring no talents at all.
  - **Save version 13**, zero-default migration, `selectedTalent` written per hero only when
    non-zero. `GameSession.SelectTalent` is the whole selection rule - free, no cooldown, refuses
    a slot the hero does not author, and clearing to none always succeeds.
  - **No existing battle moved, measured rather than asserted.** Every one of the 24 stages
    resolved to nine real positions on its wave ladder (first, middle and last battle of the
    first, middle and last wave) at 3 seeds each, plus every hero in content fought solo across 6
    stages: 714 fingerprints over event type, source, target, value, crit, status, duration,
    ability id and round, all byte-identical to `main`. The full harness report is identical line
    for line apart from the 17 lines that print a save version number. 38 new checks cover the new
    behaviour, and one of them is the standing guard: no shipped unit carries more than one
    passive and no shipped hero authors a talent, so the day either stops being true the
    no-fingerprints-move claim has to be re-earned rather than assumed.
  - **`Tools/doc-lint.ps1` gained `EMBERFALL_SKIP_DOC_LINT`.** Both of its git-backed classes ask
    what resolves, so a `--depth 1` clone reports every hash and every branch in the docs as dead.
    The variable says "this clone cannot answer that", prints it and exits 0. CI checks out at
    full depth and sets no environment, so it never fires there - and a baseline must still never
    be regenerated from a clone that needed it.
  - **Not in this pass, deliberately:** the talent selection UI, and carrying a talent selection
    into a Proving opponent snapshot (`BattleLoadout` holds a relic and armor but not a talent, so
    a snapshot opponent currently fights without one).
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

- **Star contract (the authoritative kit rule).** Heroes have 1 to 5 stars. Kit completeness
  varies by tier:

  | Stars | Kit |
  |-------|-----|
  | 1 | basicAttack only. No active, no passive, no talents. |
  | 2 | Adds passive1. No active, no talents. |
  | 3 | Adds activeSkill. passive1 required. No passive2, no talents. |
  | 4 | activeSkill and passive1 required. passive2 optional. No talents. |
  | 5 | Full kit: activeSkill, passive1, passive2, all three talents. |

  `validate_heroes.py` enforces this at import time (severity fail). The DevHarness enforces what
  the engine model can see (basicAttack always present, active at 3+, passive at 2+; passive2 and
  talents are import-format fields not yet in `HeroDef`). `CheckEnumParity` keeps the Python and
  C# enum sets in sync.

- **Recital Guardians and Coda: shipped.** Two additions to The Deep Recital.
  - **Guardians** — every 10th floor (`recitalGuardianInterval` in `balance.json`) is a
    boss-plus-escorts encounter, the same shape the campaign finales use. One enemy at a raised
    level (`recitalGuardianBossLevelBonus`, default 5) with the rest at the floor's base level.
    The Aspect cycle is unbroken across Guardian floors. `RecitalGenerator.IsGuardian` and
    `BuildFloor` express the shape; `StageDef.IsBoss` is set on Guardian floors. Harness checks
    prove every 10th floor is a Guardian and no other is, the cycle is unbroken, the boss is
    measurably stronger, and a Guardian floor is still winnable.
  - **Coda** — the Recital's own currency, isolated from Gold/Shards/Dust/Slag/Valor. Earned on
    every Recital clear (larger on Guardian floors), rates in `balance.json`. Save version
    **11 to 12**, additive. **There is no sink yet** — the purse, accrual and save field exist so
    a player can see the balance accruing. See DESIGN-DEEP-RECITAL.md section 8 for what Coda is
    expected to buy later. The Coda balance is shown on the Recital screen. The CurrencyStrip
    (shared across four screens) does not yet carry a Coda cell — adding a 7th cell exceeds the
    1500px strip width and needs layout work.

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
- **All six aspect identities have been REMOVED. Read the aspect-removal entry in Now / Next
  before using anything in this bullet.** The description below is true of every release built
  before that change, and it is kept for that reason - a reader of an old battle record needs it.
  It is not a description of the live engine. An Aspect is now the damage chart, armor attunement
  and flavour; a standing guard in the harness asserts that nothing in `CombatSimulation` or
  `CombatUnit` reads `Aspect` for anything else.
- All six aspect identities, as they were wired into `CombatSimulation` before the removal:
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

    **Roster re-authoring pass, 2026-09-10.** The retune above met the hard requirement (zero
    BLOCKs) but left eleven stages with CEIL under 80% or FLOOR under 95%. This pass re-authored
    rosters for all fourteen failing stages, re-derived `recommendedPower` for every stage whose
    roster changed, and iterated until all 24 stages satisfied both targets simultaneously:

    - **Ordinary stages**: FLOOR >= 95%, CEIL 80-90%, no BLOCK.
    - **Finales** (1-12, 2-12): CEIL ~65%.
    - **1-1 through 1-5**: untouched (floor-pinned by design).

    Levers used, in order: (1) enemy composition and counts, (2) which templates appear, (3) span
    between `floorLevel` and `ceilingLevel`. `recommendedPower` was derived from each roster, never
    hand-set. A new harness assertion (`CheckArmoredCurveDelta`) now fails the build if any
    non-pinned stage's CEIL drifts outside [78%, 92%] (ordinary) or [55%, 72%] (finale).

    Final table, 200 samples, measured against live code:

    | stage | recPwr | FLOOR | CEIL | BLOCK |
    |-------|--------|-------|------|-------|
    | 1-1 to 1-5 | 3950–6100 | 100% | 100% | none |
    | 1-6   |  7430 | 100%  | 86.5% | none |
    | 1-7   |  7475 | 100%  | 90%   | none |
    | 1-8   |  7801 | 98%   | 90%   | none |
    | 1-9   |  9531 | 100%  | 87%   | none |
    | 1-10  |  9711 | 99%   | 89.5% | none |
    | 1-11  | 10262 | 97%   | 90%   | none |
    | 1-12  | 10936 | 96%   | 64.5% | none |
    | 2-1   |  8084 | 100%  | 90.5% | none |
    | 2-2   |  9003 | 97.5% | 89%   | none |
    | 2-3   |  9830 | 99%   | 88%   | none |
    | 2-4   | 11647 | 100%  | 83%   | none |
    | 2-5   | 13278 | 98%   | 90.5% | none |
    | 2-6   | 15405 | 98.5% | 85.5% | none |
    | 2-7   | 15745 | 94%   | 87%   | none |
    | 2-8   | 17537 | 96%   | 84%   | none |
    | 2-9   | 19479 | 94.5% | 82.5% | none |
    | 2-10  | 21635 | 97%   | 87.5% | none |
    | 2-11  | 23239 | 96.5% | 85.5% | none |
    | 2-12  | 26490 | 99.5% | 63%   | none |

    Two stages sit at CEIL 90.5% (2-1, 2-5), 0.5% above the band boundary. Both are seed noise
    at 200 samples; the `tidecaller_wretch` in 2-5's roster collapses the floor-ceil gap and
    prevents a clean hit without replacing the unit, which would change the stage's authored
    identity.

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

    **Guardians and Coda** added atop this base:
    - **Guardians**: every 10th floor (`recitalGuardianInterval`) is a boss-plus-escorts encounter.
      `RecitalGenerator.IsGuardian` and `BuildFloor` express the shape.
    - **Coda**: the Recital's own isolated currency, earned on every clear with a larger payout on
      Guardian floors. Save version 11 to 12. No sink yet — purse and accrual only. See
      DESIGN-DEEP-RECITAL.md section 8.

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
    Orren covers the new value. **`Ranger` no longer exists**, and Orren is a Marksman: the enum
    was later widened to the incoming roster's ten roles, which have no Ranger among them. See the
    role-widening entry in Now / Next. Two checks did fail at the time, both content invariants
    rather than anything about Ranger: the hero count is asserted as an exact literal (7, now 11), and Veska and Orren
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

    **All five of those heroes have since been deleted. The pin survives, the line does not** - see
    the roster-swap entry at the top of Now / Next. The reasoning below is why the line is pinned at
    all, which still holds; the names and the restored numbers are a record of a state the repo no
    longer has.

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

### Sprite rendering fixes

Three bugs fixed in one pass. All are rendering-only; no gameplay, balance or save
changes.

**1. Flash fringe (shader).** `EmberfallFx.shader`'s `_Flash` lerp pushed all pixels
toward white, including DXT5-compressed transparent-region artifacts (alpha 1–100 of 255)
that are invisible at Flash=0 but become a visible black border at Flash>0. Fixed by
gating: `_Flash * step(0.5, c.a)` — pixels below 50% alpha never receive flash. Every
other material using this shader (`Emberfall/Lit` for procedural rigs, FX effects that
leave `_Flash` at its 0 default) is unaffected.

**2. Missing-clip fallback (SpriteAnimator).** `PlayOnce` previously returned silently
when `GetOrLoad` found no clip, leaving the renderer on whatever texture/UV it held.
Now falls back to `PlayLoop("idle")`, keeping the unit visually present on the correct
frame rather than stuck on a stale rect.

**3. Per-clip foot fractions (sidecars + SpriteAnimator).** `footFraction` was a single
hero-level value derived from the idle clip. Clips whose content extends to a different
vertical position in the frame (Sylven's hit_reaction/death/skill all have
footFraction=0.000 vs idle's 0.227) inherited idle's margin and appeared to float or
sink. Each clip's sidecar now carries its own `footFraction`, and `SpriteAnimator`
repositions the quad on every clip switch.

#### Clip coverage (30 heroes with sprite sheets)

Every hero ships idle, basic, hit_reaction and death. Optional clips vary:

| Hero | basic2 | skill | controlled |
|---|---|---|---|
| Aeron | — | ✓ | — |
| Altheon | ✓ | ✓ | ✓ |
| Aminah | ✓ | ✓ | ✓ |
| Arden | ✓ | ✓ | ✓ |
| Brenna | ✓ | ✓ | ✓ |
| Caldus | ✓ | ✓ | ✓ |
| Celen | ✓ | ✓ | ✓ |
| Cyrra | ✓ | ✓ | ✓ |
| Dahlia | — | ✓ | — |
| Darian | ✓ | ✓ | ✓ |
| Edrin | ✓ | ✓ | ✓ |
| Elyra | ✓ | ✓ | ✓ |
| Isara | ✓ | ✓ | ✓ |
| Isolde | ✓ | ✓ | ✓ |
| Kael | — | ✓ | — |
| Marrow | — | ✓ | — |
| Merek | — | ✓ | — |
| Mireth | ✓ | ✓ | — |
| Nysera | ✓ | ✓ | ✓ |
| Oris | — | ✓ | — |
| Orren | — | ✓ | — |
| Rheia | ✓ | — | ✓ |
| Rovan | ✓ | ✓ | ✓ |
| Seraphine | — | ✓ | — |
| Sylven | ✓ | ✓ | ✓ |
| Talren | — | ✓ | ✓ |
| Vaelis | — | ✓ | ✓ |
| Velis | ✓ | ✓ | ✓ |
| Veska | — | ✓ | — |
| Zella | — | ✓ | — |

Rheia is the only hero missing `skill`. All 30 have the four required clips.
