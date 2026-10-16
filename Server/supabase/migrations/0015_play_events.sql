-- 0015_play_events.sql - the playtest's anonymous play stats: one insert-only table.
-- Docs/DESIGN-TELEMETRY.md. Nothing in the game reads from it; the owner reads it in the SQL
-- editor (Server/supabase/queries/play-counts.sql).
--
-- RUN AFTER 0014. It touches nothing 0001-0014 made: it adds one table, its constraints, its
-- indexes, its grants and its policy, and nothing else reads or writes any of them.
--
-- RE-APPLIABLE, like 0004-0014: the table and indexes are `if not exists`, the grants are revoked
-- and re-granted to the same end state, and the policy is dropped and created again. Running it
-- twice changes nothing. It does NOT widen the stored constraints on an existing table: if the
-- event list ever grows, that is a new migration that replaces play_events_event_known.
--
-- ---------------------------------------------------------------- why this is a table of its own
--
-- The playtest needs to see which onboarding step, stage or wave loses people, and how strong the
-- team was when it did. That is a count over events, not state, so it is one append-only table
-- keyed by an install id and not a column on profiles or a row in saves. The player has no
-- account to key it to: a stats row must be sendable by a build that never signed in, and must
-- stay unlinkable to a profile, a name or a save. The install id is a random GUID the client makes
-- only after the player says Yes, and deletes when they say No; nothing here joins it to auth.users.
--
-- ---------------------------------------------------------------- id: bigint identity
--
-- Not a uuid. Nothing refers to a row (no foreign key, no URL, no client ever reads one back), so
-- the one job of the key is to exist and to be cheap. A bigint identity is 8 bytes against 16,
-- orders by insertion for the owner's `order by id desc limit 50` eyeballing, and needs no
-- extension function. `generated always` also means a client that tries to set it is refused by
-- the database even if the column grant below were ever widened.
--
-- ---------------------------------------------------------------- who may do what
--
--   insert  : anon and authenticated, on FOUR columns only (install_id, event, payload, build).
--   select  : nobody. No grant and no policy. The anon key ships inside every build, so anything
--             it can read, every player can read: the table holds other players' install ids.
--   update  : nobody.
--   delete  : nobody. The owner empties the table in the SQL editor (which is not the anon role)
--             when the playtest ends, at most 90 days after the first row.
--
-- WHY ANON. The playtest has no account. A tester who never opens the Guild screen never signs in,
-- and the stats are most valuable from exactly those people, who stopped early. So the insert
-- must work for the anon role. `authenticated` gets it too, because a signed-in build sends with
-- the same code path and the user's token, and a policy limited to anon would refuse it.
--
-- WHY COLUMN-LEVEL. id is generated, and created_at is the server's clock. Granting insert on the
-- four columns means a client cannot back-date or forward-date a row: sending created_at is
-- refused with 42501 (permission denied for table play_events), not stored. The client never sets
-- id or created_at; the defaults below do.
--
-- RETURN=MINIMAL IS REQUIRED. With no select grant, an insert that asks PostgREST for the stored
-- row back (Prefer: return=representation, or any default that does a RETURNING of a column)
-- fails with 401/403, because RETURNING is a read. The client sends `Prefer: return=minimal`.
--
-- ---------------------------------------------------------------- the abuse risk, a KNOWN LIMIT
--
-- An open insert can be spammed. Anyone holding the anon key (it is in every build) can post rows
-- in a loop: junk event counts, install ids that never existed, a table that grows until the
-- project's storage quota. What is bounded is the size of ONE row, not the number of rows:
--
--   * event must be one of ten names, so the table cannot be used to store arbitrary text there;
--   * payload must be a JSON object of at most 2048 bytes as text, build at most 64 characters,
--     so a row is a few kilobytes at worst;
--   * nothing can be read back, so it cannot be used to exfiltrate or to host content.
--
-- NOT BUILT, on purpose: a rate limit. The shape for later, if the table is ever abused, is a
-- BEFORE INSERT trigger (or an RPC that replaces the direct insert grant) that counts the rows
-- this install_id wrote in the last minute and raises 429 over a limit, in 0014's guild_by_code
-- style. It would not stop an attacker who invents a fresh install_id per row, so a global ceiling
-- (refuse when the table passed N rows in the last hour) would sit next to it. Until then the
-- mitigation is the owner's eyes: the closing queries below show the busiest installs, and
-- `delete from public.play_events where install_id = '<uuid>';` removes one.
--
-- Rows that are junk but well-formed (a valid event, a made-up step id) are not detectable here.
-- The owner's queries count what the client sends; treat a very small playtest's numbers as
-- indications, not measurements, and treat an install with thousands of rows as an attacker.
--
-- ---------------------------------------------------------------- the table
create table if not exists public.play_events (
  id         bigint generated always as identity primary key,
  install_id uuid        not null,
  event      text        not null,
  payload    jsonb       not null default '{}'::jsonb,
  build      text        not null,
  created_at timestamptz not null default now(),

  constraint play_events_event_known check (event in (
    'session_start', 'session_end', 'onboarding_step', 'stage_attempt', 'wave_lost',
    'stage_clear', 'hero_level_up', 'summon', 'feature_opened', 'stats_opt_change')),
  -- A same-row check, so no function and no trigger: the row is refused by the table itself.
  -- octet_length of the text form is the size that counts, not the number of keys.
  constraint play_events_payload_object check (jsonb_typeof(payload) = 'object'),
  constraint play_events_payload_size check (octet_length(payload::text) <= 2048),
  constraint play_events_build_length check (char_length(build) between 1 and 64)
);

