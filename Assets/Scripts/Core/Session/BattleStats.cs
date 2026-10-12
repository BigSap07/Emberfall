using System;
using System.Collections.Generic;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;

namespace Emberfall.Core.Session
{
    /// <summary>One hero's totals over some set of battles. Keyed by DefId, which is stable across
    /// the battles of a wave (a team cannot hold two copies of a hero) where a unit Id is only
    /// stable inside one fight.</summary>
    public sealed class HeroTally
    {
        public string DefId = "";
        public string Name = "";
        /// <summary>Formation slot at the start of the fight, 0-based; 0 is the front.</summary>
        public int Slot;
        public Aspect Aspect = Aspect.Ember;
        public HeroRole Role = HeroRole.Striker;

        /// <summary>Damage events this hero sourced onto the enemy side, HP damage after shield
        /// absorption (the absorbed part is a separate ShieldAbsorb event and is not added).</summary>
        public long Dealt;
        /// <summary>Damage events that landed on this hero, whoever sourced them.</summary>
        public long Taken;
        /// <summary>Heal events this hero sourced; effective healing only, self-heals included.</summary>
        public long Healed;
        /// <summary>ShieldGain events this hero sourced.</summary>
        public long Shielded;
        /// <summary>Death events this hero sourced onto an enemy.</summary>
        public int Kills;
    }

    /// <summary>The line's tallies for one scope (one battle, or a sum of battles), in slot order.</summary>
    public sealed class TallySet
    {
        public readonly List<HeroTally> Heroes = new List<HeroTally>();

        public HeroTally Find(string defId)
        {
            for (int i = 0; i < Heroes.Count; i++) if (Heroes[i].DefId == defId) return Heroes[i];
            return null;
        }

        /// <summary>Adds another set into this one, matching heroes by DefId. A hero this set does
        /// not hold yet is copied in with its numbers.</summary>
        public void Add(TallySet other)
        {
            for (int i = 0; i < other.Heroes.Count; i++)
            {
                HeroTally o = other.Heroes[i];
                HeroTally mine = Find(o.DefId);
                if (mine == null)
                {
                    mine = new HeroTally();
                    mine.DefId = o.DefId; mine.Name = o.Name; mine.Slot = o.Slot;
                    mine.Aspect = o.Aspect; mine.Role = o.Role;
                    Heroes.Add(mine);
                }
                mine.Dealt += o.Dealt; mine.Taken += o.Taken;
                mine.Healed += o.Healed; mine.Shielded += o.Shielded;
                mine.Kills += o.Kills;
            }
            Heroes.Sort(delegate (HeroTally a, HeroTally b) { return a.Slot.CompareTo(b.Slot); });
        }
    }

    /// <summary>One fought battle's tally, with its number inside the wave.</summary>
    public sealed class BattleTallyEntry
    {
        public int Battle;
        public bool Lost;
        public TallySet Stats;
    }

    /// <summary>
    /// What a resolved wave's fights came to, per hero: the sum over the battles this resolve
    /// fought (Whole) and each battle on its own (Battles). Read off the BattleEvent records
    /// GameSession.ResolveWave already produces, in memory only: never in the save, never read by
    /// a fight, so combat stays a pure function.
    /// </summary>
    public sealed class WaveStats
    {
        public readonly TallySet Whole = new TallySet();
        public readonly List<BattleTallyEntry> Battles = new List<BattleTallyEntry>();

        public BattleTallyEntry ForBattle(int battle)
        {
            for (int i = 0; i < Battles.Count; i++) if (Battles[i].Battle == battle) return Battles[i];
            return null;
        }

        /// <summary>Folds one fought battle in. Called by ResolveWave for each battle, won or lost,
        /// before the BattleResult goes out of scope.</summary>
        public void Record(int battle, BattleResult result)
        {
            BattleTallyEntry entry = new BattleTallyEntry();
            entry.Battle = battle;
            entry.Lost = !result.Victory;
            entry.Stats = BattleTally.Of(result);
            Battles.Add(entry);
            Whole.Add(entry.Stats);
        }
    }

    /// <summary>
    /// The tally itself: a pure function of one BattleResult. It credits by source over Damage,
    /// Heal and ShieldGain only, exactly as TalentTrial.OutputOf and the star band do, so the
    /// panel agrees with the instruments. Pure and Unity-free so the DevHarness can call it.
    /// </summary>
    public static class BattleTally
    {
        public static TallySet Of(BattleResult r)
        {
            TallySet set = new TallySet();
            Dictionary<int, HeroTally> byId = new Dictionary<int, HeroTally>();
            HashSet<int> enemies = new HashSet<int>();
            for (int i = 0; i < r.Units.Count; i++)
            {
                UnitSnapshot u = r.Units[i];
                if (!u.IsPlayer) { enemies.Add(u.Id); continue; }
                HeroTally t = new HeroTally();
                t.DefId = u.DefId; t.Name = u.Name; t.Slot = u.Slot;
                t.Aspect = u.Aspect; t.Role = u.Role;
                byId[u.Id] = t;
                set.Heroes.Add(t);
            }
            set.Heroes.Sort(delegate (HeroTally a, HeroTally b) { return a.Slot.CompareTo(b.Slot); });

            for (int i = 0; i < r.Events.Count; i++)
            {
                BattleEvent e = r.Events[i];
                HeroTally src, dst;
                byId.TryGetValue(e.SourceId, out src);
                byId.TryGetValue(e.TargetId, out dst);
                switch (e.Type)
                {
                    case BattleEventType.Damage:
                        if (src != null && enemies.Contains(e.TargetId)) src.Dealt += e.Value;
                        if (dst != null) dst.Taken += e.Value;
                        break;
                    case BattleEventType.Heal:
                        if (src != null) src.Healed += e.Value;
                        break;
                    case BattleEventType.ShieldGain:
                        if (src != null) src.Shielded += e.Value;
                        break;
                    case BattleEventType.Death:
                        if (src != null && enemies.Contains(e.TargetId)) src.Kills++;
                        break;
                }
            }
            return set;
        }

