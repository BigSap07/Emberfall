# Emberfall - the shape of chapters 2 to 6

Chapter-level scope only. This document does not design a single stage, does not author a single
enemy composition, and does not pick a single level. That is task 10 scale work, times five, and
it is a different document's job. What follows is the shape a chapter is, read off the one chapter
that exists, and what shape chapters 2 through 6 should take from it.

DESIGN-WORLD is the source of the fiction here and nothing below invents past it. Six regions, one
per chapter, are decided. Chapter 3 contested between Verdant and Ember is decided. Chapter 5 as a
second contest, Umbra against Lumen, following chapter 3's rather than replacing it, is decided.
Where this document reasons past what DESIGN-WORLD states outright, that reasoning is marked as
this document's own inference, not as a fact pulled from the source.

## 1. What a chapter actually is, read off chapter 1

Chapter 1, the Ashen Road (the Ash Marches under an older name), is the only chapter that exists,
so it is the only evidence available for what a chapter is shaped like. Twelve stages is not
assumed as a rule here, it is what got measured.

**The power curve is not a straight line.** The twelve `recommendedPower` values run 2600, 3000,
3300, 3700, 4000, 4300, 4600, 4900, 5550, 5850, 5900, 6400. Most steps rise 300 to 400. Two steps
don't: stage 8 to 9 jumps 650, and stage 11 to 12 jumps 500. Stage 10 to 11 barely moves at all, 50.
Read plainly, the chapter climbs steadily for eight stages, spikes hard into stage 9, holds almost
flat across 10 and 11 as a breather before the finale, then spikes again into the boss. A chapter
is not an even ramp; it's a climb with one mid-chapter step-up and one run-up to the boss.

**Elites are not one moment, they're the back two-thirds.** Every stage from 4 through 11 except 9
fields at least one elite-kind enemy alongside its minions. Stage 6, subtitled "An elite guard, and
it has been waiting," is not the first elite appearance, it's the only stage where two elites
appear together with just one minion filling the third slot, the highest elite density in the
chapter outside the boss fight itself. That's what makes it read as a spike: not novelty, density.

