#!/usr/bin/env python3
"""Proves the GUILD half of the database's rules against a real Supabase project.

A sibling of Tools/supabase-rules-test.py rather than more of it. That file is about one
player's own data - a profile, a save, a friendship - and it is already long. This one is about
a rule that exists BETWEEN people: who may read a roster, who may accept somebody, who may
remove them, and what happens when thirty devices ask the same question in the same instant.
The two share nothing but the transport, and merging them would have produced a file where the
interesting section is the last fifth.

    Run Server/supabase/migrations/0004, 0005, 0006 and 0007 first, in order.

    $env:SUPABASE_URL      = 'https://<ref>.supabase.co'
    $env:SUPABASE_ANON_KEY = '<anon public key>'
    python Tools/supabase-guild-rules-test.py

    python Tools/supabase-guild-rules-test.py --racers 30   # the cap race, default 30
    python Tools/supabase-guild-rules-test.py --no-race     # everything except the cap race

CREDENTIALS. Environment first, then Assets/Resources/Config/supabase.json - the same gitignored
file the game reads, so a machine that can run the game can run this. NEITHER VALUE IS EVER
PRINTED. Only the ANON PUBLIC key is used: every request here is either unauthenticated or made
as a user this script created. The service role key bypasses RLS, so every check would pass and
none of them would mean anything; it appears nowhere in this project and must not be set.

EVERY USER THIS CREATES IS TAGGED. Each throwaway account's display_name is set to
`gt-<run>-<who>`, where <run> is a tag minted per run and printed at the top, so a user left
behind by a crashed run is identifiable in the dashboard by sight. At the end the script
disbands every guild it created and prints ONE SQL statement that deletes exactly the users it
made, by id. Deleting a user needs the service role, which this script deliberately does not
hold - hence the paste rather than a call.

WHAT IT COSTS. The cap race needs a guild to actually reach thirty, so the default run creates
about forty throwaway accounts. Supabase's default auth rate limits are lower than that (30
anonymous sign-ins per hour per IP, 30 sign-ups per 5 minutes per IP), so the first run on a
project that has not had them raised will stall in SETUP and say exactly which dashboard setting
to raise. --no-race runs everything else with six accounts.

Standard library only - no pip install, so it runs on a machine that has nothing set up.
"""

import argparse
import json
import os
import random
import string
import sys
import threading
import time
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG_PATH = os.path.join(ROOT, "Assets", "Resources", "Config", "supabase.json")

URL = ""
ANON = ""

RUN_TAG = "".join(random.choice("23456789abcdefghjkmnpqrstvwxyz") for _ in range(5))

PASSES = 0
FAILURES = 0

# Every user id this run created, in creation order. The one thing the cleanup statement is
# built from, and the reason it can be by id rather than by pattern.
CREATED = []
CREATED_LOCK = threading.Lock()


# ---------------------------------------------------------------- reporting


def check(condition, label, detail=""):
    global PASSES, FAILURES
    if condition:
        PASSES += 1
        print("  ok    " + label)
    else:
        FAILURES += 1
        print("  FAIL  " + label + (("  <- " + str(detail)) if detail else ""))


def note(text):
    print("        " + text)


def section(title):
    print("")
    print(title)


# ---------------------------------------------------------------- transport


def load_credentials():
    global URL, ANON
    URL = os.environ.get("SUPABASE_URL", "").rstrip("/")
    ANON = os.environ.get("SUPABASE_ANON_KEY", "")
    if URL and ANON:
        print("credentials: from SUPABASE_URL / SUPABASE_ANON_KEY")
        return
    if not os.path.exists(CONFIG_PATH):
        print("FATAL: no SUPABASE_URL/SUPABASE_ANON_KEY and no Assets/Resources/Config/supabase.json")
        sys.exit(2)
    with open(CONFIG_PATH, "r", encoding="utf-8") as handle:
        config = json.load(handle)
    URL = (config.get("url") or "").rstrip("/")
    ANON = config.get("anonKey") or ""
    if not URL or not ANON:
        print("FATAL: Assets/Resources/Config/supabase.json is missing url or anonKey")
        sys.exit(2)
    print("credentials: from Assets/Resources/Config/supabase.json (not printed)")


def request(method, path, token=None, body=None, prefer=None, timeout=30):
    """(status, parsed-or-text). Never raises on an HTTP status - a refusal IS the result in
    most of these cases, and a 403 is as much an answer as a 200."""
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
        with urllib.request.urlopen(req, timeout=timeout) as response:
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
    """The sentence Postgres raised, or the body as it came. Every refusal below is checked on
    its STATUS; this is only ever used to print what happened."""
    if isinstance(body, dict):
        return body.get("message") or body.get("error_description") or body.get("msg") or str(body)
    return str(body)


# ---------------------------------------------------------------- users


def sign_up(who, anonymous=True, attempts=6, max_delay=20.0):
    """One tagged throwaway user, with backoff on the auth rate limit.

    A 429 here is not a bug in anything - it is Supabase's own default, and the defaults are
    lower than what the cap race needs: 30 anonymous sign-ins per HOUR per IP, against 30
    sign-ups per FIVE MINUTES for one carrying an email. Backing off can outwait the second and
    cannot outwait the first.

    WHICH IS WHY THE RACERS CARRY AN EMAIL AND THE REST OF THE CAST DOES NOT. The sections above
    the race are about the anonymous-first design and want anonymous users to be about; the race
    is about thirty distinct authenticated callers arriving at one guild in one instant, and
    which door they came through changes nothing a policy can see - an anonymous sign-in produces
    a real auth.users row holding the `authenticated` role, which 0001 records as the reason none
    of these policies has an anonymous case. The address is at example.com, which RFC 2606
    reserves and which cannot receive mail: the point is that the user EXISTS, not that a mailbox
    does. Pass --anonymous-racers on a project whose limits have been raised to make the race
    anonymous as well."""
    delay = 2.0
    for attempt in range(attempts):
        if anonymous:
            payload = {}
        else:
            slug = "".join(random.choice(string.ascii_lowercase + string.digits) for _ in range(10))
            payload = {"email": "emberfall-gt-" + RUN_TAG + "-" + slug + "@example.com",
                       "password": "Emberfall-" + slug + "-Aa1"}
        status, body = request("POST", "/auth/v1/signup", body=payload)
        if status in (200, 201) and isinstance(body, dict) and "access_token" in body:
            user = {
                "name": who,
                "token": body["access_token"],
                "refresh": body.get("refresh_token", ""),
                "id": body["user"]["id"],
            }
            with CREATED_LOCK:
                CREATED.append(user["id"])
            # The tag. 24 characters is the display_name ceiling, and this fits inside it.
            request("PATCH", "/rest/v1/profiles?user_id=eq." + user["id"],
                    token=user["token"], body={"display_name": ("gt-" + RUN_TAG + "-" + who)[:24]})
            return user
        if status == 429:
            time.sleep(delay)
            delay = min(delay * 2, max_delay)
            continue
        return None
    return None


