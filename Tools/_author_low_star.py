#!/usr/bin/env python3
"""Author the twelve 2-star and eight 1-star kits into heroes.json.

ONE SOURCE FOR NUMBER AND SENTENCE. Every description in here is BUILT from the
numbers the effects carry, so a scale change cannot leave a percentage in the text
that the effect no longer does. That was the failure mode of the earlier batches,
which carried a separate text-rewrite table beside the number table.

THE SCALE DICT IS THE BAND. Pinoy's own damage and healing numbers are the shape;
`SCALE` multiplies them to put each hero inside its tier's band, exactly as the
4-star and 3-star passes moved theirs. Nothing but Damage power, Heal power and an
ally-facing Shield ratio is scaled: every buff magnitude, duration, Energy amount
and percent-of-Max-HP term is Pinoy's number untouched, because the band is a claim
about output and those are not output.

Run from the repo root:  python Tools/_author_low_star.py
"""
import json
import sys

HEROES_PATH = "Assets/Resources/Content/heroes.json"

# hero id -> multiplier on every Damage power and Heal power in its kit.
# Measured rather than guessed: pass one authored every kit at Pinoy's raw numbers,
# the harness's STAR BAND BY ROLE printed each hero's output, and each factor below
# is target/measured against the middle of its tier's band.
SCALE = {
    # ---- two stars
    "arno_the_unfinished_mason":   3.50,
    "barr_the_splintered_door":    3.08,
    "piri_the_match_seller":       3.05,
    "dett_the_untied_knot":        3.36,
    "hod_the_redirected_load":     3.13,
    "yara_the_spare_compass":      3.50,
    "garron_the_stout_frame":      3.39,
    "nara_the_road_nurse":         4.22,
    "wynn_the_hand_me_down":       3.15,
    "jax_the_clockwork_rat":       3.07,
    "kora_the_interrupted_queue":  3.49,
    "sena_the_empty_chair":        3.40,
    # ---- one stars
    "fenn_the_parcel_runner":      2.60,
    "ulric_the_fallen_sign":       3.59,
    "moss_the_side_route":         2.71,
    "orenna_the_turning_wheel":    3.16,
    "hal_the_stone_cache":         3.58,
    "iri_the_kept_token":          4.37,
    "bex_the_unlatched":           3.14,
    "till_the_shifting_target":    3.95,
}


def p(hero_id, base):
    """A scaled power, rounded the way every authored kit rounds one."""
    return round(base * SCALE[hero_id], 2)


def pct(value):
    """A power as the percentage its description prints."""
    return str(int(round(value * 100)))


# ============================================================ TWO STARS


def arno():
    h = "arno_the_unfinished_mason"
    main, bonus = p(h, 1.15), p(h, 0.20)
    basic = {
        "id": "arno_chisel_strike",
        "name": "Chisel Strike",
        "description": (
            "Deals " + pct(main) + "% Attack damage to the front enemy, and "
            + pct(bonus) + "% more if that enemy's Shield is still standing."
        ),
        "effects": [
            {"kind": "Damage", "target": "FrontEnemy", "power": main},
            {"kind": "Damage", "target": "LastTarget", "power": bonus,
             "condition": "HasShield", "conditionOn": "Target"},
        ],
    }
    passive1 = {
        "id": "arno_rubble_resolve",
        "name": "Rubble Resolve",
        "description": (
            "When Arno strikes an enemy carrying a Shield, he gains 8% Attack for 2 rounds. "
            "Pinoy writes it as the moment an enemy Shield is removed; a break is raised on the "
            "body that lost the Shield and on that body's own side, so the unit that broke it "
            "never hears about it. Draft, Pinoy to confirm, see Docs/ENGINE-GAPS.md group 32."
        ),
        "trigger": "AfterBasicAttack",
        "oncePerBattle": False,
        "effects": [
            {"kind": "ApplyStatus", "target": "Self", "status": "AttackUp",
             "chance": 1, "bypassResist": True, "duration": 2, "magnitude": 0.08,
             "condition": "HasShield", "conditionOn": "LastTarget"},
        ],
    }
    return basic, passive1


