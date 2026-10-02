# -*- coding: utf-8 -*-
"""B-PERCH-5 (bird lane, 26.09): the cut of the slot-7 MEASURED-SEAT verification take
`bPCH5a_tree` (suite owner7auto: owner flags + -perchSlot 7 + -perchSeatAuto 1; the
probe's own value seated: ARM seat=-0.0054 seatMeasured=-0.0054 seatHits=0.2372;0.1736;0.0771;0.0624;0.018).
Landing window (events): pads down 11.87, guide on 12.32, guide off 13.02, seat 13.07,
held 11.6552 s, release 24.7252.

Strip A (sequence, 8 panels): x 785..955, y 140..330 at 4x - the owner-scale landing.
Strip B (contact zoom, 4 panels): x 815..862, y 230..272 at 14x - the claw/wood zone
   (the same box as the bPCH3d cut, so the two seat values compare panel to panel).
Strip C (hold, 4 panels): x 785..950, y 150..335 at 4x - the seated perch over time.
Prints the claw rows and the wood top edge for the contact frames.
Usage: python Tools/_pch5a-cuts.py
"""
from PIL import Image, ImageDraw
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
REV = RUNS + r"\reviews"
TAKE = "bPCH5a_tree"

SEQ = [(240, "t=12.00"), (250, "t=12.50"), (256, "t=12.80"), (259, "t=12.95"),
       (262, "t=13.10 TOUCH"), (265, "t=13.25"), (270, "t=13.50"), (300, "t=15.00 settled")]
ZOOM = [(258, "t=12.90"), (261, "t=13.05 CONTACT"), (263, "t=13.15"), (300, "t=15.00 hold")]
HOLD = [(300, "t=15.0"), (400, "t=20.0"), (500, "t=25.0"), (660, "t=33.0")]


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
    """claw (light) rows and wood top edge in the contact window."""
    a = load(i)
    win = a[226:276, 815:865]
    r, g, b = win[..., 0], win[..., 1], win[..., 2]
    claw = (r > 150) & (g > 140) & (b > 120) & (np.abs(r - g) < 45) & (np.abs(g - b) < 60)
    wood = (r > g + 15) & (g >= b) & (r > 50)
    cy = np.where(claw)[0]
    row = ""
    for x in (840, 850, 855, 860, 865):
        col = np.where(wood[:, x - 830])[0]
        row += " x%d:%s" % (x, (col.min() + 225) if len(col) else "-")
    print("f%-4d claw_rows=%s  wood_top:%s" % (
        i, (cy.min() + 225, cy.max() + 225) if len(cy) else "-", row))


for i, _ in ZOOM:
    facts(i)

outA = grid([panel(load(i), (785, 140, 955, 330), lb, 4) for i, lb in SEQ], cols=4)
pA = REV + r"\bPCH5a_seq.png"
outA.save(pA)
print("saved", pA, outA.size)

outB = grid([panel(load(i), (815, 230, 862, 272), lb, 14, grid_step=10) for i, lb in ZOOM], cols=4)
pB = REV + r"\bPCH5a_zoom.png"
outB.save(pB)
print("saved", pB, outB.size)

outC = grid([panel(load(i), (785, 150, 950, 335), lb, 4) for i, lb in HOLD], cols=4)
pC = REV + r"\bPCH5a_hold.png"
outC.save(pC)
print("saved", pC, outC.size)
