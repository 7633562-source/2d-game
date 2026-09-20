"""Read-only frame trace of one fallen window: who moves the pelvis?

Cycle 13. Prints a compact per-N-frame table of one side's press phase, both
elbow/shoulder angles, the latched palm Y, the live palm Y, the pivot Y and the
pelvis Y, so "the arm lifted the body" can be told from "the body bobbed while
the arm worked". Reads a stored CSV; no run, no build, no physics.

Usage: python Tools/arm-press-frame-trace.py p_arms70 --from 27.0 --to 30.2 --every 10
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
    ap.add_argument("--every", type=int, default=10)
    a = ap.parse_args()
    with open(os.path.join(RUNS, a.label + ".csv"), newline="", encoding="utf-8-sig") as f:
        rows = list(csv.DictReader(f, delimiter=";"))
    sel = [r for r in rows if a.t0 <= num(r, "time") <= a.t1]
    print("== %s  %d frames in %.2f..%.2f" % (a.label, len(sel), a.t0, a.t1))
    print(" time  phL phR  strkL  elL    elR    |  pivYL  plmYL  plmLivYL handYL ctL"
          "  |  pelvisY  tilt  legL legR")
    for r in sel[::a.every]:
        print(" %5.2f  %2d  %2d  %4d  %6.1f %6.1f | %6.3f %6.3f %6.3f %6.3f  %d  | %7.3f %6.1f  %2d   %2d"
              % (num(r, "time"), int(num(r, "armPressPhaseL")), int(num(r, "armPressPhaseR")),
                 int(num(r, "armStrokesL")), num(r, "elbowAngleL"), num(r, "elbowAngleR"),
                 num(r, "armPivotYL"), num(r, "armPalmYL"),
                 num(r, "handYL") - 0.0, num(r, "handYL"),
                 1 if r.get("handContactL") in ("True", "true", "1") else 0,
                 num(r, "pelvisY"), num(r, "torsoTilt"),
                 int(num(r, "leftGrounded")), int(num(r, "rightGrounded"))))


if __name__ == "__main__":
    main()
