-- 0011_guild_path.sql - the Guild Path: what the guild dungeon pays, and what it buys.
-- claude/DESIGN-GUILDS-PHASE-TWO.md, system 2.
--
-- Run after 0010. RE-APPLIABLE, like 0004-0010: tables and indexes are `if not exists`, columns
-- are `add column if not exists`, policies are dropped before they are created, functions are
-- `create or replace`, and the node rows are upserted. 0007 and 0008 are applied and are not
-- edited; everything here is additive to them.
--
-- ---------------------------------------------------------------- what this adds
--
--   * guild_dungeon_attempts.boards - which of the six boards an attempt entered, as bits.
--   * guild_path_nodes    - the Path: every node, its price and what it requires. Read-only.
--   * guild_path_ledger   - every Waymark ever paid. One row per (member, cycle, kind).
--   * guild_path_unlocks  - every node a member bought, at the price they paid.
--   * guild_dungeon_attempt(...) - guild_dungeon_submit's rules, plus the board bits, plus the
--     base pay. What the client calls now.
--   * guild_path_state()  - settles every closed cycle's board bonus, then returns the balance
--     and the nodes owned.
--   * guild_path_unlock(node) - spends.
--
-- ---------------------------------------------------------------- who can mint
--
-- NOBODY BUT THIS FILE'S FUNCTIONS. The ledger has no client write grant of any kind, and a row
-- gets in only three ways: guild_dungeon_attempt pays 'attempt' and 'clear' as the attempt is
-- stored, and guild_path_state pays 'boards' for a cycle that has closed. The client never sends
-- an amount. The unique key (user_id, cycle, kind) makes each payment a constraint rather than a
-- habit, so two requests in one instant cannot both pay it.
--
-- WHAT A MODIFIED CLIENT CAN STILL DO, stated rather than implied. Every field of an attempt is
-- still the client's word - 0007 said so and nothing here changes it - so a client can claim a
-- clear it did not win, or a board it did not enter. What it cannot do is be paid more than an
-- honest member who won everything: the attempt cap bounds the rows, each kind pays once a
-- cycle, and the board bonus counts at most guild_path_boards_counted() placements. The most any
-- account can be paid in a cycle is attempt + clear + board_point x boards_counted, whatever it
-- sends. Four of the six board bits are also checked against the row itself (below), so those
-- four cannot be claimed falsely at all.
--
-- KEYED BY ACCOUNT, NEVER BY GUILD. The ledger and the unlocks reference profiles, not guilds:
-- leaving a guild, being removed from one or watching one disband takes nothing away. That is
-- the property that stops the Path becoming guild lock-in.
--
-- ---------------------------------------------------------------- the constants
--
-- Repeated in Assets/Resources/Content/guild-path.json, the same duplication 0007's attempt cap
-- lives under. Tools/DevHarness/GuildPathChecks.cs reads both and fails the build-check when they
-- disagree. Every number is derived by `DevHarness.exe --guild-path`; the working is in the
-- design record. The bodies must stay the single-line `select <n>;` form, which is what the
-- harness parses.

create or replace function public.guild_path_pay_attempt()
returns integer
language sql
immutable
as $$ select 40; $$;

create or replace function public.guild_path_pay_clear()
returns integer
language sql
immutable
as $$ select 40; $$;

create or replace function public.guild_path_pay_board_point()
returns integer
language sql
immutable
as $$ select 15; $$;

create or replace function public.guild_path_boards_counted()
returns integer
language sql
immutable
as $$ select 6; $$;

create or replace function public.guild_path_board_floor()
returns integer
language sql
immutable
as $$ select 1; $$;

-- ---------------------------------------------------------------- the boards an attempt entered

