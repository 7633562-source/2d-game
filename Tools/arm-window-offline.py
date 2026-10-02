"""Offline reconstruction of the arm press window from a stored run CSV.

Read-only: no Unity, no trial. It re-derives, from the per-frame CSV alone, the
same quantities ArmDrive publishes as the matched press window (PressWindow*):
the shoulder-pivot rise, the pelvis rise, the travel along the slab and the
real palm-ground contact fraction, all measured from the run's FIRST down edge
(`down` column) so a press-ON run and a press-OFF run of the same build are
comparable.

Why it exists: the press numbers inside the run JSON are stroke-scoped maxima,
and on a press-OFF run there are no strokes at all, so those columns read 0 by
construction and cannot answer "does the press lift the body". The per-frame
CSV can, on runs that were made before the window columns existed.

Usage:
    python Tools/arm-window-offline.py p_arms70 p_arms71
    python Tools/arm-window-offline.py --tilt 45 p_arms77 p_arms76

With two labels it also prints the deltas (second minus first) and the first
row/column where the two runs part company, which is what makes the pair a
matched control rather than two unrelated runs.
"""
import csv
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "Trials", "Runs")

SIDES = ("L", "R")
COLS = ("time", "down", "torsoTilt", "pelvisY", "comX", "armPivotYL", "armPivotYR",
        "armPalmYL", "armPalmYR", "handContactL", "handContactR",
        "handGroundedL", "handGroundedR", "armStrokeExtL", "armStrokeExtR",
        "armPressPhaseL", "armPressPhaseR", "armShoulderLiftL", "armShoulderLiftR",
        "pelvisTilt", "comVelX", "ctrlFallState", "stepSwapCount")


def read_run(label):
    path = os.path.join(RUNS, label + ".csv")
    if not os.path.exists(path):
        raise SystemExit("missing %s" % path)
    with open(path, newline="", encoding="utf-8") as fh:
        reader = csv.reader(fh, delimiter=";")
        head = next(reader)
        rows = [r for r in reader if r and r[0].strip() != ""]
    return path, head, rows


def num(head, row, name):
    try:
        return float(row[head.index(name)])
    except (ValueError, IndexError):
        return float("nan")


def window(path, head, rows, tilt_limit, label):
    """Everything the press window would have published, from the CSV alone."""
    edge = None
    for i, row in enumerate(rows):
        down = num(head, row, "down") == 1.0
        tilted = abs(num(head, row, "torsoTilt")) >= tilt_limit
        if down or tilted:
            edge = i
            break
    if edge is None:
        return {"label": label, "path": os.path.basename(path), "rows": len(rows),
                "edge": None, "edge_row": None, "edge_t": float("nan")}

    pivot0 = {s: num(head, rows[edge], "armPivotY" + s) for s in SIDES}
    pelvis0 = num(head, rows[edge], "pelvisY")
    com0 = num(head, rows[edge], "comX")
    win = rows[edge:]
    out = {
        "label": label,
        "path": os.path.basename(path),
        "rows": len(rows),
        "edge_row": edge,
        "edge_t": num(head, rows[edge], "time"),
        "seconds": num(head, win[-1], "time") - num(head, rows[edge], "time"),
        "contact": {}, "rise": {}, "status": {}, "recover": {},
    }
    for s in SIDES:
        hits = sum(1 for row in win if num(head, row, "handContact" + s) == 1.0)
        out["contact"][s] = hits / len(win)
        out["rise"][s] = max(num(head, row, "armPivotY" + s) - pivot0[s] for row in win)
        # what the run's own stroke columns say inside the window
        ext = [num(head, row, "armStrokeExt" + s) for row in win]
        out["status"]["strokeExtMax" + s] = max(ext)
        fase = [num(head, row, "armPressPhase" + s) for row in win]
        out["status"]["pressRows" + s] = sum(1 for f in fase if f == 2.0)
        out["status"]["phaseMax" + s] = max(fase)
    out["pelvisRise"] = max(num(head, row, "pelvisY") - pelvis0 for row in win)
    out["travelX"] = max(abs(num(head, row, "comX") - com0) for row in win)

    # Recovery: the largest climb above the RUNNING minimum, i.e. how far the
    # body came back up from its own deepest point since it went down. The
    # edge-referenced rise above is cycle 8's definition and reads 0 whenever a
    # falling body's shoulder is already at its highest on the down frame, so
    # the two are published side by side.
    for key, names in (("recover", ("armPivotYL", "armPivotYR")),
                       ("pelvisRecover", ("pelvisY",))):
        for name in names:
            low = None
            best = 0.0
            for row in win:
                v = num(head, row, name)
                low = v if low is None or v < low else low
                if v - low > best:
                    best = v - low
            tag = name.replace("armPivotY", "") if key == "recover" else ""
            if key == "recover":
                out["recover"][tag] = best
            else:
                out["pelvisRecover"] = best
    out["fell"] = max(num(head, row, "ctrlFallState") for row in win)
    out["swaps"] = max(num(head, row, "stepSwapCount") for row in win)
    return out


