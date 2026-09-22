using System;
using System.Collections.Generic;
using Emberfall.Core.Data;

namespace Emberfall.Core.Progression
{
    /// <summary>Which of the three quantities a card can print an ability effect carries.</summary>
    public enum AbilityValueKind
    {
        Damage,
        Heal,
        Shield
    }

    /// <summary>
    /// One number an ability puts on the screen, already resolved against a hero's own stats.
    ///
    /// One of these per DAMAGE, HEAL or SHIELD effect, in authoring order. Nothing here is a
    /// summary of an ability: an ability that hits and then mends is two entries, because those
    /// are two numbers a player reads separately and folding them would produce a figure that is
    /// true of neither.
    /// </summary>
    public sealed class AbilityValue
    {
        /// <summary>Damage, healing or ward.</summary>
        public AbilityValueKind Kind;

        /// <summary>Where this came from inside the ability's own effect list, so a caller that
        /// needs the EffectDef back can index straight to it rather than matching on anything.</summary>
        public int EffectIndex;

        /// <summary>What one application is worth at these stats, rounded the way the simulation
        /// rounds it. Zero and meaningless when <see cref="Derived"/> is set.</summary>
        public int PerHit;

        /// <summary>How many times one firing applies it. Always 1 for a heal or a ward: the
        /// simulation's Hits loop is on the damage branch alone.</summary>
        public int Hits;

        /// <summary>The whole firing, which is what a card prints: PerHit times Hits.</summary>
        public int Amount;

        /// <summary>The authored multiplier as a percentage of Attack - the secondary line. Read
        /// from the effect, never from the description prose.</summary>
        public float Percent;

        /// <summary>True when the effect carries a clause that decides whether it resolves at all.
        /// The number is what it is worth WHEN it lands, not what it is worth on average.</summary>
        public bool Conditional;

        /// <summary>True when part of the amount depends on something the card cannot know - a
        /// share of the target's health, or a counter the fight has not built yet. The printed
        /// number is the part that depends on the caster alone, and is therefore a floor.</summary>
        public bool Partial;

        /// <summary>True when the effect's whole amount is a runtime quantity rather than the
        /// caster's Attack - what a Ledger just paid out, what a removal just took. There is no
        /// honest flat number for one of these, so a card prints the percentage alone.</summary>
        public bool Derived;

        /// <summary>
        /// True when there is a flat number worth printing at all.
        ///
        /// False for the eleven effects in the roster authored at power 0 whose whole payout is a
        /// share of the target's health - "2% of its Max HP, up to 200% of Attack". Their
        /// <see cref="Amount"/> is still computed, because the harness compares it against a
        /// simulation run with the target term taken off, but it is the minimum-damage floor
        /// rather than a figure any card should lead with.
        /// </summary>
        public bool HasFlat;

        /// <summary>True when one firing lands this on more than one body.</summary>
        public bool Area;
    }

    /// <summary>
    /// What an ability's numbers ARE, for every surface that prints them.
    ///
    /// THE POINT OF THIS FILE is that a percentage does not compare across star tiers and a flat
    /// value does. A 1-star's basic is authored at 437% and a 5-star's at 110% because the
    /// percentage is priced against its own owner's Attack, and the tier already scaled that
    /// Attack down - so the tier is divided out of the number twice and the rarest hero in the
    /// game reads as the weakest. Multiplying it back out against the hero's own stat line gives
    /// 2,231 against 1,334, which is a pair of numbers a player can check in a fight.
    /// Docs/DESIGN-DAMAGE-DISPLAY.md is the argument in full; this is its arithmetic.
    ///
    /// IT IS COMPUTED FROM THE EFFECT DATA, never from the description. Many descriptions in the
    /// roster carry a number in prose, some of those prose numbers disagree with the effect
    /// beneath them, and a few carry drafting notes addressed to a designer. A display that parsed
    /// the sentence would print the wrong number and would break the day an author rephrased it.
    /// The sentence is left exactly where it is.
    ///
    /// IT LIVES IN CORE, NOT IN UI, for one reason: the harness compiles Core, so it can run this
    /// formula and CombatSimulation side by side. CheckAbilityNumbersMatchTheSimulation does
    /// exactly that for every authored basic and active in the roster, so the number on the card
    /// cannot drift away from the number in the fight without a check going red.
    ///
    /// WHAT IT DELIBERATELY DOES NOT MODEL: the target's Defence, the crit roll and the damage
    /// variance. Those are properties of a HIT rather than of an ability, and two of the three
    /// belong to somebody else entirely. The screen says so once, in a note under the row, rather
    /// than qualifying every line.
    /// </summary>
    public static class AbilityMath
    {
        /// <summary>True when this effect is one of the three kinds that carries a printable
        /// number. Asked rather than compared at each site so the set lives in one place.</summary>
        public static bool Carries(EffectDef effect)
        {
            if (effect == null) return false;
            return effect.Kind == EffectKind.Damage
                || effect.Kind == EffectKind.Heal
                || effect.Kind == EffectKind.Shield;
        }