def must_sign_up(who, anonymous=True, attempts=6, max_delay=20.0):
    user = sign_up(who, anonymous=anonymous, attempts=attempts, max_delay=max_delay)
    if user is None:
        print("")
        print("  FATAL  could not create the throwaway user '" + who + "'.")
        print("         If this was a 429: Supabase's defaults are 30 anonymous sign-ins per hour")
        print("         per IP and 30 sign-ups per 5 minutes per IP, and this test needs more.")
        print("         Dashboard -> Authentication -> Rate Limits, then re-run.")
        print("         If it was not: is 'Allow anonymous sign-ins' on under Sign In / Providers?")
        # No cleanup print here - main's finally does it, for this exit as much as for a clean
        # one, and two copies of a paste-this statement is one copy too many.
        sys.exit(2)
    return user


def profile_of(user):
    status, body = request("GET", "/rest/v1/profiles?select=user_id,display_name,friend_code"
                                  "&user_id=eq." + user["id"], token=user["token"])
    return body[0] if status == 200 and isinstance(body, list) and body else None


def roster_size(guild_id, token):
    status, body = request("GET", "/rest/v1/guild_members?select=user_id&guild_id=eq." + guild_id,
                           token=token)
    return len(body) if status == 200 and isinstance(body, list) else -1


def my_membership(user):
    status, body = request("GET", "/rest/v1/guild_members?select=guild_id,role,last_seen"
                                  "&user_id=eq." + user["id"], token=user["token"])
    return body[0] if status == 200 and isinstance(body, list) and body else None


def guild_name(prefix):
    return (prefix + "-" + RUN_TAG + "-" + "".join(random.choice(string.digits) for _ in range(3)))[:20]


# ---------------------------------------------------------------- the cases


def check_creation(a, b):
    section("CREATING A GUILD - the creator is the leader, and a player has at most one guild")

    name = guild_name("Ember")
    status, body = rpc("guild_create", token=a["token"], p_name=name, p_description="a test guild")
    made = isinstance(body, dict) and body.get("id")
    check(status == 200 and made, "A creates a guild", (status, message_of(body)))
    if not made:
        return None

    guild = body
    alphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ"
    code = guild.get("guild_code", "")
    check(len(code) == 6 and all(c in alphabet for c in code),
          "its code is six characters from the friend-code alphabet (" + code + ")")
    check(guild.get("name") == name, "and the name is the one that was sent", guild.get("name"))

    membership = my_membership(a)
    check(membership is not None and membership.get("role") == "leader",
          "A IS THE LEADER of it, written in the same call that made the guild", membership)

    status, body = rpc("guild_create", token=a["token"], p_name=guild_name("Second"),
                       p_description="")
    check(status == 409, "A CANNOT CREATE A SECOND GUILD while in one", (status, message_of(body)))

    # Case-insensitive, because two guilds whose names differ only in case are one guild to
    # anybody reading a roster.
    status, body = rpc("guild_create", token=b["token"], p_name=name.upper(), p_description="")
    check(status == 409, "B cannot take the same name in different case", (status, message_of(body)))

    for bad, why in ((" ab", "too short"), ("x" * 21, "too long"), ("ok\nname", "with a newline")):
        status, body = rpc("guild_create", token=b["token"], p_name=bad, p_description="")
        check(status == 400, "a name " + why + " is refused", (status, message_of(body)))

    status, body = rpc("guild_create", token=b["token"], p_name=guild_name("Blurb"),
                       p_description="x" * 141)
    check(status == 400, "and a description over 140 characters is refused",
          (status, message_of(body)))

    return guild


def check_lookup_and_privacy(guild, a, b, c):
    section("FINDING A GUILD - one exact code in, and no way to page the table")

    status, body = rpc("guild_by_code", token=b["token"], p_code=guild["guild_code"])
    found = status == 200 and isinstance(body, list) and len(body) == 1
    check(found, "B resolves the code to the guild without being in it", (status, body))
    if found:
        check(body[0]["id"] == guild["id"] and body[0]["member_count"] == 1,
              "and gets its name, whether it is open, and how full it is", body[0])

    # The code as a player would type it off a screen: grouped, lower case.
    typed = (guild["guild_code"][:3] + "-" + guild["guild_code"][3:]).lower()
    status, body = rpc("guild_by_code", token=b["token"], p_code=typed)
    check(status == 200 and isinstance(body, list) and len(body) == 1,
          "a code typed as '" + typed + "' resolves to the same guild - the dash and the case are repaired")

    status, body = rpc("guild_by_code", token=b["token"], p_code="ZZZZZZ")
    check(status == 200 and isinstance(body, list) and len(body) == 0,
          "a code nobody holds resolves to nothing rather than to an error", (status, body))

    status, body = rpc("guild_by_code", token=b["token"], p_code="ABC0I1")
    check(status == 200 and isinstance(body, list) and len(body) == 0,
          "and so does a code carrying letters the alphabet does not have", (status, body))

    # The whole point of the lookup being a function. RLS filters rather than refuses, so the
    # right shape here is 200 with zero rows.
    status, body = request("GET", "/rest/v1/guilds?select=id,name,guild_code", token=c["token"])
    check(status == 200 and isinstance(body, list) and len(body) == 0,
          "C CANNOT PAGE THE GUILD TABLE - a list of codes would be a list of open guilds to walk into",
          (status, body))

    status, body = request("GET", "/rest/v1/guilds?select=id,name&id=eq." + guild["id"],
                           token=c["token"])
    check(status == 200 and isinstance(body, list) and len(body) == 0,
          "and cannot read the one guild by id either", (status, body))

    status, body = request("GET", "/rest/v1/guilds?select=id,name,guild_code,is_open&id=eq." + guild["id"],
                           token=a["token"])
    check(status == 200 and isinstance(body, list) and len(body) == 1,
          "while A, who is in it, reads it normally", (status, body))


