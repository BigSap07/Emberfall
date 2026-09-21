# -*- coding: utf-8 -*-
"""Draw the one placeholder mannequin every undrawn hero stands in for.

THE POINT IS THAT THERE IS EXACTLY ONE FILE. Sixty-three of the ninety-four heroes have no art
yet, and a magenta square for each of them says "something is broken" when nothing is. A single
neutral mannequin, tinted per hero by aspect at runtime, says "not drawn yet" instead - and when
an artist wants a better placeholder they replace this one PNG and every undrawn hero changes.

IT IS BUILT TO THE SAME MEASUREMENTS AS THE REAL CHIBIS, so a line-up of drawn and undrawn heroes
keeps its proportions:

    frame          220 x 260, the size every delivered sheet downsamples to
    bodyFraction   0.558, the median of the 31 delivered heroes (range 0.508 - 0.827)
    footFraction   0.220, their median (range 0.096 - 0.238)

Those two fractions are what UnitRig.BuildSprite uses to decide how tall the quad is and how far
below the ground line it hangs, so matching them is what makes the dummy stand at the same height
on the same ground as a drawn hero rather than floating or sinking.

It is drawn in WHITE with flat internal values, because the material tints it by multiplying -
white takes the aspect colour exactly, and the small value steps between head, torso and limbs
survive the tint as shading rather than becoming three different hues.

    python Tools/make_placeholder_dummy.py
"""
import io
import os
import sys

try:
    from PIL import Image, ImageDraw
except ImportError:
    sys.exit("Pillow is required: python -m pip install Pillow")

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Sprites", "Units", "placeholder_dummy.png")
TEMPLATE_META = os.path.join(ROOT, "Assets", "Resources", "Sprites", "Units",
                             "kael_the_unyielding_idle.png.meta")

W, H = 220, 260
BODY_FRACTION = 0.558
FOOT_FRACTION = 0.220

# Supersampled, then reduced - the only way to get clean curves out of PIL's hard-edged ellipse.
SS = 4

# Flat values, not colours. The material multiplies the aspect hue through these.
HEAD = (255, 255, 255, 255)
TORSO = (226, 226, 232, 255)
LIMB = (198, 198, 206, 255)
DARK = (168, 168, 178, 255)


def ellipse(d, cx, cy, rx, ry, fill):
    d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=fill)


def capsule(d, x0, y0, x1, y1, r, fill):
    d.rounded_rectangle([x0, y0, x1, y1], radius=r, fill=fill)


def main():
    img = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    foot_px = FOOT_FRACTION * H          # empty margin below the feet
    body_px = BODY_FRACTION * H          # the drawn figure
    bottom = (H - foot_px) * SS          # ground line, in supersampled pixels
    top = bottom - body_px * SS
    cx = W * 0.5 * SS

    # A chibi: head a little over a third of the whole figure. Leaned slightly toward the
    # viewer's right, which is the three-quarter turn DESIGN-ART-DELIVERY asks every sprite to be
    # drawn at - so the mannequin reads as facing the same way the real art does, and the engine's
    # per-side mirror does the right thing to it.
    lean = 6 * SS
    head_r = body_px * 0.195 * SS
    head_cy = top + head_r * 1.05
    shoulder_y = head_cy + head_r * 1.15
    hip_y = bottom - body_px * 0.34 * SS

    # legs, drawn first so the torso overlaps them at the hip
    leg_w = body_px * 0.085 * SS
    capsule(d, cx - leg_w * 1.9, hip_y, cx - leg_w * 0.2, bottom, leg_w, LIMB)
    capsule(d, cx + leg_w * 0.3 + lean, hip_y, cx + leg_w * 2.0 + lean, bottom, leg_w, DARK)

    # torso, a tapered capsule
    torso_w = body_px * 0.165 * SS
    capsule(d, cx - torso_w + lean * 0.3, shoulder_y, cx + torso_w + lean * 0.3, hip_y + leg_w,
            torso_w * 0.62, TORSO)

    # arms: the far one behind the torso, the near one across the front
    arm_w = body_px * 0.062 * SS
    capsule(d, cx - torso_w - arm_w * 2.2, shoulder_y + arm_w,
            cx - torso_w - arm_w * 0.2, hip_y + arm_w * 2.0, arm_w, DARK)
    capsule(d, cx + torso_w - arm_w * 0.4 + lean, shoulder_y + arm_w,
            cx + torso_w + arm_w * 1.8 + lean, hip_y + arm_w * 1.4, arm_w, LIMB)

    # head last, so it sits over the shoulders
    ellipse(d, cx + lean * 0.5, head_cy, head_r, head_r * 0.94, HEAD)

    # A single flat marker where a face would be, so the mannequin has a front and the mirror is
    # visible on it. Deliberately not eyes: this must not read as a character.
    mark_r = head_r * 0.16
    ellipse(d, cx + lean * 0.5 + head_r * 0.42, head_cy + head_r * 0.06, mark_r, mark_r, DARK)

    # PLACED TO THE PIXEL, not merely drawn near the right place. The first cut of this drew the
    # figure at the target fractions and saved the canvas, and the saved file measured 0.208 /
    # 0.573 instead of 0.220 / 0.558 - antialiasing spreads alpha past the shapes, so where the
    # figure was DRAWN and where it ENDED UP were a few pixels apart. That gap is not cosmetic:
    # BuildSprite takes these two fractions as an instruction, so a file that disagrees with them
    # stands at a height and on a ground line that are both slightly wrong, and no error says so.
    #
    # So the drawing is cropped to its own rendered alpha and then placed: scaled to exactly
    # BODY_PX tall and seated with its lowest opaque row exactly FOOT_PX above the bottom edge.
    # The fractions below are then true of the file by construction rather than by intention.
    box = img.getbbox()
    figure = img.crop(box)
    body_px = int(round(BODY_FRACTION * H))
    foot_px = int(round(FOOT_FRACTION * H))
    scaled_w = max(1, int(round(figure.size[0] * body_px / float(figure.size[1]))))
    figure = figure.resize((scaled_w, body_px), Image.LANCZOS)

    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    out.paste(figure, ((W - scaled_w) // 2, H - foot_px - body_px), figure)
    out.save(OUT)
    print("wrote %s (%dx%d)" % (OUT, W, H))

    # Measured back off the saved file, because that is the only number that is true.
    check = Image.open(OUT)
    x0, y0, x1, y1 = check.getbbox()
    print("   content bbox %s" % ((x0, y0, x1, y1),))
    print("   bodyFraction %.3f  (%d px)" % ((y1 - y0) / float(H), y1 - y0))
    print("   footFraction %.3f  (%d px)" % ((H - y1) / float(H), H - y1))

    meta = OUT + ".meta"
    if not os.path.exists(meta) and os.path.exists(TEMPLATE_META):
        text = io.open(TEMPLATE_META, encoding="utf-8").read()
        # A fresh deterministic guid, so the asset has a stable identity in the project.
        text = text.replace("42b0ca10037745d8b2214b5af4928d57", "d17b9a4c0e3f4a1e8b6c2f5d9a0e7b31", 1)
        io.open(meta, "w", encoding="utf-8", newline="").write(text)
        print("wrote %s" % meta)


if __name__ == "__main__":
    main()
