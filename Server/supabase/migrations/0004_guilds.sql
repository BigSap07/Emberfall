-- 0004_guilds.sql - guilds: the four tables, and who may see or touch a row.
--
-- Run after 0003. 0005 adds the calls whose rules span rows; 0006 opens a guildmate's team
-- snapshot. All three are new files: 0001-0003 are applied and are never edited.
--
-- RE-APPLIABLE, unlike 0001-0003. Every policy here is preceded by `drop policy if exists`, so
-- pasting this file a second time is a no-op rather than `policy ... already exists`. The three
-- files before it were written without that and the README says what re-applying them costs;
-- there is no reason for a fourth file to inherit the inconvenience.
--
-- ---------------------------------------------------------------- the shape of the decision
--
-- ONE GUILD PER PLAYER IS A PRIMARY KEY, NOT A CHECK. `guild_members.user_id` is the primary
-- key of the table, so a second membership for the same player is refused by the index whatever
-- else is happening - two officers of two different guilds accepting the same applicant in the
-- same instant included. A rule that can be expressed as a constraint is not left to a function
-- to remember.
--
-- ROLES ARE ONE COLUMN AND ONE PARTIAL INDEX. There is no `leader` column on `guilds`: the
-- leader is the member whose role says so, and `guild_members_one_leader` makes "at most one
-- leader per guild" an index rather than a habit. A second copy of the same fact on the parent
-- row is a second thing to keep in step, and the two would disagree the first time a transfer
-- failed halfway.
--
-- NOTHING IS TRUSTED FROM THE CLIENT, and the grants below are half of how. Joining, accepting,
-- changing a role, transferring and posting a message carry no client privilege at all - they
-- are the RPCs in 0005, for the same reason `saves` grants only SELECT and every write goes
-- through `push_save`. What IS left to a policy is what a policy can state completely: reading
-- a roster, leaving, removing somebody, declining a request, disbanding.

-- ---------------------------------------------------------------- guilds

-- `name` is unique on lower(name), not on name. Two guilds called "Emberfall" and "emberfall"
-- are one guild as far as a player reading a roster is concerned, and a namespace that treats
-- them as two is a namespace that can be squatted twice over.
--
-- `guild_code` uses the SAME GENERATOR AND THE SAME ALPHABET as a friend code - see
-- generate_friend_code() in 0001 - because the reason for that alphabet is about people reading
-- six characters off a phone screen to each other, and that is exactly what a guild code is for
-- too. The two code spaces are separate: they live in different tables and are resolved by
-- different calls, so a guild code and a friend code may be equal and neither lookup can see
-- the other's rows.
--
-- The CHECK restates the alphabet rather than trusting the generator, as profiles does, and
-- `name = btrim(name)` makes the trim a guarantee instead of something guild_create remembers.
-- [[:cntrl:]] is refused on every free-text column here: a newline in a guild name or a chat
-- line is a layout bug in a UI built out of single-line labels, and refusing it at the column is
-- the only place it cannot be forgotten.
create table if not exists public.guilds (
  id          uuid primary key default gen_random_uuid(),
  name        text not null
              check (name = btrim(name)
                     and char_length(name) between 3 and 20
                     and name !~ '[[:cntrl:]]'),
  description text not null default ''
              check (char_length(description) <= 140 and description !~ '[[:cntrl:]]'),
  guild_code  text not null unique
              check (guild_code ~ '^[2-9A-HJKMNP-TV-Z]{6}$'),
  -- An open guild accepts a request the moment it is made. It is a property of the guild rather
  -- than of the request, so a guild that closes stops auto-accepting immediately and whatever
  -- was already queued stays queued.
  is_open     boolean not null default false,
  created_at  timestamptz not null default now()
);

create unique index if not exists guilds_name_unique on public.guilds (lower(name));

-- ---------------------------------------------------------------- membership

-- profiles rather than auth.users, for the reason 0003 gives: a roster is read alongside every
-- member's name, and a declared relationship is what lets PostgREST resolve those names inline
-- instead of turning a thirty-member roster into thirty profile lookups.
--
-- `last_seen` is a presence stamp, not a login time. Nothing in this game has a session, so the
-- only honest thing it can mean is "when this player's game last reached the server", which is
-- what guild_touch() in 0005 records. It is written by that function and by posting a message,
-- and by nothing else - a client cannot set it, so it cannot claim a freshness it does not have,
-- which is the same rule team_snapshots.updated_at is under.
create table if not exists public.guild_members (
  -- THE ONE-GUILD RULE. Primary key on the player, not on (guild, player).
  user_id   uuid primary key references public.profiles (user_id) on delete cascade,
  guild_id  uuid not null references public.guilds (id) on delete cascade,
  role      text not null default 'member' check (role in ('leader', 'officer', 'member')),
  joined_at timestamptz not null default now(),
  last_seen timestamptz not null default now()
);

