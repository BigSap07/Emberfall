using System;

namespace Emberfall.Core.Data
{
    /// <summary>Elemental identity of a unit. Drives the counter chart and colour identity.</summary>
    public enum Aspect
    {
        Ember,   // fire / aggression
        Tide,    // water / control
        Verdant, // growth / sustain
        Gale,    // wind / speed
        Umbra,   // shadow
        Lumen    // light
    }

    /// <summary>Combat archetype. Purely descriptive - behaviour comes from the ability data.</summary>
    public enum HeroRole
    {
        Bulwark, // tank
        Striker, // single target damage
        Breaker, // area damage
        Mender,  // healer
        Warden,  // support
        Stalker  // assassin
    }

    public enum AbilityKind
    {
        BasicAttack,
        Active,
        Passive
    }

    public enum TargetMode
    {
        Self,
        LowestHpEnemy,
        HighestHpEnemy,
        HighestAttackEnemy,
        FrontEnemy,
        RandomEnemy,
        AllEnemies,
        LowestHpAlly,
        AllAllies,
        RandomAlly,
        LastAttacker,
        LastTarget
    }

    public enum EffectKind
    {
        Damage,
        Heal,
        Shield,
        ApplyStatus,
        Cleanse,
        EnergyGain,
        Dispel,
        SwapPositions
    }

    public enum StatusKind
    {
        None,
        AttackUp,
        AttackDown,
        DefenseUp,
        DefenseDown,
        Burn,
        Regen,
        Shield,
        Taunt,
        Focus,   // increased critical chance
        Slow,    // reduced speed
        Silence, // blocks the Active on the holder's next turn; a Basic Attack resolves instead
        Stun,    // skips the holder's entire next turn - no Basic Attack, no Active
        Stealth  // inert placeholder — no targeting-visibility logic yet
    }

    public enum PassiveTrigger
    {
        None,
        BattleStart,
        TurnStart,
        AfterBasicAttack,
        OnCriticalHit,
        OnKill,
        OnHpBelowThreshold,
        OnDamaged,
        OnBlock
    }

    /// <summary>
    /// A relic's grade, per DESIGN-RELICS "The word is grade, and it is not tier or rarity".
    ///
    /// Deliberately NOT called tier or rarity, because both words are already taken and either
    /// would silently overload one: "tier" is a relic's upgrade level (OwnedRelic.Tier,
    /// BalanceConfig.RelicMaxTier), and "rarity" is a hero's star rarity (StatMath.StarMultiplier).
    /// A relic has both a grade and a tier and they are orthogonal - a common relic upgrades from
    /// tier 1 to tier 2 exactly like a legendary one does.
    /// </summary>
    public enum RelicGrade
    {
        /// <summary>
        /// A bundle of flat and percentage stats, resolved entirely in StatMath before the
        /// simulation starts. Carries RelicStatBonus entries and Hook is always None: a common
        /// relic never enters the effect pipeline, so it cannot contest a hook with an aspect or
        /// with a legendary relic. Numerous, cheap to author, aspect-neutral and universal.
        /// </summary>
        Common,

        /// <summary>
        /// Rare, rule-breaking, and number-free. Carries a Hook and no RelicStatBonus entries at
        /// all. This is the grade DESIGN-RELICS' original founding standard still applies to in
        /// full: it changes a rule in the effect pipeline, states itself as one sentence, and
        /// contains no number. If it cannot be written that way, it is a common relic wearing the
        /// wrong grade.
        /// </summary>
        Legendary
    }

    /// <summary>
    /// Which rule in the effect pipeline a LEGENDARY relic bends. One relic, one hook.
    ///
    /// The "a relic changes a RULE, never a number" constraint that shapes this enum is now the
    /// legendary grade's bar specifically, not a blanket rule for all relics - DESIGN-RELICS
    /// withdrew the blanket version on 2026-08-26 when the common grade was introduced. It is
    /// undiminished here: there is still deliberately no "add N%" member, because a relic that
    /// wants one is a common relic and belongs in RelicStatBonus, which never reaches this enum.
    ///
    /// Both members below are hooks no aspect holds. DESIGN-RELICS "Hook contention: aspects win
    /// by default" - Tide owns the AddEnergy overflow redirect, Verdant owns the ApplyHealValue
    /// overflow banking, Ember owns Burn stacking, Orrin is Lumen - and a legendary relic must
    /// find a hook none of them claims rather than compete for one.
    /// </summary>
    public enum RelicHook
    {
        None,

        /// <summary>
        /// The Unbroken Ledger. A harmful effect from the wearer passes through
        /// ResolveLandingChance with the target's Effect Resist forced to zero.
        ///
        /// Not "hook the chance gate": the gate stopped being _rng.Chance(effect.Chance) when
        /// Effect Hit and Effect Resist shipped. It is the landing chance that gets rebuilt, and
        /// that arithmetic is claimed by the stat layer rather than by any aspect, which is what
        /// makes it available. Control Immunity is deliberately NOT bypassed - it is a separate,
        /// narrower layer that only ever touches Silence and Stun, and erasing both would make
        /// hard control unanswerable by any defensive stat in the game.
        /// </summary>
        BypassEffectResist,

        /// <summary>
        /// Mirrored Wound. When the wearer takes damage, a fraction of it returns to whoever dealt
        /// it. The fraction lives in balance.json (MirroredWoundReflectFraction), never in the
        /// relic's text, exactly as emberBurnStackCap and galeExtraTurnsPerRound do for aspects -
        /// which is how a legendary relic states itself with no number and still has a magnitude.
        ///
        /// Shares PassiveTrigger.OnDamaged's dispatch point but not its mechanism, and that trigger
        /// is NOT unused: tideturned_passive ("Backwash") already listens on it in enemies.json.
        /// The reflect is built to coexist with it rather than to assume an empty trigger.
        /// </summary>
        ReflectDamage
    }

