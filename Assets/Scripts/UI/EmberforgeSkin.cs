using System.Collections.Generic;
using UnityEngine;

namespace Emberfall.UI
{
    /// <summary>
    /// WHERE AN EMBERFORGE SURFACE COMES FROM, and the one place that decides.
    ///
    /// Every component in EmberforgeUI asks this class for a NAMED SLOT and draws whatever comes
    /// back. A slot resolves in three steps, in this order:
    ///
    ///   1. Resources/Sprites/Hud/&lt;slot&gt;.png   - HAND-PAINTED ART. If a file is there, that is
    ///      the art, full stop. This is the same folder HudSkin already reads, so an artist has
    ///      one place to drop PNGs for the whole game rather than one per subsystem.
    ///   2. Resources/Sprites/Emberforge/&lt;slot&gt;.png - THE BAKED KIT. Written by EmberforgeBaker
    ///      from the tokens in Emberforge, committed, and what the game actually ships with.
    ///   3. EmberforgeArt.Generate  - the same drawing code, run on the device. Only reachable in
    ///      a tree where the baker has never run.
    ///
    /// So a re-skin is a folder of PNGs and no code change, exactly as HudSkin's contract already
    /// promised, and a retune of the kit is editing Emberforge and re-running the baker.
    ///
    /// ---------------------------------------------------------------------------------------
    /// WHY BAKED SPRITES RATHER THAN A UI SHADER
    ///
    /// The two ways to get a gradient, a bevel, a gloss and a rounded outline onto a uGUI element
    /// are to bake them into a nine-sliced texture at import, or to compute them per fragment in a
    /// custom UI shader. This kit is baked, and the reason is the phone rather than the desktop.
    ///
    ///   BATCHING. uGUI batches by material and texture. A shader that draws a gradient needs its
    ///   stops, its radius, its lip height and its gloss fraction as per-element parameters, and
    ///   CanvasRenderer does not honour MaterialPropertyBlock - so per-element parameters mean a
    ///   material instance per element, and every button, panel and pill becomes its own draw call
    ///   plus its own material. The Hub's frame alone is around forty Images. Baked sprites all
    ///   share the one default UI material and pack into one atlas, so the same frame collapses to
    ///   a handful of batches. On a mid-range Android this is the binding constraint, not fill.
    ///
    ///   FRAGMENT COST WHERE IT HURTS MOST. The largest elements are the ones a shader would be
    ///   most expensive on: a panel sheet or a popup plate covers a large share of a 2400x1080
    ///   screen, and the UI already overdraws it (map, scrim, plate, contents). A signed-distance
    ///   rounded rect with a four-stop metal ramp, an exponential inner shadow and a gloss is tens
    ///   of ALU and several branches per fragment; a nine-sliced sprite is one bilinear sample.
    ///
    ///   STARTUP COST DISAPPEARS RATHER THAN MOVING. The generator in EmberforgeArt is the same
    ///   code either way, but baked it runs on a desktop at import, and unbaked it runs on the
    ///   device at load - SetPixels32 and Apply on twenty-seven textures, on the main thread,
    ///   allocating. Baking takes that off the device entirely.
    ///
    ///   THE OVERRIDE CONTRACT STAYS HONEST. A painted PNG from an artist and a generated PNG go
    ///   through the same importer, carry their nine-slice border the same way, and are read by
    ///   the same Resources.Load. With a shader the generated path would be a material and the
    ///   painted path a sprite, which is two code paths and one of them untested.
    ///
    /// What baking costs: the kit is a committed binary, and changing a token means running a menu
    /// item rather than pressing play. That is the trade, and it is the right one for a game whose
    /// chrome changes once a month and whose frame rate matters every frame.
    /// ---------------------------------------------------------------------------------------
    /// </summary>
    public static class EmberforgeSkin
    {
        /// <summary>Where a hand-painted override lives. Deliberately HudSkin's folder: one place
        /// for art, not one per subsystem.</summary>
        public const string PaintedFolder = HudSkin.Folder;

