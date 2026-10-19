using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;

namespace Emberfall.UI
{
    /// <summary>
    /// How an ability's numbers are WRITTEN, once, for every surface that writes them.
    ///
    /// AbilityMath in Core decides what the numbers are; this decides the sentence. The split is
    /// the same one every other pair in this project keeps: the arithmetic is engine-free and
    /// provable by the harness, the phrasing is UI and provable only by looking at it.
    ///
    /// THE MAIN LINE IS A FLAT VALUE and the authored percentage is the smaller line beneath it -
    /// Docs/DESIGN-DAMAGE-DISPLAY.md section 4, ship B. The percentage is kept rather than
    /// dropped because it is the only form in which a player can reason about a relic or an
    /// Attack buff; it is demoted because on its own it makes a 1-star read as four times a
    /// 5-star.
    ///
    /// NOTHING HERE READS AN ABILITY'S DESCRIPTION. Every figure comes from the effect data, so a
    /// prose number that disagrees with the effect beneath it cannot reach a card. The sentence on
    /// the ability card comes from AbilityTextConfig.Prose, which never returns a drafting note:
    /// a good half of the roster's authored descriptions once carried notes addressed to a
    /// designer, and ability-text.json stands in for those until the owner answers them.
    /// </summary>
    public static class AbilityText
    {
        /// <summary>The one caveat the whole row carries, said once rather than on every line.
        /// Defence, crit and variance are properties of a HIT; these are numbers about an
        /// ABILITY, and two of the three belong to whoever is being hit.</summary>
        public const string Caveat = "Before the target's Defence, and before crit and variance.";

        /// <summary>The prompt under the ability row: the cards hold one number each, and a tap opens
        /// the sentence, the facts and every number (AbilityCardSheet).</summary>
        public const string TapHint = "Tap an ability for what it does.";

        /// <summary>The same sentence for a hero nobody owns yet, where the stat line the numbers
        /// are built from is a level-1 hero with no gear rather than one of the player's.</summary>
        public const string CaveatLocked = "At level 1 with no gear, before the target's Defence,"
                                         + " and before crit and variance.";

        /// <summary>What an ability with no damage, healing or ward of its own says instead of a
        /// number. A third of the roster's passives are control or utility and have none.</summary>
        public const string NoNumbers = "This one deals no damage, healing or ward of its own.";

