using UnityEngine;
using UnityEngine.UI;
using Emberfall.Game.Cloud;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// The settings tab of the player panel: the handful of switches this game actually has, in
    /// one place, instead of one reset dialog hiding behind a "..." button on the map.
    ///
    /// NOTHING HERE IS NEW STATE. Sound is Sfx.Muted, which has existed since the audio pass and
    /// had no control anywhere in the game; the save line reads CloudCoordinator, which the
    /// Account tab already reads; and Reset is the confirm dialog the Hub's old options button
    /// opened, moved rather than rewritten. A settings page that invented options would be a
    /// feature, and this pass is layout and navigation.
    ///
    /// It is a screen rather than a lump of content inside the panel so that it goes through the
    /// same door every other tab does - PanelHost hosts UIScreens, the router owns them, and a
    /// tab that was special-cased would be the one that drifts.
    /// </summary>
    public sealed class SettingsScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Settings; } }

        private const float CardWidth = 1180f;

        private Text _headerSubtitle;
        private RectTransform _header;
        private ScrollRect _scroll;

        private UIButton _soundButton;
        private Text _soundNote;
        private Text _saveNote;

        protected override void Build()
        {
            _header = ScreenChrome.Header(Root, "SETTINGS", Router, out _headerSubtitle);

            RectTransform content = UIBuilder.ScrollList("Content", Root, out _scroll, 16f,
                                                         new RectOffset(0, 0, 8, 48));
            ApplyContentInset();

            BuildSoundCard(content);
            BuildSaveCard(content);
            BuildResetCard(content);
        }

        // ------------------------------------------------------------------ embedding

        /// <summary>
        /// Drops the page header when the panel is already drawing one, and lets the content sit
        /// against the sheet's own edges rather than under a top bar that is not there.
        /// </summary>
        public override void SetEmbedded(bool embedded)
        {
            base.SetEmbedded(embedded);
            if (_header != null) _header.gameObject.SetActive(!embedded);
            ApplyContentInset();
        }

        private void ApplyContentInset()
        {
            if (_scroll == null) return;
            UIBuilder.FillParent(_scroll.GetComponent<RectTransform>(),
                                 UITheme.ScreenPadding,
                                 Embedded ? UITheme.Space16 : UITheme.TopBarHeight + 118f,
                                 UITheme.ScreenPadding, 20f);
        }

        // ------------------------------------------------------------------ cards

        private void BuildSoundCard(RectTransform parent)
        {
            RectTransform card = Card(parent, 200f);
            Heading(card, "SOUND");

            _soundButton = UIBuilder.SecondaryButton("Sound", card, "", HubStyle.FontButton);
            UIBuilder.Box(_soundButton.Root, 30f, -56f, 320f, HubStyle.TouchMin, new Vector2(0f, 1f));
            _soundButton.OnClick(ToggleSound);

            _soundNote = UIBuilder.WrappedLabel("Note", card, "", UITheme.FontTiny, UITheme.TextMuted,
                                                TextAnchor.UpperLeft);
            UIBuilder.Box(_soundNote.rectTransform, 380f, -60f, CardWidth - 440f, 80f, new Vector2(0f, 1f));
        }

        private void BuildSaveCard(RectTransform parent)
        {
            RectTransform card = Card(parent, 190f);
            Heading(card, "YOUR SAVE");

            _saveNote = UIBuilder.WrappedLabel("Note", card, "", UITheme.FontSmall, UITheme.TextPrimary,
                                               TextAnchor.UpperLeft);
            UIBuilder.Box(_saveNote.rectTransform, 30f, -56f, CardWidth - 90f, 110f, new Vector2(0f, 1f));
        }

        /// <summary>
        /// The one destructive control in the game, and it keeps every guard it had: a confirm
        /// dialog that says exactly what is lost, with the destructive answer as the secondary
        /// button rather than the primary one.
        /// </summary>
        private void BuildResetCard(RectTransform parent)
        {
            RectTransform card = Card(parent, 230f);
            Heading(card, "START OVER");

            Text note = UIBuilder.WrappedLabel("Note", card,
                "Wipes your heroes, currency and campaign progress and starts a fresh account. "
                + "There is no way back from this.",
                UITheme.FontTiny, UITheme.TextMuted, TextAnchor.UpperLeft);
            UIBuilder.Box(note.rectTransform, 30f, -56f, CardWidth - 90f, 70f, new Vector2(0f, 1f));

            UIButton reset = UIBuilder.SecondaryButton("Reset", card, "RESET PROGRESS", HubStyle.FontButton);
            UIBuilder.Box(reset.Root, 30f, 24f, 420f, HubStyle.TouchMin, new Vector2(0f, 0f));
            reset.OnClick(ConfirmReset);
        }

        private void ConfirmReset()
        {
            App.Audio.Tap();
            App.Modal.ShowConfirm(
                "RESET PROGRESS",
                "This wipes your heroes, currency and campaign progress and starts a fresh account. " +
                "Useful for testing the first-run experience.",
                "RESET", "KEEP PLAYING",
                App.ResetProgress,
                null);
        }

        // ------------------------------------------------------------------ behaviour

        private void ToggleSound()
        {
            if (App.Audio == null) return;
            App.Audio.Muted = !App.Audio.Muted;
            // Tapped AFTER the flip, so turning sound back on is audible and turning it off is
            // silent - the feedback matches what the switch now says.
            App.Audio.Tap();
            RefreshSound();
        }

        private void RefreshSound()
        {
            bool muted = App.Audio != null && App.Audio.Muted;
            _soundButton.SetText(muted ? "SOUND: OFF" : "SOUND: ON");
            _soundNote.text = muted
                ? "Every sound in the game is silenced, including battle hits and rewards."
                : "Taps, battle hits and rewards play at their usual volume.";
        }

        private void RefreshSave()
        {
            CloudCoordinator cloud = App.Cloud;
            string local = "Your progress saves to this device automatically.";
            if (cloud == null)
            {
                _saveNote.text = local + " This build has no account service, so it stays here.";
                return;
            }
            if (!cloud.SignedIn)
            {
                _saveNote.text = local + " You are offline right now, so nothing has reached the cloud "
                               + "since your last sync. Open the Account tab to try again.";
                return;
            }
            _saveNote.text = local + " It is also backed up to your account, so a new phone can pick it up.";
        }

        public override void OnShow()
        {
            App.RefreshHud();
            _headerSubtitle.text = "";
            RefreshSound();
            RefreshSave();
        }

        // ------------------------------------------------------------------ shared

        private static void Heading(RectTransform card, string text)
        {
            Text label = UIBuilder.Label("Heading", card, text, UITheme.FontTiny, UITheme.TextDim,
                                         TextAnchor.UpperLeft);
            UIBuilder.Box(label.rectTransform, 30f, -22f, 600f, 26f, new Vector2(0f, 1f));
        }

        private static RectTransform Card(RectTransform parent, float height)
        {
            RectTransform card = UIBuilder.Node("Card", parent);
            UIBuilder.Sized(card, CardWidth, height);

            Image bg = card.gameObject.AddComponent<Image>();
            bg.sprite = UISkin.Panel(18);
            bg.type = Image.Type.Sliced;
            bg.color = UITheme.Panel;
            UIBuilder.Frame("Edge", card, UITheme.BorderSoft, 18, 2);
            return card;
        }
    }
}
