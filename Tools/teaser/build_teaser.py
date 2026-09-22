"""Builds the teaser from the raw Recorder takes. Every decision in it is in Docs/TEASER-CUT.md.

    python Tools/teaser/build_teaser.py            # both cuts
    python Tools/teaser/build_teaser.py --only 16x9
    python Tools/teaser/build_teaser.py --debug    # burns shot names and timecode in

Inputs: Media/captures/*.mp4 (Emberfall.EditorTools.TeaserCapture), Media/title/*.png
(Tools/teaser/title_card.py) and Media/teaser-track.mp3. Outputs: Media/emberfall-teaser-16x9.mp4
and Media/emberfall-teaser-vertical.mp4. Needs ffmpeg on PATH and numpy.

ffmpeg does all of the work - the cuts, the speed ramps, the reframing, the grade, the title
composite and the mux. This script only turns the shot table below into one filter graph.

HOW A SPEED RAMP IS DONE. Every take was shot slower than it plays (the summon at 1/8 and 1/16,
the battle at 1/4), so a take is oversampled footage and any speed from its capture rate up is
frame SELECTION. Each shot gives a speed curve in content-seconds per edit-second, anchored to one
musical event: "this content instant happens at this edit instant". The curve is integrated per
output frame, inverted, and handed to ffmpeg as a setpts expression over source frame numbers;
fps=60 then picks one source frame per output frame. No frame is ever synthesised.

THE BEAT WINS. Every anchor is a transient measured off the track (see the cut list). A shot
whose action and beat disagree has its speed curve changed, never its anchor.
"""
import argparse
import os
import subprocess
import sys

import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MEDIA = os.path.join(ROOT, "Media")
CAP = os.path.join(MEDIA, "captures")
FPS = 60
SRC_W, SRC_H = 2560, 1440

# ---------------------------------------------------------------- the music

TRACK = os.path.join(MEDIA, "teaser-track.mp3")
SONG_IN = 3.10          # the window: 3.10 s to 33.10 s of the track
LENGTH = 30.0


def song(t):
    """Edit time of a moment in the track."""
    return round(t - SONG_IN, 4)


# Transients, in TRACK time, measured by multi-band spectral flux (see TEASER-CUT.md).
TELL = song(17.517)          # first stroke of the drum flam: the light commits to gold
FLAM_SUB = song(17.978)      # the flam's last, sub-heavy stroke
FLASH = song(19.734)         # taiko 1: the flash goes up
REVEAL = song(19.882)        # taiko 2, the heaviest low end in the window: the hero
NAME = song(20.019)          # taiko 3: the name stamps (authored at reveal + 0.14 s; lands on it)
COMBAT = song(24.23)         # pulse: into the fight
ANSWER = song(26.05)         # pulse: cut to her answer
CONTACT = song(28.006)       # the sharpest hit in the window: her bolt lands
TITLE = song(29.841)         # last light hit: the title
AUDIO_FADE = (28.2, LENGTH)  # edit-time audio fade to silence

# ---------------------------------------------------------------- the takes

# SummonTiming.Full, for a 5-star: press 0.22, buildup 1.55 (tell at +0.80), flash 0.06.
B0, TELL_SEQ, FLASH_SEQ, REVEAL_SEQ = 0.22, 1.02, 1.77, 1.83

TAKES = {
    # name: (file, content seconds at source frame 0, content seconds per source frame)
    "slow16": ("summon_slow16.mp4", -0.05, 1 / 960),
    "slow8": ("summon_slow8.mp4", -0.05, 1 / 480),
    "reveal": ("reveal_name.mp4", REVEAL_SEQ - 0.10, 1 / 60),
    # Battle content is GAME seconds; shot at timeScale 0.25, so 1/240 s per frame.
    "wide": ("battle_slow4.mp4", 0.0, 1 / 240),
    "close": ("battle_close_slow4.mp4", 0.0, 1 / 240),
}

# Game time of the two contacts, from each take's own frame-difference peak. The close take
# reads one frame later than the wide one: its recorder started a frame after the fight did.
VAELIS_HIT = 2982 / 240      # wide take: Vaelis's slash lands on Seraphine
SERAPHINE_HIT = 5716 / 240   # close take: her bolt lands on Vaelis


# ---------------------------------------------------------------- the shot table
#
# speed: [(edit_t, content speed)], linear between points, flat past the ends.
# anchor: (edit_t, content_t) - the instant that must land where the music says.
# frame / vframe: [(edit_t, zoom, centre_x, centre_y)] in SOURCE pixels, eased between points.
#   16:9 zoom 1.0 shows the whole 2560x1440 take; 1.333 is 1:1 pixels at 1080p.
#   9:16 zoom 1.0 is a full-height 810x1440 slice of the take.

