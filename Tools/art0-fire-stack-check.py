#!/usr/bin/env python3
"""ART-0: hold Campfire.cs to its one-source contract for the fire's picture.

The campfire is drawn twice - by the live pool (`TickTongues`, `BuildTongues`,
`BuildLogs`) and, frozen, by the gallery's FIRE row (`Campfire.FrozenStack`).
A catalogue that drifts from the fire it catalogues is worse than no catalogue,
so the geometry and the pose were MOVED into three shared functions
(`PoolRow`, `TonguePicture`, `LogRow`) and `FrozenStack` retypes nothing.

This reads the file and fails if that stops being true:

  1. the multiset of float literals of the whole file differs from the given
     previous version by EXACTLY the two frozen-stack constants - so the move
     changed no number anywhere in the file (the bands, the heats, the lives,
     the leans, the flicker weights, the log boxes, the ember and core boxes);
  2. `FrozenStack` contains no float literal of its own;
  3. the live methods and the frozen stack really call the same three rows.

Usage (from the project root):

    python Tools/art0-fire-stack-check.py <previousCampfire.cs> [current.cs]

Take the previous version out of git with:

    git show HEAD:Assets/Campfire.cs > <previousCampfire.cs>
"""
import collections
import os
import re
import sys

FLOAT = re.compile(r"\d+\.\d+f")
SHARED = ("PoolRow", "TonguePicture", "LogRow")


def literals(text):
    return collections.Counter(FLOAT.findall(text))


def method(text, signature):
    start = text.find(signature)
    if start < 0:
        return None
    end = text.find("\n    }\n", start)
    return text[start:end] if end < 0 else text[start:end + len("\n    }\n")]


def main():
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)

    old_path = sys.argv[1]
    new_path = sys.argv[2] if len(sys.argv) > 2 else os.path.join(
        os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets", "Campfire.cs")

    old = open(old_path, "rb").read().decode("utf-8").replace("\r\n", "\n")
    new = open(new_path, "rb").read().decode("utf-8").replace("\r\n", "\n")

    failures = 0

    a = literals(old)
    b = literals(new)
    added = b - a
    removed = a - b
    print("float literals: previous %d distinct / %d total, current %d distinct / %d total"
          % (len(a), sum(a.values()), len(b), sum(b.values())))
    print("  added   %s" % (sorted(added.items()) if added else "none"))
    print("  removed %s" % (sorted(removed.items()) if removed else "none"))

    expected = collections.Counter({"0.70f": 1, "0.50f": 1})
    if added != expected or removed:
        print("FAIL 1: the literal set moved further than the two frozen constants")
        failures += 1
    else:
        print("PASS 1: exactly FrozenStackTime 0.70f and FrozenStackFlicker 0.50f are new, "
              "no literal was removed or changed")

    stack = method(new, "    public static FirePiece[] FrozenStack()")
    if stack is None:
        print("FAIL 2: FrozenStack not found")
        failures += 1
    else:
        own = FLOAT.findall(stack)
        if own:
            print("FAIL 2: FrozenStack retypes %d float literals: %s" % (len(own), own))
            failures += 1
        else:
            print("PASS 2: FrozenStack retypes no float literal (%d chars of description only)"
                  % len(stack))
        used = [name for name in SHARED if name + "(" in stack]
        if used == list(SHARED):
            print("PASS 2b: FrozenStack reads %s" % ", ".join(used))
        else:
            print("FAIL 2b: FrozenStack reads %s, expected %s" % (used, list(SHARED)))
            failures += 1

    callers = {
        "    private void BuildTongues()": "PoolRow",
        "    private void TickTongues(float n, float n2)": "TonguePicture",
        "    private void BuildLogs()": "LogRow",
    }
    for signature, row in callers.items():
        body = method(new, signature)
        if body is None:
            print("FAIL 3: %s not found" % signature.strip())
            failures += 1
        elif row + "(" not in body:
            print("FAIL 3: %s does not call %s" % (signature.strip(), row))
            failures += 1
        else:
            print("PASS 3: %s calls %s" % (signature.strip(), row))

    for row in SHARED:
        body = method(new, "    private static void " + row + "(")
        if body is None:
            print("FAIL 4: %s not found" % row)
            failures += 1
        else:
            print("PASS 4: %s is one function, %d lines, %d float literals"
                  % (row, body.count("\n"), len(FLOAT.findall(body))))

    print("art0-fire-stack-check end failures=%d" % failures)
    return 0 if failures == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
