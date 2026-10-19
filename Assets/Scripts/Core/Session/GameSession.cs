using System;
using System.Collections.Generic;
using Emberfall.Core.Cloud;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Telemetry;

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

    /// <summary>What one ascension did (GameSession.Ascend).</summary>
    public sealed class AscensionResult
    {
        public bool Success;
        public string Message = "";
        public int FromTier;
        public int ToTier;
        public int CopiesSpent;
        /// <summary>The level the hero stood at before the reset to 1 (F6).</summary>
        public int LevelBefore;
    }

    /// <summary>
    /// What one ascension WOULD do, read without doing it (GameSession.PreviewAscension).
    ///
    /// It exists so a screen can say what the step costs and what it moves BEFORE the player
    /// commits, and so it can say it without owning a second copy of the arithmetic: Ascend and
    /// this share AscensionLanding, so the level the preview promises is the level the ascension
    /// lands on by construction rather than by two functions agreeing.
    ///
    /// TierLevelAfter is the figure the warning is about. A tier level resets to 1 (F6) while the
    /// Level it stands at goes UP, and a page that showed only one of those two would either
    /// hide the reset or invent a loss that does not happen.
    /// </summary>
    public sealed class AscensionPreview
    {
        /// <summary>True when the step can be taken right now. False carries Blocker.</summary>
        public bool Possible;
        /// <summary>Why not, or empty. GameSession.AscensionBlocker's own sentence.</summary>
        public string Blocker = "";
        /// <summary>False when this hero has no next tier at all - a hero below five stars, or
        /// one already at the top. There is nothing to preview, and the numbers below repeat the
        /// present rather than pretending at a future.</summary>
        public bool HasNextTier;
        public int FromTier;
        public int ToTier;
        /// <summary>Copies beyond the hero's own body the step spends, and how many it has to
        /// spare. HeroInstance.Copies counts the hero itself, so the spare is Copies - 1.</summary>
        public int CopiesNeeded;
        public int CopiesSpare;
        public int LevelBefore;
        public int LevelAfter;
        public int TierLevelBefore;
        public int TierLevelAfter;
        /// <summary>The cap over TierLevelBefore/After, counted IN TIER LEVELS
        /// (StatMath.HeroLevelCap) rather than as a Level (StatMath.CapLevel).
        ///
        /// It is that unit and not the other because the page it feeds counts in nothing else: the
        /// hero list beside the card reads "TIER LEVEL 9" and "TIER CAP 9" from TierLevelOf and
        /// HeroLevelCap, so a card that answered the same question in Levels would print a cap of
        /// 45 next to a list that had just said 9 - two true numbers that cannot both be the same
        /// cap. A screen showing one hero in two units is a screen the player has to be told how
        /// to read.</summary>
        public int TierCapBefore;
        public int TierCapAfter;
    }

    /// <summary>What one conversion did (GameSession.Convert).</summary>
    public sealed class ConversionResult
    {
        public bool Success;
        public string Message = "";
        public Aspect Aspect;
        public HeroDef Hero;
        public HeroInstance Instance;
        public bool IsNew;
        public int PointsSpent;
        public int BodiesSpent;
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

        /// <summary>The wall or finale chest this first clear opened, already paid into the profile,
        /// or null when the stage carries none or this was not a first clear. See WallChests.</summary>
        public WallChestView Chest;
    }

    /// <summary>
    /// What one won ladder battle did, from GameSession.RecordLadderWin: whether it finished its
    /// wave or the stage, what a finished wave's drip share paid, and the stage clear's payout.
    /// </summary>
    public sealed class LadderWin
    {
        /// <summary>True when the win finished its wave (a stage clear counts).</summary>
        public bool WaveCleared;
        /// <summary>True when the win finished the stage; Rewards is then set.</summary>
        public bool StageCleared;
        /// <summary>The drip share a finished mid-stage wave paid. Zero otherwise.</summary>
        public int Gold;
        public int AccountXp;
        public int PlayerLevelsGained;
        /// <summary>The stage clear's payout, net of the drip its waves already paid. Null unless
        /// StageCleared.</summary>
        public BattleRewards Rewards;
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

        /// <summary>
        /// Tells the daily loop that something it counts just happened. The one door every rule in
        /// this class uses, and a no-op unless DailyLoopSwitch is on, so with the switch off the
        /// profile is never touched.
        /// </summary>
        private void OrderEvent(OrderKind kind, int amount, long nowUnix)
        {
            if (!DailyLoopSwitch.Enabled) return;
            DailyLoop.Record(this, kind, amount, nowUnix);
        }

        // ------------------------------------------------------------------ hero levelling

        /// <summary>
        /// The Level this hero stops at: its star tier and its ascension tier decide it, and
        /// account rank does not (F7). A 1-star and a 5-star never share one. A Level, not a tier
        /// level - StatMath.HeroLevelCap is the same cap counted in tier levels.
        /// </summary>
        public int HeroLevelCapOf(HeroInstance hero)
        {
            if (hero == null) return 1;
            return StatMath.CapLevel(Balance, StarsOf(hero), hero.Ascension);
        }

        private static int StarsOf(HeroInstance hero)
        {
            return hero.Def != null ? hero.Def.Stars : 5;
        }

        /// <summary>The level inside this hero's ascension tier: 1 on the day it ascends (F6), and
        /// equal to Level for every hero at tier 0 and every hero below five stars.</summary>
        public int TierLevelOf(HeroInstance hero)
        {
            if (hero == null) return 1;
            if (StarsOf(hero) < 5) return hero.Level;
            return StatMath.TierLevelOf(Balance, hero.Ascension, hero.Level);
        }

        /// <summary>The Level one more tier level puts this hero at: the next step of its tier's
        /// stride (StatMath.LevelAt).</summary>
        public int NextLevelOf(HeroInstance hero)
        {
            if (hero == null) return 1;
            if (StarsOf(hero) < 5) return hero.Level + 1;
            return StatMath.LevelAt(Balance, hero.Ascension, TierLevelOf(hero) + 1);
        }

        /// <summary>The next level's price, read off the tier level: each ascension reopens the
        /// gold sink at the level-1 price, times the tier's curve multiplier, which is also how many
        /// Levels that tier level buys - so a coin buys the same stride of the ladder at every tier.</summary>
        public int LevelUpCost(HeroInstance hero)
        {
            return StatMath.LevelUpCost(Balance, TierLevelOf(hero), hero.Ascension);
        }

        public bool CanLevelUp(HeroInstance hero)
        {
            if (hero == null) return false;
            if (hero.Level >= HeroLevelCapOf(hero)) return false;
            return Profile.Gold >= LevelUpCost(hero);
        }

        public LevelUpResult LevelUpHero(HeroInstance hero, int times)
        {
            return LevelUpHero(hero, times, TimeUtil.NowUnix());
        }

        /// <summary>The level-up with the clock handed in, so the daily orders it counts toward
        /// are dated by the caller's clock like every other rule here. The two-argument form reads
        /// the device clock for it.</summary>
        public LevelUpResult LevelUpHero(HeroInstance hero, int times, long nowUnix)
        {
            LevelUpResult result = new LevelUpResult();
            if (hero == null) { result.Message = "No hero selected."; return result; }
            result.NewLevel = hero.Level;

            int cap = HeroLevelCapOf(hero);
            if (hero.Level >= cap)
            {
                result.Message = hero.Def != null && hero.Def.Stars >= 5
                    ? (hero.Ascension < StatMath.MaxAscensionTier(Balance)
                        ? "Level cap reached. Ascend to raise it."
                        : "Level cap reached at the highest ascension.")
                    : "Level cap reached. A hero below five stars stops here.";
                return result;
            }

            for (int i = 0; i < times; i++)
            {
                if (hero.Level >= cap) break;
                int cost = LevelUpCost(hero);
                if (Profile.Gold < cost) break;
                Profile.Gold -= cost;
                hero.Level = Math.Min(cap, NextLevelOf(hero));
                result.GoldSpent += cost;
                result.LevelsGained++;
            }

            result.NewLevel = hero.Level;
            result.Success = result.LevelsGained > 0;
            if (result.Success) TelemetryHooks.HeroLevelUp(result.LevelsGained);
            if (result.Success) OrderEvent(OrderKind.Level, result.LevelsGained, nowUnix);
            if (!result.Success) result.Message = "Not enough gold.";
            else if (hero.Level >= cap) result.Message = "Level cap reached.";
            return result;
        }

        // ------------------------------------------------------------------ ascension

        /// <summary>
        /// Why this hero cannot ascend right now, or null when it can. Only a 5-star ascends (F2),
        /// only up to the highest tier balance.json authors, and only by spending copies of ITSELF
        /// (F5): Copies counts the hero's own body, so a step costing n needs n + 1.
        /// </summary>
        public string AscensionBlocker(HeroInstance hero)
        {
            if (hero == null || hero.Def == null) return "No hero selected.";
            if (hero.Def.Stars < 5) return "Only a five-star hero ascends.";
            int need = StatMath.AscensionCopiesFor(Balance, hero.Ascension);
            if (need < 0) return "This hero is at the highest ascension.";
            if (hero.Copies - 1 < need)
                return "Needs " + need + " more cop" + (need == 1 ? "y" : "ies") + " of this hero.";
            return null;
        }

        /// <summary>
        /// One ascension: spends the copies, raises the tier and resets the tier level to 1 on the
        /// higher curve (F6), which is one Level past the old tier's cap (StatMath.LevelAt). Gold
        /// spent on the old tier's levels is not refunded - the new tier's level 1 already
        /// out-powers the old tier at its cap, which is what makes the reset a promotion rather than
        /// a loss.
        ///
        /// Level never goes down, and that is the one case the reset needs a rule for: a hero can
        /// stand above its tier's cap. A save no longer carries one (SaveSerializer clamps every
        /// level to its cap on load, since version 18), but the debug cheat panel still sets one.
        /// It lands on the first
        /// step of the new tier above where it stood, or stays where it is if that is past the new
        /// tier's cap too, so an ascension is a promotion for it as well.
        /// </summary>
        public AscensionResult Ascend(HeroInstance hero)
        {
            AscensionResult result = new AscensionResult();
            string blocker = AscensionBlocker(hero);
            if (blocker != null) { result.Message = blocker; return result; }

            int cost = StatMath.AscensionCopiesFor(Balance, hero.Ascension);
            result.FromTier = hero.Ascension;
            result.LevelBefore = TierLevelOf(hero);
            result.CopiesSpent = cost;
            int landing = AscensionLanding(hero, hero.Ascension + 1);
            hero.Copies -= cost;
            hero.Ascension++;
            hero.Level = landing;
            result.ToTier = hero.Ascension;
            result.Success = true;
            result.Message = "Ascended to tier " + hero.Ascension + ".";
            return result;
        }

        /// <summary>
        /// The Level this hero would stand at once it reached `toTier`: the first step of that
        /// tier, and the rule for the one hero that is already past it.
        ///
        /// Extracted from Ascend rather than copied out of it, because PreviewAscension has to
        /// promise this number before the step is taken and a second copy of it is a second
        /// answer waiting to disagree. Reads the hero and writes nothing.
        /// </summary>
        private int AscensionLanding(HeroInstance hero, int toTier)
        {
            int before = hero.Level;
            int landing = StatMath.TierBaseLevel(Balance, toTier);
            if (landing <= before)
            {
                int tierLevel = StatMath.TierLevelOf(Balance, toTier, before) + 1;
                landing = Math.Max(before, Math.Min(StatMath.CapLevel(Balance, StarsOf(hero), toTier),
                                                    StatMath.LevelAt(Balance, toTier, tierLevel)));
            }
            return landing;
        }

        /// <summary>
        /// What ascending this hero would cost and what it would move, WITHOUT moving anything.
        ///
        /// The fusion screen's whole job on the ascension side is to say this before the player
        /// spends, and the tier level is why: it goes back to 1 (F6) while the Level it stands at
        /// and the cap over it both rise. A page that announced only the reset would be reporting
        /// a loss the game does not inflict, and one that announced only the rise would be hiding
        /// the number the player is about to watch change.
        ///
        /// TWO UNITS LIVE HERE AND THE FUSION SCREEN DRAWS ONE OF THEM, and which is which is not
        /// a detail a caller can be left to work out. TierLevelBefore/After and TierCapBefore/After
        /// are a matched pair in tier levels - the same pair GameSession.TierLevelOf and
        /// StatMath.HeroLevelCap give the hero list - and they are all the fusion screen reads.
        /// LevelBefore/After are Levels on the one stat ladder, for a caller that is not that
        /// screen. A tier-0 five-star at the top of its tier previews as tier level 9 -> 1 of a cap
        /// 9 -> 18, standing at Level 9 -> 10; the screen printed that Level beside the cap and it
        /// read as "10 of 18", so it no longer does. The absolute Level cap over that hero
        /// (9 -> 45, StatMath.CapLevel) is a true number this deliberately does not carry.
        ///
        /// Nothing here decides anything. AscensionBlocker is still the rule, AscensionLanding is
        /// still the arithmetic, and this reads both.
        /// </summary>
        public AscensionPreview PreviewAscension(HeroInstance hero)
        {
            AscensionPreview preview = new AscensionPreview();
            if (hero == null || hero.Def == null) { preview.Blocker = "No hero selected."; return preview; }

            preview.FromTier = hero.Ascension;
            preview.ToTier = hero.Ascension;
            preview.LevelBefore = hero.Level;
            preview.LevelAfter = hero.Level;
            preview.TierLevelBefore = TierLevelOf(hero);
            preview.TierLevelAfter = preview.TierLevelBefore;
            preview.TierCapBefore = StatMath.HeroLevelCap(Balance, StarsOf(hero), hero.Ascension);
            preview.TierCapAfter = preview.TierCapBefore;
            preview.CopiesSpare = hero.Copies - 1;

            string blocker = AscensionBlocker(hero);
            preview.Blocker = blocker != null ? blocker : "";
            preview.Possible = blocker == null;

            int need = StatMath.AscensionCopiesFor(Balance, hero.Ascension);
            if (hero.Def.Stars < 5 || need < 0) return preview;

            preview.HasNextTier = true;
            preview.CopiesNeeded = need;
            preview.ToTier = hero.Ascension + 1;
            preview.LevelAfter = AscensionLanding(hero, preview.ToTier);
            preview.TierLevelAfter = StatMath.TierLevelOf(Balance, preview.ToTier, preview.LevelAfter);
            preview.TierCapAfter = StatMath.HeroLevelCap(Balance, StarsOf(hero), preview.ToTier);
            return preview;
        }

        // ------------------------------------------------------------------ conversion

        /// <summary>What one body of this hero is worth in a conversion, or 0 for a 5-star, which
        /// is never fodder: above five stars only copies of the same hero count (F5).</summary>
        public int FodderPoints(HeroDef def)
        {
            if (def == null || def.Stars >= 5 || def.Stars < 1) return 0;
            int[] points = Balance.ConversionFodderPoints;
            if (points == null || points.Length < def.Stars) return 0;
            return points[def.Stars - 1];
        }

        /// <summary>True for an aspect whose conversion takes one seed of that aspect and fodder of
        /// any aspect (F9), false for one whose fodder must all be of that aspect (F4).</summary>
        public bool IsSeedAspect(Aspect aspect)
        {
            string[] seeds = Balance.ConversionSeedAspects;
            if (seeds == null) return false;
            string name = aspect.ToString();
            for (int i = 0; i < seeds.Length; i++) if (seeds[i] == name) return true;
            return false;
        }

        /// <summary>The 5-stars a conversion into this aspect can produce: every fusable 5-star of
        /// it. The roll over them is uniform and the player never chooses (F3).</summary>
        public List<HeroDef> ConversionPool(Aspect aspect)
        {
            List<HeroDef> pool = new List<HeroDef>();
            for (int i = 0; i < Content.Heroes.Count; i++)
            {
                HeroDef h = Content.Heroes[i];
                if (h.Stars == 5 && h.Aspect == aspect && h.Fusable) pool.Add(h);
            }
            return pool;
        }

        /// <summary>
        /// How many heroes below five stars this aspect authors AT ALL - the fodder pool the
        /// roster gives it, not the bodies the player holds.
        ///
        /// It is the figure the seed rule is made of. F9: Umbra and Lumen author four low-star
        /// heroes each against fourteen for the other four aspects, which is why their recipe
        /// takes one seed of the aspect and generic fodder for the rest. A screen that states the
        /// rule without this number is asking the player to take the rule on faith.
        /// </summary>
        public int ConversionFodderRoster(Aspect aspect)
        {
            int n = 0;
            for (int i = 0; i < Content.Heroes.Count; i++)
            {
                HeroDef h = Content.Heroes[i];
                if (h.Aspect == aspect && h.Stars >= 1 && h.Stars < 5) n++;
            }
            return n;
        }

        /// <summary>
        /// Why these bodies cannot be converted into this aspect, or null when they can. `bodies`
        /// names one entry per body, so a hero listed twice gives two of its copies. The rules:
        /// every body is below five stars (F3); no hero gives more bodies than it has, and a
        /// deployed hero keeps the one on the team; the bodies are worth
        /// ConversionPointsRequired; and either every body is of the target aspect (F4) or, for a
        /// seed aspect, at least one is (F9).
        /// </summary>
        public string ConversionBlocker(Aspect target, List<HeroInstance> bodies)
        {
            if (bodies == null || bodies.Count == 0) return "Choose heroes to convert.";
            if (ConversionPool(target).Count == 0) return "No five-star of that aspect can be converted into.";

            Dictionary<HeroInstance, int> taken = new Dictionary<HeroInstance, int>();
            int points = 0;
            bool seeded = false, allSame = true;
            for (int i = 0; i < bodies.Count; i++)
            {
                HeroInstance b = bodies[i];
                if (b == null || b.Def == null || !Profile.Heroes.Contains(b)) return "A chosen hero is not in the collection.";
                if (b.Def.Stars >= 5) return "A five-star hero is never fodder.";
                int n;
                taken.TryGetValue(b, out n);
                taken[b] = n + 1;
                points += FodderPoints(b.Def);
                if (b.Def.Aspect == target) seeded = true;
                else allSame = false;
            }
            foreach (KeyValuePair<HeroInstance, int> kv in taken)
            {
                int spare = kv.Key.Copies - (Profile.IsDeployed(kv.Key.Uid) ? 1 : 0);
                if (kv.Value > spare)
                    return kv.Key.DisplayName + " cannot give " + kv.Value + " (has " + spare + " to spare).";
            }
            if (IsSeedAspect(target))
            {
                if (!seeded) return Article(target, true) + " " + target + " conversion needs one " + target + " hero as its seed.";
            }
            else if (!allSame) return "Every hero in " + Article(target, false) + " " + target + " conversion must be " + target + ".";
            if (points < Balance.ConversionPointsRequired)
                return "Worth " + points + " of the " + Balance.ConversionPointsRequired + " a conversion needs.";
            return null;
        }

        private static string Article(Aspect aspect, bool capital)
        {
            string name = aspect.ToString();
            bool vowel = name.Length > 0 && "AEIOU".IndexOf(name[0]) >= 0;
            return capital ? (vowel ? "An" : "A") : (vowel ? "an" : "a");
        }

        /// <summary>
        /// One conversion: spends the bodies and grants one 5-star of the target aspect, rolled
        /// uniformly from ConversionPool with the given seed (F3 - random, never chosen, because the
        /// randomness is what manufactures the duplicate 5-stars ascension eats). A hero whose last
        /// body is spent leaves the collection. A 5-star already owned gains a copy and nothing
        /// else: no duplicate refund, because that copy is what ascension spends.
        /// </summary>
        public ConversionResult Convert(Aspect target, List<HeroInstance> bodies, int seed, long nowUnix)
        {
            ConversionResult result = new ConversionResult();
            result.Aspect = target;
            string blocker = ConversionBlocker(target, bodies);
            if (blocker != null) { result.Message = blocker; return result; }

            for (int i = 0; i < bodies.Count; i++)
            {
                HeroInstance b = bodies[i];
                result.PointsSpent += FodderPoints(b.Def);
                b.Copies--;
                if (b.Copies <= 0) Profile.RemoveHero(b);
            }
            result.BodiesSpent = bodies.Count;

            List<HeroDef> pool = ConversionPool(target);
            DeterministicRng rng = new DeterministicRng(seed);
            HeroDef rolled = pool[rng.Range(0, pool.Count)];
            result.Hero = rolled;

            HeroInstance existing = Profile.FindHeroByDefId(rolled.Id);
            if (existing == null)
            {
                result.IsNew = true;
                result.Instance = AddHero(Profile, rolled, nowUnix);
                Profile.NormaliseTeam();
            }
            else
            {
                existing.Copies++;
                result.Instance = existing;
            }
            result.Success = true;
            result.Message = result.IsNew ? "A new five-star answers the conversion." : "A copy of a five-star you command.";
            return result;
        }

        // ------------------------------------------------------------------ campaign

        public bool IsStageUnlocked(StageDef stage)
        {
            if (stage == null) return false;
            // A cleared stage stays open wherever content later puts it; the only open stage that
            // is not cleared is the frontier, the first uncleared one in campaign order.
            if (Profile.HasClearedStage(stage.Id)) return true;
            StageDef first = FirstUnclearedStage;
            return first != null && stage.Id == first.Id;
        }

        /// <summary>The first stage in campaign order the player has not cleared, or null when
        /// every stage is cleared. Derived from the cleared ids, never from a stored position.</summary>
        public StageDef FirstUnclearedStage
        {
            get { return CampaignProgress.FirstUncleared(Profile, Content); }
        }

        /// <summary>The cleared stage furthest along the campaign, or null before the first clear.
        /// What idle income and the profile's furthest-stage line are paid and named from.</summary>
        public StageDef FurthestClearedStage
        {
            get { return CampaignProgress.FurthestCleared(Profile, Content); }
        }

        public StageDef NextStage
        {
            get
            {
                StageDef stage = FirstUnclearedStage;
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
        /// This does NOT pay out; it is the cursor half of RecordLadderWin, which is what a
        /// campaign win calls. Nothing is paid per battle - that would multiply every stage's
        /// income by 144 and is exactly what DESIGN-CAMPAIGN-STRUCTURE.md rules out - but a
        /// finished wave of a first clear pays its drip share and the stage clear pays the rest
        /// (WaveDripPercent), so a stage's whole payout is unchanged.
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
        /// A lost battle. Changes no state; it only reports the loss to the play-stats tap.
        ///
        /// "Defeat inside a stage: retry the single battle that was lost, not the wave and not the
        /// stage" (DESIGN-CAMPAIGN-STRUCTURE.md, decided 2026-09-07). Leaving the cursor untouched
        /// IS that behaviour - the next attempt reads the same (wave, battle) back out of
        /// ResumePointFor and re-fights it. This method exists so the call site reads as a
        /// deliberate "nothing moves" rather than a missing branch someone should add later.
        /// </summary>
        public void RecordBattleLost(StageDef stage, int wave, int battle)
        {
            TelemetryHooks.WaveLost(this, stage, wave, battle);
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

        // ============================================================ the per-wave drip

        /// <summary>
        /// The share of a stage's purse its waves pay as they fall on a FIRST clear, in percent:
        /// each wave pays an equal share of this much of the stage's gold and Wayfarer XP, and the
        /// stage clear pays what is left, plus the first-clear bonus, the shards, the drop and the
        /// unlock as before. The stage's whole payout is the same integer it always was; each share
        /// is rounded down and the remainder stays on the stage clear. Repeat clears and sweeps do
        /// not drip. Docs/ui-specs/WAVE-RESULTS.md, section E.
        ///
        /// A constant rather than a balance.json key: balance.json is hashed into ContentVersion
        /// and no fight reads this, so a key there would retire every recorded guild dungeon
        /// replay over a number none of them can see, which is why guild-path.json is kept out of
        /// the hash.
        /// </summary>
        public const int WaveDripPercent = 40;

        /// <summary>The gold one wave of this stage pays on a first clear. Zero for a stage of
        /// one wave, whose only wave is its stage clear.</summary>
        public static int WaveDripGold(StageDef stage)
        {
            return stage == null ? 0 : WaveShare(stage.RewardGold, stage);
        }

        /// <summary>The Wayfarer XP one wave of this stage pays on a first clear.</summary>
        public static int WaveDripAccountXp(StageDef stage)
        {
            return stage == null ? 0 : WaveShare(stage.RewardAccountXp, stage);
        }

        /// <summary>An equal share of WaveDripPercent of a purse, one per wave, rounded down.
        /// Integer arithmetic, so a share is the same number on every machine.</summary>
        private static int WaveShare(int purse, StageDef stage)
        {
            int waves = Math.Max(1, stage.WaveCount);
            if (waves <= 1 || purse <= 0) return 0;
            return (int)((long)purse * WaveDripPercent / (100L * waves));
        }

        /// <summary>
        /// How many of this stage's waves have already paid their share: the waves before the one
        /// the cursor stands in. A wave pays at the moment the cursor leaves it and the cursor
        /// never goes back, so this is the count paid, with no field of its own in the save; a
        /// resume reads the same cursor and cannot pay a wave twice. Zero for any stage the cursor
        /// is not in.
        /// </summary>
        public int WavesPaidBefore(StageDef stage)
        {
            if (stage == null || Profile.CampaignCurrentStageId != stage.Id) return 0;
            int waves = Math.Max(1, stage.WaveCount);
            return Math.Min(Math.Max(1, Profile.CampaignCurrentWave), waves) - 1;
        }

        /// <summary>
        /// A won ladder battle, recorded and paid: the one call every campaign win takes, so the
        /// cursor and the purse cannot disagree. Moves the cursor through RecordBattleWon; a win
        /// that finishes a wave of an uncleared stage pays that wave's drip share; a win that
        /// finishes the stage pays the stage clear through GrantBattleResult, net of the shares
        /// its waves already paid. On a cleared stage nothing drips and the clear is the full
        /// repeat purse, exactly as before.
        /// </summary>
        public LadderWin RecordLadderWin(StageDef stage, int wave, int battle, long nowUnix)
        {
            LadderWin win = new LadderWin();
            if (stage == null) return win;

            // All read BEFORE the cursor moves: a stage clear resets it, and the first clear is
            // what adds the id. A wave pays only when the win is in the wave the cursor stands in,
            // so a caller handing in a position the cursor has already left cannot pay it again.
            bool firstClear = !Profile.HasClearedStage(stage.Id);
            int paidWaves = firstClear ? WavesPaidBefore(stage) : 0;
            int cursorWave, cursorBattle;
            ResumePointFor(stage, out cursorWave, out cursorBattle);

            if (RecordBattleWon(stage, wave, battle))
            {
                win.WaveCleared = true;
                win.StageCleared = true;
                TelemetryHooks.StageCleared(this, stage, wave, battle);
                win.Rewards = GrantBattleResult(stage, true, nowUnix,
                                                paidWaves * WaveDripGold(stage),
                                                paidWaves * WaveDripAccountXp(stage));
                OrderEvent(OrderKind.Waves, 1, nowUnix);
                return win;
            }

            int nextWave, nextBattle;
            ResumePointFor(stage, out nextWave, out nextBattle);
            if (nextWave == wave) return win;

            win.WaveCleared = true;
            OrderEvent(OrderKind.Waves, 1, nowUnix);
            if (!firstClear || wave != cursorWave) return win;
            win.Gold = WaveDripGold(stage);
            win.AccountXp = WaveDripAccountXp(stage);
            Profile.Gold += win.Gold;
            win.PlayerLevelsGained = GrantAccountXp(win.AccountXp);
            return win;
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

            BattleResult result = ResolveLadderBattle(
                stage, Math.Max(1, stage.WaveCount), Math.Max(1, stage.BattleCount), seed);
            if (!result.Victory)
            {
                rewards.Victory = false;
                rewards.StageId = stage.Id;
                return rewards;
            }
            BattleRewards swept = GrantBattleResult(stage, true, nowUnix);
            // A sweep stands for the stage's whole ladder, so it counts the waves the stage has:
            // a player with nothing left to fight the long way can still finish a wave order,
            // which is the one place it differs from RecordLadderWin. A sweep past the repeat cap
            // pays nothing, so it counts nothing either: one free tap must not finish two paid
            // orders. (GrantBattleResult holds back the stage win for the same reason.)
            if (!swept.RepeatCapped) OrderEvent(OrderKind.Waves, Math.Max(1, stage.WaveCount), nowUnix);
            return swept;
        }

        /// <summary>
        /// Resolves ONE battle of a stage's ladder: builds it with CampaignWaveGenerator.BuildBattle
        /// and runs it through CombatSimulation. The single path every ladder fight takes, shared
        /// by SweepClearedStage (the hardest battle) and ResolveWave (each battle of a wave), so
        /// there is no second resolution path that could drift from the first. The seed is the
        /// caller's; this adds nothing to it.
        /// </summary>
        public BattleResult ResolveLadderBattle(StageDef stage, int wave, int battle, int seed)
        {
            StageDef resolved = CampaignWaveGenerator.BuildBattle(stage, wave, battle);
            return CombatSimulation.Run(BuildBattleRequest(resolved, seed));
        }

        /// <summary>
        /// The seed for one battle of a wave resolve: the caller's base seed mixed with the
        /// battle's ordinal in the stage, so battles of one wave do not share an RNG stream while
        /// the whole wave stays reproducible from one number. A murmur-style finaliser, not
        /// cryptographic. DeterministicRng takes any non-zero int (it maps 0 to a constant and
        /// reads the rest as a uint), so the result is NOT masked positive; only zero is avoided.
        /// </summary>
        public static int BattleSeed(int baseSeed, int ordinal)
        {
            unchecked
            {
                uint h = (uint)baseSeed ^ ((uint)ordinal * 0x9E3779B9u);
                h ^= h >> 16; h *= 0x85EBCA6Bu; h ^= h >> 13; h *= 0xC2B2AE35u; h ^= h >> 16;
                return h == 0u ? 1 : (int)h;
            }
        }

        /// <summary>
        /// Resolves the rest of the CURRENT wave in one tap.
        ///
        /// WHY ONE TAP: a stage is up to 12 waves of 12 battles, and fighting every battle by
        /// hand is a chore, not a decision. A wave is the unit the player reasons about ("did I
        /// win the wave, am I still winning"), so the resolve runs every remaining battle of that
        /// wave and stops there.
        ///
        /// NOTHING ABOUT THE FIGHTS CHANGES. Each battle still runs through CombatSimulation via
        /// ResolveLadderBattle, the same helper SweepClearedStage uses, so the battle count per
        /// stage is unchanged and the result screen only reads the record this returns.
        ///
        /// EVERY WIN GOES THROUGH RecordLadderWin: a won mid-stage wave of a first clear moves the
        /// cursor and pays its drip share (WaveDripPercent), and the wave that finishes the stage
        /// pays the rest of the stage clear, once. The stage's whole payout is unchanged.
        ///
        /// A LOSS stops the wave at the lost battle and leaves the cursor on it, so the next
        /// resolve retries exactly that battle and never refights the ones already won.
        /// Refusals change nothing. A cleared stage is refused: the sweep is its only replay path.
        /// </summary>
        public WaveResolution ResolveWave(StageDef stage, int baseSeed, long nowUnix)
        {
            WaveResolution res = new WaveResolution();
            if (stage == null) { res.Refused = WaveRefusal.NoStage; return res; }
            res.StageId = stage.Id;
            if (!stage.HasWaveCurve) { res.Refused = WaveRefusal.NoWaveCurve; return res; }
            if (Profile.HasClearedStage(stage.Id)) { res.Refused = WaveRefusal.StageCleared; return res; }
            if (!IsStageUnlocked(stage)) { res.Refused = WaveRefusal.StageLocked; return res; }
            if (Profile.TeamHeroes().Count == 0) { res.Refused = WaveRefusal.NoTeam; return res; }

            int wave, battle;
            ResumePointFor(stage, out wave, out battle);
            TelemetryHooks.StageAttempt(this, stage, wave, battle);
            res.Wave = wave;
            res.WaveCount = Math.Max(1, stage.WaveCount);
            res.BattleCount = Math.Max(1, stage.BattleCount);
            res.FirstBattle = battle;

            // The line that is about to fight, copied before anything can move it. A stage clear
            // pays out below, after the fights, and the player can level a hero the moment they
            // leave the result screen; the replay has to be the line that fought.
            List<HeroInstance> fighting = new List<HeroInstance>();
            List<HeroInstance> live = Profile.TeamHeroes();
            for (int i = 0; i < live.Count; i++) fighting.Add(live[i].Copy());

            BattleResult lastFought = null;
            res.Stats = new WaveStats();
            for (int b = battle; b <= res.BattleCount; b++)
            {
                int ordinal = CampaignWaveGenerator.BattleOrdinal(stage, wave, b);
                int seed = BattleSeed(baseSeed, ordinal);
                BattleResult result = ResolveLadderBattle(stage, wave, b, seed);
                lastFought = result;
                res.Stats.Record(b, result);

                WaveBattleOutcome outcome = new WaveBattleOutcome();
                outcome.Wave = wave;
                outcome.Battle = b;
                outcome.Ordinal = ordinal;
                outcome.Seed = seed;
                outcome.Victory = result.Victory;
                outcome.TimedOut = result.TimedOut;
                outcome.Rounds = result.Rounds;
                outcome.PlayerHpRatio = result.PlayerHpRatio;
                outcome.EnemyHpRatio = result.EnemyHpRatio;
                res.Battles.Add(outcome);

                if (!result.Victory)
                {
                    RecordBattleLost(stage, wave, b);
                    res.LostBattle = b;
                    res.LostResult = result;
                    res.LostDebrief = Debrief.Summarize(result);
                    break;
                }

                LadderWin win = RecordLadderWin(stage, wave, b, nowUnix);
                if (win.StageCleared)
                {
                    res.StageCleared = true;
                    res.WaveCleared = true;
                    res.Rewards = win.Rewards;
                    break;
                }
                if (win.WaveCleared)
                {
                    res.WaveCleared = true;
                    res.WaveGold = win.Gold;
                    res.WaveAccountXp = win.AccountXp;
                    res.WavePlayerLevelsGained = win.PlayerLevelsGained;
                    break;
                }
            }

            if (lastFought != null)
                for (int i = 0; i < lastFought.Units.Count; i++)
                    if (lastFought.Units[i].IsPlayer) res.Squad.Add(lastFought.Units[i]);

            res.Replay = ChooseReplay(stage, res, fighting);

            int nextWave, nextBattle;
            ResumePointFor(stage, out nextWave, out nextBattle);
            res.NextWave = nextWave;
            res.NextBattle = nextBattle;
            return res;
        }

        /// <summary>
        /// The battle of a resolved wave worth watching: the one that was lost, else the win that
        /// left the lowest share of the line's HP. The first of equals, so the choice is stable.
        /// Chosen from the record ResolveWave already holds, so it adds no fight and no rule.
        /// </summary>
        private static WaveReplay ChooseReplay(StageDef stage, WaveResolution res, List<HeroInstance> fighting)
        {
            WaveBattleOutcome pick = null;
            for (int i = 0; i < res.Battles.Count; i++)
            {
                WaveBattleOutcome o = res.Battles[i];
                if (!o.Victory) { pick = o; break; }
                if (pick == null || o.PlayerHpRatio < pick.PlayerHpRatio) pick = o;
            }
            if (pick == null) return null;

            WaveReplay replay = new WaveReplay();
            replay.Stage = stage;
            replay.Wave = pick.Wave;
            replay.Battle = pick.Battle;
            replay.Seed = pick.Seed;
            replay.Lost = !pick.Victory;
            replay.Team.AddRange(fighting);
            return replay;
        }

        /// <summary>
        /// Fights a replay's battle again: the same stage position, the same seed, the copied line.
        /// Reads the profile only for what a request always reads from it (equipped armor and guild
        /// tech) and writes nothing to it: no cursor, no purse, no tally. That is the whole of what
        /// "a replay pays nothing" means, and CheckWaveReplay proves it against the serialized save.
        /// </summary>
        public BattleResult ReplayBattle(WaveReplay replay)
        {
            StageDef resolved = CampaignWaveGenerator.BuildBattle(replay.Stage, replay.Wave, replay.Battle);
            BattleRequest request = BuildBattleRequest(resolved, replay.Seed);
            request.Team = replay.Team;
            return CombatSimulation.Run(request);
        }

        /// <summary>Whether a one-tap sweep is offered: the stage has a real curve and the player
        /// has already finished it the long way. A stage never cleared has to be fought.</summary>
        public bool CanSweep(StageDef stage)
        {
            return stage != null && stage.HasWaveCurve && Profile.HasClearedStage(stage.Id);
        }

        public bool CampaignComplete
        {
            get { return Content.AllStages.Count > 0 && FirstUnclearedStage == null; }
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
            return GrantBattleResult(stage, victory, nowUnix, 0, 0);
        }

        /// <summary>
        /// The stage clear, less what its waves already paid on the way (RecordLadderWin). The
        /// deduction applies to a first clear only and never takes the purse below zero; a repeat
        /// clear has nothing to deduct, because nothing drips on one.
        /// </summary>
        private BattleRewards GrantBattleResult(StageDef stage, bool victory, long nowUnix,
                                                int dripGoldPaid, int dripAccountXpPaid)
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
            if (firstClear)
            {
                gold = Math.Max(0, gold - dripGoldPaid);
                accountXp = Math.Max(0, accountXp - dripAccountXpPaid);
            }

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
            if (!rewards.RepeatCapped) OrderEvent(OrderKind.Stage, 1, nowUnix);

            if (firstClear)
            {
                // The frontier is taken BEFORE the id is added: clearing the frontier stage is
                // what unlocks the next one, and once the id is in the list the frontier has
                // already moved on.
                StageDef frontier = FirstUnclearedStage;
                Profile.ClearedStageIds.Add(stage.Id);
                Profile.StagesCleared = CampaignProgress.CountCleared(Profile, Content);
                rewards.UnlockedNextStage = frontier != null && frontier.Id == stage.Id;
                // The wall and finale chests open on the first clear, at the frontier this clear
                // just reached. Null for every other stage and when wall-chests.json is off.
                rewards.Chest = WallChests.PayFirstClear(this, stage);
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
        /// Chooses which of a hero's talents is live, or 0 to run none. Freely changeable and free
        /// of charge: there is no cost, no cooldown and no confirmation, so this is the whole rule
        /// and a screen calling it needs no other API.
        ///
        /// Returns false for a hero this profile does not own, and for a slot the hero does not
        /// author - a selection is a promise that something resolves, so this refuses rather than
        /// storing an index that silently does nothing. Clearing to 0 always succeeds, including
        /// for a hero with no talents at all, because "none" is always a reachable state.
        ///
        /// Also returns false for a slot the hero's ASCENSION has not unlocked (DESIGN-TALENTS T6,
        /// HeroInstance.TalentSlotsUnlocked): tier 0 may pick talent 1, tier 1 talents 1-2, tier 2
        /// all three. Refusing here is the same promise as refusing an unauthored slot - a player
        /// cannot choose what the hero cannot run.
        ///
        /// Refusing a pick is NOT the same as clearing one already stored. A selection that was
        /// legal when it was made and is locked now - a tier that moved under it, a hand-edited
        /// save - is kept, resolves to nothing in the fight, and comes back whole when the tier
        /// does. That rule lives on HeroInstance.LiveTalent, where an unauthored slot's already
        /// does.
        ///
        /// Deliberately no "only out of battle" guard. A battle resolves from a BattleRequest built
        /// at the moment it starts (BuildBattleRequest), and CombatSimulation.BuildUnits reads the
        /// selection once during setup, so a change made mid-replay cannot reach a fight already
        /// resolved - there is no live state for a guard here to protect.
        /// </summary>
        public bool SelectTalent(HeroInstance hero, int selection)
        {
            if (hero == null) return false;
            if (Profile.FindHeroByUid(hero.Uid) != hero) return false;

            if (selection == 0) { hero.SelectedTalent = 0; return true; }
            if (selection < 0 || selection > HeroDef.MaxTalents) return false;
            if (hero.Def == null || hero.Def.TalentAt(selection) == null) return false;
            if (!hero.TalentSlotUnlocked(selection)) return false;

            hero.SelectedTalent = selection;
            return true;
        }

        // ------------------------------------------------------------------ the Guild Path

        /// <summary>
        /// Whether the player owns a Guild Path node. Read off the server's last answer
        /// (PlayerProfile.GuildPathNodes) against this build's guild-path.json, so a node the build
        /// does not know grants nothing here.
        /// </summary>
        public bool OwnsGuildPathNode(string id)
        {
            if (Content.GuildPath.Node(id) == null) return false;
            // BOUGHT OR GRANTED, and the OR is G9 in one line. These nodes were sold for Waymarks
            // under 0011 and are handed out by early guild levels under the tech tree that replaced
            // it (claude/DESIGN-GUILD-TECH.md). An account that already paid keeps what it paid
            // for, and one that never did gets it from its guild's level - so the tree could
            // replace the Path with no migration of anybody's purchases and nothing taken away.
            if (Profile.GuildPathNodes.Contains(id)) return true;
            return Content.GuildTech.NodesGrantedAt(GuildLevel).Contains(id);
        }

        /// <summary>
        /// The guild's tech level, 0 for a player in no guild or a guild that has not levelled.
        /// Read off the server's last answer and held down to what this build's tree can express,
        /// for the reason OwnsGuildPathNode clamps: a level past the ceiling this build knows would
        /// open steps it cannot draw.
        /// </summary>
        public int GuildLevel
        {
            get
            {
                if (Profile.GuildTech == null) return 0;
                int ceiling = Content.GuildTech.IsUsable ? Content.GuildTech.LevelCeiling : 0;
                return Math.Max(0, Math.Min(Profile.GuildTech.Level, ceiling));
            }
        }

        private bool OwnsGuildPathKind(GuildPathNodeKind kind)
        {
            for (int i = 0; i < Content.GuildPath.Nodes.Count; i++)
            {
                GuildPathNodeDef n = Content.GuildPath.Nodes[i];
                if (n.Kind == kind && OwnsGuildPathNode(n.Id)) return true;
            }
            return false;
        }

        /// <summary>Lines the formation can switch between: the one every player has, plus one
        /// per Line node owned.</summary>
        public int LinesAllowed
        {
            get
            {
                int lines = 1;
                for (int i = 0; i < Content.GuildPath.Nodes.Count; i++)
                {
                    GuildPathNodeDef n = Content.GuildPath.Nodes[i];
                    if (n.Kind == GuildPathNodeKind.Line && OwnsGuildPathNode(n.Id)) lines++;
                }
                return lines;
            }
        }

        public bool LinesKeepTalents
        {
            get { return OwnsGuildPathKind(GuildPathNodeKind.LineTalents); }
        }

        /// <summary>
        /// Takes the server's answer as the truth about the Path: the balance and the nodes owned,
        /// replacing whatever the save held. The server is the only place either can change, so a
        /// save that disagrees is simply behind (or edited), and the answer wins. Clamps the active
        /// line if the answer allows fewer lines than the save was on.
        /// </summary>
        public void ApplyGuildPathState(GuildPathState state)
        {
            if (state == null) return;
            Profile.Waymarks = state.Balance;
            Profile.GuildPathNodes.Clear();
            for (int i = 0; i < state.Nodes.Count; i++) Profile.GuildPathNodes.Add(state.Nodes[i]);
            if (Profile.ActiveLine >= LinesAllowed) SwitchLine(0);
        }

        // ------------------------------------------------------------------ the guild tech tree

        /// <summary>
        /// Takes the server's answer as the truth about the tree (claude/DESIGN-GUILD-TECH.md): the
        /// guild's level and XP, the rungs this account bought, and the talent the guild picked on
        /// each family. ApplyGuildPathState's sibling and the same contract - the server is the only
        /// place any of it can change, so a save that disagrees is behind or edited and the answer
        /// wins.
        ///
        /// Clamps the active line afterwards for the same reason ApplyGuildPathState does, and it
        /// matters more here: a guild level grants Line nodes (G9), so an answer reporting a LOWER
        /// level than the save held takes lines away, and the formation must not be left standing
        /// on one that is gone.
        /// </summary>
        public void ApplyGuildTechState(GuildTechState state)
        {
            if (state == null) return;
            Profile.GuildTech = state.Clone();
            if (Profile.ActiveLine >= LinesAllowed) SwitchLine(0);
        }

        /// <summary>Rungs this account has bought on a family that actually count: what was bought,
        /// held down to what the guild level has opened (GuildTech.RungsOn).</summary>
        public int RungsOn(string familyId)
        {
            return GuildTech.RungsOn(Content.GuildTech, Profile.GuildTech, familyId);
        }

        /// <summary>
        /// Whether this account may buy the next rung on a family right now, and why not when it
        /// may not. The screen prints the reason; the server decides (0012) and this only predicts.
        /// </summary>
        public bool CanBuyRung(string familyId, out int rung, out int cost, out string why)
        {
            rung = 0; cost = 0; why = "";
            GuildTechConfig tree = Content.GuildTech;
            GuildTechFamilyDef family = tree.Family(familyId);
            if (family == null) { why = "no such track"; return false; }
            int have = Profile.GuildTech != null ? Math.Max(0, Profile.GuildTech.RungsIn(familyId)) : 0;
            if (have >= tree.RungsPerFamily) { why = "every rung bought"; return false; }
            rung = have + 1;
            cost = tree.RungCost(rung);
            int needs = tree.LevelForRung(familyId, rung);
            if (needs > GuildLevel) { why = "guild level " + needs; return false; }
            if (Profile.Waymarks < cost) { why = (cost - Profile.Waymarks) + " more " + tree.Currency; return false; }
            return true;
        }

        /// <summary>
        /// Makes another saved line the formation. The line being left is written back into its
        /// slot first - its heroes, and its talent picks once the player owns Lines Keep Talents -
        /// so nothing set on it is lost. A line switched to for the first time starts as a copy of
        /// the one being left, which is what a player editing a variant wants.
        ///
        /// Heroes the player no longer owns are dropped from the loaded line and NormaliseTeam
        /// fills the gap, exactly as it does for Team on load. Talent picks are restored through
        /// SelectTalent, so a pick naming a slot the hero no longer authors is refused rather than
        /// written. Formation only: nothing here changes what any hero is.
        /// </summary>
        public bool SwitchLine(int index)
        {
            if (index < 0 || index >= LinesAllowed) return false;
            int from = Profile.ActiveLine < 0 ? 0 : Profile.ActiveLine;
            while (Profile.Lines.Count <= Math.Max(index, from)) Profile.Lines.Add(null);

            SavedLine left = CaptureLine();
            Profile.Lines[from] = left;
            if (index == from) return true;

            SavedLine target = Profile.Lines[index] ?? CaptureLine();
            Profile.Lines[index] = target;
            for (int s = 0; s < Profile.Team.Length; s++)
            {
                string uid = s < target.Heroes.Length ? target.Heroes[s] : null;
                Profile.Team[s] = Profile.FindHeroByUid(uid) != null ? uid : null;
            }
            Profile.NormaliseTeam();
            if (target.HasTalents && LinesKeepTalents)
            {
                for (int s = 0; s < Profile.Team.Length && s < target.Talents.Length; s++)
                {
                    HeroInstance hero = Profile.FindHeroByUid(Profile.Team[s]);
                    if (hero != null && hero.Uid == target.Heroes[s]) SelectTalent(hero, target.Talents[s]);
                }
            }
            Profile.ActiveLine = index;
            return true;
        }

        private SavedLine CaptureLine()
        {
            SavedLine line = new SavedLine();
            bool talents = LinesKeepTalents;
            for (int s = 0; s < Profile.Team.Length && s < line.Heroes.Length; s++)
            {
                line.Heroes[s] = Profile.Team[s];
                HeroInstance hero = talents ? Profile.FindHeroByUid(Profile.Team[s]) : null;
                line.Talents[s] = hero != null ? hero.SelectedTalent : 0;
            }
            line.HasTalents = talents;
            return line;
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
        /// permanent - so ClaimedMilestoneIds is the only thing standing between this and a repeat
        /// payout: checked and updated in the same call, before the reward is applied, so there is
        /// no window where a second call could slip through between the check and the grant. By
        /// id, never by threshold, so retuning a threshold cannot reopen a claim (EventMilestone).
        /// Returns false for an unknown event or milestone, an already-claimed one, or one whose
        /// threshold the player has not reached yet.
        /// </summary>
        public bool ClaimEventMilestone(string eventId, string milestoneId)
        {
            EventDef evt = Content.GetEvent(eventId);
            if (evt == null) return false;
            EventMilestone milestone = evt.FindMilestone(milestoneId);
            if (milestone == null) return false;
            EventProgressEntry progress = GetEventProgress(eventId);
            if (progress.HasClaimed(milestone.Id)) return false;
            if (progress.Points < milestone.Threshold) return false;
            progress.ClaimedMilestoneIds.Add(milestone.Id);
            ApplyEventReward(milestone.Reward);
            return true;
        }

        /// <summary>
        /// Redeems one tier of an event's currency ladder: spends Cost of the event's own currency,
        /// grants Reward. Unlike a milestone this is a genuine purchase rather than an achievement -
        /// the balance is actually deducted - so redeeming the same tier twice is two purchases, not
        /// a double-claim, and nothing tracks a "redeemed" state the way ClaimedMilestoneIds
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

        /// <summary>
        /// Puts one Echo back in the bank, settling regen first: the Armory's Echo (SinkShop).
        /// Returns false, changing nothing but the settled regen, when the bank is already full.
        /// Reaching the cap snaps the clock to nowUnix, as SettleRecitalEchoes does, because time
        /// that passes while the bank is full banks nothing; below the cap the clock is left alone,
        /// so the fractional remainder toward the next Echo survives the purchase.
        /// </summary>
        public bool AddRecitalEcho(long nowUnix)
        {
            SettleRecitalEchoes(nowUnix);
            int max = Math.Max(0, Balance.RecitalEchoesMax);
            if (Profile.RecitalEchoesBanked >= max) return false;
            Profile.RecitalEchoesBanked++;
            if (Profile.RecitalEchoesBanked >= max) Profile.RecitalEchoesClockUnix = Math.Max(Profile.RecitalEchoesClockUnix, nowUnix);
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

        /// <summary>AddRecitalEcho for Brands: one Brand back in the bank, refused when it is full.</summary>
        public bool AddProvingBrand(long nowUnix)
        {
            SettleProvingBrands(nowUnix);
            int max = Math.Max(0, Balance.ProvingBrandsMax);
            if (Profile.ProvingBrandsBanked >= max) return false;
            Profile.ProvingBrandsBanked++;
            if (Profile.ProvingBrandsBanked >= max) Profile.ProvingBrandsClockUnix = Math.Max(Profile.ProvingBrandsClockUnix, nowUnix);
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
            OrderEvent(OrderKind.Arena, 1, nowUnix);

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
            OrderEvent(OrderKind.Arena, 1, nowUnix);
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

            IdleReport yield = IdleYield(elapsed);
            report.Gold = yield.Gold;
            report.Shards = yield.Shards;
            return report;
        }

        /// <summary>
        /// What this many seconds of idle income are worth at the frontier as it stands: the one
        /// formula behind the idle chest (PeekIdle) and behind every daily-loop reward, which are
        /// priced in hours of this and nowhere else. Reads the profile, writes nothing, and ignores
        /// the idle cap and the claim clock: only the rate is asked for.
        /// </summary>
        public IdleReport IdleYield(long seconds)
        {
            IdleReport report = new IdleReport();
            report.Seconds = seconds;
            StageDef idleStage = FurthestClearedStage;
            report.Gold = (int)Math.Floor(StatMath.IdleGoldPerSecond(Balance, idleStage) * seconds);
            report.Shards = (int)Math.Floor(StatMath.IdleShardsPerSecond(Balance, idleStage) * seconds);
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
                StageDef s = FurthestClearedStage;
                return StatMath.IdleGoldPerSecond(Balance, s) * 3600f;
            }
        }

        public float IdleShardsPerHour
        {
            get
            {
                StageDef s = FurthestClearedStage;
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
            TelemetryHooks.Summon(1);
            OrderEvent(OrderKind.Summon, 1, nowUnix);

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
