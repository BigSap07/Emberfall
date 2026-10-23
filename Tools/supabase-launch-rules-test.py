#!/usr/bin/env python3
"""Proves the launch migrations' rules (0016 to 0019) against a real Supabase project.

A sibling of Tools/supabase-rules-test.py and Tools/supabase-guild-rules-test.py, for the four files
written for the Play Store: account deletion (0016), guild chat report, block and mute (0017), the
server clock (0018) and the purchase receipt ledger (0019). It shares their transport, their key
guard and their run record, and asks the same kind of question: does the database hold the rule when
the client is hostile, missing or replaced? Every request is made straight at PostgREST, never
through the game.

    Apply Server/supabase/migrations/0001 to 0019 first, in order. Every section here asserts a file
    from 0016 on, so against a project without them it goes red, which is the point.

    python Tools/supabase-launch-rules-test.py

CREDENTIALS. Assets/Resources/Config/supabase.json, the same gitignored file the game reads, with
SUPABASE_URL and SUPABASE_ANON_KEY each overriding its own field when set. load_credentials and
record_run are the same as in the two sibling scripts. THE KEY IS NEVER PRINTED; the project url is.
The finished run is recorded in Tools/SupabaseRuns/launch.json (gitignored).

Only the ANON PUBLIC key is used. The service role key bypasses RLS, so every refusal here would
pass for the wrong reason; the guard below refuses to run with one. The receipt WRITERS in 0019 are
granted to service_role alone, so this script can only prove that a client cannot reach them. The
writers' own logic is proved by the Edge Function's tests (Server/supabase/functions) and by the
local database check described in Docs/DESIGN-RECEIPT-VALIDATION.md.

WHAT IT LEAVES BEHIND: nothing, when 0016 is applied. Every throwaway user it creates is tagged
`lt-<run>-<who>` in profiles.display_name and deleted at the end through delete_my_account, which is
itself one of the things under test. Any user that could not be deleted that way is printed with a
single SQL statement to paste into the SQL editor, as the sibling scripts do.

WHAT IT COSTS: six anonymous sign-ins. Supabase's default is 30 per hour per IP.

Standard library only, so it runs on a machine that has nothing set up.
"""

import json
import os
import random
import string
import sys
import time
import urllib.error
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG_PATH = os.path.join(ROOT, "Assets", "Resources", "Config", "supabase.json")
RUN_RECORD = os.path.join(ROOT, "Tools", "SupabaseRuns", "launch.json")

URL = ""
ANON = ""

RUN_TAG = "".join(random.choice("23456789abcdefghjkmnpqrstvwxyz") for _ in range(5))

PASSES = 0
FAILURES = 0

# Every user this run created, so the end of the run can delete exactly those.
CREATED = []


# ---------------------------------------------------------------- the key guard (as the siblings)


def looks_secret(key):
    """True for a key that bypasses RLS. See Tools/supabase-rules-test.py, Docs/CLOUD-GAPS.md P12."""
    return key_kind(key) != "anon"


def key_kind(key):
    """"anon", "secret" or "unknown", read from the key itself. Only "anon" may run."""
    import base64
    key = (key or "").strip()
    if key.startswith("sb_secret_"):
        return "secret"
    if key.startswith("sb_publishable_"):
        return "anon"
    parts = key.split(".")
    if len(parts) != 3:
        return "unknown"
    try:
        segment = parts[1] + "=" * (-len(parts[1]) % 4)
        payload = json.loads(base64.urlsafe_b64decode(segment.encode("ascii")).decode("utf-8"))
    except (ValueError, UnicodeDecodeError):
        return "secret"
    return "anon" if isinstance(payload, dict) and payload.get("role") == "anon" else "secret"


