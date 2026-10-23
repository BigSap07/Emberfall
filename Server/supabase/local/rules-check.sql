-- rules-check.sql - the rules of migrations 0016 to 0024 exercised on a LOCAL PostgreSQL, as a player
-- (authenticated), as nobody (anon) and as the Edge Functions (service_role). LOCAL ONLY: it creates
-- users, saves and receipts freely. Tools/supabase-local-check.sh applies the stand-in and every
-- migration first, then runs this file, then counts the FAIL lines.
--
-- Every check prints PASS or FAIL. A check that expects a refusal catches the error itself, so one
-- failure never hides the checks after it.

create schema if not exists t;
grant usage on schema t to anon, authenticated, service_role;

create or replace function t.check(ok boolean, what text) returns void language plpgsql as $$
begin
  if coalesce(ok, false) then raise notice 'PASS: %', what; else raise notice 'FAIL: %', what; end if;
end; $$;

create or replace function t.as_user(u uuid) returns void language plpgsql as $$
begin
  perform set_config('role', 'authenticated', true);
  perform set_config('request.jwt.claim.sub', u::text, true);
end; $$;

create or replace function t.as_role(r text) returns void language plpgsql as $$
begin
  perform set_config('role', r, true);
  perform set_config('request.jwt.claim.sub', '', true);
end; $$;

create or replace function t.new_user() returns uuid language plpgsql as $$
declare u uuid;
begin
  insert into auth.users default values returning id into u;
  return u;
end; $$;

grant execute on all functions in schema t to anon, authenticated, service_role;

-- The server takes the build's save version (0013 shipped 19; Docs/DESIGN-LEADERBOARDS.md owner step 2).
update public.save_limits set max_save_version = 26, updated_at = now();
update public.store_products set active = true;

-- ================================================================ 0016 account deletion

do $$
declare a uuid := t.new_user(); b uuid := t.new_user(); res jsonb; ok boolean;
begin
  perform t.as_user(a);
  perform public.push_save(0, 26, 'dev', '{"version": 26, "gold": 1}'::jsonb);
  insert into public.friendships (requester, addressee) values (a, b);
  perform t.as_role('service_role');
  perform public.purchase_receipt_record(a, 'placeholder.emberfall.store.weekly_pack', 'GPA.DEL-1',
    encode(sha256('del-token'::bytea), 'hex'), 1, false, 'BE', now(), false, '{}'::jsonb);

  perform t.as_user(a);
  begin
    perform public.delete_my_account('delete');
    ok := false;
  exception when others then ok := true;
  end;
  perform t.check(ok, '0016: delete_my_account refuses a confirmation that is not exactly DELETE');

  res := public.delete_my_account('DELETE');
  perform t.as_role('postgres');
  perform t.check(not exists (select 1 from auth.users where id = a), '0016: the auth user is gone');
  perform t.check(not exists (select 1 from public.saves where user_id = a), '0016: the save is gone');
  perform t.check(not exists (select 1 from public.friendships where a in (requester, addressee)), '0016: the friendships are gone');
  perform t.check(exists (select 1 from public.purchase_receipts where order_id = 'GPA.DEL-1' and user_id is null),
                  '0016 and 0019: the receipt outlives the account with no user attached');
end; $$;

do $$
declare ok boolean;
begin
  perform t.as_role('anon');
  begin perform public.delete_my_account('DELETE'); ok := false; exception when others then ok := true; end;
  perform t.check(ok, '0016: anon cannot call delete_my_account');
  perform t.as_role('authenticated');
  begin perform public.admin_delete_account(gen_random_uuid()); ok := false; exception when others then ok := true; end;
  perform t.check(ok, '0016: a player cannot call admin_delete_account');
end; $$;

-- ================================================================ 0017 chat safety, 0021 blocks, the owner's block decision

