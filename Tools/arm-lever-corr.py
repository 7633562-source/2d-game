"""Levers of the press, read off the arm-lane records that exist (no runs).

Cycle 8 measured corr(plant offset, shoulder lift) = -0.171 over 94 side pairs
and dropped the offset as a lever. The new pair measurement (p_arms70 vs
p_arms71) shows what the press actually does is SHOVE, so this prints the same
correlation against the shove as well: corr(offset, comTravelX).

Usage: python Tools/arm-lever-corr.py
"""
import glob
import json
import math
import os

RUNS = r"E:/Games/2D Game/My project/Trials/Runs"


def pearson(xs, ys):
    n = len(xs)
    if n < 3:
        return float("nan"), n
    mx = sum(xs) / n
    my = sum(ys) / n
    sxy = sum((x - mx) * (y - my) for x, y in zip(xs, ys))
    sxx = sum((x - mx) ** 2 for x in xs)
    syy = sum((y - my) ** 2 for y in ys)
    if sxx <= 0 or syy <= 0:
        return float("nan"), n
    return sxy / math.sqrt(sxx * syy), n


pairs = []
for path in sorted(glob.glob(os.path.join(RUNS, "p_arms*.json"))):
    with open(path, encoding="utf-8") as fh:
        j = json.load(fh)
    if (j.get("armPressStrokesL") or 0) + (j.get("armPressStrokesR") or 0) <= 0:
        continue  # press OFF or never pressed
    travel = j.get("comTravelX")
    if travel is None:
        continue
    for side in ("L", "R"):
        off = j.get("armPressPlantOffset" + side)
        lift = j.get("armPressShoulderLift" + side)
        ext = j.get("armPressStrokeMaxDeg" + side)
        if off is None or lift is None:
            continue
        pairs.append((j.get("label"), side, off, lift, travel, ext))

print("press-on side pairs: %d over %d runs" % (
    len(pairs), len({p[0] for p in pairs})))
if pairs:
    offs = [p[2] for p in pairs]
    print("offset  min %.3f mean %.3f max %.3f m" % (
        min(offs), sum(offs) / len(offs), max(offs)))

for name, idx in (("shoulder lift (cycle 8)", 3), ("run travelX (comTravelX)", 4),
                  ("stroke extension deg", 5)):
    xs = [p[2] for p in pairs if p[idx] is not None]
    ys = [p[idx] for p in pairs if p[idx] is not None]
    r, n = pearson(xs, ys)
    print("  corr(plant offset, %-24s) = %+.3f   n %d" % (name, r, n))

# The same correlation restricted to the three runs that shove hardest.
hard = sorted(pairs, key=lambda p: -p[4])[:6]
print("hardest shoves:")
for p in hard:
    print("   %-10s %s offset %.3f m  lift %.4f m  travelX %.3f m  strokeExt %.1f deg" % p)
