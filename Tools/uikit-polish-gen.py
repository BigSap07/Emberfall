#!/usr/bin/env python3
"""R26 UI KIT POLISH: code-drawn pieces and re-exports for the kit in Assets/Resources/Art/UIKit.

What it makes (PIL only, nothing traced or copied from any reference image):
  corner_ornament.png   a gold filigree corner (top-left; the UI mirrors it into the other three), drawn
                        here from curves: a tapered L bracket, a volute, two buds and an ember jewel.
  btn_primary.png       the call-to-action plate in a clear warm orange (was copper brown), gold rim kept.
  btn_secondary.png     a lighter warm-brown plate with a gold hairline, so it separates from a dark panel.
  btn_disabled.png      a lighter, flatter stone plate with a thin gold hairline at low strength.
  panel_large.png       the same panel with a stronger gilded rim (brighter gold, an outer dark line and an
  panel_small.png       inner gold line).

The five re-exports read their PRISTINE inputs from Tools/uikit-polish-src (copies of the pieces as they were
before this pass), never from the file they write, so the script is re-runnable and idempotent. Image sizes and
therefore every 9-slice border in the .meta files are unchanged; only corner_ornament.png is a new asset, and
its .meta is written here with the same BC7 and ASTC 4x4 settings Tools/uikit-import.py gives every piece.

Run:  python Tools/uikit-polish-gen.py          writes the PNGs and the one new .meta
      python Tools/uikit-polish-gen.py --check  proves the outputs are what a run would write (no writes)
Output is deterministic: no random numbers, no timestamps.
"""
import hashlib
import math
import os
import re
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SRC = os.path.join(ROOT, "Tools", "uikit-polish-src")
DEST = os.path.join(ROOT, "Assets", "Resources", "Art", "UIKit")
TEMPLATE = os.path.join(ROOT, "Assets", "Resources", "Art", "UI", "panel.png.meta")

CHECK = "--check" in sys.argv

GOLD_HI = (255, 238, 170)
GOLD = (232, 176, 64)
GOLD_LO = (150, 96, 28)
DARK = (34, 18, 8)


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def ramp(t, stops):
    """Colour at t (0..1) along a list of (position, colour) stops."""
    t = max(0.0, min(1.0, t))
    for i in range(len(stops) - 1):
        p0, c0 = stops[i]
        p1, c1 = stops[i + 1]
        if t <= p1:
            return lerp(c0, c1, 0.0 if p1 == p0 else (t - p0) / (p1 - p0))
    return stops[-1][1]


# ---------------------------------------------------------------------------------------------- ornament

def taper_line(draw, p0, p1, w0, w1, steps=140, fill=255):
    for i in range(steps + 1):
        t = i / float(steps)
        x = p0[0] + (p1[0] - p0[0]) * t
        y = p0[1] + (p1[1] - p0[1]) * t
        r = (w0 + (w1 - w0) * t) * 0.5
        draw.ellipse((x - r, y - r, x + r, y + r), fill=fill)


def spiral(draw, cx, cy, r_out, r_in, turns, a0, w_out, w_in, steps=400, fill=255):
    for i in range(steps + 1):
        t = i / float(steps)
        ang = a0 + t * turns * 2.0 * math.pi
        r = r_out + (r_in - r_out) * (t ** 0.85)
        w = (w_out + (w_in - w_out) * t) * 0.5
        x = cx + r * math.cos(ang)
        y = cy + r * math.sin(ang)
        draw.ellipse((x - w, y - w, x + w, y + w), fill=fill)


def leaf(draw, cx, cy, length, width, angle, fill=255):
    pts = []
    for i in range(0, 41):
        t = i / 40.0
        x = (t - 0.5) * length
        y = math.sin(t * math.pi) * width * 0.5
        pts.append((x, y))
    for i in range(40, -1, -1):
        t = i / 40.0
        x = (t - 0.5) * length
        y = -math.sin(t * math.pi) * width * 0.5
        pts.append((x, y))
    ca, sa = math.cos(angle), math.sin(angle)
    draw.polygon([(cx + x * ca - y * sa, cy + x * sa + y * ca) for x, y in pts], fill=fill)


