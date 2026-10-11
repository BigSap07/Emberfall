using System.Collections.Generic;
using Emberfall.Core.Combat;

namespace Emberfall.Core.Session
{
    /// <summary>Why GameSession.ResolveWave declined to fight. None means it ran. Every refusal
    /// leaves the profile untouched.</summary>
    public enum WaveRefusal
    {
        None,
        /// <summary>No stage was supplied.</summary>
        NoStage,
        /// <summary>The stage has no wave curve, so there is no ladder to resolve.</summary>
        NoWaveCurve,
        /// <summary>The stage is not unlocked yet.</summary>
        StageLocked,
        /// <summary>The stage is already cleared; the sweep is its only replay path.</summary>
        StageCleared,
        /// <summary>No hero is deployed.</summary>
        NoTeam
    }

    /// <summary>One battle inside a resolved wave: the few numbers the wave result screen needs
    /// to draw a row, without carrying the whole event list of a won fight.</summary>
    public sealed class WaveBattleOutcome
    {
        public int Wave;
        public int Battle;
        /// <summary>1-based position across the whole stage; also what the seed was mixed with.</summary>
        public int Ordinal;
        public int Seed;
        public int Rounds;
        public bool Victory;
        public bool TimedOut;
        /// <summary>Share of the player's HP left at the end. The "am I still winning" signal.</summary>
        public float PlayerHpRatio;
        public float EnemyHpRatio;
    }

    /// <summary>
    /// What one GameSession.ResolveWave did. The wave result screen answers "what did I just
    /// win" and "am I still winning" and only READS this record; it never computes an outcome.
    /// </summary>
    public sealed class WaveResolution
    {
        /// <summary>None when the wave ran; otherwise why it did not. Nothing else is meaningful
        /// on a refusal except StageId.</summary>
        public WaveRefusal Refused = WaveRefusal.None;
        public string StageId = "";

        /// <summary>The wave that was resolved (1-based) and the stage's wave and battle counts.</summary>
        public int Wave;
        public int WaveCount;
        public int BattleCount;
        /// <summary>The battle the resolve started at: 1 for a fresh wave, later when a loss
        /// earlier left the cursor mid-wave.</summary>
        public int FirstBattle;

        /// <summary>Every battle fought, in order, ending on the lost one if there was a loss.</summary>
        public readonly List<WaveBattleOutcome> Battles = new List<WaveBattleOutcome>();

        /// <summary>True when the wave was won to its last battle (a stage clear counts).</summary>
        public bool WaveCleared;
        /// <summary>True when the wave also finished the stage, which is when rewards are paid.</summary>
        public bool StageCleared;

        /// <summary>The battle number that was lost, or 0 when none was. The next resolve
        /// retries exactly this one.</summary>
        public int LostBattle;
        /// <summary>The lost fight, for the screen to play or inspect. Null when nothing was lost.</summary>
        public BattleResult LostResult;
        /// <summary>The plain-language reason for the loss. Null when nothing was lost.</summary>
        public DebriefResult LostDebrief;

        /// <summary>The stage-clear payout. Set only when StageCleared; a won mid-stage wave pays
        /// nothing, per DESIGN-CAMPAIGN-STRUCTURE.md.</summary>
        public BattleRewards Rewards;

        /// <summary>Where the cursor stands after the resolve; (1, 1) once the stage is cleared.</summary>
        public int NextWave;
        public int NextBattle;

        /// <summary>PlayerHpRatio of the last battle WON, how much headroom the line had at the
        /// end of the run; -1 when no battle was won.</summary>
        public float Margin = -1f;
    }
}
