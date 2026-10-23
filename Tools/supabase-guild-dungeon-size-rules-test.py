#!/usr/bin/env python3
"""Proves migration 0022's rules - the guild dungeon's ladder by guild size and the per-account
cycle cap - against a real Supabase project.

WRITTEN, NOT RUN. Nothing here has been executed against any project; 0022 is not applied. A
sibling of Tools/supabase-guild-rules-test.py, kept apart because that file proves 0004 to 0014
and is red-or-green against what is applied today, while every case here needs 0022.

    Apply Server/supabase/migrations/0004 through 0022 first, in order (0022 refuses to apply
    without 0014). Then:

    SUPABASE_URL=... SUPABASE_ANON_KEY=... python Tools/supabase-guild-dungeon-size-rules-test.py

CREDENTIALS COME FROM THE ENVIRONMENT ONLY: SUPABASE_URL and SUPABASE_ANON_KEY. Unlike its sibling
this script never opens Assets/Resources/Config/supabase.json, so nothing it does reads the
gitignored config. THE KEY IS NEVER PRINTED; the project url is. Only the ANON PUBLIC key is
accepted, and the guard that refuses a service-role key is the sibling's, shown to fire before it
is trusted: the service role bypasses RLS, so every check would pass and none would mean anything.

WHAT IT COSTS. The ladder case needs a guild of ten at the moment its cycle opens, so a run
creates about fourteen throwaway accounts, every one tagged `gs-<run>-<who>` in display_name.
Supabase's default limit is 30 anonymous sign-ins per hour per IP, so one run fits and two in the
same hour may stall in SETUP. At the end the script disbands every guild it created and prints ONE
SQL statement that deletes exactly the users it made, by id; deleting a user needs the service
role, which this script deliberately does not hold.

ONE TIMING CAVEAT. The snapshot is taken the first time anybody asks for a guild's cycle. Every
guild here is created fresh and filled BEFORE its first status call, so the ladder it reads is the
one this script built. A run that straddles the weekly roll (Monday 00:00 UTC) sees a new cycle
mid-run and its counts will not line up; re-run.

Standard library only.
"""

import json
import os
import random
import re
import string
import sys
import threading
import time
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCALE_JSON = os.path.join(ROOT, "Assets", "Resources", "Content", "guild-dungeon-scale.json")
BALANCE_JSON = os.path.join(ROOT, "Assets", "Resources", "Content", "balance.json")

URL = ""
ANON = ""
RUN_TAG = "".join(random.choice("23456789abcdefghjkmnpqrstvwxyz") for _ in range(5))
PASSES = 0
FAILURES = 0
CREATED = []
CREATED_LOCK = threading.Lock()

DUNGEON_LINE = [{"defId": "kael_the_unyielding", "level": 40, "stars": 5}]
CONTENT_VERSION = "c1-0000000000000000"


# ---------------------------------------------------------------- the key guard (the sibling's, verbatim in behaviour)


def key_kind(key):
    """"anon" for a publishable key or a JWT whose decoded payload says role anon; "secret" for an
    sb_secret_ key, any other JWT role, or a JWT that will not decode; "unknown" otherwise. Only
    "anon" may run."""
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


# ---------------------------------------------------------------- reporting and transport


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


def load_credentials():
    global URL, ANON
    url = os.environ.get("SUPABASE_URL", "").strip()
    anon = os.environ.get("SUPABASE_ANON_KEY", "").strip()
    if not url or not anon:
        print("FATAL: set SUPABASE_URL and SUPABASE_ANON_KEY. This script reads no config file.")
        return False
    URL, ANON = url.rstrip("/"), anon
    print("credentials: from the environment (the key is never printed)")
    return True


def request(method, path, token=None, body=None, prefer=None, timeout=30):
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
    if isinstance(body, dict):
        return body.get("message") or body.get("error_description") or body.get("msg") or str(body)
    return str(body)


# ---------------------------------------------------------------- the client's copy of the numbers


