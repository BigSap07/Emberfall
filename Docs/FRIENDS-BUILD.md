# The friends build (R27)

A test build the owner hands to friends: the Windows zip and the Android APK, with the free test store and a local
play log. Everything here is behind two defines and changes no rule, balance, content, save or economy. The real save
hash, every fingerprint and `ContentDatabase.ContentVersion` are unchanged, and there is no save version change.

## The define set

| Define | What it does | Where |
| --- | --- | --- |
| `EMBERFALL_TESTSTORE` | A release build runs the test store: purchases are free and grant the offer's fixed contents, with a TEST STORE chip on the Store and Events screens. | `TestStoreSwitch` in `StoreSwitch.cs`, see `Docs/DESIGN-STORE.md` |
| `EMBERFALL_PLAYLOG` | The local play log, the Settings button that shares it, and the smoke mode. Off by default: no file, no hook that writes, no log line. | `PlayLogSwitch` in `PlayLogSwitch.cs`, `PlayLogService` in `Assets/Scripts/Game/PlayLog` |

Both are passed for one build only, through `BuildPlayerOptions.extraScriptingDefines`. `EmberfallBuild.RequestedDefines`
reads the command line argument `-emberfallDefines "A;B"` and `BuildDefines.Parse` (Core, engine-free) turns it into the
list. `ProjectSettings.asset` is never written for them, so a build leaves the tree clean. Together with
`EMBERFALL_STORE_LIVE` the test store still resolves to COMING SOON (`StoreModes.Resolve`).

`EMBERFALL_UNITY_IAP` also shows in the defines of a build (and in the play log header): the project's `csc.rsp` sets it
for every build, so it is expected and not something `-Defines` added.

## Building

All three scripts refuse a dirty tree, keep the `supabase.json` rules (offline by default, the file is moved out for
the build and back; `-Cloud` includes it untouched) and delete nothing new. Close the editor on the project first.
Unity rewrites `ProjectSettings/ProjectSettings.asset` on its way out (it drops one `m_SubKind:` line), which would trip
the clean-tree check after the build. `build-windows.ps1` and `build-android.ps1` therefore save its bytes before Unity
starts and put them back after it exits, before the check; every other file is still checked. `build-friends.ps1` does
the same between its two steps and lists anything else a step left changed. Unity's native build tools also write a
`.utmp` folder in the project root during an Android build; it is in `.gitignore`.

- `Tools/build-windows.ps1 -Cloud -Defines "EMBERFALL_TESTSTORE;EMBERFALL_PLAYLOG"` builds the Windows zip. Without
  `-Defines` it behaves as it always did. With `EMBERFALL_PLAYLOG` it also checks the play log is inside the player.
- `Tools/build-android.ps1 -Cloud -Defines "..."` builds the APK through `EmberfallBuild.BuildAndroidPlaytest`: batchmode
  with `-buildTarget Android` (no switch-target dialog), the release backend (IL2CPP, ARM64) applied over
  `ApplyPlayerSettings`, a release APK signed with the Unity debug keystore, an explicit output path, the commit stamp,
  and a `.sizes.txt` beside it. The editor log is kept beside the output. It checks the Android module (SDK, NDK,
  OpenJDK) first and prints the install steps when something is missing, and it puts `ProjectSettings.asset` back if
  the editor rewrote it.
- `Tools/build-friends.ps1` builds both from the play slot (the checkout that holds `supabase.json`, on main) with
  `-Cloud` and both defines into `D:\Emberfall\Builds\friends-<yyyy-mm-dd>` (`-OutputFolder` changes it). It never
  empties the folder: a build of the same commit already in it stops the run. A missing Android module builds Windows
  only and prints the install steps. `-WhatIf` prints the checks and the exact commands and launches nothing.