def draw_ornament():
    """Top-left corner filigree on a 128 px tile, drawn at 4x and reduced."""
    S = 4
    N = 128 * S
    mask = Image.new("L", (N, N), 0)
    d = ImageDraw.Draw(mask)

    # the L bracket along the top and left edges, thick at the corner, tapering to a point
    taper_line(d, (30 * S, 30 * S), (120 * S, 30 * S), 17 * S, 5 * S)
    taper_line(d, (30 * S, 30 * S), (30 * S, 120 * S), 17 * S, 5 * S)
    # a second, lighter line inside it, so the corner reads as layered metal
    taper_line(d, (46 * S, 46 * S), (92 * S, 46 * S), 7 * S, 3 * S)
    taper_line(d, (46 * S, 46 * S), (46 * S, 92 * S), 7 * S, 3 * S)
    # the volute that sweeps into the room, springing from the corner
    spiral(d, 74 * S, 74 * S, 25 * S, 5 * S, 1.55, math.radians(200), 7 * S, 3 * S)
    # a counter curl that closes the shape
    spiral(d, 98 * S, 52 * S, 11 * S, 2 * S, 1.25, math.radians(40), 5 * S, 2 * S)
    spiral(d, 52 * S, 98 * S, 11 * S, 2 * S, 1.25, math.radians(310), 5 * S, 2 * S)
    # buds at the bracket ends
    leaf(d, 112 * S, 34 * S, 22 * S, 10 * S, math.radians(8))
    leaf(d, 34 * S, 112 * S, 22 * S, 10 * S, math.radians(82))
    # the jewel's seat: a solid diamond on the corner
    c = 34 * S
    r = 17 * S
    d.polygon([(c, c - r), (c + r, c), (c, c + r), (c - r, c)], fill=255)

    # outline: the mask grown, drawn dark under the gold
    outline = mask.filter(ImageFilter.MaxFilter(2 * 3 * S + 1)).filter(ImageFilter.GaussianBlur(S * 0.6))
    # gold fill with a bevel: a light edge top-left, a dark edge bottom-right
    blur = mask.filter(ImageFilter.GaussianBlur(S * 1.6))
    shifted_a = ImageChops.offset(blur, int(S * 1.8), int(S * 1.8))
    shifted_b = ImageChops.offset(blur, -int(S * 1.8), -int(S * 1.8))
    light = ImageChops.subtract(blur, shifted_a)
    shade = ImageChops.subtract(blur, shifted_b)

    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    px = img.load()
    mpx = mask.load()
    opx = outline.load()
    lpx = light.load()
    spx = shade.load()
    for y in range(N):
        for x in range(N):
            o = opx[x, y]
            if o == 0:
                continue
            m = mpx[x, y]
            if m == 0:
                px[x, y] = DARK + (min(255, int(o * 0.95)),)
                continue
            t = (x + y) / float(2 * N)
            base = ramp(t * 1.6, [(0.0, GOLD_HI), (0.35, GOLD), (1.0, GOLD_LO)])
            hi = min(1.0, lpx[x, y] / 60.0)
            lo = min(1.0, spx[x, y] / 60.0)
            col = lerp(lerp(base, (255, 250, 220), hi * 0.7), (96, 56, 14), lo * 0.6)
            px[x, y] = col + (255,)

    # the ember jewel inside the seat
    jd = ImageDraw.Draw(img)
    jr = 10 * S
    jd.polygon([(c, c - jr), (c + jr, c), (c, c + jr), (c - jr, c)], fill=(226, 70, 40, 255))
    jd.polygon([(c, c - jr), (c + jr, c), (c, c)], fill=(255, 140, 90, 255))
    jd.polygon([(c, c), (c + jr, c), (c, c + jr)], fill=(168, 38, 22, 255))
    jd.line([(c, c - jr), (c + jr, c), (c, c + jr), (c - jr, c), (c, c - jr)], fill=(255, 226, 150, 255), width=S)
    return img.resize((128, 128), Image.LANCZOS)


# ---------------------------------------------------------------------------------------------- plates

def interior_mask(alpha, inset):
    """Where the plate is: the opaque area pulled in by `inset` pixels from every edge (rim excluded)."""
    solid = alpha.point(lambda v: 255 if v > 128 else 0)
    size = 2 * inset + 1
    return solid.filter(ImageFilter.MinFilter(size)).filter(ImageFilter.GaussianBlur(1.2))


