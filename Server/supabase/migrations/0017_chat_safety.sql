-- 0017_chat_safety.sql - report a guild chat message, block a player, and let officers mute.
--
-- WRITTEN, NOT APPLIED. Run after 0004 and 0005 (it reads guild_members, guild_messages and the four
-- caller-keyed questions 0004 defines). Independent of 0007 to 0016.
--
-- RE-APPLIABLE, like 0004 onward: tables are `if not exists`, every policy is preceded by
-- `drop policy if exists`, functions are `create or replace`, and the trigger is dropped first.
--
-- ---------------------------------------------------------------- why this exists
--
-- Guild chat is text one player writes and other players read. Google Play's user-generated content
-- policy asks for an in-app way to report it and to block the person writing it, and for the
-- developer to be able to act on reports. Apple's guideline 1.2 asks for the same plus a way to
-- filter. 0004 shipped chat with no edit, no delete and no moderation, and said so; this file is the
-- moderation half.
--
-- ---------------------------------------------------------------- the three pieces
--
-- BLOCK is personal. A player blocks another player (user_block) and stops seeing their messages,
-- and unblocks them the same way (user_unblock). Nobody else is affected and the blocked player is
-- not told. The table is read only to clients, like guild_messages, so the two calls are the only
-- writers. It is enforced HERE, in guild_messages' read policy,
-- not by the client hiding rows: a client that has not been updated still stops receiving a blocked
-- player's lines, because the database no longer returns them. A block is by account, not by guild,
-- so it follows the blocked player into any guild the two later share.
--
-- MUTE is a guild officer's tool. A leader or officer stops a member posting in that guild's chat
-- for a fixed time. The matrix is 0004's removal matrix: a leader may mute an officer or a member,
-- an officer may mute a member, nobody may mute the leader, nobody mutes themselves. A mute is keyed
-- to (guild, player) and survives the player leaving, so leaving and rejoining does not lift it.
-- It is enforced by a BEFORE INSERT trigger on guild_messages, which covers both doors a message
-- comes through (guild_post_message in 0005 and guild_share_attempt in 0008) without rewriting
-- either function.
--
-- REPORT goes to the developer, not to the guild. A member reports a message they can see; the
-- report stores a copy of the body and the time it was sent, so it still says what was written if
-- the message, the guild or the author is later deleted. No client role can read reports. The owner
-- reviews them in the SQL editor with Server/supabase/queries/chat-reports.sql.
--
-- ---------------------------------------------------------------- what is not here, on purpose
--
--   * No word filter. Apple asks for "a method for filtering objectionable material"; whether that
--     is a client-side word list, a server-side check in guild_post_message or a third-party service
--     is an owner decision (Docs/LAUNCH-CHECKLIST.md).
--   * No automatic hide after N reports. A threshold is easy to abuse with a few accounts, and the
--     right number depends on how many players there are. Also an owner decision.
--   * No leader or officer delete of a message. The operator can delete one from the SQL editor
--     (the queries file shows how); whether officers get that power in game is a design call.

-- ---------------------------------------------------------------- limits, held in one row
--
-- One row, like save_limits (0013) and guild_rate_limits (0014), so a limit moves with an UPDATE
-- rather than a function rewrite. No client role can read or write it.
--
--   report_daily_limit   20 reports per reporter per rolling 24 hours. Far above what a real player
--                        sends, low enough that one account cannot flood the review queue.
--   mute_max_minutes     10080, seven days. A longer problem is a removal, which officers can do.
--   block_limit          500 blocks per account, a ceiling against a script filling the table.
create table if not exists public.chat_safety_limits (
  id                 boolean primary key default true check (id),
  report_daily_limit integer not null check (report_daily_limit >= 1),
  mute_max_minutes   integer not null check (mute_max_minutes between 1 and 525600),
  block_limit        integer not null check (block_limit >= 1),
  updated_at         timestamptz not null default now()
);

alter table public.chat_safety_limits enable row level security;
revoke all on public.chat_safety_limits from anon, authenticated;