def guard_fires():
    """The guard, proven on every run before it is trusted, exactly as the siblings prove it."""
    import base64

    def jwt(role):
        def b64(text):
            return base64.urlsafe_b64encode(text.encode("utf-8")).decode("ascii").rstrip("=")
        return (b64('{"alg":"HS256","typ":"JWT"}') + "." + b64('{"iss":"supabase","role":"' + role + '"}')
                + "." + b64("signature"))

    cases = ((jwt("service_role"), "secret"), (jwt("authenticated"), "secret"), (jwt("anon"), "anon"),
             ("sb_secret_" + "x" * 32, "secret"), ("sb_publishable_" + "x" * 32, "anon"),
             ("a.!!!notbase64!!!.c", "secret"), ("plain-text-key", "unknown"), ("", "unknown"))
    wrong = [(expected, key_kind(key)) for key, expected in cases if key_kind(key) != expected]
    wrong += [("no literal role text", "found")] if "service_role" in jwt("service_role") else []
    return wrong


# ---------------------------------------------------------------- reporting


def check(condition, label, detail=""):
    global PASSES, FAILURES
    if condition:
        PASSES += 1
        print("  ok    " + label)
    else:
        FAILURES += 1
        print("  FAIL  " + label + (("  <- " + str(detail)) if detail else ""))


def section(title):
    print("")
    print(title)


def refused(status):
    """A refusal is any status that is not a success. 401, 403, 404 and 400 are all refusals here;
    which one PostgREST picks for a missing privilege depends on the role and its version."""
    return status not in (200, 201, 204)


# ---------------------------------------------------------------- credentials and the run record
#
# load_credentials and record_run are the same in Tools/supabase-rules-test.py and
# Tools/supabase-guild-rules-test.py. Keep them so.


def load_credentials():
    """supabase.json first, then SUPABASE_URL and SUPABASE_ANON_KEY, each overriding its own field.
    Returns False with the reason printed when either value is still missing. Never prints the key."""
    global URL, ANON
    url, anon, sources = "", "", {}
    if os.path.exists(CONFIG_PATH):
        try:
            with open(CONFIG_PATH, "r", encoding="utf-8-sig") as handle:
                config = json.load(handle)
        except (OSError, ValueError) as err:
            print("note: Assets/Resources/Config/supabase.json could not be read (" + type(err).__name__ + ")")
            config = None
        if isinstance(config, dict):
            url = (config.get("url") or "").strip()
            anon = (config.get("anonKey") or "").strip()
            if url:
                sources["url"] = "supabase.json"
            if anon:
                sources["key"] = "supabase.json"
    if os.environ.get("SUPABASE_URL", "").strip():
        url, sources["url"] = os.environ["SUPABASE_URL"].strip(), "SUPABASE_URL"
    if os.environ.get("SUPABASE_ANON_KEY", "").strip():
        anon, sources["key"] = os.environ["SUPABASE_ANON_KEY"].strip(), "SUPABASE_ANON_KEY"
    if not url or not anon:
        print("FATAL: no " + ("project url" if not url else "anon key") + ". Put url and anonKey in")
        print("Assets/Resources/Config/supabase.json (copy supabase.template.json), or set")
        print("SUPABASE_URL / SUPABASE_ANON_KEY, which override the file field by field.")
        return False
    URL, ANON = url.rstrip("/"), anon
    print("credentials: url from " + sources["url"] + ", key from " + sources["key"] + " (the key is never printed)")
    return True


def record_run(passes, failures):
    """What Tools/status.ps1 can read to say whether this test ran at the commit it describes."""
    import datetime
    import subprocess
    try:
        commit = subprocess.run(["git", "-C", ROOT, "rev-parse", "HEAD"], capture_output=True,
                                text=True, timeout=30).stdout.strip()
    except (OSError, subprocess.SubprocessError):
        commit = ""
    record = {"utc": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
              "commit": commit, "passes": passes, "failures": failures}
    try:
        os.makedirs(os.path.dirname(RUN_RECORD), exist_ok=True)
        with open(RUN_RECORD, "w", encoding="utf-8") as handle:
            json.dump(record, handle)
    except OSError as err:
        print("note: could not write " + RUN_RECORD + " (" + type(err).__name__ + ")")


# ---------------------------------------------------------------- transport


