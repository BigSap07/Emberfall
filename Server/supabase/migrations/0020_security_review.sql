-- 0020_security_review.sql - the fixes from the security review of 0001 to 0015.
-- Docs/SECURITY-REVIEW-SUPABASE.md is the review; every finding it fixes in SQL is fixed here, and the
-- ones it only recommends are listed there with the reason they are not a migration.
--
-- WRITTEN, NOT APPLIED. Run after 0015. It touches only objects 0001 to 0015 created, so it does not
-- care whether 0016 to 0019 (account deletion, chat safety, server time, receipts) are applied;
-- 0021_security_review_launch.sql is the half that needs them.
--
-- RE-APPLIABLE, like 0004 onward: tables are `if not exists`, the limits row is inserted `on conflict
-- do nothing` (so re-applying never resets a limit somebody raised), functions are `create or
-- replace`, the trigger is dropped before it is created, and each constraint is added only when it is
-- absent. Running it twice changes nothing.
--
-- ---------------------------------------------------------------- what is fixed here
--
--   R1  guild_tech_xp counted one account's cycle pay once for EVERY guild that account filed an
--       attempt for in that cycle. A member who changes guild mid-cycle gets fresh attempts (0007), so
--       one account, or a handful of throwaway anonymous accounts, could carry the same Waymarks into
--       guild after guild and raise each one's level, which is a high-water mark (0012) and never
--       comes back down. Now each (account, cycle) feeds exactly one guild: the guild of that
--       account's FIRST attempt of the cycle.
--   R2  team_snapshots had no limit on size or shape. A client could store megabytes in `units` (read
--       back by every friend and guildmate who opens the row), a label of any length with newlines in
--       it, and a negative power score. Constraints now bound all three.
--   R3  saves.device_id was outside 0013's size cap: any length of text rode along with every push.
--   R4  Friend requests had no rate limit. profiles are readable by every signed-in user (0001), so a
--       script can send a request to every player in the project, delete it and send it again. A
--       per-account tally now refuses more than a set number of requests in a window, counting sends,
--       not rows, so deleting and re-sending does not reset it.
--   R5  The three trigger functions of 0001, 0003 and 0004 were never revoked. Postgres refuses to run
--       a trigger function outside a trigger, so nothing was reachable, but CloudChecks exempts trigger
--       functions only because of that refusal; revoking them makes the rule "nothing a client did not
--       get on purpose" true without the exemption. A trigger fires whatever its function's EXECUTE
--       privileges say: Postgres checks EXECUTE when CREATE TRIGGER runs, not when the trigger fires.