-- One bit per board, 1 shifted by the board's place in GuildDungeonBoard:
--   1 four or fewer, 2 no five stars, 4 one aspect, 8 no healer, 16 first attempt, 32 nobody fell.
-- The client computes it (GuildPath.BoardMask) because two of the six need content the server
-- does not hold - an aspect and a role - and so does a floor condition's suspension (C7). NULL on
-- every row written before this file, and a NULL row enters no board for pay.
alter table public.guild_dungeon_attempts
  add column if not exists boards smallint
    check (boards is null or boards between 0 and 63);

-- ---------------------------------------------------------------- the Path

create table if not exists public.guild_path_nodes (
  id       text primary key check (id ~ '^[a-z][a-z0-9-]{1,31}$'),
  cost     integer not null check (cost > 0),
  requires text references public.guild_path_nodes (id)
);

-- The rows, as guild-path.json authors them. Upserted, so re-applying this file after a price
-- change is how a price changes. Parents before children, for the foreign key.
insert into public.guild_path_nodes (id, cost, requires) values
  ('second-line', 100, null),
  ('third-line', 200, 'second-line'),
  ('line-talents', 200, 'second-line'),
  ('fourth-line', 300, 'third-line')
on conflict (id) do update set cost = excluded.cost, requires = excluded.requires;

create table if not exists public.guild_path_ledger (
  id         bigint generated always as identity primary key,
  user_id    uuid not null references public.profiles (user_id) on delete cascade,
  cycle      integer not null check (cycle >= 0),
  kind       text not null check (kind in ('attempt', 'clear', 'boards')),
  amount     integer not null check (amount >= 0),
  created_at timestamptz not null default now(),
  constraint guild_path_ledger_once unique (user_id, cycle, kind)
);

create table if not exists public.guild_path_unlocks (
  user_id    uuid not null references public.profiles (user_id) on delete cascade,
  node_id    text not null references public.guild_path_nodes (id),
  cost       integer not null check (cost > 0),
  created_at timestamptz not null default now(),
  primary key (user_id, node_id)
);

-- ---------------------------------------------------------------- privileges and policy
--
-- Two locks, as everywhere. No client role writes any of the three tables; the nodes are read by
-- everyone signed in, the ledger and the unlocks by their owner only.
alter table public.guild_path_nodes enable row level security;
alter table public.guild_path_ledger enable row level security;
alter table public.guild_path_unlocks enable row level security;

revoke all on public.guild_path_nodes from anon, authenticated;
revoke all on public.guild_path_ledger from anon, authenticated;
revoke all on public.guild_path_unlocks from anon, authenticated;
grant select on public.guild_path_nodes to authenticated;
grant select on public.guild_path_ledger to authenticated;
grant select on public.guild_path_unlocks to authenticated;

drop policy if exists guild_path_nodes_read on public.guild_path_nodes;
create policy guild_path_nodes_read
  on public.guild_path_nodes for select
  to authenticated
  using (true);

drop policy if exists guild_path_ledger_read_own on public.guild_path_ledger;
create policy guild_path_ledger_read_own
  on public.guild_path_ledger for select
  to authenticated
  using (user_id = auth.uid());

drop policy if exists guild_path_unlocks_read_own on public.guild_path_unlocks;
create policy guild_path_unlocks_read_own
  on public.guild_path_unlocks for select
  to authenticated
  using (user_id = auth.uid());

-- ---------------------------------------------------------------- recording an attempt, and paying for it

