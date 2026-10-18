#!/usr/bin/env python
"""Tabulate arm columns of a run over a time range, one row per N frames.

Usage: arm-trace.py <label> --from <s> --to <s> [--step <n>] [--cols a,b,c]

Default columns are the press geometry: the latched plant offset and fold, the
shoulder pivot height, the palm target height, the hand's own height, real
contact, the stroke extension, the shoulder rise inside the stroke and the
pelvis height. Reading them side by side is how "the elbow opened but nothing
rose" is told from "the palm was never under the shoulder".
"""
import csv
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")
DEFAULT = ["armPlantOffsetL", "armPlantFoldL", "armPivotYL", "armPalmYL", "handYL",
           "handContactL", "armStrokeExtL", "armShoulderLiftL", "armPressPhaseL",
           "armPressPhaseR", "pelvisY", "comX", "torsoTilt"]


def main():
    label = sys.argv[1]
    argv = sys.argv[2:]
    t_from = float(argv[argv.index("--from") + 1]) if "--from" in argv else 9.0
    t_to = float(argv[argv.index("--to") + 1]) if "--to" in argv else 16.0
    step = int(argv[argv.index("--step") + 1]) if "--step" in argv else 40
    cols = argv[argv.index("--cols") + 1].split(",") if "--cols" in argv else DEFAULT
    with open(os.path.join(RUNS, label + ".csv"), newline="", encoding="utf-8-sig") as f:
        rows = list(csv.DictReader(f, delimiter=";"))
    sel = [r for r in rows if t_from <= float(r["time"]) <= t_to]
    print("== %s  %.1f..%.1f s, every %d frames" % (label, t_from, t_to, step))
    print("   %-7s %s" % ("t", " ".join("%9s" % c.replace("arm", "").replace("hand", "h")[:9] for c in cols)))
    for i in range(0, len(sel), step):
        r = sel[i]
        print("   %-7.2f %s" % (float(r["time"]),
                                " ".join("%9.3f" % float(r.get(c) or 0) for c in cols)))


main()
