#!/usr/bin/env python
"""ARM cycle 14, read-only. Per-frame table of the fall press, so that the
quantity that LEADS a rise or a sink can be told from the one that follows it.

Usage: python Tools/arm-cycle14-trace.py <label> --from 29.0 --to 34.0 [--step 0.1]

Columns: t, phaseL/R, elbowL/R, shoulderL/R, pivot Y (shoulder joint), palm Y
(the latched target), hand Y (the live palm centre), contact L/R, pelvisY, comX,
fzL/fzR. No physics, no build, no run - it reads a stored record.
"""
import csv
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")

COLS = ["time",
        "armPressPhaseL", "armPressPhaseR",
        "elbowAngleL", "elbowAngleR",
        "shoulderAngleL", "shoulderAngleR",
        "armPivotYL", "armPivotYR",
        "armPalmYL", "armPalmYR",
        "handYL", "handYR",
        "handContactL", "handContactR",
        "pelvisY", "comX", "fzL", "fzR"]


def main():
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        return
    label = args[0]
    t_from, t_to, step = 0.0, 1e9, 0.1
    for i, a in enumerate(args):
        if a == "--from":
            t_from = float(args[i + 1])
        if a == "--to":
            t_to = float(args[i + 1])
        if a == "--step":
            step = float(args[i + 1])

    path = os.path.join(RUNS, label + ".csv")
    rows = []
    with open(path, newline="") as fh:
        for row in csv.DictReader(fh, delimiter=";"):
            t = float(row["time"])
            if t < t_from or t > t_to:
                continue
            rows.append(row)

    have = [c for c in COLS if rows and c in rows[0]]
    miss = [c for c in COLS if c not in have]
    print("== %s  %d rows  cols missing: %s" % (label, len(rows), miss or "none"))
    print("  row: " + "  ".join("%s" % c for c in have))
    print("  " + "-" * 100)
    last = -1e9
    for row in rows:
        t = float(row["time"])
        if t - last < step - 1e-9:
            continue
        last = t
        cells = []
        for c in have:
            if c in ("armPressPhaseL", "armPressPhaseR", "handContactL", "handContactR"):
                cells.append("%s=%s" % (c[-9:], row[c]))
            else:
                try:
                    cells.append("%8.4f" % float(row[c]))
                except ValueError:
                    cells.append(row[c])
        print("%7.4f  " % t + "  ".join(cells))


if __name__ == "__main__":
    main()
