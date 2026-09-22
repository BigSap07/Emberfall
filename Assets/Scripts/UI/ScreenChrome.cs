using UnityEngine;
using UnityEngine.UI;

namespace Emberfall.UI
{
    /// <summary>Shared page header: back arrow, title, and a subtitle line the screen fills in.</summary>
    public static class ScreenChrome
    {
        public const float HeaderTop = UITheme.TopBarHeight + 12f;

        /// <summary>
        /// The header sixteen screens still use, unchanged.
        ///
        /// It is left exactly as it was on purpose. Restyling it would have restyled every page in
        /// the game in one edit, including the fifteen that have not had a visual pass and whose
        /// content is still measured against this header's metrics - a gold ribbon over a screen
        /// whose body is still in the old language is worse than either language on its own. A
        /// screen converts by calling ForgedHeader below, one at a time, and this method retires
        /// when the last caller has moved.
        /// </summary>
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
            UIBuilder.Shadowed(titleLabel, UITheme.InkPanel, 3f);

            subtitle = UIBuilder.Label("Subtitle", header, "", UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperCenter);
            UIBuilder.Box(subtitle.rectTransform, 0f, -54f, 860f, 30f, new Vector2(0.5f, 1f));

            return header;
        }

        /// <summary>
        /// THE SAME HEADER IN EMBERFORGE: a title ribbon across the top of the page and a round
        /// back disc beside it.
        ///
        /// Same rect, same height, same HeaderTop, same subtitle line underneath, and the subtitle
        /// still comes back through an out parameter - so a screen converts by changing which of
        /// these two it calls and nothing else. Every metric a page measures its body against is
        /// unchanged, which is what keeps a conversion to one screen rather than to a screen and
        /// everything that happened to sit under its header.
        ///
        /// The back control is a DISC rather than a chunky button. A rectangular ember button in
        /// the corner of every page would be a second primary action competing with whatever the
        /// page's own is, and back is never the thing a screen wants pressed.
        /// </summary>
        public static RectTransform ForgedHeader(RectTransform root, string title, ScreenRouter router,
                                                 out Text subtitle)
        {
            RectTransform header = UIBuilder.Node("Header", root);
            UIBuilder.TopStrip(header, 108f, HeaderTop, UITheme.ScreenPadding);

            ScreenRouter capturedRouter = router;
            ForgeDisc back = EmberforgeUI.IconButton("Back", header, "", TextureLab.Sigil(0, 64),
                                                     Emberforge.TextGold, 84f, delegate
            {
                if (Game.GameApp.Instance != null) Game.GameApp.Instance.Audio.Tap();
                capturedRouter.Back();
            });
            UIBuilder.Box(back.Root, 0f, 0f, HubStyle.TouchMin, HubStyle.TouchMin, new Vector2(0f, 0.5f));

            // The chevron is a Text glyph over the disc rather than a sprite, because there is no
            // arrow in TextureLab and adding one for a single caller would put a shape in the
            // shared generator that only this header ever asks for.
            back.Glyph.enabled = false;
            Text chevron = EmberforgeUI.Title("Chevron", back.Face, "<", UITheme.FontHeading,
                                              Emberforge.TextGold, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(chevron.rectTransform, 0f);
            UIBuilder.Outlined(chevron, Emberforge.Outline, Emberforge.StrokeThin);
            chevron.raycastTarget = false;

            Text titleLabel = EmberforgeUI.TitleRibbon("Title", header, title, 640f);
            UIBuilder.Box(titleLabel.rectTransform.parent as RectTransform, 0f, 0f,
                          640f + Emberforge.RibbonOverhang * 2f, Emberforge.RibbonHeight,
                          new Vector2(0.5f, 1f));

            subtitle = EmberforgeUI.Label("Subtitle", header, "", UITheme.FontTiny,
                                          Emberforge.TextGold, TextAnchor.UpperCenter);
            UIBuilder.Box(subtitle.rectTransform, 0f, -(Emberforge.RibbonHeight + Emberforge.Space8),
                          860f, 30f, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(subtitle, Emberforge.Outline, Emberforge.StrokeThin);

            return header;
        }
    }
}
