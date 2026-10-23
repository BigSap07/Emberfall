// purchase_logic.ts - the decisions the receipt functions make, with no network and no Deno API.
//
// Everything here is a pure function of its inputs, so it runs unchanged under Deno (the Edge
// Functions) and under Node 22 with type stripping (the tests in ../tests). The two index.ts files
// do the I/O: read the request, ask Google, call the database. Every judgement they make about what
// came back is made here, where a test can reach it.
//
// Docs/DESIGN-RECEIPT-VALIDATION.md is the design this implements.

// ---------------------------------------------------------------- Google's answer, as far as we read it
//
// purchases.productsv2.getproductpurchasev2. Only the fields read below are typed; Google may add
// more and they are ignored.
export type PurchaseState = "PURCHASE_STATE_UNSPECIFIED" | "PURCHASED" | "CANCELLED" | "PENDING";

export interface ProductLineItem {
  productId?: string;
  productOfferDetails?: {
    quantity?: number;
    refundableQuantity?: number;
    consumptionState?: string;
  };
}

export interface ProductPurchaseV2 {
  productLineItem?: ProductLineItem[];
  purchaseStateContext?: { purchaseState?: PurchaseState };
  testPurchaseContext?: { fopType?: string };
  orderId?: string;
  obfuscatedExternalAccountId?: string;
  regionCode?: string;
  purchaseCompletionTime?: string;
  acknowledgementState?: string;
}

// ---------------------------------------------------------------- the request the game sends

// Google's rule for a product id: lowercase letters, digits, underscores and periods, starting with
// a letter or digit. The 150 cap is generous; Google's own limit is lower.
const PRODUCT_ID = /^[a-z0-9][a-z0-9._]{0,148}$/;
// A purchase token is an opaque string Google hands the device. Real ones are a few hundred
// characters of URL-safe text; anything else is not one.
const PURCHASE_TOKEN = /^[A-Za-z0-9._\-]{20,4096}$/;

export type VerifyRequest =
  | { ok: true; productId: string; purchaseToken: string }
  | { ok: false; reason: string };

export function parseVerifyRequest(body: unknown): VerifyRequest {
  if (body === null || typeof body !== "object" || Array.isArray(body)) {
    return { ok: false, reason: "body_not_object" };
  }
  const record = body as Record<string, unknown>;
  const productId = record.productId;
  const purchaseToken = record.purchaseToken;
  if (typeof productId !== "string" || !PRODUCT_ID.test(productId)) {
    return { ok: false, reason: "bad_product_id" };
  }
  if (typeof purchaseToken !== "string" || !PURCHASE_TOKEN.test(purchaseToken)) {
    return { ok: false, reason: "bad_purchase_token" };
  }
  return { ok: true, productId, purchaseToken };
}

// ---------------------------------------------------------------- the verdict on a purchase

export interface VerifyContext {
  // The product the client said it bought. Google's answer must name the same one.
  productId: string;
  // sha256 hex of the signed-in player's user id. The client passes the same value to Unity IAP as
  // the obfuscated account id when it starts the purchase, so Google echoes it back here.
  expectedAccountHash: string;
  // When true, a purchase with no obfuscated account id, or a different one, is refused. Only turn
  // it off for a client build that predates the binding.
  requireAccountBinding: boolean;
  // When false, a purchase made with a Play Console test card is refused. True on internal and
  // closed testing tracks, false in production.
  allowTestPurchases: boolean;
}

export type Verdict =
  | {
    ok: true;
    orderId: string | null;
    quantity: number;
    testPurchase: boolean;
    regionCode: string | null;
    purchasedAt: string | null;
    acknowledged: boolean;
  }
  | { ok: false; outcome: "pending" | "rejected"; reason: string };

