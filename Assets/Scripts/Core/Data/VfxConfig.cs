using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Data
{
    /// <summary>
    /// One effect, as vfx.json authors it: either a flipbook sheet to play, or the numbers for the
    /// procedural burst that stands in until the sheet exists. Presentation only. Nothing here is
    /// read by a fight.
    /// </summary>
    public sealed class VfxEntry
    {
        public string Id = "";

        /// <summary>Resources path of a flipbook sheet, no extension ("Vfx/slash_wide"). Empty means
        /// no sheet is authored. A path that names a file that is not there is treated the same
        /// way, silently: that is the normal shape of "the art has not been drawn yet".</summary>
        public string Sheet = "";
        public int Columns = 1;
        public int Rows = 1;
        /// <summary>How many cells of the grid are frames, reading left to right then top to
        /// bottom. 0 means every cell.</summary>
        public int Frames = 0;
        /// <summary>The first frame played; the frames before it are skipped. 0 plays the whole
        /// sheet. heal and buff use 4 because frames 0 to 3 of heal_ring.png are the source art's
        /// core flash: frame 2 is a solid filled circle, and frame 3 still has a solid core, which
        /// shows as an opaque green disc over a hero's torso. Frames 4 to 7 are the ring.</summary>
        public int Start = 0;
        public float Fps = 24f;
        /// <summary>World size of the sheet's quad.</summary>
        public float Scale = 1.6f;
        /// <summary>True multiplies the sheet by the attacker's aspect accent. False plays it as
        /// drawn.</summary>
        public bool TintByAspect = true;

        /// <summary>Procedural fallback, by shape: shards, slash, bolt, burst or implode. These are
        /// neutral shapes tinted by aspect, never element-literal (DESIGN-ART-BIBLE section 4).</summary>
        public string Shape = "shards";
        public int Chips = 7;
        public int Streaks = 0;
        public float Speed = 2.6f;
        public float Size = 0.17f;
        public float Life = 0.38f;
        /// <summary>Multiplier on the expanding ring's size. 0 draws no ring.</summary>
        public float Ring = 0f;

        public bool HasSheet { get { return !string.IsNullOrEmpty(Sheet); } }

        public int FrameCount
        {
            get
            {
                int cells = Columns * Rows;
                if (cells < 1) return 1;
                return Frames > 0 && Frames <= cells ? Frames : cells;
            }
        }
    }

    /// <summary>
    /// vfx.json: which effect a hit plays.
    ///
    /// NOT PART OF ContentVersion, and that is the whole reason it is its own file with its own
    /// loader instead of a field in heroes.json. Every hashed content file retires every recorded
    /// guild dungeon replay when it is edited. Which spray of sparks a sword makes cannot change
    /// what a fight does, so editing it must not cost anyone a replay. Nothing here is handed to
    /// ContentDatabase.Load; it is read by the battle view alone, through VfxDirector.
    ///
    /// Two tables, resolved in this order for a damage event:
    ///   abilities  keyed by ability id (BattleEvent.AbilityId). A sheet goes here.
    ///   types      keyed by the ability's authored vfx tag (BattleEvent.Vfx: impact, slash, bolt,
    ///              burst, shadow...). The procedural fallback by damage type.
    /// An entry that names a sheet which exists plays the flipbook. Otherwise the same entry's
    /// procedural numbers play, and an ability with no entry at all falls through to its tag.
    /// </summary>
    public sealed class VfxConfig
    {
        public readonly Dictionary<string, VfxEntry> Abilities = new Dictionary<string, VfxEntry>();
        public readonly Dictionary<string, VfxEntry> Types = new Dictionary<string, VfxEntry>();
        public readonly List<string> Warnings = new List<string>();

        /// <summary>False when the file was absent or unreadable. The director still plays: it
        /// uses the built-in default entry, so a build without vfx.json is plain rather than
        /// broken.</summary>
        public bool IsUsable;

        /// <summary>The R12 hit-feedback numbers and their flag (vfx.json "juice"). Legacy when the
        /// file or the block is absent, or when the block says "enabled": false.</summary>
        public JuiceProfile Juice = JuiceProfile.Legacy;

        public const string FallbackType = "impact";

        public VfxEntry ForAbility(string abilityId)
        {
            VfxEntry found;
            return !string.IsNullOrEmpty(abilityId) && Abilities.TryGetValue(abilityId, out found) ? found : null;
        }

        public VfxEntry ForType(string tag)
        {
            VfxEntry found;
            if (!string.IsNullOrEmpty(tag) && Types.TryGetValue(tag, out found)) return found;
            return Types.TryGetValue(FallbackType, out found) ? found : null;
        }

        public static VfxConfig FromJson(string text)
        {
            VfxConfig c = new VfxConfig();
            if (string.IsNullOrEmpty(text)) return c;

            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { c.Warnings.Add("vfx.json: " + err); return c; }

            ReadTable(root["abilities"], c.Abilities, c);
            ReadTable(root["types"], c.Types, c);
            c.Juice = JuiceProfile.FromJson(root["juice"]);
            c.IsUsable = true;
            return c;
        }

        private static void ReadTable(JsonValue table, Dictionary<string, VfxEntry> into, VfxConfig c)
        {
            foreach (string key in table.Keys)
            {
                JsonValue j = table[key];
                VfxEntry e = new VfxEntry();
                e.Id = key;
                e.Sheet = j.GetString("sheet", "");
                e.Columns = j.GetInt("columns", 1);
                e.Rows = j.GetInt("rows", 1);
                e.Frames = j.GetInt("frames", 0);
                e.Start = j.GetInt("start", 0);
                e.Fps = j.GetFloat("fps", 24f);
                e.Scale = j.GetFloat("scale", 1.6f);
                e.TintByAspect = j.GetBool("tintByAspect", true);
                e.Shape = j.GetString("shape", "shards");
                e.Chips = j.GetInt("chips", 7);
                e.Streaks = j.GetInt("streaks", 0);
                e.Speed = j.GetFloat("speed", 2.6f);
                e.Size = j.GetFloat("size", 0.17f);
                e.Life = j.GetFloat("life", 0.38f);
                e.Ring = j.GetFloat("ring", 0f);

                if (e.Columns < 1 || e.Rows < 1)
                {
                    c.Warnings.Add("vfx.json: " + key + " has a sheet grid smaller than 1x1");
                    e.Columns = 1;
                    e.Rows = 1;
                }
                if (e.Fps <= 0f) e.Fps = 24f;
                if (e.Start < 0 || e.Start >= e.FrameCount)
                {
                    c.Warnings.Add("vfx.json: " + key + " starts outside its frames, playing from frame 0");
                    e.Start = 0;
                }
                into[key] = e;
            }
        }
    }
}
