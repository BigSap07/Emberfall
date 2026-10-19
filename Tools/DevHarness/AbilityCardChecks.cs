using System;
using System.Collections.Generic;
using System.IO;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE ABILITY CARD (R17 task 1): what the hero page and the paused battle card print besides
    /// damage, healing and ward numbers, proved against the simulation.
    ///
    /// Two named checks. CheckAbilityCardTextIsComplete proves every hero has a sentence for every
    /// basic, active and passive it owns, and that the overlay file standing in for the drafting
    /// notes is player text. CheckAbilityFactsMatchTheSimulation proves every duration, Energy
    /// cost, Energy grant and status size the card prints equal to what CombatSimulation does with
    /// the same effect. The damage, healing and ward numbers stay with
    /// CheckAbilityNumbersMatchTheSimulation.
    /// </summary>
    internal static partial class Program
    {
        private static ContentDatabase LoadAbilityTextOverlay(ContentDatabase live, string contentDir)
        {
            string path = contentDir == null ? null : Path.Combine(contentDir, "ability-text.json");
            string text = path != null && File.Exists(path) ? File.ReadAllText(path) : "";
            live.LoadAbilityTexts(text);
            return live;
        }

        private static void CheckAbilityCardTextIsComplete(ContentDatabase live, string contentDir)
        {
            Line("ABILITY CARD TEXT");
            LoadAbilityTextOverlay(live, contentDir);
            AbilityTextConfig overlay = live.AbilityTexts;

            Check(overlay.Warnings.Count == 0,
                  overlay.Warnings.Count == 0 ? "ability-text.json parses without a warning"
                  : "ability-text.json warns: " + string.Join("; ", overlay.Warnings.ToArray()));

            List<string> empty = new List<string>(), notes = new List<string>(), numbers = new List<string>();
            List<string> shadowing = new List<string>(), unused = new List<string>(), dashes = new List<string>();
            HashSet<string> needed = new HashSet<string>();
            HashSet<string> everyId = new HashSet<string>();
            int total = 0;

            for (int h = 0; h < live.Heroes.Count; h++)
            {
                HeroDef def = live.Heroes[h];
                if (def.BasicAttack != null && def.BasicAttack.Id.Contains("placeholder")) continue;
                List<string> ids = new List<string>(), authored = new List<string>();
                if (def.BasicAttack != null) { ids.Add(def.BasicAttack.Id); authored.Add(def.BasicAttack.Description); }
                if (def.Active != null) { ids.Add(def.Active.Id); authored.Add(def.Active.Description); }
                for (int i = 0; i < def.Passives.Count; i++) { ids.Add(def.Passives[i].Id); authored.Add(def.Passives[i].Description); }
                for (int i = 0; i < ids.Count; i++)
                {
                    total++;
                    everyId.Add(ids[i]);
                    string prose = overlay.Prose(ids[i], authored[i]);
                    string who = def.Id + "/" + ids[i];
                    if (prose.Length == 0) { empty.Add(who); continue; }
                    if (AbilityTextConfig.IsDraftingNote(authored[i] ?? "")) needed.Add(ids[i]);
                    else if (overlay.Lines.ContainsKey(ids[i])) shadowing.Add(who);
                    if (AbilityTextConfig.IsDraftingNote(prose)) notes.Add(who);
                    for (int c = 0; c < prose.Length; c++)
                        if (char.IsDigit(prose[c]) || prose[c] == '%') { numbers.Add(who); break; }
                    if (prose.IndexOf((char)0x2014) >= 0 || prose.IndexOf((char)0x2013) >= 0) dashes.Add(who);
                }
            }
            foreach (string id in overlay.Lines.Keys) if (!needed.Contains(id)) unused.Add(id);

            Check(empty.Count == 0,
                  empty.Count == 0
                    ? "every basic, active and passive on all " + live.Heroes.Count + " heroes has card text ("
                      + total + " abilities; " + needed.Count + " of them stand in for a drafting note)"
                    : empty.Count + " ability card(s) would be blank: " + string.Join(", ", empty.ToArray()));
            Check(notes.Count == 0, notes.Count == 0 ? "no card text is a drafting note"
                  : "card text that is a drafting note: " + string.Join(", ", notes.ToArray()));
            Check(numbers.Count == 0 && dashes.Count == 0,
                  numbers.Count == 0 && dashes.Count == 0
                    ? "no card sentence quotes a number or carries a dash (the numbers are the facts and the number line)"
                    : "card sentence(s) with a number or a dash: " + string.Join(", ", numbers.ToArray())
                      + " " + string.Join(", ", dashes.ToArray()));
            Check(shadowing.Count == 0 && unused.Count == 0,
                  shadowing.Count == 0 && unused.Count == 0
                    ? "the overlay holds exactly the " + overlay.Lines.Count
                      + " ability ids whose authored description is a drafting note, and no more"
                    : "overlay line(s) that shadow a clean description: " + string.Join(", ", shadowing.ToArray())
                      + "; overlay line(s) for an id with no drafting note: " + string.Join(", ", unused.ToArray()));

            // Forty words at most (ABILITY-TEXT-STYLE), so a card never has to scroll for prose.
            List<string> long_ = new List<string>();
            foreach (KeyValuePair<string, string> pair in overlay.Lines)
                if (pair.Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length > 45) long_.Add(pair.Key);
            Check(long_.Count == 0, long_.Count == 0 ? "every overlay sentence is within the style guide's length"
                  : "overlay sentence(s) over length: " + string.Join(", ", long_.ToArray()));
            Line("");
        }

        /// <summary>The raw stat each additive-status probe starts from, so a Down can never clamp
        /// at zero and an Up can never reach a cap: the readback measures the status, not a floor.</summary>
        private static CombatUnit ReadBackUnit(HeroDef def)
        {
            UnitStats stats = new UnitStats();
            stats.MaxHp = 10000f; stats.Attack = 1000f; stats.Defense = 1000f; stats.Speed = 1000f;
            stats.CritChance = 0.3f; stats.CritDamage = 1.5f;
            CombatUnit u = CombatUnit.FromDef(def, stats, 40, true, 0, 1, 100);
            u.EffectHit = 0.5f; u.EffectResist = 0.5f; u.SkillDamage = 0.5f; u.Block = 0.5f;
            u.ArmorBreak = 0.5f; u.DamageReduction = 0.5f;
            return u;
        }

        /// <summary>How much one status moves the stat the card says it moves, measured through the
        /// same Effective* getter the simulation reads, as a fraction. NaN for a kind with no
        /// getter, which the card does not quote.</summary>
        private static float StatusReadBack(HeroDef def, StatusKind kind, float stored)
        {
            CombatUnit u = ReadBackUnit(def);
            float before, after;
            switch (kind)
            {
                case StatusKind.AttackUp: case StatusKind.AttackDown:
                    before = u.EffectiveAttack; u.AddOrRefresh(kind, 2, stored, 99); after = u.EffectiveAttack;
                    return kind == StatusKind.AttackUp ? after / before - 1f : 1f - after / before;
                case StatusKind.DefenseUp: case StatusKind.DefenseDown:
                    before = u.EffectiveDefense; u.AddOrRefresh(kind, 2, stored, 99); after = u.EffectiveDefense;
                    return kind == StatusKind.DefenseUp ? after / before - 1f : 1f - after / before;
                case StatusKind.SpeedUp: case StatusKind.Slow:
                    before = u.EffectiveSpeed; u.AddOrRefresh(kind, 2, stored, 99); after = u.EffectiveSpeed;
                    return kind == StatusKind.SpeedUp ? after / before - 1f : 1f - after / before;
                case StatusKind.MaxHpUp:
                    before = u.EffectiveMaxHp; u.AddOrRefresh(kind, 2, stored, 99); after = u.EffectiveMaxHp;
                    return after / before - 1f;
                case StatusKind.Focus:
                    before = u.EffectiveCritChance; u.AddOrRefresh(kind, 2, stored, 99); return u.EffectiveCritChance - before;
                case StatusKind.DamageReductionUp: case StatusKind.DamageReductionDown:
                    before = u.EffectiveDamageReduction; u.AddOrRefresh(kind, 2, stored, 99); after = u.EffectiveDamageReduction;
                    return kind == StatusKind.DamageReductionUp ? after - before : before - after;
                case StatusKind.BlockUp: case StatusKind.BlockDown:
                    before = u.EffectiveBlock; u.AddOrRefresh(kind, 2, stored, 99); after = u.EffectiveBlock;
                    return kind == StatusKind.BlockUp ? after - before : before - after;
                case StatusKind.EffectHitUp: case StatusKind.EffectHitDown:
                    before = u.EffectiveEffectHit; u.AddOrRefresh(kind, 2, stored, 99); after = u.EffectiveEffectHit;
                    return kind == StatusKind.EffectHitUp ? after - before : before - after;
                case StatusKind.EffectResistUp: case StatusKind.EffectResistDown:
                    before = u.EffectiveEffectResist; u.AddOrRefresh(kind, 2, stored, 99); after = u.EffectiveEffectResist;
                    return kind == StatusKind.EffectResistUp ? after - before : before - after;
                case StatusKind.CritDamageUp: case StatusKind.CritDamageDown:
                    before = u.EffectiveCritDamage; u.AddOrRefresh(kind, 2, stored, 99); after = u.EffectiveCritDamage;
                    return kind == StatusKind.CritDamageUp ? after - before : before - after;
                case StatusKind.SkillDamageUp: case StatusKind.SkillDamageDown:
                    before = u.EffectiveSkillDamage; u.AddOrRefresh(kind, 2, stored, 99); after = u.EffectiveSkillDamage;
                    return kind == StatusKind.SkillDamageUp ? after - before : before - after;
                case StatusKind.ArmorBreakUp: case StatusKind.ArmorBreakDown:
                    before = u.EffectiveArmorBreak; u.AddOrRefresh(kind, 2, stored, 99); after = u.EffectiveArmorBreak;
                    return kind == StatusKind.ArmorBreakUp ? after - before : before - after;
            }
            return float.NaN;
        }

        /// <summary>One authored effect alone in a basic, with the clauses that decide whether it
        /// resolves taken off and a target this arena can always answer. Chance and resistance are
        /// taken out of the way so a status always lands: the card states what a landed status is.</summary>
        private static AbilityDef FactProbe(AbilityDef source, EffectDef authored)
        {
            EffectDef probe = authored.Clone();
            probe.Condition = EffectCondition.None;
            probe.Condition2 = EffectCondition.None;
            probe.Condition3 = EffectCondition.None;
            probe.Chance = 1f;
            probe.BypassResist = true;
            probe.Target = TargetMode.FrontEnemy;
            AbilityDef ability = new AbilityDef();
            ability.Id = "factprobe_" + source.Id;
            ability.Name = source.Name;
            ability.Kind = source.Kind == AbilityKind.Active ? AbilityKind.Active : AbilityKind.BasicAttack;
            ability.EnergyCost = 0;
            ability.Vfx = source.Vfx;
            ability.Effects.Add(probe);
            return ability;
        }

        private static BattleSession OpenFactSession(ContentDatabase arena, StageDef stage, HeroDef def)
        {
            BattleRequest req = new BattleRequest();
            req.Content = arena;
            req.Stage = stage;
            req.Seed = 77;
            req.Team = new List<HeroInstance>();
            req.Team.Add(MakeInstance(def, AbilityNumberLevel));
            return CombatSimulation.Begin(req);
        }

        private static int EnemyId(BattleSession s)
        {
            IList<UnitView> us = s.Units;
            for (int i = 0; i < us.Count; i++) if (!us[i].IsPlayer) return us[i].Id;
            return -1;
        }

        private static StatusView FindStatus(UnitView v, StatusKind kind)
        {
            if (v == null) return null;
            for (int i = 0; i < v.Statuses.Count; i++) if (v.Statuses[i].Kind == kind) return v.Statuses[i];
            return null;
        }

        private static void CheckAbilityFactsMatchTheSimulation(ContentDatabase live, string contentDir)
        {
            Line("ABILITY FACTS (what the card prints besides its numbers, against the simulation)");

            ContentDatabase arena = BuildAbilityNumberArena();
            StageDef stage = arena == null ? null : arena.GetStage("number_arena");
            if (arena == null || stage == null) { Check(false, "ability-fact arena built"); Line(""); return; }
            arena.Balance.DamageVariance = 0f;
            arena.Balance.SpeedVariance = 0f;

            int derivedWards = 0, costs = 0, energies = 0, wards = 0, statuses = 0, sizes = 0, thresholds = 0, limits = 0, needs = 0;
            List<string> wrong = new List<string>();
            List<string> notLanded = new List<string>();

            for (int h = 0; h < arena.Heroes.Count; h++)
            {
                HeroDef def = arena.Heroes[h];
                if (def.BasicAttack != null && def.BasicAttack.Id.Contains("placeholder")) continue;
                UnitStats stats = StatMath.ForHero(def, AbilityNumberLevel);

                List<PassiveDef> passives = new List<PassiveDef>(def.Passives);
                AbilityDef realBasic = def.BasicAttack;
                AbilityDef realActive = def.Active;

                List<AbilityDef> abilities = new List<AbilityDef>();
                List<PassiveDef> owners = new List<PassiveDef>();
                abilities.Add(realBasic); owners.Add(null);
                abilities.Add(realActive); owners.Add(null);
                for (int p = 0; p < passives.Count; p++) { abilities.Add(AbilityMath.AsAbility(passives[p])); owners.Add(passives[p]); }

                for (int a = 0; a < abilities.Count; a++)
                {
                    AbilityDef ability = abilities[a];
                    if (ability == null) continue;
                    PassiveDef passive = owners[a];
                    List<AbilityFact> facts = AbilityFacts.For(ability, passive, stats);

                    for (int f = 0; f < facts.Count; f++)
                    {
                        AbilityFact fact = facts[f];
                        string who = ability.Id + " " + fact.Kind + " '" + fact.Text + "'";

                        if (fact.Kind == AbilityFactKind.Cost)
                        {
                            costs++;
                            // The cost is the Energy the simulation asks for: one short and the cast
                            // is refused out loud, exactly enough and it is accepted.
                            def.Passives.Clear();
                            def.Active = realActive;
                            BattleSession s = OpenFactSession(arena, stage, def);
                            s.IgnoreEnergyForManualCasts = false;
                            int me = ActorId(s);
                            int cost = (int)fact.Amount;
                            s.SetEnergy(me, cost - 1);
                            ActionReport refused = s.Act(me, ManualChoice.Active, -1);
                            s.SetEnergy(me, cost);
                            ActionReport accepted = s.Act(me, ManualChoice.Active, -1);
                            if (refused.Accepted || !accepted.Accepted)
                                wrong.Add(who + ": at cost-1 accepted=" + refused.Accepted + ", at cost accepted=" + accepted.Accepted);
                            def.Passives.AddRange(passives);
                            continue;
                        }

                        if (fact.Kind == AbilityFactKind.Trigger)
                        {
                            if (fact.Amount > 0f) thresholds++;
                            continue;
                        }
                        if (fact.Kind == AbilityFactKind.Limit) { limits++; continue; }
                        if (fact.Kind == AbilityFactKind.Needs) { needs++; continue; }
                        if (fact.EffectIndex < 0) continue;

                        AbilityDef probe = FactProbe(ability, ability.Effects[fact.EffectIndex]);
                        def.BasicAttack = probe;
                        def.Active = null;
                        def.Passives.Clear();
                        def.StartEnergy = 0;

                        BattleSession live2 = OpenFactSession(arena, stage, def);
                        int hero = ActorId(live2);
                        int dummy = EnemyId(live2);
                        if (fact.Kind == AbilityFactKind.Energy) live2.SetEnergy(dummy, 50);
                        ActionReport r = live2.Act(hero, ManualChoice.Basic, -1);

                        def.BasicAttack = realBasic;
                        def.Active = realActive;
                        def.Passives.AddRange(passives);

                        if (!r.Accepted) { wrong.Add(who + ": probe refused: " + r.Refusal); continue; }
                        UnitView dv = live2.Unit(dummy);

                        if (fact.Kind == AbilityFactKind.Energy)
                        {
                            energies++;
                            int expect = Math.Max(0, Math.Min(dv.EnergyMax, 50 + (int)fact.Amount));
                            if (dv.Energy != expect) wrong.Add(who + ": card " + fact.Amount + ", dummy 50 -> " + dv.Energy);
                        }
                        else if (fact.Kind == AbilityFactKind.Ward)
                        {
                            wards++;
                            StatusView sv = FindStatus(dv, StatusKind.Shield);
                            // A ward whose whole amount is a runtime quantity (the ScaleBy family) is
                            // worth nothing until the fight has built it, so the probe cannot land it.
                            // Its duration is the same Math.Max(1, Duration) line; it is counted, not hidden.
                            if (sv == null && AbilityMath.ReplacesTheAttackTerm(ability.Effects[fact.EffectIndex])) derivedWards++;
                            else if (sv == null) notLanded.Add(who);
                            else if (sv.RoundsLeft != fact.Rounds)
                                wrong.Add(who + ": card " + fact.Rounds + " rounds, simulation " + sv.RoundsLeft);
                        }
                        else if (fact.Kind == AbilityFactKind.Status)
                        {
                            statuses++;
                            StatusView sv = FindStatus(dv, fact.Status);
                            if (sv == null) { notLanded.Add(who); continue; }
                            if (sv.RoundsLeft != fact.Rounds)
                                wrong.Add(who + ": card " + fact.Rounds + " rounds, simulation " + sv.RoundsLeft);
                            if (fact.QuotesSize)
                            {
                                sizes++;
                                if (fact.Status == StatusKind.Burn || fact.Status == StatusKind.Regen)
                                {
                                    if (Math.Abs(sv.Magnitude - fact.Stored) > 0.01f)
                                        wrong.Add(who + ": card stores " + fact.Stored + ", simulation " + sv.Magnitude);
                                }
                                else
                                {
                                    if (Math.Abs(sv.Magnitude - fact.Stored) > 1e-4f)
                                        wrong.Add(who + ": card " + fact.Stored + ", simulation stored " + sv.Magnitude);
                                    float measured = StatusReadBack(def, fact.Status, sv.Magnitude);
                                    float printed = fact.Stored;
                                    if (float.IsNaN(measured) || Math.Abs(measured - printed) > 1e-3f)
                                        wrong.Add(who + ": card " + printed + ", stat moved by " + measured);
                                }
                            }
                        }
                    }
                }
                def.BasicAttack = realBasic;
                def.Active = realActive;
            }

            Line("  [note] " + costs + " Energy cost(s), " + energies + " Energy grant(s)/drain(s), " + wards
                 + " Ward duration(s) (" + derivedWards + " on a runtime amount, not probeable), " + statuses + " status line(s) of which " + sizes + " quote a size, "
                 + thresholds + " HP threshold(s), " + limits + " limit line(s), " + needs + " stack need(s)");
            Check(wrong.Count == 0,
                  wrong.Count == 0
                    ? "every Energy cost, Energy amount, Ward and status duration, and status size the card prints equals what "
                      + "CombatSimulation does with the same effect"
                    : wrong.Count + " card fact(s) do NOT match the simulation: " + string.Join("; ", wrong.ToArray()));
            Check(notLanded.Count == 0,
                  notLanded.Count == 0 ? "every probed status and Ward landed, so none of the above is a vacuous pass"
                  : notLanded.Count + " probe(s) never landed the status: " + string.Join("; ", notLanded.ToArray()));

            // The set of kinds the card quotes a size for is the set the harness can read back.
            List<string> unreadable = new List<string>();
            foreach (StatusKind k in Enum.GetValues(typeof(StatusKind)))
            {
                if (AbilityFacts.StatSize(k, 0.1f) == null) continue;
                if (float.IsNaN(StatusReadBack(arena.Heroes[0], k, 0.1f))) unreadable.Add(k.ToString());
            }
            Check(unreadable.Count == 0,
                  unreadable.Count == 0 ? "every status kind the card quotes a size for has a getter the harness reads it back through"
                  : "kind(s) the card quotes with no read-back: " + string.Join(", ", unreadable.ToArray()));
            Line("");
        }
    }
}
