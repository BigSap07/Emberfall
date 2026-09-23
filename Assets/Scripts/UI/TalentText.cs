using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;

namespace Emberfall.UI
{
    /// <summary>
    /// HOW TRUE A PRINTED FIGURE IS OF THE FIGHT, which is the one thing a talent card cannot
    /// leave the reader to guess.
    ///
    /// AbilityMath resolves every damage, heal and ward effect in the roster against a hero's own
    /// stats, and the harness proves the result equal to what CombatSimulation deals. For most
    /// effects that is the whole story. For two kinds of effect it is not, and the harness pins
    /// both by name rather than counting them: six whose amount is a share of a runtime quantity
    /// instead of Attack, and twenty-one priced entirely off the target's health. A card that
    /// printed those as plain numbers would be printing the minimum-damage floor as though it
    /// were the payout.
    ///
    /// So a figure says which of the three it is, and the card says what that means once, under
    /// the list, rather than qualifying every line.
    /// </summary>
    public enum TalentFigureBasis
    {
        /// <summary>A flat figure the fight deals exactly, at these stats, before the target's
        /// Defence and before crit and variance.</summary>
        Exact,

        /// <summary>A flat figure with something the card cannot see added on top: a share of the
        /// target's health, or a counter the fight has not built yet. It is the least it does.
        /// AbilityValue.Partial.</summary>
        Floor,

        /// <summary>No flat figure exists outside a fight, so the clause is the whole of it.
        /// AbilityValue.Derived, or an effect with no caster-side term at all.</summary>
        Clause
    }

    /// <summary>One printed number beside a talent's prose: where it lands, and what it is.</summary>
    public sealed class TalentNumber
    {
        /// <summary>The ability or the moment the number belongs to, in the kit's quiet voice.</summary>
        public string Where = "";

        /// <summary>The figure itself: "2,450 to 2,205 damage" with a guillemet for "to", or
        /// "Cinder for 2 rounds".</summary>
        public string Figure = "";

        /// <summary>How true of the fight this figure is. <see cref="TalentFigureBasis"/>.</summary>
        public TalentFigureBasis Basis = TalentFigureBasis.Exact;

        public Color Tint;
    }

    /// <summary>
    /// HOW A TALENT'S NUMBERS ARE WRITTEN, beside a sentence that by house rule has none.
    ///
    /// Docs/ABILITY-TEXT-STYLE.md keeps durations, thresholds, Energy amounts and status sizes out
    /// of the prose, because a number in a sentence is a second copy that goes stale. That leaves
    /// them nowhere at all unless the screen prints them from the data, and a talent is exactly
    /// where they matter most: "Rubble lasts for the rest of the battle, but Take Down the Wall
    /// deals less damage" is a decision only once it says how much less.
    ///
    /// TalentMath in Core decides WHAT changed and resolves it against the kit; this decides the
    /// words, the same split AbilityMath and AbilityText keep. Nothing here reads a description.
    /// A change is printed as FROM and TO with a guillemet between them: Barlow Condensed has no
    /// arrow, checked against the font's own character map, and it does have the guillemet.
    /// </summary>
    public static class TalentText
    {
        /// <summary>
        /// What marks a description as a note to a designer rather than text for a player - the
        /// markers the harness's CheckAbilityTextIsPlayerFacing matches, restated here because
        /// the harness is not compiled into the game. Thirty-one descriptions in the roster still
        /// carry one (Docs/PINOY-LIST.md 2f), and one talent has no description at all.
        /// </summary>
        private static readonly string[] DraftingNoteMarkers =
        {
            "pinoy", "docs/", ".md", "the engine", "harness", "star band", "measured:"
        };

        /// <summary>What a talent says in place of a sentence it does not have yet. True, and
        /// short, and it sends the reader to the numbers, which are real either way.</summary>
        public const string NoProse = "Its description is still being written. The numbers below are what it does.";

        /// <summary>What "or more" means, said once under a list that contains one rather than on
        /// every line that is one. <see cref="TalentFigureBasis.Floor"/>.</summary>
        public const string FloorNote = "\"or more\": a share of the target's health, or a counter the"
                                      + " fight builds, is added on top. The figure is the least of it.";

        /// <summary>What a clause standing in place of a figure means.
        /// <see cref="TalentFigureBasis.Clause"/>.</summary>
        public const string ClauseNote = "No flat figure outside a fight: the amount is a share of"
                                       + " something the fight settles, so the clause is the whole of it.";

