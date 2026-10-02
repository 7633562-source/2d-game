#!/usr/bin/env python3
"""BALANCE-ARENA-1 acceptance 3: is the throw strictly in-plane, and is the impulse reported?

    python Tools/impact-inplane-check.py [label ...]        # default: every imp_cb* run

The card (`.cursor/tasks/BALANCE-ARENA-1.md`, acceptance 3): "The object is strictly in-plane
and the impulse (mass * speed) is reported." Every cycle so far has quoted that clause from
whatever the JSON happened to carry, so the fields were never named anywhere a machine reads
them. They live in the harness's OWN record `<label>-impacts.json` (the physics trial record
`<label>.json` carries no throw at all), and the plane fields live PER THROW, not at the top:

    switches.projectileGravity / switches.launch      the shot line: gravity-free, X-Y plane
    switches.massKg * switches.speedMps                the impulse the switches ask for
    throws[i].outOfPlane / .maxAbsZMeters              the projectile's plane and its |z|
    throws[i].projectileGravityScale                   must be 0
    throws[i].referenceImpulseNs                       the impulse the record publishes

A missing field is a FAILURE here, not a pass: the first version of this tool read the wrong
file, found nothing, and printed `OK runs=8 bad=0` on eight runs; the second version looked
for the plane fields at the top level and failed the same eight runs for the same reason. A
run with no throw (`-impactCount 0`) has nothing to be in plane and is reported N/A - its
switches are still checked. Strictly read-only. Exit 1 when any thrown body is out of plane,
carries a gravity scale, misses a field, or publishes a reference impulse that is not
mass * speed.
"""
from __future__ import annotations

import glob
import io
import json
import os
import sys

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(PROJECT, "Trials", "Runs")


def load(path):
    with io.open(path, encoding="utf-8-sig") as handle:
        return json.load(handle)


def check_throw(throw, mass_speed):
    problems = []
    if throw.get("outOfPlane") is not False:
        problems.append("outOfPlane=%s" % throw.get("outOfPlane"))
    max_z = throw.get("maxAbsZMeters")
    if max_z is None or abs(float(max_z)) > 1e-9:
        problems.append("maxAbsZMeters=%s" % max_z)
    gravity = throw.get("projectileGravityScale")
    if gravity is None or abs(float(gravity)) > 1e-9:
        problems.append("projectileGravityScale=%s" % gravity)
    reference = throw.get("referenceImpulseNs")
    if reference is None:
        problems.append("referenceImpulseNs missing")
    elif mass_speed is not None and abs(float(reference) - mass_speed) > 0.5:
        problems.append("referenceImpulseNs=%s != mass*speed %s" % (reference, mass_speed))
    return problems


def main():
    labels = sys.argv[1:]
    if not labels:
        labels = sorted(os.path.basename(p)[:-len("-impacts.json")]
                        for p in glob.glob(os.path.join(RUNS, "imp_cb*-impacts.json")))
    bad = 0
    print("%-13s %-6s %-9s %-9s %-8s %s"
          % ("label", "throws", "in-plane", "maxAbsZ_m", "grav", "verdict"))
    for label in labels:
        path = os.path.join(RUNS, label + "-impacts.json")
        if not os.path.exists(path):
            print("%-13s MISSING %s" % (label, path))
            bad += 1
            continue
        run = load(path)
        switches = run.get("switches") or {}
        mass, speed = switches.get("massKg"), switches.get("speedMps")
        mass_speed = None if (mass is None or speed is None) else float(mass) * float(speed)
        throws = run.get("throws") or []

        problems = []
        if mass is None or speed is None:
            problems.append("switches massKg/speedMps missing")
        if switches.get("projectileGravity") is None:
            problems.append("switches.projectileGravity missing")
        elif abs(float(switches["projectileGravity"])) > 1e-9:
            problems.append("switches.projectileGravity=%s" % switches["projectileGravity"])
        if not switches.get("launch"):
            problems.append("switches.launch missing")

        worst_z, worst_g = None, None
        for throw in throws:
            for problem in check_throw(throw, mass_speed):
                problems.append("throw[%s] %s" % (throw.get("index"), problem))
            max_z = throw.get("maxAbsZMeters")
            if max_z is not None and (worst_z is None or float(max_z) > float(worst_z)):
                worst_z = max_z
            gravity = throw.get("projectileGravityScale")
            if gravity is not None:
                worst_g = gravity

        bad += 1 if problems else 0
        print("%-13s %-6d %-9s %-9s %-8s %s"
              % (label, len(throws),
                 "n/a" if not throws else ("yes" if not any("outOfPlane=" in p for p in problems) else "NO"),
                 "n/a" if not throws else worst_z, "n/a" if not throws else worst_g,
                 "OK" if not problems else "FAIL " + "; ".join(problems[:4])))
    print("IMPACT-INPLANE-CHECK %s runs=%d bad=%d" % ("OK" if bad == 0 else "FAILED", len(labels), bad))
    return 1 if bad else 0


if __name__ == "__main__":
    raise SystemExit(main())
