#!/usr/bin/env python3
"""Proves the leaderboard rules (migration 0023) against a real Supabase project.

Server/supabase/migrations/0023_leaderboards.sql says the client never sends a score: leaderboard_submit()
reads the caller's own cloud save, checks it against itself, against ceilings and against the time the
server has seen pass, stamps everything with the server's clock and stores only what passed. This script
asks the live database whether that is true. Like Tools/supabase-rules-test.py it works WITHOUT the game
client, as throwaway anonymous users, with the anon public key only.

    Apply 0001 to 0023 first (in number order, in the SQL editor; 0023 itself needs only 0013). Against a project without 0023 the first
    call answers 404, which is reported as one FAIL and the run stops.

    python Tools/supabase-leaderboard-rules-test.py           # every case, about two minutes
    python Tools/supabase-leaderboard-rules-test.py --quick   # skips the two cases that wait out the
                                                              # submission interval (61 s each)

CREDENTIALS. The same as Tools/supabase-rules-test.py, read the same way, by the same function:
Assets/Resources/Config/supabase.json, with SUPABASE_URL and SUPABASE_ANON_KEY each overriding its own
field when set. THE KEY IS NEVER PRINTED. Only the anon key runs; a service-role or secret key is refused
by the same decoded-key guard, proven on every run before it is trusted.

THE SAVE VERSION. push_save refuses a save_version above save_limits.max_save_version (0013). The script
tries SaveSerializer.CurrentVersion first and steps down to 20 (0023's min_save_version) until the server
takes one, and prints which it used. If the server takes none, raise save_limits first.

THE RUN IS RECORDED in Tools/SupabaseRuns/leaderboards.json (gitignored): when, at which commit, and the
pass and fail counts.

WHAT IT LEAVES BEHIND: about twenty anonymous users, with their profiles, saves and leaderboard rows. It
prints every user id at the end for deletion in Authentication -> Users; the cascades take the rest.

Standard library only.
"""

import datetime
import json
import os
import random
import re
import sys
import time
import urllib.error
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG_PATH = os.path.join(ROOT, "Assets", "Resources", "Config", "supabase.json")
RUN_RECORD = os.path.join(ROOT, "Tools", "SupabaseRuns", "leaderboards.json")
SAVE_SERIALIZER = os.path.join(ROOT, "Assets", "Scripts", "Core", "Save", "SaveSerializer.cs")

URL = ""
ANON = ""
PASSES = 0
FAILURES = 0
USERS = []
SAVE_VERSION = None

TABLES = ("leaderboard_limits", "leaderboard_entries", "leaderboard_recital_weeks",
          "leaderboard_refusals", "leaderboard_bans")


# ---------------------------------------------------------------- the key guard (as Tools/supabase-rules-test.py)


def key_kind(key):
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
    import base64

    def jwt(role):
        def b64(text):
            return base64.urlsafe_b64encode(text.encode("utf-8")).decode("ascii").rstrip("=")
        return (b64('{"alg":"HS256","typ":"JWT"}') + "." + b64('{"iss":"supabase","role":"' + role + '"}')
                + "." + b64("signature"))

    cases = ((jwt("service_role"), "secret"), (jwt("authenticated"), "secret"), (jwt("anon"), "anon"),
             ("sb_secret_" + "x" * 32, "secret"), ("sb_publishable_" + "x" * 32, "anon"),
             ("a.!!!notbase64!!!.c", "secret"), ("plain-text-key", "unknown"), ("", "unknown"))
    return [(expected, key_kind(key)) for key, expected in cases if key_kind(key) != expected]


# ---------------------------------------------------------------- reporting, credentials, the run record


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


def load_credentials():
    """supabase.json first, then SUPABASE_URL and SUPABASE_ANON_KEY, each overriding its own field.
    Never prints the key. The same function as Tools/supabase-rules-test.py; keep them so."""
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


def user(who):
    status, body = request("POST", "/auth/v1/signup", body={})
    if status not in (200, 201) or not isinstance(body, dict) or "access_token" not in body:
        print("  FATAL  could not create an anonymous user for " + who + ": " + str(status) + " " + str(body))
        print("         Is 'Allow anonymous sign-ins' on in Authentication -> Sign In / Providers?")
        sys.exit(2)
    u = {"name": who, "token": body["access_token"], "id": body["user"]["id"], "revision": 0}
    USERS.append(u)
    return u


