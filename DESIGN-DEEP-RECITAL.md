# The Deep Recital

A specification for an endless PvE mode. This document extends the fiction in DESIGN-WORLD.md and
proposes mechanics for implementation. Nothing here is implemented. Numbers flagged as *illustrative*
are examples for a later balance pass; exact values are not decided here.

## The third category

DESIGN-WORLD establishes two categories of thing the Cinder relates to. **Heroes** are willing and
complete: patterns that imprinted on the First Light while it held, which the Cinder surfaces cleanly
on request. **Relics** are a separate kind entirely — objects not from the First Light at all, which
the Cinder cannot remember, and whose origin the fiction deliberately leaves open.

The Deep Recital surfaces a third. Pushed past the depth at which voluntary, clean recall is
possible, the Cinder reaches older strata: imprints that are partial, degraded, and not cooperative.
What surfaces is hostile rather than purposeful. It held a belief once. What remains of that belief
is not the person and not the relic's alien logic. It is the residue of a conviction, expressed as
aggression.

**These are not heroes.** The consent and completeness that define a hero are absent. A hero is a
recollection the Cinder holds at the surface and can call. What the Deep Recital surfaces is what the
Cinder holds at depth and should not be calling.

**They are not relics either.** Relics return nothing and are not from the First Light. These echoes
are from it — from its oldest and least-legible strata. The distinction matters: a relic is alien
evidence, something the world's memory misremembers. A degraded echo is the Cinder's own memory
failing under strain. One is a clue about something outside the Cinder. The other is a symptom of
the Cinder itself.

This is the same structural move DESIGN-WORLD already makes to keep heroes, relics, and the Cinder
distinct: three things that touch or touch on the First Light are three different kinds of thing, not
variants of one. The degraded echoes of the Deep Recital are a **third category of recalled thing** —
what the Cinder holds but cannot surface cleanly, sitting between the willing recall of a hero and
the total non-recall of a relic. They are called **echoes** in this document as a working term; whether
that word appears in UI copy is a decision made elsewhere.

---

## 1. Floor structure

One enemy team per floor. **Enemy team size equals `maxTeamSize`.** In `balance.json` that is
currently **5**. DESIGN-ASPECTS §7 documents the move from 3 to 5; the value there now is 5. If it
changes for campaign reasons it changes here too — the mode inherits it, because the point of the
Deep Recital is that the Cinder is overextended, and what it produces is no smaller for it.

**No HP or energy reset between floors within a single run.** The player's team carries its state —
current HP, active statuses, energy totals — from one floor to the next, exactly as the simulation
left them. A team that barely survived floor 12 arrives at floor 13 already damaged. A team that
burned its big abilities to close floor 12 arrives with lower energy. Floor difficulty escalates
continuously; what looks like a clean early run becomes a resource management problem as the run
deepens. This is the pressure the mode is built around.

A run ends when the player's team is wiped. The run's score is the highest floor completed.

---

## 2. Aspect bands

The mode's design content is not stat scaling. It is the shift in enemy **aspect composition**
across floor ranges.

The fiction drives the structure. Earlier floors surface the oldest, most degraded patterns — the
deepest strata of the Cinder's reach, furthest from what it holds clearly. Later floors surface
newer patterns, ones the Cinder holds with more fidelity even under the stress of a deep recall.
Which aspects dominate which floors is an expression of which beliefs are oldest and which are
recently held.

The following table is **illustrative and not final.** Exact floor ranges and aspect weights are a
balance decision made when content and tuning data exist. The fiction's ordering — from oldest and
most violent belief to most coherent and recently held — is the argument; the specific floor numbers
are not.

| Floor range | Aspect lean | Fiction reading |
|---|---|---|
| 1–15 | Ember, Umbra | The deepest and most damaged. Escalation without purpose; removal without motive. The beliefs the First Light held before it had context for them. |
| 16–30 | Gale, Verdant | Patterns that either disrupted or persisted. Older, but holding a recognisable shape. |
| 31–45 | Tide | Patterns that redirected rather than consumed. Still partial, but coherent enough to act with something resembling intent. |
| 46+ | Lumen, mixed | The newest strata the Cinder reaches under strain. The most organised echoes, and the most dangerous — not because they are complete, but because the order they want is legible. |

"Lean" does not mean mono-aspect. A band that leans Ember still contains other aspects, and the
composition shifts gradually rather than switching at a clean border. A floor near the edge of two
bands should already feel different from the centre of either. The mode should never read as six
chapters of one colour each — that is precisely the problem DESIGN-WORLD §2 identifies in the
campaign shape.

The band structure is also the mode's teaching mechanism. A player who reaches floor 16 and
encounters a different team shape learns something about the fiction's ordering of beliefs — and
has a reason to reconfigure their line for the next run. This is the same loop the contested
chapter proposal is built to produce: the world teaching through enemy composition, not through
a cutscene.

---

