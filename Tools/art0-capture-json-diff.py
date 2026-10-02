#!/usr/bin/env python3
"""ART-0: compare two gallery capture JSONs key by key.

Usage:  python Tools/art0-capture-json-diff.py <labelOld> <labelNew>

Prints every key that moved, with both values, and the totals. This is how a cycle says
"the capture's GEOMETRY did not move, only its identity and its pictures" - the frames prove
the pixels, this proves the numbers behind them (row boxes, content bounds, dressed counts).
"""
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")


def load(label):
    path = os.path.join(RUNS, label + "-gallery.json")
    with open(path, "r", encoding="utf-8") as fh:
        return path, json.load(fh)


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    pa, a = load(sys.argv[1])
    pb, b = load(sys.argv[2])
    print("A = %s" % os.path.basename(pa))
    print("B = %s" % os.path.basename(pb))

    keys = sorted(set(a) | set(b))
    moved = []
    for k in keys:
        if a.get(k) != b.get(k):
            moved.append(k)
            print("  MOVED %-22s %s -> %s" % (k, json.dumps(a.get(k))[:90], json.dumps(b.get(k))[:90]))
    print("keys total=%d identical=%d moved=%d" % (len(keys), len(keys) - len(moved), len(moved)))
    print("moved keys: %s" % ", ".join(moved) if moved else "moved keys: none")

    for k in ("boundsBeforeDress", "boundsAfterDress", "boundsDeltaMeters", "rowBoxes",
              "dressedRenderers", "realItems", "missingItems"):
        if k in a or k in b:
            same = a.get(k) == b.get(k)
            print("  %-20s %s   A=%s  B=%s" % (k, "same" if same else "MOVED",
                                               json.dumps(a.get(k))[:60], json.dumps(b.get(k))[:60]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
