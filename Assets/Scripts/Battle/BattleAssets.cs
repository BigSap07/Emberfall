using System;
using System.Collections.Generic;
using UnityEngine;
using Emberfall.Core.Json;
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

        /// <summary>Additive when glowing, alpha-blended for shadows and the backdrop.</summary>
        public static Material NewFx(Texture texture, Color color, bool additive)
        {
            Material material = new Material(FxShader);
            material.SetTexture("_MainTex", texture);
            material.SetColor("_Color", color);
            material.SetFloat("_SrcBlend", 5f);              // SrcAlpha
            material.SetFloat("_DstBlend", additive ? 1f : 10f); // One : OneMinusSrcAlpha
            return material;
        }

        // ------------------------------------------------------------------ unit sprites

        /// <summary>
        /// Loads Resources/Sprites/Units/{spriteId}.png for the sprite rig path. Cached like the
        /// generated textures below, so repeat battles with the same unit don't re-hit Resources.
        /// Never returns null: a missing or misspelled id logs a warning and falls back to a flat
        /// magenta square, which is deliberately impossible to mistake for real art, rather than
        /// silently reverting to the procedural rig (that fallback belongs to VisualDef.Sprite
        /// being empty, a different and correct case - a non-empty id that fails to resolve is a
        /// content bug and should look like one).
        /// </summary>
        public static Texture2D UnitSprite(string spriteId)
        {
            string key = "unitsprite_" + spriteId;
            Texture2D cached;
            if (TextureCache.TryGetValue(key, out cached)) return cached;

            Texture2D tex = Resources.Load<Texture2D>("Sprites/Units/" + spriteId);
            if (tex == null)
            {
                Debug.LogWarning("Emberfall: no sprite at Resources/Sprites/Units/" + spriteId + ".png - showing a placeholder instead.");
                tex = MissingSpriteTexture;
            }
            TextureCache[key] = tex;
            return tex;
        }

        private static Texture2D MissingSpriteTexture
        {
            get
            {
                Texture2D cached;
                if (TextureCache.TryGetValue("missingsprite", out cached)) return cached;
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Color32 magenta = new Color32(255, 0, 220, 255);
                tex.SetPixels32(new[] { magenta, magenta, magenta, magenta });
                tex.Apply();
                TextureCache["missingsprite"] = tex;
                return tex;
            }
        }

        // ------------------------------------------------------------------ unit sprite-sheet clips

        private static readonly Dictionary<string, SpriteAnimClip> ClipCache = new Dictionary<string, SpriteAnimClip>();

        /// <summary>
        /// Loads Resources/Sprites/Units/{spriteId}_{clipId}.png (+ its sidecar .json) as one
        /// animated flipbook clip. Extends the plain UnitSprite convention above rather than
        /// inventing a new one: same folder, same {spriteId} prefix, "_{clipId}" is the only new
        /// part, so a hero with no clip data (everyone except Kael today) is entirely untouched -
        /// the sidecar simply isn't there and this returns null.
        ///
        /// Unlike UnitSprite, a missing clip is NOT a content bug and logs nothing: it is the
        /// normal shape of "this hero has no animation set" (every hero but Kael) or "this hero
        /// ships fewer than five clips" (a real future case this is written to allow). Only a
        /// malformed sidecar - one that exists but fails to parse - warns, since that is
        /// distinguishable from "not authored yet".
        /// </summary>
        public static SpriteAnimClip UnitAnimClip(string spriteId, string clipId)
        {
            string key = spriteId + "_" + clipId;
            SpriteAnimClip cached;
            if (ClipCache.TryGetValue(key, out cached)) return cached;

            TextAsset sidecar = Resources.Load<TextAsset>("Sprites/Units/" + key);
            if (sidecar == null) { ClipCache[key] = null; return null; }

            JsonValue root;
            string err;
            if (!JsonValue.TryParse(sidecar.text, out root, out err))
            {
                Debug.LogWarning("Emberfall: malformed sprite clip sidecar Sprites/Units/" + key + ".json - " + err);
                ClipCache[key] = null;
                return null;
            }

            Texture2D tex = Resources.Load<Texture2D>("Sprites/Units/" + key);
            if (tex == null)
            {
                Debug.LogWarning("Emberfall: sprite clip sidecar Sprites/Units/" + key + ".json has no matching .png");
                ClipCache[key] = null;
                return null;
            }

            JsonValue sheet = root["sheet"];
            JsonValue defaultAnim = root["defaultAnimation"];
            SpriteAnimClip clip = new SpriteAnimClip();
            clip.Texture = tex;
            clip.FrameWidth = root.GetInt("frameWidth", tex.width);
            clip.FrameHeight = root.GetInt("frameHeight", tex.height);
            clip.Columns = Math.Max(1, sheet.GetInt("cols", 1));
            clip.Rows = Math.Max(1, sheet.GetInt("rows", 1));
            clip.StartFrame = Math.Max(0, defaultAnim.GetInt("start", 0));
            clip.EndFrame = Math.Max(clip.StartFrame, defaultAnim.GetInt("end", clip.StartFrame));
            clip.Fps = Math.Max(1f, defaultAnim.GetFloat("fps", 30f));
            clip.ImpactFraction = Math.Max(0f, Math.Min(1f, defaultAnim.GetFloat("impactFraction", clip.ImpactFraction)));
            clip.PlaybackSpeed = Math.Max(0.01f, defaultAnim.GetFloat("playbackSpeed", clip.PlaybackSpeed));
            clip.FootFraction = defaultAnim.GetFloat("footFraction", -1f);

            ClipCache[key] = clip;
            return clip;
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
                case "proving":
                    // The Proving's gatehouse yard at dusk. Warm on purpose, and distinct from both
                    // of the other named palettes: ember is a hot vermilion fire-lit red, ashen is a
                    // cool grey-mauve, and this sits between them as a low gold-amber sunset - the
                    // same warm-dusk register the Hub painting and DESIGN-ART-BIBLE section 5's
                    // Cinder imagery already establish, so the mode reads as part of this world
                    // rather than as a fourth unrelated mood.
                    //
                    // Accent is UITheme.Gold's value (F2C14E) rather than a new hex: gold is already
                    // this mode's own colour - Valor is paid in it and the Proving screen's purse
                    // shows it - so the light pooling above the horizon is the colour the player
                    // associates with the mode.
                    //
                    // NOT VERIFIED VISUALLY. These are chosen tones, not a rendered result; nothing
                    // outside Play mode can say how this actually looks, exactly as ashen, ember and
                    // hollow's own values could not be checked by the loader that reads them.
                    env.SkyTop = UITheme.Hex("221726");
                    env.SkyBottom = UITheme.Hex("9A5A3C");
                    env.GroundNear = UITheme.Hex("3B3026");
                    env.GroundFar = UITheme.Hex("17110D");
                    env.Accent = UITheme.Hex("F2C14E");
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

        // ------------------------------------------------------------------ backdrops

        /// <summary>
        /// The painted backdrop for one environment, from Resources/Backgrounds/{environmentId}.png.
        /// Cached exactly like UnitSprite above, and keyed on the same environmentId campaign.json
        /// already authors per stage, so adding a painting for a new environment is a file drop
        /// rather than a code change.
        ///
        /// PAINTED TODAY: "ashen", "ember", "hollow" - four campaign stages each.
        ///
        /// NAMED BUT UNPAINTED, falling through to GeneratedBackdrop below: "coast", "deep",
        /// "tidal" (twelve chapter 2 stages) and "proving" (The Proving). Each becomes a real
        /// painting by dropping a file at Assets/Resources/Backgrounds/{id}.png - specifically
        /// Backgrounds/proving.png for The Proving - with NO code change of any kind. That is the
        /// whole point of keying this on the id campaign.json already authors.
        ///
        /// A TRAP WORTH KNOWING, because it makes the fallback quieter than it sounds. The
        /// generated backdrop is built from GetEnvironment(id), and GetEnvironment only has cases
        /// for "ember", "hollow" and now "proving" - everything else, INCLUDING "coast", "deep" and
        /// "tidal", hits its default arm and is drawn in ashen's palette. So those twelve stages do
        /// not currently generate "their own" colours; they generate ashen's. That is a real gap
        /// and it is not fixed here, because a palette is an art-direction decision rather than a
        /// mechanical one. "proving" was given a case precisely so it would not join them.
        ///
        /// The fallback deliberately differs from UnitSprite's. There, a non-empty sprite id that
        /// fails to resolve is a content bug and gets the unmissable magenta square. Here a
        /// missing file is the normal case, not a bug - one painting exists and most environments
        /// will have none for a long time - so this falls through to GeneratedBackdrop, which is
        /// built from the same Environment palette and always renders. Nothing ever gets magenta
        /// or black from this path.
        /// </summary>
        public static Texture2D BackdropTexture(string environmentId)
        {
            string key = "backdrop_" + environmentId;
            Texture2D cached;
            if (TextureCache.TryGetValue(key, out cached)) return cached;

            Texture2D tex = Resources.Load<Texture2D>("Backgrounds/" + environmentId);
            if (tex == null) tex = GeneratedBackdrop(environmentId);
            TextureCache[key] = tex;
            return tex;
        }

        /// <summary>
        /// Stand-in backdrop for any environment with no painting yet: sky gradient above a
        /// horizon, ground below it, from that environment's own palette. This replaces the
        /// separate sky-gradient and arena-floor textures that used to dress a 3D sky billboard
        /// and ground plane; both are gone now that one backdrop quad carries the whole scene
        /// (see BattleStage.BuildWorld), so their two palettes are combined into this single
        /// image and every field of Environment is still used.
        ///
        /// Generated at 16:9 on purpose. BattleStage sizes the backdrop quad to cover the frame
        /// while preserving its texture's aspect ratio, so a tall thin gradient strip would be
        /// blown up enormously to cover the width. Matching the frame's own shape keeps the
        /// painted and generated paths through that same sizing code identical.
        /// </summary>
        private static Texture2D GeneratedBackdrop(string environmentId)
        {
            Environment env = GetEnvironment(environmentId);
            const int w = 320, h = 180;
            const float horizon = 0.62f;   // measured from the bottom, so ground takes the lower 62%

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[w * h];

            uint rng = 0x9E3779B9u;
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);
                Color row;
                if (t >= horizon)
                {
                    // sky: darkens with height, with the accent pooling just above the horizon
                    float k = (t - horizon) / (1f - horizon);
                    row = Color.Lerp(env.SkyBottom, env.SkyTop, Mathf.Pow(k, 0.7f));
                    row = Color.Lerp(row, env.Accent, Mathf.Pow(1f - k, 3.2f) * 0.35f);
                }
                else
                {
                    // ground: far and dim at the horizon, nearer and warmer at the bottom edge
                    float k = t / horizon;
                    row = Color.Lerp(env.GroundNear, env.GroundFar, Mathf.Pow(k, 0.8f));
                    row = Color.Lerp(row, env.Accent, Mathf.Pow(k, 6f) * 0.18f);
                }

                for (int x = 0; x < w; x++)
                {
                    rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                    float grit = ((rng & 0xFF) / 255f - 0.5f) * 0.05f;
                    pixels[y * w + x] = new Color(Mathf.Clamp01(row.r + grit),
                                                  Mathf.Clamp01(row.g + grit),
                                                  Mathf.Clamp01(row.b + grit), 1f);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
