using UnityEngine;

namespace Emberfall.UI
{
    /// <summary>
    /// Holds a RectTransform inside Screen.safeArea, so nothing in the HUD frame lands under a
    /// notch, a punch-hole camera or a gesture bar.
    ///
    /// It works in ANCHOR space rather than by setting offsets, which matters: the parent of one
    /// of these is a full-bleed layer whose own size follows the canvas, and expressing the inset
    /// as a fraction of the screen means a resolution change needs no second pass. The rect's own
    /// offsets are zeroed, so a child anchored to "the top right corner" is anchored to the top
    /// right corner OF THE SAFE AREA, and every element built inside one is safe by construction
    /// rather than by each element remembering to subtract something.
    ///
    /// WHAT IS DELIBERATELY NOT INSIDE ONE: the map. Layer 0 is full-bleed art and should run
    /// under the notch - insetting it would put black bars down the sides of a painting on a
    /// device that has no bars. The frame, the panels and the popups are inside; the painting is
    /// not.
    ///
    /// Applied every frame, but only written when the numbers actually moved. Screen.safeArea
    /// changes on rotation, on a resolution change, and in the editor whenever the Game view is
    /// resized, and Unity raises no event for it, so a cheap poll is the honest implementation.
    /// The comparison is against the last APPLIED rect, so a still screen costs a few compares.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SafeAreaRect : MonoBehaviour
    {
#if UNITY_EDITOR
        /// <summary>
        /// A pretend safe area, for capturing what a notched phone looks like without owning one.
        ///
        /// Editor only, and off unless something sets it: a width or height of zero means "use the
        /// real Screen.safeArea", which is what every run that is not a capture does. The capture
        /// tool sets it as Play Mode starts and clears it when the run ends - see HubVerification -
        /// so a simulated notch cannot survive into a build or into an ordinary editor session.
        /// Expressed in PIXELS, exactly like the real thing, so one piece of arithmetic below
        /// serves both.
        /// </summary>
        public static Rect Simulated = new Rect(0f, 0f, 0f, 0f);
#endif

        private Rect _applied = new Rect(-1f, -1f, -1f, -1f);
        private int _appliedWidth = -1;
        private int _appliedHeight = -1;

        private RectTransform _rect;

        /// <summary>The safe area this frame, in pixels: the real one, or the simulated one when a
        /// capture has asked for a notch.</summary>
        public static Rect Current
        {
            get
            {
#if UNITY_EDITOR
                if (Simulated.width > 0f && Simulated.height > 0f) return Simulated;
#endif
                return Screen.safeArea;
            }
        }

        /// <summary>Adds the fitter to a rect and applies it immediately, so the first frame a
        /// screen draws is already inset rather than snapping into place one frame later.</summary>
        public static SafeAreaRect Attach(RectTransform rect)
        {
            SafeAreaRect fitter = rect.gameObject.AddComponent<SafeAreaRect>();
            fitter.Apply();
            return fitter;
        }

        private void Awake()
        {
            Apply();
        }

        private void Update()
        {
            Apply();
        }

        public void Apply()
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();
            if (_rect == null) return;

            Rect area = Current;
            int width = Screen.width;
            int height = Screen.height;
            if (width <= 0 || height <= 0) return;
            if (area.x == _applied.x && area.y == _applied.y
                && area.width == _applied.width && area.height == _applied.height
                && width == _appliedWidth && height == _appliedHeight) return;

            _applied = area;
            _appliedWidth = width;
            _appliedHeight = height;

            Vector2 min = new Vector2(area.x / width, area.y / height);
            Vector2 max = new Vector2((area.x + area.width) / width, (area.y + area.height) / height);

            // Clamped rather than trusted: this value comes from the OS, and a safe area reported
            // as larger than the screen or inverted would push the whole frame off the canvas.
            min.x = Mathf.Clamp01(min.x);
            min.y = Mathf.Clamp01(min.y);
            max.x = Mathf.Clamp01(max.x);
            max.y = Mathf.Clamp01(max.y);
            if (max.x <= min.x || max.y <= min.y) { min = Vector2.zero; max = Vector2.one; }

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
