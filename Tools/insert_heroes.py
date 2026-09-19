#!/usr/bin/env python3
"""Insert Edrin and Zella into heroes.json."""
import json

HEROES_PATH = "Assets/Resources/Content/heroes.json"

with open(HEROES_PATH, "r", encoding="utf-8") as f:
    data = json.load(f)

edrin = {
    "id": "edrin_the_distant_bell",
    "name": "Edrin",
    "role": "Marksman",
    "aspect": "Verdant",
    "stars": 5,
    "startingHero": False,
    "hp": 1424,
    "attack": 190,
    "defense": 62,
    "speed": 109,
    "critChance": 0.10,
    "critDamage": 1.55,
    "growth": 0.115,
    "startEnergy": 0,
    "effectHit": 0.06,
    "effectResist": 0.0,
    "skillDamage": 0,
    "block": 0,
    "armorBreak": 0,
    "controlImmunity": 0,
    "damageReduction": 0,
    "holyDamage": 0,
    "visual": {
        "primary": "3B5E28",
        "secondary": "1A2F14",
        "accent": "A0C070",
        "silhouette": "humanoid",
        "weapon": "bow",
        "scale": 1.02,
        "sigil": None,
        "sprite": "edrin_the_distant_bell",
        "spriteBodyFraction": 0.55,
        "spriteFootFraction": 0.22,
        "spriteMirror": False
    },
    "basic": {
        "id": "edrin_mark_of_survival",
        "name": "Mark of Survival",
        "effects": [
            {
                "kind": "Damage",
                "target": "LowestHpEnemy",
                "power": 1.2
            },
            {
                "kind": "ApplyStatus",
                "target": "LowestHpEnemy",
                "status": "Mark",
                "duration": 2,
                "stackOnReapply": True,
                "maxStacks": 3
            }
        ]
    },
    "active": {
        "id": "edrin_the_old_watchtower",
        "name": "The Old Watchtower",
        "energyCost": 100,
        "effects": [
            {
                "kind": "Damage",
                "target": "AllEnemies",
                "power": 2.2
            },
            {
                "kind": "Damage",
                "target": "HighestMyMarkStacksEnemy",
                "power": 1.0
            },
            {
                "kind": "ApplyStatus",
                "target": "HighestMyMarkStacksEnemy",
                "status": "Ledger",
                "duration": 2,
                "magnitude": 0.25,
                "storedCap": 1.5,
                "ledgerAutoRelease": True,
                "ledgerAutoReleaseSplitHeal": 0.5
            },
            {
                "kind": "RemoveStatus",
                "target": "HighestMyMarkStacksEnemy",
                "status": "Mark",
                "hits": 3
            }
        ]
    },
    "passive": {
        "id": "edrin_guardian_stats",
        "name": "Guardian of What Remains",
        "trigger": "BattleStart",
        "effects": [
            {
                "kind": "ApplyStatus",
                "target": "Self",
                "status": "AttackUp",
                "magnitude": 0.08,
                "duration": 99
            },
            {
                "kind": "ApplyStatus",
                "target": "Self",
                "status": "SpeedUp",
                "magnitude": 0.08,
                "duration": 99
            },
            {
                "kind": "ApplyStatus",
                "target": "Self",
                "status": "Focus",
                "magnitude": 0.08,
                "duration": 99
            }
        ]
    },
    "passive1": {
        "id": "edrin_guardian_of_what_remains",
        "name": "Guardian of What Remains",
        "trigger": "AfterBasicAttack",
        "oncePerBattle": False,
        "outputModStatus": "Resolve",
        "outputModPerStack": 0.04,
        "outputModScope": "AllDamage",
        "outputModMarkedOnly": True,
        "effects": [
            {
                "kind": "GainResolve",
                "target": "Self",
                "maxStacks": 4,
                "condition": "HasMyMark",
                "conditionOn": "LastTarget"
            }
        ]
    },
    "passive2": {
        "id": "edrin_still_here",
        "name": "Still Here",
        "trigger": "OnAllyHpBelowThreshold",
        "threshold": 0.35,
        "limit": "OncePerRound",
        "effects": [
            {
                "kind": "Damage",
                "target": "HighestMyMarkStacksEnemy",
                "power": 0.8
            },
            {
                "kind": "Shield",
                "target": "LowestHpAlly",
                "selfHpRatio": 0.06,
                "duration": 2
            },
            {
                "kind": "EnergyGain",
                "target": "Self",
                "flat": 10,
                "condition": "MyStatusStacksAtLeast",
                "conditionStatus": "Mark",
                "conditionValue": 3,
                "conditionOn": "LastTarget"
            }
        ]
    },
    "talents": {
        "talentI": {
            "id": "edrin_kept_watch",
            "name": "Kept Watch",
            "trigger": "None",
            "effects": [],
            "overrides": [
                {
                    "ability": "basic",
                    "effectIndex": 1,
                    "field": "duration",
                    "delta": 1
                }
            ],
            "appends": [
                {
                    "ability": "basic",
                    "effect": {
                        "kind": "Heal",
                        "target": "Self",
                        "selfHpRatio": 0.04,
                        "condition": "MyStatusStacksAtLeast",
                        "conditionStatus": "Mark",
                        "conditionValue": 3,
                        "conditionOn": "LastTarget"
                    }
                }
            ]
        },
        "talentII": {
            "id": "edrin_watchfires",
            "name": "Watchfires",
            "trigger": "None",
            "effects": [],
            "appends": [
                {
                    "ability": "active",
                    "effect": {
                        "kind": "Shield",
                        "target": "LowestHpAlly",
                        "selfHpRatio": 0.05,
                        "duration": 2
                    }
                }
            ]
        },
        "talentIII": {
            "id": "edrin_nothing_is_lost",
            "name": "Nothing Is Lost",
            "trigger": "None",
            "effects": [],
            "appends": [
                {
                    "ability": "passive1",
                    "effect": {
                        "kind": "Damage",
                        "target": "LowestHpEnemy",
                        "power": 0.5,
                        "scaleByLastRelease": True,
                        "condition": "StatusStacksAtLeast",
                        "conditionStatus": "Resolve",
                        "conditionValue": 4,
                        "conditionOn": "Caster",
                        "consumesResolve": True
                    }
                }
            ]
        }
    }
}