- `Tools/unity-editor.ps1` is the shared, read only helper that finds the editor (an explicit path,
  `EMBERFALL_UNITY_EXE`, then Unity Hub's install folders) and the Android module.

## The play log

`emberfall_playlog.txt` in `Application.persistentDataPath`, plain text, append only. It is local: the game never sends
it, and it does not depend on the play stats choice. The file header says so. It holds no save data and no account
details. Format (`PlayLogSession` in `PlayLog.cs` is the one place that builds the lines):

    # Emberfall play log. This file stays on this device. ...
    == session <id> started 2026-10-23T12:00:00Z ==
    build 0.3.0 (<commit>) | platform Android | device SM-X200 | defines EMBERFALL_PLAYLOG;EMBERFALL_TESTSTORE
    +2.0s screen MainMenu
    +6.0s stage 1-1 attempt
    +20.0s stage 1-1 lose wave 1 battle 0
    +40.0s stage 1-1 win
    +50.0s purchase offer <offer id> result bought
    +51.0s purchase offer <offer id> result refused reason ComingSoon
    +52.0s cloud Offline (no signal)
    +53.0s error Exception: ...
    +60.0s summary length 60s screens 2 stages_cleared 1 purchases 1 errors 1

- Times are seconds since the session started. Screens come from `ScreenRouter.Shown` and the panel host, stages from
  the `TelemetryHooks` campaign taps (so the log works with the stats off), purchases from `PurchaseFlow.Buy`
  refusals and `PurchaseFlow.Finished` / `Failed`, cloud transitions from `CloudCoordinator`, errors from
  `Application.logMessageReceived` (errors and exceptions only, 25 a session).
- The summary line is cumulative and is written once per session end (`PlayLogSession.Summary`): the first call at pause
  or quit writes it and closes the session, so a quit after a pause adds nothing and a late event (the cloud syncing as
  the app leaves) is dropped instead of landing after it. When the app comes back (`Resume`) a later summary is a new line
  only if events happened since the previous one.
- Caps (`PlayLogRules`): at most 2500 event lines a session; a file over 200 KB is moved to
  `emberfall_playlog.prev.txt` at the next start (an older prev is replaced, nothing else is removed) and a fresh file
  begins.
- **SHARE PLAY LOG** (Settings, only with the define). Android: the system share sheet with `ACTION_SEND`, `text/plain`,
  the text from `PlayLogRules.ShareText` (last session summary plus the tail, at most 12000 characters; no file
  provider needed). Windows: the folder opens in Explorer; send `emberfall_playlog.txt`.

## Smoke mode

`Emberfall.exe -emberfallSmoke` (only in a build with `EMBERFALL_PLAYLOG`) runs a short cloud check after boot, then
quits. It opens the Friends and Guild screens through the router (the feature lock is bypassed for the visit only; it
grants nothing) and creates no purchase.

**It runs as a throw-away player and never touches the real data.** Before any scene loads, `PlayLogService` sets
`SmokeStorage.Active` (flag on the command line and the define compiled in). While it is active, `SmokeStorage` redirects
every name the real data lives under: the save file to `scratch-smoke\emberfall_save.json` inside
`persistentDataPath` (`SaveService.FilePath`), the save's PlayerPrefs mirror to `emberfall.smoke.save.v1`, the cloud
session (user id and refresh token, `CloudLocalState`) to `emberfall.smoke.cloud.v1`, and the play log to
`emberfall_playlog.smoke.txt`. The play stats are not installed. So the run starts as a brand new anonymous player with
its own session; nothing in the real `emberfall_save.json`, its mirror, the real session or the real play log is read
or written, and the real session is not needed for the cloud sign in (a fresh anonymous sign in works with the public
key). What it does create in the cloud is what any first launch creates: one anonymous user and that user's starter
save. The smoke identity is kept between smoke runs (same scratch keys), so repeating the test reuses that one user.
`CheckR27PlayLog` proves the mapping with pure functions and a source scan of `SaveService` and `CloudLocalState`.
No real-save backup is needed any more. Every line goes to `Player.log` and to the smoke play log, so
`grep "\[smoke\]"` finds them:

    [smoke] start
    [smoke] cloud signed in anonymously (status Ready)
    [smoke] user id prefix <8 chars>
    [smoke] friends screen loaded (status 0 FRIENDS)
    [smoke] guild screen loaded (status NO GUILD)
    [smoke] done ok

Failure variants: `[smoke] cloud FAILED (status Offline: ...)`, `[smoke] friends screen TIMED OUT (status ...)`,
`[smoke] friends screen NOT opened`, and `[smoke] done with failures`. The process exit code is 0 only after
`[smoke] done ok`, otherwise 1. Waits are 60 s for the cloud and 30 s per screen. Windows writes `Player.log` under
`%USERPROFILE%\AppData\LocalLow\Emberfall Studio\Emberfall`. With the define on, the normal path also logs
`Emberfall: cloud status Ready (signed in).` once the cloud is ready.

## What proves it

`CheckR27PlayLog` (`Tools/DevHarness/R27PlayLogChecks.cs`) covers the formats, counts, caps, rotation, share text,
defines parse, switch off means no file, the wiring text and a `build-friends.ps1 -WhatIf` run. `Tools/build-check.ps1`
adds a `release-playlog` compile with `EMBERFALL_UNITY_IAP;EMBERFALL_TESTSTORE;EMBERFALL_PLAYLOG` beside
`release-plain` and `release-teststore`. A real Unity build and the Settings card layout were not captured in this run.
