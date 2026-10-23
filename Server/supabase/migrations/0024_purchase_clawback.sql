-- 0024_purchase_clawback.sql - a refunded purchase takes back its unspent goods, on the server.
-- Docs/DESIGN-RECEIPT-VALIDATION.md, section 4, and Docs/SECURITY-REVIEW-SUPABASE.md, finding L1.
--
-- THE OWNER'S DECISION (2026-10-23): refunds claw back unspent goods, server side. Review decision 1,
-- option 2, made a server rule rather than a client one.
--
-- WRITTEN, NOT APPLIED to any project. Applied to a local PostgreSQL with stand-in Supabase roles
-- (Server/supabase/local, Tools/supabase-local-check.sh) and exercised there.
--
-- RUN AFTER 0023 (the files go in number order); it needs 0002 (saves) and 0019 (purchase_receipts).
-- RE-APPLIABLE: the table is `if not exists`, every function is `create or replace`, every trigger and
-- policy is dropped before it is created, and the grants are revoked and re-granted to the same end state.
--
-- ---------------------------------------------------------------- the shape of the decision
--
-- WHAT A PURCHASE GAVE is written by the game into the purchase's own receipt in the save
-- (`store.receipts[]`, PurchaseReceipt in StoreState.cs): its Sealed Gold (`gold`), its Sealed Slag
-- (`slag`), its Relic Dust (`dust`) and the hero a Sealed copy went to (`hero`). The server never guesses
-- an amount: an offer paid in hours of the buyer's own idle Gold has no fixed size it could know.
--
-- WHAT IS UNSPENT is what a refunder still holds of exactly that:
--   gold   the least of what the purchase gave, the Sealed part of the Gold purse, and the purse itself.
--          Sealed Gold only ever leaves through a deterministic sink, Sealed part first (SpendGoldDeterministic),
--          so a Sealed part that is still there is bought Gold that was never spent.
--   slag   the same, against the Slag purse and its Sealed part.
--   dust   the least of what the purchase gave and the Dust purse. Dust carries no Sealed mark (its only
--          sink sells fixed relics), so this is the one good where earned and bought cannot be told apart;
--          it never takes more than the purchase gave.
--   hero   one Sealed copy of that hero, if the hero holds a Sealed copy AND more than one copy. A hero whose
--          only copy is the bought one is left in the roster: removing a hero from a line, a guild snapshot
--          or a talent build is a game operation, not a JSON edit. The clawback row says `hero_kept`.
-- Relics (Relic of Choice), skins, the paid tracks and the Ember Ledger's and Growth Fund's later payouts
-- are not taken back here; each is listed for the owner in Docs/DESIGN-RECEIPT-VALIDATION.md.
--
-- WHERE IT HAPPENS. The cloud save is the server's copy of the game (0002). Two moments:
--   1. When a receipt is revoked (purchase_receipt_revoke, called by google-play-rtdn on a refund or a
--      cancellation), a clawback row is filed and the stored save is clawed back at once. Its revision moves
--      on, so the next sync of the device that bought it sees a cloud save newer than its own (SyncPlanner).
--   2. Every save written afterwards for that account (push_save, by insert or update) passes through the
--      same rule before it is stored. A device that keeps its own copy and pushes it again still lands a
--      clawed-back save in the cloud.
-- THE MARK. A receipt that was clawed back carries `"clawedBack": true` and what was taken (`taken`). The
-- game keeps the mark when it reads and writes the save (StoreState), so a save is clawed back once, never
-- twice, whichever device and whichever copy it came from.
--
-- MATCHING A RECEIPT. The game keys a receipt by the transaction id the store gave it. For Google Play that
-- is the order id or the purchase token, depending on the Unity IAP version, so a save receipt matches a
-- revoked one when its id equals the order id OR its SHA-256 equals the stored token hash.
--
-- THE BELGIAN RULE: nothing here draws, rolls or picks. Every amount is a least() of numbers in the save.

-- ---------------------------------------------------------------- the clawback ledger

