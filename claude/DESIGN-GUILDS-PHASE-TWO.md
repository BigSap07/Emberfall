# Emberfall - guilds phase two

**Design record, 2026-09-22.** Phase one is built and in the tree: founding, codes, join
requests, promote, demote, hand over, kick, leave, disband, guild chat with a rate limit, a
30-member cap, and fighting any guildmate's line. `GuildScreen`, `GuildChatScreen`,
`GuildRequestsScreen` and `GuildService` are the client, six migrations back it, RLS on every
table, anon revoked, no INSERT grants.

Phase one deliberately excluded a guild shop, currency, raids, rewards, guild levels and
leaderboards. This document is the design for those, and for what replaces the ones that should
not be built as described.

## The bet

A guild is not a group that damages the same boss. It is a group that **solves the same fight**.

Combat is deterministic: a seed plus two lines through `DeterministicRng` is the entire fight, and
`CombatSimulation.Run()` returns the whole ordered event list up front. That means a fight is a
thing one player can hand to another, exactly, with no recording and no server-side combat.

Once a cycle the guild gets one fight: fixed seed, fixed enemy line, identical for every member.
Everyone attempts it with their own roster. Because it is deterministic, everyone is solving the
same problem, and a solution transfers. Guild chat stops being a greeting channel and becomes a
solving channel, because there is finally something that is the same for all of you.

No other game in this genre can do the handover, because none of them have a replayable
deterministic event list already sitting there.

## What this is not, and why

Three failure modes were named explicitly during design. Every rule below exists to prevent one
of them.

**The hero check.** A fixed fight whose answer is a named hero is the worst gate in a gacha:
most of the guild cannot participate, and the ones who can did not earn it, they pulled it.

**Compounding.** If guild output accelerates your roster, a better guild gives you a stronger
roster, which gets you into a better guild. That loop is what makes guild-shopping the primary
skill in every game where guilds went wrong.

**Overtaking the loop.** Always-on, infinitely grindable guild content becomes the game whether
or not that was intended.

## The fight

Scored, never pass or fail. A pass/fail fight invites hero checks by its nature, because the
design question becomes "what beats this" and there is always a best answer. A scored fight asks
how efficiently you beat it, so everybody clears it and the conversation is about the line rather
than the lockout.

It stresses a **behaviour**, not a unit: sustained healing, burst before a threshold, spreading
damage across five bodies, stripping a status, surviving a turn shift. With six aspects and the
role set, "this fight punishes single-target teams" has dozens of valid answers across 94 heroes.
"This fight needs Kael" has one.

A single-solution puzzle dies the moment somebody solves it. One person posts the answer,
everyone copies it, and the guild goes quiet until next cycle. A scored puzzle with many
approaches never fully closes, because there is always a tighter line.

**Handing over a fight** is the signature feature. You lose, you post the attempt to guild chat,
it embeds as a replayable battle. A guildmate opens it, watches your exact fight, and re-runs it
substituting their own roster.

## Scoring

Several boards over one fight, not one board:

- Fastest, by turns taken
- Cheapest, by total power spent
- Lowest average star rating
- Constraint boards: no healer, single aspect, and similar

Same fight, different lenses. Costs almost nothing to build, and it is where the creative play
lives. It also kills the hero check permanently: whatever a player's roster looks like, some
board suits it.

**Rank solutions, never guilds against each other.** A board sorted by damage or guild level is
won by whoever accumulated most and is dead on arrival. A board of records is non-zero-sum:
another guild beating your nine-turn clear takes nothing from you. That is the speedrun model,
competitive without ever becoming a barrier.

## Rewards

**Horizontal, not vertical.** Guild content pays in breadth: roster options, talent flexibility,
access to heroes you do not have, identity, cosmetics. It does not pay raw power faster than the
campaign. A solo player and a guild player progress vertically at the same rate; the guild player
has more options and more to do with them.

**No collective performance gating.** Everyone who participates earns the base reward, scored on
their own attempt. The bonus categories are recognition with small material value. The moment a
guild's combined output sets everyone's payout you get activity policing, kicking
underperformers, and recruitment stress.