def check_requests(guild, a, b, c):
    section("REQUESTS - who may queue, who may answer, and who may not see one at all")

    status, body = rpc("guild_request_send", token=b["token"], p_code=guild["guild_code"])
    check(status == 200 and body == "requested",
          "B asks to join a closed guild and is queued, not admitted", (status, body))

    status, body = rpc("guild_request_send", token=b["token"], p_code=guild["guild_code"])
    check(status == 200 and body == "requested",
          "asking twice is the same answer, not a duplicate-key failure", (status, body))

    status, body = request("GET", "/rest/v1/guild_requests?select=id,guild_id,user_id",
                           token=a["token"])
    pending = body[0] if status == 200 and isinstance(body, list) and body else None
    check(pending is not None and pending["user_id"] == b["id"],
          "A, the leader, sees the request", (status, body))

    status, body = request("GET", "/rest/v1/guild_requests?select=id,user_id", token=c["token"])
    check(status == 200 and isinstance(body, list) and len(body) == 0,
          "C, who is in no guild and asked for nothing, sees no request at all", (status, body))

    status, body = request("GET", "/rest/v1/guild_members?select=user_id,role&guild_id=eq." + guild["id"],
                           token=b["token"])
    check(status == 200 and isinstance(body, list) and len(body) == 0,
          "AND A PENDING APPLICANT CANNOT READ THE ROSTER - queuing buys nothing", (status, body))

    if pending is None:
        return None

    # No INSERT grant on guild_members at all, so this is refused on privilege before RLS is
    # consulted - the same shape `saves` has.
    status, body = request("POST", "/rest/v1/guild_members", token=b["token"],
                           body={"user_id": b["id"], "guild_id": guild["id"], "role": "leader"})
    check(status not in (200, 201, 204),
          "B CANNOT INSERT ITSELF INTO THE ROSTER - no client role holds INSERT on guild_members",
          (status, message_of(body)))

    status, body = rpc("guild_request_accept", token=c["token"], p_request_id=pending["id"])
    check(status == 403, "C, a stranger, cannot accept a request to somebody else's guild",
          (status, message_of(body)))

    status, body = rpc("guild_request_accept", token=a["token"], p_request_id=pending["id"])
    accepted = status == 200 and isinstance(body, dict) and body.get("user_id") == b["id"]
    check(accepted, "A, the leader, accepts", (status, message_of(body)))
    check(accepted and body.get("role") == "member", "and B joins as a member, not as anything else",
          body)

    status, body = request("GET", "/rest/v1/guild_requests?select=id", token=a["token"])
    check(status == 200 and isinstance(body, list) and len(body) == 0,
          "the request is gone - accepting turns it into a membership rather than marking it",
          (status, body))

    status, body = request("GET", "/rest/v1/guild_members?select=user_id,role&guild_id=eq." + guild["id"],
                           token=b["token"])
    check(status == 200 and isinstance(body, list) and len(body) == 2,
          "and B can now read the whole roster", (status, body))

    return b


def check_roles(guild, a, b, c):
    section("ROLES - only the leader moves one, and 'leader' is not a role you can be given")

    status, body = rpc("guild_set_role", token=b["token"], p_user=a["id"], p_role="member")
    check(status == 403, "B, a member, CANNOT DEMOTE THE LEADER", (status, message_of(body)))

    status, body = rpc("guild_set_role", token=b["token"], p_user=b["id"], p_role="officer")
    check(status == 403, "and cannot promote itself", (status, message_of(body)))

    status, body = request("PATCH", "/rest/v1/guild_members?user_id=eq." + b["id"],
                           token=b["token"], body={"role": "leader"})
    after = my_membership(b)
    check(status not in (200, 204) and after and after["role"] == "member",
          "NOR CAN IT PATCH ITS OWN ROLE - guild_members carries no UPDATE grant for anybody",
          (status, after))

    status, body = rpc("guild_set_role", token=a["token"], p_user=b["id"], p_role="officer")
    check(status == 200 and isinstance(body, dict) and body.get("role") == "officer",
          "A promotes B to officer", (status, message_of(body)))

    status, body = rpc("guild_set_role", token=a["token"], p_user=b["id"], p_role="leader")
    check(status == 400, "and cannot hand leadership over through the role call - that is a transfer",
          (status, message_of(body)))

    status, body = rpc("guild_set_role", token=a["token"], p_user=c["id"], p_role="officer")
    check(status == 404, "a player who is not in the guild cannot be given a role in it",
          (status, message_of(body)))

    status, body = rpc("guild_set_role", token=a["token"], p_user=a["id"], p_role="member")
    check(status == 400, "and the leader cannot demote themselves out of the job",
          (status, message_of(body)))


def check_officer_powers(guild, a, b, c, d):
    section("WHAT AN OFFICER MAY DO - accept and remove members, and nothing above that")

    status, body = rpc("guild_request_send", token=c["token"], p_code=guild["guild_code"])
    check(status == 200 and body == "requested", "C asks to join", (status, body))
    status, body = request("GET", "/rest/v1/guild_requests?select=id,user_id", token=b["token"])
    check(status == 200 and isinstance(body, list) and len(body) == 1,
          "B, now an officer, SEES the request", (status, body))
    pending = body[0] if status == 200 and isinstance(body, list) and body else None
    if pending is None:
        return

    status, body = rpc("guild_request_accept", token=b["token"], p_request_id=pending["id"])
    check(status == 200, "and accepts it, which is an officer's job", (status, message_of(body)))

    status, body = rpc("guild_request_send", token=d["token"], p_code=guild["guild_code"])
    status, body = request("GET", "/rest/v1/guild_requests?select=id,user_id", token=b["token"])
    spare = body[0] if status == 200 and isinstance(body, list) and body else None
    if spare:
        status, body = request("DELETE", "/rest/v1/guild_requests?id=eq." + spare["id"],
                               token=b["token"])
        check(status in (200, 204), "an officer DECLINES by deleting the row", status)
        status, body = request("GET", "/rest/v1/guild_requests?select=id", token=b["token"])
        check(status == 200 and isinstance(body, list) and len(body) == 0,
              "and the request is gone", (status, body))

    status, body = rpc("guild_set_role", token=b["token"], p_user=c["id"], p_role="officer")
    check(status == 403, "AN OFFICER CANNOT PROMOTE - roles are the leader's alone",
          (status, message_of(body)))

    status, body = request("DELETE", "/rest/v1/guild_members?user_id=eq." + a["id"], token=b["token"])
    still = my_membership(a)
    check(still is not None and still["role"] == "leader",
          "AN OFFICER CANNOT REMOVE THE LEADER", (status, still))

    status, body = request("DELETE", "/rest/v1/guilds?id=eq." + guild["id"], token=b["token"])
    status2, body2 = request("GET", "/rest/v1/guilds?select=id&id=eq." + guild["id"], token=a["token"])
    check(status2 == 200 and isinstance(body2, list) and len(body2) == 1,
          "AND CANNOT DISBAND THE GUILD - it is still there afterwards", (status, body2))

    status, body = request("DELETE", "/rest/v1/guild_members?user_id=eq." + c["id"], token=b["token"])
    gone = my_membership(c)
    check(status in (200, 204) and gone is None,
          "but an officer CAN remove a member", (status, gone))

    # Two officers: the one thing the delete policy deliberately does not permit.
    rpc("guild_request_send", token=c["token"], p_code=guild["guild_code"])
    status, body = request("GET", "/rest/v1/guild_requests?select=id", token=a["token"])
    again = body[0] if status == 200 and isinstance(body, list) and body else None
    if again:
        rpc("guild_request_accept", token=a["token"], p_request_id=again["id"])
        rpc("guild_set_role", token=a["token"], p_user=c["id"], p_role="officer")
        status, body = request("DELETE", "/rest/v1/guild_members?user_id=eq." + c["id"],
                               token=b["token"])
        survived = my_membership(c)
        check(survived is not None and survived["role"] == "officer",
              "ONE OFFICER CANNOT REMOVE ANOTHER - a peer is not a subordinate", (status, survived))

        status, body = request("DELETE", "/rest/v1/guild_members?user_id=eq." + c["id"],
                               token=a["token"])
        check(status in (200, 204) and my_membership(c) is None,
              "while the leader can remove an officer", status)


