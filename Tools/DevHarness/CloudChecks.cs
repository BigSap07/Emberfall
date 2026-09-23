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
    /// same rule the harness's turned-around tripwires follow. A flipped check says FIXED and the
    /// number, so it still names what it guards.
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
            CheckContentSkewKeepsHeroes(content);
            CheckUnknownArmorIsKeptVerbatim(content);
            CheckFreshStartDecision();
            CheckUnreadableDocumentIsNotUploadable(content);
            CheckConflictCardCannotTellApart(content);
            CheckSecretKeyRecognition();
            CheckSessionRenewal(root);
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
        /// CLOUD-GAPS P14, FIXED in version 15, and the documented refusal. A number above
        /// CurrentVersion is refused, which is the claim. The field used to be read with GetInt and
        /// a fallback of 1, so anything that was not a number, or a number too large for an int,
        /// read as an OLD save and was let through. SaveSerializer.TryReadVersion now refuses every
        /// one of those with a sentence, the way a newer build's save is refused.
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
                  "and a fractional version above this build");

            Check(!Refused(content, whole.Replace(current + ",", "")),
                  "a save with NO version field is read as version 1 - by design, versions were written from the start");
            Check(!Refused(content, whole.Replace(current, "\"version\": " + (SaveSerializer.CurrentVersion - 1))),
                  "and an older whole-number version is read, as every migration needs");

            // The P14 cases, each once let through as version 1 and each now refused. The guard is
            // only worth anything if the refusal says why in words, so each is also checked for a
            // sentence that names the damaged field.
            string[][] damaged =
            {
                new[] { "\"fifteen\"", "a version that is not a number" },
                new[] { "1e10", "a version too large for an int, which used to overflow negative" },
                new[] { "null", "a null version" },
                new[] { "\"" + SaveSerializer.CurrentVersion + "\"", "a current version written as a string, which no build writes" },
                new[] { (SaveSerializer.CurrentVersion - 1) + ".5", "a fraction below this build, which used to round to a real version" },
                new[] { "0", "version 0, which no build ever wrote" },
                new[] { "-3", "a negative version" },
                new[] { "true", "a boolean" },
                new[] { "[" + SaveSerializer.CurrentVersion + "]", "an array" },
            };
            for (int i = 0; i < damaged.Length; i++)
            {
                string error;
                PlayerProfile p = SaveSerializer.Deserialize(whole.Replace(current, "\"version\": " + damaged[i][0]), content, out error);
                Check(p == null && error != null && error.Contains("version number is damaged"),
                      "FIXED (P14) - " + damaged[i][1] + " is refused, not read as version 1"
                      + (error != null ? " (\"" + error + "\")" : " (it was read)"));
            }

            // The summary a conflict card shows reads by the same rule but never refuses: a card
            // must still describe a save it cannot open.
            SaveSummary summary = SaveSummaryReader.From(whole.Replace(current, "\"version\": \"fifteen\""));
            Check(summary.Readable && summary.SaveVersion == 0,
                  "and the conflict card's summary still describes such a save, reporting its version as 0 ("
                  + summary.SaveVersion + ")");
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
            //
            // 13 is the last version any build could have written with those ids: version 14 arrived
            // on 2026-09-21, ten days after both deletions, so this loop stops there however far
            // CurrentVersion moves.
            const int LastVersionWithDeletedIds = 13;
            string[] firstRoster = { "kalyx_emberwright", "maeve_corrin", "nyx_serrow" };
            string[] secondRoster = { "kael_the_ashbound", "marrow_the_burning_vow", "orren_the_cinder_hunter" };
            int noPlayable = 0, carried = 0;
            for (int v = 1; v <= LastVersionWithDeletedIds; v++)
            {
                string[] era = v <= 5 ? firstRoster : secondRoster;
                string error;
                PlayerProfile p = SaveSerializer.Deserialize(OldSave(v, era, relic, stage), content, out error);
                if (p == null) continue;
                if (p.Heroes.Count == 0) noPlayable++;
                // Since version 15 the records are kept. Re-saved, all three heroes are still in the
                // document, and so is the armor the first one wore.
                string again = SaveSerializer.Serialize(p);
                bool all = p.UnresolvedHeroRecords.Count == 3;
                for (int i = 0; i < era.Length; i++) all &= again.Contains("\"" + era[i] + "\"");
                if (v >= 8) all &= again.Contains("\"Old Blade\"");
                if (all) carried++;
            }
            for (int i = 0; i < 3; i++)
                Check(content.GetHero(firstRoster[i]) == null && content.GetHero(secondRoster[i]) == null,
                      "the old starter " + firstRoster[i] + " / " + secondRoster[i] + " is gone from heroes.json");
            Check(noPlayable == LastVersionWithDeletedIds,
                  "STILL PRESENT (P10), BY DECISION - a save holding the heroes its own build shipped has NO playable hero at "
                  + "every version from 1 to " + LastVersionWithDeletedIds + " (" + noPlayable + "), so Load still refuses it "
                  + "and keeps it aside (P1). No old-to-new id map: it would hand players heroes they never summoned");
            Check(carried == LastVersionWithDeletedIds,
                  "CONTAINED BY P7 - none of those heroes is dropped from the document any more: re-saved, every version "
                  + "carries all three records and the armor they wore (" + carried + " of " + LastVersionWithDeletedIds + ")");
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
        /// CLOUD-GAPS P7, FIXED in version 15. save_version guards the FORMAT, and nothing guarded
        /// the CONTENT: a device whose heroes.json lacked a hero dropped it on load, and its next
        /// upload deleted the hero from the cloud copy. The record is now kept verbatim, with the
        /// armor it wears, and loads as an ordinary hero the day content names it again - the rule
        /// an unresolvable relic id already followed.
        /// </summary>
        private static void CheckContentSkewKeepsHeroes(ContentDatabase content)
        {
            PlayerProfile profile = Rich(content);
            string doc = SaveSerializer.Serialize(profile);
            JsonValue root;
            string error;
            JsonValue.TryParse(doc, out root, out error);

            // A hero from a newer build, wearing a weapon, beside the player's own heroes.
            const string Unknown = "a_hero_from_a_newer_build";
            JsonValue stranger = JsonValue.NewObject();
            stranger.Set("uid", "h900");
            stranger.Set("defId", Unknown);
            stranger.Set("level", 60);
            stranger.Set("copies", 4);
            stranger.Set("acquired", 1760000000);
            stranger.Set("selectedTalent", 2);
            stranger.Set("aFieldANewerBuildWrites", "kept as it came");
            JsonValue worn = JsonValue.NewArray();
            worn.Add("g900");
            for (int s = 1; s < ArmorInfo.AllSlots.Length; s++) worn.Add("");
            stranger.Set("equippedArmor", worn);
            root["heroes"].Add(stranger);
            root["ownedArmor"].Add(Piece("g900", "Newer Blade", "Weapon", "Attack"));
            string newer = root.ToJson(true);
            string strangerJson = stranger.ToJson(false);

            PlayerProfile read = SaveSerializer.Deserialize(newer, content, out error);
            Check(read != null && read.Heroes.Count == profile.Heroes.Count,
                  "a save naming a hero this build lacks still loads, with only the heroes it can play");
            if (read == null) return;
            Check(read.UnresolvedHeroRecords.Count == 1 && read.UnresolvedHeroRecords[0] == strangerJson,
                  "FIXED (P7) - the unknown hero is kept aside byte for byte, including a key this build has never heard of");
            Check(read.FindArmor("g900") == null && read.UnresolvedArmorRecords.Count == 1,
                  "and the weapon it wears goes aside with it, so this build cannot salvage or combine it");

            string reSaved = SaveSerializer.Serialize(read);
            Check(reSaved.Contains(Unknown) && reSaved.Contains("Newer Blade"),
                  "FIXED (P7) - the hero and its weapon are both in the very next save, which is the one the next sync uploads");
            string error2;
            PlayerProfile twice = SaveSerializer.Deserialize(reSaved, content, out error2);
            Check(twice != null && SaveSerializer.Serialize(twice) == reSaved,
                  "and loading and saving again changes nothing, so two devices on two builds can agree about the document");

            // The day content names it, simulated by renaming the id, in the document the older
            // build wrote, to a real hero this profile does not own.
            HeroDef restoredDef = null;
            for (int i = 0; i < content.Heroes.Count && restoredDef == null; i++)
            {
                bool owned = false;
                for (int h = 0; h < profile.Heroes.Count; h++) owned |= profile.Heroes[h].DefId == content.Heroes[i].Id;
                if (!owned) restoredDef = content.Heroes[i];
            }
            if (restoredDef == null) { Check(false, "content has a hero the rich profile does not own"); return; }
            string error3;
            PlayerProfile restored = SaveSerializer.Deserialize(reSaved.Replace(Unknown, restoredDef.Id), content, out error3);
            HeroInstance back = restored != null ? restored.FindHeroByUid("h900") : null;
            Check(back != null && back.Level == 60 && back.Copies == 4 && back.SelectedTalent == 2,
                  "when content names the hero again it loads as an ordinary hero, level, copies and talent intact");
            Check(back != null && back.EquippedArmor[0] == "g900" && restored.FindArmor("g900") != null
                  && restored.UnresolvedHeroRecords.Count == 0 && restored.UnresolvedArmorRecords.Count == 0,
                  "wearing the weapon it wore, with nothing left aside");

            // A record with no defId is corrupt rather than unknown: no content could ever name it.
            string noId = newer.Replace("\"defId\": \"" + Unknown + "\"", "\"defId\": \"\"");
            PlayerProfile corrupt = SaveSerializer.Deserialize(noId, content, out error);
            Check(corrupt != null && corrupt.UnresolvedHeroRecords.Count == 0,
                  "a hero record with no defId at all is still skipped, since no content could ever name it");
        }

        /// <summary>
        /// CLOUD-GAPS P7's armor half, FIXED in version 15. ReadArmorPiece maps a slot, aspect,
        /// temper, stat or mode it does not know to Weapon, Ember, Rough or Attack, so a piece from
        /// a newer build came back changed and was saved that way. An owned piece this build cannot
        /// read faithfully is now kept aside verbatim.
        /// </summary>
        private static void CheckUnknownArmorIsKeptVerbatim(ContentDatabase content)
        {
            string doc = SaveSerializer.Serialize(Rich(content));
            string error;
            string[][] cases =
            {
                new[] { "Earring", "Attack", "a slot this build does not have" },
                new[] { "Weapon", "Haste", "a stat this build does not have" },
                new[] { "7", "Attack", "a slot written as a number, which Enum.Parse would accept though it is undefined" },
            };
            for (int c = 0; c < cases.Length; c++)
            {
                JsonValue root;
                JsonValue.TryParse(doc, out root, out error);
                JsonValue piece = Piece("g901", "Strange Piece", cases[c][0], cases[c][1]);
                root["ownedArmor"].Add(piece);
                PlayerProfile read = SaveSerializer.Deserialize(root.ToJson(true), content, out error);
                string again = read != null ? SaveSerializer.Serialize(read) : "";
                JsonValue againRoot;
                bool verbatim = false;
                if (JsonValue.TryParse(again, out againRoot, out error) && againRoot != null)
                {
                    List<JsonValue> owned = againRoot.GetList("ownedArmor");
                    for (int i = 0; i < owned.Count; i++)
                        if (owned[i].ToJson(false) == piece.ToJson(false)) verbatim = true;
                }
                Check(read != null && read.FindArmor("g901") == null && verbatim,
                      "FIXED (P7, armor) - a piece naming " + cases[c][2] + " is kept aside and written back exactly as it came");
            }

            PlayerProfile known = SaveSerializer.Deserialize(doc, content, out error);
            Check(known != null && known.UnresolvedArmorRecords.Count == 0 && known.UnresolvedHeroRecords.Count == 0,
                  "while a save whose every hero and piece this build knows keeps nothing aside");
        }

        /// <summary>An owned armor piece in the shape WriteArmorPiece writes, with the names given.</summary>
        private static JsonValue Piece(string uid, string name, string slot, string stat)
        {
            JsonValue piece = JsonValue.NewObject();
            piece.Set("uid", uid);
            piece.Set("name", name);
            piece.Set("slot", slot);
            piece.Set("aspect", "Ember");
            piece.Set("temper", "Rough");
            JsonValue primary = JsonValue.NewObject();
            primary.Set("stat", stat);
            primary.Set("mode", "Multiply");
            primary.Set("value", 0.05);
            piece.Set("primary", primary);
            piece.Set("subStats", JsonValue.NewArray());
            return piece;
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

        /// <summary>
        /// CLOUD-GAPS P13, FIXED. Nothing retried a 401, though a comment said callers did, so a
        /// device clock running slow failed every request until the next launch. The rule lives in
        /// SessionRenewal and is pinned here; that the one Send in the project actually follows it
        /// is proven end to end by Tools/CloudProbe, and held here as a tripwire on the source.
        /// </summary>
        private static void CheckSessionRenewal(string root)
        {
            Check(SessionRenewal.ShouldRenewAndRetry(401, "rest/v1/rpc/push_save", true, false),
                  "FIXED (P13) - a 401 on a request that carried a user's token earns one refresh and one resend");
            Check(!SessionRenewal.ShouldRenewAndRetry(401, "rest/v1/rpc/push_save", true, true),
                  "but never a second: the resend's own 401 goes to the caller, so nothing can loop");
            Check(!SessionRenewal.ShouldRenewAndRetry(401, "rest/v1/profiles", false, false),
                  "and not a request sent with the anon key, which has no session to renew");
            Check(!SessionRenewal.ShouldRenewAndRetry(401, "auth/v1/token?grant_type=refresh_token", true, false)
                  && !SessionRenewal.ShouldRenewAndRetry(401, "auth/v1/signup", true, false),
                  "and not the token or sign-up endpoints, where a refresh itself goes and would recurse");
            Check(!SessionRenewal.ShouldRenewAndRetry(403, "rest/v1/rpc/guild_touch", true, false)
                  && !SessionRenewal.ShouldRenewAndRetry(409, "rest/v1/rpc/push_save", true, false)
                  && !SessionRenewal.ShouldRenewAndRetry(0, "rest/v1/saves", true, false),
                  "and not a role refusal, a conflict or a transport failure, none of which a new token changes");
            Check(SessionRenewal.RenewFailed("x").Contains("could not be renewed")
                  && SessionRenewal.StillRefused("x").Contains("even after"),
                  "and both ways it can still fail are told to the player in words");

            string scripts = Path.Combine(root, Path.Combine("Assets", Path.Combine("Scripts", Path.Combine("Game", "Cloud"))));
            string client = StripComments(File.ReadAllText(Path.Combine(scripts, "SupabaseClient.cs")));
            string account = StripComments(File.ReadAllText(Path.Combine(scripts, "AccountService.cs")));
            Check(client.Contains("SessionRenewal.ShouldRenewAndRetry(") && client.Contains("RenewSession(sentToken"),
                  "SupabaseClient.Send asks SessionRenewal and renews through RenewSession");
            Check(Regex.IsMatch(account, "_client\\.RenewSession\\s*=\\s*RenewAfterUnauthorised"),
                  "and AccountService is what renews, so a refresh token is only ever spent in one place");
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
