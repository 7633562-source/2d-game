#!/usr/bin/env python3
"""Push-back column audit for the grid runs.

The card asks for "whether the rig pushed back at all (vertical force above body
weight after the impact, with the frame number where it happens)". This script checks
the *after* part on the runs' own rows: contactTime, pushTime and the sign of
(pushTime - contactTime) for every grid run.
"""
import json
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "Trials", "Runs")


def main():
    labels = sys.argv[1:]
    print("| label | contact s | first contact part | pushTime s | push-contact s | "
          "pushedBack | peakFz/W | groundPush N*s |")
    for label in labels:
        path = os.path.join(RUNS, label + "-impacts.json")
        with open(path, encoding="utf-8-sig") as handle:
            doc = json.load(handle)
        throw = doc["throws"][0]
        contact = throw["contactTime"]
        push = throw.get("pushTime", -1.0)
        print("| {label} | {t:.3f} | {part} | {push:.3f} | {delta:+.3f} | {flag} | "
              "{fz:.3f} | {imp:.3f} |".format(
                  label=label, t=contact, part=throw["contactPart"], push=push,
                  delta=push - contact, flag=throw["pushedBack"],
                  fz=throw["peakFzOverWeight"],
                  imp=throw.get("groundPushImpulseNs", float("nan"))))
    return 0


if __name__ == "__main__":
    sys.exit(main())
