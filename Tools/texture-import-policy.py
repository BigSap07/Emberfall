#!/usr/bin/env python3
"""THE TEXTURE IMPORT POLICY, applied to the .meta files it describes.

Why a script and not an AssetPostprocessor
------------------------------------------
An AssetPostprocessor would be the obvious home for this, and it is deliberately not used.
Tools/build-check.ps1 compiles every file under Assets/Editor against the API shim in
Tools/UnityShim, and that shim carries no texture-importer types at all - no AssetPostprocessor,
no TextureImporter, no TextureImporterPlatformSettings. A postprocessor dropped into Assets/Editor
therefore fails the "unity-code" compile step and shows up as a THIRD harness failure, which
DESIGN-BUILD-ORDER.md defines as a regression rather than a reading.

The propagation the postprocessor would have bought already exists for the one group that grows.
Tools/import-sprite-sheets.ps1 writes every new sheet's .meta by copying ANY existing sheet .meta
out of Sprites/Units and stamping a fresh guid (its note 5). So the 334 sheet metas this script
writes ARE the template for every sheet Pinoy delivers from here on, by construction rather than by
anyone remembering. The other three groups are closed sets.

What the policy is, and what measured it
----------------------------------------
Run with --report to print the model of what each group costs in the built Windows player without
changing anything; run with --apply to write it. Every number below came off a measurement that is
recorded in the group's own comment in GROUPS.
"""

import argparse
import os
import re
import struct
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))

# UnityEditor.TextureImporterFormat members, by their serialised value.
RGBA32 = 4
DXT5 = 12
BC7 = 25
DXT5_CRUNCHED = 29
ASTC_4x4 = 48
ASTC_5x5 = 49
ASTC_6x6 = 50

# Bytes per pixel each format costs in the player, used by --report.
BPP = {RGBA32: 4.0, DXT5: 1.0, BC7: 1.0, DXT5_CRUNCHED: 1.0,
       ASTC_4x4: 1.0, ASTC_5x5: 0.64, ASTC_6x6: 16.0 / 36.0}

FORMAT_NAMES = {RGBA32: "RGBA32", DXT5: "DXT5", BC7: "BC7", DXT5_CRUNCHED: "DXT5 Crunched",
                ASTC_4x4: "ASTC 4x4", ASTC_5x5: "ASTC 5x5", ASTC_6x6: "ASTC 6x6"}


class Group(object):
    def __init__(self, name, folder, max_size, npot, mipmap, filter_mode,
                 win_format, android_format, crunch_quality, note, multiple_of_4_only=False):
        self.name = name
        self.folder = folder
        self.max_size = max_size
        self.npot = npot
        self.mipmap = mipmap
        self.filter_mode = filter_mode
        self.win_format = win_format
        self.android_format = android_format
        self.crunch_quality = crunch_quality
        self.note = note
        # A block format stores 4x4 blocks, and Unity falls back to uncompressed for a texture whose
        # sides are not multiples of 4. With this set, such a file is left exactly as it is (it is
        # tiny, and the report names it) instead of being given an entry that cannot take effect.
        self.multiple_of_4_only = multiple_of_4_only


