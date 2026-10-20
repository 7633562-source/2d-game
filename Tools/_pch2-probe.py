# -*- coding: utf-8 -*-
"""B-PERCH-2 probe: camera + framing facts for the phase-6 landing cut.

Questions answered with pixels only:
  1. Is the bPCH1d_tree camera static across the take (frames 100..900)?
  2. Where is the perch/wood in that take (vs the bBEND1v_tree coordinate frame)?
Usage: python Tools/_pch2-probe.py
"""
import numpy as np
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"


def load(take, idx):
    return np.asarray(Image.open(f"{RUNS}\\shot_{take}_{idx}.png").convert("RGB")).astype(np.int16)


def bbox_changed(a, b, thr=12):
    d = np.abs(a - b).max(axis=2)
    ys, xs = np.where(d > thr)
    if len(xs) == 0:
        return None
    return int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max()), int(d.max())


def dark_profile(img):
    """column-wise count of dark pixels (the tree trunk / branch are dark wood)."""
    g = img.mean(axis=2)
    dark = (g < 90).sum(axis=0)
    return dark


print("== bPCH1d_tree camera check (full-frame changed bbox) ==")
f = {i: load("bPCH1d_tree", i) for i in (100, 240, 260, 300, 500, 900)}
print("shape", f[100].shape)
for a, b in ((100, 500), (100, 900), (500, 900), (240, 300)):
    print(f"  {a}->{b}:", bbox_changed(f[a], f[b]))

print("== trunk darkness columns (bPCH1d_tree f100 vs f900) ==")
for i in (100, 900):
    d = dark_profile(f[i])
    cols = np.where(d > 40)[0]
    print(f"  f{i}: strong-dark cols {cols.min() if len(cols) else '-'}..{cols.max() if len(cols) else '-'} n={len(cols)}")
    top = np.argsort(d)[-6:]
    print(f"       top cols {sorted(top.tolist())} counts {[int(d[c]) for c in sorted(top.tolist())]}")

print("== cross-take frame identity (bBEND1v_tree vs bPCH1d_tree, f120/f500) ==")
p = load("bBEND1v_tree", 120)
q = load("bPCH1d_tree", 120)
print("  120:", bbox_changed(p, q, thr=20))
p5 = load("bBEND1v_tree", 500)
q5 = load("bPCH1d_tree", 500)
print("  500:", bbox_changed(p5, q5, thr=20))
