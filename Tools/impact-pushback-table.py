#!/usr/bin/env python3
"""The push-back table: the new ground channel against the build that was accepted before it.

    python Tools/impact-pushback-table.py

Reads the twelve classes of the accepted grid (`grid_<shape>_<mass>_<speed>-impacts.json`,
build 4681f70b) and the same twelve on the push-back build (`pb_grid_...-impacts.json`).
Block 1 proves the simulation did not move: reference impulse, delivered impulse, contact
time and fall verdict must be identical, because the change is instrumentation only.
Block 2 prints the ground channel: what the blow did (passive window), how long the object
stayed in contact, and what the body did about it - next to the old column, which was the
collision itself (report section 5c).
"""
import json
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Trials", "Runs")
CLASSES = []
for shape in ("circle", "square", "rect"):
    for mass in ("0p5", "15"):
        for speed in ("4", "20"):
            CLASSES.append((shape, mass, speed))


def load(label):
    path = os.path.join(RUNS, label + "-impacts.json")
    if not os.path.exists(path):
        return None, None
    with open(path, encoding="utf-8-sig") as handle:
        doc = json.load(handle)
    throws = doc.get("throws") or doc.get("windows") or []
    return doc, (throws[0] if throws else None)


def num(value, digits=3):
    if value is None:
        return "-"
    if isinstance(value, bool):
        return "yes" if value else "no"
    if isinstance(value, (int, float)):
        return f"{value:.{digits}f}"
    return str(value)


def ms_since_contact(window, key):
    value = window.get(key, -1)
    if value is None or value < 0:
        return "-"
    return f"{(value - window['contactTime']) * 1000.0:.0f}"


def main():
    print("── 1. did the simulation move? (it must not: the change is a measure) ──")
    print(f"{'class':22s} {'ref N*s':>8s} {'delivered old':>14s} {'delivered new':>14s} "
          f"{'contact old':>12s} {'contact new':>12s} {'fell old':>9s} {'fell new':>9s} verdict")
    identical = 0
    for shape, mass, speed in CLASSES:
        old_doc, old = load(f"grid_{shape}_{mass}_{speed}")
        new_doc, new = load(f"pb_grid_{shape}_{mass}_{speed}")
        if old is None or new is None:
            print(f"pb_grid_{shape}_{mass}_{speed:22s} MISSING")
            continue
        same = (abs(old["deliveredImpulseNs"] - new["deliveredImpulseNs"]) < 1e-9
                and abs(old["referenceImpulseNs"] - new["referenceImpulseNs"]) < 1e-9
                and old["contactTime"] == new["contactTime"]
                and old["fell"] == new["fell"])
        identical += 1 if same else 0
        print(f"{shape:6s} {mass:>4s} kg {speed:>2s} m/s {num(new['referenceImpulseNs'], 0):>8s} "
              f"{num(old['deliveredImpulseNs']):>14s} {num(new['deliveredImpulseNs']):>14s} "
              f"{num(old['contactTime']):>12s} {num(new['contactTime']):>12s} "
              f"{num(old['fell']):>9s} {num(new['fell']):>9s} "
              f"{'IDENTICAL' if same else 'MOVED'}")
    print(f"identical in {identical}/{len(CLASSES)} classes\n")

    print("── 2. the ground channel: the blow, the contact, the body's answer ──")
    print(f"{'class':22s} {'blow peak W':>11s} {'blow N*s':>9s} {'contact ms':>10s} "
          f"{'push':>5s} {'at +ms':>7s} {'hold ms':>8s} {'push N*s':>9s} {'peak 40ms W':>11s} "
          f"{'old push':>9s} {'old at +ms':>10s} {'old N*s':>8s}")
    pushes = 0
    old_pushes = 0
    for shape, mass, speed in CLASSES:
        old_doc, old = load(f"grid_{shape}_{mass}_{speed}")
        new_doc, new = load(f"pb_grid_{shape}_{mass}_{speed}")
        if old is None or new is None:
            continue
        if new.get("pushedBack"):
            pushes += 1
        if old.get("pushedBack"):
            old_pushes += 1
        print(f"{shape:6s} {mass:>4s} kg {speed:>2s} m/s {num(new.get('passivePeakFzRatio')):>11s} "
              f"{num(new.get('passiveExcessImpulseNs')):>9s} {num(new.get('contactMs'), 0):>10s} "
              f"{num(new.get('pushedBack')):>5s} {ms_since_contact(new, 'pushTime'):>7s} "
              f"{num(new.get('pushBackHoldMs'), 0):>8s} {num(new.get('pushBackImpulseNs')):>9s} "
              f"{num(new.get('pushMeanRatioPeak')):>11s} {num(old.get('pushedBack')):>9s} "
              f"{ms_since_contact(old, 'pushTime'):>10s} {num(old.get('groundPushImpulseNs')):>8s}")
    print(f"\nnew column: a push-back in {pushes}/{len(CLASSES)} classes")
    print(f"old column (the collision): a push-back in {old_pushes}/{len(CLASSES)} classes")
    build = {}
    for shape, mass, speed in CLASSES:
        doc, _ = load(f"pb_grid_{shape}_{mass}_{speed}")
        if doc:
            build[doc.get("buildGuid", "?")] = build.get(doc.get("buildGuid", "?"), 0) + 1
    print("runs per build guid:", ", ".join(f"{k[:12]}x{v}" for k, v in build.items()))
    for shape, mass, speed in CLASSES:
        doc, _ = load(f"pb_grid_{shape}_{mass}_{speed}")
        definition = (doc or {}).get("totals", {}).get("pushDefinition")
        if not definition:
            print(f"WARNING: {shape} {mass} {speed} carries no pushDefinition")
    return 0


if __name__ == "__main__":
    sys.exit(main())
