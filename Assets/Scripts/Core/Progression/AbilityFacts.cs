using System;
using System.Collections.Generic;
using System.Globalization;
using Emberfall.Core.Data;

namespace Emberfall.Core.Progression
{
    public enum AbilityFactKind
    {
        /// <summary>The Energy an active needs before it can be cast.</summary>
        Cost,
        /// <summary>When a passive fires, where the data names a moment or a threshold.</summary>
        Trigger,
        /// <summary>How often a passive may fire.</summary>
        Limit,
        /// <summary>A status the ability applies: its name, its size where the size is a plain
        /// stat change, and how many rounds it lasts.</summary>
        Status,
        /// <summary>How many rounds a Ward the ability grants lasts.</summary>
        Ward,
        /// <summary>Energy the ability grants or takes.</summary>
        Energy,
        /// <summary>The stack count a stack-threshold passive waits for.</summary>
        Needs
    }

    /// <summary>
    /// One line of the card under the ability's prose and above its numbers: a duration, a
    /// threshold, an Energy cost or a status size. Text is what the player reads; the numeric
    /// fields are the values the text was built from, kept so the harness can compare each against
    /// what CombatSimulation does with the same effect.
    /// </summary>
    public sealed class AbilityFact
    {
        public AbilityFactKind Kind;
        public string Label = "";
        public string Text = "";

        /// <summary>The effect this came from, or -1 for a fact about the ability or passive.</summary>
        public int EffectIndex = -1;

        /// <summary>Status kind, for Status facts.</summary>
        public StatusKind Status = StatusKind.None;

        /// <summary>Rounds a status or Ward lasts, as the simulation stamps it (at least 1).</summary>
        public int Rounds;

        /// <summary>The magnitude the simulation stores on the status entry, for Status facts. For
        /// Cinder and Renewal this is Attack times the authored magnitude, like the simulation's.</summary>
        public float Stored;

        /// <summary>True when Text quotes Stored as a size the harness can read back from the
        /// simulation, rather than only naming the status and its duration.</summary>
        public bool QuotesSize;

        /// <summary>Cost, Energy delta, threshold fraction, limit rounds or stack count, by Kind.</summary>
        public float Amount;
    }

    /// <summary>
    /// The facts an ability's card prints besides its damage, healing and ward numbers: Energy
    /// cost, durations, thresholds, limits and status sizes.
    ///
    /// Same split as AbilityMath: this decides WHAT the numbers are and how they are worded, from
    /// the effect data and never from the description; the harness (CheckAbilityFactsMatchTheSimulation)
    /// fires the same effects through CombatSimulation and proves each fact equal to what the fight
    /// does. Docs/ABILITY-TEXT-STYLE.md keeps numbers out of the prose precisely so that this is the
    /// only place they are written.
    ///
    /// A status size is quoted only for the kinds whose effect is a plain change to one stat read
    /// back through a CombatUnit.Effective* getter. Every other status is named with its duration:
    /// a size the card cannot back with a getter would be a number nobody proved.
    /// </summary>
    public static class AbilityFacts
    {
        /// <summary>The facts for one ability, in reading order. <paramref name="passive"/> is the
        /// PassiveDef when the ability is a passive read through AbilityMath.AsAbility, else null.
        /// <paramref name="stats"/> may be null; a Cinder or Renewal then names no per-round amount.</summary>
        public static List<AbilityFact> For(AbilityDef ability, PassiveDef passive, UnitStats stats)
        {
            List<AbilityFact> facts = new List<AbilityFact>();
            if (ability == null) return facts;

            if (passive == null && ability.Kind == AbilityKind.Active && ability.EnergyCost > 0)
            {
                AbilityFact cost = new AbilityFact();
                cost.Kind = AbilityFactKind.Cost;
                cost.Label = "COST";
                cost.Text = ability.EnergyCost + " Energy";
                cost.Amount = ability.EnergyCost;
                facts.Add(cost);
            }

            if (passive != null) AddPassiveFacts(facts, passive);

            for (int i = 0; i < ability.Effects.Count; i++)
                AddEffectFacts(facts, ability.Effects[i], i, stats);
            return facts;
        }

