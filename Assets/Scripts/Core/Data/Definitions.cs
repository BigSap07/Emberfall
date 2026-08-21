using System;
using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Data
{
    /// <summary>Colour / silhouette recipe used by the procedural character builder.</summary>
    public sealed class VisualDef
    {
        public string Primary = "8A6BFF";
        public string Secondary = "3A2E63";
        public string Accent = "FFD36B";
        public string Silhouette = "humanoid"; // humanoid | brute | beast | wisp | golem
        public string Weapon = "sword";        // sword | greatsword | spear | staff | dagger | orb | hammer | claws | none
        public float Scale = 1f;
        public int Sigil = 0;                  // index of the emblem drawn on the portrait

        /// <summary>
        /// Optional sprite id for the battle screen rig. Empty (the default) means the unit
        /// stays fully procedural - no behaviour change from today. A non-empty value loads
        /// Resources/Sprites/Units/{Sprite}.png and the battle screen renders that image on a
        /// billboard quad instead of building the primitive body. Menu portraits (hero cards,
        /// hero detail, the summon reveal) are a separate rendering path and are unaffected
        /// either way - this only concerns the 3D battle rig.
        /// </summary>
        public string Sprite = "";

        public static VisualDef FromJson(JsonValue j)
        {
            VisualDef v = new VisualDef();
            if (j == null || j.IsNull) return v;
            v.Primary = j.GetString("primary", v.Primary);
            v.Secondary = j.GetString("secondary", v.Secondary);
            v.Accent = j.GetString("accent", v.Accent);
            v.Silhouette = j.GetString("silhouette", v.Silhouette);
            v.Weapon = j.GetString("weapon", v.Weapon);
            v.Scale = j.GetFloat("scale", v.Scale);
            v.Sigil = j.GetInt("sigil", v.Sigil);
            v.Sprite = j.GetString("sprite", v.Sprite);
            return v;
        }
    }

    /// <summary>One atomic thing an ability does. Abilities are just ordered lists of these.</summary>
    public sealed class EffectDef
    {
        public EffectKind Kind = EffectKind.Damage;
        public TargetMode Target = TargetMode.FrontEnemy;
        public float Power = 1f;      // multiplier applied to the caster's attack
        public float Flat;            // flat bonus added before mitigation
        public int Hits = 1;          // repeated applications
        public StatusKind Status = StatusKind.None;
        public int Duration = 2;      // rounds, for ApplyStatus
        public float Magnitude = 0.2f;// status strength (percentage or dot power)
        public float Chance = 1f;     // application chance, 0..1
        public float SelfHpRatio;     // for Shield/Heal scaling off the caster's max HP instead of attack

        public static EffectDef FromJson(JsonValue j)
        {
            EffectDef e = new EffectDef();
            e.Kind = EnumUtil.Parse(j.GetString("kind", "Damage"), EffectKind.Damage);
            e.Target = EnumUtil.Parse(j.GetString("target", "FrontEnemy"), TargetMode.FrontEnemy);
            e.Power = j.GetFloat("power", e.Kind == EffectKind.Damage ? 1f : 0f);
            e.Flat = j.GetFloat("flat", 0f);
            e.Hits = Math.Max(1, j.GetInt("hits", 1));
            e.Status = EnumUtil.Parse(j.GetString("status", "None"), StatusKind.None);
            e.Duration = j.GetInt("duration", 2);
            e.Magnitude = j.GetFloat("magnitude", 0.2f);
            e.Chance = j.GetFloat("chance", 1f);
            e.SelfHpRatio = j.GetFloat("selfHpRatio", 0f);
            return e;
        }
    }

    public sealed class AbilityDef
    {
        public string Id = "";
        public string Name = "";
        public string Description = "";
        public AbilityKind Kind = AbilityKind.Active;
        public int EnergyCost = 100;
        public string Vfx = "impact";     // impact | slash | burst | heal | buff | shadow | bolt
        public List<EffectDef> Effects = new List<EffectDef>();

        public static AbilityDef FromJson(JsonValue j, AbilityKind fallbackKind)
        {
            AbilityDef a = new AbilityDef();
            if (j == null || j.IsNull) return null;
            a.Id = j.GetString("id", "");
            a.Name = j.GetString("name", "Unnamed");
            a.Description = j.GetString("description", "");
            a.Kind = EnumUtil.Parse(j.GetString("kind", fallbackKind.ToString()), fallbackKind);
            a.EnergyCost = j.GetInt("energyCost", a.Kind == AbilityKind.Active ? 100 : 0);
            a.Vfx = j.GetString("vfx", "impact");
            List<JsonValue> effects = j.GetList("effects");
            for (int i = 0; i < effects.Count; i++) a.Effects.Add(EffectDef.FromJson(effects[i]));
            return a;
        }
    }

    public sealed class PassiveDef
    {
        public string Id = "";
        public string Name = "";
        public string Description = "";
        public PassiveTrigger Trigger = PassiveTrigger.None;
        public float Threshold = 0.5f;        // for OnHpBelowThreshold
        public bool OncePerBattle = true;     // for threshold style passives
        public StatusKind BonusVsStatus = StatusKind.None;
        public float BonusMultiplier;         // extra damage vs targets carrying BonusVsStatus
        public string Vfx = "buff";
        public List<EffectDef> Effects = new List<EffectDef>();

        public static PassiveDef FromJson(JsonValue j)
        {
            if (j == null || j.IsNull) return null;
            PassiveDef p = new PassiveDef();
            p.Id = j.GetString("id", "");
            p.Name = j.GetString("name", "Passive");
            p.Description = j.GetString("description", "");
            p.Trigger = EnumUtil.Parse(j.GetString("trigger", "None"), PassiveTrigger.None);
            p.Threshold = j.GetFloat("threshold", 0.5f);
            p.OncePerBattle = j.GetBool("oncePerBattle", p.Trigger == PassiveTrigger.OnHpBelowThreshold || p.Trigger == PassiveTrigger.BattleStart);
            p.BonusVsStatus = EnumUtil.Parse(j.GetString("bonusVsStatus", "None"), StatusKind.None);
            p.BonusMultiplier = j.GetFloat("bonusMultiplier", 0f);
            p.Vfx = j.GetString("vfx", "buff");
            List<JsonValue> effects = j.GetList("effects");
            for (int i = 0; i < effects.Count; i++) p.Effects.Add(EffectDef.FromJson(effects[i]));
            return p;
        }
    }

    /// <summary>Shared shape for anything that can stand on the battlefield.</summary>
    public abstract class UnitDef
    {
        public string Id = "";
        public string Name = "";
        public Aspect Aspect = Aspect.Ember;
        public float BaseHp = 1000;
        public float BaseAttack = 100;
        public float BaseDefense = 60;
        public float BaseSpeed = 100;
        public float CritChance = 0.15f;
        public float CritDamage = 1.6f;
        /// <summary>Flat landing-chance bonus/penalty for harmful ApplyStatus effects. Phase A: always 0, authored nowhere.</summary>
        public float EffectHit;
        public float EffectResist;
        public float Growth = 0.11f;   // per level, applied to hp / attack / defense
        public int StartEnergy;
        public VisualDef Visual = new VisualDef();
        public AbilityDef BasicAttack;
        public AbilityDef Active;
        public PassiveDef Passive;

        protected void ReadCommon(JsonValue j)
        {
            Id = j.GetString("id", "");
            Name = j.GetString("name", Id);
            Aspect = EnumUtil.Parse(j.GetString("aspect", "Ember"), Aspect.Ember);
            BaseHp = j.GetFloat("hp", BaseHp);
            BaseAttack = j.GetFloat("attack", BaseAttack);
            BaseDefense = j.GetFloat("defense", BaseDefense);
            BaseSpeed = j.GetFloat("speed", BaseSpeed);
            CritChance = j.GetFloat("critChance", CritChance);
            CritDamage = j.GetFloat("critDamage", CritDamage);
            EffectHit = j.GetFloat("effectHit", EffectHit);
            EffectResist = j.GetFloat("effectResist", EffectResist);
            Growth = j.GetFloat("growth", Growth);
            StartEnergy = j.GetInt("startEnergy", 0);
            Visual = VisualDef.FromJson(j["visual"]);
            BasicAttack = AbilityDef.FromJson(j["basic"], AbilityKind.BasicAttack);
            Active = AbilityDef.FromJson(j["active"], AbilityKind.Active);
            Passive = PassiveDef.FromJson(j["passive"]);
            if (BasicAttack == null) BasicAttack = DefaultBasicAttack();
        }

        public static AbilityDef DefaultBasicAttack()
        {
            AbilityDef a = new AbilityDef();
            a.Id = "basic_strike";
            a.Name = "Strike";
            a.Description = "A standard attack.";
            a.Kind = AbilityKind.BasicAttack;
            a.EnergyCost = 0;
            a.Vfx = "impact";
            EffectDef e = new EffectDef();
            e.Kind = EffectKind.Damage;
            e.Target = TargetMode.FrontEnemy;
            e.Power = 1f;
            a.Effects.Add(e);
            return a;
        }
    }

    public sealed class HeroDef : UnitDef
    {
        public string Title = "";
        public string Lore = "";
        public HeroRole Role = HeroRole.Striker;
        public Rarity Rarity = Rarity.Common;
        public bool StartingHero;

        public static HeroDef FromJson(JsonValue j)
        {
            HeroDef h = new HeroDef();
            h.ReadCommon(j);
            h.Title = j.GetString("title", "");
            h.Lore = j.GetString("lore", "");
            h.Role = EnumUtil.Parse(j.GetString("role", "Striker"), HeroRole.Striker);
            h.Rarity = EnumUtil.Parse(j.GetString("rarity", "Common"), Rarity.Common);
            h.StartingHero = j.GetBool("startingHero", false);
            return h;
        }
    }

    public sealed class EnemyDef : UnitDef
    {
        public string Kind = "minion"; // minion | elite | boss

        public static EnemyDef FromJson(JsonValue j)
        {
            EnemyDef e = new EnemyDef();
            e.ReadCommon(j);
            e.Kind = j.GetString("kind", "minion");
            return e;
        }
    }

    public sealed class EnemySpawn
    {
        public string EnemyId = "";
        public int Level = 1;

        public static EnemySpawn FromJson(JsonValue j)
        {
            EnemySpawn s = new EnemySpawn();
            s.EnemyId = j.GetString("id", "");
            s.Level = Math.Max(1, j.GetInt("level", 1));
            return s;
        }
    }

    public sealed class StageDef
    {
        public string Id = "";
        public string ChapterId = "";
        public string Name = "";
        public string Subtitle = "";
        public int Index;            // 1-based within the chapter
        public int GlobalIndex;      // 0-based across the whole campaign
        public bool IsBoss;
        public string Environment = "ashen";
        public List<EnemySpawn> Enemies = new List<EnemySpawn>();
        public int RewardGold = 100;
        public int RewardShards = 5;
        public int RewardAccountXp = 20;
        public int FirstClearGold = 200;
        public int FirstClearShards = 25;
        public int RecommendedPower = 100;

        public string DisplayCode
        {
            get { return ChapterNumber + "-" + Index; }
        }

        public int ChapterNumber = 1;

        public static StageDef FromJson(JsonValue j)
        {
            StageDef s = new StageDef();
            s.Id = j.GetString("id", "");
            s.Name = j.GetString("name", "Stage");
            s.Subtitle = j.GetString("subtitle", "");
            s.Index = j.GetInt("index", 1);
            s.IsBoss = j.GetBool("boss", false);
            s.Environment = j.GetString("environment", "ashen");
            s.RewardGold = j.GetInt("gold", 100);
            s.RewardShards = j.GetInt("shards", 5);
            s.RewardAccountXp = j.GetInt("accountXp", 20);
            s.FirstClearGold = j.GetInt("firstClearGold", s.RewardGold * 2);
            s.FirstClearShards = j.GetInt("firstClearShards", s.RewardShards * 5);
            s.RecommendedPower = j.GetInt("recommendedPower", 100);
            List<JsonValue> enemies = j.GetList("enemies");
            for (int i = 0; i < enemies.Count; i++) s.Enemies.Add(EnemySpawn.FromJson(enemies[i]));
            return s;
        }
    }

    public sealed class ChapterDef
    {
        public string Id = "";
        public string Name = "";
        public string Subtitle = "";
        public int Number = 1;
        public List<StageDef> Stages = new List<StageDef>();
    }

    /// <summary>Every tuning number the prototype uses, in one editable place.</summary>
    public sealed class BalanceConfig
    {
        public float DefenseConstant = 320f;
        public float DefensePerLevel = 18f;
        public int EnergyMax = 100;
        public int EnergyPerBasicAttack = 28;
        public int EnergyOnDamaged = 9;
        public int MaxRounds = 30;
        public float DamageVariance = 0.06f;
        public float MinDamage = 1f;
        public float SpeedVariance = 0.08f;

        /// <summary>Gates BOTH sides of a battle: player deploys and enemy spawns. Keep equal to PlayerProfile.TeamSize.</summary>
        public int MaxTeamSize = 5;

        /// <summary>
        /// Cap on extra turns a Gale unit's kill re-entry can grant per round. The re-entry
        /// mechanic itself is not built yet (DESIGN-BUILD-ORDER task 11); this constant ships
        /// ahead of it, with team size 5, because uncapped re-entry at five killable enemies
        /// lets one Gale unit resolve a round unilaterally. Task 11 must read this, never
        /// hardcode its own bound.
        /// </summary>
        public int GaleExtraTurnsPerRound = 1;

        // hero levelling
        public float LevelCostA = 55f;
        public float LevelCostB = 26f;
        public float LevelCostC = 3.2f;
        public int HeroLevelCapBase = 12;
        public int HeroLevelCapPerPlayerLevel = 4;
        public int HeroLevelHardCap = 80;

        // account levelling
        public float PlayerXpBase = 120f;
        public float PlayerXpExponent = 1.35f;

        // idle income
        public float IdleGoldPerHourBase = 140f;
        public float IdleGoldPerHourPerStage = 55f;
        public float IdleShardsPerHourBase = 7f;
        public float IdleShardsPerHourPerStage = 2.2f;
        public float IdleCapHours = 8f;

        // summoning
        public int SummonCost = 120;
        public float SummonWeightCommon = 62f;
        public float SummonWeightRare = 29f;
        public float SummonWeightEpic = 9f;
        public int DuplicateGold = 450;
        public int DuplicateShards = 30;

        // new account
        public int StartingGold = 1500;
        public int StartingShards = 260;

        public static BalanceConfig FromJson(JsonValue j)
        {
            BalanceConfig b = new BalanceConfig();
            if (j == null || j.IsNull) return b;
            b.DefenseConstant = j.GetFloat("defenseConstant", b.DefenseConstant);
            b.DefensePerLevel = j.GetFloat("defensePerLevel", b.DefensePerLevel);
            b.EnergyMax = j.GetInt("energyMax", b.EnergyMax);
            b.EnergyPerBasicAttack = j.GetInt("energyPerBasicAttack", b.EnergyPerBasicAttack);
            b.EnergyOnDamaged = j.GetInt("energyOnDamaged", b.EnergyOnDamaged);
            b.MaxRounds = j.GetInt("maxRounds", b.MaxRounds);
            b.DamageVariance = j.GetFloat("damageVariance", b.DamageVariance);
            b.SpeedVariance = j.GetFloat("speedVariance", b.SpeedVariance);
            b.MaxTeamSize = j.GetInt("maxTeamSize", b.MaxTeamSize);
            b.GaleExtraTurnsPerRound = j.GetInt("galeExtraTurnsPerRound", b.GaleExtraTurnsPerRound);
            b.LevelCostA = j.GetFloat("levelCostA", b.LevelCostA);
            b.LevelCostB = j.GetFloat("levelCostB", b.LevelCostB);
            b.LevelCostC = j.GetFloat("levelCostC", b.LevelCostC);
            b.HeroLevelCapBase = j.GetInt("heroLevelCapBase", b.HeroLevelCapBase);
            b.HeroLevelCapPerPlayerLevel = j.GetInt("heroLevelCapPerPlayerLevel", b.HeroLevelCapPerPlayerLevel);
            b.HeroLevelHardCap = j.GetInt("heroLevelHardCap", b.HeroLevelHardCap);
            b.PlayerXpBase = j.GetFloat("playerXpBase", b.PlayerXpBase);
            b.PlayerXpExponent = j.GetFloat("playerXpExponent", b.PlayerXpExponent);
            b.IdleGoldPerHourBase = j.GetFloat("idleGoldPerHourBase", b.IdleGoldPerHourBase);
            b.IdleGoldPerHourPerStage = j.GetFloat("idleGoldPerHourPerStage", b.IdleGoldPerHourPerStage);
            b.IdleShardsPerHourBase = j.GetFloat("idleShardsPerHourBase", b.IdleShardsPerHourBase);
            b.IdleShardsPerHourPerStage = j.GetFloat("idleShardsPerHourPerStage", b.IdleShardsPerHourPerStage);
            b.IdleCapHours = j.GetFloat("idleCapHours", b.IdleCapHours);
            b.SummonCost = j.GetInt("summonCost", b.SummonCost);
            b.SummonWeightCommon = j.GetFloat("summonWeightCommon", b.SummonWeightCommon);
            b.SummonWeightRare = j.GetFloat("summonWeightRare", b.SummonWeightRare);
            b.SummonWeightEpic = j.GetFloat("summonWeightEpic", b.SummonWeightEpic);
            b.DuplicateGold = j.GetInt("duplicateGold", b.DuplicateGold);
            b.DuplicateShards = j.GetInt("duplicateShards", b.DuplicateShards);
            b.StartingGold = j.GetInt("startingGold", b.StartingGold);
            b.StartingShards = j.GetInt("startingShards", b.StartingShards);
            return b;
        }
    }
}