SHOTS = [
    dict(name="black", start=0.0, end=2.60, black=True),
    dict(name="approach", take="slow16", start=2.60, end=TELL,
         speed=[(2.60, 1 / 16), (10.5, 1 / 16), (TELL, 0.13)], anchor=(TELL, TELL_SEQ - 0.004),
         frame=[(2.60, 1.00, 1280, 720), (TELL, 2.05, 1280, 720)],
         vframe=[(2.60, 1.00, 1280, 720), (TELL, 1.55, 1280, 720)]),
    dict(name="tell", take="slow16", start=TELL, end=FLAM_SUB,
         speed=[(TELL, 0.24), (FLAM_SUB, 0.10)], anchor=(FLAM_SUB, 1.14),
         frame=[(TELL, 1.20, 1280, 720), (FLAM_SUB, 1.24, 1280, 720)],
         vframe=[(TELL, 1.10, 1280, 720), (FLAM_SUB, 1.13, 1280, 720)]),
    dict(name="rings", take="slow8", start=FLAM_SUB, end=FLASH,
         speed=[(FLAM_SUB, 0.12), (FLASH, 0.60)], anchor=(FLAM_SUB, 1.14),
         frame=[(FLAM_SUB, 1.00, 1280, 720), (FLASH, 1.12, 1280, 720)],
         vframe=[(FLAM_SUB, 1.00, 1280, 720), (FLASH, 1.06, 1280, 720)]),
    dict(name="flash", take="slow8", start=FLASH, end=REVEAL,
         speed=[(FLASH, (REVEAL_SEQ - FLASH_SEQ) / (REVEAL - FLASH))], anchor=(FLASH, FLASH_SEQ),
         frame=[(FLASH, 1.12, 1280, 720), (REVEAL, 1.13, 1280, 720)],
         vframe=[(FLASH, 1.06, 1280, 720), (REVEAL, 1.06, 1280, 720)]),
    # The take changes here, under a full-white frame: the flash is the transition.
    dict(name="reveal", take="reveal", start=REVEAL, end=COMBAT,
         speed=[(REVEAL, 1.0)], anchor=(REVEAL, REVEAL_SEQ),
         frame=[(REVEAL, 1.30, 1280, 690), (COMBAT, 1.50, 1280, 675)],
         vframe=[(REVEAL, 1.02, 1280, 700), (COMBAT, 1.14, 1280, 690)]),
    dict(name="strike", take="wide", start=COMBAT, end=ANSWER,
         speed=[(COMBAT, 1.0), (ANSWER - 0.45, 1.0), (ANSWER - 0.15, 0.5)],
         anchor=(ANSWER - 0.15, VAELIS_HIT),
         frame=[(COMBAT, 1.04, 1290, 760), (ANSWER, 1.10, 1290, 780)],
         vframe=[(COMBAT, 1.00, 1500, 720), (ANSWER, 1.02, 1180, 720)]),
    dict(name="answer", take="close", start=ANSWER, end=TITLE,
         speed=[(ANSWER, 1.6), (CONTACT - 0.70, 1.6), (CONTACT - 0.06, 0.25),
                (CONTACT + 0.55, 0.25), (CONTACT + 1.25, 0.6)],
         anchor=(CONTACT, SERAPHINE_HIT),
         frame=[(ANSWER, 1.00, 1300, 740), (CONTACT, 1.08, 1320, 740), (TITLE, 1.38, 1440, 660)],
         vframe=[(ANSWER, 1.00, 880, 720), (CONTACT - 0.35, 1.00, 900, 720),
                 (CONTACT, 1.05, 1430, 700), (TITLE, 1.30, 1720, 600)]),
    dict(name="title", start=TITLE, end=LENGTH, black=True),
]

TITLE_IN = (TITLE, 0.70)          # fade in over black
TITLE_OUT = (28.95, 0.65)         # fade out; black to the end


# ---------------------------------------------------------------- the maths

def edit_frames(shot):
    n = int(round(shot["end"] * FPS)) - int(round(shot["start"] * FPS))
    return shot["start"] + (np.arange(n) + 0.5) / FPS, n


def speed_at(curve, t):
    ts = np.array([p[0] for p in curve])
    vs = np.array([p[1] for p in curve])
    return np.interp(t, ts, vs)


def content_curve(shot):
    """Content time at every edit instant of the shot, from its speed curve and anchor."""
    a_t, a_c = shot["anchor"]
    lo = min(shot["start"], a_t) - 0.05
    hi = max(shot["end"], a_t) + 0.05
    grid = np.arange(lo, hi, 1 / 2400.0)
    v = speed_at(shot["speed"], grid)
    c = np.concatenate([[0.0], np.cumsum((v[1:] + v[:-1]) * 0.5 * np.diff(grid))])
    c += a_c - np.interp(a_t, grid, c)
    return grid, c