-- ---------------------------------------------------------------- R1: one guild per account per cycle
--
-- 0012's guild_tech_xp, same signature, same grants (none to clients), with the join narrowed: the
-- attempt rows are reduced to ONE row per (user_id, cycle), the one with the lowest id, before they
-- meet the ledger. The ledger is already one row per (user_id, cycle, kind), so after this every Waymark
-- paid is counted toward exactly one guild.
--
-- WHY THE FIRST ATTEMPT: the 'attempt' pay is written by that very call (0011), so the guild that
-- received the attempt is the guild that caused the pay. A 'clear' or a 'boards' payment won later in
-- the cycle for another guild is credited to the first one too; that is the price of one owner per
-- cycle, and it only ever moves XP between guilds the same account fought for, never creates it.
--
-- Levels already stored are a high-water mark and are NOT lowered by this (0012's rule). A guild that
-- was raised by double counting keeps its level; it simply earns at the honest rate from here on.
create or replace function public.guild_tech_xp(p_guild uuid)
returns bigint
language sql
stable
security definer
set search_path = public, pg_temp
as $$
  select coalesce(sum(l.amount), 0)::bigint
    from public.guild_path_ledger l
    join (select distinct on (a.user_id, a.cycle) a.user_id, a.cycle, a.guild_id
            from public.guild_dungeon_attempts a
           order by a.user_id, a.cycle, a.id) owner_guild
      on owner_guild.user_id = l.user_id and owner_guild.cycle = l.cycle
   where owner_guild.guild_id = p_guild;
$$;

revoke all on function public.guild_tech_xp(uuid) from public, anon;
revoke execute on function public.guild_tech_xp(uuid) from authenticated;

-- ---------------------------------------------------------------- R2 and R3: the column rules
--
-- In 0013's shape: added NOT VALID, which enforces each rule on every write from this moment without
-- reading the rows already there, then VALIDATED only if no stored row breaks it, else a NOTICE says
-- how many rows do. The bounds are generous against what the game writes:
--
--   * label: TeamSnapshotBuilder writes the player's name, "Wayfarer" when there is none. 64 characters,
--     one line, which is more than any name field in the game takes.
--   * units: TeamSnapshotCodec.WriteUnits writes at most balance.json's maxTeamSize (5) objects, each a
--     hero id, level, stars, a relic id, a talent and the armor pieces by value: a few kilobytes for a
--     whole line. 32 KiB is roughly ten times that. An empty array stays allowed (the column default,
--     and what the rules test files).
--   * power_score: the client's own reading of the line, never negative.
--   * device_id: the client sends a GUID. 128 characters.
do $$
declare
  broken bigint;
begin
  if not exists (select 1 from pg_constraint
                  where conname = 'team_snapshots_bounded' and conrelid = 'public.team_snapshots'::regclass) then
    alter table public.team_snapshots
      add constraint team_snapshots_bounded
      check (char_length(label) <= 64
             and label !~ '[[:cntrl:]]'
             and power_score >= 0
             and jsonb_typeof(units) = 'array'
             and jsonb_array_length(units) <= 5
             and octet_length(units::text) <= 32768) not valid;
  end if;
  if exists (select 1 from pg_constraint
              where conname = 'team_snapshots_bounded' and conrelid = 'public.team_snapshots'::regclass
                and not convalidated) then
    select count(*) into broken from public.team_snapshots t
     where not (char_length(t.label) <= 64 and t.label !~ '[[:cntrl:]]' and t.power_score >= 0
                and jsonb_typeof(t.units) = 'array' and jsonb_array_length(t.units) <= 5
                and octet_length(t.units::text) <= 32768);
    if broken > 0 then
      raise notice '0020: team_snapshots_bounded: enforced on new writes, NOT validated - % stored snapshot(s) break it', broken;
    else
      alter table public.team_snapshots validate constraint team_snapshots_bounded;
    end if;
  end if;

  if not exists (select 1 from pg_constraint
                  where conname = 'saves_device_id_bounded' and conrelid = 'public.saves'::regclass) then
    alter table public.saves
      add constraint saves_device_id_bounded check (char_length(device_id) <= 128) not valid;
  end if;
  if exists (select 1 from pg_constraint
              where conname = 'saves_device_id_bounded' and conrelid = 'public.saves'::regclass
                and not convalidated) then
    select count(*) into broken from public.saves s where char_length(s.device_id) > 128;
    if broken > 0 then
      raise notice '0020: saves_device_id_bounded: enforced on new writes, NOT validated - % stored save(s) break it', broken;
    else
      alter table public.saves validate constraint saves_device_id_bounded;
    end if;
  end if;
end;
$$;

-- ---------------------------------------------------------------- R4: friend requests, at a bounded rate
--
-- One limits row, in the shape of save_limits (0013) and guild_rate_limits (0014): raising a number is
-- an UPDATE, not a function rewrite. No client role can read or write it.
--
--   request_limit           20 requests sent per window. A player adding friends from a Discord thread
--                           sends a handful; a script sends thousands.
--   request_window_seconds  3600, one hour.
--
--     update public.social_rate_limits set request_limit = 40, updated_at = now();
create table if not exists public.social_rate_limits (
  id                     boolean primary key default true check (id),
  request_limit          integer not null check (request_limit >= 1),
  request_window_seconds integer not null check (request_window_seconds between 60 and 86400),
  updated_at             timestamptz not null default now()
);

alter table public.social_rate_limits enable row level security;
revoke all on public.social_rate_limits from anon, authenticated;

insert into public.social_rate_limits (id, request_limit, request_window_seconds)
values (true, 20, 3600)
on conflict (id) do nothing;

-- One row per account that has ever sent a request: a fixed window, guild_code_lookups' shape (0014).
-- A SEND is counted, not a row, so withdrawing a request does not give the send back. Keyed to profiles,
-- so deleting the account takes it.
create table if not exists public.friend_request_tally (
  user_id      uuid primary key references public.profiles (user_id) on delete cascade,
  window_start timestamptz not null default now(),
  sent         integer not null default 0 check (sent >= 0)
);

alter table public.friend_request_tally enable row level security;
revoke all on public.friend_request_tally from anon, authenticated;

-- BEFORE INSERT on friendships. The client inserts a request straight through PostgREST (0003 grants
-- INSERT and the policy pins requester = auth.uid() and status = 'pending'), so the limit has to sit on
-- the table rather than in a function the client might not call. SECURITY DEFINER because the tally
-- has no client grant. The tally row is locked FOR UPDATE, so two sends in one instant queue and count
-- exactly. PT429, so PostgREST answers 429 Too Many Requests.
--
-- It reads the row's requester, not auth.uid(): the insert policy already guarantees they are equal for
-- a client, and a row written by the owner in the SQL editor (no auth.uid()) is still counted against
-- the account it names.
create or replace function public.friendships_throttle_request()
returns trigger
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  lim   integer;
  win   interval;
  tally public.friend_request_tally;
begin
  select l.request_limit, make_interval(secs => l.request_window_seconds) into lim, win
    from public.social_rate_limits l where l.id;
  if lim is null or win is null then
    raise exception 'friend requests have no social_rate_limits row to check against - see 0020'
      using errcode = 'PT500';
  end if;

  insert into public.friend_request_tally as t (user_id) values (new.requester)
  on conflict (user_id) do nothing;
  select * into tally from public.friend_request_tally t where t.user_id = new.requester for update;

  if now() - tally.window_start >= win then
    update public.friend_request_tally t set window_start = now(), sent = 1 where t.user_id = new.requester;
    return new;
  end if;
  if tally.sent >= lim then
    raise exception 'too many friend requests - try again in % seconds',
      greatest(1, ceil(extract(epoch from (tally.window_start + win - now()))))::integer
      using errcode = 'PT429';
  end if;
  update public.friend_request_tally t set sent = t.sent + 1 where t.user_id = new.requester;
  return new;
end;
$$;

drop trigger if exists friendships_request_throttle on public.friendships;
create trigger friendships_request_throttle
  before insert on public.friendships
  for each row execute function public.friendships_throttle_request();

-- ---------------------------------------------------------------- R5: the trigger functions, revoked

revoke all on function public.handle_new_user() from public, anon, authenticated;
revoke all on function public.stamp_team_snapshot() from public, anon, authenticated;
revoke all on function public.guild_succeed_leader() from public, anon, authenticated;
revoke all on function public.friendships_throttle_request() from public, anon, authenticated;

-- ---------------------------------------------------------------- what was applied, said once

do $$
declare
  who text;
begin
  foreach who in array array['anon', 'authenticated'] loop
    if has_function_privilege(who, 'public.guild_tech_xp(uuid)', 'execute') then
      raise exception '0020: % can execute guild_tech_xp', who;
    end if;
    if has_table_privilege(who, 'public.social_rate_limits', 'select')
       or has_table_privilege(who, 'public.friend_request_tally', 'select')
       or has_table_privilege(who, 'public.friend_request_tally', 'insert')
       or has_table_privilege(who, 'public.friend_request_tally', 'update') then
      raise exception '0020: % can read or write a social rate limit table', who;
    end if;
    if has_function_privilege(who, 'public.friendships_throttle_request()', 'execute')
       or has_function_privilege(who, 'public.handle_new_user()', 'execute')
       or has_function_privilege(who, 'public.stamp_team_snapshot()', 'execute')
       or has_function_privilege(who, 'public.guild_succeed_leader()', 'execute') then
      raise exception '0020: % can execute a trigger function', who;
    end if;
  end loop;
  raise notice '0020: guild XP counts each account''s cycle once, snapshots and device ids are bounded, friend requests are throttled, trigger functions are revoked';
end;
$$;

notify pgrst, 'reload schema';

-- ---------------------------------------------------------------- after applying, by hand
--
-- Read-only, and nothing in this file runs them.
--
--   -- guilds whose stored level is above what their XP now earns (raised by the double count; kept)
--   select l.guild_id, l.level, public.guild_tech_xp(l.guild_id) as xp
--     from public.guild_tech_levels l order by l.level desc limit 20;
--   -- accounts that filed attempts for more than one guild in one cycle (the R1 pattern)
--   select user_id, cycle, count(distinct guild_id) as guilds
--     from public.guild_dungeon_attempts group by user_id, cycle having count(distinct guild_id) > 1
--    order by guilds desc limit 20;
--   select conname, convalidated from pg_constraint
--    where conname in ('team_snapshots_bounded', 'saves_device_id_bounded');
--   select user_id, sent, window_start from public.friend_request_tally order by sent desc limit 20;
