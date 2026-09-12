#!/usr/bin/env python3
"""Inject abilities for the five pinned heroes into heroes.json.

Run from repo root: python Tools/inject_pinned_five.py
"""
import json, sys, os

HEROES_PATH = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Content", "heroes.json")


# ──────────────────────────────────────── SERAPHINE ────────────────────────────
def build_seraphine():
    return {
        "basic": {
            "id": "seraphine_marginal_note",
            "name": "Marginal Note",
            "effects": [
                {"kind": "Damage", "target": "FrontEnemy", "power": 1.15},
                {"kind": "ApplyStatus", "target": "FrontEnemy", "status": "Mark", "duration": 2},
                {"kind": "ApplyStatus", "target": "FrontEnemy", "status": "Ledger",
                 "magnitude": 0.25, "duration": 2, "storedCap": 0.8, "ledgerBankLimit": 1}
            ]
        },
        "active": {
            "id": "seraphine_contradiction_filed",
            "name": "Contradiction Filed",
            "energyCost": 100,
            "effects": [
                {"kind": "Damage", "target": "AllEnemies", "power": 2.05},
                {"kind": "LedgerRelease", "target": "AllEnemies", "power": 1.0,
                 "condition": "HasMyMark", "conditionOn": "Target"},
                {"kind": "RemoveStatus", "target": "AllEnemies", "status": "Mark", "hits": 1},
                {"kind": "RemoveStatus", "target": "AllEnemies", "status": "Ledger", "hits": 1},
                {"kind": "ApplyStatus", "target": "HighestEnergyEnemy", "status": "Mark", "duration": 2},
                {"kind": "ApplyStatus", "target": "HighestEnergyEnemy", "status": "Ledger",
                 "magnitude": 0.25, "duration": 2, "storedCap": 0.8, "ledgerBankLimit": 1},
                # Dormant LedgerIncrement — TII Cross Reference activates it (power 0→0.3)
                {"kind": "LedgerIncrement", "target": "HighestEnergyEnemy", "power": 0.0,
                 "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
                 "conditionOn": "Caster", "conditionValue": 99}
            ]
        },
        "passive": {
            "id": "seraphine_contrary_evidence_stats",
            "name": "Contrary Evidence",
            "trigger": "BattleStart",
            "effects": [
                {"kind": "ApplyStatus", "target": "Self", "status": "AttackUp",
                 "magnitude": 0.08, "duration": 99},
                {"kind": "ApplyStatus", "target": "Self", "status": "EffectHitUp",
                 "magnitude": 0.1, "duration": 99},
                {"kind": "ApplyStatus", "target": "Self", "status": "SkillDamageUp",
                 "magnitude": 0.1, "duration": 99}
            ]
        },
        "passive1": {
            "id": "seraphine_contrary_evidence",
            "name": "Contrary Evidence",
            "trigger": "OnEnemyBuffGained",
            "oncePerBattle": False,
            "effects": [
                {"kind": "Dispel", "target": "BroadcastSubject", "hits": 1,
                 "condition": "HasMyMark", "conditionOn": "Target"},
                {"kind": "LedgerIncrement", "target": "BroadcastSubject", "power": 0.2,
                 "condition": "HasMyMark", "conditionOn": "Target"}
            ]
        },
        "passive2": {
            "id": "seraphine_volume_closed",
            "name": "Volume Closed",
            "trigger": "TurnStart",
            "oncePerBattle": False,
            "effects": [
                # Phase A: Footnote at cap AND not yet tagged → BuffSuppression + tag
                {"kind": "ApplyStatus", "target": "AllEnemies", "status": "BuffSuppression",
                 "duration": 1,
                 "condition": "LedgerAtCap", "conditionOn": "Target",
                 "condition2": "LacksMyMark", "conditionStatus2": "MarkAlt",
                 "conditionOn2": "Target"},
                {"kind": "ApplyStatus", "target": "AllEnemies", "status": "MarkAlt",
                 "duration": 2,
                 "condition": "LedgerAtCap", "conditionOn": "Target",
                 "condition2": "LacksMyMark", "conditionStatus2": "MarkAlt",
                 "conditionOn2": "Target"},
                # Dormant TurnShift — TIII No Revision overrides magnitude to −0.1
                {"kind": "TurnShift", "target": "AllEnemies", "magnitude": 0.0,
                 "condition": "LedgerAtCap", "conditionOn": "Target",
                 "condition2": "LacksMyMark", "conditionStatus2": "MarkAlt",
                 "conditionOn2": "Target"},
                # Phase B: tag exists AND BuffSuppression gone → cleanup
                {"kind": "RemoveStatus", "target": "AllEnemies", "status": "Mark", "hits": 1,
                 "condition": "HasMyMark", "conditionStatus": "MarkAlt",
                 "conditionOn": "Target",
                 "condition2": "LacksStatus", "conditionStatus2": "BuffSuppression",
                 "conditionOn2": "Target"},
                {"kind": "RemoveStatus", "target": "AllEnemies", "status": "Ledger", "hits": 1,
                 "condition": "HasMyMark", "conditionStatus": "MarkAlt",
                 "conditionOn": "Target",
                 "condition2": "LacksStatus", "conditionStatus2": "BuffSuppression",
                 "conditionOn2": "Target"},
                {"kind": "RemoveStatus", "target": "AllEnemies", "status": "MarkAlt", "hits": 1,
                 "condition": "HasMyMark", "conditionStatus": "MarkAlt",
                 "conditionOn": "Target",
                 "condition2": "LacksStatus", "conditionStatus2": "BuffSuppression",
                 "conditionOn2": "Target"},
                {"kind": "EnergyGain", "target": "Self", "flat": 15,
                 "condition": "HasMyMark", "conditionStatus": "MarkAlt",
                 "conditionOn": "Target",
                 "condition2": "LacksStatus", "conditionStatus2": "BuffSuppression",
                 "conditionOn2": "Target"}
            ]
        },
        "talents": {
            "talentI": {
                "id": "seraphine_second_reading",
                "name": "Second Reading",
                "trigger": "None",
                "effects": [],
                "overrides": [
                    {"ability": "basic", "effectIndex": 2, "field": "magnitude", "delta": 0.1},
                    {"ability": "active", "effectIndex": 5, "field": "magnitude", "delta": 0.1}
                ]
            },
            "talentII": {
                "id": "seraphine_cross_reference",
                "name": "Cross Reference",
                "trigger": "None",
                "effects": [],
                "overrides": [
                    {"ability": "active", "effectIndex": 6, "field": "power", "delta": 0.3},
                    {"ability": "active", "effectIndex": 6, "field": "conditionValue", "delta": -99}
                ]
            },
            "talentIII": {
                "id": "seraphine_no_revision",
                "name": "No Revision",
                "trigger": "None",
                "effects": [],
                "overrides": [
                    {"ability": "passive2", "effectIndex": 2, "field": "magnitude", "delta": -0.1}
                ]
            }
        }
    }


