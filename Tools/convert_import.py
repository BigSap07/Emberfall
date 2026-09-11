#!/usr/bin/env python3
"""One-shot converter: import/Heroes.json → engine-format heroes.json.
Run once, delete after. Not part of the build."""

import json, sys, collections

# ── aspect palette (derived from UITheme.AspectColor, darkened for body tones) ─
ASPECT_COLORS = {
    "Ember":   {"primary": "8B3A20", "secondary": "2A2020", "accent": "D09060"},
    "Tide":    {"primary": "2A5A80", "secondary": "1A2530", "accent": "70B0D0"},
    "Verdant": {"primary": "306040", "secondary": "1A2A1E", "accent": "80C888"},
    "Gale":    {"primary": "3A8090", "secondary": "1A2A2E", "accent": "90D8E0"},
    "Umbra":   {"primary": "503080", "secondary": "201828", "accent": "A880D0"},
    "Lumen":   {"primary": "203870", "secondary": "181A28", "accent": "6090E0"},
}

STARTERS = {"kael_the_unyielding", "isara_the_second_chance", "kiva_the_forsaken_oath"}

GATED = ("skillDamage", "block", "armorBreak", "controlImmunity", "damageReduction", "holyDamage")

# ── silhouette mapping ──────────────────────────────────────────────────────────
# Engine vocabulary: humanoid | brute | beast | wisp | golem
def map_silhouette(sil):
    s = sil.lower()
    if any(k in s for k in ("massive", "ogre", "troll", "ursan")):
        return "brute", True
    return "humanoid", s == "humanoid"

# ── weapon mapping ──────────────────────────────────────────────────────────────
# Engine vocabulary: sword | greatsword | spear | staff | dagger | orb | hammer | claws | none
# Range detection: bow | spellbook also recognised
def map_weapon(w):
    wl = w.lower()
    # ranged first
    if any(k in wl for k in ("longbow", "shortbow", "crossbow", "recurve")):
        return "bow", True
    if "bow" in wl and "elbow" not in wl:
        return "bow", True
    if any(k in wl for k in ("gun", "pistol", "rifle", "carbine", "launcher", "nailgun", "scattergun")):
        return "bow", True
    if any(k in wl for k in ("spellbook", "codex")):
        return "spellbook", True
    # melee/caster
    if any(k in wl for k in ("staff", "censer", "rod", "scepter", "cane", "flute", "violin",
                               "handbell", "bell_staff", "lantern")):
        return "staff", True
    if any(k in wl for k in ("battleaxe", "execution_blade")):
        return "greatsword", True
    if any(k in wl for k in ("halberd", "glaive", "polearm", "lance")):
        return "spear", True
    if "spear" in wl:
        return "spear", True
    if any(k in wl for k in ("dagger", "stiletto", "needle_blade", "needle_dagger", "bone_saw",
                               "fan", "spindle", "whip", "baton")):
        return "dagger", True
    if any(k in wl for k in ("hammer", "mace", "flail", "warhammer", "maul", "ram", "pick")):
        return "hammer", True
    if any(k in wl for k in ("hook", "chain", "claw", "tow")):
        return "claws", True
    if any(k in wl for k in ("ring", "tablet", "floating")):
        return "orb", True
    if any(k in wl for k in ("sword", "blade", "saber", "sabre")):
        return "sword", True
    if "shield" in wl:
        return "sword", False  # shield-only, defaulted
    return "sword", False  # unmatched

# ── main ────────────────────────────────────────────────────────────────────────
with open(sys.argv[1], encoding="utf-8") as f:
    data = json.load(f)

heroes = data["heroes"]
output_heroes = []
gated_zeroed = 0
races_dropped = collections.Counter()
sil_map = collections.Counter()
sil_defaulted = []
wep_map = collections.Counter()
wep_defaulted = []
star_counts = collections.Counter()
aspect_counts = collections.Counter()
role_counts = collections.Counter()

# stat range tracking for report
stat_ranges = {}

