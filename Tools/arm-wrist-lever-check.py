#!/usr/bin/env python
# Read-only: what the press does with the muscle the chain is spent through.
# ARM cycle 11. Reads stored Trials/Runs/*.csv only; writes nothing, runs no trial.
#
# The question: during a press STROKE (armPressPhase != 0), is the elbow actually
# saturated (|actElbow| ~ 1) while the palm is NOT in contact on the same frame?
# If yes, the chain is over-driving a contact it cannot hold, and the joint that
# holds that contact (the wrist, ceiling 10 N.m against the elbow's 120) is the
# candidate weak link - that is the lever this cycle lands. If the elbow is
# nowhere near saturation during the press, the wrist ceiling is not the binding
# constraint and the lever is wrong.
#
# Usage: python Tools/arm-wrist-lever-check.py p_arms70 p_arms71
import sys, os

RUNS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Trials", "Runs")


def load(label):
    path = os.path.join(RUNS, label + ".csv")
    with open(path, "r") as f:
        hdr = f.readline().rstrip("\n").rstrip("\r").split(";")
    idx = {name: i for i, name in enumerate(hdr)}
    rows = []
    with open(path, "r") as f:
        f.readline()
        for line in f:
            parts = line.rstrip("\n").rstrip("\r").split(";")
            if len(parts) != len(hdr):
                continue
            rows.append(parts)
    return hdr, idx, rows


def num(parts, idx, name, default=0.0):
    try:
        return float(parts[idx[name]])
    except (KeyError, ValueError):
        return default


def main():
    on = sys.argv[1] if len(sys.argv) > 1 else "p_arms70"
    off = sys.argv[2] if len(sys.argv) > 2 else "p_arms71"
    for label in (on, off):
        hdr, idx, rows = load(label)
        print("=" * 78)
        print("%s  rows=%d  cols=%d" % (label, len(rows), len(hdr)))
        missing = [c for c in ("wristAngle", "armWrist", "wristTorque")
                   if c not in idx]
        print("  wrist columns present in the record: %s"
              % ("none" if len(missing) == 3 else
                 [c for c in hdr if "wrist" in c.lower()]))
        down_row = next((i for i, r in enumerate(rows) if num(r, idx, "down") > 0.5), None)
        if down_row is None:
            print("  no down edge")
            continue
        print("  down edge: row %d  t=%.3f s" % (down_row, num(rows[down_row], idx, "time")))
        win = rows[down_row:]
        for side in ("L", "R"):
            ph = "armPressPhase" + side
            press = [r for r in win if num(r, idx, ph) > 0]
            stroke = [r for r in press]
            if not stroke:
                print("  side %s: no press frames" % side)
                continue
            act = [abs(num(r, idx, "actElbow" + side)) for r in stroke]
            sat = sum(1 for a in act if a >= 0.95)
            contact = sum(1 for r in stroke if num(r, idx, "handContact" + side) > 0.5)
            err = [abs(num(r, idx, "elbowAngle" + side) - num(r, idx, "armTargetElbow" + side))
                   for r in stroke]
            palm = [num(r, idx, "handY" + side) - num(r, idx, "armPalmY" + side) for r in stroke]
            ext = max(num(r, idx, "armStrokeExt" + side) for r in win)
            print("  side %s: press frames %d  of window %d" % (side, len(stroke), len(win)))
            print("      |actElbow| mean %.3f  saturated(>=0.95) %d/%d = %.3f"
                  % (sum(act) / len(act), sat, len(act), sat / len(act)))
            print("      palm in contact %d/%d = %.4f" % (contact, len(stroke), contact / len(stroke)))
            print("      |elbowAngle - armTargetElbow| mean %.2f deg  max %.2f deg"
                  % (sum(err) / len(err), max(err)))
            print("      handY - armPalmY mean %+.4f m  min %+.4f  max %+.4f"
                  % (sum(palm) / len(palm), min(palm), max(palm)))
            print("      run stroke extension %s = %.2f deg" % (side, ext))
        travel = max(abs(num(r, idx, "comX") - num(rows[down_row], idx, "comX")) for r in win)
        print("  window travel along the slab |comX - comX(down)| max = %.4f m" % travel)


if __name__ == "__main__":
    main()
