#!/usr/bin/env python3
"""Field-by-field diff of one species capture against another (level lane, 20.09).

    python Tools/level-species-json-diff.py <a.json> <b.json>

Written for the ART-0 28th pass cycle: every one-species scene's measured box moved on the
new preview player, and the question is which FIELD carries the move. Read-only.
"""
import json
import sys


def load(path):
    with open(path, encoding="utf-8-sig") as handle:
        return json.load(handle)


def main(argv):
    if len(argv) != 2:
        print(__doc__)
        return 2
    a, b = load(argv[0]), load(argv[1])
    print("# %s" % argv[0])
    print("# %s" % argv[1])
    moved = 0
    for key in sorted(set(a) | set(b)):
        if a.get(key) == b.get(key):
            continue
        moved += 1
        print("%-30s %-58s -> %s" % (key, str(a.get(key))[:58], str(b.get(key))[:58]))
    print("# moved fields: %d of %d" % (moved, len(set(a) | set(b))))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
