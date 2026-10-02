#!/usr/bin/env python
"""Arm press verdict reader: does the fall press push the body up?

Usage: arm-press-check.py <label> [<label> ...] [--fall <t>] [--out <file>]

Reads Trials/Runs/<label>.csv (delimiter ';') and, for the window that starts
`--fall` seconds after the fall (default: the run's own fall time + 0.5 s, read
from the JSON), prints per side:

  strokes      number of press strokes (armStrokes<side> counter changes)
  ext>=20      strokes whose peak armStrokeExt<side> is at least the 20 deg bar
  max ext      deepest stroke extension, deg
  contact      share of window frames with handContact<side> true (real contact)
  rise         best armShoulderLift<side>, metres

and for the body: pelvisY min/max/final, torsoTilt min/final, comX travel, and
the share of the window in which the pelvis is above its lying band. A press
that only strokes the elbow is visible here as ext >= 20 with rise ~ 0 and a
pelvis that never leaves the floor band.
"""
import csv
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")


def fall_time(label):
    path = os.path.join(RUNS, label + ".json")
    try:
        with open(path, encoding="utf-8-sig") as f:
            doc = json.load(f)
    except Exception:  # noqa: BLE001 - the CSV alone is still readable
        return None
    for key in ("fallTime", "fellTime"):
        if key in doc:
            return float(doc[key])
    return None


def read(label):
    path = os.path.join(RUNS, label + ".csv")
    with open(path, newline="", encoding="utf-8-sig") as f:
        rows = list(csv.DictReader(f, delimiter=";"))
    return rows


def num(row, key, default=0.0):
    try:
        return float(row[key])
    except (KeyError, ValueError, TypeError):
        return default


def strokes_of(rows, side):
    """Split the window into strokes on the armStrokes<side> counter."""
    key_c = "armStrokes" + side
    key_e = "armStrokeExt" + side
    out = []
    cur = None
    last = int(num(rows[0], key_c))
    for row in rows:
        c = int(num(row, key_c))
        if c != last:
            if cur is not None:
                out.append(cur)
            cur = [num(row, key_e)]
            last = c
        elif cur is not None:
            cur[0] = max(cur[0], num(row, key_e))
    if cur is not None:
        out.append(cur)
    return [s[0] for s in out]


def report(label, t0):
    rows = read(label)
    win = [r for r in rows if num(r, "time") >= t0]
    if not win:
        print("== %s  (no rows after %.2f s)" % (label, t0))
        return
    print("== %s  window %.2f-%.2f s, %d frames" % (label, t0, num(win[-1], "time"), len(win)))
    for side in ("L", "R"):
        ext = strokes_of(win, side)
        big = [e for e in ext if e >= 20.0]
        contact = sum(1 for r in win if num(r, "handContact" + side) > 0.5)
        grounded = sum(1 for r in win if num(r, "handGrounded" + side) > 0.5)
        rise = max(num(r, "armShoulderLift" + side) for r in win)
        palm = [abs(num(r, "handY" + side) - num(r, "armPalmY" + side)) for r in win]
        palm.sort()
        print("   %s strokes %-3d ext>=20 %d (%.0f%%) max ext %5.1f deg | contact %.0f%%"
              " grounded %.0f%% | rise %.3f m | med |hand-palm| %.3f m"
              % (side, len(ext), len(big), 100.0 * len(big) / len(ext) if ext else 0.0,
                 max(ext) if ext else 0.0, 100.0 * contact / len(win),
                 100.0 * grounded / len(win), rise, palm[len(palm) // 2]))
    py = [num(r, "pelvisY") for r in win]
    tilt = [num(r, "torsoTilt") for r in win]
    comx = [num(r, "comX") for r in win]
    floor = min(py)
    band = floor + 0.05
    lifted = sum(1 for v in py if v > band)
    piv = [max(num(r, "armPivotYL"), num(r, "armPivotYR")) for r in win]
    print("   shoulder pivot Y min %.3f max %.3f final %.3f (floor+0.05 share %.0f%%)"
          % (min(piv), max(piv), piv[-1],
             100.0 * sum(1 for v in piv if v > min(piv) + 0.05) / len(piv)))
    print("   body pelvisY min %.3f max %.3f final %.3f (floor %.3f, above %.3f m: %.0f%%)"
          % (floor, max(py), py[-1], floor, band, 100.0 * lifted / len(py)))
    print("   body torsoTilt min %.1f final %.1f | comX %.3f -> %.3f | handY min %.3f"
          % (min(tilt), tilt[-1], comx[0], comx[-1],
             min(num(r, "handY" + s) for r in win for s in ("L", "R"))))


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    force = None
    if "--fall" in sys.argv:
        force = float(sys.argv[sys.argv.index("--fall") + 1])
    for label in args:
        t0 = force
        if t0 is None:
            ft = fall_time(label)
            t0 = (ft + 0.5) if ft is not None else 8.0
        report(label, t0)


main()
