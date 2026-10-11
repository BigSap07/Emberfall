#!/usr/bin/env python3
"""The OTHER player, for a live end-to-end run of the accounts feature in the editor.

Tools/supabase-rules-test.py proves the database's rules with the game client out of the way.
This is the opposite job: it is a second real account on the real project, doing the things a
second player does, so the editor session has somebody to be friends with. Nothing here
asserts anything about the server - it exists so a human (or an agent driving Unity) can watch
the CLIENT do the whole thing against real data.

Every subcommand prints the throwaway user ids it touches, because deleting a user needs the
service role key and this script deliberately does not hold one. Delete them from the
dashboard, Authentication -> Users; the cascades take the profile, save, snapshot and
friendship with them.

    python Tools/accounts-live-drive.py create   --name "Rival"
    python Tools/accounts-live-drive.py befriend --token-file <f> --code ABC234
    python Tools/accounts-live-drive.py publish  --token-file <f> --hero kael_the_unyielding --level 30
    python Tools/accounts-live-drive.py conflict --email <a@b> --password <p>
    python Tools/accounts-live-drive.py guild-ask --token-file <f> --code ABC234
    python Tools/accounts-live-drive.py guild-say --token-file <f> --body "hello from the other side"

CREDENTIALS. Reads SUPABASE_URL and SUPABASE_ANON_KEY when they are set, and otherwise reads
Assets/Resources/Config/supabase.json - the same gitignored file the game reads, so a machine
that can run the game can run this with nothing else set up. IT NEVER PRINTS EITHER VALUE, and
the anon key is the only key used: every request below is either unauthenticated or made as a
user this script created, or as an account whose email and password were passed in. The
service role key bypasses RLS, appears nowhere in this project, and must never be set for this.

The token file is how a created account survives between subcommands. It holds a refresh
token, so it is written to a path the caller chooses and should be somewhere temporary -
TREAT IT AS A CREDENTIAL and do not put it in the repository.

Standard library only.
"""

import argparse
import json
import os
import random
import re
import string
import sys
import urllib.error
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG_PATH = os.path.join(ROOT, "Assets", "Resources", "Config", "supabase.json")
CAMPAIGN_PATH = os.path.join(ROOT, "Assets", "Resources", "Content", "campaign.json")

# SaveSerializer.PreIdStageOrder, the FROZEN order a pre-version-20 `stagesCleared` position is
# read through. Never derived from campaign.json, for the reason the serializer gives.
PRE_ID_STAGE_ORDER = ["ch%d_%d" % (chapter, stage) for chapter in range(1, 7) for stage in range(1, 13)]

URL = ""
ANON = ""


def load_credentials():
    """Env first, then the game's own config file. Neither is ever echoed."""
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


def request(method, path, token=None, body=None, prefer=None):
    """(status, parsed-or-text). Never raises on an HTTP status - a refusal is a result."""
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
        with urllib.request.urlopen(req, timeout=25) as response:
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


def die(message, detail=""):
    print("FATAL: " + message + (("  <- " + str(detail)) if detail else ""))
    sys.exit(2)


def write_token_file(path, session):
    with open(path, "w", encoding="utf-8") as handle:
        json.dump(session, handle)
    print("token file written to " + path + " (treat as a credential)")


def read_token_file(path):
    with open(path, "r", encoding="utf-8") as handle:
        return json.load(handle)


def refresh(session):
    """A stored refresh token back into a live access token. Access tokens last an hour and the
    editor run between two subcommands can take longer than that."""
    status, body = request("POST", "/auth/v1/token?grant_type=refresh_token",
                           body={"refresh_token": session["refresh"]})
    if status != 200 or not isinstance(body, dict) or "access_token" not in body:
        die("could not refresh the throwaway account's session", status)
    session["token"] = body["access_token"]
    session["refresh"] = body.get("refresh_token", session["refresh"])
    return session


# ---------------------------------------------------------------- subcommands


