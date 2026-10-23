using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Save;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE FIRST-WEEK BUG HUNT: one check per bug a player would hit in their first week, each written
    /// to fail against the code as it stood and to pass once the bug was fixed. Every check names the
    /// path a player takes to reach the bug, so the check stays a reproduction rather than a unit test.
    /// </summary>
    internal static partial class Program
    {
        private static void CheckBugHunt(ContentDatabase db, string contentDir)
        {
            Line("");
            Line("FIRST-WEEK BUG HUNT (one reproduction per bug)");
            CheckServerAcceptsWhatThisBuildWrites(db, contentDir);
            CheckRecitalRematchRepaysNoMilestone(db);
        }

        // ------------------------------------------------------------------ 1. the server's save limit

        /// <summary>
        /// push_save (0013) refuses any document whose save_version is above save_limits.max_save_version.
        /// The limit is whatever the migrations, applied in order, leave in that row: 0013 seeds it, and
        /// every later bump has to be a migration that raises it. This build writes up to CurrentVersion,
        /// and a first session reaches it: the first stage clear counts toward the live event that is
        /// running, and a save holding live event state is a version 26 document. A limit below that
        /// refuses every sync from the first clear on.
        /// </summary>
        private static void CheckServerAcceptsWhatThisBuildWrites(ContentDatabase db, string contentDir)
        {
            string root = Path.GetFullPath(Path.Combine(contentDir, Path.Combine("..", Path.Combine("..", ".."))));
            string dir = Path.Combine(root, Path.Combine("Server", Path.Combine("supabase", "migrations")));
            if (!Directory.Exists(dir)) { Check(false, "the migrations folder exists"); return; }
            string[] files = Directory.GetFiles(dir, "*.sql");
            Array.Sort(files, StringComparer.Ordinal);

            // The row as the migrations leave it, statement by statement in file order: the seed in the
            // insert, then every update that sets it (a plain value, or greatest(max_save_version, N)).
            int limit = 0;
            string setBy = "nothing";
            Regex seed = new Regex(@"insert\s+into\s+public\.save_limits\s*\([^)]*\)\s*values\s*\(\s*true\s*,\s*(\d+)", RegexOptions.IgnoreCase);
            Regex raise = new Regex(@"update\s+public\.save_limits\s+set\s+max_save_version\s*=\s*(?:greatest\s*\(\s*max_save_version\s*,\s*)?(\d+)", RegexOptions.IgnoreCase);
            foreach (string f in files)
            {
                string sql = StripSqlLineComments(File.ReadAllText(f));
                foreach (Match m in seed.Matches(sql)) { limit = int.Parse(m.Groups[1].Value); setBy = Path.GetFileName(f); }
                foreach (Match m in raise.Matches(sql))
                {
                    int n = int.Parse(m.Groups[1].Value);
                    limit = m.Value.IndexOf("greatest", StringComparison.OrdinalIgnoreCase) >= 0 ? Math.Max(limit, n) : n;
                    setBy = Path.GetFileName(f);
                }
            }

            // What a first session writes: a Keeper who skips the tutorial and clears 1-1 while the
            // first live event runs.
            int firstSession = 0;
            if (db.LiveEvents.IsUsable && db.AllStages.Count > 0)
            {
                long t = db.LiveEvents.EpochDay * 86400L + (long)db.Balance.DailyResetHourUtc * 3600L + 3600L;
                GameSession s = new GameSession(db, Onboarding.CreateKeeperProfile(db, t));
                Onboarding.Skip(s, t);
                s.GrantBattleResult(db.AllStages[0], true, t + 60);
                firstSession = SaveSerializer.VersionOf(s.Profile);
            }

            Check(firstSession == 0 || firstSession <= limit,
                  "THE SERVER TAKES A FIRST SESSION'S SAVE: clearing 1-1 while a live event runs writes a version "
                  + firstSession + " save, and the migrations leave push_save accepting up to " + limit + " (set by " + setBy + ")");
            Check(SaveSerializer.CurrentVersion <= limit,
                  "and every version this build writes: save_limits.max_save_version (" + limit + ") is at least SaveSerializer.CurrentVersion ("
                  + SaveSerializer.CurrentVersion + "), so a version bump without a migration that raises the row turns this red");
        }

        private static string StripSqlLineComments(string sql)
        {
            return Regex.Replace(sql, "--[^\\n]*", "");
        }

        // ------------------------------------------------------------------ 2. the Recital rematch

        /// <summary>
        /// After a Recital win the result panel offers REMATCH, which fights the floor just won again
        /// (BattleScreen keeps the floor it was given). A milestone is the reward for reaching a floor, so
        /// it pays on the floor's first clear and never again: a rematch of floor 10 paid its milestone
        /// Relic Dust once per Echo, and a rematch of a deep milestone granted a different relic each time.
        /// The floor's ordinary pay (Gold, Shards, Coda) is what any win pays, and a rematch keeps it.
        /// </summary>
        private static void CheckRecitalRematchRepaysNoMilestone(ContentDatabase db)
        {
            const long Now = 1792713600L;
            BalanceConfig b = db.Balance;
            int milestone = Math.Max(1, b.RecitalMilestoneInterval);
            int deep = Math.Max(1, b.RecitalDeepMilestoneInterval);

            GameSession s = new GameSession(db, GameSession.CreateNewProfile(db, Now));
            PlayerProfile p = s.Profile;
            for (int f = 1; f <= milestone; f++) s.GrantRecitalResult(f, true, Now);
            Check(p.RecitalHighestFloor == milestone && p.RecitalCurrentFloor == milestone + 1,
                  "(setup) climbing to floor " + milestone + " leaves the player on floor " + p.RecitalCurrentFloor);

            long dust = p.RelicDust, gold = p.Gold, coda = p.Coda;
            RecitalRewards again = s.GrantRecitalResult(milestone, true, Now + 60);
            Check(p.RelicDust == dust && again.RelicDust == 0,
                  "A RECITAL REMATCH REPAYS NO MILESTONE: winning floor " + milestone + " again pays " + (p.RelicDust - dust)
                  + " Relic Dust (its milestone paid " + b.RecitalMilestoneDustPerTier + " on the first clear)");
            Check(p.Gold > gold && p.Coda > coda && !again.AdvancedFloor && p.RecitalCurrentFloor == milestone + 1,
                  "and still pays the floor's own Gold, Shards and Coda, and moves no floor (+" + (p.Gold - gold) + " Gold, +" + (p.Coda - coda) + " Coda)");

            for (int f = milestone + 1; f <= deep; f++) s.GrantRecitalResult(f, true, Now + 120);
            int relics = p.OwnedRelics.Count;
            long dustAtDeep = p.RelicDust;
            RecitalRewards deepAgain = s.GrantRecitalResult(deep, true, Now + 180);
            Check(p.OwnedRelics.Count == relics && string.IsNullOrEmpty(deepAgain.GrantedRelicId) && p.RelicDust == dustAtDeep,
                  "nor a deep milestone: winning floor " + deep + " again grants " + (p.OwnedRelics.Count - relics) + " more relic(s) and "
                  + (p.RelicDust - dustAtDeep) + " Dust (" + (deepAgain.GrantedRelicId.Length > 0 ? deepAgain.GrantedRelicId : "none") + ")");

            // The first clear is untouched: a fresh climb pays the milestone exactly as before.
            GameSession fresh = new GameSession(db, GameSession.CreateNewProfile(db, Now));
            long before = 0;
            RecitalRewards first = null;
            for (int f = 1; f <= milestone; f++)
            {
                before = fresh.Profile.RelicDust;
                first = fresh.GrantRecitalResult(f, true, Now);
            }
            Check(first != null && first.IsMilestone && first.RelicDust == b.RecitalMilestoneDustPerTier && fresh.Profile.RelicDust - before == first.RelicDust,
                  "(control) the first clear of floor " + milestone + " still pays its milestone, " + (first != null ? first.RelicDust : 0) + " Relic Dust");
        }
    }
}
