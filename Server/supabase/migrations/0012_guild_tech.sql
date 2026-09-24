-- 0012_guild_tech.sql - the guild tech tree that replaced the four-node Guild Path.
-- claude/DESIGN-GUILD-TECH.md.
--
-- RUN AFTER 0011, and 0011 must be applied first: this file reads guild_path_ledger and
-- guild_path_balance, and it REPLACES guild_path_balance (see "the balance" below, which is the
-- one thing here that changes something 0011 created).
--
-- RE-APPLIABLE, like 0004-0011: tables and indexes are `if not exists`, columns are `add column
-- if not exists`, policies are dropped before they are created, functions are `create or replace`,
-- and every content row is upserted. Running it twice changes nothing and loses nothing.
--
-- NOTHING IN 0011 IS EDITED. The Path's node rows, its ledger, its unlocks and its pay all stay
-- exactly as they are, and every Waymark anybody ever bought a node with stays bought. G9's "the
-- Path's nodes become early guild level unlocks" is done on the CLIENT, as an OR
-- (GameSession.OwnsGuildPathNode): bought under 0011, or granted by the guild's level. So there is
-- no migration of anybody's purchases and nothing is taken away.
--
-- ---------------------------------------------------------------- what this adds
--
--   * guild_tech_families / guild_tech_talents - the tree's content, mirrored from
--     Assets/Resources/Content/guild-tech.json. Read-only to clients.
--   * guild_tech_rungs   - rungs one MEMBER bought, keyed to the account.
--   * guild_tech_levels  - one row per guild: the level it has reached, as a HIGH-WATER MARK.
--   * guild_tech_picks   - the talent the GUILD runs on each family, and the cycle it starts.
--   * guild_tech_xp(guild)      - the guild's XP, DERIVED from the Path's ledger. No new faucet.
--   * guild_tech_state()        - settles the level, then answers everything the client needs.
--   * guild_tech_buy_rung(f)    - a member spends Waymarks on the next rung of a track.
--   * guild_tech_pick_talent(f, t) - leadership sets a family's talent, from the next cycle.
--   * guild_tech_dev_set(g, level, xp) - the dev override. See its own comment.
--
-- ---------------------------------------------------------------- who can mint, and who can spend
--
-- NOBODY MINTS ANYTHING NEW HERE. Guild XP is not stored and is not paid: it is SUMMED from
-- guild_path_ledger, the rows 0011 already writes, joined to the guild each member was fighting
-- for in that cycle. So a guild's level is a function of what its members actually earned, it is
-- correct the moment this file is applied without backfilling anything, and a member who leaves
-- does not take the XP they earned while they were here.
--
-- Spending is the member's own Waymarks, through the same balance 0011 defined, with the rung
-- spend added to it. The rung rows have no client write grant; guild_tech_buy_rung is the only way
-- in, it takes the caller's profile FOR UPDATE first (guild_path_unlock's reason), and the primary
-- key on (user_id, family_id) plus the rung count check is what stops a double purchase.
--
-- ---------------------------------------------------------------- the constants
--
-- Repeated in Assets/Resources/Content/guild-tech.json, the duplication 0011's pay lives under.
-- Tools/DevHarness/GuildTechChecks.cs reads both and fails the build-check when they disagree, and
-- `DevHarness.exe --guild-tech` prints the derivation every one of them comes out of. The bodies
-- must stay the single-line `select <n>;` form, which is what the harness parses.

create or replace function public.guild_tech_rung_unit()
returns integer
language sql
immutable
as $$ select 217; $$;

create or replace function public.guild_tech_xp_per_level()
returns integer
language sql
immutable
as $$ select 1028; $$;

create or replace function public.guild_tech_level_ceiling()
returns integer
language sql
immutable
as $$ select 16; $$;

create or replace function public.guild_tech_rungs_per_family()
returns integer
language sql
immutable
as $$ select 3; $$;

-- ---------------------------------------------------------------- the tree's content

create table if not exists public.guild_tech_families (
  id       text primary key check (id ~ '^[a-z][a-z0-9-]{1,31}$'),
  ordinal  integer not null check (ordinal >= 0)
);

create table if not exists public.guild_tech_talents (
  id        text primary key check (id ~ '^[a-z][a-z0-9_-]{1,63}$'),
  family_id text not null references public.guild_tech_families (id)
);

