# /api/state schema (contract between backend and web/app.js)

Reference: state.sample.json. app.js only renders this JSON and never computes anything.
Times are ISO 8601 with offset (+02:00, or +01:00 after the clock change on 25 Oct). Tones: ok, warn, hot, bad, mute, dim, text.

Top level keys: now, mock, header, hints, kpis, chain, run_cost, runs, replicate, waiting, shipped, road, plan, limits, week, playtest, health, tips.

- header: main (short hash), unity {text,tone}, waiting (int, badge), clock, date, mock (bool).
- kpis: 6 tiles {label,value,tone,sub}. Order: Chain, Spend today, 5h block, Weekly limit, Road to a real game, Plan.
- chain: axis_start, axis_end, quiet {start,end}, desc, badge {text,tone}, note, rows[{name,meta,bars[{start,end,kind: done|run|proj|late}]}].
- run_cost.rows: {name,cost,bar (0-100 relative to max),time,rate,opus,idle,hot,opus_hot,idle_hot}. Newest first, hot = over 2x median.
- runs: desc, badge ("N running"), found (int, runs found by discovery), rows[{name,state,state_tone,source,source_label,started,elapsed,last,flag,note}]. Newest first, runs from the last 3 days plus anything running or BLOCKED. state: running, BLOCKED, waiting, stale, DONE. stale = no final line and untouched for over 20 minutes, or the queue recorded an end the log never got. source: queue (tmp/runs.csv or QUEUE.txt), log (tmp/<RUN>_running_log.md by convention), found (a *running_log*.md in the discovery roots tmp, ui-gen, handoff; override with "run_roots" in costs.json). A run in more than one source shows once. flag is set when a log stamp was ahead of the clock and was clamped.
- replicate: desc, badge ("N generating" or "idle"), live (int), rows[{name,state,tone,images,cost,cap,rate,updated}], total {label,value,sub}. One row per ui-gen/ledger*.json and ui-gen/*/ledger*.json (shape {images,cost}, or the older entries list). state: generating (changed in the last 5 minutes, rate = images per minute since its run started), idle, unreadable (not counted, also a hint). cap is read from the generator script in ui-gen/tools when it states MAX_IMAGES / MAX_COST, else blank. total is an estimate: the price per image is assumed (costs.json replicate_price_per_image, default 0.04), check Replicate billing.
- waiting.items: {tone,title,sub}. From USER ACTIONS only (BOARD.md, REPORT.md) plus the latest OWNER CHAIN UPDATE and QUEUE.txt.
- shipped: counters[{n,label}], feed[{time,text}] built from run logs, git log on main and the Builds folder.
- road: pct (mean of the six tracks), milestones[{pct,label,tone}], tracks[{name,pct,sub,tone}].
- plan: default (x5|x20), options[], bars[], note. The page switches plan locally (localStorage cockpit.plan).
- limits: block {sub,pct,left,right}, weekly {sub,pct,pace,tone,note}, models[{name,pct,tone}].
- week.days: 7 x {day,label,value,today}. playtest: {desc,items[{title,done}]}. health: [{tone,label,value}]. tips: [string].

Writes: POST /api/set, header X-Cockpit: 1, JSON {key,value}. Allowed keys: block_reset ("3h12", "3:12" or "01:12"), weekly ("62" or "62 2d13h"), playtest_date (YYYY-MM-DD or empty). Stored in Tools/cockpit/state.json. Anything else returns 400.
