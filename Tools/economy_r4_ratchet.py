"""The ratchet of Docs/ECONOMY-PROPOSAL-R4.md, section 4, "The rule for chapter 2 and later, and why it is
a ratchet". One round per call.

NOT RE-RUN IN THE R6 APPLY. A converged run took 16 rounds of 20 walks of the reference player and hours of
wall time. economy_r4_levers.py takes the converged recommendedPower column of
Tools/economy-r4-candidate-F.txt as its input instead. This file is kept so the derivation of that column
is repeatable, and so a future retune of chapter 2 and later has the rule in code. It has not been run
since it was moved here from the R4 simulation; expect to fix paths.

The rule (proposal section 4): after each reading of 20 walks, every stage a walk stuck on has its target
ratio T raised by x1.04 (x1.08 if 3 or more walks stuck), and by x1.05 and x1.10 from round 6. This code
applies exactly that schedule (RAISE_EARLY, RAISE_LATE). The R4 simulation's script applied x1.05 and x1.10
in every round, so a rerun from round 1 is not bit for bit the simulation's path. Its recommendedPower is derived again as round(median reference entry power / T), its
levels are inverted (economy_r4_lib.inversion) and searched again to CEIL 85 percent (ordinary) and
65 percent (walls) through the harness's --relevel.

The state file holds {"T": {stage_id: ratio}, "round": n}. T starts as median entry power over the
recommendedPower the lever R pass left.

usage:
  python Tools/economy_r4_ratchet.py init WORKDIR WALK_STUDY_OUT   # WORKDIR: a content copy with lever E and R applied
  python Tools/economy_r4_ratchet.py step WORKDIR [ANCHOR_DIR]   # one round: 20 walks, raise stuck stages, relevel; ANCHOR_DIR is the pre-proposal content
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import economy_r4_lib as lib

ORDINARY_CEIL = 0.85
WALL_CEIL = 0.65
WALKS = 20
RAISE_EARLY = (1.04, 1.08)   # rounds 1 to 5: fewer than 3 walks stuck, 3 or more
RAISE_LATE = (1.05, 1.10)    # round 6 on


def state_path(work):
    return Path(work) / "ratchet.state.json"


def init(work, walk_out):
    """T per stage = reference median entry power / the recommendedPower the work copy carries.
    Stages 1-1 to 1-6 are not ratcheted."""
    med = lib.parse_walk_study(walk_out)["no fusion"]
    targets = {}
    for s in lib.read_stages(work):
        code = lib.code_of(s["id"])
        chapter, index = (int(x) for x in code.split("-"))
        if chapter == 1 and index <= 6:
            continue
        if code in med and med[code]["med"] > 0:
            targets[s["id"]] = med[code]["med"] / float(s["recommendedPower"])
    json.dump(dict(T=targets, round=0), open(state_path(work), "w"))
    print("init: %d stages" % len(targets))


def step(work, anchor_dir=None):
    st = json.load(open(state_path(work)))
    targets = st["T"]
    out = Path(work) / "ratchet.walk.out"
    lib.run_harness(work, "--wall-study", str(WALKS), "--wall-modes", "none", out_path=out)
    study = lib.parse_walk_study(out)["no fusion"]
    anchor = {s["id"]: s for s in lib.read_stages(anchor_dir or lib.CONTENT)}
    stuck = {}
    for code, row in study.items():
        sid = lib.id_of(code)
        if row["stuck"] and sid in targets:
            stuck[sid] = row["stuck"]
    st["round"] += 1
    print("round %d, stuck: %s, walks stuck: %d of %d" % (
        st["round"], {lib.code_of(k): v for k, v in stuck.items()}, sum(stuck.values()), WALKS))
    if not stuck:
        json.dump(st, open(state_path(work), "w"))
        print("CONVERGED")
        return 0
    overrides = {}
    changed = []
    for sid, walks_stuck in stuck.items():
        schedule = RAISE_LATE if st["round"] >= 6 else RAISE_EARLY
        targets[sid] *= schedule[1] if walks_stuck >= 3 else schedule[0]
        rec = int(round(study[lib.code_of(sid)]["med"] / targets[sid]))
        floor, ceil, levels = lib.inversion(anchor[sid], rec)
        overrides[sid] = dict(recommendedPower=rec, floorLevel=floor, ceilingLevel=ceil, enemy_levels=levels)
        changed.append(lib.code_of(sid))
    lib.patch_file(Path(work) / "campaign.json", overrides)
    spec = "ord=%s,wall=%s,ch1wall=%s,skipto=7,only=%s" % (ORDINARY_CEIL, WALL_CEIL, WALL_CEIL, ";".join(changed))
    lib.run_harness(work, "--relevel", spec)
    json.dump(st, open(state_path(work), "w"))
    print("raised: %s" % {c: round(targets[lib.id_of(c)], 3) for c in changed})
    return 0


if __name__ == "__main__":
    if len(sys.argv) >= 4 and sys.argv[1] == "init":
        init(sys.argv[2], sys.argv[3])
    elif len(sys.argv) >= 3 and sys.argv[1] == "step":
        sys.exit(step(sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else None))
    else:
        print(__doc__)
        sys.exit(2)
