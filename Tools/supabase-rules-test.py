#!/usr/bin/env python3
"""Proves the database's own rules against a real Supabase project.

The harness in Tools/DevHarness proves the CLIENT cannot lose a save. This proves the SERVER
cannot be talked into handing one over. Neither substitutes for the other: Row Level Security,
the friend-code trigger and push_save's compare-and-swap are facts about a Postgres instance,
and the only way to know they hold is to ask that instance.

Every rule is tested WITHOUT the game client, on purpose. A test that went through
SocialService would be testing the two halves together, and the half that matters here is the
one that has to hold when the client is hostile, missing or replaced.

    Run the three migrations in Server/supabase/migrations first, in order.

    $env:SUPABASE_URL      = 'https://<ref>.supabase.co'
    $env:SUPABASE_ANON_KEY = '<anon public key>'
    python Tools/supabase-rules-test.py

Only the ANON PUBLIC key is used, and only the anon key is needed - every case here is either
an unauthenticated request or one made as a user the script itself created. The service role
key is never used and must not be set for this.

WHAT IT LEAVES BEHIND: two anonymous users, and their profiles, saves, snapshots and
friendship. Deleting a user needs the service role, which this script deliberately does not
hold, so it prints both user ids at the end for deletion from the dashboard
(Authentication -> Users). Cascades take everything else with them.

Standard library only - no pip install, so it runs on a machine that has nothing set up.
"""

import json
import os
import random
import string
import sys
import urllib.error
import urllib.request

URL = os.environ.get("SUPABASE_URL", "").rstrip("/")
ANON = os.environ.get("SUPABASE_ANON_KEY", "")

PASSES = 0
FAILURES = 0


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


# ---------------------------------------------------------------- transport


def request(method, path, token=None, body=None, prefer=None):
    """Returns (status, parsed_body_or_text). Never raises on an HTTP error status -
    a refusal IS the result in most of these cases."""
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
        with urllib.request.urlopen(req, timeout=20) as response:
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


def sign_up_anonymous(who):
    status, body = request("POST", "/auth/v1/signup", body={})
    if status not in (200, 201) or not isinstance(body, dict) or "access_token" not in body:
        print("  FATAL  could not create an anonymous user for " + who + ": " + str(status) + " " + str(body))
        print("         Is 'Allow anonymous sign-ins' on in Authentication -> Sign In / Providers?")
        sys.exit(2)
    return {
        "name": who,
        "token": body["access_token"],
        "refresh": body.get("refresh_token", ""),
        "id": body["user"]["id"],
    }


def profile_of(user):
    status, body = request(
        "GET",
        "/rest/v1/profiles?select=user_id,display_name,friend_code&user_id=eq." + user["id"],
        token=user["token"],
    )
    if status == 200 and isinstance(body, list) and body:
        return body[0]
    return None


def random_email():
    tag = "".join(random.choice(string.ascii_lowercase + string.digits) for _ in range(10))
    # example.com is reserved by RFC 2606 and cannot receive mail, which is what is wanted:
    # the test is that the USER ID survives linking, not that a mailbox exists.
    return "emberfall-test-" + tag + "@example.com"


# ---------------------------------------------------------------- the cases


def check_profiles(a, b):
    section("PROFILES - created automatically, readable by any signed-in user, code is not writable")

    pa = profile_of(a)
    pb = profile_of(b)
    check(pa is not None, "an anonymous user gets a profile row automatically", pa)
    check(pb is not None, "and so does the second one", pb)
    if not pa or not pb:
        return None, None

    alphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ"
    code = pa.get("friend_code", "")
    check(
        len(code) == 6 and all(c in alphabet for c in code),
        "the friend code is six characters from the unambiguous alphabet (" + code + ")",
    )
    check(pa["friend_code"] != pb["friend_code"], "the two users got different codes")

    status, body = request(
        "GET",
        "/rest/v1/profiles?select=user_id,display_name&user_id=eq." + b["id"],
        token=a["token"],
    )
    check(
        status == 200 and isinstance(body, list) and len(body) == 1,
        "A can read B's profile - profiles are public to signed-in users by design",
        status,
    )

    # The friend code column carries no UPDATE grant, so this is refused on privilege rather
    # than filtered by a policy: PostgREST answers 403, not 200-with-zero-rows.
    status, body = request(
        "PATCH",
        "/rest/v1/profiles?user_id=eq." + a["id"],
        token=a["token"],
        body={"friend_code": "AAAAAA"},
    )
    check(status not in (200, 204), "A cannot rewrite its OWN friend code", status)
    after = profile_of(a)
    check(after and after["friend_code"] == code, "and the code is unchanged afterwards")

    status, _ = request(
        "PATCH",
        "/rest/v1/profiles?user_id=eq." + a["id"],
        token=a["token"],
        body={"display_name": "Matthias"},
    )
    check(status in (200, 204), "but A can set its own display name", status)

    status, _ = request(
        "PATCH",
        "/rest/v1/profiles?user_id=eq." + b["id"],
        token=a["token"],
        body={"display_name": "not yours"},
    )
    moved = profile_of(b)
    check(
        moved is not None and moved["display_name"] != "not yours",
        "and A cannot rename B",
        status,
    )
    return pa, pb