def first_difference(head, ra, rb):
    n = min(len(ra), len(rb))
    for i in range(n):
        if ra[i] != rb[i]:
            for c, (x, y) in enumerate(zip(ra[i], rb[i])):
                if x != y:
                    return i, head[c] if c < len(head) else "col%d" % c, x, y
            return i, "<row>", "", ""
    return None


def show(r):
    if r["edge_row"] is None:
        print("%-12s rows %6d  never down" % (r["label"], r["rows"]))
        return
    print("%-12s rows %6d  down at row %5d (t %.3f s)  window %.3f s" % (
        r["label"], r["rows"], r["edge_row"], r["edge_t"], r["seconds"]))
    print("             contact L/R %.4f / %.4f   pivot rise L/R %+.4f / %+.4f m" % (
        r["contact"]["L"], r["contact"]["R"], r["rise"]["L"], r["rise"]["R"]))
    print("             pelvis rise %+.4f m   travelX %.4f m   press rows L/R %d / %d"
          "   strokeExt max L/R %.2f / %.2f deg" % (
              r["pelvisRise"], r["travelX"], r["status"]["pressRowsL"],
              r["status"]["pressRowsR"], r["status"]["strokeExtMaxL"],
              r["status"]["strokeExtMaxR"]))
    print("             recovery above own minimum: pivot L/R %+.4f / %+.4f m"
          "   pelvis %+.4f m" % (
              r["recover"]["L"], r["recover"]["R"], r["pelvisRecover"]))
    print("             ctrlFallState max %g   swaps %g" % (r["fell"], r["swaps"]))


def main(argv):
    tilt_limit = 45.0
    labels = []
    i = 0
    while i < len(argv):
        if argv[i] == "--tilt":
            tilt_limit = float(argv[i + 1])
            i += 2
            continue
        labels.append(argv[i])
        i += 1
    if len(labels) < 1:
        raise SystemExit(__doc__)

    loaded = []
    for label in labels:
        path, head, rows = read_run(label)
        loaded.append((label, head, rows))
        show(window(path, head, rows, tilt_limit, label))

    if len(loaded) == 2:
        (la, ha, ra), (lb, hb, rb) = loaded
        diff = first_difference(ha, ra, rb)
        print("\npair %s -> %s" % (la, lb))
        wa = window("", ha, ra, tilt_limit, la)
        wb = window("", hb, rb, tilt_limit, lb)
        # Is this a matched control? Same build, same switches bar one, and the
        # recorded rows identical up to the frame the arms are allowed to differ.
        n = min(len(ra), len(rb))
        differing_rows = [i for i in range(n) if ra[i] != rb[i]]
        cols = {}
        for i in differing_rows:
            for c, (x, y) in enumerate(zip(ra[i], rb[i])):
                if x != y:
                    cols[ha[c] if c < len(ha) else "col%d" % c] = cols.get(
                        ha[c] if c < len(ha) else "col%d" % c, 0) + 1
        print("  rows %d / %d, differing rows %d, first at row %s" % (
            len(ra), len(rb), len(differing_rows),
            "none" if not differing_rows else differing_rows[0]))
        if differing_rows:
            print("  differing columns (%d): %s" % (
                len(cols), ", ".join("%s x%d" % (k, v)
                                     for k, v in sorted(cols.items(), key=lambda kv: -kv[1])[:12])))
        if diff is None:
            print("  the two runs are row-for-row identical")
        else:
            print("  first difference row %d  t %.3f  column %s  %s -> %s" % (
                diff[0], num(ha, ra[diff[0]], "time"), diff[1], diff[2], diff[3]))
            if wa["edge_row"] is not None and wa["edge_row"] == wb["edge_row"]:
                print("  both runs go down on the same row %d (t %.3f s)" % (
                    wa["edge_row"], wa["edge_t"]))
                if diff[0] >= wa["edge_row"]:
                    print("  every row BEFORE the down edge is identical")
        print("  delta (rise L/R)   %+.4f / %+.4f m" % (
            wb["rise"]["L"] - wa["rise"]["L"], wb["rise"]["R"] - wa["rise"]["R"]))
        print("  delta pelvis rise  %+.4f m   delta travelX %+.4f m" % (
            wb["pelvisRise"] - wa["pelvisRise"], wb["travelX"] - wa["travelX"]))
        print("  delta recovery L/R %+.4f / %+.4f m   pelvis %+.4f m" % (
            wb["recover"]["L"] - wa["recover"]["L"],
            wb["recover"]["R"] - wa["recover"]["R"],
            wb["pelvisRecover"] - wa["pelvisRecover"]))


if __name__ == "__main__":
    main(sys.argv[1:])
