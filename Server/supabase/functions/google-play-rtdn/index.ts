// google-play-rtdn - Google Play tells the server about refunds and cancellations.
//
// NOT DEPLOYED. Docs/DESIGN-RECEIPT-VALIDATION.md, "Refunds", is the design.
//
// Google Play publishes Real-time developer notifications to a Cloud Pub/Sub topic. A PUSH
// subscription on that topic posts each one here, with a Google-signed OIDC token in Authorization.
// This function is deployed WITHOUT Supabase JWT verification (--no-verify-jwt), because that token
// is Google's, not Supabase's; it verifies Google's token itself before reading anything.
//
// What it does:
//   voided purchase (a refund or a chargeback)  -> purchase_receipt_revoke, reason voided
//   one-time product cancelled                   -> re-read the purchase; revoke if Google says CANCELLED
//   one-time product purchased                   -> nothing (the client verifies its own purchases)
//   test notification, other package, subscription -> nothing
//
// Revoking marks the receipt. It takes nothing back from the save, which lives on the device; what
// a revoked receipt should cost the player is an owner decision (the design doc lists the options).
//
// Pub/Sub treats 2xx as delivered and retries anything else, so: a request that fails the token
// check gets 401 (it is not from Google); a malformed message gets 200 and a log line (retrying it
// would never help); a database or Google failure gets 500 so the message comes back.
//
// Secrets:
//   GOOGLE_PLAY_PACKAGE_NAME     the release application id
//   GOOGLE_SERVICE_ACCOUNT_JSON  as verify-google-purchase (to re-read a cancelled purchase)
//   RTDN_AUDIENCE                the audience set on the push subscription (this function's URL)
//   RTDN_PUSH_SERVICE_ACCOUNT    the service account email the push subscription signs as

import { createClient } from "npm:@supabase/supabase-js@2";
import { createRemoteJWKSet, jwtVerify } from "npm:jose@5";
import { getProductPurchaseV2, googleAccessToken } from "../_shared/google_play.ts";
import { ONE_TIME_PRODUCT_CANCELED, parseNotification, sha256Hex } from "../_shared/purchase_logic.ts";

const PACKAGE = Deno.env.get("GOOGLE_PLAY_PACKAGE_NAME") ?? "";
const SERVICE_ACCOUNT = Deno.env.get("GOOGLE_SERVICE_ACCOUNT_JSON") ?? "";
const AUDIENCE = Deno.env.get("RTDN_AUDIENCE") ?? "";
const PUSH_ACCOUNT = Deno.env.get("RTDN_PUSH_SERVICE_ACCOUNT") ?? "";

const GOOGLE_KEYS = createRemoteJWKSet(new URL("https://www.googleapis.com/oauth2/v3/certs"));

const admin = createClient(Deno.env.get("SUPABASE_URL") ?? "", Deno.env.get("SUPABASE_SERVICE_ROLE_KEY") ?? "", {
  auth: { persistSession: false, autoRefreshToken: false },
});

async function fromGoogle(req: Request): Promise<boolean> {
  const token = (req.headers.get("Authorization") ?? "").replace(/^Bearer\s+/i, "");
  if (!token) return false;
  try {
    const { payload } = await jwtVerify(token, GOOGLE_KEYS, {
      issuer: ["https://accounts.google.com", "accounts.google.com"],
      audience: AUDIENCE,
    });
    return payload.email === PUSH_ACCOUNT && payload.email_verified === true;
  } catch {
    return false;
  }
}

async function revoke(purchaseToken: string, orderId: string | null, reason: string): Promise<boolean> {
  const { error } = await admin.rpc("purchase_receipt_revoke", {
    p_token_sha256: await sha256Hex(purchaseToken),
    p_order_id: orderId,
    p_reason: reason,
  });
  return !error;
}

Deno.serve(async (req: Request): Promise<Response> => {
  if (req.method !== "POST") return new Response(null, { status: 405 });
  if (!PACKAGE || !AUDIENCE || !PUSH_ACCOUNT) return new Response(null, { status: 500 });
  if (!(await fromGoogle(req))) return new Response(null, { status: 401 });

  let envelope: unknown;
  try {
    envelope = await req.json();
  } catch {
    console.log("rtdn: body is not JSON, acknowledged and dropped");
    return new Response(null, { status: 200 });
  }

  const note = parseNotification(envelope, PACKAGE);
  switch (note.kind) {
    case "invalid":
    case "ignored":
      console.log("rtdn: " + note.kind + " (" + note.reason + ")");
      return new Response(null, { status: 200 });
    case "test":
      console.log("rtdn: test notification received");
      return new Response(null, { status: 200 });
    case "voided": {
      const reason = note.refundType === 2 ? "voided_partial" : "voided";
      return new Response(null, { status: (await revoke(note.purchaseToken, note.orderId, reason)) ? 200 : 500 });
    }
    case "one_time": {
      if (note.notificationType !== ONE_TIME_PRODUCT_CANCELED) return new Response(null, { status: 200 });
      if (!SERVICE_ACCOUNT) return new Response(null, { status: 500 });
      let accessToken: string;
      try {
        accessToken = await googleAccessToken(SERVICE_ACCOUNT);
      } catch {
        return new Response(null, { status: 500 });
      }
      const fetched = await getProductPurchaseV2(PACKAGE, note.purchaseToken, accessToken);
      if (fetched.kind === "error") return new Response(null, { status: 500 });
      if (fetched.kind === "not_found") return new Response(null, { status: 200 });
      if (fetched.value.purchaseStateContext?.purchaseState !== "CANCELLED") {
        return new Response(null, { status: 200 });
      }
      const ok = await revoke(note.purchaseToken, fetched.value.orderId ?? null, "cancelled");
      return new Response(null, { status: ok ? 200 : 500 });
    }
  }
});
