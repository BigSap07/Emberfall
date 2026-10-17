"""Lever E of Docs/ECONOMY-PROPOSAL-R4.md (section 4, "Lever E"): the rewards-only mode.

Pulls first-clear payouts down from 1-7 through 2-12 and raises idle gold per hour toward 1.00 purses an
hour by 3-1. The purse (gold, shards, accountXp) and idleShardsPerHour are written back as the generator
authors them, so they do not move. Nothing else in campaign.json is touched: this is a field patch, not
a generation. Do NOT run _author_campaign_stages.py main() instead; it rewrites recommendedPower and
every enemy level from seeds.

The law, with the proposal line each constant comes from:

  FIRST_CLEAR  (0.50, 6, 14, 24)  "x0.50 from 1-12 to 2-3, then rises (2-4 is x0.55) to x1.0 at 3-1".
               Global 0-based stage index: 6 is 1-7, 14 is 2-3, 24 is 3-1. Multiplier 0.50 from index 6
               to 14, then linear back to 1.00 at index 24, 1.00 from there on.
  RISING_STEP  1.02               "kept rising by about 2 percent per ordinary stage (at least 1.6
               percent, from the generator's rounding), so it cannot drop at once after 610 at 1-6".
               That is what makes 1-7 x0.87, 1-8 x0.76, 1-9 x0.67, 1-10 x0.60 and 1-11 x0.54 instead of
               x0.50: each ordinary first clear must clear the one before by this step. Finales keep
               leading their neighbours and the next opener (whole payout); see FINALE_LEAD.
  IDLE         (1.25, 14, 24)     "Idle income rises from 0.80 purses an hour to 1.00 by 3-1 and holds".
               1.00 / 0.80 = 1.25. Multiplier 1.00 up to index 14, linear to 1.25 at index 24, 1.25 on.

usage:
  python Tools/economy_r4_rewards.py            # patch Assets/Resources/Content/campaign.json in place
  python Tools/economy_r4_rewards.py --check    # change nothing, print what would move, exit 1 on a reward-law error
  python Tools/economy_r4_rewards.py --content DIR   # patch DIR/campaign.json instead
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import _author_campaign_stages as GEN   # import only; main() is guarded and is never called
import economy_r4_lib as lib

FIRST_CLEAR = (0.50, 6, 14, 24)
RISING_STEP = 1.02
IDLE = (1.25, 14, 24)
# Not in the proposal: a simulation choice carried over from r4law.py. It is inert on the shipped law (the
# output is identical at 1.0, 1.01 and 1.05), because the finales already lead their neighbours.
FINALE_LEAD = 1.10
STAGES = 72


def lerp(a, b, t):
    return a + (b - a) * t


def first_clear_multiplier(i):
    lo, start, hold_end, back = FIRST_CLEAR
    if i < start:
        return 1.0
    if i <= hold_end:
        return lo
    if i < back:
        return lerp(lo, 1.0, (i - hold_end) / float(back - hold_end))
    return 1.0


def idle_multiplier(i):
    hi, ramp_start, ramp_end = IDLE
    if i <= ramp_start:
        return 1.0
    if i < ramp_end:
        return lerp(1.0, hi, (i - ramp_start) / float(ramp_end - ramp_start))
    return hi


def is_finale(i):
    return i % 12 == 11


def reward_rows():
    """The seven reward fields for all 72 stages, under the proposal's law."""
    rows = []
    for i in range(STAGES):
        gold, shards, xp, fcg, fcs, idle_g, idle_s = GEN.rewards_at(i, is_finale(i))
        fm = first_clear_multiplier(i)
        rows.append(dict(gold=gold, shards=shards, accountXp=xp,
                         firstClearGold=GEN._round_nice(fcg * fm),
                         firstClearShards=max(1, GEN._round_nice(fcs * fm)),
                         idleGoldPerHour=GEN._round_nice(idle_g * idle_multiplier(i)),
                         idleShardsPerHour=idle_s))
    return rows


