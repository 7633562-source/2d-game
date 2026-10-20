#!/usr/bin/env python
"""Structure dump of Trials/Runs/<label>.json: top-level keys and block sizes.

Usage: _arms-dump.py <label> [<label> ...] [--key <name>]
"""
import json
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Trials", "Runs")


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    show = None
    if "--key" in sys.argv:
        show = sys.argv[sys.argv.index("--key") + 1]
    for label in args:
        path = os.path.join(RUNS, label + ".json")
        with open(path, encoding="utf-8-sig") as f:
            doc = json.load(f)
            f.seek(0)
            head = f.read(400)
        print("== %s" % label)
        for k, v in doc.items():
            if isinstance(v, dict):
                print("   %-24s dict(%d)" % (k, len(v)))
            elif isinstance(v, list):
                print("   %-24s list(%d)" % (k, len(v)))
            else:
                print("   %-24s %s" % (k, v))
        if show and isinstance(doc.get(show), dict):
            for k, v in sorted(doc[show].items()):
                print("      %-32s %s" % (k, v))


main()
