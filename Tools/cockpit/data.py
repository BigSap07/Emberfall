"""Read-only collectors for the cockpit.

Everything here only reads files or runs `ccusage`, `git log`, `git branch`, `git bundle list-heads`
and a process list. The only things written are Tools/cockpit/state.json (keys b and w, via
`save_state`) and the transcript cache tmp/cockpit-cache.json (via `localusage.scan`). Every collector
returns plain dicts and lists, and a `hint` string instead of raising when its source is missing.
"""

from __future__ import annotations

import csv
import json
import os
import re
import shutil
import statistics
import subprocess
import sys
import time
from dataclasses import dataclass, field
from datetime import date, datetime, time as dtime, timedelta
from pathlib import Path

import localusage

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
LIVE_ROOT = Path(os.environ.get("EMBERFALL_ROOT", r"D:\Emberfall"))
RUNNING_WINDOW = timedelta(minutes=15)
PARALLEL_WINDOW = timedelta(hours=1)
YOUNG_BLOCK = timedelta(minutes=30)
DISCOVER_RUNNING = timedelta(minutes=20)   # a discovered log changed this recently, with no final line, is running
LEDGER_LIVE = timedelta(minutes=5)         # a ledger changed this recently is generating
RUN_ROOTS = ("tmp", "ui-gen", "handoff")   # roots scanned for run logs; "run_roots" in costs.json overrides
DISCOVER_DEPTH = 2                         # folders below a root that are searched
IMAGE_PRICE = 0.04                         # assumed Replicate price per image when a ledger states none
RECENT_RUNS = timedelta(days=3)            # a queue row without a log only shows when it ended this recently
SUB_TIMEOUT = 60
HISTORY_DAYS = 12
QUIET_FROM, QUIET_TO = dtime(3, 0), dtime(5, 30)
# Sessions that count towards a run: the play slot, run worktrees (Game-r28lead, Game-r29b) and merge
# worktrees (Game-m1). Owner sessions in D:\Emberfall itself, the cockpit worktree and other projects
# (pulse) do not. Override with "run_cwd_regex" in costs.json.
RUN_CWD = r"^Game(-(r\d+\w*|m\d+))?$"


@dataclass
class Env:
    """Where to read from. The mock env points every path into Tools/cockpit/mock."""

    root: Path
    game: Path
    claude_md: Path
    costs: Path
    mock: bool = False
    clock: datetime | None = None
    mtimes: dict = field(default_factory=dict)
    projects: Path | None = None        # ~/.claude/projects
    cache: Path | None = None           # tmp/cockpit-cache.json (None: in memory only)
    state: Path | None = None           # Tools/cockpit/state.json
    state_mem: dict = field(default_factory=dict)
    cwd_root: str = ""                  # transcripts' cwd prefix for this root

    def now(self) -> datetime:
        return self.clock or datetime.now()

    def mtime(self, p: Path) -> datetime:
        rel = p.relative_to(self.root).as_posix() if p.is_relative_to(self.root) else str(p)
        if rel in self.mtimes:
            return parse_time(self.mtimes[rel])
        return datetime.fromtimestamp(p.stat().st_mtime)


def live_env() -> Env:
    cfg = Path(os.environ.get("CLAUDE_CONFIG_DIR", Path.home() / ".claude"))
    return Env(root=LIVE_ROOT, game=LIVE_ROOT / "Game", claude_md=REPO / "CLAUDE.md",
               costs=HERE / "costs.json", projects=cfg / "projects", cache=LIVE_ROOT / "tmp" / "cockpit-cache.json",
               state=HERE / "state.json", cwd_root=LIVE_ROOT.as_posix())


def mock_env() -> Env:
    m = HERE / "mock"
    meta = json.loads((m / "mock.json").read_text(encoding="utf-8"))
    return Env(root=m / "Emberfall", game=m / "Emberfall" / "Game", claude_md=m / "CLAUDE.md",
               costs=m / "costs.json", mock=True, clock=parse_time(meta["now"]),
               mtimes=meta.get("mtimes", {}), projects=m / "claude-projects",
               state_mem=dict(meta.get("state", {})), cwd_root="D:/Emberfall")


# ---------------------------------------------------------------- helpers

def parse_time(s) -> datetime | None:
    """ISO time to local naive datetime. Aware times are converted to local time."""
    if not s:
        return None
    try:
        t = datetime.fromisoformat(str(s).replace("Z", "+00:00"))
    except ValueError:
        return None
    if t.tzinfo is not None:
        t = t.astimezone().replace(tzinfo=None)
    return t


