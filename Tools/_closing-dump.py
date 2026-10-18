#!/usr/bin/env python3
"""Dump the throw block of impact runs (cycle: closingSpeedMps diagnosis)."""
import json
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "Trials", "Runs")
KEYS = ["throwIndex", "shape", "width", "height", "massKg", "speedMps",
        "referenceImpulseNs", "aimedPart", "launchVelocity", "contactPart",
        "contactTime", "contactMs", "closingSpeedMps", "deliveredImpulseNs",
        "deliveredImpulseAtEntryNs", "deliveredVerticalImpulseNs",
        "fallTimeAfterContactS", "comExcursionM", "minSupportMarginM"]


def main():
    for label in sys.argv[1:]:
        with open(os.path.join(RUNS, label + "-impacts.json"), encoding="utf-8-sig") as handle:
            doc = json.load(handle)
        throw = doc["throws"][0]
        print("== " + label)
        for key in KEYS:
            if key in throw:
                print("   %-28s %s" % (key, throw[key]))
        print("   all keys: " + ", ".join(throw.keys()))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
