# -*- coding: utf-8 -*-
"""B-PERCH-24 meter: THE LEGS' OWN SUPPORT AT THE STROKE (the eye's "the body shifts").

For each take: the camera's ground-texture pan between the rest frame (t=7.90) and the stroke
frame (t=8.25) is measured on the grass band and compensated, then the bird's mask is compared
zone by zone (the world-aligned overlap, Jaccard):
  * head/neck rows 470-520, torso rows 520-560, LEGS rows 560-605 (the standing row cut),
plus the sole read: the leg zone's own lowest pixel + x centroid (the support's place),
and the beak band's tip height above the grass line.

Usage: python Tools/_pch24-legs.py <take> [take...]
"""
import os
import sys

import numpy as np
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
RATE = 20
BG = np.array([140, 199, 240])
T_REST = 7.90
T_STROKE = 8.25
BOX = (860, 430, 1080, 630)          # the bird + a margin
ZONES = (("head", 470, 520), ("torso", 520, 560), ("legs", 560, 605))
GROUND = (610, 700, 300, 1700)       # the textured ground for the pan probe


def load(take, tt):
    i = int(round(tt * RATE))
    for d in (0, -1, 1, -2, 2):
        p = os.path.join(RUNS, "shot_%s_%d.png" % (take, i + d))
        if os.path.exists(p):
            return np.asarray(Image.open(p).convert("RGB")).astype(int)
    raise SystemExit("take %s: no shot near t=%.2f" % (take, tt))


def pan(take, a, b, rng=10):
    ya, yb, xa, xb = GROUND
    A = a[ya:yb, xa:xb].astype(float)
    best = None
    for dy in range(-rng, rng + 1):
        for dx in range(-rng, rng + 1):
            B = b[ya + dy:yb + dy, xa + dx:xb + dx].astype(float)
            if B.shape != A.shape:
                continue
            v = float(np.abs(A - B).mean())
            if best is None or v < best[0]:
                best = (v, dx, dy)
    return best[1], best[2], best[0]


def grass_line(a):
    for y in range(540, 700):
        if (np.abs(a[y, 0:1920] - BG).sum(axis=1) > 60).mean() > 0.5:
            return y
    return -1


def bird_mask(img, panxy, cut_y):
    x0, y0, x1, y1 = BOX
    dx, dy = panxy
    sub = img[y0 + dy:y1 + dy, x0 + dx:x1 + dx]
    m = (np.abs(sub - BG).sum(axis=2) > 60)
    gy = cut_y - y0
    if 0 < gy < m.shape[0]:
        m[gy:, :] = False
    return m


print("%-16s pan(dx,dy)       head  torso  legs | sole y/x (rest -> stroke) | tip px" % "take")
out = {}
for take in sys.argv[1:]:
    a = load(take, T_REST)
    b = load(take, T_STROKE)
    gtop = grass_line(a)
    dx, dy, err = pan(take, a, b)
    ma = bird_mask(a, (0, 0), gtop - 4)
    mb = bird_mask(b, (dx, dy), gtop - 4)
    row = []
    for name, za, zb in ZONES:
        ra, rb = za - BOX[1], zb - BOX[1]
        sa, sb = ma[ra:rb], mb[ra:rb]
        u = int((sa | sb).sum())
        row.append(int((sa & sb).sum()) / u if u else float("nan"))
    # the sole: the leg zone's lowest row + its x centroid, in ABSOLUTE coords for the rest and
    # the stroke (the stroke mapped back to the rest's frame by the pan)
    def sole(mask, off):
        ra, rb = ZONES[2][1] - BOX[1], ZONES[2][2] - BOX[1]
        sub = mask[ra:rb]
        ys, xs = np.where(sub)
        if not len(ys):
            return None
        return int(ys.max()) + ZONES[2][1] + off, round(float(xs.mean()) + BOX[0], 1)
    s_a = sole(ma, 0)
    s_b = sole(mb, dy)
    # the beak's tip: the lowest bird pixel in the bird's rightmost 45 px (the head's own band)
    ys, xs = np.where(mb)
    xmax = xs.max() + BOX[0]
    band = mb[:, max(0, (xmax - 45) - BOX[0]):(xmax - BOX[0]) + 1]
    yy, _ = np.where(band)
    tip = gtop - (int(yy.max()) + BOX[1] + dy) if len(yy) else -1
    print("%-16s (%+d,%+d) %.1f  %.2f   %.2f   %.2f | %s -> %s | %d" % (
        take, dx, dy, err, row[0], row[1], row[2], s_a, s_b, tip))
    out[take] = dict(pan=(dx, dy), jaccard=row, sole=(s_a, s_b), tip=tip)
