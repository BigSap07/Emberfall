# COCKPIT-V3 changes (branch agent/cockpit-v3 from agent/cockpit 86aacde)

New: web/ (scaffold from handoff\cockpit-v3), state_builder.py, serve.py, cockpit-web.bat, make-shortcut.ps1 + make_icon.py (web/ember.ico),
progress.json, plan block in costs.json (mock too). Edited: data.py (USAGE_TTL memo for ccusage, off for the TUI), tests, README, .gitignore.
The TUI (cockpit.ps1, cockpit.py) is untouched. 49 tests pass (25 old, 24 new). No Unity, no network, stdlib only.

Frontend bugs fixed (web/):
1. style.css had no .g-bar.run and .g-bar.late, so running and will-not-start bars were invisible. Added both.
2. style.css: a long chain row meta wrapped over the next row. Now one line with an ellipsis.
3. app.js: Enter in the set dialog hit the first button (Cancel) and saved nothing. Enter now saves.
4. app.js: a rejected POST (400) failed silently. The server message now shows as a hint.
5. serve.py sends Cache-Control: no-cache for web/ files, so an edited app.js or style.css is never stale in the app window.

Real from collectors: chain, run cost (cost, time, $/h, opus and idle share), spend today, week bars, block cost and burn, models, waiting
(USER ACTIONS of the last 2 days, queue, bundles), shipped (run logs, git log on main, Builds), health, tips, plan paid and multiple.
Real from files you edit: Road tracks (progress.json), plan prices, start month, renewal day (costs.json).
From your typing: block end, weekly % and reset, playtest date.
Estimated: block start and end when not set by hand (first local message + 5h), chain ETAs (median of the last 8 finished runs or the
prompt's timebox), API value (ccusage total x usd_to_eur, this PC only), the block bar is time elapsed, not a real limit.
Heuristic: Checks green = worktrees whose Game*\STATUS.md passed today. Playtest items are derived (files exist), not ticked by hand.
Not available: the real claude.ai limits (type them), whether the APK was installed on a device.

Check these values: progress.json (kits show 94 of 94 and 0 validator failures from DESIGN-BUILD-ORDER.md, the scaffold sample said 30 of 94;
Core, UI and Android pct are my reading), costs.json plan start 2026-08 and renew day 29 are the scaffold's numbers.
Live oddity: the R30 log has a stamp 21:35 written at 21:21, so V2 anchors R30 a day early (24h elapsed). Not touched.

V3b data fixes (60 tests, the 11 new ones fail on the old data.py):
1. Cutoff: a line like "Cutoff lifted by owner" now counts, the newest cutoff line wins (data.LIFT). A queue entry whose Builds folder exists and is newer than the entry is not queued (friends build b is gone). Live: no_new_after None, queue empty.
2. Opus split was right: local Opus equals ccusage Opus every day (10-23 43.85/43.85) and all of today's Opus is project pulse; Emberfall runs ran Sonnet today. Older runs show it (R22 86%, R25 70%). Spend tile now says "Emberfall $84, other projects $87". Idle was really 0 (only sleep lines counted): now gaps of 5+ min between transcript messages. R28 0%->37%, MERGE-1 0%->45%, R26 1% once its window is right (see 3).
3. A stamp past the file mtime is the mtime. A first stamp more than 3h past it still means the day before. R30: 24h15 -> 0h44. Side find: R30's "(R28 RUN DONE)" start condition then ended R28 at 21:14; a run now ends at its first done after its last start (R26 8h50 -> 1h20, back to V2's 80 min, $13.13).
4. Report only: DESIGN-BUILD-ORDER.md lines 107-112 (validator 0 failures, "Every one of the 94 heroes carries an authored kit"), 261 and 293 (94 of 94 authored), 1655-1659 ("31 of 94 heroes have drawn battle art", 63 pending). Read only run: validate_heroes.py "Summary: 0 fail 0 warn 0 info"; heroes.json has 94 heroes, 63 spriteStatus pending, 31 drawn. pct values unchanged.