def read_jsonc(path):
    """The content files carry // comments, which json does not; strip whole-line and trailing
    ones outside strings the simple way the files are written (no // inside any string value)."""
    with open(path, "r", encoding="utf-8-sig") as handle:
        text = "\n".join(re.sub(r"(^|\s)//.*$", "", line) for line in handle.read().splitlines())
    text = re.sub(r",(\s*[}\]])", r"\1", text)
    return json.loads(text)


def client_ladders():
    """[(members, floors)] from guild-dungeon-scale.json, ascending."""
    return [(int(r["members"]), int(r["floors"])) for r in read_jsonc(SCALE_JSON)["ladders"]]


def client_floors_for(members, ladders):
    floors = ladders[0][1]
    for at, f in ladders:
        if members >= at:
            floors = f
    return floors


def client_caps():
    """(per floor, base ladder) from balance.json, which is what the cycle cap is derived from."""
    with open(BALANCE_JSON, "r", encoding="utf-8-sig") as handle:
        text = handle.read()
    per_floor = int(re.search(r'"guildDungeonAttemptsPerFloor"\s*:\s*(\d+)', text).group(1))
    floors = int(re.search(r'"guildDungeonFloors"\s*:\s*(\d+)', text).group(1))
    return per_floor, floors


# ---------------------------------------------------------------- users and guilds


def sign_up(who, attempts=6, max_delay=20.0):
    delay = 2.0
    for _ in range(attempts):
        status, body = request("POST", "/auth/v1/signup", body={})
        if status in (200, 201) and isinstance(body, dict) and "access_token" in body:
            user = {"name": who, "token": body["access_token"], "id": body["user"]["id"]}
            with CREATED_LOCK:
                CREATED.append(user["id"])
            request("PATCH", "/rest/v1/profiles?user_id=eq." + user["id"], token=user["token"],
                    body={"display_name": ("gs-" + RUN_TAG + "-" + who)[:24]})
            return user
        if status == 429:
            time.sleep(delay)
            delay = min(delay * 2, max_delay)
            continue
        return None
    return None


def must_sign_up(who):
    user = sign_up(who)
    if user is None:
        print("  FATAL  could not create the throwaway user '" + who + "' (auth rate limit, or anonymous")
        print("         sign-ins are off). Dashboard -> Authentication -> Rate Limits / Providers.")
        sys.exit(2)
    return user


def guild_name(prefix):
    return (prefix + "-" + RUN_TAG + "-" + "".join(random.choice(string.digits) for _ in range(3)))[:20]


def my_membership(user):
    status, body = request("GET", "/rest/v1/guild_members?select=guild_id,role&user_id=eq." + user["id"],
                           token=user["token"])
    return body[0] if status == 200 and isinstance(body, list) and body else None


def open_guild(leader, prefix):
    """A fresh guild, opened so a request admits on arrival. Its cycle is NOT opened here: nobody
    calls guild_dungeon_status until the caller has filled it."""
    status, body = rpc("guild_create", token=leader["token"], p_name=guild_name(prefix), p_description="")
    guild = body if status == 200 and isinstance(body, dict) else None
    if guild is None:
        return None
    request("PATCH", "/rest/v1/guilds?id=eq." + guild["id"], token=leader["token"], body={"is_open": True})
    return guild


def join(user, guild):
    """Find the code, then ask (0014: only a found code may be asked for). Open guild: admitted."""
    rpc("guild_by_code", token=user["token"], p_code=guild["guild_code"])
    rpc("guild_request_send", token=user["token"], p_code=guild["guild_code"])
    membership = my_membership(user)
    return membership is not None and membership["guild_id"] == guild["id"]


def leave(user):
    status, _ = request("DELETE", "/rest/v1/guild_members?user_id=eq." + user["id"], token=user["token"])
    return status in (200, 204) and my_membership(user) is None


def status_of(user):
    status, body = rpc("guild_dungeon_status", token=user["token"])
    return body[0] if status == 200 and isinstance(body, list) and len(body) == 1 else None