# ──────────────────────────────────────── KAEL ─────────────────────────────────
def build_kael():
    return {
        "basic": {
            "id": "kael_breach_inspection",
            "name": "Breach Inspection",
            "effects": [
                {"kind": "Damage", "target": "FrontEnemy", "power": 1.1},
                {"kind": "ApplyStatus", "target": "FrontEnemy", "status": "Mark", "duration": 2}
            ]
        },
        "active": {
            "id": "kael_take_down_the_wall",
            "name": "Take Down the Wall",
            "energyCost": 100,
            "effects": [
                {"kind": "Damage", "target": "AllEnemies", "power": 2.1},
                {"kind": "Dispel", "target": "AllEnemies", "hits": 1,
                 "condition": "HasMyMark", "conditionOn": "Target"},
                {"kind": "GainResolve", "target": "Self", "maxStacks": 4, "duration": 3},
                # Dormant re-apply Fault Line — TII Clear the Debris activates
                {"kind": "ApplyStatus", "target": "AllEnemies", "status": "Mark", "duration": 2,
                 "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
                 "conditionOn": "Caster", "conditionValue": 99,
                 "condition2": "HasMyMark", "conditionOn2": "Target"}
            ]
        },
        "passive": {
            "id": "kael_unusable_cover_stats",
            "name": "Unusable Cover",
            "trigger": "BattleStart",
            "effects": [
                {"kind": "ApplyStatus", "target": "Self", "status": "DefenseUp",
                 "magnitude": 0.12, "duration": 99},
                {"kind": "ApplyStatus", "target": "Self", "status": "BlockUp",
                 "magnitude": 0.08, "duration": 99}
            ]
        },
        "passive1": {
            "id": "kael_unusable_cover",
            "name": "Unusable Cover",
            "trigger": "OnAllyTurnEnd",
            "oncePerBattle": False,
            "limit": "OncePerRound",
            "effects": [
                # If ally has Shield AND Kael holds Rubble → move 30% of ally Shield
                # Step 1: ally → Kael
                {"kind": "ShieldTransfer", "target": "BroadcastSubject", "magnitude": 0.3,
                 "transferFromTarget": True,
                 "condition": "HasShield", "conditionOn": "Target",
                 "condition2": "StatusStacksAtLeast", "conditionStatus2": "Resolve",
                 "conditionValue2": 1, "conditionOn2": "Caster"},
                # Step 2: Kael → lowest-HP ally
                {"kind": "ShieldTransfer", "target": "LowestHpAlly", "magnitude": 1.0,
                 "condition": "HasShield", "conditionOn": "Caster",
                 "condition2": "StatusStacksAtLeast", "conditionStatus2": "Resolve",
                 "conditionValue2": 1, "conditionOn2": "Caster"},
                # Consume 1 Rubble
                {"kind": "RemoveStatus", "target": "Self", "status": "Resolve", "hits": 1,
                 "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
                 "conditionValue": 1, "conditionOn": "Caster"}
            ]
        },
        "passive2": {
            "id": "kael_what_breaks_forward",
            "name": "What Breaks Forward",
            "trigger": "AfterBasicAttack",
            "oncePerBattle": False,
            "effects": [
                # 35% ATK bonus damage per Rubble (stacked-condition pattern)
                {"kind": "Damage", "target": "LastTarget", "power": 0.35,
                 "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
                 "conditionValue": 1, "conditionOn": "Caster"},
                {"kind": "Damage", "target": "LastTarget", "power": 0.35,
                 "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
                 "conditionValue": 2, "conditionOn": "Caster"},
                {"kind": "Damage", "target": "LastTarget", "power": 0.35,
                 "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
                 "conditionValue": 3, "conditionOn": "Caster"},
                {"kind": "Damage", "target": "LastTarget", "power": 0.35,
                 "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
                 "conditionValue": 4, "conditionOn": "Caster"},
                # Shield = 4% Kael MaxHP per Rubble (stacked-condition)
                {"kind": "Shield", "target": "LowestHpAlly", "selfHpRatio": 0.04,
                 "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
                 "conditionValue": 1, "conditionOn": "Caster"},
                {"kind": "Shield", "target": "LowestHpAlly", "selfHpRatio": 0.04,
                 "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
                 "conditionValue": 2, "conditionOn": "Caster"},
                {"kind": "Shield", "target": "LowestHpAlly", "selfHpRatio": 0.04,
                 "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
                 "conditionValue": 3, "conditionOn": "Caster"},
                {"kind": "Shield", "target": "LowestHpAlly", "selfHpRatio": 0.04,
                 "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
                 "conditionValue": 4, "conditionOn": "Caster"},
                # Consume all Rubble
                {"kind": "RemoveStatus", "target": "Self", "status": "Resolve", "hits": 10,
                 "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
                 "conditionValue": 1, "conditionOn": "Caster"}
            ]
        },
        "talents": {
            "talentI": {
                "id": "kael_measured_demolition",
                "name": "Measured Demolition",
                "trigger": "AfterBasicAttack",
                "oncePerBattle": False,
                "effects": [
                    {"kind": "ApplyStatus", "target": "LastTarget", "status": "EffectResistDown",
                     "magnitude": 0.1, "duration": 2,
                     "condition": "HasMyMark", "conditionOn": "Target"}
                ]
            },
            "talentII": {
                "id": "kael_clear_the_debris",
                "name": "Clear the Debris",
                "trigger": "None",
                "effects": [],
                "overrides": [
                    {"ability": "active", "effectIndex": 3, "field": "conditionValue", "delta": -99}
                ]
            },
            "talentIII": {
                "id": "kael_lasting_materials",
                "name": "Lasting Materials",
                "trigger": "None",
                "effects": [],
                "overrides": [
                    {"ability": "active", "effectIndex": 2, "field": "duration", "delta": 97},
                    {"ability": "active", "effectIndex": 2, "field": "maxStacks", "delta": 1},
                    {"ability": "active", "effectIndex": 0, "field": "power", "delta": -0.21}
                ]
            }
        }
    }


