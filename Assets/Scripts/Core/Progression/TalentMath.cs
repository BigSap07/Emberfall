using System;
using System.Collections.Generic;
using Emberfall.Core.Data;

namespace Emberfall.Core.Progression
{
    public enum TalentChangeKind
    {
        /// <summary>One numeric field of one effect on a named ability or passive moves.</summary>
        EffectField,

        /// <summary>A number that belongs to the ability or passive rather than to an effect:
        /// Energy cost, a passive's HP threshold, its firing budget, or its limit switched off.</summary>
        OwnerField,

        /// <summary>A whole effect is added to a named ability or passive.</summary>
        Append,

        /// <summary>The talent's own trigger and effects: it fires like any passive.</summary>
        OwnRule
    }

    /// <summary>
    /// One thing a talent does to its hero's kit, resolved against the kit so it can be printed
    /// as a before and an after rather than as a delta on an index.
    /// </summary>
    public sealed class TalentChange
    {
        public TalentChangeKind Kind;

        /// <summary>The ability's display name, or the talent's own for an OwnRule.</summary>
        public string AbilityName = "";
        public AbilityKind AbilityKind = AbilityKind.Passive;

        /// <summary>Lower-case, as TalentOverrideDef.Field is matched.</summary>
        public string Field = "";
        public float From;
        public float To;

        /// <summary>The effect before and after this change, for an EffectField. After has every
        /// earlier change the same talent makes to the same effect already applied, so two
        /// overrides on one effect read as a chain rather than as two changes from one start.</summary>
        public EffectDef Before;
        public EffectDef After;

        /// <summary>The appended effect, for an Append.</summary>
        public EffectDef Appended;

        /// <summary>The passive a change lands on, when it lands on a passive.</summary>
        public PassiveDef OnPassive;
    }

    /// <summary>
    /// WHAT A TALENT CHANGES, as data a screen can print.
    ///
    /// A talent authored as overrides is a list of (ability key, effect index, field, delta), and
    /// a delta on an index is not a number a player can read: "+97 on effect 2 of active" is Kael's
    /// Rubble lasting the rest of the battle. This resolves each entry against the hero's own kit
    /// and hands back the value before and after, the way AbilityMath hands back a flat figure
    /// rather than a multiplier.
    ///
    /// IT APPLIES DELTAS THE WAY THE ENGINE DOES, BECAUSE IT IS THE ENGINE'S CODE. TryApplyDelta
    /// below is the switch CombatSimulation.ApplyDelta used to hold, moved here and called from
    /// there, so a field the simulation ignores is a field this reports as not applied. A screen
    /// that printed a change the fight never makes would be exactly the lie the prose rule was
    /// written to stop.
    /// </summary>
    public static class TalentMath
    {
        /// <summary>
        /// Adds a talent override's delta to one field of one effect, and returns false for a
        /// field the engine does not change. The single definition of that list.
        /// </summary>
        public static bool TryApplyDelta(EffectDef e, string field, float delta)
        {
            if (e == null || field == null) return false;
            switch (field.ToLowerInvariant())
            {
                case "power":      e.Power += delta; return true;
                case "magnitude":  e.Magnitude += delta; return true;
                case "maxstacks":  e.MaxStacks += (int)Math.Round(delta, MidpointRounding.AwayFromZero); return true;
                case "duration":   e.Duration += (int)Math.Round(delta, MidpointRounding.AwayFromZero); return true;
                case "flat":       e.Flat += delta; return true;
                case "chance":     e.Chance += delta; return true;
                case "selfhpratio": e.SelfHpRatio += delta; return true;
                case "targetmaxhpratio": e.TargetMaxHpRatio += delta; return true;
                case "targetmissinghpratio": e.TargetMissingHpRatio += delta; return true;
                case "storedcap":  e.StoredCap += delta; return true;
                case "hits":       e.Hits += (int)Math.Round(delta, MidpointRounding.AwayFromZero); return true;
                case "perstack":   e.PerStack += delta; return true;
                case "conditionvalue": e.ConditionValue += delta; return true;
                case "conditionvalue3": e.ConditionValue3 += delta; return true;
                case "ignoredefense": e.IgnoreDefense += delta; return true;
            }
            return false;
        }

