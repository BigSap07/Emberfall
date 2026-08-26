# The World

Source: `DESIGN-WORLD.md`. Nothing here is implemented in code — this is fiction only, and it
explicitly does not decide balance, numbers, or difficulty.

## The First Light and the Emberfall

Before anything else there was the **First Light**: not a god, not a person, but the law that made
the world behave coherently. It broke — nobody knows why, and the doc insists that's staying
open, not a mystery being saved for a reveal. When it shattered, six surviving intentions fell into
the world. Each one kept exactly one idea about what existence *should* be. That shattering event
is **the Emberfall** — the title names the event, not the Ember aspect specifically, and it
deliberately doesn't privilege Ember over the other five (Ember is already over-represented in the
hero roster, so a title that reads as "the flame-coloured one" would look like favoritism it isn't).

## The six beliefs

The aspects are **beliefs, not elements**. Fire, water, wind — those are what a belief looks like
when it lands somewhere, not what the belief is.

- **Ember** — nothing changes without destruction. Escalates.
- **Tide** — nothing should remain fixed. Redirects.
- **Verdant** — what survives deserves to continue. Persists.
- **Gale** — nothing should be held in place. Disrupts sequencing.
- **Umbra** — not everything should be known. Removes and conceals.
- **Lumen** — everything should have a knowable place. Marks and defines.

None of the six is evil and none is the antagonist — each is a partial law behaving exactly like a
law, which is what makes the world hard to live in. See [Aspects](Aspects.md) for how each belief
maps to an actual combat mechanic.

## The Cinder and the Keeper

The player is the **Keeper of the Cinder** — an office, not a prophecy. Others have held it before
and will after; nothing about the player is unique.

The **Cinder** is a fragment of the First Light that never became an aspect — it took no side, and
that neutrality is exactly why it's useful. It can't create anything, but it can remember people
whose pattern imprinted on the First Light while it still held. Summoning recalls those patterns
into form. That's why the hero roster needs no shared history: Orrin held a gate, Nyx ended a
contract, Wren grew a garden in a crater, and there's no reason any of them ever met. They aren't a
party — they're six recollections standing in the same place because one office called them back.
This also means heroes can be added indefinitely without owing the story anything (a new hero only
has to have *existed*), and a hero's aspect is a belief they personally held in life, not a faction
they were assigned to.

Whether a recalled hero *is* the person, or just a copy of their pattern, is explicitly left open.

## Relics

Relics are objects recovered where the world's memory disagrees with the world itself — fragments
of an *alternative* rule, not a stronger version of an existing one. That's why relics never carry
a flat percentage bonus (see [Combat Systems](Combat-Systems.md) and the relic docs). The Cinder
can't remember relics at all — it only remembers people — which is the fiction's clue that whatever
a relic is a fragment of, it isn't a fragment of the First Light. The doc stops there deliberately.

## The six regions

One per campaign chapter, each named for what won there:

1. The Ash Marches — Ember
2. The Wandering Coast — Tide
3. The Rootbound Crown — Verdant
4. The Unmoored Reach — Gale
5. The Hollow Archive — Umbra
6. The Glass Dominion — Lumen

Chapter 6 reveals none of these are corruptions — every region is simply what its aspect genuinely
wants, achieved. No dark lord required or available.

**The doc flags a structural problem with this layout itself**: six mono-aspect regions is one
idea told six times, and it also fights the matchup-wheel narrowing (a mono-aspect region has one
correct counter-colour, which is exactly the "bring the counter-colour" compliance-check failure
the aspect work is trying to get away from). The proposal is to **contest Chapter 3** between
Verdant and Ember (their beliefs are direct opposites, and it's visible on one health bar via
`Regen` vs. `Burn` ticking on the same unit) and optionally contest **Chapter 5** between Umbra and
Lumen as a second pass. This is a proposal, not a decision — see
[Open Questions](Open-Questions.md).

## Orrin: relabelled, not rewritten

The World doc independently reaches the same conclusion [Aspects](Aspects.md) reaches from the
mechanics side: Orrin Kaal's kit (Shield, Taunt, DefenseUp) is Lumen's rule three times over, not
Ember's. The fictional argument: holding a gate alone for nine nights so a line would stay put is
"everything should have a knowable place," acted out at cost — the opposite of Ember's "the gate
should have fallen." Orrin's ash, his title, his lore, and his fire-red visual colour all stay —
the ash is *where he was*, not what he believed. This relabel has since been built (see
[Aspects](Aspects.md) and [Heroes](Heroes.md)).

## What this fiction deliberately does not decide

The doc is explicit about leaving these open (see [Open Questions](Open-Questions.md) for the
full running list):

- What shattered the First Light (may simply have ended — not a saved-up reveal).
- Whether the aspects are conscious or just consistent.
- Whether a recalled hero is the person or a copy of their pattern.
- Who else has held the office of Keeper, or holds it now.
- What relics are fragments of.
- Whether the First Light can or should be restored (no win condition implied).
- What "the Hollow" are (the content already uses the word like a faction — `hollow_husk`, the
  Hollow Archive — but the fiction hasn't said what they are).
- Whether shards (the summoning currency) are literally First Light fragments — suggestive, not
  canon.
- Whether a hero can hold more than one aspect — undecided in fiction, and currently impossible in
  code (`Aspect` is a single enum value per unit).
- Rarity (Common/Rare/Epic) has no fiction at all and isn't a statement about how well the Cinder
  remembers someone.
