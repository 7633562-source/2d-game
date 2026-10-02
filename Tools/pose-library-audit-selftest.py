#!/usr/bin/env python
"""Selftest for Tools/pose-library-audit.py.

A check that cannot fail is a rubber stamp: this mutates COPIES of
`Assets/PoseDriveController.cs` under the system temp directory and requires the
audit to catch each one, while the unmutated copy must still pass.

Read-only on the repository (the source is copied out before it is mutated):
it edits nothing in the tree, builds nothing, runs no trial.

Exit 0 = every case behaved, 1 = the audit missed a mutation or rejected the
unmutated table.
"""
import os
import shutil
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TOOL = os.path.join(ROOT, "Tools", "pose-library-audit.py")
SRC = os.path.join(ROOT, "Assets", "PoseDriveController.cs")
TMP = os.environ.get("LOCALAPPDATA") or os.environ.get("TEMP") or "."

CASES = [
    ("unmutated copy", None, 0),
    ("knee 130 (outside 0..120)",
     ("Leg(-30f, 68f, -10f, -14f), Leg(-6f, 10f, 4f, 2f)",
      "Leg(-30f, 130f, -10f, -14f), Leg(-6f, 10f, 4f, 2f)"), 1),
    ("hip +200 (outside -90..45)",
     ("Leg(-16f, 28f, -6f, -12f)", "Leg(200f, 28f, -6f, -12f)"), 1),
    ("drawn fold knee drifted from the report",
     ("Leg(-30f, 68f, -10f, -14f)", "Leg(-30f, 66f, -10f, -14f)"), 1),
    ("a pose renamed",
     ('Make("footplace"', 'Make("footplacement"'), 1),
]


def main():
    fail = 0
    for name, mutation, want in CASES:
        path = os.path.join(TMP, "pose-lib-selftest.cs")
        shutil.copyfile(SRC, path)
        if mutation:
            text = open(path, encoding="utf-8").read()
            old, new = mutation
            if old not in text:
                print("MUTATION TARGET NOT FOUND: %s" % old)
                fail += 1
                continue
            open(path, "w", encoding="utf-8").write(text.replace(old, new, 1))
        r = subprocess.run([sys.executable, TOOL, path],
                           capture_output=True, text=True)
        if r.returncode != want:
            fail += 1
        lines = [l for l in r.stdout.strip().splitlines() if l]
        print("%-42s rc=%d want=%d %-5s | %s"
              % (name, r.returncode, want, "OK" if r.returncode == want else "WRONG",
                 lines[-1] if lines else ""))
    print("POSE-LIBRARY-AUDIT-SELFTEST %s"
          % ("OK cases=%d" % len(CASES) if not fail else "FAILED %d" % fail))
    return 1 if fail else 0


if __name__ == "__main__":
    sys.exit(main())
