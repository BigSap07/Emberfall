using UnityEngine;
using UnityEngine.UI;

namespace Emberfall.UI
{
    /// <summary>Shared page header: back arrow, title, and a subtitle line the screen fills in.</summary>
    public static class ScreenChrome
    {
        public const float HeaderTop = UITheme.TopBarHeight + 12f;

        public static RectTransform Header(RectTransform root, string title, ScreenRouter router, out Text subtitle)
        {
            RectTransform header = UIBuilder.Node("Header", root);
            UIBuilder.TopStrip(header, 108f, HeaderTop, UITheme.ScreenPadding);

            UIButton back = UIBuilder.IconButton("Back", header, "<", 76f,
                                                 UITheme.WithAlpha(UITheme.PanelRaised, 0.95f), UITheme.TextPrimary);
            UIBuilder.Box(back.Root, 0f, 0f, 76f, 76f, new Vector2(0f, 0.5f));
            ScreenRouter capturedRouter = router;
            back.OnClick(delegate
            {
                if (Game.GameApp.Instance != null) Game.GameApp.Instance.Audio.Tap();
                capturedRouter.Back();
            });

            Text titleLabel = UIBuilder.Label("Title", header, title, UITheme.FontTitle, UITheme.TextPrimary, TextAnchor.UpperCenter);
            UIBuilder.Box(titleLabel.rectTransform, 0f, 0f, 760f, 52f, new Vector2(0.5f, 1f));
            UIBuilder.Shadowed(titleLabel, new Color(0f, 0f, 0f, 0.55f), 3f);

            subtitle = UIBuilder.Label("Subtitle", header, "", UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperCenter);
            UIBuilder.Box(subtitle.rectTransform, 0f, -54f, 860f, 30f, new Vector2(0.5f, 1f));

            return header;
        }
    }
}
