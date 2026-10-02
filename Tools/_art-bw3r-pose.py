# ART-BW3R pose match: the closest same-pose frame pair between two takes (the standing
# picker's own weights), restricted to a time window. usage:
#   _art-bw3r-pose.py <labelA> <labelB> <rateA> <rateB> <t0> <t1> [n]
import csv
import sys

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
W = {"x": 0.5, "bodyY": 10.0, "pitch": 0.02, "heightAboveSupport": 10.0, "fanChord": 100.0}
KEYS = ("x", "bodyY", "pitch", "heightAboveSupport", "fanChord")


def load(label, t0, t1):
    with open(RUNS + "\\" + label + ".csv", encoding="utf-8-sig") as f:
        return [d for d in csv.DictReader(f)
                if t0 <= float(d["t"]) <= t1]


def num(d, k):
    try:
        return float(d[k])
    except (KeyError, ValueError):
        return None


a, b, ra, rb = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4])
t0, t1 = float(sys.argv[5]), float(sys.argv[6])
n = int(sys.argv[7]) if len(sys.argv) > 7 else 5
A, B = load(a, t0, t1), load(b, t0, t1)
best = []
for da in A:
    for db in B:
        if num(da, "mode") != num(db, "mode"):
            continue
        d = sum(abs(num(da, k) - num(db, k)) * W[k] for k in KEYS)
        best.append((d, da, db))
best.sort(key=lambda p: p[0])
print("%s (%d rows) vs %s (%d rows) in t %.2f-%.2f" % (a, len(A), b, len(B), t0, t1))
for d, da, db in best[:n]:
    print("  d=%.4f  A f%-4d t=%.3f bodyY=%.4f pitch=%.3f h=%.4f chord=%.5f x=%.3f mode %s  |  B f%-4d t=%.3f bodyY=%.4f pitch=%.3f h=%.4f chord=%.5f x=%.3f" % (
        d, round(num(da, "t") * ra), num(da, "t"), num(da, "bodyY"), num(da, "pitch"),
        num(da, "heightAboveSupport"), num(da, "fanChord"), num(da, "x"), da["mode"],
        round(num(db, "t") * rb), num(db, "t"), num(db, "bodyY"), num(db, "pitch"),
        num(db, "heightAboveSupport"), num(db, "fanChord"), num(db, "x")))
