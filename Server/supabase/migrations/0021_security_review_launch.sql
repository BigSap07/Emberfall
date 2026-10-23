-- 0021_security_review_launch.sql - the fixes from the security review of 0016 to 0019 and the two
-- receipt Edge Functions. Docs/SECURITY-REVIEW-SUPABASE.md is the review.
--
-- WRITTEN, NOT APPLIED to any project. Run after 0019 AND after 0020. It reads user_blocks (0017),
-- purchase_receipts (0019) and friendships (0003), and its friend request rule sits beside 0020's
-- throttle on the same table.
--
-- RE-APPLIABLE: tables are `if not exists`, the limits row is inserted `on conflict do nothing`,
-- functions are `create or replace`, triggers are dropped before they are created.
--
-- ---------------------------------------------------------------- what is fixed here
--
--   L2  verify-google-purchase has no rate limit of its own. Every call that passes the request shape
--       costs one Google Play Developer API request, and anonymous sign-up mints accounts for free, so
--       a script can spend the project's API quota with made-up tokens until real purchases stop
--       verifying. This adds the server half of a per-account limit: a tally, a limits row and
--       purchase_verify_allow(user), callable by the service role only. The Edge Function calls it
--       right after it knows the user and before it asks Google (the review gives the three lines).
--   L5  A block (0017) hid chat lines and nothing else. A blocked player could keep sending the
--       blocker friend requests, and delete and re-send them. Now a request to a player who has
--       blocked the sender is refused, and blocking someone ends every friendship row between the
--       two: a pending request in either direction and an accepted friendship alike (the owner's
--       decision of 2026-10-23, review decision 4).
--   L6  0017's trigger function guild_messages_refuse_muted is revoked from every client role, for
--       0020's R5 reason.
--
-- NOT fixed here, because none of them is a database change (the review has each one):
--   L1  the auto-refund path (consumables were acknowledged only by the client), an Edge Function change,
--       now in verify-google-purchase/index.ts (the server acknowledges every verified purchase);
--   L3  restoring a one-time purchase after a reinstall or an account deletion, an owner decision;
--   L4  a refund that lands before the purchase was verified, a polling backstop.

-- ---------------------------------------------------------------- L2: verification, at a bounded rate
--
-- One limits row. 20 verifications per account per 10 minutes: a purchase is one call, a restore is
-- one call per restored token (two one-time offers at most, plus whatever consumables are still
-- pending), and a retry after an error is one more. No honest player comes near it.
--
--     update public.purchase_verify_limits set verify_limit = 40, updated_at = now();
create table if not exists public.purchase_verify_limits (
  id                    boolean primary key default true check (id),
  verify_limit          integer not null check (verify_limit >= 1),
  verify_window_seconds integer not null check (verify_window_seconds between 60 and 86400),
  updated_at            timestamptz not null default now()
);

alter table public.purchase_verify_limits enable row level security;
revoke all on public.purchase_verify_limits from anon, authenticated;

insert into public.purchase_verify_limits (id, verify_limit, verify_window_seconds)
values (true, 20, 600)
on conflict (id) do nothing;

-- One row per account that has ever asked for a verification. Keyed to auth.users like the receipts
-- (0019), and cascaded rather than set null: a tally is not a sales record and has no reason to outlive
-- the account.
create table if not exists public.purchase_verify_tally (
  user_id      uuid primary key references auth.users (id) on delete cascade,
  window_start timestamptz not null default now(),
  attempts     integer not null default 0 check (attempts >= 0)
);

alter table public.purchase_verify_tally enable row level security;
revoke all on public.purchase_verify_tally from anon, authenticated;

-- Counts one verification attempt for the user and answers whether it may go ahead:
--   {"allowed": true}                          go on and ask Google
--   {"allowed": false, "retry_after": seconds} answer 429 and do not ask Google
-- Counted BEFORE Google is asked, whatever Google then says, because the cost being limited is the call
-- to Google. The tally row is locked FOR UPDATE so concurrent calls from one account count exactly.
create or replace function public.purchase_verify_allow(p_user_id uuid)
returns jsonb
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  lim   integer;
  win   interval;
  tally public.purchase_verify_tally;
