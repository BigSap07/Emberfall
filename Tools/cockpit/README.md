# Emberfall cockpit

A full-screen, read-only terminal dashboard for the agent runs on this PC. It refreshes every
30 seconds and only reads: files under `D:\Emberfall`, `ccusage`, `git log`, `git branch` and the
process list. It never writes, posts or fetches, and it never reads secrets or `supabase.json`.

![cockpit on mock data](screenshot-mock.png)

## Install

```powershell
pip install textual
```

Python 3.11 or later (3.14 is fine). Optional:

- `npm i -g ccusage` for the 7-day bars and the Opus tip. Without it the cockpit falls back
  to `npx ccusage@latest` and, if Node is missing too, shows a hint in those panels.
- `pip install psutil` makes the Unity process check cheaper. Without it the cockpit asks
  PowerShell (`Get-Process Unity`).
- Git for Windows on `PATH` for BRANCHES. Without it the panel shows a hint.

## Start

```powershell
Tools\cockpit\cockpit.ps1          # live data from D:\Emberfall
Tools\cockpit\cockpit.ps1 -Mock    # the sample data in Tools\cockpit\mock
```

Keys: `q` quit, `r` refresh now, `l` open the active run log in Notepad, `a` toggle "actions only", `u` all your actions full screen, `b` type the 5h reset from claude.ai (`3h12`, `3:12`, `01:12`), `w` type the weekly percent and time left (`62 4d3h`). `b` and `w` write `Tools/cockpit/state.json` (gitignored). Panels scroll with the mouse or Tab and the arrow keys.
Set `EMBERFALL_ROOT` to read another folder than `D:\Emberfall`.

## Fill in costs.json

`Tools/cockpit/costs.json` holds the real money spent, all in EUR. Entries with 0 are ignored.

- `usd_to_eur`: the rate used for Replicate and for the Claude Code API price equivalent.
- `subscriptions`: one entry per plan. `monthly_eur` is the price, `start_month` the first month
  paid (`YYYY-MM`), `end_month` the last month paid or `null` while it runs. A plan change is two
  entries: end the old one, start the new one the month after.
- `api_topups`: every Anthropic Console credit purchase, with `date` (`YYYY-MM-DD`) and `eur`.
- `one_off`: anything else (`category` becomes its own line in PROJECT COST: assets, store
  accounts, hardware).

Replicate spend is not typed in: it is read from every `ledger.json` under `D:\Emberfall\ui-gen`
(a list of entries, or an object with an `entries` list; each entry's cost is taken from
`cost_usd`, `usd`, `cost` or `price`, and its month from `date` or `created_at`).

PROJECT COST then shows the total since the first month, this month, each category, and the
API price equivalent of all Claude Code usage beside what was paid for Claude. The difference
is what the subscription saved.

## What the panels read

