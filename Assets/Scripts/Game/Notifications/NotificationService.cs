using System;
using System.Collections.Generic;
using UnityEngine;
using Emberfall.Core.Progression;
using Emberfall.Core.Screens;
using Emberfall.Core.Session;

namespace Emberfall.Game.Notifications
{
    /// <summary>
    /// What the game needs from the device's notification system (Docs/DESIGN-NOTIFICATIONS.md, "What a Unity
    /// Mobile Notifications integration needs"). AN INTERFACE because the package (com.unity.mobile.notifications)
    /// is not in the project: adding it is an owner decision that touches every build. Until then the game runs
    /// on UnsupportedNotificationBackend, which schedules nothing and makes the settings screen say so.
    ///
    /// The Android implementation, once the package is added, lives in this folder behind a scripting define
    /// (EMBERFALL_MOBILE_NOTIFICATIONS) so the build-check's shim compile stays independent of the package: one
    /// channel, explicit ids (PlannedNotification.Id), inexact timing, reschedule on restart.
    /// </summary>
    public interface INotificationBackend
    {
        NotificationPermission Permission { get; }

        /// <summary>Shows the system's permission prompt. done receives the answer.</summary>
        void RequestPermission(Action<NotificationPermission> done);

        /// <summary>Opens this app's page in the phone's settings, where a refused permission is changed.</summary>
        void OpenSystemSettings();

        /// <summary>Cancels every notification this app scheduled, and clears the delivered ones.</summary>
        void CancelAll();

        /// <summary>Schedules one notification with its explicit id, so a re-plan replaces rather than stacks.</summary>
        void Schedule(PlannedNotification note);
    }

    /// <summary>The backend of every build without the notifications package: nothing is ever scheduled.</summary>
    public sealed class UnsupportedNotificationBackend : INotificationBackend
    {
        public NotificationPermission Permission { get { return NotificationPermission.Unsupported; } }
        public void RequestPermission(Action<NotificationPermission> done) { if (done != null) done(NotificationPermission.Unsupported); }
        public void OpenSystemSettings() { }
        public void CancelAll() { }
        public void Schedule(PlannedNotification note) { }
    }

    public static class NotificationBackends
    {
        /// <summary>Set only by editor tooling (a stand-in that records what would be scheduled) or, once the
        /// package is added, by the Android backend at startup.</summary>
        public static INotificationBackend Override;

        private static readonly UnsupportedNotificationBackend _unsupported = new UnsupportedNotificationBackend();

        public static INotificationBackend Current { get { return Override ?? _unsupported; } }
    }

    /// <summary>
    /// The player's four switches, kept in PlayerPrefs and never in the save: they are a device setting, and a
    /// second device may want different ones (Docs/DESIGN-NOTIFICATIONS.md, item 10). All on by default.
    /// </summary>
    public static class NotificationPrefsStore
    {
        private const string KeyEnabled = "emberfall.notify.enabled";
        private const string KeyIdle = "emberfall.notify.idle";
        private const string KeyOrders = "emberfall.notify.orders";
        private const string KeyEvents = "emberfall.notify.events";

        public static NotificationPrefs Load()
        {
            NotificationPrefs p = new NotificationPrefs();
            p.Enabled = PlayerPrefs.GetInt(KeyEnabled, 1) != 0;
            p.IdleChest = PlayerPrefs.GetInt(KeyIdle, 1) != 0;
            p.Orders = PlayerPrefs.GetInt(KeyOrders, 1) != 0;
            p.Events = PlayerPrefs.GetInt(KeyEvents, 1) != 0;
            return p;
        }

        public static void Save(NotificationPrefs p)
        {
            if (p == null) return;
            PlayerPrefs.SetInt(KeyEnabled, p.Enabled ? 1 : 0);
            PlayerPrefs.SetInt(KeyIdle, p.IdleChest ? 1 : 0);
            PlayerPrefs.SetInt(KeyOrders, p.Orders ? 1 : 0);
            PlayerPrefs.SetInt(KeyEvents, p.Events ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// When the plan is made (Docs/DESIGN-NOTIFICATIONS.md, "When the plan is made"): on going to the background,
    /// cancel everything and schedule exactly what NotificationPlanner.Plan returns; on coming back, cancel
    /// everything so nothing pings while the player is in the game. Nothing here writes the save.
    /// </summary>
    public static class NotificationScheduler
    {
        public static int UtcOffsetMinutes()
        {
            return (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes;
        }

        public static void OnBackground(GameSession session)
        {
            INotificationBackend backend = NotificationBackends.Current;
            if (backend.Permission != NotificationPermission.Granted || session == null) return;
            backend.CancelAll();
            List<PlannedNotification> plan = NotificationPlanner.Plan(session, TimeUtil.NowUnix(), UtcOffsetMinutes(), NotificationPrefsStore.Load());
            for (int i = 0; i < plan.Count; i++) backend.Schedule(plan[i]);
        }

        public static void OnForeground()
        {
            INotificationBackend backend = NotificationBackends.Current;
            if (backend.Permission == NotificationPermission.Unsupported) return;
            backend.CancelAll();
        }
    }

    /// <summary>
    /// The settings screen's half that acts: it holds the switches, asks for the permission, and rebuilds the
    /// view. The screen reads View and calls the tap methods. Docs/ui-specs/R28-SCREENS.md, "Notifications".
    /// </summary>
    public sealed class NotificationSettingsController
    {
        private readonly GameSession _session;
        private readonly INotificationBackend _backend;
        private NotificationPrefs _prefs;
        private bool _asking;

        public event Action Changed;

        public NotificationSettingsController(GameSession session, INotificationBackend backend)
        {
            _session = session;
            _backend = backend ?? NotificationBackends.Current;
            _prefs = NotificationPrefsStore.Load();
        }

        public NotificationSettingsView View
        {
            get
            {
                return NotificationSettingsPresenter.Build(_session, _prefs, _backend.Permission, _asking,
                                                           TimeUtil.NowUnix(), NotificationScheduler.UtcOffsetMinutes());
            }
        }

        /// <summary>The Reminders switch (topic null) or one topic's switch.</summary>
        public void Toggle(NotificationKind? topic)
        {
            _prefs = NotificationSettingsPresenter.Toggle(View, _prefs, topic);
            NotificationPrefsStore.Save(_prefs);
            Raise();
        }

        /// <summary>ALLOW REMINDERS or OPEN PHONE SETTINGS, whichever the view drew.</summary>
        public void PermissionButton()
        {
            NotificationSettingsView v = View;
            if (!v.PermissionButton.Visible || !v.PermissionButton.Enabled) return;
            if (_backend.Permission == NotificationPermission.Denied) { _backend.OpenSystemSettings(); return; }
            _asking = true;
            Raise();
            _backend.RequestPermission(delegate (NotificationPermission answer)
            {
                _asking = false;
                Raise();
            });
        }

        private void Raise()
        {
            if (Changed != null) Changed();
        }
    }
}
