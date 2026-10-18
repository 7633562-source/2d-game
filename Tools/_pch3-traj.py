# -*- coding: utf-8 -*-
"""B-PERCH-3 trajectory check (bird lane, 25.09): bird bbox rows across the landing window
of the lift take -> quantify the 'up jump' (~12.4) and the 'plunge' (12.9->13.05) the eyes name.
Bird = steel-blue pixels in x 650..1150 y 350..780; feet/light = light pixels in the foot window.
Usage: python Tools/_pch3-traj.py [take]
"""
import sys
from PIL import Image
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TAKE = sys.argv[1] if len(sys.argv) > 1 else "bPCH2l_tree"
WOODX = 920


def load(i):
    return np.asarray(Image.open(f"{RUNS}\\shot_{TAKE}_{i}.png").convert("RGB")).astype(int)


print("take=%s  (idx = t*20; t from 11.50 to 13.60)" % TAKE)
print("  t     top  bot  colL colR | feet(light in x900-955 y560-650) | wood_top@x%d" % WOODX)
for i in range(230, 273, 2):
    a = load(i)
    win = a[350:780, 650:1150]
    r, g, b = win[..., 0], win[..., 1], win[..., 2]
    blue = (b > r + 15) & (b > 90) & (b < 225)
    ys, xs = np.where(blue)
    if len(ys):
        top, bot, cl, cr = ys.min() + 350, ys.max() + 350, xs.min() + 650, xs.max() + 650
    else:
        top = bot = cl = cr = -1
    fw = a[560:650, 900:955]
    fr, fg, fb = fw[..., 0], fw[..., 1], fw[..., 2]
    light = (fr > 150) & (fg > 140) & (fb > 120) & (np.abs(fr - fg) < 45) & (np.abs(fg - fb) < 60)
    fy = np.where(light)[0]
    feet = "%d..%d" % (fy.min() + 560, fy.max() + 560) if len(fy) else "-"
    aw = a[560:660, WOODX]
    wr, wg, wb = aw[..., 0], aw[..., 1], aw[..., 2]
    wood = (wr > wg + 15) & (wg >= wb) & (wr > 50)
    wy = np.where(wood)[0]
    wt = (wy.min() + 560) if len(wy) else "-"
    print("%6.2f  %4d %4d  %4d %4d | %-12s | %s" % (i / 20.0, top, bot, cl, cr, feet, wt))
