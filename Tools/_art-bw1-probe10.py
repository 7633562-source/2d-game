# ART-BW1 probe 10: pick the pose-matched frame of the final take (vs the kept f93 row of
# bW10f2), then the dark-zone count for before vs after (same box).
import csv
import os
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
LABEL = "aBW1f2_wander"
BOX = (690, 700, 1090, 1060)
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
    if num(d, "mode") != 2 or num(d, "grounded") != 0 or (num(d, "fanOpen") or 0) < 0.99:
        continue
    dist = (abs(num(d, "bodyY") - TGT["bodyY"]) * 10.0
            + abs(num(d, "pitch") - TGT["pitch"]) * 0.02
            + abs(num(d, "heightAboveSupport") - TGT["heightAboveSupport"]) * 10.0
            + abs(num(d, "fanChord") - TGT["fanChord"]) * 100.0
            + abs(num(d, "x") - TGT["x"]) * 0.5)
    cand.append((dist, d))
cand.sort(key=lambda p: p[0])
print("candidates:", len(cand))
for dist, d in cand[:4]:
    print("  f%d t=%.3f dist=%.4f bodyY=%.4f pitch=%.3f h=%.4f chord=%.5f x=%.4f" % (
        round(num(d, "t") * 25), num(d, "t"), dist, num(d, "bodyY"), num(d, "pitch"),
        num(d, "heightAboveSupport"), num(d, "fanChord"), num(d, "x")))
n = round(num(cand[0][1], "t") * 25)
print("chosen frame", n)


def stats(label, frame, tag):
    im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (label, frame))).convert("RGB")
    box = im.crop(BOX)
    bpx = box.load()
    out = []
    for thr in (250, 300, 350):
        k = 0
        x0, y0, x1, y1 = 400, 300, 0, 0
        for y in range(300):
            for x in range(400):
                r, g, b = bpx[x, y]
                if r + g + b < thr:
                    k += 1
                    x0, y0, x1, y1 = min(x0, x), min(y0, y), max(x1, x), max(y1, y)
        out.append("sum<%d:%5d%s" % (thr, k, (" bbox x%d..%d y%d..%d" % (x0, x1, y0, y1)) if k else ""))
    dark = (9999, None)
    for y in range(300):
        for x in range(400):
            r, g, b = bpx[x, y]
            if r + g + b < dark[0]:
                dark = (r + g + b, (r, g, b))
    print("%-7s f%-3d %s darkest %s" % (tag, frame, "  ".join(out), dark[1]))


stats("bW10f2", 93, "before")
stats(LABEL, n, "after")
