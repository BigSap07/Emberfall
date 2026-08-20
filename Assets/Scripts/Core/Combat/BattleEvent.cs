using System.Collections.Generic;
using Emberfall.Core.Data;

namespace Emberfall.Core.Combat
{
    public enum BattleEventType
    {
        BattleStart,
        RoundStart,
        TurnStart,
        BasicAttack,
        AbilityCast,
        PassiveTrigger,
        Damage,
        Heal,
        ShieldGain,
        ShieldAbsorb,
        StatusApplied,
        StatusExpired,
        EnergyChanged,
        Death,
        BattleEnd
    }

    /// <summary>
    /// The simulation's only output. The battle view walks this list and animates it;
    /// it never recomputes anything.
    /// </summary>
    public sealed class BattleEvent
    {
        public BattleEventType Type;
        public int Round;
        public int SourceId = -1;
        public int TargetId = -1;
        public int Value;
        public bool Critical;
        public string AbilityId;
        public string AbilityName;
        public string Vfx;
        public StatusKind Status = StatusKind.None;
        public int Duration;
        public bool Victory;

        public static BattleEvent Make(BattleEventType type, int round)
        {
            BattleEvent e = new BattleEvent();
            e.Type = type;
            e.Round = round;
            return e;
        }
    }

    /// <summary>Immutable picture of a unit at battle start, used to build the visual scene.</summary>
    public sealed class UnitSnapshot
    {
        public int Id;
        public bool IsPlayer;
        public int Slot;
        public string DefId = "";
        public string Name = "";
        public int Level = 1;
        public float MaxHp;
        public float Attack;
        public float Defense;
        public float Speed;
        public int Power;
        public Aspect Aspect = Aspect.Ember;
        public HeroRole Role = HeroRole.Striker;
        public Rarity Rarity = Rarity.Common;
        public bool IsBoss;
        public VisualDef Visual = new VisualDef();
        public string ActiveAbilityName = "";
        public int EnergyMax = 100;
        public int StartEnergy;
    }

    public sealed class BattleResult
    {
        public bool Victory;
        public bool TimedOut;
        public int Rounds;
        public int Seed;
        public string StageId = "";
        public float PlayerHpRatio;
        public float EnemyHpRatio;
        public readonly List<BattleEvent> Events = new List<BattleEvent>();
        public readonly List<UnitSnapshot> Units = new List<UnitSnapshot>();

        public UnitSnapshot FindUnit(int id)
        {
            for (int i = 0; i < Units.Count; i++) if (Units[i].Id == id) return Units[i];
            return null;
        }
    }
}
