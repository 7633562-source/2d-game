#!/usr/bin/env python3
"""Does `Tools/impact-inplane-check.py` actually fail when a run is out of plane?

    python Tools/impact-inplane-selftest.py

The instrument this tests was written twice before it worked. Version 1 read
`<label>.json` (the physics trial record, which holds no throw at all) and printed
`OK runs=8 bad=0` while checking nothing. Version 2 read the right file but looked for the
plane fields at the top level, where they do not exist, and failed the same eight runs for
the wrong reason. A checker that passes on a broken record is worse than none, so this
selftest mutates a REAL accepted run inside `Trials/Runs` into three broken copies, runs the
check on each as a subprocess, and demands FAILED plus the named problem - then removes the
copies. Clean run of the same label must print OK.

The mutations:
  1. `outOfPlane` flipped to true on throw 0     -> must fail naming outOfPlane
  2. `maxAbsZMeters` set to 1.5 on throw 0       -> must fail naming maxAbsZMeters
  3. `projectileGravityScale` set to 1 on throw 0 -> must fail naming projectileGravityScale
  4. `referenceImpulseNs` set to 999             -> must fail naming the impulse
Nothing under `Assets/` is touched and no player runs. Exit 1 when a mutation survives.
"""
from __future__ import annotations

import io
import json
import os
import subprocess
import sys

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(PROJECT, "Trials", "Runs")
CHECK = os.path.join(PROJECT, "Tools", "impact-inplane-check.py")
SOURCE = os.path.join(RUNS, "imp_cb1-impacts.json")


def run_check(label):
    result = subprocess.run([sys.executable, CHECK, label], capture_output=True, text=True)
    return result.returncode, result.stdout


def mutate(name, change):
    with io.open(SOURCE, encoding="utf-8-sig") as handle:
        run = json.load(handle)
    change(run)
    path = os.path.join(RUNS, name + "-impacts.json")
    with io.open(path, "w", encoding="utf-8") as handle:
        json.dump(run, handle)
    return path


def main():
    if not os.path.exists(SOURCE):
        print("SELFTEST-SKIP no %s" % SOURCE)
        return 2

    failures = 0
    code, out = run_check("imp_cb1")
    if code != 0 or "OK" not in out:
        print("SELFTEST BROKEN the unmutated run does not pass:\n%s" % out)
        failures += 1
    else:
        print("control     imp_cb1 as stored            -> OK (exit 0)")

    def flip_plane(run):
        run["throws"][0]["outOfPlane"] = True

    def set_z(run):
        run["throws"][0]["maxAbsZMeters"] = 1.5

    def set_gravity(run):
        run["throws"][0]["projectileGravityScale"] = 1

    def set_impulse(run):
        run["throws"][0]["referenceImpulseNs"] = 999

    cases = [
        ("imp_cb1Z_selftest_plane", flip_plane, "outOfPlane"),
        ("imp_cb1Z_selftest_z", set_z, "maxAbsZMeters"),
        ("imp_cb1Z_selftest_grav", set_gravity, "projectileGravityScale"),
        ("imp_cb1Z_selftest_imp", set_impulse, "referenceImpulseNs"),
    ]
    written = []
    try:
        for name, change, expected in cases:
            written.append(mutate(name, change))
            code, out = run_check(name)
            caught = code != 0 and expected in out
            print("mutation    %-26s %s -> %s (%s)"
                  % (name, expected, "CAUGHT" if caught else "SURVIVED",
                     [line for line in out.splitlines() if line.startswith(name)][-1:]))
            failures += 0 if caught else 1
    finally:
        for path in written:
            if os.path.exists(path):
                os.remove(path)
        print("cleaned %d selftest file(s)" % len(written))

    print("IMPACT-INPLANE-SELFTEST %s mutations=%d survived=%d"
          % ("OK" if failures == 0 else "FAILED", len(cases), failures))
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
