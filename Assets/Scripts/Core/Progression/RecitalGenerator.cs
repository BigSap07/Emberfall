using System;
using System.Collections.Generic;
using Emberfall.Core.Data;

namespace Emberfall.Core.Progression
{
    /// <summary>
    /// Turns a floor number into a fightable StageDef for The Deep Recital - procedural content,
    /// not hand-authored JSON like a campaign stage. A pure function of (content, floor): the same
    /// floor number always produces the same enemy lineup and level, deterministically, so
    /// "reattempt the current floor" means fighting the identical line again, not a re-roll. Only
    /// CombatSimulation's own seed varies attempt to attempt, exactly as it already does for a
    /// campaign stage replay.
    ///
    /// Confirmed before writing this: StatMath.ForEnemy(EnemyDef, int level) needed NO change to
    /// support floor scaling. It already turns a small template plus a level into full UnitStats,
    /// with no upper bound on level (unlike a hero, an enemy has no level cap anywhere in the
    /// simulation) - floor difficulty is expressed entirely through the level this class computes
    /// and hands to the existing EnemySpawn/CombatSimulation.BuildUnits path, unchanged. Nothing in
    /// CombatSimulation, StatMath or EnemySpawn was touched to build this mode.
    /// </summary>
    public static class RecitalGenerator
    {
        /// <summary>Ember, Tide, Verdant, Gale, Umbra, Lumen, repeat - the order named in the spec
        /// that added this mode, not DESIGN-DEEP-RECITAL.md's own illustrative band table (four
        /// uneven, non-repeating bands with mixed leans). See DESIGN-BUILD-ORDER's Deep Recital
        /// entry for the conflict between the two; this is the one that shipped.</summary>
        private static readonly Aspect[] Cycle =
        {
            Aspect.Ember, Aspect.Tide, Aspect.Verdant, Aspect.Gale, Aspect.Umbra, Aspect.Lumen
        };

        public const int AspectCycleLength = 6;

        /// <summary>True on every 10th floor: a Guardian is a boss-plus-escorts encounter at a
        /// raised level, the same shape the campaign finales use. The interval is read from
        /// balance.json so it can be tuned, but the spec names it as 10 and the harness checks
        /// assume it. Guardians keep the Aspect cycle unbroken - floor 10 is whatever the cycle
        /// says floor 10 is (Gale), not a special Aspect.</summary>
        public static bool IsGuardian(BalanceConfig cfg, int floor)
        {
            int interval = cfg.RecitalGuardianInterval;
            return interval > 0 && floor > 0 && floor % interval == 0;
        }

        public static Aspect AspectForFloor(int floor)
        {
            int index = (Math.Max(1, floor) - 1) % AspectCycleLength;
            return Cycle[index];
        }

        /// <summary>0-based count of full 6-floor cycles completed before this floor.</summary>
        public static int CycleIndexForFloor(int floor)
        {
            return (Math.Max(1, floor) - 1) / AspectCycleLength;
        }

        /// <summary>
        /// Enemy level for a floor: a base, plus a flat amount per floor climbed, plus a bonus
        /// applied once per completed Aspect cycle - the term that makes difficulty step up at
        /// cycle boundaries rather than climbing at one flat per-floor rate forever.
        /// </summary>
        public static int LevelForFloor(BalanceConfig cfg, int floor)
        {
            int f = Math.Max(1, floor);
            int level = cfg.RecitalBaseLevel
                      + (f - 1) * cfg.RecitalLevelsPerFloor
                      + CycleIndexForFloor(f) * cfg.RecitalBonusLevelsPerCycle;
            return Math.Max(1, level);
        }

        public static bool IsMilestone(BalanceConfig cfg, int floor)
        {
            return cfg.RecitalMilestoneInterval > 0 && floor % cfg.RecitalMilestoneInterval == 0;
        }

        public static bool IsDeepMilestone(BalanceConfig cfg, int floor)
        {
            return cfg.RecitalDeepMilestoneInterval > 0 && floor % cfg.RecitalDeepMilestoneInterval == 0;
        }

        /// <summary>
        /// Builds one floor's fight. Picks MaxTeamSize enemies from whichever templates in content
        /// carry the floor's Aspect - "a small pool of enemy templates", scaled by floor rather than
        /// hand-placed. An Aspect with zero matching templates in content produces an EMPTY floor
        /// (Enemies.Count == 0) rather than throwing; ContentDatabase.Validate warns about this at
        /// load so a content gap is caught there; DevHarness also asserts it can't happen for any of
        /// the six today (Fractured Chorister exists specifically to close the one gap that did).
        /// </summary>
        public static StageDef BuildFloor(ContentDatabase content, int floor)
        {
            BalanceConfig cfg = content.Balance;
            int f = Math.Max(1, floor);
            Aspect aspect = AspectForFloor(f);
            int level = LevelForFloor(cfg, f);
            bool guardian = IsGuardian(cfg, f);

            List<EnemyDef> pool = new List<EnemyDef>();
            for (int i = 0; i < content.Enemies.Count; i++)
                if (content.Enemies[i].Aspect == aspect) pool.Add(content.Enemies[i]);

            StageDef stage = new StageDef();
            stage.Id = "recital_floor_" + f;
            stage.Name = guardian ? "Floor " + f + " (Guardian)" : "Floor " + f;
            stage.Subtitle = aspect + " echoes";
            stage.Environment = EnvironmentForAspect(aspect);
            stage.IsBoss = IsDeepMilestone(cfg, f) || guardian;

            if (pool.Count == 0) return stage;

            int teamSize = Math.Max(1, cfg.MaxTeamSize);
            int bossLevel = guardian ? level + Math.Max(0, cfg.RecitalGuardianBossLevelBonus) : level;

            for (int slot = 0; slot < teamSize; slot++)
            {
                EnemyDef def = pool[(f + slot) % pool.Count];
                EnemySpawn spawn = new EnemySpawn();
                spawn.EnemyId = def.Id;
                // Guardian shape: first enemy at a raised level, rest at the floor's base level,
                // the same boss-plus-escorts pattern the campaign finales use.
                spawn.Level = (guardian && slot == 0) ? bossLevel : level;
                stage.Enemies.Add(spawn);
            }
            return stage;
        }

        /// <summary>Best-fit existing environment id per Aspect - none of the six painted
        /// backgrounds were authored with an Aspect mapping in mind, so this is a placeholder
        /// pairing, not a claimed design; every environment already falls back to a generated
        /// gradient when nothing is painted for it (see DESIGN-BUILD-ORDER's art pipeline entry).</summary>
        private static string EnvironmentForAspect(Aspect aspect)
        {
            switch (aspect)
            {
                case Aspect.Ember: return "ember";
                case Aspect.Tide: return "tidal";
                case Aspect.Verdant: return "hollow";
                case Aspect.Gale: return "coast";
                case Aspect.Umbra: return "deep";
                case Aspect.Lumen: return "ashen";
            }
            return "ashen";
        }
    }
}
