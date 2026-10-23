"""Emberfall cockpit: a read-only terminal dashboard. Start it with cockpit.ps1.

Keys: q quit, r refresh, l open the active run log in Notepad, a toggle actions only.
"""

from __future__ import annotations

import argparse
import subprocess
import sys
from datetime import timedelta

from rich.console import Group
from rich.table import Table
from rich.text import Text
from textual.app import App, ComposeResult
from textual.containers import Grid
from textual.widgets import Static

import data
import tips

REFRESH_S = 30
GOLD, EMBER, TEXT, DIM, RED, GREEN, YELLOW, BLUE = (
    "#e8b04a", "#e2571e", "#d8cfc4", "#7a6f66", "#e5484d", "#46c26a", "#e0c341", "#5b9bd5")
STATE = {"running": GREEN, "BLOCKED": RED, "waiting": YELLOW, "queued": BLUE, "DONE": DIM}


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


# ---------------------------------------------------------------- panels

def render_header(d: dict, left: int) -> Text:
    g, u = d["git"], d["unity"]
    t = Text()
    t.append(" EMBERFALL COCKPIT ", style=f"bold #0d0b0a on {EMBER}")
    t.append("  main ", style=GOLD)
    t.append(g["main"][:60] or (g["hint"] or "-"), style=TEXT if g["main"] else YELLOW)
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


def render_runs(d: dict):
    r = d["runs"]
    if r["hint"]:
        return hint(r["hint"])
    t = grid_table(("", {"width": 2}), ("run", {"ratio": 3, "no_wrap": True}), ("state", {"width": 8}),
                   ("last line", {"width": 11}), ("elapsed", {"width": 7, "justify": "right"}))
    for run in r["runs"][:12]:
        c = STATE[run["state"]]
        last = f"{run['last']:%a %H:%M}" if run["last"] else "-"
        t.add_row(dot(c), Text(run["name"], style=TEXT), Text(run["state"], style=c),
                  Text(last, style=DIM), Text(data.fmt_dur(run["elapsed"]), style=TEXT))
    if len(r["runs"]) > 12:
        t.add_row("", Text(f"+{len(r['runs']) - 12} more", style=DIM), "", "", "")
    parts = [t]
    if r["active"]:
        parts.append(Text(f"\nactive: {r['active']['name']}", style=f"bold {GOLD}"))
        for line in r["tail"]:
            parts.append(Text("  " + line.strip()[:160], style=TEXT, no_wrap=True, overflow="ellipsis"))
    return Group(*parts)


def render_usage(d: dict):
    u = d["usage"]
    if u["hint"] and not u["days"] and not u["blocks"]:
        return hint(u["hint"])
    out = [Text("API price equivalent, not a bill", style=f"italic {DIM}")]
    active = next((b for b in u["blocks"] if b["active"]), None)
    if active and active["start"] and active["end"]:
        span = (active["end"] - active["start"]).total_seconds()
        frac = (d["now"] - active["start"]).total_seconds() / span if span else 0
        line = Text("5h block ", style=GOLD) + bar(frac, 20) + Text(f" ends {active['end']:%H:%M}", style=TEXT)
        out.append(line)
        cost = Text(f"         ${active['cost']:,.2f}", style=TEXT)
        if active["projection"]:
            cost.append(f"  projected ${active['projection']:,.2f}", style=DIM)
        if active["limit_pct"] is not None:
            cost.append(f"  {active['limit_pct']:.0f}% of max block", style=RED if active["limit_pct"] >= 100 else DIM)
        out.append(cost)
        if active["burn"]:
            out.append(Text("burn     ", style=GOLD) + Text(f"${active['burn']:,.2f}/h", style=EMBER))
    else:
        out.append(Text("5h block ", style=GOLD) + Text("no active block", style=DIM))
    today = next((x for x in u["days"] if x["date"] == d["now"].date()), None)
    out.append(Text("today    ", style=GOLD) + Text(f"${today['cost'] if today else 0:,.2f}", style=TEXT))
    week = [x for x in u["days"] if d["now"].date() - x["date"] < timedelta(days=7)]
    top = max((x["cost"] for x in week), default=0) or 1
    out.append(Text("last 7 days", style=GOLD))
    by_date = {x["date"]: x["cost"] for x in week}
    for i in range(6, -1, -1):
        day = d["now"].date() - timedelta(days=i)
        c = by_date.get(day, 0.0)
        out.append(Text(f" {day:%a} ", style=DIM) + bar(c / top, 18) + Text(f" ${c:,.0f}", style=TEXT))
    out.append(Text("Real weekly limit: claude.ai > Settings > Usage", style=f"italic {BLUE}"))
    return Group(*out)


