using UnityEngine;
using UnityEngine.UI;
using Emberfall.Game.Cloud;
using Emberfall.UI.Summon;

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
        private Image _ground;
        private ScrollRect _scroll;

        private UIButton _soundButton;
        private Text _soundNote;
        private UIButton _fastSummonButton;
        private Text _fastSummonNote;
        private Text _saveNote;
        private Text _resetNote;

        protected override void Build()
        {
            _ground = EmberforgeUI.Ground(Root);
            _header = ScreenChrome.ForgedHeader(Root, "SETTINGS", Router, out _headerSubtitle);

            RectTransform content = UIBuilder.ScrollList("Content", Root, out _scroll, 16f,
                                                         new RectOffset(0, 0, 8, 48));
            ApplyContentInset();

            BuildSoundCard(content);
            BuildSummonCard(content);
            BuildSaveCard(content);
            BuildResetCard(content);
            BuildStampLine(content);
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
            // The ground goes with the header. Inside a panel sheet this page is already sitting
            // on the sheet's own framed surface, and a second ground over it would cover that
            // face with a flat rectangle - see EmberforgeUI.Ground.
            if (_ground != null) _ground.enabled = !embedded;
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

            _soundButton = EmberforgeUI.Button("Sound", card, "", HubStyle.FontButton,
                                               ForgeButtonKind.Secondary);
            UIBuilder.Box(_soundButton.Root, 30f, -56f, 320f, HubStyle.TouchMin, new Vector2(0f, 1f));
            _soundButton.OnClick(ToggleSound);

            _soundNote = EmberforgeUI.Body("Note", card, "", UITheme.FontTiny, Emberforge.TextBody,
                                           TextAnchor.UpperLeft);
            UIBuilder.Box(_soundNote.rectTransform, 380f, -60f, CardWidth - 440f, 80f, new Vector2(0f, 1f));
        }

        /// <summary>
        /// Fast summon. A player sees the summon buildup hundreds of times, and past the first few
        /// dozen the drama is a wait. This cuts the buildup to a fraction of a second and keeps the
        /// reveal whole, because the reveal is the part that still says something new. The light
        /// still commits to the rolled tier's colour, just sooner.
        ///
        /// Stored per device in PlayerPrefs (SummonSettings), not on the save.
        /// </summary>
        private void BuildSummonCard(RectTransform parent)
        {
            RectTransform card = Card(parent, 200f);
            Heading(card, "SUMMONING");

            _fastSummonButton = EmberforgeUI.Button("FastSummon", card, "", HubStyle.FontButton,
                                                    ForgeButtonKind.Secondary);
            UIBuilder.Box(_fastSummonButton.Root, 30f, -56f, 320f, HubStyle.TouchMin, new Vector2(0f, 1f));
            _fastSummonButton.OnClick(ToggleFastSummon);

            _fastSummonNote = EmberforgeUI.Body("Note", card, "", UITheme.FontTiny, Emberforge.TextBody,
                                                TextAnchor.UpperLeft);
            UIBuilder.Box(_fastSummonNote.rectTransform, 380f, -60f, CardWidth - 440f, 80f, new Vector2(0f, 1f));
        }

        private void BuildSaveCard(RectTransform parent)
        {
            RectTransform card = Card(parent, 190f);
            Heading(card, "YOUR SAVE");

            _saveNote = EmberforgeUI.Body("Note", card, "", UITheme.FontSmall, Emberforge.TextBody,
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

            // Filled on show rather than here, because what a reset reaches depends on whether this
            // build has an account service, and the card is built once. See ResetNote.
            _resetNote = EmberforgeUI.Body("Note", card, "", UITheme.FontTiny, Emberforge.TextBody,
                                           TextAnchor.UpperLeft);
            UIBuilder.Box(_resetNote.rectTransform, 30f, -56f, CardWidth - 90f, 70f, new Vector2(0f, 1f));

            // TEAL, and that is the point rather than an oversight. The kit has two voices and
            // ember is the thing to press; the one destructive control in the game is deliberately
            // NOT the thing to press, which is the same reasoning that put the destructive answer
            // on the confirm dialog's secondary button.
            UIButton reset = EmberforgeUI.Button("Reset", card, "RESET PROGRESS", HubStyle.FontButton,
                                                 ForgeButtonKind.Secondary);
            UIBuilder.Box(reset.Root, 30f, 24f, 420f, HubStyle.TouchMin, new Vector2(0f, 0f));
            reset.OnClick(ConfirmReset);
        }

        /// <summary>
        /// Which build this is, as the last line of the page rather than a card: the same words
        /// Game.BuildStamp puts in the Hub's corner, here because the settings page is where a
        /// person looks for them when a report asks. A row of the scroll list like the cards above
        /// it, so it can never sit on top of one, and set in the page's quietest text because
        /// nothing on it can be changed.
        /// </summary>
        private static void BuildStampLine(RectTransform parent)
        {
            RectTransform row = UIBuilder.Node("BuildStamp", parent);
            UIBuilder.Sized(row, CardWidth, 32f);
            Text stamp = EmberforgeUI.Label("Label", row, Game.BuildStamp.Label, UITheme.FontTiny - 2,
                                            Emberforge.TextQuiet, TextAnchor.MiddleLeft);
            UIBuilder.FillParent(stamp.rectTransform, 30f, 0f, 30f, 0f);
            stamp.raycastTarget = false;
        }

        private void ConfirmReset()
        {
            App.Audio.Tap();
            ShowResetConfirm(App, ResetReachesCloud(App));
        }

        /// <summary>
        /// Whether a reset on this device also replaces the cloud save. Docs/CLOUD-GAPS.md P11.
        ///
        /// GameApp.ResetProgress deletes the save, writes a starter profile and leaves the base
        /// revision where it was, so the next sync finds the cloud where this device last left it,
        /// reads the starter profile as descending from it, and uploads it. That is true whenever
        /// the account service exists, signed in or not: an offline device uploads the moment it
        /// is back. Only a build with no account service at all resets this device alone.
        /// </summary>
        public static bool ResetReachesCloud(Game.GameApp app)
        {
            return app != null && app.Cloud != null;
        }

        /// <summary>
        /// The RESET PROGRESS confirm, in the words of what it actually does. It used to say only
        /// that it "starts a fresh account", which was wrong twice: the account is the same one,
        /// and the cloud copy a player may have been counting on goes with it. A player resetting
        /// a device expects to reset that device, so the cloud half is stated in the dialog itself,
        /// before the button, rather than left for them to find out.
        ///
        /// Public and static so the capture tools open exactly this dialog, not a copy of its words.
        /// </summary>
        public static void ShowResetConfirm(Game.GameApp app, bool reachesCloud)
        {
            app.Modal.ShowConfirm("RESET PROGRESS", ResetBody(reachesCloud), "RESET", "KEEP PLAYING",
                                  app.ResetProgress, null);
        }

        public static string ResetBody(bool reachesCloud)
        {
            return reachesCloud
                ? "This wipes your heroes, currency and campaign progress on this device AND in your "
                  + "cloud save. At the next sync the fresh start replaces your cloud copy, so no other "
                  + "device can bring your progress back. Your account stays the same."
                : "This wipes your heroes, currency and campaign progress on this device. "
                  + "There is no way back from this.";
        }

        private static string ResetNote(bool reachesCloud)
        {
            return reachesCloud
                ? "Wipes your heroes, currency and campaign progress, on this device and in your cloud "
                  + "save. There is no way back from this."
                : "Wipes your heroes, currency and campaign progress on this device. "
                  + "There is no way back from this.";
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

        private void ToggleFastSummon()
        {
            SummonSettings.FastSummon = !SummonSettings.FastSummon;
            App.Audio.Tap();
            RefreshFastSummon();
        }

        private void RefreshFastSummon()
        {
            bool fast = SummonSettings.FastSummon;
            _fastSummonButton.SetText(fast ? "FAST SUMMON: ON" : "FAST SUMMON: OFF");
            _fastSummonNote.text = fast
                ? "The build-up before each summon is cut short. The colour still tells you the rarity first."
                : "Each summon builds up in full before the hero is shown. Tap to skip it, or hold to skip them all.";
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
            RefreshFastSummon();
            RefreshSave();
            _resetNote.text = ResetNote(ResetReachesCloud(App));
        }

        // ------------------------------------------------------------------ shared

        /// <summary>
        /// A section heading, and deliberately not a ribbon: this page is a column of three cards
        /// and a ribbon laid across each of them would be three banners in a stack. See the note
        /// on EmberforgeUI.Heading.
        /// </summary>
        private static void Heading(RectTransform card, string text)
        {
            EmberforgeUI.Heading("Heading", card, text, 30f, -22f);
        }

        private static RectTransform Card(RectTransform parent, float height)
        {
            RectTransform card = UIBuilder.Node("Card", parent);
            UIBuilder.Sized(card, CardWidth, height);
            EmberforgeUI.PanelOn(card, false);
            return card;
        }
    }
}
