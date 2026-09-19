#!/usr/bin/env python3
"""Print the applied argv of two runs side by side (why two runs are not the same recipe)."""
import json
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "Trials", "Runs")


def main():
    for label in sys.argv[1:]:
        with open(os.path.join(RUNS, label + ".json"), encoding="utf-8-sig") as handle:
            doc = json.load(handle)
        args = doc.get("commandArgs") or doc.get("inputs", {}).get("commandArgs") or []
        print("%s (%d args)" % (label, len(args)))
        pairs = []
        i = 0
        while i < len(args):
            token = args[i]
            if token.startswith("-") and i + 1 < len(args) and not args[i + 1].startswith("-"):
                pairs.append((token, args[i + 1]))
                i += 2
            else:
                pairs.append((token, ""))
                i += 1
        left = pairs
        print("   " + "  ".join("%s%s" % (k, (" " + v) if v else "") for k, v in left))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
