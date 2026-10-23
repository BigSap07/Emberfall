# Design: local notifications

Decision record from 2026-10-22. The reasoning is durable. Names and numbers are not.
`DESIGN-BUILD-ORDER.md` says what is built.

## What is built and what is not

- **Built:** `NotificationPlanner` in `Assets/Scripts/Core/Progression`, the rule for when each
  notification fires given a save, and `NotificationChecks.cs` in `Tools/DevHarness`, which proves it.
  It is engine-free Core, reads the profile and content, writes nothing, and the same inputs give the
  same plan.
- **Built in R28:** the Unity glue short of the package. `Assets/Scripts/Game/Notifications/NotificationService.cs`
  holds `INotificationBackend` (with `UnsupportedNotificationBackend` as the only implementation),
  `NotificationPrefsStore` (the four switches in `PlayerPrefs`), `NotificationScheduler` (called from
  `GameApp`'s pause and quit hooks: re-plan on leaving, clear on return; inert until a backend reports the
  permission granted) and the settings screen's controller. `NotificationSettingsPresenter` says what the
  settings screen draws (`Docs/ui-specs/R28-SCREENS.md`).
- **Not built:** the package and the Android backend. The Mobile Notifications package is not in
  `Packages/manifest.json`, so nothing is scheduled on a device yet. The section **What a Unity Mobile
  Notifications integration needs** below is the list for whoever builds it.

## The three notifications

| Kind | Fires | Only when | Id | Title / body |
|---|---|---|---|---|
| Idle chest full | the moment the chest reaches `Balance.IdleCapHours` after `LastIdleClaimUnix` | the chest is not already full when the plan is made | 101 | "Your chest is full" / "Your heroes have filled the idle chest. Collect it so they keep earning." |
| Orders reset | the next reward-day turnover (`GameSession.DayStartUnix` + 1 day, 04:00 UTC) | `DailyLoop.Active` (the loop is OFF by default, so this never fires in the shipped build) | 102 | "New orders are posted" / "A fresh set of daily orders is waiting on the board." |
| Event ending | 8 hours before the weekly event ends, 24 hours before the monthly one (`LiveEvents.PeriodEndUnix`) | `LiveEvents.Active`, the player has taken part (points or quest progress above 0), and something free is still unclaimed or unearned | 103 weekly, 104 monthly | "{event} ends soon" / one of two bodies: rewards waiting, or time left to earn |

Only the **next** orders turnover is planned. A player who does not come back is not reminded every
morning.

An event reminder is about the **free** side only. It is never planned because a paid track or a pack is
for sale, and its words never mention either.

## The caps

| Rule | Value | Why |
|---|---|---|
| Quiet hours | 22:00 to 09:00, device local time | A reward that is ready can wait for the morning. Nobody should be woken by a game. |
| A "ready" reminder in quiet hours | moves to 09:00 local | The chest and the orders do not expire at night. |
| A deadline in quiet hours | moves to 21:45 local the evening before | Moving it later would pass the deadline. If even that is too late it is dropped. |
| Gap between any two | 4 hours | Two pings close together read as nagging. |
| Per local day | 2 | A ceiling, not a target. Most days plan one or none. |
| Horizon | 48 hours | A player who stops playing gets what was planned at their last visit, then silence. Nothing re-arms itself. |
| Minimum lead | 30 minutes | Backgrounding the app for a moment does not ping a minute later. |
| First session | none until a stage is cleared | The first session teaches the game. The permission prompt belongs after it. |

**Priority when two collide:** event ending, then idle chest, then orders. The higher one keeps its time.
A ready reminder slides later by the gap; a deadline slides earlier or is dropped.

All of these live in `NotificationRules` with these defaults. They are tuning numbers, not content, so
they are not in a JSON file yet (see **Decisions for the owner**).

## Nothing sold

No notification names a price, an offer, a pack, the store or a purchase, and none is planned because
something is for sale. `CheckNotifySweep` scans every notification the sweep plans for those words and
fails on any. This follows the project's rule for paid goods (`Docs/DESIGN-SEALED.md`, `Docs/DESIGN-STORE.md`:
everything bought is fixed and shown up front, nothing bought is random). A push that sells, or that
counts down a sale, is the same pressure in a different place and is out.

## When the plan is made

The planner is called with the profile, the device clock, the device's UTC offset in minutes and the
player's switches (`NotificationPrefs`). The Unity side should:

1. **On going to the background** (`OnApplicationPause(true)`, and on quit): cancel every notification it
   scheduled, call `NotificationPlanner.Plan`, schedule exactly what it returns with the ids it returns.
2. **On coming back** (`OnApplicationPause(false)`): cancel every scheduled notification and clear the
   delivered ones, so nothing pings while the player is in the game.

