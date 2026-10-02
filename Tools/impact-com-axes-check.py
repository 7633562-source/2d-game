#!/usr/bin/env python3
"""BALANCE-ARENA-1: are the record's CoM numbers visible in the run's OWN trace?

    python Tools/impact-com-axes-check.py [label ...]

Evidence #8 (section 6) left this lane a job: `peakComExcursionM` / `peakComDropM` moved
between two builds while `Tools/impact-trace-diff.py` reported "0 differing" on the frame
trace it compares - the 217-column `TrialRecorder` trace, which carries `comX`/`comOffset`
but no vertical CoM.  The conclusion drawn there was "publish the vertical CoM".

This tool tests that conclusion before anybody edits another lane's recorder.  The impact
run writes TWO traces: `<label>.csv` (TrialRecorder, 217 columns) and `<label>-impacts.csv`
(Assets/ImpactRecorder.cs, 31 columns, `;`-delimited) - and the impact recorder's own trace
already carries `comX` AND `comY`, the exact pair `Accumulate()` turns into the excursion and
the drop (`Assets/ImpactRecorder.cs:580-585`).

So: recompute both peaks from the trace and compare against the record, per throw.

  * the contact frame is the frame where the window opened - for the first throw that is the
    first row with `contacted == 1`, whose `comX`/`comY` are the record's own
    `comXAtContact`/`comYAtContact` (row written in the same Update, `:341-342`);
  * for a later throw the row nearest its `contactTime` is used (a fixed step is 5 ms);
  * peaks are taken over `[contactTime, contactTime + windowSeconds]`, exactly like
    `Accumulate()`.

Prints the residual in metres.  A residual at the trace's own quantisation (the CSV is
written with 6 decimals, `F()`) means record and trace agree; a residual of the size of one
frame of CoM motion means the frame is off by one and the numbers still describe the same
event.  Read-only.
"""
from __future__ import annotations

import csv
import json
import os
import sys

PROJECT = r"E:/Games/2D Game/My project"
# IMPACT_RUNS lets the checker be pointed at a superseded set (Trials/Runs/impact-pre-...)
# without moving anything: the archive holds the same file names.
RUNS = os.environ.get("IMPACT_RUNS") or os.path.join(PROJECT, "Trials", "Runs")
DEFAULT = ["imp_cb_quiet", "imp_cb1", "imp_cb2", "imp_cb3", "imp_cb1L", "imp_cb2L", "imp_cb3L"]


def load_trace(label):
    path = os.path.join(RUNS, label + "-impacts.csv")
    with open(path, newline="", encoding="utf-8-sig") as handle:
        reader = csv.DictReader(handle, delimiter=";")
        rows = []
        for row in reader:
            try:
                rows.append({
                    "time": float(row["time"]),
                    "comX": float(row["comX"]),
                    "comY": float(row["comY"]),
                    "supportMargin": float(row["supportMargin"]),
                    "contacted": int(float(row["contacted"])),
                })
            except (KeyError, ValueError):
                continue
    return rows


def nearest(rows, time, first_contacted=False):
    if first_contacted:
        for row in rows:
            if row["contacted"] == 1:
                return row
        return None
    best = None
    for row in rows:
        delta = abs(row["time"] - time)
        if best is None or delta < best[0]:
            best = (delta, row)
    return best[1] if best else None


def check(label):
    record_path = os.path.join(RUNS, label + "-impacts.json")
    with open(record_path, encoding="utf-8") as handle:
        doc = json.load(handle)
    throws = doc.get("throws") or []
    window = float(doc.get("windowSeconds") or 0.0)
    if window <= 0.0:
        window = 6.0
    trace = load_trace(label)
    print("== %s  throws=%d  trace rows=%d  window=%.1f s"
          % (label, len(throws), len(trace), window))
    if not throws:
        print("   (no throw: nothing to recompute - the stand is the rule-zero control)")
        return 0
    problems = 0
    for index, throw in enumerate(throws):
        contact_time = float(throw.get("contactTime") or 0.0)
        row = nearest(trace, contact_time, first_contacted=(index == 0))
        if row is None:
            print("   throw %d: no contact frame in the trace" % index)
            problems += 1
            continue
        inwin = [r for r in trace
                 if contact_time - 1e-9 <= r["time"] <= contact_time + window + 1e-9]
        if not inwin:
            print("   throw %d: empty window" % index)
            problems += 1
            continue
        excursion = max(abs(r["comX"] - row["comX"]) for r in inwin)
        drop = max(row["comY"] - r["comY"] for r in inwin)
        margin = min(r["supportMargin"] for r in inwin)
        rec_exc = float(throw.get("peakComExcursionM") or 0.0)
        rec_drop = float(throw.get("peakComDropM") or 0.0)
        rec_margin = float(throw.get("minSupportMarginM") or 0.0)
        print("   throw %d  contact %.6f s  frame %.6f s (dt %+.3f ms)  frames in window %d"
              % (index, contact_time, row["time"], (row["time"] - contact_time) * 1000.0,
                 len(inwin)))
        print("      peakComExcursionM  record %12.6f  trace %12.6f  residual %+.3e m"
              % (rec_exc, excursion, excursion - rec_exc))
        print("      peakComDropM       record %12.6f  trace %12.6f  residual %+.3e m"
              % (rec_drop, drop, drop - rec_drop))
        print("      minSupportMarginM  record %12.6f  trace %12.6f  residual %+.3e m"
              % (rec_margin, margin, margin - rec_margin))
        if max(abs(excursion - rec_exc), abs(drop - rec_drop)) > 0.002:
            problems += 1
    print("   -> %s" % ("OK the record's CoM pair is in the run's own trace"
                        if problems == 0 else "MISMATCH count=%d" % problems))
    return problems


def main():
    labels = sys.argv[1:] or DEFAULT
    problems = 0
    for label in labels:
        if not os.path.exists(os.path.join(RUNS, label + "-impacts.json")):
            print("== %s: no record" % label)
            continue
        problems += check(label)
    print("IMPACT-COM-AXES-CHECK %s problems=%d labels=%d"
          % ("OK" if problems == 0 else "FAIL", problems, len(labels)))
    return 0 if problems == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