        /// <summary>The formation slot as the Formation screen names it: the two ends are named,
        /// the middle is just order.</summary>
        public static string PositionLabel(int slot)
        {
            int last = PlayerProfile.TeamSize - 1;
            if (slot == 0) return "1 FRONT";
            if (slot == last) return (last + 1) + " BACK";
            return (slot + 1).ToString();
        }
    }

    public enum StatsScope { Whole, Battle }

    /// <summary>One panel row: a hero and the five figures, with each bar already scaled to the
    /// highest value in its column (0 to 1).</summary>
    public sealed class StatsRow
    {
        public string Name = "";
        public string Position = "";
        public Aspect Aspect;
        public HeroRole Role;
        public long Dealt, Taken, Healed, Shielded;
        public int Kills;
        public float DealtBar, TakenBar, HealedBar, ShieldedBar;
    }

    /// <summary>
    /// Everything the stats panel draws, decided here so the UI file only paints it. Built from a
    /// WaveResolution and READS it: it changes nothing, and the save never sees it.
    ///
    /// Two scopes: WHOLE WAVE, the battles this resolve fought, and a single battle. On a loss the
    /// single battle is the lost one and the panel opens on it. Otherwise it is the battle the
    /// replay (the WATCH battle) points at, and the panel opens on the whole wave.
    /// </summary>
    public sealed class BattleStatsView
    {
        public readonly StatsScope DefaultScope;
        /// <summary>The battle number the single-battle scope shows; 0 when there is none.</summary>
        public readonly int BattleNumber;
        public readonly string Heading;
        public readonly List<StatsRow> Rows = new List<StatsRow>();
        public StatsScope Scope { get; private set; }
        /// <summary>The hero with the highest damage dealt in the current scope; empty when nobody
        /// dealt any. The first of equals, in slot order.</summary>
        public string MvpName { get; private set; }
        /// <summary>The MVP's share of the line's damage, 0 to 1.</summary>
        public float MvpShare { get; private set; }

        private readonly WaveStats _stats;

        public static BattleStatsView For(WaveResolution r, string stageCode)
        {
            if (r == null || r.Stats == null || r.Stats.Battles.Count == 0) return null;
            return new BattleStatsView(r, stageCode);
        }

        private BattleStatsView(WaveResolution r, string stageCode)
        {
            _stats = r.Stats;
            bool lost = r.LostBattle > 0;
            int pick = lost ? r.LostBattle : (r.Replay != null ? r.Replay.Battle : 0);
            if (pick == 0 || _stats.ForBattle(pick) == null) pick = _stats.Battles[_stats.Battles.Count - 1].Battle;
            BattleNumber = pick;
            DefaultScope = lost ? StatsScope.Battle : StatsScope.Whole;
            Heading = stageCode + " · " + (r.StageCleared ? "STAGE CLEARED"
                                         : "WAVE " + r.Wave + (lost ? " LOST" : " CLEARED"));
            Select(DefaultScope);
        }

        public void Select(StatsScope scope)
        {
            Scope = scope;
            TallySet set = scope == StatsScope.Whole ? _stats.Whole : _stats.ForBattle(BattleNumber).Stats;
            Rows.Clear();

            long maxD = 0, maxT = 0, maxH = 0, maxS = 0, total = 0;
            HeroTally mvp = null;
            for (int i = 0; i < set.Heroes.Count; i++)
            {
                HeroTally t = set.Heroes[i];
                maxD = Math.Max(maxD, t.Dealt); maxT = Math.Max(maxT, t.Taken);
                maxH = Math.Max(maxH, t.Healed); maxS = Math.Max(maxS, t.Shielded);
                total += t.Dealt;
                if (t.Dealt > 0 && (mvp == null || t.Dealt > mvp.Dealt)) mvp = t;
            }
            for (int i = 0; i < set.Heroes.Count; i++)
            {
                HeroTally t = set.Heroes[i];
                StatsRow row = new StatsRow();
                row.Name = t.Name; row.Position = BattleTally.PositionLabel(t.Slot);
                row.Aspect = t.Aspect; row.Role = t.Role;
                row.Dealt = t.Dealt; row.Taken = t.Taken; row.Healed = t.Healed; row.Shielded = t.Shielded;
                row.Kills = t.Kills;
                row.DealtBar = Frac(t.Dealt, maxD); row.TakenBar = Frac(t.Taken, maxT);
                row.HealedBar = Frac(t.Healed, maxH); row.ShieldedBar = Frac(t.Shielded, maxS);
                Rows.Add(row);
            }
            MvpName = mvp != null ? mvp.Name : "";
            MvpShare = mvp != null && total > 0 ? (float)mvp.Dealt / total : 0f;
        }

        private static float Frac(long v, long max) { return max <= 0 ? 0f : (float)v / max; }
    }
}
