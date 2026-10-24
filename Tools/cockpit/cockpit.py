"""Emberfall cockpit: a read-only terminal dashboard. Start it with cockpit.ps1.

Keys: q quit, r refresh, l open the active run log in Notepad, a toggle actions only, u all your
actions full screen, b type the 5h reset from claude.ai, w type the weekly percent and reset.
b and w write Tools/cockpit/state.json (gitignored); nothing else is ever written but the
transcript cache.
"""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from datetime import timedelta

from rich.console import Group
from rich.table import Table
from rich.text import Text
from textual.app import App, ComposeResult
from textual.containers import Grid, VerticalScroll
from textual.screen import ModalScreen, Screen
from textual.widgets import Footer, Input, Label, Static

import data
import tips

REFRESH_S = 30
ACTIONS_MAX_AGE = timedelta(days=2)   # older sections are in `u`, not in the panel or the badge
GOLD, EMBER, TEXT, DIM, RED, GREEN, YELLOW, BLUE = (
    "#e8b04a", "#e2571e", "#d8cfc4", "#7a6f66", "#e5484d", "#46c26a", "#e0c341", "#5b9bd5")
STATE = {"running": GREEN, "BLOCKED": RED, "waiting": YELLOW, "stale": DIM, "queued": BLUE, "DONE": DIM}


def dot(color: str) -> Text:
    return Text("\u25cf ", style=color)


def bar(frac: float, width: int, color: str = EMBER) -> Text:
    frac = max(0.0, min(1.0, frac))
    full = int(round(frac * width))
    return Text("\u2588" * full, style=color) + Text("\u2591" * (width - full), style=DIM)


def hint(msg: str) -> Text:
    return Text(msg, style=f"italic {YELLOW}")


def grid_table(*cols: tuple[str, dict]) -> Table:
    t = Table(box=None, expand=True, pad_edge=False, header_style=f"bold {GOLD}", show_edge=False)
    for name, kw in cols:
        t.add_column(name, **kw)
    return t


def hm(t) -> str:
    return f"{t:%H:%M}" if t else "-"


def age_color(td: timedelta) -> str:
    m = td.total_seconds() / 60
    return RED if m > 30 else YELLOW if m > 15 else GREEN


def recent_actions(d: dict) -> list[dict]:
    return [i for i in d["actions"]["items"] if d["now"] - i["time"] <= ACTIONS_MAX_AGE]


def blocking(d: dict) -> bool:
    return any(i["blocker"] for i in recent_actions(d)) or any(r["state"] == "BLOCKED" for r in d["runs"]["runs"])


# ---------------------------------------------------------------- panels

def render_header(d: dict, left: int) -> Text:
    g, u = d["git"], d["unity"]
    t = Text()
    t.append(" EMBERFALL COCKPIT ", style=f"bold #0d0b0a on {EMBER}")
    n = len(recent_actions(d))
    if n or blocking(d):
        t.append(f" WAITING {n} ", style=f"bold #0d0b0a on {RED if blocking(d) else GOLD}")
    t.append("  main ", style=GOLD)
    t.append(g["main"][:50] or (g["hint"] or "-"), style=TEXT if g["main"] else YELLOW)
    t.append("   Unity ", style=GOLD)
    if u["procs"]:
        p = u["procs"][0]
        up = data.fmt_dur(d["now"] - p["start"]) if p["start"] else "?"
        t.append_text(dot(GREEN))
        t.append(f"PID {p['pid']} up {up}" + (f" (+{len(u['procs']) - 1})" if len(u["procs"]) > 1 else ""), style=TEXT)
    else:
        t.append_text(dot(DIM))
        t.append("not running", style=DIM)
    t.append(f"   refresh in {left:2d}s", style=DIM)
    t.append(f"   {d['now']:%a %d %b %Y  %H:%M:%S}", style=GOLD)
    if d["env"].mock:
        t.append("   MOCK DATA", style=f"bold {EMBER}")
    return t