-- The families, in guild-tech.json's own order - which is the order the unlock rule walks, so the
-- ordinal is what decides which family's rung 1 opens at guild level 1.
insert into public.guild_tech_families (id, ordinal) values
  ('line', 0),
  ('blade', 1),
  ('grace', 2),
  ('reach', 3)
on conflict (id) do update set ordinal = excluded.ordinal;

-- The twelve guild talents. The server holds only the ID and the family: what a talent DOES is
-- content, the client resolves it out of guild-tech.json, and a server that held a copy of the
-- effects would be a second place for them to disagree. All the server decides is that a guild
-- cannot pick a talent that is not its family's.
insert into public.guild_tech_talents (id, family_id) values
  ('line_hold_the_wall', 'line'),
  ('line_answer_in_kind', 'line'),
  ('line_dig_in', 'line'),
  ('blade_first_blood', 'blade'),
  ('blade_press_the_wound', 'blade'),
  ('blade_no_survivors', 'blade'),
  ('grace_steady_hands', 'grace'),
  ('grace_unbroken', 'grace'),
  ('grace_last_word', 'grace'),
  ('reach_ranging_shot', 'reach'),
  ('reach_steady_aim', 'reach'),
  ('reach_mark_the_line', 'reach')
on conflict (id) do update set family_id = excluded.family_id;

-- ---------------------------------------------------------------- what a member owns

-- KEYED BY ACCOUNT, NEVER BY GUILD, exactly as 0011's unlocks are. Rungs are bought with the
-- member's own Waymarks and travel with them: leaving a guild, being removed from one or watching
-- one disband takes none of them away. That is what stops the tree becoming lock-in.
create table if not exists public.guild_tech_rungs (
  user_id    uuid not null references public.profiles (user_id) on delete cascade,
  family_id  text not null references public.guild_tech_families (id),
  rungs      integer not null default 0 check (rungs >= 0),
  spent      integer not null default 0 check (spent >= 0),
  updated_at timestamptz not null default now(),
  primary key (user_id, family_id)
);

-- ---------------------------------------------------------------- what a guild owns

-- THE LEVEL IS A HIGH-WATER MARK, and that is the whole reason this table exists rather than the
-- level being computed on every read. The XP requirement scales with member count (G7), so a guild
-- that loses members needs less XP per level and one that gains them needs more - and a level that
-- went DOWN when somebody left would revoke rungs a member had already paid for and lines they
-- were already using. Never lowering it costs one row per guild and removes the whole class of
-- problem.
create table if not exists public.guild_tech_levels (
  guild_id   uuid primary key references public.guilds (id) on delete cascade,
  level      integer not null default 0 check (level >= 0),
  updated_at timestamptz not null default now()
);

-- G4: THE GUILD PICKS THE TALENT, ONE PER FAMILY, AND IT CHANGES AT CYCLE ROLL. `from_cycle` is
-- what makes that true rather than intended: a pick made now names the NEXT cycle, so a guild
-- cannot swap talents in the middle of a week and cannot answer a dungeon floor by re-picking
-- after seeing it. Reading the active pick is "the newest row whose from_cycle has arrived".
create table if not exists public.guild_tech_picks (
  guild_id   uuid not null references public.guilds (id) on delete cascade,
  family_id  text not null references public.guild_tech_families (id),
  talent_id  text not null references public.guild_tech_talents (id),
  from_cycle integer not null check (from_cycle >= 0),
  set_by     uuid references public.profiles (user_id) on delete set null,
  created_at timestamptz not null default now(),
  primary key (guild_id, family_id, from_cycle)
);

create index if not exists guild_tech_picks_guild_idx on public.guild_tech_picks (guild_id, family_id, from_cycle desc);

-- ---------------------------------------------------------------- privileges and policy
--
-- Two locks, as everywhere. No client role writes any of these tables; the tree's content is read
-- by everyone signed in, a member's rungs by their owner only, and a guild's level and picks by
-- that guild's members.
alter table public.guild_tech_families enable row level security;
alter table public.guild_tech_talents  enable row level security;
alter table public.guild_tech_rungs    enable row level security;
alter table public.guild_tech_levels   enable row level security;
alter table public.guild_tech_picks    enable row level security;