        /// <summary>
        /// A passive read as an ability, so one formula covers all three kinds a hero card shows.
        ///
        /// PassiveDef and AbilityDef share no base type - the two differ by a trigger and a limit,
        /// not by what their effects do - and every caller that wants to ask "what is this worth"
        /// would otherwise need a second arm for passives. The simulation's own damage pipeline
        /// only ever asks the ability for its KIND, and Passive answers that question the same way
        /// Active does: Skill Damage reaches a passive-sourced blow and never a basic.
        ///
        /// The effect list is SHARED rather than copied, because every reader of it is a reader.
        /// </summary>
        public static AbilityDef AsAbility(PassiveDef passive)
        {
            if (passive == null) return null;
            AbilityDef a = new AbilityDef();
            a.Id = passive.Id;
            a.Name = passive.Name;
            a.Description = passive.Description;
            a.Kind = AbilityKind.Passive;
            a.EnergyCost = 0;
            a.Effects = passive.Effects;
            return a;
        }

        /// <summary>Every number one ability prints, in authoring order. Empty for an ability that
        /// only applies statuses, moves turns or strips buffs.</summary>
        public static List<AbilityValue> Values(AbilityDef ability, UnitStats stats, BalanceConfig cfg)
        {
            List<AbilityValue> values = new List<AbilityValue>();
            if (ability == null || stats == null) return values;
            for (int i = 0; i < ability.Effects.Count; i++)
            {
                AbilityValue v = ValueOf(ability.Effects[i], ability.Kind, stats, cfg);
                if (v == null) continue;
                v.EffectIndex = i;
                values.Add(v);
            }
            return values;
        }

        /// <summary>
        /// One effect's number, or null when the effect carries none.
        ///
        /// The arithmetic mirrors CombatSimulation.ApplyDamageEffect and the Heal and Shield arms
        /// of ApplyEffect, with every term that belongs to the TARGET or to the ROLL left out:
        /// Defence mitigation, Block, the aspect factor, the crit roll and the variance spread.
        /// Against a zero-Defence body of the caster's own aspect, with crit and variance off,
        /// every one of those is an exact identity - which is why the harness can prove this EQUAL
        /// to what the simulation deals rather than merely close to it.
        /// </summary>
        public static AbilityValue ValueOf(EffectDef effect, AbilityKind abilityKind, UnitStats stats, BalanceConfig cfg)
        {
            if (!Carries(effect) || stats == null) return null;

            AbilityValue v = new AbilityValue();
            v.Kind = effect.Kind == EffectKind.Damage ? AbilityValueKind.Damage
                   : effect.Kind == EffectKind.Heal ? AbilityValueKind.Heal
                   : AbilityValueKind.Shield;
            v.Percent = effect.Power * 100f;
            v.Conditional = effect.Condition != EffectCondition.None
                         || effect.Condition2 != EffectCondition.None
                         || effect.Condition3 != EffectCondition.None;
            v.Derived = ReplacesTheAttackTerm(effect);
            v.Partial = !v.Derived && DependsOnSomethingUnknown(effect);
            v.Area = effect.Target == TargetMode.AllEnemies
                  || effect.Target == TargetMode.AllAllies
                  || effect.Target == TargetMode.TwoLowestHpAllies
                  || effect.Splash;

            // Hits reads either way, because the count is authored either way. The Hits loop is on
            // the simulation's damage branch alone - a heal or a ward applies once however many
            // hits the effect declares - so this is where that asymmetry is stated.
            v.Hits = effect.Kind == EffectKind.Damage ? Math.Max(0, effect.Hits) : 1;

            // A derived effect's Power multiplies a runtime amount rather than Attack, so there is
            // no flat figure to print and inventing one would be the same lie the bare percentage
            // already tells.
            if (v.Derived) return v;

            // Whether there is a caster-side term at all. Damage never reads SelfHpRatio - only
            // the heal and shield arms of ApplyEffect do - so the question is asked per kind
            // rather than over one list of fields.
            v.HasFlat = effect.Power != 0f || effect.Flat != 0f
                     || (effect.Kind != EffectKind.Damage && effect.SelfHpRatio != 0f);

            float attack = stats.Attack;
            float raw = attack * effect.Power + effect.Flat;

            if (effect.Kind == EffectKind.Damage)
            {
                // "Up to 130% of Attack", applied to the assembled amount and before anything
                // scales it, exactly where ApplyDamageEffect applies it.
                if (effect.AttackCap > 0f)
                {
                    float ceiling = attack * effect.AttackCap;
                    if (raw > ceiling) raw = ceiling;
                }

                // Skill Damage reaches an active and never a basic. The simulation gates it on the
                // ability's kind rather than on the stat being non-zero, and so does this. Zero on
                // every hero in shipped content; armour and relics can roll it.
                if (abilityKind != AbilityKind.BasicAttack)
                {
                    float skill = stats.SkillDamage;
                    if (skill < -0.9f) skill = -0.9f;
                    if (skill != 0f) raw *= 1f + skill;
                }

                // Holy Damage is added AFTER the Defence multiply in the simulation, which against
                // a zero-Defence body is a multiply by one, so it lands here as a plain add.
                raw += stats.HolyDamage;

                v.PerHit = Round(raw);
                float floor = cfg != null ? cfg.MinDamage : 1f;
                if (v.PerHit < floor) v.PerHit = (int)floor;
            }
            else
            {
                // A heal and a ward read the caster's own Max HP as well, which damage never does.
                raw += stats.MaxHp * effect.SelfHpRatio;
                v.PerHit = Round(raw);
                if (v.PerHit < 0) v.PerHit = 0;
            }

            v.Amount = v.PerHit * v.Hits;
            return v;
        }

