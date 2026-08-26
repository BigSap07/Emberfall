# Emberfall — where the project is

**Deprecated. Superseded by `DESIGN-BUILD-ORDER.md`.**

This document described the project as of an earlier build — 3-a-side portrait combat, six
heroes, one twelve-stage chapter, no relics, no aspects, and no art at all. All of that has since
moved on (5-a-side landscape combat, relics and all six aspect identities shipped, two chapters
and 24 stages, real sprite art on six heroes and three enemies), and narrative status docs like
this one go stale the moment anything ships — keeping this rewritten in step would just be a
second copy of `DESIGN-BUILD-ORDER.md`'s "Already done, do not rebuild" section, drifting out of
sync with it the same way this one drifted from the code.

**`DESIGN-BUILD-ORDER.md` is the source of truth for what's built.** Its "Already done, do not
rebuild" section is the current equivalent of this document's old "What works right now"; its
numbered build order and "Numbers already decided" section track what's shipped versus what's
still open, verified against the actual code rather than against its own prior claims.

For everything else this document used to cover — the pitch, individual hero kits, how a battle
plays — `README.md` covers the same ground, but was not part of tonight's audit and was spot-checked
only in passing while writing this notice: its "auto 3v3" loop diagram, "No imported art" section,
and the `1.20 / 0.85` aspect multipliers in "Combat rules" all look stale against the same reality
this document was rewritten against (5-a-side, real sprite art now exists, and DESIGN-ASPECTS §4
narrowed the wheel to `1.10 / 0.92`). Not fixed here — out of scope for tonight's pass — but worth
a look before pointing anyone to it as current.
