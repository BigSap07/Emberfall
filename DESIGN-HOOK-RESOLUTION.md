# Emberfall - the relic/aspect hook conflict, resolved

DESIGN-HOOK-CONFLICT laid out what each side of four collisions actually costs, without choosing.
This document chooses, argues each choice, and states what it unblocks. It is the [D] task 12 in
DESIGN-BUILD-ORDER is waiting on. Nothing here is new code; it is a decision about who owns which
hook and which piece of content is true.

Read DESIGN-HOOK-CONFLICT first. This document assumes its findings and does not re-derive them.

## 1. Aspects keep both overflow hooks

**The energy clamp goes to Tide. The heal clamp goes to Verdant. Both relics that wanted them,
Tidewrought Metronome and Green Hour Vessel, are cut.**

The argument is not "aspects were first" or "DESIGN-ASPECTS said so." It is what the two systems
actually are. An Aspect applies to every unit in every battle, for the entire life of the project,
whether or not anyone ever notices it exists. A relic is optional equipment a player chooses to
put on a specific hero, or does not. An identity that only exists when a piece of gear is equipped
is not an identity, it is a build. Tide's whole claim to being a real aspect, per DESIGN-ASPECTS
section 1, is that the belief "nothing should remain fixed" is true of Maeve on every single fight
she is in. If the energy-overflow rule only fires when she is wearing a specific relic, "Tide" stops
meaning anything on the ninety percent of her battles where the relic slot holds something else, or
nothing. The same argument applies to Verdant and the heal clamp, word for word.

Both rules were also read off existing kits, not invented for this decision. DESIGN-ASPECTS section
1 is explicit that all six identities came from what the roster already does, and section 3 confirms
it for these two specifically: Maeve's `Rising Chorus` ticks energy every one of her turns and is
already the hero who most obviously wastes it at cap; Wren's `Verdant Tide` heals `AllAllies` and is
already the hero whose output goes to waste on a healthy line. Tide's rule and Verdant's rule are not
proposals, they are descriptions of what Maeve and Wren are already trying to do, finally given a
mechanic. A relic cannot own a description of a hero's own kit; that is not equipment, that is the
hero.

And there is nowhere else to put either relic. DESIGN-HOOK-CONFLICT section 1 shows that Tide's
entire identity in DESIGN-ASPECTS is this one hook, with no second mechanism to fall back to if it
loses the clamp; the same document's section 2 shows the same is true of Verdant. Handing the clamp
to the relics does not narrow either aspect, it empties it, and DESIGN-ASPECTS section 6 already
established, before this document existed, that there are no spare hooks in the pipeline to give
either aspect instead. Nine hooks are claimed between six relics and six aspects and there is no
tenth. Cutting Tide or Verdant down to nothing to keep a relic alive is not a trade, it is deleting
one thing to save another that was never in danger.

## 2. Tidewrought Metronome: cut

Once Tide owns the energy clamp, Metronome does not become a smaller relic, it becomes a no-op. Its
named wearer is Maeve, the one hero DESIGN-ASPECTS counts as Tide. The overflow the Metronome wants
to bank never reaches its own code, because Tide's redirect already consumed it one hook earlier in
the same method. There is no version of "equip Metronome on Maeve" that does anything, because Maeve
is exactly the hero whose overflow the aspect rule claims first.

DESIGN-ASPECTS section 6 proposed a fix: move Metronome off the clamp entirely, onto `EnergyGain`
cost instead. DESIGN-HOOK-CONFLICT section 1 checked that proposal against the actual code and it
fails on its own terms. `EnergyCost` is a flat number read at cast time; a relic that reduces it is a
numeric discount on how much a cast costs, which is precisely what DESIGN-RELICS' founding rule
forbids in its own words, a stat stick describable as "costs 20 less energy to cast." Worse,
DESIGN-HOOK-CONFLICT also found that `AddEnergy` has no source tagging at all: the method cannot
currently tell "this energy came from Maeve's `Tidal Cadence`" apart from "this unit's own basic
attack refilled itself," so even a correctly-shaped relocation would need new plumbing before it
could distinguish what it is supposed to modify. Cut it, not relocate it. There is no relocation on
the table that survives contact with either the design rule or the code.

## 3. Green Hour Vessel: cut

Same shape, same verdict. Once Verdant owns the heal clamp, Green Hour Vessel is a no-op on its
named wearer, Wren, for the identical reason Metronome is a no-op on Maeve: the aspect rule claims
the overflow before the relic's code ever runs.