def seed_for(guild_id, cycle):
    hashed = 2166136261
    for byte in ((guild_id or "").strip().lower() + ":" + str(int(cycle))).encode("utf-8"):
        hashed = ((hashed ^ byte) * 16777619) & 0xFFFFFFFF
    return hashed & 0x7FFFFFFF


def submit(user, guild_id, cycle, floor, victory=True):
    args = {"p_cycle": cycle, "p_floor": floor, "p_seed": seed_for(guild_id, cycle), "p_team": DUNGEON_LINE,
            "p_victory": victory, "p_turns": 9, "p_power": 12345, "p_avg_stars": 5.0,
            "p_fallen": 0 if victory else 1, "p_content_version": CONTENT_VERSION}
    return rpc("guild_dungeon_submit", token=user["token"], **args)


def cycle_rows(user, guild_id):
    status, body = request("GET", "/rest/v1/guild_dungeon_cycles?select=guild_id,cycle,member_count,floor_count"
                                  "&guild_id=eq." + guild_id, token=user["token"])
    return body if status == 200 and isinstance(body, list) else None


# ---------------------------------------------------------------- the cases


def check_constants(a, ladders, per_floor, base):
    section("THE CONSTANTS - the server's ladder table and caps are the client's")
    for members in sorted({1, 9, 10, 14, 15, 20, 25, 29, 30, 31} | {m for m, _ in ladders}):
        status, body = rpc("guild_dungeon_floors_for", token=a["token"], p_members=members)
        want = client_floors_for(members, ladders)
        check(status == 200 and body == want, "guild_dungeon_floors_for(" + str(members) + ") is " + str(want)
              + ", as guild-dungeon-scale.json says", (status, body))
    status, body = rpc("guild_dungeon_floor_max", token=a["token"])
    check(status == 200 and body == max(f for _, f in ladders), "the longest ladder is the table's longest", (status, body))
    status, body = rpc("guild_dungeon_cycle_attempt_cap", token=a["token"])
    check(status == 200 and body == per_floor * base,
          "the cycle cap is " + str(per_floor * base) + ": three a floor times the base ladder", (status, body))
    status, body = rpc("guild_dungeon_floors_for", p_members=30)
    check(status not in (200, 201), "the anon key cannot call guild_dungeon_floors_for", (status, message_of(body)))


def check_small_guild(lead, mate, outsider, ladders, per_floor, base):
    """Returns (guild, cycle)."""
    section("A SMALL GUILD - the base ladder, a snapshot that does not move, and the ladder as a wall")
    guild = open_guild(lead, "Small")
    check(guild is not None, "a fresh guild of one")
    if guild is None:
        return None, None
    row = status_of(lead)
    check(row is not None, "its leader asks where the dungeon stands", row)
    if row is None:
        return guild, None
    cycle = row["cycle"]
    check(row["floor_count"] == client_floors_for(1, ladders) == base and row["ladder_members"] == 1,
          "FIVE FLOORS, SET AT ONE MEMBER - nothing taken from a small guild", row)
    check(row["cycle_attempt_cap"] == per_floor * base and row["attempts_used"] == 0 and row["attempt_cap"] == per_floor,
          "fifteen attempts this cycle, none used, three a floor", row)

    rows = cycle_rows(lead, guild["id"])
    check(rows is not None and len(rows) == 1 and rows[0]["member_count"] == 1 and rows[0]["floor_count"] == base,
          "the snapshot row is there and the guild's own member reads it", rows)
    rows = cycle_rows(outsider, guild["id"])
    check(rows is not None and len(rows) == 0, "SOMEBODY IN NO GUILD READS NONE OF IT - zero rows", rows)

    for method, body in (("POST", {"guild_id": guild["id"], "cycle": cycle + 1, "member_count": 30, "floor_count": 10}),
                         ("PATCH", {"floor_count": 10}), ("DELETE", None)):
        path = "/rest/v1/guild_dungeon_cycles" + ("" if method == "POST" else "?guild_id=eq." + guild["id"])
        status, answer = request(method, path, token=lead["token"], body=body)
        after = cycle_rows(lead, guild["id"])
        kept = after is not None and len(after) == 1 and after[0]["floor_count"] == base
        check(status not in (200, 201, 204) or kept,
              "A MEMBER CANNOT " + method + " A LADDER - no client role writes guild_dungeon_cycles", (status, message_of(answer)))
    status, body = rpc("guild_dungeon_cycle_floors", token=lead["token"], p_guild=guild["id"], p_cycle=cycle + 1)
    check(status not in (200, 201), "and cannot call the snapshot writer", (status, message_of(body)))

    check(join(mate, guild), "a second member joins MID-CYCLE")
    row = status_of(mate)
    check(row is not None and row["ladder_members"] == 1 and row["floor_count"] == base,
          "THE LADDER DOES NOT MOVE MID-CYCLE - still set at one member", row)

    for floor in range(1, base + 1):
        status, body = submit(lead, guild["id"], cycle, floor)
        check(status == 200, "the leader clears floor " + str(floor), (status, message_of(body)))
    status, body = submit(mate, guild["id"], cycle, base + 1)
    check(status == 400, "FLOOR " + str(base + 1) + " IS PAST THIS GUILD'S LADDER - refused with 400 though floor "
          + str(base) + " is cleared", (status, message_of(body)))
    status, body = submit(mate, guild["id"], cycle, max(f for _, f in ladders) + 1)
    check(status == 400, "and a floor past the longest ladder exists for nobody", (status, message_of(body)))
    return guild, cycle


