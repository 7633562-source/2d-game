# -*- coding: utf-8 -*-
"""B-PERCH-10 (bird lane, 26.09): the cut of the bPCH10b seat take.
The seat window is SHORT (SEAT 12.96 -> RELEASE 13.23: the drive's Fly phase
leaves right after the touch), so the round's own strips sit on the arrival and
the seat's own 0.27 s: _pch7-cuts.py's tails (SEAT+2.55 ...) fall past the
departure and would show an empty branch.
Usage: python Tools/_pch10-cuts.py <take_label> [<out_tag>]
Writes Trials/Runs/reviews/<tag>_seq.png (8 panels, the arrival) and
<tag>_seat.png (8 panels, the seat's own window at 6x).
"""
import os
import sys
from PIL import Image, ImageDraw

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
REV = RUNS + r"\reviews"
TAKE = sys.argv[1] if len(sys.argv) > 1 else "bPCH10b_tree"
TAG = sys.argv[2] if len(sys.argv) > 2 else TAKE

SEQ_BOX = (760, 170, 1000, 410)
SEAT_BOX = (812, 238, 911, 360)
CAMNOTE = ("camera: -camAuto 1 (owner flags) - the frame pans/zooms with the objects; "
           "world points shift between panels")
RATE = 20


def key_times(take):
    t = {}
    with open(RUNS + "\\%s_trigperch.csv" % take, encoding="utf-8", errors="ignore") as f:
        for ln in f:
            p = ln.rstrip("\n").split(",", 2)
            if len(p) >= 2 and p[0] in ("PADS_DOWN", "GUIDE_ON", "SEAT", "RELEASE"):
                t[p[0]] = float(p[1])
    return t


T = key_times(TAKE)
SEAT = T.get("SEAT", 12.96)
REL = T.get("RELEASE", SEAT + 0.27)
SEQ = [(SEAT - 0.55, "t=%.2f" % (SEAT - 0.55)), (SEAT - 0.35, "t=%.2f" % (SEAT - 0.35)),
       (SEAT - 0.20, "t=%.2f" % (SEAT - 0.20)), (SEAT - 0.10, "t=%.2f" % (SEAT - 0.10)),
       (SEAT, "t=%.2f SEAT" % SEAT), (SEAT + 0.06, "t=%.2f" % (SEAT + 0.06)),
       (SEAT + 0.13, "t=%.2f" % (SEAT + 0.13)), (REL, "t=%.2f LEAVE" % REL)]
SEATW = [(SEAT - 0.06, "t=%.2f" % (SEAT - 0.06)), (SEAT - 0.02, "t=%.2f" % (SEAT - 0.02)),
         (SEAT, "t=%.2f SEAT" % SEAT), (SEAT + 0.03, "t=%.2f" % (SEAT + 0.03)),
         (SEAT + 0.08, "t=%.2f" % (SEAT + 0.08)), (SEAT + 0.13, "t=%.2f" % (SEAT + 0.13)),
         (SEAT + 0.18, "t=%.2f" % (SEAT + 0.18)), (SEAT + 0.23, "t=%.2f" % (SEAT + 0.23))]


def load(t):
    i = int(round(t * RATE))
    p = RUNS + "\\shot_%s_%d.png" % (TAKE, i)
    if not os.path.exists(p):
        best = None
        for d in range(1, 6):
            for s in (-d, d):
                q = RUNS + "\\shot_%s_%d.png" % (TAKE, i + s)
                if os.path.exists(q):
                    best = q
                    break
            if best:
                break
        if best is None:
            raise SystemExit("no shot near t=%.2f" % t)
        p = best
    return Image.open(p).convert("RGB")


def panel(img, box, label, scale, grid_step=8):
    x0, y0, x1, y1 = box
    crop = img.crop((x0, y0, x1, y1))
    w, h = crop.size
    crop = crop.resize((w * scale, h * scale), Image.NEAREST)
    if grid_step > 0:
        d = ImageDraw.Draw(crop)
        for x in range(0, w * scale, grid_step * scale):
            d.line([(x, 0), (x, h * scale)], fill=(40, 40, 40), width=1)
        for y in range(0, h * scale, grid_step * scale):
            d.line([(0, y), (w * scale, y)], fill=(40, 40, 40), width=1)
    d = ImageDraw.Draw(crop)
    d.rectangle([0, 0, w * scale - 1, h * scale - 1], outline=(255, 255, 0), width=2)
    d.text((6, 4), label, fill=(255, 255, 0))
    return crop


def grid(panels, cols, foot=""):
    w = max(p.size[0] for p in panels)
    h = max(p.size[1] for p in panels)
    gap = 6
    fh = 22 if foot else 0
    rows = (len(panels) + cols - 1) // cols
    out = Image.new("RGB", (cols * w + (cols - 1) * gap, rows * h + (rows - 1) * gap + fh),
                    (0, 0, 0))
    for k, p in enumerate(panels):
        out.paste(p, ((k % cols) * (w + gap), (k // cols) * (h + gap)))
    if foot:
        ImageDraw.Draw(out).text((6, rows * h + (rows - 1) * gap + 5), foot, fill=(180, 220, 255))
    return out


print("take=%s seat=%.2f rel=%.2f times=%s" % (TAKE, SEAT, REL, T))
pA = REV + r"\%s_seq.png" % TAG
outA = grid([panel(load(t), SEQ_BOX, lb, 3) for t, lb in SEQ], cols=4, foot=CAMNOTE)
outA.save(pA)
print("saved", pA, outA.size)
pB = REV + r"\%s_seat.png" % TAG
outB = grid([panel(load(t), SEAT_BOX, lb, 6, grid_step=10) for t, lb in SEATW], cols=4, foot=CAMNOTE)
outB.save(pB)
print("saved", pB, outB.size)
