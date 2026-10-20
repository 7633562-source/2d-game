#!/usr/bin/env python3
"""Compare two trial-run JSONs key by key (rule-zero check for the impact grid).

    python Tools/impact-json-diff.py Trials/Runs/imp_quiet40.json Trials/Runs/imp_quiet_grid.json

Prints the card's four quiet-stand numbers from both runs and every key whose value
differs, sorted. Flattened key paths, so a nested dict cannot hide a difference.
"""
import json
import sys


def flat(doc, prefix=""):
    out = {}
    if isinstance(doc, dict):
        for key, value in doc.items():
            out.update(flat(value, f"{prefix}/{key}"))
    elif isinstance(doc, list):
        out[prefix] = f"list[{len(doc)}]"
    else:
        out[prefix] = doc
    return out


def main():
    a_path, b_path = sys.argv[1], sys.argv[2]
    with open(a_path, encoding="utf-8-sig") as handle:
        a = flat(json.load(handle))
    with open(b_path, encoding="utf-8-sig") as handle:
        b = flat(json.load(handle))

    card = ("pelvisDrop", "rmsShoulderJointSpeed", "rmsElbowJointSpeed",
            "armSwingFraction", "sensorTick", "maxAbsPelvisAccY", "wSupNewtons")
    print("card numbers:")
    for name in card:
        key = "/" + name
        print(f"  {name:22s} {a.get(key)!r:>24} -> {b.get(key)!r}")

    shared = sorted(set(a) & set(b))
    diffs = [k for k in shared if a[k] != b[k]]
    print(f"shared keys: {len(shared)}; differing: {len(diffs)}")
    for key in diffs:
        print(f"  {key:44s} {a[key]!r:>34} -> {b[key]!r}")
    only_a = sorted(set(a) - set(b))
    only_b = sorted(set(b) - set(a))
    if only_a:
        print("only in", a_path, ":", only_a[:20])
    if only_b:
        print("only in", b_path, ":", only_b[:20])
    return 0


if __name__ == "__main__":
    sys.exit(main())
