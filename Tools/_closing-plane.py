#!/usr/bin/env python3
"""In-plane / steps / outcome sweep over the closing cycle's runs (report data)."""
import json
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "Trials", "Runs")


def main():
    print("| label | contactPart | closing new | engine | delivered N*s | steps | outOfPlane | maxAbsZ | fell | t to fall s | peak CoM m | min margin m |")
    print("|---|---|---|---|---|---|---|---|---|---|---|---|")
    for label in sys.argv[1:]:
        with open(os.path.join(RUNS, label + "-impacts.json"), encoding="utf-8-sig") as handle:
            doc = json.load(handle)
        row = doc["throws"][0]
        print("| %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s |" % (
            label, row.get("contactPart"), row.get("closingSpeedMps"),
            row.get("engineRelativeSpeedMps"), row.get("deliveredImpulseNs"),
            row.get("stepsAfterImpact"), row.get("outOfPlane"), row.get("maxAbsZMeters"),
            row.get("fell"), row.get("timeToFallSeconds"), row.get("peakComExcursionM"),
            row.get("minSupportMarginM")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
