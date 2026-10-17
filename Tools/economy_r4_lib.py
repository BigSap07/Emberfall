"""Shared helpers for the economy proposal tools (Docs/ECONOMY-PROPOSAL-R4.md).

Implements no proposal section on its own; the three tools import it:
  economy_r4_rewards.py   section 4, lever E
  economy_r4_levers.py    section 4, levers R and R2
  economy_r4_ratchet.py   section 4, "The rule for chapter 2 and later, and why it is a ratchet"

NEVER call main() of _author_campaign_stages.py to apply any of this: it rewrites recommendedPower and
every enemy level from seeds and throws the solve away. These helpers patch named fields in place and
leave every comment and every other byte of campaign.json alone, line endings included.
"""
import json
import re
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CONTENT = ROOT / "Assets" / "Resources" / "Content"
HARNESS = ROOT / "Tools" / "DevHarness" / "bin" / "DevHarness.exe"

# Scalar fields a patch may name. Everything else on a stage line is left byte for byte.
SCALAR = ["gold", "shards", "accountXp", "firstClearGold", "firstClearShards", "recommendedPower",
          "idleGoldPerHour", "idleShardsPerHour", "floorLevel", "ceilingLevel"]

# The solver's analytic inversion of power to level (ProposeRecommendedPower): scale = 1 + G * (level - 1).
G = 0.12


def scale(level):
    return 1 + G * (level - 1)


def level_for(power_scale):
    return 1 + (power_scale - 1) / G


def code_of(stage_id):
    """ch2_7 -> 2-7"""
    return stage_id.replace("ch", "").replace("_", "-")


def id_of(code):
    """2-7 -> ch2_7"""
    return "ch%s_%s" % tuple(code.split("-"))


def read_stages(content_dir):
    """The 72 stage objects, in campaign order. campaign.json carries // comments, so strip them."""
    text = Path(content_dir, "campaign.json").read_text(encoding="utf-8")
    text = re.sub(r"^\s*//.*$", "", text, flags=re.M)
    data = json.loads(text)
    return [s for ch in data["chapters"] for s in ch["stages"]]


def patch_text(text, overrides):
    """In-place field patch. overrides: {stage_id: {field: value, 'enemy_levels': [..]}}.
    Only the named fields change. 'enemy_levels' rewrites the "level" of each enemy on the stage's
    single enemies line, in order."""
    out = []
    cur = None
    for line in text.split("\n"):
        m = re.search(r'"id": "(ch\d+_\d+)", "index"', line)
        if m:
            cur = m.group(1)
        if cur and cur in overrides:
            ov = overrides[cur]
            for f in SCALAR:
                if f in ov:
                    line = re.sub(r'("%s": )-?\d+' % f, lambda mm, f=f: mm.group(1) + str(int(ov[f])), line)
            if "enemy_levels" in ov and '"enemies": [' in line:
                it = iter(list(ov["enemy_levels"]))
                line = re.sub(r'("level": )\d+', lambda mm: mm.group(1) + str(next(it)), line)
        out.append(line)
    return "\n".join(out)


def patch_file(path, overrides):
    """patch_text on a file, keeping its line endings and writing UTF-8 without a BOM."""
    p = Path(path)
    raw = p.read_bytes().decode("utf-8")
    crlf = "\r\n" in raw
    text = patch_text(raw.replace("\r\n", "\n"), overrides)
    if crlf:
        text = text.replace("\n", "\r\n")
    p.write_bytes(text.encode("utf-8"))


def copy_content(src_dir, dst_dir):
    """A scratch copy of the content jsons, so the harness can write to it. Overwrites files in
    dst_dir that this call created; never deletes anything."""
    dst = Path(dst_dir)
    dst.mkdir(parents=True, exist_ok=True)
    for f in Path(src_dir).glob("*.json"):
        shutil.copy(f, dst / f.name)
    return dst