revoke all on public.guild_tech_families from anon, authenticated;
revoke all on public.guild_tech_talents  from anon, authenticated;
revoke all on public.guild_tech_rungs    from anon, authenticated;
revoke all on public.guild_tech_levels   from anon, authenticated;
revoke all on public.guild_tech_picks    from anon, authenticated;
grant select on public.guild_tech_families to authenticated;
grant select on public.guild_tech_talents  to authenticated;
grant select on public.guild_tech_rungs    to authenticated;
grant select on public.guild_tech_levels   to authenticated;
grant select on public.guild_tech_picks    to authenticated;

drop policy if exists guild_tech_families_read on public.guild_tech_families;
create policy guild_tech_families_read on public.guild_tech_families for select to authenticated using (true);

drop policy if exists guild_tech_talents_read on public.guild_tech_talents;
create policy guild_tech_talents_read on public.guild_tech_talents for select to authenticated using (true);

drop policy if exists guild_tech_rungs_read_own on public.guild_tech_rungs;
create policy guild_tech_rungs_read_own
  on public.guild_tech_rungs for select
  to authenticated
  using (user_id = auth.uid());

drop policy if exists guild_tech_levels_read_mine on public.guild_tech_levels;
create policy guild_tech_levels_read_mine
  on public.guild_tech_levels for select
  to authenticated
  using (guild_id = public.my_guild());

drop policy if exists guild_tech_picks_read_mine on public.guild_tech_picks;
create policy guild_tech_picks_read_mine
  on public.guild_tech_picks for select
  to authenticated
  using (guild_id = public.my_guild());

-- ---------------------------------------------------------------- the balance
--
-- THE ONE THING HERE THAT CHANGES SOMETHING 0011 CREATED, and it has to: 0011's balance is the
-- ledger less the Path's unlocks, and it has never heard of a rung. A member who bought rungs
-- would read a balance that still held the Waymarks they had spent, and could spend them again.
--
-- A `create or replace` with the identical signature, which is what 0011's own header calls the
-- supported way to change a function, and it is additive - the Path's unlocks are still
-- subtracted, exactly as before. Re-applying 0011 AFTER this would put the old body back and
-- reopen the double-spend, so 0012 runs after 0011 and stays after it.
create or replace function public.guild_path_balance(p_user uuid)
returns bigint
language sql
stable
security definer
set search_path = public, pg_temp
as $$
  select coalesce((select sum(amount) from public.guild_path_ledger where user_id = p_user), 0)
       - coalesce((select sum(cost) from public.guild_path_unlocks where user_id = p_user), 0)
       - coalesce((select sum(spent) from public.guild_tech_rungs where user_id = p_user), 0);
$$;

-- ---------------------------------------------------------------- the guild's XP
--
-- DERIVED, NOT STORED, AND NOT A NEW FAUCET. A guild's XP is the Waymarks its members earned while
-- fighting for it: guild_path_ledger holds what each account was paid in each cycle, and
-- guild_dungeon_attempts says which guild that account was fighting for in that cycle, so the join
-- is exact. Three consequences worth stating, because each was a design choice:
--
--   * Applying this file needs no backfill. Every guild's level is correct the moment it runs,
--     computed from the weeks that already happened.
--   * A member who leaves does not take away the XP they earned here. The attempt rows stay, and
--     they name this guild.
--   * A member who joins brings nothing. Their earlier cycles name their earlier guild.
--
-- A member paid in a cycle they made no attempt in contributes nothing, which cannot happen: 0011
-- only ever pays a cycle an attempt was filed in.
create or replace function public.guild_tech_xp(p_guild uuid)
returns bigint
language sql
stable
security definer
set search_path = public, pg_temp
as $$
  select coalesce(sum(l.amount), 0)::bigint
    from public.guild_path_ledger l
    join (select distinct user_id, cycle, guild_id from public.guild_dungeon_attempts) a
      on a.user_id = l.user_id and a.cycle = l.cycle
   where a.guild_id = p_guild;
$$;