def check_big_guild(lead, crowd, small_guild, ladders, per_floor, base):
    section("A GUILD OF TEN - a longer ladder, the same fifteen attempts, and the cap under contention")
    guild = open_guild(lead, "Ten")
    check(guild is not None, "a fresh guild")
    if guild is None:
        return
    joined = sum(1 for user in crowd if join(user, guild))
    check(joined == len(crowd), str(joined + 1) + " members BEFORE anybody opens the cycle", joined)
    members = joined + 1
    want = client_floors_for(members, ladders)

    row = status_of(lead)
    check(row is not None and row["ladder_members"] == members and row["floor_count"] == want and want > base,
          str(want) + " FLOORS, SET AT " + str(members) + " MEMBERS - a bigger guild clears more", row)
    if row is None:
        return
    cycle = row["cycle"]
    spender, racer, mate = crowd[0], crowd[1], crowd[2]

    # The spender clears floors 1 to base three times each: fifteen, every one a clear.
    for floor in range(1, base + 1):
        for _ in range(per_floor):
            status, body = submit(spender, guild["id"], cycle, floor)
            if status != 200:
                check(False, "the spender's attempt at floor " + str(floor) + " is accepted", (status, message_of(body)))
    status, body = request("GET", "/rest/v1/guild_dungeon_attempts?select=cycle_attempt&user_id=eq." + spender["id"]
                                  + "&order=cycle_attempt.asc", token=spender["token"])
    numbers = [r["cycle_attempt"] for r in body] if status == 200 and isinstance(body, list) else None
    check(numbers == list(range(1, per_floor * base + 1)),
          "the spender's fifteen are numbered 1 to 15 within the cycle - no gap, no repeat", numbers)
    status, body = submit(spender, guild["id"], cycle, base + 1)
    check(status == 409, "A SIXTEENTH IS REFUSED - floor " + str(base + 1) + " is open and the floor has three left, "
          "but the cycle has none", (status, message_of(body)))
    row = status_of(spender)
    check(row is not None and row["attempts_used"] == per_floor * base, "status says fifteen of fifteen", row)
    status, body = submit(mate, guild["id"], cycle, base + 1)
    check(status == 200, "while a guildmate with attempts left takes floor " + str(base + 1), (status, message_of(body)))

    # The racer spends fourteen, then fires three at once at a floor with three left: the cycle
    # allows one.
    spent = 0
    for floor in range(1, base + 1):
        for _ in range(per_floor if floor < base else per_floor - 1):
            status, _ = submit(racer, guild["id"], cycle, floor)
            spent += 1 if status == 200 else 0
    check(spent == per_floor * base - 1, "the racer has spent " + str(spent) + " of " + str(per_floor * base), spent)
    barrier = threading.Barrier(3)
    results = [None] * 3

    def race(index):
        try:
            barrier.wait(timeout=30)
        except threading.BrokenBarrierError:
            pass
        results[index] = submit(racer, guild["id"], cycle, base + 1)

    with ThreadPoolExecutor(max_workers=3) as pool:
        list(pool.map(race, range(3)))
    accepted = [r for r in results if r and r[0] == 200]
    refused = [r for r in results if r and r[0] == 409]
    check(len(accepted) == 1 and len(refused) == 2,
          "THREE AT ONCE WITH ONE LEFT IN THE CYCLE: EXACTLY ONE IS STORED", [r[0] for r in results if r])

    # The spender changes guild: the account's budget goes with it.
    check(leave(spender), "the spender leaves the guild of ten")
    check(join(spender, small_guild), "and joins the small guild")
    row = status_of(spender)
    check(row is not None and row["attempts_used"] == per_floor * base,
          "THE BUDGET IS THE ACCOUNT'S: status in the new guild still says fifteen used", row)
    if row is not None:
        status, body = submit(spender, small_guild["id"], row["cycle"], 1)
        check(status == 409, "and floor 1 of the new guild is refused for the cycle", (status, message_of(body)))
    row = status_of(lead)
    check(row is not None and row["ladder_members"] == members and row["floor_count"] == want,
          "the guild of ten keeps its " + str(want) + " floors after a member leaves - fixed for the cycle", row)