        /// <summary>What a talent with no numbers of its own says where the list would be.</summary>
        public const string NoNumbers = "No numbers of its own. The sentence above is all of it.";

        /// <summary>The talent's player-facing sentence, or <see cref="NoProse"/>.</summary>
        public static string Prose(PassiveDef talent)
        {
            string text = talent != null && talent.Description != null ? talent.Description.Trim() : "";
            if (text.Length == 0) return NoProse;
            string lower = text.ToLowerInvariant();
            for (int i = 0; i < DraftingNoteMarkers.Length; i++)
                if (lower.Contains(DraftingNoteMarkers[i])) return NoProse;
            return text;
        }

        /// <summary>Every number a talent carries, in the order the simulation applies them.</summary>
        public static List<TalentNumber> Numbers(HeroDef hero, PassiveDef talent, UnitStats stats,
                                                 BalanceConfig cfg)
        {
            List<TalentNumber> lines = new List<TalentNumber>();
            int dropped;
            List<TalentChange> changes = TalentMath.Changes(hero, talent, out dropped);
            for (int i = 0; i < changes.Count; i++)
            {
                TalentChange c = changes[i];
                switch (c.Kind)
                {
                    case TalentChangeKind.OwnRule:
                    {
                        string when = FiringLimits(c.OnPassive);
                        if (when.Length > 0) Add(lines, "WHEN IT FIRES", when, Emberforge.TextBody,
                                                 TalentFigureBasis.Exact);
                        for (int e = 0; e < c.OnPassive.Effects.Count; e++)
                            AddEffect(lines, c.OnPassive.Name, c.OnPassive.Effects[e], AbilityKind.Passive, stats, cfg);
                        break;
                    }
                    case TalentChangeKind.EffectField:
                    {
                        TalentFigureBasis basis;
                        string figure = FieldChange(c, stats, cfg, out basis);
                        Add(lines, c.AbilityName, figure, ChangeTint(c), basis);
                        break;
                    }
                    case TalentChangeKind.OwnerField:
                        Add(lines, c.AbilityName, OwnerChange(c), Emberforge.TextBody, TalentFigureBasis.Exact);
                        break;
                    case TalentChangeKind.Append:
                        AddEffect(lines, c.AbilityName + " also", c.Appended, c.AbilityKind, stats, cfg);
                        break;
                }
            }
            return lines;
        }

        /// <summary>True when any figure in a list needs the note that goes with that basis.</summary>
        public static bool Any(List<TalentNumber> lines, TalentFigureBasis basis)
        {
            if (lines == null) return false;
            for (int i = 0; i < lines.Count; i++) if (lines[i].Basis == basis) return true;
            return false;
        }

        // ------------------------------------------------------------------ effects

        private static void AddEffect(List<TalentNumber> lines, string where, EffectDef effect,
                                      AbilityKind kind, UnitStats stats, BalanceConfig cfg)
        {
            Color tint;
            TalentFigureBasis basis;
            string figure = EffectFigure(effect, kind, stats, cfg, out tint, out basis);
            if (figure.Length == 0) return;
            string when = ConditionFigure(effect);
            if (when.Length > 0) figure += ", " + when;
            Add(lines, where, figure, tint, basis);
        }