do $$
declare a uuid := t.new_user(); b uuid := t.new_user(); g public.guilds; m public.guild_messages; n integer; ok boolean; res jsonb;
begin
  perform t.as_user(a);
  g := public.guild_create('Block Test ' || left(a::text, 6), '');
  perform t.as_role('postgres');
  insert into public.guild_members (guild_id, user_id, role) values (g.id, b, 'member');

  perform t.as_user(b);
  m := public.guild_post_message('hello from b');
  perform t.as_user(a);
  select count(*) into n from public.guild_messages where id = m.id;
  perform t.check(n = 1, '0017: a guildmate reads the message before any block');

  res := public.guild_report_message(m.id, 'spam', 'local check');
  perform t.check(res is not null, '0017: a member can report a message');
  begin perform count(*) from public.chat_reports; ok := false; exception when others then ok := true; end;
  perform t.check(ok, '0017: no player can read chat_reports');

  perform public.guild_mute_member(b, 60);
  perform t.as_user(b);
  begin perform public.guild_post_message('muted?'); ok := false; exception when others then ok := true; end;
  perform t.check(ok, '0017: a muted member cannot post');

  perform t.as_user(a);
  perform public.user_block(b);
  select count(*) into n from public.guild_messages where id = m.id;
  perform t.check(n = 0, '0017: after a block the blocker no longer reads the blocked player''s lines');
  perform t.as_user(b);
  select count(*) into n from public.guild_messages where id = m.id;
  perform t.check(n = 1, '0017: the blocked player still reads their own line (and is not told)');
end; $$;

do $$
declare a uuid := t.new_user(); b uuid := t.new_user(); c uuid := t.new_user(); n integer; ok boolean;
begin
  -- an ACCEPTED friendship between a and b, and a pending request from c to a
  perform t.as_user(a);
  insert into public.friendships (requester, addressee) values (a, b);
  perform t.as_user(b);
  update public.friendships set status = 'accepted' where requester = a and addressee = b;
  perform t.as_user(c);
  insert into public.friendships (requester, addressee) values (c, a);
  perform t.as_role('postgres');
  select count(*) into n from public.friendships where status = 'accepted' and requester = a and addressee = b;
  perform t.check(n = 1, '0003: a and b are accepted friends before the block');

  perform t.as_user(a);
  perform public.user_block(b);
  perform public.user_block(c);
  perform t.as_role('postgres');
  perform t.check(not exists (select 1 from public.friendships where (requester = a and addressee = b) or (requester = b and addressee = a)),
                  '0021 (owner decision): a block ENDS an accepted friendship');
  perform t.check(not exists (select 1 from public.friendships where requester = c and addressee = a),
                  '0021: a block removes the blocked player''s pending request');

  perform t.as_user(b);
  begin insert into public.friendships (requester, addressee) values (b, a); ok := false; exception when others then ok := true; end;
  perform t.check(ok, '0021: the blocked player cannot send the blocker a new request');

  perform t.as_user(a);
  perform public.user_unblock(b);
  perform t.as_role('postgres');
  perform t.check(not exists (select 1 from public.friendships where a in (requester, addressee) and b in (requester, addressee)),
                  '0021: unblocking restores nothing; a new friendship starts with a new request');
end; $$;

-- ================================================================ 0018 server time

do $$
declare a uuid := t.new_user(); res jsonb; ok boolean;
begin
  perform t.as_user(a);
  res := public.clock_check(extract(epoch from now())::bigint);
  perform t.check((res ->> 'trusted')::boolean, '0018: an honest device clock is trusted');
  res := public.clock_check(extract(epoch from now())::bigint + 86400);
  perform t.check(not (res ->> 'trusted')::boolean, '0018: a clock a day ahead is not trusted');
  perform t.as_role('anon');
  begin perform public.server_time(); ok := false; exception when others then ok := true; end;
  perform t.check(ok, '0018: anon cannot call server_time');
end; $$;

-- ================================================================ 0019 receipts, 0021 verification throttle

do $$
declare a uuid := t.new_user(); b uuid := t.new_user(); res jsonb; ok boolean; i integer; allowed integer := 0;
  tok text := encode(sha256('receipt-token-1'::bytea), 'hex');
