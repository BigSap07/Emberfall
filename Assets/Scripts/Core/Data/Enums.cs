using System;

namespace Emberfall.Core.Data
{
    /// <summary>Elemental identity of a unit. Drives the counter chart and colour identity.</summary>
    public enum Aspect
    {
        Ember,   // fire / aggression
        Tide,    // water / control
        Verdant, // growth / sustain
        Gale,    // wind / speed
        Umbra,   // shadow
        Lumen    // light
    }

    /// <summary>Combat archetype. Purely descriptive - behaviour comes from the ability data.</summary>
    public enum HeroRole
    {
        Bulwark, // tank
        Striker, // single target damage
        Breaker, // area damage
        Mender,  // healer
        Warden,  // support
        Stalker, // assassin
        Ranger   // ranged / mixed single-target and line damage
    }

    public enum AbilityKind
    {
        BasicAttack,
        Active,
        Passive
    }

    public enum TargetMode
    {
        Self,
        LowestHpEnemy,
        HighestHpEnemy,
        HighestAttackEnemy,
        FrontEnemy,
        RandomEnemy,
        AllEnemies,
        LowestHpAlly,
        AllAllies,
        RandomAlly,
        LastAttacker,
        LastTarget
    }

    public enum EffectKind
    {
        Damage,
        Heal,
        Shield,
        ApplyStatus,
        Cleanse,
        EnergyGain,
        Dispel,
        SwapPositions,
        GainResolve
    }

    public enum StatusKind
    {
        None,
        AttackUp,
        AttackDown,
        DefenseUp,
        DefenseDown,
        Burn,
        Regen,
        Shield,
        Taunt,
        Focus,   // increased critical chance
        Slow,    // reduced speed
        Silence, // blocks the Active on the holder's next turn; a Basic Attack resolves instead
        Stun,    // skips the holder's entire next turn - no Basic Attack, no Active
        Stealth, // inert placeholder — no targeting-visibility logic yet
        Resolve  // generic personal stacking counter - see EffectKind.GainResolve
    }

    public enum PassiveTrigger
    {
        None,
        BattleStart,
        TurnStart,
        AfterBasicAttack,
        OnCriticalHit,
        OnKill,
        OnHpBelowThreshold,
        OnDamaged,
        OnBlock
    }

    /// <summary>
    /// One kind of player activity a live event can award points for. Deliberately closed and
    /// small today, but the events engine never switches on this enum anywhere - GameSession.
    /// RecordEventActivity walks every active EventDef and compares its own Activity field against
    /// the kind passed in, so adding a member here is never itself a behaviour change. What makes a
    /// new kind actually earn points is one call to RecordEventActivity at that activity's own
    /// natural call site - e.g. StageCleared would go inside GameSession.GrantBattleResult - not a
    /// new case anywhere in the engine. Neither member below needed a CombatSimulation change to
    /// wire, because neither activity happens inside a battle; a future kind that does would be the
    /// first one to test that promise.
    /// </summary>
    public enum EventActivityKind
    {
        /// <summary>One completed summon, successful or a duplicate - GameSession.Summon.</summary>
        SummonPerformed,

        /// <summary>Relic Dust spent, amount-scaled - GameSession.SpendRelicDust, the one choke
        /// point every Dust-spending path (today: GameSession.BuyRelic) routes through.</summary>
        RelicDustSpent
    }

