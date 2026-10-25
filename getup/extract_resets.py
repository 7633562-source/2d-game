"""extract_resets.py - lying poses from the real Unity fall takes (READ ONLY on E:/Games).

Scans Trials/Runs/cl527*.csv and cl520*.csv (the get-up batteries), finds in each take the first QUIET row
after the fall flag (`fell` = 1), at least 0.3 s after it: over the next 0.5 s |d pelvisY| < 1.5 cm,
|d pelvisTilt| < 8 deg, every joint |d q| < 15 deg, and the pelvis centre within 0.45 m of the ground. That is the
body at rest right after the fall = where the game's get-up dwell (1.5 s quiet) begins. Classified by the PELVIS
world angle: back (supine) if pelvisTilt > 45 deg, belly (prone) if < -45, else "other" (a floor sit in 2D).

Writes resets.json: list of {kind, route, take, t, q (17, rad, JN order), tilt (rad, pelvis), z (pelvis centre
above the Unity ground top), head_z, hand_z}. Wrist angles are not logged by the game -> 0.
Unity -> MuJoCo: z = pelvisY + 2.0 (ground top y = -2.0), angles degrees -> rad, same sign (skeleton.md).
usage: env -u PYTHONPATH <venv>/python.exe -I extract_resets.py
"""
import glob
import json
import math
import os
import time

RUNS = ["E:/Games/2D Game/My project/Trials/Runs", "E:/Games/2D Game/walkwt/Trials/Runs"]
HERE = os.path.dirname(os.path.abspath(__file__))
JN = ["lumbar", "neck", "head", "shoulder_r", "elbow_r", "wrist_r", "shoulder_l", "elbow_l", "wrist_l",
      "hip_r", "knee_r", "ankle_r", "mtp_r", "hip_l", "knee_l", "ankle_l", "mtp_l"]
COL = {"lumbar": "lumbar", "neck": "neckAngle", "head": "headAngle", "shoulder_r": "shoulderAngleR",
       "elbow_r": "elbowAngleR", "wrist_r": None, "shoulder_l": "shoulderAngleL", "elbow_l": "elbowAngleL",
       "wrist_l": None, "hip_r": "hipR", "knee_r": "kneeR", "ankle_r": "ankleR", "mtp_r": "toeR",
       "hip_l": "hipL", "knee_l": "kneeL", "ankle_l": "ankleL", "mtp_l": "toeL"}
NEED = ["time", "fell", "pelvisY", "pelvisTilt", "torsoTilt", "headY", "handYL", "handYR"] + [c for c in COL.values() if c]
WIN = 100  # rows = 0.5 s at 200 Hz


def scan(path):
    with open(path, encoding="utf-8", errors="replace") as f:
        hdr = f.readline().rstrip("\n").split(";")
        idx = {}
        for c in NEED:
            if c not in hdr:
                return None, "no column " + c
            idx[c] = hdr.index(c)
        rows = []
        fell_i = None
        for ln in f:
            p = ln.rstrip("\n").split(";")
            if len(p) < len(hdr):
                continue
            try:
                r = [float(p[idx[c]]) for c in NEED]
            except ValueError:
                continue
            rows.append(r)
            if fell_i is None and r[1] >= 0.5:
                fell_i = len(rows) - 1
            if fell_i is not None and len(rows) - fell_i > 6000:  # 30 s after the fall is enough
                break
    if fell_i is None:
        return None, "no fall"
    ji = {c: NEED.index(c) for c in NEED}
    qcols = [ji[COL[j]] for j in JN if COL[j]]
    for i in range(fell_i + 60, len(rows) - WIN):
        a, b = rows[i], rows[i + WIN]
        if a[ji["pelvisY"]] > -2.0 + 0.45:
            continue
        ok = abs(b[ji["pelvisY"]] - a[ji["pelvisY"]]) < 0.015 and abs(b[ji["pelvisTilt"]] - a[ji["pelvisTilt"]]) < 8.0
        if ok:
            for k in range(i, i + WIN, 10):
                c = rows[k]
                if abs(c[ji["pelvisY"]] - a[ji["pelvisY"]]) > 0.015 or abs(c[ji["pelvisTilt"]] - a[ji["pelvisTilt"]]) > 8.0:
                    ok = False
                    break
                if any(abs(c[q] - a[q]) > 15.0 for q in qcols):
                    ok = False
                    break
        if ok:
            q = [math.radians(a[ji[COL[j]]]) if COL[j] else 0.0 for j in JN]
            tor = a[ji["torsoTilt"]]
            tp = a[ji["pelvisTilt"]]
            route = "back" if tp > 45 else ("belly" if tp < -45 else "other")
            return dict(t=a[0], q=[round(x, 5) for x in q], tilt=round(math.radians(a[ji["pelvisTilt"]]), 5),
                        torso=round(math.radians(tor), 5), z=round(a[ji["pelvisY"]] + 2.0, 4),
                        head_z=round(a[ji["headY"]] + 2.0, 4),
                        hand_z=[round(a[ji["handYR"]] + 2.0, 4), round(a[ji["handYL"]] + 2.0, 4)],
                        route=route, t_fell=rows[fell_i][0]), None
    return None, "never quiet"


def main():
    t0 = time.time()
    files = []
    for d in RUNS:
        for pat in ("cl527*.csv", "cl520*.csv"):
            files += glob.glob(os.path.join(d, pat))
    files = sorted(f for f in files if not f.endswith(("_plants.csv", "_assist.csv")) and "_assist" not in os.path.basename(f))
    out, fails = [], {}
    for k, f in enumerate(files):
        stem = os.path.basename(f)[:-4]
        kind = stem.rsplit("_", 1)[1]
        r, why = scan(f)
        if r is None:
            fails[why] = fails.get(why, 0) + 1
            continue
        r["kind"] = kind
        r["take"] = stem
        out.append(r)
        if k % 100 == 0:
            print(k, len(files), stem, r["route"], "%.1f s" % (time.time() - t0), flush=True)
    json.dump(out, open(os.path.join(HERE, "resets.json"), "w"), indent=0)
    from collections import Counter
    print("takes", len(files), "poses", len(out), "fails", fails)
    print("routes", Counter(r["route"] for r in out))
    print("kind -> route", sorted(Counter((r["kind"], r["route"]) for r in out).items()))
    print("wall %.1f s" % (time.time() - t0))


if __name__ == "__main__":
    main()
