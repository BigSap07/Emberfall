# Emberfall - Google Play receipt validation

**Status: designed, written and tested locally. Not deployed, not applied, and not yet called by
the game.** This is a decision record from 2026-10-22 in the sense `CLAUDE.md` gives: the reasoning
is durable, the status lines are not. `DESIGN-BUILD-ORDER.md` is where status lives.

| Piece | Where | State |
|---|---|---|
| Receipt ledger and product list | `Server/supabase/migrations/0019_purchase_receipts.sql` | written, not applied |
| Verification function | `Server/supabase/functions/verify-google-purchase/index.ts` | type-checked, not deployed |
| Refund notification function | `Server/supabase/functions/google-play-rtdn/index.ts` | type-checked, not deployed |
| Decision logic | `Server/supabase/functions/_shared/purchase_logic.ts` | 22 tests pass under Node and Deno |
| Google API calls | `Server/supabase/functions/_shared/google_play.ts` | type-checked |
| Client side | `PurchaseService`, `UnityIapBackend`, `PurchaseLedger` | **not started**, owner: code |

---

## 1. The problem

The Store (R19 and R24) grants everything on the device. `PurchaseFlow` hands a completed purchase
to `PurchaseLedger.Complete`, which grants once per receipt id through `PurchaseGrants.Apply`, and
the save records it. Nothing outside the device ever sees the purchase. So:

- **A forged purchase is believed.** Tools that fake the Play Billing response on a rooted device
  produce a "purchase" the client cannot tell from a real one.
- **A refund keeps the goods.** A player can buy, receive, and ask Google for their money back. The
  game never hears about it.
- **A receipt can be replayed.** The same token pasted into another account is a second set of
  goods.
- **Nothing is recorded server-side**, so a support request ("I paid and got nothing") has nothing
  to check against, and accounting has no record but Google's.

## 2. What this design does, and what it does not

**It does** make the server the judge of whether a purchase is real, bind each purchase to the
account that started it, record every verified purchase once, and mark refunded ones.

**It does not** make the save server-authoritative. The device still holds the save and still runs
`PurchaseGrants.Apply`. A player who edits their own save can still give themselves anything; that
was true before and is a far larger problem (server-side economy) that this design deliberately
leaves alone. What changes is that a purchase is no longer the easy way to do it, and that real
money is never involved in a forged grant.

## 3. The flow

```
Device (Unity IAP)                     Edge Function                    Google / Database
------------------                     -------------                    -----------------
Start purchase with
 obfuscatedAccountId = sha256(userId)
Play Billing completes  ---------->
ProcessPurchase(token, productId)
  POST verify-google-purchase  ------> check the Supabase JWT
   { productId, purchaseToken }        product active?  -------------> store_products
                                       productsv2.getproductpurchasev2 -> Google Play API
                                       decidePurchase(...)
                                       purchase_receipt_record(...) ---> purchase_receipts
                                       acknowledge (non-consumables) --> Google Play API
  <------ { status, grant, receiptId, offerId }
if grant: PurchaseLedger.Complete -> PurchaseGrants.Apply
if status is not pending/error: ConfirmPendingPurchase (consumes a consumable)
```

### 3.1 Binding a purchase to an account

The client sets Unity IAP's Google obfuscated account id to the lowercase hex SHA-256 of the
player's Supabase user id before it starts a purchase. Google stores it with the purchase and
echoes it back as `obfuscatedExternalAccountId`. The server computes the same hash from the JWT it
was called with and refuses a purchase whose binding does not match (`account_mismatch`). A token
copied to another account therefore fails even before the ledger's uniqueness rule is reached.

`REQUIRE_ACCOUNT_BINDING` (default true) refuses a purchase with no binding at all. It exists to be
switched off only if a client build without the binding is ever live; a wrong binding is refused
either way.

### 3.2 The verdict

`decidePurchase` in `purchase_logic.ts` reads Google's `ProductPurchaseV2` answer:

