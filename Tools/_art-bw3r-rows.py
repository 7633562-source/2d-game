# ART-BW3R row fence: the same ruler as ART-BW3's (_art-bw3-rows.py), pairs from argv.
# usage: python Tools/_art-bw3r-rows.py <labelA> <labelB> [<labelC> <labelD> ...]
import csv
import sys

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
COLS = ("x", "y", "vx", "vy", "pitch", "mode")


def rows(label):
    with open(RUNS + "\\" + label + ".csv", encoding="utf-8-sig") as f:
        return list(csv.DictReader(f))


def cmp(a, b):
    ra, rb = rows(a), rows(b)
    n = min(len(ra), len(rb))
    print("== %s vs %s  (rows %d vs %d)" % (a, b, len(ra), len(rb)))
    for c in COLS:
        mx = 0.0
        cnt = {1e-4: 0, 1e-3: 0}
        for i in range(n):
            try:
                d = abs(float(ra[i][c]) - float(rb[i][c]))
            except (ValueError, TypeError):
                continue
            if d > mx:
                mx = d
            for k in cnt:
                if d > k:
                    cnt[k] += 1
        print("   %-6s max|d|=%.6f  >1e-4/%d >1e-3/%d" % (c, mx, cnt[1e-4], cnt[1e-3]))


a = sys.argv[1:]
for i in range(0, len(a), 2):
    cmp(a[i], a[i + 1])
