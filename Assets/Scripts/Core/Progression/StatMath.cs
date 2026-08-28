using System;
using Emberfall.Core.Data;

namespace Emberfall.Core.Progression
{
    /// <summary>Resolved combat statistics for one unit at one level.</summary>
    public sealed class UnitStats
    {
        public float MaxHp;
        public float Attack;
        public float Defense;
        public float Speed;
        public float CritChance;
        public float CritDamage;
        public float EffectHit;
        public float EffectResist;
        public float SkillDamage;
        public float Block;
        public float ArmorBreak;
        public float ControlImmunity;
        public float DamageReduction;
        public float HolyDamage;

        public int Power
        {
            get { return StatMath.PowerScore(this); }
        }
    }

    /// <summary>
    /// Every formula that turns a definition plus a level into numbers.
    /// Deliberately boring and centralised so balancing is a one-file job.
    /// </summary>
    public static class StatMath
    {
        /// <summary>
        /// Star tier is applied here rather than baked into the JSON so base stats stay
        /// role-comparable. Curve is a placeholder subject to task-14 retuning.
        /// Stars 3/4/5 preserve the old Common/Rare/Epic multipliers exactly.
        /// </summary>
        public static float StarMultiplier(int stars)
        {
            switch (stars)
            {
                case 1: return 0.70f;
                case 2: return 0.85f;
                case 3: return 1.00f;
                case 4: return 1.16f;
                case 5: return 1.34f;
            }
            return 1.00f;
        }

        public static UnitStats ForHero(HeroDef def, int level)
        {
            float starMult = StarMultiplier(def.Stars);
            return Build(def, level, starMult);
        }

        /// <summary>
        /// A hero's stats with an equipped relic's stat bundle already folded in. THE entry point
        /// for relic stat bonuses, and deliberately the only one: both CombatSimulation's setup and
        /// the hero detail screen's preview call this, so the number a player reads on the card is
        /// by construction the number the simulation fights with. A null relic, or one with an
        /// empty Stats list, returns exactly what ForHero(def, level) returns.
        /// </summary>
        public static UnitStats ForHero(HeroDef def, int level, RelicDef relic)
        {
            UnitStats s = ForHero(def, level);
            ApplyRelic(s, relic);
            return s;
        }

        /// <summary>
        /// Folds a relic's stat bundle into an already-built UnitStats, in place. Grade-agnostic on
        /// purpose: a legendary (stat-only) relic's whole contribution runs through here and never
        /// reaches CombatSimulation, but a mythic relic's stat block ALSO runs through here - only
        /// its Hook is read separately, at its own resolution point inside the simulation. Both
        /// grades share one equipment slot, so this has to be safe to call with whatever is
        /// equipped; a relic with an empty Stats list (nothing shipped has one, but validation does
        /// not force one to be non-empty on the mythic grade) is a no-op through the loop below with
        /// no grade test needed to make it one.
        ///
        /// Rounding follows Build exactly rather than inventing its own rule: the four multiplied
        /// stats are the four Build rounds, and the four added ones are the four Build passes
        /// through raw. That is what keeps a relicced stat line the same SHAPE as an unrelicced one
        /// (whole HP, fractional crit chance) instead of showing a hero 4271.4 max HP.
        ///
        /// NOT seed-safe, and nobody should write down that it is. A stat bundle changes stat values
        /// rather than the number of RNG draws, with one real edge: DeterministicRng.Chance
        /// short-circuits at p &lt;= 0 and p &gt;= 1 WITHOUT drawing, so a relic that pushes a landing
        /// chance across either boundary adds or removes a draw. Marksman's Coil's Effect Hit is
        /// the one in the shipped set that can do it.
        /// </summary>
        public static void ApplyRelic(UnitStats stats, RelicDef relic)
        {
            if (stats == null || relic == null) return;

            for (int i = 0; i < relic.Stats.Count; i++)
            {
                RelicStatBonus b = relic.Stats[i];
                bool multiply = b.Mode == RelicStatMode.Multiply;
                switch (b.Stat)
                {
                    case RelicStat.MaxHp:
                        stats.MaxHp = Round(multiply ? stats.MaxHp * (1f + b.Value) : stats.MaxHp + b.Value);
                        break;
                    case RelicStat.Attack:
                        stats.Attack = Round(multiply ? stats.Attack * (1f + b.Value) : stats.Attack + b.Value);
                        break;
                    case RelicStat.Defense:
                        stats.Defense = Round(multiply ? stats.Defense * (1f + b.Value) : stats.Defense + b.Value);
                        break;
                    case RelicStat.Speed:
                        stats.Speed = Round(multiply ? stats.Speed * (1f + b.Value) : stats.Speed + b.Value);
                        break;
                    case RelicStat.CritChance:
                        stats.CritChance = multiply ? stats.CritChance * (1f + b.Value) : stats.CritChance + b.Value;
                        break;
                    case RelicStat.CritDamage:
                        stats.CritDamage = multiply ? stats.CritDamage * (1f + b.Value) : stats.CritDamage + b.Value;
                        break;
                    case RelicStat.EffectHit:
                        stats.EffectHit = multiply ? stats.EffectHit * (1f + b.Value) : stats.EffectHit + b.Value;
                        break;
                    case RelicStat.EffectResist:
                        stats.EffectResist = multiply ? stats.EffectResist * (1f + b.Value) : stats.EffectResist + b.Value;
                        break;
                }
            }
        }

