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
# If a member is added or removed in Enums.cs, update the matching set here.
# The DevHarness CheckEnumParity test asserts these two sources agree.
# ---------------------------------------------------------------------------
EFFECT_KINDS = {
    "Damage", "Heal", "Shield", "ApplyStatus", "Cleanse",
    "EnergyGain", "Dispel", "SwapPositions", "GainResolve",
    "TurnShift", "SwapTurnOrder", "RemoveStatus", "MoveDebuffs",
    "MoveBuffs", "InvertStatus", "LedgerRelease", "RecordFact",
    "Interrupt", "ExtendStatus", "ConsumeTurnDebt",
    "HpTransfer", "ShieldTransfer", "LedgerIncrement", "RedirectLethal",
    "SnapshotEnergy", "TransferStatus", "SnapshotHp", "LedgerDeposit",
    "LinkUnits", "EchoDamage",
}
STATUS_KINDS = {
    "None", "AttackUp", "AttackDown", "DefenseUp", "DefenseDown",
    "Burn", "Regen", "Shield", "Taunt", "Focus", "Slow",
    "Silence", "Stun", "Stealth", "Resolve",
    "SpeedUp", "BlockUp", "BlockDown",
    "DamageReductionUp", "DamageReductionDown",
    "EffectHitUp", "EffectHitDown",
    "EffectResistUp", "EffectResistDown",
    "CritDamageUp", "CritDamageDown",
    "SkillDamageUp", "SkillDamageDown",
    "ArmorBreakUp", "ArmorBreakDown",
    "Mark", "MarkAlt", "Ledger",
    "Covered", "Provoked", "Guard",
    "Vulnerable", "TurnDebt",
    "EvasionUp", "AccuracyDown",
    "GuaranteedCrit", "BuffSuppression",
    "Disrupted", "MaxHpUp", "HolyDamageUp",
    "HealSuppression", "StatusGainSuppression",
    "Resolve2",
    "Linked",
    # batch eight: the partial sibling of HealSuppression.
    "HealingReduction",
}
# A standing rule forbidding a unit one class of target. Declared on a passive as
# "targetBan"; the harness reads this set out of this file and compares it to Enums.cs,
# so adding a member there and not here is a failure rather than a silent drift.
TARGET_BANS = {"None", "LastTarget", "LastRoundTarget", "CarryingStatus"}
ASPECTS = {"Ember", "Tide", "Verdant", "Gale", "Umbra", "Lumen"}
HERO_ROLES = {
    "Bulwark", "Striker", "Breaker", "Mender", "Warden", "Stalker",
    "Vanguard", "Binder", "Marksman", "Tinkerer",
}
TARGET_MODES = {
    "Self", "LowestHpEnemy", "HighestHpEnemy", "HighestAttackEnemy",
    "FrontEnemy", "RandomEnemy", "AllEnemies", "LowestHpAlly",
    "AllAllies", "RandomAlly", "LastAttacker", "LastTarget",
    "EnemyWithStatus", "HighestEnergyEnemy", "HighestSpeedEnemy",
    "LowestSpeedEnemy", "LowestEnergyAlly", "TwoLowestHpAllies",
    "SecondLowestHpAlly", "AllyWithMostDebuffs", "EnemyWithMyMark",
    "DefeatedUnit", "ThresholdUnit", "BroadcastSubject",
    "ThreeLowestHpAllies", "HighestDamageDealtEnemy", "HighestDoTDealtEnemy",
    "BroadcastVictim",
    # HighestMyMarkStacksEnemy was in Enums.cs and the harness's own parity list but had
    # never been added here, so a kit authoring it validated as a bad TargetMode. Found and
    # fixed while adding the two below, because the harness parity check compares its own
    # hardcoded list against the C# enum and therefore cannot see this file drift.
    "HighestMyMarkStacksEnemy",
    "PreviousTarget", "LinkedUnit",
    # nine-gap pass: naming the other end of a link, the whole set, and the unit that
    # caused a broadcast.
    "OtherLinkedUnit", "AllLinkedUnits", "BroadcastSource",
    # the enemy carrying the most PENDING DoT, as against HighestDoTDealtEnemy
    # which is the one that has DEALT the most.
    "HighestPendingDoTEnemy", "LowestEnergyEnemy",
    # batch eight: the two bodies a SwapTurnOrder moved.
    "SwapAdvancedUnit", "SwapDelayedUnit",
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
    """Return a dict keyed by import-format slot names (basicAttack, activeSkill,
    passive1, passive2).  Accepts three shapes:
      - Generator format: skills block with those keys directly.
      - Engine format: flat keys basic, active, passive at the hero root.
      - Synthetic fixture format: flat keys basicAttack/activeSkill at root.

    An engine-format hero may ALSO carry a skills block, which the engine reads as
    extra passive slots (UnitDef.ReadPassives appends skills.passive1/passive2 after
    the flat ones).  Returning that block wholesale made this validator blind to
    every such hero: it saw no basicAttack, reported one, and then checked the star
    shape against a hero it could not see.  Seven of them were authored that way
    before anyone noticed, which is a guard reporting on nothing.  The block is now
    an overlay that cannot shadow a flat slot.
    """
    skills = hero.get("skills")
    if isinstance(skills, dict) and ("basicAttack" in skills or "activeSkill" in skills):
        return skills
    # Engine format uses basic/active/passive; map to import-format keys so
    # every downstream check uses one vocabulary.
    result = {}
    for import_key, engine_key in (("basicAttack", "basic"),
                                    ("activeSkill", "active"),
                                    ("passive1", "passive")):
        val = hero.get(import_key) or hero.get(engine_key)
        if val is not None:
            result[import_key] = val
    val = hero.get("passive2")
    if val is not None:
        result["passive2"] = val
    if isinstance(skills, dict):
        for key, val in skills.items():
            if val is not None:
                result.setdefault(key, val)
    return result


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

def check_required_fields(hero):
    """Fail on missing required fields. The engine silently defaults every one of
    these (verified against HeroDef.FromJson / UnitDef.ReadCommon), but a hero
    import that omits them is almost certainly a mistake rather than an intentional
    use of the default.

    Engine optionality (all have fallbacks):
      basicAttack — engine creates DefaultBasicAttack() if absent.
      growth      — defaults to 0.11.
      startEnergy — defaults to 0.
      visual      — VisualDef.FromJson handles null with defaults.
      aspect/role/stars/id/name — all default silently.
    """
    findings = []
    for key in ("id", "name", "aspect", "role", "stars"):
        if key not in hero or hero[key] is None:
            findings.append({"severity": "fail", "message": f"missing required field '{key}'"})

    if not isinstance(hero.get("visual"), dict):
        findings.append({"severity": "fail", "message": "missing required field 'visual'"})

    stats = hero.get("stats")
    has_stats_block = isinstance(stats, dict)
    if not has_stats_block and "hp" not in hero:
        findings.append({"severity": "fail", "message": "missing 'stats' block (or flat stat keys)"})

    stat_source = stats if has_stats_block else hero
    for key in ("growth", "startEnergy"):
        if key not in stat_source:
            findings.append({"severity": "fail", "message": f"missing required stat '{key}'"})

    skills = _get_skills(hero)
    if not is_set(skills.get("basicAttack")):
        findings.append({"severity": "fail", "message": "missing required 'basicAttack'"})

    return findings


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


def check_stat_values(hero):
    """Sanity bands on stat values. Fail on negatives and zeroes for core stats;
    warn on rate stats outside [0, 1] since a legitimate outlier is possible."""
    findings = []
    stats = hero.get("stats")
    if not isinstance(stats, dict):
        stats = hero

    for key in ("hp", "attack", "defense", "speed"):
        val = stats.get(key)
        if val is not None and val <= 0:
            findings.append({"severity": "fail",
                              "message": f"stats.{key} must be positive (got {val})"})

    for key in ("critChance", "effectHit", "effectResist"):
        val = stats.get(key)
        if val is not None:
            if val < 0:
                findings.append({"severity": "fail",
                                  "message": f"stats.{key} is negative ({val})"})
            elif val > 1:
                findings.append({"severity": "warn",
                                  "message": f"stats.{key} is above 1.0 ({val})"})

    return findings


def check_aspect(hero):
    """Aspect must be a member of the Aspect enum in Enums.cs."""
    aspect = hero.get("aspect")
    if aspect is not None and aspect not in ASPECTS:
        return [{"severity": "fail",
                 "message": f"aspect '{aspect}' is not a valid Aspect (Enums.cs)"}]
    return []


def check_role(hero):
    """Role must be a member of the HeroRole enum in Enums.cs."""
    role = hero.get("role")
    if role is not None and role not in HERO_ROLES:
        return [{"severity": "fail",
                 "message": f"role '{role}' is not a valid HeroRole (Enums.cs)"}]
    return []


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


def check_target_modes(hero):
    """Every effect target must be a member of the TargetMode enum in Enums.cs."""
    findings = []
    for slot, eff in collect_all_effects(hero):
        target = eff.get("target")
        if target is not None and target not in TARGET_MODES:
            findings.append({"severity": "fail",
                              "message": f"{slot}: target '{target}' is not a valid TargetMode (Enums.cs)"})
    return findings


def check_star_shape(hero):
    """Star-tier kit contract — severity fail.
    The star contract is authoritative: 1-star is basicAttack only; 2 adds
    passive1; 3 adds activeSkill; 4 makes passive2 optional; 5 requires
    passive2 and all three talents."""
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
            findings.append({"severity": "fail",
                              "message": "stars 1: should have activeSkill null, passive1 null, passive2 null, talents all null"})
    elif stars == 2:
        if active or not p1 or p2 or not t_null:
            findings.append({"severity": "fail",
                              "message": "stars 2: should have activeSkill null, exactly passive1 set, passive2 null, talents null"})
    elif stars == 3:
        if not active or not p1 or p2 or not t_null:
            findings.append({"severity": "fail",
                              "message": "stars 3: should have activeSkill set, exactly passive1 set, passive2 null, talents null"})
    elif stars == 4:
        if not active or not p1 or not t_null:
            findings.append({"severity": "fail",
                              "message": "stars 4: should have activeSkill set, passive1 set, passive2 optional, talents null"})
    elif stars == 5:
        if not active or not p1 or not p2 or not t_full:
            findings.append({"severity": "fail",
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
    findings += check_required_fields(hero)
    findings += check_stats(hero)
    findings += check_stat_values(hero)
    findings += check_aspect(hero)
    findings += check_role(hero)
    findings += check_effect_kinds(hero)
    findings += check_status_kinds(hero)
    findings += check_target_modes(hero)
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

    # Cross-hero: duplicate id check
    id_counts = {}
    for hero in heroes:
        hid = hero_id(hero)
        id_counts[hid] = id_counts.get(hid, 0) + 1
    dupes = {k: v for k, v in id_counts.items() if v > 1}
    if dupes:
        print("\nDUPLICATE IDS")
        for hid, count in sorted(dupes.items()):
            print(f"  [FAIL] '{hid}' appears {count} times")
            totals["fail"] += 1
            any_fail = True

    print()
    print(f"Summary: {totals['fail']} fail  {totals['warn']} warn  {totals['info']} info")
    sys.exit(1 if any_fail else 0)


if __name__ == "__main__":
    main()
