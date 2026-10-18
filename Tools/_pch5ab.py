# -*- coding: utf-8 -*-
"""B-PERCH-5 (bird lane, 26.09): the seat A/B sheet for slot 7, same crop and scale
in every panel - row A = `bPCH3d_tree` (seat -0.037, the ACCEPTED pose), row B =
`bPCH5a_tree` (the probe's measured seat -0.0054). Box x 815..862, y 230..272 at
14x (the claw/wood zone), the same box as the bPCH3d/bPCH5a zoom cuts.
Usage: python Tools/_pch5ab.py
"""
from PIL import Image, ImageDraw
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
REV = RUNS + r"\reviews"

ROW_A = ("bPCH3d_tree", [(258, "A -0.037 t=12.90"), (260, "A -0.037 t=13.00"),
                         (262, "A -0.037 t=13.10"), (268, "A -0.037 t=13.40")])
ROW_B = ("bPCH5a2_tree", [(259, "C -0.0354 t=12.95"), (261, "C -0.0354 t=13.05"),
                          (263, "C -0.0354 t=13.15"), (266, "C -0.0354 t=13.30")])


def load(take, i):
    return np.asarray(Image.open(RUNS + "\\shot_%s_%02d.png" % (take, i)).convert("RGB")).astype(int)


def panel(take, i, label, box=(815, 230, 862, 272), zoom=14, grid_step=10):
    a = load(take, i)
    x0, y0, x1, y1 = box
    c = a[y0:y1, x0:x1].astype("uint8")
    im = Image.fromarray(c).resize(((x1 - x0) * zoom, (y1 - y0) * zoom), Image.NEAREST)
    d = ImageDraw.Draw(im)
    for gx in range(x0 - x0 % grid_step + grid_step, x1, grid_step):
        X = (gx - x0) * zoom
        d.line([(X, 0), (X, 6)], fill=(255, 255, 255), width=1)
    for gy in range(y0 - y0 % grid_step + grid_step, y1, grid_step):
        Y = (gy - y0) * zoom
        d.line([(0, Y), (6, Y)], fill=(255, 255, 255), width=1)
    band = 26
    out = Image.new("RGB", (im.width, im.height + band), (12, 12, 12))
    out.paste(im, (0, band))
    ImageDraw.Draw(out).text((6, 7), label, fill=(255, 232, 90))
    return out


def row(take, frames):
    return [panel(take, i, lb) for i, lb in frames]


pa = row(*ROW_A)
pb = row(*ROW_B)
w = max(p.width for p in pa + pb)
half = 200
out = Image.new("RGB", (4 * w + 3 * 8, 2 * (half + 26) + 40), (0, 0, 0))
d = ImageDraw.Draw(out)
d.text((6, 6), "SLOT 7 SEAT A/C - same crop x815-862 y230-272, 14x. Row A = the accepted -0.037; Row C = the patched probe's -0.0354. White ticks every 10 px.", fill=(255, 255, 255))
for k, p in enumerate(pa):
    out.paste(p, (k * (w + 8), 24))
for k, p in enumerate(pb):
    out.paste(p, (k * (w + 8), 24 + half + 26 + 16))
path = REV + r"\bPCH5_ac_seat.png"
out.save(path)
print("saved", path, out.size)
