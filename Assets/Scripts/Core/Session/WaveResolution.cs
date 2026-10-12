using System.Collections.Generic;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;

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
    /// What it takes to fight one battle of a resolved wave again: the stage and position, the seed
    /// the battle was resolved at, and the line that fought it. A fight is a pure function of its
    /// request, so these four are the whole battle and a replay is the same event list, not a
    /// second roll.
    ///
    /// MEMORY ONLY. It lives on the WaveResolution and nowhere else: SaveSerializer never sees it,
    /// no save version moved for it, and it is gone when the next wave resolves. The line is a set
    /// of copies, so a hero levelled after the wave does not change the fight being watched.
    /// </summary>
    public sealed class WaveReplay
    {
        /// <summary>The authored stage, not the resolved battle; GameSession.ReplayBattle resolves it.</summary>
        public StageDef Stage;
        public int Wave;
        public int Battle;
        public int Seed;
        /// <summary>True when this is the battle that was lost, false when it is the closest win.</summary>
        public bool Lost;
        public readonly List<HeroInstance> Team = new List<HeroInstance>();
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

        /// <summary>The stage-clear payout. Set only when StageCleared. On a first clear its gold
        /// and Wayfarer XP are what the stage still owed after its waves' drip shares, so the
        /// stage's whole payout is unchanged (GameSession.WaveDripPercent).</summary>
        public BattleRewards Rewards;

        /// <summary>What this wave paid on its own when it was won without finishing the stage: an
        /// equal share of WaveDripPercent of the stage's gold and Wayfarer XP, on a first clear
        /// only. Zero on a loss, on a stage clear (Rewards carries that) and on any wave whose share
        /// rounds to nothing.</summary>
        public int WaveGold;
        public int WaveAccountXp;
        /// <summary>Account ranks the wave's XP share crossed. Zero when it crossed none.</summary>
        public int WavePlayerLevelsGained;

        /// <summary>Where the cursor stands after the resolve; (1, 1) once the stage is cleared.</summary>
        public int NextWave;
        public int NextBattle;

        /// <summary>
        /// The player's side of the last battle fought, as the simulation snapshotted it: who
        /// stands on the field behind the result. Presentation reads it to stand the squad up; it
        /// is copied off the fight, so nothing is built twice. Empty on a refusal. Never saved.
        /// </summary>
        public readonly List<UnitSnapshot> Squad = new List<UnitSnapshot>();

        /// <summary>
        /// The battle worth watching: the lost one after a loss, otherwise the win that left the
        /// least of the line's HP. Null on a refusal. Never saved.
        /// </summary>
        public WaveReplay Replay;

        /// <summary>
        /// What each hero did, per battle and summed over the battles this resolve fought, tallied
        /// from the BattleEvents inside ResolveWave. Null on a refusal. Never saved, and nothing
        /// that fights reads it.
        /// </summary>
        public WaveStats Stats;
    }
}