| Google says | Verdict | HTTP | Client does |
|---|---|---|---|
| `PENDING` | pending | 202 | nothing yet; asks again on the next launch or when Unity IAP redelivers |
| `CANCELLED`, unspecified | rejected | 422 | does not grant, confirms the purchase to clear it |
| product id differs | rejected `product_mismatch` | 422 | as above |
| every unit refunded | rejected `refunded` | 422 | as above |
| binding missing or different | rejected | 422 | as above |
| test card, `ALLOW_TEST_PURCHASES` off | rejected | 422 | as above |
| `PURCHASED`, all checks pass | goes to the ledger | | |

Then the ledger (`purchase_receipt_record` in 0019) decides:

| Ledger outcome | Reply | `grant` |
|---|---|---|
| `recorded`, a new row | `verified`, 200 | **true** |
| `already_recorded` by this player | `already_verified`, 200 | false |
| `already_recorded` but revoked | `rejected revoked`, 422 | false |
| `owned_by_other` | `rejected`, 403 | false |
| `unknown_product` | `rejected`, 422 | false |

**`grant` is the only field the client acts on.** It is true exactly once per purchase token, so a
retry, a restore or a second device can never grant twice, even if the client's own receipt record
was lost.

Errors on our side (Google unreachable, database down, server not configured) answer
`status: "error"` with 500 or 502. The client must **not** confirm the purchase on an error, so
Unity IAP keeps it pending and redelivers it; Google refunds an unacknowledged purchase only after
three days.

### 3.3 Consumables and non-consumables

`store_products.kind` says which an offer is. The Starter Pact and the Growth Fund are
non-consumable (bought once, restorable, matching `restorable: true` in `store.json`); everything
else can be bought again and is consumable.

- A **non-consumable** is acknowledged by the server once it is recorded.
- A **consumable** is consumed by the client through `ConfirmPendingPurchase`, which also
  acknowledges it. The server does not consume, because consuming is what lets the player buy the
  item again, and that should happen only after the client has granted.

### 3.4 Restore

Today only the Starter Pact and the Growth Fund restore (`PurchaseLedger.Restore`). With the server
ledger, a restore on a new device calls the same function with each restored token: the answer is
`already_verified` with the `offerId`, and the client re-marks the offer as owned without granting
the goods a second time. A player can also read their own receipts directly
(`purchase_receipts`, own rows, a handful of columns), which is enough for a "your purchases" page.

## 4. Refunds and cancellations

