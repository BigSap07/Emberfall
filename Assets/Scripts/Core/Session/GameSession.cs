using System;
using System.Collections.Generic;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;

namespace Emberfall.Core.Session
{
    public static class TimeUtil
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long NowUnix()
        {
            return (long)(DateTime.UtcNow - Epoch).TotalSeconds;
        }
    }

    public sealed class LevelUpResult
    {
        public bool Success;
        public string Message = "";
        public int LevelsGained;
        public int NewLevel;
        public long GoldSpent;
    }

    public sealed class IdleReport
    {
        public long Seconds;
        public int Gold;
        public int Shards;
        public bool Capped;
        public float CapSeconds;

        public bool HasAnything
        {
            get { return Gold > 0 || Shards > 0; }
        }
    }

    public sealed class SummonOutcome
    {
        public bool Success;
        public string Message = "";
        public HeroDef Hero;
        public HeroInstance Instance;
        public bool IsNew;
        public int GoldRefund;
        public int ShardRefund;
        public long CostPaid;
    }

    public sealed class BattleRewards
    {
        public bool Victory;
        public int Gold;
        public int Shards;
        public int AccountXp;
        public bool FirstClear;
        public int PlayerLevelsGained;
        public bool UnlockedNextStage;
        public string StageId = "";
    }

    /// <summary>
    /// The player's game, as a set of operations. Every rule that changes the profile
    /// lives here so the UI layer only has to render and call.
    /// </summary>
    public sealed class GameSession
    {
        public ContentDatabase Content { get; private set; }
        public PlayerProfile Profile { get; private set; }

        public GameSession(ContentDatabase content, PlayerProfile profile)
        {
            Content = content;
            Profile = profile;
            BindDefinitions();
            Profile.NormaliseTeam();
        }

        public BalanceConfig Balance
        {
            get { return Content.Balance; }
        }

        /// <summary>Re-links hero instances to their definitions after a load.</summary>
        public void BindDefinitions()
        {
            for (int i = Profile.Heroes.Count - 1; i >= 0; i--)
            {
                HeroInstance h = Profile.Heroes[i];
                h.Def = Content.GetHero(h.DefId);
                if (h.Def == null) Profile.Heroes.RemoveAt(i);
            }
        }

        public static PlayerProfile CreateNewProfile(ContentDatabase content, long nowUnix)
        {
            PlayerProfile profile = new PlayerProfile();
            profile.Gold = content.Balance.StartingGold;
            profile.Shards = content.Balance.StartingShards;
            profile.LastIdleClaimUnix = nowUnix;
            profile.LastSaveUnix = nowUnix;

            List<HeroDef> starters = content.StartingHeroes;
            for (int i = 0; i < starters.Count; i++) AddHero(profile, starters[i], nowUnix);

            profile.NormaliseTeam();
            return profile;
        }

        private static HeroInstance AddHero(PlayerProfile profile, HeroDef def, long nowUnix)
        {
            HeroInstance h = new HeroInstance();
            h.Uid = profile.AllocateUid();
            h.DefId = def.Id;
            h.Def = def;
            h.Level = 1;
            h.Copies = 1;
            h.AcquiredUnix = nowUnix;
            profile.Heroes.Add(h);
            return h;
        }

        // ------------------------------------------------------------------ hero levelling

        public int HeroLevelCap
        {
            get { return StatMath.HeroLevelCap(Balance, Profile.PlayerLevel); }
        }

        public int LevelUpCost(HeroInstance hero)
        {
            return StatMath.LevelUpCost(Balance, hero.Level);
        }

        public bool CanLevelUp(HeroInstance hero)
        {
            if (hero == null) return false;
            if (hero.Level >= HeroLevelCap) return false;
            return Profile.Gold >= LevelUpCost(hero);
        }

        public LevelUpResult LevelUpHero(HeroInstance hero, int times)
        {
            LevelUpResult result = new LevelUpResult();
            if (hero == null) { result.Message = "No hero selected."; return result; }
            result.NewLevel = hero.Level;

            int cap = HeroLevelCap;
            if (hero.Level >= cap)
            {
                result.Message = "Level cap reached. Raise your Wayfarer rank to continue.";
                return result;
            }

            for (int i = 0; i < times; i++)
            {
                if (hero.Level >= cap) break;
                int cost = StatMath.LevelUpCost(Balance, hero.Level);
                if (Profile.Gold < cost) break;
                Profile.Gold -= cost;
                hero.Level++;
                result.GoldSpent += cost;
                result.LevelsGained++;
            }

            result.NewLevel = hero.Level;
            result.Success = result.LevelsGained > 0;
            if (!result.Success) result.Message = "Not enough gold.";
            else if (hero.Level >= cap) result.Message = "Level cap reached.";
            return result;
        }

        // ------------------------------------------------------------------ campaign

        public bool IsStageUnlocked(StageDef stage)
        {
            if (stage == null) return false;
            return stage.GlobalIndex <= Profile.StagesCleared;
        }

        public StageDef NextStage
        {
            get
            {
                StageDef stage = Content.GetStageByGlobalIndex(Profile.StagesCleared);
                if (stage != null) return stage;
                // campaign fully cleared - let the player replay the last stage
                return Content.AllStages.Count > 0 ? Content.AllStages[Content.AllStages.Count - 1] : null;
            }
        }

        public bool CampaignComplete
        {
            get { return Profile.StagesCleared >= Content.AllStages.Count; }
        }

        public BattleRequest BuildBattleRequest(StageDef stage, int seed)
        {
            BattleRequest request = new BattleRequest();
            request.Content = Content;
            request.Stage = stage;
            request.Seed = seed;
            request.Team = Profile.TeamHeroes();
            return request;
        }

        public BattleRewards GrantBattleResult(StageDef stage, bool victory, long nowUnix)
        {
            BattleRewards rewards = new BattleRewards();
            rewards.Victory = victory;
            if (stage == null) return rewards;
            rewards.StageId = stage.Id;
            if (!victory) return rewards;

            bool firstClear = !Profile.HasClearedStage(stage.Id);
            rewards.FirstClear = firstClear;
            rewards.Gold = stage.RewardGold + (firstClear ? stage.FirstClearGold : 0);
            rewards.Shards = stage.RewardShards + (firstClear ? stage.FirstClearShards : 0);
            rewards.AccountXp = stage.RewardAccountXp;

            Profile.Gold += rewards.Gold;
            Profile.Shards += rewards.Shards;
            Profile.TotalBattlesWon++;

            if (firstClear)
            {
                Profile.ClearedStageIds.Add(stage.Id);
                if (stage.GlobalIndex >= Profile.StagesCleared)
                {
                    Profile.StagesCleared = stage.GlobalIndex + 1;
                    rewards.UnlockedNextStage = true;
                }
            }

            rewards.PlayerLevelsGained = GrantAccountXp(rewards.AccountXp);
            Profile.LastSaveUnix = nowUnix;
            return rewards;
        }

        public int GrantAccountXp(int amount)
        {
            if (amount <= 0) return 0;
            Profile.PlayerXp += amount;
            int gained = 0;
            while (true)
            {
                int needed = StatMath.PlayerXpForNextLevel(Balance, Profile.PlayerLevel);
                if (Profile.PlayerXp < needed) break;
                Profile.PlayerXp -= needed;
                Profile.PlayerLevel++;
                gained++;
                if (gained > 500) break; // paranoia guard
            }
            return gained;
        }

        public int PlayerXpForNextLevel
        {
            get { return StatMath.PlayerXpForNextLevel(Balance, Profile.PlayerLevel); }
        }

        // ------------------------------------------------------------------ idle income

        public IdleReport PeekIdle(long nowUnix)
        {
            IdleReport report = new IdleReport();
            report.CapSeconds = Balance.IdleCapHours * 3600f;

            long elapsed = nowUnix - Profile.LastIdleClaimUnix;
            if (elapsed < 0) elapsed = 0;
            if (elapsed > report.CapSeconds) { elapsed = (long)report.CapSeconds; report.Capped = true; }
            report.Seconds = elapsed;

            int stages = Math.Max(0, Profile.StagesCleared);
            report.Gold = (int)Math.Floor(StatMath.IdleGoldPerSecond(Balance, stages) * elapsed);
            report.Shards = (int)Math.Floor(StatMath.IdleShardsPerSecond(Balance, stages) * elapsed);
            return report;
        }

        public IdleReport ClaimIdle(long nowUnix)
        {
            IdleReport report = PeekIdle(nowUnix);
            Profile.Gold += report.Gold;
            Profile.Shards += report.Shards;
            Profile.LastIdleClaimUnix = nowUnix;
            return report;
        }

        public float IdleGoldPerHour
        {
            get { return StatMath.IdleGoldPerSecond(Balance, Profile.StagesCleared) * 3600f; }
        }

        public float IdleShardsPerHour
        {
            get { return StatMath.IdleShardsPerSecond(Balance, Profile.StagesCleared) * 3600f; }
        }

        // ------------------------------------------------------------------ summoning

        public bool CanSummon
        {
            get { return Profile.Shards >= Balance.SummonCost; }
        }

        public SummonOutcome Summon(int seed, long nowUnix)
        {
            SummonOutcome outcome = new SummonOutcome();
            if (Profile.Shards < Balance.SummonCost)
            {
                outcome.Message = "Not enough Ember Shards.";
                return outcome;
            }

            Profile.Shards -= Balance.SummonCost;
            outcome.CostPaid = Balance.SummonCost;
            Profile.TotalSummons++;

            DeterministicRng rng = new DeterministicRng(seed);
            HeroDef pulled = RollHero(rng);
            if (pulled == null)
            {
                Profile.Shards += Balance.SummonCost; // refund, content is broken
                outcome.Message = "The rift is empty - no hero content loaded.";
                return outcome;
            }

            outcome.Success = true;
            outcome.Hero = pulled;

            HeroInstance existing = Profile.FindHeroByDefId(pulled.Id);
            if (existing == null)
            {
                outcome.IsNew = true;
                outcome.Instance = AddHero(Profile, pulled, nowUnix);
                Profile.NormaliseTeam();
                outcome.Message = "New ally bound to your banner.";
            }
            else
            {
                existing.Copies++;
                outcome.Instance = existing;
                outcome.GoldRefund = Balance.DuplicateGold;
                outcome.ShardRefund = Balance.DuplicateShards;
                Profile.Gold += outcome.GoldRefund;
                Profile.Shards += outcome.ShardRefund;
                outcome.Message = "Echo of an ally you already command.";
            }
            return outcome;
        }

        private HeroDef RollHero(DeterministicRng rng)
        {
            float common = Balance.SummonWeightCommon;
            float rare = Balance.SummonWeightRare;
            float epic = Balance.SummonWeightEpic;

            // only offer rarities that actually have heroes
            if (Content.HeroesOfRarity(Rarity.Common).Count == 0) common = 0f;
            if (Content.HeroesOfRarity(Rarity.Rare).Count == 0) rare = 0f;
            if (Content.HeroesOfRarity(Rarity.Epic).Count == 0) epic = 0f;

            float total = common + rare + epic;
            if (total <= 0f)
            {
                if (Content.Heroes.Count == 0) return null;
                return Content.Heroes[rng.Range(0, Content.Heroes.Count)];
            }

            float roll = rng.NextFloat() * total;
            Rarity rarity;
            if (roll < epic) rarity = Rarity.Epic;
            else if (roll < epic + rare) rarity = Rarity.Rare;
            else rarity = Rarity.Common;

            List<HeroDef> pool = Content.HeroesOfRarity(rarity);
            if (pool.Count == 0) pool = Content.Heroes;
            if (pool.Count == 0) return null;
            return pool[rng.Range(0, pool.Count)];
        }

        // ------------------------------------------------------------------ misc

        public HeroInstance GrantHeroDirect(HeroDef def, long nowUnix)
        {
            if (def == null) return null;
            HeroInstance existing = Profile.FindHeroByDefId(def.Id);
            if (existing != null) { existing.Copies++; return existing; }
            HeroInstance added = AddHero(Profile, def, nowUnix);
            Profile.NormaliseTeam();
            return added;
        }
    }
}