def check_leaving(guild, a, b, c):
    section("LEAVING - anyone may, except the one person the guild cannot do without")

    status, body = request("DELETE", "/rest/v1/guild_members?user_id=eq." + a["id"], token=a["token"])
    still = my_membership(a)
    check(still is not None and still["role"] == "leader",
          "THE LEADER CANNOT LEAVE - the row will not move, whatever the client shows",
          (status, still))

    rpc("guild_request_send", token=c["token"], p_code=guild["guild_code"])
    status, body = request("GET", "/rest/v1/guild_requests?select=id", token=a["token"])
    pending = body[0] if status == 200 and isinstance(body, list) and body else None
    if pending:
        rpc("guild_request_accept", token=a["token"], p_request_id=pending["id"])
    status, body = request("DELETE", "/rest/v1/guild_members?user_id=eq." + c["id"], token=c["token"])
    check(status in (200, 204) and my_membership(c) is None, "a member may walk out", status)

    status, body = rpc("guild_transfer_leadership", token=b["token"], p_user=b["id"])
    check(status == 403, "an officer cannot hand leadership to itself", (status, message_of(body)))

    status, body = rpc("guild_transfer_leadership", token=a["token"], p_user=b["id"])
    check(status == 200 and isinstance(body, dict) and body.get("role") == "leader",
          "A HANDS LEADERSHIP TO B", (status, message_of(body)))
    was = my_membership(a)
    check(was is not None and was["role"] == "officer",
          "and A becomes an officer rather than being dropped", was)

    status, body = request("DELETE", "/rest/v1/guild_members?user_id=eq." + a["id"], token=a["token"])
    check(status in (200, 204) and my_membership(a) is None,
          "the former leader can now leave, which is what a transfer is for", status)

    # Put A back as leader so the rest of the run has a guild to work in.
    rpc("guild_request_send", token=a["token"], p_code=guild["guild_code"])
    status, body = request("GET", "/rest/v1/guild_requests?select=id", token=b["token"])
    back = body[0] if status == 200 and isinstance(body, list) and body else None
    if back:
        rpc("guild_request_accept", token=b["token"], p_request_id=back["id"])
    rpc("guild_transfer_leadership", token=b["token"], p_user=a["id"])
    check(my_membership(a) is not None and my_membership(a)["role"] == "leader",
          "(and back again, so the rest of this run has a guild with A at the top)")


def check_chat(guild, a, b, c):
    section("CHAT - members only, one line at a time, and the rate limit is the database's")

    status, body = rpc("guild_post_message", token=a["token"], p_body="  first light  ")
    posted = status == 200 and isinstance(body, dict)
    check(posted, "A posts a message", (status, message_of(body)))
    check(posted and body.get("body") == "first light",
          "and it is stored trimmed - the column refuses anything the screen did not tidy", body)

    status, body = rpc("guild_post_message", token=a["token"], p_body="straight after")
    check(status == 429, "A SECOND MESSAGE INSIDE TWO SECONDS IS REFUSED, with a 429",
          (status, message_of(body)))

    time.sleep(2.5)
    status, body = rpc("guild_post_message", token=a["token"], p_body="and again, later")
    check(status == 200, "and the same message two seconds later is accepted", (status, message_of(body)))

    status, body = rpc("guild_post_message", token=b["token"], p_body="x" * 201)
    check(status == 400, "a message over 200 characters is refused", (status, message_of(body)))
    status, body = rpc("guild_post_message", token=b["token"], p_body="   ")
    check(status == 400, "and so is one that is nothing but spaces", (status, message_of(body)))
    status, body = rpc("guild_post_message", token=b["token"], p_body="two\nlines")
    check(status == 400, "and so is one carrying a newline", (status, message_of(body)))

    status, body = rpc("guild_post_message", token=c["token"], p_body="let me in")
    check(status == 403, "C, who is in no guild, CANNOT POST TO ONE", (status, message_of(body)))

    status, body = request("POST", "/rest/v1/guild_messages", token=a["token"],
                           body={"guild_id": guild["id"], "user_id": a["id"], "body": "around the limit"})
    check(status not in (200, 201, 204),
          "AND NOBODY CAN INSERT A MESSAGE DIRECTLY - the rate limit is a privilege, not a convention",
          (status, message_of(body)))

    status, body = request("GET", "/rest/v1/guild_messages?select=id,body,created_at,user_id"
                                  "&guild_id=eq." + guild["id"] + "&order=id.desc&limit=100",
                           token=b["token"])
    readable = status == 200 and isinstance(body, list) and len(body) >= 2
    check(readable, "B, a member, reads the guild's messages", (status, body))
    if readable:
        check(body[0]["id"] > body[1]["id"], "newest first, which is the order the client asks for")

    status, body = request("GET", "/rest/v1/guild_messages?select=id,body", token=c["token"])
    check(status == 200 and isinstance(body, list) and len(body) == 0,
          "C READS NOTHING OF IT - zero rows, which is what RLS filtering looks like", (status, body))

    status, body = request("PATCH", "/rest/v1/guild_messages?guild_id=eq." + guild["id"],
                           token=a["token"], body={"body": "rewritten"})
    check(status not in (200, 204), "a message cannot be edited, by its author or by anybody",
          (status, message_of(body)))
    status, body = request("DELETE", "/rest/v1/guild_messages?guild_id=eq." + guild["id"],
                           token=a["token"])
    status2, body2 = request("GET", "/rest/v1/guild_messages?select=id&guild_id=eq." + guild["id"],
                             token=a["token"])
    check(status2 == 200 and isinstance(body2, list) and len(body2) >= 2,
          "and cannot be deleted either - they are still there", (status, len(body2) if isinstance(body2, list) else body2))