def luminance(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def recolor_plate(src, inset, stops, texture, hairline=None, hair_inset=None, rim_boost=1.0, top_light=0.18):
    """Re-ramp the plate's interior through `stops` keyed on the source's luminance (keeping its painted
    texture at amplitude `texture`), leave the rim, optionally add a gold hairline just inside the rim."""
    src = src.convert("RGBA")
    w, h = src.size
    alpha = src.getchannel("A")
    inner = interior_mask(alpha, inset)
    out = src.copy()
    sp = src.load()
    ip = inner.load()
    op = out.load()
    # mean luminance of the interior, so the texture is the deviation from it
    total = 0.0
    n = 0
    for y in range(0, h, 2):
        for x in range(0, w, 2):
            if ip[x, y] > 200:
                total += luminance(sp[x, y])
                n += 1
    mean = total / max(1, n)
    for y in range(h):
        for x in range(w):
            m = ip[x, y]
            r, g, b, a = sp[x, y]
            if a == 0:
                continue
            if m > 0:
                dev = (luminance((r, g, b)) - mean) / 255.0
                t = 0.5 + dev * texture - (y / float(h) - 0.5) * top_light
                col = ramp(t, stops)
                k = m / 255.0
                op[x, y] = (int(col[0] * k + r * (1 - k)), int(col[1] * k + g * (1 - k)), int(col[2] * k + b * (1 - k)), a)
            elif rim_boost != 1.0:
                f = rim_boost
                op[x, y] = (min(255, int(r * f)), min(255, int(g * f)), min(255, int(b * f)), a)
    if hairline is not None:
        edge = interior_mask(alpha, inset)
        deeper = interior_mask(alpha, inset + (hair_inset or 3))
        line = ImageChops.subtract(edge, deeper)
        lp = line.load()
        for y in range(h):
            for x in range(w):
                v = lp[x, y]
                if v > 0 and op[x, y][3] > 0:
                    k = v / 255.0 * hairline[3] / 255.0
                    r, g, b, a = op[x, y]
                    op[x, y] = (int(hairline[0] * k + r * (1 - k)), int(hairline[1] * k + g * (1 - k)),
                                int(hairline[2] * k + b * (1 - k)), a)
    return out


def gild_panel(src, line_width):
    """A stronger gilded rim. The plate is found by COLOUR (dark, muted brown), not by distance from the
    edge, because the big panel's corner blocks reach far further in than its sides do. Every other opaque pixel
    is rim: its gold is pushed brighter and more saturated, a dark line is drawn on the silhouette's edge, and a
    bright gold line sits where the rim meets the plate. The plate itself is untouched."""
    src = src.convert("RGBA")
    w, h = src.size
    alpha = src.getchannel("A")
    solid = alpha.point(lambda v: 255 if v > 128 else 0)
    lum = Image.new("L", (w, h), 0)
    lp = lum.load()
    sp = src.load()
    for y in range(h):
        for x in range(w):
            r, g, b, a = sp[x, y]
            mx = max(r, g, b)
            sat = (mx - min(r, g, b)) / float(mx) if mx > 0 else 0.0
            # a plate pixel is dark AND muted brown; the corner bevels are dark but saturated bronze (rim)
            lp[x, y] = int(luminance((r, g, b))) if (a > 128 and sat < 0.56) else 255
    plate = lum.point(lambda v: 255 if v <= 88 else 0).filter(ImageFilter.MedianFilter(7))
    plate = ImageChops.multiply(plate, solid)
    grown = plate.filter(ImageFilter.MaxFilter(2 * line_width + 1))
    line = ImageChops.subtract(grown, plate)
    out = src.copy()
    op = out.load()
    pp = plate.load()
    for y in range(h):
        for x in range(w):
            r, g, b, a = sp[x, y]
            if a == 0 or pp[x, y] > 0:
                continue
            t = max(0.0, min(1.0, (luminance((r, g, b)) - 70.0) / 150.0))
            col = ramp(t, [(0.0, (130, 80, 20)), (0.45, (232, 168, 54)), (1.0, (255, 236, 156))])
            op[x, y] = col + (a,)
    # the dark line on the silhouette's edge
    inner = solid.filter(ImageFilter.MinFilter(2 * 2 + 1))
    edge = ImageChops.subtract(solid, inner)
    ep = edge.load()
    for y in range(h):
        for x in range(w):
            if ep[x, y] > 0 and op[x, y][3] > 0:
                r, g, b, a = op[x, y]
                op[x, y] = (int(r * 0.35), int(g * 0.28), int(b * 0.2), a)
    # the bright gold line where the rim meets the plate
    gp = line.filter(ImageFilter.GaussianBlur(0.6)).load()
    for y in range(h):
        for x in range(w):
            v = gp[x, y]
            if v > 0 and op[x, y][3] > 0 and pp[x, y] == 0:
                k = min(1.0, v / 255.0 * 1.25) * 0.95
                r, g, b, a = op[x, y]
                op[x, y] = (int(255 * k + r * (1 - k)), int(230 * k + g * (1 - k)), int(150 * k + b * (1 - k)), a)
    return out


def build_all():
    pieces = {}
    pieces["corner_ornament.png"] = draw_ornament()

    def load(name):
        return Image.open(os.path.join(SRC, name)).convert("RGBA")

    orange = [(0.0, (150, 62, 10)), (0.30, (214, 104, 14)), (0.55, (246, 140, 28)), (0.8, (255, 176, 66)), (1.0, (255, 214, 130))]
    pieces["btn_primary.png"] = recolor_plate(load("btn_primary.png"), 17, orange, texture=1.1,
                                              hairline=(255, 214, 120, 190), hair_inset=3, rim_boost=1.12, top_light=0.35)
    warm = [(0.0, (74, 52, 34)), (0.35, (104, 76, 50)), (0.6, (126, 94, 62)), (1.0, (160, 124, 84))]
    pieces["btn_secondary.png"] = recolor_plate(load("btn_secondary.png"), 12, warm, texture=0.9,
                                                hairline=(238, 196, 108, 200), hair_inset=3, rim_boost=1.1, top_light=0.3)
    stone = [(0.0, (74, 74, 82)), (0.4, (98, 98, 106)), (0.7, (116, 114, 120)), (1.0, (140, 136, 138))]
    pieces["btn_disabled.png"] = recolor_plate(load("btn_disabled.png"), 15, stone, texture=0.55,
                                               hairline=(214, 178, 108, 120), hair_inset=2, rim_boost=0.95, top_light=0.2)
    pieces["panel_large.png"] = gild_panel(load("panel_large.png"), 3)
    pieces["panel_small.png"] = gild_panel(load("panel_small.png"), 3)
    return pieces


# ---------------------------------------------------------------------------------------------- meta

def guid_for(rel):
    # the same scheme Tools/uikit-import.py uses, so the new piece's guid is deterministic
    return hashlib.md5(("emberfall/uikit/" + rel.replace("\\", "/")).encode("utf-8")).hexdigest()


def ornament_meta():
    with open(TEMPLATE, "r", encoding="utf-8") as f:
        s = f.read()
    s = re.sub(r"^guid: .*$", "guid: " + guid_for("corner_ornament.png"), s, count=1, flags=re.M)
    s = re.sub(r"maxTextureSize: \d+", "maxTextureSize: 2048", s)
    s = re.sub(r"spriteBorder: \{.*\}", "spriteBorder: {x: 0, y: 0, z: 0, w: 0}", s)
    s = re.sub(r"spritePixelsToUnits: [\d.]+", "spritePixelsToUnits: 100", s)
    s = re.sub(r"(buildTarget: Android\n(?:    .*\n)*?    textureFormat: )\d+", r"\g<1>48", s)
    s = re.sub(r"(buildTarget: Standalone\n(?:    .*\n)*?    textureFormat: )\d+", r"\g<1>25", s)
    return s.replace("\r\n", "\n")


def png_bytes(img):
    import io
    buf = io.BytesIO()
    img.save(buf, "PNG", optimize=False)
    return buf.getvalue()


def main():
    pieces = build_all()
    problems = []
    for name, img in sorted(pieces.items()):
        path = os.path.join(DEST, name)
        data = png_bytes(img)
        if CHECK:
            if not os.path.isfile(path):
                problems.append("missing " + name)
            else:
                with open(path, "rb") as f:
                    on_disk = Image.open(path).convert("RGBA")
                if on_disk.size != img.size or ImageChops.difference(on_disk, img).getbbox() is not None:
                    problems.append("differs from a fresh run: " + name)
            continue
        with open(path, "wb") as f:
            f.write(data)
        print("wrote %-22s %4dx%-4d %7d bytes" % (name, img.size[0], img.size[1], len(data)))
    meta_path = os.path.join(DEST, "corner_ornament.png.meta")
    meta = ornament_meta()
    if CHECK:
        if not os.path.isfile(meta_path) or open(meta_path, "r", encoding="utf-8", newline="").read().replace("\r\n", "\n") != meta:
            problems.append("corner_ornament.png.meta is not what a run writes")
        for p in problems:
            print("PROBLEM: " + p)
        print("uikit-polish-gen check: " + ("FAILED" if problems else "ok"))
        return 1 if problems else 0
    with open(meta_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(meta)
    print("wrote corner_ornament.png.meta")
    return 0


if __name__ == "__main__":
    sys.exit(main())