        /// <summary>"2,231". Grouped, because these run to five digits at the level cap and an
        /// ungrouped five-digit number is the one a player misreads.</summary>
        public static string Amount(int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>What the number IS, in a word a player already has.</summary>
        public static string Word(AbilityValueKind kind)
        {
            if (kind == AbilityValueKind.Heal) return "healing";
            if (kind == AbilityValueKind.Shield) return "ward";
            return "damage";
        }

        /// <summary>
        /// The tint the figure carries. Gold for damage is the kit's own value colour - the same
        /// gold POWER is printed in - and the other two borrow the colours their bars already
        /// use in battle, so a green number and a green bar mean the same thing.
        /// </summary>
        public static Color Tint(AbilityValueKind kind)
        {
            if (kind == AbilityValueKind.Heal) return UITheme.HealthAlly;
            if (kind == AbilityValueKind.Shield) return UITheme.Shield;
            return Emberforge.TextGold;
        }

        /// <summary>
        /// What leads a row: the flat figure, or for an effect priced off the target's health the most
        /// it can deal ("up to 1,454 damage"), or the bare word when it has neither. The cap is
        /// AbilityValue.CapAmount, proved against the simulation.
        /// </summary>
        public static string Figure(AbilityValue value, bool shortForm)
        {
            if (value == null) return "";
            if (value.HasFlat) return shortForm ? Headline(value) : Headline(value);
            if (value.CapAmount > 0) return "up to " + Amount(value.CapAmount) + (shortForm ? "" : " " + Word(value.Kind));
            return Word(value.Kind);
        }

        /// <summary>The main line: "2,231 damage".</summary>
        public static string Headline(AbilityValue value)
        {
            if (value == null) return "";
            return Amount(value.Amount) + " " + Word(value.Kind);
        }

        /// <summary>
        /// The smaller line under it: what the ability is AUTHORED at, assembled from whichever
        /// terms the effect actually carries rather than assumed to be one percentage of Attack.
        /// Six effects in the roster are priced off the caster's Max HP and nine off the target's
        /// health, and a line that said "0% of Attack" for those would be worse than no line.
        /// </summary>
        public static string Secondary(AbilityValue value, EffectDef effect)
        {
            if (value == null || effect == null) return "";
            List<string> parts = new List<string>();

            if (value.Derived)
            {
                parts.Add(Pct(value.Percent) + " of " + DerivedSource(effect));
            }
            else
            {
                if (effect.Power != 0f) parts.Add(Pct(value.Percent) + " of Attack");
                if (effect.Flat != 0f) parts.Add(Amount(Mathf.RoundToInt(effect.Flat)) + " flat");
                if (effect.Kind != EffectKind.Damage && effect.SelfHpRatio != 0f)
                    parts.Add(Pct(effect.SelfHpRatio * 100f) + " of Max HP");
                if (effect.TargetMaxHpRatio != 0f)
                    parts.Add(Pct(effect.TargetMaxHpRatio * 100f) + " of the target's Max HP");
                if (effect.TargetCurrentHpRatio != 0f)
                    parts.Add(Pct(effect.TargetCurrentHpRatio * 100f) + " of the target's current HP");
                if (effect.TargetMissingHpRatio != 0f)
                    parts.Add(Pct(effect.TargetMissingHpRatio * 100f) + " of the target's missing HP");
                if (effect.ScaleByBandGap) parts.Add("the band its record left");
                if (effect.ScaleByBandHigh) parts.Add("the highest its record held");
                if (effect.PerStack != 0f) parts.Add(Pct(effect.PerStack * 100f) + " more per stack");
            }

            if (parts.Count == 0) return "";
            string line = Join(parts, " + ");
            if (effect.AttackCap > 0f) line += ", up to " + Pct(effect.AttackCap * 100f) + " of Attack";
            if (value.Hits != 1) line += ", " + value.Hits + " hits";
            return line;
        }

        /// <summary>
        /// The two riders that change what a number MEANS without changing its size: that it
        /// lands on more than one body, and that it lands only when a clause holds.
        ///
        /// Separate from <see cref="Secondary"/> because only the breakdown prints them. A card
        /// is about 150 units wide and its authored-terms line is already the longest thing on
        /// it; "210% of Attack, each target" does not fit and a clipped rider is worse than one
        /// that is a tap away. The note under the row is what tells a reader the cards are
        /// summaries of something larger.
        /// </summary>
        public static string Riders(AbilityValue value)
        {
            if (value == null) return "";
            if (value.Area && value.Conditional) return "each target, on a condition";
            if (value.Area) return "each target";
            if (value.Conditional) return "on a condition";
            return "";
        }

        /// <summary>Where a ScaleBy effect takes its amount from, in the player's words. Six
        /// effects in the roster reach this, and all six are counted and named by
        /// CheckAbilityNumbersMatchTheSimulation - so a seventh arriving without a phrase here is
        /// a failing check rather than an empty line.
        ///
        /// Public because the talent card needs it too: a talent that moves a derived effect's
        /// Power moves a share of THIS, and "% of Attack" would name the wrong quantity.</summary>
        public static string DerivedSource(EffectDef effect)
        {
            if (effect == null) return "what came before it";
            if (effect.ScaleByLastRelease) return "what its ledger last paid out";
            if (effect.ScaleByLastBanked) return "what its ledger last banked";
            if (effect.ScaleByLastRedirect) return "what it last redirected";
            if (effect.ScaleByLastRemoval) return "what the last removal took";
            if (effect.ScaleByLastMoved) return "what it last moved";
            if (effect.ScaleByLastDamage) return "the last damage dealt";
            if (effect.ScaleByLastShift) return "the turn progress it last moved";
            return "what came before it";
        }

        /// <summary>A percentage with no decimal unless the authored value has one. Every power in
        /// the roster but Isara's is a whole percentage, and printing "437.0%" for the other 228
        /// to keep her company would be the wrong trade.</summary>
        private static string Pct(float percent)
        {
            float rounded = Mathf.Round(percent);
            if (Mathf.Abs(percent - rounded) < 0.05f)
                return Mathf.RoundToInt(rounded).ToString(CultureInfo.InvariantCulture) + "%";
            return percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        private static string Join(List<string> parts, string sep)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0) sb.Append(sep);
                sb.Append(parts[i]);
            }
            return sb.ToString();
        }
    }
}
