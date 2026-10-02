# -*- coding: utf-8 -*-
"""B-PERCH-5 (bird lane, 26.09): the EXPOSED-SLOT seat A/B, slot 1 (Fork) - row W =
`bPCH4w_tree` (seat -0.037, the old default), row V = `bPCH5b_tree` (seat -0.03, the
nearest-face probe's value). Same crop x 818..952, y 368..466 at 8x in both rows (the
bPCH4y/bPCH5w contact box), frames at the touch (12.45/12.50) and the hold (15.00).
Usage: python Tools/_pch5vw.py
"""
from PIL import Image, ImageDraw
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
REV = RUNS + r"\reviews"

ROW_A = ("bPCH4w_tree", [(249, "W -0.037 t=12.45"), (250, "W -0.037 t=12.50"),
                         (252, "W -0.037 t=12.60"), (300, "W -0.037 t=15.00")])
ROW_B = ("bPCH5b_tree", [(249, "V -0.03 t=12.45"), (250, "V -0.03 t=12.50"),
                         (252, "V -0.03 t=12.60"), (300, "V -0.03 t=15.00")])


def load(take, i):
    return np.asarray(Image.open(RUNS + "\\shot_%s_%02d.png" % (take, i)).convert("RGB")).astype(int)


def panel(take, i, label, box=(818, 368, 952, 466), zoom=8, grid_step=10):
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


pa = [panel(ROW_A[0], i, lb) for i, lb in ROW_A[1]]
pb = [panel(ROW_B[0], i, lb) for i, lb in ROW_B[1]]
w = max(p.width for p in pa + pb)
hh = max(p.height for p in pa + pb)
out = Image.new("RGB", (4 * w + 3 * 8, 2 * hh + 46), (0, 0, 0))
d = ImageDraw.Draw(out)
d.text((6, 6), "SLOT 1 (Fork) EXPOSED SEAT A/B - crop x818-952 y368-466, 8x. Row W = seat -0.037 (old default); Row V = seat -0.03 (the probe's value). White ticks every 10 px.", fill=(255, 255, 255))
for k, p in enumerate(pa):
    out.paste(p, (k * (w + 8), 24))
for k, p in enumerate(pb):
    out.paste(p, (k * (w + 8), 24 + hh + 22))
path = REV + r"\bPCH5_vw_seat.png"
out.save(path)
print("saved", path, out.size)
