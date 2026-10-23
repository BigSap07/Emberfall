# COCKPIT-V2 changes (real data from this PC, 2026-10-23 about 20:10 to 20:35)

ccusage 20.0.41 keys (native binary, no readable price cache, so prices were fitted to its output, see `localusage.PRICING`):
- `daily --json`: `daily[]` + `totals{cacheCreationTokens,cacheReadTokens,inputTokens,outputTokens,totalCost,totalTokens}`. Row: `period` (date), `agent`, `totalCost`, the four token counts, `totalTokens`, `modelsUsed`, `metadata{agents}`, `modelBreakdowns[{modelName,cost,inputTokens,outputTokens,cacheCreationTokens,cacheReadTokens}]`.
- `session --json`: `session[]` (427 rows) with `period` = session id, `metadata.lastActivity` only (no start, no cwd), same cost and token keys. Cannot be attributed to a run window or folder.
- `blocks --json`: `blocks[]` row: `id,startTime,endTime,actualEndTime,isActive,isGap,costUSD,entries,models,totalTokens,tokenCounts{input,output,cacheCreation,cacheRead}`, `burnRate{costPerHour,tokensPerMinute,tokensPerMinuteForIndicator}|null`, `projection{totalCost,totalTokens,remainingMinutes}|null`. `tokenLimitStatus` only with `--token-limit`.

| # | Before (old code, real data) | After (real data) |
|---|---|---|
| A1 | R28-SCREENS `DONE` (its line 3 quotes "RUN DONE"); screenshot: `active: R26-POLISH` (log file name) | R28-SCREENS `running`; active = newest start without a later done: R29B-FORMATION-TUNE (started 20:17 in the chain log); R26-POLISH/MERGE-1/R27/R29 `DONE` with real windows (R26 12:10-13:30) |
| A2 | 10 queued, 9 of them old PROMPT files (R13-UX-LAYOUT, R18, R20A..D, R26A, COCKPIT-V2...) | 2 queued from the newest OWNER CHAIN UPDATE: `friends build b`, `R30-EFFICIENCY` (+ QUEUE.txt, absent) |
| A3 | 50 items incl. `(none yet)`, `OWNER NOTE`, `RUN DONE` and log lines | 24 real sections, 2 from the last 2 days in the panel and the badge, all in key `u`. BOARD ignored when the run has a REPORT |
| A4 | block start 20:00 (floored), ends 01:00, `projected $414` / `473%` at $0.17 | start 20:05:27 (first message), ends 01:05, `est, local logs only`; projection and % hidden under 30 min; key `b` stores a manual end |
| A5 | cost 0 for every run | R26-POLISH $13.13 (80 min), R28-SCREENS $27.24 (163 min), R29-FORMATION $11.75 (98), MERGE-1 $10.83, R27 $2.61 |

A5 acceptance, my per-day sum vs `ccusage daily` (local days): 10-17 306.70/306.73, 10-18 317.28/316.88 (+0.13%), 10-19 469.75/469.76, 10-20 198.90/198.91, 10-21 48.88/48.89, 10-22 0/0, 10-23 156.64/156.74 (-0.06%). All within 0.13%. Dedupe on message.id+requestId keeping the largest output (streamed partials repeat the id). Cache writes are priced 5m and 1h apart. First scan of 12 days (1.9 GB) 12 s, then 0.7 s; cache `tmp\cockpit-cache.json`.
A5 rule: a message counts for a run when its cwd is the play slot or a run worktree under D:\Emberfall (`Game`, `Game-r28lead`, `Game-m1`) and it falls in the run's window; `Game-rNN*` breaks ties within 2h. Owner sessions in D:\Emberfall, the cockpit worktree and other projects (`pulse` ran in the same windows) are excluded.

Not as asked:
- Build-check result is read from `Game*\STATUS.md` (newest), not tmp: nothing in tmp holds one. FAIL shown today is Game-r29t, 14 failures, a work in progress.
- Runs may overlap (R28 and R29B both write now), so several can be `running`; NOW shows the newest and lists the others.
- WAITING and the badge count only sections from the last 2 days; older ones are in `u`.
- API CREDIT: costs.json has no `api_credit` yet, so the line is hidden on live data (mock shows "USD 31 left, 8 days"). Run durations in CHAIN use the median of the last 8 finished runs, or the prompt's "Timebox N minutes".
- Old `ccusage session` is no longer called. `mock/ccusage_session.json` is unused (no deletes in this run).
- Haiku has no single fitted rate (0.05 to 0.21 USD/Mtok input by day); it is under 0.5 percent of any day.