        /// <summary>
        /// The number one effect carries, or "" for an effect that carries none, together with how
        /// true of the fight that number is.
        ///
        /// THE THREE CASES ARE THE THREE THE HARNESS PINS BY NAME. A derived effect and one priced
        /// entirely off the target's health both print their clause, because AbilityMath has no
        /// honest figure for either and its Amount for the second is the minimum-damage floor. An
        /// effect with a real caster-side term AND a term the card cannot see prints the figure and
        /// says "or more", because that figure is a floor rather than a guess.
        /// </summary>
        public static string EffectFigure(EffectDef e, AbilityKind kind, UnitStats stats, BalanceConfig cfg,
                                          out Color tint, out TalentFigureBasis basis)
        {
            tint = Emberforge.TextBody;
            basis = TalentFigureBasis.Exact;
            if (e == null) return "";
            switch (e.Kind)
            {
                case EffectKind.Damage:
                case EffectKind.Heal:
                case EffectKind.Shield:
                {
                    AbilityValue v = AbilityMath.ValueOf(e, kind, stats, cfg);
                    if (v == null) return "";
                    tint = AbilityText.Tint(v.Kind);
                    string terms = AbilityText.Secondary(v, e);
                    if (!v.HasFlat || v.Derived)
                    {
                        basis = TalentFigureBasis.Clause;
                        return AbilityText.Word(v.Kind) + (terms.Length > 0 ? ": " + terms
                                                                            : ", settled in the fight");
                    }
                    if (v.Partial)
                    {
                        basis = TalentFigureBasis.Floor;
                        return AbilityText.Headline(v) + " or more"
                             + (terms.Length > 0 ? " (" + terms + ")" : "");
                    }
                    return AbilityText.Headline(v) + (terms.Length > 0 ? " (" + terms + ")" : "");
                }
                case EffectKind.ApplyStatus:
                    tint = StatusInfo.IsHarmful(e.Status) ? UITheme.Danger : Emberforge.TextGold;
                    return StatusFigure(e.Status, e.Magnitude) + Rounds(e.Duration)
                         + (e.Chance < 0.999f ? ", " + Pct(e.Chance) + " chance" : "");
                case EffectKind.EnergyGain:
                {
                    tint = UITheme.Energy;
                    int amount = Mathf.RoundToInt(e.Flat + e.Power * 100f);
                    return (amount >= 0 ? "+" : "") + amount + " Energy";
                }
                case EffectKind.GainResolve:
                    tint = Emberforge.TextGold;
                    return "+" + Mathf.Max(1, e.Hits) + " stack" + (e.Hits > 1 ? "s" : "")
                         + ", up to " + Mathf.Max(1, e.MaxStacks);
                case EffectKind.TurnShift:
                    tint = Emberforge.TextGold;
                    return "turn progress moved " + Pct(Mathf.Abs(e.Magnitude));
                case EffectKind.Dispel:
                    return "strips " + Mathf.Max(1, e.Hits) + (e.Hits > 1 ? " buffs" : " buff");
                case EffectKind.Cleanse:
                    return "clears " + Mathf.Max(1, e.Hits) + (e.Hits > 1 ? " debuffs" : " debuff");
                case EffectKind.RemoveStatus:
                    if (e.Status == StatusKind.None) return "";
                    return IsCounter(e.Status) ? "spends its stacks" : "removes " + StatusInfo.DisplayName(e.Status);
            }
            return "";
        }

        /// <summary>A status and its size, where its size is a number a player can use. A Stun
        /// or a Mark is authored with a magnitude the engine never reads, and printing it would
        /// be a number that means nothing.</summary>
        private static string StatusFigure(StatusKind status, float magnitude)
        {
            string name = StatusInfo.DisplayName(status);
            switch (status)
            {
                case StatusKind.Burn:
                    return name + " " + Pct(magnitude) + " of Attack a turn";
                case StatusKind.Regen:
                    return name + " " + Pct(magnitude) + " a turn";
                case StatusKind.AttackUp: case StatusKind.AttackDown:
                case StatusKind.DefenseUp: case StatusKind.DefenseDown:
                case StatusKind.SpeedUp: case StatusKind.Slow:
                case StatusKind.BlockUp: case StatusKind.BlockDown:
                case StatusKind.DamageReductionUp: case StatusKind.DamageReductionDown:
                case StatusKind.EffectHitUp: case StatusKind.EffectHitDown:
                case StatusKind.EffectResistUp: case StatusKind.EffectResistDown:
                case StatusKind.CritDamageUp: case StatusKind.CritDamageDown:
                case StatusKind.SkillDamageUp: case StatusKind.SkillDamageDown:
                case StatusKind.ArmorBreakUp: case StatusKind.ArmorBreakDown:
                    return name + " " + Pct(magnitude);
            }
            return name;
        }

        /// <summary>The one gate on an effect a player can read as a number: an HP line, a stack
        /// count, an Energy level. Every other condition is a rule the sentence already says.</summary>
        private static string ConditionFigure(EffectDef e)
        {
            string a = OneCondition(e.Condition, e.ConditionValue, e.ConditionStatus);
            string b = OneCondition(e.Condition2, e.ConditionValue2, e.ConditionStatus2);
            if (a.Length > 0 && b.Length > 0) return a + " and " + b;
            return a.Length > 0 ? a : b;
        }

