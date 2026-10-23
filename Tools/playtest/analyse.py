#!/usr/bin/env python3
"""analyse.py - turns a folder of friends-build play logs into one PLAYTEST-REPORT.md.

Standard library only. No network. Read only on the logs; the only file written is the report.

USAGE
    python Tools/playtest/analyse.py PLAYLOG_FOLDER
    python Tools/playtest/analyse.py PLAYLOG_FOLDER --out C:\\somewhere\\PLAYTEST-REPORT.md

    --out PATH          where to write the report: a .md file, or a folder (PLAYTEST-REPORT.md goes in
                        it). Default: PLAYTEST-REPORT.md inside the log folder.
    --loss-streak N     a stage lost N or more times in a row is a finding (default 3)
    --short-session S   a session shorter than S seconds is a finding (default 180)
    --include-smoke     also read smoke runs (skipped by default)

THE FORMAT
    The lines are the ones PlayLogSession in Assets/Scripts/Core/Session/PlayLog.cs builds
    (Docs/FRIENDS-BUILD.md, "The play log"). The tests check those formats are still in that file.

WHO IS WHO
    A sub folder is one player: every log file anywhere inside PLAYLOG_FOLDER/alice/ is alice.
    A file directly in PLAYLOG_FOLDER is one player named after the file, with a trailing ".prev"
    dropped, so alice.txt and alice.prev.txt are both alice. Files ending in .txt or .log are read.
"""

import argparse
import os
import re
import sys
from collections import OrderedDict

REPORT_NAME = "PLAYTEST-REPORT.md"
# Screens the game opens by itself on the way through a fight. They are left out of the "opened once" rule,
# which is about a screen a player chose to visit and did not come back to.
FLOW_SCREENS = ("Battle", "WaveResult")
LOG_EXTENSIONS = (".txt", ".log")

RE_HEADER = re.compile(r"^== session (\S*) started (\S*) ==$")
RE_BUILD = re.compile(r"^build (.*?) \((.*?)\) \| platform (.*?) \| device (.*?) \| defines (.*)$")
RE_EVENT = re.compile(r"^\+(\d+(?:\.\d+)?)s (\S+)(?: (.*))?$")
RE_SCREEN = re.compile(r"^(\S+)$")
RE_STAGE = re.compile(r"^(\S+) (attempt|win|lose)(?: wave (-?\d+) battle (-?\d+))?$")
RE_PURCHASE = re.compile(r"^offer (\S+) result (bought|refused)(?: reason (.*))?$")
RE_SUMMARY = re.compile(r"^length (\d+)s screens (\d+) stages_cleared (\d+) purchases (\d+) errors (\d+)$")


class Session(object):
    def __init__(self, player, source, session_id, started):
        self.player = player
        self.source = source
        self.session_id = session_id
        self.started = started
        self.build = ""
        self.platform = ""
        self.device = ""
        self.events = []          # (t, kind, data) in file order
        self.summary_length = None
        self.smoke = False
        self.order = ("~", 0)

    @property
    def last_stamp(self):
        return max([e[0] for e in self.events] or [0.0])

    @property
    def has_summary(self):
        return self.summary_length is not None

    @property
    def length(self):
        """Seconds. The last summary is cumulative; a session without one ends at its last line."""
        if self.summary_length is None:
            return self.last_stamp
        return max(float(self.summary_length), self.last_stamp)

    def of_kind(self, kind):
        return [e for e in self.events if e[1] == kind]

    @property
    def screens(self):
        return [(t, d) for t, k, d in self.events if k == "screen"]

    @property
    def last_screen(self):
        s = self.screens
        return s[-1][1] if s else None


# ---------------------------------------------------------------- parsing