for h in heroes:
    stats = h["stats"]
    hid = h["id"]

    # ── convert base four ──
    hp      = round(stats["hp"] * 3.9)
    attack  = round(stats["attack"] * 5)
    defense = round(stats["defense"] * 3.1)
    speed   = round(stats["speed"] * 0.5)

    # ── percent stats (/100) ──
    critChance  = round(stats["critChance"] / 100, 2)
    critDamage  = round(1 + stats["critDamage"] / 100, 2)
    effectHit   = round(stats["effectHit"] / 100, 2)
    effectResist = round(stats["effectResist"] / 100, 2)

    # ── gated stats: count nonzero, then zero ──
    for gs in GATED:
        if stats.get(gs, 0) != 0:
            gated_zeroed += 1

    # ── track dropped race ──
    if "race" in h:
        races_dropped[h["race"]] += 1

    role = h["role"]
    aspect = h["aspect"]
    stars = h["stars"]
    star_counts[stars] += 1
    aspect_counts[aspect] += 1
    role_counts[role] += 1

    startEnergy = 15 if role == "Mender" else 0

    # ── track ranges ──
    converted = {"hp": hp, "attack": attack, "defense": defense, "speed": speed,
                 "critChance": critChance, "critDamage": critDamage,
                 "effectHit": effectHit, "effectResist": effectResist}
    for k, v in converted.items():
        lo, hi = stat_ranges.get(k, (v, v))
        stat_ranges[k] = (min(lo, v), max(hi, v))

    # ── visual ──
    colors = ASPECT_COLORS[aspect]
    vis_import = h.get("visual", {})
    sil_raw = vis_import.get("silhouette", "humanoid")
    wep_raw = vis_import.get("weapon", "sword")

    sil, sil_matched = map_silhouette(sil_raw)
    wep, wep_matched = map_weapon(wep_raw)
    sil_map[sil] += 1
    wep_map[wep] += 1
    if not sil_matched:
        sil_defaulted.append(f"  {hid}: {sil_raw!r} -> {sil}")
    if not wep_matched:
        wep_defaulted.append(f"  {hid}: {wep_raw!r} -> {wep}")

    visual = {
        "primary": colors["primary"],
        "secondary": colors["secondary"],
        "accent": colors["accent"],
        "silhouette": sil,
        "weapon": wep,
        "scale": vis_import.get("scale", 1.0),
        "sigil": None,
        "sprite": hid,
        "spriteBodyFraction": 0.53,
        "spriteFootFraction": 0.23,
        "spriteMirror": True,
    }

    # ── placeholder abilities ──
    basic = {
        "id": hid + "_placeholder_basic",
        "name": "Strike (placeholder)",
        "effects": [{"kind": "Damage", "target": "FrontEnemy", "power": 1.0}]
    }

    out = {
        "id": hid,
        "name": h["name"],
        "role": role,
        "aspect": aspect,
        "stars": stars,
        "startingHero": hid in STARTERS,
        "hp": hp, "attack": attack, "defense": defense, "speed": speed,
        "critChance": critChance, "critDamage": critDamage,
        "growth": 0.115, "startEnergy": startEnergy,
        "effectHit": effectHit, "effectResist": effectResist,
        "skillDamage": 0, "block": 0, "armorBreak": 0,
        "controlImmunity": 0, "damageReduction": 0, "holyDamage": 0,
        "visual": visual,
        "basic": basic,
    }

    if stars >= 3:
        out["active"] = {
            "id": hid + "_placeholder_active",
            "name": "Sweep (placeholder)",
            "energyCost": 100,
            "effects": [{"kind": "Damage", "target": "AllEnemies", "power": 1.5}]
        }

    output_heroes.append(out)

# ── write output ──
with open(sys.argv[2], "w", encoding="utf-8") as f:
    json.dump({"heroes": output_heroes}, f, indent=2)

# ── report ──
print(f"Converted {len(output_heroes)} heroes")
print(f"\nStar distribution: " + ", ".join(f"{s}-star: {star_counts[s]}" for s in sorted(star_counts)))
print(f"Per aspect: " + ", ".join(f"{a}: {aspect_counts[a]}" for a in sorted(aspect_counts)))
print(f"Per role:   " + ", ".join(f"{r}: {role_counts[r]}" for r in sorted(role_counts)))
print(f"\nGated stats zeroed: {gated_zeroed} nonzero values across {len(output_heroes)} heroes")
print(f"Races dropped: {len(races_dropped)} unique values ({sum(races_dropped.values())} total entries)")
print(f"\nConverted stat ranges:")
for k in ("hp", "attack", "defense", "speed", "critChance", "critDamage", "effectHit", "effectResist"):
    lo, hi = stat_ranges[k]
    print(f"  {k}: {lo} - {hi}")

print(f"\nSilhouette mapping ({sum(sil_map.values())} total):")
for k, v in sorted(sil_map.items()):
    print(f"  {k}: {v}")
if sil_defaulted:
    print(f"Silhouette defaulted ({len(sil_defaulted)}):")
    for s in sil_defaulted:
        print(s)
else:
    print("Silhouette: all matched")

print(f"\nWeapon mapping ({sum(wep_map.values())} total):")
for k, v in sorted(wep_map.items()):
    print(f"  {k}: {v}")
if wep_defaulted:
    print(f"Weapon defaulted ({len(wep_defaulted)}):")
    for s in wep_defaulted:
        print(s)
else:
    print("Weapon: all matched")

print(f"\nStarters: {[h['id'] for h in output_heroes if h['startingHero']]}")
print(f"Menders (startEnergy=15): {[h['id'] for h in output_heroes if h['startEnergy']==15]}")
