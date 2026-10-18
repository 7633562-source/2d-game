#!/usr/bin/env python
"""Recompute the arm counter-phase pairings from stored trial records.

Why this exists: ArmDrive.cs quotes pairing numbers as if they were measured
("shL-hipR -0.658, shR-hipL -0.418 on p_arms50"), and a default was added to the
shared tree on the strength of such a quote. A quoted number that does not come
back out of the record it cites is a defect of the same class as an unparseable
record: it costs the next cycle a build and a trial before anyone notices.

The published definition, pinned on Trials/Runs/arms4-report.md's capture-walk row
(+0.138 / -0.379, 5.00-6.63 s) and reproduced by p_arms44:

    shL-hipR = corr(shoulderAngleL, hipR)      shR-hipL = corr(shoulderAngleR, hipL)

Read-only: opens CSVs, prints a table, edits nothing and launches nothing.

Usage:
    python Tools/arm-pairing-check.py                 # the recent p_arms runs
    python Tools/arm-pairing-check.py p_arms50 p_arms61
"""
import glob
import hashlib
import math
import os
import re
import sys

RUNS_DIR = os.path.join("Trials", "Runs")
# (start, end, name) in seconds. The first two are the windows the arm campaign
# itself reports; the pre-fall window is the whole walk before the body goes down.
WINDOWS = [(0.1, 7.3, "pre-fall"), (5.0, 6.63, "accepted 5.00-6.63"), (5.0, 5.78, "report 5.00-5.78")]
COLS = ["time", "down", "hipL", "hipR",
        "shoulderAngleL", "shoulderAngleR",                       # measured joints
        "armTargetShoulderL", "armTargetShoulderR"]               # the drives (a different thing)
HASH_COLS = ["time", "shoulderAngleL", "shoulderAngleR", "hipL", "hipR",
             "armTargetShoulderL", "armTargetShoulderR"]


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
    hdr = lines[0].split(";")
    idx = {h: i for i, h in enumerate(hdr)}
    if any(c not in idx for c in COLS):
        return None, None, None
    col = {c: [] for c in COLS}
    for line in lines[1:]:
        f = line.split(";")
        if len(f) < len(hdr):
            continue
        for c in COLS:
            try:
                col[c].append(float(f[idx[c]]))
            except ValueError:
                col[c].append(float("nan"))
    return col, idx, hdr


def walk_hash(lines, hdr, idx):
    """MD5 of the walk-phase (t <= 7.0 s) rows of the columns that matter.

    Identical hashes mean the runs are ONE trajectory: any pairing statement drawn
    across them is a single sample, not a sweep.
    """
    acc = []
    for line in lines[1:]:
        f = line.split(";")
        if len(f) < len(hdr):
            continue
        try:
            if float(f[idx["time"]]) > 7.0:
                break
        except ValueError:
            continue
        acc.append(",".join(f[idx[c]] for c in HASH_COLS))
    return hashlib.md5("\n".join(acc).encode()).hexdigest()[:12], len(acc)


def report(label, path):
    lines = open(path, encoding="utf-8", errors="ignore").read().splitlines()
    hdr = lines[0].split(";")
    col, idx, _ = load(path)
    if col is None:
        print("%-12s no arm/hip columns" % label)
        return None
    t = col["time"]
    h, nrows = walk_hash(lines, hdr, idx)
    print("%-12s %6.2f s  walk-phase md5 %s (%d rows)" % (label, t[-1], h, nrows))
    for a, b, name in WINDOWS:
        ii = [i for i in range(len(t)) if a <= t[i] <= b]
        if len(ii) < 30:
            print("    %-18s n=%d  (window not reached)" % (name, len(ii)))
            continue
        g = lambda c: [col[c][i] for i in ii]
        print("    %-18s n=%-5d measured %+.3f/%+.3f   drive %+.3f/%+.3f"
              % (name, len(ii),
                 corr(g("shoulderAngleL"), g("hipR")), corr(g("shoulderAngleR"), g("hipL")),
                 corr(g("armTargetShoulderL"), g("hipR")), corr(g("armTargetShoulderR"), g("hipL"))))
    return h


def main(argv):
    if argv:
        labels = argv
    else:
        got = sorted(glob.glob(os.path.join(RUNS_DIR, "p_arms*.csv")),
                     key=lambda p: int(re.search(r"p_arms(\d+)", p).group(1))
                     if re.search(r"p_arms(\d+)", p) else -1)
        labels = [os.path.basename(p)[:-4] for p in got][-10:]
    print("target of the campaign's brief: both measured pairings below -0.5")
    hashes = {}
    for lab in labels:
        p = os.path.join(RUNS_DIR, lab + ".csv")
        if not os.path.exists(p):
            print("%-12s missing %s" % (lab, p))
            continue
        h = report(lab, p)
        hashes.setdefault(h, []).append(lab)
    shared = {h: v for h, v in hashes.items() if h and len(v) > 1}
    if shared:
        print("\nNOT INDEPENDENT - one trajectory, several labels:")
        for h, v in shared.items():
            print("    %s : %s" % (h, ", ".join(v)))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