def check_presence(a, b):
    section("PRESENCE - stamped by the database, never sent by a client")

    before = my_membership(a)
    time.sleep(1.1)
    status, body = rpc("guild_touch", token=a["token"])
    check(status == 200 and isinstance(body, str), "guild_touch answers with the stamp it wrote",
          (status, body))
    after = my_membership(a)
    check(before and after and after["last_seen"] > before["last_seen"],
          "and last_seen moved forward", (before, after))

    status, body = request("PATCH", "/rest/v1/guild_members?user_id=eq." + a["id"], token=a["token"],
                           body={"last_seen": "2099-01-01T00:00:00Z"})
    unchanged = my_membership(a)
    check(status not in (200, 204) and unchanged and unchanged["last_seen"] == after["last_seen"],
          "A CANNOT CLAIM A FRESHNESS IT DOES NOT HAVE - no UPDATE grant on the column",
          (status, unchanged))

    status, body = request("PATCH", "/rest/v1/guild_members?user_id=eq." + b["id"], token=a["token"],
                           body={"last_seen": "2099-01-01T00:00:00Z"})
    check(status not in (200, 204), "and cannot stamp somebody else either", status)


def check_snapshots(guild, a, b, c):
    section("A GUILDMATE'S LINE - readable without a friendship, and only to the guild")

    line = [{"defId": "kael_the_unyielding", "level": 30, "stars": 5, "armor": []}]
    status, _ = request("POST", "/rest/v1/team_snapshots?on_conflict=user_id", token=b["token"],
                        body={"user_id": b["id"], "label": "gt-" + RUN_TAG,
                              "power_score": 7777, "units": line},
                        prefer="resolution=merge-duplicates,return=minimal")
    check(status in (200, 201, 204), "B publishes a line", status)

    status, body = request("GET", "/rest/v1/team_snapshots?select=user_id,label,power_score,units"
                                  "&user_id=eq." + b["id"], token=a["token"])
    got = status == 200 and isinstance(body, list) and len(body) == 1
    check(got, "A CAN READ IT - they are guildmates, and they are NOT friends", (status, body))
    if got:
        check(body[0]["power_score"] == 7777, "and it is the line B published, intact", body[0])

    status, body = request("GET", "/rest/v1/friendships?select=id", token=a["token"])
    check(status == 200 and isinstance(body, list) and len(body) == 0,
          "there is no friendship between them at all, so the guild is what bought the read",
          (status, body))

    status, body = request("GET", "/rest/v1/team_snapshots?select=user_id&user_id=eq." + b["id"],
                           token=c["token"])
    check(status == 200 and isinstance(body, list) and len(body) == 0,
          "C, in no guild and no friendship, reads nothing of it", (status, body))

    # The privacy line the whole split between `saves` and `team_snapshots` exists to draw, now
    # asked of a guildmate rather than of a friend.
    status, body = request("GET", "/rest/v1/saves?select=user_id,data&user_id=eq." + b["id"],
                           token=a["token"])
    check(status == 200 and isinstance(body, list) and len(body) == 0,
          "BEING GUILDMATES DOES NOT MAKE B's SAVE READABLE", (status, body))

    status, _ = request("PATCH", "/rest/v1/team_snapshots?user_id=eq." + b["id"], token=a["token"],
                        body={"label": "vandalised"})
    status2, body2 = request("GET", "/rest/v1/team_snapshots?select=label&user_id=eq." + b["id"],
                             token=a["token"])
    check(status2 == 200 and body2 and body2[0]["label"] != "vandalised",
          "and a guildmate can read the line but not write it")


# ---------------------------------------------------------------- the guild dungeon (0007)


DUNGEON_LINE = [{"defId": "kael_the_unyielding", "level": 40, "stars": 5}]


def record_attempt(user, cycle, floor, team=None, **overrides):
    """One guild_dungeon_record call with a well-formed attempt, any argument overridable. The
    values are what a client would send. The server trusts them, which is the design, so the
    cases below are about WHO may write, HOW MANY and WHEN, not whether the fight was real."""
    args = {"p_cycle": cycle, "p_floor": floor, "p_seed": 417005212,
            "p_team": DUNGEON_LINE if team is None else team,
            "p_victory": True, "p_turns": 9, "p_power": 12345, "p_avg_stars": 5.0}
    args.update(overrides)
    return rpc("guild_dungeon_record", token=user["token"], **args)


def dungeon_rows(user, guild_id):
    status, body = request("GET", "/rest/v1/guild_dungeon_attempts?select=id,user_id,cycle,floor,attempt,"
                                  "seed,team,victory,turns,power,avg_stars&guild_id=eq." + guild_id
                                  + "&order=id.asc", token=user["token"])
    return body if status == 200 and isinstance(body, list) else None