def fmt_dur(td: timedelta | None) -> str:
    if td is None:
        return "-"
    m = int(td.total_seconds() // 60)
    if m < 0:
        return "-"
    if m < 60:
        return f"{m}m"
    h, m = divmod(m, 60)
    if h < 48:
        return f"{h}h{m:02d}"
    return f"{h // 24}d{h % 24}h"


def read_text(p: Path) -> str:
    try:
        return p.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return ""


def run(cmd: list[str], cwd: Path | None = None) -> tuple[int, str]:
    try:
        r = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, encoding="utf-8",
                           errors="replace", timeout=SUB_TIMEOUT,
                           creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        return r.returncode, r.stdout
    except (OSError, subprocess.SubprocessError):
        return -1, ""


def clock_after(hhmm: str, ref: datetime) -> datetime:
    """The first HH:MM at or after `ref` (same day, or the next one if it already passed)."""
    h, m = (int(x) for x in hhmm.split(":"))
    t = ref.replace(hour=h, minute=m, second=0, microsecond=0)
    return t + timedelta(days=1) if t < ref - timedelta(minutes=1) else t


# ---------------------------------------------------------------- state.json (keys b and w)

def load_state(env: Env) -> dict:
    if env.mock or not env.state:
        return dict(env.state_mem)
    try:
        return json.loads(env.state.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {}


def save_state(env: Env, **changes) -> dict:
    st = {**load_state(env), **changes}
    if env.mock or not env.state:
        env.state_mem = st
        return st
    try:
        env.state.write_text(json.dumps(st, indent=1, default=str), encoding="utf-8")
    except OSError:
        pass
    return st


def parse_remaining(text: str) -> timedelta | None:
    """How long until a claude.ai reset: '3h12', '3h 12m', '3:12', '01:12', '45m', '4d3h', '2d'."""
    s = re.sub(r"days?", "d", re.sub(r"minutes?|mins?", "m", re.sub(r"hours?|hrs?", "h", text.strip().lower())))
    m = re.fullmatch(r"(?:(\d+)\s*d)?\s*(?:(\d+)\s*h)?\s*(?:(\d+)\s*m?)?", s)
    if m and any(m.groups()) and not re.fullmatch(r"\d+", s):
        d, h, mi = (int(x or 0) for x in m.groups())
        return timedelta(days=d, hours=h, minutes=mi)
    m = re.fullmatch(r"(\d{1,3}):(\d{2})", s)
    if m:
        return timedelta(hours=int(m.group(1)), minutes=int(m.group(2)))
    return None


def parse_weekly(text: str, now: datetime) -> dict | None:
    """'62 4d3h' or '62% 4d 3h' or '62 2026-10-27 09:00': percent used, then time left or the reset."""
    m = re.match(r"\s*(\d{1,3}(?:[.,]\d+)?)\s*%?\s+(.+)$", text)
    if not m:
        return None
    pct = float(m.group(1).replace(",", "."))
    rest = m.group(2).strip()
    left = parse_remaining(rest)
    reset = now + left if left else parse_time(rest.replace(" ", "T", 1) if re.match(r"\d{4}-", rest) else rest)
    if not reset or pct > 100:
        return None
    return {"pct": pct, "reset": reset.replace(microsecond=0).isoformat(), "set_at": now.replace(microsecond=0).isoformat()}


# ---------------------------------------------------------------- runs

LEAD_RE = re.compile(r"^\s*(?:[-*]\s*)?(?:(\d{4}-\d{2}-\d{2})[ T])?\b(\d{1,2}):(\d{2})(?::(\d{2}))?\b")
ANY_RE = re.compile(r"(?:(\d{4}-\d{2}-\d{2})[ T])?\b(\d{1,2}):(\d{2})(?::(\d{2}))?\b")
SLEEP_RE = re.compile(r"\b(?:Start-Sleep|sleep)\s+(?:-s(?:econds)?\s+)?(\d+)|\bpoll(?:ing|ed)?\b|\bwaiting for\b", re.I)
LIMIT_RE = re.compile(r"usage limit reached|limit reached|rate limit", re.I)
NAME = r"[A-Za-z][A-Za-z0-9]*(?:-[A-Za-z0-9]+)*"
START_NAMED = re.compile(rf"\b({NAME})\s+(?:started|start)\b(?!\s+conditions?)")
STARTING = re.compile(rf"\bStarting\s+({NAME})")
START_BARE = re.compile(r"^\s*(?:[-*]\s*)?(?:\d{4}-\d{2}-\d{2}[ T])?\d{1,2}:\d{2}(?::\d{2})?\s+start\b", re.I)
DONE_NAMED = re.compile(rf"\b({NAME})\s+RUN DONE\b")
CHAIN_UPDATE = re.compile(r"OWNER CHAIN UPDATE\b[^:]*:\s*after\s+(.+?)\s+(?-i:run)\s+(\S.+)$", re.I)
NO_NEW = re.compile(r"(?:no new run after|do not start a new run after|not after)\s+(\d{1,2}:\d{2})", re.I)
# "Cutoff lifted by owner", "the cutoff was removed", "lifted the no new run rule": the newest cutoff line wins, lift or not
LIFT = re.compile(r"cut-?off\s+(?:is\s+|was\s+|has\s+been\s+)?(?:lifted|removed|cancell?ed|waived)"
                  r"|(?:lifted|removed|waived|cancell?ed)\s+the\s+(?:no[- ]new[- ]run\s+)?(?:cut-?off|rule)", re.I)
FUTURE_SLACK = timedelta(hours=3)   # a first stamp this far past the file's mtime means the run began the day before
IDLE_GAP_S = 300                    # no transcript message for this long inside a run counts as waiting
DEADLINE = re.compile(r"Deadline\s+(\d{1,2}:\d{2})", re.I)
STOP_START = re.compile(r"stop starting work\s+(\d{1,2}:\d{2})", re.I)


def line_times(text: str, anchor: date) -> list[tuple[datetime, str]]:
    """Every timestamped line. A stamp counts when it opens the line (after an optional bullet); if
    no line in the file has one, a stamp anywhere in a line does. Lines with only HH:MM take their
    date from the last full date seen (or the anchor) and roll over midnight when the clock goes
    backwards."""
    lines = text.splitlines()
    rx = LEAD_RE if any(LEAD_RE.match(l) for l in lines) else ANY_RE
    out, day, last = [], anchor, None
    for line in lines:
        m = rx.match(line) if rx is LEAD_RE else rx.search(line)
        if not m:
            continue
        if m.group(1):
            day = date.fromisoformat(m.group(1))
        h, mi = int(m.group(2)), int(m.group(3))
        if h > 23 or mi > 59:
            continue
        t = datetime.combine(day, datetime.min.time()).replace(hour=h, minute=mi, second=int(m.group(4) or 0))
        if last and not m.group(1) and t < last - timedelta(hours=1):
            day += timedelta(days=1)
            t += timedelta(days=1)
        out.append((t, line))
        last = t
    return out


def key_of(name: str) -> str:
    return name.upper().split("-")[0]


def run_name(n: str) -> bool:
    """A run name has a capital first letter and a digit: R28-SCREENS, MERGE-1, R29B."""
    return n[:1].isupper() and any(c.isdigit() for c in n)


def log_events(stamps: list[tuple[datetime, str]], own: str, last_line: str, fallback: datetime) -> list[tuple]:
    """(time, 'start'|'done', name, line) for every step boundary a log states. A bare 'start' line
    belongs to the log's own run. 'RUN DONE' counts as the log's own end only as its last line;
    'X RUN DONE' counts wherever it stands. A quoted 'RUN DONE' elsewhere is not an event."""
    ev = []
    for t, line in stamps:
        if START_BARE.match(line):
            ev.append((t, "start", own, line))
        for rx in (START_NAMED, STARTING):
            for m in rx.finditer(line):
                if run_name(m.group(1)):
                    ev.append((t, "start", m.group(1), line))
        for m in DONE_NAMED.finditer(line):
            if run_name(m.group(1)):
                ev.append((t, "done", m.group(1), line))
    if "RUN DONE" in last_line.upper() and not DONE_NAMED.search(last_line):
        t = stamps[-1][0] if stamps else fallback
        ev.append((t, "done", own, last_line))
    return ev


def prompt_duration(path: Path) -> timedelta | None:
    """Timebox stated in a run prompt: 'Timebox 90 minutes' or 'deadline start plus 1h30'."""
    text = read_text(path)[:4000]
    m = re.search(r"timebox\s+(\d+)\s*min", text, re.I)
    if m:
        return timedelta(minutes=int(m.group(1)))
    m = re.search(r"start plus\s+(\d+)\s*h\s*(\d{1,2})?", text, re.I)
    if m:
        return timedelta(hours=int(m.group(1)), minutes=int(m.group(2) or 0))
    return None


def parse_chain_update(line: str) -> list[str]:
    """Names after 'after X run <a>, then <b>, then <c>. No new run after ...'"""
    m = CHAIN_UPDATE.search(line)
    if not m:
        return []
    rest = re.split(r"\.\s", m.group(2).strip() + " ", maxsplit=1)[0]
    names = []
    for part in re.split(r",?\s+then\s+", rest):
        part = part.strip().rstrip(".")
        p = re.search(r"PROMPT_([\w-]+)\.txt", part)
        if p:
            names.append(p.group(1))
        elif part:
            names.append(re.split(r"\s*\(", re.sub(r"^the\s+", "", part))[0].strip())
    return names


def name_tokens(name: str) -> list[str]:
    """'friends build b' -> ['friends', 'b']; the folder 'friends-b-2026-10-23' -> ['friends', 'b', '2026', '10', '23']."""
    return [w for w in re.findall(r"[a-z0-9]+", name.lower()) if w not in ("build", "the", "run")]


def output_exists(name: str, since: datetime, builds: list) -> bool:
    """A queued entry whose Builds folder (named like it, changed after the entry was queued) already exists."""
    want = name_tokens(name)
    return bool(want) and any(tok[: len(want)] == want and when >= since for tok, when in builds)


FUTURE_TOLERANCE = timedelta(minutes=1)    # HH:MM stamps against a file time that has seconds


def stamps_for(text: str, changed: datetime, now: datetime) -> tuple[list[tuple[datetime, str]], bool]:
    """The log's stamps, dated and clamped. The file date anchors HH:MM-only stamps; if that puts the
    first stamp well after the file's own change time, the run started the day before. A stamp past the
    file's change time (or past now) is a clock written ahead (R30's 21:35 at 21:21): it counts as that
    time, never later, so no elapsed time is negative or in the future. The flag says a stamp was clamped."""
    stamps = line_times(text, changed.date())
    if stamps and stamps[0][0] > changed + FUTURE_SLACK:
        stamps = line_times(text, changed.date() - timedelta(days=1))
    ceil = min(changed, now)
    future = any(t > ceil + FUTURE_TOLERANCE for t, _ in stamps)
    return [(min(t, ceil), line) for t, line in stamps], future


def read_runs_csv(env: Env) -> dict[str, dict]:
    """tmp/runs.csv written by the queue runner, newest row per run name (upper case). {} if missing or unreadable."""
    p = env.root / "tmp" / "runs.csv"
    rows: dict[str, dict] = {}
    try:
        with p.open(encoding="utf-8-sig", errors="replace", newline="") as f:
            for row in csv.DictReader(f):
                name = (row.get("run") or "").strip()
                if not name:
                    continue
                rows[name.upper()] = {"name": name, "start": parse_time((row.get("start") or "").strip()),
                                      "end": parse_time((row.get("end") or "").strip()),
                                      "result": (row.get("result") or "").strip()}
    except (OSError, csv.Error):
        return {}
    return rows


def read_queue_lock(env: Env) -> str:
    """Contents of handoff/run-queue.lock: 'FREE', a PID, or '' when there is no lock file."""
    return read_text(env.root / "handoff" / "run-queue.lock").strip()[:40]


def log_id(p: Path) -> str:
    """Run id from a log file name: RUNNING_LOG_R43.md -> R43, R20B_running_log.md -> R20B, RUNNING_LOG.md -> its folder."""
    s = re.sub(r"(?i)[_\- ]*running[_\- ]*log[_\- ]*", "-", p.stem).strip("-_ ")
    return s or p.parent.name


def find_run_logs(env: Env, cfg: dict) -> list[Path]:
    """*running_log*.md files in the configured roots, at most DISCOVER_DEPTH folders down. The queue's own
    tmp/<RUN>_running_log.md files are left out: collect_runs reads those by convention."""
    roots = cfg.get("run_roots") if isinstance(cfg.get("run_roots"), list) and cfg.get("run_roots") else RUN_ROOTS
    tmp = env.root / "tmp"
    out: list[Path] = []

    def walk(d: Path, depth: int) -> None:
        try:
            with os.scandir(d) as it:
                for e in sorted(it, key=lambda x: x.name.lower()):
                    if e.is_dir(follow_symlinks=False):
                        if depth < DISCOVER_DEPTH:
                            walk(Path(e.path), depth + 1)
                    elif e.name.lower().endswith(".md") and "running_log" in e.name.lower():
                        out.append(Path(e.path))
        except OSError:
            pass

    for r in roots:
        walk(env.root / str(r), 0)
    return [p for p in out if not (p.parent == tmp and p.name.endswith("_running_log.md"))]


def discovered_run(env: Env, p: Path, now: datetime) -> dict | None:
    """One run from a log found by discovery. None when the file cannot be read."""
    try:
        changed = env.mtime(p)
        text = p.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return None
    name = log_id(p)
    stamps, future = stamps_for(text, changed, now)
    lines = [l for l in text.splitlines() if l.strip()]
    last_line = lines[-1].upper() if lines else ""
    r = blank_run(name)
    r.update(path=str(p), changed=changed, lines=lines, has_log=True, source="found", future=future)
    r["start"] = stamps[0][0] if stamps else min(changed, now)
    r["last"] = stamps[-1][0] if stamps else min(changed, now)
    if "RUN DONE" in last_line:
        r["state"], r["end"] = "DONE", r["last"]
    elif "BLOCKED" in last_line:
        r["state"], r["end"] = "BLOCKED", r["last"]
    elif now - changed <= DISCOVER_RUNNING:
        r["state"], r["end"] = "running", now
    else:
        r["state"], r["end"] = "stale", r["last"]
    r["elapsed"] = max(timedelta(0), r["end"] - r["start"])
    return r


RESULT_STATE = (("DONE", "DONE"), ("BLOCKED", "BLOCKED"))


def apply_queue_rows(runs: list[dict], rows: dict[str, dict], now: datetime) -> None:
    """Mark runs the queue runner also knows (source "queue"), let its recorded end settle a log that has no
    final line (R31: FAILED at 09:48 while its log stopped at 08:54), and add rows that have no log at all."""
    by_name = {r["name"].upper(): r for r in runs}
    for key, row in rows.items():
        r = by_name.get(key)
        word = row["result"].upper()
        state = next((st for w, st in RESULT_STATE if word.startswith(w)), "stale")
        if r is None:
            if row["end"] and now - row["end"] <= RECENT_RUNS:
                n = blank_run(row["name"])
                n.update(state=state, start=row["start"], end=row["end"], last=row["end"], changed=row["end"],
                         source="queue", note=row["result"][:120])
                n["elapsed"] = max(timedelta(0), row["end"] - row["start"]) if row["start"] else None
                runs.append(n)
                by_name[key] = n
            continue
        r["source"] = "queue"
        if r["state"] in ("running", "waiting") and row["end"] and row["end"] <= now and (
                not r["last"] or row["end"] >= r["last"] - timedelta(minutes=2)):
            r["state"], r["end"] = state, row["end"]
            r["note"] = row["result"][:120]
            if r["start"]:
                r["elapsed"] = max(timedelta(0), r["end"] - r["start"])


def discover_runs(env: Env, cfg: dict, runs: list[dict], now: datetime) -> None:
    """Add every discovered log that is not already a run. The same run counts once: a log whose id shares its
    key with a queue or convention run (R43 and R43-UI-ART-GEN) is that run."""
    have = {r["key"] for r in runs} | {r["name"].lower() for r in runs}
    for p in find_run_logs(env, cfg):
        name = log_id(p)
        if key_of(name) in have or name.lower() in have:
            continue
        r = discovered_run(env, p, now)
        if r:
            runs.append(r)
            have |= {r["key"], name.lower()}


def blank_run(name: str) -> dict:
    return {"name": name, "key": key_of(name), "path": "", "state": "queued", "changed": None, "start": None,
            "last": None, "end": None, "elapsed": None, "lines": [], "idle_s": 0, "sleep_hits": 0,
            "limit_hits": 0, "deadline": None, "stop_start": None, "prompt": None, "has_log": False,
            "source": "queue", "future": False, "note": ""}


def collect_runs(env: Env) -> dict:
    tmp, handoff = env.root / "tmp", env.root / "handoff"
    if not tmp.is_dir():
        return {"runs": [], "active": None, "tail": [], "queue": [], "no_new_after": None, "hint": f"{tmp} not found"}
    now = env.now()
    logs, events, updates, no_new = [], [], [], []
    for p in sorted(tmp.glob("*_running_log.md")):
        name = p.name[: -len("_running_log.md")]
        text = read_text(p)
        changed = env.mtime(p)
        stamps, future = stamps_for(text, changed, now)
        lines = [l for l in text.splitlines() if l.strip()]
        last_line = lines[-1] if lines else ""
        ev = log_events(stamps, name, last_line, changed)
        events += [(*e, name) for e in ev]
        for t, line in stamps:
            if CHAIN_UPDATE.search(line):
                updates.append((t, parse_chain_update(line)))
            for m in NO_NEW.finditer(line):
                no_new.append((t, clock_after(m.group(1), t)))
            if LIFT.search(line):
                no_new.append((t, None))
        logs.append({"name": name, "path": p, "text": text, "changed": changed, "stamps": stamps,
                     "lines": lines, "last_line": last_line, "future": future})

    # step state per key: newest start, newest done
    by_key: dict[str, dict] = {}
    for t, kind, name, line, src in sorted(events, key=lambda e: e[0]):
        k = by_key.setdefault(key_of(name), {"starts": [], "dones": [], "names": []})
        k["starts" if kind == "start" else "dones"].append((t, line, src))
        k["names"].append(name)
    open_keys = [(k, v["starts"][-1][0]) for k, v in by_key.items()
                 if v["starts"] and not (v["dones"] and v["dones"][-1][0] >= v["starts"][-1][0])]
    active_key = max(open_keys, key=lambda kv: kv[1])[0] if open_keys else None

    runs, seen = [], set()
    log_by_key = {key_of(l["name"]): l for l in logs}
    for k in list(log_by_key) + [k for k in by_key if k not in log_by_key]:
        lg = log_by_key.get(k)
        ev = by_key.get(k, {"starts": [], "dones": [], "names": []})
        name = lg["name"] if lg else max(ev["names"], key=len)
        r = blank_run(name)
        r["has_log"] = lg is not None
        src_lines = lg["lines"] if lg else []
        starts, dones = ev["starts"], ev["dones"]
        done = bool(dones) and (not starts or dones[-1][0] >= starts[-1][0])
        if lg:
            first = lg["stamps"][0][0] if lg["stamps"] else None
            r.update(path=str(lg["path"]), changed=lg["changed"], lines=src_lines, future=lg["future"], source="log")
            r["start"] = starts[0][0] if starts and (not first or starts[0][0] <= first) else (first or (starts[0][0] if starts else None))
            r["last"] = lg["stamps"][-1][0] if lg["stamps"] else lg["changed"]
            sleeps = [int(m.group(1)) if m.group(1) else 60 for m in SLEEP_RE.finditer(lg["text"])]
            r.update(idle_s=sum(sleeps), sleep_hits=len(sleeps), limit_hits=len(LIMIT_RE.findall(lg["text"])))
            blocked = "BLOCKED" in lg["last_line"].upper()
        else:
            src = next((l for l in logs if l["path"].name.startswith(starts[-1][2])), None) if starts else None
            src = src or (logs[0] if logs else None)
            r["start"] = starts[0][0] if starts else None
            r["path"] = str(src["path"]) if src else ""
            r["changed"] = src["changed"] if src else None
            r["last"] = starts[-1][0] if starts else None
            if src and starts:
                # the chain log's lines from this step's start line onwards
                idx = next((i for i, l in enumerate(src["lines"]) if l == starts[-1][1]), 0)
                r["lines"] = src["lines"][idx:]
            blocked = False
        if done:
            # the first done after the last start: a later "(R28 RUN DONE)" quoted as another run's start condition is not its end
            r["state"], r["end"] = "DONE", min(t for t, _, _ in dones if not starts or t >= starts[-1][0])
        elif k == active_key:
            r["state"], r["end"] = ("BLOCKED" if blocked else "running"), now
        elif blocked:
            r["state"], r["end"] = "BLOCKED", r["last"]
        elif starts and lg and now - lg["changed"] <= PARALLEL_WINDOW:
            r["state"], r["end"] = "running", now  # started, not done, still writing: runs side by side
        elif lg and not starts and now - lg["changed"] <= RUNNING_WINDOW:
            r["state"], r["end"] = "running", now
        else:
            r["state"], r["end"] = "waiting", r["last"]
        if r["start"] and r["end"]:
            r["elapsed"] = r["end"] - r["start"]
        # deadline from this run's own start line (or the chain log's start line for it)
        for line in (r["lines"][:6] if lg else r["lines"][:1]):
            d, s = DEADLINE.search(line), STOP_START.search(line)
            if d and r["start"]:
                r["deadline"] = clock_after(d.group(1), r["start"])
            if s and r["start"]:
                r["stop_start"] = clock_after(s.group(1), r["start"])
        runs.append(r)
        seen.add(k)

    # run sources: the queue runner's runs.csv settles and adds runs, discovery adds interactive runs by their log
    cfg, _ = load_costs(env)
    apply_queue_rows(runs, read_runs_csv(env), now)
    discover_runs(env, cfg, runs, now)
    seen |= {r["key"] for r in runs}

    # queued: only the newest OWNER CHAIN UPDATE plus handoff\QUEUE.txt
    newest =max(updates, key=lambda u: u[0]) if updates else None
    queued_names = [(n, newest[0]) for n in newest[1]] if newest else []
    qfile = handoff / "QUEUE.txt"
    if qfile.is_file():
        for line in read_text(qfile).splitlines():
            line = line.strip()
            if line and not line.startswith("#"):
                p = re.search(r"PROMPT_([\w-]+)\.txt", line)
                queued_names.append((p.group(1) if p else line, env.mtime(qfile)))
    builds = [(name_tokens(p.name), env.mtime(p)) for p in (env.root / "Builds").iterdir() if p.is_dir()]         if (env.root / "Builds").is_dir() else []
    queue = []
    for name, since in queued_names:
        k = key_of(name)
        if k in seen or name.lower() in {q["name"].lower() for q in queue}:
            continue
        if output_exists(name, since, builds):  # its build folder is already there: done, not queued
            continue
        q = blank_run(name)
        pf = handoff / f"PROMPT_{name}.txt"
        if pf.is_file():
            q["prompt"] = pf
        queue.append(q)
    runs += queue

    live = [r for r in runs if r["state"] == "running"] or [r for r in runs if r["changed"] and r["state"] != "stale"]
    # a step the queue runner recorded as ended (R31, FAILED) is no longer the active run
    active = next((r for r in runs if r["key"] == active_key and r["state"] != "stale"), None) or (
        max(live, key=lambda r: r["changed"]) if live else None)
    order = {"running": 0, "BLOCKED": 1, "waiting": 2, "stale": 3, "queued": 4, "DONE": 5}
    runs.sort(key=lambda r: (order[r["state"]], -(r["changed"].timestamp() if r["changed"] else 0)))
    chain_no_new = no_new[max(range(len(no_new)), key=lambda i: (no_new[i][0], i))][1] if no_new else None
    return {"runs": runs, "active": active, "tail": active["lines"][-4:] if active else [],
            "queue": queue, "no_new_after": chain_no_new, "hint": ""}


def plan_chain(rr: dict, now: datetime) -> dict:
    """ETA for the active run and every queued one, from the median duration of the last 8 finished
    runs (or the run prompt's own timebox). A queued run whose predicted start is after the
    'no new run after' time is marked late: it will not start."""
    done = sorted((r for r in rr["runs"] if r["state"] == "DONE" and r["start"] and r["end"]), key=lambda r: r["end"])
    durs = [r["end"] - r["start"] for r in done[-8:]]
    durs = [x for x in durs if timedelta(minutes=5) <= x <= timedelta(hours=24)]
    med = statistics.median(durs) if durs else timedelta(hours=1)
    cursor = now
    act = rr["active"]
    act_eta = None
    if act and act["state"] in ("running", "BLOCKED") and act["start"]:
        act_eta = max(now, act["start"] + med)
        cursor = act_eta
    rows = []
    for q in rr["queue"]:
        dur = (prompt_duration(q["prompt"]) if q["prompt"] else None) or med
        start, end = cursor, cursor + dur
        late = bool(rr["no_new_after"] and start > rr["no_new_after"])
        rows.append({"name": q["name"], "start": start, "end": end, "dur": dur, "late": late})
        cursor = end
    return {"median": med, "n_finished": len(durs), "done": done[-4:], "active_eta": act_eta, "queued": rows,
            "no_new_after": rr["no_new_after"]}


# ---------------------------------------------------------------- user actions

ACTIONS_HEAD = re.compile(r"^\s*(?:#+\s*)?\**\s*(?:\d+\.\s*)?USER ACTIONS\b.*$", re.I)
HEADING = re.compile(r"^\s*(#+\s|\*\*[A-Z][A-Z /]+\*\*\s*$|[A-Z][A-Z /]{3,}:\s*$)")
EMPTY_ITEM = re.compile(r"^[\s\-*>(]*(none( yet)?|nothing( yet)?|n/?a|no action[s]?( needed)?|-)?[.)]*\s*$", re.I)
NOT_AN_ACTION = re.compile(r"^[\s\-*>]*(?:OWNER\b|RUN DONE\b|\d{1,2}:\d{2}\b|\d{4}-\d{2}-\d{2}\b)", re.I)
BLOCKER = re.compile(r"block", re.I)


def collect_actions(env: Env) -> dict:
    handoff = env.root / "handoff"
    if not handoff.is_dir():
        return {"items": [], "hint": f"{handoff} not found"}
    items = []
    # A run's REPORT.md replaces its BOARD.md: the board is the running copy of the same section.
    files = list(handoff.glob("*/REPORT.md")) + [b for b in handoff.glob("*/BOARD.md") if not (b.parent / "REPORT.md").is_file()]
    for p in files:
        lines, grab, body = read_text(p).splitlines(), False, []
        for line in lines:
            if ACTIONS_HEAD.match(line):
                grab = True
                rest = line.split(":", 1)[1].strip() if ":" in line else ""
                if rest and not NOT_AN_ACTION.match(rest) and not EMPTY_ITEM.match(rest):
                    body.append(rest)
                continue
            if grab and HEADING.match(line):
                grab = False
            if grab and not EMPTY_ITEM.match(line) and not NOT_AN_ACTION.match(line):
                body.append(line.strip())
        if body:
            items.append({"run": p.parent.name, "file": p.name, "time": env.mtime(p),
                          "lines": body, "blocker": any(BLOCKER.search(b) for b in body)})
    items.sort(key=lambda i: i["time"], reverse=True)
    return {"items": items, "hint": ""}


# ---------------------------------------------------------------- git

BUNDLE_HEADS: dict = {}


def bundle_heads(git: str, path: Path) -> list[tuple[str, str]]:
    key = (str(path), path.stat().st_mtime)
    if key not in BUNDLE_HEADS:
        code, out = run([git, "bundle", "list-heads", str(path)])
        BUNDLE_HEADS[key] = [(l.split()[1].removeprefix("refs/heads/"), l.split()[0]) for l in out.splitlines()
                             if code == 0 and len(l.split()) == 2 and l.split()[1].startswith("refs/heads/")]
    return BUNDLE_HEADS[key]


def collect_bundles(env: Env, git: str) -> list[dict]:
    """handoff\\*.bundle whose branch is not on origin yet (as of the last fetch): the fetch and push chore."""
    handoff = env.root / "handoff"
    _, refs = run([git, "for-each-ref", "--format=%(refname:short) %(objectname)", "refs/remotes/origin"], env.game)
    origin = {l.split()[0].removeprefix("origin/"): l.split()[1] for l in refs.splitlines() if len(l.split()) == 2}
    out = []
    for p in sorted(handoff.glob("*.bundle"), key=lambda p: p.stat().st_mtime, reverse=True):
        for branch, sha in bundle_heads(git, p):
            on = origin.get(branch)
            if on and (on == sha or run([git, "merge-base", "--is-ancestor", sha, on], env.game)[0] == 0):
                continue
            out.append({"file": p.name, "branch": branch, "sha": sha[:7], "when": env.mtime(p),
                        "why": "not on origin" if not on else "origin is behind"})
    return out


def collect_git(env: Env) -> dict:
    if env.mock:
        g = json.loads((HERE / "mock" / "git.json").read_text(encoding="utf-8"))
        g["bundles"] = [{**b, "when": parse_time(b["when"])} for b in g.get("bundles", [])]
        return {**g, "hint": ""}
    git = shutil.which("git")
    if not git:
        return {"hint": "git not on PATH: install Git for Windows", "main": "", "merged": [],
                "unmerged": [], "recent": [], "bundles": []}
    if not (env.game / ".git").exists():
        return {"hint": f"{env.game} is not a git checkout", "main": "", "merged": [],
                "unmerged": [], "recent": [], "bundles": []}
    base = "origin/main"
    code, out = run([git, "log", "-2", "--format=%h|%cr|%s", base], env.game)
    if code != 0:
        base = "main"
        code, out = run([git, "log", "-2", "--format=%h|%cr|%s", base], env.game)
    recent = [dict(zip(("hash", "when", "subject"), l.split("|", 2))) for l in out.splitlines() if l]
    fmt = "--format=%(refname:short)|%(committerdate:relative)"

    def branches(flag: str) -> list[dict]:
        _, o = run([git, "branch", "-r", flag, base, "--sort=-committerdate", fmt], env.game)
        rows = [l.split("|", 1) for l in o.splitlines() if l and "->" not in l]
        return [{"name": n, "when": w} for n, w in rows if n not in ("origin/main", "origin/HEAD", "origin")]

    return {"main": f"{recent[0]['hash']} {recent[0]['subject']}" if recent else "",
            "recent": recent, "merged": branches("--merged"), "unmerged": branches("--no-merged"),
            "bundles": collect_bundles(env, git), "hint": ""}


# ---------------------------------------------------------------- Unity process

def collect_unity(env: Env) -> dict:
    if env.mock:
        u = json.loads((HERE / "mock" / "mock.json").read_text(encoding="utf-8")).get("unity")
        return {"procs": [{"pid": u["pid"], "start": parse_time(u["start"])}] if u else []}
    procs = []
    try:
        import psutil  # optional, faster than spawning PowerShell
        for p in psutil.process_iter(["name", "pid", "create_time"]):
            if (p.info["name"] or "").lower() in ("unity.exe", "unity"):
                procs.append({"pid": p.info["pid"], "start": datetime.fromtimestamp(p.info["create_time"])})
        return {"procs": procs}
    except ImportError:
        pass
    if sys.platform == "win32":
        code, out = run(["powershell", "-NoProfile", "-Command",
                         "Get-Process -Name Unity -ErrorAction SilentlyContinue | "
                         "ForEach-Object { \"$($_.Id)|$($_.StartTime.ToString('o'))\" }"])
    else:
        code, out = run(["ps", "-eo", "pid=,lstart=,comm="])
        out = "\n".join(l for l in out.splitlines() if l.strip().lower().endswith("unity"))
        procs = [{"pid": int(l.split()[0]), "start": datetime.strptime(" ".join(l.split()[1:6]), "%a %b %d %H:%M:%S %Y")}
                 for l in out.splitlines()]
        return {"procs": procs}
    for l in out.splitlines():
        if "|" in l:
            pid, st = l.split("|", 1)
            procs.append({"pid": int(pid), "start": parse_time(st.strip())})
    return {"procs": procs}


# ---------------------------------------------------------------- health

def in_quiet_window(t: datetime) -> bool:
    return QUIET_FROM <= t.time() < QUIET_TO


def collect_health(env: Env, unity: dict) -> dict:
    now = env.now()
    root = env.root
    out = {"unity_running": bool(unity["procs"]), "quiet_now": in_quiet_window(now)}
    nxt = datetime.combine(now.date(), QUIET_FROM)
    out["quiet_in"] = (nxt if nxt > now else nxt + timedelta(days=1)) - now
    # newest build-check result: a tmp\*build-check* log, or the STATUS.md every build-check writes
    # into the checkout it ran in (that is where the result really lands).
    cands = [p for p in (root / "tmp").glob("*build*check*") if p.is_file()] if (root / "tmp").is_dir() else []
    cands += [p for p in root.glob("Game*/STATUS.md")]
    best = None
    for p in cands:
        try:
            t = env.mtime(p)
        except OSError:
            continue
        if best is None or t > best[0]:
            best = (t, p)
    out["build_check"] = None
    if best:
        text = read_text(best[1])
        m = re.search(r"Exit code (\d+),\s*\**(\d+) failure", text)
        code = int(m.group(1)) if m else (0 if re.search(r"exit 0\b|RESULT: all", text) else 1 if re.search(r"exit [1-9]|FAILED", text) else None)
        mode = re.search(r"Mode: \**(\w+)", text)
        where = best[1].parent.name if best[1].name == "STATUS.md" else best[1].name
        out["build_check"] = {"ok": code == 0 if code is not None else None, "when": best[0], "where": where,
                              "mode": mode.group(1) if mode else "", "failures": int(m.group(2)) if m else None}
    builds = root / "Builds"
    folders = [p for p in builds.iterdir() if p.is_dir()] if builds.is_dir() else []
    newest = max(folders, key=env.mtime, default=None)
    out["build"] = {"name": newest.name, "when": env.mtime(newest)} if newest else None
    fb = root / "playtest-feedback"
    out["feedback"] = None
    if fb.is_dir():
        files = [p for p in fb.rglob("*") if p.is_file()]
        marks = [p for p in files if re.match(r"(DIGEST|ANALY[SZ])", p.name, re.I)]
        mark = max((env.mtime(p) for p in marks), default=None)
        fresh = [p for p in files if p not in marks and p.name.upper() != "README.MD" and not p.name.upper().startswith("EXAMPLE")
                 and (mark is None or env.mtime(p) > mark)]
        out["feedback"] = {"new": len(fresh), "since": mark}
    return out


# ---------------------------------------------------------------- ccusage

def ccusage_cmd() -> list[str] | None:
    exe = shutil.which("ccusage")
    if exe:
        return [exe]
    npx = shutil.which("npx")
    return [npx, "--yes", "ccusage@latest"] if npx else None


def ccusage_json(env: Env, name: str, args: list[list[str]]) -> dict | None:
    if env.mock:
        p = HERE / "mock" / f"ccusage_{name}.json"
        return json.loads(p.read_text(encoding="utf-8")) if p.exists() else None
    base = ccusage_cmd()
    if not base:
        return None
    for a in args:
        code, out = run(base + a)
        if code == 0 and out.strip().startswith("{"):
            try:
                return json.loads(out)
            except json.JSONDecodeError:
                pass
    return None


USAGE_TTL = 0.0          # seconds to reuse a live ccusage result; the web server sets it, the TUI leaves it off
_USAGE_MEMO: dict = {}


def collect_usage(env: Env) -> dict:
    if USAGE_TTL > 0 and not env.mock:
        hit = _USAGE_MEMO.get("v")
        if hit and time.monotonic() - hit[0] < USAGE_TTL:
            return hit[1]
        out = _collect_usage(env)
        _USAGE_MEMO["v"] = (time.monotonic(), out)
        return out
    return _collect_usage(env)


def _collect_usage(env: Env) -> dict:
    daily = ccusage_json(env, "daily", [["daily", "--json", "--breakdown"], ["daily", "--json"]])
    blocks = ccusage_json(env, "blocks", [["blocks", "--json", "--token-limit", "max"], ["blocks", "--json"]])
    if daily is None and blocks is None:
        return {"hint": "ccusage not found: install Node.js, then `npm i -g ccusage`",
                "days": [], "blocks": [], "total": 0.0}
    days = []
    for d in (daily or {}).get("daily", []):
        models = {}
        for b in d.get("modelBreakdowns") or []:
            models[b.get("modelName", "?")] = models.get(b.get("modelName", "?"), 0) + float(b.get("cost", 0) or 0)
        days.append({"date": date.fromisoformat(str(d.get("date", d.get("period", "")))[:10]),
                     "cost": float(d.get("totalCost", 0) or 0), "models": models,
                     "cache_read": int(d.get("cacheReadTokens", 0) or 0),
                     "tokens": int(d.get("totalTokens", 0) or 0)})
    bl = []
    for b in (blocks or {}).get("blocks", []):
        if b.get("isGap"):
            continue
        lim = b.get("tokenLimitStatus") or {}
        bl.append({"start": parse_time(b.get("startTime")), "end": parse_time(b.get("endTime")),
                   "actual_end": parse_time(b.get("actualEndTime")), "active": bool(b.get("isActive")),
                   "cost": float(b.get("costUSD", 0) or 0), "tokens": int(b.get("totalTokens", 0) or 0),
                   "burn": (b.get("burnRate") or {}).get("costPerHour"),
                   "projection": (b.get("projection") or {}).get("totalCost"),
                   "limit_pct": lim.get("percentUsed"), "limit_status": lim.get("status")})
    total = float(((daily or {}).get("totals") or {}).get("totalCost", 0) or sum(d["cost"] for d in days))
    return {"hint": "" if daily is not None else "ccusage daily failed", "days": days, "blocks": bl, "total": total}


# ---------------------------------------------------------------- local usage: block, run cost

def collect_local(env: Env, costs_cfg: dict) -> dict:
    now = env.now()
    if not env.projects or not env.projects.is_dir():
        return {"hint": f"{env.projects} not found", "msgs": [], "events": [], "table": localusage.price_table(None),
                "days": {}, "sessions": {}}
    table = localusage.price_table(costs_cfg.get("pricing"))
    scan = localusage.scan(env.projects, env.cache, now.timestamp(), HISTORY_DAYS)
    days: dict[date, float] = {}
    for m in scan["msgs"]:
        d = localusage.to_dt(m.ts).date()
        days[d] = days.get(d, 0.0) + localusage.msg_cost(m, table)
    return {"hint": "", "msgs": scan["msgs"], "events": scan["events"], "table": table, "days": days,
            "scanned_bytes": scan["scanned_bytes"]}


def current_block(env: Env, local: dict, usage: dict, state: dict) -> dict | None:
    """The 5h window. A manual end time (key b) wins until it passes; otherwise the window is
    reconstructed from the exact first message time in the local transcripts (no flooring)."""
    now = env.now()
    end = parse_time(state.get("block_end_manual"))
    if end and end > now:
        start, src = end - timedelta(hours=5), "manual"
    else:
        ws = localusage.window_start(local["events"], now.timestamp()) if local.get("events") else None
        if ws is None:
            return None
        start = localusage.to_dt(ws)
        end, src = start + timedelta(hours=5), "est, local logs only"
    cost, tokens = 0.0, 0
    s_ts, e_ts = start.timestamp(), end.timestamp()
    for m in local["msgs"]:
        if s_ts <= m.ts <= e_ts:
            cost += localusage.msg_cost(m, local["table"])
            tokens += m.inp + m.out + m.cc5 + m.cc1 + m.cr
    age = now - start
    young = age < YOUNG_BLOCK
    hours = age.total_seconds() / 3600
    burn = None if young or hours <= 0 else cost / hours
    active = next((b for b in usage["blocks"] if b["active"]), None)
    return {"start": start, "end": end, "src": src, "cost": cost, "tokens": tokens, "age": age, "young": young,
            "burn": burn, "projection": None if burn is None else burn * 5,
            "limit_pct": None if young or not active else active["limit_pct"]}


FAMILIES = ("opus", "sonnet", "haiku")


def attribute_runs(env: Env, local: dict, runs: list[dict], cfg: dict) -> dict:
    """Cost per run from the transcripts. A message belongs to a run when it falls in the run's
    start..end window. The cwd is the tie breaker (Game-r26lead and Game-r26a belong to R26 even where
    two windows meet) and the filter: only sessions in the play slot or a run worktree under the
    Emberfall root count, so a cockpit session or another project running at the same time does not."""
    cwd_re = re.compile(cfg.get("run_cwd_regex", RUN_CWD), re.I)
    root = env.cwd_root.lower().rstrip("/")
    wins = [r for r in runs if r["start"] and r["end"] and r["state"] != "queued"]
    out = {r["name"]: {"cost": 0.0, "opus": 0.0, "n": 0, "ts": []} for r in wins}
    for m in local.get("msgs", []):
        cwd = m.cwd.replace("\\", "/").rstrip("/")
        if not cwd.lower().startswith(root + "/") and cwd.lower() != root:
            continue
        base = cwd.rsplit("/", 1)[-1]
        if not cwd_re.match(base):
            continue
        t = localusage.to_dt(m.ts)
        inside = [r for r in wins if r["start"] <= t <= r["end"]]
        pick = None
        num = re.match(r"(?i)game-r(\d+)", base)
        if num:
            pool = [r for r in wins if re.fullmatch(rf"R{int(num.group(1))}[A-Z]?", r["key"])]
            hit = [r for r in pool if r in inside]
            pick = hit[0] if hit else None
            if pick is None and pool and not inside:
                near = min(pool, key=lambda r: min(abs((t - r["start"]).total_seconds()), abs((t - r["end"]).total_seconds())))
                if min(abs((t - near["start"]).total_seconds()), abs((t - near["end"]).total_seconds())) <= 7200:
                    pick = near
        if pick is None and inside:
            pick = max(inside, key=lambda r: r["start"])
        if pick is None:
            continue
        c = localusage.msg_cost(m, local["table"])
        o = out[pick["name"]]
        o["cost"] += c
        o["n"] += 1
        o["ts"].append(m.ts)
        if localusage.family(m.model) == "opus":
            o["opus"] += c
    return out


def idle_seconds(ts: list[float], start: datetime, end: datetime) -> float:
    """Time inside a run with no transcript message from any of its sessions for IDLE_GAP_S or more,
    counted from the run's start to its end (waiting on a build, a sub agent, the owner or a limit)."""
    pts = sorted([start.timestamp(), end.timestamp()] + [t for t in ts if start.timestamp() <= t <= end.timestamp()])
    return sum(b - a for a, b in zip(pts, pts[1:]) if b - a >= IDLE_GAP_S)


def per_run_rows(runs: list[dict], attributed: dict) -> list[dict]:
    rows = []
    for r in runs:
        a = attributed.get(r["name"])
        if not a or a["cost"] <= 0 or not r["start"]:
            continue
        mins = r["elapsed"].total_seconds() / 60 if r["elapsed"] else 0
        idle = 0.0
        if mins:
            gaps = idle_seconds(a["ts"], r["start"], r["end"]) if a.get("ts") and r["end"] else 0.0
            idle = min(1.0, max(r["idle_s"], gaps) / 60 / mins)
        rows.append({"name": r["name"], "state": r["state"], "cost": a["cost"], "sessions": a["n"],
                     "minutes": mins, "per_h": a["cost"] / (mins / 60) if mins else 0.0,
                     "opus_share": a["opus"] / a["cost"] if a["cost"] else 0.0, "idle_share": idle,
                     "start": r["start"], "elapsed": r["elapsed"], "run": r})
    rows.sort(key=lambda c: c["start"], reverse=True)
    return rows


def local_today(local: dict, now: datetime) -> dict:
    """Today's cost by family and the cache-read tokens per session (for the cache tip)."""
    fam = {f: 0.0 for f in FAMILIES}
    sess: dict[str, dict] = {}
    start = datetime.combine(now.date(), datetime.min.time()).timestamp()
    for m in local.get("msgs", []):
        if m.ts < start:
            continue
        f = localusage.family(m.model)
        if f in fam:
            fam[f] += localusage.msg_cost(m, local["table"])
        s = sess.setdefault(m.sess, {"cr": 0, "cost": 0.0, "folder": m.cwd.replace("\\", "/").rstrip("/").rsplit("/", 1)[-1]})
        s["cr"] += m.cr
        s["cost"] += localusage.msg_cost(m, local["table"])
    return {"families": fam, "sessions": sess}


# ---------------------------------------------------------------- project cost

def months_between(start: str, end: date) -> int:
    y, m = (int(x) for x in start.split("-")[:2])
    return max(0, (end.year - y) * 12 + end.month - m + 1)


def ledger_entries(obj) -> list[tuple[float, str]]:
    """(usd, date) pairs from a ledger.json of unknown shape: a list of entries, or a dict
    holding one. An entry's cost is the first of the known cost keys it has."""
    keys = ("cost_usd", "costUSD", "usd", "cost", "price_usd", "price", "amount_usd", "amount")
    if isinstance(obj, dict):
        for k in ("entries", "predictions", "items", "runs", "ledger", "jobs"):
            if isinstance(obj.get(k), list):
                return ledger_entries(obj[k])
        for k in ("total_usd", "total_cost", "total"):
            if isinstance(obj.get(k), (int, float)):
                return [(float(obj[k]), "")]
        return []
    out = []
    for e in obj if isinstance(obj, list) else []:
        if not isinstance(e, dict):
            continue
        usd = next((e[k] for k in keys if isinstance(e.get(k), (int, float))), None)
        if usd is None:
            continue
        when = str(e.get("date") or e.get("created_at") or e.get("time") or e.get("timestamp") or "")
        out.append((float(usd), when))
    return out


def ledger_files(env: Env) -> list[Path]:
    """ui-gen/ledger*.json and ui-gen/*/ledger*.json."""
    ui = env.root / "ui-gen"
    if not ui.is_dir():
        return []
    found = {p for pat in ("ledger*.json", "*/ledger*.json") for p in ui.glob(pat) if p.is_file()}
    return sorted(found, key=lambda p: p.as_posix().lower())


def generator_caps(ui: Path, rel: str) -> dict:
    """Best effort: the caps and price a generator script under ui-gen/tools states for the ledger `rel`
    (path below ui-gen). It must name the ledger file in a LEDGER line and carry MAX_IMAGES / MAX_COST."""
    for script in sorted((ui / "tools").glob("*.py")) if (ui / "tools").is_dir() else []:
        text = read_text(script)
        if not re.search(rf"^LEDGER\s*=.*[\"']{re.escape(rel)}[\"']", text, re.M):
            continue
        m = re.search(r"^MAX_IMAGES\s*,\s*MAX_COST\s*,\s*PRICE\s*=\s*(\d+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)", text, re.M)
        if m:
            return {"images": int(m.group(1)), "cost": float(m.group(2)), "price": float(m.group(3))}
        imgs = re.search(r"^MAX_IMAGES\s*=\s*(\d+)", text, re.M)
        cost = re.search(r"^MAX_COST\s*=\s*([\d.]+)", text, re.M)
        price = re.search(r"^PRICE\s*=\s*([\d.]+)", text, re.M)
        if imgs or cost:
            return {"images": int(imgs.group(1)) if imgs else None, "cost": float(cost.group(1)) if cost else None,
                    "price": float(price.group(1)) if price else None}
    return {}


def ledger_run_start(path: Path, runs: list[dict]) -> datetime | None:
    """Start of the run a ledger belongs to: ledger_r43.json -> the run whose key is R43."""
    m = re.search(r"ledger[_\-]([A-Za-z]\d+[A-Za-z]?)", path.stem, re.I)
    if not m:
        return None
    hit = next((r for r in runs if r["key"] == m.group(1).upper() and r["start"]), None)
    return hit["start"] if hit else None


def collect_ledgers(env: Env, runs: list[dict] | None = None, cfg: dict | None = None) -> dict:
    """Every Replicate spend ledger under ui-gen. Shape: {"images": n, "cost": usd}, or the older list-of-entries
    shape. An unreadable one is listed with ok False and counts for nothing. A ledger changed in the last
    LEDGER_LIVE is generating, with images per minute taken from the start of its run (or, failing that, the
    time the file was created). The total is an estimate: the price per image is assumed, Replicate's billing
    is the truth."""
    cfg = cfg or {}
    now = env.now()
    default_price = cfg.get("replicate_price_per_image")
    default_price = float(default_price) if isinstance(default_price, (int, float)) and default_price > 0 else IMAGE_PRICE
    ui = env.root / "ui-gen"
    rows, total_usd, total_images = [], 0.0, 0
    for p in ledger_files(env):
        rel = p.relative_to(ui).as_posix()
        try:
            changed = env.mtime(p)
            obj = json.loads(p.read_text(encoding="utf-8-sig"))
        except (OSError, ValueError):
            rows.append({"name": rel, "path": str(p), "ok": False, "images": 0, "cost": 0.0, "entries": [],
                         "cap_images": None, "cap_cost": None, "price": default_price, "live": False, "ipm": None,
                         "changed": None})
            continue
        if isinstance(obj, dict) and isinstance(obj.get("cost"), (int, float)) and not isinstance(obj.get("cost"), bool):
            images = obj.get("images") if isinstance(obj.get("images"), (int, float)) else 0
            entries = [(float(obj["cost"]), changed.date().isoformat())]
        else:
            entries = ledger_entries(obj)
            images = sum(e.get("images", 0) for e in (obj.get("entries") if isinstance(obj, dict) else obj) or []
                         if isinstance(e, dict) and isinstance(e.get("images"), (int, float))) or len(entries)
        cost = sum(u for u, _ in entries)
        caps = generator_caps(ui, rel)
        price = obj.get("price") if isinstance(obj, dict) and isinstance(obj.get("price"), (int, float)) else (
            caps.get("price") or default_price)
        live = timedelta(0) <= now - changed <= LEDGER_LIVE
        ipm = None
        if live and images:
            began = ledger_run_start(p, runs or [])
            if not began and sys.platform == "win32":
                try:
                    created = datetime.fromtimestamp(p.stat().st_ctime)
                    began = created if created < changed else None
                except OSError:
                    began = None
            if began and changed > began:
                ipm = images / max(1.0, (changed - began).total_seconds() / 60)
        rows.append({"name": rel, "path": str(p), "ok": True, "images": int(images), "cost": cost, "entries": entries,
                     "cap_images": caps.get("images"), "cap_cost": caps.get("cost"), "price": price, "live": live,
                     "ipm": ipm, "changed": changed})
        total_usd += cost
        total_images += int(images)
    return {"ledgers": rows, "total_usd": total_usd, "total_images": total_images, "price": default_price}


def load_costs(env: Env) -> tuple[dict, str]:
    try:
        return json.loads(env.costs.read_text(encoding="utf-8")), ""
    except (OSError, json.JSONDecodeError) as e:
        return {}, f"costs.json unreadable: {e.__class__.__name__}"


def api_credit(cfg: dict, now: datetime) -> dict | None:
    c = cfg.get("api_credit") or {}
    left = c.get("remaining_usd")
    if not isinstance(left, (int, float)) or left <= 0:
        return None
    exp = None
    try:
        exp = date.fromisoformat(str(c.get("expires", ""))[:10])
    except ValueError:
        pass
    return {"usd": float(left), "expires": exp, "days": (exp - now.date()).days if exp else None}


def collect_project_cost(env: Env, usage_total_usd: float, cfg: dict | None = None, hint: str = "",
                         ledgers: dict | None = None) -> dict:
    if cfg is None:
        cfg, hint = load_costs(env)
    rate = float(cfg.get("usd_to_eur", 0.92))
    today = env.now().date()
    month = today.strftime("%Y-%m")
    cats: dict[str, list[float]] = {}  # category -> [total, this month]

    def add(cat: str, eur: float, month_eur: float) -> None:
        c = cats.setdefault(cat, [0.0, 0.0])
        c[0] += eur
        c[1] += month_eur

    starts = []
    for s in cfg.get("subscriptions", []):
        if not s.get("start_month") or not s.get("monthly_eur"):
            continue
        stop = today
        if s.get("end_month"):
            y, m = (int(x) for x in s["end_month"].split("-")[:2])
            stop = min(today, date(y, m, 1))
        n = months_between(s["start_month"], stop)
        price = float(s["monthly_eur"])
        add("Subscriptions", n * price, price if stop.strftime("%Y-%m") == month and n > 0 else 0.0)
        starts.append(s["start_month"])
    for t in cfg.get("api_topups", []):
        if t.get("eur"):
            eur = float(t["eur"])
            add("API credit", eur, eur if str(t.get("date", "")).startswith(month) else 0.0)
            starts.append(str(t.get("date", ""))[:7])
    for o in cfg.get("one_off", []):
        if o.get("eur"):
            eur = float(o["eur"])
            add(o.get("category") or "Other", eur, eur if str(o.get("date", "")).startswith(month) else 0.0)
            starts.append(str(o.get("date", ""))[:7])
    rep_files = 0
    for lg in (ledgers if ledgers is not None else collect_ledgers(env, [], cfg))["ledgers"]:
        if not lg["ok"]:
            continue
        rep_files += 1
        for usd, when in lg["entries"]:
            add("Replicate", usd * rate, usd * rate if when.startswith(month) else 0.0)
            if when[:7]:
                starts.append(when[:7])
    total = sum(c[0] for c in cats.values())
    claude_paid = sum(cats.get(k, [0, 0])[0] for k in ("Subscriptions", "API credit"))
    equiv = usage_total_usd * rate
    return {"cats": cats, "total": total, "month": sum(c[1] for c in cats.values()),
            "since": min((s for s in starts if s), default=""), "equiv_eur": equiv,
            "claude_paid": claude_paid, "saved": equiv - claude_paid, "rate": rate,
            "ledgers": rep_files, "filled": total > 0, "hint": hint}


# ---------------------------------------------------------------- everything

def collect(env: Env) -> dict:
    now = env.now()
    cfg, cost_hint = load_costs(env)
    runs = collect_runs(env)
    usage = collect_usage(env)
    local = collect_local(env, cfg)
    state = load_state(env)
    chain = plan_chain(runs, now) if not runs["hint"] else {}
    attributed = attribute_runs(env, local, runs["runs"], cfg)
    per_run = per_run_rows(runs["runs"], attributed)
    finished = [c["cost"] for c in per_run if c["state"] == "DONE"]
    active = runs["active"]
    unity = collect_unity(env)
    ledgers = collect_ledgers(env, runs["runs"], cfg)
    return {"env": env, "now": now, "runs": runs, "chain": chain, "usage": usage, "local": local,
            "block": current_block(env, local, usage, state), "state": state, "per_run": per_run,
            "median_run": statistics.median(finished) if finished else 0.0,
            "active_cost": attributed.get(active["name"], {}).get("cost", 0.0) if active else 0.0,
            "today": local_today(local, now), "actions": collect_actions(env), "git": collect_git(env),
            "unity": unity, "health": collect_health(env, unity),
            "project": collect_project_cost(env, usage["total"], cfg, cost_hint, ledgers),
            "ledgers": ledgers, "queue_lock": read_queue_lock(env),
            "api_credit": api_credit(cfg, now),
            "claude_md_words": len(read_text(env.claude_md).split())}