insert into public.chat_safety_limits (id, report_daily_limit, mute_max_minutes, block_limit)
values (true, 20, 10080, 500)
on conflict (id) do nothing;

-- ---------------------------------------------------------------- blocks

create table if not exists public.user_blocks (
  blocker    uuid not null references public.profiles (user_id) on delete cascade,
  blocked    uuid not null references public.profiles (user_id) on delete cascade,
  created_at timestamptz not null default now(),
  primary key (blocker, blocked),
  constraint user_blocks_not_self check (blocker <> blocked)
);

create index if not exists user_blocks_blocked_idx on public.user_blocks (blocked);

-- The question guild_messages' read policy asks: has the CALLER blocked this author? auth.uid() is
-- read inside, as in every caller-keyed question since 0003, and SECURITY DEFINER for the reason
-- is_friend gives: a policy that reads another table through that table's own policies is the shape
-- that recurses later.
create or replace function public.has_blocked(other_user uuid)
returns boolean
language sql
stable
security definer
set search_path = public, pg_temp
as $$
  select exists (
    select 1 from public.user_blocks
     where blocker = auth.uid() and blocked = other_user
  );
$$;

revoke all on function public.has_blocked(uuid) from public, anon;
grant execute on function public.has_blocked(uuid) to authenticated;

alter table public.user_blocks enable row level security;

revoke all on public.user_blocks from anon, authenticated;
-- Read only, like guild_messages. Blocking and unblocking go through the two calls below, so the
-- client write grants stay exactly the set CloudChecks' allowlist names, and the ceiling is held
-- where the row is written.
grant select on public.user_blocks to authenticated;

-- A player sees only their own block list. The blocked player cannot learn they were blocked.
drop policy if exists user_blocks_read_own on public.user_blocks;
create policy user_blocks_read_own
  on public.user_blocks for select
  to authenticated
  using (blocker = auth.uid());

