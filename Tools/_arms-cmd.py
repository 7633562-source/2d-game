#!/usr/bin/env python
"""Print the command line a trial run was launched with.

Usage: _arms-cmd.py <label> [<label> ...]

The line is stored in the run JSON as ``commandArgs`` (a list); this prints it
as one shell-quoted string so two labels can be compared with diff.
"""
import json
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "Trials", "Runs")


def main():
    for label in sys.argv[1:]:
        with open(os.path.join(RUNS, label + ".json"), encoding="utf-8-sig") as f:
            doc = json.load(f)
        args = doc.get("commandArgs") or []
        if isinstance(args, str):
            args = args.split()
        print("== %s" % label)
        print("   " + " ".join(args[1:]))


main()
