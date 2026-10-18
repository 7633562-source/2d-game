#!/usr/bin/env python
"""Is `elbowAngleL` a different signal from `elbowAngleR`, or one shared value?

Why this exists: the arm brief asks for counter phase between the two elbows and
quotes `corr(elbowL, elbowR) = +0.961` on p_arms2, with the explicit instruction
to find out FIRST whether the two columns are one shared number (a shared signal,
or a mirrored joint whose sign is lost) before adding a sign to the drive - a sign
on an already-shared signal cannot produce counter phase.

The question is decidable from the records alone, no build and no run:

  * one shared value differs in 0 frames;
  * a mirror whose sign is lost differs in every frame but reads +1.000 against
    its own source column;
  * two real joints differ in nearly every frame, and their correlation is not
    +1: whatever is left of it is coupling, not identity.

It also prints, per window, the MEASURED joints next to their DRIVES, because a
drive that is counter phase while the joint is not means the joint is not
following its drive (and a common sign flip on the two drives cannot change
corr(driveL, driveR) at all - the check that the brief's warning is about).

Read-only: opens the CSVs, prints, edits nothing and launches nothing.

Usage:
    python Tools/arm-elbow-shared-check.py              # the labels below
    python Tools/arm-elbow-shared-check.py p_arms2 p_arms81
"""
import math
import os
import sys

RUNS_DIR = os.path.join("Trials", "Runs")
DEFAULT_LABELS = ["p_arms2", "p_arms15", "p_arms50", "p_arms81", "p_arms82", "p_arms_stance"]
REQUIRED = ["time", "hipL", "hipR", "elbowAngleL", "elbowAngleR"]
OPTIONAL = ["shoulderAngleL", "shoulderAngleR",
            "armTargetElbowL", "armTargetElbowR",
            "armTargetShoulderL", "armTargetShoulderR",
            "armPhaseL", "armPhaseR", "armSwingLevel",
            "armPressPhaseL", "armPressPhaseR"]
# (start, end, name): the campaign's own windows
WINDOWS = [(None, None, "whole run"), (0.1, 7.3, "walk 0.1-7.3"), (5.0, 6.63, "accepted 5.00-6.63")]


def corr(xs, ys):
    n = len(xs)
    if n < 30:
        return float("nan")
    mx = sum(xs) / n
    my = sum(ys) / n
    sxy = sum((a - mx) * (b - my) for a, b in zip(xs, ys))
    sxx = sum((a - mx) ** 2 for a in xs)
    syy = sum((b - my) ** 2 for b in ys)
    d = math.sqrt(sxx * syy)
    return sxy / d if d > 1e-12 else float("nan")


def load(path):
    lines = open(path, encoding="utf-8", errors="ignore").read().splitlines()
    if not lines:
        return None, None, ["empty file"]
    hdr = lines[0].split(";")
    idx = {h: i for i, h in enumerate(hdr)}
    miss = [c for c in REQUIRED if c not in idx]
    if miss:
        return None, None, miss
    cols = [c for c in REQUIRED + OPTIONAL if c in idx]
    col = {c: [] for c in cols}
    for line in lines[1:]:
        f = line.split(";")
        if len(f) < len(hdr):
            continue
        for c in cols:
            try:
                col[c].append(float(f[idx[c]]))
            except ValueError:
                col[c].append(float("nan"))
    return col, cols, []


def identity(label, col):
    """The brief's question, frame by frame."""
    print("%-14s frames=%d  span=%.2f..%.2f s" % (label, len(col["time"]), col["time"][0], col["time"][-1]))
    for a, b in [("elbowAngleL", "elbowAngleR"), ("shoulderAngleL", "shoulderAngleR"),
                 ("armTargetElbowL", "armTargetElbowR"), ("armTargetShoulderL", "armTargetShoulderR")]:
        if a not in col:
            print("   %-20s vs %-20s not logged in this record" % (a, b))
            continue
        d = [abs(y - x) for x, y in zip(col[a], col[b])]
        same = sum(1 for x in d if x <= 1e-6)
        rms = math.sqrt(sum(x * x for x in d) / len(d))
        print("   %-20s vs %-20s max|d|=%9.6f rms|d|=%9.6f identical=%d/%d corr=%+.3f"
              % (a, b, max(d), rms, same, len(d), corr(col[a], col[b])))


