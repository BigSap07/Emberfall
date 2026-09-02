#!/usr/bin/env python3
"""Generate head-and-shoulders portraits from the hero idle sheets.

    python Tools/make_portraits.py                  # the 5 heroes with animated sheets
    python Tools/make_portraits.py --include-singles # + the 6 placeholder single-image heroes
    python Tools/make_portraits.py --dry-run        # measure and report, write nothing

Writes Assets/Resources/Sprites/Units/portrait_<heroid>.png, 256x256 RGBA.

Committed rather than left as a throwaway for one specific reason: the script that imported
Kael's sheets was a one-off too, was never committed, and no longer exists anywhere - every quirk
it knew had to be rediscovered from its output. A one-off that produces committed assets is worth
the few lines it takes to keep.

--------------------------------------------------------------------------------------------
Resources loading pattern (BattleAssets.cs)
--------------------------------------------------------------------------------------------
Unity loads anything under an Assets/**/Resources/ folder by an EXTENSIONLESS path relative to
that folder. BattleAssets.UnitSprite does Resources.Load<Texture2D>("Sprites/Units/" + spriteId),
and the animated path does the same for "<spriteId>_<clip>" plus a TextAsset sidecar of the same
name. So these files land beside the sheets in Assets/Resources/Sprites/Units/ and would be read
as Resources.Load<Texture2D>("Sprites/Units/portrait_<heroid>").

Note the id used is the HERO id from heroes.json, as asked. That happens to equal visual.sprite
for all eleven heroes today, but they are different fields and only the hero id is guaranteed to
be the thing heroes.json keys on - so the sprite id is what gets read off disk, and the hero id is
what gets written.

--------------------------------------------------------------------------------------------
Crop geometry
--------------------------------------------------------------------------------------------
Per the spec: square, side = 45% of the alpha bounding box WIDTH, centred horizontally on the
centroid of opaque pixels in the top 20% of the bbox (the head, rather than the bbox centre -
these poses are asymmetric, with cloaks and weapons pulling the bbox sideways), top edge slightly
above the topmost opaque row.

Two judgement calls the spec left open, both flagged in the report rather than buried:
  - "slightly above" is HEADROOM_FRAC of the crop side (default 6%).
  - A crop running past the frame edge is PADDED with transparency, not clamped inward. Clamping
    would silently slide the head off-centre, which is the one thing the centroid step exists to
    prevent. Any padding is reported per hero.

Alpha threshold matches Tools/import-sprite-sheets.ps1 (>8) so both tools agree on what "opaque"
means and a bbox measured here matches the contentBox already in each sidecar.
"""

import argparse
import json
import os
import re
import sys

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow is required:  python -m pip install Pillow")

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UNITS = os.path.join(REPO, "Assets", "Resources", "Sprites", "Units")
HEROES = os.path.join(REPO, "Assets", "Resources", "Content", "heroes.json")

ALPHA_THRESHOLD = 8      # matches import-sprite-sheets.ps1
CROP_FRAC = 0.45         # square side, as a fraction of bbox width
TOP_BAND_FRAC = 0.20     # the "head" band, as a fraction of bbox height
HEADROOM_FRAC = 0.06     # how far above the topmost opaque row the crop starts
OUT_SIZE = 256


def hero_sprites():
    """[(hero_id, sprite_id)] in heroes.json order. The file carries // comments, so it is
    scanned with regex rather than parsed - the same reason the other tools here do."""
    text = open(HEROES, encoding="utf-8").read()
    out = []
    for m in re.finditer(r'"visual":\s*\{([^}]*)\}', text):
        sprite = re.search(r'"sprite":\s*"([a-z_0-9]+)"', m.group(1))
        if not sprite:
            continue
        ids = re.findall(r'"id":\s*"([a-z_0-9]+)"', text[: m.start()])
        if ids:
            out.append((ids[-1], sprite.group(1)))
    return out


def original_idle_frame(hero_id):
    """Frame 0 of the 720x1280 export still staged in Assets/_Incoming, when it is there.

    The shipped sheets are 288x512 per frame, which is all the battle rig needs but leaves a
    portrait crop around 115px - a 2.2x upscale to reach 256, and visibly soft. The pre-downscale
    originals are 2.5x larger in each axis and turn that into a slight DOWNSCALE instead.

    Only available for the four Ember heroes: Kael's originals were deleted in f936aba once his
    import was verified, so he can only ever be cropped from the shipped sheet."""
    folder = os.path.join(REPO, "Assets", "_Incoming", hero_id)
    manifest = os.path.join(folder, "sprite_sheets.json")
    if not os.path.exists(manifest):
        return None, None, None
    data = json.load(open(manifest, encoding="utf-8"))
    for sprite in data.get("sprites", []):
        if "idle" not in sprite["image"]:
            continue
        path = os.path.join(folder, sprite["image"])
        if not os.path.exists(path):
            return None, None, None
        fw, fh = int(sprite["frameWidth"]), int(sprite["frameHeight"])
        img = Image.open(path).convert("RGBA")
        size = img.size
        return img.crop((0, 0, fw, fh)), size, (fw, fh)
    return None, None, None


def first_idle_frame(sprite_id):
    """The first idle frame as an RGBA image, plus a note on where it came from.

    An animated hero crops frame 0 out of its sheet using the sidecar's own frame size. A
    placeholder hero has a single image and no sidecar, which is just a one-frame sheet."""
    sheet = os.path.join(UNITS, sprite_id + "_idle.png")
    side = os.path.join(UNITS, sprite_id + "_idle.json")
    if os.path.exists(sheet) and os.path.exists(side):
        meta = json.load(open(side, encoding="utf-8"))
        fw, fh = int(meta["frameWidth"]), int(meta["frameHeight"])
        img = Image.open(sheet).convert("RGBA")
        sheet_size = img.size
        return img.crop((0, 0, fw, fh)), "sheet", sheet_size, (fw, fh)
    single = os.path.join(UNITS, sprite_id + ".png")
    if os.path.exists(single):
        img = Image.open(single).convert("RGBA")
        return img, "single", img.size, img.size
    return None, None, None, None


