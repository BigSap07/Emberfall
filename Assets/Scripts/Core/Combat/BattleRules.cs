using System.Collections.Generic;
using Emberfall.Core.Data;

namespace Emberfall.Core.Combat
{
    /// <summary>Which side of the fight a rule effect lands on.</summary>
    public enum RuleSide
    {
        Enemies,
        Heroes,
        Both
    }

    /// <summary>Which bodies on that side. Read off the line as built, never off a template id, so a
    /// rule means the same thing whichever enemies a floor happens to field.</summary>
    public enum RulePick
    {
        /// <summary>Every living body on the side.</summary>
        All,
        /// <summary>The living body in the lowest slot.</summary>
        Front,
        /// <summary>The living body with the most Max HP; the lower slot on a tie.</summary>
        Toughest
    }

    /// <summary>
    /// One effect a rule puts on the field: an ordinary EffectDef, applied by each picked body TO
    /// ITSELF, at the opening of the fight or at the top of every round. Nothing new resolves it -
    /// it goes through the same ApplyEffect an ability's effect does, so a Shield is a Shield and a
    /// Slow can be cleansed like any other Slow.
    /// </summary>
    public sealed class RuleEffect
    {
        public RuleSide Side = RuleSide.Enemies;
        public RulePick Pick = RulePick.All;
        /// <summary>False: once, as the fight opens, before any BattleStart passive. True: at the top
        /// of every round, before any RoundStart passive.</summary>
        public bool EveryRound;
        public EffectDef Effect;
    }

    /// <summary>
    /// A standing change to how one fight is played, carried on the request like its seed. Built
    /// for the guild dungeon's floor conditions (DESIGN-GUILD-FLOOR-CONDITIONS), but nothing here
    /// knows guilds exist: every field is a primitive the engine already had - an energy refill, a
    /// heal, a stat line, a status, the turn order - with a number on it.
    ///
    /// NULL IS THE ONLY VALUE EVERY EXISTING CALLER PASSES, and a null request field changes
    /// nothing: no branch below it runs, no RNG is drawn, and no seeded fingerprint moves. A
    /// non-null BattleRules left at its defaults is also an exact no-op, field by field.
    /// </summary>
    public sealed class BattleRules
    {
        /// <summary>What the rule is called where a player reads it. Carried so an event or a log
        /// line can say where an effect came from.</summary>
        public string Name = "";

        /// <summary>Multiplies the Energy a body refills on its own - per basic attack and per hit
        /// taken (balance.json's energyPerBasicAttack and energyOnDamaged). Energy one unit HANDS
        /// another is untouched, which is the point: a slow refill argues for whoever hands it out.</summary>
        public float HeroEnergyGain = 1f;
        public float EnemyEnergyGain = 1f;

        /// <summary>Added to each body's opening Energy, then clamped into [0, EnergyMax].</summary>
        public int HeroStartEnergy;
        public int EnemyStartEnergy;

        /// <summary>Multiplies every heal a body receives, Regen included, after HealSuppression and
        /// before every other heal modifier.</summary>
        public float HeroHealing = 1f;
        public float EnemyHealing = 1f;

        /// <summary>Multiplies each body's Max HP as it is built; it opens at full. A difficulty
        /// knob and nothing else - the harness's control condition uses it to show what failing
        /// DESIGN-GUILD-FLOOR-CONDITIONS C5 looks like.</summary>
        public float HeroMaxHp = 1f;
        public float EnemyMaxHp = 1f;

        /// <summary>For this many rounds (the opening's BattleStart order included) every enemy acts
        /// before every hero, whatever the speeds say. Within each side the speed order stands.</summary>
        public int EnemiesActFirstRounds;

        public readonly List<RuleEffect> Effects = new List<RuleEffect>();

        public bool HasEffects(bool everyRound)
        {
            for (int i = 0; i < Effects.Count; i++)
                if (Effects[i] != null && Effects[i].Effect != null && Effects[i].EveryRound == everyRound) return true;
            return false;
        }
    }
}
