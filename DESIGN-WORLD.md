# Emberfall, the world

The fiction, and only the parts of it that are decided. This document reconciles against
DESIGN-ASPECTS.md; where the two disagree, the disagreement is named and one side is called wrong.
Nothing here changes a number, and nothing here is implemented.

## The name

Ember is an old word for a remaining spark, not for flame. The Emberfall is the event in which one
complete force shattered into six surviving intentions. The title names the event, not the Ember
aspect, and it does not privilege Ember over the other five. This matters because Ember is already
over-represented in the roster (DESIGN-ASPECTS 5), and a title that reads as a colour would make
that look intentional.

## The First Light

Before the aspects there was the First Light. Not a god and not a person: the law that made the
world behave coherently. It broke. Six surviving impulses fell into the world, each retaining one
idea of what existence should be.

Nobody knows what shattered it. It may simply have reached the end of its life. This is not a
mystery being withheld for a later chapter. It is not decided, and section "What this does not
decide" says so plainly so that nobody later treats a guess as canon.

## The six beliefs

The aspects are beliefs, not elements. Fire, water and wind are how a belief looks when it lands in
a place, not what it is.

- **Ember.** Nothing changes without destruction. Escalates.
- **Tide.** Nothing should remain fixed. Redirects.
- **Verdant.** What survives deserves to continue. Persists.
- **Gale.** Nothing should be held in place. Disrupts sequencing.
- **Umbra.** Not everything should be known. Removes and conceals.
- **Lumen.** Everything should have a knowable place. Marks and defines.

None of the six is evil, and none of them is the antagonist. Each is a partial law behaving exactly
as a law does, which is the only reason the world is difficult.

## 1. Belief against mechanic

DESIGN-ASPECTS derived six mechanical identities from the code without a fiction to answer to. Four
of them match the belief they now have to carry. Two do not.

| Aspect | Belief | Mechanic in DESIGN-ASPECTS 1 | Verdict |
|---|---|---|---|
| Ember | Escalates | Burn stacks instead of overwriting | Agree |
| Tide | Redirects | Energy overflow is handed on, not discarded | Agree |
| Verdant | Persists | Wasted healing is banked forward as Regen | Agree |
| Gale | Disrupts sequencing | A kill re-enters the round's turn order | Agree |
| Umbra | Removes and conceals | Debuffs are uncleansable and do not expire | Half wrong |
| Lumen | Marks and defines | Lumen intercepts damage | Mis-weighted |

**Ember agrees, exactly.** Escalation is a thing that gets worse because it already started. A Burn
stack that a second Ember deepens rather than replaces is escalation written as a data structure.
The belief also explains the cost the mechanic already carries: Ember buys damage that lands later,
which is what "nothing changes without destruction" sounds like when you have to wait for it.

**Tide agrees, and the verb is the same word.** The belief says redirects. The mechanic redirects
energy overflow to the ally furthest from casting. Nothing to reconcile.

**Verdant agrees, including the conditional.** "What survives deserves to continue" is a rule with a
precondition, and Regen is a mechanic with the same precondition: it does nothing for the dead, it
only extends the living. The belief is not generosity, it is a filter, and the mechanic filters the
same way.

**Gale agrees, and it is the cleanest of the six.** "Nothing should be held in place" is a statement
about order, and turn order is the only ordering the simulation has. Gale is the one aspect whose
belief could have been read straight off `TurnOrder()`.

**Umbra: the mechanic is half wrong, and the wrong half is the non-expiry.** "Cannot be cleansed" is
removal, and removal is in the belief. But "does not tick down while the applier lives" is
persistence, and persistence is Verdant's word, stated in Verdant's own belief. Two aspects cannot
own one verb; that is the entire reason DESIGN-ASPECTS gave them separate identities. A mark that
never fades is also a fixed, knowable, permanent fact about a unit, which is Lumen's belief, not
Umbra's. As written the Umbra mechanic borrows from the two aspects it least resembles.

