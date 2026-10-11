-- 0014_guild_rule_gaps.sql - the dungeon seed is derived by the server, guild_touch writes at most
-- once a window, and guild codes cannot be walked. Docs/CLOUD-GAPS.md S8 and the A-list's
-- guild_touch and guild_by_code rows.
--
-- RUN AFTER 0013. It replaces guild_by_code, guild_request_send and guild_touch (0005) and
-- guild_dungeon_submit (0008), and adds a constraint to guild_dungeon_attempts (0007). 0011's
-- guild_dungeon_attempt is not touched: it calls guild_dungeon_submit, so it takes the seed rule
-- from there. 0013 is not read by anything here; it is "after" only so the numbers stay in order.
--
-- RE-APPLIABLE, like 0004-0013: tables are `if not exists`, the limits row is inserted `on
-- conflict do nothing` (so re-applying never resets a limit somebody raised), every function is
-- `create or replace`, and the constraint is added only when it is absent. Running it twice
-- changes nothing.
--
-- ---------------------------------------------------------------- what was wrong
--
-- S8. guild_dungeon_submit stored whatever p_seed it was sent. The dungeon seed is a pure function
-- of the guild and the cycle (GuildDungeon.SeedFor), so every honest row of a guild's cycle
-- carries the same one, and a row carrying another is a fight against a different dungeon: other
-- floors, other conditions, possibly an easier one. It still ranked on the guild's boards and,
-- since 0011, a board place pays. The rule test filed seed 1 and it was stored.
--
-- guild_touch had no limit. Every call is an UPDATE of the caller's membership row: a new tuple
-- and a WAL record each time, on the row guild_post_message and guild_dungeon_submit lock FOR
-- UPDATE. The game calls it at most once a minute (GuildService.Touch); a script calls it as fast
-- as the network allows, from as many throwaway accounts as anonymous sign-up will mint.
--
-- guild_by_code had no limit. A code is six characters from a 30-letter alphabet, so a script can
-- ask for random codes forever; each miss is one indexed read, and each hit hands back a guild's
-- name, description, member count and whether it is open, and an open guild can then be walked
-- into. guild_request_send answers the same question a different way (404 for no guild, 'joined'
-- or 'requested' for one), so throttling guild_by_code alone would only move the walk there.
--
-- ---------------------------------------------------------------- the limits the server holds
--
-- One row, in 0013's shape: the numbers live in the database rather than in the functions, so
-- raising one is an UPDATE and not a function rewrite.
--
-- touch_interval_seconds is 30. The client stamps at most once every 60 seconds
-- (GuildService.TouchIntervalSeconds), so an honest client is never inside the window, and a
-- touch that lands inside it (a restarted app, a stamp right after a chat post) loses nothing:
-- the stamp it would have written is at most 30 seconds newer than the one already there.
--
-- code_miss_limit and code_miss_window_seconds are 10 and 600: ten codes that matched no guild in
-- ten minutes, then every lookup is refused until the window ends. A hit is never counted, and a
-- malformed code never reaches the table (it cannot match anything, so it costs nothing and buys
-- nothing). A player who mistypes a code ten times in ten minutes waits out the rest of the ten.
--
--     update public.guild_rate_limits set code_miss_limit = 20, updated_at = now();
--
-- No client role can read or write it. Every function that reads it is SECURITY DEFINER.
create table if not exists public.guild_rate_limits (
  id                       boolean primary key default true check (id),
  touch_interval_seconds   integer not null check (touch_interval_seconds between 1 and 3600),
  code_miss_limit          integer not null check (code_miss_limit >= 1),
  code_miss_window_seconds integer not null check (code_miss_window_seconds between 1 and 86400),
  updated_at               timestamptz not null default now()
);

alter table public.guild_rate_limits enable row level security;
revoke all on public.guild_rate_limits from anon, authenticated;

insert into public.guild_rate_limits (id, touch_interval_seconds, code_miss_limit, code_miss_window_seconds)
values (true, 30, 10, 600)
on conflict (id) do nothing;

