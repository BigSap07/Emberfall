-- 0013_save_and_name_rules.sql - push_save refuses what is not a save, and display names take
-- the guild tables' text rule. Docs/CLOUD-GAPS.md S2 and S7.
--
-- RUN AFTER 0001 and 0002: it replaces push_save (0002) and constrains profiles (0001). Nothing in
-- 0003-0012 is touched, so it does not care whether those are applied.
--
-- RE-APPLIABLE, like 0004-0012: the table is `if not exists`, its row is inserted `on conflict do
-- nothing` (so re-applying never resets a limit somebody raised), push_save is `create or
-- replace`, and each constraint is added only when it is absent. Running it twice changes nothing.
--
-- ---------------------------------------------------------------- what was wrong
--
-- S2. push_save checked that a document was PRESENT and nothing about what it was. An empty
-- object, a bare string and an array were each stored as a save, a 3 MB document was accepted,
-- and save_version was whatever the caller claimed. That last one is the damaging one: a row
-- labelled 999 makes every real client answer RefuseCloudNewerVersion for that account from then
-- on, and it is RIGHT to - refusing a save from a newer build is the safety rule working. The
-- client cannot be the one to tell a lie from a newer build, so the server has to refuse the lie.
--
-- S7. profiles.display_name was checked for length only. 0004 refuses [[:cntrl:]] on every guild
-- text column and requires a guild name to be its own trim, because a newline breaks a UI built
-- out of single-line labels - and display names are shown in the same rosters. So a name that
-- was a newline, or nothing but spaces, was stored.
--
-- ---------------------------------------------------------------- the limits the server holds
--
-- One row. The highest save_version this project accepts, and the largest document it stores,
-- held in the database rather than written into push_save, so raising either is an UPDATE and not
-- a function rewrite.
--
-- max_save_version is SaveSerializer.CurrentVersion at the time of writing (19). It has to move
-- WITH the client: a build that writes version 20 cannot sync until this row says 20. That is the
-- price of the rule and it is deliberate - the alternative, a ceiling with headroom, is a range of
-- versions a caller can still poison an account with. Raise it BEFORE a build that bumps
-- CurrentVersion goes out, never after:
--
--     update public.save_limits set max_save_version = 20, updated_at = now();
--
-- max_document_bytes is 1 MiB, measured against octet_length(data::text), which is jsonb's own
-- rendering (", " and ": " separators, so slightly larger than the file on disk). The largest real
-- save measured for this was a 94-hero development save at about 213 KB in that rendering, so the
-- cap is roughly five times the biggest account there is, and still refuses S2's 3 MB case.
--
-- No client role can read or write it. push_save is SECURITY DEFINER and reads it as the owner;
-- nothing else needs to.
create table if not exists public.save_limits (
  id                 boolean primary key default true check (id),
  max_save_version   integer not null check (max_save_version >= 1),
  max_document_bytes integer not null check (max_document_bytes >= 1024),
  updated_at         timestamptz not null default now()
);

alter table public.save_limits enable row level security;
revoke all on public.save_limits from anon, authenticated;

insert into public.save_limits (id, max_save_version, max_document_bytes)
values (true, 19, 1048576)
on conflict (id) do nothing;

