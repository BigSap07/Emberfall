# Emberfall — aspect identities

Today an Aspect is a colour and a multiplier. `AspectChart.Multiplier` swings a hit ×1.20 / ×0.85
and nothing else in the codebase reads `Aspect` except `UITheme.AspectColor`. Two Embers on a line
is a palette, not a plan.

This document gives each Aspect one **rule in the effect pipeline** — the same standard
DESIGN-RELICS.md sets: if it can be written as a percentage, it is a stat stick and it is cut. The
six rules below are drawn from what the existing six heroes already do, not invented. Kalyx already
burns, Maeve already feeds energy, Nyx already debuffs; the job here is to notice that and finish
it.

Nothing here is implemented. This is a specification and an argument about cost.

**SUPERSEDED ON THE MECHANICAL HALF, 2026-09-11. All six rules have been removed from the
engine.** They were built, they shipped, and they are gone; an Aspect is the damage chart, armor
attunement and flavour again, which is close to the state the opening paragraph above describes.
A standing guard in the harness now asserts that nothing in `CombatSimulation` or `CombatUnit`
reads `Aspect` for anything except the chart lookup and the one float it produces.

**What that does and does not void.** Every §1 rule, every §2 hook, §8's team-size argument and
the identity half of §3 and §5 are void as statements about the engine. The REASONING is not: §4's
narrowing of the wheel to ×1.10 / ×0.92 is still live and still in `Enums.cs`, §3's reading of
which heroes fit which belief still holds as a character argument, and §6's reconciliation with the
other documents is still how those disagreements were settled. Read this as the record of a
decision that was made, implemented, measured and reversed, not as a description of the code.

**The cost of the reversal is recorded in DESIGN-BUILD-ORDER's Now / Next, and it was large:** 675
of 714 campaign fingerprints moved and 20 of 24 geared stages got harder, because §1's Lumen rule
was routing 36.2% of all damage in shipped content onto one body and chapter 1 was tuned with that
routing in place. §5's "Lumen has no hero" problem does not come back - Orrin is still Lumen, the
relabel stands, and the aspect still swings his damage against Umbra - but the interception that
DESIGN-WORLD §3 credits for stage 1-12 getting *easier* is gone, and 1-12 is one of the two stages
that now blocks.

**One hero is left without an identity.** §3 cut Kalyx Emberwright's Kindling as a percentage stat
stick on the argument that the Ember stacking rule would carry him. It no longer exists, and he now
ships with nothing on his sheet but numbers and targeting. That is content work, not engine work,
and it is owed.

The paragraph above ("Nothing here is implemented") describes the pre-implementation state and is
accurate again for the six rules, though not for §4's wheel.

---

## 1. The six identities

**Ember — the fire compounds.** Burn instances from Ember sources stack as separate entries instead
of overwriting each other, so a second Ember deepens the same fire rather than replacing it. Ember
buys damage that lands later, on the victim's own turn.

**Tide — energy is a resource you can move.** Tide never discards energy. Overflow past `EnergyMax`
is handed to the living ally furthest from casting instead of being thrown away, so Tide decides the
order the line's big abilities arrive in.

**Verdant — health is banked forward, not wasted.** Healing and warding with nowhere to go becomes
`Regen` on the recipient rather than being clamped away. Verdant's output is never wasted on a
healthy line; it is stored on it.

**Gale — Gale acts out of order.** Gale manipulates the initiative list itself: a Gale unit that
lands a kill re-enters the current round's turn order, and Gale's `Slow` pushes a target's turn
later within the round it is applied, not merely the next one. **Both halves built in
`claude/aspects-gale`.** The Slow half has no shipped-content source: the game's only `Slow` is the
Sundered Colossus's, which is Umbra, and neither Gale unit applies one. §7's content list does not
add one either, so that half is correct and currently unreachable, proved against a constructed Gale
slower and asserted as a gap in the harness.

**Umbra — what Umbra marks stays marked.** Harmful statuses applied by an Umbra source cannot be
cleansed and do not tick down while the applier lives. Umbra picks the enemy that matters and
permanently subtracts it from the fight.

**Lumen — Lumen decides who takes the hit.** Lumen intercepts. Damage aimed at the line resolves
onto Lumen or onto a ward before it reaches its intended target, so the fragile heroes never resolve
as targets in the first place. **Built in `claude/aspects-lumen`, to DESIGN-WORLD
section 1's re-weighting rather than to the text above.** The paragraph leads with absorption;
DESIGN-WORLD overrides that ordering and makes targeting determinism the identity. As shipped, a
living Lumen unit answers every single-enemy targeting question aimed at its side, carrying `Taunt`
or not, and outranks a non-Lumen ally's `Taunt` even from a higher slot. The unconditional is the
mechanic: a rule that only held while a duration-2 `Taunt` was up would be a function whose answer
changes every other round, which is the opposite of the belief it carries. Absorption got no code at
all — see §2.

### Rejected, per the no-percentage constraint

