# -*- coding: utf-8 -*-
"""PART 2B - what the delivery says per clip against what the runtime uses.

Reads sprite_sheets.json straight out of each delivery zip (no extraction) and compares its
per-clip defaultAnimation against the tracked sidecar the loader actually reads.
"""
import io, json, os, re, zipfile
from collections import OrderedDict

ZIPS = r"D:\Emberfall\import\sprites"
UNITS = r"D:\Emberfall\Game\Assets\Resources\Sprites\Units"

CLIPS = ["idle", "basic", "basic2", "skill", "hit_reaction", "death", "controlled"]

# engine clip name -> the token the delivery manifest uses
TOKEN = {"idle": "idle", "basic": "basic", "basic2": "basic2", "skill": "skill",
         "hit_reaction": "hit_reaction", "death": "death", "controlled": "controlled"}

# hero ids that have tracked sidecars, grouped by their first name token
ids = {}
for f in os.listdir(UNITS):
    m = re.match(r"^(.*)_(idle|basic2|basic|skill|hit_reaction|death|controlled)\.json$", f)
    if not m:
        continue
    hid = m.group(1)
    ids.setdefault(hid.split("_")[0], set()).add(hid)

def sidecar(hid, clip):
    p = os.path.join(UNITS, "%s_%s.json" % (hid, clip))
    if not os.path.exists(p):
        return None
    return json.load(io.open(p, encoding="utf-8"))

def manifest_of(zpath):
    with zipfile.ZipFile(zpath) as z:
        names = [n for n in z.namelist() if n.lower().endswith("sprite_sheets.json")]
        if not names:
            return None, None
        with z.open(names[0]) as fh:
            return json.loads(fh.read().decode("utf-8")), names[0]

rows = []
unmatched = []
delivered_fps = {}
for fn in sorted(os.listdir(ZIPS)):
    if not fn.lower().endswith(".zip"):
        continue
    short = os.path.splitext(fn)[0]
    man, where = manifest_of(os.path.join(ZIPS, fn))
    if man is None:
        unmatched.append("%s: no sprite_sheets.json inside" % fn)
        continue
    sprites = man.get("sprites", [])

    # which hero id this delivery wired into: prefer the short name, else the image prefix
    cand = ids.get(short.lower())
    if not cand and sprites:
        pref = str(sprites[0].get("image", "")).split("_")[0].lower()
        cand = ids.get(pref)
    if not cand:
        unmatched.append("%s: no tracked sidecars under '%s'" % (fn, short.lower()))
        continue
    hid = sorted(cand)[0]

    for clip in CLIPS:
        tok = TOKEN[clip]
        entry = None
        for sp in sprites:
            img = str(sp.get("image", ""))
            name = str(sp.get("name", ""))
            stem = os.path.splitext(img)[0]
            # match by the trailing token, so celen_basic1.png and kael_basic.png both land
            t = stem.split("_", 1)[1] if "_" in stem else stem
            t = {"basic1": "basic", "basic_2": "basic2", "basic_1": "basic"}.get(t, t)
            n = {"basic1": "basic"}.get(name, name)
            if t == tok or n == tok:
                entry = sp
                break
        side = sidecar(hid, clip)
        if entry is None and side is None:
            continue
        if entry is None:
            rows.append((hid, clip, "no manifest entry", "sidecar present", ""))
            continue
        if side is None:
            rows.append((hid, clip, "manifest entry", "NO SIDECAR - clip not wired", ""))
            continue

        da = entry.get("defaultAnimation") or {}
        sa = side.get("defaultAnimation") or {}
        delivered_fps.setdefault(round(float(da.get("fps", 30)), 3), 0)
        delivered_fps[round(float(da.get("fps", 30)), 3)] += 1

        for key, default in (("fps", 30), ("start", 0), ("end", None),
                             ("impactFraction", None), ("playbackSpeed", None),
                             ("footFraction", None)):
            if key not in da:
                continue
            want = da[key]
            got = sa.get(key, "(absent)")
            if isinstance(want, float) or isinstance(got, float):
                same = isinstance(got, (int, float)) and abs(float(want) - float(got)) < 1e-4
            else:
                same = want == got
            if not same:
                rows.append((hid, clip, key, want, got))

def pacing_report():
    """What the RUNTIME actually plays, which is the question the delivery cannot answer.

    The deliveries carry no playbackSpeed and no impactFraction at all, so comparing against them
    can never show a pacing problem. This section reads the tracked sidecars on their own terms:
    which speeds are in play across the roster, and whether every attack clip carries its own
    measured impact rather than a seeded constant.
    """
    heroes = {}
    for f in sorted(os.listdir(UNITS)):
        m = re.match(r"^(.*)_(idle|basic2|basic|skill|hit_reaction|death|controlled)\.json$", f)
        if not m:
            continue
        hid, clip = m.group(1), m.group(2)
        a = json.load(io.open(os.path.join(UNITS, f), encoding="utf-8")).get("defaultAnimation") or {}
        heroes.setdefault(hid, {})[clip] = a

    print("")
    print("=" * 78)
    print("RUNTIME PACING - what the sidecars tell the loader to play")
    print("=" * 78)

    speeds = {}
    seeded = []
    hand = 0
    own = 0
    attacks = 0
    stray = []
    for hid in sorted(heroes):
        for clip, a in sorted(heroes[hid].items()):
            sp = a.get("playbackSpeed", 1.0)
            speeds.setdefault(round(float(sp), 3), []).append(hid)
            if clip in ("basic", "basic2", "skill"):
                attacks += 1
                imp = a.get("impactFraction")
                if imp is None or abs(float(imp) - 0.5) < 1e-9:
                    seeded.append("%s %s" % (hid, clip))
                else:
                    own += 1
                if a.get("impactSetBy") == "hand":
                    hand += 1
            elif "impactFraction" in a:
                stray.append("%s %s" % (hid, clip))

    print("playback speeds in use:")
    for sp in sorted(speeds):
        who = sorted(set(speeds[sp]))
        print("  %.2fx on %d clip(s) across %d hero(es)%s"
              % (sp, len(speeds[sp]), len(who),
                 "" if len(who) > 6 else ": " + ", ".join(w.split("_")[0] for w in who)))

    print("")
    print("attack clips with their own impact: %d of %d   (%d set by hand off a contact sheet)"
          % (own, attacks, hand))
    if seeded:
        print("  still on the seeded 0.5 (or absent): " + ", ".join(seeded))
    if stray:
        print("  impactFraction on a non-attack clip (the 25 carry none): " + ", ".join(stray))
    print("")
    print("%d animated hero(es); %s"
          % (len(heroes),
             "all on one speed" if len(speeds) == 1 else "%d different speeds" % len(speeds)))


print("=" * 78)
print("CLIP DATA AUDIT - delivery manifest against the tracked sidecar the loader reads")
print("=" * 78)
print("fps values present in the deliveries: %s"
      % ", ".join("%g x%d" % (k, v) for k, v in sorted(delivered_fps.items())))
print("")
if unmatched:
    print("deliveries not compared:")
    for u in unmatched:
        print("  " + u)
    print("")
if not rows:
    print("NO MISMATCHES. Every value the deliveries specify is what the runtime uses.")
else:
    print("%-30s %-13s %-16s %-12s %s" % ("hero", "clip", "field", "delivered", "runtime"))
    for r in rows:
        print("%-30s %-13s %-16s %-12s %s" % (r[0][:30], r[1], str(r[2])[:16], str(r[3])[:12], r[4]))
    print("")
    print("%d mismatch(es)" % len(rows))

pacing_report()
