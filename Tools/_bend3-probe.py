# -*- coding: utf-8 -*-
"""B-BEND-3 (bird lane, 25.09 fire 20:15): geometry probe for the left-limb-only cut.

Maps, for the static twin bBEND1v_tree, the topmost wood pixel per column and the
sky gaps that sit INSIDE the wood band (the fork's inner wedge + any plant seam), in
both states: seated (f120, t=6.0 s) and unloaded (f800, t=40 s).
"""
from PIL import Image
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
SKY = np.array([140, 199, 240])


def load(i):
    return np.asarray(Image.open(f"{RUNS}\\shot_bBEND1v_tree_{i}.png").convert("RGB")).astype(int)


def sky(a):
    return (np.abs(a - SKY).sum(axis=2) < 40)


def wood(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    return (r > g + 15) & (g >= b) & (r > 50)


A = load(120)   # seated
C = load(800)   # unloaded

print("frame size:", A.shape)
print("x | seat top | unld top | seat gaps | unld gaps  (y-ranges of sky inside wood band y555-705)")
for x in range(866, 1006, 3):
    row = []
    for a in (A, C):
        col = a[555:705, x]
        r, g, b = col[:, 0], col[:, 1], col[:, 2]
        w = (r > g + 15) & (g >= b) & (r > 50)
        s = (np.abs(col - SKY).sum(axis=1) < 40)
        ys = np.where(w)[0]
        top = (ys.min() + 555) if len(ys) else -1
        gaps = []
        i = 0
        n = len(col)
        while i < n:
            if s[i]:
                j = i
                while j < n and s[j]:
                    j += 1
                if w[:i].any() and w[j:].any():
                    gaps.append((i + 555, j - 1 + 555))
                i = j
            else:
                i += 1
        row.append((int(top), gaps))
    print(f"{x} | {row[0][0]} | {row[1][0]} | {row[0][1]} | {row[1][1]}")
