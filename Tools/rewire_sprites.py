"""Re-wire sprite sheets for specified heroes, or re-export every unit sheet at the current cell.

Reads source PNGs + manifest from an extracted zip folder, fits each frame by height into a
transparent 336x396 cell (uniform scale, centred; see fit_frame), trims trailing transparent frames, measures foot/body
fractions, and writes the output PNG + sidecar JSON + .meta into
Assets/Resources/Sprites/Units.

Usage:
    python Tools/rewire_sprites.py [--source-root DIR] [--out-dir DIR] [Short ...]
    python Tools/rewire_sprites.py --reexport [--import-root DIR] [--out-dir DIR] [--jobs N] [unit_id ...]

    --source-root DIR   read each hero from DIR/<Short>/ instead of the TEMP layout
    --out-dir DIR       write somewhere other than Sprites/Units (for diffing a run)
    Short ...           only these heroes, by the short names in HEROES

    --reexport          rebuild EVERY sheet that already has a sidecar in Sprites/Units from the
                        original deliveries, at the current cell size. See reexport_unit.
    --import-root DIR   where the deliveries live (default: the folder beside the repo named
                        import, i.e. D:/Emberfall/import): heroes as sprites/<Name>.zip, enemies as
                        enemies-ch1-3/<Chapter>/SPRITES/<Name>/.
"""

import io
import json
import math
import os
import re
import shutil
import sys
import uuid
import zipfile
from pathlib import Path

try:
    from PIL import Image, ImageChops, ImageStat
except ImportError:
    print("Requires Pillow: pip install Pillow", file=sys.stderr)
    sys.exit(1)

Image.MAX_IMAGE_PIXELS = None  # delivered sheets reach 8496 x 9360; they are trusted local files

REPO = Path(__file__).resolve().parent.parent
UNITS_DIR = REPO / "Assets" / "Resources" / "Sprites" / "Units"
OUT_DIR = UNITS_DIR  # --out-dir redirects the writes; the .meta template always comes from UNITS_DIR
ALPHA_THRESHOLD = 10

# THE CELL. 336 x 396, which is 220 x 260 scaled by 1.523, and every one of these numbers is
# forced, not chosen:
#
#   - Resolution. At 220 x 260 a unit was drawn at 1.5 to 1.7 screen pixels per texel (heroes) and
#     about 2.0 (elites and bosses) at 1920 x 1080, and the desktop's 4x4 compression blocks showed
#     as blotches at that magnification. 396 rows puts the hero median at 1.03.
#   - Multiple of 4 on both axes, for BC7 on Windows. Since Unity 2021.2 a BCn texture's width and
#     height must both be multiples of 4 or it is not compressed at all, and a sheet is cols x
#     frameWidth by rows x frameHeight with cols and rows from 7 to 9. 330 x 390, the plain 1.5x,
#     fails that: 9 x 330 = 2970 and 7 x 390 = 2730 are not multiples of 4, so those sheets would
#     have landed in the Windows player as uncompressed RGBA32, four times the memory.
#   - Multiple of 6 on both axes, for ASTC 6x6 on Android, so every frame boundary is also a block
#     boundary and no block mixes the end of one frame with the start of the next.
#
# The smallest cell at or above 1.5x that is a multiple of 12 both ways and keeps the 11:13 shape
# within half a pixel is 336 x 396. The shape is 0.8485 against 0.8462, and fit_frame pads that
# extra 0.28% as transparency, so an 880 x 1040 frame becomes 335 x 396 centred in the cell and the
# character keeps the proportions it was drawn at; nothing on screen gets wider.
DST_W = 336
DST_H = 396
CELL_MULTIPLE = 12  # lcm of the BC7 block (4) and the ASTC 6x6 block (6)

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
    # The five Ember 5-stars were delivered as 720x1280 frames. Their sources are flat zips, so
    # pass --source-root DIR (DIR/<Short>/ holding sprite_sheets.json) rather than relying on
    # the nested TEMP layout above. "clips" overrides CLIP_MAP tokens for this delivery only.
    {"short": "Kael", "hero_id": "kael_the_unyielding", "manifest_prefix": "kael",
     "file_prefix": "kael", "clips": {"basic": "basic", "basic2": "basic_2"}},
    {"short": "Kiva", "hero_id": "kiva_the_forsaken_oath", "manifest_prefix": "kiva",
     "file_prefix": "kiva"},
    {"short": "Seraphine", "hero_id": "seraphine_the_closed_volume",
     "manifest_prefix": "seraphine", "file_prefix": "seraphine"},
    {"short": "Hale", "hero_id": "hale_the_relentless_hunter", "manifest_prefix": "hale",
     "file_prefix": "hale"},
    {"short": "Marrow", "hero_id": "marrow_the_irrevocable_vow", "manifest_prefix": "marrow",
     "file_prefix": "marrow", "clips": {"basic": "basic"}},
    # A normal 880x1040 delivery, kept as the control for "identical to the old plain resize".
    {"short": "Isara", "hero_id": "isara_the_second_chance", "manifest_prefix": "isara",
     "file_prefix": "isara"},
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
    for f in sorted(UNITS_DIR.glob("*.png.meta")):
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


