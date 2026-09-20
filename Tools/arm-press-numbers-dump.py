"""Read-only: dump the press numbers the arm brief asks about from stored p_arms* JSONs.

Cycle 13 recon. No run, no build, no physics.
"""
import json
import os
import sys

RUN = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Trials", "Runs")

KEYS = [
    "armPressStrokesL", "armPressStrokesR",
    "armPressStrokeMaxDegL", "armPressStrokeMaxDegR",
    "armPressShoulderMaxDegL", "armPressShoulderMaxDegR",
    "armPressLiftMaxMeters",
    "armPressShoulderLiftL", "armPressShoulderLiftR",
    "armPressWindowSeconds", "armPressWindowRiseL", "armPressWindowRiseR",
    "armPressWindowPelvisRise", "armPressWindowTravelX",
    "armPressWindowContactL", "armPressWindowContactR",
    "armHandContactFractionL", "armHandContactFractionR",
    "armPressPlantOffsetL", "armPressPlantOffsetR",
    "armPressAimBranchL", "armPressAimBranchR", "armPressRequest",
    "rmsShoulderJointSpeed", "elbowLimitFraction",
    "crouchBothHandsGroundedFraction",
    "cmd_line", "commandArgs",
]


def main(labels):
    for lbl in labels:
        p = os.path.join(RUN, lbl + ".json")
        if not os.path.exists(p):
            print("%-12s MISSING" % lbl)
            continue
        d = json.load(open(p, encoding="utf-8"))
        print("== %s" % lbl)
        for k in KEYS:
            if k in d:
                print("    %-32s %s" % (k, d[k]))


if __name__ == "__main__":
    main(sys.argv[1:] or ["p_arms70", "p_arms71", "p_arms81", "p_arms82"])
