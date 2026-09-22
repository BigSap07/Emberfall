using UnityEngine;
using UnityEngine.UI;

namespace Emberfall.UI
{
    /// <summary>
    /// Shared page header: a title ribbon across the top of the page, a round back disc beside
    /// it, and a subtitle line under both which the screen fills in.
    ///
    /// THERE IS ONE HEADER AGAIN. There were two for the length of the Emberforge conversion -
    /// the original, and an opt-in Emberforge one screens moved to a page at a time - and the
    /// original's own note said it retires when the last caller has moved. Every screen in the
    /// game calls ForgedHeader now, so it has.
    /// </summary>
    public static class ScreenChrome
    {
        public const float HeaderTop = UITheme.TopBarHeight + 12f;

        /// <summary>
        /// THE SAME HEADER IN EMBERFORGE: a title ribbon across the top of the page and a round
        /// back disc beside it.
        ///
        /// Same rect, same height, same HeaderTop and the same subtitle line underneath as the
        /// header it replaced, and the subtitle still comes back through an out parameter - which
        /// is what let a screen convert by changing which of the two it called and nothing else.
        /// Every metric a page measures its body against is unchanged, and that is why converting
        /// twenty screens did not also move everything that happened to sit under their headers.
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