GROUPS = [
    Group(
        name="Units (battle sprite sheets)",
        folder="Assets/Resources/Sprites/Units",
        # 4096, stated per platform instead of inherited from DefaultTexturePlatform, and at least
        # the largest sheet (3240 wide). maxTextureSize is the one setting that MUST NOT bite
        # here: a sheet is a cols x rows grid of frames, and any clamp rescales the whole sheet
        # and shears that grid, which is the failure import-sprite-sheets.ps1 notes 3 and 5 exist
        # to prevent.
        #
        # The resolution is the cell's, set in Tools/rewire_sprites.py: 336x396. A body occupies
        # bodyFraction (median 0.55) of the cell, so about 218 sheet pixels; on screen at
        # 1920x1080 it spans UnitRig.HumanoidTop (1.71 world units) against BattleStage's
        # orthographicSize 3.65, which is 1.71 / 7.3 * 1080 = 253 px, times cos(28) for the
        # camera's pitch = 223 px at a hero's scale. That is about 1.03 screen pixels per texel
        # for the median hero. The old 220x260 cell put it at 1.57, and a DXT5 block magnified
        # 1.6x is a visible 6 px blotch. 2400x1080 asks for no more: an orthographic camera fixes
        # the vertical, so a wider screen shows more width at the same pixels-per-unit.
        max_size=4096,
        npot=0,            # None. A sheet must never be resized to a power of two.
        mipmap=0,          # Never minified at 1080p, so a mip chain is 33% of nothing.
        filter_mode=1,     # Bilinear. Point would alias at a non-integer magnification.
        # BC7, not crunched. Crunch was the old choice because it shrank the download for free,
        # but crunch only exists for DXT, and DXT5's two colour endpoints per 4x4 block are the
        # blotches. BC7 is the same 8 bits per pixel, so VRAM per pixel does not move; the build
        # grows by the crunch saving. It needs both sheet dimensions to be multiples of 4, which
        # is half of why the cell is 336x396 (see rewire_sprites.py) and not 330x390.
        win_format=BC7,
        # ASTC 6x6, chosen by the frame grid as 5x5 was before it. 336 and 396 both divide by 6,
        # so every frame boundary is also a block boundary and no block mixes the end of one
        # frame with the start of the next - the bleed import-sprite-sheets.ps1 note 3 refuses to
        # let the resampler introduce. At 3.56 bits per pixel it costs 0.69x what 5x5 did per
        # pixel, which pays back most of the 2.33x the larger cell adds.
        android_format=ASTC_6x6,
        crunch_quality=50,  # unused: nothing in this group is crunched
        note="334 sheets at 336x396 cells, almost the whole texture budget",
    ),
    Group(
        name="Portraits",
        folder="Assets/Resources/Sprites/Portraits",
        # 512 unchanged, which is what the icons branch chose, and the measurement agrees with it
        # rather than overriding it: the largest a portrait is ever drawn is the hero-detail badge
        # at about 320 px across, and the hero list draws it at about 130. 512 is still a
        # downscale at the biggest use; 256 would be a 1.25x UPSCALE there.
        max_size=512,
        npot=0,
        mipmap=0,          # The icons branch's choice, kept. See the report for the trade.
        filter_mode=1,
        # BC7 instead of DXT5. Same 8 bits per pixel, so the build does not move; DXT5's three
        # interpolated colours per 4x4 block is exactly what bruises a gold filigree frame and a
        # dark outline, and BC7 does not have that limit.
        win_format=BC7,
        android_format=ASTC_4x4,   # also 8 bpp, so Android does not grow either
        crunch_quality=50,
        note="94 badges",
    ),
    Group(
        name="UI art",
        folder="Assets/Resources/Art/UI",
        # 256. Six of these ten files are 1008x1008 and land in the player as UNCOMPRESSED RGBA32,
        # because DefaultTexturePlatform carries textureCompression 0 and no platform entry
        # overrides it - 23.3 MB of the build, from 0.3 MB of source, for art that nothing in the
        # project currently loads (UISkin.Surface asks for panel, button, disc and panel_frame and
        # for nothing else; everything under Resources ships regardless). No icon slot in this UI
        # draws above about 72 px, so 256 is still three and a half times the largest drawn size.
        max_size=256,
        npot=0,
        mipmap=0,
        filter_mode=1,
        win_format=BC7,        # hard edges and alpha: the one place DXT5 is visibly wrong
        android_format=ASTC_4x4,
        crunch_quality=50,
        note="10 files, 6 of them oversized and uncompressed",
    ),
    Group(
        name="Hit flipbook sheets",
        folder="Assets/Resources/Art/Vfx",
        # 2048, and it must be at least the largest sheet (1040 wide). A flipbook is a cols x rows
        # grid that VfxDirector slices by UV, so a clamp that bit would rescale the whole sheet and
        # shear every cell boundary, the same failure the Units group is written to avoid.
        max_size=2048,
        npot=0,            # None. 780x520 and 1040x1040 are meant, not rounded to 1024.
        mipmap=0,          # A hit is never drawn smaller than its frame, so no mip is sampled.
        filter_mode=1,     # Bilinear: a frame is magnified or near 1:1 at 1080p.
        # BC7: these are pure white RGB with a soft alpha, and alpha is the whole image. BC7 spends
        # as much on alpha as on colour, where DXT5 would step the glow; same 8 bits per pixel.
        win_format=BC7,
        # ASTC 5x5, chosen by the frame and not by taste, exactly as in the Units group. Every
        # sheet was cut to 260 px cells, a multiple of both 4 (BC7) and 5 (ASTC 5x5), so no block
        # on either platform straddles two frames and bleeds one into the next.
        android_format=ASTC_5x5,
        crunch_quality=50,
        note="6 white-on-alpha sheets from the Vefects Lite pack",
    ),
    Group(
        name="Backdrops and the hub painting",
        folder="Assets/Resources/Backgrounds",
        max_size=2048,
        # nPOTScale None, and this is a correction rather than a saving. All five of these carried
        # ToNearest, which force-scales every one of them to 2048x1024: ashen, ember and hollow
        # from 1672x941 (aspect 1.7768 -> 2.0), hub from 1536x828 (1.8551 -> 2.0) and campaign
        # from 960x420 (2.2857 -> 2.0). That is a 12.5% horizontal stretch on four paintings and
        # a squeeze on the fifth, and it is already known to the code that has to live with it:
        # MainMenuScreen feeds its AspectRatioFitter from hard-coded authored dimensions with a
        # comment saying the importer "has already stretched by a different factor per axis", and
        # BattleStage's backdrop comment does its arithmetic with ashen at 1.7768 while the code
        # beside it reads the imported 2.0. Turning the resize off makes both of those true.
        npot=0,
        # Off. The backdrop quad is sized to cover the camera frame and the hub map envelopes its
        # parent, so a painting narrower than the screen is always magnified and a mip below the
        # top level is never sampled. 33% of the group for nothing.
        mipmap=0,
        filter_mode=1,
        # BC7: these are sunset skies, and banding in a smooth gradient is DXT5's signature.
        win_format=BC7,
        # ASTC 6x6 is affordable here and nowhere else in this project. A backdrop carries no cel
        # outline to smear - it sits behind a scrim and under the whole HUD - so it is the one
        # group where a bigger block costs nothing anyone can see.
        android_format=ASTC_6x6,
        crunch_quality=50,
        note="5 paintings, all five currently stretched to a power of two",
    ),
    # R20 UI KIT, three groups because the policy walks one folder each and the three want different ceilings.
    # Tools/uikit-import.py writes these metas and states the same numbers, so --report on them says
    # "0 to write"; the .png files are already at their shipping size, so the ceiling never resamples
    # (Unity would only halve a file larger than it). BC7 on Windows and ASTC 4x4 on Android for all of
    # them: hard edges and gold filigree on alpha, the same call the UI art group makes, and the explicit
    # entries are what keep these out of the RGBA32 trap this file's header describes.
    Group(
        name="UI kit (R20): kit, atlas, icons",
        folder="Assets/Resources/Art/UIKit",
        # 2048 is the ability atlas (2048x896, 109 icons for all 254 abilities). Every other file here is
        # 512 or less and is never touched by this ceiling.
        max_size=2048,
        npot=0, mipmap=0, filter_mode=1,
        win_format=BC7, android_format=ASTC_4x4, crunch_quality=50,
        note="50 sprites and the ability atlas",
    ),
    Group(
        name="UI kit (R20): Store art",
        folder="Assets/Resources/Art/UIKit/store",
        max_size=1024,     # the Wayfarer's Road header is 1024x340; the six offer cards are 512 tall
        npot=0, mipmap=0, filter_mode=1,
        win_format=BC7, android_format=ASTC_4x4, crunch_quality=50,
        note="6 masked offer cards and the header",
    ),
    Group(
        name="UI kit (R20): relic icons",
        folder="Assets/Resources/Art/UIKit/relics",
        max_size=256,
        npot=0, mipmap=0, filter_mode=1,
        win_format=BC7, android_format=ASTC_4x4, crunch_quality=50,
        note="10 relic icons",
    ),
    # R26 EMBERFORGE SPRITES: the code-built UI's 9-slice pieces (panels, buttons, tabs, chips, frames).
    # Their metas never named a Standalone or Android platform, so they inherited
    # DefaultTexturePlatform's textureCompression 0 and shipped as uncompressed RGBA32: 31 files, 1.27 MB
    # of raw pixels in the player from 0.37 MB of PNG. BC7 on Windows and ASTC 4x4 on Android, the call the
    # UI kit groups make for the same kind of art (hard edges, soft gold gradients, alpha), is visually
    # lossless on a 9-slice piece, mips stay off, and the max size stays 2048 (the largest file is 160 px,
    # so nothing is resampled). forge_rule.png is 32x6, not a multiple of 4, and stays as it is.
    Group(
        name="Emberforge sprites (R26)",
        folder="Assets/Resources/Sprites/Emberforge",
        max_size=2048,
        npot=0, mipmap=0, filter_mode=1,
        win_format=BC7, android_format=ASTC_4x4, crunch_quality=50,
        note="32 code-built UI pieces, were RGBA32",
        multiple_of_4_only=True,
    ),
]

