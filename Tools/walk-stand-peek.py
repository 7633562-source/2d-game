"""Read-only peek at a trial CSV: the walk-window timing (stand -> first step -> fall).

Usage: python walk-stand-peek.py [label]   (default burn_walk)
Prints: comX on a 0.5 s grid for the first 9 s, ground-contact transitions,
the first fell=1 / down=1 rows, and the comX span of the pre-walk window.
"""
import csv
import os
import sys

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
label = sys.argv[1] if len(sys.argv) > 1 else "burn_walk"
path = os.path.join(RUNS, label + ".csv")

rows = []
with open(path, newline="", encoding="utf-8-sig") as f:
    r = csv.DictReader(f, delimiter=";")
    for row in r:
        rows.append(row)
        if float(row["time"]) > 12.0:
            break

print("%s: %d rows read (to 12 s)" % (label, len(rows)))

print("--- 0.5 s grid, 0..9 s ---")
for row in rows:
    t = float(row["time"])
    if abs(t * 2 - round(t * 2)) < 1e-9 and t <= 9.0:
        print("%5.2f comX=%7s velX=%8s L=%s R=%s stepActive=%s swaps=%s fell=%s down=%s"
              % (t, row["comX"], row["comVelX"], row["leftGrounded"], row["rightGrounded"],
                 row["stepWalkActive"], row["stepSwapCount"], row["fell"], row["down"]))

print("--- ground-contact transitions ---")
prev = None
for row in rows:
    cur = (row["leftGrounded"], row["rightGrounded"])
    if prev is not None and cur != prev:
        print("t=%s  L=%s R=%s (was %s,%s) swaps=%s comX=%s"
              % (row["time"], cur[0], cur[1], prev[0], prev[1], row["stepSwapCount"], row["comX"]))
    prev = cur

print("--- first fell=1 / down=1 ---")
for key in ("fell", "down"):
    for row in rows:
        if row[key] == "1":
            print("%s first 1 at t=%s comX=%s" % (key, row["time"], row["comX"]))
            break

pre = [float(x["comX"]) for x in rows if float(x["time"]) <= 5.0]
print("comX 0-5 s: min %.4f max %.4f" % (min(pre), max(pre)))
