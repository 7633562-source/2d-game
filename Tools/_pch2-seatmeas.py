# -*- coding: utf-8 -*-
"""B-PERCH-2 seat-state measure: seat 0.000 (bBEND1v_tree f120) vs seat -0.075 (bPCH1m2v_tree
f260) in the SAME camera frame.  For each take: the bird's steel-blue body bbox, the light claw
rows, and the wood top edge at x 915/920/925/930; the leaf (green) rows just under the claws.

Goal of the numbers: how many px the claws sit above / inside the wood's top edge, and how far
the body sits above / inside the crown.  0.075 m / the claw-row difference gives px per metre.
Usage: python Tools/_pch2-seatmeas.py
"""
import numpy as np
from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
CASES = [("bBEND1v_tree", 120, "seat 0.000"), ("bPCH1m2v_tree", 260, "seat -0.075 (default)"),
         ("bPCH1m2v_tree", 268, "seat -0.075, hold")]

for take, idx, label in CASES:
    a = np.asarray(Image.open(f"{RUNS}\\shot_{take}_{idx}.png").convert("RGB")).astype(int)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    steel = (b > r + 15) & (b > 90) & (b < 225)
    claw = (r > 150) & (g > 140) & (b > 120) & (np.abs(r - g) < 45) & (np.abs(g - b) < 60)
    wood = (r > g + 15) & (g >= b) & (r > 50)
    leaf = (g > r + 5) & (g > b + 5)
    ys, xs = np.where(steel[500:700, 880:1010])
    bb = (xs.min() + 880, ys.min() + 500, xs.max() + 880, ys.max() + 500) if len(xs) else None
    cy = np.where(claw[560:660, 880:1010])[0]
    claw_rows = (cy.min() + 560, cy.max() + 560) if len(cy) else None
    tops = {}
    for x in (915, 920, 925, 930):
        col = np.where(wood[560:700, x])[0]
        tops[x] = int(col.min() + 560) if len(col) else None
    # leaf rows in the 4 columns right above the claws
    lrows = []
    for x in (920, 925, 930):
        col = np.where(leaf[560:700, x])[0]
        lrows.append(int(col.min() + 560) if len(col) else None)
    print(f"{take} f{idx} ({label}):")
    print(f"   body(steel) bbox {bb}   claw_rows {claw_rows}")
    print(f"   wood_top {tops}   leaf_top@920/925/930 {lrows}")
