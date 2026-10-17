"""Levers R and R2 of Docs/ECONOMY-PROPOSAL-R4.md (section 4, "Lever R" and "Lever R2").

  Lever R   recommendedPower for the 66 stages from 1-7 on comes from Tools/economy-r4-candidate-F.txt,
            the per-stage source the proposal names (column "recPwr old->new"). The floor level, the
            ceiling level and the enemy levels follow by the solver's analytic inversion
            (scale = 1 + 0.12 x (level - 1), economy_r4_lib.inversion), anchored on the stage as it
            stood before the proposal.
  Lever R2  the enemy levels of all 66 stages from 1-7 are searched again by the harness (--relevel)
            until the geared CEIL lands at 85 percent for ordinary stages and 65 percent for walls
            (1-12 and stages 6 and 12 from chapter 2).

The recommendedPower column is the OUTPUT of the ratchet described in section 4, "The rule for chapter 2
and later, and why it is a ratchet" (economy_r4_ratchet.py). Re-running that takes hours (16 rounds of
20 walks), so this tool takes its converged result as input. It then proves the table is consistent:
after the level search, every stage's floor and ceiling must equal the table's "floor/ceil old->new"
column, and the tool prints and exits 1 on any stage that does not.

The tool works on a scratch copy of the content and only then patches the real campaign.json, and only
the four fields it owns (recommendedPower, floorLevel, ceilingLevel, the enemy levels).

Anchor: the pre-proposal content. If campaign.json already carries the table's "new" recommendedPower
the tool reports "already applied" and checks floor and ceiling against the table without changing
anything. If it carries neither the old nor the new column the tool stops: pass --anchor DIR with the
pre-proposal content directory.

usage:
  python Tools/economy_r4_levers.py [--content DIR] [--anchor DIR] [--scratch DIR] [--no-relevel]
"""
import sys
import tempfile
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import economy_r4_lib as lib

TABLE = Path(__file__).resolve().parent / "economy-r4-candidate-F.txt"
FIRST_LEVELLED = 7            # global 1-based stage index: 1-7 is the first stage lever R moves
ORDINARY_CEIL = 0.85          # proposal: "geared CEIL lands at 85 percent for ordinary stages"
WALL_CEIL = 0.65              # "and 65 percent for walls"


def arg(argv, name, default=None):
    return Path(argv[argv.index(name) + 1]) if name in argv else default


def main(argv):
    content = arg(argv, "--content", lib.CONTENT)
    anchor_dir = arg(argv, "--anchor", content)
    scratch = arg(argv, "--scratch", Path(tempfile.gettempdir()) / "emberfall-r4-levers")
    table = lib.read_candidate_table(TABLE)
    if len(table) != 72:
        print("ERROR: the candidate table has %d rows, expected 72" % len(table))
        return 1

    now = lib.read_stages(content)
    anchor = lib.read_stages(anchor_dir)
    old_ok = all(table[lib.code_of(s["id"])]["rec_old"] == s["recommendedPower"] for s in anchor)
    new_ok = all(table[lib.code_of(s["id"])]["rec_new"] == s["recommendedPower"] for s in now)
    if not old_ok and not new_ok:
        print("ERROR: %s matches neither the table's old nor its new recommendedPower; pass --anchor with the "
              "pre-proposal content" % content)
        return 1
    if new_ok:
        print("recommendedPower already matches the table; checking floor and ceiling only")
        return verify(now, table)
    if anchor_dir == content:
        print("anchor = the content being patched (matches the table's old column)")

    # Lever R: recommendedPower from the table, levels by inversion. Stages 1-1 to 1-6 are untouched.
    work = Path(scratch)
    lib.copy_content(content, work)
    overrides = {}
    for i, s in enumerate(anchor):
        if i + 1 < FIRST_LEVELLED:
            continue
        code = lib.code_of(s["id"])
        rec = table[code]["rec_new"]
        floor, ceil, levels = lib.inversion(s, rec)
        overrides[s["id"]] = dict(recommendedPower=rec, floorLevel=floor, ceilingLevel=ceil, enemy_levels=levels)
    lib.patch_file(work / "campaign.json", overrides)
    print("lever R: %d stages carry the table's recommendedPower and inverted levels" % len(overrides))

    # Lever R2: the harness searches the enemy levels again, in the scratch copy.
    if "--no-relevel" not in argv:
        spec = "ord=%s,wall=%s,ch1wall=%s,skipto=%d" % (ORDINARY_CEIL, WALL_CEIL, WALL_CEIL, FIRST_LEVELLED)
        started = time.time()
        log = work / "relevel.log"
        rc = lib.run_harness(work, "--relevel", spec, out_path=log)
        print("lever R2: harness --relevel %s exited %d in %d s; log %s" % (spec, rc, time.time() - started, log))
        if rc != 0:
            print("ERROR: the level search failed; campaign.json was not changed")
            return 1

    # Carry the four owned fields from the scratch copy into the real file, in place.
    levelled = lib.read_stages(work)
    final = {}
    for i, s in enumerate(levelled):
        if i + 1 < FIRST_LEVELLED:
            continue
        final[s["id"]] = dict(recommendedPower=s["recommendedPower"], floorLevel=s["floorLevel"],
                              ceilingLevel=s["ceilingLevel"], enemy_levels=[e["level"] for e in s["enemies"]])
    lib.patch_file(Path(content) / "campaign.json", final)
    print("patched " + str(Path(content) / "campaign.json"))
    return verify(lib.read_stages(content), table)


def verify(stages, table):
    bad = []
    for s in stages:
        t = table[lib.code_of(s["id"])]
        got = (s["recommendedPower"], s["floorLevel"], s["ceilingLevel"])
        want = (t["rec_new"], t["floor_new"], t["ceil_new"])
        if got != want:
            bad.append((lib.code_of(s["id"]), got, want))
    print("floor and ceiling against the table: %d of %d stages differ" % (len(bad), len(stages)))
    for code, got, want in bad:
        print("  %s: rec/floor/ceil %s, table %s" % (code, got, want))
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
