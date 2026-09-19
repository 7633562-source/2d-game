#!/usr/bin/env python3
"""Does the plant OFFSET predict the shoulder RISE, across the arm records?

Read-only. For every `Trials/Runs/p_arms*.json` that carries the press columns,
takes the two recorded plant offsets and the two recorded shoulder lifts and
prints the Pearson correlation, plus the two groups split at 0.05 m so the
claim is readable without the coefficient.

Usage: python Tools/arm-offset-lift-corr.py [prefix]
"""

import json
import os
import sys

RUNS = "Trials/Runs"


def pearson(xs, ys):
    n = len(xs)
    if n < 3:
        return None
    mx = sum(xs) / n
    my = sum(ys) / n
    sx = sum((x - mx) ** 2 for x in xs) ** 0.5
    sy = sum((y - my) ** 2 for y in ys) ** 0.5
    if sx == 0 or sy == 0:
        return None
    return sum((x - mx) * (y - my) for x, y in zip(xs, ys)) / (sx * sy)


def main():
    prefix = sys.argv[1] if len(sys.argv) > 1 else "p_arms"
    offs, lifts, rows = [], [], []
    for name in sorted(os.listdir(RUNS)):
        if not name.endswith(".json") or not name.startswith(prefix):
            continue
        d = json.load(open(os.path.join(RUNS, name), encoding="utf-8"))
        if not isinstance(d, dict) or "armPressPlantOffsetL" not in d:
            continue
        lab = name[:-5]
        for side in ("L", "R"):
            off = d.get("armPressPlantOffset" + side)
            lift = d.get("armPressShoulderLift" + side)
            if isinstance(off, (int, float)) and isinstance(lift, (int, float)):
                offs.append(float(off))
                lifts.append(float(lift))
                rows.append((lab, side, float(off), float(lift)))
    r = pearson(offs, lifts)
    print("records with both columns: %d pairs (both sides of every run)" % len(rows))
    print("Pearson corr(plant offset, shoulder lift) = %s"
          % ("%.3f" % r if r is not None else "n/a"))
    low = [(o, l) for _, _, o, l in rows if o <= 0.05]
    high = [(o, l) for _, _, o, l in rows if o > 0.05]
    for name, grp in (("offset <= 0.05 m", low), ("offset >  0.05 m", high)):
        if not grp:
            continue
        print("  %-16s n=%-4d mean offset %.3f  mean lift %.3f m  max lift %.3f m"
              % (name, len(grp),
                 sum(o for o, _ in grp) / len(grp),
                 sum(l for _, l in grp) / len(grp),
                 max(l for _, l in grp)))
    print("  the five largest lifts:")
    for lab, side, off, lift in sorted(rows, key=lambda r: -r[3])[:5]:
        print("    %-12s %s offset %.3f m -> lift %.3f m" % (lab, side, off, lift))


if __name__ == "__main__":
    main()
