#!/usr/bin/env python3
"""Read a tilt-stand take: the per-step log + the summary's tilt section.

usage: python Tools/tilt-stand-read.py <label> [<label> ...]
       (labels are under Trials/Runs/, without the .csv)

Prints, per take: the fall line, the per-step table, and the channels row.
Read-only; the take's own files are never modified.
"""
import json
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Trials", "Runs")


def read(label):
    csv_path = os.path.join(RUNS, label + "_tiltsteps.csv")
    json_path = os.path.join(RUNS, label + ".json")
    if not os.path.exists(csv_path):
        print(f"{label}: no _tiltsteps.csv (the take did not arm the stand)")
        return
    rows = []
    with open(csv_path, "r", encoding="utf-8") as fh:
        header = fh.readline().rstrip("\n").split(";")
        for line in fh:
            if line.strip():
                rows.append(dict(zip(header, line.rstrip("\n").split(";"))))
    tilt = {}
    if os.path.exists(json_path):
        with open(json_path, "r", encoding="utf-8") as fh:
            summary = json.load(fh)
        tilt = summary.get("tilt", {})
        fell = summary.get("fell")
        survived = summary.get("survivedSeconds")
        print(f"== {label}: fell={fell} survived={survived} "
              f"heldDeg={tilt.get('heldDeg')} fellStep={tilt.get('fellStep')} fellAt={tilt.get('fellAt')}")
    else:
        print(f"== {label}: (no json)")
    print("step angle held complete fell  tStart    tEnd      knee  hip   ankle arm   comOff margin air tilt  channels")
    for row in rows:
        print("{:>4} {:>5} {:>4} {:>8} {:>4} {:>8} {:>9} {:>5} {:>5} {:>5} {:>5} {:>6} {:>6} {:>3} {:>5}  {}".format(
            row["step"], row["angleDeg"], row["held"], row["complete"], row["fell"],
            row["tStart"], row["tEnd"], row["kneeDeg"], row["hipDeg"], row["ankleDeg"],
            row["armDeg"], row["comOffsetMaxM"], row["marginMinM"], row["airFrames"],
            row["tiltEndDeg"], row["channels"]))
    print()


if __name__ == "__main__":
    labels = sys.argv[1:]
    if not labels:
        print(__doc__)
        sys.exit(2)
    for lbl in labels:
        read(lbl)
