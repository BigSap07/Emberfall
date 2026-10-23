"""Read-only collectors for the cockpit.

Everything here only reads files or runs `ccusage`, `git log`, `git branch` and a process
list. Nothing is written anywhere. Every collector returns plain dicts and lists, and a
`hint` string instead of raising when its source is missing.
"""

from __future__ import annotations

import json
import os
import re
import shutil
import statistics
import subprocess
import sys
from dataclasses import dataclass, field
from datetime import date, datetime, timedelta
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
LIVE_ROOT = Path(os.environ.get("EMBERFALL_ROOT", r"D:\Emberfall"))
RUNNING_WINDOW = timedelta(minutes=15)
SUB_TIMEOUT = 60


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

    def now(self) -> datetime:
        return self.clock or datetime.now()

    def mtime(self, p: Path) -> datetime:
        rel = p.relative_to(self.root).as_posix() if p.is_relative_to(self.root) else str(p)
        if rel in self.mtimes:
            return parse_time(self.mtimes[rel])
        return datetime.fromtimestamp(p.stat().st_mtime)


def live_env() -> Env:
    return Env(root=LIVE_ROOT, game=LIVE_ROOT / "Game", claude_md=REPO / "CLAUDE.md",
               costs=HERE / "costs.json")


def mock_env() -> Env:
    m = HERE / "mock"
    meta = json.loads((m / "mock.json").read_text(encoding="utf-8"))
    return Env(root=m / "Emberfall", game=m / "Emberfall" / "Game", claude_md=REPO / "CLAUDE.md",
               costs=m / "costs.json", mock=True, clock=parse_time(meta["now"]),
               mtimes=meta.get("mtimes", {}))


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


# ---------------------------------------------------------------- runs

TS_RE = re.compile(r"(?:(\d{4}-\d{2}-\d{2})[ T])?\b(\d{1,2}):(\d{2})(?::(\d{2}))?\b")
CHAIN_RE = re.compile(r"^\s*[-*]?\s*CHAIN\b[:\s]*(.+)$", re.I | re.M)
SLEEP_RE = re.compile(r"\b(?:Start-Sleep|sleep)\s+(?:-s(?:econds)?\s+)?(\d+)|\bpoll(?:ing|ed)?\b|\bwaiting for\b", re.I)
LIMIT_RE = re.compile(r"usage limit reached|limit reached|rate limit", re.I)


def line_times(text: str, anchor: date) -> list[tuple[datetime, str]]:
    """Every timestamped line. Lines with only HH:MM take their date from the last full date
    seen (or the anchor) and roll over midnight when the clock goes backwards."""
    out, day, last = [], anchor, None
    for line in text.splitlines():
        m = TS_RE.search(line)
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


