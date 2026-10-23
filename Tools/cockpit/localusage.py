"""Per-message cost and 5h windows from the Claude Code transcripts (~/.claude/projects/**/*.jsonl).

ccusage floors a block start to the full hour, groups its session output by session id with a
date-only stamp, and cannot see claude.ai or the desktop app. This reads the transcripts itself:
one record per API message (deduped on message.id + requestId, keeping the largest output count,
like ccusage), priced per model. The scan is incremental: the cache file remembers each
transcript's size and read offset, so a refresh only parses what was appended.

The only file written is the cache (tmp\\cockpit-cache.json). Nothing here touches a transcript.
"""

from __future__ import annotations

import json
import os
import re
import statistics
from collections import namedtuple
from datetime import datetime
from pathlib import Path

CACHE_VERSION = 2
WINDOW_S = 5 * 3600

# USD per million tokens: input, output, cache write 5m, cache write 1h, cache read.
# ccusage 20 keeps no readable price cache (it is a native binary), so these were fitted to its own
# `daily --breakdown` output: 30 days of opus-5-5 and 19 days of sonnet-5-5 reproduce to the cent
# once the 1h and 5m cache writes are priced apart. haiku-5-5 does not fit one rate (0.05 to 0.21
# per day); its row is a middle value, and haiku is below 0.5 percent of any day. Override or add
# a model in costs.json under "pricing": {"<model>": {"in":..,"out":..,"cc5":..,"cc1":..,"cr":..}}.
PRICING = {
    "claude-opus-5-5": (2.0, 20.0, 5.0, 8.0, 0.2),
    "claude-sonnet-5-5": (1.0, 10.0, 2.5, 4.0, 0.1),
    "claude-haiku-5-5": (0.17, 1.7, 0.425, 0.68, 0.017),
    "claude-opus-5": (5.0, 25.0, 10.0, 10.0, 0.5),
    "claude-sonnet-5": (2.0, 10.0, 4.0, 4.0, 0.2),
    "claude-opus-4-7": (5.0, 25.0, 6.25, 6.25, 0.5),
    "claude-opus-4-6": (5.0, 25.0, 6.25, 6.25, 0.5),
    "claude-sonnet-4-6": (3.0, 15.0, 3.75, 3.75, 0.3),
}
FAMILY_FALLBACK = {"opus": "claude-opus-5-5", "sonnet": "claude-sonnet-5-5", "haiku": "claude-haiku-5-5"}
PRICE_KEYS = ("in", "out", "cc5", "cc1", "cr")

Msg = namedtuple("Msg", "ts cwd model inp out cc5 cc1 cr sess")

TYPE_RE = re.compile(rb'"type":\s*"(user|assistant)"')
TS_RE = re.compile(rb'"timestamp":\s*"([^"]+)"')


def family(model: str) -> str:
    m = (model or "").lower()
    for f in ("opus", "sonnet", "haiku"):
        if f in m:
            return f
    return "other"


def price_table(overrides: dict | None) -> dict:
    table = dict(PRICING)
    for model, row in (overrides or {}).items():
        if isinstance(row, dict):
            base = table.get(model) or PRICING[FAMILY_FALLBACK.get(family(model), "claude-sonnet-5-5")]
            table[model] = tuple(float(row.get(k, base[i])) for i, k in enumerate(PRICE_KEYS))
    return table


def price_of(model: str, table: dict):
    if model in table:
        return table[model]
    fam = family(model)
    return table.get(FAMILY_FALLBACK.get(fam, "")) if fam != "other" else None


def msg_cost(m: Msg, table: dict) -> float:
    p = price_of(m.model, table)
    if not p:
        return 0.0
    return (m.inp * p[0] + m.out * p[1] + m.cc5 * p[2] + m.cc1 * p[3] + m.cr * p[4]) / 1e6


def epoch(s: str) -> float | None:
    try:
        return datetime.fromisoformat(s.replace("Z", "+00:00")).timestamp()
    except ValueError:
        return None


def intern(lst: list, value: str) -> int:
    try:
        return lst.index(value)
    except ValueError:
        lst.append(value)
        return len(lst) - 1


def empty_store() -> dict:
    return {"v": CACHE_VERSION, "files": {}, "cwds": [], "models": [], "sess": [], "msgs": {}, "events": []}


def load_store(cache: Path | None) -> dict:
    if cache:
        try:
            st = json.loads(cache.read_text(encoding="utf-8"))
            if st.get("v") == CACHE_VERSION:
                return st
        except (OSError, json.JSONDecodeError, AttributeError):
            pass
    return empty_store()


