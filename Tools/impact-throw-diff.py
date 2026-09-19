#!/usr/bin/env python3
"""Diff throws[0] between two impact runs (the video take vs the -nographics run).

    python Tools/impact-throw-diff.py Trials/Runs/grid_circle_0p5_20.json Trials/Runs/imp_g1L.json

Prints every key of the first throw that differs, so the claim "the visual take carries
the same physics as the headless run" is measured and not assumed.
"""
import json
import sys


def main():
    a_path, b_path = sys.argv[1], sys.argv[2]
    with open(a_path, encoding="utf-8-sig") as handle:
        a = json.load(handle)
    with open(b_path, encoding="utf-8-sig") as handle:
        b = json.load(handle)
    rows_a = a["throws"][0]
    rows_b = b["throws"][0]
    keys = sorted(set(rows_a) | set(rows_b))
    print(f"# {a_path}  ({a['label']})")
    print(f"# {b_path}  ({b['label']})")
    diffs = 0
    for key in keys:
        va, vb = rows_a.get(key), rows_b.get(key)
        if va != vb:
            diffs += 1
            print(f"DIFF {key}: {va!r} -> {vb!r}")
    print(f"# throw0 keys compared: {len(keys)}; differing: {diffs}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
