# ART-BW3R probe1: compare the take params (camera, shotRate, switches) of the standing
# art take aBW3b vs the bird lane's fresh final-state render bVW1fv_wander.
import json

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
KEYS = ["shotRate", "camAuto", "camAutoMargin", "birdCam", "birdCamSize", "camZoom",
        "sourceFingerprint", "duration", "samples"]

for lab in ["aBW3b", "aBW1f2_wander", "bVW1fv_wander", "bVW1s_wander"]:
    try:
        d = json.load(open(RUNS + "\\" + lab + ".json", encoding="utf-8-sig"))
    except Exception as e:
        print("%-16s MISSING %s" % (lab, e))
        continue
    ap = d.get("applied") or {}
    print("== %s" % lab)
    for k in KEYS:
        if k in ap:
            print("   applied.%-14s %s" % (k, ap[k]))
        elif k in d:
            print("   %-22s %s" % (k, d[k]))
    # print any remaining applied keys that look like camera/rate flags
    for k in sorted(ap):
        if any(s in k.lower() for s in ("cam", "rate", "shot", "zoom", "bird")):
            if k not in KEYS:
                print("   applied.%-14s %s" % (k, ap[k]))
