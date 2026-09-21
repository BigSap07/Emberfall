# Server

Everything Emberfall keeps outside the device. There is no server process here and there is
not meant to be one: the game talks to Supabase's own REST endpoints directly, so what lives
in this folder is the database's shape and the script that proves its rules hold.

## Migrations

`supabase/migrations/`, numbered, run **in order**, pasted into the Supabase SQL editor as the
project owner:

1. `0001_profiles.sql` - accounts and friend codes. Creates the `profiles` table, the friend
   code generator, and the `auth.users` trigger that gives every new user a profile. Run this
   first; the other two assume a profile exists for every user.
2. `0002_saves.sql` - the cloud save and its concurrency rule.
3. `0003_social.sql` - friendships and team snapshots.

Each is idempotent enough to re-run (`create table if not exists`, `create or replace`,
`drop trigger if exists` before create), but the policies are not: re-running a file that
already applied raises `policy ... already exists`. Re-applying one cleanly means dropping its
policies first.

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

`anon` is revoked on all three tables. RLS with no anon policy already denies it; the revoke
is there so "no table is readable by the anon role" stays true even if a later policy is
written for `public` instead of `authenticated`.

## Anonymous users are authenticated users

An anonymous sign-in produces a real row in `auth.users` and a real JWT whose role is
`authenticated`. Every policy in these files therefore applies to an anonymous player exactly
as it does to one who has linked an email, and linking an email keeps the same user id - which
is why none of these tables have anything to migrate when a player converts.

## The live rule test

`Tools/supabase-rules-test.py` creates two throwaway anonymous users and proves the rules
above against the real project. It is the only thing here that needs credentials, and it takes
them from the environment rather than from a file. See the header of that script.
