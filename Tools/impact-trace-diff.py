#!/usr/bin/env python3
"""Where do two impact FRAME traces stop agreeing?

    python Tools/impact-trace-diff.py Trials/Runs/imp_cb1.csv Trials/Runs/other/imp_cb1.csv

`Tools/impact-cb-diverge.py` answers this for the `-impacts.csv` row-group (one row per
throw, 31 columns). The full frame trace `<label>.csv` is the other half of the record:
8001 rows for a 40 s quiet run at 200 Hz, 3200 for a 16 s throw, one row per fixed step and
~200 columns of body state. A rebuild can leave every published impact number untouched (the
card's quotable keys) and still print a different md5 - either because the recorder gained a
column (the walk lane owns that file) or because the body's own trace moved.

So the comparison is on the COMMON COLUMNS, by name, and the added / removed columns are
printed separately: a file that grew a column is not a file whose numbers moved. Prints rows
compared, rows differing on the common columns, the FIRST differing row with its time, the
columns that differ there and the union over the run. Exit code 1 when the common columns
move anywhere, 0 when they do not (a grown column set alone prints a NOTE and exits 0), so a
shell loop can test a whole set.
"""
from __future__ import annotations

import io
import sys


def load(path):
    with io.open(path, encoding="utf-8-sig") as handle:
        lines = [line.rstrip("\r\n") for line in handle if line.strip()]
    names = [name.strip() for name in lines[0].split(";")]
    rows = [line.split(";") for line in lines[1:]]
    return names, rows


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2

    left_path, right_path = sys.argv[1], sys.argv[2]
    names_a, rows_a = load(left_path)
    names_b, rows_b = load(right_path)
    common = [name for name in names_a if name in names_b]
    only_a = [name for name in names_a if name not in names_b]
    only_b = [name for name in names_b if name not in names_a]
    print("# %s: %d rows, %d columns" % (left_path, len(rows_a), len(names_a)))
    print("# %s: %d rows, %d columns" % (right_path, len(rows_b), len(names_b)))
    if only_a or only_b:
        print("NOTE column sets differ: only in %s -> %s; only in %s -> %s"
              % (left_path, only_a[:8], right_path, only_b[:8]))

    idx_a = [names_a.index(name) for name in common]
    idx_b = [names_b.index(name) for name in common]
    compared = min(len(rows_a), len(rows_b))
    time_index = None
    for candidate in ("time", "t", "simTime", "tSim"):
        if candidate in names_a:
            time_index = names_a.index(candidate)
            break
    differing_rows = 0
    first = None
    moved_columns = set()
    for i in range(compared):
        row_a, row_b = rows_a[i], rows_b[i]
        if len(row_a) <= idx_a[-1] or len(row_b) <= idx_b[-1]:
            continue
        same = True
        for column_index, a_index in enumerate(idx_a):
            if row_a[a_index] != row_b[idx_b[column_index]]:
                same = False
                moved_columns.add(common[column_index])
        if same:
            continue
        differing_rows += 1
        if first is None:
            first = i

    print("# common columns: %d; rows compared: %d; rows differing on them: %d"
          % (len(common), compared, differing_rows))
    if first is None:
        print("IDENTICAL on every common column, %d rows" % compared)
        return 0

    stamp = rows_a[first][time_index] if time_index is not None else str(first)
    at_first = [name for name, a_index in zip(common, idx_a)
                if rows_a[first][a_index] != rows_b[first][idx_b[common.index(name)]]]
    print("FIRST DIFFERING ROW %d (t = %s s): %d column(s) -> %s"
          % (first, stamp, len(at_first), ", ".join(at_first[:12])))
    for column in at_first[:6]:
        print("    %-28s %s -> %s"
              % (column, rows_a[first][names_a.index(column)], rows_b[first][names_b.index(column)]))
    print("COLUMNS THAT MOVE ANYWHERE (%d): %s"
          % (len(moved_columns), ", ".join(sorted(moved_columns))))
    return 1


if __name__ == "__main__":
    sys.exit(main())
