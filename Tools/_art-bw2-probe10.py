# ART-BW2 probe 10: pick the after frame (same pose window as the kept f93/f95 pair), then the
# dark-core check (ART-BW1's ruler) and the fan's envelope/ink, before vs after.
import csv
import os
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
LABEL = "aBW2f2_wander"
BEFORE = ("aBW1f2_wander", 95)
BOX = (690, 700, 1090, 1060)
PX = 635.3          # the take's px/m (birdCam 0.85 ortho, 1080 px)
TGT = {"bodyY": -1.796623, "pitch": 7.019785, "heightAboveSupport": 0.203377,
       "fanChord": 0.109992, "x": -0.655642}


def num(d, k):
    try:
        return float(d[k])
    except (ValueError, TypeError):
        return None


def pick(label, tgt):
    rows = list(csv.DictReader(open(os.path.join(RUNS, label + ".csv"), encoding="utf-8-sig")))
    cand = []
    for d in rows:
        t = num(d, "t")
        if t is None or not (2.5 <= t <= 6.0):
            continue
        if num(d, "mode") != 2 or num(d, "grounded") != 0 or (num(d, "fanOpen") or 0) < 0.99:
            continue
        dist = (abs(num(d, "bodyY") - tgt["bodyY"]) * 10.0
                + abs(num(d, "pitch") - tgt["pitch"]) * 0.02
                + abs(num(d, "heightAboveSupport") - tgt["heightAboveSupport"]) * 10.0
                + abs(num(d, "fanChord") - tgt["fanChord"]) * 100.0
                + abs(num(d, "x") - tgt["x"]) * 0.5)
        cand.append((dist, d))
    cand.sort(key=lambda p: p[0])
    print("%s: candidates %d" % (label, len(cand)))
    for dist, d in cand[:3]:
        print("   f%-4d t=%.3f dist=%.4f bodyY=%.4f pitch=%.3f h=%.4f chord=%.5f x=%.4f fanO=%.2f el=%.0f" % (
            round(num(d, "t") * 25), num(d, "t"), dist, num(d, "bodyY"), num(d, "pitch"),
            num(d, "heightAboveSupport"), num(d, "fanChord"), num(d, "x"),
            num(d, "fanOpen") or 0, num(d, "elbowDeg") or 0))
    return round(num(cand[0][1], "t") * 25)


def stats(label, frame, tag):
    im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (label, frame))).convert("RGB")
    box = im.crop(BOX)
    bpx = box.load()
    W, H = box.size
    TOP = 300          # ART-BW1's own ruler: the grass strip below y300 is not the bird
    out = []
    for thr in (250, 300, 350):
        k = 0
        x0, y0, x1, y1 = W, H, 0, 0
        for y in range(TOP):
            for x in range(W):
                r, g, b = bpx[x, y]
                if r + g + b < thr:
                    k += 1
                    x0, y0, x1, y1 = min(x0, x), min(y0, y), max(x1, x), max(y1, y)
        out.append("sum<%d:%5d%s" % (thr, k, (" bbox x%d..%d y%d..%d" % (x0, x1, y0, y1)) if k else ""))
    dark = (9999, None)
    for y in range(TOP):
        for x in range(W):
            r, g, b = bpx[x, y]
            if r + g + b < dark[0]:
                dark = (r + g + b, (r, g, b))
    print("%-8s f%-3d %s darkest %s" % (tag, frame, "  ".join(out), dark[1]))

    # the pad's ink: the slate family (the fan + the parts), sky (132,192,240) and the pale
    # blends excluded by r <= 129
    n = 0
    mx0, my0, mx1, my1 = W, H, 0, 0
    for y in range(TOP):
        for x in range(W):
            r, g, b = bpx[x, y]
            if b > g > r and b - r <= 104 and r <= 128:
                n += 1
                mx0, my0, mx1, my1 = min(mx0, x), min(my0, y), max(mx1, x), max(my1, y)
    print("   ink px %6d  bbox x%d..%d y%d..%d  (%.0f x %.0f mm)" % (
        n, mx0, mx1, my0, my1, (mx1 - mx0) / PX * 1000, (my1 - my0) / PX * 1000))
    return n, (mx0, my0, mx1, my1)


if __name__ == "__main__":
    after = pick(LABEL, TGT)
    print("chosen after frame", after)
    stats(*BEFORE, tag="before")
    stats(LABEL, after, tag="after")
    print("FRAME=%d" % after)
