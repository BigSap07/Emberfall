-- 0016_account_deletion.sql - a player can delete their own account from inside the game.
--
-- WRITTEN, NOT APPLIED. Run after 0001 to 0006. It does not care whether 0007 to 0015 are applied:
-- every table those files add is keyed to profiles or guilds with ON DELETE CASCADE (or SET NULL),
-- so deleting the profile takes their rows with it whether or not they exist.
--
-- RE-APPLIABLE: every function is `create or replace`, and the grants are restated.
--
-- ---------------------------------------------------------------- why this exists
--
-- Google Play requires an app that lets a player create an account to also let them delete it,
-- from inside the app and from a web page. An anonymous Supabase sign-in is an account for that
-- rule. Until this file, the only way to delete one was the dashboard (0004's succession note says
-- so), because deleting an auth.users row needs more than the client's role holds.
--
-- ---------------------------------------------------------------- the shape of the decision
--
-- ONE INTERNAL FUNCTION, TWO DOORS. `account_delete_internal(uid)` does the work and is granted to
-- no client role. `delete_my_account(p_confirm)` is the client's door: it reads auth.uid() itself,
-- so there is no argument by which a caller can name somebody else. `admin_delete_account(p_user)`
-- is the operator's door for a deletion request that arrives by web form or email; it is granted to
-- no client role either and is run from the SQL editor.
--
-- THE CONFIRMATION IS AN ARGUMENT, NOT A FLAG IN THE CLIENT. `p_confirm` must be the literal text
-- DELETE. A call made by mistake (a retried request, a mistyped RPC name in a tool) is refused
-- with PT400 rather than ending an account.
--
-- EXPLICIT DELETES FIRST, THEN THE CASCADE. The rows the brief names (guild membership, friendships,
-- team snapshots, the save, the profile) are deleted by name so the function says what it deletes.
-- Everything else keyed to the player (chat messages, guild dungeon attempts, the Guild Path ledger
-- and unlocks, guild tech rungs, the code lookup tally, blocks and mutes from 0017) goes by the
-- cascade from profiles. Purchase receipts (0019) reference auth.users with ON DELETE SET NULL, so
-- the order record survives without the player attached; see that file for why.
--
-- A LEADER WHO IS THE LAST MEMBER DISBANDS THE GUILD. Otherwise the guild would be left with no
-- members and no leader, readable by nobody and unreachable forever. A leader with other members
-- leaves through 0004's succession trigger, which promotes the longest-serving officer, then the
-- longest-serving member.
--
-- LOCK ORDER IS GUILD, THEN MEMBERSHIP, the same order guild_request_send and guild_request_accept
-- take them, so a deletion racing a join queues instead of deadlocking.
--
-- THE AUTH ROW GOES LAST. Deleting from auth.users also removes the user's identities, sessions and
-- refresh tokens (Supabase's own cascades). The access token the client holds stays signature-valid
-- until it expires, but it now names a user with no rows anywhere, and the refresh token is gone,
-- so the client cannot get another. The client must drop its local cloud state after a success.
--
-- WHAT THIS DOES NOT DELETE, said here so the privacy policy can say it too:
--   * play_events (0015). Those rows carry a random install id that is never linked to an account,
--     so there is nothing to find them by. The client wipes the install id when stats are turned off.
--   * Database backups. Supabase keeps point-in-time backups for the plan's retention window; a
--     deleted account disappears from them when they age out.
--   * Chat reports ABOUT this player's messages (0017). The reported user id is set to NULL and the
--     body snapshot is kept for moderation until the operator's retention purge removes it.
--   * Purchase receipts (0019), kept without the user id for accounting.
--
-- SIDE EFFECT ON A GUILD, deliberate and worth knowing: guild XP in 0012 is summed from
-- guild_path_ledger, and the cascade deletes this player's ledger rows. A guild whose member deletes
-- their account loses the XP that member earned. Keeping it would mean keeping a row keyed to a
-- deleted person. If the owner prefers the guild keep its level, the change is to make the ledger's
-- user_id ON DELETE SET NULL in a later migration and have guild_tech_xp count rows with no user.

-- ---------------------------------------------------------------- the work

create or replace function public.account_delete_internal(p_user uuid)
returns jsonb
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  member_guild uuid;
  member_role  text;
  others       integer := 0;
  disbanded    boolean := false;
  had_profile  boolean;
begin
  if p_user is null then
    raise exception 'account_delete_internal needs a user id' using errcode = 'PT400';
  end if;

  select exists (select 1 from public.profiles where user_id = p_user) into had_profile;

  -- Guild first, so the lock order matches the guild calls in 0005.
  select guild_id into member_guild from public.guild_members where user_id = p_user;
  if member_guild is not null then
    perform 1 from public.guilds where id = member_guild for update;
    select role into member_role from public.guild_members
     where user_id = p_user and guild_id = member_guild for update;

    if member_role = 'leader' then
      select count(*) into others from public.guild_members
       where guild_id = member_guild and user_id <> p_user;
      if others = 0 then
        -- Cascades to members, requests, messages, dungeon attempts, tech levels and picks.
        delete from public.guilds where id = member_guild;
        disbanded := true;
      end if;
    end if;

    -- The succession trigger (0004) promotes an heir when this row was the leader's.
    delete from public.guild_members where user_id = p_user;
  end if;

  delete from public.guild_requests where user_id = p_user;
  delete from public.friendships   where p_user in (requester, addressee);
  delete from public.team_snapshots where user_id = p_user;
  delete from public.saves          where user_id = p_user;
  -- Everything else keyed to profiles goes with this row (see the header).
  delete from public.profiles       where user_id = p_user;
  -- Identities, sessions and refresh tokens go with this row (Supabase's auth cascades).
  delete from auth.users            where id = p_user;

  return jsonb_build_object(
    'deleted', had_profile,
    'guild_disbanded', disbanded
  );
end;
$$;

-- No client role may call the internal function, ever.
revoke all on function public.account_delete_internal(uuid) from public, anon, authenticated;

-- ---------------------------------------------------------------- the client's door
--
-- Answers {"deleted": true, "guild_disbanded": <bool>}. "deleted" is false when there was no
-- profile to delete, which is what a second call with a still-valid access token sees: the first
-- call already removed everything, and the second finds nothing and says so rather than failing.
create or replace function public.delete_my_account(p_confirm text)
returns jsonb
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid uuid := auth.uid();
begin
  if uid is null then
    raise exception 'delete_my_account requires a signed-in user' using errcode = 'PT401';
  end if;
  if p_confirm is distinct from 'DELETE' then
    raise exception 'delete_my_account needs p_confirm to be the text DELETE' using errcode = 'PT400';
  end if;
  return public.account_delete_internal(uid);
end;
$$;

revoke all on function public.delete_my_account(text) from public, anon;
grant execute on function public.delete_my_account(text) to authenticated;

-- ---------------------------------------------------------------- the operator's door
--
-- For a deletion request that arrives outside the game (the web form Google Play requires). The
-- operator finds the account by the friend code the player reads off their Friends screen:
--
--     select user_id, display_name from public.profiles where friend_code = 'ABC234';
--     select public.admin_delete_account('<user_id from above>');
--
-- Granted to no client role. The SQL editor runs as the project owner, which is the only caller.
create or replace function public.admin_delete_account(p_user uuid)
returns jsonb
language plpgsql
security definer
set search_path = public, pg_temp
as $$
begin
  return public.account_delete_internal(p_user);
end;
$$;

revoke all on function public.admin_delete_account(uuid) from public, anon, authenticated;

-- ---------------------------------------------------------------- what was applied, said once
--
-- The deletion of the auth row needs the function owner to hold DELETE on auth.users. On a standard
-- Supabase project the postgres role does (the rule tests' cleanup statement relies on it). If a
-- project ever revokes it, this stops the migration HERE, at apply time, instead of letting every
-- player's deletion fail later with an error they cannot do anything about.
do $$
declare
  who text;
begin
  if not has_table_privilege('auth.users', 'delete') then
    raise exception '0016: the role applying this (%) cannot delete from auth.users, so delete_my_account could not finish. Apply as the project owner.', current_user;
  end if;
  foreach who in array array['anon', 'authenticated'] loop
    if has_function_privilege(who, 'public.account_delete_internal(uuid)', 'execute') then
      raise exception '0016: % can execute account_delete_internal', who;
    end if;
    if has_function_privilege(who, 'public.admin_delete_account(uuid)', 'execute') then
      raise exception '0016: % can execute admin_delete_account', who;
    end if;
  end loop;
  if has_function_privilege('anon', 'public.delete_my_account(text)', 'execute') then
    raise exception '0016: anon can execute delete_my_account';
  end if;
end;
$$;