-- guild_dungeon_submit's rules, ALL OF THEM, by calling it: the cycle, the floor gate, the cap,
-- the normalised line. One copy of those rules, not two. Then the board bits, then the base.
--
-- THE BITS ARE THE CLIENT'S CLAIM, CUT DOWN TO WHAT THE ROW CAN SUPPORT: a loss enters no board;
-- four or fewer needs at most four units; no five stars needs no unit at five stars; first
-- attempt needs attempt 1 as the server numbered it; nobody fell needs fallen = 0. One aspect and
-- no healer are kept as claimed - the server holds no aspect and no role.
--
-- THE BASE IS SCORED ON THE CALLER'S OWN ATTEMPT AND ON NO PARTICULAR FLOOR: the first attempt
-- of a cycle pays guild_path_pay_attempt(), won or lost, and the first clear of a cycle pays
-- guild_path_pay_clear(). A deeper floor pays no more, because floor N opens only when the guild
-- clears floor N-1, and pay the rest of the guild unlocked is the collective gating the design
-- record rules out. Floor 1 is always open, so every member can earn the whole base.
--
-- Named differently from guild_dungeon_submit rather than overloading it, for 0008's reason:
-- PostgREST picks an overload by argument names.
create or replace function public.guild_dungeon_attempt(
  p_cycle           integer,
  p_floor           integer,
  p_seed            integer,
  p_team            jsonb,
  p_victory         boolean,
  p_turns           integer,
  p_power           integer,
  p_avg_stars       numeric,
  p_fallen          integer,
  p_content_version text,
  p_boards          integer
)
returns public.guild_dungeon_attempts
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid    uuid := auth.uid();
  result public.guild_dungeon_attempts;
  bits   integer;
begin
  if p_boards is null or p_boards < 0 or p_boards > 63 then
    raise exception 'boards is a bit mask from 0 to 63' using errcode = 'PT400';
  end if;

  result := public.guild_dungeon_submit(p_cycle, p_floor, p_seed, p_team, p_victory, p_turns,
                                        p_power, p_avg_stars, p_fallen, p_content_version);

  bits := case when result.victory then p_boards else 0 end;
  if jsonb_array_length(result.team) > 4 then bits := bits & ~1; end if;
  if exists (select 1 from jsonb_array_elements(result.team) u
              where jsonb_typeof(u -> 'stars') is distinct from 'number'
                 or (u ->> 'stars')::numeric >= 5) then
    bits := bits & ~2;
  end if;
  if result.attempt <> 1 then bits := bits & ~16; end if;
  if result.fallen is distinct from 0 then bits := bits & ~32; end if;

  update public.guild_dungeon_attempts set boards = bits where id = result.id
  returning * into result;

  insert into public.guild_path_ledger (user_id, cycle, kind, amount)
  values (uid, result.cycle, 'attempt', public.guild_path_pay_attempt())
  on conflict (user_id, cycle, kind) do nothing;

  if result.victory then
    insert into public.guild_path_ledger (user_id, cycle, kind, amount)
    values (uid, result.cycle, 'clear', public.guild_path_pay_clear())
    on conflict (user_id, cycle, kind) do nothing;
  end if;

  return result;
end;
$$;

-- ---------------------------------------------------------------- the board bonus

-- Pays every CLOSED cycle's board bonus the caller has not been paid yet, and records a zero for
-- one that earned nothing so it is never computed again. Not callable by a client: it runs at
-- the top of guild_path_state, so "settle, then tell me" is one round trip.
--
-- A cycle pays once it has ended because a board is not final until then - a placement at noon
-- can be lost at one o'clock.
--
-- ONLY guild_path_board_floor()'s BOARDS PAY, for the base's reason: deeper boards exist only in
-- guilds that opened deeper floors. The rest are still boards, and still recognition.
--
-- A PLACE IS WORTH THE SHARE OF THE BOARD IT BEAT: (n - rank) / (n - 1) among the n members on
-- the board, first 1 and last 0, and a board of one pays nothing. Averaged over a board's
-- entrants that is one half at every size, so a member of a big guild and of a small one are paid
-- the same for the same standing. Each member's entry on a board is their best qualifying row,
-- and ranking is GuildDungeon.Rank's order: fewest turns, fewest fallen (none recorded counts as
-- the most), earliest attempt, earliest row, user id. The best guild_path_boards_counted() scores
-- of the cycle are paid - one guild's worth, whatever guilds the caller fought for that week.
create or replace function public.guild_path_settle()
returns void
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid       uuid := auth.uid();
  now_cycle integer := public.guild_dungeon_cycle_at(now());
  on_floor  integer := public.guild_path_board_floor();