begin
  perform t.as_user(a);
  begin
    perform public.purchase_receipt_record(a, 'placeholder.emberfall.store.weekly_pack', 'GPA.R-1', tok, 1, false, 'BE', now(), false, '{}'::jsonb);
    ok := false;
  exception when others then ok := true; end;
  perform t.check(ok, '0019: a player cannot call the receipt writer');

  perform t.as_role('service_role');
  res := public.purchase_receipt_record(a, 'placeholder.emberfall.store.weekly_pack', 'GPA.R-1', tok, 1, false, 'BE', now(), false, '{}'::jsonb);
  perform t.check(res ->> 'outcome' = 'recorded', '0019: the service role records a new receipt');
  res := public.purchase_receipt_record(a, 'placeholder.emberfall.store.weekly_pack', 'GPA.R-1', tok, 1, false, 'BE', now(), false, '{}'::jsonb);
  perform t.check(res ->> 'outcome' = 'already_recorded', '0019: the same token again is already_recorded (no second grant)');
  res := public.purchase_receipt_record(b, 'placeholder.emberfall.store.weekly_pack', 'GPA.R-1', tok, 1, false, 'BE', now(), false, '{}'::jsonb);
  perform t.check(res ->> 'outcome' = 'owned_by_other', '0019: the same token from another player is owned_by_other');

  perform t.as_user(a);
  begin perform purchase_token_sha256 from public.purchase_receipts; ok := false; exception when others then ok := true; end;
  perform t.check(ok, '0019: a player cannot read the token hash');
  perform t.check((select count(*) from public.purchase_receipts where order_id = 'GPA.R-1') = 1, '0019: a player reads their own receipt');
  perform t.as_user(b);
  perform t.check((select count(*) from public.purchase_receipts where order_id = 'GPA.R-1') = 0, '0019: another player does not see it');

  perform t.as_role('service_role');
  for i in 1 .. 21 loop
    if (public.purchase_verify_allow(a) ->> 'allowed')::boolean then allowed := allowed + 1; end if;
  end loop;
  perform t.check(allowed = 20, '0021: purchase_verify_allow lets 20 verifications through in 10 minutes and refuses the 21st (' || allowed || ')');
  perform t.as_user(a);
  begin perform public.purchase_verify_allow(a); ok := false; exception when others then ok := true; end;
  perform t.check(ok, '0021: a player cannot call purchase_verify_allow');
end; $$;

-- ================================================================ 0020 bounds and the friend request throttle

do $$
declare a uuid := t.new_user(); ok boolean; i integer; sent integer := 0; other uuid;
begin
  perform t.as_user(a);
  begin
    insert into public.team_snapshots (user_id, label, power_score, units) values (a, repeat('x', 65), 1, '[]'::jsonb);
    ok := false;
  exception when others then ok := true; end;
  perform t.check(ok, '0020: a team snapshot label over 64 characters is refused');
  begin
    insert into public.team_snapshots (user_id, label, power_score, units) values (a, 'ok', -1, '[]'::jsonb);
    ok := false;
  exception when others then ok := true; end;
  perform t.check(ok, '0020: a negative power score is refused');
  insert into public.team_snapshots (user_id, label, power_score, units) values (a, 'Wayfarer', 100, '[{},{}]'::jsonb);
  perform t.check(true, '0020: a well-formed snapshot is taken');

  for i in 1 .. 21 loop
    perform t.as_role('postgres');
    other := t.new_user();
    perform t.as_user(a);
    begin
      insert into public.friendships (requester, addressee) values (a, other);
      sent := sent + 1;
    exception when others then null; end;
  end loop;
  perform t.check(sent = 20, '0020: 20 friend requests an hour go through and the 21st is refused (' || sent || ')');
end; $$;

-- ================================================================ 0022 guild dungeon size

do $$
begin
  perform t.check(public.guild_dungeon_floors_for(1) = 5 and public.guild_dungeon_floors_for(9) = 5
              and public.guild_dungeon_floors_for(10) = 6 and public.guild_dungeon_floors_for(15) = 7
              and public.guild_dungeon_floors_for(20) = 8 and public.guild_dungeon_floors_for(25) = 9
              and public.guild_dungeon_floors_for(30) = 10,
                  '0022: the ladder by guild size is guild-dungeon-scale.json''s (5, 6 at 10, 7 at 15, 8 at 20, 9 at 25, 10 at 30)');
  perform t.check(public.guild_dungeon_cycle_attempt_cap() = 15, '0022: an account has 15 attempts a cycle across every guild');
