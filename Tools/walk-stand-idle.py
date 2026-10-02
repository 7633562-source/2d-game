"""Is the pre-walk stand provably IDLE between 1.5 s and 5.0 s?

Reads one trial CSV and compares the row at the first sample >= 1.5 s with the
row at the first sample >= 5.0 s, column by column, plus the drift of every
column over the 1.5..5.0 s window. If nothing moves, shortening -walkTime from
5 to 1.5 only removes dead time.

Usage: python walk-stand-idle.py [label] [t_early] [t_late]
"""
import csv
import os
import sys

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
label = sys.argv[1] if len(sys.argv) > 1 else "burn_walk"
t_early = float(sys.argv[2]) if len(sys.argv) > 2 else 1.5
t_late = float(sys.argv[3]) if len(sys.argv) > 3 else 5.0
path = os.path.join(RUNS, label + ".csv")

head = None
rows = []
with open(path, newline="", encoding="utf-8-sig") as f:
    r = csv.reader(f, delimiter=";")
    head = next(r)
    for row in r:
        if len(row) != len(head):
            continue
        t = float(row[0])
        if t > t_late + 0.05:
            row_time = t
            rows.append(row)
            break
        rows.append(row)

# pick rows
early = next(x for x in rows if float(x[0]) >= t_early)
late = next(x for x in rows if float(x[0]) >= t_late)
window = [x for x in rows if t_early <= float(x[0]) <= t_late]

print("%s: early row t=%s, late row t=%s, window rows=%d"
      % (label, early[0], late[0], len(window)))

diffs = []
for c, name in enumerate(head):
    try:
        a = float(early[c] or 0.0)
        b = float(late[c] or 0.0)
    except ValueError:
        if early[c] != late[c]:
            diffs.append((name, early[c], late[c], None))
        continue
    if abs(a - b) > 1e-6:
        diffs.append((name, a, b, abs(a - b)))

print("columns differing between t=%s and t=%s: %d of %d"
      % (early[0], late[0], len(diffs), len(head)))
for name, a, b, gap in diffs[:20]:
    print("   %-28s %s -> %s (gap %s)" % (name, a, b, gap))

# drift of each column across the window
drift = []
for c, name in enumerate(head):
    vals = []
    for row in window:
        try:
            vals.append(float(row[c] or 0.0))
        except ValueError:
            vals = None
            break
    if not vals:
        continue
    span = max(vals) - min(vals)
    if span > 1e-6:
        drift.append((name, span, min(vals), max(vals)))

print("columns whose value moves anywhere in %s..%s s: %d"
      % (early[0], late[0], len(drift)))
drift.sort(key=lambda x: -x[1])
for name, span, lo, hi in drift[:15]:
    print("   %-28s span %.6g  [%s .. %s]" % (name, span, lo, hi))