## 3. Stamina

A stamina-like resource gates how many runs the player can attempt. The resource regenerates over
time, whether or not the game is open — the same model `idleCapHours` uses for the passive income
system.

**Naming.** `balance.json` uses the `idle*` family for the income layer: `idleGoldPerHourBase`,
`idleShardsPerHourBase`, `idleCapHours`. The stamina resource is distinct from idle income and
must not share that prefix — the `idle*` keys belong to the cap-and-accumulate income clock, and
stamina is a separate gate on a separate mode. Proposed keys, following the same camelCase and
`Hours`-for-time convention already established:

- `recitalStaminaMax` — maximum stored attempts *(illustrative: 5)*
- `recitalStaminaRechargeHours` — hours to restore one attempt *(illustrative: 4)*

At the illustrative values: a player who exhausts their attempts waits up to 20 hours for a full
refill, gains one attempt every 4 hours, and banks no more than 5 before regeneration pauses.
These figures are placeholders; the right values depend on intended daily session length and
economy decisions this document does not make.

Regeneration accumulates while offline and is applied on next open, consistent with how
`idleCapHours` already works. No new clock is introduced; the same `LastIdleClaimUnix`-style
timestamp pattern applies.

---

## 4. Rewards

Rewards scale with floor reached. Every floor grants something on completion. **Milestone floors
grant larger or rarer rewards.**

The milestone interval is **illustrative and not final.** Every 5 floors or every 10 floors are
both reasonable proposals with different effects on how often the reward pulse arrives. Every 5
creates a more frequent drip; every 10 creates a cleaner psychological target and makes each
milestone feel more distinct. This is a pacing and economy decision for the balance pass.

Reward types are not specified here. What they should not be: flat gold or shards at amounts
equivalent to idle income, which collapses the mode into a faster version of waiting. DESIGN-IDLE-PROGRESSION
and DESIGN-REPLAY-ECONOMY are the relevant documents if a new reward category is introduced. This
mode does not introduce one without first consulting those.

---

## 5. Leaderboard

The mode ranks players by the highest floor reached in a given period. **This feature is the only
one in this document that cannot be implemented without server infrastructure this project does
not currently have.**

The full codebase contains no networking code of any kind: no `UnityWebRequest`, no sockets, no
HTTP clients, no REST calls, and no leaderboard service of any description. Every layer of the
project is local and offline — save data in `SaveService`/`SaveSerializer`, content in
`ContentDatabase`, progression in `PlayerProfile`, gameplay in `CombatSimulation`. A leaderboard
requires at minimum a persistent server that can receive floor scores from multiple players, store
them, and return a ranked list on request. Nothing here provides that.

The leaderboard is a design intent, not a specification. It is included because the genre
establishes the expectation: an endless mode without a visible rank is a mode without a ceiling
the player can aim at. In the short term that ceiling can be served locally by recording the
player's personal best floor in `PlayerProfile` alongside other persistent state. A personal best
requires no server, gives the player a target, and is honest about what it is. It is not a
leaderboard; it is not a substitute for one; it is what is implementable today.

Any work toward a real leaderboard begins with an infrastructure decision that is out of scope
here.

---

## 6. What this does not decide

**What echoes are called in copy.** "Echo" is a working term. Whether it appears in UI, in what
form, and whether players encounter the word or only the thing, is a copy decision made
elsewhere.

**Whether echoes have names, authored lore, or distinct visual identities.** The fiction
establishes the category; it does not require that every echo be a named entity. An echo can be
a hostile assembled pattern without a backstory. Whether some echoes receive authored identities
at milestone or boss floors is a content decision made when the mode is being built.

**The specific floor-band numbers and aspect weights.** Named in §2 as illustrative. The
fiction's ordering of beliefs from oldest to most recently held is the argument; which floor
number falls in which band is tuning.

**The milestone interval.** Named in §4 as illustrative. Every 5 or every 10 floors are both
proposals.

**The reward economy.** Named in §4 as deferred to DESIGN-IDLE-PROGRESSION and
DESIGN-REPLAY-ECONOMY.

**Stamina tuning.** Named in §3 as illustrative. Specific values depend on intended session
rhythm and economy decisions outside this document.

**Whether an echo can become a hero.** A complete enough echo, surfacing through enough
repetitions, could fictionally cross into something the Cinder holds willingly. This is not
decided here. If it is decided later, it is a summoning-system question as much as a Deep
Recital one, and it belongs in DESIGN-RECOLLECTION.

**Whether the Hollow are echoes.** DESIGN-WORLD §4 explicitly leaves the Hollow undecided — not
an aspect's work, not a byproduct of the Emberfall, not people who lost their pattern. Echoes
are not proposed as the answer to that question. The category the Deep Recital introduces does
not resolve anything DESIGN-WORLD flagged as open.

**Server infrastructure for the leaderboard.** Out of scope. Decided elsewhere.