PLATFORM_ENTRY = """  - serializedVersion: 4
    buildTarget: {target}
    maxTextureSize: {max_size}
    resizeAlgorithm: 0
    textureFormat: {fmt}
    textureCompression: {compression}
    compressionQuality: {quality}
    crunchedCompression: {crunched}
    allowsAlphaSplitting: 0
    overridden: {overridden}
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: {force_hq}
"""


def platform_block(group):
    out = []
    # DefaultTexturePlatform is the fallback for any target not named below (WebGL, iOS, the
    # editor's own preview). Left on Automatic so an unnamed target still gets something sane,
    # but carrying this group's max size so the resolution decision holds everywhere.
    out.append(PLATFORM_ENTRY.format(
        target="DefaultTexturePlatform", max_size=group.max_size, fmt=-1,
        compression=1, quality=50, crunched=0, overridden=0, force_hq=0))
    for target, fmt in (("Standalone", group.win_format), ("Android", group.android_format)):
        crunched = 1 if fmt == DXT5_CRUNCHED else 0
        quality = group.crunch_quality if crunched else 50
        # forceMaximumCompressionQuality_BC6H_BC7 is BC7's own quality switch and is free at
        # import time in every sense that matters here: it costs encoder seconds, not bytes.
        force_hq = 1 if fmt == BC7 else 0
        out.append(PLATFORM_ENTRY.format(
            target=target, max_size=group.max_size, fmt=fmt,
            compression=1, quality=quality, crunched=crunched, overridden=1, force_hq=force_hq))
    return "".join(out)