        /// <summary>
        /// The ScaleBy family: flags that replace the Attack term outright with something only a
        /// fight in progress knows. An effect carrying one of these has no flat value outside one.
        /// </summary>
        public static bool ReplacesTheAttackTerm(EffectDef effect)
        {
            if (effect == null) return false;
            return effect.ScaleByLastRelease
                || effect.ScaleByLastRedirect
                || effect.ScaleByLastRemoval
                || effect.ScaleByLastBanked
                || effect.ScaleByLastMoved
                || effect.ScaleByLastDamage
                || effect.ScaleByLastShift;
        }

        /// <summary>
        /// Terms that ADD to the Attack-scaled amount out of somewhere the card cannot see: a
        /// share of the target's health, a band left behind by an expiring record, or a counter
        /// standing at zero until the fight builds it. The printed number is the rest of the sum,
        /// which makes it a floor rather than a guess.
        /// </summary>
        public static bool DependsOnSomethingUnknown(EffectDef effect)
        {
            if (effect == null) return false;
            return effect.TargetMaxHpRatio != 0f
                || effect.TargetCurrentHpRatio != 0f
                || effect.TargetMissingHpRatio != 0f
                || effect.ScaleByBandGap
                || effect.ScaleByBandHigh
                || effect.PerStack != 0f;
        }

        /// <summary>
        /// The ability's headline: the biggest number it puts on the board, which is what a card
        /// too small for the whole breakdown prints. Damage outranks healing and healing outranks
        /// a ward, because that is the order the roster's own abilities lead with.
        ///
        /// Only a value with a flat figure can be the headline. An effect priced entirely off the
        /// target's health has an Amount of 1 - the minimum-damage floor - and a card that led
        /// with "1 damage" would be worse than a card that led with nothing.
        /// </summary>
        public static AbilityValue Headline(List<AbilityValue> values)
        {
            if (values == null) return null;
            for (int pass = 0; pass < 3; pass++)
            {
                AbilityValueKind want = pass == 0 ? AbilityValueKind.Damage
                                      : pass == 1 ? AbilityValueKind.Heal
                                      : AbilityValueKind.Shield;
                AbilityValue best = null;
                for (int i = 0; i < values.Count; i++)
                {
                    AbilityValue v = values[i];
                    if (v.Kind != want || !v.HasFlat) continue;
                    if (best == null || v.Amount > best.Amount) best = v;
                }
                if (best != null) return best;
            }
            return null;
        }

        private static int Round(float v)
        {
            return (int)Math.Round(v, MidpointRounding.AwayFromZero);
        }
    }
}
