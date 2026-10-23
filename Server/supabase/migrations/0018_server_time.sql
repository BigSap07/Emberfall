-- 0018_server_time.sql - the server's clock, for the client's clock-cheat check.
--
-- WRITTEN, NOT APPLIED. Independent of every other migration: it reads no table but its own.
--
-- RE-APPLIABLE: the table is `if not exists`, its row is inserted `on conflict do nothing`, the
-- functions are `create or replace`, and the grants are restated.
--
-- ---------------------------------------------------------------- why this exists
--
-- Every daily rule in the game reads the device clock: the daily orders, the login calendar, the
-- weekly chest, Wayfarer's Road seasons, live events, store offer resets and idle income. The client
-- already refuses a clock wound BACKWARDS (`GameSession.RollDailyReset` and `StoreOffers` only move
-- forward), but a clock wound FORWARDS is believed. While nothing could be bought that was a player
-- cheating only themselves (`GameSession` says exactly that). With paid offers that reset weekly and
-- monthly, live events with paid tracks and guilds that compare progress, it is no longer harmless.
--
-- This file gives the client something to compare against. It does not change any rule by itself;
-- the client has to call it and act on the answer (Docs/LAUNCH-CHECKLIST.md, owner: code).
--
-- ---------------------------------------------------------------- the two calls
--
-- server_time() answers the server's clock: {"unix": seconds, "unix_ms": milliseconds, "iso": text}.
--
-- clock_check(p_client_unix) answers the same plus how far the device is off and whether the server
-- trusts it: {"server_unix", "client_unix", "skew_seconds", "tolerance_seconds", "trusted"}.
-- skew_seconds is client minus server, so a clock set into the future is positive. The tolerance
-- lives in a one-row table, so tightening or loosening it is an UPDATE and needs no client release:
--
--     update public.clock_rules set tolerance_seconds = 600, updated_at = now();
--
-- 300 seconds by default. A phone with automatic time is within a second or two; a round trip on a
-- bad mobile connection is a few seconds. Five minutes refuses a deliberate change of the date and
-- forgives everything an honest device does.
--
-- WHAT THE CLIENT SHOULD DO WITH IT (the proposal, for the code owner):
--   * Ask clock_check at boot and when the app returns to the foreground, when online.
--   * When trusted, carry on reading the device clock as today.
--   * When not trusted, date every daily and store rule by server_unix plus the time elapsed on a
--     monotonic timer since the answer, and say nothing to the player. A player whose date is wrong
--     by accident (travel, a dead battery) loses nothing; a player who moved it on purpose gains
--     nothing.
--   * Offline, keep today's behaviour. Idle income is capped (`Balance.IdleCapHours`) and the
--     backwards-clock refusal already holds, so the worst an offline cheat buys is a capped claim.
--
-- Authenticated only, like every other function in these migrations: the client signs in
-- anonymously before it makes any cloud call, so there is no caller this leaves out.

create table if not exists public.clock_rules (
  id                boolean primary key default true check (id),
  tolerance_seconds integer not null check (tolerance_seconds between 1 and 86400),
  updated_at        timestamptz not null default now()
);

alter table public.clock_rules enable row level security;
revoke all on public.clock_rules from anon, authenticated;

insert into public.clock_rules (id, tolerance_seconds)
values (true, 300)
on conflict (id) do nothing;

-- clock_timestamp() rather than now(): now() is the start of the transaction, and a PostgREST call
-- is one short transaction, so the two agree to a millisecond; clock_timestamp() says what is meant.
create or replace function public.server_time()
returns jsonb
language sql
volatile
security invoker
set search_path = public, pg_temp
as $$
  select jsonb_build_object(
    'unix',    floor(extract(epoch from t))::bigint,
    'unix_ms', floor(extract(epoch from t) * 1000)::bigint,
    'iso',     to_char(t at time zone 'utc', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')
  )
  from (select clock_timestamp() as t) now_row;
$$;

-- SECURITY DEFINER only to read clock_rules, which no client role can read.
create or replace function public.clock_check(p_client_unix bigint)
returns jsonb
language plpgsql
volatile
security definer
set search_path = public, pg_temp
as $$
declare
  server_unix bigint := floor(extract(epoch from clock_timestamp()))::bigint;
  tolerance   integer;
  skew        bigint;
begin
  if auth.uid() is null then
    raise exception 'clock_check requires a signed-in user' using errcode = 'PT401';
  end if;
  if p_client_unix is null or p_client_unix < 0 then
    raise exception 'clock_check needs the device time in unix seconds' using errcode = 'PT400';
  end if;

  select tolerance_seconds into tolerance from public.clock_rules where id;
  if tolerance is null then
    raise exception 'clock_rules has no row - see 0018' using errcode = 'PT500';
  end if;

  skew := p_client_unix - server_unix;
  return jsonb_build_object(
    'server_unix',       server_unix,
    'client_unix',       p_client_unix,
    'skew_seconds',      skew,
    'tolerance_seconds', tolerance,
    'trusted',           abs(skew) <= tolerance
  );
end;
$$;

revoke all on function public.server_time() from public, anon;
revoke all on function public.clock_check(bigint) from public, anon;
grant execute on function public.server_time() to authenticated;
grant execute on function public.clock_check(bigint) to authenticated;

do $$
begin
  if has_function_privilege('anon', 'public.server_time()', 'execute')
     or has_function_privilege('anon', 'public.clock_check(bigint)', 'execute') then
    raise exception '0018: anon can call a clock function';
  end if;
  if has_table_privilege('authenticated', 'public.clock_rules', 'select') then
    raise exception '0018: authenticated can read clock_rules';
  end if;
end;
$$;
