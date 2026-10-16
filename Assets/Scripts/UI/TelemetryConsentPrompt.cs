using System;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Telemetry;
using Emberfall.Game;
using Emberfall.Game.Telemetry;

namespace Emberfall.UI
{
    /// <summary>
    /// THE ONE-TIME QUESTION about anonymous play stats (Docs/DESIGN-TELEMETRY.md). LAYOUT ONLY:
    /// whether it is owed is TelemetryPromptRule's call, what the answer does is the recorder's, and
    /// this draws the question, two buttons, and hands the answer to TelemetryService.
    ///
    /// AN OVERLAY, NOT A STEP. In the first session it stands on the boundary after the Wake and
    /// before the Cinder's recall, over a near-opaque scrim, and completes nothing: the coach keeps
    /// reading Profile.TutorialStep underneath, so the counter and the order (wake, recall,
    /// prologue, team, 1-1, wave result, 1-2, level up, idle) are exactly what they were. For a save
    /// whose first session is over it waits for the Hub with nothing else open.
    ///
    /// NO IS THE DEFAULT and is the lit button (Primary); Yes is the plain one. Nothing dismisses
    /// the prompt but one of the two: it swallows every tap, and Back never reaches it.
    ///
    /// It is installed after the coach, so it draws above it, and re-raises itself every frame
    /// while it is up in case the coach moves itself to the front for a step.
    /// </summary>
    public sealed class TelemetryConsentPrompt : MonoBehaviour
    {
        public static TelemetryConsentPrompt Instance { get; private set; }

        private GameApp _app;
        private RectTransform _root;
        private UIButton _yes;
        private UIButton _no;
        private bool _showing;

        /// <summary>True while the question is on screen.</summary>
        public static bool Showing
        {
            get { return Instance != null && Instance._showing; }
        }

        /// <summary>Builds the prompt once, hidden. Never throws: a failure leaves the game without
        /// the prompt, and so without stats, which is the safe side.</summary>
        public static TelemetryConsentPrompt Install(GameApp app)
        {
            if (!TelemetrySwitch.Enabled || app == null) return null;
            if (Instance != null) return Instance;
            try
            {
                RectTransform root = UIBuilder.Node("TelemetryConsent", app.OverlayLayer);
                UIBuilder.FillParent(root, 0f);
                TelemetryConsentPrompt prompt = root.gameObject.AddComponent<TelemetryConsentPrompt>();
                prompt._app = app;
                prompt._root = root;
                prompt.Build();
                Instance = prompt;
                return prompt;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void Build()
        {
            // The scrim swallows every tap, so nothing under the question can be pressed.
            Image scrim = _root.gameObject.AddComponent<Image>();
            scrim.sprite = TextureLab.White;
            scrim.color = UITheme.WithAlpha(UITheme.BackdropDeep, 0.94f);
            scrim.raycastTarget = true;

            RectTransform card = UIBuilder.Node("Card", _root);
            UIBuilder.Box(card, 0f, 0f, 1100f, 560f, new Vector2(0.5f, 0.5f));
            EmberforgeUI.PanelOn(card, true);

            Text title = EmberforgeUI.Title("Title", card, TelemetryText.PromptTitle, UITheme.FontTitle,
                                            Emberforge.TextGold, TextAnchor.MiddleCenter);
            UIBuilder.Box(title.rectTransform, 0f, -56f, 800f, 70f, new Vector2(0.5f, 1f));
            UIBuilder.BestFit(title, UITheme.FontHeading, UITheme.FontTitle);
            UIBuilder.Outlined(title, Emberforge.Outline, Emberforge.StrokeThin);

            Text body = EmberforgeUI.Body("Body", card, TelemetryText.PromptQuestion, UITheme.FontBody,
                                          Emberforge.TextBody, TextAnchor.UpperCenter);
            UIBuilder.Box(body.rectTransform, 0f, -150f, 940f, 150f, new Vector2(0.5f, 1f));
            UIBuilder.BestFit(body, UITheme.FontTiny, UITheme.FontBody);
            UIBuilder.Outlined(body, Emberforge.Outline, Emberforge.StrokeThin);

            Text note = EmberforgeUI.Label("Note", card, TelemetryText.PromptNote, UITheme.FontTiny,
                                           Emberforge.TextQuiet, TextAnchor.MiddleCenter);
            UIBuilder.Box(note.rectTransform, 0f, -320f, 900f, 40f, new Vector2(0.5f, 1f));
            note.raycastTarget = false;

            _yes = EmberforgeUI.Button("Yes", card, TelemetryText.Yes, UITheme.FontHeading, ForgeButtonKind.Secondary);
            UIBuilder.Box(_yes.Root, -230f, 56f, 380f, 96f, new Vector2(0.5f, 0f));
            _yes.OnClick(delegate { Answer(true); });

            _no = EmberforgeUI.Button("No", card, TelemetryText.No, UITheme.FontHeading, ForgeButtonKind.Primary);
            UIBuilder.Box(_no.Root, 230f, 56f, 380f, 96f, new Vector2(0.5f, 0f));
            _no.OnClick(delegate { Answer(false); });

            _root.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            TelemetryService service = TelemetryService.Instance;
            if (service == null || _app == null || _app.Session == null) return;

            if (_showing)
            {
                // Stays up until it is answered, whatever the screens underneath do meanwhile.
                if (!service.Pending) Hide();
                else _root.SetAsLastSibling();
                return;
            }

            bool onHub = _app.Router != null && _app.Router.Current != null && _app.Router.Current.Id == ScreenId.MainMenu;
            bool nothingOpen = (_app.Layers == null || _app.Layers.Count == 0)
                               && (OnboardingCoach.Instance == null || !OnboardingCoach.Instance.TipShowing);
            if (service.ShouldPrompt(onHub, nothingOpen)) Show();
        }

        private void Show()
        {
            _showing = true;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
        }

        private void Hide()
        {
            _showing = false;
            _root.gameObject.SetActive(false);
        }

        private void Answer(bool yes)
        {
            if (_app.Audio != null) _app.Audio.Tap();
            TelemetryService service = TelemetryService.Instance;
            if (service != null) service.Answer(yes);
            Hide();
        }

#if UNITY_EDITOR
        /// <summary>Capture door: presses a button through the same click a finger would, so the
        /// capture shows the real answer path and not a shortcut around it.</summary>
        public void ClickForCapture(bool yes)
        {
            (yes ? _yes : _no).Button.onClick.Invoke();
        }
#endif
    }
}