def check_dungeon(guild, a, b, c):
    section("THE GUILD DUNGEON - the server holds the clock and the cap, and nothing else")

    status, body = rpc("guild_dungeon_status", token=a["token"])
    row = body[0] if status == 200 and isinstance(body, list) and len(body) == 1 else None
    check(row is not None, "A asks where the dungeon stands and gets one answer", (status, message_of(body)))
    if row is None:
        return None
    cycle = row["cycle"]
    check(row["guild_id"] == guild["id"], "for A's own guild - the client derives the seed from this id", row)
    check(isinstance(cycle, int) and cycle >= 0 and row["attempt_cap"] == 3 and row["floor_count"] == 5,
          "a cycle number, three attempts a floor, five floors (cycle " + str(cycle) + ")", row)
    check(row["starts_at"] < row["ends_at"], "and the window the cycle covers", row)

    status, body = rpc("guild_dungeon_status", token=c["token"])
    check(status == 403, "C, in no guild, has no dungeon to ask about", (status, message_of(body)))

    status, body = record_attempt(a, cycle, 1)
    first = body if status == 200 and isinstance(body, dict) else None
    check(first is not None, "A records an attempt at floor 1", (status, message_of(body)))
    if first is None:
        return None
    check(first.get("attempt") == 1 and first.get("user_id") == a["id"] and first.get("guild_id") == guild["id"],
          "numbered 1, and filed under A and A's guild by the server rather than by anything A sent", first)
    check(first.get("team") == DUNGEON_LINE and first.get("seed") == 417005212,
          "carrying the seed and the whole line that fought - what a replay needs", first)

    for expected in (2, 3):
        status, body = record_attempt(a, cycle, 1, p_turns=9 + expected)
        check(status == 200 and isinstance(body, dict) and body.get("attempt") == expected,
              "attempt " + str(expected) + " at floor 1 is accepted", (status, message_of(body)))

    status, body = record_attempt(a, cycle, 1)
    check(status == 409, "A FOURTH ATTEMPT AT THE SAME FLOOR IS REFUSED - three is the rule",
          (status, message_of(body)))

    status, body = record_attempt(a, cycle, 2)
    check(status == 200 and isinstance(body, dict) and body.get("attempt") == 1,
          "while floor 2 still has all three - the cap is per floor", (status, message_of(body)))

    status, body = record_attempt(a, cycle - 1, 3)
    check(status == 409, "an attempt filed against LAST cycle is refused", (status, message_of(body)))
    status, body = record_attempt(a, cycle + 1, 3)
    check(status == 409, "and so is one against NEXT cycle - the server holds the clock", (status, message_of(body)))

    for floor, why in ((0, "floor 0"), (6, "floor 6")):
        status, body = record_attempt(a, cycle, floor)
        check(status == 400, why + " does not exist", (status, message_of(body)))
    status, body = record_attempt(a, cycle, 3, team=[])
    check(status == 400, "an empty line is refused", (status, message_of(body)))
    status, body = record_attempt(a, cycle, 3, team=DUNGEON_LINE * 6)
    check(status == 400, "and so is a line of six", (status, message_of(body)))
    status, body = record_attempt(a, cycle, 3, team={"defId": "kael_the_unyielding"})
    check(status == 400, "and so is a team that is not a list", (status, message_of(body)))
    status, body = record_attempt(a, cycle, 3, p_turns=-1)
    check(status == 400, "and a negative turn count", (status, message_of(body)))

    status, body = record_attempt(c, cycle, 1)
    check(status == 403, "C, IN NO GUILD, CANNOT RECORD AN ATTEMPT", (status, message_of(body)))

    rows = dungeon_rows(b, guild["id"])
    check(rows is not None and len(rows) == 4 and all(r["user_id"] == a["id"] for r in rows),
          "B, a guildmate, reads all four of A's attempts - the boards are built from this read",
          (len(rows) if rows is not None else rows))
    rows_c = dungeon_rows(c, guild["id"])
    check(rows_c is not None and len(rows_c) == 0,
          "C READS NONE OF THEM - zero rows, which is what RLS filtering looks like", rows_c)

    status, body = request("POST", "/rest/v1/guild_dungeon_attempts", token=a["token"],
                           body={"guild_id": guild["id"], "user_id": a["id"], "cycle": cycle, "floor": 4,
                                 "attempt": 1, "seed": 1, "team": DUNGEON_LINE, "victory": True,
                                 "turns": 1, "power": 1, "avg_stars": 1})
    check(status not in (200, 201, 204),
          "A CANNOT INSERT AN ATTEMPT DIRECTLY - no client role holds INSERT, so the cap cannot be walked around",
          (status, message_of(body)))

    status, body = request("PATCH", "/rest/v1/guild_dungeon_attempts?id=eq." + str(first["id"]),
                           token=a["token"], body={"turns": 1})
    after = dungeon_rows(a, guild["id"])
    kept = after is not None and any(r["id"] == first["id"] and r["turns"] == first["turns"] for r in after)
    check(status not in (200, 204) and kept,
          "A CANNOT IMPROVE A SCORE AFTER THE FACT - no UPDATE for anybody, its owner included",
          (status, message_of(body)))

    status, body = request("DELETE", "/rest/v1/guild_dungeon_attempts?id=eq." + str(first["id"]),
                           token=a["token"])
    after = dungeon_rows(a, guild["id"])
    check(after is not None and len(after) == 4,
          "and cannot delete a bad attempt to get it back - the four rows are still there",
          (status, len(after) if after is not None else after))
    return cycle


def check_dungeon_race(guild, b, cycle):
    """The cap as a constraint rather than a count. Six submissions from one account at the same
    instant, at a floor it has not touched: the member-row lock makes them queue, and the check
    plus the unique key would refuse a fourth row even if it did not."""
    section("THE ATTEMPT CAP, UNDER CONTENTION - one member, six submissions, one instant")

    racers = 6
    barrier = threading.Barrier(racers)
    results = [None] * racers

    def race(index):
        try:
            barrier.wait(timeout=30)
        except threading.BrokenBarrierError:
            pass
        results[index] = record_attempt(b, cycle, 4, p_turns=20 + index)

    with ThreadPoolExecutor(max_workers=racers) as pool:
        list(pool.map(race, range(racers)))

    accepted = [r for r in results if r and r[0] == 200]
    refused = [r for r in results if r and r[0] == 409]
    check(len(accepted) == 3, "EXACTLY THREE OF SIX WERE STORED (" + str(len(accepted)) + ")",
          [r[0] for r in results if r])
    check(len(refused) == racers - 3, "and the other three were refused with a 409 (" + str(len(refused)) + ")",
          [message_of(r[1]) for r in results if r and r[0] not in (200, 409)][:2])
    numbers = sorted(r[1].get("attempt") for r in accepted if isinstance(r[1], dict))
    check(numbers == [1, 2, 3], "numbered 1, 2 and 3 - no gap and no repeat", numbers)

def check_friends_keep_access(c, d):
    section("0006 ADDED A POLICY - so a friend who is not a guildmate still reads a line")

    line = [{"defId": "kael_the_unyielding", "level": 7, "stars": 3, "armor": []}]
    request("POST", "/rest/v1/team_snapshots?on_conflict=user_id", token=d["token"],
            body={"user_id": d["id"], "label": "friend-line", "power_score": 1234, "units": line},
            prefer="resolution=merge-duplicates,return=minimal")

    status, body = request("GET", "/rest/v1/team_snapshots?select=user_id&user_id=eq." + d["id"],
                           token=c["token"])
    check(status == 200 and isinstance(body, list) and len(body) == 0,
          "before they are friends, C reads nothing of D's line", (status, body))

    status, _ = request("POST", "/rest/v1/friendships", token=c["token"],
                        body={"requester": c["id"], "addressee": d["id"], "status": "pending"})
    status, body = request("GET", "/rest/v1/friendships?select=id,status", token=d["token"])
    friendship = body[0] if status == 200 and isinstance(body, list) and body else None
    if friendship:
        request("PATCH", "/rest/v1/friendships?id=eq." + friendship["id"], token=d["token"],
                body={"status": "accepted"})

    status, body = request("GET", "/rest/v1/team_snapshots?select=user_id,power_score"
                                  "&user_id=eq." + d["id"], token=c["token"])
    check(status == 200 and isinstance(body, list) and len(body) == 1,
          "FRIENDS KEEP THE ACCESS 0003 GAVE THEM - the guild policy was added beside it, not over it",
          (status, body))