def request(method, path, token=None, body=None, prefer=None):
    """(status, parsed-or-text). Never raises on an HTTP status: a refusal IS the result here."""
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(URL + path, data=data, method=method)
    req.add_header("apikey", ANON)
    req.add_header("Authorization", "Bearer " + (token or ANON))
    req.add_header("Accept", "application/json")
    if data is not None:
        req.add_header("Content-Type", "application/json")
    if prefer:
        req.add_header("Prefer", prefer)
    try:
        with urllib.request.urlopen(req, timeout=30) as response:
            raw = response.read().decode("utf-8")
            status = response.status
    except urllib.error.HTTPError as err:
        raw = err.read().decode("utf-8")
        status = err.code
    except urllib.error.URLError as err:
        return 0, str(err)
    if not raw:
        return status, None
    try:
        return status, json.loads(raw)
    except ValueError:
        return status, raw


def rpc(name, token=None, **args):
    return request("POST", "/rest/v1/rpc/" + name, token=token, body=args)


def message_of(body):
    if isinstance(body, dict):
        return body.get("message") or body.get("msg") or str(body)
    return str(body)


def rows(status, body):
    return body if status == 200 and isinstance(body, list) else None


# ---------------------------------------------------------------- users


def sign_up(who):
    """One tagged anonymous throwaway user, with backoff on the auth rate limit."""
    delay = 2.0
    for _ in range(6):
        status, body = request("POST", "/auth/v1/signup", body={})
        if status in (200, 201) and isinstance(body, dict) and "access_token" in body:
            user = {"name": who, "token": body["access_token"], "id": body["user"]["id"], "deleted": False}
            CREATED.append(user)
            request("PATCH", "/rest/v1/profiles?user_id=eq." + user["id"], token=user["token"],
                    body={"display_name": ("lt-" + RUN_TAG + "-" + who)[:24]})
            return user
        if status == 429:
            time.sleep(delay)
            delay = min(delay * 2, 20.0)
            continue
        break
    print("  FATAL  could not create the throwaway user '" + who + "'. Is 'Allow anonymous sign-ins'")
    print("         on, and is the anonymous sign-in rate limit above 6 per hour?")
    finish_cleanup()
    sys.exit(2)


def membership_of(user):
    status, body = request("GET", "/rest/v1/guild_members?select=guild_id,role&user_id=eq." + user["id"],
                           token=user["token"])
    found = rows(status, body)
    return found[0] if found else None


def join(user, code):
    """Look first, ask second: since 0014 the only order the server accepts."""
    rpc("guild_by_code", token=user["token"], p_code=code)
    return rpc("guild_request_send", token=user["token"], p_code=code)


def post(user, text):
    return rpc("guild_post_message", token=user["token"], p_body=text)


def visible_message_ids(user, guild_id):
    status, body = request("GET", "/rest/v1/guild_messages?select=id,user_id&guild_id=eq." + guild_id
                                  + "&order=id.desc&limit=100", token=user["token"])
    found = rows(status, body)
    return [r["id"] for r in found] if found is not None else None


# ---------------------------------------------------------------- 0018 the server clock


