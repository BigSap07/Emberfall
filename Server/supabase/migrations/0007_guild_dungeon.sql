-- 0007_guild_dungeon.sql - the guild dungeon: one seeded fight per guild per cycle, and the
-- record of every attempt anybody made at it.
--
-- Run after 0006. RE-APPLIABLE, like 0004-0006: every policy is preceded by `drop policy if
-- exists`, every table and index is `if not exists`, every function is `create or replace`.
--
-- A PROTOTYPE, and the shape says so. There is no reward table, no currency and no guild level
-- here, because each of those prices against an economy decision that has not been made, and the
-- loop - same fight, own roster, three boards - is testable without them.
--
-- ---------------------------------------------------------------- what the server holds, and what it does not
--
-- THE SERVER HOLDS THE CLOCK AND THE CAP. The cycle number is computed here from now(), and a
-- record for any other cycle is refused, so a client cannot bank attempts against a cycle it has
-- not reached or reopen one that has ended. The attempt cap is a CHECK constraint on the table
-- plus a unique key, so a fourth attempt cannot be stored whatever the timing of the requests.
--
-- THE SERVER DOES NOT HOLD THE FIGHT. There is no combat on the server and no seed derivation
-- either: the client derives the dungeon seed from the guild id and the cycle
-- (GuildDungeon.SeedFor), fights it, and submits the result. Every field is trusted. What makes
-- that acceptable for a prototype is that the record carries everything needed to re-run it -
-- seed, floor and the whole team by value - so an attempt can be re-verified later by anything
-- that holds the engine, without a schema change. No anti-cheat is claimed here.
--
-- ---------------------------------------------------------------- the constants
--
-- Repeated in balance.json as guildDungeonAttemptsPerFloor and guildDungeonFloors, deliberately,
-- the same duplication guild_member_cap() lives under: the client's copy tells a player before a
-- round trip, this copy is what makes it true. Tools/DevHarness/GuildDungeonChecks.cs reads both
-- files and fails the build-check when they disagree, because neither can read the other at
-- runtime.
--
-- The functions' bodies must stay the single-line `select <n>;` form. That is what the harness
-- parses.

create or replace function public.guild_dungeon_attempt_cap()
returns integer
language sql
immutable
as $$ select 3; $$;

create or replace function public.guild_dungeon_floor_count()
returns integer
language sql
immutable
as $$ select 5; $$;

-- One cycle is a week, counted from a Monday at midnight UTC. Server-only: the client never
-- computes a cycle, it asks guild_dungeon_status() for one, so this can change without a build.
create or replace function public.guild_dungeon_cycle_length()
returns interval
language sql
immutable
as $$ select interval '7 days'; $$;

create or replace function public.guild_dungeon_epoch()
returns timestamptz
language sql
immutable
as $$ select timestamptz '2026-01-05 00:00:00+00'; $$;

-- The cycle a moment falls in. floor() rather than a cast, because a cast rounds and a cycle that
-- began at half past the boundary would be credited to the next one.
create or replace function public.guild_dungeon_cycle_at(p_at timestamptz)
returns integer
language sql
immutable
as $$
  select floor(extract(epoch from (p_at - public.guild_dungeon_epoch()))
               / extract(epoch from public.guild_dungeon_cycle_length()))::integer;
$$;

-- ---------------------------------------------------------------- the record

