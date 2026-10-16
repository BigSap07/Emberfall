using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Emberfall.Core.Data;
using Emberfall.Core.Json;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Save;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE FIRST SESSION AND THE UNLOCK TABLE (DESIGN-ONBOARDING.md), as checks.
    ///
    /// Three groups. CheckOnboarding drives Onboarding the way the coach does and reads the save
    /// it leaves: a new player gets Kael exactly once, SKIP still gives him, a quit resumes at the
    /// step after the last completed one, a progressed save never sees the flow, and the recall
    /// touches neither the summon pool nor the pity count. CheckFeatureUnlockGating enumerates
    /// every entry point to every feature and proves each against the one table.
    /// CheckWalksRespectUnlocks reads the economy walk and the pinned lines for what they spent
    /// before each feature opened.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>How many times an instrument line was read at a stage before armor opens and
        /// so wore nothing. Counted by EquipInstrumentArmor; read by CheckWalksRespectUnlocks.</summary>
        private static int _armorWithheldReadings;

        /// <summary>The table as the owner decided it, 2026-10-12. Moving a row is a decision, so
        /// this pins it: a moved row fails here until this copy moves with it.</summary>
        private static readonly string[][] DecidedUnlocks =
        {
            new[] { "Campaign", "" }, new[] { "Summon", "" }, new[] { "Heroes", "" }, new[] { "Idle", "" },
            new[] { "Friends", "ch1_4" }, new[] { "Fusion", "ch1_6" },
            new[] { "Talents", "ch1_12" }, new[] { "Armor", "ch1_12" }, new[] { "Forge", "ch1_12" },
            new[] { "Guilds", "ch2_1" }, new[] { "Proving", "ch2_4" }, new[] { "Recital", "ch2_8" },
            new[] { "Relics", "ch3_1" }
        };

        private const long OnboardingNow = 1760000000L;

        private static void CheckOnboarding(ContentDatabase db)
        {
            Line("");
            Line("ONBOARDING (DESIGN-ONBOARDING.md: the Keeper wakes, the Cinder recalls Kael, five guided steps)");

            OnboardingConfig cfg = db.Onboarding;
            Check(cfg.IsUsable && cfg.Warnings.Count == 0,
                  "onboarding.json loads with an unlock table and no warnings (" + cfg.Warnings.Count + " warnings)");
            HeroDef kael = Onboarding.RecallHero(db);
            Check(kael != null && kael.Id == "kael_the_unyielding",
                  "the Cinder recalls Kael (" + (kael != null ? kael.Id : "none") + ")");
            Check(cfg.WakeLines.Count == 3 && cfg.Steps.Count == Onboarding.GuidedSteps && cfg.PrologueLine.Length > 0,
                  "the three wake lines, the five guided steps and the prologue's line are content (" + cfg.WakeLines.Count
                  + " lines, " + cfg.Steps.Count + " steps)");
            if (kael == null) return;

            // ---- a new save runs the scripted flow and gets Kael exactly once
            PlayerProfile keeper = Onboarding.CreateKeeperProfile(db, OnboardingNow);
            GameSession session = new GameSession(db, keeper);
            Check(keeper.TutorialStep == Onboarding.StepWake && !keeper.Owns(kael.Id) && !Onboarding.IsComplete(keeper),
                  "a new player starts at the wake, without Kael: the Cinder is where he comes from");
            int summonsBefore = keeper.TotalSummons;
            long shardsBefore = keeper.Shards, goldBefore = keeper.Gold;

            Check(!Onboarding.CompleteStep(keeper, Onboarding.StepRecall) && keeper.TutorialStep == Onboarding.StepWake,
                  "a step cannot be completed out of turn: recall before the wake changes nothing");
            Onboarding.CompleteStep(keeper, Onboarding.StepWake);
            RecallOutcome recall = Onboarding.Recall(session, OnboardingNow);
            RecallOutcome again = Onboarding.Recall(session, OnboardingNow);
            Check(recall.Granted && recall.Hero != null && recall.Hero.DefId == kael.Id && !again.Granted
                  && CountOf(keeper, kael.Id) == 1 && keeper.FindHeroByDefId(kael.Id).Copies == 1,
                  "RECALL gives Kael once; a second recall gives nothing and adds no copy");
            Check(keeper.TutorialStep == Onboarding.StepPrologue, "and the recall step is saved as complete the moment it finishes");

            PlayerProfile reference = GameSession.CreateNewProfile(db, OnboardingNow);
            reference.TutorialStep = keeper.TutorialStep;
            Check(SaveSerializer.Serialize(reference) == SaveSerializer.Serialize(keeper),
                  "after the recall the save is byte for byte the ordinary new profile: Kael's uid, his place in the roster and the starting banner");

            // ---- the summon pool and the pity state are untouched by the recall
            Check(keeper.TotalSummons == summonsBefore && keeper.Shards == shardsBefore && keeper.Gold == goldBefore,
                  "the recall spends no shards and no gold and counts no summon (TotalSummons " + keeper.TotalSummons + ")");
            GameSession refSession = new GameSession(db, reference);
            bool samePulls = true;
            for (int i = 0; i < GameSession.PityGuaranteedSummons + 2; i++)
            {
                keeper.Shards = reference.Shards = db.Balance.SummonCost;
                SummonOutcome a = session.Summon(9100 + i, OnboardingNow);
                SummonOutcome b = refSession.Summon(9100 + i, OnboardingNow);
                if (!a.Success || !b.Success || a.Hero.Id != b.Hero.Id || a.IsNew != b.IsNew) samePulls = false;
            }
            Check(samePulls && keeper.TotalSummons == reference.TotalSummons,
                  "and the pulls after it match an ordinary new profile's pull for pull, the three pity pulls included: same pool, same pity count");

            // ---- the rest of the flow, step by step, through the game's own calls
            PlayerProfile walker = Onboarding.CreateKeeperProfile(db, OnboardingNow);
            GameSession ws = new GameSession(db, walker);
            Onboarding.CompleteStep(walker, Onboarding.StepWake);
            Onboarding.Recall(ws, OnboardingNow);
            Onboarding.CompleteStep(walker, Onboarding.StepPrologue);
            Check(Onboarding.GuidedIndex(walker) == 0, "the prologue hands over to guided step 1 of 5");
            Check(walker.IsDeployed(walker.FindHeroByDefId(kael.Id).Uid),
                  "the team step's default holds: Kael already stands in the line with nothing done");
            walker.ClearTeamSlot(walker.TeamSlotOf(walker.FindHeroByDefId(kael.Id).Uid));
            Check(Onboarding.EnsureRecallHeroDeployed(ws) && walker.IsDeployed(walker.FindHeroByDefId(kael.Id).Uid),
                  "and taken off the banner, he is placed again automatically when the step completes");
            Onboarding.CompleteStep(walker, Onboarding.StepTeam);
            StageDef first = db.AllStages[0];
            int resolves = 0;
            while (!walker.HasClearedStage(first.Id) && resolves < 12)
            {
                ws.ResolveWave(first, 7100 + resolves * 101, OnboardingNow + resolves);
                if (resolves == 0) Onboarding.CompleteStep(walker, Onboarding.StepCampaign);
                resolves++;
            }
            Check(walker.HasClearedStage(first.Id),
                  "the recalled line clears " + first.DisplayCode + " wave by wave (" + resolves + " resolves)");
            Onboarding.CompleteStep(walker, Onboarding.StepWaveResult);
            HeroInstance kh = walker.FindHeroByDefId(kael.Id);
            long goldAfterFirst = walker.Gold;
            int cost = ws.LevelUpCost(kh);
            Check(!Onboarding.CanLevelAnyone(ws),
                  "FINDING, not a gate on the flow: after " + first.DisplayCode + " a new player holds " + goldAfterFirst
                  + " gold against a first level of " + cost + " on every starter, so the level-up step routes on to the next stage first");
            StageDef second = db.AllStages[1];
            int more = 0;
            while (!walker.HasClearedStage(second.Id) && more < 12)
            {
                ws.ResolveWave(second, 7900 + more * 101, OnboardingNow + 100 + more);
                more++;
            }
            Check(Onboarding.CanLevelAnyone(ws),
                  "and after " + second.DisplayCode + " (" + walker.Gold + " gold) a level is affordable, so the step can always be finished");
            int levelBefore = kh.Level;
            LevelUpResult up = ws.LevelUpHero(kh, 1);
            Check(kh.Level > levelBefore, "the level-up step is a real level bought with real gold (Kael " + levelBefore + " to " + kh.Level
                  + (up.Message != null && up.Message.Length > 0 ? ", " + up.Message : "") + ")");
            Onboarding.CompleteStep(walker, Onboarding.StepLevelUp);
            ws.ClaimIdle(OnboardingNow + 3600);
            Onboarding.CompleteStep(walker, Onboarding.StepIdle);
            Check(Onboarding.IsComplete(walker) && CountOf(walker, kael.Id) == 1 && walker.FindHeroByDefId(kael.Id).Copies == 1,
                  "the whole flow ends complete with Kael owned exactly once");

            // ---- SKIP grants Kael and completes, from any step
            bool skipAll = true;
            for (int at = Onboarding.StepWake; at < Onboarding.Complete; at++)
            {
                PlayerProfile p = Onboarding.CreateKeeperProfile(db, OnboardingNow);
                GameSession s = new GameSession(db, p);
                for (int k = 0; k < at; k++)
                {
                    if (k == Onboarding.StepRecall) Onboarding.Recall(s, OnboardingNow);
                    else Onboarding.CompleteStep(p, k);
                }
                Onboarding.Skip(s, OnboardingNow);
                HeroInstance hk = p.FindHeroByDefId(kael.Id);
                if (!Onboarding.IsComplete(p) || CountOf(p, kael.Id) != 1 || hk.Copies != 1 || !p.IsDeployed(hk.Uid)
                    || p.TotalSummons != 0 || PlayerProfile.HasSeen(p, "Summon"))
                    skipAll = false;
            }
            Check(skipAll, "SKIP from each of the eight steps ends the flow complete, with Kael owned once and deployed, no summon counted, and the free-play tips still to come");

            // ---- resume after quitting mid-step
            PlayerProfile quitter = Onboarding.CreateKeeperProfile(db, OnboardingNow);
            GameSession qs = new GameSession(db, quitter);
            Onboarding.CompleteStep(quitter, Onboarding.StepWake);
            Onboarding.Recall(qs, OnboardingNow);
            Onboarding.CompleteStep(quitter, Onboarding.StepPrologue);
            Onboarding.CompleteStep(quitter, Onboarding.StepTeam);
            Onboarding.MarkSeen(quitter, "Formation");
            string err;
            PlayerProfile resumed = SaveSerializer.Deserialize(SaveSerializer.Serialize(quitter), db, out err);
            Check(resumed != null && resumed.TutorialStep == Onboarding.StepCampaign && Onboarding.GuidedIndex(resumed) == 1
                  && CountOf(resumed, kael.Id) == 1 && PlayerProfile.HasSeen(resumed, "Formation") && !PlayerProfile.HasSeen(resumed, "Summon"),
                  "quitting mid-flow after the team step resumes at the campaign step, guided pip 2 of 5, with Kael and the seen tips kept");
            PlayerProfile atWake = SaveSerializer.Deserialize(SaveSerializer.Serialize(Onboarding.CreateKeeperProfile(db, OnboardingNow)), db, out err);
            Check(atWake != null && atWake.TutorialStep == Onboarding.StepWake && !atWake.Owns(kael.Id),
                  "a player who quits at the wake comes back to the wake, still without Kael");

            // ---- an existing progressed save never sees the tutorial (the version 21 migration)
            PlayerProfile old = GameSession.CreateNewProfile(db, OnboardingNow);
            old.ClearedStageIds.Add(first.Id);
            PlayerProfile oldLoaded = SaveSerializer.Deserialize(AsVersion20(SaveSerializer.Serialize(old)), db, out err);
            Check(oldLoaded != null && Onboarding.IsComplete(oldLoaded) && PlayerProfile.HasSeen(oldLoaded, "Summon")
                  && PlayerProfile.HasSeen(oldLoaded, "SomeScreenAddedLater"),
                  "a version 20 save with a cleared stage loads tutorial-complete with every tip seen, later ones included");
            PlayerProfile midStage = GameSession.CreateNewProfile(db, OnboardingNow);
            midStage.CampaignCurrentStageId = first.Id;
            PlayerProfile midLoaded = SaveSerializer.Deserialize(AsVersion20(SaveSerializer.Serialize(midStage)), db, out err);
            Check(midLoaded != null && Onboarding.IsComplete(midLoaded),
                  "and so does one with a stage in progress and nothing cleared");
            PlayerProfile fresh20 = SaveSerializer.Deserialize(AsVersion20(SaveSerializer.Serialize(GameSession.CreateNewProfile(db, OnboardingNow))), db, out err);
            Check(fresh20 != null && fresh20.TutorialStep == Onboarding.StepWake && fresh20.Owns(kael.Id),
                  "a version 20 save with no progress starts at the wake, already owning Kael");
            if (fresh20 != null)
            {
                GameSession fs = new GameSession(db, fresh20);
                Onboarding.CompleteStep(fresh20, Onboarding.StepWake);
                RecallOutcome r20 = Onboarding.Recall(fs, OnboardingNow);
                Check(!r20.Granted && CountOf(fresh20, kael.Id) == 1 && fresh20.FindHeroByDefId(kael.Id).Copies == 1
                      && fresh20.TutorialStep == Onboarding.StepPrologue,
                      "and its recall gives nothing twice: no copy, no second Kael, the step still advances");
            }
            PlayerProfile v21 = GameSession.CreateNewProfile(db, OnboardingNow);
            v21.ClearedStageIds.Add(first.Id);
            v21.TutorialStep = Onboarding.StepLevelUp;
            PlayerProfile v21Loaded = SaveSerializer.Deserialize(SaveSerializer.Serialize(v21), db, out err);
            Check(v21Loaded != null && v21Loaded.TutorialStep == Onboarding.StepLevelUp,
                  "the migration is conditioned on the version: a version 21 save mid-flow with a cleared stage keeps its step");
            Check(SaveSerializer.CurrentVersion == 21, "the save is at version 21 (" + SaveSerializer.CurrentVersion + ")");

            // ---- the prologue step is the existing battle, unchanged
            PrologueScript.Step step = Onboarding.PrologueStep(db);
            List<HeroInstance> squad = PrologueScript.BuildSquad(db);
            Check(squad != null && squad.Count > 0 && PrologueScript.BuildStage(step).Enemies.Count > 0,
                  "the opening fights PrologueScript's " + step + " step with its own squad of " + (squad != null ? squad.Count : 0));
        }

        private static int CountOf(PlayerProfile p, string defId)
        {
            int n = 0;
            for (int i = 0; i < p.Heroes.Count; i++) if (p.Heroes[i].DefId == defId) n++;
            return n;
        }

        /// <summary>A save as version 20 wrote it: the version number, and neither version 21 key.</summary>
        private static string AsVersion20(string json)
        {
            JsonValue root = JsonValue.Parse(json);
            JsonValue old = JsonValue.NewObject();
            foreach (string key in root.Keys)
                if (key != "tutorialStep" && key != "seenTips") old.Set(key, root[key]);
            old.Set("version", 20);
            return old.ToJson(false);
        }

        // ------------------------------------------------------------------ the unlock table

        private static void CheckFeatureUnlockGating(ContentDatabase db, string contentDir)
        {
            Line("");
            Line("FEATURE UNLOCKS (DESIGN-ONBOARDING.md: locked, not hidden; one table, every entry point)");

            // ---- the table is the owner's, and it is complete
            bool decided = db.Onboarding.Unlocks.Count == DecidedUnlocks.Length;
            for (int i = 0; i < DecidedUnlocks.Length; i++)
            {
                Feature f = (Feature)Enum.Parse(typeof(Feature), DecidedUnlocks[i][0]);
                FeatureUnlockDef row = db.Onboarding.UnlockOf(f);
                if (row == null || row.StageId != DecidedUnlocks[i][1]) decided = false;
                if (DecidedUnlocks[i][1].Length > 0 && db.GetStage(DecidedUnlocks[i][1]) == null) decided = false;
            }
            Check(decided && Enum.GetValues(typeof(Feature)).Length == DecidedUnlocks.Length,
                  "onboarding.json's table is the decided one, one row per feature: Friends 1-4, Fusion 1-6, Talents, Armor and Forge 1-12, Guilds 2-1, Proving 2-4, Recital 2-8, Relics 3-1, the rest from the start");
            Check(FeatureGate.LockLine(db, Feature.Fusion) == "UNLOCKS AT 1-6" && FeatureGate.LockLine(db, Feature.Campaign) == "",
                  "a locked feature's line reads \"" + FeatureGate.LockLine(db, Feature.Fusion) + "\", and an open one has none");

            // ---- every entry point, against the table, at every point of the campaign
            string root = Path.GetFullPath(Path.Combine(contentDir, "..", "..", ".."));
            List<string> wrong = new List<string>();
            PlayerProfile p = GameSession.CreateNewProfile(db, OnboardingNow);
            for (int s = -1; s < db.AllStages.Count; s++)
            {
                if (s >= 0) p.ClearedStageIds.Add(db.AllStages[s].Id);
                for (int e = 0; e < FeatureEntryPoints.All.Length; e++)
                {
                    FeatureEntryPoint entry = FeatureEntryPoints.All[e];
                    StageDef unlock = FeatureGate.UnlockStage(db, entry.Feature);
                    bool expected = unlock == null || unlock.GlobalIndex <= s;
                    if (FeatureEntryPoints.Allows(db, p, entry.Id) != expected && wrong.Count < 6)
                        wrong.Add(entry.Id + " after " + (s < 0 ? "nothing" : db.AllStages[s].DisplayCode));
                }
            }
            Check(wrong.Count == 0, "all " + FeatureEntryPoints.All.Length + " entry points open exactly when their feature's stage is cleared, checked after every one of "
                  + db.AllStages.Count + " stages" + (wrong.Count > 0 ? " - wrong: " + string.Join("; ", wrong.ToArray()) : ""));

            List<string> perFeature = new List<string>();
            foreach (Feature f in Enum.GetValues(typeof(Feature)))
                perFeature.Add(f + " " + FeatureEntryPoints.Of(f).Count);
            Line("  entry points per feature: " + string.Join(", ", perFeature.ToArray()));
            bool everyGatedHasDoors = true;
            foreach (Feature f in Enum.GetValues(typeof(Feature)))
                if (FeatureEntryPoints.Of(f).Count == 0) everyGatedHasDoors = false;
            Check(everyGatedHasDoors, "every feature in the table has at least one entry point listed");

            // ---- the router's screens: each one belongs to a feature or is named open
            string routerPath = Path.Combine(root, "Assets", "Scripts", "UI", "ScreenRouter.cs");
            if (!File.Exists(routerPath)) { Check(false, "ScreenRouter.cs is where the harness expects it (" + routerPath + ")"); return; }
            string router = File.ReadAllText(routerPath);
            Match enumBody = Regex.Match(router, @"public enum ScreenId\s*\{([^}]*)\}");
            List<string> unplaced = new List<string>();
            int screens = 0;
            foreach (string raw in enumBody.Groups[1].Value.Split(','))
            {
                string name = raw.Trim();
                if (name.Length == 0) continue;
                screens++;
                Feature ignored;
                if (!FeatureEntryPoints.TryFeatureOfScreen(name, out ignored) && Array.IndexOf(FeatureEntryPoints.OpenScreens, name) < 0)
                    unplaced.Add(name);
            }
            Check(screens > 20 && unplaced.Count == 0,
                  "every one of the router's " + screens + " screens belongs to a feature or is named open"
                  + (unplaced.Count > 0 ? " - unplaced: " + string.Join(", ", unplaced.ToArray()) : ""));
            Check(router.Contains("FeatureGates.Allows(App, FeatureGates.ScreenEntry(id))"),
                  "ScreenRouter.Show refuses every screen of a locked feature, whoever asks: the door a deep link would use");

            // ---- every drawn door is bound to its id in the UI source
            string uiDir = Path.Combine(root, "Assets", "Scripts", "UI");
            StringBuilder ui = new StringBuilder();
            foreach (string file in Directory.GetFiles(uiDir, "*.cs", SearchOption.AllDirectories))
                ui.Append(File.ReadAllText(file)).Append('\n');
            string uiText = ui.ToString();
            List<string> unbound = new List<string>();
            int bound = 0;
            for (int e = 0; e < FeatureEntryPoints.All.Length; e++)
            {
                string id = FeatureEntryPoints.All[e].Id;
                if (id.StartsWith("screen:", StringComparison.Ordinal)) continue;
                bool isPanelTab = id.StartsWith("panel.profile.", StringComparison.Ordinal);
                if (isPanelTab ? uiText.Contains("\"panel.profile.\" + caption") : uiText.Contains("\"" + id + "\"")) bound++;
                else unbound.Add(id);
            }
            Check(unbound.Count == 0, bound + " drawn doors - hub buildings, nav cells, the rail, the card, the chest, the panel tabs, the hero page's buttons, the talent page's locked cards, the inventory's relic tab, the formation's guild tech - each pass their own id to the gate"
                  + (unbound.Count > 0 ? " - unbound: " + string.Join(", ", unbound.ToArray()) : ""));

            string panel = File.ReadAllText(Path.Combine(uiDir, "Hud", "PanelHost.cs"));
            Check(panel.Contains("FeatureGates.TryEnter(_app, \"panel.guild\")") && panel.Contains("FeatureGates.TryEnter(_app, TabEntry("),
                  "the panel host, which hosts screens without the router, asks the gate for the guild sheet and for each tab");

            // ---- one table: nothing else decides an unlock
            List<string> scattered = new List<string>();
            foreach (string file in Directory.GetFiles(Path.Combine(root, "Assets", "Scripts"), "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (name == "FeatureGate.cs" || name == "FeatureGates.cs") continue;
                if (File.ReadAllText(file).Contains("FeatureGate.IsUnlocked(")) scattered.Add(name);
            }
            Check(scattered.Count == 0,
                  "no game file outside the gate asks FeatureGate.IsUnlocked itself; every door goes through its entry id"
                  + (scattered.Count > 0 ? " - found in " + string.Join(", ", scattered.ToArray()) : ""));
        }

        // ------------------------------------------------------------------ the instruments

        private static void CheckWalksRespectUnlocks(ContentDatabase db, EconomyRun economy)
        {
            Line("");
            Line("THE INSTRUMENTS RESPECT THE UNLOCK TABLE (no fusion before 1-6, no armor before 1-12, no guild tech before 2-1, no relics or talents in any walk)");

            StageDef fusion = FeatureGate.UnlockStage(db, Feature.Fusion);
            StageDef armor = FeatureGate.UnlockStage(db, Feature.Armor);
            if (fusion == null || armor == null) { Check(false, "the table names a stage for Fusion and for Armor"); return; }

            string firstFuse = "", firstGear = "";
            bool fusedEarly = false, gearedEarly = false, gearedLater = false;
            for (int i = 0; i < economy.Stages.Count; i++)
            {
                EconomyStageOutcome o = economy.Stages[i];
                bool fused = o.Conversions > 0 || o.Ascensions > 0;
                bool geared = o.EntryPower > o.EntryBarePower;
                if (fused && firstFuse.Length == 0) firstFuse = o.Stage.DisplayCode;
                if (geared && firstGear.Length == 0) firstGear = o.Stage.DisplayCode;
                if (fused && o.Stage.GlobalIndex <= fusion.GlobalIndex) fusedEarly = true;
                if (geared && o.Stage.GlobalIndex <= armor.GlobalIndex) gearedEarly = true;
                if (geared && o.Stage.GlobalIndex > armor.GlobalIndex) gearedLater = true;
            }
            Check(!fusedEarly, "the fusing walk converts and ascends nothing in a sitting before " + fusion.DisplayCode
                  + " is cleared (first fusion at " + (firstFuse.Length > 0 ? firstFuse : "none") + ")");
            Check(!gearedEarly && gearedLater, "the walk arrives at no stage up to " + armor.DisplayCode
                  + " wearing anything, and wears its drops after (first geared arrival at " + (firstGear.Length > 0 ? firstGear : "none") + ")");

            StageDef beforeArmor = db.GetStage("ch1_7"), afterArmor = db.GetStage("ch2_1");
            if (beforeArmor != null && afterArmor != null)
            {
                int withheld = _armorWithheldReadings;
                GameSession early = BuildPinnedLine(db, beforeArmor, true);
                GameSession late = BuildPinnedLine(db, afterArmor, true);
                Check(early.Profile.OwnedArmor.Count == 0 && late.Profile.OwnedArmor.Count > 0 && _armorWithheldReadings > withheld,
                      "the geared CEIL line reads " + beforeArmor.DisplayCode + " bare and " + afterArmor.DisplayCode + " geared ("
                      + early.Profile.OwnedArmor.Count + " and " + late.Profile.OwnedArmor.Count + " pieces); "
                      + _armorWithheldReadings + " instrument readings this run were taken before armor opens");
            }
        }
    }
}