        /// <summary>
        /// Enemies do not wear relics, so this takes none. Stated as a signature rather than as a
        /// comment on the call site.
        /// </summary>
        public static UnitStats ForEnemy(EnemyDef def, int level)
        {
            return Build(def, level, 1f);
        }

        private static UnitStats Build(UnitDef def, int level, float rarityMultiplier)
        {
            if (level < 1) level = 1;
            float scale = 1f + def.Growth * (level - 1);
            UnitStats s = new UnitStats();
            s.MaxHp = Round(def.BaseHp * scale * rarityMultiplier);
            s.Attack = Round(def.BaseAttack * scale * rarityMultiplier);
            s.Defense = Round(def.BaseDefense * scale * rarityMultiplier);
            s.Speed = Round(def.BaseSpeed + (level - 1) * 0.35f);
            s.CritChance = def.CritChance;
            s.CritDamage = def.CritDamage;
            // Flat pass-through, like CritChance/CritDamage - never scaled by level or rarity.
            // Scaling this would make every high-level unit incidentally debuff-immune as a side
            // effect of its level, which nobody decided.
            s.EffectHit = def.EffectHit;
            s.EffectResist = def.EffectResist;
            s.SkillDamage = def.SkillDamage;
            s.Block = def.Block;
            s.ArmorBreak = def.ArmorBreak;
            s.ControlImmunity = def.ControlImmunity;
            s.DamageReduction = def.DamageReduction;
            s.HolyDamage = def.HolyDamage;
            return s;
        }

        private static float Round(float v)
        {
            return (float)Math.Round(v, MidpointRounding.AwayFromZero);
        }

        public static int PowerScore(UnitStats s)
        {
            if (s == null) return 0;
            float p = s.MaxHp * 0.11f
                    + s.Attack * 2.4f
                    + s.Defense * 1.35f
                    + s.Speed * 1.9f
                    + s.CritChance * 260f
                    + (s.CritDamage - 1f) * 120f;
            return (int)Math.Round(p, MidpointRounding.AwayFromZero);
        }

        // ---------- hero levelling ----------

        public static int LevelUpCost(BalanceConfig cfg, int currentLevel)
        {
            if (currentLevel < 1) currentLevel = 1;
            double cost = cfg.LevelCostA
                        + cfg.LevelCostB * currentLevel
                        + cfg.LevelCostC * currentLevel * currentLevel;
            return (int)Math.Round(cost, MidpointRounding.AwayFromZero);
        }

        public static int HeroLevelCap(BalanceConfig cfg, int playerLevel)
        {
            int cap = cfg.HeroLevelCapBase + cfg.HeroLevelCapPerPlayerLevel * Math.Max(0, playerLevel - 1);
            if (cap > cfg.HeroLevelHardCap) cap = cfg.HeroLevelHardCap;
            return cap;
        }

        // ---------- account levelling ----------

        public static int PlayerXpForNextLevel(BalanceConfig cfg, int playerLevel)
        {
            if (playerLevel < 1) playerLevel = 1;
            double xp = cfg.PlayerXpBase * Math.Pow(playerLevel, cfg.PlayerXpExponent);
            return (int)Math.Round(xp, MidpointRounding.AwayFromZero);
        }

        // ---------- idle income ----------

        /// <summary>
        /// Rate resolved from the player's best cleared stage. Pass null when StagesCleared is
        /// zero (no stage cleared yet) to get the floor rate from BalanceConfig.
        /// </summary>
        public static float IdleGoldPerSecond(BalanceConfig cfg, StageDef stage)
        {
            float perHour = stage != null ? stage.IdleGoldPerHour : cfg.IdleGoldPerHourFloor;
            return perHour / 3600f;
        }

        public static float IdleShardsPerSecond(BalanceConfig cfg, StageDef stage)
        {
            float perHour = stage != null ? stage.IdleShardsPerHour : cfg.IdleShardsPerHourFloor;
            return perHour / 3600f;
        }
    }
}
