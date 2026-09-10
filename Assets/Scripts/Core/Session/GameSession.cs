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

        /// <summary>True when this win landed past the stage's daily paid-repeat allowance, so
        /// the purse below was scaled by BalanceConfig.RepeatRewardFloor - zero by default.</summary>
        public bool RepeatCapped;

        /// <summary>Paid repeat clears left on this stage for the rest of the reward day,
        /// counted after this win was banked.</summary>
        public int RepeatClearsRemaining;

        /// <summary>Relic Dust this win paid. Zero on a first clear and zero past the repeat
        /// cap, both by design - see GameSession.RelicDustForRepeatClear.</summary>
        public int RelicDust;

        /// <summary>
        /// The armor piece this win dropped, or null. Null is the common case - the drop is a roll
        /// against ArmorConfig.DropChance - and null is also what a repeat-capped clear always
        /// gets, for the same reason it gets no gold: a currency or an item exempt from the cap
        /// hands the uncapped farming loop straight back in a new denomination.
        ///
        /// The piece is already in Profile.OwnedArmor by the time this is returned. It is carried
        /// here so the UI can name what dropped, not so the UI can grant it.
        /// </summary>
        public ArmorPiece ArmorDrop;
    }

    /// <summary>Echoes available right now, as GameSession.PeekRecitalEchoes reports it - a
    /// snapshot, not a mutation, the same split IdleReport is for idle income.</summary>
    public sealed class RecitalEchoesStatus
    {
        public int Banked;
        public int Max;

        /// <summary>Seconds until the next Echo, or 0 if the bank is already at Max.</summary>
        public long SecondsToNext;
    }

    /// <summary>Brands available right now, as GameSession.PeekProvingBrands reports it - the
    /// same snapshot-not-mutation split RecitalEchoesStatus is for Echoes.</summary>
    public sealed class ProvingBrandsStatus
    {
        public int Banked;
        public int Max;

        /// <summary>Seconds until the next Brand, or 0 if the bank is already at Max.</summary>
        public long SecondsToNext;
    }

    /// <summary>
    /// One Proving attempt's outcome, from GameSession.GrantProvingResult.
    ///
    /// Valor only - The Proving's own currency, and nothing else. It pays no Gold, no Shards, no
    /// Relic Dust and no Slag, which keeps the mode out of every existing economy: nothing a player
    /// earns here can accelerate summoning, armor or relics, so none of those can be destabilised
    /// by however often this mode turns out to be played.
    ///
    /// This is a reversal of DESIGN-PVP section 6 (which specified Gold and Relic Dust and no new
    /// currency), made deliberately after sign-off. The doc has been corrected to match.
    /// </summary>
    public sealed class ProvingRewards
    {
        public bool Victory;

        /// <summary>Standing BEFORE this attempt resolved - what the rewards were scaled off.</summary>
        public int Standing;

        /// <summary>Standing after. Floors at zero; never negative.</summary>
        public int NewStanding;

        /// <summary>True when this win set a new ProvingHighestStanding.</summary>
        public bool NewBest;

        public int Valor;
    }

    /// <summary>One floor attempt's outcome, from GameSession.GrantRecitalResult.</summary>
    public sealed class RecitalRewards
    {
        public bool Victory;
        public int Floor;

        /// <summary>True when this win was against the player's current floor (the only case that
        /// exists today) and RecitalHighestFloor/RecitalCurrentFloor moved forward because of it.</summary>
        public bool AdvancedFloor;

        public int Gold;
        public int Shards;
        public int RelicDust;
        public int Coda;

        /// <summary>Non-empty only on a deep milestone that had an unowned relic left to grant -
        /// "something rarer" at depth, per spec. Empty on every other floor, including a deep
        /// milestone that fell back to RelicDust because nothing was left to grant.</summary>
        public string GrantedRelicId = "";

        public bool IsMilestone;
        public bool IsDeepMilestone;
        public bool IsGuardian;
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

            // Deep Recital: a new account starts at floor 1 with a full Echo bank, not an empty
            // one - nobody should have to wait hours after their first login just to try the mode
            // once. The clock is stamped now so regen (if any accrues before the first attempt)
            // measures from a real moment, not from unix epoch zero.
            profile.RecitalCurrentFloor = 1;
            profile.RecitalHighestFloor = 0;
            profile.RecitalEchoesBanked = content.Balance.RecitalEchoesMax;
            profile.RecitalEchoesClockUnix = nowUnix;

            // The Proving: same shape as the Echo bank above and for the same reason - a new
            // account can attempt immediately rather than waiting out a first recharge. The clock
            // is stamped to now rather than left at 0, which matters: a zero clock means an elapsed
            // time measured from 1970, so the first settle would compute an enormous regen and the
            // bank would be indistinguishable from full however it had actually been spent.
            profile.ProvingBrandsBanked = content.Balance.ProvingBrandsMax;
            profile.ProvingBrandsClockUnix = nowUnix;
            profile.ProvingStanding = 0;
            profile.ProvingHighestStanding = 0;

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

        // ============================================================ wave progress inside a stage

        /// <summary>
        /// Where an attempt on this stage should begin: the saved position if the player is
        /// part-way through THIS stage, otherwise its first battle.
        ///
        /// The stage id check is the whole point. Progress is stored as one (stageId, wave,
        /// battle) triple rather than a per-stage table, because only one stage is ever in
        /// progress at a time - so entering a DIFFERENT stage has to start at the beginning
        /// instead of inheriting the previous stage's position, which is what a bare wave/battle
        /// pair would have done. An empty saved id (a fresh account, or a version 10 save
        /// upgrading) matches nothing and therefore starts at (1,1), which is correct for both.
        /// </summary>
        public void ResumePointFor(StageDef stage, out int wave, out int battle)
        {
            wave = 1;
            battle = 1;
            if (stage == null) return;
            if (Profile.CampaignCurrentStageId != stage.Id) return;

            int waves = Math.Max(1, stage.WaveCount);
            int battles = Math.Max(1, stage.BattleCount);
            wave = Math.Min(Math.Max(1, Profile.CampaignCurrentWave), waves);
            battle = Math.Min(Math.Max(1, Profile.CampaignCurrentBattle), battles);
        }

        /// <summary>True when the saved position is a real mid-stage resume rather than a fresh
        /// start, which is what lets the UI say "resume" instead of "begin".</summary>
        public bool HasResumePoint(StageDef stage)
        {
            if (stage == null || Profile.CampaignCurrentStageId != stage.Id) return false;
            return Profile.CampaignCurrentWave > 1 || Profile.CampaignCurrentBattle > 1;
        }

        /// <summary>
        /// Records a won battle inside a stage and returns TRUE when that win finished the stage.
        ///
        /// This does NOT pay out. Rewards stay at stage granularity per
        /// DESIGN-CAMPAIGN-STRUCTURE.md - "repeat cap and idle income stay at Stage granularity" -
        /// so 143 of a stage's 144 wins move a cursor and pay nothing, and the caller invokes
        /// GrantBattleResult once, on the win that returns true here. Paying per battle would
        /// multiply every stage's income by 144 and is exactly what the design rules out.
        /// </summary>
        public bool RecordBattleWon(StageDef stage, int wave, int battle)
        {
            if (stage == null) return true;

            int nextWave, nextBattle;
            bool more = CampaignWaveGenerator.Next(stage, wave, battle, out nextWave, out nextBattle);
            if (more)
            {
                Profile.CampaignCurrentStageId = stage.Id;
                Profile.CampaignCurrentWave = nextWave;
                Profile.CampaignCurrentBattle = nextBattle;
                return false;
            }

            ClearStageProgress(stage);
            return true;
        }

        /// <summary>
        /// A lost battle. Deliberately does nothing at all.
        ///
        /// "Defeat inside a stage: retry the single battle that was lost, not the wave and not the
        /// stage" (DESIGN-CAMPAIGN-STRUCTURE.md, decided 2026-09-07). Leaving the cursor untouched
        /// IS that behaviour - the next attempt reads the same (wave, battle) back out of
        /// ResumePointFor and re-fights it. This method exists so the call site reads as a
        /// deliberate no-op rather than a missing branch someone should add later.
        /// </summary>
        public void RecordBattleLost(StageDef stage, int wave, int battle)
        {
        }

        /// <summary>Forgets any in-stage position for this stage. Called when a stage completes,
        /// so the cursor never points into a stage the player has finished.</summary>
        public void ClearStageProgress(StageDef stage)
        {
            if (stage == null) return;
            if (Profile.CampaignCurrentStageId != stage.Id) return;
            Profile.CampaignCurrentStageId = "";
            Profile.CampaignCurrentWave = 1;
            Profile.CampaignCurrentBattle = 1;
        }

        /// <summary>
        /// Replaying a stage that is already fully cleared: one tap, resolved instantly.
        ///
        /// ONE SIMULATION, NOT 144. The battle it runs is the stage's LAST one - its hardest, at
        /// the ceiling - because that is the battle that decides whether the line can clear the
        /// stage at all. A team that beats the ceiling beats all 143 easier battles below it, so
        /// running the hardest one answers the same question 144 runs would, in 1/144th of the
        /// work, and answers it honestly: a sweep can still FAIL, and an under-powered team gets
        /// told so rather than being handed a free clear for a stage it once beat.
        ///
        /// Rewards route through GrantBattleResult unchanged, so a sweep spends one of
        /// repeatFullClearsPerDay exactly as a normal clear does. That system's granularity is
        /// untouched: it still counts stage clears, and a sweep is one stage clear.
        /// </summary>
        public BattleRewards SweepClearedStage(StageDef stage, int seed, long nowUnix)
        {
            BattleRewards rewards = new BattleRewards();
            if (stage == null) return rewards;

            StageDef hardest = CampaignWaveGenerator.BuildBattle(
                stage, Math.Max(1, stage.WaveCount), Math.Max(1, stage.BattleCount));

            BattleResult result = CombatSimulation.Run(BuildBattleRequest(hardest, seed));
            if (!result.Victory)
            {
                rewards.Victory = false;
                rewards.StageId = stage.Id;
                return rewards;
            }
            return GrantBattleResult(stage, true, nowUnix);
        }

        /// <summary>Whether a one-tap sweep is offered: the stage has a real curve and the player
        /// has already finished it the long way. A stage never cleared has to be fought.</summary>
        public bool CanSweep(StageDef stage)
        {
            return stage != null && stage.HasWaveCurve && Profile.HasClearedStage(stage.Id);
        }

        public bool CampaignComplete
        {
            get { return Profile.StagesCleared >= Content.AllStages.Count; }
        }

        /// <summary>
        /// Builds the request for ONE battle. A stage that expands to a ladder must arrive already
        /// resolved to a position on it, via CampaignWaveGenerator.BuildBattle.
        ///
        /// The refusal below is the point. The authored StageDef's Enemies list is, by
        /// construction, the wave 1 battle 1 roster, so passing it here does not fail - it quietly
        /// fights the opening battle and returns a perfectly ordinary result. Three balance
        /// instruments did that for months and reported it as the whole stage; the tables looked
        /// right and did not move when the campaign was restructured underneath them, which is the
        /// only reason it was ever noticed. Making the unresolved case throw converts a silent
        /// wrong answer into a loud one at the call site that caused it.
        ///
        /// Stages with no ladder (Recital floors, Proving matches, prologue steps, any synthetic
        /// one-battle stage) are unaffected: HasWaveStructure is false for them and BuildBattle
        /// hands them back unchanged, so nothing has to know about this.
        /// </summary>
        public BattleRequest BuildBattleRequest(StageDef stage, int seed)
        {
            if (stage != null && stage.TotalBattles > 1 && stage.ResolvedBattleOrdinal <= 0)
                throw new InvalidOperationException(
                    "BuildBattleRequest was handed the authored StageDef '" + stage.Id + "', which expands to "
                    + stage.TotalBattles + " battles. Resolve it first with CampaignWaveGenerator.BuildBattle("
                    + "stage, wave, battle) - passing it unresolved silently fights wave 1 battle 1.");

            BattleRequest request = new BattleRequest();
            request.Content = Content;
            request.Stage = stage;
            request.Seed = seed;
            request.Team = Profile.TeamHeroes();
            // The profile rides along solely so the simulation can resolve each hero's armor uids
            // into pieces - an armor uid resolves against the save, not against content, which is
            // the one thing Content on this request cannot do.
            request.Profile = Profile;
            return request;
        }

        /// <summary>
        /// A battle against a snapshot opponent. Identical to BuildBattleRequest except for the one
        /// extra field, which is the whole point of the boundary: the fight does not know or care
        /// that the far side came from a generator rather than from authored content.
        ///
        /// The stage still rides along because a fight needs an environment and a name to display -
        /// only who stands opposite changes, and CombatSimulation ignores Stage.Enemies entirely
        /// when Opponent is set.
        /// </summary>
        public BattleRequest BuildProvingRequest(StageDef stage, BattleLoadout opponent, int seed)
        {
            BattleRequest request = BuildBattleRequest(stage, seed);
            request.Opponent = opponent;
            return request;
        }

        /// <summary>
        /// A fight the PLAYER's side of which is not the player's team. The prologue's fixed squad
        /// fights here; the profile is not consulted for either half.
        ///
        /// The mirror of BuildProvingRequest, which replaces the ENEMY line with a snapshot and
        /// leaves the player's own team alone. This replaces the player's line instead, which
        /// BattleRequest already supports without changing anything: Team is a plain
        /// List&lt;HeroInstance&gt; that CombatSimulation reads as given.
        ///
        /// Profile is deliberately left NULL rather than passed through. It is read for exactly one
        /// thing - resolving armor uids against OwnedArmor - and a prologue hero owns no armor, so
        /// passing the real profile could only ever let save state leak into a scripted fight.
        /// </summary>
        public BattleRequest BuildPrologueRequest(StageDef stage, List<HeroInstance> squad, int seed)
        {
            BattleRequest request = new BattleRequest();
            request.Content = Content;
            request.Stage = stage;
            request.Seed = seed;
            request.Team = squad != null ? squad : new List<HeroInstance>();
            request.Profile = null;
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

            int gold = stage.RewardGold + (firstClear ? stage.FirstClearGold : 0);
            int shards = stage.RewardShards + (firstClear ? stage.FirstClearShards : 0);
            int accountXp = stage.RewardAccountXp;

            // The repeat cap, per DESIGN-REPLAY-ECONOMY. A first clear is progress rather than
            // farming and is never counted or capped; every clear after it spends one of the
            // stage's paid repeats for the day. Past the allowance the fight still ran, still
            // counts as a win and still unlocks nothing new - it simply pays the floor, which is
            // zero, so the loss-adjust-reclear loop stays free while the twentieth farm run of
            // the day stops paying.
            // Relic Dust rides the same branch, per DESIGN-RELIC-PROGRESSION section 1: paid by a
            // repeat clear, never by a first clear, and scaled by stage difficulty. It is subject
            // to the repeat cap for the same reason gold is - a currency exempt from the cap would
            // hand the uncapped farming loop straight back, just denominated differently.
            int relicDust = firstClear ? 0 : RelicDustForRepeatClear(stage);

            RollDailyReset(nowUnix);
            if (!firstClear)
            {
                int used = Profile.ClearsTodayFor(stage.Id);
                if (used >= RepeatFullClearsPerDay)
                {
                    float floor = Balance.RepeatRewardFloor;
                    if (floor < 0f) floor = 0f;
                    if (floor > 1f) floor = 1f;
                    // Only ever scale below par, so the uncapped purse stays exactly the integer
                    // the stage authored instead of a float round trip of it.
                    gold = (int)Math.Floor(gold * (double)floor);
                    shards = (int)Math.Floor(shards * (double)floor);
                    accountXp = (int)Math.Floor(accountXp * (double)floor);
                    relicDust = (int)Math.Floor(relicDust * (double)floor);
                    rewards.RepeatCapped = true;
                }
                else
                {
                    Profile.StageClearsToday[stage.Id] = used + 1;
                }
            }

            rewards.Gold = gold;
            rewards.Shards = shards;
            rewards.AccountXp = accountXp;
            rewards.RelicDust = relicDust;

            Profile.Gold += rewards.Gold;
            Profile.Shards += rewards.Shards;
            Profile.RelicDust += rewards.RelicDust;
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

            // The armor drop rides the repeat cap on exactly the branch Relic Dust does, per
            // DESIGN-ARMOR section 8: a capped clear drops nothing, so armor cannot become the
            // uncapped farming loop in a new denomination. Rolled last, after the purse is settled,
            // so its seed cannot depend on anything the purse did.
            rewards.ArmorDrop = rewards.RepeatCapped ? null : RollArmorDrop(stage, nowUnix);

            rewards.PlayerLevelsGained = GrantAccountXp(rewards.AccountXp);
            rewards.RepeatClearsRemaining = RepeatClearsRemaining(stage, nowUnix);
            Profile.LastSaveUnix = nowUnix;
            return rewards;
        }

        // ------------------------------------------------------------------ armor

        /// <summary>
        /// Rolls one armor piece for a full-purse stage clear and adds it to the collection, or
        /// returns null - which is the common case, since the whole thing is gated on
        /// ArmorConfig.DropChance.
        ///
        /// The seed is derived from data the caller already has (the clock, the stage's depth and
        /// how many pieces the player already owns) rather than from a shared RNG, for the same
        /// reason the roller takes an int at all: a drop must be reproducible in a test, and it
        /// must not touch the battle's RNG stream. TotalBattlesWon is deliberately NOT in the mix -
        /// it is incremented above this line, and a seed that moved with the purse would tie a
        /// piece's rolls to reward tuning.
        /// </summary>
        public ArmorPiece RollArmorDrop(StageDef stage, long nowUnix)
        {
            if (stage == null) return null;
            ArmorConfig config = Content != null ? Content.Armor : null;
            if (config == null || !config.IsUsable) return null;

            int baseSeed = ArmorSeed(nowUnix, stage.GlobalIndex, Profile.OwnedArmor.Count);

            // Four independent draws off four derived seeds rather than one RNG walked four times:
            // each of RollSlot/RollAspect/RollTemper/Roll builds its own DeterministicRng, so they
            // must not be handed the same integer or a slot and an aspect would move together.
            DeterministicRng gate = new DeterministicRng(baseSeed);
            if (!gate.Chance(config.DropChance)) return null;

            ArmorSlot slot = ArmorRoller.RollSlot(baseSeed + 1);
            Aspect aspect = ArmorRoller.RollAspect(baseSeed + 2);
            ArmorTemper temper = ArmorRoller.RollTemper(config, stage.GlobalIndex, baseSeed + 3);

            ArmorPiece piece = ArmorRoller.Roll(config, slot, temper, aspect, baseSeed + 4);
            if (piece == null) return null;

            piece.Uid = Profile.AllocateArmorUid();
            Profile.OwnedArmor.Add(piece);
            return piece;
        }

        /// <summary>
        /// A stable, cheap mix of the three inputs a drop is allowed to depend on. Not
        /// cryptographic and not trying to be - it exists so two drops in the same second at the
        /// same stage do not produce the same piece, and so a test can reproduce one exactly.
        /// </summary>
        private static int ArmorSeed(long nowUnix, int stageIndex, int ownedCount)
        {
            unchecked
            {
                int h = (int)(nowUnix ^ (nowUnix >> 32));
                h = h * 486187739 + stageIndex * 31 + ownedCount * 7919;
                return h == 0 ? 1 : h;
            }
        }

        /// <summary>Slag one piece salvages for, by its temper. The single source both SalvageArmor
        /// and the inventory screen read, so a displayed payout and a paid one cannot drift - the
        /// same reason RelicPrice exists for relics.</summary>
        public int SalvageValue(ArmorPiece piece)
        {
            if (piece == null || Content == null) return 0;
            ArmorTemperDef def = Content.Armor.GetTemper(piece.Temper);
            return def != null ? def.SalvageSlag : 0;
        }

        /// <summary>Slag one reroll costs, by the piece's temper. Same single-source reasoning as
        /// SalvageValue.</summary>
        public int RerollCost(ArmorPiece piece)
        {
            if (piece == null || Content == null) return 0;
            ArmorTemperDef def = Content.Armor.GetTemper(piece.Temper);
            return def != null ? def.RerollSlag : 0;
        }

        /// <summary>
        /// Slag one Combine costs, keyed by the temper it PRODUCES rather than the temper of any
        /// input. SalvageValue and RerollCost key off a piece's own temper because neither action
        /// changes it; Combine keys off the output because that is the one number a player is
        /// actually deciding whether to pay for. Same single-source reasoning as both: this is what
        /// CombineArmor itself reads to charge, so a shown price and a paid one cannot drift.
        /// </summary>
        public int CombineCost(ArmorTemper outputTemper)
        {
            if (Content == null) return 0;
            ArmorTemperDef def = Content.Armor.GetTemper(outputTemper);
            return def != null ? def.CombineSlag : 0;
        }

        /// <summary>
        /// Puts a piece in its own slot on a hero, displacing whatever was there and taking it off
        /// whoever else was wearing it. Modelled on AssignRelic, with the one extra rule a
        /// six-slot system needs: a piece only ever goes in the slot it is FOR, so the slot is read
        /// off the piece rather than passed in and cannot be got wrong by a caller.
        /// </summary>
        public bool EquipArmor(HeroInstance hero, string armorUid)
        {
            if (hero == null) return false;
            ArmorPiece piece = Profile.FindArmor(armorUid);
            if (piece == null) return false;

            int index = PlayerProfile.SlotIndex(piece.Slot);
            if (index < 0 || index >= hero.EquippedArmor.Length) return false;

            // Taking it off its previous wearer first covers the case where that wearer IS this
            // hero in a different slot, which cannot happen through this method but can through a
            // hand-edited save that NormaliseArmor has not run over yet.
            HeroInstance previous = Profile.HeroWearingArmor(armorUid);
            if (previous != null)
            {
                int previousIndex = PlayerProfile.SlotIndex(piece.Slot);
                if (previousIndex >= 0 && previousIndex < previous.EquippedArmor.Length
                    && previous.EquippedArmor[previousIndex] == armorUid)
                    previous.EquippedArmor[previousIndex] = "";
            }

            // Whatever was in the slot returns to the pool rather than being destroyed. There is no
            // "swap" concept here for the same reason there is none for relics: the pool is where an
            // unequipped thing lives, so displacing one is just clearing a reference.
            hero.EquippedArmor[index] = armorUid;
            return true;
        }

        /// <summary>Empties one of a hero's slots, returning whatever was in it to the pool.</summary>
        public bool UnequipArmor(HeroInstance hero, ArmorSlot slot)
        {
            if (hero == null) return false;
            int index = PlayerProfile.SlotIndex(slot);
            if (index < 0 || index >= hero.EquippedArmor.Length) return false;
            if (string.IsNullOrEmpty(hero.EquippedArmor[index])) return false;
            hero.EquippedArmor[index] = "";
            return true;
        }

        /// <summary>
        /// Destroys a piece and pays its Slag. Refuses a piece somebody is wearing rather than
        /// silently unequipping it: salvage is irreversible, and an accidental one that also
        /// stripped a hero's slot would be two mistakes deep before the player noticed.
        ///
        /// Pays BEFORE removing, so a piece can never be removed without being paid for.
        /// </summary>
        public bool SalvageArmor(string armorUid)
        {
            ArmorPiece piece = Profile.FindArmor(armorUid);
            if (piece == null) return false;
            if (Profile.HeroWearingArmor(armorUid) != null) return false;

            Profile.Slag += SalvageValue(piece);
            Profile.OwnedArmor.Remove(piece);
            return true;
        }

        /// <summary>
        /// Reroll, one of the Forge's three actions. Spends Slag and rerolls every substat on a
        /// piece, keeping its slot, primary, aspect and temper.
        ///
        /// Order matters and is deliberate: the reroll is attempted FIRST and the Slag is deducted
        /// only once it actually happened. ArmorRoller.Reroll returns false without touching the
        /// piece when the content cannot roll, and charging before that check would take a
        /// player's currency for nothing. It works on an equipped piece as well as a pooled one -
        /// unlike salvage, a reroll is not destructive, so making the player unequip first would be
        /// friction with no failure behind it.
        /// </summary>
        public bool RerollArmor(string armorUid, long nowUnix)
        {
            ArmorPiece piece = Profile.FindArmor(armorUid);
            if (piece == null) return false;
            ArmorConfig config = Content != null ? Content.Armor : null;
            if (config == null || !config.IsUsable) return false;

            int cost = RerollCost(piece);
            if (Profile.Slag < cost) return false;

            int seed = ArmorSeed(nowUnix, PlayerProfile.SlotIndex(piece.Slot), Profile.OwnedArmor.Count + piece.SubStats.Count);
            if (!ArmorRoller.Reroll(config, piece, seed)) return false;

            Profile.Slag -= cost;
            return true;
        }

        /// <summary>
        /// Combines three same-slot, same-temper pieces into one freshly rolled piece exactly one
        /// temper higher. The only way Gilded or Radiant come to exist - RollArmorDrop's temper
        /// roll is capped at ArmorInfo.MaxDropTemper, so those two rungs are reachable through
        /// nothing else. See DESIGN-ARMOR section 7.
        ///
        /// The output is a brand new ArmorRoller.Roll result, not a stat graft off the strongest
        /// input and not a mutation of any of the three - "freshly rolled substats" was the
        /// explicit requirement, and a fresh roll is also the only answer that does not need a rule
        /// for what happens when the three inputs disagree on aspect, which nothing about "same
        /// slot, same tier" requires them to share. The new piece's aspect is drawn the same way a
        /// drop's is: uniformly, independent of the inputs.
        ///
        /// Refuses an equipped input rather than silently unequipping it, the same rule
        /// SalvageArmor enforces and for the same reason: Combine is destructive (three real pieces
        /// stop existing), and an accidental one that also stripped a hero's slot would be two
        /// mistakes deep before the player noticed. Reroll allows an equipped piece because reroll
        /// is not destructive; Combine is, so it follows Salvage's rule instead.
        ///
        /// Order matters and is deliberate, the same as RerollArmor above: the new piece is rolled
        /// and validated FIRST, and only once that succeeds are the three inputs removed and Slag
        /// deducted. A refusal at any earlier check - mismatched slot, mismatched temper, already
        /// at Radiant, insufficient Slag, an unrollable config - leaves all three inputs, every
        /// other owned piece, and the Slag purse completely untouched.
        /// </summary>
        public bool CombineArmor(string armorUidA, string armorUidB, string armorUidC, long nowUnix)
        {
            if (string.IsNullOrEmpty(armorUidA) || string.IsNullOrEmpty(armorUidB) || string.IsNullOrEmpty(armorUidC))
                return false;
            if (armorUidA == armorUidB || armorUidA == armorUidC || armorUidB == armorUidC)
                return false;   // three DISTINCT pieces - the same uid twice is not three inputs

            ArmorPiece a = Profile.FindArmor(armorUidA);
            ArmorPiece b = Profile.FindArmor(armorUidB);
            ArmorPiece c = Profile.FindArmor(armorUidC);
            if (a == null || b == null || c == null) return false;
            if (a.Slot != b.Slot || a.Slot != c.Slot) return false;
            if (a.Temper != b.Temper || a.Temper != c.Temper) return false;

            if (Profile.HeroWearingArmor(armorUidA) != null
                || Profile.HeroWearingArmor(armorUidB) != null
                || Profile.HeroWearingArmor(armorUidC) != null)
                return false;

            // "Cannot skip a tier" is enforced by construction, not by a separate check: the output
            // temper is ALWAYS ArmorInfo.NextTemper of the shared input temper, so there is no
            // parameter anywhere on this method through which a caller could ask for anything else.
            // At the top of the ladder there is no next rung, and combining stops being possible.
            ArmorTemper? outputTemper = ArmorInfo.NextTemper(a.Temper);
            if (outputTemper == null) return false;

            ArmorConfig config = Content != null ? Content.Armor : null;
            if (config == null || !config.IsUsable) return false;

            int cost = CombineCost(outputTemper.Value);
            if (Profile.Slag < cost) return false;

            int baseSeed = ArmorSeed(nowUnix, PlayerProfile.SlotIndex(a.Slot), Profile.OwnedArmor.Count);
            Aspect aspect = ArmorRoller.RollAspect(baseSeed + 1);
            ArmorPiece produced = ArmorRoller.Roll(config, a.Slot, outputTemper.Value, aspect, baseSeed + 2);
            if (produced == null) return false;   // content cannot roll this slot/temper - refuse, charge nothing

            // Only now, after the new piece exists, does anything about the profile change.
            Profile.OwnedArmor.Remove(a);
            Profile.OwnedArmor.Remove(b);
            Profile.OwnedArmor.Remove(c);
            produced.Uid = Profile.AllocateArmorUid();
            Profile.OwnedArmor.Add(produced);
            Profile.Slag -= cost;
            return true;
        }

        // ------------------------------------------------------------------ repeat cap

        /// <summary>Paid repeat clears each already-cleared stage gets per reward day.</summary>
        public int RepeatFullClearsPerDay
        {
            get { return Math.Max(0, Balance.RepeatFullClearsPerDay); }
        }

        /// <summary>
        /// Start of the reward day nowUnix falls in: the most recent Balance.DailyResetHourUtc
        /// boundary at or before it, in UTC. Floors rather than truncates, so it stays correct
        /// for timestamps before the epoch instead of jumping a day forward.
        /// </summary>
        public long DayStartUnix(long nowUnix)
        {
            const long Day = 86400L;
            long offset = (long)Balance.DailyResetHourUtc * 3600L;
            long shifted = nowUnix - offset;
            long days = shifted >= 0 ? shifted / Day : -(((-shifted) + Day - 1L) / Day);
            return days * Day + offset;
        }

        /// <summary>
        /// Empties the per-stage repeat counters if nowUnix has crossed into a later reward day,
        /// and returns true when it did. The stamp only ever moves FORWARD: with no server the
        /// device clock is the only source of truth, so a clock wound backwards must not buy a
        /// fresh set of paid clears (DESIGN-REPLAY-ECONOMY). There is nothing to sell and no
        /// leaderboard, so a player who forces the clock forward only cheats themselves.
        /// </summary>
        public bool RollDailyReset(long nowUnix)
        {
            long start = DayStartUnix(nowUnix);
            if (start <= Profile.DailyResetUnix) return false;
            Profile.DailyResetUnix = start;
            Profile.StageClearsToday.Clear();
            return true;
        }

        /// <summary>
        /// Paid repeat clears left on a stage for the rest of the reward day. A stage the player
        /// has not cleared yet reports the full allowance: the first clear of a stage is never
        /// capped, so there is nothing spent against it.
        /// </summary>
        public int RepeatClearsRemaining(StageDef stage, long nowUnix)
        {
            if (stage == null) return 0;
            RollDailyReset(nowUnix);
            if (!Profile.HasClearedStage(stage.Id)) return RepeatFullClearsPerDay;
            int left = RepeatFullClearsPerDay - Profile.ClearsTodayFor(stage.Id);
            return left > 0 ? left : 0;
        }

        /// <summary>True when a further clear of this stage would pay the floor instead of the
        /// purse. Never true for a stage the player has not cleared yet.</summary>
        public bool RepeatCapReached(StageDef stage, long nowUnix)
        {
            if (stage == null || !Profile.HasClearedStage(stage.Id)) return false;
            return RepeatClearsRemaining(stage, nowUnix) <= 0;
        }

        // ------------------------------------------------------------------ relic progression

        /// <summary>
        /// Relic Dust one repeat clear of a stage pays, before the repeat cap is applied to it.
        ///
        /// PLACEHOLDER SOURCE, and the comment matters more than the arithmetic. DESIGN-RELIC-
        /// PROGRESSION section 1 hangs Relic Dust off the repeat-clear loop because that loop was
        /// the only grind the game had when it was written. The intended long-term home is harder,
        /// dedicated content - the endless mode proposed as The Deep Recital in
        /// DESIGN-DEEP-RECITAL.md.
        ///
        /// ⚠️ The Deep Recital is now built (GameSession.GrantRecitalResult), and the instruction
        /// this comment used to carry - "move this, don't add a second source" - was deliberately
        /// NOT followed. Cutting repeat-clear Dust would strand a fresh account with zero Dust
        /// income until their roster can survive Recital floors, which start well above a new
        /// player's level; repeat clears stay as the early-game source, milestone Recital floors
        /// as the deep-game one. This is a real, deliberate deviation from the original plan, not
        /// an oversight - flagged here so it isn't mistaken for one.
        ///
        /// First clears pay nothing, per the doc: first-clear rewards are already designed and
        /// tuned, and adding a currency to them would mean rebalancing them.
        ///
        /// The curve is base + perStageIndex * globalIndex, matching the base + per-stage shape
        /// balance.json already uses for idle income. Both numbers are a balance pass - the doc
        /// defers the curve the same way DESIGN-BUILD-ORDER task 14 defers recommendedPower.
        ///
        /// No chapter gating: chapter 1 is the only chapter, so gating on a chapter-2 clear would
        /// lock out every current player. Revisit when there is a second boundary to gate against.
        /// </summary>
        public int RelicDustForRepeatClear(StageDef stage)
        {
            if (stage == null) return 0;
            int dust = Balance.RelicDustPerRepeatClear + Balance.RelicDustPerStageIndex * Math.Max(0, stage.GlobalIndex);
            return dust > 0 ? dust : 0;
        }

        /// <summary>Highest tier a relic may be owned at. 1 until the balance pass authors tier 2.</summary>
        public int RelicMaxTier
        {
            get { return Math.Max(1, Balance.RelicMaxTier); }
        }

        /// <summary>
        /// Adds a relic to the player's collection UNASSIGNED, which is the state
        /// DESIGN-RELIC-PROGRESSION section 3 exists to make representable. Pairing a relic with a
        /// hero is flavour and a suggested starting point, not a constraint, so nothing is
        /// auto-equipped. Returns false when the relic is unknown to content or already owned -
        /// there is at most one entry per relic id, and the shop shows an owned relic as owned
        /// rather than re-purchasable.
        ///
        /// This does NOT charge Relic Dust. Costs live with the shop, which is not built; this is
        /// the model operation the shop will call once it is.
        /// </summary>
        public bool GrantRelic(string relicId)
        {
            if (string.IsNullOrEmpty(relicId)) return false;
            if (Content.GetRelic(relicId) == null) return false;
            if (Profile.OwnsRelic(relicId)) return false;
            OwnedRelic owned = new OwnedRelic();
            owned.RelicId = relicId;
            owned.Tier = 1;
            Profile.OwnedRelics.Add(owned);
            return true;
        }

        /// <summary>
        /// Equips an owned relic on a hero. Free and reversible, per section 3: reassignment is a
        /// team-building decision, not a resource expenditure - no cost, no cooldown.
        ///
        /// Only one hero holds a given relic at a time, so this unassigns it from whoever held it
        /// first. relicSlotsPerHero is 1, so it also displaces whatever the target hero was
        /// already wearing, which returns to the collection unassigned rather than being lost.
        /// Returns false for an unowned relic or a hero that is not in this profile.
        /// </summary>
        public bool AssignRelic(HeroInstance hero, string relicId)
        {
            if (hero == null || string.IsNullOrEmpty(relicId)) return false;
            if (!Profile.OwnsRelic(relicId)) return false;
            if (Profile.FindHeroByUid(hero.Uid) != hero) return false;

            HeroInstance previous = Profile.HeroHoldingRelic(relicId);
            if (previous == hero) return true;
            if (previous != null) previous.EquippedRelicId = "";
            hero.EquippedRelicId = relicId;
            return true;
        }

        /// <summary>
        /// The Relic Dust cost to buy one relic from the shop, by its grade. The single source both
        /// BuyRelic and the shop screen read, so a displayed price and a charged price cannot drift
        /// apart. A legendary (stat-only) relic prices at RelicShopPrice, unchanged from before the
        /// grade rename; a mythic relic prices at the much steeper RelicMythicShopPrice, per
        /// DESIGN-RELICS' Mythic pricing section - deliberately a long grind, not a quick buy.
        /// </summary>
        public int RelicPrice(RelicDef relic)
        {
            if (relic == null) return Balance.RelicShopPrice;
            return relic.Grade == RelicGrade.Mythic ? Balance.RelicMythicShopPrice : Balance.RelicShopPrice;
        }

        /// <summary>
        /// Purchases a relic from the shop: deducts its grade's price in Relic Dust and adds it to
        /// the collection unassigned. Returns false when the relic is unknown, already owned, or
        /// the player cannot afford it — the caller should surface whichever message fits.
        /// </summary>
        public bool BuyRelic(string relicId, long nowUnix)
        {
            if (string.IsNullOrEmpty(relicId)) return false;
            if (Profile.OwnsRelic(relicId)) return false;
            RelicDef relic = Content.GetRelic(relicId);
            if (relic == null) return false;
            int price = RelicPrice(relic);
            if (Profile.RelicDust < price) return false;
            if (!GrantRelic(relicId)) return false;
            SpendRelicDust(price, nowUnix);
            return true;
        }

        /// <summary>
        /// The one choke point every Relic Dust expenditure routes through — today only BuyRelic
        /// above, since relic upgrades (DESIGN-RELIC-PROGRESSION section 2, RelicMaxTier) are not
        /// built yet. Whoever builds the upgrade spend should call this rather than decrementing
        /// Profile.RelicDust directly, so RelicDustSpent events keep seeing every Dust expenditure
        /// without a second call site to remember.
        /// </summary>
        private void SpendRelicDust(long amount, long nowUnix)
        {
            Profile.RelicDust -= amount;
            RecordEventActivity(EventActivityKind.RelicDustSpent, amount, nowUnix);
        }

        /// <summary>Takes a hero's relic off them. The relic stays owned, just unassigned.</summary>
        public bool UnassignRelic(HeroInstance hero)
        {
            if (hero == null || string.IsNullOrEmpty(hero.EquippedRelicId)) return false;
            hero.EquippedRelicId = "";
            return true;
        }

        // ------------------------------------------------------------------ events
        //
        // Activity-driven, not switch-driven: RecordEventActivity takes an EventActivityKind and
        // walks every currently-active EventDef, comparing its own Activity field against the kind
        // passed in. Adding a new activity kind later is one enum member (Enums.cs) plus one call
        // to RecordEventActivity at that activity's own natural call site (Summon and
        // SpendRelicDust are the two today) — never a new case here, and never a touch to
        // CombatSimulation, since no activity kind so far happens inside a battle.

        /// <summary>Every event currently inside its UTC window - what the events screen lists.</summary>
        public List<EventDef> ActiveEvents(long nowUnix)
        {
            List<EventDef> result = new List<EventDef>();
            for (int i = 0; i < Content.Events.Count; i++)
                if (Content.Events[i].IsActive(nowUnix)) result.Add(Content.Events[i]);
            return result;
        }

        /// <summary>Progress for one event, created empty on first touch rather than requiring a
        /// separate "start tracking this event" step - the same lazy pattern OwnedRelic's absence
        /// already uses for "does not own it yet".</summary>
        public EventProgressEntry GetEventProgress(string eventId)
        {
            EventProgressEntry existing = Profile.FindEventProgress(eventId);
            if (existing != null) return existing;
            EventProgressEntry created = new EventProgressEntry();
            created.EventId = eventId;
            Profile.EventProgress.Add(created);
            return created;
        }

        /// <summary>
        /// Records one occurrence of an activity against every currently-active event that earns
        /// points from it. amount is the activity's own unit - 1 for a discrete occurrence like a
        /// summon, or a quantity like the Relic Dust spent in one purchase. A no-op for any event
        /// outside its window: points and currency are earned only during it, though a milestone or
        /// currency tier already earned stays claimable after it closes (ClaimEventMilestone and
        /// RedeemEventCurrency do not re-check the window).
        /// </summary>
        public void RecordEventActivity(EventActivityKind kind, long amount, long nowUnix)
        {
            if (amount <= 0) return;
            for (int i = 0; i < Content.Events.Count; i++)
            {
                EventDef evt = Content.Events[i];
                if (evt.Activity != kind || !evt.IsActive(nowUnix)) continue;
                EventProgressEntry progress = GetEventProgress(evt.Id);
                progress.Points += (long)Math.Floor(amount * evt.PointsPerActivity);
                if (evt.HasCurrency) progress.Currency += (long)Math.Floor(amount * evt.CurrencyPerActivity);
            }
        }

        private void ApplyEventReward(EventReward reward)
        {
            if (reward == null) return;
            Profile.Gold += reward.Gold;
            Profile.Shards += reward.Shards;
            Profile.RelicDust += reward.RelicDust;
        }

        /// <summary>
        /// Claims one milestone's reward. Points are never spent - the ladder is cumulative and
        /// permanent - so ClaimedMilestoneThresholds is the only thing standing between this and a
        /// repeat payout: checked and updated in the same call, before the reward is applied, so
        /// there is no window where a second call could slip through between the check and the
        /// grant. Returns false for an unknown event or milestone, an already-claimed one, or one
        /// whose threshold the player has not reached yet.
        /// </summary>
        public bool ClaimEventMilestone(string eventId, long threshold)
        {
            EventDef evt = Content.GetEvent(eventId);
            if (evt == null) return false;
            EventMilestone milestone = evt.FindMilestone(threshold);
            if (milestone == null) return false;
            EventProgressEntry progress = GetEventProgress(eventId);
            if (progress.ClaimedMilestoneThresholds.Contains(threshold)) return false;
            if (progress.Points < threshold) return false;
            progress.ClaimedMilestoneThresholds.Add(threshold);
            ApplyEventReward(milestone.Reward);
            return true;
        }

        /// <summary>
        /// Redeems one tier of an event's currency ladder: spends Cost of the event's own currency,
        /// grants Reward. Unlike a milestone this is a genuine purchase rather than an achievement -
        /// the balance is actually deducted - so redeeming the same tier twice is two purchases, not
        /// a double-claim, and nothing tracks a "redeemed" state the way ClaimedMilestoneThresholds
        /// does. Returns false for an unknown event, an event with no currency, an unknown tier, or
        /// insufficient balance.
        /// </summary>
        public bool RedeemEventCurrency(string eventId, long cost)
        {
            EventDef evt = Content.GetEvent(eventId);
            if (evt == null || !evt.HasCurrency) return false;
            EventCurrencyTier tier = evt.FindCurrencyTier(cost);
            if (tier == null) return false;
            EventProgressEntry progress = GetEventProgress(eventId);
            if (progress.Currency < cost) return false;
            progress.Currency -= cost;
            ApplyEventReward(tier.Reward);
            return true;
        }

        // ------------------------------------------------------------------ Deep Recital
        //
        // A persistent one-way floor ladder (PlayerProfile.RecitalHighestFloor), not a continuous
        // run: each attempt is one floor's fight, always at full HP/energy like every other battle
        // in the game, win or lose. See RecitalGenerator for how a floor number becomes a fightable
        // StageDef, and DESIGN-BUILD-ORDER's Deep Recital entry for how this shape and
        // DESIGN-DEEP-RECITAL.md disagree - that document specifies a continuous multi-floor run
        // with no reset between floors, ended by a wipe. This is not that.

        public StageDef BuildRecitalStage(int floor)
        {
            return RecitalGenerator.BuildFloor(Content, floor);
        }

        /// <summary>
        /// Echoes available right now, without spending anything or mutating the profile - Peek,
        /// not Settle, same split IdleReport/PeekIdle already uses for idle income. Backwards-clock
        /// immune: if nowUnix is at or before the stored clock, elapsed reads as zero, never
        /// negative, so a wound-back device clock manufactures no regen.
        /// </summary>
        public RecitalEchoesStatus PeekRecitalEchoes(long nowUnix)
        {
            RecitalEchoesStatus status = new RecitalEchoesStatus();
            status.Max = Math.Max(0, Balance.RecitalEchoesMax);
            long rechargeSeconds = RecitalRechargeSeconds();
            long elapsed = nowUnix - Profile.RecitalEchoesClockUnix;
            if (elapsed < 0) elapsed = 0;
            long gained = rechargeSeconds > 0 ? elapsed / rechargeSeconds : 0;
            status.Banked = (int)Math.Min(status.Max, Profile.RecitalEchoesBanked + gained);

            if (status.Banked >= status.Max || rechargeSeconds <= 0) status.SecondsToNext = 0;
            else status.SecondsToNext = rechargeSeconds - (elapsed % rechargeSeconds);
            return status;
        }

        private long RecitalRechargeSeconds()
        {
            return (long)(Math.Max(0.01f, Balance.RecitalEchoesRechargeHours) * 3600f);
        }

        /// <summary>
        /// Folds accrued regen into the stored banked count and advances the clock by only the
        /// whole recharge-intervals actually consumed - not to nowUnix - so a fractional remainder
        /// toward the next tick survives rather than resetting on every settle. Once the bank
        /// reaches the cap the clock snaps to nowUnix instead, since further elapsed time produces
        /// nothing worth preserving while full - the same "excess time is simply not banked" rule
        /// IdleReport.Capped already applies to idle income. Never moves the clock backward: if
        /// elapsed is zero or negative (a wound-back device clock), this is a no-op.
        /// </summary>
        private void SettleRecitalEchoes(long nowUnix)
        {
            int max = Math.Max(0, Balance.RecitalEchoesMax);
            long rechargeSeconds = RecitalRechargeSeconds();
            long elapsed = nowUnix - Profile.RecitalEchoesClockUnix;
            if (elapsed <= 0 || rechargeSeconds <= 0) return;

            long gained = elapsed / rechargeSeconds;
            if (gained <= 0) return;

            long banked = Profile.RecitalEchoesBanked + gained;
            if (banked >= max)
            {
                Profile.RecitalEchoesBanked = max;
                Profile.RecitalEchoesClockUnix = nowUnix;
            }
            else
            {
                Profile.RecitalEchoesBanked = (int)banked;
                Profile.RecitalEchoesClockUnix += gained * rechargeSeconds;
            }
        }

        /// <summary>
        /// Spends one Echo for an attempt, settling regen first. Returns false - spending nothing -
        /// when the bank is still empty after settling. The one gate on whether an attempt can
        /// start at all; GrantRecitalResult below never checks Echoes, because by the time it runs
        /// one has already been spent here, whether the fight that follows is a win or a loss.
        /// </summary>
        public bool TryStartRecitalAttempt(long nowUnix)
        {
            SettleRecitalEchoes(nowUnix);
            if (Profile.RecitalEchoesBanked <= 0) return false;
            Profile.RecitalEchoesBanked--;
            return true;
        }

        // ------------------------------------------------------------------ The Proving (PvP)
        // The whole block below is the Echo bank's shape, field for field and method for method.
        // That is DESIGN-PVP section 5's decision, not laziness: two attempt-gated modes with two
        // different regen idioms is two things to reason about, and the Echo pattern is the one
        // already proven against a wound-back clock and a fractional remainder.

        public ProvingBrandsStatus PeekProvingBrands(long nowUnix)
        {
            ProvingBrandsStatus status = new ProvingBrandsStatus();
            status.Max = Math.Max(0, Balance.ProvingBrandsMax);
            long rechargeSeconds = ProvingRechargeSeconds();
            long elapsed = nowUnix - Profile.ProvingBrandsClockUnix;
            if (elapsed < 0) elapsed = 0;
            long gained = rechargeSeconds > 0 ? elapsed / rechargeSeconds : 0;
            status.Banked = (int)Math.Min(status.Max, Profile.ProvingBrandsBanked + gained);

            if (status.Banked >= status.Max || rechargeSeconds <= 0) status.SecondsToNext = 0;
            else status.SecondsToNext = rechargeSeconds - (elapsed % rechargeSeconds);
            return status;
        }

        private long ProvingRechargeSeconds()
        {
            return (long)(Math.Max(0.01f, Balance.ProvingBrandsRechargeHours) * 3600f);
        }

        /// <summary>SettleRecitalEchoes for Brands. Same fractional-remainder preservation, same
        /// snap-to-now at the cap, same no-op on a wound-back clock.</summary>
        private void SettleProvingBrands(long nowUnix)
        {
            int max = Math.Max(0, Balance.ProvingBrandsMax);
            long rechargeSeconds = ProvingRechargeSeconds();
            long elapsed = nowUnix - Profile.ProvingBrandsClockUnix;
            if (elapsed <= 0 || rechargeSeconds <= 0) return;

            long gained = elapsed / rechargeSeconds;
            if (gained <= 0) return;

            long banked = Profile.ProvingBrandsBanked + gained;
            if (banked >= max)
            {
                Profile.ProvingBrandsBanked = max;
                Profile.ProvingBrandsClockUnix = nowUnix;
            }
            else
            {
                Profile.ProvingBrandsBanked = (int)banked;
                Profile.ProvingBrandsClockUnix += gained * rechargeSeconds;
            }
        }

        /// <summary>
        /// Spends one Brand, settling regen first. Returns false - spending nothing - when the bank
        /// is still empty after settling.
        ///
        /// Spent on ENTRY, never on outcome. GrantProvingResult below does not look at Brands at
        /// all, because by the time it runs one is already gone whether the fight was won or lost.
        /// Charging on the result instead would make the resource a tax on failure and would
        /// quietly reward abandoning a fight that is going badly.
        /// </summary>
        public bool TryStartProvingAttempt(long nowUnix)
        {
            SettleProvingBrands(nowUnix);
            if (Profile.ProvingBrandsBanked <= 0) return false;
            Profile.ProvingBrandsBanked--;
            return true;
        }

        /// <summary>
        /// The opponent currently on offer, generating and persisting one if there is none.
        ///
        /// Persisted rather than regenerated per view so the line a player is looking at does not
        /// change under them between opening the screen and pressing fight. Cleared by
        /// GrantProvingResult, so the next attempt draws a fresh one.
        /// </summary>
        public BattleLoadout CurrentProvingOpponent(long nowUnix)
        {
            if (Profile.ProvingOpponent != null) return Profile.ProvingOpponent;

            if (ProvingGenerator.ShouldUseGhost(Balance, Profile))
            {
                Profile.ProvingOpponent = ProvingGenerator.GhostOf(Content, Profile, "Your Own Line");
            }
            else
            {
                // Indexed by ATTEMPTS, which always advances. Indexing by highest standing meant a
                // stalled player - standing floored at 0, highest static - drew the same seed and
                // therefore the same opponent on every single attempt.
                int seed = ProvingGenerator.SeedFor(Profile.ProvingStanding, Profile.ProvingAttempts);
                Profile.ProvingOpponent = ProvingGenerator.Generate(Content, Balance,
                                                                    Profile.ProvingStanding,
                                                                    ProvingTargetPower(), seed);
            }
            return Profile.ProvingOpponent;
        }

        /// <summary>
        /// The power a generated opponent aims at: the player's own line, scaled up with standing.
        /// Reading the player's real line rather than a table is what keeps the mode tracking where
        /// they actually are; the standing term is what makes climbing cost something.
        /// </summary>
        public int ProvingTargetPower()
        {
            int own = 0;
            List<HeroInstance> team = Profile.TeamHeroes();
            for (int i = 0; i < team.Count; i++)
            {
                HeroDef def = Content.GetHero(team[i].DefId);
                if (def == null) continue;
                own += StatMath.PowerScore(StatMath.ForHero(def, team[i].Level));
            }
            if (own <= 0) own = 1;
            float standingScale = 1f + 0.01f * Math.Max(0, Profile.ProvingStanding);
            return (int)(own * standingScale);
        }

        /// <summary>
        /// Resolves one Proving attempt. Does NOT touch Brands - TryStartProvingAttempt already
        /// spent one before the fight ran, regardless of what this reports.
        ///
        /// A loss pays ProvingLossRewardFraction of the win amount rather than nothing. Recital
        /// pays zero on a loss and that is right for a ladder, where losing means not advancing;
        /// this mode is a repeatable sink for a regenerating resource, and a spent Brand that
        /// bought literally nothing reads as a bug rather than a result.
        /// </summary>
        public ProvingRewards GrantProvingResult(bool victory, long nowUnix)
        {
            ProvingRewards rewards = new ProvingRewards();
            rewards.Victory = victory;
            rewards.Standing = Profile.ProvingStanding;

            // Counted for every attempt, won or lost, before anything else moves - it drives both
            // the ghost bootstrap and the opponent seed, and both need it to advance unconditionally.
            Profile.ProvingAttempts++;

            int valor = Balance.ProvingRewardValorBase
                      + Balance.ProvingRewardValorPerStanding * Math.Max(0, Profile.ProvingStanding);

            if (!victory) valor = (int)(valor * Math.Max(0f, Balance.ProvingLossRewardFraction));

            rewards.Valor = valor;
            Profile.Valor += valor;

            if (victory)
            {
                Profile.TotalBattlesWon++;
                Profile.ProvingStanding += Math.Max(0, Balance.ProvingStandingPerWin);
                if (Profile.ProvingStanding > Profile.ProvingHighestStanding)
                {
                    Profile.ProvingHighestStanding = Profile.ProvingStanding;
                    rewards.NewBest = true;
                }
            }
            else
            {
                // Floors at zero. A standing that could go negative would be a punishment with no
                // floor, and it is also the difficulty dial - driving it below zero would ask the
                // generator for an opponent weaker than the weakest it can build.
                Profile.ProvingStanding = Math.Max(0, Profile.ProvingStanding - Math.Max(0, Balance.ProvingStandingPerLoss));
            }

            rewards.NewStanding = Profile.ProvingStanding;

            // The line just fought is spent either way, so the next attempt draws a fresh one.
            Profile.ProvingOpponent = null;
            return rewards;
        }

        /// <summary>
        /// Resolves the outcome of one floor attempt: applies floor rewards and advances
        /// RecitalCurrentFloor/RecitalHighestFloor on a win, grants nothing on a loss. Does NOT
        /// touch Echoes - TryStartRecitalAttempt already spent one before the fight ran, regardless
        /// of what this call reports.
        /// </summary>
        public RecitalRewards GrantRecitalResult(int floor, bool victory, long nowUnix)
        {
            RecitalRewards rewards = new RecitalRewards();
            rewards.Victory = victory;
            rewards.Floor = floor;
            if (!victory) return rewards;

            Profile.TotalBattlesWon++;

            rewards.Gold = Balance.RecitalRewardGoldBase + Balance.RecitalRewardGoldPerFloor * floor;
            rewards.Shards = Balance.RecitalRewardShardsBase + Balance.RecitalRewardShardsPerFloor * floor;
            rewards.IsDeepMilestone = RecitalGenerator.IsDeepMilestone(Balance, floor);
            rewards.IsMilestone = RecitalGenerator.IsMilestone(Balance, floor) && !rewards.IsDeepMilestone;
            rewards.IsGuardian = RecitalGenerator.IsGuardian(Balance, floor);

            if (rewards.IsDeepMilestone)
            {
                string grantedId = GrantRandomUnownedRelic(floor);
                if (!string.IsNullOrEmpty(grantedId)) rewards.GrantedRelicId = grantedId;
                else rewards.RelicDust = Balance.RecitalDeepMilestoneDustFallback;
            }
            else if (rewards.IsMilestone)
            {
                int tier = Balance.RecitalMilestoneInterval > 0 ? floor / Balance.RecitalMilestoneInterval : 0;
                rewards.RelicDust = Balance.RecitalMilestoneDustPerTier * Math.Max(1, tier);
            }

            // Coda: larger payout on Guardian floors, same isolated-purse pattern as Valor.
            if (rewards.IsGuardian)
                rewards.Coda = Balance.RecitalCodaGuardianBase + Balance.RecitalCodaGuardianPerFloor * floor;
            else
                rewards.Coda = Balance.RecitalCodaBase + Balance.RecitalCodaPerFloor * floor;

            Profile.Gold += rewards.Gold;
            Profile.Shards += rewards.Shards;
            Profile.RelicDust += rewards.RelicDust;
            Profile.Coda += rewards.Coda;

            if (floor >= Profile.RecitalCurrentFloor)
            {
                rewards.AdvancedFloor = true;
                if (floor > Profile.RecitalHighestFloor) Profile.RecitalHighestFloor = floor;
                Profile.RecitalCurrentFloor = Profile.RecitalHighestFloor + 1;
            }

            Profile.LastSaveUnix = nowUnix;
            return rewards;
        }

        /// <summary>"Something rarer" at a deep milestone, per spec - a guaranteed relic rather
        /// than a bigger pile of an existing currency. Falls back to a Dust lump so the reward is
        /// never a silent no-op once every relic in content is owned. Chosen by floor number alone
        /// so which relic a given deep milestone grants is reproducible, not tied to unrelated
        /// profile state that could reorder it.</summary>
        private string GrantRandomUnownedRelic(int floor)
        {
            List<RelicDef> unowned = new List<RelicDef>();
            for (int i = 0; i < Content.Relics.Count; i++)
                if (!Profile.OwnsRelic(Content.Relics[i].Id)) unowned.Add(Content.Relics[i]);
            if (unowned.Count == 0) return "";

            int index = ((floor % unowned.Count) + unowned.Count) % unowned.Count;
            RelicDef chosen = unowned[index];
            GrantRelic(chosen.Id);
            return chosen.Id;
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

            StageDef idleStage = Profile.StagesCleared > 0
                ? Content.GetStageByGlobalIndex(Profile.StagesCleared - 1) : null;
            report.Gold = (int)Math.Floor(StatMath.IdleGoldPerSecond(Balance, idleStage) * elapsed);
            report.Shards = (int)Math.Floor(StatMath.IdleShardsPerSecond(Balance, idleStage) * elapsed);
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
            get
            {
                StageDef s = Profile.StagesCleared > 0
                    ? Content.GetStageByGlobalIndex(Profile.StagesCleared - 1) : null;
                return StatMath.IdleGoldPerSecond(Balance, s) * 3600f;
            }
        }

        public float IdleShardsPerHour
        {
            get
            {
                StageDef s = Profile.StagesCleared > 0
                    ? Content.GetStageByGlobalIndex(Profile.StagesCleared - 1) : null;
                return StatMath.IdleShardsPerSecond(Balance, s) * 3600f;
            }
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
            // first-summon guarantee: pulls 1-3 draw only from heroes not already owned, so the
            // opening pulls can't duplicate a starter. Falls back to normal weights if every hero
            // is already owned (can't happen with the current roster, but content can grow).
            HeroDef pulled = Profile.TotalSummons <= PityGuaranteedSummons ? RollUnownedHero(rng) : null;
            if (pulled == null) pulled = RollHero(rng);
            if (pulled == null)
            {
                Profile.Shards += Balance.SummonCost; // refund, content is broken
                outcome.Message = "The rift is empty - no hero content loaded.";
                return outcome;
            }

            outcome.Success = true;
            outcome.Hero = pulled;
            RecordEventActivity(EventActivityKind.SummonPerformed, 1, nowUnix);

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

        public const int PityGuaranteedSummons = 3;

        private HeroDef RollUnownedHero(DeterministicRng rng)
        {
            List<HeroDef> pool = new List<HeroDef>();
            for (int i = 0; i < Content.Heroes.Count; i++)
                if (!Profile.Owns(Content.Heroes[i].Id)) pool.Add(Content.Heroes[i]);
            if (pool.Count == 0) return null;
            return pool[rng.Range(0, pool.Count)];
        }

        private HeroDef RollHero(DeterministicRng rng)
        {
            float[] weights = {
                Balance.SummonWeight1,
                Balance.SummonWeight2,
                Balance.SummonWeight3,
                Balance.SummonWeight4,
                Balance.SummonWeight5,
            };

            // zero out star tiers that have no heroes in the pool
            for (int s = 1; s <= 5; s++)
                if (Content.HeroesOfStars(s).Count == 0) weights[s - 1] = 0f;

            float total = 0f;
            for (int i = 0; i < weights.Length; i++) total += weights[i];
            if (total <= 0f)
            {
                if (Content.Heroes.Count == 0) return null;
                return Content.Heroes[rng.Range(0, Content.Heroes.Count)];
            }

            float roll = rng.NextFloat() * total;
            int stars = 5;
            float acc = 0f;
            for (int i = 0; i < 5; i++)
            {
                acc += weights[i];
                if (roll < acc) { stars = i + 1; break; }
            }

            List<HeroDef> pool = Content.HeroesOfStars(stars);
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