def fit_slope(xs, ys):
    """Least-squares slope/R2 of y on x: the sign of a drive read off the record."""
    n = len(xs)
    if n < 30:
        return float("nan"), float("nan")
    mx = sum(xs) / n
    my = sum(ys) / n
    sxy = sum((a - mx) * (b - my) for a, b in zip(xs, ys))
    sxx = sum((a - mx) ** 2 for a in xs)
    syy = sum((b - my) ** 2 for b in ys)
    if sxx < 1e-12 or syy < 1e-12:
        return float("nan"), float("nan")
    slope = sxy / sxx
    r2 = (sxy * sxy) / (sxx * syy)
    return slope, r2


def windows(label, col):
    t = col["time"]
    for a, b, name in WINDOWS:
        ii = range(len(t)) if a is None else [i for i in range(len(t)) if a <= t[i] <= b]
        ii = list(ii)
        if len(ii) < 30:
            print("   %-18s n=%d (window not reached)" % (name, len(ii)))
            continue
        g = lambda c: [col[c][i] for i in ii]
        row = "   %-18s n=%-5d" % (name, len(ii))
        row += "  meas elbL/elbR %+.3f" % corr(g("elbowAngleL"), g("elbowAngleR"))
        if "shoulderAngleL" in col:
            row += "  shL/shR %+.3f" % corr(g("shoulderAngleL"), g("shoulderAngleR"))
        row += "  elbL-hipR %+.3f  elbR-hipL %+.3f" % (corr(g("elbowAngleL"), g("hipR")),
                                                       corr(g("elbowAngleR"), g("hipL")))
        if "shoulderAngleL" in col:
            row += "  shL-hipR %+.3f  shR-hipL %+.3f" % (corr(g("shoulderAngleL"), g("hipR")),
                                                         corr(g("shoulderAngleR"), g("hipL")))
        print(row)
        if "armTargetElbowL" in col:
            row = "   %-18s drive" % ""
            row += "  tgtElbL/tgtElbR %+.3f" % corr(g("armTargetElbowL"), g("armTargetElbowR"))
            row += "  tgtShL/tgtShR %+.3f" % corr(g("armTargetShoulderL"), g("armTargetShoulderR"))
            row += "  tgtElbL-hipR %+.3f  tgtShL-hipR %+.3f" % (corr(g("armTargetElbowL"), g("hipR")),
                                                                corr(g("armTargetShoulderL"), g("hipR")))
            print(row)
        if "armPhaseL" in col:
            # The walk law is linear in the side's own phase, so its SIGN is the
            # slope of the target on the phase: +walkShoulderSwing for the
            # shoulder, and whatever the elbow line carries.
            slSh, r2Sh = fit_slope(g("armPhaseL"), g("armTargetShoulderL"))
            slEl, r2El = fit_slope(g("armPhaseL"), g("armTargetElbowL"))
            slPh = corr(g("hipL"), g("hipR"))
            print("   %-18s phase  phL/phR %+.3f  phL-hipL %+.3f  phL-hipR %+.3f  swing>0.5 in %d%%"
                  % ("", corr(g("armPhaseL"), g("armPhaseR")), corr(g("armPhaseL"), g("hipL")),
                     corr(g("armPhaseL"), g("hipR")),
                     int(100.0 * sum(1 for i in ii if col["armSwingLevel"][i] > 0.5) / len(ii))
                     if "armSwingLevel" in col else -1))
            print("   %-18s slope  tgtShL/phase %+8.3f (R2 %.3f)   tgtElbL/phase %+8.3f (R2 %.3f)   hipL/hipR %+.3f"
                  % ("", slSh, r2Sh, slEl, r2El, slPh))


def main(argv):
    print("QUESTION: are the two elbow columns one shared value, or two joints?")
    print("A shared value differs in 0 frames; a lost-sign mirror reads +1.000 against its")
    print("own source. Two real joints differ in nearly every frame.\n")
    for lab in (argv or DEFAULT_LABELS):
        path = os.path.join(RUNS_DIR, lab + ".csv")
        if not os.path.exists(path):
            print("%-14s missing %s" % (lab, path))
            continue
        col, cols, miss = load(path)
        if col is None:
            print("%-14s no such columns: %s" % (lab, ", ".join(miss)))
            continue
        identity(lab, col)
        windows(lab, col)
        print("")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
