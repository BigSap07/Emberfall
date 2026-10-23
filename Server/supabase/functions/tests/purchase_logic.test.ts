// purchase_logic.test.ts - the receipt functions' decisions, tested without a network.
//
// Runs under Node 22 or later with type stripping, from the repository root:
//
//     node --experimental-strip-types --test Server/supabase/functions/tests/purchase_logic.test.ts
//
// and under Deno (`deno test Server/supabase/functions/tests/`), which supports node:test.

import { test } from "node:test";
import assert from "node:assert/strict";
import { Buffer } from "node:buffer";
import {
  auditCopy,
  constantTimeEqual,
  decidePurchase,
  flag,
  ONE_TIME_PRODUCT_CANCELED,
  parseNotification,
  parseVerifyRequest,
  type ProductPurchaseV2,
  replyForRecord,
  replyForVerdict,
  sha256Hex,
  type VerifyContext,
} from "../_shared/purchase_logic.ts";

const USER = "user-under-test";
const PRODUCT = "emberfall.weekly_pack";
const TOKEN = "opaque-token-from-google-play-billing-0123456789";

async function context(overrides: Partial<VerifyContext> = {}): Promise<VerifyContext> {
  return {
    productId: PRODUCT,
    expectedAccountHash: await sha256Hex(USER),
    requireAccountBinding: true,
    allowTestPurchases: false,
    ...overrides,
  };
}

async function purchase(overrides: Partial<ProductPurchaseV2> = {}): Promise<ProductPurchaseV2> {
  return {
    productLineItem: [{ productId: PRODUCT, productOfferDetails: { quantity: 1, refundableQuantity: 1 } }],
    purchaseStateContext: { purchaseState: "PURCHASED" },
    orderId: "GPA.1111-2222-3333-44444",
    obfuscatedExternalAccountId: await sha256Hex(USER),
    regionCode: "BE",
    purchaseCompletionTime: "2026-10-22T12:00:00Z",
    acknowledgementState: "ACKNOWLEDGEMENT_STATE_PENDING",
    ...overrides,
  };
}

// ---------------------------------------------------------------- the request

test("a well-formed request is accepted", () => {
  const parsed = parseVerifyRequest({ productId: PRODUCT, purchaseToken: TOKEN });
  assert.deepEqual(parsed, { ok: true, productId: PRODUCT, purchaseToken: TOKEN });
});

test("malformed requests are refused with a reason", () => {
  assert.deepEqual(parseVerifyRequest(null), { ok: false, reason: "body_not_object" });
  assert.deepEqual(parseVerifyRequest([]), { ok: false, reason: "body_not_object" });
  assert.deepEqual(parseVerifyRequest({ purchaseToken: TOKEN }), { ok: false, reason: "bad_product_id" });
  assert.deepEqual(parseVerifyRequest({ productId: "Upper.Case", purchaseToken: TOKEN }), {
    ok: false,
    reason: "bad_product_id",
  });
  assert.deepEqual(parseVerifyRequest({ productId: PRODUCT, purchaseToken: "short" }), {
    ok: false,
    reason: "bad_purchase_token",
  });
  assert.deepEqual(parseVerifyRequest({ productId: PRODUCT, purchaseToken: "has spaces in it, not a token" }), {
    ok: false,
    reason: "bad_purchase_token",
  });
});

// ---------------------------------------------------------------- the verdict

test("a completed purchase bound to this player is verified", async () => {
  const verdict = decidePurchase(await purchase(), await context());
  assert.equal(verdict.ok, true);
  if (verdict.ok) {
    assert.equal(verdict.orderId, "GPA.1111-2222-3333-44444");
    assert.equal(verdict.quantity, 1);
    assert.equal(verdict.testPurchase, false);
    assert.equal(verdict.regionCode, "BE");
    assert.equal(verdict.acknowledged, false);
  }
});

test("a pending payment is pending, not granted and not rejected", async () => {
  const verdict = decidePurchase(await purchase({ purchaseStateContext: { purchaseState: "PENDING" } }), await context());
  assert.deepEqual(verdict, { ok: false, outcome: "pending", reason: "payment_pending" });
});