end; $$;

-- ================================================================ 0023 leaderboards

do $$
declare a uuid := t.new_user(); res jsonb; ok boolean; n integer; v bigint; cleared jsonb;
begin
  select jsonb_agg('ch' || c || '_' || s) into cleared
    from generate_series(1, 2) c, generate_series(1, 12) s where (c - 1) * 12 + s <= 20;
  perform t.as_role('postgres');
  update auth.users set created_at = now() - interval '30 days' where id = a;
  update public.profiles set created_at = now() - interval '30 days' where user_id = a;
  perform t.as_user(a);
  perform public.push_save(0, 26, 'dev', jsonb_build_object('version', 26, 'clearedStages', cleared,
    'recitalHighestFloor', 8, 'recitalCurrentFloor', 9, 'provingHighestStanding', 12,
    'provingStanding', 12, 'provingAttempts', 4));
  res := public.leaderboard_submit();
  perform t.check(res -> 'recital' ->> 'status' = 'raised', '0023: a consistent save raises the Recital board (' || coalesce(res ->> 'recital', 'null') || ')');
  perform t.check(res -> 'proving' ->> 'status' = 'raised', '0023: and the Proving board, on the highest standing');
  perform t.check(res -> 'campaign' ->> 'status' = 'raised', '0023: and the campaign board');

  select value into v from public.leaderboard_top('recital_week', 100) where is_me;
  perform t.check(v = 8, '0023 (owner decision): the weekly Recital board ranks the highest floor this week (8)');
  select value into v from public.leaderboard_top('proving', 100) where is_me;
  perform t.check(v = 12, '0023 (owner decision): the Proving board ranks the highest standing (12)');
  begin perform * from public.leaderboard_top('recital_week_climb', 10); ok := false; exception when others then ok := true; end;
  perform t.check(ok, '0023: there is no floors-climbed board');

  begin perform public.leaderboard_submit(); ok := false; exception when others then ok := true; end;
  perform t.check(ok, '0023: a second submission inside a minute is refused');
  begin perform count(*) from public.leaderboard_entries; ok := false; exception when others then ok := true; end;
  perform t.check(ok, '0023: no player can read a leaderboard table directly');
  perform t.as_role('anon');
  begin perform * from public.leaderboard_top('recital', 10); ok := false; exception when others then ok := true; end;
  perform t.check(ok, '0023: anon cannot read a board');
end; $$;

do $$
declare a uuid := t.new_user(); res jsonb;
begin
  perform t.as_user(a);
  perform public.push_save(0, 26, 'dev', jsonb_build_object('version', 26, 'clearedStages', '[]'::jsonb,
    'recitalHighestFloor', 4000, 'recitalCurrentFloor', 4001, 'provingHighestStanding', 0,
    'provingStanding', 0, 'provingAttempts', 0));
  res := public.leaderboard_submit();
  perform t.check(res -> 'recital' ->> 'status' = 'refused', '0023: a floor without the Recital unlocked is refused');
  perform t.check(res -> 'campaign' ->> 'status' <> 'refused', '0023: a refused board does not block another in the same call');
end; $$;

-- ================================================================ 0024 the clawback (owner decision)

do $$
declare a uuid := t.new_user(); b uuid := t.new_user(); s public.saves; doc jsonb; old_doc jsonb; ok boolean; n integer;
  tok text := encode(sha256('claw-token-1'::bytea), 'hex');
