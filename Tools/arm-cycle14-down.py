#!/usr/bin/env python
"""ARM cycle 14, read-only. Does the press's own enable survive the lift it makes?

Usage: python Tools/arm-cycle14-down.py <label> <from> <to> [step]

Prints, one line per step: the drive's own `down` flag (|tilt| >= fallTiltDeg,
which is also the press's enable), the tilt it is computed from, the pelvis
height, the body's comX, the two press phases, the two hand contacts and the two
live palm heights. No physics, no build, no run -- it reads a stored record.
"""
import csv
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Trials", "Runs")


def main():
    if len(sys.argv) < 4:
        print(__doc__)
        return
    label, a, b = sys.argv[1], float(sys.argv[2]), float(sys.argv[3])
    step = float(sys.argv[4]) if len(sys.argv) > 4 else 0.1
    print("== %s  %.1f..%.1f s" % (label, a, b))
    last = -1e9
    with open(os.path.join(RUNS, label + ".csv"), newline="") as fh:
        for r in csv.DictReader(fh, delimiter=";"):
            t = float(r["time"])
            if t < a or t > b or t - last < step - 1e-9:
                continue
            last = t
            print("%7.4f down=%s tilt=%8.2f pelvY=%8.4f comX=%8.4f ph=%s/%s c=%s/%s hand=%8.4f/%8.4f palm=%8.4f/%8.4f pelvTilt=%8.2f"
                  % (t, r.get("down"), float(r.get("torsoTilt", 0)), float(r.get("pelvisY", 0)),
                     float(r.get("comX", 0)), r.get("armPressPhaseL"), r.get("armPressPhaseR"),
                     r.get("handContactL"), r.get("handContactR"),
                     float(r.get("handYL", 0)), float(r.get("handYR", 0)),
                     float(r.get("armPalmYL", 0)), float(r.get("armPalmYR", 0)),
                     float(r.get("pelvisTilt", 0))))


if __name__ == "__main__":
    main()