def setpts_expr(shot, first_frame):
    """PTS (seconds, from the shot's start) as a function of N, the source frame number counted
    from first_frame. Built as a sum of clipped linear pieces, so it is flat, not nested."""
    c0, dc = TAKES[shot["take"]][1], TAKES[shot["take"]][2]
    grid, c = content_curve(shot)
    # the edit time at which each source frame's content instant falls
    last = int(np.ceil((np.interp(shot["end"], grid, c) - c0) / dc)) + 2
    src = np.arange(first_frame, last + 1)
    src_content = c0 + src * dc
    t_of = np.interp(src_content, c, grid) - shot["start"]
    # Knots every 6 source frames, then collinear runs merged, so a constant-speed stretch is one
    # term however long it is. ffmpeg's expression parser recurses per operator, so the sum is
    # also built as a balanced tree rather than a flat chain.
    step = 6
    knots = list(range(0, len(src), step))
    if knots[-1] != len(src) - 1:
        knots.append(len(src) - 1)
    pieces = []
    for i in range(len(knots) - 1):
        a, b = knots[i], knots[i + 1]
        slope = (t_of[b] - t_of[a]) / (b - a)
        if pieces and abs(pieces[-1][2] - slope) < 1e-7:
            pieces[-1][1] = b
        else:
            pieces.append([a, b, slope])
    terms = ["%.7f*clip(N-%d,0,%d)" % (sl, a, b - a) for a, b, sl in pieces]

    def tree(items):
        if len(items) == 1:
            return items[0]
        mid = len(items) // 2
        return "(" + tree(items[:mid]) + "+" + tree(items[mid:]) + ")"

    return "(%.6f+%s)/TB" % (t_of[0], tree(terms)), last - first_frame + 1


def first_source_frame(shot):
    c0, dc = TAKES[shot["take"]][1], TAKES[shot["take"]][2]
    grid, c = content_curve(shot)
    start_c = np.interp(shot["start"], grid, c)
    return max(0, int(np.floor((start_c - c0) / dc)) - 1)


def eased(keys, idx, local_t, dur):
    """ffmpeg expression for key component idx over a shot, smoothstep between keys."""
    if len(keys) == 1:
        return "%.4f" % keys[0][idx]
    t0 = keys[0][0]
    expr = "%.4f" % keys[-1][idx]
    for i in range(len(keys) - 2, -1, -1):
        a, b = keys[i], keys[i + 1]
        ta, tb = a[0] - t0, b[0] - t0
        k = "clip((t-%.4f)/%.4f,0,1)" % (ta, max(1e-3, tb - ta))
        seg = "(%.4f+(%.4f)*(%s*%s*(3-2*%s)))" % (a[idx], b[idx] - a[idx], k, k, k)
        expr = "if(lt(t,%.4f),%s,%s)" % (tb, seg, expr)
    return expr


