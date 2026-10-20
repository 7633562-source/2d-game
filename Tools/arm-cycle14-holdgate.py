#!/usr/bin/env python
"""ARM cycle 14, read-only: how often the two-palm branch of the Hold could fire.

Usage: python Tools/arm-cycle14-holdgate.py <label> [<label> ...]

Counts, over the fallen window, the frames that satisfy the cycle-14 gate before
the edit: the side is in Hold (phase 3) and BOTH palms are in real contact at the
same time (the drive's own lostContactTime is not recorded, but a frame with both
contacts real is the necessary condition for the gate), and the frames where the
side is in Hold with its OWN palm still on the slab while the other side is
loaded or not. This is the upper bound of the branch's firing rate -- an edit
whose gate is never satisfied on the stored record cannot be measured by it.

No physics, no build, no run.
"""
import csv
import json
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Trials", "Runs")


def main():
    for label in sys.argv[1:]:
        rows = []
        with open(os.path.join(RUNS, label + ".csv"), newline="") as fh:
            for r in csv.DictReader(fh, delimiter=";"):
                rows.append(r)
        try:
            js = json.load(open(os.path.join(RUNS, label + ".json")))
        except Exception:
            js = {}
        fall = float(js.get("fallTime", 7.385))
        win = [r for r in rows if float(r["time"]) >= fall + 0.5]
        n = float(len(win))
        print("== %s  fallen window %d frames" % (label, len(win)))
        for s, o in (("L", "R"), ("R", "L")):
            hold = 0
            hold_own = 0
            hold_both = 0
            for r in win:
                if int(r["armPressPhase" + s]) == 3:
                    hold += 1
                    own = r["handContact" + s] == "1"
                    other = r["handContact" + o] == "1"
                    if own:
                        hold_own += 1
                    if own and other:
                        hold_both += 1
            print("   side %s: Hold %5d (%.1f%%) | Hold with own palm on the slab %5d | Hold with BOTH palms %5d"
                  % (s, hold, 100 * hold / n, hold_own, hold_both))


if __name__ == "__main__":
    main()
