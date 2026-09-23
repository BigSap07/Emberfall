using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Model;

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
        /// A hero's stats with an equipped relic AND equipped armor folded in - THE entry point for
        /// both, and the reason armor reuses this mechanism rather than getting its own. Both
        /// CombatSimulation's setup and the hero detail screen call this, so the number a player
        /// reads on the card is by construction the number the simulation fights with.
        ///
        /// Order is relic, then armor, and it is fixed rather than incidental: the two do not
        /// commute for multiplicative stats, and a stat line that depended on which system happened
        /// to resolve first would be a stat line nobody could reproduce.
        ///
        /// Every argument after `level` is optional in the "null is an exact no-op" sense - a null
        /// relic, a null or empty armor list, and a null config each return exactly what
        /// ForHero(def, level) returns, with no branch anywhere downstream needing to know which of
        /// them was absent.
        /// </summary>
        public static UnitStats ForHero(HeroDef def, int level, RelicDef relic, List<ArmorPiece> armor, ArmorConfig config)
        {
            UnitStats s = ForHero(def, level);
            ApplyRelic(s, relic);
            ApplyArmor(s, armor, def != null ? def.Aspect : Aspect.Ember, config);
            return s;
        }

        /// <summary>
        /// A hero standing on the OPPONENT side, from a BattleLoadout snapshot (DESIGN-PVP section
        /// 3). ForHero's sibling, and deliberately a sibling rather than a fourth ForHero overload,
        /// because it differs in exactly two ways and both are properties of being a snapshot:
        ///
        /// 1. Stars come from the snapshot, not from def.Stars. A def's rarity is content and
        ///    content moves; a snapshot records what was actually fought, so it carries its own
        ///    rarity the same way it carries its own level.
        /// 2. The armor list arrives already in hand rather than being resolved out of a profile's
        ///    OwnedArmor, because an opponent's uids do not exist in the local player's collection.
        ///    See BattleLoadout for the full reasoning.
        ///
        /// Everything else is identical ON PURPOSE. It calls the same ApplyRelic and ApplyArmor, in
        /// the same fixed relic-then-armor order, with the same "null is an exact no-op" contract -
        /// so an opponent's gear goes through exactly the arithmetic the player's does. That
        /// equivalence is the whole reason a fight against one means anything; the moment the two
        /// sides compute stats differently, the mode is measuring two different games.
        ///
        /// This is also what the enemy path cannot do: ForEnemy passes a rarity multiplier of 1f
        /// and takes neither a relic nor armor, so an EnemyDef line has no way to express gear at
        /// all. That gap is why this exists rather than the opponent reusing ForEnemy.
        /// </summary>
        public static UnitStats ForOpponent(HeroDef def, int level, int stars, RelicDef relic,
                                            List<ArmorPiece> armor, ArmorConfig config)
        {
            if (def == null) return new UnitStats();
            UnitStats s = Build(def, level, StarMultiplier(stars));
            ApplyRelic(s, relic);
            ApplyArmor(s, armor, def.Aspect, config);
            return s;
        }

        /// <summary>
        /// Folds every equipped armor piece's rolls, plus whatever attunement thresholds the wearer
        /// has met, into an already-built UnitStats, in place. ApplyRelic's sibling: same in-place
        /// signature, same rounding rule, same "absent argument is an exact no-op" contract.
        ///
        /// Two passes, in a fixed order. First every piece's own rolls, primary and substats, in
        /// slot order - so the result does not depend on the order the caller happened to assemble
        /// the list in, which is why PlayerProfile.EquippedArmorOf is the only sanctioned way to
        /// build it. Then the attunement bonuses, last, because a set bonus is a bonus ON a
        /// completed set rather than a seventh piece.
        ///
        /// Attunement counts pieces whose OWN aspect matches the WEARER's, and reads nothing else -
        /// no counter-aspect penalty, no off-aspect bonus. A second aspect axis layered on top of
        /// AspectChart would make the wheel argue with itself.
        ///
        /// NOT seed-safe, for a stronger reason than ApplyRelic's: DeterministicRng.Chance
        /// short-circuits at p &lt;= 0 without drawing, and Block is exactly zero on every unit in
        /// shipped content, so the first armor piece that rolls Block adds one RNG draw to every
        /// incoming hit that unit takes. Effect Hit can do the same at the landing gate. That is
        /// what it means for these stats to be live and it is not a defect - it means no harness
        /// fingerprint check may equip armor. See DESIGN-ARMOR section 9.
        /// </summary>
        public static void ApplyArmor(UnitStats stats, List<ArmorPiece> armor, Aspect wearerAspect, ArmorConfig config)
        {
            if (stats == null || armor == null || armor.Count == 0) return;

            int matching = 0;
            for (int i = 0; i < armor.Count; i++)
            {
                ArmorPiece piece = armor[i];
                if (piece == null) continue;
                if (piece.Aspect == wearerAspect) matching++;

                List<ArmorStatRoll> rolls = piece.AllRolls();
                for (int r = 0; r < rolls.Count; r++) ApplyRoll(stats, rolls[r]);
            }

            if (config == null || matching <= 0) return;
            List<ArmorAttunementDef> met = config.AttunementsFor(wearerAspect, matching);
            for (int a = 0; a < met.Count; a++)
            {
                List<ArmorRollDef> bonuses = met[a].Bonuses;
                for (int b = 0; b < bonuses.Count; b++)
                {
                    ArmorRollDef def = bonuses[b];
                    // An attunement bonus is authored as a degenerate range (Min == Max), so Min is
                    // its value. Read through ArmorInfo.ModeFor rather than trusting a mode key,
                    // for the same reason the roller does - the mode is a fact about the stat.
                    ApplyRoll(stats, new ArmorStatRoll(def.Stat, ArmorInfo.ModeFor(def.Stat), def.Min));
                }
            }
        }

        /// <summary>
        /// One roll onto one stat. The rounding follows Build and ApplyRelic exactly rather than
        /// inventing a third rule: the four multiplied stats are the four Build rounds, everything
        /// else passes through raw. That is what keeps an armoured stat line the same SHAPE as an
        /// unarmoured one - whole HP, fractional crit chance - instead of showing 4271.4 max HP.
        ///
        /// The switch is total over ArmorStat and has no default case on purpose: adding a member
        /// to ArmorStat without adding it here would silently make that stat inert, and a stat the
        /// UI advertises and the maths ignores is the worst failure this system can have. There is
        /// a harness check (CheckArmorEveryStatReachesUnitStats) that proves every member moves its
        /// field.
        /// </summary>
        private static void ApplyRoll(UnitStats stats, ArmorStatRoll roll)
        {
            if (roll == null) return;
            bool multiply = roll.Mode == ArmorStatMode.Multiply;
            float v = roll.Value;

            switch (roll.Stat)
            {
                case ArmorStat.MaxHp:
                    stats.MaxHp = Round(multiply ? stats.MaxHp * (1f + v) : stats.MaxHp + v);
                    break;
                case ArmorStat.Attack:
                    stats.Attack = Round(multiply ? stats.Attack * (1f + v) : stats.Attack + v);
                    break;
                case ArmorStat.Defense:
                    stats.Defense = Round(multiply ? stats.Defense * (1f + v) : stats.Defense + v);
                    break;
                case ArmorStat.Speed:
                    stats.Speed = Round(multiply ? stats.Speed * (1f + v) : stats.Speed + v);
                    break;
                case ArmorStat.CritChance:
                    stats.CritChance = multiply ? stats.CritChance * (1f + v) : stats.CritChance + v;
                    break;
                case ArmorStat.CritDamage:
                    stats.CritDamage = multiply ? stats.CritDamage * (1f + v) : stats.CritDamage + v;
                    break;
                case ArmorStat.EffectHit:
                    stats.EffectHit = multiply ? stats.EffectHit * (1f + v) : stats.EffectHit + v;
                    break;
                case ArmorStat.EffectResist:
                    stats.EffectResist = multiply ? stats.EffectResist * (1f + v) : stats.EffectResist + v;
                    break;
                case ArmorStat.SkillDamage:
                    stats.SkillDamage = multiply ? stats.SkillDamage * (1f + v) : stats.SkillDamage + v;
                    break;
                case ArmorStat.Block:
                    stats.Block = multiply ? stats.Block * (1f + v) : stats.Block + v;
                    break;
                case ArmorStat.ArmorBreak:
                    stats.ArmorBreak = multiply ? stats.ArmorBreak * (1f + v) : stats.ArmorBreak + v;
                    break;
                case ArmorStat.ControlImmunity:
                    stats.ControlImmunity = multiply ? stats.ControlImmunity * (1f + v) : stats.ControlImmunity + v;
                    break;
                case ArmorStat.DamageReduction:
                    stats.DamageReduction = multiply ? stats.DamageReduction * (1f + v) : stats.DamageReduction + v;
                    break;
                case ArmorStat.HolyDamage:
                    stats.HolyDamage = multiply ? stats.HolyDamage * (1f + v) : stats.HolyDamage + v;
                    break;
            }
        }

        /// <summary>
        /// Enemies do not wear relics, so this takes none. Stated as a signature rather than as a
        /// comment on the call site. Enemies do not wear armor either, for the same reason and by
        /// the same means - there is no overload here that takes any.
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
            return LevelUpCost(cfg, currentLevel, 0);
        }

        /// <summary>
        /// The level above currentLevel, at an ascension tier, where currentLevel is the TIER level
        /// (GameSession.TierLevelOf). The tier's curve multiplier scales the cost as it scales the
        /// stride (LevelAt), so a coin buys the same stride of the ladder at every tier: ascension
        /// moves where gold goes, never what it buys. Without it a tier-2 level would sell six
        /// Levels at a tier-0 level's price, and the gold that already buys 70% of the simulated
        /// player's power would buy more of it the further they ascended.
        /// </summary>
        public static int LevelUpCost(BalanceConfig cfg, int currentLevel, int ascensionTier)
        {
            if (currentLevel < 1) currentLevel = 1;
            double cost = cfg.LevelCostA
                        + cfg.LevelCostB * currentLevel
                        + cfg.LevelCostC * currentLevel * currentLevel;
            cost *= CurveMultiplier(cfg, ascensionTier);
            return (int)Math.Round(cost, MidpointRounding.AwayFromZero);
        }

        // ---------- level caps and ascension (F1, F2, F5, F6, F7) ----------

        /// <summary>
        /// A hero's level cap IN TIER LEVELS: a function of its star tier and its ascension tier and
        /// of nothing else (F7). Account rank used to set it, and nothing does now. A tier past the
        /// last one balance.json authors reads as the last one, so a save from a build with more
        /// tiers still loads to a real cap. CapLevel is the same cap as a Level.
        /// </summary>
        public static int HeroLevelCap(BalanceConfig cfg, int stars, int ascensionTier)
        {
            if (stars < 5)
            {
                int[] low = cfg.LevelCapsBelowFiveStars;
                if (low == null || low.Length == 0) return 1;
                int i = Math.Max(1, stars) - 1;
                return Math.Max(1, low[Math.Min(i, low.Length - 1)]);
            }
            int[] caps = cfg.FiveStarLevelCaps;
            if (caps == null || caps.Length == 0) return 1;
            return Math.Max(1, caps[ClampTier(cfg, ascensionTier)]);
        }

        /// <summary>The highest ascension tier balance.json authors. Zero means ascension is off.</summary>
        public static int MaxAscensionTier(BalanceConfig cfg)
        {
            int n = cfg.FiveStarLevelCaps != null ? cfg.FiveStarLevelCaps.Length : 1;
            if (cfg.AscensionCurveMultipliers != null) n = Math.Min(n, cfg.AscensionCurveMultipliers.Length);
            int byCopies = cfg.AscensionCopies != null ? cfg.AscensionCopies.Length + 1 : 1;
            n = Math.Min(n, byCopies);
            return Math.Max(0, n - 1);
        }

        private static int ClampTier(BalanceConfig cfg, int tier)
        {
            if (tier < 0) return 0;
            int max = cfg.FiveStarLevelCaps != null ? cfg.FiveStarLevelCaps.Length - 1 : 0;
            return tier > max ? Math.Max(0, max) : tier;
        }

        /// <summary>A tier's curve multiplier: the levels of the one ladder each of its tier levels
        /// is worth, and the multiple its level cost carries. 1 at tier 0.</summary>
        public static float CurveMultiplier(BalanceConfig cfg, int ascensionTier)
        {
            float[] m = cfg.AscensionCurveMultipliers;
            if (ascensionTier <= 0 || m == null || m.Length == 0) return 1f;
            return m[Math.Min(ascensionTier, m.Length - 1)];
        }

        /// <summary>
        /// ONE LADDER, WALKED AT A TIER'S STRIDE. The Level a 5-star at this tier stands at on the
        /// given tier level.
        ///
        /// Ascension does not give a hero a second stat curve. It changes how far along the one it
        /// already has each of its levels moves it: tier t's first level stands one Level past tier
        /// t - 1's cap (F6: it out-powers that cap, by exactly one Level), and every tier level
        /// after it moves the hero CurveMultiplier(t) Levels, rounded. Since StatMath.Build is
        /// 1 + growth * (Level - 1), a tier whose first level stands at 1 + (M - 1) / growth and
        /// which steps M at a time has HP, Attack and Defense of M * (1 + growth * (tierLevel - 1)):
        /// M times tier 0's curve, the higher curve F6 asks for, exactly as a multiplier on the
        /// stats would give.
        ///
        /// WHY IT IS DONE ON LEVEL AND NOT AS A MULTIPLIER ON THE STATS, because it was first done as
        /// a multiplier and the harness measured what that cost. Combat reads a hero's level for
        /// more than its stats: Defense mitigation is defense / (defense + constant + perLevel *
        /// attacker's Level). A tier-1 hero whose stats were doubled by a multiplier stood at a
        /// lower Level than a tier-0 hero of the same power, so its blows were mitigated harder,
        /// and an even pinned line of them cleared 2-12's ceiling 39% of the time where the tier-0
        /// line at the same power cleared it 63% of the time. Power stopped predicting the fight,
        /// which every instrument and every recommendedPower in the campaign rests on. Walking the
        /// one ladder keeps Level the single number a stat line and a mitigation roll are both built
        /// from, so equal power is an equal fight at every tier, and no battle code had to change.
        ///
        /// Heroes below five stars never ascend (F1) and walk the ladder one Level at a time.
        /// </summary>
        public static int LevelAt(BalanceConfig cfg, int ascensionTier, int tierLevel)
        {
            if (tierLevel < 1) tierLevel = 1;
            return TierBaseLevel(cfg, ascensionTier) + Stride(cfg, ascensionTier, tierLevel);
        }

        /// <summary>The Level of a tier's first tier level: 1 at tier 0, and one Level past the
        /// previous tier's cap above it (F6).</summary>
        public static int TierBaseLevel(BalanceConfig cfg, int ascensionTier)
        {
            int level = 1;
            int tiers = Math.Min(Math.Max(0, ascensionTier), MaxAscensionTier(cfg));
            for (int t = 0; t < tiers; t++) level += Stride(cfg, t, HeroLevelCap(cfg, 5, t)) + 1;
            return level;
        }

        private static int Stride(BalanceConfig cfg, int ascensionTier, int tierLevel)
        {
            return (int)Math.Round(CurveMultiplier(cfg, ascensionTier) * (tierLevel - 1), MidpointRounding.AwayFromZero);
        }

        /// <summary>The tier level a Level stands at: the highest tier level whose Level is not
        /// above it, and at least 1. The inverse of LevelAt on every Level LevelAt produces.</summary>
        public static int TierLevelOf(BalanceConfig cfg, int ascensionTier, int level)
        {
            int tierLevel = 1;
            int cap = HeroLevelCap(cfg, 5, ascensionTier);
            while (tierLevel < cap && LevelAt(cfg, ascensionTier, tierLevel + 1) <= level) tierLevel++;
            return tierLevel;
        }

        /// <summary>The Level a hero stops at: its star tier's cap below five stars, and a 5-star's
        /// tier cap walked along its tier's stride.</summary>
        public static int CapLevel(BalanceConfig cfg, int stars, int ascensionTier)
        {
            if (stars < 5) return HeroLevelCap(cfg, stars, 0);
            return LevelAt(cfg, ascensionTier, HeroLevelCap(cfg, 5, ascensionTier));
        }

        /// <summary>Copies of the same hero the step from this tier to the next consumes, or -1
        /// where there is no next tier.</summary>
        public static int AscensionCopiesFor(BalanceConfig cfg, int ascensionTier)
        {
            if (ascensionTier < 0 || ascensionTier >= MaxAscensionTier(cfg)) return -1;
            return cfg.AscensionCopies[ascensionTier];
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