def inversion(base, new_rec):
    """Lever R: floor, ceiling and enemy levels for a new recommendedPower, anchored on the stage as
    it stood BEFORE the proposal (base is that stage object). No battle is fought. The enemy levels
    all shift by the same whole number of levels; lever R2 then searches them to the CEIL band."""
    lv = [e["level"] for e in base["enemies"]]
    mean = sum(lv) / len(lv)
    low = min(lv)
    ratio = new_rec / float(base["recommendedPower"])
    mean_w = level_for(ratio * scale(mean))
    ceil_mean_base = mean + (base["ceilingLevel"] - base["floorLevel"])
    ceil_mean_w = level_for(ratio * scale(ceil_mean_base))
    shift = int(round(mean_w - mean))
    if low + shift < 1:
        shift = 1 - low
    new_floor = base["floorLevel"] + shift
    new_ceil = int(round(new_floor + (ceil_mean_w - (mean + shift))))
    if new_ceil < new_floor:
        new_ceil = new_floor
    return new_floor, new_ceil, [l + shift for l in lv]


def run_harness(content_dir, *args, out_path=None):
    """Run DevHarness.exe against a content directory. Returns the exit code. Output goes to out_path
    when given, else is discarded. Build the harness first (Tools/build-check.ps1)."""
    cmd = [str(HARNESS), str(content_dir).replace("\\", "/")] + list(args)
    if out_path:
        with open(out_path, "w", encoding="utf-8", errors="replace") as f:
            return subprocess.run(cmd, stdout=f, stderr=subprocess.STDOUT, cwd=str(ROOT)).returncode
    return subprocess.run(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.STDOUT, cwd=str(ROOT)).returncode


def parse_walk_study(path):
    """Read a --wall-study output: {mode: {code: {rec, p10, med, p90, ratio, extra, wall, stuck}}}.
    med is the median entry power of the walks that reached the stage."""
    d = {}
    mode = None
    row = re.compile(r"\s+(\d-\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+x([\d.]+)\s+(\d+)/(\d+)/(\d+)(.*)")
    for line in open(path, encoding="utf-8", errors="replace"):
        m = re.match(r"WALL STUDY - (.*?), (\d+) walks", line)
        if m:
            mode = m.group(1)
            d[mode] = {}
            continue
        m = row.match(line)
        if m and mode:
            tail = m.group(10)
            s = re.search(r"STUCK (\d+)", tail)
            d[mode][m.group(1)] = dict(
                rec=int(m.group(2)), p10=int(m.group(3)), med=int(m.group(4)), p90=int(m.group(5)),
                ratio=float(m.group(6)), extra=(int(m.group(7)), int(m.group(8)), int(m.group(9))),
                wall="WALL" in tail, stuck=int(s.group(1)) if s else 0)
    return d


# The candidate table the proposal names as the per-stage source (section 3 header and section 6).
TABLE_ROW = re.compile(
    r"^(\d-\d+)\s+(\d+) -> (\d+)\s+(\d+)/(\d+) -> (\d+)/(\d+)\s+(\d+) -> (\d+)\s+(\d+) -> (\d+)\s+x([\d.]+)")


def read_candidate_table(path):
    """{code: dict(rec_old, rec_new, floor_old, ceil_old, floor_new, ceil_new, fc_old, fc_new,
    idle_old, idle_new, ratio)} from Tools/economy-r4-candidate-F.txt."""
    rows = {}
    for line in Path(path).read_text(encoding="utf-8").splitlines():
        m = TABLE_ROW.match(line)
        if not m:
            continue
        v = [int(x) for x in m.groups()[1:11]]
        rows[m.group(1)] = dict(rec_old=v[0], rec_new=v[1], floor_old=v[2], ceil_old=v[3], floor_new=v[4],
                                ceil_new=v[5], fc_old=v[6], fc_new=v[7], idle_old=v[8], idle_new=v[9],
                                ratio=float(m.group(12)))
    return rows
