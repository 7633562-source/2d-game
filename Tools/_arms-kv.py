#!/usr/bin/env python
"""Dump run-level keys of Trials/Runs/<label>.json, filtered by substring.

Usage: _arms-kv.py <label> [<label> ...] [--grep pat] [--csv]

Prints every key of the run block whose name contains one of the arm/press
patterns, so a cycle can read the press numbers without opening the JSON by
hand. Keys come from the block the trial recorder wrote (``applied``), falling
back to the whole document when that block is absent.
"""
import json
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Trials", "Runs")
PATTERNS = ("press", "hand", "palm", "stroke", "arm", "elbow", "shoulder", "rise")


def load(label):
    path = os.path.join(RUNS, label + ".json")
    with open(path, encoding="utf-8-sig") as f:
        doc = json.load(f)
    for key in ("applied", "run"):
        if isinstance(doc.get(key), dict):
            return doc[key]
    return doc


def main():
    argv = sys.argv[1:]
    grep = None
    if "--grep" in argv:
        i = argv.index("--grep")
        grep = argv[i + 1].lower()
        del argv[i:i + 2]
    args = [a for a in argv if not a.startswith("--")]
    for label in args:
        try:
            run = load(label)
        except Exception as exc:  # noqa: BLE001 - a missing label must not stop the sweep
            print("== %s  UNREADABLE (%s)" % (label, exc))
            continue
        print("== %s  (%d run keys)" % (label, len(run)))
        for k in sorted(run):
            lk = k.lower()
            if grep is not None:
                if grep not in lk:
                    continue
            elif not any(p in lk for p in PATTERNS):
                continue
            print("    %-34s %s" % (k, run[k]))


main()