# ──────────────────────────────────────── AERON ────────────────────────────────
def build_aeron():
    return {
        "basic": {
            "id": "aeron_press_forward",
            "name": "Press Forward",
            "effects": [
                {"kind": "Damage", "target": "FrontEnemy", "power": 1.1},
                # Delays 12%, banks as TurnDebt up to 30%. Rate 50 = 5 Energy per 10% debt.
                {"kind": "TurnShift", "target": "FrontEnemy", "magnitude": -0.12,
                 "shiftBanksAs": "TurnDebt", "storedCap": 0.3, "power": 50},
                # Bonus damage from recorded TurnDebt repayments (Resolve stacks)
                # perStack: 0.15*(1+1*n). At 1→0.30, 2→0.45, 3→0.60, 4→0.75
                {"kind": "Damage", "target": "FrontEnemy", "power": 0.15, "perStack": 1.0,
                 "consumesResolve": True,
                 "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
                 "conditionValue": 1, "conditionOn": "Caster"}
            ]
        },
        "active": {
            "id": "aeron_break_the_rhythm",
            "name": "Break the Rhythm",
            "energyCost": 100,
            "effects": [
                {"kind": "Damage", "target": "AllEnemies", "power": 1.85},
                {"kind": "TurnShift", "target": "HighestEnergyEnemy", "magnitude": -0.25,
                 "shiftBanksAs": "TurnDebt", "storedCap": 0.25, "power": 50}
            ]
        },
        "passive": {
            "id": "aeron_never_yield_stats",
            "name": "Never Yield the Pace",
            "trigger": "BattleStart",
            "effects": [
                {"kind": "ApplyStatus", "target": "Self", "status": "SpeedUp",
                 "magnitude": 0.08, "duration": 99},
                {"kind": "ApplyStatus", "target": "Self", "status": "BlockUp",
                 "magnitude": 0.08, "duration": 99}
            ]
        },
        "passive1": {
            "id": "aeron_never_yield",
            "name": "Never Yield the Pace",
            "trigger": "OnTurnDebtRepaid",
            "oncePerBattle": False,
            "effects": [
                {"kind": "GainResolve", "target": "Self", "maxStacks": 4, "duration": 99}
            ]
        },
        "passive2": {
            "id": "aeron_momentum_breaker",
            "name": "Momentum Breaker",
            "trigger": "OnEnemyEnergyThreshold",
            "oncePerBattle": False,
            "perTargetLimit": 1,
            "effects": [
                {"kind": "ConsumeTurnDebt", "target": "ThresholdUnit", "magnitude": 0.10,
                 "secondaryTarget": "LowestEnergyAlly"},
                # Dormant TurnShift — TII Shared Tempo activates (magnitude 0→0.1)
                {"kind": "TurnShift", "target": "LowestEnergyAlly", "magnitude": 0.0,
                 "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
                 "conditionOn": "Caster", "conditionValue": 99}
            ]
        },
        "talents": {
            "talentI": {
                "id": "aeron_interest_owed",
                "name": "Interest Owed",
                "trigger": "OnEnemyTurnEnd",
                "oncePerBattle": False,
                "effects": [
                    {"kind": "ApplyStatus", "target": "BroadcastSubject", "status": "Slow",
                     "magnitude": 0.05, "duration": 1,
                     "condition": "HasStatus", "conditionStatus": "TurnDebt",
                     "conditionOn": "Target"}
                ]
            },
            "talentII": {
                "id": "aeron_shared_tempo",
                "name": "Shared Tempo",
                "trigger": "None",
                "effects": [],
                "overrides": [
                    {"ability": "passive2", "effectIndex": 1, "field": "magnitude", "delta": 0.1},
                    {"ability": "passive2", "effectIndex": 1, "field": "conditionValue", "delta": -99}
                ]
            },
            "talentIII": {
                "id": "aeron_unpaid_action",
                "name": "Unpaid Action",
                "trigger": "OnTurnDebtRepaid",
                "oncePerBattle": False,
                "limit": "OncePerRound",
                "effects": [
                    {"kind": "ApplyStatus", "target": "BroadcastSubject", "status": "Silence",
                     "duration": 1}
                ]
            }
        }
    }