def barr():
    h = "barr_the_splintered_door"
    main = p(h, 1.05)
    basic = {
        "id": "barr_splintering_blow",
        "name": "Splintering Blow",
        "description": (
            "Deals " + pct(main) + "% Attack damage to the front enemy and gives Barr 8% Damage "
            "Reduction for 1 round."
        ),
        "effects": [
            {"kind": "Damage", "target": "FrontEnemy", "power": main},
            {"kind": "ApplyStatus", "target": "Self", "status": "DamageReductionUp",
             "chance": 1, "bypassResist": True, "duration": 1, "magnitude": 0.08},
        ],
    }
    passive1 = {
        "id": "barr_give_way",
        "name": "Give Way",
        "description": (
            "The first time Barr falls below 40% HP, he gains a Shield equal to 10% of his Max HP "
            "for 2 rounds."
        ),
        "trigger": "OnHpBelowThreshold",
        "threshold": 0.4,
        "oncePerBattle": True,
        "effects": [
            {"kind": "Shield", "target": "Self", "duration": 2, "selfHpRatio": 0.10},
        ],
    }
    return basic, passive1


def piri():
    h = "piri_the_match_seller"
    main = p(h, 1.10)
    basic = {
        "id": "piri_price_breaker",
        "name": "Price Breaker",
        "description": (
            "Deals " + pct(main) + "% Attack damage to the front enemy and takes 5 Energy off it."
        ),
        "effects": [
            {"kind": "Damage", "target": "FrontEnemy", "power": main},
            {"kind": "EnergyGain", "target": "LastTarget", "flat": -5},
        ],
    }
    passive1 = {
        "id": "piri_cheap_kindling",
        "name": "Cheap Kindling",
        "description": (
            "When Piri defeats an enemy, the ally with the emptiest bar gains 15 Energy. "
            "Pinoy pays the 15 to Piri; a 2-star has no active skill to spend Energy on, so the "
            "spark is passed to somebody who does. See Docs/PINOY-LIST.md section 2c."
        ),
        "trigger": "OnKill",
        "oncePerBattle": False,
        "effects": [
            {"kind": "EnergyGain", "target": "LowestEnergyAlly", "flat": 15},
        ],
    }
    return basic, passive1


def dett():
    h = "dett_the_untied_knot"
    main = p(h, 1.10)
    basic = {
        "id": "dett_unmoored_swing",
        "name": "Unmoored Swing",
        "description": (
            "Deals " + pct(main) + "% Attack damage to the front enemy and advances Dett's next "
            "turn by 8%."
        ),
        "effects": [
            {"kind": "Damage", "target": "FrontEnemy", "power": main},
            {"kind": "TurnShift", "target": "Self", "magnitude": 0.08},
        ],
    }
    passive1 = {
        "id": "dett_turn_with_it",
        "name": "Turn With It",
        "description": (
            "When an active skill is aimed at Dett, he gains 8% Damage Reduction for 1 round."
        ),
        "trigger": "OnTargeted",
        "oncePerBattle": False,
        "effects": [
            {"kind": "ApplyStatus", "target": "Self", "status": "DamageReductionUp",
             "chance": 1, "bypassResist": True, "duration": 1, "magnitude": 0.08,
             "condition": "SubjectLastActionWasActive", "conditionOn": "BroadcastSource"},
        ],
    }
    return basic, passive1