test("a cancelled or unspecified purchase is rejected", async () => {
  assert.deepEqual(
    decidePurchase(await purchase({ purchaseStateContext: { purchaseState: "CANCELLED" } }), await context()),
    { ok: false, outcome: "rejected", reason: "cancelled" },
  );
  assert.deepEqual(decidePurchase(await purchase({ purchaseStateContext: {} }), await context()), {
    ok: false,
    outcome: "rejected",
    reason: "not_purchased",
  });
});

test("a token for a different product is rejected", async () => {
  const verdict = decidePurchase(await purchase(), await context({ productId: "emberfall.monthly_pack" }));
  assert.deepEqual(verdict, { ok: false, outcome: "rejected", reason: "product_mismatch" });
});

test("a fully refunded purchase is rejected", async () => {
  const verdict = decidePurchase(
    await purchase({ productLineItem: [{ productId: PRODUCT, productOfferDetails: { quantity: 1, refundableQuantity: 0 } }] }),
    await context(),
  );
  assert.deepEqual(verdict, { ok: false, outcome: "rejected", reason: "refunded" });
});

test("a purchase started by another account is rejected", async () => {
  const verdict = decidePurchase(
    await purchase({ obfuscatedExternalAccountId: await sha256Hex("somebody-else") }),
    await context(),
  );
  assert.deepEqual(verdict, { ok: false, outcome: "rejected", reason: "account_mismatch" });
});

test("a purchase with no account binding is rejected unless binding is switched off", async () => {
  const unbound = await purchase({ obfuscatedExternalAccountId: undefined });
  assert.deepEqual(decidePurchase(unbound, await context()), {
    ok: false,
    outcome: "rejected",
    reason: "no_account_binding",
  });
  assert.equal(decidePurchase(unbound, await context({ requireAccountBinding: false })).ok, true);
});

test("a wrong binding is rejected even with binding switched off", async () => {
  const verdict = decidePurchase(
    await purchase({ obfuscatedExternalAccountId: await sha256Hex("somebody-else") }),
    await context({ requireAccountBinding: false }),
  );
  assert.deepEqual(verdict, { ok: false, outcome: "rejected", reason: "account_mismatch" });
});

test("a test-card purchase is refused in production and accepted on a test project", async () => {
  const test = await purchase({ testPurchaseContext: { fopType: "TEST" } });
  assert.deepEqual(decidePurchase(test, await context()), {
    ok: false,
    outcome: "rejected",
    reason: "test_purchase_not_allowed",
  });
  const allowed = decidePurchase(test, await context({ allowTestPurchases: true }));
  assert.equal(allowed.ok, true);
  if (allowed.ok) assert.equal(allowed.testPurchase, true);
});

test("an already acknowledged purchase says so, and odd fields are normalised", async () => {
  const verdict = decidePurchase(
    await purchase({ acknowledgementState: "ACKNOWLEDGEMENT_STATE_ACKNOWLEDGED", orderId: "", regionCode: "be" }),
    await context(),
  );
  assert.equal(verdict.ok, true);
  if (verdict.ok) {
    assert.equal(verdict.acknowledged, true);
    assert.equal(verdict.orderId, null);
    assert.equal(verdict.regionCode, null);
  }
});

test("the audit copy carries no purchase token", async () => {
  const copy = auditCopy(await purchase());
  assert.equal(JSON.stringify(copy).includes(TOKEN), false);
  assert.equal(copy.orderId, "GPA.1111-2222-3333-44444");
});

// ---------------------------------------------------------------- the reply

test("only a newly recorded receipt grants", async () => {
  const verdict = decidePurchase(await purchase(), await context());
  assert.equal(verdict.ok, true);
  if (!verdict.ok) return;
  const recorded = replyForRecord({ outcome: "recorded", receipt_id: 7, offer_id: "weekly_pack" }, verdict);
  assert.equal(recorded.httpStatus, 200);
  assert.equal(recorded.body.grant, true);
  assert.equal(recorded.body.status, "verified");

  const again = replyForRecord({ outcome: "already_recorded", receipt_id: 7, offer_id: "weekly_pack" }, verdict);
  assert.equal(again.body.grant, false);
  assert.equal(again.body.status, "already_verified");

  const revoked = replyForRecord({ outcome: "already_recorded", receipt_id: 7, revoked: true }, verdict);
  assert.deepEqual(revoked, { httpStatus: 422, body: { status: "rejected", grant: false, reason: "revoked" } });

  assert.equal(replyForRecord({ outcome: "owned_by_other" }, verdict).httpStatus, 403);
  assert.equal(replyForRecord({ outcome: "unknown_product" }, verdict).body.grant, false);
  assert.equal(replyForRecord({ outcome: "surprise" }, verdict).httpStatus, 500);
});