def cmd_create(args):
    """A second anonymous player, named, with a line published."""
    status, body = request("POST", "/auth/v1/signup", body={})
    if status not in (200, 201) or not isinstance(body, dict) or "access_token" not in body:
        die("could not create the throwaway account - is 'Allow anonymous sign-ins' on?", status)

    session = {
        "id": body["user"]["id"],
        "token": body["access_token"],
        "refresh": body.get("refresh_token", ""),
    }

    status, _ = request("PATCH", "/rest/v1/profiles?user_id=eq." + session["id"],
                        token=session["token"], body={"display_name": args.name})
    if status not in (200, 204):
        die("could not set the display name", status)

    status, rows = request(
        "GET",
        "/rest/v1/profiles?select=user_id,display_name,friend_code&user_id=eq." + session["id"],
        token=session["token"])
    if status != 200 or not rows:
        die("the throwaway account has no profile row", status)

    write_token_file(args.token_file, session)
    print("")
    print("THROWAWAY USER ID (delete from the dashboard when done): " + session["id"])
    print("display name: " + rows[0]["display_name"])
    print("friend code:  " + rows[0]["friend_code"])


def cmd_publish(args):
    """A team snapshot for the throwaway account, in the exact shape TeamSnapshotCodec writes.

    Hand-built rather than exported from the game, deliberately: the point of the duel is that a
    line is plain data the simulation cannot tell the origin of, and a snapshot written by a
    python script that has never seen Unity is the strongest available statement of that. If the
    editor can fight this, the format really is the wire format.
    """
    session = refresh(read_token_file(args.token_file))

    units = []
    for index, hero in enumerate(args.hero):
        unit = {"defId": hero, "level": args.level, "stars": args.stars}
        if args.talent:
            unit["selectedTalent"] = args.talent
        units.append(unit)

    body = {
        "user_id": session["id"],
        "label": args.label,
        "power_score": args.power,
        "units": units,
    }
    status, response = request("POST", "/rest/v1/team_snapshots?on_conflict=user_id",
                               token=session["token"], body=body,
                               prefer="resolution=merge-duplicates,return=minimal")
    if status not in (200, 201, 204):
        die("could not publish the snapshot", str(status) + " " + str(response))
    print("published a " + str(len(units)) + "-hero line for " + session["id"])
    print("  " + ", ".join(h + " lv" + str(args.level) for h in args.hero))
    write_token_file(args.token_file, session)


def cmd_befriend(args):
    """Sends a friend request from the throwaway account to a friend code."""
    session = refresh(read_token_file(args.token_file))

    code = args.code.upper().replace("-", "").replace(" ", "")
    status, rows = request("GET",
                           "/rest/v1/profiles?select=user_id,display_name,friend_code&friend_code=eq." + code,
                           token=session["token"])
    if status != 200 or not rows:
        die("no account has the friend code " + code, status)
    target = rows[0]

    status, response = request("POST", "/rest/v1/friendships", token=session["token"],
                               body={"requester": session["id"],
                                     "addressee": target["user_id"],
                                     "status": "pending"})
    if status == 409:
        print("a friendship row already exists between these two - nothing sent")
    elif status not in (200, 201, 204):
        die("could not send the request", str(status) + " " + str(response))
    else:
        print("friend request sent to " + code + " ("
              + (target.get("display_name") or "unnamed") + ", " + target["user_id"] + ")")
    write_token_file(args.token_file, session)


def campaign_stage_ids():
    """Stage ids in campaign.json order. The file carries // comments, so it is read for its
    stage ids rather than parsed; every stage id has the shape chN_M and nothing else does."""
    with open(CAMPAIGN_PATH, "r", encoding="utf-8") as handle:
        return re.findall(r'"id"\s*:\s*"(ch\d+_\d+)"', handle.read())


