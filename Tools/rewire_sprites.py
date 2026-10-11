"""Re-wire sprite sheets for specified heroes.

Reads source PNGs + manifest from an extracted zip folder, downsamples each
frame to 220x260, trims trailing transparent frames, measures foot/body
fractions, and writes the output PNG + sidecar JSON + .meta into
Assets/Resources/Sprites/Units.

Usage:
    python Tools/rewire_sprites.py
"""

import json
import math
import os
import shutil
import sys
import uuid
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    print("Requires Pillow: pip install Pillow", file=sys.stderr)
    sys.exit(1)

REPO = Path(__file__).resolve().parent.parent
OUT_DIR = REPO / "Assets" / "Resources" / "Sprites" / "Units"
ALPHA_THRESHOLD = 10
DST_W = 220
DST_H = 260

# Hero configs: (short_name, hero_id, source_dir, manifest_prefix_override)
# manifest_prefix_override handles the Nereia/Celen name mismatch
#
# WHICH FILE HOLDS THE BASIC ATTACK VARIES BY DELIVERY, and it is not derivable: most exports
# ship <hero>_basic1.png, but Kael and Marrow ship <hero>_basic.png with no digit, and Kael's
# second basic is <hero>_basic_2.png with an underscore the "basic2" token does not match. Set
# CLIP_MAP["basic"] / ["basic2"] per hero before calling process_hero - match by ROLE, never by
# filename, which is the one note the original PowerShell importer carried that still bites.
#
# The five Ember 5-stars were wired this way on 2026-09-20:
#   Seraphine -> seraphine_the_closed_volume   basic1
#   Kael      -> kael_the_unyielding           basic   (and basic_2 for the second basic)
#   Kiva      -> kiva_the_forsaken_oath        basic1
#   Hale      -> hale_the_relentless_hunter    basic1
#   Marrow    -> marrow_the_irrevocable_vow    basic
HEROES = [
    {
        "short": "Nereia",
        "hero_id": "nereia_the_field_medic",
        "source": Path(os.environ.get("TEMP", "/tmp")) / "sprites" / "Nereia" / "Nereia",
        "manifest_prefix": "celen",  # manifest uses celen_*, files are nereia_*
        "file_prefix": "nereia",
    },
    {
        "short": "Sylven",
        "hero_id": "sylven_the_unfading_blade",
        "source": Path(os.environ.get("TEMP", "/tmp")) / "sprites" / "Sylven" / "Sylven",
        "manifest_prefix": "sylven",
        "file_prefix": "sylven",
    },
    {
        "short": "Mireth",
        "hero_id": "mireth_the_erased_name",
        "source": Path(os.environ.get("TEMP", "/tmp")) / "sprites" / "Mireth" / "Mireth",
        "manifest_prefix": "mireth",
        "file_prefix": "mireth",
    },
]

# Map engine clip name -> manifest token. basic1 -> basic, basic2 -> basic2.
CLIP_MAP = {
    "idle": "idle",
    "basic": "basic1",
    "basic2": "basic2",
    "skill": "skill",
    "hit_reaction": "hit_reaction",
    "death": "death",
    "controlled": "controlled",
}


def find_meta_template():
    """Any existing unit-sheet .meta, to copy import settings from.

    It used to name kael_the_ashbound specifically. He was a hero of the placeholder roster and
    his art has been deleted, so that glob now matches nothing and every sheet written after it
    would import at Unity's 2048 default and be sheared - silently, since a missing template only
    prints a warning. The settings are identical across every sheet in the folder and differ only
    by guid, so the first one that exists serves; naming a particular hero only created something
    that could be deleted out from under this.
    """
    for f in sorted(OUT_DIR.glob("*.png.meta")):
        return f
    return None


def write_meta(png_path, template):
    """Write a .meta file for the PNG if one doesn't exist."""
    meta_path = Path(str(png_path) + ".meta")
    if meta_path.exists():
        return "kept"
    if template is None:
        return "no-template"
    text = template.read_text(encoding="utf-8")
    new_guid = uuid.uuid4().hex
    import re
    text = re.sub(r"(?m)^guid: [0-9a-f]{32}\s*$", f"guid: {new_guid}", text)
    meta_path.write_text(text, encoding="utf-8")
    return "written"


def find_sheet(sprites, token, manifest_prefix):
    """Find a sprite entry in the manifest matching the given token."""
    hits = []
    for s in sprites:
        img = s["image"]
        # Match by token in the image name
        if token in img:
            hits.append(s)
    if not hits:
        return None
    # Shortest name wins (avoids basic matching basic2)
    return min(hits, key=lambda s: len(s["image"]))