def render_now(d: dict):
    rr = d["runs"]
    if rr["hint"]:
        return hint(rr["hint"])
    a, now = rr["active"], d["now"]
    if not a:
        return Text("No active run.", style=DIM)
    plan = d["chain"]
    others = [r["name"] for r in rr["runs"] if r["state"] == "running" and r is not a]
    l1 = Text()
    l1.append_text(dot(STATE.get(a["state"], GREEN)))
    l1.append(a["name"], style=f"bold {GOLD}")
    l1.append(f"  {a['state']}", style=STATE.get(a["state"], TEXT))
    l1.append(f"  since {hm(a['start'])} ({data.fmt_dur(a['elapsed'])})", style=DIM)
    if others:
        l1.append("   also running: " + ", ".join(others), style=GREEN)
    step = re.sub(r"^[-*\s]*(?:\d{4}-\d{2}-\d{2}\s+)?\d{1,2}:\d{2}\s*", "", a["lines"][-1].strip()) if a["lines"] else ""
    l1.append("   step: ", style=GOLD)
    l1.append(step[:110], style=TEXT)
    l2 = Text()
    if a["last"] and a["has_log"]:
        silent = now - a["last"]
        l2.append("last log line ", style=GOLD)
        l2.append(f"{data.fmt_dur(silent)} ago ({hm(a['last'])})", style=age_color(silent))
    else:
        l2.append("last log line - (no own log)", style=DIM)
    l2.append("   deadline ", style=GOLD)
    if a["deadline"]:
        left = a["deadline"] - now
        l2.append(f"{hm(a['deadline'])} ", style=TEXT)
        l2.append(f"in {data.fmt_dur(left)}" if left.total_seconds() > 0 else "PASSED",
                  style=RED if left < timedelta(minutes=30) else TEXT)
        if a["stop_start"]:
            sl = a["stop_start"] - now
            l2.append(f"   stop starting work {hm(a['stop_start'])} ", style=GOLD)
            l2.append(f"in {data.fmt_dur(sl)}" if sl.total_seconds() > 0 else "passed", style=RED if sl.total_seconds() <= 0 else TEXT)
    else:
        l2.append("- (no Deadline in its start line)", style=DIM)
    l3 = Text()
    l3.append("cost ", style=GOLD)
    med = d["median_run"]
    l3.append(f"${d['active_cost']:,.2f}", style=EMBER)
    if med:
        x = d["active_cost"] / med
        l3.append(f" vs median ${med:,.2f} of earlier runs ({x:.1f}x)", style=RED if x > 2 else DIM)
    l3.append("   next ", style=GOLD)
    q = plan.get("queued") if plan else None
    if q:
        n = q[0]
        l3.append(f"{n['name']} ", style=TEXT)
        l3.append(f"~{hm(n['start'])}", style=RED if n["late"] else DIM)
        if n["late"]:
            l3.append(f" is after {hm(plan['no_new_after'])}: will not start", style=RED)
    else:
        l3.append("nothing queued", style=DIM)
    return Group(l1, l2, l3)