        private static string OneCondition(EffectCondition c, float value, StatusKind status)
        {
            switch (c)
            {
                case EffectCondition.HpBelow: return "below " + Pct(value) + " HP";
                case EffectCondition.HpAbove: return "above " + Pct(value) + " HP";
                case EffectCondition.StatusStacksAtLeast:
                    return "at " + Mathf.RoundToInt(value) + "+ " + StackName(status);
                case EffectCondition.EnergyAtLeast: return "at " + Mathf.RoundToInt(value) + "+ Energy";
                case EffectCondition.EnergyBelow: return "under " + Mathf.RoundToInt(value) + " Energy";
            }
            return "";
        }

        /// <summary>How often the talent's own rule may fire, and the HP line that sets it off.</summary>
        private static string FiringLimits(PassiveDef p)
        {
            if (p == null) return "";
            List<string> parts = new List<string>();
            if (p.Trigger == PassiveTrigger.OnHpBelowThreshold || p.Trigger == PassiveTrigger.OnEnemyHpBelowThreshold)
                parts.Add("below " + Pct(p.Threshold) + " HP");
            if (p.Trigger == PassiveTrigger.OnStackThreshold && p.StackThresholdCount > 0)
                parts.Add("at " + p.StackThresholdCount + " stacks");
            if (p.OncePerBattle || p.Limit == PassiveLimit.OncePerBattle) parts.Add("once per battle");
            else if (p.Limit == PassiveLimit.OncePerRound) parts.Add("once each round");
            else if (p.Limit == PassiveLimit.EveryNRounds) parts.Add("every " + p.LimitRounds + " rounds");
            if (p.FiresPerSubject > 0) parts.Add(p.FiresPerSubject + (p.FiresPerSubject == 1 ? " time" : " times") + " per enemy");
            return string.Join(", ", parts.ToArray());
        }

        // ------------------------------------------------------------------ overrides

        private static Color ChangeTint(TalentChange c)
        {
            if (c.Before == null) return Emberforge.TextGold;
            if (c.Before.Kind == EffectKind.Damage) return AbilityText.Tint(AbilityValueKind.Damage);
            if (c.Before.Kind == EffectKind.Heal) return AbilityText.Tint(AbilityValueKind.Heal);
            if (c.Before.Kind == EffectKind.Shield) return AbilityText.Tint(AbilityValueKind.Shield);
            if (c.Before.Kind == EffectKind.EnergyGain) return UITheme.Energy;
            if (c.Before.Kind == EffectKind.ApplyStatus && StatusInfo.IsHarmful(c.Before.Status)) return UITheme.Danger;
            return Emberforge.TextGold;
        }