# ──────────────────────────────────────── NEREIA ───────────────────────────────
def build_nereia():
    return {
        "basic": {
            "id": "nereia_shared_pain",
            "name": "Shared Pain",
            "effects": [
                {"kind": "Damage", "target": "FrontEnemy", "power": 1.05},
                # Approximate 3-party HP transfer: if Nereia >80% HP, bonus heal 5% Nereia MaxHP
                {"kind": "Heal", "target": "LowestHpAlly", "selfHpRatio": 0.05,
                 "condition": "HpAbove", "conditionValue": 0.8, "conditionOn": "Caster"},
                # Heal lowest-HP ally 45% ATK
                {"kind": "Heal", "target": "LowestHpAlly", "power": 0.45},
                # Dormant: at 4 Flow, extra heal 70% ATK + consume Flow (passive1 grants Flow)
                {"kind": "Heal", "target": "LowestHpAlly", "power": 0.7,
                 "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
                 "conditionValue": 4, "conditionOn": "Caster"},
                {"kind": "RemoveStatus", "target": "Self", "status": "Resolve", "hits": 10,
                 "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
                 "conditionValue": 4, "conditionOn": "Caster"}
            ]
        },
        "active": {
            "id": "nereia_relief_circuit",
            "name": "Relief Circuit",
            "energyCost": 100,
            "effects": [
                {"kind": "Damage", "target": "AllEnemies", "power": 1.7},
                {"kind": "Heal", "target": "TwoLowestHpAllies", "power": 1.0},
                # 20% damage received redirected to Nereia for 2 rounds
                {"kind": "ApplyStatus", "target": "TwoLowestHpAllies", "status": "Covered",
                 "magnitude": 0.2, "duration": 2},
                # Reserve: Ledger on self, banks 50% of incoming damage, cap 20% MaxHP
                # Auto-releases as heal to lowest-HP ally on expiry
                {"kind": "ApplyStatus", "target": "Self", "status": "Ledger",
                 "magnitude": 0.5, "duration": 2,
                 "storedCap": 0.2, "storedCapBasis": "maxHp",
                 "ledgerAutoRelease": True,
                 "ledgerAutoReleaseAsHeal": True,
                 "ledgerAutoReleaseTarget": "LowestHpAlly"}
            ]
        },
        "passive": {
            "id": "nereia_triage_buffer_stats",
            "name": "Triage Buffer",
            "trigger": "BattleStart",
            "effects": [
                {"kind": "ApplyStatus", "target": "Self", "status": "MaxHpUp",
                 "magnitude": 0.1, "duration": 99},
                {"kind": "ApplyStatus", "target": "Self", "status": "EffectResistUp",
                 "magnitude": 0.1, "duration": 99}
            ]
        },
        "passive1": {
            "id": "nereia_triage_buffer",
            "name": "Triage Buffer",
            "trigger": "OnDamaged",
            "oncePerBattle": False,
            "effects": [
                # On damage to Nereia, gain 1 Flow up to 4
                # (approximate: description says "redirected damage or healing ally <40%")
                {"kind": "GainResolve", "target": "Self", "maxStacks": 4, "duration": 99}
            ]
        },
        "passive2": {
            "id": "nereia_change_of_course",
            "name": "Change of Course",
            "trigger": "OnAllyWouldDie",
            "oncePerBattle": False,
            "limit": "OncePerRound",
            "effects": [
                {"kind": "RedirectLethal", "target": "BroadcastSubject", "power": 1.0},
                {"kind": "Heal", "target": "BroadcastSubject", "power": 0.01,
                 "targetMaxHpRatio": 0.12}
            ]
        },
        "talents": {
            "talentI": {
                "id": "nereia_deeper_reservoir",
                "name": "Deeper Reservoir",
                "trigger": "OnStatusExpired",
                "oncePerBattle": False,
                "triggerStatus": "Ledger",
                "effects": [
                    {"kind": "EnergyGain", "target": "Self", "flat": 10}
                ],
                "overrides": [
                    {"ability": "active", "effectIndex": 3, "field": "storedCap", "delta": 0.05}
                ]
            },
            "talentII": {
                "id": "nereia_shared_reserve",
                "name": "Shared Reserve",
                "trigger": "OnStatusExpired",
                "oncePerBattle": False,
                "triggerStatus": "Ledger",
                "effects": [
                    # When Reserve expires, Shield lowest-HP ally (approximate: 10% Nereia MaxHP)
                    {"kind": "Shield", "target": "LowestHpAlly", "selfHpRatio": 0.1, "duration": 2}
                ]
            },
            "talentIII": {
                "id": "nereia_reversed_flow",
                "name": "Reversed Flow",
                "trigger": "OnAllyWouldDie",
                "oncePerBattle": False,
                "effects": [
                    # When Change of Course triggers, deal 80% ATK to highest-ATK enemy
                    # (approximate: flat ATK-scaled, not based on actual redirected amount)
                    {"kind": "Damage", "target": "HighestAttackEnemy", "power": 0.8}
                ]
            }
        }
    }