def render_chain(d: dict):
    rr, plan = d["runs"], d["chain"]
    if rr["hint"]:
        return hint(rr["hint"])
    cost = {c["name"]: c["cost"] for c in d["per_run"]}
    t = grid_table(("", {"width": 2}), ("run", {"ratio": 3, "no_wrap": True}), ("state", {"width": 8}),
                   ("start", {"width": 6}), ("end / ETA", {"width": 10}), ("took", {"width": 6, "justify": "right"}),
                   ("cost", {"width": 8, "justify": "right"}), ("", {"ratio": 3, "no_wrap": True}))
    for r in plan.get("done", []):
        c = cost.get(r["name"])
        t.add_row(dot(DIM), Text(r["name"], style=DIM), Text("DONE", style=DIM), Text(hm(r["start"]), style=DIM),
                  Text(hm(r["end"]), style=DIM), Text(data.fmt_dur(r["elapsed"]), style=DIM),
                  Text(f"${c:,.0f}" if c else "-", style=DIM), "")
    live = [r for r in rr["runs"] if r["state"] in ("running", "BLOCKED", "waiting")
            and r["start"] and d["now"] - r["start"] < timedelta(days=2)]
    for r in sorted(live, key=lambda r: r["start"]):
        c = STATE[r["state"]]
        is_active = r is rr["active"]
        eta = plan.get("active_eta") if is_active else None
        t.add_row(dot(c), Text(r["name"], style=f"bold {TEXT}" if is_active else TEXT), Text(r["state"], style=c),
                  Text(hm(r["start"]), style=TEXT), Text(f"~{hm(eta)}" if eta else "-", style=TEXT),
                  Text(data.fmt_dur(r["elapsed"]), style=TEXT),
                  Text(f"${cost[r['name']]:,.0f}" if cost.get(r["name"]) else "-", style=EMBER),
                  Text("active" if is_active else "", style=GOLD))
    for q in plan.get("queued", []):
        c = RED if q["late"] else BLUE
        note = f"start ~{hm(q['start'])} is after {hm(plan['no_new_after'])}: will not start" if q["late"] else ""
        t.add_row(dot(c), Text(q["name"], style=c), Text("queued", style=c), Text(hm(q["start"]), style=c),
                  Text(f"~{hm(q['end'])}", style=c), Text(data.fmt_dur(q["dur"]), style=c), "", Text(note, style=RED))
    foot = Text(f"ETA from the median of the last {plan.get('n_finished', 0)} finished runs "
                f"({data.fmt_dur(plan.get('median'))})", style=DIM)
    if plan.get("no_new_after"):
        foot.append(f"   no new run after {hm(plan['no_new_after'])} (red = will not start)", style=DIM)
    return Group(t, foot)


def render_waiting(d: dict, full: bool = False):
    a = d["actions"]
    if a["hint"]:
        return hint(a["hint"])
    items = a["items"] if full else recent_actions(d)
    if not items:
        old = len(a["items"])
        return Text("Nothing waiting on you." + (f" ({old} older sections: press u)" if old and not full else ""), style=GREEN)
    out = []
    for it in items:
        c = RED if it["blocker"] else GOLD
        out.append(dot(c) + Text(f"{it['run']} ", style=f"bold {c}") +
                   Text(f"{it['file']}  {it['time']:%a %H:%M}", style=DIM))
        for line in it["lines"][: (60 if full else 3)]:
            out.append(Text("  " + (line if full else line[:200]), style=RED if (it["blocker"] and "block" in line.lower()) else TEXT))
        if not full and len(it["lines"]) > 3:
            out.append(Text(f"  +{len(it['lines']) - 3} more (press u)", style=DIM))
    older = len(a["items"]) - len(items)
    if older > 0 and not full:
        out.append(Text(f"{older} older sections: press u", style=DIM))
    return Group(*out)


def weekly_line(d: dict) -> list[Text]:
    w = d["state"].get("weekly")
    if not w:
        return [Text("week     ", style=GOLD) + Text("press w to store the % and reset from claude.ai", style=f"italic {BLUE}")]
    reset = data.parse_time(w.get("reset"))
    pct = float(w.get("pct", 0))
    if not reset or reset <= d["now"]:
        return [Text("week     ", style=GOLD) + Text(f"{pct:.0f}% stored, reset passed: press w", style=f"italic {YELLOW}")]
    left = reset - d["now"]
    elapsed_d = max(7 - left.total_seconds() / 86400, 0.05)
    burn = pct / elapsed_d
    left_d = left.total_seconds() / 86400
    allow = (100 - pct) / left_d if left_d > 0 else 0
    out_in = (100 - pct) / burn if burn > 0 else None
    over = out_in is not None and out_in < left_d
    l1 = Text("week     ", style=GOLD) + bar(pct / 100, 14, RED if over else EMBER) + \
        Text(f" {pct:.0f}% used, resets in {data.fmt_dur(left)} ({reset:%a %H:%M})", style=TEXT)
    l2 = Text("         burn ", style=DIM) + Text(f"{burn:.1f}%/day", style=RED if over else TEXT) + \
        Text(f" vs {allow:.1f}%/day left", style=TEXT)
    if over:
        l2.append(f"  out in {out_in:.1f} d, {left_d - out_in:.1f} d before reset", style=RED)
    return [l1, l2]


