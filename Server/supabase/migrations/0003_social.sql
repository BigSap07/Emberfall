-- 0003_social.sql - friendships, and the team snapshot a friend is allowed to fight.
--
-- Run after 0002.

-- The two user columns reference public.profiles, NOT auth.users directly, and the difference
-- is load-bearing rather than stylistic. Integrity is identical either way - a profile row
-- exists for every user from the instant the user does (0001's trigger) and cascades from
-- auth.users when one is deleted - but a foreign key into profiles is also what lets the
-- client read a friend list in ONE request: PostgREST can only embed a table it can reach
-- through a declared relationship, so `profiles!friendships_requester_fkey(display_name,...)`
-- resolves both parties' names and codes inline. Pointing at auth.users instead would make the
-- friend list an N+1 of profile lookups for exactly the same rows.
create table if not exists public.friendships (
  id         uuid primary key default gen_random_uuid(),
  requester  uuid not null references public.profiles (user_id) on delete cascade,
  addressee  uuid not null references public.profiles (user_id) on delete cascade,
  status     text not null default 'pending' check (status in ('pending', 'accepted')),
  created_at timestamptz not null default now(),
  constraint friendships_not_self check (requester <> addressee)
);

-- One row per PAIR, in either direction. A friendship is an unordered relationship that
-- happens to remember who asked, so the uniqueness has to be stated on the unordered pair -
-- a plain unique (requester, addressee) would let A request B while B independently requests
-- A, leaving two rows that disagree about who may accept.
create unique index if not exists friendships_pair_unique
  on public.friendships (least(requester, addressee), greatest(requester, addressee));

create index if not exists friendships_addressee_idx on public.friendships (addressee, status);
create index if not exists friendships_requester_idx on public.friendships (requester, status);

-- ---------------------------------------------------------------- the friendship test
--
-- Answers one question and only that question: is the CALLER an accepted friend of the user
-- named? auth.uid() is read inside the function rather than passed in, so there is no
-- argument by which a caller can ask about two people who are not themselves.
--
-- SECURITY DEFINER so the team_snapshots read policy below does not have to satisfy
-- friendships' own policies while it is being evaluated. Both would pass as written, but a
-- policy that reads a table whose policies read back is the shape that produces
-- infinite-recursion errors later, when one of the two grows a clause. STABLE so the planner
-- may call it once per statement instead of once per row.
create or replace function public.is_friend(other_user uuid)
returns boolean
language sql
stable
security definer
set search_path = public, pg_temp
as $$
  select exists (
    select 1
      from public.friendships f
     where f.status = 'accepted'
       and ( (f.requester = auth.uid() and f.addressee = other_user)
          or (f.addressee = auth.uid() and f.requester = other_user) )
  );
$$;

revoke all on function public.is_friend(uuid) from public, anon;
grant execute on function public.is_friend(uuid) to authenticated;

alter table public.friendships enable row level security;

revoke all on public.friendships from anon, authenticated;
grant select, insert, delete on public.friendships to authenticated;
-- status is the only writable column, which is what stops an accept from also rewriting who
-- the friendship is between. The policy below decides WHICH status transition is allowed;
-- this decides that nothing else on the row can move at all.
grant update (status) on public.friendships to authenticated;

create policy friendships_read_either_party
  on public.friendships for select
  to authenticated
  using (auth.uid() in (requester, addressee));

-- Only ever created BY the requester, and only ever pending. A row inserted as 'accepted'
-- would be a friendship the other party never agreed to.
create policy friendships_request_as_self
  on public.friendships for insert
  to authenticated
  with check (auth.uid() = requester and status = 'pending');

-- Only the addressee accepts, and the only transition is pending -> accepted. USING sees the
-- row as it stands, WITH CHECK the row as it would be, so together they say: you may touch
-- this row only while it is pending and addressed to you, and only to make it accepted.
-- Un-accepting is not a transition; removing a friendship is a delete.
create policy friendships_accept_as_addressee
  on public.friendships for update
  to authenticated
  using (auth.uid() = addressee and status = 'pending')
  with check (auth.uid() = addressee and status = 'accepted');

-- Either party may walk away, at any status. That covers three different player actions -
-- cancelling a request you sent, declining one you received, and removing a friend you have -
-- which are one row deletion each and need no separate state.
create policy friendships_remove_either_party
  on public.friendships for delete
  to authenticated
  using (auth.uid() in (requester, addressee));

-- ---------------------------------------------------------------- team snapshots
--
-- The resolved line a friend fights: hero id, level, stars, selected talent, relic id and the
-- armor pieces BY VALUE. Everything CombatSimulation needs to rebuild the far side of a
-- fight, and deliberately nothing else - a snapshot is not a save, and a friend who can fight
-- your line does not thereby learn your gold, your collection or your campaign position.
--
-- Armor travels by value for the reason BattleLoadout already records: an armor uid resolves
-- against a save only its owner holds, so a snapshot carrying uids would either fail to
-- resolve or silently resolve against the READER's own pieces and fight a hybrid line.
--
-- `units` is jsonb rather than a child table with a row per hero. The snapshot is written and
-- read whole, five units at a time, and is never queried inside - the same reasoning that
-- keeps the save a document.
create table if not exists public.team_snapshots (
  -- profiles rather than auth.users, for the reason friendships gives above: a snapshot is
  -- read alongside the owner's name, and one relationship is one request.
  user_id     uuid primary key references public.profiles (user_id) on delete cascade,
  label       text not null default '',
  power_score integer not null default 0,
  units       jsonb not null default '[]'::jsonb,
  updated_at  timestamptz not null default now()
);

-- updated_at is stamped by the database, not sent. A client that could write it could claim
-- a freshness it does not have, and "when did this line last change" is the one field a
-- friend reads to decide whether the fight they are being offered is current.
create or replace function public.stamp_team_snapshot()
returns trigger
language plpgsql
as $$
begin
  new.updated_at := now();
  return new;
end;
$$;

drop trigger if exists team_snapshots_stamp on public.team_snapshots;
create trigger team_snapshots_stamp
  before insert or update on public.team_snapshots
  for each row execute function public.stamp_team_snapshot();

alter table public.team_snapshots enable row level security;

revoke all on public.team_snapshots from anon, authenticated;
-- insert AND update, because the client upserts: PostgREST's merge-duplicates resolution is
-- an INSERT ... ON CONFLICT DO UPDATE, and both halves are privilege-checked.
grant select, insert, update, delete on public.team_snapshots to authenticated;

create policy team_snapshots_read_own_or_friend
  on public.team_snapshots for select
  to authenticated
  using (user_id = auth.uid() or public.is_friend(user_id));

create policy team_snapshots_insert_own
  on public.team_snapshots for insert
  to authenticated
  with check (user_id = auth.uid());

create policy team_snapshots_update_own
  on public.team_snapshots for update
  to authenticated
  using (user_id = auth.uid())
  with check (user_id = auth.uid());

create policy team_snapshots_delete_own
  on public.team_snapshots for delete
  to authenticated
  using (user_id = auth.uid());
