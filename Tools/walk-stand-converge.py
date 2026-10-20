"""When does the stand's own state finish converging?

Prints the timeline (0..6 s, 0.25 s grid) of the columns the walk/drive logic
reads as "the body is quiet and its load path is calibrated":
wSup, fzOverWsup, fzL, fzR, comX, comVelX, supportMargin, pelvisVelY.
Also prints, for the first sample above a list of candidate start times,
whether those columns are already at their 5.0 s value.

Usage: python walk-stand-converge.py [label]
"""
import csv
import os
import sys

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
label = sys.argv[1] if len(sys.argv) > 1 else "burn_walk"
path = os.path.join(RUNS, label + ".csv")

WATCH = ["wSup", "fzOverWsup", "fzL", "fzR", "comX", "comVelX", "supportMargin",
         "pelvisVelY", "down", "stepWalkActive", "walkCollapsed", "walkPosed"]

rows = []
with open(path, newline="", encoding="utf-8-sig") as f:
    r = csv.DictReader(f, delimiter=";")
    for row in r:
        rows.append(row)
        if float(row["time"]) > 6.0:
            break

print("%s: %d rows to 6 s" % (label, len(rows)))
print("  t     " + "".join("%14s" % c for c in WATCH))
step = None
for row in rows:
    t = float(row["time"])
    if abs(t * 4 - round(t * 4)) < 1e-9 and t <= 5.5:
        print("%5.2f  " % t + "".join("%14s" % row.get(c, "-") for c in WATCH))

ref = next(x for x in rows if float(x["time"]) >= 5.0)
print("--- candidate start times vs the 5.0 s reference (col: value@start -> @5.0) ---")
for t0 in (0.75, 1.0, 1.2, 1.5, 2.0, 2.5, 3.0):
    row = next(x for x in rows if float(x["time"]) >= t0)
    print("t0=%4.2f (%s)" % (t0, row["time"]))
    for c in WATCH:
        a, b = row.get(c, "-"), ref.get(c, "-")
        try:
            gap = abs(float(a) - float(b))
            flag = "   " if gap < 1e-6 else "  *" if gap < 1.0 else " **"
            print("        %-14s %14s -> %14s  gap %.6g%s" % (c, a, b, gap, flag))
        except (TypeError, ValueError):
            print("        %-14s %14s -> %14s %s" % (c, a, b, "" if a == b else "  *"))