def render_usage(d: dict):
    u, b = d["usage"], d["block"]
    if u["hint"] and not u["days"] and not u["blocks"] and not b:
        return hint(u["hint"])
    out = [Text("API price equivalent, not a bill", style=f"italic {DIM}")]
    if b:
        span = (b["end"] - b["start"]).total_seconds()
        frac = (d["now"] - b["start"]).total_seconds() / span if span else 0
        out.append(Text("5h block ", style=GOLD) + bar(frac, 18) +
                   Text(f" ends {hm(b['end'])} ", style=TEXT) + Text(f"({b['src']})", style=BLUE if b["src"] == "manual" else DIM))
        cost = Text(f"         ${b['cost']:,.2f} since {hm(b['start'])}", style=TEXT)
        if b["young"]:
            cost.append(f"  projection hidden: block is {data.fmt_dur(b['age'])} old (shown from 30m)", style=DIM)
        else:
            if b["projection"]:
                cost.append(f"  projected ${b['projection']:,.2f}", style=DIM)
            if b["limit_pct"] is not None:
                cost.append(f"  {b['limit_pct']:.0f}% of max", style=RED if b["limit_pct"] >= 100 else DIM)
        out.append(cost)
        if b["burn"]:
            out.append(Text("burn     ", style=GOLD) + Text(f"${b['burn']:,.2f}/h", style=EMBER))
        out.append(Text("         press b to type the reset from claude.ai", style=f"italic {DIM}"))
    else:
        out.append(Text("5h block ", style=GOLD) + Text("no active window (press b to type the reset)", style=DIM))
    out += weekly_line(d)
    fam = d["today"]["families"]
    total = sum(fam.values())
    local_today = d["local"].get("days", {}).get(d["now"].date())
    day = next((x for x in u["days"] if x["date"] == d["now"].date()), None)
    cost_today = local_today if local_today is not None else (day["cost"] if day else 0)
    line = Text("today    ", style=GOLD) + Text(f"${cost_today:,.2f}", style=TEXT)
    if total > 0:
        o, s = fam["opus"] / total, fam["sonnet"] / total
        line.append(f"   opus {o:.0%}", style=RED if o > tips.OPUS_SHARE_LIMIT else TEXT)
        line.append(f"  sonnet {s:.0%}", style=TEXT)
        if fam["haiku"] / total >= 0.005:
            line.append(f"  haiku {fam['haiku'] / total:.0%}", style=DIM)
    out.append(line)
    cr = d["api_credit"]
    if cr:
        days = cr["days"]
        col = RED if days is not None and days < 0 else YELLOW if days is not None and days <= 3 else TEXT
        txt = f"USD {cr['usd']:,.0f} left" + (f", {days} days" if days is not None and days >= 0 else ", expired" if days is not None else "")
        out.append(Text("API CREDIT ", style=GOLD) + Text(txt, style=col))
    week = [x for x in u["days"] if d["now"].date() - x["date"] < timedelta(days=7)]
    by_date = {x["date"]: x["cost"] for x in week} or {k: v for k, v in d["local"].get("days", {}).items()}
    top = max(by_date.values(), default=0) or 1
    out.append(Text("last 7 days", style=GOLD))
    for i in range(6, -1, -1):
        day_ = d["now"].date() - timedelta(days=i)
        c = by_date.get(day_, 0.0)
        out.append(Text(f" {day_:%a} ", style=DIM) + bar(c / top, 18) + Text(f" ${c:,.0f}", style=TEXT))
    out.append(Text("Real weekly limit: claude.ai > Settings > Usage", style=f"italic {BLUE}"))
    return Group(*out)