def parse_text(text, player, source):
    """All sessions in one file's text. Lines that are not play log lines are ignored, so a pasted share
    text (its "Last session summary" first line and the "..." cut) reads the same as the file."""
    sessions = []
    current = None
    orphan = 0
    for raw in text.replace("\r\n", "\n").split("\n"):
        line = raw.strip()
        if not line:
            continue
        m = RE_HEADER.match(line)
        if m:
            current = Session(player, source, m.group(1), m.group(2))
            sessions.append(current)
            continue
        m = RE_BUILD.match(line)
        if m and current is not None:
            current.build, current.platform, current.device = m.group(1), m.group(3), m.group(4)
            continue
        m = RE_EVENT.match(line)
        if not m:
            continue
        if current is None:
            # A share text cut in the middle of a session: its header is gone, its lines are not.
            orphan += 1
            current = Session(player, source, "", "")
            current.session_id = "(cut %d)" % orphan
            sessions.append(current)
        t = float(m.group(1))
        kind = m.group(2)
        rest = m.group(3) or ""
        if kind == "screen":
            sm = RE_SCREEN.match(rest)
            if sm:
                current.events.append((t, "screen", sm.group(1)))
        elif kind == "stage":
            sm = RE_STAGE.match(rest)
            if sm:
                current.events.append((t, "stage", (sm.group(1), sm.group(2))))
        elif kind == "purchase":
            sm = RE_PURCHASE.match(rest)
            if sm:
                current.events.append((t, "purchase", (sm.group(1), sm.group(2), sm.group(3) or "")))
        elif kind == "summary":
            sm = RE_SUMMARY.match(rest)
            if sm:
                current.summary_length = int(sm.group(1))
                current.events.append((t, "summary", int(sm.group(1))))
        elif kind == "error":
            current.events.append((t, "error", rest))
        elif kind == "cloud":
            current.events.append((t, "cloud", rest))
        elif kind.startswith("[smoke]"):
            current.smoke = True
    sessions = [s for s in sessions if s.events or s.session_id]
    # Play order key. A cut session is the start of a share text, so it comes just before the next dated session
    # in the same text (or after everything dated, when nothing follows it).
    for i, s in enumerate(sessions):
        if s.started:
            s.order = (s.started, 1)
        else:
            nxt = next((o.started for o in sessions[i + 1:] if o.started), "~")
            s.order = (nxt, 0)
    return sessions


def player_name(root, path):
    rel = os.path.relpath(path, root)
    parts = rel.replace("\\", "/").split("/")
    if len(parts) > 1:
        return parts[0]
    stem = os.path.splitext(parts[0])[0]
    if stem.lower().endswith(".prev"):
        stem = stem[:-5]
    return stem


def is_smoke_file(path):
    return ".smoke." in os.path.basename(path).lower()


def read_folder(root, include_smoke=False, skip=()):
    """Returns (players, skipped). players: name -> sessions in play order. skipped: (relative path, why)."""
    skip = set(os.path.abspath(p) for p in skip)
    found = []
    skipped = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames.sort(key=str.lower)
        for name in sorted(filenames, key=str.lower):
            path = os.path.join(dirpath, name)
            if os.path.abspath(path) in skip or not name.lower().endswith(LOG_EXTENSIONS):
                continue
            rel = os.path.relpath(path, root)
            if is_smoke_file(path) and not include_smoke:
                skipped.append((rel, "smoke run file"))
                continue
            with open(path, "r", encoding="utf-8", errors="replace") as f:
                text = f.read()
            sessions = parse_text(text, player_name(root, path), rel)
            if not sessions:
                skipped.append((rel, "no play log lines"))
                continue
            for s in sessions:
                if s.smoke and not include_smoke:
                    skipped.append((rel, "smoke session %s" % s.session_id))
                else:
                    found.append(s)

    players = OrderedDict()
    for s in found:
        players.setdefault(s.player, [])
        # The same session can arrive twice (the file and a pasted share text): keep the fuller copy.
        twin = None
        if not s.session_id.startswith("(cut"):
            twin = next((o for o in players[s.player] if o.session_id == s.session_id), None)
        if twin is None:
            players[s.player].append(s)
        elif len(s.events) > len(twin.events):
            players[s.player][players[s.player].index(twin)] = s
    for name in players:
        # Start stamps are ISO UTC, so they sort as text.
        arrived = list(players[name])
        players[name] = sorted(arrived, key=lambda s: (s.order, arrived.index(s)))
    return OrderedDict(sorted(players.items(), key=lambda kv: kv[0].lower())), skipped


# ---------------------------------------------------------------- analysis

def stage_key(stage_id):
    nums = [int(n) for n in re.findall(r"\d+", stage_id)]
    return (nums, stage_id)


def screen_time(session):
    """Seconds on each screen: from the line that opened it to the next screen line or the session end."""
    out = {}
    screens = session.screens
    end = session.length
    for i, (t, name) in enumerate(screens):
        nxt = screens[i + 1][0] if i + 1 < len(screens) else end
        out[name] = out.get(name, 0.0) + max(0.0, nxt - t)
    return out


