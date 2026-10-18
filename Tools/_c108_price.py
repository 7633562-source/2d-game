#!/usr/bin/env python3
"""DOG CYCLE 108 - THE FRONT-SIDE HAND-OVER: the price read on the EXISTING rows.

Reads only (no build, no law edit): the collapse windows of
  - the base row   dog_c107pre_w04.csv   (law fc3a22c6; fall 3.185, FL leaves 2.935)
  - the layout row dog_c105_w04.csv      (law 5ab37d7b; fall 10.880, FR leaves 10.625)
and prints, frame by frame through the window: the support set, the capture
margin + its own terms (support interval, capture point), the CoM, the trunk
pitch / pelvis tilt, the front joints, the front paw clearances.

Usage: python Tools/_c108_price.py
"""
import csv
import json
import os

RUNS = r"E:/Games/2D Game/My project/Trials/Runs"
ROWS = [("pre", "dog_c107pre_w04"), ("layout", "dog_c105_w04")]


def load(name):
    csv_path = os.path.join(RUNS, name + ".csv")
    with open(csv_path, "r", encoding="utf-8-sig", newline="") as f:
        r = csv.reader(f)
        hdr = next(r)
        rows = [row for row in r if row and row[0].strip() != ""]
    idx = {h: i for i, h in enumerate(hdr)}
    j = {}
    jp = os.path.join(RUNS, name + ".json")
    if os.path.exists(jp):
        j = json.load(open(jp, encoding="utf-8"))
    return hdr, idx, rows, j


def f(row, idx, key):
    try:
        return float(row[idx[key]])
    except (KeyError, IndexError, ValueError):
        return float("nan")


def read(tag, name):
    hdr, idx, rows, j = load(name)
    fall = j.get("survivedSeconds")
    print("=" * 100)
    print("%s  %s   fall=%.4f  letter=%s  cycles=%s" % (
        tag, name, fall if fall else -1,
        round(j.get("comSpeedLetter", -1), 4), j.get("walkCycleCount")))
    print("cols:", " ".join(h for h in hdr[0:1]))
    # the last grounded frame of each paw before the fall
    last_g = {}
    for pk, k in (("FR", "frG"), ("FL", "flG"), ("RR", "rrG"), ("RL", "rlG")):
        lt = None
        for row in rows:
            t = f(row, idx, "t")
            if fall is not None and t > fall:
                break
            if f(row, idx, k) > 0.5:
                lt = t
        last_g[pk] = lt
    print("last grounded before the fall:", " ".join("%s=%.3f" % (k, v if v else -1)
          for k, v in last_g.items()))
    # the margin sign change
    cross = None
    prev = None
    for row in rows:
        t = f(row, idx, "t")
        if fall is not None and t > fall:
            break
        m = f(row, idx, "capturePointMargin")
        if prev is not None and prev > 0 >= m:
            cross = (t, m, prev)
            break
        prev = m
    if cross:
        print("margin crossing: t=%.3f  %.4f -> %.4f" % cross)
    else:
        print("margin crossing: not found before the fall")
    print()
    lo = (fall - 0.45) if fall else 0.0
    hi = (fall + 0.015) if fall else 1.0
    print("   t    frG flG rrG rlG | margin   cpX    comX   vX    | chest pelvis chestY | shoL shoR elbL elbR | "
          "push trunkLift hov pike | clearFR clearFL clearRR clearRL")
    shown = 0
    for row in rows:
        t = f(row, idx, "t")
        if t < lo or t > hi:
            continue
        fr = f(row, idx, "comX")
        cp = f(row, idx, "captureXi")
        # velocity: finite difference over 5 frames
        i = rows.index(row)
        vx = float("nan")
        if i + 5 < len(rows):
            vx = (f(rows[i + 5], idx, "comX") - fr) / (f(rows[i + 5], idx, "t") - t)
        if (shown % 4) == 0 or abs(t - round(t, 1)) < 0.0025:
            print("%7.3f %3.0f %3.0f %3.0f %3.0f | %+.4f %+.3f %+.3f %+.2f | %+7.2f %+7.2f %+.4f | "
                  "%+7.2f %+7.2f %+7.2f %+7.2f | %+5.1f %+5.2f %2.0f %2.0f | "
                  "%.4f %.4f %.4f %.4f" % (
                      t, f(row, idx, "frG"), f(row, idx, "flG"), f(row, idx, "rrG"), f(row, idx, "rlG"),
                      f(row, idx, "capturePointMargin"), cp, fr, vx,
                      f(row, idx, "chestPitch"), f(row, idx, "pelvisTilt"), f(row, idx, "chestY"),
                      f(row, idx, "shoL"), f(row, idx, "shoR"), f(row, idx, "elbL"), f(row, idx, "elbR"),
                      f(row, idx, "push"), f(row, idx, "trunkLift"), f(row, idx, "handOver"), f(row, idx, "pike"),
                      f(row, idx, "pawClearFR"), f(row, idx, "pawClearFL"),
                      f(row, idx, "pawClearRR"), f(row, idx, "pawClearRL")))
        shown += 1
    # the pitch rate through the crossing
    print()
    if cross:
        tc = cross[0]
        p0 = p1 = None
        for row in rows:
            t = f(row, idx, "t")
            if abs(t - (tc - 0.05)) < 0.003:
                p0 = f(row, idx, "chestPitch")
            if abs(t - (tc + 0.05)) < 0.003:
                p1 = f(row, idx, "chestPitch")
        if p0 is not None and p1 is not None:
            print("chestPitch +/-0.05 s around the crossing: %+.2f -> %+.2f = %.1f deg/s" % (
                p0, p1, (p1 - p0) / 0.1))


for tag, name in ROWS:
    read(tag, name)
