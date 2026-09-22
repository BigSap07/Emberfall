#!/usr/bin/env python3
"""Put a before/after capture pair side by side, and say how far apart they are.

Written for the texture-size pass, where the question is not "did the screen change" - a screen
that changed is the whole point of a layout capture - but "did the ART get worse". So this does
two things a plain pair of PNGs does not:

  * a full-frame pair, for the overall look, with a caption strip naming which side is which;
  * a ZOOM pair at 3x nearest-neighbour on a named crop, because a compression artifact at 1:1 on
    a 1920x1080 frame is smaller than the eye can resolve on a page, and the only honest way to
    look at one is magnified with no resampling of its own.

It also prints the mean absolute difference per channel. That number is a tripwire, not a verdict:
the battle capture cannot be identical between runs (BattleScreen.RunSimulation seeds off
Time.unscaledTime, so the fight differs), and a static screen that moves at all is worth a look.
"""

import os
import sys
from PIL import Image, ImageDraw

ZOOM = 3
BAR = 34


def label(img, text):
    out = Image.new("RGB", (img.width, img.height + BAR), (16, 16, 20))
    out.paste(img.convert("RGB"), (0, BAR))
    d = ImageDraw.Draw(out)
    d.text((10, 10), text, fill=(235, 225, 210))
    return out


def pair(before, after, dest, crop=None, zoom=ZOOM):
    a = Image.open(before)
    b = Image.open(after)
    if crop:
        x, y, w, h = crop
        a = a.crop((x, y, x + w, y + h)).resize((w * zoom, h * zoom), Image.NEAREST)
        b = b.crop((x, y, x + w, y + h)).resize((w * zoom, h * zoom), Image.NEAREST)
    a = label(a, "BEFORE  " + os.path.basename(before))
    b = label(b, "AFTER  " + os.path.basename(after))
    gap = 12
    out = Image.new("RGB", (a.width + gap + b.width, max(a.height, b.height)), (16, 16, 20))
    out.paste(a, (0, 0))
    out.paste(b, (a.width + gap, 0))
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    out.save(dest)
    return dest


def mad(before, after):
    a = Image.open(before).convert("RGB")
    b = Image.open(after).convert("RGB")
    if a.size != b.size:
        return None
    pa, pb = a.load(), b.load()
    total = 0
    n = 0
    step = 3        # every third pixel in both axes; 230k samples is plenty for a mean
    for y in range(0, a.height, step):
        for x in range(0, a.width, step):
            ra, ga, ba_ = pa[x, y]
            rb, gb, bb = pb[x, y]
            total += abs(ra - rb) + abs(ga - gb) + abs(ba_ - bb)
            n += 3
    return total / float(n)


if __name__ == "__main__":
    before_dir, after_dir, out_dir = sys.argv[1], sys.argv[2], sys.argv[3]
    # name -> crop that actually shows this group's art, in 1920x1080 capture coordinates.
    CROPS = {
        # Sylven and Kiva, the two clearest full-body sprites on the player's line.
        "battle": ("sprite sheets: Kiva, centre of the player line", (560, 620, 420, 300)),
        # The hero-detail badge is the largest a portrait is ever drawn.
        "herodetail": ("portrait: the hero-detail badge", (330, 380, 320, 300)),
        # Three badges from the top row of the list, where a portrait is minified hardest.
        "heroes": ("portraits: the hero list at list size", (380, 270, 640, 220)),
        # The painting behind the Hub, which is the group that had nPOTScale forcing it to 2:1.
        "hub": ("hub painting: the market and the obelisk", (620, 300, 640, 420)),
    }
    for name in sorted(CROPS):
        bpath = os.path.join(before_dir, name + ".png")
        apath = os.path.join(after_dir, name + ".png")
        if not (os.path.exists(bpath) and os.path.exists(apath)):
            print("  %-12s skipped, missing a side" % name)
            continue
        full = pair(bpath, apath, os.path.join(out_dir, name + "_full.png"))
        what, crop = CROPS[name]
        zoomed = pair(bpath, apath, os.path.join(out_dir, name + "_zoom.png"), crop)
        d = mad(bpath, apath)
        print("  %-12s mean abs diff %s" % (name, ("%.2f / 255" % d) if d is not None else "n/a"))
        print("               %s" % full)
        print("               %s   (%s, %dx nearest)" % (zoomed, what, ZOOM))
