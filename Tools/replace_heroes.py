#!/usr/bin/env python3
"""Replace Edrin and Zella stubs with full kits, remove duplicates."""
import json

HEROES_PATH = "Assets/Resources/Content/heroes.json"

with open(HEROES_PATH, "r", encoding="utf-8") as f:
    data = json.load(f)

heroes = data["heroes"]

# Find the full kits (appended at end) and the stubs
full_edrin = None
full_zella = None
remove_indices = []

for i, h in enumerate(heroes):
    if h["id"] == "edrin_the_distant_bell":
        if "passive1" in h:
            full_edrin = h
            remove_indices.append(i)
        else:
            edrin_stub_idx = i
    elif h["id"] == "zella_the_slow_argument":
        if "passive1" in h:
            full_zella = h
            remove_indices.append(i)
        else:
            zella_stub_idx = i

assert full_edrin is not None, "Full Edrin not found"
assert full_zella is not None, "Full Zella not found"

# Replace stubs with full kits
heroes[edrin_stub_idx] = full_edrin
heroes[zella_stub_idx] = full_zella

# Remove the appended duplicates (iterate in reverse to preserve indices)
for idx in sorted(remove_indices, reverse=True):
    heroes.pop(idx)

with open(HEROES_PATH, "w", encoding="utf-8", newline="\n") as f:
    json.dump(data, f, indent=2, ensure_ascii=False)
    f.write("\n")

print(f"Replaced stubs. Total heroes: {len(heroes)}")
# Verify no duplicates
ids = [h["id"] for h in heroes]
dupes = [x for x in ids if ids.count(x) > 1]
if dupes:
    print(f"WARNING: duplicates remain: {set(dupes)}")
else:
    print("No duplicates.")