Google Play publishes **Real-time developer notifications** to a Cloud Pub/Sub topic. A push
subscription posts them to `google-play-rtdn`, with a Google-signed OIDC token that the function
verifies (issuer, audience, the push service account's email) before it reads the message.

| Notification | Action |
|---|---|
| `voidedPurchaseNotification` (refund, chargeback) | `purchase_receipt_revoke`, reason `voided` or `voided_partial` |
| `oneTimeProductNotification` type 2, cancelled | re-read the purchase; revoke if Google says `CANCELLED` |
| `oneTimeProductNotification` type 1, purchased | nothing; the client verifies its own purchases |
| test, another package, subscriptions | nothing |

Pub/Sub retries anything that is not 2xx, so the function answers 401 to a caller that is not
Google, 200 to a malformed message (a retry would never help), and 500 when the database or Google
failed (so the message comes back).

One-time product notifications are published only if the owner opts into them when configuring
RTDN in the Play Console. Voided purchase notifications need no opt-in. As a backstop, the Voided
Purchases API can be polled; that is not built.

**What a revoked receipt costs the player is an owner decision.** Revoking marks the row; it takes
nothing back, because the goods are in a save on the device. The options, cheapest first:

1. Record only, and refuse future purchases from an account with repeated refunds.
2. Have the client read its revoked receipts at boot and remove what is still unspent from that
   purchase's Sealed purses (Sealed goods are tracked separately, so this is possible for currency).
3. Flag the account server-side and keep it out of guild and PvP leaderboards when those exist.

## 5. Setting it up (owner, when the store goes live)

1. **Play Console.** Create the in-app products. Each product id must match `store.json`'s
   `productId` for its offer, and `store_products.product_id` for the same offer.
2. **Google Cloud.** In the project linked to the Play Console, enable the Google Play Android
   Developer API. Create a service account, create a JSON key for it, and keep the key out of the
   repository.
3. **Play Console, Users and permissions.** Invite the service account's email with "View financial
   data, orders and cancellation survey responses" and "Manage orders and subscriptions" for this
   app.
4. **Supabase.** Apply `0019_purchase_receipts.sql`. Then for every live offer:
   `update public.store_products set product_id = '<real id>', active = true, updated_at = now() where offer_id = '<offer id>';`
5. **Secrets** (`supabase secrets set NAME=value`):
   - `GOOGLE_PLAY_PACKAGE_NAME`: the release application id (today's bundle id is
     `com.emberfall.prototype`; the release id is an owner decision and cannot change after launch)
   - `GOOGLE_SERVICE_ACCOUNT_JSON`: the whole key file's contents
   - `ALLOW_TEST_PURCHASES`: `true` while testing on internal or closed tracks, `false` in production
   - `RTDN_AUDIENCE`: the full URL of the `google-play-rtdn` function
   - `RTDN_PUSH_SERVICE_ACCOUNT`: the email of the service account the push subscription signs as
6. **Deploy.**
   `supabase functions deploy verify-google-purchase` (JWT verification on, the default) and
   `supabase functions deploy google-play-rtdn --no-verify-jwt` (Google's token is not a Supabase
   one; the function verifies it itself).
7. **Pub/Sub.** Create a topic, grant `google-play-developer-notifications@system.gserviceaccount.com`
   the Publisher role on it, create a **push** subscription to the `google-play-rtdn` URL with
   authentication enabled (service account from step 5, audience = the function URL), and set the
   topic in Play Console, Monetisation setup, Real-time developer notifications. Send the test
   notification and look for `rtdn: test notification received` in the function's logs.

## 6. What the client has to change (owner: code)

None of this is built; the server side is ready for it. In `Assets` (not touched by this work):

1. **Binding.** Before a purchase, set the Google obfuscated account id to `sha256(userId)` hex
   through Unity IAP's Google Play extension.
2. **Verify before granting.** In the live store path (`EMBERFALL_STORE_LIVE`), `UnityIapBackend`
   posts `{productId, purchaseToken}` to `verify-google-purchase` with the player's access token, and
   `PurchaseFlow` completes the ledger only when `grant` is true. The receipt id the ledger keys on
   stays what it is today.
3. **Confirm after settling.** Call `ConfirmPendingPurchase` for any reply but `pending` and `error`.
4. **Offline.** A purchase cannot be verified offline. Play Billing needs a connection anyway; a
   reply that never arrives is treated as `error` and the purchase waits.
5. **Signed out of the cloud.** A build with no cloud config cannot verify, so the live store must
   require the cloud. A release build without `supabase.json` keeps saying COMING SOON.
6. **Restore** (section 3.4) and, if chosen, the revoked-receipt behaviour (section 4).

## 7. How it was checked

- **The SQL** was applied to a local PostgreSQL 16 with a stand-in for Supabase's `auth` schema,
  roles and default grants, after migrations 0001 to 0018, applied twice to prove it re-appliable,
  and exercised as both a player (`authenticated`) and the Edge Function (`service_role`): a client
  cannot call the writers, insert a receipt, read the token hash or the product list; the service
  role records, sees a repeat as `already_recorded`, refuses the same token or order id from another
  player as `owned_by_other`, refuses an inactive product, and revokes once. The account deletion in
  0016 leaves the receipt with no user attached.
- **The logic** is covered by `Server/supabase/functions/tests/purchase_logic.test.ts`, 22 tests,
  run with `node --experimental-strip-types --test` and `deno test`.
- **The two functions** pass `deno check`.
- **Not checked:** a call to Google's real API, and a deployed function. Both need the Play Console
  products and a service account, which do not exist yet.
- **The live client-side refusals** are in `Tools/supabase-launch-rules-test.py`, section PURCHASE
  RECEIPTS, to run once 0019 is applied.
