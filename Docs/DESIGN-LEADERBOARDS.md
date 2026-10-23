# Design: leaderboards

Decision record from 2026-10-22. The reasoning is durable. Names and numbers are not.
`DESIGN-BUILD-ORDER.md` says what is built.

## What is built and what is not

- **Written, not applied:** `Server/supabase/migrations/0016_leaderboards.sql`, five tables, four functions
  and their privileges. Nothing has been run against any database.
- **Written, not run:** `Tools/supabase-leaderboard-rules-test.py`, which proves the rules against the live
  project once 0016 is applied. It needs the project's anon key, as the other two rule tests do.
- **Built in R28, not on screen:** the client side. `LeaderboardCodec` (Core) reads both functions' bodies,
  `Assets/Scripts/Game/Cloud/LeaderboardService.cs` holds the transport and the screen's controller, and
  `LeaderboardsPresenter` says what the screen draws. The screen itself is UI work, specified in
  `Docs/ui-specs/R28-SCREENS.md`.

## The three boards

| Board | Value | Read from the save | Resets |
|---|---|---|---|
| Deep Recital | highest floor | `recitalHighestFloor` | all-time board never; a **weekly** board every Monday 04:00 UTC |
| The Proving | highest standing | `provingHighestStanding` | never |
| Campaign | stages cleared | distinct ids in `clearedStages` | never |

Ties rank by who reached the value first, by the server's clock.

## The trust model

**The client never sends a score.** `leaderboard_submit()` takes no argument. It reads the caller's own row
in `saves`, the document `push_save` already accepted (0013: a non-empty object, a version the server
takes, under the size cap), and lifts the three numbers out of it. A board can never say more than the
player's own cloud save says.

**Every time is the server's.** Rows are stamped with `now()`, the week is computed from `now()`, and the
rate checks measure server time since the server last accepted a value. The device clock never reaches the
database. This matters more than it looks: the Recital's Echoes and the Proving's Brands regenerate on the
device clock (`GameSession.TryStartProvingAttempt` settles against the time it is handed), so a player who
winds their clock forward gets more attempts. The server's rate limit is what caps that.

**No table is readable or writable by a client.** Each has RLS on, no policy and no grant. Boards are read
through `leaderboard_top()`, which returns rank, display name, value and `is_me`, and never a user id.
`CloudChecks` in the harness asserts every table is revoked and every function is revoked from `anon` and
`PUBLIC`.

## The anti-cheat basics

Each board is judged on its own, so a refused Proving value does not hold back an honest campaign value.
A board ends **raised** (stored, stamped), **kept** (not higher than what is stored: an older device; boards
only rise) or **refused** (logged to `leaderboard_refusals` for the owner).

1. **Save version** at least 20 (`clearedStages` holds ids from 20 on).
2. **Well-formed numbers:** whole, 0 or more, at most nine digits. A string or a fraction is malformed, never
   read as a number.
3. **Consistent with the same save:**
   - Recital: `recitalCurrentFloor` = highest + 1 (`PlayerProfile.NormaliseRecital` keeps it so); a floor
     above 0 needs `ch2_8` cleared (the unlock table).
   - Proving: standing at most the highest standing; highest standing at most attempts x 3 (standing is only
     won, 3 a win); above 0 needs `ch2_4` cleared.
   - Campaign: every element is a string that looks like a stage id, none repeats, at most 72.
4. **Ceilings:** floor 5,000 and standing 100,000, sanity bounds only.
5. **Not faster than play:** a rise is allowed at most `burst + per_day x days` since the server last raised
   that board, or since the account's profile was created. Recital 10 + 20 a day, Proving 20 + 60 a day,
   Campaign 12 + 24 a day. A refused rise is not stored, so the allowance keeps growing and an honest fast
   player is accepted on a later submission.
6. **Flood limit:** one submission a minute per account (429), checked before anything is read.

All numbers are in the one-row `leaderboard_limits` table, so retuning is one `UPDATE`, as 0013's limits are.

### What it does not stop

