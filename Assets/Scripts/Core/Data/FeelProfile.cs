using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Data
{
    /// <summary>
    /// Every number of the R15 feel pass, in one object, so the switch and the rollback are one
    /// value. Presentation only: it lives in vfx.json ("feelPreset" at the top level and a "v2"
    /// block), which is NOT part of ContentVersion, and nothing in a fight reads it.
    ///
    /// TWO PROFILES.
    ///   V1   what the battle did after R12. "feelPreset": "v1", or no feelPreset key at all (what an
    ///        old file meant), resolves to exactly this, and every field holds the R12 value it
    ///        replaces (a zero or an empty string where R12 had no such thing). V2 false means "no
    ///        new behaviour, the R12 code path runs".
    ///   V2   the numbers the pass ships. FromJson starts here and lets the "v2" block override any
    ///        field; anything the block leaves out keeps the shipped value.
    ///
    /// The R12 numbers themselves stay in the R12 profile, untouched. This object only says what the
    /// feel pass changes on top of it, so v1 is R12 without a refactor of it. See FeelMath for the
    /// decisions that read this.
    /// </summary>
    public sealed class FeelProfile
    {
        public const string PresetKey = "feelPreset";
        public const string BlockKey = "v2";
        public const string PresetV1 = "v1";
        public const string PresetV2 = "v2";

        /// <summary>False is the rollback: every consumer takes its R12 path.</summary>
        public bool V2;

        // ---- hit flash. A short tint toward white, not a fill. Seconds is how long a hit's flash
        // takes to fall to zero, counted in play time that hit stop does not pause.
        public float FlashPeakNormal = 0.35f;
        public float FlashPeakCrit = 0.55f;
        public float FlashSeconds = 0.06f;

        // ---- VFX restraint and one look per event class. Sizes are world units, seconds are play
        // time at 1x (the director divides by the playback speed). 0 live or 0 per unit is no cap.
        /// <summary>Most effects alive at once on one unit; a stronger class retires the oldest
        /// weaker one, a weaker one is dropped. On top of the global quad cap.</summary>
        public int VfxMaxPerUnit = 2;
        /// <summary>Most v2 effect quads alive at once, all units.</summary>
        public int VfxMaxLive = 14;

        // basic hit: one small soft puff at the strike point, no ring, no streak, no sheet.
        public float HitPuffSize = 0.5f;
        public float HitPuffSeconds = 0.14f;
        /// <summary>How far the puff colour is pulled toward white (0 is the pure aspect accent).</summary>
        public float PuffWhite = 0.35f;
        // crit: the same puff, bigger and a touch longer, with a soft glow.
        public float CritPuffSize = 1.25f;
        /// <summary>Where a crit puff starts, as a fraction of its size, so even its first frames read big.</summary>
        public float CritPuffStart = 0.75f;
        public float CritPuffSeconds = 0.20f;
        public float CritGlowRadius = 0.8f;

        // skill hit: the softest existing sheet that reads as magic, larger and slower than a hit.
        public string SkillSheet = "Art/Vfx/burst_puff";
        public int SkillColumns = 4;
        public int SkillRows = 4;
        public int SkillFrames = 16;
        public float SkillFps = 30f;
        public float SkillScale = 2.4f;
        /// <summary>The skill moment's glow behind the caster replaces the R12 geometric ring.</summary>
        public float SkillGlowRadius = 1.0f;
        public float SkillGlowAlpha = 0.5f;
        /// <summary>A soft glow on the caster for the cast itself, in the caster's accent.</summary>
        public float CastGlowRadius = 0.9f;
        public float CastGlowSeconds = 0.30f;

        // heal: a soft glow and a few chips rising off the target.
        public float HealGlowRadius = 0.85f;
        public float HealGlowSeconds = 0.45f;
        public int HealSparks = 7;
        public float HealSparkRise = 1.6f;
        public float HealSparkSeconds = 0.9f;
        public float HealSparkSize = 0.3f;

        // buff and debuff: a pulse that travels up or down the body, and an arrow chip.
        public float PulseSize = 1.0f;
        public float PulseSeconds = 0.42f;
        public float PulseTravel = 0.8f;
        public float ArrowSize = 0.5f;
        public float ArrowSeconds = 0.8f;
        public float ArrowTravel = 0.7f;
        /// <summary>A debuff darkens the target body for a moment: how far toward the dark tint, and for how long.</summary>
        public float DebuffTint = 0.45f;
        public float DebuffTintSeconds = 0.30f;

        // shield: a short shell flash around the body (alpha scales it for an absorb or a break).
        public float ShellSeconds = 0.26f;
        public float ShellAlpha = 0.85f;
        public float ShellAbsorbAlpha = 0.55f;

        // ---- damage numbers
        /// <summary>Multiplier on the size of every floating number. 1 is R12.</summary>
        public float NumberScale = 1.25f;

        // ---- sound restraint
        /// <summary>Most sfx voices sounding at once. 0 keeps the pool audio.json asks for.</summary>
        public int AudioSfxVoices = 6;
        private readonly List<string> _singleSound = new List<string> { "battle.hit" };

        private JsonValue _block;

        /// <summary>The raw "v2" object (null for V1 or when absent), so a sibling pass can read a
        /// sub-object of its own without this file growing a field for it. Not a field on purpose:
        /// the harness compares profiles field by field.</summary>
        public JsonValue Block { get { return _block; } }

        /// <summary>Cues that play their main sound alone, layers skipped (a basic hit).</summary>
        public IList<string> SingleSoundCues { get { return _singleSound; } }

        public bool IsSingleSound(string cueId)
        {
            return V2 && !string.IsNullOrEmpty(cueId) && _singleSound.Contains(cueId);
        }

        /// <summary>What the battle did after R12 and before this pass.</summary>
        public static FeelProfile V1
        {
            get
            {
                FeelProfile p = new FeelProfile();
                p.V2 = false;
                p.FlashPeakNormal = 1f;
                p.FlashPeakCrit = 1f;
                p.FlashSeconds = 0.08f;
                p.VfxMaxPerUnit = 0;
                p.VfxMaxLive = 0;
                p.HitPuffSize = 0f;
                p.HitPuffSeconds = 0f;
                p.PuffWhite = 0f;
                p.CritPuffSize = 0f;
                p.CritPuffSeconds = 0f;
                p.CritGlowRadius = 0f;
                p.CritPuffStart = 0f;
                p.SkillSheet = "";
                p.SkillColumns = 1;
                p.SkillRows = 1;
                p.SkillFrames = 0;
                p.SkillFps = 0f;
                p.SkillScale = 0f;
                p.SkillGlowRadius = 0f;
                p.SkillGlowAlpha = 0f;
                p.CastGlowRadius = 0f;
                p.CastGlowSeconds = 0f;
                p.HealGlowRadius = 0f;
                p.HealGlowSeconds = 0f;
                p.HealSparks = 0;
                p.HealSparkRise = 0f;
                p.HealSparkSeconds = 0f;
                p.HealSparkSize = 0f;
                p.PulseSize = 0f;
                p.PulseSeconds = 0f;
                p.PulseTravel = 0f;
                p.ArrowSize = 0f;
                p.ArrowSeconds = 0f;
                p.ArrowTravel = 0f;
                p.DebuffTint = 0f;
                p.DebuffTintSeconds = 0f;
                p.ShellSeconds = 0f;
                p.ShellAlpha = 0f;
                p.ShellAbsorbAlpha = 0f;
                p.NumberScale = 1f;
                p.AudioSfxVoices = 0;
                p._singleSound.Clear();
                return p;
            }
        }

        /// <summary>The numbers the pass ships, with the switch on.</summary>
        public static FeelProfile Shipped
        {
            get { FeelProfile p = new FeelProfile(); p.V2 = true; return p; }
        }

        /// <summary>The preset vfx.json asks for: "v2" only when the top-level key says so. A missing
        /// key, a key that is not a string and any other word all read "v1", the safe side.</summary>
        public static string PresetOf(JsonValue root)
        {
            if (root == null || !root.IsObject) return PresetV1;
            return root.GetString(PresetKey, PresetV1) == PresetV2 ? PresetV2 : PresetV1;
        }

        /// <summary>The v2 profile from the file's "v2" block: every field starts at Shipped and the
        /// block overrides it. An absent block is Shipped. Always built, whatever the preset says,
        /// so a capture or an env var can ask for v2 while the file says v1.</summary>
        public static FeelProfile V2From(JsonValue root)
        {
            FeelProfile p = Shipped;
            if (root == null || !root.IsObject) return p;
            JsonValue b = root[BlockKey];
            if (b == null || !b.IsObject) return p;
            p._block = b;

            JsonValue fl = b["flash"];
            p.FlashPeakNormal = fl.GetFloat("peakNormal", p.FlashPeakNormal);
            p.FlashPeakCrit = fl.GetFloat("peakCrit", p.FlashPeakCrit);
            p.FlashSeconds = fl.GetFloat("seconds", p.FlashSeconds);

            JsonValue vx = b["vfx"];
            p.VfxMaxPerUnit = vx.GetInt("maxPerUnit", p.VfxMaxPerUnit);
            p.VfxMaxLive = vx.GetInt("maxLive", p.VfxMaxLive);
            p.PuffWhite = vx.GetFloat("puffWhite", p.PuffWhite);
            JsonValue hit = vx["hit"];
            p.HitPuffSize = hit.GetFloat("size", p.HitPuffSize);
            p.HitPuffSeconds = hit.GetFloat("seconds", p.HitPuffSeconds);
            JsonValue crit = vx["crit"];
            p.CritPuffSize = crit.GetFloat("size", p.CritPuffSize);
            p.CritPuffSeconds = crit.GetFloat("seconds", p.CritPuffSeconds);
            p.CritGlowRadius = crit.GetFloat("glowRadius", p.CritGlowRadius);
            p.CritPuffStart = crit.GetFloat("start", p.CritPuffStart);
            JsonValue sk = vx["skill"];
            p.SkillSheet = sk.GetString("sheet", p.SkillSheet);
            p.SkillColumns = sk.GetInt("columns", p.SkillColumns);
            p.SkillRows = sk.GetInt("rows", p.SkillRows);
            p.SkillFrames = sk.GetInt("frames", p.SkillFrames);
            p.SkillFps = sk.GetFloat("fps", p.SkillFps);
            p.SkillScale = sk.GetFloat("scale", p.SkillScale);
            p.SkillGlowRadius = sk.GetFloat("glowRadius", p.SkillGlowRadius);
            p.SkillGlowAlpha = sk.GetFloat("glowAlpha", p.SkillGlowAlpha);
            p.CastGlowRadius = sk.GetFloat("castGlowRadius", p.CastGlowRadius);
            p.CastGlowSeconds = sk.GetFloat("castGlowSeconds", p.CastGlowSeconds);
            JsonValue he = vx["heal"];
            p.HealGlowRadius = he.GetFloat("glowRadius", p.HealGlowRadius);
            p.HealGlowSeconds = he.GetFloat("glowSeconds", p.HealGlowSeconds);
            p.HealSparks = he.GetInt("sparks", p.HealSparks);
            p.HealSparkRise = he.GetFloat("rise", p.HealSparkRise);
            p.HealSparkSeconds = he.GetFloat("seconds", p.HealSparkSeconds);
            p.HealSparkSize = he.GetFloat("size", p.HealSparkSize);
            JsonValue pu = vx["pulse"];
            p.PulseSize = pu.GetFloat("size", p.PulseSize);
            p.PulseSeconds = pu.GetFloat("seconds", p.PulseSeconds);
            p.PulseTravel = pu.GetFloat("travel", p.PulseTravel);
            p.ArrowSize = pu.GetFloat("arrowSize", p.ArrowSize);
            p.ArrowSeconds = pu.GetFloat("arrowSeconds", p.ArrowSeconds);
            p.ArrowTravel = pu.GetFloat("arrowTravel", p.ArrowTravel);
            p.DebuffTint = pu.GetFloat("debuffTint", p.DebuffTint);
            p.DebuffTintSeconds = pu.GetFloat("debuffTintSeconds", p.DebuffTintSeconds);
            JsonValue sh = vx["shield"];
            p.ShellSeconds = sh.GetFloat("seconds", p.ShellSeconds);
            p.ShellAlpha = sh.GetFloat("alpha", p.ShellAlpha);
            p.ShellAbsorbAlpha = sh.GetFloat("absorbAlpha", p.ShellAbsorbAlpha);

            p.NumberScale = b["numbers"].GetFloat("scale", p.NumberScale);

            JsonValue au = b["audio"];
            p.AudioSfxVoices = au.GetInt("sfxVoices", p.AudioSfxVoices);
            List<JsonValue> single = au.GetList("singleSound");
            if (au.Has("singleSound"))
            {
                p._singleSound.Clear();
                for (int i = 0; i < single.Count; i++)
                {
                    string cue = single[i].AsString("");
                    if (cue.Length > 0) p._singleSound.Add(cue);
                }
            }
            return p;
        }
    }
}
