#!/usr/bin/env python3
"""The verdict table: the shared FallCriterion against the controller's own ladder.

    python Tools/impact-verdict-table.py

Block 1 proves the measurement change moved no number that was already published: the
twelve classes of the accepted push-back grid (`pb_grid_*`, build c49c83d3) are compared
with the same twelve on the verdict build (`cv_grid_*`): reference impulse, delivered
impulse, contact time, fall verdict and the whole ground channel must be identical.

Block 2 prints the new column: for each class, the criterion's verdict next to the
controller's own (BalanceController.GeometricFallState, the BodySupportGeometry ladder),
as a time after the contact and as a one-word agreement.
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

# The columns the two builds must agree on: everything the previous cycles published.
SAME_KEYS = ("referenceImpulseNs", "deliveredImpulseNs", "contactTime", "fell",
             "criterionFallTime", "peakComExcursionM", "minSupportMarginM",
             "stepsAfterImpact", "passivePeakFzRatio", "contactMs", "pushedBack",
             "pushMeanRatioPeak", "closingSpeedMps", "latencyPassiveMs")


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


def main():
    labels = sys.argv[1:]
    pairs = [(f"pb_grid_{s}_{m}_{v}", f"cv_grid_{s}_{m}_{v}") for s, m, v in CLASSES]
    if labels:
        pairs = [(a, b) for a, b in pairs
                 if any(a.endswith(tag) or b.endswith(tag) for tag in labels)]

    print("── 1. did any published column move? (it must not: the change is a measure) ──")
    moved_total = 0
    for old_label, new_label in pairs:
        _, old = load(old_label)
        _, new = load(new_label)
        if old is None or new is None:
            print(f"{new_label:26s} MISSING (old={bool(old)} new={bool(new)})")
            continue
        moved = [k for k in SAME_KEYS
                 if (old.get(k), new.get(k)) != (new.get(k), old.get(k)) and
                 (old.get(k) != new.get(k) or (k == "fell" and old.get(k) != new.get(k)))]
        moved_total += len(moved)
        flag = "OK" if not moved else "MOVED " + ",".join(moved)
        print(f"{new_label:26s} ref {num(old['referenceImpulseNs'], 0):>5s} N*s "
              f"delivered {num(old['deliveredImpulseNs']):>9s} -> {num(new['deliveredImpulseNs']):>9s} "
              f"contact {num(old['contactTime'], 3):>8s} -> {num(new['contactTime'], 3):>8s} "
              f"{flag}")
    print(f"columns that moved across {len(pairs)} classes: {moved_total}")

    print()
    print("── 2. two verdicts on the same window ──")
    print(f"{'class':22s} {'ref N*s':>8s} {'criterion':>10s} {'t_fall':>8s} "
          f"{'controller':>11s} {'t_fall':>8s} {'crit frac':>9s} {'fall frac':>9s} verdict")
    counts = {}
    for old_label, new_label in pairs:
        _, row = load(new_label)
        if row is None:
            continue
        verdict = row.get("fallVerdict", "-")
        counts[verdict] = counts.get(verdict, 0) + 1
        crit = "yes" if row.get("fell") else "no"
        ctrl = "yes" if row.get("ctrlFell") else "no"
        crit_t = row.get("timeToFallSeconds", -1)
        ctrl_t = row.get("ctrlFallenAfterContactSeconds", -1)
        ctrl_crit_t = row.get("ctrlCriticalAfterContactSeconds", -1)
        print(f"{new_label.replace('cv_grid_', ''):22s} "
              f"{num(row.get('referenceImpulseNs'), 0):>8s} {crit:>10s} "
              f"{(num(crit_t) if crit_t and crit_t > 0 else '-'):>8s} {ctrl:>11s} "
              f"{(num(ctrl_t) if ctrl_t and ctrl_t > 0 else '-'):>8s} "
              f"{num(row.get('ctrlCriticalFraction'), 3):>9s} "
              f"{num(row.get('ctrlFallenFraction'), 3):>9s} {verdict}")
    print("verdicts:", ", ".join(f"{k}={v}" for k, v in sorted(counts.items())))


if __name__ == "__main__":
    main()
