"""Per-side press phase mix and the real hand contact INSIDE each phase.

Reads one or more stored arm records (`Trials/Runs/<label>.csv`, the semicolon
CSV the headless trial writes) and answers the two questions the press work needs:

  * how much of the ground window each side spends in each press phase
    (0 idle 1 lift 2 press 3 hold 4 plant), and
  * whether the palm is REALLY on the slab inside that phase
    (`handContactL/R`, i.e. Collider2D.IsTouchingLayers, not the slop band).

It also lists the start-to-start gaps of each side's strokes, taken from the
plant -> press phase transition; a regular gap is the churn signature.

Usage: python arm-phase-contact.py <label> [<label> ...]
"""

import collections
import csv
import os
import sys

BASE = r"E:/Games/2D Game/My project/Trials/Runs"
PHASE_NAMES = {"0": "idle", "1": "lift", "2": "press", "3": "hold", "4": "plant"}
GROUND_WINDOW_START_S = 7.88


def open_rows(path):
    first = open(path, newline="").readline()
    delim = ";" if first.count(";") > first.count(",") else ","
    rows = list(csv.DictReader(open(path, newline=""), delimiter=delim))
    return rows, list(rows[0].keys())


def find(cols, *names):
    for name in names:
        for col in cols:
            if col.lower() == name.lower():
                return col
    for name in names:
        for col in cols:
            if name.lower() in col.lower():
                return col
    return None


def report(label):
    path = os.path.join(BASE, label + ".csv")
    if not os.path.exists(path):
        print("missing", label)
        return
    rows, cols = open_rows(path)
    t_col = find(cols, "time", "t")
    pair = (("L", find(cols, "armPressPhaseL", "pressPhaseL")),
            ("R", find(cols, "armPressPhaseR", "pressPhaseR")))
    contact = (("L", find(cols, "handContactL")), ("R", find(cols, "handContactR")))
    window = [r for r in rows if float(r[t_col]) >= GROUND_WINDOW_START_S]
    n = len(window)
    print("== %s  rows %d  ground window >= %.2f s: %d frames" %
          (label, len(rows), GROUND_WINDOW_START_S, n))
    for (side, ph), (_, ct) in zip(pair, contact):
        if not ph or not ct:
            print("   %s: phase or contact column missing (%s, %s)" % (side, ph, ct))
            continue
        share = collections.Counter(r[ph] for r in window)
        hits = collections.defaultdict(lambda: [0, 0])
        for r in window:
            slot = hits[r[ph]]
            slot[0] += 1
            if r[ct] in ("True", "true", "1"):
                slot[1] += 1
        print("   %s phase share:" % side,
              {PHASE_NAMES.get(k, k): round(v / n, 3) for k, v in sorted(share.items())})
        print("   %s contact inside phase:" % side,
              {PHASE_NAMES.get(k, k): "%d%%" % round(100 * v[1] / v[0])
               for k, v in sorted(hits.items())})
        prev, starts = None, []
        for r in window:
            if prev == "4" and r[ph] == "2":
                starts.append(float(r[t_col]))
            prev = r[ph]
        if len(starts) > 1:
            print("   %s strokes %d, start-to-start gaps" % (side, len(starts)),
                  [round(starts[i + 1] - starts[i], 2) for i in range(len(starts) - 1)])


if __name__ == "__main__":
    for lab in sys.argv[1:] or ["p_arms81"]:
        report(lab)
