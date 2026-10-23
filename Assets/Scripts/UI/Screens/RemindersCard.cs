using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Progression;
using Emberfall.Core.Screens;
using Emberfall.Game;
using Emberfall.Game.Notifications;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// The REMINDERS card of the settings page (Docs/ui-specs/R28-SCREENS.md, "Notification settings"): whether the
    /// phone allows reminders, the Reminders switch with its three topics under it, what is planned to arrive, and the
    /// rules reminders keep.
    ///
    /// IT DECIDES NOTHING. Every word and every switch is a field of the NotificationSettingsView that
    /// NotificationSettingsController builds; the card lays it out and rebuilds when the controller says Changed and
    /// when the view differs on the once-a-second look. A tap calls Toggle or PermissionButton on the controller and
    /// nothing else: no PlayerPrefs, no backend, no save, no copy of its own beyond the three constants below, which the
    /// spec names.
    ///
    /// Today the build has no notification package, so the controller's backend reports Unsupported and the card says
    /// so once, with every switch disabled and no list. That is the real state of the game, not a placeholder.
    /// </summary>
    internal sealed class RemindersCard
    {
        /// <summary>The card's heading, as the spec names the card.</summary>
        public const string Heading = "REMINDERS";
        /// <summary>The list's heading (the spec: constant).</summary>
        public const string UpcomingHeading = "COMING UP";
        /// <summary>The permission button that opens the phone's settings is the one secondary action in the card; ALLOW
        /// REMINDERS and ASKING are the primary one. The view carries a label and no kind, so the label tells them apart.</summary>
        public const string OpenSettingsLabel = "OPEN PHONE SETTINGS";
        public const string SwitchOn = "ON";
        public const string SwitchOff = "OFF";

        private readonly GameApp _app;
        private RectTransform _card;
        private LayoutElement _size;
        private float _width;
        private NotificationSettingsController _controller;
        private string _key;
        private float _tick;

        public RemindersCard(GameApp app)
        {
            _app = app;
        }

        public void Build(RectTransform card, LayoutElement size, float cardWidth)
        {
            _card = card;
            _size = size;
            _width = cardWidth;
        }

        /// <summary>The page is on screen: a controller for this visit, so the switches are read fresh from the device.</summary>
        public void Show()
        {
            Hide();
            _controller = new NotificationSettingsController(_app.Session, null);
            _controller.Changed += OnChanged;
            _key = null;
            _tick = 0f;
            Refresh();
        }

        public void Hide()
        {
            if (_controller != null) _controller.Changed -= OnChanged;
            _controller = null;
        }

        /// <summary>For the capture tool: the controller behind the card, so a target can press the same two taps a player does.</summary>
        public NotificationSettingsController ControllerForCapture { get { return _controller; } }

        public RectTransform CardRect { get { return _card; } }

        /// <summary>Called by the page every frame it is open. A look once a second rebuilds only when the view differs.</summary>
        public void Tick(float deltaTime)
        {
            if (_controller == null) return;
            _tick += deltaTime;
            if (_tick < 1f) return;
            _tick = 0f;
            Refresh();
        }

        private void OnChanged()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (_controller == null || _card == null) return;
            NotificationSettingsView v = _controller.View;
            string key = RemindersCardLayout.StructureKey(v);
            if (key == _key) return;
            _key = key;
            Rebuild(v);
        }

        // ------------------------------------------------------------------ drawing

        private void Rebuild(NotificationSettingsView v)
        {
            for (int i = _card.childCount - 1; i >= 0; i--)
            {
                GameObject old = _card.GetChild(i).gameObject;
                old.SetActive(false);
                Object.Destroy(old);
            }

            RemindersPlan plan = RemindersCardLayout.Plan(v, _width);
            _size.preferredHeight = plan.Height;
            _card.sizeDelta = new Vector2(_card.sizeDelta.x, plan.Height);

            EmberforgeUI.Heading("Heading", _card, Heading, RemindersCardLayout.Pad, -22f);

            BuildPermission(v, plan);
            Rule(plan.RuleTop1);

            ToggleModel[] toggles = { v.Master, v.IdleChest, v.Orders, v.Events };
            NotificationKind?[] topics = { null, NotificationKind.IdleChestFull, NotificationKind.OrdersReset, NotificationKind.EventEndingWeekly };
            for (int i = 0; i < toggles.Length; i++) BuildToggle(v, plan, i, toggles[i], topics[i]);

            if (plan.ShowUpcoming) BuildUpcoming(v, plan);

            Rule(plan.RuleTop3);
            for (int i = 0; i < v.RuleLines.Count; i++)
            {
                Text rule = EmberforgeUI.Label("RuleLine" + i, _card, v.RuleLines[i], UITheme.FontTiny, UITheme.TextDim, TextAnchor.MiddleLeft);
                UIBuilder.Box(rule.rectTransform, RemindersCardLayout.Pad, -(plan.RulesTop + i * RemindersCardLayout.LineHeight),
                              _width - RemindersCardLayout.Pad * 2f, RemindersCardLayout.LineHeight, new Vector2(0f, 1f));
            }
        }

        private void BuildPermission(NotificationSettingsView v, RemindersPlan plan)
        {
            bool button = v.PermissionButton.Visible;
            float lineWidth = _width - RemindersCardLayout.Pad * 2f - (button ? RemindersCardLayout.PermissionButtonWidth + 16f : 0f);
            Text line = EmberforgeUI.Body("PermissionLine", _card, v.PermissionLine, UITheme.FontSmall, UITheme.TextPrimary, TextAnchor.MiddleLeft);
            UIBuilder.Box(line.rectTransform, RemindersCardLayout.Pad, -plan.PermissionTop, lineWidth, plan.PermissionHeight, new Vector2(0f, 1f));
            if (!button) return;

            ButtonModel model = v.PermissionButton;
            ForgeButtonKind kind = model.Label == OpenSettingsLabel ? ForgeButtonKind.Secondary : ForgeButtonKind.Primary;
            UIButton ask = EmberforgeUI.Button("Permission", _card, model.Label, HubStyle.FontButton, kind);
            UIBuilder.Box(ask.Root, -RemindersCardLayout.Pad, -plan.PermissionTop, RemindersCardLayout.PermissionButtonWidth, RemindersCardLayout.TapMin, new Vector2(1f, 1f));
            ask.Interactable = model.Enabled;
            ask.OnClick(delegate
            {
                _app.Audio.Tap();
                if (_controller != null) _controller.PermissionButton();
            });
            if (plan.ReasonUnderButton)
            {
                Text why = EmberforgeUI.Label("PermissionReason", _card, model.Reason, UITheme.FontTiny, UITheme.TextMuted, TextAnchor.UpperRight);
                UIBuilder.Box(why.rectTransform, -RemindersCardLayout.Pad, -(plan.PermissionTop + RemindersCardLayout.TapMin + 2f),
                              RemindersCardLayout.PermissionButtonWidth + 200f, RemindersCardLayout.ReasonHeight, new Vector2(1f, 1f));
            }
        }

        private void BuildToggle(NotificationSettingsView v, RemindersPlan plan, int index, ToggleModel t, NotificationKind? topic)
        {
            float indent = index == 0 ? 0f : RemindersCardLayout.TopicIndent;
            float x = RemindersCardLayout.Pad + indent;
            float top = plan.ToggleTops[index] + RemindersCardLayout.RowPad * 0.5f;
            float textWidth = RemindersCardLayout.TextWidth(_width, indent);

            Text label = EmberforgeUI.Label("Label" + index, _card, t.Label, UITheme.FontSmall, t.Enabled ? UITheme.TextPrimary : UITheme.TextMuted, TextAnchor.MiddleLeft);
            UIBuilder.Box(label.rectTransform, x, -top, textWidth, RemindersCardLayout.TitleLineHeight, new Vector2(0f, 1f));

            string sub = RemindersCardLayout.SubLine(t, v.Phase == ScreenPhase.Unavailable ? v.Status : "");
            float below = top + RemindersCardLayout.TitleLineHeight;
            if (sub.Length > 0)
            {
                Text description = EmberforgeUI.Body("Sub" + index, _card, sub, UITheme.FontTiny, UITheme.TextMuted, TextAnchor.UpperLeft);
                UIBuilder.Box(description.rectTransform, x, -below, textWidth, plan.SubLines[index] * RemindersCardLayout.LineHeight, new Vector2(0f, 1f));
                below += plan.SubLines[index] * RemindersCardLayout.LineHeight;
            }
            if (t.Note.Length > 0)
            {
                Text note = EmberforgeUI.Label("Note" + index, _card, t.Note, UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperLeft);
                UIBuilder.Box(note.rectTransform, x, -below, textWidth, RemindersCardLayout.LineHeight, new Vector2(0f, 1f));
            }

            UIButton toggle = EmberforgeUI.Button("Switch" + index, _card, t.On ? SwitchOn : SwitchOff, HubStyle.FontButton,
                                                  t.On ? ForgeButtonKind.Primary : ForgeButtonKind.Secondary);
            UIBuilder.Box(toggle.Root, -RemindersCardLayout.Pad, -(plan.ToggleTops[index] + (plan.ToggleHeights[index] - RemindersCardLayout.TapMin) * 0.5f),
                          RemindersCardLayout.SwitchWidth, RemindersCardLayout.TapMin, new Vector2(1f, 1f));
            toggle.Interactable = t.Enabled;
            toggle.OnClick(delegate
            {
                _app.Audio.Tap();
                if (_controller != null) _controller.Toggle(topic);
            });
        }

        private void BuildUpcoming(NotificationSettingsView v, RemindersPlan plan)
        {
            Rule(plan.RuleTop2);
            Text header = EmberforgeUI.Label("UpcomingHeading", _card, UpcomingHeading, UITheme.FontTiny, UITheme.ChromeBrass, TextAnchor.MiddleLeft);
            UIBuilder.Box(header.rectTransform, RemindersCardLayout.Pad, -plan.UpcomingHeaderTop, 600f, 30f, new Vector2(0f, 1f));

            for (int i = 0; i < v.Upcoming.Count; i++)
            {
                UpcomingReminderView u = v.Upcoming[i];
                float y = plan.UpcomingTops[i];
                Text title = EmberforgeUI.Label("UpTitle" + i, _card, u.Title, UITheme.FontSmall, UITheme.TextPrimary, TextAnchor.MiddleLeft);
                UIBuilder.Box(title.rectTransform, RemindersCardLayout.Pad + RemindersCardLayout.TopicIndent, -y,
                              _width - RemindersCardLayout.Pad * 2f - 300f, RemindersCardLayout.TitleLineHeight, new Vector2(0f, 1f));

                Text when = EmberforgeUI.Label("UpWhen" + i, _card, u.WhenText, UITheme.FontSmall, UITheme.TextMuted, TextAnchor.MiddleRight);
                UIBuilder.Box(when.rectTransform, -RemindersCardLayout.Pad, -y, 280f, RemindersCardLayout.TitleLineHeight, new Vector2(1f, 1f));

                if (u.Body.Length > 0)
                {
                    float bodyWidth = _width - RemindersCardLayout.Pad * 2f - 240f;
                    Text body = EmberforgeUI.Body("UpBody" + i, _card, u.Body, UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperLeft);
                    UIBuilder.Box(body.rectTransform, RemindersCardLayout.Pad + RemindersCardLayout.TopicIndent, -(y + RemindersCardLayout.TitleLineHeight),
                                  bodyWidth, RemindersCardLayout.LinesFor(u.Body, bodyWidth) * RemindersCardLayout.LineHeight, new Vector2(0f, 1f));
                }
            }

            if (v.UpcomingStatus.Length > 0)
            {
                Text status = EmberforgeUI.Label("UpcomingStatus", _card, v.UpcomingStatus, UITheme.FontTiny, UITheme.TextMuted, TextAnchor.MiddleLeft);
                UIBuilder.Box(status.rectTransform, RemindersCardLayout.Pad + RemindersCardLayout.TopicIndent, -plan.UpcomingStatusTop,
                              _width - RemindersCardLayout.Pad * 2f, RemindersCardLayout.LineHeight, new Vector2(0f, 1f));
            }
        }

        private void Rule(float bandTop)
        {
            Image rule = EmberforgeUI.Rule("Rule", _card);
            UIBuilder.Box(rule.rectTransform, RemindersCardLayout.Pad, -(bandTop + (RemindersCardLayout.RuleBand - Emberforge.RuleHeight) * 0.5f),
                          _width - RemindersCardLayout.Pad * 2f, Emberforge.RuleHeight, new Vector2(0f, 1f));
        }
    }
}
