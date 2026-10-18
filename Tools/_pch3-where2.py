# -*- coding: utf-8 -*-
"""Locate the bird in a camAuto take by its palette (sampled from a known static-suite frame).
Usage: python Tools/_pch3-where2.py <take> <f1> [f2 ...]
"""
import sys
from PIL import Image
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
REF = "bPCH2l_tree"      # ring7 take: the bird sat at x 915-937, y 582-607 in f268
REF_BOX = (912, 580, 942, 610)


def bird_palette():
    a = np.asarray(Image.open(f"{RUNS}\\shot_{REF}_268.png").convert("RGB")).astype(int)
    x0, y0, x1, y1 = REF_BOX
    win = a[y0:y1, x0:x1].reshape(-1, 3)
    # steel-blue bird pixels: blue dominant, not the light sky (b>=225) and not dark outline
    sel = (win[:, 2] > win[:, 0] + 15) & (win[:, 2] > 90) & (win[:, 2] < 225)
    pal = np.unique(win[sel], axis=0)
    return pal[:: max(1, len(pal) // 12)][:12]


def main():
    take = sys.argv[1]
    pal = bird_palette()
    print("palette:", ["#%02x%02x%02x" % tuple(c) for c in pal])
    for arg in sys.argv[2:]:
        fr = int(arg)
        a = np.asarray(Image.open(f"{RUNS}\\shot_{take}_{fr}.png").convert("RGB")).astype(int)
        h, w, _ = a.shape
        m = np.zeros((h, w), bool)
        for c in pal:
            m |= (np.abs(a - c).sum(axis=2) <= 6)
        ys, xs = np.where(m)
        if len(ys):
            print("%s f%d %dx%d bird-ish bbox x %d..%d y %d..%d n=%d" % (
                take, fr, w, h, xs.min(), xs.max(), ys.min(), ys.max(), len(ys)))
        else:
            print("%s f%d %dx%d none" % (take, fr, w, h))


main()