def frame_filter(keys, vertical):
    """Animated zoom and centre, as a per-frame scale and a per-frame crop."""
    ow, oh = (1080, 1920) if vertical else (1920, 1080)
    base = oh / SRC_H            # scale at zoom 1.0 (both cuts are height-bound at 1.0)
    z = eased(keys, 1, 0, 0)
    cx = eased(keys, 2, 0, 0)
    cy = eased(keys, 3, 0, 0)
    sw = "(round(%d*%.9f*(%s)/2)*2)" % (SRC_W, base, z)
    sh = "(round(%d*%.9f*(%s)/2)*2)" % (SRC_H, base, z)
    sc = "scale=w='%s':h='%s':eval=frame:flags=lanczos" % (sw, sh)
    # Offsets from the same zoom expression, NOT from iw/ih: crop reads its input size once, when
    # it is configured, so after a per-frame scale iw is still the first frame's width.
    cr = ("crop=%d:%d:x='clip((%s)*%s/%d-%d,0,%s-%d)':y='clip((%s)*%s/%d-%d,0,%s-%d)'"
          % (ow, oh, cx, sw, SRC_W, ow // 2, sw, ow, cy, sh, SRC_H, oh // 2, sh, oh))
    return sc + "," + cr


# ---------------------------------------------------------------- the graph

def build(vertical, debug, out_path):
    ow, oh = (1080, 1920) if vertical else (1920, 1080)
    inputs, chains, labels = [], [], []

    def input_of(name):
        # One input per shot, even when two shots cut from the same take: a filter-graph input
        # pad can only be read once, and a second demuxer is cheaper than a split that has to
        # buffer every frame between the two shots' in-points.
        inputs.append(["-i", os.path.join(CAP, TAKES[name][0])])
        return len(inputs) - 1

    for shot in SHOTS:
        _, n = edit_frames(shot)
        lab = "s_" + shot["name"]
        if shot.get("black"):
            chains.append("color=c=black:s=%dx%d:r=%d:d=%.4f,format=yuv420p,trim=end_frame=%d,setpts=N/%d/TB[%s]"
                          % (ow, oh, FPS, n / FPS + 0.1, n, FPS, lab))
        else:
            idx = input_of(shot["take"])
            f0 = first_source_frame(shot)
            expr, count = setpts_expr(shot, f0)
            keys = shot["vframe"] if vertical else shot["frame"]
            chains.append(
                "[%d:v]trim=start_frame=%d:end_frame=%d,setpts='%s',fps=%d,"
                "trim=end_frame=%d,setpts=N/%d/TB,%s,format=yuv420p,setsar=1[%s]"
                % (idx, f0, f0 + count, expr, FPS, n, FPS, frame_filter(keys, vertical), lab))
        labels.append(lab)

    chains.append("".join("[%s]" % l for l in labels) + "concat=n=%d:v=1:a=0[cut]" % len(labels))

    # The grade: a touch of contrast and warmth, a soft vignette, and fine grain so the long glow
    # gradients do not band in H.264. Blacks stay black - the opening depends on it.
    grade = ("eq=contrast=1.06:saturation=1.07:gamma=0.98,"
             "colorbalance=rs=0.02:bs=-0.03:rm=0.015:bm=-0.02,"
             "vignette=angle=PI/5:mode=forward,"
             "noise=alls=3:allf=t")
    chains.append("[cut]%s[graded]" % grade)

    title_png = os.path.join(MEDIA, "title", "title_vertical.png" if vertical else "title_16x9.png")
    tidx = len(inputs)
    inputs.append(["-loop", "1", "-framerate", str(FPS), "-t", "%.3f" % (LENGTH + 0.5), "-i", title_png])
    chains.append("[%d:v]format=rgba,fade=t=in:st=%.3f:d=%.3f:alpha=1,fade=t=out:st=%.3f:d=%.3f:alpha=1[title]"
                  % (tidx, TITLE_IN[0], TITLE_IN[1], TITLE_OUT[0], TITLE_OUT[1]))
    chains.append("[graded][title]overlay=0:0:shortest=1:format=auto[v0]")

    last = "v0"
    if debug:
        font = os.path.join(ROOT, "Assets", "Resources", "Fonts", "BarlowCondensed-SemiBold.ttf")
        font = font.replace("\\", "/").replace(":", "\\:")
        marks = []
        for shot in SHOTS:
            marks.append("drawtext=fontfile='%s':text='%s':x=20:y=20:fontsize=32:fontcolor=yellow:"
                         "enable='between(t,%.3f,%.3f)'" % (font, shot["name"], shot["start"], shot["end"]))
        marks.append("drawtext=fontfile='%s':text='%%{pts\\:hms}':x=20:y=60:fontsize=28:fontcolor=yellow" % font)
        chains.append("[v0]%s[v1]" % ",".join(marks))
        last = "v1"

    aidx = len(inputs)
    inputs.append(["-i", TRACK])
    chains.append("[%d:a]atrim=start=%.3f:end=%.3f,asetpts=PTS-STARTPTS,afade=t=in:st=0:d=0.12,"
                  "afade=t=out:st=%.3f:d=%.3f,aresample=48000[a]"
                  % (aidx, SONG_IN, SONG_IN + LENGTH, AUDIO_FADE[0], AUDIO_FADE[1] - AUDIO_FADE[0]))

    graph = ";\n".join(chains)
    gpath = out_path + ".filtergraph.txt"
    with open(gpath, "w", encoding="utf-8") as f:
        f.write(graph)

    cmd = ["ffmpeg", "-hide_banner", "-loglevel", "warning", "-stats", "-y"]
    for i in inputs:
        cmd += i
    cmd += ["-/filter_complex", gpath, "-map", "[%s]" % last, "-map", "[a]",
            "-c:v", "libx264", "-preset", "slow", "-profile:v", "high", "-pix_fmt", "yuv420p",
            "-crf", "12", "-maxrate", "45M", "-bufsize", "90M", "-g", "60", "-r", str(FPS),
            "-c:a", "aac", "-b:a", "320k", "-ar", "48000",
            "-movflags", "+faststart", "-t", "%.3f" % LENGTH, out_path]
    print(" ".join(cmd[:8]), "...", out_path)
    subprocess.run(cmd, check=True)
    os.remove(gpath)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", choices=["16x9", "vertical"])
    ap.add_argument("--debug", action="store_true")
    a = ap.parse_args()
    suffix = "-debug" if a.debug else ""
    if a.only in (None, "16x9"):
        build(False, a.debug, os.path.join(MEDIA, "emberfall-teaser-16x9%s.mp4" % suffix))
    if a.only in (None, "vertical"):
        build(True, a.debug, os.path.join(MEDIA, "emberfall-teaser-vertical%s.mp4" % suffix))


if __name__ == "__main__":
    main()
