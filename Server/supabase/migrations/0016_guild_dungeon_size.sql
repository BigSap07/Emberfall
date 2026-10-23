-- 0016_guild_dungeon_size.sql - the guild dungeon's ladder scales with the guild, and a member's
-- attempts are bounded the same at every size. claude/DESIGN-GUILDS-PHASE-TWO.md, "Size" and
-- "Bounded"; DESIGN-BUILD-ORDER.md, "Guild dungeon size".
--
-- WRITTEN, NOT APPLIED. Nothing in this file has been run against any database.
--
-- RUN AFTER 0015, and only once 0014 is applied: guild_dungeon_submit below is 0014's body with two
-- rules added, and it calls 0014's guild_dungeon_seed_for. The guard straight below stops the file
-- there if 0014 is missing, before anything is created or replaced.
--
-- RE-APPLIABLE, like 0004-0015: the table is `if not exists`, every policy is preceded by `drop
-- policy if exists`, every function is `create or replace` except guild_dungeon_status, which is
-- dropped and recreated because its answer gains columns (a returned row type cannot change in
-- place), and the floor constraint is dropped and re-added with the same name. Running it twice
-- changes nothing.
--
-- ---------------------------------------------------------------- what changes
--
-- 1. THE LADDER. A guild's dungeon has guild_dungeon_floors_for(members) floors this cycle: five
--    for any guild under ten members, exactly what every guild had before, then one more for every
--    five members, ten at the 30-member cap. The member count is SNAPSHOTTED into
--    guild_dungeon_cycles the first time anybody in the guild asks for the cycle
--    (guild_dungeon_status) or files an attempt in it, and the ladder does not move until the
--    cycle rolls, so a floor never disappears under somebody's recorded attempt and a mass invite
--    mid-week opens nothing until next week.
--
--    What a floor IS does not depend on the ladder: floor N is a function of the seed and N alone
--    (GuildDungeonGenerator), so the server still holds no fight and needs none.
--
-- 2. THE CYCLE CAP. Three attempts a floor, as 0007 has it, and now at most
--    guild_dungeon_cycle_attempt_cap() a cycle per ACCOUNT: three times the base five, 15. A
--    member of a ten-floor guild has more floors to spend fifteen attempts on, never more than
--    fifteen. Counted per account across guilds, because the cap bounds a person's week and the
--    Waymark ledger already pays per account; changing guild mid-cycle starts the new guild's floors
--    fresh but not the account's budget. Held as a CONSTRAINT as well as a count: each new row takes
--    the next `cycle_attempt` number for its account and cycle, the number is CHECKed against the
--    cap and unique per (user, cycle), so a sixteenth row cannot exist whatever the timing.
--
-- 3. NOTHING NEW IS PAID. There is no reward table here on purpose: what a floor beyond the base
--    ladder pays is an open economy decision, and GuildDungeonRewards in the client is a marked
--    placeholder that pays nothing. 0011's Waymark pay is untouched and stays blind to depth: the
--    first attempt and the first clear of ANY floor pay once a cycle, and only floor 1's boards pay.
--
-- ---------------------------------------------------------------- the constants
--
-- Repeated in Assets/Resources/Content/guild-dungeon-scale.json, deliberately, for the reason
-- 0007 repeats the attempt cap: the client's copy tells a player before a round trip, this copy is
-- what makes it true. GuildDungeonChecks.CheckServerAgreesOnSize reads both files and fails the
-- build-check when they disagree. guild_dungeon_floor_max must stay the single-line `select <n>;`
-- form and guild_dungeon_floors_for a CASE of `when p_members >= <n> then <floors>` lines, because
-- that is what the harness parses.

do $$
begin
  if to_regprocedure('public.guild_dungeon_seed_for(uuid, integer)') is null then
    raise exception '0016 needs 0014 (guild_dungeon_seed_for) applied first - nothing in this file was applied';
  end if;
end;
$$;

-- The longest ladder any guild can have: a guild at guild_member_cap().
create or replace function public.guild_dungeon_floor_max()
returns integer
language sql
immutable
as $$ select 10; $$;

-- Floors for a guild of this many members. Below ten, the base five (guild_dungeon_floor_count,
-- 0007), so no guild has fewer floors than before.
create or replace function public.guild_dungeon_floors_for(p_members integer)
returns integer
language sql
immutable
as $$
  select case
    when p_members >= 30 then 10
    when p_members >= 25 then 9
    when p_members >= 20 then 8
    when p_members >= 15 then 7
    when p_members >= 10 then 6
    else 5
  end;
$$;

-- Attempts one account may make in one cycle, across every floor. DERIVED, never typed: the
-- per-floor cap times the base ladder, so it moves when either of 0007's constants does.
create or replace function public.guild_dungeon_cycle_attempt_cap()
returns integer
language sql
immutable
as $$ select public.guild_dungeon_attempt_cap() * public.guild_dungeon_floor_count(); $$;

