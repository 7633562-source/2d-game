# -*- coding: utf-8 -*-
"""B-PERCH-6 (bird lane, 26.09): the cut of the support-face seat take.

Usage: python Tools/_pch6-cuts.py <take_label> [<out_tag>]

Reads the take's own trigperch csv for the key times (PADS_DOWN, GUIDE_ON, SEAT,
RELEASE); frames = round(t * 20), the takes run with -shotRate 20.
Strips (all written into Trials/Runs/reviews/):
  A seq  (8 panels) x 780..1020 y 300..560 at 3x - the approach, the touch, the settle
  B zoom (4 panels) AUTO-CROPPED on the claw/wood contact at 8x
  C hold (4 panels) as A, over the seat
Prints the claw band rows vs the first wood row per column (is the claw ON the wood?).
"""
import os
import sys
from PIL import Image, ImageDraw
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
REV = RUNS + r"\reviews"
TAKE = sys.argv[1] if len(sys.argv) > 1 else "bPCH6b_tree"
TAG = sys.argv[2] if len(sys.argv) > 2 else TAKE


def key_times(take):
    t = {}
    with open(RUNS + "\\%s_trigperch.csv" % take, encoding="utf-8", errors="ignore") as f:
        for ln in f:
            p = ln.rstrip("\n").split(",", 2)
            if len(p) >= 2 and p[0] in ("PADS_DOWN", "GUIDE_ON", "SEAT", "RELEASE"):
                t[p[0]] = float(p[1])
    return t


T = key_times(TAKE)
SEAT = round(T.get("SEAT", 12.45) * 20)
SEQ = [(SEAT - 13, "t=%.2f" % (T.get("SEAT", 12.45) - 0.65)),
       (SEAT - 9, "t=%.2f" % (T.get("SEAT", 12.45) - 0.45)),
       (SEAT - 5, "t=%.2f" % (T.get("SEAT", 12.45) - 0.25)),
       (SEAT - 2, "t=%.2f" % (T.get("SEAT", 12.45) - 0.10)),
       (SEAT, "t=%.2f TOUCH" % T.get("SEAT", 12.45)),
       (SEAT + 2, "t=%.2f" % (T.get("SEAT", 12.45) + 0.10)),
       (SEAT + 7, "t=%.2f" % (T.get("SEAT", 12.45) + 0.35)),
       (SEAT + 51, "t=%.1f settled" % (T.get("SEAT", 12.45) + 2.55))]
ZOOM = [(SEQ[4][0], SEQ[4][1]), (SEQ[5][0], SEQ[5][1]), (SEQ[6][0], SEQ[6][1]),
        (SEQ[7][0], SEQ[7][1])]
HOLD = [(SEQ[7][0], "t=%.1f" % (T.get("SEAT", 12.45) + 2.55)),
        (SEQ[7][0] + 60, "t=%.1f" % (T.get("SEAT", 12.45) + 5.55)),
        (SEQ[7][0] + 120, "t=%.1f" % (T.get("SEAT", 12.45) + 8.55)),
        (SEQ[7][0] + 170, "t=%.1f" % (T.get("SEAT", 12.45) + 11.05))]


def load(i):
    return np.asarray(Image.open(RUNS + "\\shot_%s_%02d.png" % (TAKE, i)).convert("RGB")).astype(int)


def masks(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    claw = (r > 150) & (g > 140) & (b > 120) & (np.abs(r - b) < 40)
    wood = (r > g + 15) & (g >= b) & (r > 50)
    return claw, wood


def probe(i):
    """claw band vs the first wood row, per column, over a wide window."""
    a = load(i)
    win = a[300:520, 760:1060]
    claw, wood = masks(win)
    cy = np.where(claw.any(axis=1))[0]
    cx = np.where(claw.any(axis=0))[0]
    if not len(cy):
        print("f%-4d no claw pixels in the window" % i)
        return None
    out = "f%-4d claw_rows=%s claw_cols=%s" % (i, (cy.min() + 300, cy.max() + 300),
                                               (cx.min() + 760, cx.max() + 760))
    for x in range(cx.min() + 760, cx.max() + 760 + 1, 10):
        col_w = np.where(wood[:, x - 760])[0]
        out += "  x%d:wood=%s" % (x, (col_w.min() + 300) if len(col_w) else "-")
    print(out)
    return (cx.min() + 760, cy.min() + 300, cx.max() + 760, cy.max() + 300)


def panel(a, box, label, zoom, grid_step=20):
    x0, y0, x1, y1 = box
    c = a[y0:y1, x0:x1].astype("uint8")
    im = Image.fromarray(c).resize(((x1 - x0) * zoom, (y1 - y0) * zoom), Image.NEAREST)
    d = ImageDraw.Draw(im)
    for gx in range(x0 - x0 % grid_step + grid_step, x1, grid_step):
        X = (gx - x0) * zoom
        d.line([(X, 0), (X, 5)], fill=(255, 255, 255), width=1)
    for gy in range(y0 - y0 % grid_step + grid_step, y1, grid_step):
        Y = (gy - y0) * zoom
        d.line([(0, Y), (5, Y)], fill=(255, 255, 255), width=1)
    band = 24
    out = Image.new("RGB", (im.width, im.height + band), (12, 12, 12))
    out.paste(im, (0, band))
    ImageDraw.Draw(out).text((5, 6), label, fill=(255, 232, 90))
    return out


def grid(panels, cols=4, gap=8):
    w = max(p.width for p in panels)
    h = max(p.height for p in panels)
    rows = (len(panels) + cols - 1) // cols
    out = Image.new("RGB", (cols * w + (cols - 1) * gap, rows * h + (rows - 1) * gap), (0, 0, 0))
    for k, p in enumerate(panels):
        out.paste(p, ((k % cols) * (w + gap), (k // cols) * (h + gap)))
    return out


print("take=%s seat=%.2f frame=%d times=%s" % (TAKE, T.get("SEAT", 12.45), SEAT, T))

box = None
for i, _ in ZOOM:
    got = probe(i)
    if got:
        x0, y0, x1, y1 = got
        # union over every zoom frame: one crop for the whole strip
        box = got if box is None else (min(box[0], x0), min(box[1], y0),
                                       max(box[2], x1), max(box[3], y1))

# auto-crop: the claw band + 45 px above/below, 25 px left/right
if box:
    x0, y0, x1, y1 = box
    ZB = (max(0, x0 - 25), max(0, y0 - 45), min(1300, x1 + 25), y1 + 45)
else:
    ZB = (818, 368, 952, 466)
print("zoom box=%s" % (ZB,))

outA = grid([panel(load(i), (780, 300, 1020, 560), lb, 3) for i, lb in SEQ], cols=4)
pA = REV + r"\%s_seq.png" % TAG
outA.save(pA)
print("saved", pA, outA.size)

outB = grid([panel(load(i), ZB, lb, 8, grid_step=10) for i, lb in ZOOM], cols=4)
pB = REV + r"\%s_zoom.png" % TAG
outB.save(pB)
print("saved", pB, outB.size)

outC = grid([panel(load(i), (780, 300, 1020, 560), lb, 3) for i, lb in HOLD], cols=4)
pC = REV + r"\%s_hold.png" % TAG
outC.save(pC)
print("saved", pC, outC.size)