def clear_more_stages(document, save_version, count):
    """Clears the next `count` stages the way SaveSerializer records them from version 20 on:
    by id in `clearedStages`, with `stagesCleared` written as the count of ids content names.

    A pre-20 document's `stagesCleared` is a position, so its ids are seeded first through the
    frozen order, which is the serializer's own migration. The ids then stay a prefix of the
    campaign, so the position this writes back means the same stages to a pre-20 reader."""
    cleared = [sid for sid in (document.get("clearedStages") or []) if isinstance(sid, str)]
    if save_version < 20:
        position = max(0, min(int(document.get("stagesCleared", 0)), len(PRE_ID_STAGE_ORDER)))
        for sid in PRE_ID_STAGE_ORDER[:position]:
            if sid not in cleared:
                cleared.append(sid)
    stages = campaign_stage_ids()
    cleared.extend([sid for sid in stages if sid not in cleared][:count])
    document["clearedStages"] = cleared
    document["stagesCleared"] = len(set(cleared) & set(stages))


def cmd_conflict(args):
    """Pushes a save to the EDITOR's own account from off the device, so its next sync has to ask.

    This is the only way to produce a genuine conflict rather than a simulated one. SyncPlanner
    asks the player when the cloud has moved past the revision this device last agreed with, and
    the honest way to move it is for something that is not this device to write. Signing in with
    the linked email is exactly what a second phone would do.

    The document pushed is the account's OWN current cloud save with two fields nudged - a
    different player name and more stages cleared, by id as SaveSerializer records them - so
    the conflict card has two genuinely different saves to describe rather than two identical
    ones. Nothing about the local save is
    touched; the editor still holds whatever it held.
    """
    status, body = request("POST", "/auth/v1/token?grant_type=password",
                           body={"email": args.email, "password": args.password})
    if status != 200 or not isinstance(body, dict) or "access_token" not in body:
        die("could not sign in as the editor's account", str(status) + " " + str(body))

    token = body["access_token"]
    user_id = body["user"]["id"]
    print("signed in as the editor's account: " + user_id)

    status, rows = request("GET",
                           "/rest/v1/saves?select=user_id,save_version,revision,device_id,data"
                           "&user_id=eq." + user_id, token=token)
    if status != 200 or not rows:
        die("that account has no cloud save yet - let the editor sync once first", status)

    row = rows[0]
    document = row["data"]
    revision = row["revision"]
    document["playerName"] = args.name
    clear_more_stages(document, int(row["save_version"]), args.stages)
    document["playerLevel"] = int(document.get("playerLevel", 1)) + args.levels

    status, pushed = request("POST", "/rest/v1/rpc/push_save", token=token,
                             body={"p_expected_revision": revision,
                                   "p_save_version": row["save_version"],
                                   "p_device_id": "another-device",
                                   "p_data": document})
    if status not in (200, 201):
        die("push_save refused", str(status) + " " + str(pushed))

    new_revision = pushed["revision"] if isinstance(pushed, dict) else "?"
    print("pushed a newer save from 'another-device': revision " + str(revision)
          + " -> " + str(new_revision))
    print("  playerName    -> " + document["playerName"])
    print("  clearedStages -> " + str(len(document["clearedStages"])) + " ids, last "
          + (document["clearedStages"][-1] if document["clearedStages"] else "none"))
    print("  stagesCleared -> " + str(document["stagesCleared"]))
    print("  playerLevel   -> " + str(document["playerLevel"]))
    print("")
    print("The editor's next sync now has a cloud revision it has never seen, so SyncPlanner")
    print("returns AskPlayer and the chooser opens. Tap Sync Now.")