        /// <summary>The current value of a field TryApplyDelta can move, so a change can be
        /// printed as from and to.</summary>
        public static float FieldValue(EffectDef e, string field)
        {
            if (e == null || field == null) return 0f;
            switch (field.ToLowerInvariant())
            {
                case "power":      return e.Power;
                case "magnitude":  return e.Magnitude;
                case "maxstacks":  return e.MaxStacks;
                case "duration":   return e.Duration;
                case "flat":       return e.Flat;
                case "chance":     return e.Chance;
                case "selfhpratio": return e.SelfHpRatio;
                case "targetmaxhpratio": return e.TargetMaxHpRatio;
                case "targetmissinghpratio": return e.TargetMissingHpRatio;
                case "storedcap":  return e.StoredCap;
                case "hits":       return e.Hits;
                case "perstack":   return e.PerStack;
                case "conditionvalue": return e.ConditionValue;
                case "conditionvalue3": return e.ConditionValue3;
                case "ignoredefense": return e.IgnoreDefense;
            }
            return 0f;
        }

        /// <summary>
        /// The passive a talent names BY ITS JSON KEY, the same resolution
        /// CombatSimulation.PassiveSlotIndex makes: the flat key first, then "skills." + key.
        /// </summary>
        public static PassiveDef FindPassive(HeroDef hero, string key)
        {
            if (hero == null || string.IsNullOrEmpty(key)) return null;
            for (int i = 0; i < hero.Passives.Count; i++)
                if (hero.Passives[i].SlotKey == key) return hero.Passives[i];
            if (!key.StartsWith("passive") || key.StartsWith("passives.")) return null;
            string nested = "skills." + key;
            for (int i = 0; i < hero.Passives.Count; i++)
                if (hero.Passives[i].SlotKey == nested) return hero.Passives[i];
            return null;
        }

