# ART-BW3 frame picker v2: the same weighted pick as ART-BW2 probe 10, so the same-pose frame
# of every film is chosen by the round's own convention. Target = the standing before's own row
# (aBW1f2_wander f95's window: bodyY -1.796623, pitch 7.019785, h 0.203377, fanChord 0.109992,
# x -0.655642), mode 2, airborne, fan open.
import csv
import os
import sys

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TGT = {"bodyY": -1.796623, "pitch": 7.019785, "heightAboveSupport": 0.203377,
       "fanChord": 0.109992, "x": -0.655642}


def num(d, k):
    try:
        return float(d[k])
    except (ValueError, TypeError):
        return None


def pick(label, tgt=TGT):
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


for label in sys.argv[1:] or ["aBW1f2_wander", "aBW3pre", "aBW3"]:
    if os.path.exists(os.path.join(RUNS, label + ".csv")):
        pick(label)
    else:
        print("%s MISSING" % label)
