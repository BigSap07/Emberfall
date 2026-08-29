using System;
using System.Collections.Generic;
using System.Text;
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

        /// <summary>
        /// Sprite only. Fraction of the canvas, measured from the bottom, that is EMPTY margin
        /// below the character's own feet - the counterpart SpriteBodyFraction does not cover.
        /// Defaults to 0: feet touch the bottom edge of the canvas exactly, today's behaviour,
        /// unchanged for every hero that does not set this key (every hero but Kael).
        ///
        /// Distinct from SpriteBodyFraction's reserved headroom above the body: that space is
        /// legitimately used by some frames of a clip (a raised weapon mid-swing), so it stays
        /// part of the canvas the quad renders. A margin below the feet has no equivalent use -
        /// nothing in a battle animation extends below where a character stands - so instead of
        /// being sized into the canvas it is measured out of it: the quad shifts upward by this
        /// fraction of canvas height so the character's actual feet, not the transparent edge
        /// below them, land on the ground. See UnitRig.BuildSprite for the shift, and
        /// DESIGN-ART-DELIVERY.md for how an artist (or an automated sprite-sheet export) measures
        /// it off their own file the same way SpriteBodyFraction already is.
        /// </summary>
        public float SpriteFootFraction = 0f;

        /// <summary>
        /// Sprite only. True mirrors the quad horizontally. The battle rig turns every ally
        /// toward +X and every enemy toward -X by the same fixed angle in opposite directions
        /// (UnitRig.Build's ThreeQuarterTurn), which is what DESIGN-ART-DELIVERY.md's sprites are
        /// drawn against - a hero authored facing that convention needs nothing else. This exists
        /// for art that was authored facing the opposite way: rather than re-exporting the source
        /// frames, the quad flips instead. Defaults to false - unchanged for every hero whose art
        /// already matches the convention.
        /// </summary>
        public bool SpriteMirror;

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
            v.SpriteFootFraction = j.GetFloat("spriteFootFraction", v.SpriteFootFraction);
            v.SpriteMirror = j.GetBool("spriteMirror", v.SpriteMirror);
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
    /// One stat a relic moves, and how. Numbers live HERE and only here.
    ///
    /// This type is the whole reason RelicDef can carry a stat bundle without a hook-based relic's
    /// unique passive losing its own no-numbers guarantee. That guarantee is no longer "this relic
    /// carries no numbers anywhere" - a mythic relic's Stats can be non-empty - it is narrower and
    /// just as real: Rule itself is still required to be a number-free sentence for any relic that
    /// carries a Hook, and the digit test is enforced against Rule specifically, in the harness and
    /// at content load. A relic with no Hook is required to carry a non-empty Stats list instead, or
    /// it does nothing. Both rules are enforced by ContentDatabase at load, not by review.
    /// </summary>
    public sealed class RelicStatBonus
    {
        public RelicStat Stat = RelicStat.Attack;
        public RelicStatMode Mode = RelicStatMode.Multiply;

        /// <summary>
        /// Read against Mode, never on its own. 0.14 with Multiply is "+14% Attack" and means
        /// Attack *= 1.14; 0.12 with Add is "+12% Crit Chance" and means CritChance += 0.12. The
        /// mode is stored rather than inferred from Stat so the two arithmetic rules are explicit
        /// in the data instead of implied by the stat name.
        /// </summary>
        public float Value;

        public static RelicStatBonus FromJson(JsonValue j)
        {
            if (j == null || j.IsNull) return null;
            RelicStatBonus b = new RelicStatBonus();
            b.Stat = EnumUtil.Parse(j.GetString("stat", "Attack"), RelicStat.Attack);
            b.Mode = EnumUtil.Parse(j.GetString("mode", "Multiply"), RelicStatMode.Multiply);
            b.Value = j.GetFloat("value", 0f);
            return b;
        }
    }

    /// <summary>
    /// One relic, at one of two grades (DESIGN-RELICS, renamed since). The grade decides which
    /// fields are live, enforced by validation rather than by convention:
    ///
    /// - A LEGENDARY relic (formerly "Common" - the label moved, the eight starting relics and
    ///   their values did not) carries Stats and leaves Hook at None. It resolves entirely in
    ///   StatMath before the simulation starts, and CombatSimulation never learns it exists.
    /// - A MYTHIC relic (formerly "Legendary") carries a Hook and states itself as one sentence in
    ///   Rule with no number in it, exactly as the grade named Legendary always did. What changed
    ///   at the rename is that a mythic relic can ALSO carry Stats now - a stat block stacked on
    ///   top of its unique passive, not a replacement for it. It resolves inside the simulation at
    ///   the hook's own resolution point, and its Stats fold in at the same setup step a legendary
    ///   relic's do.
    ///
    /// DESIGN-RELICS' founding "no relic contains an attack percentage" standard used to be
    /// enforced by this type's SHAPE - there was no numeric field anywhere on it, so a relic that
    /// wanted to be "+15% attack" had nowhere to put the 15. That standard was withdrawn as a
    /// blanket rule on 2026-08-26 and narrowed rather than removed: it now binds Rule specifically,
    /// for any relic carrying a Hook. The 15 still cannot go in Rule; it can now go in Stats, on
    /// either grade, which is the whole point of promoting a relic to mythic.
    /// </summary>
    public sealed class RelicDef
    {
        public string Id = "";
        public string Name = "";

        /// <summary>
        /// Set only on a relic that carries a Hook (today: mythic grade): the rule, stated as one
        /// sentence and never as a number. Empty on a relic with no Hook, which has no rule to
        /// state - its card shows DescribeStats() instead, and writing prose for it would be
        /// dressing up a stat bundle as something it is not.
        /// </summary>
        public string Rule = "";

        /// <summary>
        /// The hero this relic was designed around. Display only - nothing enforces it. A
        /// legendary (stat-only) relic never carries one: the grade is aspect-neutral and universal
        /// by design, and a stat bundle that only fits one kind of hero is a worse stat bundle, not
        /// a design.
        /// </summary>
        public string PairedHeroId = "";

        public RelicGrade Grade = RelicGrade.Legendary;

        /// <summary>Mythic only. None on every legendary relic - see the class summary.</summary>
        public RelicHook Hook = RelicHook.None;

        /// <summary>
        /// The stat bundle. Required to be non-empty on a legendary relic, since a legendary relic
        /// with no Hook and no Stats does nothing. Optional on a mythic relic - both shipped mythic
        /// relics carry one, stacked on top of their Hook, but nothing requires a future one to.
        /// </summary>
        public readonly List<RelicStatBonus> Stats = new List<RelicStatBonus>();

        /// <summary>
        /// The stat half of a card's text, built from the stat block rather than authored: "+14%
        /// Attack, +10% Speed". Lives here rather than in either screen so the shop and the
        /// inventory cannot drift into describing the same relic two different ways.
        ///
        /// One "%" for both modes is deliberate and is the player-facing half of the decision
        /// RelicStatMode documents: "+12% Crit Chance" reads the same whether the 12 multiplies or
        /// adds percentage points, and the difference is not something a player can act on.
        /// </summary>
        public string DescribeStats()
        {
            if (Stats.Count == 0) return "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Stats.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                int pct = (int)Math.Round(Stats[i].Value * 100f, MidpointRounding.AwayFromZero);
                sb.Append(pct >= 0 ? "+" : "").Append(pct).Append("% ").Append(StatLabel(Stats[i].Stat));
            }
            return sb.ToString();
        }

        /// <summary>Display spelling of a RelicStat. "Max HP" and "Crit Chance", not the enum name.</summary>
        public static string StatLabel(RelicStat stat)
        {
            switch (stat)
            {
                case RelicStat.MaxHp: return "Max HP";
                case RelicStat.Attack: return "Attack";
                case RelicStat.Defense: return "Defense";
                case RelicStat.Speed: return "Speed";
                case RelicStat.CritChance: return "Crit Chance";
                case RelicStat.CritDamage: return "Crit Damage";
                case RelicStat.EffectHit: return "Effect Hit";
                case RelicStat.EffectResist: return "Effect Resist";
            }
            return stat.ToString();
        }

        /// <summary>
        /// What a card shows for this relic. A legendary (stat-only) relic shows its numbers and
        /// nothing else - there is no rule to state. A mythic relic shows its rule AND its numbers,
        /// on two lines, because a mythic relic now genuinely has both halves and hiding either one
        /// would misrepresent what the player is equipping. `Rule` alone, with no Stats, still
        /// renders as just the rule - the second line only appears when there is something for it
        /// to say.
        /// </summary>
        public string CardText()
        {
            if (Grade == RelicGrade.Legendary) return DescribeStats();

            string stats = DescribeStats();
            return string.IsNullOrEmpty(stats) ? Rule : Rule + "\n" + stats;
        }

        public static RelicDef FromJson(JsonValue j)
        {
            if (j == null || j.IsNull) return null;
            RelicDef r = new RelicDef();
            r.Id = j.GetString("id", "");
            r.Name = j.GetString("name", "Relic");
            r.Rule = j.GetString("rule", "");
            r.PairedHeroId = j.GetString("pairedHero", "");
            r.Grade = EnumUtil.Parse(j.GetString("grade", "Legendary"), RelicGrade.Legendary);
            r.Hook = EnumUtil.Parse(j.GetString("hook", "None"), RelicHook.None);

            JsonValue stats = j["stats"];
            if (stats != null && stats.IsArray)
            {
                for (int i = 0; i < stats.Count; i++)
                {
                    RelicStatBonus b = RelicStatBonus.FromJson(stats[i]);
                    if (b != null) r.Stats.Add(b);
                }
            }
            return r;
        }
    }

    /// <summary>
    /// One rollable stat line in armor.json: a stat, and the closed range its value is drawn from.
    /// Used for both the primary and the substat tables, and for the attunement bonuses, which is
    /// why Min and Max are allowed to be equal - an attunement bonus is a fixed number, expressed
    /// as a degenerate range rather than as a second type.
    ///
    /// Weight is read only by the substat table. A primary is chosen by the slot, not rolled, and
    /// an attunement bonus is chosen by the aspect, so neither of those competes for a draw.
    /// </summary>
    public sealed class ArmorRollDef
    {
        public ArmorStat Stat = ArmorStat.Attack;
        public float Min;
        public float Max;

        /// <summary>Relative draw weight in the substat table. Clamped to at least 0 - an entry at
        /// zero is authored-but-disabled, which is a legitimate thing to want and is why this is
        /// not forced to 1.</summary>
        public int Weight = 10;

        public static ArmorRollDef FromJson(JsonValue j)
        {
            if (j == null || j.IsNull) return null;
            ArmorRollDef r = new ArmorRollDef();
            r.Stat = EnumUtil.Parse(j.GetString("stat", "Attack"), ArmorStat.Attack);
            r.Min = j.GetFloat("min", 0f);
            r.Max = j.GetFloat("max", r.Min);
            if (r.Max < r.Min) { float swap = r.Min; r.Min = r.Max; r.Max = swap; }
            r.Weight = Math.Max(0, j.GetInt("weight", 10));
            return r;
        }
    }

    /// <summary>
    /// The economy and generosity numbers for one temper rung. PrimaryScale multiplies whatever
    /// the primary table rolled, which is what makes a Radiant Weapon strictly better than a Rough
    /// one without needing a second primary table per rung.
    ///
    /// SalvageSlag and RerollSlag are the Anvil's two prices. DESIGN-ARMOR section 7 requires
    /// salvage to pay less than reroll costs at every rung so the loop consumes pieces instead of
    /// printing Slag; ContentDatabase.Validate enforces it rather than trusting the author.
    /// </summary>
    public sealed class ArmorTemperDef
    {
        public ArmorTemper Temper = ArmorTemper.Rough;
        public float PrimaryScale = 1f;
        public int SalvageSlag;
        public int RerollSlag;

        /// <summary>
        /// Slag one Combine into THIS temper costs - i.e. combining three pieces one rung below
        /// costs the OUTPUT rung's CombineSlag, read via GameSession.CombineCost(outputTemper).
        /// Same per-temper field pattern SalvageSlag and RerollSlag already use, not a new pricing
        /// curve: DESIGN-ARMOR's Anvil scales every one of its actions by temper this same way.
        ///
        /// Meaningless on Rough - nothing combines INTO the bottom rung, since there is no rung
        /// below it to combine FROM. Left at its zero default there; content validation does not
        /// require it to be authored on that one entry, only on Honed/Gilded/Radiant.
        /// </summary>
        public int CombineSlag;

        /// <summary>Relative draw weight for a drop at stage index 0. See ArmorDropWeightPerStageIndex
        /// for how deep stages tilt away from this.</summary>
        public int DropWeight = 10;

        /// <summary>
        /// How much this rung's drop weight moves per point of StageDef.GlobalIndex. Positive on
        /// the high rungs and negative on the low ones, so deep stages skew toward Gilded and
        /// Radiant without any rung ever becoming impossible - the roller floors the resulting
        /// weight at 1 rather than at 0, so a Rough piece stays reachable at the final stage and a
        /// Radiant one stays reachable at the first.
        /// </summary>
        public float DropWeightPerStageIndex;

        public static ArmorTemperDef FromJson(JsonValue j)
        {
            if (j == null || j.IsNull) return null;
            ArmorTemperDef t = new ArmorTemperDef();
            t.Temper = EnumUtil.Parse(j.GetString("temper", "Rough"), ArmorTemper.Rough);
            t.PrimaryScale = j.GetFloat("primaryScale", 1f);
            t.SalvageSlag = Math.Max(0, j.GetInt("salvageSlag", 0));
            t.RerollSlag = Math.Max(0, j.GetInt("rerollSlag", 0));
            t.CombineSlag = Math.Max(0, j.GetInt("combineSlag", 0));
            t.DropWeight = Math.Max(0, j.GetInt("dropWeight", 10));
            t.DropWeightPerStageIndex = j.GetFloat("dropWeightPerStageIndex", 0f);
            return t;
        }
    }

    /// <summary>
    /// One attunement threshold for one aspect (DESIGN-ARMOR section 6): "an Ember hero wearing at
    /// least 2 Ember pieces gets these bonuses". Thresholds are CUMULATIVE, not exclusive - a hero
    /// at 4 matching pieces receives the 2-piece entry and the 4-piece entry both - so the 4-piece
    /// entry lists only what it adds on top.
    ///
    /// Bonuses are ArmorRollDef with Min == Max. That is deliberate reuse rather than a fixed-value
    /// type of its own: it makes it structurally impossible for a set bonus to express anything a
    /// substat could not, which is the whole "no unique effects, ever" guarantee restated for the
    /// set layer.
    /// </summary>
    public sealed class ArmorAttunementDef
    {
        public Aspect Aspect = Aspect.Ember;
        public int Pieces = 2;
        public string Name = "";
        public readonly List<ArmorRollDef> Bonuses = new List<ArmorRollDef>();

        public static ArmorAttunementDef FromJson(JsonValue j)
        {
            if (j == null || j.IsNull) return null;
            ArmorAttunementDef a = new ArmorAttunementDef();
            a.Aspect = EnumUtil.Parse(j.GetString("aspect", "Ember"), Aspect.Ember);
            a.Pieces = Math.Max(1, j.GetInt("pieces", 2));
            a.Name = j.GetString("name", "");
            List<JsonValue> bonuses = j.GetList("bonuses");
            for (int i = 0; i < bonuses.Count; i++)
            {
                ArmorRollDef b = ArmorRollDef.FromJson(bonuses[i]);
                if (b != null) a.Bonuses.Add(b);
            }
            return a;
        }
    }

    /// <summary>
    /// Everything armor.json carries. Deliberately NOT a per-item catalogue the way relics.json is:
    /// an armor piece is generated, not authored, so what content owns is the roll tables, the
    /// naming pools and the prices. There is no ArmorDef for an individual piece and there is no
    /// id to reference, which is also why a saved piece stores its resolved values rather than a
    /// reference into here (DESIGN-ARMOR section 10).
    ///
    /// A build with no armor.json loads an empty config, which ArmorRoller treats as "this build
    /// cannot roll armor" - the same shape relics.json and events.json already have.
    /// </summary>
    public sealed class ArmorConfig
    {
        public readonly List<ArmorRollDef> PrimaryRolls = new List<ArmorRollDef>();
        public readonly List<ArmorRollDef> SubRolls = new List<ArmorRollDef>();
        public readonly List<ArmorTemperDef> Tempers = new List<ArmorTemperDef>();
        public readonly List<ArmorAttunementDef> Attunements = new List<ArmorAttunementDef>();

        /// <summary>Flavour names per slot, drawn from at roll time. Purely cosmetic - a piece's
        /// behaviour is its slot, temper, aspect and rolls, never its name.</summary>
        public readonly Dictionary<ArmorSlot, List<string>> SlotNames = new Dictionary<ArmorSlot, List<string>>();

        /// <summary>Chance, 0..1, that a full-purse stage clear drops a piece at all. One at the
        /// default so an unauthored armor.json is obvious rather than silently stingy.</summary>
        public float DropChance = 1f;

        /// <summary>True once the config carries enough to roll a piece. Everything downstream tests
        /// this instead of testing four collections for emptiness in four places.</summary>
        public bool IsUsable
        {
            get { return PrimaryRolls.Count > 0 && SubRolls.Count > 0 && Tempers.Count > 0; }
        }

        public ArmorTemperDef GetTemper(ArmorTemper temper)
        {
            for (int i = 0; i < Tempers.Count; i++) if (Tempers[i].Temper == temper) return Tempers[i];
            return null;
        }

        /// <summary>The primary roll range for a slot, resolved through the slot's guaranteed stat
        /// rather than by looking a slot up - the guarantee lives in ArmorInfo, and this reads it
        /// rather than restating it.</summary>
        public ArmorRollDef GetPrimaryRoll(ArmorSlot slot)
        {
            ArmorStat wanted = ArmorInfo.PrimaryStat(slot);
            for (int i = 0; i < PrimaryRolls.Count; i++) if (PrimaryRolls[i].Stat == wanted) return PrimaryRolls[i];
            return null;
        }

        public List<string> NamesFor(ArmorSlot slot)
        {
            List<string> names;
            return SlotNames.TryGetValue(slot, out names) ? names : null;
        }

        /// <summary>
        /// Every attunement entry an aspect has at or below a matching-piece count, in ascending
        /// threshold order. Cumulative by construction: the caller applies all of them, which is
        /// what makes the 4-piece entry an addition to the 2-piece one rather than a replacement.
        /// </summary>
        public List<ArmorAttunementDef> AttunementsFor(Aspect aspect, int matchingPieces)
        {
            List<ArmorAttunementDef> result = new List<ArmorAttunementDef>();
            for (int i = 0; i < Attunements.Count; i++)
            {
                ArmorAttunementDef a = Attunements[i];
                if (a.Aspect == aspect && a.Pieces <= matchingPieces) result.Add(a);
            }
            result.Sort(delegate (ArmorAttunementDef x, ArmorAttunementDef y) { return x.Pieces.CompareTo(y.Pieces); });
            return result;
        }

        public static ArmorConfig FromJson(JsonValue j)
        {
            ArmorConfig c = new ArmorConfig();
            if (j == null || j.IsNull) return c;

            c.DropChance = j.GetFloat("dropChance", 1f);
            if (c.DropChance < 0f) c.DropChance = 0f;
            if (c.DropChance > 1f) c.DropChance = 1f;

            List<JsonValue> primaries = j.GetList("primaryRolls");
            for (int i = 0; i < primaries.Count; i++)
            {
                ArmorRollDef r = ArmorRollDef.FromJson(primaries[i]);
                if (r != null) c.PrimaryRolls.Add(r);
            }

            List<JsonValue> subs = j.GetList("subRolls");
            for (int i = 0; i < subs.Count; i++)
            {
                ArmorRollDef r = ArmorRollDef.FromJson(subs[i]);
                if (r != null) c.SubRolls.Add(r);
            }

            List<JsonValue> tempers = j.GetList("tempers");
            for (int i = 0; i < tempers.Count; i++)
            {
                ArmorTemperDef t = ArmorTemperDef.FromJson(tempers[i]);
                if (t != null && c.GetTemper(t.Temper) == null) c.Tempers.Add(t);
            }

            List<JsonValue> attunements = j.GetList("attunements");
            for (int i = 0; i < attunements.Count; i++)
            {
                ArmorAttunementDef a = ArmorAttunementDef.FromJson(attunements[i]);
                if (a != null) c.Attunements.Add(a);
            }

            JsonValue names = j["slotNames"];
            if (names != null && names.IsObject)
            {
                foreach (string key in names.Keys)
                {
                    ArmorSlot slot;
                    try { slot = (ArmorSlot)Enum.Parse(typeof(ArmorSlot), key, true); }
                    catch (Exception) { continue; }   // an unknown slot key is dropped, not defaulted
                    List<string> pool = new List<string>();
                    List<JsonValue> entries = names.GetList(key);
                    for (int i = 0; i < entries.Count; i++)
                    {
                        string n = entries[i].AsString(null);
                        if (!string.IsNullOrEmpty(n)) pool.Add(n);
                    }
                    if (pool.Count > 0) c.SlotNames[slot] = pool;
                }
            }

            return c;
        }
    }

    /// <summary>
    /// A flat bundle of currency an event can hand out - a milestone claim or a currency-ladder
    /// redemption, same shape either way. Deliberately as small as StageDef's own reward fields:
    /// no hero grants, no relic grants, nothing content does not already know how to apply. All
    /// three fields default to zero, and IsEmpty exists so content validation can catch an entry
    /// that would silently hand out nothing.
    /// </summary>
    public sealed class EventReward
    {
        public int Gold;
        public int Shards;
        public int RelicDust;

        public bool IsEmpty { get { return Gold <= 0 && Shards <= 0 && RelicDust <= 0; } }

        /// <summary>Player-facing summary, e.g. "200 Gold, 50 Shards" - built rather than authored,
        /// same reasoning as RelicDef.DescribeStats: one source, so a card and a claim confirmation
        /// cannot drift into describing the same reward two different ways.</summary>
        public string Describe()
        {
            List<string> parts = new List<string>();
            if (Gold > 0) parts.Add(Gold + " Gold");
            if (Shards > 0) parts.Add(Shards + " Shards");
            if (RelicDust > 0) parts.Add(RelicDust + " Relic Dust");
            return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "Nothing";
        }

        public static EventReward FromJson(JsonValue j)
        {
            EventReward r = new EventReward();
            if (j == null || j.IsNull) return r;
            r.Gold = j.GetInt("gold", 0);
            r.Shards = j.GetInt("shards", 0);
            r.RelicDust = j.GetInt("relicDust", 0);
            return r;
        }
    }

    /// <summary>One rung of an event's points ladder: reach Threshold cumulative points, claim
    /// Reward once. Threshold is the rung's own identity - GameSession.ClaimEventMilestone and the
    /// save format both key off it rather than a list index, so re-ordering milestones in content
    /// never repoints an already-claimed entry at a different reward.</summary>
    public sealed class EventMilestone
    {
        public long Threshold;
        public EventReward Reward = new EventReward();
    }

    /// <summary>One rung of an event's optional currency ladder: spend Cost of the event's own
    /// currency, receive Reward. Unlike a milestone this is a genuine purchase - the currency is
    /// deducted - so nothing tracks whether a tier was redeemed before; redeeming it again is a
    /// second purchase, not a double-claim, and simply costs the currency a second time.</summary>
    public sealed class EventCurrencyTier
    {
        public long Cost;
        public EventReward Reward = new EventReward();
    }

    /// <summary>
    /// One limited-time event: a UTC window (same startUnix/endUnix style as
    /// BalanceConfig.DailyResetHourUtc, but an absolute pair rather than an hour-of-day), one
    /// EventActivityKind it earns points from, a milestone ladder every event has, and an optional
    /// event-specific currency with its own small redeemable ladder.
    ///
    /// Content, not code: a new event is a new entry in events.json, never a new class or a new
    /// case anywhere in GameSession. The two starter events (The Recollection Rite,
    /// The Ledger's Week) are both ordinary EventDef entries - nothing about either is special-cased.
    /// </summary>
    public sealed class EventDef
    {
        public string Id = "";
        public string Name = "";

        /// <summary>UTC Unix seconds. Points are earned only while IsActive; a milestone or
        /// currency tier already earned stays claimable after EndUnix passes - see GameSession.</summary>
        public long StartUnix;
        public long EndUnix;

        public EventActivityKind Activity = EventActivityKind.SummonPerformed;

        /// <summary>Display name for the point currency this event's ladder climbs, e.g.
        /// "Memory Fragments" - never hardcoded as "points" anywhere UI-facing.</summary>
        public string PointsLabel = "Points";

        /// <summary>Points earned per unit of Activity's own amount - 1 for a per-occurrence
        /// activity like SummonPerformed, or a rate against a quantity like RelicDustSpent.</summary>
        public float PointsPerActivity = 1f;

        public readonly List<EventMilestone> Milestones = new List<EventMilestone>();

        /// <summary>Empty means this event has no currency - a pure point ladder, per The
        /// Recollection Rite. Non-empty is both the display name and the flag that HasCurrency
        /// reads, so there is exactly one source of truth for whether the currency half exists.</summary>
        public string CurrencyName = "";

        /// <summary>Currency earned per unit of Activity's own amount, at the same dispatch point
        /// and the same time as Points - both accrue from one RecordEventActivity call, at their
        /// own independent rates. Meaningless while CurrencyName is empty.</summary>
        public float CurrencyPerActivity = 0f;

        public readonly List<EventCurrencyTier> CurrencyRewards = new List<EventCurrencyTier>();

        public bool HasCurrency { get { return !string.IsNullOrEmpty(CurrencyName); } }

        public bool IsActive(long nowUnix) { return nowUnix >= StartUnix && nowUnix < EndUnix; }
        public bool HasEnded(long nowUnix) { return nowUnix >= EndUnix; }
        public bool HasNotStarted(long nowUnix) { return nowUnix < StartUnix; }

        public EventMilestone FindMilestone(long threshold)
        {
            for (int i = 0; i < Milestones.Count; i++) if (Milestones[i].Threshold == threshold) return Milestones[i];
            return null;
        }

        public EventCurrencyTier FindCurrencyTier(long cost)
        {
            for (int i = 0; i < CurrencyRewards.Count; i++) if (CurrencyRewards[i].Cost == cost) return CurrencyRewards[i];
            return null;
        }

        /// <summary>The highest milestone threshold this event's points can ever reach a claim
        /// against - the ladder's ceiling, for a progress bar that wants a denominator.</summary>
        public long HighestThreshold
        {
            get { return Milestones.Count > 0 ? Milestones[Milestones.Count - 1].Threshold : 0; }
        }

        public static EventDef FromJson(JsonValue j)
        {
            if (j == null || j.IsNull) return null;
            EventDef e = new EventDef();
            e.Id = j.GetString("id", "");
            e.Name = j.GetString("name", "Event");
            e.StartUnix = j.GetLong("startUnix", 0);
            e.EndUnix = j.GetLong("endUnix", 0);
            e.Activity = EnumUtil.Parse(j.GetString("activity", "SummonPerformed"), EventActivityKind.SummonPerformed);
            e.PointsLabel = j.GetString("pointsLabel", "Points");
            e.PointsPerActivity = j.GetFloat("pointsPerActivity", 1f);

            List<JsonValue> milestones = j.GetList("milestones");
            for (int i = 0; i < milestones.Count; i++)
            {
                JsonValue mj = milestones[i];
                EventMilestone m = new EventMilestone();
                m.Threshold = mj.GetLong("threshold", 0);
                m.Reward = EventReward.FromJson(mj);
                e.Milestones.Add(m);
            }

            e.CurrencyName = j.GetString("currencyName", "");
            e.CurrencyPerActivity = j.GetFloat("currencyPerActivity", 0f);

            List<JsonValue> currencyRewards = j.GetList("currencyRewards");
            for (int i = 0; i < currencyRewards.Count; i++)
            {
                JsonValue cj = currencyRewards[i];
                EventCurrencyTier t = new EventCurrencyTier();
                t.Cost = cj.GetLong("cost", 0);
                t.Reward = EventReward.FromJson(cj);
                e.CurrencyRewards.Add(t);
            }

            return e;
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

        /// <summary>
        /// Fraction of the damage Mirrored Wound returns to the attacker.
        ///
        /// Lives here rather than in the relic's text because a LEGENDARY relic contains no number
        /// (DESIGN-RELICS), exactly as EmberBurnStackCap and GaleExtraTurnsPerRound do for aspect
        /// identities: the card says "part of that damage returns", this constant says how much.
        /// Named for the relic rather than for the grade, so a second reflecting relic gets its own
        /// constant instead of silently inheriting this one's tuning.
        ///
        /// Placeholder, like every other number the relic pass ships - task 13's retune owns the
        /// real value.
        /// </summary>
        public float MirroredWoundReflectFraction = 0.25f;

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

        /// <summary>Relic Dust cost to purchase one LEGENDARY-grade relic from the shop.</summary>
        public int RelicShopPrice = 80;

        /// <summary>
        /// Relic Dust cost to purchase one MYTHIC-grade relic from the shop. Deliberately steep and
        /// deliberately Dust-only - no second currency track - so a mythic relic reads as a long
        /// grinding goal rather than a purchase, per DESIGN-RELICS' Mythic pricing section. At
        /// today's repeat-clear rates this is roughly 2-4 weeks of normal, single-stage daily
        /// farming, not a five-minute buy. A first-pass placeholder like every other relic number.
        /// </summary>
        public int RelicMythicShopPrice = 2400;

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

        // The Deep Recital - see DESIGN-DEEP-RECITAL.md. That document specifies an endless PvE
        // mode in fiction and shape (floor structure, aspect bands, a stamina-like gate, milestone
        // rewards) but explicitly leaves every number illustrative. It also names the gate
        // "stamina" (recitalStaminaMax/recitalStaminaRechargeHours) and describes a continuous
        // multi-floor run with no HP/energy reset between floors, ended by a wipe. What shipped
        // here is a different shape, decided in the session that built it: a persistent one-way
        // floor LADDER (PlayerProfile.RecitalHighestFloor is the permanent record, one floor is
        // attempted at a time, each attempt is its own fresh-HP battle exactly like every other
        // fight in the game) rather than a continuous run, and the gate is named Echoes rather than
        // Stamina - which collides with the document's own "echoes" (the hostile degraded patterns
        // you fight, not a resource you spend). Both are real, unreconciled conflicts with
        // DESIGN-DEEP-RECITAL.md, not oversights; see DESIGN-BUILD-ORDER's Deep Recital entry for
        // the full account. Every number below is a first-pass placeholder regardless of shape,
        // same discipline as Mythic relic pricing and the two starter events.

        /// <summary>Enemy level at floor 1. StatMath.ForEnemy(EnemyDef, level) needed no change to
        /// reach this - the level parameter alone already does everything floor-scaling needs.</summary>
        public int RecitalBaseLevel = 20;

        /// <summary>Enemy level added per floor climbed, on top of RecitalBaseLevel.</summary>
        public int RecitalLevelsPerFloor = 2;

        /// <summary>
        /// Extra enemy levels added once per completed 6-floor Aspect cycle (Ember, Tide, Verdant,
        /// Gale, Umbra, Lumen, repeat - RecitalGenerator.AspectCycleLength), stacked on top of the
        /// per-floor levels above. This is what makes difficulty step up at each cycle boundary
        /// rather than climbing at the same flat per-floor rate forever.
        /// </summary>
        public int RecitalBonusLevelsPerCycle = 3;

        /// <summary>Maximum Echoes banked at once. Regeneration pauses at this cap, the same shape
        /// idleCapHours already uses for idle income.</summary>
        public int RecitalEchoesMax = 5;

        /// <summary>Hours to regenerate one Echo. DESIGN-DEEP-RECITAL section 3's illustrative
        /// values (5 max, 4 hours) are kept as a starting point even though the resource's name and
        /// per-attempt shape changed.</summary>
        public float RecitalEchoesRechargeHours = 4f;

        /// <summary>Base gold on any floor clear, before the per-floor scaling below.</summary>
        public int RecitalRewardGoldBase = 200;

        /// <summary>Extra gold per floor number on top of RecitalRewardGoldBase.</summary>
        public int RecitalRewardGoldPerFloor = 20;

        /// <summary>Base Ember Shards on any floor clear, before per-floor scaling.</summary>
        public int RecitalRewardShardsBase = 10;

        /// <summary>Extra shards per floor number on top of RecitalRewardShardsBase.</summary>
        public int RecitalRewardShardsPerFloor = 1;

        /// <summary>Every floor number divisible by this (and not also a deep milestone below)
        /// grants a Relic Dust bonus on top of the ordinary gold/shard reward.</summary>
        public int RecitalMilestoneInterval = 10;

        /// <summary>Relic Dust granted at a milestone floor, times the milestone's own number
        /// (floor / RecitalMilestoneInterval) - milestone 1 (floor 10) grants this much, milestone
        /// 2 (floor 20) grants double, and so on.</summary>
        public int RecitalMilestoneDustPerTier = 150;

        /// <summary>Every floor number divisible by this grants a guaranteed relic instead of the
        /// ordinary milestone's Dust bonus - "something rarer" at deep milestones, per spec. Falls
        /// back to a large Dust grant if every relic in content is already owned, so the reward is
        /// never a silent no-op.</summary>
        public int RecitalDeepMilestoneInterval = 50;

        /// <summary>The Relic Dust fallback when a deep milestone has no unowned relic left to
        /// grant.</summary>
        public int RecitalDeepMilestoneDustFallback = 1000;

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
            b.MirroredWoundReflectFraction = j.GetFloat("mirroredWoundReflectFraction", b.MirroredWoundReflectFraction);
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
            b.RelicMythicShopPrice = j.GetInt("relicMythicShopPrice", b.RelicMythicShopPrice);
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
            b.RecitalBaseLevel = j.GetInt("recitalBaseLevel", b.RecitalBaseLevel);
            b.RecitalLevelsPerFloor = j.GetInt("recitalLevelsPerFloor", b.RecitalLevelsPerFloor);
            b.RecitalBonusLevelsPerCycle = j.GetInt("recitalBonusLevelsPerCycle", b.RecitalBonusLevelsPerCycle);
            b.RecitalEchoesMax = j.GetInt("recitalEchoesMax", b.RecitalEchoesMax);
            b.RecitalEchoesRechargeHours = j.GetFloat("recitalEchoesRechargeHours", b.RecitalEchoesRechargeHours);
            b.RecitalRewardGoldBase = j.GetInt("recitalRewardGoldBase", b.RecitalRewardGoldBase);
            b.RecitalRewardGoldPerFloor = j.GetInt("recitalRewardGoldPerFloor", b.RecitalRewardGoldPerFloor);
            b.RecitalRewardShardsBase = j.GetInt("recitalRewardShardsBase", b.RecitalRewardShardsBase);
            b.RecitalRewardShardsPerFloor = j.GetInt("recitalRewardShardsPerFloor", b.RecitalRewardShardsPerFloor);
            b.RecitalMilestoneInterval = j.GetInt("recitalMilestoneInterval", b.RecitalMilestoneInterval);
            b.RecitalMilestoneDustPerTier = j.GetInt("recitalMilestoneDustPerTier", b.RecitalMilestoneDustPerTier);
            b.RecitalDeepMilestoneInterval = j.GetInt("recitalDeepMilestoneInterval", b.RecitalDeepMilestoneInterval);
            b.RecitalDeepMilestoneDustFallback = j.GetInt("recitalDeepMilestoneDustFallback", b.RecitalDeepMilestoneDustFallback);
            return b;
        }
    }
}
