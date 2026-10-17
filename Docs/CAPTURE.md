# Capturing screens in Play Mode

A run captures its targets in batches in one editor session, never one launch per target. A single launch is only for retrying a target.

Why: entering Play right after a fresh editor launch is where the editor sometimes goes quiet (the log ends after the game's Awake and Unity's load of the backup scene, then nothing, and the folder `Temp\__Backupscenes` is left behind). Every launch is another roll of that dice. One session of 68 targets ran in 19 minutes without a hang. Three of eight single launches hung in another run.

This page is current for the tooling it names. The tools are `Tools/capture-batch.ps1`, `ScratchCaptures` and `PlayModeScreenshot` (with its decision class `CaptureWatchdog`) under `Assets/Editor`.

## How to run a batch

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/capture-batch.ps1 `
    -Targets "Screen:MainMenu;Screen:Summon;Screen:Battle" -LogDir D:\Emberfall\handoff\<RUN>\logs -Tag batch1
```

- `-Targets` is a `;` list. `-TargetsFile` takes a file with one target per line (lines starting with `#` are skipped). A target can carry a size, `Screen:Summon@2400x1080`, or a stage, `Screen:Battle=ch2_6` (a stage id from `campaign.json`).
- `-LogDir` and `-Tag` are required. The editor log is `capture-batch-<tag>-launch1.log` there, and the script's own record is `capture-batch-<tag>.txt` and `capture-batch-<tag>-result.txt`.
- `-CeilMin` (default 90) is the wall-clock ceiling for the whole session.
- Run it in the foreground. It starts ONE editor, raises its window every few seconds (a backgrounded Game view photographs stale frames), and ends when the editor exits. It never starts a second editor except for the one relaunch described below.
- It refuses to start, with exit code 2 and the command to run, when a Unity.exe already has the project open or when `Temp\__Backupscenes` exists. That folder is only ever left by a killed editor. Move it away (never delete it): `Move-Item -LiteralPath <path> -Destination D:\Emberfall\tmp\backupscenes-<tag>`.
- It hashes the real save before and after and puts the result on the SUMMARY line. A changed real save is a failure whatever else happened.

The capture runs on a scratch identity with the cloud held off (`ScratchCaptures` and `EditorCaptureGuard`). Nothing here reads `supabase.json`.

## Reading the RESULT lines

One line per target, in the order given:

| line | meaning |
|---|---|
| `RESULT <target> captured` | the log asked for the PNG and a fresh file of that name exists. `(after 1 watchdog recovery)` means the first play entry was lost and the second worked |
| `RESULT <target> FAILED: <why>` | the watchdog gave up on it (two lost play entries), or the editor hung at it and the relaunch could not finish it, or no PNG was requested, or the PNG is missing or old, or the PNG was written but the log has `[FAIL]` lines (the first one is quoted) |
| `RESULT <target> not reached` | the editor never started it (it exited, was stopped, or the ceiling hit first) |

Then `NOTE` lines (a launch the script stopped and why, `[FAIL]` lines from `ScratchCaptures`, whether `ProjectSettings.asset` is unchanged) and one summary:

```
SUMMARY wall=1140s targets=68 captured=66 FAILED=2 not_reached=0 play_entries=69 heartbeats=68
        watchdog_recoveries=1 outer_relaunches=0 launches=1 real_save=unchanged
```

`play_entries` counts the "play entry requested" lines (one per entry, a re-entry counts), `heartbeats` the "play heartbeat" lines. Entries minus heartbeats is the number of lost entries. Exit code 0 means every target was captured and the save is unchanged; 1 means something FAILED, was not reached, or the save changed.

To retry a FAILED target, run the same script with just that target. That is the one case where a single launch is right.

## The watchdog

A play entry is **lost** when the editor never reaches the first Pump tick that finds it playing. The tick prints `PlayModeScreenshot: play heartbeat, target ...`. That is the heartbeat.

The in-editor watchdog (`CaptureWatchdog` decides, `PlayModeScreenshot.WatchdogTick` acts, checked from `EditorApplication.update`):