begin
  if uid is null then
    raise exception 'guild_path_settle requires a signed-in user' using errcode = 'PT401';
  end if;

  with owed as (
    select distinct a.cycle
      from public.guild_dungeon_attempts a
     where a.user_id = uid and a.cycle < now_cycle and a.floor = on_floor
       and not exists (select 1 from public.guild_path_ledger l
                        where l.user_id = uid and l.cycle = a.cycle and l.kind = 'boards')
  ),
  fought as (
    select distinct a.guild_id, a.cycle
      from public.guild_dungeon_attempts a
     where a.user_id = uid and a.floor = on_floor and a.cycle in (select cycle from owed)
  ),
  bits (bit) as (values (1), (2), (4), (8), (16), (32)),
  entries as (
    select a.guild_id, a.cycle, b.bit, a.user_id, a.turns, a.fallen, a.attempt, a.id,
           row_number() over (partition by a.guild_id, a.cycle, b.bit, a.user_id
                              order by a.turns, coalesce(a.fallen, 32767), a.attempt, a.id) as pick
      from public.guild_dungeon_attempts a
      join fought f on f.guild_id = a.guild_id and f.cycle = a.cycle
      join bits b on (coalesce(a.boards, 0) & b.bit) <> 0
     where a.victory and a.floor = on_floor
  ),
  ranked as (
    select guild_id, cycle, bit, user_id,
           row_number() over (partition by guild_id, cycle, bit
                              order by turns, coalesce(fallen, 32767), attempt, id, user_id) as place,
           count(*) over (partition by guild_id, cycle, bit) as n
      from entries
     where pick = 1
  ),
  scores as (
    select cycle,
           case when n < 2 then 0::numeric else (n - place)::numeric / (n - 1) end as score
      from ranked
     where user_id = uid
  ),
  best as (
    select cycle, score,
           row_number() over (partition by cycle order by score desc) as k
      from scores
  )
  insert into public.guild_path_ledger (user_id, cycle, kind, amount)
  select uid, o.cycle, 'boards',
         round(coalesce(sum(b.score) filter (where b.k <= public.guild_path_boards_counted()), 0)
               * public.guild_path_pay_board_point())::integer
    from owed o
    left join best b on b.cycle = o.cycle
   group by o.cycle
  on conflict (user_id, cycle, kind) do nothing;
end;
$$;

-- ---------------------------------------------------------------- the balance

-- Everything paid, less everything spent. Not callable by a client; the two functions below are
-- the only readers, and the client reads the answer through them.
create or replace function public.guild_path_balance(p_user uuid)
returns bigint
language sql
stable
security definer
set search_path = public, pg_temp
as $$
  select coalesce((select sum(amount) from public.guild_path_ledger where user_id = p_user), 0)
       - coalesce((select sum(cost) from public.guild_path_unlocks where user_id = p_user), 0);
$$;

-- Settles, then answers: the balance, everything ever earned, and the nodes owned. Works for a
-- player in no guild at all - the Path is theirs, and so is what it cost.
create or replace function public.guild_path_state()
returns table (balance bigint, earned bigint, nodes text[])
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid uuid := auth.uid();
begin
  if uid is null then
    raise exception 'guild_path_state requires a signed-in user' using errcode = 'PT401';
  end if;
  perform public.guild_path_settle();
  return query
    select public.guild_path_balance(uid),
           coalesce((select sum(amount) from public.guild_path_ledger where user_id = uid), 0)::bigint,
           coalesce((select array_agg(u.node_id order by u.created_at, u.node_id)
                       from public.guild_path_unlocks u where u.user_id = uid), array[]::text[]);
end;
$$;

-- ---------------------------------------------------------------- spending

