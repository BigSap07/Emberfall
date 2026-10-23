# Security review: the Supabase migrations and the receipt functions

Decision record, dated 2026-10-22. Scope: every migration in `Server/supabase/migrations` (0001 to 0015 on main), the four launch migrations 0016 to 0019 (account deletion, chat safety, server time, purchase receipts) and the two receipt Edge Functions with their shared code (`verify-google-purchase`, `google-play-rtdn`, `_shared/purchase_logic.ts`, `_shared/google_play.ts`), which are written but not yet on main. Nothing was applied to any project: the review read the files, and the fixes are two new migration files, `0020_security_review.sql` and `0021_security_review_launch.sql`, also not applied. `supabase.json` and every secret were left unread.

The SQL was checked by parsing only: every migration, 0001 to 0021, goes through the PostgreSQL parser (libpg_query, through `pglast`), and the PL/pgSQL bodies of 0014, 0020 and 0021 through the PL/pgSQL parser. That catches syntax, not behaviour. The behaviour is for `Tools/supabase-rules-test.py`, `supabase-guild-rules-test.py` and `supabase-launch-rules-test.py` on a test project once the owner applies the files.

## The model the review assumed

- **The anon key is public.** It ships in every build. Anything the `anon` role can do, anybody can do.
- **`authenticated` is almost as public.** Anonymous sign-in is on (the game signs in anonymously before any cloud call), and an anonymous user holds the `authenticated` role. So "signed in" means "made one HTTP call". Every per-account limit in these files is a limit per throwaway account unless Supabase Auth's own sign-up limits hold (finding A1).
- **The save is client-authoritative and stays so.** `push_save` stores whatever document a client sends inside the size and version rules. A modified client can give itself anything, including goods that look bought. That is accepted in `DESIGN-RECEIPT-VALIDATION.md` section 2, and it is not re-litigated here. It matters only where another player, the server's ledgers or real money are affected, and those are the findings below.
- **The Belgian rule is not a server rule.** Sealed goods are enforced in the client (`SealedGrants`, `PurchaseGrants.Apply`). Nothing on the server grants a random reward, and nothing in these findings or fixes adds one.

## What holds, checked and confirmed

These were the questions the review asked first, and every one of them came back clean:

- **No table is readable by `anon`.** Every table revokes `anon` and `authenticated` before it grants anything (`CloudChecks.CheckMigrationGrants` asserts it from the text), and the one `anon` grant is the four-column insert on `play_events` (0015). RLS is enabled on every table.
- **No function a client can call is left open to `anon`.** Every non-trigger function is revoked from `public, anon`. The service-role functions (the receipt writers in 0019, `account_delete_internal` and `admin_delete_account` in 0016, `guild_tech_dev_set` in 0012, the settle and balance helpers in 0011 and 0012) are revoked from `authenticated` as well, and 0016 and 0019 assert it in their closing DO blocks.
- **No SECURITY DEFINER function takes a user id from the caller and acts on it.** Every one reads `auth.uid()` itself. The two that take a user id (`guild_path_balance`, `guild_tech_xp`) are closed to clients. Every SECURITY DEFINER function pins `search_path`.
- **No client write reaches a ledger.** Saves, chat, dungeon attempts, Waymarks, rungs, receipts, reports and mutes have no client INSERT or UPDATE grant. Their RPCs are the only way in, and each locks the caller's own row before it counts.
- **The receipt functions are sound in their core.** The token is stored as a hash, and one token belongs to one account. Account binding uses the obfuscated account id. The comparison is constant-time. The RTDN function verifies Google's OIDC token (issuer, audience, the push account's email) before it reads anything. Test purchases are refused unless allowed.

## Findings

Severity is for this game at launch: High is real money or another player's data, Medium is an abuse path that costs the project or other players something, Low is hardening.

