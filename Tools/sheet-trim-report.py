#!/usr/bin/env python3
"""READ-ONLY: how much of the battle sprite sheets is transparent, and what a trim could save.

Reads Assets/Resources/Sprites/Units/*.png (and the .json sidecar next to each) and never writes anything.
For every sheet it measures, as the player stores it (BC7, 1 byte per pixel, 16 bytes per 4x4 block):

  - frames the sidecar plays versus cells the grid holds (cells nothing plays are dead weight),
  - the UNION of every played frame's visible pixels (alpha above ALPHA) as a rectangle inside one cell:
    a sheet could be re-cut to that rectangle, uniformly, and lose no pixel,
  - the share of 4x4 blocks that are fully transparent (BC7 still stores them at full price).

The numbers are the ceiling of what a repack or crop could give. Using one needs a code and content change
(the sheet's frame size and the pivot are read by the sprite player), so this script only measures.

    python Tools/sheet-trim-report.py            # prints the table and the totals
    python Tools/sheet-trim-report.py --csv out.csv

Needs Pillow and numpy.
"""
import argparse
import json
import os
import sys
from multiprocessing import Pool

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UNITS = os.path.join(ROOT, "Assets", "Resources", "Sprites", "Units")
PAD = 2            # transparent pixels kept on every cropped side
ALPHA = 8          # below this a pixel is invisible; 0 is stricter and changes the totals by under 1 percent
MB = 1024.0 * 1024.0


