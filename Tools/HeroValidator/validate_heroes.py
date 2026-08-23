#!/usr/bin/env python3
"""
validate_heroes.py  —  Emberfall hero JSON validator
CLI:  python validate_heroes.py <path-to-json>
Exit: 1 if any 'fail' finding exists; 0 otherwise.
"""

import json
import sys

# ---------------------------------------------------------------------------
# Ground truth from Assets/Scripts/Core/Data/Enums.cs
# ---------------------------------------------------------------------------
EFFECT_KINDS = {"Damage", "Heal", "Shield", "ApplyStatus", "Cleanse", "EnergyGain", "Dispel"}
STATUS_KINDS = {
    "None", "AttackUp", "AttackDown", "DefenseUp", "DefenseDown",
    "Burn", "Regen", "Shield", "Taunt", "Focus", "Slow",
    "Silence", "Stun", "Stealth",
}

# ---------------------------------------------------------------------------
# Ground truth from Assets/Scripts/Core/Data/Definitions.cs — UnitDef.ReadCommon()
# Exactly the JSON keys ReadCommon() reads, verbatim.
# ---------------------------------------------------------------------------
VALID_STAT_KEYS = {
    "id", "name", "aspect",
    "hp", "attack", "defense", "speed",
    "critChance", "critDamage",
    "effectHit", "effectResist",
    "skillDamage", "block", "armorBreak", "controlImmunity", "damageReduction", "holyDamage",
    "growth", "startEnergy",
}

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def hero_id(hero):
    return hero.get("id") or "(no id)"


def collect_effects(ability):
    if not isinstance(ability, dict):
        return []
    return [e for e in ability.get("effects", []) if isinstance(e, dict)]


def _get_skills(hero):
    """Return the dict containing ability slots.
    Generator format nests them under 'skills'; fall back to the hero dict
    itself so the flat synthetic fixture (regression-batch-1.json) works too.
    """
    skills = hero.get("skills")
    if isinstance(skills, dict):
        return skills
    return hero


def _get_talent_slots(hero):
    """Yield (label, value) for each talent slot.
    Generator format: talents is a dict {"talentI": ..., "talentII": ..., "talentIII": ...}.
    Synthetic fixture format: talents is a list [obj_or_null, ...].
    """
    raw = hero.get("talents")
    if isinstance(raw, dict):
        for k, v in raw.items():
            yield k, v
    elif isinstance(raw, list):
        for i, v in enumerate(raw):
            yield f"[{i}]", v


def collect_all_effects(hero):
    """Yield (slot_label, effect_dict) for every effect in the hero."""
    skills = _get_skills(hero)
    for slot in ("basicAttack", "activeSkill", "passive1", "passive2"):
        ab = skills.get(slot)
        if isinstance(ab, dict):
            for eff in collect_effects(ab):
                yield slot, eff

    for label, t in _get_talent_slots(hero):
        if isinstance(t, dict):
            for eff in collect_effects(t):
                yield f"talents.{label}", eff


def find_stacks(obj, path=""):
    """Recursively yield dotted paths for every 'stacks' key found."""
    if isinstance(obj, dict):
        if "stacks" in obj:
            yield (path + ".stacks") if path else "stacks"
        for k, v in obj.items():
            prefix = (path + "." + k) if path else k
            yield from find_stacks(v, prefix)
    elif isinstance(obj, list):
        for i, item in enumerate(obj):
            yield from find_stacks(item, f"{path}[{i}]")


def is_set(ability):
    return isinstance(ability, dict)


def _near_miss_suggestions(key, valid_keys):
    """Return a sorted list of plausible real-key matches for an unknown key.
    Checks case-insensitive exact first, then substring in either direction.
    """
    key_lower = key.lower()
    exact = [r for r in valid_keys if r.lower() == key_lower]
    if exact:
        return exact
    return sorted(r for r in valid_keys
                  if r.lower() in key_lower or key_lower in r.lower())


def talents_all_null(hero):
    raw = hero.get("talents")
    if raw is None:
        return True
    if isinstance(raw, list):
        return all(x is None for x in raw)
    if isinstance(raw, dict):
        return all(x is None for x in raw.values())
    return False


def talents_all_three_set(hero):
    raw = hero.get("talents")
    if isinstance(raw, list):
        return len(raw) >= 3 and all(isinstance(x, dict) for x in raw[:3])
    if isinstance(raw, dict):
        vals = list(raw.values())
        return len(vals) >= 3 and all(isinstance(x, dict) for x in vals[:3])
    return False


# ---------------------------------------------------------------------------
# Per-hero checks
# ---------------------------------------------------------------------------

def check_stats(hero):
    findings = []
    stats = hero.get("stats")
    if not isinstance(stats, dict):
        return findings
    for key in stats:
        if key not in VALID_STAT_KEYS:
            suggestions = _near_miss_suggestions(key, VALID_STAT_KEYS)
            if suggestions:
                hint = " or ".join(f"'{s}'" for s in suggestions)
                msg = f"stats.{key} — unknown stat key, did you mean {hint}?"
            else:
                msg = f"stats.{key} — unknown stat key (not in UnitDef)"
            findings.append({"severity": "fail", "message": msg})
    return findings