        /// <summary>
        /// Every change a talent makes, in the order the simulation applies them: its own rule if
        /// it has one, then its overrides, then its appends - CombatSimulation.BuildUnits calls
        /// AddPassive, ApplyTalentOverrides and ApplyTalentAppends in that order.
        ///
        /// An override the engine drops - a slot the kit does not have, an index past the end, a
        /// field it does not move - is left out rather than printed, because it changes nothing.
        /// The out count says how many there were, so a caller can know one existed.
        /// </summary>
        public static List<TalentChange> Changes(HeroDef hero, PassiveDef talent, out int dropped)
        {
            List<TalentChange> changes = new List<TalentChange>();
            dropped = 0;
            if (hero == null || talent == null) return changes;

            if (talent.Trigger != PassiveTrigger.None && talent.Effects.Count > 0)
            {
                TalentChange own = new TalentChange();
                own.Kind = TalentChangeKind.OwnRule;
                own.AbilityName = talent.Name;
                own.AbilityKind = AbilityKind.Passive;
                own.OnPassive = talent;
                changes.Add(own);
            }

            // Working copies, keyed by slot and effect, so a second override on the same effect
            // starts where the first one left it.
            Dictionary<string, EffectDef> working = new Dictionary<string, EffectDef>();

            for (int i = 0; i < talent.Overrides.Count; i++)
            {
                TalentOverrideDef ov = talent.Overrides[i];
                if (string.IsNullOrEmpty(ov.Ability) || string.IsNullOrEmpty(ov.Field)) { dropped++; continue; }
                string slot = ov.Ability.ToLowerInvariant();
                string field = ov.Field.ToLowerInvariant();

                List<EffectDef> effects;
                string name;
                AbilityKind kind;
                PassiveDef passive = null;
                if (!Resolve(hero, slot, out effects, out name, out kind, out passive)) { dropped++; continue; }

                if (slot == "basic" || slot == "active")
                {
                    if (field == "energycost")
                    {
                        AbilityDef ability = slot == "basic" ? hero.BasicAttack : hero.Active;
                        TalentChange c = new TalentChange();
                        c.Kind = TalentChangeKind.OwnerField;
                        c.AbilityName = name;
                        c.AbilityKind = kind;
                        c.Field = field;
                        c.From = ability.EnergyCost;
                        c.To = ability.EnergyCost + (int)Math.Round(ov.Delta, MidpointRounding.AwayFromZero);
                        changes.Add(c);
                        continue;
                    }
                }
                else if (field == "threshold" || field == "firespersubject" || field == "limitoff")
                {
                    TalentChange c = new TalentChange();
                    c.Kind = TalentChangeKind.OwnerField;
                    c.AbilityName = name;
                    c.AbilityKind = kind;
                    c.Field = field;
                    c.OnPassive = passive;
                    if (field == "threshold") { c.From = passive.Threshold; c.To = passive.Threshold + ov.Delta; }
                    else if (field == "firespersubject")
                    {
                        c.From = passive.FiresPerSubject;
                        c.To = Math.Max(0, passive.FiresPerSubject + (int)Math.Round(ov.Delta, MidpointRounding.AwayFromZero));
                    }
                    else
                    {
                        if (ov.Delta <= 0f) { dropped++; continue; }
                        c.From = (float)passive.Limit;
                        c.To = (float)PassiveLimit.Unlimited;
                    }
                    changes.Add(c);
                    continue;
                }

                if (ov.EffectIndex < 0 || ov.EffectIndex >= effects.Count) { dropped++; continue; }
                string key = slot + "#" + ov.EffectIndex;
                EffectDef current;
                if (!working.TryGetValue(key, out current)) current = effects[ov.EffectIndex].Clone();
                EffectDef next = current.Clone();
                if (!TryApplyDelta(next, field, ov.Delta)) { dropped++; continue; }
                working[key] = next;

                TalentChange change = new TalentChange();
                change.Kind = TalentChangeKind.EffectField;
                change.AbilityName = name;
                change.AbilityKind = kind;
                change.Field = field;
                change.Before = current;
                change.After = next;
                change.From = FieldValue(current, field);
                change.To = FieldValue(next, field);
                change.OnPassive = passive;
                changes.Add(change);
            }

            for (int i = 0; i < talent.Appends.Count; i++)
            {
                TalentAppendDef ap = talent.Appends[i];
                if (ap == null || ap.Effect == null || string.IsNullOrEmpty(ap.Ability)) { dropped++; continue; }
                List<EffectDef> effects;
                string name;
                AbilityKind kind;
                PassiveDef passive;
                if (!Resolve(hero, ap.Ability.ToLowerInvariant(), out effects, out name, out kind, out passive))
                { dropped++; continue; }
                TalentChange c = new TalentChange();
                c.Kind = TalentChangeKind.Append;
                c.AbilityName = name;
                c.AbilityKind = kind;
                c.Appended = ap.Effect;
                c.OnPassive = passive;
                changes.Add(c);
            }
            return changes;
        }

        private static bool Resolve(HeroDef hero, string slot, out List<EffectDef> effects, out string name,
                                    out AbilityKind kind, out PassiveDef passive)
        {
            effects = null;
            name = "";
            kind = AbilityKind.Passive;
            passive = null;
            if (slot == "basic" || slot == "active")
            {
                AbilityDef ability = slot == "basic" ? hero.BasicAttack : hero.Active;
                if (ability == null) return false;
                effects = ability.Effects;
                name = ability.Name;
                kind = ability.Kind;
                return true;
            }
            passive = FindPassive(hero, slot);
            if (passive == null) return false;
            effects = passive.Effects;
            name = passive.Name;
            return true;
        }
    }
}
