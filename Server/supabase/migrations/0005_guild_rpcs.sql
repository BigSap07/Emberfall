-- 0005_guild_rpcs.sql - the guild calls whose rule spans more than one row.
--
-- Run after 0004.
--
-- ---------------------------------------------------------------- why these are functions
--
-- 0004 left four tables with no INSERT grant and no UPDATE grant on membership. Everything that
-- would have used one is here, and the test for whether a rule belongs in this file rather than
-- in a policy is the same test `push_save` passed: a policy can only look at the row in front of
-- it, so a rule that has to COUNT rows, or write two of them, or compare against a row that is
-- not the one being written, cannot be a policy. Creating a guild writes two tables. Joining one
-- counts the roster first. Posting a message reads the poster's last message. Transferring
-- leadership moves two rows in one step. None of those is expressible as a WITH CHECK, and a
-- client that could reach the tables directly could do all four wrongly.
--
-- Every function reads auth.uid() itself and scopes every statement to it, so SECURITY DEFINER
-- buys the rule and nothing else: there is no argument by which a caller names somebody else's
-- membership and has it honoured.
--
-- ---------------------------------------------------------------- the lock, stated once
--
-- THE 30 CAP IS HELD BY `select ... for update` ON THE GUILD ROW, not by the count that follows
-- it. Two officers accepting two different applicants at the same instant both read a roster of
-- 29 and both insert, and the guild ends up at 31 - unless they queue behind each other first.
-- Taking the guild's own row exclusively is what makes them queue: the second transaction blocks
-- until the first commits, and because each statement in READ COMMITTED takes a fresh snapshot,
-- the count it then runs SEES the member the first one added. The guild row is not otherwise
-- touched by either of them; it is being used as the thing they both have to agree about.
--
-- The one-guild rule needs no lock and gets none. `guild_members.user_id` is the primary key, so
-- two guilds accepting the SAME applicant simultaneously - which lock different guild rows and do
-- not queue - end with one insert and one unique violation, whatever the timing. The explicit
-- check below it exists to produce a sentence, not to produce the guarantee.
--
-- ---------------------------------------------------------------- errors
--
-- PostgREST's PT-prefixed SQLSTATEs, where the last three characters are the HTTP status it
-- returns - the convention 0002 introduced so a refusal arrives as a status the client can branch
-- on instead of a 500 with a message to parse. PT400 bad input, PT401 not signed in, PT403 not
-- your role, PT404 no such thing, PT409 it will not fit or it is taken, PT429 too fast.

-- ---------------------------------------------------------------- reading a typed code

-- The client half of this is FriendCode.Normalise, and the two agree deliberately rather than by
-- derivation: upper-case, drop spaces, dashes and underscores, and refuse anything else. Nothing
-- is repaired beyond that - an O is not read as a 0 - because those letters are absent from the
-- alphabet precisely because they are ambiguous, and a silent substitution turns a typo into a
-- successful lookup of somebody else's guild.
--
-- Returns NULL rather than raising for a code that cannot be one, so `where guild_code = ...`
-- simply matches nothing: a malformed code resolves to no guild, which is the same answer a
-- well-formed code nobody holds gets.
create or replace function public.guild_normalise_code(p_code text)
returns text
language sql
immutable
as $$
  select case
           when upper(translate(coalesce(p_code, ''), ' -_', '')) ~ '^[2-9A-HJKMNP-TV-Z]{6}$'
             then upper(translate(coalesce(p_code, ''), ' -_', ''))
           else null
         end;
$$;

-- ---------------------------------------------------------------- finding a guild

-- THE ONLY WAY TO SEE A GUILD YOU ARE NOT IN. 0004's read policy is keyed to the caller on both
-- sides, so this function is what replaces the table scan a friend-code lookup gets to use - and
-- it is a function rather than a policy precisely because a function cannot be asked for the
-- second row. One exact code in, at most one guild out; there is no argument that widens it.
--
-- member_count travels with it because the one thing a player decides on this screen is whether
-- to apply, and "29 of 30" is most of that decision.
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
language sql
stable
security definer
set search_path = public, pg_temp
as $$
  select g.id, g.name, g.description, g.guild_code, g.is_open,
         (select count(*)::integer from public.guild_members m where m.guild_id = g.id),
         g.created_at
    from public.guilds g
   where g.guild_code = public.guild_normalise_code(p_code);
