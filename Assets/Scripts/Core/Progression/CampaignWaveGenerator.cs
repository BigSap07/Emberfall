using System;
using System.Collections.Generic;
using Emberfall.Core.Data;

namespace Emberfall.Core.Progression
{
    /// <summary>
    /// Turns one authored stage definition into its wave x battle ladder, per
    /// DESIGN-CAMPAIGN-STRUCTURE.md (decided 2026-09-07).
    ///
    /// THIS IS RecitalGenerator'S PATTERN, NOT A PARALLEL SYSTEM. Recital answers "what level is
    /// floor N of an endless climb" with LevelForFloor; this answers "what level is battle B of
    /// wave W inside THIS stage's own floor and ceiling". Both are one arithmetic function from a
    /// position to a level, both build a StageDef on demand, and neither stores a per-encounter
    /// row anywhere. The difference is only that Recital's climb is unbounded and grows from
    /// config constants, while a campaign stage interpolates between two numbers authored on the
    /// stage itself.
    ///
    /// The 20,736 battles in the design exist as arithmetic. Nothing here is stored in content,
    /// in the save, or in memory beyond the single StageDef a caller asked for.
    /// </summary>
    public static class CampaignWaveGenerator
    {
        /// <summary>
        /// Position of a (wave, battle) pair along the stage, from 0 at the first battle to 1 at
        /// the last. Plain linear across all battles, NOT stepped per wave: battle 12 of wave 1
        /// and battle 1 of wave 2 are one step apart, exactly like any other adjacent pair, so a
        /// wave boundary is a readout for the player rather than a difficulty cliff.
        ///
        /// t = ((wave - 1) * battleCount + (battle - 1)) / (waveCount * battleCount - 1)
        ///
        /// A stage with a single battle has no span to interpolate across and returns 0, which
        /// puts it at its floor - the correct answer for every synthetic stage.
        /// </summary>
        public static float Progress(StageDef stage, int wave, int battle)
        {
            if (stage == null) return 0f;
            int waves = Math.Max(1, stage.WaveCount);
            int battles = Math.Max(1, stage.BattleCount);
            int span = waves * battles - 1;
            if (span <= 0) return 0f;

            int w = Clamp(wave, 1, waves);
            int b = Clamp(battle, 1, battles);
            int index = (w - 1) * battles + (b - 1);
            return index / (float)span;
        }

        /// <summary>
        /// The level the stage's HARDEST unit sits at for this battle: floor at (1,1), ceiling at
        /// the last battle, linear in between.
        ///
        /// Rounded rather than truncated so the curve is symmetric about its midpoint; truncation
        /// would spend most of a 144-battle stage one level below the line and only reach the
        /// ceiling on the final battle.
        /// </summary>
        public static int LevelForBattle(StageDef stage, int wave, int battle)
        {
            if (stage == null) return 1;
            float t = Progress(stage, wave, battle);
            float level = stage.FloorLevel + t * (stage.CeilingLevel - stage.FloorLevel);
            return Math.Max(1, (int)Math.Round(level));
        }

        /// <summary>
        /// How far every enemy in the roster moves for this battle. One delta for the whole
        /// roster is what keeps composition fixed: a stage authored as 13/14/15 is 13/14/15 at the
        /// floor and 18/19/20 five levels up, never re-spread and never re-ordered.
        /// </summary>
        public static int LevelDelta(StageDef stage, int wave, int battle)
        {
            if (stage == null) return 0;
            return LevelForBattle(stage, wave, battle) - stage.FloorLevel;
        }

        /// <summary>
        /// The stage as it should be fought at this (wave, battle).
        ///
        /// Returns a COPY. The definition loaded from content is shared by every caller and must
        /// never be mutated into one battle's version of itself - doing that once would make the
        /// next read of the same stage return the previous battle's levels.
        ///
        /// A stage with no curve returns a copy of itself unchanged, so callers do not need to
        /// know whether they are holding a campaign stage or a synthetic one.
        /// </summary>
        public static StageDef BuildBattle(StageDef stage, int wave, int battle)
        {
            if (stage == null) return null;

            StageDef s = new StageDef();
            s.Id = stage.Id;
            s.ChapterId = stage.ChapterId;
            s.Name = stage.Name;
            s.Subtitle = stage.Subtitle;
            s.Index = stage.Index;
            s.GlobalIndex = stage.GlobalIndex;
            s.IsBoss = stage.IsBoss;
            s.Environment = stage.Environment;
            s.RewardGold = stage.RewardGold;
            s.RewardShards = stage.RewardShards;
            s.RewardAccountXp = stage.RewardAccountXp;
            s.FirstClearGold = stage.FirstClearGold;
            s.FirstClearShards = stage.FirstClearShards;
            s.RecommendedPower = stage.RecommendedPower;
            s.IdleGoldPerHour = stage.IdleGoldPerHour;
            s.IdleShardsPerHour = stage.IdleShardsPerHour;
            s.ChapterNumber = stage.ChapterNumber;
            s.WaveCount = stage.WaveCount;
            s.BattleCount = stage.BattleCount;
            s.FloorLevel = stage.FloorLevel;
            s.CeilingLevel = stage.CeilingLevel;

            // Stamped here and nowhere else. This is the token BuildBattleRequest checks for, so
            // the only way to obtain a battle it will accept is to come through this function.
            int battles = Math.Max(1, stage.BattleCount);
            s.ResolvedBattleOrdinal = (Math.Max(1, wave) - 1) * battles + Math.Max(1, battle);

            int delta = LevelDelta(stage, wave, battle);
            for (int i = 0; i < stage.Enemies.Count; i++)
            {
                EnemySpawn src = stage.Enemies[i];
                EnemySpawn copy = new EnemySpawn();
                copy.EnemyId = src.EnemyId;
                copy.Level = Math.Max(1, src.Level + delta);
                s.Enemies.Add(copy);
            }
            return s;
        }

        /// <summary>
        /// The battle after this one, or false when the stage is finished.
        ///
        /// Waves fill left to right: battle runs to battleCount, then rolls to the next wave.
        /// Returning false rather than clamping is what lets the caller tell "advance to the next
        /// battle" apart from "the stage is cleared", which are different outcomes.
        /// </summary>
        public static bool Next(StageDef stage, int wave, int battle, out int nextWave, out int nextBattle)
        {
            nextWave = wave;
            nextBattle = battle;
            if (stage == null) return false;

            int waves = Math.Max(1, stage.WaveCount);
            int battles = Math.Max(1, stage.BattleCount);

            if (battle < battles) { nextBattle = battle + 1; return true; }
            if (wave < waves) { nextWave = wave + 1; nextBattle = 1; return true; }
            return false;
        }

        /// <summary>
        /// 1-based ordinal of a (wave, battle) across the whole stage: (1,1) is 1, the last battle
        /// is TotalBattles. Used by the harness and by any readout that wants "battle 37 of 144"
        /// rather than the two-part form.
        /// </summary>
        public static int BattleOrdinal(StageDef stage, int wave, int battle)
        {
            if (stage == null) return 1;
            int battles = Math.Max(1, stage.BattleCount);
            int w = Clamp(wave, 1, Math.Max(1, stage.WaveCount));
            int b = Clamp(battle, 1, battles);
            return (w - 1) * battles + b;
        }

        private static int Clamp(int v, int lo, int hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }
    }
}
