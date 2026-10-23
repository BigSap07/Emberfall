-- 0019_purchase_receipts.sql - the server's record of every Google Play purchase it has verified.
--
-- WRITTEN, NOT APPLIED. Independent of 0002 to 0018 (it references auth.users only). It is the
-- database half of Docs/DESIGN-RECEIPT-VALIDATION.md; the other half is the Edge Function in
-- Server/supabase/functions/verify-google-purchase, which is the ONLY writer.
--
-- RE-APPLIABLE: tables are `if not exists`, the seed rows are `on conflict do nothing` (so
-- re-applying never resets a product the owner activated), every policy is preceded by
-- `drop policy if exists`, functions are `create or replace`.
--
-- ---------------------------------------------------------------- the shape of the decision
--
-- THE CLIENT NEVER WRITES A RECEIPT. Not through REST, not through a function. The two writing
-- functions are granted to service_role alone, and the only holder of the service role key is the
-- Edge Function, which runs on Supabase's servers and never ships in the game. This is the first
-- piece of this project that uses the service role at all; every earlier migration was written to
-- be correct against the anon key and a user's own token, and every one of them still is.
--
-- THE TOKEN IS STORED AS A HASH. A Google purchase token is a bearer secret: with the package name
-- and Google credentials it fetches the purchase. The table keeps sha256(token) as hex, which is
-- enough to recognise the same purchase again (a retry, a restore, a refund notification, which
-- carries the token) and useless to anyone who reads the row.
--
-- ONE PURCHASE, ONE ROW, ONE OWNER. The token hash is unique, and so is the order id when Google
-- gives one. A second verification of the same token by the same player answers "already recorded"
-- and the client grants nothing new; the same token from a DIFFERENT player is refused, because a
-- purchase token moving between accounts is a shared or stolen receipt.
--
-- RECEIPTS OUTLIVE THE ACCOUNT. user_id is ON DELETE SET NULL, so 0016's account deletion removes
-- the link to the person and keeps the order record. Belgian accounting rules expect sales records
-- to be kept for years, and Google's order id is the reference for any refund dispute. How long to
-- keep them, and whether the region code and the raw Google answer should be dropped at deletion
-- too, is an owner decision with the accountant (Docs/LAUNCH-CHECKLIST.md).
--
-- THE GAME'S SAVE STAYS THE PLACE A PURCHASE BECOMES GOODS. `PurchaseGrants.Apply` on the client is
-- unchanged in design: the client grants once per receipt id after the server says "verified". The
-- server ledger is what stops a forged purchase being reported as real, what a restore can be
-- checked against, and what a refund marks. It does not make the save server-authoritative; that
-- would be a far larger change and is not proposed here.

-- ---------------------------------------------------------------- the product list
--
-- Which Google product ids the server will verify, and which store offer each one is. A product
-- that is not here, or is not active, is refused before Google is even asked.
--
-- `kind` decides what the Edge Function does after verifying: a non_consumable (bought once,
-- restorable) is acknowledged by the server; a consumable is left for the client to consume through
-- Unity IAP's ConfirmPendingPurchase, which also acknowledges it.
--
-- SEEDED INACTIVE WITH THE PLACEHOLDER IDS from Assets/Resources/Content/store.json. Every product
-- id there is a placeholder and no product exists in the Play Console yet. When the owner creates
-- the real products, the update per offer is:
--
--     update public.store_products
--        set product_id = 'emberfall.starter_pact', active = true, updated_at = now()
--      where offer_id = 'starter_pact';
--
-- and store.json's productId must say the same thing, or the client will ask for a product the
-- server refuses.
create table if not exists public.store_products (
  product_id text primary key
             check (product_id ~ '^[a-z0-9][a-z0-9._]{0,148}$'),
  offer_id   text not null unique
             check (offer_id ~ '^[a-z0-9_]{1,64}$'),
  kind       text not null check (kind in ('consumable', 'non_consumable')),
  active     boolean not null default false,
  updated_at timestamptz not null default now()
);

alter table public.store_products enable row level security;
revoke all on public.store_products from anon, authenticated;

