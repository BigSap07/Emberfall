# Emberfall - guilds phase two

> **SUPERSEDED IN TWO PLACES, 2026-09-24.** `DESIGN-GUILD-TECH.md` in the `claude` folder retires two rules this
> record is built on, and it wins where they disagree:
>
> - **"No collective performance gating"** and the flatness it implied - that a member of a guild
>   of two earns within 10% of a member of a guild of thirty - are **gone**. Guild level XP scales
>   with the square root of member count, so a large guild levels meaningfully faster per person,
>   and that is now the recruitment pressure a guild is meant to have. The harness assertion that
>   enforced flatness is deleted. What survives is that the CEILING does not move with size.
> - **"Horizontal, not vertical"**, and the rule that owning the whole Path changes no fight, are
>   **gone**. Guild progression is real power now. What replaces it is G5: guild tech accelerates a
>   wall and never gates one, the fusion rule extended, checked the same way.
>
> Everything else in this record still holds, including the three failure modes it names, the
> shape of the dungeon, the boards, and the Waymark pay - which is unchanged and still derived the
> same way. The section "The Guild Path, as built" is rewritten at the bottom.

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
   the only one that can be evaluated with two players. **Built and live.**

2. **The Guild Path, fed by the dungeon's own currency.** Individually owned, so it cannot be
   taken away by a bad guild and it travels with the player if they leave, which is what prevents
   guild-lock-in. Keep it mostly breadth: extra loadouts, talent respecs, roster slots, summon
   options, rather than flat stat gains. (This record first called it a personal talent tree. It
   is the Guild Path everywhere now, because hero talents are a separate system and two things
   called talents would be confused forever.) **Built, then replaced.** Its four nodes were bought
   with Waymarks; they are granted by guild level now, and what spends Waymarks is system 3. The
   "mostly breadth" instruction is retired - see the note at the top.

3. **Guild levels and upgrades, decided by leaders.** Upgrades **unlock content**, they do not
   grant power. "Guild level 5 unlocks hard mode" is fine; "+8% attack to all members" is the
   compounding trap. This gives a leader something to actually lead. **Built, and the second
   sentence is the part that changed**: `DESIGN-GUILD-TECH.md` in the `claude` folder is a tech tree that does
   grant power, held by G5 rather than by a ban. What survived intact is the leader's job - the
   guild picks one talent per family, changeable at cycle roll, and that is the decision a leader
   actually leads.

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

## The Guild Path, as built - and what replaced it

**Added 2026-09-23, when system 2 was built. Rewritten 2026-09-24, when the tech tree replaced
it.** The rest of this record is the design from 2026-09-22; this section says what was built from
it, what the numbers are, and where the design met the game as it stands and had to be read again.

**WHAT IS STILL TRUE:** the Waymark pay, its whole derivation, the three rules it is built on, the
boards, and who can mint. Those are unchanged and `GuildPathChecks` still holds them.

**WHAT IS NOT:** the Path as a storefront. Four nodes at 800 Waymarks was eight weeks of play, and
after that the currency bought nothing - which is the wall `DESIGN-GUILD-TECH.md` in the `claude` folder was
written against. The four nodes are guild level rewards now (G9); an account that bought one keeps
it, because `GameSession.OwnsGuildPathNode` reads bought-or-granted as an OR. What spends Waymarks
is the tech tree's stat rungs.

### What it buys, and the four items that did not survive

The Rewards section's test was applied to each item system 2 listed. A node must widen what a
player can do with what they own and never add to what it is worth in a fight.

- **Extra loadouts: built.** The **Line** nodes (Second, Third and Fourth Line) each add a saved
  line of five that the banner switches to with one tap (`GameSession.SwitchLine`). The line being
  left keeps its heroes, so a player can hold a floor-condition line, a campaign line and a Proving
  line side by side.
- **Talent respecs: nothing to sell.** A talent pick is already free, with no cost and no cooldown
  (`GameSession.SelectTalent` is the whole rule). A respec node would sell something every player
  already has. What a guild member can use is a line that remembers how its heroes were set, so
  **Lines Keep Talents** makes each saved line restore its heroes' picks when it is switched to.
  It is the same freedom made convenient, and no talent in it is new.
