"""Read-only: is the press's stroke ended by the DRIVE or by the JOINT?

Cycle 13. Per step inside one press phase: the elbow TARGET the drive asked for,
the elbow angle the joint achieved, the stroke's extension, the pivot and the
pelvis. If the target goes to full extension and the joint cannot follow, the
press is a strength problem; if the target stops moving, it is a drive problem.

Usage: python Tools/arm-press-target-trace.py p_arms70 --from 35.6 --to 38.2 --side L --every 8
"""
import argparse
import csv
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")


def num(r, k, d=0.0):
    try:
        return float(r[k])
    except Exception:
        return d


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("label")
    ap.add_argument("--from", dest="t0", type=float, default=0.0)
    ap.add_argument("--to", dest="t1", type=float, default=1e9)
    ap.add_argument("--side", default="L")
    ap.add_argument("--every", type=int, default=8)
    a = ap.parse_args()
    s = a.side.upper()
    with open(os.path.join(RUNS, a.label + ".csv"), newline="", encoding="utf-8-sig") as f:
        rows = list(csv.DictReader(f, delimiter=";"))
    sel = [r for r in rows if a.t0 <= num(r, "time") <= a.t1]
    print("== %s side %s  %d frames %.2f..%.2f" % (a.label, s, len(sel), a.t0, a.t1))
    print(" time  ph  strk  tgtElb  elbAngle  tgtSh   shAngle  ext   strokeExt  pivY    pelvY   handY")
    for r in sel[::a.every]:
        print(" %5.2f  %2d  %4d  %7.1f  %7.1f  %7.1f  %7.1f  %5.1f %7.1f  %7.4f %7.3f %7.3f"
              % (num(r, "time"), int(num(r, "armPressPhase" + s)), int(num(r, "armStrokes" + s)),
                 num(r, "armTargetElbow" + s), num(r, "elbowAngle" + s),
                 num(r, "armTargetShoulder" + s), num(r, "shoulderAngle" + s),
                 num(r, "elbowAngle" + s) - num(r, "armTargetElbow" + s),
                 num(r, "armStrokeExt" + s), num(r, "armPivotY" + s), num(r, "pelvisY"),
                 num(r, "handY" + s)))


if __name__ == "__main__":
    main()