create index if not exists guild_members_guild_idx on public.guild_members (guild_id);

-- At most one leader per guild, as an index. The other half - that a guild always HAS one - is
-- kept by guild_transfer_leadership demoting before it promotes, by the leave policy refusing a
-- leader, and by the succession trigger below.
create unique index if not exists guild_members_one_leader
  on public.guild_members (guild_id) where role = 'leader';

-- ---------------------------------------------------------------- requests

-- A REQUEST HAS NO STATUS COLUMN, and the absence is the design. A row's existence is "pending";
-- accepting turns it into a membership and deletes it; declining and withdrawing delete it. Three
-- player actions, one row deletion each, exactly as 0003's friendships found for its own three -
-- and unlike a friendship there is no accepted state to keep, because the accepted state is the
-- membership row.
--
-- A player may have several requests out at once, to several guilds. Accepting one deletes the
-- rest (see guild_request_accept), because they describe a player who is now in a guild.
create table if not exists public.guild_requests (
  id         uuid primary key default gen_random_uuid(),
  guild_id   uuid not null references public.guilds (id) on delete cascade,
  user_id    uuid not null references public.profiles (user_id) on delete cascade,
  created_at timestamptz not null default now(),
  constraint guild_requests_once unique (guild_id, user_id)
);

create index if not exists guild_requests_guild_idx
  on public.guild_requests (guild_id, created_at);

-- ---------------------------------------------------------------- chat

-- `id` is a bigint identity rather than a uuid because the only thing ever asked of it besides
-- identity is order, and a sequence answers both. The client reads the newest hundred whole on
-- every poll rather than paging forward from a cursor - see GuildService for why that is the
-- cheaper mistake to make at this size.
--
-- 200 characters is the column's rule, not the screen's. The screen restates it so a player is
-- stopped before a round trip; this is what makes it true.
create table if not exists public.guild_messages (
  id         bigint generated always as identity primary key,
  guild_id   uuid not null references public.guilds (id) on delete cascade,
  user_id    uuid not null references public.profiles (user_id) on delete cascade,
  body       text not null
             check (char_length(body) between 1 and 200 and body !~ '[[:cntrl:]]'),
  created_at timestamptz not null default now()
);

create index if not exists guild_messages_guild_idx
  on public.guild_messages (guild_id, id desc);

-- The rate limit's own probe: "when did this player last post". Indexed so the check in
-- guild_post_message is one index lookup rather than a scan of the guild's whole history.
create index if not exists guild_messages_author_idx
  on public.guild_messages (user_id, created_at desc);

-- ---------------------------------------------------------------- the four questions
--
-- Every policy below is written in terms of these, and each answers exactly one question about
-- the CALLER. auth.uid() is read inside them rather than passed in, so there is no argument by
-- which a caller can ask a question about somebody who is not themselves.
--
-- SECURITY DEFINER for the same reason is_friend is: a policy on one table that reads a second
-- table through that table's own policies is the shape that produces infinite recursion later,
-- when one of the two grows a clause. guild_members' own SELECT policy calls my_guild(), which
-- reads guild_members - which is precisely the loop this avoids rather than a loop it creates.
--
-- STABLE so the planner may call them once per statement instead of once per row.

create or replace function public.my_guild()
returns uuid
language sql
stable
security definer
set search_path = public, pg_temp
as $$
  select guild_id from public.guild_members where user_id = auth.uid();
$$;

create or replace function public.my_guild_role()
returns text
language sql
stable
security definer
set search_path = public, pg_temp
as $$
  select role from public.guild_members where user_id = auth.uid();
$$;

-- True when the caller and the named user are in the same guild. True for the caller themselves,
-- which costs nothing - every policy that uses it already permits the caller's own row by a
-- separate clause - and keeps the function a plain statement about two rows.
create or replace function public.is_guildmate(other_user uuid)
returns boolean
language sql
stable
security definer
set search_path = public, pg_temp
as $$
  select exists (
    select 1
      from public.guild_members me
      join public.guild_members them on them.guild_id = me.guild_id
     where me.user_id = auth.uid()
       and them.user_id = other_user
  );
$$;