def collect_runs(env: Env) -> dict:
    tmp, handoff = env.root / "tmp", env.root / "handoff"
    if not tmp.is_dir():
        return {"runs": [], "active": None, "tail": [], "hint": f"{tmp} not found"}
    now = env.now()
    runs, chained = [], []
    for p in sorted(tmp.glob("*_running_log.md")):
        name = p.name[: -len("_running_log.md")]
        text = read_text(p)
        changed = env.mtime(p)
        # The file date anchors HH:MM-only stamps. If that puts the last stamp after the file's
        # own change time, the run started the day before.
        stamps = line_times(text, changed.date())
        if stamps and stamps[-1][0] > changed + timedelta(minutes=5):
            stamps = line_times(text, changed.date() - timedelta(days=1))
        upper = text.upper()
        if "RUN DONE" in upper:
            state = "DONE"
        elif "BLOCKED" in upper:
            state = "BLOCKED"
        elif now - changed <= RUNNING_WINDOW:
            state = "running"
        else:
            state = "waiting"
        start = stamps[0][0] if stamps else None
        last = stamps[-1][0] if stamps else changed
        end = now if state == "running" else last
        sleeps = [int(m.group(1)) if m.group(1) else 60 for m in SLEEP_RE.finditer(text)]
        chained += [c.strip().split()[0].strip("`*") for c in CHAIN_RE.findall(text) if c.strip()]
        runs.append({"name": name, "path": str(p), "state": state, "changed": changed,
                     "start": start, "last": last, "end": end,
                     "elapsed": (end - start) if start else None,
                     "lines": [l for l in text.splitlines() if l.strip()],
                     "idle_s": sum(sleeps), "sleep_hits": len(sleeps),
                     "limit_hits": len(LIMIT_RE.findall(text))})
    known = {r["name"].lower() for r in runs}
    queued = []
    for name in chained:
        if name.lower() not in known:
            queued.append(name)
            known.add(name.lower())
    if handoff.is_dir():
        for p in sorted(handoff.glob("PROMPT_*.txt")):
            name = p.stem[len("PROMPT_"):]
            if name.lower() not in known and not (handoff / name).is_dir():
                queued.append(name)
                known.add(name.lower())
    for name in queued:
        runs.append({"name": name, "path": "", "state": "queued", "changed": None, "start": None,
                     "last": None, "end": None, "elapsed": None, "lines": [], "idle_s": 0,
                     "sleep_hits": 0, "limit_hits": 0})
    live = [r for r in runs if r["state"] == "running"] or [r for r in runs if r["changed"]]
    active = max(live, key=lambda r: r["changed"]) if live else None
    order = {"running": 0, "BLOCKED": 1, "waiting": 2, "queued": 3, "DONE": 4}
    runs.sort(key=lambda r: (order[r["state"]], -(r["changed"].timestamp() if r["changed"] else 0)))
    return {"runs": runs, "active": active, "tail": active["lines"][-4:] if active else [], "hint": ""}


# ---------------------------------------------------------------- user actions

ACTIONS_HEAD = re.compile(r"^\s*(?:#+\s*)?\**\s*USER ACTIONS\b.*$", re.I)
HEADING = re.compile(r"^\s*(#+\s|\*\*[A-Z][A-Z /]+\*\*\s*$|[A-Z][A-Z /]{3,}:\s*$)")
EMPTY_ITEM = re.compile(r"^[\s\-*>(]*(none|nothing|n/?a|no action[s]?( needed)?|-)?[.)]*\s*$", re.I)
BLOCKER = re.compile(r"block", re.I)


def collect_actions(env: Env) -> dict:
    handoff = env.root / "handoff"
    if not handoff.is_dir():
        return {"items": [], "hint": f"{handoff} not found"}
    items = []
    for p in list(handoff.glob("*/BOARD.md")) + list(handoff.glob("*/REPORT.md")):
        lines, grab, body = read_text(p).splitlines(), False, []
        for line in lines:
            if ACTIONS_HEAD.match(line):
                grab = True
                rest = line.split(":", 1)[1].strip() if ":" in line else ""
                if rest:
                    body.append(rest)
                continue
            if grab and HEADING.match(line):
                grab = False
            if grab and not EMPTY_ITEM.match(line):
                body.append(line.strip())
        if body:
            items.append({"run": p.parent.name, "file": p.name, "time": env.mtime(p),
                          "lines": body, "blocker": any(BLOCKER.search(b) for b in body)})
    items.sort(key=lambda i: i["time"], reverse=True)
    return {"items": items, "hint": ""}


# ---------------------------------------------------------------- git

def collect_git(env: Env) -> dict:
    if env.mock:
        g = json.loads((HERE / "mock" / "git.json").read_text(encoding="utf-8"))
        return {**g, "hint": ""}
    git = shutil.which("git")
    if not git:
        return {"hint": "git not on PATH: install Git for Windows", "main": "", "merged": [],
                "unmerged": [], "recent": []}
    if not (env.game / ".git").exists():
        return {"hint": f"{env.game} is not a git checkout", "main": "", "merged": [],
                "unmerged": [], "recent": []}
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
            "hint": ""}


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