def hod():
    h = "hod_the_redirected_load"
    main = p(h, 1.05)
    basic = {
        "id": "hod_cargo_bolt",
        "name": "Cargo Bolt",
        "description": (
            "Deals " + pct(main) + "% Attack damage to the front enemy, takes 5 Energy off it and "
            "gives 5 Energy to the ally with the emptiest bar."
        ),
        "effects": [
            {"kind": "Damage", "target": "FrontEnemy", "power": main},
            {"kind": "EnergyGain", "target": "LastTarget", "flat": -5},
            {"kind": "EnergyGain", "target": "LowestEnergyAlly", "flat": 5},
        ],
    }
    passive1 = {
        "id": "hod_counterweight",
        "name": "Counterweight",
        "description": (
            "An ally Hod hands Energy to gains 5% Defense for 1 round."
        ),
        "trigger": "OnAllyEnergyGained",
        "triggerSourceIsSelf": True,
        "oncePerBattle": False,
        "effects": [
            {"kind": "ApplyStatus", "target": "BroadcastSubject", "status": "DefenseUp",
             "chance": 1, "bypassResist": True, "duration": 1, "magnitude": 0.05},
        ],
    }
    return basic, passive1


def yara():
    h = "yara_the_spare_compass"
    main = p(h, 1.10)
    basic = {
        "id": "yara_deflection_shot",
        "name": "Deflection Shot",
        "description": (
            "Deals " + pct(main) + "% Attack damage to the front enemy and gives the lowest-HP "
            "ally 5% Speed for 1 round."
        ),
        "effects": [
            {"kind": "Damage", "target": "FrontEnemy", "power": main},
            {"kind": "ApplyStatus", "target": "LowestHpAlly", "status": "SpeedUp",
             "chance": 1, "bypassResist": True, "duration": 1, "magnitude": 0.05},
        ],
    }
    passive1 = {
        "id": "yara_second_bearing",
        "name": "Second Bearing",
        "description": (
            "Yara never aims twice running at the same enemy, and gains 10% Attack for 1 round "
            "each time she takes a new bearing. Pinoy writes it as a reward for changing target "
            "and pays it in Energy; nothing lets a hero hear its own target change, and a 2-star "
            "has no active skill to spend Energy on, so the switch is made a standing rule and the "
            "reward is paid in Attack. Draft, Pinoy to confirm, see Docs/ENGINE-GAPS.md group 33."
        ),
        "trigger": "AfterBasicAttack",
        "targetBan": "LastTarget",
        "oncePerBattle": False,
        "effects": [
            {"kind": "ApplyStatus", "target": "Self", "status": "AttackUp",
             "chance": 1, "bypassResist": True, "duration": 1, "magnitude": 0.10},
        ],
    }
    return basic, passive1


def garron():
    h = "garron_the_stout_frame"
    main = p(h, 1.05)
    basic = {
        "id": "garron_frame_strike",
        "name": "Frame Strike",
        "description": (
            "Deals " + pct(main) + "% Attack damage to the front enemy and gives Garron 6% Block "
            "for 2 rounds."
        ),
        "effects": [
            {"kind": "Damage", "target": "FrontEnemy", "power": main},
            {"kind": "ApplyStatus", "target": "Self", "status": "BlockUp",
             "chance": 1, "bypassResist": True, "duration": 2, "magnitude": 0.06},
        ],
    }
    passive1 = {
        "id": "garron_set_iron",
        "name": "Set Iron",
        "description": "When Garron blocks an attack, he heals 3% of his Max HP.",
        "trigger": "OnBlock",
        "oncePerBattle": False,
        "effects": [
            {"kind": "Heal", "target": "Self", "targetMaxHpRatio": 0.03},
        ],
    }
    return basic, passive1


def nara():
    h = "nara_the_road_nurse"
    main, mend = p(h, 1.00), p(h, 0.45)
    basic = {
        "id": "nara_roadside_care",
        "name": "Roadside Care",
        "description": (
            "Deals " + pct(main) + "% Attack damage to the front enemy and heals the lowest-HP "
            "ally for " + pct(mend) + "% of Attack."
        ),
        "effects": [
            {"kind": "Damage", "target": "FrontEnemy", "power": main},
            {"kind": "Heal", "target": "LowestHpAlly", "power": mend},
        ],
    }
    passive1 = {
        "id": "nara_carry_on",
        "name": "Carry On",
        "description": (
            "An ally who is still below 35% HP after being healed gains 6% Damage Reduction for "
            "1 round. Pinoy writes it as the first time only; a once-per-battle limit is spent "
            "when the trigger arrives rather than when the clause lands, so it would be gone on "
            "the battle's first heal whatever that ally's health. Draft, Pinoy to confirm, see "
            "Docs/ENGINE-GAPS.md group 34."
        ),
        "trigger": "OnAllyHealed",
        "oncePerBattle": False,
        "effects": [
            {"kind": "ApplyStatus", "target": "BroadcastSubject", "status": "DamageReductionUp",
             "chance": 1, "bypassResist": True, "duration": 1, "magnitude": 0.06,
             "condition": "HpBelow", "conditionOn": "BroadcastSubject", "conditionValue": 0.35},
        ],
    }
    return basic, passive1


