using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Save;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// Runs the engine-free half of the game outside Unity: content validation, determinism
    /// checks, a save round-trip, a readable sample battle and a full campaign balance pass.
    /// Build with build.cmd, run the exe, read the report.
    /// </summary>
    internal static class Program
    {
        private static int _failures;

        private static int Main(string[] args)
        {
            string contentDir = args.Length > 0 ? args[0] : FindContentDir();

            Console.OutputEncoding = Encoding.UTF8;
            Line("EMBERFALL CORE HARNESS");
            Line("content: " + contentDir);
            Line("");

            ContentDatabase content = LoadContent(contentDir);
            if (content == null) return 1;

            CheckContent(content);
            CheckDeterminism(content);
            CheckSaveRoundTrip(content);
            PrintSampleBattle(content);
            RunCampaignBalancePass(content);

            Line("");
            if (_failures == 0) Line("RESULT: all checks passed");
            else Line("RESULT: " + _failures + " check(s) FAILED");
            return _failures == 0 ? 0 : 1;
        }

        // ---------------------------------------------------------------- helpers

        private static void Line(string text) { Console.WriteLine(text); }

        /// <summary>Walk up from the executable until Assets/Resources/Content shows up.</summary>
        private static string FindContentDir()
        {
            DirectoryInfo dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            for (int i = 0; i < 8 && dir != null; i++)
            {
                string candidate = Path.Combine(dir.FullName, Path.Combine("Assets", Path.Combine("Resources", "Content")));
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content");
        }

        private static void Check(bool condition, string label)
        {
            if (condition) { Line("  [ok]   " + label); return; }
            _failures++;
            Line("  [FAIL] " + label);
        }

        private static string Fmt(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
        private static string Pct(float v) { return (v * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%"; }

        private static ContentDatabase LoadContent(string dir)
        {
            try
            {
                string heroes = File.ReadAllText(Path.Combine(dir, "heroes.json"));
                string enemies = File.ReadAllText(Path.Combine(dir, "enemies.json"));
                string campaign = File.ReadAllText(Path.Combine(dir, "campaign.json"));
                string balance = File.ReadAllText(Path.Combine(dir, "balance.json"));
                return ContentDatabase.Load(heroes, enemies, campaign, balance);
            }
            catch (Exception ex)
            {
                _failures++;
                Line("  [FAIL] could not read content: " + ex.Message);
                return null;
            }
        }

        // ---------------------------------------------------------------- checks

        private static void CheckContent(ContentDatabase db)
        {
            Line("CONTENT");
            for (int i = 0; i < db.Warnings.Count; i++) Line("  warning: " + db.Warnings[i]);
            Check(db.Warnings.Count == 0, "content parses with no warnings");
            Check(db.Heroes.Count == 6, "6 heroes loaded (got " + db.Heroes.Count + ")");
            Check(db.Enemies.Count >= 6, "enemies loaded (got " + db.Enemies.Count + ")");
            Check(db.AllStages.Count >= 10, "campaign stages loaded (got " + db.AllStages.Count + ")");
            Check(db.StartingHeroes.Count == 3, "3 starting heroes (got " + db.StartingHeroes.Count + ")");

            bool everyRoleCovered = true;
            foreach (HeroRole role in Enum.GetValues(typeof(HeroRole)))
            {
                bool found = false;
                for (int i = 0; i < db.Heroes.Count; i++) if (db.Heroes[i].Role == role) found = true;
                if (!found) { everyRoleCovered = false; Line("  missing role: " + role); }
            }
            Check(everyRoleCovered, "every combat role has a hero");

            bool abilitiesOk = true;
            for (int i = 0; i < db.Heroes.Count; i++)
            {
                HeroDef h = db.Heroes[i];
                if (h.BasicAttack == null || h.Active == null || h.Passive == null)
                {
                    abilitiesOk = false;
                    Line("  hero " + h.Id + " is missing an ability");
                }
            }
            Check(abilitiesOk, "every hero has a basic attack, an active and a passive");
            Line("");
        }

        private static void CheckDeterminism(ContentDatabase db)
        {
            Line("DETERMINISM");
            GameSession session = new GameSession(db, GameSession.CreateNewProfile(db, 0));
            StageDef stage = db.AllStages[0];

            string a = BattleFingerprint(session, stage, 12345);
            string b = BattleFingerprint(session, stage, 12345);
            string c = BattleFingerprint(session, stage, 999);

            Check(a == b, "same seed produces an identical battle");
            Check(a != c, "different seeds produce different battles");

            BattleResult r = CombatSimulation.Run(session.BuildBattleRequest(stage, 12345));
            Check(r.Events.Count > 10, "battle produced an event stream (" + r.Events.Count + " events)");
            Check(r.Units.Count == 6, "3v3 battle built 6 units (got " + r.Units.Count + ")");
            Check(r.Rounds >= 1 && r.Rounds <= db.Balance.MaxRounds, "battle finished within the round limit (" + r.Rounds + ")");
            Line("");
        }

        private static string BattleFingerprint(GameSession session, StageDef stage, int seed)
        {
            BattleResult r = CombatSimulation.Run(session.BuildBattleRequest(stage, seed));
            StringBuilder sb = new StringBuilder();
            sb.Append(r.Victory).Append('|').Append(r.Rounds).Append('|').Append(r.Events.Count).Append('|');
            for (int i = 0; i < r.Events.Count; i++)
            {
                BattleEvent e = r.Events[i];
                sb.Append((int)e.Type).Append(',').Append(e.SourceId).Append(',')
                  .Append(e.TargetId).Append(',').Append(e.Value).Append(';');
            }
            return sb.ToString();
        }

        private static void CheckSaveRoundTrip(ContentDatabase db)
        {
            Line("SAVE");
            PlayerProfile profile = GameSession.CreateNewProfile(db, 1700000000);
            GameSession session = new GameSession(db, profile);

            session.LevelUpHero(profile.Heroes[0], 5);
            session.GrantBattleResult(db.AllStages[0], true, 1700000100);
            session.Summon(4242, 1700000200);

            string json = SaveSerializer.Serialize(profile);
            string error;
            PlayerProfile loaded = SaveSerializer.Deserialize(json, db, out error);

            Check(error == null, "save deserialises without error" + (error != null ? " (" + error + ")" : ""));
            Check(loaded != null, "save produced a profile");
            if (loaded == null) { Line(""); return; }

            Check(loaded.Gold == profile.Gold, "gold survives the round trip (" + loaded.Gold + " vs " + profile.Gold + ")");
            Check(loaded.Shards == profile.Shards, "shards survive the round trip");
            Check(loaded.Heroes.Count == profile.Heroes.Count, "hero count survives the round trip");
            Check(loaded.Heroes[0].Level == profile.Heroes[0].Level, "hero level survives the round trip");
            Check(loaded.StagesCleared == profile.StagesCleared, "campaign progress survives the round trip");
            Check(loaded.PlayerLevel == profile.PlayerLevel, "account level survives the round trip");

            bool teamOk = true;
            for (int i = 0; i < profile.Team.Length; i++) if (profile.Team[i] != loaded.Team[i]) teamOk = false;
            Check(teamOk, "team assignment survives the round trip");

            string error2;
            PlayerProfile fromGarbage = SaveSerializer.Deserialize("{ not json", db, out error2);
            Check(fromGarbage == null && error2 != null, "corrupt save is rejected instead of throwing");
            Line("");
        }

        private static void PrintSampleBattle(ContentDatabase db)
        {
            Line("SAMPLE BATTLE (stage 1-1, seed 7)");
            PlayerProfile profile = GameSession.CreateNewProfile(db, 0);
            GameSession session = new GameSession(db, profile);
            BattleResult r = CombatSimulation.Run(session.BuildBattleRequest(db.AllStages[0], 7));

            for (int i = 0; i < r.Units.Count; i++)
            {
                UnitSnapshot u = r.Units[i];
                Line("  " + (u.IsPlayer ? "ALLY  " : "ENEMY ") + u.Name.PadRight(20)
                     + " Lv" + u.Level.ToString().PadRight(3)
                     + " HP " + Fmt(u.MaxHp).PadLeft(6)
                     + "  ATK " + Fmt(u.Attack).PadLeft(5)
                     + "  DEF " + Fmt(u.Defense).PadLeft(5)
                     + "  SPD " + Fmt(u.Speed).PadLeft(5)
                     + "  PWR " + u.Power);
            }
            Line("  ---");

            int printed = 0;
            for (int i = 0; i < r.Events.Count && printed < 45; i++)
            {
                string text = DescribeEvent(r, r.Events[i]);
                if (text == null) continue;
                Line("  " + text);
                printed++;
            }
            if (printed >= 45) Line("  ... (" + r.Events.Count + " events total)");
            Line("  => " + (r.Victory ? "VICTORY" : "DEFEAT") + " in " + r.Rounds + " rounds"
                 + (r.TimedOut ? " (timed out)" : "")
                 + "   ally hp " + Pct(r.PlayerHpRatio) + ", enemy hp " + Pct(r.EnemyHpRatio));
            Line("");
        }

        private static string DescribeEvent(BattleResult r, BattleEvent e)
        {
            string source = NameOf(r, e.SourceId);
            string target = NameOf(r, e.TargetId);
            switch (e.Type)
            {
                case BattleEventType.RoundStart: return "-- round " + e.Value + " --";
                case BattleEventType.AbilityCast: return source + " casts " + e.AbilityName;
                case BattleEventType.PassiveTrigger: return source + "'s " + e.AbilityName + " triggers";
                case BattleEventType.Damage:
                    return "   " + target + " takes " + e.Value + (e.Critical ? " CRIT" : "")
                           + (e.Status == StatusKind.Burn ? " burn" : "") + " damage";
                case BattleEventType.Heal: return "   " + target + " heals " + e.Value;
                case BattleEventType.ShieldGain: return "   " + target + " wards " + e.Value;
                case BattleEventType.ShieldAbsorb: return "   " + target + "'s ward absorbs " + e.Value;
                case BattleEventType.StatusApplied: return "   " + target + " gains " + StatusInfo.DisplayName(e.Status) + " (" + e.Duration + ")";
                case BattleEventType.Death: return "   " + target + " falls";
                case BattleEventType.BattleEnd: return "== " + (e.Victory ? "victory" : "defeat") + " ==";
            }
            return null;
        }

        private static string NameOf(BattleResult r, int id)
        {
            UnitSnapshot u = r.FindUnit(id);
            return u != null ? u.Name : "?";
        }

        // ---------------------------------------------------------------- balance pass

        private static void RunCampaignBalancePass(ContentDatabase db)
        {
            Line("CAMPAIGN BALANCE (simulated player, 1 hour idle between sessions)");
            Line("  stage  name                      teamPwr  recPwr   winRate  avgRounds  attempts");

            PlayerProfile profile = GameSession.CreateNewProfile(db, 0);
            GameSession session = new GameSession(db, profile);
            long now = 0;
            int totalAttempts = 0;
            int seedCounter = 1;
            int stuckAt = -1;

            for (int stageIdx = 0; stageIdx < db.AllStages.Count; stageIdx++)
            {
                StageDef stage = db.AllStages[stageIdx];
                int attemptsForStage = 0;
                int wins = 0;
                int rounds = 0;
                int samples = 0;
                bool cleared = false;

                while (attemptsForStage < 60)
                {
                    // a session: collect idle income, spend gold on the team, then fight
                    now += 3600;
                    session.ClaimIdle(now);
                    SpendGold(session);
                    while (session.CanSummon) session.Summon(seedCounter++, now);
                    session.Profile.NormaliseTeam();

                    BattleResult r = CombatSimulation.Run(session.BuildBattleRequest(stage, seedCounter++));
                    attemptsForStage++;
                    totalAttempts++;
                    samples++;
                    rounds += r.Rounds;
                    if (r.Victory) wins++;

                    if (r.Victory)
                    {
                        session.GrantBattleResult(stage, true, now);
                        cleared = true;
                        break;
                    }
                    // losing still means replaying the best cleared stage for gold
                    if (stageIdx > 0)
                    {
                        StageDef farm = db.AllStages[stageIdx - 1];
                        BattleResult fr = CombatSimulation.Run(session.BuildBattleRequest(farm, seedCounter++));
                        if (fr.Victory) session.GrantBattleResult(farm, true, now);
                    }
                }

                // measure the true win rate at the power the player actually had
                int measureWins = 0;
                const int MeasureRuns = 24;
                for (int m = 0; m < MeasureRuns; m++)
                {
                    BattleResult mr = CombatSimulation.Run(session.BuildBattleRequest(stage, 100000 + m * 37 + stageIdx));
                    if (mr.Victory) measureWins++;
                }

                float winRate = measureWins / (float)MeasureRuns;
                float avgRounds = samples > 0 ? rounds / (float)samples : 0f;
                Line("  " + stage.DisplayCode.PadRight(6)
                     + " " + Trim(stage.Name, 25).PadRight(25)
                     + " " + profile.TeamPower().ToString().PadLeft(7)
                     + "  " + stage.RecommendedPower.ToString().PadLeft(6)
                     + "   " + Pct(winRate).PadLeft(6)
                     + "  " + Fmt(avgRounds).PadLeft(9)
                     + "  " + attemptsForStage.ToString().PadLeft(8)
                     + (cleared ? "" : "   <-- STUCK"));

                if (!cleared) { stuckAt = stageIdx; break; }
            }

            Line("");
            Line("  final: rank " + profile.PlayerLevel
                 + ", " + profile.StagesCleared + "/" + db.AllStages.Count + " stages"
                 + ", " + profile.Heroes.Count + " heroes"
                 + ", gold " + profile.Gold
                 + ", shards " + profile.Shards
                 + ", hero cap Lv" + session.HeroLevelCap
                 + ", total battles " + totalAttempts);

            for (int i = 0; i < profile.Heroes.Count; i++)
            {
                HeroInstance h = profile.Heroes[i];
                Line("    " + (profile.IsDeployed(h.Uid) ? "*" : " ") + " "
                     + h.DisplayName.PadRight(20) + " Lv" + h.Level.ToString().PadLeft(2)
                     + "  power " + h.Power.ToString().PadLeft(6)
                     + "  " + h.Def.Role + "/" + h.Def.Aspect + "  " + (int)h.Def.Rarity + "*");
            }

            Check(stuckAt < 0, "a normal player can clear the whole campaign");
            Check(profile.Heroes.Count > 3, "summoning grew the roster past the 3 starters");
            Line("");
        }

        private static string Trim(string s, int max)
        {
            if (s == null) return "";
            return s.Length <= max ? s : s.Substring(0, max);
        }

        /// <summary>Simple bot: always pour gold into the cheapest upgrade on the deployed team.</summary>
        private static void SpendGold(GameSession session)
        {
            for (int guard = 0; guard < 500; guard++)
            {
                List<HeroInstance> team = session.Profile.TeamHeroes();
                HeroInstance cheapest = null;
                int cheapestCost = int.MaxValue;
                for (int i = 0; i < team.Count; i++)
                {
                    HeroInstance h = team[i];
                    if (h.Level >= session.HeroLevelCap) continue;
                    int cost = session.LevelUpCost(h);
                    if (cost < cheapestCost) { cheapestCost = cost; cheapest = h; }
                }
                if (cheapest == null || session.Profile.Gold < cheapestCost) return;
                session.LevelUpHero(cheapest, 1);
            }
        }
    }
}