-- Buys one node, at the price guild_path_nodes holds now, and answers as guild_path_state does.
-- The caller's profile row is taken FOR UPDATE first, for guild_dungeon_record's reason: two
-- purchases from one account in one instant queue here, and the second sees the first's cost in
-- the balance. The primary key on guild_path_unlocks is the guarantee against buying a node
-- twice; the lock is what makes the balance check honest.
create or replace function public.guild_path_unlock(p_node text)
returns table (balance bigint, earned bigint, nodes text[])
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  uid  uuid := auth.uid();
  node public.guild_path_nodes;
  have bigint;
begin
  if uid is null then
    raise exception 'guild_path_unlock requires a signed-in user' using errcode = 'PT401';
  end if;
  perform 1 from public.profiles p where p.user_id = uid for update;
  if not found then
    raise exception 'no profile for this account' using errcode = 'PT403';
  end if;
  perform public.guild_path_settle();

  select * into node from public.guild_path_nodes n where n.id = p_node;
  if not found then
    raise exception 'there is no Path node called %', coalesce(p_node, 'nothing') using errcode = 'PT404';
  end if;
  if exists (select 1 from public.guild_path_unlocks u where u.user_id = uid and u.node_id = node.id) then
    raise exception 'you already own %', node.id using errcode = 'PT409';
  end if;
  if node.requires is not null and not exists (
       select 1 from public.guild_path_unlocks u where u.user_id = uid and u.node_id = node.requires) then
    raise exception '% needs % first', node.id, node.requires using errcode = 'PT409';
  end if;
  have := public.guild_path_balance(uid);
  if have < node.cost then
    raise exception '% costs % Waymarks and you have %', node.id, node.cost, have using errcode = 'PT409';
  end if;

  insert into public.guild_path_unlocks (user_id, node_id, cost) values (uid, node.id, node.cost);
  return query select * from public.guild_path_state();
end;
$$;

-- ---------------------------------------------------------------- privileges

revoke all on function public.guild_path_pay_attempt() from public, anon;
revoke all on function public.guild_path_pay_clear() from public, anon;
revoke all on function public.guild_path_pay_board_point() from public, anon;
revoke all on function public.guild_path_boards_counted() from public, anon;
revoke all on function public.guild_path_board_floor() from public, anon;
revoke all on function public.guild_dungeon_attempt(integer, integer, integer, jsonb, boolean, integer,
  integer, numeric, integer, text, integer) from public, anon;
revoke all on function public.guild_path_settle() from public, anon;
revoke all on function public.guild_path_balance(uuid) from public, anon;
revoke all on function public.guild_path_state() from public, anon;
revoke all on function public.guild_path_unlock(text) from public, anon;

-- settle and balance stay closed to clients: settle is reached through state, and balance takes
-- a user id, which no client should be able to name.
revoke execute on function public.guild_path_settle() from authenticated;
revoke execute on function public.guild_path_balance(uuid) from authenticated;

grant execute on function public.guild_path_pay_attempt() to authenticated;
grant execute on function public.guild_path_pay_clear() to authenticated;
grant execute on function public.guild_path_pay_board_point() to authenticated;
grant execute on function public.guild_path_boards_counted() to authenticated;
grant execute on function public.guild_path_board_floor() to authenticated;
grant execute on function public.guild_dungeon_attempt(integer, integer, integer, jsonb, boolean, integer,
  integer, numeric, integer, text, integer) to authenticated;
grant execute on function public.guild_path_state() to authenticated;
grant execute on function public.guild_path_unlock(text) to authenticated;

-- ---------------------------------------------------------------- guild_dungeon_submit
--
-- LEFT CALLABLE, for 0008's reason about guild_dungeon_record: Tools/supabase-guild-rules-test.py
-- exercises it, and this client no longer calls it. A row it writes carries no board bits and
-- pays nothing, so leaving it open mints nothing. The revoke belongs in the migration after the
-- rules test moves to guild_dungeon_attempt:
--
--   revoke execute on function public.guild_dungeon_submit(integer, integer, integer, jsonb,
--     boolean, integer, integer, numeric, integer, text) from authenticated;
