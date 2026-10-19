using UnityEngine;
using UnityEngine.UI;

namespace Emberfall.UI
{
    /// <summary>
    /// THE BOOT SPLASH (R21): the key art shown over the Hub for a moment when the app starts.
    ///
    /// Behind the UI kit switch and nowhere else. UiKit.Splash answers null while UiKitSwitch is off, and
    /// null here means "draw nothing", so with the switch off no splash object is built, no texture is
    /// loaded and the first frame is the Hub exactly as it was. It adds no screen to the router and reads
    /// nothing from a save.
    ///
    /// The Hub is already up underneath: GameApp builds this last, after the first screen, the cloud
    /// start and the onboarding coach, so the splash covers a finished screen rather than hiding work.
    /// It holds for HoldSeconds, fades out over FadeSeconds, and a tap ends it early. The art is
    /// envelope-fitted like the Hub's own map (cropped by the screen, never stretched).
    ///
    /// Editor capture runs set GameApp.SuppressFirstRunKey, and the splash stays out of them so no
    /// Hub shot is taken under it. Screen:Splash+kit builds it with ForCapture, which holds forever.
    /// </summary>
    public static class UiKitSplash
    {
        public const float HoldSeconds = 1.6f;
        public const float FadeSeconds = 0.4f;

        /// <summary>Opacity at a time since the splash appeared: 1 while held, then a linear fade to 0.
        /// A pure function, so the timing is a number and not a feeling.</summary>
        public static float Alpha(float elapsed)
        {
            if (elapsed <= HoldSeconds) return 1f;
            float k = (elapsed - HoldSeconds) / FadeSeconds;
            return k >= 1f ? 0f : 1f - k;
        }

        /// <summary>Builds the splash over layer, or returns null when the kit is off or has no splash art.
        /// forCapture holds it until something else removes it.</summary>
        public static GameObject Show(RectTransform layer, bool forCapture)
        {
            Sprite art = UiKit.Splash();
            if (art == null || layer == null) return null;

            RectTransform root = UIBuilder.Node("Splash", layer);
            UIBuilder.FillParent(root, 0f);
            Image floor = root.gameObject.AddComponent<Image>();
            floor.sprite = TextureLab.White;
            floor.color = Color.black;
            // A control as well as a picture: a tap ends it, and no tap reaches the Hub through it.
            Button skip = root.gameObject.AddComponent<Button>();
            skip.targetGraphic = floor;
            skip.transition = Selectable.Transition.None;

            RectTransform frame = UIBuilder.Node("Art", root);
            frame.anchorMin = new Vector2(0.5f, 0.5f);
            frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.anchoredPosition = Vector2.zero;
            Image picture = UIBuilder.Img("Picture", frame, art, Color.white);
            picture.raycastTarget = false;
            UIBuilder.FillParent(picture.rectTransform, 0f);
            AspectRatioFitter fitter = frame.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = art.rect.width / art.rect.height;

            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            root.SetAsLastSibling();

            if (!forCapture)
            {
                UiKitSplashRunner runner = root.gameObject.AddComponent<UiKitSplashRunner>();
                runner.Group = group;
                skip.onClick.AddListener(runner.End);
            }
            return root.gameObject;
        }
    }

    /// <summary>Drives one splash: reads the clock, sets the opacity, removes the object when it is done.
    /// Unscaled time, so a paused or slowed game still shows it for the same wall time.</summary>
    public sealed class UiKitSplashRunner : MonoBehaviour
    {
        public CanvasGroup Group;
        private float _elapsed;
        private bool _ending;

        public void End()
        {
            if (_ending) return;
            _ending = true;
            Destroy(gameObject);
        }

        private void Update()
        {
            if (_ending) return;
            _elapsed += Time.unscaledDeltaTime;
            float a = UiKitSplash.Alpha(_elapsed);
            if (Group != null) Group.alpha = a;
            if (a <= 0f) End();
        }
    }
}
