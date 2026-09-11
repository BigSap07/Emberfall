"""Derive footFraction for every sprite-clip sidecar JSON.

For each sidecar in Assets/Resources/Sprites/Units/{name}.json, loads the matching PNG,
extracts the clip's start frame, scans from the bottom up to find the lowest row with
any pixel at alpha > 10, and writes footFraction into the sidecar's defaultAnimation
object. This is the same alpha-scan method used to derive the hero-level
SpriteFootFraction, applied per clip instead of per hero.
"""

import json
import os
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    print("Requires Pillow: pip install Pillow", file=sys.stderr)
    sys.exit(1)

SPRITES_DIR = Path(__file__).resolve().parent.parent / "Assets" / "Resources" / "Sprites" / "Units"
ALPHA_THRESHOLD = 10


def compute_foot_fraction(png_path, frame_w, frame_h, cols, start_frame):
    img = Image.open(png_path).convert("RGBA")

    col_idx = start_frame % cols
    row_idx = start_frame // cols
    x0 = col_idx * frame_w
    y0 = row_idx * frame_h

    # Crop to the start frame
    frame = img.crop((x0, y0, x0 + frame_w, y0 + frame_h))
    pixels = frame.load()

    empty_rows = 0
    for row in range(frame_h - 1, -1, -1):
        has_content = False
        for x in range(frame_w):
            if pixels[x, row][3] > ALPHA_THRESHOLD:
                has_content = True
                break
        if has_content:
            break
        empty_rows += 1

    return round(empty_rows / frame_h, 3)


def process_sidecar(json_path):
    png_path = json_path.with_suffix(".png")
    if not png_path.exists():
        return None

    with open(json_path, "r", encoding="utf-8") as f:
        data = json.load(f)

    frame_w = data.get("frameWidth")
    frame_h = data.get("frameHeight")
    sheet = data.get("sheet", {})
    cols = sheet.get("cols", 1)
    default_anim = data.get("defaultAnimation", {})
    start_frame = default_anim.get("start", 0)

    if not frame_w or not frame_h:
        return None

    foot = compute_foot_fraction(png_path, frame_w, frame_h, cols, start_frame)

    default_anim["footFraction"] = foot
    data["defaultAnimation"] = default_anim

    with open(json_path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")

    return foot


def main():
    json_files = sorted(SPRITES_DIR.glob("*.json"))
    print(f"Found {len(json_files)} sidecar JSON files in {SPRITES_DIR}")

    results = []
    for jf in json_files:
        foot = process_sidecar(jf)
        name = jf.stem
        if foot is not None:
            results.append((name, foot))
            print(f"  {name}: footFraction={foot:.3f}")
        else:
            print(f"  {name}: SKIPPED (no PNG or missing frame dimensions)")

    print(f"\nUpdated {len(results)} sidecars.")


if __name__ == "__main__":
    main()
