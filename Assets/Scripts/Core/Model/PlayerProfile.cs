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
        /// A REFERENCE into PlayerProfile.OwnedRelics, or empty for an empty slot. Resolved
        /// against the ContentDatabase by the simulation during setup, not stored as a RelicDef -
        /// a save holds ids, exactly like DefId, so content can change under it without bricking it.
        ///
        /// Named for the reference rather than the thing, per DESIGN-RELIC-PROGRESSION section 3:
        /// the relic itself now exists in the profile's own collection, so a relic can be owned
        /// and unassigned, which the old hero-level RelicId could not express at all. Only one
        /// hero may point at a given owned relic at a time; GameSession.AssignRelic enforces it.
        /// </summary>
        public string EquippedRelicId = "";

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

    /// <summary>
    /// One relic the player owns, held by the profile rather than by a hero
    /// (DESIGN-RELIC-PROGRESSION section 3). Its independent existence is the whole point: an
    /// entry here with no hero's EquippedRelicId pointing at it is an owned, unassigned relic,
    /// a state the previous hero-level model could not represent.
    ///
    /// There is at most one entry per relic id. The design leaves a second copy of the same relic
    /// undecided and explicitly does not need one, since a tier is a property of the owned relic
    /// rather than a separate item.
    /// </summary>
    public sealed class OwnedRelic
    {
        public string RelicId = "";

        /// <summary>
        /// 1 or 2 per DESIGN-RELIC-PROGRESSION section 2, and 1 in practice until the balance
        /// pass authors tier 2's numbers. The field is here so that pass is a content and tuning
        /// change rather than another save migration. NOTHING in the simulation reads it: a relic
        /// at tier 2 today would behave exactly like the same relic at tier 1.
        /// </summary>
        public int Tier = 1;
    }

    /// <summary>Everything the player owns. Pure data - serialised by SaveSerializer.</summary>
    public sealed class PlayerProfile
    {
        public string PlayerName = "Wayfarer";
        public int PlayerLevel = 1;
        public int PlayerXp;
        public long Gold;
        public long Shards;

        /// <summary>
        /// Spent on relics in the (not yet built) relic shop, per DESIGN-RELIC-PROGRESSION
        /// section 1. Paid by repeat clears - see GameSession.RelicDustForRepeatClear for why
        /// that source is a placeholder.
        /// </summary>
        public long RelicDust;

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

        /// <summary>
        /// Every relic the player owns, assigned or not. HeroInstance.EquippedRelicId references
        /// entries here by relic id; an entry nothing references is simply unassigned.
        /// </summary>
        public readonly List<OwnedRelic> OwnedRelics = new List<OwnedRelic>();
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

        // ---------- relic helpers ----------

        public OwnedRelic FindOwnedRelic(string relicId)
        {
            if (string.IsNullOrEmpty(relicId)) return null;
            for (int i = 0; i < OwnedRelics.Count; i++) if (OwnedRelics[i].RelicId == relicId) return OwnedRelics[i];
            return null;
        }

        public bool OwnsRelic(string relicId)
        {
            return FindOwnedRelic(relicId) != null;
        }

        /// <summary>Tier of an owned relic, or 0 for one the player does not own.</summary>
        public int RelicTier(string relicId)
        {
            OwnedRelic owned = FindOwnedRelic(relicId);
            return owned != null ? owned.Tier : 0;
        }

        /// <summary>The hero currently holding a relic, or null when it is owned but unassigned.</summary>
        public HeroInstance HeroHoldingRelic(string relicId)
        {
            if (string.IsNullOrEmpty(relicId)) return null;
            for (int i = 0; i < Heroes.Count; i++) if (Heroes[i].EquippedRelicId == relicId) return Heroes[i];
            return null;
        }

        /// <summary>Owned relics no hero is holding. The shop and a future inventory screen both
        /// need this state to exist; it is the reason the collection was lifted off HeroInstance.</summary>
        public List<OwnedRelic> UnassignedRelics()
        {
            List<OwnedRelic> result = new List<OwnedRelic>();
            for (int i = 0; i < OwnedRelics.Count; i++)
            {
                if (HeroHoldingRelic(OwnedRelics[i].RelicId) == null) result.Add(OwnedRelics[i]);
            }
            return result;
        }

        /// <summary>
        /// Forces the two halves of the relic model back into agreement: a hero pointing at a
        /// relic nobody owns is emptied, and if several heroes somehow point at one relic only
        /// the first keeps it. Neither is reachable through GameSession, which is exactly why it
        /// runs on load - a hand-edited or partially-written save is the case it exists for.
        /// </summary>
        public void NormaliseRelics()
        {
            List<string> claimed = new List<string>();
            for (int i = 0; i < Heroes.Count; i++)
            {
                string relicId = Heroes[i].EquippedRelicId;
                if (string.IsNullOrEmpty(relicId)) continue;
                if (!OwnsRelic(relicId) || claimed.Contains(relicId)) { Heroes[i].EquippedRelicId = ""; continue; }
                claimed.Add(relicId);
            }
        }

        public string AllocateUid()
        {
            string uid = "h" + NextHeroUid;
            NextHeroUid++;
            return uid;
        }
    }
}
