#!/usr/bin/env python3
"""R20 UI KIT IMPORT: copies the generator's import-ready kit into Assets/Resources/Art/UIKit and writes
the .meta files by hand, so the import settings (sprite mode, borders, pixels per unit, size,
compression) are deterministic and do not depend on what Unity guesses.

Why it lives under Resources and not Assets/Art: the UI loads a piece by key with Resources.Load
(UiKit.Get), exactly as HudSkin and EmberforgeSkin load theirs, because this project builds its UI in
code and has no prefab or scene to hold a reference. Tools/texture-import-policy.py states the cost
(everything under Resources ships) and every .meta here follows its trap note: DefaultTexturePlatform
carries textureCompression 0, so each file gets explicit Standalone (BC7) and Android (ASTC 4x4)
entries or it would ship as RGBA32.

Inputs, all outside the repo and never written to:
  D:/Emberfall/ui-gen/picked/MANIFEST.json     what is picked (316 pieces). The kit is wired from this: every
                                               non-ability piece must have an import file, every ability a
                                               cell in the atlas map, or this script says so and exits 1.
  D:/Emberfall/ui-gen/r20/import/              the generator's import-ready set: kit/, store/, relics/ at the
                                               sizes they ship at, ability_atlas.png (109 unique icons cover
                                               all 254 abilities) with ability_atlas.json, and
                                               nineslice-unity.json (borders as left, bottom, right, top).

Abilities are ONE texture (the atlas), cut into sprites at run time by UiKit from ability_atlas.json, so
there is no per-ability file or meta. Everything else is one Single sprite per file.

Re-runnable: it overwrites only what it wrote. Run:  python Tools/uikit-import.py [--dry]
"""
import hashlib
import json
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
PICKED = r"D:\Emberfall\ui-gen\picked"
IMPORT = r"D:\Emberfall\ui-gen\r20\import"
DEST = os.path.join(ROOT, "Assets", "Resources", "Art", "UIKit")
TEMPLATE = os.path.join(ROOT, "Assets", "Resources", "Art", "UI", "panel.png.meta")

DRY = "--dry" in sys.argv

# UnityEditor.TextureImporterFormat values, as in Tools/texture-import-policy.py.
BC7 = 25
ASTC_4x4 = 48

# How thick a sliced piece's border should DRAW, in canvas units, so it matches the baked kit it replaces
# (forge_panel's border is 34 to 64, a button's 24 to 30). Pixels per unit is what sets it: a border of
# b pixels draws at b * 100 / ppu units. frame: by the left border. plate: by the sprite's height, the
# natural height the control is drawn at. A piece not listed is never sliced.
MIDDLE = 6          # pixels of flat plate left between two borders that would otherwise meet
DRAWN_LEFT_BORDER = {"panel_large": 34.0, "panel_small": 26.0, "popup": 30.0}
NATURAL_HEIGHT = {"btn_primary": 90.0, "btn_secondary": 90.0, "btn_disabled": 90.0, "tab_on": 72.0, "tab_off": 72.0,
                  "pill_currency": 64.0, "bar_frame": 36.0, "banner_title": 150.0}


def guid_for(rel):
    return hashlib.md5(("emberfall/uikit/" + rel.replace("\\", "/")).encode("utf-8")).hexdigest()


def png_size(path):
    import struct
    with open(path, "rb") as f:
        head = f.read(32)
    return struct.unpack(">II", head[16:24])


def max_texture_size(key):
    """The ceiling Tools/texture-import-policy.py states for the folder a key lives in. The files are already at
    their shipping size, so a ceiling at or above it never resamples; stating the policy's number keeps the
    policy script's --report at zero files to write."""
    if key.startswith("store/"):
        return 1024
    if key.startswith("relics/"):
        return 256
    return 2048


def read_template():
    with open(TEMPLATE, "r", encoding="utf-8") as f:
        return f.read()


def meta_for(template, guid, max_size, border, ppu):
    s = template
    s = re.sub(r"^guid: .*$", "guid: " + guid, s, count=1, flags=re.M)
    s = re.sub(r"maxTextureSize: \d+", "maxTextureSize: %d" % max_size, s)
    s = re.sub(r"spriteBorder: \{.*\}", "spriteBorder: {x: %d, y: %d, z: %d, w: %d}" % border, s)
    s = re.sub(r"spritePixelsToUnits: [\d.]+", "spritePixelsToUnits: %s" % ("%g" % ppu), s)
    # The template's Android entry is ASTC 4x4 already and its Windows entry BC7; stated here so a template
    # change cannot move them silently.
    s = re.sub(r"(buildTarget: Android\n(?:    .*\n)*?    textureFormat: )\d+", r"\g<1>%d" % ASTC_4x4, s)
    s = re.sub(r"(buildTarget: Standalone\n(?:    .*\n)*?    textureFormat: )\d+", r"\g<1>%d" % BC7, s)
    # the template's spriteID belongs to another asset; Unity writes this one's on first import
    s = re.sub(r"^    spriteID: .*\n", "", s, flags=re.M)
    return s


FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

TEXT_META = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def write(path, data, binary=False):
    if DRY:
        return
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if binary:
        with open(path, "wb") as f:
            f.write(data)
    else:
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(data)


