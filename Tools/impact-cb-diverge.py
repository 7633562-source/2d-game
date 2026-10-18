#!/usr/bin/env python3
"""Where do two impact runs of the same class stop agreeing?

    python Tools/impact-cb-diverge.py Trials/Runs/l2_hit_s1-impacts.csv Trials/Runs/imp_cb1-impacts.csv

Both runs fire the same throw (seed 1, circle 0.25 m, 4 kg, 8 m/s -> torso), so their
per-frame rows must be identical until the blow changes something. The L2 cycle verified
that inside ONE build; this prints, for two DIFFERENT builds, the first row that differs,
the columns that differ there and which columns differ at all - i.e. whether a move in the
impact numbers is a move in the RESPONSE to the blow or a move in what happens later.

The frame files are semicolon-separated with the column names in the first line (no comma
delimiter), so they are parsed as plain text: the first line is the header, every later line
is one fixed step.
"""
import io
import sys


def load(path):
    with io.open(path, encoding="utf-8-sig") as handle:
        lines = [line.rstrip("\r\n") for line in handle if line.strip()]
    header = lines[0].split(";")
    names = [name.strip() for name in header]
    index = {name: i for i, name in enumerate(names)}
    rows = [line.split(";") for line in lines[1:]]
    return names, index, rows


def main():
    a_path, b_path = sys.argv[1], sys.argv[2]
    names_a, index_a, a = load(a_path)
    names_b, index_b, b = load(b_path)
    shared = [name for name in names_a if name in index_b]
    n = min(len(a), len(b))
    print("# {0}: {1} rows, {2} columns".format(a_path, len(a), len(names_a)))
    print("# {0}: {1} rows, {2} columns".format(b_path, len(b), len(names_b)))
    print("# common columns: {0}; rows compared: {1}".format(len(shared), n))
    first = None
    differing_rows = 0
    column_counts = {}
    for i in range(n):
        bad = [c for c in shared if a[i][index_a[c]] != b[i][index_b[c]]]
        if bad:
            differing_rows += 1
            if first is None:
                first = (i, bad)
            for c in bad:
                column_counts[c] = column_counts.get(c, 0) + 1
    if first is None:
        print("# the two runs are identical on every common column, {0} rows".format(n))
        return 0
    i, bad = first
    time = a[i][index_a.get("time", 0)]
    print("# rows differing: {0} of {1}".format(differing_rows, n))
    print("# first differing row {0} (t = {1} s): {2}".format(i, time, ", ".join(
        "{0} {1} -> {2}".format(c, a[i][index_a[c]], b[i][index_b[c]]) for c in bad[:6])))
    for column, count in sorted(column_counts.items(), key=lambda kv: -kv[1]):
        print("#   {0:28s} {1} rows".format(column, count))
    return 0


if __name__ == "__main__":
    sys.exit(main())