        /// <summary>One override, as the value it had and the value it has with the talent, and
        /// how true of the fight that pair is.</summary>
        private static string FieldChange(TalentChange c, UnitStats stats, BalanceConfig cfg,
                                          out TalentFigureBasis basis)
        {
            basis = TalentFigureBasis.Exact;
            EffectDef before = c.Before;
            EffectDef after = c.After;
            switch (c.Field)
            {
                case "power":
                case "flat":
                    if (AbilityMath.Carries(before) && stats != null)
                    {
                        AbilityValue a = AbilityMath.ValueOf(before, c.AbilityKind, stats, cfg);
                        AbilityValue b = AbilityMath.ValueOf(after, c.AbilityKind, stats, cfg);
                        // A derived effect's Power multiplies a runtime amount, so the move is a
                        // move in THAT share. "% of Attack" below would name the wrong quantity.
                        if (a != null && a.Derived)
                        {
                            basis = TalentFigureBasis.Clause;
                            return Pct(c.From) + " \u00BB " + Pct(c.To) + " of "
                                 + AbilityText.DerivedSource(before);
                        }
                        if (a != null && b != null && a.HasFlat && b.HasFlat)
                        {
                            bool floor = a.Partial || b.Partial;
                            if (floor) basis = TalentFigureBasis.Floor;
                            return AbilityText.Amount(a.Amount) + " \u00BB " + AbilityText.Amount(b.Amount) + " "
                                 + AbilityText.Word(a.Kind) + (floor ? " or more" : "")
                                 + " (" + Pct(before.Power) + " \u00BB " + Pct(after.Power) + " of Attack)";
                        }
                    }
                    if (before.Kind == EffectKind.EnergyGain)
                        return Mathf.RoundToInt(before.Flat + before.Power * 100f) + " \u00BB "
                             + Mathf.RoundToInt(after.Flat + after.Power * 100f) + " Energy";
                    if (c.Field == "flat") return Num(c.From) + " \u00BB " + Num(c.To) + " flat";
                    return Pct(c.From) + " \u00BB " + Pct(c.To) + " of Attack";
                case "duration":
                    return Thing(before) + (ThingIsPlural(before) ? " last " : " lasts ") + RoundsWord(c.From) + " \u00BB " + RoundsWord(c.To);
                case "magnitude":
                    if (before.Kind == EffectKind.TurnShift) return "turn progress " + Pct(Mathf.Abs(c.From)) + " \u00BB " + Pct(Mathf.Abs(c.To));
                    return Thing(before) + " " + Pct(c.From) + " \u00BB " + Pct(c.To);
                case "maxstacks":
                    return Thing(before) + " up to " + Num(Mathf.Max(1f, c.From)) + " \u00BB " + Num(c.To);
                case "chance":
                    return "chance " + Pct(c.From) + " \u00BB " + Pct(c.To);
                case "selfhpratio":
                    return Pct(c.From) + " \u00BB " + Pct(c.To) + " of Max HP";
                case "targetmaxhpratio":
                    return Pct(c.From) + " \u00BB " + Pct(c.To) + " of the target's Max HP";
                case "targetmissinghpratio":
                    return Pct(c.From) + " \u00BB " + Pct(c.To) + " of the target's missing HP";
                case "storedcap":
                    return "holds up to " + Pct(c.From) + " \u00BB " + Pct(c.To)
                         + (before.StoredCapBasis == "maxHp" ? " of Max HP" : " of Attack");
                case "hits":
                    return HitsChange(before, c.From, c.To);
                case "perstack":
                    return Pct(c.From) + " \u00BB " + Pct(c.To) + " more per stack";
                case "ignoredefense":
                    return "ignores " + Pct(c.From) + " \u00BB " + Pct(c.To) + " of Defence";
                case "conditionvalue":
                    return ConditionValueChange(before.Condition, before.ConditionStatus, c.From, c.To);
                case "conditionvalue3":
                    return ConditionValueChange(before.Condition3, before.ConditionStatus3, c.From, c.To);
            }
            return Num(c.From) + " \u00BB " + Num(c.To);
        }

        private static string ConditionValueChange(EffectCondition condition, StatusKind status, float from, float to)
        {
            switch (condition)
            {
                case EffectCondition.HpBelow:
                    // A negative line is how content says "never" - no body is ever below -100% -
                    // so a talent that moves it up from there is ADDING the clause, not moving it.
                    if (from < 0f) return "now also sets in below " + Pct(to) + " HP";
                    return "sets in below " + Pct(from) + " \u00BB " + Pct(to) + " HP";
                case EffectCondition.HpAbove: return "sets in above " + Pct(from) + " \u00BB " + Pct(to) + " HP";
                case EffectCondition.StatusStacksAtLeast:
                    return "needs " + Num(from) + " \u00BB " + Num(to) + " " + StackName(status);
                case EffectCondition.EnergyAtLeast: return "needs " + Num(from) + " \u00BB " + Num(to) + " Energy";
                case EffectCondition.EnergyBelow: return "under " + Num(from) + " \u00BB " + Num(to) + " Energy";
            }
            return "threshold " + Num(from) + " \u00BB " + Num(to);
        }

        private static string OwnerChange(TalentChange c)
        {
            switch (c.Field)
            {
                case "energycost": return "costs " + Num(c.From) + " \u00BB " + Num(c.To) + " Energy";
                case "threshold": return "fires below " + Pct(c.From) + " \u00BB " + Pct(c.To) + " HP";
                case "firespersubject":
                    return (c.From <= 0f ? "no per-enemy limit" : Num(c.From) + " per enemy") + " \u00BB "
                         + (c.To <= 0f ? "no per-enemy limit" : Num(c.To) + " per enemy");
                case "limitoff":
                    return LimitWord((PassiveLimit)(int)c.From) + " \u00BB no limit";
            }
            return Num(c.From) + " \u00BB " + Num(c.To);
        }

        private static string LimitWord(PassiveLimit limit)
        {
            switch (limit)
            {
                case PassiveLimit.OncePerBattle: return "once per battle";
                case PassiveLimit.OncePerRound: return "once each round";
                case PassiveLimit.EveryNRounds: return "every few rounds";
            }
            return "no limit";
        }

