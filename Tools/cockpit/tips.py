"""Tip rules. Deterministic, no AI calls: each rule reads the collected data and returns
zero or more tips. A tip is (priority, text); higher priority shows first, at most MAX_TIPS.

To add a rule: write a function that takes the data dict from `data.collect` and returns a
list of Tip, then decorate it with @rule. Every tip should carry the number behind it.
"""

from __future__ import annotations

from dataclasses import dataclass

MAX_TIPS = 6
RULES = []

SILENT_MIN = 30
OPUS_SHARE_LIMIT = 0.15
CACHE_OUTLIER = 2.5       # a session this many times the next one
CACHE_MIN_TOKENS = 50_000_000


@dataclass
class Tip:
    priority: int
    text: str


def rule(fn):
    RULES.append(fn)
    return fn


def sonnet_ratio(d: dict) -> float:
    """Sonnet's output price over Opus's, from the same price table that costs the messages."""
    t = d["local"].get("table") or {}
    try:
        return t["claude-sonnet-5-5"][1] / t["claude-opus-5-5"][1]
    except (KeyError, ZeroDivisionError):
        return 0.5


@rule
def opus_share(d):
    fam = d["today"]["families"]
    total = sum(fam.values())
    opus = fam.get("opus", 0.0)
    if total <= 0:  # no local transcripts: fall back to ccusage's day
        day = next((x for x in d["usage"]["days"] if x["date"] == d["now"].date()), None)
        if not day:
            return []
        total = day["cost"]
        opus = sum(c for m, c in day["models"].items() if "opus" in m.lower())
    if total <= 0 or opus / total <= OPUS_SHARE_LIMIT:
        return []
    saving = opus * (1 - sonnet_ratio(d))
    return [Tip(60, f"Opus is {opus / total:.0%} of today's ${total:,.0f}. "
                    f"The same work on Sonnet would cost about ${saving:,.0f} less.")]


@rule
def idle_loops(d):
    tips = []
    for c in d["per_run"]:
        r = c["run"]
        if r["sleep_hits"] < 3 or not r["elapsed"]:
            continue
        mins = r["elapsed"].total_seconds() / 60
        idle_min = min(r["idle_s"] / 60, mins)
        idle_cost = c["cost"] * idle_min / mins if mins else 0
        tips.append(Tip(50 + min(int(idle_cost), 40),
                        f"{r['name']}: {r['sleep_hits']} sleep/poll lines, ~{idle_min:.0f} min idle, "
                        f"~${idle_cost:,.2f} of its ${c['cost']:,.2f} spent waiting. Use a CHAIN or a scheduled start instead."))
    return tips


@rule
def claude_md_size(d):
    w = d["claude_md_words"]
    if w <= 2000:
        return []
    return [Tip(30, f"CLAUDE.md is {w:,} words (over 2,000). Every session pays for it; move detail into linked docs.")]


@rule
def expensive_runs(d):
    med = d["median_run"]
    if med <= 0:
        return []
    return [Tip(55, f"{c['name']} cost ${c['cost']:,.2f}, {c['cost'] / med:.1f}x the median run (${med:,.2f}).")
            for c in d["per_run"] if c["cost"] > 2 * med]


@rule
def block_limit(d):
    # Only a block that really finished over the limit counts. The active block, and a young one
    # extrapolated over 5h, never does (that was the false alarm at $0.17 spent).
    hit = [b for b in d["usage"]["blocks"] if b["limit_status"] == "exceeds" and not b["active"]
           and b["actual_end"] and b["actual_end"] <= d["now"]]
    tips = []
    if hit:
        b = max(hit, key=lambda b: b["start"])
        tips.append(Tip(70, f"{len(hit)} finished block(s) hit the token limit, last one started "
                            f"{b['start']:%a %H:%M} ({b['limit_pct'] or 0:.0f}% of the largest block). Spread heavy runs across blocks."))
    for r in d["runs"]["runs"]:
        if r["limit_hits"]:
            tips.append(Tip(65, f"{r['name']} log mentions a usage limit {r['limit_hits']}x: the run stalled until the block reset."))
    return tips


@rule
def silent_run(d):
    a = d["runs"]["active"]
    if not a or a["state"] not in ("running", "BLOCKED") or not a["last"] or not a["has_log"]:
        return []
    mins = (d["now"] - a["last"]).total_seconds() / 60
    if mins <= SILENT_MIN:
        return []
    return [Tip(75, f"{a['name']} has written nothing for {mins:.0f} min (last line {a['last']:%H:%M}). "
                    f"Check it is not stuck on a prompt or a hung Unity.")]


@rule
def deadline_pace(d):
    a, plan = d["runs"]["active"], d["chain"]
    if not a or not a["deadline"] or not a["start"] or not plan:
        return []
    med = plan["median"]
    elapsed = d["now"] - a["start"]
    if elapsed < 0.4 * (a["deadline"] - a["start"]):  # too early to say anything about pace
        return []
    # Under the median duration it ends at the median; past it, assume a quarter more of what it has taken.
    end = a["start"] + med if elapsed < med else d["now"] + elapsed / 4
    if end <= a["deadline"]:
        return []
    return [Tip(72, f"{a['name']} will pass its deadline {a['deadline']:%H:%M} at the current pace "
                    f"(predicted end {end:%H:%M}, typical run {fmt(med)}).")]


@rule
def cache_session(d):
    sess = sorted(d["today"]["sessions"].values(), key=lambda s: s["cr"], reverse=True)
    if len(sess) < 2 or sess[0]["cr"] < CACHE_MIN_TOKENS or sess[0]["cr"] < CACHE_OUTLIER * max(sess[1]["cr"], 1):
        return []
    s = sess[0]
    nxt = sess[1]["cr"]
    rel = f"{s['cr'] / nxt:.1f}x the next" if nxt >= 1_000_000 else "far above any other session"
    return [Tip(45, f"One session in {s['folder']} read {s['cr'] / 1e6:,.0f}M cache tokens today, "
                    f"{rel} (${s['cost']:,.0f}). Long context: /clear or start a fresh one.")]


def fmt(td) -> str:
    m = int(td.total_seconds() // 60)
    return f"{m // 60}h{m % 60:02d}" if m >= 60 else f"{m}m"


def tips_for(d: dict) -> list[Tip]:
    out = []
    for fn in RULES:
        try:
            out += fn(d)
        except Exception as e:  # one broken rule must not take the panel down
            out.append(Tip(0, f"rule {fn.__name__} failed: {e.__class__.__name__}"))
    out.sort(key=lambda t: t.priority, reverse=True)
    return out[:MAX_TIPS]