-- G7: THE SQUARE ROOT, AND THE ONLY PLACE THE SERVER WRITES IT. The client predicts with the same
-- expression (GuildTechConfig.XpForLevel) and GuildTechChecks fails the build when the two
-- disagree. A guild of m members earns m x (a member's cycle) and pays this per level, so it
-- climbs at sqrt(m) times the per-person rate of a guild of one - a large guild arrives sooner and
-- never arrives somewhere a small one cannot reach, because the CEILING does not move.
create or replace function public.guild_tech_xp_for_level(p_members integer)
returns bigint
language sql
immutable
as $$
  select round(public.guild_tech_xp_per_level() * sqrt(greatest(1, coalesce(p_members, 1))))::bigint;
$$;

-- Raises the guild's stored level to what its XP has earned, and never lowers it. Not callable by
-- a client: it runs at the top of guild_tech_state, so "settle, then tell me" is one round trip.
create or replace function public.guild_tech_settle(p_guild uuid)
returns integer
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  members integer;
  xp      bigint;
  earned  integer;
  held    integer;
begin
  if p_guild is null then return 0; end if;
  select count(*) into members from public.guild_members where guild_id = p_guild;
  xp := public.guild_tech_xp(p_guild);
  earned := least(public.guild_tech_level_ceiling(),
                  (xp / greatest(1, public.guild_tech_xp_for_level(members)))::integer);

  insert into public.guild_tech_levels (guild_id, level)
  values (p_guild, earned)
  on conflict (guild_id) do update
    set level = greatest(public.guild_tech_levels.level, excluded.level),
        updated_at = now();

  select level into held from public.guild_tech_levels where guild_id = p_guild;
  return coalesce(held, earned);
end;
$$;

-- ---------------------------------------------------------------- the state

-- Settles the level, then answers: the guild's level, its XP, how many members it counted, the
-- rungs THIS MEMBER bought, and the talent the guild is running on each family this cycle.
--
-- Works for a player in no guild at all. They read level 0, no picks and whatever rungs they
-- bought before they left - which still cost what they cost and are still theirs.
create or replace function public.guild_tech_state()
returns table (level integer, xp bigint, members integer, rungs jsonb, talents jsonb)
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid   uuid := auth.uid();
  mine  uuid;
  cyc   integer := public.guild_dungeon_cycle_at(now());
  lvl   integer;
begin
  if uid is null then
    raise exception 'guild_tech_state requires a signed-in user' using errcode = 'PT401';
  end if;
  select guild_id into mine from public.guild_members where user_id = uid;
  lvl := coalesce(public.guild_tech_settle(mine), 0);

  return query
    select lvl,
           coalesce(public.guild_tech_xp(mine), 0)::bigint,
           coalesce((select count(*)::integer from public.guild_members m where m.guild_id = mine), 0),
           coalesce((select jsonb_agg(jsonb_build_object('family', r.family_id, 'rungs', r.rungs))
                       from public.guild_tech_rungs r where r.user_id = uid and r.rungs > 0), '[]'::jsonb),
           coalesce((select jsonb_agg(jsonb_build_object('family', p.family_id, 'talent', p.talent_id))
                       from (select distinct on (family_id) family_id, talent_id
                               from public.guild_tech_picks
                              where guild_id = mine and from_cycle <= cyc
                              order by family_id, from_cycle desc) p), '[]'::jsonb);
end;
$$;

-- ---------------------------------------------------------------- buying a rung

-- The next rung on one track, at the price guild_tech_rung_unit() sets now, gated by the guild's
-- level. Answers as guild_tech_state does.
--
-- THE LEVEL GATE IS THE SERVER'S, and the unlock rule is written here rather than read from a
-- table for the reason the client does not author one either: the order is a RULE over the tree's
-- shape, not a list somebody typed. Rung 1 of every family, then every family's talent slot, then
-- rung 2, then rung 3 - so the level that opens rung r of the family at ordinal o is
--
--     r = 1 :                      o + 1
--     r > 1 : families x r      +  o + 1
--
-- with the talent slots occupying the block families x 1 + 1 .. families x 2.
create or replace function public.guild_tech_rung_level(p_family text, p_rung integer)
returns integer
language sql
stable
security definer
set search_path = public, pg_temp
as $$
  select case
           when p_rung < 1 or p_rung > public.guild_tech_rungs_per_family() then 0
           when p_rung = 1 then f.ordinal + 1
           else (select count(*) from public.guild_tech_families) * p_rung + f.ordinal + 1
         end
    from public.guild_tech_families f
   where f.id = p_family;
$$;

-- The level at which a family's talent slot opens: the block straight after every family's rung 1.
create or replace function public.guild_tech_slot_level(p_family text)
returns integer
language sql
stable
security definer
set search_path = public, pg_temp
as $$
  select (select count(*) from public.guild_tech_families) + f.ordinal + 1
    from public.guild_tech_families f
   where f.id = p_family;
$$;

create or replace function public.guild_tech_buy_rung(p_family text)
returns table (level integer, xp bigint, members integer, rungs jsonb, talents jsonb)
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid   uuid := auth.uid();
  mine  uuid;
  have  integer;
  want  integer;
  cost  integer;
  needs integer;
  purse bigint;
begin
  if uid is null then
    raise exception 'guild_tech_buy_rung requires a signed-in user' using errcode = 'PT401';
  end if;
  -- FOR UPDATE first, guild_path_unlock's reason: two purchases from one account in one instant
  -- queue here, and the second sees the first's cost in the balance.
  perform 1 from public.profiles p where p.user_id = uid for update;
  if not found then
    raise exception 'no profile for this account' using errcode = 'PT403';
  end if;
  perform public.guild_path_settle();

  if not exists (select 1 from public.guild_tech_families f where f.id = p_family) then
    raise exception 'there is no guild tech track called %', coalesce(p_family, 'nothing') using errcode = 'PT404';
  end if;

  select guild_id into mine from public.guild_members where user_id = uid;
  perform public.guild_tech_settle(mine);

  select coalesce(r.rungs, 0) into have from public.guild_tech_rungs r
   where r.user_id = uid and r.family_id = p_family;
  have := coalesce(have, 0);
  if have >= public.guild_tech_rungs_per_family() then
    raise exception 'every rung on % is bought', p_family using errcode = 'PT409';
  end if;

  want := have + 1;
  cost := want * public.guild_tech_rung_unit();
  needs := public.guild_tech_rung_level(p_family, want);
  if needs > coalesce((select l.level from public.guild_tech_levels l where l.guild_id = mine), 0) then
    raise exception 'rung % on % needs guild level %', want, p_family, needs using errcode = 'PT409';
  end if;

  purse := public.guild_path_balance(uid);
  if purse < cost then
    raise exception 'rung % on % costs % Waymarks and you have %', want, p_family, cost, purse using errcode = 'PT409';
  end if;

  insert into public.guild_tech_rungs (user_id, family_id, rungs, spent)
  values (uid, p_family, want, cost)
  on conflict (user_id, family_id) do update
    set rungs = excluded.rungs,
        spent = public.guild_tech_rungs.spent + excluded.spent,
        updated_at = now();

  return query select * from public.guild_tech_state();
end;
$$;

-- ---------------------------------------------------------------- picking a talent

-- G4: the guild picks, not the member, and the pick takes effect AT CYCLE ROLL. Leadership only -
-- a leader or an officer - because it is the one decision in the tree that is everybody's to live
-- with, and it is the thing a leader actually gets to lead.
--
-- The new pick is written against the NEXT cycle, so this week's fights are fought with the talent
-- the guild committed to last week. Re-picking before the cycle rolls replaces the pending row
-- rather than stacking one, which is what the primary key on (guild, family, from_cycle) does.
create or replace function public.guild_tech_pick_talent(p_family text, p_talent text)
returns table (level integer, xp bigint, members integer, rungs jsonb, talents jsonb)
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid  uuid := auth.uid();
  mine uuid;
  mrol text;
  slot integer;
  nxt  integer := public.guild_dungeon_cycle_at(now()) + 1;
begin
  if uid is null then
    raise exception 'guild_tech_pick_talent requires a signed-in user' using errcode = 'PT401';
  end if;
  select guild_id, role into mine, mrol from public.guild_members where user_id = uid;
  if mine is null then
    raise exception 'you are not in a guild' using errcode = 'PT403';
  end if;
  if mrol not in ('leader', 'officer') then
    raise exception 'only a leader or an officer sets the guild talents' using errcode = 'PT403';
  end if;
  if not exists (select 1 from public.guild_tech_talents t where t.id = p_talent and t.family_id = p_family) then
    raise exception '% is not a talent on the % track', coalesce(p_talent, 'nothing'), p_family using errcode = 'PT404';
  end if;

  perform public.guild_tech_settle(mine);
  slot := public.guild_tech_slot_level(p_family);
  if slot > coalesce((select l.level from public.guild_tech_levels l where l.guild_id = mine), 0) then
    raise exception 'the % talent slot opens at guild level %', p_family, slot using errcode = 'PT409';
  end if;

  insert into public.guild_tech_picks (guild_id, family_id, talent_id, from_cycle, set_by)
  values (mine, p_family, p_talent, nxt, uid)
  on conflict (guild_id, family_id, from_cycle) do update
    set talent_id = excluded.talent_id, set_by = excluded.set_by, created_at = now();

  return query select * from public.guild_tech_state();
end;
$$;

-- ---------------------------------------------------------------- the dev override

-- SETS A GUILD'S LEVEL AND XP DIRECTLY, so a two-person test guild can sit at the ceiling and see
-- the whole tree without playing a year of cycles.
--
-- IT IS NOT GRANTED TO ANY CLIENT ROLE. Both `anon` and `authenticated` are revoked below and
-- nothing grants it back, so it is reachable only from the SQL editor or the service role - which
-- is exactly the level of access "change a guild's progression by hand" should need. It is in the
-- migration rather than in a script because a test project needs it at the same moment the tables
-- exist, and a loose script is the thing that goes missing.
--
-- The XP it writes is a FLOOR, not the truth: guild_tech_xp is derived from the ledger and cannot
-- be set. What this really does is raise the stored high-water level, which is the thing that
-- gates. Lowering is allowed here and only here, so a tester can walk a guild back down.
create or replace function public.guild_tech_dev_set(p_guild uuid, p_level integer)
returns integer
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  want integer := greatest(0, least(coalesce(p_level, 0), public.guild_tech_level_ceiling()));
begin
  if p_guild is null then
    raise exception 'guild_tech_dev_set needs a guild id' using errcode = 'PT400';
  end if;
  insert into public.guild_tech_levels (guild_id, level) values (p_guild, want)
  on conflict (guild_id) do update set level = excluded.level, updated_at = now();
  return want;
end;
$$;

-- ---------------------------------------------------------------- privileges

revoke all on function public.guild_tech_rung_unit() from public, anon;
revoke all on function public.guild_tech_xp_per_level() from public, anon;
revoke all on function public.guild_tech_level_ceiling() from public, anon;
revoke all on function public.guild_tech_rungs_per_family() from public, anon;
revoke all on function public.guild_tech_xp(uuid) from public, anon;
revoke all on function public.guild_tech_xp_for_level(integer) from public, anon;
revoke all on function public.guild_tech_settle(uuid) from public, anon;
revoke all on function public.guild_tech_rung_level(text, integer) from public, anon;
revoke all on function public.guild_tech_slot_level(text) from public, anon;
revoke all on function public.guild_tech_state() from public, anon;
revoke all on function public.guild_tech_buy_rung(text) from public, anon;
revoke all on function public.guild_tech_pick_talent(text, text) from public, anon;
revoke all on function public.guild_tech_dev_set(uuid, integer) from public, anon;

-- settle and xp stay closed to clients: settle writes, and both take a guild id, which no client
-- should be able to name. They are reached through guild_tech_state, which asks auth.uid() which
-- guild that is.
revoke execute on function public.guild_tech_settle(uuid) from authenticated;
revoke execute on function public.guild_tech_xp(uuid) from authenticated;
-- The dev override is not a client feature. Service role and the SQL editor only.
revoke execute on function public.guild_tech_dev_set(uuid, integer) from authenticated;

grant execute on function public.guild_tech_rung_unit() to authenticated;
grant execute on function public.guild_tech_xp_per_level() to authenticated;
grant execute on function public.guild_tech_level_ceiling() to authenticated;
grant execute on function public.guild_tech_rungs_per_family() to authenticated;
grant execute on function public.guild_tech_xp_for_level(integer) to authenticated;
grant execute on function public.guild_tech_rung_level(text, integer) to authenticated;
grant execute on function public.guild_tech_slot_level(text) to authenticated;
grant execute on function public.guild_tech_state() to authenticated;
grant execute on function public.guild_tech_buy_rung(text) to authenticated;
grant execute on function public.guild_tech_pick_talent(text, text) to authenticated;