export function decidePurchase(purchase: ProductPurchaseV2, ctx: VerifyContext): Verdict {
  const state = purchase.purchaseStateContext?.purchaseState ?? "PURCHASE_STATE_UNSPECIFIED";
  if (state === "PENDING") {
    // A slow payment method (cash at a shop, some bank transfers). Nothing is granted until it
    // completes; the client asks again later, or the purchase arrives through Unity IAP once paid.
    return { ok: false, outcome: "pending", reason: "payment_pending" };
  }
  if (state !== "PURCHASED") {
    return { ok: false, outcome: "rejected", reason: state === "CANCELLED" ? "cancelled" : "not_purchased" };
  }

  const items = purchase.productLineItem ?? [];
  const line = items.find((item) => item.productId === ctx.productId);
  if (!line) {
    return { ok: false, outcome: "rejected", reason: "product_mismatch" };
  }

  const offer = line.productOfferDetails ?? {};
  const quantity = Number.isInteger(offer.quantity) && (offer.quantity as number) > 0 ? offer.quantity as number : 1;
  if (offer.refundableQuantity === 0) {
    // Every unit of this purchase has been refunded already.
    return { ok: false, outcome: "rejected", reason: "refunded" };
  }

  const bound = purchase.obfuscatedExternalAccountId ?? "";
  if (bound === "") {
    if (ctx.requireAccountBinding) {
      return { ok: false, outcome: "rejected", reason: "no_account_binding" };
    }
  } else if (!constantTimeEqual(bound, ctx.expectedAccountHash)) {
    // The purchase was started by a different player's account. This is how a receipt copied from
    // another device shows up.
    return { ok: false, outcome: "rejected", reason: "account_mismatch" };
  }

  const testPurchase = purchase.testPurchaseContext !== undefined && purchase.testPurchaseContext !== null;
  if (testPurchase && !ctx.allowTestPurchases) {
    return { ok: false, outcome: "rejected", reason: "test_purchase_not_allowed" };
  }

  return {
    ok: true,
    orderId: purchase.orderId && purchase.orderId.length > 0 ? purchase.orderId : null,
    quantity,
    testPurchase,
    regionCode: purchase.regionCode && /^[A-Z]{2}$/.test(purchase.regionCode) ? purchase.regionCode : null,
    purchasedAt: purchase.purchaseCompletionTime ?? null,
    acknowledged: purchase.acknowledgementState === "ACKNOWLEDGEMENT_STATE_ACKNOWLEDGED",
  };
}

// What is kept of Google's answer in purchase_receipts.google_response. Everything that is useful
// for a dispute, nothing that is a secret (the answer carries no token).
export function auditCopy(purchase: ProductPurchaseV2): Record<string, unknown> {
  return {
    orderId: purchase.orderId ?? null,
    purchaseState: purchase.purchaseStateContext?.purchaseState ?? null,
    acknowledgementState: purchase.acknowledgementState ?? null,
    purchaseCompletionTime: purchase.purchaseCompletionTime ?? null,
    regionCode: purchase.regionCode ?? null,
    test: purchase.testPurchaseContext ? true : false,
    lineItems: (purchase.productLineItem ?? []).map((item) => ({
      productId: item.productId ?? null,
      quantity: item.productOfferDetails?.quantity ?? null,
      refundableQuantity: item.productOfferDetails?.refundableQuantity ?? null,
      consumptionState: item.productOfferDetails?.consumptionState ?? null,
    })),
  };
}

// ---------------------------------------------------------------- the answer the game receives
//
// The database's outcome (0019 purchase_receipt_record) and Google's verdict, turned into one reply.
// `grant` is the only field the client acts on: true means "run PurchaseGrants.Apply for this
// receipt once"; false means do not grant (again).
export interface Reply {
  httpStatus: number;
  body: {
    status: "verified" | "already_verified" | "pending" | "rejected" | "error";
    grant: boolean;
    reason?: string;
    receiptId?: number;
    offerId?: string;
    orderId?: string | null;
    testPurchase?: boolean;
  };
}

export function replyForRecord(
  record: { outcome?: string; receipt_id?: number; offer_id?: string; revoked?: boolean },
  verdict: Extract<Verdict, { ok: true }>,
): Reply {
  switch (record.outcome) {
    case "recorded":
      return {
        httpStatus: 200,
        body: {
          status: "verified", grant: true, receiptId: record.receipt_id, offerId: record.offer_id,
          orderId: verdict.orderId, testPurchase: verdict.testPurchase,
        },
      };
    case "already_recorded":
      if (record.revoked) {
        return { httpStatus: 422, body: { status: "rejected", grant: false, reason: "revoked" } };
      }
      return {
        httpStatus: 200,
        body: {
          status: "already_verified", grant: false, receiptId: record.receipt_id, offerId: record.offer_id,
          orderId: verdict.orderId, testPurchase: verdict.testPurchase,
        },
      };
    case "owned_by_other":
      return { httpStatus: 403, body: { status: "rejected", grant: false, reason: "owned_by_other" } };
    case "unknown_product":
      return { httpStatus: 422, body: { status: "rejected", grant: false, reason: "unknown_product" } };
    default:
      return { httpStatus: 500, body: { status: "error", grant: false, reason: "unexpected_record_outcome" } };
  }
}

