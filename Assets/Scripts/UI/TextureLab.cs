using System.Collections.Generic;
using UnityEngine;

namespace Emberfall.UI
{
    /// <summary>
    /// Every sprite in the game is drawn here at runtime - rounded panels, bars, glows,
    /// hero sigils. Keeps the project asset-free while still looking deliberate.
    /// Results are cached by key, so repeated requests are free.
    /// </summary>
    public static class TextureLab
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static Sprite _white;

        public static Sprite White
        {
            get
            {
                if (_white != null) return _white;
                Texture2D tex = NewTexture(4, 4);
                Color32[] pixels = new Color32[16];
                for (int i = 0; i < 16; i++) pixels[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(pixels);
                tex.Apply();
                _white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                return _white;
            }
        }

        private static Texture2D NewTexture(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        // ------------------------------------------------------------------ panels

        /// <summary>Nine-sliced rounded rectangle. Tint it with Image.color; frame it with RoundedOutline.</summary>
        public static Sprite RoundedRect(int radius)
        {
            string key = "rr_" + radius;
            Sprite cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            int size = radius * 2 + 8;
            Texture2D tex = NewTexture(size, size);
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // negative inside the shape, zero on the edge
                    float d = RoundedDistance(x + 0.5f, y + 0.5f, size, size, radius);
                    float inside = Mathf.Clamp01(-d + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(inside * 255f, 0f, 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            float b = radius + 3;
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                          SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Rounded outline only - used to frame panels and portraits.</summary>
        public static Sprite RoundedOutline(int radius, int thickness)
        {
            string key = "ro_" + radius + "_" + thickness;
            Sprite cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            int size = radius * 2 + 8;
            Texture2D tex = NewTexture(size, size);
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedDistance(x + 0.5f, y + 0.5f, size, size, radius);
                    float outer = Mathf.Clamp01(-d + 0.5f);
                    float inner = Mathf.Clamp01(-(d + thickness) + 0.5f);
                    float ring = Mathf.Clamp01(outer - inner);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(ring * 255f, 0f, 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            float b = radius + 3;
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                          SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Signed distance to a rounded rectangle edge, negative inside.</summary>
        private static float RoundedDistance(float px, float py, float w, float h, float radius)
        {
            float cx = w * 0.5f;
            float cy = h * 0.5f;
            float dx = Mathf.Abs(px - cx) - (cx - radius);
            float dy = Mathf.Abs(py - cy) - (cy - radius);
            float ax = Mathf.Max(dx, 0f);
            float ay = Mathf.Max(dy, 0f);
            float outside = Mathf.Sqrt(ax * ax + ay * ay);
            float insideDistance = Mathf.Min(Mathf.Max(dx, dy), 0f);
            return outside + insideDistance - radius;
        }

        // ------------------------------------------------------------------ shapes

        public static Sprite Circle(int size)
        {
            string key = "circle_" + size;
            Sprite cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            Texture2D tex = NewTexture(size, size);
            Color32[] pixels = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            Cache[key] = sprite;
            return sprite;
        }

        public static Sprite Ring(int size, int thickness)
        {
            string key = "ring_" + size + "_" + thickness;
            Sprite cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            Texture2D tex = NewTexture(size, size);
            Color32[] pixels = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float outer = Mathf.Clamp01(r - d);
                    float inner = Mathf.Clamp01(r - thickness - d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(outer - inner) * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Soft radial falloff - glows, shadows under units, particle dots.</summary>
        public static Sprite Glow(int size, float power)
        {
            string key = "glow_" + size + "_" + power;
            Sprite cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            Texture2D tex = NewTexture(size, size);
            Color32[] pixels = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - r) / r;
                    float dy = (y + 0.5f - r) / r;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = Mathf.Pow(1f - d, power);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(a * 255f, 0f, 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Vertical gradient strip, stretched behind screens and cards.</summary>
        public static Sprite VerticalFade(int height, bool topOpaque)
        {
            string key = "vfade_" + height + "_" + topOpaque;
            Sprite cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            Texture2D tex = NewTexture(4, height);
            Color32[] pixels = new Color32[4 * height];
            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);
                float a = topOpaque ? 1f - t : t;
                a = a * a;
                byte alpha = (byte)Mathf.Clamp(a * 255f, 0f, 255f);
                for (int x = 0; x < 4; x++) pixels[y * 4 + x] = new Color32(255, 255, 255, alpha);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 4, height), new Vector2(0.5f, 0.5f), 100f, 0,
                                          SpriteMeshType.FullRect, new Vector4(0, 0, 0, 0));
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Diagonal hatch used on locked stages and empty slots.</summary>
        public static Sprite Hatch(int size, int spacing)
        {
            string key = "hatch_" + size + "_" + spacing;
            Sprite cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            Texture2D tex = NewTexture(size, size);
            tex.wrapMode = TextureWrapMode.Repeat;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool on = ((x + y) % spacing) < 2;
                    pixels[y * size + x] = new Color32(255, 255, 255, on ? (byte)255 : (byte)0);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            Cache[key] = sprite;
            return sprite;
        }

        // ------------------------------------------------------------------ star icon

        /// <summary>
        /// Five-pointed star shape, white on transparent, sized at <paramref name="size"/> px.
        /// Tint with Image.color: StarColor(n) for filled, WithAlpha(TextDim, 0.55f) for empty.
        /// Generated procedurally via a signed-distance field — no external asset needed.
        /// Placeholder: drop a real PNG at Assets/Resources/Art/UI/star_icon.png and redirect
        /// this method to Resources.Load it; the sprite contract (white, centred pivot) stays
        /// identical, so every call site works without further changes.
        /// </summary>
        public static Sprite Star(int size)
        {
            string key = "star_" + size;
            Sprite cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            Texture2D tex = NewTexture(size, size);
            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f - half) / half; // -1..1
                    float ny = (y + 0.5f - half) / half;
                    // Rotate so the top point faces up: map screen-up (+ny) → SDF-right (+px)
                    float sdf = StarSdf(ny, -nx, 0.85f);
                    // ~1.5-pixel anti-aliased edge in normalised space
                    float a = Mathf.Clamp01(-sdf * half / 1.5f + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            Cache[key] = sprite;
            return sprite;
        }

        // Signed-distance field for a regular 5-pointed star with outer radius r in [-1,1] space.
        // Returns negative inside the star, positive outside.
        // Adapted from iquilezles.org/articles/distfunctions2d — sdStar(p, r, n=5, m=2).
        private static float StarSdf(float px, float py, float r)
        {
            const float n  = 5f;
            // m=2 gives the classic sharp-pointed star; with m=2, en=π/2 so ecs=(0,1).
            // All ecs terms collapse: dot(q,ecs)=qy, ecx=0 → qx unchanged, ecy/ecy=1.
            float an  = Mathf.PI / n;             // 36°
            float acx = Mathf.Cos(an);            // cos(36°) ≈ 0.809
            float acy = Mathf.Sin(an);            // sin(36°) ≈ 0.588

            // Reduce the point to the first sector [-an, an] around angle 0.
            // Mathf.Floor gives true floor division so mod is always ≥ 0.
            float angle = Mathf.Atan2(py, px);
            float twoAn = 2f * an;
            float mod   = angle - twoAn * Mathf.Floor(angle / twoAn);
            float bn    = mod - an;

            float len = Mathf.Sqrt(px * px + py * py);
            float qx  = len * Mathf.Cos(bn);
            float qy  = len * Mathf.Abs(Mathf.Sin(bn));

            // Translate by r * acs.
            qx -= r * acx;
            qy -= r * acy;

            // Project onto edge. With ecs=(0,1): dot(q,ecs)=qy, ecx=0, ecs.y/ecs.y=1.
            qy += Mathf.Clamp(-qy, 0f, r * acy);

            return Mathf.Sqrt(qx * qx + qy * qy) * Mathf.Sign(qx);
        }

        // ------------------------------------------------------------------ hero sigils

        /// <summary>
        /// A distinct emblem per unit, drawn from simple analytic shapes.
        /// Gives every hero a recognisable portrait without a single image file.
        /// </summary>
        public static Sprite Sigil(int index, int size)
        {
            string key = "sigil_" + index + "_" + size;
            Sprite cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            Texture2D tex = NewTexture(size, size);
            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f - half) / half; // -1..1
                    float ny = (y + 0.5f - half) / half;
                    float a = SigilAlpha(index, nx, ny);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(a * 255f, 0f, 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            Cache[key] = sprite;
            return sprite;
        }

        private static float SigilAlpha(int index, float nx, float ny)
        {
            float r = Mathf.Sqrt(nx * nx + ny * ny);
            float ang = Mathf.Atan2(ny, nx);
            switch (index % 14)
            {
                case 0: // shield - bulwark
                    return Band(Mathf.Max(Mathf.Abs(nx) * 1.15f, ny * 0.9f + r * 0.5f), 0.72f);
                case 1: // flame petals - breaker
                    return Band(r - 0.16f * Mathf.Sin(ang * 5f), 0.70f);
                case 2: // leaf / droplet - mender
                    return Band(Mathf.Sqrt(nx * nx * 1.9f + (ny - 0.12f) * (ny - 0.12f)), 0.66f);
                case 3: // lance chevron - striker
                    return Band(Mathf.Abs(Mathf.Abs(nx) - Mathf.Max(0f, ny * 0.8f + 0.1f)) * 1.6f + Mathf.Abs(ny) * 0.5f, 0.62f);
                case 4: // wave rings - warden
                    return Ripple(r, 3f, 0.72f);
                case 5: // dagger diamond - stalker
                    return Band(Mathf.Abs(nx) * 1.6f + Mathf.Abs(ny) * 0.85f, 0.72f);
                case 6: // hollow eye
                    return Mathf.Clamp01(Band(r, 0.74f) - Band(r, 0.34f));
                case 7: // ember spark
                    return Band(r + 0.22f * Mathf.Cos(ang * 3f), 0.62f);
                case 8: // moth wings
                    return Band(Mathf.Abs(nx) * 0.9f + Mathf.Abs(ny) * 1.5f - 0.14f * Mathf.Cos(ang * 2f), 0.70f);
                case 9: // fang trio
                    return Band(Mathf.Abs(Mathf.Repeat(nx * 3f + 1.5f, 1f) - 0.5f) * 2.2f + ny * 0.8f, 0.70f);
                case 10: // block / golem
                    return Band(Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny)) * 1.05f, 0.66f);
                case 11: // revenant crown
                    return Band(Mathf.Abs(ny + 0.15f) * 1.6f + Mathf.Abs(Mathf.Repeat(nx * 2.5f + 1.25f, 1f) - 0.5f) * 1.2f, 0.66f);
                case 12: // tide spiral
                    return Ripple(r + ang * 0.06f, 4f, 0.74f);
                default: // sundered star
                    return Band(r + 0.30f * Mathf.Cos(ang * 6f), 0.60f);
            }
        }

        private static float Band(float value, float edge)
        {
            return Mathf.Clamp01((edge - value) * 12f);
        }

        private static float Ripple(float r, float frequency, float limit)
        {
            if (r > limit) return 0f;
            float wave = Mathf.Abs(Mathf.Sin(r * frequency * Mathf.PI));
            float fade = Mathf.Clamp01((limit - r) * 6f);
            return Mathf.Clamp01((wave - 0.55f) * 4f) * fade;
        }

        // ------------------------------------------------------------------ the Cinder

        /// <summary>
        /// The Keeper's own motif (DESIGN-ART-BIBLE section 5): "a small floating fragment... an
        /// irregular piece of translucent, almost colorless material containing six razor-thin
        /// internal fractures [that] nearly meet in the center but never connect." An outline only,
        /// not a filled gem - the material is translucent, and a filled shape reads as a solid icon
        /// rather than a fragment of something you can see through. Colourless by construction: this
        /// method draws white-on-transparent like every other TextureLab shape, and it is on the
        /// caller to tint it with a neutral tone. Nothing about the Cinder is aspect-tinted at rest,
        /// so no call site anywhere in this project may pass it an AspectColor.
        ///
        /// The outer silhouette is a hexagon perturbed by two fixed sine harmonics rather than a
        /// regular one, so it reads as "irregular" rather than as a gem-icon cliché - deliberately
        /// asymmetric, not randomised, so the sprite is identical on every call the way every other
        /// TextureLab shape is. The six fractures are spaced 60 degrees apart with a small per-line
        /// jitter for the same reason, each stopping short of both the centre and the outer edge.
        /// </summary>
        public static Sprite CinderFragment(int size)
        {
            string key = "cinder_" + size;
            Sprite cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            Texture2D tex = NewTexture(size, size);
            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f - half) / half;
                    float ny = (y + 0.5f - half) / half;
                    float a = CinderAlpha(nx, ny);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(a * 255f, 0f, 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            Cache[key] = sprite;
            return sprite;
        }

        private static float CinderAlpha(float nx, float ny)
        {
            float r = Mathf.Sqrt(nx * nx + ny * ny);
            float ang = Mathf.Atan2(ny, nx);

            // Outline: a hexagonal radius perturbed per-angle by two harmonics, so no two facets
            // match - "an irregular piece", not a regular gem icon.
            float wobble = 0.09f * Mathf.Sin(ang * 3f + 0.6f) + 0.05f * Mathf.Sin(ang * 5f - 1.1f);
            float outerRadius = 0.80f + wobble;
            float outline = Mathf.Clamp01((0.045f - Mathf.Abs(r - outerRadius)) * 30f);

            // Six fractures, evenly spaced with a small per-line jitter, each a thin band along its
            // own ray. Stops short of the centre (proj > 0.14) and short of the outer edge
            // (proj < outerRadius * 0.72) so they "nearly meet... but never connect" at either end.
            float fractures = 0f;
            for (int i = 0; i < 6; i++)
            {
                float baseAngle = i * (Mathf.PI * 2f / 6f);
                float jitter = 0.06f * Mathf.Sin(i * 2.1f + 0.4f);
                float a2 = baseAngle + jitter;
                float dx = Mathf.Cos(a2);
                float dy = Mathf.Sin(a2);
                float proj = nx * dx + ny * dy;
                float perp = Mathf.Abs(-nx * dy + ny * dx);
                float inSegment = (proj > 0.14f && proj < outerRadius * 0.72f) ? 1f : 0f;
                float line = inSegment * Mathf.Clamp01((0.022f - perp) * 46f);
                fractures = Mathf.Max(fractures, line);
            }

            return Mathf.Max(outline, fractures);
        }

        // ================================================================== shop-screen shapes
        //
        // Everything below is an ALPHA MASK, like every other sprite in this file: white pixels at
        // varying alpha, tinted at the call site with Image.color. That is what keeps the currency
        // strip's six icons "small flat icons, not illustrated scenes" - a silhouette plus at most
        // one interior facet at reduced alpha to suggest a cut face. None of them carry a colour of
        // their own; UITheme decides that.

        /// <summary>
        /// Coverage of one pixel against a shape function, 3x3 supersampled.
        ///
        /// The rest of this file antialiases by clamping a signed distance, which is exact and
        /// cheap for circles and rounded rects because their SDF is closed-form. The shapes below
        /// are polygons and spirals whose SDFs are not, so they sample instead. 3x3 is the smallest
        /// grid that keeps a 45-degree polygon edge from stair-stepping at the 32-40px the strip
        /// actually draws these at.
        /// </summary>
        private static float Coverage(int x, int y, int size, System.Func<float, float, float> shape)
        {
            float total = 0f;
            for (int sy = 0; sy < 3; sy++)
            {
                for (int sx = 0; sx < 3; sx++)
                {
                    float u = (x + (sx + 0.5f) / 3f) / size;
                    float v = (y + (sy + 0.5f) / 3f) / size;
                    total += Mathf.Clamp01(shape(u, v));
                }
            }
            return total / 9f;
        }

        /// <summary>Rasterises a shape function into a cached square alpha-mask sprite.</summary>
        private static Sprite Mask(string key, int size, System.Func<float, float, float> shape)
        {
            Sprite cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            Texture2D tex = NewTexture(size, size);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color32(255, 255, 255,
                        (byte)Mathf.Clamp(Coverage(x, y, size, shape) * 255f, 0f, 255f));

            tex.SetPixels32(pixels);
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                          SpriteMeshType.FullRect);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>1 when (u,v) is inside the closed polygon, 0 outside. Even-odd crossing test.</summary>
        private static float InPoly(float u, float v, float[] poly)
        {
            bool inside = false;
            int count = poly.Length / 2;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                float xi = poly[i * 2], yi = poly[i * 2 + 1];
                float xj = poly[j * 2], yj = poly[j * 2 + 1];
                if ((yi > v) != (yj > v) && u < (xj - xi) * (v - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside ? 1f : 0f;
        }

        /// <summary>1 within halfWidth of the segment a-b, 0 beyond. Round caps.</summary>
        private static float OnSegment(float u, float v, float ax, float ay, float bx, float by, float halfWidth)
        {
            float dx = bx - ax, dy = by - ay;
            float len2 = dx * dx + dy * dy;
            float t = len2 <= 0f ? 0f : Mathf.Clamp01(((u - ax) * dx + (v - ay) * dy) / len2);
            float px = ax + dx * t - u, py = ay + dy * t - v;
            return Mathf.Sqrt(px * px + py * py) <= halfWidth ? 1f : 0f;
        }

        // ---------------------------------------------------------------- currency icons

        /// <summary>Gold. A struck disc with an inner rim groove, read at reduced alpha.</summary>
        public static Sprite CoinIcon(int size)
        {
            return Mask("icon_coin_" + size, size, delegate (float u, float v)
            {
                float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f));
                if (d > 0.46f) return 0f;
                // The groove: a band held back to half alpha so the disc reads as struck metal with
                // a raised rim rather than as a plain filled circle.
                if (d > 0.33f && d < 0.37f) return 0.5f;
                return 1f;
            });
        }

        /// <summary>Ember Shards. A leaning four-point crystal with one facet.</summary>
        public static Sprite CrystalIcon(int size)
        {
            float[] body = { 0.60f, 0.97f, 0.88f, 0.58f, 0.40f, 0.03f, 0.12f, 0.42f };
            return Mask("icon_crystal_" + size, size, delegate (float u, float v)
            {
                float inside = InPoly(u, v, body);
                if (inside <= 0f) return 0f;
                // Facet down the long axis, held back so the crystal reads as cut glass.
                return OnSegment(u, v, 0.60f, 0.97f, 0.40f, 0.03f, 0.055f) > 0f ? 0.55f : 1f;
            });
        }

        /// <summary>Relic Dust. An open spiral with a loose mote at its head.</summary>
        public static Sprite SpiralIcon(int size)
        {
            return Mask("icon_spiral_" + size, size, delegate (float u, float v)
            {
                float dx = u - 0.48f, dy = v - 0.44f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                if (Mathf.Sqrt((u - 0.70f) * (u - 0.70f) + (v - 0.88f) * (v - 0.88f)) < 0.075f) return 1f;
                if (r < 0.04f) return 1f;
                if (r > 0.42f) return 0f;

                // Archimedean spiral r = k*theta, unwound over three turns. A pixel is on the stroke
                // when its radius matches the spiral's at its own angle, on any turn.
                float theta = Mathf.Atan2(dy, dx);
                if (theta < 0f) theta += Mathf.PI * 2f;
                const float k = 0.052f;
                float best = 1f;
                for (int turn = 0; turn < 3; turn++)
                {
                    float spiralR = k * (theta + turn * Mathf.PI * 2f);
                    best = Mathf.Min(best, Mathf.Abs(r - spiralR));
                }
                return best < 0.036f ? 1f : 0f;
            });
        }

        /// <summary>Slag. A poured bar seen from a corner: front face solid, top face lifted.</summary>
        public static Sprite IngotIcon(int size)
        {
            float[] top = { 0.14f, 0.52f, 0.72f, 0.52f, 0.90f, 0.70f, 0.32f, 0.70f };
            float[] front = { 0.14f, 0.52f, 0.72f, 0.52f, 0.72f, 0.30f, 0.14f, 0.30f };
            return Mask("icon_ingot_" + size, size, delegate (float u, float v)
            {
                if (InPoly(u, v, front) > 0f) return 1f;
                if (InPoly(u, v, top) > 0f) return 0.62f;
                return 0f;
            });
        }

        /// <summary>Echoes. An hourglass - the one currency that refills on a clock.</summary>
        public static Sprite HourglassIcon(int size)
        {
            float[] upper = { 0.22f, 0.80f, 0.78f, 0.80f, 0.50f, 0.50f };
            float[] lower = { 0.22f, 0.20f, 0.78f, 0.20f, 0.50f, 0.50f };
            return Mask("icon_hourglass_" + size, size, delegate (float u, float v)
            {
                if (v > 0.80f && v < 0.92f && u > 0.16f && u < 0.84f) return 1f;   // top cap
                if (v > 0.08f && v < 0.20f && u > 0.16f && u < 0.84f) return 1f;   // bottom cap
                if (InPoly(u, v, lower) > 0f) return 1f;                           // sand, settled
                if (InPoly(u, v, upper) > 0f) return 0.55f;                        // glass, draining
                return 0f;
            });
        }

        /// <summary>An event's own token. A cut hexagonal gem with a table facet.</summary>
        public static Sprite GemIcon(int size)
        {
            float[] body = { 0.50f, 0.95f, 0.88f, 0.72f, 0.88f, 0.28f, 0.50f, 0.05f, 0.12f, 0.28f, 0.12f, 0.72f };
            return Mask("icon_gem_" + size, size, delegate (float u, float v)
            {
                if (InPoly(u, v, body) <= 0f) return 0f;
                if (v > 0.66f) return 0.6f;   // table facet across the crown
                return 1f;
            });
        }

        // ---------------------------------------------------------------- layout shapes

        /// <summary>
        /// Nine-sliced rectangle with CHAMFERED corners - the mockup's frame language, on the
        /// relic cards, the pull button, the fight button and the event reward tiles.
        ///
        /// Same signed-distance machinery as RoundedRect; only the corner metric changes. A round
        /// corner measures the corner region euclidean (sqrt(ax^2+ay^2)); a chamfer measures it
        /// along the diagonal ((ax+ay)*0.7071), which cuts the corner off flat instead of curving
        /// it. One line of difference, and it is why this is here rather than a hand-drawn asset.
        /// </summary>
        public static Sprite NotchedRect(int cut)
        {
            string key = "nr_" + cut;
            Sprite cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            int size = cut * 2 + 8;
            Texture2D tex = NewTexture(size, size);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = NotchedDistance(x + 0.5f, y + 0.5f, size, size, cut);
                    pixels[y * size + x] = new Color32(255, 255, 255,
                        (byte)Mathf.Clamp(Mathf.Clamp01(-d + 0.5f) * 255f, 0f, 255f));
                }

            tex.SetPixels32(pixels);
            tex.Apply();
            float b = cut + 3;
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                          SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>The chamfered frame that goes around a NotchedRect.</summary>
        public static Sprite NotchedOutline(int cut, int thickness)
        {
            string key = "no_" + cut + "_" + thickness;
            Sprite cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            int size = cut * 2 + 8;
            Texture2D tex = NewTexture(size, size);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = NotchedDistance(x + 0.5f, y + 0.5f, size, size, cut);
                    float outer = Mathf.Clamp01(-d + 0.5f);
                    float inner = Mathf.Clamp01(-(d + thickness) + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255,
                        (byte)Mathf.Clamp(Mathf.Clamp01(outer - inner) * 255f, 0f, 255f));
                }

            tex.SetPixels32(pixels);
            tex.Apply();
            float b = cut + 3;
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                          SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Signed distance to a chamfered rectangle edge, negative inside.
        ///
        /// An octagon is the INTERSECTION of a box and a diagonal half-plane, so its distance is
        /// the MAX of the two - not the box's own corner metric with the corner term swapped, which
        /// is what this was first written as. That version measured a point halfway up the left
        /// edge as 6px INSIDE the shape rather than on it, because (ax+ay)*0.7071 with ay=0
        /// understates a straight-edge distance by a factor of root two. The fill still looked
        /// roughly right - everything it got wrong was still inside - but NotchedOutline computes a
        /// ring as the difference of two of these, and a distance that never approaches zero along
        /// an edge produces no ring there. Every chamfered frame in the game rendered as four
        /// corner ticks with nothing joining them.
        /// </summary>
        private static float NotchedDistance(float px, float py, float w, float h, float cut)
        {
            float cx = w * 0.5f, cy = h * 0.5f;
            float x = Mathf.Abs(px - cx);
            float y = Mathf.Abs(py - cy);

            float box = Mathf.Max(x - cx, y - cy);
            float diagonal = (x + y - (cx + cy - cut)) * 0.70710678f;
            return Mathf.Max(box, diagonal);
        }

        /// <summary>Flat-top hexagon. The Deep Recital's floor plate.</summary>
        public static Sprite Hexagon(int size)
        {
            float[] hex = { 0.50f, 0.99f, 0.95f, 0.75f, 0.95f, 0.25f, 0.50f, 0.01f, 0.05f, 0.25f, 0.05f, 0.75f };
            return Mask("hex_" + size, size, delegate (float u, float v) { return InPoly(u, v, hex); });
        }

        /// <summary>Hexagon outline. Thickness is a fraction of the sprite's own size.</summary>
        public static Sprite HexagonOutline(int size, float thickness)
        {
            float[] outer = { 0.50f, 0.99f, 0.95f, 0.75f, 0.95f, 0.25f, 0.50f, 0.01f, 0.05f, 0.25f, 0.05f, 0.75f };
            float t = thickness;
            float[] inner = { 0.50f, 0.99f - t * 1.6f, 0.95f - t, 0.75f - t * 0.6f, 0.95f - t, 0.25f + t * 0.6f,
                              0.50f, 0.01f + t * 1.6f, 0.05f + t, 0.25f + t * 0.6f, 0.05f + t, 0.75f - t * 0.6f };
            return Mask("hexo_" + size + "_" + thickness, size, delegate (float u, float v)
            {
                return Mathf.Clamp01(InPoly(u, v, outer) - InPoly(u, v, inner));
            });
        }

        /// <summary>A milestone node on the events track.</summary>
        public static Sprite Diamond(int size)
        {
            float[] d = { 0.5f, 0.98f, 0.98f, 0.5f, 0.5f, 0.02f, 0.02f, 0.5f };
            return Mask("diamond_" + size, size, delegate (float u, float v) { return InPoly(u, v, d); });
        }

        // ---------------------------------------------------------------- glyphs

        /// <summary>Highest floor reached. A three-point crown on a band.</summary>
        public static Sprite Crown(int size)
        {
            float[] c = { 0.06f, 0.32f, 0.94f, 0.32f, 0.94f, 0.78f, 0.76f, 0.56f,
                          0.50f, 0.80f, 0.24f, 0.56f, 0.06f, 0.78f };
            return Mask("crown_" + size, size, delegate (float u, float v)
            {
                if (v < 0.30f && v > 0.16f && u > 0.06f && u < 0.94f) return 1f;   // band
                return InPoly(u, v, c);
            });
        }

        /// <summary>The fight action. Two crossed blades.</summary>
        public static Sprite Swords(int size)
        {
            return Mask("swords_" + size, size, delegate (float u, float v)
            {
                float a = OnSegment(u, v, 0.16f, 0.14f, 0.84f, 0.86f, 0.055f);
                float b = OnSegment(u, v, 0.84f, 0.14f, 0.16f, 0.86f, 0.055f);
                // Guards, so the blades read as swords rather than as an X.
                float g1 = OnSegment(u, v, 0.10f, 0.30f, 0.32f, 0.10f, 0.038f);
                float g2 = OnSegment(u, v, 0.90f, 0.30f, 0.68f, 0.10f, 0.038f);
                return Mathf.Max(Mathf.Max(a, b), Mathf.Max(g1, g2));
            });
        }

        /// <summary>The reward at the end of a track.</summary>
        public static Sprite Chest(int size)
        {
            return Mask("chest_" + size, size, delegate (float u, float v)
            {
                bool box = u > 0.10f && u < 0.90f && v > 0.12f && v < 0.52f;
                float lx = (u - 0.50f) / 0.40f, ly = (v - 0.52f) / 0.34f;
                bool lid = v >= 0.52f && lx * lx + ly * ly <= 1f;   // domed lid on the box
                if (!box && !lid) return 0f;
                if (v > 0.46f && v < 0.56f) return 0.45f;                            // lid seam
                if (u > 0.44f && u < 0.56f && v > 0.30f && v < 0.62f) return 0.45f;  // clasp
                return 1f;
            });
        }

        /// <summary>A claimed node.</summary>
        public static Sprite Check(int size)
        {
            return Mask("check_" + size, size, delegate (float u, float v)
            {
                float a = OnSegment(u, v, 0.14f, 0.52f, 0.40f, 0.22f, 0.075f);
                float b = OnSegment(u, v, 0.40f, 0.22f, 0.86f, 0.78f, 0.075f);
                return Mathf.Max(a, b);
            });
        }

        /// <summary>A locked node.</summary>
        public static Sprite Padlock(int size)
        {
            return Mask("padlock_" + size, size, delegate (float u, float v)
            {
                if (u > 0.16f && u < 0.84f && v > 0.08f && v < 0.56f) return 1f;   // body
                float dx = u - 0.50f, dy = v - 0.56f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                if (dy >= 0f && r > 0.20f && r < 0.31f) return 1f;                 // shackle
                return 0f;
            });
        }
    }
}