def check_saves(a, b):
    section("SAVES - owner only, and the revision check is the database's, not the client's")

    document = {"version": 13, "playerName": "Matthias", "playerLevel": 4, "stagesCleared": 2, "heroes": []}

    status, body = request(
        "POST",
        "/rest/v1/rpc/push_save",
        token=a["token"],
        body={"p_expected_revision": 0, "p_save_version": 13, "p_device_id": "test-a", "p_data": document},
    )
    check(status == 200 and isinstance(body, dict) and body.get("revision") == 1,
          "A's first upload claims revision 0 and is stored as revision 1", (status, body))

    # The whole point of compare-and-swap: the SAME call a second time is now stale, because
    # the stored revision moved to 1 and this still claims 0.
    status, body = request(
        "POST",
        "/rest/v1/rpc/push_save",
        token=a["token"],
        body={"p_expected_revision": 0, "p_save_version": 13, "p_device_id": "test-a", "p_data": document},
    )
    check(status == 409, "A STALE REVISION IS REFUSED, with a 409", (status, body))

    status, body = request(
        "POST",
        "/rest/v1/rpc/push_save",
        token=a["token"],
        body={"p_expected_revision": 1, "p_save_version": 13, "p_device_id": "test-a", "p_data": document},
    )
    check(status == 200 and isinstance(body, dict) and body.get("revision") == 2,
          "and the correct revision is accepted and increments to 2", (status, body))

    check(isinstance(body, dict) and body.get("user_id") == a["id"],
          "the stored row belongs to A, whatever the client sent")

    # B, signed in and legitimate, simply cannot see it. RLS FILTERS rather than refuses, so
    # the shape of the right answer here is 200 with zero rows, not 403.
    status, body = request(
        "GET", "/rest/v1/saves?select=user_id,revision,data&user_id=eq." + a["id"], token=b["token"]
    )
    check(status == 200 and isinstance(body, list) and len(body) == 0,
          "B CANNOT READ A's SAVE - zero rows, which is what RLS filtering looks like", (status, body))

    status, body = request("GET", "/rest/v1/saves?select=user_id,revision", token=b["token"])
    check(status == 200 and isinstance(body, list) and len(body) == 0,
          "and an unfiltered select returns B nothing either", (status, body))

    # No client role holds INSERT or UPDATE on saves at all, so a direct write is refused on
    # privilege before RLS is consulted. This is the check that says the concurrency rule
    # cannot be gone around rather than merely being followed.
    status, body = request(
        "PATCH",
        "/rest/v1/saves?user_id=eq." + a["id"],
        token=a["token"],
        body={"revision": 99, "data": document},
    )
    check(status not in (200, 204), "NOBODY can PATCH saves directly - not even its owner", status)

    status, body = request(
        "POST", "/rest/v1/saves", token=b["token"],
        body={"user_id": a["id"], "save_version": 13, "revision": 1, "data": document},
    )
    check(status not in (200, 201), "and B cannot INSERT a row over A's save", status)

    status, body = request("GET", "/rest/v1/saves?select=revision&user_id=eq." + a["id"], token=a["token"])
    check(status == 200 and isinstance(body, list) and len(body) == 1 and body[0]["revision"] == 2,
          "A's save is still at revision 2 after all of that", (status, body))


