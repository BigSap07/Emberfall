using System;
using System.Collections.Generic;
using System.IO;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE WALL CHESTS (R17 task 5, wall-chests.json): a one-time chest on stage 6 and stage 12 of
    /// every chapter, opened on the first clear and shown on the stage card before the fight.
    ///
    /// One named check per rule: the shipped content and the card line, the payout and its
    /// one-time-ness, the rollback (the switch off pays exactly what a database without the file
    /// pays), and the file staying out of ContentVersion. The curve walk is the existing
    /// CheckFusionNeverGates and the economy guards: the walk clears every stage through
    /// GrantBattleResult, so the chests flow into it without any special case.
    /// </summary>
    internal static partial class Program
    {
        private const long ChestNow = 1760000000L;

        private static string ChestJson(bool enabled)
        {
            return "{ \"enabled\": " + (enabled ? "true" : "false") + ", \"wallStageIndex\": 6, \"finaleStageIndex\": 12,"
                   + " \"wall\": { \"hours\": 1, \"slagPerChapter\": 12 },"
                   + " \"finale\": { \"hours\": 3, \"slagPerChapter\": 30 } }";
        }

        private static ContentDatabase LoadWallChestContent(string contentDir, string chestJson)
        {
            ContentDatabase db = LoadContent(contentDir);
            if (db != null) db.LoadWallChests(chestJson);
            return db;
        }

        private static void ClearThrough(GameSession session, ContentDatabase db, int count, List<BattleRewards> sink)
        {
            for (int i = 0; i < count && i < db.AllStages.Count; i++)
            {
                BattleRewards r = session.GrantBattleResult(db.AllStages[i], true, ChestNow + i);
                if (sink != null) sink.Add(r);
            }
        }

        private static void CheckWallChestsContentAndCard(ContentDatabase live, string contentDir)
        {
            Line("WALL CHESTS (content and the stage card)");
            string path = Path.Combine(contentDir, "wall-chests.json");
            string shipped = File.Exists(path) ? File.ReadAllText(path) : "";
            // NEVER MUTATE `live`: it is Main's shared content and every later walk reads it. The shipped
            // default is read from a throwaway database, and so is the enabled variant below.
            ContentDatabase shippedDb = LoadWallChestContent(contentDir, shipped);
            WallChestConfig shippedCfg = shippedDb.WallChests;

            // THE SHIPPED DEFAULT IS ON, at 1 hour (wall) and 3 hours (finale) of Gold. At 1.5 and 4 hours the
            // reference walk 0's power band went over x1.25 at 2-7, 3-4 and 3-6; at 1 and 3 it does not.
            Check(shippedCfg.Warnings.Count == 0, shippedCfg.Warnings.Count == 0 ? "wall-chests.json parses without a warning"
                  : "wall-chests.json warns: " + string.Join("; ", shippedCfg.Warnings.ToArray()));
            Check(shippedCfg.Enabled && shippedCfg.IsUsable && shippedCfg.Wall.Hours == 1.0 && shippedCfg.Finale.Hours == 3.0,
                  "the shipped switch is ON, the wall chest is 1 hour and the finale chest 3 hours (found wall "
                  + shippedCfg.Wall.Hours + " h, finale " + shippedCfg.Finale.Hours + " h)");

            // Exactly the 12 wall and finale stages show a card line, and it says the new hours.
            ContentDatabase on = shippedDb;
            WallChestConfig cfg = shippedCfg;
            GameSession cardSession = new GameSession(on, GameSession.CreateNewProfile(on, 0));
            int shown = 0, wrongHours = 0;
            for (int i = 0; i < on.AllStages.Count; i++)
            {
                StageDef st = on.AllStages[i];
                WallChestView v = WallChests.Preview(cardSession, st);
                if (v.Present || v.Line.Length != 0) shown++;
                if (v.Present && v.Line.IndexOf(v.Finale ? "3 hours of Gold" : "1 hour of Gold", StringComparison.Ordinal) < 0) wrongHours++;
            }
            Check(shown == 12 && wrongHours == 0,
                  "with the shipped default exactly 12 stages show a chest line (" + shown + " do) and each reads 1 hour (wall) or 3 hours (finale) of Gold ("
                  + wrongHours + " do not)");

            // The switch off still draws nothing and pays nothing: the rollback, on a throwaway database.
            ContentDatabase offDb = LoadWallChestContent(contentDir, shipped.Replace("\"enabled\": true", "\"enabled\": false"));
            GameSession offSession = new GameSession(offDb, GameSession.CreateNewProfile(offDb, 0));
            int offShown = 0;
            for (int i = 0; i < offDb.AllStages.Count; i++)
                if (WallChests.Preview(offSession, offDb.AllStages[i]).Present) offShown++;
            Check(offDb.WallChests.IsUsable == false && offShown == 0, "and with the switch turned off no stage shows a chest (" + offShown + ")");
            Check(cfg.Finale.Hours > cfg.Wall.Hours && cfg.Finale.SlagPerChapter > cfg.Wall.SlagPerChapter,
                  "the finale chest is larger than the wall chest in both hours (" + cfg.Finale.Hours + " against "
                  + cfg.Wall.Hours + ") and Slag per chapter (" + cfg.Finale.SlagPerChapter + " against "
                  + cfg.Wall.SlagPerChapter + ")");

            GameSession session = new GameSession(on, GameSession.CreateNewProfile(on, 0));
            int walls = 0, finales = 0, chapters = 0;
            List<string> wrong = new List<string>();
            HashSet<string> seenChapters = new HashSet<string>();
            for (int i = 0; i < on.AllStages.Count; i++)
            {
                StageDef st = on.AllStages[i];
                if (seenChapters.Add(st.ChapterId)) chapters++;
                WallChestView v = WallChests.Preview(session, st);
                bool expectWall = st.Index == 6, expectFinale = st.Index == 12;
                if (v.Present != (expectWall || expectFinale)) { wrong.Add(st.DisplayCode + " present=" + v.Present); continue; }
                if (!v.Present) { if (v.Line.Length != 0) wrong.Add(st.DisplayCode + " has a card line without a chest"); continue; }
                if (v.Finale != expectFinale) wrong.Add(st.DisplayCode + " finale flag wrong");
                if (v.Opened) wrong.Add(st.DisplayCode + " reads opened on a fresh profile");
                if (v.Line.IndexOf("Slag", StringComparison.Ordinal) < 0 || v.Line.IndexOf("hour", StringComparison.Ordinal) < 0
                    || v.Line.IndexOf("of Gold", StringComparison.Ordinal) < 0 || v.Line.IndexOf("hours of income", StringComparison.Ordinal) >= 0
                    || v.Line.IndexOf("Shard", StringComparison.Ordinal) >= 0)
                    wrong.Add(st.DisplayCode + " card line must say hours of Gold and Slag and name no Shards: " + v.Line);
                if (expectWall) walls++; else finales++;
            }
            Check(wrong.Count == 0 && walls == chapters && finales == chapters,
                  wrong.Count == 0 && walls == chapters && finales == chapters
                    ? "every one of the " + chapters + " chapters carries exactly one wall chest (stage 6) and one finale chest (stage 12), and no other stage shows one"
                    : "chest layout wrong (walls " + walls + ", finales " + finales + ", chapters " + chapters + "): "
                      + string.Join("; ", wrong.ToArray()));

            // Deeper chapters pay more Slag, and the card says so before the fight.
            StageDef first = on.AllStages[5], last = on.AllStages[on.AllStages.Count - 1];
            WallChestView a = WallChests.Preview(session, first), b = WallChests.Preview(session, last);
            Check(a.Present && b.Present && b.Slag > a.Slag,
                  "Slag grows with the chapter: " + first.DisplayCode + " holds " + a.Slag + ", " + last.DisplayCode + " holds " + b.Slag);
            Line("  [note] card line, first wall: " + a.Line);
            Line("  [note] card line, last finale: " + b.Line);

            // The file is read by no fight, so it cannot move ContentVersion.
            ContentDatabase without = LoadWallChestContent(contentDir, "");
            ContentDatabase with = LoadWallChestContent(contentDir, ChestJson(true));
            Check(without != null && with != null && without.ContentVersion == with.ContentVersion,
                  "wall-chests.json is outside ContentVersion: " + (with == null ? "?" : with.ContentVersion)
                  + " with the file and without it");
            Line("");
        }

        private static void CheckWallChestsPayOnce(ContentDatabase live, string contentDir)
        {
            Line("WALL CHESTS (paid on the first clear, once)");
            ContentDatabase db = LoadWallChestContent(contentDir, ChestJson(true));
            GameSession session = new GameSession(db, GameSession.CreateNewProfile(db, 0));
            PlayerProfile p = session.Profile;

            // Stages 1 to 5: no chest, no Slag.
            List<BattleRewards> early = new List<BattleRewards>();
            ClearThrough(session, db, 5, early);
            bool noneEarly = true;
            for (int i = 0; i < early.Count; i++) if (early[i].Chest != null) noneEarly = false;
            Check(noneEarly && p.Slag == 0, "stages 1 to 5 open no chest and pay no Slag (Slag " + p.Slag + ")");

            StageDef six = db.AllStages[5];
            long goldBefore = p.Gold, shardsBefore = p.Shards, slagBefore = p.Slag;
            BattleRewards r = session.GrantBattleResult(six, true, ChestNow + 100);
            WallChestView chest = r.Chest;
            Check(chest != null && chest.Present && !chest.Finale && chest.Opened, "the first clear of " + six.DisplayCode + " opens the wall chest");
            if (chest != null)
            {
                IdleReport expect = session.IdleYield((long)Math.Round(db.WallChests.Wall.Hours * 3600.0));
                Check(chest.Gold == expect.Gold && chest.Gold > 0 && expect.Shards > 0,
                      "it pays " + db.WallChests.Wall.Hours + " hours of Gold at the frontier it reached: " + chest.Gold
                      + " gold (the idle formula's Gold half says " + expect.Gold + ")");
                Check(p.Gold == goldBefore + r.Gold + chest.Gold && p.Shards == shardsBefore + r.Shards
                      && p.Slag == slagBefore + 12 * six.ChapterNumber,
                      "GOLD AND SLAG ARE PAID AND SHARDS ARE NOT: the Gold purse moved by the stage reward plus the chest, the Shards purse by the stage reward alone (the chest holds none, though "
                      + expect.Shards + " Shards of idle income were on offer), and Slag by 12 x chapter " + six.ChapterNumber + " = " + (p.Slag - slagBefore));
            }

            // Once. Clearing it again pays the repeat purse and no chest.
            long slagAfter = p.Slag;
            BattleRewards again = session.GrantBattleResult(six, true, ChestNow + 200);
            Check(again.Chest == null && p.Slag == slagAfter, "a second clear of " + six.DisplayCode + " opens nothing: the chest is one-time");
            Check(WallChests.Preview(session, six).Opened && WallChests.Preview(session, six).Line.Contains("opened"),
                  "and the stage card now reads the chest as opened");

            // Stage 12 is the finale: larger.
            ClearThrough(session, db, 11, null);
            long slagBeforeFinale = p.Slag;
            BattleRewards fin = session.GrantBattleResult(db.AllStages[11], true, ChestNow + 300);
            Check(fin.Chest != null && fin.Chest.Finale && fin.Chest.Hours > chest.Hours && p.Slag - slagBeforeFinale == 30 * db.AllStages[11].ChapterNumber,
                  "the first clear of " + db.AllStages[11].DisplayCode + " opens the finale chest, larger than the wall's (Slag +"
                  + (p.Slag - slagBeforeFinale) + ")");
            Line("");
        }

        private static void CheckWallChestsRollBack(ContentDatabase live, string contentDir)
        {
            Line("WALL CHESTS (rollback: switch off equals before)");
            ContentDatabase before = LoadWallChestContent(contentDir, "");
            ContentDatabase off = LoadWallChestContent(contentDir, ChestJson(false));
            ContentDatabase on = LoadWallChestContent(contentDir, ChestJson(true));

            GameSession sBefore = new GameSession(before, GameSession.CreateNewProfile(before, 0));
            GameSession sOff = new GameSession(off, GameSession.CreateNewProfile(off, 0));
            GameSession sOn = new GameSession(on, GameSession.CreateNewProfile(on, 0));
            int n = Math.Min(24, before.AllStages.Count);
            List<BattleRewards> rBefore = new List<BattleRewards>(), rOff = new List<BattleRewards>(), rOn = new List<BattleRewards>();
            ClearThrough(sBefore, before, n, rBefore);
            ClearThrough(sOff, off, n, rOff);
            ClearThrough(sOn, on, n, rOn);

            bool same = true;
            string why = "";
            for (int i = 0; i < n && same; i++)
            {
                if (rBefore[i].Gold != rOff[i].Gold || rBefore[i].Shards != rOff[i].Shards || rOff[i].Chest != null)
                { same = false; why = "stage " + (i + 1); }
            }
            PlayerProfile pb = sBefore.Profile, po = sOff.Profile, pn = sOn.Profile;
            same = same && pb.Gold == po.Gold && pb.Shards == po.Shards && pb.Slag == po.Slag
                   && pb.ClearedStageIds.Count == po.ClearedStageIds.Count;
            Check(same, same ? "with the switch off, " + n + " first clears pay exactly what a database without the file pays (gold "
                              + po.Gold + ", shards " + po.Shards + ", Slag " + po.Slag + ")"
                            : "switch off differs from before at " + why);
            Check(pn.Slag > po.Slag && pn.Gold > po.Gold && pn.Shards == po.Shards,
                  "and with it on the same " + n + " clears are richer in Gold and Slag and exactly as rich in Shards: gold " + pn.Gold + ", shards " + pn.Shards + " (" + po.Shards + " off), Slag " + pn.Slag);
            Line("");
        }
    }
}
