using System;
using System.Collections.Generic;
using Emberfall.Core.Combat;
using Emberfall.Core.Json;
using Emberfall.Core.Progression;

namespace Emberfall.Core.Data
{
    /// <summary>
    /// The condition families of DESIGN-GUILD-FLOOR-CONDITIONS section 4. Appended, never
    /// inserted. Aspect is parsed so the file can name it, and refused at load: section 6 makes a
    /// single-aspect floor a separate decision, and until it is taken no condition may be drawn
    /// from that family.
    /// </summary>
    public enum FloorConditionFamily
    {
        Resource,
        Healing,
        Composition,
        EnemyOpening,
        Tempo,
        Aspect
    }

    /// <summary>
    /// What a line must be to be fielded on the floor. Checked before the fight, never inside it:
    /// CombatSimulation fights whatever it is handed, and a line that breaks the rule is refused by
    /// GuildDungeon.LineLegal before it is committed and kept off every board after.
    ///
    /// Every field is written as a CAP OR A BAN, never as a requirement. A rule that said "bring a
    /// Mender" or "bring a Tide hero" would be a hero check with the names filed off, which C6
    /// forbids; a cap only ever asks the player to leave something out.
    /// </summary>
    public sealed class LineRules
    {
        /// <summary>At most this many heroes. Zero is no cap.</summary>
        public int MaxHeroes;
        /// <summary>At most this many heroes sharing one role. Zero is no cap.</summary>
        public int MaxPerRole;
        /// <summary>At most this many stars on any one hero. Zero is no cap.</summary>
        public int MaxStars;
        /// <summary>Roles that may not be fielded at all.</summary>
        public readonly List<HeroRole> BannedRoles = new List<HeroRole>();

        public bool IsEmpty
        {
            get { return MaxHeroes <= 0 && MaxPerRole <= 0 && MaxStars <= 0 && BannedRoles.Count == 0; }
        }

        public static LineRules FromJson(JsonValue j)
        {
            LineRules r = new LineRules();
            if (j == null || !j.IsObject) return r;
            r.MaxHeroes = Math.Max(0, j.GetInt("maxHeroes", 0));
            r.MaxPerRole = Math.Max(0, j.GetInt("maxPerRole", 0));
            r.MaxStars = Math.Max(0, j.GetInt("maxStars", 0));
            List<JsonValue> banned = j.GetList("bannedRoles");
            for (int i = 0; i < banned.Count; i++)
            {
                HeroRole role;
                if (banned[i] != null && Enum.TryParse(banned[i].AsString(""), out role) && !r.BannedRoles.Contains(role))
                    r.BannedRoles.Add(role);
            }
            return r;
        }
    }

    /// <summary>
    /// ONE FLOOR CONDITION (DESIGN-GUILD-FLOOR-CONDITIONS). A sentence the player reads on the
    /// floor card, and the two things that make it true: the fight rules CombatSimulation plays
    /// under, and the line rules the line is checked against before it is committed.
    ///
    /// Content, in floor-conditions.json, so ContentDatabase.ContentVersion covers it (C4): a
    /// replay recorded under one condition set is refused under another, exactly as it is when a
    /// balance number moves.
    ///
    /// Written against engine primitives and floor themes only. Nothing here names an enemy
    /// template, because the templates a floor draws from are whatever the campaign fields and the
    /// campaign is about to be replaced.
    /// </summary>
    public sealed class FloorConditionDef
    {
        public string Id = "";
        public string Name = "";
        /// <summary>The one sentence on the floor card (C1). Numbers in it are checked against the
        /// rules by the harness, so the card cannot say one thing while the fight does another.</summary>
        public string Text = "";
        public FloorConditionFamily Family;

        /// <summary>The floor themes that may draw this condition. Empty: none may, and the loader
        /// warns - a condition no floor can draw is dead content. Which themes each condition keeps
        /// is the harness's reading, not a declaration (C5, section 7).</summary>
        public readonly List<GuildDungeonTheme> Themes = new List<GuildDungeonTheme>();

        public BattleRules Fight = new BattleRules();
        public LineRules Line = new LineRules();

        public bool AllowsTheme(GuildDungeonTheme theme) { return Themes.Contains(theme); }

        public static FloorConditionDef FromJson(JsonValue j)
        {
            FloorConditionDef c = new FloorConditionDef();
            if (j == null || !j.IsObject) return c;
            c.Id = j.GetString("id", "");
            c.Name = j.GetString("name", c.Id);
            c.Text = j.GetString("text", "");
            c.Family = EnumUtil.Parse(j.GetString("family", "Resource"), FloorConditionFamily.Resource);

            List<JsonValue> themes = j.GetList("floors");
            for (int i = 0; i < themes.Count; i++)
            {
                GuildDungeonTheme theme;
                if (themes[i] != null && Enum.TryParse(themes[i].AsString(""), out theme) && !c.Themes.Contains(theme))
                    c.Themes.Add(theme);
            }

            c.Fight = RulesFromJson(j["fight"], j.GetList("effects"));
            c.Fight.Name = c.Name;
            c.Line = LineRules.FromJson(j["line"]);
            return c;
        }

        private static BattleRules RulesFromJson(JsonValue f, List<JsonValue> effects)
        {
            BattleRules r = new BattleRules();
            if (f != null && f.IsObject)
            {
                r.HeroEnergyGain = f.GetFloat("heroEnergyGain", 1f);
                r.EnemyEnergyGain = f.GetFloat("enemyEnergyGain", 1f);
                r.HeroStartEnergy = f.GetInt("heroStartEnergy", 0);
                r.EnemyStartEnergy = f.GetInt("enemyStartEnergy", 0);
                r.HeroHealing = f.GetFloat("heroHealing", 1f);
                r.EnemyHealing = f.GetFloat("enemyHealing", 1f);
                r.HeroMaxHp = f.GetFloat("heroMaxHp", 1f);
                r.EnemyMaxHp = f.GetFloat("enemyMaxHp", 1f);
                r.EnemiesActFirstRounds = Math.Max(0, f.GetInt("enemiesActFirstRounds", 0));
            }
            for (int i = 0; effects != null && i < effects.Count; i++)
            {
                JsonValue e = effects[i];
                if (e == null || !e.IsObject || e["effect"] == null || !e["effect"].IsObject) continue;
                RuleEffect rule = new RuleEffect();
                rule.Side = EnumUtil.Parse(e.GetString("side", "Enemies"), RuleSide.Enemies);
                rule.Pick = EnumUtil.Parse(e.GetString("pick", "All"), RulePick.All);
                rule.EveryRound = e.GetBool("everyRound", false);
                rule.Effect = EffectDef.FromJson(e["effect"]);
                // The field is not an attacker: nothing it lays is resisted, rolled or dodged.
                rule.Effect.BypassResist = true;
                rule.Effect.Chance = 1f;
                r.Effects.Add(rule);
            }
            return r;
        }
    }
}