def render_actions(d: dict, full: bool = False):
    a = d["actions"]
    if a["hint"]:
        return hint(a["hint"])
    if not a["items"]:
        return Text("Nothing waiting on you.", style=GREEN)
    out = []
    for it in a["items"]:
        c = RED if it["blocker"] else GOLD
        out.append(dot(c) + Text(f"{it['run']} ", style=f"bold {c}") +
                   Text(f"{it['file']}  {it['time']:%a %H:%M}", style=DIM))
        for line in it["lines"][: (40 if full else 4)]:
            out.append(Text("  " + line[:200], style=RED if (it["blocker"] and "block" in line.lower()) else TEXT))
        if not full and len(it["lines"]) > 4:
            out.append(Text(f"  +{len(it['lines']) - 4} more (press a)", style=DIM))
    return Group(*out)


def render_branches(d: dict):
    g = d["git"]
    if g["hint"]:
        return hint(g["hint"])
    out = [Text(f"on origin, not merged ({len(g['unmerged'])})", style=f"bold {EMBER}")]
    for b in g["unmerged"][:6]:
        out.append(dot(EMBER) + Text(b["name"].removeprefix("origin/")[:40], style=TEXT) + Text(f"  {b['when']}", style=DIM))
    out.append(Text(f"merged into main ({len(g['merged'])})", style=f"bold {GOLD}"))
    for b in g["merged"][:4]:
        out.append(dot(GREEN) + Text(b["name"].removeprefix("origin/")[:40], style=DIM) + Text(f"  {b['when']}", style=DIM))
    out.append(Text("main", style=f"bold {GOLD}"))
    for c in g["recent"]:
        out.append(Text(f" {c['hash']} ", style=EMBER) + Text(c["subject"][:70], style=TEXT, overflow="ellipsis") +
                   Text(f"  {c['when']}", style=DIM))
    return Group(*out)


def render_cost_per_run(d: dict):
    rows = d["per_run"]
    if not d["usage"]["sessions"]:
        return hint(d["usage"]["hint"] or "no ccusage session data")
    if not rows:
        return Text("No run with timestamps yet.", style=DIM)
    t = grid_table(("", {"width": 2}), ("run", {"ratio": 1, "no_wrap": True}),
                   ("cost", {"width": 9, "justify": "right"}), ("sess", {"width": 4, "justify": "right"}),
                   ("$/h", {"width": 6, "justify": "right"}))
    med = d["median_run"]
    for c in sorted(rows, key=lambda c: c["cost"], reverse=True)[:10]:
        hours = c["elapsed"].total_seconds() / 3600 if c["elapsed"] else 0
        hot = med and c["cost"] > 2 * med
        t.add_row(dot(STATE[c["state"]]), Text(c["name"], style=TEXT),
                  Text(f"{'~' if c['rough'] else ''}${c['cost']:,.2f}", style=RED if hot else EMBER),
                  Text(str(c["sessions"]), style=DIM), Text(f"{c['cost'] / hours:,.1f}" if hours else "-", style=DIM))
    return Group(t, Text(f"median ${med:,.2f}   ~ = date-only session match", style=DIM))


