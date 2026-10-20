#!/usr/bin/env python
"""Column-by-column diff of two trial CSVs (same shape, label rows allowed).

Usage: _csv-cols.py <labelA> <labelB> [--max <n>]

Prints how many columns differ anywhere and, for the first few, the first row
and the widest gap. Used to tell "the run is a repeat" (only identity columns
differ) from "the physics moved" (body columns differ).
"""
import csv
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "Trials", "Runs")


def load(label):
    with open(os.path.join(RUNS, label + ".csv"), newline="", encoding="utf-8-sig") as f:
        r = csv.reader(f, delimiter=";")
        head = next(r)
        rows = [row for row in r if len(row) == len(head)]
    return head, rows


def main():
    a, b = sys.argv[1], sys.argv[2]
    show = int(sys.argv[sys.argv.index("--max") + 1]) if "--max" in sys.argv else 8
    ha, ra = load(a)
    hb, rb = load(b)
    print("== %s vs %s: %d rows vs %d rows, %d cols vs %d cols"
          % (a, b, len(ra), len(rb), len(ha), len(hb)))
    if ha != hb:
        print("   HEADER DIFFERS")
    n = min(len(ra), len(rb))
    bad = []
    for c, name in enumerate(ha):
        first = None
        worst = 0.0
        for i in range(n):
            if ra[i][c] != rb[i][c]:
                worst = max(worst, abs(float(ra[i][c] or 0) - float(rb[i][c] or 0)))
                if first is None:
                    first = i
        if first is not None:
            bad.append((name, first, worst))
    print("   columns differing: %d of %d" % (len(bad), len(ha)))
    for name, first, worst in bad[:show]:
        print("      %-24s first row %-6d max |gap| %.6g" % (name, first, worst))


main()