    /// <summary>
    /// A relic's grade, per DESIGN-RELICS "The word is grade, and it is not tier or rarity" -
    /// with a rename layered on top of that same axis, not a new axis.
    ///
    /// Deliberately NOT called tier or rarity, because both words are already taken and either
    /// would silently overload one: "tier" is a relic's upgrade level (OwnedRelic.Tier,
    /// BalanceConfig.RelicMaxTier), and "rarity" is a hero's star rarity (StatMath.StarMultiplier,
    /// an int, not an enum - hero rarity has no "Legendary" value to collide with). Checked before
    /// this rename: no shared Rarity-style enum exists between relics and heroes, so no separate
    /// RelicTier type was needed - this enum was renamed in place instead.
    ///
    /// The two members are a straight relabel of what used to be Common and Legendary, plus one
    /// new value above them. What was Common is now LEGENDARY; what was Legendary (the two
    /// hook-based relics) is now MYTHIC, and Mythic additionally gained a stat block on top of its
    /// hook - the one shape change that came with the rename, not a pure label swap. A relic
    /// therefore has a grade and a tier and they are orthogonal - either grade upgrades from tier 1
    /// to tier 2 exactly the same way.
    /// </summary>
    public enum RelicGrade
    {
        /// <summary>
        /// A bundle of flat and percentage stats, resolved entirely in StatMath before the
        /// simulation starts. Carries RelicStatBonus entries and Hook is always None: this grade
        /// never enters the effect pipeline, so it cannot contest a hook with an aspect or with a
        /// mythic relic. Numerous, cheap to author, aspect-neutral and universal. This is what the
        /// grade named Common was before the rename - the eight starting stat bundles are
        /// unchanged in every value, only the label moved.
        /// </summary>
        Legendary,

        /// <summary>
        /// Rare, rule-breaking, and NOT number-free any more - that is what promotion to this
        /// grade changed. Carries a Hook, exactly as the grade named Legendary did before the
        /// rename, and the rule-changing half is held to DESIGN-RELICS' original founding standard
        /// without exception: the hook changes a rule in the effect pipeline and states itself as
        /// one sentence with no number in it, in Rule. What is new is that a mythic relic ALSO
        /// carries RelicStatBonus entries on top of that hook - a stat block stacked onto an
        /// unchanged unique passive, not a replacement for either half. The no-numbers guarantee
        /// therefore now applies to Rule specifically, not to the relic as a whole.
        /// </summary>
        Mythic
    }

    /// <summary>
    /// Which rule in the effect pipeline a MYTHIC relic bends. One relic, one hook.
    ///
    /// The "a relic changes a RULE, never a number" constraint that shapes this enum is the mythic
    /// grade's bar specifically, not a blanket rule for all relics - DESIGN-RELICS withdrew the
    /// blanket version on 2026-08-26 when the stat-bundle grade was introduced. It is undiminished
    /// here: there is still deliberately no "add N%" member, because a relic that wants one belongs
    /// in RelicStatBonus, which a mythic relic can also carry but this enum never reaches.
    ///
    /// Both members below are hooks no aspect holds. DESIGN-RELICS "Hook contention: aspects win
    /// by default" - Tide owns the AddEnergy overflow redirect, Verdant owns the ApplyHealValue
    /// overflow banking, Ember owns Burn stacking, Orrin is Lumen - and a relic hook must find one
    /// none of them claims rather than compete for one. That conflict is unchanged and out of
    /// scope for the grade rename: nothing here reopens it.
    /// </summary>
    public enum RelicHook
    {
        None,

        /// <summary>
        /// The Unbroken Ledger. A harmful effect from the wearer passes through
        /// ResolveLandingChance with the target's Effect Resist forced to zero.
        ///
        /// Not "hook the chance gate": the gate stopped being _rng.Chance(effect.Chance) when
        /// Effect Hit and Effect Resist shipped. It is the landing chance that gets rebuilt, and
        /// that arithmetic is claimed by the stat layer rather than by any aspect, which is what
        /// makes it available. Control Immunity is deliberately NOT bypassed - it is a separate,
        /// narrower layer that only ever touches Silence and Stun, and erasing both would make
        /// hard control unanswerable by any defensive stat in the game.
        /// </summary>
        BypassEffectResist,

        /// <summary>
        /// Mirrored Wound. When the wearer takes damage, a fraction of it returns to whoever dealt
        /// it. The fraction lives in balance.json (MirroredWoundReflectFraction), never in the
        /// relic's text, exactly as emberBurnStackCap and galeExtraTurnsPerRound do for aspects -
        /// which is how a legendary relic states itself with no number and still has a magnitude.
        ///
        /// Shares PassiveTrigger.OnDamaged's dispatch point but not its mechanism, and that trigger
        /// is NOT unused: tideturned_passive ("Backwash") already listens on it in enemies.json.
        /// The reflect is built to coexist with it rather than to assume an empty trigger.
        /// </summary>
        ReflectDamage
    }

