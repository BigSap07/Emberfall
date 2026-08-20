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
        /// <summary>Rarity is applied here rather than baked into the JSON so base stats stay role-comparable.</summary>
        public static float RarityMultiplier(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Rare: return 1.16f;
                case Rarity.Epic: return 1.34f;
            }
            return 1f;
        }

        public static UnitStats ForHero(HeroDef def, int level)
        {
            float rarity = RarityMultiplier(def.Rarity);
            return Build(def, level, rarity);
        }

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

        public static float IdleGoldPerSecond(BalanceConfig cfg, int stagesCleared)
        {
            return (cfg.IdleGoldPerHourBase + cfg.IdleGoldPerHourPerStage * stagesCleared) / 3600f;
        }

        public static float IdleShardsPerSecond(BalanceConfig cfg, int stagesCleared)
        {
            return (cfg.IdleShardsPerHourBase + cfg.IdleShardsPerHourPerStage * stagesCleared) / 3600f;
        }
    }
}
