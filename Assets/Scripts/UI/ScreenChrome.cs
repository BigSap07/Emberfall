using System;
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
            return ForgedHeader(root, title, router, false, out subtitle);
        }

        /// <summary>
        /// The same header with HOME in the corner disc instead of the chevron. A screen in the
        /// campaign flow takes it: its corner answers "take me to the main menu" in one tap, and
        /// the hardware back key is what steps back one screen.
        /// </summary>
        public static RectTransform ForgedHeader(RectTransform root, string title, ScreenRouter router,
                                                 bool home, out Text subtitle)
        {
            RectTransform header = UIBuilder.Node("Header", root);
            UIBuilder.TopStrip(header, 108f, HeaderTop, UITheme.ScreenPadding);

            ScreenRouter capturedRouter = router;
            ForgeDisc back = home
                ? CornerDisc("Home", header, "HOME", UITheme.FontTiny, capturedRouter.GoHome)
                : CornerDisc("Back", header, "<", UITheme.FontHeading, capturedRouter.Back);
            UIBuilder.Box(back.Root, 0f, 0f, HubStyle.TouchMin, HubStyle.TouchMin, new Vector2(0f, 0.5f));

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

        /// <summary>
        /// The HOME disc for a screen that draws no header (the wave result, the battle): the same
        /// disc, in the same corner, inset the way the header insets its own. It goes to the main
        /// menu and does nothing else, so no screen ever has to ask what leaving costs.
        /// </summary>
        public static ForgeDisc HomeDisc(RectTransform parent, ScreenRouter router)
        {
            return HomeDisc(parent, router, UITheme.ScreenPadding, -12f);
        }

        /// <summary>The same disc for a parent that already carries the screen padding: the
        /// offset is from the parent's own top-left corner.</summary>
        public static ForgeDisc HomeDisc(RectTransform parent, ScreenRouter router, float x, float y)
        {
            ForgeDisc home = CornerDisc("Home", parent, "HOME", UITheme.FontTiny, router.GoHome);
            UIBuilder.Box(home.Root, x, y, HubStyle.TouchMin, HubStyle.TouchMin, new Vector2(0f, 1f));
            return home;
        }

        /// <summary>
        /// The round corner disc both the back and the home control are built from, so the two
        /// cannot drift apart. The label is a Text glyph over the disc rather than a sprite,
        /// because there is no arrow or house in TextureLab and adding one for this caller would
        /// put a shape in the shared generator that only this chrome asks for.
        /// </summary>
        private static ForgeDisc CornerDisc(string name, RectTransform parent, string glyph, int size,
                                            Action action)
        {
            ForgeDisc disc = EmberforgeUI.IconButton(name, parent, "", TextureLab.Sigil(0, 64),
                                                     Emberforge.TextGold, 84f, delegate
            {
                if (Game.GameApp.Instance != null) Game.GameApp.Instance.Audio.Back();
                action();
            });

            disc.Glyph.enabled = false;
            Text label = EmberforgeUI.Title("Label", disc.Face, glyph, size,
                                            Emberforge.TextGold, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(label.rectTransform, 0f);
            UIBuilder.Outlined(label, Emberforge.Outline, Emberforge.StrokeThin);
            label.raycastTarget = false;
            return disc;
        }

        /// <summary>
        /// A "?" disc on the header's right edge, mirroring the back disc on its left: the page's
        /// rules, one tap away rather than printed under its content.
        /// </summary>
        public static ForgeDisc HeaderHelp(RectTransform header, Action action)
        {
            ForgeDisc help = EmberforgeUI.HelpDisc("Help", header, 84f, delegate
            {
                if (Game.GameApp.Instance != null) Game.GameApp.Instance.Audio.Tap();
                action();
            });
            UIBuilder.Box(help.Root, 0f, 0f, HubStyle.TouchMin, HubStyle.TouchMin, new Vector2(1f, 0.5f));
            return help;
        }
    }
}
