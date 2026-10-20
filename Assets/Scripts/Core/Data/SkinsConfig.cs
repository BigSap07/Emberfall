using System;
using System.Collections.Generic;
using System.Globalization;
using Emberfall.Core.Json;

namespace Emberfall.Core.Data
{
    /// <summary>How a skin reaches a player. Closed: a file naming another is dropped with a warning.</summary>
    public enum SkinSource
    {
        /// <summary>Sold in the Store, at a price set by its rarity (store.json's skinTiers).</summary>
        Store,
        /// <summary>Not sold. Free from an event track: the event's reward names the skin id and the grant goes
        /// through PurchaseGrants.Apply like everything else a skin can arrive by.</summary>
        Event,
        /// <summary>A test skin: not sold, only given by the harness and the debug paths. See SkinDef.Test.</summary>
        Test
    }

    /// <summary>
    /// One skin: a look for ONE hero, plus a small stat bonus on that hero only (Idle Heroes style).
    /// Cosmetic first. The bonus is a percent of the stat the hero's own level, stars, relic and armor leave it
    /// (HeroSkins.Apply), never more than SkinsConfig.MaxBonusPct in any stat, and one skin is worn per hero so
    /// bonuses cannot stack.
    /// </summary>
    public sealed class SkinDef
    {
        public string Id = "";
        public string Name = "";
        /// <summary>The hero this skin is for. A skin changes no other hero.</summary>
        public string HeroId = "";
        /// <summary>rare, epic or legendary: sets the Store price tier (StoreConfig.SkinTiers). Display and price only.</summary>
        public string Rarity = "rare";
        public SkinSource Source = SkinSource.Store;
        public string Blurb = "";

        /// <summary>Percent points of bonus, 2 means +2 percent. Zero when the skin does not touch the stat.</summary>
        public double AttackPct;
        public double HpPct;
        public double DefensePct;

        /// <summary>A TEST skin proves the system before any art exists: it is a runtime hue shift of the hero's own
        /// sprite and portrait. Hidden outside the editor and a development build (SkinsConfig.TestVisible), never
        /// sold, never read by a fight in a release build.</summary>
        public bool Test;
        /// <summary>Degrees of hue to rotate the hero's own art by when the skin has no art of its own. 0 for a skin
        /// with delivered art.</summary>
        public float Hue;
        /// <summary>The portrait this skin shows: Resources/Sprites/Portraits/{Portrait}.png once delivered. Empty means the
        /// hero's own portrait, hue shifted by Hue.</summary>
        public string Portrait = "";
        /// <summary>The battle sprite this skin shows: Resources/Sprites/Units/{Sprite}.png once delivered. Empty means
        /// the hero's own sprite, hue shifted by Hue.</summary>
        public string Sprite = "";

        public bool HasBonus
        {
            get { return AttackPct > 0.0 || HpPct > 0.0 || DefensePct > 0.0; }
        }

        /// <summary>The bonus in words: "+2% Attack, +3% HP".</summary>
        public string BonusLine()
        {
            List<string> parts = new List<string>();
            if (AttackPct > 0.0) parts.Add("+" + Pct(AttackPct) + "% Attack");
            if (HpPct > 0.0) parts.Add("+" + Pct(HpPct) + "% HP");
            if (DefensePct > 0.0) parts.Add("+" + Pct(DefensePct) + "% Defense");
            return parts.Count == 0 ? "Cosmetic only" : string.Join(", ", parts.ToArray());
        }