-- ONE ROW IS ONE ATTEMPT, AND ONE ATTEMPT IS A REPLAY. seed + floor + team is enough for the
-- engine to reproduce the fight event for event, because combat is a pure function. The three
-- scores are stored beside it so a board is a SELECT rather than thirty battles.
--
-- `seed` is the DUNGEON seed, not the fight seed: the fight seed is derived from it per floor, so
-- one number describes the whole cycle and every row of a guild's cycle should carry the same one.
--
-- `team` is the TeamSnapshotCodec units array - the same shape team_snapshots.units holds - so the
-- line that fought is stored by value: heroes, levels, stars, relics, talents and armour. Up to
-- five, which is balance.json's maxTeamSize; a line may be smaller, and on the power board a
-- smaller line is the point.
--
-- `attempt` is assigned by guild_dungeon_record, 1 to the cap. THE CAP IS A CONSTRAINT: the CHECK
-- bounds the number and the unique key forbids reusing one, so four rows for one member on one
-- floor in one cycle cannot exist, whatever the timing - the same "a rule that can be a constraint
-- is not left to a function" that makes one guild per player a primary key in 0004.
--
-- Keyed by guild as well as by member, so a player who changes guild mid-cycle starts the new
-- guild's dungeon with fresh attempts. It is a different seed and a different fight, and with no
-- rewards there is nothing to farm by hopping.
--
-- A member who leaves keeps their rows, and the guild keeps reading them - an attempt is a record
-- of something that happened. Disbanding the guild takes every row with it.
create table if not exists public.guild_dungeon_attempts (
  id         bigint generated always as identity primary key,
  guild_id   uuid not null references public.guilds (id) on delete cascade,
  user_id    uuid not null references public.profiles (user_id) on delete cascade,
  cycle      integer not null check (cycle >= 0),
  floor      smallint not null
             check (floor between 1 and public.guild_dungeon_floor_count()),
  attempt    smallint not null
             check (attempt between 1 and public.guild_dungeon_attempt_cap()),
  seed       integer not null check (seed >= 0),
  team       jsonb not null
             check (jsonb_typeof(team) = 'array' and jsonb_array_length(team) between 1 and 5),
  victory    boolean not null,
  turns      integer not null check (turns >= 0),
  power      integer not null check (power >= 0),
  avg_stars  numeric(4,2) not null check (avg_stars between 1 and 10),
  created_at timestamptz not null default now(),
  constraint guild_dungeon_attempts_once unique (guild_id, user_id, cycle, floor, attempt)
);

-- The board read: one guild, one cycle, every floor.
create index if not exists guild_dungeon_attempts_board_idx
  on public.guild_dungeon_attempts (guild_id, cycle, floor);

-- ---------------------------------------------------------------- privileges and policy
--
-- Two locks, as everywhere in these migrations. No INSERT, UPDATE or DELETE for any client role:
-- a row gets in only through guild_dungeon_record, which holds the clock and the cap, and once in
-- it is a record - nobody edits a score after the fact, including its owner.
alter table public.guild_dungeon_attempts enable row level security;

revoke all on public.guild_dungeon_attempts from anon, authenticated;
grant select on public.guild_dungeon_attempts to authenticated;

-- Members read their own guild's attempts, every member's, every cycle's. The boards are built
-- from exactly this read. A non-member gets zero rows, the same filtering shape as the roster.
drop policy if exists guild_dungeon_attempts_read_own_guild on public.guild_dungeon_attempts;
create policy guild_dungeon_attempts_read_own_guild
  on public.guild_dungeon_attempts for select
  to authenticated
  using (guild_id = public.my_guild());

-- ---------------------------------------------------------------- where the dungeon stands

-- What the client needs before it can build a single floor: which guild, which cycle, when it
-- ends, and the two constants as the server holds them. The client derives the seed from
-- guild_id and cycle; the server never sends one, so there is nothing here to spoof or cache.
--
-- Attempts already spent are not in this answer. They are rows the caller can already read, and
-- a second copy of a count is a second thing to keep in step.
create or replace function public.guild_dungeon_status()
returns table (
  guild_id    uuid,
  cycle       integer,
  starts_at   timestamptz,
  ends_at     timestamptz,
  attempt_cap integer,
  floor_count integer
)
language plpgsql
stable
security definer
set search_path = public, pg_temp
as $$
declare
  uid  uuid := auth.uid();
  mine uuid := public.my_guild();
  now_cycle integer := public.guild_dungeon_cycle_at(now());
