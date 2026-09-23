using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Emberfall.Core.Cloud;
using Emberfall.Core.Data;
using Emberfall.Core.Json;
using Emberfall.Core.Model;
using Emberfall.Core.Save;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// The save and cloud audit's harness checks - Docs/CLOUD-GAPS.md, the engine-free half.
    ///
    /// A file of its own so the audit branch and whoever owns Program.cs never edit the same lines.
    /// Program.Main calls Run once and adds what it returns to its failure count:
    ///
    ///     _failures += CloudChecks.Run(content, contentDir);
    ///
    /// Everything here runs on Core alone. What needs UnityEngine - AccountService, SaveService,
    /// CloudSaveService - is proven by Tools/CloudProbe instead, against a fake engine.
    ///
    /// THE RULE FOR A DEFECT THAT WAS WRITTEN UP AND NOT FIXED: it is asserted as STILL PRESENT,
    /// with the finding's number in the label. The day somebody fixes it this goes red, and the fix
    /// is not done until the check is flipped and the doc's entry moves to the fixed section - the
    /// same rule the harness's turned-around tripwires follow.
    /// </summary>
    internal static class CloudChecks
    {
        private static int _failures;

        public static int Run(ContentDatabase content, string contentDir)
        {
            _failures = 0;
            string root = Path.GetFullPath(Path.Combine(contentDir, Path.Combine("..", Path.Combine("..", ".."))));

            Line("");
            Line("CLOUD AND SAVE AUDIT (Docs/CLOUD-GAPS.md)");
            CheckPartialFilesAreDetectable(content);
            CheckHandEditedSaves(content);
            CheckVersionField(content);
            CheckEveryOldVersionLoads(content);
            CheckContentSkewDropsHeroes(content);
            CheckFreshStartDecision();
            CheckUnreadableDocumentIsNotUploadable(content);
            CheckConflictCardCannotTellApart(content);
            CheckSecretKeyRecognition();
            CheckOfflineHasOneDoor(root);
            CheckNoSecretKeyInTheTree(root);
            CheckMigrationGrants(root);
            return _failures;
        }

        // ---------------------------------------------------------------- save integrity

        /// <summary>
        /// A save cut short anywhere is refused, never read as a smaller save. This is what makes a
        /// partial file DISTINGUISHABLE from a complete one - there is no checksum or length in the
        /// format, so the only witness is that no strict prefix of the document parses. It holds
        /// because the document is one JSON object and nothing closes it but its last character.
        /// </summary>
        private static void CheckPartialFilesAreDetectable(ContentDatabase content)
        {
            string whole = SaveSerializer.Serialize(Rich(content));
            int step = Math.Max(1, whole.Length / 600);
            int parsed = 0, tried = 0;
            string firstParsed = null;
            for (int cut = 1; cut < whole.Length; cut += step)
            {
                tried++;
                string error;
                if (SaveSerializer.Deserialize(whole.Substring(0, cut), content, out error) != null)
                {
                    parsed++;
                    if (firstParsed == null) firstParsed = cut.ToString();
                }
            }
            string lastCut;
            Check(SaveSerializer.Deserialize(whole.Substring(0, whole.Length - 1), content, out lastCut) == null,
                  "a save missing only its closing brace is refused");
            Check(parsed == 0, "no truncation of a save reads as a save (" + tried + " cut points, "
                  + parsed + " accepted" + (firstParsed != null ? ", first at " + firstParsed : "") + ")");

            string error2;
            Check(SaveSerializer.Deserialize("", content, out error2) == null, "an empty file is refused");
            Check(SaveSerializer.Deserialize(new string('\0', 64), content, out error2) == null,
                  "a file of NUL bytes - what a crash after the truncate and before the data leaves - is refused");
        }

        /// <summary>Hand edits. Values that cannot be right are clamped rather than refused, and a
        /// save whose hero list is not a list keeps nothing - which SaveService.Load treats as
        /// "no save" and now keeps a copy of rather than overwriting.</summary>
        private static void CheckHandEditedSaves(ContentDatabase content)
        {
            string whole = SaveSerializer.Serialize(Rich(content));
            string error;

            PlayerProfile negative = SaveSerializer.Deserialize(Regex.Replace(whole, "\"gold\": \\d+", "\"gold\": -500"), content, out error);
            Check(negative != null && negative.Gold == 0, "a negative purse is clamped to zero, not refused");

            PlayerProfile words = SaveSerializer.Deserialize(Regex.Replace(whole, "\"gold\": \\d+", "\"gold\": \"lots\""), content, out error);
            Check(words != null && words.Gold == 0,
                  "a purse that is not a number reads as zero - silently, the whole balance gone without a warning");

            PlayerProfile noList = SaveSerializer.Deserialize(Regex.Replace(whole, "\"heroes\": \\[", "\"heroes\": {\"x\": ["), content, out error);
            Check(noList == null || noList.Heroes.Count == 0,
                  "a hero list that is not a list keeps no heroes, which Load treats as no save at all");
        }

        /// <summary>
        /// CLOUD-GAPS P14 and the documented refusal. A number above CurrentVersion is refused, which
        /// is the claim. The field is read with GetInt and a fallback of 1, so anything that is not
        /// a number - or a number too large for an int - reads as an OLD save and is let through.
        /// </summary>
        private static void CheckVersionField(ContentDatabase content)
        {
            string whole = SaveSerializer.Serialize(Rich(content));
            string current = "\"version\": " + SaveSerializer.CurrentVersion;
            Check(whole.Contains(current), "the serialised save carries its version as a plain integer");

            Check(Refused(content, whole.Replace(current, "\"version\": " + (SaveSerializer.CurrentVersion + 1))),
                  "a save one version newer than this build is refused");
            Check(Refused(content, whole.Replace(current, "\"version\": \"" + (SaveSerializer.CurrentVersion + 1) + "\"")),
                  "and so is the same number written as a string");
            Check(Refused(content, whole.Replace(current, "\"version\": " + SaveSerializer.CurrentVersion + ".5")),
                  "and a fractional version that rounds past this build");

            Check(!Refused(content, whole.Replace(current + ",", "")),
                  "a save with NO version field is read as version 1 - by design, versions were written from the start");
            Check(!Refused(content, whole.Replace(current, "\"version\": \"fifteen\"")),
                  "STILL PRESENT (P14) - a version that is not a number reads as version 1 and is let through");
            Check(!Refused(content, whole.Replace(current, "\"version\": 1e10")),
                  "STILL PRESENT (P14) - a version too large for an int overflows negative and is let through");
            Check(!Refused(content, whole.Replace(current, "\"version\": null")),
                  "STILL PRESENT (P14) - a null version reads as version 1");
        }

        /// <summary>
        /// CLOUD-GAPS P10. A document in the exact shape each old build wrote - the key set is read out
        /// of SaveSerializer.Serialize at the commit that introduced each version - loaded by this
        /// build. With heroes that still exist, every step migrates. With the heroes those builds
        /// actually shipped, every step from 1 to 12 loses the whole save, because every one of
        /// those ids left heroes.json on 2026-09-11.
        /// </summary>
        private static void CheckEveryOldVersionLoads(ContentDatabase content)
        {
            List<HeroDef> starters = content.StartingHeroes;
            if (starters.Count < 3) { Check(false, "content has three starting heroes to build old saves from"); return; }
            string[] live = { starters[0].Id, starters[1].Id, starters[2].Id };
            string relic = content.Relics.Count > 0 ? content.Relics[0].Id : "";
            string stage = content.AllStages[0].Id;
            int echoesMax = content.Balance.RecitalEchoesMax;
            int brandsMax = content.Balance.ProvingBrandsMax;

            int migrated = 0;
            List<string> broken = new List<string>();
            for (int v = 1; v < SaveSerializer.CurrentVersion; v++)
            {
                string doc = OldSave(v, live, relic, stage);
                string error;
                PlayerProfile p = SaveSerializer.Deserialize(doc, content, out error);
                List<string> wrong = new List<string>();
                if (p == null) wrong.Add("refused: " + error);
                else
                {
                    if (p.Heroes.Count != 3) wrong.Add(p.Heroes.Count + " heroes");
                    if (p.Gold != 4321) wrong.Add("gold " + p.Gold);
                    if (!p.ClearedStageIds.Contains(stage)) wrong.Add("cleared stage lost");
                    if (p.Team[0] != "h1" || p.Team[1] != "h2" || p.Team[2] != "h3") wrong.Add("team moved");
                    if (v >= 3 && relic != "" && !p.OwnsRelic(relic)) wrong.Add("relic not owned after migration");
                    if (v >= 3 && relic != "" && p.Heroes[0].EquippedRelicId != relic) wrong.Add("relic unequipped");
                    if (v < 7 && p.RecitalEchoesBanked != echoesMax) wrong.Add("echoes " + p.RecitalEchoesBanked + " not full");
                    if (v >= 7 && p.RecitalEchoesBanked != 2) wrong.Add("echoes " + p.RecitalEchoesBanked + " not kept");
                    if (v < 9 && p.ProvingBrandsBanked != brandsMax) wrong.Add("brands " + p.ProvingBrandsBanked + " not full");
                    if (v >= 13 && p.Heroes[0].SelectedTalent != 2) wrong.Add("talent lost");
                    if (v >= 8 && p.OwnedArmor.Count != 1) wrong.Add("armor lost");

                    string again = SaveSerializer.Serialize(p);
                    string error2;
                    PlayerProfile back = SaveSerializer.Deserialize(again, content, out error2);
                    if (back == null || back.Gold != p.Gold || back.Heroes.Count != p.Heroes.Count)
                        wrong.Add("does not survive being re-saved at version " + SaveSerializer.CurrentVersion);
                }
                if (wrong.Count == 0) migrated++;
                else broken.Add("v" + v + ": " + string.Join(", ", wrong.ToArray()));
            }
            Check(broken.Count == 0, "every save version from 1 to " + (SaveSerializer.CurrentVersion - 1)
                  + " migrates to " + SaveSerializer.CurrentVersion + " when its heroes still exist ("
                  + migrated + " of " + (SaveSerializer.CurrentVersion - 1) + ")"
                  + (broken.Count > 0 ? " - " + string.Join("; ", broken.ToArray()) : ""));

            // The ids each era's builds actually wrote. Every one left heroes.json on 2026-09-11:
            // the six placeholders in "delete the six placeholder heroes", the rest in "The 94-hero
            // import". A starter from each era is enough - a save held at least its starters.
            string[] firstRoster = { "kalyx_emberwright", "maeve_corrin", "nyx_serrow" };
            string[] secondRoster = { "kael_the_ashbound", "marrow_the_burning_vow", "orren_the_cinder_hunter" };
            int lost = 0;
            for (int v = 1; v < SaveSerializer.CurrentVersion; v++)
            {
                string[] era = v <= 5 ? firstRoster : secondRoster;
                string error;
                PlayerProfile p = SaveSerializer.Deserialize(OldSave(v, era, relic, stage), content, out error);
                if (p != null && p.Heroes.Count == 0) lost++;
            }
            for (int i = 0; i < 3; i++)
                Check(content.GetHero(firstRoster[i]) == null && content.GetHero(secondRoster[i]) == null,
                      "the old starter " + firstRoster[i] + " / " + secondRoster[i] + " is gone from heroes.json");
            Check(lost == SaveSerializer.CurrentVersion - 1,
                  "STILL PRESENT (P10) - a save holding the heroes its own build shipped keeps NO hero at every version from 1 to "
                  + (SaveSerializer.CurrentVersion - 1) + " (" + lost + "), so Load reports no save and the boot starts a new game");
        }

        /// <summary>
        /// One save in the shape build version v wrote it. The key set per version is the one
        /// Serialize wrote at the commit that set CurrentVersion to v; see CLOUD-GAPS P10 for the
        /// table. Values are fixed so the checks above can tell a migrated value from a default.
        /// </summary>
        private static string OldSave(int v, string[] heroIds, string relic, string stage)
        {
            JsonValue root = JsonValue.NewObject();
            root.Set("version", v);
            root.Set("playerName", "Old Timer");
            if (v >= 10) root.Set("avatarHeroId", heroIds[0]);
            root.Set("playerLevel", 7);
            root.Set("playerXp", 40);
            root.Set("gold", 4321);
            root.Set("shards", 55);
            if (v >= 5) root.Set("relicDust", 12);
            if (v >= 8) root.Set("slag", 30);
            root.Set("stagesCleared", 1);
            root.Set("lastIdleClaimUnix", 1755000000);
            root.Set("lastSaveUnix", 1755000000);
            if (v >= 4) root.Set("dailyResetUnix", 1755000000);
            root.Set("nextHeroUid", 4);
            if (v >= 8) root.Set("nextArmorUid", 2);
            root.Set("totalSummons", 3);
            root.Set("totalBattlesWon", 5);
            if (v >= 11)
            {
                root.Set("campaignCurrentStageId", stage);
                root.Set("campaignCurrentWave", 2);
                root.Set("campaignCurrentBattle", 3);
            }
            if (v >= 7)
            {
                root.Set("recitalCurrentFloor", 3);
                root.Set("recitalHighestFloor", 2);
                root.Set("recitalEchoesBanked", 2);
                root.Set("recitalEchoesClockUnix", 1755000000);
            }
            if (v >= 12) root.Set("coda", 9);
            if (v >= 9)
            {
                root.Set("valor", 11);
                root.Set("provingBrandsBanked", 1);
                root.Set("provingBrandsClockUnix", 1755000000);
                root.Set("provingStanding", 4);
                root.Set("provingHighestStanding", 4);
                root.Set("provingAttempts", 6);
            }

            JsonValue cleared = JsonValue.NewArray();
            cleared.Add(stage);
            root.Set("clearedStages", cleared);

            JsonValue heroes = JsonValue.NewArray();
            for (int i = 0; i < heroIds.Length; i++)
            {
                JsonValue hj = JsonValue.NewObject();
                hj.Set("uid", "h" + (i + 1));
                hj.Set("defId", heroIds[i]);
                hj.Set("level", 5 + i);
                hj.Set("copies", 1);
                hj.Set("acquired", 1755000000);
                if (i == 0 && relic != "")
                {
                    if (v == 3 || v == 4) hj.Set("relicId", relic);
                    else if (v >= 5) hj.Set("equippedRelicId", relic);
                }
                if (i == 0 && v >= 8)
                {
                    JsonValue worn = JsonValue.NewArray();
                    worn.Add("a1");
                    for (int s = 1; s < 6; s++) worn.Add("");
                    hj.Set("equippedArmor", worn);
                }
                if (i == 0 && v >= 13) hj.Set("selectedTalent", 2);
                heroes.Add(hj);
            }
            root.Set("heroes", heroes);

            if (v >= 8)
            {
                JsonValue owned = JsonValue.NewArray();
                JsonValue piece = JsonValue.NewObject();
                piece.Set("uid", "a1");
                piece.Set("name", "Old Blade");
                piece.Set("slot", "Weapon");
                piece.Set("aspect", "Ember");
                piece.Set("temper", "Rough");
                JsonValue primary = JsonValue.NewObject();
                primary.Set("stat", "Attack");
                primary.Set("mode", "Multiply");
                primary.Set("value", 0.05);
                piece.Set("primary", primary);
                piece.Set("subStats", JsonValue.NewArray());
                owned.Add(piece);
                root.Set("ownedArmor", owned);
            }
            if (v >= 5)
            {
                JsonValue relics = JsonValue.NewArray();
                if (relic != "")
                {
                    JsonValue oj = JsonValue.NewObject();
                    oj.Set("relicId", relic);
                    oj.Set("tier", 1);
                    relics.Add(oj);
                }
                root.Set("ownedRelics", relics);
            }
            if (v >= 6) root.Set("eventProgress", JsonValue.NewArray());
            if (v >= 4) root.Set("stageClearsToday", JsonValue.NewObject());

            JsonValue team = JsonValue.NewArray();
            int slots = v == 1 ? 3 : 5;
            for (int i = 0; i < slots; i++) team.Add(new JsonValue(i < heroIds.Length ? "h" + (i + 1) : null));
            root.Set("team", team);
            return root.ToJson(true);
        }

        /// <summary>
        /// CLOUD-GAPS P7. save_version guards the FORMAT, and nothing guards the CONTENT. A device on
        /// an older build reads a save holding a hero its heroes.json does not have yet, drops the
        /// hero without a word, and its next upload deletes that hero from the cloud copy.
        /// </summary>
        private static void CheckContentSkewDropsHeroes(ContentDatabase content)
        {
            PlayerProfile profile = Rich(content);
            string doc = SaveSerializer.Serialize(profile);
            JsonValue root;
            string error;
            JsonValue.TryParse(doc, out root, out error);
            JsonValue stranger = JsonValue.NewObject();
            stranger.Set("uid", "h900");
            stranger.Set("defId", "a_hero_from_a_newer_build");
            stranger.Set("level", 60);
            stranger.Set("copies", 4);
            stranger.Set("acquired", 1760000000);
            root["heroes"].Add(stranger);
            string newer = root.ToJson(true);

            PlayerProfile read = SaveSerializer.Deserialize(newer, content, out error);
            string reSaved = read != null ? SaveSerializer.Serialize(read) : "";
            Check(read != null && read.Heroes.Count == profile.Heroes.Count,
                  "a save naming a hero this build lacks still loads, at the same save version");
            Check(read != null && !reSaved.Contains("a_hero_from_a_newer_build"),
                  "STILL PRESENT (P7) - and the hero is gone from the very next save, which is the one the next sync uploads");
        }

        // ---------------------------------------------------------------- the sync decision

        /// <summary>
        /// CLOUD-GAPS P1, the planner's half. A boot that replaced an unusable save with a starter
        /// profile still HAS a local save, and with the device's old base revision in place the
        /// planner uploads it over the cloud. GameApp.StartCloud now zeroes the base on a fresh
        /// start, and zero turns the same situation into a question.
        /// </summary>
        private static void CheckFreshStartDecision()
        {
            SyncInputs stale = new SyncInputs();
            stale.HasLocal = true;
            stale.HasCloud = true;
            stale.ReaderVersion = SaveSerializer.CurrentVersion;
            stale.CloudSaveVersion = SaveSerializer.CurrentVersion;
            stale.CloudRevision = 9;
            stale.BaseRevision = 9;
            Check(SyncPlanner.Decide(stale).Action == SyncAction.UploadLocal,
                  "a starter profile on a device whose base is still the cloud's revision WOULD be uploaded over it");

            SyncInputs forgotten = stale;
            forgotten.BaseRevision = 0;
            Check(SyncPlanner.Decide(forgotten).Action == SyncAction.AskPlayer,
                  "with the base forgotten, as a fresh start now does, the same sync asks the player");
        }

        /// <summary>CLOUD-GAPS P2, the codec's half: what is not a JSON object cannot become a push
        /// body. It used to become {} and be stored.</summary>
        private static void CheckUnreadableDocumentIsNotUploadable(ContentDatabase content)
        {
            string whole = SaveSerializer.Serialize(Rich(content));
            Check(CloudSaveCodec.WritePushBody(3, SaveSerializer.CurrentVersion, "d", whole.Substring(0, whole.Length / 2)) == null,
                  "a partial save produces no push body at all");
            Check(CloudSaveCodec.WritePushBody(3, SaveSerializer.CurrentVersion, "d", "") == null
                  && CloudSaveCodec.WritePushBody(3, SaveSerializer.CurrentVersion, "d", "[]") == null
                  && CloudSaveCodec.WritePushBody(3, SaveSerializer.CurrentVersion, "d", "42") == null,
                  "and neither does an empty document, an array or a bare number");
            Check(CloudSaveCodec.WritePushBody(3, SaveSerializer.CurrentVersion, "d", whole) != null,
                  "while a whole save still does");
        }

        /// <summary>
        /// CLOUD-GAPS P9. The conflict card shows name, rank, stages cleared and heroes owned, plus
        /// when each save was written. Two saves a week of play apart - a fortune in gold, every
        /// hero ten levels higher, a Recital run - can agree on all four, and then the only thing
        /// separating them on the card is the timestamp.
        /// </summary>
        private static void CheckConflictCardCannotTellApart(ContentDatabase content)
        {
            PlayerProfile before = Rich(content);
            string a = SaveSerializer.Serialize(before);
            PlayerProfile after = Rich(content);
            after.Gold += 2000000;
            after.Shards += 5000;
            after.RecitalHighestFloor = 40;
            for (int i = 0; i < after.Heroes.Count; i++) after.Heroes[i].Level += 10;
            after.LastSaveUnix = before.LastSaveUnix + 7 * 86400;
            string b = SaveSerializer.Serialize(after);

            SaveSummary sa = SaveSummaryReader.From(a);
            SaveSummary sb = SaveSummaryReader.From(b);
            Check(sa.Readable && sb.Readable && sa.Describe() == sb.Describe() && sa.LastSaveUnix != sb.LastSaveUnix,
                  "STILL PRESENT (P9) - a week apart and two million gold apart, the card's four rows are identical: \""
                  + sa.Describe() + "\"");
        }

        // ---------------------------------------------------------------- keys

        private static void CheckSecretKeyRecognition()
        {
            string header = B64("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
            string service = header + "." + B64("{\"iss\":\"supabase\",\"ref\":\"x\",\"role\":\"service_role\"}") + ".sig";
            string anon = header + "." + B64("{\"iss\":\"supabase\",\"ref\":\"x\",\"role\":\"anon\"}") + ".sig";
            Check(!service.Contains("service_role"),
                  "the text 'service_role' is NOT in a service-role JWT, so the Python guard that looked for it could never fire");
            Check(SupabaseKeys.IsSecret(service), "a service-role JWT is recognised as secret");
            Check(SupabaseKeys.IsSecret("sb_secret_abc123"), "and so is a new-format secret key");
            Check(!SupabaseKeys.IsSecret(anon), "an anon JWT is not");
            Check(!SupabaseKeys.IsSecret("sb_publishable_abc123"), "and neither is a new-format publishable key");
            Check(SupabaseKeys.IsSecret(header + ".!!!.sig"), "a JWT whose payload cannot be read is treated as secret");
        }

        private static string B64(string text)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        // ---------------------------------------------------------------- offline and the tree

        /// <summary>
        /// Offline is the absence of the online code, not a path through it. Proven by execution in
        /// Tools/CloudProbe; held here as a tripwire on the source, because what keeps it true is
        /// that there is exactly one door: one file builds requests, one line builds that file's
        /// client, and that line sits behind the Configured check.
        /// </summary>
        private static void CheckOfflineHasOneDoor(string root)
        {
            string scripts = Path.Combine(root, Path.Combine("Assets", "Scripts"));
            List<string> requestBuilders = new List<string>();
            List<string> clientBuilders = new List<string>();
            foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                string text = StripComments(File.ReadAllText(file));
                string rel = file.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
                if (Regex.IsMatch(text, "\\bUnityWebRequest\\b|\\bHttpClient\\b|\\bWebClient\\b|\\bTcpClient\\b|\\bnew\\s+WWW\\b"))
                    requestBuilders.Add(rel);
                if (Regex.IsMatch(text, "new\\s+SupabaseClient\\s*\\(")) clientBuilders.Add(rel);
            }
            Check(requestBuilders.Count == 1 && requestBuilders[0].EndsWith("Game/Cloud/SupabaseClient.cs"),
                  "exactly one file in Assets/Scripts can build a network request (" + string.Join(", ", requestBuilders.ToArray()) + ")");
            Check(clientBuilders.Count == 1 && clientBuilders[0].EndsWith("Game/GameApp.cs"),
                  "and exactly one file constructs its client (" + string.Join(", ", clientBuilders.ToArray()) + ")");

            string app = StripComments(File.ReadAllText(Path.Combine(scripts, Path.Combine("Game", "GameApp.cs"))));
            int guard = app.IndexOf("if (!config.Configured)", StringComparison.Ordinal);
            int build = app.IndexOf("new SupabaseClient(", StringComparison.Ordinal);
            int ret = guard >= 0 ? app.IndexOf("return;", guard, StringComparison.Ordinal) : -1;
            Check(guard >= 0 && ret > guard && build > ret,
                  "and that construction comes after the unconfigured case has returned");
        }

        /// <summary>
        /// The service role key never enters the repository, the game or ProjectSettings. Every JWT
        /// and every new-format secret key in the working tree is decoded and its role read. The
        /// check prints the FILE, never the key. It scans the gitignored supabase.json too, when
        /// one is present, because that is exactly where a wrong key would be pasted.
        /// </summary>
        private static void CheckNoSecretKeyInTheTree(string root)
        {
            string[] roots = { "Assets", "ProjectSettings", "Server", "Tools", "Packages", "Docs" };
            HashSet<string> extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".cs", ".json", ".asset", ".txt", ".md", ".sql", ".py", ".ps1", ".sh", ".yaml", ".yml",
                ".xml", ".cfg", ".ini", ".env", ".unity", ".prefab"
            };
            // The detector, proven to fire before its silence is trusted: a service-role JWT built
            // here, sitting in a line of text the way it would sit in a config file.
            string header = B64("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
            string planted = "{\"anonKey\": \"" + header + "."
                           + B64("{\"iss\":\"supabase\",\"ref\":\"abcdefgh\",\"role\":\"service_role\"}") + ".c2ln\"}";
            string anonLine = "{\"anonKey\": \"" + header + "."
                            + B64("{\"iss\":\"supabase\",\"ref\":\"abcdefgh\",\"role\":\"anon\"}") + ".c2ln\"}";
            int ignored;
            Check(TextHasSecret(planted, out ignored), "the tree scan fires on a planted service-role JWT");
            Check(!TextHasSecret(anonLine, out ignored), "and stays quiet on an anon one");

            List<string> offenders = new List<string>();
            int scanned = 0, tokens = 0;

            List<string> files = new List<string>();
            foreach (string top in roots)
            {
                string dir = Path.Combine(root, top);
                if (Directory.Exists(dir)) files.AddRange(Directory.GetFiles(dir, "*", SearchOption.AllDirectories));
            }
            files.AddRange(Directory.GetFiles(root, "*.md"));

            foreach (string file in files)
            {
                string norm = file.Replace('\\', '/');
                if (norm.Contains("/bin/") || norm.Contains("/obj/") || norm.Contains("/__pycache__/")) continue;
                if (!extensions.Contains(Path.GetExtension(file))) continue;
                FileInfo info = new FileInfo(file);
                if (info.Length > 8 * 1024 * 1024) continue;
                string text;
                try { text = File.ReadAllText(file); } catch (IOException) { continue; }
                scanned++;
                int found;
                if (TextHasSecret(text, out found)) offenders.Add(file.Substring(root.Length).TrimStart('\\', '/'));
                tokens += found;
            }
            Check(offenders.Count == 0, "no service-role or secret key anywhere in the tree (" + scanned + " files, "
                  + tokens + " JWT" + (tokens == 1 ? "" : "s") + " decoded)"
                  + (offenders.Count > 0 ? " - FOUND IN: " + string.Join(", ", offenders.ToArray()) : ""));
        }

        private static readonly Regex Jwt = new Regex("eyJ[A-Za-z0-9_-]{8,}\\.eyJ[A-Za-z0-9_-]{8,}\\.[A-Za-z0-9_-]*");
        private static readonly Regex SecretKey = new Regex("sb_secret_[A-Za-z0-9_-]{8,}");

        /// <summary>Whether a text holds a key that must never ship. Counts the JWTs it decoded;
        /// never returns or prints the key itself.</summary>
        private static bool TextHasSecret(string text, out int jwts)
        {
            jwts = 0;
            bool bad = SecretKey.IsMatch(text);
            foreach (Match m in Jwt.Matches(text))
            {
                jwts++;
                if (SupabaseKeys.IsSecret(m.Value)) bad = true;
            }
            return bad;
        }

        /// <summary>
        /// The migrations, read as text: which roles may do what. Asserted against an exact
        /// allowlist, so a new client write grant - in 0008 or anywhere after - turns this red until
        /// somebody has decided it belongs here and added it on purpose.
        /// </summary>
        private static void CheckMigrationGrants(string root)
        {
            string dir = Path.Combine(root, Path.Combine("Server", Path.Combine("supabase", "migrations")));
            if (!Directory.Exists(dir)) { Check(false, "the migrations folder exists"); return; }
            string[] files = Directory.GetFiles(dir, "*.sql");
            Array.Sort(files, StringComparer.Ordinal);

            StringBuilder all = new StringBuilder();
            foreach (string f in files) all.Append(StripSqlComments(File.ReadAllText(f))).Append('\n');
            string sql = all.ToString().ToLowerInvariant();

            HashSet<string> tables = Names(sql, "create table if not exists public\\.(\\w+)");
            HashSet<string> revokedTables = Names(sql, "revoke all on public\\.(\\w+)\\s+from anon, authenticated");
            List<string> unrevoked = new List<string>();
            foreach (string t in tables) if (!revokedTables.Contains(t)) unrevoked.Add(t);
            Check(unrevoked.Count == 0, "every table revokes anon and authenticated before granting anything ("
                  + tables.Count + " tables" + (unrevoked.Count > 0 ? ", not: " + string.Join(", ", unrevoked.ToArray()) : "") + ")");

            List<string> anonGrants = new List<string>();
            foreach (Match m in Regex.Matches(sql, "grant [^;]*? to [^;]*\\banon\\b[^;]*;")) anonGrants.Add(m.Value);
            Check(anonGrants.Count == 0, "no statement grants anything to anon" + (anonGrants.Count > 0 ? " - " + anonGrants[0] : ""));

            HashSet<string> writes = new HashSet<string>();
            foreach (Match m in Regex.Matches(sql, "grant ([^;]+?) on public\\.(\\w+) to (\\w+);"))
            {
                if (m.Groups[1].Value.Contains("function")) continue;
                foreach (Match p in Regex.Matches(m.Groups[1].Value, "(insert|update|delete)(\\s*\\(([^)]*)\\))?"))
                {
                    string cols = p.Groups[3].Success ? "(" + Regex.Replace(p.Groups[3].Value, "\\s+", "") + ")" : "";
                    writes.Add(m.Groups[2].Value + ":" + p.Groups[1].Value + cols + ":" + m.Groups[3].Value);
                }
            }
            string[] allowed =
            {
                "profiles:update(display_name):authenticated",
                "friendships:insert:authenticated", "friendships:delete:authenticated",
                "friendships:update(status):authenticated",
                "team_snapshots:insert:authenticated", "team_snapshots:update:authenticated",
                "team_snapshots:delete:authenticated",
                "guilds:update(description,is_open):authenticated", "guilds:delete:authenticated",
                "guild_members:delete:authenticated", "guild_requests:delete:authenticated"
            };
            HashSet<string> expected = new HashSet<string>(allowed);
            List<string> extra = new List<string>(), missing = new List<string>();
            foreach (string w in writes) if (!expected.Contains(w)) extra.Add(w);
            foreach (string w in expected) if (!writes.Contains(w)) missing.Add(w);
            Check(extra.Count == 0 && missing.Count == 0,
                  "the client write grants are exactly the eleven the design names (" + writes.Count + ")"
                  + (extra.Count > 0 ? " - UNEXPECTED: " + string.Join(", ", extra.ToArray()) : "")
                  + (missing.Count > 0 ? " - missing: " + string.Join(", ", missing.ToArray()) : ""));
            Check(!writes.Contains("saves:insert:authenticated") && !writes.Contains("saves:update:authenticated")
                  && !writes.Contains("guild_dungeon_attempts:insert:authenticated")
                  && !writes.Contains("guild_messages:insert:authenticated"),
                  "saves, chat and dungeon attempts carry no client write at all - their RPCs are the only door");

            // Functions. Postgres grants EXECUTE to PUBLIC on every new function, and Supabase's
            // default privileges grant it to anon as well, so a function nobody revoked is callable
            // with the anon key alone. Trigger functions cannot be called directly and are exempt.
            List<string> exposed = new List<string>();
            HashSet<string> revokedFns = Names(sql, "revoke all on function public\\.(\\w+)\\s*\\([^)]*\\)\\s+from public, anon");
            foreach (Match m in Regex.Matches(sql, "create or replace function public\\.(\\w+)\\s*\\(([^;]*?)\\bas \\$\\$", RegexOptions.Singleline))
            {
                string name = m.Groups[1].Value;
                if (Regex.IsMatch(m.Groups[2].Value, "returns\\s+trigger")) continue;
                if (!revokedFns.Contains(name) && !exposed.Contains(name)) exposed.Add(name);
            }
            exposed.Sort(StringComparer.Ordinal);
            Check(exposed.Count == 1 && exposed[0] == "generate_friend_code",
                  "STILL PRESENT (S6) - exactly one callable function is never revoked from anon and PUBLIC ("
                  + string.Join(", ", exposed.ToArray()) + ")");
        }

        // ---------------------------------------------------------------- plumbing

        private static HashSet<string> Names(string text, string pattern)
        {
            HashSet<string> found = new HashSet<string>();
            foreach (Match m in Regex.Matches(text, pattern)) found.Add(m.Groups[1].Value);
            return found;
        }

        private static string StripSqlComments(string sql)
        {
            return Regex.Replace(sql, "--[^\\n]*", "");
        }

        private static string StripComments(string code)
        {
            string noBlock = Regex.Replace(code, "/\\*.*?\\*/", "", RegexOptions.Singleline);
            return Regex.Replace(noBlock, "//[^\\n]*", "");
        }

        private static bool Refused(ContentDatabase content, string doc)
        {
            string error;
            return SaveSerializer.Deserialize(doc, content, out error) == null;
        }

        /// <summary>A profile with something in every part of the format that matters here.</summary>
        private static PlayerProfile Rich(ContentDatabase content)
        {
            PlayerProfile profile = GameSession.CreateNewProfile(content, 1700000000);
            GameSession session = new GameSession(content, profile);
            session.LevelUpHero(profile.Heroes[0], 6);
            session.GrantBattleResult(content.AllStages[0], true, 1700000100);
            profile.Gold = 123456;
            return profile;
        }

        private static void Line(string text) { Console.WriteLine(text); }

        private static void Check(bool condition, string label)
        {
            if (condition) { Line("  [ok]   " + label); return; }
            _failures++;
            Line("  [FAIL] " + label);
        }
    }
}