        /// <summary>What an effect's duration or size belongs to, in the battle's own words.</summary>
        /// <summary>What a Hits override means depends on what is being counted: blows on a
        /// strike, bodies' worth of statuses on a removal or a move.</summary>
        private static string HitsChange(EffectDef e, float from, float to)
        {
            string span = Num(from) + " \u00BB " + Num(to);
            switch (e.Kind)
            {
                case EffectKind.Damage: return span + " hits";
                case EffectKind.RemoveStatus:
                case EffectKind.Dispel:
                case EffectKind.Cleanse:
                    return "removes up to " + span;
                case EffectKind.MoveBuffs:
                case EffectKind.MoveDebuffs:
                case EffectKind.TransferStatus:
                    return "moves up to " + span;
            }
            return span + " times";
        }

        /// <summary>
        /// The engine's counters are Resolve and Resolve II, and no hero calls them that: Talren's
        /// are Route, Kael's Rubble, Isolde's Traces. The hero's own word is in the sentence
        /// above and nowhere in the data, so the number says "stacks" rather than print an
        /// engine name that contradicts the sentence it sits under.
        /// </summary>
        private static bool IsCounter(StatusKind status)
        {
            return status == StatusKind.Resolve || status == StatusKind.Resolve2;
        }

        private static string StackName(StatusKind status)
        {
            return status == StatusKind.None || IsCounter(status) ? "stacks" : StatusInfo.DisplayName(status);
        }

        private static bool ThingIsPlural(EffectDef e)
        {
            return e != null && e.Kind == EffectKind.GainResolve;
        }

        private static string Thing(EffectDef e)
        {
            if (e == null) return "it";
            if (e.Kind == EffectKind.ApplyStatus && e.Status != StatusKind.None) return StatusInfo.DisplayName(e.Status);
            if (e.Kind == EffectKind.GainResolve) return "its stacks";
            if (e.Kind == EffectKind.Shield) return "the Ward";
            return "it";
        }

        // ------------------------------------------------------------------ formatting

        /// <summary>A duration of this many rounds or more is the whole fight: nothing in the
        /// roster lasts longer than a handful of rounds on purpose, and "99 rounds" would read as
        /// a number to plan around rather than as "for good".</summary>
        private const int ForGood = 50;

        private static string Rounds(int duration)
        {
            if (duration <= 0) return "";
            return duration >= ForGood ? " for the rest of the battle" : " for " + RoundsWord(duration);
        }

        private static string RoundsWord(float rounds)
        {
            int n = Mathf.RoundToInt(rounds);
            if (n >= ForGood) return "the rest of the battle";
            return n + (n == 1 ? " round" : " rounds");
        }

        private static string Pct(float fraction)
        {
            float percent = fraction * 100f;
            float rounded = Mathf.Round(percent);
            if (Mathf.Abs(percent - rounded) < 0.05f)
                return Mathf.RoundToInt(rounded).ToString(CultureInfo.InvariantCulture) + "%";
            return percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        private static string Num(float value)
        {
            float rounded = Mathf.Round(value);
            if (Mathf.Abs(value - rounded) < 0.005f) return Mathf.RoundToInt(rounded).ToString(CultureInfo.InvariantCulture);
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        /// <summary>Adds a line, folding an identical line straight after the one before it into
        /// a count: Oris's talent raises two strikes of one ability by the same amount, and two
        /// identical rows read as a mistake rather than as "both".</summary>
        private static void Add(List<TalentNumber> lines, string where, string figure, Color tint,
                                TalentFigureBasis basis)
        {
            if (string.IsNullOrEmpty(figure)) return;
            if (lines.Count > 0)
            {
                TalentNumber last = lines[lines.Count - 1];
                string bare = last.Figure;
                int times = 1;
                int mark = bare.LastIndexOf("  x");
                if (mark > 0 && int.TryParse(bare.Substring(mark + 3), out times)) bare = bare.Substring(0, mark);
                else times = 1;
                if (last.Where == where && bare == figure)
                {
                    last.Figure = figure + "  x" + (times + 1);
                    return;
                }
            }
            TalentNumber n = new TalentNumber();
            n.Where = where;
            n.Figure = figure;
            n.Tint = tint;
            n.Basis = basis;
            lines.Add(n);
        }
    }
}