-- ---------------------------------------------------------------- the per-account tally
--
-- One row per account that has ever looked a code up. A fixed window: `window_start` is when the
-- current run of misses began, `misses` how many there have been since. A miss after the window
-- has ended starts a new one at 1.
--
-- `found_code` is the last code this account resolved to a guild. guild_request_send accepts only
-- that code, which is what makes it stop being a second, unthrottled way of asking whether a code
-- exists: an account can only ask to join a guild it has already found, and finding one is what
-- the tally bounds.
--
-- Keyed to profiles like every other per-account table here, so deleting the user takes it.
create table if not exists public.guild_code_lookups (
  user_id      uuid primary key references public.profiles (user_id) on delete cascade,
  window_start timestamptz not null default now(),
  misses       integer not null default 0 check (misses >= 0),
  found_code   text check (found_code is null or found_code ~ '^[2-9A-HJKMNP-TV-Z]{6}$')
);

alter table public.guild_code_lookups enable row level security;
revoke all on public.guild_code_lookups from anon, authenticated;

-- ---------------------------------------------------------------- the dungeon seed, on the server
--
-- GuildDungeon.SeedFor, statement for statement: 32-bit FNV-1a over the UTF-8 bytes of
-- "<guild id, trimmed and lower-cased>:<cycle in invariant digits>", then the top bit cleared.
-- uuid::text is already the lower-case canonical form the client lower-cases to, and the key is
-- ASCII whatever the database encoding, so the bytes are the client's bytes.
--
-- The arithmetic is in bigint and masked back to 32 bits after every multiply: the hash is below
-- 2^32 and the prime below 2^25, so no product reaches 2^57 and nothing overflows.
--
-- The DO block straight after it checks three vectors the C# function produces. If the port is
-- wrong, the migration stops THERE, before guild_dungeon_submit is replaced, so a wrong port can
-- never start refusing honest fights.
create or replace function public.guild_dungeon_seed_for(p_guild uuid, p_cycle integer)
returns integer
language plpgsql
immutable
as $$
declare
  bytes bytea;
  hash bigint := 2166136261;
  i    integer;