| Panel | Source |
|---|---|
| Header | `git log -2 origin/main`, the Unity process, the clock, a WAITING badge (red when a run is BLOCKED or a section says blocker) |
| NOW | the active run: newest "start/started/Starting X" line in any `tmp\*_running_log.md` without a later "X RUN DONE"; age of its last log line (yellow 15 min, red 30); `Deadline HH:MM, stop starting work HH:MM` from its start line; its cost vs the median of finished runs |
| CHAIN | last 4 done runs, running ones, queued ones with ETA (median of the last 8 finished runs, or the prompt's `Timebox N minutes`). Queued = the newest `OWNER CHAIN UPDATE: after X run <path>, then ...` line plus `handoff\QUEUE.txt`, never old PROMPT files. Red = predicted start after "No new run after HH:MM". A run is DONE only by "X RUN DONE" or by "RUN DONE" in its log's last line |
| WAITING ON YOU | `USER ACTIONS` sections of `handoff\*\REPORT.md` (else BOARD.md); OWNER NOTE and log lines inside them are dropped. Panel and badge: last 2 days, `u`: all |
| USAGE | 5h block from the first message in `~\.claude\projects` (no flooring, "est, local logs only"), or the end typed with `b` ("manual", until it passes); projection and % hidden while younger than 30 min; weekly line from `w`; today's Opus/Sonnet share; API CREDIT from `costs.json`; 7 days from `ccusage daily`. claude.ai and the desktop app share the limit but are not in local logs |
| RUN COST | cost per run from the transcripts (see below): cost, minutes, $/h, Opus share, idle share (sleep/poll lines), flag over 2x the median |
| HEALTH | Unity process, the 03:00-05:30 nightly window, newest `Game*\STATUS.md` build-check result, newest folder in `Builds`, files in `playtest-feedback` newer than DIGEST/ANALYSE |
| BRANCHES | `git branch -r --merged/--no-merged origin/main`, plus `handoff\*.bundle` whose branch is not on origin (as of the last fetch) |
| PROJECT COST | `costs.json` and the Replicate ledgers; two lines while costs.json is all zero |
| TIPS | `tips.py`, at most 6, highest priority first |

Cost per run: `localusage.py` parses `~\.claude\projects\**\*.jsonl` incrementally (size and offset per file in `tmp\cockpit-cache.json`, 12 days), dedupes on message.id + requestId, prices per model (`localusage.PRICING`, override in `costs.json` under `pricing`), and attributes a message to a run whose start..end window holds it, if its cwd is the play slot or a run worktree (`run_cwd_regex` in costs.json). Per-day sums match `ccusage daily` within 0.2 percent.

costs.json also takes `"api_credit": {"remaining_usd": 31, "expires": "2026-10-31"}` for the USAGE line.

## Add a tip rule

All rules live in `tips.py`. Add a function that takes the data dict and returns a list of
`Tip(priority, text)`, decorate it with `@rule`, and put the number behind the tip in the text:

```python
@rule
def long_runs(d):
    slow = [r for r in d["runs"]["runs"] if r["elapsed"] and r["elapsed"].total_seconds() > 8 * 3600]
    return [Tip(35, f"{r['name']} has run {r['elapsed'].total_seconds() / 3600:.0f} h: split it or chain it.") for r in slow]
```

Useful keys: `d["usage"]["days"]` (date, cost, models, cache_read, tokens), `d["usage"]["blocks"]`, `d["block"]`,
`d["runs"]["runs"]`, `d["chain"]`, `d["today"]`, `d["per_run"]`, `d["median_run"]`, `d["project"]`, `d["claude_md_words"]`.
A rule that raises shows as one "rule failed" line instead of breaking the panel.

## Tests

```powershell
python Tools\cockpit\test_cockpit.py
python Tools\cockpit\cockpit.py --mock --screenshot cockpit.svg
```

The tests run every collector, every tip rule and the keys against `mock/`, and the collectors
once more with no git, no ccusage and no folders. The mock fixes the clock at 23 Oct 2026 14:00
and gives file times in `mock/mock.json`, so the result does not depend on the checkout. It has its own
`mock/CLAUDE.md` and small transcripts in `mock/claude-projects`; no test reads the real ones.

## Web cockpit (V3)

A browser view of the same data, in an Edge app window. The terminal dashboard above stays as the fallback.

- Start: double-click `cockpit-web.bat`, or run `make-shortcut.ps1` once for an `Emberfall Cockpit` icon on the Desktop (it pins nothing).
  By hand: `python serve.py [--port 8765] [--mock]`, then open http://127.0.0.1:8765. Only stdlib Python is needed.
- `serve.py` binds 127.0.0.1 only. `GET /api/state` is `state_builder.build_state(data.collect(env))` cached for 5 s; the page polls every 10 s.
- `POST /api/set` needs the header `X-Cockpit: 1` and accepts only `block_reset` ("3h12", "3:12", "01:12"), `weekly` ("62 2d13h", or "62" when a reset is already stored) and `playtest_date` (YYYY-MM-DD or empty). They write `state.json` (gitignored) through the same parsers as the TUI keys `b` and `w`. Anything else is a 400.
- `web/SCHEMA.md` is the contract with `web/app.js`, `web/state.sample.json` the sample. `python state_builder.py [--mock]` prints the JSON.
- Inputs that are not collected: `progress.json` (the six Road to a real game tracks and milestones, edit by hand) and the `plan` block of `costs.json` (prices, start month, renewal day, optional `api_value_eur`).
- `python test_cockpit.py` covers the builder (keys and types against the sample) and the server (header, keys, formats).

### Run sources and Replicate spend

- A run is listed from any of three sources, and a run known to more than one shows once (the key is the run id up to the first dash, so R43 and R43-UI-ART-GEN are one run):
  the queue runner (`tmp/runs.csv`, `handoff/QUEUE.txt`, `handoff/run-queue.lock`), the convention `tmp/<RUN>_running_log.md`, and discovery of any `*running_log*.md` / `RUNNING_LOG*.md` under the roots `tmp`, `ui-gen`, `handoff` (two folders deep; set `run_roots` in `costs.json` to change them).
- A discovered log is running when it changed in the last 20 minutes and has no final line, DONE on `RUN DONE`, BLOCKED on a last line with `BLOCKED`, otherwise stale. The queue's recorded end beats a log that never got a final line (a FAILED run no longer shows as running).
- A log stamp later than now or later than the file time is clamped to that time and flagged, so no elapsed time is negative or in the future.
- Spend ledgers: every `ui-gen/ledger*.json` and `ui-gen/*/ledger*.json`. The Replicate card shows images, cost and the cap per ledger, and a total labelled as an estimate (price per image assumed, `replicate_price_per_image` in `costs.json`, default 0.04; check Replicate billing). A ledger changed in the last 5 minutes shows as generating with images per minute. An unreadable ledger is listed, not counted, and raises a hint.
- Rule for agents: interactive runs log to `tmp/<RUN>_running_log.md` and end with `RUN DONE` or `BLOCKED` (handoff/STANDING-RULES.txt).