**Bounded.** One fight per cycle, a handful of attempts. The cap, not the reward tuning, is what
stops this overtaking the game loop. Bounded attempts also make each one matter, which serves the
competitive side.

## Size

Scale the **content** to the guild, not the payout per head. A 30-member guild attempts the
30-member version, a 5-member guild gets a 5-member version, and a member of either earns roughly
the same. Bigger guilds clear more stuff; nobody is forced to chase the biggest guild.

The honest cost: a small guild sees less variety. There is no way around that one.

## The five systems, sequenced

1. **Guild dungeon.** The shared fight above, multi-floor so the guild collectively pushes and
   size scaling falls out naturally. Build this first. Everything else decorates it, and it is
   the only one that can be evaluated with two players.

2. **The Guild Path, fed by the dungeon's own currency.** Individually owned, so it cannot be
   taken away by a bad guild and it travels with the player if they leave, which is what prevents
   guild-lock-in. Keep it mostly breadth: extra loadouts, talent respecs, roster slots, summon
   options, rather than flat stat gains. (This record first called it a personal talent tree. It
   is the Guild Path everywhere now, because hero talents are a separate system and two things
   called talents would be confused forever.)

3. **Guild levels and upgrades, decided by leaders.** Upgrades **unlock content**, they do not
   grant power. "Guild level 5 unlocks hard mode" is fine; "+8% attack to all members" is the
   compounding trap. This gives a leader something to actually lead.

4. **Fodder exchange, not a marketplace.** Open player trading in a gacha creates real-money
   trading, makes the richest guild the best guild, and is a moderation burden. Fusion is built
   (`GameSession.Convert` spends heroes below five stars as fodder), so fodder is already the
   commodity, and a guild fodder exchange is genuinely useful without trading anything that
   breaks. It is no longer blocked on anything; it is simply fourth in line.

5. **Guild wars. Parked, possibly permanently.** Technically reachable, `team_snapshots` and
   async duels already exist. But it is the most compounding system on the list, it rewards
   whichever guild is biggest and most active, it needs matchmaking, and it cannot be tested with
   two players. See whether the dungeon carries the social loop on its own first.

## A testable property, not an intention

"No hero check" should be a harness check. Run each candidate guild fight against a spread of
sample rosters, deliberately including ones missing the obvious answers, and assert that a wide
range of compositions can clear it. The machinery already exists; the star bands and role medians
are the same kind of measurement. This finds a hero check at authoring time rather than from an
angry guild.

## On Discord

Do not compete with it. Guild chat is deliberately thin already: two second rate limit, no edit,
no delete. People will use Discord for the real conversation regardless, so the job is to give
them something worth discussing there. The shared fight does that by itself. A better in-game
chat would just be a worse Discord.

## Blocked on

Nothing, any more. Both entries this section carried have closed.

- **The economy decision is closed.** The x1.55 to x3.2 reading this section quoted was the old
  quadratic level cost, and the economy-curve pass and the campaign retune ended it. As
  `DESIGN-BUILD-ORDER.md` records it, the campaign is now re-solved under the wall rule (fusion
  accelerates a wall, it never gates one): `recommendedPower` is derived from a reference player
  who never converts and never ascends, and that player meets every ordinary stage inside its
  x1.10 to x1.30 band and the walls inside theirs, bar `2-6` and `2-12`, which settled at x1.197.
  A player who fuses meets `2-12` at about x3.3. What is still open there is what a player's power
  is made of (open decision 4), not the entry ratio. For this document the consequence is
  sharper than "rewards can now be priced": the player who fuses is already well above the
  curve, so guild content must pay into breadth only, and anything it paid into power would
  widen a gap that is already a multiple.
- **Fusion and ascension are built**, so the fodder exchange is not waiting on them (system 4).

The dungeon loop itself needed rewards to ship, not to be built.

## Explicitly excluded

- Damage-summing raids. Parallel solo play with a shared number.
- Hero-borrowing. It exists in other games to paper over hero checks; designed without hero
  checks, it is unnecessary, and it harms a collection game by making your own roster matter
  less.
- Open player-to-player trading.
- Guild versus guild ranking.
