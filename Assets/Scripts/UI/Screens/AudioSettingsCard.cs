using UnityEngine;
using UnityEngine.UI;
using Emberfall.Game;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// The SOUND card of the settings page: a mute switch and three volume sliders (music, sound
    /// effects, interface), saved on the device through AudioPrefs and never in the save.
    ///
    /// It decides nothing. A slider writes one number to AudioPrefs and the audio service reads it
    /// on the next frame; the mute switch flips the one flag Sfx.Muted already was. The two
    /// preview sounds (a hit for the effects slider, a tap for the interface slider) are cues
    /// like any other, played through Sfx, and are throttled so a drag is a tick rather than a
    /// roar.
    /// </summary>
    internal sealed class AudioSettingsCard
    {
        public const float Height = 470f;

        private const float RowHeight = 84f;
        private const float HandleSize = 60f;
        private const float PreviewGap = 0.18f;

        private readonly GameApp _app;
        private UIButton _muteButton;
        private Text _muteNote;
        private Slider _music;
        private Slider _sfx;
        private Slider _ui;
        private Text _musicValue;
        private Text _sfxValue;
        private Text _uiValue;
        private bool _refreshing;
        private float _lastPreview;

        public AudioSettingsCard(GameApp app)
        {
            _app = app;
        }

        public void Build(RectTransform card, float cardWidth)
        {
            EmberforgeUI.Heading("Heading", card, "SOUND", 30f, -22f);

            _muteButton = EmberforgeUI.Button("Mute", card, "", HubStyle.FontButton, ForgeButtonKind.Secondary);
            UIBuilder.Box(_muteButton.Root, 30f, -56f, 320f, HubStyle.TouchMin, new Vector2(0f, 1f));
            _muteButton.OnClick(ToggleMute);

            _muteNote = EmberforgeUI.Body("Note", card, "", UITheme.FontTiny, Emberforge.TextBody,
                                          TextAnchor.UpperLeft);
            UIBuilder.Box(_muteNote.rectTransform, 380f, -60f, cardWidth - 440f, 100f, new Vector2(0f, 1f));

            float top = -56f - HubStyle.TouchMin - 16f;
            _music = Row(card, cardWidth, top, "MUSIC", out _musicValue, OnMusic);
            _sfx = Row(card, cardWidth, top - RowHeight, "EFFECTS", out _sfxValue, OnSfx);
            _ui = Row(card, cardWidth, top - RowHeight * 2f, "INTERFACE", out _uiValue, OnUi);
        }

        public void Refresh()
        {
            _refreshing = true;
            _music.value = AudioPrefs.MusicPercent;
            _sfx.value = AudioPrefs.SfxPercent;
            _ui.value = AudioPrefs.UiPercent;
            _refreshing = false;
            _musicValue.text = AudioPrefs.MusicPercent + "%";
            _sfxValue.text = AudioPrefs.SfxPercent + "%";
            _uiValue.text = AudioPrefs.UiPercent + "%";

            bool muted = AudioPrefs.Muted;
            _muteButton.SetText(muted ? "SOUND: OFF" : "SOUND: ON");
            bool real = _app.Audio != null && _app.Audio.RealAudio;
            if (muted) _muteNote.text = "Every sound in the game is silenced, including battle hits and rewards.";
            else if (real) _muteNote.text = "Drag a slider to set how loud that kind of sound is.";
            else _muteNote.text = "This build is playing the original tones, which the sliders do not change.";
        }

        /// <summary>Writes pending values to disk. Called when the page is left, not on every step
        /// of a drag.</summary>
        public void Flush()
        {
            AudioPrefs.Flush();
        }

        // ------------------------------------------------------------------ behaviour

        private void ToggleMute()
        {
            if (_app.Audio == null) return;
            _app.Audio.Muted = !_app.Audio.Muted;
            // Tapped AFTER the flip, so turning sound back on is audible and turning it off is
            // silent - the feedback matches what the switch now says.
            _app.Audio.Tap();
            Refresh();
        }

        private void OnMusic(float value)
        {
            if (_refreshing) return;
            AudioPrefs.MusicPercent = Mathf.RoundToInt(value);
            _musicValue.text = AudioPrefs.MusicPercent + "%";
        }

        private void OnSfx(float value)
        {
            if (_refreshing) return;
            AudioPrefs.SfxPercent = Mathf.RoundToInt(value);
            _sfxValue.text = AudioPrefs.SfxPercent + "%";
            Preview(false);
        }

        private void OnUi(float value)
        {
            if (_refreshing) return;
            AudioPrefs.UiPercent = Mathf.RoundToInt(value);
            _uiValue.text = AudioPrefs.UiPercent + "%";
            Preview(true);
        }

        private void Preview(bool ui)
        {
            if (_app.Audio == null || Time.unscaledTime - _lastPreview < PreviewGap) return;
            _lastPreview = Time.unscaledTime;
            if (ui) _app.Audio.Tap();
            else _app.Audio.Hit();
        }

        // ------------------------------------------------------------------ layout

        private static Slider Row(RectTransform card, float cardWidth, float y, string label, out Text valueText,
                                  UnityEngine.Events.UnityAction<float> onChanged)
        {
            RectTransform row = UIBuilder.Node("Row" + label, card);
            UIBuilder.Box(row, 0f, y, cardWidth, RowHeight, new Vector2(0f, 1f));

            Text name = EmberforgeUI.Label("Label", row, label, UITheme.FontSmall, Emberforge.TextGold,
                                           TextAnchor.MiddleLeft);
            UIBuilder.Box(name.rectTransform, 30f, 0f, 200f, RowHeight, new Vector2(0f, 0.5f));

            const float sliderX = 250f;
            float sliderWidth = cardWidth - sliderX - 170f;
            Slider slider = MakeSlider(row, sliderX, sliderWidth);
            slider.onValueChanged.AddListener(onChanged);

            valueText = EmberforgeUI.Label("Value", row, "", UITheme.FontSmall, Emberforge.TextBody,
                                           TextAnchor.MiddleRight);
            UIBuilder.Box(valueText.rectTransform, -30f, 0f, 110f, RowHeight, new Vector2(1f, 0.5f));
            return slider;
        }

        /// <summary>
        /// A sunken track, a teal fill (ember orange is for the one thing to press) and a gold handle, with a transparent full-height hit
        /// area behind them so the touch target is the row, not the thin track.
        ///
        /// THE SLIDER OWNS THE ANCHORS OF ITS FILL AND HANDLE. Unity's Slider resets both rects'
        /// anchors to the full height of their parent and moves only the horizontal pair, so a fill
        /// given its own thin offsets inside a tall parent came out as a block the height of the
        /// row (the first capture showed it). The fix is to make the PARENTS the right height:
        /// the fill area is exactly the track's thickness, the handle area exactly the handle's,
        /// and the fill and the handle stretch to fill them with no offsets of their own.
        /// </summary>
        private static Slider MakeSlider(RectTransform parent, float x, float width)
        {
            RectTransform root = UIBuilder.Node("Slider", parent);
            UIBuilder.Box(root, x, 0f, width, RowHeight, new Vector2(0f, 0.5f));

            Image hit = root.gameObject.AddComponent<Image>();
            hit.sprite = TextureLab.White;
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            const float trackHeight = 18f;
            Image track = UIBuilder.Panel("Track", root, Emberforge.SunkenMid, 0);
            track.raycastTarget = false;
            Band(track.rectTransform, trackHeight, 0f);

            RectTransform fillArea = UIBuilder.Node("FillArea", root);
            Band(fillArea, trackHeight, HandleSize * 0.5f);
            Image fill = UIBuilder.Panel("Fill", fillArea, Emberforge.SecondaryMid, 0);
            fill.raycastTarget = false;
            RectTransform fr = fill.rectTransform;
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            fr.offsetMin = Vector2.zero;
            fr.offsetMax = Vector2.zero;

            RectTransform handleArea = UIBuilder.Node("HandleArea", root);
            Band(handleArea, HandleSize, HandleSize * 0.5f);
            Image handle = UIBuilder.Img("Handle", handleArea, TextureLab.Circle(64), Emberforge.GoldMid);
            handle.raycastTarget = true;
            RectTransform hr = handle.rectTransform;
            hr.anchorMin = Vector2.zero;
            hr.anchorMax = new Vector2(0f, 1f);
            hr.pivot = new Vector2(0.5f, 0.5f);
            hr.offsetMin = Vector2.zero;
            hr.offsetMax = Vector2.zero;
            hr.sizeDelta = new Vector2(HandleSize, 0f);

            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fr;
            slider.handleRect = hr;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.value = 0f;
            root.gameObject.AddComponent<SliderRelease>();
            return slider;
        }

        /// <summary>A horizontal band across the middle of its parent: full width less the side
        /// inset, exactly this tall.</summary>
        private static void Band(RectTransform rt, float height, float sideInset)
        {
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(sideInset, -height * 0.5f);
            rt.offsetMax = new Vector2(-sideInset, height * 0.5f);
        }
    }

    /// <summary>Writes the volumes to disk when a slider is let go, so a drag costs one disk write and
    /// a kill right after it loses nothing. The settings page flushes again when it is left.</summary>
    internal sealed class SliderRelease : MonoBehaviour, UnityEngine.EventSystems.IPointerUpHandler
    {
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData)
        {
            AudioPrefs.Flush();
        }
    }
}
