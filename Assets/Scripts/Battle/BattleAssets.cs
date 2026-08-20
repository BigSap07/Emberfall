using System.Collections.Generic;
using UnityEngine;
using Emberfall.UI;

namespace Emberfall.Battle
{
    /// <summary>
    /// Shaders, materials and world textures for the battlefield. Everything is generated or
    /// loaded from Resources, so the battle scene needs no imported art.
    /// </summary>
    public static class BattleAssets
    {
        private static Shader _lit;
        private static Shader _fx;
        private static readonly Dictionary<string, Texture2D> TextureCache = new Dictionary<string, Texture2D>();

        public static Shader LitShader
        {
            get
            {
                if (_lit == null) _lit = Resources.Load<Shader>("Shaders/EmberfallLit");
                if (_lit == null)
                {
                    _lit = Shader.Find("Sprites/Default");
                    Debug.LogWarning("Emberfall: Shaders/EmberfallLit not found in Resources, falling back to an unlit shader.");
                }
                return _lit;
            }
        }

        public static Shader FxShader
        {
            get
            {
                if (_fx == null) _fx = Resources.Load<Shader>("Shaders/EmberfallFx");
                if (_fx == null)
                {
                    _fx = Shader.Find("Sprites/Default");
                    Debug.LogWarning("Emberfall: Shaders/EmberfallFx not found in Resources, falling back to an unlit shader.");
                }
                return _fx;
            }
        }

        public static Material NewLit(Color color, Color rim, float emission)
        {
            Material material = new Material(LitShader);
            material.SetColor("_Color", color);
            material.SetColor("_RimColor", rim);
            material.SetColor("_ShadeColor", new Color(0.24f, 0.19f, 0.36f, 1f));
            material.SetColor("_BurnColor", new Color(1f, 0.5f, 0.18f, 1f));
            material.SetFloat("_RimPower", 3.1f);
            material.SetFloat("_Emission", emission);
            material.SetFloat("_Flash", 0f);
            material.SetFloat("_Fade", 1f);
            return material;
        }

        /// <summary>Additive when glowing, alpha-blended for shadows and the sky.</summary>
        public static Material NewFx(Texture texture, Color color, bool additive)
        {
            Material material = new Material(FxShader);
            material.SetTexture("_MainTex", texture);
            material.SetColor("_Color", color);
            material.SetFloat("_SrcBlend", 5f);              // SrcAlpha
            material.SetFloat("_DstBlend", additive ? 1f : 10f); // One : OneMinusSrcAlpha
            return material;
        }

        // ------------------------------------------------------------------ shared textures

        public static Texture2D SoftDot { get { return TextureLab.Glow(128, 2.1f).texture; } }
        public static Texture2D HardDot { get { return TextureLab.Circle(128).texture; } }
        public static Texture2D RingTexture { get { return TextureLab.Ring(256, 14).texture; } }
        public static Texture2D BlobShadow { get { return TextureLab.Glow(128, 1.35f).texture; } }

        /// <summary>Tapered streak used for slashes and bolts.</summary>
        public static Texture2D Streak
        {
            get
            {
                Texture2D cached;
                if (TextureCache.TryGetValue("streak", out cached)) return cached;

                const int w = 128, h = 32;
                Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                Color32[] pixels = new Color32[w * h];
                for (int y = 0; y < h; y++)
                {
                    float ny = (y + 0.5f) / h * 2f - 1f;
                    for (int x = 0; x < w; x++)
                    {
                        float nx = (x + 0.5f) / w;
                        float taper = Mathf.Sin(nx * Mathf.PI);
                        float across = Mathf.Clamp01(1f - Mathf.Abs(ny) / Mathf.Max(0.08f, taper));
                        float a = Mathf.Pow(across, 1.6f) * Mathf.Pow(taper, 0.7f);
                        pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(a * 255f, 0f, 255f));
                    }
                }
                tex.SetPixels32(pixels);
                tex.Apply();
                TextureCache["streak"] = tex;
                return tex;
            }
        }

        // ------------------------------------------------------------------ environments

        public struct Environment
        {
            public Color SkyTop;
            public Color SkyBottom;
            public Color GroundNear;
            public Color GroundFar;
            public Color Accent;
        }

        public static Environment GetEnvironment(string id)
        {
            Environment env = new Environment();
            switch (id)
            {
                case "ember":
                    env.SkyTop = UITheme.Hex("2A0F1C");
                    env.SkyBottom = UITheme.Hex("B0421C");
                    env.GroundNear = UITheme.Hex("3A2019");
                    env.GroundFar = UITheme.Hex("15090A");
                    env.Accent = UITheme.Hex("FF8A3C");
                    break;
                case "hollow":
                    env.SkyTop = UITheme.Hex("100B22");
                    env.SkyBottom = UITheme.Hex("3B2A6B");
                    env.GroundNear = UITheme.Hex("241E38");
                    env.GroundFar = UITheme.Hex("0A0714");
                    env.Accent = UITheme.Hex("A483FF");
                    break;
                default: // ashen
                    env.SkyTop = UITheme.Hex("14101F");
                    env.SkyBottom = UITheme.Hex("6A4A55");
                    env.GroundNear = UITheme.Hex("332B33");
                    env.GroundFar = UITheme.Hex("120E15");
                    env.Accent = UITheme.Hex("E8A06A");
                    break;
            }
            return env;
        }

        public static Texture2D SkyTexture(string environmentId)
        {
            string key = "sky_" + environmentId;
            Texture2D cached;
            if (TextureCache.TryGetValue(key, out cached)) return cached;

            Environment env = GetEnvironment(environmentId);
            const int w = 8, h = 256;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[w * h];

            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);
                // horizon glow sits low in the frame
                float glow = Mathf.Pow(1f - t, 3.2f);
                Color c = Color.Lerp(env.SkyBottom, env.SkyTop, Mathf.Pow(t, 0.7f));
                c = Color.Lerp(c, env.Accent, glow * 0.35f);
                for (int x = 0; x < w; x++) pixels[y * w + x] = c;
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            TextureCache[key] = tex;
            return tex;
        }

        /// <summary>Arena floor: a lit centre, concentric rings and speckled grit.</summary>
        public static Texture2D GroundTexture(string environmentId)
        {
            string key = "ground_" + environmentId;
            Texture2D cached;
            if (TextureCache.TryGetValue(key, out cached)) return cached;

            Environment env = GetEnvironment(environmentId);
            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[size * size];

            uint rng = 0x9E3779B9u;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);

                    Color c = Color.Lerp(env.GroundNear, env.GroundFar, Mathf.Clamp01(r * 1.15f));

                    // two faint arena rings
                    float ring = Mathf.Abs(r - 0.42f);
                    if (ring < 0.012f) c = Color.Lerp(c, env.Accent, 0.28f);
                    ring = Mathf.Abs(r - 0.66f);
                    if (ring < 0.008f) c = Color.Lerp(c, env.Accent, 0.16f);

                    rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                    float grit = ((rng & 0xFF) / 255f - 0.5f) * 0.09f;
                    c = new Color(Mathf.Clamp01(c.r + grit), Mathf.Clamp01(c.g + grit), Mathf.Clamp01(c.b + grit), 1f);

                    pixels[y * size + x] = c;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            TextureCache[key] = tex;
            return tex;
        }
    }
}
