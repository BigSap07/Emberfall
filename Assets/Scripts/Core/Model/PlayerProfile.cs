using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;

namespace Emberfall.Core.Model
{
    /// <summary>One owned hero: a definition id plus everything the player has changed about it.</summary>
    public sealed class HeroInstance
    {
        public string Uid = "";        // stable per-collection id
        public string DefId = "";
        public int Level = 1;
        public int Copies = 1;         // duplicates pulled from summoning
        public long AcquiredUnix;

        /// <summary>
        /// Equipped relic id, or empty for an empty slot. Resolved against the ContentDatabase by
        /// the simulation during setup, not stored as a RelicDef - a save holds ids, exactly like
        /// DefId, so content can change under it without bricking it.
        /// </summary>
        public string RelicId = "";

        [NonSerialized] public HeroDef Def;

        public UnitStats Stats
        {
            get { return Def != null ? StatMath.ForHero(Def, Level) : new UnitStats(); }
        }

        public int Power
        {
            get { return Stats.Power; }
        }

        public string DisplayName
        {
            get { return Def != null ? Def.Name : DefId; }
        }
    }

    /// <summary>Everything the player owns. Pure data - serialised by SaveSerializer.</summary>
    public sealed class PlayerProfile
    {
        public string PlayerName = "Wayfarer";
        public int PlayerLevel = 1;
        public int PlayerXp;
        public long Gold;
        public long Shards;

        /// <summary>Number of distinct campaign stages cleared; also the index of the next locked stage.</summary>
        public int StagesCleared;

        /// <summary>
        /// Banner slots every player has, from the first minute. Deploying fewer heroes than this
        /// is fine - empty slots are simply empty. Must stay equal to BalanceConfig.MaxTeamSize,
        /// which gates the same number on the simulation side; they are two names for one decision.
        /// </summary>
        public const int TeamSize = 5;

        public readonly List<string> ClearedStageIds = new List<string>();
        public readonly List<HeroInstance> Heroes = new List<HeroInstance>();
        public readonly string[] Team = new string[TeamSize]; // hero uids, null = empty slot

        public long LastIdleClaimUnix;
        public long LastSaveUnix;

        /// <summary>
        /// Start of the reward day the StageClearsToday counters below belong to: the most recent
        /// BalanceConfig.DailyResetHourUtc boundary at or before the clear that last touched them.
        /// GameSession only ever moves this forward - with no server the device clock is the only
        /// source of truth, so winding it backwards must not hand out a fresh day of paid clears.
        /// </summary>
        public long DailyResetUnix;

        /// <summary>
        /// Repeat clears banked against each stage id since DailyResetUnix - stage id to count,
        /// absent meaning zero. A player's FIRST clear of a stage is progress rather than farming
        /// and is never counted here. Emptied wholesale when the reward day rolls over.
        /// </summary>
        public readonly Dictionary<string, int> StageClearsToday = new Dictionary<string, int>();

        public int NextHeroUid = 1;
        public int TotalSummons;
        public int TotalBattlesWon;

        // ---------- collection helpers ----------

        public HeroInstance FindHeroByUid(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return null;
            for (int i = 0; i < Heroes.Count; i++) if (Heroes[i].Uid == uid) return Heroes[i];
            return null;
        }

        public HeroInstance FindHeroByDefId(string defId)
        {
            if (string.IsNullOrEmpty(defId)) return null;
            for (int i = 0; i < Heroes.Count; i++) if (Heroes[i].DefId == defId) return Heroes[i];
            return null;
        }

        public bool Owns(string defId)
        {
            return FindHeroByDefId(defId) != null;
        }

        public bool IsDeployed(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return false;
            for (int i = 0; i < Team.Length; i++) if (Team[i] == uid) return true;
            return false;
        }

        public int TeamSlotOf(string uid)
        {
            for (int i = 0; i < Team.Length; i++) if (Team[i] == uid) return i;
            return -1;
        }

        /// <summary>Puts a hero in a slot, swapping if it was already deployed elsewhere.</summary>
        public void AssignToTeam(int slot, string uid)
        {
            if (slot < 0 || slot >= Team.Length) return;
            int existing = TeamSlotOf(uid);
            string displaced = Team[slot];
            Team[slot] = uid;
            if (existing >= 0 && existing != slot) Team[existing] = displaced;
        }

        public void ClearTeamSlot(int slot)
        {
            if (slot < 0 || slot >= Team.Length) return;
            Team[slot] = null;
        }

        /// <summary>Drops any team entry whose hero no longer exists, then fills gaps with the strongest bench heroes.</summary>
        public void NormaliseTeam()
        {
            for (int i = 0; i < Team.Length; i++)
            {
                if (!string.IsNullOrEmpty(Team[i]) && FindHeroByUid(Team[i]) == null) Team[i] = null;
            }
            for (int i = 0; i < Team.Length; i++)
            {
                if (!string.IsNullOrEmpty(Team[i])) continue;
                HeroInstance best = null;
                for (int h = 0; h < Heroes.Count; h++)
                {
                    HeroInstance candidate = Heroes[h];
                    if (IsDeployed(candidate.Uid)) continue;
                    if (best == null || candidate.Power > best.Power) best = candidate;
                }
                if (best != null) Team[i] = best.Uid;
            }
        }

        public List<HeroInstance> TeamHeroes()
        {
            List<HeroInstance> result = new List<HeroInstance>();
            for (int i = 0; i < Team.Length; i++)
            {
                HeroInstance h = FindHeroByUid(Team[i]);
                if (h != null) result.Add(h);
            }
            return result;
        }

        public int TeamPower()
        {
            int total = 0;
            List<HeroInstance> team = TeamHeroes();
            for (int i = 0; i < team.Count; i++) total += team[i].Power;
            return total;
        }

        public bool HasClearedStage(string stageId)
        {
            return ClearedStageIds.Contains(stageId);
        }

        /// <summary>Repeat clears already banked against a stage today. Never throws on an
        /// unknown id - a stage nobody has farmed today simply reads zero.</summary>
        public int ClearsTodayFor(string stageId)
        {
            int count;
            if (string.IsNullOrEmpty(stageId)) return 0;
            return StageClearsToday.TryGetValue(stageId, out count) ? count : 0;
        }

        public string AllocateUid()
        {
            string uid = "h" + NextHeroUid;
            NextHeroUid++;
            return uid;
        }
    }
}