def wynn():
    h = "wynn_the_hand_me_down"
    main = p(h, 1.05)
    basic = {
        "id": "wynn_mended_shot",
        "name": "Mended Shot",
        "description": (
            "Deals " + pct(main) + "% Attack damage to the front enemy and gives a random ally "
            "5% Defense for 2 rounds."
        ),
        "effects": [
            {"kind": "Damage", "target": "FrontEnemy", "power": main},
            {"kind": "ApplyStatus", "target": "RandomAlly", "status": "DefenseUp",
             "chance": 1, "bypassResist": True, "duration": 2, "magnitude": 0.05},
        ],
    }
    passive1 = {
        "id": "wynn_useful_again",
        "name": "Useful Again",
        "description": (
            "When an ally carrying Wynn's Defense buff is hit, that ally gains 5 Energy. "
            "Pinoy pays the 5 to Wynn; a 2-star has no active skill to spend Energy on, so the "
            "hand-me-down keeps paying its holder. See Docs/PINOY-LIST.md section 2c."
        ),
        "trigger": "OnEnemyDamagedAlly",
        "oncePerBattle": False,
        "effects": [
            {"kind": "EnergyGain", "target": "BroadcastSubject", "flat": 5,
             "condition": "MyStatusStacksAtLeast", "conditionStatus": "DefenseUp",
             "conditionValue": 1, "conditionOn": "BroadcastSubject"},
        ],
    }
    return basic, passive1


def jax():
    h = "jax_the_clockwork_rat"
    main = p(h, 1.05)
    basic = {
        "id": "jax_rattle_shot",
        "name": "Rattle Shot",
        "description": (
            "Deals " + pct(main) + "% Attack damage to the front enemy and puts its turn back "
            "by 8%."
        ),
        "effects": [
            {"kind": "Damage", "target": "FrontEnemy", "power": main},
            {"kind": "TurnShift", "target": "LastTarget", "magnitude": -0.08},
        ],
    }
    passive1 = {
        "id": "jax_loose_spring",
        "name": "Loose Spring",
        "description": "When Jax puts an enemy's turn back, he gains 5% Speed for 1 round.",
        "trigger": "OnTurnDelayed",
        "oncePerBattle": False,
        "effects": [
            {"kind": "ApplyStatus", "target": "Self", "status": "SpeedUp",
             "chance": 1, "bypassResist": True, "duration": 1, "magnitude": 0.05},
        ],
    }
    return basic, passive1


def kora():
    h = "kora_the_interrupted_queue"
    main, opening = p(h, 1.10), p(h, 0.11)
    basic = {
        "id": "kora_skip_ahead",
        "name": "Skip Ahead",
        "description": (
            "Deals " + pct(main) + "% Attack damage to the front enemy and advances Kora's next "
            "turn by 10%."
        ),
        "effects": [
            {"kind": "Damage", "target": "FrontEnemy", "power": main},
            {"kind": "TurnShift", "target": "Self", "magnitude": 0.10},
        ],
    }
    passive1 = {
        "id": "kora_first_opening",
        "name": "First Opening",
        "description": (
            "Kora's strike deals " + pct(opening) + "% of Attack in additional damage to an enemy "
            "that has not acted this round. Pinoy writes it as 10% more damage; the engine's "
            "damage modifiers are counted off a status the target carries, and there is none that "
            "reads a condition, so the tenth is authored as a second blow. Draft, Pinoy to "
            "confirm, see Docs/ENGINE-GAPS.md group 35."
        ),
        "trigger": "AfterBasicAttack",
        "oncePerBattle": False,
        "effects": [
            {"kind": "Damage", "target": "LastTarget", "power": opening,
             "condition": "SubjectHasNotActedThisRound", "conditionOn": "LastTarget"},
        ],
    }
    return basic, passive1


