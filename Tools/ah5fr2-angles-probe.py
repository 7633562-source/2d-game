#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5f-R2 - the walk's own angle ranges + the four named peaks, from the row of record.

Usage: python Tools/ah5fr2-angles-probe.py Trials/Runs/c65post_walk.csv [--peaks 15.43,28.38,30.00,33.22]
"""
import argparse
import csv

ap = argparse.ArgumentParser()
ap.add_argument("csv")
ap.add_argument("--peaks", default="15.43,28.38,30.00,33.22")
ap.add_argument("--shots", type=float, default=25.0)
args = ap.parse_args()

with open(args.csv, newline="") as fh:
    rd = csv.reader(fh, delimiter=";")
    head = next(rd)
    idx = {n: i for i, n in enumerate(head)}
    rows = [r for r in rd if len(r) > idx.get("kneeR", 0)]

print("cols", len(head), "rows", len(rows))
t_i = idx["time"]
walk = [r for r in rows if 5.0 <= float(r[t_i]) <= 40.0]
print("walk rows %d  t %.2f..%.2f" % (len(walk), float(walk[0][t_i]), float(walk[-1][t_i])))
for name in ("kneeL", "kneeR", "hipL", "hipR", "ankleL", "ankleR",
             "shoulderAngleL", "shoulderAngleR", "elbowAngleL", "elbowAngleR"):
    if name not in idx:
        continue
    v = [float(r[idx[name]]) for r in walk]
    print("%-14s %+7.1f .. %+7.1f deg" % (name, min(v), max(v)))

peaks = [float(x) for x in args.peaks.split(",")]
print("--- the named peaks ---")
for t in peaks:
    r = min(rows, key=lambda rr: abs(float(rr[t_i]) - t))
    f = int(round(float(r[t_i]) * args.shots))
    print("t %6.2f f%-4d kneeR %+7.2f kneeL %+7.2f hipR %+7.2f elbowR %+7.2f shoulderR %+7.2f"
          % (float(r[t_i]), f, float(r[idx["kneeR"]]), float(r[idx["kneeL"]]),
             float(r[idx["hipR"]]), float(r[idx["elbowAngleR"]]),
             float(r[idx["shoulderAngleR"]])))

print("--- top kneeR rows in the walk ---")
top = sorted(walk, key=lambda r: -float(r[idx["kneeR"]]))[:6]
for r in top:
    print("t %6.2f f%-4d kneeR %+7.2f hipR %+7.2f elbowR %+7.2f"
          % (float(r[t_i]), int(round(float(r[t_i]) * args.shots)),
             float(r[idx["kneeR"]]), float(r[idx["hipR"]]), float(r[idx["elbowAngleR"]])))
