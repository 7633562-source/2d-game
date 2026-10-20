#!/usr/bin/env python3
"""Per-class verdict for a grid run (one row per (shape, mass, speed) class).

    python Tools/impact-grid-verdict.py grid_circle_0p5_4 ...

Reads only the run's own `-impacts.json`. The verdict is stated from the run's numbers,
never from the printed label: a class "stands" only when the shared fall criterion did
not fire, the CoM never left the support polygon and the body came back inside 50 mm.
"""
import json
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "Trials", "Runs")


def main():
    print("| class (shape size | mass | speed) | ref N*s | delivered N*s | verdict | "
          "time to fall s | CoM excursion m | min margin m | left support | back inside s | "
          "returned to 50 mm | steps | criterion fall s | clairvoyance flag |")
    for label in sys.argv[1:]:
        with open(os.path.join(RUNS, label + "-impacts.json"), encoding="utf-8-sig") as handle:
            doc = json.load(handle)
        throw = doc["throws"][0]
        size = f'{throw["sizeMeters"]}x{throw["sizeHeightMeters"]}' \
            if throw["shape"] == "rect" else f'{throw["sizeMeters"]}'
        verdict = "STANDS" if not throw["fell"] else "DOWN"
        cls = (f'{throw["shape"]} {size} m | {throw["massKg"]:g} kg | '
               f'{throw["speedMps"]:g} m/s')
        print("| {cls} | {ref:.0f} | {delivered:.3f} | {verdict} | {fall} | {com:.3f} | "
              "{margin:.3f} | {left} | {back} | {ret} | {steps} | {crit} | {clair} |".format(
                  cls=cls, ref=throw["referenceImpulseNs"], delivered=throw["deliveredImpulseNs"],
                  verdict=verdict,
                  fall=f'{throw["timeToFallSeconds"]:.3f}' if throw["fell"] else "-",
                  com=throw["peakComExcursionM"], margin=throw["minSupportMarginM"],
                  left=throw["wentOutsideSupport"],
                  back=f'{throw["backInsideSupportSeconds"]:.3f}'
                  if throw["backInsideSupportSeconds"] >= 0 else "-",
                  ret=throw["returnedWithin50mmAtWindowEnd"], steps=throw["stepsAfterImpact"],
                  crit=f'{doc["fallCriterionTime"]:.3f}' if doc["fallCriterionTime"] > 0 else "-",
                  clair=throw["clairvoyanceFlag"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
