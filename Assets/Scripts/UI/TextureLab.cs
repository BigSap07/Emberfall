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
    }
}
