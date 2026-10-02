# ART-BW1 probe 7: pick the pose-matched frame of the new take (vs the kept f93 row of
# bW10f2) and print the tone ladder at fixed sample points, so the fill change is checked
# numerically before the eyes read the sheets.
import csv
import os
from collections import Counter
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
LABEL = "aBW1f1_wander"
TGT = {"bodyY": -1.796623, "pitch": 7.019785, "heightAboveSupport": 0.203377,
       "fanChord": 0.109992, "x": -0.655642}


def num(d, k):
    try:
        return float(d[k])
    except (ValueError, TypeError):
        return None


rows = list(csv.DictReader(open(os.path.join(RUNS, LABEL + ".csv"), encoding="utf-8-sig")))
cand = []
for d in rows:
    t = num(d, "t")
    if t is None or not (2.5 <= t <= 6.0):
        continue
    if num(d, "mode") != 2 or num(d, "grounded") != 0:
        continue
    if (num(d, "fanOpen") or 0) < 0.99:
        continue
    dist = (abs(num(d, "bodyY") - TGT["bodyY"]) * 10.0
            + abs(num(d, "pitch") - TGT["pitch"]) * 0.02
            + abs(num(d, "heightAboveSupport") - TGT["heightAboveSupport"]) * 10.0
            + abs(num(d, "fanChord") - TGT["fanChord"]) * 100.0
            + abs(num(d, "x") - TGT["x"]) * 0.5)
    cand.append((dist, d))
cand.sort(key=lambda p: p[0])
print("candidates (mode 2, fan open, grounded 0):", len(cand))
for dist, d in cand[:5]:
    print("  f%d t=%.3f dist=%.4f bodyY=%.4f pitch=%.3f h=%.4f chord=%.5f x=%.4f vx=%.4f vy=%.4f" % (
        round(num(d, "t") * 25), num(d, "t"), dist, num(d, "bodyY"), num(d, "pitch"),
        num(d, "heightAboveSupport"), num(d, "fanChord"), num(d, "x"),
        num(d, "vx"), num(d, "vy")))

best = cand[0][1]
n = round(num(best, "t") * 25)
print("chosen frame", n)

im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (LABEL, n))).convert("RGB")
BOX = (690, 700, 1090, 1060)
box = im.crop(BOX)
bpx = box.load()
cnt = Counter()
for y in range(300):
    for x in range(400):
        r, g, b = bpx[x, y]
        if b >= r and b >= g and (r + g + b) < 620:
            cnt[(r // 8 * 8, g // 8 * 8, b // 8 * 8)] += 1
print("new-take bluish tones (top 12):")
for c, k in cnt.most_common(12):
    print("   ", c, k)
print("fixed samples (box coords):")
for (bx, by) in ((110, 140), (130, 160), (150, 180), (170, 200), (250, 240)):
    print("   (%d,%d) %s" % (bx, by, bpx[bx, by]))