def scan_frame_alpha(pixels, x0, y0, w, h):
    """Scan a single frame for vertical alpha bounds. Returns (minY, maxY) or None."""
    min_y = h
    max_y = -1
    for y in range(h):
        for x in range(w):
            r, g, b, a = pixels[x0 + x, y0 + y]
            if a > ALPHA_THRESHOLD:
                if y < min_y:
                    min_y = y
                if y > max_y:
                    max_y = y
                break
    if max_y < 0:
        return None
    return (min_y, max_y)


def compute_foot_fraction_with_border(frame_img):
    """Compute footFraction with border guard, matching derive_clip_foot_fractions.py."""
    pixels = frame_img.load()
    w, h = frame_img.size

    def row_uniform(r):
        for x in range(w):
            if pixels[x, r][3] <= ALPHA_THRESHOLD:
                return False
        return True

    def col_uniform(c):
        for y in range(h):
            if pixels[c, y][3] <= ALPHA_THRESHOLD:
                return False
        return True

    border_bottom = 0
    for r in range(h - 1, -1, -1):
        if row_uniform(r):
            border_bottom += 1
        else:
            break

    border_left = 0
    border_right = 0
    if border_bottom > 0:
        for c in range(w):
            if col_uniform(c):
                border_left += 1
            else:
                break
        for c in range(w - 1, -1, -1):
            if col_uniform(c):
                border_right += 1
            else:
                break

    x_start = border_left
    x_end = w - border_right

    empty_rows = border_bottom
    for row in range(h - 1 - border_bottom, -1, -1):
        has_content = False
        for x in range(x_start, x_end):
            if pixels[x, row][3] > ALPHA_THRESHOLD:
                has_content = True
                break
        if has_content:
            break
        empty_rows += 1

    return round(empty_rows / h, 3), border_bottom