**The boss fight has no elites in it at all.** Stage 12 is the boss plus two ordinary minions
(`grave_moth`, the chapter's default filler), nothing stronger standing beside it. And the boss
itself is authored at level 17, lower than the trash mobs surrounding stages 9 through 11 (levels
22 to 30). Every enemy in the game shares the same growth rate per level, so a boss reading far
above its own level number is a base-stat decision, not a level decision: `sundered_colossus`'s
raw stats (2450 HP, 232 Attack) run roughly 2 to 2.5 times a minion's and clearly past even an
elite's. A boss is a stat-block choice, not a leveling choice, and it earns the spotlight alone
rather than sharing the stage with anything else strong.

**One enemy carries half the chapter.** Across the chapter's 36 enemy slots, `grave_moth` fills 18
of them, exactly half, appearing in ten of the twelve stages. Three other minions
(`hollow_husk`, `cinder_imp`, `blight_hound`) get introduced early and retired by stage 4, never
seen again. The two recurring elites (`stone_warden`, `ashen_revenant`) span the back two-thirds
of the chapter. One elite (`tidecaller_wretch`) appears exactly once, a single-stage cameo. This
is the pattern to expect from a chapter, not a flaw to fix in this document: a workhorse filler
enemy, a handful of minions confined to the chapter's opening, elites that recur once introduced,
and at least one aspect represented by nothing more than a walk-on.

**The chapter is not mono-aspect at any layer, including the layers you'd expect it to be.**
DESIGN-WORLD already makes this point once, correctly: the Ash Marches, nominally Ember's region,
contains Umbra husks, Gale moths, Verdant hounds and a Tide wretch. Counting appearances by aspect
sharpens it further. By raw volume across the 36 slots: Gale 18, Ember 7, Umbra 5, Verdant 5, Tide
1, Lumen 0. Ember, the region's own named aspect, is not even the plurality; Gale is, by a wide
margin, because `grave_moth` is Gale and it's the default filler. And the chapter's boss is Umbra,
not Ember. A chapter named for one aspect is, in practice, filler-dominated by a second, elite-
weighted toward a third, and closed out by a fourth. Section 2 works out what that means for the
chapters still to come.

**Subtitles work at two scales.** The chapter itself carries one subtitle that frames a journey
and a deadline: "Follow the shardfall south, before the Hollow reach the valley towns." Each of
the twelve stages carries its own, a single specific observation about that place, never a generic
label: "Something came down here last night," "The road markers have been chewed." Neither is this
document's job to write for chapters 2 through 6, that's stage-level work, but the shape, one
framing line plus twelve specific ones, is part of what a chapter is.

**Environment is a separate axis from aspect, and its own bands don't match the region's aspect
either.** The `environment` field (which only chooses a background sky gradient) runs in three
even bands: `ashen` for stages 1 to 4, `ember` for 5 to 8, `hollow` for 9 to 12. A region can carry
more than one visual environment across its own run, and none of the three bands here is even
named for the chapter's own nominal aspect until the middle third.

## 2. What this means for chapters 2, 4 and 6

DESIGN-WORLD argues that six mono-aspect regions is one idea told six times, and proposes
contesting chapters 3 and 5 as the fix, leaving 2, 4 and 6 single-aspect. That's decided and this
document doesn't revisit it. What it adds, from section 1's evidence, is a read on how strictly
"single-aspect" should actually be held at the roster level, since the one chapter built so far
was never strict about it either.

**This document's own inference, not DESIGN-WORLD's:** hold the nominal aspect firmly at the
level of fiction, framing, and elite presence, the way Ember shows up as the most frequent elite
in the Ash Marches even while losing the volume count to Gale. Don't hold it at the level of
filler enemies or the boss. Chapter 1's precedent is that the boss doesn't have to match the
chapter's own aspect, and forcing it to would be a stricter rule than the one chapter that exists
actually follows. Whether a future single-aspect chapter's boss should share its aspect is
therefore an open call per chapter, not a rule this document is setting.

## 3. Chapter 2: The Wandering Coast, Tide

**Region and aspect.** The Wandering Coast, Tide, decided in DESIGN-WORLD's region list. Single-
aspect chapter, per section 2.

**What it teaches.** Chapter 1 gave Tide exactly one enemy, in one stage, once. Chapter 2 is the
first place a player would meet Tide at any real repetition, which makes it the first chapter to
teach a second aspect's identity by volume rather than by cameo. It's also the first proof, simply
by existing, that the six-region structure DESIGN-WORLD promises is real: chapter 1 could have
been a one-off tutorial region, and chapter 2 is what shows the player it wasn't.

**Difficulty.** Continues the climb chapter 1 established. No number is proposed here;
DESIGN-WORLD explicitly leaves difficulty undecided and stage-level tuning is out of this
document's scope regardless.

**Content bill.** Tide's entire current roster is one enemy, `tidecaller_wretch`, an elite.
A chapter built around Tide needs at least one Tide minion that doesn't exist yet, since reusing
`grave_moth` as the default filler across an entire Tide-nominal chapter would repeat chapter 1's
own imbalance at a more extreme ratio than chapter 1 itself. It very likely needs a Tide boss;
none exists in any chapter today.

## 4. Chapter 3: The Rootbound Crown, contested Verdant and Ember

**Region and aspect.** The Rootbound Crown, contested between Verdant and Ember rather than held
by either, decided in DESIGN-WORLD.

**What it teaches.** The first chapter where a single correct counter-colour stops being the
read. DESIGN-WORLD's own argument for why this works mechanically, not just narratively, is worth
repeating because it's the whole point of the chapter existing: Verdant banks health forward as
Regen and Ember stacks Burn, both entries in the same status list, ticked on the same unit, in the
order applied. The chapter teaches that two beliefs can be visible on one health bar at once, not
just on one map.

**Difficulty.** Heavier than chapters 1 and 2 by construction: two elite lineages present at once
rather than one region's worth. Again, no number proposed.

**Content bill.** The best-positioned of the five new chapters. Verdant already has two units from
chapter 1 (`blight_hound` minion, `stone_warden` elite) and Ember already has two (`cinder_imp`
minion, `ashen_revenant` elite). Neither aspect currently owns a boss, so the chapter likely needs
a new one regardless of which belief the story lets win at the finale, and DESIGN-WORLD does not
decide which that is. Neither does this document.

## 5. Chapter 4: The Unmoored Reach, Gale

**Region and aspect.** The Unmoored Reach, Gale, decided in DESIGN-WORLD's region list.
Single-aspect chapter, per section 2.

**What it teaches.** Gale is already the most-seen aspect in the game by raw volume, since
`grave_moth` is chapter 1's default filler in ten of twelve stages, but it has never been the
aspect a region is framed around, and it has never had an elite or a boss. This chapter is where
what Gale actually does, disrupting the turn order rather than just filling a slot, gets to matter
at the scale the fiction names it for, instead of reading as wallpaper.

**Difficulty.** Continues climbing past chapter 3. Worth naming without designing around it: Gale's
kill-re-entry mechanic doesn't exist in the simulation yet (see section 9), and a Gale-framed
chapter is exactly where a missing cap on it would be felt hardest, since it's the chapter asking
players to fight the most Gale units at once.

**Content bill.** Gale's entire current roster is one enemy, `grave_moth`, a minion, despite being
the single most-used enemy in the game. Zero Gale elites exist, zero Gale bosses exist. Of the
three single-aspect chapters left to build, this is the widest gap between how much the chapter
needs its aspect to feel distinct and how little unique roster exists to make it so.

## 6. Chapter 5: The Hollow Archive, second contest, Umbra and Lumen

**Region and aspect.** The Hollow Archive, contested between Umbra and Lumen, proposed in
DESIGN-WORLD as a second contest that should follow chapter 3's rather than replace it. Taken as
decided here, per this document's own scope, since DESIGN-WORLD frames it as the stronger of its
two candidates and names no alternative.

**What it teaches.** The sharpest direct opposition of any two beliefs in the six: "not everything
should be known" against "everything should have a knowable place." DESIGN-WORLD notes this is
also the only pairing the matchup wheel already treats as a pair, so the chapter pre-figures
chapter 6's reveal rather than repeating chapter 3's contest shape.

**One thing this document is required to flag and not resolve.** The chapter's own name is the
Hollow Archive, and "the Hollow" is already a live, unresolved question. `hollow_husk` and an
environment tag called `hollow` already ship in chapter 1, and its subtitle warns of "the Hollow"
reaching the valley towns as though it were a faction. DESIGN-WORLD states plainly that what the
Hollow actually are, an aspect's work, a byproduct of the Emberfall, or people who lost their
pattern, is undecided and flagged that way on purpose, so nobody treats a guess as canon.
Building a region called the Hollow Archive is the moment that question stops being avoidable:
either the name commits to an answer or the region needs to earn its name without one. This
document does not pick. Whoever writes chapter 5's actual fiction has to.

**Difficulty.** Heavier still, second to last.

**Content bill.** Umbra already has two units from chapter 1 (`hollow_husk` minion,
`sundered_colossus` boss), though `sundered_colossus` already belongs to chapter 1's own finale,
so whether it's reused here or a new Umbra boss gets authored is an open question, not answered
here. Lumen has zero units of any kind. If this contest is built, Lumen's content debt gets paid
two chapters ahead of its own nominal home, since chapter 5 comes before chapter 6.

## 7. Chapter 6: The Glass Dominion, Lumen

**Region and aspect.** The Glass Dominion, Lumen, decided in DESIGN-WORLD's region list. The last
chapter.

**The reveal, and what it means for how this chapter gets built, not what it says.**
DESIGN-WORLD's reveal is that none of the six regions was ever a corruption; each is its own
aspect's belief, achieved. That's not repeated here for its own sake, it changes a real production
choice for chapter 6 specifically: this cannot be built as a twisted, decayed final-boss kingdom,
the genre's default shorthand for a last chapter, because the reveal is that nothing about this
place is wrong. DESIGN-ART-BIBLE's own material language for Lumen already supports this if it's
followed rather than fought: enamel, ruled stone, engraved brass, porcelain, measured grids, "as
though reality requires documentation to remain real." That's a description of order and
craftsmanship, not menace. Chapter 6's art direction and its fiction both have to resist making the
Glass Dominion look sick or wrong, since making it look wrong is the one thing the reveal
specifically forbids.

**Difficulty.** Highest of the six, by construction, being last.

**Content bill.** The largest of the five. Zero Lumen enemies exist anywhere in the game today, so
this chapter needs an entire roster built from nothing: at least one minion, at least one elite,
and a boss, unless chapter 5's contest already originated some of that roster first, in which case
chapter 6 extends it rather than starting cold. DESIGN-ASPECTS already recommends Lumen be built
last among the six aspects, since it's the one identity derived by elimination rather than read
off an existing kit and the least trustworthy of the six as a result. That recommendation and this
chapter's own position line up on their own; nothing here forces it.

## 8. The content bill across all five chapters, in one place

The useful output of this document, gathered rather than scattered across five sections.

| Aspect | Roster today | Needed for | Gap |
|---|---|---|---|
| Ember | 1 minion, 1 elite | Chapter 3 (contested) | Best-covered of the five; still no boss |
| Verdant | 1 minion, 1 elite | Chapter 3 (contested) | Same as Ember; still no boss |
| Tide | 1 elite, 0 minions | Chapter 2 | Needs a minion at minimum; likely a boss |
| Gale | 1 minion, 0 elites | Chapter 4 | Widest gap of the three single-aspect chapters; needs an elite and likely a boss |
| Umbra | 1 minion, 1 boss | Chapter 5 (contested) | Boss already belongs to chapter 1's finale; reuse or replace is open |
| Lumen | Nothing at all | Chapters 5 and 6 | Largest gap outright; needs a full roster, twice as early as its own chapter if chapter 5 ships |

## 9. Named as dependencies, not designed here

Nothing new is invented in this document: no new mechanic, no new aspect, no new currency, no new
system. Where a chapter would want something that doesn't exist, it's named here as a dependency
for whoever builds that chapter, not specified.

- **Gale's kill-re-entry mechanic** does not exist in `CombatSimulation` yet. Chapter 4 is where
  its absence would be most felt.
- **Umbra's targeting-concealment correction**, proposed in DESIGN-WORLD section 1 as a fix for
  the mechanic DESIGN-ASPECTS shipped, is a proposal for DESIGN-ASPECTS to absorb, not a built
  rule. Chapter 5 leans on Umbra hardest of any chapter and inherits whichever version of the
  mechanic is actually built.
- **The relic/aspect hook conflict**, unresolved per DESIGN-HOOK-CONFLICT, sits upstream of all
  six aspect identities being real in the simulation at all, which sits upstream of any of this
  chapter's fiction being something a player can actually feel in a fight.
- **Which belief wins chapter 3's contest**, and whether that boss is Verdant or Ember, is not
  decided by DESIGN-WORLD and not decided here.
- **Whether `sundered_colossus` is reused for chapter 5 or replaced**, noted in section 6, is
  open.
- **What the Hollow are**, forced into the open by chapter 5's own name, noted in section 6, is
  explicitly not resolved here, on instruction and because DESIGN-WORLD flags it as deliberately
  undecided.

## 10. Sequencing, honestly

None of this can be built before the full twelve-stage retune, DESIGN-BUILD-ORDER task 14. That
task is stated as the only step that produces a valid win-rate table, with everything tuned before
it explicitly not meant to be read as final. Five more chapters authored against numbers that
aren't final yet would be tuned against a player who does not exist, the same problem
DESIGN-RECOLLECTION already named for its own system and declined to schedule around.

Every aspect identity this document leans on, Ember's stacking, Tide's redirect, Verdant's Regen,
Gale's re-entry, Umbra's concealment, Lumen's targeting override, is unimplemented. Chapter 3's
entire pitch is that Regen and Burn are visible on one health bar at once; that pitch is fiction
until the mechanics it depends on are code. Building chapter content against mechanics that don't
exist yet is building a chapter around a fight nobody can actually have.

This document adds no numbered task to DESIGN-BUILD-ORDER, and shouldn't. It's a shape for content
that has nowhere to load into yet, five chapters ahead of a `campaign.json` that still only
contains one.
