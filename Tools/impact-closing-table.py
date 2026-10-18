#!/usr/bin/env python3
"""The closing-speed cycle's table: new measure vs the engine reading vs the twin run.

    python Tools/impact-closing-table.py <close_label> <grid_label> [...]

For every class it prints the launch speed, the new `closingSpeedMps` (measured from the
pre-step velocities of the two bodies that met), the engine's own reading of the same
contact (`engineRelativeSpeedMps`, the old value kept as the A/B witness), the accepted
`grid_*` twin's old value, the delivered impulse and the outcome, and then the key-by-key
comparison of the two runs: which shared keys moved (identity and clock fields excluded)
and which of them are the physics columns the card cares about.
"""
import json
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "Trials", "Runs")
# Fields that are identity or wall-clock by nature: they may never be compared.
IDENTITY = {"label", "sourceFingerprint", "buildGuid", "gitCommit", "gitDirty",
            "wallSeconds", "wallPerSim", "unityVersion", "startedUtc", "harness",
            "simulatedSeconds", "sampleWall"}


def load(label):
    with open(os.path.join(RUNS, label + "-impacts.json"), encoding="utf-8-sig") as handle:
        return json.load(handle)


def flatten(node, prefix="", out=None):
    if out is None:
        out = {}
    if isinstance(node, dict):
        for key, value in node.items():
            flatten(value, prefix + key + ".", out)
    elif isinstance(node, list):
        for index, value in enumerate(node):
            flatten(value, "%s%d." % (prefix, index), out)
    else:
        out[prefix[:-1]] = node
    return out


def main():
    pairs = []
    args = sys.argv[1:]
    if len(args) % 2 != 0:
        print(__doc__)
        return 2
    for i in range(0, len(args), 2):
        pairs.append((args[i], args[i + 1]))

    header = ("| class | launch m/s | closing (new) | engine witness | grid twin (old) "
              "| delivered N*s | fall | keys moved |")
    print(header)
    print("|---|---|---|---|---|---|---|---|")
    for close_label, grid_label in pairs:
        doc = load(close_label)
        twin = load(grid_label)
        throw = doc["throws"][0]
        twin_throw = twin["throws"][0]
        left, right = flatten(throw), flatten(twin_throw)
        moved = []
        for key in sorted(set(left) & set(right)):
            if key.split(".")[0] in IDENTITY:
                continue
            if left[key] != right[key]:
                moved.append(key)
        print("| %s | %s | **%s** | %s | %s | %s | %s | %d: %s |" % (
            close_label,
            throw.get("launchSpeedMps"),
            throw.get("closingSpeedMps"),
            throw.get("engineRelativeSpeedMps"),
            twin_throw.get("closingSpeedMps"),
            throw.get("deliveredImpulseNs"),
            throw.get("fell"),
            len(moved),
            ", ".join(moved) if moved else "none"))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