def process_hero(hero_cfg, meta_template):
    """Process all clips for one hero."""
    hero_id = hero_cfg["hero_id"]
    source = hero_cfg["source"]
    manifest_prefix = hero_cfg["manifest_prefix"]
    file_prefix = hero_cfg["file_prefix"]

    manifest_path = source / "sprite_sheets.json"
    if not manifest_path.exists():
        print(f"  ERROR: no sprite_sheets.json in {source}")
        return {}

    with open(manifest_path, encoding="utf-8") as f:
        manifest = json.load(f)

    sprites = manifest.get("sprites", [])
    results = {}  # clip_name -> {footFraction, bodyFraction, frames, ...}

    idle_top = None
    idle_bottom = None

    for engine_clip, token in CLIP_MAP.items():
        sheet_entry = find_sheet(sprites, token, manifest_prefix)
        if sheet_entry is None:
            print(f"  {engine_clip:<15} SKIPPED - no sheet matching '{token}'")
            continue

        # Resolve actual file on disk
        manifest_image = sheet_entry["image"]
        # Fix the name mismatch: replace manifest_prefix with file_prefix
        actual_filename = manifest_image.replace(
            f"{manifest_prefix}_", f"{file_prefix}_", 1
        )
        src_path = source / actual_filename
        if not src_path.exists():
            # Try the manifest name as-is
            src_path = source / manifest_image
            if not src_path.exists():
                print(f"  {engine_clip:<15} SKIPPED - {actual_filename} not on disk")
                continue

        cols = sheet_entry["sheet"]["cols"]
        rows = sheet_entry["sheet"]["rows"]
        frames = sheet_entry["sheet"]["frames"]
        src_fw = sheet_entry["frameWidth"]
        src_fh = sheet_entry["frameHeight"]
        fps = 30
        if "defaultAnimation" in sheet_entry and "fps" in sheet_entry["defaultAnimation"]:
            fps = sheet_entry["defaultAnimation"]["fps"]

        dst_sheet_w = cols * DST_W
        dst_sheet_h = rows * DST_H

        # Load source and downsample per-frame
        src_img = Image.open(src_path).convert("RGBA")
        dst_img = Image.new("RGBA", (dst_sheet_w, dst_sheet_h), (0, 0, 0, 0))

        for n in range(frames):
            sc = n % cols
            sr = n // cols
            src_box = (sc * src_fw, sr * src_fh, (sc + 1) * src_fw, (sr + 1) * src_fh)
            frame = src_img.crop(src_box)
            frame = frame.resize((DST_W, DST_H), Image.LANCZOS)
            dst_img.paste(frame, (sc * DST_W, sr * DST_H))

        src_img.close()

        # Scan for trailing transparent frames
        dst_pixels = dst_img.load()
        last_real = frames - 1
        while last_real >= 0:
            c = last_real % cols
            r = last_real // cols
            bounds = scan_frame_alpha(dst_pixels, c * DST_W, r * DST_H, DST_W, DST_H)
            if bounds is not None:
                break
            last_real -= 1

        # Scan all real frames for content bounds
        top = DST_H
        bottom = -1
        for n in range(last_real + 1):
            c = n % cols
            r = n // cols
            bounds = scan_frame_alpha(dst_pixels, c * DST_W, r * DST_H, DST_W, DST_H)
            if bounds is not None:
                if bounds[0] < top:
                    top = bounds[0]
                if bounds[1] > bottom:
                    bottom = bounds[1]

        if engine_clip == "idle":
            idle_top = top
            idle_bottom = bottom

        # Foot/body fraction from the start frame using border guard
        start_frame = 0
        sc = start_frame % cols
        sr = start_frame // cols
        start_frame_img = dst_img.crop(
            (sc * DST_W, sr * DST_H, (sc + 1) * DST_W, (sr + 1) * DST_H)
        )
        foot_frac, border_px = compute_foot_fraction_with_border(start_frame_img)

        body_frac = round((bottom - top) / DST_H, 4) if bottom >= 0 else 0.0

        # Write PNG
        out_png = OUT_DIR / f"{hero_id}_{engine_clip}.png"
        dst_img.save(str(out_png), "PNG")
        size_mb = out_png.stat().st_size / (1024 * 1024)
        dst_img.close()

        # Write sidecar JSON
        sidecar_path = OUT_DIR / f"{hero_id}_{engine_clip}.json"
        existing_sidecar = None
        if sidecar_path.exists():
            with open(sidecar_path, "r", encoding="utf-8") as f:
                existing_sidecar = json.load(f)

        if existing_sidecar is not None:
            # Re-wire: update only footFraction from new art, preserve everything else
            existing_sidecar.setdefault("defaultAnimation", {})
            existing_sidecar["defaultAnimation"]["footFraction"] = foot_frac
            sidecar = existing_sidecar
        else:
            # New clip: build from scratch
            anim = {"start": 0, "end": last_real, "fps": fps, "loop": True,
                    "footFraction": foot_frac}
            # NO SEEDED CONSTANTS HERE. This used to stamp impactFraction 0.5 and playbackSpeed
            # 1.30 onto every non-idle clip, and because a seeded number looks exactly like a
            # measured one once it is in the file, six heroes ran 30% fast against the other 25
            # for as long as they existed and nobody could see why.
            #
            # Speed now comes from ONE place - SpriteAnimClip.DefaultPlaybackSpeed - and a sidecar
            # only names playbackSpeed when that clip genuinely wants its own. Impact is a
            # MEASUREMENT, taken from the art by Tools/derive_clip_impact_fractions.py, which this
            # importer deliberately does not do inline: run it after wiring a delivery, on the
            # attack clips, the same way the 25-hero delivery got its values.
            sidecar = {
                "frameWidth": DST_W,
                "frameHeight": DST_H,
                "sheet": {"cols": cols, "rows": rows},
                "defaultAnimation": anim,
            }

        with open(sidecar_path, "w", encoding="utf-8") as f:
            json.dump(sidecar, f, indent=2, ensure_ascii=False)
            f.write("\n")

        # Write .meta
        meta_status = write_meta(out_png, meta_template)

        trim_note = ""
        if last_real != frames - 1:
            trim_note = f" trimmed {frames-1}->{last_real}"

        border_note = f" border={border_px}px" if border_px > 0 else ""

        print(
            f"  {engine_clip:<15} {last_real+1:>4} frames  "
            f"{dst_sheet_w:>5}x{dst_sheet_h:<5} {size_mb:>6.1f} MB  "
            f"foot={foot_frac:.3f}  meta={meta_status}{trim_note}{border_note}"
        )

        results[engine_clip] = {
            "footFraction": foot_frac,
            "bodyFraction": body_frac,
            "frames": last_real + 1,
            "border_px": border_px,
        }

    # Report hero-level fractions from idle
    if idle_top is not None and idle_bottom is not None:
        hero_foot = round((DST_H - 1 - idle_bottom) / DST_H, 3)
        hero_body = round((idle_bottom - idle_top) / DST_H, 3)
        print(f"\n  heroes.json visual block for {hero_id}:")
        print(f'    "spriteBodyFraction": {hero_body}, "spriteFootFraction": {hero_foot}')

    return results


def main():
    meta_template = find_meta_template()
    if meta_template is None:
        print("WARNING: no *.png.meta in Sprites/Units to template from - sheets will import at\n"
              "         Unity's 2048 default and be sheared. Import one hero by hand first.")

    for hero_cfg in HEROES:
        print(f"\n{'='*60}")
        print(f"  {hero_cfg['short']} -> {hero_cfg['hero_id']}")
        print(f"  source: {hero_cfg['source']}")
        print(f"{'='*60}")
        results = process_hero(hero_cfg, meta_template)


if __name__ == "__main__":
    main()