def sena():
    h = "sena_the_empty_chair"
    main, bite = p(h, 1.10), p(h, 0.25)
    basic = {
        "id": "sena_displaced_arrow",
        "name": "Displaced Arrow",
        "description": (
            "Deals " + pct(main) + "% Attack damage to the front enemy and applies 8% Speed Down "
            "for 1 round."
        ),
        "effects": [
            {"kind": "Damage", "target": "FrontEnemy", "power": main},
            {"kind": "ApplyStatus", "target": "LastTarget", "status": "Slow",
             "chance": 1, "bypassResist": True, "duration": 1, "magnitude": 0.08},
        ],
    }
    passive1 = {
        "id": "sena_unfixed_aim",
        "name": "Unfixed Aim",
        "description": (
            "Sena's shot bites " + pct(bite) + "% of Attack deeper into a slowed enemy. Pinoy "
            "writes it as 10% Critical Chance against the slowed; StatusKind has no Critical "
            "Chance entry, so the advantage is paid as damage instead. Draft, Pinoy to confirm, "
            "see Docs/ENGINE-GAPS.md group 13."
        ),
        "trigger": "AfterBasicAttack",
        "oncePerBattle": False,
        "effects": [
            {"kind": "Damage", "target": "LastTarget", "power": bite,
             "condition": "HasStatus", "conditionStatus": "Slow", "conditionOn": "LastTarget"},
        ],
    }
    return basic, passive1


# ============================================================ ONE STARS


def fenn():
    h = "fenn_the_parcel_runner"
    main = p(h, 1.10)
    return {
        "id": "fenn_cargo_spark",
        "name": "Cargo Spark",
        "description": "Deals " + pct(main) + "% Attack damage to the enemy with the lowest HP.",
        "effects": [
            {"kind": "Damage", "target": "LowestHpEnemy", "power": main},
        ],
    }, None


def ulric():
    h = "ulric_the_fallen_sign"
    main = p(h, 1.05)
    return {
        "id": "ulric_signpost_strike",
        "name": "Signpost Strike",
        "description": "Deals " + pct(main) + "% Attack damage to the front enemy.",
        "effects": [
            {"kind": "Damage", "target": "FrontEnemy", "power": main},
        ],
    }, None


def moss():
    h = "moss_the_side_route"
    main = p(h, 1.10)
    return {
        "id": "moss_side_shot",
        "name": "Side Shot",
        "description": "Deals " + pct(main) + "% Attack damage to a random enemy.",
        "effects": [
            {"kind": "Damage", "target": "RandomEnemy", "power": main},
        ],
    }, None


def orenna():
    h = "orenna_the_turning_wheel"
    main = p(h, 1.10)
    return {
        "id": "orenna_wheelhouse_swing",
        "name": "Wheelhouse Swing",
        "description": (
            "Deals " + pct(main) + "% Attack damage to the front enemy and gives 5 Energy to the "
            "ally with the emptiest bar. Pinoy pays the 5 to Orenna; a 1-star has no active skill "
            "to spend Energy on, so the wheel passes it on. See Docs/PINOY-LIST.md section 2c."
        ),
        "effects": [
            {"kind": "Damage", "target": "FrontEnemy", "power": main},
            {"kind": "EnergyGain", "target": "LowestEnergyAlly", "flat": 5},
        ],
    }, None