def save_store(cache: Path, st: dict) -> None:
    try:
        tmp = cache.with_suffix(".tmp")
        tmp.write_text(json.dumps(st, separators=(",", ":")), encoding="utf-8")
        os.replace(tmp, cache)
    except OSError:
        pass


def parse_chunk(chunk: bytes, st: dict, events: set, sess_id: str) -> None:
    for line in chunk.split(b"\n"):
        if not line:
            continue
        # The top-level "type" sits after the message body, so look at the whole line.
        kind = TYPE_RE.search(line)
        if not kind:
            continue
        if kind.group(1) == b"user" or b'"usage"' not in line:
            hits = TS_RE.findall(line)
            ts = epoch(hits[-1].decode("ascii", "replace")) if hits else None
            if ts:
                events.add(int(ts))
            continue
        try:
            d = json.loads(line)
        except ValueError:
            continue
        ts = epoch(str(d.get("timestamp", "")))
        if not ts:
            continue
        events.add(int(ts))
        m = d.get("message") or {}
        u = m.get("usage")
        if not isinstance(u, dict):
            continue
        cc = u.get("cache_creation") or {}
        total_cc = int(u.get("cache_creation_input_tokens") or 0)
        cc1 = int(cc.get("ephemeral_1h_input_tokens") or 0)
        row = [ts, intern(st["cwds"], str(d.get("cwd") or "")), intern(st["models"], str(m.get("model") or "")),
               int(u.get("input_tokens") or 0), int(u.get("output_tokens") or 0), max(0, total_cc - cc1), cc1,
               int(u.get("cache_read_input_tokens") or 0), intern(st["sess"], str(d.get("sessionId") or sess_id))]
        key = f"{m.get('id')}|{d.get('requestId')}" if m.get("id") and d.get("requestId") else f"u:{d.get('uuid')}"
        old = st["msgs"].get(key)
        if old is None or row[4] > old[4]:
            st["msgs"][key] = row


def scan(projects: Path, cache: Path | None, now_ts: float, history_days: int) -> dict:
    """Bring the store up to date. Returns {"msgs": [Msg], "events": [epoch], "scanned_bytes": n}."""
    st = load_store(cache)
    cutoff = now_ts - history_days * 86400
    events = set(st["events"])
    dirty = False
    scanned = 0
    if projects.is_dir():
        for dirpath, _, names in os.walk(projects):
            for n in names:
                if not n.endswith(".jsonl"):
                    continue
                p = Path(dirpath) / n
                try:
                    stat = p.stat()
                except OSError:
                    continue
                if stat.st_mtime < cutoff:
                    continue
                key = str(p)
                size, off = st["files"].get(key, (-1, 0))
                if size == stat.st_size:
                    continue
                start = off if 0 <= off <= stat.st_size else 0
                try:
                    with open(p, "rb") as fh:
                        fh.seek(start)
                        data = fh.read()
                except OSError:
                    continue
                end = data.rfind(b"\n") + 1
                parse_chunk(data[:end], st, events, p.stem)
                st["files"][key] = [stat.st_size, start + end]
                scanned += end
                dirty = True
    old_n = len(st["msgs"])
    st["msgs"] = {k: v for k, v in st["msgs"].items() if v[0] >= cutoff}
    events = {e for e in events if e >= cutoff}
    if len(st["msgs"]) != old_n:
        dirty = True
    st["events"] = sorted(events)
    if dirty and cache:
        save_store(cache, st)
    cw, mo, se = st["cwds"], st["models"], st["sess"]
    msgs = [Msg(r[0], cw[r[1]], mo[r[2]], r[3], r[4], r[5], r[6], r[7], se[r[8]]) for r in st["msgs"].values()]
    msgs.sort(key=lambda m: m.ts)
    return {"msgs": msgs, "events": st["events"], "scanned_bytes": scanned}


def window_start(events: list, now_ts: float) -> float | None:
    """Start of the 5h window the newest activity falls in: a new window opens at the first message
    at least 5h after the previous window's start. No flooring to the hour. None if it has ended."""
    ws = None
    for t in events:
        if ws is None or t >= ws + WINDOW_S:
            ws = t
    return ws if ws is not None and now_ts < ws + WINDOW_S else None


def to_dt(ts: float) -> datetime:
    return datetime.fromtimestamp(ts)


def median_or_zero(xs: list) -> float:
    return statistics.median(xs) if xs else 0.0