-- ---------------------------------------------------------------- the snapshot
--
-- ONE ROW PER GUILD PER CYCLE: how many members the guild had when its cycle opened, and the
-- ladder that set. Written only by guild_dungeon_cycle_floors, inside guild_dungeon_status and
-- guild_dungeon_submit, which are SECURITY DEFINER; no client role can write it. Members read their
-- own guild's rows, so the boards screen can say "ten floors this week, set at 31 members" from
-- the row rather than from a guess. Disbanding the guild takes its rows.
create table if not exists public.guild_dungeon_cycles (
  guild_id     uuid not null references public.guilds (id) on delete cascade,
  cycle        integer not null check (cycle >= 0),
  member_count integer not null check (member_count >= 1),
  floor_count  smallint not null
               check (floor_count between public.guild_dungeon_floor_count() and public.guild_dungeon_floor_max()),
  created_at   timestamptz not null default now(),
  primary key (guild_id, cycle)
);

alter table public.guild_dungeon_cycles enable row level security;

revoke all on public.guild_dungeon_cycles from anon, authenticated;
grant select on public.guild_dungeon_cycles to authenticated;

drop policy if exists guild_dungeon_cycles_read_own_guild on public.guild_dungeon_cycles;
create policy guild_dungeon_cycles_read_own_guild
  on public.guild_dungeon_cycles for select
  to authenticated
  using (guild_id = public.my_guild());

