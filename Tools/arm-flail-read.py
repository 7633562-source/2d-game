#!/usr/bin/env python3
"""Read-only diagnosis of the arm's post-fall flail, from records that already exist.

The brief's complaint: rmsShoulderJointSpeed 67 -> 103 against a calm stand of
9.6 deg/s ("the joint flails at 10x the calm value"). This tool says WHERE the
speed lives, without building or running anything:

  * per-step |d(shoulderAngle)| / dt and the same for the elbow, RMS per window
    (walk, the fall itself, and the down window after it),
  * the same numbers for control runs (a quiet stand, and the press OFF build),
  * the press phase histogram of the frames that carry the top decile of speed,
    so "the aim is chasing the tumbling body" is told from "the stroke is
    working" and from "the arm is released and snaps".

Usage:
    python Tools/arm-flail-read.py p_arms81 --control p_arms_stance --control p_arms71
"""

import csv
import json
import os
import sys

RUNS = "Trials/Runs"
JOINTS = ["shoulderAngleL", "shoulderAngleR", "elbowAngleL", "elbowAngleR"]
PHASES = ["pressPhaseL", "pressPhaseR"]


def delimited(path):
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        head = f.readline()
    delim = ";" if head.count(";") >= head.count(",") else ","
    with open(path, "r", encoding="utf-8", errors="replace", newline="") as f:
        return list(csv.DictReader(f, delimiter=delim))


def fnum(v):
    try:
        return float(v)
    except (TypeError, ValueError):
        return None


def rms(vals):
    return (sum(v * v for v in vals) / len(vals)) ** 0.5 if vals else 0.0


def dt_of(rows):
    """Fixed step from the run's own time column, not assumed."""
    for key in ("time", "t", "simTime"):
        if rows and key in rows[0]:
            ts = [fnum(r.get(key)) for r in rows]
            ts = [t for t in ts if t is not None]
            diffs = sorted(b - a for a, b in zip(ts, ts[1:]) if b > a)
            if diffs:
                return diffs[len(diffs) // 2]
    return 0.005


def analyse(label, verbose=True):
    csv_path = os.path.join(RUNS, label + ".csv")
    json_path = os.path.join(RUNS, label + ".json")
    if not os.path.exists(csv_path):
        print("%-16s MISSING csv" % label)
        return
    rows = delimited(csv_path)
    data = json.load(open(json_path, encoding="utf-8")) if os.path.exists(json_path) else {}
    dt = dt_of(rows)
    step = {}
    for col in JOINTS:
        vals = [fnum(r.get(col)) for r in rows]
        step[col] = [
            (abs(b - a) / dt) if (a is not None and b is not None) else None
            for a, b in zip(vals, vals[1:])
        ]
    fall_t = None
    for key in ("physicalFallTime", "fallTime", "armsFallTime"):
        if isinstance(data.get(key), (int, float)) and data[key] > 0:
            fall_t = float(data[key])
            break
    n = len(rows)
    windows = [("whole", 0, n)]
    if fall_t:
        fi = min(n - 1, int(round(fall_t / dt)))
        windows += [("pre-fall", 0, fi),
                    ("fall+2s", max(0, fi - 20), min(n, fi + int(2 / dt))),
                    ("down", fi, n)]
    print("=" * 74)
    print("%s  rows=%d dt=%.5f  physicalFallTime=%s  sensorTick=%s"
          % (label, n, dt, fall_t, data.get("sensorTick")))
    if verbose:
        print("  window        " + "".join("%-14s" % c for c in JOINTS))
        for name, lo, hi in windows:
            if hi - lo < 2:
                continue
            print("  %-13s" % name + "".join(
                "%-14.2f" % rms([v for v in step[c][lo:hi] if v is not None]) for c in JOINTS))
    # Where the top decile of the shoulder speed sits, per press phase.
    sp = [v if v is not None else 0.0 for v in step["shoulderAngleL"]]
    if sp:
        cut = sorted(sp)[int(len(sp) * 0.9)]
        hist = {}
        for idx, v in enumerate(sp):
            if v < cut:
                continue
            row = rows[idx + 1]
            for ph in PHASES:
                val = fnum(row.get(ph))
                if val is not None:
                    hist[(ph, int(val))] = hist.get((ph, int(val)), 0) + 1
        top = sorted(hist.items(), key=lambda kv: -kv[1])[:8]
        print("  top-decile shoulder speed (>=%.1f deg/s) by press phase: %s"
              % (cut, ", ".join("%s=%d %d frames" % (k[0], k[1], v) for k, v in top)))


def main():
    args = sys.argv[1:]
    if not args:
        args = ["--help"]
    if args[0] == "--help":
        print(__doc__)
        return
    controls = []
    labels = []
    it = iter(args)
    for a in it:
        if a == "--control":
            controls.append(next(it, ""))
        else:
            labels.append(a)
    for lab in labels:
        analyse(lab)
    for lab in controls:
        analyse(lab)


if __name__ == "__main__":
    main()
