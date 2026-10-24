# COCKPIT-RUN-SOURCES changes (branch agent/cockpit-run-sources from agent/cockpit-v3)

New: runs and replicate sections in /api/state (state_builder.py, web/SCHEMA.md, web/state.sample.json), Runs and Replicate spend cards (web/app.js), stale run state (data.py, cockpit.py), RunSources tests.
Edited: data.py (run discovery, runs.csv merge, ledger collector, stamp clamp with flag), README.
Replicate spend now comes from every ui-gen ledger, so the Replicate row of project cost is no longer 0 when a ledger holds {images, cost}.
The TUI shows a stale run in the dim colour and is otherwise unchanged.
