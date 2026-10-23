"""Maps the data.collect() dict to the JSON the web cockpit renders (web/SCHEMA.md, sample: web/state.sample.json).

build_state(d) is pure apart from a few read-only file and `git log` lookups for the shipped feed, the
builds and the playtest list. Nothing is hardcoded except what progress.json (Road to a real game) and
costs.json (plan prices, start month, renewal day) hold. Run it alone to print the JSON:

    python state_builder.py            live data
    python state_builder.py --mock     Tools/cockpit/mock, with "mock": true
"""

from __future__ import annotations

import argparse
import json
import re
import statistics
from datetime import date, datetime, timedelta
from pathlib import Path

import data
import tips

HERE = Path(__file__).resolve().parent
PROGRESS = HERE / "progress.json"
ACTIONS_MAX_AGE = timedelta(days=2)       # same cut as the TUI: older USER ACTIONS sections are not "waiting"
MAX_ACTION_ITEMS = 8
RUN_ROWS = 10
OPUS_SHARE_LIMIT = tips.OPUS_SHARE_LIMIT
IDLE_HOT = 0.30
BLOCK_LEN = timedelta(hours=5)
WEEK_LEN = timedelta(days=7)
TONE_OF_STATE = {"DONE": "ok", "running": "warn", "BLOCKED": "bad", "waiting": "mute", "queued": "dim"}
MODEL_TONE = {"sonnet": "ok", "opus": "hot", "haiku": "text"}
BULLET = re.compile(r"^\s*(?:[-*]\s+|\d+[.)]\s+)")
STAMP = re.compile(r"^[-*\s]*(?:\d{4}-\d{2}-\d{2}[ T])?\d{1,2}:\d{2}(?::\d{2})?\s*")
STATUS_PASS = re.compile(r"Exit code (\d+),\s*\**(\d+) failure")


# ---------------------------------------------------------------- formatting

def iso(t: datetime) -> str:
    return t.astimezone().isoformat(timespec="seconds")


def hm(t: datetime | None) -> str:
    return f"{t:%H:%M}" if t else "-"


