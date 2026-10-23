using System;
using System.Collections.Generic;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.Core.Screens
{
    /// <summary>Where the device's notification permission stands, as the Game layer's backend reports it.</summary>
    public enum NotificationPermission
    {
        /// <summary>This build or device cannot schedule notifications at all (the package is not in the project).</summary>
        Unsupported,
        /// <summary>Never asked. The planner plans nothing before the first stage is cleared, and the ask waits for it too.</summary>
        NotAsked,
        Granted,
        /// <summary>Refused. Final until the player changes it in the phone's settings; the game never asks again by itself.</summary>
        Denied
    }

    /// <summary>One reminder the device has planned, as the settings screen lists it.</summary>
    public sealed class UpcomingReminderView
    {
        public NotificationKind Kind;
        public string Title = "";
        public string Body = "";
        /// <summary>"tomorrow 09:00", device local time.</summary>
        public string WhenText = "";
        public long FireUnix;
    }

    /// <summary>Everything the notification settings screen draws.</summary>
    public sealed class NotificationSettingsView
    {
        public ScreenPhase Phase;
        public string Title = "NOTIFICATIONS";
        /// <summary>The whole-screen sentence when Unavailable.</summary>
        public string Status = "";
        /// <summary>Where the permission stands, in one line: "Reminders are allowed on this phone."</summary>
        public string PermissionLine = "";
        /// <summary>ALLOW REMINDERS, OPEN PHONE SETTINGS, or hidden once granted.</summary>
        public ButtonModel PermissionButton = ButtonModel.Hidden();
        public ToggleModel Master = new ToggleModel();
        public ToggleModel IdleChest = new ToggleModel();
        public ToggleModel Orders = new ToggleModel();
        public ToggleModel Events = new ToggleModel();
        /// <summary>The fixed rules, one line each: quiet hours, the daily cap, nothing sold.</summary>
        public readonly List<string> RuleLines = new List<string>();
        /// <summary>What would be scheduled if the app went to the background now, soonest first.</summary>
        public readonly List<UpcomingReminderView> Upcoming = new List<UpcomingReminderView>();
        /// <summary>Shown in place of the list when it is empty or hidden: "No reminders planned right now."</summary>
        public string UpcomingStatus = "";
    }

    /// <summary>
    /// THE NOTIFICATION SETTINGS SCREEN (Docs/DESIGN-NOTIFICATIONS.md, Docs/ui-specs/R28-SCREENS.md).
    ///
    /// A pure function of the save, the player's switches (NotificationPrefs, kept in PlayerPrefs by the Game
    /// layer, never in the save), the permission and the clock. The upcoming list is NotificationPlanner.Plan's
    /// own answer, so the screen shows exactly what the device would schedule, never a second guess at it.
    ///
    /// Nothing sold: no line here names a price, an offer or a purchase, and no reminder is planned because
    /// something is for sale (NotificationPlanner's rule; the R28 checks scan this screen's strings too).
    /// </summary>
    public static class NotificationSettingsPresenter
    {
        public static NotificationSettingsView Build(GameSession session, NotificationPrefs prefs, NotificationPermission permission,
                                                     bool asking, long nowUnix, int utcOffsetMinutes)
        {
            return Build(session, prefs, permission, asking, nowUnix, utcOffsetMinutes, new NotificationRules());
        }

        public static NotificationSettingsView Build(GameSession session, NotificationPrefs prefs, NotificationPermission permission,
                                                     bool asking, long nowUnix, int utcOffsetMinutes, NotificationRules rules)
        {
            NotificationSettingsView v = new NotificationSettingsView();
            if (prefs == null) prefs = new NotificationPrefs();
            bool firstClear = session != null && session.Profile != null && session.Profile.ClearedStageIds.Count > 0;

            v.RuleLines.Add("Quiet hours " + Clock(rules.QuietStartMinute) + " to " + Clock(rules.QuietEndMinute) + ", your time. Nothing arrives in them.");
            v.RuleLines.Add("At most " + rules.MaxPerLocalDay + " a day, at least " + (rules.MinGapSeconds / 3600L) + " hours apart.");
            v.RuleLines.Add("Reminders are only ever about rewards you have earned.");

            v.Master.Label = "Reminders";
            v.Master.Description = "Let Emberfall remind you when something is ready.";
            v.Master.On = prefs.Enabled;
            v.IdleChest.Label = "Idle chest full";
            v.IdleChest.Description = "When your heroes have filled the idle chest.";
            v.IdleChest.On = prefs.IdleChest;
            v.Orders.Label = "New daily orders";
            v.Orders.Description = "When a fresh set of daily orders is posted.";
            v.Orders.On = prefs.Orders;
            v.Events.Label = "Event ending";
            v.Events.Description = "Before an event you are taking part in ends, while it still has free rewards for you.";
            v.Events.On = prefs.Events;
            if (session != null && !DailyLoop.Active(session)) v.Orders.Note = "Daily orders are not open yet.";
            if (session != null && !LiveEvents.Active(session)) v.Events.Note = "No events are running yet.";

            if (permission == NotificationPermission.Unsupported)
            {
                v.Phase = ScreenPhase.Unavailable;
                v.Status = "Reminders are not available on this device yet.";
                v.PermissionLine = v.Status;
                Disable(v, "Reminders are not available on this device yet.");
                v.UpcomingStatus = "";
                return v;
            }

            v.Phase = ScreenPhase.Ready;
            switch (permission)
            {
                case NotificationPermission.Granted:
                    v.PermissionLine = "Reminders are allowed on this phone.";
                    v.PermissionButton = ButtonModel.Hidden();
                    break;
                case NotificationPermission.Denied:
                    v.PermissionLine = "Reminders are blocked in your phone's settings.";
                    v.PermissionButton = ButtonModel.On("OPEN PHONE SETTINGS");
                    break;
                default:
                    v.PermissionLine = firstClear ? "Emberfall needs your permission to send reminders."
                                                  : "Reminders start after you clear your first stage.";
                    v.PermissionButton = asking ? ButtonModel.Off("ASKING", "Waiting for your answer.")
                                       : firstClear ? ButtonModel.On("ALLOW REMINDERS")
                                       : ButtonModel.Off("ALLOW REMINDERS", "Clear your first stage first.");
                    break;
            }

            if (permission == NotificationPermission.Denied)
            {
                Disable(v, "Allow reminders in your phone's settings first.");
            }
            else
            {
                v.Master.Enabled = true;
                string topicReason = prefs.Enabled ? "" : "Turn on Reminders first.";
                Topic(v.IdleChest, prefs.Enabled, topicReason);
                Topic(v.Orders, prefs.Enabled, topicReason);
                Topic(v.Events, prefs.Enabled, topicReason);
            }

            if (permission != NotificationPermission.Granted)
            {
                v.UpcomingStatus = permission == NotificationPermission.Denied ? "Nothing can be scheduled while reminders are blocked."
                                 : "Nothing is scheduled until reminders are allowed.";
                return v;
            }
            if (!prefs.Enabled)
            {
                v.UpcomingStatus = "Reminders are off.";
                return v;
            }
            if (!firstClear)
            {
                v.UpcomingStatus = "Reminders start after you clear your first stage.";
                return v;
            }

            List<PlannedNotification> plan = NotificationPlanner.Plan(session, nowUnix, utcOffsetMinutes, prefs, rules);
            for (int i = 0; i < plan.Count; i++)
            {
                UpcomingReminderView u = new UpcomingReminderView();
                u.Kind = plan[i].Kind;
                u.Title = plan[i].Title;
                u.Body = plan[i].Body;
                u.FireUnix = plan[i].FireUnix;
                u.WhenText = ScreenText.LocalDayAndClock(plan[i].FireUnix, nowUnix, utcOffsetMinutes);
                v.Upcoming.Add(u);
            }
            v.UpcomingStatus = v.Upcoming.Count == 0 ? "No reminders planned right now." : "";
            return v;
        }

        /// <summary>The prefs after one toggle is tapped. A toggle that cannot be flipped returns the prefs unchanged.</summary>
        public static NotificationPrefs Toggle(NotificationSettingsView view, NotificationPrefs prefs, NotificationKind? topic)
        {
            NotificationPrefs p = Copy(prefs);
            if (topic == null)
            {
                if (view.Master.Enabled) p.Enabled = !p.Enabled;
                return p;
            }
            switch (topic.Value)
            {
                case NotificationKind.IdleChestFull:
                    if (view.IdleChest.Enabled) p.IdleChest = !p.IdleChest;
                    break;
                case NotificationKind.OrdersReset:
                    if (view.Orders.Enabled) p.Orders = !p.Orders;
                    break;
                default:
                    if (view.Events.Enabled) p.Events = !p.Events;
                    break;
            }
            return p;
        }

        public static NotificationPrefs Copy(NotificationPrefs prefs)
        {
            NotificationPrefs p = new NotificationPrefs();
            if (prefs == null) return p;
            p.Enabled = prefs.Enabled;
            p.IdleChest = prefs.IdleChest;
            p.Orders = prefs.Orders;
            p.Events = prefs.Events;
            return p;
        }

        private static void Topic(ToggleModel t, bool masterOn, string reason)
        {
            t.Enabled = masterOn;
            t.Reason = masterOn ? "" : reason;
        }

        private static void Disable(NotificationSettingsView v, string reason)
        {
            ToggleModel[] all = { v.Master, v.IdleChest, v.Orders, v.Events };
            for (int i = 0; i < all.Length; i++)
            {
                all[i].Enabled = false;
                all[i].Reason = reason;
            }
        }

        private static string Clock(int minuteOfDay)
        {
            int m = ((minuteOfDay % 1440) + 1440) % 1440;
            return (m / 60).ToString("00") + ":" + (m % 60).ToString("00");
        }
    }
}
