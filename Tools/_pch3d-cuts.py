# -*- coding: utf-8 -*-
"""B-PERCH-4 (bird lane, 26.09): the cut of the OWNER-SUITE verification take `bPCH3d_tree`
(owner launcher flags as-is, camAuto framing, seatDy -0.037 adopted default, build 61b33d87).
Landing window (events): perch/takeoff 11.855, turn 12.305, touchdown+flare 12.895, bounce
12.910, seat 12.97, held 11.66 s, release 24.63.

Strip A (sequence, 8 panels): x 785..955, y 140..330 at 4x - the owner-scale landing.
Strip B (contact zoom, 4 panels): x 815..862, y 232..266 at 14x - the claw/wood zone.
   (owner-scale seat geometry, measured: feet block x826..842, rows 238..249; wood top edge y 249..250;
    leaf blocks touch the feet's left edge x825..827 and right x843..847 - the pocket.)
Strip C (hold, 4 panels): x 785..950, y 150..335 at 4x - the seated perch over time.
Prints the claw rows and the wood top edge for the contact frames.
Usage: python Tools/_pch3d-cuts.py
"""
from PIL import Image, ImageDraw
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
REV = RUNS + r"\reviews"
TAKE = "bPCH3d_tree"

SEQ = [(234, "t=11.70"), (244, "t=12.20"), (248, "t=12.40"), (252, "t=12.60"),
       (256, "t=12.80"), (258, "t=12.90 TOUCH"), (262, "t=13.10"), (268, "t=13.40 settled")]
ZOOM = [(256, "t=12.80"), (258, "t=12.90 CONTACT"), (260, "t=13.00"), (268, "t=13.40 hold")]
HOLD = [(300, "t=15.0"), (400, "t=20.0"), (480, "t=24.0"), (620, "t=31.0")]


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
pA = REV + r"\bPCH3d_seq.png"
outA.save(pA)
print("saved", pA, outA.size)

outB = grid([panel(load(i), (815, 230, 862, 272), lb, 14, grid_step=10) for i, lb in ZOOM], cols=4)
pB = REV + r"\bPCH3d_zoom.png"
outB.save(pB)
print("saved", pB, outB.size)

outC = grid([panel(load(i), (785, 150, 950, 335), lb, 4) for i, lb in HOLD], cols=4)
pC = REV + r"\bPCH3d_hold.png"
outC.save(pC)
print("saved", pC, outC.size)
