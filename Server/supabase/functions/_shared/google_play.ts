// google_play.ts - the three calls the receipt functions make to Google, and nothing else.
//
// Authentication is a Google Cloud service account with the Play Console permission "View financial
// data" (to read purchases) and "Manage orders and subscriptions" (to acknowledge). Its JSON key is
// the GOOGLE_SERVICE_ACCOUNT_JSON secret of the Edge Function; it is never in the repository and
// never in the game. Docs/DESIGN-RECEIPT-VALIDATION.md, "Setting it up", lists the steps.

import { importPKCS8, SignJWT } from "npm:jose@5";
import type { ProductPurchaseV2 } from "./purchase_logic.ts";

const TOKEN_URL = "https://oauth2.googleapis.com/token";
const SCOPE = "https://www.googleapis.com/auth/androidpublisher";
const API = "https://androidpublisher.googleapis.com/androidpublisher/v3/applications/";

interface ServiceAccount {
  client_email: string;
  private_key: string;
}

// One access token per function instance, reused until a minute before it expires. Google's tokens
// last an hour; an Edge Function instance lives for minutes, so most calls reuse one.
let cached: { token: string; expiresAt: number } | null = null;

export async function googleAccessToken(serviceAccountJson: string): Promise<string> {
  const now = Math.floor(Date.now() / 1000);
  if (cached && cached.expiresAt - 60 > now) return cached.token;

  const account = JSON.parse(serviceAccountJson) as ServiceAccount;
  if (!account.client_email || !account.private_key) {
    throw new Error("GOOGLE_SERVICE_ACCOUNT_JSON is missing client_email or private_key");
  }
  const key = await importPKCS8(account.private_key, "RS256");
  const assertion = await new SignJWT({ scope: SCOPE })
    .setProtectedHeader({ alg: "RS256", typ: "JWT" })
    .setIssuer(account.client_email)
    .setAudience(TOKEN_URL)
    .setIssuedAt(now)
    .setExpirationTime(now + 3600)
    .sign(key);

  const response = await fetch(TOKEN_URL, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({
      grant_type: "urn:ietf:params:oauth:grant-type:jwt-bearer",
      assertion,
    }),
  });
  if (!response.ok) {
    // The status only. Google's error body can echo parts of the request.
    throw new Error("Google token exchange failed with HTTP " + response.status);
  }
  const body = await response.json() as { access_token?: string; expires_in?: number };
  if (!body.access_token) throw new Error("Google token exchange returned no access_token");
  cached = { token: body.access_token, expiresAt: now + (body.expires_in ?? 3600) };
  return body.access_token;
}

export type GoogleResult<T> =
  | { kind: "ok"; value: T }
  | { kind: "not_found" } // the token is not a purchase of this app (forged, or another app's)
  | { kind: "error"; status: number }; // Google or the network failed; the client should retry

// purchases.productsv2.getproductpurchasev2: the state of a one-time product purchase by token.
export async function getProductPurchaseV2(
  packageName: string,
  purchaseToken: string,
  accessToken: string,
): Promise<GoogleResult<ProductPurchaseV2>> {
  const url = API + encodeURIComponent(packageName) + "/purchases/productsv2/tokens/" +
    encodeURIComponent(purchaseToken);
  let response: Response;
  try {
    response = await fetch(url, { headers: { Authorization: "Bearer " + accessToken, Accept: "application/json" } });
  } catch {
    return { kind: "error", status: 0 };
  }
  if (response.status === 404 || response.status === 410 || response.status === 400) {
    await response.body?.cancel();
    return { kind: "not_found" };
  }
  if (!response.ok) {
    await response.body?.cancel();
    return { kind: "error", status: response.status };
  }
  return { kind: "ok", value: await response.json() as ProductPurchaseV2 };
}

// purchases.products.acknowledge. Required within three days for a purchase that is not consumed,
// or Google refunds it. Used for the non-consumable offers (store_products.kind); consumables are
// consumed by the client through Unity IAP, which acknowledges them too.
export async function acknowledgeProduct(
  packageName: string,
  productId: string,
  purchaseToken: string,
  accessToken: string,
): Promise<boolean> {
  const url = API + encodeURIComponent(packageName) + "/purchases/products/" + encodeURIComponent(productId) +
    "/tokens/" + encodeURIComponent(purchaseToken) + ":acknowledge";
  try {
    const response = await fetch(url, {
      method: "POST",
      headers: { Authorization: "Bearer " + accessToken, "Content-Type": "application/json" },
      body: "{}",
    });
    await response.body?.cancel();
    return response.ok;
  } catch {
    return false;
  }
}
