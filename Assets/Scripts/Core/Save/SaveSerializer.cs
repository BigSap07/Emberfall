using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Json;
using Emberfall.Core.Model;

namespace Emberfall.Core.Save
{
    /// <summary>
    /// Turns a PlayerProfile into JSON and back. Unknown hero ids are dropped rather than
    /// throwing, so removing a hero from content never bricks an existing save.
    /// </summary>
    public static class SaveSerializer
    {
        /// <summary>
        /// 2: team array grew from 3 to 5 slots. The upgrade path (a 3-entry team read into 5
        /// slots) needs no migration - the read loop is length-tolerant and NormaliseTeam fills
        /// the rest. The bump exists for the downgrade path: a 5-entry team read by 3-slot code
        /// truncates silently, dropping two heroes with no error. Older builds never checked the
        /// version at all, so this build is where the guard starts existing; from here on, a save
        /// from a newer build is refused instead of quietly mangled.
        ///
        /// 3: HeroInstance.RelicId, written beside uid/defId. DESIGN-RELICS allows this to ride
        /// the team-size bump only if relics ship in the same pass; they did not, so 2 -> 3 as
        /// that document specifies. Upgrade needs no migration - a version 2 save has no relicId
        /// key and reads as empty, which is an empty relic slot, which is the default. The bump is
        /// again for the downgrade direction: a version 3 save read by version 2 code silently
        /// drops every equipped relic, and the guard below turns that into a refusal.
        ///
        /// 4: the repeat cap's dailyResetUnix and stageClearsToday map (DESIGN-REPLAY-ECONOMY).
        /// Upgrade needs no migration: a version 3 save has neither key, reads as stamp 0 with an
        /// empty map, and the first clear afterwards rolls the stamp to the current reward day -
        /// which is exactly a player who has spent none of today's paid repeats, the correct and
        /// generous default. The bump is once more for the downgrade direction: version 3 code
        /// drops both keys, so a capped-out player who rolled back would silently get their whole
        /// day of paid repeats handed back, and the guard below refuses instead.
        ///
        /// 5: the relic inventory (DESIGN-RELIC-PROGRESSION section 3) plus the relicDust purse.
        /// The profile grows an ownedRelics array of { relicId, tier }, and the per-hero relicId
        /// key becomes equippedRelicId - a REFERENCE into that array rather than the relic's only
        /// existence. The doc calls this bump 3 -> 4; 3 was the version when it was written, and
        /// the repeat cap has since taken 4, so it is 4 -> 5.
        ///
        /// Upgrade DOES need a migration this time, and it is the reason this is not a free bump:
        /// a version 4 save has no ownedRelics array at all, so a relic that exists only as a
        /// hero's relicId would simply cease to exist under the new model. The read below
        /// synthesises an ownedRelics entry at tier 1 for every hero-level relic it finds, from
        /// either key, so a player who had one equipped keeps it equipped and now also owns it.
        /// A player with none gets an empty collection, which is the correct default.
        ///
        /// Downgrade is guarded the same way 2, 3 and 4 are, by the version check below: version 4
        /// code reads no equippedRelicId and no ownedRelics, so it would silently unequip every
        /// relic AND drop the player's whole relic collection and dust purse. Refusing is the only
        /// safe answer, and refusing is what the existing guard already does once this constant moves.
        /// </summary>
        public const int CurrentVersion = 5;

        /// <summary>
        /// The pre-version-5 per-hero relic key. Read on upgrade and never written again; see the
        /// migration in Deserialize. Named here rather than inlined so the one place that still
        /// understands the old format is obvious.
        /// </summary>
        private const string LegacyHeroRelicKey = "relicId";