The plan is re-made on every background, so it is always derived from the save as it stands. There is no
notification state in the save and no save version bump.

The device clock is what local notifications run on anyway. A player who moves their clock moves only
their own reminders.

## What a Unity Mobile Notifications integration needs

Android is the target (`CLAUDE.md`); iOS is blocked on hardware (`DESIGN-BUILD-ORDER.md`, **Blocked**).

1. **The package.** `com.unity.mobile.notifications` (the 2.x line supports Unity 6) in
   `Packages/manifest.json`. A package change is an owner decision: it touches every build.
2. **A channel.** Register one Android channel at startup with
   `AndroidNotificationCenter.RegisterNotificationChannel`, for example id `emberfall_reminders`, name
   "Reminders", default importance, a one-line description. One channel is enough for three kinds; split
   later if players ask to mute one kind from the system settings.
3. **The permission.** Android 13 and later need `POST_NOTIFICATIONS` at runtime. Ask through the
   package's `PermissionRequest` only after the first stage is cleared (the planner returns nothing before
   that anyway), behind a one-line in-game explanation. A refusal is final until the player turns it on in
   settings; do not ask again every launch.
4. **Icons.** A small icon (white silhouette on transparent, Android requires it) and optionally a large
   icon, added in Project Settings, Mobile Notifications. Without a small icon Android shows a blank square.
5. **Scheduling.** For each `PlannedNotification`: an `AndroidNotification` with `Title`, `Text` = `Body`,
   `FireTime` = the local `DateTime` of `FireUnix`, the small icon id, then
   `AndroidNotificationCenter.SendNotificationWithExplicitID(n, channel, Id)`. Explicit ids are what make a
   re-plan replace rather than stack.
6. **Cancelling.** `CancelAllScheduledNotifications` before each plan, and
   `CancelAllScheduledNotifications` plus `CancelAllDisplayedNotifications` on resume.
7. **Inexact timing is fine.** Do not request exact alarms (`SCHEDULE_EXACT_ALARM`). A reminder a few
   minutes late costs nothing, and the exact-alarm permission is restricted on recent Android versions.
8. **Device restart.** Turn on "Reschedule on Device Restart" in the package settings (it adds
   `RECEIVE_BOOT_COMPLETED`). Without it a reboot drops the plan until the next launch.
9. **The UTC offset.** `(int)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes`. A daylight
   saving change inside the 48 hour horizon can put one reminder an hour off; that is accepted.
10. **The player's switches.** Four toggles (all, idle chest, orders, events) stored in `PlayerPrefs`, not
    in the save: they are a device setting, and a second device may want different ones. They map onto
    `NotificationPrefs`.
11. **A tap.** `AndroidNotificationCenter.GetLastNotificationIntent` on launch says which notification
    opened the game. Routing it (the Orders screen, the Events page) is UI work and out of this pass.
12. **A switch.** Like every system here, the glue should sit behind a switch so the build without it is
    one define away. Where the glue lives is `Assets/Scripts/Game`, never `Assets/Scripts/UI`, and never
    in Core: Core cannot reference the package.

## Checks

`NotificationChecks.cs`: `CheckNotifyGates` (no plan before the first clear, the master switch, each
topic's own switch), `CheckNotifyIdle` (fires at the cap, quiet hours move it to 09:00, a full chest is not
announced), `CheckNotifyOrders` (nothing with the loop off, the reset at noon in UTC+8 fires at the reset,
before dawn in UTC+1 it fires at 09:00, only one turnover), `CheckNotifyEvents` (nothing without taking
part, 8 hours before the week ends, the monthly event beyond the horizon is not planned, a deadline in quiet
hours moves earlier and never past the end, nothing ten minutes before the end, nothing with live events
off), `CheckNotifyPriority` (the chest keeps the minute, the orders move 4 hours), and `CheckNotifySweep`
(about 1,500 plans over a week of clocks, eleven time zones and four save shapes: no quiet hours, no gap
under 4 hours, no day over 2, nothing outside the lead and horizon, no deadline after its event, nothing
written to the profile, the same plan every time, no word that sells, no em or en dash).

## Decisions for the owner

1. **Ship the integration at all, and when.** Adding the package and the glue is the next step; the rule is
   ready for it.
2. **The numbers.** Quiet hours 22:00 to 09:00, 2 a day, 4 hours apart, 48 hour horizon, event leads of 8
   and 24 hours. Whether to move them into a JSON file (like `orders.json`) or keep them in code.
3. **The orders reminder** only matters once the daily loop is on by default (`Docs/DAILY-LOOP-CURVE.md`).
   A variant that fires two hours before the reset when finished orders are still unclaimed was considered
   and left out: it would be a third daily ping for the most engaged players.
4. **Default on or off** for each toggle after the permission is granted. The planner assumes all on.