test("pending and rejected verdicts never grant", () => {
  assert.deepEqual(replyForVerdict({ ok: false, outcome: "pending", reason: "payment_pending" }), {
    httpStatus: 202,
    body: { status: "pending", grant: false, reason: "payment_pending" },
  });
  assert.equal(replyForVerdict({ ok: false, outcome: "rejected", reason: "cancelled" }).body.grant, false);
});

// ---------------------------------------------------------------- notifications

function envelope(note: Record<string, unknown>): unknown {
  const data = Buffer.from(JSON.stringify(note), "utf8").toString("base64");
  return { message: { data, messageId: "1" }, subscription: "projects/x/subscriptions/y" };
}

const PACKAGE = "com.example.emberfall";

test("a voided one-time purchase is read as voided", () => {
  const note = parseNotification(
    envelope({
      packageName: PACKAGE,
      voidedPurchaseNotification: { purchaseToken: TOKEN, orderId: "GPA.9", productType: 2, refundType: 1 },
    }),
    PACKAGE,
  );
  assert.deepEqual(note, { kind: "voided", purchaseToken: TOKEN, orderId: "GPA.9", refundType: 1 });
});

test("a cancelled one-time product is read with its type", () => {
  const note = parseNotification(
    envelope({
      packageName: PACKAGE,
      oneTimeProductNotification: { notificationType: ONE_TIME_PRODUCT_CANCELED, purchaseToken: TOKEN, sku: PRODUCT },
    }),
    PACKAGE,
  );
  assert.deepEqual(note, { kind: "one_time", notificationType: 2, purchaseToken: TOKEN, sku: PRODUCT });
});

test("notifications that are not ours, or not about purchases, are ignored", () => {
  assert.equal(parseNotification(envelope({ packageName: "com.other.app", testNotification: {} }), PACKAGE).kind, "ignored");
  assert.deepEqual(parseNotification(envelope({ packageName: PACKAGE, testNotification: { version: "1.0" } }), PACKAGE), {
    kind: "test",
  });
  assert.deepEqual(
    parseNotification(
      envelope({ packageName: PACKAGE, voidedPurchaseNotification: { purchaseToken: TOKEN, productType: 1 } }),
      PACKAGE,
    ),
    { kind: "ignored", reason: "subscription" },
  );
  assert.deepEqual(parseNotification(envelope({ packageName: PACKAGE, subscriptionNotification: {} }), PACKAGE), {
    kind: "ignored",
    reason: "subscription",
  });
});

test("malformed envelopes are invalid, not exceptions", () => {
  assert.equal(parseNotification(null, PACKAGE).kind, "invalid");
  assert.equal(parseNotification({}, PACKAGE).kind, "invalid");
  assert.equal(parseNotification({ message: { data: "!!!not base64" } }, PACKAGE).kind, "invalid");
  assert.equal(
    parseNotification({ message: { data: Buffer.from("not json").toString("base64") } }, PACKAGE).kind,
    "invalid",
  );
  assert.equal(
    parseNotification(envelope({ packageName: PACKAGE, voidedPurchaseNotification: {} }), PACKAGE).kind,
    "invalid",
  );
});

// ---------------------------------------------------------------- helpers

test("sha256Hex matches a known vector", async () => {
  assert.equal(await sha256Hex("abc"), "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
});

test("constantTimeEqual compares exactly", () => {
  assert.equal(constantTimeEqual("abc", "abc"), true);
  assert.equal(constantTimeEqual("abc", "abd"), false);
  assert.equal(constantTimeEqual("abc", "abcd"), false);
});

test("flag reads the usual spellings and falls back when unset", () => {
  assert.equal(flag(undefined, true), true);
  assert.equal(flag("", false), false);
  assert.equal(flag("TRUE", false), true);
  assert.equal(flag("0", true), false);
  assert.equal(flag("off", true), false);
});
