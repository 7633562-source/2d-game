#!/usr/bin/env python
"""ARM cycle 14, read-only: what carries the pelvis while it FALLS back.

Usage: python Tools/arm-cycle14-fallback.py <label> [<label> ...]

Finds every rise-then-fall of the pelvis in the fallen window (a local maximum
followed by a drop of at least 0.05 m) and reports the palm state over the
falling stretch: the share of the drop covered with BOTH palms in real contact,
exactly one, and neither, plus the phases of the two sides on the first and last
frame of the drop. The question: does the body sink while both palms are on the
slab (a support problem) or while one palm is off it (a release)?

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
        ys = [float(r["pelvisY"]) for r in win]
        print("== %s   window %.2f..%.2f s" % (label, float(win[0]["time"]), float(win[-1]["time"])))
        i = 1
        n = len(win)
        found = 0
        while i < n - 1:
            # a local maximum
            if ys[i] >= ys[i - 1] and ys[i] > ys[i + 1]:
                j = i
                while j < n - 1 and ys[j + 1] <= ys[j]:
                    j += 1
                drop = ys[i] - ys[j]
                if drop >= 0.05:
                    seg = win[i:j + 1]
                    both = one = none = 0
                    for r in seg:
                        l = r["handContactL"] == "1"
                        rr = r["handContactR"] == "1"
                        if l and rr:
                            both += 1
                        elif l or rr:
                            one += 1
                        else:
                            none += 1
                    m = float(len(seg))
                    found += 1
                    print("   top t %6.2f y %.3f -> bottom t %6.2f y %.3f  drop %.3f m over %.2f s"
                          % (float(win[i]["time"]), ys[i], float(win[j]["time"]), ys[j], drop,
                             float(win[j]["time"]) - float(win[i]["time"])))
                    print("        palms while falling: both %4.1f%%  one %4.1f%%  none %4.1f%%   phases %s/%s -> %s/%s"
                          % (100 * both / m, 100 * one / m, 100 * none / m,
                             win[i]["armPressPhaseL"], win[i]["armPressPhaseR"],
                             win[j]["armPressPhaseL"], win[j]["armPressPhaseR"]))
                i = j + 1
            else:
                i += 1
        print("   rise/fall cycles >= 0.05 m: %d" % found)


if __name__ == "__main__":
    main()