        private static string Pct(double v)
        {
            return v.ToString("0.#", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// skins.json (R24, Docs/DESIGN-STORE.md): the skins and their bonuses, data only.
    ///
    /// THE CEILING. No stat of any skin may exceed MaxBonusPct (5 percent), whatever a file says: the constant
    /// HardCapPct is the ceiling the validator enforces and a file can only ask for less. A skin that asks for more
    /// is DROPPED with a warning at load, not clamped, so a typo cannot ship a 50 percent skin as a 5 percent one.
    ///
    /// NOT HASHED INTO ContentDatabase.ContentVersion. Every fight that is RECORDED and replayed reads no skin: the
    /// guild dungeon builds its player side from a scratch hero that wears none and an empty profile that owns none
    /// (GuildDungeon.FightUnder), and a fight against a snapshot opponent (The Proving, friend duels) is excluded by
    /// HeroSkins.AppliesTo so two identical lines cannot differ by a purchase. So no recorded replay can depend on
    /// this file, and hashing it would retire every one of them for nothing. CheckSkinsNotHashed and
    /// CheckSkinsReplaysUntouched prove both halves. Loaded through ContentDatabase.LoadSkins after Load.
    ///
    /// ZERO SKINS IS A WORKING SYSTEM: an empty or absent file means no skin is sold, equipped or read.
    /// </summary>
    public sealed class SkinsConfig
    {
        /// <summary>No stat of any skin may be higher than this, in percent points. Enforced at load.</summary>
        public const double HardCapPct = 5.0;
        public const double DefaultAttackPct = 2.0;
        public const double DefaultHpPct = 3.0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Test skins are visible in the editor and a development build.</summary>
        public static bool TestVisible = true;
#else
        /// <summary>Test skins are hidden in a release build: not listed, not sold, not equipped, not read by a fight.
        /// Writable so the harness can show both sides; whatever sets it puts it back.</summary>
        public static bool TestVisible = false;
#endif

        public double MaxBonusPct = HardCapPct;
        public readonly List<SkinDef> Skins = new List<SkinDef>();
        public readonly List<string> Warnings = new List<string>();

        public bool IsUsable
        {
            get { return Skins.Count > 0; }
        }

        /// <summary>True when the skin may exist for a player in this build: every skin but a test skin in a release build.</summary>
        public static bool Visible(SkinDef skin)
        {
            return skin != null && (!skin.Test || TestVisible);
        }

        /// <summary>The skin with this id, or null when there is none or it is hidden in this build.</summary>
        public SkinDef Skin(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Skins.Count; i++) if (Skins[i].Id == id) return Visible(Skins[i]) ? Skins[i] : null;
            return null;
        }

        /// <summary>Every skin of one hero that exists in this build, in file order.</summary>
        public List<SkinDef> SkinsOf(string heroId)
        {
            List<SkinDef> result = new List<SkinDef>();
            for (int i = 0; i < Skins.Count; i++)
                if (Skins[i].HeroId == heroId && Visible(Skins[i])) result.Add(Skins[i]);
            return result;
        }

        /// <summary>The skins the Store sells in this build: source Store, not hidden.</summary>
        public List<SkinDef> Sellable()
        {
            List<SkinDef> result = new List<SkinDef>();
            for (int i = 0; i < Skins.Count; i++)
                if (Skins[i].Source == SkinSource.Store && Visible(Skins[i])) result.Add(Skins[i]);
            return result;
        }

        public static SkinsConfig FromJson(string text)
        {
            SkinsConfig c = new SkinsConfig();
            if (string.IsNullOrEmpty(text)) return c;
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { c.Warnings.Add("skins.json: " + err); return c; }

            double max = root["maxBonusPct"].AsDouble(HardCapPct);
            c.MaxBonusPct = Math.Max(0.0, Math.Min(HardCapPct, max));
            if (max > HardCapPct) c.Warnings.Add("skins.json: maxBonusPct " + max.ToString("0.#", CultureInfo.InvariantCulture) + " is above the ceiling of " + HardCapPct.ToString("0.#", CultureInfo.InvariantCulture) + " and is lowered to it");

            JsonValue defaults = root["defaults"];
            double defAttack = defaults != null && defaults.IsObject ? defaults["attackPct"].AsDouble(DefaultAttackPct) : DefaultAttackPct;
            double defHp = defaults != null && defaults.IsObject ? defaults["hpPct"].AsDouble(DefaultHpPct) : DefaultHpPct;
            double defDefense = defaults != null && defaults.IsObject ? defaults["defensePct"].AsDouble(0.0) : 0.0;

            List<JsonValue> skins = root.GetList("skins");
            for (int i = 0; i < skins.Count; i++)
            {
                SkinDef s = ReadSkin(skins[i], c, i + 1, defAttack, defHp, defDefense);
                if (s == null) continue;
                bool dup = false;
                for (int k = 0; k < c.Skins.Count; k++) if (c.Skins[k].Id == s.Id) dup = true;
                if (dup) { c.Warnings.Add("skins.json: skin '" + s.Id + "' is listed twice and the second is dropped"); continue; }
                c.Skins.Add(s);
            }
            return c;
        }

        private static SkinDef ReadSkin(JsonValue j, SkinsConfig c, int number, double defAttack, double defHp, double defDefense)
        {
            SkinDef s = new SkinDef();
            s.Id = j.GetString("id", "");
            string where = "skins.json: skin " + number + (s.Id.Length > 0 ? " '" + s.Id + "'" : "");
            if (s.Id.Length == 0) { c.Warnings.Add(where + " has no id and is dropped"); return null; }
            s.HeroId = j.GetString("hero", "");
            if (s.HeroId.Length == 0) { c.Warnings.Add(where + " names no hero and is dropped"); return null; }
            s.Name = j.GetString("name", s.Id);
            s.Blurb = j.GetString("blurb", "");
            s.Rarity = j.GetString("rarity", "rare");
            if (s.Rarity != "rare" && s.Rarity != "epic" && s.Rarity != "legendary")
            {
                c.Warnings.Add(where + " has unknown rarity '" + s.Rarity + "' and is dropped");
                return null;
            }
            string source = j.GetString("source", "store");
            switch (source)
            {
                case "store": s.Source = SkinSource.Store; break;
                case "event": s.Source = SkinSource.Event; break;
                case "test": s.Source = SkinSource.Test; break;
                default: c.Warnings.Add(where + " has unknown source '" + source + "' and is dropped"); return null;
            }
            s.Test = j.GetBool("test", false) || s.Source == SkinSource.Test;
            if (s.Test) s.Source = SkinSource.Test;
            s.AttackPct = j["attackPct"].AsDouble(defAttack);
            s.HpPct = j["hpPct"].AsDouble(defHp);
            s.DefensePct = j["defensePct"].AsDouble(defDefense);
            s.Hue = (float)j["hue"].AsDouble(0.0);
            s.Portrait = j.GetString("portrait", "");
            s.Sprite = j.GetString("sprite", "");

            // THE CEILING, per stat, in the file's own words. Dropped, not clamped.
            string stat = null;
            if (s.AttackPct < 0.0 || s.AttackPct > c.MaxBonusPct) stat = "attackPct";
            else if (s.HpPct < 0.0 || s.HpPct > c.MaxBonusPct) stat = "hpPct";
            else if (s.DefensePct < 0.0 || s.DefensePct > c.MaxBonusPct) stat = "defensePct";
            if (stat != null)
            {
                c.Warnings.Add(where + " has " + stat + " outside 0 to " + c.MaxBonusPct.ToString("0.#", CultureInfo.InvariantCulture) + " percent and is dropped");
                return null;
            }
            return s;
        }

        /// <summary>Drops what this content cannot honour: a skin for a hero the roster does not have. Adds the warnings.
        /// Called by ContentDatabase.LoadSkins.</summary>
        public void Validate(ContentDatabase db)
        {
            for (int i = Skins.Count - 1; i >= 0; i--)
            {
                if (db.GetHero(Skins[i].HeroId) != null) continue;
                Warnings.Add("skins.json: skin '" + Skins[i].Id + "' names hero '" + Skins[i].HeroId + "' that does not exist and is dropped");
                Skins.RemoveAt(i);
            }
        }
    }
}
