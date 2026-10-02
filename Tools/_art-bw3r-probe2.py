# ART-BW3R probe2: (a) where the bird+fan sits in the NEW take bVW1fv_wander (shotRate 20) vs the
# standing art takes (shotRate 25, birdCam 0.85) - full-frame fan-mask bbox (the row probe's mask);
# (b) a pose-matched frame pick inside the new take by the ART-BW3 picker's own weights.
import csv
import os
import sys

from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TGT = {"bodyY": -1.796623, "pitch": 7.019785, "heightAboveSupport": 0.203377,
       "fanChord": 0.109992, "x": -0.655642}


def mask_bbox(label, frame):
    im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (label, frame))).convert("RGB")
    px = im.load()
    w, h = im.size
    xs, ys = [], []
    for y in range(0, h, 2):
        for x in range(0, w, 2):
            r, g, b = px[x, y]
            if r < 135 and g < 192 and b < 232 and b > 90:
                xs.append(x)
                ys.append(y)
    if not xs:
        print("%-16s f%-4d no mask" % (label, frame))
        return None
    bb = (min(xs), min(ys), max(xs), max(ys))
    print("%-16s f%-4d bbox %4d,%4d .. %4d,%4d  w %3d h %3d  centre %4d,%4d" % (
        label, frame, bb[0], bb[1], bb[2], bb[3], bb[2] - bb[0], bb[3] - bb[1],
        (bb[0] + bb[2]) // 2, (bb[1] + bb[3]) // 2))
    return bb


def num(d, k):
    try:
        return float(d[k])
    except (ValueError, TypeError):
        return None


def pick(label, rate):
    rows = list(csv.DictReader(open(os.path.join(RUNS, label + ".csv"), encoding="utf-8-sig")))
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
    print("%s: airborne+fanspread candidates %d (t 2.5-6.0)" % (label, len(cand)))
    for dist, d in cand[:4]:
        print("   f%-4d t=%.3f dist=%.4f bodyY=%.4f pitch=%.3f h=%.4f chord=%.5f x=%.4f fanO=%.2f el=%.0f" % (
            round(num(d, "t") * rate), num(d, "t"), dist, num(d, "bodyY"), num(d, "pitch"),
            num(d, "heightAboveSupport"), num(d, "fanChord"), num(d, "x"),
            num(d, "fanOpen") or 0, num(d, "elbowDeg") or 0))
    return cand


# (a) the standing takes
for lab, fr in [("aBW3b", 93), ("aBW1f2_wander", 95), ("aBW3pre", 92)]:
    mask_bbox(lab, fr)

# (b) the new take: candidates + where the bird sits on a walk frame and on the chosen frames
pick("bVW1fv_wander", 20)
for fr in [40, 450]:
    mask_bbox("bVW1fv_wander", fr)
