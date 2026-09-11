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
                "seraphine_the_closed_volume",
                "aeron_the_unbroken_stride",
                "nereia_the_field_medic",
                "isolde_the_slow_fading",
                "hale_the_relentless_hunter"
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

                // Two husks, and BOTH of them target FrontEnemy (enemies.json, "target":
                // "FrontEnemy"), which CombatSimulation resolves to the lowest living Slot -
                // "u.Slot < picked.Slot", the tie-break at the bottom of ResolveTargets. So every
                // hit in this fight lands on whoever is in slot 1 and the other four finish it
                // near full. That is the lesson, and it is a fact of the sim rather than a
                // staged coincidence.
                //
                // Umbra on purpose: Umbra counters only Lumen, so an all-Ember line takes and
                // deals flat damage here. An enemy with an aspect edge would put a second
                // unexplained variable on the one screen teaching position.
                case Step.Formation:
                    stage.Name = "The Narrow Bridge";
                    stage.Subtitle = "Only one of you can stand in front.";
                    Spawn(stage, "hollow_husk", 36);
                    Spawn(stage, "hollow_husk", 36);
                    break;

                // Every hero in the squad has a 100-cost active, so this fight has to LAST, and
                // "has to" is measured rather than hoped for. The first draft of this stage was two
                // ashen revenants at 40 and it ran a mean of 3.0 rounds: across 40 seeded runs an
                // ultimate fired in ZERO of them. The step taught nothing and both its cards said
                // otherwise, which is the failure mode a tutorial can least afford.
                //
                // Two sundered colossi at 48 was picked from a five-way sweep on the numbers that
                // matter, then confirmed at 50 seeds: 50/50 wins, mean 4.4 rounds, an ultimate in
                // 50/50 runs and Seraphine's own in 50/50 - she is the hero the card puts on
                // screen, so it matters that the ultimate the player is told to watch for is
                // hers. It is not guaranteed, though: a 30-seed sweep during tuning saw hers in
                // 29/30, which is why the card says WATCH FOR IT rather than promising it.
                // Re-measure if the squad, the energy curve or these enemies ever change.
                //
                // Umbra against an Ember line is deliberately the neutral matchup - Umbra counters
                // only Lumen. The aspect wheel is step 6's job, and a fight that also happened to
                // be an aspect blowout would let the player credit the wrong mechanic for the
                // damage they are watching.
                //
                // That these are chapter-1's boss, casually doubled and walked over, is the point
                // rather than an accident: it is the clearest statement of what this line was.
                case Step.Skills:
                    stage.Name = "The Long Watch";
                    stage.Subtitle = "Long enough for everything to come up.";
                    Spawn(stage, "sundered_colossus", 48);
                    Spawn(stage, "sundered_colossus", 48);
                    break;

                // Both directions of the wheel on one field, which is why it is two enemies and
                // not one. The squad is entirely Ember: Ember beats Verdant, so the blight hound
                // takes +10%; Gale beats Ember, so the grave moth deals it. A fight that only
                // showed the good half would teach the wheel as a bonus rather than a trade.
                case Step.Aspects:
                    stage.Name = "The Colour of Things";
                    stage.Subtitle = "Some of them burn easier than others.";
                    Spawn(stage, "blight_hound", 36);
                    Spawn(stage, "grave_moth", 36);
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
                    // Roles and Equipment are taught as callouts over HeroDetailScreen and never
                    // reach this method. Returning a real, winnable stage rather than null means a
                    // mistimed call teaches nothing instead of crashing.
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