    /// <summary>
    /// How one RelicStatBonus combines with the stat it names. Which mode a stat takes is not one
    /// answer, and DESIGN-RELICS decides it per stat rather than leaving it to the content author:
    /// relic stat bonuses use one "%" sign for two different operations because the stats they
    /// touch are stored in two different shapes. The rule is per-stat, not per-grade, so it applies
    /// identically whether the bonus sits on a legendary stat bundle or stacked onto a mythic hook.
    /// </summary>
    public enum RelicStatMode
    {
        /// <summary>
        /// Attack, Defense, MaxHp, Speed. Large scaled numbers built off BaseAttack * scale *
        /// rarityMultiplier, so "+14% Attack" means Attack *= 1.14.
        /// </summary>
        Multiply,

        /// <summary>
        /// CritChance, CritDamage, EffectHit, EffectResist. Stored as small fractions or as a
        /// multiplier and deliberately never scaled by level or star rarity, so multiplying them
        /// does not work: a multiplicative +12% on a 0.08 crit chance is +0.0096, which is nothing,
        /// and on Kalyx's 0.00 Effect Hit it is exactly zero. So "+12% Crit Chance" means
        /// CritChance += 0.12, and "+20% Crit Damage" means CritDamage += 0.20 - 1.5 becomes 1.7.
        /// </summary>
        Add
    }

    /// <summary>
    /// Which UnitStats field a RelicStatBonus moves. Deliberately a closed enum rather than a
    /// string field name: relic stat bonuses add no new stat, every member below already exists on
    /// UnitStats and is already read by the simulation, and a typo'd stat name should not parse.
    /// </summary>
    public enum RelicStat
    {
        MaxHp,
        Attack,
        Defense,
        Speed,
        CritChance,
        CritDamage,
        EffectHit,
        EffectResist
    }

    /// <summary>
    /// One of the six equipment slots a hero carries (DESIGN-ARMOR section 3). The slot decides
    /// the piece's primary stat outright - see ArmorInfo.PrimaryStat - so a Weapon is always an
    /// Attack piece and only its magnitude and its substats vary.
    ///
    /// The chest slot is PLATE, not Armor. One word naming both the system and one of its slots is
    /// the collision DESIGN-BUILD-ORDER already carries for Forge (task 6's idle-income mechanic),
    /// and reproducing it inside the system commissioned to avoid it would be worse than an odd
    /// slot name.
    ///
    /// The two accessory slots are deliberately a hard A/B pair rather than "two accessory slots":
    /// each guarantees its own primary (Crit Chance, Crit Damage), so a hero cannot wear two Crit
    /// Chance accessories, and every slot in the enum has exactly one answer to "what does this
    /// guarantee".
    /// </summary>
    public enum ArmorSlot
    {
        Weapon,
        Helm,
        Plate,
        Boots,
        AccessoryA,
        AccessoryB
    }

    /// <summary>
    /// An armor piece's quality rung (DESIGN-ARMOR section 4). It gates how many substats roll
    /// alongside the guaranteed primary, and scales the primary itself. It never changes WHICH
    /// stats can roll.
    ///
    /// Deliberately NOT called rarity, grade or tier, and its rungs deliberately avoid Legendary
    /// and Mythic: "grade" and both of those labels belong to RelicGrade, "tier" belongs to
    /// OwnedRelic.Tier, and hero rarity is HeroDef.Stars (an int, which is why hero rarity has no
    /// enum member to collide with - see RelicGrade's own note on that check). "Common" is
    /// technically free but is the pre-rename label of RelicGrade.Legendary, so reusing it would
    /// point a reader at a word that moved. Temper is new to the project and pairs with the Forge.
    ///
    /// Fixed at the moment a piece drops. There is no upgrade path in this pass - a better piece is
    /// a better drop, not a fed one.
    /// </summary>
    public enum ArmorTemper
    {
        Rough,
        Honed,
        Gilded,
        Radiant
    }

