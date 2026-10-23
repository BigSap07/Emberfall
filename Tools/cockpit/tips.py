"""Tip rules. Deterministic, no AI calls: each rule reads the collected data and returns
zero or more tips. A tip is (priority, text); higher priority shows first, at most MAX_TIPS.

To add a rule: write a function that takes the data dict from `data.collect` and returns a
list of Tip, then decorate it with @rule. Every tip should carry the number behind it.
"""

from __future__ import annotations

from dataclasses import dataclass
from datetime import timedelta

MAX_TIPS = 5
RULES = []

# Sonnet price as a share of the Opus price it would replace (input and output scale alike).
# Opus 4 and 4.1 list at 15/75 USD per Mtok, later Opus at 5/25, Sonnet at 3/15.
SONNET_RATIO_OLD_OPUS = 0.2
SONNET_RATIO_NEW_OPUS = 0.6


@dataclass
class Tip:
    priority: int
    text: str


def rule(fn):
    RULES.append(fn)
    return fn


def week_days(d: dict) -> list[dict]:
    today = d["now"].date()
    return [x for x in d["usage"]["days"] if today - x["date"] < timedelta(days=7)]


@rule
def opus_share(d):
    days = week_days(d)
    total = sum(x["cost"] for x in days)
    opus = [(m, c) for x in days for m, c in x["models"].items() if "opus" in m.lower()]
    opus_cost = sum(c for _, c in opus)
    if total <= 0 or opus_cost / total <= 0.15:
        return []
    old = ("opus-4-1", "opus-4-2025")
    saving = sum(c * (1 - (SONNET_RATIO_OLD_OPUS if any(o in m for o in old) else SONNET_RATIO_NEW_OPUS))
                 for m, c in opus)
    return [Tip(60, f"Opus is {opus_cost / total:.0%} of this week's ${total:,.0f}. "
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
def cache_read_share(d):
    days = week_days(d)
    tokens = sum(x["tokens"] for x in days)
    read = sum(x["cache_read"] for x in days)
    if tokens <= 0 or read / tokens <= 0.90:
        return []
    return [Tip(40, f"Cache reads are {read / tokens:.0%} of this week's tokens. Long sessions re-read their "
                    f"whole history each turn: /clear between unrelated tasks.")]


@rule
def block_limit(d):
    hit = [b for b in d["usage"]["blocks"] if b["limit_status"] == "exceeds"]
    logs = [r for r in d["runs"]["runs"] if r["limit_hits"]]
    tips = []
    if hit:
        b = max(hit, key=lambda b: b["start"])
        tips.append(Tip(70, f"{len(hit)} block(s) hit the token limit, last one started "
                            f"{b['start']:%a %H:%M} ({b['limit_pct'] or 0:.0f}% of the largest block). Spread heavy runs across blocks."))
    for r in logs:
        tips.append(Tip(65, f"{r['name']} log mentions a usage limit {r['limit_hits']}x: the run stalled until the block reset."))
    return tips


def tips_for(d: dict) -> list[Tip]:
    out = []
    for fn in RULES:
        try:
            out += fn(d)
        except Exception as e:  # one broken rule must not take the panel down
            out.append(Tip(0, f"rule {fn.__name__} failed: {e.__class__.__name__}"))
    out.sort(key=lambda t: t.priority, reverse=True)
    return out[:MAX_TIPS]
