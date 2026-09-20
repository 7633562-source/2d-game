#!/usr/bin/env python
"""ARM cycle 14, read-only: is the stroke-extension column the joint's own motion?

Usage: python Tools/arm-cycle14-ext.py <label> <from> <to> [step]

Prints per step, per side: the recorded elbow joint angle, the drive's stroke
extension column, the press phase and the real palm contact. If the extension
column is a run-scoped maximum it can never fall; if it is the joint's extension
it must track the joint angle. No physics, no build, no run.
"""
import csv
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Trials", "Runs")


def main():
    label, a, b = sys.argv[1], float(sys.argv[2]), float(sys.argv[3])
    step = float(sys.argv[4]) if len(sys.argv) > 4 else 0.05
    last = -1e9
    print("== %s  %.2f..%.2f  side L / side R" % (label, a, b))
    with open(os.path.join(RUNS, label + ".csv"), newline="") as fh:
        for r in csv.DictReader(fh, delimiter=";"):
            t = float(r["time"])
            if t < a or t > b or t - last < step - 1e-9:
                continue
            last = t
            print("%7.4f | L el=%9.4f ext=%9.4f ph=%s c=%s | R el=%9.4f ext=%9.4f ph=%s c=%s | pelvY=%8.4f"
                  % (t, float(r.get("elbowAngleL", 0)), float(r.get("armStrokeExtL", 0)),
                     r.get("armPressPhaseL"), r.get("handContactL"),
                     float(r.get("elbowAngleR", 0)), float(r.get("armStrokeExtR", 0)),
                     r.get("armPressPhaseR"), r.get("handContactR"),
                     float(r.get("pelvisY", 0))))


if __name__ == "__main__":
    main()
