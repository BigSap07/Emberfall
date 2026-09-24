# DESIGN-GUILD-TECH

The Guild Path as four node purchases is retired. What replaces it is a tech tree
with two layers: stat rungs that are always worth buying, and guild talents that
make one guild's heroes behave differently from another's.

Numbers are deliberately absent from this document. The harness derives them, the
same way it derives fusion caps and Guild Path pay. If a number appears here it is
wrong.

## The shape

Four family tracks. Heroes belong to a family by role, and families are grouped by
output profile rather than by flavour.

| Family | Roles | Heroes |
|---|---|---|
| Line | Bulwark, Vanguard | 28 |
| Blade | Striker, Breaker, Stalker | 20 |
| Grace | Mender, Binder, Warden | 21 |
| Reach | Marksman, Tinkerer | 25 |

Family names are placeholders until Pinoy names them from the world's own
vocabulary.

Each track carries three stat rungs and one guild talent slot offering a choice of
three.

## Rules

**G1. A hero's family is decided by its role, and by nothing else.** Not aspect, not
star tier, not who is in the line. A player who owns no Blade hero gets nothing from
Blade tech, and that is the cost of a narrow roster.

**G2. Stat rungs are the floor and guild talents are the reward.** The rungs exist so
there is always something to spend on. They are meant to be unremarkable. The talent
is the thing a guild remembers buying.

**G3. A guild talent is authored the way a hero talent is** — a trigger, effects,
conditions, overrides and appends, in the same vocabulary. It applies to every hero
of its family that the player owns. It is not a new engine.

**G4. The guild picks the talent, not the member.** One active per family, changeable
at cycle roll. Two guilds at maximum tech should field noticeably different sides,
and the argument about which one to run is the point.

**G5. Guild tech accelerates a wall, it never gates one.** The reference player that
recommendedPower is derived for never converts, never ascends, and has no guild tech.
A player with no guild finishes the campaign. A player with one arrives earlier and
is hit softer. This is the fusion rule, extended, and it is checked the same way.

**G6. Guild level raises ceilings and unlocks slots, and nothing else reads it yet.**
It gates how far a family's rungs may be bought and which family's talent slot is
open. A marketplace, a sixth floor, a fourth attempt and a guild hall may all read it
later. None of them is designed today, and guessing at them now means building them
twice.

**G7. Guild level XP required scales with the square root of member count.** A large
guild levels meaningfully faster per person than a small one, which is the
recruitment pressure a guild is supposed to have. The ceiling is the same for every
size: a large guild arrives sooner, it does not arrive somewhere a small guild cannot
reach. A permanent class divide by guild size is the failure state.

**G8. A guild talent must be measurable at parity.** The same instrument that grades
hero talents grades these, at 100% of recommended power, against the same floor. A
guild talent that cannot be felt is the failure the four-node Path already made once.

**G9. The Path's existing nodes become early guild level unlocks.** A saved line and
lines that keep their talents are a reasonable level 1 and 2 reward. That code is
built and works; it moves rather than being deleted.

**G10. No guild talent may be the best pick on every floor theme it can be drawn
against.** A family's three exist to be argued about, and an argument needs a floor on
which each side is right. So each one is authored as an ANSWER to something a floor is
made of, not as a size: a retaliation answers the floors full of bodies because it fires
once per blow taken, a stacking wound answers the one body that lives long enough to
carry the stacks, a Taunt answers a chooser because nothing else argues with one, a ward
against sweeps answers what comes at the whole line. A family passes when at least two of
its talents are the best pick on some theme. It fails two ways, and they are the same
failure in opposite coats: one talent never out-picked, or no theme separating the three
at all.

There is deliberately no ratio in G10. Two talents may measure a factor apart and both be
real picks as long as each answers a floor the other does not; two a hair apart are still
a fake choice if the same one always leads. What matters is whether a guild facing a Wall
cycle and a guild facing a Swarm cycle would reasonably pick differently.

## What is retired

The rule that owning the whole Path changes no fight. It was what kept the Path safe
and what made it pointless. G5 replaces it.

The no-collective-gating guarantee, that a member of a guild of two earns within 10%
of a member of a guild of thirty. G7 replaces it deliberately.

**The rule that a family's strongest option may be worth at most three times its
weakest.** It was written to catch a fake choice and could not: it passed all four
families while one option in each was the pick on every floor. Line read Dig In +39.1
against Hold the Wall +25.0 and Answer in Kind +17.6 on the parity ladder - comfortably
inside three times, and eight of the twelve talents were flavour text anyway. The rule
was measuring SIZE in one arena, and the thing that makes a choice real is WHEN, which
one arena cannot show. G10 replaces it.

**"Three different shapes" as the way a family's three are authored.** The first cut
offered a front-loaded one, a reactive one and a conditional one, on the argument that a
guild would pick for how a fight feels. Shape is invisible to an instrument that reads
one arena, so what the guild actually saw was three sizes. G10 asks for three answers
instead, and an answer is checkable.

## What the harness must prove

- Guild tech never gates a wall: walks with zero guild tech all finish, no wall waits
  on it. Modelled on the fusion check.
- Every guild talent changes a fight, from both sides of an arena, and clears the
  parity floor.
- Every family's choice is a real one: across the floor themes and the conditions each
  theme may draw, at least two of its talents are the best pick somewhere. The bar a lead
  has to clear is derived from what chance alone moves the same floors by, never authored.
- A guild of two and a guild of thirty reach the same ceiling, and the rate ratio is
  the authored one rather than an accident of the curve.
- The stat rung ceiling per guild level is the one the tree authors, and no rung is
  reachable below its level.
