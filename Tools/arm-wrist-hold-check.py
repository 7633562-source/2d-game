#!/usr/bin/env python3
"""Read the stored p_arms* runs and answer the one question arms cycle 12 opens with:

    is the palm's HOLD the thing that fails when the press arrives, and with how
    much authority is the hold asked to hold it?

Three numbers per run, all from records that already exist (no new trial):

  1. grip   - `armHandContactFraction*` (JSON) plus the per-frame `handContact*`
              fraction inside `armPressPhase* in {2,3}` against outside it, from
              the CSV. The press-OFF control of the same build is the baseline.
  2. gap    - `handY - armPalmY` inside the press: the palm's own height against
              the point the drive latched for it. Positive = the palm is ABOVE
              its own target, i.e. the arm is not holding the hold.
  3. ratio  - the elbow's press ceiling (`elbowMuscleTorque x pressTorqueScale`,
              read from the run's own `applied` block) against the hand's
              (`wristMuscleTorque`). This is the asymmetry, in N.m.

Read-only. Writes nothing. Usage:
    python Tools/arm-wrist-hold-check.py [label ...]
"""
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")
DEFAULT = ["p_arms70", "p_arms71", "p_arms81", "p_arms82", "p_arms15"]


def flatten(obj, prefix=""):
    """Every scalar in a nested JSON, keyed by its dotted path."""
    out = {}
    if isinstance(obj, dict):
        for k, v in obj.items():
            out.update(flatten(v, prefix + k + "."))
    elif isinstance(obj, list):
        for i, v in enumerate(obj):
            out.update(flatten(v, prefix + str(i) + "."))
    else:
        out[prefix[:-1]] = obj
    return out


def find(flat, *needles):
    """First scalar whose path contains every needle (case-insensitive)."""
    for path, value in flat.items():
        low = path.lower()
        if all(n.lower() in low for n in needles):
            return path, value
    return None, None


def read_csv(name):
    path = os.path.join(RUNS, name + ".csv")
    if not os.path.exists(path):
        return None
    with open(path, "r", encoding="utf-8", errors="replace") as handle:
        header = handle.readline().strip()
        delim = ";" if header.count(";") >= header.count(",") else ","
        cols = header.split(delim)
        rows = []
        for line in handle:
            line = line.strip()
            if not line:
                continue
            rows.append(line.split(delim))
    return cols, rows, delim


def column(cols, name):
    try:
        return cols.index(name)
    except ValueError:
        return None


def as_float(text):
    try:
        return float(text)
    except (TypeError, ValueError):
        return None


def press_stats(name):
    """Contact and palm-gap inside the press, from the run's own per-frame CSV."""
    blob = read_csv(name)
    if blob is None:
        return None
    cols, rows, _ = blob
    out = {}
    for side in ("L", "R"):
        i_hand = column(cols, "handY" + side)
        i_palm = column(cols, "armPalmY" + side)
        i_phase = column(cols, "armPressPhase" + side)
        i_touch = column(cols, "handContact" + side)
        if None in (i_hand, i_palm, i_phase):
            out[side] = None
            continue
        gaps_in, gaps_out = [], []
        touch_in = touch_out = n_in = n_out = 0
        for row in rows:
            if len(row) <= max(i_hand, i_palm, i_phase):
                continue
            hand, palm, phase = (as_float(row[i_hand]), as_float(row[i_palm]),
                                 as_float(row[i_phase]))
            if None in (hand, palm, phase):
                continue
            if i_touch is not None and len(row) > i_touch:
                touch = as_float(row[i_touch])
            else:
                touch = None
            if int(phase) in (2, 3):
                n_in += 1
                gaps_in.append(hand - palm)
                touch_in += 1 if touch == 1 else 0
            else:
                n_out += 1
                gaps_out.append(hand - palm)
                touch_out += 1 if touch == 1 else 0
        out[side] = {
            "n_in": n_in,
            "n_out": n_out,
            "gap_in_mean": sum(gaps_in) / n_in if n_in else None,
            "gap_in_max": max(gaps_in) if gaps_in else None,
            "gap_out_mean": sum(gaps_out) / n_out if n_out else None,
            "touch_in": touch_in / n_in if n_in else None,
            "touch_out": touch_out / n_out if n_out else None,
        }
    return out


def switch(args, name, fallback):
    """The run's own value of an `-arm...` switch, else the code's default."""
    for i, tok in enumerate(args):
        if tok == name and i + 1 < len(args):
            try:
                return float(args[i + 1])
            except ValueError:
                return fallback
    return fallback


def main():
    labels = sys.argv[1:] or DEFAULT
    print("label              | push/scale | ceiling el/Hand N.m | grip JSON L/R | "
          "grip CSV in L/R | gap in mean L/R (m)")
    print("-" * 122)
    for label in labels:
        path = os.path.join(RUNS, label + ".json")
        if not os.path.exists(path):
            print("%-18s | missing JSON" % label)
            continue
        with open(path, "r", encoding="utf-8", errors="replace") as handle:
            flat = flatten(json.load(handle))
        args = flat.get("commandArgs") or []
        if isinstance(args, str):
            args = args.split()
        push = switch(args, "-armPush", 1.0)
        scale = switch(args, "-armPushScale", 4.0)
        _, el_torque = find(flat, "elbowmuscletorque")
        _, wr_torque = find(flat, "wristmuscletorque")
        _, grip_l = find(flat, "armhandcontactfractionl")
        _, grip_r = find(flat, "armhandcontactfractionr")
        stats = press_stats(label)
        csv_in = "n/a"
        gap = "n/a"
        if stats and stats["L"] and stats["R"]:
            if None not in (stats["L"]["touch_in"], stats["R"]["touch_in"]):
                csv_in = "%.3f/%.3f" % (stats["L"]["touch_in"], stats["R"]["touch_in"])
            if stats["L"]["gap_in_mean"] is not None:
                gap = "%+.4f/%+.4f" % (stats["L"]["gap_in_mean"],
                                       stats["R"]["gap_in_mean"])
        ceiling = "n/a"
        if None not in (el_torque, wr_torque):
            ceiling = "%.1f/%.1f" % (el_torque * scale, wr_torque)
        def fmt(value):
            return "-" if value is None else "%.4f" % value
        print("%-18s | %g/%-6g | %-19s | %s/%s | %-14s | %s"
              % (label, push, scale, ceiling,
                 fmt(grip_l), fmt(grip_r), csv_in, gap))
    print("-" * 122)
    print("push/scale = the run's own -armPush / -armPushScale (defaults 1 / 4).")
    print("ceiling    = elbowMuscleTorque x scale against wristMuscleTorque, N.m.")
    print("grip JSON  = armHandContactFraction* over the frames the body is DOWN.")
    print("grip CSV   = handContact* == 1 as a fraction of press frames (phase 2/3) "
          "and of the rest.")
    print("gap        = handY - armPalmY, metres: the palm against the point the "
          "drive latched for it (positive = above it).")


if __name__ == "__main__":
    main()
