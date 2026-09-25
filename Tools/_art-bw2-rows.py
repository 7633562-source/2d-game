# ART-BW2 row fence: the art-only change must not move a physics row.
# Same ruler as B-WING-10 / ART-BW1: the six columns x/y/vx/vy/pitch/mode, the floor <= 3.1e-5,
# zero rows > 1e-4; the reference take = bW10p2_wander (the earlier build).
import csv

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


cmp("aBW2p3_wander", "bW10p2_wander")
cmp("aBW2p3_wander", "aBW1p2_wander")
cmp("aBW2p2_wander", "bW10p2_wander")