def check_anon_role():
    section("THE ANON ROLE - no user at all, which is what a leaked anon key gets you")

    for table in ("profiles", "saves", "team_snapshots", "friendships"):
        status, body = request("GET", "/rest/v1/" + table + "?select=*")
        readable = status == 200 and isinstance(body, list) and len(body) > 0
        check(not readable, "the anon role reads nothing from " + table, (status, body))

    status, body = request(
        "POST", "/rest/v1/rpc/push_save",
        body={"p_expected_revision": 0, "p_save_version": 13, "p_device_id": "anon", "p_data": {}},
    )
    check(status not in (200, 201), "and the anon role cannot call push_save", (status, body))


def check_friendship_and_snapshots(a, b, pa, pb):
    section("FRIENDSHIPS AND TEAM SNAPSHOTS - a snapshot is unreadable until the friendship is accepted")

    # Friend-code lookup is a plain filtered select rather than an RPC, which is only possible
    # because profiles are readable by any signed-in user. This is the call the Add Friend
    # screen makes.
    status, body = request(
        "GET",
        "/rest/v1/profiles?select=user_id,display_name,friend_code&friend_code=eq." + pb["friend_code"],
        token=a["token"],
    )
    check(status == 200 and isinstance(body, list) and len(body) == 1
          and body[0]["user_id"] == b["id"],
          "A resolves B's friend code to B's account (" + pb["friend_code"] + ")", (status, body))

    status, body = request(
        "GET", "/rest/v1/profiles?select=user_id&friend_code=eq.ZZZZZZ", token=a["token"]
    )
    check(status == 200 and isinstance(body, list) and len(body) == 0,
          "and a code nobody holds resolves to nothing rather than to an error", (status, body))

    line = [{"defId": "kael_the_unyielding", "level": 12, "stars": 5, "armor": []}]
    status, _ = request(
        "POST", "/rest/v1/team_snapshots?on_conflict=user_id", token=b["token"],
        body={"user_id": b["id"], "label": "Pinoy", "power_score": 4200, "units": line},
        prefer="resolution=merge-duplicates,return=minimal",
    )
    check(status in (200, 201, 204), "B publishes a team snapshot", status)

    status, body = request(
        "GET", "/rest/v1/team_snapshots?select=user_id,label,units&user_id=eq." + b["id"], token=a["token"]
    )
    check(status == 200 and isinstance(body, list) and len(body) == 0,
          "A CANNOT READ B's SNAPSHOT before they are friends", (status, body))

    status, body = request(
        "POST", "/rest/v1/friendships", token=a["token"],
        body={"requester": a["id"], "addressee": b["id"], "status": "pending"},
    )
    check(status in (200, 201, 204), "A sends B a friend request", (status, body))

    status, body = request(
        "GET", "/rest/v1/friendships?select=id,requester,addressee,status", token=a["token"]
    )
    friendship = body[0] if status == 200 and isinstance(body, list) and body else None
    check(friendship is not None, "and A can see the friendship row", (status, body))
    if not friendship:
        return

    # A row inserted as already-accepted would be a friendship the other party never agreed to.
    status, _ = request(
        "POST", "/rest/v1/friendships", token=a["token"],
        body={"requester": a["id"], "addressee": b["id"], "status": "accepted"},
    )
    check(status not in (200, 201), "A cannot insert an already-accepted friendship", status)

    status, body = request(
        "PATCH", "/rest/v1/friendships?id=eq." + friendship["id"], token=a["token"],
        body={"status": "accepted"}, prefer="return=representation",
    )
    accepted_by_requester = status == 200 and isinstance(body, list) and len(body) > 0
    check(not accepted_by_requester,
          "ONLY THE ADDRESSEE CAN ACCEPT - the requester's own accept changes nothing", (status, body))

    status, body = request(
        "GET", "/rest/v1/team_snapshots?select=user_id&user_id=eq." + b["id"], token=a["token"]
    )
    check(status == 200 and isinstance(body, list) and len(body) == 0,
          "and the snapshot is still unreadable while the request is pending", (status, body))

    status, body = request(
        "PATCH", "/rest/v1/friendships?id=eq." + friendship["id"], token=b["token"],
        body={"status": "accepted"}, prefer="return=representation",
    )
    check(status == 200 and isinstance(body, list) and len(body) == 1
          and body[0]["status"] == "accepted",
          "B, the addressee, accepts", (status, body))

    status, body = request(
        "GET", "/rest/v1/team_snapshots?select=user_id,label,power_score,units&user_id=eq." + b["id"],
        token=a["token"],
    )
    got = status == 200 and isinstance(body, list) and len(body) == 1
    check(got, "A CAN NOW READ B's SNAPSHOT - the duel is fightable", (status, body))
    if got:
        check(body[0]["label"] == "Pinoy" and body[0]["power_score"] == 4200,
              "and it is the line B published, intact", body[0])

    # Being friends buys the snapshot and nothing else. This is the privacy line the whole
    # split between `saves` and `team_snapshots` exists to draw.
    status, body = request(
        "GET", "/rest/v1/saves?select=user_id,data&user_id=eq." + b["id"], token=a["token"]
    )
    check(status == 200 and isinstance(body, list) and len(body) == 0,
          "being friends does NOT make B's save readable", (status, body))

    status, _ = request(
        "PATCH", "/rest/v1/team_snapshots?user_id=eq." + b["id"], token=a["token"],
        body={"label": "vandalised"},
    )
    status2, body2 = request(
        "GET", "/rest/v1/team_snapshots?select=label&user_id=eq." + b["id"], token=a["token"]
    )
    check(status2 == 200 and isinstance(body2, list) and body2 and body2[0]["label"] == "Pinoy",
          "and a friend can read the snapshot but not write it")

    status, _ = request(
        "POST", "/rest/v1/friendships", token=b["token"],
        body={"requester": b["id"], "addressee": a["id"], "status": "pending"},
    )
    check(status == 409, "a duplicate friendship in the OTHER direction is refused", status)


