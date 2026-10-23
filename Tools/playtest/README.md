# Playtest analyser

`analyse.py` reads the play logs your friends send back from the friends build and writes one
`PLAYTEST-REPORT.md`: who played how long, how far they got, where their time went, where they stopped,
which stages fought back, what they tried in the test store, and a short "Look at this" list.

Python 3 standard library only. Nothing is sent anywhere; the logs are only read, and the report is the only
file written.

## 1. Collect the logs

The friends build (built with `EMBERFALL_PLAYLOG`, see `Docs/FRIENDS-BUILD.md`) keeps
`emberfall_playlog.txt` on the device. Make one folder for the playtest and put each player's log in it:

- **Android:** the player taps Settings, SHARE PLAY LOG and sends the text to you (mail, chat). Save what
  arrives as a text file named after the player, for example `alice.txt`. The share holds the last 12000
  characters of the log, so ask for a share after every day of play, or ask for the file itself if they can
  get at it. Several shares from one player can go in that player's folder (see below); a session that shows
  up twice is counted once.
- **Windows:** SHARE PLAY LOG opens the folder
  (`%USERPROFILE%\AppData\LocalLow\Emberfall Studio\Emberfall`). Ask for `emberfall_playlog.txt` and, if it
  is there, `emberfall_playlog.prev.txt` (the older part, moved aside once the file passes 200 KB).

Two layouts work, and you can mix them:

```
playtest-2026-10-27/
  alice/                      <- a folder per player: every .txt / .log inside is alice
    emberfall_playlog.txt
    emberfall_playlog.prev.txt
    share-sunday.txt
  bob.txt                     <- or one file per player, named after them
  bob.prev.txt                <- ".prev" is dropped from the name, so this is bob too
```

`emberfall_playlog.smoke.txt` files and sessions from a smoke run are skipped. Any file with no play log
lines in it (notes, a reply mail) is skipped and listed at the end of the report.

## 2. Run it

```
python Tools/playtest/analyse.py D:\Emberfall\playtest-2026-10-27
```

It writes `PLAYTEST-REPORT.md` inside that folder (run it again after new logs arrive; it overwrites the
report and never reads it as a log). Options:

| Option | What it does |
|---|---|
| `--out PATH` | Write the report elsewhere: a `.md` path, or a folder. |
| `--loss-streak N` | Losses in a row that make a finding (default 3). |
| `--short-session S` | Sessions shorter than S seconds make a finding (default 180). |
| `--include-smoke` | Read smoke runs too. |

To see what a report looks like without real logs, open `EXAMPLE-REPORT.md`: it is the report for the made up
players in `mock_logs/`, made with
`python Tools/playtest/analyse.py Tools/playtest/mock_logs --out Tools/playtest/EXAMPLE-REPORT.md`.

## 3. Read the report

- **Look at this** comes first. Three rules:
  - a stage lost 3 or more times in a row by one player (counted across sessions, so losing twice, quitting
    and losing again next day is a run of 3);
  - a session under 3 minutes (a session with no summary line is marked: the game was likely killed rather
    than put away);
  - a screen a player opened once and never again, grouped by screen with the players who did it. Battle and
    WaveResult are left out because the game opens them itself.
- **Overview:** one row per player: sessions, total time, furthest stage cleared, the last screen of their
  last session (where they stopped), test purchases and errors.
- **Per player:** a session table (start time, length, the last screen before quitting), time per screen,
  stages lost twice or more, and every test purchase with its result and refusal reason.

## How things are counted

- **Session length** is the game's own `summary length` line (the last one, since it is cumulative). A session
  without one ends at its last line.
- **Time on a screen** runs from the line that opened it to the next screen line, or to the session end.
- **Fights and losses.** The game writes `attempt` each time a battle starts, `lose` when one is lost and `win`
  when the stage is cleared. A stage with several battles logs several attempts even when nothing is lost, so
  losses are the measure of difficulty, not attempts. A battle won mid stage writes nothing, so an attempt
  followed by another attempt with no loss in between counts as a won battle and ends a losing run. A fight left
  by quitting ends nothing.
- **Furthest stage** is the highest stage id with a `win`, ordered by its numbers (`ch1_10` after `ch1_9`). When
  the player tried a later stage without clearing it, that is shown too.
- **Shared text cut in the middle** of a session: the lines kept are read as a session named `(cut 1)`. Its
  length is right (the summary is cumulative) but its screens and stages are only the part that was kept.

## Tests

```
python Tools/playtest/test_analyse.py
```

They cover the line parser, the share text, the three rules, the folder layouts, the mock report and
`EXAMPLE-REPORT.md`, and a guard that fails if the line formats in
`Assets/Scripts/Core/Session/PlayLog.cs` change under the parser.