create table if not exists public.purchase_clawbacks (
  receipt_id      bigint primary key references public.purchase_receipts (id) on delete cascade,
  user_id         uuid not null references auth.users (id) on delete cascade,
  offer_id        text not null,
  order_id        text,
  token_sha256    text not null check (token_sha256 ~ '^[0-9a-f]{64}$'),
  created_at      timestamptz not null default now(),
  applications    integer not null default 0 check (applications >= 0),
  last_applied_at timestamptz,
  last_result     text not null default 'filed'
                  check (last_result in ('filed', 'taken', 'already_taken', 'not_in_save', 'no_save')),
  last_taken      jsonb not null default '{}'::jsonb check (jsonb_typeof(last_taken) = 'object')
);

create index if not exists purchase_clawbacks_user_idx on public.purchase_clawbacks (user_id);

alter table public.purchase_clawbacks enable row level security;

revoke all on public.purchase_clawbacks from anon, authenticated;
-- A player may see that a refunded purchase was taken back, and what was taken. Nothing else.
grant select (receipt_id, offer_id, created_at, last_applied_at, last_result, last_taken)
  on public.purchase_clawbacks to authenticated;

drop policy if exists purchase_clawbacks_read_own on public.purchase_clawbacks;
create policy purchase_clawbacks_read_own
  on public.purchase_clawbacks for select
  to authenticated
  using (user_id = auth.uid());

-- ---------------------------------------------------------------- the rule, on one document