begin
  if p_user_id is null then
    raise exception 'purchase_verify_allow needs a user id' using errcode = 'PT400';
  end if;
  select l.verify_limit, make_interval(secs => l.verify_window_seconds) into lim, win
    from public.purchase_verify_limits l where l.id;
  if lim is null or win is null then
    raise exception 'purchase_verify_limits has no row - see 0021' using errcode = 'PT500';
  end if;

  insert into public.purchase_verify_tally as t (user_id) values (p_user_id)
  on conflict (user_id) do nothing;
  select * into tally from public.purchase_verify_tally t where t.user_id = p_user_id for update;

  if now() - tally.window_start >= win then
    update public.purchase_verify_tally t set window_start = now(), attempts = 1 where t.user_id = p_user_id;
    return jsonb_build_object('allowed', true);
  end if;
  if tally.attempts >= lim then
    return jsonb_build_object('allowed', false, 'retry_after',
      greatest(1, ceil(extract(epoch from (tally.window_start + win - now()))))::integer);
  end if;
  update public.purchase_verify_tally t set attempts = t.attempts + 1 where t.user_id = p_user_id;
  return jsonb_build_object('allowed', true);
end;
$$;

revoke all on function public.purchase_verify_allow(uuid) from public, anon, authenticated;
grant execute on function public.purchase_verify_allow(uuid) to service_role;

-- ---------------------------------------------------------------- L5: a block covers friend requests
--
-- The question the friend request rule asks: has the ADDRESSEE blocked the REQUESTER? SECURITY DEFINER
-- because user_blocks is readable only by the blocker (0017), and the requester is the one inserting.
-- It answers about the two ids of the row in front of it and nothing else, and it is not callable by any
-- client: a client that could call it could learn who has blocked them, which 0017 deliberately hides.
create or replace function public.friendships_refuse_blocked()
returns trigger
language plpgsql
security definer
set search_path = public, pg_temp
as $$
begin
  if exists (select 1 from public.user_blocks b
              where b.blocker = new.addressee and b.blocked = new.requester) then
    -- The same refusal a request to a player who cannot be found gets from the client's point of view:
    -- 0017's rule is that the blocked player is not told they were blocked.
    raise exception 'that player cannot receive your friend request' using errcode = 'PT403';
  end if;
  return new;
end;
$$;

drop trigger if exists friendships_block_check on public.friendships;
create trigger friendships_block_check
  before insert on public.friendships
  for each row execute function public.friendships_refuse_blocked();

-- Blocking someone ends whatever stood between the two: their request pending to the blocker, the
-- blocker's own request pending to them, and an ACCEPTED friendship. The owner decided (2026-10-23) that
-- a block also ends a friendship, so the blocked player stops seeing the blocker on their friend list and
-- loses 0003's friend read on the blocker's team snapshot in the same statement. Unblocking (0017's
-- user_unblock) restores nothing: a new friendship starts with a new request, as after a Remove.
create or replace function public.user_blocks_end_friendship()
returns trigger
language plpgsql
security definer
set search_path = public, pg_temp
as $$
begin
  delete from public.friendships f
   where (f.requester = new.blocked and f.addressee = new.blocker)
      or (f.requester = new.blocker and f.addressee = new.blocked);
  return new;
end;
$$;

drop trigger if exists user_blocks_end_friendship on public.user_blocks;
create trigger user_blocks_end_friendship
  after insert on public.user_blocks
  for each row execute function public.user_blocks_end_friendship();

-- ---------------------------------------------------------------- L6 and the new trigger functions, revoked

revoke all on function public.guild_messages_refuse_muted() from public, anon, authenticated;
revoke all on function public.friendships_refuse_blocked() from public, anon, authenticated;
revoke all on function public.user_blocks_end_friendship() from public, anon, authenticated;

-- ---------------------------------------------------------------- what was applied, said once

do $$
declare
  who text;
begin
  foreach who in array array['anon', 'authenticated'] loop
    if has_function_privilege(who, 'public.purchase_verify_allow(uuid)', 'execute') then
      raise exception '0021: % can execute purchase_verify_allow', who;
    end if;
    if has_table_privilege(who, 'public.purchase_verify_tally', 'select')
       or has_table_privilege(who, 'public.purchase_verify_limits', 'select') then
      raise exception '0021: % can read a purchase verification table', who;
    end if;
    if has_function_privilege(who, 'public.friendships_refuse_blocked()', 'execute')
       or has_function_privilege(who, 'public.user_blocks_end_friendship()', 'execute')
       or has_function_privilege(who, 'public.guild_messages_refuse_muted()', 'execute') then
      raise exception '0021: % can execute a trigger function', who;
    end if;
  end loop;
  if not has_function_privilege('service_role', 'public.purchase_verify_allow(uuid)', 'execute') then
    raise exception '0021: service_role cannot execute purchase_verify_allow, so the Edge Function could not ask it';
  end if;
  raise notice '0021: purchase verification is throttled per account (service role), a block refuses friend requests and ends a friendship';
end;
$$;

notify pgrst, 'reload schema';

-- ---------------------------------------------------------------- after applying, by hand
--
--   select * from public.purchase_verify_limits;
--   select user_id, attempts, window_start from public.purchase_verify_tally order by attempts desc limit 20;
