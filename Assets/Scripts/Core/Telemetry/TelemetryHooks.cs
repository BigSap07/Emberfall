using System;
using Emberfall.Core.Data;
using Emberfall.Core.Session;

namespace Emberfall.Core.Telemetry
{
    /// <summary>Whoever listens for the few moments of play the stats count. Implemented by the game's
    /// TelemetryService and by the harness; null in every other run.</summary>
    public interface ITelemetrySink
    {
        /// <summary>A stage_attempt, wave_lost or stage_clear, with the numbers its payload carries.</summary>
        void Campaign(string eventName, string stage, int wave, int battle, int teamPower, int recommendedPower);
        void HeroLevelUp(int count);
        void Summon(int count);
    }

    /// <summary>
    /// THE TAPS ON GameSession (Docs/DESIGN-TELEMETRY.md). GameSession calls these at the moment a
    /// stage is attempted, a battle is lost, a stage is cleared, a hero is levelled and a hero is
    /// summoned. They OBSERVE: they read, hand the numbers to the sink, and return. They never
    /// change an argument, a result or the profile, and the harness proves a session played with a
    /// sink attached is byte for byte the session played without one.
    ///
    /// With no sink, or the switch off, each call is one comparison. A sink that throws is
    /// swallowed here, so the game cannot be broken by its own statistics. The team power is read
    /// only when there is a sink to give it to.
    /// </summary>
    public static class TelemetryHooks
    {
        public static ITelemetrySink Sink;

        private static ITelemetrySink Live
        {
            get { return TelemetrySwitch.Enabled ? Sink : null; }
        }

        public static void StageAttempt(GameSession session, StageDef stage, int wave, int battle)
        {
            Campaign(TelemetrySchema.StageAttempt, session, stage, wave, battle);
        }

        public static void WaveLost(GameSession session, StageDef stage, int wave, int battle)
        {
            Campaign(TelemetrySchema.WaveLost, session, stage, wave, battle);
        }

        public static void StageCleared(GameSession session, StageDef stage, int wave, int battle)
        {
            Campaign(TelemetrySchema.StageClear, session, stage, wave, battle);
        }

        public static void HeroLevelUp(int count)
        {
            ITelemetrySink sink = Live;
            if (sink == null) return;
            try { sink.HeroLevelUp(count); } catch (Exception) { }
        }

        public static void Summon(int count)
        {
            ITelemetrySink sink = Live;
            if (sink == null) return;
            try { sink.Summon(count); } catch (Exception) { }
        }

        private static void Campaign(string eventName, GameSession session, StageDef stage, int wave, int battle)
        {
            ITelemetrySink sink = Live;
            if (sink == null || session == null || stage == null) return;
            try
            {
                // EffectiveTeamPower is the number a stage's recommendedPower is meant to be
                // compared with: relics, armor and guild tech folded in the way a fight folds them.
                sink.Campaign(eventName, stage.Id, wave, battle,
                              session.Profile.EffectiveTeamPower(session.Content), stage.RecommendedPower);
            }
            catch (Exception) { }
        }
    }
}