-- A whole, non-negative number from the save, or 0. A string, a fraction or a negative reads as 0, so a
-- damaged field can only make the clawback smaller.
create or replace function public.purchase_clawback_num(p jsonb)
returns bigint
language sql
immutable
set search_path = public, pg_temp
as $$
  select case
           when p is null or jsonb_typeof(p) <> 'number' then 0::bigint
           when (p #>> '{}')::numeric < 0 then 0::bigint
           else least(floor((p #>> '{}')::numeric), 1000000000000)::bigint
         end
$$;

-- Applies ONE refunded receipt to ONE save document and answers
--   {"result": "taken" | "already_taken" | "not_in_save", "doc": <the document>, "taken": {...}}
-- The document comes back unchanged unless the result is "taken".
create or replace function public.purchase_clawback_take(p_doc jsonb, p_order_id text, p_token_sha256 text)
returns jsonb
language plpgsql
immutable
set search_path = public, pg_temp
as $$
declare
  receipts   jsonb := p_doc #> '{store,receipts}';
  heroes     jsonb := p_doc -> 'heroes';
  r          jsonb;
  h          jsonb;
  idx        integer := -1;
  i          integer;
  rid        text;
  doc        jsonb := p_doc;
  give_gold  bigint;
  give_slag  bigint;
  give_dust  bigint;
  hero_id    text;
  has_sealed boolean := jsonb_typeof(p_doc -> 'sealed') = 'object';
  purse      bigint;
  part       bigint;
  take_gold  bigint := 0;
  take_slag  bigint := 0;
  take_dust  bigint := 0;
  take_hero  integer := 0;
  hero_kept  boolean := false;
  copies     bigint;
  sealed_cp  bigint;
  taken      jsonb;
begin
  if jsonb_typeof(p_doc) <> 'object' or receipts is null or jsonb_typeof(receipts) <> 'array' then
    return jsonb_build_object('result', 'not_in_save', 'doc', p_doc, 'taken', '{}'::jsonb);
  end if;

  for i in 0 .. jsonb_array_length(receipts) - 1 loop
    r := receipts -> i;
    continue when jsonb_typeof(r) <> 'object' or jsonb_typeof(r -> 'id') <> 'string';
    rid := r ->> 'id';
    if (p_order_id is not null and p_order_id <> '' and rid = p_order_id)
       or encode(sha256(convert_to(rid, 'UTF8')), 'hex') = p_token_sha256 then
      idx := i;
      exit;
    end if;
  end loop;

  if idx < 0 then
    return jsonb_build_object('result', 'not_in_save', 'doc', p_doc, 'taken', '{}'::jsonb);
  end if;
  if r -> 'clawedBack' = 'true'::jsonb then
    return jsonb_build_object('result', 'already_taken', 'doc', p_doc, 'taken', coalesce(r -> 'taken', '{}'::jsonb));
  end if;

  give_gold := public.purchase_clawback_num(r -> 'gold');
  give_slag := public.purchase_clawback_num(r -> 'slag');
  give_dust := public.purchase_clawback_num(r -> 'dust');
  hero_id   := case when jsonb_typeof(r -> 'hero') = 'string' then r ->> 'hero' else '' end;

  -- Gold and Slag: the unspent Sealed part, never more than the purchase gave.
  if has_sealed then
    purse := public.purchase_clawback_num(doc -> 'gold');
    part  := least(public.purchase_clawback_num(doc #> '{sealed,gold}'), purse);
    take_gold := least(give_gold, part);
    if take_gold > 0 then
      doc := jsonb_set(doc, '{gold}', to_jsonb(purse - take_gold));
      doc := jsonb_set(doc, '{sealed,gold}', to_jsonb(public.purchase_clawback_num(doc #> '{sealed,gold}') - take_gold));
    end if;

    purse := public.purchase_clawback_num(doc -> 'slag');
    part  := least(public.purchase_clawback_num(doc #> '{sealed,slag}'), purse);
    take_slag := least(give_slag, part);
    if take_slag > 0 then
      doc := jsonb_set(doc, '{slag}', to_jsonb(purse - take_slag));
      doc := jsonb_set(doc, '{sealed,slag}', to_jsonb(public.purchase_clawback_num(doc #> '{sealed,slag}') - take_slag));
    end if;
  end if;

  -- Dust: no Sealed mark, so the purse itself, never more than the purchase gave.
  purse := public.purchase_clawback_num(doc -> 'relicDust');
  take_dust := least(give_dust, purse);
  if take_dust > 0 then
    doc := jsonb_set(doc, '{relicDust}', to_jsonb(purse - take_dust));
  end if;

  -- One Sealed hero copy, only from a hero that keeps at least one copy afterwards.
  if hero_id <> '' and heroes is not null and jsonb_typeof(heroes) = 'array' then
    for i in 0 .. jsonb_array_length(heroes) - 1 loop
      h := heroes -> i;
      continue when jsonb_typeof(h) <> 'object' or h ->> 'defId' is distinct from hero_id;
      sealed_cp := public.purchase_clawback_num(h -> 'sealedCopies');
      copies    := greatest(1, public.purchase_clawback_num(h -> 'copies'));
      continue when sealed_cp <= 0;
      if copies > 1 then
        h := jsonb_set(h, '{copies}', to_jsonb(copies - 1));
        if sealed_cp - 1 > 0 then
          h := jsonb_set(h, '{sealedCopies}', to_jsonb(sealed_cp - 1));
        else
          h := h - 'sealedCopies';
        end if;
        doc := jsonb_set(doc, array['heroes', i::text], h);
        take_hero := 1;
      else
        hero_kept := true;
      end if;
      exit;
    end loop;
  end if;

  taken := jsonb_build_object('gold', take_gold, 'slag', take_slag, 'dust', take_dust,
                              'heroCopies', take_hero, 'heroKept', hero_kept);
  doc := jsonb_set(doc, array['store', 'receipts', idx::text],
                   r || jsonb_build_object('clawedBack', true, 'taken', taken));
  return jsonb_build_object('result', 'taken', 'doc', doc, 'taken', taken);
end;
$$;

-- Applies every refunded receipt of an account to one document, and notes each result on its row.
create or replace function public.purchase_clawback_apply_all(p_user uuid, p_doc jsonb)
returns jsonb
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  c   public.purchase_clawbacks;
  res jsonb;
  doc jsonb := p_doc;
begin
  for c in select * from public.purchase_clawbacks where user_id = p_user order by created_at, receipt_id loop
    res := public.purchase_clawback_take(doc, c.order_id, c.token_sha256);
    doc := res -> 'doc';
    if res ->> 'result' = 'taken' then
      update public.purchase_clawbacks
         set applications = applications + 1, last_applied_at = now(),
             last_result = 'taken', last_taken = res -> 'taken'
       where receipt_id = c.receipt_id;
    elsif c.last_result in ('filed', 'no_save') then
      update public.purchase_clawbacks set last_result = res ->> 'result' where receipt_id = c.receipt_id;
    end if;
  end loop;
  return doc;
end;
$$;

-- ---------------------------------------------------------------- moment 2: every save written afterwards

create or replace function public.saves_apply_clawbacks()
returns trigger
language plpgsql
security definer
set search_path = public, pg_temp
as $$
begin
  if exists (select 1 from public.purchase_clawbacks c where c.user_id = new.user_id) then
    new.data := public.purchase_clawback_apply_all(new.user_id, new.data);
  end if;
  return new;
end;
$$;

drop trigger if exists saves_clawback on public.saves;
create trigger saves_clawback
  before insert or update on public.saves
  for each row execute function public.saves_apply_clawbacks();

-- ---------------------------------------------------------------- moment 1: the refund itself

create or replace function public.purchase_receipts_file_clawback()
returns trigger
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  stored jsonb;
  clawed jsonb;
begin
  if old.revoked_at is not null or new.revoked_at is null or new.user_id is null then
    return new;
  end if;

  insert into public.purchase_clawbacks (receipt_id, user_id, offer_id, order_id, token_sha256)
  values (new.id, new.user_id, new.offer_id, new.order_id, new.purchase_token_sha256)
  on conflict (receipt_id) do nothing;

  select s.data into stored from public.saves s where s.user_id = new.user_id for update;
  if not found then
    update public.purchase_clawbacks set last_result = 'no_save' where receipt_id = new.id;
    return new;
  end if;

  clawed := public.purchase_clawback_apply_all(new.user_id, stored);
  if clawed is distinct from stored then
    -- The revision moves on so the buying device sees a newer cloud save on its next sync. The saves
    -- trigger runs again on this update and finds every receipt already marked, so it changes nothing.
    update public.saves
       set data = clawed, revision = revision + 1, updated_at = now()
     where user_id = new.user_id;
  end if;
  return new;
end;
$$;

drop trigger if exists purchase_receipts_clawback on public.purchase_receipts;
create trigger purchase_receipts_clawback
  after update of revoked_at on public.purchase_receipts
  for each row execute function public.purchase_receipts_file_clawback();

-- ---------------------------------------------------------------- no client may call any of it

revoke all on function public.purchase_clawback_num(jsonb) from public, anon, authenticated;
revoke all on function public.purchase_clawback_take(jsonb, text, text) from public, anon, authenticated;
revoke all on function public.purchase_clawback_apply_all(uuid, jsonb) from public, anon, authenticated;
revoke all on function public.saves_apply_clawbacks() from public, anon, authenticated;
revoke all on function public.purchase_receipts_file_clawback() from public, anon, authenticated;

-- ---------------------------------------------------------------- what was applied, said once

do $$
declare
  who  text;
  verb text;
begin
  foreach who in array array['anon', 'authenticated'] loop
    foreach verb in array array['insert', 'update', 'delete'] loop
      if has_table_privilege(who, 'public.purchase_clawbacks', verb) then
        raise exception '0024: % can % purchase_clawbacks', who, verb;
      end if;
    end loop;
    if has_column_privilege(who, 'public.purchase_clawbacks', 'token_sha256', 'select')
       or has_column_privilege(who, 'public.purchase_clawbacks', 'user_id', 'select') then
      raise exception '0024: % can read a clawback token hash or user id', who;
    end if;
    if has_function_privilege(who, 'public.purchase_clawback_take(jsonb, text, text)', 'execute')
       or has_function_privilege(who, 'public.purchase_clawback_apply_all(uuid, jsonb)', 'execute')
       or has_function_privilege(who, 'public.purchase_clawback_num(jsonb)', 'execute') then
      raise exception '0024: % can call a clawback function', who;
    end if;
  end loop;
  if has_any_column_privilege('anon', 'public.purchase_clawbacks', 'select') then
    raise exception '0024: anon can read purchase_clawbacks';
  end if;
  raise notice '0024: a revoked receipt claws back its unspent goods from the cloud save, and every later save of that account';
end;
$$;

notify pgrst, 'reload schema';

-- ---------------------------------------------------------------- after applying, by hand
--
--   select receipt_id, offer_id, last_result, last_taken, applications, last_applied_at
--     from public.purchase_clawbacks order by created_at desc limit 50;