begin
  old_doc := jsonb_build_object(
    'version', 26, 'gold', 10000, 'slag', 50, 'relicDust', 3000,
    'sealed', jsonb_build_object('gold', 6000, 'shards', 0, 'slag', 0),
    'heroes', jsonb_build_array(
       jsonb_build_object('defId', 'brenna_the_remaining_wall', 'copies', 2, 'sealedCopies', 1),
       jsonb_build_object('defId', 'vaelis_the_unfixed_blade', 'copies', 1, 'sealedCopies', 1)),
    'store', jsonb_build_object('receipts', jsonb_build_array(
       jsonb_build_object('id', 'GPA.CLAW-1', 'product', 'p', 'offer', 'starter_pact', 'at', 1,
                          'gold', 5000, 'dust', 2000, 'hero', 'brenna_the_remaining_wall'),
       jsonb_build_object('id', 'GPA.KEEP-1', 'product', 'p', 'offer', 'weekly_pack', 'at', 2, 'gold', 4000))));
  perform t.as_user(a);
  perform public.push_save(0, 26, 'dev', old_doc);

  perform t.as_role('service_role');
  perform public.purchase_receipt_record(a, 'placeholder.emberfall.store.starter_pact', 'GPA.CLAW-1', tok, 1, false, 'BE', now(), false, '{}'::jsonb);
  perform t.check(public.purchase_receipt_revoke(tok, null, 'voided'), '0024: the refund notification revokes the receipt');

  perform t.as_role('postgres');
  select * into s from public.saves where user_id = a;
  doc := s.data;
  perform t.check(s.revision = 2, '0024: the clawback moves the cloud save''s revision on, so the buying device sees a newer save');
  perform t.check((doc ->> 'gold')::bigint = 5000 and (doc #>> '{sealed,gold}')::bigint = 1000,
                  '0024: unspent Sealed Gold is taken back, never more than the purchase gave (10000 to 5000, Sealed 6000 to 1000)');
  perform t.check((doc ->> 'relicDust')::bigint = 1000, '0024: Dust is taken back up to what the purchase gave (3000 to 1000)');
  perform t.check((doc #>> '{heroes,0,copies}')::int = 1 and not (doc -> 'heroes' -> 0 ? 'sealedCopies'),
                  '0024: one Sealed copy of the bought hero is taken back (2 copies to 1)');
  perform t.check((doc #>> '{store,receipts,0,clawedBack}')::boolean, '0024: the receipt carries the clawedBack mark');
  perform t.check(not (doc -> 'store' -> 'receipts' -> 1 ? 'clawedBack'), '0024: a receipt that was not refunded is untouched');
  perform t.check((doc ->> 'slag')::bigint = 50, '0024: a good the purchase did not give is untouched');

  -- The buying device keeps its old copy and pushes it again: the cloud still stores it clawed back.
  perform t.as_user(a);
  s := public.push_save(2, 26, 'dev', old_doc);
  perform t.check((s.data ->> 'gold')::bigint = 5000 and (s.data #>> '{store,receipts,0,clawedBack}')::boolean,
                  '0024: a later push of the un-clawed copy is clawed back on its way in');
  -- A push of the clawed copy (the mark kept, as StoreState keeps it) is never clawed twice.
  s := public.push_save(3, 26, 'dev', s.data);
  perform t.check((s.data ->> 'gold')::bigint = 5000 and (s.data ->> 'relicDust')::bigint = 1000,
                  '0024: a save that carries the mark is not clawed back a second time');

  perform t.as_role('postgres');
  select count(*) into n from public.purchase_clawbacks where receipt_id = (select id from public.purchase_receipts where order_id = 'GPA.CLAW-1');
  perform t.check(n = 1, '0024: one clawback row per refunded receipt');
  perform t.check((select last_result from public.purchase_clawbacks where user_id = a) = 'taken', '0024: its last result is taken');
  perform t.as_user(a);
  begin perform token_sha256 from public.purchase_clawbacks; ok := false; exception when others then ok := true; end;
  perform t.check(ok, '0024: a player cannot read the clawback token hash');
  perform t.check((select count(*) from public.purchase_clawbacks) = 1, '0024: the player sees their own clawback row');
  begin delete from public.purchase_clawbacks; ok := false; exception when others then ok := true; end;
  perform t.check(ok, '0024: a player cannot delete a clawback row');
  begin perform public.purchase_clawback_take('{}'::jsonb, 'x', tok); ok := false; exception when others then ok := true; end;
  perform t.check(ok, '0024: a player cannot call the clawback functions');
  perform t.as_user(b);
  perform t.check((select count(*) from public.purchase_clawbacks) = 0, '0024: another player sees none of it');
end; $$;

do $$
declare a uuid := t.new_user(); s public.saves; tok text := 'second-claw-token';
begin
  -- The receipt id is the purchase token, the only copy of the bought hero, all its Gold already spent.
  perform t.as_user(a);
  perform public.push_save(0, 26, 'dev', jsonb_build_object(
    'version', 26, 'gold', 100, 'relicDust', 0, 'sealed', jsonb_build_object('gold', 0),
    'heroes', jsonb_build_array(jsonb_build_object('defId', 'nereia_the_field_medic', 'copies', 1, 'sealedCopies', 1)),
    'store', jsonb_build_object('receipts', jsonb_build_array(
       jsonb_build_object('id', tok, 'offer', 'starter_pact', 'gold', 5000, 'hero', 'nereia_the_field_medic')))));
  perform t.as_role('service_role');
  perform public.purchase_receipt_record(a, 'placeholder.emberfall.store.starter_pact', 'GPA.CLAW-2',
    encode(sha256(convert_to(tok, 'UTF8')), 'hex'), 1, false, 'BE', now(), false, '{}'::jsonb);
  perform public.purchase_receipt_revoke(null, 'GPA.CLAW-2', 'voided');
  perform t.as_role('postgres');
  select * into s from public.saves where user_id = a;
  perform t.check((s.data #>> '{store,receipts,0,clawedBack}')::boolean,
                  '0024: a save receipt keyed by the purchase token matches by its SHA-256');
  perform t.check((s.data ->> 'gold')::bigint = 100, '0024: spent Gold is not taken (nothing Sealed was left)');
  perform t.check((s.data #>> '{heroes,0,copies}')::int = 1 and (s.data #>> '{store,receipts,0,taken,heroKept}')::boolean,
                  '0024: the only copy of a hero is left in the roster and the clawback says heroKept');
end; $$;

do $$
declare a uuid := t.new_user(); tok text := encode(sha256('no-save-token'::bytea), 'hex');
begin
  perform t.as_role('service_role');
  perform public.purchase_receipt_record(a, 'placeholder.emberfall.store.weekly_pack', 'GPA.CLAW-3', tok, 1, false, 'BE', now(), false, '{}'::jsonb);
  perform public.purchase_receipt_revoke(tok, null, 'voided');
  perform t.as_role('postgres');
  perform t.check((select last_result from public.purchase_clawbacks where user_id = a) = 'no_save',
                  '0024: a refund for an account with no cloud save is filed as no_save');
  perform t.as_user(a);
  perform public.push_save(0, 26, 'dev', '{"version": 26, "gold": 7, "store": {"receipts": [{"id": "GPA.CLAW-3", "gold": 7}]}, "sealed": {"gold": 7}}'::jsonb);
  perform t.as_role('postgres');
  perform t.check((select (data ->> 'gold')::bigint from public.saves where user_id = a) = 0,
                  '0024: the first save that account writes afterwards is clawed back on its way in');
end; $$;

-- ================================================================ every client door is closed

do $$
declare f record; open_fns text := '';
begin
  for f in
    select p.oid::regprocedure::text as sig
      from pg_proc p join pg_namespace n on n.oid = p.pronamespace
     where n.nspname = 'public' and p.prorettype <> 'trigger'::regtype
  loop
    if has_function_privilege('anon', f.sig, 'execute') then open_fns := open_fns || f.sig || ' '; end if;
  end loop;
  perform t.check(open_fns = '', 'every public function is closed to anon' || case when open_fns <> '' then ': ' || open_fns else '' end);
end; $$;

do $$
declare r record; open_tables text := '';
begin
  for r in select c.relname from pg_class c join pg_namespace n on n.oid = c.relnamespace
            where n.nspname = 'public' and c.relkind = 'r' loop
    if not (select relrowsecurity from pg_class where oid = ('public.' || r.relname)::regclass) then
      open_tables := open_tables || r.relname || '(no RLS) ';
    end if;
    if r.relname <> 'play_events' and (has_table_privilege('anon', 'public.' || r.relname, 'select')
       or has_table_privilege('anon', 'public.' || r.relname, 'insert')
       or has_any_column_privilege('anon', 'public.' || r.relname, 'select')) then
      open_tables := open_tables || r.relname || '(anon) ';
    end if;
  end loop;
  perform t.check(open_tables = '', 'every table has RLS on and none but play_events is open to anon' || case when open_tables <> '' then ': ' || open_tables else '' end);
end; $$;
