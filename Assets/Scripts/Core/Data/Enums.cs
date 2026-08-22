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

    public enum Rarity
    {
        Common = 3,
        Rare = 4,
        Epic = 5
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
        Dispel
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
        Silence, // inert placeholder — no cast-blocking logic yet
        Stun,    // inert placeholder — no turn-blocking logic yet
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
        OnDamaged
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
