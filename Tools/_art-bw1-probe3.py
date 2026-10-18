# ART-BW1 probe 3: print the mic pair's own rows (PRE bW9f1_wander f95, kept bW10f2 f93)
# so the after-take can be matched to the same pose.
import csv

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
COLS = ("frame", "t", "bodyY", "bodyPitch", "x", "y", "vx", "vy", "pitch", "mode",
        "heightAboveSupport", "fanOpen", "fanChord", "elbowDeg", "wristDeg", "wingReach",
        "flapPhase", "flapOn", "grounded", "supportKind")


def row_at(label, frame):
    t = frame / 25.0
    with open(RUNS + "\\" + label + ".csv", encoding="utf-8-sig") as f:
        best, bd = None, 9e9
        for d in csv.DictReader(f):
            try:
                tt = float(d["t"])
            except (ValueError, TypeError):
                continue
            if abs(tt - t) < bd:
                bd, best = abs(tt - t), d
    return best, bd


for label, frame in (("bW9f1_wander", 95), ("bW10f2", 93)):
    d, bd = row_at(label, frame)
    out = {k: None for k in COLS}
    out["frame"] = frame
    for k in COLS:
        if k == "frame":
            continue
        v = d.get(k)
        try:
            out[k] = round(float(v), 6)
        except (ValueError, TypeError):
            out[k] = v
    print(label, "|dt|=%.5f" % bd, out)
