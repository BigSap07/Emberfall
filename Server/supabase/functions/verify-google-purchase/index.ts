// verify-google-purchase - the game asks the server whether a Google Play purchase is real.
//
// NOT DEPLOYED. Docs/DESIGN-RECEIPT-VALIDATION.md is the design, the setup and the client contract.
//
// POST, with the player's own Supabase access token in Authorization (the gateway checks it,
// because this function is deployed WITH JWT verification), and a JSON body:
//
//     { "productId": "<Google product id>", "purchaseToken": "<token from Unity IAP>" }
//
// Answers { status, grant, reason?, receiptId?, offerId?, orderId?, testPurchase? }. The client runs
// PurchaseGrants.Apply only when grant is true, and calls Unity IAP's ConfirmPendingPurchase only
// after any answer that is not "error" or "pending", so Google keeps retrying a purchase the server
// could not settle.
//
// Secrets (supabase secrets set ...):
//   GOOGLE_PLAY_PACKAGE_NAME     the release application id
//   GOOGLE_SERVICE_ACCOUNT_JSON  the service account key, the whole JSON document
//   REQUIRE_ACCOUNT_BINDING      default true; false only while a client without the binding is live
//   ALLOW_TEST_PURCHASES         default false; true on a project that serves testing tracks
// Provided by Supabase to every Edge Function: SUPABASE_URL, SUPABASE_SERVICE_ROLE_KEY.

import { createClient } from "npm:@supabase/supabase-js@2";
import { acknowledgeProduct, getProductPurchaseV2, googleAccessToken } from "../_shared/google_play.ts";
import {
  auditCopy,
  decidePurchase,
  flag,
  parseVerifyRequest,
  type Reply,
  replyForRecord,
  replyForVerdict,
  sha256Hex,
} from "../_shared/purchase_logic.ts";

const PACKAGE = Deno.env.get("GOOGLE_PLAY_PACKAGE_NAME") ?? "";
const SERVICE_ACCOUNT = Deno.env.get("GOOGLE_SERVICE_ACCOUNT_JSON") ?? "";
const REQUIRE_BINDING = flag(Deno.env.get("REQUIRE_ACCOUNT_BINDING"), true);
const ALLOW_TEST = flag(Deno.env.get("ALLOW_TEST_PURCHASES"), false);

// The service role client. It never leaves this function: it reads store_products and calls the
// three receipt writers in 0019, which no other role may call.
const admin = createClient(Deno.env.get("SUPABASE_URL") ?? "", Deno.env.get("SUPABASE_SERVICE_ROLE_KEY") ?? "", {
  auth: { persistSession: false, autoRefreshToken: false },
});

function send(reply: Reply): Response {
  return new Response(JSON.stringify(reply.body), {
    status: reply.httpStatus,
    headers: { "Content-Type": "application/json" },
  });
}

function fail(httpStatus: number, reason: string, status: "rejected" | "error" = "rejected"): Response {
  return send({ httpStatus, body: { status, grant: false, reason } });
}

Deno.serve(async (req: Request): Promise<Response> => {
  if (req.method !== "POST") return fail(405, "method_not_allowed");
  if (!PACKAGE || !SERVICE_ACCOUNT) return fail(500, "server_not_configured", "error");

  // Who is asking. The gateway already refused a request with no valid token; this turns the token
  // into a user id, and refuses an anon key presented as a token (it has no user).
  const jwt = (req.headers.get("Authorization") ?? "").replace(/^Bearer\s+/i, "");
  const { data: userData, error: userError } = await admin.auth.getUser(jwt);
  if (userError || !userData?.user?.id) return fail(401, "not_signed_in");
  const userId = userData.user.id;

  let raw: unknown;
  try {
    raw = await req.json();
  } catch {
    return fail(400, "body_not_json");
  }
  const parsed = parseVerifyRequest(raw);
  if (!parsed.ok) return fail(400, parsed.reason);

  // Refuse a product the server does not sell before asking Google anything.
  const { data: product, error: productError } = await admin
    .from("store_products")
    .select("product_id, offer_id, kind")
    .eq("product_id", parsed.productId)
    .eq("active", true)
    .maybeSingle();
  if (productError) return fail(500, "product_lookup_failed", "error");
  if (!product) return fail(422, "unknown_product");

  let accessToken: string;
  try {
    accessToken = await googleAccessToken(SERVICE_ACCOUNT);
  } catch {
    return fail(502, "google_auth_failed", "error");
  }

  const fetched = await getProductPurchaseV2(PACKAGE, parsed.purchaseToken, accessToken);
  if (fetched.kind === "not_found") return fail(422, "unknown_purchase_token");
  if (fetched.kind === "error") return fail(502, "google_unavailable", "error");

  const verdict = decidePurchase(fetched.value, {
    productId: parsed.productId,
    expectedAccountHash: await sha256Hex(userId),
    requireAccountBinding: REQUIRE_BINDING,
    allowTestPurchases: ALLOW_TEST,
  });
  if (!verdict.ok) return send(replyForVerdict(verdict));

  const tokenHash = await sha256Hex(parsed.purchaseToken);
  const { data: record, error: recordError } = await admin.rpc("purchase_receipt_record", {
    p_user_id: userId,
    p_product_id: parsed.productId,
    p_order_id: verdict.orderId,
    p_token_sha256: tokenHash,
    p_quantity: verdict.quantity,
    p_test_purchase: verdict.testPurchase,
    p_region_code: verdict.regionCode,
    p_purchased_at: verdict.purchasedAt,
    p_acknowledged: verdict.acknowledged,
    p_google_response: auditCopy(fetched.value),
  });
  if (recordError || !record) return fail(500, "record_failed", "error");

  const reply = replyForRecord(record as Record<string, unknown>, verdict);

  // A non-consumable is acknowledged here, once it is safely recorded. A failure is not fatal: the
  // purchase is recorded, the client's ConfirmPendingPurchase also acknowledges, and the next
  // verification of the same token tries again.
  if (reply.body.grant || reply.body.status === "already_verified") {
    if (product.kind === "non_consumable" && !verdict.acknowledged) {
      if (await acknowledgeProduct(PACKAGE, parsed.productId, parsed.purchaseToken, accessToken)) {
        await admin.rpc("purchase_receipt_mark_acknowledged", { p_token_sha256: tokenHash });
      }
    }
  }

  return send(reply);
});