def check_anon_role():
    section("THE ANON ROLE - no user at all, which is what a leaked anon key gets you")

    for table in ("guilds", "guild_members", "guild_requests", "guild_messages", "guild_dungeon_attempts"):
        status, body = request("GET", "/rest/v1/" + table + "?select=*")
        readable = status == 200 and isinstance(body, list) and len(body) > 0
        check(not readable, "the anon role reads nothing from " + table, (status, body))

    for name, args in (("guild_create", {"p_name": "Anonymous", "p_description": ""}),
                       ("guild_by_code", {"p_code": "ABC234"}),
                       ("guild_request_send", {"p_code": "ABC234"}),
                       ("guild_post_message", {"p_body": "hello"}),
                       ("guild_touch", {}),
                       ("guild_dungeon_status", {}),
                       ("guild_dungeon_record", {"p_cycle": 0, "p_floor": 1, "p_seed": 1,
                                                 "p_team": DUNGEON_LINE, "p_victory": True,
                                                 "p_turns": 1, "p_power": 1, "p_avg_stars": 1})):
        status, body = rpc(name, **args)
        check(status not in (200, 201), "and cannot call " + name, (status, message_of(body)))


def check_one_guild_rule(guild, d):
    section("ONE GUILD PER PLAYER - a primary key, not a check, and not a client's promise")

    other = guild_name("Rival")
    status, body = rpc("guild_create", token=d["token"], p_name=other, p_description="")
    rival = body if status == 200 and isinstance(body, dict) else None
    check(rival is not None, "D creates a guild of its own", (status, message_of(body)))
    if rival is None:
        return None

    status, body = rpc("guild_request_send", token=d["token"], p_code=guild["guild_code"])
    check(status == 409, "D CANNOT ASK TO JOIN A SECOND GUILD while leading one",
          (status, message_of(body)))

    status, body = request("POST", "/rest/v1/guild_members", token=d["token"],
                           body={"user_id": d["id"], "guild_id": guild["id"], "role": "member"})
    check(status not in (200, 201, 204),
          "and cannot insert a second membership row - guild_members grants no INSERT to anybody",
          (status, message_of(body)))

    membership = my_membership(d)
    check(membership is not None and membership["guild_id"] == rival["id"],
          "D is still in exactly one guild, its own", membership)
    return rival


def check_cap_race(racers, leader):
    """THE ONE RULE THAT CANNOT BE TESTED ONE REQUEST AT A TIME.

    A count inside a function is not a cap. Two officers accepting two different applicants both
    read a roster of 29 and both insert, and the guild ends at 31 - unless something makes them
    queue first, which is what `select ... for update` on the guild's own row is for. The only
    honest way to know it holds is to make a real guild really reach thirty with real requests
    arriving at the same instant, which is why this costs thirty throwaway accounts."""
    section("THE CAP, UNDER CONTENTION - " + str(len(racers)) + " devices asking at the same instant")

    name = guild_name("Crowd")
    status, body = rpc("guild_create", token=leader["token"], p_name=name, p_description="open house")
    guild = body if status == 200 and isinstance(body, dict) else None
    check(guild is not None, "the leader creates a guild", (status, message_of(body)))
    if guild is None:
        return None

    status, _ = request("PATCH", "/rest/v1/guilds?id=eq." + guild["id"], token=leader["token"],
                        body={"is_open": True})
    check(status in (200, 204), "and opens it, so a request admits on arrival", status)

    status, body = request("GET", "/rest/v1/guilds?select=is_open&id=eq." + guild["id"],
                           token=leader["token"])
    check(status == 200 and body and body[0]["is_open"] is True, "which the row confirms", body)

    cap = 30
    status, body = rpc("guild_member_cap", token=leader["token"])
    if status == 200 and isinstance(body, int):
        cap = body
    note("the database's own cap is " + str(cap) + "; the guild already holds its leader, so "
         + str(cap - 1) + " of the " + str(len(racers)) + " racers can fit")

    barrier = threading.Barrier(len(racers))
    results = [None] * len(racers)

    def race(index):
        user = racers[index]
        try:
            barrier.wait(timeout=60)
        except threading.BrokenBarrierError:
            pass
        results[index] = rpc("guild_request_send", token=user["token"], p_code=guild["guild_code"])

    started = time.time()
    with ThreadPoolExecutor(max_workers=len(racers)) as pool:
        list(pool.map(race, range(len(racers))))
    elapsed = time.time() - started

    joined = [i for i, r in enumerate(results) if r and r[0] == 200 and r[1] == "joined"]
    refused = [i for i, r in enumerate(results) if r and r[0] == 409]
    other = [i for i, r in enumerate(results) if i not in joined and i not in refused]

    note(str(len(racers)) + " requests fired from a barrier, all answered in "
         + ("%.2f" % elapsed) + "s")
    for i in other[:3]:
        note("  unexpected answer from racer " + str(i) + ": " + str(results[i]))

    check(len(other) == 0, "every racer got a definite answer - a join or a refusal, nothing else",
          other[:3])
    check(len(joined) == cap - 1,
          "EXACTLY " + str(cap - 1) + " RACERS GOT IN (" + str(len(joined)) + ")",
          (len(joined), len(refused)))
    check(len(refused) == len(racers) - (cap - 1),
          "and the rest were refused with a 409 (" + str(len(refused)) + ")", len(refused))

    size = roster_size(guild["id"], leader["token"])
    check(size == cap,
          "THE ROSTER IS EXACTLY " + str(cap) + " - the cap held with " + str(len(racers))
          + " requests racing for " + str(cap - 1) + " places (" + str(size) + ")", size)

    status, body = rpc("guild_request_send", token=racers[refused[0]]["token"] if refused else leader["token"],
                       p_code=guild["guild_code"])
    check(status == 409, "and a request arriving afterwards is still refused",
          (status, message_of(body)))

    status, body = request("GET", "/rest/v1/guild_members?select=user_id,role&guild_id=eq." + guild["id"]
                                  + "&role=eq.leader", token=leader["token"])
    check(status == 200 and isinstance(body, list) and len(body) == 1,
          "and the guild still has exactly one leader after all of it", (status, body))
    return guild


def check_disband(a):
    section("DISBAND - the leader's alone, and the cascades take everything with it")

    membership = my_membership(a)
    if membership is None:
        check(False, "A is in a guild to disband", membership)
        return
    guild_id = membership["guild_id"]

    before = roster_size(guild_id, a["token"])
    status, body = request("GET", "/rest/v1/guild_messages?select=id&guild_id=eq." + guild_id,
                           token=a["token"])
    messages = len(body) if isinstance(body, list) else 0
    attempts = dungeon_rows(a, guild_id)
    attempts = len(attempts) if attempts is not None else 0

    status, _ = request("DELETE", "/rest/v1/guilds?id=eq." + guild_id, token=a["token"])
    check(status in (200, 204), "the leader disbands the guild", status)

    check(my_membership(a) is None,
          "every membership went with it (" + str(before) + " of them)")
    status, body = request("GET", "/rest/v1/guild_messages?select=id&guild_id=eq." + guild_id,
                           token=a["token"])
    check(status == 200 and isinstance(body, list) and len(body) == 0,
          "and so did its " + str(messages) + " message(s) - nothing is left addressed to a guild "
          "that does not exist", (status, body))
    rows = dungeon_rows(a, guild_id)
    check(rows is not None and len(rows) == 0,
          "and so did its " + str(attempts) + " dungeon attempt(s)", rows)

    status, body = rpc("guild_create", token=a["token"], p_name=guild_name("After"), p_description="")
    check(status == 200, "and A, now in no guild, can make another one", (status, message_of(body)))