$$;

-- ---------------------------------------------------------------- creating one

-- Two tables in one call, and that is the reason it is a call. A guild with no leader is not a
-- state this schema should be able to hold for an instant, so the guild row and the leader's
-- membership are written in one transaction or neither is.
--
-- The name check before the loop is for the message; `guilds_name_unique` is for the guarantee.
-- The two are not redundant - the first tells a player "that name is taken" where the second can
-- only refuse - and the handler inside the loop re-asks the same question because the answer can
-- change between them.
--
-- The retry loop is 0001's, for 0001's reason: 30^6 is 729 million codes, so a collision is a
-- formality rather than a hot path, and ten attempts is not a real limit. If it is ever exhausted
-- the exception is re-raised, because a guild created with no readable code would be a guild
-- nobody can be invited to.
create or replace function public.guild_create(p_name text, p_description text)
returns public.guilds
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid     uuid := auth.uid();
  trimmed text := btrim(coalesce(p_name, ''));
  blurb   text := btrim(coalesce(p_description, ''));
  attempt integer := 0;
  result  public.guilds;
begin
  if uid is null then
    raise exception 'guild_create requires a signed-in user' using errcode = 'PT401';
  end if;
  if char_length(trimmed) < 3 or char_length(trimmed) > 20 then
    raise exception 'a guild name is 3 to 20 characters' using errcode = 'PT400';
  end if;
  if trimmed ~ '[[:cntrl:]]' or blurb ~ '[[:cntrl:]]' then
    raise exception 'a guild name and description are each a single line' using errcode = 'PT400';
  end if;
  if char_length(blurb) > 140 then
    raise exception 'a guild description is at most 140 characters' using errcode = 'PT400';
  end if;
  if public.my_guild() is not null then
    raise exception 'you are already in a guild' using errcode = 'PT409';
  end if;
  if exists (select 1 from public.guilds where lower(name) = lower(trimmed)) then
    raise exception 'a guild called % already exists', trimmed using errcode = 'PT409';
  end if;

  loop
    attempt := attempt + 1;
    begin
      insert into public.guilds (name, description, guild_code)
      values (trimmed, blurb, public.generate_friend_code())
      returning * into result;
      exit;
    exception when unique_violation then
      -- Two uniques can land here and they mean different things: the name, which somebody else
      -- took between the check above and this statement, and the code, which is a collision to
      -- retry. Asking which is the only way to tell them apart from inside the handler.
      if exists (select 1 from public.guilds where lower(name) = lower(trimmed)) then
        raise exception 'a guild called % already exists', trimmed using errcode = 'PT409';
      end if;
      if attempt >= 10 then raise; end if;
    end;
  end loop;

  insert into public.guild_members (user_id, guild_id, role)
  values (uid, result.id, 'leader');

  return result;
end;
$$;

-- ---------------------------------------------------------------- asking to join

-- Takes the CODE rather than the id, and that is the access rule rather than a convenience: the
-- code is the only handle on a guild a non-member can hold, so a guild id that leaks through some
-- future join still buys nothing. The client resolves the code with guild_by_code first so the
-- player can read the guild's name before applying - two round trips, for the same reason the
-- friends screen takes two: it is what lets "no guild has that code" and "that guild is full" be
-- different sentences.
--
-- Returns what happened, in one word. An open guild admits the caller immediately and answers
-- 'joined'; a closed one queues a request and answers 'requested'. One call either way, because
-- from the player's side there is one button and its outcome is the guild's setting, not theirs.
--
-- Applying twice is not an error. `on conflict do nothing` makes a second tap the same answer as
-- the first rather than a duplicate-key failure the player has to interpret.
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

-- ---------------------------------------------------------------- accepting one

