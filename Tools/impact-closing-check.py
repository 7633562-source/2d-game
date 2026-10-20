#!/usr/bin/env python3
"""What the object was actually doing when it hit (per-frame CSV, finite differences).

    python Tools/impact-closing-check.py grid_rect_15_4

The run's CSV carries the projectile position every physics step (200 Hz), so the
projectile's own speed before and after the contact frame can be measured from its own
track - which is what `closingSpeedMps` claims to be. Prints the four frames on each
side of the contact with the finite-difference velocity, and the momentum the object
lost (m * dv), to compare with the reported delivered impulse.
"""
import csv
import json
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "Trials", "Runs")
DT = 0.005


def main():
    label = sys.argv[1]
    with open(os.path.join(RUNS, label + "-impacts.json"), encoding="utf-8-sig") as handle:
        doc = json.load(handle)
    throw = doc["throws"][0]
    mass = throw["massKg"]
    with open(os.path.join(RUNS, label + "-impacts.csv"), newline="",
              encoding="utf-8-sig") as handle:
        rows = list(csv.DictReader(handle, delimiter=";"))
    contact_index = None
    for index, row in enumerate(rows):
        if row["contacted"] == "1":
            contact_index = index
            break
    print(f"# {label}: contact frame {contact_index} at t={rows[contact_index]['time']} s, "
          f"mass {mass} kg, reported closing {throw['closingSpeedMps']:.3f} m/s, "
          f"delivered {throw['deliveredImpulseNs']:.3f} N*s")
    print("| frame | t s | projX m | projY m | vx m/s | vy m/s | |v| m/s |")
    for index in range(max(1, contact_index - 4), min(len(rows), contact_index + 5)):
        prev, cur = rows[index - 1], rows[index]
        vx = (float(cur["projectileX"]) - float(prev["projectileX"])) / DT
        vy = (float(cur["projectileY"]) - float(prev["projectileY"])) / DT
        mark = " <-- contact" if index == contact_index else ""
        print("| {i} | {t} | {x} | {y} | {vx:.3f} | {vy:.3f} | {v:.3f} |{mark}".format(
            i=index, t=cur["time"], x=cur["projectileX"], y=cur["projectileY"],
            vx=vx, vy=vy, v=(vx * vx + vy * vy) ** 0.5, mark=mark))

    def velocity(index):
        prev, cur = rows[index - 1], rows[index]
        return ((float(cur["projectileX"]) - float(prev["projectileX"])) / DT,
                (float(cur["projectileY"]) - float(prev["projectileY"])) / DT)

    before = velocity(contact_index)
    after = velocity(contact_index + 2)
    dvx, dvy = before[0] - after[0], before[1] - after[1]
    print(f"# momentum the object lost: m*|dv| = {mass * (dvx * dvx + dvy * dvy) ** 0.5:.3f} N*s "
          f"(reported delivered {throw['deliveredImpulseNs']:.3f} N*s)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