-- This guild's ladder for this cycle, snapshotting it first if nobody has asked yet. `on conflict
-- do nothing` and then a read, so two members opening the cycle in the same instant both get the
-- one row the first of them wrote. Internal: granted to no client role, called only from the two
-- SECURITY DEFINER functions below.
create or replace function public.guild_dungeon_cycle_floors(p_guild uuid, p_cycle integer)
returns integer
language plpgsql
volatile
security definer
set search_path = public, pg_temp
as $$
declare
  members integer;
  floors  integer;
begin
  if p_guild is null or p_cycle is null then
    return public.guild_dungeon_floor_count();
  end if;
  select count(*) into members from public.guild_members m where m.guild_id = p_guild;
  members := greatest(1, members);
  insert into public.guild_dungeon_cycles (guild_id, cycle, member_count, floor_count)
  values (p_guild, p_cycle, members,
          least(public.guild_dungeon_floor_max(),
                greatest(public.guild_dungeon_floor_count(), public.guild_dungeon_floors_for(members))))
  on conflict (guild_id, cycle) do nothing;
  select c.floor_count into floors from public.guild_dungeon_cycles c
   where c.guild_id = p_guild and c.cycle = p_cycle;
  return floors;
end;
$$;

-- ---------------------------------------------------------------- the attempts table
--
-- 0007 bounded `floor` by guild_dungeon_floor_count(), the base five. A ten-floor guild's floor 6
-- has to fit, so the bound becomes the longest ladder. Whether THIS guild's ladder reaches a floor
-- is guild_dungeon_submit's rule below, because a constraint cannot read another table.
--
-- `cycle_attempt` is the account's attempt number within the cycle, 1 to the cycle cap, assigned
-- by guild_dungeon_submit. Null on every row written before this file, which is why the unique key
-- tolerates it: Postgres treats nulls as distinct. A new row's number counts the old rows too, so
-- numbering in a cycle already under way starts after them.
alter table public.guild_dungeon_attempts drop constraint if exists guild_dungeon_attempts_floor_check;
alter table public.guild_dungeon_attempts
  add constraint guild_dungeon_attempts_floor_check
  check (floor between 1 and public.guild_dungeon_floor_max());

alter table public.guild_dungeon_attempts
  add column if not exists cycle_attempt smallint;

alter table public.guild_dungeon_attempts drop constraint if exists guild_dungeon_attempts_cycle_attempt_range;
alter table public.guild_dungeon_attempts
  add constraint guild_dungeon_attempts_cycle_attempt_range
  check (cycle_attempt is null or cycle_attempt between 1 and public.guild_dungeon_cycle_attempt_cap());

create unique index if not exists guild_dungeon_attempts_cycle_once
  on public.guild_dungeon_attempts (user_id, cycle, cycle_attempt);

-- ---------------------------------------------------------------- where the dungeon stands
--
-- 0007's answer, with floor_count now THIS GUILD'S ladder for the cycle (opening the cycle if
-- nobody has), and three columns added: the cycle cap, the caller's attempts this cycle by the
-- server's count (every guild, which the caller's own reads cannot see after a guild change), and
-- the member count the ladder was set from. Volatile, because it may write the snapshot; PostgREST
-- runs a STABLE function read-only, and the insert would fail there.
drop function if exists public.guild_dungeon_status();
create or replace function public.guild_dungeon_status()
returns table (
  guild_id          uuid,
  cycle             integer,
  starts_at         timestamptz,
  ends_at           timestamptz,
  attempt_cap       integer,
  floor_count       integer,
  cycle_attempt_cap integer,
  attempts_used     integer,
  ladder_members    integer
)
language plpgsql
volatile
security definer
set search_path = public, pg_temp
as $$
declare
  uid  uuid := auth.uid();
  mine uuid := public.my_guild();
  now_cycle integer := public.guild_dungeon_cycle_at(now());
  floors integer;
  used   integer;
  members integer;
begin
  if uid is null then
    raise exception 'guild_dungeon_status requires a signed-in user' using errcode = 'PT401';
  end if;
  if mine is null then
    raise exception 'you are not in a guild' using errcode = 'PT403';
  end if;

  floors := public.guild_dungeon_cycle_floors(mine, now_cycle);
  select c.member_count into members from public.guild_dungeon_cycles c
   where c.guild_id = mine and c.cycle = now_cycle;
  select count(*) into used from public.guild_dungeon_attempts a
   where a.user_id = uid and a.cycle = now_cycle;

  return query
    select mine,
           now_cycle,
           public.guild_dungeon_epoch() + now_cycle * public.guild_dungeon_cycle_length(),
           public.guild_dungeon_epoch() + (now_cycle + 1) * public.guild_dungeon_cycle_length(),
           public.guild_dungeon_attempt_cap(),
           floors,
           public.guild_dungeon_cycle_attempt_cap(),
           used,
           members;
end;
$$;

-- ---------------------------------------------------------------- recording an attempt, with the ladder and the cycle cap
--
-- 0014's guild_dungeon_submit, every rule unchanged and in the same order, with two changes:
--
--   - The floor range check reads the LONGEST ladder (a floor above it exists for nobody, 400 as
--     before), and after the seed check, THIS GUILD'S ladder: a floor past it is refused with 400
--     and spends nothing. A guild under ten members therefore still answers "floor 6 does not
--     exist" exactly as the live rules test expects.
--   - After the per-floor cap, the CYCLE cap per account (409), and the new row takes the next
--     cycle_attempt number. The member row is still locked FOR UPDATE first, and an account is in
--     one guild at a time, so two submissions from one account queue and the second counts the
--     first's row.
--
-- The signature is 0008's exactly, so `create or replace` swaps the body in place and 0011's
-- guild_dungeon_attempt, which calls this, takes both rules with no change of its own.
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
  uid        uuid := auth.uid();
  mine       uuid;
  now_cycle  integer := public.guild_dungeon_cycle_at(now());
  used       integer;
  cycle_used integer;
  ladder     integer;
  result     public.guild_dungeon_attempts;
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
  if p_floor is null or p_floor < 1 or p_floor > public.guild_dungeon_floor_max() then
    raise exception 'a floor is 1 to %', public.guild_dungeon_floor_max() using errcode = 'PT400';
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

  -- The ladder (0016). Snapshotted on the cycle's first touch and fixed until it rolls.
  ladder := public.guild_dungeon_cycle_floors(mine, now_cycle);
  if p_floor > ladder then
    raise exception 'a floor is 1 to %: your guild''s dungeon is % floors this cycle', ladder, ladder
      using errcode = 'PT400';
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

  -- The cycle cap (0016). Per account, every guild.
  select count(*) into cycle_used
    from public.guild_dungeon_attempts a
   where a.user_id = uid and a.cycle = now_cycle;
  if cycle_used >= public.guild_dungeon_cycle_attempt_cap() then
    raise exception 'no attempts left this cycle (% of %)',
      cycle_used, public.guild_dungeon_cycle_attempt_cap() using errcode = 'PT409';
  end if;

  insert into public.guild_dungeon_attempts
    (guild_id, user_id, cycle, floor, attempt, seed, team, victory, turns, power, avg_stars,
     fallen, content_version, cycle_attempt)
  values
    (mine, uid, now_cycle, p_floor, used + 1, p_seed, p_team, p_victory, p_turns, p_power,
     round(p_avg_stars, 2), p_fallen, p_content_version, cycle_used + 1)
  returning * into result;

  return result;
end;
$$;

-- ---------------------------------------------------------------- privileges
--
-- The three constants and the ladder function are callable by a signed-in client, like 0007's;
-- they hold no data. The snapshot writer is not: it is called only from inside the two functions
-- below it, which run as their owner.

revoke all on function public.guild_dungeon_floor_max() from public, anon;
revoke all on function public.guild_dungeon_floors_for(integer) from public, anon;
revoke all on function public.guild_dungeon_cycle_attempt_cap() from public, anon;
revoke all on function public.guild_dungeon_cycle_floors(uuid, integer) from public, anon;
revoke all on function public.guild_dungeon_status() from public, anon;
revoke all on function public.guild_dungeon_submit(integer, integer, integer, jsonb, boolean, integer,
  integer, numeric, integer, text) from public, anon;

revoke execute on function public.guild_dungeon_cycle_floors(uuid, integer) from authenticated;

grant execute on function public.guild_dungeon_floor_max() to authenticated;
grant execute on function public.guild_dungeon_floors_for(integer) to authenticated;
grant execute on function public.guild_dungeon_cycle_attempt_cap() to authenticated;
grant execute on function public.guild_dungeon_status() to authenticated;
grant execute on function public.guild_dungeon_submit(integer, integer, integer, jsonb, boolean, integer,
  integer, numeric, integer, text) to authenticated;
