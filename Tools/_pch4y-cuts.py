# -*- coding: utf-8 -*-
"""B-PERCH-4 (bird lane, 26.09): the cut of the SECOND exposure-pick take `bPCH4y_tree` (owner
suite; the pick = slot 1, kind Fork, point -0.0754:-0.3744, the least wood overlap after the
wood term; seatAt 12.47, held 11.21 s).

Strip A (sequence, 8 panels): x 780..1020, y 300..560 at 3x - the approach and the settle.
Strip B (contact zoom, 4 panels): x 818..952, y 368..466 at 8x - the claw/wood zone.
Strip C (hold, 4 panels): x 780..1020, y 300..560 at 3x - the seated perch over time.
Prints the claw rows and the wood top edge for the seat frames.
Usage: python Tools/_pch4y-cuts.py
"""
from PIL import Image, ImageDraw
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
REV = RUNS + r"\reviews"
TAKE = "bPCH4y_tree"

SEQ = [(240, "t=12.00"), (244, "t=12.20"), (246, "t=12.30"), (248, "t=12.40"),
       (250, "t=12.50 TOUCH"), (254, "t=12.70"), (258, "t=12.90"), (300, "t=15.00 settled")]
ZOOM = [(246, "t=12.30"), (248, "t=12.40 CONTACT"), (250, "t=12.50"), (300, "t=15.00 hold")]
HOLD = [(300, "t=15.0"), (360, "t=18.0"), (420, "t=21.0"), (470, "t=23.5")]


def load(i):
    return np.asarray(Image.open(RUNS + "\\shot_%s_%02d.png" % (TAKE, i)).convert("RGB")).astype(int)


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


def facts(i):
    a = load(i)
    win = a[380:470, 818:952]
    r, g, b = win[..., 0], win[..., 1], win[..., 2]
    claw = (r > 150) & (g > 140) & (b > 120) & (np.abs(r - g) < 45) & (np.abs(g - b) < 60)
    wood = (r > g + 15) & (g >= b) & (r > 50)
    cy = np.where(claw)[0]
    row = ""
    for x in (840, 850, 860, 870, 880, 890):
        col = np.where(wood[:, x - 818])[0]
        row += " x%d:%s" % (x, (col.min() + 380) if len(col) else "-")
    print("f%-4d claw_rows=%s  wood_top:%s" % (
        i, (cy.min() + 380, cy.max() + 380) if len(cy) else "-", row))


for i, _ in ZOOM:
    facts(i)

outA = grid([panel(load(i), (780, 300, 1020, 560), lb, 3) for i, lb in SEQ], cols=4)
pA = REV + r"\bPCH4y_seq.png"
outA.save(pA)
print("saved", pA, outA.size)

outB = grid([panel(load(i), (818, 368, 952, 466), lb, 8, grid_step=10) for i, lb in ZOOM], cols=4)
pB = REV + r"\bPCH4y_zoom.png"
outB.save(pB)
print("saved", pB, outB.size)

outC = grid([panel(load(i), (780, 300, 1020, 560), lb, 3) for i, lb in HOLD], cols=4)
pC = REV + r"\bPCH4y_hold.png"
outC.save(pC)
print("saved", pC, outC.size)
