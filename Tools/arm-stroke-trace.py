#!/usr/bin/env python
"""Per-stroke press trace: what each arm press stroke does to the body.

Usage: arm-stroke-trace.py <label> [--from <s>] [--head <n>]

For every press stroke on every side in the fallen window prints one line:

  t0..t1  ext peak  phase mix  shoulder rise  pelvisY start->peak->end  comX delta

and then two aggregates that decide whether the press is a press at all:
the share of the fallen window with BOTH palms loaded (phase 2 or 3 on both
sides), and the share with only one. A press-up lifts the chest with both palms
down; alternating strokes shove the body along the slab instead.
"""
import csv
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")


def num(row, key, default=0.0):
    try:
        return float(row[key])
    except (KeyError, ValueError, TypeError):
        return default


def load(label):
    with open(os.path.join(RUNS, label + ".json"), encoding="utf-8-sig") as f:
        doc = json.load(f)
    with open(os.path.join(RUNS, label + ".csv"), newline="", encoding="utf-8-sig") as f:
        rows = list(csv.DictReader(f, delimiter=";"))
    ft = float(doc.get("fallTime") or 8.0)
    return ft, rows


def main():
    label = sys.argv[1]
    t_from = float(sys.argv[sys.argv.index("--from") + 1]) if "--from" in sys.argv else None
    head = int(sys.argv[sys.argv.index("--head") + 1]) if "--head" in sys.argv else 40
    ft, rows = load(label)
    t0 = t_from if t_from is not None else ft + 0.5
    win = [r for r in rows if num(r, "time") >= t0]
    print("== %s  fall %.2f s, window %.2f..%.2f, %d frames" % (label, ft, t0, num(win[-1], "time"), len(win)))

    both = sum(1 for r in win if num(r, "armPressPhaseL") in (2.0, 3.0) and num(r, "armPressPhaseR") in (2.0, 3.0))
    one = sum(1 for r in win if (num(r, "armPressPhaseL") in (2.0, 3.0)) != (num(r, "armPressPhaseR") in (2.0, 3.0)))
    idle = sum(1 for r in win if num(r, "armPressPhaseL") == 0 and num(r, "armPressPhaseR") == 0)
    n = float(len(win))
    print("   phase mix: both palms %5.1f%%  one palm %5.1f%%  idle %5.1f%%"
          % (100 * both / n, 100 * one / n, 100 * idle / n))

    for side in ("L", "R"):
        cnt = [int(num(r, "armStrokes" + side)) for r in win]
        idx = []
        last = cnt[0]
        start = None
        for i, c in enumerate(cnt):
            if c != last:
                if start is not None:
                    idx.append((start, i - 1))
                start = i
                last = c
        if start is not None:
            idx.append((start, len(win) - 1))
        print("   -- side %s: %d strokes" % (side, len(idx)))
        for k, (a, b) in enumerate(idx[:head]):
            seg = win[a:b + 1]
            t_a, t_b = num(seg[0], "time"), num(seg[-1], "time")
            ext = max(num(r, "armStrokeExt" + side) for r in seg)
            rise = max(num(r, "armShoulderLift" + side) for r in seg)
            py = [num(r, "pelvisY") for r in seg]
            cx = [num(r, "comX") for r in seg]
            print("      %2d  %6.2f..%6.2f s (%4.2f s)  ext %5.1f deg  rise %5.3f m"
                  "  pelvisY %6.3f -> %6.3f -> %6.3f  dComX %+5.2f m"
                  % (k, t_a, t_b, t_b - t_a, ext, rise, py[0], max(py), py[-1], cx[-1] - cx[0]))


main()