def stage_stats(sessions, streak_at):
    """Per stage: attempts, losses, cleared, the longest run of losses in a row, and where the runs that reach
    streak_at happened. An attempt (one battle fight) followed by a lose is a loss; one followed by the next
    attempt with no lose was a battle won, which ends the run; a win ends it too. An attempt left open at a
    session end (quit mid fight) ends nothing."""
    stats = OrderedDict()
    for si, s in enumerate(sessions):
        open_attempt = {}
        for t, kind, data in s.events:
            if kind != "stage":
                continue
            sid, result = data
            st = stats.setdefault(sid, {"attempts": 0, "losses": 0, "cleared": False, "run": 0,
                                        "best_run": 0, "run_start": None, "streaks": []})
            if result == "attempt":
                st["attempts"] += 1
                if open_attempt.get(sid):
                    _end_run(st, streak_at)
                open_attempt[sid] = True
            elif result == "lose":
                if st["run"] == 0:
                    st["run_start"] = (si, t)
                st["run"] += 1
                st["losses"] += 1
                st["best_run"] = max(st["best_run"], st["run"])
                open_attempt[sid] = False
            elif result == "win":
                st["cleared"] = True
                _end_run(st, streak_at)
                open_attempt[sid] = False
    for st in stats.values():
        _end_run(st, streak_at)
    return stats


def _end_run(st, streak_at):
    if st["run"] >= streak_at:
        st["streaks"].append((st["run"], st["run_start"]))
    st["run"] = 0
    st["run_start"] = None


def analyse_player(name, sessions, streak_at):
    p = {"name": name, "sessions": sessions}
    p["total"] = sum(s.length for s in sessions)
    p["stages"] = stage_stats(sessions, streak_at)
    cleared = [k for k, v in p["stages"].items() if v["cleared"]]
    p["furthest"] = max(cleared, key=stage_key) if cleared else None
    p["furthest_tried"] = max(p["stages"].keys(), key=stage_key) if p["stages"] else None
    times = {}
    counts = {}
    for s in sessions:
        for k, v in screen_time(s).items():
            times[k] = times.get(k, 0.0) + v
        for t, sc in s.screens:
            counts[sc] = counts.get(sc, 0) + 1
    p["screen_time"] = times
    p["screen_count"] = counts
    p["purchases"] = [(si, t, d) for si, s in enumerate(sessions) for t, k, d in s.events if k == "purchase"]
    p["errors"] = sum(len(s.of_kind("error")) for s in sessions)
    builds = []
    for s in sessions:
        b = " ".join(x for x in (s.build, s.platform, s.device) if x)
        if b and b not in builds:
            builds.append(b)
    p["builds"] = builds
    return p


def findings(players, streak_at, short_seconds):
    out = []
    for p in players:
        for sid, st in p["stages"].items():
            for run, start in st["streaks"]:
                where = ""
                if start is not None:
                    where = " (from session %d at %s)" % (start[0] + 1, clock(start[1]))
                tail = "cleared later" if st["cleared"] else "never cleared"
                out.append("**%s** lost stage `%s` %d times in a row%s, %s." % (p["name"], sid, run, where, tail))
    for p in players:
        for i, s in enumerate(p["sessions"]):
            if s.length < short_seconds:
                last = s.last_screen or "no screen"
                note = "" if s.has_summary else ", no summary line so the game may have been killed"
                out.append("**%s** session %d lasted %s (last screen %s%s)." % (p["name"], i + 1, duration(s.length), last, note))
    once = OrderedDict()
    for p in players:
        for sc, n in sorted(p["screen_count"].items()):
            if n == 1 and sc not in FLOW_SCREENS:
                once.setdefault(sc, []).append(p["name"])
    for sc in sorted(once, key=lambda k: (-len(once[k]), k)):
        who = once[sc]
        out.append("`%s` opened once and never again by %d of %d players (%s)." % (sc, len(who), len(players), ", ".join(who)))
    return out


# ---------------------------------------------------------------- report

def clock(seconds):
    return "+" + duration(seconds)


def duration(seconds):
    seconds = int(round(seconds))
    h, rem = divmod(seconds, 3600)
    m, s = divmod(rem, 60)
    if h:
        return "%dh %02dm" % (h, m)
    if m:
        return "%dm %02ds" % (m, s)
    return "%ds" % s


