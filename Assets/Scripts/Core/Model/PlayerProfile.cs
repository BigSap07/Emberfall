using System;
using System.Collections.Generic;
using System.Text;
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

        /// <summary>The piece's own aspect tag, rolled at drop and independent of its slot. Matched
        /// against the WEARER's aspect for attunement (DESIGN-ARMOR section 6) and used for nothing
        /// else - it never touches AspectChart and never affects combat directly.</summary>
        public Aspect Aspect = Aspect.Ember;

        public ArmorTemper Temper = ArmorTemper.Rough;

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

        /// <summary>Milestone thresholds already claimed, keyed by the threshold itself rather than
        /// a list index - see EventMilestone. The only guard against double-paying a milestone.</summary>
        public readonly List<long> ClaimedMilestoneThresholds = new List<long>();
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

        /// <summary>
        /// The Anvil's currency (DESIGN-ARMOR section 7). Paid by salvaging armor, spent rerolling
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

        /// <summary>
        /// Every armor piece the player owns, equipped or not. HeroInstance.EquippedArmor holds
        /// uids referencing entries here; an entry nothing references is simply in the pool.
        /// Same relationship OwnedRelics and EquippedRelicId have, deliberately - and deliberately
        /// a separate list rather than a shared "owned equipment" one, so no armor change can
        /// disturb a relic and vice versa.
        /// </summary>
        public readonly List<ArmorPiece> OwnedArmor = new List<ArmorPiece>();

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
            if (piece == null) return result;
            for (int i = 0; i < OwnedArmor.Count; i++)
            {
                ArmorPiece other = OwnedArmor[i];
                if (other == piece) continue;
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
