# /api/state schema (contract between backend and web/app.js)

Reference: state.sample.json. app.js only renders this JSON and never computes anything.
Times are ISO 8601 with offset (+02:00, or +01:00 after the clock change on 25 Oct). Tones: ok, warn, hot, bad, mute, dim, text.

Top level keys: now, mock, header, hints, kpis, chain, run_cost, waiting, shipped, road, plan, limits, week, playtest, health, tips.

- header: main (short hash), unity {text,tone}, waiting (int, badge), clock, date, mock (bool).
- kpis: 6 tiles {label,value,tone,sub}. Order: Chain, Spend today, 5h block, Weekly limit, Road to a real game, Plan.
- chain: axis_start, axis_end, quiet {start,end}, desc, badge {text,tone}, note, rows[{name,meta,bars[{start,end,kind: done|run|proj|late}]}].
- run_cost.rows: {name,cost,bar (0-100 relative to max),time,rate,opus,idle,hot,opus_hot,idle_hot}. Newest first, hot = over 2x median.
- waiting.items: {tone,title,sub}. From USER ACTIONS only (BOARD.md, REPORT.md) plus the latest OWNER CHAIN UPDATE and QUEUE.txt.
- shipped: counters[{n,label}], feed[{time,text}] built from run logs, git log on main and the Builds folder.
- road: pct (mean of the six tracks), milestones[{pct,label,tone}], tracks[{name,pct,sub,tone}].
- plan: default (x5|x20), options[], bars[], note. The page switches plan locally (localStorage cockpit.plan).
- limits: block {sub,pct,left,right}, weekly {sub,pct,pace,tone,note}, models[{name,pct,tone}].
- week.days: 7 x {day,label,value,today}. playtest: {desc,items[{title,done}]}. health: [{tone,label,value}]. tips: [string].

Writes: POST /api/set, header X-Cockpit: 1, JSON {key,value}. Allowed keys: block_reset ("3h12", "3:12" or "01:12"), weekly ("62" or "62 2d13h"), playtest_date (YYYY-MM-DD or empty). Stored in Tools/cockpit/state.json. Anything else returns 400.