def alpha_bbox(img):
    alpha = img.getchannel("A")
    mask = alpha.point(lambda a: 255 if a > ALPHA_THRESHOLD else 0)
    return mask.getbbox(), mask       # (left, top, right, bottom) exclusive on right/bottom


def head_centroid_x(mask, bbox):
    """Weighted centroid x of opaque pixels in the top TOP_BAND_FRAC of the bbox."""
    left, top, right, bottom = bbox
    band_bottom = top + max(1, int(round((bottom - top) * TOP_BAND_FRAC)))
    band = mask.crop((left, top, right, band_bottom))
    px = band.load()
    w, h = band.size
    total = 0
    weighted = 0
    for y in range(h):
        for x in range(w):
            if px[x, y]:
                total += 1
                weighted += x
    if total == 0:
        return (left + right) / 2.0, 0
    return left + weighted / float(total), total


def make_portrait(img, bbox, mask):
    left, top, right, bottom = bbox
    bbox_w = right - left
    side = int(round(bbox_w * CROP_FRAC))
    cx, band_px = head_centroid_x(mask, bbox)
    headroom = int(round(side * HEADROOM_FRAC))

    crop_left = int(round(cx - side / 2.0))
    crop_top = top - headroom
    box = (crop_left, crop_top, crop_left + side, crop_top + side)

    # Pad rather than clamp: keep the head where the centroid put it.
    pad = (
        max(0, -box[0]),
        max(0, -box[1]),
        max(0, box[2] - img.width),
        max(0, box[3] - img.height),
    )
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    src = img.crop((max(0, box[0]), max(0, box[1]), min(img.width, box[2]), min(img.height, box[3])))
    canvas.paste(src, (pad[0], pad[1]))
    portrait = canvas.resize((OUT_SIZE, OUT_SIZE), Image.LANCZOS)
    return portrait, side, cx, band_px, pad, box


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--include-singles", action="store_true",
                    help="also do the placeholder heroes that have a single image and no sheet")
    ap.add_argument("--dry-run", action="store_true", help="measure and report, write nothing")
    ap.add_argument("--from-originals", action="store_true",
                    help="crop from the 720x1280 exports in Assets/_Incoming where they still "
                         "exist (everyone but Kael), for a sharp portrait instead of a 2.2x upscale")
    args = ap.parse_args()

    print("source            : Assets/Resources/Sprites/Units/<sprite>_idle.png, frame 0")
    print("output            : Assets/Resources/Sprites/Units/portrait_<heroid>.png  (%dx%d RGBA)"
          % (OUT_SIZE, OUT_SIZE))
    print("loads in Unity as : Resources.Load<Texture2D>(\"Sprites/Units/portrait_<heroid>\")")
    print("crop side         : %.0f%% of bbox width, centred on top-%.0f%% centroid, %.0f%% headroom"
          % (CROP_FRAC * 100, TOP_BAND_FRAC * 100, HEADROOM_FRAC * 100))
    print()
    hdr = ("%-26s %-7s %-13s %-11s %-13s %-9s %s"
           % ("hero id", "source", "sheet", "frame", "alpha bbox", "crop px", "-> 256 scale"))
    print(hdr)
    print("-" * len(hdr))

    written = 0
    for hero_id, sprite_id in hero_sprites():
        img = None
        if args.from_originals:
            img, sheet_size, frame_size = original_idle_frame(hero_id)
            kind = "ORIG"
        if img is None:
            img, kind, sheet_size, frame_size = first_idle_frame(sprite_id)
        if img is None:
            print("%-26s %s" % (hero_id, "NO SOURCE"))
            continue
        if kind == "single" and not args.include_singles:
            print("%-26s %-7s %-13s %-11s  (skipped: no idle sheet; --include-singles to add)"
                  % (hero_id, kind, "%dx%d" % sheet_size, "%dx%d" % frame_size))
            continue

        bbox, mask = alpha_bbox(img)
        if bbox is None:
            print("%-26s %s" % (hero_id, "frame 0 is fully transparent"))
            continue

        portrait, side, cx, band_px, pad, box = make_portrait(img, bbox, mask)
        scale = OUT_SIZE / float(side)
        note = ""
        if any(pad):
            note = "  padded L%d T%d R%d B%d" % pad
        print("%-26s %-7s %-13s %-11s %-13s %-9s x%.2f%s"
              % (hero_id, kind,
                 "%dx%d" % sheet_size, "%dx%d" % frame_size,
                 "%d,%d %dx%d" % (bbox[0], bbox[1], bbox[2] - bbox[0], bbox[3] - bbox[1]),
                 "%d" % side, scale, note))

        if not args.dry_run:
            out = os.path.join(UNITS, "portrait_%s.png" % hero_id)
            portrait.save(out, "PNG")
            written += 1

    print()
    if args.dry_run:
        print("dry run - nothing written.")
    else:
        print("wrote %d portraits to Assets/Resources/Sprites/Units/" % written)
    print("A scale above x1.00 means the crop is being UPSCALED to 256 and will be soft;")
    print("at or below x1.00 the source has enough pixels for a sharp portrait.")


if __name__ == "__main__":
    main()