def render_runcost(d: dict):
    rows = d["per_run"]
    if d["local"]["hint"]:
        return hint(d["local"]["hint"])
    if not rows:
        return Text("No run with cost in the local transcripts yet.", style=DIM)
    t = grid_table(("", {"width": 2}), ("run", {"ratio": 1, "no_wrap": True, "min_width": 18}),
                   ("cost", {"width": 7, "justify": "right"}), ("min", {"width": 4, "justify": "right"}),
                   ("$/h", {"width": 5, "justify": "right"}), ("opus", {"width": 4, "justify": "right"}),
                   ("idle", {"width": 4, "justify": "right"}), ("", {"width": 4}))
    med = d["median_run"]
    for c in rows[:16]:
        hot = bool(med) and c["cost"] > 2 * med
        t.add_row(dot(STATE[c["state"]]), Text(c["name"], style=TEXT),
                  Text(f"${c['cost']:,.2f}", style=RED if hot else EMBER),
                  Text(f"{c['minutes']:.0f}", style=DIM), Text(f"{c['per_h']:,.1f}" if c["minutes"] else "-", style=DIM),
                  Text(f"{c['opus_share']:.0%}", style=RED if c["opus_share"] > tips.OPUS_SHARE_LIMIT else DIM),
                  Text(f"{c['idle_share']:.0%}", style=YELLOW if c["idle_share"] > 0.3 else DIM),
                  Text(f"{c['cost'] / med:.1f}x" if hot else "", style=RED))
    return Group(t, Text(f"median of finished ${med:,.2f}   red = over 2x", style=DIM))


def render_health(d: dict):
    h, now = d["health"], d["now"]
    out = []
    unity = Text("Unity     ", style=GOLD)
    if h["unity_running"]:
        unity += Text("running", style=GREEN)
    else:
        unity += Text("not running", style=DIM)
    out.append(unity)
    qw = Text("quiet     ", style=GOLD)
    if h["quiet_now"]:
        qw.append("inside 03:00-05:30 now", style=RED if h["unity_running"] else YELLOW)
        if h["unity_running"]:
            qw.append("  Unity open: the nightly refuses to start", style=RED)
    else:
        qw.append(f"03:00-05:30 starts in {data.fmt_dur(h['quiet_in'])}", style=DIM)
    out.append(qw)
    bc = h["build_check"]
    line = Text("build-chk ", style=GOLD)
    if bc:
        ok = bc["ok"]
        line.append("PASS" if ok else "FAIL" if ok is False else "?", style=GREEN if ok else RED if ok is False else YELLOW)
        line.append(f" {bc['mode']} in {bc['where']}, {data.fmt_dur(now - bc['when'])} ago", style=TEXT)
        if ok is False and bc["failures"]:
            line.append(f" ({bc['failures']} failures)", style=RED)
    else:
        line.append("no result found", style=DIM)
    out.append(line)
    bl = Text("newest bld ", style=GOLD)
    if h["build"]:
        bl.append(f"{h['build']['name']}, {data.fmt_dur(now - h['build']['when'])} ago", style=TEXT)
    else:
        bl.append("no folder in Builds", style=DIM)
    out.append(bl)
    fb = Text("feedback  ", style=GOLD)
    if h["feedback"]:
        n = h["feedback"]["new"]
        fb.append(f"{n} new file(s)", style=YELLOW if n else GREEN)
        fb.append(f" since the last analyse ({h['feedback']['since']:%d %b %H:%M})" if h["feedback"]["since"] else " (never analysed)", style=DIM)
    else:
        fb.append("no playtest-feedback folder", style=DIM)
    out.append(fb)
    return Group(*out)


