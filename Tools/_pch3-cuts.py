# -*- coding: utf-8 -*-
"""B-PERCH-3 (bird lane, 25.09): the FULL landing sequence on the LIFT take `bPCH2l_tree`
(seatDy -0.037, the standing ring7 suite: static camera, wind 0) - the same lens/crops as the
bPCH2 cuts so the two strips compare frame-for-frame.
Landing window (events): perch 11.855, takeoff 11.860, turn/guide 12.305, touchdown+flare
12.935, bounce settle 12.955-13.01, seat 13.07, first wood-solid 13.075 (t = idx/20).

Strip A (sequence, 8 panels): x 875..1010, y 495..690 at 4x.
Strip B (contact zoom, 4 panels): x 905..945, y 592..624 at 16x - the claw/wood zone.
Prints the claw rows and the wood top edge for the contact frames.
Usage: python Tools/_pch3-cuts.py
"""
from PIL import Image, ImageDraw
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
REV = RUNS + r"\reviews"
TAKE = "bPCH2l_tree"

SEQ = [(234, "t=11.7 approach"), (238, "t=11.9"), (242, "t=12.1"),
       (248, "t=12.4 turn"), (254, "t=12.7"), (258, "t=12.9 flare"),
       (261, "t=13.05 touch"), (268, "t=13.4 settled")]
ZOOM = [(259, "t=12.95 feet arrive"), (261, "t=13.05 CONTACT"),
        (263, "t=13.15"), (268, "t=13.4 hold")]


def load(i):
    return np.asarray(Image.open(f"{RUNS}\\shot_{TAKE}_{i}.png").convert("RGB")).astype(int)


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
    win = a[585:630, 895:955]
    r, g, b = win[..., 0], win[..., 1], win[..., 2]
    claw = (r > 150) & (g > 140) & (b > 120) & (np.abs(r - g) < 45) & (np.abs(g - b) < 60)
    wood = (r > g + 15) & (g >= b) & (r > 50)
    cy = np.where(claw)[0]
    row = ""
    for x in (905, 915, 920, 925, 930, 935):
        col = np.where(wood[:, x - 895])[0]
        row += " x%d:%s" % (x, (col.min() + 585) if len(col) else "-")
    print("f%-4d claw_rows=%s  wood_top:%s" % (
        i, (cy.min() + 585, cy.max() + 585) if len(cy) else "-", row))


for i, _ in SEQ:
    facts(i)

outA = grid([panel(load(i), (875, 495, 1010, 690), lb, 4) for i, lb in SEQ], cols=4)
pA = REV + r"\bPCH3_seq.png"
outA.save(pA)
print("saved", pA, outA.size)
outB = grid([panel(load(i), (905, 592, 945, 624), lb, 16, grid_step=10) for i, lb in ZOOM], cols=4)
pB = REV + r"\bPCH3_zoom.png"
outB.save(pB)
print("saved", pB, outB.size)
