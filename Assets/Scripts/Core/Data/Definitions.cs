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

        /// <summary>
        /// Sprite only. Fraction of the canvas, measured from the bottom (the feet), that is the
        /// character's own body - head to feet, not counting anything raised above the head.
        /// Defaults to 1: the whole canvas is body, today's behaviour, unchanged for every hero
        /// that does not set this key. A raised weapon or prop needs the remaining top fraction
        /// reserved for it; see UnitRig.BuildSprite for how that fraction turns into world space,
        /// and DESIGN-ART-DELIVERY.md for how an artist measures it from their own file.
        /// </summary>
        public float SpriteBodyFraction = 1f;

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
            v.SpriteBodyFraction = j.GetFloat("spriteBodyFraction", v.SpriteBodyFraction);
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

        /// <summary>
        /// A field-for-field copy. Relics rewrite Target and Hits, and the EffectDef they are
        /// handed is the one owned by the AbilityDef in the ContentDatabase - shared by every unit
        /// in every battle for the life of the process. Mutating it in place would leak one
        /// wearer's relic onto every future cast of that ability by anybody, including the enemy
        /// side, and would survive across battles. Every relic rewrite goes through this.
        /// </summary>
        public EffectDef Clone()
        {
            EffectDef c = new EffectDef();
            c.Kind = Kind;
            c.Target = Target;
            c.Power = Power;
            c.Flat = Flat;
            c.Hits = Hits;
            c.Status = Status;
            c.Duration = Duration;
            c.Magnitude = Magnitude;
            c.Chance = Chance;
            c.SelfHpRatio = SelfHpRatio;
            return c;
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
        /// <summary>Phase B flat stat: multiplies activeSkill/passive-sourced Damage, never Basic Attack. See ApplyDamageEffect step 4.</summary>
        public float SkillDamage;
        /// <summary>Phase B flat stat: target-side chance to fully negate an incoming hit. See ApplyDamageEffect step 0 (before Base).</summary>
        public float Block;
        /// <summary>Phase B flat stat: source-side multiplicative reduction of the target's mitigation ratio. See ApplyDamageEffect step 6.</summary>
        public float ArmorBreak;
        /// <summary>Phase B flat stat: target-side chance reduction against Silence/Stun landing only. See ApplyEffect's ApplyStatus branch.</summary>
        public float ControlImmunity;
        /// <summary>Phase B flat stat: flat percentage reduction of incoming damage, applied after Defense mitigation. See ApplyDamageEffect step 7.</summary>
        public float DamageReduction;
        /// <summary>Phase B flat stat: source-side flat bonus damage added after Defense/Armor Break mitigation, so Defense never reduces it. See ApplyDamageEffect step 6.5.</summary>
        public float HolyDamage;
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
            SkillDamage = j.GetFloat("skillDamage", SkillDamage);
            Block = j.GetFloat("block", Block);
            ArmorBreak = j.GetFloat("armorBreak", ArmorBreak);
            ControlImmunity = j.GetFloat("controlImmunity", ControlImmunity);
            DamageReduction = j.GetFloat("damageReduction", DamageReduction);
            HolyDamage = j.GetFloat("holyDamage", HolyDamage);
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
        public int Stars = 5;
        public bool Fusable = true;
        public bool StartingHero;

        public static HeroDef FromJson(JsonValue j)
        {
            HeroDef h = new HeroDef();
            h.ReadCommon(j);
            h.Title = j.GetString("title", "");
            h.Lore = j.GetString("lore", "");
            h.Role = EnumUtil.Parse(j.GetString("role", "Striker"), HeroRole.Striker);
            h.Stars = Math.Max(1, Math.Min(5, j.GetInt("stars", 5)));
            h.Fusable = j.GetBool("fusable", true);
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
        public int IdleGoldPerHour;
        public int IdleShardsPerHour;

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
            s.IdleGoldPerHour = j.GetInt("idleGoldPerHour", 0);
            s.IdleShardsPerHour = j.GetInt("idleShardsPerHour", 0);
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

    /// <summary>
    /// One relic: a rule change in the effect pipeline, equipped on one hero.
    ///
    /// DESIGN-RELICS' founding constraint is enforced by this type's SHAPE, not by review: there
    /// is no Power, Magnitude, Multiplier or Bonus field anywhere on it, so a relic that wanted to
    /// be "+15% attack" has nowhere to put the 15. Everything here is an enum or a display string.
    /// If a future relic needs a number to express itself, that is the signal DESIGN-RELICS
    /// describes ("if a relic cannot be written as one sentence without a number, it is a stat
    /// stick and should be cut"), and the answer is to cut it rather than to widen this class.
    /// </summary>
    public sealed class RelicDef
    {
        public string Id = "";
        public string Name = "";

        /// <summary>Display text, stated as a rule and never as a number. Shown on the hero card.</summary>
        public string Rule = "";

        /// <summary>The hero this relic was designed around. Display only - nothing enforces it.</summary>
        public string PairedHeroId = "";

        public RelicHook Hook = RelicHook.None;

        // Retarget params.
        public TargetMode FromTarget = TargetMode.Self;
        public TargetMode ToTarget = TargetMode.Self;

        /// <summary>
        /// When true, Retarget only rewrites effects of FilterKind. Warden's Debt needs it: Orrin's
        /// Cinderguard aims BOTH a Shield and a Taunt at Self, and the relic is specified as moving
        /// the ward only. Without the filter it would also hand his Taunt to the lowest-HP ally,
        /// which per DESIGN-HOOK-RESOLUTION section 5 is the one thing it must not touch - Taunt is
        /// the targeting guarantee that survives the relabel, and the relic's whole argument is
        /// that it costs him the ward while leaving the guarantee intact.
        /// </summary>
        public bool HasFilterKind;
        public EffectKind FilterKind = EffectKind.Damage;

        /// <summary>
        /// Retarget only. Drops the caster from the rewritten effect's resolved targets.
        ///
        /// Warden's Debt needs it and needs it for a measured reason, not a hypothetical one.
        /// DESIGN-RELICS states the behaviour in prose - the ward "leaves the tank and lands on Ryn
        /// or Kalyx" - and LowestHpAlly, read literally, includes the caster. On its own named
        /// wearer that literal reading is close to a no-op: Orrin is the Lumen anchor, so every
        /// single-enemy ability aimed at his side resolves onto him, so he is very reliably the
        /// most wounded ally, so the ward he was supposed to give away comes straight back. The
        /// harness caught it on shipped content (CheckRelicsBiteOnShippedContent), where the ward
        /// landed on Orrin every time.
        ///
        /// That is the same shape of failure that got Tidewrought Metronome and Green Hour Vessel
        /// cut - an aspect rule claiming the thing one hook earlier and leaving the relic with
        /// nothing to do on the one hero it was written for. The difference is that this one is
        /// fixable in the relic rather than fatal to it, because the relic's premise (the ward
        /// departs) never depended on the clamp; only the vocabulary did.
        ///
        /// When exclusion empties the target list the effect resolves against nobody and the ward
        /// is simply lost. That is deliberate rather than a fallback to self: a last-man-standing
        /// Orrin warding himself would quietly restore exactly the behaviour the relic is sold as
        /// removing, at precisely the moment it matters most.
        /// </summary>
        public bool ExcludeSelf;

        // WidenPassiveTrigger params.
        public PassiveTrigger FromTrigger = PassiveTrigger.None;

        public static RelicDef FromJson(JsonValue j)
        {
            if (j == null || j.IsNull) return null;
            RelicDef r = new RelicDef();
            r.Id = j.GetString("id", "");
            r.Name = j.GetString("name", "Relic");
            r.Rule = j.GetString("rule", "");
            r.PairedHeroId = j.GetString("pairedHero", "");
            r.Hook = EnumUtil.Parse(j.GetString("hook", "None"), RelicHook.None);
            r.FromTarget = EnumUtil.Parse(j.GetString("fromTarget", "Self"), TargetMode.Self);
            r.ToTarget = EnumUtil.Parse(j.GetString("toTarget", "Self"), TargetMode.Self);
            string filter = j.GetString("effectKind", "");
            r.HasFilterKind = !string.IsNullOrEmpty(filter);
            if (r.HasFilterKind) r.FilterKind = EnumUtil.Parse(filter, EffectKind.Damage);
            r.ExcludeSelf = j.GetBool("excludeSelf", false);
            r.FromTrigger = EnumUtil.Parse(j.GetString("fromTrigger", "None"), PassiveTrigger.None);
            return r;
        }
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

        /// <summary>
        /// Most simultaneous Burn entries one unit can carry from Ember stacking (DESIGN-ASPECTS
        /// section 8). Ember's rule lets each Ember source add its own Burn instead of overwriting,
        /// so the entry count scales with how many Embers are on the line: at team size 5, three
        /// Embers casting an AllEnemies Burn against five enemies is 15 live StatusInstances all
        /// ticking in TickDamageOverTime. The doc calls that a legibility and stability problem
        /// rather than a balance one, which is why the bound is a flat entry cap and not a damage
        /// scale. At the cap a further application degrades to the pre-Ember refresh: it keeps the
        /// stronger magnitude and the longer duration on an existing entry instead of adding a new
        /// one, so a strong Ember still deepens the fire and nothing is silently dropped.
        /// </summary>
        public int EmberBurnStackCap = 3;

        /// <summary>
        /// Relic slots on one hero (DESIGN-RELICS "Equipping"). One, and the document argues the
        /// number rather than picking it: more than one slot stacks rule changes into a build, and
        /// a team-wide slot would be an aura, which is a global multiplier, which is the stat stick
        /// the design exists to avoid.
        ///
        /// DESIGN-RELICS also specifies a second balance key, relicEnergyBankMax, as "the ceiling
        /// on the Metronome's carry so it cannot spiral". It is deliberately absent: Tidewrought
        /// Metronome is cut (DESIGN-HOOK-RESOLUTION section 2), there is no bank to bound, and a
        /// tuning key that no code reads is worse than no key - it reads as a live lever.
        /// </summary>
        public int RelicSlotsPerHero = 1;

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
        public float IdleGoldPerHourFloor = 140f;
        public float IdleShardsPerHourFloor = 7f;
        public float IdleCapHours = 8f;

        // replay economy - see DESIGN-REPLAY-ECONOMY.md
        /// <summary>Repeat clears of an already-cleared stage that still pay the full purse,
        /// per stage per UTC day. A player's first clear of a stage is never counted here.</summary>
        public int RepeatFullClearsPerDay = 3;
        /// <summary>Fraction of the purse a repeat clear pays once the allowance is spent.
        /// Zero by design: any positive floor still out-earns the idle cache many times over.</summary>
        public float RepeatRewardFloor = 0f;
        /// <summary>Hour of the UTC day the per-stage repeat counters roll over.</summary>
        public int DailyResetHourUtc = 4;

        // relic progression - see DESIGN-RELIC-PROGRESSION.md
        /// <summary>
        /// Relic Dust paid by a repeat clear of the campaign's first stage. Later stages add
        /// RelicDustPerStageIndex per step of StageDef.GlobalIndex, which is the difficulty
        /// scaling the design doc asks for and leaves to tuning.
        /// </summary>
        public int RelicDustPerRepeatClear = 6;

        /// <summary>Extra Relic Dust per step of StageDef.GlobalIndex.</summary>
        public int RelicDustPerStageIndex = 2;

        /// <summary>
        /// Highest tier a relic can be owned at. Pinned to 1 until the balance pass authors
        /// tier 2; nothing in the simulation branches on tier, so raising this alone changes
        /// no relic's behaviour.
        /// </summary>
        public int RelicMaxTier = 1;

        /// <summary>Relic Dust cost to purchase one relic from the shop.</summary>
        public int RelicShopPrice = 80;

        // summoning
        public int SummonCost = 120;
        public float SummonWeight1 = 0f;
        public float SummonWeight2 = 0f;
        public float SummonWeight3 = 62f;
        public float SummonWeight4 = 29f;
        public float SummonWeight5 = 9f;
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
            b.EmberBurnStackCap = j.GetInt("emberBurnStackCap", b.EmberBurnStackCap);
            b.RelicSlotsPerHero = j.GetInt("relicSlotsPerHero", b.RelicSlotsPerHero);
            b.LevelCostA = j.GetFloat("levelCostA", b.LevelCostA);
            b.LevelCostB = j.GetFloat("levelCostB", b.LevelCostB);
            b.LevelCostC = j.GetFloat("levelCostC", b.LevelCostC);
            b.HeroLevelCapBase = j.GetInt("heroLevelCapBase", b.HeroLevelCapBase);
            b.HeroLevelCapPerPlayerLevel = j.GetInt("heroLevelCapPerPlayerLevel", b.HeroLevelCapPerPlayerLevel);
            b.HeroLevelHardCap = j.GetInt("heroLevelHardCap", b.HeroLevelHardCap);
            b.PlayerXpBase = j.GetFloat("playerXpBase", b.PlayerXpBase);
            b.PlayerXpExponent = j.GetFloat("playerXpExponent", b.PlayerXpExponent);
            b.IdleGoldPerHourFloor = j.GetFloat("idleGoldPerHourFloor", b.IdleGoldPerHourFloor);
            b.IdleShardsPerHourFloor = j.GetFloat("idleShardsPerHourFloor", b.IdleShardsPerHourFloor);
            b.IdleCapHours = j.GetFloat("idleCapHours", b.IdleCapHours);
            b.RepeatFullClearsPerDay = j.GetInt("repeatFullClearsPerDay", b.RepeatFullClearsPerDay);
            b.RepeatRewardFloor = j.GetFloat("repeatRewardFloor", b.RepeatRewardFloor);
            b.DailyResetHourUtc = j.GetInt("dailyResetHourUtc", b.DailyResetHourUtc);
            b.RelicDustPerRepeatClear = j.GetInt("relicDustPerRepeatClear", b.RelicDustPerRepeatClear);
            b.RelicDustPerStageIndex = j.GetInt("relicDustPerStageIndex", b.RelicDustPerStageIndex);
            b.RelicMaxTier = j.GetInt("relicMaxTier", b.RelicMaxTier);
            b.RelicShopPrice = j.GetInt("relicShopPrice", b.RelicShopPrice);
            b.SummonCost = j.GetInt("summonCost", b.SummonCost);
            b.SummonWeight1 = j.GetFloat("summonWeight1", b.SummonWeight1);
            b.SummonWeight2 = j.GetFloat("summonWeight2", b.SummonWeight2);
            b.SummonWeight3 = j.GetFloat("summonWeight3", b.SummonWeight3);
            b.SummonWeight4 = j.GetFloat("summonWeight4", b.SummonWeight4);
            b.SummonWeight5 = j.GetFloat("summonWeight5", b.SummonWeight5);
            b.DuplicateGold = j.GetInt("duplicateGold", b.DuplicateGold);
            b.DuplicateShards = j.GetInt("duplicateShards", b.DuplicateShards);
            b.StartingGold = j.GetInt("startingGold", b.StartingGold);
            b.StartingShards = j.GetInt("startingShards", b.StartingShards);
            return b;
        }
    }
}
