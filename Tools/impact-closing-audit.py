#!/usr/bin/env python3
"""Closing-speed audit: is `closingSpeedMps` the speed the object actually arrived at?

    python Tools/impact-closing-audit.py <label> [...]

`closingSpeedMps` is `collision.relativeVelocity.magnitude` at the contact
(`Assets/ImpactThrower.cs:464`) and the caption prints it next to the delivered impulse.
This script puts it side by side with the plan's own launch speed and the delivered
impulse, so a contradiction (a 4 m/s throw reported as closing at 0.06 m/s) is visible
as a number and not as a hunch.
"""
import json
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "Trials", "Runs")


def main():
    print("| label | shape | mass kg | planned m/s | launch m/s | closing m/s | "
          "closing/launch | part hit | ref N*s | delivered N*s | delivered/ref |")
    for label in sys.argv[1:]:
        with open(os.path.join(RUNS, label + "-impacts.json"), encoding="utf-8-sig") as handle:
            doc = json.load(handle)
        throw = doc["throws"][0]
        launch = throw["launchSpeedMps"]
        closing = throw["closingSpeedMps"]
        print("| {label} | {shape} | {mass:g} | {planned:g} | {launch:.3f} | {closing:.3f} | "
              "{ratio} | {part} | {ref:.0f} | {delivered:.3f} | {frac:.2f} |".format(
                  label=label, shape=throw["shape"], mass=throw["massKg"],
                  planned=throw["speedMps"], launch=launch, closing=closing,
                  ratio=f"{closing / launch:.3f}" if launch else "-",
                  part=throw["contactPart"], ref=throw["referenceImpulseNs"],
                  delivered=throw["deliveredImpulseNs"],
                  frac=throw["deliveredImpulseNs"] / throw["referenceImpulseNs"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