def fit_frame(frame, log_label=None, cell_w=None, cell_h=None):
    """Scale one source frame uniformly into a transparent cell_w x cell_h cell (DST_W x DST_H).

    The loader derives a character's width from the SHAPE of one cell (sidecar frameWidth over
    frameHeight), so the cell has to keep its 11:13 shape and the art has to keep its own
    proportions inside it. This used to be frame.resize((DST_W, DST_H)), which stretched whatever
    aspect the delivery used to 0.846: the five Ember 5-stars arrived as 720x1280 (0.5625) and
    rendered 1.3x to 1.65x too wide. Fit by height; centre horizontally. If that would be wider than
    the cell, fit by width instead and centre vertically, and say so (log_label names the clip once).

    Upscaling is allowed, with the same filter, and is reported: a frame shorter than the cell (the
    Hollowed Mage's skill clip was delivered at 433 x 512) is magnified to fit, which is softer than
    the rest of its set and is the designer's call to redeliver, not something to hide.
    """
    cell_w = cell_w or DST_W
    cell_h = cell_h or DST_H
    w, h = frame.size
    new_w = round(w * cell_h / h)
    new_h = cell_h
    if new_w > cell_w:
        new_w = cell_w
        new_h = round(h * cell_w / w)
        if log_label:
            print(f"    NOTE {log_label}: {w}x{h} is wider than the cell, fit by width "
                  f"-> {new_w}x{new_h}, centred vertically")
    if log_label and new_h > h:
        print(f"    UPSCALED {log_label}: source frame {w}x{h} magnified {new_h / h:.2f}x to "
              f"{new_w}x{new_h} - softer than its set, flag it for redelivery")
    resized = frame.resize((new_w, new_h), Image.LANCZOS)
    if (new_w, new_h) == (cell_w, cell_h):
        return resized
    cell = Image.new("RGBA", (cell_w, cell_h), (0, 0, 0, 0))
    cell.paste(resized, ((cell_w - new_w) // 2, (cell_h - new_h) // 2))
    return cell


def process_hero(hero_cfg, meta_template):
    """Process all clips for one hero."""
    hero_id = hero_cfg["hero_id"]
    source = hero_cfg["source"]
    manifest_prefix = hero_cfg["manifest_prefix"]
    file_prefix = hero_cfg["file_prefix"]

    clip_map = {**CLIP_MAP, **hero_cfg.get("clips", {})}

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

    for engine_clip, token in clip_map.items():
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
            frame = fit_frame(frame, f"{hero_id}_{engine_clip}" if n == 0 else None)
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

        # Leave a sidecar alone when the re-wire measured nothing new: rewriting it only adds
        # a trailing newline to files that were committed without one.
        if existing_sidecar is None or sidecar != json.loads(sidecar_path.read_text("utf-8")):
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


# ---------------------------------------------------------------------------------------------
# --reexport: every sheet already in Sprites/Units, rebuilt from the original deliveries
# ---------------------------------------------------------------------------------------------
#
# This is how a cell-size change reaches all 57 units without anyone re-deciding, per clip, which
# delivered file it came from. That mapping is NOT derivable from names: basic is basic1 for most
# heroes and basic for Kael and Marrow, Kael's second basic is basic_2, Nereia's manifest says
# celen_* over files named nereia_*, the Hollowed Hunter shipped as hollowed_archer, and Rheia
# ships an idle2. So the re-export does not guess. It renders three frames of every delivered
# sheet at the cell the shipped sheet uses today and compares them, pixel by pixel, with the
# shipped sheet; a delivered sheet is written only into the clip it reproduces. A right match
# scores about 0.01 (mean absolute difference per channel, 0..255) and the nearest wrong one over
# 10, so MATCH_TOLERANCE has a wide margin either side, and a clip nothing reproduces is reported
# rather than filled.
#
# Only the PNG and the two pixel fields in the sidecar (frameWidth, frameHeight) change. Every
# other sidecar value is a fraction of the cell or a frame index - footFraction, impactFraction,
# start, end, the grid - and survives a uniform resize unchanged, as do spriteBodyFraction and
# spriteFootFraction in heroes.json and enemies.json. The run re-measures each clip's start-frame
# foot and the idle clip's body at the new size and prints them beside the authored ones, so that
# claim is checked on every run rather than assumed. The .meta is never touched: its guid is the
# asset's identity.

MATCH_TOLERANCE = 2.0
CLIP_SUFFIXES = ("hit_reaction", "controlled", "basic2", "basic", "idle2", "idle", "skill", "death")

# A shipped cell -> the cell it re-exports to. 220 x 260 is every sheet but Brenna's; her 944 x 944
# delivery was cut to 236 x 236 cells and scales by the same 1.523 to the nearest multiple of 12.
# A cell already in the right-hand column is current and re-exports to itself, so a second run
# rebuilds the same sheets rather than growing them again.
LEGACY_CELLS = {(220, 260): (DST_W, DST_H), (236, 236): (360, 360)}
STEM_ALIASES = {"hollowed_archer": "hollowed_hunter"}


def target_cell(w, h):
    if (w, h) in LEGACY_CELLS:
        return LEGACY_CELLS[(w, h)]
    if (w, h) in LEGACY_CELLS.values():
        return (w, h)
    raise ValueError(f"no target cell for a {w}x{h} sheet - add it to LEGACY_CELLS")


class Delivery(object):
    """One unit's delivered export: a zip (heroes) or an extracted folder (enemies)."""

    def __init__(self, path):
        self.path = Path(path)
        self.zip = zipfile.ZipFile(self.path) if self.path.suffix.lower() == ".zip" else None
        if self.zip:
            names = self.zip.namelist()
        else:
            names = [str(Path(r, f).relative_to(self.path)).replace("\\", "/")
                     for r, _, fs in os.walk(self.path) for f in fs]
        manifests = [n for n in names if n.endswith("sprite_sheets.json")]
        if len(manifests) != 1:
            raise ValueError(f"{self.path}: expected one sprite_sheets.json, found {len(manifests)}")
        self.base = manifests[0][: -len("sprite_sheets.json")]
        self.manifest = json.loads(self._read(manifests[0]))
        self.pngs = {}
        for n in names:
            rel = n[len(self.base):]
            if n.startswith(self.base) and rel.lower().endswith(".png") and "/" not in rel:
                self.pngs[rel.lower()] = n

    def _read(self, name):
        if self.zip:
            return self.zip.read(name)
        return (self.path / name).read_bytes()

    def resolve(self, image):
        """The file a manifest entry names. Nereia's manifest says celen_* over nereia_* files, so
        fall back to the one file whose name after its first underscore matches."""
        hit = self.pngs.get(image.lower())
        if hit is None:
            tail = image.lower().split("_", 1)[-1]
            hits = [v for k, v in self.pngs.items() if k.split("_", 1)[-1] == tail]
            hit = hits[0] if len(hits) == 1 else None
        return hit

    def open(self, name):
        return Image.open(io.BytesIO(self._read(name))).convert("RGBA")


def find_deliveries(import_root):
    """unit key -> delivery path. Heroes by zip stem (the hero's first name), enemies by folder."""
    found = {}
    for z in sorted((import_root / "sprites").glob("*.zip")):
        found[z.stem.lower()] = z
    for d in sorted((import_root / "enemies-ch1-3").glob("*/SPRITES/*")):
        if d.is_dir():
            key = re.sub(r"\s+", "_", d.name.strip().lower())
            found[STEM_ALIASES.get(key, key)] = d
    return found


def shipped_clips(unit_id):
    clips = {}
    for clip in CLIP_SUFFIXES:
        side = UNITS_DIR / f"{unit_id}_{clip}.json"
        if side.exists() and (UNITS_DIR / f"{unit_id}_{clip}.png").exists():
            clips[clip] = side
    return clips


def shipped_units():
    units = set()
    for side in UNITS_DIR.glob("*.json"):
        for clip in CLIP_SUFFIXES:
            if side.stem.endswith("_" + clip):
                units.add(side.stem[: -len(clip) - 1])
                break
    return sorted(units)


def cell_of(img, n, cols, w, h):
    c, r = n % cols, n // cols
    return img.crop((c * w, r * h, (c + 1) * w, (r + 1) * h))


def mean_diff(a, b):
    return sum(ImageStat.Stat(ImageChops.difference(a, b)).mean) / 4.0


def alpha_rows(cell):
    """(top, bottom) rows of the cell's alpha above ALPHA_THRESHOLD, or None when it is empty."""
    box = cell.getchannel("A").point(lambda a: 255 if a > ALPHA_THRESHOLD else 0).getbbox()
    return None if box is None else (box[1], box[3] - 1)


def reexport_unit(unit_id, delivery_path, out_dir):
    """Rebuild every shipped clip of one unit at its target cell. Returns (log lines, summary)."""
    log = []
    delivery = Delivery(delivery_path)
    clips = shipped_clips(unit_id)
    shipped = {}
    for clip, side in clips.items():
        sc = json.loads(side.read_text("utf-8"))
        shipped[clip] = (sc, Image.open(UNITS_DIR / f"{unit_id}_{clip}.png").convert("RGBA"))
    unmatched = set(clips)
    summary = {"unit": unit_id, "written": [], "missing": [], "upscaled": [], "idle": None}

    for entry in delivery.manifest.get("sprites", []):
        name = delivery.resolve(entry["image"])
        if name is None:
            log.append(f"  {entry['image']}: named by the manifest, not in the delivery")
            continue
        cols, rows = entry["sheet"]["cols"], entry["sheet"]["rows"]
        frames = entry["sheet"]["frames"]
        sfw, sfh = entry["frameWidth"], entry["frameHeight"]
        candidates = [c for c in unmatched
                      if (shipped[c][0]["sheet"]["cols"], shipped[c][0]["sheet"]["rows"]) == (cols, rows)]
        if not candidates:
            log.append(f"  {name}: no shipped clip with a {cols}x{rows} grid left, not imported")
            continue
        src = delivery.open(name)
        probe = sorted({0, frames // 3, (2 * frames) // 3})
        scores = []
        for clip in candidates:
            sc, old = shipped[clip]
            ow, oh = sc["frameWidth"], sc["frameHeight"]
            d = sum(mean_diff(fit_frame(cell_of(src, k, cols, sfw, sfh), None, ow, oh),
                              cell_of(old, k, cols, ow, oh)) for k in probe) / len(probe)
            scores.append((d, clip))
        scores.sort()
        d, clip = scores[0]
        if d > MATCH_TOLERANCE:
            log.append(f"  {name}: reproduces no shipped clip (best {clip} at {d:.2f}), not imported")
            src.close()
            continue
        unmatched.discard(clip)
        sc, old = shipped[clip]
        nw, nh = target_cell(sc["frameWidth"], sc["frameHeight"])

        dst = Image.new("RGBA", (cols * nw, rows * nh), (0, 0, 0, 0))
        buf = io.StringIO()
        stdout, sys.stdout = sys.stdout, buf
        try:
            for n in range(frames):
                frame = fit_frame(cell_of(src, n, cols, sfw, sfh),
                                  f"{unit_id}_{clip}" if n == 0 else None, nw, nh)
                dst.paste(frame, ((n % cols) * nw, (n // cols) * nh))
        finally:
            sys.stdout = stdout
        notes = [l for l in buf.getvalue().splitlines() if l.strip()]
        log.extend(notes)
        if any("UPSCALED" in l for l in notes):
            summary["upscaled"].append(clip)
        src.close()

        out_png = out_dir / f"{unit_id}_{clip}.png"
        dst.save(str(out_png), "PNG")

        # The sidecar keeps every byte but the two pixel fields, so it diffs as exactly those two
        # lines (several were committed without a trailing newline, and json.dump would add one).
        text = clips[clip].read_text("utf-8")
        text = re.sub(r'("frameWidth":\s*)\d+', r"\g<1>%d" % nw, text, count=1)
        text = re.sub(r'("frameHeight":\s*)\d+', r"\g<1>%d" % nh, text, count=1)
        with open(out_dir / f"{unit_id}_{clip}.json", "w", encoding="utf-8", newline="") as f:
            f.write(text)

        anim = sc["defaultAnimation"]
        start = anim.get("start", 0)
        foot_new, _ = compute_foot_fraction_with_border(cell_of(dst, start, cols, nw, nh))
        log.append(f"  {clip:<13} <- {name:<44} match {d:5.2f}  {cols * nw}x{rows * nh}  "
                   f"{out_png.stat().st_size / 1048576:6.1f} MB  foot {anim.get('footFraction')} "
                   f"-> {foot_new:.3f} re-measured")
        if clip == "idle":
            top, bottom = nh, -1
            for n in range(start, anim.get("end", frames - 1) + 1):
                b = alpha_rows(cell_of(dst, n, cols, nw, nh))
                if b:
                    top, bottom = min(top, b[0]), max(bottom, b[1])
            summary["idle"] = {"body": round((bottom - top) / nh, 3),
                               "foot": round((nh - 1 - bottom) / nh, 3), "cell": [nw, nh]}
        summary["written"].append(clip)
        dst.close()

    for clip in sorted(unmatched):
        log.append(f"  {clip:<13} MISSING - no delivered sheet reproduces it; left as shipped")
        summary["missing"].append(clip)
    return log, summary


def _reexport_job(job):
    unit_id, delivery_path, out_dir = job
    try:
        return unit_id, reexport_unit(unit_id, delivery_path, Path(out_dir))
    except Exception as e:  # one unit's broken delivery must not lose the other units' report
        return unit_id, ([f"  ERROR {type(e).__name__}: {e}"],
                         {"unit": unit_id, "written": [], "missing": ["*"], "upscaled": [], "idle": None})


def reexport(import_root, out_dir, only, jobs):
    from concurrent.futures import ProcessPoolExecutor
    deliveries = find_deliveries(import_root)
    work = []
    for unit_id in shipped_units():
        if only and unit_id not in only:
            continue
        key = unit_id if unit_id in deliveries else unit_id.split("_", 1)[0]
        if key not in deliveries:
            print(f"{unit_id}: no delivery under {import_root}, left as shipped")
            continue
        work.append((unit_id, str(deliveries[key]), str(out_dir)))
    summaries = []
    with ProcessPoolExecutor(max_workers=jobs) as pool:
        for unit_id, (log, summary) in pool.map(_reexport_job, work):
            print(f"\n{unit_id}", flush=True)
            for line in log:
                print(line, flush=True)
            summaries.append(summary)
    missing = [(s["unit"], m) for s in summaries for m in s["missing"]]
    upscaled = [(s["unit"], c) for s in summaries for c in s["upscaled"]]
    print(f"\n{sum(len(s['written']) for s in summaries)} sheets written for {len(summaries)} units")
    print(f"upscaled from a source smaller than the cell: {upscaled or 'none'}")
    print(f"shipped clips no delivery reproduces: {missing or 'none'}")
    print("idle clip re-measured at the new cell (compare with spriteBodyFraction / spriteFootFraction):")
    for s in summaries:
        if s["idle"]:
            print(f"  {s['unit']:<32} body {s['idle']['body']:.3f}  foot {s['idle']['foot']:.3f}")
    return 1 if missing else 0


def main():
    global OUT_DIR
    args = sys.argv[1:]
    source_root = None
    import_root = REPO.parent / "import"
    do_reexport = False
    jobs = max(1, (os.cpu_count() or 2) // 2)
    only = []
    while args:
        a = args.pop(0)
        if a == "--source-root":
            source_root = Path(args.pop(0))
        elif a == "--out-dir":
            OUT_DIR = Path(args.pop(0))
            OUT_DIR.mkdir(parents=True, exist_ok=True)
        elif a == "--reexport":
            do_reexport = True
        elif a == "--import-root":
            import_root = Path(args.pop(0))
        elif a == "--jobs":
            jobs = int(args.pop(0))
        else:
            only.append(a.lower())

    if do_reexport:
        return reexport(import_root, OUT_DIR, only, jobs)

    meta_template = find_meta_template()
    if meta_template is None:
        print("WARNING: no *.png.meta in Sprites/Units to template from - sheets will import at\n"
              "         Unity's 2048 default and be sheared. Import one hero by hand first.")

    for hero_cfg in HEROES:
        if only and hero_cfg["short"].lower() not in only:
            continue
        if source_root is not None:
            hero_cfg = {**hero_cfg, "source": source_root / hero_cfg["short"]}
        elif "source" not in hero_cfg:
            print(f"  {hero_cfg['short']}: needs --source-root, skipped")
            continue
        print(f"\n{'='*60}")
        print(f"  {hero_cfg['short']} -> {hero_cfg['hero_id']}")
        print(f"  source: {hero_cfg['source']}")
        print(f"{'='*60}")
        results = process_hero(hero_cfg, meta_template)


if __name__ == "__main__":
    sys.exit(main())