    /// <summary>
    /// Which UnitStats field an armor roll moves. Closed, like RelicStat, and for the same reason:
    /// armor adds no new stat, every member below already exists on UnitStats and is already read
    /// by the simulation, and a typo'd stat name should not parse.
    ///
    /// This is a SEPARATE enum from RelicStat rather than a reuse of it, per DESIGN-ARMOR section 9.
    /// RelicStat has eight members and reaches none of the six stats at the end of this list;
    /// extending it to fourteen would put armor's pool inside the enum ContentDatabase.ParseRelics
    /// validates relic content against, which means an armor stat could break relic loading. The
    /// two systems share the MECHANISM (StatMath folding a bonus list into an already-built
    /// UnitStats) and share no type.
    ///
    /// The last six members are the point of the whole system: they are live in CombatSimulation,
    /// each has its own harness check, and before armor no relic and no player-reachable path could
    /// grant any of them.
    /// </summary>
    public enum ArmorStat
    {
        MaxHp,
        Attack,
        Defense,
        Speed,
        CritChance,
        CritDamage,
        EffectHit,
        EffectResist,
        SkillDamage,
        Block,
        ArmorBreak,
        ControlImmunity,
        DamageReduction,
        HolyDamage
    }

    /// <summary>
    /// How one armor roll combines with the stat it names. The rule is per STAT, not per system:
    /// it is a fact about how UnitStats stores that stat, so armor and relics necessarily agree on
    /// it and RelicStatMode's own reasoning applies verbatim.
    ///
    /// Multiply: MaxHp, Attack, Defense, Speed - large numbers built off base * scale * starMult,
    /// so "+14% Attack" means Attack *= 1.14.
    /// Add: everything else - small fractions (or a multiplier, for Crit Damage) that are never
    /// scaled by level or star rarity, where multiplying does nothing. A multiplicative +12% on a
    /// 0.15 Crit Chance is +0.018, and on a 0.00 Block it is exactly zero.
    ///
    /// Holy Damage is the one member of the Add family that is not a fraction at all - it is flat
    /// damage in the same units as a damage number. See ArmorInfo.IsPercentStat, which is what the
    /// UI reads to decide whether to print a "%" sign.
    /// </summary>
    public enum ArmorStatMode
    {
        Multiply,
        Add
    }

    /// <summary>
    /// Display and rule helpers for the armor types above. The slot-to-primary table lives here
    /// rather than in content because it is the guarantee, not a tuning value: DESIGN-ARMOR section
    /// 3 makes the primary a property of the slot, so a Weapon that granted Defense would not be a
    /// mistuned drop, it would be a broken one.
    /// </summary>
    public static class ArmorInfo
    {
        /// <summary>Every slot, in display order. One array so the UI, the roller, the profile and
        /// the save all iterate the same six in the same order rather than each writing their own
        /// loop - the save in particular reads a hero's slots positionally.</summary>
        public static readonly ArmorSlot[] AllSlots =
        {
            ArmorSlot.Weapon, ArmorSlot.Helm, ArmorSlot.Plate,
            ArmorSlot.Boots, ArmorSlot.AccessoryA, ArmorSlot.AccessoryB
        };

        /// <summary>The stat a slot guarantees. Total over ArmorSlot - every member has an answer,
        /// and the trailing return exists only to satisfy the compiler, not as a real fallback.</summary>
        public static ArmorStat PrimaryStat(ArmorSlot slot)
        {
            switch (slot)
            {
                case ArmorSlot.Weapon: return ArmorStat.Attack;
                case ArmorSlot.Helm: return ArmorStat.MaxHp;
                case ArmorSlot.Plate: return ArmorStat.Defense;
                case ArmorSlot.Boots: return ArmorStat.Speed;
                case ArmorSlot.AccessoryA: return ArmorStat.CritChance;
                case ArmorSlot.AccessoryB: return ArmorStat.CritDamage;
            }
            return ArmorStat.Attack;
        }

        /// <summary>
        /// The arithmetic mode a stat takes, derived rather than authored. RelicStatBonus stores
        /// its mode in the data so a content author can see the two rules explicitly; armor derives
        /// it, because armor rolls are GENERATED rather than authored and a generator that could
        /// emit the wrong mode is a generator with a bug in it. Content still names a mode on its
        /// roll ranges (armor.json) for readability, and the roller passes it through here rather
        /// than trusting it - see ArmorRoller.
        /// </summary>
        public static ArmorStatMode ModeFor(ArmorStat stat)
        {
            switch (stat)
            {
                case ArmorStat.MaxHp:
                case ArmorStat.Attack:
                case ArmorStat.Defense:
                case ArmorStat.Speed:
                    return ArmorStatMode.Multiply;
            }
            return ArmorStatMode.Add;
        }