def measure(name):
    png = os.path.join(UNITS, name + ".png")
    side_path = os.path.join(UNITS, name + ".json")
    if not os.path.exists(side_path):
        return None
    with open(side_path, encoding="utf-8") as f:
        side = json.load(f)
    fw, fh = side["frameWidth"], side["frameHeight"]
    cols, rows = side["sheet"]["cols"], side["sheet"]["rows"]
    anim = side["defaultAnimation"]
    played = anim["end"] - anim["start"] + 1
    im = Image.open(png).convert("RGBA")
    a = np.asarray(im)[:, :, 3]
    w, h = im.size
    cells = cols * rows
    # union of visible pixels over every cell of the grid (a cell nothing plays is empty or unused either way)
    x0, y0, x1, y1 = fw, fh, 0, 0
    for r in range(rows):
        for c in range(cols):
            cell = a[r * fh:(r + 1) * fh, c * fw:(c + 1) * fw]
            ys, xs = np.nonzero(cell >= ALPHA)
            if len(xs) == 0:
                continue
            x0 = min(x0, xs.min()); x1 = max(x1, xs.max() + 1)
            y0 = min(y0, ys.min()); y1 = max(y1, ys.max() + 1)
    if x1 <= x0:
        x0, y0, x1, y1 = 0, 0, fw, fh
    # fully transparent 4x4 blocks
    hh, ww = (h // 4) * 4, (w // 4) * 4
    blocks = (a[:hh, :ww] >= ALPHA).reshape(hh // 4, 4, ww // 4, 4).any(axis=(1, 3))
    empty_blocks = int(blocks.size - blocks.sum())
    # a cell grid re-cut to the union rectangle, each side rounded up to a multiple of 4 (BC7) with its position kept
    tw = int(-(-(x1 - x0) // 4) * 4)
    th = int(-(-(y1 - y0) // 4) * 4)
    # The two re-cuts that keep the sprite where it is drawn, with PAD transparent pixels left on every side so a
    # bilinear tap at a cell edge cannot reach the next cell, and each side rounded up to a multiple of 12 (4 for
    # BC7 and 6 for ASTC 6x6, so no block ever straddles two frames).
    #   SYM  - crop the same amount off the left and the right of every cell. The body stays centred on the quad, so
    #          nothing changes but the sidecar's frameWidth: no code and no content (UnitRig sizes the quad from the
    #          frame's aspect, and the flip is about the centre).
    #   TOP  - also crop the empty space above the tallest pose. The canvas gets shorter, so heroes.json bodyFraction
    #          and the sidecar footFraction would be re-derived: a content change (heroes.json is hashed into
    #          ContentVersion) and a code-adjacent one.
    keep = max(0, min(x0, fw - x1) - PAD)
    sym_w = min(fw, int(-(-(fw - 2 * keep) // 12) * 12))
    top_h = min(fh, int(-(-(fh - max(0, y0 - PAD)) // 12) * 12))
    return {
        "sym_w": sym_w, "top_h": top_h,
        "sym_mb": sym_w * cols * fh * rows / MB,
        "symtop_mb": sym_w * cols * top_h * rows / MB,
        "name": name, "w": w, "h": h, "fw": fw, "fh": fh, "cols": cols, "rows": rows,
        "cells": cells, "played": played,
        "ux0": int(x0), "uy0": int(y0), "ux1": int(x1), "uy1": int(y1), "tw": tw, "th": th,
        "packed_mb": w * h / MB,
        "recut_mb": tw * th * cells / MB,
        "recut_played_mb": tw * th * played / MB,
        "empty_block_share": empty_blocks / float(blocks.size),
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--csv")
    ap.add_argument("--limit", type=int, default=0, help="measure only the first N sheets (a pilot)")
    args = ap.parse_args()
    names = sorted(n[:-4] for n in os.listdir(UNITS) if n.endswith(".png"))
    if args.limit:
        names = names[:args.limit]
    with Pool(max(1, (os.cpu_count() or 4) - 2)) as pool:
        rows = [r for r in pool.map(measure, names, chunksize=4) if r]
    n = len(rows)
    packed = sum(r["packed_mb"] for r in rows)
    recut = sum(r["recut_mb"] for r in rows)
    recut_played = sum(r["recut_played_mb"] for r in rows)
    blocks = sum(r["packed_mb"] * r["empty_block_share"] for r in rows)
    dead_cells = sum((r["cells"] - r["played"]) * r["fw"] * r["fh"] for r in rows) / MB
    print("sheets measured: %d" % n)
    print("packed today (BC7, 1 byte a pixel): %.1f MB" % packed)
    print("fully transparent 4x4 blocks inside those sheets: %.1f MB (%.1f%%)" % (blocks, 100.0 * blocks / packed))
    print("cells the grid holds but the default clip never plays: %.1f MB" % dead_cells)
    print("re-cut every cell to its sheet's union rectangle (same grid, no pixel lost): %.1f MB (saves %.1f MB, %.1f%%)"
          % (recut, packed - recut, 100.0 * (packed - recut) / packed))
    print("same, and drop the cells the default clip never plays: %.1f MB (saves %.1f MB)" % (recut_played, packed - recut_played))
    sym = sum(r["sym_mb"] for r in rows)
    symtop = sum(r["symtop_mb"] for r in rows)
    widths = sorted(r["sym_w"] for r in rows)
    print("SYM  crop left and right equally, %d px padding, widths in 12s (no code, no content): %.1f MB (saves %.1f MB, %.1f%%); frame width min %d median %d max %d"
          % (PAD, sym, packed - sym, 100.0 * (packed - sym) / packed, widths[0], widths[len(widths) // 2], widths[-1]))
    print("TOP  SYM and also the empty space above the tallest pose (re-derives bodyFraction and footFraction, a content change): %.1f MB (saves %.1f MB, %.1f%%)"
          % (symtop, packed - symtop, 100.0 * (packed - symtop) / packed))
    fills = sorted(float((r["tw"] * r["th"])) / (r["fw"] * r["fh"]) for r in rows)
    print("union rectangle as a share of one cell: min %.2f median %.2f max %.2f" % (fills[0], fills[len(fills) // 2], fills[-1]))
    if args.csv:
        keys = list(rows[0].keys())
        with open(args.csv, "w", encoding="utf-8", newline="\n") as f:
            f.write(",".join(keys) + "\n")
            for r in rows:
                f.write(",".join(str(r[k]) for k in keys) + "\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