def border_and_ppu(key, size, nine):
    entry = nine.get(key)
    if entry is None or entry["mode"] in ("uniform", "stretch", "slice_mild"):
        return (0, 0, 0, 0), 100.0      # shown whole (icons, round pieces, card frames, bar fills)
    l, b, r, t = [int(v) for v in entry["border_lbrt"]]
    w, h = size
    # A border pair that covers the whole side leaves a middle of ZERO pixels, and a control taller than the
    # sprite's natural height then stretches that nothing into a hairline across the plate (seen in the
    # nine-slice simulation of buttons, tabs and the pill). Keep a few pixels of flat plate to stretch.
    for _ in range(2):
        if t + b > h - MIDDLE:
            cut = (t + b - (h - MIDDLE) + 1) // 2
            t, b = t - cut, b - cut
        if l + r > w - MIDDLE:
            cut = (l + r - (w - MIDDLE) + 1) // 2
            l, r = l - cut, r - cut
    if key in DRAWN_LEFT_BORDER:
        ppu = 100.0 * l / DRAWN_LEFT_BORDER[key]
    else:
        ppu = 100.0 * h / NATURAL_HEIGHT[key]
    return (l, b, r, t), max(100.0, ppu)


def main():
    with open(os.path.join(PICKED, "MANIFEST.json"), "r", encoding="utf-8") as f:
        manifest = json.load(f)
    with open(os.path.join(IMPORT, "nineslice-unity.json"), "r", encoding="utf-8") as f:
        nine = json.load(f)["pieces"]
    with open(os.path.join(IMPORT, "ability_atlas.json"), "r", encoding="utf-8") as f:
        atlas = json.load(f)
    template = read_template()

    problems = []
    report = []
    folders = set([""])
    written = 0

    # 1. every manifest piece that is not an ability must have an import file
    wanted = []
    for piece in manifest["pieces"]:
        if piece["group"] == "abilities":
            continue
        key = os.path.splitext(piece["file"].replace("\\", "/"))[0]
        wanted.append(key)
    # the open card frames are the generator's addition (a frame with its centre cut out, for over a portrait)
    for n in range(1, 6):
        wanted.append("card_frame_%d_open" % n)

    for key in wanted:
        src = os.path.join(IMPORT, (key + ".png").replace("/", os.sep))
        if "/" not in key:
            src = os.path.join(IMPORT, "kit", key + ".png")
        if not os.path.isfile(src):
            problems.append("MISSING import file for " + key)
            continue
        with open(src, "rb") as f:
            raw = f.read()
        w, h = png_size(src)
        base = os.path.basename(key)
        border, ppu = border_and_ppu(base, (w, h), nine)
        out_png = os.path.join(DEST, key.replace("/", os.sep) + ".png")
        write(out_png, raw, binary=True)
        write(out_png + ".meta", meta_for(template, guid_for(key + ".png"), max_texture_size(key), border, ppu))
        folders.add(os.path.dirname(key))
        written += 1
        report.append("%-40s %4dx%-4d border=%s ppu=%g" % (key, w, h, border, ppu))

    # 2. the ability atlas: one texture, every manifest ability has a cell
    atlas_src = os.path.join(IMPORT, "ability_atlas.png")
    aw, ah = png_size(atlas_src)
    with open(atlas_src, "rb") as f:
        write(os.path.join(DEST, "ability_atlas.png"), f.read(), binary=True)
    write(os.path.join(DEST, "ability_atlas.png.meta"),
          meta_for(template, guid_for("ability_atlas.png"), max_texture_size("ability_atlas"), (0, 0, 0, 0), 100.0))
    written += 1
    report.append("%-40s %4dx%-4d (atlas, %d unique icons)" % ("ability_atlas", aw, ah, atlas["unique_icons"]))

    amap = atlas["map"]
    covered = 0
    for piece in manifest["pieces"]:
        if piece["group"] != "abilities":
            continue
        name = os.path.basename(piece["file"])
        if name in amap:
            covered += 1
        else:
            problems.append("no atlas cell for " + name)
    # UiKit reads the map as JSON: name -> x, y_unity, w, h. Written compact, stable order.
    compact = {"size": atlas["size"], "cell": atlas["cell"], "map": {}}
    for name in sorted(amap):
        e = amap[name]
        compact["map"][os.path.splitext(name)[0]] = [e["x"], e["y_unity"], e["w"], e["h"]]
    write(os.path.join(DEST, "ability_atlas_map.json"), json.dumps(compact, separators=(",", ":")) + "\n")
    write(os.path.join(DEST, "ability_atlas_map.json.meta"), TEXT_META.format(guid=guid_for("ability_atlas_map.json")))

    # 3. folder metas, every folder from UIKit down
    all_folders = set([""])
    for fdr in folders:
        parts = fdr.split("/") if fdr else []
        for i in range(1, len(parts) + 1):
            all_folders.add("/".join(parts[:i]))
    for fdr in sorted(all_folders):
        path = DEST if not fdr else os.path.join(DEST, fdr.replace("/", os.sep))
        write(path + ".meta", FOLDER_META.format(guid=guid_for("folder:" + fdr)))

    print("textures written: %d, ability cells covered: %d of %d, folders: %d%s" % (
        written, covered, sum(1 for p in manifest["pieces"] if p["group"] == "abilities"), len(all_folders),
        " (dry run)" if DRY else ""))
    for line in report:
        print("  " + line)
    for line in problems:
        print("  PROBLEM: " + line)
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