def hal():
    h = "hal_the_stone_cache"
    main = p(h, 1.05)
    return {
        "id": "hal_cache_shovel",
        "name": "Cache Shovel",
        "description": (
            "Deals " + pct(main) + "% Attack damage to the front enemy and recovers 2% of Hal's "
            "Max HP."
        ),
        "effects": [
            {"kind": "Damage", "target": "FrontEnemy", "power": main},
            {"kind": "Heal", "target": "Self", "targetMaxHpRatio": 0.02},
        ],
    }, None


def iri():
    h = "iri_the_kept_token"
    main, mend = p(h, 1.00), p(h, 0.30)
    return {
        "id": "iri_tokenkeepers_tap",
        "name": "Tokenkeeper's Tap",
        "description": (
            "Deals " + pct(main) + "% Attack damage to the front enemy and heals the lowest-HP "
            "ally for " + pct(mend) + "% of Attack."
        ),
        "effects": [
            {"kind": "Damage", "target": "FrontEnemy", "power": main},
            {"kind": "Heal", "target": "LowestHpAlly", "power": mend},
        ],
    }, None


def bex():
    h = "bex_the_unlatched"
    main = p(h, 1.10)
    return {
        "id": "bex_latchbreaker",
        "name": "Latchbreaker",
        "description": (
            "Deals " + pct(main) + "% Attack damage to the front enemy and gives Bex 5% Speed for "
            "1 round."
        ),
        "effects": [
            {"kind": "Damage", "target": "FrontEnemy", "power": main},
            {"kind": "ApplyStatus", "target": "Self", "status": "SpeedUp",
             "chance": 1, "bypassResist": True, "duration": 1, "magnitude": 0.05},
        ],
    }, None


def till():
    h = "till_the_shifting_target"
    main = p(h, 1.05)
    return {
        "id": "till_rolling_shot",
        "name": "Rolling Shot",
        "description": (
            "Deals " + pct(main) + "% Attack damage to the front enemy and puts its turn back "
            "by 5%."
        ),
        "effects": [
            {"kind": "Damage", "target": "FrontEnemy", "power": main},
            {"kind": "TurnShift", "target": "LastTarget", "magnitude": -0.05},
        ],
    }, None


KITS = {
    "arno_the_unfinished_mason":  arno,
    "barr_the_splintered_door":   barr,
    "piri_the_match_seller":      piri,
    "dett_the_untied_knot":       dett,
    "hod_the_redirected_load":    hod,
    "yara_the_spare_compass":     yara,
    "garron_the_stout_frame":     garron,
    "nara_the_road_nurse":        nara,
    "wynn_the_hand_me_down":      wynn,
    "jax_the_clockwork_rat":      jax,
    "kora_the_interrupted_queue": kora,
    "sena_the_empty_chair":       sena,
    "fenn_the_parcel_runner":     fenn,
    "ulric_the_fallen_sign":      ulric,
    "moss_the_side_route":        moss,
    "orenna_the_turning_wheel":   orenna,
    "hal_the_stone_cache":        hal,
    "iri_the_kept_token":         iri,
    "bex_the_unlatched":          bex,
    "till_the_shifting_target":   till,
}


def main():
    with open(HEROES_PATH, "r", encoding="utf-8") as f:
        data = json.load(f)

    heroes = data["heroes"]
    index = {h["id"]: h for h in heroes}

    for hero_id, build in KITS.items():
        hero = index.get(hero_id)
        if hero is None:
            print("ERROR: " + hero_id + " not in heroes.json")
            sys.exit(1)
        basic, passive1 = build()
        hero["basic"] = basic
        if passive1 is None:
            hero.pop("passive1", None)
            if hero["stars"] != 1:
                print("ERROR: " + hero_id + " is " + str(hero["stars"]) + " stars and has no passive1")
                sys.exit(1)
        else:
            hero["passive1"] = passive1
            if hero["stars"] != 2:
                print("ERROR: " + hero_id + " is " + str(hero["stars"]) + " stars and has a passive1")
                sys.exit(1)
        print("  authored " + str(hero["stars"]) + "*  " + hero_id)

    with open(HEROES_PATH, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")
    print("Done: " + str(len(KITS)) + " kits.")


if __name__ == "__main__":
    main()
