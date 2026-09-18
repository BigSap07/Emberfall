#!/usr/bin/env python3
"""Migrate dormant effects (conditionValue 99) to talent appends."""
import json, sys, copy

HEROES_PATH = "Assets/Resources/Content/heroes.json"

# Each entry: (hero_id, ability_slot, effect_index, talent_slot)
# The talent_slot's override with delta -99 on conditionValue is what wakes the dormant effect.
# We move the dormant effect to the talent's appends list (stripping the sentinel condition)
# and remove the -99 override.
MIGRATIONS = [
    ("kael_the_unyielding",         "active",   3, "talentII"),
    ("marrow_the_irrevocable_vow",  "active",   3, "talentI"),
    ("marrow_the_irrevocable_vow",  "active",   4, "talentI"),
    ("seraphine_the_closed_volume", "active",   6, "talentII"),
    ("isara_the_second_chance",     "passive2", 7, "talentII"),
    ("aeron_the_unbroken_stride",   "passive2", 1, "talentII"),
    ("cyrra_the_burden_bearer",     "active",   7, "talentI"),
    ("cyrra_the_burden_bearer",     "active",   8, "talentI"),
]

def main():
    with open(HEROES_PATH, "r", encoding="utf-8") as f:
        data = json.load(f)

    heroes = data["heroes"]
    lookup = {h["id"]: h for h in heroes}

    migrated = 0
    # Group by hero to handle index shifts when removing multiple effects from same ability
    by_hero_ability = {}
    for hero_id, ability_slot, effect_index, talent_slot in MIGRATIONS:
        key = (hero_id, ability_slot)
        if key not in by_hero_ability:
            by_hero_ability[key] = []
        by_hero_ability[key].append((effect_index, talent_slot))

    for (hero_id, ability_slot), entries in by_hero_ability.items():
        hero = lookup[hero_id]
        ability = hero[ability_slot]
        effects = ability["effects"]

        # Sort by effect_index DESCENDING so removal doesn't shift later indices
        entries.sort(key=lambda x: x[0], reverse=True)

        for effect_index, talent_slot in entries:
            effect = effects[effect_index]
            assert effect.get("conditionValue") == 99, \
                f"{hero_id}/{ability_slot}[{effect_index}] conditionValue is {effect.get('conditionValue')}, expected 99"

            # Build the appended effect: strip the sentinel condition
            appended = copy.deepcopy(effect)
            # The sentinel is always: condition=StatusStacksAtLeast, conditionStatus=Resolve,
            # conditionValue=99, conditionOn=Caster. Some have a condition2 that is the REAL
            # condition (AND gate). If there's a condition2, promote it to condition.
            if "condition2" in appended:
                appended["condition"] = appended.pop("condition2")
                appended["conditionOn"] = appended.pop("conditionOn2")
                if "conditionStatus2" in appended:
                    appended["conditionStatus"] = appended.pop("conditionStatus2")
                else:
                    appended.pop("conditionStatus", None)
                if "conditionValue2" in appended:
                    appended["conditionValue"] = appended.pop("conditionValue2")
                else:
                    appended.pop("conditionValue", None)
            else:
                # No condition2 — the effect is unconditional when the talent is active
                appended.pop("condition", None)
                appended.pop("conditionStatus", None)
                appended.pop("conditionValue", None)
                appended.pop("conditionOn", None)

            # Add to talent's appends list
            talent = hero["talents"][talent_slot]
            if "appends" not in talent:
                talent["appends"] = []
            talent["appends"].append({
                "ability": ability_slot,
                "effect": appended
            })

            # Remove the -99 override from the talent
            if "overrides" in talent:
                talent["overrides"] = [
                    ov for ov in talent["overrides"]
                    if not (ov.get("ability") == ability_slot
                            and ov.get("effectIndex") == effect_index
                            and ov.get("field") == "conditionValue"
                            and ov.get("delta") == -99)
                ]
                if not talent["overrides"]:
                    del talent["overrides"]

            # Remove the dormant effect from the ability
            effects.pop(effect_index)
            migrated += 1

            # Fix remaining overrides that reference effect indices ABOVE the removed one
            # in the same ability, for ALL talents on this hero
            for tslot in ["talentI", "talentII", "talentIII"]:
                t = hero["talents"].get(tslot)
                if not t:
                    continue
                for ov in t.get("overrides", []):
                    if ov.get("ability") == ability_slot and ov.get("effectIndex") > effect_index:
                        ov["effectIndex"] -= 1
                # Also fix appends that we already added which reference other effect indices
                # (not needed since appends don't reference indices, but check overrides)

            print(f"  migrated {hero_id}/{ability_slot}[{effect_index}] -> {talent_slot} appends")

    with open(HEROES_PATH, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")

    print(f"Done. {migrated} dormant effect(s) migrated.")

if __name__ == "__main__":
    main()
