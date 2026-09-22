-- 0008_guild_dungeon_playable.sql - what the guild dungeon needs to be played rather than only
-- recorded: a content version and a loss count on every attempt, one level for every line, and
-- a way to put an attempt into guild chat as something a guildmate can watch.
--
-- Run after 0007. RE-APPLIABLE, like 0004-0007: columns are `add column if not exists`, every
-- function is `create or replace`. 0007 is not edited; it is applied to the live project, and
-- everything here is additive to it.
--
-- ---------------------------------------------------------------- what changed in the design
--
-- 1. EVERY LINE FIGHTS AT ONE LEVEL, with no relics and no armor; talents stay. The client
--    normalises (GuildDungeon.NormaliseTeam) and this file refuses a submitted line that is not
--    normalised, so a record on a board is a record of the same kind of fight as every other.
--    The level lives twice, like the attempt cap: guild_dungeon_hero_level() below and
--    balance.json's guildDungeonHeroLevel, and Tools/DevHarness/GuildDungeonChecks.cs fails the
--    build-check when they disagree.
--
-- 2. THE BOARDS ARE CONSTRAINTS NOW (four or fewer, no five stars, one aspect, no healer, first
--    attempt, nobody fell), ranked by turns. Nothing here ranks anything: every one of them is a
--    pure function of the team, the attempt number and the loss count, which the row already
--    holds or gains below, so the board is still one SELECT and a client-side sort.
--
-- 3. A RECORD NAMES ITS CONTENT. `content_version` is ContentDatabase.ContentVersion at the moment
--    of the fight. A replay under other content says so instead of playing a fight that did not
--    happen. Rows written through 0007's guild_dungeon_record carry NULL, which the client reads
--    as "cannot vouch for this" - the honest answer about a row that predates the field.
--
-- ---------------------------------------------------------------- 0007's guild_dungeon_record
--
-- LEFT CALLABLE, AND THAT IS A DELIBERATE, TEMPORARY GAP. It still accepts an un-normalised line
-- with no content version and no floor gate. The client no longer calls it; it calls
-- guild_dungeon_submit below. Revoking it here would break Tools/supabase-guild-rules-test.py,
-- which exercises it and is not this migration's to change. The revoke belongs in the migration
-- after the rules test moves to guild_dungeon_submit:
--
--   revoke execute on function public.guild_dungeon_record(integer, integer, integer, jsonb,
--     boolean, integer, integer, numeric) from authenticated;
--
-- No anti-cheat is claimed by either path. Every field is still the client's word; what makes that
-- tolerable is that every row can be re-run by anything holding the engine.

-- ---------------------------------------------------------------- the constant

create or replace function public.guild_dungeon_hero_level()
returns integer
language sql
immutable
as $$ select 40; $$;

-- ---------------------------------------------------------------- the record's new columns

-- 64 characters is generous: the client's scheme is "c1-" and sixteen hex digits. The bound is
-- there so the column cannot become somewhere to store anything else.
alter table public.guild_dungeon_attempts
  add column if not exists content_version text
    check (content_version is null or char_length(content_version) between 1 and 64);

-- Heroes of the line that died during the fight. NULL on a 0007 row. Bounded by the team size in
-- the submit call, where the team is in hand; five is the ceiling a CHECK can state on its own.
alter table public.guild_dungeon_attempts
  add column if not exists fallen smallint
    check (fallen is null or fallen between 0 and 5);

-- ---------------------------------------------------------------- recording an attempt

-- guild_dungeon_record's rules, all of them, plus four:
--
--   * a content version is required;
--   * the loss count is 0 to the team's size, and a loss lost at least one hero;
--   * EVERY UNIT IS AT guild_dungeon_hero_level() AND CARRIES NO RELIC AND NO ARMOR - the line
--     that fought the dungeon, not the line the player owns;
--   * THE FLOOR IS OPEN: floor 1 always, floor N once anybody in the caller's guild has cleared
--     floor N-1 this cycle. The guild pushes together; a newer member is never locked out of the
--     floor the guild is on.
--
-- Named differently from guild_dungeon_record rather than overloading it. PostgREST picks an
-- overload by the set of argument NAMES, and two functions of one name whose name sets overlap
-- this much is an ambiguity waiting for the first optional argument.
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