def disband_everything(users):
    for user in users:
        membership = my_membership(user)
        if membership and membership.get("role") == "leader":
            request("DELETE", "/rest/v1/guilds?id=eq." + membership["guild_id"], token=user["token"])


def print_cleanup_statement():
    print("")
    print("CLEANUP - paste this ONE statement into the Supabase SQL editor (deleting users needs the")
    print("service role, which this script does not hold):")
    print("")
    if not CREATED:
        print("-- this run created no users")
        return
    print("delete from auth.users where id in (")
    for index, uid in enumerate(CREATED):
        print("  '" + uid + "'" + ("," if index < len(CREATED) - 1 else ""))
    print(");")


def main():
    if not load_credentials():
        return 2
    broken = guard_fires()
    if broken:
        print("The key guard does not work (expected, got): " + str(broken) + ". Not running.")
        return 2
    if key_kind(ANON) != "anon":
        print("That key is not an anon public key (it reads as: " + key_kind(ANON) + "). Not running.")
        return 2

    ladders = client_ladders()
    per_floor, base = client_caps()
    print("EMBERFALL GUILD DUNGEON SIZE RULE TEST (0022)")
    print("project: " + URL)
    print("run tag: gs-" + RUN_TAG + "-*")

    cast = []
    try:
        section("SETUP")
        small_lead, mate, outsider, big_lead = (must_sign_up(w) for w in ("small", "mate", "out", "big"))
        need = ladders[1][0] - 1 if len(ladders) > 1 else 9
        crowd = [must_sign_up("c" + str(i)) for i in range(need)]
        cast = [small_lead, mate, outsider, big_lead] + crowd
        check(len({u["id"] for u in cast}) == len(cast), str(len(cast)) + " distinct throwaway users")

        check_constants(small_lead, ladders, per_floor, base)
        small_guild, _ = check_small_guild(small_lead, mate, outsider, ladders, per_floor, base)
        if small_guild is not None:
            check_big_guild(big_lead, crowd, small_guild, ladders, per_floor, base)
    finally:
        disband_everything(cast)
        print_cleanup_statement()
        print("")
        print("RESULT: " + str(PASSES) + " passed, " + str(FAILURES) + " failed")
    return 0 if FAILURES == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