def check_email_linking(a):
    section("LINKING AN EMAIL - the user id and the save both survive")

    before_id = a["id"]
    status, body = request("GET", "/rest/v1/saves?select=revision&user_id=eq." + before_id, token=a["token"])
    before_revision = body[0]["revision"] if status == 200 and isinstance(body, list) and body else None

    email = random_email()
    status, body = request(
        "PUT", "/auth/v1/user", token=a["token"],
        body={"email": email, "password": "emberfall-" + email[:8] + "-Aa1"},
    )
    check(status == 200 and isinstance(body, dict), "the anonymous account accepts an email and password",
          (status, body))
    if status != 200 or not isinstance(body, dict):
        return

    check(body.get("id") == before_id,
          "THE USER ID IS UNCHANGED - this is the same account, not a new one", (body.get("id"), before_id))

    status, body = request("GET", "/rest/v1/saves?select=revision&user_id=eq." + before_id, token=a["token"])
    after_revision = body[0]["revision"] if status == 200 and isinstance(body, list) and body else None
    check(after_revision is not None and after_revision == before_revision,
          "and the save is still there, at the same revision (" + str(after_revision) + ")")

    print("        note: with 'Confirm email' on, the address stays pending until the link is")
    print("        clicked. The id and the save are unaffected either way, which is what is")
    print("        checked here - not whether mail was delivered.")


# ---------------------------------------------------------------- entry point


def main():
    if not URL or not ANON:
        print("Set SUPABASE_URL and SUPABASE_ANON_KEY first. See the header of this file.")
        return 2
    if "service_role" in ANON:
        print("That looks like the service role key. Use the ANON PUBLIC key - the service role")
        print("bypasses RLS, so every check here would pass and none of them would mean anything.")
        return 2

    print("EMBERFALL SUPABASE RULE TEST")
    print("project: " + URL)

    section("SETUP")
    a = sign_up_anonymous("A")
    b = sign_up_anonymous("B")
    check(a["id"] != b["id"], "two distinct anonymous users were created")

    pa, pb = check_profiles(a, b)
    check_saves(a, b)
    check_anon_role()
    if pa and pb:
        check_friendship_and_snapshots(a, b, pa, pb)
    check_email_linking(a)

    print("")
    print("RESULT: " + str(PASSES) + " ok, " + str(FAILURES) + " FAILED")
    print("")
    print("Throwaway users left behind - delete them in Authentication -> Users:")
    print("  A  " + a["id"])
    print("  B  " + b["id"])
    print("Deleting a user cascades to its profile, save, snapshot and friendships.")
    return 1 if FAILURES else 0


if __name__ == "__main__":
    sys.exit(main())