        private static void AddPassiveFacts(List<AbilityFact> facts, PassiveDef p)
        {
            string when = null;
            bool threshold = false;
            switch (p.Trigger)
            {
                case PassiveTrigger.BattleStart: when = "At the start of battle"; break;
                case PassiveTrigger.RoundStart: when = "At the start of each round"; break;
                case PassiveTrigger.RoundEnd: when = "At the end of each round"; break;
                case PassiveTrigger.TurnStart: when = "At the start of its own turn"; break;
                case PassiveTrigger.AfterBasicAttack: when = "After its basic attack"; break;
                case PassiveTrigger.AfterActiveCast: when = "After its active skill"; break;
                case PassiveTrigger.OnHpBelowThreshold:
                    when = "At " + Pct(p.Threshold) + " HP or lower"; threshold = true; break;
                case PassiveTrigger.OnAllyHpBelowThreshold:
                    when = "When an ally falls to " + Pct(p.Threshold) + " HP or lower"; threshold = true; break;
                case PassiveTrigger.OnEnemyHpBelowThreshold:
                    when = "When an enemy falls to " + Pct(p.Threshold) + " HP or lower"; threshold = true; break;
            }
            if (when != null)
            {
                AbilityFact f = new AbilityFact();
                f.Kind = AbilityFactKind.Trigger;
                f.Label = "WHEN";
                f.Text = when;
                f.Amount = threshold ? p.Threshold : 0f;
                facts.Add(f);
            }

            if (p.Trigger == PassiveTrigger.OnStackThreshold && p.StackThresholdCount > 0
                && p.StackThresholdStatus != StatusKind.None)
            {
                AbilityFact f = new AbilityFact();
                f.Kind = AbilityFactKind.Needs;
                f.Label = "NEEDS";
                f.Text = p.StackThresholdCount + " " + StatusInfo.DisplayName(p.StackThresholdStatus);
                f.Amount = p.StackThresholdCount;
                f.Status = p.StackThresholdStatus;
                facts.Add(f);
            }

            string limit = null;
            float limitAmount = 0f;
            if (p.Limit == PassiveLimit.OncePerRound) limit = "Once per round";
            else if (p.Limit == PassiveLimit.EveryNRounds)
            { limit = "Then not again for " + p.LimitRounds + " rounds"; limitAmount = p.LimitRounds; }
            else if (p.Limit == PassiveLimit.OncePerBattle
                     || (p.OncePerBattle && p.Trigger != PassiveTrigger.BattleStart))
                limit = "Once per battle";
            if (limit != null)
            {
                AbilityFact f = new AbilityFact();
                f.Kind = AbilityFactKind.Limit;
                f.Label = "LIMIT";
                f.Text = limit;
                f.Amount = limitAmount;
                facts.Add(f);
            }
        }

        private static void AddEffectFacts(List<AbilityFact> facts, EffectDef e, int index, UnitStats stats)
        {
            if (e == null) return;

            if (e.Kind == EffectKind.Shield)
            {
                AbilityFact f = new AbilityFact();
                f.Kind = AbilityFactKind.Ward;
                f.Label = "WARD";
                f.Rounds = Math.Max(1, e.Duration);
                f.Text = "Lasts " + RoundsText(f.Rounds);
                f.EffectIndex = index;
                AddUnique(facts, f);
                return;
            }

            if (e.Kind == EffectKind.EnergyGain)
            {
                int amount = (int)Math.Round(e.Flat + e.Power * 100f, MidpointRounding.AwayFromZero);
                if (amount == 0) return;
                AbilityFact f = new AbilityFact();
                f.Kind = AbilityFactKind.Energy;
                f.Label = "ENERGY";
                f.Amount = amount;
                f.Text = (amount > 0 ? "+" : "-") + Math.Abs(amount) + " Energy";
                f.EffectIndex = index;
                AddUnique(facts, f);
                return;
            }

            if (e.Kind != EffectKind.ApplyStatus || e.Status == StatusKind.None) return;
            if (StatusInfo.IsInert(e.Status)) return;

            AbilityFact s = new AbilityFact();
            s.Kind = AbilityFactKind.Status;
            s.Label = "STATUS";
            s.Status = e.Status;
            s.EffectIndex = index;
            s.Rounds = Math.Max(1, e.Duration);

            string name = StatusInfo.DisplayName(e.Status);
            string size = null;

            if (e.Status == StatusKind.Burn || e.Status == StatusKind.Regen)
            {
                if (e.HolderHpTick > 0f)
                    size = Pct(e.HolderHpTick) + (e.HolderHpTickCurrent ? " of current HP" : " of Max HP") + " each round";
                else if (stats != null)
                {
                    s.Stored = stats.Attack * e.Magnitude;
                    s.QuotesSize = true;
                    size = Grouped(TickAmount(s.Stored)) + " each round";
                }
            }
            else
            {
                s.Stored = e.Magnitude;
                string sizeText = StatSize(e.Status, e.Magnitude);
                if (sizeText != null) { size = sizeText; s.QuotesSize = true; }
            }

            s.Text = name + (size != null ? " " + size : "") + ", " + RoundsText(s.Rounds);
            AddUnique(facts, s);
        }