-- ---------------------------------------------------------------- push_save, with the shape checked
--
-- Everything 0002's version did, unchanged and in the same order, plus three refusals made BEFORE
-- the row is read, so a refused push never takes the row lock and never moves a revision:
--
--   * save_version above the server's maximum   PT400
--   * a document that is not a JSON object       PT400
--   * an empty object                            PT400
--   * a document larger than the server's cap    PT413 (Payload Too Large, by PostgREST's PT rule)
--
-- The empty object is refused by name because it is the one S2 found that is still an object, and
-- it is exactly what P2 used to upload in place of a save that would not parse. A rule any stricter
-- than "a non-empty object" - "its version key equals save_version", say - would be reading the
-- save format, which this table deliberately does not do (0002's header).
--
-- The signature is 0002's exactly, so `create or replace` swaps the body in place and every grant
-- on it survives. They are restated below anyway, so this file stands on its own.
--
-- A missing save_limits row refuses every push rather than accepting every push. "Could not read
-- the limit" is not a reason to store whatever arrived.
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
  uid        uuid := auth.uid();
  stored     integer;
  result     public.saves;
  max_ver    integer;
  max_bytes  integer;
  doc_bytes  integer;
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

  select max_save_version, max_document_bytes into max_ver, max_bytes
    from public.save_limits where id;
  if max_ver is null or max_bytes is null then
    raise exception 'push_save has no save_limits row to check against - see 0013'
      using errcode = 'PT500';
  end if;

  if p_save_version > max_ver then
    raise exception 'push_save refuses save_version %: this server accepts up to %', p_save_version, max_ver
      using errcode = 'PT400';
  end if;
  if jsonb_typeof(p_data) <> 'object' then
    raise exception 'push_save needs a save document that is a JSON object, not a %', jsonb_typeof(p_data)
      using errcode = 'PT400';
  end if;
  if p_data = '{}'::jsonb then
    raise exception 'push_save refuses an empty object: it is not a save'
      using errcode = 'PT400';
  end if;
  doc_bytes := octet_length(p_data::text);
  if doc_bytes > max_bytes then
    raise exception 'push_save refuses a % byte document: this server stores up to %', doc_bytes, max_bytes
      using errcode = 'PT413';
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

-- ---------------------------------------------------------------- the same rules, on the columns
--
-- The second lock, in the shape every table here already has one. push_save is the only write
-- path into saves and no client role holds a write grant on profiles beyond display_name, but a
-- rule that can be a constraint is not left to a function alone (0004's header says why).
--
-- Each is added NOT VALID, which enforces it on every write from this moment without reading the
-- rows already there, and is then VALIDATED only if no existing row breaks it. The live project
-- holds rows the rule tests wrote on purpose before this file existed, and a migration that
-- failed on them would leave push_save replaced and the constraint missing. When a row does break
-- it, the constraint stays enforced-but-unvalidated and a NOTICE says how many rows; the queries
-- at the bottom of this file list them.
--
-- The size and version ceilings are NOT restated here: a CHECK cannot read save_limits, and a
-- second copy of the numbers would drift from the first. The object rule needs no number.
--
-- The display name rule is 0004's guild-name rule word for word: the name is its own btrim, so a
-- name of nothing but spaces fails (its trim is empty, and is not equal to it), and no control
-- character is allowed, so no newline. 0001's length check of 1 to 24 stays as it is.
do $$
begin
  if not exists (select 1 from pg_constraint
                  where conname = 'saves_data_is_object' and conrelid = 'public.saves'::regclass) then
    alter table public.saves
      add constraint saves_data_is_object check (jsonb_typeof(data) = 'object') not valid;
  end if;
  if exists (select 1 from pg_constraint
              where conname = 'saves_data_is_object' and conrelid = 'public.saves'::regclass
                and not convalidated) then
    if exists (select 1 from public.saves where jsonb_typeof(data) <> 'object') then
      raise notice 'saves_data_is_object: enforced on new writes, NOT validated - % stored save(s) are not objects',
        (select count(*) from public.saves where jsonb_typeof(data) <> 'object');
    else
      alter table public.saves validate constraint saves_data_is_object;
    end if;
  end if;

  if not exists (select 1 from pg_constraint
                  where conname = 'profiles_display_name_single_line' and conrelid = 'public.profiles'::regclass) then
    alter table public.profiles
      add constraint profiles_display_name_single_line
      check (display_name = btrim(display_name) and display_name !~ '[[:cntrl:]]') not valid;
  end if;
  if exists (select 1 from pg_constraint
              where conname = 'profiles_display_name_single_line' and conrelid = 'public.profiles'::regclass
                and not convalidated) then
    if exists (select 1 from public.profiles
                where not (display_name = btrim(display_name) and display_name !~ '[[:cntrl:]]')) then
      raise notice 'profiles_display_name_single_line: enforced on new writes, NOT validated - % stored name(s) break it',
        (select count(*) from public.profiles
          where not (display_name = btrim(display_name) and display_name !~ '[[:cntrl:]]'));
    else
      alter table public.profiles validate constraint profiles_display_name_single_line;
    end if;
  end if;
end;
$$;

-- ---------------------------------------------------------------- after applying, by hand
--
-- Read-only, and nothing in this file runs them. They answer whether anything stored before 0013
-- breaks the rules it introduces. Rows owned by the rule tests' throwaway users are expected.
--
--   select user_id, save_version from public.saves
--    where save_version > (select max_save_version from public.save_limits);
--   select user_id, jsonb_typeof(data) from public.saves where jsonb_typeof(data) <> 'object';
--   select user_id, octet_length(data::text) as bytes from public.saves
--    where octet_length(data::text) > (select max_document_bytes from public.save_limits);
--   select user_id, display_name from public.profiles
--    where not (display_name = btrim(display_name) and display_name !~ '[[:cntrl:]]');
--   select conname, convalidated from pg_constraint
--    where conname in ('saves_data_is_object', 'profiles_display_name_single_line');