DESIGN-ASPECTS section 6 proposed the same kind of fix here too: move Green Hour Vessel to "the
`Shield` path Verdant doesn't take." DESIGN-HOOK-CONFLICT section 2 checked this against the actual
`EffectKind.Shield` handler and it does not exist to move to. The Shield branch has no discard
mechanism at all; `target.Shield += value` is unconditional, nothing is ever clamped or thrown away
there for a relic to intercept. Green Hour Vessel's entire premise is turning a discard into a
reward, and the Shield path never discards anything in the first place. This is not a relic that
lost its home, it is a relic whose premise only exists on the one branch Verdant now owns outright.
Cut it, for the same reason as Metronome: not narrowed, not relocated, gone.

Both cuts land in the same place. DESIGN-RELICS drops from six relics to four survivors, with two
gone entirely rather than rewritten, because both were built on a hook that an aspect now owns
before the relic's own code ever sees it.

## 4. Splitting Prism: keep, new cost built on `_rng.Chance`

Splitting Prism's problem was never the collision table in DESIGN-HOOK-CONFLICT, which correctly
calls it a content disagreement, not a hook fight. Its stated downside, "the Burn no longer spreads,
so Kindling has almost nothing to feed on," depends entirely on Kindling existing to be starved. Once
DESIGN-ASPECTS section 3 cuts Kindling outright, as the percentage stat stick the project's own
constraint forbids, that sentence describes a passive that is no longer in the game. The relic's
upside is untouched, its downside evaporates, and DESIGN-HOOK-CONFLICT section 3 correctly flags
that this turns it into a pure-upside relic, exactly what DESIGN-RELICS exists to prevent.

The fix is not to rewrite the relic's mechanic, which still does exactly what it should: retarget
`AllEnemies` Damage to `FrontEnemy`, `Hits` multiplied by the enemy count it would have hit. The fix
is to give it a new cost, and task 11 just handed the project exactly the hook to build it on.
DESIGN-COMBAT-STATS section 7 establishes, independently of this decision and before it was made,
that the `_rng.Chance(effect.Chance)` gate on `ApplyStatus` is claimed by neither the six relics nor
the six aspects: nine hooks taken between the two documents, and this one left untouched by both.
That finding did not change when Metronome and Green Hour Vessel were cut above; if anything it
strengthens it, since two of the nine claims that DESIGN-COMBAT-STATS was counting around no longer
exist. This is genuinely open hook space, not space contested by anyone else in this document.

The new cost: **the split applications do not roll their status, or roll once on the first target
only.** Pick the second. "Roll once, on the first target only" is a real, statable cost in the sense
DESIGN-RELICS demands, one sentence, no number: *the drill only afflicts the first thing it hits.*
It also keeps the relic legible against what it actually does. Splitting Prism turns a wide,
weak-per-target hit into a narrow, hits-multiplied one; a status effect authored to matter once per
`AllEnemies` cast (Orrin's `Cinderguard` Burn, Kalyx's `Cinder Bloom` Burn) was always going to
resolve multiple times against a spread cast and, before this relic, exactly once per enemy struck.
Under "roll once on the first target only," Splitting Prism keeps that one shot but throws away every
repeat the multiplied `Hits` would otherwise have bought it: more raw damage on a single target, but
the status rider that used to land three or five separate times now only gets one attempt at all,
ever, for the whole cast. That is a real trade a player has to weigh, not a cost that quietly
vanishes the way "no status roll at all" would (which is just deleting a status effect from an
ability, not a rule about splitting). "Roll once, first target only" is specifically about what
splitting costs, which is the sentence the relic needs.

This also answers DESIGN-COMBAT-STATS' own caution about the `_rng.Chance` gate: adding a landing
roll there is the highest-frequency draw site in the simulation, and a relic that reduces how often
that roll happens (from up to five draws at team size 5, one per enemy, down to one) is the cheap
direction to move in, not the expensive one. It costs the relic something and costs the RNG budget
nothing extra.

## 5. Warden's Debt: survives

DESIGN-HOOK-CONFLICT concluded that Warden's Debt does not survive Orrin's relabel to Lumen, on the
argument that its "double-edged" downside was built for an Ember tank whose self-shielding was
incidental, and that once interception is his identity rather than a side effect, a relic that
removes his interception "erodes the one thing his colour is" rather than costing him something
outside it. That conclusion is wrong, and it is wrong specifically because it is answering to
DESIGN-ASPECTS' original weighting of Lumen rather than DESIGN-WORLD's correction of it.

DESIGN-ASPECTS section 1 wrote Lumen as "Lumen intercepts," absorption first, and DESIGN-ASPECTS
section 5 called this the least trustworthy of the six identities, "derived by elimination" rather
than read off a kit, because DESIGN-ASPECTS had already used up the other five identities on the
other five heroes. Under that weighting, DESIGN-HOOK-CONFLICT's reading is defensible: if absorption
is what Lumen means, a relic that removes Orrin's ability to absorb is removing his color, not
bending it.