def dur_hm(td: timedelta | None) -> str:
    """2h54, 0h08: the run table's style."""
    if td is None:
        return "-"
    m = max(0, int(td.total_seconds() // 60))
    return f"{m // 60}h{m % 60:02d}"


def dur_words(td: timedelta) -> str:
    """3h48, 48m, 2d 13h."""
    m = max(0, int(td.total_seconds() // 60))
    if m < 60:
        return f"{m}m"
    if m < 48 * 60:
        return f"{m // 60}h{m % 60:02d}"
    return f"{m // 1440}d {m % 1440 // 60}h"


def usd(x: float, dec: int = 2) -> str:
    return f"${x:,.{dec}f}"


def eur(x: float) -> str:
    return f"EUR {x:,.0f}"


def plural(n: int, word: str) -> str:
    return f"{n} {word}" + ("" if n == 1 else "s")


def md_clean(s: str) -> str:
    return re.sub(r"\s+", " ", s.replace("`", "").replace("\ufeff", "")).strip()


def floor_even(t: datetime) -> datetime:
    t = t.replace(minute=0, second=0, microsecond=0)
    return t - timedelta(hours=t.hour % 2)


def ceil_even(t: datetime) -> datetime:
    f = floor_even(t)
    return f if f == t else f + timedelta(hours=2)


def trunc(s: str, n: int) -> str:
    return s if len(s) <= n else s[: n - 3].rstrip() + "..."


# ---------------------------------------------------------------- small readers

def read_progress(path: Path = PROGRESS) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {}


def builds_list(env: data.Env) -> list[dict]:
    root = env.root / "Builds"
    if not root.is_dir():
        return []
    out = []
    for p in root.iterdir():
        if not p.is_dir():
            continue
        files = {}
        for f in p.iterdir():
            if f.is_file() and f.suffix.lower() in (".zip", ".apk"):
                files[f.suffix.lower()] = files.get(f.suffix.lower(), 0) + f.stat().st_size
        out.append({"name": p.name, "when": env.mtime(p), "zip": files.get(".zip", 0), "apk": files.get(".apk", 0)})
    out.sort(key=lambda b: b["when"], reverse=True)
    return out


def feedback_files(env: data.Env) -> list[Path]:
    fb = env.root / "playtest-feedback"
    if not fb.is_dir():
        return []
    return [p for p in fb.rglob("*") if p.is_file() and not re.match(r"(DIGEST|ANALY[SZ]|EXAMPLE)", p.name, re.I)
            and p.name.upper() != "README.MD"]


def commits_today(env: data.Env, now: datetime) -> list[dict]:
    """Commits on main since midnight, newest first. Empty in mock mode (the mock has no timestamps)."""
    if env.mock:
        return []
    import shutil
    git = shutil.which("git")
    if not git or not (env.game / ".git").exists():
        return []
    since = datetime.combine(now.date(), datetime.min.time()).isoformat()
    for base in ("origin/main", "main"):
        code, out = data.run([git, "log", base, f"--since={since}", "--format=%h|%cI|%p|%s"], env.game)
        if code == 0:
            break
    else:
        return []
    rows = []
    for line in out.splitlines():
        parts = line.split("|", 3)
        if len(parts) == 4:
            when = data.parse_time(parts[1])
            rows.append({"hash": parts[0], "when": when, "merge": len(parts[2].split()) > 1, "subject": parts[3]})
    return rows


def status_checks_today(env: data.Env, now: datetime) -> int:
    """Worktrees whose latest build-check result (Game*/STATUS.md) is a pass written today."""
    n = 0
    for p in env.root.glob("Game*/STATUS.md"):
        try:
            if env.mtime(p).date() != now.date():
                continue
        except OSError:
            continue
        text = data.read_text(p)
        m = STATUS_PASS.search(text)
        if (int(m.group(1)) == 0) if m else bool(re.search(r"exit 0\b|RESULT: all", text)):
            n += 1
    return n


def prompt_model(path) -> str:
    """'Sonnet 5.5, effort high' from a run prompt's 'Run model:' or 'Model:' sentence."""
    if not path:
        return ""
    m = re.search(r"(?:Run model|Model):\s*([^.\n]+)", data.read_text(path)[:1500])
    return trunc(m.group(1).strip(), 90) if m else ""


# ---------------------------------------------------------------- limits and plan

def weekly_info(d: dict) -> dict | None:
    w = d["state"].get("weekly")
    if not w:
        return None
    now = d["now"]
    reset = data.parse_time(w.get("reset"))
    pct = float(w.get("pct", 0))
    out = {"pct": pct, "reset": reset, "stale": not reset or reset <= now, "pace": None, "diff": 0.0, "over": None,
           "left": None}
    if out["stale"]:
        return out
    left = reset - now
    elapsed_d = max(7 - left.total_seconds() / 86400, 0.05)
    left_d = left.total_seconds() / 86400
    burn = pct / elapsed_d
    out_in = (100 - pct) / burn if burn > 0 else None
    out.update(left=left, pace=max(0.0, min(100.0, (1 - left / WEEK_LEN) * 100)))
    out["diff"] = pct - out["pace"]
    out["over"] = (left_d - out_in) if out_in is not None and out_in < left_d else None
    return out


def block_info(d: dict) -> dict | None:
    b = d["block"]
    if not b:
        return None
    now = d["now"]
    left = max(timedelta(0), b["end"] - now)
    set_at = data.parse_time(d["state"].get("block_set_at"))
    return {"b": b, "left": left, "manual": b["src"] == "manual", "set_at": set_at,
            "pct": max(0, min(100, round((now - b["start"]) / BLOCK_LEN * 100)))}


def renewal(plan_cfg: dict, today: date) -> date | None:
    day = plan_cfg.get("renew_day")
    if not isinstance(day, int) or not 1 <= day <= 31:
        return None
    y, m = today.year, today.month
    for _ in range(2):
        nxt = date(y, m, min(day, (date(y + (m == 12), m % 12 + 1, 1) - timedelta(days=1)).day))
        if nxt >= today:
            return nxt
        y, m = y + (m == 12), m % 12 + 1
    return None


def plan_section(d: dict, cfg: dict, hints: list[str]) -> dict:
    p = cfg.get("plan") or {}
    now = d["now"]
    opts_cfg = [o for o in p.get("options", []) if o.get("id") and isinstance(o.get("monthly_eur"), (int, float))]
    if not opts_cfg:
        hints.append("costs.json has no plan block: add plan.options to see plan prices.")
        opts_cfg = [{"id": "x20", "name": "No plan set", "monthly_eur": 0, "capacity": 0}]
    api = p.get("api_value_eur")
    api = float(api) if isinstance(api, (int, float)) else float(d["project"]["equiv_eur"])
    start = str(p.get("start_month") or "")
    months = data.months_between(start, now.date()) if re.fullmatch(r"\d{4}-\d{2}", start) else 0
    options = []
    for o in opts_cfg:
        paid = months * float(o["monthly_eur"])
        options.append({"id": o["id"], "name": o.get("name") or o["id"], "price_label": eur(o["monthly_eur"]),
                        "paid": eur(paid), "multiple": f"{api / paid:.1f}x" if paid > 0 else "-",
                        "_paid": paid, "_cap": o.get("capacity") or 0})
    ranked = sorted(options, key=lambda o: o["_paid"], reverse=True)
    bars = [{"label": "API price", "pct": 100, "val": eur(api)}]
    for o in ranked:
        bars.append({"label": o["name"], "plan_id": o["id"], "pct": round(min(100.0, o["_paid"] / api * 100), 1) if api > 0 else 0,
                     "val": o["paid"]})
    nxt = renewal(p, now.date())
    days = (nxt - now.date()).days if nxt else None
    ren = ("" if nxt is None else f"{nxt:%d %b}, " + ("today" if days == 0 else "tomorrow" if days == 1 else f"in {days} days"))
    caps = sorted((o for o in options if o["_cap"]), key=lambda o: o["_cap"], reverse=True)
    held = (f" If {caps[0]['name']} never passes about {caps[1]['_cap'] / caps[0]['_cap'] * 100:.0f}% of its week, "
            f"{caps[1]['name']} would have held.") if len(caps) >= 2 else ""
    note = (f"Renews {ren}. " if ren else "") + "Log the weekly % just before each reset." + held
    default = p.get("default") if any(o["id"] == p.get("default") for o in options) else options[0]["id"]
    since = date(int(start[:4]), int(start[5:7]), 1).strftime("%b") if months else "-"
    return {"default": default, "since": since, "api_value": eur(api),
            "options": [{k: v for k, v in o.items() if not k.startswith("_")} for o in options],
            "bars": bars, "note": note, "_renew": ren, "_name": next(o["name"] for o in options if o["id"] == default)}


# ---------------------------------------------------------------- sections

def road_section(prog: dict, hints: list[str]) -> dict:
    tracks = []
    for t in prog.get("tracks", []):
        pct = max(0, min(100, int(round(float(t.get("pct", 0))))))
        tracks.append({"name": t.get("name", "?"), "pct": pct, "sub": t.get("sub", ""),
                       "tone": t.get("tone") or ("ok" if pct >= 70 else "warn" if pct >= 40 else "hot")})
    if not tracks:
        hints.append("progress.json is missing or has no tracks: the Road to a real game is empty.")
    pct = int(round(statistics.mean(t["pct"] for t in tracks))) if tracks else 0
    ms, nxt_seen = [], False
    for m in sorted(prog.get("milestones", []), key=lambda m: m.get("pct", 0)):
        reached = pct >= m.get("pct", 0)
        tone = "ok" if reached else ("dim" if nxt_seen else "warn")
        nxt_seen = nxt_seen or not reached
        ms.append({"pct": m.get("pct", 0), "label": m.get("label", ""), "tone": tone})
    upd = prog.get("updated")
    return {"pct": pct, "desc": f"Average of {len(tracks)} tracks, from progress.json" + (f" ({upd})." if upd else "."),
            "milestones": ms, "tracks": tracks}


def chain_section(d: dict) -> tuple[dict, dict]:
    """The chain card, plus facts the KPI and the waiting list reuse."""
    now, rr = d["now"], d["runs"]
    plan = d["chain"] or {}
    cost = {c["name"]: c["cost"] for c in d["per_run"]}
    active = rr["active"]
    rows = []
    for r in plan.get("done", []):
        if r["end"] < now - timedelta(hours=12):
            continue
        meta = f"done {hm(r['end'])}, {dur_hm(r['elapsed'])}" + (f", {usd(cost[r['name']])}" if cost.get(r["name"]) else "")
        rows.append({"name": r["name"], "meta": meta, "bars": [{"start": r["start"], "end": r["end"], "kind": "done"}]})
    ends = []                                         # (end, name) of every projected finish
    for r in rr["runs"]:
        if r["state"] not in ("running", "BLOCKED", "waiting") or not r["start"] or now - r["start"] > timedelta(days=2):
            continue
        bars = [{"start": r["start"], "end": r["end"] if r["state"] == "waiting" else now, "kind": "run"}]
        eta = plan.get("active_eta") if r is active else None
        if r["state"] == "waiting":
            meta = f"no log line since {hm(r['last'])}"
        elif r["state"] == "BLOCKED":
            meta = f"blocked, since {hm(r['start'])}"
        else:
            meta = f"running since {hm(r['start'])}" + (
                f", ETA {hm(eta)}" if eta and eta > now + timedelta(minutes=1) else ", past the typical length" if eta else "")
        if eta and eta > now:
            bars.append({"start": now, "end": eta, "kind": "proj"})
            ends.append((eta, r["name"]))
        rows.append({"name": r["name"], "meta": meta, "bars": bars})
    for q in plan.get("queued", []):
        ready = q["start"] <= now + timedelta(minutes=1)
        if q["late"]:
            meta = f"will not start, after {hm(plan['no_new_after'])}"
        else:
            meta = f"ready, ETA {hm(q['end'])}" if ready else f"queued, {hm(q['start'])} to {hm(q['end'])}"
        rows.append({"name": q["name"], "meta": meta,
                     "bars": [{"start": q["start"], "end": q["end"], "kind": "late" if q["late"] else "proj"}]})
        if not q["late"]:
            ends.append((q["end"], q["name"]))
    rows.sort(key=lambda r: r["bars"][0]["start"])
    all_bars = [b for r in rows for b in r["bars"]]
    # a bar that began long ago (an overnight run, or a log with a wrong stamp) is clipped at the axis edge, not allowed to stretch it
    lo = min([b["start"] for b in all_bars if b["start"] >= now - timedelta(hours=14)] + [now - timedelta(hours=3)])
    start = floor_even(lo)
    end = start + timedelta(hours=12)
    if all_bars:
        end = min(max(end, ceil_even(max(b["end"] for b in all_bars))), start + timedelta(hours=24))
    quiet = None
    for day in (start.date() - timedelta(days=1), start.date(), start.date() + timedelta(days=1)):
        qs, qe = datetime.combine(day, data.QUIET_FROM), datetime.combine(day, data.QUIET_TO)
        if qe > start and qs < end:
            quiet = {"start": iso(qs), "end": iso(qe)}
            break
    nq = datetime.combine(now.date(), data.QUIET_FROM)
    nq = nq if nq > now else nq + timedelta(days=1)
    late = [q for q in plan.get("queued", []) if q["late"]]
    last_end, last_name = max(ends, default=(None, ""))
    unity = d["health"]["unity_running"]
    if late:
        badge = {"text": f"{len(late)} will not start", "tone": "bad"}
    elif last_end and last_end >= nq:
        badge = {"text": "Runs into the quiet window", "tone": "bad" if unity else "warn"}
    elif plan.get("no_new_after"):
        badge = {"text": f"Cutoff {hm(plan['no_new_after'])}, all fit", "tone": "ok"}
    else:
        badge = {"text": "No cutoff set", "tone": "dim"}
    if last_end is None:
        note = "Nothing queued." if not late else ""
    elif last_end >= nq:
        note = (f"{last_name} ends about {hm(last_end)}, inside the quiet window {data.QUIET_FROM:%H:%M}-{data.QUIET_TO:%H:%M}."
                + (" Unity is open, so the nightly refuses to start." if unity else ""))
    else:
        gap = nq - last_end
        note = f"{last_name} ends about {hm(last_end)}, {'well ' if gap >= timedelta(hours=2) else ''}before the quiet window."
    if late:
        note = (note + " " if note else "") + f"{', '.join(q['name'] for q in late)} would start after the {hm(plan['no_new_after'])} cutoff."
    n = plan.get("n_finished", 0)
    when = "Today" if start.date() == now.date() else f"{start:%a}"
    desc = (rr["hint"] if rr["hint"] else
            f"{when} {hm(start)} to {hm(end)}. Dashed bars are projected from the median of the last {plural(n, 'finished run')}"
            f" ({dur_hm(plan.get('median'))}).")
    for r in rows:
        for b in r["bars"]:
            b["start"], b["end"] = iso(b["start"]), iso(b["end"])
    card = {"axis_start": iso(start), "axis_end": iso(end), "quiet": quiet, "desc": desc, "badge": badge, "note": note,
            "rows": rows}
    return card, {"queued": plan.get("queued", []), "late": late}


def chain_kpi(d: dict, facts: dict) -> dict:
    rr, plan = d["runs"], d["chain"] or {}
    if rr["hint"]:
        return {"label": "Chain", "value": "-", "tone": "mute", "sub": trunc(rr["hint"], 60)}
    running = [r for r in rr["runs"] if r["state"] in ("running", "BLOCKED")]
    if running:
        a = rr["active"] if rr["active"] in running else running[0]
        eta = plan.get("active_eta") if a is rr["active"] else None
        more = f" (+{len(running) - 1} more)" if len(running) > 1 else ""
        blocked = a["state"] == "BLOCKED"
        return {"label": "Chain", "value": "Blocked" if blocked else "Running", "tone": "bad" if blocked else "ok",
                "sub": f"{a['name']} since {hm(a['start'])}" + (
                    f", ETA {hm(eta)}" if eta and eta > d["now"] + timedelta(minutes=1) else ", past the typical length" if eta else "") + more}
    q = facts["queued"]
    if q:
        return {"label": "Chain", "value": "Idle", "tone": "bad" if q[0]["late"] else "warn",
                "sub": f"{q[0]['name']} " + ("will not start, past the cutoff" if q[0]["late"] else "is ready to start")}
    return {"label": "Chain", "value": "Idle", "tone": "dim", "sub": "Nothing queued"}


def run_cost_section(d: dict) -> dict:
    now = d["now"]
    rows_src = [c for c in d["per_run"] if now - c["start"] <= timedelta(days=3)][:RUN_ROWS]
    med = d["median_run"]
    top = max((c["cost"] for c in rows_src), default=0) or 1
    rows = []
    for c in rows_src:
        rows.append({"name": c["name"], "state_tone": TONE_OF_STATE.get(c["state"], "dim"), "cost": usd(c["cost"]),
                     "bar": max(1, round(c["cost"] / top * 100)), "time": dur_hm(c["elapsed"]),
                     "rate": f"{c['per_h']:.1f}" if c["minutes"] else "-", "opus": f"{c['opus_share']:.0%}",
                     "idle": f"{c['idle_share']:.0%}", "hot": bool(med) and c["cost"] > 2 * med,
                     "opus_hot": c["opus_share"] > OPUS_SHARE_LIMIT, "idle_hot": c["idle_share"] > IDLE_HOT})
    today_n = sum(1 for c in rows_src if c["start"].date() == now.date())
    desc = "API price equivalent, newest first." + (f" Median finished run {usd(med)}." if med else "")
    return {"desc": desc, "badge": f"{plural(today_n, 'run')} today" if today_n else "no runs today",
            "hint": d["local"]["hint"], "rows": rows}


def action_items(d: dict) -> list[dict]:
    """USER ACTIONS from the last 2 days, one item per bullet. A line that starts a bullet or number begins
    a new item; any other line continues the one before. A bold first sentence is the title."""
    items = []
    for sec in d["actions"]["items"]:
        if d["now"] - sec["time"] > ACTIONS_MAX_AGE:
            continue
        chunks: list[list[str]] = []
        for line in sec["lines"]:
            if BULLET.match(line) or not chunks:
                chunks.append([BULLET.sub("", line)])
            else:
                chunks[-1].append(line.strip())
        for ch in chunks[:6]:
            text = " ".join(ch).strip()
            m = re.match(r"\*\*(.+?)\*\*\s*(.*)$", text)
            if m:
                title, sub = m.group(1), m.group(2)
            else:
                m = re.match(r"(.{12,90}?[.:])\s+(.*)$", text)
                if m:
                    title, sub = m.group(1), m.group(2)
                elif len(text) > 90:                       # no sentence end in reach: cut at a word, keep all of it below
                    cut = text.rfind(" ", 0, 88)
                    title, sub = text[:cut].rstrip(",;:") + "...", text[cut:].strip()
                else:
                    title, sub = text, ""
            title, sub = md_clean(title).rstrip(".:"), md_clean(sub)
            if not title:
                continue
            blocker = bool(data.BLOCKER.search(text))
            items.append({"tone": "bad" if blocker else "warn", "title": trunc(title, 110), "sub": trunc(sub, 260),
                          "_run": sec["run"]})
    return items


def waiting_section(d: dict, facts: dict) -> dict:
    items = action_items(d)
    rr = d["runs"]
    running = any(r["state"] in ("running", "BLOCKED") for r in rr["runs"])
    if facts["queued"]:
        q = facts["queued"][0]
        qr = next((r for r in rr["queue"] if r["name"] == q["name"]), None)
        model = prompt_model(qr["prompt"]) if qr else ""
        dur = data.prompt_duration(qr["prompt"]) if qr and qr["prompt"] else None
        bits = [f"Fresh session{', ' + model if model else ''}" + ".", f"About {dur_words(dur or q['dur'])}."]
        if running:
            items.append({"tone": "mute", "title": f"Next in the chain: {q['name']}",
                          "sub": "Starts after the running one. " + " ".join(bits)})
        else:
            items.append({"tone": "bad" if q["late"] else "warn", "title": f"Start {q['name']}",
                          "sub": " ".join(bits) + (" It is past the cutoff." if q["late"] else "")})
    for b in d["git"].get("bundles", [])[:3]:
        items.append({"tone": "mute", "title": f"Cloud bundle {b['branch']}",
                      "sub": f"{b['file']} in handoff is {b['why']}. git fetch it, then git push. Fetch and push only."})
    items = items[:MAX_ACTION_ITEMS]
    for i in items:
        i.pop("_run", None)
    open_n = sum(1 for i in items if i["tone"] != "mute")
    return {"desc": "Only things that block or advance the game.", "open": open_n, "items": items}


def shipped_section(d: dict) -> dict:
    env, now = d["env"], d["now"]
    feed = []
    done_today = [r for r in d["runs"]["runs"] if r["state"] == "DONE" and r["end"] and r["end"].date() == now.date()]
    for r in done_today:
        line = next((l for l in reversed(r["lines"]) if "RUN DONE" in l.upper()), "")
        text = md_clean(re.sub(r"\bRUN DONE\b[.:,]?", "done", STAMP.sub("", line), flags=re.I)) if line else ""
        text = text if text.lower().startswith(r["name"].lower()) else f"{r['name']} {text or 'done'}"
        feed.append((r["end"], trunc(text, 180)))
    commits = commits_today(env, now)
    for c in commits[:5]:
        feed.append((c["when"], f"main {c['hash']}: {trunc(c['subject'], 120)}"))
    builds = [b for b in builds_list(env) if b["when"].date() == now.date()]
    for b in builds:
        parts = [f"{label} {size / 1e6:,.0f} MB" for label, size in (("Windows zip", b["zip"]), ("APK", b["apk"])) if size]
        feed.append((b["when"], f"Build {b['name']} ready" + (": " + ", ".join(parts) + "." if parts else ".")))
    feed.sort(key=lambda f: f[0], reverse=True)
    counters = [("Runs done", len(done_today)), ("Merges", sum(1 for c in commits if c["merge"])),
                ("Builds", len(builds)), ("Checks green", status_checks_today(env, now))]
    return {"date": f"{now:%a %d %b}", "counters": [{"n": str(n), "label": label} for label, n in counters],
            "feed": [{"time": hm(t), "text": text} for t, text in feed[:6]]}


def limits_section(d: dict) -> dict:
    now = d["now"]
    bi, wi = block_info(d), weekly_info(d)
    if bi:
        b = bi["b"]
        sub = (f"resets {hm(b['end'])}, set by hand {hm(bi['set_at'])}" if bi["manual"] and bi["set_at"]
               else f"resets {hm(b['end'])}, set by hand" if bi["manual"] else f"resets {hm(b['end'])}, estimated from local logs")
        block = {"sub": sub, "pct": bi["pct"], "left": f"{usd(b['cost'])} in block",
                 "right": f"burn {usd(b['burn'], 0)}/h" if b["burn"] else "burn shown after 30 min"}
    else:
        block = {"sub": "no active window", "pct": 0, "left": "no cost yet", "right": "set the reset by hand"}
    if wi is None:
        weekly = {"sub": "not set", "pct": 0, "pace": None, "tone": "mute",
                  "note": "Not set. Press set and type the % used and the time left from claude.ai."}
    elif wi["stale"]:
        weekly = {"sub": "reset passed", "pct": round(wi["pct"]), "pace": None, "tone": "warn",
                  "note": f"{wi['pct']:.0f}% stored, but its reset has passed. Press set to enter the new week."}
    else:
        diff = round(wi["diff"])
        rel = (f"{abs(diff)} points ahead of an even pace" if diff > 0 else f"{abs(diff)} points behind an even pace" if diff < 0
               else "on an even pace")
        tone = "hot" if wi["over"] is not None else "warn" if wi["diff"] > 0 else "ok"
        note = f"{wi['pct']:.0f}% used, {rel} (white tick)." + (
            f" At this rate it runs out {wi['over']:.1f} days before the reset." if wi["over"] is not None else "")
        weekly = {"sub": f"resets in {dur_words(wi['left'])}", "pct": round(wi["pct"]), "pace": round(wi["pace"]), "tone": tone,
                  "note": note}
    fam = d["today"]["families"]
    total = sum(fam.values())
    models = []
    if total > 0:
        for name in ("sonnet", "opus", "haiku"):
            models.append({"name": name.capitalize(), "pct": round(fam.get(name, 0.0) / total * 100), "tone": MODEL_TONE[name]})
    return {"block": block, "weekly": weekly, "models": models}


def today_cost(d: dict) -> float:
    local = d["local"].get("days", {}).get(d["now"].date())
    if local is not None:
        return local
    day = next((x for x in d["usage"]["days"] if x["date"] == d["now"].date()), None)
    return day["cost"] if day else 0.0


def week_section(d: dict) -> dict:
    now = d["now"]
    local = d["local"].get("days", {})
    by_date = dict(local) if local else {x["date"]: x["cost"] for x in d["usage"]["days"]}
    days = []
    for i in range(6, -1, -1):
        day = now.date() - timedelta(days=i)
        c = by_date.get(day, 0.0)
        days.append({"day": f"{day:%a}", "label": usd(c, 0), "value": round(c), "today": i == 0})
    return {"days": days}


def playtest_section(d: dict) -> dict:
    env, now = d["env"], d["now"]
    builds = builds_list(env)
    friends = next((b for b in builds if b["name"].lower().startswith("friends")), None)
    fb = feedback_files(env)
    analysed = bool(fb) and bool(d["health"]["feedback"]) and d["health"]["feedback"]["new"] == 0
    target = None
    try:
        target = date.fromisoformat(str(d["state"].get("playtest_date") or ""))
    except ValueError:
        pass
    items = [
        {"title": f"Friends build ready ({friends['name']})" if friends else "Friends build ready", "done": friends is not None},
        {"title": "APK in the friends build folder", "done": bool(friends and friends["apk"])},
        {"title": "Testers sent feedback files", "done": bool(fb)},
        {"title": "Feedback analysed (digest newer than every file)", "done": analysed},
        {"title": "Target date set", "done": target is not None},
    ]
    logged = f"{plural(len(fb), 'feedback file')} logged."
    if target:
        n = (target - now.date()).days
        when = "today" if n == 0 else f"in {plural(n, 'day')}" if n > 0 else f"{plural(-n, 'day')} ago"
        desc = f"Target {target:%a %d %b}, {when}. {logged}"
    else:
        desc = f"No date yet. {logged}"
    return {"desc": desc, "items": items}


def health_section(d: dict) -> list[dict]:
    h, now, g = d["health"], d["now"], d["git"]
    out = []
    if h["unity_running"]:
        inside = h["quiet_now"]
        out.append({"tone": "bad" if inside else "ok", "label": "Unity",
                    "value": "running inside the quiet window, the nightly will refuse to start" if inside
                    else f"running, quiet window in {dur_words(h['quiet_in'])}"})
    else:
        out.append({"tone": "mute", "label": "Unity", "value": "not running"})
    bc = h["build_check"]
    if bc:
        word = "pass" if bc["ok"] else "FAIL" if bc["ok"] is False else "unknown"
        val = f"{word}, {bc['mode'] or '-'}, {hm(bc['when'])}" + (f", {bc['failures']} failures" if bc["ok"] is False and bc["failures"] else "")
        out.append({"tone": "ok" if bc["ok"] else "bad" if bc["ok"] is False else "warn", "label": "Build-check",
                    "value": f"{val} ({bc['where']})"})
    else:
        out.append({"tone": "dim", "label": "Build-check", "value": "no result found"})
    if h["build"]:
        age = now - h["build"]["when"]
        out.append({"tone": "warn" if age > timedelta(days=3) else "ok", "label": "Newest build",
                    "value": f"{h['build']['name']}, {dur_words(age)} old"})
    else:
        out.append({"tone": "dim", "label": "Newest build", "value": "no folder in Builds"})
    fb = h["feedback"]
    if fb:
        out.append({"tone": "warn" if fb["new"] else "ok", "label": "Playtest logs",
                    "value": f"{fb['new']} new since last analyse"})
    else:
        out.append({"tone": "dim", "label": "Playtest logs", "value": "no playtest-feedback folder"})
    if g.get("hint"):
        out.append({"tone": "warn", "label": "Branches", "value": g["hint"]})
    else:
        nb = len(g.get("bundles", []))
        out.append({"tone": "warn" if nb else "dim", "label": "Branches",
                    "value": f"{len(g['unmerged'])} on origin, not merged" + (f", {plural(nb, 'bundle')} to push" if nb else "")})
    return out


def header_section(d: dict, waiting_open: int) -> dict:
    now, u, g = d["now"], d["unity"], d["git"]
    if u["procs"]:
        p = u["procs"][0]
        up = dur_words(now - p["start"]) if p["start"] else "?"
        unity = {"text": f"Unity running, up {up}", "tone": "bad" if d["health"]["quiet_now"] else "ok"}
    else:
        unity = {"text": "Unity not running", "tone": "mute"}
    main = (g.get("recent") or [{}])[0].get("hash", "") if not g.get("hint") else ""
    return {"main": main, "unity": unity, "waiting": waiting_open, "clock": f"{now:%H:%M:%S}",
            "date": f"{now:%a} {now.day} {now:%b %Y}", "mock": d["env"].mock}


def kpi_spend(d: dict) -> dict:
    fam = d["today"]["families"]
    total = sum(fam.values())
    sub = ", ".join(f"{n.capitalize()} {fam[n] / total:.0%}" for n in ("sonnet", "opus", "haiku")
                    if total and fam[n] / total >= 0.005) if total else "no local transcripts today"
    return {"label": "Spend today", "value": usd(today_cost(d), 0), "tone": "text", "sub": sub}


def kpi_block(d: dict) -> dict:
    bi = block_info(d)
    if not bi:
        return {"label": "5h block", "value": "-", "tone": "mute", "sub": "no active window, set the reset by hand"}
    b = bi["b"]
    how = "set by hand" if bi["manual"] else "estimated from logs"
    return {"label": "5h block", "value": dur_words(bi["left"]), "tone": "text", "sub": f"left, resets {hm(b['end'])} ({how})"}


def kpi_weekly(d: dict) -> dict:
    wi = weekly_info(d)
    if wi is None:
        return {"label": "Weekly limit", "value": "-", "tone": "mute", "sub": "not set, use set under Plan and limits"}
    if wi["stale"]:
        return {"label": "Weekly limit", "value": f"{wi['pct']:.0f}%", "tone": "warn", "sub": "its reset has passed, set it again"}
    diff = round(wi["diff"])
    sub = f"{abs(diff)} points ahead of an even pace" if diff > 0 else f"{abs(diff)} points behind an even pace" if diff < 0 else "on an even pace"
    tone = "hot" if wi["over"] is not None else "warn" if wi["diff"] > 0 else "ok"
    return {"label": "Weekly limit", "value": f"{wi['pct']:.0f}%", "tone": tone, "sub": sub}


# ---------------------------------------------------------------- entry points

def build_state(d: dict, progress: dict | None = None) -> dict:
    """The /api/state JSON for one data.collect() result."""
    hints = [h for h in (d["runs"]["hint"], d["usage"]["hint"], d["local"]["hint"], d["actions"]["hint"],
                         d["git"]["hint"], d["project"]["hint"]) if h]
    cfg, _ = data.load_costs(d["env"])
    prog = read_progress() if progress is None else progress
    chain_card, facts = chain_section(d)
    waiting = waiting_section(d, facts)
    road = road_section(prog, hints)
    plan = plan_section(d, cfg, hints)
    kpis = [chain_kpi(d, facts), kpi_spend(d), kpi_block(d), kpi_weekly(d),
            {"label": "Road to a real game", "value": f"{road['pct']}%",
             "tone": "ok" if road["pct"] >= 80 else "warn" if road["pct"] >= 50 else "hot",
             "sub": f"{len(road['tracks'])} tracks, details below"},
            {"label": "Plan", "value": plan["_name"], "tone": "text", "sub": f"renews {plan['_renew']}" if plan["_renew"] else "renewal day not set"}]
    plan = {k: v for k, v in plan.items() if not k.startswith("_")}
    return {"now": iso(d["now"]), "mock": d["env"].mock, "header": header_section(d, waiting["open"]),
            "hints": hints, "kpis": kpis, "chain": chain_card, "run_cost": run_cost_section(d), "waiting": waiting,
            "shipped": shipped_section(d), "road": road, "plan": plan, "limits": limits_section(d),
            "week": week_section(d), "playtest": playtest_section(d), "health": health_section(d),
            "tips": [t.text for t in tips.tips_for(d)]}


def build_live_or_mock(mock: bool = False) -> dict:
    env = data.mock_env() if mock else data.live_env()
    return build_state(data.collect(env))


def main() -> None:
    ap = argparse.ArgumentParser(description="Print the cockpit /api/state JSON")
    ap.add_argument("--mock", action="store_true", help="read Tools/cockpit/mock instead of D:\\Emberfall")
    args = ap.parse_args()
    print(json.dumps(build_live_or_mock(args.mock), indent=1, ensure_ascii=False))


if __name__ == "__main__":
    main()
