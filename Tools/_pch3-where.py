# -*- coding: utf-8 -*-
"""Locate the bird (blue sprite) in a take's frames - for camAuto framings where the
static-suite crops do not apply. Usage: python Tools/_pch3-where.py <take> <f1> [f2 ...]
"""
import sys
from PIL import Image
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"


def main():
    take = sys.argv[1]
    for arg in sys.argv[2:]:
        fr = int(arg)
        try:
            a = np.asarray(Image.open(f"{RUNS}\\shot_{take}_{fr}.png").convert("RGB")).astype(int)
        except FileNotFoundError:
            print(take, fr, "missing")
            continue
        h, w, _ = a.shape
        r, g, b = a[..., 0], a[..., 1], a[..., 2]
        blue = (b > r + 15) & (b > 90) & (b < 225)
        ys, xs = np.where(blue)
        if len(ys):
            print("%s f%d size %dx%d bird bbox x %d..%d y %d..%d" % (
                take, fr, w, h, xs.min(), xs.max(), ys.min(), ys.max()))
        else:
            print("%s f%d size %dx%d no bird" % (take, fr, w, h))


main()
