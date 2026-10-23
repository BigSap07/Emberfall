-- 0016_leaderboards.sql - three leaderboards: the Deep Recital's highest floor (with a weekly board
-- that resets every Monday at the reward-day turnover), the Proving's highest standing, and campaign
-- progress. Docs/DESIGN-LEADERBOARDS.md.
--
-- WRITTEN, NOT APPLIED. Nothing in the game calls these functions yet. The owner applies this file in
-- the SQL editor, after 0013 (it reads public.saves as 0013 shapes it) and before any build that shows
-- a board. Tools/supabase-leaderboard-rules-test.py proves the rules once it is applied.
--
-- RUN AFTER 0013. It reads public.saves (0002, 0013) and public.profiles (0001) and touches neither: no
-- column, constraint, policy or grant on them changes. 0014 and 0015 are not read.
--
-- RE-APPLIABLE, like 0004-0015: tables and indexes are `if not exists`, the limits row is inserted
-- `on conflict do nothing` (re-applying never resets a limit somebody tuned), every function is
-- `create or replace`, and the grants are revoked and re-granted to the same end state. There are no
-- policies to drop: no table here has one, on purpose (below).
--
-- ---------------------------------------------------------------- the shape of the decision
--
-- THE CLIENT NEVER SENDS A SCORE. leaderboard_submit() takes no argument. It reads the caller's own
-- row in public.saves, the document push_save already stored and checked (0013: a non-empty object,
-- a version this server accepts, under the size cap), and lifts the three numbers out of it:
--
--   recital   recitalHighestFloor
--   proving   provingHighestStanding
--   campaign  the number of distinct stage ids in clearedStages
--
-- So a board can never say more than the cloud save says. A player who wants to appear higher has to
-- push a save that claims it, and the checks below are what a save claiming it has to get past. This
-- is anti-cheat BASICS, not a server-side game: the save is still written by the client, and a save
-- forged consistently and slowly enough passes. What it stops is the cheap attacks: a hand-written
-- number, a value the save itself contradicts, a jump no human could have played, and a submission
-- flood. Docs/DESIGN-LEADERBOARDS.md lists what it does not stop.
--
-- EVERY TIME IS THE SERVER'S. now() inside the function stamps every row, the week a Recital score
-- belongs to is computed from now(), and the rate checks measure the time since the server last
-- accepted a value (or since the server created the account's profile). No argument carries a time,
-- and the device clock never reaches this file.
--
-- NO TABLE IS READABLE OR WRITABLE BY A CLIENT. Each one has RLS on, no policy and no grant. A board is
-- read through leaderboard_top(), which returns a rank, a display name, a value and whether the row is
-- the caller's own: never a user id, never another player's save revision or refusal history.
--
-- ---------------------------------------------------------------- the checks, per board
--
-- Each board is judged on its own, so a refused Proving value does not hold back an honest campaign
-- value. A board ends in one of three states, returned to the caller and, for a refusal, logged:
--
--   raised    the value is higher than the one stored, and passed every check: stored, stamped now()
--   kept      the value is not higher than the one stored (an older save, a restored device): the
--             stored value stays, nothing is logged. Boards only ever rise.
--   refused   the value failed a check: the stored value stays, a row goes to leaderboard_refusals
--
-- The checks, in the order they run:
--
--   1. the save is at least min_save_version (20: clearedStages holds stage ids from 20 on);
--   2. each number is a whole number, 0 or more, with at most nine digits;
--   3. CONSISTENT WITH ITSELF:
--        recital   recitalCurrentFloor = recitalHighestFloor + 1 (PlayerProfile.NormaliseRecital);
--                  a floor above 0 needs the Recital's unlock stage in clearedStages;
--        proving   provingStanding <= provingHighestStanding, and provingHighestStanding <=
--                  provingAttempts x proving_standing_per_win (a standing is only ever won);
--                  a standing above 0 needs the Proving's unlock stage in clearedStages;
--        campaign  every element of clearedStages is a string matching campaign_stage_pattern, none
--                  repeats, and there are at most campaign_stage_count of them;
--   4. UNDER THE CEILING: recital_floor_cap and proving_standing_cap (generous; a sanity bound);
--   5. NOT FASTER THAN PLAY: a rise is allowed at most  burst + per_day x days  since the server last
--      accepted a rise on that board, or since the profile was created if it never has. A refused rise
--      is not stored, so the allowance keeps growing, and an honest fast player who was refused is
--      accepted on a later submission once the time has caught up.
--
-- The rates are first-pass numbers read off the content as it ships (balance.json):
--   recital  5 Echoes banked, one back every 4 hours (6 a day), plus Echoes from the Archive. 20 floors
--            a day and a burst of 10 is above that with room.
--   proving  5 Brands banked, one back every 3 hours (8 a day), plus Brands from the Armory, 3 standing
--            a win. 60 standing a day and a burst of 20 is above that with room.
--   campaign the reference walk clears chapter 1 (12 stages) in about 9 days; a player who plays all day
--            is much faster. 24 stages a day and a burst of 12 lets a heavy first day through.
-- Raise any of them with one UPDATE, as 0013's limits are raised:
--
--     update public.leaderboard_limits set recital_floors_per_day = 30, updated_at = now();
--
-- ---------------------------------------------------------------- the weekly Recital board
--
-- The Recital itself is an endless ladder and nothing in the game resets it (a weekly reset of the mode
-- would be a game change, not a server one). The WEEKLY BOARD resets: a week runs from Monday at
-- week_reset_hour_utc (4, balance.json dailyResetHourUtc, so the board turns with the daily orders and
-- DailyLoop.WeekOf's week) to the next Monday at that hour, server time. A player appears on a week's
-- board once they submit during it. Two readings are kept per player per week:
--
--   best_floor    the highest floor they held at their latest submission this week
--   start_floor   the highest floor the server had for them before the week's first submission (their
--                 current floor if they never submitted before), so best_floor - start_floor is the
--                 floors climbed this week
--
-- leaderboard_top('recital_week') ranks by best_floor and leaderboard_top('recital_week_climb') by the
-- floors climbed. Which one the game shows is the owner's call (Docs/DESIGN-LEADERBOARDS.md). Old weeks
-- are pruned by leaderboard_submit once they are more than weeks_kept weeks old.
--
-- ---------------------------------------------------------------- the limits the server holds

create table if not exists public.leaderboard_limits (
  id                       boolean primary key default true check (id),
  min_save_version         integer not null check (min_save_version >= 1),
  campaign_stage_count     integer not null check (campaign_stage_count >= 1),
  campaign_stage_pattern   text    not null,
  proving_unlock_stage     text    not null,
  recital_unlock_stage     text    not null,
  proving_standing_per_win integer not null check (proving_standing_per_win >= 1),
  recital_floor_cap        integer not null check (recital_floor_cap >= 1),
  proving_standing_cap     integer not null check (proving_standing_cap >= 1),
  recital_floors_per_day   numeric not null check (recital_floors_per_day >= 0),
  recital_floors_burst     integer not null check (recital_floors_burst >= 0),
  proving_standing_per_day numeric not null check (proving_standing_per_day >= 0),
  proving_standing_burst   integer not null check (proving_standing_burst >= 0),
  campaign_stages_per_day  numeric not null check (campaign_stages_per_day >= 0),
  campaign_stages_burst    integer not null check (campaign_stages_burst >= 0),
  submit_interval_seconds  integer not null check (submit_interval_seconds between 0 and 86400),
  week_reset_hour_utc      integer not null check (week_reset_hour_utc between 0 and 23),
  weeks_kept               integer not null check (weeks_kept between 1 and 520),
  updated_at               timestamptz not null default now()
);

alter table public.leaderboard_limits enable row level security;
revoke all on public.leaderboard_limits from anon, authenticated;

insert into public.leaderboard_limits (
  id, min_save_version, campaign_stage_count, campaign_stage_pattern, proving_unlock_stage,
  recital_unlock_stage, proving_standing_per_win, recital_floor_cap, proving_standing_cap,
  recital_floors_per_day, recital_floors_burst, proving_standing_per_day, proving_standing_burst,
  campaign_stages_per_day, campaign_stages_burst, submit_interval_seconds, week_reset_hour_utc, weeks_kept)
values (
  true, 20, 72, '^ch[1-9][0-9]?_[1-9][0-9]?$', 'ch2_4',
  'ch2_8', 3, 5000, 100000,
  20, 10, 60, 20,
  24, 12, 60, 4, 8)
on conflict (id) do nothing;

-- ---------------------------------------------------------------- the boards

-- One row per player who has ever submitted: the all-time value of each board, when the server last
-- raised it (the tie-break: whoever got there first ranks first), the save revision it was read from,
-- and when the player last submitted (the flood limit). Keyed to profiles, so deleting the user takes it.
create table if not exists public.leaderboard_entries (
  user_id              uuid primary key references public.profiles (user_id) on delete cascade,
  recital_floor        integer check (recital_floor >= 0),
  recital_reached_at   timestamptz,
  proving_standing     integer check (proving_standing >= 0),
  proving_reached_at   timestamptz,
  campaign_stages      integer check (campaign_stages >= 0),
  campaign_reached_at  timestamptz,
  save_revision        integer,
  last_submit_at       timestamptz,
  created_at           timestamptz not null default now()
);

create index if not exists leaderboard_entries_recital_idx  on public.leaderboard_entries (recital_floor desc, recital_reached_at);
create index if not exists leaderboard_entries_proving_idx  on public.leaderboard_entries (proving_standing desc, proving_reached_at);
create index if not exists leaderboard_entries_campaign_idx on public.leaderboard_entries (campaign_stages desc, campaign_reached_at);

-- One row per player per week they submitted in.
create table if not exists public.leaderboard_recital_weeks (
  week_start   timestamptz not null,
  user_id      uuid not null references public.profiles (user_id) on delete cascade,
  start_floor  integer not null check (start_floor >= 0),
  best_floor   integer not null check (best_floor >= start_floor),
  reached_at   timestamptz not null default now(),
  updated_at   timestamptz not null default now(),
  primary key (week_start, user_id)
);

create index if not exists leaderboard_recital_weeks_best_idx on public.leaderboard_recital_weeks (week_start, best_floor desc, reached_at);

-- Every refused value: who, which board, what the save claimed, what was allowed, and why. Read by the
-- owner only (the queries at the bottom). Bounded by the flood limit: one submission a minute at most.
create table if not exists public.leaderboard_refusals (
  id             bigint generated always as identity primary key,
  user_id        uuid not null references public.profiles (user_id) on delete cascade,
  board          text not null check (board in ('recital', 'proving', 'campaign')),
  claimed        bigint,
  allowed        bigint,
  reason         text not null,
  save_revision  integer,
  created_at     timestamptz not null default now()
);

create index if not exists leaderboard_refusals_user_idx on public.leaderboard_refusals (user_id, created_at);

-- Players the owner has taken off the boards. Their submissions are still judged and stored (so lifting a
-- ban restores them as they are), but leaderboard_top never shows them, to anyone but themselves.
create table if not exists public.leaderboard_bans (
  user_id    uuid primary key references public.profiles (user_id) on delete cascade,
  reason     text not null default '',
  banned_at  timestamptz not null default now()
);

alter table public.leaderboard_entries       enable row level security;
alter table public.leaderboard_recital_weeks enable row level security;
alter table public.leaderboard_refusals      enable row level security;
alter table public.leaderboard_bans          enable row level security;

revoke all on public.leaderboard_entries from anon, authenticated;
revoke all on public.leaderboard_recital_weeks from anon, authenticated;
revoke all on public.leaderboard_refusals from anon, authenticated;
revoke all on public.leaderboard_bans from anon, authenticated;

-- ---------------------------------------------------------------- helpers

-- The start of the board week a moment falls in: the latest Monday at p_hour UTC at or before it.
-- date_trunc('week') of a UTC timestamp is that Monday at 00:00, so the hour is taken off first and
-- put back after, which moves the boundary to Monday at p_hour exactly as DailyLoop.WeekOf does.
create or replace function public.leaderboard_week_start(p_at timestamptz, p_hour integer)
returns timestamptz
language sql
immutable
set search_path = public, pg_temp
as $$
  select (date_trunc('week', (p_at at time zone 'utc') - make_interval(hours => p_hour))
          + make_interval(hours => p_hour)) at time zone 'utc';
$$;

revoke all on function public.leaderboard_week_start(timestamptz, integer) from public, anon, authenticated;

-- A whole number from the document, or null when the key is missing, not a number, negative, fractional
-- or longer than nine digits. Null is "malformed" to the caller, never zero.
create or replace function public.leaderboard_int(p_doc jsonb, p_key text)
returns bigint
language sql
immutable
set search_path = public, pg_temp
as $$
  select case
           when jsonb_typeof(p_doc -> p_key) = 'number' and (p_doc ->> p_key) ~ '^[0-9]{1,9}$'
             then (p_doc ->> p_key)::bigint
           else null
         end;
$$;

revoke all on function public.leaderboard_int(jsonb, text) from public, anon, authenticated;

-- ---------------------------------------------------------------- leaderboard_submit
--
-- Reads the caller's stored save, judges each board, stores what passed, logs what did not, and returns
-- what happened:
--
--   { "server_time": ..., "week_start": ..., "save_revision": 12,
--     "recital":  { "status": "raised", "value": 41 },
--     "proving":  { "status": "kept",   "value": 30 },
--     "campaign": { "status": "refused", "value": 30, "allowed": 22, "reason": "faster than play" } }
--
-- Refusals of the whole call, with PostgREST's PT convention (the last three digits are the HTTP status):
--   PT401  not signed in
--   PT404  no cloud save stored for this account (push the save first)
--   PT400  the save is older than min_save_version
--   PT429  submitted again within submit_interval_seconds
--   PT500  the leaderboard_limits row is missing
--
-- SECURITY DEFINER, because it reads public.saves and writes tables its callers hold no privilege on. It
-- reads auth.uid() itself and every statement is scoped to that value: there is no argument at all, so
-- there is nothing by which a caller can name another player or a number.
create or replace function public.leaderboard_submit()
returns jsonb
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid          uuid := auth.uid();
  lim          public.leaderboard_limits;
  sv           public.saves;
  ent          public.leaderboard_entries;
  born         timestamptz;
  doc          jsonb;
  stages       jsonb;
  stage_total  integer;
  stage_ok     integer;
  stage_unique integer;
  has_proving  boolean;
  has_recital  boolean;
  rec_high     bigint;
  rec_cur      bigint;
  pv_high      bigint;
  pv_cur       bigint;
  pv_tries     bigint;
  camp         bigint;
  week         timestamptz;
  allowed      bigint;
  base_at      timestamptz;
  days         numeric;
  rec_before   integer;
  result       jsonb;
  verdict      jsonb;
  why          text;
begin
  if uid is null then
    raise exception 'leaderboard_submit requires a signed-in user' using errcode = 'PT401';
  end if;

  select * into lim from public.leaderboard_limits where id;
  if not found then
    raise exception 'leaderboard_submit has no leaderboard_limits row - see 0016' using errcode = 'PT500';
  end if;

  select * into sv from public.saves where user_id = uid;
  if not found then
    raise exception 'leaderboard_submit found no cloud save: push the save first' using errcode = 'PT404';
  end if;
  if sv.save_version < lim.min_save_version then
    raise exception 'leaderboard_submit needs a save of version % or later, this one is %', lim.min_save_version, sv.save_version
      using errcode = 'PT400';
  end if;

  select created_at into born from public.profiles where user_id = uid;
  born := coalesce(born, now());

  -- The caller's row, made if absent and then LOCKED, so two submissions from one account queue and the
  -- second sees what the first stored (0005's chat-limit shape).
  insert into public.leaderboard_entries (user_id) values (uid) on conflict (user_id) do nothing;
  select * into ent from public.leaderboard_entries where user_id = uid for update;

  if ent.last_submit_at is not null
     and now() < ent.last_submit_at + make_interval(secs => lim.submit_interval_seconds) then
    raise exception 'leaderboard_submit: one submission every % seconds', lim.submit_interval_seconds
      using errcode = 'PT429';
  end if;
  update public.leaderboard_entries set last_submit_at = now(), save_revision = sv.revision where user_id = uid;

  doc := sv.data;
  week := public.leaderboard_week_start(now(), lim.week_reset_hour_utc);
  result := jsonb_build_object('server_time', now(), 'week_start', week, 'save_revision', sv.revision);

  -- ---- campaign: the cleared stage ids, checked element by element
  stages := doc -> 'clearedStages';
  if stages is null or jsonb_typeof(stages) <> 'array' then
    stage_total := -1;
  else
    select count(*),
           count(*) filter (where jsonb_typeof(e) = 'string' and (e #>> '{}') ~ lim.campaign_stage_pattern),
           count(distinct e)
      into stage_total, stage_ok, stage_unique
      from jsonb_array_elements(stages) as e;
  end if;
  has_proving := stage_total > 0 and stages ? lim.proving_unlock_stage;
  has_recital := stage_total > 0 and stages ? lim.recital_unlock_stage;

  why := null;
  if stage_total < 0 then
    why := 'clearedStages is missing or not a list';
  elsif stage_ok <> stage_total then
    why := 'clearedStages holds something that is not a stage id';
  elsif stage_unique <> stage_total then
    why := 'clearedStages names a stage twice';
  elsif stage_total > lim.campaign_stage_count then
    why := 'more stages than the campaign has';
  end if;
  camp := greatest(stage_total, 0);
  base_at := coalesce(ent.campaign_reached_at, born);
  days := extract(epoch from (now() - base_at)) / 86400.0;
  allowed := coalesce(ent.campaign_stages, 0) + lim.campaign_stages_burst + floor(lim.campaign_stages_per_day * days);
  if why is null and camp > coalesce(ent.campaign_stages, 0) and camp > allowed then
    why := 'faster than play';
  end if;
  if why is not null then
    insert into public.leaderboard_refusals (user_id, board, claimed, allowed, reason, save_revision)
    values (uid, 'campaign', camp, allowed, why, sv.revision);
    verdict := jsonb_build_object('status', 'refused', 'value', camp, 'allowed', allowed, 'reason', why);
  elsif camp > coalesce(ent.campaign_stages, 0) then
    update public.leaderboard_entries set campaign_stages = camp, campaign_reached_at = now() where user_id = uid;
    verdict := jsonb_build_object('status', 'raised', 'value', camp);
  else
    verdict := jsonb_build_object('status', 'kept', 'value', coalesce(ent.campaign_stages, 0));
  end if;
  result := result || jsonb_build_object('campaign', verdict);

  -- ---- proving: the highest standing, against the standing and the attempts the same save holds
  pv_high  := public.leaderboard_int(doc, 'provingHighestStanding');
  pv_cur   := public.leaderboard_int(doc, 'provingStanding');
  pv_tries := public.leaderboard_int(doc, 'provingAttempts');
  why := null;
  if pv_high is null or pv_cur is null or pv_tries is null then
    why := 'the Proving numbers are missing or malformed';
  elsif pv_cur > pv_high then
    why := 'the standing is above the highest standing';
  elsif pv_high > pv_tries * lim.proving_standing_per_win then
    why := 'more standing than the attempts could have won';
  elsif pv_high > 0 and not has_proving then
    why := 'a standing without the Proving unlocked';
  elsif pv_high > lim.proving_standing_cap then
    why := 'above the standing ceiling';
  end if;
  pv_high := coalesce(pv_high, 0);
  base_at := coalesce(ent.proving_reached_at, born);
  days := extract(epoch from (now() - base_at)) / 86400.0;
  allowed := coalesce(ent.proving_standing, 0) + lim.proving_standing_burst + floor(lim.proving_standing_per_day * days);
  if why is null and pv_high > coalesce(ent.proving_standing, 0) and pv_high > allowed then
    why := 'faster than play';
  end if;
  if why is not null then
    insert into public.leaderboard_refusals (user_id, board, claimed, allowed, reason, save_revision)
    values (uid, 'proving', pv_high, allowed, why, sv.revision);
    verdict := jsonb_build_object('status', 'refused', 'value', pv_high, 'allowed', allowed, 'reason', why);
  elsif pv_high > coalesce(ent.proving_standing, 0) then
    update public.leaderboard_entries set proving_standing = pv_high, proving_reached_at = now() where user_id = uid;
    verdict := jsonb_build_object('status', 'raised', 'value', pv_high);
  else
    verdict := jsonb_build_object('status', 'kept', 'value', coalesce(ent.proving_standing, 0));
  end if;
  result := result || jsonb_build_object('proving', verdict);

  -- ---- recital: the highest floor, against the current floor the same save holds
  rec_high := public.leaderboard_int(doc, 'recitalHighestFloor');
  rec_cur  := public.leaderboard_int(doc, 'recitalCurrentFloor');
  why := null;
  if rec_high is null or rec_cur is null then
    why := 'the Recital numbers are missing or malformed';
  elsif rec_cur <> rec_high + 1 then
    why := 'the current floor is not the floor after the highest';
  elsif rec_high > 0 and not has_recital then
    why := 'a floor without the Recital unlocked';
  elsif rec_high > lim.recital_floor_cap then
    why := 'above the floor ceiling';
  end if;
  rec_high := coalesce(rec_high, 0);
  rec_before := ent.recital_floor;
  base_at := coalesce(ent.recital_reached_at, born);
  days := extract(epoch from (now() - base_at)) / 86400.0;
  allowed := coalesce(ent.recital_floor, 0) + lim.recital_floors_burst + floor(lim.recital_floors_per_day * days);
  if why is null and rec_high > coalesce(ent.recital_floor, 0) and rec_high > allowed then
    why := 'faster than play';
  end if;
  if why is not null then
    insert into public.leaderboard_refusals (user_id, board, claimed, allowed, reason, save_revision)
    values (uid, 'recital', rec_high, allowed, why, sv.revision);
    verdict := jsonb_build_object('status', 'refused', 'value', rec_high, 'allowed', allowed, 'reason', why);
  elsif rec_high > coalesce(ent.recital_floor, 0) then
    update public.leaderboard_entries set recital_floor = rec_high, recital_reached_at = now() where user_id = uid;
    verdict := jsonb_build_object('status', 'raised', 'value', rec_high);
  else
    verdict := jsonb_build_object('status', 'kept', 'value', coalesce(ent.recital_floor, 0));
  end if;
  result := result || jsonb_build_object('recital', verdict);

  -- ---- the week's Recital row, from what is STORED now (a refused value never reaches a board)
  select recital_floor into ent.recital_floor from public.leaderboard_entries where user_id = uid;
  if coalesce(ent.recital_floor, 0) > 0 then
    insert into public.leaderboard_recital_weeks (week_start, user_id, start_floor, best_floor, reached_at, updated_at)
    values (week, uid, least(coalesce(rec_before, ent.recital_floor), ent.recital_floor), ent.recital_floor, now(), now())
    on conflict (week_start, user_id) do update
       set best_floor = greatest(public.leaderboard_recital_weeks.best_floor, excluded.best_floor),
           reached_at = case when excluded.best_floor > public.leaderboard_recital_weeks.best_floor
                             then now() else public.leaderboard_recital_weeks.reached_at end,
           updated_at = now();
  end if;

  delete from public.leaderboard_recital_weeks
   where week_start < week - make_interval(days => 7 * lim.weeks_kept);

  return result;
end;
$$;

revoke all on function public.leaderboard_submit() from public, anon;
grant execute on function public.leaderboard_submit() to authenticated;

-- ---------------------------------------------------------------- leaderboard_top
--
-- One board, best first: the top p_limit rows (1 to 100, 50 when not given), and the caller's own row
-- after them when it is not among them. Each row is a rank, a display name, a value and is_me. Ties
-- rank by who reached the value first (server time). Rows with value 0 are not on a board. Banned
-- players are left out for everyone but themselves.
--
--   recital             all-time highest floor
--   recital_week        highest floor held at the latest submission this week (the weekly board)
--   recital_week_climb  floors climbed this week
--   proving             all-time highest standing
--   campaign            stages cleared
--
-- PT401 when not signed in, PT400 for any other board name. SECURITY DEFINER because the tables carry no
-- grant; it returns no user id, so a board cannot be used to page every account.
create or replace function public.leaderboard_top(p_board text, p_limit integer default 50)
returns table (rank bigint, display_name text, value bigint, is_me boolean)
language plpgsql
stable
security definer
set search_path = public, pg_temp
as $$
#variable_conflict use_column
declare
  uid  uuid := auth.uid();
  n    integer := least(greatest(coalesce(p_limit, 50), 1), 100);
  hr   integer;
  week timestamptz;
begin
  if uid is null then
    raise exception 'leaderboard_top requires a signed-in user' using errcode = 'PT401';
  end if;
  if p_board is null or p_board not in ('recital', 'recital_week', 'recital_week_climb', 'proving', 'campaign') then
    raise exception 'leaderboard_top: no board called %', coalesce(p_board, 'null') using errcode = 'PT400';
  end if;
  select week_reset_hour_utc into hr from public.leaderboard_limits where id;
  week := public.leaderboard_week_start(now(), coalesce(hr, 4));

  return query
  with scored as (
    select e.user_id,
           case p_board
             when 'recital'  then e.recital_floor::bigint
             when 'proving'  then e.proving_standing::bigint
             else e.campaign_stages::bigint
           end as v,
           case p_board
             when 'recital'  then e.recital_reached_at
             when 'proving'  then e.proving_reached_at
             else e.campaign_reached_at
           end as at
      from public.leaderboard_entries e
     where p_board in ('recital', 'proving', 'campaign')
    union all
    select w.user_id,
           case p_board when 'recital_week' then w.best_floor::bigint else (w.best_floor - w.start_floor)::bigint end,
           w.reached_at
      from public.leaderboard_recital_weeks w
     where p_board in ('recital_week', 'recital_week_climb') and w.week_start = week
  ),
  visible as (
    select s.* from scored s
     where coalesce(s.v, 0) > 0
       and (s.user_id = uid or not exists (select 1 from public.leaderboard_bans b where b.user_id = s.user_id))
  ),
  ranked as (
    select rank() over (order by v.v desc, v.at asc nulls last) as r, v.user_id, v.v
      from visible v
  )
  select ranked.r, p.display_name, ranked.v, ranked.user_id = uid
    from ranked join public.profiles p on p.user_id = ranked.user_id
   where ranked.r <= n or ranked.user_id = uid
   order by ranked.r, p.display_name;
end;
$$;

revoke all on function public.leaderboard_top(text, integer) from public, anon;
grant execute on function public.leaderboard_top(text, integer) to authenticated;

-- ---------------------------------------------------------------- what was applied, said once

do $$
declare
  t    text;
  who  text;
  verb text;
begin
  foreach t in array array['leaderboard_limits', 'leaderboard_entries', 'leaderboard_recital_weeks',
                           'leaderboard_refusals', 'leaderboard_bans'] loop
    if not (select c.relrowsecurity from pg_class c where c.oid = ('public.' || t)::regclass) then
      raise exception '0016: row level security is not on for %', t;
    end if;
    if exists (select 1 from pg_policies where schemaname = 'public' and tablename = t) then
      raise exception '0016: % carries a policy; no leaderboard table is meant to have one', t;
    end if;
    foreach who in array array['anon', 'authenticated'] loop
      foreach verb in array array['select', 'insert', 'update', 'delete'] loop
        if has_table_privilege(who, 'public.' || t, verb) or has_any_column_privilege(who, 'public.' || t, verb) then
          raise exception '0016: % can % %', who, verb, t;
        end if;
      end loop;
    end loop;
  end loop;
  if has_function_privilege('anon', 'public.leaderboard_submit()', 'execute')
     or has_function_privilege('anon', 'public.leaderboard_top(text, integer)', 'execute') then
    raise exception '0016: anon can call a leaderboard function';
  end if;
  raise notice '0016: five leaderboard tables, none readable or writable by a client; leaderboard_submit and leaderboard_top for authenticated only';
end;
$$;

notify pgrst, 'reload schema';

-- ---------------------------------------------------------------- after applying, by hand
--
-- Read-only, and nothing in this file runs them. The SQL editor bypasses RLS, so these can read what no
-- client can.
--
--   -- the limits as they stand
--   select * from public.leaderboard_limits;
--
--   -- the latest refusals, and the accounts refused most (an account with dozens is worth a look)
--   select r.created_at, p.display_name, r.board, r.claimed, r.allowed, r.reason
--     from public.leaderboard_refusals r join public.profiles p using (user_id)
--    order by r.created_at desc limit 50;
--   select user_id, count(*) from public.leaderboard_refusals group by user_id order by 2 desc limit 20;
--
--   -- this week's Recital board as the game would show it
--   select * from public.leaderboard_recital_weeks
--    where week_start = public.leaderboard_week_start(now(), 4) order by best_floor desc limit 20;
--
--   -- take a player off every board, and put them back
--   --   insert into public.leaderboard_bans (user_id, reason) values ('<uuid>', 'forged save');
--   --   delete from public.leaderboard_bans where user_id = '<uuid>';
--
--   -- the rows the rules test left behind are owned by its throwaway users; deleting those users in
--   -- Authentication -> Users cascades to every table here.