-- ---------------------------------------------------------------- the handover

-- A chat message can carry an attempt. The message's body is still the 1-200 character line
-- 0004 requires, and it is written to stand on its own ("Floor 3: cleared in 9 turns with 4
-- heroes. Watch it."), because a client that predates this column draws the body and nothing
-- else. A client that knows the column draws a playable card instead, from the row it embeds.
--
-- ON DELETE SET NULL: an attempt only goes when its guild or its member's profile does, and the
-- guild case takes the message with it anyway. The member case leaves the line, which is a line
-- somebody wrote, and drops the card, which can no longer be played.
alter table public.guild_messages
  add column if not exists attempt_id bigint
    references public.guild_dungeon_attempts (id) on delete set null;

-- Posts one of the caller's OWN attempts into the caller's guild chat. The same rules as
-- guild_post_message - the member row locked, the body checked, one message every two seconds,
-- posting counts as presence - and one more: the attempt must be the caller's and must belong to
-- the guild being posted in. A member who changed guild cannot post their old guild's fight into
-- the new one, whose members could not read the row anyway.
--
-- Win or lose, deliberately. A loss is the most useful thing to share: "this is what beat me".
create or replace function public.guild_share_attempt(p_attempt_id bigint, p_body text)
returns public.guild_messages
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid      uuid := auth.uid();
  mine     uuid;
  line     text := btrim(coalesce(p_body, ''));
  last_at  timestamptz;
  result   public.guild_messages;
  cooldown constant interval := interval '2 seconds';
begin
  if uid is null then
    raise exception 'guild_share_attempt requires a signed-in user' using errcode = 'PT401';
  end if;

  select guild_id into mine from public.guild_members where user_id = uid for update;
  if not found then
    raise exception 'you are not in a guild' using errcode = 'PT403';
  end if;

  if not exists (select 1 from public.guild_dungeon_attempts a
                  where a.id = p_attempt_id and a.user_id = uid and a.guild_id = mine) then
    raise exception 'you can only share your own attempts, in the guild they were fought for'
      using errcode = 'PT403';
  end if;

  if char_length(line) < 1 or char_length(line) > 200 then
    raise exception 'a message is 1 to 200 characters' using errcode = 'PT400';
  end if;
  if line ~ '[[:cntrl:]]' then
    raise exception 'a message is a single line' using errcode = 'PT400';
  end if;

  select max(created_at) into last_at from public.guild_messages where user_id = uid;
  if last_at is not null and now() - last_at < cooldown then
    raise exception 'you are posting too quickly - one message every % seconds',
      extract(epoch from cooldown)::integer using errcode = 'PT429';
  end if;

  insert into public.guild_messages (guild_id, user_id, body, attempt_id)
  values (mine, uid, line, p_attempt_id)
  returning * into result;

  update public.guild_members set last_seen = now() where user_id = uid;
  return result;
end;
$$;

-- ---------------------------------------------------------------- privileges

revoke all on function public.guild_dungeon_hero_level() from public, anon;
revoke all on function public.guild_dungeon_submit(integer, integer, integer, jsonb, boolean, integer,
  integer, numeric, integer, text) from public, anon;
revoke all on function public.guild_share_attempt(bigint, text) from public, anon;

grant execute on function public.guild_dungeon_hero_level() to authenticated;
grant execute on function public.guild_dungeon_submit(integer, integer, integer, jsonb, boolean, integer,
  integer, numeric, integer, text) to authenticated;
grant execute on function public.guild_share_attempt(bigint, text) to authenticated;
