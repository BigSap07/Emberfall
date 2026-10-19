using System;
using System.Collections.Generic;
using System.Text;
using Emberfall.Core.Combat;
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
        public int Copies = 1;         // bodies of this hero owned, itself included: duplicates add, conversions and ascensions spend
        public long AcquiredUnix;

        /// <summary>
        /// How many of Copies are SEALED: granted by a purchase, so they may never enter a random
        /// mechanic (SealedState). Always read through SealedCopiesHeld, which caps it at Copies,
        /// so a spend that forgot to lower it can only ever err toward "more sealed". Zero for every
        /// hero that was earned, and zero is not written to the save.
        /// </summary>
        public int SealedCopies;

        /// <summary>SealedCopies, never above Copies and never below zero.</summary>
        public int SealedCopiesHeld
        {
            get { return Math.Max(0, Math.Min(SealedCopies, Copies)); }
        }

        /// <summary>
        /// Ascension tier, 0 until the hero first ascends. Only a 5-star ever has anything else
        /// (F2). It sets the hero's level cap (F7), and it sets how far along the one level
        /// ladder each of its levels moves the hero: see StatMath.LevelAt for why Level stays the
        /// single number every stat line is built from, and GameSession.TierLevelOf for the level
        /// inside the tier that an ascension resets to 1 (F6).
        /// </summary>
        public int Ascension;

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

        /// <summary>
        /// The six armor slots, positional: index i holds the uid of the ArmorPiece in
        /// ArmorInfo.AllSlots[i], or empty for an empty slot. REFERENCES into
        /// PlayerProfile.OwnedArmor, exactly as EquippedRelicId is a reference into OwnedRelics -
        /// a piece can be owned and unequipped, and the same piece can never be worn twice.
        ///
        /// Positional rather than a slot-keyed dictionary because the slot is already a property of
        /// the piece: an entry here whose piece disagrees with its index is a corrupt save, and
        /// NormaliseArmor is what catches it. Sized from ArmorInfo.AllSlots rather than from a
        /// literal 6, so adding a seventh slot is one enum member and not a hunt for hardcoded
        /// sixes - the trap DESIGN-BUILD-ORDER records for MaxTeamSize's "three hardcoded 3s".
        /// </summary>
        public readonly string[] EquippedArmor = new string[ArmorInfo.AllSlots.Length];

        /// <summary>
        /// Which of the hero's talents this player has chosen: 1, 2 or 3, or 0 for none. Per owned
        /// hero, not per def, because the def is shared by every unit in every battle and two
        /// players owning the same hero must be able to run different talents.
        ///
        /// Zero is the default and means exactly what it says - no talent is live, the hero fights
        /// on its authored passives alone. That is the state every hero in every save written
        /// before version 12 is in, and it needs no migration for the same reason the version 10
        /// avatar bump needed none: absent reads as 0, and 0 is precisely true of a player who was
        /// never offered the choice rather than a synthesised value.
        ///
        /// An INDEX, not an id. A talent has no identity outside the hero that authors it, so the
        /// save names the slot and HeroDef.TalentAt resolves it - which also means a content edit
        /// that renames a talent does not orphan a selection. A selection naming a slot the hero no
        /// longer authors resolves to null and is kept rather than cleared, exactly as an
        /// unresolvable EquippedRelicId is kept.
        ///
        /// Freely changeable, with no cost and no cooldown: GameSession.SelectTalent is the whole
        /// of the rule, and it writes this field.
        /// </summary>
        public int SelectedTalent;

        /// <summary>
        /// THE ASCENSION GATE, DERIVED. How many of this hero's three talent slots its ascension
        /// tier has unlocked: one at tier 0, two at tier 1, three at tier 2 (DESIGN-TALENTS T6).
        ///
        /// Derived, never stored. Ascension is already save state and the mapping is a rule, so a
        /// persisted "unlocked" field could only ever disagree with the tier that produced it -
        /// and the first save that disagreed would be a hero whose card said one thing and whose
        /// fight did another. Nothing about the gate is written to disk, which is why it needs no
        /// save version: every save ever written already carries the tier it derives from.
        ///
        /// The two ceilings meet exactly and not by accident. HeroDef.MaxTalents is 3 and
        /// StatMath.MaxAscensionTier reads 2 off balance.json's three-entry fiveStarLevelCaps, so
        /// the last tier unlocks the last talent with nothing left over on either side. The Min is
        /// what keeps that true if either number moves: a fourth tier would unlock nothing new
        /// rather than name a slot no hero authors.
        /// </summary>
        public int TalentSlotsUnlocked
        {
            get { return Math.Min(HeroDef.MaxTalents, Math.Max(0, Ascension) + 1); }
        }

        /// <summary>Whether a 1-based talent slot is unlocked for this hero. False for 0, which is
        /// "none" and is not a slot - clearing the pick is always allowed and never asks this.</summary>
        public bool TalentSlotUnlocked(int selection)
        {
            return selection >= 1 && selection <= TalentSlotsUnlocked;
        }

        /// <summary>The ascension tier that unlocks a 1-based talent slot: 0, 1, 2. The inverse of
        /// TalentSlotsUnlocked, and the only other place the mapping is written.</summary>
        public static int TierUnlockingTalent(int selection)
        {
            return Math.Max(0, selection - 1);
        }

        /// <summary>
        /// The talent this hero ACTUALLY FIGHTS WITH: the selection resolved through both the def
        /// and the gate, or null. CombatSimulation.BuildUnits reads this and nothing else, so a
        /// locked slot is as absent from the battle as an unselected one - not a quieter talent, no
        /// passive, no extra draw, nothing the record can see.
        ///
        /// A locked selection is KEPT rather than cleared, exactly as an unresolvable
        /// EquippedRelicId and an unauthored talent slot already are. The selection is the player's
        /// statement of intent; the tier is a fact about the hero today. A hero that somehow drops
        /// below the tier it picked at gets its pick back the moment it climbs again, and nothing
        /// in between silently rewrote what the player asked for.
        /// </summary>
        public PassiveDef LiveTalent
        {
            get { return Def != null && TalentSlotUnlocked(SelectedTalent) ? Def.TalentAt(SelectedTalent) : null; }
        }

        /// <summary>The slot LiveTalent resolved, or 0. What a snapshot records: a published line
        /// must name the talent it will be seen fighting with, and a kept-but-locked pick is not
        /// that.</summary>
        public int LiveTalentSelection
        {
            get { return LiveTalent != null ? SelectedTalent : 0; }
        }

        [NonSerialized] public HeroDef Def;

        /// <summary>
        /// An independent copy of every field the simulation or the save reads, so a line can be
        /// held after the player changes the hero it was taken from (GameSession.ResolveWave keeps
        /// one to replay its hardest battle). Every public instance field is copied, and the
        /// harness reflects over them to say so: a field added to this class and left out here
        /// fails CheckWaveReplay rather than quietly replaying a different hero.
        /// </summary>
        public HeroInstance Copy()
        {
            HeroInstance c = new HeroInstance();
            c.Uid = Uid;
            c.DefId = DefId;
            c.Level = Level;
            c.Copies = Copies;
            c.SealedCopies = SealedCopies;
            c.AcquiredUnix = AcquiredUnix;
            c.Ascension = Ascension;
            c.EquippedRelicId = EquippedRelicId;
            for (int i = 0; i < EquippedArmor.Length && i < c.EquippedArmor.Length; i++)
                c.EquippedArmor[i] = EquippedArmor[i];
            c.SelectedTalent = SelectedTalent;
            c.Def = Def;
            return c;
        }

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

    /// <summary>
    /// One resolved stat line on an armor piece - the primary, or one substat, or one attunement
    /// bonus. Three fields and nowhere to put a fourth: DESIGN-ARMOR's "no unique effects, ever"
    /// is enforced by this shape rather than by convention, the same way RelicDef's original
    /// no-numbers guarantee was enforced by having no numeric field at all. There is no ability
    /// reference, no status, no trigger, no target mode and no hook, so a piece cannot express one.
    ///
    /// Value is read against Mode exactly as RelicStatBonus.Value is: 0.14 with Multiply means
    /// Attack *= 1.14, 0.12 with Add means CritChance += 0.12. Mode is stored rather than re-derived
    /// on read so a saved piece keeps the arithmetic it was rolled with even if ArmorInfo.ModeFor
    /// is ever changed under it - the same reason a piece stores resolved values instead of a seed.
    /// </summary>
    public sealed class ArmorStatRoll
    {
        public ArmorStat Stat = ArmorStat.Attack;
        public ArmorStatMode Mode = ArmorStatMode.Multiply;
        public float Value;

        public ArmorStatRoll() { }

        public ArmorStatRoll(ArmorStat stat, ArmorStatMode mode, float value)
        {
            Stat = stat;
            Mode = mode;
            Value = value;
        }

        /// <summary>
        /// One card line: "+14% Attack", or "+40 Holy Damage". The percent sign is decided by
        /// ArmorInfo.IsPercentStat rather than by Mode - every Add stat but Holy Damage is a
        /// fraction that reads as a percentage, and Holy Damage is flat damage that does not.
        /// </summary>
        public string Describe()
        {
            string label = ArmorInfo.StatLabel(Stat);
            if (!ArmorInfo.IsPercentStat(Stat))
            {
                int flat = (int)Math.Round(Value, MidpointRounding.AwayFromZero);
                return (flat >= 0 ? "+" : "") + flat + " " + label;
            }
            int pct = (int)Math.Round(Value * 100f, MidpointRounding.AwayFromZero);
            return (pct >= 0 ? "+" : "") + pct + "% " + label;
        }
    }

    /// <summary>
    /// One armor piece the player owns (DESIGN-ARMOR). Self-describing: it carries its own slot,
    /// aspect, temper and resolved stat rolls, and holds no reference into content at all.
    ///
    /// That is the one real design decision in this type. A piece could have stored a roll seed
    /// instead - smaller, and a reroll would be a single integer bump - but armor.json is tuning
    /// data that is expected to move, and a seed would mean every retune silently rerolls every
    /// piece every player owns. A stored roll is immune to that. It is the same instinct that makes
    /// a save hold hero ids rather than hero stats, applied to a thing that has no id to hold.
    ///
    /// There is at most one entry per uid, and a uid is allocated from PlayerProfile.NextArmorUid.
    /// </summary>
    public sealed class ArmorPiece
    {
        public string Uid = "";
        public string Name = "Armor";
        public ArmorSlot Slot = ArmorSlot.Weapon;

        /// <summary>An independent copy: the same slot, temper, aspect, name and rolls, a fresh set of
        /// roll objects. Uid and the Sealed mark are copied as they stand. The Quartermaster adds a
        /// copy of a stocked piece to the bag so the bag's piece and the shelf's are never one object.</summary>
        public ArmorPiece Clone()
        {
            ArmorPiece c = new ArmorPiece();
            c.Uid = Uid;
            c.Name = Name;
            c.Slot = Slot;
            c.Aspect = Aspect;
            c.Temper = Temper;
            c.Sealed = Sealed;
            c.Primary = new ArmorStatRoll(Primary.Stat, Primary.Mode, Primary.Value);
            for (int i = 0; i < SubStats.Count; i++)
                c.SubStats.Add(new ArmorStatRoll(SubStats[i].Stat, SubStats[i].Mode, SubStats[i].Value));
            return c;
        }

        /// <summary>The piece's own aspect tag, rolled at drop and independent of its slot. Matched
        /// against the WEARER's aspect for attunement (DESIGN-ARMOR section 6) and used for nothing
        /// else - it never touches AspectChart and never affects combat directly.</summary>
        public Aspect Aspect = Aspect.Ember;

        public ArmorTemper Temper = ArmorTemper.Rough;

        /// <summary>True for a piece granted by a purchase (SealedState). A sealed piece may be
        /// worn, kept and salvaged, and may never be rerolled or combined: both are a roll. Written
        /// to the save only when true.</summary>
        public bool Sealed;

        /// <summary>The slot's guaranteed stat, at whatever magnitude this piece rolled. Never
        /// null on a piece the roller produced; a hand-edited save with a missing primary reads as
        /// a zero-value Attack roll, which is inert rather than crashing.</summary>
        public ArmorStatRoll Primary = new ArmorStatRoll();

        /// <summary>Temper-many substats, no two naming the same stat. A substat MAY duplicate the
        /// primary's stat - see DESIGN-ARMOR section 5 for why that is allowed and duplicates
        /// within the substat list are not.</summary>
        public readonly List<ArmorStatRoll> SubStats = new List<ArmorStatRoll>();

        /// <summary>Every line on the piece, primary first. The one place that decides what "all of
        /// this piece's stats" means, so StatMath and the UI cannot disagree about it.</summary>
        public List<ArmorStatRoll> AllRolls()
        {
            List<ArmorStatRoll> all = new List<ArmorStatRoll>();
            if (Primary != null) all.Add(Primary);
            for (int i = 0; i < SubStats.Count; i++) if (SubStats[i] != null) all.Add(SubStats[i]);
            return all;
        }

        /// <summary>The piece's stat lines as one comma-separated string, primary first - the same
        /// job RelicDef.DescribeStats does for a relic, so the two card layouts stay comparable.</summary>
        public string DescribeStats()
        {
            List<ArmorStatRoll> all = AllRolls();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < all.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(all[i].Describe());
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// One player's progress against one live event: points earned, which milestone thresholds
    /// have already been claimed, and the event's own currency balance if it has one. Held by the
    /// profile the same way OwnedRelic is - by event id, not by a reference into content, so a
    /// save from before an event existed simply has no entry for it (the default, empty state) and
    /// a save from after the event's window closes keeps its claimed history intact.
    /// </summary>
    public sealed class EventProgressEntry
    {
        public string EventId = "";
        public long Points;
        public long Currency;

        /// <summary>Ids of the milestones already claimed - see EventMilestone. The only guard
        /// against double-paying a milestone. An id this build's content does not author is kept,
        /// not pruned: the milestone stays claimed, so it cannot reopen if content names it again.</summary>
        public readonly List<string> ClaimedMilestoneIds = new List<string>();

        /// <summary>Claims recorded by threshold, before milestones had ids (save version 18 and
        /// earlier), that the version 19 migration could not name - see
        /// SaveSerializer.CurrentVersion. Carried so the save still says they were claimed. Read by
        /// nothing else, and never matched against a threshold again, since that was the bug.</summary>
        public readonly List<long> UnresolvedClaimedThresholds = new List<long>();

        public bool HasClaimed(string milestoneId)
        {
            return !string.IsNullOrEmpty(milestoneId) && ClaimedMilestoneIds.Contains(milestoneId);
        }
    }

    /// <summary>
    /// One saved line of the formation (the Guild Path's Line nodes). Hero uids by slot, and -
    /// once the player owns Lines Keep Talents - each slot's talent pick as it stood when the line
    /// was last left. Uids, not def ids, for PlayerProfile.Team's reason: the formation names owned
    /// heroes, and a uid whose hero is gone is dropped on switch the way NormaliseTeam drops it.
    /// </summary>
    public sealed class SavedLine
    {
        public readonly string[] Heroes = new string[PlayerProfile.TeamSize];

        /// <summary>Talent slot per team slot, meaningful only when HasTalents: a line saved
        /// before the player owned Lines Keep Talents carries none and restores none.</summary>
        public readonly int[] Talents = new int[PlayerProfile.TeamSize];
        public bool HasTalents;
    }

    /// <summary>Everything the player owns. Pure data - serialised by SaveSerializer.</summary>
    public sealed class PlayerProfile
    {
        public string PlayerName = "Wayfarer";

        /// <summary>
        /// HeroDef id whose portrait stands for the player, or empty for none.
        ///
        /// Stored as the DEF id rather than a hero uid on purpose: a uid dies when its hero is
        /// salvaged or a save is rebuilt, and an avatar pointing at a dead uid is a broken picture.
        /// A def id is content, so the worst case is a player who no longer owns that hero - which
        /// the Profile screen treats as "pick again" rather than as an error.
        ///
        /// Empty is a real, expected value, not a missing one: a brand-new account owns three
        /// heroes but has chosen nothing, and every reader has to render that case anyway.
        /// </summary>
        public string AvatarHeroId = "";
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

        /// <summary>
        /// The Forge's currency (DESIGN-ARMOR section 7). Paid by salvaging armor, spent rerolling
        /// an armor piece's substats, and used for nothing else.
        ///
        /// A separate purse from RelicDust on purpose, and not merely for flavour: RelicDust has one
        /// choke point (GameSession.SpendRelicDust) that every expenditure routes through so the
        /// EventActivityKind.RelicDustSpent activity keeps seeing all of them. Armor spending through
        /// that choke point would silently start paying out event points; armor spending around it
        /// would leave a Dust path that skips the hook. A second currency is the only shape where
        /// neither is true.
        /// </summary>
        public long Slag;

        /// <summary>
        /// How many stages the loaded content names among ClearedStageIds. DERIVED, and it decides
        /// nothing: unlocks, the frontier, idle income and the furthest-stage readout all read the
        /// ids through CampaignProgress. It is kept (and saved as "stagesCleared") only so the
        /// cloud summary, the save comparison and the debug panel have a count without loading
        /// content. Before save version 20 it was a position, and a pre-20 save is migrated
        /// through SaveSerializer.PreIdStageOrder.
        /// </summary>
        public int StagesCleared;

        /// <summary>
        /// Position INSIDE the stage currently being attempted, per DESIGN-CAMPAIGN-STRUCTURE.md.
        ///
        /// ClearedStageIds says which stages are done; these three say how far into the next one the
        /// player has got. Mirrors RecitalCurrentFloor exactly: a plain persisted position with no
        /// expiry, so leaving a stage 90 battles in and coming back a week later resumes at battle
        /// 91 rather than restarting. There is deliberately no timestamp and no decay.
        ///
        /// CampaignCurrentStageId is what makes the pair meaningful. Wave 4 battle 7 is a
        /// different position in ch1_3 than in ch2_9, so the stage the progress BELONGS to has to
        /// be stored with it - otherwise switching stages mid-run would silently inherit the other
        /// stage's position. Empty means "no stage in progress", which is the correct state for a
        /// new account and for the moment after a stage is cleared.
        ///
        /// Both indices are 1-based, matching the readout the player sees ("Wave 4 of 12").
        /// </summary>
        public string CampaignCurrentStageId = "";
        public int CampaignCurrentWave = 1;
        public int CampaignCurrentBattle = 1;

        /// <summary>
        /// Banner slots every player has, from the first minute. Deploying fewer heroes than this
        /// is fine - empty slots are simply empty. Must stay equal to BalanceConfig.MaxTeamSize,
        /// which gates the same number on the simulation side; they are two names for one decision.
        /// </summary>
        public const int TeamSize = 5;

        public readonly List<string> ClearedStageIds = new List<string>();
        public readonly List<HeroInstance> Heroes = new List<HeroInstance>();

        /// <summary>
        /// How far through the first session's guided flow this player is (DESIGN-ONBOARDING.md):
        /// the number of steps COMPLETED, so a player who quits resumes at the step after the last
        /// one they finished. The steps and the complete value are Onboarding's constants. Saved
        /// from version 21; a save from before it that has any campaign progress loads as complete.
        /// </summary>
        public int TutorialStep;

        /// <summary>
        /// The free-play tips this player has seen, by screen name, and the features they have
        /// opened since each unlocked (FeatureGate.OpenedKey), which is what retires a feature's
        /// one-time "new" dot. Onboarding.AllTipsSeen stands for every tip there is, including ones
        /// added later: it is what a save that predates tips is migrated to. Saved from version 21.
        /// </summary>
        public readonly List<string> SeenTips = new List<string>();

        /// <summary>
        /// The daily loop's memory: today's orders and their progress, the chests, the week's count
        /// and the login calendar (DESIGN-DAILY-LOOP.md). NEVER NULL. Empty for every profile that
        /// never had DailyLoopSwitch on, and an empty one is not written to the save. Saved from
        /// version 22.
        /// </summary>
        public DailyLoopState DailyLoop = new DailyLoopState();

        /// <summary>
        /// What the Armory and the Archive remember: how much of each capped item has been bought
        /// today and this month (DESIGN-SINKS.md). NEVER NULL. Empty for every profile that never
        /// bought anything, and an empty one is not written to the save. Saved from version 23, and
        /// only a save that carries one says 23 (SaveSerializer.VersionOf).
        /// </summary>
        public SinksState Sinks = new SinksState();

        /// <summary>
        /// The Sealed part of each purse (SealedState): what a purchase granted, which a summon, a
        /// conversion or a forge gamble may never spend. NEVER NULL. Empty for every profile that
        /// never held a purchased good, and an empty one is not written to the save. Saved from
        /// version 24, and only a save that carries any Sealed good says 24
        /// (SaveSerializer.VersionOf).
        /// </summary>
        public SealedState Sealed = new SealedState();

        /// <summary>
        /// The Quartermaster's shelf: today's stock of rolled armor and which pieces are sold
        /// (Docs/DESIGN-QUARTERMASTER.md). NEVER NULL. Empty for every profile that never opened the
        /// shop, and an empty one is not written to the save. Saved from version 24, and only a save
        /// that carries a stock says 24 (SaveSerializer.VersionOf).
        /// </summary>
        public QuartermasterState Quartermaster = new QuartermasterState();

        /// <summary>Gold a random mechanic may spend: the purse less its Sealed part.</summary>
        public long FreeGold { get { return Gold - Math.Min(Math.Max(0, Sealed.Gold), Gold); } }

        /// <summary>Shards a summon may spend: the purse less its Sealed part.</summary>
        public long FreeShards { get { return Shards - Math.Min(Math.Max(0, Sealed.Shards), Shards); } }

        /// <summary>Slag a forge gamble may spend: the purse less its Sealed part.</summary>
        public long FreeSlag { get { return Slag - Math.Min(Math.Max(0, Sealed.Slag), Slag); } }

        /// <summary>Spends Gold in a DETERMINISTIC sink (a level-up), Sealed part first, so the
        /// Gold left is as free as it can be. The caller has checked Gold covers the amount.</summary>
        public void SpendGoldDeterministic(long amount)
        {
            if (amount <= 0) return;
            Gold -= amount;
            Sealed.Gold = Math.Max(0, Sealed.Gold - amount);
        }

        /// <summary>Whether this tip or opening is recorded. Null-safe so a caller with no profile
        /// reads "not seen".</summary>
        public static bool HasSeen(PlayerProfile profile, string key)
        {
            if (profile == null || string.IsNullOrEmpty(key)) return false;
            return profile.SeenTips.Contains(key) || profile.SeenTips.Contains(AllTipsSeenKey);
        }

        /// <summary>The SeenTips entry that stands for every tip. Onboarding.AllTipsSeen is the
        /// same string; it is declared here because HasSeen reads it.</summary>
        public const string AllTipsSeenKey = "*";

        /// <summary>
        /// Every relic the player owns, assigned or not. HeroInstance.EquippedRelicId references
        /// entries here by relic id; an entry nothing references is simply unassigned.
        /// </summary>
        public readonly List<OwnedRelic> OwnedRelics = new List<OwnedRelic>();

        /// <summary>
        /// Every armor piece the player owns, equipped or not. HeroInstance.EquippedArmor holds
        /// uids referencing entries here; an entry nothing references is simply in the pool.
        /// Same relationship OwnedRelics and EquippedRelicId have, deliberately - and deliberately
        /// a separate list rather than a shared "owned equipment" one, so no armor change can
        /// disturb a relic and vice versa.
        /// </summary>
        public readonly List<ArmorPiece> OwnedArmor = new List<ArmorPiece>();

        /// <summary>
        /// Hero records this build's content cannot resolve, kept as the exact JSON the save held
        /// and written back unchanged (save version 15, Docs/CLOUD-GAPS.md P7). A hero summoned on a
        /// newer build, or one whose def has left heroes.json, used to be dropped on load, and the
        /// next save and sync deleted it from the cloud copy. Now nothing in the game can see or
        /// spend it, and the day content names its defId again it loads as an ordinary hero. The
        /// same rule an unresolvable relic id already follows, applied to the whole record.
        ///
        /// Deliberately strings rather than parsed values: nothing reads them but the serializer,
        /// and a string cannot be edited by accident on the way through.
        /// </summary>
        public readonly List<string> UnresolvedHeroRecords = new List<string>();

        /// <summary>
        /// Armor records held back the same way: pieces worn by an unresolved hero, which travel
        /// with it so this build cannot salvage or combine what that hero is wearing, and pieces
        /// naming a slot, aspect, temper or stat this build does not know, which used to be read
        /// as Weapon, Ember, Rough or Attack and saved back changed.
        /// </summary>
        public readonly List<string> UnresolvedArmorRecords = new List<string>();

        /// <summary>Per-event progress, one entry per event the player has ever earned a point or
        /// currency toward. Absent entirely on a save from before the events engine existed, or for
        /// any event the player has not touched - both read as "no progress", the correct default,
        /// with no migration needed (see SaveSerializer.CurrentVersion).</summary>
        public readonly List<EventProgressEntry> EventProgress = new List<EventProgressEntry>();

        /// <summary>
        /// The Deep Recital: a persistent one-way floor ladder, not a run that resets. The floor
        /// currently being attempted - always RecitalHighestFloor + 1 in practice, since there is no
        /// mechanic that lets a player attempt any floor but the next unbeaten one. Stored explicitly
        /// anyway rather than derived, so the save format has a real field to migrate forward if a
        /// future mechanic ever breaks that invariant; NormaliseRecital re-derives it from
        /// RecitalHighestFloor if the two are ever found out of step (a hand-edited or migrated save).
        /// </summary>
        public int RecitalCurrentFloor = 1;

        /// <summary>The permanent record: the highest floor ever completed. 0 means no floor has
        /// been cleared yet. This is the number that never goes down.</summary>
        public int RecitalHighestFloor = 0;

        /// <summary>Whole Echoes currently banked, as of RecitalEchoesClockUnix - see
        /// GameSession.PeekRecitalEchoes for the regen math this is read against.</summary>
        public int RecitalEchoesBanked;

        /// <summary>UTC Unix seconds Echo regeneration is computed forward from. Only ever moves
        /// forward - the same backwards-clock immunity GameSession.RollDailyReset already proves for
        /// the repeat cap applies here: a clock wound back must not grant regen.</summary>
        public long RecitalEchoesClockUnix;

        /// <summary>
        /// Coda: The Deep Recital's own currency, the same isolated-purse pattern The Proving's
        /// Valor uses and for the same reason. It does not move Gold, Shards, Relic Dust, Slag or
        /// Valor. Its one sink is the Archive (SinkShop, DESIGN-SINKS.md), open only while
        /// SinksSwitch is on: with the switch off the purse accrues and buys nothing.
        /// </summary>
        public long Coda;

        // ---------------------------------------------------------------- The Proving (PvP)
        // Four fields plus a pending opponent, mirroring the Recital block above field for field.
        // Deliberately the same shape rather than a new one: DESIGN-PVP section 5 chose the Echo
        // bank as the gating pattern precisely so there is one attempt-resource idiom in the
        // project, not two that have to be reasoned about separately.

        /// <summary>
        /// Valor: The Proving's own currency, and the ONLY thing the mode pays.
        ///
        /// This reverses DESIGN-PVP section 6, which chose to pay Gold and Relic Dust and to add no
        /// new currency. That call was flagged for sign-off and was overruled deliberately, so the
        /// document has been corrected rather than left saying the opposite of the code.
        ///
        /// What the original reasoning was protecting still stands and is now protected a different
        /// way: the worry was that a new currency needs a sink, a sink means a shop, and a shop that
        /// sells power is a second progression track beside armor and relics - exactly the surface
        /// task 13 stabilised. The owner has since decided Valor may buy armor, attempts and hero
        /// copies. Its one sink is the Armory (SinkShop, DESIGN-SINKS.md), open only while
        /// SinksSwitch is on, and it sells fixed items for earned Valor with no chance in any of
        /// them. With the switch off Valor accrues and buys nothing.
        /// </summary>
        public long Valor;

        /// <summary>Whole Brands currently banked, as of ProvingBrandsClockUnix - see
        /// GameSession.PeekProvingBrands for the regen math this is read against.</summary>
        public int ProvingBrandsBanked;

        /// <summary>UTC Unix seconds Brand regeneration is computed forward from. Only ever moves
        /// forward, with the same wound-back-clock immunity SettleRecitalEchoes documents.</summary>
        public long ProvingBrandsClockUnix;

        /// <summary>
        /// The player's standing. A PERSONAL-PROGRESS NUMBER, not a rank (DESIGN-PVP section 7):
        /// every opponent in this mode is generated locally, so there is no population to be ranked
        /// against and nothing that reads this may present it as a competitive position. It earns
        /// its meaning by being the difficulty dial - ProvingGenerator scales opponent power off
        /// it - which is a real feedback loop that needs no other players at all.
        /// </summary>
        public int ProvingStanding;

        /// <summary>The permanent record: highest standing ever reached. Never goes down.</summary>
        public int ProvingHighestStanding;

        /// <summary>
        /// Total Proving attempts ever resolved. Monotonic - it counts losses as well as wins, and
        /// never decreases.
        ///
        /// It exists because the two numbers above CANNOT drive opponent generation on their own,
        /// and a full-session harness run proved it rather than a review catching it. The seed was
        /// (standing, highestStanding); once standing floors at 0 and highest stops moving, both
        /// inputs are stationary, so every further attempt regenerated the IDENTICAL opponent. A
        /// player who had stalled would have refought one line forever - the exact failure the
        /// generator's determinism is supposed to be a feature against, turned into a trap by
        /// keying it on values that stop changing.
        ///
        /// It also fixes the ghost bootstrap, which used highest standing as a stand-in for "how
        /// many attempts has this player had". One win takes highest from 0 to 3, so a player who
        /// won their first fight left the bootstrap after ONE opponent rather than three. Attempts
        /// is the quantity that question was always actually asking about.
        /// </summary>
        public int ProvingAttempts;

        /// <summary>
        /// The opponent currently on offer, persisted so it survives closing the app, or null when
        /// none has been generated yet. Stored as the resolved snapshot rather than as a generator
        /// seed for the reason DESIGN-ARMOR section 10 gives about armor rolls: a seed is smaller,
        /// but heroes.json, armor.json and balance.json are all tuning data expected to move, and a
        /// seed would mean every retune silently rerolled the opponent every player was mid-way
        /// through facing. This is also the field that becomes a cached server response later,
        /// unchanged in shape.
        /// </summary>
        public BattleLoadout ProvingOpponent;

        public readonly string[] Team = new string[TeamSize]; // hero uids, null = empty slot

        // ---------------------------------------------------------------- the Guild Path
        // claude/DESIGN-GUILDS-PHASE-TWO.md, system 2. THE SERVER OWNS BOTH NUMBERS BELOW: the
        // balance is minted only inside guild_dungeon_attempt and guild_path_settle and spent only
        // inside guild_path_unlock (Server/supabase/migrations/0011_guild_path.sql). These are the
        // last answer it gave, kept so an offline session still has the lines it paid for.
        // Keyed by account, never by guild: leaving a guild takes nothing away.

        /// <summary>Waymarks unspent, as the server last reported them. Display only.</summary>
        public long Waymarks;

        /// <summary>Guild Path node ids owned, as the server last reported them. An id this
        /// build's guild-path.json does not name is kept and grants nothing.</summary>
        public readonly List<string> GuildPathNodes = new List<string>();

        /// <summary>
        /// THE GUILD TECH TREE, as the server last reported it (claude/DESIGN-GUILD-TECH.md): the
        /// guild's level and XP, the rungs this account bought, and the talent the guild picked on
        /// each family. Copied here for the reason the Waymarks are - an offline session still
        /// fights with the tech it paid for.
        ///
        /// NEVER NULL, and an empty one is an exact no-op everywhere: no rung, no talent, no change
        /// to any stat line. That is what makes the reference player's "no guild tech" (G5) the
        /// default rather than a special case somebody has to remember to arrange.
        /// </summary>
        public Cloud.GuildTechState GuildTech = new Cloud.GuildTechState();

        /// <summary>
        /// Saved formation lines, index 0 upward. The ACTIVE one lives in Team, not here: Lines
        /// holds what each line was when it was last left, and GameSession.SwitchLine writes Team
        /// back into its slot before loading another. Empty until a player first switches.
        /// </summary>
        public readonly List<SavedLine> Lines = new List<SavedLine>();

        /// <summary>Which line Team is. 0 for every player who owns no Line node.</summary>
        public int ActiveLine;

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

        /// <summary>Counter behind AllocateArmorUid. Its own counter, not NextHeroUid's, so armor
        /// uids ("g1", "g2") and hero uids ("h1", "h2") are independent sequences - sharing one
        /// would make a save's uids depend on the interleaving of drops and summons.</summary>
        public int NextArmorUid = 1;
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

        /// <summary>
        /// Takes a hero out of the collection entirely: its last body went into a conversion. Its
        /// armor and relic are references held on the instance, so they return to the owned pools
        /// unworn with it. Refuses a deployed hero; the caller is expected to have checked, and a
        /// team slot naming nobody is a state NormaliseTeam repairs rather than one to create.
        /// </summary>
        public bool RemoveHero(HeroInstance hero)
        {
            if (hero == null || IsDeployed(hero.Uid)) return false;
            return Heroes.Remove(hero);
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

        /// <summary>
        /// BARE team power: level and stars only. HeroInstance.Stats is StatMath.ForHero(Def,
        /// Level) with no relic and no armor, so this deliberately sees neither.
        ///
        /// Kept as-is and kept named this, because it is the right number for "how far has this
        /// player levelled" and it is what the under-geared floor instrument pins against. What it
        /// is NOT is the number a stage's difficulty should be compared to - see
        /// EffectiveTeamPower, and DESIGN-BUILD-ORDER's task 13 entry for what assuming otherwise
        /// cost.
        /// </summary>
        public int TeamPower()
        {
            int total = 0;
            List<HeroInstance> team = TeamHeroes();
            for (int i = 0; i < team.Count; i++) total += team[i].Power;
            return total;
        }

        /// <summary>
        /// EFFECTIVE team power: what the line actually fights at, with equipped relics and armor
        /// folded in through the same StatMath.ForHero overload CombatSimulation itself uses. The
        /// number a stage's recommendedPower is meant to be compared against.
        ///
        /// This exists because the absence of it was a real, measured bug rather than an
        /// abstraction anyone wanted. recommendedPower was a bare-power target, so a line pinned to
        /// it AND wearing armor was over-geared for its own stage by construction and read 100% on
        /// sixteen of twenty-four stages. Six measured retune passes failed to separate the geared
        /// and ungeared columns by any setting of enemy level or composition, because gear is a
        /// straight power increase and no content lever hits a geared team harder.
        ///
        /// Resolved exactly the way the simulation resolves it - same overload, same order, relic
        /// then armor - so the number this reports and the number the fight uses cannot drift. A
        /// hero with no relic and no armor contributes exactly what TeamPower would have counted.
        /// </summary>
        public int EffectiveTeamPower(ContentDatabase content)
        {
            int total = 0;
            List<HeroInstance> team = TeamHeroes();
            ArmorConfig armorConfig = content != null ? content.Armor : null;
            for (int i = 0; i < team.Count; i++)
            {
                HeroInstance hero = team[i];
                if (hero.Def == null) continue;
                RelicDef relic = content != null && !string.IsNullOrEmpty(hero.EquippedRelicId)
                    ? content.GetRelic(hero.EquippedRelicId) : null;
                // Guild tech is IN the power score now. DESIGN-GUILD-TECH retires the rule that
                // guild progression changes no fight, so a line that fights harder has to read as
                // stronger - a power score that ignored the tree would understate every member of
                // a levelled guild against the campaign's own recommendedPower.
                total += StatMath.PowerScore(StatMath.ForHero(hero.Def, hero.Level, relic,
                                                              EquippedArmorOf(hero), armorConfig,
                                                              content != null ? content.GuildTech : null, GuildTech));
            }
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

        // ---------- armor helpers ----------

        public ArmorPiece FindArmor(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return null;
            for (int i = 0; i < OwnedArmor.Count; i++) if (OwnedArmor[i].Uid == uid) return OwnedArmor[i];
            return null;
        }

        /// <summary>The hero wearing a piece, or null when it is owned but sitting in the pool.</summary>
        public HeroInstance HeroWearingArmor(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return null;
            for (int h = 0; h < Heroes.Count; h++)
            {
                string[] worn = Heroes[h].EquippedArmor;
                for (int s = 0; s < worn.Length; s++) if (worn[s] == uid) return Heroes[h];
            }
            return null;
        }

        /// <summary>Owned pieces no hero is wearing - what the equip picker and the salvage list
        /// both read.</summary>
        public List<ArmorPiece> UnequippedArmor()
        {
            List<ArmorPiece> result = new List<ArmorPiece>();
            for (int i = 0; i < OwnedArmor.Count; i++)
            {
                if (HeroWearingArmor(OwnedArmor[i].Uid) == null) result.Add(OwnedArmor[i]);
            }
            return result;
        }

        /// <summary>
        /// Up to two OTHER unequipped pieces sharing a piece's slot and temper - what a one-tap
        /// Combine on that piece would consume alongside it. Short (fewer than 2) or empty is
        /// exactly when Combine has nothing to offer, which the UI reads to grey the button out
        /// rather than let a tap fail after the fact.
        ///
        /// Walks OwnedArmor in list order, which is acquisition order (uids are allocated
        /// sequentially), rather than sorting - a piece's uid is a string ("g1".."g10"...) and a
        /// lexicographic sort of those would put "g10" before "g2", which is not acquisition order.
        /// List order already is, with no parsing needed.
        /// </summary>
        public List<ArmorPiece> ArmorCombineCandidates(ArmorPiece piece)
        {
            List<ArmorPiece> result = new List<ArmorPiece>();
            if (piece == null || piece.Sealed) return result;   // a combine is a gamble: nothing Sealed enters it
            for (int i = 0; i < OwnedArmor.Count; i++)
            {
                ArmorPiece other = OwnedArmor[i];
                if (other == piece || other.Sealed) continue;
                if (other.Slot != piece.Slot || other.Temper != piece.Temper) continue;
                if (HeroWearingArmor(other.Uid) != null) continue;
                result.Add(other);
                if (result.Count == 2) break;
            }
            return result;
        }

        /// <summary>
        /// The piece in one of a hero's slots, or null. Resolves the uid through OwnedArmor, so a
        /// slot naming a piece the player no longer owns reads as empty rather than throwing -
        /// the same tolerance the relic slot has for an unresolvable relic id.
        /// </summary>
        public ArmorPiece ArmorInSlot(HeroInstance hero, ArmorSlot slot)
        {
            if (hero == null) return null;
            int index = SlotIndex(slot);
            if (index < 0 || index >= hero.EquippedArmor.Length) return null;
            return FindArmor(hero.EquippedArmor[index]);
        }

        /// <summary>Everything a hero is actually wearing, in slot order, skipping empty slots.
        /// THE input to StatMath.ApplyArmor - built here rather than in the simulation so the
        /// battle and the hero-detail preview cannot assemble it two different ways.</summary>
        public List<ArmorPiece> EquippedArmorOf(HeroInstance hero)
        {
            List<ArmorPiece> worn = new List<ArmorPiece>();
            if (hero == null) return worn;
            for (int i = 0; i < ArmorInfo.AllSlots.Length; i++)
            {
                ArmorPiece piece = ArmorInSlot(hero, ArmorInfo.AllSlots[i]);
                if (piece != null) worn.Add(piece);
            }
            return worn;
        }

        /// <summary>Position of a slot in ArmorInfo.AllSlots, which is the index EquippedArmor uses.
        /// -1 for a slot not in the array, which cannot happen through the enum but can through a
        /// cast.</summary>
        public static int SlotIndex(ArmorSlot slot)
        {
            for (int i = 0; i < ArmorInfo.AllSlots.Length; i++) if (ArmorInfo.AllSlots[i] == slot) return i;
            return -1;
        }

        /// <summary>
        /// How many of a hero's equipped pieces carry the hero's OWN aspect - the number the
        /// attunement thresholds are read against (DESIGN-ARMOR section 6). Zero for a hero with no
        /// def resolved, since without a def there is no wearer aspect to match.
        /// </summary>
        public int AttunementCount(HeroInstance hero)
        {
            if (hero == null || hero.Def == null) return 0;
            int matching = 0;
            List<ArmorPiece> worn = EquippedArmorOf(hero);
            for (int i = 0; i < worn.Count; i++) if (worn[i].Aspect == hero.Def.Aspect) matching++;
            return matching;
        }

        /// <summary>
        /// Forces the armor model back into agreement with itself, exactly as NormaliseRelics does
        /// for relics, and for the same reason: none of the three faults below is reachable through
        /// GameSession, so this exists for a hand-edited or half-written save rather than for a bug.
        ///
        /// Three faults, in order: a slot naming a piece nobody owns is emptied; a piece claimed by
        /// two heroes stays with the first that claims it; and a piece sitting in a slot it does not
        /// belong to is unequipped rather than silently granting its stats from the wrong slot.
        /// The third is the one the relic model has no analogue for - a relic has one slot and
        /// cannot be in the wrong one.
        /// </summary>
        public void NormaliseArmor()
        {
            List<string> claimed = new List<string>();
            for (int h = 0; h < Heroes.Count; h++)
            {
                string[] worn = Heroes[h].EquippedArmor;
                for (int s = 0; s < worn.Length; s++)
                {
                    string uid = worn[s];
                    if (string.IsNullOrEmpty(uid)) continue;

                    ArmorPiece piece = FindArmor(uid);
                    if (piece == null || claimed.Contains(uid)) { worn[s] = ""; continue; }
                    if (s >= ArmorInfo.AllSlots.Length || piece.Slot != ArmorInfo.AllSlots[s]) { worn[s] = ""; continue; }
                    claimed.Add(uid);
                }
            }
        }

        public string AllocateArmorUid()
        {
            string uid = "g" + NextArmorUid;
            NextArmorUid++;
            return uid;
        }

        // ---------- event helpers ----------

        public EventProgressEntry FindEventProgress(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return null;
            for (int i = 0; i < EventProgress.Count; i++) if (EventProgress[i].EventId == eventId) return EventProgress[i];
            return null;
        }

        // ---------- Deep Recital helpers ----------

        /// <summary>
        /// Re-derives RecitalCurrentFloor from RecitalHighestFloor - the invariant every path
        /// through GameSession already maintains, restated here the same way NormaliseRelics
        /// restates the relic model's invariant, for a hand-edited or migrated save that arrived
        /// out of step.
        /// </summary>
        public void NormaliseRecital()
        {
            if (RecitalHighestFloor < 0) RecitalHighestFloor = 0;
            int expected = RecitalHighestFloor + 1;
            if (RecitalCurrentFloor != expected) RecitalCurrentFloor = expected;
        }

        public string AllocateUid()
        {
            string uid = "h" + NextHeroUid;
            NextHeroUid++;
            return uid;
        }
    }
}