-- Declining is a DELETE through 0004's policy; only accepting is here, because only accepting has
-- to count the roster and write a second table. The pair is deliberately asymmetric for the same
-- reason friendships has a Remove and no Decline: a refusal is a row going away, and dressing it
-- up as a call would imply it had its own rules.
--
-- `is distinct from` rather than `<>`, and coalesce on the role, because my_guild() returns NULL
-- for a caller in no guild at all - and `null <> x` is null, which an IF does not take. Written
-- with plain inequality this check would wave through exactly the caller it exists to refuse.
create or replace function public.guild_request_accept(p_request_id uuid)
returns public.guild_members
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid     uuid := auth.uid();
  req     public.guild_requests;
  members integer;
  result  public.guild_members;
begin
  if uid is null then
    raise exception 'guild_request_accept requires a signed-in user' using errcode = 'PT401';
  end if;

  select * into req from public.guild_requests where id = p_request_id;
  if not found then
    raise exception 'that request no longer exists' using errcode = 'PT404';
  end if;

  if public.my_guild() is distinct from req.guild_id
     or coalesce(public.my_guild_role(), '') not in ('leader', 'officer') then
    raise exception 'only a leader or an officer of that guild can accept a request'
      using errcode = 'PT403';
  end if;

  -- The lock. See the note at the top of this file for what it is holding and why a count on its
  -- own would not.
  perform 1 from public.guilds where id = req.guild_id for update;

  select count(*) into members from public.guild_members where guild_id = req.guild_id;
  if members >= public.guild_member_cap() then
    raise exception 'your guild is full (% members)', public.guild_member_cap()
      using errcode = 'PT409';
  end if;

  -- The message. The guarantee is guild_members' primary key, which refuses the second insert
  -- when two DIFFERENT guilds accept this applicant at the same instant - they lock different
  -- guild rows, so nothing makes them queue and nothing needs to.
  if exists (select 1 from public.guild_members where user_id = req.user_id) then
    raise exception 'that player has already joined a guild' using errcode = 'PT409';
  end if;

  insert into public.guild_members (user_id, guild_id, role)
  values (req.user_id, req.guild_id, 'member')
  returning * into result;

  delete from public.guild_requests where user_id = req.user_id;
  return result;
end;
$$;

-- ---------------------------------------------------------------- roles

-- Promote and demote are one call, because they are one field. 'leader' is deliberately not
-- accepted: leadership is a transfer, which moves two rows, and letting it be set here would
-- produce a guild with two leaders or - once the partial index refused that - a confusing failure
-- where a role change should have been.
create or replace function public.guild_set_role(p_user uuid, p_role text)
returns public.guild_members
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid    uuid := auth.uid();
  mine   uuid := public.my_guild();
  result public.guild_members;
begin
  if uid is null then
    raise exception 'guild_set_role requires a signed-in user' using errcode = 'PT401';
  end if;
  if mine is null or coalesce(public.my_guild_role(), '') <> 'leader' then
    raise exception 'only the leader can change a role' using errcode = 'PT403';
  end if;
  if p_role is null or p_role not in ('officer', 'member') then
    raise exception 'a role is officer or member - leadership moves through guild_transfer_leadership'
      using errcode = 'PT400';
  end if;
  if p_user = uid then
    raise exception 'the leader cannot change their own role' using errcode = 'PT400';
  end if;

  update public.guild_members
     set role = p_role
   where user_id = p_user and guild_id = mine and role <> 'leader'
  returning * into result;

  if not found then
    raise exception 'that player is not a member of your guild' using errcode = 'PT404';
  end if;
  return result;
end;
$$;

-- DEMOTE BEFORE PROMOTE, and the order is load-bearing. `guild_members_one_leader` is a partial
-- unique INDEX, which is checked at the end of each statement and cannot be deferred to commit
-- the way a constraint can - so promoting first fails on the index with two rows claiming the
-- role. Demoting first leaves the guild momentarily without a leader, which is invisible outside
-- this transaction and is over by the time anybody can read it.
--
-- The outgoing leader becomes an officer rather than a member. They keep the ability to answer
-- requests, which is what somebody who has just handed over is most likely to be in the middle of.
create or replace function public.guild_transfer_leadership(p_user uuid)
returns public.guild_members
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid    uuid := auth.uid();
  mine   uuid := public.my_guild();
  result public.guild_members;