1. No heartbeat 90 s after the entry was requested: log one `watchdog RECOVER` line with the state (`isPlaying`, `isPlayingOrWillChangePlaymode`, frame, whether `repaint call start` and `returned` were printed, whether `GameApp` exists), then `EditorApplication.ExitPlaymode()`. Unity removes `Temp\__Backupscenes` itself when Play ends normally. The tools never touch those files.
2. After the editor has been out of Play for 5 s: `watchdog re-entering`, and the same target is begun again as attempt 2.
3. The second entry gets 60 s (shorter, so a target that misses twice is settled inside three minutes: 90 s, a few seconds to leave Play, 5 s, 60 s). No heartbeat by then: `PlayModeScreenshot: FAILED <target> after a missed heartbeat on two play entries`. The target is dropped and `ScratchCaptures` carries on with the next one.
4. A heartbeat at any time means healthy; the watchdog does nothing more for that entry.

The outer watchdog, in `capture-batch.ps1`, is for an editor whose update loop is dead, so the in-editor one cannot run. If the log shows a play entry requested, no heartbeat after it, and no watchdog line after it for 150 s, the script kills the editor by the PID it started and relaunches ONCE with the targets not yet captured, the stuck one first, counted as its second attempt (`EMBERFALL_CAPTURE_FIRST_ATTEMPT=2`). Before the relaunch it MOVES `Temp\__Backupscenes` to `D:\Emberfall\tmp\backupscenes-r11-<tag>-<n>\` (never deletes it; otherwise the new editor stops on a "Recovering Scene Backups" dialog), puts back a `ProjectSettings.asset` that a killed editor left on the scratch identity (from the copy taken before launch 1), and restores the first-run EditorPref. If the relaunched editor hangs again, it is killed too, the stuck target is FAILED, the rest are not reached, and the folder is moved aside again so the next batch can start. Two other nets stay on: no capture progress for 10 minutes, and the ceiling.

### An editor that quits by itself

Once, a single launch quit cleanly (exit code 0) about 13 s after `entered play mode`, with no heartbeat and none of our own exit lines. If `capture-batch.ps1` sees the editor exit by itself, with any code, before `ScratchCaptures` logged `identity restored`, it prints `editor exited by itself with N targets left: relaunching once`, runs the same cleanup as after a kill, waits 10 s, and relaunches once with the targets not yet captured (the unfinished one first, as its second attempt). This shares the one relaunch with the outer watchdog. While a capture run is active, `PlayModeScreenshot` also logs `PlayModeScreenshot: editor QUIT (wantsToQuit)` and `(quitting)` with the managed stack, `isPlaying`, the `Active` and `Waiting` flags and the current target; it never blocks the quit. A stack that names `ScratchCaptures.Tick` or another `EditorApplication.Exit` caller means our code asked to quit. A stack of engine frames only means something outside asked (a window close, a signal, the licensing client). If neither line appears, the editor went down without an orderly quit.

### Switching it off

Set `EMBERFALL_CAPTURE_WATCHDOG=0` in the environment of the shell that launches the batch. `PlayModeScreenshot` reads it when a run begins and logs `watchdog mode ON` or `watchdog mode OFF`. Off switches off both watchdogs (a lost play entry then waits for the 10 minute stall net). Unset, or any other value, is ON. Never combine `EMBERFALL_CAPTURE_WATCHDOG=0` with `EMBERFALL_CAPTURE_FORCE_MISS`: the target then hangs on purpose with no recovery.

### Showing the recovery on demand

Set `EMBERFALL_CAPTURE_FORCE_MISS=<target name>` (for example `Screen:Summon`). That target's first play entry sends no heartbeat and does no work (`FORCE-MISS hook` lines say so in the log). The watchdog must then print `watchdog RECOVER`, exit Play, print `watchdog re-entering`, and the second entry runs normally. Expected log, in order, for a three-target batch with the hook on the second:

```
PlayModeScreenshot: FORCE-MISS hook active for Screen:Summon (EMBERFALL_CAPTURE_FORCE_MISS): ...
PlayModeScreenshot: play entry requested, target Screen:Summon, attempt 1, watchdog on
PlayModeScreenshot: FORCE-MISS hook, Pump returns without a heartbeat for this play entry
PlayModeScreenshot: watchdog RECOVER Screen:Summon attempt 1: no play heartbeat 90.x s after the entry request. State: ...
PlayModeScreenshot: watchdog re-entering Screen:Summon as attempt 2 after 5.x s in edit mode
PlayModeScreenshot: play entry requested, target Screen:Summon, attempt 2, watchdog on
PlayModeScreenshot: play heartbeat, target Screen:Summon, attempt 2, ...
```

and `RESULT Screen:Summon captured (after 1 watchdog recovery)`. The hook never fires on attempt 2, so it cannot make a target FAILED.

### Skipping the domain reload (an A/B, off by default, EXPERIMENTAL)

Measured once: play entries were about 11 times faster (median 0.6 s against 7.0 s), but the whole batch was not faster (263 s against 229 s and 340 s for the reloading batches), and it broke captures: three `MissingReferenceException` lines from cached textures and a sprite that Unity had destroyed, and a summon screen drawn as flat squares. Do not use it for proof captures. It is kept as an opt-in so the measurement can be repeated.

Set `EMBERFALL_CAPTURE_SKIP_RELOAD=1` in the environment of the launching shell and `ScratchCaptures.Run` enters Play without a domain reload for that session only (scene reload stays on). It remembers the project's Enter Play Mode options in `SessionState`, sets `DisableDomainReload`, and logs it; when the queue finishes it puts the exact previous values back and logs `Enter Play Mode options restored`, then checks that `ProjectSettings/EditorSettings.asset` is byte for byte what it was when the session began (the API's "enabled" flag does not match the file's `m_EnterPlayModeOptionsEnabled` in Unity 6, so the check compares bytes, not a parsed value). `capture-batch.ps1` also keeps its own copy of that file and restores it if it changed, after the run and after any kill or crash cleanup, and prints a `NOTE EditorSettings.asset` line either way. Unset, or any other value, changes nothing.

If `ScratchCaptures.Run` is run directly (not through `capture-batch.ps1`) with `EMBERFALL_CAPTURE_SKIP_RELOAD=1` and the editor is killed, nothing restores the setting: run `git checkout -- ProjectSettings/EditorSettings.asset`. And because `capture-batch.ps1` restores `EditorSettings.asset` whenever it changed during a batch, do not edit project settings while a batch runs: the edit would be undone.

The catch: with no reload, static state in the game and in the capture tools is not reset between play entries. The ones worth knowing: `GameApp.Instance`, `GameApp.BootStage`, `BootCompleted` and `BootFailed`; the `Instance` statics of `TelemetryService`, `OnboardingCoach` and `TelemetryConsentPrompt`; `AudioPrefs` (its cached values and its `Changed` event, whose handlers stay subscribed); `AudioCueLog` (its byte cap and path); `SupabaseConfig` (the cached config and `ClientsConstructed`); `SummonAudio.Hook`; the static overrides of the guild services; `FirstRun`'s squad; the capture statics of `WaveResultScreen`; the cached fonts, sprites and textures in `Emberforge`, `UITheme`, `TextureLab`, `BattleAssets` and `VfxDirector`; and in `PlayModeScreenshot` the per-domain heartbeat and repaint flags and the audio summon counters. None of this was fixed; an A/B that shows odd captures should look here first.

## Reading a hung log

Look at the last lines of the editor log and match them to a row. The lines are all printed by `PlayModeScreenshot` unless noted.

| what the log shows | meaning |
|---|---|
| ends at `play entry requested`, nothing from a new domain (`loaded, build marker`), no `entered play mode` | the editor was stuck leaving edit mode or reloading the domain. If the update loop is dead nothing in the editor can act: the outer watchdog kills it at 150 s |
| `entered play mode`, then `heartbeat, run active ... playing False` and no `play heartbeat` | stuck entering Play (`isPlaying` never became true). The in-editor watchdog recovers it at 90 s |
| no `heartbeat, run active` line at all after the entry | the editor update loop is dead |
| `play heartbeat`, then `repaint call start` with no `repaint call returned` | `ForceGameViewRepaint` blocked (the Game view was mid-recreation). Fix is to call it once or on a delay, not every tick |
| `play heartbeat`, `repaint call returned`, but no `[ok]` line | Pump is returning early (check the run's `ActiveKey` and target) |
| `play stats loop is ticking` (from `TelemetryService`) absent while Pump is fine | the player loop never ran a frame |
| `play stats loop is ticking` present, no `play heartbeat` | editor loop dead, player loop alive |
| `watchdog RECOVER`, then nothing for 120 s | `ExitPlaymode` did not get the editor out of Play; the outer watchdog kills it |
| `watchdog RECOVER`, `re-entering`, a second `play entry requested`, then `FAILED <target> after a missed heartbeat on two play entries` | the target is genuinely stuck, not a one-off. Retry it alone, and keep the log |
| `FORCE-MISS hook` lines | the test hook was on. A lost entry is expected and not a finding |

The `watchdog RECOVER` line carries the state, so the first four rows can be read from it directly.

## Not in scope

The nightly (`Tools/nightly.ps1`) already runs every target in one session. It gets the in-editor watchdog for free because that lives in the editor code; its own stall and ceiling nets are unchanged.