def render_branches(d: dict):
    g = d["git"]
    if g["hint"]:
        return hint(g["hint"])
    out = [Text(f"on origin, not merged ({len(g['unmerged'])})", style=f"bold {EMBER}")]
    for b in g["unmerged"][:6]:
        out.append(dot(EMBER) + Text(b["name"].removeprefix("origin/")[:40], style=TEXT) + Text(f"  {b['when']}", style=DIM))
    bundles = g.get("bundles", [])
    out.append(Text(f"bundles not on origin, fetch and push ({len(bundles)})", style=f"bold {YELLOW if bundles else GOLD}"))
    for b in bundles[:6]:
        out.append(dot(YELLOW) + Text(b["branch"][:36], style=TEXT) +
                   Text(f"  {b['file'][:30]}  {b['when']:%a %H:%M}  {b['why']}", style=DIM))
    out.append(Text(f"merged into main ({len(g['merged'])})", style=f"bold {GOLD}"))
    for b in g["merged"][:3]:
        out.append(dot(GREEN) + Text(b["name"].removeprefix("origin/")[:40], style=DIM) + Text(f"  {b['when']}", style=DIM))
    out.append(Text("main", style=f"bold {GOLD}"))
    for c in g["recent"]:
        out.append(Text(f" {c['hash']} ", style=EMBER) + Text(c["subject"][:60], style=TEXT, overflow="ellipsis") +
                   Text(f"  {c['when']}", style=DIM))
    return Group(*out)


def render_project(d: dict):
    p = d["project"]
    out = []
    if p["hint"]:
        out.append(hint(p["hint"]))
    if not p["filled"]:
        out.append(Text("Real spend: costs.json is not filled in (all zero)", style=YELLOW))
        out.append(Text(f"Claude Code API price equivalent EUR {p['equiv_eur']:,.2f} (not a saving until you fill in what you paid)", style=TEXT))
        return Group(*out)
    out.append(Text("Real spend (EUR)", style=f"bold {GOLD}") + Text(f"   since {p['since'] or '-'}", style=DIM))
    out.append(Text(f" total EUR {p['total']:,.2f}", style=f"bold {EMBER}") + Text(f"   this month EUR {p['month']:,.2f}", style=TEXT))
    for cat, (tot, mon) in sorted(p["cats"].items(), key=lambda kv: -kv[1][0]):
        out.append(Text(f"  {cat:<14}", style=TEXT) + Text(f"{tot:>10,.2f}", style=TEXT) + Text(f"  {mon:>8,.2f} this month", style=DIM))
    out.append(Text(f"  Replicate from {p['ledgers']} ledger.json file(s)", style=DIM))
    out.append(Text("Claude Code API price equivalent", style=f"bold {GOLD}"))
    out.append(Text(f" EUR {p['equiv_eur']:,.2f}", style=EMBER) + Text(f"  vs EUR {p['claude_paid']:,.2f} paid for Claude", style=TEXT))
    if p["claude_paid"] > 0:
        style = GREEN if p["saved"] >= 0 else RED
        out.append(Text(f" subscription {'saved' if p['saved'] >= 0 else 'cost extra'}: EUR {abs(p['saved']):,.2f}", style=f"bold {style}"))
    else:
        out.append(Text(" no Claude payment filled in: no saving shown", style=DIM))
    out.append(Text(f" (1 USD = {p['rate']} EUR, set in costs.json)", style=DIM))
    return Group(*out)


def render_tips(d: dict):
    ts = tips.tips_for(d)
    if not ts:
        return Text("No tips: nothing stands out.", style=GREEN)
    return Group(*[Text("\u2022 ", style=EMBER) + Text(t.text, style=TEXT) for t in ts])


# ---------------------------------------------------------------- app

