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
        /// </summary>
        public const int CurrentVersion = 2;

        public static string Serialize(PlayerProfile profile)
        {
            JsonValue root = JsonValue.NewObject();
            root.Set("version", CurrentVersion);
            root.Set("playerName", profile.PlayerName);
            root.Set("playerLevel", profile.PlayerLevel);
            root.Set("playerXp", profile.PlayerXp);
            root.Set("gold", profile.Gold);
            root.Set("shards", profile.Shards);
            root.Set("stagesCleared", profile.StagesCleared);
            root.Set("lastIdleClaimUnix", profile.LastIdleClaimUnix);
            root.Set("lastSaveUnix", profile.LastSaveUnix);
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
                heroes.Add(hj);
            }
            root.Set("heroes", heroes);

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
            profile.StagesCleared = Math.Max(0, root.GetInt("stagesCleared", 0));
            profile.LastIdleClaimUnix = root.GetLong("lastIdleClaimUnix", 0);
            profile.LastSaveUnix = root.GetLong("lastSaveUnix", 0);
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
                profile.Heroes.Add(h);
            }

            List<JsonValue> team = root.GetList("team");
            for (int i = 0; i < team.Count && i < profile.Team.Length; i++)
            {
                profile.Team[i] = team[i].AsString(null);
            }

            profile.NormaliseTeam();
            return profile;
        }
    }
}
