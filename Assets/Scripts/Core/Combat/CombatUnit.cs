using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;

namespace Emberfall.Core.Combat
{
    public sealed class StatusInstance
    {
        public StatusKind Kind;
        public int RoundsLeft;
        /// <summary>Fraction for stat modifiers, flat per-tick value for Burn / Regen.</summary>
        public float Magnitude;
        public int SourceId = -1;
    }

    /// <summary>Mutable battle-time state for one combatant.</summary>
    public sealed class CombatUnit
    {
        public int Id;
        public bool IsPlayer;
        public int Slot;
        public string DefId = "";
        public string Name = "";
        public int Level = 1;
        public Aspect Aspect = Aspect.Ember;
        public HeroRole Role = HeroRole.Striker;
        public Rarity Rarity = Rarity.Common;
        public bool IsBoss;
        public VisualDef Visual = new VisualDef();

        public float MaxHp;
        public float Hp;
        public float Attack;
        public float Defense;
        public float Speed;
        public float CritChance;
        public float CritDamage;
        public float EffectHit;
        public float EffectResist;

        public int Energy;
        public int EnergyMax = 100;
        public float Shield;
        public bool Alive = true;

        /// <summary>Re-rolled once per round by the sim; keeps turn order from being the same every fight.</summary>
        public float RoundSpeedJitter;

        public AbilityDef Basic;
        public AbilityDef Active;
        public PassiveDef Passive;
        public bool PassiveFired;

        public int LastAttackerId = -1;
        public int LastTargetId = -1;

        public readonly List<StatusInstance> Statuses = new List<StatusInstance>();

        public float HpRatio
        {
            get { return MaxHp <= 0f ? 0f : Hp / MaxHp; }
        }

        // ---------- derived combat values ----------

        public float EffectiveAttack
        {
            get
            {
                float m = 1f + SumMagnitude(StatusKind.AttackUp) - SumMagnitude(StatusKind.AttackDown);
                if (m < 0.2f) m = 0.2f;
                return Attack * m;
            }
        }

        public float EffectiveDefense
        {
            get
            {
                float m = 1f + SumMagnitude(StatusKind.DefenseUp) - SumMagnitude(StatusKind.DefenseDown);
                if (m < 0.2f) m = 0.2f;
                return Defense * m;
            }
        }

        public float EffectiveSpeed
        {
            get
            {
                float m = 1f - SumMagnitude(StatusKind.Slow);
                if (m < 0.3f) m = 0.3f;
                return Speed * m;
            }
        }

        /// <summary>EffectiveSpeed plus this round's jitter - what turn order is actually sorted by.</summary>
        public float TurnOrderSpeed
        {
            get { return EffectiveSpeed * (1f + RoundSpeedJitter); }
        }

        public float EffectiveCritChance
        {
            get
            {
                float c = CritChance + SumMagnitude(StatusKind.Focus);
                if (c < 0f) c = 0f;
                if (c > 0.95f) c = 0.95f;
                return c;
            }
        }

        /// <summary>No status modifies this yet - shaped like EffectiveAttack for when one does.</summary>
        public float EffectiveEffectHit
        {
            get { return EffectHit; }
        }

        /// <summary>No status modifies this yet - shaped like EffectiveAttack for when one does.</summary>
        public float EffectiveEffectResist
        {
            get { return EffectResist; }
        }

        public bool HasTaunt
        {
            get { return Alive && Has(StatusKind.Taunt); }
        }

        public float SumMagnitude(StatusKind kind)
        {
            float total = 0f;
            for (int i = 0; i < Statuses.Count; i++) if (Statuses[i].Kind == kind) total += Statuses[i].Magnitude;
            return total;
        }

        public bool Has(StatusKind kind)
        {
            for (int i = 0; i < Statuses.Count; i++) if (Statuses[i].Kind == kind) return true;
            return false;
        }

        public void AddOrRefresh(StatusKind kind, int rounds, float magnitude, int sourceId)
        {
            for (int i = 0; i < Statuses.Count; i++)
            {
                StatusInstance s = Statuses[i];
                if (s.Kind != kind) continue;
                // refresh: keep the stronger magnitude and the longer duration
                if (magnitude > s.Magnitude) s.Magnitude = magnitude;
                if (rounds > s.RoundsLeft) s.RoundsLeft = rounds;
                s.SourceId = sourceId;
                return;
            }
            StatusInstance added = new StatusInstance();
            added.Kind = kind;
            added.RoundsLeft = rounds;
            added.Magnitude = magnitude;
            added.SourceId = sourceId;
            Statuses.Add(added);
        }

        public void Remove(StatusKind kind)
        {
            for (int i = Statuses.Count - 1; i >= 0; i--) if (Statuses[i].Kind == kind) Statuses.RemoveAt(i);
        }

        public UnitSnapshot ToSnapshot()
        {
            UnitSnapshot s = new UnitSnapshot();
            s.Id = Id;
            s.IsPlayer = IsPlayer;
            s.Slot = Slot;
            s.DefId = DefId;
            s.Name = Name;
            s.Level = Level;
            s.MaxHp = MaxHp;
            s.Attack = Attack;
            s.Defense = Defense;
            s.Speed = Speed;
            s.Aspect = Aspect;
            s.Role = Role;
            s.Rarity = Rarity;
            s.IsBoss = IsBoss;
            s.Visual = Visual;
            s.EnergyMax = EnergyMax;
            s.StartEnergy = Energy;
            s.ActiveAbilityName = Active != null ? Active.Name : "";
            UnitStats stats = new UnitStats();
            stats.MaxHp = MaxHp;
            stats.Attack = Attack;
            stats.Defense = Defense;
            stats.Speed = Speed;
            stats.CritChance = CritChance;
            stats.CritDamage = CritDamage;
            s.Power = stats.Power;
            return s;
        }

        public static CombatUnit FromDef(UnitDef def, UnitStats stats, int level, bool isPlayer, int slot, int id, int energyMax)
        {
            CombatUnit u = new CombatUnit();
            u.Id = id;
            u.IsPlayer = isPlayer;
            u.Slot = slot;
            u.DefId = def.Id;
            u.Name = def.Name;
            u.Level = level;
            u.Aspect = def.Aspect;
            u.Visual = def.Visual;
            u.MaxHp = stats.MaxHp;
            u.Hp = stats.MaxHp;
            u.Attack = stats.Attack;
            u.Defense = stats.Defense;
            u.Speed = stats.Speed;
            u.CritChance = stats.CritChance;
            u.CritDamage = stats.CritDamage;
            u.EffectHit = stats.EffectHit;
            u.EffectResist = stats.EffectResist;
            u.EnergyMax = energyMax;
            u.Energy = Math.Min(energyMax, def.StartEnergy);
            u.Basic = def.BasicAttack != null ? def.BasicAttack : UnitDef.DefaultBasicAttack();
            u.Active = def.Active;
            u.Passive = def.Passive;

            HeroDef hero = def as HeroDef;
            if (hero != null)
            {
                u.Role = hero.Role;
                u.Rarity = hero.Rarity;
            }
            EnemyDef enemy = def as EnemyDef;
            if (enemy != null)
            {
                u.IsBoss = enemy.Kind == "boss";
                u.Role = enemy.Kind == "boss" ? HeroRole.Bulwark : HeroRole.Striker;
            }
            return u;
        }
    }
}
