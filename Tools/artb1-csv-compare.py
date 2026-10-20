"""ART-B1 CSV comparator: is the run's physics the same?

The recorder writes one row per physics sample (t, then the measured columns). Two runs
of the same command can still differ in the FLOAT TAIL of a value, so this tool reports,
per column: how many rows differ as text, the first such row, and the largest numeric
delta. A column whose only deltas are float tails is not a physics change; a column that
differs from row 0 onwards with growing deltas is.

    python Tools/artb1-csv-compare.py <labelA> <labelB> [--rows N] [--tol 1e-6]
"""

import sys

RUNS = "E:/Games/2D Game/My project/Trials/Runs"


def read(label, limit=None):
    path = "%s/%s.csv" % (RUNS, label)
    with open(path, "r", encoding="utf-8-sig") as fh:
        head = fh.readline().rstrip("\r\n").split(",")
        rows = []
        for line in fh:
            if limit is not None and len(rows) >= limit:
                break
            rows.append(line.rstrip("\r\n").split(","))
    return head, rows


def main():
    a_label, b_label = sys.argv[1], sys.argv[2]
    limit = None
    tol = 1e-6
    if "--rows" in sys.argv:
        limit = int(sys.argv[sys.argv.index("--rows") + 1])
    if "--tol" in sys.argv:
        tol = float(sys.argv[sys.argv.index("--tol") + 1])

    head_a, rows_a = read(a_label, limit)
    head_b, rows_b = read(b_label, limit)
    print("columns: %d / %d  rows: %d / %d"
          % (len(head_a), len(head_b), len(rows_a), len(rows_b)))
    if head_a != head_b:
        print("HEADERS DIFFER")
        return
    if len(rows_a) != len(rows_b):
        print("ROW COUNT DIFFERS - the runs are not the same length")
        return

    ncols = len(head_a)
    for c in range(ncols):
        text_diff = 0
        first = None
        max_delta = 0.0
        nonnumeric = 0
        for r in range(len(rows_a)):
            va, vb = rows_a[r][c], rows_b[r][c]
            if va == vb:
                continue
            text_diff += 1
            if first is None:
                first = r
            try:
                da = float(va.replace(",", "."))
                db = float(vb.replace(",", "."))
            except ValueError:
                nonnumeric += 1
                continue
            d = abs(da - db)
            if d > max_delta:
                max_delta = d
        if text_diff == 0:
            print("  %-24s identical" % head_a[c])
        else:
            flag = "" if max_delta <= tol else "   <-- beyond %g" % tol
            print("  %-24s differs in %5d rows (first row %s), maxdelta=%.3g, text-only=%d%s"
                  % (head_a[c], text_diff, first, max_delta, nonnumeric, flag))


if __name__ == "__main__":
    main()
