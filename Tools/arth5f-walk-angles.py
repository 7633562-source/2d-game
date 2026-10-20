#!/usr/bin/env python3
"""ART-H5f - the walk's own joint angles out of an accepted CSV, and the peak-lift rows.

Usage: python Tools/arth5f-walk-angles.py Trials/Runs/c65post_walk.csv [--shots 25]
Prints the ranges of hipL/R, kneeL/R, shoulderAngleL/R, elbowAngleL/R while the walk runs
(after the walk's onset), the top rows by knee flexion per leg (the highest knee-lift
candidates), and the shot indices those rows fall on at the given capture rate.
"""
import argparse
import csv
import sys

ap = argparse.ArgumentParser()
ap.add_argument("csv")
ap.add_argument("--shots", type=float, default=25.0)
ap.add_argument("--top", type=int, default=6)
args = ap.parse_args()

with open(args.csv, newline="") as fh:
    rd = csv.reader(fh, delimiter=";")
    head = next(rd)
    idx = {name: i for i, name in enumerate(head)}
    rows = []
    for r in rd:
        try:
            rows.append(r)
        except Exception:
            pass

cols = ["time", "hipL", "hipR", "kneeL", "kneeR", "ankleL", "ankleR",
        "shoulderAngleL", "shoulderAngleR", "elbowAngleL", "elbowAngleR", "walkPosed",
        "walkCollapsed", "comX"]
print("rows", len(rows), "cols", len(head))
def val(r, c):
    try:
        return float(r[idx[c]])
    except Exception:
        return float("nan")

# the walk section: rows where walkPosed > 0 (the posed walker drives)
walk_rows = [r for r in rows if val(r, "walkPosed") > 0.5]
print("walk rows (walkPosed>0.5):", len(walk_rows),
      "t %.3f..%.3f" % (val(walk_rows[0], "time"), val(walk_rows[-1], "time")) if walk_rows else "")

for c in ["hipL", "hipR", "kneeL", "kneeR", "shoulderAngleL", "shoulderAngleR",
          "elbowAngleL", "elbowAngleR"]:
    vs = sorted(val(r, c) for r in walk_rows)
    if not vs:
        continue
    n = len(vs)
    print("%-15s min %+8.2f  p5 %+8.2f  p50 %+8.2f  p95 %+8.2f  max %+8.2f"
          % (c, vs[0], vs[int(0.05 * n)], vs[n // 2], vs[int(0.95 * n)], vs[-1]))

for leg in ("L", "R"):
    key = "knee" + leg
    best = sorted(walk_rows, key=lambda r: -val(r, key))[: args.top]
    print("--- top knee flexion, leg %s ---" % leg)
    for r in best:
        t = val(r, "time")
        print("  t %7.3f  knee %+7.2f hip %+7.2f ankle %+7.2f  shoulder %+7.2f elbow %+7.2f  shot %4d"
              % (t, val(r, "knee" + leg), val(r, "hip" + leg), val(r, "ankle" + leg),
                 val(r, "shoulderAngle" + leg), val(r, "elbowAngle" + leg),
                 round(t * args.shots)))
