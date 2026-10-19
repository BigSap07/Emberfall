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
    ///        replaces. V2 false means "no new behaviour, the R12 code path runs".
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

        private JsonValue _block;

        /// <summary>The raw "v2" object (null for V1 or when absent), so a sibling pass can read a
        /// sub-object of its own without this file growing a field for it. Not a field on purpose:
        /// the harness compares profiles field by field.</summary>
        public JsonValue Block { get { return _block; } }

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
            return p;
        }
    }
}
