#!/usr/bin/env python3
"""BALANCE-ARENA-1 grid table: read the grid runs' own -impacts.json into one table.

    python Tools/impact-grid-table.py grid_circle_0p5_4 grid_square_15_20 ...

Each argument is a run label in Trials/Runs. Prints one markdown row per run plus a
per-class verdict line, so the report's table is generated from the JSONs and not
typed by hand.
"""
import json
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "Trials", "Runs")


def num(value, digits=3, dash="-"):
    if value is None:
        return dash
    if isinstance(value, bool):
        return "yes" if value else "no"
    if isinstance(value, (int, float)):
        if value != value:            # NaN
            return "NaN"
        return f"{value:.{digits}f}"
    return str(value)


def main():
    labels = sys.argv[1:]
    if not labels:
        print("usage: impact-grid-table.py <label> [label ...]")
        return 2
    header = ("| label | shape | size m | mass kg | speed m/s | ref N*s | part hit | "
              "delivered N*s | % | vert N*s | fell | fall s | CoM m | margin m | "
              "steps | push-back | push t | fz/W | L1 passive ms | L1 active ms | "
              "hip N*m | knee N*m | ankle N*m | limit frac |")
    print(header)
    print("|" + "---|" * 25)
    for label in labels:
        path = os.path.join(RUNS, label + "-impacts.json")
        if not os.path.exists(path):
            print(f"| {label} | MISSING |")
            continue
        with open(path, encoding="utf-8-sig") as handle:
            doc = json.load(handle)
        throw = doc["throws"][0]
        size = throw["sizeMeters"]
        if throw["shape"] == "rect":
            size = f'{throw["sizeMeters"]}x{throw["sizeHeightMeters"]}'
        ref = throw["referenceImpulseNs"]
        delivered = throw["deliveredImpulseNs"]
        peak = throw.get("peakTorqueNm", {})
        frac = throw.get("limitHitFraction", {})
        worst_frac = max(frac.values()) if frac else 0.0
        print("| {label} | {shape} | {size} | {mass} | {speed} | {ref} | {part} | "
              "{delivered} | {pct} | {vert} | {fell} | {fall} | {com} | {margin} | "
              "{steps} | {push} | {pusht} | {fzw} | {l1p} | {l1a} | {hip} | {knee} | "
              "{ankle} | {worst} |".format(
                  label=label,
                  shape=throw["shape"],
                  size=size,
                  mass=num(throw["massKg"], 2),
                  speed=num(throw["speedMps"], 2),
                  ref=num(ref, 2),
                  part=throw["contactPart"],
                  delivered=num(delivered, 3),
                  pct=(num(100.0 * delivered / ref, 0) + " %") if ref else "-",
                  vert=num(throw["deliveredVerticalImpulseNs"], 3),
                  fell=num(throw["fell"]),
                  fall=num(throw["timeToFallSeconds"], 3),
                  com=num(throw["peakComExcursionM"], 3),
                  margin=num(throw["minSupportMarginM"], 3),
                  steps=throw["stepsAfterImpact"],
                  push=num(throw["pushedBack"]),
                  pusht=num(throw.get("pushTime", -1), 3),
                  fzw=num(throw["peakFzOverWeight"], 3),
                  l1p=num(throw["latencyPassiveMs"], 3),
                  l1a=num(throw["latencyActiveMs"], 3),
                  hip=num(max(peak.get("hipL", 0), peak.get("hipR", 0)), 2),
                  knee=num(max(peak.get("kneeL", 0), peak.get("kneeR", 0)), 2),
                  ankle=num(max(peak.get("ankleL", 0), peak.get("ankleR", 0)), 2),
                  worst=num(worst_frac, 3)))
        print(f"    # {label}: switches={json.dumps(doc.get('switches'))} "
              f"buildGuid={doc.get('buildGuid')} fp={doc.get('sourceFingerprint', '')[:12]} "
              f"sim={num(doc.get('simulatedSeconds'), 3)}s "
              f"criterionFall={num(doc.get('fallCriterionTime'), 3)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
