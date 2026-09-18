#!/usr/bin/env python3
"""Author full kits for 5 batch-three heroes into heroes.json."""
import json, sys

HEROES_PATH = "Assets/Resources/Content/heroes.json"

def main():
    with open(HEROES_PATH, "r", encoding="utf-8") as f:
        data = json.load(f)

    heroes = data["heroes"]
    lookup = {h["id"]: i for i, h in enumerate(heroes)}

    kits = build_kits()
    for hero_id, kit in kits.items():
        idx = lookup.get(hero_id)
        if idx is None:
            print(f"ERROR: {hero_id} not found"); sys.exit(1)
        hero = heroes[idx]
        hero["basic"]    = kit["basic"]
        hero["active"]   = kit["active"]
        hero["passive"]  = kit["passive"]
        hero["passive1"] = kit["passive1"]
        hero["passive2"] = kit["passive2"]
        hero["talents"]  = kit["talents"]
        print(f"  authored {hero_id}")

    with open(HEROES_PATH, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")
    print("Done.")


def build_kits():
    return {
        "caldus_the_appointed_guard":  caldus(),
        "elyra_the_patient_warden":    elyra(),
        "rheia_the_revision_scribe":   rheia(),
        "mireth_the_erased_name":      mireth(),
        "arden_the_precise_measure":   arden(),
    }


# ================================================================ CALDUS
def caldus():
    basic = {
        "id": "caldus_answer_the_blade",
        "name": "Answer the Blade",
        "effects": [
            {"kind": "Damage", "target": "FrontEnemy", "power": 1.15},
            # GAP: +5 Energy if target has Mark (Self EnergyGain can't condition on enemy state)
        ]
    }
    active = {
        "id": "caldus_declare_the_threat",
        "name": "Declare the Threat",
        "energyCost": 100,
        "effects": [
            {"kind": "Damage",      "target": "AllEnemies",         "power": 1.80},
            {"kind": "ApplyStatus", "target": "HighestAttackEnemy", "status": "Mark", "duration": 2},
        ]
    }
    passive = {
        "id": "caldus_defined_duty_stats",
        "name": "Defined Duty",
        "trigger": "BattleStart",
        "effects": [
            {"kind": "ApplyStatus", "target": "Self", "status": "MaxHpUp",   "magnitude": 0.08, "duration": 99},
            {"kind": "ApplyStatus", "target": "Self", "status": "DefenseUp", "magnitude": 0.08, "duration": 99},
        ]
    }
    # OnDamaged: counterattack DT for 75%, 1/round/target, PerStack Resolve +10%, consume 1
    # GAP: GainResolve after counterattack (Self in TriggerPassive)
    passive1 = {
        "id": "caldus_defined_duty",
        "name": "Defined Duty",
        "trigger": "OnDamaged",
        "oncePerBattle": False,
        "perTargetLimit": 1,
        "perTargetPerRound": True,
        "effects": [
            {"kind": "Damage", "target": "LastAttacker", "power": 0.75,
             "condition": "HasMyMark", "conditionOn": "Target",
             "perStackStatus": "Resolve", "perStackOn": "Caster", "perStack": 0.10,
             "consumesResolve": True, "consumeStacks": 1},
        ]
    }
    # OnEnemyDamagedAlly: DT attacks ally → 45% damage + 8 Energy + Shield 5% MaxHP 2rnd
    # GAP: P2 shield-break counterattack (no OnAllyShieldBreak)
    passive2 = {
        "id": "caldus_no_uncertain_ground",
        "name": "No Uncertain Ground",
        "trigger": "OnEnemyDamagedAlly",
        "oncePerBattle": False,
        "effects": [
            {"kind": "Damage",     "target": "LastAttacker",    "power": 0.45,
             "condition": "HasMyMark", "conditionOn": "LastAttacker"},
            {"kind": "EnergyGain", "target": "Self",            "flat": 8,
             "condition": "HasMyMark", "conditionOn": "LastAttacker"},
            {"kind": "Shield",     "target": "BroadcastVictim", "selfHpRatio": 0.05, "duration": 2,
             "condition": "HasMyMark", "conditionOn": "LastAttacker"},
        ]
    }
    talents = {
        "talentI": {
            "id": "caldus_absolute_focus",
            "name": "Absolute Focus",
            "trigger": "None",
            "effects": [],
            "overrides": [
                {"ability": "active",   "effectIndex": 1, "field": "duration", "delta": 1},
                {"ability": "passive1", "effectIndex": 0, "field": "power",    "delta": 0.20},
            ]
        },
        "talentII": {
            "id": "caldus_guardian_oath",
            "name": "Guardian's Oath",
            "trigger": "None",
            "effects": [],
            "overrides": [
                {"ability": "passive2", "effectIndex": 2, "field": "selfHpRatio", "delta": 0.03},
                # GAP: heal 5% MaxHP when shield breaks (no OnAllyShieldBreak)
            ]
        },
        "talentIII": {
            "id": "caldus_final_verdict",
            "name": "Final Verdict",
            "trigger": "OnEnemyHpBelowThreshold",
            "oncePerBattle": True,
            "threshold": 0.30,
            "effects": [
                {"kind": "GainResolve", "target": "Self", "maxStacks": 3, "hits": 2, "duration": 99,
                 "condition": "HasMyMark", "conditionOn": "BroadcastSubject"},
            ]
        }
    }
    return dict(basic=basic, active=active, passive=passive,
                passive1=passive1, passive2=passive2, talents=talents)


# ================================================================ ELYRA
def elyra():
    basic = {
        "id": "elyra_patient_bloom",
        "name": "Patient Measure",
        "effects": [
            {"kind": "Damage",      "target": "HighestAttackEnemy", "power": 1.15},
            {"kind": "ApplyStatus", "target": "HighestAttackEnemy", "status": "Slow", "duration": 2, "bypassResist": True},
            # GAP: heal LowestHpAlly 55% if target already has Slow
        ]
    }
    active = {
        "id": "elyra_sanctuary_unbroken",
        "name": "Sanctuary Unbroken",
        "energyCost": 100,
        "effects": [
            {"kind": "Damage",      "target": "AllEnemies",         "power": 1.90},
            {"kind": "ApplyStatus", "target": "AllEnemies",         "status": "Slow",  "duration": 2, "bypassResist": True},
            {"kind": "ApplyStatus", "target": "HighestAttackEnemy", "status": "Taunt", "duration": 1},
        ]
    }
    passive = {
        "id": "elyra_slow_growth_stats",
        "name": "Slow Growth",
        "trigger": "BattleStart",
        "outputModStatus": "Resolve",
        "outputModPerStack": 0.05,
        "outputModScope": "HealingOnly",
        "effects": [
            {"kind": "ApplyStatus", "target": "Self", "status": "AttackUp",      "magnitude": 0.08, "duration": 99},
            {"kind": "ApplyStatus", "target": "Self", "status": "EffectHitUp",   "magnitude": 0.10, "duration": 99},
            {"kind": "ApplyStatus", "target": "Self", "status": "SkillDamageUp", "magnitude": 0.08, "duration": 99},
        ]
    }
    # P1 gain Patience — GAP: should only fire when basic target has Slow
    passive1 = {
        "id": "elyra_slow_growth",
        "name": "Slow Growth",
        "trigger": "AfterBasicAttack",
        "oncePerBattle": False,
        "effects": [
            {"kind": "GainResolve", "target": "Self", "maxStacks": 4, "duration": 99},
        ]
    }
    # P2 + Active "when Taunt ends" combined
    passive2 = {
        "id": "elyra_stillness_bears_fruit",
        "name": "Stillness Pays",
        "trigger": "OnAppliedStatusExpired",
        "triggerStatus": "Taunt",
        "triggerSourceIsSelf": True,
        "oncePerBattle": False,
        "effects": [
            {"kind": "Damage", "target": "BroadcastSubject", "power": 0.80},
            {"kind": "Heal",   "target": "LowestHpAlly",     "power": 1.00},
            {"kind": "Damage", "target": "BroadcastSubject", "power": 0.30,
             "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
             "conditionValue": 1, "conditionOn": "Caster"},
            {"kind": "Damage", "target": "BroadcastSubject", "power": 0.30,
             "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
             "conditionValue": 2, "conditionOn": "Caster"},
            {"kind": "Damage", "target": "BroadcastSubject", "power": 0.30,
             "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
             "conditionValue": 3, "conditionOn": "Caster"},
            {"kind": "Damage", "target": "BroadcastSubject", "power": 0.30,
             "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
             "conditionValue": 4, "conditionOn": "Caster"},
            {"kind": "Heal", "target": "LowestHpAlly", "power": 0.40,
             "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
             "conditionValue": 2, "conditionOn": "Caster"},
            {"kind": "Heal", "target": "LowestHpAlly", "power": 0.40,
             "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
             "conditionValue": 4, "conditionOn": "Caster"},
            {"kind": "EnergyGain", "target": "Self", "flat": 15,
             "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
             "conditionValue": 4, "conditionOn": "Caster"},
            {"kind": "RemoveStatus", "target": "Self", "status": "Resolve", "hits": 10},
        ]
    }
    talents = {
        "talentI": {
            "id": "elyra_deep_rest",
            "name": "Deep Rest",
            "trigger": "OnAppliedStatusExpired",
            "triggerStatus": "Slow",
            "triggerSourceIsSelf": True,
            "oncePerBattle": False,
            "effects": [
                {"kind": "EnergyGain", "target": "Self", "flat": 5},
            ],
            "overrides": [
                {"ability": "basic",  "effectIndex": 1, "field": "duration", "delta": 1},
                {"ability": "active", "effectIndex": 1, "field": "duration", "delta": 1},
            ]
        },
        "talentII": {
            "id": "elyra_sanctuary_spreads",
            "name": "Sanctuary Spreads",
            "trigger": "None",
            "effects": [],
            "overrides": [
                # GAP: can't add new effects (Slow + damage to lowest-HP enemy) via overrides
            ]
        },
        "talentIII": {
            "id": "elyra_harvest_of_patience",
            "name": "Calculated Patience",
            "trigger": "None",
            "effects": [],
            "overrides": [
                {"ability": "passive2", "effectIndex": 5, "field": "power", "delta": 1.00},
                # GAP: Shield to ally not expressible as override
            ]
        }
    }
    return dict(basic=basic, active=active, passive=passive,
                passive1=passive1, passive2=passive2, talents=talents)


# ================================================================ RHEIA
def rheia():
    basic = {
        "id": "rheia_shifting_script",
        "name": "Revision Script",
        "effects": [
            {"kind": "Damage", "target": "HighestAttackEnemy", "power": 1.15},
            {"kind": "ApplyStatus",  "target": "HighestAttackEnemy", "status": "DefenseDown", "duration": 2,
             "bypassResist": True, "condition": "HasStatus", "conditionStatus": "AttackUp", "conditionOn": "Target"},
            {"kind": "RemoveStatus", "target": "HighestAttackEnemy", "status": "AttackUp", "hits": 1},
        ]
    }
    active = {
        "id": "rheia_rewrite_the_outcome",
        "name": "Rewrite the Outcome",
        "energyCost": 100,
        "effects": [
            {"kind": "Damage",       "target": "AllEnemies", "power": 2.00},
            {"kind": "ApplyStatus",  "target": "AllEnemies", "status": "DefenseDown", "duration": 2, "bypassResist": True},
            {"kind": "ApplyStatus",  "target": "AllEnemies", "status": "AttackUp",    "duration": 2,
             "condition": "HasStatus", "conditionStatus": "AttackDown", "conditionOn": "Target"},
            {"kind": "Damage",       "target": "AllEnemies", "power": 0.45,
             "condition": "HasStatus", "conditionStatus": "AttackDown", "conditionOn": "Target"},
            {"kind": "RemoveStatus", "target": "AllEnemies", "status": "AttackDown",  "hits": 1},
        ]
    }
    passive = {
        "id": "rheia_adaptive_reasoning_stats",
        "name": "Applied Revision",
        "trigger": "BattleStart",
        "statModStatus": "Resolve",
        "statModPerStack": 0.03,
        "statModKind": "Speed",
        "effects": [
            {"kind": "ApplyStatus", "target": "Self", "status": "AttackUp",      "magnitude": 0.08, "duration": 99},
            {"kind": "ApplyStatus", "target": "Self", "status": "EffectHitUp",   "magnitude": 0.10, "duration": 99},
            {"kind": "ApplyStatus", "target": "Self", "status": "SkillDamageUp", "magnitude": 0.08, "duration": 99},
        ]
    }
    # OnEnemyBuffGained: gain Revision + Energy per conversion
    passive1 = {
        "id": "rheia_adaptive_reasoning",
        "name": "Applied Revision",
        "trigger": "OnEnemyBuffGained",
        "triggerSourceIsSelf": True,
        "oncePerBattle": False,
        "perTargetLimit": 4,
        "perTargetPerRound": True,
        "effects": [
            {"kind": "GainResolve", "target": "Self", "maxStacks": 4, "duration": 99},
            {"kind": "EnergyGain", "target": "Self", "flat": 5},
        ]
    }
    # P2: Second Draft — enemy with DefenseDown gains AttackUp → damage + energy
    passive2 = {
        "id": "rheia_second_direction",
        "name": "Second Draft",
        "trigger": "OnEnemyBuffGained",
        "triggerSourceIsSelf": True,
        "oncePerBattle": False,
        "perTargetLimit": 1,
        "perTargetPerRound": True,
        "effects": [
            {"kind": "Damage", "target": "BroadcastSubject", "power": 0.70,
             "condition": "HasStatus", "conditionStatus": "DefenseDown", "conditionOn": "BroadcastSubject"},
            {"kind": "EnergyGain", "target": "Self", "flat": 10,
             "condition": "HasStatus", "conditionStatus": "DefenseDown", "conditionOn": "BroadcastSubject"},
            {"kind": "Damage", "target": "BroadcastSubject", "power": 1.00,
             "condition": "HasStatus",          "conditionStatus": "DefenseDown",  "conditionOn": "BroadcastSubject",
             "condition2": "StatusStacksAtLeast","conditionStatus2": "Resolve", "conditionValue2": 4, "conditionOn2": "Caster"},
            {"kind": "RemoveStatus", "target": "Self", "status": "Resolve", "hits": 10,
             "condition": "HasStatus",          "conditionStatus": "DefenseDown",  "conditionOn": "BroadcastSubject",
             "condition2": "StatusStacksAtLeast","conditionStatus2": "Resolve", "conditionValue2": 4, "conditionOn2": "Caster"},
        ]
    }
    talents = {
        "talentI": {
            "id": "rheia_endless_revision",
            "name": "Endless Revision",
            "trigger": "None",
            "effects": [],
            "overrides": [
                {"ability": "passive1", "effectIndex": 1, "field": "flat",  "delta": -2},
                {"ability": "active",   "effectIndex": 3, "field": "power", "delta": 0.20},
            ]
        },
        "talentII": {
            "id": "rheia_counterrevision",
            "name": "Counterrevision",
            "trigger": "None",
            "effects": [],
            "overrides": [
                # GAP: conditional ApplyStatus (AttackDown/DefenseUp) not expressible as delta
            ]
        },
        "talentIII": {
            "id": "rheia_perfect_reversal",
            "name": "Perfect Reversal",
            "trigger": "None",
            "effects": [],
            "overrides": [
                {"ability": "passive2", "effectIndex": 2, "field": "power", "delta": 0.80},
                {"ability": "passive2", "effectIndex": 3, "field": "hits",  "delta": -10},
            ]
        }
    }
    return dict(basic=basic, active=active, passive=passive,
                passive1=passive1, passive2=passive2, talents=talents)


# ================================================================ MIRETH
def mireth():
    basic = {
        "id": "mireth_unwritten_line",
        "name": "Unwritten Line",
        "effects": [
            {"kind": "Damage", "target": "HighestAttackEnemy", "power": 1.15},
            {"kind": "ApplyStatus", "target": "HighestAttackEnemy", "status": "Mark", "duration": 2,
             "condition": "HasAnyBeneficialStatus", "conditionOn": "Target"},
            # GAP: +5 Energy if target has positive status
        ]
    }
    active = {
        "id": "mireth_remove_from_record",
        "name": "Remove From Record",
        "energyCost": 100,
        "effects": [
            {"kind": "Damage",      "target": "AllEnemies",         "power": 2.00},
            {"kind": "ApplyStatus", "target": "HighestAttackEnemy", "status": "Mark", "duration": 2},
            # GAP: "first positive status removed immediately" — _broadcastBuff exclusion
        ]
    }
    passive = {
        "id": "mireth_absence_has_weight_stats",
        "name": "Absence Has Weight",
        "trigger": "BattleStart",
        "effects": [
            {"kind": "ApplyStatus", "target": "Self", "status": "AttackUp",      "magnitude": 0.08, "duration": 99},
            {"kind": "ApplyStatus", "target": "Self", "status": "EffectHitUp",   "magnitude": 0.10, "duration": 99},
            {"kind": "ApplyStatus", "target": "Self", "status": "SkillDamageUp", "magnitude": 0.08, "duration": 99},
        ]
    }
    # P1: gain Absence (Resolve) when Mireth removes a status; +6% damage vs Focused per Absence
    passive1 = {
        "id": "mireth_absence_has_weight",
        "name": "Absence Has Weight",
        "trigger": "OnStatusRemoved",
        "oncePerBattle": False,
        "outputModStatus": "Resolve",
        "outputModPerStack": 0.06,
        "outputModScope": "AllDamage",
        "outputModMarkedOnly": True,
        "effects": [
            {"kind": "GainResolve", "target": "Self", "maxStacks": 3, "duration": 99},
        ]
    }
    # P2: OnEnemyBuffGained → damage + Pages (Resolve2) + at 3 Pages: burst + AttackDown
    passive2 = {
        "id": "mireth_the_missing_page",
        "name": "The Missing Page",
        "trigger": "OnEnemyBuffGained",
        "oncePerBattle": False,
        "limit": "OncePerRound",
        "effects": [
            {"kind": "Damage", "target": "BroadcastSubject", "power": 0.70,
             "condition": "HasMyMark", "conditionOn": "BroadcastSubject"},
            {"kind": "GainResolve", "target": "Self", "status": "Resolve2", "maxStacks": 3, "duration": 99,
             "condition": "HasMyMark", "conditionOn": "BroadcastSubject"},
            {"kind": "Damage", "target": "BroadcastSubject", "power": 1.20,
             "condition": "HasMyMark",           "conditionOn": "BroadcastSubject",
             "condition2": "StatusStacksAtLeast", "conditionStatus2": "Resolve2",
             "conditionValue2": 3, "conditionOn2": "Caster"},
            {"kind": "ApplyStatus", "target": "BroadcastSubject", "status": "AttackDown", "duration": 2,
             "bypassResist": True,
             "condition": "HasMyMark",           "conditionOn": "BroadcastSubject",
             "condition2": "StatusStacksAtLeast", "conditionStatus2": "Resolve2",
             "conditionValue2": 3, "conditionOn2": "Caster"},
            {"kind": "RemoveStatus", "target": "Self", "status": "Resolve2", "hits": 10,
             "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve2",
             "conditionValue": 3, "conditionOn": "Caster"},
        ]
    }
    talents = {
        "talentI": {
            "id": "mireth_deeper_erasure",
            "name": "Deeper Erasure",
            "trigger": "None",
            "effects": [],
            "overrides": [
                # GAP: changing OncePerRound to "twice per round" not expressible as delta
            ]
        },
        "talentII": {
            "id": "mireth_blank_space",
            "name": "Blank Space",
            "trigger": "None",
            "effects": [],
            "overrides": [
                # GAP: +15 Energy and -10 enemy Energy when Pages consumed — needs new effects
            ]
        },
        "talentIII": {
            "id": "mireth_no_record_remains",
            "name": "No Record Remains",
            "trigger": "None",
            "effects": [],
            "overrides": [
                # GAP: HP-gated bonus damage on Page consumption
            ]
        }
    }
    return dict(basic=basic, active=active, passive=passive,
                passive1=passive1, passive2=passive2, talents=talents)


# ================================================================ ARDEN
def arden():
    basic = {
        "id": "arden_find_the_measure",
        "name": "Find the Measure",
        "effects": [
            # Check for Measured BEFORE applying Mark: bonus damage vs already-Measured target
            {"kind": "Damage", "target": "HighestHpEnemy", "power": 0.0,
             "targetMaxHpRatio": 0.02, "maxHpCap": 1.00,
             "condition": "HasMyMark", "conditionOn": "Target"},
            {"kind": "Damage",      "target": "HighestHpEnemy", "power": 1.25},
            {"kind": "ApplyStatus", "target": "HighestHpEnemy", "status": "Mark", "duration": 2},
        ]
    }
    active = {
        "id": "arden_define_the_weak_point",
        "name": "Define the Weak Point",
        "energyCost": 100,
        "effects": [
            {"kind": "ApplyStatus", "target": "HighestHpEnemy", "status": "MarkAlt", "duration": 2},
            {"kind": "Damage",      "target": "HighestHpEnemy", "power": 1.90},
            # True Measure bonus damage on active hit
            {"kind": "Damage", "target": "HighestHpEnemy", "power": 0.0,
             "targetMaxHpRatio": 0.03, "maxHpCap": 1.20,
             "condition": "HasMyMark", "conditionStatus": "MarkAlt", "conditionOn": "Target"},
            # Measured bonus if target also has Mark
            {"kind": "Damage", "target": "HighestHpEnemy", "power": 0.0,
             "targetMaxHpRatio": 0.02, "maxHpCap": 1.00,
             "condition": "HasMyMark", "conditionOn": "Target"},
            # GAP: "first 3 times" charge counting, removal after 3rd + 15 Energy
        ]
    }
    passive = {
        "id": "arden_exact_calculation_stats",
        "name": "Exact Calculation",
        "trigger": "BattleStart",
        "effects": [
            {"kind": "ApplyStatus", "target": "Self", "status": "SpeedUp",      "magnitude": 0.08, "duration": 99},
            {"kind": "ApplyStatus", "target": "Self", "status": "ArmorBreakUp", "magnitude": 0.05, "duration": 99},
            # +8% damage vs Measured: grant 1 permanent Resolve, use OutputMod
            {"kind": "GainResolve", "target": "Self", "maxStacks": 1, "duration": 99},
        ]
    }
    # P1 mechanic via AfterBasicAttack: bonus damage vs Measured/True Measured
    passive1 = {
        "id": "arden_exact_calculation",
        "name": "Exact Calculation",
        "trigger": "AfterBasicAttack",
        "oncePerBattle": False,
        "outputModStatus": "Resolve",
        "outputModPerStack": 0.08,
        "outputModScope": "AllDamage",
        "outputModMarkedOnly": True,
        "effects": [
            # +1% MaxHP damage vs Measured enemy above 70% HP
            {"kind": "Damage", "target": "LastTarget", "power": 0.0,
             "targetMaxHpRatio": 0.01, "maxHpCap": 0.50,
             "condition": "HasMyMark", "conditionOn": "Target",
             "condition2": "HpAbove", "conditionValue2": 0.70, "conditionOn2": "Target"},
            # True Measure bonus on basic attack
            {"kind": "Damage", "target": "LastTarget", "power": 0.0,
             "targetMaxHpRatio": 0.03, "maxHpCap": 1.20,
             "condition": "HasMyMark", "conditionStatus": "MarkAlt", "conditionOn": "Target"},
        ]
    }
    # P2: Everything Has a Limit
    # GAP: per-hit HP recording, running minimum, difference-based burst damage
    passive2 = {
        "id": "arden_everything_has_a_limit",
        "name": "Everything Has a Limit",
        "trigger": "None",
        "effects": []
    }
    talents = {
        "talentI": {
            "id": "arden_deeper_measure",
            "name": "Deeper Measure",
            "trigger": "None",
            "effects": [],
            "overrides": [
                {"ability": "basic", "effectIndex": 2, "field": "duration",         "delta": 1},
                {"ability": "basic", "effectIndex": 0, "field": "targetMaxHpRatio", "delta": 0.005},
                {"ability": "basic", "effectIndex": 0, "field": "maxHpCap",         "delta": 0.10},
            ]
        },
        "talentII": {
            "id": "arden_perfect_line",
            "name": "Perfect Line",
            "trigger": "None",
            "effects": [],
            "overrides": [
                # GAP: "ignore 10% Defense" not expressible; +5 Energy conditional on enemy HP
            ]
        },
        "talentIII": {
            "id": "arden_final_calculation",
            "name": "Final Calculation",
            "trigger": "None",
            "effects": [],
            "overrides": [
                # GAP: depends on P2 HP recording
            ]
        }
    }
    return dict(basic=basic, active=active, passive=passive,
                passive1=passive1, passive2=passive2, talents=talents)


if __name__ == "__main__":
    main()