PANELS = [("chain", "CHAIN", render_chain), ("waiting", "WAITING ON YOU", render_waiting),
          ("usage", "USAGE", render_usage), ("runcost", "RUN COST", render_runcost),
          ("health", "HEALTH", render_health), ("branches", "BRANCHES", render_branches),
          ("project", "PROJECT COST", render_project), ("tips", "TIPS", render_tips)]


class Prompt(ModalScreen):
    """One line of input. Dismisses with the text, or None on escape."""

    BINDINGS = [("escape", "cancel", "cancel")]
    CSS = f"""
    Prompt {{ align: center middle; }}
    #box {{ width: 90; height: 7; border: round {EMBER}; background: #120f0d; padding: 1 2; }}
    """

    def __init__(self, question: str):
        super().__init__()
        self.question = question

    def compose(self) -> ComposeResult:
        with VerticalScroll(id="box"):
            yield Label(self.question)
            yield Input(id="answer")

    def on_mount(self) -> None:
        self.query_one(Input).focus()

    def on_input_submitted(self, ev: Input.Submitted) -> None:
        self.dismiss(ev.value)

    def action_cancel(self) -> None:
        self.dismiss(None)


class ActionsScreen(Screen):
    """Every USER ACTIONS section, full screen. u, escape or q closes it."""

    BINDINGS = [("escape", "close", "back"), ("u", "close", "back"), ("q", "close", "back")]
    CSS = f"ActionsScreen {{ background: #0d0b0a; }} #all {{ border: round {EMBER} 60%; padding: 0 1; height: 1fr; }}"

    def __init__(self, d: dict):
        super().__init__()
        self.d = d

    def compose(self) -> ComposeResult:
        with VerticalScroll(id="all"):
            yield Static(render_waiting(self.d, full=True))
        yield Footer()

    def action_close(self) -> None:
        self.app.pop_screen()