def collect_usage(env: Env) -> dict:
    daily = ccusage_json(env, "daily", [["daily", "--json", "--breakdown"], ["daily", "--json"]])
    blocks = ccusage_json(env, "blocks", [["blocks", "--json", "--token-limit", "max"], ["blocks", "--json"]])
    sessions = ccusage_json(env, "session", [["session", "--json"]])
    if daily is None and blocks is None:
        return {"hint": "ccusage not found: install Node.js, then `npm i -g ccusage`",
                "days": [], "blocks": [], "sessions": [], "total": 0.0}
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
    ss = []
    raw = (sessions or {}).get("session") or (sessions or {}).get("sessions") or []
    for s in raw:
        first = parse_time(s.get("firstActivity") or s.get("startTime"))
        last = parse_time(s.get("lastActivity") or s.get("endTime"))
        ss.append({"id": s.get("sessionId", "?"), "first": first, "last": last,
                   "cost": float(s.get("totalCost", 0) or 0)})
    total = float(((daily or {}).get("totals") or {}).get("totalCost", 0) or sum(d["cost"] for d in days))
    return {"hint": "" if daily is not None else "ccusage daily failed", "days": days,
            "blocks": bl, "sessions": ss, "total": total}


def cost_per_run(runs: list[dict], sessions: list[dict]) -> list[dict]:
    """A session counts towards a run when its activity window overlaps the run's start..end.
    Date-only session stamps (00:00) cover their whole day, so such a match is marked rough."""
    out = []
    for r in runs:
        if not r["start"]:
            continue
        cost, n, rough = 0.0, 0, False
        for s in sessions:
            if not s["last"]:
                continue
            first = s["first"] or s["last"]
            last = s["last"]
            if last.time() == datetime.min.time():
                last += timedelta(days=1)
                rough = True
            if first <= r["end"] and last >= r["start"]:
                cost += s["cost"]
                n += 1
        out.append({"name": r["name"], "state": r["state"], "cost": cost, "sessions": n,
                    "rough": rough, "elapsed": r["elapsed"], "run": r})
    return out


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


def collect_project_cost(env: Env, usage_total_usd: float) -> dict:
    hint = ""
    try:
        cfg = json.loads(env.costs.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as e:
        cfg, hint = {}, f"costs.json unreadable: {e.__class__.__name__}"
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
    ui = env.root / "ui-gen"
    if ui.is_dir():
        for p in ui.rglob("ledger.json"):
            try:
                entries = ledger_entries(json.loads(read_text(p)))
            except json.JSONDecodeError:
                continue
            rep_files += 1
            for usd, when in entries:
                add("Replicate", usd * rate, usd * rate if when.startswith(month) else 0.0)
                if when[:7]:
                    starts.append(when[:7])
    total = sum(c[0] for c in cats.values())
    claude_paid = sum(cats.get(k, [0, 0])[0] for k in ("Subscriptions", "API credit"))
    equiv = usage_total_usd * rate
    return {"cats": cats, "total": total, "month": sum(c[1] for c in cats.values()),
            "since": min((s for s in starts if s), default=""), "equiv_eur": equiv,
            "claude_paid": claude_paid, "saved": equiv - claude_paid, "rate": rate,
            "ledgers": rep_files, "hint": hint}


# ---------------------------------------------------------------- everything

def collect(env: Env) -> dict:
    runs = collect_runs(env)
    usage = collect_usage(env)
    real = [r for r in runs["runs"] if r["start"]]
    per_run = cost_per_run(real, usage["sessions"])
    costs = [c["cost"] for c in per_run if c["cost"] > 0]
    return {"env": env, "now": env.now(), "runs": runs, "usage": usage, "per_run": per_run,
            "median_run": statistics.median(costs) if costs else 0.0,
            "actions": collect_actions(env), "git": collect_git(env), "unity": collect_unity(env),
            "project": collect_project_cost(env, usage["total"]),
            "claude_md_words": len(read_text(env.claude_md).split())}