        /// <summary>
        /// Whether a stat's value reads as a percentage on a card. False for Holy Damage alone: it
        /// is flat damage in damage units, so "+40 Holy Damage" is right and the "+4000% Holy
        /// Damage" that printing every Add stat as a percentage produces is not.
        /// </summary>
        public static bool IsPercentStat(ArmorStat stat)
        {
            return stat != ArmorStat.HolyDamage;
        }

        /// <summary>How many substats a temper rolls. The ladder itself, in one place.</summary>
        public static int SubStatCount(ArmorTemper temper)
        {
            switch (temper)
            {
                case ArmorTemper.Rough: return 1;
                case ArmorTemper.Honed: return 2;
                case ArmorTemper.Gilded: return 3;
                case ArmorTemper.Radiant: return 4;
            }
            return 1;
        }

        public static string SlotLabel(ArmorSlot slot)
        {
            switch (slot)
            {
                case ArmorSlot.Weapon: return "Weapon";
                case ArmorSlot.Helm: return "Helm";
                case ArmorSlot.Plate: return "Plate";
                case ArmorSlot.Boots: return "Boots";
                case ArmorSlot.AccessoryA: return "Accessory I";
                case ArmorSlot.AccessoryB: return "Accessory II";
            }
            return slot.ToString();
        }

        /// <summary>Display spelling of an ArmorStat. "Max HP" and "Crit Chance", not the enum
        /// name - the same job RelicDef.StatLabel does for RelicStat, over a wider pool.</summary>
        public static string StatLabel(ArmorStat stat)
        {
            switch (stat)
            {
                case ArmorStat.MaxHp: return "Max HP";
                case ArmorStat.Attack: return "Attack";
                case ArmorStat.Defense: return "Defense";
                case ArmorStat.Speed: return "Speed";
                case ArmorStat.CritChance: return "Crit Chance";
                case ArmorStat.CritDamage: return "Crit Damage";
                case ArmorStat.EffectHit: return "Effect Hit";
                case ArmorStat.EffectResist: return "Effect Resist";
                case ArmorStat.SkillDamage: return "Skill Damage";
                case ArmorStat.Block: return "Block";
                case ArmorStat.ArmorBreak: return "Armor Break";
                case ArmorStat.ControlImmunity: return "Control Immunity";
                case ArmorStat.DamageReduction: return "Damage Reduction";
                case ArmorStat.HolyDamage: return "Holy Damage";
            }
            return stat.ToString();
        }

        public static string TemperLabel(ArmorTemper temper)
        {
            return temper.ToString().ToUpperInvariant();
        }

        /// <summary>
        /// The highest temper a stage-clear drop may ever produce. A CODE invariant, not a content
        /// default: ArmorRoller.RollTemper filters candidates against this constant before it even
        /// looks at armor.json's drop weights, so a content edit that gives Gilded or Radiant a
        /// nonzero dropWeight cannot make either reachable by RNG. DESIGN-ARMOR section 8 - Gilded
        /// and Radiant exist only through GameSession.CombineArmor.
        ///
        /// Declared as the rung itself (Honed), not as a count or an index, so the comparison at
        /// the call site reads as what it means: "this temper is no higher than the ceiling."
        /// </summary>
        public static readonly ArmorTemper MaxDropTemper = ArmorTemper.Honed;

        /// <summary>
        /// The rung directly above this one, or null at the top of the ladder (Radiant) - the one
        /// place "one tier higher" needs an answer outside a drop. GameSession.CombineArmor is the
        /// only caller: a Combine's output temper IS this, by DESIGN-ARMOR's "cannot skip a tier"
        /// rule, so there is nowhere else in the game that needs to ask "what comes next."
        ///
        /// Safe to express as a bare +1 on the underlying int because the four rungs are declared
        /// above in ascending, contiguous order - Radiant is excluded first specifically so that
        /// invariant only has to hold for the three casts this method can actually reach.
        /// </summary>
        public static ArmorTemper? NextTemper(ArmorTemper temper)
        {
            if (temper == ArmorTemper.Radiant) return null;
            return (ArmorTemper)((int)temper + 1);
        }
    }

