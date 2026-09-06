using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Model;

namespace Emberfall.Core.Progression
{
    /// <summary>
    /// The prologue's fixed squad and its synthetic stages.
    ///
    /// WHY THIS IS CODE AND NOT campaign.json, which is the same reason ProvingGenerator is:
    /// everything in campaign.json is reachable by playing, and reachable means counted.
    /// GetStageByGlobalIndex walks it, NextStage derives from StagesCleared over it, the harness's
    /// pinned win-rate curve iterates it, and CampaignScreen lists it. A prologue stage that
    /// appeared there would show up in all four - a thirteenth chapter-1 row nobody can replay, a
    /// stage index that shifts every id after it, and a win-rate column for a fight the player is
    /// meant to lose. Built here, it is invisible to all of them.
    ///
    /// It also means the prologue can never collide with a balance pass. campaign.json is not
    /// touched by this feature at all.
    /// </summary>
    public static class PrologueScript
    {
        /// <summary>
        /// The teaching steps, in order. Phase one implements Combat, FullBattle and Boss; the
        /// middle five are declared now because the step machine switches on this enum and a
        /// half-declared sequence would make "which steps exist" a question you answer by reading
        /// two files instead of one.
        /// </summary>
        public enum Step
        {
            Combat = 1,
            Formation = 2,
            Skills = 3,
            Roles = 4,
            Equipment = 5,
            Aspects = 6,
            FullBattle = 7,
            Boss = 8
        }

        /// <summary>
        /// Painted, and deliberately not the campaign's opening look. The prologue is meant to read
        /// as somewhere much further along than Shardfall Verge, and "hollow" is one of the three
        /// environments that actually has art (ashen, ember, hollow) rather than a generated
        /// palette - see BattleAssets.BackdropTexture for which do.
        /// </summary>
        public const string EnvironmentId = "hollow";

        /// <summary>Level every squad member is built at. High enough to read as late-game against
        /// chapter-1 content, low enough that The Unmade still ends them.</summary>
        public const int SquadLevel = 42;

        private const string StageIdPrefix = "prologue_";

        /// <summary>
        /// The five heroes the player opens the game holding.
        ///
        /// This is the Ember line, and the choice is not flavour. They are the only five heroes in
        /// the game with PAINTED sprites - everyone else renders as a generated primitive rig - and
        /// a prologue whose whole job is to feel like power the player has not earned yet has to
        /// LOOK like it. They are also all non-starters, so the three heroes handed over after the
        /// rewind (Orrin Kaal, Kalyx Emberwright, Wren Ilyra) share nobody with this squad: the
        /// loss takes away a line the player never sees again, rather than demoting one they keep.
        ///
        /// Built as plain HeroInstances with no relic and no armor. BattleRequest.Profile is left
        /// null for prologue fights, and a null profile is an exact no-op through
        /// StatMath.ApplyArmor, so this squad resolves purely from its defs and level and cannot
        /// read or touch the player's save.
        /// </summary>
        public static List<HeroInstance> BuildSquad(ContentDatabase content)
        {
            string[] ids =
            {
                "kael_the_ashbound",
                "seraphine_the_last_ember",
                "veska_the_broken_oath",
                "marrow_the_burning_vow",
                "orren_the_cinder_hunter"
            };

            List<HeroInstance> squad = new List<HeroInstance>();
            if (content == null) return squad;

            for (int i = 0; i < ids.Length; i++)
            {
                HeroDef def = content.GetHero(ids[i]);
                if (def == null) continue;   // content can change under a hardcoded id; skip, do not throw

                HeroInstance hero = new HeroInstance();
                hero.Uid = "prologue_" + ids[i];
                hero.DefId = ids[i];
                hero.Level = SquadLevel;
                hero.Copies = 1;

                // Def is [NonSerialized] and is BOUND, not stored - GameSession does it on load and
                // on every grant, and CombatSimulation.BuildUnits silently `continue`s past any
                // hero whose Def is null. Missing this bind is not a crash, it is a battle the
                // player is simply absent from: the first live run of this file fought step one
                // with an empty banner and lost it in one round at 0%.
                hero.Def = def;
                squad.Add(hero);
            }
            return squad;
        }

        /// <summary>
        /// One synthetic stage per step. Same shape ProvingGenerator.BuildStage produces: an id
        /// that is not in the campaign, a name, an environment, and a spawn list.
        ///
        /// Rewards are left at their StageDef defaults and never read - BattleScreen routes a
        /// prologue result to ShowPrologueResult, which grants nothing. That is the load-bearing
        /// half of "reward-free": GrantBattleResult would happily record a first clear against
        /// "prologue_1" and write that id into ClearedStageIds, which is a fake campaign clear
        /// living in the player's save forever.
        /// </summary>
        public static StageDef BuildStage(Step step)
        {
            StageDef stage = new StageDef();
            stage.Id = StageIdPrefix + (int)step;
            stage.Environment = EnvironmentId;
            stage.ChapterNumber = 0;
            stage.Index = (int)step;

            switch (step)
            {
                case Step.Combat:
                    stage.Name = "The Long Retreat";
                    stage.Subtitle = "You have done this a thousand times.";
                    Spawn(stage, "hollow_husk", 34);
                    Spawn(stage, "grave_moth", 34);
                    break;

                case Step.FullBattle:
                    stage.Name = "The Last Gate";
                    stage.Subtitle = "Hold here. Nothing gets past.";
                    Spawn(stage, "ashen_revenant", 38);
                    Spawn(stage, "cinder_imp", 37);
                    Spawn(stage, "cinder_imp", 37);
                    Spawn(stage, "grave_moth", 36);
                    break;

                case Step.Boss:
                    stage.Name = "The Unmade";
                    stage.Subtitle = "It has been waiting for you to arrive.";
                    stage.IsBoss = true;
                    Spawn(stage, "the_unmade", 60);
                    break;

                default:
                    // Steps 2-6 are phase two. Returning a real, winnable stage rather than null
                    // means a mistimed call teaches nothing instead of crashing.
                    stage.Name = "The Long Retreat";
                    stage.Subtitle = "You have done this a thousand times.";
                    Spawn(stage, "hollow_husk", 34);
                    break;
            }
            return stage;
        }

        private static void Spawn(StageDef stage, string enemyId, int level)
        {
            EnemySpawn spawn = new EnemySpawn();
            spawn.EnemyId = enemyId;
            spawn.Level = level;
            stage.Enemies.Add(spawn);
        }

        /// <summary>
        /// Whether a stage came from here. Keyed on the id prefix rather than on object identity
        /// because BattleScreen holds the StageDef across a rebuild and a fresh BuildStage call
        /// would not be the same instance.
        /// </summary>
        public static bool IsPrologueStage(StageDef stage)
        {
            return stage != null && !string.IsNullOrEmpty(stage.Id) && stage.Id.StartsWith(StageIdPrefix);
        }
    }
}