export function replyForVerdict(verdict: Extract<Verdict, { ok: false }>): Reply {
  if (verdict.outcome === "pending") {
    return { httpStatus: 202, body: { status: "pending", grant: false, reason: verdict.reason } };
  }
  return { httpStatus: 422, body: { status: "rejected", grant: false, reason: verdict.reason } };
}

// ---------------------------------------------------------------- real-time developer notifications
//
// Google Play publishes a DeveloperNotification to a Pub/Sub topic, and a push subscription posts
// {"message": {"data": <base64 JSON>, "messageId": ...}, "subscription": ...} to the RTDN function.

export type Notification =
  | { kind: "voided"; purchaseToken: string; orderId: string | null; refundType: number | null }
  | { kind: "one_time"; notificationType: number; purchaseToken: string; sku: string | null }
  | { kind: "test" }
  | { kind: "ignored"; reason: string }
  | { kind: "invalid"; reason: string };

// OneTimeProductNotification.notificationType
export const ONE_TIME_PRODUCT_PURCHASED = 1;
export const ONE_TIME_PRODUCT_CANCELED = 2;

export function parseNotification(envelope: unknown, expectedPackage: string): Notification {
  if (envelope === null || typeof envelope !== "object") return { kind: "invalid", reason: "envelope_not_object" };
  const message = (envelope as Record<string, unknown>).message;
  if (message === null || typeof message !== "object") return { kind: "invalid", reason: "no_message" };
  const data = (message as Record<string, unknown>).data;
  if (typeof data !== "string" || data.length === 0 || data.length > 65536) {
    return { kind: "invalid", reason: "no_data" };
  }

  let note: Record<string, unknown>;
  try {
    note = JSON.parse(decodeBase64(data)) as Record<string, unknown>;
  } catch {
    return { kind: "invalid", reason: "data_not_json" };
  }
  if (note === null || typeof note !== "object") return { kind: "invalid", reason: "data_not_object" };
  if (note.packageName !== expectedPackage) return { kind: "ignored", reason: "other_package" };

  if (note.testNotification) return { kind: "test" };

  const voided = note.voidedPurchaseNotification as Record<string, unknown> | undefined;
  if (voided && typeof voided === "object") {
    if (typeof voided.purchaseToken !== "string" || voided.purchaseToken.length === 0) {
      return { kind: "invalid", reason: "voided_without_token" };
    }
    // productType 1 is a subscription. This game sells none, so a voided subscription is not ours.
    if (voided.productType === 1) return { kind: "ignored", reason: "subscription" };
    return {
      kind: "voided",
      purchaseToken: voided.purchaseToken,
      orderId: typeof voided.orderId === "string" && voided.orderId.length > 0 ? voided.orderId : null,
      refundType: typeof voided.refundType === "number" ? voided.refundType : null,
    };
  }

  const oneTime = note.oneTimeProductNotification as Record<string, unknown> | undefined;
  if (oneTime && typeof oneTime === "object") {
    if (typeof oneTime.purchaseToken !== "string" || typeof oneTime.notificationType !== "number") {
      return { kind: "invalid", reason: "one_time_incomplete" };
    }
    return {
      kind: "one_time",
      notificationType: oneTime.notificationType,
      purchaseToken: oneTime.purchaseToken,
      sku: typeof oneTime.sku === "string" ? oneTime.sku : null,
    };
  }

  if (note.subscriptionNotification) return { kind: "ignored", reason: "subscription" };
  return { kind: "ignored", reason: "unknown_notification" };
}

// ---------------------------------------------------------------- small helpers

export async function sha256Hex(text: string): Promise<string> {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(text));
  return Array.from(new Uint8Array(digest)).map((b) => b.toString(16).padStart(2, "0")).join("");
}

// Equal-length strings compared without an early exit, so the comparison time does not say how
// many leading characters matched. Different lengths are simply unequal.
export function constantTimeEqual(a: string, b: string): boolean {
  if (a.length !== b.length) return false;
  let diff = 0;
  for (let i = 0; i < a.length; i++) diff |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return diff === 0;
}

// Pub/Sub sends standard base64. atob gives a binary string; decode it as UTF-8.
export function decodeBase64(data: string): string {
  const binary = atob(data);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return new TextDecoder().decode(bytes);
}

export function flag(value: string | undefined, fallback: boolean): boolean {
  if (value === undefined || value.trim() === "") return fallback;
  return ["1", "true", "yes", "on"].includes(value.trim().toLowerCase());
}
