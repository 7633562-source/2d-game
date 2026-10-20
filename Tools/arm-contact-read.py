#!/usr/bin/env python3
"""Read-only reader for the arm press/contact columns of an existing run record.

Answers one question from the records, without building or running anything:
where do the palms actually stop, and why does the ground flag never fire?

Usage:
    python Tools/arm-contact-read.py p_arms81 p_arms70 p_arms_stance
    python Tools/arm-contact-read.py --scan          # every label that has the columns

Prints, per record: the arm/contact/press keys it carries, the recorded hand
column extrema (from the per-frame CSV when it is present) and the ground
constants the run itself logged.
"""

import csv
import json
import os
import sys

RUNS = "Trials/Runs"

KEY_HINTS = ("hand", "ground", "palm", "press", "elbow", "shoulder", "crouch")

# Per-frame columns the arm worker's acceptance is read from.
CSV_COLS = [
    "handContactL", "handContactR", "handYL", "handYR",
    "handGroundedL", "handGroundedR", "armPhaseL", "armPhaseR",
    "pressPhaseL", "pressPhaseR", "shoulderAngleL", "shoulderAngleR",
    "elbowAngleL", "elbowAngleR", "hipAngleL", "hipAngleR",
    "tiltDeg", "pelvisY",
]


def load_json(label):
    path = os.path.join(RUNS, label + ".json")
    if not os.path.exists(path):
        return None
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def delimited(path):
    """The project writes both ';' and ',' CSVs; use the header's own delimiter."""
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        head = f.readline()
    delim = ";" if head.count(";") >= head.count(",") else ","
    with open(path, "r", encoding="utf-8", errors="replace", newline="") as f:
        return list(csv.DictReader(f, delimiter=delim))


def fnum(text):
    try:
        return float(text)
    except (TypeError, ValueError):
        return None


def table(labels):
    """One row per record: the plant geometry against what the stroke delivered."""
    head = ("label", "offL", "offR", "foldL", "foldR", "stkL", "stkR",
            "extL", "extR", "shLiftL", "shLiftR", "liftMax", "contactL", "contactR")
    print("%-14s %6s %6s %6s %6s %5s %5s %7s %7s %8s %8s %8s %8s %8s" % head)
    for lab in labels:
        d = load_json(lab)
        if not isinstance(d, dict):
            continue
        def g(key, scale=1.0):
            v = d.get(key)
            return ("%*.3f" % (8, v * scale)) if isinstance(v, (int, float)) else "%8s" % "-"
        print("%-14s %6s %6s %6s %6s %5s %5s %7s %7s %8s %8s %8s %8s %8s" % (
            lab,
            g("armPressPlantOffsetL", 1)[-6:], g("armPressPlantOffsetR", 1)[-6:],
            g("armPressPlantFoldL", 1)[-6:], g("armPressPlantFoldR", 1)[-6:],
            ("%5d" % d["armPressStrokesL"]) if isinstance(d.get("armPressStrokesL"), (int, float)) else "    -",
            ("%5d" % d["armPressStrokesR"]) if isinstance(d.get("armPressStrokesR"), (int, float)) else "    -",
            g("armPressStrokeMaxDegL"), g("armPressStrokeMaxDegR"),
            g("armPressShoulderLiftL"), g("armPressShoulderLiftR"),
            g("armPressLiftMaxMeters"),
            g("armHandContactFractionL"), g("armHandContactFractionR")))


def main():
    args = [a for a in sys.argv[1:]]
    if args and args[0] == "--series":
        pat = args[1] if len(args) > 1 else "p_arms"
        labels = sorted(
            os.path.splitext(f)[0] for f in os.listdir(RUNS)
            if f.endswith(".json") and f.startswith(pat)
            and isinstance(load_json(os.path.splitext(f)[0]), dict)
            and "armPressPlantOffsetL" in load_json(os.path.splitext(f)[0])
        )
        table(labels)
        return
    if not args or args[0] == "--scan":
        labels = sorted(
            os.path.splitext(f)[0] for f in os.listdir(RUNS) if f.endswith(".json")
        )
        hits = []
        for lab in labels:
            d = load_json(lab)
            if not isinstance(d, dict):
                continue
            if any("handContact" in k for k in d) or os.path.exists(
                os.path.join(RUNS, lab + ".csv")
            ):
                hits.append(lab)
        print("labels with a csv: %d of %d json records" % (len(hits), len(labels)))
        print("last 12:", ", ".join(hits[-12:]))
        return

    for label in args:
        d = load_json(label)
        print("=" * 72)
        print(label, "-> json", "present" if d else "MISSING")
        if isinstance(d, dict):
            for k in sorted(d):
                if any(h in k.lower() for h in KEY_HINTS):
                    print("   %-38s %s" % (k, d[k]))
        csv_path = os.path.join(RUNS, label + ".csv")
        if not os.path.exists(csv_path):
            print("   (no per-frame csv)")
            continue
        rows = delimited(csv_path)
        print("   rows %d, columns %d" % (len(rows), len(rows[0]) if rows else 0))
        for col in CSV_COLS:
            if not rows or col not in rows[0]:
                continue
            vals = [fnum(r.get(col)) for r in rows]
            vals = [v for v in vals if v is not None]
            if not vals:
                continue
            frac = None
            if set(vals) <= {0.0, 1.0}:
                frac = sum(vals) / len(vals)
            print("   %-18s n=%-6d min=%-12.5f max=%-12.5f %s" % (
                col, len(vals), min(vals), max(vals),
                ("true-fraction %.4f" % frac) if frac is not None else ""))


if __name__ == "__main__":
    main()