Cut the non-expiry clause. Keep the uncleansable clause, which is removal of an option and sits
correctly. Concealment then points at a hook DESIGN-ASPECTS did not take: what `ResolveTargets` is
allowed to see. An Umbra that removes a unit from the targeting resolution is removing and
concealing, in the belief's own words, and it is not persistence.

That correction also answers a problem DESIGN-ASPECTS raised against itself. Section 8 flags Umbra
as the one identity that gets quietly worse as the team grows, diluted from a third of the enemy
line to a fifth, and marks it "3 only". Concealment does not dilute. More units on the field means
more targeting decisions available to corrupt, so the fiction happens to fix the size flag rather
than inherit it. This is a proposal for DESIGN-ASPECTS to absorb, not a specification here.

One objection worth answering before someone raises it: concealment inside targeting does not hide
anything from the player. `CombatSimulation.Run` stays a pure function and the event list still
records every resolution, so the battle screen can show exactly what was concealed from whom.
Concealment is a rule the simulation obeys, not information the record withholds.

**Lumen: the mechanic is not wrong, it is mis-weighted.** DESIGN-ASPECTS wrote Lumen as "Lumen
intercepts", with absorption first and Taunt second. Under the belief that ordering is backwards.
"Everything should have a knowable place" is a description of a function that returns the same
answer no matter what was asked of it, and that function already exists: `FindTaunter` overrides
every single-enemy `TargetMode`, including `LastAttacker` and `LastTarget`. Lumen is the aspect that
makes targeting deterministic. Absorption is not the identity, it is what happens when the answer is
always you.

This is a promotion, not a criticism. DESIGN-ASPECTS 5 admitted Lumen was the one identity derived
by elimination rather than read off an existing kit, called it the least trustworthy of the six, and
recommended building it last. The belief gives it the evidence it was missing. Lumen should still be
built last, but it is no longer the weakest entry.

**One naming collision, small but worth fixing.** ~~Nyx's passive is called `Shadowmark`, and marking
is Lumen's verb under this fiction, not Umbra's. Her mechanic is fine. Her passive is named after
the wrong aspect.~~ **Fixed in `claude/aspects-tide-umbra`:** renamed to `Hollowing`, which is
already the project's established Umbra word (`hollow_husk`, `Hollow Cut`).

**What the fiction does not excuse.** DESIGN-ASPECTS 3 found three of six heroes cross-wired, and
the Cinder does not give them cover. A recalled hero is a pattern, and a pattern is how that person
behaved, so a hero's abilities are their pattern. A kit that contradicts its aspect is not
characterful variety, it is the Cinder having recalled someone wrong. The cross-wiring is still a
bug in the content, and it should still be fixed.

## The Cinder and the Keeper

The player is the **Keeper of the Cinder**. An office, not a prophecy. Somebody held it before and
somebody will hold it after, and nothing about the player is unique or foretold.

The **Cinder** is a fragment of the First Light that never became an aspect. It kept no belief about
how existence should be, which is precisely why it is still useful: it takes no side among the six.
It cannot create. What it can do is remember people whose pattern imprinted on the First Light while
the First Light still held.

Summoning calls those patterns back into form. This is why the roster needs no shared history, and
the absence is deliberate rather than unwritten: Orrin held a gate, Nyx ended a contract, Wren grew
a garden in a crater, and there is no reason any of them met. They are not a party. They are six
recollections that happen to be standing in the same place because one office recalled them.

Two consequences follow, and both are load-bearing.

Heroes can be added indefinitely without a story debt, since a new hero needs only to have existed,
not to have belonged. And a hero's aspect is a belief they held in life, not a faction they enlisted
in. Nobody assigned Kalyx to Ember. Ember is what Kalyx thought was true.

## Relics

Relics are objects recovered where the world's memory disagrees with the world itself. They carry a
fragment of an alternative behaviour of reality: not a stronger version of a rule, a different rule.
That is why DESIGN-RELICS is able to insist that no relic contains a percentage. A relic is not more
of anything. It is evidence that the effect pipeline could have resolved another way.

