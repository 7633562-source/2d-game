"""Read-only: the press's effect on the body, from stored arm records.

Cycle 13 recon. For each label: inside the fallen window, how HIGH the body
actually is (pelvis, shoulder pivot, head) and how far it travelled, split into
press-on and press-off frames. No run, no build, no physics.
"""
import csv
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")


def load(label):
    j = json.load(open(os.path.join(RUNS, label + ".json"), encoding="utf-8-sig"))
    with open(os.path.join(RUNS, label + ".csv"), newline="", encoding="utf-8-sig") as f:
        rows = list(csv.DictReader(f, delimiter=";"))
    return j, rows


def num(r, k, d=0.0):
    try:
        return float(r[k])
    except Exception:
        return d


def pct(xs, p):
    if not xs:
        return float("nan")
    xs = sorted(xs)
    return xs[min(len(xs) - 1, int(p * len(xs)))]


def main(labels):
    for lab in labels:
        j, rows = load(lab)
        ft = float(j.get("fallTime") or 8.0)
        win = [r for r in rows if num(r, "time") >= ft + 0.5]
        if not win:
            print(lab, "no fallen window")
            continue
        cols = list(win[0].keys())
        pel = [c for c in cols if c.lower() in ("pelvisy", "pelvis_y")]
        sh = [c for c in cols if "shoulder" in c.lower() and "y" in c.lower()]
        print("== %s  fallen window %.2f..%.2f s  %d frames" %
              (lab, num(win[0], "time"), num(win[-1], "time"), len(win)))
        print("   pelvis column: %s   shoulder-y candidates: %s" % (pel, sh[:6]))
        for name in pel:
            ys = [num(r, name) for r in win]
            print("   %-14s min %.3f  p10 %.3f  mean %.3f  p90 %.3f  max %.3f  > -1.80: %d%%"
                  % (name, min(ys), pct(ys, 0.10), sum(ys) / len(ys), pct(ys, 0.90), max(ys),
                     100 * sum(1 for y in ys if y > -1.80) / len(ys)))
        for ph in ("armPressPhaseL", "armPressPhaseR"):
            on = [r for r in win if num(r, ph) in (2.0, 3.0)]
            off = [r for r in win if num(r, ph) not in (2.0, 3.0)]
            for tag, grp in (("press/hold", on), ("other", off)):
                if not grp or not pel:
                    continue
                ys = [num(r, pel[0]) for r in grp]
                print("   %-12s %-11s n=%-5d pelvis mean %.3f  max %.3f  >-1.80 %d%%"
                      % (ph, tag, len(grp), sum(ys) / len(ys), max(ys),
                         100 * sum(1 for y in ys if y > -1.80) / len(ys)))
        for k in ("comTravelX", "maxAbsTiltDeg", "fallTime"):
            if k in j:
                print("   json %-14s %s" % (k, j[k]))


if __name__ == "__main__":
    main(sys.argv[1:] or ["p_arms70", "p_arms71"])