# ──────────────────────────────────────── ISOLDE ───────────────────────────────
def build_isolde():
    return {
        "basic": {
            "id": "isolde_exposed_vein",
            "name": "Exposed Vein",
            "effects": [
                {"kind": "Damage", "target": "LowestHpEnemy", "power": 1.25},
                {"kind": "ApplyStatus", "target": "LowestHpEnemy", "status": "Burn",
                 "magnitude": 0.35, "duration": 2}
            ]
        },
        "active": {
            "id": "isolde_inevitable_decay",
            "name": "Inevitable Decay",
            "energyCost": 100,
            "effects": [
                {"kind": "Damage", "target": "LowestHpEnemy", "power": 2.2},
                # Revelation = Mark + Ledger, 3 rounds
                {"kind": "ApplyStatus", "target": "LowestHpEnemy", "status": "Mark", "duration": 3},
                # Ledger records 25% of DoT damage (Burn), auto-releases on expiry
                {"kind": "ApplyStatus", "target": "LowestHpEnemy", "status": "Ledger",
                 "magnitude": 0.25, "duration": 3,
                 "ledgerAutoRelease": True,
                 "ledgerBankOnlyFrom": "Burn"}
            ]
        },
        "passive": {
            "id": "isolde_proof_leaves_stats",
            "name": "Proof Leaves a Trace",
            "trigger": "BattleStart",
            "effects": [
                {"kind": "ApplyStatus", "target": "Self", "status": "SpeedUp",
                 "magnitude": 0.08, "duration": 99},
                {"kind": "ApplyStatus", "target": "Self", "status": "EffectHitUp",
                 "magnitude": 0.08, "duration": 99},
                {"kind": "ApplyStatus", "target": "Self", "status": "HolyDamageUp",
                 "magnitude": 0.08, "duration": 99}
            ]
        },
        "passive1": {
            "id": "isolde_proof_leaves",
            "name": "Proof Leaves a Trace",
            "trigger": "OnAppliedDoTTick",
            "oncePerBattle": False,
            "effects": [
                {"kind": "GainResolve", "target": "Self", "maxStacks": 4, "duration": 99}
            ]
        },
        "passive2": {
            "id": "isolde_unfinished_sentence",
            "name": "Unfinished Sentence",
            "trigger": "OnAppliedDoTTick",
            "oncePerBattle": False,
            "limit": "OncePerRound",
            "effects": [
                # First DoT tick each round on Revealed enemy → 55% ATK damage
                {"kind": "Damage", "target": "BroadcastSubject", "power": 0.55,
                 "condition": "HasMyMark", "conditionOn": "BroadcastSubject"},
                # If 3+ Burn stacks: additional 35% ATK (total 90%) + 8 Energy
                {"kind": "Damage", "target": "BroadcastSubject", "power": 0.35,
                 "condition": "HasMyMark", "conditionOn": "BroadcastSubject",
                 "condition2": "StatusStacksAtLeast", "conditionStatus2": "Burn",
                 "conditionValue2": 3, "conditionOn2": "BroadcastSubject"},
                {"kind": "EnergyGain", "target": "Self", "flat": 8,
                 "condition": "HasMyMark", "conditionOn": "BroadcastSubject",
                 "condition2": "StatusStacksAtLeast", "conditionStatus2": "Burn",
                 "conditionValue2": 3, "conditionOn2": "BroadcastSubject"}
            ]
        },
        "talents": {
            "talentI": {
                "id": "isolde_deeper_trace",
                "name": "Deeper Trace",
                "trigger": "OnAppliedStatusExpired",
                "oncePerBattle": False,
                "triggerStatus": "Burn",
                "effects": [
                    {"kind": "EnergyGain", "target": "Self", "flat": 10}
                ],
                "overrides": [
                    {"ability": "basic", "effectIndex": 1, "field": "magnitude", "delta": 0.15}
                ]
            },
            "talentII": {
                "id": "isolde_revealed_consequence",
                "name": "Revealed Consequence",
                "trigger": "OnEnemyDefeated",
                "oncePerBattle": False,
                "effects": [
                    # On enemy death: transfer Revelation to lowest HP enemy
                    {"kind": "TransferStatus", "target": "DefeatedUnit", "status": "Mark",
                     "secondaryTarget": "LowestHpEnemy",
                     "condition": "HasMyMark", "conditionOn": "Defeated"},
                    {"kind": "TransferStatus", "target": "DefeatedUnit", "status": "Ledger",
                     "secondaryTarget": "LowestHpEnemy",
                     "condition": "HasMyMark", "conditionOn": "Defeated"},
                    # New target gets Lingering Proof
                    {"kind": "ApplyStatus", "target": "LowestHpEnemy", "status": "Burn",
                     "magnitude": 0.35, "duration": 2,
                     "condition": "HasMyMark", "conditionOn": "Defeated"}
                ]
            },
            "talentIII": {
                "id": "isolde_final_revelation",
                "name": "Final Revelation",
                "trigger": "OnAppliedStatusExpired",
                "oncePerBattle": False,
                "triggerStatus": "Mark",
                "effects": [
                    # On Revelation (Mark) expiry with 4 Traces → bonus damage
                    # Note: Ledger auto-release already fires. This adds extra when Traces are at 4.
                    {"kind": "Damage", "target": "BroadcastSubject", "power": 0.6,
                     "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
                     "conditionValue": 4, "conditionOn": "Caster"},
                    # If target below 25% HP, additional damage
                    {"kind": "Damage", "target": "BroadcastSubject", "power": 0.4,
                     "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
                     "conditionValue": 4, "conditionOn": "Caster",
                     "condition2": "HpBelow", "conditionValue2": 0.25, "conditionOn2": "Target"},
                    {"kind": "RemoveStatus", "target": "Self", "status": "Resolve", "hits": 10,
                     "condition": "StatusStacksAtLeast", "conditionStatus": "Resolve",
                     "conditionValue": 4, "conditionOn": "Caster"}
                ]
            }
        }
    }


# ──────────────────────────────────────── INJECT ───────────────────────────────
def inject(heroes_data, hero_id, abilities):
    for hero in heroes_data["heroes"]:
        if hero["id"] == hero_id:
            hero["basic"] = abilities["basic"]
            hero["active"] = abilities["active"]
            hero["passive"] = abilities["passive"]
            hero["passive1"] = abilities["passive1"]
            hero["passive2"] = abilities["passive2"]
            hero["talents"] = abilities["talents"]
            return True
    return False


def main():
    with open(HEROES_PATH, "r", encoding="utf-8") as f:
        data = json.load(f)

    builders = [
        ("seraphine_the_closed_volume", build_seraphine),
        ("kael_the_unyielding", build_kael),
        ("aeron_the_unbroken_stride", build_aeron),
        ("nereia_the_field_medic", build_nereia),
        ("isolde_the_slow_fading", build_isolde),
    ]

    for hero_id, builder in builders:
        abilities = builder()
        if inject(data, hero_id, abilities):
            print(f"  Injected {hero_id}")
        else:
            print(f"  ERROR: {hero_id} not found!", file=sys.stderr)
            return 1

    with open(HEROES_PATH, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")

    print("Done.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
