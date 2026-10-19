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
        /// THE ONE PLACEHOLDER FILE. Every hero whose art is not drawn yet renders this mannequin,
        /// tinted by aspect, instead of its own sheet - so an undrawn roster reads as "not drawn
        /// yet" rather than as ninety-four broken sprites, and an artist who wants a better
        /// placeholder replaces this single PNG and every one of them changes at once.
        ///
        /// It is a real file under Resources like any other sprite, not a generated texture,
        /// precisely so it can be replaced without a code change. Tools/make_placeholder_dummy.py
        /// is how the current one was drawn and is the thing to re-run if the measurements below
        /// need to change.
        /// </summary>
        public const string DummySpriteId = "placeholder_dummy";

        /// <summary>
        /// The dummy's own measurements, the same two numbers heroes.json carries per hero, and
        /// they are MEASURED OFF THE FILE rather than intended for it - make_placeholder_dummy.py
        /// crops the drawing to its rendered alpha and seats it at exactly these fractions, then
        /// prints them back from the saved PNG. BuildSprite treats them as an instruction, so a
        /// file that disagreed with them would stand at the wrong height on the wrong ground line
        /// and nothing would say so.
        ///
        /// The values themselves are the MEDIAN of the thirty-one delivered heroes (body 0.558 of
        /// range 0.508-0.827, foot 0.220 of range 0.096-0.238), which is what makes a line of
        /// drawn and undrawn heroes keep its proportions instead of the dummies standing tall or
        /// sunk. 0.219 rather than 0.220 because 0.220 x 260px is 57.2 and a pixel is a pixel.
        ///
        /// IF THE PNG IS REPLACED these must be re-measured. Re-running the generator prints them.
        /// </summary>
        public const float DummyBodyFraction = 0.558f;
        public const float DummyFootFraction = 0.219f;

        /// <summary>
        /// The placeholder mannequin, drawn white so a tint multiplies cleanly through it.
        ///
        /// Unlike UnitSprite below, reaching this is NOT an error - it is the expected path for
        /// most of the roster. The file going missing IS an error, and gets the magenta square,
        /// because at that point there is no placeholder left to place.
        /// </summary>
        public static Texture2D DummyUnitSprite()
        {
            string key = "unitsprite_dummy";
            Texture2D cached;
            if (TextureCache.TryGetValue(key, out cached)) return cached;

            Texture2D tex = Resources.Load<Texture2D>("Sprites/Units/" + DummySpriteId);
            if (tex == null)
            {
                Debug.LogError("Emberfall: the placeholder mannequin is missing from "
                               + "Resources/Sprites/Units/" + DummySpriteId + ".png. Every hero "
                               + "without art depends on it; run Tools/make_placeholder_dummy.py.");
                tex = MissingSpriteTexture;
            }
            TextureCache[key] = tex;
            return tex;
        }

        /// <summary>
        /// Loads Resources/Sprites/Units/{spriteId}.png for the sprite rig path. Cached like the
        /// generated textures below, so repeat battles with the same unit don't re-hit Resources.
        ///
        /// REACHING THE FALLBACK HERE IS A BUG, AND SAYS SO. A unit only gets this far if its data
        /// claims it has art - VisualDef.Sprite is set and VisualDef.SpriteStatus is not "pending"
        /// - so the file failing to load means it is missing, misnamed, or outside Resources. That
        /// is a content bug, it gets a LogError naming the unit and the path it looked in, and it
        /// keeps the flat magenta square, which is deliberately impossible to mistake for art.
        ///
        /// This used to be a LogWarning, and it used to be the ONLY thing that happened when a
        /// hero had no art - which was every hero, because most of the roster is undrawn. Ninety-
        /// odd warnings a battle is the same as none: the one real failure would have sat in the
        /// middle of them unread. Undrawn heroes are now declared as such in heroes.json and never
        /// arrive here (see VisualDef.SpriteStatus), which is what makes it worth shouting about
        /// the ones that do.
        /// </summary>
        /// <param name="unitLabel">Who to blame. The unit's display name or def id - a bare sprite
        /// path is not enough to find the entry that names it.</param>
        public static Texture2D UnitSprite(string spriteId, string unitLabel)
        {
            string key = "unitsprite_" + spriteId;
            Texture2D cached;
            if (TextureCache.TryGetValue(key, out cached)) return cached;

            Texture2D tex = Resources.Load<Texture2D>("Sprites/Units/" + spriteId);
            if (tex == null)
            {
                Debug.LogError("Emberfall: BROKEN SPRITE - " + (string.IsNullOrEmpty(unitLabel) ? "a unit" : unitLabel)
                               + " declares art but Resources/Sprites/Units/" + spriteId
                               + ".png does not load. Either deliver the file or mark the unit"
                               + " \"spriteStatus\": \"pending\" in its content entry.");
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
        /// PAINTED TODAY: "ashen", "ember", "hollow" (chapter 1, four stages each) and chapters 2
        /// to 6 as "crossing", "city", "old_kingdom", "divided_realm" and "sanctuary", the ids
        /// campaign.json authors for those chapters.
        ///
        /// NAMED BUT UNPAINTED, falling through to GeneratedBackdrop below: "proving" (The
        /// Proving), "deep" (the guild dungeon) and the recital ids "tidal" and "coast". Each
        /// becomes a real painting by dropping a file at Assets/Resources/Backgrounds/{id}.png
        /// with NO code change of any kind. That is the whole point of keying this on the id
        /// campaign.json already authors. A boss variant is the same drop: stage 12 of each chapter
        /// 2 to 6 names "{id}_boss" in campaign.json, and Backgrounds/{id}_boss.png is its painting. Nothing
        /// else keys on the environment id (GetEnvironment's default arm gives "crossing_boss" the palette
        /// "crossing" always had), so no fallback to the base id is needed.
        ///
        /// A TRAP WORTH KNOWING, because it makes the fallback quieter than it sounds. The
        /// generated backdrop is built from GetEnvironment(id), and GetEnvironment only has cases
        /// for "ember", "hollow" and "proving" - everything else, including an id that has no
        /// painting, hits its default arm and is drawn in ashen's palette. A palette is an
        /// art-direction decision rather than a mechanical one, so an unpainted id does not get
        /// "its own" colours. "proving" was given a case precisely so it would not join them.
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
