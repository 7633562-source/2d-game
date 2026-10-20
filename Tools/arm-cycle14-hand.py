#!/usr/bin/env python
"""ARM cycle 14, read-only: how low each palm actually gets, per side.

Usage: python Tools/arm-cycle14-hand.py <label> [<label> ...]

Prints, per run: the lowest hand centre per side and when, the recorded real
contact fraction per side, and the lowest point the drive latched per side
(armPalmYL/R) -- so "the palm never grips" can be told from "the palm never
arrives". No physics, no build, no run.
"""
import csv
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Trials", "Runs")


def main():
    for label in sys.argv[1:]:
        path = os.path.join(RUNS, label + ".csv")
        mn = {"L": (9.0, 0.0), "R": (9.0, 0.0)}
        palm = {"L": 9.0, "R": 9.0}
        hit = {"L": 0, "R": 0}
        n = 0
        low = {"L": (9.0, 0.0), "R": (9.0, 0.0)}
        with open(path, newline="") as fh:
            for r in csv.DictReader(fh, delimiter=";"):
                n += 1
                t = float(r["time"])
                for s in ("L", "R"):
                    y = float(r["handY" + s])
                    if y < mn[s][0]:
                        mn[s] = (y, t)
                    if y < low[s][0]:
                        low[s] = (y, t)
                    p = float(r["armPalmY" + s])
                    if p < palm[s]:
                        palm[s] = p
                    if r["handContact" + s] == "1":
                        hit[s] += 1
        print("%-12s rows %d" % (label, n))
        for s in ("L", "R"):
            print("   side %s: hand centre low %.4f m @ %.2f s | latched point low %.4f m | real contact %.3f"
                  % (s, mn[s][0], mn[s][1], palm[s], hit[s] / float(n)))


if __name__ == "__main__":
    main()
