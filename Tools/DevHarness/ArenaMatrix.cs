using System;
using System.Collections.Generic;
using System.Reflection;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE ARENA MATRIX: every authored rule in the roster, measured somewhere.
    ///
    /// The authored-hero and authored-talent sweeps field every kit in ONE arena, one squad and one
    /// enemy line at five seeds. That is what makes their fingerprints comparable, and it is also
    /// why a double-figure tail of rules is never seen doing anything there: one line cannot raise
    /// a Taunt, a particular Aspect matchup, a second copy of the hero, an ally role the fillers do
    /// not field and a kill at the right moment all at once. THE DARK TALENTS generalised the fix
    /// for about twenty clauses - a small room per gate. This file generalises it to the roster.
    ///
    /// A RULE is one basic, one active, one passive slot or one talent. Each is measured by
    /// ABLATION: the same hero, the same room, the same seed, with that one rule removed (a basic or
    /// active keeps its cast and loses its effects; a passive becomes an inert passive under the
    /// same id and slot key; a talent is simply not selected). The comparison is the test, and the
    /// rule's own PassiveTrigger announcement is taken OUT of it before hashing - a clause whose
    /// only trace is announcing itself has changed nothing, and an instrument that counted the
    /// announcement would convert that unknown into a false green.
    ///
    /// Three classes, reported rather than inferred:
    ///   (a) observed firing AND changing the fight in the sweep arena;
    ///   (b) observed only in a named room of this matrix, where the room also proves the same seed
    ///       twice draws nothing extra and that the control (the ablated run) differs, and states
    ///       the measurement as a count or an amount;
    ///   (c) observed nowhere - named, with what it waits on.
    /// </summary>
    internal static partial class Program
    {
        private enum MatrixRuleKind { Basic, Active, Passive, Talent }

        private sealed class MatrixRule
        {
            public HeroDef Hero;
            public MatrixRuleKind Kind;
            /// <summary>Passive index into Hero.Passives, or talent selection 1..3.</summary>
            public int Index;
            /// <summary>The ability or passive id, which is what its events carry.</summary>
            public string Id;
            /// <summary>What the rest of the harness calls it: a passive or ability id, or "hero II".</summary>
            public string Name;
            /// <summary>True when the rule answers its own trigger and so can be counted firing.</summary>
            public bool Triggered;
            public PassiveDef Passive;

            // What the sweep arena measured.
            public int SweepFires, SweepMoved;
            // What the matrix measured, when a room was run for it.
            public string Room;
            public bool RoomPassed;
            public string RoomReading;

            public bool InSweep { get { return SweepMoved > 0 && (!Triggered || SweepFires > 0); } }
        }

        // ------------------------------------------------------------------ the rules

        private static List<MatrixRule> MatrixRulesOf(ContentDatabase arena, ContentDatabase live)
        {
            List<HeroDef> heroes = new List<HeroDef>();
            for (int i = 0; i < live.Heroes.Count; i++)
            {
                HeroDef h = live.Heroes[i];
                if (h.BasicAttack != null && h.BasicAttack.Id.Contains("placeholder")) continue;
                HeroDef a = arena.GetHero(h.Id);
                if (a != null) heroes.Add(a);
            }
            heroes.Sort(delegate (HeroDef x, HeroDef y) { return string.CompareOrdinal(x.Id, y.Id); });

            List<MatrixRule> rules = new List<MatrixRule>();
            for (int h = 0; h < heroes.Count; h++)
            {
                HeroDef def = heroes[h];
                if (def.BasicAttack != null)
                    rules.Add(new MatrixRule { Hero = def, Kind = MatrixRuleKind.Basic, Index = -1,
                                               Id = def.BasicAttack.Id, Name = def.BasicAttack.Id });
                if (def.Active != null)
                    rules.Add(new MatrixRule { Hero = def, Kind = MatrixRuleKind.Active, Index = -1,
                                               Id = def.Active.Id, Name = def.Active.Id });
                for (int p = 0; p < def.Passives.Count; p++)
                {
                    PassiveDef pd = def.Passives[p];
                    rules.Add(new MatrixRule { Hero = def, Kind = MatrixRuleKind.Passive, Index = p,
                                               Id = pd.Id, Name = pd.Id, Passive = pd,
                                               Triggered = pd.Trigger != PassiveTrigger.None && pd.Effects.Count > 0 });
                }
                for (int t = 1; t <= def.Talents.Count; t++)
                {
                    PassiveDef td = def.TalentAt(t);
                    rules.Add(new MatrixRule { Hero = def, Kind = MatrixRuleKind.Talent, Index = t,
                                               Id = td == null ? "" : td.Id, Name = def.Id + " " + RomanOf(t),
                                               Passive = td,
                                               Triggered = td != null && td.Trigger != PassiveTrigger.None
                                                           && td.Effects.Count > 0 });
                }
            }
            return rules;
        }

        private static readonly MethodInfo _memberwiseClone =
            typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _passivesField =
            typeof(UnitDef).GetField("Passives", BindingFlags.Public | BindingFlags.Instance);

        /// <summary>
        /// The hero with ONE rule taken out and everything else left exactly as authored. A basic
        /// or an active keeps its id, its cost and its cast and loses its effects, so the turn order
        /// and the energy clock are the same fight and only what the rule DID is gone. A passive is
        /// replaced by an inert one under the same id and slot key, so a talent that names the slot
        /// still finds it. A talent needs no ablation: not selecting it is the control.
        /// </summary>
        private static HeroDef MatrixAblate(MatrixRule rule)
        {
            if (rule.Kind == MatrixRuleKind.Talent) return rule.Hero;
            HeroDef clone = (HeroDef)_memberwiseClone.Invoke(rule.Hero, null);
            if (rule.Kind == MatrixRuleKind.Basic) clone.BasicAttack = HollowAbility(rule.Hero.BasicAttack);
            else if (rule.Kind == MatrixRuleKind.Active) clone.Active = HollowAbility(rule.Hero.Active);
            else if (MatrixCarriedByBasic(rule.Passive))
            {
                // A passive that CARRIES NO RULE OF ITS OWN and whose sentence the basic implements:
                // the ablation takes out exactly the basic effects that sentence names, so the
                // passive is measured by the thing it describes rather than read as empty.
                AbilityDef b = HollowAbility(rule.Hero.BasicAttack);
                for (int i = 0; i < rule.Hero.BasicAttack.Effects.Count; i++)
                {
                    EffectDef e = rule.Hero.BasicAttack.Effects[i];
                    if (!MatrixRefrainEffect(e)) b.Effects.Add(e);
                }
                clone.BasicAttack = b;
            }
            else
            {
                List<PassiveDef> list = new List<PassiveDef>(rule.Hero.Passives);
                PassiveDef was = list[rule.Index];
                PassiveDef inert = new PassiveDef();
                inert.Id = was.Id;
                inert.SlotKey = was.SlotKey;
                inert.Name = was.Name;
                list[rule.Index] = inert;
                _passivesField.SetValue(clone, list);
            }
            return clone;
        }

        /// <summary>
        /// The one passive in the roster that carries no trigger, no effects and no standing
        /// modifier, and is still a rule: Oris's No Fixed Refrain. Its sentence - "after Improvise
        /// the Way, the next basic strikes the highest-Energy enemy and takes Energy; the one after
        /// strikes the lowest-Energy enemy and advances his turn" - is implemented by the five
        /// effects of his basic gated on his holding MarkAlt, which his active lays. Named here by
        /// id rather than inferred from "empty", so a passive that is empty by MISTAKE still reads
        /// as dark instead of being quietly measured through somebody else's basic.
        /// </summary>
        private static bool MatrixCarriedByBasic(PassiveDef p)
        {
            return p != null && p.Id == "oris_no_fixed_refrain";
        }

        private static bool MatrixRefrainEffect(EffectDef e)
        {
            return e.Condition == EffectCondition.HasStatus && e.ConditionOn == ConditionSubject.Caster
                   && e.ConditionStatus == StatusKind.MarkAlt;
        }

        private static AbilityDef HollowAbility(AbilityDef a)
        {
            AbilityDef h = new AbilityDef();
            h.Id = a.Id;
            h.Name = a.Name;
            h.Description = a.Description;
            h.Kind = a.Kind;
            h.EnergyCost = a.EnergyCost;
            h.Vfx = a.Vfx;
            return h;
        }

        // ------------------------------------------------------------------ one battle, read

        /// <summary>
        /// One battle reduced to what the matrix compares. The hash is the sweep's own, with the
        /// rule's own PassiveTrigger announcements left out; the tally is a set of named counts and
        /// amounts read off the event list, so every room can say WHAT moved, and by how much.
        /// </summary>
        private sealed class MatrixReading
        {
            public string Hash;
            public string FullHash;
            public readonly Dictionary<string, long> Tally = new Dictionary<string, long>();
            public long Get(string key) { long v; Tally.TryGetValue(key, out v); return v; }
            public void Add(string key, long by) { long v; Tally.TryGetValue(key, out v); Tally[key] = v + by; }
        }

        private static MatrixReading MatrixRead(BattleResult r, MatrixRule rule, int subjectId)
        {
            MatrixReading m = new MatrixReading();
            if (r == null) { m.Hash = "NULL"; m.FullHash = "NULL"; return m; }
            m.FullHash = HeroSweepHash(r);

            ulong hash = 14695981039346656037UL;
            HeroSweepMix(ref hash, r.Victory ? 1 : 0);
            HeroSweepMix(ref hash, r.Rounds);
            Dictionary<int, int> energy = new Dictionary<int, int>();
            for (int i = 0; i < r.Units.Count; i++) energy[r.Units[i].Id] = r.Units[i].StartEnergy;

            for (int i = 0; i < r.Events.Count; i++)
            {
                BattleEvent e = r.Events[i];
                bool own = e.Type == BattleEventType.PassiveTrigger && !string.IsNullOrEmpty(rule.Id)
                           && e.AbilityId == rule.Id;
                if (own) { m.Add("firings", 1); continue; }

                HeroSweepMix(ref hash, (int)e.Type);
                HeroSweepMix(ref hash, e.SourceId);
                HeroSweepMix(ref hash, e.TargetId);
                HeroSweepMix(ref hash, e.Value);
                HeroSweepMix(ref hash, (int)e.Status);

                UnitSnapshot tgt = r.FindUnit(e.TargetId);
                bool bySubject = e.SourceId == subjectId;
                bool onSubject = e.TargetId == subjectId;
                switch (e.Type)
                {
                    case BattleEventType.BasicAttack:
                        if (bySubject && rule.Kind == MatrixRuleKind.Basic) m.Add("casts", 1);
                        break;
                    case BattleEventType.AbilityCast:
                        if (bySubject && rule.Kind == MatrixRuleKind.Active && e.AbilityId == rule.Id) m.Add("casts", 1);
                        if (bySubject) m.Add("subject.casts", 1);
                        break;
                    case BattleEventType.Damage:
                        if (bySubject) { m.Add("damage", e.Value); m.Add("hits", 1); }
                        if (onSubject) m.Add("damage.taken", e.Value);
                        break;
                    case BattleEventType.Heal:
                        if (bySubject) m.Add(onSubject ? "heal.self" : "heal.others", e.Value);
                        if (tgt != null && !tgt.IsPlayer) m.Add("heal.enemy", e.Value);
                        break;
                    case BattleEventType.ShieldGain:
                        if (bySubject) m.Add(onSubject ? "shield.self" : "shield.others", e.Value);
                        break;
                    case BattleEventType.ShieldAbsorb:
                        if (onSubject) m.Add("absorbed.subject", e.Value);
                        break;
                    case BattleEventType.StatusApplied:
                        if (bySubject) m.Add("applied:" + e.Status, 1);
                        if (onSubject) m.Add("gained:" + e.Status, 1);
                        break;
                    case BattleEventType.StatusResisted:
                        if (bySubject) m.Add("resisted:" + e.Status, 1);
                        break;
                    case BattleEventType.StatusExpired:
                        if (bySubject && !onSubject) m.Add("removed:" + e.Status, 1);
                        if (onSubject) m.Add("lost:" + e.Status, 1);
                        break;
                    case BattleEventType.StatusSpread:
                        m.Add("spreads", 1);
                        break;
                    case BattleEventType.TurnOrderShift:
                        if (tgt != null) m.Add(tgt.IsPlayer ? "shift.ally" : "shift.enemy", 1);
                        if (bySubject) m.Add("shift.by.subject", 1);
                        break;
                    case BattleEventType.ExtraTurn:
                        m.Add("extra.turns", 1);
                        break;
                    case BattleEventType.PositionSwapped:
                        m.Add("swaps", 1);
                        break;
                    case BattleEventType.CastCancelled:
                        m.Add("cancels", 1);
                        break;
                    case BattleEventType.TargetAnchored:
                        m.Add("anchors", 1);
                        break;
                    case BattleEventType.Death:
                        if (tgt != null) m.Add(tgt.IsPlayer ? "deaths.ally" : "deaths.enemy", 1);
                        break;
                    case BattleEventType.EnergyChanged:
                    {
                        int was;
                        if (!energy.TryGetValue(e.TargetId, out was)) was = 0;
                        int d = e.Value - was;
                        energy[e.TargetId] = e.Value;
                        if (tgt == null || d == 0) break;
                        string side = onSubject ? "subject" : tgt.IsPlayer ? "ally" : "enemy";
                        m.Add("energy." + side + (d > 0 ? "+" : "-"), Math.Abs(d));
                        // Counted by EXACT delta as well as summed, for the reason DarkProbe gives:
                        // a bar is capped, so a total swallows every grant that arrived at a full
                        // bar, and "+5 once" is both what a talent pays and what its text says.
                        m.Add("payments." + side + (d > 0 ? ":+" : ":") + d, 1);
                        break;
                    }
                }
            }
            m.Tally["rounds"] = r.Rounds;
            m.Tally["victory"] = r.Victory ? 1 : 0;
            m.Hash = hash.ToString("x16");
            return m;
        }

        private static int MatrixSubjectId(BattleResult r, string heroId)
        {
            if (r == null) return -1;
            for (int i = 0; i < r.Units.Count; i++)
                if (r.Units[i].IsPlayer && r.Units[i].Slot == 0 && r.Units[i].DefId == heroId) return r.Units[i].Id;
            for (int i = 0; i < r.Units.Count; i++)
                if (r.Units[i].IsPlayer && r.Units[i].DefId == heroId) return r.Units[i].Id;
            return -1;
        }

        /// <summary>One battle, intact or ablated, with the subject in slot 0 and the rest behind it.</summary>
        private static MatrixReading MatrixFight(ContentDatabase db, StageDef stage, MatrixRule rule, bool ablated,
                                                 List<HeroDef> rest, int seed)
        {
            List<HeroInstance> team = new List<HeroInstance>();
            HeroDef def = ablated ? MatrixAblate(rule) : rule.Hero;
            HeroInstance me = MakeInstance(def, 40);
            me.SelectedTalent = rule.Kind == MatrixRuleKind.Talent && !ablated ? rule.Index : 0;
            team.Add(me);
            for (int i = 0; i < rest.Count; i++) team.Add(MakeInstance(rest[i], 40));
            BattleResult r = RunWith(db, stage, seed, team);
            return MatrixRead(r, rule, MatrixSubjectId(r, rule.Hero.Id));
        }

        // ------------------------------------------------------------------ the sweep arena

        private static List<MatrixRule> _matrixRules;

        /// <summary>
        /// Every rule, ablated in the sweep arena at the sweep's own five seeds and squad. A rule is
        /// class (a) when the fight without it differs from the fight with it, its own announcement
        /// excluded, and - for a rule with a trigger of its own - when it fired.
        /// </summary>
        private static List<MatrixRule> MeasureMatrixSweep(ContentDatabase live)
        {
            ContentDatabase arena = BuildHeroSweepArena(live);
            List<HeroDef> fillers = BuildHeroSweepFillers();
            StageDef stage = arena == null ? null : arena.GetStage("sweep_arena");
            if (arena == null || fillers == null || stage == null) return null;

            List<MatrixRule> rules = MatrixRulesOf(arena, live);
            for (int i = 0; i < rules.Count; i++)
            {
                MatrixRule rule = rules[i];
                for (int s = 1; s <= HeroSweepSeeds; s++)
                {
                    int seed = HeroSweepSeed(rule.Hero.Id, s);
                    MatrixReading with = MatrixFight(arena, stage, rule, false, fillers, seed);
                    MatrixReading without = MatrixFight(arena, stage, rule, true, fillers, seed);
                    int fires = (int)(rule.Kind == MatrixRuleKind.Basic || rule.Kind == MatrixRuleKind.Active
                                      ? with.Get("casts") : with.Get("firings"));
                    rule.SweepFires += fires;
                    if (with.Hash != without.Hash) rule.SweepMoved++;
                }
            }
            return rules;
        }

        // ------------------------------------------------------------------ the rooms

        /// <summary>
        /// THE MATRIX ARENA: THE DARK TALENTS' bodies and stages, plus the handful of archetypes that
        /// section never needed. Each new body opens one gate the sweep's five cannot: a body that
        /// Wards itself (an enemy gaining a Shield), one that hides itself (an enemy gaining
        /// Stealth, which is not the same as t_cloaker handing Stealth to the FRONT HERO), one that
        /// debuffs the heroes (an enemy applying a status), one that Taunts itself, brittle bodies
        /// that die to a cast rather than to the first blow, and one body of each Aspect the dark
        /// line does not carry. None is tuned; the dark arena's zero-variance clock is kept, so a
        /// room's amounts are amounts rather than dice.
        /// </summary>
        private const string MatrixExtraEnemies = @",
          {""id"":""m_warder"",""name"":""MWarder"",""aspect"":""Ember"",""hp"":400000,""attack"":200,""defense"":0,""speed"":75,""critChance"":0,""critDamage"":1.0,""basic"":{""id"":""m_warder_b"",""name"":""Brace"",""effects"":[{""kind"":""Shield"",""target"":""Self"",""selfHpRatio"":0.05,""duration"":2},{""kind"":""Damage"",""target"":""FrontEnemy"",""power"":0.2}]}},
          {""id"":""m_hider"",""name"":""MHider"",""aspect"":""Ember"",""hp"":400000,""attack"":200,""defense"":0,""speed"":75,""critChance"":0,""critDamage"":1.0,""basic"":{""id"":""m_hider_b"",""name"":""Fade"",""effects"":[{""kind"":""ApplyStatus"",""target"":""Self"",""status"":""Stealth"",""duration"":1,""chance"":1},{""kind"":""Damage"",""target"":""FrontEnemy"",""power"":0.2}]}},
          {""id"":""m_hexer"",""name"":""MHexer"",""aspect"":""Ember"",""hp"":400000,""attack"":200,""defense"":0,""speed"":75,""critChance"":0,""critDamage"":1.0,""basic"":{""id"":""m_hexer_b"",""name"":""Hex"",""effects"":[{""kind"":""ApplyStatus"",""target"":""FrontEnemy"",""status"":""AttackDown"",""magnitude"":0.05,""duration"":2,""chance"":1,""bypassResist"":true},{""kind"":""Damage"",""target"":""FrontEnemy"",""power"":0.2}]}},
          {""id"":""m_taunter"",""name"":""MTaunter"",""aspect"":""Ember"",""hp"":400000,""attack"":200,""defense"":0,""speed"":75,""critChance"":0,""critDamage"":1.0,""basic"":{""id"":""m_taunter_b"",""name"":""Bellow"",""effects"":[{""kind"":""ApplyStatus"",""target"":""Self"",""status"":""Taunt"",""magnitude"":1.0,""duration"":1,""chance"":1},{""kind"":""Damage"",""target"":""FrontEnemy"",""power"":0.2}]}},
          {""id"":""m_brittle"",""name"":""MBrittle"",""aspect"":""Ember"",""hp"":5000,""attack"":150,""defense"":0,""speed"":40,""critChance"":0,""critDamage"":1.0,""basic"":{""id"":""m_brittle_b"",""name"":""Prod"",""effects"":[{""kind"":""Damage"",""target"":""FrontEnemy"",""power"":0.2}]}},
          {""id"":""m_fastbruiser"",""name"":""MFastBruiser"",""aspect"":""Ember"",""hp"":400000,""attack"":900,""defense"":0,""speed"":200,""critChance"":0,""critDamage"":1.0,""basic"":{""id"":""m_fastbruiser_b"",""name"":""Maul"",""effects"":[{""kind"":""Damage"",""target"":""LowestHpEnemy"",""power"":0.6}]}},
          {""id"":""m_gale"",""name"":""MGale"",""aspect"":""Gale"",""hp"":400000,""attack"":200,""defense"":0,""speed"":70,""critChance"":0,""critDamage"":1.0,""basic"":{""id"":""m_gale_b"",""name"":""Prod"",""effects"":[{""kind"":""Damage"",""target"":""FrontEnemy"",""power"":0.2}]}},
          {""id"":""m_verdant"",""name"":""MVerdant"",""aspect"":""Verdant"",""hp"":400000,""attack"":200,""defense"":0,""speed"":70,""critChance"":0,""critDamage"":1.0,""basic"":{""id"":""m_verdant_b"",""name"":""Prod"",""effects"":[{""kind"":""Damage"",""target"":""FrontEnemy"",""power"":0.2}]}},
          {""id"":""m_lumen"",""name"":""MLumen"",""aspect"":""Lumen"",""hp"":400000,""attack"":200,""defense"":0,""speed"":70,""critChance"":0,""critDamage"":1.0,""basic"":{""id"":""m_lumen_b"",""name"":""Prod"",""effects"":[{""kind"":""Damage"",""target"":""FrontEnemy"",""power"":0.2}]}},
          {""id"":""m_umbra"",""name"":""MUmbra"",""aspect"":""Umbra"",""hp"":400000,""attack"":200,""defense"":0,""speed"":70,""critChance"":0,""critDamage"":1.0,""basic"":{""id"":""m_umbra_b"",""name"":""Prod"",""effects"":[{""kind"":""Damage"",""target"":""FrontEnemy"",""power"":0.2}]}}
          ]}";

        private const string MatrixExtraStages = @",
          {""id"":""m_warder_tail"",""index"":101,""enemies"":[{""id"":""m_warder"",""level"":40},{""id"":""t_tail"",""level"":40}]},
          {""id"":""m_hider_tail"",""index"":102,""enemies"":[{""id"":""m_hider"",""level"":40},{""id"":""t_tail"",""level"":40}]},
          {""id"":""m_hexer"",""index"":103,""enemies"":[{""id"":""m_hexer"",""level"":40}]},
          {""id"":""m_taunter_tail"",""index"":104,""enemies"":[{""id"":""m_taunter"",""level"":40},{""id"":""t_tail"",""level"":40}]},
          {""id"":""m_brittle3_big"",""index"":105,""enemies"":[{""id"":""m_brittle"",""level"":40},{""id"":""m_brittle"",""level"":40},{""id"":""m_brittle"",""level"":40},{""id"":""t_big"",""level"":40}]},
          {""id"":""m_aspects"",""index"":106,""enemies"":[{""id"":""m_gale"",""level"":40},{""id"":""m_verdant"",""level"":40},{""id"":""m_lumen"",""level"":40},{""id"":""m_umbra"",""level"":40},{""id"":""t_big"",""level"":40}]},
          {""id"":""m_caster_brittle"",""index"":107,""enemies"":[{""id"":""t_caster"",""level"":40},{""id"":""m_brittle"",""level"":40},{""id"":""m_brittle"",""level"":40}]},
          {""id"":""m_fastbruiser"",""index"":109,""enemies"":[{""id"":""m_fastbruiser"",""level"":40}]},
          {""id"":""m_buffer_brittle"",""index"":108,""enemies"":[{""id"":""m_brittle"",""level"":40},{""id"":""t_buffer"",""level"":40}]}
          ]}]}";

        private static ContentDatabase _matrixArena, _matrixArenaSlow;

        /// <summary>
        /// The dark arena's content with the matrix's extra bodies and stages spliced onto the end
        /// of each list, so every dark stage is still here under its own id and a room can name
        /// either kind.
        /// </summary>
        private static ContentDatabase MatrixArena(bool slow)
        {
            if (slow && _matrixArenaSlow != null) return _matrixArenaSlow;
            if (!slow && _matrixArena != null) return _matrixArena;
            string dir = FindContentDirForSweep();
            if (dir == null) return null;
            string heroes;
            try { heroes = System.IO.File.ReadAllText(System.IO.Path.Combine(dir, "heroes.json")); }
            catch (Exception) { return null; }

            string enemies = DarkEnemies.Substring(0, DarkEnemies.LastIndexOf(']')).TrimEnd() + MatrixExtraEnemies;
            int stagesEnd = DarkCampaign.LastIndexOf(']', DarkCampaign.LastIndexOf(']') - 1);
            string campaign = DarkCampaign.Substring(0, stagesEnd).TrimEnd() + MatrixExtraStages;
            ContentDatabase db = LoadSyntheticContent(heroes, enemies, campaign,
                                                      slow ? DarkSlowBalance : DarkBalance,
                                                      slow ? "arena-matrix (slow clock)" : "arena-matrix");
            if (slow) _matrixArenaSlow = db; else _matrixArena = db;
            return db;
        }

        /// <summary>
        /// One room: the stage, the squad beside the subject, the seed and the clock, and what the
        /// room is asserted to MEASURE - a named tally from <see cref="MatrixRead"/> and the
        /// direction it has to move when the rule is present. Why says what the room raises that
        /// the sweep arena does not, in the rule's own terms.
        /// </summary>
        private sealed class MatrixRoom
        {
            public readonly string Rule, Stage, Allies, Measure, Why;
            public readonly int Seed, Direction;
            public readonly bool Slow;
            public MatrixRoom(string rule, string stage, string allies, int seed, bool slow,
                              string measure, int direction, string why)
            {
                Rule = rule; Stage = stage; Allies = allies; Seed = seed; Slow = slow;
                Measure = measure; Direction = direction; Why = why;
            }
        }

        /// <summary>
        /// The squad beside the subject, or null when it is not a lineup a player could field. A
        /// hero appears in a team once - PlayerProfile.AssignToTeam SWAPS a hero that is already
        /// deployed rather than placing it twice - so a room that names the subject among its
        /// allies, or any hero twice, is an arena that cannot occur in play, and a clause observed
        /// only there has not been observed.
        /// </summary>
        private static List<HeroDef> MatrixSquad(ContentDatabase db, string allies, HeroDef subject)
        {
            List<HeroDef> squad = new List<HeroDef>();
            if (string.IsNullOrEmpty(allies)) return squad;
            string[] ids = allies.Split(',');
            if (ids.Length + 1 > PlayerProfile.TeamSize) return null;
            Dictionary<string, bool> seen = new Dictionary<string, bool>();
            seen[subject.Id] = true;
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i].Trim();
                if (seen.ContainsKey(id)) return null;
                seen[id] = true;
                HeroDef d = db.GetHero(id);
                if (d == null) return null;
                squad.Add(d);
            }
            return squad;
        }

        /// <summary>
        /// A room, run the way the house pattern says a room is run: the rule present, the rule
        /// ablated (the control), and the rule present AGAIN on the same seed. It passes only when
        /// the two present runs are byte-identical (the arena draws nothing extra), the control
        /// differs from them with the rule's own announcement excluded (the comparison can fail),
        /// the rule fired if it has a trigger of its own, and the named measurement moved the way
        /// the room says it does. The reading is stated as counts and amounts either way.
        /// </summary>
        private static bool EvaluateMatrixRoom(MatrixRule rule, MatrixRoom room, out string reading)
        {
            reading = "";
            ContentDatabase db = MatrixArena(room.Slow);
            if (db == null) { reading = "arena did not build"; return false; }
            StageDef stage = db.GetStage(room.Stage);
            HeroDef subject = db.GetHero(rule.Hero.Id);
            if (stage == null || subject == null) { reading = "no stage " + room.Stage; return false; }
            List<HeroDef> squad = MatrixSquad(db, room.Allies, subject);
            if (squad == null)
            {
                reading = "not a lineup a player can field (a hero twice, or more than "
                          + PlayerProfile.TeamSize + "): " + rule.Hero.Id + " beside " + room.Allies;
                return false;
            }

            MatrixRule local = RebindRule(rule, subject);
            MatrixReading with = MatrixFight(db, stage, local, false, squad, room.Seed);
            MatrixReading again = MatrixFight(db, stage, local, false, squad, room.Seed);
            MatrixReading without = MatrixFight(db, stage, local, true, squad, room.Seed);
            MatrixReading withoutAgain = MatrixFight(db, stage, local, true, squad, room.Seed);

            bool same = with.FullHash == again.FullHash && without.FullHash == withoutAgain.FullHash;
            long fires = local.Kind == MatrixRuleKind.Basic || local.Kind == MatrixRuleKind.Active
                         ? with.Get("casts") : with.Get("firings");
            bool fired = !local.Triggered || fires > 0;
            bool moved = with.Hash != without.Hash;

            bool measured;
            string amount;
            if (!string.IsNullOrEmpty(room.Measure))
            {
                long w = with.Get(room.Measure), wo = without.Get(room.Measure);
                measured = room.Direction > 0 ? w > wo : room.Direction < 0 ? w < wo : w != wo;
                amount = room.Measure + " " + w + " against " + wo + " without it";
            }
            else
            {
                List<string> diff = MatrixDiff(with, without, 3);
                measured = diff.Count > 0;
                amount = diff.Count > 0 ? string.Join(", ", diff.ToArray()) : "no tally moved";
            }

            reading = (local.Kind == MatrixRuleKind.Basic || local.Kind == MatrixRuleKind.Active
                          ? fires + " cast(s)" : fires + " firing(s)")
                      + ", " + amount
                      + (same ? "" : "; SAME SEED TWICE DIFFERED")
                      + (moved ? "" : "; the control fought the identical fight");
            return same && fired && moved && measured;
        }

        /// <summary>The rule, pointed at the matrix arena's copy of its hero rather than the sweep's.</summary>
        private static MatrixRule RebindRule(MatrixRule rule, HeroDef subject)
        {
            MatrixRule r = new MatrixRule();
            r.Hero = subject; r.Kind = rule.Kind; r.Index = rule.Index; r.Id = rule.Id; r.Name = rule.Name;
            r.Triggered = rule.Triggered;
            r.Passive = rule.Kind == MatrixRuleKind.Passive ? subject.Passives[rule.Index]
                      : rule.Kind == MatrixRuleKind.Talent ? subject.TalentAt(rule.Index) : null;
            return r;
        }

        /// <summary>The tallies that differ between two readings, biggest relative move first.</summary>
        private static List<string> MatrixDiff(MatrixReading with, MatrixReading without, int max)
        {
            List<KeyValuePair<string, double>> moves = new List<KeyValuePair<string, double>>();
            Dictionary<string, bool> keys = new Dictionary<string, bool>();
            foreach (string k in with.Tally.Keys) keys[k] = true;
            foreach (string k in without.Tally.Keys) keys[k] = true;
            foreach (string k in keys.Keys)
            {
                if (k == "firings" || k == "casts") continue;
                long a = with.Get(k), b = without.Get(k);
                if (a == b) continue;
                double rel = Math.Abs(a - b) / (double)Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));
                moves.Add(new KeyValuePair<string, double>(k, rel));
            }
            moves.Sort(delegate (KeyValuePair<string, double> x, KeyValuePair<string, double> y)
            {
                int c = y.Value.CompareTo(x.Value);
                return c != 0 ? c : string.CompareOrdinal(x.Key, y.Key);
            });
            List<string> outp = new List<string>();
            for (int i = 0; i < moves.Count && i < max; i++)
                outp.Add(moves[i].Key + " " + with.Get(moves[i].Key) + " against " + without.Get(moves[i].Key));
            return outp;
        }

        /// <summary>
        /// Every room the matrix fields: the pre-existing THE DARK TALENTS rooms, read under the
        /// matrix's own pattern with no measurement named (so any tally beyond the rule's own
        /// firings has to move against the control), followed by the rooms this file adds.
        /// </summary>
        private static List<MatrixRoom> AllMatrixRooms()
        {
            List<MatrixRoom> rooms = new List<MatrixRoom>();
            for (int i = 0; i < DarkTalentRooms.Length; i++)
            {
                TalentRoom t = DarkTalentRooms[i];
                rooms.Add(new MatrixRoom(t.Pair, t.Stage, t.Allies, t.Seed, t.Slow, null, 0,
                                         "THE DARK TALENTS room"));
            }
            rooms.AddRange(MatrixRooms);
            return rooms;
        }

        private const string MxGuardSquad = "eira_the_rolling_barricade,isara_the_second_chance";
        private const string MxPair = "brenna_the_remaining_wall,kael_the_unyielding";
        private const string MxMender = "isara_the_second_chance";

        /// <summary>
        /// THE ROOMS THIS FILE ADDS, one per rule the sweep leaves dark, each the smallest room the
        /// search found that raises the rule's own gate in a lineup a player can field. The measure
        /// is the tally the rule's OWN SENTENCE promises - a Mark removed, a Resolve banked, an
        /// Energy payment of the exact size it names - and the direction is the one that sentence
        /// gives. "Any tally moved" is not accepted here: that is the search's bar, and the search
        /// is not a check.
        /// </summary>
        private static readonly MatrixRoom[] MatrixRooms = {
            // ---- the eighteen shipped passives the sweep never fires (step 2 of the brief)
            new MatrixRoom("kiva_broken_links", "s_buffer", "", 1, false, "applied:Resolve", 1,
                "a Forsaken enemy that buffs itself every turn - the sweep's buffs land on bodies Kiva has not marked"),
            new MatrixRoom("tavia_dead_channel", "s_buffer", "", 1, false, "hits", 1,
                "a marked enemy gaining a buff; the strike is counted, its size moves with the fight"),
            new MatrixRoom("vanno_wrong_turn_misdirect", "s_buffer", "", 1, false, "spreads", 1,
                "a Misdirected enemy gaining a buff, so there is a buff to move"),
            new MatrixRoom("merek_anticipated", "s_caster_tail", "", 1, false, "shift.enemy", 1,
                "an enemy that CASTS - the sweep's five enemies have no active at all"),
            new MatrixRoom("merek_priority_inversion", "s_caster_tail", MxPair, 1, false, "shift.ally", 1,
                "an enemy that casts while carrying First Claim, and allies whose turn can still be advanced"),
            new MatrixRoom("kest_no_way_back_finish", "m_buffer_brittle", MxMender, 2, false, "hits", 1,
                "an enemy that dies DURING No Way Back - a body that survives the first blow and not the cast"),
            new MatrixRoom("maera_keep_the_beacon_relief", "m_buffer_brittle", "", 1, false, "heal.self", 1,
                "an enemy that dies during Keep the Beacon; the relief heals every ally, Maera included"),
            new MatrixRoom("iolan_missing_precedent", "s_caster_tail", "", 1, false, "removed:AttackUp", 1,
                "an Unlisted enemy that finishes a cast holding a buff to strike from the record"),
            new MatrixRoom("nadine_proper_place", "m_hider_tail", "", 1, false, "hits", 1,
                "an enemy that hides ITSELF - t_cloaker hands Stealth to the front hero, which is the wrong side"),
            new MatrixRoom("cal_new_sightline", "s_frail_big", MxGuardSquad, 1, false, "applied:GuaranteedCrit", 1,
                "Cal landing the kill on an Offset body, which needs a body that dies to him and not before"),
            new MatrixRoom("rusk_borrowed_time", "s_cloaker_armored", MxPair, 1, false, "removed:DefenseUp", 1,
                "a lethal blow on an ally carrying Patchwork: the save spends it"),
            new MatrixRoom("roan_leave_before_asked", "m_buffer_brittle", "", 2, false, "applied:Stealth", 1,
                "Roan killing an Off Balance body - the sweep kills Roan in round 3"),
            new MatrixRoom("piri_cheap_kindling", "s_mender", MxMender, 3, false, "payments.ally:+15", 1,
                "a kill that is Piri's, paying the emptiest ally exactly the 15 Energy the sentence names"),
            new MatrixRoom("garron_set_iron", "m_hexer", "", 1, false, "heal.self", 1,
                "Garron at the front of a line that strikes the FRONT body, so he is hit and blocks"),
            new MatrixRoom("nara_carry_on", "s_wash", MxMender, 1, false, "applied:DamageReductionUp", 1,
                "an ally still under 35% after Nara heals it - a line that hits everyone hard"),
            new MatrixRoom("vaelis_departure", "m_buffer_brittle", MxMender, 1, false, "applied:Resolve", 1,
                "an ALLY striking the body Departure is bound to; the sweep's fillers never reach it"),

            // ---- the rest of the kit rules the sweep leaves dark
            new MatrixRoom("aeron_momentum_breaker", "s_caster_tail", MxMender, 1, false, "energy.enemy-", 1,
                "an enemy carrying Turn Debt that charges an active"),
            new MatrixRoom("aminah_emergency_release", "m_fastbruiser", MxGuardSquad, 1, false, "removed:Ledger", 1,
                "an ally holding Priority struck below 30% by a blow fast enough to land before it is topped up"),
            new MatrixRoom("arden_everything_has_a_limit", "m_hider_tail", "", 1, false, "damage", 1,
                "a True Measure body alive long enough for the recorded band to be read by his active"),
            new MatrixRoom("arden_everything_has_a_limit_active", "m_hider_tail", "", 3, false, "damage", 1,
                "the same, read from the active's side"),
            new MatrixRoom("arno_rubble_resolve", "m_warder_tail", "", 1, false, "applied:AttackUp", 1,
                "an enemy carrying a Ward big enough to survive the blow - no sweep enemy Wards at all"),
            new MatrixRoom("aster_stress_line", "m_buffer_brittle", MxMender, 2, false, "energy.enemy-", 1,
                "a Fractured body dying with others still standing to lose Energy"),
            new MatrixRoom("brenna_survival_of_the_stubborn", "s_bruiser", MxGuardSquad, 1, false, "removed:Guard", 1,
                "a lethal blow on an ally holding a Guard that OUTLIVES the blow - see the note on Brenna III"),
            new MatrixRoom("dahlia_broken_rhythm_answer", "s_caster_tail", "", 1, false, "hits", 1,
                "a Displaced enemy that tries to cast"),
            new MatrixRoom("dahlia_scattered_choir_silence", "s_caster_tail", "", 1, false, "cancels", 1,
                "a Displaced enemy whose cast can be cancelled"),
            new MatrixRoom("darian_the_cost_of_knowing", "m_fastbruiser", MxPair, 1, true, "payments.subject:+10", 1,
                "Hidden Strength filling to its cap, which needs Darian's Ward to absorb enough"),
            new MatrixRoom("dett_turn_with_it", "s_caster_tail", "", 1, false, "applied:DamageReductionUp", 1,
                "an enemy ACTIVE aimed at Dett; the sweep's enemies have none"),
            new MatrixRoom("elin_tighten_the_thread", "m_hexer", "", 1, false, "payments.subject:+10", 1,
                "Stitching running its full course on a body whose bar is not already full"),
            new MatrixRoom("hale_no_safe_distance", "m_warder_tail", "", 1, false, "removed:Mark", 1,
                "a Quarry body gaining a Ward"),
            new MatrixRoom("hale_no_safe_distance_unseen", "m_hider_tail", "", 1, false, "removed:Mark", 1,
                "a Quarry body making itself Untargetable"),
            new MatrixRoom("isolde_unfinished_sentence", "m_hexer", "", 1, false, "hits", 1,
                "a Revealed body her own damage over time is ticking on, alive for a whole fight"),
            new MatrixRoom("kael_unusable_cover", "m_brittle3_big", MxMender, 3, false, "shield.self", 1,
                "an ally ending its turn with a Ward while Kael holds Rubble"),
            new MatrixRoom("kiva_severance_paid", "s_buffer", "", 1, false, "applied:HealSuppression", 1,
                "Broken Links to spend, which needs a Forsaken enemy that buffs"),
            new MatrixRoom("leon_no_unfiled_death", "m_buffer_brittle", "", 1, false, "applied:Mark", 1,
                "a Case Number body dying with a higher-Attack enemy left to list"),
            new MatrixRoom("mireth_absence_has_weight", "s_buffer", "", 1, false, "applied:Resolve", 1,
                "Mireth stripping a buff off a marked enemy"),
            new MatrixRoom("mireth_the_missing_page", "s_buffer", "", 1, false, "removed:AttackUp", 1,
                "a marked enemy gaining a buff for The Missing Page to take"),
            new MatrixRoom("niko_ruin_the_agenda", "m_hexer", "", 1, false, "applied:Silence", 1,
                "a Flustered enemy that applies a debuff - the sweep's Burn lands on unmarked bodies"),
            new MatrixRoom("rheia_adaptive_reasoning", "s_selfdown", "", 1, false, "payments.subject:+5", 1,
                "an enemy for Rheia's rewrite to hand a buff to"),
            new MatrixRoom("rheia_second_direction", "s_selfdown", "", 1, false, "payments.subject:+10", 1,
                "an enemy with Guard Down gaining a buff from Rheia"),
            new MatrixRoom("roan_through_the_service_door", "m_hexer", "", 1, false, "damage", 1,
                "Roan alive at a full bar - he dies in round 3 of the sweep"),
            new MatrixRoom("rovan_return_the_guard", "m_hexer", MxPair, 1, false, "shield.self", 1,
                "Covered expiring on an ally who still holds the Ward Rovan passed"),
            new MatrixRoom("seraphine_contrary_evidence", "s_buffer", "", 1, false, "hits", 1,
                "a Footnoted enemy that buffs itself every turn"),
            new MatrixRoom("seraphine_volume_closed", "m_hexer", "", 1, false, "applied:BuffSuppression", 1,
                "a Footnote record that reaches its cap on a body that lives long enough"),
            new MatrixRoom("sylven_persistent_edge", "m_buffer_brittle", "", 1, false, "applied:Ledger", 1,
                "a Scar Record body dying with a higher-HP enemy left to inherit it"),
            new MatrixRoom("sylven_wounds_remember", "s_buffer", "", 1, false, "removed:Ledger", 1,
                "an enemy that gains the SAME buff twice, so the Record has something to remember"),
            new MatrixRoom("vanno_exit_unmarked", "m_buffer_brittle", "", 1, false, "applied:Stealth", 1,
                "a Misdirected body dying"),
            new MatrixRoom("velis_no_second_breath", "s_mender", "", 1, false, "payments.subject:+10", 1,
                "an enemy healed below half HP"),
            // Seed 5, not 1: once Denied Return's second strike read the enemy's HP rather than
            // Velis's own, the seed-1 fight ends a round sooner and the one firing's blows are
            // absorbed into a shorter fight - it still fires, and still changes the fight.
            new MatrixRoom("velis_what_was_taken", "s_mender", "", 5, false, "hits", 1,
                "Remnants reaching three, which needs Hollowed heals to convert"),
            new MatrixRoom("kora_first_opening", "s_frail_big", "", 1, false, "hits", 1,
                "an enemy that has not acted yet when Kora's basic lands - t_big is slower than she is"),
            new MatrixRoom("zella_reaction_study", "s_caster_tail", "", 1, false, "payments.enemy:-10", 1,
                "an enemy that CHANGES its kind of action - one that alternates basic and active"),
            new MatrixRoom("zella_nothing_stays_settled", "m_buffer_brittle", "", 1, false, "applied:Burn", 1,
                "a Residue body dying with a higher-HP enemy left to inherit it"),

            // ---- the talents the sweep and THE DARK TALENTS leave dark
            new MatrixRoom("aminah_the_true_measure III", "m_fastbruiser", MxGuardSquad, 1, false, "lost:Resolve", 1,
                "No Wasted Recovery releasing on an ally, so Perfect Timing can spend every Measure"),
            new MatrixRoom("brenna_the_remaining_wall III", "s_bruiser", MxGuardSquad, 1, false, "applied:Resolve", 1,
                "a Survival of the Stubborn save, which needs a Guard that outlives the blow"),
            new MatrixRoom("caldus_the_appointed_guard III", "s_mender", MxMender, 1, false, "applied:Resolve", 1,
                "the Declared Target falling under 30% while still Declared - a body that CAN fall"),
            new MatrixRoom("darian_the_hidden_bulwark III", "m_hexer", "dahlia_the_wayward_hymn", 1, false, "applied:Resolve2", 1,
                "a Ward Darian gave running out while his own still stands - an ally who acts BEFORE him"),
            new MatrixRoom("isolde_the_slow_fading I", "m_hexer", "", 1, false, "payments.subject:+10", 1,
                "a Cinder of hers running its full course on a body that lives"),
            new MatrixRoom("merek_the_sequence_thief III", "s_soft_big", "", 1, false, "energy.enemy-", 1,
                "First Claim dying before it has acted that round"),
            new MatrixRoom("nysera_the_missing_arrow III", "s_soft_big", "", 1, false, "applied:ArmorBreakUp", 1,
                "a kill made while Last Known Position's Armor Break Up is up, so the talent's own trigger fires"),
            new MatrixRoom("talren_the_wayfinder III", "s_soft_mid_big", "", 1, false, "hits", 1,
                "recorded enemies standing when Route pays out, so the strikes have bodies to land on"),
            new MatrixRoom("velis_the_hollow_vow II", "s_mender", "", 1, false, "energy.enemy-", 1,
                "Hollowed converting an enemy heal"),
            new MatrixRoom("zella_the_slow_argument III", "s_caster_tail", "", 1, false, "applied:Focus", 1,
                "an enemy that changes its kind of action while Residue ticks on it"),
        };

        // ------------------------------------------------------------------ what the rest wait on

        /// <summary>
        /// A rule no room raises, and WHY, in one of three kinds: AUTHORING (the clause as written
        /// cannot do what its sentence says in any arena - a finding for Pinoy, not an arena to
        /// build), ENGINE (the clause is right and the engine drops it - a finding for whoever owns
        /// CombatSimulation) or UNRAISED (no room found, cause not established). Where the event
        /// list can show the reason, Evidence measures it on this build, so the stated reason is
        /// re-proved every run rather than remembered.
        /// </summary>
        private sealed class MatrixWait
        {
            public readonly string Rule, Kind, Why;
            public readonly Func<string> Evidence;
            public MatrixWait(string rule, string kind, string why, Func<string> evidence)
            { Rule = rule; Kind = kind; Why = why; Evidence = evidence; }
        }

        private static readonly MatrixWait[] MatrixWaits = {
            new MatrixWait("aeron_the_unbroken_stride I", "AUTHORING",
                "Interest Owed asks for an enemy ENDING its turn with Turn Debt, and the engine repays"
                + " and removes every Turn Debt entry as that turn OPENS (RepayTurnDebt, before the"
                + " TurnStart passives); only Aeron's own basic and active bank debt, and he never acts"
                + " inside an enemy's turn", EvidenceInterestOwed),
            new MatrixWait("finn_ringing_toll", "AUTHORING",
                "the toll answers OnEnemyActive, which TakeTurn raises after it has set the caster's"
                + " Energy to 0 for the cast, so its -5 always lands on an empty bar. NO ORDER MENDS IT:"
                + " every active costs 100 against a bar capped at 100, so a cast empties the bar whatever"
                + " was taken first. Raised ahead of the spend (measured, not shipped) the toll moves Energy"
                + " on 0 empty bars of 5 and changes nothing across 1,152 campaign battles - deaths, rounds"
                + " and Energy lost identical, only the extra Energy event differs", EvidenceRingingToll),
            new MatrixWait("eira_leave_no_corner", "UNRAISED",
                "each of its two gates lands alone and the pair never has: in the one blow of"
                + " m_fastbruiser where an un-acted ally held Mobile Cover, IncomingHitIsSingleTarget"
                + " answered no for a single-target basic. Cause not established; 96 rooms tried",
                EvidenceLeaveNoCorner),
            new MatrixWait("vaelis_opening_moves", "UNRAISED",
                "no enemy carrying Departure died in 360 rooms (8 stages, 9 squads, 5 seeds): the link"
                + " goes on the PREVIOUS target when his lowest-HP basic changes target, which it"
                + " mostly does because that target died, and an ally's hit on a linked body spends"
                + " the link before any killing blow can find it. Reachability is a question for Pinoy",
                null),
        };

        private static MatrixWait MatrixWaitFor(string rule)
        {
            for (int i = 0; i < MatrixWaits.Length; i++) if (MatrixWaits[i].Rule == rule) return MatrixWaits[i];
            return null;
        }

        /// <summary>
        /// A hero's rule counted by what it LANDS: a tally with the passive in place against the
        /// same fight with it ablated, for one variant of the hero. What the four pieces of evidence
        /// below are built from.
        /// </summary>
        private static long MatrixLanded(HeroDef variant, string passiveId, string stage, string allies,
                                         int seed, string key)
        {
            ContentDatabase db = MatrixArena(false);
            int idx = -1;
            for (int i = 0; i < variant.Passives.Count; i++) if (variant.Passives[i].Id == passiveId) idx = i;
            if (db == null || idx < 0) return -1;
            MatrixRule r = new MatrixRule();
            r.Hero = variant; r.Kind = MatrixRuleKind.Passive; r.Index = idx; r.Id = passiveId; r.Name = passiveId;
            r.Passive = variant.Passives[idx];
            List<HeroDef> squad = MatrixSquad(db, allies, variant);
            MatrixReading with = MatrixFight(db, db.GetStage(stage), r, false, squad, seed);
            MatrixReading without = MatrixFight(db, db.GetStage(stage), r, true, squad, seed);
            return with.Get(key) - without.Get(key);
        }

        private static HeroDef MatrixVariant(HeroDef def, string passiveId, Func<PassiveDef, PassiveDef> edit)
        {
            HeroDef clone = (HeroDef)_memberwiseClone.Invoke(def, null);
            List<PassiveDef> list = new List<PassiveDef>(def.Passives);
            for (int i = 0; i < list.Count; i++) if (list[i].Id == passiveId) list[i] = edit(list[i]);
            _passivesField.SetValue(clone, list);
            return clone;
        }

        private static PassiveDef MatrixCopyPassive(PassiveDef p, Func<EffectDef, EffectDef> edit)
        {
            PassiveDef q = new PassiveDef();
            q.Id = p.Id; q.SlotKey = p.SlotKey; q.Name = p.Name; q.Trigger = p.Trigger;
            q.OncePerBattle = p.OncePerBattle; q.Limit = p.Limit; q.Threshold = p.Threshold;
            for (int i = 0; i < p.Effects.Count; i++) q.Effects.Add(edit(p.Effects[i].Clone()));
            return q;
        }

        private static string EvidenceLeaveNoCorner()
        {
            ContentDatabase db = MatrixArena(false);
            HeroDef eira = db == null ? null : db.GetHero("eira_the_rolling_barricade");
            if (eira == null) return "Eira not found";
            const string squad = DarkStrikers;
            long both = MatrixLanded(eira, "eira_leave_no_corner", "m_fastbruiser", squad, 1, "shift.by.subject");
            long guardOnly = MatrixLanded(MatrixVariant(eira, "eira_leave_no_corner",
                delegate (PassiveDef p) { return MatrixCopyPassive(p, delegate (EffectDef e)
                    { e.Condition2 = EffectCondition.None; return e; }); }),
                "eira_leave_no_corner", "m_fastbruiser", squad, 1, "shift.by.subject");
            long singleOnly = MatrixLanded(MatrixVariant(eira, "eira_leave_no_corner",
                delegate (PassiveDef p) { return MatrixCopyPassive(p, delegate (EffectDef e)
                    { e.Condition = e.Condition2; e.ConditionOn = e.ConditionOn2; e.Condition2 = EffectCondition.None; return e; }); }),
                "eira_leave_no_corner", "m_fastbruiser", squad, 1, "shift.by.subject");
            return "in m_fastbruiser beside the strikers Eira advances an ally " + both + " time(s) as shipped, "
                   + guardOnly + " with only the Mobile Cover gate, " + singleOnly + " with only the single-target gate";
        }

        /// <summary>Walks one battle's events keeping each unit's Energy, and at every firing of
        /// the toll reads the bar of the enemy whose cast raised it.</summary>
        private static string EvidenceRingingToll()
        {
            BattleResult r = MatrixSoloBattle("finn_the_bellmaker", "s_caster_tail");
            if (r == null) return "Finn not found";
            Dictionary<int, int> energy = new Dictionary<int, int>();
            foreach (UnitSnapshot u in r.Units) energy[u.Id] = u.StartEnergy;
            int lastCaster = -1, firings = 0, atEmpty = 0, moved = 0;
            for (int i = 0; i < r.Events.Count; i++)
            {
                BattleEvent e = r.Events[i];
                if (e.Type == BattleEventType.EnergyChanged) energy[e.TargetId] = e.Value;
                if (e.Type == BattleEventType.AbilityCast) { UnitSnapshot c = r.FindUnit(e.SourceId); lastCaster = c != null && !c.IsPlayer ? c.Id : -1; }
                if (e.Type == BattleEventType.PassiveTrigger && e.AbilityId == "finn_ringing_toll")
                {
                    firings++;
                    int bar; energy.TryGetValue(lastCaster, out bar);
                    if (lastCaster >= 0 && bar == 0) atEmpty++;
                    // Anything the toll moved is the next event on the caster's bar.
                    if (i + 1 < r.Events.Count && r.Events[i + 1].Type == BattleEventType.EnergyChanged
                        && r.Events[i + 1].TargetId == lastCaster) moved++;
                }
            }
            return "in s_caster_tail it fires " + firings + " time(s), " + atEmpty + " of them on a caster whose bar"
                   + " reads 0, and moves Energy " + moved + " time(s)";
        }

        private static string EvidenceInterestOwed()
        {
            ContentDatabase db = MatrixArena(false);
            HeroDef aeron = db == null ? null : db.GetHero("aeron_the_unbroken_stride");
            if (aeron == null) return "Aeron not found";
            List<HeroInstance> team = new List<HeroInstance>();
            HeroInstance me = MakeInstance(aeron, 40); me.SelectedTalent = 1; team.Add(me);
            team.Add(MakeInstance(db.GetHero("isara_the_second_chance"), 40));
            BattleResult r = RunWith(db, db.GetStage("s_caster_tail"), 1, team);
            Dictionary<int, int> debt = new Dictionary<int, int>();
            int turn = -1, firings = 0, owing = 0;
            PassiveDef talent = aeron.TalentAt(1);
            foreach (BattleEvent e in r.Events)
            {
                if (e.Type == BattleEventType.TurnStart) turn = e.SourceId;
                if (e.Type == BattleEventType.StatusApplied && e.Status == StatusKind.TurnDebt) debt[e.TargetId] = 1;
                if (e.Type == BattleEventType.StatusExpired && e.Status == StatusKind.TurnDebt) debt.Remove(e.TargetId);
                if (e.Type == BattleEventType.PassiveTrigger && talent != null && e.AbilityId == talent.Id)
                { firings++; if (debt.ContainsKey(turn)) owing++; }
            }
            return "in s_caster_tail beside Isara it fires " + firings + " time(s) at an enemy's turn end and"
                   + " finds Turn Debt on that enemy " + owing + " time(s)";
        }

        private static BattleResult MatrixSoloBattle(string heroId, string stage)
        {
            ContentDatabase db = MatrixArena(false);
            HeroDef def = db == null ? null : db.GetHero(heroId);
            if (def == null) return null;
            List<HeroInstance> team = new List<HeroInstance>();
            team.Add(MakeInstance(def, 40));
            return RunWith(db, db.GetStage(stage), 1, team);
        }

        // ------------------------------------------------------------------ the search

        private static readonly string[] MatrixSearchSquads = {
            "", "isara_the_second_chance", "eira_the_rolling_barricade,isara_the_second_chance",
            DarkPairUp, DarkWall, "isara_the_second_chance,kael_the_unyielding,darian_the_hidden_bulwark",
            DarkStrikers, DarkFive };

        /// <summary>
        /// NOT A CHECK. Set EMBERFALL_MATRIX_SEARCH=1 and every rule the sweep leaves dark and no
        /// room covers is tried in every stage of the matrix arena, beside every squad above, at
        /// three seeds, smallest room first - and the first room that passes the full house pattern
        /// is printed as a registry line. A human reads it, names the measurement the rule's own
        /// text promises, and commits it; the search itself never writes anything.
        /// </summary>
        private static void SearchMatrixRooms(List<MatrixRule> rules)
        {
            ContentDatabase db = MatrixArena(false);
            if (db == null) return;
            List<StageDef> stages = new List<StageDef>(db.AllStages);
            List<KeyValuePair<int, string[]>> order = new List<KeyValuePair<int, string[]>>();
            for (int st = 0; st < stages.Count; st++)
                for (int sq = 0; sq < MatrixSearchSquads.Length; sq++)
                {
                    int allies = MatrixSearchSquads[sq].Length == 0 ? 0 : MatrixSearchSquads[sq].Split(',').Length;
                    order.Add(new KeyValuePair<int, string[]>(stages[st].Enemies.Count + allies,
                              new string[] { stages[st].Id, MatrixSearchSquads[sq], sq.ToString() }));
                }
            order.Sort(delegate (KeyValuePair<int, string[]> x, KeyValuePair<int, string[]> y)
            {
                int c = x.Key.CompareTo(y.Key);
                return c != 0 ? c : string.CompareOrdinal(x.Value[0] + x.Value[2], y.Value[0] + y.Value[2]);
            });

            for (int i = 0; i < rules.Count; i++)
            {
                MatrixRule rule = rules[i];
                if (rule.InSweep || rule.RoomPassed) continue;
                string found = null;
                for (int o = 0; o < order.Count && found == null; o++)
                    for (int slow = 0; slow < 2 && found == null; slow++)
                        for (int seed = 1; seed <= 3 && found == null; seed++)
                        {
                            MatrixRoom room = new MatrixRoom(rule.Name, order[o].Value[0], order[o].Value[1],
                                                             seed, slow == 1, null, 0, "");
                            string reading;
                            if (EvaluateMatrixRoom(rule, room, out reading))
                                found = "new MatrixRoom(\"" + rule.Name + "\", \"" + room.Stage + "\", \""
                                        + room.Allies + "\", " + seed + ", " + (slow == 1 ? "true" : "false")
                                        + ", ...)   // " + reading;
                        }
                Line("  [search] " + (found ?? rule.Name + " - no room in the matrix raises it"));
            }
        }

        /// <summary>
        /// NOT A CHECK either. EMBERFALL_MATRIX_PROBE="rule|stage|allies|seed|slow|measure;..." runs
        /// each named room under the full house pattern and prints its reading, pass or fail, with
        /// every tally that moved. It is how a room's measurement is chosen before it is committed.
        /// </summary>
        private static void ProbeMatrixRooms(List<MatrixRule> rules, string spec)
        {
            if (string.IsNullOrEmpty(spec)) return;
            if (spec[0] == '@') spec = System.IO.File.ReadAllText(spec.Substring(1)).Trim();
            string[] probes = spec.Split(';');
            for (int p = 0; p < probes.Length; p++)
            {
                string[] f = probes[p].Split('|');
                if (f.Length < 4) continue;
                MatrixRule rule = null;
                for (int i = 0; i < rules.Count && rule == null; i++) if (rules[i].Name == f[0]) rule = rules[i];
                if (rule == null) { Line("  [probe] no rule " + f[0]); continue; }
                bool slow = f.Length > 4 && f[4] == "slow";
                string measure = f.Length > 5 ? f[5] : null;
                MatrixRoom room = new MatrixRoom(f[0], f[1], f[2], int.Parse(f[3]), slow, measure, 0, "");
                string reading;
                bool ok = EvaluateMatrixRoom(rule, room, out reading);
                ContentDatabase db = MatrixArena(slow);
                HeroDef subject = db.GetHero(rule.Hero.Id);
                MatrixRule local = RebindRule(rule, subject);
                List<HeroDef> squad = MatrixSquad(db, f[2], subject);
                string all = "";
                if (squad != null && db.GetStage(f[1]) != null)
                {
                    MatrixReading w = MatrixFight(db, db.GetStage(f[1]), local, false, squad, room.Seed);
                    MatrixReading wo = MatrixFight(db, db.GetStage(f[1]), local, true, squad, room.Seed);
                    all = string.Join("; ", MatrixDiff(w, wo, 40).ToArray());
                    all += " | firings " + w.Get("firings") + " rounds " + w.Get("rounds") + " deaths.enemy "
                           + w.Get("deaths.enemy") + " deaths.ally " + w.Get("deaths.ally");
                }
                Line("  [probe] " + (ok ? "PASS " : "fail ") + probes[p] + " -> " + reading);
                Line("          " + all);
            }
        }

        // ------------------------------------------------------------------ normal play

        /// <summary>
        /// NOT A CHECK. EMBERFALL_MATRIX_NORMAL=pinned (or =balance, or =both) reads every rule the
        /// matrix covers ONLY through a purpose-built room - class (b) - in a line a player actually
        /// fields, across every shipped campaign stage. Class (b) is a pass for coverage and says
        /// nothing about whether a playtester will ever see the rule: the room was built to raise it.
        /// This is the other half of that sentence.
        ///
        /// THE LINE is PinnedLine or the balance line, with the rule's hero in the place of the body
        /// that shares its role, or in the last place when none does; a hero already in the line
        /// stays where it is. It is levelled and geared to each stage's recommendedPower the way
        /// BuildPinnedLineAt levels the pinned line, and fights the stage's own generated battles at
        /// wave n battle n for n = 1..12, at NormalPlaySeeds seeds each. Every other hero fights with
        /// no talent picked, as a fresh profile does. The rule is ablated exactly as the matrix
        /// ablates it, and a battle counts as changed when the fight without the rule differs with
        /// the rule's own announcement excluded.
        /// </summary>
        private const int NormalPlaySeeds = 2;

        private static readonly string[] NormalPlayBalanceLine = {
            "kael_the_unyielding", "kiva_the_forsaken_oath", "isara_the_second_chance",
            "adric_the_blank_map", "orelle_the_fever_warden" };

        private static string[] NormalPlayLineFor(ContentDatabase db, string[] basis, HeroDef hero, out string how)
        {
            string[] line = (string[])basis.Clone();
            for (int i = 0; i < line.Length; i++)
                if (line[i] == hero.Id) { how = "already in the line"; return line; }
            for (int i = 0; i < line.Length; i++)
            {
                HeroDef p = db.GetHero(line[i]);
                if (p != null && p.Role == hero.Role) { how = "for " + p.Id; line[i] = hero.Id; return line; }
            }
            how = "for " + line[line.Length - 1] + ", no " + hero.Role + " in the line";
            line[line.Length - 1] = hero.Id;
            return line;
        }

        private static GameSession NormalPlaySession(ContentDatabase db, StageDef stage, string[] line)
        {
            PlayerProfile profile = GameSession.CreateNewProfile(db, 0);
            profile.PlayerLevel = 60;
            profile.Gold = 500000000L;
            GameSession session = new GameSession(db, profile);
            for (int h = 0; h < db.Heroes.Count; h++)
                if (!profile.Owns(db.Heroes[h].Id)) session.GrantHeroDirect(db.Heroes[h], 0);
            for (int i = 0; i < profile.Team.Length; i++) profile.ClearTeamSlot(i);
            int assigned = 0;
            for (int i = 0; i < line.Length; i++)
                for (int j = 0; j < profile.Heroes.Count; j++)
                {
                    if (profile.Heroes[j].Def.Id != line[i]) continue;
                    profile.AssignToTeam(assigned, profile.Heroes[j].Uid);
                    assigned++;
                    break;
                }
            EquipInstrumentArmor(db, session, stage);
            List<HeroInstance> team = profile.TeamHeroes();
            int guard = 0;
            while (guard < 20000 && profile.EffectiveTeamPower(db) < stage.RecommendedPower
                   && InstrumentStep(session, team)) guard++;
            return session;
        }

        /// <summary>One battle of the session's line with the rule present or ablated.</summary>
        private static MatrixReading NormalPlayFight(GameSession session, StageDef fought, MatrixRule rule,
                                                     bool ablated, int seed)
        {
            BattleRequest request = session.BuildBattleRequest(fought, seed);
            List<HeroInstance> team = new List<HeroInstance>();
            for (int i = 0; i < request.Team.Count; i++)
            {
                HeroInstance h = request.Team[i];
                if (h == null) { team.Add(null); continue; }
                HeroInstance c = (HeroInstance)_memberwiseClone.Invoke(h, null);
                c.SelectedTalent = 0;
                if (h.Def != null && h.Def.Id == rule.Hero.Id)
                {
                    c.Def = ablated ? MatrixAblate(rule) : rule.Hero;
                    c.SelectedTalent = rule.Kind == MatrixRuleKind.Talent && !ablated ? rule.Index : 0;
                }
                team.Add(c);
            }
            request.Team = team;
            BattleResult r = CombatSimulation.Run(request);
            int subject = -1;
            for (int i = 0; i < r.Units.Count; i++)
                if (r.Units[i].IsPlayer && r.Units[i].DefId == rule.Hero.Id) subject = r.Units[i].Id;
            return MatrixRead(r, rule, subject);
        }

        private static void ReportMatrixNormalPlay(ContentDatabase live, List<MatrixRule> rules, string which)
        {
            List<string[]> lines = new List<string[]>();
            List<string> names = new List<string>();
            if (which == "pinned" || which == "both") { lines.Add(PinnedLine); names.Add("pinned"); }
            if (which == "balance" || which == "both") { lines.Add(NormalPlayBalanceLine); names.Add("balance"); }
            if (lines.Count == 0) return;

            List<StageDef> stages = new List<StageDef>();
            for (int i = 0; i < live.AllStages.Count; i++)
                if (live.AllStages[i].TotalBattles > 1) stages.Add(live.AllStages[i]);
            Dictionary<string, GameSession> sessions = new Dictionary<string, GameSession>();

            // EMBERFALL_MATRIX_NORMAL_RULES="rule[@tally],..." reads the named rules instead of
            // class (b), whatever class they are in, and adds the named tally with the rule against
            // without it - the amount a rule's own sentence promises, beside whether it changed a
            // fight. How a dead clause is measured before and after an engine repair.
            Dictionary<string, string> named = null;
            string namedSpec = Environment.GetEnvironmentVariable("EMBERFALL_MATRIX_NORMAL_RULES");
            if (!string.IsNullOrEmpty(namedSpec))
            {
                named = new Dictionary<string, string>();
                foreach (string part in namedSpec.Split(','))
                {
                    int at = part.IndexOf('@');
                    named[(at < 0 ? part : part.Substring(0, at)).Trim()] = at < 0 ? null : part.Substring(at + 1).Trim();
                }
            }

            int covered = 0, changes = 0, firesOnly = 0, never = 0;
            List<string> neverNames = new List<string>(), firesOnlyNames = new List<string>();
            for (int k = 0; k < rules.Count; k++)
            {
                if (named != null ? !named.ContainsKey(rules[k].Name)
                                  : rules[k].InSweep || !rules[k].RoomPassed) continue;
                string tally = named != null ? named[rules[k].Name] : null;
                covered++;
                HeroDef hero = live.GetHero(rules[k].Hero.Id);
                MatrixRule rule = RebindRule(rules[k], hero);
                long fires = 0;
                int changed = 0, battles = 0;
                List<string> per = new List<string>();
                for (int l = 0; l < lines.Count; l++)
                {
                    string how;
                    string[] line = NormalPlayLineFor(live, lines[l], hero, out how);
                    long lf = 0, lt = 0, lw = 0;
                    int lc = 0, lb = 0;
                    for (int s = 0; s < stages.Count; s++)
                    {
                        string key = string.Join(",", line) + "@" + stages[s].Id;
                        GameSession session;
                        if (!sessions.TryGetValue(key, out session))
                        { session = NormalPlaySession(live, stages[s], line); sessions[key] = session; }
                        for (int n = 1; n <= 12; n++)
                            for (int seed = 1; seed <= NormalPlaySeeds; seed++)
                            {
                                StageDef fought = CampaignWaveGenerator.BuildBattle(stages[s], n, n);
                                int sd = 9100000 + stages[s].Index * 1000 + n * 53 + seed;
                                MatrixReading with = NormalPlayFight(session, fought, rule, false, sd);
                                MatrixReading without = NormalPlayFight(session, fought, rule, true, sd);
                                lb++;
                                lf += rule.Kind == MatrixRuleKind.Basic || rule.Kind == MatrixRuleKind.Active
                                      ? with.Get("casts") : with.Get("firings");
                                if (with.Hash != without.Hash) lc++;
                                if (tally != null) { lt += with.Get(tally) - without.Get(tally); lw += with.Get(tally); }
                            }
                    }
                    fires += lf; changed += lc; battles += lb;
                    per.Add(names[l] + " (" + how + "): fires " + lf + ", changes " + lc + "/" + lb
                            + (tally != null ? ", " + tally + " " + lw + " with it, " + (lt >= 0 ? "+" : "") + lt
                                              + " against without" : ""));
                }
                // A rule with no trigger of its own - a talent that rewrites its host - has nothing to
                // announce, so for it "fires" is the same statement as "changes".
                bool fired = fires > 0 || !rule.Triggered;
                string verdict;
                if (fired && changed > 0) { verdict = "fires and changes"; changes++; }
                else if (fires > 0) { verdict = "FIRES, CHANGES NOTHING"; firesOnly++; firesOnlyNames.Add(rule.Name); }
                else { verdict = "NEVER FIRES"; never++; neverNames.Add(rule.Name); }
                Line("  [normal] " + rule.Name + " - " + verdict + ": " + string.Join("; ", per.ToArray()));
            }
            Line("  [normal] " + covered + " rules covered only by a room: " + changes + " fire and change a fight in"
                 + " normal play, " + firesOnly + " fire and change nothing, " + never + " never fire");
            if (firesOnlyNames.Count > 0) Line("  [normal] fire and change nothing: " + string.Join(", ", firesOnlyNames.ToArray()));
            if (neverNames.Count > 0) Line("  [normal] never fire: " + string.Join(", ", neverNames.ToArray()));
        }

        // ------------------------------------------------------------------ the check

        /// <summary>
        /// The whole matrix, measured once per run and kept: the talent sweep's coverage check and
        /// this file's own check both read it, so they cannot disagree about what was observed.
        /// </summary>
        private static List<MatrixRule> MatrixResults(ContentDatabase live)
        {
            if (_matrixRules != null) return _matrixRules;
            List<MatrixRule> rules = MeasureMatrixSweep(live);
            if (rules == null) return null;

            List<MatrixRoom> rooms = AllMatrixRooms();
            for (int i = 0; i < rules.Count; i++)
            {
                MatrixRule r = rules[i];
                if (r.InSweep) continue;
                for (int k = 0; k < rooms.Count && !r.RoomPassed; k++)
                {
                    if (rooms[k].Rule != r.Name) continue;
                    string reading;
                    r.RoomPassed = EvaluateMatrixRoom(r, rooms[k], out reading);
                    r.Room = rooms[k].Stage + (rooms[k].Allies.Length > 0 ? " beside " + rooms[k].Allies : " alone")
                             + ", seed " + rooms[k].Seed + (rooms[k].Slow ? ", slow clock" : "");
                    r.RoomReading = reading;
                }
            }
            _matrixRules = rules;
            return rules;
        }

        /// <summary>
        /// What a rule observed nowhere is waiting on, in the most specific words available: its
        /// stated reason and that reason's evidence on this build when one is registered, the room
        /// that stopped proving it when one did, and otherwise the gate its own trigger or first
        /// condition names.
        /// </summary>
        private static string MatrixWaitsOn(MatrixRule r)
        {
            MatrixWait w = MatrixWaitFor(r.Name);
            if (w != null)
                return w.Kind + ": " + w.Why + (w.Evidence != null ? " [measured: " + w.Evidence() + "]" : "");
            if (r.Room != null) return "its room (" + r.Room + ") no longer proves it: " + r.RoomReading;
            if (r.Kind == MatrixRuleKind.Basic || r.Kind == MatrixRuleKind.Active) return "a room where it is cast";
            return TalentGate(r.Passive);
        }

        private static string MatrixDarkLine(MatrixRule r)
        {
            return r.Name + " (sweep: fires " + r.SweepFires + ", moved " + r.SweepMoved + "/" + HeroSweepSeeds
                   + ") waits on " + MatrixWaitsOn(r);
        }

        /// <summary>
        /// EVERY BASIC, ACTIVE AND PASSIVE IN THE ROSTER, OBSERVED SOMEWHERE - and the talents'
        /// half of the same assertion is the coverage check under AUTHORED-TALENT SWEEP, which reads
        /// the same matrix. Together they are the whole roster, and neither goes green on a rule
        /// that was not seen changing a fight: in the sweep arena, or in a named room that also
        /// proved its control differs and its seed repeats.
        ///
        /// It fails both ways that matter. A rule dark everywhere is named with what it waits on,
        /// so the red is a short list of findings rather than a number. And the bookkeeping is held
        /// to the same standard as the rules: a room or a stated reason naming a rule that does not
        /// exist, or a stated reason for a rule that IS now observed, has outlived what it was
        /// written about and fails until it is taken out.
        /// </summary>
        private static void CheckArenaMatrix(ContentDatabase live)
        {
            Line("THE ARENA MATRIX (every basic, active, passive and talent, ablated in the sweep arena and,");
            Line("                 where that cannot raise it, in the smallest named room that can)");

            List<MatrixRule> rules = MatrixResults(live);
            if (rules == null) { Check(false, "arena matrix: sweep arena built"); Line(""); return; }
            if (Environment.GetEnvironmentVariable("EMBERFALL_MATRIX_SEARCH") == "1") SearchMatrixRooms(rules);
            ProbeMatrixRooms(rules, Environment.GetEnvironmentVariable("EMBERFALL_MATRIX_PROBE"));
            ReportMatrixNormalPlay(live, rules, Environment.GetEnvironmentVariable("EMBERFALL_MATRIX_NORMAL"));

            int a = 0, b = 0, c = 0;
            Dictionary<MatrixRuleKind, int[]> byKind = new Dictionary<MatrixRuleKind, int[]>();
            foreach (MatrixRuleKind k in Enum.GetValues(typeof(MatrixRuleKind))) byKind[k] = new int[3];
            List<string> kitDark = new List<string>();
            Dictionary<string, bool> names = new Dictionary<string, bool>();
            for (int i = 0; i < rules.Count; i++)
            {
                MatrixRule r = rules[i];
                names[r.Name] = true;
                int cls = r.InSweep ? 0 : (r.RoomPassed ? 1 : 2);
                byKind[r.Kind][cls]++;
                if (cls == 0) a++; else if (cls == 1) b++; else c++;
                if (cls == 1) Line("  [room] " + r.Name + " - " + r.Room + ": " + r.RoomReading);
                if (cls == 2 && r.Kind != MatrixRuleKind.Talent) kitDark.Add(MatrixDarkLine(r));
            }
            Line("  [note] " + rules.Count + " rules: (a) " + a + " observed firing and changing the fight in the"
                 + " sweep arena, (b) " + b + " only in a named room, (c) " + c + " nowhere");
            foreach (KeyValuePair<MatrixRuleKind, int[]> kv in byKind)
                Line("  [note]   " + kv.Key.ToString().ToLowerInvariant() + "s: (a) " + kv.Value[0] + ", (b) "
                     + kv.Value[1] + ", (c) " + kv.Value[2]);

            Check(kitDark.Count == 0,
                  kitDark.Count == 0
                    ? "every basic, active and passive is observed changing a fight, in the sweep arena or a named room"
                    : kitDark.Count + " basic(s), active(s) or passive(s) are observed NOWHERE in the matrix, each with"
                      + " what it waits on: " + string.Join("; ", kitDark.ToArray()));

            List<string> orphans = new List<string>();
            List<MatrixRoom> rooms = AllMatrixRooms();
            for (int i = 0; i < rooms.Count; i++)
                if (!names.ContainsKey(rooms[i].Rule)) orphans.Add("room for " + rooms[i].Rule);
            List<string> stale = new List<string>();
            for (int i = 0; i < MatrixWaits.Length; i++)
            {
                if (!names.ContainsKey(MatrixWaits[i].Rule)) { orphans.Add("reason for " + MatrixWaits[i].Rule); continue; }
                for (int k = 0; k < rules.Count; k++)
                    if (rules[k].Name == MatrixWaits[i].Rule && (rules[k].InSweep || rules[k].RoomPassed))
                        stale.Add(MatrixWaits[i].Rule);
            }
            Check(orphans.Count == 0,
                  orphans.Count == 0
                    ? "every room and every stated reason names a rule the roster has"
                    : orphans.Count + " matrix entr(ies) name a rule the roster does not have: "
                      + string.Join(", ", orphans.ToArray()));
            Check(stale.Count == 0,
                  stale.Count == 0
                    ? "no stated reason outlives its rule's darkness - every rule with one is still observed nowhere"
                    : stale.Count + " rule(s) are observed now and still carry a reason for being dark - take the"
                      + " reason out: " + string.Join(", ", stale.ToArray()));

            // EVERY ROOM IS A LINEUP A PLAYER CAN FIELD, asserted rather than printed. This was a note
            // while Brenna I's room fielded two Brennas, and THE DARK TALENTS and TALENTS WORTH PICKING
            // both read her value off that fight - the matrix refused to count it and nothing else
            // did. The matrix's own rooms are held to the same rule by MatrixSquad as they run; this
            // holds the registry THE DARK TALENTS section and the value check read from.
            List<string> illegal = new List<string>();
            ContentDatabase legalDb = MatrixArena(false);
            for (int i = 0; i < DarkTalentRooms.Length; i++)
            {
                HeroDef subject = legalDb == null ? null : legalDb.GetHero(DarkTalentRooms[i].Pair.Split(' ')[0]);
                if (subject == null || MatrixSquad(legalDb, DarkTalentRooms[i].Allies, subject) == null)
                    illegal.Add(DarkTalentRooms[i].Pair + " in " + DarkTalentRooms[i].Stage + " beside "
                                + (DarkTalentRooms[i].Allies.Length > 0 ? DarkTalentRooms[i].Allies : "nobody"));
            }
            Check(illegal.Count == 0,
                  illegal.Count == 0
                    ? "every one of the " + DarkTalentRooms.Length + " rooms THE DARK TALENTS and TALENTS WORTH PICKING"
                      + " read is a lineup a player can field - no hero twice, no more than " + PlayerProfile.TeamSize
                    : illegal.Count + " room(s) THE DARK TALENTS and TALENTS WORTH PICKING read field a lineup"
                      + " PlayerProfile.AssignToTeam never allows (a hero twice, or more than " + PlayerProfile.TeamSize
                      + "): " + string.Join("; ", illegal.ToArray()));
            Line("");
        }
    }
}
