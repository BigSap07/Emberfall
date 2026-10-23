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
7. `0007_guild_dungeon.sql` - the guild dungeon's attempt record, and the two calls that hold
   its clock and its cap: `guild_dungeon_status` and `guild_dungeon_record`.
8. `0008_guild_dungeon_playable.sql` - a content version and a loss count on every attempt,
   `guild_dungeon_submit` (one level for every line, floors that open as the guild clears
   them), and `guild_share_attempt`, which puts an attempt into guild chat as a playable card.
   Additive to `0007`, and re-appliable the same way.
9. `0009_revoke_guild_dungeon_record.sql` - takes `guild_dungeon_record` away from clients, so
   `guild_dungeon_submit` is the only way an attempt is written. Applied to the live project by
   hand first; the file is the record of it.
10. `0010_revoke_generate_friend_code.sql` - revokes `generate_friend_code()` from `public` and
   `anon`, the last function `0001` left callable with the anon key alone. Applied to the live
   project by hand first; the file is the record of it.
11. `0011_guild_path.sql` - the Guild Path: what the guild dungeon pays, and what it buys.
12. `0012_guild_tech.sql` - the guild tech tree that replaced the four-node Path. Run after `0011`.
13. `0013_save_and_name_rules.sql` - `push_save` refuses a document that is not a non-empty
   object, a `save_version` above the server's maximum, and a document over the size cap, both
   held in the one-row `save_limits` table; `display_name` takes the guild tables' text rule
   (`Docs/CLOUD-GAPS.md` S2 and S7). **Raise `save_limits.max_save_version` before shipping a
   build that bumps `SaveSerializer.CurrentVersion`**, or that build cannot sync.
14. `0014_guild_rule_gaps.sql` - the guild rule gaps `Docs/CLOUD-GAPS.md` recorded: the guild
   dungeon seed is derived by the server (`guild_dungeon_seed_for`) and any other is refused;
   `guild_touch` writes at most once per window; `guild_by_code` refuses an account that has
   missed too many codes, and `guild_request_send` takes only a code the caller has found. The
   limits are the one-row `guild_rate_limits` table. Written, not applied: the operator steps
   are in `Docs/CLOUD-GAPS.md`, under **Fixed, pending apply: 0014**.
15. `0015_play_events.sql` - the playtest's anonymous play stats (`Docs/DESIGN-TELEMETRY.md`): the
   `play_events` table, insert-only for `anon` and `authenticated` on four columns, with no
   select, update or delete. Applied by the owner by hand; nothing else in the project reads or
   needs it. `Tools/supabase-rules-test.py` `check_play_events` needs it applied. The owner reads
   the table with `supabase/queries/play-counts.sql` in the SQL editor.

16. `0016_account_deletion.sql` - `delete_my_account(p_confirm)`, the in-game account deletion Google
   Play requires: the caller's guild membership (a last-member leader's guild is disbanded, otherwise
   0004's succession promotes an heir), requests, friendships, team snapshot, save, profile and
   `auth.users` row, with everything else keyed to the profile going by cascade.
   `admin_delete_account(p_user)` is the operator's door for a web deletion request, granted to no
   client role. Written, not applied.
17. `0017_chat_safety.sql` - guild chat moderation: `user_block` / `user_unblock` (a blocked
   author's lines stop being returned by `guild_messages`' read policy), `guild_mute_member` /
   `guild_unmute_member` for leaders and officers (enforced by a trigger on `guild_messages`), and
   `guild_report_message`, which files a report with a copy of the message into `chat_reports`, a
   table no client role can read. Limits in the one-row `chat_safety_limits`. The owner reviews
   reports with `supabase/queries/chat-reports.sql`. Written, not applied.
18. `0018_server_time.sql` - `server_time()` and `clock_check(p_client_unix)`, so the client can tell
   a device clock moved forward from an honest one. The tolerance is the one-row `clock_rules`
   (300 seconds). Nothing calls it yet; see `Docs/LAUNCH-CHECKLIST.md`. Written, not applied.
19. `0019_purchase_receipts.sql` - the Google Play receipt ledger: `store_products` (which product
   ids the server verifies, seeded inactive with `store.json`'s placeholder ids) and
   `purchase_receipts` (one row per verified purchase, token stored as a SHA-256, readable by its
   owner on a few columns). Its three writers are granted to `service_role` only, for the Edge
   Functions below. `Docs/DESIGN-RECEIPT-VALIDATION.md` is the design. Written, not applied.

`0001`-`0003` are idempotent enough to re-run (`create table if not exists`,
`create or replace`, `drop trigger if exists` before create), but their policies are not:
re-running one of those three raises `policy ... already exists`, and re-applying it cleanly
means dropping its policies first. **`0004`-`0019` are fully re-appliable** - every policy in
them is preceded by `drop policy if exists` - so pasting one of them twice, or pasting it again
after a half-finished run, is a no-op rather than a failure to unpick.

Nothing in the game uses the service role key, and nothing in the game ever will. The client
holds the anon public key and a user's own access token, and every rule in these files is written
to be correct against exactly that. **The one server-side exception** is the pair of Edge Functions
under `supabase/functions` (below): they run on Supabase's servers, receive the service role key
from Supabase's own environment, and use it only to call the receipt writers in `0019`.

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

Every callable **function** is revoked from `public` and `anon` the same way. The last exception,
`generate_friend_code()` from `0001`, is revoked by `0010` (`Docs/CLOUD-GAPS.md` S6). `CloudChecks`
in the harness asserts that no callable function is left unrevoked, so a new one cannot arrive
unnoticed. Trigger functions cannot be called directly and need no revoke.

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

## Edge Functions

`supabase/functions` holds Google Play receipt validation, **written and type-checked, not
deployed**. `Docs/DESIGN-RECEIPT-VALIDATION.md` has the setup, the secrets and the client contract.

- `verify-google-purchase` - the game posts a product id and purchase token with the player's own
  token; the function asks Google, records the receipt through `0019` and answers whether to grant.
- `google-play-rtdn` - Google Play's real-time developer notifications (refunds, cancellations)
  arrive here from a Pub/Sub push subscription and revoke the receipt.
- `_shared/purchase_logic.ts` - every decision the two make, pure and tested by
  `tests/purchase_logic.test.ts` (`node --experimental-strip-types --test` or `deno test`).

## The live rule test

`Tools/supabase-rules-test.py` (accounts, saves, friends),
`Tools/supabase-guild-rules-test.py` (guilds and the dungeon) and
`Tools/supabase-launch-rules-test.py` (account deletion, chat safety, server time and the receipt
ledger, 0016 to 0019) create throwaway users and prove the rules above against the real project.
The launch test deletes its own users through `delete_my_account` when it finishes. They are the only things here that need credentials.
See the header of each script. Both carry the cases the cloud audit added, and both have been run
against the live project; `Docs/CLOUD-GAPS.md` records what each measured. A case labelled STILL
PRESENT asserts a defect that was written up and left alone, so it passes today and goes red the
day that defect is fixed.