    /// <summary>
    /// How one RelicStatBonus combines with the stat it names. Which mode a stat takes is not one
    /// answer, and DESIGN-RELICS decides it per stat rather than leaving it to the content author:
    /// the eight starting common relics use one "%" sign for two different operations because the
    /// stats they touch are stored in two different shapes.
    /// </summary>
    public enum RelicStatMode
    {
        /// <summary>
        /// Attack, Defense, MaxHp, Speed. Large scaled numbers built off BaseAttack * scale *
        /// rarityMultiplier, so "+14% Attack" means Attack *= 1.14.
        /// </summary>
        Multiply,

        /// <summary>
        /// CritChance, CritDamage, EffectHit, EffectResist. Stored as small fractions or as a
        /// multiplier and deliberately never scaled by level or star rarity, so multiplying them
        /// does not work: a multiplicative +12% on a 0.08 crit chance is +0.0096, which is nothing,
        /// and on Kalyx's 0.00 Effect Hit it is exactly zero. So "+12% Crit Chance" means
        /// CritChance += 0.12, and "+20% Crit Damage" means CritDamage += 0.20 - 1.5 becomes 1.7.
        /// </summary>
        Add
    }

    /// <summary>
    /// Which UnitStats field a RelicStatBonus moves. Deliberately a closed enum rather than a
    /// string field name: the common grade adds no new stat, every member below already exists on
    /// UnitStats and is already read by the simulation, and a typo'd stat name should not parse.
    /// </summary>
    public enum RelicStat
    {
        MaxHp,
        Attack,
        Defense,
        Speed,
        CritChance,
        CritDamage,
        EffectHit,
        EffectResist
    }

    public static class AspectChart
    {
        // Narrowed from 1.20/0.85 per DESIGN-ASPECTS section 4: a 41% swing between an advantaged
        // and a disadvantaged attacker out-argues every aspect identity, which forces one correct
        // counter-colour per stage and turns team building into a compliance check. At 1.10/0.92
        // the wheel is a tiebreak between two lines you'd already be happy with, which is what a
        // secondary layer is. Nothing reads these as literals - see Debrief.AspectFactorThreshold
        // and DevHarness CheckDebriefAspect* for the two places that used to.
        public const float Advantage = 1.10f;
        public const float Disadvantage = 0.92f;

        /// <summary>Ember &gt; Verdant &gt; Tide &gt; Gale &gt; Ember. Lumen and Umbra counter each other.</summary>
        public static float Multiplier(Aspect attacker, Aspect defender)
        {
            if (Beats(attacker, defender)) return Advantage;
            if (Beats(defender, attacker)) return Disadvantage;
            return 1f;
        }

        public static bool Beats(Aspect a, Aspect b)
        {
            switch (a)
            {
                case Aspect.Ember: return b == Aspect.Verdant;
                case Aspect.Verdant: return b == Aspect.Tide;
                case Aspect.Tide: return b == Aspect.Gale;
                case Aspect.Gale: return b == Aspect.Ember;
                case Aspect.Lumen: return b == Aspect.Umbra;
                case Aspect.Umbra: return b == Aspect.Lumen;
            }
            return false;
        }

        public static string DisplayName(Aspect a)
        {
            return a.ToString().ToUpperInvariant();
        }
    }

    public static class StatusInfo
    {
        public static bool IsHarmful(StatusKind kind)
        {
            return kind == StatusKind.AttackDown
                || kind == StatusKind.DefenseDown
                || kind == StatusKind.Burn
                || kind == StatusKind.Slow
                || kind == StatusKind.Silence
                || kind == StatusKind.Stun;
        }

        public static string DisplayName(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.AttackUp: return "Attack Up";
                case StatusKind.AttackDown: return "Attack Down";
                case StatusKind.DefenseUp: return "Guard Up";
                case StatusKind.DefenseDown: return "Guard Down";
                case StatusKind.Burn: return "Cinder";
                case StatusKind.Regen: return "Renewal";
                case StatusKind.Shield: return "Ward";
                case StatusKind.Taunt: return "Provoke";
                case StatusKind.Focus: return "Focus";
                case StatusKind.Slow: return "Mire";
                case StatusKind.Silence: return "Silence";
                case StatusKind.Stun: return "Stun";
                case StatusKind.Stealth: return "Stealth";
            }
            return kind.ToString();
        }

        /// <summary>Short tag drawn on the unit health bar.</summary>
        public static string ShortTag(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.AttackUp: return "ATK+";
                case StatusKind.AttackDown: return "ATK-";
                case StatusKind.DefenseUp: return "DEF+";
                case StatusKind.DefenseDown: return "DEF-";
                case StatusKind.Burn: return "BRN";
                case StatusKind.Regen: return "REG";
                case StatusKind.Shield: return "WRD";
                case StatusKind.Taunt: return "PRV";
                case StatusKind.Focus: return "FOC";
                case StatusKind.Slow: return "MIR";
                case StatusKind.Silence: return "SIL";
                case StatusKind.Stun: return "STN";
                case StatusKind.Stealth: return "STH";
            }
            return "?";
        }
    }

    public static class EnumUtil
    {
        public static T Parse<T>(string text, T fallback) where T : struct
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            try
            {
                return (T)Enum.Parse(typeof(T), text.Trim(), true);
            }
            catch (Exception)
            {
                return fallback;
            }
        }
    }
}