-- Blocking. Answers true when a block was added and false when it already existed. Any signed-in
-- player can be blocked, guildmate or not, because a block is by account (see the header).
--
-- The caller's own profile row is taken FOR UPDATE before the count, so two blocks sent in the same
-- instant queue and the ceiling holds in fact rather than in practice (guild_post_message's reason).
create or replace function public.user_block(p_user uuid)
returns boolean
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid     uuid := auth.uid();
  ceiling integer;
  held    integer;
begin
  if uid is null then
    raise exception 'user_block requires a signed-in user' using errcode = 'PT401';
  end if;
  if p_user is null or p_user = uid then
    raise exception 'you cannot block yourself' using errcode = 'PT400';
  end if;
  if not exists (select 1 from public.profiles where user_id = p_user) then
    raise exception 'no such player' using errcode = 'PT404';
  end if;

  select block_limit into ceiling from public.chat_safety_limits where id;
  if ceiling is null then
    raise exception 'chat_safety_limits has no row - see 0017' using errcode = 'PT500';
  end if;

  perform 1 from public.profiles where user_id = uid for update;
  if exists (select 1 from public.user_blocks where blocker = uid and blocked = p_user) then
    return false;
  end if;
  select count(*) into held from public.user_blocks where blocker = uid;
  if held >= ceiling then
    raise exception 'you can block at most % players', ceiling using errcode = 'PT409';
  end if;

  insert into public.user_blocks (blocker, blocked) values (uid, p_user);
  return true;
end;
$$;

-- Unblocking. Answers true when a block was removed and false when there was none.
create or replace function public.user_unblock(p_user uuid)
returns boolean
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid uuid := auth.uid();
begin
  if uid is null then
    raise exception 'user_unblock requires a signed-in user' using errcode = 'PT401';
  end if;
  delete from public.user_blocks where blocker = uid and blocked = p_user;
  return found;
end;
$$;

revoke all on function public.user_block(uuid) from public, anon;
revoke all on function public.user_unblock(uuid) from public, anon;
grant execute on function public.user_block(uuid) to authenticated;
grant execute on function public.user_unblock(uuid) to authenticated;

-- 0004's read policy, with one clause added: a blocked author's lines are not returned. The rest is
-- 0004's text unchanged, so a member still reads only their own guild's chat.
drop policy if exists guild_messages_read_own_guild on public.guild_messages;
create policy guild_messages_read_own_guild
  on public.guild_messages for select
  to authenticated
  using (guild_id = public.my_guild() and not public.has_blocked(user_id));

-- ---------------------------------------------------------------- mutes

create table if not exists public.guild_chat_mutes (
  guild_id    uuid not null references public.guilds (id) on delete cascade,
  user_id     uuid not null references public.profiles (user_id) on delete cascade,
  muted_by    uuid references public.profiles (user_id) on delete set null,
  muted_until timestamptz not null,
  created_at  timestamptz not null default now(),
  primary key (guild_id, user_id)
);

alter table public.guild_chat_mutes enable row level security;

revoke all on public.guild_chat_mutes from anon, authenticated;
-- Read only. Muting and unmuting go through the two calls below, which hold the role matrix.
grant select on public.guild_chat_mutes to authenticated;

-- The muted player sees their own mute (so the client can say "muted until ..."), and the guild's
-- leader and officers see every mute in their guild. Ordinary members do not see who is muted.
drop policy if exists guild_chat_mutes_read on public.guild_chat_mutes;
create policy guild_chat_mutes_read
  on public.guild_chat_mutes for select
  to authenticated
  using (
    user_id = auth.uid()
    or (guild_id = public.my_guild() and public.my_guild_role() in ('leader', 'officer'))
  );

-- Muting. Answers the mute row. p_minutes from 1 to chat_safety_limits.mute_max_minutes. Muting a
-- player who is already muted replaces the end time, so an officer can shorten or lengthen a mute
-- with the same call.
create or replace function public.guild_mute_member(p_user uuid, p_minutes integer)
returns public.guild_chat_mutes
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid         uuid := auth.uid();
  mine        uuid := public.my_guild();
  my_role     text := coalesce(public.my_guild_role(), '');
  their_role  text;
  max_minutes integer;
  result      public.guild_chat_mutes;
begin
  if uid is null then
    raise exception 'guild_mute_member requires a signed-in user' using errcode = 'PT401';
  end if;
  if mine is null or my_role not in ('leader', 'officer') then
    raise exception 'only a leader or an officer can mute' using errcode = 'PT403';
  end if;
  if p_user is null or p_user = uid then
    raise exception 'you cannot mute yourself' using errcode = 'PT400';
  end if;

  select mute_max_minutes into max_minutes from public.chat_safety_limits where id;
  if max_minutes is null then
    raise exception 'chat_safety_limits has no row - see 0017' using errcode = 'PT500';
  end if;
  if p_minutes is null or p_minutes < 1 or p_minutes > max_minutes then
    raise exception 'a mute is 1 to % minutes', max_minutes using errcode = 'PT400';
  end if;

  select role into their_role from public.guild_members
   where user_id = p_user and guild_id = mine for update;
  if not found then
    raise exception 'that player is not in your guild' using errcode = 'PT404';
  end if;
  if not ((my_role = 'leader' and their_role in ('officer', 'member'))
          or (my_role = 'officer' and their_role = 'member')) then
    raise exception 'a % cannot mute a %', my_role, their_role using errcode = 'PT403';
  end if;

  insert into public.guild_chat_mutes (guild_id, user_id, muted_by, muted_until)
  values (mine, p_user, uid, now() + make_interval(mins => p_minutes))
  on conflict (guild_id, user_id) do update
     set muted_by = excluded.muted_by,
         muted_until = excluded.muted_until,
         created_at = now()
  returning * into result;
  return result;
end;
$$;

-- Unmuting. The same matrix as muting, read against the player's role now. Answers true when a mute
-- was lifted and false when there was none.
create or replace function public.guild_unmute_member(p_user uuid)
returns boolean
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid        uuid := auth.uid();
  mine       uuid := public.my_guild();
  my_role    text := coalesce(public.my_guild_role(), '');
  their_role text;
begin
  if uid is null then
    raise exception 'guild_unmute_member requires a signed-in user' using errcode = 'PT401';
  end if;
  if mine is null or my_role not in ('leader', 'officer') then
    raise exception 'only a leader or an officer can unmute' using errcode = 'PT403';
  end if;

  -- A player who has left still has a mute row for this guild; their role then counts as member,
  -- so an officer can lift it.
  select coalesce((select role from public.guild_members where user_id = p_user and guild_id = mine), 'member')
    into their_role;
  if not ((my_role = 'leader' and their_role in ('officer', 'member'))
          or (my_role = 'officer' and their_role = 'member')) then
    raise exception 'a % cannot unmute a %', my_role, their_role using errcode = 'PT403';
  end if;

  delete from public.guild_chat_mutes where guild_id = mine and user_id = p_user;
  return found;
end;
$$;

revoke all on function public.guild_mute_member(uuid, integer) from public, anon;
revoke all on function public.guild_unmute_member(uuid) from public, anon;
grant execute on function public.guild_mute_member(uuid, integer) to authenticated;
grant execute on function public.guild_unmute_member(uuid) to authenticated;

-- The enforcement. Every row into guild_messages passes here, whichever function wrote it.
-- PT403 so PostgREST answers 403, the same status a non-member's post gets.
create or replace function public.guild_messages_refuse_muted()
returns trigger
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  until_at timestamptz;
begin
  select muted_until into until_at from public.guild_chat_mutes
   where guild_id = new.guild_id and user_id = new.user_id;
  if until_at is not null and until_at > now() then
    raise exception 'you are muted in this guild chat until %', to_char(until_at at time zone 'utc', 'YYYY-MM-DD HH24:MI "UTC"')
      using errcode = 'PT403';
  end if;
  return new;
end;
$$;

drop trigger if exists guild_messages_mute_check on public.guild_messages;
create trigger guild_messages_mute_check
  before insert on public.guild_messages
  for each row execute function public.guild_messages_refuse_muted();

-- ---------------------------------------------------------------- reports

-- Every user column is SET NULL rather than CASCADE: a report outlives the account that sent it and
-- the account it is about, because the copy of the body is the evidence. The operator's retention
-- purge (queries/chat-reports.sql) is what removes old reports, not an account deletion.
create table if not exists public.chat_reports (
  id              bigint generated always as identity primary key,
  reporter        uuid references public.profiles (user_id) on delete set null,
  reported_user   uuid references public.profiles (user_id) on delete set null,
  guild_id        uuid references public.guilds (id) on delete set null,
  message_id      bigint references public.guild_messages (id) on delete set null,
  message_body    text not null,
  message_sent_at timestamptz not null,
  reason          text not null
                  check (reason in ('harassment', 'hate', 'sexual', 'spam', 'cheating',
                                    'personal_info', 'other')),
  note            text not null default ''
                  check (char_length(note) <= 300 and note !~ '[[:cntrl:]]'),
  status          text not null default 'open'
                  check (status in ('open', 'actioned', 'dismissed')),
  created_at      timestamptz not null default now(),
  reviewed_at     timestamptz
);

-- One report per reporter per message. A partial index, because either side may later be NULL.
create unique index if not exists chat_reports_once
  on public.chat_reports (reporter, message_id)
  where reporter is not null and message_id is not null;

create index if not exists chat_reports_queue_idx on public.chat_reports (status, created_at);
create index if not exists chat_reports_reporter_idx on public.chat_reports (reporter, created_at desc);

alter table public.chat_reports enable row level security;
-- No client privilege at all. Filing goes through the call below; reading is the operator's.
revoke all on public.chat_reports from anon, authenticated;

-- Filing a report. Answers {"report_id": <id>}.
--
-- The reporter must be in the guild the message was posted in, which is the same rule as being able
-- to read it, so a player can only report what they have seen. A blocked author's message can still
-- be reported: the function reads the table as its owner, so the block that hides the line does not
-- hide it from the report.
create or replace function public.guild_report_message(p_message_id bigint, p_reason text, p_note text default '')
returns jsonb
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid       uuid := auth.uid();
  mine      uuid := public.my_guild();
  msg       public.guild_messages;
  per_day   integer;
  sent      integer;
  clean     text := btrim(coalesce(p_note, ''));
  new_id    bigint;
begin
  if uid is null then
    raise exception 'guild_report_message requires a signed-in user' using errcode = 'PT401';
  end if;
  if p_reason is null or p_reason not in ('harassment', 'hate', 'sexual', 'spam', 'cheating',
                                          'personal_info', 'other') then
    raise exception 'a reason is one of harassment, hate, sexual, spam, cheating, personal_info, other'
      using errcode = 'PT400';
  end if;
  if char_length(clean) > 300 or clean ~ '[[:cntrl:]]' then
    raise exception 'a note is a single line of at most 300 characters' using errcode = 'PT400';
  end if;

  select * into msg from public.guild_messages where id = p_message_id;
  if not found or mine is null or msg.guild_id <> mine then
    -- One answer for "no such message" and "not your guild", so the call cannot be used to probe
    -- which message ids exist in other guilds.
    raise exception 'you can only report a message in your own guild chat' using errcode = 'PT403';
  end if;
  if msg.user_id = uid then
    raise exception 'you cannot report your own message' using errcode = 'PT400';
  end if;

  select report_daily_limit into per_day from public.chat_safety_limits where id;
  if per_day is null then
    raise exception 'chat_safety_limits has no row - see 0017' using errcode = 'PT500';
  end if;
  -- Serialise one reporter's calls so two in the same instant cannot both pass the count.
  perform 1 from public.profiles where user_id = uid for update;
  select count(*) into sent from public.chat_reports
   where reporter = uid and created_at > now() - interval '24 hours';
  if sent >= per_day then
    raise exception 'you have sent % reports in the last 24 hours; try again later', sent
      using errcode = 'PT429';
  end if;

  begin
    insert into public.chat_reports
      (reporter, reported_user, guild_id, message_id, message_body, message_sent_at, reason, note)
    values
      (uid, msg.user_id, msg.guild_id, msg.id, msg.body, msg.created_at, p_reason, clean)
    returning id into new_id;
  exception when unique_violation then
    raise exception 'you have already reported this message' using errcode = 'PT409';
  end;

  return jsonb_build_object('report_id', new_id);
end;
$$;

revoke all on function public.guild_report_message(bigint, text, text) from public, anon;
grant execute on function public.guild_report_message(bigint, text, text) to authenticated;

-- ---------------------------------------------------------------- what was applied, said once

do $$
declare
  who text;
  verb text;
begin
  foreach who in array array['anon', 'authenticated'] loop
    foreach verb in array array['select', 'insert', 'update', 'delete'] loop
      if has_table_privilege(who, 'public.chat_reports', verb) then
        raise exception '0017: % can % chat_reports', who, verb;
      end if;
      if has_table_privilege(who, 'public.chat_safety_limits', verb) then
        raise exception '0017: % can % chat_safety_limits', who, verb;
      end if;
    end loop;
    -- has_any_column_privilege knows select, insert, update and references only, so delete is
    -- asked of the table alone.
    foreach verb in array array['insert', 'update', 'delete'] loop
      if has_table_privilege(who, 'public.guild_chat_mutes', verb) then
        raise exception '0017: % can % guild_chat_mutes directly', who, verb;
      end if;
      if has_table_privilege(who, 'public.user_blocks', verb) then
        raise exception '0017: % can % user_blocks directly', who, verb;
      end if;
    end loop;
    foreach verb in array array['insert', 'update'] loop
      if has_any_column_privilege(who, 'public.user_blocks', verb)
         or has_any_column_privilege(who, 'public.guild_chat_mutes', verb) then
        raise exception '0017: % can % a column of user_blocks or guild_chat_mutes', who, verb;
      end if;
    end loop;
  end loop;
  if has_table_privilege('anon', 'public.user_blocks', 'select')
     or has_table_privilege('anon', 'public.guild_chat_mutes', 'select') then
    raise exception '0017: anon can read user_blocks or guild_chat_mutes';
  end if;
end;
$$;