begin
  if uid is null then
    raise exception 'guild_transfer_leadership requires a signed-in user' using errcode = 'PT401';
  end if;
  if mine is null or coalesce(public.my_guild_role(), '') <> 'leader' then
    raise exception 'only the leader can hand leadership over' using errcode = 'PT403';
  end if;
  if p_user = uid then
    raise exception 'you are already the leader' using errcode = 'PT400';
  end if;
  if not exists (select 1 from public.guild_members where user_id = p_user and guild_id = mine) then
    raise exception 'that player is not a member of your guild' using errcode = 'PT404';
  end if;

  update public.guild_members set role = 'officer' where user_id = uid  and guild_id = mine;
  update public.guild_members set role = 'leader'  where user_id = p_user and guild_id = mine
  returning * into result;

  return result;
end;
$$;

-- ---------------------------------------------------------------- chat

-- THE RATE LIMIT IS WHY THIS IS A FUNCTION AND WHY guild_messages HAS NO INSERT GRANT. A limit
-- the client is asked to keep is a limit the next client does not keep; this one is a fact about
-- privileges, exactly as push_save's revision check is.
--
-- The poster's own membership row is taken FOR UPDATE before the last-message time is read.
-- Without it, two posts from one account in the same instant both read the same "last posted"
-- value and both pass - the limit would hold in practice and not in fact. Locking the row that
-- identifies the poster is the smallest thing that makes them queue, and it is a row this
-- function has to read anyway to learn which guild the message belongs to.
--
-- Two seconds. Slow enough that a person typing cannot hit it and fast enough that a conversation
-- does not feel governed; it is a floor under a script, not a throttle on a player.
create or replace function public.guild_post_message(p_body text)
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
    raise exception 'guild_post_message requires a signed-in user' using errcode = 'PT401';
  end if;

  select guild_id into mine from public.guild_members where user_id = uid for update;
  if not found then
    raise exception 'you are not in a guild' using errcode = 'PT403';
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

  insert into public.guild_messages (guild_id, user_id, body) values (mine, uid, line)
  returning * into result;

  -- Posting is presence. Stamped here as well as in guild_touch so a player who is talking never
  -- reads as away, whatever the client's polling happens to be doing.
  update public.guild_members set last_seen = now() where user_id = uid;
  return result;
end;
$$;

-- ---------------------------------------------------------------- presence

-- "Last seen" has to mean something this game can actually observe, and the only such thing is
-- when a player's device last reached the server. That is what this records. It is not a login
-- time - nothing here has a session - and it is not "last played", because a device with no
-- signal plays exactly as well as one with.
--
-- Silent for a caller who is in no guild: it updates nothing and answers NULL. The client calls
-- this on a timer, and a player who has just left their guild should not be handed an error by a
-- timer.
create or replace function public.guild_touch()
returns timestamptz
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid     uuid := auth.uid();
  stamped timestamptz;
begin
  if uid is null then return null; end if;
  update public.guild_members set last_seen = now()
   where user_id = uid
  returning last_seen into stamped;
  return stamped;
end;
$$;

-- ---------------------------------------------------------------- privileges

revoke all on function public.guild_normalise_code(text) from public, anon;
revoke all on function public.guild_by_code(text) from public, anon;
revoke all on function public.guild_create(text, text) from public, anon;
revoke all on function public.guild_request_send(text) from public, anon;
revoke all on function public.guild_request_accept(uuid) from public, anon;
revoke all on function public.guild_set_role(uuid, text) from public, anon;
revoke all on function public.guild_transfer_leadership(uuid) from public, anon;
revoke all on function public.guild_post_message(text) from public, anon;
revoke all on function public.guild_touch() from public, anon;

grant execute on function public.guild_normalise_code(text) to authenticated;
grant execute on function public.guild_by_code(text) to authenticated;
grant execute on function public.guild_create(text, text) to authenticated;
grant execute on function public.guild_request_send(text) to authenticated;
grant execute on function public.guild_request_accept(uuid) to authenticated;
grant execute on function public.guild_set_role(uuid, text) to authenticated;
grant execute on function public.guild_transfer_leadership(uuid) to authenticated;
grant execute on function public.guild_post_message(text) to authenticated;
grant execute on function public.guild_touch() to authenticated;