        /// <summary>Where EmberforgeBaker writes the generated kit.</summary>
        public const string BakedFolder = "Sprites/Emberforge/";

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>
        /// Slots already reported on, so the log below is one line per surface rather than one per
        /// element built. The same discipline UISkin arrived at, and for the same reason: with
        /// three possible sources, silence cannot be told apart from "this code never ran".
        /// </summary>
        private static readonly HashSet<string> Reported = new HashSet<string>();

        /// <summary>How many slots fell through to the runtime generator this session. Reported
        /// once, as a single summary line, because twenty-seven separate warnings about an unbaked
        /// project is a wall rather than a message.</summary>
        private static int _generatedCount;
        private static bool _generatedReported;

        public static Sprite Get(string slot)
        {
            // THE UI KIT FIRST, while its switch is on and the map has a piece for this slot. UiKit
            // answers null otherwise, which falls straight through to the three steps below exactly as
            // before; the kit's sprite is never put in Cache, so the switch cannot leak between states.
            Sprite kit = UiKit.Forge(slot);
            if (kit != null) return kit;

            Sprite cached;
            if (Cache.TryGetValue(slot, out cached)) return cached;

            Sprite painted = Resources.Load<Sprite>(PaintedFolder + slot);
            if (painted != null)
            {
                Report(slot, "PAINTED", PaintedFolder + slot, painted);
                Cache[slot] = painted;
                return painted;
            }

            Sprite baked = Resources.Load<Sprite>(BakedFolder + slot);
            if (baked != null)
            {
                Report(slot, "baked", BakedFolder + slot, baked);
                Cache[slot] = baked;
                return baked;
            }

            Sprite generated = EmberforgeArt.Generate(slot);
            _generatedCount++;
            Cache[slot] = generated;
            ReportUnbaked();
            return generated;
        }

        private static void Report(string slot, string source, string path, Sprite sprite)
        {
            if (Reported.Contains(slot)) return;
            Reported.Add(slot);
            if (source != "PAINTED") return;

            // Only the painted case is worth a line. A baked hit is the ordinary case and saying
            // so twenty-seven times every launch is noise; a painted hit means an artist's file is
            // now overriding the kit, which is a thing somebody will want to confirm out loud.
            Debug.Log("EmberforgeSkin: '" + path + "' -> PAINTED sprite in use for slot '" + slot
                      + "' (" + sprite.rect.width + "x" + sprite.rect.height
                      + ", nine-slice border " + sprite.border + "). The baked kit is NOT being "
                      + "used for this slot.");
        }

        private static void ReportUnbaked()
        {
            if (_generatedReported) return;
            _generatedReported = true;
            Debug.LogWarning("EmberforgeSkin: no baked kit found under Assets/Resources/"
                + BakedFolder + ", so surfaces are being generated on the device at load instead. "
                + "The game will look correct but pays CPU, garbage and draw calls for it. Run "
                + "Emberfall > Design System > Bake Emberforge Sprites in the editor and commit "
                + "the result.");
        }

        /// <summary>How many slots have fallen through to the runtime generator. The component
        /// gallery prints it, so "is this project baked" is answerable by looking at the screen
        /// rather than by scrolling the console.</summary>
        public static int GeneratedCount { get { return _generatedCount; } }

        /// <summary>True when a hand-painted file is overriding this slot. Reporting only.</summary>
        public static bool IsPainted(string slot)
        {
            return Resources.Load<Sprite>(PaintedFolder + slot) != null;
        }

        /// <summary>True when the baked kit is supplying this slot.</summary>
        public static bool IsBaked(string slot)
        {
            return Resources.Load<Sprite>(BakedFolder + slot) != null;
        }

        /// <summary>Drops every cached lookup, so a sprite imported while the editor is running is
        /// picked up by the next screen that builds rather than after a domain reload.</summary>
        public static void Forget()
        {
            Cache.Clear();
            Reported.Clear();
            EmberforgeArt.Forget();
            _generatedCount = 0;
            _generatedReported = false;
        }
    }
}