begin
  if p_guild is null or p_cycle is null then
    return null;
  end if;
  bytes := convert_to(lower(btrim(p_guild::text)) || ':' || p_cycle::text, 'UTF8');
  for i in 0 .. length(bytes) - 1 loop
    hash := ((hash # get_byte(bytes, i)) * 16777619) & 4294967295;
  end loop;
  return (hash & 2147483647)::integer;
end;
$$;

do $$
begin
  if public.guild_dungeon_seed_for('3f2a9c1e-7b4d-4e8a-9c21-5d6e7f8a9b0d', 37) <> 35606394
     or public.guild_dungeon_seed_for('00000000-0000-0000-0000-000000000000', 0) <> 1967870163
     or public.guild_dungeon_seed_for('FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF', 41) <> 734521770 then
    raise exception '0014: guild_dungeon_seed_for disagrees with GuildDungeon.SeedFor - nothing else in this file was applied';
  end if;
  raise notice '0014: guild_dungeon_seed_for agrees with GuildDungeon.SeedFor on all three vectors';
end;
$$;

-- ---------------------------------------------------------------- recording an attempt, with the seed checked
--
-- 0008's guild_dungeon_submit, every rule unchanged and in the same order, plus one: THE SEED IS
-- THE ONE THE SERVER DERIVES for the caller's guild and the current cycle. Refused with 400,
-- after the shape checks and before the floor gate and the cap, so a wrong seed spends nothing.
--
-- Checked rather than silently replaced. A client that sent another seed fought another dungeon,
-- and storing its result under this guild's seed would store a row that does not replay as the
-- fight it records. The one honest way to send a wrong seed is to change guild between asking
-- guild_dungeon_status and submitting, and that fight belonged to the other guild.
--
-- The signature is 0008's exactly, so `create or replace` swaps the body in place and the grants
-- survive. They are restated below anyway.
create or replace function public.guild_dungeon_submit(
  p_cycle           integer,
  p_floor           integer,
  p_seed            integer,
  p_team            jsonb,
  p_victory         boolean,
  p_turns           integer,
  p_power           integer,
  p_avg_stars       numeric,
  p_fallen          integer,
  p_content_version text
)
returns public.guild_dungeon_attempts
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid       uuid := auth.uid();
  mine      uuid;
  now_cycle integer := public.guild_dungeon_cycle_at(now());
  used      integer;
  result    public.guild_dungeon_attempts;
begin
  if uid is null then
    raise exception 'guild_dungeon_submit requires a signed-in user' using errcode = 'PT401';
  end if;

  -- The member's own row, FOR UPDATE, for guild_dungeon_record's reason: two submissions from
  -- one account in one instant queue here instead of both counting the same attempts used.
  select m.guild_id into mine from public.guild_members m where m.user_id = uid for update;
  if not found then
    raise exception 'you are not in a guild' using errcode = 'PT403';
  end if;

  if p_cycle is distinct from now_cycle then
    raise exception 'that dungeon cycle is not the current one (%)', now_cycle using errcode = 'PT409';
  end if;
  if p_floor is null or p_floor < 1 or p_floor > public.guild_dungeon_floor_count() then
    raise exception 'a floor is 1 to %', public.guild_dungeon_floor_count() using errcode = 'PT400';
  end if;
  if p_team is null or jsonb_typeof(p_team) <> 'array'
     or jsonb_array_length(p_team) < 1 or jsonb_array_length(p_team) > 5 then
    raise exception 'a team is one to five units' using errcode = 'PT400';
  end if;
  if p_seed is null or p_seed < 0 or p_victory is null
     or p_turns is null or p_turns < 0 or p_power is null or p_power < 0
     or p_avg_stars is null or p_avg_stars < 1 or p_avg_stars > 10 then
    raise exception 'an attempt carries a seed, a result, a turn count, a power and a star average'
      using errcode = 'PT400';
  end if;
  if p_fallen is null or p_fallen < 0 or p_fallen > jsonb_array_length(p_team)
     or (not p_victory and p_fallen < 1) then
    raise exception 'heroes lost is 0 to the size of the team, and a loss lost at least one'
      using errcode = 'PT400';
  end if;
  if p_content_version is null or char_length(p_content_version) < 1
     or char_length(p_content_version) > 64 then
    raise exception 'an attempt names the content it was fought under' using errcode = 'PT400';
  end if;

  -- The normalised line. A unit whose level is missing or not a number fails the comparison
  -- rather than being coerced.
  if exists (
       select 1 from jsonb_array_elements(p_team) u
        where jsonb_typeof(u) <> 'object'
           or jsonb_typeof(u -> 'level') is distinct from 'number'
           or (u ->> 'level')::numeric <> public.guild_dungeon_hero_level()
           or u ? 'relicId'
           or u ? 'armor') then
    raise exception 'every hero fights the dungeon at level % with no relic and no armor',
      public.guild_dungeon_hero_level() using errcode = 'PT400';
  end if;

  -- The seed (0014). Derived, never chosen.
  if p_seed is distinct from public.guild_dungeon_seed_for(mine, now_cycle) then
    raise exception 'seed % is not your guild''s dungeon for cycle %: the seed comes from the guild and the cycle',
      p_seed, now_cycle using errcode = 'PT400';
  end if;

  if p_floor > 1 and not exists (
       select 1 from public.guild_dungeon_attempts a
        where a.guild_id = mine and a.cycle = now_cycle and a.floor = p_floor - 1 and a.victory) then
    raise exception 'floor % opens when somebody in your guild clears floor %', p_floor, p_floor - 1
      using errcode = 'PT409';
  end if;

  select count(*) into used
    from public.guild_dungeon_attempts a
   where a.guild_id = mine and a.user_id = uid and a.cycle = now_cycle and a.floor = p_floor;
  if used >= public.guild_dungeon_attempt_cap() then
    raise exception 'no attempts left on floor % this cycle (% of %)',
      p_floor, used, public.guild_dungeon_attempt_cap() using errcode = 'PT409';
  end if;

  insert into public.guild_dungeon_attempts
    (guild_id, user_id, cycle, floor, attempt, seed, team, victory, turns, power, avg_stars,
     fallen, content_version)
  values
    (mine, uid, now_cycle, p_floor, used + 1, p_seed, p_team, p_victory, p_turns, p_power,
     round(p_avg_stars, 2), p_fallen, p_content_version)
  returning * into result;

  return result;
end;
$$;

-- ---------------------------------------------------------------- presence, at most once a window
--
-- 0005's guild_touch, with the write made conditional: the stamp moves only when the stored one is
-- at least touch_interval_seconds old. Inside the window the call writes NOTHING and answers the
-- stamp already there, so a caller still learns when it was last seen, and the answer is true.
--
-- Not a 429. The client ignores this call's answer and its failures by design (GuildService.Touch
-- is "silent about everything"), so a refusal would buy nothing a no-op does not, and a no-op
-- keeps 0005's contract: a stamp for a member, NULL for somebody in no guild.
--
-- No FOR UPDATE: the UPDATE takes the row lock itself, and under READ COMMITTED a second touch
-- that queued behind the first re-reads the row, finds the fresh stamp, and matches nothing.
--
-- guild_post_message and guild_share_attempt still stamp last_seen on every post. They have their
-- own two-second floor and are not touched here.
create or replace function public.guild_touch()
returns timestamptz
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid     uuid := auth.uid();
  gap     interval;
  stamped timestamptz;
begin
  if uid is null then return null; end if;

  select make_interval(secs => l.touch_interval_seconds) into gap
    from public.guild_rate_limits l where l.id;
  if gap is null then
    raise exception 'guild_touch has no guild_rate_limits row to check against - see 0014'
      using errcode = 'PT500';
  end if;

  update public.guild_members set last_seen = now()
   where user_id = uid and last_seen <= now() - gap
  returning last_seen into stamped;

  if stamped is null then
    select m.last_seen into stamped from public.guild_members m where m.user_id = uid;
  end if;
  return stamped;
end;
$$;

-- ---------------------------------------------------------------- finding a guild, at a bounded rate
--
-- 0005's guild_by_code, same arguments and same columns, plus the tally. Three changes beyond it:
--
--   * VOLATILE AND PLPGSQL, because it now writes. PostgREST runs a STABLE function in a read-only
--     transaction, so the old declaration could not hold the tally. The client already calls it
--     with POST (GuildService.LookupByCode), which a volatile function needs.
--   * A caller over code_miss_limit inside the window is refused with 429 BEFORE the guild table
--     is read, for every code, a real one included. Refusing only misses would still answer
--     "this one exists" to anybody who kept asking.
--   * A miss is counted; a hit is not, and records the code as the one guild_request_send will
--     take.
--
-- The tally row is created on the first lookup and locked FOR UPDATE on every one, so two lookups
-- from one account in one instant queue and count exactly. A refused lookup raises, which rolls
-- back nothing it needed to keep.
--
-- Every reference to a column of the limits or tally table is qualified: RETURNS TABLE makes `id`
-- and `guild_code` variables inside this body, and an unqualified `id` would be ambiguous.
create or replace function public.guild_by_code(p_code text)
returns table (
  id           uuid,
  name         text,
  description  text,
  guild_code   text,
  is_open      boolean,
  member_count integer,
  created_at   timestamptz
)
language plpgsql
volatile
security definer
set search_path = public, pg_temp
as $$
declare
  uid     uuid := auth.uid();
  wanted  text := public.guild_normalise_code(p_code);
  lim     integer;
  win     interval;
  tally   public.guild_code_lookups;
  expired boolean;
begin
  if uid is null then
    raise exception 'guild_by_code requires a signed-in user' using errcode = 'PT401';
  end if;
  -- Not a code at all: it can match nothing, so it is answered with nothing and not counted.
  if wanted is null then
    return;
  end if;

  select l.code_miss_limit, make_interval(secs => l.code_miss_window_seconds) into lim, win
    from public.guild_rate_limits l where l.id;
  if lim is null or win is null then
    raise exception 'guild_by_code has no guild_rate_limits row to check against - see 0014'
      using errcode = 'PT500';
  end if;

  insert into public.guild_code_lookups as t (user_id) values (uid)
  on conflict (user_id) do nothing;
  select * into tally from public.guild_code_lookups t where t.user_id = uid for update;

  expired := now() - tally.window_start >= win;
  if not expired and tally.misses >= lim then
    raise exception 'too many guild codes that matched nothing - try again in % seconds',
      greatest(1, ceil(extract(epoch from (tally.window_start + win - now()))))::integer
      using errcode = 'PT429';
  end if;

  return query
    select g.id, g.name, g.description, g.guild_code, g.is_open,
           (select count(*)::integer from public.guild_members m where m.guild_id = g.id),
           g.created_at
      from public.guilds g
     where g.guild_code = wanted;

  if found then
    update public.guild_code_lookups t set found_code = wanted where t.user_id = uid;
  elsif expired then
    update public.guild_code_lookups t set misses = 1, window_start = now() where t.user_id = uid;
  else
    update public.guild_code_lookups t set misses = t.misses + 1 where t.user_id = uid;
  end if;
  return;
end;
$$;

-- ---------------------------------------------------------------- asking to join, only a guild you have found
--
-- 0005's guild_request_send, every rule unchanged and in the same order, plus one after "already
-- in a guild": THE CODE MUST BE THE ONE THIS ACCOUNT LAST FOUND through guild_by_code. Anything
-- else is refused with 428 before the guild table is read, so the answer is the same whether a
-- guild holds the code or not, and this call stops being a way to test codes.
--
-- The game never sends anything else. GuildScreen looks a code up and offers JOIN only for the
-- guild it found, sending that guild's own code (GuildScreen.JoinRoutine). The tally is not
-- cleared by asking, so asking twice is still the same answer, not a refusal.
create or replace function public.guild_request_send(p_code text)
returns text
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid     uuid := auth.uid();
  code    text := public.guild_normalise_code(p_code);
  target  public.guilds;
  members integer;
begin
  if uid is null then
    raise exception 'guild_request_send requires a signed-in user' using errcode = 'PT401';
  end if;
  if code is null then
    raise exception 'a guild code is six characters from 23456789ABCDEFGHJKMNPQRSTVWXYZ'
      using errcode = 'PT400';
  end if;
  if public.my_guild() is not null then
    raise exception 'you are already in a guild - leave it first' using errcode = 'PT409';
  end if;

  -- The binding (0014).
  if not exists (select 1 from public.guild_code_lookups t
                  where t.user_id = uid and t.found_code = code) then
    raise exception 'look the guild up by its code first' using errcode = 'PT428';
  end if;

  select * into target from public.guilds where guild_code = code for update;
  if not found then
    raise exception 'no guild has the code %', code using errcode = 'PT404';
  end if;

  select count(*) into members from public.guild_members where guild_id = target.id;
  if members >= public.guild_member_cap() then
    raise exception 'that guild is full (% members)', public.guild_member_cap()
      using errcode = 'PT409';
  end if;

  if target.is_open then
    insert into public.guild_members (user_id, guild_id, role) values (uid, target.id, 'member');
    -- Everything else this player had out describes somebody who is now in a guild.
    delete from public.guild_requests where user_id = uid;
    return 'joined';
  end if;

  insert into public.guild_requests (guild_id, user_id) values (target.id, uid)
  on conflict (guild_id, user_id) do nothing;
  return 'requested';
end;
$$;

-- ---------------------------------------------------------------- privileges
--
-- The four replaced functions keep their signatures, so their grants survived the replace; they
-- are restated so this file stands on its own. guild_dungeon_seed_for is open to signed-in users
-- like 0007's other dungeon helpers: it is a pure function of two values every member already
-- holds, and the rules test asks it for the seed it expects the submit to accept.
revoke all on function public.guild_dungeon_seed_for(uuid, integer) from public, anon;
revoke all on function public.guild_dungeon_submit(integer, integer, integer, jsonb, boolean, integer,
  integer, numeric, integer, text) from public, anon;
revoke all on function public.guild_touch() from public, anon;
revoke all on function public.guild_by_code(text) from public, anon;
revoke all on function public.guild_request_send(text) from public, anon;

grant execute on function public.guild_dungeon_seed_for(uuid, integer) to authenticated;
grant execute on function public.guild_dungeon_submit(integer, integer, integer, jsonb, boolean, integer,
  integer, numeric, integer, text) to authenticated;
grant execute on function public.guild_touch() to authenticated;
grant execute on function public.guild_by_code(text) to authenticated;
grant execute on function public.guild_request_send(text) to authenticated;

-- ---------------------------------------------------------------- the seed rule, on the column
--
-- The second lock, in 0013's shape: a rule that can be a constraint is not left to a function
-- alone. Added NOT VALID, which enforces it on every write from this moment without reading the
-- rows already there, then VALIDATED only if no stored row breaks it. The live project holds rows
-- the rule test filed on purpose with seeds no guild derives (S8's seed 1, and the test's old fixed
-- seed), so expect the NOT VALIDATED notice there; the queries at the bottom list the rows.
--
-- The only UPDATE of this table anywhere is 0011's `boards` stamp on the row guild_dungeon_attempt
-- has just inserted, which already passed the rule, so an old row that breaks it is never
-- rewritten into a failure.
do $$
declare
  broken bigint;
begin
  if not exists (select 1 from pg_constraint
                  where conname = 'guild_dungeon_attempts_seed_derived'
                    and conrelid = 'public.guild_dungeon_attempts'::regclass) then
    alter table public.guild_dungeon_attempts
      add constraint guild_dungeon_attempts_seed_derived
      check (seed = public.guild_dungeon_seed_for(guild_id, cycle)) not valid;
  end if;
  if exists (select 1 from pg_constraint
              where conname = 'guild_dungeon_attempts_seed_derived'
                and conrelid = 'public.guild_dungeon_attempts'::regclass
                and not convalidated) then
    select count(*) into broken from public.guild_dungeon_attempts a
     where a.seed <> public.guild_dungeon_seed_for(a.guild_id, a.cycle);
    if broken > 0 then
      raise notice '0014: guild_dungeon_attempts_seed_derived: enforced on new writes, NOT validated - % stored attempt(s) carry a seed their guild and cycle do not derive',
        broken;
    else
      alter table public.guild_dungeon_attempts validate constraint guild_dungeon_attempts_seed_derived;
      raise notice '0014: guild_dungeon_attempts_seed_derived: validated against every stored attempt';
    end if;
  else
    raise notice '0014: guild_dungeon_attempts_seed_derived: already validated';
  end if;
end;
$$;

-- ---------------------------------------------------------------- what was applied, said once

do $$
declare
  limits public.guild_rate_limits;
begin
  select * into limits from public.guild_rate_limits l where l.id;
  raise notice '0014: guild_rate_limits: guild_touch writes at most once every % s; guild_by_code allows % miss(es) per % s',
    limits.touch_interval_seconds, limits.code_miss_limit, limits.code_miss_window_seconds;
  if (select p.provolatile from pg_proc p where p.oid = 'public.guild_by_code(text)'::regprocedure) = 'v' then
    raise notice '0014: guild_by_code is volatile, so PostgREST runs it read-write';
  else
    raise exception '0014: guild_by_code is not volatile - its tally write would fail under PostgREST';
  end if;
end;
$$;

-- PostgREST caches each function's volatility. Supabase reloads the cache on DDL by itself; this
-- says so explicitly, so guild_by_code is not run read-only from a stale cache.
notify pgrst, 'reload schema';

-- ---------------------------------------------------------------- after applying, by hand
--
-- Read-only, and nothing in this file runs them. They answer whether what 0014 installed is what
-- this file says, and what stored before 0014 breaks the seed rule. Rows filed by the rule tests'
-- throwaway users are expected in the second.
--
--   select * from public.guild_rate_limits;
--   select a.id, a.guild_id, a.cycle, a.seed, public.guild_dungeon_seed_for(a.guild_id, a.cycle) as derived
--     from public.guild_dungeon_attempts a
--    where a.seed <> public.guild_dungeon_seed_for(a.guild_id, a.cycle);
--   select conname, convalidated from pg_constraint
--    where conname = 'guild_dungeon_attempts_seed_derived';
--   select proname, provolatile from pg_proc
--    where pronamespace = 'public'::regnamespace
--      and proname in ('guild_by_code', 'guild_touch', 'guild_request_send', 'guild_dungeon_submit',
--                      'guild_dungeon_seed_for');
--   select public.guild_dungeon_seed_for('3f2a9c1e-7b4d-4e8a-9c21-5d6e7f8a9b0d', 37);   -- 35606394
--   select user_id, misses, window_start, found_code from public.guild_code_lookups
--    order by misses desc limit 20;
