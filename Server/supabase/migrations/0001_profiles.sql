-- 0001_profiles.sql - accounts and friend codes.
--
-- Run this FIRST, in the Supabase SQL editor, as the project owner. 0002 and 0003 both
-- depend on the profile row this file creates for every user.
--
-- The shape of the decision recorded here: a profile is created by a TRIGGER on auth.users,
-- not by the client. An anonymous sign-in is a normal user creation, so an anonymous player
-- gets a profile and a friend code in the same instant a permanent one does, and there is no
-- window in which a signed-in user has no row. The client is never given INSERT on this
-- table at all, which is what makes that guarantee hold rather than merely describing it.

-- ---------------------------------------------------------------- friend code alphabet
--
-- Six characters from a 30-symbol alphabet: the digits 2-9 and the letters A-Z with I, L, O
-- and U removed. 0/O and 1/I/L are the pairs people actually mistype when reading a code off
-- a phone screen to another person, and U is dropped so the generator cannot spell anything
-- it would then have to be apologised for. 30^6 is 729 million codes, so collisions are rare
-- and the retry loop below is a formality rather than a hot path.
--
-- The CHECK constraint restates the alphabet as a regex rather than trusting the generator:
-- a code that arrives any other way - a hand-written INSERT, a future admin tool - is
-- rejected by the database instead of quietly becoming a code nobody can read out loud.
create or replace function public.generate_friend_code()
returns text
language plpgsql
volatile
as $$
declare
  alphabet constant text := '23456789ABCDEFGHJKMNPQRSTVWXYZ';
  code text := '';
  i integer;
begin
  for i in 1..6 loop
    code := code || substr(alphabet, 1 + floor(random() * length(alphabet))::int, 1);
  end loop;
  return code;
end;
$$;

create table if not exists public.profiles (
  user_id      uuid primary key references auth.users (id) on delete cascade,
  display_name text not null default 'Wayfarer'
               check (char_length(display_name) between 1 and 24),
  friend_code  text not null unique
               check (friend_code ~ '^[2-9A-HJKMNP-TV-Z]{6}$'),
  created_at   timestamptz not null default now()
);

-- ---------------------------------------------------------------- profile on user creation
--
-- SECURITY DEFINER because it runs as the trigger's owner against a table the new user has no
-- INSERT privilege on - that is the whole point. search_path is pinned so a schema planted on
-- the caller's path cannot redirect the insert; every SECURITY DEFINER function in these
-- migrations pins it for the same reason.
--
-- The loop retries on a friend-code collision rather than letting the unique violation reach
-- auth.users and fail the sign-up. Ten attempts against 729 million codes is not a real
-- limit; if it is ever exhausted the exception is re-raised, because a sign-up that silently
-- produced a profile with no code would be worse than a sign-up that failed loudly.
create or replace function public.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  attempt integer := 0;
begin
  loop
    attempt := attempt + 1;
    begin
      insert into public.profiles (user_id, friend_code)
      values (new.id, public.generate_friend_code());
      exit;
    exception when unique_violation then
      if attempt >= 10 then raise; end if;
    end;
  end loop;
  return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
  after insert on auth.users
  for each row execute function public.handle_new_user();

-- ---------------------------------------------------------------- privileges and RLS
--
-- Two independent locks, deliberately. RLS decides which ROWS a role may touch; the grants
-- below decide which COLUMNS and which verbs it may use at all. friend_code is protected by
-- the second lock rather than the first: an owner-scoped UPDATE policy would happily let a
-- player rewrite their own code, and a friend code a player can choose is a friend code a
-- player can take from somebody else. Only display_name is grantable, so the column cannot be
-- written by any client statement regardless of policy.
--
-- anon is revoked on every table in these migrations. RLS with no anon policy already denies
-- it, so this is belt and braces - but "no table readable by the anon role" is a claim worth
-- being true twice, since a future policy written for `public` rather than `authenticated`
-- would otherwise quietly hand the anon key read access to every profile in the project.
alter table public.profiles enable row level security;

revoke all on public.profiles from anon, authenticated;
grant select on public.profiles to authenticated;
grant update (display_name) on public.profiles to authenticated;

-- Any signed-in user may read any profile. That is what makes friend-code lookup a plain
-- query instead of an RPC, and it is also the widest rule in these migrations: it means a
-- signed-in client can page the whole profile table, not merely resolve a code it was given.
-- Accepted deliberately for two players and a display name; the tightening, when there is a
-- population to protect, is to drop this policy and expose an exact-match lookup function.
create policy profiles_read_any_signed_in
  on public.profiles for select
  to authenticated
  using (true);

create policy profiles_update_own
  on public.profiles for update
  to authenticated
  using (user_id = auth.uid())
  with check (user_id = auth.uid());