def keep_rising(rows):
    """After the multiplier pulled first clears down, keep every ordinary first-clear channel strictly
    rising (at least RISING_STEP above the last ordinary stage), and every finale leading both
    neighbours and the next opener's whole payout."""
    finales = set(a for a in range(STAGES) if is_finale(a))
    for k in ("firstClearGold", "firstClearShards"):
        prev = None
        for a in range(STAGES):
            if a in finales:
                continue
            if prev is not None:
                need = GEN._round_nice(rows[prev][k] * RISING_STEP)
                if need <= rows[prev][k]:
                    need = rows[prev][k] + GEN._grain_of(rows[prev][k])
                if rows[a][k] < need:
                    rows[a][k] = need
            prev = a
        for a in sorted(finales):
            neighbours = [rows[b][k] for b in (a - 1, a + 1) if 0 <= b < STAGES]
            need = GEN._round_nice(max(neighbours) * FINALE_LEAD)
            if rows[a][k] < need:
                rows[a][k] = need
    for a in sorted(finales):
        if a + 1 < STAGES:
            while rows[a]["gold"] + rows[a]["firstClearGold"] <= rows[a + 1]["gold"] + rows[a + 1]["firstClearGold"]:
                rows[a]["firstClearGold"] += GEN._grain_of(rows[a]["firstClearGold"])
    return rows


def validate(rows):
    """The generator's own reward rules: every always-earned channel rises at every step, ordinary
    first clears rise, finales lead their neighbours, and a finale's whole payout leads the opener."""
    errs = []
    for k in ("gold", "shards", "accountXp", "idleGoldPerHour", "idleShardsPerHour"):
        for a in range(1, STAGES):
            if rows[a][k] <= rows[a - 1][k]:
                errs.append("%s not rising at %d" % (k, a))
    finales = [a for a in range(STAGES) if is_finale(a)]
    ordinary = [a for a in range(STAGES) if a not in finales]
    for k in ("firstClearGold", "firstClearShards"):
        for b in range(1, len(ordinary)):
            if rows[ordinary[b]][k] <= rows[ordinary[b - 1]][k]:
                errs.append("%s ordinary not rising at %d" % (k, ordinary[b]))
        for a in finales:
            for b in (a - 1, a + 1):
                if 0 <= b < STAGES and rows[a][k] <= rows[b][k]:
                    errs.append("%s finale %d not leading %d" % (k, a, b))
    for a in finales:
        if a + 1 < STAGES and rows[a]["gold"] + rows[a]["firstClearGold"] <= rows[a + 1]["gold"] + rows[a + 1]["firstClearGold"]:
            errs.append("finale %d whole payout not leading opener" % a)
    return errs


def main(argv):
    content = Path(argv[argv.index("--content") + 1]) if "--content" in argv else lib.CONTENT
    check_only = "--check" in argv
    rows = keep_rising(reward_rows())
    errs = validate(rows)
    if errs:
        print("REWARD LAW ERRORS:")
        for e in errs:
            print("  " + e)
        return 1
    stages = lib.read_stages(content)
    overrides = {}
    moved = {}
    for i, s in enumerate(stages):
        ov = {}
        for k, v in rows[i].items():
            if s[k] != v:
                ov[k] = v
                moved.setdefault(k, []).append(lib.code_of(s["id"]))
        if ov:
            overrides[s["id"]] = ov
    print("%d stages change; fields moved: %s" % (len(overrides),
          ", ".join("%s x%d" % (k, len(v)) for k, v in sorted(moved.items())) or "none"))
    for k in ("gold", "shards", "accountXp", "idleShardsPerHour"):
        if k in moved:
            print("ERROR: %s moved on %s; lever E leaves the purse, shards, XP and idle shards alone" % (k, moved[k][:5]))
            return 1
    if check_only:
        return 0
    lib.patch_file(content / "campaign.json", overrides)
    print("patched " + str(content / "campaign.json"))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