        public static string Serialize(PlayerProfile profile)
        {
            JsonValue root = JsonValue.NewObject();
            root.Set("version", CurrentVersion);
            root.Set("playerName", profile.PlayerName);
            root.Set("playerLevel", profile.PlayerLevel);
            root.Set("playerXp", profile.PlayerXp);
            root.Set("gold", profile.Gold);
            root.Set("shards", profile.Shards);
            root.Set("relicDust", profile.RelicDust);
            root.Set("stagesCleared", profile.StagesCleared);
            root.Set("lastIdleClaimUnix", profile.LastIdleClaimUnix);
            root.Set("lastSaveUnix", profile.LastSaveUnix);
            root.Set("dailyResetUnix", profile.DailyResetUnix);
            root.Set("nextHeroUid", profile.NextHeroUid);
            root.Set("totalSummons", profile.TotalSummons);
            root.Set("totalBattlesWon", profile.TotalBattlesWon);

            JsonValue cleared = JsonValue.NewArray();
            for (int i = 0; i < profile.ClearedStageIds.Count; i++) cleared.Add(profile.ClearedStageIds[i]);
            root.Set("clearedStages", cleared);

            JsonValue heroes = JsonValue.NewArray();
            for (int i = 0; i < profile.Heroes.Count; i++)
            {
                HeroInstance h = profile.Heroes[i];
                JsonValue hj = JsonValue.NewObject();
                hj.Set("uid", h.Uid);
                hj.Set("defId", h.DefId);
                hj.Set("level", h.Level);
                hj.Set("copies", h.Copies);
                hj.Set("acquired", h.AcquiredUnix);
                // Written only when set, so a profile with no relics equipped serialises byte for
                // byte the way it did before relics existed apart from the version number. The
                // legacy "relicId" key is never written again - only read, on upgrade.
                if (!string.IsNullOrEmpty(h.EquippedRelicId)) hj.Set("equippedRelicId", h.EquippedRelicId);
                heroes.Add(hj);
            }
            root.Set("heroes", heroes);

            // The relic collection is written even when empty, unlike the per-hero key above: an
            // empty array and an absent array mean different things here now. Absent is a save
            // from before version 5, which the read below migrates; empty is a version 5 player
            // who genuinely owns no relics and needs nothing synthesised.
            JsonValue ownedRelics = JsonValue.NewArray();
            for (int i = 0; i < profile.OwnedRelics.Count; i++)
            {
                OwnedRelic owned = profile.OwnedRelics[i];
                if (string.IsNullOrEmpty(owned.RelicId)) continue;
                JsonValue oj = JsonValue.NewObject();
                oj.Set("relicId", owned.RelicId);
                oj.Set("tier", owned.Tier);
                ownedRelics.Add(oj);
            }
            root.Set("ownedRelics", ownedRelics);

            // Zero counts are dropped rather than written: absent already means zero on the way
            // back in, so a player who has farmed nothing today serialises no map at all.
            JsonValue clearsToday = JsonValue.NewObject();
            foreach (KeyValuePair<string, int> pair in profile.StageClearsToday)
            {
                if (pair.Value > 0) clearsToday.Set(pair.Key, pair.Value);
            }
            root.Set("stageClearsToday", clearsToday);

            JsonValue team = JsonValue.NewArray();
            for (int i = 0; i < profile.Team.Length; i++) team.Add(new JsonValue(profile.Team[i]));
            root.Set("team", team);

            return root.ToJson(true);
        }