def cmd_guild_ask(args):
    """Asks to join a guild by code, as the throwaway account.

    THE POINT OF THIS IS THAT IT IS NOT THE GAME. The editor session watching a request arrive
    has to be watching a request some OTHER process made, or the whole thing is one client
    talking to itself and the roster, the accept button and the chat all prove nothing about two
    players. A python script that has never seen Unity is the strongest available version of
    "somebody else".

    Answers with the one word guild_request_send answers with: an open guild admits on arrival
    and says 'joined', a closed one queues and says 'requested'.
    """
    session = refresh(read_token_file(args.token_file))

    # Look first, as GuildScreen does: since 0014 guild_request_send takes only the code this
    # account last found through guild_by_code.
    status, found = request("POST", "/rest/v1/rpc/guild_by_code", token=session["token"],
                            body={"p_code": args.code})
    if status != 200 or not found:
        die("guild_by_code found no guild", str(status) + " " + str(found))

    status, response = request("POST", "/rest/v1/rpc/guild_request_send", token=session["token"],
                               body={"p_code": args.code})
    if status != 200:
        die("guild_request_send refused", str(status) + " " + str(response))
    print("guild_request_send -> " + str(response) + "  (code " + args.code.upper() + ")")

    status, rows = request("GET", "/rest/v1/guild_members?select=guild_id,role&user_id=eq."
                           + session["id"], token=session["token"])
    if status == 200 and rows:
        print("  now a " + rows[0]["role"] + " of " + rows[0]["guild_id"])
    else:
        print("  not a member yet - a leader or an officer has to accept")
    write_token_file(args.token_file, session)


def cmd_guild_say(args):
    """Posts one chat message as the throwaway account.

    Goes through guild_post_message like every other client, so the two-second rate limit applies
    to this exactly as it applies to the game - which is the point: a message the editor session
    sees arrive was subject to the same rule the editor's own messages are.
    """
    session = refresh(read_token_file(args.token_file))

    status, response = request("POST", "/rest/v1/rpc/guild_post_message", token=session["token"],
                               body={"p_body": args.body})
    if status == 429:
        print("refused: one message every two seconds. Wait and run it again.")
        write_token_file(args.token_file, session)
        return
    if status != 200:
        die("guild_post_message refused", str(status) + " " + str(response))
    print("said: " + str(response.get("body") if isinstance(response, dict) else response))
    write_token_file(args.token_file, session)


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    subparsers = parser.add_subparsers(dest="command", required=True)

    create = subparsers.add_parser("create", help="make a second anonymous player")
    create.add_argument("--name", default="Rival " + "".join(random.choice(string.digits) for _ in range(3)))
    create.add_argument("--token-file", required=True)
    create.set_defaults(func=cmd_create)

    publish = subparsers.add_parser("publish", help="publish a team snapshot for that player")
    publish.add_argument("--token-file", required=True)
    publish.add_argument("--hero", action="append", required=True)
    publish.add_argument("--level", type=int, default=20)
    publish.add_argument("--stars", type=int, default=5)
    publish.add_argument("--talent", type=int, default=0)
    publish.add_argument("--power", type=int, default=0)
    publish.add_argument("--label", default="Rival")
    publish.set_defaults(func=cmd_publish)

    befriend = subparsers.add_parser("befriend", help="send a friend request to a friend code")
    befriend.add_argument("--token-file", required=True)
    befriend.add_argument("--code", required=True)
    befriend.set_defaults(func=cmd_befriend)

    conflict = subparsers.add_parser("conflict", help="push a newer save to the editor's account")
    conflict.add_argument("--email", required=True)
    conflict.add_argument("--password", required=True)
    conflict.add_argument("--name", default="The Other Phone")
    conflict.add_argument("--stages", type=int, default=3)
    conflict.add_argument("--levels", type=int, default=2)
    conflict.set_defaults(func=cmd_conflict)

    guild_ask = subparsers.add_parser("guild-ask", help="ask to join a guild by code")
    guild_ask.add_argument("--token-file", required=True)
    guild_ask.add_argument("--code", required=True)
    guild_ask.set_defaults(func=cmd_guild_ask)

    guild_say = subparsers.add_parser("guild-say", help="post one guild chat message")
    guild_say.add_argument("--token-file", required=True)
    guild_say.add_argument("--body", required=True)
    guild_say.set_defaults(func=cmd_guild_say)

    args = parser.parse_args()
    load_credentials()
    args.func(args)


if __name__ == "__main__":
    main()