class Cockpit(App):
    CSS = f"""
    Screen {{ background: #0d0b0a; color: {TEXT}; }}
    #header {{ height: 1; background: #1a1411; padding: 0 1; }}
    #now {{ height: 5; border: round {GOLD} 70%; border-title-color: {GOLD}; border-title-style: bold;
            background: #120f0d; padding: 0 1; }}
    #grid {{ grid-size: 3 3; grid-columns: 1fr 1fr 1fr; grid-rows: 3fr 4fr 3fr; grid-gutter: 0 1; padding: 0 1; height: 1fr; }}
    .panel {{ border: round {EMBER} 60%; border-title-color: {GOLD}; border-title-style: bold;
              background: #120f0d; padding: 0 1; height: 100%; }}
    .panel:focus {{ border: round {GOLD}; }}
    #chain {{ column-span: 2; }}
    #grid.actions-only {{ grid-size: 1 1; grid-columns: 1fr; grid-rows: 1fr; }}
    Footer {{ background: #1a1411; }}
    """
    BINDINGS = [("q", "quit", "quit"), ("r", "refresh", "refresh"), ("l", "open_log", "log"),
                ("a", "actions_only", "actions only"), ("u", "all_actions", "all actions"),
                ("b", "block_reset", "5h reset"), ("w", "weekly", "weekly")]

    def __init__(self, env: data.Env):
        super().__init__()
        self.env = env
        self.data: dict | None = None
        self.left = REFRESH_S
        self.actions_only = False

    def compose(self) -> ComposeResult:
        yield Static(Text("loading...", style=DIM), id="header")
        now = Static(Text("loading...", style=DIM), id="now")
        now.border_title = "NOW"
        yield now
        with Grid(id="grid"):
            for pid, title, _ in PANELS:
                p = VerticalScroll(Static(Text("loading...", style=DIM), id=f"{pid}-body"), id=pid, classes="panel")
                p.border_title = title
                yield p
        yield Footer()

    def on_mount(self) -> None:
        self.action_refresh()
        self.set_interval(1, self.tick)

    def tick(self) -> None:
        self.left -= 1
        if self.left <= 0:
            self.action_refresh()
        elif self.data:
            self.data["now"] = self.env.now()
            self.query_one("#header", Static).update(render_header(self.data, self.left))

    def action_refresh(self) -> None:
        self.left = REFRESH_S
        self.run_worker(self.load, thread=True, exclusive=True)

    def load(self) -> None:
        d = data.collect(self.env)
        self.call_from_thread(self.show, d)

    def show(self, d: dict) -> None:
        self.data = d
        self.query_one("#header", Static).update(render_header(d, self.left))
        self.query_one("#now", Static).update(render_now(d))
        for pid, _, fn in PANELS:
            body = fn(d, self.actions_only) if pid == "waiting" else fn(d)
            self.query_one(f"#{pid}-body", Static).update(body)

    def action_open_log(self) -> None:
        active = self.data and self.data["runs"]["active"]
        if not active or not active["path"]:
            self.notify("No active run log.", severity="warning")
            return
        if sys.platform == "win32":
            subprocess.Popen(["notepad.exe", active["path"]])
        else:
            self.notify(active["path"], title="Notepad is Windows only")

    def action_actions_only(self) -> None:
        self.actions_only = not self.actions_only
        self.query_one("#grid").set_class(self.actions_only, "actions-only")
        for pid, _, _ in PANELS:
            if pid != "waiting":
                self.query_one(f"#{pid}").display = not self.actions_only
        if self.data:
            self.query_one("#waiting-body", Static).update(render_waiting(self.data, self.actions_only))

    def action_all_actions(self) -> None:
        if self.data:
            self.push_screen(ActionsScreen(self.data))

    def action_block_reset(self) -> None:
        self.push_screen(Prompt("Time left on the 5h block, from claude.ai Settings > Usage: 3h12, 3:12 or 01:12"),
                         self.set_block)

    def action_weekly(self) -> None:
        self.push_screen(Prompt("Weekly: percent used, then time left (62 4d3h) or the reset (62 2026-10-27 09:00)"),
                         self.set_weekly)

    def set_block(self, text: str | None) -> None:
        if text is None:
            return
        left = data.parse_remaining(text)
        if not left:
            self.notify(f"Could not read '{text}'. Use 3h12, 3:12 or 01:12.", severity="error")
            return
        end = (self.env.now() + left).replace(second=0, microsecond=0)
        data.save_state(self.env, block_end_manual=end.isoformat(), block_set_at=self.env.now().replace(microsecond=0).isoformat())
        self.notify(f"5h block ends {end:%H:%M} (manual)")
        self.action_refresh()

    def set_weekly(self, text: str | None) -> None:
        if text is None:
            return
        w = data.parse_weekly(text, self.env.now())
        if not w:
            self.notify(f"Could not read '{text}'. Use '62 4d3h'.", severity="error")
            return
        data.save_state(self.env, weekly=w)
        self.notify(f"Weekly {w['pct']:.0f}% stored, resets {w['reset']}")
        self.action_refresh()


def main() -> None:
    ap = argparse.ArgumentParser(description="Emberfall cockpit (read-only dashboard)")
    ap.add_argument("--mock", action="store_true", help="read Tools/cockpit/mock instead of D:\\Emberfall")
    ap.add_argument("--screenshot", metavar="SVG", help="render once to an SVG file and exit")
    args = ap.parse_args()
    env = data.mock_env() if args.mock else data.live_env()
    app = Cockpit(env)
    if args.screenshot:
        import asyncio

        async def shoot():
            async with app.run_test(size=(200, 56)) as pilot:
                await app.workers.wait_for_complete()
                await pilot.pause(0.5)
                app.save_screenshot(args.screenshot)
        asyncio.run(shoot())
        return
    app.run()


if __name__ == "__main__":
    main()