insert into public.store_products (product_id, offer_id, kind, active) values
  ('placeholder.emberfall.store.starter_pact',        'starter_pact',        'non_consumable', false),
  ('placeholder.emberfall.store.growth_fund',         'growth_fund',         'non_consumable', false),
  ('placeholder.emberfall.store.weekly_pack',         'weekly_pack',         'consumable',     false),
  ('placeholder.emberfall.store.monthly_pack',        'monthly_pack',        'consumable',     false),
  ('placeholder.emberfall.store.ember_ledger',        'ember_ledger',        'consumable',     false),
  ('placeholder.emberfall.store.pass_premium',        'pass_premium',        'consumable',     false),
  ('placeholder.emberfall.store.event_track_weekly',  'event_track_weekly',  'consumable',     false),
  ('placeholder.emberfall.store.event_track_monthly', 'event_track_monthly', 'consumable',     false),
  ('placeholder.emberfall.store.event_pack_weekly',   'event_pack_weekly',   'consumable',     false),
  ('placeholder.emberfall.store.event_pack_monthly',  'event_pack_monthly',  'consumable',     false),
  ('placeholder.emberfall.store.hero_contract',       'hero_contract',       'consumable',     false),
  ('placeholder.emberfall.store.relic_dust_satchel',  'relic_dust_satchel',  'consumable',     false),
  ('placeholder.emberfall.store.relic_dust_chest',    'relic_dust_chest',    'consumable',     false),
  ('placeholder.emberfall.store.relic_of_choice',     'relic_of_choice',     'consumable',     false)
on conflict do nothing;

-- ---------------------------------------------------------------- the receipts

create table if not exists public.purchase_receipts (
  id                 bigint generated always as identity primary key,
  user_id            uuid references auth.users (id) on delete set null,
  platform           text not null default 'google_play' check (platform in ('google_play')),
  product_id         text not null,
  offer_id           text not null,
  order_id           text check (order_id is null or char_length(order_id) between 1 and 128),
  purchase_token_sha256 text not null unique
                     check (purchase_token_sha256 ~ '^[0-9a-f]{64}$'),
  quantity           integer not null default 1 check (quantity >= 1),
  test_purchase      boolean not null default false,
  region_code        text check (region_code is null or region_code ~ '^[A-Z]{2}$'),
  purchased_at       timestamptz,
  verified_at        timestamptz not null default now(),
  acknowledged       boolean not null default false,
  revoked_at         timestamptz,
  revoke_reason      text check (revoke_reason is null or char_length(revoke_reason) <= 64),
  google_response    jsonb not null default '{}'::jsonb
                     check (jsonb_typeof(google_response) = 'object'
                            and octet_length(google_response::text) <= 8192)
);

create unique index if not exists purchase_receipts_order_unique
  on public.purchase_receipts (order_id) where order_id is not null;
create index if not exists purchase_receipts_user_idx
  on public.purchase_receipts (user_id, verified_at desc);

alter table public.purchase_receipts enable row level security;

revoke all on public.purchase_receipts from anon, authenticated;
-- A player may read their OWN receipts, and only the columns a restore or a "your purchases" page
-- needs. The token hash, the raw Google answer and the region are not readable by any client.
grant select (id, product_id, offer_id, order_id, quantity, test_purchase, purchased_at,
              verified_at, revoked_at) on public.purchase_receipts to authenticated;

drop policy if exists purchase_receipts_read_own on public.purchase_receipts;
create policy purchase_receipts_read_own
  on public.purchase_receipts for select
  to authenticated
  using (user_id = auth.uid());

-- ---------------------------------------------------------------- the two writers (service_role)
--
-- Recording. Answers {"outcome": ..., "receipt_id": ..., "offer_id": ...} where outcome is
--   recorded          a new row, the client may grant
--   already_recorded  this player already holds this receipt, the client must NOT grant again
--   owned_by_other    another account holds this token, refuse
--   unknown_product   not in store_products, or not active
--
-- The Edge Function has already checked the purchase with Google before it calls this; this
-- function only decides whether the ledger already knows it. Serialised on the token hash by the
-- unique index: two verifications of one token in the same instant cannot both record.
create or replace function public.purchase_receipt_record(
  p_user_id        uuid,
  p_product_id     text,
  p_order_id       text,
  p_token_sha256   text,
  p_quantity       integer,
  p_test_purchase  boolean,
  p_region_code    text,
  p_purchased_at   timestamptz,
  p_acknowledged   boolean,
  p_google_response jsonb
)
returns jsonb
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
  product  public.store_products;
  existing public.purchase_receipts;
  new_id   bigint;