-- Whether the caller has a request out to this guild. The one thing that lets an applicant read
-- the guild they applied to while they wait, without making guilds readable to anybody who has
-- not been given its code.
create or replace function public.has_guild_request(p_guild uuid)
returns boolean
language sql
stable
security definer
set search_path = public, pg_temp
as $$
  select exists (
    select 1 from public.guild_requests
     where guild_id = p_guild and user_id = auth.uid()
  );
$$;

-- The cap, in one place. Repeated in the client as a display constant and deliberately not read
-- from here at runtime - the same duplication FriendCode.Alphabet is under, and for the same
-- reason: this is what makes it true, and the client's copy is what tells a player before a
-- round trip. Neither can be the other's source.
create or replace function public.guild_member_cap()
returns integer
language sql
immutable
as $$ select 30; $$;

revoke all on function public.my_guild() from public, anon;
revoke all on function public.my_guild_role() from public, anon;
revoke all on function public.is_guildmate(uuid) from public, anon;
revoke all on function public.has_guild_request(uuid) from public, anon;
revoke all on function public.guild_member_cap() from public, anon;
grant execute on function public.my_guild() to authenticated;
grant execute on function public.my_guild_role() to authenticated;
grant execute on function public.is_guildmate(uuid) to authenticated;
grant execute on function public.has_guild_request(uuid) to authenticated;
grant execute on function public.guild_member_cap() to authenticated;

-- ---------------------------------------------------------------- succession
--
-- A guild whose leader's account is deleted would otherwise have no leader and no way to get
-- one: promoting needs a leader, and every member's own leave is permitted while disband is not.
-- Nothing in the game can delete an account - that needs the service role, which this project
-- does not hold - so the only way to reach it is the dashboard, which is exactly the kind of
-- rare, unattended case that must not leave a guild permanently stuck.
--
-- Longest-serving officer, then longest-serving member. Deliberately not "whoever was last seen":
-- succession should be predictable from the roster a player is looking at.
--
-- The existence guard is what makes this safe during a DISBAND. Deleting the guild cascades to
-- its members and fires this trigger for each of them; by then the parent row is already gone in
-- this transaction, so the function returns without trying to promote somebody into a guild that
-- no longer exists.
create or replace function public.guild_succeed_leader()
returns trigger
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  heir uuid;
begin
  if old.role <> 'leader' then return old; end if;
  if not exists (select 1 from public.guilds where id = old.guild_id) then return old; end if;

  select user_id into heir
    from public.guild_members
   where guild_id = old.guild_id
   order by (role = 'officer') desc, joined_at asc
   limit 1;

  if heir is not null then
    update public.guild_members set role = 'leader' where user_id = heir;
  end if;
  return old;
end;
$$;

drop trigger if exists guild_members_succession on public.guild_members;
create trigger guild_members_succession
  after delete on public.guild_members
  for each row execute function public.guild_succeed_leader();

-- ---------------------------------------------------------------- privileges
--
-- Two locks, as everywhere in these migrations. RLS below decides which ROWS; these decide which
-- verbs and columns exist at all.
--
-- No INSERT anywhere. Creating a guild, joining one and posting a message each carry a rule that
-- spans rows - the cap, the one-guild key, the rate limit - so each is an RPC in 0005 and none of
-- them can be reached by a POST aimed straight at PostgREST.
--
-- No UPDATE on guild_members. Roles move through guild_set_role and guild_transfer_leadership,
-- and last_seen through guild_touch, so there is no statement a client can send that rewrites its
-- own role or backdates its own presence.
--
-- guilds gets UPDATE on two columns only. `name` and `guild_code` are not grantable, which is
-- what stops a leader renaming a guild out from under its members or choosing a code - the same
-- second lock profiles.friend_code is behind.
alter table public.guilds         enable row level security;
alter table public.guild_members  enable row level security;
alter table public.guild_requests enable row level security;
alter table public.guild_messages enable row level security;

revoke all on public.guilds         from anon, authenticated;
revoke all on public.guild_members  from anon, authenticated;
revoke all on public.guild_requests from anon, authenticated;
revoke all on public.guild_messages from anon, authenticated;

grant select on public.guilds to authenticated;
grant update (description, is_open) on public.guilds to authenticated;
grant delete on public.guilds to authenticated;

grant select, delete on public.guild_members to authenticated;
grant select, delete on public.guild_requests to authenticated;
grant select on public.guild_messages to authenticated;

