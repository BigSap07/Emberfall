using System;
using System.Collections.Generic;
using UnityEngine;

namespace Emberfall.UI
{
    /// <summary>
    /// The seam painted UI art drops into.
    ///
    /// Every UI surface in the project is generated at runtime by TextureLab: rounded panels,
    /// frames, circles, rings, glows, hatches, sigils. That is the right default (the project needs
    /// zero asset imports to run) but it means roughly 60 call sites name a GENERATOR rather than a
    /// SURFACE. When the designer delivers painted art, swapping any one of those surfaces would
    /// otherwise mean editing every site that draws it.
    ///
    /// UISkin puts a name in between. A call asks for "the panel surface" and gets a painted sprite
    /// if one exists at Resources/UI/{name}.png, or the generated one if it does not.
    ///
    /// This deliberately mirrors BattleAssets.BackdropTexture rather than inventing a convention:
    ///
    ///   - Same lookup shape. BackdropTexture loads Resources/Backgrounds/{environmentId}; this
    ///     loads Resources/UI/{name}. Adding art is a file drop, not a code change.
    ///   - Same fallback POLICY, and the distinction matters. BattleAssets.UnitSprite treats a
    ///     missing file as a content bug and returns magenta, because a unit that names a sprite
    ///     must have one. BackdropTexture treats it as the ordinary case and falls through
    ///     silently, because most environments have no painting yet. UI is the second case: today
    ///     NOTHING is painted, so a missing file is normal and must never be loud.
    ///   - Same cache discipline. Painted and generated results are cached on one key, so
    ///     downstream code cannot tell which path it got and behaves identically either way.
    ///
    /// **Until art is added this class is inert by construction.** Resources/UI does not exist, so
    /// every Resources.Load returns null, so every call returns exactly the TextureLab sprite the
    /// call site used before. That is the point of landing the seam now: it can be proven to change
    /// nothing today, rather than being invented under pressure on the day the art arrives.
    ///
    /// ---------------------------------------------------------------------------------------
    /// FOR WHOEVER DELIVERS THE ART
    ///
    /// Drop a PNG at Assets/Resources/UI/{name}.png and it is used automatically. No code change,
    /// no registration step, no rebuild of anything but Unity's own import. The names currently
    /// requested are listed in Surface below.
    ///
    /// Nine-slice: a painted panel or button MUST have its border set in Unity's sprite importer
    /// (Sprite Editor, the green border handles). The generated fallbacks set their own borders in
    /// code, so a painted sprite with no border will stretch its corners where the generated one
    /// did not, and it will look wrong at a size the mockup did not show. This is the single
    /// easiest thing to get wrong here.
    ///
    /// Tint: every surface is drawn through Image.color using a UITheme palette colour, so paint
    /// them WHITE or greyscale unless the art is genuinely meant to override the palette. A
    /// coloured painting multiplied by a coloured tint gives neither.
    /// ---------------------------------------------------------------------------------------
    /// </summary>
    public static class UISkin
    {
        /// <summary>
        /// The surface names this project asks for, and so the filenames the art can use.
        /// A name here is a REQUEST, not a promise that a file exists.
        ///
        /// Deliberately short. A name is only listed once something actually draws through it, so
        /// this list is a true statement about what the project asks for rather than a wishlist.
        /// The decorative generators (Glow, Hatch, Sigil, VerticalFade) and the many per-widget
        /// rings and circles are still called directly on TextureLab, because they are used per
        /// screen rather than through shared infrastructure, and routing them would have meant
        /// touching screens rather than the builder. Adding one later is two lines here plus the
        /// call site, and the first person to need it should do exactly that.
        /// </summary>
        public static class Surface
        {
            public const string Panel = "panel";
            public const string Button = "button";
            public const string Disc = "disc";

            /// <summary>
            /// Every rounded outline in the project, on panels AND on buttons: UIBuilder.Frame is
            /// shared by both, so painting panel_frame.png restyles button edges too. Split this
            /// into a second surface if they should ever diverge; today they genuinely are one
            /// thing and naming them separately would imply a distinction that does not exist.
            /// </summary>
            public const string PanelFrame = "panel_frame";
        }

        private const string ResourceFolder = "UI/";

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>
        /// Painted sprite for this surface if one exists, otherwise whatever the generator returns.
        ///
        /// The generator is a delegate rather than an already-built Sprite so that TextureLab does
        /// no work when a painted file wins. Both outcomes are cached on the same key, which is the
        /// variant key rather than the bare name: one surface can have several sizes or radii live
        /// at once (the project uses at least three panel radii), and they must not collide.
        /// </summary>
        public static Sprite Get(string surface, string variantKey, Func<Sprite> generate)
        {
            string key = surface + "#" + variantKey;
            Sprite cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            // One load attempt per key, not per call: a miss is cached as the generated sprite, so
            // a project with no painted art does not hit Resources.Load on every panel it builds.
            Sprite painted = Resources.Load<Sprite>(ResourceFolder + surface);
            Sprite result = painted != null ? painted : generate();

            Cache[key] = result;
            return result;
        }

        // ------------------------------------------------------------------ named surfaces
        //
        // Thin wrappers so call sites read as a surface rather than as a lookup. Each one states
        // the generator it falls back to, which is the same call the site made before UISkin
        // existed - that equivalence is what makes this migration a no-op.

        /// <summary>Filled rounded panel. Falls back to TextureLab.RoundedRect.</summary>
        public static Sprite Panel(int radius)
        {
            int r = radius;
            return Get(Surface.Panel, r.ToString(), delegate { return TextureLab.RoundedRect(r); });
        }

        /// <summary>Rounded outline framing a panel. Falls back to TextureLab.RoundedOutline.</summary>
        public static Sprite PanelFrame(int radius, int thickness)
        {
            int r = radius;
            int t = thickness;
            return Get(Surface.PanelFrame, r + "_" + t, delegate { return TextureLab.RoundedOutline(r, t); });
        }

        /// <summary>A button's fill. Falls back to TextureLab.RoundedRect, same as Panel does today.</summary>
        public static Sprite Button(int radius)
        {
            int r = radius;
            return Get(Surface.Button, r.ToString(), delegate { return TextureLab.RoundedRect(r); });
        }

        /// <summary>Solid circle, used for portraits and icon buttons. Falls back to TextureLab.Circle.</summary>
        public static Sprite Disc(int size)
        {
            int s = size;
            return Get(Surface.Disc, s.ToString(), delegate { return TextureLab.Circle(s); });
        }

        /// <summary>
        /// Drops every cached lookup. Only needed if art is added or replaced while the game is
        /// running; a normal launch resolves each surface once and keeps it.
        /// </summary>
        public static void ClearCache()
        {
            Cache.Clear();
        }
    }
}