def current_save_version():
    try:
        with open(SAVE_SERIALIZER, "r", encoding="utf-8-sig") as handle:
            found = re.search(r"public\s+const\s+int\s+CurrentVersion\s*=\s*(\d+)\s*;", handle.read())
    except OSError:
        return None
    return int(found.group(1)) if found else None


def stages(n, chapter_first=1):
    """The first n stage ids in campaign order (twelve a chapter), as SaveSerializer writes clearedStages."""
    out = []
    ch, st = chapter_first, 1
    while len(out) < n:
        out.append("ch" + str(ch) + "_" + str(st))
        st += 1
        if st > 12:
            ch, st = ch + 1, 1
    return out


def save(cleared, recital=0, proving=0, standing=None, attempts=None, current=None, **extra):
    """A document shaped like the fields 0023 reads. Nothing else in it matters to the server."""
    doc = {"version": SAVE_VERSION, "playerName": "Leaderboard Test", "clearedStages": cleared,
           "recitalHighestFloor": recital, "recitalCurrentFloor": recital + 1 if current is None else current,
           "provingHighestStanding": proving, "provingStanding": proving if standing is None else standing,
           "provingAttempts": (proving + 2) // 3 if attempts is None else attempts}
    doc.update(extra)
    return doc


def push(u, doc):
    status, body = request("POST", "/rest/v1/rpc/push_save", token=u["token"],
                           body={"p_expected_revision": u["revision"], "p_save_version": SAVE_VERSION,
                                 "p_device_id": "leaderboard-test", "p_data": doc})
    if status == 200 and isinstance(body, dict):
        u["revision"] = body.get("revision", u["revision"] + 1)
    return status, body


def submit(u, body=None):
    return request("POST", "/rest/v1/rpc/leaderboard_submit", token=u["token"], body=body if body is not None else {})


def top(u, board, limit=50):
    return request("POST", "/rest/v1/rpc/leaderboard_top", token=u["token"], body={"p_board": board, "p_limit": limit})


def verdict(body, board):
    return body.get(board, {}) if isinstance(body, dict) else {}


def fresh_submit(who, doc):
    """A new user, one push, one submit: the shape of most cases, since a user may submit once a minute."""
    u = user(who)
    status, body = push(u, doc)
    if status != 200:
        return u, status, body
    status, body = submit(u)
    return u, status, body


# ---------------------------------------------------------------- the cases


def find_save_version():
    global SAVE_VERSION
    section("SETUP - a save version this server takes")
    probe = user("version-probe")
    top_version = current_save_version() or 20
    for v in range(top_version, 19, -1):
        SAVE_VERSION = v
        status, body = push(probe, save(stages(1)))
        if status == 200:
            check(True, "push_save takes save_version " + str(v) + (" (SaveSerializer.CurrentVersion)" if v == top_version
                                                                    else " (CurrentVersion " + str(top_version) + " is above save_limits; raise it before shipping)"))
            return probe
        if status != 400:
            break
    check(False, "push_save takes some save_version from " + str(top_version) + " down to 20", (status, body))
    return None


def check_probe(probe):
    section("0023 IS APPLIED")
    status, body = submit(probe)
    missing = status == 404 and isinstance(body, dict) and body.get("code") in ("PGRST202", "42883")
    check(not missing, "leaderboard_submit exists (otherwise apply 0023: the run stops here)", (status, body))
    if missing:
        return False
    check(status == 200 and verdict(body, "campaign").get("status") == "raised" and verdict(body, "campaign").get("value") == 1,
          "a one-stage save submits and the campaign board is raised to 1", (status, body))
    return True


def check_closed_doors(a):
    section("NO CLIENT DOOR BUT THE TWO FUNCTIONS - no table readable or writable, nothing for anon")
    for table in TABLES:
        for token, who in ((None, "anon"), (a["token"], "a signed-in user")):
            status, body = request("GET", "/rest/v1/" + table + "?select=*&limit=1", token=token)
            readable = status == 200 and isinstance(body, list) and len(body) > 0
            check(not readable and status != 200, who + " cannot SELECT " + table, (status, body))
        status, body = request("POST", "/rest/v1/" + table, token=a["token"], body={"user_id": a["id"]}, prefer="return=minimal")
        check(status not in (200, 201, 204), "a signed-in user cannot INSERT into " + table, (status, body))
        status, body = request("PATCH", "/rest/v1/" + table + "?user_id=eq." + a["id"], token=a["token"], body={"reason": "x"})
        check(status not in (200, 204), "nor UPDATE it", (status, body))
        status, body = request("DELETE", "/rest/v1/" + table + "?user_id=eq." + a["id"], token=a["token"])
        check(status not in (200, 204), "nor DELETE from it", (status, body))

    status, body = request("POST", "/rest/v1/rpc/leaderboard_submit", body={})
    check(status in (401, 403, 404), "the anon role cannot call leaderboard_submit", (status, body))
    status, body = request("POST", "/rest/v1/rpc/leaderboard_top", body={"p_board": "campaign"})
    check(status in (401, 403, 404), "nor leaderboard_top", (status, body))
    helpers = (("leaderboard_week_start", {"p_at": "2026-10-22T00:00:00Z", "p_hour": 4}),
               ("leaderboard_int", {"p_doc": {"x": 1}, "p_key": "x"}))
    for name, args in helpers:
        status, body = request("POST", "/rest/v1/rpc/" + name, token=a["token"], body=args)
        check(status in (401, 403, 404), "the helper " + name + " is not callable by a signed-in user", (status, body))

    status, body = submit(a, {"p_value": 999})
    check(status in (400, 404), "THE CLIENT CANNOT SEND A SCORE: leaderboard_submit takes no argument, so one with a value is no function", (status, body))


def check_submit_basics():
    section("SUBMIT - no save, the interval, the server's clock")
    nobody = user("no-save")
    status, body = submit(nobody)
    check(status == 404, "an account with no cloud save is told to push first (404)", (status, body))

    u, status, body = fresh_submit("clock", save(stages(3)))
    check(status == 200, "a three-stage save submits", (status, body))
    if status == 200:
        server = datetime.datetime.fromisoformat(str(body.get("server_time")).replace("Z", "+00:00"))
        week = datetime.datetime.fromisoformat(str(body.get("week_start")).replace("Z", "+00:00"))
        here = datetime.datetime.now(datetime.timezone.utc)
        check(abs((server - here).total_seconds()) < 600, "server_time is the server's own clock, within ten minutes of this machine's", body.get("server_time"))
        check(week.astimezone(datetime.timezone.utc).weekday() == 0 and week.astimezone(datetime.timezone.utc).hour == 4
              and week <= server < week + datetime.timedelta(days=7),
              "week_start is the Monday at 04:00 UTC this week began, the turnover of DailyLoop.WeekOf", body.get("week_start"))
    status, body = submit(u)
    check(status == 429, "A SECOND SUBMISSION INSIDE THE INTERVAL IS REFUSED (429), before anything is read", (status, body))


def check_campaign():
    section("CAMPAIGN - the ids are checked one by one, and the rise against the time played")
    _, status, body = fresh_submit("camp-dup", save(stages(4) + ["ch1_2"]))
    check(verdict(body, "campaign").get("status") == "refused" and "twice" in verdict(body, "campaign").get("reason", ""),
          "a stage named twice is refused", (status, body))
    _, status, body = fresh_submit("camp-junk", save(stages(4) + ["not_a_stage"]))
    check(verdict(body, "campaign").get("status") == "refused", "an element that is not a stage id is refused", (status, body))
    _, status, body = fresh_submit("camp-number", save(stages(4) + [5]))
    check(verdict(body, "campaign").get("status") == "refused", "a number in the list is refused", (status, body))
    _, status, body = fresh_submit("camp-many", save(stages(73)))
    check(verdict(body, "campaign").get("status") == "refused" and "more stages" in verdict(body, "campaign").get("reason", ""),
          "73 stages, one more than the campaign has, is refused", (status, body))
    _, status, body = fresh_submit("camp-fast", save(stages(40)))
    check(verdict(body, "campaign").get("status") == "refused" and verdict(body, "campaign").get("reason") == "faster than play",
          "40 stages on an account minutes old is FASTER THAN PLAY and refused", (status, body))
    _, status, body = fresh_submit("camp-ok", save(stages(12)))
    check(verdict(body, "campaign").get("status") == "raised" and verdict(body, "campaign").get("value") == 12,
          "12 stages (the burst) on a new account is taken", (status, body))


def check_proving():
    section("PROVING - the standing against the attempts and the unlock the same save holds")
    through = stages(12)  # chapter 1 only: the Proving opens at 2-4
    opened = stages(16)   # through ch2_4
    _, status, body = fresh_submit("pv-locked", save(through, proving=9, attempts=3))
    check(verdict(body, "proving").get("status") == "refused" and "unlocked" in verdict(body, "proving").get("reason", ""),
          "a standing with 2-4 not cleared is refused", (status, body))
    _, status, body = fresh_submit("pv-above", save(opened, proving=3, standing=6, attempts=5))
    check(verdict(body, "proving").get("status") == "refused", "a standing above the highest standing is refused", (status, body))
    _, status, body = fresh_submit("pv-attempts", save(opened, proving=30, attempts=2))
    check(verdict(body, "proving").get("status") == "refused" and "attempts" in verdict(body, "proving").get("reason", ""),
          "30 standing from 2 attempts (at 3 a win) is refused", (status, body))
    _, status, body = fresh_submit("pv-string", save(opened, provingHighestStanding="9"))
    check(verdict(body, "proving").get("status") == "refused" and "malformed" in verdict(body, "proving").get("reason", ""),
          "a standing written as a string is malformed, never read as a number", (status, body))
    _, status, body = fresh_submit("pv-fraction", save(opened, provingHighestStanding=4.5, provingStanding=0, provingAttempts=9))
    check(verdict(body, "proving").get("status") == "refused", "a fractional standing is malformed", (status, body))
    _, status, body = fresh_submit("pv-fast", save(opened, proving=60, attempts=20))
    check(verdict(body, "proving").get("status") == "refused" and verdict(body, "proving").get("reason") == "faster than play",
          "60 standing on a new account (burst 20) is faster than play", (status, body))
    _, status, body = fresh_submit("pv-ok", save(opened, proving=9, attempts=3))
    check(verdict(body, "proving").get("status") == "raised" and verdict(body, "proving").get("value") == 9,
          "9 standing from 3 attempts with 2-4 cleared is taken", (status, body))
    check(verdict(body, "campaign").get("status") == "refused",
          "and in the same call the 16-stage campaign (over the burst of 12) is refused on its own: the boards are judged apart", body)


def check_recital():
    section("RECITAL - the floor against the current floor and the unlock")
    opened = stages(20)  # through ch2_8
    _, status, body = fresh_submit("rc-locked", save(stages(12), recital=5))
    check(verdict(body, "recital").get("status") == "refused" and "unlocked" in verdict(body, "recital").get("reason", ""),
          "a floor with 2-8 not cleared is refused", (status, body))
    _, status, body = fresh_submit("rc-current", save(opened, recital=5, current=5))
    check(verdict(body, "recital").get("status") == "refused" and "current floor" in verdict(body, "recital").get("reason", ""),
          "a current floor that is not the floor after the highest is refused", (status, body))
    _, status, body = fresh_submit("rc-fast", save(opened, recital=50))
    check(verdict(body, "recital").get("status") == "refused" and verdict(body, "recital").get("reason") == "faster than play",
          "floor 50 on a new account (burst 10) is faster than play", (status, body))
    _, status, body = fresh_submit("rc-negative", save(opened, recitalHighestFloor=-3, recitalCurrentFloor=1))
    check(verdict(body, "recital").get("status") == "refused" and "malformed" in verdict(body, "recital").get("reason", ""),
          "a negative floor is malformed", (status, body))
    u, status, body = fresh_submit("rc-ok", save(opened, recital=8))
    check(verdict(body, "recital").get("status") == "raised" and verdict(body, "recital").get("value") == 8,
          "floor 8 with 2-8 cleared is taken", (status, body))
    return u


def check_boards(watcher, climber):
    section("READING A BOARD - ranks, names, values, is_me, and never a user id")
    status, body = top(watcher, "no_such_board")
    check(status == 400, "a board name that does not exist is refused (400)", (status, body))
    status, body = top(climber, "recital", 10)
    rows = body if status == 200 and isinstance(body, list) else []
    check(status == 200 and len(rows) > 0, "the all-time Recital board reads", (status, body))
    check(all(set(r.keys()) == {"rank", "display_name", "value", "is_me"} for r in rows),
          "every row is exactly rank, display_name, value, is_me: no user id, no revision, no time", rows[:2])
    mine = [r for r in rows if r.get("is_me")]
    check(len(mine) == 1 and mine[0].get("value") == 8, "the caller's own row is there once, marked is_me, at floor 8", mine)
    check(all(rows[i]["rank"] <= rows[i + 1]["rank"] for i in range(len(rows) - 1)), "rows come best first", [r["rank"] for r in rows])
    status, body = top(watcher, "campaign", 1000)
    check(status == 200 and isinstance(body, list) and len(body) <= 101, "a limit of 1000 is held to 100 rows (and the caller's own)", (status, len(body) if isinstance(body, list) else body))
    status, body = top(climber, "recital_week", 100)
    week = [r for r in body if r.get("is_me")] if status == 200 and isinstance(body, list) else []
    check(len(week) == 1 and week[0].get("value") == 8, "THE WEEKLY BOARD: the climber is on this week's board at floor 8", (status, week))
    status, body = top(climber, "recital_week_climb", 100)
    climb = [r for r in body if r.get("is_me")] if status == 200 and isinstance(body, list) else None
    check(climb == [], "and NOT on the climb board yet: a first submission counts its floor as where the week started", (status, climb))


def check_slow(climber):
    section("OVER TIME - an older save keeps the board, a climb shows on the week's climb board (waits 2 x 61 s)")
    time.sleep(61)
    push(climber, save(stages(20), recital=4))
    status, body = submit(climber)
    check(status == 200 and verdict(body, "recital").get("status") == "kept" and verdict(body, "recital").get("value") == 8,
          "a save from an older device (floor 4) is KEPT at 8: a board never goes down", (status, body))
    time.sleep(61)
    push(climber, save(stages(20), recital=11))
    status, body = submit(climber)
    check(status == 200 and verdict(body, "recital").get("status") == "raised" and verdict(body, "recital").get("value") == 11,
          "floor 11 a minute later (8 + burst 10 allows it) is raised", (status, body))
    status, body = top(climber, "recital_week_climb", 100)
    climb = [r for r in body if r.get("is_me")] if status == 200 and isinstance(body, list) else []
    check(len(climb) == 1 and climb[0].get("value") == 3, "and the week's climb board shows 3 floors climbed (8 to 11)", (status, climb))


# ---------------------------------------------------------------- entry point


def main():
    quick = "--quick" in sys.argv[1:]
    if not load_credentials():
        return 2
    broken = guard_fires()
    if broken:
        print("The key guard does not work (expected, got): " + str(broken) + ". Not running.")
        return 2
    if key_kind(ANON) != "anon":
        print("That key is not an anon public key (it reads as: " + key_kind(ANON) + "). Use the ANON PUBLIC key.")
        return 2
    print("key: decoded as an anon public key, and the guard was shown to fire on built secret keys")
    print("EMBERFALL LEADERBOARD RULE TEST (0023)")
    print("project: " + URL)

    probe = find_save_version()
    if probe is not None and check_probe(probe):
        check_closed_doors(probe)
        check_submit_basics()
        check_campaign()
        check_proving()
        climber = check_recital()
        check_boards(probe, climber)
        if quick:
            print("")
            print("--quick: the two cases that wait out the submission interval were skipped")
        else:
            check_slow(climber)

    print("")
    print("RESULT: " + str(PASSES) + " ok, " + str(FAILURES) + " FAILED")
    print("")
    print("Throwaway users left behind - delete them in Authentication -> Users (the cascades take the rest):")
    for u in USERS:
        print("  " + u["name"] + "  " + u["id"])
    record_run(PASSES, FAILURES)
    return 1 if FAILURES else 0


if __name__ == "__main__":
    sys.exit(main())