    public static class AspectChart
    {
        // Narrowed from 1.20/0.85 per DESIGN-ASPECTS section 4: a 41% swing between an advantaged
        // and a disadvantaged attacker out-argues every aspect identity, which forces one correct
        // counter-colour per stage and turns team building into a compliance check. At 1.10/0.92
        // the wheel is a tiebreak between two lines you'd already be happy with, which is what a
        // secondary layer is. Nothing reads these as literals - see Debrief.AspectFactorThreshold
        // and DevHarness CheckDebriefAspect* for the two places that used to.
        public const float Advantage = 1.10f;
        public const float Disadvantage = 0.92f;

        /// <summary>Ember &gt; Verdant &gt; Tide &gt; Gale &gt; Ember. Lumen and Umbra counter each other.</summary>
        public static float Multiplier(Aspect attacker, Aspect defender)
        {
            if (Beats(attacker, defender)) return Advantage;
            if (Beats(defender, attacker)) return Disadvantage;
            return 1f;
        }

        public static bool Beats(Aspect a, Aspect b)
        {
            switch (a)
            {
                case Aspect.Ember: return b == Aspect.Verdant;
                case Aspect.Verdant: return b == Aspect.Tide;
                case Aspect.Tide: return b == Aspect.Gale;
                case Aspect.Gale: return b == Aspect.Ember;
                case Aspect.Lumen: return b == Aspect.Umbra;
                case Aspect.Umbra: return b == Aspect.Lumen;
            }
            return false;
        }

        public static string DisplayName(Aspect a)
        {
            return a.ToString().ToUpperInvariant();
        }
    }

    public static class StatusInfo
    {
        public static bool IsHarmful(StatusKind kind)
        {
            return kind == StatusKind.AttackDown
                || kind == StatusKind.DefenseDown
                || kind == StatusKind.Burn
                || kind == StatusKind.Slow
                || kind == StatusKind.Silence
                || kind == StatusKind.Stun;
        }

        public static string DisplayName(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.AttackUp: return "Attack Up";
                case StatusKind.AttackDown: return "Attack Down";
                case StatusKind.DefenseUp: return "Guard Up";
                case StatusKind.DefenseDown: return "Guard Down";
                case StatusKind.Burn: return "Cinder";
                case StatusKind.Regen: return "Renewal";
                case StatusKind.Shield: return "Ward";
                case StatusKind.Taunt: return "Provoke";
                case StatusKind.Focus: return "Focus";
                case StatusKind.Slow: return "Mire";
                case StatusKind.Silence: return "Silence";
                case StatusKind.Stun: return "Stun";
                case StatusKind.Stealth: return "Stealth";
                case StatusKind.Resolve: return "Resolve";
            }
            return kind.ToString();
        }

        /// <summary>Short tag drawn on the unit health bar.</summary>
        public static string ShortTag(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.AttackUp: return "ATK+";
                case StatusKind.AttackDown: return "ATK-";
                case StatusKind.DefenseUp: return "DEF+";
                case StatusKind.DefenseDown: return "DEF-";
                case StatusKind.Burn: return "BRN";
                case StatusKind.Regen: return "REG";
                case StatusKind.Shield: return "WRD";
                case StatusKind.Taunt: return "PRV";
                case StatusKind.Focus: return "FOC";
                case StatusKind.Slow: return "MIR";
                case StatusKind.Silence: return "SIL";
                case StatusKind.Stun: return "STN";
                case StatusKind.Stealth: return "STH";
                case StatusKind.Resolve: return "RSV";
            }
            return "?";
        }
    }

    public static class EnumUtil
    {
        public static T Parse<T>(string text, T fallback) where T : struct
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            try
            {
                return (T)Enum.Parse(typeof(T), text.Trim(), true);
            }
            catch (Exception)
            {
                return fallback;
            }
        }
    }
}