The Cinder cannot remember relics at all. It remembers people, and a relic returns nothing. That is
not a limitation to be worked around later, it is the clue: whatever a relic is a fragment of, it is
not a fragment of the First Light the Cinder came from.

The fiction stops there. It does not say what relics are fragments of, and DESIGN-RELICS does not
need it to.

## The six regions

One per chapter, each named for what won there.

1. **The Ash Marches**, Ember.
2. **The Wandering Coast**, Tide.
3. **The Rootbound Crown**, Verdant.
4. **The Unmoored Reach**, Gale.
5. **The Hollow Archive**, Umbra.
6. **The Glass Dominion**, Lumen.

Chapter 6 reveals that none of these are corruptions. Every region is what its aspect genuinely
wants, achieved. The Glass Dominion is not a place Lumen ruined; it is a place where everything has
a knowable place, exactly as promised, and the player has spent five chapters learning what that
costs. No dark lord is required and none is available.

### 2. The problem: all six regions are the same shape

Six regions of one-aspect-wins is one idea told six times. The player learns the shape in chapter 1
and re-learns it five times, and by chapter 4 the only new information in a region is which colour
it is. Chapter 6's reveal also lands weaker for it, because a reveal that every region was honest is
less interesting when every region was also identical.

There is a mechanical cost on top of the narrative one. A mono-aspect chapter has a single correct
counter-line, which is precisely the failure DESIGN-ASPECTS 4 argued the matchup wheel must be
narrowed to avoid: bring the counter-colour, and team building becomes a compliance check. Six
mono-aspect regions make the wheel louder in exactly the place the wheel was supposed to get
quieter. The region layout and the wheel narrowing are pulling against each other.

The shipped content already disagrees with the premise, which is the strongest evidence available.
Chapter 1 is the Ash Marches under an older name, "The Ashen Road", and it contains Umbra husks,
Gale moths, Verdant hounds and a Tide wretch, with a stage called Drowned Causeway in the middle of
it. Its boss, the Sundered Colossus, is Umbra. The one region that exists is not mono-aspect and is
better for it.

### The proposal: contest chapter 3

**The Rootbound Crown should be contested between Verdant and Ember, not held by Verdant.**

Three reasons, in order of weight.

The beliefs are directly opposed, in their own words. "What survives deserves to continue" against
"nothing changes without destruction" is not two colours sharing a map, it is one argument with two
positions, and neither position is the villain. A player can hold both in mind at once, which is
what makes the chapter 6 reveal land later.

The opposition is visible in the simulation, on one health bar. Verdant banks health forward as
`Regen` and Ember stacks `Burn`. Both are entries in the same `Statuses` list, ticked by the same
loop in `TickDamageOverTime`, on the same unit, on that unit's own turn. One adds and one subtracts,
alternating, in the order they were applied. No other pairing of the six resolves this literally.
The contested region is legible without a single line of new UI.

Chapter 3 is the right slot. Chapters 1 and 2 establish the shape so that breaking it means
something, and chapter 3 is early enough that the lesson has three more chapters to pay off. Placed
at 5 it would be a twist; placed at 3 it is a rule.

**Second candidate, if more than one is wanted: contest chapter 5,** the Hollow Archive, between
Umbra and Lumen. Not everything should be known against everything should have a knowable place, in
the chapter immediately before the Glass Dominion. It pre-figures chapter 6 rather than duplicating
chapter 3, and it is the only pairing the matchup wheel already treats as a pair. The cost is that
it puts the game's two least-built aspects in one chapter, so it should follow chapter 3's contest,
not replace it.

Chapters 2, 4 and 6 stay single-aspect. The shape is not wrong, it is wrong six times.

## 3. Orrin: relabel, do not rewrite

DESIGN-ASPECTS 3 found Orrin Kaal labelled Ember with a kit that is Shield, Taunt and DefenseUp,
which is the Lumen rule three times over. Under this fiction the fix is **relabelling**, and the
fiction makes the argument stronger than the code did.