begin
  if p_user_id is null or p_token_sha256 is null then
    raise exception 'purchase_receipt_record needs a user and a token hash' using errcode = 'PT400';
  end if;

  select * into product from public.store_products where product_id = p_product_id and active;
  if not found then
    return jsonb_build_object('outcome', 'unknown_product');
  end if;

  select * into existing from public.purchase_receipts where purchase_token_sha256 = p_token_sha256;
  if found then
    if existing.user_id is distinct from p_user_id then
      return jsonb_build_object('outcome', 'owned_by_other');
    end if;
    return jsonb_build_object('outcome', 'already_recorded', 'receipt_id', existing.id,
                              'offer_id', existing.offer_id, 'revoked', existing.revoked_at is not null);
  end if;

  begin
    insert into public.purchase_receipts
      (user_id, product_id, offer_id, order_id, purchase_token_sha256, quantity, test_purchase,
       region_code, purchased_at, acknowledged, google_response)
    values
      (p_user_id, product.product_id, product.offer_id, nullif(p_order_id, ''), p_token_sha256,
       greatest(coalesce(p_quantity, 1), 1), coalesce(p_test_purchase, false),
       nullif(p_region_code, ''), p_purchased_at, coalesce(p_acknowledged, false),
       coalesce(p_google_response, '{}'::jsonb))
    returning id into new_id;
  exception when unique_violation then
    -- Lost a race with a verification of the same token, or the order id is already recorded under
    -- another token. Read what won and answer as above.
    select * into existing from public.purchase_receipts
     where purchase_token_sha256 = p_token_sha256
        or (p_order_id is not null and p_order_id <> '' and order_id = p_order_id)
     limit 1;
    if existing.user_id is distinct from p_user_id then
      return jsonb_build_object('outcome', 'owned_by_other');
    end if;
    return jsonb_build_object('outcome', 'already_recorded', 'receipt_id', existing.id,
                              'offer_id', existing.offer_id, 'revoked', existing.revoked_at is not null);
  end;

  return jsonb_build_object('outcome', 'recorded', 'receipt_id', new_id, 'offer_id', product.offer_id);
end;
$$;

-- Marking a receipt acknowledged after the Edge Function acknowledged it with Google.
create or replace function public.purchase_receipt_mark_acknowledged(p_token_sha256 text)
returns boolean
language plpgsql
security definer
set search_path = public, pg_temp
as $$
begin
  update public.purchase_receipts set acknowledged = true
   where purchase_token_sha256 = p_token_sha256;
  return found;
end;
$$;

-- Revoking. Called by the refund notification handler (a voided purchase or a cancelled one). Finds
-- the row by token hash, or by order id when only that is known. Answers true when a row moved.
-- Revoking twice keeps the first time and reason.
create or replace function public.purchase_receipt_revoke(p_token_sha256 text, p_order_id text, p_reason text)
returns boolean
language plpgsql
security definer
set search_path = public, pg_temp
as $$
begin
  update public.purchase_receipts
     set revoked_at = now(),
         revoke_reason = left(coalesce(p_reason, 'revoked'), 64)
   where revoked_at is null
     and ((p_token_sha256 is not null and purchase_token_sha256 = p_token_sha256)
          or (p_order_id is not null and p_order_id <> '' and order_id = p_order_id));
  return found;
end;
$$;

revoke all on function public.purchase_receipt_record(uuid, text, text, text, integer, boolean, text, timestamptz, boolean, jsonb)
  from public, anon, authenticated;
revoke all on function public.purchase_receipt_mark_acknowledged(text) from public, anon, authenticated;
revoke all on function public.purchase_receipt_revoke(text, text, text) from public, anon, authenticated;
grant execute on function public.purchase_receipt_record(uuid, text, text, text, integer, boolean, text, timestamptz, boolean, jsonb)
  to service_role;
grant execute on function public.purchase_receipt_mark_acknowledged(text) to service_role;
grant execute on function public.purchase_receipt_revoke(text, text, text) to service_role;
-- The Edge Function reads the product list with the service role.
grant select on public.store_products to service_role;

-- ---------------------------------------------------------------- what was applied, said once

do $$
declare
  who  text;
  verb text;
begin
  foreach who in array array['anon', 'authenticated'] loop
    foreach verb in array array['insert', 'update', 'delete'] loop
      if has_table_privilege(who, 'public.purchase_receipts', verb) then
        raise exception '0019: % can % purchase_receipts', who, verb;
      end if;
      if has_table_privilege(who, 'public.store_products', verb) then
        raise exception '0019: % can % store_products', who, verb;
      end if;
    end loop;
    if has_any_column_privilege(who, 'public.purchase_receipts', 'insert')
       or has_any_column_privilege(who, 'public.purchase_receipts', 'update') then
      raise exception '0019: % can write a column of purchase_receipts', who;
    end if;
    if has_column_privilege(who, 'public.purchase_receipts', 'purchase_token_sha256', 'select')
       or has_column_privilege(who, 'public.purchase_receipts', 'google_response', 'select') then
      raise exception '0019: % can read the token hash or the raw Google answer', who;
    end if;
    if has_function_privilege(who, 'public.purchase_receipt_record(uuid, text, text, text, integer, boolean, text, timestamptz, boolean, jsonb)', 'execute')
       or has_function_privilege(who, 'public.purchase_receipt_revoke(text, text, text)', 'execute')
       or has_function_privilege(who, 'public.purchase_receipt_mark_acknowledged(text)', 'execute') then
      raise exception '0019: % can call a receipt writer', who;
    end if;
  end loop;
  if has_any_column_privilege('anon', 'public.purchase_receipts', 'select') then
    raise exception '0019: anon can read purchase_receipts';
  end if;
end;
$$;