def check_server_time(a):
    section("SERVER TIME (0018) - the server's clock, and whether it trusts the device's")

    status, body = rpc("server_time", token=a["token"])
    ok = status == 200 and isinstance(body, dict) and isinstance(body.get("unix"), int)
    check(ok, "a signed-in player can read server_time", (status, message_of(body)))
    if not ok:
        return
    local = int(time.time())
    print("        measured: this machine is " + str(local - body["unix"]) + " s off the server")
    check(body["unix"] > 1700000000, "it answers a plausible unix time", body)
    check(abs(body["unix_ms"] // 1000 - body["unix"]) <= 1, "unix_ms agrees with unix", body)

    status, body = rpc("clock_check", token=a["token"], p_client_unix=body["unix"])
    check(status == 200 and isinstance(body, dict) and body.get("trusted") is True,
          "a device on the server's time is trusted", (status, body))
    tolerance = body.get("tolerance_seconds", 300) if isinstance(body, dict) else 300

    status, body = rpc("server_time", token=a["token"])
    ahead = body["unix"] + 86400 if isinstance(body, dict) else local + 86400
    status, body = rpc("clock_check", token=a["token"], p_client_unix=ahead)
    check(status == 200 and isinstance(body, dict) and body.get("trusted") is False,
          "a device a day ahead is not trusted", (status, body))
    check(isinstance(body, dict) and abs(body.get("skew_seconds", 0) - 86400) <= 5,
          "and its skew reads about +86400 seconds", body)

    status, body = rpc("server_time", token=a["token"])
    edge = body["unix"] + tolerance - 5 if isinstance(body, dict) else local
    status, body = rpc("clock_check", token=a["token"], p_client_unix=edge)
    check(status == 200 and isinstance(body, dict) and body.get("trusted") is True,
          "a device just inside the tolerance (" + str(tolerance) + " s) is trusted", (status, body))

    status, body = rpc("clock_check", token=a["token"], p_client_unix=-1)
    check(status == 400, "a negative device time is refused (400)", (status, message_of(body)))

    status, body = request("GET", "/rest/v1/clock_rules?select=tolerance_seconds", token=a["token"])
    check(refused(status) or rows(status, body) == [], "a player cannot read clock_rules", (status, body))

    status, body = rpc("server_time")
    check(refused(status), "the anon role cannot call server_time", status)
    status, body = rpc("clock_check", p_client_unix=local)
    check(refused(status), "nor clock_check", status)


# ---------------------------------------------------------------- 0017 chat safety


def check_chat_safety(leader, m1, m2, outsider):
    section("GUILD CHAT SAFETY (0017) - report, block, mute")

    name = ("Safe-" + RUN_TAG + "-" + "".join(random.choice(string.digits) for _ in range(3)))[:20]
    status, guild = rpc("guild_create", token=leader["token"], p_name=name, p_description="launch test")
    made = status == 200 and isinstance(guild, dict) and guild.get("id")
    check(made, "the leader creates a guild", (status, message_of(guild)))
    if not made:
        return None
    request("PATCH", "/rest/v1/guilds?id=eq." + guild["id"], token=leader["token"], body={"is_open": True})
    for user in (m1, m2):
        status, body = join(user, guild["guild_code"])
        check(status == 200 and body == "joined", user["name"] + " joins the open guild", (status, message_of(body)))

    status, msg = post(m1, "a line to report")
    posted = status == 200 and isinstance(msg, dict) and msg.get("id")
    check(posted, "M1 posts a message", (status, message_of(msg)))
    if not posted:
        return guild
    mid = msg["id"]

    # --- reports
    status, body = rpc("guild_report_message", token=m2["token"], p_message_id=mid, p_reason="spam",
                       p_note="test report")
    check(status == 200 and isinstance(body, dict) and body.get("report_id"),
          "M2 REPORTS M1's message", (status, message_of(body)))
    status, body = rpc("guild_report_message", token=m2["token"], p_message_id=mid, p_reason="spam")
    check(status == 409, "reporting the same message twice is refused (409)", (status, message_of(body)))
    status, body = rpc("guild_report_message", token=m2["token"], p_message_id=mid, p_reason="rude")
    check(status == 400, "a reason outside the list is refused (400)", (status, message_of(body)))
    status, body = rpc("guild_report_message", token=m2["token"], p_message_id=mid, p_reason="other",
                       p_note="x" * 301)
    check(status == 400, "a note over 300 characters is refused (400)", (status, message_of(body)))
    status, body = rpc("guild_report_message", token=m1["token"], p_message_id=mid, p_reason="spam")
    check(status == 400, "M1 cannot report their own message (400)", (status, message_of(body)))
    status, body = rpc("guild_report_message", token=outsider["token"], p_message_id=mid, p_reason="spam")
    check(status == 403, "a player outside the guild cannot report it (403)", (status, message_of(body)))
    status, body = rpc("guild_report_message", token=outsider["token"], p_message_id=2147483000, p_reason="spam")
    check(status == 403, "and a message id that does not exist answers the same 403", (status, message_of(body)))
    status, body = request("GET", "/rest/v1/chat_reports?select=id", token=m2["token"])
    check(refused(status) or rows(status, body) == [], "NO PLAYER CAN READ chat_reports, the reporter included",
          (status, body))
    status, body = rpc("guild_report_message", p_message_id=mid, p_reason="spam")
    check(refused(status), "the anon role cannot report", status)

    # --- blocks
    status, body = rpc("user_block", token=m2["token"], p_user=m1["id"])
    check(status == 200 and body is True, "M2 BLOCKS M1", (status, message_of(body)))
    status, body = rpc("user_block", token=m2["token"], p_user=m1["id"])
    check(status == 200 and body is False, "blocking twice answers false and changes nothing", (status, message_of(body)))
    seen = visible_message_ids(m2, guild["id"])
    check(seen is not None and mid not in seen, "M2 no longer receives M1's message", seen)
    seen = visible_message_ids(leader, guild["id"])
    check(seen is not None and mid in seen, "the leader still does", seen)
    status, body = request("GET", "/rest/v1/user_blocks?select=blocker,blocked", token=m2["token"])
    check(rows(status, body) is not None and len(body) == 1, "M2 can read their own block list", (status, body))
    status, body = request("GET", "/rest/v1/user_blocks?select=blocker,blocked", token=m1["token"])
    check(rows(status, body) == [], "M1 cannot see that M2 blocked them", (status, body))
    status, body = request("GET", "/rest/v1/user_blocks?select=blocker,blocked", token=leader["token"])
    check(rows(status, body) == [], "nor can anybody else read M2's block list", (status, body))
    status, body = request("POST", "/rest/v1/user_blocks", token=m2["token"],
                           body={"blocker": m2["id"], "blocked": leader["id"]}, prefer="return=minimal")
    check(refused(status), "a block cannot be written straight into the table", (status, message_of(body)))
    status, body = request("DELETE", "/rest/v1/user_blocks?blocker=eq." + m2["id"], token=m2["token"])
    check(refused(status), "nor deleted straight from it", (status, message_of(body)))
    status, body = rpc("user_block", token=m2["token"], p_user=m2["id"])
    check(status == 400, "nobody can block themselves (400)", (status, message_of(body)))
    status, body = rpc("user_block", token=m2["token"], p_user="00000000-0000-0000-0000-000000000000")
    check(status == 404, "a player who does not exist cannot be blocked (404)", (status, message_of(body)))
    status, body = rpc("user_block", p_user=m1["id"])
    check(refused(status), "the anon role cannot block", status)
    status, body = rpc("user_unblock", token=m2["token"], p_user=m1["id"])
    check(status == 200 and body is True, "M2 unblocks M1", (status, message_of(body)))
    seen = visible_message_ids(m2, guild["id"])
    check(seen is not None and mid in seen, "and receives the message again", seen)

    # --- mutes
    status, body = rpc("guild_mute_member", token=m1["token"], p_user=m2["id"], p_minutes=5)
    check(status == 403, "a member cannot mute (403)", (status, message_of(body)))
    status, body = rpc("guild_mute_member", token=leader["token"], p_user=leader["id"], p_minutes=5)
    check(status == 400, "the leader cannot mute themselves (400)", (status, message_of(body)))
    status, body = rpc("guild_mute_member", token=leader["token"], p_user=m1["id"], p_minutes=0)
    check(status == 400, "a mute of 0 minutes is refused (400)", (status, message_of(body)))
    status, body = rpc("guild_mute_member", token=leader["token"], p_user=m1["id"], p_minutes=10081)
    check(status == 400, "a mute over seven days is refused (400)", (status, message_of(body)))
    status, body = rpc("guild_mute_member", token=leader["token"], p_user=outsider["id"], p_minutes=5)
    check(status == 404, "a player outside the guild cannot be muted (404)", (status, message_of(body)))
    status, body = rpc("guild_mute_member", token=leader["token"], p_user=m1["id"], p_minutes=5)
    check(status == 200 and isinstance(body, dict) and body.get("muted_until"),
          "THE LEADER MUTES M1 for five minutes", (status, message_of(body)))
    time.sleep(2.2)  # past guild_post_message's two-second cooldown, so only the mute can refuse
    status, body = post(m1, "can I still talk")
    check(status == 403, "M1 CANNOT POST while muted (403)", (status, message_of(body)))
    status, body = request("GET", "/rest/v1/guild_chat_mutes?select=user_id,muted_until", token=m1["token"])
    check(rows(status, body) is not None and len(body) == 1, "M1 can read their own mute", (status, body))
    status, body = request("GET", "/rest/v1/guild_chat_mutes?select=user_id", token=m2["token"])
    check(rows(status, body) == [], "an ordinary member cannot see who is muted", (status, body))
    status, body = request("POST", "/rest/v1/guild_chat_mutes", token=leader["token"],
                           body={"guild_id": guild["id"], "user_id": m2["id"],
                                 "muted_until": "2099-01-01T00:00:00Z"}, prefer="return=minimal")
    check(refused(status), "a mute cannot be written straight into the table", (status, message_of(body)))

    status, body = rpc("guild_set_role", token=leader["token"], p_user=m2["id"], p_role="officer")
    check(status == 200, "the leader makes M2 an officer", (status, message_of(body)))
    status, body = request("GET", "/rest/v1/guild_chat_mutes?select=user_id", token=m2["token"])
    check(rows(status, body) is not None and len(body) == 1, "an officer sees the guild's mutes", (status, body))
    status, body = rpc("guild_mute_member", token=m2["token"], p_user=leader["id"], p_minutes=5)
    check(status == 403, "an officer cannot mute the leader (403)", (status, message_of(body)))
    status, body = rpc("guild_unmute_member", token=m2["token"], p_user=m1["id"])
    check(status == 200 and body is True, "the officer unmutes M1", (status, message_of(body)))
    status, body = post(m1, "thank you")
    check(status == 200, "and M1 can post again", (status, message_of(body)))
    rpc("guild_set_role", token=leader["token"], p_user=m2["id"], p_role="member")
    return guild


# ---------------------------------------------------------------- 0019 receipts, from a client


def check_receipts(a):
    section("PURCHASE RECEIPTS (0019) - a client reads its own and can write none")

    status, body = request("GET", "/rest/v1/purchase_receipts?select=id,offer_id,order_id,revoked_at",
                           token=a["token"])
    check(rows(status, body) == [], "a player can query their own receipts (none yet)", (status, body))
    status, body = request("GET", "/rest/v1/purchase_receipts?select=purchase_token_sha256", token=a["token"])
    check(refused(status), "the token hash column is not readable by a client", (status, message_of(body)))
    status, body = request("GET", "/rest/v1/purchase_receipts?select=google_response", token=a["token"])
    check(refused(status), "nor is the raw Google answer", (status, message_of(body)))
    status, body = request("POST", "/rest/v1/purchase_receipts", token=a["token"],
                           body={"product_id": "x", "offer_id": "starter_pact",
                                 "purchase_token_sha256": "a" * 64}, prefer="return=minimal")
    check(refused(status), "A CLIENT CANNOT INSERT A RECEIPT", (status, message_of(body)))
    status, body = rpc("purchase_receipt_record", token=a["token"], p_user_id=a["id"],
                       p_product_id="placeholder.emberfall.store.starter_pact", p_order_id="GPA.TEST",
                       p_token_sha256="a" * 64, p_quantity=1, p_test_purchase=True, p_region_code="BE",
                       p_purchased_at=None, p_acknowledged=False, p_google_response={})
    check(refused(status), "nor call the recording function the Edge Function uses", (status, message_of(body)))
    status, body = rpc("purchase_receipt_revoke", token=a["token"], p_token_sha256="a" * 64,
                       p_order_id=None, p_reason="x")
    check(refused(status), "nor the revoking one", (status, message_of(body)))
    status, body = rpc("purchase_receipt_mark_acknowledged", token=a["token"], p_token_sha256="a" * 64)
    check(refused(status), "nor the acknowledging one", (status, message_of(body)))
    status, body = request("GET", "/rest/v1/store_products?select=product_id", token=a["token"])
    check(refused(status) or rows(status, body) == [], "a client cannot read the server's product list",
          (status, body))
    status, body = request("GET", "/rest/v1/purchase_receipts?select=id")
    check(refused(status) or rows(status, body) == [], "the anon role reads no receipts", (status, body))


# ---------------------------------------------------------------- 0016 account deletion


def check_account_deletion(leader, m1, m2, friend, solo, outsider, guild):
    section("ACCOUNT DELETION (0016) - one call removes the account and everything keyed to it")

    status, body = rpc("delete_my_account", token=leader["token"], p_confirm="yes")
    check(status == 400, "the call is refused without the confirmation text DELETE (400)",
          (status, message_of(body)))
    status, body = rpc("delete_my_account", p_confirm="DELETE")
    check(refused(status), "the anon role cannot call it", status)
    status, body = rpc("account_delete_internal", token=leader["token"], p_user=m1["id"])
    check(refused(status), "A PLAYER CANNOT CALL THE INTERNAL FUNCTION to delete somebody else",
          (status, message_of(body)))
    status, body = rpc("admin_delete_account", token=leader["token"], p_user=m1["id"])
    check(refused(status), "nor the operator's door", (status, message_of(body)))
    check(membership_of(m1) is not None, "and M1 is still there afterwards")

    # Give the leader something in every table the brief names.
    rpc("push_save", token=leader["token"], p_expected_revision=0, p_save_version=1, p_device_id="lt",
        p_data={"version": 1, "launchTest": RUN_TAG})
    request("POST", "/rest/v1/team_snapshots", token=leader["token"],
            body={"user_id": leader["id"], "label": "lt", "units": []}, prefer="return=minimal")
    request("POST", "/rest/v1/friendships", token=leader["token"],
            body={"requester": leader["id"], "addressee": friend["id"]}, prefer="return=minimal")
    request("PATCH", "/rest/v1/friendships?requester=eq." + leader["id"], token=friend["token"],
            body={"status": "accepted"})
    status, body = request("GET", "/rest/v1/team_snapshots?select=user_id&user_id=eq." + leader["id"],
                           token=friend["token"])
    check(rows(status, body) is not None and len(body) == 1, "setup: the friend can read the leader's snapshot",
          (status, body))
    if guild:
        rpc("guild_set_role", token=leader["token"], p_user=m2["id"], p_role="officer")

    status, body = rpc("delete_my_account", token=leader["token"], p_confirm="DELETE")
    deleted = status == 200 and isinstance(body, dict) and body.get("deleted") is True
    check(deleted, "THE LEADER DELETES THEIR ACCOUNT", (status, message_of(body)))
    if deleted:
        leader["deleted"] = True
    check(isinstance(body, dict) and body.get("guild_disbanded") is False,
          "a guild with other members is not disbanded", body)

    status, body = rpc("delete_my_account", token=leader["token"], p_confirm="DELETE")
    check(status == 200 and isinstance(body, dict) and body.get("deleted") is False,
          "a second call with the old token finds nothing and does not fail", (status, message_of(body)))
    status, body = request("GET", "/auth/v1/user", token=leader["token"])
    check(status >= 400, "the auth user is gone (the old token no longer resolves a user)", status)
    status, body = request("GET", "/rest/v1/profiles?select=user_id&user_id=eq." + leader["id"],
                           token=friend["token"])
    check(rows(status, body) == [], "the profile is gone", (status, body))
    status, body = request("GET", "/rest/v1/saves?select=user_id", token=leader["token"])
    check(rows(status, body) == [] or refused(status), "the save is gone", (status, body))
    status, body = request("GET", "/rest/v1/team_snapshots?select=user_id&user_id=eq." + leader["id"],
                           token=friend["token"])
    check(rows(status, body) == [], "the snapshot is gone", (status, body))
    status, body = request("GET", "/rest/v1/friendships?select=id", token=friend["token"])
    check(rows(status, body) == [], "the friendship is gone from the friend's list", (status, body))
    if guild:
        mine = membership_of(m2)
        check(mine is not None and mine.get("role") == "leader", "THE OFFICER SUCCEEDED AS LEADER", mine)
        status, body = request("GET", "/rest/v1/guild_members?select=user_id&guild_id=eq." + guild["id"],
                               token=m1["token"])
        ids = [r["user_id"] for r in (rows(status, body) or [])]
        check(leader["id"] not in ids and len(ids) == 2, "the roster no longer lists the deleted player", ids)

    status, alone = rpc("guild_create", token=solo["token"],
                        p_name=("Solo-" + RUN_TAG + "-" + str(random.randint(100, 999)))[:20], p_description="")
    if status == 200 and isinstance(alone, dict):
        status, body = rpc("delete_my_account", token=solo["token"], p_confirm="DELETE")
        check(status == 200 and isinstance(body, dict) and body.get("guild_disbanded") is True,
              "A LEADER WHO IS THE LAST MEMBER takes the guild with them", (status, message_of(body)))
        if status == 200:
            solo["deleted"] = True
        status, body = rpc("guild_by_code", token=outsider["token"], p_code=alone["guild_code"])
        check(rows(status, body) == [], "and the guild's code no longer finds anything", (status, body))
    else:
        check(False, "setup: the solo player creates a guild", (status, message_of(alone)))


# ---------------------------------------------------------------- cleanup


def finish_cleanup():
    """Deletes every user this run made through delete_my_account, and prints SQL for any that
    could not be deleted that way (a project without 0016, or a failed call)."""
    left = []
    for user in CREATED:
        if user["deleted"]:
            continue
        status, body = rpc("delete_my_account", token=user["token"], p_confirm="DELETE")
        if status == 200 and isinstance(body, dict):
            user["deleted"] = True
        else:
            left.append(user)
    print("")
    if not left:
        print("Cleanup: every throwaway user this run made was deleted through delete_my_account.")
        return
    print("Cleanup: " + str(len(left)) + " user(s) could not delete themselves. Paste this into the SQL editor:")
    print("delete from auth.users where id in (")
    for index, user in enumerate(left):
        print("  '" + user["id"] + "'" + ("," if index < len(left) - 1 else ""))
    print(");")
    print("-- tagged lt-" + RUN_TAG + "-* in profiles.display_name")


# ---------------------------------------------------------------- entry point


def main():
    if not load_credentials():
        return 2
    broken = guard_fires()
    if broken:
        print("The key guard does not work (expected, got): " + str(broken) + ". Not running.")
        return 2
    if looks_secret(ANON):
        print("That key is not an anon public key (it reads as: " + key_kind(ANON) + "). Use the ANON")
        print("PUBLIC key: the service role bypasses RLS, so every refusal here would mean nothing.")
        return 2
    print("key: decoded as an anon public key, and the guard was shown to fire on built secret keys")
    print("EMBERFALL SUPABASE LAUNCH RULE TEST (0016 to 0019)")
    print("project: " + URL)
    print("run tag: lt-" + RUN_TAG)

    section("SETUP")
    leader, m1, m2 = sign_up("L"), sign_up("M1"), sign_up("M2")
    outsider, friend, solo = sign_up("O"), sign_up("F"), sign_up("Z")
    check(len({u["id"] for u in CREATED}) == 6, "six distinct anonymous users were created")

    try:
        check_server_time(leader)
        guild = check_chat_safety(leader, m1, m2, outsider)
        check_receipts(leader)
        check_account_deletion(leader, m1, m2, friend, solo, outsider, guild)
    finally:
        finish_cleanup()

    print("")
    print("RESULT: " + str(PASSES) + " ok, " + str(FAILURES) + " FAILED")
    record_run(PASSES, FAILURES)
    return 1 if FAILURES else 0


if __name__ == "__main__":
    sys.exit(main())