def clamp(w, h, max_size):
    """Unity halves a texture until both dimensions fit maxTextureSize."""
    while w > max_size or h > max_size:
        w, h = max(1, w // 2), max(1, h // 2)
    return w, h


def png_size(path):
    with open(path, "rb") as f:
        head = f.read(26)
    if head[:8] != b"\x89PNG\r\n\x1a\n":
        return None
    return struct.unpack(">II", head[16:24])


def rewrite(meta_text, group):
    """Return the .meta with this group's policy applied. The guid is never touched."""
    text = meta_text

    def set_scalar(pattern, value, text):
        return re.sub(pattern, value, text, count=1)

    text = set_scalar(r"(?m)^(    enableMipMap: )\d+$", r"\g<1>%d" % group.mipmap, text)
    text = set_scalar(r"(?m)^(  nPOTScale: )\d+$", r"\g<1>%d" % group.npot, text)
    text = set_scalar(r"(?m)^(    filterMode: )-?\d+$", r"\g<1>%d" % group.filter_mode, text)
    # The legacy top-level maxTextureSize, kept in step with the platform entries so a reader of
    # the file is not told two different numbers.
    text = set_scalar(r"(?m)^(  maxTextureSize: )\d+$", r"\g<1>%d" % group.max_size, text)

    start = text.index("  platformSettings:\n")
    end = text.index("  spriteSheet:\n", start)
    return text[:start] + "  platformSettings:\n" + platform_block(group) + text[end:]


def walk(group, limit=None):
    d = os.path.join(ROOT, group.folder)
    names = sorted(n for n in os.listdir(d)
                   if n.lower().endswith((".png", ".jpg", ".jpeg", ".tga")))
    if limit:
        names = [n for n in names if any(n.startswith(p) for p in limit)]
    for n in names:
        yield os.path.join(d, n)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true", help="write the .meta files")
    ap.add_argument("--only", help="comma-separated filename prefixes, for a pilot run")
    ap.add_argument("--group", help="restrict to one group by folder substring")
    args = ap.parse_args()

    limit = args.only.split(",") if args.only else None
    mb = 1024 * 1024
    total_before = total_after = 0

    for group in GROUPS:
        if args.group and args.group not in group.folder:
            continue
        n = changed = 0
        before = after = 0
        for png in walk(group, limit):
            meta = png + ".meta"
            if not os.path.exists(meta):
                print("  no .meta for %s - skipped" % os.path.basename(png))
                continue
            w, h = png_size(png) or (0, 0)
            if group.multiple_of_4_only and (w % 4 or h % 4):
                print("  %s is %dx%d, not a multiple of 4 - left as it is" % (os.path.basename(png), w, h))
                continue
            n += 1

            old = open(meta, "r", encoding="utf-8").read()
            before += predict(old, w, h)
            new = rewrite(old, group)
            aw, ah = clamp(w, h, group.max_size)
            after += aw * ah * BPP[group.win_format] * (4.0 / 3.0 if group.mipmap else 1.0)
            if new != old:
                changed += 1
                if args.apply:
                    with open(meta, "w", encoding="utf-8", newline="\n") as f:
                        f.write(new)
        total_before += before
        total_after += after
        print("%-34s %3d files  %8.1f -> %8.1f MB (Windows, uncrunched)  %d to write"
              % (group.name, n, before / mb, after / mb, changed))
        print("    max %-5d npot=%d mip=%d  Windows %-14s  Android %s"
              % (group.max_size, group.npot, group.mipmap,
                 FORMAT_NAMES[group.win_format], FORMAT_NAMES[group.android_format]))
    print("%-34s            %8.1f -> %8.1f MB" % ("TOTAL", total_before / mb, total_after / mb))
    if not args.apply:
        print("\n(nothing written - pass --apply)")


def predict(meta_text, w, h):
    """What this .meta costs in the Windows player today, for the before column."""
    mip = int(re.search(r"enableMipMap: (\d+)", meta_text).group(1))
    npot = int(re.search(r"nPOTScale: (\d+)", meta_text).group(1))
    blocks = re.split(r"\n  - serializedVersion: 4\n", meta_text)[1:]
    plats = {}
    for b in blocks:
        m = re.search(r"buildTarget: (\S+)", b)
        if not m:
            continue
        plats[m.group(1)] = {
            "max": int(re.search(r"maxTextureSize: (\d+)", b).group(1)),
            "comp": int(re.search(r"textureCompression: (\d+)", b).group(1)),
            "over": int(re.search(r"overridden: (\d+)", b).group(1)),
        }
    p = plats.get("Standalone")
    if not p or not p["over"]:
        p = plats["DefaultTexturePlatform"]
    if npot == 1:
        def near(v):
            lo = 1 << (v.bit_length() - 1)
            hi = lo << 1
            return lo if (v - lo) < (hi - v) else hi
        w, h = near(w), near(h)
    w, h = clamp(w, h, p["max"])
    b = w * h * (4.0 if p["comp"] == 0 else 1.0)
    if mip:
        b *= 4.0 / 3.0
    return b


if __name__ == "__main__":
    sys.exit(main())