# ---------------------------------------------------------------- cleanup


def disband_everything(users):
    """Every guild this run created is led by one of these users. Asking each of them what they
    lead and deleting it leaves nothing behind but the accounts themselves, which is what makes
    the paste below a single statement about users."""
    dropped = 0
    for user in users:
        membership = my_membership(user)
        if membership and membership.get("role") == "leader":
            status, _ = request("DELETE", "/rest/v1/guilds?id=eq." + membership["guild_id"],
                                token=user["token"])
            if status in (200, 204):
                dropped += 1
    return dropped


def print_cleanup_statement():
    print("")
    print("=" * 78)
    print("CLEANUP - paste this ONE statement into the Supabase SQL editor.")
    print("Deleting a user needs the service role, which this script deliberately does not hold.")
    print("The cascades take each user's profile, save, snapshot, friendships and membership.")
    print("=" * 78)
    print("")
    if not CREATED:
        print("-- this run created no users")
        return
    print("delete from auth.users where id in (")
    for index, uid in enumerate(CREATED):
        tail = "," if index < len(CREATED) - 1 else ""
        print("  '" + uid + "'" + tail)
    print(");")
    print("")
    print("-- " + str(len(CREATED)) + " user(s), all tagged gt-" + RUN_TAG + "-* in profiles.display_name")


# ---------------------------------------------------------------- entry point


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--racers", type=int, default=30,
                        help="how many accounts race for the last places (default 30)")
    parser.add_argument("--no-race", action="store_true",
                        help="skip the cap race, which is what makes the run cheap")
    parser.add_argument("--anonymous-racers", action="store_true",
                        help="make the racers anonymous too - needs the hourly anonymous sign-in "
                             "limit raised in the dashboard")
    parser.add_argument("--all-emails", action="store_true",
                        help="put the whole cast on emailed accounts, for a run made after the "
                             "hourly anonymous limit is already spent")
    args = parser.parse_args()

    load_credentials()
    if "service_role" in ANON:
        print("That looks like the service role key. Use the ANON PUBLIC key - the service role")
        print("bypasses RLS, so every check here would pass and none of them would mean anything.")
        return 2

    print("EMBERFALL SUPABASE GUILD RULE TEST")
    print("project: " + URL)
    print("run tag: gt-" + RUN_TAG + "-*   (every account this run creates carries it)")

    cast = []
    guild = None
    a = None
    try:
        section("SETUP")
        if args.all_emails:
            note("--all-emails: the whole cast carries an email, because the hourly anonymous")
            note("limit is spent. Run without it to put the small cast back on anonymous users,")
            note("which is what the game actually creates.")
        cast_anonymous = not args.all_emails
        a = must_sign_up("a", anonymous=cast_anonymous)
        b = must_sign_up("b", anonymous=cast_anonymous)
        c = must_sign_up("c", anonymous=cast_anonymous)
        d = must_sign_up("d", anonymous=cast_anonymous)
        cast = [a, b, c, d]
        check(len({a["id"], b["id"], c["id"], d["id"]}) == 4, "four distinct throwaway users")
        for user in cast:
            check(profile_of(user) is not None, user["name"] + " has a profile row")

        guild = check_creation(a, b)
        if guild:
            check_lookup_and_privacy(guild, a, b, c)
            check_requests(guild, a, b, c)
            check_roles(guild, a, b, c)
            check_officer_powers(guild, a, b, c, d)
            check_leaving(guild, a, b, c)
            check_chat(guild, a, b, c)
            check_presence(a, b)
            check_snapshots(guild, a, b, c)
            cycle = check_dungeon(guild, a, b, c)
            if cycle is not None:
                check_dungeon_race(guild, b, cycle)
            check_one_guild_rule(guild, d)
        check_anon_role()

        # C and D are both out of a guild by now for the friendship case, so it is run last of
        # the small-cast sections and uses two fresh accounts rather than unpicking theirs.
        e = must_sign_up("e", anonymous=cast_anonymous)
        f = must_sign_up("f", anonymous=cast_anonymous)
        cast.extend([e, f])
        check_friends_keep_access(e, f)

        if not args.no_race:
            section("SETUP FOR THE CAP RACE - " + str(args.racers + 1) + " more accounts"
                    + ("" if args.anonymous_racers else ", carrying an email"))
            if not args.anonymous_racers:
                note("emailed rather than anonymous, because the anonymous limit is per HOUR and")
                note("this needs more accounts than an hour's worth. See sign_up() for why that")
                note("changes nothing a policy can see. --anonymous-racers overrides it.")
            host = must_sign_up("host", anonymous=args.anonymous_racers,
                                attempts=12, max_delay=60.0)
            cast.append(host)
            racers = []
            for index in range(args.racers):
                # Twelve attempts at up to a minute apart, which can outwait a five-minute
                # sign-up window. It cannot outwait the hourly anonymous one, and is not asked to.
                racer = sign_up("r" + str(index), anonymous=args.anonymous_racers,
                                attempts=12, max_delay=60.0)
                if racer is None:
                    print("")
                    print("  FATAL  the auth rate limit stopped account " + str(index)
                          + " of " + str(args.racers) + ".")
                    print("         Supabase's defaults are 30 anonymous sign-ins per hour per IP")
                    print("         and 30 sign-ups per 5 minutes per IP. Raise them under")
                    print("         Dashboard -> Authentication -> Rate Limits and re-run, or use")
                    print("         --no-race to run everything else.")
                    break
                racers.append(racer)
            cast.extend(racers)
            check(len(racers) == args.racers,
                  str(args.racers) + " racing accounts were created (" + str(len(racers)) + ")")
            if len(racers) == args.racers:
                check_cap_race(racers, host)

        if guild:
            check_disband(a)
    except SystemExit:
        raise
    finally:
        section("CLEANUP")
        dropped = disband_everything(cast)
        note("disbanded " + str(dropped) + " guild(s) this run created, so nothing is left but accounts")

        print("")
        print("RESULT: " + str(PASSES) + " ok, " + str(FAILURES) + " FAILED")
        print_cleanup_statement()

    return 1 if FAILURES else 0


if __name__ == "__main__":
    sys.exit(main())
