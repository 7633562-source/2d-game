#!/usr/bin/env python
"""ARM cycle 14, read-only: is the press released while the body still has two palms?

Usage: python Tools/arm-cycle14-release.py <label> [<label> ...]

For each stored run, over the fallen window (t >= fall + 0.5 s, so the fall itself
is not counted):

  * the share of frames with BOTH palms in real contact, exactly one, none;
  * per side, every press stroke (a contiguous run of phase 2 frames): its length,
    its own contact share, and whether the stroke ENDED while both palms were
    still loaded (a release that had no reason);
  * every sink of the pelvis >= 0.10 m: the contact state on the frame the sink
    started, and the side whose phase left the press first.

No physics, no build, no run.
"""
import csv
import json
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Trials", "Runs")


def load(label):
    rows = []
    with open(os.path.join(RUNS, label + ".csv"), newline="") as fh:
        for r in csv.DictReader(fh, delimiter=";"):
            rows.append(r)
    return rows


def main():
    for label in sys.argv[1:]:
        rows = load(label)
        try:
            js = json.load(open(os.path.join(RUNS, label + ".json")))
        except Exception:
            js = {}
        fall = float(js.get("fallTime", 7.385))
        win = [r for r in rows if float(r["time"]) >= fall + 0.5]
        if not win:
            continue
        both = one = none = 0
        for r in win:
            l = r["handContactL"] == "1"
            rr = r["handContactR"] == "1"
            if l and rr:
                both += 1
            elif l or rr:
                one += 1
            else:
                none += 1
        n = float(len(win))
        print("== %s  fallen window %.2f..%.2f s, %d frames" % (label, float(win[0]["time"]), float(win[-1]["time"]), len(win)))
        print("   palms loaded: both %.1f%%  one %.1f%%  none %.1f%%" % (100 * both / n, 100 * one / n, 100 * none / n))

        for s in ("L", "R"):
            strokes = []
            cur = None
            for r in win:
                ph = int(r["armPressPhase" + s])
                if ph == 2:
                    if cur is None:
                        cur = []
                    cur.append(r)
                elif cur is not None:
                    strokes.append(cur)
                    cur = None
            if cur is not None:
                strokes.append(cur)
            ended_two = 0
            durs = []
            for st in strokes:
                durs.append(float(st[-1]["time"]) - float(st[0]["time"]))
                if st[-1]["handContactL"] == "1" and st[-1]["handContactR"] == "1":
                    ended_two += 1
            if durs:
                print("   side %s: %d strokes, length mean %.2f s (min %.2f max %.2f), ended with BOTH palms loaded %d/%d"
                      % (s, len(strokes), sum(durs) / len(durs), min(durs), max(durs), ended_two, len(strokes)))
            else:
                print("   side %s: no strokes" % s)

        # sinks
        sinks = []
        i = 0
        while i < len(win) - 1:
            y0 = float(win[i]["pelvisY"])
            j = i
            best = y0
            while j < len(win) and float(win[j]["pelvisY"]) <= y0 + 0.02:
                best = min(best, float(win[j]["pelvisY"]))
                j += 1
            drop = y0 - best
            if drop >= 0.10:
                sinks.append((float(win[i]["time"]), drop, win[i]["handContactL"], win[i]["handContactR"],
                              win[i]["armPressPhaseL"], win[i]["armPressPhaseR"]))
            i = max(j, i + 1)
        print("   sinks >= 0.10 m: %d" % len(sinks))
        for t, drop, cl, cr, pl, pr in sinks[:8]:
            print("      t %6.2f s  drop %.3f m  contact %s/%s  phase %s/%s" % (t, drop, cl, cr, pl, pr))


if __name__ == "__main__":
    main()
