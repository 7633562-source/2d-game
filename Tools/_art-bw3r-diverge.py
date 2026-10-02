# ART-BW3R divergence: the first row where two takes' chains separate (per column).
# usage: _art-bw3r-diverge.py <labelA> <labelB> [col ...]
import csv
import sys

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
COLS = sys.argv[3:] or ["x", "bodyY", "pitch"]


def rows(label):
    with open(RUNS + "\\" + label + ".csv", encoding="utf-8-sig") as f:
        return list(csv.DictReader(f))


a, b = sys.argv[1], sys.argv[2]
ra, rb = rows(a), rows(b)
n = min(len(ra), len(rb))
first = {}
mx = {c: 0.0 for c in COLS}
for i in range(n):
    for c in COLS:
        try:
            d = abs(float(ra[i][c]) - float(rb[i][c]))
        except (ValueError, TypeError):
            continue
        if d > mx[c]:
            mx[c] = d
        if d > 1e-4 and c not in first:
            first[c] = (i, float(ra[i]["t"]), d)
print("%s vs %s  rows %d vs %d" % (a, b, len(ra), len(rb)))
for c in COLS:
    f = first.get(c)
    print("   %-6s first>1e-4 at row %s | max|d| %.6f" % (
        c, ("%d (t=%.2fs)" % (f[0], f[1])) if f else "never", mx[c]))
