"""Renders the teaser's title card as PNGs with alpha, for ffmpeg to composite.

    python Tools/teaser/title_card.py <out_dir>

Writes title_16x9.png (1920x1080) and title_vertical.png (1080x1920). Needs Pillow.

Built from the Emberforge tokens so the card is the game's own type, not a trailer font: the
wordmark is Cinzel (Emberforge.TitleFont), the line under it is Barlow Condensed SemiBold
(Emberforge.UiFont), the gold is the GoldLit / GoldMid / GoldLow / GoldDeep ramp every Emberforge
frame is drawn in, and the dark edge is Emberforge.Outline, the one outline colour the design system
puts on every element. Nothing here is a new colour; each hex below names the token it copies.

Transparent everywhere except the type and its glow, so it can sit over black or over a frame.
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FONTS = os.path.join(ROOT, "Assets", "Resources", "Fonts")

# Emberforge.cs tokens.
OUTLINE = (0x0B, 0x0C, 0x18)        # Emberforge.Outline
GOLD_LIT = (0xFF, 0xF4, 0xC2)       # Emberforge.GoldLit
GOLD_MID = (0xF2, 0xC7, 0x66)       # Emberforge.GoldMid
GOLD_LOW = (0xD3, 0x9A, 0x36)       # Emberforge.GoldLow
GOLD_DEEP = (0x9C, 0x64, 0x20)      # Emberforge.GoldDeep
EMBER = (0xE0, 0x50, 0x2A)          # Emberforge.RibbonMid, the ember accent
TEXT_QUIET = (0xA9, 0x93, 0x80)     # Emberforge.TextQuiet

WORD = "EMBERFALL"
LINE = "A FIRST LOOK"


def gradient(size, stops):
    """A vertical gradient through (position, colour) stops, as an RGB image."""
    w, h = size
    img = Image.new("RGB", (1, h))
    px = img.load()
    for y in range(h):
        k = y / max(1, h - 1)
        for i in range(len(stops) - 1):
            a, ca = stops[i]
            b, cb = stops[i + 1]
            if a <= k <= b:
                t = (k - a) / max(1e-6, b - a)
                px[0, y] = tuple(int(ca[c] + (cb[c] - ca[c]) * t) for c in range(3))
                break
    return img.resize((w, h))


def tracked(draw, text, font, tracking):
    """Width of text drawn with extra letter spacing."""
    total = 0
    for i, ch in enumerate(text):
        total += draw.textlength(ch, font=font)
        if i < len(text) - 1:
            total += tracking
    return total


def draw_tracked(draw, xy, text, font, tracking, **kw):
    x, y = xy
    for ch in text:
        draw.text((x, y), ch, font=font, **kw)
        x += draw.textlength(ch, font=font) + tracking


def render(width, height, word_px, out_path):
    scale = 2  # supersample, then downsample, for clean edges
    W, H = width * scale, height * scale
    title_font = ImageFont.truetype(os.path.join(FONTS, "Cinzel.ttf"), word_px * scale)
    line_font = ImageFont.truetype(os.path.join(FONTS, "BarlowCondensed-SemiBold.ttf"),
                                   int(word_px * 0.2) * scale)

    probe = ImageDraw.Draw(Image.new("L", (W, H)))
    track = int(word_px * 0.16) * scale
    word_w = tracked(probe, WORD, title_font, track)
    asc, desc = title_font.getmetrics()
    word_x = (W - word_w) / 2
    word_y = H * 0.5 - asc * 0.62

    # ---- the wordmark as a mask
    mask = Image.new("L", (W, H), 0)
    draw_tracked(ImageDraw.Draw(mask), (word_x, word_y), WORD, title_font, track, fill=255)
    bbox = mask.getbbox()

    # ---- the outline: the mask grown by the Emberforge stroke, in Outline
    stroke = max(2, int(word_px * 0.035)) * scale
    edge = mask.filter(ImageFilter.MaxFilter(stroke * 2 + 1))

    # ---- the glow: ember, soft and low, so the word reads as lit rather than printed
    glow = mask.filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.GaussianBlur(word_px * 0.42 * scale))

    card = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    card.paste(Image.new("RGBA", (W, H), EMBER + (255,)), (0, 0), glow.point(lambda v: int(v * 0.55)))
    card.paste(Image.new("RGBA", (W, H), OUTLINE + (255,)), (0, 0), edge)

    top, bottom = bbox[1], bbox[3]
    face = Image.new("RGB", (W, H), GOLD_MID)
    ramp = gradient((W, bottom - top), [(0.0, GOLD_LIT), (0.38, GOLD_MID), (0.72, GOLD_LOW), (1.0, GOLD_DEEP)])
    face.paste(ramp, (0, top))
    card.paste(face.convert("RGBA"), (0, 0), mask)

    # ---- the rule and the line beneath: quiet, tracked wide, one step down the type scale
    d = ImageDraw.Draw(card)
    rule_y = bottom + int(word_px * 0.34) * scale
    rule_half = int(word_w * 0.20)
    cx = W // 2
    for i in range(rule_half):
        a = int(200 * (1 - (i / rule_half) ** 1.6))
        d.line([(cx - i, rule_y), (cx - i, rule_y + scale)], fill=GOLD_LOW + (a,))
        d.line([(cx + i, rule_y), (cx + i, rule_y + scale)], fill=GOLD_LOW + (a,))
    lt = int(word_px * 0.2 * 0.5) * scale
    line_w = tracked(d, LINE, line_font, lt)
    draw_tracked(d, ((W - line_w) / 2, rule_y + int(word_px * 0.16) * scale), LINE, line_font, lt,
                 fill=TEXT_QUIET + (235,))

    card = card.resize((width, height), Image.LANCZOS)
    card.save(out_path)
    print("wrote", out_path)


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Media", "title")
    os.makedirs(out, exist_ok=True)
    render(1920, 1080, 150, os.path.join(out, "title_16x9.png"))
    render(1080, 1920, 118, os.path.join(out, "title_vertical.png"))


if __name__ == "__main__":
    main()
