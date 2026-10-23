# Playtest report

From `mock_logs`: 3 players, 7 sessions, 40m 55s played in total. Written by `Tools/playtest/analyse.py`.

## Look at this

Rules: a stage lost 3 or more times in a row, a session under 3m 00s, a screen a player opened once and never again (Battle and WaveResult left out: the game opens them itself).

- **alice** lost stage `ch1_4` 3 times in a row (from session 2 at +2m 10s), cleared later.
- **bob** lost stage `ch1_3` 4 times in a row (from session 2 at +1m 40s), never cleared.
- **bob** session 1 lasted 1m 35s (last screen Campaign, no summary line so the game may have been killed).
- **carla** session 2 lasted 2m 30s (last screen MainMenu).
- `Summon` opened once and never again by 2 of 3 players (alice, bob).
- `Forge` opened once and never again by 1 of 3 players (alice).
- `Formation` opened once and never again by 1 of 3 players (alice).
- `Recital` opened once and never again by 1 of 3 players (carla).
- `Store` opened once and never again by 1 of 3 players (bob).
- `Talents` opened once and never again by 1 of 3 players (bob).

## Overview

| Player | Sessions | Total time | Furthest cleared | Stopped at (last screen) | Test purchases | Errors |
|---|---|---|---|---|---|---|
| alice | 3 | 20m 12s | ch1_4 | Battle | 2 | 1 |
| bob | 2 | 6m 33s | ch1_2 | Campaign | 2 | 0 |
| carla | 2 | 14m 10s | ch1_3 | MainMenu | 0 | 0 |

## alice

Build: 0.3.0 Android SM-X200

Furthest stage cleared: ch1_4 (furthest tried: ch1_5, not cleared). Total time: 20m 12s.

### Sessions

| # | Started (UTC) | Length | Screens | Stages cleared | Last screen before quitting | Source |
|---|---|---|---|---|---|---|
| 1 | 2026-10-24T18:02:11Z | 7m 27s | 11 | 2 | MainMenu | alice/emberfall_playlog.prev.txt |
| 2 | 2026-10-25T09:15:40Z | 7m 05s | 13 | 1 | MainMenu | alice/emberfall_playlog.txt |
| 3 | 2026-10-25T20:40:03Z | 5m 40s | 11 | 1 | Battle | alice/emberfall_playlog.txt |

### Where the time went

| Screen | Time | Share | Opened |
|---|---|---|---|
| Battle | 6m 49s | 34% | 8 |
| Heroes | 4m 39s | 23% | 2 |
| Formation | 2m 40s | 13% | 1 |
| Summon | 2m 00s | 10% | 1 |
| WaveResult | 1m 00s | 5% | 7 |
| Forge | 1m 00s | 5% | 1 |
| Store | 57s | 5% | 2 |
| Campaign | 34s | 3% | 8 |
| MainMenu | 27s | 2% | 5 |

### Stages that took several tries

| Stage | Fights | Losses | Most losses in a row | Cleared |
|---|---|---|---|---|
| ch1_4 | 4 | 3 | 3 | yes |

### Test purchases

- Session 1 +6m 55s: `starter_pack` bought
- Session 3 +4m 41s: `monthly_pack` refused (ComingSoon)

## bob

Build: 0.3.0 WindowsPlayer Desktop

Furthest stage cleared: ch1_2 (furthest tried: ch1_3, not cleared). Total time: 6m 33s.

### Sessions

| # | Started (UTC) | Length | Screens | Stages cleared | Last screen before quitting | Source |
|---|---|---|---|---|---|---|
| 1 | 2026-10-24T21:00:00Z | 1m 35s (no summary) | 6 | 1 | Campaign | bob.txt |
| 2 | 2026-10-24T21:05:30Z | 4m 58s | 14 | 1 | Campaign | bob.txt |

### Where the time went

| Screen | Time | Share | Opened |
|---|---|---|---|
| Battle | 3m 58s | 61% | 6 |
| Talents | 39s | 10% | 1 |
| Summon | 35s | 9% | 1 |
| Campaign | 30s | 8% | 8 |
| Store | 30s | 8% | 1 |
| MainMenu | 9s | 2% | 2 |
| WaveResult | 9s | 2% | 1 |

### Stages that took several tries

| Stage | Fights | Losses | Most losses in a row | Cleared |
|---|---|---|---|---|
| ch1_3 | 4 | 4 | 4 | no |

### Test purchases

- Session 2 +4m 30s: `weekly_pack` bought
- Session 2 +4m 35s: `weekly_pack` refused (AlreadyOwned)

## carla

Build: 0.3.0 Android Pixel 7

Furthest stage cleared: ch1_3. Total time: 14m 10s.

### Sessions

| # | Started (UTC) | Length | Screens | Stages cleared | Last screen before quitting | Source |
|---|---|---|---|---|---|---|
| 1 | (cut 1) | 11m 40s | 2 | 1 | Battle | carla.txt |
| 2 | 2026-10-26T07:30:00Z | 2m 30s | 5 | 1 | MainMenu | carla.txt |

### Where the time went

| Screen | Time | Share | Opened |
|---|---|---|---|
| Battle | 2m 18s | 58% | 2 |
| Recital | 59s | 25% | 1 |
| MainMenu | 35s | 15% | 2 |
| Campaign | 6s | 3% | 2 |

### Stages that took several tries

No stage was lost twice or more.

### Test purchases

None.

## Files not read

- `emberfall_playlog.smoke.txt`: smoke run file
- `notes.txt`: no play log lines