- **Roster slots: nothing to sell.** The roster has no cap to raise.
- **Summon options: refused, as vertical.** Fusion is built, and under fusion a copy is power:
  conversion turns fodder into a 5-star, and ascension spends copies of the same hero on a tier.
  `DESIGN-BUILD-ORDER.md` measures fusion as a two-to-three-times multiplier on the campaign's
  wall player. Any summon option that changes which heroes or how many land therefore changes how
  fast a player grows, which is the compounding trap under another name.
- **Identity and cosmetics: not built.** They are horizontal. But a title or a crest is worth
  something only if other people see it, which needs a surface guildmates can read, and that is
  more than this job. It is the natural next node family.

So the Path is four nodes in one branch, and that is an honest size for the breadth this game has
today, not a target. Every node kind is an allowlist entry in
`GuildPathChecks.CheckEveryNodeIsHorizontal`, which also fights the same stage event for event
with every node owned and with none. A kind added later fails the build until somebody has written
down why it is breadth.

### The currency: Waymarks

The guild dungeon's own currency, isolated like Coda and Valor, and spent only on the Path. The
server mints it and nothing else does. Three rules, each written against a failure this record
names:

1. **The base is scored on the member's own attempt, and on no floor in particular.** The first
   attempt of a cycle pays, won or lost, and the first clear of any floor pays again. Floor N opens
   only when somebody in the guild clears floor N-1, so a payout that grew with depth would be a
   payout the rest of the guild set. That is collective gating. Floor 1 is always open, so every
   member can earn the whole base whatever the guild has done.
2. **Only floor 1's boards pay.** The deeper boards are still boards and still recognition. They
   carry no material value, because a guild that opened more floors would otherwise hand its
   members more chances to be paid.
3. **A placement pays its share of the board beaten**: (n - rank) / (n - 1), so first is 1 and last
   is 0. Averaged over a board's entrants that is exactly one half at every size from two up, so a
   big guild pays no more per standing than a small one. A board of one pays nothing, because first
   of one is not a placement. Without that rule a guild of one would top all six boards every week
   and be the best-paid guild in the game. The best six scores of a cycle are paid, one guild's
   worth, so changing guild mid-cycle pays no more.

### The numbers, and the working

`DevHarness.exe --guild-path` is the study. It builds guilds of 1, 2, 5, 10, 20 and 30 members,
16 dungeons at each size. Every member is a fresh profile given 10, 30 or 60 pulls through the
real `GameSession.Summon` at the real rates and pity. Every member makes all three attempts on
floor 1: first their strongest line by stars, then lines drawn from what they own. Each line is
made legal under the floor's condition. The fights are real, the boards are `GuildDungeon.Rank`'s,
and 3,264 fights take about four seconds. `GuildPathChecks` re-runs it on every build-check.

**MEASURED** on the campaign of 72 stages and its 84 enemies, which is the pool the dungeon's
floors are now drawn from (`GuildDungeonGenerator.TemplatePool` takes only what some stage fields).
The run before that campaign landed gave the same clear counts and board-score means within 0.04,
and every number below derives to the same rounded pay:

| roster | cleared floor 1 in the cycle |
| --- | --- |
| new (3 + 10 pulls) | 364 of 365 |
| settled (3 + 30) | 363 of 363 |
| deep (3 + 60) | 360 of 360 |

| guild size | members | board-score sum per member, mean | median | p90 |
| --- | --- | --- | --- | --- |
| 1 | 16 | 0.00 | 0.00 | 0.00 |
| 2 | 32 | 1.09 | 1.00 | 2.00 |
| 5 | 80 | 1.34 | 1.33 | 2.50 |
| 10 | 160 | 1.45 | 1.38 | 2.54 |
| 20 | 320 | 1.47 | 1.37 | 2.61 |
| 30 | 480 | 1.42 | 1.36 | 2.50 |

**CHOSEN**, each a sentence of this record turned into a number:

- **Fairness floor 0.8.** A new roster must expect at least 80% of the base a deep one expects.
  This caps what a clear may be worth.
- **A clear is worth at most as much as turning up** (k <= 1).
- **The unit.** A member's mean cycle is 100 Waymarks, so prices read in weeks.
- **Board share 20%**, for "recognition with small material value".
- **The price rule.** A node costs its depth on the Path in mean cycles.

