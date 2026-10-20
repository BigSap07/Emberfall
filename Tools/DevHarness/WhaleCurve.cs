using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE WHALE STUDY (R24 task 5, report only). Two instruments on the R16 reference walk:
    ///   --whale-dust [walks]    the Relic Dust income per day of a player at 4-12 and at 6-12, as the walk
    ///                           earns it and as the R17/R19 models count it (committed, casual, ceiling).
    ///   --whale-curve [walks]   the walk as a non-spender, the R19 spender and a whale who buys every offer
    ///                           every period (see WhaleStep), per chapter.
    /// Neither is a check and neither runs in the gate; both are inert in every other run (_walkStoreMode
    /// is below 4 and WhaleRequested is false).
    /// </summary>
    internal static partial class Program
    {
        private static bool WhaleRequested;
        private static string WhaleMode = "";
        private static int WhaleWalks = 20;

        /// <summary>Main's loop hands each argument here first; true means it was a whale flag and has been consumed.</summary>
        private static bool WhaleTakeArg(string[] args, ref int i)
        {
            if (args[i] != "--whale-dust" && args[i] != "--whale-curve") return false;
            WhaleRequested = true;
            WhaleMode = args[i].Substring("--whale-".Length);
            int n;
            if (i + 1 < args.Length && int.TryParse(args[i + 1], out n)) { WhaleWalks = n; i++; }
            return true;
        }

        private static int RunWhaleStudy(string contentDir)
        {
            Console.OutputEncoding = Encoding.UTF8;
            ContentDatabase db = LoadContent(contentDir);
            if (db == null) return 1;
            if (WhaleMode == "dust") return WhaleDustStudy(db, WhaleWalks);
            Line("unknown whale mode " + WhaleMode);
            return 1;
        }

        private static int WhaleStageIndex(ContentDatabase db, string code)
        {
            for (int i = 0; i < db.AllStages.Count; i++) if (db.AllStages[i].DisplayCode == code) return i;
            return -1;
        }

        private static string Spread(List<int> xs)
        {
            if (xs.Count == 0) return "-";
            return Median(xs) + " (p10 " + Percentile(xs, 0.1) + ", p90 " + Percentile(xs, 0.9) + ", min " + Percentile(xs, 0) + ", max " + Percentile(xs, 1.0) + ")";
        }

        /// <summary>The model Dust a day (R17 relic-pricing.md): the top n cleared stages, repeatFullClearsPerDay sweeps each.</summary>
        private static long WhaleModelDust(ContentDatabase db, GameSession s, int frontierIdx, int topStages)
        {
            long total = 0;
            for (int i = frontierIdx; i >= 0 && i > frontierIdx - topStages; i--)
                total += s.RelicDustForRepeatClear(db.AllStages[i]) * (long)db.Balance.RepeatFullClearsPerDay;
            return total;
        }

        private static int WhaleDustStudy(ContentDatabase db, int walks)
        {
            Line("");
            Line("WHALE DUST - " + walks + " reference walks (never converts, never ascends), plain clock. The walk buys no relic, so its Dust only accrues.");
            EconomyRun[] runs = SimulateEconomies(db, walks, false, false);
            string[] codes = { "3-12", "4-12", "5-12", "6-12" };
            for (int c = 0; c < codes.Length; c++)
            {
                int idx = WhaleStageIndex(db, codes[c]);
                int chapterFirst = idx - 11;
                List<int> dayCleared = new List<int>(), heldPerDay = new List<int>(), chapterRate = new List<int>(), stageRate = new List<int>(), stageDays = new List<int>();
                int reached = 0;
                for (int k = 0; k < runs.Length; k++)
                {
                    EconomyRun r = runs[k];
                    if (idx >= r.Stages.Count || !r.Stages[idx].Cleared) continue;
                    reached++;
                    long secs = 0;
                    for (int s = 0; s <= idx; s++) secs += r.Stages[s].ElapsedSeconds;
                    double days = secs / 86400.0;
                    dayCleared.Add((int)Math.Round(days));
                    heldPerDay.Add((int)Math.Round(r.Stages[idx].ExitRelicDust / days));
                    long chSecs = 0;
                    for (int s = chapterFirst; s <= idx; s++) chSecs += r.Stages[s].ElapsedSeconds;
                    chapterRate.Add((int)Math.Round((r.Stages[idx].ExitRelicDust - r.Stages[chapterFirst].EntryRelicDust) / (chSecs / 86400.0)));
                    double stDays = Math.Max(0.01, r.Stages[idx].ElapsedSeconds / 86400.0);
                    stageDays.Add((int)Math.Round(stDays * 10));
                    stageRate.Add((int)Math.Round((r.Stages[idx].ExitRelicDust - r.Stages[idx].EntryRelicDust) / stDays));
                }
                Line("");
                Line("  frontier " + codes[c] + " (cleared, every earlier stage cleared): reached by " + reached + " of " + walks);
                Line("    walk day the frontier is cleared:        " + Spread(dayCleared));
                Line("    walk's Dust a day over its whole campaign: " + Spread(heldPerDay));
                Line("    walk's Dust a day over chapter " + codes[c][0] + " alone:    " + Spread(chapterRate));
                Line("    walk's Dust a day while sitting at " + codes[c] + ": " + Spread(stageRate) + "; days spent there x10: " + Spread(stageDays));
                EconomyRun any = runs[0];
                Line("    model Dust a day: committed (12 stages x3) " + WhaleModelDust(db, any.Session, idx, 12)
                     + ", casual (4 stages x3) " + WhaleModelDust(db, any.Session, idx, 4)
                     + ", ceiling (every stage x3) " + WhaleModelDust(db, any.Session, idx, idx + 1)
                     + "; one frontier sweep " + any.Session.RelicDustForRepeatClear(db.AllStages[idx]));
            }
            return 0;
        }
    }
}
