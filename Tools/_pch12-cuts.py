# -*- coding: utf-8 -*-
"""B-PERCH-12 (bird lane, 26.09): the cut of the seat take (the level-band pick +
the seat-stays-on-the-mark's-band rule). Strips: the arrival/seat sequence, the
contact zoom over the hold, the leave.
Usage: python Tools/_pch12-cuts.py <take_label> [<tag>]
Writes Trials/Runs/reviews/<tag>_seq.png, _seat.png, _leave.png
Boxes default to the bPCH12a framing; override with env PCH12_SEQBOX / PCH12_SEATBOX
as "x0,y0,x1,y1".
"""
import os
import sys
from PIL import Image, ImageDraw

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
REV = RUNS + r"\reviews"
TAKE = sys.argv[1] if len(sys.argv) > 1 else "bPCH12b"
TAG = sys.argv[2] if len(sys.argv) > 2 else TAKE


def box_from_env(name, dflt):
    v = os.environ.get(name)
    if not v:
        return dflt
    return tuple(int(x) for x in v.split(","))


SEQ_BOX = box_from_env("PCH12_SEQBOX", (860, 250, 1100, 490))
SEAT_BOX = box_from_env("PCH12_SEATBOX", (900, 330, 1030, 452))
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
SEAT = T.get("SEAT", 13.0)
REL = T.get("RELEASE", SEAT + 2.77)
SEQ = [(SEAT - 0.55, "t=%.2f" % (SEAT - 0.55)), (SEAT - 0.35, "t=%.2f" % (SEAT - 0.35)),
       (SEAT - 0.20, "t=%.2f" % (SEAT - 0.20)), (SEAT - 0.10, "t=%.2f" % (SEAT - 0.10)),
       (SEAT, "t=%.2f SEAT" % SEAT), (SEAT + 0.10, "t=%.2f" % (SEAT + 0.10)),
       (SEAT + 1.30, "t=%.2f settled" % (SEAT + 1.30)),
       (SEAT + 2.20, "t=%.2f settled" % (SEAT + 2.20))]
SEATW = [(SEAT - 0.04, "t=%.2f" % (SEAT - 0.04)), (SEAT, "t=%.2f SEAT" % SEAT),
         (SEAT + 0.06, "t=%.2f" % (SEAT + 0.06)), (SEAT + 0.30, "t=%.2f" % (SEAT + 0.30)),
         (SEAT + 0.90, "t=%.2f" % (SEAT + 0.90)), (SEAT + 1.50, "t=%.2f" % (SEAT + 1.50)),
         (SEAT + 2.10, "t=%.2f" % (SEAT + 2.10)), (SEAT + 2.20, "t=%.2f" % (SEAT + 2.20))]
LEAVE = [(REL - 0.10, "t=%.2f" % (REL - 0.10)), (REL, "t=%.2f RELEASE" % REL),
         (REL + 0.10, "t=%.2f" % (REL + 0.10)), (REL + 0.25, "t=%.2f" % (REL + 0.25)),
         (REL + 0.45, "t=%.2f" % (REL + 0.45)), (REL + 0.75, "t=%.2f" % (REL + 0.75))]


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
    d = ImageDraw.Draw(crop)
    if grid_step > 0:
        for x in range(0, w * scale, grid_step * scale):
            d.line([(x, 0), (x, h * scale)], fill=(40, 40, 40), width=1)
        for y in range(0, h * scale, grid_step * scale):
            d.line([(0, y), (w * scale, y)], fill=(40, 40, 40), width=1)
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
a = REV + r"\%s_seq.png" % TAG
grid([panel(load(t), SEQ_BOX, lb, 3) for t, lb in SEQ], cols=4, foot=CAMNOTE).save(a)
print("saved", a)
b = REV + r"\%s_seat.png" % TAG
grid([panel(load(t), SEAT_BOX, lb, 6, grid_step=10) for t, lb in SEATW], cols=4, foot=CAMNOTE).save(b)
print("saved", b)
c = REV + r"\%s_leave.png" % TAG
grid([panel(load(t), SEQ_BOX, lb, 3) for t, lb in LEAVE], cols=3, foot=CAMNOTE).save(c)
print("saved", c)