An aspect is a belief. Beliefs are not inherited from the place you were standing. Orrin held the
Cinder Gate alone for nine nights so that a line would hold and everyone behind it would stay where
they were: that is "everything should have a knowable place", acted out at cost. It is also the
opposite of "nothing changes without destruction". Ember's belief is that the gate should have
fallen. Orrin's whole recorded act is the refusal of that. He is not a weak Ember, he is a Lumen who
happened to defend an Ember place.

The ash is where he was, not what he believed. His name, his title "Bulwark of Ash", his lore and
his `visual.primary` of `C2502F` all stay exactly as they are, and the fiction now explains them:
the Cinder Gate is in the Ash Marches, so his fire is borrowed from the ground he stood on. Keep the
ash, change the belief. The reading gets better rather than needing a patch.

Rewriting him instead means rebuilding Cinderguard, Emberskin and his basic to escalate rather than
hold, discarding the only tank the starting roster has, and doing it to preserve a colour that his
own lore contradicts. Relabelling costs one string. Rewriting costs a hero and buys a worse one.

**The consequence has to be owned, because it is not free.** DESIGN-ASPECTS 5 notes that Umbra's
advantage has never once fired in this game, since `Beats` pairs Lumen and Umbra and no Lumen unit
exists. Relabelling Orrin activates that pair for the first time, and the first thing it does is
give every Umbra enemy live advantage against the player's only tank: the Hollow Husks in chapter 1
stage 1, and the Sundered Colossus that ends the chapter. The Ash Marches get harder at both ends
the moment this string changes. Thematically that is a gift, since the Hollow Archive's aspect
preying on the Glass Dominion's is the campaign in miniature. Practically it is a difficulty spike
in the first minute of the game and the last stage of the chapter, and it lands on the tutorial. It
is another reason to narrow the wheel in the same pass, per DESIGN-ASPECTS 4, and stage 12 should be
re-checked afterwards regardless.

## 4. What this fiction does not decide

Everything below is open. If a later document needs one of these, it decides it there and says so.
None of it should be inferred from anything above.

**What shattered the First Light.** Explicitly and permanently open. It may have simply ended. Do
not resolve this in a chapter.

**Whether the aspects are conscious.** Whether Ember wants anything or merely behaves consistently
is undecided. The beliefs are written as positions, not as opinions held by a mind, and it should
stay possible to read them either way.

**Whether a recalled hero is the person or a copy.** The Cinder recalls patterns. Whether the
pattern is the person is not answered, and the heroes themselves need not agree about it.

**Who else holds or held the office.** The Keeper is a role, so there were others. How many, whether
any are alive, and whether any is hostile is unwritten.

**What relics are fragments of.** Named as a clue, deliberately unanswered.

**Whether the First Light can be restored, or should be.** No win condition is implied by anything
here. Nothing above promises the game ends with the world fixed.

**What the Hollow are.** The shipped content already uses the word as if it were a faction:
`hollow_husk`, an environment called `hollow`, and chapter 1's subtitle "before the Hollow reach the
valley towns". This fiction does not say whether the Hollow are an aspect's work, a byproduct of the
Emberfall, or people who lost their pattern. Somebody will assume, so it is flagged here as
undecided rather than left quiet.

**Whether shards are literally fragments of the First Light.** The pull is obvious and already in
the content: shards are the summoning currency, the chapter opens at Shardfall Verge, and stage 12
is "The shard did not land. It stood up." That is suggestive and it is not canon. Decide it in an
economy document if it needs deciding.

**Whether a person can hold more than one aspect.** Undecided in fiction, and currently impossible
in code: `UnitDef.Aspect` is a single enum value. Anyone who wants a two-aspect hero is changing a
type, not just a story.

**Six regions is not a claim that the world has six places.** It is a claim about six beliefs and
six chapters. Whether anywhere exists outside them is unwritten.

**No number.** Nothing here decides balance, rarity, level caps, team size or difficulty. Rarity in
particular has no fiction at all: Common, Rare and Epic are not statements about how well the Cinder
remembers someone, and should not be turned into one by accident.
