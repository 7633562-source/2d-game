#!/usr/bin/env python3
"""ART-H3 probe: how far two trial rows (same walk flags) are apart, column by column.

The row of record is bit-reproducible only on a quiet machine: the first frames' dt carry
the wall clock, so a loaded box shifts the 4th decimal on row ~3. This prints the honest
shape of the difference (how many cells, which columns, the max |delta| and its time), so a
report can say "the same walk, float noise of order X" instead of claiming or hiding equality.

Usage:
  python Tools/arth3-rowdiff.py Trials/Runs/human_dress3_ref531.csv Trials/Runs/c6w60_gene531_walk.csv
"""
import sys


def load(path):
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        head = fh.readline().rstrip("\n").split(";")
        rows = [ln.rstrip("\n").split(";") for ln in fh if ln.strip()]
    return head, rows


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    ha, ra = load(sys.argv[1])
    hb, rb = load(sys.argv[2])
    if ha != hb:
        print("HEADERS DIFFER: %d vs %d fields" % (len(ha), len(hb)))
        return 2
    n = min(len(ra), len(rb))
    print("%s vs %s: %d vs %d rows" % (sys.argv[1], sys.argv[2], len(ra), len(rb)))
    diff_cells = 0
    per_col = {}
    first = None
    worst = None  # (delta, row, col)
    for i in range(n):
        a, b = ra[i], rb[i]
        if len(a) != len(b):
            print("row %d: field count %d vs %d" % (i + 1, len(a), len(b)))
            continue
        for j, (x, y) in enumerate(zip(a, b)):
            if x == y:
                continue
            k = "row%d" % (i + 1)
            per_col.setdefault(j, 0)
            per_col[j] += 1
            diff_cells += 1
            try:
                d = abs(float(x) - float(y))
            except ValueError:
                d = float("inf")
            if worst is None or d > worst[0]:
                worst = (d, i + 1, j)
            if first is None:
                first = (i + 1, j, ha[j], x, y)
    print("differing cells: %d of %d" % (diff_cells, n * len(ha)))
    if first:
        print("first difference: row %d col %d (%s): %s vs %s" % first)
    if worst and worst[0] != float("inf"):
        i, j = worst[1], worst[2]
        try:
            t = ra[i - 1][ha.index("time")] if "time" in ha else "?"
        except ValueError:
            t = "?"
        print("max |delta| %.6g at row %d (t=%s) col %d (%s): %s vs %s"
              % (worst[0], i, t, j, ha[j], ra[i - 1][j], rb[i - 1][j]))
    top = sorted(per_col.items(), key=lambda kv: -kv[1])[:8]
    print("columns by cell count: " + ", ".join(
        "%s(%d)" % (ha[j], c) for j, c in top if j < len(ha)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