        public static PlayerProfile Deserialize(string json, ContentDatabase content, out string error)
        {
            error = null;
            JsonValue root;
            if (!JsonValue.TryParse(json, out root, out error)) return null;

            // Saves never carried a version below 1, and version was written from the start,
            // so a missing field just means "1". Anything above CurrentVersion comes from a
            // newer build; reading it here would silently drop whatever the newer format added
            // (a 5-slot team truncated to 3 was the first real casualty), so refuse instead.
            int version = root.GetInt("version", 1);
            if (version > CurrentVersion)
            {
                error = "save is from a newer build (version " + version + ", this build reads up to " + CurrentVersion + ")";
                return null;
            }

            PlayerProfile profile = new PlayerProfile();
            profile.PlayerName = root.GetString("playerName", "Wayfarer");
            profile.PlayerLevel = Math.Max(1, root.GetInt("playerLevel", 1));
            profile.PlayerXp = Math.Max(0, root.GetInt("playerXp", 0));
            profile.Gold = Math.Max(0, root.GetLong("gold", 0));
            profile.Shards = Math.Max(0, root.GetLong("shards", 0));
            profile.RelicDust = Math.Max(0, root.GetLong("relicDust", 0));
            profile.StagesCleared = Math.Max(0, root.GetInt("stagesCleared", 0));
            profile.LastIdleClaimUnix = root.GetLong("lastIdleClaimUnix", 0);
            profile.LastSaveUnix = root.GetLong("lastSaveUnix", 0);
            profile.DailyResetUnix = Math.Max(0, root.GetLong("dailyResetUnix", 0));
            profile.NextHeroUid = Math.Max(1, root.GetInt("nextHeroUid", 1));
            profile.TotalSummons = root.GetInt("totalSummons", 0);
            profile.TotalBattlesWon = root.GetInt("totalBattlesWon", 0);

            List<JsonValue> cleared = root.GetList("clearedStages");
            for (int i = 0; i < cleared.Count; i++)
            {
                string id = cleared[i].AsString(null);
                if (!string.IsNullOrEmpty(id) && !profile.ClearedStageIds.Contains(id)) profile.ClearedStageIds.Add(id);
            }

            List<JsonValue> heroes = root.GetList("heroes");
            for (int i = 0; i < heroes.Count; i++)
            {
                JsonValue hj = heroes[i];
                string defId = hj.GetString("defId", "");
                HeroDef def = content != null ? content.GetHero(defId) : null;
                if (def == null) continue; // content changed under the save - skip quietly

                HeroInstance h = new HeroInstance();
                h.Uid = hj.GetString("uid", profile.AllocateUid());
                h.DefId = defId;
                h.Def = def;
                h.Level = Math.Max(1, hj.GetInt("level", 1));
                h.Copies = Math.Max(1, hj.GetInt("copies", 1));
                h.AcquiredUnix = hj.GetLong("acquired", 0);
                // Missing reads as empty per DESIGN-RELICS. A relic id whose def no longer exists
                // is deliberately NOT dropped here: the simulation resolves ids to defs and treats
                // an unresolvable one as no relic, so keeping the string means re-adding the relic
                // to content restores the equip rather than silently having wiped it.
                // Version 5 writes equippedRelicId; anything older wrote relicId. Reading both
                // here is what makes the migration below a no-op for an already-current save.
                h.EquippedRelicId = hj.GetString("equippedRelicId", hj.GetString(LegacyHeroRelicKey, ""));
                profile.Heroes.Add(h);
            }

            List<JsonValue> ownedRelics = root.GetList("ownedRelics");
            for (int i = 0; i < ownedRelics.Count; i++)
            {
                string relicId = ownedRelics[i].GetString("relicId", "");
                if (string.IsNullOrEmpty(relicId) || profile.OwnsRelic(relicId)) continue;
                OwnedRelic owned = new OwnedRelic();
                owned.RelicId = relicId;
                owned.Tier = Math.Max(1, ownedRelics[i].GetInt("tier", 1));
                profile.OwnedRelics.Add(owned);
            }

            // THE VERSION 4 -> 5 MIGRATION. Before version 5 a relic existed only as a hero's
            // relicId, so every one of those has to become an owned relic or it vanishes with the
            // format. Synthesised at tier 1, which is the only tier that has ever existed.
            //
            // Deliberately not conditioned on the version number: it is written as "every equipped
            // relic must be owned", which is true of a version 5 save as well and simply finds
            // nothing to do there. A version check would make the invariant hold only on the one
            // read path that happened to be tested.
            for (int i = 0; i < profile.Heroes.Count; i++)
            {
                string relicId = profile.Heroes[i].EquippedRelicId;
                if (string.IsNullOrEmpty(relicId) || profile.OwnsRelic(relicId)) continue;
                OwnedRelic synthesised = new OwnedRelic();
                synthesised.RelicId = relicId;
                synthesised.Tier = 1;
                profile.OwnedRelics.Add(synthesised);
            }

            // Stage ids are not validated against content here for the same reason relic ids are
            // not: a stage that vanished and came back should not have had its counter wiped in
            // between, and a counter against an id nothing can launch is inert either way.
            JsonValue clearsToday = root["stageClearsToday"];
            foreach (string stageId in clearsToday.Keys)
            {
                int count = clearsToday[stageId].AsInt(0);
                if (count > 0) profile.StageClearsToday[stageId] = count;
            }

            List<JsonValue> team = root.GetList("team");
            for (int i = 0; i < team.Count && i < profile.Team.Length; i++)
            {
                profile.Team[i] = team[i].AsString(null);
            }

            profile.NormaliseTeam();
            profile.NormaliseRelics();
            return profile;
        }
    }
}
