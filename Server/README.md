# Server

Everything Emberfall keeps outside the device. There is no server process here and there is
not meant to be one: the game talks to Supabase's own REST endpoints directly, so what lives
in this folder is the database's shape and the script that proves its rules hold.

## Migrations

`supabase/migrations/`, numbered, run **in order**, pasted into the Supabase SQL editor as the
project owner:

1. `0001_profiles.sql` - accounts and friend codes. Creates the `profiles` table, the friend
   code generator, and the `auth.users` trigger that gives every new user a profile. Run this
   first; every file after it assumes a profile exists for every user.
2. `0002_saves.sql` - the cloud save and its concurrency rule.
3. `0003_social.sql` - friendships and team snapshots.
4. `0004_guilds.sql` - the four guild tables, the four caller-keyed questions every guild
   policy is written in terms of, and the policies themselves.
5. `0005_guild_rpcs.sql` - the guild calls whose rule spans more than one row: creating,
   joining, accepting, roles, leadership, chat and presence.
6. `0006_guild_team_snapshots.sql` - the one thing guilds change about a table they did not
   create: a guildmate may read a team snapshot, so a guildmate may be duelled.

`0001`-`0003` are idempotent enough to re-run (`create table if not exists`,
`create or replace`, `drop trigger if exists` before create), but their policies are not:
re-running one of those three raises `policy ... already exists`, and re-applying it cleanly
means dropping its policies first. **`0004`-`0006` are fully re-appliable** - every policy in
them is preceded by `drop policy if exists` - so pasting one of them twice, or pasting it again
after a half-finished run, is a no-op rather than a failure to unpick.

Nothing here uses the service role key, and nothing in the game ever will. The client holds
the anon public key and a user's own access token, and every rule in these files is written to
be correct against exactly that.

## Two locks on every table

Each table is protected twice, and the two locks answer different questions.

**Row Level Security** decides which *rows* a signed-in user may touch. **Grants** decide
which *verbs and columns* a role may use at all. Where a rule can be expressed as a missing
privilege rather than as a policy, it is:

- `profiles.friend_code` is not grantable, so no client statement can rewrite a friend code.
- `saves` grants `select` only. Every write goes through `push_save`, which is what makes the
  revision check a fact about privileges rather than a convention the client is asked to keep.
- `friendships` grants `update (status)` only, so accepting a request cannot also rewrite who
  the request is between.
- `guilds` grants `update (description, is_open)` only, so a leader cannot rename a guild out
  from under its members or choose its code.
- `guild_members` grants no `insert` and no `update` at all. Joining, roles, leadership and the
  presence stamp are the RPCs in `0005`, so there is no statement a client can send that
  promotes itself or backdates its own "last seen".
- `guild_messages` grants `select` only. Every message goes through `guild_post_message`, which
  is what makes the one-message-every-two-seconds limit a fact about privileges rather than a
  convention the client is asked to keep - the same shape as `saves` and `push_save`.

`anon` is revoked on every table in every file. RLS with no anon policy already denies it; the
revoke is there so "no table is readable by the anon role" stays true even if a later policy is
written for `public` instead of `authenticated`.

## Where a rule cannot be a policy

A policy only ever sees the row in front of it. Every guild rule that has to count rows, write
two tables, or compare against a row that is not the one being written is therefore a function
in `0005` rather than a `with check`, and the tables carry no grant that would let a client
reach around it:

- **The 30-member cap** is held by `select ... for update` on the *guild's own row*, taken
  before the roster is counted. Two officers accepting two different applicants at the same
  instant would otherwise both read 29 and both insert; the lock makes the second queue, and
  the count it runs afterwards sees the member the first one added.
- **One guild per player** is `guild_members.user_id` being the *primary key*. Two different
  guilds accepting the same applicant lock different rows and do not queue, so the constraint
  is the only thing that can hold - and it does, whatever the timing.
- **At most one leader per guild** is a partial unique index, which is why
  `guild_transfer_leadership` demotes before it promotes: the index is checked per statement
  and cannot be deferred.
- **The chat rate limit** takes the poster's own membership row `for update` before reading
  when they last posted, so two simultaneous posts from one account queue instead of both
  passing the same check.

## Finding a guild you are not in

`profiles` are readable by any signed-in user, which is what makes friend-code lookup a plain
filtered select. **Guilds are not.** A table anybody can page is a list of every guild code in
the project, and with auto-accept on, a list of codes is a list of guilds anyone can walk into.
So `guilds` is readable only by its own members and by somebody with a request out to it, and
resolving a code you were given goes through `guild_by_code()` - one exact match, no second row,
no argument that widens it. This is the tightening `0001` describes for `profiles` and defers;
guilds start there rather than arriving there later.

## Anonymous users are authenticated users

An anonymous sign-in produces a real row in `auth.users` and a real JWT whose role is
`authenticated`. Every policy in these files therefore applies to an anonymous player exactly
as it does to one who has linked an email, and linking an email keeps the same user id - which
is why none of these tables have anything to migrate when a player converts.

## The live rule test

`Tools/supabase-rules-test.py` creates two throwaway anonymous users and proves the rules
above against the real project. It is the only thing here that needs credentials, and it takes
them from the environment rather than from a file. See the header of that script.
