using UnityEngine;
using Emberfall.Core.Data;
using Emberfall.Core.Session;
using Emberfall.Game;

namespace Emberfall.UI
{
    /// <summary>
    /// Where a campaign stage start goes. The one door every start walks through - the stage
    /// stepper's ENTER BATTLE, the first-run hand-off, CONTINUE on a result screen - so there is
    /// no second way to begin a stage to keep in sync.
    ///
    /// It decides only WHERE to go. Every rule (what can be swept, what a wave costs, what pays)
    /// stays in GameSession; this reads the answer and picks the screen.
    /// </summary>
    public static class CampaignFlow
    {
        /// <summary>What Launch did, for a caller that has to tidy up after itself.</summary>
        public enum LaunchResult
        {
            /// <summary>Nothing started: no hero is deployed.</summary>
            Refused,
            /// <summary>A cleared stage was swept in place; no screen changed.</summary>
            Swept,
            /// <summary>The wave result screen was opened to resolve the next wave.</summary>
            Resolved,
            /// <summary>The battle screen was opened for a single fight (a stage with no ladder).</summary>
            Battle
        }

        /// <summary>
        /// The seed for one tap: the stage, how many battles have been won, and the moment of the
        /// tap, so two taps on the same stage do not replay the same fights. Never zero.
        /// </summary>
        public static int NewSeed(GameApp app, StageDef stage)
        {
            int seed = Mathf.Abs(stage.Id.GetHashCode() * 31
                                 + app.Session.Profile.TotalBattlesWon * 7919
                                 + Mathf.RoundToInt(Time.unscaledTime * 997f));
            if (seed == 0) seed = 1;
            return seed;
        }

        /// <summary>
        /// Resolves a repeat clear in one tap and reports it as a toast rather than a result
        /// screen, because there is no fight to narrate - the outcome is already known by the time
        /// the tap finishes.
        ///
        /// A sweep can fail. It runs the stage's hardest battle to decide (see
        /// GameSession.SweepClearedStage), so a line that has been stripped of gear since the
        /// original clear is told so instead of being paid for a stage it can no longer beat.
        /// </summary>
        public static void Sweep(GameApp app, StageDef stage)
        {
            int seed = NewSeed(app, stage);

            BattleRewards rewards = app.Session.SweepClearedStage(stage, seed, TimeUtil.NowUnix());
            if (!rewards.Victory)
            {
                app.Notify("Your line cannot sweep " + stage.Name + " any more.");
                app.Audio.Defeat();
                return;
            }

            app.Audio.Victory();
            app.Notify(rewards.RepeatCapped
                ? "Swept " + stage.Name + ". Daily repeat cap reached, no spoils."
                : "Swept " + stage.Name + ".  +" + rewards.Gold + "g  +" + rewards.Shards + " shards");
            app.RefreshHud();
        }

        /// <summary>
        /// Starts a stage the way its state says it should start.
        ///
        /// A STAGE ALREADY CLEARED IS SWEPT, NOT REFOUGHT, and a stage with a wave curve that is
        /// not yet cleared is resolved a wave at a time on the wave result screen. Only a stage
        /// with no ladder at all falls through to the battle screen as a single fight.
        /// </summary>
        public static LaunchResult Launch(GameApp app, StageDef stage)
        {
            if (app.Session.Profile.TeamHeroes().Count == 0)
            {
                app.Notify("Deploy at least one hero first.");
                return LaunchResult.Refused;
            }

            if (app.Session.CanSweep(stage))
            {
                Sweep(app, stage);
                return LaunchResult.Swept;
            }

            if (stage.HasWaveCurve)
            {
                app.Router.WaveResult.Begin(stage);
                return LaunchResult.Resolved;
            }

            app.Router.Battle.SetStage(stage);
            app.Router.Show(ScreenId.Battle);
            return LaunchResult.Battle;
        }
    }
}