**DERIVED:**

- The clear-to-attempt ratio k: fairness allows k <= 0.2 / (0.8 q_deep - q_new). With
  q_new 0.997 and q_deep 1.000 that is unbounded, **so the cap binds and k = 1.** Floor 1 is
  cleared by practically every roster in three attempts, so the clear pay is set by the cap and
  not by fairness. If a content change hardens floor 1, the fairness floor starts binding and the
  gate says so.
- attempt = 100 x 0.8 / (1 + k q_all) = 40.02 -> **40**; clear = k x attempt -> **40**.
- S-bar, the mean board-score sum over sizes 2 to 30 weighted once each, = 1.355.
  board point = 100 x 0.2 / 1.355 = 14.76 -> **15**.
- Prices: **Second Line 100, Third Line 200, Lines Keep Talents 200, Fourth Line 300.** The whole
  Path is 800, which is 8 mean cycles, or 10 for a new-roster member paid the base and no board.

**WHAT THAT PAYS**, expected Waymarks per member per cycle:

| guild size | base | boards | total |
| --- | --- | --- | --- |
| 1 | 80.0 | 0.0 | 80.0 |
| 2 | 80.0 | 16.4 | 96.4 |
| 5 | 79.5 | 20.1 | 99.6 |
| 10 | 80.0 | 21.8 | 101.8 |
| 20 | 80.0 | 22.0 | 102.0 |
| 30 | 80.0 | 21.3 | 101.3 |

Every size from 2 to 30 sits within 4% of the mean cycle, and the gate fails past 10%. A guild of
one earns 80%. That is the "small guild sees less variety" cost, paid in recognition rather than
in base. The most any account can be paid in a cycle, whatever it sends, is 40 + 40 + 15 x 6 = 170.

The gate fails when a written pay number sits more than 10% from what the tree now derives, when a
price breaks the price rule, when fairness or flatness breaks under the written numbers, or when
the migration and `guild-path.json` disagree on any number.

### Who can mint

- **Only the server.** `guild_dungeon_attempt` (migration 0011) runs `guild_dungeon_submit`'s rules
  by calling it, then records the board bits and pays the base. `guild_path_state` settles every
  closed cycle's board bonus before it answers. The ledger has no client write grant, and its
  unique key on (user, cycle, kind) makes each payment a constraint.
- **What a modified client can still do.** Every attempt field is still the client's word, as it
  was in 0007, so a client can claim a clear or a board it did not earn. It cannot be paid more
  than an honest member who won everything: the cap bounds the rows, each kind pays once a cycle,
  and six placements at most are counted. Four of the six board bits (four or fewer, no five
  stars, first attempt, nobody fell) are checked against the row itself. Only one aspect and no
  healer are trusted, because the server holds no aspect and no role.
- **Keyed by account, never by guild.** The ledger and the unlocks reference `profiles`, so
  leaving, being removed or watching a guild disband takes nothing away. The Path screen is
  reached from the formation screen, which every player has, as well as from the dungeon.

### What is not done

- **Migration 0011 is written and not applied.** Until it is, the client files attempts through
  0008's `guild_dungeon_submit` (they count, and pay nothing), and the Path screen says the Path
  is not open on the server yet.
- **`guild_dungeon_submit` stays callable**, for the reason 0008 left `guild_dungeon_record`
  callable: `Tools/supabase-guild-rules-test.py` exercises it and has not been moved to
  `guild_dungeon_attempt`. Rows it writes pay nothing. The revoke is written in 0011's closing
  comment.
- **Nothing in the rules test covers 0011.** The live test needs the live project, which this job
  did not touch.
- ~~**After the Path, Waymarks have no sink.**~~ **Closed.** This was the whole reason the Path
  was replaced. The tech tree's twelve stat rungs cost 5,208 Waymarks against the Path's 800, and
  the guild level curve that opens them takes a mid-sized guild about a year - so a member finishes
  paying for the tree at about the moment their guild finishes opening it, which is one clock
  rather than two. See `DESIGN-GUILD-TECH.md` in the `claude` folder and `DevHarness.exe --guild-tech`.
