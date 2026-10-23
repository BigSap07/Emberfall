-- supabase-standin.sql - the smallest stand-in for what a Supabase project gives a migration, so the
-- files in ../migrations can be applied to a throwaway LOCAL PostgreSQL and their rules exercised.
--
-- LOCAL ONLY. Never run this against a Supabase project: a real project already has every object
-- below, and the real ones are richer. Tools/supabase-local-check.sh is the only caller.
--
-- What it reproduces, because the migrations depend on it:
--   - the roles anon, authenticated and service_role (service_role bypasses RLS, as on Supabase);
--   - Supabase's default grants: every table and function created in public by the owner is granted
--     to all three roles unless a migration revokes it. That default is the reason every migration
--     here revokes before it grants, so the stand-in must have it or the revokes prove nothing;
--   - auth.users (the columns the migrations touch) and auth.uid(), which reads the caller's id the
--     way PostgREST hands it over: the setting request.jwt.claim.sub.
--
-- A test impersonates a player with:
--     set role authenticated; select set_config('request.jwt.claim.sub', '<uuid>', false);

do $$
begin
  if not exists (select 1 from pg_roles where rolname = 'anon') then create role anon nologin noinherit; end if;
  if not exists (select 1 from pg_roles where rolname = 'authenticated') then create role authenticated nologin noinherit; end if;
  if not exists (select 1 from pg_roles where rolname = 'service_role') then create role service_role nologin noinherit bypassrls; end if;
end;
$$;

grant anon, authenticated, service_role to current_user;

create schema if not exists auth;
grant usage on schema auth to anon, authenticated, service_role;

create table if not exists auth.users (
  id                 uuid primary key default gen_random_uuid(),
  email              text,
  is_anonymous       boolean not null default true,
  raw_user_meta_data jsonb not null default '{}'::jsonb,
  created_at         timestamptz not null default now()
);

create or replace function auth.uid()
returns uuid
language sql
stable
as $$ select nullif(current_setting('request.jwt.claim.sub', true), '')::uuid $$;

grant execute on function auth.uid() to anon, authenticated, service_role;

grant usage on schema public to anon, authenticated, service_role;
alter default privileges in schema public grant all on tables to anon, authenticated, service_role;
alter default privileges in schema public grant all on sequences to anon, authenticated, service_role;
alter default privileges in schema public grant execute on functions to anon, authenticated, service_role;
