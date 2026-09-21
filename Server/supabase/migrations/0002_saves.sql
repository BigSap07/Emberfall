-- 0002_saves.sql - the cloud save, and the concurrency rule that guards it.
--
-- Run after 0001.
--
-- One row per player. `data` is the SaveSerializer blob unchanged - the same JSON the local
-- file holds, not a re-modelled copy of it. That is the whole design: the save format has a
-- version, a migration history and a downgrade refusal already, and a second server-side
-- schema of the same facts would be a second thing to migrate and a second thing to get out
-- of step. The database stores the document and the numbers needed to decide WHICH document
-- wins; it does not interpret it.
--
-- `save_version` is lifted out of the blob into its own column for exactly one reason: the
-- conflict decision has to be able to refuse a save from a newer build WITHOUT parsing it,
-- the same refusal SaveSerializer.Deserialize already makes locally.

create table if not exists public.saves (
  user_id      uuid primary key references auth.users (id) on delete cascade,
  save_version integer not null check (save_version >= 1),
  revision     integer not null check (revision >= 1),
  device_id    text not null default '',
  data         jsonb not null,
  updated_at   timestamptz not null default now()
);

alter table public.saves enable row level security;

-- ---------------------------------------------------------------- no client writes, at all
--
-- SELECT is the only verb any client role holds on this table. Every write goes through
-- push_save below, which is what makes "the revision check is enforced in the database, not
-- trusted from the client" a fact about privileges rather than a convention the client is
-- asked to honour. A PATCH straight at /rest/v1/saves fails on privilege before RLS is even
-- consulted, so there is no path that sets a revision of the caller's choosing.
revoke all on public.saves from anon, authenticated;
grant select on public.saves to authenticated;

create policy saves_read_own
  on public.saves for select
  to authenticated
  using (user_id = auth.uid());

-- The write policies exist even though no client role can reach them. They are the second
-- lock: if a later migration grants INSERT or UPDATE to authenticated - for a tool, for a
-- migration script, by accident - the rows a client can touch are still only its own.
create policy saves_insert_own
  on public.saves for insert
  to authenticated
  with check (user_id = auth.uid());

create policy saves_update_own
  on public.saves for update
  to authenticated
  using (user_id = auth.uid())
  with check (user_id = auth.uid());

-- ---------------------------------------------------------------- optimistic concurrency
--
-- Compare and swap on `revision`. The caller sends the revision it believes is stored; the
-- update only lands where that is still true, and the new revision is computed HERE rather
-- than sent. A client cannot skip a revision, reuse one, or wind one back, because it never
-- names one.
--
-- p_expected_revision = 0 means "I believe there is no save yet". It is a real case, not a
-- sentinel for missing input: a fresh install that has never synced sends 0 and gets revision
-- 1. If a row does exist, 0 is stale like any other wrong number and is refused.
--
-- Errors are raised with PostgREST's PT-prefixed SQLSTATE convention, where the last three
-- characters are the HTTP status PostgREST returns. PT409 gives the client a plain 409
-- Conflict to branch on instead of a 500 it would have to parse a message out of.
--
-- SECURITY DEFINER, because the function writes a table its callers deliberately have no
-- write privilege on. It reads auth.uid() itself and scopes every statement to that value, so
-- the elevated privilege buys the concurrency check and nothing else: there is no argument by
-- which a caller can name another user's row.
create or replace function public.push_save(
  p_expected_revision integer,
  p_save_version      integer,
  p_device_id         text,
  p_data              jsonb
)
returns public.saves
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid      uuid := auth.uid();
  stored   integer;
  result   public.saves;
begin
  if uid is null then
    raise exception 'push_save requires a signed-in user' using errcode = 'PT401';
  end if;
  if p_save_version is null or p_save_version < 1 then
    raise exception 'push_save needs a positive save_version' using errcode = 'PT400';
  end if;
  if p_data is null then
    raise exception 'push_save needs a save document' using errcode = 'PT400';
  end if;

  select revision into stored from public.saves where user_id = uid;

  if stored is null then
    if coalesce(p_expected_revision, 0) <> 0 then
      raise exception 'stale revision: no save is stored, % was expected', p_expected_revision
        using errcode = 'PT409';
    end if;
    insert into public.saves (user_id, save_version, revision, device_id, data)
    values (uid, p_save_version, 1, coalesce(p_device_id, ''), p_data)
    returning * into result;
    return result;
  end if;

  update public.saves
     set save_version = p_save_version,
         revision     = stored + 1,
         device_id    = coalesce(p_device_id, ''),
         data         = p_data,
         updated_at   = now()
   where user_id  = uid
     and revision = p_expected_revision
  returning * into result;

  -- FOUND is set by the UPDATE above: false means the WHERE clause matched no row, which at
  -- this point can only be the revision mismatch, since the row's existence was just proven.
  if not found then
    raise exception 'stale revision: % is stored, % was sent', stored, p_expected_revision
      using errcode = 'PT409';
  end if;
  return result;
end;
$$;

revoke all on function public.push_save(integer, integer, text, jsonb) from public, anon;
grant execute on function public.push_save(integer, integer, text, jsonb) to authenticated;
