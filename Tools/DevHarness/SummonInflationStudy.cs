using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// SUMMON INFLATION, a report and nothing else (R17 task 7). A summon costs balance.json's
    /// summonCost (120) at every stage while the Shards a day grow with the frontier, so a summon gets
    /// cheaper in hours of play and a 5-star gets more common. This prints the Shards a day at each
    /// chapter's last stage, the summons and days per random 5-star that buys, and the two options the
    /// report proposes: a summon cost that scales by chapter, or shard income that flattens.
    ///
    /// It changes nothing and asserts nothing; run it with --check SummonInflationStudy. The income is
    /// the one IDEAS-ECONOMY-PACKS section 1 uses (idle collected twice a day, so 20 of the 10 hour
    /// cap's hours), with a second column that adds a committed player's repeat clears (the 12
    /// highest stages, 3 sweeps each, at each stage's own RewardShards).
    /// </summary>
    internal static partial class Program
    {
        private static void SummonInflationStudy(ContentDatabase db)
        {
            Line("SUMMON INFLATION (report only: Shards a day against a summon that costs " + db.Balance.SummonCost + " at every stage)");
            float w5 = db.Balance.SummonWeight5;
            float total = db.Balance.SummonWeight1 + db.Balance.SummonWeight2 + db.Balance.SummonWeight3 + db.Balance.SummonWeight4 + db.Balance.SummonWeight5;
            double p5 = total > 0f ? w5 / total : 0.0;
            Line("  A pull is a 5-star with probability " + Fmt((float)(p5 * 100.0)) + "% (summonWeight5 over the weights), the first "
                 + GameSession.PityGuaranteedSummons + " pulls of an account aside. Idle is collected twice a day (20 hours).");
            string[] frontiers = { "1-12", "2-12", "3-12", "4-12", "5-12", "6-12" };
            int cost = db.Balance.SummonCost;

            double anchorIdle = 0.0, anchorWithSweeps = 0.0;
            StageDef anchor = RelicStageByCode(db, "2-12");
            if (anchor != null)
            {
                anchorIdle = anchor.IdleShardsPerHour * 20.0;
                anchorWithSweeps = anchorIdle + SweepShards(db, anchor.GlobalIndex);
            }

            Line("");
            Line("  TODAY (cost " + cost + " everywhere)");
            Line("  frontier  idleSh/h  idleSh/day  +sweeps/day  summons/day  daysPer5star  (with sweeps: summons/day  daysPer5star)");
            for (int f = 0; f < frontiers.Length; f++)
            {
                StageDef s = RelicStageByCode(db, frontiers[f]);
                if (s == null) continue;
                double idle = s.IdleShardsPerHour * 20.0;
                double sweeps = SweepShards(db, s.GlobalIndex);
                double summons = idle / cost, summonsAll = (idle + sweeps) / cost;
                Line("  " + frontiers[f].PadRight(8) + s.IdleShardsPerHour.ToString().PadLeft(8) + Fmt((float)idle).PadLeft(12) + Fmt((float)sweeps).PadLeft(13)
                     + Fmt((float)summons).PadLeft(13) + Fmt((float)(1.0 / (summons * p5))).PadLeft(14)
                     + Fmt((float)summonsAll).PadLeft(22) + Fmt((float)(1.0 / (summonsAll * p5))).PadLeft(15));
            }
            StageDef first = RelicStageByCode(db, "1-12"), last = RelicStageByCode(db, "6-12");
            if (first != null && last != null)
                Line("  Idle Shards a day grow x" + Fmt((float)(last.IdleShardsPerHour / (double)first.IdleShardsPerHour)) + " from 1-12 to 6-12 and a summon stays " + cost + ".");

            if (anchor == null || anchorIdle <= 0.0) return;
            double anchorSummons = anchorIdle / cost;
            Line("");
            Line("  OPTION A: the summon cost scales by chapter, holding the 2-12 pace (" + Fmt((float)anchorSummons) + " summons a day, "
                 + Fmt((float)(1.0 / (anchorSummons * p5))) + " days per 5-star)");
            Line("  frontier  cost  summons/day  daysPer5star  cost vs 2-12   (committed basis, same pace: cost  xToday)");
            for (int f = 0; f < frontiers.Length; f++)
            {
                StageDef s = RelicStageByCode(db, frontiers[f]);
                if (s == null) continue;
                double idle = s.IdleShardsPerHour * 20.0;
                int scaled = (int)(Math.Round(idle / anchorSummons / 10.0) * 10.0);
                if (f <= 1) scaled = cost;
                double summons = idle / scaled;
                double committedCost = f <= 0 ? cost : Math.Round((idle + SweepShards(db, s.GlobalIndex)) / anchorSummons / 10.0) * 10.0;
                Line("  " + frontiers[f].PadRight(8) + scaled.ToString().PadLeft(5) + Fmt((float)summons).PadLeft(13)
                     + Fmt((float)(1.0 / (summons * p5))).PadLeft(14) + ("x" + Fmt((float)(scaled / (double)cost))).PadLeft(14)
                     + Fmt((float)committedCost).PadLeft(30) + ("x" + Fmt((float)(committedCost / cost))).PadLeft(9));
            }

            Line("");
            Line("  OPTION B: shard income flattens, the cost stays " + cost + " (idleShardsPerHour in campaign.json, a hashed file)");
            Line("  frontier  idleSh/h now  idleSh/h needed  summons/day  daysPer5star  share of today   (committed basis: share of idle+sweep Shards that may stay)");
            for (int f = 0; f < frontiers.Length; f++)
            {
                StageDef s = RelicStageByCode(db, frontiers[f]);
                if (s == null) continue;
                double needed = f <= 1 ? s.IdleShardsPerHour : anchorIdle / 20.0;
                double summons = needed * 20.0 / cost;
                Line("  " + frontiers[f].PadRight(8) + s.IdleShardsPerHour.ToString().PadLeft(12) + Fmt((float)needed).PadLeft(17)
                     + Fmt((float)summons).PadLeft(13) + Fmt((float)(1.0 / (summons * p5))).PadLeft(14)
                     + (Fmt((float)(100.0 * needed / s.IdleShardsPerHour)) + "%").PadLeft(16)
                     + (Fmt((float)(100.0 * (anchorSummons * cost) / (s.IdleShardsPerHour * 20.0 + SweepShards(db, s.GlobalIndex)))) + "%").PadLeft(40));
            }
            Line("");
        }

        /// <summary>Shards a day from a committed player's repeat clears: the 12 highest stages up to the
        /// frontier, as many sweeps as the repeat cap pays, at each stage's own RewardShards.</summary>
        private static double SweepShards(ContentDatabase db, int frontierIndex)
        {
            double total = 0.0;
            int taken = 0;
            for (int i = frontierIndex; i >= 0 && taken < CommittedSweeps; i--, taken++)
                total += db.AllStages[i].RewardShards * (double)db.Balance.RepeatFullClearsPerDay;
            return total;
        }
    }
}
