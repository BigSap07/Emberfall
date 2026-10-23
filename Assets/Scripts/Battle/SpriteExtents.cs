using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Battle
{
    /// <summary>
    /// WHERE THE VISIBLE PIXELS OF EACH BATTLE SPRITE SIT, as the game reads them: [ybot, ytop] per sprite id, fractions
    /// of one frame with y from the bottom, the union over the idle clip (Tools/sprite_extents.py writes
    /// Assets/Resources/Content/sprite-extents.json from the art, and the same numbers sit in
    /// Tools/DevHarness/sprite-extents.txt, which the harness holds equal).
    ///
    /// The battle uses it for one thing: where a unit's HEAD really is. UnitRig sizes a sprite so the body top lands at
    /// the silhouette height (spriteBodyFraction, spriteFootFraction), and for nearly every unit that is the top of the
    /// visible art too; this file says which units it is not, and by how much, so the formation scales on the visible
    /// figure and the bars, the numbers and the effects anchor to its real head. A unit with no entry (a placeholder, a
    /// sprite drawn after the file was written) takes the frame rule it always had.
    ///
    /// Presentation only and free of UnityEngine, so the harness compiles it. ContentDatabase never reads the file, so
    /// it is not part of ContentVersion.
    /// </summary>
    public sealed class SpriteExtents
    {
        /// <summary>The file, relative to Resources.</summary>
        public const string ResourcePath = "Content/sprite-extents";

        /// <summary>The most the art's visible top may differ from the rig's body top and still be trusted as the
        /// same figure. Beyond it the extents include something that is not the body (the Carrion Vulture's raised
        /// wings are 2.4 times its body), so the body rule stands.</summary>
        public const float MinTopRatio = 0.80f;
        public const float MaxTopRatio = 1.25f;

        public readonly Dictionary<string, float[]> Sprites = new Dictionary<string, float[]>();
        public readonly List<string> Warnings = new List<string>();

        public static readonly SpriteExtents Empty = new SpriteExtents();

        public static SpriteExtents FromJson(string text)
        {
            SpriteExtents e = new SpriteExtents();
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err) || !root.IsObject)
            {
                e.Warnings.Add("sprite-extents.json: " + (err ?? "not an object"));
                return e;
            }
            JsonValue sprites = root["sprites"];
            if (!sprites.IsObject) { e.Warnings.Add("sprite-extents.json: no \"sprites\" object"); return e; }
            foreach (string key in sprites.Keys)
            {
                JsonValue pair = sprites[key];
                if (key.Length == 0 || !pair.IsArray || pair.Count != 2) { e.Warnings.Add("sprite-extents.json: '" + key + "' is not [ybot, ytop]"); continue; }
                float bot = pair[0].AsFloat(-1f), top = pair[1].AsFloat(-1f);
                if (bot < 0f || top <= bot || top > 1.0001f) { e.Warnings.Add("sprite-extents.json: '" + key + "' is out of range"); continue; }
                e.Sprites[key] = new[] { bot, top };
            }
            return e;
        }

        /// <summary>The art's [ybot, ytop] for a sprite id, or false when the file has none.</summary>
        public bool TryGet(string sprite, out float ybot, out float ytop)
        {
            float[] pair;
            if (sprite != null && Sprites.TryGetValue(sprite, out pair)) { ybot = pair[0]; ytop = pair[1]; return true; }
            ybot = 0f; ytop = 0f;
            return false;
        }

        /// <summary>
        /// The art's visible top above the feet divided by the rig's body top above the feet, for a unit with this
        /// body fraction and foot fraction. In the content spriteBodyFraction is the body's height above the feet as a
        /// fraction of the frame, so the visible top above the feet is ytop minus footFraction, in the same measure.
        /// 1 when there is no entry, no usable fractions, or the ratio is outside MinTopRatio to MaxTopRatio.
        /// </summary>
        public float TopRatio(string sprite, float bodyFraction, float footFraction)
        {
            float bot, top;
            if (!TryGet(sprite, out bot, out top)) return 1f;
            return RatioOf(top, bodyFraction, footFraction);
        }

        /// <summary>The ratio itself, for a known ytop.</summary>
        public static float RatioOf(float ytop, float bodyFraction, float footFraction)
        {
            if (bodyFraction <= 0.05f) return 1f;
            float visible = ytop - footFraction;
            if (visible <= 0f) return 1f;
            float r = visible / bodyFraction;
            return r < MinTopRatio || r > MaxTopRatio ? 1f : r;
        }
    }
}
