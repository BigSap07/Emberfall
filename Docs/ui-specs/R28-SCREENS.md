# R28 screens: leaderboards, guild dungeon ladder, notifications, rewarded ads

Written 2026-10-23 for the client run that builds these four screens in uGUI. Everything below the screen is built:
for each screen a presenter in `Assets/Scripts/Core/Screens` that says exactly what is drawn, a controller in
`Assets/Scripts/Game` that makes the calls, and harness checks for every state (`Tools/DevHarness/R28ScreenChecks.cs`).
**Built so far:** screen 1 (`LeaderboardsScreen`) and the REMINDERS card of screen 3 (`RemindersCard` on `SettingsScreen`), held
to this spec by `Tools/DevHarness/R28UiChecks.cs`. Screens 2 and 4 are not built in uGUI yet.
The UI run writes the screen and binds it. It decides nothing: **no game logic goes in UI**, and here that includes
whether a button is drawn, whether it can be tapped, every sentence and every countdown.

## How every screen here is built

**The loop.** The screen holds its controller, subscribes to its `Changed` event, and on every `Changed` (and once a
second while a countdown is on screen) rebuilds from the controller's `View`. A tap calls one controller method and
nothing else. The screen never calls a backend, never reads the save to decide a state, and never writes copy of its
own: every string it draws is a field of the view.

**Shared models** (`ScreenModels.cs`):

| Model | Fields | How it is drawn |
|---|---|---|
| `ScreenPhase` | `Offline`, `Loading`, `Error`, `Empty`, `Ready`, `Unavailable` | Picks the layout. `Offline`, `Error`, `Empty` and `Unavailable` replace the body with the status block below. |
| `ButtonModel` | `Visible`, `Enabled`, `Label`, `Reason` | `Visible` false: not built at all. `Enabled` false: drawn with `UITheme.ButtonDisabled`, not tappable, and `Reason` printed under it in `UITheme.TextMuted` at `UITheme.FontTiny`. |
| `ToggleModel` | `Label`, `Description`, `On`, `Enabled`, `Reason`, `Note` | A row: label and description on the left, the switch on the right. `Reason` replaces `Description` while disabled. `Note` is a third line in `UITheme.TextDim`. |
| `ScreenText` | `Countdown`, `Group`, `LocalDayAndClock` | Formatting the presenters already applied. The screen does not call these. |

**The status block** (every `Offline`, `Error`, `Empty`, `Unavailable` body): centred in the body area, `View.Status`
in `UITheme.TextMuted` at `UITheme.FontBody`, and under it the screen's `Retry` (or `FindGuild`) button as a
`UIBuilder.SecondaryButton` when visible. `Loading` with nothing cached is a spinner in the same place and nothing else.

**A banner** (`View.Banner`, when not empty): a one-line strip at the top of the body, `UITheme.PanelSunken` at
`HubStyle.PlateAlpha`, a `HubStyle.BorderWarm` hairline at `HubStyle.BorderAlpha`, text in `UITheme.Warning` at
`UITheme.FontTiny`. It sits over rows that are still shown (a failed refresh, offline with a cache, a refused attempt).