begin
  if uid is null then
    raise exception 'guild_dungeon_status requires a signed-in user' using errcode = 'PT401';
  end if;
  if mine is null then
    raise exception 'you are not in a guild' using errcode = 'PT403';
  end if;

  return query
    select mine,
           now_cycle,
           public.guild_dungeon_epoch() + now_cycle * public.guild_dungeon_cycle_length(),
           public.guild_dungeon_epoch() + (now_cycle + 1) * public.guild_dungeon_cycle_length(),
           public.guild_dungeon_attempt_cap(),
           public.guild_dungeon_floor_count();
end;
$$;

-- ---------------------------------------------------------------- recording an attempt

-- THE ONLY WAY A ROW GETS IN. The caller's guild and user come from auth.uid(), never from an
-- argument, so there is no call by which one member files an attempt as another.
--
-- The caller's own membership row is taken FOR UPDATE first, for guild_post_message's reason:
-- two submissions from one account in the same instant would otherwise both count two attempts
-- used and both write attempt three. Locking the row that identifies the member makes them queue,
-- and the count the second one runs then sees the first one's row. The constraint on the table is
-- the guarantee; the lock is what turns a would-be unique violation into the sentence below.
--
-- A cycle other than the current one is refused with 409 rather than silently re-labelled. A
-- client that fought last week's dungeon a second before the boundary and submitted a second
-- after it fought a seed that is no longer this week's; filing that fight under this week would
-- store a row that does not replay against the cycle it claims.
create or replace function public.guild_dungeon_record(
  p_cycle     integer,
  p_floor     integer,
  p_seed      integer,
  p_team      jsonb,
  p_victory   boolean,
  p_turns     integer,
  p_power     integer,
  p_avg_stars numeric
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
    raise exception 'guild_dungeon_record requires a signed-in user' using errcode = 'PT401';
  end if;

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

  select count(*) into used
    from public.guild_dungeon_attempts a
   where a.guild_id = mine and a.user_id = uid and a.cycle = now_cycle and a.floor = p_floor;
  if used >= public.guild_dungeon_attempt_cap() then
    raise exception 'no attempts left on floor % this cycle (% of %)',
      p_floor, used, public.guild_dungeon_attempt_cap() using errcode = 'PT409';
  end if;

  insert into public.guild_dungeon_attempts
    (guild_id, user_id, cycle, floor, attempt, seed, team, victory, turns, power, avg_stars)
  values
    (mine, uid, now_cycle, p_floor, used + 1, p_seed, p_team, p_victory, p_turns, p_power,
     round(p_avg_stars, 2))
  returning * into result;

  return result;
end;
$$;

-- ---------------------------------------------------------------- privileges

revoke all on function public.guild_dungeon_attempt_cap() from public, anon;
revoke all on function public.guild_dungeon_floor_count() from public, anon;
revoke all on function public.guild_dungeon_cycle_length() from public, anon;
revoke all on function public.guild_dungeon_epoch() from public, anon;
revoke all on function public.guild_dungeon_cycle_at(timestamptz) from public, anon;
revoke all on function public.guild_dungeon_status() from public, anon;
revoke all on function public.guild_dungeon_record(integer, integer, integer, jsonb, boolean, integer, integer, numeric)
  from public, anon;

grant execute on function public.guild_dungeon_attempt_cap() to authenticated;
grant execute on function public.guild_dungeon_floor_count() to authenticated;
grant execute on function public.guild_dungeon_cycle_length() to authenticated;
grant execute on function public.guild_dungeon_epoch() to authenticated;
grant execute on function public.guild_dungeon_cycle_at(timestamptz) to authenticated;
grant execute on function public.guild_dungeon_status() to authenticated;
grant execute on function public.guild_dungeon_record(integer, integer, integer, jsonb, boolean, integer, integer, numeric)
  to authenticated;