| # | Severity | Where | Finding | Fix |
|---|---|---|---|---|
| L1 | High | `verify-google-purchase`, `DESIGN-RECEIPT-VALIDATION.md` 3.3 | **Free goods through the automatic refund.** The server answers `grant: true` and acknowledges only non-consumables. A consumable is acknowledged only when the client calls `ConfirmPendingPurchase`. A modified client takes the grant and never confirms. Google refunds an unacknowledged purchase after three days, the RTDN marks the receipt revoked, and revocation takes nothing back. Every pack, pass and event offer is a consumable. | Edge Function change, below. |
| L2 | Medium | `verify-google-purchase` | **The Google API quota can be drained.** There is no per-account limit. Each well-formed request costs one Play Developer API call, and accounts are free (A1). A script with made-up tokens can spend the quota until real purchases stop verifying. | 0021: `purchase_verify_allow`, service role only, plus three lines in the function. |
| R1 | Medium | 0012 `guild_tech_xp` | **Guild XP is counted once per guild an account fought for in a cycle.** The ledger pays an account once a cycle. The XP join credits that pay to every guild the account filed an attempt for that cycle, and changing guild gives fresh attempts. One account, or a few throwaway ones, can raise any number of guilds. The level is a high-water mark, so it never comes back down. Guild tech talents apply in campaign fights. | 0020: each (account, cycle) feeds only the guild of its first attempt. |
| R2 | Medium | 0003 `team_snapshots` | **No size or shape limit.** `units` can hold megabytes, which every friend and guildmate downloads when they open the row. `label` can be any length, with newlines. `power_score` can be negative. | 0020: `team_snapshots_bounded` (label 64 chars, one line; at most 5 units; 32 KiB; score not negative). |
| R4 | Medium | 0003 `friendships` | **Friend requests are unlimited.** Every profile is readable by every signed-in user (0001), so a script can request every player in the project, withdraw and request again. | 0020: a per-account tally counts sends (not rows), 20 an hour by default. |
| L5 | Medium | 0017 blocks | **A block covers chat only.** A blocked player can keep sending friend requests to the person who blocked them. | 0021: a request to someone who blocked you is refused (the error does not say why), and blocking removes the blocked player's pending request. |
| A1 | Medium | Supabase Auth settings (not in the repo) | **Every per-account limit is per throwaway account.** That covers guild code lookups (0014), chat cooldowns, reports, friend requests, verification and Waymarks. | Project settings: keep the anonymous sign-in rate limit low (Auth > Rate Limits) and turn on CAPTCHA (Turnstile or hCaptcha) for anonymous sign-ins before launch. No SQL can do this. |
| L3 | Medium (players' money) | 0016 and 0019, `store.json` `restorable` | **The two "restorable" offers cannot be restored by an anonymous player who reinstalls.** A reinstall makes a new anonymous user. The purchase is bound to the old user's hash (`account_mismatch`), and the receipt row belongs to the old user (`owned_by_other`). After an account deletion the receipt's `user_id` is NULL, so it is `owned_by_other` forever. The Starter Pact and the Growth Fund are paid for and lost. | Owner decision (below). |
| R3 | Low | 0002 and 0013 `saves.device_id` | Unbounded text outside 0013's 1 MiB cap. | 0020: `saves_device_id_bounded`, 128 chars. |
| R5, L6 | Low | 0001, 0003, 0004, 0017 trigger functions | Never revoked. Postgres refuses to run them outside a trigger, so nothing was reachable. | 0020 and 0021 revoke them. Revoking does not stop a trigger from firing. |
| L4 | Low | `google-play-rtdn` | A refund that arrives before the purchase was verified updates no row and is acknowledged. A later verification still refuses a fully refunded purchase (`refundableQuantity` 0), but the record is lost. | Poll the Voided Purchases API daily as a backstop (already listed as not built). |
| S1 | Low | 0008, 0011, 0014 `guild_dungeon_submit` | Still granted to `authenticated`, though no client calls it. Its rows carry no board bits and pay nothing, but they do open the guild's next floor. Before R1 they also attributed the caller's cycle to that guild. | Revoke it once `supabase-guild-rules-test.py` moves to `guild_dungeon_attempt` (the test still calls it, so it is not revoked here). |
| S2 | Low | 0011 boards | Turns, "one aspect" and "no healer" are the client's word, so a cheater takes first place on every board of their guild. The pay is bounded (at most 90 Waymarks a cycle), but it pushes honest members down. Already stated in 0011. | Re-run reported attempts with the engine (the row holds seed and team), or check turns against a replay before boards settle. |
| S3 | Low | 0001 `profiles_read_any_signed_in` | Any signed-in user can page the whole profile table (user id, display name, friend code). Accepted in 0001 for a two-player test. With anonymous sign-up it is a full player directory. | Before launch: replace with an exact-match `profile_by_friend_code` function (0014's `guild_by_code` shape) and a friends-only read policy. The client's friend lookup changes with it, so it is a code task, not this migration. |
| S4 | Low | 0002 and 0013 `push_save` | No minimum interval. Each push may be 1 MiB, so a script writes 1 MiB per request per throwaway account. | A `last pushed` floor of a few seconds in `push_save`. It needs the client's push cadence confirmed first, so it is listed, not applied. |
| S5 | Info | 0015 `play_events` | Open insert, documented as a known limit. | 0015's own plan (a per-install trigger and a global ceiling) if the table is ever abused. |
| S6 | Low | 0016 `delete_my_account` | The confirmation is the constant `DELETE`, so a stolen access token can end the account. | Acceptable for anonymous accounts. For linked accounts, require a fresh sign-in (Supabase reauthentication) before the call. Code task. |
| S7 | Low | 0019 `quantity` | The client grants once per receipt, whatever the quantity. A multi-quantity purchase would pay for N and grant 1. | Leave multi-quantity off for every product in the Play Console. |
| S8 | Info | 0006 and 0003 snapshots | A friend or guildmate fights whatever line the snapshot claims, so a forged snapshot is possible. No reward depends on a duel. | None while duels pay nothing. Revisit if they ever do. |

## L1 in detail: the Edge Function change

Google's own guidance: acknowledge or consume on the backend right after granting, because client-side processing leaves room for "poor network connectivity and malicious activity". An unacknowledged purchase is refunded after three days. The change, in `verify-google-purchase/index.ts`, is to acknowledge every verified purchase that is not yet acknowledged, consumables included. Today it acknowledges only when `product.kind === "non_consumable"`:

```ts
if (reply.body.grant || reply.body.status === "already_verified") {
  if (!verdict.acknowledged) {   // was: product.kind === "non_consumable" && !verdict.acknowledged
    if (await acknowledgeProduct(PACKAGE, parsed.productId, parsed.purchaseToken, accessToken)) {
      await admin.rpc("purchase_receipt_mark_acknowledged", { p_token_sha256: tokenHash });
    }
  }
}
```

Consumption stays with the client (`ConfirmPendingPurchase`), so an unconfirmed purchase is still redelivered by Unity IAP and the design's "a consumable can be bought again only after the client granted" holds. Acknowledging is what stops the automatic refund. **One thing to prove on the internal test track before relying on it:** that `purchases.products.acknowledge` accepts a consumable and that the client's later consume still succeeds. Google documents the server-side `Purchases.products:consume` for consumables. If acknowledge is refused for them, the fallback is to consume on the server and have the client grant from its own `purchase_receipts` rows (they are readable to their owner) rather than from Unity IAP's redelivery.

L1 also needs an owner decision on what a revoked receipt costs (below). Without one, a manual refund through Google still keeps the goods.

## L2 in detail: the throttle call

After `userId` is known and before `googleAccessToken`:

```ts
const { data: allow, error: allowError } = await admin.rpc("purchase_verify_allow", { p_user_id: userId });
if (allowError || !allow) return fail(500, "throttle_failed", "error");
if (!(allow as { allowed?: boolean }).allowed) {
  return send({ httpStatus: 429, body: { status: "error", grant: false, reason: "too_many_verifications" } });
}
```

`status: "error"` is deliberate: the client must not confirm on an error, so a throttled honest purchase stays pending and is verified on the next try (`DESIGN-RECEIPT-VALIDATION.md` 3.2). The tests in `functions/tests/purchase_logic.test.ts` do not cover `index.ts`, so this needs one integration run on a test project.

## What the owner must decide

1. **What a revoked receipt costs (L1).** `DESIGN-RECEIPT-VALIDATION.md` section 4 lists three options. Google's fraud guidance recommends the second (claw back unspent goods; Sealed purses make that possible for currency). Recommendation: option 2 for currency, plus option 1 (refuse new purchases after two refunds).
2. **Restoring a one-time purchase after a reinstall (L3).** Either require an account link (email or Google) before the Starter Pact or the Growth Fund can be bought, or allow the server to re-bind a non-consumable receipt to a new account when Google confirms the same Google account bought it. The second needs a design of its own, because re-binding is exactly what account binding exists to refuse. Recommendation: require the link, and say so on the offer card.
3. **Anonymous sign-up limits and CAPTCHA (A1).** These are project settings, made in the dashboard.
4. **Whether a block also ends an accepted friendship (L5).** 0021 removes only a pending request.
5. **The profile directory (S3)** before launch, which is a client change.