One candidate per Aspect was cut on the way here, and they are worth naming so they do not come
back: Ember "+% versus burning" (this is Kalyx's `Kindling`, see §3), Tide "+% energy generation",
Verdant "+% healing received", Gale "+% speed", Umbra "+% crit damage from behind", Lumen "+% to all
allied stats". Every one of them is `EffectiveAttack`-style arithmetic wearing a colour. None of
them changes a single decision about who stands next to whom.

---

## 2. Where each one hooks into the code

Every hook below is an existing branch. No new subsystem, no new clock, no ambient lookup.

**Ember** — `CombatUnit.AddOrRefresh` is the whole mechanic. Today it finds a matching `StatusKind`,
keeps the stronger `Magnitude` and the longer `RoundsLeft`, and returns; two Ember heroes applying
Burn to the same target therefore produce *one* Burn, and the weaker application is silently
discarded. `Sim.TickDamageOverTime` already walks `unit.Statuses` by index and ticks each entry
independently, so it needs no change at all — it will tick three Burns if three exist. The Burn
magnitude is snapshotted as `source.EffectiveAttack * effect.Magnitude` in the
`EffectKind.ApplyStatus` branch of `Sim.ApplyEffect`, so a strong Ember's stack and a weak Ember's
stack are correctly different sizes already. Ember is one `if` in one method.

**Tide** — `Sim.AddEnergy` clamps to `unit.EnergyMax` and drops the remainder on the floor. The
redirect goes there, with `Sim.EmitEnergy` firing for both units so the battle screen sees it.
`EffectKind.EnergyGain` in `Sim.ApplyEffect` and `BalanceConfig.EnergyMax` are the surrounding
constants. `Sim.TakeTurn` sets `unit.Energy = 0` on cast, which is the sink the redirect feeds.
**Clarified during `claude/aspects-tide-umbra`:** the rule keys on the *granter*, not the receiver.
`AddEnergy` takes an explicit `sourceId`; the redirect fires only when that source is a living Tide
unit. A unit's own basic-attack refill and on-damaged refill both pass `sourceId = -1` and are
excluded — otherwise a Tide enemy hitting the player's line would redirect the player's own
self-regen.

**Verdant** — `Sim.ApplyHealValue` returns early when `missing <= 0`; that early return is the hook.
The banked value becomes a `StatusKind.Regen` entry via `CombatUnit.AddOrRefresh`, which
`Sim.TickDamageOverTime` already ticks in its `else if (s.Kind == StatusKind.Regen)` branch.
~~`EffectKind.Shield` in `Sim.ApplyEffect` is the second entry point.~~ **Audited false** during `claude/aspects-ember-verdant`: the Shield handler is `target.Shield += value`, unconditional — nothing is ever clamped or discarded there, so there is no overflow to intercept. `ApplyHealValue` is Verdant's only real hook.

**Gale** — `Sim.TurnOrder()` builds and sorts the round's list; `Sim.Execute` then iterates a
snapshot of it in a plain `for` loop, which is what has to change for a re-entry.
`CompareTurnOrder`, `CombatUnit.TurnOrderSpeed`, `CombatUnit.RoundSpeedJitter` and
`CombatUnit.EffectiveSpeed` are the sort inputs. The kill hook already exists and is already fired:
`Sim.DealRawDamage` calls `TriggerPassive(killer, PassiveTrigger.OnKill, 0)` in its death branch —
and **no hero and no enemy in the content files uses `OnKill`**. It is a live, wired, completely
unused trigger. Gale is what it was built for.

**Verified and built during `claude/aspects-gale`.** Every claim above was re-checked against `main`
before it was relied on and all of them held: the `for` loop over the snapshot, the four sort inputs,
the `TriggerPassive(killer, PassiveTrigger.OnKill, 0)` call in the death branch, and `OnKill` being
authored by nobody. That last one is now deliberately false — Ryn's `Follow-Through` uses it, per §3.

Two clarifications the section did not have. **First, re-entry is not a passive and cannot be one.**
There is no `EffectKind` that grants a turn, so "Gale is what `OnKill` was built for" is true of the
*call site* and not of the trigger: the rule is keyed on `killer.Aspect == Aspect.Gale` at that same
point in the death branch, exactly as Ember keys on the Burn's source and Tide on the energy's
granter. It fires for every Gale unit whether or not that unit authors a passive at all. **Second,
the section misses one consequence of the re-entry it asks for.** `ExpireStatuses` runs at the end
of the acting unit's turn, and `RoundsLeft` is a round counter — the same thing right up until a
unit can take two turns in one round. Left alone, a Gale unit that kills would shed its own `Slow`,
`Silence`, `Stun` and buffs at double rate, a second undesigned reward riding on the first. Expiry
is now once per round per carrier.

⚠️ **One hazard found in the hook itself, not fixed here.** `DealRawDamage` fires `OnKill` with a
hardcoded depth of `0`, unlike the `OnCriticalHit` call twenty lines above it which passes `depth`
through. `MaxPassiveDepth` therefore does not bound an `OnKill` passive at all, and an `OnKill`
passive that could itself land a kill would re-enter its own trigger without limit. This was
harmless while nothing authored `OnKill`; §3's change makes it live. `DealRawDamage` has no `depth`
parameter to thread, so fixing it means touching every damage path in the file. It is left alone and
the path is kept unreachable by content instead — the harness asserts no `OnKill` passive in shipped
content carries an effect that can kill (Ryn's is `EnergyGain`, which cannot), and fails the day one
does.

**Umbra** — three places. `Sim.ExpireStatuses` decrements `RoundsLeft` at the end of the carrier's
turn; the non-expiry rule lives there and needs `StatusInstance.SourceId` (already recorded) to
check the applier is alive. The `EffectKind.Cleanse` branch of `Sim.ApplyEffect` filters on
`StatusInfo.IsHarmful`; the uncleansable rule lives there. `CombatUnit.AddOrRefresh` again, for
whether a second Umbra application refreshes.

**Lumen** — `Sim.DealRawDamage`'s shield-absorb block and `Sim.ResolveTargets`' `FindTaunter`
override, which already outranks every single-enemy `TargetMode` including `LastAttacker` and
`LastTarget`. `StatusKind.Taunt`, `StatusKind.Shield` and `EffectKind.Shield` are the existing
vocabulary. Lumen needs the least new code of the six and has the least evidence behind it (§5).

**Verified during `claude/aspects-lumen`.** The `FindTaunter` override is exactly as described and
still outranks `LastAttacker` and `LastTarget` — both forced against a line where those modes
provably wanted somebody else, and both anchored 5 hits out of 5. (R16 changed half of this: a
counterattack, `LastAttacker` with a live attacker, now answers the attacker and ignores Taunt. The
anchor still outranks `LastTarget` and every ordinary single-enemy pick.) `FindTaunter` is now
`FindTargetAnchor`, since `Taunt` is no longer the only way to qualify.

⚠️ **Half of this hook list belongs to the weighting DESIGN-WORLD replaced.** The shield-absorb
block in `DealRawDamage` needed **no new code**: it already absorbs unconditionally, and under the
corrected weighting absorption is what happens to a unit that is always the answer, not a second
co-equal rule to implement. Nor did `EffectKind.Shield`. That makes this the **third** time this
document has named a Shield-path hook that turns out to need nothing — Verdant's second entry point
was audited false in `claude/aspects-ember-verdant`, DESIGN-HOOK-RESOLUTION §3 cut Green Hour Vessel
for the same reason, and this is the third. The pattern is worth naming: the Shield path in this
codebase has no discard, no clamp and no branch to intercept, so a hook proposed there is almost
always a hook that is already doing the job.

One stale comment fixed in passing: the call site said "Provoke overrides all of them". `Provoke` was
the old name for `StatusKind.Taunt` and the comment had outlived it.

**Hazards, honestly:** Ember's stacking makes `Sim.TickDamageOverTime`'s index walk more likely to
hit the death path mid-loop, where `Sim.DealRawDamage` calls `target.Statuses.Clear()`. The existing
`if (!unit.Alive) return;` guard covers it, but it goes from a rare case to a common one. Tide's
redirect can cascade — the recipient overflows and redirects again — so it must be **one hop, no
chaining**. Gale's re-entry must not recurse into another kill re-entry without a bound (§8).
`MaxPassiveDepth = 2` bounds passive recursion but not turn re-entry.

---

## 3. Which heroes actually fit

One of six is a clean fit. This is the most useful finding in this document, and it is not a
criticism of the roster — it is what happens when Aspect has never meant anything.

**Maeve Corrin (Tide) — fits completely.** `Tidal Cadence` grants `EnergyGain` 25 to `AllAllies`;
`Rising Chorus` grants 8 to `AllAllies` every one of her turns. She is already a hero whose output
is other people's cast timing. She needs nothing. She is also the hero who most obviously *wastes*
the mechanic today: with `Rising Chorus` ticking every turn, a line sitting near 100 energy throws
away most of what she produces, which is exactly the discard the Tide rule reclaims.

**Nyx Serrow (Umbra) — fits, with one stray.** `Hollow Cut` and `Umbral Step` both target
`HighestAttackEnemy`; `Hollowing` fires `AfterBasicAttack` and applies `DefenseDown` 22% to
`LastTarget`. That is "pick the enemy that matters and subtract it" already written down. The stray
is the `Focus` self-buff on `Umbral Step` — crit chance is Gale's axis, not Umbra's, and it is the
one line of her kit that would move.

**Kalyx Emberwright (Ember) — fits, and his passive has to be cut.** `Cinder Bloom` is `AllEnemies`
damage plus `Burn` 0.28 for 3 rounds at 85% — pure Ember. But `Kindling` is `bonusVsStatus: "Burn",
bonusMultiplier: 0.30`, i.e. **+30% damage**. That is precisely the thing the no-percentage
constraint forbids, and it is the current Ember identity. It has to go, and the stacking rule
replaces it. Note the second-order effect: `PassiveDef.BonusVsStatus` and `BonusMultiplier` exist in
`Definitions.cs` and are read in `Sim.ApplyDamageEffect` **only for Kalyx**. Cutting `Kindling`
orphans that entire code path, and DESIGN-RELICS' *Splitting Prism* is written around Kindling still
existing (§6).

**Orrin Kaal (Ember) — mismatched. He is a Lumen hero wearing Ember paint.** `Cinderguard` is
`Shield` (22% of his own max HP), `Taunt` on self, and `DefenseUp` 45% from `Emberskin`.
Interception and absorption — the Lumen rule, verbatim, three times over. His only Ember content is
a 0.55-power `AllEnemies` hit and a 70%-chance `Burn` bolted onto the end of the same ability. Under
this design he is not a second Ember; he is the Lumen hero the roster already has. **Relabelled in
`claude/aspects-lumen`**, one string, kit untouched and asserted untouched. One second-order effect
the section does not predict: `Cinderguard`'s `Taunt` line is now **redundant on him**, since he
answers with or without it. That is orphaned content in the same sense this section uses for Kalyx's
`Kindling`, and it is left alone because the brief was relabel, not re-kit.

**Wren Ilyra (Verdant) — half mismatched.** `Verdant Tide`'s `Heal` to `AllAllies` is Verdant. The
`Cleanse` on the same ability is anti-Umbra, and `Rootbound` — a `Shield` on `AllAllies` at
`BattleStart` — is Lumen. Two of her three moving parts belong to other Aspects. She is a healer
with a protection kit stapled on, and once Lumen exists as a rule she reads as split between two
colours.

**Ryn Solvarr (Gale) — mismatched at the passive.** Her targeting is perfect Gale: `LowestHpEnemy`
on both `Lance Jab` and `Piercing Gale` is a finisher, and "acts again on a kill" is the rule that
finisher wants. But `Follow-Through` is `OnCriticalHit → EnergyGain flat: 22` — **that is a Tide
passive on a Gale hero.** It is the single clearest cross-wiring in the roster. Under this design it
becomes `OnKill`, and `PassiveTrigger.OnCriticalHit` joins `OnKill` and `OnDamaged` in being
defined, fired by the sim, and used by nobody. **Done in `claude/aspects-gale`**, together with
her description line. Worth stating plainly, because it is easy to assume otherwise: this did *not*
enable the re-entry mechanic, which is aspect-keyed and fires for Ryn and for the Grave Moth with or
without it (see §2). It is a separate content fix that happens to belong to the same pass. Note also
that the trigger swap does not fully un-cross-wire her — the *effect* is still `EnergyGain`, Tide's
verb; what changed is which of her own moments pays it out.

**Tally:** 1 clean (Maeve), 2 near (Nyx, Kalyx), 3 that need re-labelling or re-kitting (Orrin,
Wren, Ryn). Three of six heroes change colour or change a passive. That is the real cost of this
design and it is a content cost, not a code cost.

---

## 4. What happens to the matchup wheel

**It stays, as a secondary layer, with its numbers narrowed.** It is not replaced.

Replacing it is more expensive than it looks. `BattleEvent.AspectFactor` was added specifically to
surface the wheel (DESIGN-BATTLE-READABILITY, "What must be added", item 1).
`BattleScreen.AspectTag` renders the WEAK / RESIST tags off it. `Debrief.TryAspect` is an entire
clause of the results-screen sentence built on it. DESIGN-FIRST-TEN-MINUTES asks for enemy Aspects
on the campaign card as a reason to change the line. Deleting the wheel deletes shipped work in
three files and one of five Debrief clauses.

But it cannot stay at ×1.20 / ×0.85 either. That is a **41% swing** between an advantaged and a
disadvantaged attacker, and at team size 3 it will simply out-argue every identity in this document.
A wheel that strong says *bring the counter-colour*, which means one correct line per stage, which
means mono-colour by force — and "stacking two Embers" stops being a strategy choice and goes back
to being a compliance check with extra steps. The wheel and the identities want the player to choose
along different axes, and the louder one wins.

Proposal: `AspectChart.Advantage` 1.20 → **1.10**, `AspectChart.Disadvantage` 0.85 → **0.92**. The
wheel becomes a tiebreak between two lines you'd otherwise be happy with, which is what a secondary
layer is.

⚠️ **One constant must move with it, or a feature silently dies.** `Debrief.AspectFactorThreshold =
1.2f`, and `TryAspect` counts damage where `e.AspectFactor >= AspectFactorThreshold`. Drop
`Advantage` to 1.10 and that comparison is never true again: the Aspect clause stops firing, no test
fails, no error appears, and the results screen quietly loses a fifth of its vocabulary.
`Debrief.AspectDominantShare` (0.35) should be re-checked in the same pass, since a smaller
multiplier means a smaller share of damage attributable to it.

The wheel also self-weakens at team size 5: a five-hero line covers more colours, so the average
`AspectFactor` across a fight trends toward 1. Narrowing it now costs less at 5 than it does at 3.

---

## 5. Lumen has no hero and Ember has two

State the actual counts first. Heroes: Ember 2, Verdant 1, Gale 1, Tide 1, Umbra 1, Lumen **0**.
Enemies: Ember 2, Umbra 2, Verdant 2, Gale 1, Tide 1, Lumen **0**. The string `Lumen` appears in
this project exactly twice — the `Aspect` enum and a hex colour in `UITheme.AspectColor`. Nothing
has ever been Lumen.

Three consequences, in order of how much they matter.

**One. Lumen's identity is the only one that is invented rather than observed.** Ember, Tide,
Verdant, Gale and Umbra were each read off an existing kit. Lumen was derived by elimination — it is
what the pipeline still had spare after the other five took theirs. That makes it the least
trustworthy entry in §1 and the one most likely to be wrong in play. It should be specified last and
built last, and if one of the six has to be cut to ship, cut this one.

**Held, and the thin coverage is real.** Built last, as advised. Orrin is the only Lumen unit in the
game and no enemy is Lumen, so the rule has exactly one carrier and is one-sided: enemy single-target
abilities anchor onto him, and player single-target abilities resolve exactly as they did before.
That is thinner shipped-content evidence than any of the other five aspects had, and it is why the
`claude/aspects-lumen` harness leans on forced synthetic arenas — four contrary `TargetMode`s and a
lower-slot `Taunt` — rather than on the campaign run alone. Stated as a `[note]` in the harness
output so it cannot quietly become an assumption.

**Two. Ember does not actually have two heroes.** Per §3, Orrin's kit is Shield, Taunt and DefenseUp
— the Lumen rule. Re-labelling him Lumen costs one string in `heroes.json` (`"aspect": "Ember"` →
`"Lumen"`), fixes Lumen's zero, fixes Ember's duplicate, and — this is the point — is the *less*
invasive change, because it makes his mechanics and his colour agree for the first time. His name,
title ("Bulwark of Ash"), lore and `visual.primary` `C2502F` all say fire, so this is a re-skin
decision as much as a design one. The alternative is keeping him Ember and building his kit toward
the Burn stacking rule, which is a bigger content edit than changing the word.

**Three. `AspectChart` is asymmetric and Lumen sits on the weak side.** `Beats()` runs a four-way
ring — Ember ▸ Verdant ▸ Tide ▸ Gale ▸ Ember — with Lumen and Umbra as a detached two-way pair.
Lumen and Umbra are neutral against all four ring colours and only ever interact with each other.
With no Lumen unit in the game, **Umbra's advantage has never once fired**, on either side. Nyx's
`Beats` lookup returns `1f` against every enemy in the campaign. If Orrin becomes Lumen, the pair
activates for the first time — and the first thing it does is give every Umbra enemy (Hollow Husk,
and the `sundered_colossus` boss) a live ×1.20 against the player's tank. Under the narrowed numbers
in §4 that is a ×1.10 rather than a ×1.20, which is another reason to narrow them in the same pass.

**Measured during `claude/aspects-lumen`.** The pair is live: 194 hits from an Umbra enemy onto
Orrin and 154 the other way across twelve stages, against zero non-neutral hits in the Ember control,
which confirms the chart really had never fired. Three corrections to this paragraph, in ascending
order of how much they matter.

The **live number is ×1.10**, not ×1.20. The narrowing landed in an earlier pass, so the
parenthetical at the end of this paragraph is now the operative figure and the ×1.20 in the sentence
before it is historical.

**The pair is mutual, not one-way, and this paragraph only names the half that hurts.** `Beats()`
returns true in *both* directions for Lumen and Umbra, so `Multiplier` returns `Advantage` both ways
and never `Disadvantage`. Orrin gains the same ×1.10 attacking a Hollow Husk that it gains attacking
him. He is a tank with 105 Attack, so the offensive half is small — but it is not zero and it is not
mentioned.

⚠️ **The predicted difficulty spike did not appear, and went the other way.** Stage 1-12, which is
3 of 5 Umbra, goes from **73% to 85%** at a contested level — *easier* by 12 points. A second,
independently-built instrument (`CheckPinnedFiveBodyCurve`) moves the same direction on the same
stage, 20.8% to 58.3%. The reason is the half of the relabel this paragraph does not weigh: it does
not only apply a multiplier, it also routes every single-target enemy hit onto the highest-HP,
highest-Defense, ward-carrying unit on the line. On this content the routing beats the multiplier
outright. Stage 1-1 could not be measured at all — three starters clear it 40 seeds out of 40 at
Lv1, the floor, so there is no headroom for a spike to show in either direction; the tutorial half of
DESIGN-WORLD §3's warning is **untested, not refuted**. None of this says the spike was never a risk:
it returns on any stage whose Umbra enemies out-damage what one tank can absorb, and on any line that
does not field Orrin at all, where the penalty arrives with no routing benefit to offset it.

---

## 6. Contradictions with the existing design docs

Named explicitly, because most of them are real.

**DESIGN-RELICS — the collision is structural, not incidental.** That document enumerates "what is
actually modifiable" in the effect pipeline and then claims the six richest hooks for six relics.
This document needs six hooks in the same pipeline. There are not twelve. Four direct collisions:

1. **Tidewrought Metronome vs the Tide identity.** The relic is *"`EnergyGain` the wearer grants is
   not clamped at 100; the excess banks toward the next cast."* The Tide rule in §1 is the same
   hook,
   the same discard, the same fix. If Tide gets it, the Metronome is a no-op on any Tide hero — and
   its named wearer is Maeve, who is the Tide hero. The relic is dead on arrival.

2. **Green Hour Vessel vs the Verdant identity.** The relic is *"healing above a target's missing HP
   becomes a ward instead of being discarded."* Verdant's rule is the same early return in
   `ApplyHealValue`, banking to `Regen` instead of to a ward. Its named wearer is Wren, the Verdant
   hero. Same collision, same wearer logic.

3. **Splitting Prism depends on a passive this document deletes.** The relic's cost is stated as
   *"the Burn no longer spreads, so Kindling (+30% versus burning) has almost nothing to feed on."*
   §3 cuts `Kindling` as a percentage. With it gone the Prism's downside evaporates and it becomes a
   pure upside relic — exactly the stat stick DESIGN-RELICS exists to avoid.

4. **Warden's Debt assumes Orrin is a self-shielding Ember tank.** It redirects `Self` shields to
   the
   lowest-HP ally *"so Orrin drops below half health faster — which is where Emberskin wants him."*
   If Orrin becomes the Lumen hero whose identity is interception, a relic that removes his ability
   to intercept is no longer double-edged, it is a direct contradiction of his colour.

**Resolution:** aspects own the rule, relics move off it. A relic that duplicates an aspect identity
is not a choice; a relic that *bends* one is. Metronome becomes a hook on `EnergyGain` *cost* rather
than cap; Green Hour Vessel moves to the `Shield` path Verdant doesn't take. This is a rewrite of
two
of six relics and a re-argument of two more — do not build relics and aspects in the same pass and
expect either spec to survive.

**Resolved.** 1 and 2: Tidewrought Metronome and Green Hour Vessel are cut, not moved; the aspect
owns the hook and no relic path remained. 3: `Kindling` is cut (§3), so Splitting Prism's stated
downside changed — it is now about Burn spread lost, not a passive left hungry. 4: Orrin relabelled
to Lumen; `Warden's Debt` operates on `Shield` effects, not on targeting, so the relic leaves his
interception identity intact. See `relics.json` comments and DESIGN-RELICS for the final four.

**DESIGN-BUILD-ORDER — three problems.**

5. **Task 7 (Opening Gambit) and task 8 (Debrief) are both upstream of this work and neither knows
   it.** Task 7 *"touches `BattleRequest` in Core — coordinate with Agent 1"*, and this document
   touches `CombatSimulation` more heavily than task 7 does. Task 8 *"consumes Agent 1's
   `AspectFactor`"* — and §4 changes what `AspectFactor` ranges over. Aspects must land **after** 7
   and 8, or the coordination note in task 7 is understated by a wide margin.

6. **Task 9's tuning target is invalidated by this.** *"Stage 6 to ~40% win rate. Last."* Its stated
   dependencies are 1, 3, 7 and pacing work. Aspects change what a line is capable of, and §4
   changes
   every damage number in the game by up to 8%. Task 9 must depend on this document too, or it is
   tuned against a player who does not exist — the same argument build-order already makes for its
   own tasks.

7. **The closing line "Only task 6 touches the save format" survives, but only just.** See §7 — true
   for the aspect rules, and true for team size 3 → 5 as well, but the second one is closer than it
   looks and the line should not be read as a standing guarantee.

**DESIGN-FIRST-TEN-MINUTES.**

8. **It builds the tutorial on the wheel's current strength.** *"`AspectChart.Multiplier` already
   swings every hit ×1.20 / ×0.85"* is listed under "systems that already support this", and the
   campaign card is asked to show enemy Aspects because *"Aspects are a reason to change the line."*
   §4 narrows exactly that. The reason to change the line becomes the identity, not the multiplier,
   and the minute-2 explanation has to be rewritten to match — a bigger copy change than a number
   change. The doc's *"they know Ember beats Verdant within a minute"* stops being the lesson.

**DESIGN-BATTLE-READABILITY — no contradiction, but new load.** The doc's rule is that the screen is
a player for the event list and *"most of what looks missing is not missing, it is unread."* Ember's
stacking is the first thing this project has that the current bar genuinely cannot show: the status
bar stores one entry per `StatusKind`, and three Burns will render as one `BRN` tag. That is a
readability gap the doc's method does not close on its own, because the information really is absent
from the display, not merely unread. It is still not a new event type — `StatusApplied` fires per
application already — but the bar needs a count.

**Closed on `feat/aspect-identities`, and the doc was right that it is not a new event.**
`StatusApplied` now carries the simulation's own `CountOf` for that kind in `Value`, and the bar
renders a stack as `BRNx3`. The count is read off the event and never derived on the screen, which
is the part that is easy to get wrong: `AddOrRefresh` folds an ordinary re-application into its
existing entry and emits `StatusApplied` anyway, so a bar tallying events would have shown Nyx's
Guard Down growing one deeper on every basic attack while the simulation held exactly one entry all
fight. The four-tag cap on the row now counts *kinds* rather than entries, since four entries can be
one affliction.

---

## 7. Cost

**Core, and this is where the risk is.** `Assets/Scripts/Core/Combat/CombatSimulation.cs` —
`TickDamageOverTime`, `ExpireStatuses`, `AddEnergy`, `ApplyHealValue`, `TurnOrder`, `Execute`'s turn
loop, the `Cleanse` and `EnergyGain` branches of `ApplyEffect`, `DealRawDamage`'s shield block,
`ResolveTargets`. That is most of the file. It is a 630-line file and the single most load-bearing
thing in the project. `Assets/Scripts/Core/Combat/CombatUnit.cs` — `AddOrRefresh` (stacking), and
`SumMagnitude` needs no change since it already sums across duplicate entries.
`Assets/Scripts/Core/Data/Enums.cs` — `AspectChart.Advantage` / `Disadvantage`; a per-aspect rule
lookup if the rules are not hardcoded at each site. `Assets/Scripts/Core/Combat/Debrief.cs` —
`AspectFactorThreshold`, `AspectDominantShare` (§4).

**Content.** `heroes.json`: Kalyx's `Kindling` removed, Ryn's `Follow-Through` trigger
`OnCriticalHit` → `OnKill`, Orrin's `aspect` string, Wren's `Rootbound` and `Cleanse` re-homed or
accepted as cross-colour, Nyx's `Focus` line. `balance.json`: an Ember stack cap, a Gale extra-turn
cap, a Tide redirect toggle — three or four constants. `enemies.json` unchanged unless a Lumen enemy
is wanted to make the Lumen ⇄ Umbra pair two-sided.

**UI.** `HeroDetailScreen` (the Aspect chip must state the rule, not the colour name — one sentence,
no number, same standard DESIGN-RELICS sets for relic text), `SummonScreen`, `Widgets` aspect chip,
`BattleScreen`'s status bar for the Burn stack count.

**Save format: not affected by the aspect rules.** `SaveSerializer` writes `uid`, `defId`, `level`,
`copies`, `acquired`. Aspect is content, resolved from `defId` through `ContentDatabase` at load.
Every change in §1 is a rule in the simulation, and the simulation is reconstructed from the request
each fight. `SaveSerializer.CurrentVersion` stays at 1 per the aspect rules. Re-labelling Orrin from Ember to
Lumen changes a content file, not a save file — an existing save holding `"defId": "orrin_kaal"`
picks the new Aspect up on next load with no migration.

⚠️ **Stale.** `CurrentVersion` is 3: bumped to 2 when `PlayerProfile.Team` grew to 5 slots (for
downgrade protection — a 5-entry save read by 3-slot code truncates silently), and to 3 when
`HeroInstance.RelicId` landed on `claude/relics-rework`. The aspect rules themselves caused no bump,
as stated.

**Save format: not affected by team size 3 → 5 either, and this is worth stating precisely** because
it is not obvious. `PlayerProfile.Team` is `new string[3]`, and the array has to grow. But the
reader is already length-tolerant: `for (int i = 0; i < team.Count && i < profile.Team.Length;
i++)`, followed by `NormaliseTeam()`, which fills empty slots from the collection. A 3-entry team
array read into a 5-slot field fills slots 0–2 from the save and auto-fills 3–4. Default-empty
upgrade, no version bump. Every other loop in `PlayerProfile` (`TeamSlotOf`, `AssignToTeam`,
`ClearTeamSlot`, `NormaliseTeam`, `TeamHeroes`) already iterates `Team.Length` rather than a
literal. The hardcoded `3`s that do need finding are `PlayerProfile.Team`'s initialiser and
`HeroDetailScreen._slotButtons = new UIButton[3]`.

⚠️ One trap in that change: `BalanceConfig.MaxTeamSize` gates **both sides** — `Sim.BuildUnits` uses
it for the player team *and* for `_request.Stage.Enemies`. Setting `maxTeamSize` to 5 raises the
enemy cap too. No current stage spawns more than three, so the immediate effect is a 5-vs-3 rout
across the whole campaign until `campaign.json` is refilled.

---

## 8. Team size 3 vs 5

Evaluated at both, as required. Three of the six are size-sensitive.

| Aspect | At 3 | At 5 | Verdict |
|---|---|---|---|
| Ember | Works | Works, needs a cap | Both, with a constant |
| Tide | Works | Stronger | Both |
| Verdant | Works | Stronger | Both |
| Gale | Works | **Unbounded** | ⚠️ 3 only, without a cap |
| Umbra | Works | **Diluted** | ⚠️ 3 only |
| Lumen | **Too expensive** | Works | ⚠️ 5 only |

**Ember — both, with one constant.** Stacking is bounded by how many Embers are on the line, so 3
Embers × an `AllEnemies` Burn against 5 enemies is 15 simultaneous `StatusInstance` entries ticking
in `TickDamageOverTime`. That is not a balance problem so much as a legibility and stability one.
Needs `emberBurnStackCap` (3) in `balance.json`.

**Tide — both.** The rule itself is size-neutral, but the value of every `AllAllies` flat effect is
linear in team size: `Rising Chorus` gives 8 to three units today and 8 to five at team size 5 —
`AllAllies` includes the caster, via `CollectSide` — a silent +67% on Maeve's passive. Her numbers
get retuned regardless of this document.

**Verdant — both, better at 5.** Overflow is rare on a 3-hero line early in a fight and common on a
5-hero one, so the banking rule fires more often at 5. Direction is benign — Verdant's floor rises,
its ceiling doesn't.

**Gale — flagged. Only safe at 3 as written.** "Acts again on a kill" is bounded by the number of
killable enemies. At 3v3 a Gale finisher can chain at most two extra turns and the round ends. At
5v5 it can chain four, and a single Gale unit can resolve a round unilaterally — the exact failure
the `MaxPassiveDepth = 2` constant exists to prevent one level down, with no equivalent guard on
turn re-entry. **Needs `galeExtraTurnsPerRound` (1) before team size moves, not after.**

**Measured during `claude/aspects-gale`, and the prediction was exact.** Team size was already 5 by
the time this was built, so the guard shipped in the same change as the mechanic rather than before
it. Forced in a synthetic 5v5 arena — one Gale unit that one-shots, four inert allies, five one-hit
enemies — the same arena run twice at two values of the constant: at `galeExtraTurnsPerRound` 9 the
Gale unit chained **four** extra turns and was the **only** unit to take a turn in round 1, out of
ten on the field, which is this paragraph's sentence happening. At the shipped value of 1 it took
one extra turn, eight units acted in that round, and the same kills took three rounds instead of
one. The guard is per unit per round, since the failure this section names is per unit.

One correction to the reasoning, not the conclusion: the cap is a **balance** bound, not a hang
guard. Every re-entry costs a kill and there are only ever as many kills as units, so the loop
terminates even with the constant misconfigured upward. That is why it could be raised to 9 to
measure it.

**Umbra — flagged in the opposite direction. Weakens at 5.** **Decided and built on `feat/aspect-identities`:
the spread rule, not the boss-stage acceptance.** When a marked unit dies, every harmful status on it
whose applier was Umbra jumps to the highest-attack living unit on that same side, carrying its
remaining `RoundsLeft` and `Magnitude` unchanged. It chains: each subsequent death moves the mark
again, which is what keeps a single cast covering a five-enemy line as that line shrinks.

Three things worth stating, because none of them are in the paragraph below.

**It creates no value, and that is the design constraint the rule is built to.** A jump uses
`AddOrRefresh`, so the mark never deepens and never refreshes - it only relocates. This is why it
needs no cap where Gale's re-entry did: every hop costs a death, there are only ever as many deaths
as units, and a hop hands out nothing that was not already on the board. Gale's cap bounds a rule
that *grants* something; there is nothing here to bound.

**The heir is picked by attack, not by slot or proximity.** That is Umbra's own targeting verb - Nyx
aims `HighestAttackEnemy` with every ability she has - so the mark lands where she would have aimed
it next rather than wherever the iteration order happened to stop. Ties break on the lower Slot then
the lower Id, so the choice is total and consumes nothing from `_rng`.

**The section's own wording and the sentence that authorised this build disagree, and the difference
matters.** This document says "the mark jumps on the *marked unit's* death". A spread keyed on the
*Umbra unit's* own death would be a different mechanic and would not answer the dilution at all - it
would fire once, at the moment Umbra's carrier is lost, which is a consolation prize rather than a
scaling rule. The marked-unit reading is what is built, because it is what this section specifies and
what the dilution problem actually needs.

The original assessment, which the rule above answers rather than replaces. Its rule is inherently single-target:
permanently subtract the enemy that matters. At 3 that is a third of the enemy's offence removed and
decisive. At 5 it is a fifth, while every other identity on this list holds steady or improves.
Umbra is the one identity that gets *quietly worse* as the game grows, which is the harder failure
to notice. It needs either a spread rule at 5 (the mark jumps on the marked unit's death) or an
explicit acceptance that Umbra is a specialist pick against elite and boss stages — where there are
1–2 enemies regardless of team size, and where it stays excellent. The boss stages are the argument
for leaving it alone.

**Lumen — flagged. Probably unplayable at 3.** Its cost is a whole slot spent on interception. At 3
that is a third of the line producing no damage, against a campaign where every stage is a race; at
5 it is a fifth, and the line can afford it. This is a second reason to build Lumen last (§5): its
viability is downstream of a team-size decision that has not been made yet. If team size stays at 3,
Lumen needs a different rule, and the honest answer is that the pipeline has no sixth hook left that
is cheap at 3.

**The summary judgement:** the identities as specified want team size 5. Ember, Tide, Verdant and
Lumen are all equal-or-better there, and only Gale and Umbra need work — Gale a cap, Umbra a
decision. At team size 3, Lumen has no good answer. If the team-size move is likely, this document
should be built after it, not before.