- **A forged save that is consistent and climbs no faster than the rate.** The save is written by the client.
  The rate limit bounds how fast a forger rises; it cannot tell a forger from a strong player.
- **A modified client.** Combat runs on the device. A client that fakes wins writes a save that looks real.
- **Many accounts.** Anonymous sign-up is free. Each fresh account starts from the burst allowance, so a
  forger can put a new account at floor 10, standing 20 and 12 stages at once, and no higher.
- **An honest player whose account is newer than their play.** Accounts sign in at launch in a configured
  build (`CloudCoordinator.Begin`), so this is rare: a first launch with no signal, or a lost anonymous token.
  Their first submission may be refused and is accepted once server time catches up.

Closing the first two needs fights the server can replay. The guild dungeon already records a seed and a
content version for that reason (0007, 0008, 0014); the Recital could follow it. That is a much larger piece
of work and a separate decision.

## The weekly Recital board

The Recital is an endless ladder and nothing in the game resets it; resetting the mode weekly would be a game
change. The **board** resets: a week runs from Monday 04:00 UTC (`week_reset_hour_utc`, matching
`balance.json` `dailyResetHourUtc` and `DailyLoop.WeekOf`) to the next. A player is on a week's board once
they submit during it. Two readings are kept:

- `recital_week`: the highest floor held at the latest submission this week.
- `recital_week_climb`: floors climbed this week (best minus the floor the server had for them when the week
  began; a first-ever submission counts as the starting point, so it climbs 0).

The first favours veterans every week. The second rewards playing that week. Which one the game shows is the
owner's call; both are served. Weeks older than 8 are pruned by `leaderboard_submit`.

## How the client calls it

1. After a successful `push_save` (the sync already in `CloudSaveService`), call `leaderboard_submit` at most
   once a minute. Its answer says, per board, raised, kept or refused, and why.
2. To show a board, call `leaderboard_top(board, limit)`. It returns the top `limit` (1 to 100) and the
   caller's own row after them.
3. The service belongs in `Assets/Scripts/Game/Cloud`, beside `GuildService`. The screen is UI work, out of
   this pass.

## Rewards

None in this pass: a board pays nothing. If a reward is ever attached, it must be fixed per rank band, shown
before the week starts, and never random, and a rank must never be bought (`Docs/DESIGN-SEALED.md`: nothing
bought is random).

## The rules test

`Tools/supabase-leaderboard-rules-test.py`, standard library only, anon key only, refuses a secret key by
the same decoded-key guard as `Tools/supabase-rules-test.py`. About twenty throwaway users. It proves: no
table is readable or writable by anon or a signed-in user; the two helpers are not callable; a submission
with a value argument is no function (the client cannot send a score); no save means 404; a second submission
inside a minute means 429; `server_time` is the server's and `week_start` is the Monday 04:00 UTC before it;
each campaign, Proving and Recital refusal above, one by one; a refused board does not block another in the
same call; `leaderboard_top` rows carry no user id, mark `is_me`, come best first and hold to 100; the weekly
board shows a first submission and the climb board does not; and, with two 61 second waits (skip with
`--quick`), an older save keeps the board and a climb shows on the climb board.

## Owner steps

1. Apply `0016_leaderboards.sql` in the SQL editor, after 0013. It ends with a block that refuses to finish if
   any table is readable or writable by a client.
2. Make sure `save_limits.max_save_version` is at the build's `SaveSerializer.CurrentVersion` (26 at this
   writing; 0013 inserted 19). The rules test prints which version the server took.
3. Run `python Tools/supabase-leaderboard-rules-test.py` and delete the users it prints.

## Decisions for the owner

1. Which Recital weekly board the game shows: highest floor, or floors climbed.
2. Proving on highest standing (all-time, only rises, chosen here) or current standing (moves both ways, needs
   a weekly reset to mean anything).
3. The rate numbers, and whether a refusal should also hide the player until reviewed (today it only keeps
   their old value).
4. Whether boards ever pay, and if so the fixed table.
5. Whether to invest in server-replayed Recital fights, the only real answer to a forged save.
