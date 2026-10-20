# -*- coding: utf-8 -*-
"""B-PERCH-5 (bird lane, 26.09): the seat fine map, take A (`bPCH3d_tree`, seat -0.037,
the accepted) vs take B (`bPCH5a_tree`, seat -0.0054, the probe's measured value).

For the contact zone (window x 815..865, y 226..276 of the SAME owner-scale crop the
two cuts use) the script prints, per column band, the claw band rows (light sprite
pixels: r>150 g>140 b>120, |r-b|<40 - the sky's blue is r-b ~60+ and drops out) and
the first wood row (brown: r>g+15, g>=b, r>50) going down.
Claws standing ON the wood reads as claw_max + 1-2 == wood_top at the same x.
Usage: python Tools/_pch5fine.py
"""
from PIL import Image
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
PAIRS = [("A bPCH3d -0.037", "bPCH3d_tree", 262), ("A bPCH3d -0.037", "bPCH3d_tree", 268),
         ("B bPCH5a -0.0054", "bPCH5a_tree", 263), ("B bPCH5a -0.0054", "bPCH5a_tree", 300),
         ("C bPCH5a2 -0.0354", "bPCH5a2_tree", 261), ("C bPCH5a2 -0.0354", "bPCH5a2_tree", 266),
         ("C bPCH5a2 -0.0354", "bPCH5a2_tree", 300)]


def load(take, i):
    return np.asarray(Image.open(RUNS + "\\shot_%s_%02d.png" % (take, i)).convert("RGB")).astype(int)


for lab, take, i in PAIRS:
    a = load(take, i)
    win = a[226:276, 815:865]
    r, g, b = win[..., 0], win[..., 1], win[..., 2]
    claw = (r > 150) & (g > 140) & (b > 120) & (np.abs(r - b) < 40)
    wood = (r > g + 15) & (g >= b) & (r > 50)
    row = ""
    for x in (828, 832, 836, 840):
        col_claw = np.where(claw[:, x - 815])[0]
        col_wood = np.where(wood[:, x - 815])[0]
        row += "  x%d claw=%s wood=%s" % (
            x,
            (int(col_claw.min()) + 226, int(col_claw.max()) + 226) if len(col_claw) else "-",
            (int(col_wood.min()) + 226) if len(col_wood) else "-")
    print("%s f%-4d %s" % (lab, i, row))