-- ---------------------------------------------------------------- policies: guilds
--
-- A GUILD IS NOT READABLE BY EVERY SIGNED-IN USER, and that is the one place this file is tighter
-- than 0001. profiles are readable by anybody signed in - 0001's own note calls it the widest rule
-- in these migrations and says the tightening, when there is a population to protect, is an
-- exact-match lookup function. A guild is that population: a table anybody can page is a list of
-- every guild code in the project, and with auto-accept on, a list of codes is a list of guilds
-- anyone can walk into. So the policy is caller-keyed on both sides, and finding a guild you are
-- not in goes through guild_by_code() in 0005, which resolves one code and cannot enumerate.
drop policy if exists guilds_read_member_or_applicant on public.guilds;
create policy guilds_read_member_or_applicant
  on public.guilds for select
  to authenticated
  using (id = public.my_guild() or public.has_guild_request(id));

drop policy if exists guilds_update_by_leader on public.guilds;
create policy guilds_update_by_leader
  on public.guilds for update
  to authenticated
  using (id = public.my_guild() and public.my_guild_role() = 'leader')
  with check (id = public.my_guild() and public.my_guild_role() = 'leader');

-- Disband. A plain delete rather than an RPC, because the whole rule fits in the USING clause and
-- the cascades do the rest: members, requests and messages go with the guild.
drop policy if exists guilds_disband_by_leader on public.guilds;
create policy guilds_disband_by_leader
  on public.guilds for delete
  to authenticated
  using (id = public.my_guild() and public.my_guild_role() = 'leader');

-- ---------------------------------------------------------------- policies: membership

-- Only members read their guild's roster. A non-member gets zero rows rather than an error,
-- which is what RLS filtering looks like - so "there is nobody here" and "this is not your
-- guild" arrive identically and the client has to say both.
drop policy if exists guild_members_read_own_guild on public.guild_members;
create policy guild_members_read_own_guild
  on public.guild_members for select
  to authenticated
  using (guild_id = public.my_guild());

-- LEAVING. Anyone may delete their own membership - except a leader, and that exception is the
-- whole of "a leader must transfer or disband first". It is not a message the client shows and a
-- server that shrugs; the row will not move.
drop policy if exists guild_members_leave_self on public.guild_members;
create policy guild_members_leave_self
  on public.guild_members for delete
  to authenticated
  using (user_id = auth.uid() and role <> 'leader');

-- REMOVING SOMEBODY ELSE. A second permissive policy rather than a longer USING on the first,
-- because permissive policies are OR'd and two of them read as the two different things they are.
-- The matrix: a leader may remove an officer or a member, an officer may remove a member, and
-- nobody may remove a leader - including the leader, whose own exit is a transfer or a disband.
drop policy if exists guild_members_remove_by_officer on public.guild_members;
create policy guild_members_remove_by_officer
  on public.guild_members for delete
  to authenticated
  using (
    guild_id = public.my_guild()
    and (
      (public.my_guild_role() = 'leader' and role in ('officer', 'member'))
      or (public.my_guild_role() = 'officer' and role = 'member')
    )
  );

-- ---------------------------------------------------------------- policies: requests

-- The applicant sees their own; a leader or an officer sees the ones addressed to their guild.
-- Nobody else sees that a request exists at all.
drop policy if exists guild_requests_read_own_or_officer on public.guild_requests;
create policy guild_requests_read_own_or_officer
  on public.guild_requests for select
  to authenticated
  using (
    user_id = auth.uid()
    or (guild_id = public.my_guild() and public.my_guild_role() in ('leader', 'officer'))
  );

-- WITHDRAW AND DECLINE ARE THE SAME DELETE. Accepting is the RPC, because accepting is the one
-- that has to count the roster and insert a member under a lock; refusing is just a row going
-- away, whichever side lets it go.
drop policy if exists guild_requests_remove_own_or_officer on public.guild_requests;
create policy guild_requests_remove_own_or_officer
  on public.guild_requests for delete
  to authenticated
  using (
    user_id = auth.uid()
    or (guild_id = public.my_guild() and public.my_guild_role() in ('leader', 'officer'))
  );

-- ---------------------------------------------------------------- policies: chat

-- Only members read the chat, and there is no write policy at all: guild_post_message holds the
-- rate limit and is the only way a row gets in, exactly as push_save is the only way a save does.
-- There is no UPDATE and no DELETE either, for anybody. Editing and moderation are real features
-- with real rules and neither is in phase one; a message is a record until one of them exists.
drop policy if exists guild_messages_read_own_guild on public.guild_messages;
create policy guild_messages_read_own_guild
  on public.guild_messages for select
  to authenticated
  using (guild_id = public.my_guild());