**Look and feel.** `UITheme` for every colour, size and spacing; `HubStyle` for plates, borders and radii where the
screen opens from the Hub. The header is `ScreenChrome.ForgedHeader` with the view's `Title`; buttons are
`UIBuilder.GoldButton` (the one primary action), `UIBuilder.SecondaryButton` (everything else); lists are
`UIBuilder.ScrollList`; tabs and chips are `UIBuilder.Chip`. **With the UI kit on (the default) the built screens draw the same
roles in the kit's components instead:** `EmberforgeUI.Button` with `ForgeButtonKind.Primary` (the ember button, where the old
text says gold) and `ForgeButtonKind.Secondary`, `EmberforgeUI.Tab` for tabs, `EmberforgeUI.PlainPanelOn` for rows and
`EmberforgeUI.PanelOn` for cards. The colours, sizes, fields and taps below are unchanged. Panels use `UITheme.Panel` and `UITheme.CornerRadius`,
rows `UITheme.PanelRaised` and `UITheme.CornerRadiusSmall`. Values a player reads as a number that matters
(`HubStyle.ValueGold`) are `UITheme.Gold`; structure is `UITheme.ChromeBrass`, never Gold. Every tap target is at least
`HubStyle.TouchMin` on both axes (so the tab chips are 108 tall, not the Store's 76). No new art, no new colour, no em or en dash in any string (the checks scan for them).

**Captures.** Each screen is captured with `PlayModeScreenshot` at 1920x1080 and 2400x1080 in every state its table
lists, before the work counts as done. Screens 1 and 3 have their targets under `Emberfall/Capture/R28 screens/`, listed in
`Tools/capture-r28a-targets.txt`. The in-memory backends make every state reachable offline:
`LeaderboardBackends.Override`, `GuildDungeonBackends.Override`, `NotificationBackends.Override`, `AdBackends.Override`
(the simulated ad backend's `FinishNext` and `FailNext` rehearse the two no-reward endings).

## 1. Leaderboards

**Presenter** `LeaderboardsPresenter` (view `LeaderboardsView`). **Controller** `LeaderboardsController` in
`Assets/Scripts/Game/Cloud/LeaderboardService.cs`, built with `LeaderboardBackends.For(app)`, the app as the coroutine
runner and the session. **Server** `0023_leaderboards.sql`: `leaderboard_submit()` and `leaderboard_top(board, limit)`.

**Doors.** A LEADERBOARDS button on `ProfileScreen`, and a BOARDS secondary button on `RecitalScreen` and
`ProvingScreen` that opens this screen with that board selected (`SelectTab` before `Open`). Nothing on the Store,
Events or pass screens links here, and nothing here links to them: boards pay nothing and a rank is never bought.
The new router screen is an open screen (no feature gates it): add `"Leaderboards"` to `FeatureEntryPoints.OpenScreens`
in the same commit, or `CheckFeatureUnlockGating` fails. Any lock is the board's `LockLine`, asked through the feature's
own door (`FeatureEntryPoints.Allows`), never through `FeatureGate.IsUnlocked`, which `OnboardingChecks` forbids
outside the gate.

### Layout (1920x1080)

```
+--------------------------------------------------------------------------------------+
| [<]  LEADERBOARDS                                                       [ REFRESH ]  |  header (ForgedHeader), Refresh at right
+--------------------------------------------------------------------------------------+
| ( DEEP RECITAL ) ( THIS WEEK ) ( THE PROVING ) ( CAMPAIGN )        Resets in 3d 16h  |  tab chips; ResetLine right-aligned
| Highest floor reached in the Deep Recital                          Updated just now  |  Caption; UpdatedLine
| [ banner, when Banner is not empty ]                                                 |
| UNLOCKS AT 2-8                                                                       |  LockLine, when not empty
|  RANK   NAME                                                       FLOOR             |  ValueHeader
|  #1     Ana ....................................................   412               |
|  #2     Me  (highlighted row) ......................................   398           |  Rows (ScrollList)
|  ...                                                                                 |
|  ------------------------------------------------------------------------------------|
|  #312   Me  (pinned) ......................................................  12      |  PinnedSelf, outside the scroll
|  You are #312                    Your new best, floor 41, is on the board.           |  MyRankLine left, SubmitLine right
+--------------------------------------------------------------------------------------+
```

Rows are 72 tall, `UITheme.PanelRaised`. The caller's row (`IsMe`) is `UITheme.PanelRaised` with a `UITheme.Gold`
frame of `HubStyle.BorderWeightHeavy` and its name in `UITheme.Gold`. Ranks 1 to 3 are not decorated differently:
boards pay nothing, and a medal would read as a prize.

### Bindings

| Element | Field | Style |
|---|---|---|
| Header title | `Title` | `ForgedHeader` |
| REFRESH | `Refresh` (`ButtonModel`) | `SecondaryButton`, `UITheme.FontSmall`; label is REFRESH or REFRESHING; in the header at the right, 300 by `HubStyle.TouchMin`; a disabled one prints its `Reason` under it |
| Tab chips | `Tabs[i].Label`, `Tabs[i].Selected` | `UIBuilder.Chip`; selected chip `UITheme.Gold` text on `UITheme.PanelRaised`, others `UITheme.TextMuted` (kit: `EmberforgeUI.Tab` and `SetTabSelected`, 280 by `HubStyle.TouchMin`) |
| Reset line | `ResetLine` (`ResetInSeconds` for the tick) | `UITheme.TextMuted`, `UITheme.FontTiny`, right-aligned |
| Caption | `Caption` | `UITheme.TextPrimary`, `UITheme.FontSmall` |
| Updated line | `UpdatedLine` | `UITheme.TextDim`, `UITheme.FontTiny` |
| Banner | `Banner` | the banner strip |
| Lock line | `LockLine` | `UITheme.ChromeBrass`, `UITheme.FontTiny`, all caps as given |
| Value column header | `ValueHeader` | `UITheme.ChromeBrass`, `UITheme.FontTiny`, right-aligned over the value column. The diagram's RANK and NAME headers are not drawn: the view carries no field for them and the screen writes no copy of its own |
| Row rank | `Rows[i].RankText` | `UITheme.TextMuted`, `UITheme.FontSmall` |
| Row name | `Rows[i].Name` | `UITheme.TextPrimary` (`UITheme.Gold` when `IsMe`), `UITheme.FontSmall` |
| Row value | `Rows[i].ValueText` | `HubStyle.ValueGold`, `HubStyle.FontRankValue`, right-aligned |
| Pinned row | `PinnedSelf` (null: not drawn) | as an `IsMe` row, under a `UIBuilder.Divider` |
| My rank | `MyRankLine` | `UITheme.TextPrimary`, `UITheme.FontSmall` |
| Submit line | `SubmitLine` | `UITheme.TextMuted`, `UITheme.FontTiny` |
| Spinner | `ShowSpinner` | over the list, the list stays |
| Status block | `Phase`, `Status`, `Retry` | see "The status block" |

### Taps

| Tap | Call | What happens |
|---|---|---|
| Screen opens | `Open()` | Submits if a submission is due (at most once a minute, `LeaderboardsPresenter.ShouldSubmit`), then loads the selected board. |
| REFRESH, TRY AGAIN | `Refresh()` | The same as opening. |
| A tab | `SelectTab(board)` | Shows that board at once if it was loaded on this visit, else loads it. |
| A row | none | Rows are not tappable: no profile is opened from a board (the server sends no user id). |
| Back | router | Leaves. Nothing to save. |

### States

| Phase | When | Drawn |
|---|---|---|
| `Offline` | no cloud in the build | status, no TRY AGAIN, REFRESH hidden, tabs still drawn |
| `Offline` | signed out or no signal | status and TRY AGAIN |
| `Loading` | first load of a board | spinner, REFRESH disabled |
| `Ready` | rows | the layout above |
| `Ready` + spinner | refreshing over rows | REFRESHING disabled |
| `Ready` + banner | refresh failed, or offline, with rows | rows under the banner |
| `Empty` | no rows | "Nobody is on this board yet." (weekly: "Nobody has climbed this week yet.") |
| `Error` | first load failed | the failure's sentence and TRY AGAIN; "The leaderboards are not open yet." has no TRY AGAIN |

**Owner decision 1** (`Docs/DESIGN-LEADERBOARDS.md`, decided 2026-10-23): the THIS WEEK tab is the highest-floor board (`RecitalWeek`). The floors-climbed board was dropped from the server, so the client has four boards and no switch. The screen reads the tab's board from `Tabs[1].Board`.

## 2. Guild dungeon: the size-scaled ladder

**Presenter** `GuildDungeonLadderPresenter` (view `GuildDungeonLadderView`). **Controller**
`GuildDungeonLadderController` in `Assets/Scripts/Game/Cloud/GuildDungeonSizeService.cs`, built with
`GuildDungeonBackends.For(app)`, `app.Cloud`, the app and the session. **Server** `0022_guild_dungeon_size.sql`: the
widened `guild_dungeon_status` (read by `GuildDungeonStatus.Read`), `guild_dungeon_floors_for`,
`guild_dungeon_floor_max`, `guild_dungeon_cycle_attempt_cap`, and the `guild_dungeon_cycles` row
(`GuildDungeonSizeService`). This section supersedes the layout half of `Docs/ui-specs/GUILD-DUNGEON-SIZE.md`; its
reading rules are what the presenter implements, so every number below is already the one that spec asks for.

**Where.** The floor list of `GuildDungeonScreen` is rebuilt from this view. The floor's line-up screen
(`GuildFloorScreen`), the boards (`GuildDungeonBoardsScreen`) and the result (`GuildDungeonResultScreen`) are not
replaced; ATTEMPT and BOARDS open them as today.

### Layout (1920x1080)

```
+--------------------------------------------------------------------------------------+
| [<]  GUILD DUNGEON  -  Ashen Oath                                       [ REFRESH ]  |
+--------------------------------------------------------------------------------------+
| 10 FLOORS THIS CYCLE - set at 31 members when the cycle opened.   ATTEMPTS 9 / 15    |  LadderLine | AttemptsLine
| A guild of 15 gets 7 floors from next cycle.                       Cycle ends in 2d 4h|  NextCycleLine | CycleLine
| o o o o O | o o o o O                                                                |  pips: PipCount, PipsCleared
| [ banner ]                                                                           |
| +----------------------------------------------------------------------------------+ |
| | FLOOR 1   CLEARED  First cleared by Ana     3 of 3 left   Reward not decided yet  [ATTEMPT] |
| | ...                                                                              | |  Rows (ScrollList)
| | FLOOR 5   CHAMPION ...                                                           | |
| | ---- SECOND BLOCK - the climb starts again ----                                  | |  DividerBefore on floor 6
| | FLOOR 6   OPEN ...                                                     [ATTEMPT] | |
| |   floor 7 opens when somebody in your guild clears floor 6   (Reason, muted)     | |
| +----------------------------------------------------------------------------------+ |
|                                                                        [ BOARDS ]    |
+--------------------------------------------------------------------------------------+
```

**Ten rows fit** because the list is a `UIBuilder.ScrollList` between the header block and the BOARDS row; it never
covers the header or the bottom bar. Rows are 96 tall. **The pip strip** is `PipCount` pips, `PipsCleared` filled in
`UITheme.Waymark`, a gap of `UITheme.Space8` after every fifth (between blocks); at ten, shrink the spacing, never the
pips. **The divider** is a `UIBuilder.Divider` in `UITheme.ChromeBrass` with `DividerCaption` centred on it in
`UITheme.ChromeBrass` at `UITheme.FontTiny`, so floor 6 is not read as harder than floor 5. It is not.

### Bindings

| Element | Field | Style |
|---|---|---|
| Header | `Title`, `GuildName` as the subtitle | `ForgedHeader` |
| REFRESH | `Refresh` | `SecondaryButton` |
| Ladder line | `LadderLine` | `UITheme.TextPrimary`, `UITheme.FontSmall` |
| Next-cycle line | `NextCycleLine` (empty: not drawn) | `UITheme.TextMuted`, `UITheme.FontTiny` |
| Attempts counter | `AttemptsLine` (`AttemptsLeftThisCycle`, `CycleAttemptCap` for a bar) | `HubStyle.ValueGold`, `UITheme.FontSmall` |
| Cycle countdown | `CycleLine` (`CycleEndsInSeconds` for the tick) | `UITheme.TextMuted`, `UITheme.FontTiny` |
| Pips | `PipCount`, `PipsCleared` | as above |
| Row title | `Rows[i].Title` | `UITheme.TextPrimary`, `UITheme.FontBody` |
| Champion treatment | `Rows[i].Champion` | the Champion treatment the floor rows have today |
| Divider | `Rows[i].DividerBefore`, `Rows[i].DividerCaption` | as above, above the row |
| State tag | `Rows[i].StateText` (`State`) | `UIBuilder.Chip`: CLEARED `UITheme.Success`, OPEN `UITheme.Gold`, LOCKED `UITheme.TextDim` |
| First clear | `Rows[i].FirstClearText` | `UITheme.TextMuted`, `UITheme.FontTiny` |
| Mine | `Rows[i].ClearedByMe` | a small tick in `UITheme.Success` beside the title |
| Attempts left | `Rows[i].AttemptsText` | `UITheme.TextPrimary`, `UITheme.FontSmall` |
| Reward | `Rows[i].RewardText`, `Rows[i].RewardMuted` | muted: `UITheme.TextMuted`, no icon, never a 0 |
| ATTEMPT | `Rows[i].Attempt` | `GoldButton`; disabled shows `Reason` verbatim under it |
| BOARDS | `Boards` | `SecondaryButton` |
| FIND A GUILD | `FindGuild` (no-guild state only) | `GoldButton` in the status block |
| Banner, status, spinner | `Banner`, `Phase`, `Status`, `Retry`, `ShowSpinner` | shared |

### Taps

| Tap | Call | What happens |
|---|---|---|
| Screen opens, REFRESH, TRY AGAIN | `Refresh()` | Status, then this cycle's attempts, then the server's ladder for the live member count. |
| ATTEMPT | router | Opens `GuildFloorScreen` for `Rows[i].Floor`. That screen fights the line and files it through `GuildDungeonFlow.Attempt`, which stores the attempt BEFORE the fight is shown, so the ladder screen never calls `RecordAttempt` (it would file one fight twice); coming back, `OnShow` reloads. A refusal in that flow is `GuildFloorScreen`'s notice, so the RECORDING label and the refused-attempt banner exist in the presenter and the controller but nothing in the shipped flow drives them yet. |
| BOARDS | router | Opens `GuildDungeonBoardsScreen`. |
| GUILD TECH | router | Opens `GuildTechScreen`. Not in the view: the old screen's door to the tree this dungeon pays into, kept, drawn only on a loaded ladder. |
| FIND A GUILD | router | Opens `GuildScreen` at its join view. |

There is no button that buys, refills or plays an ad for an attempt, and the checks fail the build if a label here
says so. Attempts cannot be bought.

### States

| Phase | When | Drawn |
|---|---|---|
| `Offline` | no cloud / signed out | status; TRY AGAIN when signed out |
| `Unavailable` | not in a guild | "You are not in a guild. Join one to climb its dungeon together." and FIND A GUILD |
| `Loading` | first load | spinner |
| `Error` | first load failed | sentence and TRY AGAIN |
| `Ready` | loaded | the layout above |
| `Ready` + banner | refresh failed, offline, refused attempt, cycle over | ladder under the banner; offline and cycle over disable every ATTEMPT |

The five states `GUILD-DUNGEON-SIZE.md` asks to capture are all reachable through `GuildDungeonBackends.Override`
(`Tools/capture-r28b-targets.txt`, `Screen:GuildLadder=<state>`): a guild of 4 on cycle open (`g4-open`), a guild of 30
at the cycle cap (`g30-cap`), a guild of 12 whose cycle opened at 9 (`g12-grew`), a member who changed guild after 15
attempts (`changed-guild`), and floor 8 just cleared (`after-floor8`). The last is a capture of the ladder: its rows read
"Reward not decided yet", muted. `GuildDungeonResultScreen` is not replaced and draws no reward line at all, so there is
no result screen to show that sentence on.

**As built.** The buttons are the kit's: ATTEMPT and FIND A GUILD are `EmberforgeUI.Button` with `ForgeButtonKind.Primary`
(the spec's `GoldButton`), the others `ForgeButtonKind.Secondary`; text colours are `Emberforge.TextBody` and
`Emberforge.TextQuiet` on the kit's plates, gold values `HubStyle.ValueGold`. REFRESH sits in the header's right end.
A row is 96 tall, but the ATTEMPT button is `HubStyle.TouchMin` (108) tall and overhangs its row by 6 a side
(`LadderLayout`), so the list spacing is 14. The Reason of a disabled ATTEMPT is printed verbatim in the row's lower
line, left of the button, since there is no room under a button that fills the row. The Champion floors (5 and 10) keep a
gold framed plate, locked floors a recess, and the row has no enemy portraits: the floor view is where a floor is
inspected. The spinner is eight dots in a ring, the only motion on the screen.

## 3. Notification settings

**Presenter** `NotificationSettingsPresenter.Build` (view `NotificationSettingsView`), a pure function of the session,
the switches, the permission and the clock. **Controller** `NotificationSettingsController` in
`Assets/Scripts/Game/Notifications/NotificationService.cs`. **No server**: reminders are local. The switches live in
`PlayerPrefs` (`NotificationPrefsStore`), never in the save. `GameApp` re-plans on going to the background and clears on
return (`NotificationScheduler`), which does nothing until a backend reports the permission granted.

**Where.** A REMINDERS card on `SettingsScreen`, under the audio card (`AudioSettingsCard`), in the same panel style.

### Layout

```
+---------------------------------------------- REMINDERS -----------------------------+
| Emberfall needs your permission to send reminders.            [ ALLOW REMINDERS ]    |  PermissionLine | PermissionButton
|--------------------------------------------------------------------------------------|
| Reminders                                                                   [ ON ]   |  Master
|   Let Emberfall remind you when something is ready.                                  |
| Idle chest full                                                             [ ON ]   |  IdleChest
|   When your heroes have filled the idle chest.                                       |
| New daily orders                                                            [ ON ]   |  Orders
|   When a fresh set of daily orders is posted.                                        |
|   Daily orders are not open yet.                                                     |  Note
| Event ending                                                                [ ON ]   |  Events
|--------------------------------------------------------------------------------------|
| COMING UP                                                                            |
|   Your chest is full                                              today 21:00        |  Upcoming[i].Title | WhenText
|   No reminders planned right now.   (UpcomingStatus, when the list is empty)         |
|--------------------------------------------------------------------------------------|
| Quiet hours 22:00 to 09:00, your time. Nothing arrives in them.                      |  RuleLines
| At most 2 a day, at least 4 hours apart.                                             |
| Reminders are only ever about rewards you have earned.                               |
+--------------------------------------------------------------------------------------+
```

### Bindings

| Element | Field | Style |
|---|---|---|
| Permission line | `PermissionLine` | `UITheme.TextPrimary`, `UITheme.FontSmall` |
| Permission button | `PermissionButton` | `GoldButton` (ALLOW REMINDERS, ASKING) or `SecondaryButton` (OPEN PHONE SETTINGS) |
| Four switches | `Master`, `IdleChest`, `Orders`, `Events` (`ToggleModel`) | toggle rows; topics indented `UITheme.Space24` under Master |
| COMING UP header | constant | `UITheme.ChromeBrass`, `UITheme.FontTiny` |
| Upcoming rows | `Upcoming[i].Title`, `Upcoming[i].WhenText` (`Body` as a second line) | title `UITheme.TextPrimary`, time `UITheme.TextMuted` right-aligned |
| Upcoming status | `UpcomingStatus` | `UITheme.TextMuted`, `UITheme.FontTiny` |
| Rule lines | `RuleLines` | `UITheme.TextDim`, `UITheme.FontTiny` |
| Whole card | `Phase == Unavailable`: `Status` replaces the permission line, every switch disabled | A switch's `Reason` is printed under its label in place of its description, except a reason that repeats the `Status` already shown, which would read five times down the card: that switch shows its description instead (`RemindersCardLayout.SubLine`) |
| Switch | `On`, `Enabled` of each `ToggleModel` | a button reading ON (ember) or OFF (teal), 220 by `HubStyle.TouchMin`, disabled when `Enabled` is false. ALLOW REMINDERS and ASKING are the ember button, OPEN PHONE SETTINGS the teal one |
| Card heading | constant REMINDERS | the card's name in the spec; `NotificationSettingsView.Title` (NOTIFICATIONS) is not drawn |

### Taps

| Tap | Call | What happens |
|---|---|---|
| A switch | `Toggle(null)` for Reminders, `Toggle(NotificationKind.IdleChestFull / OrdersReset / EventEndingWeekly)` for a topic | Flips it if `Enabled`, saves to `PlayerPrefs`, rebuilds. The COMING UP list changes with it. |
| ALLOW REMINDERS | `PermissionButton()` | Shows the system prompt (button reads ASKING meanwhile). Only offered after the first stage is cleared. |
| OPEN PHONE SETTINGS | `PermissionButton()` | Opens the app's page in the phone's settings. The game never asks again by itself. |

### States

| Permission | Drawn |
|---|---|
| `Unsupported` (today: no package) | "Reminders are not available on this device yet.", every switch disabled, no list |
| `NotAsked`, no stage cleared | ALLOW REMINDERS disabled, "Clear your first stage first." |
| `NotAsked` | ALLOW REMINDERS; switches can be set ahead; "Nothing is scheduled until reminders are allowed." |
| asking | ASKING, disabled |
| `Denied` | OPEN PHONE SETTINGS; every switch disabled with "Allow reminders in your phone's settings first." |
| `Granted`, Reminders on | the list is `NotificationPlanner.Plan`'s own answer |
| `Granted`, Reminders off | topics disabled with "Turn on Reminders first.", "Reminders are off." |

## 4. Rewarded ads

**Presenter** `RewardedAdPresenter` (view `RewardedAdView`), one per placement. **Controller**
`RewardedAdController` in `Assets/Scripts/Game/Ads/AdService.cs`. **Rules** `AdRewards` in Core, placements in
`ads.json`, state in the save's `ads` block (version 27, written only once an ad is granted). **No server function**:
the save is client-authoritative and `Docs/DESIGN-REWARDED-ADS.md` proposes no server callback for launch.

**`AdsSwitch` is OFF.** With it off every view is `Hidden` and nothing in this section is drawn, so the screens can be
built and captured now (flip it in the capture tool) and ship invisible until the owner decides.

**Where.** Never on `StoreScreen`, `EventsScreen`, `LiveEventsScreen`, `WayfarersRoadScreen` or any offer card (the
checks hold every UI file with those names to it, and `R28AdsLadderUiChecks.cs` holds the identifiers `RewardedAdBlock`,
`RewardedAdController`, `RewardedAdPresenter`, `AdRewards`, `AdBackends`, `AdContext` and `AdPlacementKind` to the
three files below and the block's own). Exactly three places:

| Placement | Screen | Context the screen passes |
|---|---|---|
| A1 `AdPlacementKind.IdleGold` | `IdleClaimPopup`, after the claim, under the claimed amounts | `AdContext.ClaimGold` = the Gold that claim paid |
| A2 `AdPlacementKind.Brand` | `ProvingScreen`, beside the Brands counter | none |
| A3 `AdPlacementKind.RepeatClear` | `WaveResultScreen`'s stage cleared view, on a repeat clear that paid the floor | `AdContext.Stage` = the stage |

**A3 cannot be seen today.** A stage with a wave curve that is already cleared is swept (`CampaignFlow.Launch` calls
`CampaignFlow.Sweep`, a toast) and never reaches `WaveResultScreen`, which refuses a cleared stage
(`WaveRefusal.StageCleared`); a stage with no curve shows its repeat clear on `BattleScreen`'s result. So the stage cleared
view only ever shows a FIRST clear, and `BattleRewards.RepeatCapped` is never true there. The block is built there as
specified and asked about only for `!FirstClear && RepeatCapped`, so it is dormant, and the A3 captures forge the repeat on
the screen's copy of the result. Where a repeat clear that paid the floor is shown to a player is an owner decision (the
sweep toast has no room for a button).

### Layout (one block, the same in all three)

```
+------------------------------------------------------------------+
| Watch an ad: +2 hours of idle Gold = 41,200 Gold    1 left today |  RewardLine | LeftTodayText
|                                         [ WATCH AN AD ]          |  Watch
+------------------------------------------------------------------+
after the ad:
+------------------------------------------------------------------+
| +41,200 Gold (Sealed)                        Next one in 20h 10m |  ResultLine | NextResetText
|                                                    [ OK ]        |  Dismiss
+------------------------------------------------------------------+
```

A `UITheme.PanelSunken` plate at `HubStyle.PlateAlpha`, `HubStyle.BorderWarm` hairline, `UITheme.CornerRadiusSmall`,
placed below the screen's own primary action, never above it and never as the primary action: the screen's own
button (COLLECT, FIGHT, CONTINUE) stays the `GoldButton`, and WATCH AN AD is a `SecondaryButton`.

**As built.** `RewardedAdBlock` is one class for all three. It is a kit `Sunken` plate (not a `PanelSunken` colour at
`HubStyle.PlateAlpha`), its buttons `ForgeButtonKind.Secondary` and `HubStyle.TouchMin` tall, and it is empty and inactive
while the view is Hidden. A1: under the claim plate (the plate is a fixed size and the block is only there after a
claim), 760 wide, the context the Gold of the last claim in that visit. A2: right of the opponent card at the height of its
Brands rows, 430 wide, so it stacks (the line, the small line, the buttons) instead of putting the small line beside the
text; "beside the Brands counter" cannot mean inside the card, whose width the stat rows fill. A3: 760 wide under the
CONTINUE row. The disabled PLAYING button prints no Reason under it (the label says it). A screen holds a new controller
per visit, so an old result does not follow the player in.

### Bindings

| Element | Field | Style |
|---|---|---|
| The whole block | `Visible` (false: not built) | |
| Reward line | `RewardLine` | `UITheme.TextPrimary`, `UITheme.FontSmall`; the amount is already in it |
| Left today | `LeftTodayText` | `UITheme.TextMuted`, `UITheme.FontTiny` |
| WATCH AN AD | `Watch` (PLAYING while the ad is up) | `SecondaryButton` |
| Result line | `ResultLine`, `ResultIsReward` | reward: `HubStyle.ValueGold`; no reward: `UITheme.TextMuted` |
| Next one | `NextResetText` (`NextResetInSeconds` for the tick) | `UITheme.TextDim`, `UITheme.FontTiny` |
| OK | `Dismiss` | `SecondaryButton` |

### Taps

| Tap | Call | What happens |
|---|---|---|
| WATCH AN AD | `Tap(context)` | Keeps the offer on the button, shows the ad, and on the network's rewarded callback grants exactly that offer through `AdRewards.Grant` (Sealed Gold, a fixed Dust line, or one Brand). The screen then saves the game on `Granted`. |
| OK | `Dismiss()` | Clears the result line. The block disappears if the day's ad is used. |

### States

| State | When | Drawn |
|---|---|---|
| `Hidden` | switch off, nothing to pay, used today, or no ad loaded (offline, no fill, no consent) | nothing at all: nothing waits on an ad |
| `Ready` | an ad is loaded and the placement can pay | reward line, "1 left today", WATCH AN AD |
| `Playing` | the ad is up | PLAYING, disabled, the line that was tapped |
| `Granted` | paid | "+41,200 Gold (Sealed)", "Next one in 20h 10m", OK |
| `NotFinished` | closed early | "No reward: the ad was not finished.", OK, WATCH again (nothing was spent) |
| `Failed` | could not show | "The ad could not be shown. Nothing was spent.", OK |
| `Refused` | already paid, or the Brands filled while it played | the refusal's sentence, OK |

**The Belgian rule.** Nothing bought is ever random, and nothing paid for watching is either. Each placement pays one
fixed line, shown in full before the tap and granted exactly as shown. No ad grants a summon, a Shard, a reroll, a
refresh, a forge attempt, a mystery reward or anything a Store offer sells as its headline. The checks scan the ad files
for the words of a luck mechanic and fail on any.

## Dependencies and owner steps

- **Base.** This work sits on the guild dungeon size work (`GuildDungeonScale`, `GuildDungeonRewards`, the status
  columns, `0022_guild_dungeon_size.sql`), the leaderboards and notifications work (`0023_leaderboards.sql`,
  `NotificationPlanner`) and the rewarded ads record (`Docs/DESIGN-REWARDED-ADS.md`), all of which are on main (merged as the integration branch; migrations 0022 and 0023).
- **Migration numbers.** The leaderboards migration is `0023_leaderboards.sql` and the guild dungeon size migration is `0022_guild_dungeon_size.sql`; both were renumbered when the branches merged.
- **Before ads ship:** apply the migration `Server/supabase/migrations/0026_save_limits_27.sql`, then turn `AdsSwitch` on (`EMBERFALL_ADS`).
- **Before reminders ship:** add `com.unity.mobile.notifications` and an Android `INotificationBackend` behind
  `EMBERFALL_MOBILE_NOTIFICATIONS`, set as `NotificationBackends.Override` at startup.
- **Before real ads ship:** add the LevelPlay package and an `IAdBackend` behind `EMBERFALL_LEVELPLAY`, set as
  `AdBackends.Override` after consent and init.