def render_project(d: dict):
    p = d["project"]
    out = []
    if p["hint"]:
        out.append(hint(p["hint"]))
    out.append(Text("Real spend (EUR)", style=f"bold {GOLD}") +
               Text(f"   since {p['since'] or '-'}", style=DIM))
    out.append(Text(f" total EUR {p['total']:,.2f}", style=f"bold {EMBER}") +
               Text(f"   this month EUR {p['month']:,.2f}", style=TEXT))
    for cat, (tot, mon) in sorted(p["cats"].items(), key=lambda kv: -kv[1][0]):
        out.append(Text(f"  {cat:<14}", style=TEXT) + Text(f"{tot:>10,.2f}", style=TEXT) +
                   Text(f"  {mon:>8,.2f} this month", style=DIM))
    if not p["cats"]:
        out.append(Text("  costs.json is empty: fill it in (see README)", style=YELLOW))
    out.append(Text(f"  Replicate from {p['ledgers']} ledger.json file(s)", style=DIM))
    out.append(Text("Claude Code API price equivalent", style=f"bold {GOLD}"))
    out.append(Text(f" EUR {p['equiv_eur']:,.2f}", style=EMBER) +
               Text(f"  vs EUR {p['claude_paid']:,.2f} paid for Claude", style=TEXT))
    style = GREEN if p["saved"] >= 0 else RED
    out.append(Text(f" subscription {'saved' if p['saved'] >= 0 else 'cost extra'}: EUR {abs(p['saved']):,.2f}", style=f"bold {style}"))
    out.append(Text(f" (1 USD = {p['rate']} EUR, set in costs.json)", style=DIM))
    return Group(*out)


def render_tips(d: dict):
    ts = tips.tips_for(d)
    if not ts:
        return Text("No tips: nothing stands out.", style=GREEN)
    return Group(*[Text("\u2022 ", style=EMBER) + Text(t.text, style=TEXT) for t in ts])


# ---------------------------------------------------------------- app

PANELS = [("runs", "RUNS"), ("usage", "USAGE"), ("actions", "YOUR ACTIONS"), ("branches", "BRANCHES"),
          ("perrun", "COST PER RUN"), ("project", "PROJECT COST"), ("tips", "TIPS")]


class Cockpit(App):
    CSS = f"""
    Screen {{ background: #0d0b0a; color: {TEXT}; }}
    #header {{ height: 1; background: #1a1411; padding: 0 1; }}
    #grid {{ grid-size: 3 3; grid-columns: 3fr 3fr 3fr; grid-rows: 4fr 3fr 3fr; grid-gutter: 0 1; padding: 0 1; }}
    .panel {{ border: round {EMBER} 60%; border-title-color: {GOLD}; border-title-style: bold;
              background: #120f0d; padding: 0 1; height: 100%; overflow-y: auto; }}
    #runs {{ column-span: 2; }}
    #tips {{ column-span: 2; }}
    #grid.actions-only {{ grid-size: 1 1; grid-columns: 1fr; grid-rows: 1fr; }}
    """
    BINDINGS = [("q", "quit", "quit"), ("r", "refresh", "refresh"), ("l", "open_log", "log"),
                ("a", "actions_only", "actions only")]

    def __init__(self, env: data.Env):
        super().__init__()
        self.env = env
        self.data: dict | None = None
        self.left = REFRESH_S
        self.actions_only = False

    def compose(self) -> ComposeResult:
        yield Static(Text("loading...", style=DIM), id="header")
        with Grid(id="grid"):
            for pid, title in PANELS:
                s = Static(Text("loading...", style=DIM), id=pid, classes="panel")
                s.border_title = title
                yield s

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
        renders = {"runs": render_runs, "usage": render_usage, "branches": render_branches,
                   "perrun": render_cost_per_run, "project": render_project, "tips": render_tips}
        self.query_one("#header", Static).update(render_header(d, self.left))
        for pid, fn in renders.items():
            self.query_one(f"#{pid}", Static).update(fn(d))
        self.query_one("#actions", Static).update(render_actions(d, self.actions_only))

    def action_open_log(self) -> None:
        active = self.data and self.data["runs"]["active"]
        if not active:
            self.notify("No active run log.", severity="warning")
            return
        if sys.platform == "win32":
            subprocess.Popen(["notepad.exe", active["path"]])
        else:
            self.notify(active["path"], title="Notepad is Windows only")

    def action_actions_only(self) -> None:
        self.actions_only = not self.actions_only
        self.query_one("#grid").set_class(self.actions_only, "actions-only")
        for pid, _ in PANELS:
            if pid != "actions":
                self.query_one(f"#{pid}").display = not self.actions_only
        if self.data:
            self.query_one("#actions", Static).update(render_actions(self.data, self.actions_only))


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
