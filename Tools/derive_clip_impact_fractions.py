# -*- coding: utf-8 -*-
"""Derive impactFraction for attack clips the way the 25-hero delivery got theirs.

THE RULE, recovered from commit "derive sprite visual fields for all 25 animated heroes":
impactFraction is written to ATTACK clips only - basic, basic2, skill - and comes from the
PEAK HORIZONTAL EXTENT per frame. Scan every frame's alpha, measure how wide the drawn content
is, and the widest frame is the one where the swing is furthest out: that is the strike. The
fraction is that frame's index over the clip's last frame, so 0 is the first frame and 1 is the
last.

It was done with a throwaway script that was never committed, which is why the Ember five - wired
later, by a different importer - carry a flat seeded 0.5 instead. This file is that script,
written down, so the next delivery does not have to guess again.

Run with no arguments to VERIFY: it re-derives every clip that already carries a value and reports
how many it reproduces. Pass --write to apply it to clips that are missing one (or --force to
rewrite every attack clip).

    python Tools/derive_clip_impact_fractions.py
    python Tools/derive_clip_impact_fractions.py --write kael_the_unyielding kiva_the_forsaken_oath
"""
import io
import json
import os
import re
import sys

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow is required: python -m pip install Pillow")

UNITS = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                     "..", "Assets", "Resources", "Sprites", "Units")
UNITS = os.path.normpath(UNITS)

# Only these carry an impactFraction. idle loops at rest, and hit_reaction, death and controlled
# are reactions rather than strikes - nothing syncs a blow to them, so a value there would be a
# number nothing reads.
ATTACK_CLIPS = ("basic", "basic2", "skill")

# A frame's content, not its bleed. The quarter-resolution downscale leaves a faint halo around
# every silhouette; counting it as content lets a frame whose halo happens to reach further win
# over the frame where the weapon actually does.
#
# 12 is not a guess. The original derivation's script was never committed, so the threshold was
# recovered by sweeping it against the 70 values that ARE committed and keeping the one that
# reproduces most of them: 2 -> 48, 4 -> 51, 6 -> 53, 8 -> 58, 10 -> 62, 12 -> 63, 14 -> 59,
# 16 -> 56, 20 -> 32, 24 -> 28. The peak is sharp and it is here.
#
# It reproduces 63 of 70 exactly, to three decimals. The other seven are frames where two
# neighbouring poses are within a pixel or two of each other, so a hair of threshold moves the
# argmax one frame - a difference of about 0.014 of a clip, roughly one frame in seventy. Those
# are left alone rather than rewritten: an existing measured value is worth more than a
# re-measured one that disagrees by a frame.
ALPHA_FLOOR = 12


def frames_of(sidecar):
    anim = sidecar.get("defaultAnimation") or {}
    start = int(anim.get("start", 0))
    end = int(anim.get("end", start))
    sheet = sidecar.get("sheet") or {}
    cols = max(1, int(sheet.get("cols", 1)))
    rows = max(1, int(sheet.get("rows", 1)))
    return start, end, cols, rows


def peak_extent_frame(png_path, sidecar):
    """The frame whose drawn content is widest, and the per-frame widths behind that answer."""
    start, end, cols, rows = frames_of(sidecar)
    img = Image.open(png_path).convert("RGBA")
    sheet_w, sheet_h = img.size
    fw = sheet_w // cols
    fh = sheet_h // rows
    alpha = img.split()[3].load()

    widths = []
    best_frame, best_width = start, -1
    for n in range(start, end + 1):
        c = n % cols
        r = n // cols
        ox, oy = c * fw, r * fh
        lo, hi = fw, -1
        for x in range(fw):
            column_has_content = False
            for y in range(fh):
                if alpha[ox + x, oy + y] >= ALPHA_FLOOR:
                    column_has_content = True
                    break
            if column_has_content:
                if x < lo:
                    lo = x
                hi = x
        width = 0 if hi < 0 else (hi - lo + 1)
        widths.append(width)
        if width > best_width:
            best_width, best_frame = width, n
    img.close()
    return best_frame, best_width, widths, start, end


def derive(png_path, sidecar):
    best_frame, best_width, _widths, start, end = peak_extent_frame(png_path, sidecar)
    span = max(1, end - start)
    return round((best_frame - start) / float(span), 3), best_frame, best_width


def main():
    args = [a for a in sys.argv[1:]]
    write = "--write" in args or "--force" in args
    force = "--force" in args
    only = [a for a in args if not a.startswith("--")]

    rows = []
    reproduced = missing = rewritten = 0
    for name in sorted(os.listdir(UNITS)):
        m = re.match(r"^(.*)_(basic2|basic|skill)\.json$", name)
        if not m:
            continue
        hero, clip = m.group(1), m.group(2)
        if only and hero not in only:
            continue
        path = os.path.join(UNITS, name)
        png = path[:-5] + ".png"
        if not os.path.exists(png):
            continue
        sidecar = json.load(io.open(path, encoding="utf-8"))
        anim = sidecar.get("defaultAnimation") or {}
        had = anim.get("impactFraction")

        value, frame, width = derive(png, sidecar)
        agrees = had is not None and abs(float(had) - value) < 0.0015
        if had is None:
            missing += 1
        elif agrees:
            reproduced += 1

        should_write = write and (force or had is None or abs(float(had) - 0.5) < 1e-9)
        if should_write:
            anim["impactFraction"] = value
            sidecar["defaultAnimation"] = anim
            out = json.dumps(sidecar, indent=2, ensure_ascii=False)
            io.open(path, "wb").write(out.replace("\n", "\r\n").encode("utf-8"))
            rewritten += 1

        rows.append((hero, clip, had, value, frame, width,
                     "same" if agrees else ("none" if had is None else "DIFFERS")))

    print("%-32s %-7s %-8s %-8s %-6s %-6s %s"
          % ("hero", "clip", "was", "derived", "frame", "width", ""))
    for r in rows:
        print("%-32s %-7s %-8s %-8s %-6s %-6s %s"
              % (r[0][:32], r[1], "-" if r[2] is None else r[2], r[3], r[4], r[5], r[6]))
    print("")
    print("%d clip(s): %d reproduce the committed value, %d had none, %d differ%s"
          % (len(rows), reproduced, missing,
             len(rows) - reproduced - missing,
             ("; %d written" % rewritten) if rewritten else ""))


if __name__ == "__main__":
    main()
