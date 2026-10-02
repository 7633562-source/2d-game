#!/usr/bin/env python3
"""ART-H3 probe: which joint bends hardest in the film window of the accepted walk row.

Reads the walk lane's accepted CSV (read-only) and prints, for the window the film is cut
from, the extreme |angle| of each tracked joint and when it happens - so the round's NEW
close-up still can be picked at the real worst bend of THIS gait (not a guess).
"""
import os
import sys

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CSV = os.path.join(PROJECT, "Trials", "Runs", "c6w60_gene531_walk.csv")
ONSET, END = 5.0, 20.0

COLS = ["kneeL", "kneeR", "elbowAngleL", "elbowAngleR", "ankleL", "ankleR",
        "hipL", "hipR", "shoulderAngleL", "shoulderAngleR"]


def main():
    with open(CSV, "r", encoding="utf-8", errors="replace") as fh:
        head = fh.readline().rstrip("\n").split(";")
        idx = {n: i for i, n in enumerate(head)}
        for c in COLS:
            if c not in idx:
                print("MISSING COLUMN", c)
                return 2
        best = {}
        for line in fh:
            r = line.rstrip("\n").split(";")
            try:
                t = float(r[idx["time"]])
            except (ValueError, IndexError):
                continue
            if t < ONSET or t > END:
                continue
            for c in COLS:
                try:
                    v = abs(float(r[idx[c]]))
                except (ValueError, IndexError):
                    continue
                if c not in best or v > best[c][0]:
                    best[c] = (v, t)
    print("window %.1f..%.1f  (max |angle| deg, deg, t)" % (ONSET, END))
    for c in COLS:
        if c in best:
            print("  %-14s %6.1f deg at t=%.3f" % (c, best[c][0], best[c][1]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