        /// <summary>What one Cinder or Renewal tick is worth: the stored amount rounded the way the
        /// simulation rounds it, never below 1.</summary>
        public static int TickAmount(float stored)
        {
            int tick = (int)Math.Round(stored, MidpointRounding.AwayFromZero);
            return tick < 1 ? 1 : tick;
        }

        /// <summary>The kinds whose size is a plain change to one stat that a CombatUnit
        /// Effective* getter returns: a share of the base for the multiplicative ones, a point
        /// value on a fraction for the additive ones. Null for anything else.</summary>
        public static string StatSize(StatusKind kind, float magnitude)
        {
            string sign, noun;
            switch (kind)
            {
                case StatusKind.AttackUp: case StatusKind.DefenseUp: case StatusKind.SpeedUp:
                case StatusKind.MaxHpUp:
                    sign = "+"; noun = ""; break;
                case StatusKind.AttackDown: case StatusKind.DefenseDown: case StatusKind.Slow:
                    sign = "-"; noun = kind == StatusKind.Slow ? " Speed" : ""; break;
                case StatusKind.Focus:
                    sign = "+"; noun = " Crit Chance"; break;
                case StatusKind.DamageReductionUp:
                    sign = "+"; noun = " damage reduction"; break;
                case StatusKind.DamageReductionDown:
                    sign = "-"; noun = " damage reduction"; break;
                case StatusKind.BlockUp: sign = "+"; noun = " Block"; break;
                case StatusKind.BlockDown: sign = "-"; noun = " Block"; break;
                case StatusKind.EffectHitUp: sign = "+"; noun = " Effect Hit"; break;
                case StatusKind.EffectHitDown: sign = "-"; noun = " Effect Hit"; break;
                case StatusKind.EffectResistUp: sign = "+"; noun = " Effect Resist"; break;
                case StatusKind.EffectResistDown: sign = "-"; noun = " Effect Resist"; break;
                case StatusKind.CritDamageUp: sign = "+"; noun = " Crit Damage"; break;
                case StatusKind.CritDamageDown: sign = "-"; noun = " Crit Damage"; break;
                case StatusKind.SkillDamageUp: sign = "+"; noun = " Skill Damage"; break;
                case StatusKind.SkillDamageDown: sign = "-"; noun = " Skill Damage"; break;
                case StatusKind.ArmorBreakUp: sign = "+"; noun = " Armor Break"; break;
                case StatusKind.ArmorBreakDown: sign = "-"; noun = " Armor Break"; break;
                default: return null;
            }
            return sign + Pct(magnitude) + noun;
        }

        private static void AddUnique(List<AbilityFact> facts, AbilityFact f)
        {
            for (int i = 0; i < facts.Count; i++)
                if (facts[i].Kind == f.Kind && facts[i].Text == f.Text) return;
            facts.Add(f);
        }

        private static string RoundsText(int rounds)
        {
            return rounds == 1 ? "1 round" : rounds + " rounds";
        }

        private static string Grouped(int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>A fraction as a percentage with no decimal unless the authored value has one.</summary>
        public static string Pct(float fraction)
        {
            float percent = fraction * 100f;
            float rounded = (float)Math.Round(percent);
            if (Math.Abs(percent - rounded) < 0.05f)
                return ((int)rounded).ToString(CultureInfo.InvariantCulture) + "%";
            return percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }
    }
}