-- The owner's queries: counts per event over time, and everything one install did in order.
create index if not exists play_events_event_created_idx on public.play_events (event, created_at);
create index if not exists play_events_install_created_idx on public.play_events (install_id, created_at);

-- ---------------------------------------------------------------- privileges
--
-- Revoke first so re-applying lands on the same state however the table was left, then grant the
-- four columns. No table-level insert, no sequence grant: an identity column's sequence is used by
-- the table's owner on the inserting role's behalf and needs no USAGE for anon.
alter table public.play_events enable row level security;

revoke all on public.play_events from public, anon, authenticated;
grant insert (install_id, event, payload, build) on public.play_events to anon, authenticated;

-- An insert policy and no other: with RLS on and no select, update or delete policy, those three
-- are refused outright even if a grant were added by mistake. `with check (true)` because the
-- table's own constraints already bound what a row may be; there is no per-user value to compare
-- the row against (the caller has no account and install_id is the client's own claim).
drop policy if exists play_events_insert_any on public.play_events;
create policy play_events_insert_any
  on public.play_events
  for insert
  to anon, authenticated
  with check (true);

-- ---------------------------------------------------------------- what was applied, said once

do $$
declare
  who text;
  verb text;
begin
  if not (select c.relrowsecurity from pg_class c where c.oid = 'public.play_events'::regclass) then
    raise exception '0015: row level security is not on for play_events';
  end if;
  -- has_table_privilege sees table-level privileges only; has_any_column_privilege also sees a
  -- grant on a single column, so between them no read, update or delete path is missed.
  foreach who in array array['anon', 'authenticated'] loop
    foreach verb in array array['select', 'update', 'delete'] loop
      if has_table_privilege(who, 'public.play_events', verb) then
        raise exception '0015: % can % play_events at table level', who, verb;
      end if;
    end loop;
    foreach verb in array array['select', 'update'] loop
      if has_any_column_privilege(who, 'public.play_events', verb) then
        raise exception '0015: % can % a column of play_events', who, verb;
      end if;
    end loop;
    if has_column_privilege(who, 'public.play_events', 'created_at', 'insert')
       or has_column_privilege(who, 'public.play_events', 'id', 'insert') then
      raise exception '0015: % can insert id or created_at on play_events', who;
    end if;
  end loop;
  raise notice '0015: play_events is insert-only for anon and authenticated, on install_id, event, payload and build';
end;
$$;

-- PostgREST learns about the new table from this; Supabase also reloads on DDL by itself.
notify pgrst, 'reload schema';

-- ---------------------------------------------------------------- after applying, by hand
--
-- Read-only, and nothing in this file runs them. They are for the owner in the SQL editor, which
-- runs as a role that bypasses RLS, so they can read what the anon key cannot.
--
--   -- is the table there, with RLS on, and the four insert-only grants?
--   select relrowsecurity from pg_class where oid = 'public.play_events'::regclass;
--   select grantee, privilege_type from information_schema.role_table_grants
--    where table_schema = 'public' and table_name = 'play_events' and grantee in ('anon', 'authenticated');
--       -- expected: no rows (the only grants are column-level)
--   select grantee, column_name, privilege_type from information_schema.column_privileges
--    where table_schema = 'public' and table_name = 'play_events' and grantee in ('anon', 'authenticated')
--    order by grantee, column_name;
--       -- expected: INSERT on build, event, install_id, payload for each of the two roles
--   select policyname, cmd, roles from pg_policies where tablename = 'play_events';
--       -- expected: one row, play_events_insert_any, INSERT
--
--   -- how much is in it, and who is loudest (an install with thousands of rows is not a player)
--   select count(*) as rows, count(distinct install_id) as installs, min(created_at), max(created_at)
--     from public.play_events;
--   select install_id, count(*) as rows from public.play_events
--    group by install_id order by rows desc limit 10;
--
--   -- the rows the rules test left behind (its install id is all zeros but the last group)
--   select * from public.play_events where install_id::text like '00000000-0000-4000-8000-%';
--
--   -- retention: at the end of the playtest, at most 90 days after the first row
--   --   delete from public.play_events;