But DESIGN-WORLD section 1 overrides that weighting explicitly, and does not merely amend it.
"Everything should have a knowable place" is not a description of absorbing damage, it is "a
description of a function that returns the same answer no matter what was asked of it," and that
function is `FindTaunter`, which already outranks every single-enemy `TargetMode` in the game.
DESIGN-WORLD's own words: "Lumen is the aspect that makes targeting deterministic. Absorption is not
the identity, it is what happens when the answer is always you." That is a demotion stated as
plainly as DESIGN-WORLD states anything. Under this reading, Orrin's Lumen identity is Taunt, the
`FindTaunter` override, the fact that every single-enemy ability aimed at his side resolves onto him
whether or not he is the intended target. Shield is what that guarantee happens to cost him in HP,
not what makes him Lumen.

Read Warden's Debt against that corrected weighting rather than the original one. The relic redirects
`Shield` effects targeting `Self` to the lowest-HP ally. It does not touch Taunt, and it cannot: Taunt
is not a `Shield` effect, and `FindTaunter`'s override is untouched by anything in DESIGN-RELICS'
description of this relic. Orrin keeps every part of what DESIGN-WORLD says his color actually is,
the targeting determinism, the guarantee that he is where every hit lands, and loses only the ward
that used to make absorbing those hits survivable. He still resolves as the target for everything
aimed at his side. He now has no shield to eat any of it. That is not "erasing the one thing his
color is," under DESIGN-WORLD's own definition of what his color is, that is making his defining
mechanic strictly more costly to be true. Before the relabel, "double-edged" meant an Ember tank
traded a bonus for being squishier. After the relabel, under DESIGN-WORLD's weighting, it means the
one hero in the game whose identity is being the target that damage cannot avoid now takes that
damage with nothing between it and his HP bar. That is more double-edged than the original text, not
less, exactly because Lumen's real cost, per DESIGN-WORLD, was always going to be "you are always the
answer," and this relic is the piece of equipment that makes the "always" part unconditionally true.

DESIGN-HOOK-CONFLICT's error was not in the mechanics, which it read correctly; it was in answering
to a weighting of Lumen that a different, later document had already overridden. It is worth stating
directly, since DESIGN-HOOK-CONFLICT asked to be argued with rather than silently corrected: the
disagreement is real, and DESIGN-HOOK-CONFLICT is the side of it that is wrong, because it was
written and reasoned before DESIGN-WORLD's correction to Lumen existed to be checked against.

Warden's Debt needs no new hook, no rewrite of its mechanic, and no new wearer. It needs its
justification text updated to describe absorption as what Lumen's targeting guarantee costs, rather
than as Lumen's identity, which is a paragraph, not a redesign.

## 6. What this leaves for the two cut relics

Metronome and Green Hour Vessel are cut, not relocated, per sections 2 and 3. DESIGN-RELICS drops to
four relics as specified (Splitting Prism, Warden's Debt, Stormlance Oath, Verdict Sigil) unless two
replacements are built to bring it back to six. This document names the space for those replacements
and does not design them: the `_rng.Chance` gate section 4 uses for Splitting Prism's new cost is
fresh hook space, claimed by neither the surviving relics nor any of the six aspects, and there is
room on it for more than one relic. Building the actual replacements is separate work, scoped to
whoever picks up the relic pass after this decision, not this document.

## 7. What this decides and what it does not

**Decided:** Tide keeps the energy-overflow clamp, Verdant keeps the heal-overflow clamp,
Tidewrought Metronome is cut, Green Hour Vessel is cut, Splitting Prism keeps its mechanic and gets a
new cost (split applications roll their status once, on the first target only, built on the
`_rng.Chance` gate), Warden's Debt survives the Lumen relabel unchanged in mechanic, with its
justification text updated. Kalyx's Kindling is cut, per DESIGN-ASPECTS section 3 and confirmed here
by section 4's resolution of Splitting Prism, which no longer depends on it existing. Orrin relabels
from Ember to Lumen, per DESIGN-ASPECTS section 5 and DESIGN-WORLD section 3, both of which reached
that conclusion independently of this document and neither of which this document reopens.

**Not decided here, and not this document's job:** the actual content and numbers for whichever
relics replace Metronome and Green Hour Vessel; the Umbra size-dilution question DESIGN-BUILD-ORDER
already defers to when task 12 reaches Umbra; anything about team size, campaign content, or the
retune, all of which stay exactly where DESIGN-BUILD-ORDER and DESIGN-COMBAT-STATS already put them.

DESIGN-BUILD-ORDER's task 12 sequencing note already says build Aspects before Relics, since two of
six relics needed rewriting once Aspects landed and two more needed re-arguing. That still holds
exactly as written: the two that needed rewriting are cut, not rewritten, and the two that needed
re-arguing (Splitting Prism, Warden's Debt) are argued in sections 4 and 5 above, both against
choices Aspects makes (cutting Kindling, relabelling Orrin) rather than independently of them.
Aspects first, Relics second, unchanged.