def check_effect_kinds(hero):
    findings = []
    for slot, eff in collect_all_effects(hero):
        kind = eff.get("kind")
        if kind is None:
            continue
        if kind not in EFFECT_KINDS:
            if kind in STATUS_KINDS:
                findings.append({"severity": "fail",
                                  "message": (f"{slot}: effect kind '{kind}' — "
                                              "should be ApplyStatus + status, not a top-level kind")})
            else:
                findings.append({"severity": "fail",
                                  "message": f"{slot}: effect kind '{kind}' is not a valid EffectKind"})
    return findings


def check_status_kinds(hero):
    findings = []
    for slot, eff in collect_all_effects(hero):
        if eff.get("kind") != "ApplyStatus":
            continue
        status = eff.get("status")
        if status is None:
            findings.append({"severity": "fail",
                              "message": f"{slot}: ApplyStatus effect is missing a 'status' value"})
        elif status not in STATUS_KINDS:
            findings.append({"severity": "fail",
                              "message": f"{slot}: ApplyStatus status '{status}' is not a valid StatusKind"})
    return findings


def check_star_shape(hero):
    """Design rules — severity warn."""
    findings = []
    stars = hero.get("stars")
    if stars is None:
        return findings

    skills = _get_skills(hero)
    active = is_set(skills.get("activeSkill"))
    p1 = is_set(skills.get("passive1"))
    p2 = is_set(skills.get("passive2"))
    t_null = talents_all_null(hero)
    t_full = talents_all_three_set(hero)

    if stars == 1:
        if active or p1 or p2 or not t_null:
            findings.append({"severity": "warn",
                              "message": "stars 1: should have activeSkill null, passive1 null, passive2 null, talents all null"})
    elif stars == 2:
        if active or not p1 or p2 or not t_null:
            findings.append({"severity": "warn",
                              "message": "stars 2: should have activeSkill null, exactly passive1 set, passive2 null, talents null"})
    elif stars == 3:
        if not active or not p1 or p2 or not t_null:
            findings.append({"severity": "warn",
                              "message": "stars 3: should have activeSkill set, exactly passive1 set, passive2 null, talents null"})
    elif stars == 4:
        if not active or not p1 or not t_null:
            findings.append({"severity": "warn",
                              "message": "stars 4: should have activeSkill set, passive1 set, passive2 optional, talents null"})
    elif stars == 5:
        if not active or not p1 or not p2 or not t_full:
            findings.append({"severity": "warn",
                              "message": "stars 5: should have activeSkill set, passive1 set, passive2 set, all three talents set"})

    return findings


def check_rarity(hero):
    if "rarity" in hero:
        return [{"severity": "fail",
                 "message": "rarity should not exist, stars replace it"}]
    return []


def check_sigil(hero):
    visual = hero.get("visual")
    if not isinstance(visual, dict):
        return []
    if "sigil" in visual and visual["sigil"] is not None:
        return [{"severity": "fail",
                 "message": "sigil mapping is undecided, must be null"}]
    return []


def check_stacks(hero):
    findings = []
    for path in find_stacks(hero):
        findings.append({"severity": "info",
                          "message": (f"stacks at {path} — undefined against engine mechanism, "
                                      "confirm intent before accepting")})
    return findings


# ---------------------------------------------------------------------------
# Validate one hero
# ---------------------------------------------------------------------------

_SEV_ORDER = {"fail": 0, "warn": 1, "info": 2}


def validate_hero(hero):
    findings = []
    findings += check_stats(hero)
    findings += check_effect_kinds(hero)
    findings += check_status_kinds(hero)
    findings += check_star_shape(hero)
    findings += check_rarity(hero)
    findings += check_sigil(hero)
    findings += check_stacks(hero)
    findings.sort(key=lambda f: _SEV_ORDER[f["severity"]])
    return findings


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

def main():
    if len(sys.argv) < 2:
        print("usage: python validate_heroes.py <path-to-json>", file=sys.stderr)
        sys.exit(2)

    path = sys.argv[1]
    try:
        with open(path, encoding="utf-8") as fh:
            data = json.load(fh)
    except FileNotFoundError:
        print(f"error: file not found: {path}", file=sys.stderr)
        sys.exit(2)
    except json.JSONDecodeError as exc:
        print(f"error: invalid JSON — {exc}", file=sys.stderr)
        sys.exit(2)

    heroes = data if isinstance(data, list) else data.get("heroes", [])
    if not isinstance(heroes, list):
        print("error: expected a JSON array or an object with a 'heroes' array", file=sys.stderr)
        sys.exit(2)

    totals = {"fail": 0, "warn": 0, "info": 0}
    any_fail = False

    for hero in heroes:
        hid = hero_id(hero)
        findings = validate_hero(hero)
        if findings:
            print(f"\n{hid}")
            for f in findings:
                print(f"  [{f['severity'].upper()}] {f['message']}")
                totals[f["severity"]] += 1
                if f["severity"] == "fail":
                    any_fail = True
        else:
            print(f"\n{hid}  OK")

    print()
    print(f"Summary: {totals['fail']} fail  {totals['warn']} warn  {totals['info']} info")
    sys.exit(1 if any_fail else 0)


if __name__ == "__main__":
    main()