def render(players, skipped, streak_at, short_seconds, folder):
    L = []
    L.append("# Playtest report")
    L.append("")
    L.append("From `%s`: %d players, %d sessions, %s played in total. Written by `Tools/playtest/analyse.py`."
             % (folder, len(players), sum(len(p["sessions"]) for p in players), duration(sum(p["total"] for p in players))))
    L.append("")

    L.append("## Look at this")
    L.append("")
    L.append("Rules: a stage lost %d or more times in a row, a session under %s, a screen a player opened once and never "
             "again (%s left out: the game opens them itself)." % (streak_at, duration(short_seconds), " and ".join(FLOW_SCREENS)))
    L.append("")
    found = findings(players, streak_at, short_seconds)
    if found:
        L.extend("- " + f for f in found)
    else:
        L.append("- Nothing tripped a rule.")
    L.append("")

    L.append("## Overview")
    L.append("")
    L.append("| Player | Sessions | Total time | Furthest cleared | Stopped at (last screen) | Test purchases | Errors |")
    L.append("|---|---|---|---|---|---|---|")
    for p in players:
        stopped = p["sessions"][-1].last_screen if p["sessions"] else None
        L.append("| %s | %d | %s | %s | %s | %d | %d |" % (
            p["name"], len(p["sessions"]), duration(p["total"]), p["furthest"] or "none",
            stopped or "none", len(p["purchases"]), p["errors"]))
    L.append("")

    for p in players:
        L.append("## %s" % p["name"])
        L.append("")
        if p["builds"]:
            L.append("Build: %s" % "; ".join(p["builds"]))
            L.append("")
        furthest = p["furthest"] or "none"
        if p["furthest_tried"] and p["furthest_tried"] != p["furthest"]:
            furthest += " (furthest tried: %s, not cleared)" % p["furthest_tried"]
        L.append("Furthest stage cleared: %s. Total time: %s." % (furthest, duration(p["total"])))
        L.append("")

        L.append("### Sessions")
        L.append("")
        L.append("| # | Started (UTC) | Length | Screens | Stages cleared | Last screen before quitting | Source |")
        L.append("|---|---|---|---|---|---|---|")
        for i, s in enumerate(p["sessions"]):
            length = duration(s.length) + ("" if s.has_summary else " (no summary)")
            wins = sum(1 for t, k, d in s.events if k == "stage" and d[1] == "win")
            L.append("| %d | %s | %s | %d | %d | %s | %s |" % (
                i + 1, s.started or s.session_id, length, len(s.screens), wins, s.last_screen or "none", s.source))
        L.append("")

        L.append("### Where the time went")
        L.append("")
        if p["screen_time"]:
            total = sum(p["screen_time"].values()) or 1.0
            L.append("| Screen | Time | Share | Opened |")
            L.append("|---|---|---|---|")
            for sc, sec in sorted(p["screen_time"].items(), key=lambda kv: (-kv[1], kv[0])):
                L.append("| %s | %s | %d%% | %d |" % (sc, duration(sec), int(round(100.0 * sec / total)), p["screen_count"].get(sc, 0)))
        else:
            L.append("No screen lines.")
        L.append("")

        L.append("### Stages that took several tries")
        L.append("")
        hard = [(k, v) for k, v in p["stages"].items() if v["losses"] >= 2]
        if hard:
            L.append("| Stage | Fights | Losses | Most losses in a row | Cleared |")
            L.append("|---|---|---|---|---|")
            for k, v in sorted(hard, key=lambda kv: (-kv[1]["losses"], stage_key(kv[0]))):
                L.append("| %s | %d | %d | %d | %s |" % (k, v["attempts"], v["losses"], v["best_run"], "yes" if v["cleared"] else "no"))
        else:
            L.append("No stage was lost twice or more.")
        L.append("")

        L.append("### Test purchases")
        L.append("")
        if p["purchases"]:
            for si, t, (offer, result, reason) in p["purchases"]:
                extra = (" (%s)" % reason) if reason else ""
                L.append("- Session %d %s: `%s` %s%s" % (si + 1, clock(t), offer, result, extra))
        else:
            L.append("None.")
        L.append("")

    if skipped:
        L.append("## Files not read")
        L.append("")
        for rel, why in skipped:
            L.append("- `%s`: %s" % (rel, why))
        L.append("")
    return "\n".join(L)


def build_report(folder, streak_at=3, short_seconds=180, include_smoke=False, skip=()):
    raw, skipped = read_folder(folder, include_smoke, skip)
    players = [analyse_player(n, s, streak_at) for n, s in raw.items() if s]
    return render(players, skipped, streak_at, short_seconds, os.path.basename(os.path.abspath(folder)))


def main(argv=None):
    ap = argparse.ArgumentParser(description="Friends-build play logs to PLAYTEST-REPORT.md")
    ap.add_argument("folder")
    ap.add_argument("--out")
    ap.add_argument("--loss-streak", type=int, default=3)
    ap.add_argument("--short-session", type=int, default=180)
    ap.add_argument("--include-smoke", action="store_true")
    a = ap.parse_args(argv)
    if not os.path.isdir(a.folder):
        print("not a folder: %s" % a.folder, file=sys.stderr)
        return 2
    out = a.out or os.path.join(a.folder, REPORT_NAME)
    if os.path.isdir(out):
        out = os.path.join(out, REPORT_NAME)
    text = build_report(a.folder, a.loss_streak, a.short_session, a.include_smoke, skip=[out])
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print("wrote %s" % out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