zella = {
    "id": "zella_the_slow_argument",
    "name": "Zella",
    "role": "Stalker",
    "aspect": "Gale",
    "stars": 5,
    "startingHero": False,
    "hp": 1268,
    "attack": 205,
    "defense": 56,
    "speed": 123,
    "critChance": 0.12,
    "critDamage": 1.55,
    "growth": 0.115,
    "startEnergy": 0,
    "effectHit": 0.15,
    "effectResist": 0.0,
    "skillDamage": 0,
    "block": 0,
    "armorBreak": 0,
    "controlImmunity": 0,
    "damageReduction": 0,
    "holyDamage": 0,
    "visual": {
        "primary": "4A6E78",
        "secondary": "1C2B30",
        "accent": "90D0D0",
        "silhouette": "humanoid",
        "weapon": "daggers",
        "scale": 0.99,
        "sigil": None,
        "sprite": "zella_the_slow_argument",
        "spriteBodyFraction": 0.55,
        "spriteFootFraction": 0.22,
        "spriteMirror": False
    },
    "basic": {
        "id": "zella_quiet_dose",
        "name": "Quiet Dose",
        "effects": [
            {
                "kind": "Damage",
                "target": "FrontEnemy",
                "power": 1.2
            },
            {
                "kind": "ApplyStatus",
                "target": "FrontEnemy",
                "status": "Burn",
                "duration": 3,
                "magnitude": 0.30,
                "doTRecord": "LastActionType"
            },
            {
                "kind": "ApplyStatus",
                "target": "FrontEnemy",
                "status": "Mark",
                "duration": 3
            }
        ]
    },
    "active": {
        "id": "zella_counterargument",
        "name": "Counterargument",
        "energyCost": 100,
        "effects": [
            {
                "kind": "Damage",
                "target": "EnemyWithMyMark",
                "power": 2.1
            },
            {
                "kind": "Damage",
                "target": "EnemyWithMyMark",
                "power": 1.2,
                "condition": "SubjectRepeatedActionType",
                "conditionOn": "Target"
            },
            {
                "kind": "ApplyStatus",
                "target": "EnemyWithMyMark",
                "status": "Silence",
                "duration": 1,
                "condition": "SubjectRepeatedActionType",
                "conditionOn": "Target"
            },
            {
                "kind": "RemoveStatus",
                "target": "EnemyWithMyMark",
                "status": "Burn",
                "hits": 99,
                "condition": "SubjectRepeatedActionType",
                "conditionOn": "Target"
            },
            {
                "kind": "RemoveStatus",
                "target": "EnemyWithMyMark",
                "status": "Mark",
                "hits": 99,
                "condition": "SubjectRepeatedActionType",
                "conditionOn": "Target"
            }
        ]
    },
    "passive": {
        "id": "zella_reaction_study_stats",
        "name": "Reaction Study",
        "trigger": "BattleStart",
        "effects": [
            {
                "kind": "ApplyStatus",
                "target": "Self",
                "status": "AttackUp",
                "magnitude": 0.10,
                "duration": 99
            },
            {
                "kind": "ApplyStatus",
                "target": "Self",
                "status": "EffectHitUp",
                "magnitude": 0.12,
                "duration": 99
            }
        ]
    },
    "passive1": {
        "id": "zella_reaction_study",
        "name": "Reaction Study",
        "trigger": "OnAppliedDoTTick",
        "perTargetLimit": 1,
        "effects": [
            {
                "kind": "EnergyGain",
                "target": "BroadcastSubject",
                "flat": -10,
                "condition": "SubjectChangedActionType",
                "conditionOn": "BroadcastSubject"
            },
            {
                "kind": "ExtendStatus",
                "target": "BroadcastSubject",
                "status": "Burn",
                "duration": 1,
                "condition": "SubjectChangedActionType",
                "conditionOn": "BroadcastSubject"
            },
            {
                "kind": "ExtendStatus",
                "target": "BroadcastSubject",
                "status": "Mark",
                "duration": 1,
                "condition": "SubjectChangedActionType",
                "conditionOn": "BroadcastSubject"
            }
        ]
    },
    "passive2": {
        "id": "zella_nothing_stays_settled",
        "name": "Nothing Stays Settled",
        "trigger": "OnEnemyDefeated",
        "effects": [
            {
                "kind": "TransferStatus",
                "target": "DefeatedUnit",
                "status": "Burn",
                "secondaryTarget": "HighestHpEnemy"
            },
            {
                "kind": "TransferStatus",
                "target": "DefeatedUnit",
                "status": "Mark",
                "secondaryTarget": "HighestHpEnemy"
            }
        ]
    },
    "talents": {
        "talentI": {
            "id": "zella_measured_reply",
            "name": "Measured Reply",
            "trigger": "None",
            "effects": [],
            "appends": [
                {
                    "ability": "basic",
                    "effect": {
                        "kind": "ApplyStatus",
                        "target": "FrontEnemy",
                        "status": "EffectResistDown",
                        "magnitude": 0.10,
                        "duration": 2,
                        "condition": "SubjectRepeatedActionType",
                        "conditionOn": "Target",
                        "bypassResist": True
                    }
                }
            ]
        },
        "talentII": {
            "id": "zella_forced_conclusion",
            "name": "Forced Conclusion",
            "trigger": "None",
            "effects": [],
            "overrides": [
                {
                    "ability": "active",
                    "effectIndex": 2,
                    "field": "duration",
                    "delta": 1
                }
            ]
        },
        "talentIII": {
            "id": "zella_changing_testimony",
            "name": "Changing Testimony",
            "trigger": "OnAppliedDoTTick",
            "effects": [
                {
                    "kind": "ApplyStatus",
                    "target": "Self",
                    "status": "Focus",
                    "magnitude": 0.85,
                    "duration": 1,
                    "condition": "SubjectChangedActionType",
                    "conditionOn": "BroadcastSubject"
                }
            ]
        }
    }
}

data["heroes"].append(edrin)
data["heroes"].append(zella)

with open(HEROES_PATH, "w", encoding="utf-8", newline="\n") as f:
    json.dump(data, f, indent=2, ensure_ascii=False)
    f.write("\n")

print(f"Done. Added Edrin and Zella. Total heroes: {len(data['heroes'])}")
