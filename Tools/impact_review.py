# -*- coding: utf-8 -*-
"""Contact sheets for judging the impact frame of every attack clip.

impactFraction says WHEN in a clip the blow lands, and the game hangs the damage number, the
flash and the hit reaction off it. It has always been derived - peak horizontal extent per frame,
see derive_clip_impact_fractions.py - and that measurement is right for a side swing and wrong for
an overhead one, where the raised weapon is widest at the top of the windup.

So it gets looked at. This writes one sheet per hero showing each attack clip across its length
with the chosen frame boxed, which is the artefact an artist can confirm or correct without
opening the project.

    python Tools/impact_review.py                 # a sheet per hero into Tools/ImpactReview
    python Tools/impact_review.py --zoom kael_the_unyielding basic 24 44
"""
import io
import json
import os
import re
import sys

try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError:
    sys.exit("Pillow is required: python -m pip install Pillow")

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
UNITS = os.path.join(ROOT, "Assets", "Resources", "Sprites", "Units")
OUT = os.path.join(ROOT, "Tools", "ImpactReview")

ATTACK = ("basic", "basic2", "skill")
COLUMNS = 12
BG = (22, 22, 28)
INK = (236, 240, 248)
DIM = (150, 158, 172)
MARK = (255, 176, 48)
HAND = (96, 200, 255)


def font(size):
    for path in (r"C:\Windows\Fonts\arialbd.ttf", r"C:\Windows\Fonts\arial.ttf"):
        if os.path.exists(path):
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def clips_of(hero):
    out = []
    for clip in ATTACK:
        p = os.path.join(UNITS, "%s_%s.json" % (hero, clip))
        if os.path.exists(p):
            out.append((clip, json.load(io.open(p, encoding="utf-8"))))
    return out


def geometry(sidecar):
    anim = sidecar.get("defaultAnimation") or {}
    sheet = sidecar.get("sheet") or {}
    start = int(anim.get("start", 0))
    end = int(anim.get("end", start))
    cols = max(1, int(sheet.get("cols", 1)))
    rows = max(1, int(sheet.get("rows", 1)))
    return start, end, cols, rows


def frame_image(png, cols, rows, n, cache={}):
    img = cache.get(png)
    if img is None:
        img = Image.open(png).convert("RGBA")
        cache[png] = img
    fw = img.size[0] // cols
    fh = img.size[1] // rows
    c, r = n % cols, n // cols
    return img.crop((c * fw, r * fh, c * fw + fw, r * fh + fh))


def pick_columns(start, end, marked, count):
    """`count` frames spread across the clip, with the marked frame guaranteed in the list."""
    span = end - start
    picks = [start + int(round(span * i / float(count - 1))) for i in range(count)]
    if marked not in picks:
        # drop whichever sample is closest to the marked frame and insert it in order
        nearest = min(range(len(picks)), key=lambda i: abs(picks[i] - marked))
        picks[nearest] = marked
    return sorted(set(picks))


def sheet_for(hero, zoom=None):
    entries = clips_of(hero)
    if not entries:
        return None
    f_big, f_small = font(30), font(22)

    cell_w = cell_h = None
    rows_data = []
    for clip, sidecar in entries:
        start, end, cols, rows = geometry(sidecar)
        anim = sidecar.get("defaultAnimation") or {}
        frac = float(anim.get("impactFraction", 0.5))
        by_hand = anim.get("impactSetBy") == "hand"
        marked = start + int(round(frac * (end - start)))
        if zoom and zoom[0] == clip:
            lo, hi = max(start, zoom[1]), min(end, zoom[2])
            picks = [lo + int(round((hi - lo) * i / float(COLUMNS - 1))) for i in range(COLUMNS)]
            picks = sorted(set(picks))
        else:
            picks = pick_columns(start, end, marked, COLUMNS)
        png = os.path.join(UNITS, "%s_%s.png" % (hero, clip))
        img = Image.open(png)
        cell_w = img.size[0] // cols
        cell_h = img.size[1] // rows
        img.close()
        rows_data.append((clip, start, end, cols, rows, png, picks, marked, frac, by_hand))

    head = 44
    label = 30
    width = cell_w * COLUMNS
    height = head + len(rows_data) * (cell_h + label + 34)
    out = Image.new("RGB", (width, height), BG)
    d = ImageDraw.Draw(out)
    d.text((12, 10), hero.replace("_", " "), font=f_big, fill=INK)

    y = head
    for clip, start, end, cols, rows, png, picks, marked, frac, by_hand in rows_data:
        tag = "%s   frames %d-%d   impact %.3f = frame %d%s" % (
            clip, start, end, frac, marked, "   HAND-SET" if by_hand else "")
        d.text((12, y), tag, font=f_small, fill=HAND if by_hand else DIM)
        y += 30
        for i, n in enumerate(picks[:COLUMNS]):
            x = i * cell_w
            out.paste(frame_image(png, cols, rows, n), (x, y), frame_image(png, cols, rows, n))
            colour = (HAND if by_hand else MARK) if n == marked else (60, 62, 72)
            w = 5 if n == marked else 1
            d.rectangle([x, y, x + cell_w - 1, y + cell_h - 1], outline=colour, width=w)
            d.text((x + 8, y + cell_h - 26), str(n), font=f_small,
                   fill=(HAND if by_hand else MARK) if n == marked else DIM)
        y += cell_h + 34
    return out


def main():
    args = sys.argv[1:]
    if not os.path.isdir(OUT):
        os.makedirs(OUT)

    if args and args[0] == "--zoom":
        hero, clip, lo, hi = args[1], args[2], int(args[3]), int(args[4])
        img = sheet_for(hero, zoom=(clip, lo, hi))
        p = os.path.join(OUT, "zoom-%s-%s-%d-%d.png" % (hero, clip, lo, hi))
        img.save(p)
        print(p)
        return

    heroes = sorted(set(m.group(1) for m in
                        (re.match(r"^(.*)_(basic2|basic|skill)\.json$", n)
                         for n in os.listdir(UNITS)) if m))
    for hero in heroes:
        img = sheet_for(hero)
        if img is None:
            continue
        p = os.path.join(OUT, hero + ".png")
        img.save(p)
        print(p)
    print("%d sheet(s) -> %s" % (len(heroes), OUT))


if __name__ == "__main__":
    main()
