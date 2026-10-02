#!/usr/bin/env python3
"""The projectile's own track from a run's per-frame CSV (is it a flight or a drop-in?).

    python Tools/impact-projectile-track.py grid_rect_15_4 [stride]

Prints every `stride`-th row of the projectile columns plus every row with a non-zero
position change near the end, so the launch, the flight and the contact are visible as
numbers rather than assumed from `launchSpeedMps`.
"""
import csv
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "Trials", "Runs")


def main():
    label = sys.argv[1]
    stride = int(sys.argv[2]) if len(sys.argv) > 2 else 25
    with open(os.path.join(RUNS, label + "-impacts.csv"), newline="",
              encoding="utf-8-sig") as handle:
        rows = list(csv.DictReader(handle, delimiter=";"))
    moving = [row for row in rows if abs(float(row["projectileX"])) > 1e-9
              or abs(float(row["projectileY"])) > 1e-9]
    print(f"# {label}: {len(rows)} frames; projectile non-zero in {len(moving)} of them")
    if moving:
        print(f"# first moving frame: {rows.index(moving[0])} at t={moving[0]['time']} "
              f"file={rows.index(moving[0])}")
        print(f"# last frame: {len(rows) - 1} at t={rows[-1]['time']}")
    print("| frame | t s | projX | projY | distance m | contacted |")
    for index in range(0, len(rows), stride):
        row = rows[index]
        print(f"| {index} | {row['time']} | {row['projectileX']} | {row['projectileY']} | "
              f"{row['projectileDistanceM']} | {row['contacted']} |")
    tail = rows[-4 * stride:]
    print("--- tail (every 5 frames) ---")
    for index in range(len(rows) - 4 * stride, len(rows), 5):
        row = rows[index]
        print(f"| {index} | {row['time']} | {row['projectileX']} | {row['projectileY']} | "
              f"{row['projectileDistanceM']} | {row['contacted']} |")
    return 0


if __name__ == "__main__":
    sys.exit(main())
